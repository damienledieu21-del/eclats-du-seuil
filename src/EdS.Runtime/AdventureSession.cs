using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using EdS.Data;
using EdS.Data.Characters;
using EdS.Data.Classes;
using EdS.Data.Crafting;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Zones;
using EdS.Domain.Builds;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using EdS.World;

namespace EdS.Runtime
{
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

    /// <summary>Une quête du journal : nom, étape en cours (texte), avancement (« 2/3 ») ou accomplie.</summary>
    public sealed class JournalEntry
    {
        public string Name { get; }
        public string? Step { get; }
        public string? Progress { get; }
        public bool Done { get; }

        internal JournalEntry(string name, string? step, string? progress, bool done)
        {
            Name = name;
            Step = step;
            Progress = progress;
            Done = done;
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

        internal RecipeEntry(string id, string name, string profession, int level, string inputs, string output, CraftRefusal refusal)
        {
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
    public sealed class AdventureSession
    {
        public const string PlayerId = "bastion";
        public const string PlayerClass = "class.bastion";
        public const string ZoneId = "zone.vallons";

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
        private readonly ZoneDefinition _zone;
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

        private AdventureSession(GameData data, CharacterProgression progression, ItemCatalog items, ZoneDefinition zone, TextCatalog texts,
            StoryBook story, CraftingBook crafting, string savePath)
        {
            _data = data;
            _progression = progression;
            _items = items;
            _zone = zone;
            Texts = texts;
            _story = story;
            _crafting = crafting;
            SavePath = savePath;
            _lastSafePoint = zone.Entry.Id;
            SaveReadResult read = SaveStore.Read(savePath);
            SaveProblem = read.Problem;
            WorldSave save = read.Save ?? new WorldSave(zone.Id, 0, 0, Array.Empty<string>(), PlayerCharacters.Create(PlayerId, PlayerClass, progression));
            if (save.Character is null) save = new WorldSave(save.Zone, save.WorldTick, save.Sessions, save.Consumed, PlayerCharacters.Create(PlayerId, PlayerClass, progression));
            Open(save, null);
            Say(read.Save is null ? "text.ui.new_game" : read.FromBackup ? "text.ui.loaded_backup" : "text.ui.loaded");
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
            ZoneLoadResult zone = ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(dataDirectory, ZoneId), scenariosDirectory, data, catalog);
            ZoneDefinition definition = Need(zone.Zone, "zone", () => zone.Report);
            TextsLoadResult texts = StoryJsonReader.ReadTextsFile(StoryJsonReader.TextsPathIn(dataDirectory));
            TextCatalog catalogTexts = Need(texts.Texts, "textes", () => texts.Report);
            StoryLoadResult story = StoryJsonReader.ReadStoryFile(StoryJsonReader.StoryPathIn(dataDirectory), catalogTexts, definition, catalog);
            CraftingLoadResult crafting = CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(dataDirectory), catalogTexts, definition, catalog);
            return new AdventureSession(data, progression, catalog, definition, catalogTexts, Need(story.Story, "histoire", () => story.Report),
                Need(crafting.Book, "métiers", () => crafting.Report), savePath);
        }

        private static T Need<T>(T? value, string what, Func<object> report) where T : class =>
            value ?? throw new InvalidDataException($"Données du monde refusées ({what}) : {report()}");

        /// <summary>Ouvre une session de la zone sur <paramref name="save"/>, au point sûr <paramref name="safePoint"/> (null : l'entrée).</summary>
        private void Open(WorldSave save, string? safePoint)
        {
            WorldSession world = new WorldSession(save, _zone, _data, _progression, _items, _story, _crafting, null, safePoint);
            World = world;
            Combat = new RealtimeCombat(world.Zone.Setup, world.Zone.Engine, PlayerId, () => Collect(world, world.Step()), TargetRange);
            _drunk.Clear();
            _strandedAnnounced = false;
            foreach (KeyValuePair<string, int> c in world.Zone.Engine.View(PlayerId).ConsumableCharges) _drunk[c.Key] = c.Value;
            Opened = checked(Opened + 1);
            if (world.Learned.Count > 0) _dirty = true;   // sous-lot 12.9b : les compétences gagnées vont à la prochaine sauvegarde
        }

        /// <summary>Sous-lot 12.9b : annonce les compétences gagnées à l'ouverture de la session, avec leur touche.</summary>
        private void AnnounceLearned()
        {
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
            int steps = MustContinue ? 0 : Combat.Advance(seconds);
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

        private void Say(string key, params object[] args)
        {
            string text = Texts.Get(key);
            if (args.Length > 0) text = string.Format(CultureInfo.InvariantCulture, text, args);
            if (_messages.Count >= MaxMessages) _messages.RemoveAt(0);
            _messages.Add(text);
        }

        /// <summary>Texte d'une clé, sinon la clé elle-même (texte manquant visible).</summary>
        public string Text(string key) => Texts.Get(key);

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
                // Sous-lot 12.9b : ce que ce niveau fera gagner à la prochaine halte.
                foreach (SkillUnlock u in _progression.ForClass(PlayerClass).SkillUnlocks)
                    if (u.Level > g.LevelBefore && u.Level <= g.LevelAfter) Say("text.ui.skill_soon", SkillName(u.Skill));
            }
            foreach (LootGained l in step.Loot) Say(l.Kept ? "text.ui.loot" : "text.ui.loot_lost", ItemName(l.Item), l.Count);
            foreach (BossEvent b in step.Boss)
            {
                if (b.Kind == BossEventKind.FormArrived && b.Phase > 0) Say("text.ui.boss_phase");
                else if (b.Kind == BossEventKind.Defeated) Say("text.ui.boss_defeated");
            }
            foreach ((string item, int count) in step.Rewards) Say("text.ui.reward", ItemName(item), count);
            foreach (StoryEvent e in step.Story) Story(e);
            if (step.Defeated) Say("text.ui.defeated");
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
                case StoryEventKind.Discovered:
                    Say("text.ui.discovered", Text(_story.Discoveries.First(d => d.Id == e.Id).Text));
                    break;
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

        /// <summary>PNJ à qui parler (le plus proche à portée), ou null.</summary>
        public Npc? NpcInReach => Me is GroundVector p
            ? _story.Npcs.Where(n => Within(p, n.Position, Narrative.TalkRange)).OrderBy(n => Distance2(p, n.Position)).FirstOrDefault() : null;

        /// <summary>Nœud de récolte prêt le plus proche à portée, ou null.</summary>
        public GatheringNode? NodeInReach => Me is GroundVector p
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
                DialogueRefusal refusal = World.Talk(npc.Id, out _);
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
            Say("text.ui.nothing_here");
            return false;
        }

