using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Guild;
using EdS.Data.Validation;
using EdS.Domain.Collections;

namespace EdS.Data.Saves
{
    /// <summary>
    /// Sous-lot V1.3 : une propriété tirée au hasard sur un objet du butin (identifiant « property.… » du catalogue, valeur entière ajoutée à sa
    /// statistique). Immuable.
    /// </summary>
    public sealed class RolledProperty : IEquatable<RolledProperty>
    {
        public string Id { get; }
        public int Value { get; }

        public RolledProperty(string id, int value)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Value = value;
        }

        public bool Equals(RolledProperty? other) => other != null && other.Id == Id && other.Value == Value;
        public override bool Equals(object? obj) => Equals(obj as RolledProperty);
        public override int GetHashCode() => unchecked(StringComparer.Ordinal.GetHashCode(Id) * 31 + Value);
        public override string ToString() => $"{Id} {Value}";
    }

    /// <summary>
    /// Sous-lot 12.3 : pile d'objets d'une place de l'inventaire (identifiant d'objet, quantité). Sous-lot V1.3 : une <b>instance d'objet</b>
    /// porte aussi ses propriétés tirées (<see cref="Properties"/>, vides pour un objet de base : marchand, récompense, objet nommé) ; une
    /// instance aux propriétés tirées ne s'empile pas (quantité 1). La qualité de fabrication et l'usure (sous-lot V1.4) prendront place ici,
    /// en champs nouveaux de l'instance (noms réservés dans la sauvegarde : <c>quality</c>, <c>wear</c>).
    /// </summary>
    public sealed class ItemStack : IEquatable<ItemStack>
    {
        public string Item { get; }
        public int Count { get; }

        /// <summary>Sous-lot V1.3 : propriétés tirées, dans l'ordre du tirage ; vide pour un objet de base.</summary>
        public IReadOnlyList<RolledProperty> Properties { get; }

        /// <summary>Instance aux propriétés tirées (elle ne s'empile pas).</summary>
        public bool IsRolled => Properties.Count > 0;

        public ItemStack(string item, int count, IEnumerable<RolledProperty>? properties = null)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            Count = count;
            Properties = Frozen.ListOrEmpty(properties, nameof(properties));
        }

        /// <summary>Même instance avec une autre quantité.</summary>
        public ItemStack WithCount(int count) => new ItemStack(Item, count, Properties);

        public bool Equals(ItemStack? other) => other != null && other.Item == Item && other.Count == Count && other.Properties.SequenceEqual(Properties);
        public override bool Equals(object? obj) => Equals(obj as ItemStack);
        public override int GetHashCode() => unchecked(StringComparer.Ordinal.GetHashCode(Item) * 31 + Count + Properties.Count * 7919);
        public override string ToString() => Properties.Count == 0 ? $"{Item} ×{Count}" : $"{Item} ×{Count} [{string.Join(", ", Properties)}]";
    }

    /// <summary>Sous-lot 12.5 : avancement d'une quête (étape en cours, ou <see cref="Done"/> ; compteur de l'objectif).</summary>
    public sealed class QuestProgress : IEquatable<QuestProgress>
    {
        /// <summary>Étape réservée d'une quête terminée.</summary>
        public const string Done = "done";

        /// <summary>
        /// Sous-lot V1.6 (sauvegarde en version 8) : étape réservée d'une quête dont toutes les étapes sont franchies et qui attend le choix
        /// de sa récompense (un objet parmi deux ou trois).
        /// </summary>
        public const string Reward = "reward";

        public bool IsChoosingReward => Step == Reward;

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

        /// <summary>Sous-lot V1.6 (version 8) : quête suivie (son objectif sur la carte), ou null.</summary>
        public string? Tracked { get; }

        /// <summary>Sous-lot V1.7 (version 10) : carte d'aventurier de la Guilde ; null tant que le personnage n'est pas inscrit.</summary>
        public SavedGuildCard? Guild { get; }

        /// <summary>
        /// Sous-lot V1.7 (version 10, relecture de la PR #79) : créatures rares de la guilde déjà vaincues (populations), inscrit ou non ; leur
        /// première victoire ne donne des points qu'une fois, et jamais plus tard si elle a eu lieu avant l'inscription. Ordre ordinal.
        /// </summary>
        public IReadOnlyList<string> Hunted { get; }

        /// <summary>Créatures rares gardées au plus.</summary>
        public const int MaxHunted = 256;

        public SavedStory(IEnumerable<string>? facts = null, IReadOnlyDictionary<string, QuestProgress>? quests = null,
            IReadOnlyDictionary<string, int>? trust = null, IEnumerable<string>? discoveries = null, string? tracked = null, SavedGuildCard? guild = null,
            IEnumerable<string>? hunted = null)
        {
            Tracked = tracked;
            Guild = guild;
            Hunted = Frozen.List((hunted ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).OrderBy(h => h, StringComparer.Ordinal), nameof(hunted));
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

        /// <summary>
        /// Sous-lot V1.1 : bourse du personnage, en pièces de cuivre (<see cref="Coins"/> : 100 cuivre font 1 argent, 100 argent font 1 or), de 0
        /// à <see cref="Coins.Max"/>.
        /// </summary>
        public long Money { get; }

        /// <summary>
        /// Sous-lot V1.3 : propriétés tirées des objets portés, par emplacement (seulement les emplacements dont l'objet en a ; ordre ordinal).
        /// Chaque emplacement cité porte un objet (<see cref="Equipment"/>).
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<RolledProperty>> EquipmentProperties { get; }

        public SavedCharacter(string id, string classId, long xp, IReadOnlyDictionary<string, int>? talents = null, IEnumerable<string>? activeSkills = null,
            string? ultimate = null, IEnumerable<string>? passiveSkills = null, IEnumerable<ItemStack>? inventory = null,
            IReadOnlyDictionary<string, string>? equipment = null, SavedStory? story = null, IReadOnlyDictionary<string, long>? professions = null,
            long money = 0, IReadOnlyDictionary<string, IReadOnlyList<RolledProperty>>? equipmentProperties = null)
        {
            // Relecture de la PR #72 : une bourse hors bornes est refusée dès la construction (comme à la lecture d'une sauvegarde) ; une
            // partie ne peut donc jamais en porter une, et son affichage (Coins.Format) ne peut pas échouer.
            if (!Coins.IsValid(money)) throw new ArgumentOutOfRangeException(nameof(money), money, $"Bourse hors de [0, {Coins.Max}] cuivre.");
            Money = money;
            Story = story ?? SavedStory.Empty;
            Professions = Frozen.Dictionary((professions ?? new Dictionary<string, long>()).Where(p => p.Value != 0).OrderBy(p => p.Key, StringComparer.Ordinal)
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal), nameof(professions));
            Inventory = Frozen.List(inventory ?? Array.Empty<ItemStack>(), nameof(inventory));
            Equipment = Frozen.Dictionary((equipment ?? new Dictionary<string, string>()).OrderBy(e => e.Key, StringComparer.Ordinal)
                .ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal), nameof(equipment));
            var rolled = new Dictionary<string, IReadOnlyList<RolledProperty>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, IReadOnlyList<RolledProperty>> e in (equipmentProperties ?? new Dictionary<string, IReadOnlyList<RolledProperty>>())
                .Where(e => e.Value != null && e.Value.Count > 0).OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                if (!Equipment.ContainsKey(e.Key))
                    throw new ArgumentException($"Propriétés tirées à l'emplacement vide « {e.Key} ».", nameof(equipmentProperties));
                rolled[e.Key] = Frozen.List(e.Value, nameof(equipmentProperties));
            }
            EquipmentProperties = Frozen.Dictionary(rolled, nameof(equipmentProperties));
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
        public SavedCharacter WithXp(long xp) => new SavedCharacter(Id, ClassId, xp, Talents, ActiveSkills, Ultimate, PassiveSkills, Inventory, Equipment, Story, Professions, Money, EquipmentProperties);

        /// <summary>
        /// Sous-lot 12.3 : même personnage avec un autre inventaire et un autre équipement. Sous-lot V1.3 : avec les propriétés tirées des objets
        /// portés (<paramref name="equipmentProperties"/>, aucune par défaut).
        /// </summary>
        public SavedCharacter WithItems(IEnumerable<ItemStack> inventory, IReadOnlyDictionary<string, string> equipment,
            IReadOnlyDictionary<string, IReadOnlyList<RolledProperty>>? equipmentProperties = null) =>
            new SavedCharacter(Id, ClassId, Xp, Talents, ActiveSkills, Ultimate, PassiveSkills, inventory, equipment, Story, Professions, Money, equipmentProperties);

        /// <summary>Sous-lot 12.5 : même personnage avec un autre état narratif.</summary>
        public SavedCharacter WithStory(SavedStory story) =>
            new SavedCharacter(Id, ClassId, Xp, Talents, ActiveSkills, Ultimate, PassiveSkills, Inventory, Equipment, story, Professions, Money, EquipmentProperties);

        /// <summary>
        /// Essai réaliste 6 : même personnage avec d'autres rangs de talent (le build est jugé contre la classe et le niveau par le
        /// validateur des builds, pas ici).
        /// </summary>
        public SavedCharacter WithTalents(IReadOnlyDictionary<string, int> talents) =>
            new SavedCharacter(Id, ClassId, Xp, talents ?? throw new ArgumentNullException(nameof(talents)), ActiveSkills, Ultimate, PassiveSkills, Inventory,
                Equipment, Story, Professions, Money, EquipmentProperties);

        /// <summary>Sous-lot 12.6 : même personnage avec d'autres expériences de métier.</summary>
        public SavedCharacter WithProfessions(IReadOnlyDictionary<string, long> professions) =>
            new SavedCharacter(Id, ClassId, Xp, Talents, ActiveSkills, Ultimate, PassiveSkills, Inventory, Equipment, Story, professions, Money, EquipmentProperties);

        /// <summary>Sous-lot 12.9b : même personnage avec d'autres compétences équipées.</summary>
        public SavedCharacter WithSkills(IEnumerable<string> activeSkills, string? ultimate, IEnumerable<string> passiveSkills) =>
            new SavedCharacter(Id, ClassId, Xp, Talents, activeSkills, ultimate, passiveSkills, Inventory, Equipment, Story, Professions, Money, EquipmentProperties);

        /// <summary>
        /// Sous-lot V1.1 : même personnage avec une autre bourse (en cuivre). Hors de [0, <see cref="Coins.Max"/>] : refusée
        /// (<see cref="ArgumentOutOfRangeException"/>, relecture de la PR #72) ; les opérations du jeu passent par <see cref="Coins.TryAdd"/>,
        /// qui refuse un dépassement.
        /// </summary>
        public SavedCharacter WithMoney(long money) =>
            new SavedCharacter(Id, ClassId, Xp, Talents, ActiveSkills, Ultimate, PassiveSkills, Inventory, Equipment, Story, Professions, money, EquipmentProperties);
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
        /// sauvegarde de version 4 est mise à jour (aucun métier). Version 6 (sous-lot V1.1) : <c>money</c> obligatoire dans le personnage
        /// (bourse en pièces de cuivre, de 0 à <see cref="Coins.Max"/>) ; une sauvegarde de version 5 est mise à jour (bourse vide). Les points
        /// sûrs découverts (voyage rapide) sont des clés du registre des événements consommés (<c>zone.vallons/safe/place_du_bourg</c>), déjà
        /// gardé depuis la version 1. Version 7 (sous-lot V1.2) : l'équipement suit les 18 places du catalogue (deux anneaux <c>ring_1</c> et
        /// <c>ring_2</c>, deux reliques, l'Éclat du Seuil <c>shard</c>, l'outil <c>tool</c>…), forme inchangée ; une sauvegarde de version 6
        /// est lue telle quelle (ses emplacements <c>weapon</c>, <c>shield</c>, <c>chest</c>, <c>neck</c> gardent leur nom) et mise à jour : à la
        /// reprise, ses places obligatoires vides reçoivent leur objet (<c>Inventory.FillRequired</c>). Le numéro change pour qu'un programme
        /// antérieur refuse d'emblée une sauvegarde aux nouvelles places au lieu de la croire incohérente. Version 8 (sous-lot V1.6) :
        /// <c>tracked</c> obligatoire dans l'histoire (quête suivie, en cours, ou null) et étape réservée <c>reward</c> (récompense au choix en
        /// attente, <see cref="QuestProgress.Reward"/>) ; une sauvegarde de version 7 ou antérieure est mise à jour (aucune quête suivie) et
        /// n'a pas d'étape <c>reward</c> (refusée à <c>quests.&lt;id&gt;.step</c>, comme <c>tracked</c>). Version 9 (sous-lot V1.3) : <b>instance d'objet</b> : une place de l'inventaire peut porter <c>properties</c>
        /// (propriétés tirées : <c>id</c> « property.… », <c>value</c> entière ; quantité 1), et un emplacement d'équipement peut s'écrire
        /// <c>{ "item": …, "properties": […] }</c> au lieu du seul identifiant ; les noms <c>quality</c> et <c>wear</c> d'une instance sont
        /// réservés au sous-lot V1.4 (refusés). Une sauvegarde de version 8 ou antérieure est lue telle quelle (objets de base, sans propriétés) et mise à jour.
        /// Version 10 (sous-lot V1.7, <see cref="GuildCardSince"/>) : <c>guild</c> facultatif dans l'histoire (la carte d'aventurier ; absent
        /// tant que le personnage n'est pas inscrit) et <c>hunted</c> facultatif (créatures rares déjà vaincues) ; une sauvegarde de version 9
        /// ou antérieure est mise à jour (pas de carte) et n'a ni <c>guild</c> ni <c>hunted</c> (refusés, champs inconnus).
        /// </summary>
        public const int CurrentSchemaVersion = 10;

        /// <summary>Sous-lot V1.7 : version où la carte d'aventurier (<c>story.guild</c>) entre dans la sauvegarde.</summary>
        public const int GuildCardSince = 10;

        /// <summary>Sous-lot V1.3 : propriétés tirées d'une instance d'objet, au plus.</summary>
        public const int MaxProperties = 8;

        /// <summary>Sous-lot V1.3 : valeur d'une propriété tirée, au plus (comme un bonus d'objet du catalogue).</summary>
        public const int MaxPropertyValue = 50_000;

        private static readonly Regex PropertyId = new Regex(@"^property(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);

        /// <summary>Identifiant de propriété tirée valide : « property.… », 200 caractères au plus.</summary>
        public static bool IsPropertyId(string? id) => id != null && id.Length <= 200 && PropertyId.IsMatch(id);

        /// <summary>
        /// Sous-lot V1.3 : forme des propriétés d'une instance : 1 à <see cref="MaxProperties"/>, identifiants bien formés et distincts, valeurs
        /// dans [1, <see cref="MaxPropertyValue"/>]. Leur accord avec le catalogue (propriété connue, admise pour cet objet, valeur dans ses
        /// bornes) est vérifié quand la partie reprend (<c>ItemCatalog.CheckInstance</c>). Rend les propriétés, ou null en cas d'erreur.
        /// </summary>
        private static List<RolledProperty>? CheckProperties(List<PropertyRollData?> list, string path, ValidationReport report)
        {
            if (list.Count < 1 || list.Count > MaxProperties)
            {
                report.Add(path, ValidationCode.OutOfRange, $"{list.Count} propriétés, de 1 à {MaxProperties} (absentes pour un objet de base).");
                return null;
            }
            var result = new List<RolledProperty>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool ok = true;
            for (int k = 0; k < list.Count; k++)
            {
                string p = $"{path}[{k}]";
                if (list[k] is not PropertyRollData r) { Missing(report, p, "propriété tirée"); ok = false; continue; }
                if (r.Id is null) { Missing(report, p + ".id", "identifiant « property.… »"); ok = false; }
                else if (!IsPropertyId(r.Id)) { report.Add(p + ".id", ValidationCode.InvalidValue, $"propriété « {r.Id} » mal formée."); ok = false; }
                else if (!seen.Add(r.Id)) { report.Add(p + ".id", ValidationCode.DuplicateId, $"propriété « {r.Id} » déjà tirée."); ok = false; }
                if (r.Value is null) { Missing(report, p + ".value", "valeur entière"); ok = false; }
                else if (r.Value < 1 || r.Value > MaxPropertyValue) { report.Add(p + ".value", ValidationCode.OutOfRange, $"valeur hors de [1, {MaxPropertyValue}]."); ok = false; }
                if (ok) result.Add(new RolledProperty(r.Id!, r.Value!.Value));
            }
            return ok ? result : null;
        }

        /// <summary>Sous-lot V1.3 : champs réservés d'une instance (<c>quality</c>, <c>wear</c>), refusés jusqu'au sous-lot V1.4.</summary>
        private static void Reserved(ItemStackData stack, string path, ValidationReport report)
        {
            foreach (string field in stack.Reserved)
                report.Add($"{path}.{field}", ValidationCode.UnknownField, $"« {field} » est réservé : qualité et usure viennent avec le sous-lot V1.4.");
        }

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
                if (data.HasCharacter || data.Character != null) report.Add("$.data.character", ValidationCode.UnknownField, $"champ inconnu en version {document.SchemaVersion} (personnage : version 2).");
            }
            else if (!data.HasCharacter && data.Character == null) Missing(report, "$.data.character", "personnage du joueur, ou null");
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
            {
                // Revue Codex de la PR #44 : une sauvegarde construite en objets publics est contrôlée de même (un seul rang par talent).
                var seenTalents = new HashSet<string>(StringComparer.Ordinal);
                foreach (var t in c.Talents)
                    if (!IsDataId(t.Key)) report.Add(path + ".talents." + t.Key, ValidationCode.InvalidValue, $"identifiant de talent invalide : {format}.");
                    else if (t.Value is not int rank) Missing(report, path + ".talents." + t.Key, "rang");
                    else if (rank < 0) report.Add(path + ".talents." + t.Key, ValidationCode.InvalidValue, "un rang ne peut pas être négatif.");
                    else if (!seenTalents.Add(t.Key)) report.Add(path + ".talents." + t.Key, ValidationCode.DuplicateId, "talent donné deux fois.");
            }
            void Names(List<string?>? list, string field)
            {
                if (list is null) { Missing(report, $"{path}.{field}", "liste, éventuellement vide"); return; }
                if (list.Count > MaxSkills) { report.Add($"{path}.{field}", ValidationCode.LimitExceeded, $"{list.Count} compétences, au plus {MaxSkills}."); return; }
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
                // Revue Codex de la PR #44 : refusés aussi quand ils sont seulement renseignés dans l'objet public (pas de perte silencieuse).
                foreach (string field in new[] { "inventory", "equipment" }.Where(f => c.Present.Contains(f) || (f == "inventory" ? c.Inventory != null : c.Equipment != null)))
                    report.Add($"{path}.{field}", ValidationCode.UnknownField, $"champ inconnu en version {version} (inventaire et équipement : version 3).");
            }
            else
            {
                if (c.Inventory is null) Missing(report, path + ".inventory", "places occupées de l'inventaire, éventuellement vide");
                else if (c.Inventory.Count > MaxInventory)   // revue Codex de la PR #44 : même borne que le lecteur JSON
                    report.Add(path + ".inventory", ValidationCode.LimitExceeded, $"{c.Inventory.Count} places, au plus {MaxInventory}.");
                else
                    for (int i = 0; i < c.Inventory.Count; i++)
                    {
                        string stackPath = $"{path}.inventory[{i}]";
                        if (c.Inventory[i] is not ItemStackData stack) { Missing(report, stackPath, "pile d'objets"); continue; }
                        if (stack.Item is null) Missing(report, stackPath + ".item", "identifiant d'objet");
                        else if (!IsItemId(stack.Item)) report.Add(stackPath + ".item", ValidationCode.InvalidValue, $"identifiant d'objet « {stack.Item} » invalide.");
                        if (stack.Count is null) Missing(report, stackPath + ".count", "quantité");
                        else if (stack.Count < 1 || stack.Count > MaxStack) report.Add(stackPath + ".count", ValidationCode.OutOfRange, $"quantité hors de [1, {MaxStack}].");
                        // Sous-lot V1.3 (version 9) : propriétés tirées d'une instance (quantité 1).
                        Reserved(stack, stackPath, report);
                        if (stack.Properties is { } properties)
                        {
                            if (version < 9) report.Add(stackPath + ".properties", ValidationCode.UnknownField, $"champ inconnu en version {version} (propriétés tirées : version 9).");
                            else
                            {
                                CheckProperties(properties, stackPath + ".properties", report);
                                if (stack.Count is int n && n != 1)
                                    report.Add(stackPath + ".count", ValidationCode.InvalidValue, "une instance aux propriétés tirées ne s'empile pas : quantité 1.");
                            }
                        }
                    }
                if (c.Equipment is null) Missing(report, path + ".equipment", "objet porté par emplacement, éventuellement vide");
                else if (c.Equipment.Count > MaxEquipment)
                    report.Add(path + ".equipment", ValidationCode.LimitExceeded, $"{c.Equipment.Count} emplacements, au plus {MaxEquipment}.");
                else
                {
                    // Revue Codex de la PR #44 : un emplacement une seule fois (sauvegarde construite en objets publics).
                    var seenSlots = new HashSet<string>(StringComparer.Ordinal);
                    for (int i = 0; i < c.Equipment.Count; i++)
                    {
                        KeyValuePair<string, string?> e = c.Equipment[i];
                        string slotPath = $"{path}.equipment.{e.Key}";
                        if (e.Key is null || !SlotId.IsMatch(e.Key) || e.Key.Length > 64) report.Add(slotPath, ValidationCode.InvalidValue, $"emplacement « {e.Key} » invalide.");
                        else if (!seenSlots.Add(e.Key)) report.Add(slotPath, ValidationCode.DuplicateId, $"emplacement « {e.Key} » donné deux fois.");
                        else if (e.Value is null) Missing(report, slotPath, "identifiant d'objet");
                        else if (!IsItemId(e.Value)) report.Add(slotPath, ValidationCode.InvalidValue, $"identifiant d'objet « {e.Value} » invalide.");
                        else if (i > 0 && string.CompareOrdinal(c.Equipment[i - 1].Key, e.Key) > 0)
                            report.Add(slotPath, ValidationCode.InvalidValue, "emplacements hors de l'ordre ordinal (forme canonique).");
                    }
                    // Sous-lot V1.3 (version 9) : objets portés sous leur forme d'instance (propriétés tirées obligatoires : sans elles, la forme
                    // simple, l'identifiant seul, est la forme canonique).
                    foreach (KeyValuePair<string, ItemStackData> e in c.EquipmentInstances ?? new List<KeyValuePair<string, ItemStackData>>())
                    {
                        string slotPath = $"{path}.equipment.{e.Key}";
                        if (version < 9) { report.Add(slotPath, ValidationCode.UnknownField, $"instance d'objet inconnue en version {version} (version 9)."); continue; }
                        if (!c.Equipment.Any(x => x.Key == e.Key)) { report.Add(slotPath, ValidationCode.InvalidValue, "propriétés d'un emplacement vide."); continue; }
                        Reserved(e.Value, slotPath, report);
                        if (e.Value.Properties is null) Missing(report, slotPath + ".properties", "propriétés tirées (sinon, l'identifiant seul)");
                        else CheckProperties(e.Value.Properties, slotPath + ".properties", report);
                    }
                }
            }
            // Sous-lot 12.5 (version 4) : état narratif personnel.
            SavedStory? story = null;
            if (version < 4)
            {
                // Comme l'inventaire et l'équipement (revue Codex de la PR #44) : refusé aussi quand il est renseigné hors du lecteur JSON.
                if (c.Present.Contains("story") || c.Story != null)
                    report.Add(path + ".story", ValidationCode.UnknownField, $"champ inconnu en version {version} (histoire : version 4).");
            }
            else if (c.Story is StoryStateData st) story = CheckStory(st, path + ".story", version, report);
            else Missing(report, path + ".story", "faits, quêtes, confiance, découvertes");
            // Sous-lot 12.6 (version 5) : expérience des métiers.
            if (version < 5)
            {
                // Comme l'histoire : refusé aussi quand il est renseigné hors du lecteur JSON.
                if (c.Present.Contains("professions") || c.Professions != null)
                    report.Add(path + ".professions", ValidationCode.UnknownField, $"champ inconnu en version {version} (métiers : version 5).");
            }
            else if (c.Professions is null) Missing(report, path + ".professions", "expérience de chaque métier, éventuellement vide");
            else if (c.Professions.Count > MaxProfessions) report.Add(path + ".professions", ValidationCode.LimitExceeded, $"{c.Professions.Count} métiers, au plus {MaxProfessions}.");
            else
                for (int i = 0; i < c.Professions.Count; i++)
                {
                    var p = c.Professions[i];
                    string pp = $"{path}.professions.{p.Key}";
                    if (p.Key is null || p.Key.Length > 64 || !SlotId.IsMatch(p.Key)) report.Add(pp, ValidationCode.InvalidValue, "métier mal formé.");
                    else if (p.Value is not long xp) Missing(report, pp, "expérience");
                    else if (xp < 1 || xp > MaxXp) report.Add(pp, ValidationCode.OutOfRange, $"expérience de métier hors de [1, {MaxXp}] (une expérience nulle n'est pas écrite).");
                    // Même règle que la revue Codex de la PR #44 : un métier une seule fois (sauvegarde construite en objets publics).
                    else if (i > 0 && string.CompareOrdinal(c.Professions[i - 1].Key, p.Key) >= 0) report.Add(pp, ValidationCode.InvalidValue, "métiers hors de l'ordre ordinal ou en double.");
                }
            // Sous-lot V1.1 (version 6) : bourse en pièces de cuivre.
            if (version < 6)
            {
                // Comme les métiers : refusée aussi quand elle est renseignée hors du lecteur JSON (pas de perte silencieuse).
                if (c.Present.Contains("money") || c.Money != null)
                    report.Add(path + ".money", ValidationCode.UnknownField, $"champ inconnu en version {version} (bourse : version 6).");
            }
            else if (c.Money is null) Missing(report, path + ".money", "bourse en pièces de cuivre, 0 pour une bourse vide");
            else if (!Coins.IsValid(c.Money.Value)) report.Add(path + ".money", ValidationCode.OutOfRange, $"bourse hors de [0, {Coins.Max}] cuivre.");
            if (!report.IsValid) return null;
            return new SavedCharacter(c.Id!, c.Class!, c.Xp!.Value, c.Talents!.ToDictionary(t => t.Key, t => t.Value!.Value, StringComparer.Ordinal),
                c.ActiveSkills!.Select(s => s!), c.Ultimate, c.PassiveSkills!.Select(s => s!),
                version < 3 ? null : c.Inventory!.Select(st => new ItemStack(st!.Item!, st.Count!.Value,
                    st.Properties?.Select(r => new RolledProperty(r!.Id!, r.Value!.Value)))),
                version < 3 ? null : c.Equipment!.ToDictionary(e => e.Key, e => e.Value!, StringComparer.Ordinal), story,
                version < 5 ? null : c.Professions!.ToDictionary(p => p.Key, p => p.Value!.Value, StringComparer.Ordinal),
                version < 6 ? 0 : c.Money!.Value,
                version < 9 || c.EquipmentInstances is null ? null : c.EquipmentInstances.ToDictionary(e => e.Key,
                    e => (IReadOnlyList<RolledProperty>)e.Value.Properties!.Select(r => new RolledProperty(r!.Id!, r.Value!.Value)).ToList(), StringComparer.Ordinal));
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

        private static SavedStory? CheckStory(StoryStateData st, string path, int version, ValidationReport report)
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
                    if (id is null || id.Length > 200 || !QuestId.IsMatch(id)) { report.Add(p, ValidationCode.InvalidValue, "quête mal formée."); continue; }
                    // Même règle que la revue Codex de la PR #44 : une quête une seule fois (sauvegarde construite en objets publics).
                    if (i > 0 && string.CompareOrdinal(st.Quests[i - 1].Id, id) >= 0) report.Add(p, ValidationCode.InvalidValue, "quêtes hors de l'ordre ordinal ou en double (forme canonique).");
                    if (q is null) { Missing(report, p, "avancement"); continue; }
                    if (q.Step is null) Missing(report, p + ".step", "étape en cours, ou « done »");
                    else if (q.Step.Length > 64 || !SlotId.IsMatch(q.Step)) report.Add(p + ".step", ValidationCode.InvalidValue, $"étape « {q.Step} » mal formée.");
                    // Sous-lot V1.6 : l'étape réservée « reward » (récompense au choix en attente) n'existe qu'à partir de la version 8.
                    else if (version < 8 && q.Step == QuestProgress.Reward)
                        report.Add(p + ".step", ValidationCode.InvalidValue, $"étape « {QuestProgress.Reward} » inconnue en version {version} (récompense au choix : version 8).");
                    if (q.Progress is null) Missing(report, p + ".progress", "compteur");
                    else if (q.Progress < 0 || q.Progress > MaxProgress) report.Add(p + ".progress", ValidationCode.OutOfRange, $"compteur hors de [0, {MaxProgress}].");
                }
            }
            if (st.Trust is null) Missing(report, path + ".trust", "confiance des PNJ, éventuellement vide");
            else if (st.Trust.Count > MaxStoryEntries) report.Add(path + ".trust", ValidationCode.LimitExceeded, $"au plus {MaxStoryEntries}.");
            else
                for (int i = 0; i < st.Trust.Count; i++)
                {
                    var t = st.Trust[i];
                    string p = $"{path}.trust.{t.Key}";
                    if (t.Key is null || t.Key.Length > 200 || !NpcId.IsMatch(t.Key)) report.Add(p, ValidationCode.InvalidValue, "PNJ mal formé.");
                    else if (t.Value is not int v) Missing(report, p, "entier");
                    else if (v == 0 || v < -MaxTrust || v > MaxTrust) report.Add(p, ValidationCode.OutOfRange, $"confiance hors de [−{MaxTrust}, {MaxTrust}] ou nulle (une confiance nulle n'est pas écrite).");
                    else if (i > 0 && string.CompareOrdinal(st.Trust[i - 1].Key, t.Key) >= 0) report.Add(p, ValidationCode.InvalidValue, "confiance hors de l'ordre ordinal ou en double.");
                }
            // Sous-lot V1.6 (version 8, après la version 7 du sous-lot V1.2) : quête suivie, en cours (ni finie ni absente), ou null.
            if (version < 8)
            {
                // Comme la bourse : refusée aussi quand elle est renseignée hors du lecteur JSON (pas de perte silencieuse).
                if (st.HasTracked || st.Tracked != null)
                    report.Add(path + ".tracked", ValidationCode.UnknownField, $"champ inconnu en version {version} (quête suivie : version 8).");
            }
            else if (!st.HasTracked && st.Tracked is null) Missing(report, path + ".tracked", "quête suivie, ou null");
            else if (st.Tracked != null)
            {
                if (st.Tracked.Length > 200 || !QuestId.IsMatch(st.Tracked)) report.Add(path + ".tracked", ValidationCode.InvalidValue, "quête mal formée.");
                else if (st.Quests is null || !st.Quests.Any(q => q.Id == st.Tracked && q.Value?.Step != null && q.Value.Step != QuestProgress.Done))
                    report.Add(path + ".tracked", ValidationCode.UnknownReference, $"quête suivie « {st.Tracked} » absente des quêtes en cours.");
            }
            // Sous-lot V1.7 (version 10) : carte d'aventurier, facultative (absente : pas encore inscrit).
            SavedGuildCard? guild = null;
            if (version < GuildCardSince)
            {
                // Comme la quête suivie : refusée aussi quand elle est renseignée hors du lecteur JSON (pas de perte silencieuse).
                if (st.HasGuild || st.Guild != null)
                    report.Add(path + ".guild", ValidationCode.UnknownField, $"champ inconnu en version {version} (carte d'aventurier : version {GuildCardSince}).");
            }
            else if (st.Guild != null) guild = CheckGuild(st.Guild, path + ".guild", report);
            else if (st.HasGuild) report.Add(path + ".guild", ValidationCode.WrongType, "carte d'aventurier attendue (champ absent tant que le personnage n'est pas inscrit).");
            // Relecture de la PR #79 (version 10) : créatures rares déjà vaincues, facultatif (absent : aucune ; jamais une liste vide).
            if (version < GuildCardSince)
            {
                if (st.Hunted != null) report.Add(path + ".hunted", ValidationCode.UnknownField, $"champ inconnu en version {version} (créatures rares : version {GuildCardSince}).");
            }
            else if (st.Hunted != null)
            {
                if (st.Hunted.Count < 1 || st.Hunted.Count > SavedStory.MaxHunted)
                    report.Add(path + ".hunted", st.Hunted.Count < 1 ? ValidationCode.OutOfRange : ValidationCode.LimitExceeded,
                        $"de 1 à {SavedStory.MaxHunted} créatures (absent quand il n'y en a aucune).");
                else
                    for (int i = 0; i < st.Hunted.Count; i++)
                    {
                        string p = $"{path}.hunted[{i}]";
                        if (st.Hunted[i] is not string id || id.Length > 64 || !SlotId.IsMatch(id)) report.Add(p, ValidationCode.InvalidValue, "créature mal formée.");
                        else if (i > 0 && st.Hunted[i - 1] is string prev && string.CompareOrdinal(prev, id) >= 0)
                            report.Add(p, ValidationCode.InvalidValue, "liste hors de l'ordre ordinal ou avec doublon (forme canonique).");
                    }
            }
            if (!report.IsValid) return null;
            return new SavedStory(st.Facts!.Select(f => f!), st.Quests!.ToDictionary(q => q.Id, q => new QuestProgress(q.Value!.Step!, q.Value.Progress!.Value), StringComparer.Ordinal),
                st.Trust!.ToDictionary(t => t.Key, t => t.Value!.Value, StringComparer.Ordinal), st.Discoveries!.Select(d => d!), version < 8 ? null : st.Tracked, guild,
                st.Hunted?.Select(h => h!));
        }

        private static readonly Regex TitleId = new Regex(@"^title(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);
        private static readonly Regex ContractId = new Regex(@"^contract(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);

        /// <summary>
        /// Sous-lot V1.7 : forme de la carte d'aventurier (rang connu, points et contrats bornés, listes triées sans doublon, titre affiché parmi
        /// les titres, contrats en cours bornés). Son accord avec les données de la guilde (rang ouvert, titres et contrats connus) est vérifié
        /// quand la partie reprend, comme les quêtes.
        /// </summary>
        private static SavedGuildCard? CheckGuild(GuildCardData g, string path, ValidationReport report)
        {
            int errors = report.Errors.Count;
            if (g.Rank is null) Missing(report, path + ".rank", "rang (g, f, e…)");
            else if (!GuildRanks.IsRank(g.Rank)) report.Add(path + ".rank", ValidationCode.InvalidValue, $"rang « {g.Rank} » inconnu ; possibles : {GuildRanks.Known}.");
            if (g.Points is null) Missing(report, path + ".points", "points de guilde");
            else if (g.Points < 0 || g.Points > SavedGuildCard.MaxPoints) report.Add(path + ".points", ValidationCode.OutOfRange, $"points hors de [0, {SavedGuildCard.MaxPoints}].");
            if (g.Contracts is null) Missing(report, path + ".contracts", "contrats réussis");
            else if (g.Contracts < 0 || g.Contracts > SavedGuildCard.MaxContracts) report.Add(path + ".contracts", ValidationCode.OutOfRange, $"contrats hors de [0, {SavedGuildCard.MaxContracts}].");
            void Sorted(List<string?>? list, string field, Func<string, bool> valid, int max, string what)
            {
                if (list is null) { Missing(report, $"{path}.{field}", what + ", éventuellement aucun"); return; }
                if (list.Count > max) { report.Add($"{path}.{field}", ValidationCode.LimitExceeded, $"{list.Count} entrées, au plus {max}."); return; }
                for (int i = 0; i < list.Count; i++)
                {
                    string p = $"{path}.{field}[{i}]";
                    if (list[i] is not string id || !valid(id)) report.Add(p, ValidationCode.InvalidValue, $"{what} mal formé(e).");
                    else if (i > 0 && list[i - 1] is string prev && string.CompareOrdinal(prev, id) >= 0)
                        report.Add(p, ValidationCode.InvalidValue, "liste hors de l'ordre ordinal ou avec doublon (forme canonique).");
                }
            }
            Sorted(g.Titles, "titles", id => id.Length <= 200 && TitleId.IsMatch(id), SavedGuildCard.MaxTitles, "titre « title.… »");
            if (!g.HasTitle) Missing(report, path + ".title", "titre affiché, ou null");
            else if (g.Title != null && (g.Titles is null || !g.Titles.Contains(g.Title)))
                report.Add(path + ".title", ValidationCode.UnknownReference, $"titre affiché « {g.Title} » absent des titres gagnés.");
            if (g.Active is null) Missing(report, path + ".active", "contrats en cours, éventuellement aucun");
            else if (g.Active.Count > SavedGuildCard.MaxActive) report.Add(path + ".active", ValidationCode.LimitExceeded, $"{g.Active.Count} contrats en cours, au plus {SavedGuildCard.MaxActive}.");
            else
                for (int i = 0; i < g.Active.Count; i++)
                {
                    var a = g.Active[i];
                    string p = $"{path}.active.{a.Key}";
                    if (a.Key is null || a.Key.Length > 200 || !ContractId.IsMatch(a.Key)) report.Add(p, ValidationCode.InvalidValue, "contrat mal formé : « contract.… ».");
                    else if (a.Value is not long progress) Missing(report, p, "avancement");
                    else if (progress < 0 || progress > MaxProgress) report.Add(p, ValidationCode.OutOfRange, $"avancement hors de [0, {MaxProgress}].");
                    else if (i > 0 && string.CompareOrdinal(g.Active[i - 1].Key, a.Key) >= 0) report.Add(p, ValidationCode.InvalidValue, "contrats hors de l'ordre ordinal ou en double.");
                }
            if (g.Day is null) Missing(report, path + ".day", "jour du tableau des contrats pris");
            else if (g.Day < 0 || g.Day > MaxWorldTick) report.Add(path + ".day", ValidationCode.OutOfRange, $"jour hors de [0, {MaxWorldTick}].");
            Sorted(g.Taken, "taken", id => id.Length <= 200 && ContractId.IsMatch(id), SavedGuildCard.MaxTaken, "contrat « contract.… »");
            if (report.Errors.Count > errors) return null;
            return new SavedGuildCard(g.Rank!, g.Points!.Value, g.Contracts!.Value, g.Titles!.Select(t => t!), g.Title,
                g.Active!.ToDictionary(a => a.Key, a => a.Value!.Value, StringComparer.Ordinal), g.Day!.Value, g.Taken!.Select(t => t!));
        }

        /// <summary>Sous-lot 12.3 : plus grand nombre de places d'un inventaire sauvegardé, et plus grande pile.</summary>
        public const int MaxInventory = 200;

        /// <summary>Emplacements d'équipement et compétences d'une liste, au plus (mêmes bornes que le lecteur JSON).</summary>
        public const int MaxEquipment = 64, MaxSkills = 64;

        /// <summary>Sous-lot 12.6 : métiers d'une sauvegarde, au plus (même borne que le lecteur JSON).</summary>
        public const int MaxProfessions = 64;

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
