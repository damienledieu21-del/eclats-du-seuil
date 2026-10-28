using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Space;

namespace EdS.Runtime
{
    /// <summary>
    /// Essai réaliste 6 (essai de Damien du 27 octobre 2026 : « je voudrais que la mini Map soit améliorée, là ça fait vraiment schéma ») :
    /// la carte peinte d'une zone, vue de dessus, nord en haut, d'après ce que le jeu pose dans le monde : herbe des prés (même bruit que les
    /// touffes), terre et roche autour des obstacles (<see cref="StyleRules.GroundMix"/>), rivière et berges, pont de planches, palissades,
    /// murets de pierres sèches, toit de tuiles de l'abri, moulin et ses ailes, rochers, arbres et lisière de forêt autour de l'aire, avec
    /// les ombres portées. Vallons agrandis (28 octobre 2026) : routes de terre et rue pavée, toits des maisons, de l'auberge, de la chapelle
    /// et de la grange, puits, étals, tour de guet, tentes, bois, champs cultivés (<see cref="TownRules"/>). Calculée sans Unity et sans
    /// hasard (mêmes pixels à chaque fois), une fois par zone.
    /// </summary>
    public static class MapPainter
    {
        /// <summary>Bande de forêt peinte autour de l'aire (m) : la petite carte, centrée sur le joueur, en montre près des bords.</summary>
        public const double Margin = 10.0;

        /// <summary>Pixels par mètre de l'image (la petite carte en montre un peu plus de six par mètre).</summary>
        public const double PixelsPerMeter = 6.0;

        /// <summary>Taille de l'image d'une carte (au plus 2048 de côté, au moins 16).</summary>
        public static (int Width, int Height) Size(AdventureMap map, double pixelsPerMeter = PixelsPerMeter, double margin = Margin)
        {
            if (map is null) throw new ArgumentNullException(nameof(map));
            int w = (int)Math.Ceiling((map.Width + 2 * margin) * pixelsPerMeter), h = (int)Math.Ceiling((map.Depth + 2 * margin) * pixelsPerMeter);
            return (Math.Max(16, Math.Min(2048, w)), Math.Max(16, Math.Min(2048, h)));
        }

        /// <summary>Place d'un point du sol dans l'image peinte, de 0 à 1 (U vers l'est, V vers le sud depuis le haut), marge comprise.</summary>
        public static (double U, double V) ToImage(AdventureMap map, double x, double z, double margin = Margin) =>
            ((x - (map.MinX - margin)) / (map.Width + 2 * margin), ((map.MaxZ + margin) - z) / (map.Depth + 2 * margin));

        // Couleurs de base (0 à 1).
        private static readonly double[] GrassA = { 0.27, 0.38, 0.17 }, GrassB = { 0.42, 0.52, 0.22 }, Soil = { 0.45, 0.35, 0.23 }, Rock = { 0.56, 0.53, 0.47 },
            Forest = { 0.12, 0.17, 0.09 }, WaterEdge = { 0.22, 0.42, 0.52 }, WaterDeep = { 0.07, 0.2, 0.33 }, Bank = { 0.28, 0.23, 0.15 },
            Plank = { 0.53, 0.38, 0.22 }, Log = { 0.42, 0.29, 0.17 }, WallStone = { 0.6, 0.58, 0.53 }, Tiles = { 0.63, 0.33, 0.18 },
            Plaster = { 0.86, 0.83, 0.76 }, Thatch = { 0.42, 0.3, 0.2 }, Leaves = { 0.17, 0.3, 0.12 }, LeavesLight = { 0.36, 0.5, 0.2 },
            Stone = { 0.5, 0.48, 0.45 }, Glow = { 0.75, 0.55, 1.0 }, Petal = { 0.98, 0.9, 0.62 }, PetalPink = { 0.98, 0.7, 0.8 },
            Dirt = { 0.56, 0.46, 0.32 }, Cobble = { 0.55, 0.52, 0.48 }, TilesDark = { 0.52, 0.27, 0.17 }, Shingles = { 0.46, 0.35, 0.23 },
            Slate = { 0.34, 0.36, 0.41 }, Wheat = { 0.8, 0.68, 0.36 }, Sprouts = { 0.44, 0.56, 0.22 }, Plowed = { 0.4, 0.3, 0.2 },
            Canvas = { 0.84, 0.78, 0.64 }, CanvasDark = { 0.58, 0.5, 0.38 }, ClothRed = { 0.68, 0.2, 0.17 }, ClothBlue = { 0.2, 0.34, 0.58 },
            Cream = { 0.9, 0.85, 0.72 }, Well = { 0.07, 0.14, 0.19 };

        private sealed class Blob
        {
            public double X, Z, Radius, Height;
            public int Kind;   // 0 arbre, 1 rocher, 2 pierre du Seuil
            public string Id = "";
        }

        /// <summary>Un bâtiment posé (maison, auberge, chapelle, grange, puits, étal, tour, tente) : sa forme et ce qu'il est.</summary>
        private sealed class Building
        {
            public string Id = "";
            public StyleBuilding Kind;
            public (double X, double Z)[] Points = Array.Empty<(double, double)>();
            public double MinX, MinZ, MaxX, MaxZ, CenterX, CenterZ, Radius;
            public (int X, int Z) Facing;

            /// <summary>Emprise du toit (avec son débord) ; égale à la boîte pour les formes rondes.</summary>
            public double RoofMinX, RoofMinZ, RoofMaxX, RoofMaxZ;

            public double Height;
            public bool Round => Kind == StyleBuilding.Well || Kind == StyleBuilding.Tower || Kind == StyleBuilding.Tent;
        }

