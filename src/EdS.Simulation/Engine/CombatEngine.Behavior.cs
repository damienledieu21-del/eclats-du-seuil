using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Space;

namespace EdS.Simulation.Engine
{
    /// <summary>
    /// Sous-lot 11.5 : comportements des ennemis dans un combat positionné (PLAN_LOT_11.md, 11.5), en données (<see cref="AssailantBehavior"/>).
    /// <list type="bullet">
    /// <item><b>Cible</b> : provocation, sinon sa cible de scénario présente et vivante, sinon l'ennemi présent vivant le plus proche
    /// (distance entre les bords, puis identifiant).</item>
    /// <item><b>Engagement</b> (phase des commandes, avant les tours) : avec <c>aggroRange</c>, l'ennemi attend qu'un ennemi vivant entre à
    /// cette distance, qu'on le frappe, qu'on le provoque, ou qu'un allié de sa meute s'engage à cette distance (<c>engaged</c>) ; jusque-là,
    /// il ne bouge pas et saute ses attaques (<c>notEngaged</c>).</item>
    /// <item><b>Placement</b> (phase de déplacement, positions du début de la phase) : il approche si sa cible est au-delà de <c>maxRange</c>,
    /// recule en deçà de <c>minRange</c>, s'arrête entre les deux et se tourne vers elle ; avec <c>encircle</c>, la meute qui vise la même
    /// cible se répartit autour d'elle, à parts égales, dans l'ordre où elle l'entoure déjà. Il s'arrête pendant l'annonce d'une de ses
    /// attaques. Le décor et les autres emprises le font glisser comme une marche ordinaire ; il n'y a pas de recherche de chemin.</item>
    /// <item><b>Charge</b> (<see cref="AssailantAttack.Charge"/>) : à l'impact de la bande, après ses coups, l'ennemi fonce depuis sa place
    /// vers le bout de la bande verrouillée, ou jusqu'au premier obstacle ou à la première emprise solide (<c>charged</c>), sans laisser un
    /// joueur sans issue (règle A).</item>
    /// </list>
    /// </summary>
    public sealed partial class CombatEngine
    {
        /// <summary>Un combattant du combat (présent ou attendu) a-t-il un comportement ? (une arrivée pilotée peut en ajouter un)</summary>
        private bool HasBehaviors => _fighters.Any(f => f.Behavior != null);

        // --- Cible ---------------------------------------------------------------------------------------------------------------

        /// <summary>Distance entre les bords de deux emprises (unités brutes, par défaut), négative si elles se chevauchent.</summary>
        private static long EdgeDistance(Body a, Body b) =>
            checked(Domain.Numerics.IntMath.Sqrt(Planar.LengthSquared(checked(b.X - a.X), checked(b.Z - a.Z))) - a.Radius - b.Radius);

        /// <summary>Ennemi présent et vivant le plus proche (bords), puis par identifiant ; null s'il n'y en a pas.</summary>
        private Fighter? Nearest(Fighter assailant)
        {
            Fighter? best = null;
            long bestDistance = 0;
            foreach (Fighter f in _fightersById)
            {
                if (f.Out || f.Team == assailant.Team || f.Body is null) continue;
                long d = EdgeDistance(assailant.Body!, f.Body);
                if (best is null || d < bestDistance)
                {
                    best = f;
                    bestDistance = d;
                }
            }
            return best;
        }

        /// <summary>
        /// Motif de saut d'une attaque d'un ennemi qui a un comportement : pas encore engagé (<c>notEngaged</c>), ou aucune cible pour une
        /// attaque qui en a besoin (<c>noTarget</c>) ; null sinon (et pour un ennemi sans comportement).
        /// </summary>
        private static string? IdleReason(Fighter assailant, AssailantAttack attack, Fighter? target)
        {
            if (assailant.Behavior is null) return null;
            if (!assailant.Engaged) return "notEngaged";
            if (target is null && attack.Targeting == SkillTargeting.Enemy && UsesTarget(attack)) return "noTarget";
            return null;
        }

        // --- Engagement ----------------------------------------------------------------------------------------------------------

