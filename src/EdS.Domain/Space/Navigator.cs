using System;
using System.Collections.Generic;

namespace EdS.Domain.Space
{
    /// <summary>Sous-lot V2.8 : façon dont un marcheur rejoint son but.</summary>
    public enum NavSteer
    {
        /// <summary>Le trajet en ligne droite est libre : il va droit au but (comportement d'avant V2.8, à l'identique).</summary>
        Direct,

        /// <summary>Un obstacle barre la ligne droite : il vise le point de passage le plus lointain visible de son chemin.</summary>
        Path,

        /// <summary>Comme <see cref="Path"/>, vers la case explorée la plus proche du but (budget de recherche épuisé).</summary>
        Partial,

        /// <summary>Aucun chemin : il reste sur place (jamais d'aller-retour).</summary>
        None,
    }

    /// <summary>
    /// Sous-lot V2.8 (recherche de chemin) : navigation dans une aire et ses obstacles, pour des marcheurs de toutes tailles. Une grille
    /// (<see cref="NavGrid"/>) par classe d'emprise (rayon arrondi au dixième de mètre supérieur), construite à la première demande ; une
    /// recherche A* bornée (<see cref="NavSearch"/>) ; un cache des chemins par (classe, case de départ, case d'arrivée). Tout résultat est
    /// une <b>fonction pure</b> de la géométrie et des deux points : le cache et les grilles construites à la demande ne changent jamais une
    /// réponse (un instantané n'a pas à les garder, un rejeu les refait). Aucune horloge, aucun flottant, aucun tirage.
    /// </summary>
    public sealed class Navigator
    {
        /// <summary>Classe d'emprise : rayon arrondi au dixième de mètre supérieur (les grilles sont partagées).</summary>
        public const long RadiusStep = 1_000;

        /// <summary>Recherche des cases libres autour du départ (±2 cases) et du but (±4 cases).</summary>
        public const int StartReach = 2, GoalReach = 4;

        /// <summary>Lissage : points de passage examinés au plus devant le marcheur (le plus lointain visible est visé).</summary>
        public const int Lookahead = 24;

        /// <summary>Longueur maximale d'un chemin gardé (au-delà, seules les premières cases comptent pour avancer).</summary>
        public const int MaxPathCells = 4_096;

        private readonly long _minX, _minZ, _maxX, _maxZ;
        private readonly ConvexPolygon[] _obstacles;
        private readonly Dictionary<long, NavGrid> _grids = new Dictionary<long, NavGrid>();
        private readonly Dictionary<long, int[]> _cache = new Dictionary<long, int[]>();
        private readonly NavSearch _search = new NavSearch();
        private readonly int[] _buffer = new int[MaxPathCells];

        /// <summary>Cases développées au plus par recherche.</summary>
        public int MaxExpanded { get; }

        /// <summary>Chemins gardés au plus (le cache se vide quand il est plein ; sans effet sur les réponses).</summary>
        public int CacheSize { get; }

        /// <summary>Mesures (sans effet sur les réponses) : recherches faites, cases développées en tout et à la dernière recherche.</summary>
        public long Searches { get; private set; }
        public long TotalExpanded { get; private set; }
        public int LastExpanded { get; private set; }

        public Navigator(long minX, long minZ, long maxX, long maxZ, IReadOnlyList<ConvexPolygon> obstacles, int maxExpanded = 8_192, int cacheSize = 512)
        {
            if (obstacles is null) throw new ArgumentNullException(nameof(obstacles));
            if (maxExpanded < 1) throw new ArgumentOutOfRangeException(nameof(maxExpanded));
            if (cacheSize < 1) throw new ArgumentOutOfRangeException(nameof(cacheSize));
            _minX = minX;
            _minZ = minZ;
            _maxX = maxX;
            _maxZ = maxZ;
            _obstacles = new ConvexPolygon[obstacles.Count];
            for (int i = 0; i < _obstacles.Length; i++) _obstacles[i] = obstacles[i];
            MaxExpanded = maxExpanded;
            CacheSize = cacheSize;
        }

        /// <summary>Navigation d'un espace de combat (tous ses obstacles, bas compris : ils arrêtent la marche).</summary>
        public static Navigator ForSpace(CombatSpace space, int maxExpanded = 8_192, int cacheSize = 512)
        {
            if (space is null) throw new ArgumentNullException(nameof(space));
            var polygons = new List<ConvexPolygon>(space.Obstacles.Count);
            foreach (SpaceObstacle o in space.Obstacles) polygons.Add(new ConvexPolygon(o.Points));
            return new Navigator(space.Min.X.Raw, space.Min.Z.Raw, space.Max.X.Raw, space.Max.Z.Raw, polygons, maxExpanded, cacheSize);
        }

        public static long RadiusClass(long radius) => radius <= RadiusStep ? RadiusStep : checked((radius + RadiusStep - 1) / RadiusStep * RadiusStep);

        /// <summary>Grille de la classe d'emprise de <paramref name="radius"/> (construite à la première demande).</summary>
        public NavGrid Grid(long radius)
        {
            long cls = RadiusClass(radius);
            if (!_grids.TryGetValue(cls, out NavGrid? grid))
            {
                grid = new NavGrid(_minX, _minZ, _maxX, _maxZ, _obstacles, cls);
                _grids[cls] = grid;
            }
            return grid;
        }

        /// <summary>Le disque de rayon <paramref name="radius"/> va-t-il de A à B sans toucher d'obstacle ?</summary>
        public bool Clear(long ax, long az, long bx, long bz, long radius)
        {
            if (radius <= 0) radius = 1;
            foreach (ConvexPolygon o in _obstacles)
                if (o.OverlapsCapsule(ax, az, bx, bz, radius)) return false;
            return true;
        }

