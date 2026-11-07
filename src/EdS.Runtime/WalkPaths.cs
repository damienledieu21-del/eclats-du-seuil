using System;
using System.Collections.Generic;
using EdS.Domain.Space;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.8 (recherche de chemin) : trajets des marcheurs de l'affichage (PNJ, passants du bourg) dans une zone : ils contournent
    /// les maisons, murs et rochers de son espace (même grille et même A* que le moteur, <see cref="Navigator"/>), au lieu d'aller en ligne
    /// droite. Le cœur ne les connaît pas : seul l'affichage marche (la place d'un PNJ reste décidée par le cœur).
    /// </summary>
    public sealed class WalkPaths
    {
        /// <summary>Emprise d'un villageois (0,35 m) : un peu plus fine que celle d'un combattant.</summary>
        public const double DefaultRadius = 0.35;

        private readonly Navigator _navigator;
        private readonly long _radius;

        public WalkPaths(CombatSpace space, double radius = DefaultRadius)
        {
            if (space is null) throw new ArgumentNullException(nameof(space));
            if (!(radius > 0 && radius <= 5)) throw new ArgumentOutOfRangeException(nameof(radius));
            _navigator = Navigator.ForSpace(space);
            _radius = Raw(radius);
        }

        /// <summary>Le navigateur (essai, mesures).</summary>
        public Navigator Navigator => _navigator;

        private static long Raw(double meters) => (long)Math.Round(meters * 10_000.0, MidpointRounding.AwayFromZero);

        private static double Meters(long raw) => raw / 10_000.0;

        private static long Clamp(double meters) => Raw(Math.Max(-1000.0, Math.Min(1000.0, meters)));

        /// <summary>
        /// Points de passage de (from) à (to), le but compris ; vide si la ligne droite est libre (aller droit) ; null s'il n'y a pas de
        /// chemin (le marcheur reste où il est, voir <see cref="Walker"/>).
        /// </summary>
        public List<(double X, double Z)>? Route(double fromX, double fromZ, double toX, double toZ)
        {
            List<GroundVector>? route = _navigator.Route(_radius, Clamp(fromX), Clamp(fromZ), Clamp(toX), Clamp(toZ));
            if (route is null) return null;
            var points = new List<(double X, double Z)>(route.Count);
            foreach (GroundVector p in route) points.Add((Meters(p.X.Raw), Meters(p.Z.Raw)));
            return points;
        }
    }

    /// <summary>
    /// Sous-lot V2.8 : un marcheur de l'affichage qui suit son trajet. Le trajet n'est recalculé que quand le but change (pas à chaque
    /// image) ; <see cref="Next"/> donne le point à viser et passe au suivant à l'arrivée. Sans chemin, le marcheur reste sur place (aucun
    /// aller-retour) ; avec <see cref="StraightWhenBlocked"/>, il va droit comme avant V2.8.
    /// </summary>
    public sealed class Walker
    {
        /// <summary>Distance (m) à laquelle un point de passage est atteint.</summary>
        public const double Reach = 0.05;

        private List<(double X, double Z)>? _route;
        private int _at;
        private double _goalX = double.NaN, _goalZ = double.NaN;

        /// <summary>Sans chemin : aller droit (vrai) ou rester sur place (faux, par défaut).</summary>
        public bool StraightWhenBlocked { get; set; }

        /// <summary>Le dernier trajet demandé n'a pas de chemin.</summary>
        public bool Blocked { get; private set; }

        /// <summary>Points de passage restants (y compris le but), pour l'essai.</summary>
        public int Remaining => _route is null ? 0 : _route.Count - _at;

        /// <summary>
        /// Point à viser depuis (x, z) vers le but (gx, gz) : le trajet est recalculé si le but a changé (de plus de <see cref="Reach"/>).
        /// <paramref name="paths"/> null : ligne droite.
        /// </summary>
        public (double X, double Z) Next(WalkPaths? paths, double x, double z, double gx, double gz)
        {
            if (double.IsNaN(_goalX) || Math.Abs(gx - _goalX) > Reach || Math.Abs(gz - _goalZ) > Reach)
            {
                _goalX = gx;
                _goalZ = gz;
                _at = 0;
                _route = paths?.Route(x, z, gx, gz) ?? (paths is null ? new List<(double X, double Z)>() : null);
                Blocked = _route is null;
            }
            if (_route is null) return StraightWhenBlocked ? (gx, gz) : (x, z);
            while (_at < _route.Count)
            {
                (double px, double pz) = _route[_at];
                if ((px - x) * (px - x) + (pz - z) * (pz - z) > Reach * Reach) return (px, pz);
                _at++;
            }
            return (gx, gz);
        }

        /// <summary>Oublie le trajet (un saut, un changement de zone) : le prochain <see cref="Next"/> le recalcule.</summary>
        public void Reset()
        {
            _route = null;
            _at = 0;
            _goalX = _goalZ = double.NaN;
            Blocked = false;
        }
    }
}
