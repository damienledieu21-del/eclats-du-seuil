using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Space;

namespace EdS.Runtime
{
    /// <summary>
    /// V2.30.2 : un modèle de l'atelier du Bourg d'Aube fortifié posé par le jeu : pièce (fin du nom du FBX, « f01_palissade »), place du
    /// pivot (au sol), lacet (degrés, comme <c>Quaternion.Euler(0, yaw, 0)</c> d'Unity : le +z du modèle, sa face extérieure ou sa façade,
    /// va vers (sin, cos)) et obstacle qu'il habille (null : décor posé dans un passage libre, qu'il n'arrête pas).
    /// </summary>
    public readonly struct BourgPiece
    {
        public BourgPiece(string piece, double x, double z, double yaw, string? covers)
        {
            Piece = piece;
            X = x;
            Z = z;
            Yaw = yaw;
            Covers = covers;
        }

        public string Piece { get; }
        public double X { get; }
        public double Z { get; }
        public double Yaw { get; }
        public string? Covers { get; }
    }

    /// <summary>
    /// V2.30.2 : habillage du Bourg : les pièces à poser et, pour chaque obstacle habillé, les pièces sans lesquelles la vue garde la forme
    /// faite par le code (<see cref="Needs"/>).
    /// </summary>
    public sealed class BourgDress
    {
        public BourgDress(IReadOnlyList<BourgPiece> pieces, IReadOnlyDictionary<string, IReadOnlyList<string>> needs)
        {
            Pieces = pieces;
            Needs = needs;
        }

        public IReadOnlyList<BourgPiece> Pieces { get; }

        /// <summary>Obstacle habillé → pièces qu'il lui faut (toutes présentes : la forme faite par le code n'est pas posée).</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Needs { get; }

        public static readonly BourgDress Empty = new BourgDress(Array.Empty<BourgPiece>(), new Dictionary<string, IReadOnlyList<string>>());
    }

    /// <summary>
    /// V2.30.2 : le Bourg d'Aube fortifié (V2.30) habillé des modèles de l'atelier (<c>Eclats_du_Seuil_AssetLab/Bourg_Fortifie</c>, exports
    /// de jeu : LOD1 sur atlas, échelle 1, pivot au sol), rangés dans « Assets/EdS/Modeles/Decor/bourg_*/bourg_&lt;pièce&gt;.fbx ».
    /// Enceinte : palissade en modules de 6 m le long de chaque pan (<c>palissade_ville_*</c>), tours de flanquement sur les tours
    /// (<c>tour_ville_*</c>), portes dans les ouvertures entre deux pans, tours de guet sur la ronde. Affichage seulement : données de zone
    /// et moteur inchangés. Sans Unity.
    /// </summary>
    public static class BourgModels
    {
        // --- Gabarits des exports de jeu ------------------------------------------------------------------------------------------

        /// <summary>
        /// Boîte d'une pièce dans le repère de Blender du FBX de jeu (export_jeu.json, « boite ») : X (droite vue de dehors), Y (vers
        /// l'intérieur ou l'arrière), hauteur. Unity lit x = −X, z = −Y.
        /// </summary>
        public static (double X0, double X1, double Y0, double Y1, double Top)? Box(string piece)
        {
            switch (piece)
            {
                case Wall: return (-3.0, 3.0, -0.16, 2.14, 4.8);
                case WallStairs: return (-3.0, 3.0, -0.16, 6.24, 4.79);
                case WallBroken: return (-3.0, 3.0, -0.16, 2.14, 4.79);
                case WallMended: return (-3.0, 3.0, -0.16, 2.14, 4.8);
                case Walkway: return (-3.0, 3.0, -1.2, 1.1, 5.92);
                case WatchTower: return (-2.5, 2.5, -2.5, 2.5, 9.403);
                case FlankTower: return (-3.5, 3.5, -3.5, 3.5, 12.7);
                case EastGate: return (-6.0, 6.0, -1.15, 5.9, 10.5);
                case WestGate: return (-8.65, 8.9, -1.3, 10.0, 13.8);
                case NorthGate: return (-4.6, 4.6, -1.5, 4.9, 10.11);
                default: return null;
            }
        }