        /// <summary>Dialogue ouvert (clés de texte), ou null.</summary>
        public DialogueView? Dialogue => World.Narrative?.Open;

        /// <summary>Nom du PNJ du dialogue ouvert, ou null.</summary>
        public string? DialogueNpc => Dialogue is DialogueView d ? Text(_story.Npc(d.Npc).Name) : null;

        /// <summary>Ce que dit le PNJ (traduit), ou null sans dialogue ouvert.</summary>
        public string? DialogueText => Dialogue is DialogueView d ? Text(d.Text) : null;

        /// <summary>Réponses proposées (traduites), dans l'ordre de <see cref="Choose"/>.</summary>
        public IReadOnlyList<string> DialogueAnswers => Dialogue is DialogueView d ? d.Options.Select(o => Text(o.Text)).ToList() : (IReadOnlyList<string>)Array.Empty<string>();

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
                list.Add(new RecipeEntry(r.Id, Text(r.Name), Text(_crafting.Professions.First(p => p.Id == r.Profession).Name), r.RequiredLevel, inputs,
                    $"{ItemName(r.Output.Item)} ×{r.Output.Count}", refusal));
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
            Say(done.Kind == CraftEventKind.Gathered ? "text.ui.gathered" : "text.ui.crafted", ItemName(done.Item), done.Count, done.Xp, profession);
            if (done.LeveledUp) Say("text.ui.profession_level", profession, done.LevelAfter);
        }

        private void Refused(CraftRefusal refusal) => Say("text.ui.craft." + refusal.ToString().ToLowerInvariant());

        /// <summary>Métiers : nom traduit, niveau et expérience.</summary>
        public IReadOnlyList<(string Name, int Level, long Xp)> Professions() =>
            _crafting.Professions.Select(p => (Text(p.Name), World.Workshop!.Level(p.Id), World.Workshop.Xp(p.Id))).ToList();

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