        private void RunEngagements(long tick)
        {
            if (!HasBehaviors) return;
            foreach (Fighter f in _fightersById)
            {
                if (f.Behavior is not { } behavior || f.Engaged || f.Out) continue;
                string? reason = EngageReason(f, behavior);
                Fighter? target = reason is null ? null : SingleTarget(f);
                Reserve(Cost.Op + Cost.Line.If(reason != null));
                Charge(1);
                if (reason is null) continue;
                f.Engaged = true;
                EmitDiagnostic(tick, CombatPhase.Commands, "engaged", w => w.Field("actor", f.Id).Field("target", target?.Id).Field("reason", reason));
            }
        }

        /// <summary>
        /// Frappé (<c>hit</c>), provoqué (<c>taunt</c>), un ennemi vivant à distance d'engagement (<c>aggro</c>), ou un allié à comportement
        /// engagé à cette distance (<c>pack</c>) ; null sinon.
        /// </summary>
        private string? EngageReason(Fighter f, AssailantBehavior behavior)
        {
            if (f.Struck || f.Result.DamageTaken > 0) return "hit";
            foreach (StatusInstance status in f.Statuses.Values)
                if (status.Definition.IsTaunt) return "taunt";
            long aggro = behavior.AggroRange!.Value.Raw;
            foreach (Fighter other in _fightersById)
                if (!other.Out && other.Team != f.Team && other.Body != null && EdgeDistance(f.Body!, other.Body) <= aggro) return "aggro";
            // La meute : un allié à comportement déjà engagé, à distance d'engagement, l'entraîne (ordre des identifiants, de proche en proche).
            foreach (Fighter ally in _fightersById)
                if (ally != f && !ally.Out && ally.Team == f.Team && ally.Behavior != null && ally.Engaged && EdgeDistance(f.Body!, ally.Body!) <= aggro) return "pack";
            return null;
        }

        // --- Placement -----------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Intention de marche d'un ennemi à comportement pour ce tick : marche (direction unitaire) ou arrêt, cible vers laquelle se tourner
        /// et, revue Codex de la PR #33, longueur de pas au plus (la frontière de sa bande de distance : pas de dépassement).
        /// </summary>
        private readonly struct Intention
        {
            public readonly bool Moving;
            public readonly long DirectionX, DirectionZ;
            public readonly int Face;   // indice de la cible vers laquelle se tourner à l'arrêt ; −1 sinon
            public readonly long MaxStep;   // unités brutes ; long.MaxValue : pas de borne

            public Intention(bool moving, long directionX, long directionZ, int face, long maxStep = long.MaxValue)
            {
                Moving = moving;
                DirectionX = directionX;
                DirectionZ = directionZ;
                Face = face;
                MaxStep = maxStep;
            }

            public static readonly Intention Hold = new Intention(false, 0, 0, -1);
        }

        /// <summary>
        /// Intentions des ennemis à comportement (indices de <see cref="_fightersById"/>), calculées sans rien modifier d'après les positions
        /// du début de la phase ; null pour un combattant sans comportement (il garde la sienne).
        /// </summary>
        /// <summary>Tolérance autour de la bande de distance d'un ennemi (2 mm, en unités brutes) : les arrondis d'un pas borné y restent.</summary>
        private const long BandTolerance = 20;

