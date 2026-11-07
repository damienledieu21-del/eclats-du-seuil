using System;
using System.Collections.Generic;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Randomness;

namespace EdS.Simulation.Engine
{
    /// <summary>
    /// Sous-lot V2.4.1 (bêtes qui se promènent ; réponse de Damien du 6 novembre 2026 à la question 8 de la PR #104 : « un petit lot du
    /// moteur, errance déterministe ») : errance des bêtes pas engagées (<see cref="WanderBehavior"/>).
    /// <list type="bullet">
    /// <item><b>Sans état propre</b> : la place visée et les pauses se déduisent du tick, de la graine du combat, de l'identifiant de la bête et
    /// de sa place de départ (sa « maison », <c>Setup.Placement</c>) ; les tirages viennent d'un <see cref="Pcg32"/> neuf par étape (graine du
    /// combat et numéro de l'étape, flux de la bête) : le générateur du combat et ceux des combattants ne sont jamais touchés, si bien qu'un
    /// combat sans bête qui erre écrit exactement les mêmes lignes qu'avant.</item>
    /// <item><b>Étapes</b> : de <c>legMs</c> chacune, décalées d'une bête à l'autre ; une pause tirée (brouter, fouiller, guetter), puis la
    /// marche à <c>pace</c> de sa vitesse vers un point de son aire (disque de <c>radius</c> autour de sa maison ; le point suivant du circuit
    /// pour une patrouille), où elle s'arrête. Un lièvre avance par bonds (trois ticks de course, cinq d'arrêt).</item>
    /// <item><b>Retour</b> : hors de son aire (après une fuite, un combat), elle rentre d'abord à sa maison, sans pause.</item>
    /// <item><b>Harde et meute</b> (<c>group</c>) : le chef (<c>lead</c>, sinon le premier membre au calme par identifiant) erre ; les autres
    /// membres au calme le suivent en formation (en file derrière un loup qui patrouille, en grappe autour d'un cerf qui broute), un peu plus
    /// vite que lui s'ils sont loin.</item>
    /// <item><b>Calme</b> : une bête engagée qui erre se désengage (ligne <c>calmed</c>) quand, pendant <c>combat.wander.calmMs</c>, aucun
    /// ennemi n'est resté à sa distance d'engagement (à sa distance de fuite pour une bête qui fuit) et que, si elle ne fuit pas, elle n'a plus
    /// de cible ; elle rentre alors chez elle. Une bête à bout (1 PV) ne retient pas une autre bête.</item>
    /// </list>
    /// </summary>
    public sealed partial class CombatEngine
    {
        /// <summary>Flux des tirages de l'errance (« WAND »), mêlé à celui de la bête.</summary>
        private const ulong WanderStream = 0x5741_4E44UL;

        /// <summary>
        /// Relecture de la PR #111 : générateur de l'errance, réutilisé (resemé à chaque étape d'une bête, sans allocation) ; il ne garde rien
        /// d'un plan à l'autre (même suite qu'un <see cref="Pcg32"/> neuf de même graine et même flux).
        /// </summary>
        private readonly Pcg32 _wanderRandom = new Pcg32(0, WanderStream);

        /// <summary>Relecture de la PR #111 : états d'errance affichés, calculés une fois par tick (et par nombre de combattants).</summary>
        private string?[]? _wanderStates;
        private long _wanderStatesTick = -1;

        /// <summary>Ticks de course puis d'arrêt d'un bond de lièvre.</summary>
        private const long HopRun = 3, HopCycle = 8;

        /// <summary>Plan d'errance d'une bête pour ce tick : intention de marche et état affiché.</summary>
        private readonly struct WanderPlan
        {
            public readonly Intention Intention;
            public readonly string State;

            public WanderPlan(Intention intention, string state)
            {
                Intention = intention;
                State = state;
            }
        }

