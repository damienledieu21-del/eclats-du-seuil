using System;
using System.Collections.Generic;
using EdS.Data;
using EdS.Data.Characters;
using EdS.Data.Items;
using EdS.Data.Saves;
using EdS.Domain.Combat;
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

    /// <summary>Ce qu'un tick de la partie a rapporté au personnage.</summary>
    public sealed class SessionStep
    {
        public static readonly SessionStep Empty = new SessionStep(Array.Empty<ExperienceGained>(), Array.Empty<LootGained>());

        public IReadOnlyList<ExperienceGained> Experience { get; }
        public IReadOnlyList<LootGained> Loot { get; }
        public bool IsEmpty => Experience.Count == 0 && Loot.Count == 0;

        internal SessionStep(IReadOnlyList<ExperienceGained> experience, IReadOnlyList<LootGained> loot)
        {
            Experience = experience;
            Loot = loot;
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
    public sealed class WorldSession
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
        {
            if (save is null) throw new ArgumentNullException(nameof(save));
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            if (items is null) throw new ArgumentNullException(nameof(items));
            _character = save.Character ?? throw new ArgumentException("Sauvegarde sans personnage : le créer d'abord.", nameof(save));
            _lootTables = new Dictionary<string, LootTable>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> entry in lootTables ?? new Dictionary<string, string>())
                _lootTables[entry.Key] = items.TryGetLoot(entry.Value, out LootTable table) ? table
                    : throw new ArgumentException($"Table de butin « {entry.Value} » ({entry.Key}) absente du catalogue.", nameof(lootTables));
            Inventory = Inventory.From(_character, items, Level);
            CharacterSetup setup = PlayerCharacters.Setup(_character, progression, data, placement, gear: Inventory.Gear());
            // Revue Codex de la PR #43 : le personnage entre avec la session, dans sa simulation de départ (en-tête du journal compris).
            Zone = new Zone(save.Zone, zoneSetup, checked(save.Sessions + 1), save.WorldTick, save.Consumed, log, entering: new[] { setup });
            EnteredLevel = setup.Build.Level;
            EnteredGear = setup.Gear;
        }

        private readonly Dictionary<string, LootTable> _lootTables;

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
            if (events.Count == 0) return SessionStep.Empty;
            CombatantView me = Zone.Engine.View(_character.Id);
            var gains = new List<ExperienceGained>();
            var loot = new List<LootGained>();
            foreach (ZoneEvent e in events)
            {
                if (e.Kind != ZoneEventKind.Death || !e.FirstTime || e.Combatant == _character.Id) continue;
                CombatantView creature = Zone.Engine.View(e.Combatant);
                if (creature.Kind != CombatantKind.Assailant || creature.MaxHealth is not long maxHealth) continue;
                // Le personnage doit être là, et vivant, quand la créature tombe (solo : il est seul à pouvoir l'avoir vaincue).
                if (!me.IsPresent || me.IsDead) continue;
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
                if (_lootTables.TryGetValue(e.Combatant, out LootTable table))
                    foreach (LootRoll roll in Loot.Roll(table, e.Key))
                        loot.Add(new LootGained(e, roll.Item, roll.Count, Inventory.Add(roll.Item, roll.Count) == InventoryRefusal.None));
            }
            return gains.Count == 0 && loot.Count == 0 ? SessionStep.Empty : new SessionStep(gains, loot);
        }

        /// <summary>Sauvegarde à ce tick : monde de la zone et personnage à jour.</summary>
        public WorldSave Save()
        {
            WorldSave world = Zone.Save();
            return new WorldSave(world.Zone, world.WorldTick, world.Sessions, world.Consumed, Inventory.SaveInto(_character));
        }
    }
}
