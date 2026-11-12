using System;
using System.Collections.Generic;

namespace EdS.Domain.Space
{
    /// <summary>
    /// Sous-lot V2.8 (recherche de chemin) : grille de navigation d'une aire et de ses obstacles pour une emprise donnée, en unités brutes
    /// (0,0001 m), sans flottant. Une case est <b>libre</b> si un disque de rayon <see cref="Clearance"/> (l'emprise, plus un quart de case
    /// de marge) posé en son centre reste dans l'aire et ne chevauche aucun obstacle (test exact de <see cref="ConvexPolygon.OverlapsCircle"/>).
    /// Les cases libres sont reliées en 8-voisinage par des arêtes ouvertes (la gélule de l'emprise d'un centre à l'autre ne touche aucun
    /// obstacle, revue Codex de la PR #113) et sans couper un coin (une diagonale exige ses deux cases orthogonales libres) ; les
    /// composantes connexes sont calculées une fois : « aucun chemin » se sait sans chercher. Immuable une fois construite.
    /// </summary>
    public sealed class NavGrid
    {
        /// <summary>Taille de case par défaut : 0,5 m.</summary>
        public const long DefaultCell = 5_000;

        /// <summary>
        /// Nombre maximal de cases : 524 288 depuis V2.26 (262 144, soit 512 × 512, avant) ; un secteur de 256 m et sa marge de 32 m (320 m
        /// de côté, 409 600 cases) tient en cases de 0,5 m. Au-delà, la case double de taille. Une aire de 256 m de côté au plus garde
        /// exactement sa grille d'avant.
        /// </summary>
        public const int MaxCells = 524_288;

        private readonly bool[] _free;
        private readonly int[] _component;

        /// <summary>
        /// Revue Codex de la PR #113 (tour 2) : arêtes fermées, un bit par direction (0 à 7) : le disque de rayon <see cref="Radius"/> balayé
        /// d'un centre de case libre au centre voisin libre touche un obstacle (un obstacle fin ou minuscule entre deux centres libres).
        /// </summary>
        private readonly byte[] _closed;
        private readonly ConvexPolygon[] _obstacles;

        /// <summary>Sous-lot V2.26 : obstacles rangés par cases (tests de gélule sans parcourir tout le décor).</summary>
        private readonly ObstacleIndex _index;

        public long OriginX { get; }
        public long OriginZ { get; }
        public long Cell { get; }
        public int Width { get; }
        public int Height { get; }

        /// <summary>Rayon de l'emprise pour laquelle la grille est faite (brut).</summary>
        public long Radius { get; }

        /// <summary>Rayon du disque testé au centre d'une case : <see cref="Radius"/> + un quart de case.</summary>
        public long Clearance { get; }

        public int Count => _free.Length;
        public int ComponentCount { get; }

        public NavGrid(long minX, long minZ, long maxX, long maxZ, IReadOnlyList<ConvexPolygon> obstacles, long radius)
            : this(minX, minZ, maxX, maxZ, obstacles, radius, null) { }

