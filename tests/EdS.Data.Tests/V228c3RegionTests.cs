using EdS.Data.Json;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.28c, partie 3 (PLAN_GRANDE_CARTE.md § 4.2 et 4.3) : routes de la région, points sûrs aux carrefours, lieux de la région
/// (emplacements vides, sans créature), relief d'affichage de la région. Le cœur ne change pas.
/// </summary>
[Trait("Category", "Sector")]
public sealed class V228c3RegionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v228c3_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static ZoneDefinition Zone() => ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"), Path.Combine(AppContext.BaseDirectory, "scenarios"),
        ShippedData, ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!).Zone!;

    private AdventureMap Whole()
    {
        AdventureSession a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), Path.Combine(_folder, "partie.json"));
        a.Advance(0.05);
        return a.Map.Whole;
    }

    private static (double X, double Z) P(GroundVector v) => (v.X.Raw / 1e4, v.Z.Raw / 1e4);

    private static GroundVector G(double x, double z) => new GroundVector(Fixed.FromRaw((long)Math.Round(x * 1e4)), Fixed.FromRaw((long)Math.Round(z * 1e4)));

    [Fact]
    public void Routes_de_la_region_jusqu_aux_bords_et_raccordees_au_coeur()
    {
        ZoneDefinition zone = Zone();
        var roads = zone.Roads.ToDictionary(r => r.Id);
        foreach (string id in new[] { "route_d_ambre_ouest", "route_d_ambre_est", "chemin_de_la_bergerie", "chemin_des_cairns", "chemin_des_pontons",
            "chemin_des_pierres", "chemin_des_vignes", "chemin_de_l_abbaye" })
            Assert.True(roads.ContainsKey(id), id);
        Assert.InRange(zone.Roads.Count, 20, ZoneValidator.MaxRoads);
        (double X, double Z) min = P(zone.Region!.Min), max = P(zone.Region.Max);
        // La route d'Ambre traverse la région d'ouest en est : du bord ouest (Havre d'Ambre) au bord est (col des Crêtes de Fer), raccordée aux
        // routes du cœur (grand-rue de l'ouest, chemin de l'est).
        var west = roads["route_d_ambre_ouest"].Points.Select(P).ToList();
        var east = roads["route_d_ambre_est"].Points.Select(P).ToList();
        Assert.Equal(roads["grand_rue_ouest"].Points.Select(P).Last(), west[0]);
        Assert.Equal(roads["chemin_de_l_est"].Points.Select(P).Last(), east[0]);
        Assert.InRange(west[^1].X - min.X, 0, 1);
        Assert.InRange(max.X - east[^1].X, 0, 1);
        // Le chemin de la bergerie passe le gué sud de la vallée (un passage fermé au chapitre 1, comme la porte du moulin pour le chemin du moulin).
        var gate = zone.Setup.Space!.Obstacles.Single(o => o.Id == "riviere_vallee_gue_sud");
        var gue = gate.Points.Select(P).ToList();
        (double cx, double cz) = (gue.Average(p => p.X), gue.Average(p => p.Z));
        Assert.True(TownRules.RoadAt(cx, cz, AdventureSession.Roads(zone)).Edge < 0, "le chemin de la bergerie ne passe pas le gué");
    }

    [Fact]
    public void Points_surs_aux_carrefours_et_lieux_vides_de_la_region()
    {
        ZoneDefinition zone = Zone();
        Assert.InRange(zone.SafePoints.Count, 14, ZoneValidator.MaxSafePoints);
        IReadOnlyList<StyleRoad> roads = AdventureSession.Roads(zone);
        foreach (string id in new[] { "porte_est_du_bourg", "pontons_du_lac", "hameau_des_mineurs", "pied_du_fort", "gue_de_la_vallee", "bergerie",
            "lisiere_des_terres_grises", "chateau_viticole" })
        {
            SafePoint s = zone.SafePoints.Single(p => p.Id == id);
            (double x, double z) = P(s.Position);
            Assert.True(TownRules.RoadAt(x, z, roads).Edge < 6, $"{id} loin des routes");
        }
        // Lieux de la région : hors du cœur (le niveau de chaque lieu du cœur ne change pas), sans créature.
        var core = new[] { "village", "moulin", "riviere", "pre_nord", "ruines", "carriere", "seuil", "coeur_du_seuil", "forge", "bourg", "champs", "lac",
            "vergers", "hameau", "plaine_est", "hautes_pierres", "tour_de_guet", "camp_des_rodeurs", "foret_des_loups" };
        var region = zone.Places.Where(p => !core.Contains(p.Id)).ToList();
        Assert.Equal(15, region.Count);
        foreach (Place p in region)
        {
            Assert.False(p.Min.X.Raw < Fixed.FromInt(140).Raw && p.Max.X.Raw > Fixed.FromInt(-100).Raw && p.Min.Z.Raw < Fixed.FromInt(100).Raw
                && p.Max.Z.Raw > Fixed.FromInt(-70).Raw, $"{p.Id} chevauche le cœur");
            Assert.DoesNotContain(zone.Populations, q => zone.Setup.Combatants.Any(c => c.Id == q.Creature && c.Placement is { } at && p.Contains(at.Position)));
        }
        Assert.Equal(1, zone.MinLevelAt(G(-18, -12)));
        Assert.Equal(16, zone.MinLevelAt(G(-850, 780)));   // forêt des Loups profonde
        Assert.Equal(24, zone.MinLevelAt(G(1170, 990)));   // fort des bandits
    }

    [Fact]
    public void Relief_de_la_region_le_coeur_inchange_les_collines_hautes_l_eau_a_zero()
    {
        AdventureMap whole = Whole();
        GroundRelief relief = GroundRelief.For(whole), flat = GroundRelief.For(whole, new ReliefSettings { RegionHeights = Array.Empty<(double, double, double)>() });
        // Le cœur ne change pas : même sol, au millimètre près, avec ou sans le relief de la région.
        for (double x = -100; x <= 140; x += 6)
            for (double z = -70; z <= 100; z += 6)
                Assert.Equal(flat.Height(x, z), relief.Height(x, z), 9);
        Assert.Equal(0, relief.Region(-18, -12));
        // Les collines montent jusqu'au sommet (≈ 320 m au-dessus du cœur), le lac reste au plus bas, le Bourg sur le plateau.
        Assert.True(relief.Base(180, 1360) > 250, $"sommet {relief.Base(180, 1360):0}");
        Assert.True(relief.Base(2100, 1220) > 250, $"col {relief.Base(2100, 1220):0}");
        Assert.InRange(relief.Region(-1010, -70), 0, 30);
        Assert.InRange(relief.Height(425, -1105), 0, 0.001);   // dans le grand lac
        // L'eau reste à 0 (le relief de la région s'y raccorde) : milieu de chaque bras de la rivière de la vallée.
        foreach (SpaceObstacle water in whole.Obstacles.Where(o => o.Id.StartsWith("riviere_vallee", StringComparison.Ordinal)))
        {
            var pts = water.Points.Select(P).ToList();
            Assert.Equal(0, relief.Height(pts.Average(p => p.X), pts.Average(p => p.Z)), 6);
        }
        // Le sol de la route d'Ambre reste praticable à pied : pas de marche de plus de 2 m sur 2 m.
        StyleRoad road = whole.Roads.Single(r => r.Id == "route_d_ambre_est");
        double worst = 0;
        for (int i = 0; i + 1 < road.Points.Count; i++)
        {
            var (a, b) = (road.Points[i], road.Points[i + 1]);
            double len = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
            for (double t = 0; t + 2 <= len; t += 2)
            {
                double x0 = a.X + (b.X - a.X) * t / len, z0 = a.Z + (b.Z - a.Z) * t / len, x1 = a.X + (b.X - a.X) * (t + 2) / len, z1 = a.Z + (b.Z - a.Z) * (t + 2) / len;
                worst = Math.Max(worst, Math.Abs(relief.Height(x1, z1) - relief.Height(x0, z0)));
            }
        }
        Assert.True(worst < 2, $"marche de {worst:0.00} m sur la route d'Ambre");
    }
}
