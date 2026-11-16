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
        // Lieux de la région : hors du cœur (le niveau de chaque lieu du cœur ne change pas), sans créature sauf celles des lieux de la grande
        // carte (décision de Damien du 16 novembre 2026, vers 23 h : peupler les lieux avec le bestiaire existant).
        var core = new[] { "village", "moulin", "riviere", "pre_nord", "ruines", "carriere", "seuil", "coeur_du_seuil", "forge", "bourg", "champs", "lac",
            "vergers", "hameau", "plaine_est", "hautes_pierres", "tour_de_guet", "camp_des_rodeurs", "foret_des_loups", "four_de_firmin", "feu_de_camp" };   // cuisine : deux postes
        var region = zone.Places.Where(p => !core.Contains(p.Id)).ToList();
        Assert.Equal(31, region.Count);   // V2.31 et V2.32 : cascade, moulin à foulon, cabane du pêcheur, pont suspendu, arbre millénaire, tanière, cabane du bûcheron, lisière ; V2.30 : place du marché et église de la ville ; relecture de la PR #170 : six lieux de transition
        foreach (Place p in region)
        {
            Assert.False(p.Min.X.Raw < Fixed.FromInt(140).Raw && p.Max.X.Raw > Fixed.FromInt(-100).Raw && p.Min.Z.Raw < Fixed.FromInt(100).Raw
                && p.Max.Z.Raw > Fixed.FromInt(-70).Raw, $"{p.Id} chevauche le cœur");
            Assert.DoesNotContain(zone.Populations, q => !ScenarioTestData.IsLieu(q.Creature) && zone.Setup.Combatants.Any(c => c.Id == q.Creature && c.Placement is { } at && p.Contains(at.Position)));
        }
        Assert.Equal(1, zone.MinLevelAt(G(-18, -12)));
        Assert.Equal(15, zone.MinLevelAt(G(-850, 780)));   // forêt des Loups profonde
        Assert.Equal(21, zone.MinLevelAt(G(1170, 990)));   // fort des bandits
        // Décision de Damien du 15 novembre 2026 : les lieux de la région restent sous le Gardien du Seuil (niveau 23, fin du chapitre 5),
        // entre 1 et 22.
        Assert.All(region, p => Assert.InRange(p.MinLevel, 1, 22));
        // Relecture de la PR #170 : aucun trou de niveau sur les routes de la région (chaque mètre hors du cœur est dans un lieu), ni aux
        // points sûrs de la région.
        foreach (Road road in zone.Roads)
            for (int i = 0; i + 1 < road.Points.Count; i++)
            {
                (double ax, double az) = P(road.Points[i]);
                (double bx, double bz) = P(road.Points[i + 1]);
                int n = (int)Math.Ceiling(Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az)));
                for (int k = 0; k <= n; k++)
                {
                    double x = ax + (bx - ax) * k / n, z = az + (bz - az) * k / n;
                    if (x >= -100 && x <= 140 && z >= -70 && z <= 100) continue;
                    Assert.True(zone.PlacesAt(G(x, z)).Any(), $"{road.Id} hors de tout lieu en ({x:0.0}, {z:0.0})");
                }
            }
        foreach (string id in new[] { "porte_est_du_bourg", "pontons_du_lac", "hameau_des_mineurs", "pied_du_fort", "gue_de_la_vallee", "bergerie",
            "lisiere_des_terres_grises", "chateau_viticole" })
            Assert.NotEmpty(zone.PlacesAt(zone.SafePoints.Single(p => p.Id == id).Position));
        Assert.Equal(20, zone.MinLevelAt(zone.SafePoints.Single(p => p.Id == "pied_du_fort").Position));
    }

    [Fact]
    public void Aucune_route_ne_franchit_le_verrou_du_chapitre_1_hors_d_un_passage()
    {
        // Relecture de la PR #170 : une route passe de la rive ouest fermée (vallée, forêt profonde, bergerie) au reste de la région seulement
        // par un passage fermé (le chemin de la bergerie par le gué sud), jamais à travers un obstacle ni par un trou.
        ZoneDefinition zone = Zone();
        var obstacles = zone.Setup.Space!.Obstacles;
        (double X, double Z) min = P(zone.Region!.Min), max = P(zone.Region.Max);
        const double step = 2;
        var locked = V228cSurfaceTests.Flood(obstacles, min, max, (-20, 300), step);
        bool In(double x, double z) => V228cSurfaceTests.Reaches(locked.Reached, min, (x, z), step);
        var gates = zone.Passages.Select(p => obstacles.Single(o => o.Id == p.Obstacle).Points.Select(P).ToList()).ToList();
        var shapes = obstacles.Select(o => o.Points.Select(P).ToList()).Select(p => (Points: p, MinX: p.Min(q => q.X), MinZ: p.Min(q => q.Z), MaxX: p.Max(q => q.X), MaxZ: p.Max(q => q.Z))).ToList();
        int crossings = 0;
        foreach (Road road in zone.Roads)
        {
            bool? side = null;
            for (int i = 0; i + 1 < road.Points.Count; i++)
            {
                (double ax, double az) = P(road.Points[i]);
                (double bx, double bz) = P(road.Points[i + 1]);
                int n = (int)Math.Ceiling(Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az)) / step);
                for (int k = 0; k <= n; k++)
                {
                    double x = ax + (bx - ax) * k / n, z = az + (bz - az) * k / n;
                    // Une case occupée (obstacle ou bord) ne dit rien : seules les cases libres comptent.
                    if (shapes.Any(o => x >= o.MinX - 1 && x <= o.MaxX + 1 && z >= o.MinZ - 1 && z <= o.MaxZ + 1 && GroundRelief.PolygonDistance(o.Points, x, z) < 1))
                        continue;
                    bool here = In(x, z);
                    if (side is bool before && before != here)
                    {
                        Assert.True(gates.Any(g => GroundRelief.PolygonDistance(g, x, z) < 6), $"{road.Id} franchit le verrou en ({x:0.0}, {z:0.0}) hors d'un passage");
                        crossings++;
                    }
                    side = here;
                }
            }
        }
        Assert.Equal(2, crossings);   // le chemin de la bergerie, au gué sud ; V2.31 : le sentier du pont suspendu, à son passage
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
        // Sous la hauteur du terrain de Unity (StyleWorld.TerrainHeight, 420 m, base de 3 m), sur toute la région et sa bordure de collines.
        double top = 0;
        for (double x = -1990; x <= 2270; x += 40)
            for (double z = -1690; z <= 1540; z += 40) top = Math.Max(top, relief.Height(x, z));
        Assert.InRange(top, 250, 410);
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
        // Relecture de la PR #170 : pente des routes de la région, le long de la route, au plus 25° (tan 25° ≈ 0,47) ; celles du cœur ont la leur (V224ReliefTests).
        foreach (StyleRoad r in whole.Roads.Where(r => r.Points.Any(p => p.X < -100 || p.X > 140 || p.Z < -70 || p.Z > 100)))
        {
            double steep = 0, sx = 0, sz = 0;
            for (int i = 0; i + 1 < r.Points.Count; i++)
            {
                var (a, b) = (r.Points[i], r.Points[i + 1]);
                double len = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
                for (double t = 0; t + 2 <= len; t += 1)
                {
                    // Pente le long de la route (celle de la marche), sur 2 m.
                    double x = a.X + (b.X - a.X) * t / len, z = a.Z + (b.Z - a.Z) * t / len;
                    double x2 = a.X + (b.X - a.X) * (t + 2) / len, z2 = a.Z + (b.Z - a.Z) * (t + 2) / len;
                    double slope = Math.Abs(relief.Height(x2, z2) - relief.Height(x, z)) / 2;
                    if (slope > steep) (steep, sx, sz) = (slope, x, z);
                }
            }
            // Les sentiers de montagne (bergerie, cairns) montent plus raide, jusqu'à 35° (limite : le relief ne suit pas encore leurs lacets ;
            // le moteur reste plat, la marche n'en dépend pas).
            double limit = r.Id is "chemin_des_cairns" or "chemin_de_la_bergerie" ? 35 : 25;
            Assert.True(steep <= Math.Tan(limit * Math.PI / 180), $"{r.Id} : pente {steep:0.00} en ({sx:0}, {sz:0})");
        }
        // Relecture de la PR #170 : ni tranchée ni marche en travers de l'éboulement et des falaises (aucun replat sous elles) : profils de
        // 40 m, au mètre, sans saut de plus de 1 m, et le sol y suit le relief (aucun replat ne l'abaisse).
        foreach ((double cx, double cz, double nx, double nz) in new[] { (190.0, 1180.0, 0.25, 0.97), (-800.0, 1280.0, 0.0, 1.0), (-20.0, 280.0, 0.0, 1.0) })
        {
            double previous = relief.Height(cx - 20 * nx, cz - 20 * nz);
            for (int k = -19; k <= 20; k++)
            {
                double x = cx + k * nx, z = cz + k * nz, h = relief.Height(x, z);
                Assert.True(Math.Abs(h - previous) < 1, $"saut de {Math.Abs(h - previous):0.00} m en ({x:0}, {z:0})");
                Assert.True(Math.Abs(h - relief.Base(x, z)) < 0.5, $"tranchée en ({x:0}, {z:0}) : {h:0.0} m pour un sol de {relief.Base(x, z):0.0} m");
                previous = h;
            }
        }
        // Le relief de la région est propre au repère des Vallons : une autre grande aire n'en a pas.
        var other = new ReliefSettings { RegionGrid = (0, 0, 32, 24) };
        Assert.Equal(0, GroundRelief.For(whole, other).Region(180, 1360));
    }
}