        /// <summary>Sous-lot V2.26 : avec l'index des mêmes obstacles (partagé par les grilles d'un navigateur), ou null pour en construire un.</summary>
        public NavGrid(long minX, long minZ, long maxX, long maxZ, IReadOnlyList<ConvexPolygon> obstacles, long radius, ObstacleIndex? index)
        {
            if (obstacles is null) throw new ArgumentNullException(nameof(obstacles));
            if (minX >= maxX || minZ >= maxZ) throw new ArgumentException("aire vide.", nameof(minX));
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            _obstacles = new ConvexPolygon[obstacles.Count];
            for (int i = 0; i < _obstacles.Length; i++) _obstacles[i] = obstacles[i] ?? throw new ArgumentNullException(nameof(obstacles));
            if (index != null && index.Count != _obstacles.Length) throw new ArgumentException("index d'autres obstacles.", nameof(index));
            _index = index ?? new ObstacleIndex(_obstacles);
            long spanX = checked(maxX - minX), spanZ = checked(maxZ - minZ);
            long cell = DefaultCell;
            while (checked(CeilDiv(spanX, cell) * CeilDiv(spanZ, cell)) > MaxCells) cell = checked(cell * 2);
            Cell = cell;
            OriginX = minX;
            OriginZ = minZ;
            Width = (int)CeilDiv(spanX, cell);
            Height = (int)CeilDiv(spanZ, cell);
            Radius = radius;
            Clearance = checked(radius + cell / 4);
            _free = new bool[checked(Width * Height)];
            long loX = checked(minX + Clearance), hiX = checked(maxX - Clearance), loZ = checked(minZ + Clearance), hiZ = checked(maxZ - Clearance);
            for (int row = 0; row < Height; row++)
            {
                long cz = CenterZ(row);
                if (cz < loZ || cz > hiZ) continue;
                for (int col = 0; col < Width; col++)
                {
                    long cx = CenterX(col);
                    _free[row * Width + col] = cx >= loX && cx <= hiX;
                }
            }
            // Chaque obstacle ne touche que les cases de sa boîte élargie.
            foreach (ConvexPolygon o in _obstacles)
            {
                int c0 = Math.Max(0, ColumnOf(checked(o.MinX - Clearance)) - 1), c1 = Math.Min(Width - 1, ColumnOf(checked(o.MaxX + Clearance)) + 1);
                int r0 = Math.Max(0, RowOf(checked(o.MinZ - Clearance)) - 1), r1 = Math.Min(Height - 1, RowOf(checked(o.MaxZ + Clearance)) + 1);
                for (int row = r0; row <= r1; row++)
                    for (int col = c0; col <= c1; col++)
                    {
                        int k = row * Width + col;
                        if (_free[k] && o.OverlapsCircle(CenterX(col), CenterZ(row), Clearance)) _free[k] = false;
                    }
            }
            // Revue Codex de la PR #113 (tour 2) : chaque arête entre deux cases libres (orthogonale ou diagonale) est jugée contre chaque
            // obstacle proche : la gélule de l'emprise (rayon Radius, au moins une unité brute) d'un centre à l'autre ne doit rien chevaucher
            // (test exact en entiers). Un obstacle plus fin ou plus petit qu'une case ne laisse donc plus passer une arête au travers.
            _closed = new byte[_free.Length];
            long sweep = Math.Max(1, radius);
            long reach = checked(sweep + 2 * cell);
            foreach (ConvexPolygon o in _obstacles)
            {
                int c0 = Math.Max(0, ColumnOf(checked(o.MinX - reach))), c1 = Math.Min(Width - 1, ColumnOf(checked(o.MaxX + reach)));
                int r0 = Math.Max(0, RowOf(checked(o.MinZ - reach))), r1 = Math.Min(Height - 1, RowOf(checked(o.MaxZ + reach)));
                for (int row = r0; row <= r1; row++)
                    for (int col = c0; col <= c1; col++)
                    {
                        int k = row * Width + col;
                        if (!_free[k]) continue;
                        for (int d = 0; d < 4; d++)   // est, nord-est, nord, nord-ouest ; l'arête opposée (d + 4) est celle du voisin
                        {
                            int nc = col + StepX[d], nr = row + StepZ[d];
                            if (nc < 0 || nr < 0 || nc >= Width || nr >= Height) continue;
                            int nb = nr * Width + nc;
                            if (!_free[nb] || (_closed[k] & (1 << d)) != 0) continue;
                            if (!o.OverlapsCapsule(CenterX(col), CenterZ(row), CenterX(nc), CenterZ(nr), sweep)) continue;
                            _closed[k] |= (byte)(1 << d);
                            _closed[nb] |= (byte)(1 << (d + 4));
                        }
                    }
            }
            // Composantes connexes (même voisinage que la recherche), numérotées dans l'ordre des cases.
            _component = new int[_free.Length];
            var queue = new int[_free.Length];
            int components = 0;
            for (int s = 0; s < _free.Length; s++)
            {
                if (!_free[s] || _component[s] != 0) continue;
                components++;
                int head = 0, tail = 0;
                queue[tail++] = s;
                _component[s] = components;
                while (head < tail)
                {
                    int c = queue[head++];
                    for (int d = 0; d < 8; d++)
                    {
                        int nb = Neighbor(c, d);
                        if (nb < 0 || _component[nb] != 0) continue;
                        _component[nb] = components;
                        queue[tail++] = nb;
                    }
                }
            }
            ComponentCount = components;
        }

