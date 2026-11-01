using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data;
using EdS.Data.Characters;
using EdS.Data.Classes;
using EdS.Data.Crafting;
using EdS.Data.Guild;
using EdS.Data.Items;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Zones;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;

namespace EdS.World
{
    /// <summary>Le personnage a gagné de l'expérience (créature vaincue), et peut-être un ou plusieurs niveaux.</summary>
    public sealed class ExperienceGained
    {
        /// <summary>Événement à l'origine du gain (mort d'une créature).</summary>
        public ZoneEvent Source { get; }
        public long Xp { get; }
        public int LevelBefore { get; }
        public int LevelAfter { get; }
        public bool LeveledUp => LevelAfter > LevelBefore;

        internal ExperienceGained(ZoneEvent source, long xp, int before, int after)
        {
            Source = source;
            Xp = xp;
            LevelBefore = before;
            LevelAfter = after;
        }
    }

    /// <summary>Sous-lot 12.3 : butin d'une créature vaincue, rangé dans l'inventaire ou perdu faute de place.</summary>
    public sealed class LootGained
    {
        /// <summary>Événement à l'origine du butin (mort d'une créature).</summary>
        public ZoneEvent Source { get; }
        public string Item { get; }
        public int Count { get; }

        /// <summary>Rangé dans l'inventaire ; faux : inventaire plein, le butin est perdu (rien n'est rangé en partie).</summary>
        public bool Kept { get; }

        /// <summary>Sous-lot V1.3 : propriétés tirées de l'objet tombé (vide pour un objet de base ou nommé).</summary>
        public IReadOnlyList<RolledProperty> Properties { get; }

        internal LootGained(ZoneEvent source, string item, int count, bool kept, IReadOnlyList<RolledProperty>? properties = null)
        {
            Source = source;
            Item = item;
            Count = count;
            Kept = kept;
            Properties = properties ?? Array.Empty<RolledProperty>();
        }
    }

    /// <summary>
    /// Ce qu'un tick de la partie a rapporté au personnage, ce qui a changé dans les populations de la zone (sous-lot 12.4) et dans son
    /// histoire (sous-lot 12.5 : quêtes, faits, découvertes).
    /// </summary>
    public sealed class SessionStep
    {
        public static readonly SessionStep Empty = new SessionStep(Array.Empty<ExperienceGained>(), Array.Empty<LootGained>(), Array.Empty<PopulationEvent>(),
            Array.Empty<StoryEvent>());

        public IReadOnlyList<ExperienceGained> Experience { get; }
        public IReadOnlyList<LootGained> Loot { get; }
        public IReadOnlyList<PopulationEvent> Population { get; }
        public IReadOnlyList<StoryEvent> Story { get; }

        /// <summary>Sous-lot 12.7 : formes du chef arrivées, victoire.</summary>
        public IReadOnlyList<BossEvent> Boss { get; }

        /// <summary>Sous-lot 12.7 : récompenses de la première victoire sur le chef, données à ce tick.</summary>
        public IReadOnlyList<(string Item, int Count)> Rewards { get; }

        /// <summary>Sous-lot 12.7 : le personnage est tombé à ce tick (défaite : la partie reprend au point sûr, sans perte).</summary>
        public bool Defeated { get; }

        /// <summary>Sous-lot V1.4 : usure appliquée à ce tick (fin d'un combat), ou null.</summary>
        public WearReport? Wear { get; }

        /// <summary>Sous-lot V1.8c : bonus finis à ce tick (dans l'ordre des places), qui ont quitté leur place.</summary>
        public IReadOnlyList<BoonExpired> BoonsExpired { get; }

        public bool IsEmpty => Experience.Count == 0 && Loot.Count == 0 && Population.Count == 0 && Story.Count == 0 && Boss.Count == 0 && Rewards.Count == 0 && !Defeated
            && Wear is null && BoonsExpired.Count == 0;

        internal SessionStep(IReadOnlyList<ExperienceGained> experience, IReadOnlyList<LootGained> loot, IReadOnlyList<PopulationEvent> population,
            IReadOnlyList<StoryEvent> story, IReadOnlyList<BossEvent>? boss = null, IReadOnlyList<(string, int)>? rewards = null, bool defeated = false,
            WearReport? wear = null, IReadOnlyList<BoonExpired>? boonsExpired = null)
        {
            BoonsExpired = boonsExpired ?? Array.Empty<BoonExpired>();
            Wear = wear;
            Experience = experience;
            Loot = loot;
            Population = population;
            Story = story;
            Boss = boss ?? Array.Empty<BossEvent>();
            Rewards = rewards ?? Array.Empty<(string, int)>();
            Defeated = defeated;
        }
    }

    /// <summary>
    /// Sous-lot 12.2 : une partie en solo. Elle reprend la sauvegarde (temps du monde, registre, personnage), ouvre une session de la zone
    /// (session suivante), y fait entrer le personnage et lui donne l'expérience des créatures vaincues : à chaque mort de créature consommée
    /// pour la première fois pendant que le personnage est présent et vivant, <see cref="CharacterProgression.KillXp"/> de ses PV maximaux.
    /// Un niveau gagné prend effet à la prochaine entrée dans une zone (le combat en cours garde le build d'entrée). Rien d'autre que la
    /// sauvegarde ne survit à la session. Sous-lot 12.3 : la partie tient l'inventaire et l'équipement du personnage ; une créature vaincue
    /// dans les mêmes conditions donne aussi le butin de sa table (tiré par la clé de sa mort, donc une seule fois). Le personnage entre avec
    /// le bonus de son équipement ; un changement d'équipement prend effet, comme un niveau, à la prochaine entrée.
    /// </summary>
    public sealed partial class WorldSession : INarrativeWorld
    {
        private readonly CharacterProgression _progression;
        private SavedCharacter _character;

        /// <summary>Essai réaliste 6 : classe du personnage et règles de progression (jugement des talents choisis en route).</summary>
        private readonly ClassDefinition? _class;
        private readonly ProgressionRules _rules;

        public Zone Zone { get; }

        /// <summary>Personnage tel qu'il sera sauvegardé (expérience à jour).</summary>
        public SavedCharacter Character => _character;

        /// <summary>Niveau du personnage d'après son expérience (celui de son entrée dans la zone peut être plus bas).</summary>
        public int Level => _progression.LevelFor(_character.Xp);

        /// <summary>Niveau avec lequel il est entré dans la zone (son build de combat).</summary>
        public int EnteredLevel { get; }

        /// <summary>Essai réaliste 6 : build du personnage à son niveau actuel (d'après son expérience), talents et compétences gardés.</summary>
        public CharacterBuild Build => PlayerCharacters.Build(_character, _progression);

        /// <summary>Essai réaliste 6 : classe du personnage.</summary>
        public ClassDefinition Class => _class ?? throw new InvalidOperationException("Classe du personnage absente des données.");

        /// <summary>Essai réaliste 6 : règles de progression des builds (points de talent, seuil du majeur).</summary>
        public ProgressionRules ProgressionRules => _rules;

        /// <summary>
        /// Essai réaliste 6 (essai de Damien du 27 octobre 2026 : l'arbre des talents, « je ne peux pas l'ouvrir ») : nouveaux rangs de talent
        /// du personnage, jugés à son niveau actuel par le validateur unique des builds. Admis, ils sont gardés (sauvegarde) et prennent effet
        /// à la prochaine entrée dans une zone, comme un niveau gagné ou un équipement changé (le combat en cours garde le build d'entrée) ;
        /// refusés, rien ne change et le premier motif est rendu.
        /// </summary>
        public BuildViolation? SetTalents(IReadOnlyDictionary<string, int> ranks)
        {
            if (ranks is null) throw new ArgumentNullException(nameof(ranks));
            SavedCharacter candidate = _character.WithTalents(ranks);
            BuildViolation? why = BuildValidator.Validate(PlayerCharacters.Build(candidate, _progression), Class, _rules).FirstOrDefault();
            if (why is null) _character = candidate;
            return why;
        }

