using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EdS.Runtime
{
    /// <summary>
    /// Kit modulaire du Bourg d'Aube : une maison du kit (export de jeu « bourg_kit_&lt;nom&gt;.fbx » et sa version lointaine
    /// « …_lod1.fbx »). Boîte dans le repère de Blender du FBX de jeu, comme <see cref="BourgModels.Box"/> : X (droite vue de dehors), Y (vers
    /// l'arrière ; la façade du rez-de-chaussée est en Y = −profondeur / 2), hauteur ; pivot au sol, au centre de l'emprise des murs.
    /// </summary>
    public sealed class KitHouse
    {
        public KitHouse(string piece, double x0, double x1, double y0, double y1, double top, bool slate, int triangles, int farTriangles,
            (double X, double Y) door, (double X, double Y, double Z)[] windows)
        {
            Piece = piece;
            Box = (x0, x1, y0, y1, top);
            Slate = slate;
            Triangles = triangles;
            FarTriangles = farTriangles;
            Door = door;
            Windows = windows;
        }

        /// <summary>Pièce (fin du nom du modèle, « kit_m01_etroite_pignon »).</summary>
        public string Piece { get; }
        public (double X0, double X1, double Y0, double Y1, double Top) Box { get; }

        /// <summary>Toit d'ardoise : maison de notable (décision de Damien du 25 novembre 2026).</summary>
        public bool Slate { get; }

        /// <summary>Triangles de la maison détaillée (LOD0) et de sa version lointaine (LOD1), comptés à l'export.</summary>
        public int Triangles { get; }
        public int FarTriangles { get; }

        /// <summary>Milieu du seuil de la porte d'entrée (repère de Blender, comme <see cref="BourgModels.FrontDoor"/>).</summary>
        public (double X, double Y) Door { get; }

        /// <summary>Fenêtres de façade du rez-de-chaussée et du premier étage (repère d'Unity du modèle : x, hauteur du centre, z).</summary>
        public (double X, double Y, double Z)[] Windows { get; }

        public double Width => Box.X1 - Box.X0;
        public double Depth => Box.Y1 - Box.Y0;
    }

    /// <summary>
    /// Kit modulaire du Bourg d'Aube (atelier <c>Bourg_Kit</c>, style validé par Damien le 25 novembre 2026) : douze maisons faites de 43 pièces,
    /// un seul matériau à texture palette de 32 × 32 px (« bourg_kit_couleur », filtrage point), grille de 1 m, étages de 3 m. Il remplace les
    /// maisons ordinaires du Bourg (<c>maison_ville_NN</c>) ; les bâtiments nommés gardent leur modèle. Chaque maison a une version
    /// lointaine générée depuis la même recette (LOD1, 66 à 282 triangles) et la vue lui donne un LODGroup. Affichage seulement ; sans Unity.
    /// </summary>
    public static class BourgKit
    {
        /// <summary>Dossier des exports de jeu du kit (un groupe du décor de l'atelier : « bourg_kit_&lt;pièce&gt;.fbx »).</summary>
        public const string Folder = "Assets/EdS/Modeles/Decor/bourg_kit";

        /// <summary>Texture palette partagée par tout le kit (32 × 32 px, une case de 4 px par teinte).</summary>
        public const string Palette = "bourg_kit_couleur";

        /// <summary>Suffixe de la version lointaine d'une maison.</summary>
        public const string FarSuffix = "_lod1";

        /// <summary>Une maison de notable (toit d'ardoise) sur <see cref="NotableEvery"/> emplacements où une maison d'ardoise tient (≈ une sur six en tout).</summary>
        public const int NotableEvery = 5;

        /// <summary>
        /// Débord latéral admis (m) au-delà de l'emprise : rives et égouts des toits (0,25 à 0,63 m) ; les maisons numérotées sont à plus de
        /// 30 m les unes des autres, ce débord ne touche rien (vérifié contre obstacles, routes et portes).
        /// </summary>
        public const double SideTolerance = 0.65;

        /// <summary>Les douze maisons du kit (atelier, <c>jeu/export_jeu.json</c>).</summary>
        public static readonly IReadOnlyList<KitHouse> Houses = new[]
        {
            new KitHouse("kit_m01_etroite_pignon", -2.58, 2.58, -5.32, 4.25, 12.1, false, 6888, 226, (-1, -4), new (double, double, double)[] { (-1, 1.6, 4.04), (1, 4.6, 4.54), (-1, 4.6, 4.54) }),
            new KitHouse("kit_m02_artisan", -3.25, 3.32, -3.7, 3.58, 10.1, false, 5136, 154, (0, -3), new (double, double, double)[] { (2, 1.6, 3.04), (-2, 1.6, 3.04), (2, 4.6, 3.04), (-2, 4.6, 3.04) }),
            new KitHouse("kit_m03_boulangerie", -4.25, 4.25, -4.3, 3.58, 10.1, false, 6352, 186, (1, -3), new (double, double, double)[] { (-3, 1.6, 3.04), (3, 4.6, 3.04), (-1, 4.6, 3.04), (-3, 4.6, 3.04) }),
            new KitHouse("kit_m04_drapier", -3.58, 3.58, -5.53, 4.25, 13.1, false, 8510, 268, (0, -4), new (double, double, double)[] { (2, 4.6, 4.54), (0, 4.6, 4.54), (-2, 4.6, 4.54) }),
            new KitHouse("kit_m05_notable", -5.63, 5.63, -5.02, 4.63, 11.1, true, 9410, 236, (0, -4), new (double, double, double)[] { (4, 1.6, 4.04), (2, 1.6, 4.04), (-2, 1.6, 4.04), (-4, 1.6, 4.04) }),
            new KitHouse("kit_m06_angle", -4.63, 5.53, -5.53, 4.63, 10.16, false, 7094, 176, (-1, -4), new (double, double, double)[] { (-3, 1.6, 4.04), (3, 4.6, 4.04), (1, 4.6, 4.04), (-3, 4.6, 4.04) }),
            new KitHouse("kit_m07_taverne", -5.25, 5.32, -5.3, 4.7, 11.1, false, 9402, 282, (0, -4), new (double, double, double)[] { (4, 1.6, 4.04), (2, 1.6, 4.04), (-2, 1.6, 4.04), (-4, 1.6, 4.04) }),
            new KitHouse("kit_m08_escalier", -3.25, 3.25, -4.62, 3.58, 10.1, false, 5361, 154, (2, -3), new (double, double, double)[] { (2, 1.6, 3.04), (2, 4.6, 3.04) }),
            new KitHouse("kit_m09_basse", -3.25, 3.25, -2.9, 2.58, 6.1, false, 3252, 66, (0, -2), new (double, double, double)[] { (2, 1.6, 2.04), (-2, 1.6, 2.04) }),
            new KitHouse("kit_m10_haute_croupe", -3.63, 3.63, -3.7, 3.63, 12.16, false, 6438, 222, (0, -3), new (double, double, double)[] { (2, 1.6, 3.04), (-2, 1.6, 3.04), (2, 4.6, 3.04), (-2, 4.6, 3.04) }),
            new KitHouse("kit_m11_galerie", -4.25, 4.25, -4.3, 3.58, 10.1, false, 6622, 230, (-1, -3), new (double, double, double)[] { (3, 1.6, 3.04), (-1, 1.6, 3.04), (3, 4.6, 3.04), (-1, 4.6, 3.04) }),
            new KitHouse("kit_m12_lucarnes", -4.25, 4.25, -3.7, 3.58, 7.1, true, 4918, 112, (-1, -3), new (double, double, double)[] { (3, 1.6, 3.04), (-1, 1.6, 3.04), (-3, 1.6, 3.04) }),
        };

        /// <summary>La maison du kit nommée <paramref name="piece"/> (« kit_m05_notable »), ou null.</summary>
        public static KitHouse? Find(string? piece) => piece == null ? null : Houses.FirstOrDefault(h => h.Piece == piece);

        /// <summary>Pièce de la version lointaine d'une maison du kit (« kit_m05_notable_lod1 »).</summary>
        public static string Far(string piece) => piece + FarSuffix;

        /// <summary>Clé de tirage d'un emplacement : le centre de son emprise (au millimètre), pas son identifiant.</summary>
        public static string Key((double X0, double X1, double Z0, double Z1) b) =>
            "bourg/kit/" + Math.Round((b.X0 + b.X1) / 2, 3).ToString("0.###", CultureInfo.InvariantCulture) + ","
            + Math.Round((b.Z0 + b.Z1) / 2, 3).ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>
        /// Maison du kit d'une emprise dont la façade est tournée vers <paramref name="facing"/> : celles qui tiennent (boîte entière, débords
        /// compris, à <see cref="BourgModels.HouseTolerance"/> près en profondeur, <see cref="SideTolerance"/> de chaque côté), notables (ardoise) sur un emplacement sur <see cref="NotableEvery"/> où
        /// l'une tient, les autres en tuile ; parmi les plus grandes (au moins <see cref="BourgModels.HouseChoice"/> de la surface de la plus
        /// grande du groupe), une tirée du centre de l'emprise. Null si aucune ne tient.
        /// </summary>
        public static KitHouse? Choose((double X0, double X1, double Z0, double Z1) b, (int X, int Z) facing)
        {
            double depth = facing.X != 0 ? b.X1 - b.X0 : b.Z1 - b.Z0, width = facing.X != 0 ? b.Z1 - b.Z0 : b.X1 - b.X0;
            var fits = Houses.Where(h => h.Width <= width + 2 * SideTolerance && h.Depth <= depth + BourgModels.HouseTolerance).ToList();
            if (fits.Count == 0) return null;
            string key = Key(b);
            bool notable = StyleRules.Variant(key + "/ardoise", NotableEvery) == 0;
            var group = fits.Where(h => h.Slate == notable).ToList();
            if (group.Count == 0) group = fits;
            double best = group.Max(h => h.Width * h.Depth);
            var pool = group.Where(h => h.Width * h.Depth >= BourgModels.HouseChoice * best).OrderBy(h => h.Piece, StringComparer.Ordinal).ToList();
            return pool[StyleRules.Variant(key, pool.Count)];
        }

        /// <summary>
        /// Décalage du pivot (m, à droite vue de dehors et vers la rue) depuis le centre de l'emprise : la boîte centrée en largeur, son avant
        /// (auvents, encorbellements compris) au bord de l'emprise côté rue (la maison donne sur la rue, sa cour derrière).
        /// </summary>
        public static (double Right, double Ahead) Offset(KitHouse house, (double X0, double X1, double Z0, double Z1) b, (int X, int Z) facing)
        {
            double depth = facing.X != 0 ? b.X1 - b.X0 : b.Z1 - b.Z0;
            return (-(house.Box.X0 + house.Box.X1) / 2, depth / 2 + house.Box.Y0);
        }

        /// <summary>Distances (m) de passage à la version lointaine et d'effacement, selon la machine.</summary>
        public static (double Far, double Cull) LodDistances(DeviceTier tier) => tier switch
        {
            DeviceTier.Phone => (30, 400),
            DeviceTier.Modest => (45, 550),
            _ => (70, 800),
        };

        /// <summary>Les deux seuils d'Unity (détaillée → lointaine, lointaine → rien), strictement décroissants.</summary>
        public static (double Far, double Cull) LodHeights(double size, DeviceTier tier, double fieldOfView)
        {
            var d = LodDistances(tier);
            double a = AtelierTrees.ScreenHeight(size, d.Far, fieldOfView), c = AtelierTrees.ScreenHeight(size, d.Cull, fieldOfView);
            return (a, Math.Max(0.0005, Math.Min(c, a * 0.9)));
        }

        /// <summary>Triangles des maisons du kit posées dans <paramref name="pieces"/>, de près (LOD0) ou de loin (LOD1).</summary>
        public static int Triangles(IEnumerable<BourgPiece> pieces, bool far) =>
            pieces.Select(p => Find(p.Piece)).Where(h => h != null).Sum(h => far ? h!.FarTriangles : h!.Triangles);
    }
}
