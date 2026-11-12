using System;
using System.Collections.Generic;

namespace EdS.Domain.Space
{
    /// <summary>
    /// Sous-lot V2.26 (secteurs de simulation, PLAN_GRANDE_CARTE.md § 2.4, point 3) : rangement des obstacles d'un espace par cases carrées
    /// (16 m par défaut, doublées tant que la grille dépasse <see cref="MaxBuckets"/> cases), pour que le coût d'un test local (un pas, une
    /// ligne de vue, une gélule) ne grandisse pas avec le nombre d'obstacles (256 au plus depuis V2.26). Chaque obstacle est inscrit dans
    /// toutes les cases que touche sa boîte englobante.
    /// <para>Les réponses sont <b>exactement</b> celles d'un parcours de tous les obstacles : une requête rend un sur-ensemble des obstacles dont
    /// la boîte touche la boîte demandée (bords compris), et les tests exacts de <see cref="ConvexPolygon"/> écartent déjà tout obstacle dont la
    /// boîte ne touche pas celle du disque ou du segment. <see cref="Candidates"/> rend les indices <b>dans l'ordre</b> des obstacles, pour les
    /// parcours dont le résultat dépend de l'ordre (dégagement d'un pas).</para>
    /// <para>Aucune allocation par requête (marques par numéro de génération) ; une instance n'est pas réentrante (un seul moteur, un seul fil).</para>
    /// </summary>
    public sealed class ObstacleIndex
    {
        /// <summary>Côté d'une case par défaut : 16 m (brut).</summary>
        public const long DefaultBucket = 160_000;

        /// <summary>Nombre maximal de cases de la grille (au-delà, la case double).</summary>
        public const int MaxBuckets = 65_536;

        private readonly ConvexPolygon[] _obstacles;
        private readonly long _originX, _originZ, _bucket;
        private readonly int _columns, _rows;
        private readonly int[] _start;
        private readonly int[] _items;
        private readonly int[] _stamp;
        private int _generation;

        public IReadOnlyList<ConvexPolygon> Obstacles => _obstacles;
        public int Count => _obstacles.Length;

        /// <summary>Côté d'une case, en unités brutes.</summary>
        public long Bucket => _bucket;
        public int Columns => _columns;
        public int Rows => _rows;

        public ObstacleIndex(IReadOnlyList<ConvexPolygon> obstacles)
        {
            if (obstacles is null) throw new ArgumentNullException(nameof(obstacles));
            _obstacles = new ConvexPolygon[obstacles.Count];
            for (int i = 0; i < _obstacles.Length; i++) _obstacles[i] = obstacles[i] ?? throw new ArgumentNullException(nameof(obstacles));
            _stamp = new int[_obstacles.Length];
            _bucket = DefaultBucket;
            if (_obstacles.Length == 0)
            {
                _columns = _rows = 0;
                _start = new int[1];
                _items = Array.Empty<int>();
                return;
            }
            long minX = long.MaxValue, minZ = long.MaxValue, maxX = long.MinValue, maxZ = long.MinValue;
            foreach (ConvexPolygon o in _obstacles)
            {
                minX = Math.Min(minX, o.MinX);
                minZ = Math.Min(minZ, o.MinZ);
                maxX = Math.Max(maxX, o.MaxX);
                maxZ = Math.Max(maxZ, o.MaxZ);
            }
            _originX = minX;
            _originZ = minZ;
            while (checked((Span(minX, maxX, _bucket)) * Span(minZ, maxZ, _bucket)) > MaxBuckets) _bucket = checked(_bucket * 2);
            _columns = (int)Span(minX, maxX, _bucket);
            _rows = (int)Span(minZ, maxZ, _bucket);
            // Rangement compact : nombre d'obstacles par case, puis positions (ordre croissant des indices dans chaque case).
            var counts = new int[checked(_columns * _rows + 1)];
            for (int i = 0; i < _obstacles.Length; i++)
                ForBuckets(_obstacles[i].MinX, _obstacles[i].MinZ, _obstacles[i].MaxX, _obstacles[i].MaxZ, (b, _) => counts[b + 1]++, 0);
            for (int b = 1; b < counts.Length; b++) counts[b] = checked(counts[b] + counts[b - 1]);
            _start = counts;
            _items = new int[_start[_start.Length - 1]];
            var fill = new int[_columns * _rows];
            for (int i = 0; i < _obstacles.Length; i++)
                ForBuckets(_obstacles[i].MinX, _obstacles[i].MinZ, _obstacles[i].MaxX, _obstacles[i].MaxZ, (b, index) => _items[_start[b] + fill[b]++] = index, i);
        }

        /// <summary>Index des polygones d'un espace (tous ses obstacles, bas compris).</summary>
        public static ObstacleIndex For(IReadOnlyList<ConvexPolygon> obstacles) => new ObstacleIndex(obstacles);

        private static long Span(long lo, long hi, long size) => (hi - lo) / size + 1;

        private static long FloorDiv(long a, long b) => a >= 0 ? a / b : -((-a + b - 1) / b);

