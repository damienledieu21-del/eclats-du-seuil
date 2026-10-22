using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Combat;

namespace EdS.Simulation.Engine
{
    /// <summary>
    /// Sous-lot 11.4c : attaques annoncées au sol et garde de face (COMBAT_ESPACE.md, sections 5 et 6, L11-D3 et L11-D7).
    /// <list type="bullet">
    /// <item><b>Forme</b> : une attaque d'assaillant peut avoir une forme (<see cref="AttackShape"/>). Coup dirigé : portée et ligne de vue
    /// au tour de l'assaillant, sinon <c>attackSkipped</c> (<c>targetOutOfRange</c>, <c>noLineOfSight</c>) ; la cible est ensuite verrouillée.
    /// Zone : sa position est verrouillée au tour de l'assaillant (positions du début du tick) et elle frappe, au tick d'impact, chaque
    /// ennemi de l'assaillant dont l'emprise la touche à ce moment-là (positions de la résolution, après les déplacements du tick).</item>
    /// <item><b>Annonce</b> : <c>telegraphMs</c> sépare le tour (<c>attackStarted</c>, qui décrit la forme et le tick d'impact) de l'impact
    /// (<c>attackLanded</c>, puis les dégâts) ; une zone persistante frappe encore toutes les <c>tickMs</c> pendant <c>persistentMs</c>. La
    /// mort de l'assaillant annule ce qui reste (<c>attackCancelled</c>) ; un coup dirigé dont la cible est morte aussi. Un contrôle posé
    /// pendant l'annonce ne l'interrompt pas.</item>
    /// <item><b>Bouclier</b> : garde et parade parfaite ne valent que contre un coup venu de l'arc frontal (<c>combat.guard.arcDegrees</c>,
    /// 180°) centré sur l'orientation du défenseur, et seulement si l'attaque les admet (<c>guardable</c>, <c>parryable</c>). Origine du
    /// coup : l'attaquant (coup dirigé, compétence, frappe de réaction), l'origine de la zone (l'assaillant pour un cône ou une bande, le
    /// centre pour un cercle). De dos : dégâts pleins (<c>defense: "behind"</c>).</item>
    /// <item><b>Garde orientée</b> : en garde, un personnage se tourne vers sa cible sélectionnée (commande <c>select</c>).</item>
    /// </list>
    /// Sans espace, rien de tout cela ne s'applique : une attaque historique (sans forme) se résout comme avant.
    /// </summary>
    public sealed partial class CombatEngine
    {
        /// <summary>Impact programmé d'une attaque annoncée, ou impulsion suivante d'une zone persistante.</summary>
        private sealed class AttackImpact : Scheduled
        {
            public Fighter Assailant = null!;
            public AssailantAttack Attack = null!;

            /// <summary>Coup dirigé : la cible verrouillée ; zone : null.</summary>
            public Fighter? Target;

            public AttackZone? Zone;

            /// <summary>Rang de l'impulsion (0 : l'impact) et tick du premier impact.</summary>
            public long Pulse;
            public long ImpactTick;

            /// <summary>Provenance : le tour de l'assaillant, causée par sa ligne <c>attackStarted</c>.</summary>
            public Provenance Origin = null!;
        }

        /// <summary>Attaques annoncées en cours (annonce ou zone persistante), dans l'ordre de leur annonce : vue du client (11.6).</summary>
        private readonly List<AttackImpact> _announced = new List<AttackImpact>();

        /// <summary>Défense applicable d'un impact sur une cible qui se défend ou non.</summary>
        private enum Defense
        {
            None,

            /// <summary>La cible se défend, mais l'attaque n'admet pas cette défense (imparable, zone non gardable) : « unblockable ».</summary>
            Ignored,

            /// <summary>La cible se défend, mais le coup vient de derrière son arc frontal : « behind », dégâts pleins.</summary>
            Behind,
            Parry,
            Guard,
        }