        /// <summary>
        /// Sous-lot V1.5 (écran des compétences, décision de Damien : changer ses compétences « partout hors combat ») : nouvelle barre du
        /// personnage, jugée à son niveau actuel par <see cref="PlayerCharacters.CheckSkills"/> (places ouvertes, compétences apprises,
        /// validateur unique des builds ; une place peut rester vide). Admise, elle est gardée (sauvegarde) et prend effet à la prochaine
        /// entrée dans une zone, comme les talents (le combat en cours garde le build d'entrée) ; refusée, rien ne change et le premier motif
        /// est rendu.
        /// </summary>
        /// <exception cref="ArgumentException">Une liste contient null.</exception>
        public BuildViolation? SetSkills(IReadOnlyList<string> actives, string? ultimate, IReadOnlyList<string> passives)
        {
            if (actives is null) throw new ArgumentNullException(nameof(actives));
            if (passives is null) throw new ArgumentNullException(nameof(passives));
            BuildViolation? why = PlayerCharacters.CheckSkills(_character, actives, ultimate, passives, _progression, Class, _rules);
            if (why is null)
            {
                // Réponse de Damien du 30 octobre 2026 (une place peut rester vide) : le joueur a changé la barre de la prochaine halte, où
                // les compétences gagnées en route sont déjà placées ; elles sont données pour de bon, et la halte ne remet pas celle qu'il
                // vient de retirer.
                // Relecture indépendante de la PR #74 : tout ou rien. Sans la place de toutes les clés au registre, le changement entier est
                // refusé (sinon une compétence retirée reviendrait à la halte et serait annoncée de nouveau).
                PlayerCharacters.Learn(_character, _progression, _rules, Zone.IsConsumed, out _, out IReadOnlyList<SkillUnlock> handed);
                if (!CanGive(Keys(handed)))
                    return new BuildViolation(BuildViolationCode.RegistryFull, "skills",
                        "Le registre du monde est plein : ce changement de compétences ne peut pas être gardé.");
                _character = _character.WithSkills(actives, ultimate, passives);
                Give(Keys(handed));
            }
            return why;
        }

        /// <summary>Clés <see cref="SkillUnlock.GivenKey"/> de <paramref name="handed"/>.</summary>
        private static IEnumerable<string> Keys(IEnumerable<SkillUnlock> handed) => handed.Select(u => u.GivenKey);

        /// <summary>Clés de <paramref name="keys"/> pas encore au registre, sans doublon.</summary>
        private List<string> ToGive(IEnumerable<string> keys) =>
            keys.Distinct(StringComparer.Ordinal).Where(k => !Zone.IsConsumed(k)).ToList();

        /// <summary>
        /// Le registre a la place de toutes les clés à donner, en gardant celle des morts possibles d'un tick (la règle de la découverte d'un
        /// point sûr) ; vrai s'il n'y a rien à donner.
        /// </summary>
        private bool CanGive(IEnumerable<string> keys)
        {
            int count = ToGive(keys).Count;
            return count == 0 || Zone.RegistryRoom - count >= Zone.Engine.CombatantIds.Count;
        }

        /// <summary>
        /// Inscrit au registre du monde les compétences données (<see cref="SkillUnlock.GivenKey"/>), <b>toutes ou aucune</b> (relecture
        /// indépendante de la PR #74) : seulement s'il reste au registre, après elles, la place des morts possibles d'un tick ; sinon rien
        /// n'est inscrit (à l'entrée, elles seront données à une halte suivante ; un changement de barre est refusé avant, par
        /// <see cref="CanGive"/>). Rend vrai si tout est inscrit.
        /// </summary>
        private bool Give(IEnumerable<string> keys)
        {
            if (!CanGive(keys)) return false;
            foreach (string key in ToGive(keys)) Zone.Consume(key);
            return true;
        }

        /// <summary>
        /// Clés à inscrire à l'entrée de cette session (<see cref="Give"/>), quand la session est prête : compétences données par la halte,
        /// et pour une sauvegarde d'avant V1.8, les compétences déjà connues (données sans être placées) et <see cref="PlayerCharacters.PlacesKey"/>.
        /// Toutes ou aucune : sans la place au registre, la migration recommence au chargement suivant.
        /// </summary>
        private readonly IReadOnlyList<string> _keysAtEntry;

        /// <summary>
        /// Inscrit les clés de l'entrée (<see cref="Give"/>, toutes ou aucune). Remarque de Codex sur la PR #82 : si la conversion d'avant V1.8
        /// a été faite et que tout n'a pas pu être inscrit, la clé <see cref="PlayerCharacters.PlacesKey"/> l'est seule (la place en a été
        /// vérifiée avant la conversion) : la conversion n'est jamais refaite.
        /// </summary>
        private void GiveAtEntry()
        {
            if (!Give(_keysAtEntry) && _migrating && !Zone.IsConsumed(PlayerCharacters.PlacesKey) && Zone.RegistryRoom > 0)
                Zone.Consume(PlayerCharacters.PlacesKey);
        }

        /// <summary>Remarque de Codex sur la PR #82 : l'expérience d'une sauvegarde d'avant V1.8 a été convertie à cette entrée.</summary>
        public bool XpMigrated { get; }

        /// <summary>Remarque de Codex sur la PR #82 : compétences retirées de la barre à l'entrée (build devenu invalide), dans la réserve.</summary>
        public IReadOnlyList<string> RemovedSkills { get; } = Array.Empty<string>();

        /// <summary>Remarque de Codex sur la PR #82 : points de talent rendus à l'entrée (dépense devenue invalide).</summary>
        public int RefundedTalentPoints { get; }

        /// <summary>Cette entrée applique la conversion d'avant V1.8 : <see cref="PlayerCharacters.PlacesKey"/> doit être inscrite.</summary>
        private readonly bool _migrating;

        /// <summary>
        /// Place à garder libre au registre pour convertir une sauvegarde d'avant V1.8 : ses clés (compétences, <see cref="PlayerCharacters.PlacesKey"/>),
        /// les morts possibles d'un tick (combattants de la zone, chefs et vies suivantes compris, avec une large marge) et la découverte.
        /// </summary>
        private static int MigrationRoom(int combatants) => checked(combatants * 2 + 256);

        /// <summary>
        /// Remarque de Codex sur la PR #83 : expérience que le personnage de <paramref name="save"/> garde à l'entrée, après la conversion d'une
        /// sauvegarde d'avant V1.8 (<see cref="PlayerCharacters.MigrateXp"/>), et si cette conversion a lieu (<paramref name="migrating"/>) :
        /// clé <see cref="PlayerCharacters.PlacesKey"/> absente et registre assez grand pour elle (<see cref="MigrationRoom"/>, avec les
        /// <paramref name="combatants"/> de la zone). Une seule règle pour le réglage de la zone (<see cref="ForPlayer"/>) et l'entrée du
        /// personnage : les créatures sont réglées au niveau qu'il garde vraiment.
        /// </summary>
        private static long RetainedXp(WorldSave save, SavedCharacter character, CharacterProgression progression, int combatants, out bool migrating)
        {
            migrating = !save.Consumed.Contains(PlayerCharacters.PlacesKey)
                && SaveValidator.MaxConsumed - save.Consumed.Count >= MigrationRoom(combatants);
            return migrating ? PlayerCharacters.MigrateXp(character.Xp, progression) : character.Xp;
        }

