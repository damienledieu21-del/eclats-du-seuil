using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;

namespace EdS.World
{
    public enum PopulationEventKind
    {
        /// <summary>Une nouvelle vie de la créature est arrivée à sa place (réapparition, ou retour au calme après la laisse).</summary>
        Respawned = 0,

        /// <summary>La créature a lâché sa cible, trop distancée : elle quitte la zone et une vie calme et soignée la remplace à sa place.</summary>
        Leashed = 1,

        /// <summary>Plus de place pour une nouvelle vie dans cette session (plafond des combattants) : elle reviendra à la session suivante.</summary>
        Exhausted = 2,
    }

    public sealed class PopulationEvent
    {
        public PopulationEventKind Kind { get; }

        /// <summary>Créature modèle (population).</summary>
        public string Population { get; }

        /// <summary>Vie concernée (combattant).</summary>
        public string Creature { get; }

        public long Tick { get; }

        internal PopulationEvent(PopulationEventKind kind, string population, string creature, long tick)
        {
            Kind = kind;
            Population = population;
            Creature = creature;
            Tick = tick;
        }
    }

    /// <summary>
    /// Sous-lot 12.4 (ARCHITECTURE_MONDE.md, section 2 : les identités ne sont jamais réutilisées) : vie des populations d'une session de
    /// zone. Chaque population suit la vie en cours de sa créature :
    /// <list type="bullet">
    /// <item><b>réapparition</b> : morte, elle revient après son délai, à sa place, sous une nouvelle identité (<c>loup1_v2</c>, <c>_v3</c>…),
    /// calme et soignée ; sa mort garde sa clé unique (butin et expérience une seule fois) ;</item>
    /// <item><b>laisse</b> (essai du 22 octobre 2026 : « il faut suffisamment s'éloigner pour que l'ennemi lâche ») : engagée, si l'écart entre
    /// les bords de son emprise et de sa cible dépasse sa laisse, elle quitte la zone (pas de mort : ni butin ni expérience) et une nouvelle
    /// vie calme et soignée la remplace à sa place au tick suivant ;</item>
    /// <item>une place occupée retarde l'arrivée d'une seconde ; au plafond des combattants de la session, la population attend la session
    /// suivante (<see cref="PopulationEventKind.Exhausted"/>).</item>
    /// </list>
    /// Tout se décide sur l'état du moteur, tick après tick : la même partie donne les mêmes vies.
    /// </summary>
    internal sealed class ZonePopulations
    {
        private sealed class Life
        {
            public Population Population = null!;
            public string Current = string.Empty;
            public int Number = 1;
            public long? RespawnAt;
            public bool Leashing;
            public bool Exhausted;
        }

        private readonly List<Life> _lives;
        private readonly Dictionary<string, Population> _byCreature = new Dictionary<string, Population>(StringComparer.Ordinal);

        public ZonePopulations(ZoneDefinition zone)
        {
            _lives = zone.Populations.Select(p => new Life { Population = p, Current = p.Creature }).ToList();
            foreach (Population p in zone.Populations) _byCreature[p.Creature] = p;
        }

        /// <summary>Population d'une vie (combattant), ou null.</summary>
        public Population? Of(string combatant) => _byCreature.TryGetValue(combatant, out Population p) ? p : null;

        /// <summary>Vie en cours d'une population.</summary>
        public string CurrentOf(string population) => _lives.First(l => l.Population.Creature == population).Current;

        /// <summary>Après un tick de la zone : laisse, morts et départs à remplacer, arrivées dues au tick suivant.</summary>
        /// <param name="reserved">Revue Codex de la PR #48 : places de combattants gardées pour d'autres arrivées (formes du chef encore à venir) :
        /// les populations ne les prennent pas.</param>
        public IReadOnlyList<PopulationEvent> Step(Zone zone, int reserved = 0)
        {
            CombatEngine engine = zone.Engine;
            if (engine.IsFinished) return Array.Empty<PopulationEvent>();
            long now = engine.CurrentTick;   // prochain tick simulé
            long tickMs = engine.TickMilliseconds;
            List<PopulationEvent>? events = null;
            foreach (Life life in _lives)
            {
                if (life.Exhausted) continue;
                if (life.RespawnAt is null)
                {
                    CombatantView v = engine.View(life.Current);
                    bool refused = !v.IsPresent && !v.HasLeft && !v.IsDead && v.ArrivalTick is long at && at < now;
                    if (v.IsDead) life.RespawnAt = checked(now - 1 + life.Population.RespawnMs / tickMs);
                    else if (v.HasLeft || refused) life.RespawnAt = now;   // laisse (ou arrivée refusée) : une vie calme la remplace aussitôt
                    else if (!life.Leashing && v.IsPresent && v.Engaged && v.CurrentTarget is string target && Beyond(v, engine.View(target), life.Population.LeashRange))
                    {
                        // Revue Codex de la PR #45 : un départ refusé par le pilote (plafond de commandes, refus atomique) ne fait pas échouer le
                        // tick ; la laisse est retentée au tick suivant.
                        if (TryLeave(zone, life.Current))
                        {
                            life.Leashing = true;
                            (events ??= new List<PopulationEvent>()).Add(new PopulationEvent(PopulationEventKind.Leashed, life.Population.Creature, life.Current, now));
                        }
                    }
                }
                if (life.RespawnAt is long due && due <= now)
                {
                    PopulationEvent? e = Respawn(zone, life, now, tickMs, reserved);
                    if (e != null) (events ??= new List<PopulationEvent>()).Add(e);
                }
            }
            return events ?? (IReadOnlyList<PopulationEvent>)Array.Empty<PopulationEvent>();
        }

