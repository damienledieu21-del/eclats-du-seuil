using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Space;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.24 (décision de Damien du 11 novembre 2026) : réglages du relief affiché. Valeurs provisoires, réglables ; elles ne
    /// dépendent pas de la taille d'une aire (une zone plus grande, découpée en cases, garde les mêmes réglages : le bruit des vallons est en
    /// coordonnées du monde, donc continu d'une case à l'autre).
    /// </summary>
    public sealed class ReliefSettings
    {
        /// <summary>Amplitude des vallons (m) : le sol ondule entre 0 et cette hauteur en rase campagne.</summary>
        public double Amplitude { get; set; } = 2.6;

        /// <summary>Ondes des vallons : longueur d'onde (m), direction (radians), poids, phase. Fixes : le même relief à chaque lancement.</summary>
        public IReadOnlyList<(double Length, double Angle, double Weight, double Phase)> Waves { get; set; } = new[]
        {
            (85.0, 0.35, 1.0, 0.7), (53.0, 2.1, 0.6, 2.3), (37.0, 4.0, 0.35, 4.1),
        };

        /// <summary>L'eau (obstacles bas « riviere… ») reste dans le creux : sol à 0 jusqu'à cette distance (m) de l'eau…</summary>
        public double WaterFlat { get; set; } = 3.0;

        /// <summary>… puis il remonte doucement sur cette distance (m).</summary>
        public double WaterRamp { get; set; } = 30.0;

        /// <summary>
        /// Un replat ne relève pas la berge : sol à 0 jusqu'à cette distance (m) de l'eau (bouts du pont compris), raccordé sur 1,2 m
        /// (<see cref="ShoreBlend"/>).
        /// </summary>
        public double ShoreFlat { get; set; } = 2.4;

        public double ShoreBlend { get; set; } = 1.2;

        /// <summary>Lieux bâtis ou travaillés (identifiants de lieux) où les vallons sont adoucis.</summary>
        public IReadOnlyList<string> CalmPlaces { get; set; } = new[] { "village", "bourg", "hameau", "carriere", "champs" };

        /// <summary>Part de l'amplitude gardée dans un lieu adouci, et distance (m) de raccord autour de lui.</summary>
        public double Calm { get; set; } = 0.35;

        public double CalmRamp { get; set; } = 16.0;

        /// <summary>
        /// Buttes : début d'identifiant des obstacles qui la portent, hauteur (m), largeur de la pente (m). Le dessus est un plateau qui couvre
        /// ces obstacles (adouci comme un lieu calme), la pente descend ensuite en douceur.
        /// </summary>
        public IReadOnlyList<(string Prefix, double Height, double Fall)> Buttes { get; set; } = new[] { ("pierre_seuil", 2.0, 20.0), ("tour", 2.0, 16.0) };

        /// <summary>Replat sous chaque obstacle bâti : horizontal jusqu'à <see cref="PadCore"/> m de son emprise, raccordé sur <see cref="PadBlend"/> m.</summary>
        public double PadCore { get; set; } = 1.5;

        public double PadBlend { get; set; } = 8.0;

        /// <summary>Replat rond (rayon, m) autour des places dégagées : points sûrs (haltes), PNJ, ateliers, nœuds de récolte, portes.</summary>
        public double ClearingRadius { get; set; } = 2.5;

        /// <summary>Collines hors de l'aire : hauteur (m) atteinte sur <see cref="HillRise"/> m au-delà du bord.</summary>
        public double HillHeight { get; set; } = 9.0;

        public double HillRise { get; set; } = 30.0;

        /// <summary>Falaises hors de l'aire, par endroits : hauteur (m), distance au bord (m), largeur de la paroi (m).</summary>
        public double CliffHeight { get; set; } = 8.0;

        public double CliffAt { get; set; } = 10.0;

        public double CliffWidth { get; set; } = 3.0;

        /// <summary>
        /// Sous-lot V2.28c (3) : relief d'affichage de la région (PLAN_GRANDE_CARTE.md § 4.2 et 4.3), en mètres au-dessus du cœur (30 m) :
        /// repères (x, z, hauteur) interpolés par l'inverse du carré de la distance. Le Bourg sur le plateau, les coteaux au sud-ouest, la
        /// forêt, la vallée jusqu'à la cascade, les collines jusqu'à 320 m au-dessus du cœur (350 m), le fort, le col, la mine et l'abbaye
        /// sur leur épaulement, le lac au plus bas (au niveau de l'eau du cœur : l'eau reste à 0), les terres corrompues en cuvette. Propres
        /// aux Vallons : seulement pour une aire de plus de 768 m de côté dont le repère est celui de <see cref="RegionGrid"/> (relecture de la
        /// PR #170 : une autre grande zone n'en hérite pas).
        /// </summary>
        public IReadOnlyList<(double X, double Z, double Height)> RegionHeights { get; set; } = new[]
        {
            (20.0, 15.0, 0.0), (-1010.0, -70.0, 5.0), (-1900.0, -240.0, 10.0), (-1240.0, -1000.0, 50.0), (-1330.0, -680.0, 60.0),
            (-830.0, 780.0, 40.0), (-1700.0, 790.0, 20.0), (300.0, 600.0, 20.0), (590.0, 1150.0, 110.0), (-130.0, 1080.0, 120.0), (100.0, 930.0, 60.0),
            (190.0, 1220.0, 230.0), (350.0, 1260.0, 160.0), (180.0, 1360.0, 320.0), (1200.0, 1350.0, 250.0), (1170.0, 990.0, 150.0), (1740.0, 820.0, 170.0),
            (2100.0, 1220.0, 320.0), (1950.0, 1050.0, 245.0), (1050.0, 275.0, 65.0), (1710.0, 145.0, 60.0), (425.0, -1105.0, 0.0), (60.0, -450.0, 0.0),
            (720.0, -450.0, 5.0), (1740.0, -730.0, 15.0), (2100.0, -1500.0, 30.0), (-1900.0, -1550.0, 60.0),
        };

        /// <summary>Repère de la région à laquelle <see cref="RegionHeights"/> s'applique (origine, colonnes, rangées) : celui des Vallons.</summary>
        public (int OriginX, int OriginZ, int Columns, int Rows) RegionGrid { get; set; } =
            (EdS.Data.Zones.RegionGrid.VallonsOriginX, EdS.Data.Zones.RegionGrid.VallonsOriginZ, EdS.Data.Zones.RegionGrid.VallonsColumns, EdS.Data.Zones.RegionGrid.VallonsRows);

        /// <summary>Rectangle du cœur, où le relief de la région est nul (le cœur ne change pas), et raccord (m) au-delà.</summary>
        public (double MinX, double MinZ, double MaxX, double MaxZ) RegionFlat { get; set; } = (-100, -70, 140, 100);

        public double RegionRamp { get; set; } = 250.0;

        /// <summary>Distance (m) de lissage des repères (évite les pointes) ; raccord (m) du relief de la région à l'eau, qui reste à 0.</summary>
        public double RegionSoften { get; set; } = 100.0;

        public double RegionWaterRamp { get; set; } = 400.0;
    }

    /// <summary>Un replat : emprise (polygone, ou un point pour une place dégagée), marge horizontale, hauteur, groupe.</summary>
    public sealed class ReliefPad
    {
        public string Id { get; }
        public IReadOnlyList<(double X, double Z)> Points { get; }
        public double Core { get; }
        public double Level { get; internal set; }

        /// <summary>Replats dont les raccords se touchent : ils ont tous la même hauteur (aucun ne penche sous l'autre).</summary>
        public int Group { get; internal set; }

        public double MinX { get; }
        public double MinZ { get; }
        public double MaxX { get; }
        public double MaxZ { get; }
        public double CenterX => Points.Average(p => p.X);
        public double CenterZ => Points.Average(p => p.Z);

        internal ReliefPad(string id, IReadOnlyList<(double X, double Z)> points, double core)
        {
            Id = id;
            Points = points;
            Core = core;
            MinX = points.Min(p => p.X);
            MinZ = points.Min(p => p.Z);
            MaxX = points.Max(p => p.X);
            MaxZ = points.Max(p => p.Z);
        }
    }

    /// <summary>
    /// Sous-lot V2.24 « relief des Vallons » (décision de Damien du 11 novembre 2026) : hauteur du sol **affiché**, en mètres, en tout point
    /// (x, z). Affichage seulement : le moteur, les règles de combat, la ligne de vue, les formats et le réseau restent sur un sol plat.
    /// Calcul déterministe (aucun aléa, aucune horloge), d'après les données de la zone : vallons doux (somme d'ondes fixes) ; eau
    /// (obstacles bas) dans le creux, à 0 ; buttes sous les pierres du Seuil et la tour ; lieux bâtis adoucis ; replat horizontal sous chaque
    /// obstacle bâti et autour des places dégagées ; collines et falaises hors de l'aire. L'intérieur d'une pièce reste plat.
    /// Extension prévue (décision du 11 novembre 2026, 1 h 23 : Vallons de 4 × 3 km en cases) : réglages sans constante propre à une aire,
    /// bruit en coordonnées du monde (continu entre cases), et un étage (<c>floor</c>) : seul l'étage 0 (la surface) a du relief.
    /// </summary>
    public sealed class GroundRelief
    {
        /// <summary>Sol plat partout (arène, intérieurs, pas de carte).</summary>
        public static readonly GroundRelief Flat = new GroundRelief();

        private readonly ReliefSettings _s;
        private readonly bool _flat;
        private readonly double _minX, _minZ, _maxX, _maxZ;
        private readonly List<(double X, double Z)[]> _water = new List<(double X, double Z)[]>();
        private readonly List<(double MinX, double MinZ, double MaxX, double MaxZ)> _calm = new List<(double, double, double, double)>();
        private readonly List<(double X, double Z, double Height, double Plateau, double Fall)> _buttes =
            new List<(double, double, double, double, double)>();
        private readonly List<ReliefPad> _pads = new List<ReliefPad>();
        private readonly double _weights;
        private readonly bool _regional;

        public ReliefSettings Settings => _s;

        /// <summary>Sol plat (aucun relief) ?</summary>
        public bool IsFlat => _flat;

        /// <summary>Replats (obstacles bâtis, places dégagées), avec leur hauteur.</summary>
        public IReadOnlyList<ReliefPad> Pads => _pads;

        private GroundRelief()
        {
            _s = new ReliefSettings();
            _flat = true;
        }

        private GroundRelief(AdventureMap map, ReliefSettings settings)
        {
            _s = settings;
            _minX = map.MinX;
            _minZ = map.MinZ;
            _maxX = map.MaxX;
            _maxZ = map.MaxZ;
            _weights = Math.Max(1e-9, _s.Waves.Sum(w => Math.Abs(w.Weight)));
            // L'aire entière décide (une fenêtre d'une aire de plus de 768 m de côté a le relief de la région, comme l'aire entière).
            AdventureMap whole = map.Whole;
            double side = 2.0 * AdventureMap.WindowMeters;
            var grid = whole.Region;
            _regional = (whole.MaxX - whole.MinX > side || whole.MaxZ - whole.MinZ > side) && _s.RegionHeights.Count > 0 && grid != null
                && (grid.OriginX, grid.OriginZ, grid.Columns, grid.Rows) == _s.RegionGrid;
            foreach (SpaceObstacle o in map.Obstacles)
                if (o.Low && StyleRules.Key(o.Id).StartsWith("riviere", StringComparison.Ordinal)) _water.Add(Points(o));
            foreach (MapPlace p in map.Places)
                if (_s.CalmPlaces.Contains(p.Id)) _calm.Add((p.MinX, p.MinZ, p.MaxX, p.MaxZ));
            foreach ((string prefix, double height, double fall) in _s.Buttes)
            {
                var stones = map.Obstacles.Where(o => o.Id.StartsWith(prefix, StringComparison.Ordinal)).Select(Points).ToList();
                if (stones.Count == 0) continue;
                double cx = stones.Average(s => s.Average(p => p.X)), cz = stones.Average(s => s.Average(p => p.Z));
                double plateau = stones.SelectMany(s => s).Max(p => Distance(p.X - cx, p.Z - cz)) + _s.PadCore + 1;
                _buttes.Add((cx, cz, height, plateau, fall));
            }
            foreach (SpaceObstacle o in map.Obstacles)
                if (Builds(o)) _pads.Add(new ReliefPad(o.Id, Points(o), _s.PadCore));
            // V2.25.2 : la porte d'un passage ouvert (son obstacle retiré) garde son replat : la barrière du moulin y reste, ouverte.
            foreach (SpaceObstacle o in map.Gates)
                if (Builds(o) && _pads.All(p => p.Id != o.Id)) _pads.Add(new ReliefPad(o.Id, Points(o), _s.PadCore));
            int c = 0;
            foreach ((double x, double z) in map.Clearings) _pads.Add(new ReliefPad("place" + c++, new[] { (x, z) }, _s.ClearingRadius));
            Group();
        }

        /// <summary>
        /// Relief de la carte d'une zone, réglages par défaut si <paramref name="settings"/> est null. Plat pour un intérieur et pour une zone
        /// sans route (souvenirs, arènes) : seules les zones de plein air à routes (aujourd'hui les Vallons) ont du relief.
        /// </summary>
        public static GroundRelief For(AdventureMap map, ReliefSettings? settings = null)
        {
            if (map is null) throw new ArgumentNullException(nameof(map));
            return map.Indoor || map.Roads.Count == 0 ? Flat : new GroundRelief(map, settings ?? new ReliefSettings());
        }

        /// <summary>
        /// Obstacle posé sur un replat : tout ce qui est bâti (bâtiments, puits, étals, tentes, palissades, murs, porte, blocs, pierres, piliers,
        /// moulin). Restent sur la pente : l'eau (dans le creux), les arbres et les bois, les haies (muret qui suit le sol) ; relecture
        /// indépendante de la PR #170 : l'éboulement et les falaises de la surface (parois de rochers longues de centaines de mètres, posées sur le
        /// sol : un replat commun imposait le niveau le plus bas sur toute leur longueur et creusait une tranchée) ; de même les pans de la
        /// palissade du Bourg fortifié (sous-lot V2.30).
        /// </summary>
        public static bool Builds(SpaceObstacle o)
        {
            if (o is null) throw new ArgumentNullException(nameof(o));
            string key = StyleRules.Key(o.Id);
            return !o.Low && !key.StartsWith("arbre", StringComparison.Ordinal) && TownRules.BuildingFor(o.Id) != StyleBuilding.Woods
                && StyleRules.FenceFor(o.Id) != StyleFence.Hedge && !key.StartsWith("eboulement", StringComparison.Ordinal)
                && !key.StartsWith("falaise", StringComparison.Ordinal)
                && !o.Id.StartsWith("palissade_ville", StringComparison.Ordinal);   // sous-lot V2.30 : les pans de 500 m de la ville suivent le sol
        }

        /// <summary>Hauteur du sol affiché (m) en (x, z), à l'étage <paramref name="floor"/> (0 : la surface ; les autres restent plats).</summary>
        public double Height(double x, double z, int floor = 0)
        {
            if (_flat || floor != 0) return 0;
            double b = Base(x, z);
            double keep = 1, sum = 0, sumLevel = 0;
            foreach (ReliefPad p in _pads)
            {
                double reach = p.Core + _s.PadBlend;
                if (x < p.MinX - reach || x > p.MaxX + reach || z < p.MinZ - reach || z > p.MaxZ + reach) continue;
                double d = PolygonDistance(p.Points, x, z) - p.Core;
                double wi = d <= 0 ? 1 : 1 - Smooth(d / _s.PadBlend);
                if (wi <= 0) continue;
                keep *= 1 - wi;   // poids réunis sans pli (1 dans le cœur d'un replat), pas le plus grand
                sum += wi;
                sumLevel += wi * p.Level;
            }
            if (sum <= 0) return b;
            // Un replat proche de l'eau ne relève ni la berge ni le lit : le sol revient à 0 à moins de ShoreFlat mètres de l'eau.
            double w = 1 - keep;
            return (b + w * (sumLevel / sum - b)) * Smooth((Water(x, z) - _s.ShoreFlat) / _s.ShoreBlend);
        }

        /// <summary>Distance (m) à l'eau la plus proche (0 dedans ; très grande sans eau).</summary>
        public double Water(double x, double z)
        {
            double dw = double.MaxValue;
            foreach ((double X, double Z)[] water in _water) dw = Math.Min(dw, PolygonDistance(water, x, z));
            return dw;
        }

        /// <summary>Pente (m par m) du sol affiché en (x, z), par différences centrées sur ±<paramref name="step"/> m.</summary>
        public double Slope(double x, double z, double step = 0.25)
        {
            double dx = (Height(x + step, z) - Height(x - step, z)) / (2 * step);
            double dz = (Height(x, z + step) - Height(x, z - step)) / (2 * step);
            return Distance(dx, dz);
        }

        /// <summary>Sol avant les replats : vallons, buttes, creux de l'eau, collines et falaises hors de l'aire.</summary>
        public double Base(double x, double z)
        {
            if (_flat) return 0;
            double n = 0;
            foreach ((double length, double angle, double weight, double phase) in _s.Waves)
                n += weight * Math.Sin(2 * Math.PI / length * (x * Math.Cos(angle) + z * Math.Sin(angle)) + phase);
            n /= _weights;
            double calm = 1;
            foreach ((double x0, double z0, double x1, double z1) in _calm)
                calm = Math.Min(calm, _s.Calm + (1 - _s.Calm) * Smooth(Distance(Math.Max(0, Math.Max(x0 - x, x - x1)), Math.Max(0, Math.Max(z0 - z, z - z1))) / _s.CalmRamp));
            double b = 0;
            foreach ((double cx, double cz, double height, double plateau, double fall) in _buttes)
            {
                double d = Distance(x - cx, z - cz);
                b += height * (1 - Smooth((d - plateau) / fall));
                calm = Math.Min(calm, _s.Calm + (1 - _s.Calm) * Smooth((d - plateau) / _s.CalmRamp));
            }
            b += calm * _s.Amplitude * (0.5 + 0.5 * n);
            double water = _water.Count > 0 ? Water(x, z) : double.MaxValue;
            if (_water.Count > 0) b *= Smooth((water - _s.WaterFlat) / _s.WaterRamp);
            if (_regional) b += Region(x, z) * (_water.Count > 0 ? Smooth((water - _s.WaterFlat) / _s.RegionWaterRamp) : 1);
            return b + Edge(x, z);
        }

        /// <summary>
        /// Sous-lot V2.28c (3) : hauteur de la région (m au-dessus du cœur) avant le raccord à l'eau : nulle dans le cœur
        /// (<see cref="ReliefSettings.RegionFlat"/>), repères interpolés au-delà, raccordés sur <see cref="ReliefSettings.RegionRamp"/> m.
        /// </summary>
        public double Region(double x, double z)
        {
            if (!_regional) return 0;
            (double x0, double z0, double x1, double z1) = _s.RegionFlat;
            double ramp = Smooth(Distance(Math.Max(0, Math.Max(x0 - x, x - x1)), Math.Max(0, Math.Max(z0 - z, z - z1))) / _s.RegionRamp);
            if (ramp <= 0) return 0;
            double soft = _s.RegionSoften * _s.RegionSoften, sw = 0, sh = 0;
            foreach ((double ax, double az, double h) in _s.RegionHeights)
            {
                double w = 1 / ((x - ax) * (x - ax) + (z - az) * (z - az) + soft);
                sw += w;
                sh += w * h;
            }
            return ramp * sh / sw;
        }

        /// <summary>Distance (m) hors de l'aire praticable (0 dedans).</summary>
        public double Outside(double x, double z) =>
            _flat ? 0 : Distance(Math.Max(0, Math.Max(_minX - x, x - _maxX)), Math.Max(0, Math.Max(_minZ - z, z - _maxZ)));

        /// <summary>Collines (et falaises par endroits) hors de l'aire : 0 sur son bord, pente nulle au bord ; décor seul.</summary>
        private double Edge(double x, double z)
        {
            double d = Outside(x, z);
            if (d <= 0) return 0;
            double rough = 0.5 + 0.5 * Math.Sin(x * 0.071 + 1.3) * Math.Cos(z * 0.059 + 0.4);
            double cliffs = Smooth((Math.Sin(x * 0.043 + 2.0) + Math.Sin(z * 0.051 + 0.9) - 0.2) / 0.6);
            return _s.HillHeight * Smooth(d / _s.HillRise) * (0.75 + 0.25 * rough)
                + _s.CliffHeight * Smooth((d - _s.CliffAt) / _s.CliffWidth) * cliffs;
        }

        /// <summary>
        /// Replats voisins (le raccord de l'un atteint l'emprise de l'autre) : même groupe, même hauteur : le sol le plus bas à leurs centres
        /// (un groupe qui touche l'eau reste au niveau de la berge, le pont et la rue s'y raccordent sans marche).
        /// </summary>
        private void Group()
        {
            int n = _pads.Count;
            var parent = Enumerable.Range(0, n).ToArray();
            int Find(int i)
            {
                while (parent[i] != i) i = parent[i] = parent[parent[i]];
                return i;
            }
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    ReliefPad a = _pads[i], b = _pads[j];
                    double link = a.Core + b.Core + _s.PadBlend;
                    if (a.MaxX + link < b.MinX || b.MaxX + link < a.MinX || a.MaxZ + link < b.MinZ || b.MaxZ + link < a.MinZ) continue;
                    double d = Math.Min(a.Points.Min(p => PolygonDistance(b.Points, p.X, p.Z)), b.Points.Min(p => PolygonDistance(a.Points, p.X, p.Z)));
                    if (d < link) parent[Find(i)] = Find(j);
                }
            var levels = new Dictionary<int, double>();
            for (int i = 0; i < n; i++)
            {
                int g = Find(i);
                double here = Base(_pads[i].CenterX, _pads[i].CenterZ);
                levels[g] = levels.TryGetValue(g, out double low) ? Math.Min(low, here) : here;
            }
            for (int i = 0; i < n; i++)
            {
                int g = Find(i);
                _pads[i].Group = g;
                _pads[i].Level = levels[g];
            }
        }

        private static (double X, double Z)[] Points(SpaceObstacle o) => o.Points.Select(p => (p.X.Raw / 10000.0, p.Z.Raw / 10000.0)).ToArray();

        private static double Distance(double dx, double dz) => Math.Sqrt(dx * dx + dz * dz);

        /// <summary>Raccord lisse de 0 (t ≤ 0) à 1 (t ≥ 1), pente nulle aux deux bouts.</summary>
        public static double Smooth(double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return t * t * (3 - 2 * t);
        }

        /// <summary>Distance (m) d'un point à un polygone (0 dedans) ; un seul sommet : distance à ce point.</summary>
        public static double PolygonDistance(IReadOnlyList<(double X, double Z)> points, double x, double z)
        {
            if (points.Count == 1) return Distance(x - points[0].X, z - points[0].Z);
            bool inside = false;
            double best = double.MaxValue;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                (double ax, double az) = points[i];
                (double bx, double bz) = points[j];
                if ((az > z) != (bz > z) && x < (bx - ax) * (z - az) / (bz - az) + ax) inside = !inside;
                double dx = bx - ax, dz = bz - az, l = dx * dx + dz * dz;
                double t = l <= 0 ? 0 : Math.Max(0, Math.Min(1, ((x - ax) * dx + (z - az) * dz) / l));
                best = Math.Min(best, Distance(x - ax - t * dx, z - az - t * dz));
            }
            return inside ? 0 : best;
        }
    }
}