        /// <summary>
        /// Relecture indépendante de la PR #82 : compétences qu'une sauvegarde d'avant V1.8 connaissait déjà, inscrites comme données sans être
        /// placées au premier chargement (<see cref="PlayerCharacters.KnownBeforePlaces"/>) ; vide pour une sauvegarde de V1.8.
        /// </summary>
        public IReadOnlyList<SkillUnlock> KnownBefore { get; }

        /// <summary>
        /// Sous-lot V1.5 : le personnage tel qu'il entrera dans la prochaine session : les compétences gagnées depuis cette entrée déjà
        /// équipées dans les places libres (<see cref="PlayerCharacters.Learn"/>, sans rien changer ici). C'est la barre que montre et
        /// que modifie l'écran des compétences.
        /// </summary>
        public SavedCharacter NextEntry => PlayerCharacters.Learn(_character, _progression, _rules, Zone.IsConsumed, out _, out _);

        /// <summary>Sous-lot 12.9b : compétences gagnées et équipées à l'entrée dans cette session (niveau atteint depuis la précédente).</summary>
        public IReadOnlyList<SkillUnlock> Learned { get; }

        /// <param name="zoneSetup">Simulation de la zone (mode zone).</param>
        /// <param name="save">Sauvegarde reprise ; son personnage doit exister (sinon : <see cref="PlayerCharacters.Create"/> d'abord).</param>
        /// <param name="placement">Place d'entrée du personnage dans la zone.</param>
        /// <exception cref="ArgumentException">Sauvegarde sans personnage, d'une autre zone, ou build invalide à son niveau.</exception>
        /// <param name="items">Catalogue des objets (inventaire, équipement, butin).</param>
        /// <param name="lootTables">Table de butin de chaque créature de la zone, par identifiant de combattant ; une créature absente ne donne
        /// rien.</param>
        public WorldSession(WorldSave save, CombatSetup zoneSetup, GameData data, CharacterProgression progression, ItemCatalog items,
            CombatantPlacement placement, IReadOnlyDictionary<string, string>? lootTables = null, ICombatLog? log = null)
            : this(save, zoneSetup, data, progression, items, placement, lootTables, log, true)
        {
            GiveAtEntry();
        }

        private WorldSession(WorldSave save, CombatSetup zoneSetup, GameData data, CharacterProgression progression, ItemCatalog items,
            CombatantPlacement placement, IReadOnlyDictionary<string, string>? lootTables, ICombatLog? log, bool _)
        {
            _items = items;
            if (save is null) throw new ArgumentNullException(nameof(save));
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            if (items is null) throw new ArgumentNullException(nameof(items));
            _character = save.Character ?? throw new ArgumentException("Sauvegarde sans personnage : le créer d'abord.", nameof(save));
            // Relecture de la PR #72 : bourse dans ses bornes (déjà vérifiée à la construction du personnage ; le contrat de la partie est
            // dit ici aussi, la bourse affichée ne peut pas sortir de [0, Coins.Max]).
            if (!Coins.IsValid(_character.Money)) throw new ArgumentException($"Bourse hors de [0, {Coins.Max}] cuivre.", nameof(save));
            // Sous-lot 12.9b : un niveau prend effet à l'entrée, avec les compétences qu'il fait gagner (gardées dans la sauvegarde).
            if (data is null) throw new ArgumentNullException(nameof(data));
            // Réponse de Damien du 30 octobre 2026 : une compétence déjà donnée (clé au registre) n'est plus placée d'office.
            var given = new HashSet<string>(save.Consumed, StringComparer.Ordinal);
            // Remarque de Codex sur la PR #82 : au premier chargement d'une sauvegarde d'avant V1.8 (sans PlayerCharacters.PlacesKey),
            // l'expérience est convertie aux nouveaux seuils (le personnage garde son ancien niveau), une seule fois : la clé est inscrite avec
            // les compétences à l'entrée. Seulement si le registre a largement la place de cette clé (sinon elle ne serait pas gardée et la
            // conversion serait refaite) ; sinon l'expérience reste telle quelle et la barre est réparée plus bas.
            long retained = RetainedXp(save, _character, _progression, zoneSetup.Combatants.Count, out bool migrating);
            if (migrating)
            {
                XpMigrated = retained != _character.Xp;
                _character = _character.WithXp(retained);
            }
            // Relecture indépendante de la PR #82 : au premier chargement d'une sauvegarde d'avant V1.8, ce que le personnage connaissait déjà
            // est donné sans être placé (Provocation, en réserve avec les anciennes règles, ne remplit pas une place vidée par le joueur).
            KnownBefore = PlayerCharacters.KnownBeforePlaces(_character, _progression, given.Contains);
            foreach (SkillUnlock known in KnownBefore) given.Add(known.GivenKey);
            _character = PlayerCharacters.Learn(_character, _progression, data.Rules.Progression, given.Contains, out IReadOnlyList<SkillUnlock> learned,
                out IReadOnlyList<SkillUnlock> handedAtEntry);
            var keysAtEntry = Keys(KnownBefore).Concat(Keys(handedAtEntry)).ToList();
            if (migrating) keysAtEntry.Add(PlayerCharacters.PlacesKey);
            _keysAtEntry = keysAtEntry;
            _migrating = migrating;
            Learned = learned;
            _rules = data.Rules.Progression;
            _class = data.TryGetClass(_character.ClassId, out LoadedClass loaded) ? loaded.Definition : null;   // absente : refusée à l'entrée (Setup)
            // Remarque de Codex sur la PR #82 : le chargement ne lève jamais pour un build devenu invalide (niveau plus bas, talents en trop) :
            // la barre et les talents sont réparés de façon déterministe, et c'est annoncé.
            if (_class != null)
            {
                _character = PlayerCharacters.RepairBuild(_character, _progression, _class, _rules, out IReadOnlyList<string> removed, out int refunded);
                RemovedSkills = removed;
                RefundedTalentPoints = refunded;
            }
            _lootTables = new Dictionary<string, LootTable>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> entry in lootTables ?? new Dictionary<string, string>())
                _lootTables[entry.Key] = items.TryGetLoot(entry.Value, out LootTable table) ? table
                    : throw new ArgumentException($"Table de butin « {entry.Value} » ({entry.Key}) absente du catalogue.", nameof(lootTables));
            Inventory = Inventory.From(_character, items, Level, out IReadOnlyList<string> adjusted);
            // Sous-lot V1.8c : bonus actifs accordés au catalogue actuel (comme les propriétés tirées), après elles dans le compte rendu.
            _boons = SettleBoons(_character.Boons, items, out IReadOnlyList<string> boonNotes);
            Adjusted = boonNotes.Count == 0 ? adjusted : adjusted.Concat(boonNotes).ToList();   // relecture de la PR #78 : propriétés tirées accordées au catalogue actuel
            // Sous-lot V1.2 : les places obligatoires vides sont remplies : l'Éclat du Seuil, et pour le Bastion un bouclier (le sien s'il en a
            // un dans l'inventaire, sinon l'écu de départ). Relecture de la PR #75 : à chaque chargement, pas seulement pour une sauvegarde mise
            // à jour depuis une version antérieure (une sauvegarde de version 7 aux places obligatoires vides est réparée elle aussi).
            // FillRequired est idempotente : sans place obligatoire vide, elle ne change rien et rend une liste vide.
            Restored = Inventory.FillRequired(Level);
            // Sous-lot 12.6 : objets de soin de l'inventaire emportés au combat, au plus une pile de chacun (recharge partagée).
            var carried = new List<ConsumableSetup>();
            foreach (string id in Inventory.Stacks.Select(st => st.Item).Distinct().OrderBy(i => i, StringComparer.Ordinal))
                if (items.Item(id) is { Type: ItemType.Consumable } potion)
                {
                    int count = Math.Min(Inventory.Count(id), potion.Stack);
                    carried.Add(new ConsumableSetup(id, potion.Heal, count, potion.CooldownMs));
                    _carried[id] = count;
                    Inventory.Reserve(id, count);   // revue Codex de la PR #47 : emportés, ils ne se retirent plus de l'inventaire avant d'être bus
                }
            // Sous-lot V1.8c : les bonus actifs s'ajoutent au bonus de l'équipement, dans la limite du combat (BoonGear).
            EnteredBoons = ActiveBoons;
            // Sous-lot V1.8d : bonus de l'équipement porté à l'entrée, base fixe de la session à laquelle s'ajoutent les bonus actifs.
            _entryEquipmentGear = Inventory.Gear();
            CharacterSetup setup = PlayerCharacters.Setup(_character, progression, data, placement, gear: BoonGear(_entryEquipmentGear, items, EnteredBoons), consumables: carried,
                shield: Inventory.ShieldFor(data.Rules.Combat),   // sous-lot V1.2 : garde du bouclier porté ; V1.4 : jamais meilleure s'il est usé à zéro
                equipment: Inventory.Effects());   // sous-lot V1.3 : effets des objets nommés et des ensembles
            // Revue Codex de la PR #43 : le personnage entre avec la session, dans sa simulation de départ (en-tête du journal compris).
            Zone = new Zone(save.Zone, zoneSetup, checked(save.Sessions + 1), save.WorldTick, save.Consumed, log, entering: new[] { setup });
            EnteredLevel = setup.Build.Level;
            // Sous-lot V1.4 : point de départ du relevé des dégâts reçus, et pièces portées à l'entrée (celles du combat de cette session).
            _lastTaken = Zone.Engine.View(_character.Id).Counters.DamageTaken;
            List<string> entryPlaces = Inventory.Places().Where(p => Inventory.WornInstanceAt(p) is { } x && items.Wears(items.Item(x.Item))).ToList();
            _entryPieces = entryPlaces.Select(p => Inventory.WornInstanceAt(p)!).ToList();   // les instances mêmes (suivi par identité)
            EnteredGear = setup.Gear;
            _combatGear = setup.Gear;   // sous-lot V1.8d : bonus du combat, changé ensuite par les bonus modifiés
            EnteredEffects = setup.Equipment;
        }