        /// <summary>
        /// Défense d'un impact : aucune si la cible (personnage vivant) ne garde ni ne pare ; ignorée si l'attaque n'admet aucune des défenses
        /// tenues ; de dos si l'origine du coup est hors de l'arc frontal (combat positionné) ; sinon la parade (prioritaire) ou la garde.
        /// </summary>
        private Defense DefenseOf(PendingAction action, Fighter target, bool parryOpen, bool guarding)
        {
            if (target.Character is null || target.Out || !(parryOpen || guarding)) return Defense.None;
            bool parry = parryOpen && action.Parryable, guard = guarding && action.Guardable;
            if (!parry && !guard) return Defense.Ignored;
            if (!FacesBlow(action, target)) return Defense.Behind;
            return parry ? Defense.Parry : Defense.Guard;
        }

        /// <summary>Vrai sans espace ; sinon, l'origine du coup est dans l'arc frontal de la cible (frontière comprise).</summary>
        private bool FacesBlow(PendingAction action, Fighter target)
        {
            if (!_spatial) return true;
            Body t = target.Body!;
            long fromX = action.FromX ?? action.Actor.Body!.X, fromZ = action.FromZ ?? action.Actor.Body!.Z;
            return Angles.WithinArc(t.FacingX, t.FacingZ, checked(fromX - t.X), checked(fromZ - t.Z), _rules.GuardArcDegrees);
        }

        /// <summary>Motif de saut d'un coup dirigé hors de portée ou sans ligne de vue (positions du début du tick) ; null sinon.</summary>
        private string? DirectedReach(Fighter assailant, AssailantAttack attack, Fighter? target)
        {
            if (attack.Shape != AttackShape.Target || target is null) return null;
            if (!InReach(assailant, target, attack.Range)) return "targetOutOfRange";
            if (!InSight(assailant, target)) return "noLineOfSight";
            return null;
        }

        /// <summary>L'attaque est-elle résolue par un impact programmé (annonce, ou zone) plutôt que dans la résolution du tour ?</summary>
        private static bool Deferred(AssailantAttack attack) => attack.Shape != null && (attack.IsZone || attack.TelegraphMs > 0);

        private long ImpactTickOf(long tick, AssailantAttack attack) => checked(tick + attack.TelegraphMs / _tickMs);

        /// <summary>
        /// Revue Codex de la PR #31 : l'attaque a-t-elle besoin de sa cible ? Oui pour un coup, un cône, une bande ou un cercle autour de la
        /// cible ; non pour un cercle (ou une zone qui reste) centré sur l'assaillant, qui part même si sa cible est morte.
        /// </summary>
        private static bool UsesTarget(AssailantAttack attack) =>
            !(attack.Shape is AttackShape.Circle or AttackShape.Persistent && attack.Center == ShapeCenter.Self);

        /// <summary>
        /// Zone verrouillée au tour de l'assaillant : origine, axe vers la cible (ou orientation de l'assaillant), dimensions. La cible
        /// est celle des attaques qui visent (provocation comprise) ; null seulement pour une zone centrée sur l'assaillant.
        /// </summary>
        private AttackZone LockZone(Fighter assailant, AssailantAttack attack, Fighter? target)
        {
            Body a = assailant.Body!;
            AttackShape shape = attack.Shape!.Value;
            if (shape is AttackShape.Circle or AttackShape.Persistent)
            {
                Body center = attack.Center == ShapeCenter.Self ? a : target!.Body!;
                return new AttackZone(shape, center.X, center.Z, 0, 0, attack.Radius!.Value.Raw);
            }
            Body t = target!.Body!;
            if (!Planar.TryNormalize(checked(t.X - a.X), checked(t.Z - a.Z), out long ux, out long uz))
            {
                ux = a.FacingX;
                uz = a.FacingZ;
            }
            return new AttackZone(shape, a.X, a.Z, ux, uz, attack.Range!.Value.Raw, attack.Angle ?? 0, (attack.Width?.Raw ?? 0) / 2);
        }