        public const string Wall = "f01_palissade";
        public const string WallStairs = "f03_palissade_escalier";
        public const string WallBroken = "f04_palissade_abimee";
        public const string WallMended = "f04_palissade_reparee";
        public const string Walkway = "f05_ronde_couverte";
        public const string WatchTower = "f06_tour_guet";
        public const string FlankTower = "f07_tour_flanquement";
        public const string EastGate = "f08_porte_est";
        public const string WestGate = "f09_porte_ouest";
        public const string NorthGate = "f10_porte_nord";

        /// <summary>Longueur d'un module de palissade (A → B, m).</summary>
        public const double ModuleLength = 6.0;

        /// <summary>Largeur libre du passage d'une porte (fiches F08 à F10), centré sur son pivot.</summary>
        public static double Passage(string piece) => piece == EastGate ? 4.2 : piece == WestGate ? 6.0 : piece == NorthGate ? 3.0 : 0.0;

        /// <summary>
        /// Enfoncement (m) d'un module de palissade ou d'une tour dans ce qui le borde : la palissade s'arrête 0,30 m dans le mur d'une tour
        /// ou d'une porte (pas de jour entre eux).
        /// </summary>
        public const double Tuck = 0.3;

        /// <summary>
        /// Décalage (m, vers l'intérieur) de la ronde couverte F05 par rapport au pivot du module F01 : origines de l'atelier Y 1,20 (F05)
        /// et Y 0,16 (F01).
        /// </summary>
        public const double WalkwayInset = 1.2 - 0.16;

        /// <summary>Recul (m, vers l'intérieur) du centre d'une tour de guet F06 par rapport à la ligne de la ronde (accroche A_ronde Y −1,50).</summary>
        public const double WatchTowerInset = 1.5;

        /// <summary>Rayon (m) du pied d'une pièce : la vue la pose au plus bas du sol affiché sous ce pied (V2.24).</summary>
        public static double Foot(string piece)
        {
            var b = Box(piece);
            if (b == null) return 0.5;
            var v = b.Value;
            return Math.Min(4.0, Math.Max(Math.Max(-v.X0, v.X1), Math.Max(-v.Y0, v.Y1)));
        }

        /// <summary>
        /// Rectangle (x0, x1, z0, z1) couvert au sol par la pièce posée en (<paramref name="x"/>, <paramref name="z"/>) au lacet
        /// <paramref name="yaw"/> (multiple de 90°).
        /// </summary>
        public static (double X0, double X1, double Z0, double Z1) Footprint(string piece, double x, double z, double yaw)
        {
            var b = Box(piece) ?? throw new ArgumentException("pièce inconnue : " + piece, nameof(piece));
            var corners = new[] { (b.X0, b.Y0), (b.X1, b.Y0), (b.X0, b.Y1), (b.X1, b.Y1) }.Select(c => Place(x, z, yaw, -c.Item1, -c.Item2)).ToList();
            return (corners.Min(c => c.X), corners.Max(c => c.X), corners.Min(c => c.Z), corners.Max(c => c.Z));
        }

        /// <summary>Point (<paramref name="lx"/>, <paramref name="lz"/>) du repère du modèle d'Unity posé en (x, z) au lacet <paramref name="yaw"/>.</summary>
        public static (double X, double Z) Place(double x, double z, double yaw, double lx, double lz)
        {
            double a = yaw * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a);
            return (Round(x + lx * cos + lz * sin), Round(z - lx * sin + lz * cos));
        }

        private static double Round(double v) => Math.Round(v, 6);

        // --- Pièces mobiles ouvertes ------------------------------------------------------------------------------------------------

        /// <summary>
        /// Pièce mobile d'une porte posée ouverte : objet du FBX (« Vantail_G »), charnière dans le repère du modèle d'Unity (x = −X,
        /// z = −Y des pivots de l'export de jeu), rotation autour de la verticale (degrés) et levée (m, la herse).
        /// </summary>
        public readonly struct OpenPart
        {
            public OpenPart(string name, double x, double z, double turn, double lift)
            {
                Name = name;
                X = x;
                Z = z;
                Turn = turn;
                Lift = lift;
            }

            public string Name { get; }
            public double X { get; }
            public double Z { get; }
            public double Turn { get; }
            public double Lift { get; }
        }