        /// <summary>
        /// Image peinte, <paramref name="width"/> × <paramref name="height"/> pixels (1 à 2048), en RVBA 8 bits, lignes du sud au nord (comme
        /// une texture Unity), couvrant l'aire et <paramref name="margin"/> mètres de forêt autour.
        /// </summary>
        public static byte[] Paint(AdventureMap map, int width, int height, double margin = Margin)
        {
            if (map is null) throw new ArgumentNullException(nameof(map));
            if (width < 1 || width > 2048) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1 || height > 2048) throw new ArgumentOutOfRangeException(nameof(height));
            if (margin < 0 || margin > 100) throw new ArgumentOutOfRangeException(nameof(margin));
            double x0 = map.MinX - margin, z0 = map.MinZ - margin, w = map.Width + 2 * margin, d = map.Depth + 2 * margin;

            var obstacles = map.Obstacles.Select(o => (o.Id, o.Low, Points: o.Points.Select(p => (X: p.X.Raw / 10000.0, Z: p.Z.Raw / 10000.0)).ToArray())).ToList();
            (double MinX, double MinZ, double MaxX, double MaxZ) Box((double X, double Z)[] pts) => (pts.Min(p => p.X), pts.Min(p => p.Z), pts.Max(p => p.X), pts.Max(p => p.Z));
            IReadOnlyList<StyleRoad> roads = map.Roads;

            // Couches du sol, comme dans le monde (TownRules.GroundSpots).
            var spots = new SpotGrid(TownRules.GroundSpots(obstacles.Select(o =>
            {
                var b = Box(o.Points);
                return (o.Id, b.MinX, b.MinZ, b.MaxX, b.MaxZ);
            })));

            // Rivière : ses bras, et le passage sous le pont.
            var rivers = obstacles.Where(o => o.Id.StartsWith("riviere", StringComparison.Ordinal)).Select(o => (IReadOnlyList<(double X, double Z)>)o.Points).ToList();
            var riverBoxes = rivers.Select(r => Box(r.ToArray())).ToList();
            (double X0, double Z0, double X1, double Z1)? deck = null;
            var south = obstacles.FirstOrDefault(o => o.Id == "riviere_sud");
            var north = obstacles.FirstOrDefault(o => o.Id == "riviere_nord");
            if (south.Points != null && north.Points != null)
            {
                var sb = Box(south.Points);
                double gz0 = sb.MaxZ, gz1 = north.Points.Min(p => p.Z);
                if (gz1 > gz0)
                {
                    rivers.Add(new[] { (sb.MinX, gz0), (sb.MaxX, gz0), (sb.MaxX, gz1), (sb.MinX, gz1) });
                    riverBoxes.Add((sb.MinX, gz0, sb.MaxX, gz1));
                    deck = (sb.MinX - 0.6, gz0, sb.MaxX + 0.6, gz1);
                }
            }

            // Bâtiments (Vallons agrandis).
            var buildings = new List<Building>();
            foreach (var o in obstacles)
            {
                StyleBuilding kind = TownRules.BuildingFor(o.Id);
                if (kind == StyleBuilding.None || kind == StyleBuilding.Woods) continue;
                var b = Box(o.Points);
                var building = new Building
                {
                    Id = o.Id, Kind = kind, Points = o.Points, MinX = b.MinX, MinZ = b.MinZ, MaxX = b.MaxX, MaxZ = b.MaxZ,
                    CenterX = o.Points.Average(p => p.X), CenterZ = o.Points.Average(p => p.Z),
                    Facing = TownRules.Facing(b.MinX, b.MinZ, b.MaxX, b.MaxZ, roads),
                };
                building.Radius = o.Points.Max(p => Math.Sqrt((p.X - building.CenterX) * (p.X - building.CenterX) + (p.Z - building.CenterZ) * (p.Z - building.CenterZ)));
                double overhang = kind == StyleBuilding.Stall ? 0.3 : building.Round ? 0 : 0.5;
                (building.RoofMinX, building.RoofMinZ, building.RoofMaxX, building.RoofMaxZ) = (b.MinX - overhang, b.MinZ - overhang, b.MaxX + overhang, b.MaxZ + overhang);
                double shortSide = Math.Min(b.MaxX - b.MinX, b.MaxZ - b.MinZ);
                building.Height = kind switch
                {
                    StyleBuilding.Well => 1.6,
                    StyleBuilding.Stall => 2.2,
                    StyleBuilding.Tent => 3.0,
                    _ => TownRules.WallHeight(kind) + shortSide * 0.45,
                };
                buildings.Add(building);
            }

            // Bois : leur sol sombre, et leurs arbres (avec ceux de la lisière, plus bas).
            var woods = obstacles.Where(o => TownRules.BuildingFor(o.Id) == StyleBuilding.Woods).Select(o => (o.Id, o.Points, Box: Box(o.Points))).ToList();

            // Champs cultivés des lieux de culture.
            var obstacleBoxes = obstacles.Select(o => Box(o.Points)).ToList();
            var fields = map.Places.Where(p => TownRules.Farmland(p.Id))
                .SelectMany(p => TownRules.Fields(p.MinX, p.MinZ, p.MaxX, p.MaxZ, obstacleBoxes, roads, map.Clearings, (map.MinX, map.MinZ, map.MaxX, map.MaxZ)))
                .ToList();

