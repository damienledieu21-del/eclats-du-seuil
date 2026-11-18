using EdS.Data.Characters;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.30 : le Bourg d'Aube fortifié (surface), d'après son plan illustré (validé par Damien le 11 novembre 2026) : palissade de
/// 600 × 500 m autour de (−1 010, −70), tours d'angle et de portes, portes est et ouest sur la route d'Ambre, poterne sud sur le chemin
/// des vignes, grande maison, église et clocher, halle, puits et étals de la place du marché, écurie, maisons serrées ; un point sûr sur la
/// place. Le déménagement des boutiques reste au V2.30.1.
/// </summary>
[Trait("Category", "World")]
public class V230BourgFortifieTests
{
    private static readonly Lazy<ZoneDefinition> Vallons = new(() => ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"),
        Path.Combine(AppContext.BaseDirectory, "scenarios"), ShippedData, ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!).Zone!);

    private const double W = -1310, E = -710, S = -320, N = 180;

    private static (double X, double Z) P(GroundVector v) => (v.X.Raw / 1e4, v.Z.Raw / 1e4);

    /// <summary>Boîtes des obstacles de la ville (palissade, tours, bâtiments), en mètres.</summary>
    private static List<(string Id, double X0, double Z0, double X1, double Z1)> Town() => Vallons.Value.Setup.Space!.Obstacles
        .Where(o => o.Id.Contains("_ville", StringComparison.Ordinal))
        .Select(o => (o.Id, o.Points.Min(p => P(p).X), o.Points.Min(p => P(p).Z), o.Points.Max(p => P(p).X), o.Points.Max(p => P(p).Z))).ToList();

    private static bool Blocked(List<(string Id, double X0, double Z0, double X1, double Z1)> town, double x, double z, double margin) =>
        town.Any(b => x > b.X0 - margin && x < b.X1 + margin && z > b.Z0 - margin && z < b.Z1 + margin);

    [Fact]
    public void Palissade_fermee_sauf_les_trois_portes()
    {
        var town = Town();
        Assert.Equal(8, town.Count(b => b.Id.StartsWith("palissade_ville", StringComparison.Ordinal)));
        Assert.Equal(12, town.Count(b => b.Id.StartsWith("tour_ville", StringComparison.Ordinal)));   // 4 d'angle, 4 de portes, poterne, clocher
        Assert.InRange(town.Count(b => b.Id.StartsWith("maison_ville", StringComparison.Ordinal)), 60, 160);
        // Tour du périmètre, tous les 0,5 m : chaque point est dans la palissade ou une tour, sauf dans les trois ouvertures.
        var gaps = new List<(double X, double Z)>();
        void Walk(double x0, double z0, double x1, double z1)
        {
            double len = Math.Max(Math.Abs(x1 - x0), Math.Abs(z1 - z0));
            for (double t = 0; t <= len; t += 0.5)
            {
                double x = x0 + (x1 - x0) * t / len, z = z0 + (z1 - z0) * t / len;
                if (!Blocked(town, x, z, 0.01)) gaps.Add((x, z));
            }
        }
        Walk(W, N, E, N);
        Walk(E, N, E, S);
        Walk(E, S, W, S);
        Walk(W, S, W, N);
        Assert.All(gaps, g => Assert.True(
            (g.X == E && g.Z > -60 && g.Z < -40) || (g.X == W && g.Z > -97 && g.Z < -83) || (g.Z == S && g.X > -1163.5 && g.X < -1156.5) || (g.Z == N && g.X > -950 && g.X < -930),   // V2.40 : porte nord (20 m, comme celle de l'Est)
            $"brèche en ({g.X}, {g.Z})"));
        Assert.Contains(gaps, g => g.X == E);
        Assert.Contains(gaps, g => g.X == W);
        Assert.Contains(gaps, g => g.Z == S);
        Assert.Contains(gaps, g => g.Z == N);
    }

    [Fact]
    public void Routes_libres_a_travers_la_ville_et_points_surs_degages()
    {
        var town = Town();
        ZoneDefinition zone = Vallons.Value;
        foreach (string id in new[] { "route_d_ambre_ouest", "chemin_des_vignes" })
        {
            var pts = zone.Roads.Single(r => r.Id == id).Points.Select(P).ToList();
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                double len = Math.Sqrt(Math.Pow(pts[i + 1].X - pts[i].X, 2) + Math.Pow(pts[i + 1].Z - pts[i].Z, 2));
                for (double t = 0; t <= len; t += 1)
                {
                    double x = pts[i].X + (pts[i + 1].X - pts[i].X) * t / len, z = pts[i].Z + (pts[i + 1].Z - pts[i].Z) * t / len;
                    Assert.False(Blocked(town, x, z, 1.5), $"{id} bouchée en ({x:0.0}, {z:0.0})");
                }
            }
        }
        SafePoint market = zone.SafePoints.Single(s => s.Id == "place_du_marche_de_la_ville");
        Assert.Equal((-1010.0, -77.0), P(market.Position));
        Assert.False(Blocked(town, -1010, -77, 3));
        Assert.Contains(zone.Places, p => p.Id == "place_du_marche_de_la_ville");
        Assert.Contains(zone.Places, p => p.Id == "eglise_de_la_ville");
        // Rien d'autre de la région dans l'enceinte, sauf les bâtiments du bourg déménagés au V2.30.1 et le panneau de la place du marché.
        var moved = new[] { "maison1", "maison2", "maison4", "maison5", "maison6", "maison7", "auberge", "chapelle", "puits", "etal1", "etal2",
            "panneau_place_du_marche" };   // et le panneau de la place du marché
        Assert.Equal(moved.OrderBy(i => i, StringComparer.Ordinal), zone.Setup.Space!.Obstacles.Where(o => !o.Id.Contains("_ville", StringComparison.Ordinal)
            && o.Points.All(p => P(p).X > W && P(p).X < E && P(p).Z > S && P(p).Z < N)).Select(o => o.Id).OrderBy(i => i, StringComparer.Ordinal));
    }

    [Fact]
    public void De_la_porte_de_l_est_a_la_place_du_marche_a_pied()
    {
        CharacterProgression progression = ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;
        ItemCatalog items = ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;
        SavedCharacter hero = Inventory.Outfit(PlayerCharacters.Create("heros", AdventureSession.PlayerClass, progression), items);
        WorldSession w = V228b2PassageTests.Open(Vallons.Value, new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, hero),
            safePoint: "porte_est_du_bourg");
        V228b2PassageTests.WalkTo(w, -860, -60);
        V228b2PassageTests.WalkTo(w, -1008, -79);
        Assert.True(w.Registered("zone.vallons/safe/place_du_marche_de_la_ville"));
    }

    // --- Relecture indépendante de la PR #188 ---------------------------------------------------------------------------

    private static WorldSession OpenAt(string safePoint, SavedCharacter? hero = null)
    {
        CharacterProgression progression = ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;
        ItemCatalog items = ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;
        hero ??= Inventory.Outfit(PlayerCharacters.Create("heros", AdventureSession.PlayerClass, progression), items);
        return V228b2PassageTests.Open(Vallons.Value, new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, hero), safePoint: safePoint);
    }

    [Fact]
    public void Par_la_porte_de_l_Ouest_et_par_la_poterne_a_pied()
    {
        WorldSession w = OpenAt("place_du_marche_de_la_ville");
        V228b2PassageTests.WalkTo(w, -1160, -82);   // rue vers l'ouest, au bord de la route d'Ambre
        V228b2PassageTests.WalkTo(w, -1290, -89);   // avant la porte
        V228b2PassageTests.WalkTo(w, -1330, -91);   // dehors, par la porte de l'Ouest
        V228b2PassageTests.WalkTo(w, -1290, -89);
        V228b2PassageTests.WalkTo(w, -1160, -82);
        V228b2PassageTests.WalkTo(w, -1010, -77);
        // Poterne sud : le long du chemin des vignes.
        V228b2PassageTests.WalkTo(w, -1080, -185);
        V228b2PassageTests.WalkTo(w, -1150, -300);
        V228b2PassageTests.WalkTo(w, -1170, -340);   // dehors, par la poterne
    }

    [Fact]
    public void A_cheval_par_la_porte_de_l_Ouest()
    {
        CharacterProgression progression = ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;
        ItemCatalog items = ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;
        // Fin du chapitre 1 : le cheval (V2.28d (2b)).
        SavedCharacter hero = Inventory.Outfit(PlayerCharacters.Create("heros", AdventureSession.PlayerClass, progression), items)
            .WithStory(new SavedStory(quests: new Dictionary<string, QuestProgress> { [PlayerCharacters.HorseQuest] = new QuestProgress(QuestProgress.Done) }));
        WorldSession w = OpenAt("place_du_marche_de_la_ville", hero);
        Assert.True(w.CanCallMount);
        CombatEngine e = w.Zone.Engine;
        e.Submit(new CombatCommand(e.CurrentTick * e.TickMilliseconds, w.CombatantId, CombatAction.Mount));
        w.Step();
        w.Step();
        Assert.True(w.Zone.Engine.View(w.CombatantId).Mounted);
        V228b2PassageTests.WalkTo(w, -1160, -82);
        V228b2PassageTests.WalkTo(w, -1330, -91);   // dehors, en selle
        Assert.True(w.Zone.Engine.View(w.CombatantId).Mounted);
    }

    [Fact]
    public void Le_relief_du_coeur_ne_change_pas_avec_la_ville()
    {
        string folder = Path.Combine(Path.GetTempPath(), "eds_v230_relief_" + Guid.NewGuid().ToString("N"));
        try
        {
            AdventureSession a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), Path.Combine(folder, "partie.json"));
            a.Advance(0.05);
            AdventureMap map = a.RegionMap;
            var without = new AdventureMap(new CombatSpace(map.Space.Min, map.Space.Max, map.Space.Obstacles.Where(o => !o.Id.Contains("_ville", StringComparison.Ordinal))),
                map.Places, map.Roads, map.Clearings, map.Doors, map.Indoor, map.Speakers, map.Gates, map.Region);
            GroundRelief with = GroundRelief.For(map), plain = GroundRelief.For(without);
            for (double x = -100; x <= 140; x += 4)
                for (double z = -70; z <= 100; z += 4)
                    Assert.True(Math.Abs(with.Height(x, z) - plain.Height(x, z)) < 1e-9, $"cœur changé en ({x}, {z}) : {with.Height(x, z) - plain.Height(x, z):0.000} m");
            // La palissade suit le sol (pas de replat), et ce sol varie le long d'un pan (d'où des rondins posés un à un dans Unity).
            Assert.True(GroundRelief.FollowsGround("palissade_ville_nord"));
            Assert.False(GroundRelief.FollowsGround("tour_ville_nord_est"));
            Assert.False(GroundRelief.Builds(map.Space.Obstacles.Single(o => o.Id == "palissade_ville_nord")));
            double lo = double.MaxValue, hi = double.MinValue;
            for (double x = W; x <= E; x += 10) { double h = with.Height(x, N); lo = Math.Min(lo, h); hi = Math.Max(hi, h); }
            Assert.True(hi - lo > 1, $"sol de {lo:0.0} à {hi:0.0} m le long du pan nord");
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }
}
