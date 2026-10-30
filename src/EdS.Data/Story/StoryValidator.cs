using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Items;
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
        /// cuivre données à la bourse, de 1 à <see cref="MaxMoney"/>). Une version antérieure est refusée (pré-alpha : aucune migration des
        /// fichiers de données).
        /// </summary>
        public const int CurrentSchemaVersion = 2;

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
        });

        public static IReadOnlyDictionary<string, EffectKind> Effects { get; } = new ReadOnlyDictionary<string, EffectKind>(new Dictionary<string, EffectKind>(StringComparer.Ordinal)
        {
            ["setFact"] = EffectKind.SetFact, ["trust"] = EffectKind.Trust, ["startQuest"] = EffectKind.StartQuest, ["giveItem"] = EffectKind.GiveItem,
            ["takeItem"] = EffectKind.TakeItem, ["giveXp"] = EffectKind.GiveXp, ["openPassage"] = EffectKind.OpenPassage, ["discover"] = EffectKind.Discover,
            ["giveMoney"] = EffectKind.GiveMoney,
        });

        public static IReadOnlyDictionary<string, ObjectiveKind> Objectives { get; } = new ReadOnlyDictionary<string, ObjectiveKind>(new Dictionary<string, ObjectiveKind>(StringComparer.Ordinal)
        {
            ["fact"] = ObjectiveKind.Fact, ["reach"] = ObjectiveKind.Reach, ["defeat"] = ObjectiveKind.Defeat, ["collect"] = ObjectiveKind.Collect,
            ["passage"] = ObjectiveKind.Passage,
        });

        public static readonly IReadOnlyList<string> Phases = new ReadOnlyCollection<string>(new[] { "dawn", "day", "dusk", "night" });

        private static Regex Id(string prefix) => new Regex($@"^{prefix}(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);

        private static readonly Regex NpcId = Id("npc"), DialogueId = Id("dialogue"), QuestId = Id("quest"), FactId = Id("fact"), DiscoveryId = Id("discovery");
        private static readonly Regex LocalId = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

        public static bool IsFactId(string? id) => id != null && id.Length <= 200 && FactId.IsMatch(id);

        public static StoryLoadResult Validate(StoryDocument document, TextCatalog texts, ZoneDefinition zone, ItemCatalog items, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (texts is null) throw new ArgumentNullException(nameof(texts));
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            if (items is null) throw new ArgumentNullException(nameof(items));
            report ??= new ValidationReport();
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, CurrentSchemaVersion, report))
                return new StoryLoadResult(report, null);
            StoryData? d = document.Data;
            if (d is null) { TextsValidator.Missing(report, "$.data", "PNJ, dialogues, quêtes, découvertes"); return new StoryLoadResult(report, null); }
            var c = new Checker(report, texts, zone, items, d);
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

            public Checker(ValidationReport report, TextCatalog texts, ZoneDefinition zone, ItemCatalog items, StoryData data)
            {
                _r = report;
                _texts = texts;
                _zone = zone;
                _items = items;
                _d = data;
            }

            public StoryBook? Run(int schemaVersion, string? contentVersion)
            {
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
                        if (n.Position is null || n.Position.X is null || n.Position.Z is null) { TextsValidator.Missing(_r, path + ".position", "[x, z]"); ok = false; }
                        else if (!Inside(Point(n.Position))) { _r.Add(path + ".position", ValidationCode.OutOfRange, "PNJ hors de l'aire praticable de la zone."); ok = false; }
                        if (n.Dialogue is null) { TextsValidator.Missing(_r, path + ".dialogue", "dialogue du PNJ"); ok = false; }
                        else if (!_dialogues.Contains(n.Dialogue)) { _r.Add(path + ".dialogue", ValidationCode.UnknownReference, $"dialogue inconnu « {n.Dialogue} »."); ok = false; }
                        if (ok && n.Id != null) npcs.Add(new Npc(n.Id, n.Name!, Point(n.Position!), n.Dialogue!));
                    }

                var dialogues = new List<Dialogue>();
                if (_d.Dialogues != null)
                    for (int i = 0; i < _d.Dialogues.Count; i++)
                        if (_d.Dialogues[i] is DialogueData x && CheckDialogue(x, $"$.data.dialogues[{i}]") is { } dialogue) dialogues.Add(dialogue);

                var quests = new List<Quest>();
                if (_d.Quests != null)
                    for (int i = 0; i < _d.Quests.Count; i++)
                        if (_d.Quests[i] is QuestData q && CheckQuest(q, $"$.data.quests[{i}]") is { } quest) quests.Add(quest);

                // Sous-lot 12.7 : la victoire sur le chef de la zone apprend son fait.
                if (_zone.Boss is { } boss) _factsSet.Add(boss.Fact);
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
                    else if (!LocalId.IsMatch(s.Id) || s.Id.Length > 64 || s.Id == Saves.QuestProgress.Done)
                    { _r.Add(p + ".id", ValidationCode.InvalidValue, $"identifiant « {s.Id} » mal formé (« done » est réservé à une quête terminée)."); stepOk = false; }
                    else if (!ids.Add(s.Id)) { _r.Add(p + ".id", ValidationCode.DuplicateId, $"étape « {s.Id} » déjà donnée."); stepOk = false; }
                    stepOk &= Text(s.Text, p + ".text");
                    Objective? objective = ObjectiveOf(s.Objective, p + ".objective");
                    List<StoryEffect>? effects = EffectsOf(s.Effects, p + ".effects");
                    if (stepOk && objective != null && effects != null) steps.Add(new QuestStep(s.Id!, s.Text!, objective, effects));
                    else ok = false;
                }
                List<StoryEffect>? rewards = EffectsOf(q.Effects, path + ".effects");
                return ok && rewards != null && q.Id != null ? new Quest(q.Id, q.Name!, steps, rewards) : null;
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
                    bool ok = kind switch
                    {
                        ConditionKind.Fact or ConditionKind.NotFact => Fact(t, p, needed: kind == ConditionKind.Fact) && NoValue(t, p) && NoStep(t, p),
                        ConditionKind.QuestNotStarted or ConditionKind.QuestActive or ConditionKind.QuestDone => Ref(t, p, _quests.Keys, "quête") && NoValue(t, p) && NoStep(t, p),
                        ConditionKind.QuestAt => Ref(t, p, _quests.Keys, "quête") && QuestStepRef(t, p) && NoValue(t, p),
                        ConditionKind.TrustAtLeast or ConditionKind.TrustBelow => Ref(t, p, _npcs, "PNJ") && Value(t, p, -MaxTrust, MaxTrust) && NoStep(t, p),
                        ConditionKind.HasItem => ItemRef(t, p) && Value(t, p, 1, MaxCount) && NoStep(t, p),
                        ConditionKind.Phase => Ref(t, p, Phases, "moment de la journée") && NoValue(t, p) && NoStep(t, p),
                        ConditionKind.InPlace => Ref(t, p, _zone.Places.Select(x => x.Id), "lieu") && NoValue(t, p) && NoStep(t, p),
                        _ => Ref(t, p, _zone.Passages.Select(x => x.Id), "passage") && NoValue(t, p) && NoStep(t, p),
                    };
                    if (ok) list.Add(new StoryCondition(kind, t.Id!, t.Value ?? 0, t.Step));
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
                    if (!NoStep(t, p)) continue;
                    bool ok = kind switch
                    {
                        EffectKind.SetFact => Fact(t, p, needed: false) && NoValue(t, p),
                        EffectKind.Trust => Ref(t, p, _npcs, "PNJ") && Value(t, p, -MaxTrust, MaxTrust) && NonZero(t, p),
                        EffectKind.StartQuest => Ref(t, p, _quests.Keys, "quête") && NoValue(t, p),
                        EffectKind.GiveItem or EffectKind.TakeItem => ItemRef(t, p) && Value(t, p, 1, MaxCount),
                        EffectKind.GiveXp => NoId(t, p) && Value(t, p, 1, MaxXp),
                        EffectKind.GiveMoney => NoId(t, p) && Value(t, p, 1, MaxMoney),
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
                bool ok = kind switch
                {
                    ObjectiveKind.Fact => Fact(t, path, needed: true) && NoValue(t, path),
                    ObjectiveKind.Reach => Ref(t, path, _zone.Places.Select(x => x.Id), "lieu") && NoValue(t, path),
                    ObjectiveKind.Defeat => Ref(t, path, _zone.Populations.Select(x => x.Creature), "population") && Value(t, path, 1, MaxDefeat),
                    ObjectiveKind.Collect => ItemRef(t, path) && Value(t, path, 1, MaxCount),
                    _ => Ref(t, path, _zone.Passages.Select(x => x.Id), "passage") && NoValue(t, path),
                };
                return ok ? new Objective(kind, t.Id!, t.Value ?? 1) : null;
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

            private bool Inside(GroundVector p)
            {
                CombatSpace s = _zone.Setup.Space!;
                return p.X >= s.Min.X && p.X <= s.Max.X && p.Z >= s.Min.Z && p.Z <= s.Max.Z;
            }

            private static GroundVector Point(PointData p) => new GroundVector(p.X!.Value, p.Z!.Value);
        }
    }
}