            // Objets posés : arbres, rochers, pierres du Seuil, front de taille (rochers serrés), bois, forêt de la lisière.
            var blobs = new List<Blob>();
            foreach (var o in obstacles)
            {
                var b = Box(o.Points);
                double cx = (b.MinX + b.MaxX) / 2, cz = (b.MinZ + b.MaxZ) / 2, size = Math.Max(b.MaxX - b.MinX, b.MaxZ - b.MinZ);
                if (o.Id.StartsWith("arbre", StringComparison.Ordinal)) blobs.Add(new Blob { X = cx, Z = cz, Radius = 2.6, Height = 1, Kind = 0, Id = o.Id });
                else if (o.Id.StartsWith("pierre_seuil", StringComparison.Ordinal)) blobs.Add(new Blob { X = cx, Z = cz, Radius = size * 0.7, Height = 0.5, Kind = 2, Id = o.Id });
                else if (o.Id.StartsWith("front_de_taille", StringComparison.Ordinal))
                {
                    bool alongX = b.MaxX - b.MinX >= b.MaxZ - b.MinZ;
                    double length = alongX ? b.MaxX - b.MinX : b.MaxZ - b.MinZ, depth = alongX ? b.MaxZ - b.MinZ : b.MaxX - b.MinX;
                    int count = Math.Max(1, (int)Math.Ceiling(length / 3.2));
                    for (int i = 0; i < count; i++)
                    {
                        double s = (i + 0.5) / count * length;
                        double rx = alongX ? b.MinX + s : cx, rz = alongX ? cz : b.MinZ + s;
                        blobs.Add(new Blob { X = rx, Z = rz, Radius = depth * 0.62, Height = 0.6, Kind = 1, Id = o.Id + "/" + i });
                        blobs.Add(new Blob { X = rx + (alongX ? 1.6 : -depth * 0.3), Z = rz + (alongX ? -depth * 0.3 : 1.6), Radius = depth * 0.3, Height = 0.35, Kind = 1, Id = o.Id + "/pied/" + i });
                    }
                }
                else if (StyleRules.ForObstacle(o.Id) == StyleSlot.Rock) blobs.Add(new Blob { X = cx, Z = cz, Radius = size * 0.62, Height = 0.5, Kind = 1, Id = o.Id });
            }
            foreach (var wood in woods)
                foreach (StyleWoodTree tree in TownRules.WoodTrees(wood.Points, wood.Id, WoodSpacing))
                    blobs.Add(new Blob { X = tree.X, Z = tree.Z, Radius = 2.5 * tree.Scale, Height = 1, Kind = 0, Id = wood.Id });
            // Lisière : autant d'arbres que la bande en porte (un tous les 12 m² environ), quelle que soit la taille de l'aire.
            double band = (map.Width + 2 * (margin + 6)) * (map.Depth + 2 * (margin + 6)) - (map.Width + 6) * (map.Depth + 6);
            foreach (var (fx, fz) in StyleRules.Forest(map.MinX, map.MinZ, map.MaxX, map.MaxZ, margin + 6, Math.Max(1, (int)(band / 12)), 3.0, 3.0))
                blobs.Add(new Blob { X = fx, Z = fz, Radius = 2.4 + 0.012 * StyleRules.Variant("lisiere" + fx.ToString("F1") + fz.ToString("F1"), 80), Height = 1, Kind = 0, Id = "lisiere" });
            // Grille des objets (cases de 4 m) pour ne tester que les voisins.
            const double Cell = 4.0;
            var grid = new Dictionary<(int, int), List<Blob>>();
            foreach (Blob blob in blobs)
                for (int gx = (int)Math.Floor((blob.X - blob.Radius - 3) / Cell); gx <= (int)Math.Floor((blob.X + blob.Radius + 3) / Cell); gx++)
                    for (int gz = (int)Math.Floor((blob.Z - blob.Radius - 3) / Cell); gz <= (int)Math.Floor((blob.Z + blob.Radius + 3) / Cell); gz++)
                    {
                        if (!grid.TryGetValue((gx, gz), out var list)) grid[(gx, gz)] = list = new List<Blob>();
                        list.Add(blob);
                    }
            List<Blob> Near(double x, double z) => grid.TryGetValue(((int)Math.Floor(x / Cell), (int)Math.Floor(z / Cell)), out var l) ? l : NoBlob;
            var buildingGrid = new Dictionary<(int, int), List<Building>>();
            foreach (Building building in buildings)
                for (int gx = (int)Math.Floor((building.RoofMinX - 3) / Cell); gx <= (int)Math.Floor((building.RoofMaxX + 3) / Cell); gx++)
                    for (int gz = (int)Math.Floor((building.RoofMinZ - 3) / Cell); gz <= (int)Math.Floor((building.RoofMaxZ + 3) / Cell); gz++)
                    {
                        if (!buildingGrid.TryGetValue((gx, gz), out var list)) buildingGrid[(gx, gz)] = list = new List<Building>();
                        list.Add(building);
                    }
            List<Building> NearBuildings(double x, double z) =>
                buildingGrid.TryGetValue(((int)Math.Floor(x / Cell), (int)Math.Floor(z / Cell)), out var l) ? l : NoBuilding;