        private Intention?[] Intentions(long[] x, long[] z)
        {
            int n = _fightersById.Count;
            var intentions = new Intention?[n];
            if (!HasBehaviors) return intentions;
            var targets = new int[n];
            var announcing = new HashSet<Fighter>(_announced.Where(a => a.Pulse == 0).Select(a => a.Assailant));
            for (int i = 0; i < n; i++)
            {
                Fighter f = _fightersById[i];
                targets[i] = -1;
                if (f.Behavior is null || f.Out) continue;
                if (f.Engaged && SingleTarget(f) is { Out: false } t) targets[i] = _fightersById.IndexOf(t);
            }
            for (int i = 0; i < n; i++)
            {
                Fighter f = _fightersById[i];
                if (f.Behavior is not { } behavior || f.Out) continue;
                int j = targets[i];
                if (j < 0 || announcing.Contains(f))
                {
                    intentions[i] = j < 0 ? Intention.Hold : new Intention(false, 0, 0, j);
                    continue;
                }
                long rr = checked(f.Body!.Radius + _fightersById[j].Body!.Radius);
                long vx = checked(x[j] - x[i]), vz = checked(z[j] - z[i]);
                long gx, gz;   // point visé
                long maxStep = long.MaxValue;
                if (behavior.Encircle)
                {
                    var (px, pz) = RingSlot(i, j, targets, x, z, behavior);
                    gx = checked(px - x[i]);
                    gz = checked(pz - z[i]);
                    long tolerance = Math.Max(1, Domain.Numerics.IntMath.MulDiv(f.Body.Speed, _tickMs, 1000, Domain.Numerics.RoundingMode.Floor));
                    if (Planar.LengthSquared(gx, gz) <= checked(tolerance * tolerance))
                    {
                        intentions[i] = new Intention(false, 0, 0, j);
                        continue;
                    }
                }
                else
                {
                    // Revue Codex de la PR #33 : le pas s'arrête à la frontière de la bande [minRange, maxRange] (distance entre les centres
                    // arrondie par défaut), avec une tolérance de BandTolerance de part et d'autre : une bande plus étroite qu'un pas ne fait
                    // plus osciller l'ennemi d'un côté à l'autre.
                    long d = Domain.Numerics.IntMath.Sqrt(Planar.LengthSquared(vx, vz));
                    long far = checked(rr + behavior.MaxRange.Raw), near = checked(rr + behavior.MinRange.Raw);
                    if (d > checked(far + BandTolerance))
                    {
                        (gx, gz) = (vx, vz);
                        maxStep = checked(d - far);
                    }
                    else if (d < checked(near - BandTolerance))
                    {
                        (gx, gz) = (checked(-vx), checked(-vz));
                        maxStep = checked(near - d);
                    }
                    else
                    {
                        intentions[i] = new Intention(false, 0, 0, j);
                        continue;
                    }
                }
                intentions[i] = Planar.TryNormalize(gx, gz, out long ux, out long uz) ? new Intention(true, ux, uz, j, maxStep) : new Intention(false, 0, 0, j);
            }
            return intentions;
        }

        /// <summary>
        /// Place de <paramref name="i"/> dans l'anneau de sa meute autour de la cible <paramref name="j"/> : les ennemis qui encerclent la même
        /// cible, rangés dans le sens trigonométrique à partir du premier (ordre angulaire, puis identifiant), se partagent le tour à angles
        /// égaux ; le rayon de l'anneau met chacun au milieu de ses distances (bords).
        /// </summary>
        private (long X, long Z) RingSlot(int i, int j, int[] targets, long[] x, long[] z, AssailantBehavior behavior)
        {
            var pack = new List<int>();
            for (int k = 0; k < targets.Length; k++)
                if (targets[k] == j && _fightersById[k].Behavior is { Encircle: true }) pack.Add(k);
            long tx = x[j], tz = z[j];
            pack.Sort((a, b) =>
            {
                long ax = checked(x[a] - tx), az = checked(z[a] - tz), bx = checked(x[b] - tx), bz = checked(z[b] - tz);
                int ha = az > 0 || (az == 0 && ax >= 0) ? 0 : 1, hb = bz > 0 || (bz == 0 && bx >= 0) ? 0 : 1;
                if (ha != hb) return ha.CompareTo(hb);
                long cross = Planar.Cross(ax, az, bx, bz);
                if (cross != 0) return cross > 0 ? -1 : 1;
                return string.CompareOrdinal(_fightersById[a].Id, _fightersById[b].Id);
            });
            int first = pack[0];
            if (!Planar.TryNormalize(checked(x[first] - tx), checked(z[first] - tz), out long rx, out long rz))
            {
                rx = Planar.Unit;
                rz = 0;
            }
            int rank = pack.IndexOf(i);
            // Revue Codex de la PR #33 : l'angle de chaque rang est pris sur le tour entier (720 demi-degrés), le reste de la division réparti.
            Angles.Rotate(rx, rz, checked(rank * 720 / pack.Count), out long sx, out long sz);
            Body self = _fightersById[i].Body!;
            long gap = (behavior.MinRange.Raw + behavior.MaxRange.Raw) / 2;
            long radius = checked(_fightersById[j].Body!.Radius + self.Radius + gap);
            return (checked(tx + sx * radius / Planar.Unit), checked(tz + sz * radius / Planar.Unit));
        }