        /// <summary>La bête erre-t-elle à ce tick ? (comportement d'errance, présente, vivante, pas engagée)</summary>
        private static bool Wanders(Fighter f) => f.Behavior?.Wander != null && !f.Out && !f.Engaged && f.Body != null;

        /// <summary>
        /// Chef du groupe d'errance de <paramref name="f"/> parmi les membres qui errent (même équipe, même groupe) : le chef désigné s'il erre,
        /// sinon le premier par identifiant ; null sans groupe.
        /// </summary>
        private Fighter? WanderLeader(Fighter f)
        {
            if (f.Behavior?.Wander?.Group is not { } group) return null;
            Fighter? first = null;
            foreach (Fighter g in _fightersById)
            {
                if (!Wanders(g) || g.Team != f.Team || g.Behavior!.Wander!.Group != group) continue;
                if (g.Behavior.Wander.Lead) return g;
                first ??= g;
            }
            return first;
        }

        /// <summary>
        /// Plan d'errance de la bête d'indice <paramref name="i"/> (elle erre) à <paramref name="tick"/>, d'après les places
        /// <paramref name="x"/>, <paramref name="z"/> (début de la phase de déplacement). Calculé sans rien modifier.
        /// </summary>
        private WanderPlan PlanWander(int i, long tick, long[] x, long[] z)
        {
            Fighter f = _fightersById[i];
            WanderBehavior w = f.Behavior!.Wander!;
            Fighter? leader = WanderLeader(f);
            if (leader != null && leader != f && FollowsLeader(f, leader, x, z)) return Follow(i, _fightersById.IndexOf(leader), x, z);
            return Roam(i, tick, x, z, w);
        }

        /// <summary>Un membre ne suit son chef que s'il le sait près de chez lui (à <see cref="FollowRange"/> + rayons) ; sinon il erre seul.</summary>
        private static readonly long FollowRange = Fixed.FromInt(20).Raw;

        /// <summary>
        /// <paramref name="f"/> suit-il <paramref name="leader"/> ? Seulement si le chef est à moins de <see cref="FollowRange"/> plus leurs deux
        /// rayons d'errance de la maison du membre : un chef parti loin (une poursuite, une autre place) ne traîne pas sa harde à travers la
        /// carte ; le membre erre alors seul chez lui.
        /// </summary>
        private bool FollowsLeader(Fighter f, Fighter leader, long[] x, long[] z)
        {
            int l = _fightersById.IndexOf(leader);
            GroundVector home = f.Setup.Placement!.Position;
            long reach = checked(FollowRange + f.Behavior!.Wander!.Radius.Raw + leader.Behavior!.Wander!.Radius.Raw);
            return Planar.LengthSquared(checked(x[l] - home.X.Raw), checked(z[l] - home.Z.Raw)) <= checked(reach * reach);
        }

