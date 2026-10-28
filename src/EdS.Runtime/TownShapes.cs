using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Runtime
{
    /// <summary>Vallons agrandis : couverture d'un toit (tuiles, tuiles sombres, ardoises, bardeaux de bois).</summary>
    public enum StyleRoofLook
    {
        Tiles = 0,
        DarkTiles = 1,
        Slate = 2,
        Shingles = 3,
    }

    /// <summary>
    /// Vallons agrandis : pièces d'un bâtiment fait par le code, une par matière : murs (crépi, pierre ou planches selon
    /// <see cref="WallSurface"/>), pierre (soubassement, cheminée, margelle), bois (colombages, portes, volets, poteaux, comptoir), toit
    /// (<see cref="RoofLook"/>), toile (auvent, tente ; <see cref="ClothStripes"/> pour les bandes claires d'un auvent rayé) et ouvertures
    /// sombres (vitres, embrasures, eau du puits).
    /// </summary>
    public sealed class StyleBuildingShapes
    {
        public StyleMesh Walls { get; } = new StyleMesh();
        public StyleMesh Stone { get; } = new StyleMesh();
        public StyleMesh Wood { get; } = new StyleMesh();
        public StyleMesh Roof { get; } = new StyleMesh();
        public StyleMesh Cloth { get; } = new StyleMesh();
        public StyleMesh ClothStripes { get; } = new StyleMesh();
        public StyleMesh Dark { get; } = new StyleMesh();

        /// <summary>Matière des murs.</summary>
        public StyleSurface WallSurface { get; internal set; } = StyleSurface.Plaster;

        /// <summary>Couverture du toit.</summary>
        public StyleRoofLook RoofLook { get; internal set; } = StyleRoofLook.Tiles;

        /// <summary>Couleur de la toile : 0 rouge, 1 bleue, 2 toile écrue, 3 toile sombre.</summary>
        public int ClothColor { get; internal set; }

        /// <summary>Plus haut point (m).</summary>
        public double Top { get; internal set; }

        /// <summary>Toutes les pièces, pour les parcourir ensemble.</summary>
        public IEnumerable<StyleMesh> All => new[] { Walls, Stone, Wood, Roof, Cloth, ClothStripes, Dark };
    }

    /// <summary>
    /// Vallons agrandis (demande de Damien du 28 octobre 2026 : « on pourrait mettre des bâtiments, des routes ») : bâtiments du bourg,
    /// du hameau et du camp faits par le code, sans Unity, sur l'emprise exacte de leur obstacle (seuls les débords de toit, l'enseigne et
    /// l'auvent en sortent, au-dessus de la tête) ; la porte donne sur la route la plus proche (<see cref="TownRules.Facing"/>). Mêmes
    /// formes à chaque lancement. Coordonnées de texture à l'échelle du monde (une répétition tous les <c>tile</c> mètres), comme
    /// <see cref="StyleShapes"/>. Affichage seulement.
    /// </summary>
    public static class TownShapes
    {
        /// <summary>Hauteur (m) du soubassement de pierre des maisons.</summary>
        public const double Plinth = 0.5;

        /// <summary>Couverture du toit d'un bâtiment (comme sur la carte peinte).</summary>
        public static StyleRoofLook RoofLookOf(StyleBuilding kind, string id)
        {
            switch (kind)
            {
                case StyleBuilding.Chapel:
                case StyleBuilding.Tower:
                    return StyleRoofLook.Slate;
                case StyleBuilding.Barn:
                case StyleBuilding.Well:
                case StyleBuilding.Stall:
                    return StyleRoofLook.Shingles;
                case StyleBuilding.Inn:
                    return StyleRoofLook.DarkTiles;
                case StyleBuilding.House:
                    int v = StyleRules.Variant(id ?? string.Empty, 3);
                    return v == 2 ? StyleRoofLook.Shingles : v == 1 ? StyleRoofLook.DarkTiles : StyleRoofLook.Tiles;
                default:
                    return StyleRoofLook.Tiles;
            }
        }

        /// <summary>
        /// Cheminée d'une maison ou de l'auberge : centre au sol (x, z) et demi-côté, sur le pan arrière, au quart du faîte (côté tiré de
        /// l'identifiant) ; null pour les autres bâtiments. Le décor et la carte peinte la placent au même endroit.
        /// </summary>
        public static (double X, double Z, double Half)? ChimneyOf(StyleBuilding kind, IReadOnlyList<(double X, double Z)> footprint, string id, (int X, int Z) facing)
        {
            if (kind != StyleBuilding.House && kind != StyleBuilding.Inn || footprint == null || footprint.Count < 3) return null;
            var f = new Frame(footprint);
            (_, int sideB) = f.Side(facing);
            double back = sideB != 0 ? -sideB : 1;
            StyleVector at = f.At(f.L * (StyleRules.Variant((id ?? string.Empty) + "/cheminee", 2) == 0 ? 0.45 : -0.45), 0, back * f.D * 0.4);
            return (at.X, at.Z, 0.38);
        }

        /// <summary>
        /// Clocher de la chapelle : centre au sol (x, z) et demi-côté de sa tour carrée, au bout opposé à l'entrée (chevet) ; null pour les
        /// autres bâtiments. Le décor et la carte peinte le placent au même endroit.
        /// </summary>
        public static (double X, double Z, double Half)? BellTowerOf(StyleBuilding kind, IReadOnlyList<(double X, double Z)> footprint, (int X, int Z) facing)
        {
            if (kind != StyleBuilding.Chapel || footprint == null || footprint.Count < 3) return null;
            var f = new Frame(footprint);
            (int sideA, _) = f.Side(facing);
            double half = Math.Min(f.D * 0.55, f.L * 0.45);
            StyleVector at = f.At((sideA != 0 ? -sideA : 1) * (f.L - half - 0.05), 0, 0);
            return (at.X, at.Z, half);
        }

        /// <summary>
        /// Bâtiment <paramref name="kind"/> sur l'emprise <paramref name="footprint"/> (polygone convexe de l'obstacle, mètres) ;
        /// <paramref name="facing"/> : côté de la porte (vecteur d'axe). Rien pour <see cref="StyleBuilding.None"/> ni pour un bois.
        /// </summary>
        public static StyleBuildingShapes Building(StyleBuilding kind, IReadOnlyList<(double X, double Z)> footprint, string id, (int X, int Z) facing,
            double wallTile, double stoneTile, double woodTile, double roofTile)
        {
            var shapes = new StyleBuildingShapes { RoofLook = RoofLookOf(kind, id) };
            if (footprint == null || footprint.Count < 3) return shapes;
            var t = new Tiles(wallTile, stoneTile, woodTile, roofTile);
            switch (kind)
            {
                case StyleBuilding.House:
                case StyleBuilding.Inn:
                case StyleBuilding.Chapel:
                case StyleBuilding.Barn:
                    Hall(shapes, kind, footprint, id ?? string.Empty, facing, t);
                    break;
                case StyleBuilding.Well:
                    Well(shapes, footprint, t);
                    break;
                case StyleBuilding.Stall:
                    Stall(shapes, footprint, id ?? string.Empty, facing, t);
                    break;
                case StyleBuilding.Tower:
                    Tower(shapes, footprint, facing, t);
                    break;
                case StyleBuilding.Tent:
                    Tent(shapes, footprint, id ?? string.Empty, facing, t);
                    break;
            }
            shapes.Top = shapes.All.Where(m => m.Vertices.Count > 0).Select(m => m.Bounds().Max.Y).DefaultIfEmpty(0).Max();
            return shapes;
        }

        private readonly struct Tiles
        {
            public Tiles(double wall, double stone, double wood, double roof)
            {
                Wall = Math.Max(0.1, wall);
                Stone = Math.Max(0.1, stone);
                Wood = Math.Max(0.1, wood);
                Roof = Math.Max(0.1, roof);
            }

            public double Wall { get; }
            public double Stone { get; }
            public double Wood { get; }
            public double Roof { get; }
        }

        /// <summary>Repère d'un rectangle : centre, axe long (u), axe court (v), demi-longueur et demi-largeur.</summary>
        private readonly struct Frame
        {
            public Frame(IReadOnlyList<(double X, double Z)> footprint)
            {
                double x0 = footprint.Min(p => p.X), x1 = footprint.Max(p => p.X), z0 = footprint.Min(p => p.Z), z1 = footprint.Max(p => p.Z);
                AlongX = x1 - x0 >= z1 - z0;
                Origin = new StyleVector((x0 + x1) / 2, 0, (z0 + z1) / 2);
                U = AlongX ? new StyleVector(1, 0, 0) : new StyleVector(0, 0, 1);
                V = AlongX ? new StyleVector(0, 0, 1) : new StyleVector(1, 0, 0);
                L = (AlongX ? x1 - x0 : z1 - z0) / 2;
                D = (AlongX ? z1 - z0 : x1 - x0) / 2;
            }

            public bool AlongX { get; }
            public StyleVector Origin { get; }
            public StyleVector U { get; }
            public StyleVector V { get; }
            public double L { get; }
            public double D { get; }

            public StyleVector At(double a, double y, double b) => Origin + U * a + StyleVector.Up * y + V * b;

            /// <summary>Le côté <paramref name="facing"/> (vecteur d'axe du monde) en repère local : (signe le long de u, signe le long de v).</summary>
            public (int A, int B) Side((int X, int Z) facing)
            {
                int fx = facing.X, fz = facing.Z;
                int a = AlongX ? fx : fz, b = AlongX ? fz : fx;
                if (a == 0 && b == 0) b = -1;
                return (a, b);
            }
        }

        /// <summary>Quadrilatère vertical sur un mur : de (a0, y0) à (a1, y1) le long de <paramref name="along"/>, à <paramref name="at"/>.</summary>
        private static void WallQuad(StyleMesh mesh, StyleVector at, StyleVector along, StyleVector outward, double a0, double a1, double y0, double y1, double tile)
        {
            double s = 1 / tile;
            StyleVector p0 = at + along * a0 + StyleVector.Up * y0, p1 = at + along * a1 + StyleVector.Up * y0;
            StyleVector p2 = at + along * a1 + StyleVector.Up * y1, p3 = at + along * a0 + StyleVector.Up * y1;
            mesh.Quad(p0, p1, p2, p3, (a0 * s, y0 * s), (a1 * s, y0 * s), (a1 * s, y1 * s), (a0 * s, y1 * s), outward);
        }

        /// <summary>Une ouverture d'un mur : position le long du mur (centre), largeur, bas et haut ; porte ou fenêtre.</summary>
        private readonly struct Opening
        {
            public Opening(double center, double width, double bottom, double top, bool door)
            {
                Center = center;
                Width = width;
                Bottom = bottom;
                Top = top;
                Door = door;
            }

            public double Center { get; }
            public double Width { get; }
            public double Bottom { get; }
            public double Top { get; }
            public bool Door { get; }
        }

        // --- Maisons, auberge, chapelle, grange -----------------------------------------------------------------------------

        private static void Hall(StyleBuildingShapes shapes, StyleBuilding kind, IReadOnlyList<(double X, double Z)> footprint, string id, (int X, int Z) facing, Tiles t)
        {
            var f = new Frame(footprint);
            double wallTop = TownRules.WallHeight(kind);
            double pitch = kind == StyleBuilding.Chapel ? 1.15 : kind == StyleBuilding.Barn ? 0.95 : 0.85;   // pente : hauteur par mètre
            shapes.WallSurface = kind == StyleBuilding.Chapel ? StyleSurface.Stone : kind == StyleBuilding.Barn ? StyleSurface.Wood : StyleSurface.Plaster;
            double plinth = kind == StyleBuilding.Barn ? 0.3 : Plinth;
            (int sideA, int sideB) = f.Side(facing);
            double ridge = wallTop + f.D * pitch;

            // Soubassement de pierre, un peu plus large que les murs.
            StyleShapes.Box(shapes.Stone, f.At(0, plinth / 2, 0), f.U * (f.L + 0.04), StyleVector.Up * (plinth / 2), f.V * (f.D + 0.04), t.Stone);

            // Ouvertures de chaque mur : (axe du mur, sortie, longueur) ; la porte sur le côté de la route.
            int upper = kind == StyleBuilding.Inn ? 2 : kind == StyleBuilding.House ? 1 : 0;
            double floor2 = kind == StyleBuilding.Inn ? 3.2 : 2.35;
            foreach ((StyleVector outward, StyleVector along, double half, double reach, bool gable) in new[]
            {
                (f.V, f.U, f.L, f.D, false), (-f.V, f.U, f.L, f.D, false), (f.U, f.V, f.D, f.L, true), (-f.U, f.V, f.D, f.L, true),
            })
            {
                bool front = gable ? sideA != 0 && StyleVector.Dot(outward, f.U) * sideA > 0 : sideA == 0 && StyleVector.Dot(outward, f.V) * sideB > 0;
                var openings = new List<Opening>();
                if (front)
                {
                    double w = kind == StyleBuilding.Barn ? Math.Min(3.4, half * 1.2) : kind == StyleBuilding.Chapel ? 1.6 : 1.1;
                    double h = kind == StyleBuilding.Barn ? Math.Min(3.4, wallTop - 0.6) : kind == StyleBuilding.Chapel ? 2.8 : 2.15;
                    openings.Add(new Opening(0, w, plinth - 0.05, plinth - 0.05 + h, true));
                }
                if (kind != StyleBuilding.Barn)
                {
                    double spacing = kind == StyleBuilding.Chapel ? 2.6 : 2.4;
                    int count = Math.Max(0, (int)Math.Floor((2 * half - 1.6) / spacing));
                    for (int i = 0; i < count; i++)
                    {
                        double c = -half + 0.8 + spacing * (i + 0.5) + (2 * half - 1.6 - spacing * count) / 2;
                        bool clear = openings.All(o => Math.Abs(o.Center - c) > o.Width / 2 + 0.75);
                        if (kind == StyleBuilding.Chapel)
                        {
                            if (!gable && clear) openings.Add(new Opening(c, 0.62, 2.0, 4.1, false));
                            continue;
                        }
                        if (!gable && clear) openings.Add(new Opening(c, 0.85, plinth + 0.6, plinth + 1.55, false));
                        for (int k = 1; k <= upper; k++)
                            if (!gable) openings.Add(new Opening(c, 0.8, floor2 + 0.35 + (k - 1) * 1.6, floor2 + 1.2 + (k - 1) * 1.6, false));
                    }
                    if (gable && kind != StyleBuilding.Chapel && upper > 0)
                        openings.Add(new Opening(0, 0.7, wallTop + 0.25, wallTop + 0.95, false));   // lucarne du pignon
                }
                else if (!front)
                    openings.Add(new Opening(0, 0.9, wallTop - 1.5, wallTop - 0.8, false));        // fenêtre haute du fenil

                StyleVector at = f.Origin + outward * reach;
                Wall(shapes, at, along, outward, half, plinth, wallTop, openings, t);
                if (gable)
                {
                    // Pignon : triangle jusqu'au faîte, avec sa lucarne s'il en a une.
                    StyleVector p0 = at + along * -half + StyleVector.Up * wallTop, p1 = at + along * half + StyleVector.Up * wallTop;
                    StyleVector p2 = at + StyleVector.Up * ridge;
                    double s = 1 / WallTileOf(shapes.WallSurface, t);
                    shapes.Walls.Triangle(p0, p1, p2, (-half * s, wallTop * s), (half * s, wallTop * s), (0, ridge * s), outward);
                }
                // Colombages (maisons, auberge) : poteaux d'angle, sablières, poteaux entre les ouvertures, croix de Saint-André.
                if (kind == StyleBuilding.House || kind == StyleBuilding.Inn)
                    Timbers(shapes, at, along, outward, half, plinth, wallTop, floor2, openings, gable, ridge, t);
            }

            // Toit à deux pans, faîte le long du grand côté, débords ; dessous et tranches en bois.
            HallRoof(shapes, f, wallTop, pitch, kind == StyleBuilding.Barn ? 0.45 : 0.55, 0.35, t);

            // Cheminée (maisons, auberge) sur le pan arrière.
            if (kind == StyleBuilding.House || kind == StyleBuilding.Inn)
            {
                (double cx, double cz, _) = ChimneyOf(kind, footprint, id, facing)!.Value;
                double ca = StyleVector.Dot(new StyleVector(cx, 0, cz) - f.Origin, f.U), cb = StyleVector.Dot(new StyleVector(cx, 0, cz) - f.Origin, f.V);
                StyleShapes.Box(shapes.Stone, f.At(ca, (wallTop + ridge + 1.0) / 2, cb), f.U * 0.38, StyleVector.Up * ((ridge + 1.0 - wallTop) / 2), f.V * 0.38, t.Stone);
                StyleShapes.Box(shapes.Stone, f.At(ca, ridge + 1.06, cb), f.U * 0.45, StyleVector.Up * 0.06, f.V * 0.45, t.Stone);
                shapes.Dark.Quad(f.At(ca - 0.28, ridge + 1.121, cb - 0.28), f.At(ca + 0.28, ridge + 1.121, cb - 0.28), f.At(ca + 0.28, ridge + 1.121, cb + 0.28),
                    f.At(ca - 0.28, ridge + 1.121, cb + 0.28), (0, 0), (1, 0), (1, 1), (0, 1), StyleVector.Up);
            }

            // Enseigne de l'auberge : potence et panneau, au-dessus de la porte.
            if (kind == StyleBuilding.Inn)
            {
                StyleVector door = sideA != 0 ? f.U * sideA : f.V * sideB;
                double reach = sideA != 0 ? f.L : f.D;
                StyleVector side = sideA != 0 ? f.V : f.U;
                StyleVector root = f.Origin + door * reach + side * 1.4 + StyleVector.Up * 3.0;
                StyleShapes.Beam(shapes.Wood, root, root + door * 1.0, 0.1, 0.12, t.Wood);
                StyleShapes.Beam(shapes.Wood, root + StyleVector.Up * -0.6, root + door * 0.55 + StyleVector.Up * -0.02, 0.07, 0.07, t.Wood);
                StyleShapes.Box(shapes.Wood, root + door * 0.62 + StyleVector.Up * -0.42, door * 0.32, StyleVector.Up * 0.26, side * 0.03, t.Wood);
            }

            // Clocher de la chapelle : tour carrée au bout opposé à l'entrée (chevet), abat-sons, flèche d'ardoises.
            if (kind == StyleBuilding.Chapel)
            {
                (double tx, double tz, double half) = BellTowerOf(kind, footprint, facing)!.Value;
                double ta = StyleVector.Dot(new StyleVector(tx, 0, tz) - f.Origin, f.U);
                double towerTop = ridge + 2.2;
                StyleShapes.Box(shapes.Walls, f.At(ta, towerTop / 2, 0), f.U * half, StyleVector.Up * (towerTop / 2), f.V * half, WallTileOf(shapes.WallSurface, t));
                foreach ((StyleVector outward, StyleVector along) in new[] { (f.U, f.V), (-f.U, f.V), (f.V, f.U), (-f.V, f.U) })
                {
                    StyleVector face = f.At(ta, 0, 0) + outward * (half + 0.01);
                    WallQuad(shapes.Dark, face, along, outward, -half * 0.35, half * 0.35, towerTop - 1.9, towerTop - 0.5, 1);
                    for (int k = 0; k < 4; k++)   // lames de l'abat-son
                    {
                        double y = towerTop - 1.75 + k * 0.32;
                        StyleShapes.Box(shapes.Wood, face + outward * 0.03 + StyleVector.Up * y, along * (half * 0.36), StyleVector.Up * 0.04, outward * 0.04, t.Wood);
                    }
                }
                Spire(shapes.Roof, f.At(ta, towerTop, 0), f.U, f.V, half + 0.15, towerTop + half * 2.6, t.Roof);
                StyleShapes.Beam(shapes.Wood, f.At(ta, towerTop + half * 2.6 - 0.1, 0), f.At(ta, towerTop + half * 2.6 + 0.9, 0), 0.08, 0.08, t.Wood);
                StyleShapes.Beam(shapes.Wood, f.At(ta - 0.3, towerTop + half * 2.6 + 0.6, 0), f.At(ta + 0.3, towerTop + half * 2.6 + 0.6, 0), 0.08, 0.08, t.Wood);
            }
        }

        private static double WallTileOf(StyleSurface surface, Tiles t) =>
            surface == StyleSurface.Stone ? t.Stone : surface == StyleSurface.Wood ? t.Wood : t.Wall;

        /// <summary>
        /// Un mur de <paramref name="half"/> de demi-longueur, de <paramref name="bottom"/> à <paramref name="top"/>, percé de ses
        /// ouvertures : bandes pleines entre elles, linteaux et allèges ; vitres et embrasures sombres en retrait, portes en planches,
        /// encadrements et volets.
        /// </summary>
        private static void Wall(StyleBuildingShapes shapes, StyleVector at, StyleVector along, StyleVector outward, double half, double bottom, double top,
            List<Opening> openings, Tiles t)
        {
            double tile = WallTileOf(shapes.WallSurface, t);
            // Colonnes du mur coupées aux bords des ouvertures.
            var cuts = new SortedSet<double> { -half, half };
            foreach (Opening o in openings)
            {
                cuts.Add(Math.Max(-half, o.Center - o.Width / 2));
                cuts.Add(Math.Min(half, o.Center + o.Width / 2));
            }
            double[] xs = cuts.ToArray();
            for (int i = 0; i + 1 < xs.Length; i++)
            {
                double a0 = xs[i], a1 = xs[i + 1], mid = (a0 + a1) / 2;
                if (a1 - a0 < 1e-6) continue;
                // Bandes verticales de la colonne, entre les ouvertures qui la traversent.
                var holes = openings.Where(o => Math.Abs(o.Center - mid) < o.Width / 2).OrderBy(o => o.Bottom).ToList();
                double y = bottom;
                foreach (Opening o in holes)
                {
                    if (o.Bottom > y + 1e-6) WallQuad(shapes.Walls, at, along, outward, a0, a1, y, Math.Min(o.Bottom, top), tile);
                    y = Math.Max(y, o.Top);
                }
                if (top > y + 1e-6) WallQuad(shapes.Walls, at, along, outward, a0, a1, y, top, tile);
            }
            foreach (Opening o in openings)
            {
                double a0 = o.Center - o.Width / 2, a1 = o.Center + o.Width / 2;
                if (o.Bottom >= top - 1e-9)
                {
                    // Lucarne du pignon (au-dessus du mur) : vitre posée sur le pignon, cadre et croisillon.
                    WallQuad(shapes.Dark, at + outward * 0.015, along, outward, a0, a1, o.Bottom, o.Top, 1);
                    StyleShapes.Box(shapes.Wood, at + outward * 0.03 + along * o.Center + StyleVector.Up * ((o.Bottom + o.Top) / 2), along * 0.025,
                        StyleVector.Up * ((o.Top - o.Bottom) / 2), outward * 0.02, t.Wood);
                    foreach (double y in new[] { o.Bottom, o.Top })
                        StyleShapes.Box(shapes.Wood, at + outward * 0.03 + along * o.Center + StyleVector.Up * y, along * (o.Width / 2 + 0.05), StyleVector.Up * 0.04,
                            outward * 0.03, t.Wood);
                    continue;
                }
                StyleVector back = at - outward * 0.12;
                // Embrasure : fond sombre en retrait et ses joues.
                WallQuad(shapes.Dark, back, along, outward, a0, a1, o.Bottom, o.Top, 1);
                if (o.Door && o.Bottom > 0.1)
                {
                    // Relecture des Vallons agrandis : deux marches de pierre devant la porte (le seuil est sur le soubassement).
                    StyleShapes.Box(shapes.Stone, at + along * o.Center + outward * 0.17 + StyleVector.Up * (o.Bottom / 4), along * (o.Width / 2 + 0.2),
                        StyleVector.Up * (o.Bottom / 4), outward * 0.17, t.Stone);
                    StyleShapes.Box(shapes.Stone, at + along * o.Center + outward * 0.085 + StyleVector.Up * (o.Bottom * 0.75), along * (o.Width / 2 + 0.12),
                        StyleVector.Up * (o.Bottom / 4), outward * 0.085, t.Stone);
                }
                if (o.Door)
                {
                    // Porte de planches fermée dans l'embrasure, avec ses pentures.
                    StyleShapes.Box(shapes.Wood, back + outward * 0.03 + along * o.Center + StyleVector.Up * ((o.Bottom + o.Top) / 2), along * (o.Width / 2 - 0.02),
                        StyleVector.Up * ((o.Top - o.Bottom) / 2 - 0.02), outward * 0.03, t.Wood);
                    foreach (double hy in new[] { o.Bottom + 0.4, o.Top - 0.45 })
                        StyleShapes.Box(shapes.Dark, back + outward * 0.07 + along * (o.Center - o.Width * 0.18) + StyleVector.Up * hy, along * (o.Width * 0.3),
                            StyleVector.Up * 0.03, outward * 0.01, 1);
                }
                else
                {
                    // Croisillons de la fenêtre.
                    StyleShapes.Box(shapes.Wood, back + outward * 0.02 + along * o.Center + StyleVector.Up * ((o.Bottom + o.Top) / 2), along * 0.025,
                        StyleVector.Up * ((o.Top - o.Bottom) / 2), outward * 0.02, t.Wood);
                    StyleShapes.Box(shapes.Wood, back + outward * 0.02 + along * o.Center + StyleVector.Up * ((o.Bottom + o.Top) / 2), along * (o.Width / 2),
                        StyleVector.Up * 0.025, outward * 0.02, t.Wood);
                    // Volets ouverts de part et d'autre (pas pour la chapelle, ni les fenêtres trop près d'un angle).
                    if (shapes.WallSurface == StyleSurface.Plaster && a0 - 0.45 > -half + 0.15 && a1 + 0.45 < half - 0.15)
                        foreach (double side in new[] { a0 - 0.23, a1 + 0.23 })
                            StyleShapes.Box(shapes.Wood, at + outward * 0.04 + along * side + StyleVector.Up * ((o.Bottom + o.Top) / 2), along * 0.21,
                                StyleVector.Up * ((o.Top - o.Bottom) / 2 + 0.02), outward * 0.025, t.Wood);
                }
                // Joues et linteau de l'embrasure.
                foreach (double a in new[] { a0, a1 })
                {
                    StyleVector inward = along * (a == a0 ? 1 : -1);
                    StyleVector q0 = at + along * a + StyleVector.Up * o.Bottom, q1 = back + along * a + StyleVector.Up * o.Bottom;
                    StyleVector q2 = back + along * a + StyleVector.Up * o.Top, q3 = at + along * a + StyleVector.Up * o.Top;
                    shapes.Walls.Quad(q0, q1, q2, q3, (0, o.Bottom / tile), (0.12 / tile, o.Bottom / tile), (0.12 / tile, o.Top / tile), (0, o.Top / tile), inward);
                }
                StyleVector l0 = at + along * a0 + StyleVector.Up * o.Top, l1 = at + along * a1 + StyleVector.Up * o.Top;
                shapes.Walls.Quad(l0, l1, l1 - outward * 0.12, l0 - outward * 0.12, (a0 / tile, 0), (a1 / tile, 0), (a1 / tile, 0.12 / tile), (a0 / tile, 0.12 / tile),
                    -StyleVector.Up);
                if (!o.Door)
                {
                    // Appui de fenêtre en pierre.
                    StyleShapes.Box(shapes.Stone, at + along * o.Center + outward * 0.03 + StyleVector.Up * (o.Bottom - 0.04), along * (o.Width / 2 + 0.06),
                        StyleVector.Up * 0.04, outward * 0.08, t.Stone);
                }
                else
                {
                    StyleVector s0 = at + along * a0 + StyleVector.Up * o.Bottom, s1 = at + along * a1 + StyleVector.Up * o.Bottom;
                    shapes.Walls.Quad(s0, s1, s1 - outward * 0.12, s0 - outward * 0.12, (a0 / tile, 0), (a1 / tile, 0), (a1 / tile, 0.12 / tile),
                        (a0 / tile, 0.12 / tile), StyleVector.Up);
                }
            }
        }

        /// <summary>Colombages d'un mur crépi : poutres de bois en saillie, posées autour des ouvertures.</summary>
        private static void Timbers(StyleBuildingShapes shapes, StyleVector at, StyleVector along, StyleVector outward, double half, double bottom, double top,
            double floor, List<Opening> openings, bool gable, double ridge, Tiles t)
        {
            const double W = 0.09, Out = 0.035;
            StyleVector face = at + outward * Out;
            void Post(double a, double y0, double y1) =>
                StyleShapes.Box(shapes.Wood, face + along * a + StyleVector.Up * ((y0 + y1) / 2), along * W, StyleVector.Up * ((y1 - y0) / 2), outward * Out, t.Wood);
            void Rail(double a0, double a1, double y)
            {
                // Une sablière ne traverse pas une ouverture : elle s'arrête de part et d'autre.
                var spans = new List<(double, double)> { (a0, a1) };
                foreach (Opening o in openings.Where(o => o.Bottom < y + W && o.Top > y - W))
                    spans = spans.SelectMany(sp => new[] { (sp.Item1, Math.Min(sp.Item2, o.Center - o.Width / 2 - 0.02)), (Math.Max(sp.Item1, o.Center + o.Width / 2 + 0.02), sp.Item2) })
                        .Where(sp => sp.Item2 - sp.Item1 > 0.05).ToList();
                foreach ((double b0, double b1) in spans)
                    StyleShapes.Box(shapes.Wood, face + along * ((b0 + b1) / 2) + StyleVector.Up * y, along * ((b1 - b0) / 2), StyleVector.Up * W, outward * Out, t.Wood);
            }
            Post(-half + W, bottom, top);
            Post(half - W, bottom, top);
            Rail(-half, half, bottom + W);
            Rail(-half, half, top - W);
            if (floor < top - 0.5) Rail(-half, half, floor);
            // Poteaux entre les ouvertures : à mi-chemin de deux ouvertures voisines.
            var edges = openings.Select(o => o.Center).OrderBy(c => c).ToList();
            var posts = new List<double>();
            double last = -half;
            foreach (double c in edges)
            {
                double between = (last + c) / 2;
                if (c - last > 1.4 && openings.All(o => Math.Abs(o.Center - between) > o.Width / 2 + 0.12)) posts.Add(between);
                last = c;
            }
            if (half - last > 1.4 && openings.All(o => Math.Abs(o.Center - (last + half) / 2) > o.Width / 2 + 0.12)) posts.Add((last + half) / 2);
            foreach (double a in posts) Post(a, bottom, top);
            // Croix de Saint-André entre deux poteaux, à l'étage, là où rien ne la traverse.
            var all = new List<double> { -half + W }.Concat(posts).Concat(new[] { half - W }).OrderBy(a => a).ToList();
            for (int i = 0; i + 1 < all.Count && floor < top - 0.8; i++)
            {
                double a0 = all[i] + W, a1 = all[i + 1] - W;
                if (a1 - a0 < 0.8 || openings.Any(o => o.Center + o.Width / 2 > a0 && o.Center - o.Width / 2 < a1 && o.Top > floor)) continue;
                StyleShapes.Beam(shapes.Wood, face + along * a0 + StyleVector.Up * (floor + W), face + along * a1 + StyleVector.Up * (top - W), W * 1.6, Out * 2, t.Wood);
                StyleShapes.Beam(shapes.Wood, face + along * a1 + StyleVector.Up * (floor + W), face + along * a0 + StyleVector.Up * (top - W), W * 1.6, Out * 2, t.Wood);
            }
            if (gable)
            {
                // Poinçon du pignon.
                StyleShapes.Box(shapes.Wood, face + StyleVector.Up * ((top + ridge) / 2 - 0.1), along * W, StyleVector.Up * ((ridge - top) / 2 - 0.1), outward * Out, t.Wood);
            }
        }

        /// <summary>Toit à deux pans d'une halle : tuiles (ou ardoises, bardeaux) dessus, voliges dessous, tranches, faîtière.</summary>
        private static void HallRoof(StyleBuildingShapes shapes, Frame f, double wallTop, double pitch, double eaveOut, double gableOut, Tiles t)
        {
            const double Thick = 0.14;
            double roofScale = 1 / t.Roof, woodScale = 1 / t.Wood;
            double Under(double b) => wallTop + (f.D - Math.Abs(b)) * pitch + 0.02;
            double slope = (f.D + eaveOut) * Math.Sqrt(1 + pitch * pitch);
            double a0 = -f.L - gableOut, a1 = f.L + gableOut;
            foreach (int side in new[] { -1, 1 })
            {
                double eb = side * (f.D + eaveOut);
                StyleVector r0 = f.At(a0, Under(0) + Thick, 0), r1 = f.At(a1, Under(0) + Thick, 0);
                StyleVector e0 = f.At(a0, Under(eb) + Thick, eb), e1 = f.At(a1, Under(eb) + Thick, eb);
                StyleVector down = StyleVector.Up * -Thick;
                StyleVector normal = (StyleVector.Up + f.V * (side * pitch)).Normalized;
                shapes.Roof.Quad(e0, e1, r1, r0, (a0 * roofScale, 0), (a1 * roofScale, 0), (a1 * roofScale, slope * roofScale), (a0 * roofScale, slope * roofScale), normal);
                shapes.Wood.Quad(e0 + down, e1 + down, r1 + down, r0 + down, (a0 * woodScale, 0), (a1 * woodScale, 0), (a1 * woodScale, slope * woodScale),
                    (a0 * woodScale, slope * woodScale), -normal);
                shapes.Wood.Quad(e0 + down, e1 + down, e1, e0, (a0 * woodScale, 0), (a1 * woodScale, 0), (a1 * woodScale, Thick * woodScale), (a0 * woodScale, Thick * woodScale),
                    f.V * side);
                foreach ((StyleVector r, StyleVector e, StyleVector outward) in new[] { (r0, e0, -f.U), (r1, e1, f.U) })
                    shapes.Wood.Quad(e + down, r + down, r, e, (0, 0), (slope * woodScale, 0), (slope * woodScale, Thick * woodScale), (0, Thick * woodScale), outward);
            }
            StyleShapes.Log(shapes.Roof, f.At(a0 - 0.05, Under(0) + Thick + 0.03, 0), f.At(a1 + 0.05, Under(0) + Thick + 0.03, 0), 0.11, 10, t.Roof);
        }

        /// <summary>Flèche à quatre pans de base carrée (demi-côté <paramref name="half"/>) jusqu'à la hauteur <paramref name="apex"/>.</summary>
        private static void Spire(StyleMesh mesh, StyleVector baseCenter, StyleVector u, StyleVector v, double half, double apex, double tile)
        {
            StyleVector top = new StyleVector(baseCenter.X, apex, baseCenter.Z);
            StyleVector[] corners = { baseCenter + u * -half + v * -half, baseCenter + u * half + v * -half, baseCenter + u * half + v * half, baseCenter + u * -half + v * half };
            double slant = Math.Sqrt(half * half + (apex - baseCenter.Y) * (apex - baseCenter.Y)) / tile;
            for (int i = 0; i < 4; i++)
            {
                StyleVector a = corners[i], b = corners[(i + 1) % 4];
                StyleVector mid = (a + b) * 0.5;
                StyleVector outward = new StyleVector(mid.X - baseCenter.X, 0.6 * half, mid.Z - baseCenter.Z);
                mesh.Triangle(a, b, top, (-half / tile, 0), (half / tile, 0), (0, slant), outward);
            }
        }

        // --- Rue pavée et labours --------------------------------------------------------------------------------------------

        /// <summary>Hauteur (m) des pavés au-dessus du sol : juste assez pour ne pas se mêler au terrain.</summary>
        public const double PavementHeight = 0.025;

        /// <summary>
        /// Rue pavée : une bande de la largeur de la route le long de chaque tronçon, et un disque à chaque point (joints arrondis, bouts
        /// en demi-cercle, comme la carte peinte), à <see cref="PavementHeight"/> ; texture en coordonnées du monde (les recouvrements
        /// tombent sur les mêmes pavés).
        /// </summary>
        public static StyleMesh Pavement(StyleRoad road, double tile, int discSides = 24)
        {
            var mesh = new StyleMesh();
            if (road == null) return mesh;
            double s = 1 / Math.Max(0.1, tile), half = road.Width / 2, y = PavementHeight;
            (double, double) Uv(StyleVector p) => (p.X * s, p.Z * s);
            for (int i = 0; i + 1 < road.Points.Count; i++)
            {
                var a = road.Points[i];
                var b = road.Points[i + 1];
                double dx = b.X - a.X, dz = b.Z - a.Z, len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-9) continue;
                var n = new StyleVector(-dz / len * half, 0, dx / len * half);
                var pa = new StyleVector(a.X, y, a.Z);
                var pb = new StyleVector(b.X, y, b.Z);
                StyleVector p0 = pa - n, p1 = pb - n, p2 = pb + n, p3 = pa + n;
                mesh.Quad(p0, p1, p2, p3, Uv(p0), Uv(p1), Uv(p2), Uv(p3), StyleVector.Up);
            }
            int sides = Math.Max(6, discSides);
            foreach (var p in road.Points)
            {
                var c = new StyleVector(p.X, y, p.Z);
                for (int k = 0; k < sides; k++)
                {
                    double a0 = 2 * Math.PI * k / sides, a1 = 2 * Math.PI * (k + 1) / sides;
                    StyleVector q0 = c + new StyleVector(Math.Cos(a0) * half, 0, Math.Sin(a0) * half), q1 = c + new StyleVector(Math.Cos(a1) * half, 0, Math.Sin(a1) * half);
                    mesh.Triangle(c, q0, q1, Uv(c), Uv(q0), Uv(q1), StyleVector.Up);
                }
            }
            return mesh;
        }

        /// <summary>Écart (m) entre deux sillons d'un champ labouré, et hauteur d'un billon.</summary>
        public const double FurrowSpacing = 0.7, FurrowHeight = 0.16;

        /// <summary>
        /// Champ labouré : billons de terre le long des rangs (deux pentes par billon, du creux au sommet), sur tout le champ ; texture de
        /// terre en coordonnées du monde.
        /// </summary>
        public static StyleMesh Furrows(StyleField field, double tile)
        {
            var mesh = new StyleMesh();
            double s = 1 / Math.Max(0.1, tile);
            double across0 = field.RowsAlongX ? field.MinZ : field.MinX, across1 = field.RowsAlongX ? field.MaxZ : field.MaxX;
            double along0 = field.RowsAlongX ? field.MinX : field.MinZ, along1 = field.RowsAlongX ? field.MaxX : field.MaxZ;
            StyleVector P(double along, double across, double y) =>
                field.RowsAlongX ? new StyleVector(along, y, across) : new StyleVector(across, y, along);
            for (double c = across0; c + FurrowSpacing <= across1 + 1e-9; c += FurrowSpacing)
            {
                double mid = c + FurrowSpacing / 2, c1 = c + FurrowSpacing;
                foreach ((double from, double to) in new[] { (c, mid), (mid, c1) })
                {
                    StyleVector a = P(along0, from, from == c || from == c1 ? 0.01 : FurrowHeight), b = P(along1, from, a.Y);
                    StyleVector d = P(along0, to, to == mid ? FurrowHeight : 0.01), e = P(along1, to, d.Y);
                    StyleVector slope = field.RowsAlongX ? new StyleVector(0, 0.7, from < mid ? -1 : 1) : new StyleVector(from < mid ? -1 : 1, 0.7, 0);
                    mesh.Quad(a, b, e, d, (a.X * s, a.Z * s), (b.X * s, b.Z * s), (e.X * s, e.Z * s), (d.X * s, d.Z * s), slope);
                }
            }
            return mesh;
        }

        /// <summary>
        /// Plants d'un champ de blé (mûr ou vert) : en rangs espacés de <see cref="FurrowSpacing"/>, une touffe tous les
        /// <paramref name="step"/> mètres, décalées d'une quantité fixe ; rien pour un champ labouré.
        /// </summary>
        public static IReadOnlyList<(double X, double Z, double Yaw, double Scale)> CropSpots(StyleField field, double step)
        {
            var spots = new List<(double X, double Z, double Yaw, double Scale)>();
            if (field.Crop == 2 || !(step > 0.05)) return spots;
            uint seed = (uint)StyleRules.Variant(FormattableString.Invariant($"champ/{field.MinX:0.##}/{field.MinZ:0.##}"), int.MaxValue) | 1u;
            double Next()
            {
                seed ^= seed << 13;
                seed ^= seed >> 17;
                seed ^= seed << 5;
                return (seed >> 8) / 16777216.0;
            }
            double across0 = field.RowsAlongX ? field.MinZ : field.MinX, across1 = field.RowsAlongX ? field.MaxZ : field.MaxX;
            double along0 = field.RowsAlongX ? field.MinX : field.MinZ, along1 = field.RowsAlongX ? field.MaxX : field.MaxZ;
            for (double c = across0 + FurrowSpacing / 2; c < across1; c += FurrowSpacing)
                for (double a = along0 + step / 2; a < along1; a += step)
                {
                    double along = a + (Next() - 0.5) * step * 0.6, across = c + (Next() - 0.5) * 0.18;
                    double x = field.RowsAlongX ? along : across, z = field.RowsAlongX ? across : along;
                    spots.Add((x, z, Next() * 360, 0.85 + 0.3 * Next()));
                }
            return spots;
        }

        // --- Puits ---------------------------------------------------------------------------------------------------------

        private static void Well(StyleBuildingShapes shapes, IReadOnlyList<(double X, double Z)> footprint, Tiles t)
        {
            double cx = footprint.Average(p => p.X), cz = footprint.Average(p => p.Z);
            double r = footprint.Min(p => Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Z - cz) * (p.Z - cz))) * Math.Cos(Math.PI / 8) - 0.02;
            var c = new StyleVector(cx, 0, cz);
            const int Sides = 12;
            const double Height = 0.85, Inner = 0.62;
            double s = 1 / t.Stone;
            for (int i = 0; i < Sides; i++)
            {
                double a0 = 2 * Math.PI * i / Sides, a1 = 2 * Math.PI * (i + 1) / Sides, am = (a0 + a1) / 2;
                StyleVector d0 = new StyleVector(Math.Cos(a0), 0, Math.Sin(a0)), d1 = new StyleVector(Math.Cos(a1), 0, Math.Sin(a1)), dm = new StyleVector(Math.Cos(am), 0, Math.Sin(am));
                double arc0 = a0 * r, arc1 = a1 * r;
                // Paroi extérieure, paroi intérieure, dessus de la margelle.
                shapes.Stone.Quad(c + d0 * r, c + d1 * r, c + d1 * r + StyleVector.Up * Height, c + d0 * r + StyleVector.Up * Height,
                    (arc0 * s, 0), (arc1 * s, 0), (arc1 * s, Height * s), (arc0 * s, Height * s), dm);
                shapes.Stone.Quad(c + d0 * (r * Inner) + StyleVector.Up * 0.25, c + d1 * (r * Inner) + StyleVector.Up * 0.25, c + d1 * (r * Inner) + StyleVector.Up * Height,
                    c + d0 * (r * Inner) + StyleVector.Up * Height, (arc0 * s, 0), (arc1 * s, 0), (arc1 * s, Height * s), (arc0 * s, Height * s), -dm);
                shapes.Stone.Quad(c + d0 * (r * Inner) + StyleVector.Up * Height, c + d1 * (r * Inner) + StyleVector.Up * Height, c + d1 * r + StyleVector.Up * Height,
                    c + d0 * r + StyleVector.Up * Height, (d0.X * r * Inner * s, d0.Z * r * Inner * s), (d1.X * r * Inner * s, d1.Z * r * Inner * s),
                    (d1.X * r * s, d1.Z * r * s), (d0.X * r * s, d0.Z * r * s), StyleVector.Up);
                // Eau sombre au fond.
                shapes.Dark.Triangle(c + StyleVector.Up * 0.25, c + d0 * (r * Inner) + StyleVector.Up * 0.25, c + d1 * (r * Inner) + StyleVector.Up * 0.25,
                    (0.5, 0.5), (0.5 + d0.X * 0.5, 0.5 + d0.Z * 0.5), (0.5 + d1.X * 0.5, 0.5 + d1.Z * 0.5), StyleVector.Up);
            }
            // Deux poteaux, la traverse et son treuil, un petit toit de bardeaux.
            var x = new StyleVector(1, 0, 0);
            double post = r * 0.82;
            foreach (int side in new[] { -1, 1 })
                StyleShapes.Beam(shapes.Wood, c + x * (side * post) + StyleVector.Up * 0.6, c + x * (side * post) + StyleVector.Up * 2.35, 0.14, 0.14, t.Wood);
            StyleShapes.Log(shapes.Wood, c + x * -post + StyleVector.Up * 1.55, c + x * post + StyleVector.Up * 1.55, 0.08, 8, t.Wood);
            StyleShapes.Box(shapes.Wood, c + StyleVector.Up * 1.25, x * 0.14, StyleVector.Up * 0.14, new StyleVector(0, 0, 0.14), t.Wood);   // seau
            StyleShapes.Beam(shapes.Dark, c + StyleVector.Up * 1.39, c + StyleVector.Up * 1.53, 0.02, 0.02, 1);                               // corde
            var z = new StyleVector(0, 0, 1);
            foreach (int side in new[] { -1, 1 })
            {
                StyleVector e0 = c + x * (-post - 0.3) + z * (side * 0.95) + StyleVector.Up * 2.2, e1 = c + x * (post + 0.3) + z * (side * 0.95) + StyleVector.Up * 2.2;
                StyleVector r0 = c + x * (-post - 0.3) + StyleVector.Up * 2.75, r1 = c + x * (post + 0.3) + StyleVector.Up * 2.75;
                double len = (2 * post + 0.6) / t.Roof, slope = Math.Sqrt(0.95 * 0.95 + 0.55 * 0.55) / t.Roof;
                StyleVector normal = new StyleVector(0, 0.95, 0.55 * side);
                shapes.Roof.Quad(e0, e1, r1, r0, (0, 0), (len, 0), (len, slope), (0, slope), normal);
                shapes.Wood.Quad(e0 + StyleVector.Up * -0.05, e1 + StyleVector.Up * -0.05, r1 + StyleVector.Up * -0.05, r0 + StyleVector.Up * -0.05, (0, 0), (len, 0), (len, slope),
                    (0, slope), -normal);
            }
        }

        // --- Étal du marché ------------------------------------------------------------------------------------------------

        private static void Stall(StyleBuildingShapes shapes, IReadOnlyList<(double X, double Z)> footprint, string id, (int X, int Z) facing, Tiles t)
        {
            var f = new Frame(footprint);
            shapes.ClothColor = StyleRules.Variant(id, 2);
            (int sideA, int sideB) = f.Side(facing);
            int front = sideB != 0 ? sideB : 1;   // l'étal ouvre sur son grand côté le plus proche de la route
            double L = f.L - 0.08, D = f.D - 0.08;
            // Comptoir sur l'avant, planches.
            StyleShapes.Box(shapes.Wood, f.At(0, 0.48, front * (D - 0.35)), f.U * L, StyleVector.Up * 0.48, f.V * 0.35, t.Wood);
            StyleShapes.Box(shapes.Wood, f.At(0, 0.99, front * (D - 0.35)), f.U * (L + 0.05), StyleVector.Up * 0.03, f.V * 0.4, t.Wood);
            // Marchandises : caisses, sacs.
            uint seed = (uint)StyleRules.Variant(id + "/marchandises", int.MaxValue) | 1u;
            double Next()
            {
                seed ^= seed << 13;
                seed ^= seed >> 17;
                seed ^= seed << 5;
                return (seed >> 8) / 16777216.0;
            }
            for (double a = -L + 0.35; a < L - 0.3; a += 0.55 + 0.2 * Next())
            {
                double h = 0.12 + 0.12 * Next();
                StyleMesh mesh = Next() < 0.5 ? shapes.Wood : shapes.Cloth;
                StyleShapes.Box(mesh, f.At(a, 1.02 + h, front * (D - 0.35)), f.U * 0.2, StyleVector.Up * h, f.V * 0.2, mesh == shapes.Wood ? t.Wood : 1);
            }
            // Poteaux et auvent rayé, plus haut devant.
            foreach (int sa in new[] { -1, 1 })
                foreach (int sb in new[] { -1, 1 })
                    StyleShapes.Beam(shapes.Wood, f.At(sa * L, 0, sb * D), f.At(sa * L, sb == front ? 2.35 : 2.05, sb * D), 0.1, 0.1, t.Wood);
            const double Stripe = 0.38;
            int stripes = Math.Max(1, (int)Math.Round((2 * L + 0.4) / Stripe));
            double width = (2 * L + 0.4) / stripes;
            for (int i = 0; i < stripes; i++)
            {
                double a0 = -L - 0.2 + i * width, a1 = a0 + width;
                StyleMesh mesh = i % 2 == 0 ? shapes.Cloth : shapes.ClothStripes;
                StyleVector p0 = f.At(a0, 2.38, front * (D + 0.35)), p1 = f.At(a1, 2.38, front * (D + 0.35));
                StyleVector p2 = f.At(a1, 2.08, -front * D), p3 = f.At(a0, 2.08, -front * D);
                StyleVector normal = (StyleVector.Up + f.V * (front * 0.2)).Normalized;
                mesh.Quad(p0, p1, p2, p3, (0, 0), (1, 0), (1, 1), (0, 1), normal);
                mesh.Quad(p0 + StyleVector.Up * -0.01, p1 + StyleVector.Up * -0.01, p2 + StyleVector.Up * -0.01, p3 + StyleVector.Up * -0.01, (0, 0), (1, 0), (1, 1), (0, 1), -normal);
                // Lambrequin : la bande qui pend devant.
                mesh.Quad(p0, p1, p1 + StyleVector.Up * -0.22, p0 + StyleVector.Up * -0.22, (0, 0), (1, 0), (1, 1), (0, 1), f.V * front);
                mesh.Quad(p0, p1, p1 + StyleVector.Up * -0.22, p0 + StyleVector.Up * -0.22, (0, 0), (1, 0), (1, 1), (0, 1), f.V * -front);
            }
        }

        // --- Tour de guet --------------------------------------------------------------------------------------------------

        private static void Tower(StyleBuildingShapes shapes, IReadOnlyList<(double X, double Z)> footprint, (int X, int Z) facing, Tiles t)
        {
            shapes.WallSurface = StyleSurface.Stone;
            double cx = footprint.Average(p => p.X), cz = footprint.Average(p => p.Z);
            double r = footprint.Min(p => Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Z - cz) * (p.Z - cz))) * Math.Cos(Math.PI / 8) - 0.02;
            var c = new StyleVector(cx, 0, cz);
            double top = TownRules.WallHeight(StyleBuilding.Tower);
            const int Sides = 16;
            const double Taper = 0.95;
            double s = 1 / t.Stone;
            var door = new StyleVector(facing.X, 0, facing.Z == 0 && facing.X == 0 ? -1 : facing.Z);
            for (int i = 0; i < Sides; i++)
            {
                double a0 = 2 * Math.PI * i / Sides, a1 = 2 * Math.PI * (i + 1) / Sides, am = (a0 + a1) / 2;
                StyleVector d0 = new StyleVector(Math.Cos(a0), 0, Math.Sin(a0)), d1 = new StyleVector(Math.Cos(a1), 0, Math.Sin(a1)), dm = new StyleVector(Math.Cos(am), 0, Math.Sin(am));
                double arc0 = a0 * r, arc1 = a1 * r;
                // Fruit : la tour se resserre un peu en montant (le pied sur l'emprise).
                shapes.Walls.Quad(c + d0 * r, c + d1 * r, c + d1 * (r * Taper) + StyleVector.Up * top, c + d0 * (r * Taper) + StyleVector.Up * top,
                    (arc0 * s, 0), (arc1 * s, 0), (arc1 * s, top * s), (arc0 * s, top * s), dm);
                // Créneaux : un merlon sur deux, en saillie sur un cordon.
                double rt = r * Taper;
                StyleShapes.Box(shapes.Walls, c + dm * (rt + 0.05) + StyleVector.Up * (top + 0.12), new StyleVector(-dm.Z, 0, dm.X) * (rt * Math.PI / Sides + 0.02), StyleVector.Up * 0.12,
                    dm * 0.2, t.Stone);
                if (i % 2 == 0)
                    StyleShapes.Box(shapes.Walls, c + dm * (rt - 0.12) + StyleVector.Up * (top + 0.24 + 0.5), new StyleVector(-dm.Z, 0, dm.X) * (rt * Math.PI / Sides - 0.02),
                        StyleVector.Up * 0.5, dm * 0.3, t.Stone);
                else
                    StyleShapes.Box(shapes.Walls, c + dm * (rt - 0.12) + StyleVector.Up * (top + 0.24 + 0.15), new StyleVector(-dm.Z, 0, dm.X) * (rt * Math.PI / Sides - 0.02),
                        StyleVector.Up * 0.15, dm * 0.3, t.Stone);
                // Archères, à deux hauteurs, une face sur quatre.
                if (i % 4 == 1)
                    foreach (double y in new[] { 4.2, 7.8 })
                        WallQuad(shapes.Dark, c + dm * (r * (1 - (1 - Taper) * (y + 0.5) / top) * Math.Cos(Math.PI / Sides) + 0.02), new StyleVector(-dm.Z, 0, dm.X), dm,
                            -0.07, 0.07, y, y + 1.0, 1);
            }
            // Plate-forme de planches au sommet, trappe, et porte de planches au pied, côté chemin.
            double ws = 1 / t.Wood;
            var top0 = new List<StyleVector>();
            for (int i = 0; i < Sides; i++)
            {
                double a = 2 * Math.PI * i / Sides;
                top0.Add(c + new StyleVector(Math.Cos(a), 0, Math.Sin(a)) * (r * Taper - 0.3) + StyleVector.Up * (top + 0.24));
            }
            StyleVector hub = c + StyleVector.Up * (top + 0.24);
            for (int i = 0; i < Sides; i++)
            {
                StyleVector a = top0[i], b = top0[(i + 1) % Sides];
                shapes.Wood.Triangle(hub, a, b, (hub.X * ws, hub.Z * ws), (a.X * ws, a.Z * ws), (b.X * ws, b.Z * ws), StyleVector.Up);
            }
            StyleShapes.Box(shapes.Dark, c + new StyleVector(r * 0.3, top + 0.245, -r * 0.25), new StyleVector(0.45, 0, 0), StyleVector.Up * 0.005, new StyleVector(0, 0, 0.45), 1);
            StyleVector across = new StyleVector(-door.Z, 0, door.X);
            StyleShapes.Box(shapes.Wood, c + door * (r - 0.02) + StyleVector.Up * 1.1, across * 0.55, StyleVector.Up * 1.1, door * 0.05, t.Wood);
            StyleShapes.Box(shapes.Stone, c + door * (r - 0.02) + StyleVector.Up * 2.32, across * 0.7, StyleVector.Up * 0.12, door * 0.08, t.Stone);
        }

        // --- Tente du camp -------------------------------------------------------------------------------------------------

        private static void Tent(StyleBuildingShapes shapes, IReadOnlyList<(double X, double Z)> footprint, string id, (int X, int Z) facing, Tiles t)
        {
            shapes.ClothColor = StyleRules.Variant(id, 3) == 0 ? 3 : 2;
            double cx = footprint.Average(p => p.X), cz = footprint.Average(p => p.Z);
            double r = footprint.Min(p => Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Z - cz) * (p.Z - cz))) * Math.Cos(Math.PI / 8) - 0.03;
            var c = new StyleVector(cx, 0, cz);
            const int Sides = 10;
            const double Skirt = 0.55, Apex = 3.0;
            var door = new StyleVector(facing.X, 0, facing.X == 0 && facing.Z == 0 ? -1 : facing.Z);
            StyleVector apex = c + StyleVector.Up * Apex;
            for (int i = 0; i < Sides; i++)
            {
                double a0 = 2 * Math.PI * i / Sides, a1 = 2 * Math.PI * (i + 1) / Sides, am = (a0 + a1) / 2;
                StyleVector d0 = new StyleVector(Math.Cos(a0), 0, Math.Sin(a0)), d1 = new StyleVector(Math.Cos(a1), 0, Math.Sin(a1)), dm = new StyleVector(Math.Cos(am), 0, Math.Sin(am));
                bool entrance = StyleVector.Dot(dm, door) > Math.Cos(Math.PI / Sides) - 1e-9;
                StyleVector b0 = c + d0 * r, b1 = c + d1 * r, s0 = b0 + StyleVector.Up * Skirt, s1 = b1 + StyleVector.Up * Skirt;
                // Paroi basse, toit conique (pans un peu creusés : sommet du pan vers l'intérieur), coutures sombres.
                if (entrance)
                {
                    shapes.Dark.Quad(b0, b1, s1, s0, (0, 0), (1, 0), (1, 1), (0, 1), dm);
                    shapes.Dark.Triangle(s0, s1, c + (d0 + d1) * (r * 0.25) + StyleVector.Up * (Apex - 1.2), (0, 0), (1, 0), (0.5, 1), dm);
                    shapes.Cloth.Triangle(s0, c + (d0 + d1) * (r * 0.25) + StyleVector.Up * (Apex - 1.2), apex, (0, 0), (0.5, 0.6), (0.5, 1), dm + StyleVector.Up * 0.8);
                    shapes.Cloth.Triangle(s1, apex, c + (d0 + d1) * (r * 0.25) + StyleVector.Up * (Apex - 1.2), (1, 0), (0.5, 1), (0.5, 0.6), dm + StyleVector.Up * 0.8);
                    continue;
                }
                shapes.Cloth.Quad(b0, b1, s1, s0, (0, 0), (1, 0), (1, Skirt), (0, Skirt), dm);
                shapes.Cloth.Quad(b0 + dm * -0.02, b1 + dm * -0.02, s1 + dm * -0.02, s0 + dm * -0.02, (0, 0), (1, 0), (1, Skirt), (0, Skirt), -dm);
                shapes.Cloth.Triangle(s0, s1, apex, (0, 0), (1, 0), (0.5, 1), dm + StyleVector.Up * 0.8);
                shapes.Cloth.Triangle(s0 + StyleVector.Up * -0.03, s1 + StyleVector.Up * -0.03, apex + StyleVector.Up * -0.03, (0, 0), (1, 0), (0.5, 1), -(dm + StyleVector.Up * 0.8));
                StyleShapes.Beam(shapes.Wood, s0 + d0 * 0.01, apex, 0.03, 0.03, t.Wood);
                // Hauban et piquet.
                StyleVector peg = c + d0 * (r - 0.05);
                StyleShapes.Beam(shapes.Dark, s0, peg + StyleVector.Up * 0.05, 0.015, 0.015, 1);
                StyleShapes.Box(shapes.Wood, peg + StyleVector.Up * 0.08, new StyleVector(0.03, 0, 0), StyleVector.Up * 0.08, new StyleVector(0, 0, 0.03), t.Wood);
            }
            // Mât qui dépasse, fanion.
            StyleShapes.Log(shapes.Wood, c, apex + StyleVector.Up * 0.45, 0.05, 6, t.Wood);
            StyleShapes.Box(shapes.Cloth, apex + StyleVector.Up * 0.32 + new StyleVector(0.18, 0, 0), new StyleVector(0.17, 0, 0), StyleVector.Up * 0.08, new StyleVector(0, 0, 0.005), 1);
        }
    }
}