        private readonly Dictionary<string, LootTable> _lootTables;

        /// <summary>Sous-lot 12.6 : charges de chaque objet de soin emporté (réservées dans l'inventaire), telles qu'usées jusqu'ici.</summary>
        private readonly Dictionary<string, int> _carried = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Sous-lot 12.6 : récolte, fabrication et expérience des métiers (null sans métiers chargés).</summary>
        public Workshop? Workshop { get; }

        /// <summary>
        /// Boit un objet de soin emporté (commande <c>consume</c> au tick courant, appliquée au prochain tick ; le moteur la refuse au besoin :
        /// charges épuisées, recharge partagée, PV pleins, étourdi). L'inventaire perd l'objet quand le moteur l'a bu.
        /// </summary>
        /// <exception cref="ArgumentException">Objet non emporté, ou commande refusée par le pilote.</exception>
        public void Drink(string item)
        {
            if (item is null || !_carried.ContainsKey(item)) throw new ArgumentException($"Objet de soin « {item} » non emporté.", nameof(item));
            Zone.Engine.Submit(CombatCommand.Use(checked(Zone.Engine.CurrentTick * Zone.Engine.TickMilliseconds), _character.Id, item));
        }

        /// <summary>Sous-lot 12.6 : récolte un nœud à portée (3 m), s'il a repoussé et que l'objet tient.</summary>
        public CraftRefusal Gather(string node, out CraftEvent? done) =>
            (Workshop ?? throw new InvalidOperationException("Partie sans métiers.")).Gather(node, Zone.Engine.CurrentTick, Zone.Engine.TickMilliseconds,
                ((INarrativeWorld)this).PlayerPosition, Inventory, out done);

        /// <summary>Sous-lot 12.6 : fabrique une recette dans son atelier (lieu de la zone) ; une recette à qualité attend ses frappes (V1.4).</summary>
        public CraftRefusal Craft(string recipe, out CraftEvent? done) => Craft(recipe, null, out done);

        /// <summary>
        /// Sous-lot V1.4 : fabrique une recette dans son atelier, avec les notes des frappes du petit jeu de forge pour une recette à qualité
        /// (<see cref="Workshop.Craft"/>). Le tirage se fait sur la clé stable (recette, jour du monde, compteur des fabrications du
        /// personnage) ; chaque tentative, réussie ou ratée, fait avancer le compteur (gardé dans la sauvegarde).
        /// </summary>
        public CraftRefusal Craft(string recipe, ForgeStrikes? strikes, out CraftEvent? done)
        {
            Workshop workshop = Workshop ?? throw new InvalidOperationException("Partie sans métiers.");
            Recipe? r = workshop.Book.Recipes.FirstOrDefault(x => x.Id == recipe);
            CraftRefusal refusal = workshop.Craft(recipe, r != null && ((INarrativeWorld)this).InPlace(r.Place), Inventory, strikes, _character.Id, Zone.Time.Day,
                _character.Crafts, out done);
            if (refusal == CraftRefusal.None) _character = _character.WithCrafts(Math.Min(SavedCharacter.MaxCrafts, checked(_character.Crafts + 1)));
            return refusal;
        }

        /// <summary>Sous-lot 12.8 : refus qu'aurait la fabrication de <paramref name="recipe"/> maintenant (rien ne change).</summary>
        public CraftRefusal CanCraft(string recipe)
        {
            Workshop workshop = Workshop ?? throw new InvalidOperationException("Partie sans métiers.");
            Recipe? r = workshop.Book.Recipes.FirstOrDefault(x => x.Id == recipe);
            return workshop.Check(recipe, r != null && ((INarrativeWorld)this).InPlace(r.Place), Inventory);
        }

        /// <summary>Retire de l'inventaire les objets de soin que le moteur a bus depuis le dernier relevé.</summary>
        private void SyncConsumables()
        {
            if (_carried.Count == 0) return;
            CombatantView me = Zone.Engine.View(_character.Id);
            foreach (string id in _carried.Keys.ToArray())
            {
                int now = me.ConsumableCharges.TryGetValue(id, out int left) ? left : _carried[id];
                int drunk = _carried[id] - now;
                if (drunk <= 0) continue;
                Inventory.UseReserved(id, drunk);   // réservés à l'entrée : toujours là
                _carried[id] = now;
            }
        }
        private readonly ItemCatalog _items;
        private readonly ZonePopulations? _populations;

        /// <summary>Sous-lot 12.4 : zone du monde de la partie (null pour une partie sur une simulation donnée).</summary>
        public ZoneDefinition? Definition { get; }

        /// <summary>Rayon de l'emprise d'un personnage qui entre par un point sûr (provisoire, celui des scénarios).</summary>
        public static readonly Fixed CharacterRadius = ZoneValidator.CharacterRadius;

