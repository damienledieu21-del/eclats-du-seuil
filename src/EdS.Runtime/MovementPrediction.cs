using System;
using System.Collections.Generic;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot 11.7 : prédiction du déplacement du joueur en réseau, pour l'affichage seulement (le combat n'en sait rien, le serveur
    /// reste seul juge). Sans elle, le personnage partirait et s'arrêterait avec un temps aller-retour de retard : la saisie met une
    /// demi-latence à atteindre le serveur, et l'état qui en résulte une autre à revenir.
    /// <list type="bullet">
    /// <item>Quand le serveur applique déjà la marche voulue (même cap, ou arrêt), la place affichée est celle du dernier tick reçu,
    /// avancée du temps écoulé dans le tick et du délai de retour (<c>lead</c>) : le personnage est montré là où le serveur l'a déjà
    /// mené.</item>
    /// <item>Quand la saisie vient de changer (nouveau cap, arrêt) et que le serveur ne l'applique pas encore, la place affichée part de
    /// la dernière montrée et suit la nouvelle saisie dès maintenant ; à l'arrêt, elle ne bouge plus.</item>
    /// <item>Si le serveur ne suit pas la saisie au bout de <see cref="MaxPendingSeconds"/> (commande perdue ou refusée), la prédiction
    /// est abandonnée : la place du serveur est montrée.</item>
    /// </list>
    /// La place prédite s'arrête au bord de l'aire et avant le décor (les autres combattants sont ignorés) ; la vitesse est celle du
    /// personnage (ralentie en garde), sans les altérations, et nulle quand il ne peut pas bouger. L'affichage la rejoint en douceur.
    /// </summary>
    public sealed class MovementPrediction
    {
        /// <summary>Délai au-delà duquel une saisie que le serveur n'applique pas est oubliée.</summary>
        public const double MaxPendingSeconds = 1.0;

        /// <summary>Avance la plus grande prise en compte (un temps aller-retour plus long n'est pas compensé au-delà).</summary>
        public const double MaxLeadSeconds = 0.5;

        /// <summary>Pas de l'essai contre le décor (5 cm, en unités brutes).</summary>
        private const long ProbeRaw = 500;

        private bool _pending;
        private int? _pendingHeading;
        private double _anchorX, _anchorZ, _since;
        private bool _hasLast;
        private double _lastX, _lastZ;

        /// <summary>Oublie tout (nouveau combat, état courant repris).</summary>
        public void Reset()
        {
            _pending = false;
            _pendingHeading = null;
            _hasLast = false;
        }

        /// <summary>
        /// Place prédite (en mètres) du joueur <paramref name="me"/> au moment <paramref name="now"/> (horloge du client, en secondes).
        /// <paramref name="progressSeconds"/> : temps écoulé depuis le dernier tick reçu ; <paramref name="leadSeconds"/> : délai entre la
        /// saisie et son effet affiché (temps aller-retour et un tick), borné à <see cref="MaxLeadSeconds"/>.
        /// </summary>
        public (double X, double Z) Predict(CombatantView me, CombatSpace space, IReadOnlyList<ConvexPolygon> obstacles, CombatRules rules, int? wished,
            double now, double leadSeconds, double progressSeconds)
        {
            if (me is null) throw new ArgumentNullException(nameof(me));
            if (space is null) throw new ArgumentNullException(nameof(space));
            if (obstacles is null) throw new ArgumentNullException(nameof(obstacles));
            if (rules is null) throw new ArgumentNullException(nameof(rules));
            if (!Finite(now) || !Finite(leadSeconds) || !Finite(progressSeconds))
                throw new ArgumentOutOfRangeException(nameof(now), "Temps de prédiction non fini.");
            if (me.Position is not GroundVector position || me.Radius is not Fixed radius)
                throw new ArgumentException($"« {me.Id} » n'a pas de place : combat sans espace.", nameof(me));
            double x = position.X.Raw / 10_000.0, z = position.Z.Raw / 10_000.0;
            if (me.IsDead || !me.IsPresent)
            {
                Reset();
                return Remember(x, z);
            }
            double speed = Speed(me, rules);
            GroundVector? server = me.MoveDirection;
            GroundVector? wishedDirection = wished is int h ? MoveInput.Direction(h) : (GroundVector?)null;
            bool agree = Nullable.Equals(server, wishedDirection);
            if (!agree && (!_pending || _pendingHeading != wished))
            {
                // La saisie vient de changer : la place affichée part de la dernière montrée.
                _pending = true;
                _pendingHeading = wished;
                _since = now;
                (_anchorX, _anchorZ) = _hasLast ? (_lastX, _lastZ) : (x, z);
            }
            if (agree) _pending = false;
            if (_pending && now - _since <= MaxPendingSeconds)
            {
                return wishedDirection is GroundVector d
                    ? Remember(Walk(_anchorX, _anchorZ, d, speed * Math.Max(0, now - _since), radius.Raw, space, obstacles))
                    : Remember(_anchorX, _anchorZ);
            }
            // Le serveur suit la saisie (ou la prédiction est abandonnée) : sa place, avancée du temps du tick et, s'il suit, du délai.
            double ahead = Math.Max(0, progressSeconds) + (agree ? Math.Min(Math.Max(0, leadSeconds), MaxLeadSeconds) : 0);
            return server is GroundVector s ? Remember(Walk(x, z, s, speed * ahead, radius.Raw, space, obstacles)) : Remember(x, z);
        }

        /// <summary>Vitesse d'affichage (m/s) : celle du personnage, ralentie en garde ; nulle s'il ne peut pas bouger.</summary>
        public static double Speed(CombatantView me, CombatRules rules)
        {
            if (me is null) throw new ArgumentNullException(nameof(me));
            if (rules is null) throw new ArgumentNullException(nameof(rules));
            if (!me.CanMove) return 0;
            double speed = (me.Character?.Placement?.Speed ?? me.Assailant?.Placement?.Speed ?? rules.MovementSpeed).Raw / 10_000.0;
            if (me.Guarding) speed *= rules.GuardSpeedFactor.Raw / 10_000.0;
            return speed;
        }

        /// <summary>
        /// Avance de <paramref name="distance"/> mètres depuis (<paramref name="x"/>, <paramref name="z"/>) dans la direction
        /// <paramref name="direction"/>, par pas de 5 cm, et s'arrête au dernier pas qui reste dans l'aire et hors du décor.
        /// </summary>
        public static (double X, double Z) Walk(double x, double z, GroundVector direction, double distance, long radiusRaw, CombatSpace space,
            IReadOnlyList<ConvexPolygon> obstacles)
        {
            if (space is null) throw new ArgumentNullException(nameof(space));
            if (obstacles is null) throw new ArgumentNullException(nameof(obstacles));
            if (!Finite(x) || !Finite(z) || !Finite(distance)) throw new ArgumentOutOfRangeException(nameof(distance), "Déplacement non fini.");
            double total = Math.Min(Math.Max(0, distance), 50) * 10_000.0;   // au plus 50 m : bien plus qu'une demi-seconde de marche
            double dx = direction.X.Raw / 10_000.0, dz = direction.Z.Raw / 10_000.0;
            double ox = x * 10_000.0, oz = z * 10_000.0;
            double bestX = x, bestZ = z;
            for (double done = 0; done < total;)
            {
                done = Math.Min(total, done + ProbeRaw);
                long px = (long)Math.Round(ox + dx * done), pz = (long)Math.Round(oz + dz * done);
                if (!Free(px, pz, radiusRaw, space, obstacles)) break;
                bestX = px / 10_000.0;
                bestZ = pz / 10_000.0;
            }
            return (bestX, bestZ);
        }

        private static bool Free(long x, long z, long r, CombatSpace space, IReadOnlyList<ConvexPolygon> obstacles)
        {
            if (x - r < space.Min.X.Raw || x + r > space.Max.X.Raw || z - r < space.Min.Z.Raw || z + r > space.Max.Z.Raw) return false;
            foreach (ConvexPolygon obstacle in obstacles)
                if (obstacle.OverlapsCircle(x, z, r)) return false;
            return true;
        }

        private (double X, double Z) Remember((double X, double Z) p) => Remember(p.X, p.Z);

        private (double X, double Z) Remember(double x, double z)
        {
            _lastX = x;
            _lastZ = z;
            _hasLast = true;
            return (x, z);
        }

        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
