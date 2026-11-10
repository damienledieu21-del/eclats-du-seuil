using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EdS.Data.Merchants;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.14 : un étal du grand jour de marché, pour l'affichage (genre, place, orientation vers la place, enseigne traduite).</summary>
    public sealed class MarketStallView
    {
        public string Id { get; }
        public string Kind { get; }
        public double X { get; }
        public double Z { get; }

        /// <summary>Orientation (degrés, 0 : face vers +z) : l'étal regarde le milieu de la place.</summary>
        public double Face { get; }

        public string? Sign { get; }
        public string? Merchant { get; }

        internal MarketStallView(string id, string kind, double x, double z, double face, string? sign, string? merchant)
        {
            Id = id;
            Kind = kind;
            X = x;
            Z = z;
            Face = face;
            Sign = sign;
            Merchant = merchant;
        }
    }

    /// <summary>Sous-lot V2.14 : une cheminée de boutique (le fournil) et si le four est allumé (fumée).</summary>
    public readonly struct ShopChimney
    {
        public ShopChimney(string id, double x, double z, bool lit)
        {
            Id = id;
            X = x;
            Z = z;
            Lit = lit;
        }

        public string Id { get; }
        public double X { get; }
        public double Z { get; }
        public bool Lit { get; }
    }

    /// <summary>
    /// Sous-lot V2.14 (décision de Damien du 9 novembre 2026) : l'affichage des vraies boutiques et du grand jour de marché : enseignes aux
    /// portes des maisons (avec ouverte ou fermée), fumée du fournil quand le four chauffe, étals de la place, foule des jours de marché,
    /// criées. Affichage seulement : le cœur (<see cref="WorldSession"/>) reste maître des horaires, des stocks et des prix.
    /// </summary>
    public sealed partial class AdventureSession
    {
        /// <summary>Mètres entre la porte et l'enseigne, le long de la façade.</summary>
        public const double SignOffset = 1.3;

        /// <summary>Le four chauffe dès trois heures et demie avant la première fournée (on pétrit), jusqu'à une heure après la dernière.</summary>
        public const int OvenBefore = 210, OvenAfter = 60;

        /// <summary>Sous-lot V2.14 : où en est la grande place.</summary>
        public MarketPhase MarketPhaseNow => World.Market is null ? MarketPhase.Calm : World.MarketPhase;

        /// <summary>Le joueur est dehors, dans la zone de l'histoire (le bourg).</summary>
        private bool InTown => !Indoors && World.InStoryZone && World.Market != null;

        /// <summary>
        /// Sous-lot V2.14 : étals du marché à afficher dans cette zone (dehors, zone de l'histoire), chacun tourné vers le milieu des étals ;
        /// vide ailleurs ou sans marché dans les données. La phase (<see cref="MarketPhaseNow"/>) dit ce qui en est monté.
        /// </summary>
        public IReadOnlyList<MarketStallView> MarketStalls()
        {
            if (!InTown || !(World.MarketDay is MarketDay md)) return Array.Empty<MarketStallView>();
            double cx = md.Stalls.Average(s => s.Position.X.Raw / 10000.0), cz = md.Stalls.Average(s => s.Position.Z.Raw / 10000.0);
            return md.Stalls.Select(s =>
            {
                double x = s.Position.X.Raw / 10000.0, z = s.Position.Z.Raw / 10000.0;
                return new MarketStallView(s.Id, s.Kind, x, z, StyleRules.FaceYaw(x, z, cx, cz), s.Sign is null ? null : Text(s.Sign), s.Merchant);
            }).ToList();
        }

        /// <summary>
        /// Sous-lot V2.14 : villageois de la foule du marché, autour des étals (devant eux, à 1,4 à 2,2 m), hors des obstacles de la carte ; au
        /// plus <see cref="MarketDay.Crowd"/>. Mêmes données, même foule. Montrée pendant le marché seulement (<see cref="MarketCrowdShown"/>).
        /// </summary>
        public IReadOnlyList<StyleVillager> MarketCrowd()
        {
            if (!InTown || !(World.MarketDay is MarketDay md) || md.Crowd == 0) return Array.Empty<StyleVillager>();
            IReadOnlyList<MarketStallView> stalls = MarketStalls();
            var list = new List<StyleVillager>();
            AdventureMap map = Map;
            bool Free(double x, double z) =>
                map.Obstacles.All(o => x < o.Points.Min(p => p.X.Raw) / 10000.0 - 0.5 || x > o.Points.Max(p => p.X.Raw) / 10000.0 + 0.5
                    || z < o.Points.Min(p => p.Z.Raw) / 10000.0 - 0.5 || z > o.Points.Max(p => p.Z.Raw) / 10000.0 + 0.5)
                && list.All(v => (v.X - x) * (v.X - x) + (v.Z - z) * (v.Z - z) > 0.8 * 0.8)
                && stalls.All(s => (s.X - x) * (s.X - x) + (s.Z - z) * (s.Z - z) > 1.2 * 1.2);
            VillagerActivity[] moods = { VillagerActivity.Talk, VillagerActivity.Look, VillagerActivity.Idle, VillagerActivity.Talk };
            for (int i = 0; i < md.Crowd * 3 && list.Count < md.Crowd; i++)
            {
                MarketStallView s = stalls[i % stalls.Count];
                string id = "marche/foule/" + i.ToString(CultureInfo.InvariantCulture);
                double angle = (s.Face + (StyleRules.Variant(id, 120) - 60)) * Math.PI / 180, reach = 1.4 + StyleRules.Variant(id + "/r", 9) * 0.1;
                double x = s.X + Math.Sin(angle) * reach, z = s.Z + Math.Cos(angle) * reach;
                if (!Free(x, z)) continue;
                list.Add(new StyleVillager(id, StyleRules.Variant(id + "/g", 5) == 0 ? "garde" : "paysan", moods[StyleRules.Variant(id + "/m", moods.Length)], x, z,
                    StyleRules.FaceYaw(x, z, s.X, s.Z)));
            }
            return list;
        }

        /// <summary>Sous-lot V2.14 : la foule est sur la place (le marché bat son plein).</summary>
        public bool MarketCrowdShown => InTown && MarketPhaseNow == MarketPhase.Open;

        /// <summary>
        /// Sous-lot V2.14 : enseignes des boutiques de cette zone, à côté de la porte de leur maison (le long de la façade) : « Boulangerie de
        /// Firmin · ouverte », « … · fermée (Bastien répond à la porte) », « … · fermée : jour de repos ».
        /// </summary>
        public IReadOnlyList<WorldMarker> ShopSigns()
        {
            if (Indoors || !(World.Market is EdS.World.Market market)) return Array.Empty<WorldMarker>();
            var signs = new List<WorldMarker>();
            foreach (Merchant m in market.Book.Merchants)
            {
                if (!(m.Shop is Shop shop) || shop.Building is null) continue;
                Door? door = _zone.Doors.FirstOrDefault(d => d.Building == shop.Building);
                SpaceObstacle? house = Map.Obstacles.FirstOrDefault(o => o.Id == shop.Building);
                if (door is null || house is null) continue;
                double dx = door.Position.X.Raw / 10000.0, dz = door.Position.Z.Raw / 10000.0;
                double hx = (house.Points.Min(p => p.X.Raw) + house.Points.Max(p => p.X.Raw)) / 20000.0, hz = (house.Points.Min(p => p.Z.Raw) + house.Points.Max(p => p.Z.Raw)) / 20000.0;
                // Le long de la façade : perpendiculaire à la direction porte → milieu de la maison.
                double nx = hx - dx, nz = hz - dz, n = Math.Sqrt(nx * nx + nz * nz);
                if (n < 1e-6) continue;
                bool alongX = Math.Abs(nz) >= Math.Abs(nx);
                double sx = dx + (alongX ? SignOffset : 0) + (nx / n) * 0.6, sz = dz + (alongX ? 0 : SignOffset) + (nz / n) * 0.6;
                signs.Add(new WorldMarker("enseigne/" + m.Id, Text(shop.Sign) + " · " + ShopStateText(m.Id), new GroundVector(ToFixed(sx), ToFixed(sz))));
            }
            return signs;
        }

        /// <summary>
        /// Sous-lot V2.17 : obstacle de la maison de la boutique d'une enseigne (« enseigne/&lt;marchand&gt; ») ou d'une cheminée
        /// (« cheminee/&lt;marchand&gt; ») ; null sinon.
        /// </summary>
        public string? ShopBuildingOf(string? markerId)
        {
            if (string.IsNullOrEmpty(markerId) || !(World.Market is EdS.World.Market market)) return null;
            int slash = markerId!.IndexOf('/');
            string merchant = slash >= 0 ? markerId.Substring(slash + 1) : markerId;
            foreach (Merchant m in market.Book.Merchants)
                if (m.Id == merchant && m.Shop is Shop shop) return shop.Building;
            return null;
        }

        private static EdS.Domain.Numerics.Fixed ToFixed(double v) => EdS.Domain.Numerics.Fixed.FromRaw((long)Math.Round(v * 10000.0));

        /// <summary>Sous-lot V2.14 : état d'une boutique en mots (« ouverte », « fermée », « fermée : jour de repos », « à la porte »).</summary>
        public string ShopStateText(string merchant)
        {
            switch (World.ShopStateOf(merchant))
            {
                case ShopState.Open: return Text("text.v214.sign.open");
                case ShopState.AtDoor: return Text("text.v214.sign.door");
                case ShopState.RestDay: return Text("text.v214.sign.rest");
                default: return Text("text.v214.sign.closed");
            }
        }

        /// <summary>
        /// Sous-lot V2.14 : cheminées des boutiques à fournées (le fournil) de cette zone, au milieu de leur maison, et si le four chauffe :
        /// de <see cref="OvenBefore"/> minutes avant la première fournée à <see cref="OvenAfter"/> après la dernière, sauf le jour de repos.
        /// </summary>
        public IReadOnlyList<ShopChimney> Chimneys()
        {
            if (Indoors || !(World.Market is EdS.World.Market market)) return Array.Empty<ShopChimney>();
            var list = new List<ShopChimney>();
            int minute = World.Zone.Time.Minute;
            int weekday = EdS.Data.Story.WorldCalendar.WeekdayOf(World.Zone.Time.Day);
            foreach (Merchant m in market.Book.Merchants)
            {
                if (!(m.Shop is Shop shop) || shop.Building is null || m.Bakes.Count == 0) continue;
                SpaceObstacle? house = Map.Obstacles.FirstOrDefault(o => o.Id == shop.Building);
                if (house is null) continue;
                double hx = (house.Points.Min(p => p.X.Raw) + house.Points.Max(p => p.X.Raw)) / 20000.0, hz = (house.Points.Min(p => p.Z.Raw) + house.Points.Max(p => p.Z.Raw)) / 20000.0;
                bool lit = !shop.Rest.Contains(weekday) && minute >= Math.Max(0, m.Bakes[0].At - OvenBefore) && minute < m.Bakes[m.Bakes.Count - 1].At + OvenAfter;
                list.Add(new ShopChimney("cheminee/" + m.Id, hx, hz, lit));
            }
            return list;
        }

        /// <summary>
        /// V2.19 : cheminées de cuisine des auberges (boutiques de genre « inn », identifiant « cuisine/… ») de cette zone, au milieu de leur
        /// maison, allumées tant que l'auberge est ouverte (<see cref="ShopState.Open"/>). V2.21 : plus le feu d'âtre des épiceries (genre
        /// « grocer », identifiant « atre/… », décision de Damien du 10 novembre 2026 au soir : la cheminée de l'épicerie fume pendant les
        /// heures d'ouverture). Affichage seulement : la vue n'en fait fumer que la cheminée connue d'un modèle de l'atelier
        /// (<see cref="TownModels.OwnChimney"/>), jamais une cheminée devinée.
        /// </summary>
        public IReadOnlyList<ShopChimney> KitchenChimneys()
        {
            if (Indoors || !(World.Market is EdS.World.Market market)) return Array.Empty<ShopChimney>();
            var list = new List<ShopChimney>();
            foreach (Merchant m in market.Book.Merchants)
            {
                if (!(m.Shop is Shop shop) || shop.Building is null || !(HearthPrefix(shop.Kind) is string prefix)) continue;
                SpaceObstacle? house = Map.Obstacles.FirstOrDefault(o => o.Id == shop.Building);
                if (house is null) continue;
                double hx = (house.Points.Min(p => p.X.Raw) + house.Points.Max(p => p.X.Raw)) / 20000.0, hz = (house.Points.Min(p => p.Z.Raw) + house.Points.Max(p => p.Z.Raw)) / 20000.0;
                list.Add(new ShopChimney(prefix + m.Id, hx, hz, World.ShopStateOf(m.Id) == ShopState.Open));
            }
            return list;
        }

        /// <summary>V2.21 : préfixe des feux tenus pendant l'ouverture (cuisine d'auberge, âtre d'épicerie) ; null pour les autres genres.</summary>
        private static string? HearthPrefix(string kind) => kind == "inn" ? "cuisine/" : kind == "grocer" ? "atre/" : null;

        /// <summary>
        /// Sous-lot V2.14 : ligne de l'affichage pour le marché (installation, marché et criée du moment si le joueur est sur la place du
        /// bourg, démontage) ; null hors des jours de marché.
        /// </summary>
        /// <remarks>Relecture indépendante de la PR #135 : lue à chaque image par l'affichage ; refaite seulement quand la minute du monde, la
        /// zone ou la présence sur la place changent (aucune allocation par image sinon).</remarks>
        public string? MarketLine
        {
            get
            {
                bool square = OnMarketSquare;
                long day = World.Zone.Time.Day;
                int minute = World.Zone.Time.Minute;
                if (_marketLineCached && _marketLineDay == day && _marketLineMinute == minute && _marketLineSquare == square && _marketLineZone == _zone)
                    return _marketLine;
                string? line = null;
                if (MarketPhaseNow != MarketPhase.Calm)
                {
                    line = MarketNotice;
                    if (MarketPhaseNow == MarketPhase.Open && square && MarketCry is string cry) line += " " + cry;
                }
                (_marketLine, _marketLineDay, _marketLineMinute, _marketLineSquare, _marketLineZone, _marketLineCached) = (line, day, minute, square, _zone, true);
                return line;
            }
        }

        private string? _marketLine;
        private long _marketLineDay;
        private int _marketLineMinute;
        private bool _marketLineSquare, _marketLineCached;
        private ZoneDefinition? _marketLineZone;

        /// <summary>Sous-lot V2.14 : le joueur est sur la place du bourg (le lieu « bourg » de la carte), dehors.</summary>
        public bool OnMarketSquare
        {
            get
            {
                if (!InTown || !(Me is GroundVector p)) return false;
                AdventureMap map = Map;
                if (!ReferenceEquals(map, _squareMap))
                {
                    _squareMap = map;
                    _square = map.Places.FirstOrDefault(pl => pl.Id == "bourg");
                }
                if (!(_square is MapPlace town)) return false;
                double x = p.X.Raw / 10000.0, z = p.Z.Raw / 10000.0;
                return x >= town.MinX && x <= town.MaxX && z >= town.MinZ && z <= town.MaxZ;
            }
        }

        private AdventureMap? _squareMap;
        private MapPlace? _square;
    }
}