        /// <summary>Ouverture des vantaux (fiches F08 à F10 : 0 à 100°, vers la ville).</summary>
        public const double LeafOpen = 100;

        /// <summary>Course de la herse de la porte de l'Ouest (fiche F09 : 0 à 5,70 m).</summary>
        public const double PortcullisLift = 5.7;

        /// <summary>
        /// Pièces mobiles d'une porte, posées ouvertes (le passage des données est libre : les vantaux sont rabattus vers la ville, la herse
        /// levée) ; vide pour les autres pièces. Le vantail gauche (vu de dehors : X &lt; 0 dans Blender, x &gt; 0 dans Unity) tourne de −100°,
        /// le droit de +100° : leur bout libre part vers −z, l'intérieur.
        /// </summary>
        public static IReadOnlyList<OpenPart> OpenParts(string piece)
        {
            double hinge, depth;
            switch (piece)
            {
                case EastGate: hinge = 2.1; depth = 0.06; break;
                case WestGate: hinge = 3.0; depth = 0.31; break;
                case NorthGate: hinge = 1.5; depth = 0.06; break;
                default: return Array.Empty<OpenPart>();
            }
            var parts = new List<OpenPart>
            {
                new OpenPart("Vantail_G", hinge, -depth, -LeafOpen, 0),
                new OpenPart("Vantail_D", -hinge, -depth, LeafOpen, 0),
            };
            if (piece == WestGate) parts.Add(new OpenPart("Herse", 0, 0, 0, PortcullisLift));
            return parts;
        }

        // --- Habillage ---------------------------------------------------------------------------------------------------------------

        public static bool IsWall(string? id) => id != null && id.StartsWith("palissade_ville_", StringComparison.Ordinal);

        /// <summary>Tour de l'enceinte (le clocher de l'église n'en est pas une).</summary>
        public static bool IsTower(string? id) => id != null && id.StartsWith("tour_ville_", StringComparison.Ordinal) && id != "tour_ville_clocher";

        /// <summary>Habillage du Bourg pour les obstacles donnés (ceux d'une fenêtre ou de toute l'aire) ; vide sans palissade du Bourg.</summary>
        public static BourgDress Dress(IReadOnlyList<SpaceObstacle> obstacles)
        {
            var pieces = new List<BourgPiece>();
            var needs = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            Enceinte(obstacles, pieces, needs);
            if (pieces.Count == 0) return BourgDress.Empty;
            return new BourgDress(pieces, needs.ToDictionary(n => n.Key, n => (IReadOnlyList<string>)n.Value.Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal));
        }

        /// <summary>Un pan : axe (vrai : le long de x), coordonnée de sa ligne, étendue, face extérieure, identifiant.</summary>
        private sealed class Side
        {
            public bool AlongX;
            public double Line, From, To;
            public (int X, int Z) Out;
            public string Id = string.Empty;
        }

        private static (double X0, double X1, double Z0, double Z1) Bounds(SpaceObstacle o) =>
            (o.Points.Min(p => p.X.Raw) / 10000.0, o.Points.Max(p => p.X.Raw) / 10000.0, o.Points.Min(p => p.Z.Raw) / 10000.0, o.Points.Max(p => p.Z.Raw) / 10000.0);

        /// <summary>Lacet d'une face tournée vers <paramref name="facing"/> (quart de tour).</summary>
        private static double Yaw((int X, int Z) facing) => TownModels.FacingYaw(facing);

        private static void Need(Dictionary<string, List<string>> needs, string id, string piece)
        {
            if (!needs.TryGetValue(id, out List<string>? list)) needs[id] = list = new List<string>();
            list.Add(piece);
        }