        /// <summary>
        /// Sous-lot 12.4 : partie dans une zone du monde. La session de zone part de sa simulation, passages ouverts retirés
        /// (<see cref="ZoneSetups"/>) ; le personnage entre au point sûr d'entrée ; les populations réapparaissent et tiennent leur laisse ;
        /// chaque créature donne le butin de sa population.
        /// </summary>
        /// <exception cref="ArgumentException">Sauvegarde d'une autre zone, sans personnage, ou incohérente avec le catalogue.</exception>
        /// <param name="story">Sous-lot 12.5 : histoire de la zone (PNJ, dialogues, quêtes) ; sans elle, l'état narratif sauvegardé est gardé tel quel.</param>
        /// <param name="crafting">Sous-lot 12.6 : métiers, recettes et nœuds de récolte ; sans eux, l'expérience des métiers sauvegardée est gardée.</param>
        /// <param name="safePoint">Sous-lot 12.8 : point sûr où le personnage entre (halte, reprise après une défaite) ; null : l'entrée de la zone.</param>
        /// <param name="door">Intérieurs : porte de la zone par où le personnage arrive (venant d'une autre zone) ; null : le point sûr. Pas
        /// les deux à la fois.</param>
        /// <param name="storyZone">Intérieurs : zone où vivent les PNJ de l'histoire, ses lieux et les nœuds et ateliers des métiers (celle
        /// contre laquelle ils ont été validés) ; null : cette zone. Ailleurs (un intérieur), le personnage n'y est pas : on n'y parle à aucun
        /// PNJ, on n'y atteint aucun lieu de l'histoire, on n'y récolte ni n'y fabrique ; le reste de l'histoire (faits, objets, journal) suit.</param>
        /// <param name="market">Sous-lot V1.1 : marchands et lots rachetables (la même instance d'une session à l'autre) ; demande l'histoire
        /// (les marchands sont ses PNJ). Null : partie sans commerce.</param>
        /// <param name="guild">Sous-lot V1.7 : la Guilde des aventuriers (rangs, contrats, épreuves) ; demande l'histoire (sa maîtresse est un
        /// PNJ). Null : partie sans guilde (une carte sauvegardée est gardée telle quelle).</param>
        public WorldSession(WorldSave save, ZoneDefinition zone, GameData data, CharacterProgression progression, ItemCatalog items, StoryBook? story = null,
            CraftingBook? crafting = null, ICombatLog? log = null, string? safePoint = null, string? door = null, string? storyZone = null, Market? market = null,
            GuildBook? guild = null)
            : this(save ?? throw new ArgumentNullException(nameof(save)),
                // Sous-lot V1.8 (zones en mélange) : la zone telle que le personnage la trouve à son niveau d'entrée (créatures, chefs),
                // calculée une seule fois (relecture de la PR #83).
                ForPlayer(zone ?? throw new ArgumentNullException(nameof(zone)), save, progression), data, progression, items, story, crafting, log, safePoint,
                door, storyZone, market, guild, scaled: true)
        {
        }

        /// <summary>Forme de la session pour une zone déjà réglée au niveau du personnage (<paramref name="zone"/>, <see cref="ForPlayer"/>).</summary>
        private WorldSession(WorldSave save, ZoneDefinition zone, GameData data, CharacterProgression progression, ItemCatalog items, StoryBook? story,
            CraftingBook? crafting, ICombatLog? log, string? safePoint, string? door, string? storyZone, Market? market, GuildBook? guild, bool scaled)
            : this(save, ZoneSetups.For(zone, save.Consumed), data, progression, items, new CombatantPlacement(EntryOf(zone, safePoint, door), CharacterRadius),
                null, log, CheckZone(save, zone, story, crafting, market, guild))
        {
            Definition = zone;
            _storyZone = storyZone ?? zone.Id;
            _populations = new ZonePopulations(zone);
            if (story != null) Narrative = new Narrative(story, _character.Story, this, guild);
            if (crafting != null) Workshop = new Workshop(crafting, _character.Professions);
            Market = market;
            // Sous-lot 12.7 : le chef arrive avec la session (sa première forme).
            // Sous-lot V1.6 : les autres chefs aussi (le chef des bandits), chacun avec sa première forme.
            foreach (Boss boss in zone.Bosses)
            {
                var fight = new BossFight(boss);
                _bossStarts.Add(fight.Start(Zone.Engine));
                _fights.Add(fight);
                if (ReferenceEquals(boss, zone.Boss)) _boss = fight;
            }
            // Sous-lot V1.1 : le point sûr où commence la session (entrée, halte, reprise, voyage) est découvert d'emblée. Après l'arrivée du
            // chef : sa première forme compte alors parmi les combattants, et la découverte lui laisse la place de sa mort dans le registre.
            if (door is null) Discover(safePoint ?? zone.Entry.Id);
            // Compétences données à l'entrée (réponse de Damien du 30 octobre 2026), après le chef comme la découverte.
            GiveAtEntry();
        }

        /// <summary>
        /// Sous-lot V1.8 (sujet 4, zones en mélange) : <paramref name="zone"/> réglée pour le niveau du personnage de la sauvegarde
        /// (<see cref="ZoneDefinition.ForLevel"/>) ; telle quelle sans personnage ni réglage. Remarque de Codex sur la PR #83 : le niveau est
        /// celui que le personnage garde à l'entrée, après la conversion d'une sauvegarde d'avant V1.8 (<see cref="RetainedXp"/>, même règle
        /// et même condition de place au registre que l'entrée ; la mise à l'échelle ne change pas le nombre de combattants).
        /// </summary>
        private static ZoneDefinition ForPlayer(ZoneDefinition zone, WorldSave save, CharacterProgression progression)
        {
            if (save.Character is not SavedCharacter c || progression is null) return zone;
            int combatants = ZoneSetups.For(zone, save.Consumed).Combatants.Count;
            long xp = RetainedXp(save, c, progression, combatants, out _);
            return zone.ForLevel(progression.LevelFor(xp));
        }

        private readonly BossFight? _boss;
        private readonly List<BossFight> _fights = new List<BossFight>();
        private readonly List<BossEvent> _bossStarts = new List<BossEvent>();
        private bool _defeated;

        /// <summary>Sous-lot V1.6 : forme en cours de chaque chef de la zone (le chef, puis les autres), pour l'affichage.</summary>
        public IReadOnlyList<string> BossForms => _fights.Select(f => f.CurrentForm).ToList();

        /// <summary>Sous-lot V1.6 : le combattant est une forme d'un chef (y compris revenue sous une nouvelle identité).</summary>
        public bool IsBossForm(string combatant) => _fights.Any(f => f.CurrentForm == combatant || f.Boss.Forms.Any(x => x.Id == combatant));

        /// <summary>Sous-lot 12.7 : forme en cours du chef (combattant), ou null sans chef.</summary>
        public string? BossForm => _boss?.CurrentForm;

        /// <summary>Sous-lot 12.7 : le chef a été vaincu dans cette session.</summary>
        public bool BossDefeated => _boss?.IsDefeated ?? false;

        /// <summary>
        /// Sous-lot 12.7 (cadrage Q8) : le personnage est tombé. La session ne lui rapporte plus rien ; la partie reprend au point sûr, PV pleins,
        /// par une nouvelle session ouverte sur <see cref="Save"/> : rien de ce qui est acquis n'est perdu (niveau, expérience, objets, métiers,
        /// étapes de quête) ; seuls les objets de soin bus manquent.
        /// </summary>
        public bool IsDefeated => _defeated;

        /// <summary>Sous-lot 12.5 : histoire du personnage (null sans histoire chargée).</summary>
        public Narrative? Narrative { get; }

