using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Space;

namespace EdS.Runtime
{
    /// <summary>V2.25.2 : un petit décor de l'atelier posé par le jeu (caillou, touffe, accessoire de la carrière) : pièce, place, lacet, échelle.</summary>
    public readonly struct DecorSpot
    {
        public DecorSpot(string piece, double x, double z, double yaw, double scale)
        {
            Piece = piece;
            X = x;
            Z = z;
            Yaw = yaw;
            Scale = scale;
        }

        /// <summary>Fin du nom du modèle (« caillou_p », « touffe », « chevre »…), voir <see cref="AtelierDecor.IsPiece"/>.</summary>
        public string Piece { get; }
        public double X { get; }
        public double Z { get; }

        /// <summary>Degrés, comme <c>Quaternion.Euler(0, yaw, 0)</c> d'Unity (le +z du modèle va vers (sin, cos)).</summary>
        public double Yaw { get; }
        public double Scale { get; }
    }

    /// <summary>
    /// V2.25.2 : décor de l'atelier qui n'est pas un bâtiment du bourg : les pierres du Seuil (faces gravées vers le centre de leur triangle,
    /// fissures qui luisent la nuit, cercle de cailloux, touffes d'herbe pâle), la barrière du moulin (vantaux qui s'ouvrent quand la quête
    /// ouvre le passage) et les accessoires de la carrière. Affichage seulement : le moteur et les données de zone ne changent pas. Sans Unity.
    /// </summary>
    public static class AtelierDecor
    {
        // --- Petits décors rangés à part (« …/Modeles/Decor/<groupe>/<groupe>_<pièce>.fbx ») ----------------------------------------

