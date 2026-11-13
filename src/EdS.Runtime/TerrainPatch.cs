using System;
using System.Collections.Generic;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.27 (docs/PLAN_GRANDE_CARTE.md, § 6.1) : un morceau de terrain affiché (une case, ou un bloc du terrain lointain), construit
    /// <b>par petits pas</b> (<see cref="StepOnce"/> : une rangée de hauteurs, puis le maillage) pour tenir dans le budget de temps d'une image.
    /// Hauteurs lues d'une seule fonction (le relief affiché, <see cref="GroundRelief.Height"/>) aux mêmes points des deux côtés d'un bord :
    /// deux cases voisines de même finesse se raccordent exactement ; les normales sont tirées des hauteurs voisines (un point de plus
    /// autour), donc identiques de part et d'autre. Entre deux finesses, une <b>jupe</b> (paroi qui descend sous chaque bord libre) bouche
    /// toute fente. Les quadrilatères entièrement dans <c>covered</c> (le terrain peint de la zone, construit par <c>SpaceView</c>) ne sont pas
    /// faits ; près de lui, le maillage s'enfonce de <see cref="Sink"/> m pour passer dessous. Affichage seulement, sans Unity.
    /// </summary>
    public sealed class TerrainPatch
    {
        /// <summary>Enfoncement (m) près du terrain peint de la zone : le terrain de la zone reste au-dessus, sans scintillement.</summary>
        public const double Sink = 0.2;

        /// <summary>Taille de la répétition de la texture (m) : coordonnées de texture = mètres du monde / <see cref="UvTile"/>.</summary>
        public const double UvTile = 8.0;

        private readonly Func<double, double, double> _height;
        private readonly Func<int, int, bool>? _keep;
        private readonly (double X0, double Z0, double X1, double Z1)? _covered;
        private readonly double _skirt;
        private readonly double[] _h;
        private int _rows;

        public double X0 { get; }
        public double Z0 { get; }
        public double Step { get; }
        public int QuadsX { get; }
        public int QuadsZ { get; }

        /// <summary>Le maillage est prêt (<see cref="Positions"/>, <see cref="Normals"/>, <see cref="Uvs"/>, <see cref="Indices"/>).</summary>
        public bool Done { get; private set; }

        /// <summary>Pas faits jusqu'ici (rangées de hauteurs, plus un pour le maillage).</summary>
        public int Steps { get; private set; }

        /// <summary>Pas nécessaires en tout.</summary>
        public int TotalSteps => QuadsZ + 3 + 1;

        public float[] Positions { get; private set; } = Array.Empty<float>();
        public float[] Normals { get; private set; } = Array.Empty<float>();
        public float[] Uvs { get; private set; } = Array.Empty<float>();
        public int[] Indices { get; private set; } = Array.Empty<int>();
        public int VertexCount { get; private set; }

        /// <summary>Plus de 65 535 sommets : index sur 32 bits.</summary>
        public bool WideIndices => VertexCount > 65535;

        public int TriangleCount => Indices.Length / 3;

        /// <param name="keep">Garder le quadrilatère (i, j) ? null : tous (sauf ceux sous <paramref name="covered"/>).</param>
        public TerrainPatch(double x0, double z0, double step, int quadsX, int quadsZ, Func<double, double, double> height,
            Func<int, int, bool>? keep = null, (double X0, double Z0, double X1, double Z1)? covered = null, double skirt = 6.0)
        {
            if (!(step > 0) || double.IsInfinity(step)) throw new ArgumentOutOfRangeException(nameof(step));
            if (quadsX < 1 || quadsZ < 1 || quadsX > 512 || quadsZ > 512) throw new ArgumentOutOfRangeException(nameof(quadsX), "De 1 à 512 quadrilatères par côté.");
            if (!(skirt >= 0) || double.IsInfinity(skirt)) throw new ArgumentOutOfRangeException(nameof(skirt));
            if (double.IsNaN(x0) || double.IsInfinity(x0) || double.IsNaN(z0) || double.IsInfinity(z0)) throw new ArgumentException("Coin non fini.");
            _height = height ?? throw new ArgumentNullException(nameof(height));
            _keep = keep;
            _covered = covered;
            _skirt = skirt;
            X0 = x0;
            Z0 = z0;
            Step = step;
            QuadsX = quadsX;
            QuadsZ = quadsZ;
            _h = new double[(quadsX + 3) * (quadsZ + 3)];
        }

        /// <summary>Octets estimés du maillage d'une case de <paramref name="resolution"/> points par côté (copie du processeur et de la carte).</summary>
        public static long EstimateBytes(int resolution)
        {
            if (resolution < 2) return 0;
            long q = resolution - 1;
            long vertices = (long)resolution * resolution + 8 * q;
            long indices = 6 * q * q + 4 * q * 12;
            return 2 * (vertices * 32 + indices * (vertices > 65535 ? 4 : 2));
        }

        /// <summary>Octets du maillage construit (même règle que <see cref="EstimateBytes"/>).</summary>
        public long Bytes => 2 * ((long)VertexCount * 32 + (long)Indices.Length * (WideIndices ? 4 : 2));

        /// <summary>Un pas : une rangée de hauteurs, ou (à la fin) le maillage. Vrai quand le maillage est prêt.</summary>
        public bool StepOnce()
        {
            if (Done) return true;
            int w = QuadsX + 3;
            if (_rows < QuadsZ + 3)
            {
                double z = Z0 + (_rows - 1) * Step;
                for (int i = 0; i < w; i++)
                {
                    double y = _height(X0 + (i - 1) * Step, z);
                    if (double.IsNaN(y) || double.IsInfinity(y)) throw new InvalidOperationException("Hauteur non finie.");
                    _h[_rows * w + i] = y;
                }
                _rows++;
                Steps++;
                return false;
            }
            Mesh();
            Steps++;
            Done = true;
            return true;
        }

        private double H(int i, int j) => _h[(j + 1) * (QuadsX + 3) + i + 1];

        private bool InCovered(double x, double z, double pad) =>
            _covered is { } c && x >= c.X0 - pad && x <= c.X1 + pad && z >= c.Z0 - pad && z <= c.Z1 + pad;

        /// <summary>Le quadrilatère (i, j) est-il fait ?</summary>
        public bool Kept(int i, int j)
        {
            if (i < 0 || j < 0 || i >= QuadsX || j >= QuadsZ) return false;
            if (_covered is { } c)
            {
                double x0 = X0 + i * Step, z0 = Z0 + j * Step;
                if (x0 >= c.X0 && x0 + Step <= c.X1 && z0 >= c.Z0 && z0 + Step <= c.Z1) return false;
            }
            return _keep == null || _keep(i, j);
        }

        private void Mesh()
        {
            int nx = QuadsX + 1, nz = QuadsZ + 1;
            var keep = new bool[QuadsX * QuadsZ];
            int kept = 0, edges = 0;
            for (int j = 0; j < QuadsZ; j++)
                for (int i = 0; i < QuadsX; i++)
                    if (keep[j * QuadsX + i] = Kept(i, j)) kept++;
            for (int j = 0; j < QuadsZ; j++)
                for (int i = 0; i < QuadsX; i++)
                {
                    if (!keep[j * QuadsX + i]) continue;
                    if (!Kept(i - 1, j)) edges++;
                    if (!Kept(i + 1, j)) edges++;
                    if (!Kept(i, j - 1)) edges++;
                    if (!Kept(i, j + 1)) edges++;
                }
            int vertices = checked(nx * nz + 2 * edges);
            var pos = new float[vertices * 3];
            var nor = new float[vertices * 3];
            var uv = new float[vertices * 2];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int v = j * nx + i;
                    double x = X0 + i * Step, z = Z0 + j * Step;
                    double y = H(i, j) - (InCovered(x, z, Step) ? Sink : 0);
                    pos[3 * v] = (float)x;
                    pos[3 * v + 1] = (float)y;
                    pos[3 * v + 2] = (float)z;
                    double ax = H(i - 1, j) - H(i + 1, j), ay = 2 * Step, az = H(i, j - 1) - H(i, j + 1);
                    double len = Math.Sqrt(ax * ax + ay * ay + az * az);
                    nor[3 * v] = (float)(ax / len);
                    nor[3 * v + 1] = (float)(ay / len);
                    nor[3 * v + 2] = (float)(az / len);
                    uv[2 * v] = (float)(x / UvTile);
                    uv[2 * v + 1] = (float)(z / UvTile);
                }
            var idx = new List<int>(checked(kept * 6 + edges * 12));
            for (int j = 0; j < QuadsZ; j++)
                for (int i = 0; i < QuadsX; i++)
                {
                    if (!keep[j * QuadsX + i]) continue;
                    int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                    // Unity : faces avant dans le sens des aiguilles d'une montre vues d'en haut (x vers l'est, z vers le nord).
                    idx.Add(a); idx.Add(c); idx.Add(d);
                    idx.Add(a); idx.Add(d); idx.Add(b);
                }
            int next = nx * nz;
            void Skirt(int top0, int top1)
            {
                int low0 = next++, low1 = next++;
                Copy(top0, low0);
                Copy(top1, low1);
                // Les deux faces : la jupe se voit des deux côtés (d'une case plus fine comme d'une plus grossière).
                idx.Add(top0); idx.Add(low0); idx.Add(low1);
                idx.Add(top0); idx.Add(low1); idx.Add(top1);
                idx.Add(top0); idx.Add(low1); idx.Add(low0);
                idx.Add(top0); idx.Add(top1); idx.Add(low1);
            }
            void Copy(int from, int to)
            {
                pos[3 * to] = pos[3 * from];
                pos[3 * to + 1] = (float)(pos[3 * from + 1] - _skirt);
                pos[3 * to + 2] = pos[3 * from + 2];
                for (int k = 0; k < 3; k++) nor[3 * to + k] = nor[3 * from + k];
                uv[2 * to] = uv[2 * from];
                uv[2 * to + 1] = (float)(uv[2 * from + 1] - _skirt / UvTile);
            }
            for (int j = 0; j < QuadsZ; j++)
                for (int i = 0; i < QuadsX; i++)
                {
                    if (!keep[j * QuadsX + i]) continue;
                    int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                    if (!Kept(i - 1, j)) Skirt(a, c);
                    if (!Kept(i + 1, j)) Skirt(b, d);
                    if (!Kept(i, j - 1)) Skirt(a, b);
                    if (!Kept(i, j + 1)) Skirt(c, d);
                }
            Positions = pos;
            Normals = nor;
            Uvs = uv;
            Indices = idx.ToArray();
            VertexCount = vertices;
        }

        /// <summary>Hauteur du sommet (i, j) de la grille (avant enfoncement), pour les tests de raccord.</summary>
        public double HeightAt(int i, int j)
        {
            if (!Done) throw new InvalidOperationException("Morceau de terrain pas encore construit.");
            if (i < 0 || j < 0 || i > QuadsX || j > QuadsZ) throw new ArgumentOutOfRangeException(nameof(i));
            return H(i, j);
        }
    }
}