            // Bâtiments et clôtures.
            var palisades = obstacles.Where(o => StyleRules.FenceFor(o.Id) == StyleFence.Palisade).Select(o => Box(o.Points)).ToList();
            var walls = obstacles.Where(o => StyleRules.FenceFor(o.Id) == StyleFence.Hedge).Select(o => Box(o.Points)).ToList();
            var mills = obstacles.Where(o => o.Id.StartsWith("moulin", StringComparison.Ordinal)).Select(o => Box(o.Points)).ToList();
            var pillars = obstacles.Where(o => o.Id.StartsWith("pilier", StringComparison.Ordinal)).Select(o => Box(o.Points)).ToList();
            (double MinX, double MinZ, double MaxX, double MaxZ)? roof = pillars.Count >= 2
                ? (pillars.Min(p => p.MinX) - 0.8, pillars.Min(p => p.MinZ) - 0.8, pillars.Max(p => p.MaxX) + 0.8, pillars.Max(p => p.MaxZ) + 0.8)
                : ((double, double, double, double)?)null;
            var others = obstacles.Where(o => !o.Low && !o.Id.StartsWith("arbre", StringComparison.Ordinal) && !o.Id.StartsWith("moulin", StringComparison.Ordinal)
                && !o.Id.StartsWith("pilier", StringComparison.Ordinal) && StyleRules.ForObstacle(o.Id) == null && StyleRules.FenceFor(o.Id) == StyleFence.None
                && !o.Id.StartsWith("front_de_taille", StringComparison.Ordinal) && TownRules.BuildingFor(o.Id) == StyleBuilding.None)
                .Select(o => (Points: o.Points, Box: Box(o.Points))).ToList();

            // Hauteur de ce qui dépasse du sol, pour les ombres (soleil au nord-ouest : ombres vers le sud-est).
            double Height(double x, double z)
            {
                double h = 0;
                foreach (Blob blob in Near(x, z))
                {
                    double dx = x - blob.X, dz = z - blob.Z, r = blob.Radius;
                    double dd = dx * dx + dz * dz;
                    if (dd < r * r) h = Math.Max(h, blob.Height * Math.Sqrt(1 - dd / (r * r)) * (blob.Kind == 0 ? 1.8 : 1));
                }
                foreach (Building b in NearBuildings(x, z))
                {
                    if (b.Round)
                    {
                        double dx = x - b.CenterX, dz = z - b.CenterZ, dd = dx * dx + dz * dz;
                        if (dd < b.Radius * b.Radius) h = Math.Max(h, b.Kind == StyleBuilding.Tent ? b.Height * (1 - Math.Sqrt(dd) / b.Radius) : b.Height);
                    }
                    else if (x >= b.RoofMinX && x <= b.RoofMaxX && z >= b.RoofMinZ && z <= b.RoofMaxZ) h = Math.Max(h, b.Height);
                }
                foreach (var m in mills) if (InCircle(x, z, m, 1.0)) h = Math.Max(h, 2.0);
                if (roof is (double rx0, double rz0, double rx1, double rz1) && x >= rx0 && x <= rx1 && z >= rz0 && z <= rz1) h = Math.Max(h, 1.6);
                foreach (var p in palisades) if (In(x, z, p)) h = Math.Max(h, 1.4);
                foreach (var wb in walls) if (In(x, z, wb)) h = Math.Max(h, 0.45);
                return h;
            }