        private static long CeilDiv(long a, long b) => (a + b - 1) / b;

        /// <summary>Décalages des huit voisins, dans un ordre fixe : est, nord-est, nord, nord-ouest, ouest, sud-ouest, sud, sud-est.</summary>
        private static readonly int[] StepX = { 1, 1, 0, -1, -1, -1, 0, 1 };
        private static readonly int[] StepZ = { 0, 1, 1, 1, 0, -1, -1, -1 };

        /// <summary>La direction <paramref name="d"/> (0 à 7) est-elle diagonale ?</summary>
        public static bool Diagonal(int d) => (d & 1) == 1;

        /// <summary>
        /// Voisin libre de la case <paramref name="c"/> dans la direction <paramref name="d"/>, par une arête ouverte (la gélule de l'emprise
        /// d'un centre à l'autre ne touche aucun obstacle) et, en diagonale, sans couper de coin (les deux cases orthogonales libres) ; −1 sinon.
        /// </summary>
        public int Neighbor(int c, int d)
        {
            int col = c % Width, row = c / Width;
            int nc = col + StepX[d], nr = row + StepZ[d];
            if (nc < 0 || nr < 0 || nc >= Width || nr >= Height) return -1;
            int k = nr * Width + nc;
            if (!_free[k] || (_closed[c] & (1 << d)) != 0) return -1;
            if (Diagonal(d) && (!_free[row * Width + nc] || !_free[nr * Width + col])) return -1;
            return k;
        }

        public long CenterX(int column) => checked(OriginX + column * Cell + Cell / 2);
        public long CenterZ(int row) => checked(OriginZ + row * Cell + Cell / 2);
        public long CellX(int cell) => CenterX(cell % Width);
        public long CellZ(int cell) => CenterZ(cell / Width);

        /// <summary>Colonne (ou ligne) d'une coordonnée, bornée à la grille.</summary>
        public int ColumnOf(long x) => (int)Math.Max(0, Math.Min(Width - 1, FloorDiv(checked(x - OriginX), Cell)));
        public int RowOf(long z) => (int)Math.Max(0, Math.Min(Height - 1, FloorDiv(checked(z - OriginZ), Cell)));

        private static long FloorDiv(long a, long b) => a >= 0 ? a / b : -((-a + b - 1) / b);

        public bool IsFree(int cell) => cell >= 0 && cell < _free.Length && _free[cell];

        /// <summary>Composante connexe d'une case libre (à partir de 1) ; 0 pour une case pleine.</summary>
        public int Component(int cell) => _component[cell];

        /// <summary>
        /// Case libre la plus proche du point (x, z) dans un carré de ±<paramref name="reach"/> cases autour de la sienne : distance au
        /// centre, puis indice de case ; −1 s'il n'y en a pas. Exact (entiers).
        /// </summary>
        public int NearestFree(long x, long z, int reach)
        {
            int col = ColumnOf(x), row = RowOf(z);
            int best = -1;
            long bestD = long.MaxValue;
            for (int r = Math.Max(0, row - reach); r <= Math.Min(Height - 1, row + reach); r++)
                for (int c = Math.Max(0, col - reach); c <= Math.Min(Width - 1, col + reach); c++)
                {
                    int k = r * Width + c;
                    if (!_free[k]) continue;
                    long d = Planar.LengthSquared(checked(CenterX(c) - x), checked(CenterZ(r) - z));
                    if (d < bestD || (d == bestD && k < best))
                    {
                        bestD = d;
                        best = k;
                    }
                }
            return best;
        }

