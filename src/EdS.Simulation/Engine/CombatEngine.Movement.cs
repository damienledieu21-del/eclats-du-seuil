using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.Simulation.Engine
{
    /// <summary>
    /// Sous-lot 11.4 : positions, déplacement et décor (COMBAT_ESPACE.md, sections 1 à 3). Un combat sans espace (<see cref="CombatSetup.Space"/>
    /// null) ne passe jamais par ici et se simule exactement comme avant. Dans un combat positionné :
    /// <list type="bullet">
    /// <item>chaque combattant a une emprise circulaire, une orientation et une intention de déplacement (commandes <c>move</c> et <c>stop</c>) ;</item>
    /// <item>la phase <c>movement</c>, entre les commandes et la résolution, avance chaque combattant qui marche, un par un dans l'ordre de leurs
    /// identifiants : vitesse de base, ralentie en garde et par les altérations (<c>movementSpeedModifier</c>), et, règle B, quand un joueur
    /// traverse une créature ;</item>
    /// <item>le décor et les bords de l'aire sont solides ; un combattant glisse le long de ce qui l'arrête, sans rien pousser ; les joueurs
    /// ne se bloquent jamais entre eux ; les créatures s'évitent ; joueur et créature selon la règle de contact (A ou B) ;</item>
    /// <item>règle A : une créature ne termine pas un pas qui laisserait un joueur sans aucune direction libre (garde-fou anti-piège).</item>
    /// </list>
    /// Tout est entier (unité brute 0,0001 m) : même combat, mêmes positions, octet par octet.
    /// </summary>
    public sealed partial class CombatEngine
    {
        /// <summary>État spatial d'un combattant positionné (unités brutes de <see cref="Fixed"/>).</summary>
        private sealed class Body
        {
            public long X, Z;

            /// <summary>Orientation, vecteur unitaire de longueur <see cref="Planar.Unit"/>.</summary>
            public long FacingX, FacingZ;

            public long Radius;

            /// <summary>Vitesse de marche de base, en m/s brutes (propre à l'assaillant ou celle des règles).</summary>
            public long Speed;

            /// <summary>Intention de déplacement en cours et sa direction unitaire.</summary>
            public bool Moving;
            public long DirectionX, DirectionZ;

            /// <summary>Sous-lot 11.4c : cible sélectionnée (commande <c>select</c>) ; en garde, le personnage se tourne vers elle.</summary>
            public Fighter? Selected;
        }

        /// <summary>Résultat prévu du déplacement d'un combattant à ce tick, calculé avant toute mutation.</summary>
        private struct MovePlan
        {
            public long X, Z, FacingX, FacingZ;
            public string? TrapPrevented;
            public bool Changed;
        }

        /// <summary>Dégagement laissé après une poussée hors d'un obstacle ou d'une emprise (0,3 mm) : absorbe les arrondis de direction.</summary>
        private const long Skin = 3;

        /// <summary>Nombre de dégagements successifs tentés pour une position ; au-delà, le pas s'arrête là où il était.</summary>
        private const int MaxPushes = 3;

        /// <summary>Huit directions d'essai du garde-fou anti-piège (est, nord-est, nord…), unitaires à 10⁻⁴ près.</summary>
        private static readonly long[] ProbeX = { 10_000, 7_071, 0, -7_071, -10_000, -7_071, 0, 7_071 };
        private static readonly long[] ProbeZ = { 0, 7_071, 10_000, 7_071, 0, -7_071, -10_000, -7_071 };

        private bool _spatial;
        private ConvexPolygon[] _obstacles = Array.Empty<ConvexPolygon>();
        private long _minX, _minZ, _maxX, _maxZ;

        /// <summary>Pas maximal par tick (vitesse technique maximale) et pas d'essai du garde-fou (vitesse de base des règles).</summary>
        private long _maxStep, _probeStep;

        /// <summary>Solidité de chaque paire de combattants (ordre des identifiants), fixe pendant tout le combat ; un mort n'est solide pour personne.</summary>
        private bool[,] _solid = new bool[0, 0];

        private void InitSpace()
        {
            if (_setup.Space is not { } space) return;
            _spatial = true;
            _obstacles = space.Obstacles.Select(o => new ConvexPolygon(o.Points)).ToArray();
            _minX = space.Min.X.Raw;
            _minZ = space.Min.Z.Raw;
            _maxX = space.Max.X.Raw;
            _maxZ = space.Max.Z.Raw;
            _maxStep = IntMath.MulDiv(SpaceLimits.MaxSpeed.Raw, _tickMs, 1000, RoundingMode.Floor);
            _probeStep = IntMath.MulDiv(_rules.MovementSpeed.Raw, _tickMs, 1000, RoundingMode.Floor);
            BuildSolid();
        }

        /// <summary>Solidité de chaque paire (ordre des identifiants) ; sous-lot 11.4d : refaite quand un combattant rejoint le combat (<see cref="Join"/>).</summary>
        private void BuildSolid()
        {
            if (!_spatial) return;
            int n = _fightersById.Count;
            _solid = new bool[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    if (i != j)
                        _solid[i, j] = _rules.AreSolid(_fightersById[i].Character != null, _fightersById[i].Setup.Placement!.Radius,
                            _fightersById[j].Character != null, _fightersById[j].Setup.Placement!.Radius);
        }

        private Body? CreateBody(CombatantSetup setup)
        {
            if (setup.Placement is not { } p) return null;
            Planar.TryNormalize(p.Facing.X.Raw, p.Facing.Z.Raw, out long fx, out long fz);   // non nulle (validation)
            return new Body
            {
                X = p.Position.X.Raw,
                Z = p.Position.Z.Raw,
                FacingX = fx,
                FacingZ = fz,
                Radius = p.Radius.Raw,
                Speed = (p.Speed ?? _rules.MovementSpeed).Raw,
            };
        }

        // --- Commandes ---------------------------------------------------------------------------

        /// <summary>Motif de refus d'un déplacement : mort, ou mannequin (il n'agit jamais) ; un personnage et un assaillant marchent.</summary>
        private static string? CheckMove(Fighter actor)
        {
            if (actor.Out) return OutReason(actor, "actor");
            if (actor.Character is null && actor.Assailant is null) return "actorCannotAct";
            return null;
        }

        private void StartMoving(long tick, CommandItem item, Fighter actor, Provenance root)
        {
            GroundVector direction = item.Command.Direction!.Value;
            Planar.TryNormalize(direction.X.Raw, direction.Z.Raw, out long ux, out long uz);   // non nulle (validation)
            Body body = actor.Body!;
            body.Moving = true;
            body.DirectionX = ux;
            body.DirectionZ = uz;
            Emit(tick, CombatPhase.Commands, "moveStarted", root, w =>
            {
                w.Field("actor", actor.Id).Field("command", item.Index);
                WriteVector(w, "direction", ux, uz);
            });
        }

        private void StopMoving(long tick, CommandItem item, Fighter actor, Provenance root)
        {
            actor.Body!.Moving = false;
            Emit(tick, CombatPhase.Commands, "moveStopped", root, w => w.Field("actor", actor.Id).Field("command", item.Index));
        }

        // --- Phase « movement » ------------------------------------------------------------------

        /// <summary>
        /// Avance les combattants qui marchent, dans l'ordre de leurs identifiants : chacun voit les positions déjà avancées de ceux qui le
        /// précèdent. Tout est calculé d'abord sur des copies, puis le coût exact est réservé (une unité par combattant, une ligne
        /// <c>moved</c> si une position, une orientation ou un garde-fou a changé quelque chose), puis l'état est modifié.
        /// </summary>
        private void RunMovement(long tick)
        {
            if (!_spatial) return;
            int n = _fightersById.Count;
            var x = new long[n];
            var z = new long[n];
            for (int i = 0; i < n; i++)
            {
                x[i] = _fightersById[i].Body!.X;
                z[i] = _fightersById[i].Body!.Z;
            }
            var plans = new MovePlan[n];
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                Fighter f = _fightersById[i];
                Body b = f.Body!;
                MovePlan plan = new MovePlan { X = b.X, Z = b.Z, FacingX = b.FacingX, FacingZ = b.FacingZ };
                if (!f.Out && b.Moving)
                {
                    long step = StepLength(f, i, x, z);
                    if (step > 0)
                    {
                        plan.FacingX = b.DirectionX;
                        plan.FacingZ = b.DirectionZ;
                        Planar.TryResize(b.DirectionX, b.DirectionZ, step, out long dx, out long dz);
                        var (nx, nz) = Travel(i, x, z, dx, dz, step);
                        // Un pas qui n'avance pas d'un huitième de sa longueur est annulé : pas de tremblement contre un angle ou entre
                        // deux contacts (les dégagements successifs y trouvent un point un peu différent à chaque tick).
                        if (checked(64 * Planar.LengthSquared(checked(nx - x[i]), checked(nz - z[i]))) < checked(step * step))
                        {
                            nx = x[i];
                            nz = z[i];
                        }
                        if ((nx != x[i] || nz != z[i]) && f.Character is null)
                        {
                            plan.TrapPrevented = TrapPrevented(i, x, z, nx, nz);
                            if (plan.TrapPrevented != null)
                            {
                                nx = x[i];
                                nz = z[i];
                            }
                        }
                        x[i] = nx;
                        z[i] = nz;
                        plan.X = nx;
                        plan.Z = nz;
                    }
                }
                plans[i] = plan;
            }
            // Sous-lot 11.4c (COMBAT_ESPACE.md, section 6, retenu par Damien) : en garde, un personnage se tourne vers sa cible sélectionnée
            // vivante, d'après les positions de fin de phase ; sans cible, il garde l'orientation de son déplacement.
            for (int i = 0; i < n; i++)
            {
                Fighter f = _fightersById[i];
                if (f.Out || !f.Guarding || f.Body!.Selected is not { Out: false } selected || ReferenceEquals(selected, f)) continue;
                int j = _fightersById.IndexOf(selected);
                if (Planar.TryNormalize(checked(x[j] - x[i]), checked(z[j] - z[i]), out long fx, out long fz))
                {
                    plans[i].FacingX = fx;
                    plans[i].FacingZ = fz;
                }
            }
            for (int i = 0; i < n; i++)
            {
                Body b = _fightersById[i].Body!;
                ref MovePlan plan = ref plans[i];
                plan.Changed = plan.X != b.X || plan.Z != b.Z || plan.FacingX != b.FacingX || plan.FacingZ != b.FacingZ || plan.TrapPrevented != null;
                any |= plan.Changed;
            }

            // Sous-lot 11.4d : un absent ne coûte rien (sa place ne change pas).
            long present = _fightersById.LongCount(f => !f.Absent);
            Reserve(checked(present * Cost.Op) + Cost.Line.If(any));
            for (int i = 0; i < n; i++)
            {
                if (!_fightersById[i].Absent) Charge(1);
                Body b = _fightersById[i].Body!;
                b.X = plans[i].X;
                b.Z = plans[i].Z;
                b.FacingX = plans[i].FacingX;
                b.FacingZ = plans[i].FacingZ;
            }
            if (!any) return;
            EmitDiagnostic(tick, CombatPhase.Movement, "moved", w =>
            {
                w.Name("combatants").BeginArray();
                for (int i = 0; i < n; i++)
                {
                    if (!plans[i].Changed) continue;
                    w.BeginObject().Field("combatant", _fightersById[i].Id);
                    WriteVector(w, "position", plans[i].X, plans[i].Z);
                    WriteVector(w, "facing", plans[i].FacingX, plans[i].FacingZ);
                    if (plans[i].TrapPrevented != null) w.Field("trapPrevented", plans[i].TrapPrevented);
                    w.EndObject();
                }
                w.EndArray();
            });
        }

        /// <summary>
        /// Longueur du pas de ce tick, en unités brutes : nulle sous une altération qui bloque le déplacement (<c>blocks: movement</c>,
        /// l'étourdissement) ; sinon arrondie par défaut à chaque facteur, dans cet ordre : vitesse de base × durée du
        /// tick ; × facteur de garde si le combattant garde ; × (1 + somme des <c>movementSpeedModifier</c> actifs, chaque altération une
        /// fois, au moins 0) ; règle B, × facteur de traversée si un joueur chevauche une créature qu'il peut traverser ; plafonnée par la
        /// vitesse technique maximale.
        /// </summary>
        private long StepLength(Fighter f, int index, long[] x, long[] z)
        {
            // Décision de Damien du 20/10/2026 : une altération qui bloque le déplacement (étourdissement) immobilise ; l'intention reste.
            if (BlockReason(f, StatusBlocks.Movement) != null) return 0;
            long step = IntMath.MulDiv(f.Body!.Speed, _tickMs, 1000, RoundingMode.Floor);
            if (f.Guarding) step = Fixed.Multiply(Fixed.FromRaw(step), _rules.GuardSpeedFactor, RoundingMode.Floor).Raw;
            Fixed modifier = MovementSpeedModifier(f);
            if (modifier != Fixed.Zero)
                step = Fixed.Multiply(Fixed.FromRaw(step), Fixed.Max(Fixed.Zero, Fixed.One + modifier), RoundingMode.Floor).Raw;
            if (f.Character != null && _rules.PlayerCreatureContact == ContactRule.SlowedCrossing && CrossesCreature(index, x, z))
                step = Fixed.Multiply(Fixed.FromRaw(step), _rules.CrossingSpeedFactor, RoundingMode.Floor).Raw;
            return Math.Min(step, _maxStep);
        }

        /// <summary>Somme des modificateurs de vitesse des altérations actives, chaque définition une fois (comme <see cref="DamageTakenIncrease"/>).</summary>
        private static Fixed MovementSpeedModifier(Fighter f)
        {
            Fixed total = Fixed.Zero;
            HashSet<string>? seen = null;
            foreach (StatusInstance status in f.Statuses.Values)
                if (status.Definition.MovementSpeedModifier != Fixed.Zero && (seen ??= new HashSet<string>(StringComparer.Ordinal)).Add(status.State))
                    total += status.Definition.MovementSpeedModifier;
            return total;
        }

        /// <summary>Règle B : le joueur chevauche-t-il une créature vivante qu'il peut traverser (non solide pour lui) ?</summary>
        private bool CrossesCreature(int index, long[] x, long[] z)
        {
            long r = _fightersById[index].Body!.Radius;
            for (int j = 0; j < _fightersById.Count; j++)
            {
                Fighter other = _fightersById[j];
                if (j == index || other.Out || other.Character != null || _solid[index, j]) continue;
                if (Planar.CirclesOverlap(x[index], z[index], r, x[j], z[j], other.Body!.Radius)) return true;
            }
            return false;
        }

        /// <summary>
        /// Avance le combattant <paramref name="index"/> du vecteur (dx, dz) de longueur <paramref name="length"/>, par sous-pas d'au plus
        /// la moitié de son rayon (rien ne traverse un mur, même mince) ; chaque sous-pas est dégagé de ce qui le chevauche
        /// (<see cref="Settle"/>), ce qui fait glisser le long d'un obstacle. Un sous-pas qui ne peut être dégagé arrête le pas là où il en était.
        /// </summary>
        private (long X, long Z) Travel(int index, long[] x, long[] z, long dx, long dz, long length)
        {
            long half = Math.Max(1, _fightersById[index].Body!.Radius / 2);
            long count = Math.Max(1, (length + half - 1) / half);
            long px = x[index], pz = z[index], doneX = 0, doneZ = 0;
            for (long k = 1; k <= count; k++)
            {
                long cumX = IntMath.MulDiv(dx, k, count, RoundingMode.HalfAwayFromZero);
                long cumZ = IntMath.MulDiv(dz, k, count, RoundingMode.HalfAwayFromZero);
                long cx = checked(px + cumX - doneX), cz = checked(pz + cumZ - doneZ);
                doneX = cumX;
                doneZ = cumZ;
                if (!Settle(index, x, z, ref cx, ref cz)) break;
                px = cx;
                pz = cz;
            }
            return (px, pz);
        }

        /// <summary>
        /// Ramène une position candidate dans l'aire, puis la dégage des obstacles (ordre du scénario) et des emprises solides (ordre des
        /// identifiants) qu'elle chevauche, au plus <see cref="MaxPushes"/> fois. Vrai si la position finale ne chevauche plus rien (test exact).
        /// </summary>
        private bool Settle(int index, long[] x, long[] z, ref long cx, ref long cz)
        {
            long r = _fightersById[index].Body!.Radius;
            for (int pass = 0; ; pass++)
            {
                cx = Math.Min(Math.Max(cx, checked(_minX + r)), checked(_maxX - r));
                cz = Math.Min(Math.Max(cz, checked(_minZ + r)), checked(_maxZ - r));
                bool overlapping = false;
                foreach (ConvexPolygon obstacle in _obstacles)
                {
                    if (!obstacle.OverlapsCircle(cx, cz, r)) continue;
                    overlapping = true;
                    if (pass < MaxPushes && !PushOutOfPolygon(obstacle, ref cx, ref cz, r)) return false;
                }
                for (int j = 0; j < _fightersById.Count; j++)
                {
                    if (j == index || !_solid[index, j] || _fightersById[j].Out) continue;
                    long rj = _fightersById[j].Body!.Radius;
                    if (!Planar.CirclesOverlap(cx, cz, r, x[j], z[j], rj)) continue;
                    overlapping = true;
                    if (pass < MaxPushes && !PushOutOfCircle(ref cx, ref cz, r, x[j], z[j], rj)) return false;
                }
                if (!overlapping) return true;
                if (pass == MaxPushes) return false;
            }
        }

        /// <summary>Pousse le centre hors d'un obstacle, le long de la normale au point du bord le plus proche ; faux si le centre est dedans.</summary>
        private static bool PushOutOfPolygon(ConvexPolygon obstacle, ref long cx, ref long cz, long r)
        {
            if (obstacle.Contains(cx, cz)) return false;
            obstacle.ClosestBoundaryPoint(cx, cz, out long qx, out long qz);
            if (!Planar.TryResize(checked(cx - qx), checked(cz - qz), checked(r + Skin), out long ox, out long oz)) return false;
            cx = checked(qx + ox);
            cz = checked(qz + oz);
            return true;
        }

        /// <summary>Pousse le centre hors d'une emprise solide (qui, elle, ne bouge pas : aucune poussée transmise) ; faux si les centres coïncident.</summary>
        private static bool PushOutOfCircle(ref long cx, ref long cz, long r, long ox, long oz, long otherRadius)
        {
            if (!Planar.TryResize(checked(cx - ox), checked(cz - oz), checked(r + otherRadius + Skin), out long px, out long pz)) return false;
            cx = checked(ox + px);
            cz = checked(oz + pz);
            return true;
        }

        /// <summary>
        /// Garde-fou de la règle A (plan du lot 11, section 4.1) : la créature <paramref name="mover"/>, qui veut finir son pas en (nx, nz),
        /// laisserait-elle sans issue un joueur qui en avait une ? Un joueur est « sans issue » si, dans aucune des huit directions d'essai,
        /// un pas d'essai (vitesse de base × un tick, avec glissement) ne l'avance d'au moins un quart de ce pas. Seuls les joueurs proches,
        /// pour qui la créature est solide, sont examinés, dans l'ordre de leurs identifiants. Renvoie le premier joueur protégé, ou null.
        /// <para>Garde-fou local : il voit un joueur coincé contre le décor, pas un cul-de-sac fermé à distance (limite connue, essai du 11.6).</para>
        /// </summary>
        private string? TrapPrevented(int mover, long[] x, long[] z, long nx, long nz)
        {
            long rm = _fightersById[mover].Body!.Radius;
            for (int j = 0; j < _fightersById.Count; j++)
            {
                Fighter player = _fightersById[j];
                if (j == mover || player.Character is null || player.Out || !_solid[mover, j]) continue;
                long reach = checked(player.Body!.Radius + rm + 2 * _probeStep);
                if (Planar.LengthSquared(checked(x[j] - nx), checked(z[j] - nz)) >= checked(reach * reach)) continue;
                if (IsTrapped(j, x, z)) continue;   // déjà sans issue avant ce pas : la créature n'en est pas la cause
                long ox = x[mover], oz = z[mover];
                x[mover] = nx;
                z[mover] = nz;
                bool trapped = IsTrapped(j, x, z);
                x[mover] = ox;
                z[mover] = oz;
                if (trapped) return player.Id;
            }
            return null;
        }

        private bool IsTrapped(int index, long[] x, long[] z)
        {
            long minimum = Math.Max(1, _probeStep / 4);
            for (int k = 0; k < ProbeX.Length; k++)
            {
                Planar.TryResize(ProbeX[k], ProbeZ[k], _probeStep, out long dx, out long dz);
                var (px, pz) = Travel(index, x, z, dx, dz, _probeStep);
                if (Planar.LengthSquared(checked(px - x[index]), checked(pz - z[index])) >= checked(minimum * minimum)) return false;
            }
            return true;
        }

        private static void WriteVector(JsonLineWriter w, string name, long x, long z) =>
            w.Name(name).BeginArray().Value(Fixed.FromRaw(x)).Value(Fixed.FromRaw(z)).EndArray();
    }
}