        /// <summary>Parle à un PNJ (à portée, personnage présent et vivant).</summary>
        /// <exception cref="InvalidOperationException">Partie sans histoire.</exception>
        public DialogueRefusal Talk(string npc, out DialogueView? view) =>
            (Narrative ?? throw new InvalidOperationException("Partie sans histoire.")).Talk(npc, out view);

        /// <summary>Choisit une réponse du dialogue ouvert ; rend les événements de ses effets (rien en cas de refus).</summary>
        public DialogueRefusal Choose(int index, out DialogueView? next, out IReadOnlyList<StoryEvent> events)
        {
            var list = new List<StoryEvent>();
            DialogueRefusal refusal = (Narrative ?? throw new InvalidOperationException("Partie sans histoire.")).Choose(index, list, out next);
            events = list;
            return refusal;
        }

        /// <summary>
        /// Sous-lot V1.6 : choisit la récompense de rang <paramref name="index"/> d'une quête qui l'attend (<see cref="Narrative.ChooseReward"/>) ;
        /// rend les événements (rien en cas de refus).
        /// </summary>
        public DialogueRefusal ChooseReward(string quest, int index, out IReadOnlyList<StoryEvent> events)
        {
            var list = new List<StoryEvent>();
            DialogueRefusal refusal = (Narrative ?? throw new InvalidOperationException("Partie sans histoire.")).ChooseReward(quest, index, list);
            events = list;
            return refusal;
        }

        /// <summary>Sous-lot V1.6 : suit une quête en cours (son objectif sur la carte), ou plus aucune (null).</summary>
        public bool Track(string? quest) => (Narrative ?? throw new InvalidOperationException("Partie sans histoire.")).Track(quest);

        // --- Monde vu par l'histoire (INarrativeWorld) ---

        void INarrativeWorld.GiveXp(long xp) => _character = _character.WithXp(Math.Min(SaveValidator.MaxXp, checked(_character.Xp + xp)));

        DayPhase INarrativeWorld.Phase => Zone.Time.Phase;

        int INarrativeWorld.PassageRoom => Zone.RegistryRoom;

        GroundVector? INarrativeWorld.PlayerPosition
        {
            get
            {
                // Intérieurs : hors de la zone de l'histoire (un intérieur), les PNJ, lieux, nœuds et ateliers ne sont pas là.
                if (!InStoryZone) return null;
                CombatantView me = Zone.Engine.View(_character.Id);
                return me.IsPresent && !me.IsDead ? me.Position : null;
            }
        }

        /// <summary>
        /// Sous-lot V1.6 : position du personnage présent et vivant dans la zone <paramref name="zone"/> (null : la zone de l'histoire) ; les
        /// PNJ des intérieurs parlent quand on est dans leur pièce.
        /// </summary>
        GroundVector? INarrativeWorld.PlayerPositionIn(string? zone)
        {
            if (zone is null) return ((INarrativeWorld)this).PlayerPosition;
            if (Definition is null || Definition.Id != zone) return null;
            CombatantView me = Zone.Engine.View(_character.Id);
            return me.IsPresent && !me.IsDead ? me.Position : null;
        }

        int INarrativeWorld.Level => Level;

        int INarrativeWorld.LevelWith(long xp) => _progression.LevelFor(Math.Min(SaveValidator.MaxXp, checked(_character.Xp + Math.Max(0, xp))));

        bool INarrativeWorld.IsEquipped(string item) => Inventory.Equipped.Values.Contains(item);

        bool INarrativeWorld.InPlace(string place) =>
            Definition != null && ((INarrativeWorld)this).PlayerPosition is GroundVector p && Definition.PlacesAt(p).Any(x => x.Id == place);

        /// <summary>Intérieurs : zone de l'histoire et des métiers (null pour une partie sur une simulation donnée).</summary>
        private readonly string? _storyZone;

        /// <summary>
        /// Intérieurs : le personnage est dans la zone de l'histoire (celle de ses PNJ, de ses lieux, des nœuds et des ateliers) ; faux dans
        /// un intérieur. Toujours vrai pour une partie sur une simulation donnée.
        /// </summary>
        public bool InStoryZone => Definition is null || Definition.Id == _storyZone;

        /// <summary>Place d'entrée : la porte demandée, sinon le point sûr demandé, sinon l'entrée de la zone.</summary>
        private static GroundVector EntryOf(ZoneDefinition zone, string? safePoint, string? door)
        {
            if (door != null)
            {
                if (safePoint != null) throw new ArgumentException("Entrée par une porte ou par un point sûr, pas les deux.", nameof(door));
                return (zone.DoorOf(door) ?? throw new ArgumentException($"Porte « {door} » inconnue de {zone.Id}.", nameof(door))).Position;
            }
            return (safePoint is null ? zone.Entry
                : zone.SafePoints.FirstOrDefault(p => p.Id == safePoint) ?? throw new ArgumentException($"Point sûr « {safePoint} » inconnu de {zone.Id}.", nameof(safePoint))).Position;
        }

        /// <summary>
        /// Contrôles faits avant l'ouverture de la zone (aucun journal écrit pour une session refusée) : sauvegarde de cette zone, état narratif
        /// et expérience des métiers qui vont avec l'histoire et les métiers chargés (revue Codex de la PR #47).
        /// </summary>
        private static bool CheckZone(WorldSave save, ZoneDefinition zone, StoryBook? story, CraftingBook? crafting, Market? market, GuildBook? guild = null)
        {
            if (save.Zone != zone.Id) throw new ArgumentException($"Sauvegarde de la zone « {save.Zone} », pas de « {zone.Id} ».", nameof(save));
            // Sous-lot V1.7 : la maîtresse de guilde est un PNJ de l'histoire.
            if (guild != null && (story is null || !story.HasNpc(guild.Npc))) throw new ArgumentException("Guilde sans l'histoire de sa maîtresse.", nameof(guild));
            // Sous-lot V1.1 : les marchands sont des PNJ de l'histoire.
            if (market != null && (story is null || market.Book.Merchants.Any(m => !story.HasNpc(m.Npc))))
                throw new ArgumentException("Commerce sans l'histoire de ses PNJ.", nameof(market));
            if (save.Character is SavedCharacter c)
            {
                // Sous-lot 12.7 : le chef arrive avec la session sous l'identité de sa forme ; un personnage qui la porte rendrait son arrivée
                // impossible (refusé ici, avant l'ouverture de la zone).
                if (zone.Bosses.Any(x => x.Forms.Any(f => f.Id == c.Id)))
                    throw new ArgumentException($"Personnage « {c.Id} » : identité d'une forme du chef de {zone.Id}.", nameof(save));
                if (story != null) Narrative.CheckSaved(story, c.Story, guild);
                if (crafting != null) Workshop.CheckSaved(crafting, c.Professions);
            }
            CheckRepairMerchants(crafting, market);   // sous-lot V1.4
            return true;
        }

        /// <summary>Table de butin d'une créature : celle donnée à l'ouverture, sinon celle de sa population.</summary>
        private LootTable? LootFor(string combatant)
        {
            if (_lootTables.TryGetValue(combatant, out LootTable table)) return table;
            // Sous-lot 12.7 : la dernière forme du chef donne son butin.
            foreach (BossFight f in _fights)
                if (f.Boss.Loot is string bossLoot && f.IsFinalForm(combatant) && _items.TryGetLoot(bossLoot, out LootTable bossTable))
                    return bossTable;
            return _populations?.Of(combatant) is { Loot: string id } && _items.TryGetLoot(id, out LootTable found) ? found : null;
        }

