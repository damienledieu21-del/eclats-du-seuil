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
    private static (HashSet<(int, int)> Reached, bool Edge) Flood(IEnumerable<SpaceObstacle> obstacles, (double X, double Z) min, (double X, double Z) max, (double X, double Z) from)
    {
        const double step = 0.5, radius = 0.5;
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

    private static bool Reaches(HashSet<(int, int)> reached, (double X, double Z) min, (double X, double Z) at) =>
        reached.Contains(((int)((at.X - min.X) / 0.5), (int)((at.Z - min.Z) / 0.5)));

    [Fact]
    public void Aire_de_toute_la_region()
    {
        ZoneDefinition zone = Zone();
        CombatSpace area = zone.Setup.Space!;
        RegionGrid region = zone.Region!;
        Assert.Equal((region.Min, region.Max), (area.Min, area.Max));
        Assert.False(SectorMap.For(zone).HomeHoldsArea);
        foreach (string id in new[] { "grand_lac", "gorge_ouest", "ravin_ronces_sud", "ravin_ronces_est", "eboulement_nord", "falaise_collines", "falaise_cascade" })
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

    [Fact]
    public void Gues_de_la_riviere_on_la_traverse_au_dela_de_l_eboulement_et_vers_le_lac()
    {
        // La rivière (obstacle bas) se traverse à ses gués : au nord de l'éboulement (vallée), et entre l'étang et le grand lac (champs).
        ZoneDefinition zone = Zone();
        var obstacles = zone.Setup.Space!.Obstacles;
        (double X, double Z) min = (150, 380), max = (650, 1060);
        var north = Flood(obstacles, min, max, (300, 420));   // rive est de la vallée, au nord de l'éboulement
        Assert.True(Reaches(north.Reached, min, (250, 600)), "pas de gué dans la vallée");   // rive ouest
        (double X, double Z) smin = (-120, -840), smax = (260, -210);
        var south = Flood(obstacles, smin, smax, (100, -300));   // rive est des champs
        Assert.True(Reaches(south.Reached, smin, (-50, -500)), "pas de gué dans les champs");   // rive ouest
    }
}