        /// <summary>Plan d'un chef ou d'une bête seule : retour, pause, puis marche vers le point de l'étape.</summary>
        private WanderPlan Roam(int i, long tick, long[] x, long[] z, WanderBehavior w)
        {
            Fighter f = _fightersById[i];
            Body b = f.Body!;
            GroundVector home = f.Setup.Placement!.Position;
            long homeX = Clamp(home.X.Raw, _minX, _maxX, b.Radius), homeZ = Clamp(home.Z.Raw, _minZ, _maxZ, b.Radius);
            long tolerance = WanderTolerance(f, w.Pace);
            // Hors de son aire (une fuite, un combat) : elle rentre d'abord, sans pause.
            long fromHome = IntMath.Sqrt(Planar.LengthSquared(checked(x[i] - homeX), checked(z[i] - homeZ)));
            if (fromHome > checked(w.Radius.Raw + tolerance)) return Toward(i, x, z, homeX, homeZ, w.Pace, tolerance, "return", RestState(w.Style));
            long legTicks = w.LegMs / _tickMs;
            ulong stream = StreamOf(f.Id);
            long t = checked(tick + (long)(stream % (ulong)legTicks));
            long leg = t / legTicks, within = t % legTicks;
            Pcg32 random = _wanderRandom;
            random.Reseed(unchecked((ulong)_setup.Seed + (ulong)leg * 0x9E37_79B9_7F4A_7C15UL), stream ^ WanderStream);
            long pauseMin = w.PauseMinMs / _tickMs, pauseMax = w.PauseMaxMs / _tickMs;
            long pause = checked(pauseMin + random.NextBounded(checked((uint)(pauseMax - pauseMin + 1))));
            long goalX, goalZ;
            if (w.Style == WanderStyle.Patrol)
            {
                // Circuit : points à égale distance sur le cercle de son aire, à partir d'un angle propre à la bête ; un point par étape.
                int start = (int)(stream % 720UL);
                int angle = checked(start + (int)(leg % w.Points) * 720 / w.Points) % 720;
                Angles.Rotate(Planar.Unit, 0, angle, out long ux, out long uz);
                goalX = checked(homeX + ux * w.Radius.Raw / Planar.Unit);
                goalZ = checked(homeZ + uz * w.Radius.Raw / Planar.Unit);
            }
            else
            {
                // Point du disque de son aire, uniforme en surface (rayon × racine d'un tirage dans [0, 1]).
                int angle = (int)random.NextBounded(720);
                long fraction = random.NextBounded(10_001);
                long distance = IntMath.Sqrt(checked(w.Radius.Raw * w.Radius.Raw / 10_000 * fraction));
                Angles.Rotate(Planar.Unit, 0, angle, out long ux, out long uz);
                goalX = checked(homeX + ux * distance / Planar.Unit);
                goalZ = checked(homeZ + uz * distance / Planar.Unit);
            }
            goalX = Clamp(goalX, _minX, _maxX, b.Radius);
            goalZ = Clamp(goalZ, _minZ, _maxZ, b.Radius);
            string rest = RestState(w.Style);
            if (within < pause) return new WanderPlan(Intention.Hold, rest);
            if (w.Style == WanderStyle.Hop && (within - pause) % HopCycle >= HopRun) return new WanderPlan(Intention.Hold, "crouch");
            return Toward(i, x, z, goalX, goalZ, w.Pace, tolerance, w.Style == WanderStyle.Hop ? "hop" : "walk", rest);
        }

        /// <summary>
        /// Plan d'un membre qui suit son chef <paramref name="l"/> : sa place dans la formation (file derrière un loup qui patrouille, grappe
        /// autour des autres), selon son rang parmi les suiveurs (ordre des identifiants) ; un peu plus vite que son allure s'il est loin.
        /// </summary>
        private WanderPlan Follow(int i, int l, long[] x, long[] z)
        {
            Fighter f = _fightersById[i], leader = _fightersById[l];
            WanderBehavior w = f.Behavior!.Wander!, lw = leader.Behavior!.Wander!;
            int rank = 0;
            for (int k = 0; k < _fightersById.Count && k <= i; k++)
            {
                Fighter g = _fightersById[k];
                if (k != l && Wanders(g) && g.Team == f.Team && g.Behavior!.Wander!.Group == w.Group && FollowsLeader(g, leader, x, z)) rank++;
            }
            Body lb = leader.Body!;
            long backX = checked(-lb.FacingX), backZ = checked(-lb.FacingZ);
            long slotX, slotZ;
            if (lw.Style == WanderStyle.Patrol)
            {
                long gap = checked(w.Spacing.Raw * rank);
                slotX = checked(x[l] + backX * gap / Planar.Unit);
                slotZ = checked(z[l] + backZ * gap / Planar.Unit);
            }
            else
            {
                // Grappe : angle d'or (137,5°) de rang en rang à partir de l'arrière du chef, à l'écart × racine du rang.
                Angles.Rotate(backX, backZ, checked(rank * 275) % 720, out long ux, out long uz);
                long gap = checked(w.Spacing.Raw * IntMath.Sqrt(checked((long)rank * 100_000_000L)) / 10_000);
                slotX = checked(x[l] + ux * gap / Planar.Unit);
                slotZ = checked(z[l] + uz * gap / Planar.Unit);
            }
            Body b = f.Body!;
            slotX = Clamp(slotX, _minX, _maxX, b.Radius);
            slotZ = Clamp(slotZ, _minZ, _maxZ, b.Radius);
            long distance = IntMath.Sqrt(Planar.LengthSquared(checked(slotX - x[i]), checked(slotZ - z[i])));
            Fixed pace = distance > checked(2 * w.Spacing.Raw) ? Fixed.Min(Fixed.One, Fixed.FromRaw(checked(w.Pace.Raw * 3 / 2))) : w.Pace;
            return Toward(i, x, z, slotX, slotZ, pace, WanderTolerance(f, pace), "walk", RestState(lw.Style));
        }