        /// <summary>
        /// Groupe d'un petit décor de l'atelier rangé dans « …/Modeles/Decor/&lt;groupe&gt;/&lt;fichier&gt;.fbx » (casse et « \ » indifférents),
        /// en minuscules ; null ailleurs. Ses textures sont « &lt;groupe&gt;_couleur » et « &lt;groupe&gt;_normale », un atlas pour le groupe.
        /// </summary>
        public static string? OwnDecorOf(string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string[] parts = path!.Replace('\\', '/').Split('/');
            int n = parts.Length;
            if (n < 4) return null;
            string file = parts[n - 1], group = parts[n - 2];
            if (!string.Equals(parts[n - 3], "Decor", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(parts[n - 4], "Modeles", StringComparison.OrdinalIgnoreCase)) return null;
            if (group.Length == 0 || file.Length <= 4 || !file.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) return null;
            return group.ToLowerInvariant();
        }

        /// <summary>Le modèle <paramref name="model"/> (« pierres_seuil_accessoires_caillou_p ») est-il la pièce <paramref name="piece"/> (« caillou_p ») ?</summary>
        public static bool IsPiece(string? model, string piece) =>
            model != null && model.EndsWith("_" + piece, StringComparison.OrdinalIgnoreCase);

        /// <summary>Matière sans élimination des faces arrière (les brins de la touffe sont en double face, rapport de l'atelier).</summary>
        public static bool DoubleSided(string? material) => StyleRules.Key(material ?? string.Empty) == "pierresseuilaccessoires";

        // --- Pierres du Seuil -------------------------------------------------------------------------------------------------------

        /// <summary>Obstacle d'une pierre du Seuil (« pierre_seuil1 » à « pierre_seuil3 »).</summary>
        public static bool IsSeuilStone(string? id) => StyleRules.Key(id ?? string.Empty).StartsWith("pierreseuil", StringComparison.Ordinal);

        private static (double X, double Z) Center(SpaceObstacle o) =>
            ((o.Points.Min(p => p.X.Raw) + o.Points.Max(p => p.X.Raw)) / 20000.0, (o.Points.Min(p => p.Z.Raw) + o.Points.Max(p => p.Z.Raw)) / 20000.0);

        /// <summary>Centre du triangle des pierres du Seuil (moyenne des centres de leurs emprises) ; null sans pierre.</summary>
        public static (double X, double Z)? SeuilCenter(IEnumerable<SpaceObstacle> obstacles)
        {
            var stones = obstacles.Where(o => IsSeuilStone(o.Id)).Select(Center).ToList();
            if (stones.Count == 0) return null;
            return (stones.Average(s => s.X), stones.Average(s => s.Z));
        }

        /// <summary>
        /// Lacet (degrés, comme <c>Quaternion.Euler(0, lacet, 0)</c>) qui tourne la face gravée d'un menhir (Blender −Y, +z du modèle dans
        /// Unity) depuis (<paramref name="x"/>, <paramref name="z"/>) vers <paramref name="center"/>.
        /// </summary>
        public static double SeuilStoneYaw(double x, double z, (double X, double Z) center) =>
            Math.Atan2(center.X - x, center.Z - z) * 180 / Math.PI;

        /// <summary>Lueur des fissures et des éclats (multiplie leur texture d'émission) : discrète le jour, nette la nuit.</summary>
        public const double SeuilGlowDay = 0.25, SeuilGlowNight = 1.6;

        /// <summary>
        /// Lueur des pierres d'après la part d'allumage des lanternes du monde (<paramref name="lamps"/>, 0 le jour, 1 la nuit, comme
        /// <see cref="TownModels.LanternLevel"/>) : elle suit l'heure et la lumière du monde.
        /// </summary>
        public static double SeuilGlow(double lamps) =>
            SeuilGlowDay + (SeuilGlowNight - SeuilGlowDay) * Math.Max(0.0, Math.Min(1.0, double.IsNaN(lamps) ? 0.0 : lamps));

        /// <summary>Cercle de cailloux : rayon (m) autour du centre du triangle, nombre de places (pas de 0,61 m), écart gardé.</summary>
        public const double PebbleRadius = 6.5, PebbleClearance = 0.3;

        public const int PebbleCount = 67;

        private static readonly string[] Pebbles = { "caillou_p", "caillou_m", "caillou_g" };

        /// <summary>
        /// Cercle de cailloux autour de <paramref name="center"/> : <see cref="PebbleCount"/> places à pas égal sur le rayon
        /// <see cref="PebbleRadius"/>, chacune d'une des trois tailles et d'un lacet tirés de son numéro (toujours les mêmes) ; une place à
        /// moins de <see cref="PebbleClearance"/> d'un obstacle (sa boîte) ou du bord d'une route n'a pas de caillou.
        /// </summary>
        public static IReadOnlyList<DecorSpot> SeuilPebbles((double X, double Z) center, IReadOnlyList<SpaceObstacle> obstacles, IReadOnlyList<StyleRoad> roads)
        {
            var boxes = obstacles.Select(o => (o.Points.Min(p => p.X.Raw) / 10000.0, o.Points.Max(p => p.X.Raw) / 10000.0,
                o.Points.Min(p => p.Z.Raw) / 10000.0, o.Points.Max(p => p.Z.Raw) / 10000.0)).ToList();
            var spots = new List<DecorSpot>(PebbleCount);
            for (int i = 0; i < PebbleCount; i++)
            {
                double a = 2 * Math.PI * i / PebbleCount;
                double x = center.X + PebbleRadius * Math.Sin(a), z = center.Z + PebbleRadius * Math.Cos(a);
                if (Free(x, z, PebbleClearance, boxes, roads))
                    spots.Add(new DecorSpot(Pebbles[StyleRules.Variant("caillou" + i, 3)], x, z, StyleRules.Variant("caillou" + i + "/lacet", 360), 1.0));
            }
            return spots;
        }

        /// <summary>Touffes d'herbe pâle au pied de chaque pierre : six à 0,80 m de son centre, aux échelles 0,5, 0,75 et 1.</summary>
        public const int TuftsPerStone = 6;

        public const double TuftDistance = 0.8;

        public static IReadOnlyList<DecorSpot> SeuilTufts(IEnumerable<SpaceObstacle> obstacles)
        {
            var spots = new List<DecorSpot>();
            foreach (SpaceObstacle stone in obstacles.Where(o => IsSeuilStone(o.Id)).OrderBy(o => o.Id, StringComparer.Ordinal))
            {
                (double cx, double cz) = Center(stone);
                int turn = StyleRules.Variant(stone.Id + "/touffes", 60);
                for (int k = 0; k < TuftsPerStone; k++)
                {
                    double a = (60.0 * k + turn) * Math.PI / 180;
                    spots.Add(new DecorSpot("touffe", cx + TuftDistance * Math.Sin(a), cz + TuftDistance * Math.Cos(a),
                        StyleRules.Variant(stone.Id + "/touffe" + k, 360), 0.5 + 0.25 * (k % 3)));
                }
            }
            return spots;
        }

        private static bool Free(double x, double z, double clearance, List<(double, double, double, double)> boxes, IReadOnlyList<StyleRoad> roads)
        {
            foreach ((double x0, double x1, double z0, double z1) in boxes)
                if (x > x0 - clearance && x < x1 + clearance && z > z0 - clearance && z < z1 + clearance) return false;
            return TownRules.RoadAt(x, z, roads).Edge >= clearance;
        }

        // --- Barrière du moulin -------------------------------------------------------------------------------------------------------

        /// <summary>Ouverture des vantaux (degrés) et durée de l'ouverture (secondes).</summary>
        public const double GateOpenDegrees = 90, GateSeconds = 3;

        /// <summary>
        /// Part de l'ouverture après <paramref name="deltaTime"/> s (0 fermée, 1 ouverte) : elle avance vers 1 si <paramref name="open"/>,
        /// vers 0 sinon, en <see cref="GateSeconds"/> s, quelle que soit la cadence d'images.
        /// </summary>
        public static double GateProgress(double progress, bool open, double deltaTime)
        {
            double step = Math.Max(0.0, deltaTime) / GateSeconds;
            double p = Math.Max(0.0, Math.Min(1.0, double.IsNaN(progress) ? 0.0 : progress));
            return open ? Math.Min(1.0, p + step) : Math.Max(0.0, p - step);
        }

        /// <summary>Angle des vantaux (degrés) pour une part d'ouverture : départ et arrivée en douceur.</summary>
        public static double GateAngle(double progress)
        {
            double p = Math.Max(0.0, Math.Min(1.0, double.IsNaN(progress) ? 0.0 : progress));
            return GateOpenDegrees * p * p * (3 - 2 * p);
        }

        /// <summary>
        /// Rôle d'un objet du modèle de la barrière d'après son nom : « G » et « D » (vantaux), « fermee » et « ouverte » (chaînes),
        /// « poteaux » ; null sinon.
        /// </summary>
        public static string? GatePart(string? name)
        {
            string key = StyleRules.Key(name ?? string.Empty);
            if (key.StartsWith("vantailg", StringComparison.Ordinal)) return "G";
            if (key.StartsWith("vantaild", StringComparison.Ordinal)) return "D";
            if (key.StartsWith("chainefermee", StringComparison.Ordinal)) return "fermee";
            if (key.StartsWith("chaineouverte", StringComparison.Ordinal)) return "ouverte";
            if (key.StartsWith("poteaux", StringComparison.Ordinal)) return "poteaux";
            return null;
        }

        /// <summary>
        /// Axe d'un vantail (« G » ou « D ») dans le repère du modèle d'Unity (signe de x changé, z = −Y de Blender), d'après l'export de
        /// jeu : Vantail_G (−2,66 ; −0,16), Vantail_D (2,66 ; −0,16). Null pour un autre nom.
        /// </summary>
        public static (double X, double Z)? GatePivot(string? leaf) =>
            leaf == "G" ? (2.66, 0.16) : leaf == "D" ? (-2.66, 0.16) : ((double, double)?)null;

        /// <summary>
        /// Sens de rotation d'un vantail dans Unity (autour de y, comme <c>Quaternion.Euler(0, signe × angle, 0)</c>) : +90° autour de Z
        /// dans Blender (Vantail_G) est −90° autour de y dans Unity (x et z changent de signe) ; les deux vers le moulin (Blender +Y).
        /// </summary>
        public static int GateLeafSign(string? leaf) => leaf == "G" ? -1 : leaf == "D" ? 1 : 0;

        /// <summary>
        /// Point (<paramref name="x"/>, <paramref name="z"/>) d'un vantail (repère du modèle d'Unity) tourné de <paramref name="degrees"/>
        /// autour de son axe, comme le fait la vue.
        /// </summary>
        public static (double X, double Z) GateTurn(string leaf, double degrees, double x, double z)
        {
            (double px, double pz) = GatePivot(leaf) ?? throw new ArgumentException("vantail G ou D", nameof(leaf));
            double a = GateLeafSign(leaf) * degrees * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a);
            double dx = x - px, dz = z - pz;
            return (px + dx * cos + dz * sin, pz - dx * sin + dz * cos);
        }

