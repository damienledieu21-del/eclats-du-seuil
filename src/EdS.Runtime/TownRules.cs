using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Runtime
{
    /// <summary>
    /// Vallons agrandis (demande de Damien du 28 octobre 2026 : « un grand agrandissement, on pourrait mettre des bâtiments, des routes ») :
    /// ce qu'un obstacle de la zone représente à l'écran, d'après le début de son identifiant (le moteur n'y voit qu'un polygone solide).
    /// </summary>
    public enum StyleBuilding
    {
        None = 0,

        /// <summary>Maison (« maison… ») : murs crépis à colombages, toit de tuiles, cheminée.</summary>
        House = 1,

        /// <summary>Auberge (« auberge… ») : deux étages, enseigne.</summary>
        Inn = 2,

        /// <summary>Chapelle (« chapelle… ») : pierre, toit d'ardoises, clocher.</summary>
        Chapel = 3,

        /// <summary>Grange (« grange… ») : planches, grand portail, toit de bardeaux.</summary>
        Barn = 4,

        /// <summary>Puits (« puits… ») : margelle de pierre, petit toit sur deux poteaux.</summary>
        Well = 5,

        /// <summary>Étal du marché (« etal… ») : comptoir, poteaux, auvent de toile rayée.</summary>
        Stall = 6,

        /// <summary>Tour de guet (« tour… ») : tour ronde de pierre, créneaux.</summary>
        Tower = 7,

        /// <summary>Tente du camp (« tente… ») : toile sur un mât.</summary>
        Tent = 8,

        /// <summary>Bois (« bois… ») : massif d'arbres serrés qu'on ne traverse pas.</summary>
        Woods = 9,
    }

    /// <summary>Une route vue par l'affichage : ligne brisée (mètres), largeur, pavée ou de terre.</summary>
    public sealed class StyleRoad
    {
        public StyleRoad(string id, IReadOnlyList<(double X, double Z)> points, double width, bool paved)
        {
            if (points == null || points.Count < 2) throw new ArgumentException("au moins deux points", nameof(points));
            if (!(width > 0)) throw new ArgumentOutOfRangeException(nameof(width));
            Id = id ?? string.Empty;
            Points = points.ToArray();
            Width = width;
            Paved = paved;
            MinX = Points.Min(p => p.X) - width;
            MinZ = Points.Min(p => p.Z) - width;
            MaxX = Points.Max(p => p.X) + width;
            MaxZ = Points.Max(p => p.Z) + width;
        }

        public string Id { get; }
        public IReadOnlyList<(double X, double Z)> Points { get; }
        public double Width { get; }
        public bool Paved { get; }

        /// <summary>Boîte de la route, élargie d'une largeur de chaque côté.</summary>
        public double MinX { get; }
        public double MinZ { get; }
        public double MaxX { get; }
        public double MaxZ { get; }
    }

    /// <summary>
    /// Ce qu'un point du sol voit de la route la plus proche : <see cref="Edge"/> distance (m) au bord (négative sur la route),
    /// <see cref="Across"/> écart signé à l'axe, <see cref="Along"/> distance parcourue le long de la route depuis son premier point.
    /// </summary>
    public readonly struct RoadHit
    {
        public RoadHit(StyleRoad? road, double edge, double across, double along, double dirX, double dirZ)
        {
            Road = road;
            Edge = edge;
            Across = across;
            Along = along;
            DirX = dirX;
            DirZ = dirZ;
        }

        /// <summary>Route la plus proche, ou null (aucune route).</summary>
        public StyleRoad? Road { get; }
        public double Edge { get; }
        public double Across { get; }
        public double Along { get; }

        /// <summary>Sens du tronçon le plus proche (vecteur unitaire).</summary>
        public double DirX { get; }
        public double DirZ { get; }
    }

    /// <summary>Un champ cultivé : rectangle, sens des rangs, culture (0 blé mûr, 1 blé vert, 2 terre labourée).</summary>
    public readonly struct StyleField
    {
        public StyleField(double minX, double minZ, double maxX, double maxZ, bool rowsAlongX, int crop)
        {
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
            RowsAlongX = rowsAlongX;
            Crop = crop;
        }

        public double MinX { get; }
        public double MinZ { get; }
        public double MaxX { get; }
        public double MaxZ { get; }
        public bool RowsAlongX { get; }
        public int Crop { get; }
    }

    /// <summary>Un arbre d'un bois : place, taille relative (0,8 à 1,2), et s'il est au bord du massif (visible de l'aire).</summary>
    public readonly struct StyleWoodTree
    {
        public StyleWoodTree(double x, double z, double scale, bool edge)
        {
            X = x;
            Z = z;
            Scale = scale;
            Edge = edge;
        }

        public double X { get; }
        public double Z { get; }
        public double Scale { get; }
        public bool Edge { get; }
    }

    /// <summary>
    /// Vallons agrandis : les taches du sol rangées par cases de 8 m, pour ne passer à <see cref="StyleRules.GroundMix"/> que celles qui
    /// touchent un point (même résultat, bien plus vite : la haie de 94 m donne à elle seule une centaine de taches).
    /// </summary>
    public sealed class SpotGrid
    {
        private const double Cell = 8.0;
        private static readonly List<StyleSpot> None = new List<StyleSpot>();
        private readonly Dictionary<(int, int), List<StyleSpot>> _cells = new Dictionary<(int, int), List<StyleSpot>>();

        public SpotGrid(IEnumerable<StyleSpot> spots)
        {
            foreach (StyleSpot spot in spots ?? Array.Empty<StyleSpot>())
            {
                double reach = Math.Max(0, spot.Radius) + StyleRules.SpotFade;
                for (int gx = (int)Math.Floor((spot.X - reach) / Cell); gx <= (int)Math.Floor((spot.X + reach) / Cell); gx++)
                    for (int gz = (int)Math.Floor((spot.Z - reach) / Cell); gz <= (int)Math.Floor((spot.Z + reach) / Cell); gz++)
                    {
                        if (!_cells.TryGetValue((gx, gz), out List<StyleSpot>? list)) _cells[(gx, gz)] = list = new List<StyleSpot>();
                        list.Add(spot);
                    }
            }
        }

        /// <summary>Taches qui peuvent toucher (<paramref name="x"/>, <paramref name="z"/>).</summary>
        public IReadOnlyList<StyleSpot> Near(double x, double z) =>
            _cells.TryGetValue(((int)Math.Floor(x / Cell), (int)Math.Floor(z / Cell)), out List<StyleSpot>? list) ? list : None;

        /// <summary>Couches du sol en (<paramref name="x"/>, <paramref name="z"/>) : <see cref="StyleRules.GroundMix"/> des taches proches.</summary>
        public StyleMix Mix(double x, double z) => StyleRules.GroundMix(x, z, Near(x, z));
    }

    /// <summary>
    /// Vallons agrandis (28 octobre 2026) : règles d'affichage du bourg, des routes, des bois et des champs, communes à la carte peinte
    /// (<see cref="MapPainter"/>) et au décor de Unity. Sans Unity et sans hasard : les mêmes résultats à chaque lancement. Le moteur ne
    /// connaît que les obstacles ; routes et champs sont du décor (on y marche comme ailleurs).
    /// </summary>
    public static class TownRules
    {
        /// <summary>Distance (m) sur laquelle le bord d'un chemin de terre se fond dans l'herbe.</summary>
        public const double RoadFade = 1.2;

        /// <summary>Ce que représente l'obstacle <paramref name="id"/> (<see cref="StyleBuilding.None"/> pour les autres).</summary>
        public static StyleBuilding BuildingFor(string id)
        {
            string key = StyleRules.Key(id);
            if (key.StartsWith("maison", StringComparison.Ordinal)) return StyleBuilding.House;
            if (key.StartsWith("auberge", StringComparison.Ordinal)) return StyleBuilding.Inn;
            if (key.StartsWith("chapelle", StringComparison.Ordinal)) return StyleBuilding.Chapel;
            if (key.StartsWith("grange", StringComparison.Ordinal)) return StyleBuilding.Barn;
            if (key.StartsWith("puits", StringComparison.Ordinal)) return StyleBuilding.Well;
            if (key.StartsWith("etal", StringComparison.Ordinal)) return StyleBuilding.Stall;
            if (key.StartsWith("tour", StringComparison.Ordinal)) return StyleBuilding.Tower;
            if (key.StartsWith("tente", StringComparison.Ordinal)) return StyleBuilding.Tent;
            if (key.StartsWith("bois", StringComparison.Ordinal)) return StyleBuilding.Woods;
            return StyleBuilding.None;
        }

        /// <summary>
        /// Hauteur (m) des murs d'un bâtiment (sous le toit) ; 0 pour ce qui n'a pas de murs (bois, tente, étal, puits).
        /// </summary>
        public static double WallHeight(StyleBuilding kind)
        {
            switch (kind)
            {
                case StyleBuilding.House: return 4.2;
                case StyleBuilding.Inn: return 6.2;
                case StyleBuilding.Chapel: return 5.6;
                case StyleBuilding.Barn: return 4.6;
                case StyleBuilding.Tower: return 11.0;
                default: return 0;
            }
        }

        /// <summary>
        /// Couches du sol qu'appellent les obstacles (<see cref="StyleRules.GroundFor"/>, et la terre battue autour des bâtiments), une tache
        /// par obstacle. Revue des Vallons agrandis : un obstacle allongé (la haie de 94 m, une palissade) donne une rangée
        /// de taches le long de son grand côté, et non un seul disque de la taille de sa longueur qui aurait couvert le bourg de terre.
        /// </summary>
        public static List<StyleSpot> GroundSpots(IEnumerable<(string Id, double MinX, double MinZ, double MaxX, double MaxZ)> obstacles)
        {
            var spots = new List<StyleSpot>();
            foreach (var o in obstacles ?? Array.Empty<(string, double, double, double, double)>())
            {
                StyleGround? ground = StyleRules.GroundFor(o.Id);
                StyleBuilding building = BuildingFor(o.Id);
                // Terre battue autour des bâtiments ; pas sous un bois (son sous-bois est peint à part, sans auréole autour).
                if (ground is null && building != StyleBuilding.None && building != StyleBuilding.Woods) ground = StyleGround.Soil;
                if (!(ground is StyleGround g)) continue;
                double w = o.MaxX - o.MinX, d = o.MaxZ - o.MinZ, small = Math.Min(w, d), large = Math.Max(w, d);
                double pad = g == StyleGround.Rock ? 1.5 : building == StyleBuilding.None ? 1.0 : 0.6;
                int count = Math.Max(1, (int)Math.Ceiling(large / Math.Max(1.0, small)));
                bool alongX = w >= d;
                double radius = (count == 1 ? large : small) / 2 + pad;
                for (int i = 0; i < count; i++)
                {
                    double t = (i + 0.5) / count;
                    double x = alongX ? o.MinX + w * t : (o.MinX + o.MaxX) / 2, z = alongX ? (o.MinZ + o.MaxZ) / 2 : o.MinZ + d * t;
                    spots.Add(new StyleSpot(x, z, radius, g));
                }
            }
            return spots;
        }

        /// <summary>Route la plus proche du point (<see cref="RoadHit.Road"/> null s'il n'y a pas de route).</summary>
        public static RoadHit RoadAt(double x, double z, IReadOnlyList<StyleRoad> roads)
        {
            StyleRoad? best = null;
            double bestEdge = double.PositiveInfinity, bestAcross = 0, bestAlong = 0, bestDx = 1, bestDz = 0;
            if (roads == null) return new RoadHit(null, bestEdge, 0, 0, 1, 0);
            foreach (StyleRoad road in roads)
            {
                // Relecture des Vallons agrandis : une route n'est sautée que si sa boîte (élargie) est déjà plus loin que le meilleur bord
                // trouvé (l'écart à la boîte, moins la demi-largeur, ne dépasse jamais l'écart au bord) ; être hors de sa boîte ne suffit pas.
                double bx = Math.Max(0, Math.Max(road.MinX - x, x - road.MaxX)), bz = Math.Max(0, Math.Max(road.MinZ - z, z - road.MaxZ));
                if (Math.Sqrt(bx * bx + bz * bz) - road.Width / 2 >= bestEdge) continue;
                double walked = 0;
                for (int i = 0; i + 1 < road.Points.Count; i++)
                {
                    var a = road.Points[i];
                    var b = road.Points[i + 1];
                    double ex = b.X - a.X, ez = b.Z - a.Z, len = Math.Sqrt(ex * ex + ez * ez);
                    if (len < 1e-9) continue;
                    double ux = ex / len, uz = ez / len;
                    double t = Math.Max(0, Math.Min(len, (x - a.X) * ux + (z - a.Z) * uz));
                    double px = a.X + ux * t, pz = a.Z + uz * t;
                    double dist = Math.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz));
                    double edge = dist - road.Width / 2;
                    if (edge < bestEdge)
                    {
                        bestEdge = edge;
                        best = road;
                        bestAcross = ux * (z - a.Z) - uz * (x - a.X);   // à gauche du sens de la route : positif
                        bestAlong = walked + t;
                        bestDx = ux;
                        bestDz = uz;
                    }
                    walked += len;
                }
            }
            return new RoadHit(best, bestEdge, bestAcross, bestAlong, bestDx, bestDz);
        }

        /// <summary>
        /// Couches du sol sur une route et à son bord : terre battue sur un chemin (bord irrégulier, fondu sur <see cref="RoadFade"/> m) ;
        /// sous une rue pavée, de la terre aussi (les pavés sont posés dessus par le décor).
        /// </summary>
        public static StyleMix RoadMix(StyleMix mix, double x, double z, RoadHit hit)
        {
            if (hit.Road is null || hit.Edge > RoadFade + 0.6) return mix;
            double wobble = hit.Road.Paved ? 0 : 0.9 * (StyleRules.Noise(x * 0.45 + 31.7, z * 0.45 - 12.9) - 0.5);
            double w = Math.Max(0, Math.Min(1, 0.5 - (hit.Edge + wobble) / RoadFade));
            return new StyleMix(mix.Soil * (1 - w) + w, mix.Rock * (1 - w), mix.Grass * (1 - w));
        }

        /// <summary>
        /// Arbres d'un bois (polygone convexe, mètres) : d'abord une rangée serrée le long des côtés (lisière), à 1,2 m du bord ; puis le
        /// cœur, sur une grille décalée d'une quantité fixe tirée de <paramref name="id"/>, chaque arbre à plus de trois quarts de
        /// <paramref name="spacing"/> des autres. Les mêmes à chaque lancement.
        /// </summary>
        public static IReadOnlyList<StyleWoodTree> WoodTrees(IReadOnlyList<(double X, double Z)> polygon, string id, double spacing)
        {
            var trees = new List<StyleWoodTree>();
            if (polygon == null || polygon.Count < 3 || !(spacing >= 1)) return trees;
            double minX = polygon.Min(p => p.X), maxX = polygon.Max(p => p.X), minZ = polygon.Min(p => p.Z), maxZ = polygon.Max(p => p.Z);
            uint seed = (uint)StyleRules.Variant(id ?? string.Empty, int.MaxValue) | 1u;
            double Next()
            {
                seed ^= seed << 13;
                seed ^= seed >> 17;
                seed ^= seed << 5;
                return (seed >> 8) / 16777216.0;
            }
            // Lisière : le long de chaque côté, un arbre tous les 0,8 espacements.
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                double ex = b.X - a.X, ez = b.Z - a.Z, len = Math.Sqrt(ex * ex + ez * ez);
                int n = Math.Max(1, (int)Math.Floor(len / (spacing * 0.8)));
                for (int k = 0; k < n; k++)
                {
                    double t = (k + 0.5) / n;
                    double x = a.X + ex * t, z = a.Z + ez * t;
                    // Vers l'intérieur : le centre du polygone.
                    double cx = (minX + maxX) / 2 - x, cz = (minZ + maxZ) / 2 - z, cl = Math.Sqrt(cx * cx + cz * cz);
                    if (cl < 1e-9) continue;
                    x += cx / cl * 1.2;
                    z += cz / cl * 1.2;
                    double jitter = (Next() - 0.5) * spacing * 0.3;
                    x += ex / len * jitter;
                    z += ez / len * jitter;
                    if (StyleRules.InsideDistance(x, z, polygon) < 0.8) continue;
                    trees.Add(new StyleWoodTree(x, z, 0.8 + 0.4 * Next(), true));
                }
            }
            // Cœur du bois : grille décalée ; un arbre n'est posé qu'à plus de trois quarts d'espacement de ceux déjà posés (lisière comprise),
            // pour que le massif soit plein sans trou entre la lisière et le cœur.
            double near2 = spacing * 0.75 * spacing * 0.75;
            for (double gz = minZ + spacing / 2; gz < maxZ; gz += spacing * 0.8)
                for (double gx = minX + spacing / 2; gx < maxX; gx += spacing * 0.8)
                {
                    double x = gx + (Next() - 0.5) * spacing * 0.5, z = gz + (Next() - 0.5) * spacing * 0.5;
                    double scale = 0.8 + 0.4 * Next();
                    if (StyleRules.InsideDistance(x, z, polygon) < 1.5) continue;
                    if (trees.Any(t => (t.X - x) * (t.X - x) + (t.Z - z) * (t.Z - z) < near2)) continue;
                    trees.Add(new StyleWoodTree(x, z, scale, false));
                }
            return trees;
        }

        /// <summary>
        /// Champs cultivés dans un lieu (rectangle [minX, maxX] × [minZ, maxZ]) : des parcelles posées du sud-ouest au nord-est, chacune la
        /// plus grande qui tient (16 × 10 m, puis 12 × 8, puis 8 × 6, dans un sens ou l'autre), à 2 m au moins des autres (les sentiers),
        /// d'un obstacle, du bord du lieu et de l'aire, à 1 m du bord d'une route et à 5 m d'une place à garder dégagée
        /// (<paramref name="keepOut"/> : PNJ, points sûrs…). Culture et sens des rangs fixes d'après la place de la parcelle.
        /// </summary>
        public static IReadOnlyList<StyleField> Fields(double minX, double minZ, double maxX, double maxZ,
            IReadOnlyList<(double MinX, double MinZ, double MaxX, double MaxZ)> obstacles, IReadOnlyList<StyleRoad> roads,
            IReadOnlyList<(double X, double Z)> keepOut, (double MinX, double MinZ, double MaxX, double MaxZ) area)
        {
            const double Gap = 2.0;
            (double W, double D)[] sizes = { (16, 10), (10, 16), (12, 8), (8, 12), (8, 6), (6, 8) };
            var fields = new List<StyleField>();
            double x0 = Math.Max(minX, area.MinX) + Gap, z0 = Math.Max(minZ, area.MinZ) + Gap;
            double x1 = Math.Min(maxX, area.MaxX) - Gap, z1 = Math.Min(maxZ, area.MaxZ) - Gap;
            bool Fits(double fx0, double fz0, double fx1, double fz1)
            {
                if (fx1 > x1 || fz1 > z1) return false;
                foreach (StyleField f in fields)
                    if (fx0 < f.MaxX + Gap && fx1 > f.MinX - Gap && fz0 < f.MaxZ + Gap && fz1 > f.MinZ - Gap) return false;
                if (obstacles != null)
                    foreach (var o in obstacles)
                        if (fx0 < o.MaxX + Gap && fx1 > o.MinX - Gap && fz0 < o.MaxZ + Gap && fz1 > o.MinZ - Gap) return false;
                if (keepOut != null)
                    foreach (var p in keepOut)
                    {
                        double dx = Math.Max(Math.Max(fx0 - p.X, p.X - fx1), 0), dz = Math.Max(Math.Max(fz0 - p.Z, p.Z - fz1), 0);
                        if (dx * dx + dz * dz < 25) return false;
                    }
                int nx = (int)Math.Ceiling((fx1 - fx0) / 1.5), nz = (int)Math.Ceiling((fz1 - fz0) / 1.5);
                for (int j = 0; j <= nz; j++)
                    for (int i = 0; i <= nx; i++)
                        if (RoadAt(fx0 + (fx1 - fx0) * i / nx, fz0 + (fz1 - fz0) * j / nz, roads).Edge < 1.0) return false;
                return true;
            }
            for (double z = z0; z < z1; z += 1)
                for (double x = x0; x < x1; x += 1)
                    foreach ((double w, double d) in sizes)
                    {
                        if (!Fits(x, z, x + w, z + d)) continue;
                        string key = FormattableString.Invariant($"champ{Math.Round(x)}_{Math.Round(z)}");
                        fields.Add(new StyleField(x, z, x + w, z + d, StyleRules.Variant(key, 2) == 0, StyleRules.Variant(key + "culture", 5) switch { 0 => 1, 1 => 2, _ => 0 }));
                        x += w + Gap - 1;
                        break;
                    }
            return fields;
        }

        /// <summary>
        /// Côté d'un bâtiment (boîte) qui fait face à la route la plus proche, en vecteur unitaire le long d'un axe (porte et enseigne) ;
        /// vers le sud s'il n'y a pas de route.
        /// </summary>
        public static (int X, int Z) Facing(double minX, double minZ, double maxX, double maxZ, IReadOnlyList<StyleRoad> roads)
        {
            double cx = (minX + maxX) / 2, cz = (minZ + maxZ) / 2;
            (int, int)[] sides = { (0, -1), (0, 1), (-1, 0), (1, 0) };
            (int X, int Z) best = (0, -1);
            double bestEdge = double.PositiveInfinity;
            if (roads == null || roads.Count == 0) return best;
            foreach (var (sx, sz) in sides)
            {
                double px = sx == 0 ? cx : sx < 0 ? minX - 1 : maxX + 1, pz = sz == 0 ? cz : sz < 0 ? minZ - 1 : maxZ + 1;
                double edge = RoadAt(px, pz, roads).Edge;
                if (edge < bestEdge - 1e-9)
                {
                    bestEdge = edge;
                    best = (sx, sz);
                }
            }
            return best;
        }

        /// <summary>Le lieu <paramref name="placeId"/> est-il habité (bourg, village, hameau) ? Pour la musique et l'ambiance.</summary>
        public static bool Inhabited(string placeId)
        {
            string key = StyleRules.Key(placeId);
            return key.Contains("village") || key.Contains("bourg") || key.Contains("hameau");
        }

        /// <summary>Le lieu <paramref name="placeId"/> porte-t-il des champs cultivés ?</summary>
        public static bool Farmland(string placeId) => StyleRules.Key(placeId).Contains("champs");
    }
}