        /// <summary>Marche vers (gx, gz) à <paramref name="pace"/>, pas borné par la distance ; arrêt (état <paramref name="rest"/>) à la tolérance près.</summary>
        private WanderPlan Toward(int i, long[] x, long[] z, long gx, long gz, Fixed pace, long tolerance, string moving, string rest)
        {
            long vx = checked(gx - x[i]), vz = checked(gz - z[i]);
            if (Planar.LengthSquared(vx, vz) <= checked(tolerance * tolerance) || !Planar.TryNormalize(vx, vz, out long ux, out long uz))
                return new WanderPlan(Intention.Hold, rest);
            long distance = IntMath.Sqrt(Planar.LengthSquared(vx, vz));
            // Sous-lot V2.8 : un obstacle entre la bête et son point : elle le contourne ; aucun chemin : elle attend (étape suivante).
            if (!SteerToward(i, x, z, gx, gz, out long sx, out long sz)) return new WanderPlan(Intention.Hold, rest);
            if ((sx != gx || sz != gz) && !Planar.TryNormalize(checked(sx - x[i]), checked(sz - z[i]), out ux, out uz)) return new WanderPlan(Intention.Hold, rest);
            return new WanderPlan(new Intention(true, ux, uz, -1, distance, pace), moving);
        }

        /// <summary>Un pas à cette allure (au moins une unité brute) : en deçà, la bête est arrivée.</summary>
        private long WanderTolerance(Fighter f, Fixed pace) =>
            Math.Max(1, Fixed.Multiply(Fixed.FromRaw(IntMath.MulDiv(f.Body!.Speed, _tickMs, 1000, RoundingMode.Floor)), pace, RoundingMode.Floor).Raw);

        private static long Clamp(long value, long min, long max, long radius) => Math.Max(checked(min + radius), Math.Min(checked(max - radius), value));

        /// <summary>État affiché d'une pause selon la manière : brouter, fouiller, guetter.</summary>
        private static string RestState(WanderStyle style) => style switch
        {
            WanderStyle.Graze => "graze",
            WanderStyle.Root => "root",
            _ => "watch",
        };

