using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Runtime
{
    /// <summary>V2.58 (villageois vivants) : métier d'un habitant du Bourg ou d'un hameau.</summary>
    public enum VillagerTrade
    {
        Laborer = 0,
        Farmer = 1,
        Baker = 2,
        Smith = 3,
        Merchant = 4,
        Washer = 5,
        Elder = 6,
        Child = 7,
    }

    /// <summary>V2.58 : genre de place où se tient un habitant à une heure donnée.</summary>
    public enum LifePlace
    {
        Home = 0,
        Work = 1,
        Meal = 2,
        Market = 3,
        Tavern = 4,
        Shelter = 5,
        Feast = 6,
        Chapel = 7,
        Play = 8,
        Porch = 9,
    }

    /// <summary>V2.58 : une place de la journée d'un habitant (orientation en degrés ; <see cref="Indoors"/> : il entre et disparaît).</summary>
    public readonly struct LifeSpot
    {
        public LifeSpot(double x, double z, double yaw, LifePlace place, VillagerActivity activity, bool indoors)
        {
            X = x;
            Z = z;
            Yaw = yaw;
            Place = place;
            Activity = activity;
            Indoors = indoors;
        }

        public double X { get; }
        public double Z { get; }
        public double Yaw { get; }
        public LifePlace Place { get; }
        public VillagerActivity Activity { get; }
        public bool Indoors { get; }

        public LifeSpot With(LifePlace place, VillagerActivity activity, bool indoors) => new LifeSpot(X, Z, Yaw, place, activity, indoors);
    }

    /// <summary>V2.58 : un créneau de la journée (minute de début, de 0 à 1439, et la place visée).</summary>
    public readonly struct LifeSlot
    {
        public LifeSlot(int start, LifeSpot spot)
        {
            Start = start;
            Spot = spot;
        }

        public int Start { get; }
        public LifeSpot Spot { get; }
    }

    /// <summary>
    /// V2.58 : ce qui change la journée de tous : saison (<see cref="WardrobeSeason"/>), averse ou orage (on s'abrite), jour de fête, grand
    /// jour de marché, et l'allure de marche en mètres par minute du monde (une journée du monde dure 3 h : 1,1 m/s font 8,25 m par minute).
    /// </summary>
    public readonly struct VillageContext
    {
        public VillageContext(WardrobeSeason season, bool shelter, bool festive, bool marketDay, double metersPerMinute = DefaultPace)
        {
            Season = season;
            Shelter = shelter;
            Festive = festive;
            MarketDay = marketDay;
            MetersPerMinute = metersPerMinute > 0 && !double.IsInfinity(metersPerMinute) ? metersPerMinute : DefaultPace;
        }

        public const double DefaultPace = 8.25;

        public WardrobeSeason Season { get; }
        public bool Shelter { get; }
        public bool Festive { get; }
        public bool MarketDay { get; }
        public double MetersPerMinute { get; }
    }

    /// <summary>V2.58 : où est un habitant à une minute : place, orientation, geste ; en marche ou non ; caché (rentré dans une maison).</summary>
    public readonly struct LifePose
    {
        public LifePose(double x, double z, double yaw, VillagerActivity activity, LifePlace place, bool moving, bool hidden)
        {
            X = x;
            Z = z;
            Yaw = yaw;
            Activity = activity;
            Place = place;
            Moving = moving;
            Hidden = hidden;
        }

        public double X { get; }
        public double Z { get; }
        public double Yaw { get; }
        public VillagerActivity Activity { get; }
        public LifePlace Place { get; }
        public bool Moving { get; }
        public bool Hidden { get; }
    }

    /// <summary>
    /// V2.58 : un habitant, tiré de sa maison (toujours le même pour une même carte) : métier, personnage du catalogue (<see cref="Character"/>,
    /// rôle des corps humains existants) et modèle du kit des villageois (<see cref="Model"/>, Tripo, pas encore livré : repli sur
    /// <see cref="Character"/> tant qu'il manque), ses places (maison, travail, marché, taverne, chapelle, abri, fête, jeux, banc).
    /// </summary>
    public sealed class Resident
    {
        internal Resident(string id, string house, VillagerTrade trade, string character, string model, int offset, LifeSpot home, LifeSpot work,
            LifeSpot market, LifeSpot? tavern, LifeSpot? chapel, LifeSpot shelter, LifeSpot feast, LifeSpot play, LifeSpot porch, bool hamlet)
        {
            Id = id;
            House = house;
            Trade = trade;
            Character = character;
            Model = model;
            Offset = offset;
            Home = home;
            Work = work;
            Market = market;
            Tavern = tavern;
            Chapel = chapel;
            Shelter = shelter;
            Feast = feast;
            Play = play;
            Porch = porch;
            Hamlet = hamlet;
            var spots = new List<LifeSpot> { home, work, market, shelter, feast, play, porch };
            if (tavern is LifeSpot t) spots.Add(t);
            if (chapel is LifeSpot ch) spots.Add(ch);
            MinX = spots.Min(p => p.X);
            MinZ = spots.Min(p => p.Z);
            MaxX = spots.Max(p => p.X);
            MaxZ = spots.Max(p => p.Z);
        }

        /// <summary>Boîte de toutes ses places (ses trajets passent par les routes entre elles : à peine au-delà).</summary>
        public double MinX { get; }
        public double MinZ { get; }
        public double MaxX { get; }
        public double MaxZ { get; }

        public string Id { get; }
        public string House { get; }
        public VillagerTrade Trade { get; }
        public string Character { get; }
        public string Model { get; }
        /// <summary>Décalage de ses heures (−20 à +20 minutes) : ils ne partent pas tous ensemble.</summary>
        public int Offset { get; }
        public LifeSpot Home { get; }
        public LifeSpot Work { get; }
        public LifeSpot Market { get; }
        /// <summary>Terrasse de l'auberge la plus proche (null : hameau sans auberge, veillée au lieu de la taverne).</summary>
        public LifeSpot? Tavern { get; }
        public LifeSpot? Chapel { get; }
        public LifeSpot Shelter { get; }
        public LifeSpot Feast { get; }
        public LifeSpot Play { get; }
        public LifeSpot Porch { get; }
        public bool Hamlet { get; }
        public bool Child => Trade == VillagerTrade.Child;
        public bool Elder => Trade == VillagerTrade.Elder;

        /// <summary>Taille du corps (1 pour un adulte) : un enfant est plus petit.</summary>
        public double Scale => Child ? 0.72 : Elder ? 0.96 : 1.0;

        /// <summary>Allure relative : un enfant trotte, un ancien va doucement.</summary>
        public double Pace => Child ? 1.15 : Elder ? 0.75 : 1.0;
    }

    /// <summary>
    /// V2.58 : réseau des routes d'une carte, pour des trajets crédibles : on rejoint la route la plus proche, on la suit (plus court chemin
    /// sur les routes et leurs croisements), puis on la quitte au plus près du but. Trajets gardés en mémoire (même résultat avec ou sans).
    /// </summary>
    public sealed class VillageRoads
    {
        private readonly List<(double X, double Z)> _nodes = new List<(double X, double Z)>();
        private readonly List<List<int>> _links = new List<List<int>>();
        private readonly List<(int A, int B)> _segments = new List<(int A, int B)>();
        private readonly Dictionary<(long, long, long, long), (double X, double Z)[]> _cache = new Dictionary<(long, long, long, long), (double X, double Z)[]>();   // clé sans perte (revue Codex de la PR #268)

        /// <summary>Distance de raccord entre deux routes (m) : un bout de route à moins de cela d'une autre route s'y branche.</summary>
        public const double JoinDistance = 6;

        /// <summary>Trajet court (m) : en dessous, on va tout droit (une place à côté de l'autre).</summary>
        public const double DirectDistance = 24;

        /// <summary>
        /// Trajets gardés au plus par défaut (relecture indépendante de la PR #268 : au-delà, on oublie le plus ancien quart, pas tout d'un
        /// coup : les trajets de la journée en cours restent).
        /// </summary>
        public const int MaxCached = 4096;

        private readonly int _maxCached;
        private readonly Queue<(long, long, long, long)> _order = new Queue<(long, long, long, long)>();

        public VillageRoads(IReadOnlyList<StyleRoad>? roads, int maxCached = MaxCached)
        {
            if (maxCached < 1) throw new ArgumentOutOfRangeException(nameof(maxCached));
            _maxCached = maxCached;
            // Relecture de la PR #268 : une route sans assez de points (ou absente) est ignorée, jamais une erreur.
            var list = (roads ?? Array.Empty<StyleRoad>()).Where(r => r != null && r.Points != null && r.Points.Count >= 2)
                .OrderBy(r => r.Id, StringComparer.Ordinal).ToList();
            var firstOf = new List<int>();
            for (int k = 0; k < list.Count; k++)
            {
                StyleRoad r = list[k];
                firstOf.Add(_nodes.Count);
                for (int i = 0; i < r.Points.Count; i++)
                {
                    Node(r.Points[i]);
                    if (i > 0) Link(_nodes.Count - 2, _nodes.Count - 1, k);
                }
            }
            // Croisements : chaque bout de route se branche sur le segment le plus proche d'une autre route (s'il est assez près). Revue
            // Codex de la PR #268 (tour 2) : le segment touché est SCINDÉ au raccord (A–raccord, raccord–B, le lien direct A–B retiré) :
            // un trajet qui part du milieu de ce segment peut ainsi passer par le raccord, sans aller chercher une extrémité.
            for (int r = 0; r < list.Count; r++)
            {
                int last = firstOf[r] + list[r].Points.Count - 1;
                foreach (int end in new[] { firstOf[r], last })
                {
                    (double X, double Z) p = _nodes[end];
                    double best = JoinDistance * JoinDistance;
                    int bestSeg = -1;
                    (double X, double Z) bestAt = default;
                    for (int s = 0; s < _segments.Count; s++)
                    {
                        int road = _segmentRoad[s];
                        if (road < 0 || road == r) continue;   // ni un raccord, ni sa propre route
                        (int a, int b) = _segments[s];
                        var q = Project(p, _nodes[a], _nodes[b]);
                        double d = D2(p, q);
                        if (d < best)
                        {
                            best = d;
                            bestSeg = s;
                            bestAt = q;
                        }
                    }
                    if (bestSeg < 0) continue;
                    (int sa, int sb) = _segments[bestSeg];
                    int join;
                    if (D2(bestAt, _nodes[sa]) < 1e-12) join = sa;   // sur une extrémité : pas de nouveau point
                    else if (D2(bestAt, _nodes[sb]) < 1e-12) join = sb;
                    else
                    {
                        join = Node(bestAt);
                        _links[sa].Remove(sb);
                        _links[sb].Remove(sa);
                        _segments[bestSeg] = (sa, join);
                        _links[sa].Add(join);
                        _links[join].Add(sa);
                        Link(join, sb, _segmentRoad[bestSeg]);
                    }
                    Link(end, join, -1);
                }
            }
        }

        public int NodeCount => _nodes.Count;

        /// <summary>Trajets gardés en ce moment.</summary>
        public int CachedCount => _cache.Count;

        /// <summary>Nombre de morceaux de réseau non reliés entre eux (0 sans route ; 1 : tout se rejoint).</summary>
        public int Components()
        {
            var seen = new bool[_nodes.Count];
            int parts = 0;
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (seen[i]) continue;
                parts++;
                var stack = new Stack<int>();
                stack.Push(i);
                seen[i] = true;
                while (stack.Count > 0)
                    foreach (int j in _links[stack.Pop()])
                        if (!seen[j])
                        {
                            seen[j] = true;
                            stack.Push(j);
                        }
            }
            return parts;
        }

        private int Node((double X, double Z) p)
        {
            _nodes.Add(p);
            _links.Add(new List<int>());
            return _nodes.Count - 1;
        }

        /// <summary>Route d'origine de chaque segment (−1 : un raccord entre deux routes).</summary>
        private readonly List<int> _segmentRoad = new List<int>();

        private void Link(int a, int b, int road)
        {
            if (a == b) return;
            _links[a].Add(b);
            _links[b].Add(a);
            _segments.Add((a, b));
            _segmentRoad.Add(road);
        }

        private static double D2((double X, double Z) a, (double X, double Z) b) => (a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z);

        private static (double X, double Z) Project((double X, double Z) p, (double X, double Z) a, (double X, double Z) b)
        {
            double dx = b.X - a.X, dz = b.Z - a.Z, len = dx * dx + dz * dz;
            double t = len <= 1e-12 ? 0 : Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Z - a.Z) * dz) / len));
            return (a.X + dx * t, a.Z + dz * t);
        }

        /// <summary>Point de route le plus proche de <paramref name="p"/> et son segment (−1 sans route).</summary>
        private ((double X, double Z) At, int Segment) Snap((double X, double Z) p)
        {
            double best = double.MaxValue;
            int seg = -1;
            (double X, double Z) at = p;
            for (int s = 0; s < _segments.Count; s++)
            {
                var q = Project(p, _nodes[_segments[s].A], _nodes[_segments[s].B]);
                double d = D2(p, q);
                if (d < best)
                {
                    best = d;
                    seg = s;
                    at = q;
                }
            }
            return (at, seg);
        }

        /// <summary>
        /// Revue Codex de la PR #268 : clé exacte (les bits du nombre), jamais arrondie : un trajet gardé a toujours les extrémités demandées,
        /// et la réponse ne dépend pas de ce que la mémoire contenait avant.
        /// </summary>
        private static long Key(double v) => BitConverter.DoubleToInt64Bits(v == 0 ? 0.0 : v);

        /// <summary>
        /// Trajet de (<paramref name="fromX"/>, <paramref name="fromZ"/>) à (<paramref name="toX"/>, <paramref name="toZ"/>) : tout droit s'il
        /// est court ou sans route ; sinon on rejoint la route, on la suit, on la quitte. Le premier point est le départ, le dernier l'arrivée.
        /// </summary>
        public IReadOnlyList<(double X, double Z)> Route(double fromX, double fromZ, double toX, double toZ) => Route(fromX, fromZ, toX, toZ, true);

        /// <summary>
        /// Comme <see cref="Route(double, double, double, double)"/> ; <paramref name="remember"/> faux : le trajet n'est pas gardé (départ
        /// quelconque, comme le corps d'un habitant qui rattrape sa place : il ne remplirait la mémoire que de trajets d'une seule fois).
        /// </summary>
        public IReadOnlyList<(double X, double Z)> Route(double fromX, double fromZ, double toX, double toZ, bool remember)
        {
            var key = (Key(fromX), Key(fromZ), Key(toX), Key(toZ));
            if (_cache.TryGetValue(key, out var cached)) return cached;
            var path = Build((fromX, fromZ), (toX, toZ));
            if (!remember) return path;
            if (_cache.Count >= _maxCached)
            {
                // Éviction partielle : le plus ancien quart (au moins un trajet).
                int drop = Math.Max(1, _maxCached / 4);
                for (int i = 0; i < drop && _order.Count > 0; i++) _cache.Remove(_order.Dequeue());
            }
            _cache[key] = path;
            _order.Enqueue(key);
            return path;
        }

        private (double X, double Z)[] Build((double X, double Z) from, (double X, double Z) to)
        {
            if (_segments.Count == 0 || D2(from, to) < DirectDistance * DirectDistance) return new[] { from, to };
            var (sa, segA) = Snap(from);
            var (sb, segB) = Snap(to);
            // Plus direct sans la route (la route ferait un détour plus long que deux fois la ligne droite) : tout droit.
            if (segA == segB) return new[] { from, sa, sb, to };
            int n = _nodes.Count;
            var dist = new double[n + 2];
            var prev = new int[n + 2];
            var done = new bool[n + 2];
            for (int i = 0; i < dist.Length; i++)
            {
                dist[i] = double.MaxValue;
                prev[i] = -1;
            }
            int start = n, goal = n + 1;
            (double X, double Z) At(int i) => i == start ? sa : i == goal ? sb : _nodes[i];
            IEnumerable<int> Next(int i)
            {
                if (i == start)
                {
                    yield return _segments[segA].A;
                    yield return _segments[segA].B;
                    yield break;
                }
                if (i == goal) yield break;
                foreach (int j in _links[i]) yield return j;
                if (i == _segments[segB].A || i == _segments[segB].B) yield return goal;
            }
            dist[start] = 0;
            while (true)
            {
                int u = -1;
                double best = double.MaxValue;
                for (int i = 0; i < dist.Length; i++)
                    if (!done[i] && dist[i] < best)
                    {
                        best = dist[i];
                        u = i;
                    }
                if (u < 0 || u == goal) break;
                done[u] = true;
                foreach (int v in Next(u))
                {
                    double d = dist[u] + Math.Sqrt(D2(At(u), At(v)));
                    if (d < dist[v])
                    {
                        dist[v] = d;
                        prev[v] = u;
                    }
                }
            }
            if (prev[goal] < 0) return new[] { from, to };   // routes non reliées : tout droit
            var nodes = new List<(double X, double Z)>();
            for (int i = goal; i >= 0; i = prev[i]) nodes.Add(At(i));
            nodes.Add(from);
            nodes.Reverse();
            nodes.Add(to);
            // Points confondus retirés (un trajet n'a jamais de pas nul).
            var clean = new List<(double X, double Z)> { nodes[0] };
            for (int i = 1; i < nodes.Count; i++)
                if (D2(clean[clean.Count - 1], nodes[i]) > 1e-6 || i == nodes.Count - 1) clean.Add(nodes[i]);
            return clean.ToArray();
        }

        /// <summary>Longueur d'un trajet (m).</summary>
        public static double Length(IReadOnlyList<(double X, double Z)> path)
        {
            double sum = 0;
            for (int i = 1; i < path.Count; i++) sum += Math.Sqrt(D2(path[i - 1], path[i]));
            return sum;
        }

        /// <summary>Point du trajet à <paramref name="distance"/> m du départ (borné à l'arrivée), et la direction (orientation en degrés).</summary>
        public static (double X, double Z, double Yaw) Along(IReadOnlyList<(double X, double Z)> path, double distance)
        {
            double left = Math.Max(0, distance);
            for (int i = 1; i < path.Count; i++)
            {
                double seg = Math.Sqrt(D2(path[i - 1], path[i]));
                double yaw = StyleRules.FaceYaw(path[i - 1].X, path[i - 1].Z, path[i].X, path[i].Z);
                if (left <= seg || i == path.Count - 1)
                {
                    double t = seg <= 1e-9 ? 1 : Math.Min(1, left / seg);
                    return (path[i - 1].X + (path[i].X - path[i - 1].X) * t, path[i - 1].Z + (path[i].Z - path[i - 1].Z) * t, yaw);
                }
                left -= seg;
            }
            var end = path[path.Count - 1];
            return (end.X, end.Z, 0);
        }
    }

    /// <summary>V2.58 : les habitants d'une carte et ses routes (construit une fois, à l'entrée dans la région).</summary>
    public sealed class VillageWorld
    {
        internal VillageWorld(IReadOnlyList<Resident> residents, VillageRoads roads)
        {
            Residents = residents;
            Roads = roads;
        }

        public IReadOnlyList<Resident> Residents { get; }
        public VillageRoads Roads { get; }

        private readonly Dictionary<Resident, IReadOnlyList<LifeSlot>> _days = new Dictionary<Resident, IReadOnlyList<LifeSlot>>();
        private VillageContext _daysOf;
        private bool _daysSet;

        /// <summary>
        /// Journée de <paramref name="r"/> (<see cref="VillageLife.Day"/>), gardée tant que le contexte ne change pas : aucune allocation par
        /// image. Même résultat que <see cref="VillageLife.Day"/>.
        /// </summary>
        public IReadOnlyList<LifeSlot> DayOf(Resident r, VillageContext c)
        {
            if (!_daysSet || !Same(_daysOf, c))
            {
                _days.Clear();
                _daysOf = c;
                _daysSet = true;
            }
            if (!_days.TryGetValue(r, out var day)) _days[r] = day = VillageLife.Day(r, c);
            return day;
        }

        private static bool Same(VillageContext a, VillageContext b) =>
            a.Season == b.Season && a.Shelter == b.Shelter && a.Festive == b.Festive && a.MarketDay == b.MarketDay && a.MetersPerMinute == b.MetersPerMinute;
    }

    /// <summary>
    /// V2.58 (villageois vivants, demande de Damien du 27 novembre 2026) : la journée des habitants du Bourg et des hameaux des Vallons, en
    /// fonctions pures de la minute du monde, de la saison, du temps et des fêtes. « Où est tel villageois à telle heure » :
    /// <see cref="At"/>. Affichage seulement : le moteur ne les connaît pas, rien n'est sauvegardé.
    /// </summary>
    public static class VillageLife
    {
        /// <summary>
        /// Distance (m) gardée entre une place d'habitant et celles des PNJ, des marchands du décor et de la foule du marché (relecture de la
        /// PR #268 : deux corps l'un dans l'autre).
        /// </summary>
        public const double AvoidDistance = 1.5;

        /// <summary>
        /// Revue Codex de la PR #268 (tour 2) : les places à éviter de toute la région (carte entière, jamais la fenêtre chargée) : les PNJ
        /// des pièces (<see cref="AdventureMap.Speakers"/>) et tous les villageois du décor que la région peut placer
        /// (<see cref="TownModels.Villagers"/> sur la carte entière, sans limite de nombre : marchands des étals compris). Même liste quelle que
        /// soit la fenêtre d'où l'on part.
        /// </summary>
        public static IReadOnlyList<(double X, double Z)> RegionAvoid(AdventureMap map)
        {
            if (map is null) throw new ArgumentNullException(nameof(map));
            AdventureMap whole = map.Whole;
            var list = new List<(double X, double Z)>();
            if (whole.Indoor) return list;
            foreach (var s in whole.Speakers) list.Add((s.X, s.Z));
            foreach (StyleVillager v in TownModels.Villagers(whole, whole.AreaFields(whole.Roads), MaxDecorVillagers)) list.Add((v.X, v.Z));
            return list;
        }

        /// <summary>Villageois du décor comptés au plus par <see cref="RegionAvoid"/> (bien au-delà de ce qu'une région en place).</summary>
        public const int MaxDecorVillagers = 4096;

        /// <summary>Au plus d'habitants par carte (une maison en donne d'un à trois).</summary>
        public const int MaxResidents = 480;

        /// <summary>Distance (m) au-delà de laquelle un lieu commun (puits, auberge, chapelle) n'est plus « le sien » : on est d'un hameau.</summary>
        public const double CommonReach = 260;

        // ---------------------------------------------------------------------------------------------------------------- habitants

        /// <summary>
        /// Habitants de la carte <paramref name="map"/> (sa carte entière : les routines ne dépendent pas de la fenêtre chargée) : d'un à trois
        /// par maison (adulte ; parfois un second adulte, un enfant, un ancien), leur métier d'après la maison (forge, boulangerie, échoppe,
        /// mine, bergerie) ou tiré, leurs places. Aucun dans un intérieur. Leurs places restent à <see cref="AvoidDistance"/> m au moins des
        /// points <paramref name="avoid"/> (PNJ, marchands du décor, foule du marché), comme des murs.
        /// </summary>
        public static VillageWorld World(AdventureMap map, IReadOnlyList<StyleField>? fields, IReadOnlyList<(double X, double Z)>? avoid = null)
        {
            if (map is null) throw new ArgumentNullException(nameof(map));
            AdventureMap whole = map.Whole;
            var roads = new VillageRoads(whole.Indoor ? null : whole.Roads);
            var residents = new List<Resident>();
            if (whole.Indoor) return new VillageWorld(residents, roads);
            var boxes = whole.Obstacles.Where(o => o.Points.Count >= 3).Select(o => new Box(o.Id, TownRules.BuildingFor(o.Id),
                o.Points.Min(p => p.X.Raw) / 10000.0, o.Points.Min(p => p.Z.Raw) / 10000.0,
                o.Points.Max(p => p.X.Raw) / 10000.0, o.Points.Max(p => p.Z.Raw) / 10000.0)).OrderBy(b => b.Id, StringComparer.Ordinal).ToList();
            var houses = boxes.Where(b => b.Kind == StyleBuilding.House && Dwelling(b.Id)).ToList();
            var wells = boxes.Where(b => b.Kind == StyleBuilding.Well).ToList();
            var inns = boxes.Where(b => b.Kind == StyleBuilding.Inn).ToList();
            var chapels = boxes.Where(b => b.Kind == StyleBuilding.Chapel).ToList();
            var stalls = boxes.Where(b => b.Kind == StyleBuilding.Stall).ToList();
            var barns = boxes.Where(b => b.Kind == StyleBuilding.Barn).ToList();
            var fieldList = (fields ?? Array.Empty<StyleField>()).ToList();
            var avoidList = (avoid ?? Array.Empty<(double X, double Z)>()).ToList();
            var covers = inns.Concat(chapels).Concat(stalls).Concat(barns).OrderBy(b => b.Id, StringComparer.Ordinal).ToList();
            var all = boxes;

            (double X, double Z, double Yaw) Front(Box b, double step)
            {
                (int X, int Z) f = TownRules.FacingOf(b.Id, b.MinX, b.MinZ, b.MaxX, b.MaxZ, whole.Roads, whole.Doors);
                double half = f.X != 0 ? (b.MaxX - b.MinX) / 2 : (b.MaxZ - b.MinZ) / 2;
                double x = b.CX + f.X * (half + step), z = b.CZ + f.Z * (half + step);
                return (x, z, StyleRules.FaceYaw(0, 0, f.X, f.Z));
            }

            Box? Nearest(List<Box> list, double x, double z, double reach)
            {
                Box? best = null;
                double bd = reach * reach;
                foreach (Box b in list)
                {
                    double d = b.Dist2(x, z);
                    if (d < bd)
                    {
                        bd = d;
                        best = b;
                    }
                }
                return best;
            }

            // Une place libre (hors des bâtiments) près de (x, z) : on s'écarte un peu si besoin (jamais dans un mur).
            (double X, double Z) Free(double x, double z, string salt)
            {
                double ox = x, oz = z;
                for (int k = 0; k < 24; k++)
                {
                    x = Math.Max(whole.MinX + 1, Math.Min(whole.MaxX - 1, x));
                    z = Math.Max(whole.MinZ + 1, Math.Min(whole.MaxZ - 1, z));
                    bool blocked = false;
                    foreach (Box b in all)
                        if (x > b.MinX - 0.4 && x < b.MaxX + 0.4 && z > b.MinZ - 0.4 && z < b.MaxZ + 0.4)
                        {
                            blocked = true;
                            break;
                        }
                    if (!blocked && avoidList.Count > 0)
                        foreach (var a0 in avoidList)
                            if ((a0.X - x) * (a0.X - x) + (a0.Z - z) * (a0.Z - z) < AvoidDistance * AvoidDistance)
                            {
                                blocked = true;
                                break;
                            }
                    if (!blocked) return (x, z);
                    // On s'écarte de plus en plus, autour de la place voulue (au bord de la carte, la porte peut donner dehors).
                    double a = StyleRules.Variant(salt + "/libre" + k, 360) * Math.PI / 180;
                    x = ox + Math.Cos(a) * (1.5 + 0.5 * k);
                    z = oz + Math.Sin(a) * (1.5 + 0.5 * k);
                }
                return (Math.Max(whole.MinX + 1, Math.Min(whole.MaxX - 1, x)), Math.Max(whole.MinZ + 1, Math.Min(whole.MaxZ - 1, z)));
            }

            LifeSpot Ring(double cx, double cz, double radius, string salt, LifePlace place, VillagerActivity activity)
            {
                double a = StyleRules.Variant(salt, 360) * Math.PI / 180;
                double r = radius + StyleRules.Variant(salt + "/r", 10) * 0.15;
                var (x, z) = Free(cx + Math.Cos(a) * r, cz + Math.Sin(a) * r, salt);
                return new LifeSpot(x, z, StyleRules.FaceYaw(x, z, cx, cz), place, activity, false);
            }

            foreach (Box house in houses)
            {
                if (residents.Count >= MaxResidents) break;
                var door = Front(house, 0.7);
                var (dx, dz) = Free(door.X, door.Z, house.Id);
                var home = new LifeSpot(dx, dz, door.Yaw, LifePlace.Home, VillagerActivity.Idle, true);
                Box? well = Nearest(wells, house.CX, house.CZ, CommonReach);
                Box? inn = Nearest(inns, house.CX, house.CZ, CommonReach + 90);
                Box? chapel = Nearest(chapels, house.CX, house.CZ, CommonReach + 90);
                bool hamlet = well == null && inn == null;
                // Centre du lieu de vie : le puits, sinon le milieu des maisons voisines (la veillée d'un hameau).
                double gx, gz;
                if (well != null)
                {
                    gx = well.CX;
                    gz = well.CZ;
                }
                else
                {
                    var near = houses.Where(h => h.Dist2(house.CX, house.CZ) < 90 * 90).ToList();
                    gx = near.Average(h => h.CX);
                    gz = near.Average(h => h.CZ);
                }
                LifeSpot? tavernOf(string salt)
                {
                    if (inn == null) return null;
                    var f = Front(inn, 2.2);
                    return Ring(f.X, f.Z, 1.4, salt + "/terrasse", LifePlace.Tavern, VillagerActivity.Drink);
                }
                LifeSpot? chapelOf(string salt)
                {
                    if (chapel == null) return null;
                    var f = Front(chapel, 1.8);
                    return Ring(f.X, f.Z, 1.2, salt + "/chapelle", LifePlace.Chapel, VillagerActivity.Idle);
                }
                LifeSpot ShelterOf(string salt)
                {
                    // Sous l'auvent le plus proche (auberge, chapelle, étal, grange) à moins de 70 m ; sinon on rentre.
                    Box? s = Nearest(covers, dx, dz, 70);
                    if (s == null) return home.With(LifePlace.Shelter, VillagerActivity.Idle, true);
                    var f = Front(s, 0.6);
                    var (x, z) = Free(f.X + (StyleRules.Variant(salt + "/abri", 5) - 2) * 0.7, f.Z + (StyleRules.Variant(salt + "/abri2", 5) - 2) * 0.7, salt);
                    return new LifeSpot(x, z, f.Yaw, LifePlace.Shelter, VillagerActivity.Look, false);   // sous l'auvent, il regarde tomber la pluie
                }

                VillagerTrade houseTrade = TradeOf(house.Id, StyleRules.Variant(house.Id + "/metier", 100),
                    fieldList.Count > 0 && fieldList.Any(f => Dist2Field(f, house.CX, house.CZ) < 220 * 220),
                    Nearest(stalls, house.CX, house.CZ, 120) != null);
                var members = new List<VillagerTrade> { houseTrade };
                if (StyleRules.Variant(house.Id + "/second", 3) == 0) members.Add(StyleRules.Variant(house.Id + "/second/metier", 2) == 0 ? VillagerTrade.Washer : VillagerTrade.Laborer);
                if (StyleRules.Variant(house.Id + "/enfant", 3) == 0) members.Add(VillagerTrade.Child);
                if (StyleRules.Variant(house.Id + "/ancien", 6) == 0) members.Add(VillagerTrade.Elder);

                for (int m = 0; m < members.Count && residents.Count < MaxResidents; m++)
                {
                    VillagerTrade trade = members[m];
                    string id = "habitant/" + house.Id + "/" + m;
                    // Revue Codex de la PR #268 : chacun sa place devant la porte (côte à côte le long de la façade), jamais l'un dans l'autre ;
                    // les places déjà prises du foyer sont évitées comme celles des PNJ.
                    double yawRad = door.Yaw * Math.PI / 180, along = (m - (members.Count - 1) / 2.0) * 1.1;
                    var (px, pz) = Free(dx + Math.Cos(yawRad) * along + Math.Sin(yawRad) * 0.3, dz - Math.Sin(yawRad) * along + Math.Cos(yawRad) * 0.3, id + "/porche");
                    LifeSpot porch = new LifeSpot(px, pz, door.Yaw, LifePlace.Porch, VillagerActivity.Sit, false);
                    avoidList.Add((px, pz));
                    LifeSpot work = trade == VillagerTrade.Elder ? porch
                        : WorkSpot(trade, id, m, home, house, well, stalls, barns, fieldList, Nearest, Front, Free, Ring, gx, gz);
                    if (work.Place == LifePlace.Work && trade != VillagerTrade.Farmer) avoidList.Add((work.X, work.Z));
                    LifeSpot market = Ring(gx, gz, well != null ? Math.Max(well.MaxX - well.MinX, well.MaxZ - well.MinZ) / 2 + 2.2 : 3.5, id + "/marche",
                        LifePlace.Market, VillagerActivity.Talk);
                    LifeSpot feast = Ring(gx, gz, 5.5, id + "/fete", LifePlace.Feast, StyleRules.Variant(id + "/danse", 2) == 0 ? VillagerActivity.Dance : VillagerActivity.Talk);
                    LifeSpot play = Ring(gx, gz, 6.5, id + "/jeux", LifePlace.Play, VillagerActivity.Talk);
                    residents.Add(new Resident(id, house.Id, trade, CharacterOf(trade, id), ModelOf(trade, id), StyleRules.Variant(id + "/heure", 41) - 20,
                        home, work, market, tavernOf(id), chapelOf(id), ShelterOf(id), feast, play, porch, hamlet));
                }
            }
            return new VillageWorld(residents, roads);
        }

        private sealed class Box
        {
            public Box(string id, StyleBuilding kind, double minX, double minZ, double maxX, double maxZ)
            {
                Id = id;
                Kind = kind;
                MinX = minX;
                MinZ = minZ;
                MaxX = maxX;
                MaxZ = maxZ;
            }

            public string Id { get; }
            public StyleBuilding Kind { get; }
            public double MinX { get; }
            public double MinZ { get; }
            public double MaxX { get; }
            public double MaxZ { get; }
            public double CX => (MinX + MaxX) / 2;
            public double CZ => (MinZ + MaxZ) / 2;

            public double Dist2(double x, double z)
            {
                double dx = Math.Max(0, Math.Max(MinX - x, x - MaxX)), dz = Math.Max(0, Math.Max(MinZ - z, z - MaxZ));
                return dx * dx + dz * dz;
            }
        }

        private static double Dist2Field(StyleField f, double x, double z)
        {
            double dx = Math.Max(0, Math.Max(f.MinX - x, x - f.MaxX)), dz = Math.Max(0, Math.Max(f.MinZ - z, z - f.MaxZ));
            return dx * dx + dz * dz;
        }

        /// <summary>Une maison où l'on vit (pas un poste de péage, un refuge ou une chute de décor).</summary>
        public static bool Dwelling(string id)
        {
            string k = id ?? string.Empty;
            return !(k.Contains("peage") || k.Contains("refuge") || k.Contains("ruine") || k.Contains("effondre"));
        }

        /// <summary>
        /// Métier du premier habitant d'une maison : d'après son nom (forge, armurier → forgeron ; boulangerie, four → boulanger ; apothicaire,
        /// tanneur, mercier, épicier, marchand → marchand ; mine → manœuvre ; berger, ferme → paysan), sinon tiré (<paramref name="roll"/>, 0 à
        /// 99) : paysan près des champs, marchand près des étals, manœuvre sinon.
        /// </summary>
        public static VillagerTrade TradeOf(string houseId, int roll, bool nearFields, bool nearStalls)
        {
            string k = houseId ?? string.Empty;
            if (k.Contains("forge") || k.Contains("armurier")) return VillagerTrade.Smith;
            if (k.Contains("boulang") || k.Contains("four")) return VillagerTrade.Baker;
            if (k.Contains("apothicaire") || k.Contains("tanneur") || k.Contains("mercier") || k.Contains("epicier") || k.Contains("marchand"))
                return VillagerTrade.Merchant;
            if (k.Contains("berger") || k.Contains("ferme")) return VillagerTrade.Farmer;
            if (k.Contains("mineur") || k.Contains("bucheron") || k.Contains("pecheur")) return VillagerTrade.Laborer;
            if (nearFields && roll < 45) return VillagerTrade.Farmer;
            if (nearStalls && roll >= 85) return VillagerTrade.Merchant;
            if (roll >= 80 && roll < 85) return VillagerTrade.Baker;
            if (roll >= 75 && roll < 80) return VillagerTrade.Smith;
            return VillagerTrade.Laborer;
        }

        /// <summary>Rôle des corps humains existants (tenue de <see cref="HumanLooks.OutfitOf"/>).</summary>
        public static string CharacterOf(VillagerTrade trade, string id) => trade switch
        {
            VillagerTrade.Baker => "boulanger",
            VillagerTrade.Smith => "forgeron",
            VillagerTrade.Merchant => "marchand",
            VillagerTrade.Elder => StyleRules.Variant(id + "/ancien", 3) == 0 ? "fidele" : "paysan",
            _ => "paysan",
        };

        /// <summary>
        /// Modèle du kit des villageois (Tripo, préparé le 27 novembre 2026, pas lancé : décision de Damien « pas maintenant ») qui irait à cet
        /// habitant : la vue le prend s'il est au catalogue, sinon <see cref="Resident.Character"/> (repli : rien à retoucher le jour où il arrive).
        /// Un seul mot de lettres (le catalogue ne garde que le premier mot d'un identifiant, <see cref="StyleRules.CharacterOf"/>) :
        /// « habitant » suivi du nom du modèle du kit sans ses tirets bas (V01_paysan : « habitantpaysan », V03_vieil_homme : « habitantvieilhomme »).
        /// </summary>
        public static string ModelOf(VillagerTrade trade, string id)
        {
            bool woman = StyleRules.Variant(id + "/femme", 2) == 0;
            return trade switch
            {
                VillagerTrade.Baker => "habitantboulanger",
                VillagerTrade.Smith => "habitantforgeron",
                VillagerTrade.Merchant => "habitantmarchande",
                VillagerTrade.Farmer => woman ? "habitantpaysanne" : "habitantfermier",
                VillagerTrade.Elder => woman ? "habitantvieillefemme" : "habitantvieilhomme",
                VillagerTrade.Child => woman ? "habitantfillette" : "habitantgarcon",
                VillagerTrade.Washer => "habitantpaysanne",
                _ => woman ? "habitantpaysanne" : "habitantpaysan",
            };
        }

        private static LifeSpot WorkSpot(VillagerTrade trade, string id, int member, LifeSpot home, Box house, Box? well, List<Box> stalls, List<Box> barns,
            List<StyleField> fields, Func<List<Box>, double, double, double, Box?> nearest, Func<Box, double, (double X, double Z, double Yaw)> front,
            Func<double, double, string, (double X, double Z)> free, Func<double, double, double, string, LifePlace, VillagerActivity, LifeSpot> ring,
            double gx, double gz)
        {
            switch (trade)
            {
                case VillagerTrade.Farmer:
                {
                    StyleField? best = null;
                    double bd = 260.0 * 260.0;
                    foreach (StyleField f in fields)
                    {
                        double d = Dist2Field(f, house.CX, house.CZ);
                        if (d < bd)
                        {
                            bd = d;
                            best = f;
                        }
                    }
                    if (best is StyleField field)
                    {
                        double u = 0.15 + 0.7 * StyleRules.Variant(id + "/champ/x", 100) / 99.0, v = 0.15 + 0.7 * StyleRules.Variant(id + "/champ/z", 100) / 99.0;
                        double x = field.MinX + (field.MaxX - field.MinX) * u, z = field.MinZ + (field.MaxZ - field.MinZ) * v;
                        return new LifeSpot(x, z, StyleRules.Variant(id + "/champ/cap", 4) * 90.0, LifePlace.Work, VillagerActivity.Gather, false);
                    }
                    Box? barn = nearest(barns, house.CX, house.CZ, 200);
                    if (barn != null) return ring(barn.CX, barn.CZ, Math.Max(barn.MaxX - barn.MinX, barn.MaxZ - barn.MinZ) / 2 + 1.5, id + "/grange",
                        LifePlace.Work, VillagerActivity.Gather);
                    break;
                }
                case VillagerTrade.Merchant:
                {
                    Box? stall = nearest(stalls, house.CX, house.CZ, 160);
                    if (stall != null)
                    {
                        var f = front(stall, -0.8 - Math.Min(stall.MaxX - stall.MinX, stall.MaxZ - stall.MinZ));
                        var (x, z) = free(f.X, f.Z, id + "/etal");
                        return new LifeSpot(x, z, f.Yaw, LifePlace.Work, VillagerActivity.Talk, false);
                    }
                    break;
                }
                case VillagerTrade.Washer:
                    if (well != null) return ring(well.CX, well.CZ, Math.Max(well.MaxX - well.MinX, well.MaxZ - well.MinZ) / 2 + 0.9, id + "/lavoir",
                        LifePlace.Work, VillagerActivity.Gather);
                    break;
                case VillagerTrade.Elder:
                    return new LifeSpot(home.X, home.Z, home.Yaw, LifePlace.Porch, VillagerActivity.Sit, false);
                case VillagerTrade.Child:
                    return ring(gx, gz, 6.5, id + "/jeux", LifePlace.Play, VillagerActivity.Talk);
            }
            // Devant chez soi, à l'ouvrage (le fournil, la forge, l'atelier sont dans la maison : on travaille à la porte).
            double a = (home.Yaw + (StyleRules.Variant(id + "/cour", 2) == 0 ? 1 : -1) * (60 + 25 * member)) * Math.PI / 180;
            var (wx, wz) = free(home.X + Math.Sin(a) * 1.6, home.Z + Math.Cos(a) * 1.6, id + "/cour");
            return new LifeSpot(wx, wz, home.Yaw, LifePlace.Work, VillagerActivity.Gather, false);
        }

        // ---------------------------------------------------------------------------------------------------------------- journée

        /// <summary>Minute ramenée dans le jour (0 à 1439).</summary>
        public static int Wrap(int minute) => ((minute % 1440) + 1440) % 1440;

        /// <summary>
        /// La journée de l'habitant <paramref name="r"/> sous le contexte <paramref name="c"/> : des créneaux triés (début de 0 à 1439). Sans
        /// fête ni averse : travail selon le métier, repas à midi chez soi, marché, taverne ou veillée le soir, maison la nuit. En hiver on se
        /// lève une demi-heure plus tard et on rentre une heure plus tôt (le paysan fend du bois devant chez lui) ; en été on veille une heure
        /// de plus. Jour de fête : on va sur la place l'après-midi et le soir (les marchands gardent leur étal). Averse : chaque place du
        /// dehors devient l'abri (un enfant rentre).
        /// </summary>
        public static IReadOnlyList<LifeSlot> Day(Resident r, VillageContext c)
        {
            if (r is null) throw new ArgumentNullException(nameof(r));
            bool winter = c.Season == WardrobeSeason.Winter, summer = c.Season == WardrobeSeason.Summer;
            int wake = winter ? 30 : 0, evening = winter ? -60 : summer ? 60 : 0;
            int o = r.Offset;
            var s = new List<(int Start, LifeSpot Spot)>();
            void Add(int start, LifeSpot spot) => s.Add((start + o, spot));
            LifeSpot home = r.Home, meal = r.Home.With(LifePlace.Meal, VillagerActivity.Idle, true);
            LifeSpot evenTavern = r.Tavern ?? r.Market.With(LifePlace.Tavern, VillagerActivity.Talk, false);   // hameau : la veillée au milieu des maisons
            LifeSpot work = r.Work;
            if (winter && r.Trade == VillagerTrade.Farmer)
                work = new LifeSpot(r.Home.X + Math.Sin(r.Home.Yaw * Math.PI / 180) * 1.8, r.Home.Z + Math.Cos(r.Home.Yaw * Math.PI / 180) * 1.8,
                    r.Home.Yaw + 90, LifePlace.Work, VillagerActivity.Gather, false);   // il fend du bois devant chez lui
            switch (r.Trade)
            {
                case VillagerTrade.Baker:
                    Add(0, home);
                    Add(3 * 60 + 30, home.With(LifePlace.Work, VillagerActivity.Gather, true));   // le pain se fait la nuit, au fournil
                    Add(7 * 60, work);   // il vend à sa porte
                    Add(11 * 60 + 30, r.Market.With(LifePlace.Market, VillagerActivity.Talk, false));
                    Add(13 * 60, home.With(LifePlace.Home, VillagerActivity.Idle, true));   // la sieste
                    Add(15 * 60 + wake, work);
                    Add(19 * 60 + evening / 2, evenTavern);
                    Add(21 * 60 + evening / 2, home);
                    break;
                case VillagerTrade.Smith:
                    Add(0, home);
                    Add(6 * 60 + 30 + wake, work);
                    Add(12 * 60, meal);
                    Add(13 * 60, work);
                    Add(19 * 60 + evening / 2, evenTavern);
                    Add(22 * 60 + evening, home);
                    break;
                case VillagerTrade.Merchant:
                    Add(0, home);
                    Add(7 * 60 + wake, work);
                    Add(12 * 60 + 30, meal);
                    Add(13 * 60 + 30, work);
                    Add(18 * 60 + 30 + evening / 2, r.Market);
                    Add(20 * 60 + evening, home);
                    break;
                case VillagerTrade.Farmer:
                    Add(0, home);
                    Add(5 * 60 + 30 + wake, work);
                    Add(12 * 60, meal);
                    Add(13 * 60, work);
                    Add(18 * 60 + 30 + evening, home.With(LifePlace.Home, VillagerActivity.Idle, true));
                    if (!winter) Add(19 * 60 + 30 + evening, evenTavern);
                    Add(21 * 60 + 30 + evening, home);
                    break;
                case VillagerTrade.Washer:
                    Add(0, home);
                    Add(7 * 60 + wake, work);
                    Add(10 * 60, c.MarketDay ? r.Market : work);
                    Add(11 * 60 + 30, meal);
                    Add(13 * 60 + 30, r.Porch.With(LifePlace.Porch, VillagerActivity.Gather, false));
                    Add(16 * 60, r.Market);
                    Add(18 * 60 + evening, home);
                    break;
                case VillagerTrade.Elder:
                    Add(0, home);
                    Add(8 * 60 + wake, r.Porch);
                    Add(10 * 60, r.Chapel ?? r.Porch);
                    Add(11 * 60, r.Market);
                    Add(12 * 60, meal);
                    Add(14 * 60 + 30, r.Porch.With(LifePlace.Porch, VillagerActivity.Look, false));
                    Add(17 * 60, r.Chapel ?? r.Market);
                    Add(18 * 60 + (summer ? 90 : 0) + (winter ? -60 : 0), home);
                    break;
                case VillagerTrade.Child:
                    Add(0, home);
                    Add(8 * 60 + wake, r.Play);
                    Add(10 * 60 + 30, c.MarketDay ? r.Market : r.Play.With(LifePlace.Play, VillagerActivity.Look, false));
                    Add(12 * 60, meal);
                    Add(13 * 60 + 30, r.Play);
                    Add(15 * 60 + 30, r.Porch.With(LifePlace.Porch, VillagerActivity.Talk, false));
                    Add(16 * 60 + 30, r.Play);
                    Add(18 * 60 + evening / 2, home);
                    break;
                default:
                    Add(0, home);
                    Add(6 * 60 + 30 + wake, work);
                    Add(9 * 60, c.MarketDay || StyleRules.Variant(r.Id + "/marche", 2) == 0 ? r.Market : work);
                    Add(10 * 60 + 15, work);
                    Add(12 * 60, meal);
                    Add(13 * 60 + 30, work);
                    Add(18 * 60 + evening / 2, r.Market);
                    Add(19 * 60 + evening / 2, StyleRules.Variant(r.Id + "/taverne", 3) == 0 ? evenTavern : r.Porch);
                    Add(21 * 60 + evening, home);
                    break;
            }

            if (c.Festive)
            {
                // Jour de fête : l'après-midi et la soirée sur la place (les marchands gardent leur étal jusqu'au soir), puis on rentre tard.
                bool keepsStall = r.Trade == VillagerTrade.Merchant;
                s.RemoveAll(x => x.Start - o >= 15 * 60 && !(keepsStall && x.Start - o < 18 * 60));
                if (!keepsStall) s.Add((15 * 60 + o, r.Feast.With(LifePlace.Feast, VillagerActivity.Talk, false)));
                s.Add((18 * 60 + o, r.Feast));
                s.Add(((r.Child ? 21 * 60 : r.Elder ? 21 * 60 + 30 : 23 * 60 + 15) + o, home));
            }

            // Ordre, minute ramenée dans le jour, doublons à la même minute (le dernier ajouté l'emporte).
            var slots = new SortedDictionary<int, LifeSpot>();
            foreach (var (start, spot) in s)
            {
                LifeSpot place = spot;
                if (c.Shelter && !place.Indoors)
                    place = r.Child ? r.Home.With(LifePlace.Shelter, VillagerActivity.Idle, true)
                        : place.Place == LifePlace.Tavern && r.Tavern != null ? place.With(LifePlace.Tavern, VillagerActivity.Drink, true)   // dans la salle
                        : r.Shelter;
                slots[Wrap(start)] = place;
            }
            return slots.Select(kv => new LifeSlot(kv.Key, kv.Value)).ToList();
        }

        /// <summary>Indice du créneau en cours à la minute <paramref name="minute"/> (le dernier commencé ; avant le premier : le dernier d'hier).</summary>
        public static int SlotAt(IReadOnlyList<LifeSlot> day, int minute)
        {
            if (day is null || day.Count == 0) throw new ArgumentException("journée vide", nameof(day));
            int m = Wrap(minute), found = day.Count - 1;
            for (int i = 0; i < day.Count; i++)
                if (day[i].Start <= m) found = i;
            return found;
        }

        /// <summary>Place visée par l'habitant à la minute <paramref name="minute"/> (sans le trajet).</summary>
        public static LifeSpot Target(Resident r, int minute, VillageContext c)
        {
            var day = Day(r, c);
            return day[SlotAt(day, minute)].Spot;
        }

        /// <summary>
        /// Créneaux remontés pour trouver d'où part un trajet (relecture indépendante de la PR #268) : un trajet plus long que son créneau
        /// n'est pas fini quand le suivant commence ; le suivant part alors de là où il en était (jamais de saut à travers les maisons),
        /// en remontant au plus ce nombre de créneaux (au-delà, la place du créneau d'avant est tenue pour atteinte).
        /// </summary>
        public const int LegDepth = 3;

        /// <summary>
        /// Où est l'habitant <paramref name="r"/> à la minute <paramref name="minute"/> (avec sa fraction) : parti, au début du créneau en cours,
        /// de là où son trajet précédent l'avait mené (la place du créneau précédent s'il l'a atteinte), il suit le trajet par les routes
        /// (<see cref="VillageRoads.Route(double, double, double, double)"/>) à son allure, puis se tient à sa place (caché s'il est entré :
        /// maison, salle de l'auberge). Fonction pure : même minute, même contexte, même réponse.
        /// </summary>
        public static LifePose At(VillageWorld world, Resident r, double minute, VillageContext c)
        {
            if (world is null) throw new ArgumentNullException(nameof(world));
            if (r is null) throw new ArgumentNullException(nameof(r));
            double m = double.IsNaN(minute) || double.IsInfinity(minute) ? 0 : ((minute % 1440) + 1440) % 1440;
            var day = world.DayOf(r, c);
            int i = SlotAt(day, (int)Math.Floor(m));
            double elapsed = m - day[i].Start;
            if (elapsed < 0) elapsed += 1440;
            var (fx, fz, _) = Departure(world, r, day, i, c, LegDepth);
            return Walk(world, r, fx, fz, day[i].Spot, elapsed, c);
        }

        /// <summary>Durée (minutes) du créneau <paramref name="i"/> (jusqu'au début du suivant, minuit passé compris).</summary>
        public static int Duration(IReadOnlyList<LifeSlot> day, int i)
        {
            int next = day[(i + 1) % day.Count].Start, start = day[i].Start;
            int d = next - start;
            return d <= 0 ? d + 1440 : d;
        }

        /// <summary>Point de départ du créneau <paramref name="i"/> : où en était le trajet du créneau précédent à sa fin.</summary>
        private static (double X, double Z, bool Arrived) Departure(VillageWorld world, Resident r, IReadOnlyList<LifeSlot> day, int i, VillageContext c, int depth)
        {
            int p = (i + day.Count - 1) % day.Count;
            LifeSpot prev = day[p].Spot;
            if (depth <= 0 || day.Count < 2) return (prev.X, prev.Z, true);
            var (fx, fz, _) = Departure(world, r, day, p, c, depth - 1);
            LifePose end = Walk(world, r, fx, fz, prev, Duration(day, p), c);
            return (end.X, end.Z, !end.Moving);
        }

        private static LifePose Walk(VillageWorld world, Resident r, double fromX, double fromZ, LifeSpot to, double elapsed, VillageContext c)
        {
            if (Math.Abs(fromX - to.X) < 0.05 && Math.Abs(fromZ - to.Z) < 0.05)
                return new LifePose(to.X, to.Z, to.Yaw, to.Activity, to.Place, false, to.Indoors);
            var path = world.Roads.Route(fromX, fromZ, to.X, to.Z);
            double walked = elapsed * c.MetersPerMinute * r.Pace;
            if (walked >= VillageRoads.Length(path)) return new LifePose(to.X, to.Z, to.Yaw, to.Activity, to.Place, false, to.Indoors);
            var (x, z, yaw) = VillageRoads.Along(path, walked);
            return new LifePose(x, z, yaw, VillagerActivity.Walk, to.Place, true, false);
        }

        /// <summary>
        /// Minutes de marche du trajet du créneau <paramref name="i"/> (depuis la place du créneau précédent, par les routes, à l'allure de
        /// l'habitant) : pour vérifier qu'il tient dans son créneau.
        /// </summary>
        public static double TravelMinutes(VillageWorld world, Resident r, IReadOnlyList<LifeSlot> day, int i, VillageContext c)
        {
            LifeSpot from = day[(i + day.Count - 1) % day.Count].Spot, to = day[i].Spot;
            if (Math.Abs(from.X - to.X) < 0.05 && Math.Abs(from.Z - to.Z) < 0.05) return 0;
            return VillageRoads.Length(world.Roads.Route(from.X, from.Z, to.X, to.Z)) / (c.MetersPerMinute * r.Pace);
        }

        // ---------------------------------------------------------------------------------------------------------------- autour de la caméra

        /// <summary>
        /// Habitants montrés au plus en même temps, selon la qualité (le PC de Damien est en Basse : PC modeste) : téléphone 6, PC modeste 10,
        /// Moyenne 18, Haute 28.
        /// </summary>
        public static int Cap(DeviceTier tier, bool light) => tier == DeviceTier.Phone ? 6 : tier == DeviceTier.Modest ? 10 : light ? 18 : 28;

        /// <summary>Rayon (m) autour de la caméra où les habitants sont construits : téléphone 45, PC modeste 60, PC complet 90.</summary>
        public static double Radius(DeviceTier tier) => tier == DeviceTier.Phone ? 45 : tier == DeviceTier.Modest ? 60 : 90;

        /// <summary>Marge (m) des détours par les routes au-delà de la boîte des places d'un habitant (tri rapide de <see cref="Near"/>).</summary>
        public const double RouteMargin = 120;

        /// <summary>
        /// Les habitants à montrer : ceux qui sont dehors (pas rentrés) dans le rayon <paramref name="radius"/> autour de (<paramref name="x"/>,
        /// <paramref name="z"/>), les plus proches d'abord, <paramref name="cap"/> au plus ; à égalité, par identifiant. Indices dans
        /// <see cref="VillageWorld.Residents"/> ; <paramref name="poses"/> reçoit la pose de chacun (même ordre).
        /// </summary>
        public static IReadOnlyList<int> Near(VillageWorld world, double minute, VillageContext c, double x, double z, double radius, int cap,
            IList<LifePose>? poses = null) => Pick(world, minute, c, x, z, radius, cap, null, poses);

        /// <summary>
        /// Marge (m) de sortie (relecture indépendante de la PR #268) : un habitant déjà montré le reste jusqu'au rayon plus cette marge ; un
        /// nouveau n'entre qu'au rayon. Pas d'apparitions et de disparitions en boucle à la lisière.
        /// </summary>
        public const double KeepMargin = 15;

        /// <summary>
        /// Comme <see cref="Near"/>, avec hystérésis : ceux de <paramref name="current"/> (déjà montrés) restent tant qu'ils sont dehors à moins
        /// de <paramref name="radius"/> + <see cref="KeepMargin"/> m, et passent avant les nouveaux ; puis les nouveaux, dans le rayon, les plus
        /// proches d'abord ; <paramref name="cap"/> au plus en tout. <paramref name="shownAt"/> : où sont affichés les corps encore visibles
        /// (indice → place) ; un habitant montré qui y figure est jugé à cette place, même si sa pose théorique est déjà rentrée.
        /// </summary>
        public static IReadOnlyList<int> Pick(VillageWorld world, double minute, VillageContext c, double x, double z, double radius, int cap,
            ICollection<int>? current, IList<LifePose>? poses = null, IReadOnlyDictionary<int, (double X, double Z)>? shownAt = null)
        {
            if (world is null) throw new ArgumentNullException(nameof(world));
            poses?.Clear();
            if (cap <= 0 || !(radius > 0)) return Array.Empty<int>();
            var found = new List<(bool Kept, double D, int I, LifePose P)>();
            double outer = radius + KeepMargin;
            for (int i = 0; i < world.Residents.Count; i++)
            {
                Resident r = world.Residents[i];
                // Tri rapide : loin de toutes ses places (marge des détours par les routes), il ne peut pas être là.
                double bx = Math.Max(0, Math.Max(r.MinX - x, x - r.MaxX)), bz = Math.Max(0, Math.Max(r.MinZ - z, z - r.MaxZ));
                if (Math.Sqrt(bx * bx + bz * bz) > outer + RouteMargin) continue;
                LifePose p = At(world, r, minute, c);
                bool kept = current != null && current.Contains(i);
                // Revue Codex de la PR #268 : un habitant montré qui rentre (chez lui, à l'abri d'une salle) est jugé là où son corps est
                // encore (shownAt), pas à sa pose théorique déjà cachée : il garde son corps jusqu'à la porte.
                if (kept && shownAt != null && shownAt.TryGetValue(i, out var seenAt))
                {
                    double ds = (seenAt.X - x) * (seenAt.X - x) + (seenAt.Z - z) * (seenAt.Z - z);
                    if (ds <= outer * outer) found.Add((true, ds, i, p));
                    continue;
                }
                if (p.Hidden) continue;
                double d = (p.X - x) * (p.X - x) + (p.Z - z) * (p.Z - z);
                if (d <= (kept ? outer * outer : radius * radius)) found.Add((kept, d, i, p));
            }
            var chosen = found.OrderBy(f => f.Kept ? 0 : 1).ThenBy(f => f.D).ThenBy(f => world.Residents[f.I].Id, StringComparer.Ordinal).Take(cap).ToList();
            if (poses != null) foreach (var f in chosen) poses.Add(f.P);
            return chosen.Select(f => f.I).ToList();
        }

        // ---------------------------------------------------------------------------------------------------------------- petites vies

        /// <summary>Distance (m) où un habitant arrêté tourne la tête (le corps) vers le joueur.</summary>
        public const double AttentionRange = 7;

        /// <summary>Distance (m) où il salue le joueur (une fois par passage : la vue se souvient qu'il l'a fait).</summary>
        public const double GreetRange = 4.5;

        /// <summary>Il regarde le joueur : arrêté, pas en train de travailler aux champs, assez près.</summary>
        public static bool LooksAtPlayer(LifePose pose, double distance) =>
            !pose.Hidden && !pose.Moving && distance <= AttentionRange && pose.Activity != VillagerActivity.Gather;

        /// <summary>Il salue : comme <see cref="LooksAtPlayer"/>, plus près, et seulement de 6 h à 22 h (la nuit, on ne parle pas fort).</summary>
        public static bool Greets(LifePose pose, double distance, int minute) =>
            !pose.Hidden && distance <= GreetRange && Wrap(minute) >= 6 * 60 && Wrap(minute) < 22 * 60;

        private static readonly string[] Morning = { "Bonjour !", "Le bonjour à vous.", "Belle matinée.", "Bien le bonjour, voyageur." };
        private static readonly string[] Day_ = { "Bonne journée !", "Salut à vous.", "Le Seuil vous garde.", "Ah, l'aventurier !" };
        private static readonly string[] Evening = { "Bonsoir.", "Bonne soirée !", "Rentrez avant la nuit.", "Bonsoir, voyageur." };
        private static readonly string[] Rain = { "Quel temps…", "Mettez-vous à l'abri !", "Ça tombe dru." };
        private static readonly string[] Feast = { "Joyeuse fête !", "Venez danser !", "Belle fête, hein ?" };
        private static readonly string[] Winter = { "Brr, il pince ce matin.", "Couvrez-vous bien." };
        private static readonly string[] Kids = { "C'est toi l'aventurier ?", "T'as une épée ?", "On joue au loup !" };
        private static readonly string[] Old = { "De mon temps, on saluait.", "Ah, la jeunesse…", "Que le Seuil vous garde." };

        /// <summary>
        /// Salut de l'habitant <paramref name="r"/> au joueur (textes courts, tirés de son identifiant et de <paramref name="turn"/>, le nombre
        /// de saluts déjà faits) : selon l'heure, le temps, la fête, l'hiver, et s'il est enfant ou ancien.
        /// </summary>
        public static string Greeting(Resident r, int minute, VillageContext c, int turn)
        {
            if (r is null) throw new ArgumentNullException(nameof(r));
            int m = Wrap(minute);
            string[] pool = r.Child ? Kids : c.Festive ? Feast : c.Shelter ? Rain : r.Elder && turn % 2 == 0 ? Old
                : c.Season == WardrobeSeason.Winter && m < 11 * 60 ? Winter : m < 11 * 60 ? Morning : m < 18 * 60 ? Day_ : Evening;
            return pool[StyleRules.Variant(r.Id + "/salut/" + turn, pool.Length)];
        }

        private static readonly string[] MarketTalk =
        {
            "Le pain de ce matin était encore chaud.", "Tu as vu le prix du sel ?", "Paraît qu'on a vu des loups près du lac.",
            "Ma fille se marie au printemps.", "La caravane d'Ambre est en retard.", "Encore des bruits du côté du Seuil…",
        };
        private static readonly string[] TavernTalk = { "À la tienne !", "Une autre, Mathurin !", "Et là, l'ours s'est levé…", "Demain, je me lève tôt. Promis." };
        private static readonly string[] FeastTalk = { "Quelle musique !", "Tu danses ?", "Les lanternes sont belles cette année.", "Goûte-moi ça !" };
        private static readonly string[] RainTalk = { "Ça ne va pas s'arrêter.", "Mes semis vont noyer.", "Au moins, le puits sera plein." };
        private static readonly string[] PlayTalk = { "Touché !", "À toi de compter !", "Je t'ai vu !", "On va voir le moulin ?" };
        private static readonly string[] SeasonTalk = { "Le printemps est en avance.", "Quelle chaleur !", "Les pommes sont belles.", "Le lac a gelé au bord." };

        /// <summary>
        /// Réplique numéro <paramref name="turn"/> d'une conversation entre deux habitants arrêtés au même endroit (place, taverne, fête,
        /// abri, jeux) ; les deux parlent à tour de rôle. Toujours la même pour la même paire et le même tour.
        /// </summary>
        public static string ChatLine(string a, string b, LifePlace place, VillageContext c, int turn)
        {
            string[] pool = place switch
            {
                LifePlace.Tavern => TavernTalk,
                LifePlace.Feast => FeastTalk,
                LifePlace.Shelter => RainTalk,
                LifePlace.Play => PlayTalk,
                _ => turn % 3 == 2 ? new[] { SeasonTalk[(int)c.Season] } : MarketTalk,
            };
            string pair = string.CompareOrdinal(a ?? string.Empty, b ?? string.Empty) <= 0 ? a + "|" + b : b + "|" + a;
            return pool[StyleRules.Variant(pair + "/" + turn, pool.Length)];
        }

        /// <summary>Distance (m) entre deux habitants arrêtés pour qu'ils se parlent.</summary>
        public const double ChatRange = 3.2;

        /// <summary>Deux habitants arrêtés, dehors, à une place où l'on bavarde, assez près : ils se parlent.</summary>
        public static bool Chatting(LifePose a, LifePose b)
        {
            bool Talky(LifePose p) => !p.Hidden && !p.Moving && p.Place != LifePlace.Work && p.Place != LifePlace.Home;
            double d2 = (a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z);
            return Talky(a) && Talky(b) && d2 <= ChatRange * ChatRange;
        }
    }
}
