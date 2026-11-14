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

        /// <summary>
        /// Décision de Damien du 14 novembre 2026 (« AAA », 1) : la forme du moment, qui avait lâché au bout de la marge de son secteur,
        /// revient à sa place avec tous ses PV (même forme).
        /// </summary>
        Returned = 2,

        /// <summary>
        /// Décision de Damien du 14 novembre 2026 (« AAA », 2) : la session de son secteur est renouvelée pendant le combat : la forme du
        /// moment y continue telle quelle (place et PV emportés).
        /// </summary>
        Carried = 3,
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
        private bool _returning;

        // Revue Codex de la PR #159 (1er tour) : état emporté au renouvellement de la session (place, PV, ressources), gardé jusqu'à l'arrivée
        // effective de la forme et réutilisé à chaque nouvel essai ; l'événement Carried n'est rendu qu'à cette arrivée.
        private CombatantPlacement? _carryPlace;
        private CarriedState? _carryState;

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

        public IReadOnlyList<BossEvent> Step(CombatEngine engine) => Step(engine, null);

        /// <summary>Sous-lot V2.28b (2c) : un pas dans la session <paramref name="zone"/> (règle de place libre pour une forme emportée).</summary>
        public IReadOnlyList<BossEvent> Step(Zone zone) => Step((zone ?? throw new ArgumentNullException(nameof(zone))).Engine, zone);

        private IReadOnlyList<BossEvent> Step(CombatEngine engine, Zone? zone)
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
                // Décision de Damien du 14 novembre 2026 (« AAA », 1) : partie sans mourir (lâchée au bout de la marge de son secteur par
                // l'hôte) : la même forme revient à sa place, tous ses PV, sous une identité nouvelle.
                else if (!v.IsPresent && !v.IsDead && v.HasLeft)
                {
                    _nextAt = now;
                    _returning = true;
                    _carryPlace = null;   // la règle du retour à sa place, tous ses PV, l'emporte
                    _carryState = null;
                }
                // Revue Codex de la PR #159 (1er tour) : la forme emportée est arrivée pour de bon : le combat est emporté.
                else if (_carryPlace != null && v.IsPresent && !v.IsDead)
                {
                    _carryPlace = null;
                    _carryState = null;
                    return new[] { new BossEvent(BossEventKind.Carried, _boss.Id, _current, _phase, now) };
                }
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
            // Revue Codex de la PR #159 (1er tour) : une forme emportée qui n'est pas encore arrivée réessaie avec son état emporté, à la
            // place libre la plus proche de la sienne (règle d'arrivée du joueur) ; les autres, à leur place de départ.
            CombatantPlacement place = form.Placement!;
            if (_carryPlace is CombatantPlacement carried)
            {
                GroundVector? spot = zone != null ? SectorWorld.FreeSpotOf(zone, carried.Position, carried.Radius)
                    : Occupied(engine, carried, now) ? (GroundVector?)null : carried.Position;
                if (spot is not GroundVector free)
                {
                    _nextAt = checked(now + Math.Max(1, 1000 / engine.TickMilliseconds));
                    return Array.Empty<BossEvent>();
                }
                place = new CombatantPlacement(free, carried.Radius, carried.Facing);
            }
            else if (Occupied(engine, place, now))
            {
                _nextAt = checked(now + Math.Max(1, 1000 / engine.TickMilliseconds));
                return Array.Empty<BossEvent>();
            }
            string id = FreeIdentity(engine, form.Id);
            try
            {
                engine.Join(Copy(form, id, engine, place, _carryPlace != null ? _carryState : null));
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                // Arrivée refusée (place prise, file pleine : refus atomique) : nouvel essai une seconde plus tard (comme les populations).
                _nextAt = checked(now + Math.Max(1, 1000 / engine.TickMilliseconds));
                return Array.Empty<BossEvent>();
            }
            _current = id;
            _nextAt = null;
            if (_carryPlace != null) return Array.Empty<BossEvent>();   // l'événement Carried vient à l'arrivée effective
            BossEventKind kind = _returning ? BossEventKind.Returned : BossEventKind.FormArrived;
            _returning = false;
            return new[] { new BossEvent(kind, _boss.Id, id, _phase, now) };
        }

        /// <summary>
        /// Décision de Damien du 14 novembre 2026 (« AAA », 2) : le combat continue dans la session renouvelée <paramref name="to"/> de son
        /// secteur, tel qu'il était dans <paramref name="from"/> : même forme, même phase ; la forme du moment, présente et vivante, y arrive
        /// à sa place (ou la place libre la plus proche, règle d'arrivée du joueur) avec son état emporté (PV, ressources) ; sinon (morte, en
        /// attente, partie), la forme à venir arrive au premier pas, à sa place. Un chef vaincu le reste (aucune forme, aucune seconde
        /// victoire). Revue Codex de la PR #159 (1er tour) : l'état emporté est gardé jusqu'à l'arrivée effective ; une arrivée refusée
        /// (place prise au moment d'arriver, aucune place libre à 8 m, refus du moteur) est retentée à chaque pas avec le même état ;
        /// l'événement <see cref="BossEventKind.Carried"/> n'est rendu qu'à l'arrivée. Ne lève pas.
        /// </summary>
        public static BossFight Continue(BossFight old, CombatEngine from, Zone to)
        {
            if (old is null) throw new ArgumentNullException(nameof(old));
            if (from is null) throw new ArgumentNullException(nameof(from));
            if (to is null) throw new ArgumentNullException(nameof(to));
            var fight = new BossFight(old._boss) { RewardPending = old.RewardPending, RewardAnnounced = old.RewardAnnounced, _phase = old._phase, _over = old._over };
            fight._current = old._current;
            if (old._over) return fight;
            CombatantView? v = System.Linq.Enumerable.Contains(from.CombatantIds, old._current) ? from.View(old._current) : null;
            if (old._nextAt is null && v is { IsPresent: true, IsDead: false, Position: GroundVector at, Radius: Fixed radius })
            {
                // Revue Codex de la PR #159 (1er tour) : l'état emporté est gardé dans le combat jusqu'à l'arrivée effective.
                CarriedState carried = from.Carried(old._current);
                fight._carryPlace = new CombatantPlacement(at, radius, v.Facing);
                fight._carryState = carried.IsEmpty ? null : carried;
            }
            else if (old._carryPlace != null)
            {
                fight._carryPlace = old._carryPlace;   // une forme emportée pas encore arrivée l'est de nouveau
                fight._carryState = old._carryState;
            }
            else fight._returning = old._nextAt is null;   // la même forme qui revient (sans état à emporter), pas une forme nouvelle
            fight._nextAt = to.Engine.CurrentTick;
            // Arrivée tout de suite, avant le premier tick de la session nouvelle, si la place (ou la plus proche) est libre ; sinon, ou si le
            // moteur refuse, nouvel essai à chaque pas avec le même état.
            if (fight._carryPlace != null) fight.Step(to.Engine, to);
            return fight;
        }

        private static string FreeIdentity(CombatEngine engine, string form)
        {
            var taken = new HashSet<string>(engine.CombatantIds, StringComparer.Ordinal);
            if (!taken.Contains(form)) return form;
            int n = 2;
            while (taken.Contains($"{form}_v{n}")) n = checked(n + 1);
            return $"{form}_v{n}";
        }

        private static AssailantSetup Copy(AssailantSetup t, string id, CombatEngine engine, CombatantPlacement? placement = null, CarriedState? carried = null) =>
            new AssailantSetup(id, t.Team, t.Armor, t.MagicResistance, t.Target, t.FirstAttackMs, t.IntervalMs, t.Attacks, t.Initiative, t.StatusResistance,
                t.Triggers, t.Health, placement ?? t.Placement, checked(engine.CurrentTick * engine.TickMilliseconds), t.Behavior, carried);

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
