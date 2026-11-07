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

        /// <summary>
        /// Relecture de la PR #113 : budget de recherche épuisé sans s'écarter de la case de départ (poche dont la sortie s'éloigne du but
        /// plus loin que le budget) : il va droit vers le but et glisse, comme avant V2.8 (jamais bloqué pour toujours par la recherche).
        /// </summary>
        Straight,
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
        private readonly Dictionary<long, (int[] Cells, NavStatus Status)> _cache = new Dictionary<long, (int[] Cells, NavStatus Status)>();
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
        public int[]? Cells(long radius, long fromX, long fromZ, long toX, long toZ, out NavStatus status) =>
            Cells(radius, fromX, fromZ, toX, toZ, out status, out _);

        /// <summary>
        /// Comme <see cref="Cells(long, long, long, long, long, out NavStatus)"/> ; <paramref name="unattached"/> : vrai si aucune case libre
        /// n'est atteignable en ligne droite depuis le départ (le marcheur est coincé entre des obstacles plus près qu'une case).
        /// </summary>
        public int[]? Cells(long radius, long fromX, long fromZ, long toX, long toZ, out NavStatus status, out bool unattached)
        {
            NavGrid grid = Grid(radius);
            status = NavStatus.None;
            // Revue Codex de la PR #113 (tour 3) : départ et but ne se rattachent qu'à une case qu'ils atteignent en ligne droite avec leur
            // emprise (jamais à une case derrière un mur fin). Un but où l'emprise ne tient pas (dans un obstacle) garde la case libre la
            // plus proche : il n'y a pas de ligne droite à juger.
            int start = Attach(grid, fromX, fromZ, StartReach, radius);
            unattached = start < 0;
            int goal = Standable(toX, toZ, radius) ? Attach(grid, toX, toZ, GoalReach, radius) : grid.NearestFree(toX, toZ, GoalReach);
            if (start < 0 || goal < 0 || grid.Component(start) != grid.Component(goal)) return null;
            long key = checked((RadiusClass(radius) / RadiusStep << 40) | ((long)start << 20) | (long)goal);
            // Relecture de la PR #113 : le statut est gardé avec le chemin (un chemin tronqué finit sur une autre case que le but, mais
            // un chemin partiel peut aussi finir à côté) : la réponse ne dépend jamais du cache.
            if (_cache.TryGetValue(key, out (int[] Cells, NavStatus Status) cached))
            {
                status = cached.Status;
                return cached.Cells;
            }
            status = _search.Find(grid, start, goal, MaxExpanded, _buffer, out int length);
            Searches++;
            LastExpanded = _search.LastExpanded;
            TotalExpanded = checked(TotalExpanded + _search.LastExpanded);
            if (status == NavStatus.None) return null;
            var cells = new int[length];
            Array.Copy(_buffer, cells, length);
            if (_cache.Count >= CacheSize) _cache.Clear();
            _cache[key] = (cells, status);
            return cells;
        }

        private readonly int[] _candidates = new int[(2 * GoalReach + 1) * (2 * GoalReach + 1)];
        private readonly long[] _distances = new long[(2 * GoalReach + 1) * (2 * GoalReach + 1)];

        /// <summary>L'emprise tient-elle au point (x, z) sans chevaucher d'obstacle ?</summary>
        private bool Standable(long x, long z, long radius)
        {
            long r = Math.Max(1, radius);
            foreach (ConvexPolygon o in _obstacles)
                if (o.OverlapsCircle(x, z, r)) return false;
            return true;
        }

        /// <summary>
        /// Revue Codex de la PR #113 (tour 3) : case libre la plus proche de (x, z), dans un carré de ±<paramref name="reach"/> cases, que
        /// l'emprise atteint en ligne droite depuis (x, z) sans toucher d'obstacle : les cases libres sont examinées par distance croissante
        /// au point, puis par indice (départage stable) ; −1 s'il n'y en a pas. Aucune allocation (tampons du navigateur).
        /// </summary>
        private int Attach(NavGrid grid, long x, long z, int reach, long radius)
        {
            int col = grid.ColumnOf(x), row = grid.RowOf(z), n = 0;
            for (int r = Math.Max(0, row - reach); r <= Math.Min(grid.Height - 1, row + reach); r++)
                for (int c = Math.Max(0, col - reach); c <= Math.Min(grid.Width - 1, col + reach); c++)
                {
                    int k = r * grid.Width + c;
                    if (!grid.IsFree(k)) continue;
                    _candidates[n] = k;
                    _distances[n] = Planar.LengthSquared(checked(grid.CenterX(c) - x), checked(grid.CenterZ(r) - z));
                    n++;
                }
            // Tri par sélection, au fil de l'examen (au plus 81 candidats) : distance, puis indice de case.
            for (int i = 0; i < n; i++)
            {
                int best = i;
                for (int j = i + 1; j < n; j++)
                    if (_distances[j] < _distances[best] || (_distances[j] == _distances[best] && _candidates[j] < _candidates[best])) best = j;
                (_candidates[i], _candidates[best]) = (_candidates[best], _candidates[i]);
                (_distances[i], _distances[best]) = (_distances[best], _distances[i]);
                int k = _candidates[i];
                if (Clear(x, z, grid.CellX(k), grid.CellZ(k), radius)) return k;
            }
            return -1;
        }

        /// <summary>
        /// Point à viser pour aller de (from) à (to) : le but lui-même si la ligne droite est libre (<see cref="NavSteer.Direct"/>) ; sinon
        /// la case la plus lointaine du chemin (au plus <see cref="Lookahead"/> cases devant) que l'emprise atteint en ligne droite (lissage),
        /// ou la première case du chemin ; <see cref="NavSteer.None"/> s'il n'y a pas de chemin (le point visé est alors le départ) ;
        /// <see cref="NavSteer.Straight"/> (le but) si la recherche, bornée, n'a rien trouvé de plus proche que la case de départ.
        /// </summary>
        public NavSteer Steer(long radius, long fromX, long fromZ, long toX, long toZ, out long aimX, out long aimZ)
        {
            aimX = toX;
            aimZ = toZ;
            if (Clear(fromX, fromZ, toX, toZ, radius)) return NavSteer.Direct;
            int[]? cells = Cells(radius, fromX, fromZ, toX, toZ, out NavStatus status, out bool unattached);
            aimX = fromX;
            aimZ = fromZ;
            // Revue Codex de la PR #113 (tour 3) : coincé à moins d'une case d'obstacles (aucune case atteignable en ligne droite) : il va
            // droit et glisse, comme avant V2.8, au lieu de rester immobile pour toujours.
            if (unattached)
            {
                aimX = toX;
                aimZ = toZ;
                return NavSteer.Straight;
            }
            if (cells is null || cells.Length == 0) return NavSteer.None;
            NavGrid grid = Grid(radius);
            NavSteer kind = status == NavStatus.Found ? NavSteer.Path : NavSteer.Partial;
            // Arrivé dans la case du but (ou chemin d'une case) : le dernier bout se fait droit (le décor fait glisser).
            if (cells.Length == 1)
            {
                aimX = toX;
                aimZ = toZ;
                return status == NavStatus.Found ? kind : NavSteer.Straight;
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
            // Revue Codex de la PR #113 (tour 2) : rien de visible devant : il rejoint d'abord le centre de sa case de départ s'il le voit
            // (de là, chaque arête du chemin est ouverte pour son emprise), sinon la case suivante (le décor le fait glisser).
            int fallback = Clear(fromX, fromZ, grid.CellX(cells[0]), grid.CellZ(cells[0]), radius) ? 0 : 1;
            aimX = grid.CellX(cells[fallback]);
            aimZ = grid.CellZ(cells[fallback]);
            if (aimX == fromX && aimZ == fromZ)
            {
                aimX = grid.CellX(cells[1]);
                aimZ = grid.CellZ(cells[1]);
            }
            return kind;
        }

        /// <summary>
        /// Trajet complet lissé (pour l'affichage : PNJ et passants) : points de passage de (from) à (to), le but compris, chacun visible du
        /// précédent quand c'est possible (le plus lointain d'abord) ; vide si la ligne droite est libre (aller droit) ; null s'il n'y a pas
        /// de chemin, ou si la recherche bornée n'a rien trouvé de plus proche que la case de départ. Un chemin partiel finit à la case la
        /// plus proche du but.
        /// </summary>
        public List<GroundVector>? Route(long radius, long fromX, long fromZ, long toX, long toZ) => Route(radius, fromX, fromZ, toX, toZ, out _);

        /// <summary>Comme <see cref="Route(long, long, long, long, long)"/> ; <paramref name="complete"/> : faux pour un chemin partiel (à reprendre
        /// depuis son bout).</summary>
        public List<GroundVector>? Route(long radius, long fromX, long fromZ, long toX, long toZ, out bool complete)
        {
            var route = new List<GroundVector>();
            complete = true;
            if (Clear(fromX, fromZ, toX, toZ, radius)) return route;
            int[]? cells = Cells(radius, fromX, fromZ, toX, toZ, out NavStatus status);
            if (cells is null) return null;
            complete = status == NavStatus.Found;
            // Partiel sans s'écarter de la case de départ : pas de trajet (null) ; le marcheur de l'affichage applique son repli
            // (rester, ou aller droit : Walker.StraightWhenBlocked).
            if (!complete && cells.Length <= 1) return null;
            NavGrid grid = Grid(radius);
            long ax = fromX, az = fromZ;
            // Revue Codex de la PR #113 (tour 2) : « at » est la case dont le marcheur occupe le centre (−1 au départ : il est à sa place,
            // pas au centre de sa case). Faute de raccourci visible, il passe par la case suivante : depuis un centre, l'arête est ouverte
            // pour son emprise ; depuis le départ, il rejoint d'abord le centre de sa case de départ.
            int at = -1;
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
                if (next == 0 && grid.CellX(cells[0]) == ax && grid.CellZ(cells[0]) == az)
                {
                    at = 0;
                    continue;
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
