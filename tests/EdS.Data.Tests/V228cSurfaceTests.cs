using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.28c, partie 2b (surface étendue, PLAN_GRANDE_CARTE.md § 4) : l'aire des Vallons couvre toute la région (4 096 × 3 072 m),
/// avec ses grands obstacles (rivière jusqu'à la cascade et jusqu'au grand lac, grand lac, gorge, ravin des Ronces, éboulement, falaises).
/// Le verrou du chapitre 1 tient par le décor : le pré et la forêt ne s'atteignent que par la porte du moulin.
/// </summary>
[Trait("Category", "Sector")]
public class V228cSurfaceTests
{
    /// <summary>
    /// Cases de 0,5 m d'un rectangle, libres pour un personnage (0,5 m d'emprise) : hors de tout obstacle de <paramref name="obstacles"/>
    /// et à plus de 0,5 m de chacun ; rend les cases atteintes depuis <paramref name="from"/> et si le bord du rectangle l'a été.
    /// </summary>
    private static (HashSet<(int, int)> Reached, bool Edge) Flood(IEnumerable<SpaceObstacle> obstacles, (double X, double Z) min, (double X, double Z) max, (double X, double Z) from,
        double step = 0.5)
    {
        const double radius = 0.5;
        var shapes = obstacles.Select(o => o.Points.Select(p => (X: p.X.Raw / 1e4, Z: p.Z.Raw / 1e4)).ToList())
            .Where(p => p.Max(q => q.X) >= min.X - 1 && p.Min(q => q.X) <= max.X + 1 && p.Max(q => q.Z) >= min.Z - 1 && p.Min(q => q.Z) <= max.Z + 1)
            .Select(p => (Points: (IReadOnlyList<(double X, double Z)>)p, MinX: p.Min(q => q.X), MaxX: p.Max(q => q.X), MinZ: p.Min(q => q.Z), MaxZ: p.Max(q => q.Z))).ToList();
        int nx = (int)((max.X - min.X) / step), nz = (int)((max.Z - min.Z) / step);
        bool Free(int i, int k)
        {
            double x = min.X + i * step, z = min.Z + k * step;
            foreach (var s in shapes)
                if (x >= s.MinX - radius && x <= s.MaxX + radius && z >= s.MinZ - radius && z <= s.MaxZ + radius
                    && GroundRelief.PolygonDistance(s.Points, x, z) < radius) return false;
            return true;
        }
        var start = ((int)((from.X - min.X) / step), (int)((from.Z - min.Z) / step));
        var seen = new HashSet<(int, int)> { start };
        var queue = new Queue<(int, int)>();
        queue.Enqueue(start);
        bool edge = false;
        while (queue.Count > 0)
        {
            (int i, int k) = queue.Dequeue();
            if (i == 0 || k == 0 || i == nx || k == nz) { edge = true; continue; }
            foreach ((int di, int dk) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var n = (i + di, k + dk);
                if (seen.Contains(n) || !Free(n.Item1, n.Item2)) continue;
                seen.Add(n);
                queue.Enqueue(n);
            }
        }
        return (seen, edge);
    }

    private static bool Reaches(HashSet<(int, int)> reached, (double X, double Z) min, (double X, double Z) at, double step = 0.5) =>
        reached.Contains(((int)((at.X - min.X) / step), (int)((at.Z - min.Z) / step)));

    [Fact]
    public void Aire_de_toute_la_region()
    {
        ZoneDefinition zone = Zone();
        CombatSpace area = zone.Setup.Space!;
        RegionGrid region = zone.Region!;
        Assert.Equal((region.Min, region.Max), (area.Min, area.Max));
        Assert.False(SectorMap.For(zone).HomeHoldsArea);
        foreach (string id in new[] { "riviere_grand_lac", "gorge_ouest", "ravin_ronces_sud", "ravin_ronces_est", "eboulement_nord", "falaise_collines", "falaise_cascade", "falaise_crete",
            "riviere_vallee_gue_sud", "riviere_vallee_gue_nord" })
            Assert.Contains(area.Obstacles, o => o.Id == id);
        Assert.True(area.Obstacles.Count(o => o.Id.StartsWith("riviere_vallee", StringComparison.Ordinal)) >= 5);
        Assert.True(area.Obstacles.Count(o => o.Id.StartsWith("riviere_champs", StringComparison.Ordinal)) >= 4);
        Assert.True(AdventureMap.Wide(area));   // carte et décor d'une fenêtre (V2.28c (2a))
    }

