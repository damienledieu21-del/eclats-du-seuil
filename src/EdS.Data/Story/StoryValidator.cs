using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Guild;
using EdS.Data.Items;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Space;

namespace EdS.Data.Story
{
    public sealed class TextsLoadResult
    {
        public ValidationReport Report { get; }
        public TextCatalog? Texts { get; }
        public bool IsValid => Texts != null;

        internal TextsLoadResult(ValidationReport report, TextCatalog? texts)
        {
            Report = report;
            Texts = texts;
        }
    }

    public sealed class StoryLoadResult
    {
        public ValidationReport Report { get; }
        public StoryBook? Story { get; }
        public bool IsValid => Story != null;

        internal StoryLoadResult(ValidationReport report, StoryBook? story)
        {
            Report = report;
            Story = story;
        }
    }

    /// <summary>Sous-lot 12.5 (cadrage Q37) : validateur unique des textes d'une langue (<c>kind: "texts"</c>).</summary>
    public static class TextsValidator
    {
        public const string Kind = "texts";
        public const int CurrentSchemaVersion = 1;
        public const int MaxEntries = 10_000, MaxLength = 2_000;

        private static readonly Regex Language = new Regex(@"^[a-z]{2}\z", RegexOptions.CultureInvariant);
        private static readonly Regex Key = new Regex(@"^text(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);

        public static bool IsKey(string? key) => key != null && key.Length <= 200 && Key.IsMatch(key);

        public static TextsLoadResult Validate(TextsDocument document, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            report ??= new ValidationReport();
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, CurrentSchemaVersion, report))
                return new TextsLoadResult(report, null);
            TextsData? d = document.Data;
            if (d is null) { Missing(report, "$.data", "langue et textes"); return new TextsLoadResult(report, null); }
            if (d.Language is null) Missing(report, "$.data.language", "langue, « fr » ou « en »");
            else if (!Language.IsMatch(d.Language)) report.Add("$.data.language", ValidationCode.InvalidValue, "langue en deux lettres minuscules attendue.");
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            if (d.Entries is null) Missing(report, "$.data.entries", "textes par clé");
            else
            {
                if (d.Entries.Count > MaxEntries) report.Add("$.data.entries", ValidationCode.LimitExceeded, $"{d.Entries.Count} textes, au plus {MaxEntries}.");
                foreach (KeyValuePair<string, string?> e in d.Entries)
                {
                    string path = "$.data.entries." + e.Key;
                    if (!IsKey(e.Key)) report.Add(path, ValidationCode.InvalidValue, "clé invalide : « text. » puis minuscules, chiffres, « _ » et « . ».");
                    else if (string.IsNullOrWhiteSpace(e.Value)) Missing(report, path, "texte");
                    else if (e.Value!.Length > MaxLength) report.Add(path, ValidationCode.OutOfRange, $"texte de plus de {MaxLength} caractères.");
                    // Même règle que la revue Codex de la PR #44 : une clé une seule fois (catalogue construit en objets publics).
                    else if (entries.ContainsKey(e.Key)) report.Add(path, ValidationCode.DuplicateId, $"clé « {e.Key} » déjà donnée plus haut.");
                    else entries[e.Key] = e.Value;
                }
            }
            if (!report.IsValid || document.ContentVersion is null) return new TextsLoadResult(report, null);
            return new TextsLoadResult(report, new TextCatalog(d.Language!, document.ContentVersion, entries));
        }

