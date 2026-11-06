using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using EdS.Data;
using EdS.Data.Characters;
using EdS.Data.Classes;
using EdS.Data.Crafting;
using EdS.Data.Guild;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Merchants;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Zones;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>Essai réaliste 7 : lieu et combat autour du personnage, pour la musique et les ambiances (voir <see cref="AdventureSession.Mood"/>).</summary>
    public readonly struct AdventureMood
    {
        public AdventureMood(bool inVillage, bool fighting, bool bossEngaged, double riverDistance, bool indoors = false, double night = 0, double rain = 0)
        {
            Night = double.IsNaN(night) ? 0 : Math.Max(0, Math.Min(1, night));
            Rain = double.IsNaN(rain) ? 0 : Math.Max(0, Math.Min(1, rain));
            InVillage = inVillage;
            Fighting = fighting;
            BossEngaged = bossEngaged;
            RiverDistance = riverDistance;
            Indoors = indoors;
        }

        public bool InVillage { get; }

        /// <summary>Intérieurs : le personnage est dans un intérieur (ni vent, ni oiseaux, ni rivière).</summary>
        public bool Indoors { get; }
        public bool Fighting { get; }
        public bool BossEngaged { get; }

        /// <summary>Distance au lit de la rivière (mètres) ; NaN sans rivière.</summary>
        public double RiverDistance { get; }

        /// <summary>Sous-lot V2.1 : part de nuit (0 le jour, 1 la nuit ; <see cref="DayCycle.NightShare"/>) : les oiseaux se taisent, la nuit chante.</summary>
        public double Night { get; }

        /// <summary>Sous-lot V2.2 : pluie (0 à 1 ; <see cref="WeatherState.Rain"/>) : la nappe de pluie la suit, les oiseaux se taisent.</summary>
        public double Rain { get; }

        /// <summary>
        /// Relecture de la PR #101 : la même ambiance avec la pluie <paramref name="rain"/> (celle qui est montrée : le temps forcé par le menu
        /// d'essai compris), pour que la nappe de pluie suive ce qu'on voit.
        /// </summary>
        public AdventureMood WithRain(double rain) => new AdventureMood(InVillage, Fighting, BossEngaged, RiverDistance, Indoors, Night, rain);
    }

    /// <summary>Un PNJ, un nœud de récolte ou un point sûr à montrer dans la zone (nom déjà traduit).</summary>
    public sealed class WorldMarker
    {
        public string Id { get; }
        public string Name { get; }
        public double X { get; }
        public double Z { get; }

        /// <summary>Nœud de récolte : métier ; null pour un PNJ ou un point sûr.</summary>
        public string? Profession { get; }

        /// <summary>Nœud de récolte prêt (repoussé) ; toujours vrai pour un PNJ ou un point sûr.</summary>
        public bool Ready { get; }

        internal WorldMarker(string id, string name, GroundVector position, string? profession = null, bool ready = true)
        {
            Id = id;
            Name = name;
            X = position.X.Raw / 10000.0;
            Z = position.Z.Raw / 10000.0;
            Profession = profession;
            Ready = ready;
        }
    }

    /// <summary>
    /// Une quête du journal : nom, étape en cours (texte), avancement (« 2/3 ») ou accomplie. Sous-lot V1.6 : identifiant, onglet (principale
    /// ou secondaire), suivie ou non, et récompenses au choix en attente (noms traduits, dans l'ordre de <see cref="AdventureSession.ChooseReward"/>).
    /// </summary>
    public sealed class JournalEntry
    {
        public string Name { get; }
        public string? Step { get; }
        public string? Progress { get; }
        public bool Done { get; }

        /// <summary>Sous-lot V1.6 : identifiant de la quête (pour <see cref="AdventureSession.Track"/> et <see cref="AdventureSession.ChooseReward"/>).</summary>
        public string Id { get; }

        /// <summary>Sous-lot V1.6 : onglet du journal.</summary>
        public QuestCategory Category { get; }

        /// <summary>Sous-lot V1.6 : la quête est suivie (son objectif sur la carte).</summary>
        public bool Tracked { get; }

        /// <summary>Sous-lot V1.6 : récompenses à choisir (« 3 × Potion de soin ») ; vide sans choix en attente.</summary>
        public IReadOnlyList<string> RewardChoices { get; }

        public bool ChoosingReward => RewardChoices.Count > 0;

        internal JournalEntry(string name, string? step, string? progress, bool done, string id = "", QuestCategory category = QuestCategory.Main,
            bool tracked = false, IReadOnlyList<string>? rewardChoices = null)
        {
            Name = name;
            Step = step;
            Progress = progress;
            Done = done;
            Id = id;
            Category = category;
            Tracked = tracked;
            RewardChoices = rewardChoices ?? Array.Empty<string>();
        }
    }

    /// <summary>Une recette telle que l'atelier la montre : nom, métier, niveau, entrées (avec ce que l'on a), produit, faisable ici.</summary>
    public sealed class RecipeEntry
    {
        public string Id { get; }
        public string Name { get; }
        public string Profession { get; }
        public int RequiredLevel { get; }
        public string Inputs { get; }
        public string Output { get; }

        /// <summary>Refus qu'aurait la fabrication maintenant (<see cref="CraftRefusal.None"/> : faisable).</summary>
        public CraftRefusal Refusal { get; }

        /// <summary>Sous-lot V1.4 : recette à qualité : elle se fait par le petit jeu de forge (<see cref="AdventureSession.Forge"/>).</summary>
        public bool Quality { get; }

        /// <summary>Sous-lot V1.4 : niveaux de métier qui manquent (0 : le niveau suffit) ; au-dessus de son niveau, la recette peut rater.</summary>
        public int LevelGap { get; }

        /// <summary>Sous-lot V1.4 : chance de rater sans adresse, et avec des frappes parfaites (pour cent ; 0 quand le niveau suffit).</summary>
        public (int Worst, int Best) Fail { get; }

        internal RecipeEntry(string id, string name, string profession, int level, string inputs, string output, CraftRefusal refusal, bool quality = false,
            int levelGap = 0, (int, int) fail = default)
        {
            Quality = quality;
            LevelGap = levelGap;
            Fail = fail;
            Id = id;
            Name = name;
            Profession = profession;
            RequiredLevel = level;
            Inputs = inputs;
            Output = output;
            Refusal = refusal;
        }
    }

    /// <summary>
    /// Sous-lot 12.8 : l'aventure en solo dans une zone du monde, pour un client temps réel (Unity). Elle charge les données du monde
    /// (zone, objets, progression, histoire, métiers, textes), reprend la sauvegarde (ou crée le personnage), ouvre une session de zone
    /// (<see cref="WorldSession"/>) et la fait jouer par le pont temps réel (<see cref="RealtimeCombat"/>, sur le moteur de la session). Elle
    /// traduit ce que la session rapporte (expérience, butin, quêtes, chef, défaite…) en messages par clés de texte (cadrage Q37), offre les
    /// actions hors combat (parler, récolter, fabriquer, s'équiper, boire) et sauvegarde : au plus toutes les <see cref="AutosaveSeconds"/>
    /// quand quelque chose a changé, à la halte et à la demande. Une halte (près d'un point sûr, sans ennemi proche) ou une reprise après
    /// une défaite ouvre une nouvelle session à ce point sûr : PV pleins, niveau, équipement et passages ouverts pris en compte. Le cœur reste
    /// seul juge : cette classe ne calcule aucune règle.
    /// </summary>
    public sealed partial class AdventureSession
    {
        public const string PlayerId = "bastion";
        public const string PlayerClass = "class.bastion";
        /// <summary>Zone de départ, celle de l'histoire et des métiers (ses PNJ, ses lieux, ses nœuds et ses ateliers).</summary>
        public const string ZoneId = "zone.vallons";

        /// <summary>Sous-lot V1.9.6 : potions de soin d'un nouveau personnage (l'étape « boire » de l'ouverture).</summary>
        public const string OpeningPotion = "item.potion_de_soin";
        public const int OpeningPotions = 2;

        /// <summary>Intérieurs : distance d'une porte pour la passer (provisoire).</summary>
        public static readonly Fixed DoorRange = Fixed.FromInt(2);

        /// <summary>Portée de choix de la cible (provisoire) : les ennemis en vue à 20 m au plus, du plus proche au plus loin.</summary>
        public static readonly Fixed TargetRange = Fixed.FromInt(20);

        /// <summary>Distance d'un point sûr pour y faire halte (provisoire).</summary>
        public static readonly Fixed RestRange = Fixed.FromInt(5);

        /// <summary>Pas de halte avec un ennemi vivant à moins de cette distance (provisoire).</summary>
        public static readonly Fixed RestSafety = Fixed.FromInt(12);

        /// <summary>Écart minimal entre deux sauvegardes automatiques, en secondes de temps réel.</summary>
        public const double AutosaveSeconds = 5;

        /// <summary>Messages gardés au plus en attente d'affichage.</summary>
        public const int MaxMessages = 32;

        private readonly GameData _data;
        private readonly CharacterProgression _progression;
        private readonly ItemCatalog _items;
        /// <summary>Intérieurs : zone où se trouve le personnage (la zone de départ, ou un intérieur).</summary>
        private ZoneDefinition _zone;

        /// <summary>Intérieurs : zones reliées par des portes, la zone de départ (<see cref="ZoneId"/>) en premier.</summary>
        private readonly IReadOnlyList<ZoneDefinition> _zones;
        private readonly StoryBook _story;
        private readonly CraftingBook _crafting;
        private readonly List<string> _messages = new List<string>();
        private readonly Dictionary<string, int> _drunk = new Dictionary<string, int>(StringComparer.Ordinal);
        private bool _dirty;
        private bool _strandedAnnounced;
        private double _sinceSave;
        private string _lastSafePoint;

        public TextCatalog Texts { get; }
        public string SavePath { get; }
        public WorldSession World { get; private set; } = null!;
        public RealtimeCombat Combat { get; private set; } = null!;

        /// <summary>Nombre de sessions ouvertes par cette aventure (une de plus à chaque halte ou reprise).</summary>
        public int Opened { get; private set; }

        /// <summary>La sauvegarde trouvée était illisible (ainsi que sa copie) : nouvelle partie, motif dans <see cref="SaveProblem"/>.</summary>
        public string? SaveProblem { get; }

        private AdventureSession(GameData data, CharacterProgression progression, ItemCatalog items, IReadOnlyList<ZoneDefinition> zones, TextCatalog texts,
            StoryBook story, CraftingBook crafting, MerchantBook merchants, GuildBook guild, string savePath)
        {
            _market = new Market(merchants);   // sous-lot V1.1 : une seule instance, d'une session à l'autre (lots rachetables)
            _guild = guild;   // sous-lot V1.7
            _data = data;
            _progression = progression;
            _items = items;
            _zones = zones;
            ZoneDefinition start = zones[0];
            Texts = texts;
            _story = story;
            _crafting = crafting;
            SavePath = savePath;
            SaveReadResult read = SaveStore.Read(savePath);
            SaveProblem = read.Problem;
            // Sous-lot V1.2 : un nouveau personnage porte son équipement de départ (l'Éclat du Seuil ; pour le Bastion, l'épée et l'écu).
            // Sous-lot V1.9.6 : il commence l'ouverture (le réveil, tutoriel), avec deux potions de soin pour son étape « boire » (emportées dès
            // la première session) ; une partie déjà commencée n'y est jamais renvoyée. Relecture indépendante de la PR #94 : seulement pour
            // une vraie nouvelle partie (aucune sauvegarde) ; une sauvegarde valide sans personnage (monde déjà joué) reçoit un personnage
            // neuf sans l'ouverture ni les potions.
            SavedCharacter Fresh(bool opening)
            {
                SavedCharacter c = PlayerCharacters.Create(PlayerId, PlayerClass, progression);
                if (opening && story.HasQuest(StoryConversions.Opening))
                    c = c.WithStory(StoryConversions.NewGame(story))
                        .WithItems(new[] { new ItemStack(OpeningPotion, OpeningPotions) }, new Dictionary<string, string>(StringComparer.Ordinal));
                return Inventory.Outfit(c, items);
            }
            WorldSave save = read.Save ?? new WorldSave(start.Id, 0, 0, Array.Empty<string>(), Fresh(true));
            if (save.Character is null) save = new WorldSave(save.Zone, save.WorldTick, save.Sessions, save.Consumed, Fresh(false));
            // Intérieurs : la partie reprend dans la zone de la sauvegarde (un intérieur : à son entrée) ; une zone que les données ne relient
            // plus reprend à l'entrée de la zone de départ.
            _zone = zones.FirstOrDefault(z => z.Id == save.Zone) ?? start;
            if (_zone.Id != save.Zone) save = new WorldSave(_zone.Id, save.WorldTick, save.Sessions, save.Consumed, save.Character, save.Migrated);
            _lastSafePoint = _zone.Entry.Id;
            Open(save, null);
            Say(read.Save is null ? "text.ui.new_game" : read.FromBackup ? "text.ui.loaded_backup" : "text.ui.loaded");
            // Sous-lot V1.9.6 : une nouvelle partie dit d'emblée la première étape de l'ouverture (le réveil).
            if (read.Save is null && World.Narrative?.CurrentStep(StoryConversions.Opening) is QuestStep first) Say("text.ui.quest_step", Text(first.Text));
            AnnounceLearned();
        }

        /// <summary>
        /// Charge le monde de <paramref name="dataDirectory"/> (<c>data/v0</c>) et <paramref name="scenariosDirectory"/>, et reprend la
        /// sauvegarde <paramref name="savePath"/> (absente ou illisible : nouvelle partie, l'illisible est signalée et remplacée à la
        /// première sauvegarde). Toute donnée refusée lève <see cref="InvalidDataException"/> avec les messages du validateur.
        /// </summary>
        public static AdventureSession Load(string dataDirectory, string scenariosDirectory, string savePath)
        {
            if (savePath is null) throw new ArgumentNullException(nameof(savePath));
            GameDataLoadResult loaded = GameDataLoader.Load(dataDirectory);
            GameData data = loaded.Data ?? throw new InvalidDataException("Données refusées : " + string.Join(" ; ", loaded.Failures.Select(f => $"{f.Path} : {f.Report}")));
            CharacterProgression progression = Need(ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(dataDirectory), data).Progression, "progression",
                () => ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(dataDirectory), data).Report);
            ItemCatalogLoadResult items = ItemJsonReader.ReadFile(ItemJsonReader.PathIn(dataDirectory), data);
            ItemCatalog catalog = Need(items.Catalog, "objets", () => items.Report);
            // Intérieurs : la zone de départ et toutes celles où mènent ses portes, de proche en proche ; les portes se répondent.
            WorldZonesLoadResult zones = ZoneJsonReader.ReadLinked(dataDirectory, ZoneId, scenariosDirectory, data, catalog);
            if (!zones.IsValid) throw new InvalidDataException($"Données du monde refusées (zone {zones.Zone}) : {zones.Report}");
            ZoneDefinition definition = zones.Zones[0];
            TextsLoadResult texts = StoryJsonReader.ReadTextsFile(StoryJsonReader.TextsPathIn(dataDirectory));
            TextCatalog catalogTexts = Need(texts.Texts, "textes", () => texts.Report);
            // Sous-lot V1.6 : les PNJ des intérieurs vivent dans les zones reliées par des portes.
            StoryLoadResult story = StoryJsonReader.ReadStoryFile(StoryJsonReader.StoryPathIn(dataDirectory), catalogTexts, definition, catalog,
                zones.Zones.Skip(1).ToList(), progression.LevelCap);
            CraftingLoadResult crafting = CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(dataDirectory), catalogTexts, definition, catalog);
            StoryBook storyBook = Need(story.Story, "histoire", () => story.Report);
            // Sous-lot V1.1 : marchands et valeur des objets.
            MerchantsLoadResult merchants = MerchantJsonReader.ReadFile(MerchantJsonReader.PathIn(dataDirectory), catalogTexts, storyBook, catalog);
            MerchantBook merchantBook = Need(merchants.Book, "marchands", () => merchants.Report);
            // Sous-lot V1.7 : la Guilde des aventuriers (rangs, contrats, épreuves, portes, boutique).
            GuildLoadResult guild = GuildJsonReader.ReadFile(GuildJsonReader.PathIn(dataDirectory), catalogTexts, storyBook, definition, zones.Zones, catalog, merchantBook);
            return new AdventureSession(data, progression, catalog, zones.Zones, catalogTexts, storyBook, Need(crafting.Book, "métiers", () => crafting.Report),
                merchantBook, Need(guild.Book, "guilde", () => guild.Report), savePath);
        }

        private static T Need<T>(T? value, string what, Func<object> report) where T : class =>
            value ?? throw new InvalidDataException($"Données du monde refusées ({what}) : {report()}");

        /// <summary>
        /// Ouvre une session de la zone en cours sur <paramref name="save"/>, au point sûr <paramref name="safePoint"/> (null : l'entrée), ou à la
        /// porte <paramref name="door"/> (intérieurs). L'histoire et les métiers restent ceux de la zone de départ.
        /// </summary>
        private void Open(WorldSave save, string? safePoint, string? door = null) => Commit(Build(save, _zone, safePoint, door));

        /// <summary>Session de <paramref name="zone"/> prête à jouer, sans rien changer à l'aventure (<see cref="Commit"/> la met en jeu).</summary>
        private WorldSession Build(WorldSave save, ZoneDefinition zone, string? safePoint, string? door) =>
            new WorldSession(save, zone, _data, _progression, _items, _story, _crafting, null, safePoint, door, ZoneId, _market, _guild);

        private void Commit(WorldSession world)
        {
            World = world;
            // Relecture de la PR #72 : toute nouvelle session (porte, voyage, halte, reprise) ferme la boutique de l'ancienne.
            Shop = null;
            GuildOpen = false;   // sous-lot V1.7 : et le tableau de la guilde
            Combat = new RealtimeCombat(world.Zone.Setup, world.Zone.Engine, PlayerId, () => Collect(world, world.Step()), TargetRange);
            _drunk.Clear();
            _strandedAnnounced = false;
            foreach (KeyValuePair<string, int> c in world.Zone.Engine.View(PlayerId).ConsumableCharges) _drunk[c.Key] = c.Value;
            Opened = checked(Opened + 1);
            if (world.Learned.Count > 0) _dirty = true;   // sous-lot 12.9b : les compétences gagnées vont à la prochaine sauvegarde
            if (world.Restored.Count > 0) _dirty = true;   // sous-lot V1.2 : places obligatoires remplies, à garder
            if (world.Adjusted.Count > 0) _dirty = true;   // relecture de la PR #78 : propriétés accordées au catalogue, à garder
            // Remarque de Codex sur la PR #82 : expérience convertie, barre ou talents réparés : à garder.
            if (world.XpMigrated || world.RemovedSkills.Count > 0 || world.RefundedTalentPoints > 0) _dirty = true;
        }

        /// <summary>Sous-lot 12.9b : annonce les compétences gagnées à l'ouverture de la session, avec leur touche.</summary>
        private void AnnounceLearned()
        {
            // Sous-lot V1.2 : places obligatoires remplies à la reprise d'une ancienne sauvegarde (Éclat du Seuil, bouclier du Bastion).
            if (World.Restored.Count > 0) Say("text.ui.inventory.restored", string.Join(", ", World.Restored.Select(ItemName)));
            // Relecture de la PR #78 : propriétés tirées accordées au catalogue actuel (bornes réglées, propriété retirée).
            if (World.Adjusted.Count > 0) Say("text.ui.inventory.adjusted", string.Join(" ", World.Adjusted));
            // Remarque de Codex sur la PR #82 : barre ou talents devenus invalides, réparés au chargement.
            if (World.RemovedSkills.Count > 0) Say("text.ui.skills.repaired", string.Join(", ", World.RemovedSkills.Select(SkillName)));
            if (World.RefundedTalentPoints > 0) Say("text.ui.talents.refunded", World.RefundedTalentPoints.ToString(System.Globalization.CultureInfo.InvariantCulture));
            foreach (SkillUnlock u in World.Learned)
                if (u.Kind == SkillKind.Ultimate) Say("text.ui.ultimate_learned", SkillName(u.Skill));
                else if (u.Kind == SkillKind.Active)
                    Say("text.ui.skill_learned", SkillName(u.Skill), World.Character.ActiveSkills.ToList().IndexOf(u.Skill) + 1);
                else Say("text.ui.passive_learned", SkillName(u.Skill));
        }

        private string SkillName(string skill) =>
            _data.TryGetClass(PlayerClass, out LoadedClass c) && c.Definition.TryGetSkill(skill, out SkillDefinition d) ? d.Name : skill;

        // --- Temps et messages -------------------------------------------------------------------------------------------

        /// <summary>
        /// Avance de <paramref name="seconds"/> secondes de temps réel (le pont simule les ticks échus) ; sauvegarde si quelque chose a changé
        /// et que la dernière sauvegarde date d'au moins <see cref="AutosaveSeconds"/>. Rend le nombre de ticks simulés.
        /// </summary>
        public int Advance(double seconds)
        {
            // Remarque de Codex sur la PR #84 : le temps des bonus actifs baisse à chaque tick simulé sans autre événement ; il est gardé à la
            // cadence de la sauvegarde automatique (pas à chaque tick), pour qu'une reprise après un arrêt brutal ne rende pas un temps ancien.
            bool boons = !MustContinue && (World.BoonAt(BoonKind.Meal) != null || World.BoonAt(BoonKind.Elixir) != null || World.BoonAt(BoonKind.Weapon) != null);
            int steps = MustContinue ? 0 : Combat.Advance(seconds);
            if (boons && steps > 0) _dirty = true;
            _sinceSave += seconds;
            // Revue Codex de la PR #49 : la défaite ne perd rien (cadrage Q8) : les acquis d'avant la chute se sauvegardent encore
            // (World.Save garde l'expérience, les objets, l'histoire ; jamais les PV du combat).
            if (_dirty && _sinceSave >= AutosaveSeconds)
            {
                // Un disque plein ou inaccessible ne coupe pas la partie : le motif s'affiche et l'essai reprend 5 s plus tard.
                try
                {
                    Save();
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
                {
                    _sinceSave = 0;
                    Say("text.ui.save_failed", e.Message);
                }
            }
            TrackSafePoint();
            return steps;
        }

        /// <summary>Messages apparus depuis le dernier appel (déjà traduits), dans l'ordre ; la file est vidée.</summary>
        public IReadOnlyList<string> DrainMessages()
        {
            string[] drained = _messages.ToArray();
            _messages.Clear();
            return drained;
        }

        /// <summary>
        /// Essai réaliste 7 : quêtes accomplies annoncées depuis l'ouverture de l'aventure (la petite fanfare de l'interface suit ce
        /// compteur). Affichage seulement.
        /// </summary>
        public int QuestsAnnounced { get; private set; }

        /// <summary>Essai réaliste 7 : butins et objets reçus annoncés depuis l'ouverture de l'aventure (le bruit des pièces). Affichage seulement.</summary>
        public int LootAnnounced { get; private set; }

        private void Say(string key, params object[] args)
        {
            if (key == "text.ui.quest_done") QuestsAnnounced++;
            if (key == "text.ui.loot" || key == "text.ui.item_given" || CoinKeys.Contains(key)) LootAnnounced++;   // sous-lot V1.1 : les pièces aussi
            string text = Texts.Get(key);
            if (args.Length > 0) text = string.Format(CultureInfo.InvariantCulture, text, args);
            if (_messages.Count >= MaxMessages) _messages.RemoveAt(0);
            _messages.Add(text);
        }

        /// <summary>Texte d'une clé, sinon la clé elle-même (texte manquant visible).</summary>
        public string Text(string key) => Texts.Get(key);

        /// <summary>
        /// Sous-lot V1.9.9 : clé du message d'une nouvelle forme du chef <paramref name="boss"/> : la sienne (« text.ui.boss_phase.{chef} »,
        /// par exemple les Voix de la Rupture qui sortent de l'écho), sinon le message commun (« text.ui.boss_phase », « {0} se relève, plus redoutable encore ! », {0} : le nom du chef).
        /// </summary>
        public string BossPhaseText(string boss)
        {
            string own = "text.ui.boss_phase." + boss;
            return Texts.Has(own) ? own : "text.ui.boss_phase";
        }

        private string ItemName(string item) => _items.Items.FirstOrDefault(i => i.Id == item)?.Name ?? item;

        /// <summary>Traduit ce qu'un tick de la session rapporte.</summary>
        private void Collect(WorldSession world, SessionStep step)
        {
            if (!ReferenceEquals(world, World)) return;
            DrinkMessages();
            if (Stranded && !_strandedAnnounced)
            {
                _strandedAnnounced = true;
                Say("text.ui.stranded");
            }
            if (step.IsEmpty) return;
            _dirty = true;
            foreach (ExperienceGained g in step.Experience)
            {
                Say("text.ui.xp", g.Xp);
                if (!g.LeveledUp) continue;
                Say("text.ui.level", g.LevelAfter);
                // Sous-lot 12.9b : ce que ce niveau fera gagner à la prochaine halte. Réponse de Damien du 30 octobre 2026 : une compétence
                // sans place à ce niveau est apprise tout de suite, dans la réserve (le joueur la place avec l'écran des compétences).
                foreach (SkillUnlock u in _progression.ForClass(PlayerClass).SkillUnlocks)
                    if (u.Level > g.LevelBefore && u.Level <= g.LevelAfter) Say(u.WithPlace ? "text.ui.skill_soon" : "text.ui.skill_reserve", SkillName(u.Skill));
            }
            foreach (LootGained l in step.Loot) Say(l.Kept ? "text.ui.loot" : "text.ui.loot_lost", ItemName(l.Item), l.Count);
            foreach (BossEvent b in step.Boss)
            {
                if (b.Kind == BossEventKind.FormArrived && b.Phase > 0) Say(BossPhaseText(b.Boss), DisplayNames.Of(b.Boss));   // le message commun nomme le chef
                else if (b.Kind == BossEventKind.Defeated) Say("text.ui.boss_defeated");
            }
            foreach ((string item, int count) in step.Rewards) Say("text.ui.reward", ItemName(item), count);
            // Réponses de Damien du 31 octobre 2026 : le signe d'une créature rare de la nuit (« Un hurlement glacé monte du pré… »).
            foreach (PopulationEvent p in step.Population)
                if (p.Kind == PopulationEventKind.Sign) Say(p.Creature);
            foreach (StoryEvent e in step.Story) Story(e);
            if (step.Defeated) Say("text.ui.defeated");
            // Sous-lot V1.4 : une pièce usée à zéro par ce combat ne donne plus ses bonus (à réparer).
            if (step.Wear is WearReport wear && wear.WornOut.Count > 0) Say("text.ui.worn_out", string.Join(", ", wear.WornOut.Select(ItemName)));
            // Sous-lot V1.8c : un bonus fini quitte sa place (sauvegardé : la place libre est gardée).
            foreach (BoonExpired b in step.BoonsExpired)
            {
                _dirty = true;
                Say("text.ui.boon.expired", ItemName(b.Item));
            }
        }

        private void Story(StoryEvent e)
        {
            switch (e.Kind)
            {
                case StoryEventKind.QuestStarted:
                    Say("text.ui.quest_started", Text(_story.Quest(e.Id).Name));
                    break;
                case StoryEventKind.StepCompleted:
                    if (World.Narrative?.CurrentStep(e.Id) is QuestStep next) Say("text.ui.quest_step", Text(next.Text));
                    break;
                case StoryEventKind.QuestCompleted:
                    Say("text.ui.quest_done", Text(_story.Quest(e.Id).Name));
                    break;
                case StoryEventKind.QuestSkipped:   // sous-lot V1.9.7
                    Say("text.ui.quest_skipped", Text(_story.Quest(e.Id).Name));
                    break;
                case StoryEventKind.Discovered:
                {
                    // Revue Codex de la PR #94 : une découverte dont le texte est le repli d'une scène à jouer (le souvenir de la Rupture) ne
                    // s'affiche pas à l'annonce : la scène est proposée, et ce texte ne vient qu'en la passant. Elle reste au journal.
                    string text = _story.Discoveries.First(d => d.Id == e.Id).Text;
                    if (World.PendingScene is Scene pending && pending.Text == text) break;
                    Say("text.ui.discovered", Text(text));
                    break;
                }
                case StoryEventKind.TrustChanged:
                    Say(e.Value > 0 ? "text.ui.trust_up" : "text.ui.trust_down", Text(_story.Npc(e.Id).Name));
                    break;
                case StoryEventKind.PassageOpened:
                    Say("text.ui.passage");
                    break;
                case StoryEventKind.ItemGiven:
                    Say("text.ui.item_given", ItemName(e.Id), e.Value);
                    break;
                case StoryEventKind.ItemTaken:
                    Say("text.ui.item_taken", ItemName(e.Id), e.Value);
                    break;
                case StoryEventKind.XpGiven:
                    Say("text.ui.xp", e.Value);
                    break;
                case StoryEventKind.RewardBlocked:
                    Say("text.ui.reward_blocked");
                    break;
                case StoryEventKind.PurseFull:   // relecture de la PR #72 : la bourse, pas le sac
                    Say("text.ui.reward_purse_full");
                    break;
                case StoryEventKind.MoneyGiven:   // sous-lot V1.1
                    Say("text.ui.money_given", MoneyText(e.Value));
                    break;
                // Sous-lot V1.6.
                case StoryEventKind.ObjectiveDone:   // relecture de la PR #76 : parler à ou livrer à un PNJ
                    if (_story.Quest(e.Id).Steps.FirstOrDefault(st => st.Id == e.Step) is QuestStep done) Say("text.ui.objective_done", Text(done.Text));
                    break;
                case StoryEventKind.MoneyTaken:
                    Say("text.ui.money_taken", MoneyText(e.Value));
                    break;
                case StoryEventKind.MoneyShort:
                    Say("text.ui.reward_money_short");
                    break;
                case StoryEventKind.QuestLocked:
                    Say("text.ui.reward_quest_locked");
                    break;
                case StoryEventKind.RewardToChoose:
                    Say("text.ui.reward_to_choose", Text(_story.Quest(e.Id).Name));
                    break;
                // Sous-lot V1.7 : la Guilde des aventuriers.
                case StoryEventKind.GuildJoined:
                case StoryEventKind.GuildPoints:
                case StoryEventKind.GuildPromoted:
                case StoryEventKind.ContractTaken:
                case StoryEventKind.ContractAbandoned:
                case StoryEventKind.ContractDone:
                case StoryEventKind.TitleShown:
                case StoryEventKind.ContractExpired:   // réponses du 31 octobre 2026
                case StoryEventKind.GuildReputation:
                    GuildMessage(e);
                    break;
                case StoryEventKind.SafePointDiscovered:   // sous-lot V1.1 : le voyage rapide y mène désormais
                    Say("text.ui.safe_point_found", _zone.SafePoints.FirstOrDefault(sp => sp.Id == e.Id)?.Name ?? e.Id);
                    break;
            }
        }

        /// <summary>Objets de soin bus depuis le dernier tick (charges du moteur).</summary>
        private void DrinkMessages()
        {
            if (_drunk.Count == 0) return;
            IReadOnlyDictionary<string, int> now = World.Zone.Engine.View(PlayerId).ConsumableCharges;
            foreach (string id in _drunk.Keys.ToArray())
            {
                int left = now.TryGetValue(id, out int n) ? n : _drunk[id];
                if (left < _drunk[id])
                {
                    Say("text.ui.drunk", ItemName(id), _items.Item(id).Heal);
                    _dirty = true;
                }
                _drunk[id] = left;
            }
        }

        // --- Actions hors combat ---------------------------------------------------------------------------------------

        private GroundVector? Me
        {
            get
            {
                CombatantView me = World.Zone.Engine.View(PlayerId);
                return me.IsPresent && !me.IsDead ? me.Position : null;
            }
        }

        private static long Distance2(GroundVector a, GroundVector b)
        {
            long dx = checked(b.X.Raw - a.X.Raw), dz = checked(b.Z.Raw - a.Z.Raw);
            return checked(dx * dx + dz * dz);
        }

        private static bool Within(GroundVector a, GroundVector b, Fixed range) => Distance2(a, b) <= checked(range.Raw * range.Raw);

        /// <summary>
        /// PNJ à qui parler (le plus proche à portée), ou null. Sous-lot V1.6 : ceux de la zone où l'on est (dehors, ou dans un intérieur :
        /// l'aubergiste, le prêtre, l'habitant).
        /// </summary>
        public Npc? NpcInReach => Me is GroundVector p
            ? HereNpcs.Where(n => Within(p, World.NpcPosition(n), Narrative.TalkRange)).OrderBy(n => Distance2(p, World.NpcPosition(n))).FirstOrDefault() : null;

        /// <summary>Sous-lot V1.6 : PNJ de la zone en cours (ceux de l'histoire dehors, ceux de la pièce dans un intérieur).</summary>
        private IEnumerable<Npc> HereNpcs => _story.NpcsIn(World.InStoryZone ? null : _zone.Id);

        /// <summary>Nœud de récolte prêt le plus proche à portée, ou null.</summary>
        public GatheringNode? NodeInReach => Me is GroundVector p && World.InStoryZone
            ? _crafting.Nodes.Where(n => Within(p, n.Position, Workshop.GatherRange) && World.Workshop!.IsReady(n.Id, World.Zone.Engine.CurrentTick))
                .OrderBy(n => Distance2(p, n.Position)).FirstOrDefault() : null;

        /// <summary>
        /// Interagit avec ce qui est à portée : parle au PNJ le plus proche, sinon récolte le nœud le plus proche. Rend faux s'il n'y a
        /// rien à portée (un message le dit).
        /// </summary>
        public bool Interact()
        {
            if (World.IsDefeated) return false;
            if (NpcInReach is Npc npc)
            {
                // Sous-lot V1.1 : parler à un marchand ouvre sa boutique (même touche que les autres PNJ). Relecture de la PR #72 : boutique
                // fermée (heure de fermeture), le marchand le dit et reste un PNJ comme les autres (son dialogue de l'histoire).
                if (World.MerchantOf(npc.Id) is Merchant merchant)
                {
                    TradeRefusal shop = World.CanTrade(merchant.Id);
                    if (shop == TradeRefusal.None) return OpenShop(merchant.Id);
                    if (shop == TradeRefusal.Closed) Say("text.ui.trade.closed");
                    if (shop == TradeRefusal.RankTooLow) Say("text.ui.trade.ranktoolow");   // sous-lot V1.7 : la réserve de la guilde
                }
                // Sous-lot V1.7 : parler à la maîtresse de guilde ouvre son tableau (inscription, contrats, épreuve).
                if (IsGuildmistress(npc.Id)) return OpenGuild();
                DialogueRefusal refusal = World.Talk(npc.Id, out _);
                // Relecture de la PR #76 : un PNJ sans rien à dire, mais à qui l'on vient de parler ou de livrer pour une quête : l'objectif
                // rempli s'annonce (au tick suivant), pas « rien à dire ».
                if (refusal == DialogueRefusal.NothingToSay && World.Narrative?.LastTalkFilled > 0) return true;
                if (refusal == DialogueRefusal.NothingToSay) Say("text.ui.nothing_to_say", Text(npc.Name));
                return refusal == DialogueRefusal.None;
            }
            if (NodeInReach is GatheringNode node)
            {
                CraftRefusal refusal = World.Gather(node.Id, out CraftEvent? done);
                if (done != null) Crafted(done);
                else Refused(refusal);
                return refusal == CraftRefusal.None;
            }
            if (DoorInReach != null) return UseDoor();
            // Revue Codex de la PR #99 : la nuit, à l'étal d'un marchand fermé qui est rentré devant sa porte (hors de portée), la boutique dit
            // qu'elle est fermée et où il est, au lieu de « rien ici ».
            if (ClosedStallInReach is Npc keeper)
            {
                Say("text.ui.trade.closed_home", Text(keeper.Name));
                return false;
            }
            // Sous-lot V2.3 : de jour, à l'étal d'un marchand parti ailleurs (son emploi du temps : la taverne, le repas…), la boutique dit où
            // il est (on lui achète là-bas).
            if (AwayStallInReach is Npc away && World.NpcActivity(away) is string activity)
            {
                Say("text.ui.trade.away", Text(away.Name), Text(ActivityText(activity)));
                return false;
            }
            Say("text.ui.nothing_here");
            return false;
        }

        /// <summary>
        /// Sous-lot V2.1 (revue Codex de la PR #99) : marchand dont l'étal (sa place de l'histoire) est à portée alors que lui n'y est pas (la
        /// nuit, devant sa porte) et que sa boutique est fermée à cette heure ; null sinon.
        /// </summary>
        public Npc? ClosedStallInReach => Me is GroundVector p
            ? HereNpcs.Where(n => Within(p, n.Position, Narrative.TalkRange) && !Within(p, World.NpcPosition(n), Narrative.TalkRange)
                    && World.MerchantOf(n.Id) is Merchant m && World.CanTrade(m.Id) == TradeRefusal.Closed)
                .OrderBy(n => Distance2(p, n.Position)).FirstOrDefault()
            : null;

        /// <summary>
        /// Sous-lot V2.3 : marchand dont l'étal est à portée, ouvert, alors que lui est ailleurs d'après son emploi du temps (la boutique est
        /// auprès de lui) ; null sinon.
        /// </summary>
        public Npc? AwayStallInReach => Me is GroundVector p
            ? HereNpcs.Where(n => Within(p, n.Position, Narrative.TalkRange) && !Within(p, World.NpcPosition(n), Narrative.TalkRange)
                    && World.NpcActivity(n) != null && World.MerchantOf(n.Id) is Merchant m && World.CanTrade(m.Id) == TradeRefusal.TooFar)
                .OrderBy(n => Distance2(p, n.Position)).FirstOrDefault()
            : null;

        /// <summary>Sous-lot V2.3 : clé du texte d'une activité de l'emploi du temps (« au fournil », « à la taverne »…).</summary>
        public static string ActivityText(string activity) => StoryValidator.ActivityTextPrefix + activity;

        /// <summary>
        /// Sous-lot V2.3 : ce que fait le PNJ <paramref name="npc"/> à cette heure, en mots (« au fournil ») ; null s'il n'a pas d'emploi du
        /// temps. Pour l'étiquette au-dessus de lui.
        /// </summary>
        public string? ActivityOf(string npc) =>
            World.Narrative is { } story && story.Book.HasNpc(npc) && World.NpcActivity(story.Book.Npc(npc)) is string activity ? Text(ActivityText(activity)) : null;

        // --- Portes (intérieurs) ----------------------------------------------------------------------------------------

        /// <summary>
        /// Intérieurs (choix de Damien du 28 octobre 2026 : « on fait la façon 1 ») : porte de la zone à <see cref="DoorRange"/> du personnage
        /// vivant (la plus proche), ou null.
        /// </summary>
        public Door? DoorInReach => Me is GroundVector p && !World.IsDefeated
            ? VisibleDoors.Where(d => Within(p, d.Position, DoorRange)).OrderBy(d => Distance2(p, d.Position)).ThenBy(d => d.Id, StringComparer.Ordinal).FirstOrDefault()
            : null;

        /// <summary>
        /// La porte à portée se passe : personnage vivant, aucun ennemi vivant à moins de <see cref="RestSafety"/> (on ne fuit pas un combat
        /// par une porte, même règle que la halte), aucun dialogue ouvert.
        /// </summary>
        public bool CanUseDoor => DoorInReach != null && Dialogue is null && !MustContinue
            && !CombatDisplay.NearbyEnemies(World.Zone.Engine, PlayerId, null, RestSafety).Any();

        /// <summary>
        /// Passe la porte à portée : la partie est sauvegardée dans la zone où elle mène, puis une session de cette zone s'ouvre, le personnage
        /// arrivant à la porte qui ramène ici (PV pleins ; niveau, équipement et talents pris en compte, comme à une halte). Rend faux, sans
        /// rien changer, si la porte ne se passe pas (<see cref="CanUseDoor"/> ; un message dit pourquoi) ou si la sauvegarde échoue (disque
        /// plein : le motif s'affiche).
        /// </summary>
        public bool UseDoor()
        {
            if (!(DoorInReach is Door door)) return false;
            if (!CanUseDoor)
            {
                Say("text.ui.door_blocked");
                return false;
            }
            // Sous-lot V1.7 : une porte gardée par un rang de la guilde (les archives de la guilde au rang F).
            if (World.DoorNeedsRank(door.Id) is string rank)
            {
                Say("text.ui.guild.door_rank", RankName(rank));
                return false;
            }
            // Sous-lot V1.9.6 : la sortie d'une scène la quitte (texte du retour si elle est finie).
            if (InScene) return LeaveScene();
            return Pass(door);
        }

        /// <summary>Portes qui se montrent et se passent à pied : toutes, sauf celles qui mènent à une scène (sous-lot V1.9.6).</summary>
        private IEnumerable<Door> VisibleDoors => _zone.Doors.Where(d => _story.SceneIn(d.To) is null);

        /// <summary>
        /// Passe <paramref name="door"/> (sans contrôle de portée) : la partie est sauvegardée dans la zone où elle mène, puis une session de
        /// cette zone s'ouvre à la porte qui ramène ici. Faux, sans rien changer, si la sauvegarde échoue.
        /// </summary>
        private bool Pass(Door door)
        {
            string from = _zone.Id;
            ZoneDefinition target = _zones.First(z => z.Id == door.To);
            WorldSave here = World.Save();
            var moved = new WorldSave(target.Id, here.WorldTick, here.Sessions, here.Consumed, here.Character);
            Door arrival = target.DoorOf(door.ToDoor)!;
            // Relecture indépendante : la session d'arrivée est construite avant d'écrire quoi que ce soit ; puis la sauvegarde ; l'aventure ne
            // change qu'une fois les deux réussies.
            WorldSession next = Build(moved, target, null, arrival.Id);
            try
            {
                Write(moved);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
            {
                Say("text.ui.save_failed", e.Message);
                return false;
            }
            _zone = target;
            // La reprise après une défaite ou une fin de session se fait au point sûr de la zone le plus proche de la porte d'arrivée.
            _lastSafePoint = target.SafePoints.OrderBy(sp => Distance2(sp.Position, arrival.Position)).ThenBy(sp => sp.Id, StringComparer.Ordinal).First().Id;
            Commit(next);
            // Sous-lot V1.9.6 : entrer dans une scène ou en sortir ne s'annonce pas comme une porte.
            if (_story.SceneIn(target.Id) is null && _story.SceneIn(from) is null) Say("text.ui.door_entered", target.Name);
            AnnounceLearned();
            return true;
        }

        // --- Scènes jouées (sous-lot V1.9.6) ------------------------------------------------------------------------------

        /// <summary>
        /// Scène à jouer (le souvenir de la Rupture, après la meute), quand on est dehors ; null sinon. L'interface la propose : la jouer
        /// (<see cref="EnterScene"/>) ou la passer (<see cref="SkipScene"/> : le texte de repli s'affiche).
        /// </summary>
        public Scene? PendingScene => World.InStoryZone ? World.PendingScene : null;

        /// <summary>Scène qu'on joue en ce moment (la zone en cours est la sienne), ou null.</summary>
        public Scene? CurrentScene => _story.SceneIn(_zone.Id);

        public bool InScene => CurrentScene != null;

        /// <summary>Nom à l'écran du personnage pendant la scène (« Galeran »), ou null hors d'une scène.</summary>
        public string? SceneActor => CurrentScene is Scene s ? Text(s.Actor) : null;

        /// <summary>La scène en cours est finie (son fait de fin appris : le chef de la scène vaincu) : l'interface peut la quitter (<see cref="LeaveScene"/>).</summary>
        public bool SceneFinished => CurrentScene is Scene s && World.Narrative?.Knows(s.End) == true;

        /// <summary>
        /// Entre dans la scène à jouer : comme une porte (sauvegarde dans la zone de la scène, PV pleins), sans être à côté. Refusé, sans rien
        /// changer, s'il n'y a pas de scène à jouer, pendant un dialogue, après une défaite ou avec un ennemi vivant à moins de
        /// <see cref="RestSafety"/> (message « porte bloquée »).
        /// </summary>
        public bool EnterScene()
        {
            if (!(PendingScene is Scene scene)) return false;
            if (World.IsDefeated || Dialogue != null || MustContinue || CombatDisplay.NearbyEnemies(World.Zone.Engine, PlayerId, null, RestSafety).Any())
            {
                Say("text.ui.door_blocked");
                return false;
            }
            Door door = _zone.Doors.First(d => d.To == scene.Zone);
            return Pass(door);
        }

        /// <summary>
        /// Passe la scène à jouer, dehors ou pendant qu'on la joue : son fait de fin est appris (comme jouée), son texte de repli s'affiche ;
        /// pendant la scène, on la quitte aussitôt. Faux s'il n'y a rien à passer.
        /// </summary>
        public bool SkipScene()
        {
            Scene? scene = CurrentScene ?? PendingScene;
            if (scene is null || World.Narrative is null || World.Narrative.Knows(scene.End)) return false;
            // Revue Codex de la PR #94 : pendant la scène, on en sort d'abord ; si la sortie échoue (sauvegarde impossible), rien n'a changé
            // et l'on peut réessayer. Le fait de fin est appris ensuite, dehors (sauvegardé par la sauvegarde automatique suivante).
            if (InScene && !LeaveScene(quiet: true)) return false;
            if (!World.SkipScene(scene.Id)) return false;
            _dirty = true;
            Say("text.ui.discovered", Text(scene.Text));
            return true;
        }

        /// <summary>
        /// Quitte la scène en cours par sa sortie (même pendant un combat : le souvenir s'arrête), vers l'endroit d'où l'on y est entré ; le
        /// texte du retour s'affiche si elle est finie (sinon elle reste à jouer). Faux hors d'une scène ou si la sauvegarde échoue.
        /// </summary>
        public bool LeaveScene() => LeaveScene(quiet: false);

        private bool LeaveScene(bool quiet)
        {
            if (!(CurrentScene is Scene scene)) return false;
            bool done = World.Narrative?.Knows(scene.End) == true;
            Door exit = _zone.Doors.First(d => d.To == ZoneId);
            if (!Pass(exit)) return false;
            if (done && !quiet) Say("text.ui.discovered", Text(scene.Back));
            return true;
        }

        /// <summary>Intérieurs : la zone en cours est un intérieur.</summary>
        public bool Indoors => _zone.Indoor;

        /// <summary>Intérieurs : identifiant de la zone en cours (« zone.vallons », « zone.auberge »).</summary>
        public string CurrentZone => _zone.Id;

        /// <summary>Intérieurs : portes de la zone en cours (nom de ce qu'il y a derrière), pour les montrer.</summary>
        public IReadOnlyList<WorldMarker> Doors() => VisibleDoors.Select(d => new WorldMarker(d.Id, d.Name, d.Position)).ToList();

        /// <summary>Dialogue ouvert (clés de texte), ou null.</summary>
        public DialogueView? Dialogue => World.Narrative?.Open;

        /// <summary>Nom du PNJ du dialogue ouvert, ou null.</summary>
        public string? DialogueNpc => Dialogue is DialogueView d ? Text(_story.Npc(d.Npc).Name) : null;

        /// <summary>Ce que dit le PNJ (traduit), ou null sans dialogue ouvert.</summary>
        public string? DialogueText => Dialogue is DialogueView d ? Text(d.Text) : null;

        /// <summary>
        /// Réponses proposées (traduites), dans l'ordre de <see cref="Choose"/>. Réponses du 31 octobre 2026 : une réponse de livraison dit ce
        /// qui est livré (« Voici : Peau de loup ×3. »).
        /// </summary>
        public IReadOnlyList<string> DialogueAnswers => Dialogue is DialogueView d
            ? d.Options.Select(o => o.Item is string item ? string.Format(CultureInfo.InvariantCulture, Text(o.Text), o.Count, ItemName(item)) : Text(o.Text)).ToList()
            : (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>
        /// Choisit la réponse de rang <paramref name="index"/> parmi celles proposées (<see cref="DialogueAnswers"/>). Revue Codex de la PR #49 :
        /// le rang affiché se traduit en indice réel de la réponse dans le nœud (une réponse dont les conditions manquent n'est pas affichée).
        /// </summary>
        public DialogueRefusal Choose(int index)
        {
            if (!(Dialogue is DialogueView open) || index < 0 || index >= open.Options.Count) return DialogueRefusal.NoSuchChoice;
            DialogueRefusal refusal = World.Choose(open.Options[index].Index, out _, out IReadOnlyList<StoryEvent> events);
            foreach (StoryEvent e in events) Story(e);
            if (events.Count > 0) _dirty = true;
            if (refusal == DialogueRefusal.NoRoom) Say("text.ui.no_room");
            else if (refusal == DialogueRefusal.PurseFull) Say("text.ui.purse_full");
            else if (refusal == DialogueRefusal.NotEnoughMoney) Say("text.ui.not_enough_money");   // sous-lot V1.6
            else if (refusal == DialogueRefusal.Locked) Say("text.ui.quest_locked");
            else if (refusal == DialogueRefusal.TooFar) Say("text.ui.too_far");
            return refusal;
        }

        public void CloseDialogue() => World.Narrative?.Close();

        /// <summary>
        /// Boit l'objet de soin emporté qui soigne le moins parmi ceux qui ont encore des charges (le moteur peut refuser : PV pleins,
        /// recharge, étourdi). Rend faux s'il n'y en a aucun.
        /// </summary>
        public bool Drink()
        {
            IReadOnlyDictionary<string, int> charges = World.Zone.Engine.View(PlayerId).ConsumableCharges;
            string? best = charges.Where(c => c.Value > 0).Select(c => c.Key).OrderBy(id => _items.Item(id).Heal).ThenBy(id => id, StringComparer.Ordinal).FirstOrDefault();
            if (best is null)
            {
                Say("text.ui.no_potion");
                return false;
            }
            try
            {
                World.Drink(best);
                return true;
            }
            catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
            {
                Say("text.ui.refused", e.Message);
                return false;
            }
        }

        /// <summary>Recettes de l'atelier, avec ce qu'il faut et le refus qu'aurait leur fabrication maintenant.</summary>
        public IReadOnlyList<RecipeEntry> Recipes()
        {
            var list = new List<RecipeEntry>();
            foreach (Recipe r in _crafting.Recipes)
            {
                string inputs = string.Join(", ", r.Inputs.Select(i => $"{ItemName(i.Item)} {World.Inventory.Count(i.Item)}/{i.Count}"));
                CraftRefusal refusal = World.CanCraft(r.Id);
                Workshop shop = World.Workshop!;
                list.Add(new RecipeEntry(r.Id, Text(r.Name), Text(_crafting.Professions.First(p => p.Id == r.Profession).Name), r.RequiredLevel, inputs,
                    // Relecture indépendante de la PR #80 : sans petit jeu, l'adresse ne change rien (le meilleur cas est le pire).
                    $"{ItemName(r.Output.Item)} ×{r.Output.Count}", refusal, r.Quality, shop.LevelGap(r),
                    (shop.Chances(r, 0).Fail, r.Quality ? shop.Chances(r, 100).Fail : shop.Chances(r, 0).Fail)));
            }
            return list;
        }

        /// <summary>Fabrique une recette ; un message dit le résultat ou le refus.</summary>
        public CraftRefusal Craft(string recipe)
        {
            CraftRefusal refusal = World.Craft(recipe, out CraftEvent? done);
            if (done != null) Crafted(done);
            else Refused(refusal);
            return refusal;
        }

        private void Crafted(CraftEvent done)
        {
            _dirty = true;
            string profession = Text(_crafting.Professions.First(p => p.Id == done.Profession).Name);
            // Sous-lot V1.4 : une recette ratée rend une partie des matériaux ; une réussite à qualité dit sa qualité.
            if (done.Kind == CraftEventKind.Failed)
            {
                Say("text.ui.craft_failed", ItemName(done.Item), done.Refunded.Count == 0 ? Text("text.ui.craft_nothing_back")
                    : string.Join(", ", done.Refunded.Select(x => $"{ItemName(x.Item)} ×{x.Count}")));
                return;
            }
            if (done.Kind == CraftEventKind.Crafted && done.Quality != ItemQuality.Normal)
                Say(done.Quality == ItemQuality.Masterwork ? "text.ui.crafted_masterwork" : "text.ui.crafted_superior", ItemName(done.Item));
            Say(done.Kind == CraftEventKind.Gathered ? "text.ui.gathered" : "text.ui.crafted", ItemName(done.Item), done.Count, done.Xp, profession);
            if (done.LeveledUp) Say("text.ui.profession_level", profession, done.LevelAfter);
        }

        private void Refused(CraftRefusal refusal) => Say("text.ui.craft." + refusal.ToString().ToLowerInvariant());

        /// <summary>Métiers : nom traduit, niveau et expérience.</summary>
        public IReadOnlyList<(string Name, int Level, long Xp)> Professions() =>
            _crafting.Professions.Select(p => (Text(p.Name), World.Workshop!.Level(p.Id), World.Workshop.Xp(p.Id))).ToList();

        /// <summary>
        /// Sous-lot V1.8c : consomme l'objet de bonus de la place <paramref name="place"/> du sac (plat, élixir, huile ou pierre d'arme ;
        /// <see cref="WorldSession.UseBoon(string, out SavedBoon?)"/>) : hors combat, il occupe sa place de bonus (effet immédiat, sous-lot V1.8d), en
        /// remplaçant le bonus de la même place ; un message dit le résultat ou le refus.
        /// </summary>
        public BoonRefusal UseBoon(int place)
        {
            if (place < 0 || place >= World.Inventory.Stacks.Count)
            {
                Say("text.ui.boon.nothingthere");
                return BoonRefusal.NothingThere;
            }
            string item = World.Inventory.Stacks[place].Item;
            BoonRefusal refusal = World.UseBoon(item, out SavedBoon? replaced);
            if (refusal != BoonRefusal.None)
            {
                Say("text.ui.boon." + refusal.ToString().ToLowerInvariant());
                return refusal;
            }
            _dirty = true;
            SavedBoon now = World.BoonAt(_items.Item(item).Boon!.Value)!;
            int minutes = now.Ticks / BoonKinds.TicksPerMinute;
            if (replaced != null) Say("text.ui.boon.replaced", ItemName(item), minutes, ItemName(replaced.Item));
            else Say("text.ui.boon.used", ItemName(item), minutes);
            return refusal;
        }

        /// <summary>Porte l'objet de la place <paramref name="place"/> (effet à la prochaine halte).</summary>
        public InventoryRefusal Equip(int place)
        {
            InventoryRefusal refusal = World.Equip(place);
            if (refusal == InventoryRefusal.None)
            {
                _dirty = true;
                Say("text.ui.equipped");
            }
            else Say("text.ui.inventory." + refusal.ToString().ToLowerInvariant());
            return refusal;
        }

        /// <summary>
        /// Retire l'objet porté à l'emplacement <paramref name="slot"/> (effet à la prochaine halte). Sous-lot V1.2 : hors combat, et jamais
        /// d'une place obligatoire (l'Éclat du Seuil, le bouclier du Bastion).
        /// </summary>
        public InventoryRefusal Unequip(string slot) => Done(World.Unequip(slot), "text.ui.unequipped");

        /// <summary>Sous-lot V1.2 : porte l'objet de la place <paramref name="place"/> du sac à la place d'équipement <paramref name="slot"/>.</summary>
        public InventoryRefusal EquipTo(int place, string slot) => Done(World.EquipTo(place, slot), "text.ui.equipped");

        /// <summary>Sous-lot V1.2 : échange deux places d'une même sorte (les anneaux, les reliques).</summary>
        public InventoryRefusal Swap(string first, string second) => Done(World.Swap(first, second), "text.ui.inventory.swapped");

        /// <summary>Sous-lot V1.2 : range le sac (sans condition : rien ne change au combat) ; gardé à la prochaine sauvegarde.</summary>
        public void SortInventory(InventorySort order)
        {
            World.Inventory.Sort(order);
            _dirty = true;
            Say("text.ui.inventory.sorted");
        }

        /// <summary>Sous-lot V1.2 : places du sac qui passent le filtre, au niveau actuel du personnage.</summary>
        public IReadOnlyList<int> InventoryPlaces(InventoryFilter filter) => World.Inventory.Filter(filter, World.Level);

        /// <summary>
        /// Sous-lot V1.2 : comparaison de l'objet <paramref name="item"/> avec ce qui est porté à sa place (null s'il ne se porte pas) ;
        /// <paramref name="wornPlace"/> : la place où il est porté, s'il l'est.
        /// </summary>
        public ItemComparison? Compare(string item, string? wornPlace = null) => ItemComparison.For(World.Inventory, item, wornPlace);

        /// <summary>
        /// Sous-lot V1.3 : comparaison de l'instance de la place <paramref name="bagPlace"/> du sac (propriétés tirées comprises), ou de l'objet
        /// porté à <paramref name="wornPlace"/> ; null s'il n'y a rien à comparer.
        /// </summary>
        public ItemComparison? CompareInstance(int bagPlace, string? wornPlace = null)
        {
            if (wornPlace != null)
                return World.Inventory.WornInstanceAt(wornPlace) is { } worn ? ItemComparison.For(World.Inventory, worn, wornPlace) : null;
            return bagPlace >= 0 && bagPlace < World.Inventory.Stacks.Count ? ItemComparison.For(World.Inventory, World.Inventory.Stacks[bagPlace]) : null;
        }

        private InventoryRefusal Done(InventoryRefusal refusal, string done)
        {
            if (refusal == InventoryRefusal.None)
            {
                _dirty = true;
                Say(done);
            }
            else Say("text.ui.inventory." + refusal.ToString().ToLowerInvariant());
            return refusal;
        }

        public ItemDefinition Item(string id) => _items.Item(id);

        /// <summary>
        /// Essai réaliste 6 : expérience cumulée du niveau actuel et du suivant (null au plafond de niveau : sous-lot V1.8), pour la barre de
        /// l'écran du personnage.
        /// </summary>
        public (long From, long? To) LevelXp
        {
            get
            {
                int level = World.Level;
                return (_progression.XpToLevel[level - 1], level < _progression.LevelCap ? _progression.XpToLevel[level] : (long?)null);
            }
        }

        /// <summary>Essai réaliste 6 : PV et endurance maximaux du personnage dans la session (bonus d'équipement compris).</summary>
        public (long Health, long Endurance) Maxima
        {
            get
            {
                CombatantView me = World.Zone.Engine.View(PlayerId);
                return (me.MaxHealth ?? 0, me.MaxEndurance);
            }
        }

        // --- Talents (essai réaliste 6) ---------------------------------------------------------------------------------

        /// <summary>
        /// Essai réaliste 6 (essai de Damien du 27 octobre 2026 : l'arbre des talents, « je ne peux pas l'ouvrir ») : éditeur des talents du
        /// personnage à son niveau actuel (les points d'un niveau gagné en route se placent tout de suite).
        /// </summary>
        public TalentEditor CreateTalentEditor() => new TalentEditor(World.Build, World.Class, World.ProgressionRules);

        /// <summary>
        /// Les talents se changent hors combat (MONDE.md) : faux tant qu'un ennemi vivant est à moins de <see cref="RestSafety"/>, que le
        /// personnage est tombé ou qu'un dialogue est ouvert.
        /// </summary>
        public bool CanChangeTalents => !World.IsDefeated && Dialogue is null
            && !CombatDisplay.NearbyEnemies(World.Zone.Engine, PlayerId, null, RestSafety).Any();

        /// <summary>
        /// Garde les talents de <paramref name="build"/> (jugés à son niveau par le validateur des builds) ; effet à la prochaine halte,
        /// comme l'équipement. Rend null si c'est fait ; sinon rien ne change et le motif est rendu (et dit) : en plein combat
        /// (<see cref="CanChangeTalents"/>), ou premier refus du validateur.
        /// </summary>
        public string? SetTalents(CharacterBuild build)
        {
            if (build is null) throw new ArgumentNullException(nameof(build));
            if (!CanChangeTalents)
            {
                Say("text.ui.talents.in_combat");
                return Text("text.ui.talents.in_combat");
            }
            BuildViolation? why = World.SetTalents(build.TalentRanks);
            if (why != null) return why.Message;
            _dirty = true;
            Say("text.ui.talents.saved");
            return null;
        }

        // --- Compétences (sous-lot V1.5) ---------------------------------------------------------------------------------

        /// <summary>
        /// Sous-lot V1.5 (décision de Damien : changer ses compétences « partout hors combat ») : faux tant que le personnage est tombé, qu'un
        /// dialogue est ouvert, qu'un ennemi vivant est à moins de <see cref="RestSafety"/> (la règle des talents et de la halte), ou qu'un
        /// ennemi engagé le vise, où qu'il soit (un archer qui tire de loin). Écrit à part de <see cref="CanChangeTalents"/> : les talents
        /// iront chez le maître de classe (cadrage Q21, 29 octobre 2026), les compétences restent « partout hors combat ».
        /// </summary>
        public bool CanChangeSkills => SkillsLockKey is null;

        /// <summary>
        /// Sous-lot V1.5 (relecture indépendante de la PR #71) : pourquoi les compétences ne se changent pas maintenant, un motif juste par
        /// cas (tombé, dialogue ouvert, ennemi proche ou engagé) ; null si elles se changent (<see cref="CanChangeSkills"/>).
        /// </summary>
        public string? SkillsLocked => SkillsLockKey is string key ? Text(key) : null;

        private string? SkillsLockKey =>
            World.IsDefeated ? "text.ui.skills.defeated"
            : Dialogue != null ? "text.ui.skills.in_dialogue"
            : CombatDisplay.NearbyEnemies(World.Zone.Engine, PlayerId, null, RestSafety).Any() || EngagedOnMe ? "text.ui.skills.in_combat"
            : null;

        /// <summary>Sous-lot V1.5 : un ennemi présent, vivant et engagé (<see cref="CombatantView.Engaged"/>) vise le personnage.</summary>
        private bool EngagedOnMe
        {
            get
            {
                CombatEngine engine = World.Zone.Engine;
                string team = engine.View(PlayerId).Team;
                foreach (string id in engine.CombatantIds)
                {
                    if (id == PlayerId) continue;
                    CombatantView v = engine.View(id);
                    if (v.Team != team && v.IsPresent && !v.IsDead && v.Engaged && v.CurrentTarget == PlayerId) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Sous-lot V1.5 : l'écran des compétences : la barre telle qu'elle sera à la prochaine halte (<see cref="WorldSession.NextEntry"/>),
        /// ses places ouvertes et fermées, les compétences apprises et celles à venir (descriptions : textes <c>text.skill.…</c>).
        /// </summary>
        public SkillBook Skills() => SkillBook.For(World.NextEntry, _progression, World.Class, World.ProgressionRules, SkillsLocked,
            id => Texts.Has(SkillTextKey(id)) ? Text(SkillTextKey(id)) : "");

        /// <summary>Clé de la description d'une compétence : <c>text.skill.</c> suivi de la fin de son identifiant.</summary>
        public static string SkillTextKey(string skill) =>
            "text.skill." + (skill ?? throw new ArgumentNullException(nameof(skill))).Substring(skill.LastIndexOf('.') + 1);

        /// <summary>
        /// Sous-lot V1.5 : garde la barre <paramref name="actives"/>, <paramref name="ultimate"/>, <paramref name="passives"/>, jugée par
        /// <see cref="WorldSession.SetSkills"/> ; effet à la prochaine halte, comme les talents. Rend null si c'est fait, ou s'il n'y a rien à
        /// changer (la barre de la prochaine halte, <see cref="WorldSession.NextEntry"/> : rien n'est écrit ni dit, et une compétence gagnée
        /// en route reste annoncée à la halte) ; sinon rien ne change et le motif est rendu : <see cref="SkillsLocked"/> (dit aussi dans
        /// les messages), ou premier refus.
        /// </summary>
        public string? SetSkills(IReadOnlyList<string> actives, string? ultimate, IReadOnlyList<string> passives)
        {
            if (actives is null) throw new ArgumentNullException(nameof(actives));
            if (passives is null) throw new ArgumentNullException(nameof(passives));
            if (Locked() is string locked) return locked;
            if (new SkillBar(actives, ultimate, passives).SameAs(SkillBar.Of(World.NextEntry))) return null;   // rien à changer, ni à garder
            BuildViolation? why = World.SetSkills(actives, ultimate, passives);
            if (why != null) return why.Code == BuildViolationCode.RegistryFull ? Text("text.ui.skills.registry_full") : why.Message;
            _dirty = true;
            Say("text.ui.skills.saved");
            return null;
        }

        /// <summary>
        /// Sous-lot V1.5 : met <paramref name="skill"/> à la place <paramref name="index"/> de la sorte <paramref name="kind"/> (0 pour
        /// l'ultime), sur la barre de la prochaine halte (<see cref="SkillBar.Place"/> : échange avec une compétence déjà dans la barre, sinon
        /// celle de la place retourne en réserve). Même jugement que <see cref="SetSkills"/>, et une place fermée est refusée avec le niveau
        /// qui l'ouvre. Rend null si c'est fait (ou s'il n'y a rien à changer : rien n'est écrit ni dit), sinon le motif ; rien ne change.
        /// </summary>
        public string? PlaceSkill(SkillKind kind, int index, string skill)
        {
            if (skill is null) throw new ArgumentNullException(nameof(skill));
            if (index < 0 || (kind == SkillKind.Ultimate && index != 0)) throw new ArgumentOutOfRangeException(nameof(index), index, "Rang de place impossible.");
            return Change(bar => bar.Place(kind, index, skill), kind, index);
        }

        /// <summary>
        /// Sous-lot V1.5 : retire la compétence de la place <paramref name="index"/> de la sorte <paramref name="kind"/> (les suivantes
        /// avancent) ; elle retourne dans la réserve. Une place peut rester vide (réponse de Damien du 30 octobre 2026) ; même jugement que
        /// <see cref="SetSkills"/>.
        /// </summary>
        public string? RemoveSkill(SkillKind kind, int index)
        {
            if (index < 0 || (kind == SkillKind.Ultimate && index != 0)) throw new ArgumentOutOfRangeException(nameof(index), index, "Rang de place impossible.");
            return Change(bar => bar.Remove(kind, index), kind, index);
        }

        private string? Change(Func<SkillBar, SkillBar> change, SkillKind kind, int index)
        {
            if (Locked() is string locked) return locked;
            if (PlayerCharacters.ClosedPlace(kind, index, World.Level, World.ProgressionRules) is BuildViolation closed) return closed.Message;
            SkillBar after = change(SkillBar.Of(World.NextEntry));
            return SetSkills(after.Actives, after.Ultimate, after.Passives);
        }

        /// <summary>Motif de <see cref="SkillsLocked"/>, dit aussi dans les messages ; null si les compétences se changent.</summary>
        private string? Locked()
        {
            if (!(SkillsLockKey is string key)) return null;
            Say(key);
            return Text(key);
        }

        // --- Journal, carte ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Quêtes commencées : en cours d'abord (étape et avancement, ou récompense à choisir), puis accomplies. Sous-lot V1.6 : celles d'un
        /// onglet seulement (<paramref name="category"/>), ou toutes (null).
        /// </summary>
        public IReadOnlyList<JournalEntry> Journal(QuestCategory? category = null)
        {
            Narrative? n = World.Narrative;
            if (n is null) return Array.Empty<JournalEntry>();
            // Relecture de la PR #76 : le journal se recalcule seulement quand l'histoire change (l'interface le lit à chaque image).
            if (!(ReferenceEquals(_journalOf, n) && _journalRevision == n.Revision)) _journal.Clear();
            _journalOf = n;
            _journalRevision = n.Revision;
            if (_journal.TryGetValue(category?.ToString() ?? "", out IReadOnlyList<JournalEntry> cached)) return cached;
            IReadOnlyList<JournalEntry> built = BuildJournal(n, category);
            _journal[category?.ToString() ?? ""] = built;
            return built;
        }

        private Narrative? _journalOf;
        private long _journalRevision = -1;
        private readonly Dictionary<string, IReadOnlyList<JournalEntry>> _journal = new Dictionary<string, IReadOnlyList<JournalEntry>>(StringComparer.Ordinal);

        /// <summary>Relecture de la PR #76 : un onglet a une quête qui attend le choix de sa récompense (un repère sur l'onglet).</summary>
        public bool RewardWaiting(QuestCategory category) => Journal(category).Any(q => q.ChoosingReward);

        private IReadOnlyList<JournalEntry> BuildJournal(Narrative n, QuestCategory? category)
        {
            var list = new List<JournalEntry>();
            IEnumerable<Quest> quests = _story.Quests.Where(q => category is null || q.Category == category);
            foreach (Quest q in quests.Where(q => n.IsActive(q.Id)))
            {
                bool tracked = n.Tracked == q.Id;
                if (n.IsChoosingReward(q.Id))
                {
                    list.Add(new JournalEntry(Text(q.Name), Text("text.ui.journal.choose_reward"), null, false, q.Id, q.Category, tracked,
                        q.RewardChoices.Select(c => c.Count > 1 ? $"{c.Count} × {ItemName(c.Item)}" : ItemName(c.Item)).ToList()));
                    continue;
                }
                QuestStep step = n.CurrentStep(q.Id)!;
                string? progress = step.Objective.Value > 1 && step.Objective.Kind != ObjectiveKind.Deliver ? $"{n.Progress(q.Id)}/{step.Objective.Value}" : null;
                list.Add(new JournalEntry(Text(q.Name), Text(step.Text), progress, false, q.Id, q.Category, tracked));
            }
            foreach (Quest q in quests.Where(q => n.IsDone(q.Id))) list.Add(new JournalEntry(Text(q.Name), null, null, true, q.Id, q.Category));
            return list;
        }

        /// <summary>
        /// Sous-lot V1.6 : quête montrée sous la petite carte et dont l'objectif est sur la carte : la quête suivie, sinon la première en cours
        /// (principales d'abord) ; null sans quête en cours.
        /// </summary>
        public JournalEntry? Followed
        {
            get
            {
                IReadOnlyList<JournalEntry> all = Journal();
                return all.FirstOrDefault(q => q.Tracked) ?? all.FirstOrDefault(q => !q.Done && q.Category == QuestCategory.Main) ?? all.FirstOrDefault(q => !q.Done);
            }
        }

        /// <summary>Sous-lot V1.6 : suit la quête en cours <paramref name="quest"/> (ou plus aucune : null) ; faux si elle n'est pas en cours.</summary>
        public bool Track(string? quest)
        {
            if (World.Narrative is null || !World.Track(quest)) return false;
            _dirty = true;
            return true;
        }

        /// <summary>
        /// Sous-lot V1.6 : choisit la récompense de rang <paramref name="index"/> d'une quête qui l'attend (<see cref="JournalEntry.RewardChoices"/>) ;
        /// un sac plein refuse sans rien changer (un message le dit).
        /// </summary>
        public DialogueRefusal ChooseReward(string quest, int index)
        {
            if (World.Narrative is null) return DialogueRefusal.Locked;
            DialogueRefusal refusal = World.ChooseReward(quest, index, out IReadOnlyList<StoryEvent> events);
            foreach (StoryEvent e in events) Story(e);
            if (events.Count > 0) _dirty = true;
            if (refusal == DialogueRefusal.NoRoom) Say("text.ui.no_room");
            return refusal;
        }

        /// <summary>Découvertes faites, dans l'ordre de l'histoire (textes traduits).</summary>
        public IReadOnlyList<string> Discoveries()
        {
            Narrative? n = World.Narrative;
            return n is null ? Array.Empty<string>() : _story.Discoveries.Where(d => n.Discoveries.Contains(d.Id)).Select(d => Text(d.Text)).ToList();
        }

        /// <summary>PNJ de la zone en cours (sous-lot V1.6 : dans un intérieur, ceux de la pièce).</summary>
        /// <remarks>Sous-lot V2.1 : à leur place de l'heure (la nuit, devant leur porte pour ceux qui rentrent chez eux).</remarks>
        public IReadOnlyList<WorldMarker> Npcs() => HereNpcs.Select(n => new WorldMarker(n.Id, Text(n.Name), World.NpcPosition(n))).ToList();

        /// <summary>
        /// Relecture de la PR #99 : les PNJ de la zone à leur place de jour (celle de l'histoire), quelle que soit l'heure : l'affichage y
        /// prend leur place et leur pose de jour, même pour une partie chargée la nuit.
        /// </summary>
        public IReadOnlyList<WorldMarker> NpcsByDay() => HereNpcs.Select(n => new WorldMarker(n.Id, Text(n.Name), n.Position)).ToList();

        /// <summary>Nœuds de récolte (dans la zone de l'histoire seulement).</summary>
        public IReadOnlyList<WorldMarker> Nodes() => World.InStoryZone
            ? _crafting.Nodes.Select(n => new WorldMarker(n.Id, ItemName(n.Item), n.Position, n.Profession, World.Workshop!.IsReady(n.Id, World.Zone.Engine.CurrentTick))).ToList()
            : (IReadOnlyList<WorldMarker>)Array.Empty<WorldMarker>();

        public IReadOnlyList<WorldMarker> SafePoints() => _zone.SafePoints.Select(s => new WorldMarker(s.Id, s.Name, s.Position)).ToList();

        /// <summary>
        /// Essai du 12.9 (Damien, 25 octobre 2026) : ateliers où l'on fabrique (lieux des recettes, la forge de Maëlle), au centre de leur lieu,
        /// pour qu'on les voie dans la zone.
        /// </summary>
        public IReadOnlyList<WorldMarker> Workshops() => !World.InStoryZone ? Array.Empty<WorldMarker>() : (IReadOnlyList<WorldMarker>)_crafting.Recipes.Select(r => r.Place).Distinct(StringComparer.Ordinal)
            .Select(id => _zone.Places.FirstOrDefault(p => p.Id == id)).Where(p => p != null)
            .Select(p => new WorldMarker(p!.Id, p.Name, new GroundVector(Fixed.FromRaw((p.Min.X.Raw + p.Max.X.Raw) / 2), Fixed.FromRaw((p.Min.Z.Raw + p.Max.Z.Raw) / 2))))
            .ToList();

        // --- Carte (sous-lot 12.9d) -------------------------------------------------------------------------------------

        private AdventureMap? _map;
        private int _mapSession = -1;

        /// <summary>
        /// Sous-lot 12.9d : carte de la zone pour la session ouverte (décor de son espace : une porte ouverte n'y est plus ; lieux nommés).
        /// Recalculée à chaque nouvelle session.
        /// </summary>
        public AdventureMap Map
        {
            get
            {
                if (_map is null || _mapSession != Opened)
                {
                    _map = new AdventureMap(World.Zone.Setup.Space ?? throw new InvalidOperationException("Zone sans espace."),
                        _zone.Places.Select(p => new MapPlace(p.Id, p.Name, Meters(p.Min.X), Meters(p.Min.Z), Meters(p.Max.X), Meters(p.Max.Z))),
                        Roads(_zone),
                        // Vallons agrandis : là où l'on s'arrête (PNJ, points sûrs, ateliers, nœuds de récolte) reste dégagé des champs.
                        Npcs().Select(n => (n.X, n.Z)).Concat(SafePoints().Select(s => (s.X, s.Z))).Concat(Workshops().Select(w => (w.X, w.Z)))
                            .Concat(Nodes().Select(n => (n.X, n.Z))).Concat(Doors().Select(d => (d.X, d.Z))),
                        VisibleDoors.Select(d => new MapDoor(d.Id, d.Name, d.Building, Meters(d.Position.X), Meters(d.Position.Z))), _zone.Indoor,
                        // Sous-lot V1.6 : les PNJ qui parlent dans une pièce y ont leur corps (décor des intérieurs).
                        _zone.Indoor ? Npcs().Select(n => (n.Id, n.X, n.Z)) : null);
                    _mapSession = Opened;
                }
                return _map;
            }
        }

        private static double Meters(Fixed f) => f.Raw / 10000.0;

        /// <summary>Vallons agrandis : routes de la zone, en mètres, pour la carte peinte et le décor.</summary>
        public static IReadOnlyList<StyleRoad> Roads(ZoneDefinition zone) =>
            (zone ?? throw new ArgumentNullException(nameof(zone))).Roads
                .Select(r => new StyleRoad(r.Id, r.Points.Select(p => (Meters(p.X), Meters(p.Z))).ToList(), Meters(r.Width), r.Kind == RoadKind.Cobble)).ToList();

        /// <summary>
        /// Sous-lot 12.9d : repères fixes de la carte : PNJ, points sûrs, ateliers, nœuds de récolte, puis le personnage (s'il est dans la zone)
        /// et l'objectif de l'étape en cours (<see cref="Objective"/>).
        /// </summary>
        public IReadOnlyList<MapMarker> MapMarkers()
        {
            var list = new List<MapMarker>();
            list.AddRange(Npcs().Select(n => new MapMarker(MapMarkerKind.Npc, n.Name, n.X, n.Z)));
            list.AddRange(SafePoints().Select(s => new MapMarker(MapMarkerKind.SafePoint, s.Name, s.X, s.Z)));
            list.AddRange(Workshops().Select(w => new MapMarker(MapMarkerKind.Workshop, w.Name, w.X, w.Z)));
            list.AddRange(Nodes().Select(n => new MapMarker(MapMarkerKind.Node, n.Name, n.X, n.Z)));
            list.AddRange(Doors().Select(d => new MapMarker(MapMarkerKind.Door, d.Name, d.X, d.Z)));
            if (Me is GroundVector me) list.Add(new MapMarker(MapMarkerKind.Player, Text("text.ui.map.you"), Meters(me.X), Meters(me.Z)));
            if (Objective is MapMarker objective) list.Add(objective);
            return list;
        }

        /// <summary>
        /// Sous-lot 12.9d : où mène l'étape en cours de la quête suivie (sous-lot V1.6 : <see cref="Followed"/>), d'après les données : lieu à
        /// atteindre (son centre), créature à vaincre (sa place ; la plus proche de sa famille), PNJ à qui parler ou livrer, fait appris d'un PNJ
        /// (le PNJ dont une réponse l'apprend) ou par la victoire sur un chef (sa première forme), passage ouvert par un PNJ. Un PNJ d'une autre
        /// zone (un intérieur) : la porte qui y mène ; dans un intérieur, la porte de sortie. Null sans étape, ou sans lieu connu.
        /// </summary>
        public MapMarker? Objective
        {
            get
            {
                Narrative? n = World.Narrative;
                if (n is null || !(Followed is JournalEntry followed) || !(n.CurrentStep(followed.Id) is QuestStep step)) return null;
                string label = Text("text.ui.map.objective");
                MapMarker At(GroundVector p) => new MapMarker(MapMarkerKind.Objective, label, Meters(p.X), Meters(p.Z));
                var o = step.Objective;
                // PNJ visé, où qu'il vive.
                Npc? npc = o.Kind switch
                {
                    ObjectiveKind.Talk => _story.Npc(o.Id),
                    ObjectiveKind.Deliver => _story.Npc(o.Npc!),
                    ObjectiveKind.Fact => NpcWith(EffectKind.SetFact, o.Id),
                    ObjectiveKind.Passage => NpcWith(EffectKind.OpenPassage, o.Id),
                    _ => null,
                };
                if (npc != null && npc.Zone == (World.InStoryZone ? null : _zone.Id)) return At(World.NpcPosition(npc));
                // Intérieurs : le PNJ vit dans une pièce dont une porte est ici ; sinon, dans une pièce, la porte de sortie.
                if (npc?.Zone != null && _zone.Doors.FirstOrDefault(d => d.To == npc.Zone) is Door toNpc) return At(toNpc.Position);
                // Sous-lot V1.7 : une pièce derrière une autre (les archives, derrière la guilde) : la porte de la pièce qui y mène.
                if (npc?.Zone != null && _zone.Doors.FirstOrDefault(d => _zones.FirstOrDefault(z => z.Id == d.To)?.Doors.Any(x => x.To == npc.Zone) == true) is Door via)
                    return At(via.Position);
                if (!World.InStoryZone)
                {
                    // Relecture de la PR #79 : un rang de guilde à atteindre n'est nulle part (pas d'étoile sur la porte).
                    if (o.Kind == ObjectiveKind.Collect || o.Kind == ObjectiveKind.Equipped || o.Kind == ObjectiveKind.GuildRank || o.Kind == ObjectiveKind.CraftQuality || _zone.Doors.Count == 0) return null;
                    return At(_zone.Doors[0].Position);
                }
                switch (o.Kind)
                {
                    case ObjectiveKind.Reach when _zone.Places.FirstOrDefault(p => p.Id == o.Id) is Place place:
                        return new MapMarker(MapMarkerKind.Objective, label, (Meters(place.Min.X) + Meters(place.Max.X)) / 2, (Meters(place.Min.Z) + Meters(place.Max.Z)) / 2);
                    case ObjectiveKind.Defeat when _zone.Populations.FirstOrDefault(p => p.Creature == o.Id)?.Template.Placement is CombatantPlacement at:
                        return At(at.Position);
                    case ObjectiveKind.DefeatFamily:
                        GroundVector from = Me ?? _zone.Entry.Position;
                        // Revue Codex de la PR #99 : la plus proche parmi celles qu'on peut trouver à cette heure (pas un loup de la nuit en plein
                        // jour) ; aucune : la plus proche de toutes.
                        CombatantPlacement? Nearest(IEnumerable<Population> among) => among.Select(p => p.Template.Placement)
                            .Where(x => x != null).OrderBy(x => Distance2(from, x!.Position)).FirstOrDefault();
                        var family = _zone.Populations.Where(p => p.Family == o.Id).ToList();
                        CombatantPlacement? nearest = Nearest(family.Where(p => World.IsAvailable(p))) ?? Nearest(family);
                        return nearest is null ? null : At(nearest.Position);
                    case ObjectiveKind.Fact when _zone.Bosses.FirstOrDefault(b => b.Fact == o.Id) is { } boss && boss.Forms[0].Placement is CombatantPlacement chef:
                        return At(chef.Position);
                }
                return null;
            }
        }

        /// <summary>Premier PNJ (ordre de l'histoire) dont une réponse de dialogue a l'effet <paramref name="kind"/> sur <paramref name="id"/>.</summary>
        private Npc? NpcWith(EffectKind kind, string id) => _story.Npcs.FirstOrDefault(npc =>
            _story.Dialogues.FirstOrDefault(d => d.Id == npc.Dialogue) is Dialogue dialogue
            && dialogue.Nodes.Any(node => node.Choices.Any(c => c.Effects.Any(e => e.Kind == kind && e.Id == id))));

        /// <summary>Atelier où se trouve le personnage (nom), ou null.</summary>
        public string? WorkshopHere => Me is GroundVector p && World.InStoryZone
            && _zone.PlacesAt(p).FirstOrDefault(place => _crafting.Recipes.Any(r => r.Place == place.Id)) is Place here ? here.Name : null;

        /// <summary>Lieu où se trouve le personnage (nom), ou le nom de la zone.</summary>
        public string PlaceName => Me is GroundVector p && _zone.PlacesAt(p).FirstOrDefault() is Place place ? place.Name : _zone.Name;

        /// <summary>
        /// Sous-lot V1.8 (zones en mélange) : niveau minimum du lieu où se trouve le personnage, et niveau de ses créatures pour lui (le niveau
        /// d'entrée de la session : le minimum du lieu, ou le sien moins le réglage de facilité s'il est plus fort) ; null sans réglage (un
        /// intérieur, une zone sans mélange).
        /// </summary>
        public (int Min, int Creatures)? PlaceLevel =>
            Me is GroundVector p && _zone.Scaling is ZoneScaling s
                ? (_zone.MinLevelAt(p), s.CreatureLevel(_zone.MinLevelAt(p), World.EnteredLevel))
                : ((int, int)?)null;

        /// <summary>Sous-lot V1.8 : nom du lieu et niveau de ses créatures pour l'affichage (« Moulin · niveau 3 »).</summary>
        public string PlaceLabel => PlaceLevel is (int _, int creatures)
            ? string.Format(CultureInfo.InvariantCulture, Text("text.ui.place_level"), PlaceName, creatures) : PlaceName;

        /// <summary>Essai réaliste 6 : nom de la zone (« Vallons d'Aube »), sous le nom du lieu près de la petite carte.</summary>
        public string ZoneName => _zone.Name;

        /// <summary>Heure du monde (« jour 1, 08 h 30 »).</summary>
        public string TimeOfDay => World.Zone.Time.ToString();

        /// <summary>Essai réaliste 7 : portée où le Gardien vivant donne sa musique (mètres).</summary>
        public static readonly Fixed BossMusicRange = Fixed.FromInt(25);

        /// <summary>
        /// Essai réaliste 7 (demande de Damien du 28 octobre 2026 : musique et ambiances) : ce qu'écoutent la musique et les nappes
        /// d'ambiance de l'essai de style. Village : un lieu habité (village, bourg, hameau : <see cref="TownRules.Inhabited"/>) ; combat : un
        /// ennemi vivant à moins de <see cref="RestSafety"/> (la règle de la halte) ; Gardien : une de ses formes vivante à moins de
        /// <see cref="BossMusicRange"/> ; rivière : distance à l'eau (obstacles « riviere… », lac compris ; mètres, 0 dedans, NaN sans eau).
        /// Affichage seulement : rien ne change dans le monde.
        /// </summary>
        public AdventureMood Mood()
        {
            if (!(Me is GroundVector p) || World.IsDefeated) return new AdventureMood(false, false, false, double.NaN);
            // Vallons agrandis : le bourg et le hameau sont habités comme le village.
            bool village = _zone.Indoor || _zone.PlacesAt(p).Any(x => TownRules.Inhabited(x.Id));
            // Relecture de l'essai 7 : une seule recherche (la plus large), puis la distance de chaque ennemi trouvé.
            bool fighting = false, boss = false;
            long rest2 = checked(RestSafety.Raw * RestSafety.Raw);
            foreach (OtherState o in CombatDisplay.NearbyEnemies(World.Zone.Engine, PlayerId, null, BossMusicRange))
            {
                if (o.View.Position is GroundVector q)
                {
                    long dx = checked(q.X.Raw - p.X.Raw), dz = checked(q.Z.Raw - p.Z.Raw);
                    if (checked(dx * dx + dz * dz) <= rest2) fighting = true;
                }
                if (IsBoss(o.Id)) boss = true;
                if (boss && fighting) break;
            }
            // Vallons agrandis : la distance à l'eau elle-même (bras de la rivière et lac, obstacles « riviere… »), et non plus au seul lieu
            // « riviere » du village : la rivière s'entend sur toute sa longueur.
            double river = double.NaN;
            double x = p.X.Raw / 10000.0, z = p.Z.Raw / 10000.0;
            foreach (SpaceObstacle water in World.Zone.Setup.Space?.Obstacles ?? Array.Empty<SpaceObstacle>())
            {
                if (!water.Id.StartsWith("riviere", StringComparison.Ordinal)) continue;
                double d = Math.Max(0, -StyleRules.InsideDistance(x, z, water.Points.Select(q => (q.X.Raw / 10000.0, q.Z.Raw / 10000.0)).ToList()));
                river = double.IsNaN(river) ? d : Math.Min(river, d);
            }
            return new AdventureMood(village, fighting, boss, river, _zone.Indoor, DayCycle.NightShare(WorldMinute()), Weather().Rain / (double)WorldWeather.Scale);
        }

        /// <summary>
        /// Sous-lot V2.1 : minute du jour du monde, avec sa fraction ; <paramref name="partialTick"/> (0 à 1) avance d'une part de tick (le
        /// temps écoulé à l'écran depuis le dernier tick) pour que le soleil glisse sans à-coups.
        /// </summary>
        public double WorldMinute(double partialTick = 0) => DayCycle.ExactMinute(World.Zone.Clock, World.Zone.WorldTick, partialTick);

        /// <summary>Sous-lot V2.1 : lumière du monde à l'heure de la partie (nuit bleue et lisible, lanternes, transitions douces).</summary>
        public DayLight Light(double partialTick = 0) => DayCycle.At(WorldMinute(partialTick));

        /// <summary>Sous-lot V2.1 : c'est la nuit du monde (22 h à 5 h) : villageois rentrés, bêtes de la nuit dehors.</summary>
        public bool IsNight => World.Zone.Time.Phase == DayPhase.Night;

        private long _weatherMinute = -1;
        private WeatherState _weather;

        /// <summary>
        /// Sous-lot V2.2 (météo) : le temps qu'il fait dans la zone de l'histoire (<see cref="WorldSession.Weather"/>), relu seulement quand la
        /// minute du monde change (7,5 s de jeu) : aucun calcul ni allocation à chaque image.
        /// </summary>
        public WeatherState Weather()
        {
            WorldTime time = World.Zone.Time;
            long key = time.Day * WorldClock.MinutesPerDay + time.Minute;
            if (key != _weatherMinute)
            {
                _weather = World.Weather;
                _weatherMinute = key;
            }
            return _weather;
        }

        /// <summary>Sous-lot V2.2 : ce que le temps change à l'affichage, à l'heure de la partie (<see cref="WeatherCycle.Look"/>).</summary>
        public WeatherLook WeatherLook(double partialTick = 0) => WeatherCycle.Look(Weather(), Light(partialTick));

        /// <summary>Formes du chef de la zone (pour l'affichage).</summary>
        /// <remarks>Sous-lot V1.6 : tous les chefs de la zone (le chef des bandits aussi), y compris une forme revenue sous une nouvelle identité
        /// (revue Codex de la PR #48).</remarks>
        public bool IsBoss(string combatant) => World.IsBossForm(combatant);

        // --- Halte, reprise, sauvegarde ---------------------------------------------------------------------------------

        private void TrackSafePoint()
        {
            if (Me is GroundVector p && _zone.SafePoints.FirstOrDefault(s => Within(p, s.Position, RestRange)) is SafePoint near) _lastSafePoint = near.Id;
        }

        /// <summary>Point sûr où la partie reprendra (le dernier approché, sinon l'entrée).</summary>
        public string LastSafePoint => _zone.SafePoints.First(s => s.Id == _lastSafePoint).Name;

        /// <summary>
        /// Halte possible : personnage vivant à <see cref="RestRange"/> d'un point sûr, aucun ennemi vivant à moins de
        /// <see cref="RestSafety"/>, aucun dialogue ouvert.
        /// </summary>
        public bool CanRest => !World.IsDefeated && Me is GroundVector p && _zone.SafePoints.Any(s => Within(p, s.Position, RestRange))
            && !CombatDisplay.NearbyEnemies(World.Zone.Engine, PlayerId, null, RestSafety).Any() && Dialogue is null;

        /// <summary>
        /// L'arrivée du personnage a été refusée (une créature occupait le point sûr) : il n'est pas dans la zone ; une reprise réessaie.
        /// </summary>
        public bool Stranded
        {
            get
            {
                CombatantView me = World.Zone.Engine.View(PlayerId);
                return !me.IsPresent && !me.HasLeft && me.ArrivalTick is long t && t < World.Zone.Engine.CurrentTick;
            }
        }

        /// <summary>Il faut reprendre : défaite, arrivée refusée, ou fin de la session (durée maximale atteinte).</summary>
        public bool MustContinue => World.IsDefeated || Stranded || Combat.IsFinished;

        /// <summary>
        /// Halte au point sûr proche : sauvegarde, puis nouvelle session à ce point sûr (PV pleins ; niveau, équipement, passages ouverts
        /// pris en compte). Rend faux si la halte n'est pas possible (<see cref="CanRest"/>).
        /// </summary>
        public bool Rest()
        {
            if (!CanRest) return false;
            TrackSafePoint();
            Reopen("text.ui.rested");
            return true;
        }

        /// <summary>Reprise après une défaite ou une fin de session : nouvelle session au dernier point sûr. Rend faux s'il n'y a pas lieu.</summary>
        public bool Continue()
        {
            if (!MustContinue) return false;
            Reopen("text.ui.continued");
            return true;
        }

        private void Reopen(string message)
        {
            WorldSave save = World.Save();
            Write(save);
            Open(save, _lastSafePoint);
            Say(message, LastSafePoint);
            AnnounceLearned();
        }

        /// <summary>Sauvegarde l'état actuel (écriture atomique, copie de secours gardée).</summary>
        /// <exception cref="IOException">Disque plein ou inaccessible (de même <see cref="UnauthorizedAccessException"/>).</exception>
        public void Save() => Write(World.Save());

        private void Write(WorldSave save)
        {
            string? folder = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            SaveStore.Write(SavePath, save);
            _dirty = false;
            _sinceSave = 0;
        }
    }
}