            var pixels = new byte[checked(width * height * 4)];
            // Vallons agrandis : une ligne par tâche (chaque pixel ne dépend que de sa place : mêmes pixels, quel que soit l'ordre).
            // Relecture : la moitié des cœurs au plus (la carte se peint pendant qu'on joue).
            var options = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) };
            System.Threading.Tasks.Parallel.For(0, height, options, row =>
            {
                var c = new double[3];
                double z = z0 + (row + 0.5) * d / height;
                for (int col = 0; col < width; col++)
                {
                    double x = x0 + (col + 0.5) * w / width;
                    bool outside = x < map.MinX || x > map.MaxX || z < map.MinZ || z > map.MaxZ;
                    double fine = 0.88 + 0.24 * StyleRules.Noise(x * 1.9, z * 1.9) + 0.06 * (Hash(col, row) - 0.5);

                    // 1. Sol : herbe des prés (même bruit que les touffes), taches de terre, roche ; sous-bois sombre hors de l'aire.
                    StyleMix mix = spots.Mix(x, z);
                    RoadHit road = TownRules.RoadAt(x, z, roads);
                    double meadow = StyleRules.Noise(x / StyleRules.MeadowPatch, z / StyleRules.MeadowPatch);
                    for (int k = 0; k < 3; k++)
                    {
                        double grass = GrassA[k] + (GrassB[k] - GrassA[k]) * meadow;
                        c[k] = (grass * (mix.Grass + 0.3 * mix.Soil) + Soil[k] * 0.7 * mix.Soil + Rock[k] * (0.9 + 0.2 * StyleRules.Noise(x * 0.8 + 40, z * 0.8)) * mix.Rock) * fine;
                    }
                    bool farmed = false;
                    foreach (StyleField field in fields)
                        if (x >= field.MinX && x <= field.MaxX && z >= field.MinZ && z <= field.MaxZ)
                        {
                            Crop(c, field, x, z, fine);
                            farmed = true;
                            break;
                        }
                    if (!farmed && mix.Grass > 0.8 && road.Edge > 0.8 && StyleRules.Noise(x * 2.3 + 101.3, z * 2.3 - 57.1) > 0.86 && meadow > 0.5 && Hash(col, row * 5) > 0.55)
                        Blend(c, Hash(col * 7, row * 3) > 0.5 ? Petal : PetalPink, 0.55);
                    if (road.Road is StyleRoad near) PaintRoad(c, near, road, x, z, fine);
                    if (outside)
                    {
                        double edge = Math.Min(1, Distance(x, z, map) / 3.0);
                        Blend(c, Forest, 0.55 * edge);
                    }
                    foreach (var wood in woods)
                        if (In(x, z, wood.Box))
                        {
                            double inside = StyleRules.InsideDistance(x, z, wood.Points);
                            if (inside > -1.5) Blend(c, Forest, 0.55 * Math.Min(1, (inside + 1.5) / 3.0));
                        }

                    // 2. Eau et berges.
                    double inWater = double.NegativeInfinity;
                    for (int i = 0; i < rivers.Count; i++)
                    {
                        var rb = riverBoxes[i];
                        if (x < rb.MinX - 1 || x > rb.MaxX + 1 || z < rb.MinZ - 1 || z > rb.MaxZ + 1) continue;
                        inWater = Math.Max(inWater, StyleRules.InsideDistance(x, z, rivers[i]));
                    }
                    if (inWater > 0)
                    {
                        double t = Math.Min(1, inWater / 1.3);
                        for (int k = 0; k < 3; k++) c[k] = WaterEdge[k] + (WaterDeep[k] - WaterEdge[k]) * t;
                        double ripple = Math.Sin(z * 3.1 + 2.0 * StyleRules.Noise(x * 1.5, z * 0.7) * 6.28) * 0.5 + 0.5;
                        double glint = StyleRules.Noise(x * 3.3, z * 1.1) > 0.78 ? 0.18 : 0;
                        Lighten(c, 0.06 * ripple + glint);
                    }
                    else if (inWater > -0.55) Blend(c, Bank, 0.75 * (1 + inWater / 0.55));

                    // 3. Ombres portées (sud-est).
                    double shadow = 0;
                    for (int k = 1; k <= 6; k++)
                    {
                        double s = k * 0.35;
                        double over = Height(x - s * 0.8, z + s * 0.6) - s * 0.55;
                        if (over > 0) shadow = Math.Max(shadow, Math.Min(1, over * 1.6));
                    }
                    if (shadow > 0) Darken(c, 0.42 * shadow);

                    // 4. Pont : planches en travers (on marche le long de x), longerons sombres sur les bords.
                    if (deck is (double dx0, double dz0, double dx1, double dz1) && x >= dx0 && x <= dx1 && z >= dz0 - 0.05 && z <= dz1 + 0.05)
                    {
                        double plank = (x - dx0) / 0.28;
                        double seam = Math.Abs(plank - Math.Round(plank));
                        Set(c, Plank, 0.88 + 0.2 * Hash((int)Math.Floor(plank), 17));
                        if (seam < 0.12) Darken(c, 0.45);
                        if (z - dz0 < 0.2 || dz1 - z < 0.2) Set(c, Log, 0.8);
                    }

                    // 5. Clôtures, abri, moulin, bâtiments, autres obstacles bâtis.
                    foreach (var p in palisades)
                        if (In(x, z, p))
                        {
                            bool alongX = p.MaxX - p.MinX >= p.MaxZ - p.MinZ;
                            double along = (alongX ? x - p.MinX : z - p.MinZ) / (2 * StyleRules.LogRadius * 0.92);
                            double across = alongX ? (z - (p.MinZ + p.MaxZ) / 2) : (x - (p.MinX + p.MaxX) / 2);
                            double f = along - Math.Floor(along);
                            double round = 1 - Math.Pow(2 * f - 1, 2);
                            Set(c, Log, 0.65 + 0.5 * round - 0.25 * Math.Abs(across));
                        }
                    foreach (var wb in walls)
                        if (In(x, z, wb))
                        {
                            double cell = StyleRules.Noise(x * 2.6, z * 2.6);
                            Set(c, WallStone, 0.75 + 0.35 * cell);
                            if (Math.Abs(cell - 0.5) < 0.04) Darken(c, 0.35);
                        }
                    if (roof is (double qx0, double qz0, double qx1, double qz1) && x >= qx0 && x <= qx1 && z >= qz0 && z <= qz1)
                        GableRoof(c, x, z, qx0, qz0, qx1, qz1, Tiles, 0.32, false);
                    foreach (var m in mills)
                    {
                        double cx = (m.MinX + m.MaxX) / 2, cz = (m.MinZ + m.MaxZ) / 2, r = Math.Min(m.MaxX - m.MinX, m.MaxZ - m.MinZ) / 2;
                        double dx = x - cx, dz = z - cz, dist = Math.Sqrt(dx * dx + dz * dz);
                        // Ailes : quatre pales en croix, tournées de 30°.
                        for (int k = 0; k < 4; k++)
                        {
                            double a = Math.PI / 6 + k * Math.PI / 2, ux = Math.Cos(a), uz = Math.Sin(a);
                            double along = dx * ux + dz * uz, side = -dx * uz + dz * ux;
                            if (along > 0 && along < r * 2.1 && Math.Abs(side) < 0.55)
                                Set(c, Math.Abs(side) < 0.1 || along < r * 0.4 ? Log : Plaster, Math.Abs(side) < 0.1 ? 0.8 : 0.82 + 0.15 * Math.Sin(along * 6));
                        }
                        if (dist < r)
                        {
                            double t = dist / r;
                            Set(c, t < 0.62 ? Thatch : Plaster, t < 0.62 ? 0.8 + 0.4 * (1 - t) * (dx < 0 && dz > 0 ? 1 : 0.5) : 0.9 + 0.1 * (dx < 0 ? 1 : -1));
                            if (Math.Abs(t - 0.62) < 0.05 || t > 0.95) Darken(c, 0.3);
                        }
                    }
                    foreach (Building b in NearBuildings(x, z)) PaintBuilding(c, b, x, z);
                    foreach (var o in others)
                        if (In(x, z, o.Box) && AdventureMap.Inside(o.Points, x, z)) Set(c, Stone, 0.85 + 0.2 * StyleRules.Noise(x * 2, z * 2));

                    // 6. Rochers, pierres du Seuil, arbres : dômes éclairés du nord-ouest, contour irrégulier.
                    foreach (Blob blob in Near(x, z))
                    {
                        double dx = x - blob.X, dz = z - blob.Z, dd = dx * dx + dz * dz;
                        // Loin du contour le plus large (et de la lueur d'une pierre du Seuil) : rien à peindre.
                        double reach = blob.Radius * (blob.Kind == 2 ? 3.2 : 1.13);
                        if (dd >= reach * reach) continue;
                        double dist = Math.Sqrt(dd);
                        double angle = Math.Atan2(dz, dx);
                        double wobble = blob.Kind == 0 ? 0.82 + 0.3 * StyleRules.Noise(angle * 2.2 + blob.X, blob.Z * 0.3) : 0.8 + 0.28 * StyleRules.Noise(angle * 1.6 + blob.X * 0.7, blob.Z);
                        double r = blob.Radius * wobble;
                        if (blob.Kind == 2 && dist < blob.Radius * 3.2) Blend(c, Glow, 0.35 * (1 - dist / (blob.Radius * 3.2)));
                        if (dist >= r) continue;
                        double t = dist / r;
                        double nz = Math.Sqrt(Math.Max(0, 1 - t * t));
                        double lx = -0.55, lz = 0.55, ly = 0.63;   // lumière venant du nord-ouest et d'en haut
                        double nx = dx / Math.Max(1e-6, r), nzz = dz / Math.Max(1e-6, r);
                        double lambert = Math.Max(0, nx * lx + nzz * lz + nz * ly);
                        if (blob.Kind == 0)
                        {
                            double leaf = StyleRules.Noise(x * 1.4 + 7, z * 1.4 - 3);
                            for (int k = 0; k < 3; k++) c[k] = Leaves[k] + (LeavesLight[k] - Leaves[k]) * Math.Min(1, lambert * 0.9 + (leaf - 0.5) * 0.6);
                            if (t > 0.9) Darken(c, 0.3);
                        }
                        else
                        {
                            double grain = StyleRules.Noise(x * 3 + 13, z * 3);
                            for (int k = 0; k < 3; k++) c[k] = Rock[k] * (0.45 + 0.75 * lambert) * (0.9 + 0.2 * grain);
                            if (blob.Kind == 2) Blend(c, Glow, 0.2);
                            if (t > 0.92) Darken(c, 0.35);
                        }
                    }

                    int at = (row * width + col) * 4;
                    pixels[at] = ToByte(c[0]);
                    pixels[at + 1] = ToByte(c[1]);
                    pixels[at + 2] = ToByte(c[2]);
                    pixels[at + 3] = 255;
                }
            });
            return pixels;
        }

        /// <summary>Espacement (m) des arbres d'un bois, sur la carte comme dans le monde.</summary>
        public const double WoodSpacing = 4.0;

        /// <summary>Champ : blé mûr ou vert en rangs, ou terre labourée en sillons ; un liseré d'herbe au bord.</summary>
        private static void Crop(double[] c, StyleField field, double x, double z, double fine)
        {
            double across = field.RowsAlongX ? z - field.MinZ : x - field.MinX;
            double rowPhase = across / 0.7 - Math.Floor(across / 0.7);
            double edge = Math.Min(Math.Min(x - field.MinX, field.MaxX - x), Math.Min(z - field.MinZ, field.MaxZ - z));
            double[] color = field.Crop == 0 ? Wheat : field.Crop == 1 ? Sprouts : Plowed;
            double shade = fine * (0.92 + 0.16 * StyleRules.Noise(x * 0.6 + 11, z * 0.6 - 5));
            double t = Math.Min(1, edge / 0.4);
            for (int k = 0; k < 3; k++) c[k] += (color[k] * shade - c[k]) * t;
            // Entre deux rangs : la terre ; dans un labour, le creux du sillon.
            if (field.Crop == 2) { if (rowPhase < 0.35) Darken(c, 0.25 * t); }
            else if (rowPhase < 0.28) Blend(c, Plowed, 0.45 * t);
        }

        /// <summary>Route : terre battue (deux ornières sur un chemin assez large) ou pavés en rangs décalés, bord fondu ou net.</summary>
        private static void PaintRoad(double[] c, StyleRoad road, RoadHit hit, double x, double z, double fine)
        {
            if (road.Paved)
            {
                if (hit.Edge > 0.1) return;
                // Relecture : rangs de pavés dans le sens du tronçon, en coordonnées du monde (pas de motif en étoile aux bouts arrondis).
                double v = (-x * hit.DirZ + z * hit.DirX) / 0.34, u = (x * hit.DirX + z * hit.DirZ) / 0.46 + (Math.Floor(v) % 2 == 0 ? 0 : 0.5);
                double su = Math.Abs(u - Math.Round(u)), sv = Math.Abs(v - Math.Round(v));
                Set(c, Cobble, (0.82 + 0.3 * Hash((int)Math.Floor(u) * 31, (int)Math.Floor(v) * 17)) * fine);
                if (su < 0.08 || sv < 0.1) Darken(c, 0.38);
                if (hit.Edge > -0.25) Darken(c, 0.25);   // bordure
                return;
            }
            double wobble = 0.9 * (StyleRules.Noise(x * 0.45 + 31.7, z * 0.45 - 12.9) - 0.5);
            double w = Math.Max(0, Math.Min(1, 0.5 - (hit.Edge + wobble) / TownRules.RoadFade));
            if (w <= 0) return;
            for (int k = 0; k < 3; k++) c[k] += (Dirt[k] * fine * (0.94 + 0.12 * StyleRules.Noise(x * 1.7, z * 1.7)) - c[k]) * w;
            if (road.Width >= 3)
            {
                double rut = Math.Abs(Math.Abs(hit.Across) - road.Width * 0.22);
                if (rut < 0.22) Darken(c, 0.18 * w * (1 - rut / 0.22));
            }
        }

        /// <summary>Un bâtiment vu d'en haut : son toit, ou sa forme (puits, étal, tour, tente).</summary>
        private static void PaintBuilding(double[] c, Building b, double x, double z)
        {
            switch (b.Kind)
            {
                case StyleBuilding.House:
                case StyleBuilding.Inn:
                case StyleBuilding.Barn:
                case StyleBuilding.Chapel:
                {
                    if (x < b.RoofMinX || x > b.RoofMaxX || z < b.RoofMinZ || z > b.RoofMaxZ) return;
                    // Relecture : mêmes couvertures, cheminées et clocher que le décor (TownShapes).
                    StyleRoofLook look = TownShapes.RoofLookOf(b.Kind, b.Id);
                    double[] material = look == StyleRoofLook.Slate ? Slate : look == StyleRoofLook.Shingles ? Shingles : look == StyleRoofLook.DarkTiles ? TilesDark : Tiles;
                    GableRoof(c, x, z, b.RoofMinX, b.RoofMinZ, b.RoofMaxX, b.RoofMaxZ, material, b.Kind == StyleBuilding.Chapel ? 0.22 : 0.32, false);
                    if (TownShapes.ChimneyOf(b.Kind, b.Points, b.Id, b.Facing) is (double cx, double cz, double ch))
                    {
                        double ex = Math.Abs(x - cx), ez = Math.Abs(z - cz);
                        if (ex < ch + 0.07 && ez < ch + 0.07)
                        {
                            Set(c, Stone, 0.75 + 0.2 * StyleRules.Noise(x * 4, z * 4));
                            if (ex < ch - 0.1 && ez < ch - 0.1) Set(c, Well, 1);
                        }
                    }
                    if (TownShapes.BellTowerOf(b.Kind, b.Points, b.Facing) is (double tx, double tz, double th))
                    {
                        double dx = x - tx, dz = z - tz, side = th + 0.15;
                        if (Math.Abs(dx) < side && Math.Abs(dz) < side)
                        {
                            // Flèche à quatre pans : le pan nord ou ouest prend le soleil.
                            bool lit = Math.Abs(dz) > Math.Abs(dx) ? dz > 0 : dx < 0;
                            Set(c, Slate, lit ? 1.15 : 0.72);
                            if (Math.Abs(Math.Abs(dz) - Math.Abs(dx)) < 0.06) Darken(c, 0.3);
                            if (Math.Max(Math.Abs(dx), Math.Abs(dz)) > side - 0.08) Darken(c, 0.35);
                        }
                    }
                    return;
                }
                case StyleBuilding.Well:
                {
                    double dx = x - b.CenterX, dz = z - b.CenterZ, dist = Math.Sqrt(dx * dx + dz * dz), r = b.Radius * 0.95;
                    if (dist > r) return;
                    if (dist < r * 0.55) Set(c, Well, 1 + 0.4 * StyleRules.Noise(x * 6, z * 6));
                    else
                    {
                        Set(c, WallStone, 0.8 + 0.3 * StyleRules.Noise(x * 5, z * 5) + (dx < 0 && dz > 0 ? 0.1 : -0.05));
                        if (Math.Abs(dist - r * 0.55) < 0.05 || dist > r - 0.06) Darken(c, 0.35);
                    }
                    if (Math.Abs(dz) < 0.12 && Math.Abs(Math.Abs(dx) - r * 0.78) < 0.12) Set(c, Log, 0.7);   // poteaux
                    if (Math.Abs(dz) < 0.06 && Math.Abs(dx) < r * 0.78) Set(c, Log, 0.85);                   // traverse
                    return;
                }
                case StyleBuilding.Stall:
                {
                    if (x < b.RoofMinX || x > b.RoofMaxX || z < b.RoofMinZ || z > b.RoofMaxZ) return;
                    bool alongX = b.MaxX - b.MinX >= b.MaxZ - b.MinZ;
                    double stripe = (alongX ? x - b.RoofMinX : z - b.RoofMinZ) / 0.38;
                    double[] cloth = StyleRules.Variant(b.Id, 2) == 0 ? ClothRed : ClothBlue;
                    double front = alongX ? (b.Facing.Z >= 0 ? z - b.RoofMinZ : b.RoofMaxZ - z) / (b.RoofMaxZ - b.RoofMinZ)
                        : (b.Facing.X >= 0 ? x - b.RoofMinX : b.RoofMaxX - x) / (b.RoofMaxX - b.RoofMinX);
                    Set(c, ((int)Math.Floor(stripe) & 1) == 0 ? cloth : Cream, 0.78 + 0.3 * front);
                    if (Math.Min(Math.Min(x - b.RoofMinX, b.RoofMaxX - x), Math.Min(z - b.RoofMinZ, b.RoofMaxZ - z)) < 0.08) Darken(c, 0.35);
                    return;
                }
                case StyleBuilding.Tower:
                {
                    double dx = x - b.CenterX, dz = z - b.CenterZ, dist = Math.Sqrt(dx * dx + dz * dz), r = b.Radius * 0.97;
                    if (dist > r) return;
                    if (dist > r * 0.74)
                    {
                        double a = (Math.Atan2(dz, dx) + Math.PI) / (2 * Math.PI) * 16;
                        bool merlon = ((int)Math.Floor(a) & 1) == 0;
                        Set(c, WallStone, (merlon ? 1.05 : 0.7) + 0.15 * StyleRules.Noise(x * 5, z * 5) + (dx < 0 && dz > 0 ? 0.08 : -0.06));
                        if (dist > r - 0.07) Darken(c, 0.3);
                    }
                    else
                    {
                        double plank = (x - b.CenterX) / 0.3;
                        Set(c, Plank, 0.78 + 0.18 * Hash((int)Math.Floor(plank), 29));
                        if (Math.Abs(plank - Math.Round(plank)) < 0.1) Darken(c, 0.35);
                        if (Math.Abs(dx - r * 0.25) < 0.45 && Math.Abs(dz + r * 0.2) < 0.45) Set(c, Well, 1.4);   // trappe
                    }
                    return;
                }
                case StyleBuilding.Tent:
                {
                    double dx = x - b.CenterX, dz = z - b.CenterZ, dist = Math.Sqrt(dx * dx + dz * dz), r = b.Radius * 0.97;
                    if (dist > r) return;
                    double slope = 0.9;   // pente de la toile : normale penchée vers l'extérieur
                    double nx = dx / Math.Max(1e-6, dist) * slope, nz = dz / Math.Max(1e-6, dist) * slope;
                    double lambert = Math.Max(0, -0.55 * nx + 0.55 * nz + 0.63);
                    Set(c, StyleRules.Variant(b.Id, 3) == 0 ? CanvasDark : Canvas, 0.55 + 0.6 * lambert);
                    double a = (Math.Atan2(dz, dx) + Math.PI) / (2 * Math.PI) * 8;
                    if (Math.Abs(a - Math.Round(a)) * dist < 0.05) Darken(c, 0.3);   // coutures
                    if (dist < 0.12) Set(c, Log, 0.6);                              // mât
                    if (dist > r - 0.06) Darken(c, 0.3);
                    return;
                }
            }
        }

        /// <summary>Toit à deux pans sur [x0, x1] × [z0, z1] : faîte le long du grand côté, rangs de tuiles (ou de bardeaux), pan au nord éclairé.</summary>
        private static void GableRoof(double[] c, double x, double z, double x0, double z0, double x1, double z1, double[] material, double rowSize, bool thatch)
        {
            bool ridgeAlongX = x1 - x0 >= z1 - z0;
            double across = ridgeAlongX ? (z - (z0 + z1) / 2) / ((z1 - z0) / 2) : (x - (x0 + x1) / 2) / ((x1 - x0) / 2);
            double rowsAlong = (ridgeAlongX ? z - z0 : x - x0) / rowSize;
            double tileSeam = Math.Abs(rowsAlong - Math.Round(rowsAlong));
            double light = across > 0 ? 1.12 : 0.8;   // le pan au nord (ou à l'est… à l'ouest pour un faîte nord-sud) prend le soleil
            if (!ridgeAlongX) light = across < 0 ? 1.12 : 0.8;
            if (thatch)
            {
                Set(c, material, light * (0.85 + 0.25 * StyleRules.Noise((ridgeAlongX ? x : z) * 3.1, (ridgeAlongX ? z : x) * 0.6)));
                if (tileSeam < 0.05) Darken(c, 0.12);
            }
            else
            {
                Set(c, material, light * (0.92 + 0.12 * Hash((int)Math.Floor(rowsAlong), (int)Math.Floor((ridgeAlongX ? x : z) / 0.3))));
                if (tileSeam < 0.12) Darken(c, 0.3);
            }
            if (Math.Abs(across) < 0.06) Set(c, Log, 0.7);
            if (Math.Abs(across) > 0.95) Darken(c, 0.35);
        }

        private static readonly List<Blob> NoBlob = new List<Blob>();
        private static readonly List<Building> NoBuilding = new List<Building>();

        private static bool In(double x, double z, (double MinX, double MinZ, double MaxX, double MaxZ) b) => x >= b.MinX && x <= b.MaxX && z >= b.MinZ && z <= b.MaxZ;

        private static bool InCircle(double x, double z, (double MinX, double MinZ, double MaxX, double MaxZ) b, double scale)
        {
            double cx = (b.MinX + b.MaxX) / 2, cz = (b.MinZ + b.MaxZ) / 2, r = Math.Min(b.MaxX - b.MinX, b.MaxZ - b.MinZ) / 2 * scale;
            return (x - cx) * (x - cx) + (z - cz) * (z - cz) < r * r;
        }

        /// <summary>Distance (m) d'un point extérieur à l'aire.</summary>
        private static double Distance(double x, double z, AdventureMap map)
        {
            double dx = Math.Max(0, Math.Max(map.MinX - x, x - map.MaxX)), dz = Math.Max(0, Math.Max(map.MinZ - z, z - map.MaxZ));
            return Math.Sqrt(dx * dx + dz * dz);
        }

        private static double Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 2246822519u ^ (uint)b * 3266489917u;
                h ^= h >> 15;
                h *= 668265263u;
                h ^= h >> 13;
                return (h & 0xFFFF) / 65535.0;
            }
        }

        private static void Blend(double[] c, double[] to, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            for (int k = 0; k < 3; k++) c[k] += (to[k] - c[k]) * t;
        }

        private static void Set(double[] c, double[] to, double shade)
        {
            for (int k = 0; k < 3; k++) c[k] = to[k] * shade;
        }

        private static void Darken(double[] c, double amount)
        {
            for (int k = 0; k < 3; k++) c[k] *= 1 - Math.Max(0, Math.Min(1, amount));
        }

        private static void Lighten(double[] c, double amount)
        {
            for (int k = 0; k < 3; k++) c[k] += (1 - c[k]) * Math.Max(0, Math.Min(1, amount));
        }

        private static byte ToByte(double v) => (byte)Math.Max(0, Math.Min(255, Math.Round(v * 255)));
    }
}
