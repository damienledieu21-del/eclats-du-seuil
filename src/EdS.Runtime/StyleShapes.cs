using System;
using System.Collections.Generic;

namespace EdS.Runtime
{
    /// <summary>Point ou direction de l'essai de style, en mètres, y vers le haut (comme Unity).</summary>
    public readonly struct StyleVector
    {
        public StyleVector(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public static readonly StyleVector Up = new StyleVector(0, 1, 0);

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        public StyleVector Normalized
        {
            get
            {
                double l = Length;
                return l > 1e-12 ? new StyleVector(X / l, Y / l, Z / l) : new StyleVector(0, 0, 0);
            }
        }

        public static StyleVector operator +(StyleVector a, StyleVector b) => new StyleVector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static StyleVector operator -(StyleVector a, StyleVector b) => new StyleVector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static StyleVector operator -(StyleVector a) => new StyleVector(-a.X, -a.Y, -a.Z);
        public static StyleVector operator *(StyleVector a, double k) => new StyleVector(a.X * k, a.Y * k, a.Z * k);
        public static StyleVector operator *(double k, StyleVector a) => a * k;

        public static double Dot(StyleVector a, StyleVector b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        /// <summary>Produit vectoriel, même formule que <c>Vector3.Cross</c> d'Unity.</summary>
        public static StyleVector Cross(StyleVector a, StyleVector b) =>
            new StyleVector(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public override string ToString() => FormattableString.Invariant($"({X:0.###}, {Y:0.###}, {Z:0.###})");
    }

    /// <summary>
    /// Essai réaliste 5 : maillage fait par le code, sans Unity (sommets, coordonnées de texture en répétitions de texture, triangles).
    /// Chaque triangle est visible du côté de sa normale <c>Cross(b - a, c - a)</c>, celle que calcule <c>Mesh.RecalculateNormals</c> ;
    /// l'ordre de ses sommets est corrigé pour qu'elle regarde vers l'extérieur donné.
    /// </summary>
    public sealed class StyleMesh
    {
        public List<StyleVector> Vertices { get; } = new List<StyleVector>();
        public List<(double U, double V)> Uvs { get; } = new List<(double U, double V)>();
        public List<int> Triangles { get; } = new List<int>();

        /// <summary>Sommet partagé (pour un rondin lisse) ; rend son numéro.</summary>
        public int Vertex(StyleVector p, (double U, double V) uv)
        {
            Vertices.Add(p);
            Uvs.Add(uv);
            return Vertices.Count - 1;
        }

        /// <summary>Triangle de sommets partagés, visible du côté de <paramref name="outward"/>.</summary>
        public void Indexed(int a, int b, int c, StyleVector outward)
        {
            StyleVector n = StyleVector.Cross(Vertices[b] - Vertices[a], Vertices[c] - Vertices[a]);
            if (StyleVector.Dot(n, outward) < 0)
            {
                int t = b;
                b = c;
                c = t;
            }
            Triangles.Add(a);
            Triangles.Add(b);
            Triangles.Add(c);
        }

        /// <summary>Triangle à facette (sommets propres), visible du côté de <paramref name="outward"/>.</summary>
        public void Triangle(StyleVector a, StyleVector b, StyleVector c, (double U, double V) ua, (double U, double V) ub, (double U, double V) uc,
            StyleVector outward)
        {
            int i = Vertex(a, ua), j = Vertex(b, ub), k = Vertex(c, uc);
            Indexed(i, j, k, outward);
        }

        /// <summary>Quadrilatère a-b-c-d (dans l'ordre du tour) en deux triangles visibles du côté de <paramref name="outward"/>.</summary>
        public void Quad(StyleVector a, StyleVector b, StyleVector c, StyleVector d, (double U, double V) ua, (double U, double V) ub,
            (double U, double V) uc, (double U, double V) ud, StyleVector outward)
        {
            Triangle(a, b, c, ua, ub, uc, outward);
            Triangle(a, c, d, ua, uc, ud, outward);
        }

        /// <summary>Boîte englobante (min, max) ; zéros sans sommet.</summary>
        public (StyleVector Min, StyleVector Max) Bounds()
        {
            if (Vertices.Count == 0) return (new StyleVector(0, 0, 0), new StyleVector(0, 0, 0));
            double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
            foreach (StyleVector v in Vertices)
            {
                x0 = Math.Min(x0, v.X);
                y0 = Math.Min(y0, v.Y);
                z0 = Math.Min(z0, v.Z);
                x1 = Math.Max(x1, v.X);
                y1 = Math.Max(y1, v.Y);
                z1 = Math.Max(z1, v.Z);
            }
            return (new StyleVector(x0, y0, z0), new StyleVector(x1, y1, z1));
        }
    }

    /// <summary>Pièces d'un pont fait par le code : planches et poutres (bois), rambardes (rondins d'écorce), culées (pierres).</summary>
    public sealed class StyleBridgeShapes
    {
        public StyleMesh Wood { get; } = new StyleMesh();
        public StyleMesh Bark { get; } = new StyleMesh();
        public StyleMesh Stone { get; } = new StyleMesh();
    }

    /// <summary>
    /// Pièces d'un abri fait par le code : charpente basse (poteaux, sablières, entraits, aisseliers), socles de pierre, et toit
    /// (tuiles dessus ; voliges, chevrons, faîtage et poinçons dessous), le toit étant retiré quand le joueur passe dessous.
    /// </summary>
    public sealed class StyleShelterShapes
    {
        public StyleMesh Frame { get; } = new StyleMesh();
        public StyleMesh Stone { get; } = new StyleMesh();
        public StyleMesh RoofTiles { get; } = new StyleMesh();
        public StyleMesh RoofWood { get; } = new StyleMesh();

        /// <summary>Emprise au sol du toit (débords compris).</summary>
        public double X0 { get; internal set; }
        public double Z0 { get; internal set; }
        public double X1 { get; internal set; }
        public double Z1 { get; internal set; }

        /// <summary>Hauteur du dessous du toit à l'aplomb des poteaux, et du haut du faîtage.</summary>
        public double RoofBottom { get; internal set; }
        public double RoofTop { get; internal set; }
    }

    /// <summary>
    /// Essai réaliste 5 (demandes de Damien du 27 octobre 2026 au matin : « texturer le bâtiment », « le mur de feuilles derrière les
    /// loups je ne suis pas fan », « le pont, on pourrait l'améliorer » ; choix : muret de pierres sèches, pont cintré fait par Claude) :
    /// formes faites par le code, sans Unity, pour les décors construits. Mêmes formes à chaque lancement (suite tirée de l'identifiant).
    /// Coordonnées de texture à l'échelle du monde : une répétition tous les <c>tile</c> mètres. Affichage seulement.
    /// </summary>
    public static class StyleShapes
    {
        /// <summary>
        /// Pavé de centre <paramref name="center"/> et de demi-axes orthogonaux <paramref name="ax"/>, <paramref name="ay"/>,
        /// <paramref name="az"/> (coins éventuellement décalés par <paramref name="jitter"/>, huit décalages dans l'ordre des signes
        /// x, puis y, puis z), à facettes. Textures planes par face : le long de ax sur les faces z et y, le long de az sur les faces x ;
        /// en hauteur le long de ay (le long de az pour les faces y).
        /// </summary>
        public static void Box(StyleMesh mesh, StyleVector center, StyleVector ax, StyleVector ay, StyleVector az, double tile,
            (double U, double V) offset = default, IReadOnlyList<StyleVector>? jitter = null)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            double scale = 1 / Math.Max(0.05, tile);
            var corners = new StyleVector[8];
            for (int i = 0; i < 8; i++)
            {
                StyleVector p = center + ((i & 1) != 0 ? ax : -ax) + ((i & 2) != 0 ? ay : -ay) + ((i & 4) != 0 ? az : -az);
                if (jitter != null && jitter.Count == 8) p += jitter[i];
                corners[i] = p;
            }
            StyleVector ux = ax.Normalized, uy = ay.Normalized, uz = az.Normalized;
            (double U, double V) Uv(StyleVector p, StyleVector a, StyleVector b) =>
                (StyleVector.Dot(p - center, a) * scale + offset.U, StyleVector.Dot(p - center, b) * scale + offset.V);
            // Une face par signe d'axe : ses quatre coins dans l'ordre du tour, sa direction, ses axes de texture.
            void Face(int axisBit, bool positive, StyleVector outward, StyleVector a, StyleVector b)
            {
                var loop = new List<StyleVector>();
                int[] others = axisBit == 1 ? new[] { 2, 4 } : axisBit == 2 ? new[] { 1, 4 } : new[] { 1, 2 };
                foreach ((bool p, bool q) in new[] { (false, false), (true, false), (true, true), (false, true) })
                {
                    int index = (positive ? axisBit : 0) | (p ? others[0] : 0) | (q ? others[1] : 0);
                    loop.Add(corners[index]);
                }
                mesh.Quad(loop[0], loop[1], loop[2], loop[3], Uv(loop[0], a, b), Uv(loop[1], a, b), Uv(loop[2], a, b), Uv(loop[3], a, b),
                    positive ? outward : -outward);
            }
            Face(1, true, ux, uz, uy);
            Face(1, false, ux, uz, uy);
            Face(2, true, uy, ux, uz);
            Face(2, false, uy, ux, uz);
            Face(4, true, uz, ux, uy);
            Face(4, false, uz, ux, uy);
        }

        /// <summary>
        /// Poutre de section <paramref name="width"/> × <paramref name="height"/> de l'axe a à l'axe b (fil du bois le long de la poutre) ;
        /// la hauteur est prise à la verticale (le long de x pour une poutre debout).
        /// </summary>
        public static void Beam(StyleMesh mesh, StyleVector a, StyleVector b, double width, double height, double tile, (double U, double V) offset = default)
        {
            StyleVector along = b - a;
            double length = along.Length;
            if (length < 1e-9) return;
            StyleVector dir = along * (1 / length);
            StyleVector reference = Math.Abs(dir.Y) > 0.9 ? new StyleVector(1, 0, 0) : StyleVector.Up;
            StyleVector side = StyleVector.Cross(reference, dir).Normalized;
            StyleVector up = StyleVector.Cross(dir, side).Normalized;
            Box(mesh, (a + b) * 0.5, dir * (length / 2), up * (height / 2), side * (width / 2), tile, offset);
        }

        /// <summary>
        /// Rondin lisse (cylindre de <paramref name="sides"/> côtés, sommets partagés) de l'axe a à l'axe b, fermé à ses deux bouts ;
        /// texture : le tour en largeur, la longueur en hauteur.
        /// </summary>
        public static void Log(StyleMesh mesh, StyleVector a, StyleVector b, double radius, int sides, double tile, double turn = 0)
        {
            StyleVector along = b - a;
            double length = along.Length;
            if (length < 1e-9 || sides < 3) return;
            StyleVector dir = along * (1 / length);
            StyleVector reference = Math.Abs(dir.Y) > 0.9 ? new StyleVector(1, 0, 0) : StyleVector.Up;
            StyleVector e1 = StyleVector.Cross(reference, dir).Normalized, e2 = StyleVector.Cross(dir, e1).Normalized;
            double scale = 1 / Math.Max(0.05, tile);
            var rings = new int[2, sides + 1];
            for (int r = 0; r < 2; r++)
                for (int s = 0; s <= sides; s++)
                {
                    double angle = 2 * Math.PI * s / sides + turn;
                    StyleVector radial = e1 * Math.Cos(angle) + e2 * Math.Sin(angle);
                    StyleVector p = (r == 0 ? a : b) + radial * radius;
                    rings[r, s] = mesh.Vertex(p, (2 * Math.PI * radius * s / sides * scale, r * length * scale));
                }
            for (int s = 0; s < sides; s++)
            {
                double angle = 2 * Math.PI * (s + 0.5) / sides + turn;
                StyleVector outward = e1 * Math.Cos(angle) + e2 * Math.Sin(angle);
                mesh.Indexed(rings[0, s], rings[1, s], rings[1, s + 1], outward);
                mesh.Indexed(rings[0, s], rings[1, s + 1], rings[0, s + 1], outward);
            }
            // Bouts : disques à facettes (section du bois).
            for (int end = 0; end < 2; end++)
            {
                StyleVector c = end == 0 ? a : b;
                StyleVector outward = end == 0 ? -dir : dir;
                for (int s = 0; s < sides; s++)
                {
                    double a0 = 2 * Math.PI * s / sides + turn, a1 = 2 * Math.PI * (s + 1) / sides + turn;
                    StyleVector p0 = c + (e1 * Math.Cos(a0) + e2 * Math.Sin(a0)) * radius, p1 = c + (e1 * Math.Cos(a1) + e2 * Math.Sin(a1)) * radius;
                    mesh.Triangle(c, p0, p1, (0.5 * scale, 0.5 * scale), (Math.Cos(a0) * radius * scale, Math.Sin(a0) * radius * scale),
                        (Math.Cos(a1) * radius * scale, Math.Sin(a1) * radius * scale), outward);
                }
            }
        }

        // --- Muret de pierres sèches ---------------------------------------------------------------------------------------

        /// <summary>Hauteur (m) du corps d'un muret de pierres sèches, sans les pierres debout du couronnement.</summary>
        public const double WallHeight = 0.85;

        /// <summary>
        /// Muret de pierres sèches sur l'emprise [x0, x1] × [z0, z1] (le long de son grand côté) : assises de pierres irrégulières en
        /// quinconce, plus larges en bas (fruit), pierres traversantes aux deux bouts, couronnement de pierres posées de chant. Le muret
        /// reste dans l'emprise (à quelques centimètres près, pierres de travers) ; texture de roche, décalée d'une pierre à l'autre.
        /// </summary>
        public static StyleMesh DryStoneWall(double x0, double z0, double x1, double z1, string id, double tile)
        {
            var mesh = new StyleMesh();
            bool alongX = x1 - x0 >= z1 - z0;
            double length = alongX ? x1 - x0 : z1 - z0, thickness = alongX ? z1 - z0 : x1 - x0;
            if (length <= 0.2 || thickness <= 0.1) return mesh;
            StyleVector dirL = alongX ? new StyleVector(1, 0, 0) : new StyleVector(0, 0, 1);
            StyleVector dirT = alongX ? new StyleVector(0, 0, 1) : new StyleVector(1, 0, 0);
            StyleVector start = alongX ? new StyleVector(x0, 0, (z0 + z1) / 2) : new StyleVector((x0 + x1) / 2, 0, z0);
            var random = new Sequence(id);
            double baseHalf = Math.Min(0.42, thickness * 0.42), topHalf = Math.Min(0.27, baseHalf * 0.65);
            Courses(mesh, random, start, dirL, dirT, length, baseHalf, topHalf, -0.06, WallHeight, tile);
            // Couronnement : pierres plates posées de chant en travers du muret, serrées, légèrement penchées.
            double s = 0.02;
            while (s < length - 0.05)
            {
                double w = random.Range(0.07, 0.15), h = random.Range(0.2, 0.3);
                w = Math.Min(w, length - 0.02 - s);
                if (w < 0.04) break;
                StyleVector center = start + dirL * (s + w / 2) + StyleVector.Up * (WallHeight + h / 2 - 0.03);
                double lean = random.Range(-0.12, 0.12), roll = random.Range(-0.06, 0.06);
                StyleVector ax = (dirL * Math.Cos(lean) + StyleVector.Up * Math.Sin(lean)) * (w / 2 - 0.006);
                StyleVector ay = (StyleVector.Up * Math.Cos(lean) - dirL * Math.Sin(lean) + dirT * roll) * (h / 2);
                StyleVector az = dirT * (topHalf + 0.03);
                Box(mesh, center, ax, ay, az, tile, (random.Next(), random.Next()), Jitter(random, 0.015));
                s += w;
            }
            return mesh;
        }

        /// <summary>
        /// Assises de pierres de <paramref name="y0"/> à <paramref name="y0"/> + <paramref name="height"/> le long de dirL (longueur
        /// <paramref name="length"/>), demi-épaisseur de <paramref name="baseHalf"/> en bas à <paramref name="topHalf"/> en haut.
        /// </summary>
        private static void Courses(StyleMesh mesh, Sequence random, StyleVector start, StyleVector dirL, StyleVector dirT, double length,
            double baseHalf, double topHalf, double y0, double height, double tile)
        {
            double y = y0;
            while (y < y0 + height - 0.04)
            {
                double progress = (y - y0) / height;
                double courseH = random.Range(0.13, 0.22) * (1.2 - 0.4 * progress);
                courseH = Math.Min(courseH, y0 + height - y);
                double half = baseHalf + (topHalf - baseHalf) * Math.Min(1, (y - y0 + courseH / 2) / height);
                double yc = y + courseH / 2;
                // Pierres traversantes aux deux bouts, sur toute l'épaisseur.
                double end = Math.Min(random.Range(0.3, 0.5), length / 2);
                Stone(mesh, random, start, dirL, dirT, 0, end, yc, courseH, 0, half, tile);
                Stone(mesh, random, start, dirL, dirT, length - end, length, yc, courseH, 0, half, tile);
                // Parements : une rangée de pierres de chaque côté, joints tirés au hasard (quinconce naturel).
                foreach (int face in new[] { -1, 1 })
                {
                    double s = end;
                    while (s < length - end - 0.01)
                    {
                        double len = random.Range(0.22, 0.55);
                        if (length - end - (s + len) < 0.15) len = length - end - s;
                        Stone(mesh, random, start, dirL, dirT, s, s + len, yc, courseH, face * half / 2, half / 2, tile);
                        s += len;
                    }
                }
                y += courseH;
            }
        }

        /// <summary>Une pierre de l'assise : de s0 à s1 le long du muret, centrée en t (en travers), demi-épaisseur depth.</summary>
        private static void Stone(StyleMesh mesh, Sequence random, StyleVector start, StyleVector dirL, StyleVector dirT, double s0, double s1,
            double yc, double courseH, double t, double depth, double tile)
        {
            double len = s1 - s0;
            if (len < 0.02) return;
            double yaw = random.Range(-0.05, 0.05), roll = random.Range(-0.04, 0.04);
            StyleVector along = (dirL * Math.Cos(yaw) + dirT * Math.Sin(yaw)).Normalized;
            StyleVector across = (dirT * Math.Cos(yaw) - dirL * Math.Sin(yaw)).Normalized;
            StyleVector up = (StyleVector.Up + across * roll).Normalized;
            StyleVector center = start + dirL * ((s0 + s1) / 2) + dirT * t + StyleVector.Up * yc;
            double gap = 0.008;
            Box(mesh, center, along * Math.Max(0.01, len / 2 - gap), up * Math.Max(0.01, courseH / 2 - gap), across * Math.Max(0.01, depth - gap * 0.5),
                tile, (random.Next(), random.Next()), Jitter(random, Math.Min(0.03, 0.18 * Math.Min(len, courseH))));
        }

        private static StyleVector[] Jitter(Sequence random, double amount)
        {
            var jitter = new StyleVector[8];
            for (int i = 0; i < 8; i++)
                jitter[i] = new StyleVector(random.Range(-amount, amount), random.Range(-amount, amount), random.Range(-amount, amount));
            return jitter;
        }

        // --- Pont cintré -----------------------------------------------------------------------------------------------------

        /// <summary>
        /// Dessus du tablier d'un pont cintré qui franchit [x0, x1] suivant x, de flèche <paramref name="rise"/> : 4 cm aux bouts,
        /// arc de parabole jusqu'à 4 cm + <paramref name="rise"/> au milieu ; 0 hors du pont.
        /// </summary>
        public static double DeckTop(double x, double x0, double x1, double rise)
        {
            if (x1 <= x0 || x < x0 || x > x1) return 0;
            double s = (2 * x - x0 - x1) / (x1 - x0);
            return 0.04 + rise * (1 - s * s);
        }

        /// <summary>
        /// Hauteur où se tient un personnage en (x, z) : sur le tablier du pont [x0, x1] × [z0, z1] (cintre <see cref="DeckTop"/>), 0 ailleurs.
        /// Affichage seulement : le moteur ne connaît pas la hauteur.
        /// </summary>
        public static double StandHeight(double x, double z, double x0, double z0, double x1, double z1, double rise) =>
            z >= z0 && z <= z1 ? DeckTop(x, x0, x1, rise) : 0;

        /// <summary>
        /// Pont cintré en bois qui franchit [x0, x1] suivant x, large de [z0, z1] : trois longerons qui suivent le cintre, planches posées
        /// en travers une à une (largeurs, longueurs et jeux variés), poteaux et deux lisses de rondins de chaque côté, culées de pierres
        /// sur les deux rives (du lit, à 1 m sous le sol, jusqu'au tablier).
        /// </summary>
        public static StyleBridgeShapes Bridge(double x0, double z0, double x1, double z1, double rise, string id, double woodTile, double barkTile,
            double stoneTile)
        {
            var shapes = new StyleBridgeShapes();
            if (x1 - x0 < 0.5 || z1 - z0 < 0.5) return shapes;
            var random = new Sequence(id);
            double Top(double x) => DeckTop(Math.Max(x0, Math.Min(x1, x)), x0, x1, rise);
            double Slope(double x)
            {
                double h = 0.01;
                return (Top(x + h) - Top(x - h)) / (2 * h);
            }
            const double Plank = 0.05;
            // Longerons : poutres par segments le long du cintre, sous les planches, prolongées dans les rives.
            const int Segments = 10;
            foreach (double z in new[] { z0 + 0.35, (z0 + z1) / 2, z1 - 0.35 })
                for (int i = 0; i < Segments; i++)
                {
                    double xa = x0 - 0.3 + (x1 - x0 + 0.6) * i / Segments, xb = x0 - 0.3 + (x1 - x0 + 0.6) * (i + 1) / Segments;
                    var a = new StyleVector(xa, Top(xa) - Plank - 0.11, z);
                    var b = new StyleVector(xb, Top(xb) - Plank - 0.11, z);
                    Beam(shapes.Wood, a, b, 0.18, 0.22, woodTile, (0, z * 0.37));
                }
            // Planches en travers, une à une.
            double x = x0 + 0.01;
            while (x < x1 - 0.05)
            {
                double w = random.Range(0.2, 0.27);
                w = Math.Min(w, x1 - x);
                double xc = x + w / 2, angle = Math.Atan(Slope(xc));
                double shorten = random.Range(0, 0.08), shift = random.Range(-0.04, 0.04), yaw = random.Range(-0.015, 0.015);
                StyleVector ax = new StyleVector(Math.Cos(angle) * Math.Cos(yaw), Math.Sin(angle), Math.Sin(yaw)) * (w / 2 - 0.007);
                StyleVector ay = new StyleVector(-Math.Sin(angle), Math.Cos(angle), 0) * (Plank / 2);
                StyleVector az = new StyleVector(-Math.Sin(yaw), 0, Math.Cos(yaw)) * ((z1 - z0) / 2 - shorten / 2);
                var center = new StyleVector(xc, Top(xc) - Plank / 2 + random.Range(-0.004, 0.004), (z0 + z1) / 2 + shift);
                Box(shapes.Wood, center, ax, ay, az, woodTile, (random.Next(), random.Next()));
                x += w;
            }
            // Poteaux et lisses (rondins) de chaque côté.
            int posts = Math.Max(2, (int)Math.Round((x1 - x0) / 1.4) + 1);
            foreach (double z in new[] { z0 + 0.09, z1 - 0.09 })
            {
                var tops = new List<StyleVector>();
                var middles = new List<StyleVector>();
                for (int i = 0; i < posts; i++)
                {
                    double px = x0 + 0.15 + (x1 - x0 - 0.3) * i / (posts - 1);
                    double deck = Top(px);
                    Beam(shapes.Wood, new StyleVector(px, deck - 0.3, z), new StyleVector(px, deck + 1.02, z), 0.14, 0.14, woodTile, (random.Next(), random.Next()));
                    tops.Add(new StyleVector(px, deck + 0.95, z));
                    middles.Add(new StyleVector(px, deck + 0.5, z));
                }
                for (int i = 0; i + 1 < posts; i++)
                {
                    Log(shapes.Bark, tops[i] - new StyleVector(0.05, 0, 0), tops[i + 1] + new StyleVector(0.05, 0, 0), 0.055, 8, barkTile, random.Range(0, 6.28));
                    Log(shapes.Bark, middles[i], middles[i + 1], 0.042, 7, barkTile, random.Range(0, 6.28));
                }
            }
            // Culées : pierres à cheval sur le bord des deux rives (la rivière commence 0,6 m après chaque bout), du lit jusque sous
            // les longerons.
            foreach ((double a, double b) in new[] { (x0 + 0.45, x0 + 1.15), (x1 - 1.15, x1 - 0.45) })
            {
                var start = new StyleVector((a + b) / 2, 0, z0 - 0.15);
                Courses(shapes.Stone, random, start, new StyleVector(0, 0, 1), new StyleVector(1, 0, 0), z1 - z0 + 0.3, (b - a) / 2, (b - a) / 2 * 0.85,
                    -1.05, 1.05 + Math.Max(0, Top((a + b) / 2) - Plank - 0.22), stoneTile);
            }
            return shapes;
        }

        // --- Abri à poteaux ---------------------------------------------------------------------------------------------------

        /// <summary>Pente du toit de l'abri (tangente, environ 30°).</summary>
        public const double RoofPitch = 0.577;

        /// <summary>
        /// Abri sur poteaux (les piliers du moteur) : poteaux de bois équarri sur socles de pierre, sablières et entraits, aisseliers,
        /// poinçons et faîtage, chevrons, toit à deux pans couvert de tuiles (rangées parallèles au faîtage), débords tout autour. Le
        /// faîtage suit le plus grand côté du rectangle des poteaux.
        /// </summary>
        public static StyleShelterShapes Shelter(IReadOnlyList<(double X, double Z)> posts, string id, double woodTile, double roofTile, double stoneTile)
        {
            var shapes = new StyleShelterShapes();
            if (posts == null || posts.Count < 2) return shapes;
            double px0 = double.MaxValue, px1 = double.MinValue, pz0 = double.MaxValue, pz1 = double.MinValue;
            foreach ((double X, double Z) p in posts)
            {
                px0 = Math.Min(px0, p.X);
                px1 = Math.Max(px1, p.X);
                pz0 = Math.Min(pz0, p.Z);
                pz1 = Math.Max(pz1, p.Z);
            }
            var random = new Sequence(id);
            bool alongX = px1 - px0 >= pz1 - pz0;
            // Repère local : a le long du faîtage, b en travers ; demi-longueurs A et B entre axes de poteaux.
            StyleVector dirA = alongX ? new StyleVector(1, 0, 0) : new StyleVector(0, 0, 1);
            StyleVector dirB = alongX ? new StyleVector(0, 0, 1) : new StyleVector(1, 0, 0);
            var origin = new StyleVector((px0 + px1) / 2, 0, (pz0 + pz1) / 2);
            double A = (alongX ? px1 - px0 : pz1 - pz0) / 2, B = Math.Max(0.5, (alongX ? pz1 - pz0 : px1 - px0) / 2);
            StyleVector At(double a, double y, double b) => origin + dirA * a + StyleVector.Up * y + dirB * b;
            const double PostTop = 2.55, PlateTop = 2.75, TieTop = 2.95, EaveOut = 0.7, GableOut = 0.55, Thick = 0.09;
            double ridgeUnder = TieTop + B * RoofPitch;
            double Under(double b) => ridgeUnder - Math.Abs(b) * RoofPitch;
            // Poteaux, socles, aisseliers.
            foreach ((double X, double Z) p in posts)
            {
                var foot = new StyleVector(p.X, 0, p.Z);
                Box(shapes.Stone, foot + StyleVector.Up * 0.05, new StyleVector(0.24, 0, 0), new StyleVector(0, 0.08, 0), new StyleVector(0, 0, 0.24), stoneTile,
                    (random.Next(), random.Next()), Jitter(random, 0.02));
                Beam(shapes.Frame, foot + StyleVector.Up * 0.12, foot + StyleVector.Up * PostTop, 0.26, 0.26, woodTile, (random.Next(), random.Next()));
                double a = StyleVector.Dot(foot - origin, dirA), b = StyleVector.Dot(foot - origin, dirB);
                double toward = a > 0 ? -1 : 1;
                Beam(shapes.Frame, At(a, PostTop - 0.6, b), At(a + toward * 0.55, PostTop - 0.02, b), 0.11, 0.11, woodTile, (random.Next(), random.Next()));
            }
            // Sablières le long du faîtage, sur les poteaux ; entraits en travers, sur les sablières.
            foreach (double b in new[] { -B, B })
                Beam(shapes.Frame, At(-A - 0.35, (PostTop + PlateTop) / 2, b), At(A + 0.35, (PostTop + PlateTop) / 2, b), 0.22, PlateTop - PostTop, woodTile,
                    (random.Next(), random.Next()));
            foreach (double a in new[] { -A, A })
                Beam(shapes.Frame, At(a, (PlateTop + TieTop) / 2, -B - 0.3), At(a, (PlateTop + TieTop) / 2, B + 0.3), 0.2, TieTop - PlateTop, woodTile,
                    (random.Next(), random.Next()));
            // Sous le toit (retiré avec lui) : poinçons, faîtage, chevrons.
            foreach (double a in new[] { -A, A })
                Beam(shapes.RoofWood, At(a, TieTop, 0), At(a, ridgeUnder - 0.21, 0), 0.18, 0.18, woodTile, (random.Next(), random.Next()));
            Beam(shapes.RoofWood, At(-A - GableOut, ridgeUnder - 0.11, 0), At(A + GableOut, ridgeUnder - 0.11, 0), 0.2, 0.22, woodTile, (random.Next(), random.Next()));
            int rafters = Math.Max(2, (int)Math.Round((2 * A + 2 * GableOut - 0.2) / 0.7) + 1);
            for (int i = 0; i < rafters; i++)
            {
                double a = -A - GableOut + 0.1 + (2 * A + 2 * GableOut - 0.2) * i / (rafters - 1);
                foreach (int side in new[] { -1, 1 })
                    Beam(shapes.RoofWood, At(a, Under(0) - 0.07, 0), At(a, Under(B + EaveOut) - 0.07, side * (B + EaveOut)), 0.08, 0.12, woodTile,
                        (random.Next(), random.Next()));
            }
            // Pans du toit : tuiles dessus (rangées parallèles au faîtage, du bas vers le haut), voliges dessous et sur les tranches.
            double roofScale = 1 / Math.Max(0.05, roofTile), woodScale = 1 / Math.Max(0.05, woodTile);
            double slopeLength = (B + EaveOut) * Math.Sqrt(1 + RoofPitch * RoofPitch);
            foreach (int side in new[] { -1, 1 })
            {
                double ridgeB = 0, eaveB = side * (B + EaveOut);
                StyleVector r0 = At(-A - GableOut, Under(ridgeB) + Thick, ridgeB), r1 = At(A + GableOut, Under(ridgeB) + Thick, ridgeB);
                StyleVector e0 = At(-A - GableOut, Under(eaveB) + Thick, eaveB), e1 = At(A + GableOut, Under(eaveB) + Thick, eaveB);
                StyleVector down = StyleVector.Up * -Thick;
                StyleVector normal = (StyleVector.Up + dirB * (side * RoofPitch)).Normalized;
                double u0 = (-A - GableOut) * roofScale, u1 = (A + GableOut) * roofScale, v1 = slopeLength * roofScale;
                shapes.RoofTiles.Quad(e0, e1, r1, r0, (u0, 0), (u1, 0), (u1, v1), (u0, v1), normal);
                double w0 = (-A - GableOut) * woodScale, w1 = (A + GableOut) * woodScale, wv = slopeLength * woodScale;
                shapes.RoofWood.Quad(e0 + down, e1 + down, r1 + down, r0 + down, (w0, 0), (w1, 0), (w1, wv), (w0, wv), -normal);
                // Tranches : égout (bas du pan) et rives (pignons).
                StyleVector eaveOut = dirB * side;
                shapes.RoofWood.Quad(e0 + down, e1 + down, e1, e0, (w0, 0), (w1, 0), (w1, Thick * woodScale), (w0, Thick * woodScale), eaveOut);
                foreach ((StyleVector r, StyleVector e, StyleVector outward) in new[] { (r0, e0, -dirA), (r1, e1, dirA) })
                    shapes.RoofWood.Quad(e + down, r + down, r, e, (0, 0), (wv, 0), (wv, Thick * woodScale), (0, Thick * woodScale), outward);
            }
            // Faîtière : rangée de tuiles rondes sur le faîtage.
            Log(shapes.RoofTiles, At(-A - GableOut - 0.05, Under(0) + Thick + 0.03, 0), At(A + GableOut + 0.05, Under(0) + Thick + 0.03, 0), 0.1, 10, roofTile);
            StyleVector min = At(-A - GableOut, 0, -B - EaveOut), max = At(A + GableOut, 0, B + EaveOut);
            shapes.X0 = Math.Min(min.X, max.X);
            shapes.X1 = Math.Max(min.X, max.X);
            shapes.Z0 = Math.Min(min.Z, max.Z);
            shapes.Z1 = Math.Max(min.Z, max.Z);
            shapes.RoofBottom = Under(B);
            shapes.RoofTop = Under(0) + Thick + 0.13;
            return shapes;
        }

        /// <summary>Suite fixe tirée d'un identifiant (FNV-1a puis xorshift) : les mêmes formes à chaque lancement.</summary>
        private sealed class Sequence
        {
            private uint _state;

            public Sequence(string id)
            {
                uint hash = 2166136261;
                foreach (char c in id ?? string.Empty)
                {
                    hash ^= c;
                    hash = unchecked(hash * 16777619);
                }
                _state = hash == 0 ? 0x9E3779B9 : hash;
            }

            /// <summary>Nombre dans [0, 1[.</summary>
            public double Next()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return (_state >> 8) / 16777216.0;
            }

            public double Range(double a, double b) => a + (b - a) * Next();
        }
    }
}