        /// <summary>Champs de forme de la ligne <c>attackStarted</c> d'une attaque qui a une forme.</summary>
        private void WriteForm(JsonLineWriter w, AssailantAttack attack, AttackZone? zone, long impactTick)
        {
            w.Field("shape", AttackShapeIds.Of(attack.Shape!.Value)).Field("telegraphMs", attack.TelegraphMs).Field("impactTick", impactTick)
                .Field("guardable", attack.Guardable).Field("parryable", attack.Parryable);
            if (zone is null)
            {
                if (attack.Range is { } range) w.Field("range", range);
                return;
            }
            WriteVector(w, "origin", zone.OriginX, zone.OriginZ);
            switch (zone.Shape)
            {
                case AttackShape.Cone:
                    WriteVector(w, "axis", zone.AxisX, zone.AxisZ);
                    w.Field("range", attack.Range!.Value).Field("angle", attack.Angle!.Value);
                    break;
                case AttackShape.Line:
                    WriteVector(w, "axis", zone.AxisX, zone.AxisZ);
                    w.Field("range", attack.Range!.Value).Field("width", attack.Width!.Value);
                    break;
                default:
                    w.Field("radius", attack.Radius!.Value);
                    if (zone.Shape == AttackShape.Persistent)
                        w.Field("pulses", attack.Pulses).Field("pulseTicks", attack.PulseMs / _tickMs);
                    break;
            }
        }

        /// <summary>Cibles d'un impact : la cible verrouillée d'un coup dirigé ; pour une zone, les ennemis vivants dont l'emprise la touche.</summary>
        private IReadOnlyList<Fighter> ImpactTargets(AttackImpact impact)
        {
            if (impact.Zone is not { } zone) return new[] { impact.Target! };
            Fighter assailant = impact.Assailant;
            return _fightersById.Where(f => f.Team != assailant.Team && !f.Out && zone.Touches(f.Body!.X, f.Body.Z, f.Body.Radius)).ToArray();
        }

        private PendingAction ImpactAction(AttackImpact impact) => new PendingAction
        {
            Actor = impact.Assailant,
            Attack = impact.Attack,
            Target = impact.Target,
            GeneratesResonance = false,
            Origin = impact.Origin,
            FromX = impact.Zone?.OriginX,
            FromZ = impact.Zone?.OriginZ,
        };

        /// <summary>Motif d'annulation d'un impact : assaillant mort, ou cible d'un coup dirigé morte ; null s'il frappe.</summary>
        private static string? CancelReason(AttackImpact impact)
        {
            if (impact.Assailant.Out) return OutReason(impact.Assailant, "actor");
            if (impact.Zone is null && impact.Target!.Out) return OutReason(impact.Target, "target");
            return null;
        }

