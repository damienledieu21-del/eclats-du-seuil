using System;
using System.Collections.Generic;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.27 (docs/PLAN_GRANDE_CARTE.md, § 6.1) : un morceau de terrain affiché (une case, ou un bloc du terrain lointain), construit
    /// <b>par petits pas</b> (<see cref="StepOnce"/> : une rangée de hauteurs, de sommets ou de triangles) pour tenir dans le budget de temps d'une image.
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
        private TerrainBuffers _b;
        private int _phase, _row, _next, _indexCount;

        public double X0 { get; }
        public double Z0 { get; }
        public double Step { get; }
        public int QuadsX { get; }
        public int QuadsZ { get; }

        /// <summary>Le maillage est prêt (<see cref="Positions"/>, <see cref="Normals"/>, <see cref="Uvs"/>, <see cref="Indices"/>).</summary>
        public bool Done { get; private set; }

        /// <summary>Pas faits jusqu'ici.</summary>
        public int Steps { get; private set; }

        /// <summary>Pas nécessaires en tout : rangées de hauteurs (une de plus de chaque côté), préparation, rangées de sommets, rangées de triangles.</summary>
        public int TotalSteps => (QuadsZ + 3) + 1 + (QuadsZ + 1) + QuadsZ;

        /// <summary>Positions (x, y, z) des <see cref="VertexCount"/> premiers sommets ; le tableau peut être plus long (recyclé).</summary>
        public float[] Positions => _b.Positions;

        public float[] Normals => _b.Normals;

        public float[] Uvs => _b.Uvs;

        /// <summary>Index des triangles : les <see cref="IndexCount"/> premiers ; le tableau peut être plus long (recyclé).</summary>
        public int[] Indices => _b.Indices;

        public int VertexCount { get; private set; }

        public int IndexCount => _indexCount;

        /// <summary>Plus de 65 535 sommets : index sur 32 bits.</summary>
        public bool WideIndices => VertexCount > 65535;

        public int TriangleCount => _indexCount / 3;

        /// <summary>Les tableaux ont été rendus au recyclage (<see cref="RegionTerrain"/>, après l'appel au <see cref="ITerrainSink"/>).</summary>
        public bool Released { get; private set; }

        /// <param name="keep">Garder le quadrilatère (i, j) ? null : tous (sauf ceux sous <paramref name="covered"/>). Lu une seule fois, à la préparation.</param>
        /// <param name="buffers">Tableaux recyclés (agrandis au besoin), ou null : neufs.</param>
        public TerrainPatch(double x0, double z0, double step, int quadsX, int quadsZ, Func<double, double, double> height,
            Func<int, int, bool>? keep = null, (double X0, double Z0, double X1, double Z1)? covered = null, double skirt = 6.0,
            TerrainBuffers? buffers = null)
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
            _b = buffers ?? new TerrainBuffers();
            _b.EnsureHeights((quadsX + 3) * (quadsZ + 3));
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

        /// <summary>Triangles estimés d'une case de <paramref name="resolution"/> points par côté (jupes comprises).</summary>
        public static long EstimateTriangles(int resolution)
        {
            if (resolution < 2) return 0;
            long q = resolution - 1;
            return 2 * q * q + 4 * q * 4;
        }

        /// <summary>Octets du maillage construit (même règle que <see cref="EstimateBytes"/>).</summary>
        public long Bytes => 2 * ((long)VertexCount * 32 + (long)_indexCount * (WideIndices ? 4 : 2));

        /// <summary>Un pas : une rangée de hauteurs, la préparation, une rangée de sommets ou de triangles. Vrai quand le maillage est prêt.</summary>
        public bool StepOnce()
        {
            if (Done) return true;
            if (Released) throw new InvalidOperationException("Morceau de terrain rendu au recyclage.");
            switch (_phase)
            {
                case 0: Heights(); break;
                case 1: Prepare(); break;
                case 2: Vertices(); break;
                default: Triangles(); break;
            }
            Steps++;
            return Done;
        }

        private void Heights()
        {
            int w = QuadsX + 3;
            double z = Z0 + (_row - 1) * Step;
            for (int i = 0; i < w; i++)
            {
                double y = _height(X0 + (i - 1) * Step, z);
                if (double.IsNaN(y) || double.IsInfinity(y)) throw new InvalidOperationException("Hauteur non finie.");
                _b.Heights[_row * w + i] = y;
            }
            if (++_row == QuadsZ + 3)
            {
                _phase = 1;
                _row = 0;
            }
        }

        private double H(int i, int j) => _b.Heights[(j + 1) * (QuadsX + 3) + i + 1];

        private bool InCovered(double x, double z, double pad) =>
            _covered is { } c && x >= c.X0 - pad && x <= c.X1 + pad && z >= c.Z0 - pad && z <= c.Z1 + pad;

        private bool Decide(int i, int j)
        {
            if (_covered is { } c)
            {
                double x0 = X0 + i * Step, z0 = Z0 + j * Step;
                if (x0 >= c.X0 && x0 + Step <= c.X1 && z0 >= c.Z0 && z0 + Step <= c.Z1) return false;
            }
            return _keep == null || _keep(i, j);
        }

        /// <summary>
        /// Le quadrilatère (i, j) est-il fait ? (décidé à la préparation ; faux hors du morceau). Lisible jusqu'au recyclage (<see cref="Release"/>) :
        /// le masque est dans les tableaux recyclés.
        /// </summary>
        public bool Kept(int i, int j)
        {
            if (_phase < 2 && !Done) throw new InvalidOperationException("Morceau de terrain pas encore préparé.");
            if (Released) throw new InvalidOperationException("Morceau de terrain rendu au recyclage.");
            return i >= 0 && j >= 0 && i < QuadsX && j < QuadsZ && _b.Kept[j * QuadsX + i];
        }

        private void Prepare()
        {
            _b.EnsureKept(QuadsX * QuadsZ);
            bool[] mask = _b.Kept;
            int kept = 0, edges = 0;
            for (int j = 0; j < QuadsZ; j++)
                for (int i = 0; i < QuadsX; i++)
                    if (mask[j * QuadsX + i] = Decide(i, j)) kept++;
            _phase = 2;
            for (int j = 0; j < QuadsZ; j++)
                for (int i = 0; i < QuadsX; i++)
                {
                    if (!mask[j * QuadsX + i]) continue;
                    if (!Kept(i - 1, j)) edges++;
                    if (!Kept(i + 1, j)) edges++;
                    if (!Kept(i, j - 1)) edges++;
                    if (!Kept(i, j + 1)) edges++;
                }
            VertexCount = checked((QuadsX + 1) * (QuadsZ + 1) + 2 * edges);
            _b.EnsureMesh(VertexCount, checked(kept * 6 + edges * 12));
            _next = (QuadsX + 1) * (QuadsZ + 1);
            _indexCount = 0;
        }

        private void Vertices()
        {
            int nx = QuadsX + 1, j = _row;
            float[] pos = _b.Positions, nor = _b.Normals, uv = _b.Uvs;
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
            if (++_row == QuadsZ + 1)
            {
                _phase = 3;
                _row = 0;
            }
        }

        private void Add(int a, int b, int c)
        {
            int[] idx = _b.Indices;
            idx[_indexCount++] = a;
            idx[_indexCount++] = b;
            idx[_indexCount++] = c;
        }

        private void Triangles()
        {
            int nx = QuadsX + 1, j = _row;
            for (int i = 0; i < QuadsX; i++)
            {
                if (!_b.Kept[j * QuadsX + i]) continue;
                int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                // Unity : faces avant dans le sens des aiguilles d'une montre vues d'en haut (x vers l'est, z vers le nord).
                Add(a, c, d);
                Add(a, d, b);
                if (!Kept(i - 1, j)) Skirt(a, c);
                if (!Kept(i + 1, j)) Skirt(b, d);
                if (!Kept(i, j - 1)) Skirt(a, b);
                if (!Kept(i, j + 1)) Skirt(c, d);
            }
            if (++_row == QuadsZ) Done = true;
        }

        private void Skirt(int top0, int top1)
        {
            int low0 = _next++, low1 = _next++;
            Copy(top0, low0);
            Copy(top1, low1);
            // Les deux faces : la jupe se voit des deux côtés (d'une case plus fine comme d'une plus grossière).
            Add(top0, low0, low1);
            Add(top0, low1, top1);
            Add(top0, low1, low0);
            Add(top0, top1, low1);
        }

        private void Copy(int from, int to)
        {
            float[] pos = _b.Positions, nor = _b.Normals, uv = _b.Uvs;
            pos[3 * to] = pos[3 * from];
            pos[3 * to + 1] = (float)(pos[3 * from + 1] - _skirt);
            pos[3 * to + 2] = pos[3 * from + 2];
            for (int k = 0; k < 3; k++) nor[3 * to + k] = nor[3 * from + k];
            uv[2 * to] = uv[2 * from];
            uv[2 * to + 1] = (float)(uv[2 * from + 1] - _skirt / UvTile);
        }

        /// <summary>Hauteur du sommet (i, j) de la grille (avant enfoncement), pour les tests de raccord.</summary>
        public double HeightAt(int i, int j)
        {
            if (!Done || Released) throw new InvalidOperationException("Morceau de terrain pas construit, ou rendu au recyclage.");
            if (i < 0 || j < 0 || i > QuadsX || j > QuadsZ) throw new ArgumentOutOfRangeException(nameof(i));
            return H(i, j);
        }

        /// <summary>Rend les tableaux au recyclage : le morceau ne garde que ses comptes (sommets, index, octets, triangles).</summary>
        public TerrainBuffers Release()
        {
            if (Released) throw new InvalidOperationException("Morceau de terrain déjà rendu.");
            Released = true;
            TerrainBuffers b = _b;
            _b = TerrainBuffers.Empty;
            return b;
        }
    }

    /// <summary>
    /// Sous-lot V2.27 : tableaux d'un morceau de terrain, recyclés d'un morceau à l'autre (pas d'allocation à chaque case : pas de pause du
    /// ramasse-miettes en chemin). Agrandis seulement si un morceau en demande plus.
    /// </summary>
    public sealed class TerrainBuffers
    {
        internal static readonly TerrainBuffers Empty = new TerrainBuffers();

        public double[] Heights { get; private set; } = Array.Empty<double>();
        public float[] Positions { get; private set; } = Array.Empty<float>();
        public float[] Normals { get; private set; } = Array.Empty<float>();
        public float[] Uvs { get; private set; } = Array.Empty<float>();
        public int[] Indices { get; private set; } = Array.Empty<int>();

        /// <summary>Masque des quadrilatères faits (rangée par rangée).</summary>
        public bool[] Kept { get; private set; } = Array.Empty<bool>();

        internal void EnsureKept(int count)
        {
            if (Kept.Length < count) Kept = new bool[count];
        }

        internal void EnsureHeights(int count)
        {
            if (Heights.Length < count) Heights = new double[count];
        }

        internal void EnsureMesh(int vertices, int indices)
        {
            if (Positions.Length < vertices * 3)
            {
                Positions = new float[vertices * 3];
                Normals = new float[vertices * 3];
                Uvs = new float[vertices * 2];
            }
            if (Indices.Length < indices) Indices = new int[indices];
        }
    }
}