        private void Range(long minX, long minZ, long maxX, long maxZ, out int c0, out int r0, out int c1, out int r1)
        {
            c0 = (int)Math.Max(0, Math.Min(_columns, FloorDiv(minX - _originX, _bucket)));
            r0 = (int)Math.Max(0, Math.Min(_rows, FloorDiv(minZ - _originZ, _bucket)));
            c1 = (int)Math.Max(-1, Math.Min(_columns - 1, FloorDiv(maxX - _originX, _bucket)));
            r1 = (int)Math.Max(-1, Math.Min(_rows - 1, FloorDiv(maxZ - _originZ, _bucket)));
        }

        private void ForBuckets(long minX, long minZ, long maxX, long maxZ, Action<int, int> action, int index)
        {
            Range(minX, minZ, maxX, maxZ, out int c0, out int r0, out int c1, out int r1);
            for (int r = r0; r <= r1; r++)
                for (int c = c0; c <= c1; c++) action(r * _columns + c, index);
        }

        private int NextGeneration()
        {
            if (++_generation == int.MaxValue)
            {
                Array.Clear(_stamp, 0, _stamp.Length);
                _generation = 1;
            }
            return _generation;
        }

        /// <summary>
        /// Indices (croissants) des obstacles dont la boîte peut toucher la boîte fermée [min, max] (sur-ensemble exact), écrits dans
        /// <paramref name="output"/> (au moins <see cref="Count"/> places) ; renvoie leur nombre.
        /// </summary>
        public int Candidates(long minX, long minZ, long maxX, long maxZ, int[] output)
        {
            if (output is null) throw new ArgumentNullException(nameof(output));
            if (_obstacles.Length == 0 || maxX < _originX || maxZ < _originZ) return 0;
            int gen = NextGeneration(), n = 0;
            Range(minX, minZ, maxX, maxZ, out int c0, out int r0, out int c1, out int r1);
            for (int r = r0; r <= r1; r++)
                for (int c = c0; c <= c1; c++)
                {
                    int b = r * _columns + c;
                    for (int k = _start[b]; k < _start[b + 1]; k++)
                    {
                        int i = _items[k];
                        if (_stamp[i] == gen) continue;
                        _stamp[i] = gen;
                        // Tri par insertion (quelques obstacles par case) : ordre des obstacles.
                        int at = n++;
                        while (at > 0 && output[at - 1] > i)
                        {
                            output[at] = output[at - 1];
                            at--;
                        }
                        output[at] = i;
                    }
                }
            return n;
        }

        /// <summary>Un obstacle chevauche-t-il strictement le disque (centre P, rayon r) ? (même réponse que le parcours de tous)</summary>
        public bool AnyOverlapsCircle(long px, long pz, long r)
        {
            if (_obstacles.Length == 0) return false;
            int gen = NextGeneration();
            Range(checked(px - r), checked(pz - r), checked(px + r), checked(pz + r), out int c0, out int r0, out int c1, out int r1);
            for (int row = r0; row <= r1; row++)
                for (int c = c0; c <= c1; c++)
                {
                    int b = row * _columns + c;
                    for (int k = _start[b]; k < _start[b + 1]; k++)
                    {
                        int i = _items[k];
                        if (_stamp[i] == gen) continue;
                        _stamp[i] = gen;
                        if (_obstacles[i].OverlapsCircle(px, pz, r)) return true;
                    }
                }
            return false;
        }

        /// <summary>Un obstacle chevauche-t-il la gélule (disque de rayon r balayé de A à B) ? (même réponse que le parcours de tous)</summary>
        public bool AnyOverlapsCapsule(long ax, long az, long bx, long bz, long r)
        {
            if (_obstacles.Length == 0) return false;
            int gen = NextGeneration();
            Range(checked(Math.Min(ax, bx) - r), checked(Math.Min(az, bz) - r), checked(Math.Max(ax, bx) + r), checked(Math.Max(az, bz) + r),
                out int c0, out int r0, out int c1, out int r1);
            for (int row = r0; row <= r1; row++)
                for (int c = c0; c <= c1; c++)
                {
                    int b = row * _columns + c;
                    for (int k = _start[b]; k < _start[b + 1]; k++)
                    {
                        int i = _items[k];
                        if (_stamp[i] == gen) continue;
                        _stamp[i] = gen;
                        if (_obstacles[i].OverlapsCapsule(ax, az, bx, bz, r)) return true;
                    }
                }
            return false;
        }

        /// <summary>Un obstacle touche-t-il le segment fermé [A, B] ? (même réponse que le parcours de tous)</summary>
        public bool AnyIntersectsSegment(long ax, long az, long bx, long bz)
        {
            if (_obstacles.Length == 0) return false;
            int gen = NextGeneration();
            Range(Math.Min(ax, bx), Math.Min(az, bz), Math.Max(ax, bx), Math.Max(az, bz), out int c0, out int r0, out int c1, out int r1);
            for (int row = r0; row <= r1; row++)
                for (int c = c0; c <= c1; c++)
                {
                    int b = row * _columns + c;
                    for (int k = _start[b]; k < _start[b + 1]; k++)
                    {
                        int i = _items[k];
                        if (_stamp[i] == gen) continue;
                        _stamp[i] = gen;
                        if (_obstacles[i].IntersectsSegment(ax, az, bx, bz)) return true;
                    }
                }
            return false;
        }
    }
}
