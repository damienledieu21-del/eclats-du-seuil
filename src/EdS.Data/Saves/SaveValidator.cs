using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Validation;
using EdS.Domain.Collections;

namespace EdS.Data.Saves
{
    /// <summary>Sous-lot 12.3 : pile d'objets d'une place de l'inventaire (identifiant d'objet, quantité).</summary>
    public sealed class ItemStack : IEquatable<ItemStack>
    {
        public string Item { get; }
        public int Count { get; }

        public ItemStack(string item, int count)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            Count = count;
        }

        public bool Equals(ItemStack? other) => other != null && other.Item == Item && other.Count == Count;
        public override bool Equals(object? obj) => Equals(obj as ItemStack);
        public override int GetHashCode() => unchecked(StringComparer.Ordinal.GetHashCode(Item) * 31 + Count);
        public override string ToString() => $"{Item} ×{Count}";
    }

    /// <summary>Sous-lot 12.5 : avancement d'une quête (étape en cours, ou <see cref="Done"/> ; compteur de l'objectif).</summary>
    public sealed class QuestProgress : IEquatable<QuestProgress>
    {
        /// <summary>Étape réservée d'une quête terminée.</summary>
        public const string Done = "done";

        public string Step { get; }

        /// <summary>Compteur de l'objectif de l'étape (créatures vaincues pendant l'étape) ; 0 sinon.</summary>
        public long Progress { get; }

        public bool IsDone => Step == Done;

        public QuestProgress(string step, long progress = 0)
        {
            Step = step ?? throw new ArgumentNullException(nameof(step));
            Progress = progress;
        }

        public bool Equals(QuestProgress? other) => other != null && other.Step == Step && other.Progress == Progress;
        public override bool Equals(object? obj) => Equals(obj as QuestProgress);
        public override int GetHashCode() => unchecked(StringComparer.Ordinal.GetHashCode(Step) * 31 + Progress.GetHashCode());
    }

    /// <summary>
    /// Sous-lot 12.5 (cadrage H1, H2, H3) : état narratif personnel du personnage : faits appris (acquis durables), avancement des quêtes,
    /// confiance des PNJ, journal des découvertes. Tout est trié (forme canonique).
    /// </summary>
    public sealed class SavedStory
    {
        public static readonly SavedStory Empty = new SavedStory();

        public IReadOnlyList<string> Facts { get; }
        public IReadOnlyDictionary<string, QuestProgress> Quests { get; }
        public IReadOnlyDictionary<string, int> Trust { get; }
        public IReadOnlyList<string> Discoveries { get; }

        public SavedStory(IEnumerable<string>? facts = null, IReadOnlyDictionary<string, QuestProgress>? quests = null,
            IReadOnlyDictionary<string, int>? trust = null, IEnumerable<string>? discoveries = null)
        {
            Facts = Frozen.List((facts ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).OrderBy(f => f, StringComparer.Ordinal), nameof(facts));
            Quests = Frozen.Dictionary((quests ?? new Dictionary<string, QuestProgress>()).OrderBy(q => q.Key, StringComparer.Ordinal)
                .ToDictionary(q => q.Key, q => q.Value, StringComparer.Ordinal), nameof(quests));
            Trust = Frozen.Dictionary((trust ?? new Dictionary<string, int>()).Where(t => t.Value != 0).OrderBy(t => t.Key, StringComparer.Ordinal)
                .ToDictionary(t => t.Key, t => t.Value, StringComparer.Ordinal), nameof(trust));
            Discoveries = Frozen.List((discoveries ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).OrderBy(f => f, StringComparer.Ordinal), nameof(discoveries));
        }
    }

    /// <summary>
    /// Sous-lot 12.2 : personnage persistant sauvegardé. Le niveau se déduit de l'expérience (progression du monde), les attributs de la
    /// classe et du niveau (cadrage Q20) : ils ne sont pas écrits. Le build (talents, compétences) est validé contre la classe et le niveau
    /// quand le personnage entre dans une zone.
    /// </summary>
    public sealed class SavedCharacter
    {
        public string Id { get; }
        public string ClassId { get; }
        public long Xp { get; }

        /// <summary>Rangs des talents, par identifiant (ordre ordinal).</summary>
        public IReadOnlyDictionary<string, int> Talents { get; }

        public IReadOnlyList<string> ActiveSkills { get; }
        public string? Ultimate { get; }
        public IReadOnlyList<string> PassiveSkills { get; }

        /// <summary>Sous-lot 12.3 : places occupées de l'inventaire, dans l'ordre (les places libres suivent).</summary>
        public IReadOnlyList<ItemStack> Inventory { get; }

        /// <summary>Sous-lot 12.3 : objet porté par emplacement (ordre ordinal des emplacements).</summary>
        public IReadOnlyDictionary<string, string> Equipment { get; }

        /// <summary>Sous-lot 12.5 : état narratif personnel (faits, quêtes, confiance, découvertes).</summary>
        public SavedStory Story { get; }

        /// <summary>Sous-lot 12.6 : expérience de chaque métier (séparée de celle du combat), non nulle, par identifiant (ordre ordinal).</summary>
        public IReadOnlyDictionary<string, long> Professions { get; }

        public SavedCharacter(string id, string classId, long xp, IReadOnlyDictionary<string, int>? talents = null, IEnumerable<string>? activeSkills = null,
            string? ultimate = null, IEnumerable<string>? passiveSkills = null, IEnumerable<ItemStack>? inventory = null,
            IReadOnlyDictionary<string, string>? equipment = null, SavedStory? story = null, IReadOnlyDictionary<string, long>? professions = null)
        {
            Story = story ?? SavedStory.Empty;
            Professions = Frozen.Dictionary((professions ?? new Dictionary<string, long>()).Where(p => p.Value != 0).OrderBy(p => p.Key, StringComparer.Ordinal)
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal), nameof(professions));
            Inventory = Frozen.List(inventory ?? Array.Empty<ItemStack>(), nameof(inventory));
            Equipment = Frozen.Dictionary((equipment ?? new Dictionary<string, string>()).OrderBy(e => e.Key, StringComparer.Ordinal)
                .ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal), nameof(equipment));
            Id = id ?? throw new ArgumentNullException(nameof(id));
            ClassId = classId ?? throw new ArgumentNullException(nameof(classId));
            Xp = xp;
            Talents = Frozen.Dictionary((talents ?? new Dictionary<string, int>()).OrderBy(t => t.Key, StringComparer.Ordinal)
                .ToDictionary(t => t.Key, t => t.Value, StringComparer.Ordinal), nameof(talents));
            ActiveSkills = Frozen.List(activeSkills ?? Array.Empty<string>(), nameof(activeSkills));
            Ultimate = ultimate;
            PassiveSkills = Frozen.List(passiveSkills ?? Array.Empty<string>(), nameof(passiveSkills));
        }

        /// <summary>Même personnage avec une autre expérience.</summary>
        public SavedCharacter WithXp(long xp) => new SavedCharacter(Id, ClassId, xp, Talents, ActiveSkills, Ultimate, PassiveSkills, Inventory, Equipment, Story, Professions);

        /// <summary>Sous-lot 12.3 : même personnage avec un autre inventaire et un autre équipement.</summary>
        public SavedCharacter WithItems(IEnumerable<ItemStack> inventory, IReadOnlyDictionary<string, string> equipment) =>
            new SavedCharacter(Id, ClassId, Xp, Talents, ActiveSkills, Ultimate, PassiveSkills, inventory, equipment, Story, Professions);

        /// <summary>Sous-lot 12.5 : même personnage avec un autre état narratif.</summary>
        public SavedCharacter WithStory(SavedStory story) =>
            new SavedCharacter(Id, ClassId, Xp, Talents, ActiveSkills, Ultimate, PassiveSkills, Inventory, Equipment, story, Professions);

        /// <summary>Sous-lot 12.6 : même personnage avec d'autres expériences de métier.</summary>
        public SavedCharacter WithProfessions(IReadOnlyDictionary<string, long> professions) =>
            new SavedCharacter(Id, ClassId, Xp, Talents, ActiveSkills, Ultimate, PassiveSkills, Inventory, Equipment, Story, professions);
    }

    /// <summary>
    /// Sous-lot 12.1 (ARCHITECTURE_MONDE.md, section 6 ; cadrage Q10, tranche : fichier local) : sauvegarde du monde validée. Le personnage
    /// (12.2), son inventaire et son équipement (12.3), puis les quêtes s'y ajoutent, la version de format incrémentée à chaque ajout.
    /// </summary>
    public sealed class WorldSave
    {
        /// <summary>Zone où se trouve le joueur.</summary>
        public string Zone { get; }

        /// <summary>Temps du monde en ticks (le jour et la nuit s'en déduisent, jamais de l'horloge réelle).</summary>
        public long WorldTick { get; }

        /// <summary>Sessions de zone déjà jouées.</summary>
        public long Sessions { get; }

        /// <summary>Clés des événements consommables déjà attribués, triées (ordre ordinal), sans doublon.</summary>
        public IReadOnlyList<string> Consumed { get; }

        /// <summary>Sous-lot 12.2 : personnage du joueur ; null tant qu'aucun n'a été créé (nouvelle partie, ou sauvegarde de version 1).</summary>
        public SavedCharacter? Character { get; }

        /// <summary>La sauvegarde vient d'une version antérieure, mise à jour à la lecture (elle sera réécrite à la version courante).</summary>
        public bool Migrated { get; }

        public WorldSave(string zone, long worldTick, long sessions, IEnumerable<string> consumed, SavedCharacter? character = null, bool migrated = false)
        {
            Character = character;
            Migrated = migrated;
            Zone = zone ?? throw new ArgumentNullException(nameof(zone));
            WorldTick = worldTick;
            Sessions = sessions;
            Consumed = Frozen.List((consumed ?? throw new ArgumentNullException(nameof(consumed))).Distinct(StringComparer.Ordinal)
                .OrderBy(k => k, StringComparer.Ordinal), nameof(consumed));
        }
    }

    /// <summary>Résultat d'une lecture de sauvegarde : la sauvegarde validée, ou le rapport qui explique pourquoi pas.</summary>
    public sealed class SaveLoadResult
    {
        public ValidationReport Report { get; }
        public WorldSave? Save { get; }
        public bool IsValid => Save != null;

        internal SaveLoadResult(ValidationReport report, WorldSave? save)
        {
            Report = report;
            Save = save;
        }
    }

    /// <summary>
    /// Validateur unique des sauvegardes (<c>kind: "save"</c>, sous-lot 12.1). Une version antérieure ou postérieure est refusée avec son
    /// chemin (pré-alpha : la tranche n'a pas encore de joueurs à migrer ; les migrations arriveront avec les premiers testeurs, Q10).
    /// </summary>
    public static class SaveValidator
    {
        public const string Kind = "save";

        /// <summary>
        /// Version 1 (sous-lot 12.1) : <c>data.zone</c>, <c>data.worldTick</c>, <c>data.sessions</c>, <c>data.consumed</c>. Version 2 (sous-lot
        /// 12.2) : <c>data.character</c> obligatoire (null ou le personnage : <c>id</c>, <c>class</c>, <c>xp</c>, <c>talents</c>,
        /// <c>activeSkills</c>, <c>ultimate</c>, <c>passiveSkills</c>). Une sauvegarde de version 1 est mise à jour à la lecture (personnage
        /// nul) ; une version postérieure est refusée. Version 3 (sous-lot 12.3) : <c>inventory</c> (places occupées : <c>item</c>,
        /// <c>count</c>) et <c>equipment</c> (objet par emplacement) obligatoires dans le personnage ; une sauvegarde de version 2 est mise à
        /// jour (inventaire et équipement vides). Version 4 (sous-lot 12.5) : <c>story</c> obligatoire dans le personnage (<c>facts</c>,
        /// <c>quests</c>, <c>trust</c>, <c>discoveries</c>) ; une sauvegarde de version 3 est mise à jour (histoire vierge).
        /// Version 5 (sous-lot 12.6) : <c>professions</c> obligatoire dans le personnage (expérience non nulle de chaque métier) ; une
        /// sauvegarde de version 4 est mise à jour (aucun métier).
        /// </summary>
        public const int CurrentSchemaVersion = 5;

        /// <summary>Plus ancienne version lue (et mise à jour).</summary>
        public const int OldestSchemaVersion = 1;

        private static readonly Regex CharacterId = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

        /// <summary>Plafond des événements consommés gardés dans une sauvegarde (enveloppe de la tranche).</summary>
        public const int MaxConsumed = 100_000;

        /// <summary>Plafond technique du temps du monde : un an de jeu en ticks de 50 ms.</summary>
        public const long MaxWorldTick = 365L * 24 * 3600 * 20;

        // Identifiants de zone (« zone.vallons ») et clés d'événements (« zone.vallons/1/death/loup1 ») : ASCII, sans espace.
        private static readonly Regex ZoneId = new Regex(@"^zone\.[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);
        private static readonly Regex EventKey = new Regex(@"^[a-z0-9_.]+(/[a-z0-9_.]+)+\z", RegexOptions.CultureInvariant);

        /// <summary>Identifiant de zone « zone.&lt;nom&gt; », 64 caractères au plus (revue Codex de la PR #42 : les clés d'événement restent bornées).</summary>
        /// <summary>
        /// Revue Codex de la PR #43 : longueur maximale de l'identifiant d'un personnage, pour que la clé de sa mort
        /// (<c>&lt;zone&gt;/&lt;session&gt;/death/&lt;id&gt;</c>, zone de 64 caractères au plus, session de 9 chiffres au plus) tienne toujours
        /// dans une clé d'événement.
        /// </summary>
        public const int MaxCharacterIdLength = 64;

        private static readonly Regex DataId = new Regex(@"^[a-z0-9_.]+\z", RegexOptions.CultureInvariant);

        /// <summary>
        /// Revue Codex de la PR #43 : identifiant de données (classe, talent, compétence) tel que l'écriture le sérialise sans échappement :
        /// minuscules ASCII, chiffres, « _ » et « . », 200 caractères au plus. La lecture exige le même format.
        /// </summary>
        public static bool IsDataId(string? id) => id != null && id.Length <= 200 && DataId.IsMatch(id);

        public static bool IsCharacterId(string? id) => id != null && id.Length <= MaxCharacterIdLength && CharacterId.IsMatch(id);

        public static bool IsZoneId(string? id) => id != null && id.Length <= 64 && ZoneId.IsMatch(id);

        public static bool IsEventKey(string? key) => key != null && key.Length <= 200 && EventKey.IsMatch(key);

        public static SaveLoadResult Validate(SaveDocument document, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            report ??= new ValidationReport();
            // Sous-lot 12.2 : une sauvegarde de version 1 est lue comme telle, puis mise à jour (migration : personnage nul) ; sous-lot 12.3 :
            // une sauvegarde de version 2, avec un inventaire et un équipement vides.
            int version = document.SchemaVersion ?? CurrentSchemaVersion;
            bool migrating = document.SchemaVersion != null && version >= OldestSchemaVersion && version < CurrentSchemaVersion;
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, migrating ? version : CurrentSchemaVersion, report))
                return new SaveLoadResult(report, null);
            SaveData? data = document.Data;
            if (data is null)
            {
                if (!report.HasErrorAtOrAbove("$.data")) report.Add("$.data", ValidationCode.MissingField, "champ obligatoire absent (zone, temps du monde, sessions, événements consommés).");
                return new SaveLoadResult(report, null);
            }
            if (data.Zone is null) Missing(report, "$.data.zone", "identifiant de zone, par exemple « zone.vallons »");
            else if (!IsZoneId(data.Zone)) report.Add("$.data.zone", ValidationCode.InvalidValue, $"identifiant de zone « {data.Zone} » invalide : « zone.<nom> » attendu.");
            if (data.WorldTick is null) Missing(report, "$.data.worldTick", "temps du monde en ticks");
            else if (data.WorldTick < 0 || data.WorldTick > MaxWorldTick)
                report.Add("$.data.worldTick", ValidationCode.OutOfRange, $"temps du monde hors de [0, {MaxWorldTick}] ticks.");
            if (data.Sessions is null) Missing(report, "$.data.sessions", "nombre de sessions jouées");
            else if (data.Sessions < 0 || data.Sessions > MaxWorldTick)
                report.Add("$.data.sessions", ValidationCode.OutOfRange, $"nombre de sessions hors de [0, {MaxWorldTick}].");
            if (data.Consumed is null) Missing(report, "$.data.consumed", "liste des événements consommés (vide au départ)");
            else
            {
                if (data.Consumed.Count > MaxConsumed)
                    report.Add("$.data.consumed", ValidationCode.LimitExceeded, $"{data.Consumed.Count} événements consommés, au plus {MaxConsumed}.");
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < data.Consumed.Count; i++)
                {
                    string? key = data.Consumed[i];
                    string path = $"$.data.consumed[{i}]";
                    if (key is null) { if (!report.HasErrorAtOrAbove(path)) report.Add(path, ValidationCode.InvalidValue, "clé d'événement attendue."); }
                    else if (!IsEventKey(key)) report.Add(path, ValidationCode.InvalidValue, $"clé d'événement « {key} » invalide : segments [a-z0-9_.] séparés par « / ».");
                    else if (!seen.Add(key)) report.Add(path, ValidationCode.DuplicateId, $"événement « {key} » déjà consommé plus haut : une seule attribution.");
                    else if (i > 0 && data.Consumed[i - 1] is string previous && string.CompareOrdinal(previous, key) > 0)
                        report.Add(path, ValidationCode.InvalidValue, "événements consommés hors de l'ordre ordinal (forme canonique).");
                }
            }
            SavedCharacter? character = null;
            if (version < 2)
            {
                if (data.HasCharacter) report.Add("$.data.character", ValidationCode.UnknownField, $"champ inconnu en version {document.SchemaVersion} (personnage : version 2).");
            }
            else if (!data.HasCharacter) Missing(report, "$.data.character", "personnage du joueur, ou null");
            else if (data.Character is CharacterData c) character = CheckCharacter(c, version, report);
            if (!report.IsValid || document.ContentVersion is null) return new SaveLoadResult(report, null);
            return new SaveLoadResult(report, new WorldSave(data.Zone!, data.WorldTick!.Value, data.Sessions!.Value, data.Consumed!.Select(k => k!), character, migrating));
        }

        private static SavedCharacter? CheckCharacter(CharacterData c, int version, ValidationReport report)
        {
            const string path = "$.data.character";
            if (c.Id is null) Missing(report, path + ".id", "identifiant du personnage");
            else if (!IsCharacterId(c.Id)) report.Add(path + ".id", ValidationCode.InvalidValue, $"identifiant « {c.Id} » invalide : minuscules, chiffres et « _ », {MaxCharacterIdLength} caractères au plus.");
            const string format = "minuscules, chiffres, « _ » et « . », 200 caractères au plus (format de l'écriture)";
            if (c.Class is null) Missing(report, path + ".class", "identifiant de classe");
            else if (!IsDataId(c.Class)) report.Add(path + ".class", ValidationCode.InvalidValue, $"identifiant de classe « {c.Class} » invalide : {format}.");
            if (c.Xp is null) Missing(report, path + ".xp", "expérience cumulée");
            else if (c.Xp < 0 || c.Xp > MaxXp) report.Add(path + ".xp", ValidationCode.OutOfRange, $"expérience hors de [0, {MaxXp}].");
            if (c.Talents is null) Missing(report, path + ".talents", "rangs des talents, éventuellement vide");
            else
                foreach (var t in c.Talents)
                    if (!IsDataId(t.Key)) report.Add(path + ".talents." + t.Key, ValidationCode.InvalidValue, $"identifiant de talent invalide : {format}.");
                    else if (t.Value is not int rank) Missing(report, path + ".talents." + t.Key, "rang");
                    else if (rank < 0) report.Add(path + ".talents." + t.Key, ValidationCode.InvalidValue, "un rang ne peut pas être négatif.");
            void Names(List<string?>? list, string field)
            {
                if (list is null) { Missing(report, $"{path}.{field}", "liste, éventuellement vide"); return; }
                for (int i = 0; i < list.Count; i++)
                    if (list[i] is null) Missing(report, $"{path}.{field}[{i}]", "identifiant de compétence");
                    else if (!IsDataId(list[i])) report.Add($"{path}.{field}[{i}]", ValidationCode.InvalidValue, $"identifiant de compétence « {list[i]} » invalide : {format}.");
            }
            Names(c.ActiveSkills, "activeSkills");
            // Revue Codex de la PR #43 : l'ultime est obligatoire (null s'il n'y en a pas), comme le dit le schéma.
            if (!c.HasUltimate) Missing(report, path + ".ultimate", "ultime, ou null");
            else if (c.Ultimate != null && !IsDataId(c.Ultimate)) report.Add(path + ".ultimate", ValidationCode.InvalidValue, $"identifiant de compétence « {c.Ultimate} » invalide : {format}.");
            Names(c.PassiveSkills, "passiveSkills");
            // Sous-lot 12.3 (version 3) : inventaire et équipement. Leur cohérence avec le catalogue des objets (objets connus, piles, emplacements,
            // niveau requis) est vérifiée quand la partie reprend : la sauvegarde ne dépend pas des données du jeu.
            if (version < 3)
            {
                foreach (string field in new[] { "inventory", "equipment" }.Where(c.Present.Contains))
                    report.Add($"{path}.{field}", ValidationCode.UnknownField, $"champ inconnu en version {version} (inventaire et équipement : version 3).");
            }
            else
            {
                if (c.Inventory is null) Missing(report, path + ".inventory", "places occupées de l'inventaire, éventuellement vide");
                else
                    for (int i = 0; i < c.Inventory.Count; i++)
                    {
                        string stackPath = $"{path}.inventory[{i}]";
                        if (c.Inventory[i] is not ItemStackData stack) { Missing(report, stackPath, "pile d'objets"); continue; }
                        if (stack.Item is null) Missing(report, stackPath + ".item", "identifiant d'objet");
                        else if (!IsItemId(stack.Item)) report.Add(stackPath + ".item", ValidationCode.InvalidValue, $"identifiant d'objet « {stack.Item} » invalide.");
                        if (stack.Count is null) Missing(report, stackPath + ".count", "quantité");
                        else if (stack.Count < 1 || stack.Count > MaxStack) report.Add(stackPath + ".count", ValidationCode.OutOfRange, $"quantité hors de [1, {MaxStack}].");
                    }
                if (c.Equipment is null) Missing(report, path + ".equipment", "objet porté par emplacement, éventuellement vide");
                else
                {
                    for (int i = 0; i < c.Equipment.Count; i++)
                    {
                        KeyValuePair<string, string?> e = c.Equipment[i];
                        string slotPath = $"{path}.equipment.{e.Key}";
                        if (!SlotId.IsMatch(e.Key) || e.Key.Length > 64) report.Add(slotPath, ValidationCode.InvalidValue, $"emplacement « {e.Key} » invalide.");
                        else if (e.Value is null) Missing(report, slotPath, "identifiant d'objet");
                        else if (!IsItemId(e.Value)) report.Add(slotPath, ValidationCode.InvalidValue, $"identifiant d'objet « {e.Value} » invalide.");
                        else if (i > 0 && string.CompareOrdinal(c.Equipment[i - 1].Key, e.Key) > 0)
                            report.Add(slotPath, ValidationCode.InvalidValue, "emplacements hors de l'ordre ordinal (forme canonique).");
                    }
                }
            }
            // Sous-lot 12.5 (version 4) : état narratif personnel.
            SavedStory? story = null;
            if (version < 4)
            {
                if (c.Present.Contains("story")) report.Add(path + ".story", ValidationCode.UnknownField, $"champ inconnu en version {version} (histoire : version 4).");
            }
            else if (!c.Present.Contains("story")) Missing(report, path + ".story", "faits, quêtes, confiance, découvertes");
            else if (c.Story is StoryStateData st) story = CheckStory(st, path + ".story", report);
            // Sous-lot 12.6 (version 5) : expérience des métiers.
            if (version < 5)
            {
                if (c.Present.Contains("professions")) report.Add(path + ".professions", ValidationCode.UnknownField, $"champ inconnu en version {version} (métiers : version 5).");
            }
            else if (c.Professions is null) Missing(report, path + ".professions", "expérience de chaque métier, éventuellement vide");
            else
                for (int i = 0; i < c.Professions.Count; i++)
                {
                    var p = c.Professions[i];
                    string pp = $"{path}.professions.{p.Key}";
                    if (p.Key.Length > 64 || !SlotId.IsMatch(p.Key)) report.Add(pp, ValidationCode.InvalidValue, "métier mal formé.");
                    else if (p.Value is not long xp) Missing(report, pp, "expérience");
                    else if (xp < 1 || xp > MaxXp) report.Add(pp, ValidationCode.OutOfRange, $"expérience de métier hors de [1, {MaxXp}] (une expérience nulle n'est pas écrite).");
                    else if (i > 0 && string.CompareOrdinal(c.Professions[i - 1].Key, p.Key) > 0) report.Add(pp, ValidationCode.InvalidValue, "métiers hors de l'ordre ordinal.");
                }
            if (!report.IsValid) return null;
            return new SavedCharacter(c.Id!, c.Class!, c.Xp!.Value, c.Talents!.ToDictionary(t => t.Key, t => t.Value!.Value, StringComparer.Ordinal),
                c.ActiveSkills!.Select(s => s!), c.Ultimate, c.PassiveSkills!.Select(s => s!),
                version < 3 ? null : c.Inventory!.Select(st => new ItemStack(st!.Item!, st.Count!.Value)),
                version < 3 ? null : c.Equipment!.ToDictionary(e => e.Key, e => e.Value!, StringComparer.Ordinal), story,
                version < 5 ? null : c.Professions!.ToDictionary(p => p.Key, p => p.Value!.Value, StringComparer.Ordinal));
        }

        public const int MaxStoryEntries = 10_000;
        public const int MaxTrust = 100;
        public const long MaxProgress = 1_000_000;

        private static readonly Regex FactId = new Regex(@"^fact(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);
        private static readonly Regex QuestId = new Regex(@"^quest(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);
        private static readonly Regex NpcId = new Regex(@"^npc(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);
        private static readonly Regex DiscoveryId = new Regex(@"^discovery(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);

        public static bool IsStoryId(string? id) => id != null && id.Length <= 200
            && (FactId.IsMatch(id) || QuestId.IsMatch(id) || NpcId.IsMatch(id) || DiscoveryId.IsMatch(id) || SlotId.IsMatch(id));

        private static SavedStory? CheckStory(StoryStateData st, string path, ValidationReport report)
        {
            void Sorted(List<string?>? list, string field, Regex pattern, string what)
            {
                if (list is null) { Missing(report, $"{path}.{field}", what + ", éventuellement vide"); return; }
                if (list.Count > MaxStoryEntries) report.Add($"{path}.{field}", ValidationCode.LimitExceeded, $"au plus {MaxStoryEntries}.");
                for (int i = 0; i < list.Count; i++)
                {
                    string p = $"{path}.{field}[{i}]";
                    if (list[i] is not string id || id.Length > 200 || !pattern.IsMatch(id)) report.Add(p, ValidationCode.InvalidValue, $"{what} mal formé(e).");
                    else if (i > 0 && list[i - 1] is string prev && string.CompareOrdinal(prev, id) >= 0)
                        report.Add(p, ValidationCode.InvalidValue, "liste hors de l'ordre ordinal ou avec doublon (forme canonique).");
                }
            }
            Sorted(st.Facts, "facts", FactId, "fait");
            Sorted(st.Discoveries, "discoveries", DiscoveryId, "découverte");
            if (st.Quests is null) Missing(report, path + ".quests", "quêtes, éventuellement vide");
            else
            {
                if (st.Quests.Count > MaxStoryEntries) report.Add(path + ".quests", ValidationCode.LimitExceeded, $"au plus {MaxStoryEntries}.");
                for (int i = 0; i < st.Quests.Count; i++)
                {
                    var (id, q) = st.Quests[i];
                    string p = $"{path}.quests.{id}";
                    if (id.Length > 200 || !QuestId.IsMatch(id)) { report.Add(p, ValidationCode.InvalidValue, "quête mal formée."); continue; }
                    if (i > 0 && string.CompareOrdinal(st.Quests[i - 1].Id, id) > 0) report.Add(p, ValidationCode.InvalidValue, "quêtes hors de l'ordre ordinal (forme canonique).");
                    if (q is null) { Missing(report, p, "avancement"); continue; }
                    if (q.Step is null) Missing(report, p + ".step", "étape en cours, ou « done »");
                    else if (q.Step.Length > 64 || !SlotId.IsMatch(q.Step)) report.Add(p + ".step", ValidationCode.InvalidValue, $"étape « {q.Step} » mal formée.");
                    if (q.Progress is null) Missing(report, p + ".progress", "compteur");
                    else if (q.Progress < 0 || q.Progress > MaxProgress) report.Add(p + ".progress", ValidationCode.OutOfRange, $"compteur hors de [0, {MaxProgress}].");
                }
            }
            if (st.Trust is null) Missing(report, path + ".trust", "confiance des PNJ, éventuellement vide");
            else
                for (int i = 0; i < st.Trust.Count; i++)
                {
                    var t = st.Trust[i];
                    string p = $"{path}.trust.{t.Key}";
                    if (t.Key.Length > 200 || !NpcId.IsMatch(t.Key)) report.Add(p, ValidationCode.InvalidValue, "PNJ mal formé.");
                    else if (t.Value is not int v) Missing(report, p, "entier");
                    else if (v == 0 || v < -MaxTrust || v > MaxTrust) report.Add(p, ValidationCode.OutOfRange, $"confiance hors de [−{MaxTrust}, {MaxTrust}] ou nulle (une confiance nulle n'est pas écrite).");
                    else if (i > 0 && string.CompareOrdinal(st.Trust[i - 1].Key, t.Key) > 0) report.Add(p, ValidationCode.InvalidValue, "confiance hors de l'ordre ordinal.");
                }
            if (!report.IsValid) return null;
            return new SavedStory(st.Facts!.Select(f => f!), st.Quests!.ToDictionary(q => q.Id, q => new QuestProgress(q.Value!.Step!, q.Value.Progress!.Value), StringComparer.Ordinal),
                st.Trust!.ToDictionary(t => t.Key, t => t.Value!.Value, StringComparer.Ordinal), st.Discoveries!.Select(d => d!));
        }

        /// <summary>Sous-lot 12.3 : plus grand nombre de places d'un inventaire sauvegardé, et plus grande pile.</summary>
        public const int MaxInventory = 200;

        public const int MaxStack = 999;

        private static readonly Regex ItemId = new Regex(@"^item(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);
        private static readonly Regex SlotId = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

        public static bool IsItemId(string? id) => id != null && id.Length <= 200 && ItemId.IsMatch(id);

        /// <summary>Plafond technique de l'expérience d'un personnage.</summary>
        public const long MaxXp = 1_000_000_000;

        private static void Missing(ValidationReport report, string path, string what)
        {
            if (!report.HasErrorAtOrAbove(path)) report.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({what}).");
        }
    }
}
