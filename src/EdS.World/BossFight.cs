using System;
using System.Collections.Generic;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;

namespace EdS.World
{
    public enum BossEventKind
    {
        /// <summary>Une forme du chef est arrivée (la première au début de la session, les suivantes quand la précédente tombe).</summary>
        FormArrived = 0,

        /// <summary>La dernière forme est tombée : victoire.</summary>
        Defeated = 1,
    }

    public sealed class BossEvent
    {
        public BossEventKind Kind { get; }
        public string Boss { get; }

        /// <summary>Forme concernée (combattant).</summary>
        public string Form { get; }

        /// <summary>Rang de la forme (0 pour la première).</summary>
        public int Phase { get; }

        public long Tick { get; }

        internal BossEvent(BossEventKind kind, string boss, string form, int phase, long tick)
        {
            Kind = kind;
            Boss = boss;
            Form = form;
            Phase = phase;
            Tick = tick;
        }
    }

    /// <summary>
    /// Sous-lot 12.7 : le chef de la zone dans une session. Sa première forme arrive au début de la session ; quand une forme tombe, la
    /// suivante se relève à sa place (une seconde plus tard si la place est occupée) ; quand la dernière tombe, c'est la victoire. Le chef ne
    /// revient qu'à la session suivante. Chaque forme est une identité distincte (sa mort a sa propre clé).
    /// </summary>
    internal sealed class BossFight
    {
        private readonly Boss _boss;
        private int _phase;
        private long? _nextAt;
        private bool _over;

        public BossFight(Boss boss)
        {
            _boss = boss ?? throw new ArgumentNullException(nameof(boss));
        }

        public string CurrentForm => _boss.Forms[_phase].Id;
        public int Phase => _phase;
        public bool IsDefeated => _over;

        /// <summary>Revue Codex de la PR #48 : formes encore à venir (places de combattants à leur garder).</summary>
        public int FormsToCome => _over ? 0 : _boss.Forms.Count - 1 - _phase;

        /// <summary>Fait arriver la première forme (début de session).</summary>
        public BossEvent Start(CombatEngine engine)
        {
            engine.Join(Copy(_boss.Forms[0], engine));
            return new BossEvent(BossEventKind.FormArrived, _boss.Id, _boss.Forms[0].Id, 0, engine.CurrentTick);
        }

        public IReadOnlyList<BossEvent> Step(CombatEngine engine)
        {
            if (_over) return Array.Empty<BossEvent>();
            long now = engine.CurrentTick;
            // Revue Codex de la PR #48 : la mort de la dernière forme compte encore au tick qui termine la session ; seules les arrivées
            // demandent un combat en cours.
            if (_nextAt is null)
            {
                if (!engine.View(CurrentForm).IsDead) return Array.Empty<BossEvent>();
                if (_phase + 1 == _boss.Forms.Count)
                {
                    _over = true;
                    return new[] { new BossEvent(BossEventKind.Defeated, _boss.Id, CurrentForm, _phase, now) };
                }
                _nextAt = now;
            }
            if (engine.IsFinished || _nextAt > now) return Array.Empty<BossEvent>();
            AssailantSetup next = _boss.Forms[_phase + 1];
            if (Occupied(engine, next.Placement!))
            {
                _nextAt = checked(now + Math.Max(1, 1000 / engine.TickMilliseconds));
                return Array.Empty<BossEvent>();
            }
            try
            {
                engine.Join(Copy(next, engine));
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                // Arrivée refusée (place prise, file pleine : refus atomique) : nouvel essai une seconde plus tard (comme les populations).
                _nextAt = checked(now + Math.Max(1, 1000 / engine.TickMilliseconds));
                return Array.Empty<BossEvent>();
            }
            _phase++;
            _nextAt = null;
            return new[] { new BossEvent(BossEventKind.FormArrived, _boss.Id, next.Id, _phase, now) };
        }

        private static AssailantSetup Copy(AssailantSetup t, CombatEngine engine) =>
            new AssailantSetup(t.Id, t.Team, t.Armor, t.MagicResistance, t.Target, t.FirstAttackMs, t.IntervalMs, t.Attacks, t.Initiative, t.StatusResistance,
                t.Triggers, t.Health, t.Placement, checked(engine.CurrentTick * engine.TickMilliseconds), t.Behavior);

        private static bool Occupied(CombatEngine engine, CombatantPlacement place)
        {
            foreach (string id in engine.CombatantIds)
            {
                CombatantView v = engine.View(id);
                if (!v.IsPresent || v.IsDead || v.Position is not GroundVector p || v.Radius is not Fixed r) continue;
                if (!ZonePopulations.Beyond(p, r, place.Position, place.Radius, Fixed.Zero)) return true;
            }
            return false;
        }
    }
}