        /// <summary>
        /// Le disque de l'emprise (<paramref name="radius"/>) balayé de A à B ne chevauche-t-il aucun obstacle ? (test exact des gélules ;
        /// l'aire elle-même est convexe : deux points dedans, le segment l'est aussi.)
        /// </summary>
        public bool Clear(long ax, long az, long bx, long bz, long radius)
        {
            if (radius <= 0) radius = 1;
            return !_index.AnyOverlapsCapsule(ax, az, bx, bz, radius);
        }
    }

    /// <summary>Issue d'une recherche de chemin.</summary>
    public enum NavStatus
    {
        /// <summary>Aucun chemin (cases de départ ou d'arrivée introuvables, composantes différentes) : rester sur place.</summary>
        None,

        /// <summary>Chemin complet trouvé.</summary>
        Found,

        /// <summary>Budget de cases explorées épuisé : chemin jusqu'à la case explorée la plus proche du but (heuristique, puis coût, puis indice).</summary>
        Partial,
    }

    /// <summary>
    /// Sous-lot V2.8 : recherche A* sur une <see cref="NavGrid"/>, en entiers (coût 10 en ligne droite, 14 en diagonale ; heuristique
    /// octile, admissible et cohérente). Départage stable : plus petit f, puis plus petit h, puis plus petit indice de case. Le nombre de
    /// cases développées est borné par appel ; tous les tableaux sont alloués une fois (taille de la plus grande grille vue), puis réutilisés
    /// d'un appel à l'autre grâce à un numéro de génération : aucune allocation par recherche.
    /// </summary>
    public sealed class NavSearch
    {
        public const int Straight = 10, DiagonalCost = 14;

        private int[] _g = Array.Empty<int>();
        private int[] _parent = Array.Empty<int>();
        private int[] _seen = Array.Empty<int>();
        private int[] _closed = Array.Empty<int>();
        private long[] _heap = Array.Empty<long>();
        private int _heapCount;
        private int _generation;

        /// <summary>Cases développées par la dernière recherche (mesure de performance, sans effet sur le résultat).</summary>
        public int LastExpanded { get; private set; }

        private void Ensure(int cells, int maxExpanded)
        {
            if (_g.Length < cells)
            {
                _g = new int[cells];
                _parent = new int[cells];
                _seen = new int[cells];
                _closed = new int[cells];
                _generation = 0;
            }
            int heap = checked(maxExpanded * 8 + 8);
            if (_heap.Length < heap) _heap = new long[heap];
        }

        private static int Heuristic(NavGrid grid, int a, int b)
        {
            int dx = Math.Abs(a % grid.Width - b % grid.Width), dz = Math.Abs(a / grid.Width - b / grid.Width);
            int lo = Math.Min(dx, dz), hi = Math.Max(dx, dz);
            return checked(DiagonalCost * lo + Straight * (hi - lo));
        }

