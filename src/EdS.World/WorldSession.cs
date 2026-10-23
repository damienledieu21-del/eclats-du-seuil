using System;
using System.Collections.Generic;
using EdS.Data;
using EdS.Data.Characters;
using EdS.Data.Saves;
using EdS.Domain.Combat;
using EdS.Domain.Space;
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

    /// <summary>
    /// Sous-lot 12.2 : une partie en solo. Elle reprend la sauvegarde (temps du monde, registre, personnage), ouvre une session de la zone
    /// (session suivante), y fait entrer le personnage et lui donne l'expérience des créatures vaincues : à chaque mort de créature consommée
    /// pour la première fois pendant que le personnage est présent et vivant, <see cref="CharacterProgression.KillXp"/> de ses PV maximaux.
    /// Un niveau gagné prend effet à la prochaine entrée dans une zone (le combat en cours garde le build d'entrée). Rien d'autre que la
    /// sauvegarde ne survit à la session.
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
        public WorldSession(WorldSave save, CombatSetup zoneSetup, GameData data, CharacterProgression progression, CombatantPlacement placement,
            ICombatLog? log = null)
        {
            if (save is null) throw new ArgumentNullException(nameof(save));
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _character = save.Character ?? throw new ArgumentException("Sauvegarde sans personnage : le créer d'abord.", nameof(save));
            CharacterSetup setup = PlayerCharacters.Setup(_character, progression, data, placement);
            Zone = new Zone(save.Zone, zoneSetup, checked(save.Sessions + 1), save.WorldTick, save.Consumed, log);
            Zone.Enter(setup);
            EnteredLevel = setup.Build.Level;
        }

        /// <summary>Simule un tick ; rend l'expérience gagnée à ce tick (une entrée par créature vaincue).</summary>
        public IReadOnlyList<ExperienceGained> Step()
        {
            IReadOnlyList<ZoneEvent> events = Zone.Step();
            if (events.Count == 0) return Array.Empty<ExperienceGained>();
            CombatantView me = Zone.Engine.View(_character.Id);
            var gains = new List<ExperienceGained>();
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
            }
            return gains;
        }

        /// <summary>Sauvegarde à ce tick : monde de la zone et personnage à jour.</summary>
        public WorldSave Save()
        {
            WorldSave world = Zone.Save();
            return new WorldSave(world.Zone, world.WorldTick, world.Sessions, world.Consumed, _character);
        }
    }
}
