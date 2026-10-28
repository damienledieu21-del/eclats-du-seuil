using System;
using System.Collections.Generic;

namespace EdS.Runtime
{
    /// <summary>
    /// Une annonce d'attaque au sol prête à dessiner : sommets (mètres, au sol), et pour chacun l'avancée dans la forme
    /// (<see cref="Progress"/> : 0 à l'origine, 1 au bout), la place sur la largeur (<see cref="Side"/> : -1 à 1), la distance au bord
    /// (<see cref="Edge"/>, mètres, négative dehors) et la portée de la forme (<see cref="Reach"/>, mètres) ; triangles tournés vers le haut.
    /// </summary>
    public sealed class TelegraphMesh
    {
        public List<StyleVector> Vertices { get; } = new List<StyleVector>();
        public List<double> Progress { get; } = new List<double>();
        public List<double> Side { get; } = new List<double>();
        public List<double> Edge { get; } = new List<double>();
        public double Reach { get; internal set; }
        public List<int> Triangles { get; } = new List<int>();

        internal int Add(double x, double z, double progress, double side, double edge)
        {
            Vertices.Add(new StyleVector(x, 0, z));
            Progress.Add(progress);
            Side.Add(side);
            Edge.Add(edge);
            return Vertices.Count - 1;
        }

        /// <summary>
        /// Grille de <paramref name="rows"/> + 1 rangées (de l'origine vers le bout) de <paramref name="columns"/> + 1 sommets (sur la
        /// largeur, dans le sens trigonométrique vu de dessus), commençant au sommet <paramref name="first"/> : deux triangles par case,
        /// tournés vers le haut ; <paramref name="pointedStart"/> : la première rangée est un seul point (pas de triangle aplati).
        /// </summary>
        internal void Grid(int first, int rows, int columns, bool pointedStart)
        {
            int width = columns + 1;
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < columns; j++)
                {
                    int a = first + i * width + j, b = a + 1, c = a + width, d = c + 1;
                    Triangles.Add(a);
                    Triangles.Add(d);
                    Triangles.Add(c);
                    if (i == 0 && pointedStart) continue;
                    Triangles.Add(a);
                    Triangles.Add(b);
                    Triangles.Add(d);
                }
        }
    }

    /// <summary>
    /// Essai réaliste 6 (essai de Damien du 27 octobre 2026 : « Les attaques aussi des ennemis sont à changer c'est à dire la charge rouge du
    /// sanglier ou les rond des autres personnages ») : formes des annonces d'attaque au sol pour le shader « EdS/Annonce », qui dessine
    /// un bord lumineux, un front qui avance jusqu'à l'impact et des chevrons (bande, cône) ou des ondes (cercle). Une seule forme pleine
    /// par annonce, découpée en cases d'une trentaine de centimètres (elle suit ainsi le tablier du pont). Mètres, affichage seulement.
    /// </summary>
    public static class TelegraphShapes
    {
        /// <summary>Taille visée d'une case (mètres).</summary>
        public const double Cell = 0.3;

        private static int Count(double length, int min, int max) =>
            Math.Max(min, Math.Min(max, (int)Math.Ceiling(Math.Max(0, length) / Cell)));

        /// <summary>Disque de rayon <paramref name="radius"/> autour de (<paramref name="x"/>, <paramref name="z"/>) ; l'avancée va du centre au bord.</summary>
        public static TelegraphMesh Circle(double x, double z, double radius)
        {
            double r = Math.Max(0.05, radius);
            var mesh = new TelegraphMesh { Reach = r };
            int rings = Count(r, 4, 24), segments = 48;
            for (int i = 0; i <= rings; i++)
            {
                double d = r * i / rings;
                for (int j = 0; j <= segments; j++)
                {
                    double a = 2 * Math.PI * j / segments;
                    mesh.Add(x + Math.Cos(a) * d, z + Math.Sin(a) * d, d / r, 2.0 * j / segments - 1, r - d);
                }
            }
            mesh.Grid(0, rings, segments, true);
            return mesh;
        }

        /// <summary>
        /// Cône de sommet (<paramref name="x"/>, <paramref name="z"/>), d'axe <paramref name="axis"/> (radians, sens trigonométrique depuis +x),
        /// d'ouverture totale <paramref name="angle"/> (radians) et de longueur <paramref name="reach"/>.
        /// </summary>
        public static TelegraphMesh Cone(double x, double z, double axis, double angle, double reach)
        {
            double r = Math.Max(0.05, reach), half = Math.Max(0.01, Math.Min(Math.PI, angle / 2));
            var mesh = new TelegraphMesh { Reach = r };
            int rings = Count(r, 4, 24), segments = Count(2 * half * r, 8, 48);
            double from = axis - half;
            for (int i = 0; i <= rings; i++)
            {
                double d = r * i / rings;
                for (int j = 0; j <= segments; j++)
                {
                    double offset = 2 * half * j / segments, a = from + offset;
                    double edge = Math.Min(r - d, Math.Min(Side(d, offset), Side(d, 2 * half - offset)));
                    mesh.Add(x + Math.Cos(a) * d, z + Math.Sin(a) * d, d / r, 2.0 * j / segments - 1, edge);
                }
            }
            mesh.Grid(0, rings, segments, true);
            return mesh;
        }

        /// <summary>Distance au rayon qui borne le cône, pour un point à <paramref name="d"/> du sommet et à <paramref name="offset"/> (radians) de ce rayon.</summary>
        private static double Side(double d, double offset) => offset >= Math.PI / 2 ? d : d * Math.Sin(offset);

        /// <summary>
        /// Bande à bouts arrondis comme dans le moteur (points à moins de <paramref name="half"/> du segment) : de (<paramref name="x"/>,
        /// <paramref name="z"/>) sur <paramref name="length"/> mètres le long de l'axe unitaire (<paramref name="ax"/>, <paramref name="az"/>).
        /// La grille couvre le rectangle autour de la bande ; ses coins hors de la bande ont une distance au bord négative (le shader ne les
        /// dessine pas).
        /// </summary>
        public static TelegraphMesh Line(double x, double z, double ax, double az, double length, double half)
        {
            double norm = Math.Sqrt(ax * ax + az * az);
            if (norm < 1e-9)
            {
                ax = 1;
                az = 0;
            }
            else
            {
                ax /= norm;
                az /= norm;
            }
            double l = Math.Max(0, length), w = Math.Max(0.05, half);
            var mesh = new TelegraphMesh { Reach = l + w };
            int rows = Count(l + 2 * w, 4, 96), columns = Count(2 * w, 4, 16);
            double nx = -az, nz = ax;   // à gauche de l'axe
            for (int i = 0; i <= rows; i++)
            {
                double u = -w + (l + 2 * w) * i / rows;
                double along = u - Math.Max(0, Math.Min(l, u));
                for (int j = 0; j <= columns; j++)
                {
                    double v = -w + 2 * w * j / columns;
                    double edge = w - Math.Sqrt(along * along + v * v);
                    double progress = l > 0 ? Math.Max(0, Math.Min(1, u / l)) : 1;
                    mesh.Add(x + ax * u + nx * v, z + az * u + nz * v, progress, Math.Max(-1, Math.Min(1, v / w)), edge);
                }
            }
            mesh.Grid(0, rows, columns, false);
            return mesh;
        }
    }
}