        /// <summary>
        /// Impact d'une attaque annoncée (ou impulsion d'une zone persistante), en tête de la file de la phase de résolution : coût exact
        /// réservé (ligne <c>attackLanded</c>, impulsion suivante, tous les impacts et leurs applications), puis la ligne, puis les impacts,
        /// causés par elle. Une annulation écrit <c>attackCancelled</c> (diagnostic) et met fin à l'attaque.
        /// </summary>
        private void ProcessImpact(long tick, AttackImpact impact)
        {
            if (CancelReason(impact) is { } reason)
            {
                ReserveAndDequeue(Cost.Op + Cost.Line);
                Charge(1);
                _announced.Remove(impact);
                EmitDiagnostic(tick, CombatPhase.Resolution, "attackCancelled", w => w
                    .Field("actor", impact.Assailant.Id).Field("attack", impact.Attack.Id).Field("pulse", impact.Pulse).Field("reason", reason));
                return;
            }
            IReadOnlyList<Fighter> targets = ImpactTargets(impact);
            long next = checked(tick + impact.Attack.PulseMs / _tickMs);
            bool again = impact.Attack.Shape == AttackShape.Persistent && impact.Pulse + 1 < impact.Attack.Pulses && next < _durationTicks;
            PendingAction action = ImpactAction(impact);
            // Sous-lot 11.5 : une charge fonce le long de sa bande, sauf si un contrôle immobilise l'ennemi. Revue Codex de la PR #33 (tours 8
            // et 9) : l'immobilisation est lue après les coups et les applications de la bande (une application sur lui-même peut l'étourdir) ;
            // une charge immobilisée, déjà avant l'impact ou par lui, écrit chargeBlocked (diagnostic, avec la raison) au lieu de charged.
            bool charges = impact.Attack.Charge && impact.Zone != null;
            // Revue Codex de la PR #33 (tour 10) : la recherche du bout de la charge (sous-pas et contrôles de piège) est réservée avec
            // l'impact, deux unités de travail par sous-pas, et consommée même si la charge est bloquée.
            long chargeWork = charges ? checked(2 * ChargeSteps(impact.Assailant, impact.Zone!)) : 0;
            ResolveAction(tick, action, targets, Cost.Op + Cost.Line + Cost.Enqueue.If(again) + Cost.Line.If(charges) + chargeWork * Cost.Op, dequeue: true, () =>
            {
                Charge(1);
                long landed = Emit(tick, CombatPhase.Resolution, "attackLanded", impact.Origin, w =>
                {
                    w.Field("actor", impact.Assailant.Id).Field("attack", impact.Attack.Id).Field("pulse", impact.Pulse);
                    w.Names("targets", targets.Select(t => t.Id));
                });
                action.Origin = impact.Origin.CausedBy(landed);
                // La même attaque reste en file pour son impulsion suivante (et dans la liste des attaques annoncées), ou s'achève.
                if (again)
                {
                    impact.Pulse++;
                    Schedule(next, CombatPhase.Resolution, impact);
                }
                else _announced.Remove(impact);
            }, () =>
            {
                // Revue Codex de la PR #33 : la charge part après les coups de la bande (ligne réservée avec l'impact).
                if (!charges) return;
                Charge(chargeWork);
                if ((impact.Assailant.Out ? OutReason(impact.Assailant, "actor") : BlockReason(impact.Assailant, StatusBlocks.Movement)) is { } stopped)
                {
                    EmitDiagnostic(tick, CombatPhase.Resolution, "chargeBlocked", w => w
                        .Field("actor", impact.Assailant.Id).Field("attack", impact.Attack.Id).Field("reason", stopped));
                    return;
                }
                var (cx, cz) = ChargeEnd(impact.Assailant, impact.Zone!);
                Body body = impact.Assailant.Body!;
                body.X = cx;
                body.Z = cz;
                body.FacingX = impact.Zone!.AxisX;
                body.FacingZ = impact.Zone.AxisZ;
                EmitDiagnostic(tick, CombatPhase.Resolution, "charged", w =>
                {
                    w.Field("actor", impact.Assailant.Id).Field("attack", impact.Attack.Id);
                    WriteVector(w, "position", cx, cz);
                });
            });
        }

        /// <summary>
        /// Sous-lot 11.4c : attaques annoncées en cours, pour l'affichage (vues 1 et 2, réseau) : assaillant, attaque, forme verrouillée ou
        /// cible, tick d'impact et dernier tick d'effet. Lecture seule.
        /// </summary>
        public IReadOnlyList<AnnouncedAttack> Announced() => _announced.Where(a => !a.Assailant.Out).Select(a => new AnnouncedAttack(a.Assailant.Id, a.Attack.Id, a.Attack.Shape!.Value,
            a.Target?.Id, a.Zone, a.ImpactTick, checked(a.ImpactTick + (a.Attack.Pulses - 1) * (a.Attack.PulseMs / _tickMs)))).ToList();
    }

    /// <summary>Sous-lot 11.4c : attaque annoncée vue du client (forme verrouillée ou cible, tick d'impact, dernier tick d'effet).</summary>
    public sealed class AnnouncedAttack
    {
        public string Actor { get; }
        public string Attack { get; }
        public AttackShape Shape { get; }
        public string? Target { get; }
        public AttackZone? Zone { get; }
        public long ImpactTick { get; }
        public long LastTick { get; }

        public AnnouncedAttack(string actor, string attack, AttackShape shape, string? target, AttackZone? zone, long impactTick, long lastTick)
        {
            Actor = actor;
            Attack = attack;
            Shape = shape;
            Target = target;
            Zone = zone;
            ImpactTick = impactTick;
            LastTick = lastTick;
        }
    }
}