    [Fact]
    public void Verrou_du_chapitre_1_le_pre_ne_s_atteint_que_par_la_porte_du_moulin()
    {
        // Le pré (au nord de la haie du moulin) : enclos par la haie, la rivière, l'éboulement et le ravin des Ronces. Porte du moulin
        // fermée, ni le village ni le reste de la région ne s'atteignent ; ouverte, le village s'atteint par elle.
        ZoneDefinition zone = Zone();
        var obstacles = zone.Setup.Space!.Obstacles;
        (double X, double Z) min = (-220, -40), max = (180, 340), pre = (-10, 30), village = (-18, -12);
        var closed = Flood(obstacles, min, max, pre);
        Assert.False(closed.Edge, "le pré s'échappe de l'enclos");
        Assert.False(Reaches(closed.Reached, min, village));
        Assert.True(Reaches(closed.Reached, min, (-115, 150)));   // la bande ouest, entre la pointe de la forêt et le ravin, en fait partie
        Assert.True(Reaches(closed.Reached, min, (-20, 250)));    // la vallée jusqu'à l'éboulement aussi
        Assert.False(Reaches(closed.Reached, min, (-20, 300)));   // au-delà de l'éboulement, non
        // Chaque pièce de l'enclos compte : sans l'éboulement, ou sans le ravin de l'est, le pré s'échappe.
        foreach (string piece in new[] { "eboulement_nord", "ravin_ronces_est" })
            Assert.True(Flood(obstacles.Where(o => o.Id != piece), min, max, pre).Edge, piece);
        var open = Flood(obstacles.Where(o => o.Id != "porte_moulin"), min, max, pre);
        Assert.True(Reaches(open.Reached, min, village));
    }

    /// <summary>Gués de la vallée fermés tant que l'éboulement l'est (passages de la zone, comme la porte du moulin).</summary>
    private static readonly string[] ValleyGates = { "eboulement_nord", "riviere_vallee_gue_sud", "riviere_vallee_gue_nord" };

    [Fact]
    public void Verrou_de_la_vallee_depuis_le_village_eboulement_et_gues_fermes()
    {
        // Relecture indépendante de la PR #167 : on contournait l'éboulement par la berge est et le gué de la vallée. La rive ouest au nord de
        // l'éboulement (vallée, forêt profonde, bergerie) est fermée par l'éboulement, la rivière et ses gués fermés, la cascade, la crête, la
        // falaise des collines, la gorge et le ravin des Ronces : depuis elle, ni le village, ni la berge est, ni le reste de la région (remplissage
        // de toute la région, cases de 2 m ; la rivière, 3 m de large et 0,5 m d'emprise de chaque côté, ne laisse passer aucune case).
        ZoneDefinition zone = Zone();
        foreach (string gate in ValleyGates) Assert.Contains(zone.Passages, p => p.Obstacle == gate);
        var obstacles = zone.Setup.Space!.Obstacles;
        RegionGrid region = zone.Region!;
        (double X, double Z) min = (region.Min.X.Raw / 1e4, region.Min.Z.Raw / 1e4), max = (region.Max.X.Raw / 1e4, region.Max.Z.Raw / 1e4);
        const double step = 2;
        var locked = Flood(obstacles, min, max, (-20, 300), step);
        Assert.False(locked.Edge, "la rive ouest touche le bord de la région");
        foreach (var outside in new[] { (-18.0, -12.0), (300.0, 420.0), (1000.0, 145.0), (700.0, 1300.0), (-1010.0, -70.0) })
            Assert.False(Reaches(locked.Reached, min, outside, step), $"({outside.Item1}, {outside.Item2}) atteint depuis la rive ouest");
        foreach (var inside in new[] { (250.0, 600.0), (-830.0, 780.0), (-130.0, 1080.0) })
            Assert.True(Reaches(locked.Reached, min, inside, step), $"({inside.Item1}, {inside.Item2}) hors de la rive ouest");
        // Chaque gué, ouvert, rend la berge est (et par elle le village) accessible.
        foreach (string ford in new[] { "riviere_vallee_gue_sud", "riviere_vallee_gue_nord" })
        {
            (double X, double Z) fmin = (150, 380), fmax = (650, 1060);
            var open = Flood(obstacles.Where(o => o.Id != ford), fmin, fmax, (250, 600));
            Assert.True(Reaches(open.Reached, fmin, (300, 420)), ford);
        }
    }

