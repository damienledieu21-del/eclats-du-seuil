using System;
using System.Collections.Generic;

namespace EdS.Domain.Space
{
    /// <summary>
    /// Sous-lot 11.4 : polygone convexe du décor, en coordonnées brutes, sommets dans le sens trigonométrique du plan (x, z) (un polygone
    /// donné dans l'autre sens est retourné). Immuable. Construit seulement à partir de sommets validés (<see cref="IsStrictlyConvex"/>).
    /// </summary>
    public sealed class ConvexPolygon
    {
        private readonly long[] _x;
        private readonly long[] _z;

        /// <summary>Boîte englobante, pour écarter sans calcul un disque éloigné.</summary>
        public long MinX { get; }
        public long MinZ { get; }
        public long MaxX { get; }
        public long MaxZ { get; }

        public int Count => _x.Length;
        public long X(int i) => _x[i];
        public long Z(int i) => _z[i];

        public ConvexPolygon(IReadOnlyList<GroundVector> points)
        {
            if (points is null) throw new ArgumentNullException(nameof(points));
            if (!IsStrictlyConvex(points, out bool clockwise)) throw new ArgumentException("polygone non strictement convexe.", nameof(points));
            int n = points.Count;
            _x = new long[n];
            _z = new long[n];
            for (int i = 0; i < n; i++)
            {
                GroundVector p = points[clockwise ? n - 1 - i : i];
                _x[i] = p.X.Raw;
                _z[i] = p.Z.Raw;
            }
            MinX = MaxX = _x[0];
            MinZ = MaxZ = _z[0];
            for (int i = 1; i < n; i++)
            {
                MinX = Math.Min(MinX, _x[i]);
                MaxX = Math.Max(MaxX, _x[i]);
                MinZ = Math.Min(MinZ, _z[i]);
                MaxZ = Math.Max(MaxZ, _z[i]);
            }
        }

        /// <summary>
        /// Polygone simple et strictement convexe : au moins 3 sommets dans les bornes de l'espace, et chaque sommet strictement du même
        /// côté de chaque arête qui ne le contient pas (ce qui exclut sommets répétés, alignés, polygones croisés ou d'aire nulle).
        /// <paramref name="clockwise"/> donne le sens des sommets.
        /// </summary>
        public static bool IsStrictlyConvex(IReadOnlyList<GroundVector> points, out bool clockwise)
        {
            clockwise = false;
            if (points is null || points.Count < SpaceLimits.MinVertices || points.Count > SpaceLimits.MaxVertices) return false;
            foreach (GroundVector p in points)
                if (!SpaceLimits.InRange(p)) return false;
            int n = points.Count;
            int sign = 0;
            for (int i = 0; i < n; i++)
            {
                GroundVector a = points[i], b = points[(i + 1) % n];
                long ex = checked(b.X.Raw - a.X.Raw), ez = checked(b.Z.Raw - a.Z.Raw);
                for (int k = 0; k < n; k++)
                {
                    if (k == i || k == (i + 1) % n) continue;
                    long side = Planar.Cross(ex, ez, checked(points[k].X.Raw - a.X.Raw), checked(points[k].Z.Raw - a.Z.Raw));
                    int s = Math.Sign(side);
                    if (s == 0) return false;
                    if (sign == 0) sign = s;
                    else if (s != sign) return false;
                }
            }
            clockwise = sign < 0;
            return true;
        }

        /// <summary>Le point est-il dans le polygone, bord compris ? (exact)</summary>
        public bool Contains(long px, long pz)
        {
            for (int i = 0; i < _x.Length; i++)
            {
                int j = i + 1 == _x.Length ? 0 : i + 1;
                if (Planar.Cross(checked(_x[j] - _x[i]), checked(_z[j] - _z[i]), checked(px - _x[i]), checked(pz - _z[i])) < 0) return false;
            }
            return true;
        }

        /// <summary>
        /// Le disque (centre P, rayon r) chevauche-t-il strictement le polygone ? Vrai si le centre est dedans (bord compris) ou à moins
        /// de r d'une arête ; un disque qui touche seulement le bord ne chevauche pas. Test exact.
        /// </summary>
        public bool OverlapsCircle(long px, long pz, long r)
        {
            if (checked(px + r) <= MinX || checked(px - r) >= MaxX || checked(pz + r) <= MinZ || checked(pz - r) >= MaxZ) return false;
            if (Contains(px, pz)) return true;
            for (int i = 0; i < _x.Length; i++)
            {
                int j = i + 1 == _x.Length ? 0 : i + 1;
                if (Planar.SegmentOverlapsCircle(_x[i], _z[i], _x[j], _z[j], px, pz, r)) return true;
            }
            return false;
        }

        /// <summary>
        /// Sous-lot 11.4b (ligne de vue) : le segment fermé [A, B] touche-t-il le polygone (une extrémité dedans, ou une arête touchée) ?
        /// Exact ; frôler un sommet ou longer une face compte comme toucher.
        /// </summary>
        public bool IntersectsSegment(long ax, long az, long bx, long bz)
        {
            if (Math.Max(ax, bx) < MinX || Math.Min(ax, bx) > MaxX || Math.Max(az, bz) < MinZ || Math.Min(az, bz) > MaxZ) return false;
            if (Contains(ax, az) || Contains(bx, bz)) return true;
            for (int i = 0; i < _x.Length; i++)
            {
                int j = i + 1 == _x.Length ? 0 : i + 1;
                if (Planar.SegmentsIntersect(ax, az, bx, bz, _x[i], _z[i], _x[j], _z[j])) return true;
            }
            return false;
        }

        /// <summary>
        /// Point du bord le plus proche d'un point extérieur, arrondi à l'unité brute ; à distance égale, la première arête dans l'ordre
        /// des sommets. Sert à la direction d'un dégagement (le chevauchement, lui, est jugé par <see cref="OverlapsCircle"/>).
        /// </summary>
        public void ClosestBoundaryPoint(long px, long pz, out long qx, out long qz)
        {
            qx = _x[0];
            qz = _z[0];
            long best = long.MaxValue;
            for (int i = 0; i < _x.Length; i++)
            {
                int j = i + 1 == _x.Length ? 0 : i + 1;
                Planar.ClosestOnSegment(_x[i], _z[i], _x[j], _z[j], px, pz, out long cx, out long cz);
                long d = Planar.LengthSquared(checked(px - cx), checked(pz - cz));
                if (d < best)
                {
                    best = d;
                    qx = cx;
                    qz = cz;
                }
            }
        }
    }
}