        /// <summary>
        /// Sous-lot 12.4 : ouvre pour de bon un passage de la zone (raccourci du moulin) : sa clé entre au registre du monde, gardé dans la
        /// sauvegarde. L'obstacle disparaît à la session suivante. Faux s'il était déjà ouvert.
        /// </summary>
        /// <exception cref="InvalidOperationException">Partie sans zone du monde.</exception>
        /// <exception cref="ArgumentException">Passage inconnu de la zone.</exception>
        public bool OpenPassage(string passage)
        {
            ZoneDefinition zone = Definition ?? throw new InvalidOperationException("Partie sans zone du monde.");
            if (zone.Passages.All(p => p.Id != passage)) throw new ArgumentException($"Passage « {passage} » inconnu de {zone.Id}.", nameof(passage));
            return Zone.Consume(zone.PassageKey(passage));
        }

        public bool IsPassageOpen(string passage) => Definition is { } zone && Zone.IsConsumed(zone.PassageKey(passage));

        /// <summary>Vie en cours d'une population (<c>loup1</c>, puis <c>loup1_v2</c>…).</summary>
        public string CurrentCreature(string population) =>
            (_populations ?? throw new InvalidOperationException("Partie sans zone du monde.")).CurrentOf(population);

        /// <summary>Sous-lot 12.3 : inventaire et équipement du personnage (changements effectifs à la prochaine entrée).</summary>
        public Inventory Inventory { get; }

        /// <summary>Bonus d'équipement avec lequel il est entré dans la zone.</summary>
        public GearBonus EnteredGear { get; }

        /// <summary>Sous-lot V1.3 : effets de l'équipement porté à l'entrée (objets nommés, paliers d'ensemble), ceux du combat de cette session.</summary>
        public IReadOnlyList<EquipmentEffect> EnteredEffects { get; }

        /// <summary>
        /// Sous-lot V1.2 : objets mis aux places obligatoires vides à l'entrée (sauvegarde d'une version antérieure, ou de version 7 aux places
        /// obligatoires vides) ; vide sinon.
        /// </summary>
        public IReadOnlyList<string> Restored { get; }

        /// <summary>
        /// Relecture de la PR #78 : compte rendu des propriétés tirées accordées au catalogue actuel à l'entrée (valeur ramenée dans ses
        /// bornes, propriété inconnue retirée : <see cref="EdS.Data.Items.ItemCatalog.Settle"/>), dans l'ordre du sac puis des places ; vide
        /// si rien n'a changé. Les changements vont à la prochaine sauvegarde.
        /// </summary>
        public IReadOnlyList<string> Adjusted { get; }

        /// <summary>Sous-lot V1.2 : garde du bouclier avec lequel il est entré dans la zone (null : celle des règles).</summary>
        public ShieldGuard? EnteredShield => Zone.Engine.View(_character.Id).Rules?.Shield;

        /// <summary>
        /// Porte l'équipement de la place <paramref name="place"/> de l'inventaire, au niveau actuel du personnage, à la place où il va
        /// (<see cref="Inventory.PlaceFor"/>). Sous-lot V1.2 : hors combat seulement (<see cref="InCombat"/>, la règle de la halte, des talents
        /// et du voyage) ; sinon <see cref="InventoryRefusal.InCombat"/> et rien ne change.
        /// </summary>
        public InventoryRefusal Equip(int place) => InCombat ? InventoryRefusal.InCombat : Inventory.Equip(place, Level);

        /// <summary>Sous-lot V1.2 : porte l'objet de la place <paramref name="place"/> à la place d'équipement <paramref name="slot"/>, hors combat.</summary>
        public InventoryRefusal EquipTo(int place, string slot) => InCombat ? InventoryRefusal.InCombat : Inventory.EquipTo(place, slot, Level);

        /// <summary>Sous-lot V1.2 : ôte l'objet porté à <paramref name="slot"/>, hors combat (jamais d'une place obligatoire).</summary>
        public InventoryRefusal Unequip(string slot) => InCombat ? InventoryRefusal.InCombat : Inventory.Unequip(slot);

        /// <summary>Sous-lot V1.2 : échange deux places d'une même sorte (les anneaux, les reliques), hors combat.</summary>
        public InventoryRefusal Swap(string first, string second) => InCombat ? InventoryRefusal.InCombat : Inventory.Swap(first, second);

        /// <summary>Simule un tick ; rend l'expérience et le butin gagnés à ce tick (créatures vaincues).</summary>
        public SessionStep Step()
        {
            // Revue Codex de la PR #43 : un départ prend effet en fin de tick, après les morts : la présence se juge aussi au début du tick.
            bool wasPresent = Zone.Engine.View(_character.Id).IsPresent;
            IReadOnlyList<ZoneEvent> events = Zone.Step();
            SyncConsumables();
            // Sous-lot V1.8c : le temps des bonus actifs baisse d'un tick ; un bonus fini quitte sa place à ce tick précis.
            IReadOnlyList<BoonExpired> expired = TickBoons();
            // Sous-lot V1.8d : effet immédiat : un bonus fini quitte le combat au tick suivant, le premier qu'il ne couvre plus (bonus modifié
            // soumis maintenant, appliqué au début de ce tick) ; un bonus modifié encore refusé par le pilote est retenté.
            if (expired.Count > 0 || _gearPending) SyncCombatGear();
            IReadOnlyList<ExperienceGained> gains = Array.Empty<ExperienceGained>();
            IReadOnlyList<LootGained> loot = Array.Empty<LootGained>();
            if (events.Count > 0) Reward(events, wasPresent, out gains, out loot);
            // Revue Codex de la PR #45 : les populations après les récompenses (les morts du tick sont déjà attribuées) ; leurs opérations
            // refusées par le moteur sont retentées au tick suivant, sans faire échouer celui-ci.
            IReadOnlyList<PopulationEvent> population = _populations?.Step(Zone, _fights.Sum(f => f.FormsToCome), NightWanted) ?? Array.Empty<PopulationEvent>();
            var story = new List<StoryEvent>();
            CombatantView me = Zone.Engine.View(_character.Id);
            // Sous-lot 12.7 : le chef (formes, victoire, récompenses une seule fois) et la défaite.
            var boss = new List<BossEvent>();
            boss.AddRange(_bossStarts);
            _bossStarts.Clear();
            var rewards = new List<(string, int)>();
            foreach (BossFight fight in _fights)
            {
                foreach (BossEvent b in fight.Step(Zone.Engine))
                {
                    boss.Add(b);
                    // Victoire avec le personnage présent et vivant ; la première seulement est récompensée.
                    // Revue Codex de la PR #48 (4e tour) : même présence que pour l'expérience : un départ prend effet en fin de tick, après
                    // les morts ; le personnage présent au début du tick et vivant compte.
                    bool here = !me.IsDead && (me.IsPresent || (me.HasLeft && wasPresent));
                    if (b.Kind == BossEventKind.Defeated && here && !Zone.IsConsumed(Definition!.VictoryKeyOf(fight.Boss))) fight.RewardPending = true;
                }
                if (fight.RewardPending) GiveBossRewards(fight, rewards, story);
            }
            bool defeated = false;
            if (!_defeated && me.IsDead)
            {
                _defeated = true;
                defeated = true;
            }
            // Sous-lot V1.4 : dégâts reçus, et usure de l'équipement porté à la fin d'un combat (hors du moteur).
            WearReport? wear = TrackWear(defeated);
            // Sous-lot V1.1 : points sûrs approchés, découverts pour de bon (voyage rapide).
            DiscoverSafePoints(story);
            // Sous-lot 12.5 : quêtes, découvertes et confiance, après les récompenses (objectifs « vaincre »), les populations et le chef.
            Narrative?.Step(story);
            return gains.Count == 0 && loot.Count == 0 && population.Count == 0 && story.Count == 0 && boss.Count == 0 && rewards.Count == 0 && !defeated && wear is null
                && expired.Count == 0
                ? SessionStep.Empty : new SessionStep(gains, loot, population, story, boss, rewards, defeated, wear, expired);
        }