        // --- Carrière ---------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Accessoires de la carrière, devant le front de taille : (pièce, recul depuis la face exploitée, décalage le long de la face vers
        /// +z quand elle regarde l'ouest, lacet ajouté à celui de la face). La chèvre et le tas d'éclats regardent le front.
        /// </summary>
        private static readonly (string Piece, double Ahead, double Along, double Yaw)[] Quarry =
        {
            ("chevre", 4.0, 1.0, 180),
            ("traineau", 3.0, -3.5, 90),
            ("outils", 2.6, 3.7, 160),
            ("tas_eclats", 2.5, -12.3, 180),
        };

        /// <summary>
        /// Places des accessoires de la carrière devant le front de taille (emprise <paramref name="minX"/> … <paramref name="maxZ"/>, face
        /// exploitée vers <paramref name="facing"/>).
        /// </summary>
        public static IReadOnlyList<DecorSpot> QuarryProps(double minX, double minZ, double maxX, double maxZ, (int X, int Z) facing)
        {
            double cx = (minX + maxX) / 2, cz = (minZ + maxZ) / 2;
            double half = facing.X != 0 ? (maxX - minX) / 2 : (maxZ - minZ) / 2;
            double fx = cx + facing.X * half, fz = cz + facing.Z * half;
            double sx = facing.Z, sz = -facing.X;   // le long de la face (+z pour une face à l'ouest)
            double yaw = TownModels.FacingYaw(facing);
            return Quarry.Select(q => new DecorSpot(q.Piece, fx + facing.X * q.Ahead + sx * q.Along, fz + facing.Z * q.Ahead + sz * q.Along,
                yaw + q.Yaw, 1.0)).ToList();
        }
    }
}
