using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Runtime
{
    /// <summary>
    /// Une décoration de fête posée par le jeu : code, place (m), quart de tour (degrés, 0 : avant vers +z), largeur (le long de l'étal) et
    /// profondeur (m) de son enveloppe.
    /// </summary>
    public readonly struct FeastPiece
    {
        public string Code { get; }
        public double X { get; }
        public double Z { get; }
        public double Yaw { get; }
        public double Width { get; }
        public double Depth { get; }

        public FeastPiece(string code, double x, double z, double yaw, double width, double depth)
        {
            Code = code;
            X = x;
            Z = z;
            Yaw = yaw;
            Width = width;
            Depth = depth;
        }

        /// <summary>
        /// L'emprise gardée libre, en disques : des centres le long de la largeur, chacun du rayon de la demi-profondeur plus la marge
        /// (<see cref="FeastLooks.Margin"/>) : une décoration large et mince (paravent, bannière) ne prend pas la place d'un disque de sa largeur.
        /// </summary>
        public IReadOnlyList<(double X, double Z, double Radius)> Footprint => FeastLooks.Footprint(X, Z, Yaw, Width, Depth);
    }

    /// <summary>
    /// Sous-lot F4 (les 12 fêtes d'Orsédia dans le jeu, sans Unity) : modèles Tripo des fêtes (<see cref="Folder"/>/&lt;code&gt;/&lt;code&gt;.fbx,
    /// « &lt;code&gt;_couleur.jpg », « &lt;code&gt;_normale.png » ; échelle réelle en mètres, pivot au sol, avant vers +z, comme le lot C),
    /// icônes peintes (« fete_f01_d01 ») et pose de l'étal de fête de Maître Florentin sur la place, habillé des décorations du mois.
    /// </summary>
    public static class FeastLooks
    {
        /// <summary>Dossier des modèles des fêtes dans le projet Unity.</summary>
        public const string Folder = "Assets/EdS/Modeles/Fetes";

        /// <summary>Codes des modèles des fêtes livrés (décorations de l'étal, sous-lot F4).</summary>
        public static bool IsCode(string? code) =>
            code != null && code.Length == 7 && code[0] == 'F' && char.IsDigit(code[1]) && char.IsDigit(code[2]) && code[3] == '_'
            && (code[4] == 'D' || code[4] == 'P' || code[4] == 'M' || code[4] == 'R') && char.IsDigit(code[5]) && char.IsDigit(code[6]);

        /// <summary>Code d'un modèle des fêtes d'après son chemin (« …/Fetes/F01_D01/F01_D01.fbx » → « F01_D01 »), ou null.</summary>
        public static string? CodeOf(string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string p = path!.Replace('\\', '/');
            if (!p.StartsWith(Folder + "/", StringComparison.Ordinal) || !p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) return null;
            string[] parts = p.Substring(Folder.Length + 1).Split('/');
            if (parts.Length != 2) return null;
            string code = parts[1].Substring(0, parts[1].Length - 4);
            return IsCode(code) && parts[0] == code ? code : null;
        }

        /// <summary>Icône peinte d'une référence des fêtes (<c>Resources/EdsInterface/Peintes/fete_f01_d01.png</c>).</summary>
        public static string IconOf(string code) => "fete_" + code.ToLowerInvariant();

        /// <summary>Étal du forain : longueur et profondeur (m) gardées libres, distance devant le forain (comme les étals du marché).</summary>
        public const double StallWidth = 2.6, StallDepth = 1.8, StallDistance = 1.9;

        /// <summary>Marge (m) gardée autour d'une décoration, des obstacles et des autres pièces.</summary>
        public const double Margin = 0.25;

        /// <summary>
        /// L'étal de fête : le forain en (<paramref name="x"/>, <paramref name="z"/>) regarde <paramref name="face"/> (degrés, 0 = +z : le point
        /// sûr le plus proche, comme tout PNJ au repos) ; l'étal est devant lui à <see cref="StallDistance"/> m (il se tient derrière), ses
        /// décorations (<paramref name="decor"/> : code et enveloppe X, Y, Z en m) aux deux bouts de l'étal puis de part et d'autre du forain,
        /// l'avant vers les visiteurs, chacune à une place libre (obstacles, étal, forain, autres décorations : <see cref="Margin"/>), sinon pas
        /// posée. Déterministe ; affichage seulement, sans collision.
        /// </summary>
        public static (double X, double Z, IReadOnlyList<FeastPiece> Pieces) Stall(double x, double z, double face,
            IReadOnlyList<(string Code, double SizeX, double SizeY, double SizeZ)> decor, IReadOnlyList<IReadOnlyList<(double X, double Z)>> obstacles)
        {
            double a = face * Math.PI / 180;
            double sx = x + StallDistance * Math.Sin(a), sz = z + StallDistance * Math.Cos(a);
            var taken = new List<IReadOnlyList<(double X, double Z)>>(obstacles ?? Array.Empty<IReadOnlyList<(double, double)>>())
            {
                Box(sx, sz, face, StallWidth, StallDepth),
                Box(x, z, face, 0.9, 0.9),   // le forain
            };
            var pieces = new List<FeastPiece>();
            int slot = 0;
            foreach ((string code, double w0, double d0, double _) in decor ?? Array.Empty<(string, double, double, double)>())
            {
                double w = Math.Max(w0, 0.1), d = Math.Max(d0, 0.1);
                foreach ((double rx, double rz) in Slots(slot++, w, d))
                {
                    // Le repère de l'étal : x le long de l'étal (vers la droite du visiteur), z vers les visiteurs.
                    double px = sx + rx * Math.Cos(a) + rz * Math.Sin(a), pz = sz - rx * Math.Sin(a) + rz * Math.Cos(a);
                    IReadOnlyList<(double X, double Z, double Radius)> disks = Footprint(px, pz, face, w, d);
                    if (disks.Any(c => Wave5.FreeSpot(new[] { (c.X, c.Z) }, taken, c.Radius) is null)) continue;
                    pieces.Add(new FeastPiece(code, px, pz, face, w, d));
                    taken.Add(Box(px, pz, face, w, d));
                    break;
                }
            }
            return (sx, sz, pieces);
        }

        /// <summary>
        /// Places essayées pour la décoration de rang <paramref name="slot"/> (largeur <paramref name="w"/>, profondeur <paramref name="d"/>),
        /// dans le repère de l'étal : bouts de l'étal (gauche, droite), derrière de part et d'autre du forain, puis plus loin ; chaque rang
        /// commence ailleurs, tous finissent par essayer toutes les places.
        /// </summary>
        private static IEnumerable<(double X, double Z)> Slots(int slot, double w, double d)
        {
            double end = StallWidth / 2 + w / 2 + Margin, back = -StallDistance;
            var places = new List<(double, double)>
            {
                (-end, 0), (end, 0), (-end, back), (end, back), (-end - 1.0, back + 0.4), (end + 1.0, back + 0.4),
                (-end - 1.0, 0.8), (end + 1.0, 0.8), (-end, back - 1.4), (end, back - 1.4), (-end - 2.2, 0), (end + 2.2, 0),
                (-end - 2.2, back), (end + 2.2, back),
            };
            for (int i = 0; i < places.Count; i++) yield return places[(slot + i) % places.Count];
        }

        /// <summary>L'emprise d'une décoration en disques (<see cref="FeastPiece.Footprint"/>).</summary>
        public static IReadOnlyList<(double X, double Z, double Radius)> Footprint(double x, double z, double yaw, double w, double d)
        {
            double r = d / 2 + Margin, a = yaw * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
            double half = Math.Max(0, w / 2 - d / 2);
            int n = half <= 0 ? 1 : (int)Math.Ceiling(2 * half / Math.Max(d, 0.2)) + 1;
            var disks = new List<(double, double, double)>();
            for (int i = 0; i < n; i++)
            {
                double t = n == 1 ? 0 : -half + 2 * half * i / (n - 1);
                disks.Add((x + t * c, z - t * s, r));
            }
            return disks;
        }

        /// <summary>Sous-lot F5 : lieux où la fête du mois pose ses décorations : la place du village (le forain) et la place du marché du Bourg.</summary>
        public static IReadOnlyList<string> DecorPlaces { get; } = new[] { "bourg", "place_du_marche_de_la_ville" };

        /// <summary>Distance (m) gardée entre une décoration et une porte (on entre et on sort sans la frôler).</summary>
        public const double DoorClearance = 2.5;

        /// <summary>Distance (m) gardée entre une décoration et un PNJ, un invité de fête ou un point sûr.</summary>
        public const double PeopleClearance = 1.0;

        /// <summary>
        /// Sous-lot F5 (décor du mois) : les décorations de la fête (<paramref name="decor"/> : code et enveloppe X, Y, Z en m), une de chaque
        /// dans chacun des <paramref name="places"/> (id et rectangle), à la première place qui convient sur des cercles autour du centre du lieu
        /// (4 à 22 m, 16 directions, dans le lieu), tournées vers le centre : emprise (<see cref="FeastPiece.Footprint"/>) hors des obstacles,
        /// **hors des routes** (bord de la chaussée), à <see cref="DoorClearance"/> m des portes, à <see cref="PeopleClearance"/> m des PNJ, des
        /// invités, des points sûrs et des pièces déjà posées (<paramref name="avoid"/>), sans toucher une autre décoration ni l'étal
        /// (<paramref name="taken"/>). Une décoration sans place n'est pas posée. Déterministe ; affichage seulement, sans collision.
        /// </summary>
        public static IReadOnlyList<FeastPiece> Village(IReadOnlyList<(string Code, double SizeX, double SizeY, double SizeZ)> decor,
            IReadOnlyList<(string Id, double MinX, double MinZ, double MaxX, double MaxZ)> places, IReadOnlyList<IReadOnlyList<(double X, double Z)>> obstacles,
            IReadOnlyList<StyleRoad> roads, IReadOnlyList<(double X, double Z)> doors, IReadOnlyList<(double X, double Z)> avoid,
            IReadOnlyList<IReadOnlyList<(double X, double Z)>>? taken = null)
        {
            var used = new List<IReadOnlyList<(double X, double Z)>>(obstacles ?? Array.Empty<IReadOnlyList<(double, double)>>());
            var mine = new List<IReadOnlyList<(double X, double Z)>>(taken ?? Array.Empty<IReadOnlyList<(double, double)>>());
            var pieces = new List<FeastPiece>();
            foreach ((string id, double x0, double z0, double x1, double z1) in places ?? Array.Empty<(string, double, double, double, double)>())
            {
                double cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
                int rank = 0;
                foreach ((string code, double w0, double d0, double _) in decor ?? Array.Empty<(string, double, double, double)>())
                {
                    double w = Math.Max(w0, 0.1), d = Math.Max(d0, 0.1);
                    foreach ((double px, double pz) in Rings(cx, cz, rank++))
                    {
                        if (px < x0 + 1 || px > x1 - 1 || pz < z0 + 1 || pz > z1 - 1) continue;
                        double yaw = Math.Round(StyleRules.FaceYaw(px, pz, cx, cz) / 15) * 15;
                        if (!Fits(px, pz, yaw, w, d, used, mine, roads, doors, avoid)) continue;
                        pieces.Add(new FeastPiece(code, px, pz, yaw, w, d));
                        mine.Add(Box(px, pz, yaw, w, d));
                        break;
                    }
                }
            }
            return pieces;
        }

        /// <summary>La décoration (centre, quart de tour, largeur, profondeur) respecte toutes les règles de <see cref="Village"/>.</summary>
        public static bool Fits(double x, double z, double yaw, double w, double d, IReadOnlyList<IReadOnlyList<(double X, double Z)>> obstacles,
            IReadOnlyList<IReadOnlyList<(double X, double Z)>> taken, IReadOnlyList<StyleRoad> roads, IReadOnlyList<(double X, double Z)> doors,
            IReadOnlyList<(double X, double Z)> avoid)
        {
            foreach ((double px, double pz, double r) in Footprint(x, z, yaw, w, d))
            {
                if (Wave5.FreeSpot(new[] { (px, pz) }, obstacles, r) is null || Wave5.FreeSpot(new[] { (px, pz) }, taken, r) is null) return false;
                if (roads != null && roads.Count > 0 && TownRules.RoadAt(px, pz, roads).Edge < r) return false;
                foreach ((double qx, double qz) in doors ?? Array.Empty<(double, double)>())
                    if ((px - qx) * (px - qx) + (pz - qz) * (pz - qz) < (r + DoorClearance) * (r + DoorClearance)) return false;
                foreach ((double qx, double qz) in avoid ?? Array.Empty<(double, double)>())
                    if ((px - qx) * (px - qx) + (pz - qz) * (pz - qz) < (r + PeopleClearance) * (r + PeopleClearance)) return false;
            }
            return true;
        }

        /// <summary>Places essayées autour du centre d'un lieu : cercles de 4 à 22 m, 16 directions, chaque rang commençant ailleurs.</summary>
        private static IEnumerable<(double X, double Z)> Rings(double cx, double cz, int rank)
        {
            foreach (double r in new[] { 4.0, 6.5, 9.0, 11.5, 14.0, 17.0, 20.0, 22.0 })
                for (int i = 0; i < 16; i++)
                {
                    double a = ((i + rank * 5) % 16) * Math.PI / 8 + r * 0.1;
                    yield return (cx + r * Math.Sin(a), cz + r * Math.Cos(a));
                }
        }

        /// <summary>Rectangle <paramref name="w"/> × <paramref name="d"/> m centré en (<paramref name="x"/>, <paramref name="z"/>), tourné de <paramref name="yaw"/>.</summary>
        public static IReadOnlyList<(double X, double Z)> Box(double x, double z, double yaw, double w, double d)
        {
            double a = yaw * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
            return new[] { (-w / 2, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2) }
                .Select(p => (x + p.Item1 * c + p.Item2 * s, z - p.Item1 * s + p.Item2 * c)).ToList();
        }
    }
}