        /// <summary>
        /// Chemin (cases) de la classe de <paramref name="radius"/> entre les cases libres les plus proches des deux points ; null si
        /// aucun. Gardé en cache ; <paramref name="status"/> : <see cref="NavStatus.Found"/> ou <see cref="NavStatus.Partial"/>.
        /// </summary>
        public int[]? Cells(long radius, long fromX, long fromZ, long toX, long toZ, out NavStatus status)
        {
            NavGrid grid = Grid(radius);
            status = NavStatus.None;
            int start = grid.NearestFree(fromX, fromZ, StartReach), goal = grid.NearestFree(toX, toZ, GoalReach);
            if (start < 0 || goal < 0 || grid.Component(start) != grid.Component(goal)) return null;
            long key = checked((RadiusClass(radius) / RadiusStep << 40) | ((long)start << 20) | (long)goal);
            if (_cache.TryGetValue(key, out int[]? cached))
            {
                status = cached.Length > 0 && cached[cached.Length - 1] == goal ? NavStatus.Found : NavStatus.Partial;
                return cached;
            }
            status = _search.Find(grid, start, goal, MaxExpanded, _buffer, out int length);
            Searches++;
            LastExpanded = _search.LastExpanded;
            TotalExpanded = checked(TotalExpanded + _search.LastExpanded);
            if (status == NavStatus.None) return null;
            var cells = new int[length];
            Array.Copy(_buffer, cells, length);
            // Un chemin partiel qui n'atteint pas le but finit ailleurs : sa dernière case n'est pas « goal » (marque du statut en cache).
            if (status == NavStatus.Partial && length > 0 && cells[length - 1] == goal) status = NavStatus.Found;
            if (_cache.Count >= CacheSize) _cache.Clear();
            _cache[key] = cells;
            return cells;
        }

        /// <summary>
        /// Point à viser pour aller de (from) à (to) : le but lui-même si la ligne droite est libre (<see cref="NavSteer.Direct"/>) ; sinon
        /// la case la plus lointaine du chemin (au plus <see cref="Lookahead"/> cases devant) que l'emprise atteint en ligne droite (lissage),
        /// ou la première case du chemin ; <see cref="NavSteer.None"/> s'il n'y a pas de chemin (le point visé est alors le départ).
        /// </summary>
        public NavSteer Steer(long radius, long fromX, long fromZ, long toX, long toZ, out long aimX, out long aimZ)
        {
            aimX = toX;
            aimZ = toZ;
            if (Clear(fromX, fromZ, toX, toZ, radius)) return NavSteer.Direct;
            int[]? cells = Cells(radius, fromX, fromZ, toX, toZ, out NavStatus status);
            aimX = fromX;
            aimZ = fromZ;
            if (cells is null || cells.Length == 0) return NavSteer.None;
            NavGrid grid = Grid(radius);
            NavSteer kind = status == NavStatus.Found ? NavSteer.Path : NavSteer.Partial;
            // Arrivé dans la case du but (ou chemin d'une case) : le dernier bout se fait droit (le décor fait glisser).
            if (cells.Length == 1)
            {
                if (status == NavStatus.Found)
                {
                    aimX = toX;
                    aimZ = toZ;
                    return kind;
                }
                return NavSteer.None;
            }
            int last = Math.Min(cells.Length - 1, Lookahead);
            for (int k = last; k >= 1; k--)
            {
                long cx = grid.CellX(cells[k]), cz = grid.CellZ(cells[k]);
                if (Clear(fromX, fromZ, cx, cz, radius))
                {
                    aimX = cx;
                    aimZ = cz;
                    return kind;
                }
            }
            aimX = grid.CellX(cells[1]);
            aimZ = grid.CellZ(cells[1]);
            return kind;
        }

        /// <summary>
        /// Trajet complet lissé (pour l'affichage : PNJ et passants) : points de passage de (from) à (to), le but compris, chacun visible du
        /// précédent quand c'est possible (le plus lointain d'abord) ; vide si la ligne droite est libre (aller droit) ; null s'il n'y a pas
        /// de chemin. Un chemin partiel finit à la case la plus proche du but.
        /// </summary>
        public List<GroundVector>? Route(long radius, long fromX, long fromZ, long toX, long toZ)
        {
            var route = new List<GroundVector>();
            if (Clear(fromX, fromZ, toX, toZ, radius)) return route;
            int[]? cells = Cells(radius, fromX, fromZ, toX, toZ, out NavStatus status);
            if (cells is null) return null;
            NavGrid grid = Grid(radius);
            long ax = fromX, az = fromZ;
            int at = 0;
            while (true)
            {
                if (status == NavStatus.Found && Clear(ax, az, toX, toZ, radius)) break;
                if (at >= cells.Length - 1) break;
                int next = at + 1;
                for (int k = Math.Min(cells.Length - 1, at + 2 * Lookahead); k > at + 1; k--)
                    if (Clear(ax, az, grid.CellX(cells[k]), grid.CellZ(cells[k]), radius))
                    {
                        next = k;
                        break;
                    }
                ax = grid.CellX(cells[next]);
                az = grid.CellZ(cells[next]);
                route.Add(GroundVector.FromRaw(ax, az));
                at = next;
            }
            if (status == NavStatus.Found) route.Add(GroundVector.FromRaw(toX, toZ));
            return route;
        }
    }
}