        private PopulationEvent? Respawn(Zone zone, Life life, long now, long tickMs, int reserved)
        {
            CombatEngine engine = zone.Engine;
            AssailantSetup t = life.Population.Template;
            if (checked(engine.CombatantIds.Count + reserved) >= zone.Setup.Combat.MaxCombatants)
            {
                life.Exhausted = true;
                return new PopulationEvent(PopulationEventKind.Exhausted, t.Id, life.Current, now);
            }
            // Place occupée par un combattant présent : nouvel essai une seconde plus tard.
            if (Occupied(engine, t.Placement!))
            {
                life.RespawnAt = checked(now + Math.Max(1, 1000 / tickMs));
                return null;
            }
            // Revue Codex de la PR #45 : l'identité suivante est libre parmi tous les combattants du moteur (personnage, créature du scénario
            // hors population) : une identité prise est sautée, jamais retentée.
            var taken = new HashSet<string>(engine.CombatantIds, StringComparer.Ordinal);
            int number = checked(life.Number + 1);
            string id = $"{t.Id}_v{number}";
            while (taken.Contains(id)) id = $"{t.Id}_v{number = checked(number + 1)}";
            var setup = new AssailantSetup(id, t.Team, t.Armor, t.MagicResistance, t.Target, t.FirstAttackMs, t.IntervalMs, t.Attacks, t.Initiative,
                t.StatusResistance, t.Triggers, t.Health, t.Placement, checked(now * tickMs), t.Behavior);
            try
            {
                engine.Join(setup);
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                // Arrivée refusée (place prise, file pleine : refus atomique) : nouvel essai une seconde plus tard.
                life.RespawnAt = checked(now + Math.Max(1, 1000 / tickMs));
                return null;
            }
            life.Number = number;
            life.Current = id;
            life.RespawnAt = null;
            life.Leashing = false;
            _byCreature[id] = life.Population;
            return new PopulationEvent(PopulationEventKind.Respawned, t.Id, id, now);
        }

        private static bool TryLeave(Zone zone, string combatant)
        {
            try
            {
                zone.Leave(combatant);
                return true;
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                return false;
            }
        }

        private static bool Occupied(CombatEngine engine, CombatantPlacement place)
        {
            foreach (string id in engine.CombatantIds)
            {
                CombatantView v = engine.View(id);
                if (!v.IsPresent || v.IsDead || v.Position is not GroundVector p || v.Radius is not Fixed r) continue;
                if (!Beyond(p, r, place.Position, place.Radius, Fixed.Zero)) return true;
            }
            return false;
        }

        private static bool Beyond(CombatantView creature, CombatantView target, Fixed leash) =>
            target.IsPresent && !target.IsDead && creature.Position is GroundVector a && creature.Radius is Fixed ra && target.Position is GroundVector b
            && target.Radius is Fixed rb && Beyond(a, ra, b, rb, leash);

        /// <summary>Écart entre les bords des deux emprises strictement supérieur à <paramref name="gap"/> (calcul exact en valeurs brutes).</summary>
        internal static bool Beyond(GroundVector a, Fixed ra, GroundVector b, Fixed rb, Fixed gap)
        {
            long dx = checked(a.X.Raw - b.X.Raw), dz = checked(a.Z.Raw - b.Z.Raw);
            long reach = checked(gap.Raw + ra.Raw + rb.Raw);
            return checked(dx * dx + dz * dz) > checked(reach * reach);
        }
    }
}