        /// <summary>
        /// Chemin de <paramref name="start"/> à <paramref name="goal"/> (cases libres de la même grille), écrit dans
        /// <paramref name="path"/> du départ à l'arrivée ; <paramref name="length"/> cases (0 si <see cref="NavStatus.None"/>). Au plus
        /// <paramref name="maxExpanded"/> cases développées ; <paramref name="path"/> doit pouvoir tenir le chemin (sinon il est tronqué au
        /// départ : seules les premières cases comptent pour avancer, et le statut est alors <see cref="NavStatus.Partial"/>).
        /// </summary>
        public NavStatus Find(NavGrid grid, int start, int goal, int maxExpanded, int[] path, out int length)
        {
            if (grid is null) throw new ArgumentNullException(nameof(grid));
            if (path is null || path.Length == 0) throw new ArgumentException("tampon de chemin vide.", nameof(path));
            if (maxExpanded < 1 || maxExpanded > MaxExpandedLimit) throw new ArgumentOutOfRangeException(nameof(maxExpanded));
            length = 0;
            LastExpanded = 0;
            if (!grid.IsFree(start) || !grid.IsFree(goal) || grid.Component(start) != grid.Component(goal)) return NavStatus.None;
            Ensure(grid.Count, maxExpanded);
            if (++_generation == int.MaxValue)
            {
                Array.Clear(_seen, 0, _seen.Length);
                Array.Clear(_closed, 0, _closed.Length);
                _generation = 1;
            }
            int gen = _generation;
            _heapCount = 0;
            _g[start] = 0;
            _parent[start] = -1;
            _seen[start] = gen;
            int h0 = Heuristic(grid, start, goal);
            Push(Key(h0, h0, start));
            int best = start, bestH = h0, bestG = 0;
            int expanded = 0;
            bool found = false;
            while (_heapCount > 0)
            {
                long key = Pop();
                int c = (int)(key & CellMask);
                if (_closed[c] == gen) continue;
                _closed[c] = gen;
                if (c == goal)
                {
                    found = true;
                    break;
                }
                int hc = Heuristic(grid, c, goal);
                if (hc < bestH || (hc == bestH && (_g[c] < bestG || (_g[c] == bestG && c < best))))
                {
                    best = c;
                    bestH = hc;
                    bestG = _g[c];
                }
                if (expanded == maxExpanded) break;
                expanded++;
                for (int d = 0; d < 8; d++)
                {
                    int nb = grid.Neighbor(c, d);
                    if (nb < 0 || _closed[nb] == gen) continue;
                    int g = checked(_g[c] + (NavGrid.Diagonal(d) ? DiagonalCost : Straight));
                    if (_seen[nb] == gen && _g[nb] <= g) continue;
                    _seen[nb] = gen;
                    _g[nb] = g;
                    _parent[nb] = c;
                    int h = Heuristic(grid, nb, goal);
                    if (_heapCount == _heap.Length) break;   // tas plein (borne : 8 poussées par case développée) : jamais atteint
                    Push(Key(checked(g + h), h, nb));
                }
            }
            LastExpanded = expanded;
            int end = found ? goal : best;
            // Longueur, puis écriture à l'endroit (les premières cases d'abord ; tronqué à la taille du tampon).
            int count = 0;
            for (int c = end; c >= 0; c = _parent[c]) count++;
            int at = count - 1;
            for (int c = end; c >= 0; c = _parent[c], at--)
                if (at < path.Length) path[at] = c;
            length = Math.Min(count, path.Length);
            // Relecture de la PR #113 : un chemin tronqué au tampon n'atteint pas le but dans ce qui est rendu : il est partiel.
            return found && count <= path.Length ? NavStatus.Found : NavStatus.Partial;
        }

        private const long CellMask = (1L << 20) - 1;

        /// <summary>
        /// Clé de tas : f sur 22 bits, h sur 21, case sur 20 (moins de 2²⁰ cases depuis V2.26 ; avant : 23, 22 et 18 bits) ; l'ordre des clés
        /// est l'ordre de départage (f, puis h, puis case), inchangé. Bornes : une aire de ±4 096 m a au plus 16 384 cases de côté, donc
        /// h ≤ 14 × 16 384 &lt; 2²¹ ; g ≤ 14 × (<see cref="MaxExpandedLimit"/> + 1), donc f = g + h &lt; 2²².
        /// </summary>
        private static long Key(int f, int h, int cell) => checked(((long)f << 41) | ((long)h << 20) | (long)cell);

        /// <summary>Sous-lot V2.26 : plus grand nombre de cases développées admis par recherche (borne de la clé de tas).</summary>
        public const int MaxExpandedLimit = 131_072;

        private void Push(long key)
        {
            int i = _heapCount++;
            _heap[i] = key;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (_heap[p] <= key) break;
                _heap[i] = _heap[p];
                i = p;
            }
            _heap[i] = key;
        }

        private long Pop()
        {
            long top = _heap[0];
            long last = _heap[--_heapCount];
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1;
                if (l >= _heapCount) break;
                int r = l + 1;
                int m = r < _heapCount && _heap[r] < _heap[l] ? r : l;
                if (_heap[m] >= last) break;
                _heap[i] = _heap[m];
                i = m;
            }
            if (_heapCount > 0) _heap[i] = last;
            return top;
        }
    }
}
