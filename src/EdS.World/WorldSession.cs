using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data;
using EdS.Data.Characters;
using EdS.Data.Crafting;
using EdS.Data.Items;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Zones;
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

        internal LootGained(ZoneEvent source, string item, int count, bool kept)
        {
            Source = source;
            Item = item;
            Count = count;
            Kept = kept;
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

        public bool IsEmpty => Experience.Count == 0 && Loot.Count == 0 && Population.Count == 0 && Story.Count == 0 && Boss.Count == 0 && Rewards.Count == 0 && !Defeated;

        internal SessionStep(IReadOnlyList<ExperienceGained> experience, IReadOnlyList<LootGained> loot, IReadOnlyList<PopulationEvent> population,
            IReadOnlyList<StoryEvent> story, IReadOnlyList<BossEvent>? boss = null, IReadOnlyList<(string, int)>? rewards = null, bool defeated = false)
        {
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
    public sealed class WorldSession : INarrativeWorld
    {
        private readonly CharacterProgression _progression;
        private SavedCharacter _character;

        public Zone Zone { get; }

        /// <summary>Personnage tel qu'il sera sauvegardé (expérience à jour).</summary>
        public SavedCharacter Character => _character;

        /// <summary>Niveau du personnage d'après son expérience (celui de son entrée dans la zone peut être plus bas).</summary>
        public int Level => _progression.LevelFor(_character.Xp);

        /// <summary>Niveau avec lequel il est entré dans la zone (son build de combat).</summary>
        public int EnteredLevel { get; }

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
        }

        private WorldSession(WorldSave save, CombatSetup zoneSetup, GameData data, CharacterProgression progression, ItemCatalog items,
            CombatantPlacement placement, IReadOnlyDictionary<string, string>? lootTables, ICombatLog? log, bool _)
        {
            _items = items;
            if (save is null) throw new ArgumentNullException(nameof(save));
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            if (items is null) throw new ArgumentNullException(nameof(items));
            _character = save.Character ?? throw new ArgumentException("Sauvegarde sans personnage : le créer d'abord.", nameof(save));
            _lootTables = new Dictionary<string, LootTable>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> entry in lootTables ?? new Dictionary<string, string>())
                _lootTables[entry.Key] = items.TryGetLoot(entry.Value, out LootTable table) ? table
                    : throw new ArgumentException($"Table de butin « {entry.Value} » ({entry.Key}) absente du catalogue.", nameof(lootTables));
            Inventory = Inventory.From(_character, items, Level);
            // Sous-lot 12.6 : objets de soin de l'inventaire emportés au combat, au plus une pile de chacun (recharge partagée).
            var carried = new List<ConsumableSetup>();
            foreach (string id in Inventory.Stacks.Select(st => st.Item).Distinct().OrderBy(i => i, StringComparer.Ordinal))
                if (items.Item(id) is { Type: ItemType.Consumable } potion)
                {
                    int count = Math.Min(Inventory.Count(id), potion.Stack);
                    carried.Add(new ConsumableSetup(id, potion.Heal, count, potion.CooldownMs));
                    _carried[id] = count;
                }
            CharacterSetup setup = PlayerCharacters.Setup(_character, progression, data, placement, gear: Inventory.Gear(), consumables: carried);
            Zone = new Zone(save.Zone, zoneSetup, checked(save.Sessions + 1), save.WorldTick, save.Consumed, log);
            Zone.Enter(setup);
            EnteredLevel = setup.Build.Level;
            EnteredGear = setup.Gear;
        }

        private readonly Dictionary<string, LootTable> _lootTables;

        /// <summary>Sous-lot 12.6 : charges de chaque objet de soin emporté, telles que retirées de l'inventaire jusqu'ici.</summary>
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

        /// <summary>Sous-lot 12.6 : fabrique une recette dans son atelier (lieu de la zone).</summary>
        public CraftRefusal Craft(string recipe, out CraftEvent? done)
        {
            Workshop workshop = Workshop ?? throw new InvalidOperationException("Partie sans métiers.");
            Recipe? r = workshop.Book.Recipes.FirstOrDefault(x => x.Id == recipe);
            return workshop.Craft(recipe, r != null && ((INarrativeWorld)this).InPlace(r.Place), Inventory, out done);
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
                Inventory.Remove(id, Math.Min(drunk, Inventory.Count(id)));
                _carried[id] = now;
            }
        }
        private readonly ItemCatalog _items;
        private readonly ZonePopulations? _populations;

        /// <summary>Sous-lot 12.4 : zone du monde de la partie (null pour une partie sur une simulation donnée).</summary>
        public ZoneDefinition? Definition { get; }

        /// <summary>Rayon de l'emprise d'un personnage qui entre par un point sûr (provisoire, celui des scénarios).</summary>
        public static readonly Fixed CharacterRadius = Fixed.Parse("0.5");

        /// <summary>
        /// Sous-lot 12.4 : partie dans une zone du monde. La session de zone part de sa simulation, passages ouverts retirés
        /// (<see cref="ZoneSetups"/>) ; le personnage entre au point sûr d'entrée ; les populations réapparaissent et tiennent leur laisse ;
        /// chaque créature donne le butin de sa population.
        /// </summary>
        /// <exception cref="ArgumentException">Sauvegarde d'une autre zone, sans personnage, ou incohérente avec le catalogue.</exception>
        /// <param name="story">Sous-lot 12.5 : histoire de la zone (PNJ, dialogues, quêtes) ; sans elle, l'état narratif sauvegardé est gardé tel quel.</param>
        /// <param name="crafting">Sous-lot 12.6 : métiers, recettes et nœuds de récolte ; sans eux, l'expérience des métiers sauvegardée est gardée.</param>
        /// <param name="safePoint">Sous-lot 12.8 : point sûr où le personnage entre (halte, reprise après une défaite) ; null : l'entrée de la zone.</param>
        public WorldSession(WorldSave save, ZoneDefinition zone, GameData data, CharacterProgression progression, ItemCatalog items, StoryBook? story = null,
            CraftingBook? crafting = null, ICombatLog? log = null, string? safePoint = null)
            : this(save ?? throw new ArgumentNullException(nameof(save)), ZoneSetups.For(zone ?? throw new ArgumentNullException(nameof(zone)), save.Consumed),
                data, progression, items, new CombatantPlacement(EntryOf(zone, safePoint).Position, CharacterRadius), null, log, CheckZone(save, zone))
        {
            Definition = zone;
            _populations = new ZonePopulations(zone);
            if (story != null) Narrative = new Narrative(story, _character.Story, this);
            if (crafting != null) Workshop = new Workshop(crafting, _character.Professions);
            // Sous-lot 12.7 : le chef arrive avec la session (sa première forme).
            if (zone.Boss is { } boss)
            {
                _boss = new BossFight(boss);
                _bossStart = _boss.Start(Zone.Engine);
            }
        }

        private readonly BossFight? _boss;
        private BossEvent? _bossStart;
        private bool _rewardPending, _rewardAnnounced, _defeated;

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

        // --- Monde vu par l'histoire (INarrativeWorld) ---

        void INarrativeWorld.GiveXp(long xp) => _character = _character.WithXp(Math.Min(SaveValidator.MaxXp, checked(_character.Xp + xp)));

        DayPhase INarrativeWorld.Phase => Zone.Time.Phase;

        GroundVector? INarrativeWorld.PlayerPosition
        {
            get
            {
                CombatantView me = Zone.Engine.View(_character.Id);
                return me.IsPresent && !me.IsDead ? me.Position : null;
            }
        }

        bool INarrativeWorld.InPlace(string place) =>
            Definition != null && ((INarrativeWorld)this).PlayerPosition is GroundVector p && Definition.PlacesAt(p).Any(x => x.Id == place);

        /// <summary>Point sûr d'entrée : celui demandé, ou l'entrée de la zone.</summary>
        private static SafePoint EntryOf(ZoneDefinition zone, string? safePoint) =>
            safePoint is null ? zone.Entry
            : zone.SafePoints.FirstOrDefault(p => p.Id == safePoint) ?? throw new ArgumentException($"Point sûr « {safePoint} » inconnu de {zone.Id}.", nameof(safePoint));

        private static bool CheckZone(WorldSave save, ZoneDefinition zone) =>
            save.Zone == zone.Id ? true : throw new ArgumentException($"Sauvegarde de la zone « {save.Zone} », pas de « {zone.Id} ».", nameof(save));

        /// <summary>Table de butin d'une créature : celle donnée à l'ouverture, sinon celle de sa population.</summary>
        private LootTable? LootFor(string combatant)
        {
            if (_lootTables.TryGetValue(combatant, out LootTable table)) return table;
            // Sous-lot 12.7 : la dernière forme du chef donne son butin.
            if (Definition?.Boss is { Loot: string bossLoot } b && b.Forms[b.Forms.Count - 1].Id == combatant && _items.TryGetLoot(bossLoot, out LootTable bossTable))
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

        /// <summary>Porte l'équipement de la place <paramref name="place"/>, au niveau actuel du personnage.</summary>
        public InventoryRefusal Equip(int place) => Inventory.Equip(place, Level);

        /// <summary>Simule un tick ; rend l'expérience et le butin gagnés à ce tick (créatures vaincues).</summary>
        public SessionStep Step()
        {
            IReadOnlyList<ZoneEvent> events = Zone.Step();
            SyncConsumables();
            IReadOnlyList<PopulationEvent> population = _populations?.Step(Zone) ?? Array.Empty<PopulationEvent>();
            CombatantView me = Zone.Engine.View(_character.Id);
            var gains = new List<ExperienceGained>();
            var loot = new List<LootGained>();
            var story = new List<StoryEvent>();
            foreach (ZoneEvent e in events)
            {
                if (e.Kind != ZoneEventKind.Death || !e.FirstTime || e.Combatant == _character.Id) continue;
                CombatantView creature = Zone.Engine.View(e.Combatant);
                if (creature.Kind != CombatantKind.Assailant || creature.MaxHealth is not long maxHealth) continue;
                // Le personnage doit être là, et vivant, quand la créature tombe (solo : il est seul à pouvoir l'avoir vaincue).
                if (!me.IsPresent || me.IsDead) continue;
                int before = Level;
                long xp = _progression.KillXp(maxHealth);
                _character = _character.WithXp(Math.Min(SaveValidator.MaxXp, checked(_character.Xp + xp)));
                gains.Add(new ExperienceGained(e, xp, before, Level));
                // Sous-lot 12.3 : butin de sa table, rangé en entier ou perdu (inventaire plein).
                if (LootFor(e.Combatant) is LootTable table)
                    foreach (LootRoll roll in Loot.Roll(table, e.Key))
                        loot.Add(new LootGained(e, roll.Item, roll.Count, Inventory.Add(roll.Item, roll.Count) == InventoryRefusal.None));
                // Sous-lot 12.5 : compte pour les objectifs « vaincre » de sa population.
                Narrative?.Defeated(_populations?.Of(e.Combatant)?.Creature ?? e.Combatant);
            }
            // Sous-lot 12.7 : le chef (formes, victoire, récompenses une seule fois) et la défaite.
            var boss = new List<BossEvent>();
            if (_bossStart != null) { boss.Add(_bossStart); _bossStart = null; }
            var rewards = new List<(string, int)>();
            if (_boss != null)
            {
                foreach (BossEvent b in _boss.Step(Zone.Engine))
                {
                    boss.Add(b);
                    // Victoire avec le personnage présent et vivant ; la première seulement est récompensée.
                    if (b.Kind == BossEventKind.Defeated && me.IsPresent && !me.IsDead && !Zone.IsConsumed(Definition!.VictoryKey)) _rewardPending = true;
                }
                if (_rewardPending) GiveBossRewards(rewards, story);
            }
            bool defeated = false;
            if (!_defeated && me.IsDead)
            {
                _defeated = true;
                defeated = true;
            }
            Narrative?.Step(story);
            return gains.Count == 0 && loot.Count == 0 && population.Count == 0 && story.Count == 0 && boss.Count == 0 && rewards.Count == 0 && !defeated
                ? SessionStep.Empty : new SessionStep(gains, loot, population, story, boss, rewards, defeated);
        }

        /// <summary>
        /// Récompenses de la première victoire, en entier ou pas du tout : si elles ne tiennent pas, elles attendent une place (annoncé une
        /// fois) ; données, la clé de victoire entre au registre du monde et le fait du chef est appris.
        /// </summary>
        private void GiveBossRewards(List<(string, int)> given, List<StoryEvent> story)
        {
            Boss b = Definition!.Boss!;
            Inventory trial = Inventory.Clone();
            foreach ((string item, int count) in b.Rewards)
                if (trial.Add(item, count) != InventoryRefusal.None)
                {
                    if (!_rewardAnnounced)
                    {
                        _rewardAnnounced = true;
                        story.Add(new StoryEvent(StoryEventKind.RewardBlocked, Definition.VictoryKey));
                    }
                    return;
                }
            Inventory.CopyFrom(trial);
            Zone.Consume(Definition.VictoryKey);
            given.AddRange(b.Rewards);
            if (Narrative != null) Narrative.Learn(b.Fact, story);
            else _pendingFacts.Add(b.Fact);
            _rewardPending = false;
        }

        /// <summary>Faits du chef appris sans histoire chargée : gardés pour la sauvegarde.</summary>
        private readonly List<string> _pendingFacts = new List<string>();

        /// <summary>Sauvegarde à ce tick : monde de la zone et personnage à jour.</summary>
        public WorldSave Save()
        {
            WorldSave world = Zone.Save();
            SavedCharacter character = Inventory.SaveInto(_character);
            if (Narrative != null) character = character.WithStory(Narrative.ToSaved());
            else if (_pendingFacts.Count > 0)
                character = character.WithStory(new SavedStory(character.Story.Facts.Concat(_pendingFacts), character.Story.Quests, character.Story.Trust,
                    character.Story.Discoveries));
            if (Workshop != null) character = character.WithProfessions(Workshop.Professions);
            return new WorldSave(world.Zone, world.WorldTick, world.Sessions, world.Consumed, character);
        }
    }
}