        /// <summary>Retire l'objet porté à l'emplacement <paramref name="slot"/> (effet à la prochaine halte).</summary>
        public InventoryRefusal Unequip(string slot)
        {
            InventoryRefusal refusal = World.Inventory.Unequip(slot);
            if (refusal == InventoryRefusal.None)
            {
                _dirty = true;
                Say("text.ui.unequipped");
            }
            else Say("text.ui.inventory." + refusal.ToString().ToLowerInvariant());
            return refusal;
        }

        public ItemDefinition Item(string id) => _items.Item(id);

        // --- Journal, carte ---------------------------------------------------------------------------------------------

        /// <summary>Quêtes commencées : en cours d'abord (étape et avancement), puis accomplies.</summary>
        public IReadOnlyList<JournalEntry> Journal()
        {
            Narrative? n = World.Narrative;
            var list = new List<JournalEntry>();
            if (n is null) return list;
            foreach (Quest q in _story.Quests.Where(q => n.IsActive(q.Id)))
            {
                QuestStep step = n.CurrentStep(q.Id)!;
                string? progress = step.Objective.Value > 1 ? $"{n.Progress(q.Id)}/{step.Objective.Value}" : null;
                list.Add(new JournalEntry(Text(q.Name), Text(step.Text), progress, false));
            }
            foreach (Quest q in _story.Quests.Where(q => n.IsDone(q.Id))) list.Add(new JournalEntry(Text(q.Name), null, null, true));
            return list;
        }

        /// <summary>Découvertes faites, dans l'ordre de l'histoire (textes traduits).</summary>
        public IReadOnlyList<string> Discoveries()
        {
            Narrative? n = World.Narrative;
            return n is null ? Array.Empty<string>() : _story.Discoveries.Where(d => n.Discoveries.Contains(d.Id)).Select(d => Text(d.Text)).ToList();
        }

        public IReadOnlyList<WorldMarker> Npcs() => _story.Npcs.Select(n => new WorldMarker(n.Id, Text(n.Name), n.Position)).ToList();

        public IReadOnlyList<WorldMarker> Nodes() => _crafting.Nodes
            .Select(n => new WorldMarker(n.Id, ItemName(n.Item), n.Position, n.Profession, World.Workshop!.IsReady(n.Id, World.Zone.Engine.CurrentTick))).ToList();

        public IReadOnlyList<WorldMarker> SafePoints() => _zone.SafePoints.Select(s => new WorldMarker(s.Id, s.Name, s.Position)).ToList();

        /// <summary>
        /// Essai du 12.9 (Damien, 25 octobre 2026) : ateliers où l'on fabrique (lieux des recettes, la forge de Maëlle), au centre de leur lieu,
        /// pour qu'on les voie dans la zone.
        /// </summary>
        public IReadOnlyList<WorldMarker> Workshops() => _crafting.Recipes.Select(r => r.Place).Distinct(StringComparer.Ordinal)
            .Select(id => _zone.Places.FirstOrDefault(p => p.Id == id)).Where(p => p != null)
            .Select(p => new WorldMarker(p!.Id, p.Name, new GroundVector(Fixed.FromRaw((p.Min.X.Raw + p.Max.X.Raw) / 2), Fixed.FromRaw((p.Min.Z.Raw + p.Max.Z.Raw) / 2))))
            .ToList();

        /// <summary>Atelier où se trouve le personnage (nom), ou null.</summary>
        public string? WorkshopHere => Me is GroundVector p
            && _zone.PlacesAt(p).FirstOrDefault(place => _crafting.Recipes.Any(r => r.Place == place.Id)) is Place here ? here.Name : null;

        /// <summary>Lieu où se trouve le personnage (nom), ou le nom de la zone.</summary>
        public string PlaceName => Me is GroundVector p && _zone.PlacesAt(p).FirstOrDefault() is Place place ? place.Name : _zone.Name;

        /// <summary>Heure du monde (« jour 1, 08 h 30 »).</summary>
        public string TimeOfDay => World.Zone.Time.ToString();

        /// <summary>Formes du chef de la zone (pour l'affichage).</summary>
        public bool IsBoss(string combatant) => _zone.Boss is { } b && (b.Forms.Any(f => f.Id == combatant) || World.BossForm == combatant);   // forme revenue sous une nouvelle identité (revue Codex de la PR #48)

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