        private static void Enceinte(IReadOnlyList<SpaceObstacle> obstacles, List<BourgPiece> pieces, Dictionary<string, List<string>> needs)
        {
            var walls = obstacles.Where(o => IsWall(o.Id)).OrderBy(o => o.Id, StringComparer.Ordinal).ToList();
            if (walls.Count == 0) return;
            // Centre de l'enceinte : celui des pans présents (la ville entière ou la fenêtre qui la voit).
            var all = walls.Select(Bounds).ToList();
            double cx = (all.Min(b => b.X0) + all.Max(b => b.X1)) / 2, cz = (all.Min(b => b.Z0) + all.Max(b => b.Z1)) / 2;
            var sides = new List<Side>();
            foreach (SpaceObstacle w in walls)
            {
                var b = Bounds(w);
                bool alongX = b.X1 - b.X0 >= b.Z1 - b.Z0;
                double line = alongX ? (b.Z0 + b.Z1) / 2 : (b.X0 + b.X1) / 2;
                double center = alongX ? cz : cx;
                int sign = line >= center ? 1 : -1;
                sides.Add(new Side
                {
                    AlongX = alongX, Line = line, From = alongX ? b.X0 : b.Z0, To = alongX ? b.X1 : b.Z1,
                    Out = alongX ? (0, sign) : (sign, 0), Id = w.Id,
                });
            }
            // Intervalles couverts le long de chaque ligne (tours, portes) : la palissade s'y arrête.
            var covered = new List<(Side Line, double A, double B)>();
            // Portes : l'ouverture entre deux pans de la même ligne (moins de 40 m).
            var gates = new List<(Side Line, double At, string Piece)>();
            foreach (var group in sides.GroupBy(s => (s.AlongX, Math.Round(s.Line, 1))))
            {
                var run = group.OrderBy(s => s.From).ToList();
                for (int i = 0; i + 1 < run.Count; i++)
                {
                    double gap = run[i + 1].From - run[i].To;
                    if (gap <= 0.5 || gap > 40) continue;
                    Side s = run[i];
                    string piece = s.Out == (1, 0) ? EastGate : s.Out == (-1, 0) ? WestGate : NorthGate;
                    double at = (run[i].To + run[i + 1].From) / 2;
                    gates.Add((s, at, piece));
                    (double x, double z) = s.AlongX ? (at, s.Line) : (s.Line, at);
                    double yaw = Yaw(s.Out);
                    pieces.Add(new BourgPiece(piece, x, z, yaw, null));
                    var f = Footprint(piece, x, z, yaw);
                    covered.Add((s, (s.AlongX ? f.X0 : f.Z0) + Tuck, (s.AlongX ? f.X1 : f.Z1) - Tuck));
                }
            }
            // Tours : sur la ligne d'un pan (ou au coin de deux) ; celles qu'une porte à tours (F09) recouvre restent sous elle.
            foreach (SpaceObstacle t in obstacles.Where(o => IsTower(o.Id)).OrderBy(o => o.Id, StringComparer.Ordinal))
            {
                var b = Bounds(t);
                double tx = (b.X0 + b.X1) / 2, tz = (b.Z0 + b.Z1) / 2, half = Math.Max(b.X1 - b.X0, b.Z1 - b.Z0) / 2;
                var on = sides.Where(s => Math.Abs((s.AlongX ? tz : tx) - s.Line) < 1.0 && (s.AlongX ? tx : tz) >= s.From - half - 1 && (s.AlongX ? tx : tz) <= s.To + half + 1).ToList();
                if (on.Count == 0) continue;
                var gate = gates.FirstOrDefault(g => g.Piece == WestGate && on.Any(s => s.AlongX == g.Line.AlongX && Math.Abs(s.Line - g.Line.Line) < 0.5)
                    && Math.Abs((g.Line.AlongX ? tx : tz) - g.At) < 15);
                if (gate.Piece != null)
                {
                    Need(needs, t.Id, gate.Piece);
                    continue;
                }
                // Coin : face vers le nord ou le sud (le pan le long de x) ; sinon la face extérieure de son pan.
                Side side = on.FirstOrDefault(s => s.AlongX) ?? on[0];
                string piece = half >= 3.4 ? FlankTower : WatchTower;
                pieces.Add(new BourgPiece(piece, tx, tz, Yaw(side.Out), t.Id));
                Need(needs, t.Id, piece);
                double reach = Box(piece)!.Value.X1 - Tuck;
                foreach (Side s in on) covered.Add((s, (s.AlongX ? tx : tz) - reach, (s.AlongX ? tx : tz) + reach));
            }
            // Palissade : chaque ligne d'un bout à l'autre (pans et ouvertures), moins ce que couvrent tours et portes, en modules de 6 m
            // au plus serrés (aucun jour) ; un module dans une ouverture (à côté d'une porte plus étroite qu'elle) n'habille aucun obstacle.
            foreach (var group in sides.GroupBy(s => (s.AlongX, Math.Round(s.Line, 1))).OrderBy(g => g.Key.AlongX).ThenBy(g => g.Key.Item2))
            {
                var run = group.OrderBy(s => s.From).ToList();
                Side first = run[0];
                double from = run.Min(s => s.From), to = run.Max(s => s.To);
                var cuts = covered.Where(c => c.Line.AlongX == first.AlongX && Math.Abs(c.Line.Line - first.Line) < 0.5).OrderBy(c => c.A).ToList();
                var free = new List<(double A, double B)>();
                double at = from;
                foreach (var c in cuts)
                {
                    if (c.A > at) free.Add((at, Math.Min(c.A, to)));
                    at = Math.Max(at, c.B);
                }
                if (at < to) free.Add((at, to));
                double yaw = Yaw(first.Out);
                int index = 0;
                foreach ((double a, double b) in free.Where(f => f.B - f.A > 0.5))
                {
                    int n = (int)Math.Ceiling((b - a) / ModuleLength - 1e-9);
                    double pitch = (b - a) / n;
                    for (int k = 0; k < n; k++)
                    {
                        // Les modules d'extrémité touchent le bord libre : centre à 3 m du bout, les autres au pas.
                        double c = n == 1 ? (a + b) / 2 : a + ModuleLength / 2 + k * (b - a - ModuleLength) / (n - 1);
                        (double x, double z) = first.AlongX ? (c, first.Line) : (first.Line, c);
                        Side? owner = run.FirstOrDefault(s => c >= s.From && c <= s.To);
                        string piece = WallPiece(first.Out, index++);
                        pieces.Add(new BourgPiece(piece, x, z, yaw, owner?.Id));
                        if (owner != null) Need(needs, owner.Id, piece);
                        // Ronde couverte (garde-corps et auvent) au-dessus des modules qui bordent une porte.
                        if (gates.Any(g => g.Line.AlongX == first.AlongX && Math.Abs(g.Line.Line - first.Line) < 0.5 && Math.Abs(g.At - c) < WalkwayReach))
                        {
                            (double wx, double wz) = (x - first.Out.X * WalkwayInset, z - first.Out.Z * WalkwayInset);
                            pieces.Add(new BourgPiece(Walkway, wx, wz, yaw, null));
                        }
                    }
                }
            }
            // Tours de guet sur la ronde (plan illustré : quatre), au milieu des plus longs pans de chaque côté sans porte ni tour proche.
            foreach (var group in sides.GroupBy(s => s.Out).OrderBy(g => g.Key.X).ThenBy(g => g.Key.Z))
            {
                Side longest = group.OrderByDescending(s => s.To - s.From).ThenBy(s => s.Id, StringComparer.Ordinal).First();
                if (longest.To - longest.From < 150) continue;
                double mid = (longest.From + longest.To) / 2;
                (double x, double z) = longest.AlongX ? (mid, longest.Line) : (longest.Line, mid);
                pieces.Add(new BourgPiece(WatchTower, x - longest.Out.X * WatchTowerInset, z - longest.Out.Z * WatchTowerInset, Yaw(longest.Out), null));
            }
        }

        /// <summary>Distance (m, le long du pan) entre le centre d'une porte et celui d'un module coiffé de la ronde couverte.</summary>
        public const double WalkwayReach = 20;

        /// <summary>Un escalier de ronde tous les <see cref="StairsEvery"/> modules (≈ 150 m).</summary>
        public const int StairsEvery = 25;

        /// <summary>
        /// Module du rang <paramref name="index"/> d'une ligne : un escalier tous les <see cref="StairsEvery"/>, sinon un module sur dix
        /// abîmé ou réparé (tirés du rang et du côté : toujours les mêmes), les autres droits.
        /// </summary>
        public static string WallPiece((int X, int Z) side, int index)
        {
            if (index % StairsEvery == StairsEvery / 2) return WallStairs;
            int v = StyleRules.Variant("bourg/palissade/" + side.X + "," + side.Z + "/" + index, 20);
            return v == 0 ? WallBroken : v == 1 ? WallMended : Wall;
        }
    }
}