        // --- Calme ---------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Phase des commandes, bête engagée qui erre : compte son temps au calme et la désengage au bout de <c>combat.wander.calmMs</c>
        /// (ligne <c>calmed</c>). Tout est calculé d'abord, puis le coût est réservé (une unité, une ligne si elle se calme), puis l'état est
        /// modifié. Vrai si elle s'est calmée.
        /// </summary>
        private bool RunCalm(long tick, Fighter f, AssailantBehavior behavior)
        {
            bool quiet = Quiet(f, behavior);
            long? since = quiet ? f.QuietSince ?? tick : (long?)null;
            long calmTicks = _rules.WanderCalmMs / _tickMs;
            bool calm = since is long s && checked(tick - s) >= calmTicks;
            long calms = calm ? checked(f.Calms + 1) : f.Calms;
            // Relecture de la PR #111 : « remarqué » (calculé sans rien modifier) partage la réserve du calme : une unité par tick.
            bool noticed = !calm && (f.Noticed || Notices(f, behavior));
            Reserve(Cost.Op + Cost.Line.If(calm));
            Charge(1);
            if (!calm)
            {
                f.QuietSince = since;
                f.Noticed = noticed;
                return false;
            }
            // Revue Codex de la PR #111 (tour 2) : ce qui tient à l'engagement est remis à zéro (engagé, son tick, remarqué, coup reçu,
            // provocation reçue, coup d'un personnage depuis le calme ; une annonce en cours ou jusqu'à la fin empêche le calme, voir Quiet).
            // « Chargée » (Charged) reste : c'est un fait passé qui libère sa place des bornes de ses pas dans l'instantané ; les contrôles
            // de l'instantané prennent alors son arrivée comme plus tôt engagement possible (EarliestEngagement). Les assaillants n'ont ni
            // recharge ni charge de rempart à remettre.
            f.Engaged = false;
            f.EngagedTick = null;
            f.Noticed = false;
            f.Struck = false;
            f.Taunted = false;
            f.CharacterHitSinceCalm = false;
            f.QuietSince = null;
            f.Calms = calms;
            EmitDiagnostic(tick, CombatPhase.Commands, "calmed", w => w.Field("actor", f.Id));
            return true;
        }

        /// <summary>
        /// Au calme : ni provocation active, ni annonce en cours, aucun ennemi présent et vivant à sa distance d'engagement (à sa distance de
        /// fuite si elle est plus grande, pour une bête qui fuit) — une bête à bout (1 PV) ne compte pas pour une bête — et, pour une bête
        /// qui ne fuit pas, plus de cible (un joueur remarqué au loin la retient : la laisse du monde décide).
        /// </summary>
        private bool Quiet(Fighter f, AssailantBehavior behavior)
        {
            foreach (StatusInstance status in f.Statuses.Values)
                if (status.Definition.IsTaunt) return false;
            if (f.AnnouncedToEnd) return false;
            foreach (AttackImpact a in _announced)
                if (a.Assailant == f) return false;
            long reach = behavior.AggroRange!.Value.Raw;
            if (behavior.Flee) reach = Math.Max(reach, behavior.MinRange.Raw);
            foreach (Fighter g in _fightersById)
            {
                if (g.Out || g.Body is null || !Enemies(g, f) || SpentBeast(g)) continue;
                if (EdgeDistance(f.Body!, g.Body) <= reach) return false;
            }
            return behavior.Flee || SingleTarget(f) is null;
        }

        /// <summary>Une créature à bout (1 PV) : les bêtes ne la visent plus et ne s'en soucient plus (une bête n'en tue jamais une autre).</summary>
        private static bool SpentBeast(Fighter g) => g.Character is null && g.Assailant != null && g.Health is long h && h <= 1;

        /// <summary>
        /// Sous-lot V2.4.1 : état d'errance affiché (vue) ; null si la bête n'erre pas. Relecture de la PR #111 : les états de toutes les bêtes
        /// sont calculés une seule fois par tick (les places ne changent qu'en avançant d'un tick ; une arrivée change le nombre de combattants).
        /// </summary>
        private string? WanderState(Fighter f, long tick)
        {
            if (!Wanders(f)) return null;
            int n = _fightersById.Count;
            if (_wanderStates is null || _wanderStatesTick != tick || _wanderStates.Length != n)
            {
                var x = new long[n];
                var z = new long[n];
                for (int k = 0; k < n; k++)
                {
                    x[k] = _fightersById[k].Body?.X ?? 0;
                    z[k] = _fightersById[k].Body?.Z ?? 0;
                }
                var states = new string?[n];
                ResetPathBudget();   // sous-lot V2.8 : passe d'affichage, son propre budget
                for (int k = 0; k < n; k++)
                    if (Wanders(_fightersById[k])) states[k] = PlanWander(k, tick, x, z).State;
                _wanderStates = states;
                _wanderStatesTick = tick;
            }
            return _wanderStates[_fightersById.IndexOf(f)];
        }
    }
}
