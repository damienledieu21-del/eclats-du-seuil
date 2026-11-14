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

        /// <summary>Identité de la forme en cours dans le moteur (celle de la forme, ou <c>{forme}_v{n}</c> après une arrivée refusée).</summary>
        private string _current;

        public BossFight(Boss boss)
        {
            _boss = boss ?? throw new ArgumentNullException(nameof(boss));
            _current = boss.Forms[0].Id;
        }

        public string CurrentForm => _current;

        /// <summary>Sous-lot V1.6 : chef de ce combat.</summary>
        public Boss Boss => _boss;

        /// <summary>Sous-lot V1.6 : récompenses de la première victoire à donner (une place a manqué), et refus déjà annoncé.</summary>
        public bool RewardPending { get; set; }

        public bool RewardAnnounced { get; set; }

        /// <summary>
        /// Sous-lot V2.28b (2c) : secteur du chef sur l'hôte des secteurs (celui de sa première forme) ; null hors de l'hôte.
        /// </summary>
        public string? Sector { get; set; }

        /// <summary>
        /// Sous-lot V2.28b (2c) : session de zone où se joue ce combat (celle du personnage hors de l'hôte, celle du secteur du chef sur
        /// l'hôte). Le combat ne vit qu'avec elle : une session nouvelle de ce secteur (ouverture, réouverture, renouvellement) en commence un
        /// autre, à la première forme, comme une halte.
        /// </summary>
        public Zone? Session { get; set; }
        public int Phase => _phase;
        public bool IsDefeated => _over;

        /// <summary>Revue Codex de la PR #48 : formes encore à venir (places de combattants à leur garder).</summary>
        public int FormsToCome => _over ? 0 : _boss.Forms.Count - 1 - _phase + (_nextAt != null ? 1 : 0);

        /// <summary>Le combattant est la dernière forme du chef (son butin).</summary>
        public bool IsFinalForm(string combatant) => _phase == _boss.Forms.Count - 1 && combatant == _current;

        /// <summary>Fait arriver la première forme (début de session).</summary>
        public BossEvent Start(CombatEngine engine)
        {
            engine.Join(Copy(_boss.Forms[0], _current, engine));
            return new BossEvent(BossEventKind.FormArrived, _boss.Id, _current, 0, engine.CurrentTick);
        }

        public IReadOnlyList<BossEvent> Step(CombatEngine engine)
        {
            if (_over) return Array.Empty<BossEvent>();
            long now = engine.CurrentTick;
            // Revue Codex de la PR #48 : la mort de la dernière forme compte encore au tick qui termine la session ; seules les arrivées
            // demandent un combat en cours.
            if (_nextAt is null)
            {
                CombatantView v = engine.View(_current);
                // Revue Codex de la PR #48 (3e tour) : une arrivée refusée par le moteur (place prise au moment d'arriver) n'est pas une forme
                // en jeu : elle est retentée sous une nouvelle identité, la même forme, une seconde plus tard.
                if (!v.IsPresent && !v.IsDead && !v.HasLeft && v.ArrivalTick is long at && at < now)
                    _nextAt = checked(now + Math.Max(1, 1000 / engine.TickMilliseconds));
                else
                {
                    if (!v.IsDead) return Array.Empty<BossEvent>();
                    if (_phase + 1 == _boss.Forms.Count)
                    {
                        _over = true;
                        return new[] { new BossEvent(BossEventKind.Defeated, _boss.Id, _current, _phase, now) };
                    }
                    _phase++;
                    _current = _boss.Forms[_phase].Id;
                    _nextAt = now;
                }
            }
            if (engine.IsFinished || _nextAt > now) return Array.Empty<BossEvent>();
            AssailantSetup form = _boss.Forms[_phase];
            if (Occupied(engine, form.Placement!, now))
            {
                _nextAt = checked(now + Math.Max(1, 1000 / engine.TickMilliseconds));
                return Array.Empty<BossEvent>();
            }
            string id = FreeIdentity(engine, form.Id);
            try
            {
                engine.Join(Copy(form, id, engine));
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                // Arrivée refusée (place prise, file pleine : refus atomique) : nouvel essai une seconde plus tard (comme les populations).
                _nextAt = checked(now + Math.Max(1, 1000 / engine.TickMilliseconds));
                return Array.Empty<BossEvent>();
            }
            _current = id;
            _nextAt = null;
            return new[] { new BossEvent(BossEventKind.FormArrived, _boss.Id, id, _phase, now) };
        }

        /// <summary>L'identité de la forme si elle est libre, sinon la première <c>{forme}_v{n}</c> libre (une identité n'est jamais réutilisée).</summary>
        private static string FreeIdentity(CombatEngine engine, string form)
        {
            var taken = new HashSet<string>(engine.CombatantIds, StringComparer.Ordinal);
            if (!taken.Contains(form)) return form;
            int n = 2;
            while (taken.Contains($"{form}_v{n}")) n = checked(n + 1);
            return $"{form}_v{n}";
        }

        private static AssailantSetup Copy(AssailantSetup t, string id, CombatEngine engine) =>
            new AssailantSetup(id, t.Team, t.Armor, t.MagicResistance, t.Target, t.FirstAttackMs, t.IntervalMs, t.Attacks, t.Initiative, t.StatusResistance,
                t.Triggers, t.Health, t.Placement, checked(engine.CurrentTick * engine.TickMilliseconds), t.Behavior);

        /// <summary>
        /// Place prise par un combattant présent, ou par un combattant qui arrive à ce même tick (revue Codex de la PR #48 : une réapparition
        /// programmée juste avant passerait d'abord et la forme serait refusée).
        /// </summary>
        private static bool Occupied(CombatEngine engine, CombatantPlacement place, long now)
        {
            foreach (string id in engine.CombatantIds)
            {
                CombatantView v = engine.View(id);
                bool arriving = !v.IsPresent && !v.IsDead && !v.HasLeft && v.ArrivalTick == now;
                if ((!v.IsPresent && !arriving) || v.IsDead || v.Position is not GroundVector p || v.Radius is not Fixed r) continue;
                if (!ZonePopulations.Beyond(p, r, place.Position, place.Radius, Fixed.Zero)) return true;
            }
            return false;
        }
    }
}