        // --- Charge --------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Bout de la charge de <paramref name="f"/> le long de l'axe de <paramref name="zone"/> : sous-pas d'au plus la moitié de son rayon,
        /// arrêt au dernier point libre (aire, décor, emprises solides présentes et vivantes) ; puis, règle A, recul sous-pas par sous-pas tant
        /// que la place laisserait un joueur sans issue. Calculé sans rien modifier.
        /// </summary>
        private (long X, long Z) ChargeEnd(Fighter f, AttackZone zone)
        {
            int index = _fightersById.IndexOf(f);
            Body b = f.Body!;
            int n = _fightersById.Count;
            var x = new long[n];
            var z = new long[n];
            for (int k = 0; k < n; k++)
            {
                x[k] = _fightersById[k].Body!.X;
                z[k] = _fightersById[k].Body!.Z;
            }
            // Revue Codex de la PR #33 : la charge va vers le bout de la bande verrouillée (origine + axe × longueur), depuis la place
            // actuelle : une charge précédente qui a déjà porté l'ennemi plus loin ne le pousse pas au-delà de sa zone.
            long endX = checked(zone.OriginX + Domain.Numerics.IntMath.MulDiv(zone.AxisX, zone.Reach, Planar.Unit, Domain.Numerics.RoundingMode.HalfAwayFromZero));
            long endZ = checked(zone.OriginZ + Domain.Numerics.IntMath.MulDiv(zone.AxisZ, zone.Reach, Planar.Unit, Domain.Numerics.RoundingMode.HalfAwayFromZero));
            long dx = checked(endX - b.X), dz = checked(endZ - b.Z);
            var path = new List<(long X, long Z)> { (b.X, b.Z) };
            if (Planar.Dot(dx, dz, zone.AxisX, zone.AxisZ) > 0)
            {
                long length = Domain.Numerics.IntMath.Sqrt(Planar.LengthSquared(dx, dz));
                long half = Math.Max(1, b.Radius / 2);
                long count = Math.Max(1, (length + half - 1) / half);
                for (long k = 1; k <= count; k++)
                {
                    long cx = checked(b.X + Domain.Numerics.IntMath.MulDiv(dx, k, count, Domain.Numerics.RoundingMode.HalfAwayFromZero));
                    long cz = checked(b.Z + Domain.Numerics.IntMath.MulDiv(dz, k, count, Domain.Numerics.RoundingMode.HalfAwayFromZero));
                    var (px, pz) = path[path.Count - 1];
                    if (!FreeFor(index, cx, cz, x, z) || !SweepFree(index, px, pz, cx, cz, x, z)) break;
                    path.Add((cx, cz));
                }
            }
            for (int k = path.Count - 1; k > 0; k--)
                if (f.Character != null || TrapPrevented(index, x, z, path[k].X, path[k].Z) is null) return path[k];
            return path[0];
        }

        /// <summary>
        /// Revue Codex de la PR #33 : le trajet de (px, pz) à (cx, cz) est-il libre pour l'emprise de <paramref name="index"/> ? Le disque
        /// balayé ne chevauche ni le décor ni une emprise solide (entre deux points libres, un coin mince ne se traverse pas) ; l'aire est
        /// convexe, ses deux extrémités suffisent.
        /// </summary>
        private bool SweepFree(int index, long px, long pz, long cx, long cz, long[] x, long[] z)
        {
            long r = _fightersById[index].Body!.Radius;
            foreach (ConvexPolygon obstacle in _obstacles)
                if (obstacle.OverlapsCapsule(px, pz, cx, cz, r)) return false;
            for (int j = 0; j < _fightersById.Count; j++)
                if (j != index && _solid[index, j] && !_fightersById[j].Out
                    && Planar.SegmentOverlapsCircle(px, pz, cx, cz, x[j], z[j], checked(r + _fightersById[j].Body!.Radius)))
                    return false;
            return true;
        }

        /// <summary>La place (cx, cz) est-elle libre pour l'emprise de <paramref name="index"/> : dans l'aire, hors du décor, sans chevaucher une emprise solide ?</summary>
        private bool FreeFor(int index, long cx, long cz, long[] x, long[] z)
        {
            long r = _fightersById[index].Body!.Radius;
            if (cx - r < _minX || cx + r > _maxX || cz - r < _minZ || cz + r > _maxZ) return false;
            foreach (ConvexPolygon obstacle in _obstacles)
                if (obstacle.OverlapsCircle(cx, cz, r)) return false;
            for (int j = 0; j < _fightersById.Count; j++)
                if (j != index && _solid[index, j] && !_fightersById[j].Out && Planar.CirclesOverlap(cx, cz, r, x[j], z[j], _fightersById[j].Body!.Radius))
                    return false;
            return true;
        }
    }
}