    [Fact]
    public void Gues_de_la_riviere_on_la_traverse_au_dela_de_l_eboulement_et_vers_le_lac()
    {
        // La rivière (obstacle bas) se traverse à ses gués : au nord de l'éboulement (vallée, une fois ses gués ouverts), et entre l'étang et le
        // grand lac (champs, toujours ouverts).
        ZoneDefinition zone = Zone();
        var obstacles = zone.Setup.Space!.Obstacles;
        (double X, double Z) min = (150, 380), max = (650, 1060);
        Assert.False(Reaches(Flood(obstacles, min, max, (300, 420)).Reached, min, (250, 600)), "gués de la vallée ouverts");
        var north = Flood(obstacles.Where(o => !ValleyGates.Contains(o.Id)), min, max, (300, 420));   // rive est de la vallée, au nord de l'éboulement
        Assert.True(Reaches(north.Reached, min, (250, 600)), "pas de gué dans la vallée");   // rive ouest
        (double X, double Z) smin = (-120, -840), smax = (260, -210);
        var south = Flood(obstacles, smin, smax, (100, -300));   // rive est des champs
        Assert.True(Reaches(south.Reached, smin, (-50, -500)), "pas de gué dans les champs");   // rive ouest
    }

    [Fact]
    public void Rochers_des_parois_le_long_du_polygone_et_coupes_a_la_fenetre()
    {
        // Revue Codex de la PR #167 : la crête est une bande en biais de 6 m : ses rochers sont sur elle (pas sur la boîte englobante de
        // 761 × 201 m), de son épaisseur ; un rectangle droit garde les places d'avant ; seuls ceux de la fenêtre sont rendus.
        ZoneDefinition zone = Zone();
        SpaceObstacle crest = zone.Setup.Space!.Obstacles.Single(o => o.Id == "falaise_crete");
        var outline = crest.Points.Select(p => (X: p.X.Raw / 1e4, Z: p.Z.Raw / 1e4)).ToList();
        var all = StyleRules.RockFaceSpots(outline, -5000, -5000, 5000, 5000, 0).ToList();
        Assert.InRange(all[0].Depth, 5.5, 6.5);
        Assert.Equal((int)Math.Ceiling(Math.Sqrt(Math.Pow(560.4 + 200.6, 2) + Math.Pow(1277.2 - 1082.0, 2)) / StyleRules.RockFaceStep), all.Count);   // 246 rochers
        Assert.All(all, r => Assert.True(GroundRelief.PolygonDistance(outline, r.X, r.Z) == 0, $"rocher {r.Index} hors de la crête en ({r.X:0.0}, {r.Z:0.0})"));
        var window = StyleRules.RockFaceSpots(outline, 0, 1000, 200, 1300, 8).ToList();
        Assert.InRange(window.Count, 1, all.Count / 2);
        Assert.All(window, r => Assert.InRange(r.X, -8, 208));
        // Rectangle droit (front de taille) : même pas et mêmes places qu'avant (le long de x, au milieu en z).
        var box = new List<(double X, double Z)> { (0, 0), (32, 0), (32, 4), (0, 4) };
        var spots = StyleRules.RockFaceSpots(box, -100, -100, 100, 100, 0).ToList();
        Assert.Equal(10, spots.Count);
        Assert.Equal((1.6, 2.0, 4.0), (spots[0].X, spots[0].Z, spots[0].Depth));
        Assert.Equal((1.0, 0.0, 0.0, 1.0), (spots[0].DirX, spots[0].DirZ, spots[0].NormalX, spots[0].NormalZ));
    }
}
