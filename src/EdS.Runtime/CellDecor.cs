using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Zones;
using EdS.Domain.Space;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.28d (1) : un arbre du décor d'une case (nappe de forêt ou bois), avec son échelle.</summary>
    public readonly struct CellTree
    {
        public CellTree(double x, double z, double scale, bool wolves)
        {
            X = x;
            Z = z;
            Scale = scale;
            Wolves = wolves;
        }

        public double X { get; }
        public double Z { get; }
        public double Scale { get; }

        /// <summary>Arbre de la forêt des Loups (épicéas, pins) plutôt qu'un feuillu.</summary>
        public bool Wolves { get; }
    }

    /// <summary>Sous-lot V2.28d (1) : un rocher d'une paroi (éboulement, falaise) dans une case.</summary>
    public readonly struct CellRock
    {
        public CellRock(string id, double x, double z, double size)
        {
            Id = id;
            X = x;
            Z = z;
            Size = size;
        }

        /// <summary>Identifiant stable (« obstacle/rang ») : même modèle et même variante d'une case à l'autre.</summary>
        public string Id { get; }
        public double X { get; }
        public double Z { get; }
        public double Size { get; }
    }

    /// <summary>Sous-lot V2.28d (1) : un morceau de route dans une case (ligne brisée coupée à la case).</summary>
    public sealed class CellRoad
    {
        public CellRoad(string id, double width, bool paved, IReadOnlyList<(double X, double Z)> points)
        {
            Id = id;
            Width = width;
            Paved = paved;
            Points = points;
        }

        public string Id { get; }
        public double Width { get; }
        public bool Paved { get; }
        public IReadOnlyList<(double X, double Z)> Points { get; }
    }

    /// <summary>Sous-lot V2.28d (1) : le décor d'une case de la région, coupé à elle (et hors du sol déjà couvert par le décor de la zone).</summary>
    public sealed class CellDecorSet
    {
        internal CellDecorSet(RegionCell cell, double minX, double minZ, double maxX, double maxZ, List<IReadOnlyList<(double X, double Z)>> water,
            List<IReadOnlyList<(double X, double Z)>> ravines, List<CellRoad> roads, List<CellRock> rocks, List<CellTree> trees)
        {
            Cell = cell;
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
            Water = water;
            Ravines = ravines;
            Roads = roads;
            Rocks = rocks;
            Trees = trees;
        }

        public RegionCell Cell { get; }
        public double MinX { get; }
        public double MinZ { get; }
        public double MaxX { get; }
        public double MaxZ { get; }

        /// <summary>Eau (rivières, lac) : polygones convexes coupés à la case.</summary>
        public IReadOnlyList<IReadOnlyList<(double X, double Z)>> Water { get; }

        /// <summary>Gorge et ravins (<see cref="StyleRules.Ravine"/>) : polygones convexes coupés à la case.</summary>
        public IReadOnlyList<IReadOnlyList<(double X, double Z)>> Ravines { get; }

        public IReadOnlyList<CellRoad> Roads { get; }
        public IReadOnlyList<CellRock> Rocks { get; }
        public IReadOnlyList<CellTree> Trees { get; }

        public bool IsEmpty => Water.Count == 0 && Ravines.Count == 0 && Roads.Count == 0 && Rocks.Count == 0 && Trees.Count == 0;
    }

    /// <summary>
    /// Sous-lot V2.28d (1) (docs/PLAN_GRANDE_CARTE.md, § 6.1) : le décor de la région construit par cases, comme son terrain (V2.27) :
    /// eau des rivières et du lac, gorge et ravins, routes, parois de rochers, nappes de forêt (lieux « foret… ») et bois. Tout est tiré de la
    /// carte de l'aire entière, coupé à la case et au-dehors du sol déjà couvert par le décor de la zone (<c>hole</c> : la fenêtre que
    /// <c>SpaceView</c> construit ; sauf les nappes des lieux de forêt qui débordent la fenêtre, que seule la case construit : revue Codex de la PR #173) : d'une case à l'autre, rien n'est en double ni ne manque (bords partagés, grille des arbres fixée au monde).
    /// Au-delà de <see cref="MaxTreesPerCell"/> arbres, la case garde ceux dont la clé de tirage (fixée au monde) est la plus petite : un
    /// sous-échantillonnage uniforme et déterministe.
    /// Sans Unity ; affichage seulement (le moteur ne voit que les obstacles).
    /// </summary>
    public static class CellDecor
    {
        /// <summary>Écart (m) des arbres d'une nappe de forêt (grille du monde, décalée au hasard dans sa maille).</summary>
        public const double ForestSpacing = 7.0;

        /// <summary>Au plus autant d'arbres par case (128 × 128 m à 7 m : 334 places ; bois serrés en plus).</summary>
        public const int MaxTreesPerCell = 1200;

        /// <summary>Écart gardé (m) entre un arbre et le bord d'une route, et autour de l'eau et des obstacles.</summary>
        public const double RoadGap = 2.5, ObstacleGap = 1.5;

        /// <summary>Le décor de la case <paramref name="cell"/> de <paramref name="grid"/>, d'après la carte de l'aire entière.</summary>
        public static CellDecorSet For(AdventureMap whole, RegionGrid grid, RegionCell cell, (double MinX, double MinZ, double MaxX, double MaxZ)? hole = null,
            int maxTrees = MaxTreesPerCell)
        {
            if (maxTrees < 0) throw new ArgumentOutOfRangeException(nameof(maxTrees));
            if (whole is null) throw new ArgumentNullException(nameof(whole));
            if (grid is null) throw new ArgumentNullException(nameof(grid));
            if (cell.Column < 0 || cell.Column >= grid.Columns || cell.Row < 0 || cell.Row >= grid.Rows) throw new ArgumentOutOfRangeException(nameof(cell));
            double x0 = grid.OriginX + (double)cell.Column * RegionGrid.CellMeters, z0 = grid.OriginZ + (double)cell.Row * RegionGrid.CellMeters;
            double x1 = x0 + RegionGrid.CellMeters, z1 = z0 + RegionGrid.CellMeters;
            List<(double MinX, double MinZ, double MaxX, double MaxZ)> rects = Outside((x0, z0, x1, z1), hole);
            var water = new List<IReadOnlyList<(double X, double Z)>>();
            var ravines = new List<IReadOnlyList<(double X, double Z)>>();
            var roads = new List<CellRoad>();
            var rocks = new List<CellRock>();
            var trees = new List<CellTree>();

            // Obstacles qui touchent la case (boîtes, pour les arbres).
            // Relecture de la PR #173 : « est un bois » calculé une fois par obstacle (pas pour chaque arbre candidat).
            var near = new List<(string Id, bool Low, bool Wood, IReadOnlyList<(double X, double Z)> Points, double MinX, double MinZ, double MaxX, double MaxZ)>();
            foreach (SpaceObstacle o in whole.Obstacles)
            {
                var pts = o.Points.Select(p => (X: p.X.Raw / 10000.0, Z: p.Z.Raw / 10000.0)).ToList();
                double bx0 = pts.Min(p => p.X), bz0 = pts.Min(p => p.Z), bx1 = pts.Max(p => p.X), bz1 = pts.Max(p => p.Z);
                if (bx1 < x0 - ObstacleGap || bx0 > x1 + ObstacleGap || bz1 < z0 - ObstacleGap || bz0 > z1 + ObstacleGap) continue;
                near.Add((o.Id, o.Low, TownRules.BuildingFor(o.Id) == StyleBuilding.Woods, pts, bx0, bz0, bx1, bz1));
            }
            foreach (var o in near)
            {
                string key = StyleRules.Key(o.Id);
                if (o.Low && key.StartsWith("riviere", StringComparison.Ordinal))
                    foreach (var r in rects) AddClipped(water, o.Points, r);
                else if (StyleRules.Ravine(o.Id))
                    foreach (var r in rects) AddClipped(ravines, o.Points, r);
                else if (StyleRules.RockFace(o.Id))
                    foreach (var spot in StyleRules.RockFaceSpots(o.Points, x0, z0, x1, z1, 0))
                        if (Owns(spot.X, spot.Z, x0, z0, x1, z1) && InAny(rects, spot.X, spot.Z))
                            rocks.Add(new CellRock(o.Id + "/" + spot.Index, spot.X, spot.Z, spot.Depth));
            }
            foreach (StyleRoad road in whole.Roads)
                foreach (var r in rects)
                    foreach (List<(double X, double Z)> piece in ClipPolyline(road.Points, r))
                        roads.Add(new CellRoad(road.Id, road.Width, road.Paved, piece));

            // Arbres : bois (obstacles « bois… », leurs arbres comme dans la vue de la zone), puis nappes des lieux de forêt.
            // Revue Codex de la PR #173 (3e tour) : les nappes des lieux de forêt ne sont construites que par les cases (SpaceView ne les fait
            // pas dans l'aire entière) : elles ignorent le sol couvert par la fenêtre de la zone (anyHole), les bois et le reste non.
            bool Free(double x, double z, bool anyHole = false)
            {
                if ((!anyHole && !InAny(rects, x, z)) || !Owns(x, z, x0, z0, x1, z1)) return false;
                if (TownRules.RoadAt(x, z, whole.Roads).Edge < RoadGap) return false;
                foreach (var o in near)
                {
                    if (o.Wood) continue;
                    if (x < o.MinX - ObstacleGap || x > o.MaxX + ObstacleGap || z < o.MinZ - ObstacleGap || z > o.MaxZ + ObstacleGap) continue;
                    if (StyleRules.InsideDistance(x, z, o.Points) > -ObstacleGap) return false;
                }
                return true;
            }
            // Candidats avec leur clé de tirage (hachage de la place, fixé au monde) : relecture de la PR #173, le plafond garde les plus petites
            // clés, réparties uniformément dans la case (et non les premiers obstacles), même résultat quel que soit l'ordre de chargement.
            var candidates = new List<(uint Key, CellTree Tree)>();
            var seen = new HashSet<(long, long)>();
            foreach (var wood in near.Where(o => o.Wood))
            {
                bool wolves = WolfPlace(whole, (wood.MinX + wood.MaxX) / 2, (wood.MinZ + wood.MaxZ) / 2);
                foreach (StyleWoodTree t in TownRules.WoodTrees(wood.Points, wood.Id, MapPainter.WoodSpacing))
                {
                    // Dédoublonnage au décimètre près (deux bois qui se touchent).
                    if (!seen.Add(((long)Math.Round(t.X * 10), (long)Math.Round(t.Z * 10))) || !Free(t.X, t.Z)) continue;
                    candidates.Add((Hash((long)Math.Round(t.X * 10), (long)Math.Round(t.Z * 10)), new CellTree(t.X, t.Z, t.Scale, wolves)));
                }
            }
            // Nappes : une seule grille du monde pour tous les lieux de forêt (relecture de la PR #173 : deux lieux qui se chevauchent ne
            // doublent pas leurs arbres) ; un point dans un lieu de la forêt des Loups y prend ses essences.
            var forests = whole.Places.Where(p => StyleRules.Key(p.Id).Contains("foret") && p.MaxX > x0 && p.MinX < x1 && p.MaxZ > z0 && p.MinZ < z1).ToList();
            if (forests.Count > 0)
            {
                long c0 = (long)Math.Floor(x0 / ForestSpacing) - 1, c1 = (long)Math.Ceiling(x1 / ForestSpacing);
                long r0 = (long)Math.Floor(z0 / ForestSpacing) - 1, r1 = (long)Math.Ceiling(z1 / ForestSpacing);
                for (long r = r0; r <= r1; r++)
                    for (long c = c0; c <= c1; c++)
                    {
                        uint h = Hash(c, r);
                        // Clairières : une maille sur six reste vide.
                        if (h % 6u == 0u) continue;
                        double x = (c + 0.15 + 0.7 * ((h >> 3) % 1000u) / 1000.0) * ForestSpacing;
                        double z = (r + 0.15 + 0.7 * ((h >> 13) % 1000u) / 1000.0) * ForestSpacing;
                        MapPlace? inside = null;
                        bool wolves = false, beyond = false;
                        foreach (MapPlace p in forests)
                            if (x >= p.MinX && x <= p.MaxX && z >= p.MinZ && z <= p.MaxZ)
                            {
                                inside ??= p;
                                wolves |= StyleRules.Key(p.Id).Contains("loup");
                                beyond |= !Within(p, hole);
                            }
                        // Sous la fenêtre de la zone, seulement les nappes d'un lieu qui la déborde (les grandes forêts de la région) : un lieu tout
                        // entier dans la fenêtre (la forêt des Loups du cœur) garde le décor de la vue de la zone, comme avant.
                        if (inside is null || !Free(x, z, anyHole: beyond)) continue;
                        // Un bois de la zone a déjà ses arbres : pas de nappe par-dessus.
                        if (near.Any(o => o.Wood && x >= o.MinX && x <= o.MaxX && z >= o.MinZ && z <= o.MaxZ && StyleRules.InsideDistance(x, z, o.Points) > 0)) continue;
                        candidates.Add((h, new CellTree(x, z, 0.8 + 0.5 * ((h >> 23) % 100u) / 100.0, wolves)));
                    }
            }
            if (candidates.Count > maxTrees)
                candidates = candidates.OrderBy(t => t.Key).ThenBy(t => t.Tree.X).ThenBy(t => t.Tree.Z).Take(maxTrees).ToList();
            trees.AddRange(candidates.Select(t => t.Tree));
            return new CellDecorSet(cell, x0, z0, x1, z1, water, ravines, roads, rocks, trees);
        }

        /// <summary>Le lieu tient-il tout entier dans la fenêtre <paramref name="hole"/> (faux sans fenêtre) ?</summary>
        private static bool Within(MapPlace p, (double MinX, double MinZ, double MaxX, double MaxZ)? hole) =>
            hole is (double hx0, double hz0, double hx1, double hz1) && p.MinX >= hx0 && p.MaxX <= hx1 && p.MinZ >= hz0 && p.MaxZ <= hz1;

        /// <summary>Le point appartient-il à la case (bord ouest et sud compris, est et nord exclus : chaque point dans une seule case) ?</summary>
        private static bool Owns(double x, double z, double x0, double z0, double x1, double z1) => x >= x0 && x < x1 && z >= z0 && z < z1;

        private static bool InAny(List<(double MinX, double MinZ, double MaxX, double MaxZ)> rects, double x, double z)
        {
            foreach (var r in rects)
                if (x >= r.MinX && x <= r.MaxX && z >= r.MinZ && z <= r.MaxZ) return true;
            return false;
        }

        private static bool WolfPlace(AdventureMap map, double x, double z) =>
            map.Places.Any(p => StyleRules.Key(p.Id).Contains("foret") && StyleRules.Key(p.Id).Contains("loup") && x >= p.MinX && x <= p.MaxX && z >= p.MinZ && z <= p.MaxZ);

        private static uint Hash(long c, long r)
        {
            unchecked
            {
                uint h = (uint)(c * 73856093L) ^ (uint)(r * 19349663L) ^ 0x9E3779B9u;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>
        /// La case moins le sol déjà couvert (<paramref name="hole"/>) : au plus quatre rectangles (bandes sud et nord, puis ouest et est), sans
        /// chevauchement ; la case entière sans trou ; aucun si le trou la couvre.
        /// </summary>
        public static List<(double MinX, double MinZ, double MaxX, double MaxZ)> Outside((double MinX, double MinZ, double MaxX, double MaxZ) cell,
            (double MinX, double MinZ, double MaxX, double MaxZ)? hole)
        {
            var result = new List<(double, double, double, double)>();
            if (hole is not (double hx0, double hz0, double hx1, double hz1) || hx1 <= cell.MinX || hx0 >= cell.MaxX || hz1 <= cell.MinZ || hz0 >= cell.MaxZ)
            {
                result.Add(cell);
                return result;
            }
            double ix0 = Math.Max(cell.MinX, hx0), iz0 = Math.Max(cell.MinZ, hz0), ix1 = Math.Min(cell.MaxX, hx1), iz1 = Math.Min(cell.MaxZ, hz1);
            if (iz0 > cell.MinZ) result.Add((cell.MinX, cell.MinZ, cell.MaxX, iz0));
            if (iz1 < cell.MaxZ) result.Add((cell.MinX, iz1, cell.MaxX, cell.MaxZ));
            if (ix0 > cell.MinX) result.Add((cell.MinX, iz0, ix0, iz1));
            if (ix1 < cell.MaxX) result.Add((ix1, iz0, cell.MaxX, iz1));
            return result;
        }

        private static void AddClipped(List<IReadOnlyList<(double X, double Z)>> into, IReadOnlyList<(double X, double Z)> polygon,
            (double MinX, double MinZ, double MaxX, double MaxZ) rect)
        {
            List<(double X, double Z)> clipped = ClipPolygon(polygon, rect);
            if (clipped.Count >= 3 && Area(clipped) > 1e-6) into.Add(clipped);
        }

        /// <summary>Polygone convexe coupé à un rectangle (Sutherland–Hodgman) ; vide s'il est dehors.</summary>
        public static List<(double X, double Z)> ClipPolygon(IReadOnlyList<(double X, double Z)> polygon, (double MinX, double MinZ, double MaxX, double MaxZ) rect)
        {
            var output = polygon.ToList();
            foreach (int edge in new[] { 0, 1, 2, 3 })
            {
                if (output.Count == 0) break;
                var input = output;
                output = new List<(double X, double Z)>();
                bool Inside((double X, double Z) p) => edge switch
                {
                    0 => p.X >= rect.MinX,
                    1 => p.X <= rect.MaxX,
                    2 => p.Z >= rect.MinZ,
                    _ => p.Z <= rect.MaxZ,
                };
                (double X, double Z) Cross((double X, double Z) a, (double X, double Z) b)
                {
                    double t = edge switch
                    {
                        0 => (rect.MinX - a.X) / (b.X - a.X),
                        1 => (rect.MaxX - a.X) / (b.X - a.X),
                        2 => (rect.MinZ - a.Z) / (b.Z - a.Z),
                        _ => (rect.MaxZ - a.Z) / (b.Z - a.Z),
                    };
                    return (a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t);
                }
                for (int i = 0; i < input.Count; i++)
                {
                    var current = input[i];
                    var previous = input[(i + input.Count - 1) % input.Count];
                    bool ci = Inside(current), pi = Inside(previous);
                    if (ci)
                    {
                        if (!pi) output.Add(Cross(previous, current));
                        output.Add(current);
                    }
                    else if (pi) output.Add(Cross(previous, current));
                }
            }
            return output;
        }

        /// <summary>Ligne brisée coupée à un rectangle (Liang–Barsky par segment) : les morceaux à l'intérieur, raccordés quand ils se suivent.</summary>
        public static List<List<(double X, double Z)>> ClipPolyline(IReadOnlyList<(double X, double Z)> line, (double MinX, double MinZ, double MaxX, double MaxZ) rect)
        {
            var pieces = new List<List<(double X, double Z)>>();
            List<(double X, double Z)>? current = null;
            for (int i = 0; i + 1 < line.Count; i++)
            {
                var a = line[i];
                var b = line[i + 1];
                double dx = b.X - a.X, dz = b.Z - a.Z, t0 = 0, t1 = 1;
                bool keep = true;
                foreach ((double p, double q) in new[] { (-dx, a.X - rect.MinX), (dx, rect.MaxX - a.X), (-dz, a.Z - rect.MinZ), (dz, rect.MaxZ - a.Z) })
                {
                    if (Math.Abs(p) < 1e-12)
                    {
                        if (q < 0) { keep = false; break; }
                        continue;
                    }
                    double t = q / p;
                    if (p < 0) t0 = Math.Max(t0, t);
                    else t1 = Math.Min(t1, t);
                    if (t0 > t1) { keep = false; break; }
                }
                if (!keep || t1 - t0 < 1e-12)
                {
                    current = null;
                    continue;
                }
                var s = (a.X + dx * t0, a.Z + dz * t0);
                var e = (a.X + dx * t1, a.Z + dz * t1);
                if (current != null && t0 <= 1e-12) current.Add(e);
                else
                {
                    current = new List<(double X, double Z)> { s, e };
                    pieces.Add(current);
                }
                if (t1 < 1 - 1e-12) current = null;
            }
            return pieces;
        }

        private static double Area(IReadOnlyList<(double X, double Z)> p)
        {
            double a = 0;
            for (int i = 0; i < p.Count; i++)
            {
                var u = p[i];
                var v = p[(i + 1) % p.Count];
                a += u.X * v.Z - v.X * u.Z;
            }
            return Math.Abs(a) / 2;
        }
    }
}
