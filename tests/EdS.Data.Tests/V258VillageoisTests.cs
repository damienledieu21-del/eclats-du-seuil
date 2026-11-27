using EdS.Runtime;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>V2.58 : villageois vivants dans le Bourg et les hameaux des Vallons (routines pures, routes, autour de la caméra, petites vies).</summary>
[Trait("Category", "World")]
public class V258VillageoisTests
{
    private static readonly Lazy<(AdventureMap Map, VillageWorld World)> Region = new(() =>
    {
        string folder = Path.Combine(Path.GetTempPath(), "eds_v258_" + Guid.NewGuid().ToString("N"));
        try
        {
            AdventureSession a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), Path.Combine(folder, "partie.json"));
            a.Advance(0.05);
            AdventureMap map = a.RegionMap;
            return (map, VillageLife.World(map, map.AreaFields(map.Roads)));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    });

    private static VillageWorld World => Region.Value.World;
    private static AdventureMap Map => Region.Value.Map;

    private static VillageContext Plain(WardrobeSeason season = WardrobeSeason.Spring, bool rain = false, bool feast = false, bool market = false) =>
        new(season, rain, feast, market);

    private static bool InBuilding(double x, double z) => Map.Obstacles.Any(o =>
        o.Id.StartsWith("maison", StringComparison.Ordinal) && x > o.Points.Min(p => p.X.Raw / 1e4) + 0.05 && x < o.Points.Max(p => p.X.Raw / 1e4) - 0.05
        && z > o.Points.Min(p => p.Z.Raw / 1e4) + 0.05 && z < o.Points.Max(p => p.Z.Raw / 1e4) - 0.05);

    [Fact]
    public void Every_dwelling_has_residents_of_every_kind_and_the_draw_is_stable()
    {
        var residents = World.Residents;
        Assert.InRange(residents.Count, 150, VillageLife.MaxResidents);
        foreach (VillagerTrade t in Enum.GetValues(typeof(VillagerTrade))) Assert.Contains(residents, r => r.Trade == t);
        Assert.Equal(residents.Count, residents.Select(r => r.Id).Distinct().Count());
        Assert.Contains(residents, r => r.House == "maison_ville_forgeron" && r.Trade == VillagerTrade.Smith);
        Assert.Contains(residents, r => r.Hamlet);
        Assert.Contains(residents, r => !r.Hamlet);
        Assert.DoesNotContain(residents, r => r.House.Contains("peage"));
        // Les places du dehors ne sont jamais dans une maison.
        foreach (Resident r in residents)
            foreach (LifeSpot s in new[] { r.Home, r.Work, r.Market, r.Feast, r.Play, r.Porch })
                Assert.False(InBuilding(s.X, s.Z), r.Id + " " + s.Place);
        // Même carte, mêmes habitants (métier, places, modèle).
        var again = VillageLife.World(Map, Map.AreaFields(Map.Roads));
        Assert.Equal(residents.Select(r => (r.Id, r.Trade, r.Home.X, r.Work.Z, r.Model, r.Character)),
            again.Residents.Select(r => (r.Id, r.Trade, r.Home.X, r.Work.Z, r.Model, r.Character)));
        // Repli : le modèle du kit (pas encore livré) a toujours un personnage existant derrière lui.
        Assert.All(residents, r => Assert.StartsWith("villageois/V", r.Model));
        Assert.All(residents, r => Assert.Contains(r.Character, new[] { "paysan", "boulanger", "forgeron", "marchand", "fidele" }));
    }

    [Fact]
    public void Trades_follow_the_house_name_then_the_draw()
    {
        Assert.Equal(VillagerTrade.Smith, VillageLife.TradeOf("maison_ville_armurier", 0, true, true));
        Assert.Equal(VillagerTrade.Merchant, VillageLife.TradeOf("maison_ville_apothicaire", 0, true, true));
        Assert.Equal(VillagerTrade.Farmer, VillageLife.TradeOf("maison_du_berger_bergers", 99, false, false));
        Assert.Equal(VillagerTrade.Laborer, VillageLife.TradeOf("maison_mineur_mine_1", 10, true, false));
        Assert.Equal(VillagerTrade.Farmer, VillageLife.TradeOf("maison1", 10, true, false));
        Assert.Equal(VillagerTrade.Laborer, VillageLife.TradeOf("maison1", 10, false, false));
        Assert.Equal(VillagerTrade.Merchant, VillageLife.TradeOf("maison1", 90, false, true));
        Assert.False(VillageLife.Dwelling("maison_poste_de_peage_passage"));
        Assert.True(VillageLife.Dwelling("maison_ville_12"));
    }

    [Fact]
    public void A_day_is_sorted_and_everyone_sleeps_at_home()
    {
        foreach (WardrobeSeason season in Enum.GetValues(typeof(WardrobeSeason)))
            foreach (Resident r in World.Residents)
            {
                var day = VillageLife.Day(r, Plain(season));
                Assert.True(day.Count >= 3);
                for (int i = 0; i < day.Count; i++)
                {
                    Assert.InRange(day[i].Start, 0, 1439);
                    if (i > 0) Assert.True(day[i].Start > day[i - 1].Start);
                }
                // De 1 h à 3 h, tout le monde dort chez soi (caché).
                for (int m = 60; m <= 180; m += 30) Assert.True(VillageLife.At(World, r, m, Plain(season)).Hidden, r.Id + " " + m);
            }
    }

    [Fact]
    public void Work_meal_market_and_tavern_happen_at_their_hours()
    {
        var c = Plain();
        Resident farmer = World.Residents.First(r => r.Trade == VillagerTrade.Farmer && r.Work.Place == LifePlace.Work);
        Assert.Equal(LifePlace.Work, VillageLife.Target(farmer, 10 * 60, c).Place);
        Assert.Equal(LifePlace.Meal, VillageLife.Target(farmer, 12 * 60 + 25, c).Place);
        Assert.True(VillageLife.Target(farmer, 12 * 60 + 25, c).Indoors);
        Resident baker = World.Residents.First(r => r.Trade == VillagerTrade.Baker);
        Assert.Equal(LifePlace.Work, VillageLife.Target(baker, 4 * 60 + 30, c).Place);   // le fournil, la nuit
        Assert.Equal(LifePlace.Market, VillageLife.Target(baker, 12 * 60, c).Place);
        Resident smith = World.Residents.First(r => r.Trade == VillagerTrade.Smith && r.Tavern != null);
        Assert.Equal(LifePlace.Tavern, VillageLife.Target(smith, 20 * 60, c).Place);
        Resident child = World.Residents.First(r => r.Child);
        Assert.Equal(LifePlace.Play, VillageLife.Target(child, 14 * 60, c).Place);
        Resident elder = World.Residents.First(r => r.Elder);
        Assert.Equal(LifePlace.Porch, VillageLife.Target(elder, 9 * 60, c).Place);
        // Un hameau sans auberge : la veillée au milieu des maisons, pas la taverne du Bourg.
        Resident hamlet = World.Residents.First(r => r.Hamlet && r.Trade == VillagerTrade.Smith || r.Hamlet && r.Trade == VillagerTrade.Laborer);
        Assert.Null(hamlet.Tavern);
    }

    [Fact]
    public void Seasons_move_the_hours_and_the_farmer_chops_wood_in_winter()
    {
        Resident farmer = World.Residents.First(r => r.Trade == VillagerTrade.Farmer && r.Work.Place == LifePlace.Work
            && (r.Work.X - r.Home.X) * (r.Work.X - r.Home.X) + (r.Work.Z - r.Home.Z) * (r.Work.Z - r.Home.Z) > 100);
        LifeSpot winter = VillageLife.Target(farmer, 10 * 60, Plain(WardrobeSeason.Winter));
        Assert.Equal(LifePlace.Work, winter.Place);
        Assert.InRange(Math.Sqrt((winter.X - farmer.Home.X) * (winter.X - farmer.Home.X) + (winter.Z - farmer.Home.Z) * (winter.Z - farmer.Home.Z)), 0, 3);
        // Hiver : pas de taverne pour le paysan ; été : il veille plus tard.
        Assert.DoesNotContain(VillageLife.Day(farmer, Plain(WardrobeSeason.Winter)), s => s.Spot.Place == LifePlace.Tavern);
        int Last(WardrobeSeason s) => VillageLife.Day(farmer, Plain(s)).Where(x => x.Start > 12 * 60).Max(x => x.Start);
        Assert.True(Last(WardrobeSeason.Summer) > Last(WardrobeSeason.Spring));
        Assert.True(Last(WardrobeSeason.Winter) < Last(WardrobeSeason.Spring));
    }

    [Fact]
    public void Under_a_downpour_nobody_stays_out_in_the_open()
    {
        var rain = Plain(rain: true);
        foreach (Resident r in World.Residents)
            foreach (LifeSlot s in VillageLife.Day(r, rain))
            {
                Assert.True(s.Spot.Indoors || s.Spot.Place == LifePlace.Shelter, r.Id + " " + s.Spot.Place);
                if (r.Child) Assert.True(s.Spot.Indoors);
            }
    }

    [Fact]
    public void A_feast_day_gathers_the_village_on_the_square()
    {
        var feast = Plain(feast: true);
        int onSquare = 0, outside = 0;
        foreach (Resident r in World.Residents)
        {
            LifePose p = VillageLife.At(World, r, 20 * 60 + 30, feast);
            if (p.Hidden) continue;
            outside++;
            if (p.Place == LifePlace.Feast) onSquare++;
        }
        Assert.True(outside > 50);
        Assert.Equal(outside, onSquare);
        Resident merchant = World.Residents.First(r => r.Trade == VillagerTrade.Merchant && r.Work.Place == LifePlace.Work);
        Assert.Equal(LifePlace.Work, VillageLife.Target(merchant, 16 * 60 + 30, feast).Place);   // l'étal reste ouvert l'après-midi
        Resident child = World.Residents.First(r => r.Child);
        Assert.True(VillageLife.At(World, child, 22 * 60 + 30, feast).Hidden);   // les enfants rentrent plus tôt
    }

    [Fact]
    public void Where_is_a_pure_function_with_walks_on_the_roads()
    {
        var c = Plain();
        var fresh = VillageLife.World(Map, Map.AreaFields(Map.Roads));
        int moving = 0;
        foreach (Resident r in World.Residents.Take(120))
        {
            Resident twin = fresh.Residents.Single(x => x.Id == r.Id);
            var day = VillageLife.Day(r, c);
            for (double m = 0; m < 1440; m += 7.5)
            {
                LifePose a = VillageLife.At(World, r, m, c), b = VillageLife.At(fresh, twin, m, c);
                Assert.Equal((a.X, a.Z, a.Hidden, a.Place, a.Moving), (b.X, b.Z, b.Hidden, b.Place, b.Moving));
                if (!a.Moving) continue;
                moving++;
                Assert.Equal(VillagerActivity.Walk, a.Activity);
                // Sans saut dans un créneau : une demi-minute plus tard, il a fait au plus son pas.
                if (day.Any(s => s.Start > m && s.Start <= m + 0.5)) continue;
                LifePose next = VillageLife.At(World, r, m + 0.5, c);
                double step = Math.Sqrt((next.X - a.X) * (next.X - a.X) + (next.Z - a.Z) * (next.Z - a.Z));
                Assert.True(step <= c.MetersPerMinute * r.Pace * 0.5 + 0.01, r.Id + " " + m + " " + step);
            }
        }
        Assert.True(moving > 100);
        // Un long trajet suit les routes : entre l'entrée sur la route et la sortie, chaque pas est sur une route (raccords compris).
        int checkedTrips = 0;
        foreach (Resident r in World.Residents.Where(r => !r.Hamlet).Take(60))
        {
            var path = World.Roads.Route(r.Home.X, r.Home.Z, r.Market.X, r.Market.Z);
            if (path.Count < 4) continue;
            checkedTrips++;
            double from = Math.Sqrt((path[1].X - path[0].X) * (path[1].X - path[0].X) + (path[1].Z - path[0].Z) * (path[1].Z - path[0].Z));
            double to = VillageRoads.Length(path) - Math.Sqrt((path[^1].X - path[^2].X) * (path[^1].X - path[^2].X) + (path[^1].Z - path[^2].Z) * (path[^1].Z - path[^2].Z));
            for (double d = from; d <= to; d += 2)
            {
                var at = VillageRoads.Along(path, d);
                Assert.True(TownRules.RoadAt(at.X, at.Z, Map.Roads).Edge < VillageRoads.JoinDistance, r.Id + " à " + d + " m");
            }
        }
        Assert.True(checkedTrips > 10);
    }

    [Fact]
    public void Routes_start_and_end_where_asked_and_short_trips_go_straight()
    {
        VillageRoads roads = World.Roads;
        Assert.True(roads.NodeCount > 50);
        Assert.Equal(2, roads.Route(0, 0, 5, 5).Count);
        Resident a = World.Residents.First(), b = World.Residents.Last();
        var path = roads.Route(a.Home.X, a.Home.Z, b.Home.X, b.Home.Z);
        Assert.Equal((a.Home.X, a.Home.Z), path[0]);
        Assert.Equal((b.Home.X, b.Home.Z), path[path.Count - 1]);
        Assert.Same(path, roads.Route(a.Home.X, a.Home.Z, b.Home.X, b.Home.Z));
        Assert.Equal(path, new VillageRoads(Map.Roads).Route(a.Home.X, a.Home.Z, b.Home.X, b.Home.Z));
        double length = VillageRoads.Length(path);
        Assert.True(length >= Math.Sqrt((a.Home.X - b.Home.X) * (a.Home.X - b.Home.X) + (a.Home.Z - b.Home.Z) * (a.Home.Z - b.Home.Z)) - 1e-6);
        var end = VillageRoads.Along(path, length + 50);
        Assert.Equal(b.Home.X, end.X, 6);
        Assert.Equal(2, new VillageRoads(null).Route(0, 0, 500, 500).Count);
    }

    [Fact]
    public void Only_the_nearest_outside_residents_are_built_and_the_count_follows_the_quality()
    {
        Assert.Equal(6, VillageLife.Cap(DeviceTier.Phone, true));
        Assert.Equal(10, VillageLife.Cap(DeviceTier.Modest, true));
        Assert.Equal(18, VillageLife.Cap(DeviceTier.Full, true));
        Assert.Equal(28, VillageLife.Cap(DeviceTier.Full, false));
        Assert.True(VillageLife.Radius(DeviceTier.Phone) < VillageLife.Radius(DeviceTier.Modest) && VillageLife.Radius(DeviceTier.Modest) < VillageLife.Radius(DeviceTier.Full));
        var c = Plain();
        Resident anchor = World.Residents.First(r => !r.Hamlet);
        var poses = new List<LifePose>();
        var near = VillageLife.Near(World, 11 * 60, c, anchor.Market.X, anchor.Market.Z, 90, 10, poses);
        Assert.InRange(near.Count, 1, 10);
        Assert.Equal(near.Count, poses.Count);
        double last = -1;
        foreach (LifePose p in poses)
        {
            Assert.False(p.Hidden);
            double d = Math.Sqrt((p.X - anchor.Market.X) * (p.X - anchor.Market.X) + (p.Z - anchor.Market.Z) * (p.Z - anchor.Market.Z));
            Assert.True(d <= 90 && d >= last);
            last = d;
        }
        Assert.Empty(VillageLife.Near(World, 2 * 60, c, anchor.Market.X, anchor.Market.Z, 90, 10));   // la nuit, tous rentrés
        Assert.Empty(VillageLife.Near(World, 11 * 60, c, anchor.Market.X, anchor.Market.Z, 90, 0));
    }

    [Fact]
    public void Small_lives_greetings_looks_and_chats()
    {
        var c = Plain();
        Resident child = World.Residents.First(r => r.Child), adult = World.Residents.First(r => r.Trade == VillagerTrade.Laborer);
        var standing = new LifePose(0, 0, 0, VillagerActivity.Talk, LifePlace.Market, false, false);
        var walking = new LifePose(0, 0, 0, VillagerActivity.Walk, LifePlace.Market, true, false);
        Assert.True(VillageLife.LooksAtPlayer(standing, 5));
        Assert.False(VillageLife.LooksAtPlayer(standing, 9));
        Assert.False(VillageLife.LooksAtPlayer(walking, 2));
        Assert.True(VillageLife.Greets(standing, 3, 10 * 60));
        Assert.False(VillageLife.Greets(standing, 3, 23 * 60));
        Assert.Equal(VillageLife.Greeting(adult, 9 * 60, c, 0), VillageLife.Greeting(adult, 9 * 60, c, 0));
        Assert.Contains("?", VillageLife.Greeting(child, 9 * 60, c, 1) + VillageLife.Greeting(child, 9 * 60, c, 2) + VillageLife.Greeting(child, 9 * 60, c, 3));
        Assert.Contains(VillageLife.Greeting(adult, 9 * 60, Plain(feast: true), 0), new[] { "Joyeuse fête !", "Venez danser !", "Belle fête, hein ?" });
        Assert.Equal(VillageLife.ChatLine("a", "b", LifePlace.Market, c, 3), VillageLife.ChatLine("b", "a", LifePlace.Market, c, 3));
        Assert.False(string.IsNullOrEmpty(VillageLife.ChatLine("a", "b", LifePlace.Tavern, c, 0)));
        var near = new LifePose(1.5, 1, 0, VillagerActivity.Talk, LifePlace.Market, false, false);
        Assert.True(VillageLife.Chatting(standing, near));
        Assert.False(VillageLife.Chatting(standing, walking));
        Assert.False(VillageLife.Chatting(standing, new LifePose(9, 0, 0, VillagerActivity.Talk, LifePlace.Market, false, false)));
    }

    [Fact]
    public void New_gestures_take_their_clip_or_the_idle()
    {
        var clips = new[] { "Idle", "Dance", "Wave_Call", "Seated_Chat", "Drink", "Walk" };
        Assert.Equal("Dance", TownModels.VillagerClip(clips, VillagerActivity.Dance, 0));
        Assert.Equal("Wave_Call", TownModels.VillagerClip(clips, VillagerActivity.Wave, 0));
        Assert.Equal("Seated_Chat", TownModels.VillagerClip(clips, VillagerActivity.Sit, 0));
        Assert.Equal("Drink", TownModels.VillagerClip(clips, VillagerActivity.Drink, 0));
        Assert.Equal("Idle", TownModels.VillagerClip(new[] { "Idle", "Walk" }, VillagerActivity.Dance, 0));
    }
}