        internal static void Missing(ValidationReport report, string path, string what)
        {
            if (!report.HasErrorAtOrAbove(path)) report.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({what}).");
        }
    }

    /// <summary>
    /// Sous-lot 12.5 : validateur unique de l'histoire (<c>kind: "story"</c>), contre les textes, la zone et le catalogue des objets :
    /// identifiants uniques et bien formés, textes existants, références connues (PNJ, dialogues, nœuds, quêtes, étapes, lieux, passages,
    /// populations, objets, découvertes), valeurs bornées, et chaque fait attendu (condition ou objectif) appris par au moins un effet.
    /// </summary>
    public static class StoryValidator
    {
        public const string Kind = "story";

        /// <summary>
        /// Version 1 (sous-lot 12.5) : PNJ, dialogues, quêtes, découvertes. Version 2 (sous-lot V1.1) : effet <c>giveMoney</c> (pièces de
        /// cuivre données à la bourse, de 1 à <see cref="MaxMoney"/>). Version 3 (sous-lot V1.6) : catégorie de quête obligatoire
        /// (<c>category</c> : « main » ou « side »), conditions d'ouverture (<c>requires</c>) et récompenses au choix (<c>rewardChoice</c>,
        /// deux ou trois <c>giveItem</c>) facultatives ; objectifs <c>talk</c>, <c>deliver</c> (avec <c>npc</c>), <c>defeatFamily</c>,
        /// <c>equipped</c> ; effet <c>takeMoney</c> ; condition <c>levelAtLeast</c> ; zone facultative d'un PNJ (<c>zone</c> : un intérieur) ;
        /// étape « reward » réservée. Version 4 (sous-lot V1.7) : objectif <c>guildRank</c> (atteindre un rang de la Guilde des aventuriers),
        /// effet <c>giveGuildPoints</c> (points de guilde, de 1 à <see cref="GuildRanks.MaxAward"/>) et condition <c>guildRankAtLeast</c>.
        /// Version 5 (sous-lot V1.9.3b) : objectif <c>craftQuality</c> (avoir fabriqué un équipement d'une qualité minimale : <c>id</c> de
        /// l'objet, qui prend une qualité, <c>quality</c> « superior » ou « masterwork », <c>value</c> nombre d'instances) ; le champ
        /// <c>quality</c> n'appartient qu'à cet objectif. Un fichier en version 4 (<see cref="PreviousSchemaVersion"/>) reste lu tel quel (il
        /// ne connaît pas <c>craftQuality</c>) ; une version plus ancienne est refusée (pré-alpha : aucune migration des fichiers de données).
        /// </summary>
        public const int CurrentSchemaVersion = 5;

        /// <summary>Sous-lot V1.9.3b : version précédente, encore lue (sous-ensemble de la version 5, sans l'objectif <c>craftQuality</c>).</summary>
        public const int PreviousSchemaVersion = 4;

        /// <summary>Sous-lot V1.9.3b : qualités minimales d'un objectif <c>craftQuality</c> (la qualité normale ne se demande pas).</summary>
        public static IReadOnlyDictionary<string, ItemQuality> Qualities { get; } = new ReadOnlyDictionary<string, ItemQuality>(new Dictionary<string, ItemQuality>(StringComparer.Ordinal)
        {
            ["superior"] = ItemQuality.Superior, ["masterwork"] = ItemQuality.Masterwork,
        });

        /// <summary>Sous-lot V1.6 : niveau le plus haut d'une condition <c>levelAtLeast</c> (les règles en permettent 30 ; marge).</summary>
        public const long MaxLevel = 100;

        /// <summary>Sous-lot V1.6 : récompenses au choix d'une quête, de 2 à 3 (« un objet parmi deux ou trois »).</summary>
        public const int MinRewardChoices = 2, MaxRewardChoices = 3;

        /// <summary>Sous-lot V1.1 : pièces de cuivre données par un effet, au plus (10 000 or).</summary>
        public const long MaxMoney = 100_000_000;
        public const int MaxNpcs = 64, MaxDialogues = 64, MaxNodes = 64, MaxChoices = 8, MaxEntries = 16, MaxQuests = 64, MaxSteps = 32, MaxTerms = 8,
            MaxDiscoveries = 256;
        public const long MaxTrust = 100, MaxCount = 999, MaxXp = 1_000_000, MaxDefeat = 1000;

        // Tables globales réellement non modifiables (une table partagée modifiée changerait toutes les validations suivantes du processus).
        public static IReadOnlyDictionary<string, ConditionKind> Conditions { get; } = new ReadOnlyDictionary<string, ConditionKind>(new Dictionary<string, ConditionKind>(StringComparer.Ordinal)
        {
            ["fact"] = ConditionKind.Fact, ["notFact"] = ConditionKind.NotFact, ["questNotStarted"] = ConditionKind.QuestNotStarted,
            ["questActive"] = ConditionKind.QuestActive, ["questAt"] = ConditionKind.QuestAt, ["questDone"] = ConditionKind.QuestDone,
            ["trustAtLeast"] = ConditionKind.TrustAtLeast, ["trustBelow"] = ConditionKind.TrustBelow, ["hasItem"] = ConditionKind.HasItem,
            ["phase"] = ConditionKind.Phase, ["inPlace"] = ConditionKind.InPlace, ["passageOpen"] = ConditionKind.PassageOpen,
            ["levelAtLeast"] = ConditionKind.LevelAtLeast, ["questReward"] = ConditionKind.QuestReward,
            ["guildRankAtLeast"] = ConditionKind.GuildRankAtLeast,   // sous-lot V1.7
        });

        public static IReadOnlyDictionary<string, EffectKind> Effects { get; } = new ReadOnlyDictionary<string, EffectKind>(new Dictionary<string, EffectKind>(StringComparer.Ordinal)
        {
            ["setFact"] = EffectKind.SetFact, ["trust"] = EffectKind.Trust, ["startQuest"] = EffectKind.StartQuest, ["giveItem"] = EffectKind.GiveItem,
            ["takeItem"] = EffectKind.TakeItem, ["giveXp"] = EffectKind.GiveXp, ["openPassage"] = EffectKind.OpenPassage, ["discover"] = EffectKind.Discover,
            ["giveMoney"] = EffectKind.GiveMoney, ["takeMoney"] = EffectKind.TakeMoney,
            ["giveGuildPoints"] = EffectKind.GiveGuildPoints,   // sous-lot V1.7
        });

        public static IReadOnlyDictionary<string, ObjectiveKind> Objectives { get; } = new ReadOnlyDictionary<string, ObjectiveKind>(new Dictionary<string, ObjectiveKind>(StringComparer.Ordinal)
        {
            ["fact"] = ObjectiveKind.Fact, ["reach"] = ObjectiveKind.Reach, ["defeat"] = ObjectiveKind.Defeat, ["collect"] = ObjectiveKind.Collect,
            ["passage"] = ObjectiveKind.Passage, ["talk"] = ObjectiveKind.Talk, ["deliver"] = ObjectiveKind.Deliver,
            ["defeatFamily"] = ObjectiveKind.DefeatFamily, ["equipped"] = ObjectiveKind.Equipped,
            ["guildRank"] = ObjectiveKind.GuildRank,   // sous-lot V1.7
            ["craftQuality"] = ObjectiveKind.CraftQuality,   // sous-lot V1.9.3b (schéma 5)
        });

        /// <summary>Sous-lot V1.6 : catégories de quête (onglets du journal).</summary>
        public static IReadOnlyDictionary<string, QuestCategory> Categories { get; } = new ReadOnlyDictionary<string, QuestCategory>(new Dictionary<string, QuestCategory>(StringComparer.Ordinal)
        {
            ["main"] = QuestCategory.Main, ["side"] = QuestCategory.Side,
        });

        public static readonly IReadOnlyList<string> Phases = new ReadOnlyCollection<string>(new[] { "dawn", "day", "dusk", "night" });

        private static Regex Id(string prefix) => new Regex($@"^{prefix}(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);

        private static readonly Regex NpcId = Id("npc"), DialogueId = Id("dialogue"), QuestId = Id("quest"), FactId = Id("fact"), DiscoveryId = Id("discovery");
        private static readonly Regex LocalId = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

        public static bool IsFactId(string? id) => id != null && id.Length <= 200 && FactId.IsMatch(id);

        /// <param name="interiors">Sous-lot V1.6 : autres zones du monde (intérieurs), où un PNJ peut vivre (<c>zone</c>) ; null : aucune.</param>
        /// <param name="levelCap">Relecture indépendante de la PR #82 : plafond de niveau actuel (progression) ; une condition
        /// <c>levelAtLeast</c> au-dessus reçoit un avertissement (personne ne l'atteint tant que le plafond ne monte pas). Null : pas de contrôle.</param>
        public static StoryLoadResult Validate(StoryDocument document, TextCatalog texts, ZoneDefinition zone, ItemCatalog items, ValidationReport? report = null,
            IReadOnlyList<ZoneDefinition>? interiors = null, int? levelCap = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (texts is null) throw new ArgumentNullException(nameof(texts));
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            if (items is null) throw new ArgumentNullException(nameof(items));
            report ??= new ValidationReport();
            // Sous-lot V1.9.3b : la version 4 est encore lue (sous-ensemble de la version 5).
            int supported = document.SchemaVersion == PreviousSchemaVersion ? PreviousSchemaVersion : CurrentSchemaVersion;
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, supported, report))
                return new StoryLoadResult(report, null);
            StoryData? d = document.Data;
            if (d is null) { TextsValidator.Missing(report, "$.data", "PNJ, dialogues, quêtes, découvertes"); return new StoryLoadResult(report, null); }
            var c = new Checker(report, texts, zone, items, d, interiors ?? Array.Empty<ZoneDefinition>()) { LevelCap = levelCap };
            StoryBook? book = c.Run(document.SchemaVersion!.Value, document.ContentVersion);
            return new StoryLoadResult(report, report.IsValid ? book : null);
        }

        private sealed class Checker
        {
            private readonly ValidationReport _r;
            private readonly TextCatalog _texts;
            private readonly ZoneDefinition _zone;
            private readonly ItemCatalog _items;
            private readonly StoryData _d;
            private readonly HashSet<string> _npcs = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> _dialogues = new HashSet<string>(StringComparer.Ordinal);
            private readonly Dictionary<string, List<string>> _quests = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            private readonly HashSet<string> _discoveries = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> _factsSet = new HashSet<string>(StringComparer.Ordinal);
            private readonly List<(string Fact, string Path)> _factsNeeded = new List<(string, string)>();
            private readonly IReadOnlyList<ZoneDefinition> _interiors;

            /// <summary>Plafond de niveau actuel, pour l'avertissement des conditions <c>levelAtLeast</c> trop hautes (null : aucun).</summary>
            public int? LevelCap { get; set; }

            public Checker(ValidationReport report, TextCatalog texts, ZoneDefinition zone, ItemCatalog items, StoryData data, IReadOnlyList<ZoneDefinition> interiors)
            {
                _interiors = interiors;
                _r = report;
                _texts = texts;
                _zone = zone;
                _items = items;
                _d = data;
            }

            /// <summary>Sous-lot V1.9.3b : version du fichier lu (l'objectif <c>craftQuality</c> demande la version 5).</summary>
            private int _schema;

            public StoryBook? Run(int schemaVersion, string? contentVersion)
            {
                _schema = schemaVersion;
                // Premier passage : identifiants déclarés (les références peuvent viser plus loin dans le fichier).
                Declare(_d.Npcs, "$.data.npcs", MaxNpcs, n => n?.Id, NpcId, _npcs);
                Declare(_d.Dialogues, "$.data.dialogues", MaxDialogues, x => x?.Id, DialogueId, _dialogues);
                var questIds = new HashSet<string>(StringComparer.Ordinal);
                Declare(_d.Quests, "$.data.quests", MaxQuests, q => q?.Id, QuestId, questIds);
                if (_d.Quests != null)
                    foreach (QuestData? q in _d.Quests)
                        if (q?.Id is string id && questIds.Contains(id) && !_quests.ContainsKey(id))
                            _quests[id] = (q.Steps ?? new List<QuestStepData?>()).Select(s => s?.Id ?? string.Empty).ToList();
                Declare(_d.Discoveries, "$.data.discoveries", MaxDiscoveries, x => x?.Id, DiscoveryId, _discoveries);
                // Relecture de la PR #81 : le signe d'une créature rare de la nuit (zones en schéma 6) est un texte du jeu : il doit exister,
                // comme les autres clés de texte. La zone est validée sans les textes ; l'histoire, qui les a, vérifie ses signes (chemin dans
                // le fichier de la zone, précédé de son identifiant).
                for (int i = 0; i < _zone.Populations.Count; i++)
                    if (_zone.Populations[i].Night is NightSpawn night)
                        Text(night.Sign, $"{_zone.Id}:$.data.populations[{i}].night.sign");

                var discoveries = new List<Discovery>();
                if (_d.Discoveries != null)
                    for (int i = 0; i < _d.Discoveries.Count; i++)
                        if (_d.Discoveries[i] is { } x && Text(x.Text, $"$.data.discoveries[{i}].text") && x.Id != null)
                            discoveries.Add(new Discovery(x.Id, x.Text!));

                var npcs = new List<Npc>();
                if (_d.Npcs != null)
                    for (int i = 0; i < _d.Npcs.Count; i++)
                    {
                        string path = $"$.data.npcs[{i}]";
                        if (_d.Npcs[i] is not NpcData n) continue;
                        bool ok = Text(n.Name, path + ".name");
                        // Sous-lot V1.6 : un PNJ peut vivre dans un intérieur (une autre zone du monde) ; sa position est alors dans cette zone.
                        ZoneDefinition home = _zone;
                        if (n.Zone != null)
                        {
                            if (n.Zone == _zone.Id) { _r.Add(path + ".zone", ValidationCode.InvalidValue, "zone de l'histoire : ne pas la donner."); ok = false; }
                            else if (_interiors.FirstOrDefault(z => z.Id == n.Zone) is ZoneDefinition inside) home = inside;
                            else { _r.Add(path + ".zone", ValidationCode.UnknownReference, $"zone inconnue « {n.Zone} » (une zone reliée par des portes attendue)."); ok = false; }
                        }
                        if (n.Position is null || n.Position.X is null || n.Position.Z is null) { TextsValidator.Missing(_r, path + ".position", "[x, z]"); ok = false; }
                        else if (!Inside(Point(n.Position), home)) { _r.Add(path + ".position", ValidationCode.OutOfRange, "PNJ hors de l'aire praticable de sa zone."); ok = false; }
                        if (n.Dialogue is null) { TextsValidator.Missing(_r, path + ".dialogue", "dialogue du PNJ"); ok = false; }
                        else if (!_dialogues.Contains(n.Dialogue)) { _r.Add(path + ".dialogue", ValidationCode.UnknownReference, $"dialogue inconnu « {n.Dialogue} »."); ok = false; }
                        if (ok && n.Id != null) npcs.Add(new Npc(n.Id, n.Name!, Point(n.Position!), n.Dialogue!, n.Zone));
                    }

                var dialogues = new List<Dialogue>();
                if (_d.Dialogues != null)
                    for (int i = 0; i < _d.Dialogues.Count; i++)
                        if (_d.Dialogues[i] is DialogueData x && CheckDialogue(x, $"$.data.dialogues[{i}]") is { } dialogue) dialogues.Add(dialogue);

                var quests = new List<Quest>();
                if (_d.Quests != null)
                    for (int i = 0; i < _d.Quests.Count; i++)
                        if (_d.Quests[i] is QuestData q && CheckQuest(q, $"$.data.quests[{i}]") is { } quest) quests.Add(quest);

                CheckRequirementCycles(quests);
                // Sous-lot 12.7 : la victoire sur le chef de la zone apprend son fait ; sous-lot V1.6 : celle sur chaque chef aussi.
                foreach (Boss boss in _zone.Bosses) _factsSet.Add(boss.Fact);
                // Un fait attendu doit pouvoir être appris : sinon la condition ou l'objectif ne serait jamais rempli.
                foreach (var (fact, path) in _factsNeeded)
                    if (!_factsSet.Contains(fact))
                        _r.Add(path, ValidationCode.UnknownReference, $"fait « {fact} » jamais appris (aucun effet setFact).");
                if (!_r.IsValid || contentVersion is null) return null;
                return new StoryBook(schemaVersion, contentVersion, npcs, dialogues, quests, discoveries);
            }

            private void Declare<T>(List<T?>? list, string path, int max, Func<T?, string?> id, Regex pattern, HashSet<string> ids) where T : class
            {
                if (list is null) { TextsValidator.Missing(_r, path, "liste, éventuellement vide"); return; }
                if (list.Count > max) _r.Add(path, ValidationCode.LimitExceeded, $"{list.Count} éléments, au plus {max}.");
                for (int i = 0; i < list.Count; i++)
                {
                    string p = $"{path}[{i}]";
                    if (list[i] is null) { TextsValidator.Missing(_r, p, "élément"); continue; }
                    string? value = id(list[i]);
                    if (value is null) TextsValidator.Missing(_r, p + ".id", "identifiant");
                    else if (value.Length > 200 || !pattern.IsMatch(value)) _r.Add(p + ".id", ValidationCode.InvalidValue, $"identifiant « {value} » mal formé.");
                    else if (!ids.Add(value)) _r.Add(p + ".id", ValidationCode.DuplicateId, $"identifiant « {value} » déjà donné plus haut.");
                }
            }

            /// <summary>
            /// Relecture de la PR #76 : les conditions d'ouverture « après une autre quête » (questDone, questActive, questAt, questReward) ne
            /// forment pas de cycle (A s'ouvre après B, B après A : aucune ne s'ouvrirait). Chaque condition d'un cycle est refusée, à son
            /// chemin <c>quests[i].requires[j]</c>.
            /// </summary>
            private void CheckRequirementCycles(IReadOnlyList<Quest> quests)
            {
                static bool Gate(StoryCondition c) => c.Kind is ConditionKind.QuestDone or ConditionKind.QuestActive or ConditionKind.QuestAt or ConditionKind.QuestReward;
                // Revue Codex de la PR #76 : un identifiant de quête donné deux fois est déjà refusé (Declare) ; seule sa première quête compte
                // ici (jamais d'exception avant le rapport).
                var edges = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (Quest q in quests)
                    if (!edges.ContainsKey(q.Id))
                        edges[q.Id] = q.Requires.Where(Gate).Select(c => c.Id).Where(t => t != q.Id).Distinct(StringComparer.Ordinal).ToList();
                bool Reaches(string from, string to)
                {
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    var stack = new Stack<string>();
                    stack.Push(from);
                    while (stack.Count > 0)
                    {
                        string q = stack.Pop();
                        if (q == to) return true;
                        if (!seen.Add(q) || !edges.TryGetValue(q, out List<string> next)) continue;
                        foreach (string n in next) stack.Push(n);
                    }
                    return false;
                }
                if (_d.Quests is null) return;
                for (int i = 0; i < _d.Quests.Count; i++)
                {
                    if (_d.Quests[i]?.Id is not string id || quests.FirstOrDefault(q => q.Id == id) is not Quest quest) continue;
                    if (_d.Quests.Take(i).Any(earlier => earlier?.Id == id)) continue;   // doublon : déjà refusé à son identifiant
                    for (int j = 0; j < quest.Requires.Count; j++)
                    {
                        StoryCondition c = quest.Requires[j];
                        if (Gate(c) && c.Id != id && Reaches(c.Id, id))
                            _r.Add($"$.data.quests[{i}].requires[{j}]", ValidationCode.InvalidValue, $"cycle d'ouverture : « {c.Id} » s'ouvre elle-même après « {id} ».");
                    }
                }
            }

            private Dialogue? CheckDialogue(DialogueData x, string path)
            {
                bool ok = true;
                var nodeIds = new HashSet<string>(StringComparer.Ordinal);
                if (x.Nodes is null) { TextsValidator.Missing(_r, path + ".nodes", "nœuds"); return null; }
                if (x.Nodes.Count < 1 || x.Nodes.Count > MaxNodes) { _r.Add(path + ".nodes", ValidationCode.OutOfRange, $"de 1 à {MaxNodes} nœuds."); ok = false; }
                for (int i = 0; i < x.Nodes.Count; i++)
                    if (x.Nodes[i]?.Id is string id)
                    {
                        if (!LocalId.IsMatch(id) || id.Length > 64) { _r.Add($"{path}.nodes[{i}].id", ValidationCode.InvalidValue, $"identifiant « {id} » mal formé."); ok = false; }
                        else if (!nodeIds.Add(id)) { _r.Add($"{path}.nodes[{i}].id", ValidationCode.DuplicateId, $"nœud « {id} » déjà donné."); ok = false; }
                    }
                    else { TextsValidator.Missing(_r, $"{path}.nodes[{i}].id", "identifiant du nœud"); ok = false; }
                var entries = new List<DialogueEntry>();
                if (x.Entries is null) { TextsValidator.Missing(_r, path + ".entries", "entrées"); ok = false; }
                else
                {
                    if (x.Entries.Count < 1 || x.Entries.Count > MaxEntries) { _r.Add(path + ".entries", ValidationCode.OutOfRange, $"de 1 à {MaxEntries} entrées."); ok = false; }
                    for (int i = 0; i < x.Entries.Count; i++)
                    {
                        string p = $"{path}.entries[{i}]";
                        if (x.Entries[i] is not DialogueEntryData e) { TextsValidator.Missing(_r, p, "entrée"); ok = false; continue; }
                        List<StoryCondition>? conditions = ConditionsOf(e.Conditions, p + ".conditions");
                        if (e.Node is null || !nodeIds.Contains(e.Node)) { _r.Add(p + ".node", ValidationCode.UnknownReference, $"nœud inconnu « {e.Node} »."); ok = false; }
                        if (conditions is null) ok = false;
                        else if (ok) entries.Add(new DialogueEntry(conditions, e.Node!));
                    }
                }
                var nodes = new List<DialogueNode>();
                for (int i = 0; i < x.Nodes.Count; i++)
                {
                    string p = $"{path}.nodes[{i}]";
                    if (x.Nodes[i] is not DialogueNodeData n) { ok = false; continue; }
                    bool nodeOk = Text(n.Text, p + ".text");
                    var choices = new List<DialogueChoice>();
                    if (n.Choices is null) { TextsValidator.Missing(_r, p + ".choices", "réponses, éventuellement vide"); nodeOk = false; }
                    else
                    {
                        if (n.Choices.Count > MaxChoices) { _r.Add(p + ".choices", ValidationCode.LimitExceeded, $"au plus {MaxChoices} réponses."); nodeOk = false; }
                        for (int k = 0; k < n.Choices.Count; k++)
                        {
                            string cp = $"{p}.choices[{k}]";
                            if (n.Choices[k] is not DialogueChoiceData ch) { TextsValidator.Missing(_r, cp, "réponse"); nodeOk = false; continue; }
                            bool chOk = Text(ch.Text, cp + ".text");
                            List<StoryCondition>? conditions = ConditionsOf(ch.Conditions, cp + ".conditions");
                            List<StoryEffect>? effects = EffectsOf(ch.Effects, cp + ".effects");
                            if (!ch.HasNext) { TextsValidator.Missing(_r, cp + ".next", "nœud suivant, ou null pour finir"); chOk = false; }
                            else if (ch.Next != null && !nodeIds.Contains(ch.Next)) { _r.Add(cp + ".next", ValidationCode.UnknownReference, $"nœud inconnu « {ch.Next} »."); chOk = false; }
                            if (chOk && conditions != null && effects != null) choices.Add(new DialogueChoice(ch.Text!, conditions, effects, ch.Next));
                            else nodeOk = false;
                        }
                    }
                    if (nodeOk && n.Id != null) nodes.Add(new DialogueNode(n.Id, n.Text!, choices));
                    else ok = false;
                }
                return ok && x.Id != null ? new Dialogue(x.Id, entries, nodes) : null;
            }

            private Quest? CheckQuest(QuestData q, string path)
            {
                bool ok = Text(q.Name, path + ".name");
                var steps = new List<QuestStep>();
                if (q.Steps is null) { TextsValidator.Missing(_r, path + ".steps", "étapes"); return null; }
                if (q.Steps.Count < 1 || q.Steps.Count > MaxSteps) { _r.Add(path + ".steps", ValidationCode.OutOfRange, $"de 1 à {MaxSteps} étapes."); ok = false; }
                var ids = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < q.Steps.Count; i++)
                {
                    string p = $"{path}.steps[{i}]";
                    if (q.Steps[i] is not QuestStepData s) { TextsValidator.Missing(_r, p, "étape"); ok = false; continue; }
                    bool stepOk = true;
                    if (s.Id is null) { TextsValidator.Missing(_r, p + ".id", "identifiant de l'étape"); stepOk = false; }
                    else if (!LocalId.IsMatch(s.Id) || s.Id.Length > 64 || s.Id == Saves.QuestProgress.Done || s.Id == Saves.QuestProgress.Reward)
                    {
                        _r.Add(p + ".id", ValidationCode.InvalidValue,
                            $"identifiant « {s.Id} » mal formé (« done » est réservé à une quête terminée, « reward » à une récompense au choix en attente).");
                        stepOk = false;
                    }
                    else if (!ids.Add(s.Id)) { _r.Add(p + ".id", ValidationCode.DuplicateId, $"étape « {s.Id} » déjà donnée."); stepOk = false; }
                    stepOk &= Text(s.Text, p + ".text");
                    Objective? objective = ObjectiveOf(s.Objective, p + ".objective");
                    List<StoryEffect>? effects = EffectsOf(s.Effects, p + ".effects");
                    if (stepOk && objective != null && effects != null) steps.Add(new QuestStep(s.Id!, s.Text!, objective, effects));
                    else ok = false;
                }
                List<StoryEffect>? rewards = EffectsOf(q.Effects, path + ".effects");
                // Sous-lot V1.6 : onglet du journal (obligatoire), conditions d'ouverture et récompenses au choix (facultatives).
                QuestCategory category = QuestCategory.Main;
                if (q.Category is null) { TextsValidator.Missing(_r, path + ".category", "« main » ou « side »"); ok = false; }
                else if (!Categories.TryGetValue(q.Category, out category))
                { _r.Add(path + ".category", ValidationCode.InvalidValue, $"catégorie inconnue « {q.Category} » ; possibles : {string.Join(", ", Categories.Keys)}."); ok = false; }
                List<StoryCondition>? requires = q.Requires is null ? new List<StoryCondition>() : ConditionsOf(q.Requires, path + ".requires");
                // Relecture de la PR #76 : une quête ne s'ouvre pas sur son propre état (elle ne s'ouvrirait jamais).
                if (requires != null && q.Id != null)
                    for (int j = 0; j < requires.Count; j++)
                        if (requires[j].Id == q.Id && requires[j].Kind is ConditionKind.QuestDone or ConditionKind.QuestActive or ConditionKind.QuestAt or ConditionKind.QuestReward)
                        { _r.Add($"{path}.requires[{j}].id", ValidationCode.InvalidValue, "une quête ne s'ouvre pas sur son propre état : elle ne s'ouvrirait jamais."); ok = false; }
                var choices = new List<RewardChoice>();
                if (q.RewardChoice != null)
                {
                    string cp = path + ".rewardChoice";
                    List<StoryEffect>? offered = EffectsOf(q.RewardChoice, cp);
                    if (q.RewardChoice.Count < MinRewardChoices || q.RewardChoice.Count > MaxRewardChoices)
                    { _r.Add(cp, ValidationCode.OutOfRange, $"de {MinRewardChoices} à {MaxRewardChoices} récompenses au choix."); ok = false; }
                    if (offered is null) ok = false;
                    else
                        for (int i = 0; i < offered.Count; i++)
                        {
                            if (offered[i].Kind != EffectKind.GiveItem)
                            { _r.Add($"{cp}[{i}].do", ValidationCode.InvalidValue, "une récompense au choix est un objet (giveItem)."); ok = false; }
                            else if (choices.Any(x => x.Item == offered[i].Id))
                            { _r.Add($"{cp}[{i}].id", ValidationCode.DuplicateId, $"objet « {offered[i].Id} » déjà proposé."); ok = false; }
                            else choices.Add(new RewardChoice(offered[i].Id, (int)offered[i].Value));
                        }
                }
                return ok && rewards != null && requires != null && q.Id != null ? new Quest(q.Id, q.Name!, steps, rewards, category, requires, choices) : null;
            }

            private List<StoryCondition>? ConditionsOf(List<TermData?>? terms, string path)
            {
                if (terms is null) { TextsValidator.Missing(_r, path, "conditions, éventuellement vide"); return null; }
                if (terms.Count > MaxTerms) { _r.Add(path, ValidationCode.LimitExceeded, $"au plus {MaxTerms} conditions."); return null; }
                var list = new List<StoryCondition>();
                for (int i = 0; i < terms.Count; i++)
                {
                    string p = $"{path}[{i}]";
                    if (terms[i] is not TermData t || t.Kind is null) { TextsValidator.Missing(_r, p + ".if", "nature de la condition"); continue; }
                    if (!Conditions.TryGetValue(t.Kind, out ConditionKind kind))
                    {
                        _r.Add(p + ".if", ValidationCode.UnknownReference, $"condition inconnue « {t.Kind} » ; possibles : {string.Join(", ", Conditions.Keys)}.");
                        continue;
                    }
                    if (!NoNpc(t, p) || !NoQuality(t, p)) continue;
                    bool ok = kind switch
                    {
                        ConditionKind.LevelAtLeast => NoId(t, p) && Value(t, p, 1, MaxLevel) && NoStep(t, p) && CapWarning(t, p),
                        ConditionKind.GuildRankAtLeast => RankRef(t, p) && NoValue(t, p) && NoStep(t, p),   // sous-lot V1.7
                        ConditionKind.Fact or ConditionKind.NotFact => Fact(t, p, needed: kind == ConditionKind.Fact) && NoValue(t, p) && NoStep(t, p),
                        ConditionKind.QuestNotStarted or ConditionKind.QuestActive or ConditionKind.QuestDone or ConditionKind.QuestReward =>
                            Ref(t, p, _quests.Keys, "quête") && NoValue(t, p) && NoStep(t, p),
                        ConditionKind.QuestAt => Ref(t, p, _quests.Keys, "quête") && QuestStepRef(t, p) && NoValue(t, p),
                        ConditionKind.TrustAtLeast or ConditionKind.TrustBelow => Ref(t, p, _npcs, "PNJ") && Value(t, p, -MaxTrust, MaxTrust) && NoStep(t, p),
                        ConditionKind.HasItem => ItemRef(t, p) && Value(t, p, 1, MaxCount) && NoStep(t, p),
                        ConditionKind.Phase => Ref(t, p, Phases, "moment de la journée") && NoValue(t, p) && NoStep(t, p),
                        ConditionKind.InPlace => Ref(t, p, _zone.Places.Select(x => x.Id), "lieu") && NoValue(t, p) && NoStep(t, p),
                        _ => Ref(t, p, _zone.Passages.Select(x => x.Id), "passage") && NoValue(t, p) && NoStep(t, p),
                    };
                    if (ok) list.Add(new StoryCondition(kind, t.Id ?? string.Empty, t.Value ?? 0, t.Step));
                }
                return list.Count == terms.Count ? list : null;
            }

            private List<StoryEffect>? EffectsOf(List<TermData?>? terms, string path)
            {
                if (terms is null) { TextsValidator.Missing(_r, path, "effets, éventuellement vide"); return null; }
                if (terms.Count > MaxTerms) { _r.Add(path, ValidationCode.LimitExceeded, $"au plus {MaxTerms} effets."); return null; }
                var list = new List<StoryEffect>();
                for (int i = 0; i < terms.Count; i++)
                {
                    string p = $"{path}[{i}]";
                    if (terms[i] is not TermData t || t.Kind is null) { TextsValidator.Missing(_r, p + ".do", "nature de l'effet"); continue; }
                    if (!Effects.TryGetValue(t.Kind, out EffectKind kind))
                    {
                        _r.Add(p + ".do", ValidationCode.UnknownReference, $"effet inconnu « {t.Kind} » ; possibles : {string.Join(", ", Effects.Keys)}.");
                        continue;
                    }
                    if (!NoStep(t, p) || !NoNpc(t, p) || !NoQuality(t, p)) continue;
                    bool ok = kind switch
                    {
                        EffectKind.SetFact => Fact(t, p, needed: false) && NoValue(t, p),
                        EffectKind.Trust => Ref(t, p, _npcs, "PNJ") && Value(t, p, -MaxTrust, MaxTrust) && NonZero(t, p),
                        EffectKind.StartQuest => Ref(t, p, _quests.Keys, "quête") && NoValue(t, p),
                        EffectKind.GiveItem or EffectKind.TakeItem => ItemRef(t, p) && Value(t, p, 1, MaxCount),
                        EffectKind.GiveXp => NoId(t, p) && Value(t, p, 1, MaxXp),
                        EffectKind.GiveMoney or EffectKind.TakeMoney => NoId(t, p) && Value(t, p, 1, MaxMoney),
                        EffectKind.GiveGuildPoints => NoId(t, p) && Value(t, p, 1, GuildRanks.MaxAward),   // sous-lot V1.7
                        EffectKind.OpenPassage => Ref(t, p, _zone.Passages.Select(x => x.Id), "passage") && NoValue(t, p),
                        _ => Ref(t, p, _discoveries, "découverte") && NoValue(t, p),
                    };
                    if (kind == EffectKind.SetFact && ok) _factsSet.Add(t.Id!);
                    if (ok) list.Add(new StoryEffect(kind, t.Id ?? string.Empty, t.Value ?? 0));
                }
                return list.Count == terms.Count ? list : null;
            }

            private Objective? ObjectiveOf(TermData? t, string path)
            {
                if (t?.Kind is null) { TextsValidator.Missing(_r, path + ".kind", "nature de l'objectif"); return null; }
                if (!Objectives.TryGetValue(t.Kind, out ObjectiveKind kind))
                {
                    _r.Add(path + ".kind", ValidationCode.UnknownReference, $"objectif inconnu « {t.Kind} » ; possibles : {string.Join(", ", Objectives.Keys)}.");
                    return null;
                }
                if (!NoStep(t, path)) return null;
                if (kind != ObjectiveKind.Deliver && !NoNpc(t, path)) return null;
                // Sous-lot V1.9.3b : craftQuality est nouveau en version 5 ; seul cet objectif a une qualité.
                if (kind == ObjectiveKind.CraftQuality && _schema < CurrentSchemaVersion)
                {
                    _r.Add(path + ".kind", ValidationCode.InvalidValue, $"objectif « craftQuality » inconnu en version {_schema} du format : version {CurrentSchemaVersion} demandée.");
                    return null;
                }
                if (kind != ObjectiveKind.CraftQuality && !NoQuality(t, path)) return null;
                bool ok = kind switch
                {
                    ObjectiveKind.Talk => Ref(t, path, _npcs, "PNJ") && NoValue(t, path),
                    ObjectiveKind.Deliver => ItemRef(t, path) && Value(t, path, 1, MaxCount) && NpcRef(t, path),
                    ObjectiveKind.DefeatFamily => Ref(t, path, _zone.Populations.Select(x => x.Family), "famille de créatures") && Value(t, path, 1, MaxDefeat),
                    ObjectiveKind.Equipped => EquipmentRef(t, path) && NoValue(t, path),
                    ObjectiveKind.GuildRank => RankRef(t, path) && NoValue(t, path),   // sous-lot V1.7
                    ObjectiveKind.CraftQuality => QualityItemRef(t, path) & QualityRef(t, path) & Value(t, path, 1, MaxCount),   // sous-lot V1.9.3b
                    ObjectiveKind.Fact => Fact(t, path, needed: true) && NoValue(t, path),
                    ObjectiveKind.Reach => Ref(t, path, _zone.Places.Select(x => x.Id), "lieu") && NoValue(t, path),
                    ObjectiveKind.Defeat => Ref(t, path, _zone.Populations.Select(x => x.Creature), "population") && Value(t, path, 1, MaxDefeat),
                    ObjectiveKind.Collect => ItemRef(t, path) && Value(t, path, 1, MaxCount),
                    _ => Ref(t, path, _zone.Passages.Select(x => x.Id), "passage") && NoValue(t, path),
                };
                return ok ? new Objective(kind, t.Id!, t.Value ?? 1, t.Npc, kind == ObjectiveKind.CraftQuality ? Qualities[t.Quality!] : ItemQuality.Normal) : null;
            }

            private bool Fact(TermData t, string p, bool needed)
            {
                if (!IsFactId(t.Id)) { _r.Add(p + ".id", ValidationCode.InvalidValue, $"fait « {t.Id} » mal formé : « fact.… »."); return false; }
                if (needed) _factsNeeded.Add((t.Id!, p + ".id"));
                return true;
            }

            private bool Ref(TermData t, string p, IEnumerable<string> known, string what)
            {
                if (t.Id is null) { TextsValidator.Missing(_r, p + ".id", what); return false; }
                if (known.Contains(t.Id)) return true;
                _r.Add(p + ".id", ValidationCode.UnknownReference, $"{what} inconnu(e) « {t.Id} ».");
                return false;
            }

            private bool QuestStepRef(TermData t, string p)
            {
                if (t.Step is null) { TextsValidator.Missing(_r, p + ".step", "étape de la quête"); return false; }
                if (_quests.TryGetValue(t.Id!, out List<string> steps) && steps.Contains(t.Step)) return true;
                _r.Add(p + ".step", ValidationCode.UnknownReference, $"étape inconnue « {t.Step} » de « {t.Id} ».");
                return false;
            }

            private bool ItemRef(TermData t, string p)
            {
                if (t.Id is null) { TextsValidator.Missing(_r, p + ".id", "objet"); return false; }
                if (_items.TryGetItem(t.Id, out _)) return true;
                _r.Add(p + ".id", ValidationCode.UnknownReference, $"objet inconnu « {t.Id} ».");
                return false;
            }

            /// <summary>Relecture de la PR #82 : avertit (sans refuser) d'une condition de niveau au-dessus du plafond actuel ; rend toujours vrai.</summary>
            private bool CapWarning(TermData t, string p)
            {
                if (LevelCap is int cap && t.Value > cap)
                    _r.AddWarning(p + ".value", ValidationCode.OutOfRange,
                        $"niveau {t.Value} au-dessus du plafond actuel ({cap}) : la condition ne peut pas être remplie tant que le plafond ne monte pas.");
                return true;
            }

            private bool Value(TermData t, string p, long min, long max)
            {
                if (t.Value is null) { TextsValidator.Missing(_r, p + ".value", $"entier de {min} à {max}"); return false; }
                if (t.Value >= min && t.Value <= max) return true;
                _r.Add(p + ".value", ValidationCode.OutOfRange, $"valeur hors de [{min}, {max}].");
                return false;
            }

            private bool NonZero(TermData t, string p)
            {
                if (t.Value != 0) return true;
                _r.Add(p + ".value", ValidationCode.InvalidValue, "changement nul.");
                return false;
            }

            private bool NoValue(TermData t, string p)
            {
                if (!t.Present.Contains("value") && t.Value is null) return true;   // revue Codex de la PR #46 : renseigné hors du lecteur JSON aussi
                _r.Add(p + ".value", ValidationCode.InvalidValue, "pas de valeur pour cette nature.");
                return false;
            }

            private bool NoId(TermData t, string p)
            {
                if (!t.Present.Contains("id") && t.Id is null) return true;   // revue Codex de la PR #46 : renseigné hors du lecteur JSON aussi
                _r.Add(p + ".id", ValidationCode.InvalidValue, "pas d'identifiant pour cette nature.");
                return false;
            }

            /// <summary>Sous-lot V1.6 : seul l'objectif deliver nomme un PNJ (<c>npc</c>).</summary>
            private bool NoNpc(TermData t, string p)
            {
                if (!t.Present.Contains("npc") && t.Npc is null) return true;
                _r.Add(p + ".npc", ValidationCode.InvalidValue, "seul l'objectif deliver nomme un PNJ.");
                return false;
            }

            /// <summary>Sous-lot V1.9.3b : seul l'objectif craftQuality a une qualité (<c>quality</c>).</summary>
            private bool NoQuality(TermData t, string p)
            {
                if (!t.Present.Contains("quality") && t.Quality is null) return true;
                _r.Add(p + ".quality", ValidationCode.InvalidValue, "seul l'objectif craftQuality a une qualité.");
                return false;
            }

            /// <summary>Sous-lot V1.9.3b : qualité minimale connue (« superior » ou « masterwork »).</summary>
            private bool QualityRef(TermData t, string p)
            {
                if (t.Quality is null) { TextsValidator.Missing(_r, p + ".quality", $"qualité minimale ({string.Join(", ", Qualities.Keys)})"); return false; }
                if (Qualities.ContainsKey(t.Quality)) return true;
                _r.Add(p + ".quality", ValidationCode.UnknownReference, $"qualité inconnue « {t.Quality} » ; possibles : {string.Join(", ", Qualities.Keys)}.");
                return false;
            }

            /// <summary>Sous-lot V1.9.3b : un objet qui prend une qualité de fabrication (équipement des places du combat, non nommé).</summary>
            private bool QualityItemRef(TermData t, string p)
            {
                if (!ItemRef(t, p)) return false;
                _items.TryGetItem(t.Id!, out ItemDefinition item);
                if (_items.TakesQuality(item)) return true;
                _r.Add(p + ".id", ValidationCode.InvalidValue, $"« {t.Id} » ne prend pas de qualité de fabrication (un équipement des places du combat, non nommé).");
                return false;
            }

            private bool NpcRef(TermData t, string p)
            {
                if (t.Npc is null) { TextsValidator.Missing(_r, p + ".npc", "PNJ à qui livrer"); return false; }
                if (_npcs.Contains(t.Npc)) return true;
                _r.Add(p + ".npc", ValidationCode.UnknownReference, $"PNJ inconnu « {t.Npc} ».");
                return false;
            }

            /// <summary>Sous-lot V1.7 : un rang de la Guilde des aventuriers (« g » à « sss »).</summary>
            private bool RankRef(TermData t, string p)
            {
                if (t.Id is null) { TextsValidator.Missing(_r, p + ".id", "rang de la guilde (g, f, e…)"); return false; }
                if (GuildRanks.IsRank(t.Id)) return true;
                _r.Add(p + ".id", ValidationCode.UnknownReference, $"rang inconnu « {t.Id} » ; possibles : {GuildRanks.Known}.");
                return false;
            }

            private bool EquipmentRef(TermData t, string p)
            {
                if (!ItemRef(t, p)) return false;
                _items.TryGetItem(t.Id!, out ItemDefinition item);
                if (item.Slot != null) return true;
                _r.Add(p + ".id", ValidationCode.InvalidValue, $"« {t.Id} » ne se porte pas (aucun emplacement).");
                return false;
            }

            private bool NoStep(TermData t, string p)
            {
                if (!t.Present.Contains("step") && t.Step is null) return true;   // revue Codex de la PR #46 : renseigné hors du lecteur JSON aussi
                _r.Add(p + ".step", ValidationCode.InvalidValue, "seule la condition questAt a une étape.");
                return false;
            }

            private bool Text(string? key, string path)
            {
                if (key is null) { TextsValidator.Missing(_r, path, "clé de texte « text.… »"); return false; }
                if (_texts.Has(key)) return true;
                _r.Add(path, ValidationCode.UnknownReference, $"texte « {key} » absent des textes ({_texts.Language}).");
                return false;
            }

            private bool Inside(GroundVector p) => Inside(p, _zone);

            private static bool Inside(GroundVector p, ZoneDefinition zone)
            {
                CombatSpace s = zone.Setup.Space!;
                return p.X >= s.Min.X && p.X <= s.Max.X && p.Z >= s.Min.Z && p.Z <= s.Max.Z;
            }

            private static GroundVector Point(PointData p) => new GroundVector(p.X!.Value, p.Z!.Value);
        }
    }
}