        /// <summary>
        /// Réponses de Damien du 31 octobre 2026 : une créature rare de la nuit est voulue dans la zone au tick <paramref name="tick"/> de la
        /// session quand une quête ou un contrat en cours la demande (elle vient alors à coup sûr, comme avant, après son signe), ou la nuit,
        /// si le tirage de cette nuit la fait venir et qu'elle n'y a pas encore été vaincue.
        /// </summary>
        private bool NightWanted(Population population, long tick)
        {
            if (Narrative?.Hunts(population.Creature, population.Family) == true) return true;
            if (!(NightCreatures.NightOf(Zone.Clock.At(checked(Zone.StartWorldTick + tick))) is long night)) return false;
            return !Zone.IsConsumed(NightCreatures.Key(Zone.Id, night, population.Creature)) && NightCreatures.Appears(Zone.Id, night, population);
        }

        /// <summary>
        /// Réponses du 31 octobre 2026 : une créature rare de la nuit vaincue la nuit garde la clé de cette nuit au registre du monde (elle ne
        /// revient pas avant la nuit suivante, même après une halte). Le registre garde la place des morts possibles d'un tick : sans elle, la
        /// clé n'est pas gardée (la créature pourra revenir cette nuit-là).
        /// </summary>
        internal void NightDefeated(Population population, long tick)
        {
            if (population.Night is null || !(NightCreatures.NightOf(Zone.Clock.At(checked(Zone.StartWorldTick + tick))) is long night)) return;
            if (Zone.RegistryRoom - 1 >= Zone.Engine.CombatantIds.Count) Zone.Consume(NightCreatures.Key(Zone.Id, night, population.Creature));
        }

        /// <summary>Expérience et butin des morts du tick (premières attributions), rangés dans le personnage et l'inventaire.</summary>
        private void Reward(IReadOnlyList<ZoneEvent> events, bool wasPresent, out IReadOnlyList<ExperienceGained> experience, out IReadOnlyList<LootGained> rolls)
        {
            CombatantView me = Zone.Engine.View(_character.Id);
            bool here = !me.IsDead && (me.IsPresent || (me.HasLeft && wasPresent));
            var gains = new List<ExperienceGained>();
            var loot = new List<LootGained>();
            foreach (ZoneEvent e in events)
            {
                if (e.Kind != ZoneEventKind.Death || !e.FirstTime || e.Combatant == _character.Id) continue;
                CombatantView creature = Zone.Engine.View(e.Combatant);
                if (creature.Kind != CombatantKind.Assailant || creature.MaxHealth is not long maxHealth) continue;
                if (_populations?.Of(e.Combatant) is Population rare) NightDefeated(rare, e.Tick);   // réponses du 31 octobre 2026
                // Le personnage doit être là, et vivant, quand la créature tombe (solo : il est seul à pouvoir l'avoir vaincue).
                if (!here) continue;
                int before = Level;
                // Revue Codex de la PR #43 : seule l'expérience réellement ajoutée (sous le plafond) est annoncée ; rien au plafond.
                long total = Math.Min(SaveValidator.MaxXp, checked(_character.Xp + _progression.KillXp(maxHealth)));
                long xp = total - _character.Xp;
                if (xp > 0)
                {
                    _character = _character.WithXp(total);
                    gains.Add(new ExperienceGained(e, xp, before, Level));
                }
                // Sous-lot 12.3 : butin de sa table, rangé en entier ou perdu (inventaire plein).
                // Sous-lot V1.3 : un équipement qui en tire reçoit ses propriétés, tirées par la même clé (flux propre à l'objet et à son rang
                // dans la table : relecture de la PR #78, les propriétés d'un objet ne dépendent pas des autres objets tombés).
                if (LootFor(e.Combatant) is LootTable table)
                {
                    IReadOnlyList<LootRoll> rolled = Loot.Roll(table, e.Key);
                    for (int r = 0; r < rolled.Count; r++)
                    {
                        ItemStack stack = ItemRolls.Roll(_items, rolled[r].Item, rolled[r].Count, e.Key, rolled[r].Rank);
                        loot.Add(new LootGained(e, stack.Item, stack.Count, Inventory.Add(stack) == InventoryRefusal.None, stack.Properties));
                    }
                }
                // Sous-lot 12.5 : compte pour les objectifs « vaincre » de sa population.
                Population? population = _populations?.Of(e.Combatant);
                Narrative?.Defeated(population?.Creature ?? e.Combatant, population?.Family);
            }
            experience = gains;
            rolls = loot;
        }

        /// <summary>
        /// Récompenses de la première victoire, en entier ou pas du tout : si elles ne tiennent pas, elles attendent une place (annoncé une
        /// fois) ; données, la clé de victoire entre au registre du monde et le fait du chef est appris.
        /// </summary>
        private void GiveBossRewards(BossFight fight, List<(string, int)> given, List<StoryEvent> story)
        {
            Boss b = fight.Boss;
            string key = Definition!.VictoryKeyOf(b);
            Inventory trial = Inventory.Clone();
            // Même règle que la revue Codex de la PR #46 : la clé de victoire réserve sa place au registre avant toute mutation (registre
            // plein : les récompenses attendent, comme un inventaire plein).
            // Relecture de la PR #76 : même règle que les autres clés du registre : la clé de victoire et les morts possibles d'un tick.
            bool room = Zone.RegistryRoom - 1 >= Zone.Engine.CombatantIds.Count;
            foreach ((string item, int count) in b.Rewards)
                if (!room || trial.Add(item, count) != InventoryRefusal.None)
                {
                    if (!fight.RewardAnnounced)
                    {
                        fight.RewardAnnounced = true;
                        story.Add(new StoryEvent(StoryEventKind.RewardBlocked, key));
                    }
                    return;
                }
            Inventory.CopyFrom(trial);
            Zone.Consume(key);
            given.AddRange(b.Rewards);
            if (Narrative != null) Narrative.Learn(b.Fact, story);
            else _pendingFacts.Add(b.Fact);
            fight.RewardPending = false;
        }

        /// <summary>Faits du chef appris sans histoire chargée : gardés pour la sauvegarde.</summary>
        private readonly List<string> _pendingFacts = new List<string>();

        /// <summary>Sauvegarde à ce tick : monde de la zone et personnage à jour.</summary>
        public WorldSave Save()
        {
            WorldSave world = Zone.Save();
            // Sous-lot V1.4 : les dégâts déjà reçus dans le combat en cours comptent dans l'usure sauvegardée (halte, reprise, sortie).
            SavedCharacter character = InventoryToSave().SaveInto(_character);
            if (Narrative != null) character = character.WithStory(Narrative.ToSaved());
            else if (_pendingFacts.Count > 0)
                character = character.WithStory(new SavedStory(character.Story.Facts.Concat(_pendingFacts), character.Story.Quests, character.Story.Trust,
                    character.Story.Discoveries, character.Story.Tracked, character.Story.Guild, character.Story.Hunted));
            if (Workshop != null) character = character.WithProfessions(Workshop.Professions);
            character = character.WithBoons(ActiveBoons);   // sous-lot V1.8c : bonus actifs et temps qui leur reste, tel quel
            return new WorldSave(world.Zone, world.WorldTick, world.Sessions, world.Consumed, character);
        }
    }
}
