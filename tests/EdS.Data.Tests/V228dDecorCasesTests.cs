using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.28d (1) (PLAN_GRANDE_CARTE.md, § 6.1) : le décor de la région par cases (<see cref="CellDecor"/>) : eau, gorge et ravins,
/// routes, rochers des parois, nappes de forêt et bois, coupés à chaque case et hors du sol déjà couvert par la vue de la zone.
/// </summary>
[Trait("Category", "Sector")]
public sealed class V228dDecorCasesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v228d_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private AdventureMap Whole()
    {
        AdventureSession a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), Path.Combine(_folder, "partie.json"));
        a.Advance(0.05);
        return a.Map.Whole;
    }

    private static double Area(IReadOnlyList<(double X, double Z)> p)
    {
        double a = 0;
        for (int i = 0; i < p.Count; i++) a += p[i].X * p[(i + 1) % p.Count].Z - p[(i + 1) % p.Count].X * p[i].Z;
        return Math.Abs(a) / 2;
    }

    private static double Length(IReadOnlyList<(double X, double Z)> l) =>
        Enumerable.Range(0, l.Count - 1).Sum(i => Math.Sqrt((l[i + 1].X - l[i].X) * (l[i + 1].X - l[i].X) + (l[i + 1].Z - l[i].Z) * (l[i + 1].Z - l[i].Z)));

    [Fact]
    public void Coupes_au_rectangle_polygone_ligne_et_case_moins_le_trou()
    {
        var square = new List<(double X, double Z)> { (-5, -5), (5, -5), (5, 5), (-5, 5) };
        Assert.Equal(25, Area(CellDecor.ClipPolygon(square, (0, 0, 10, 10))), 9);
        Assert.Empty(CellDecor.ClipPolygon(square, (20, 20, 30, 30)));
        var diamond = new List<(double X, double Z)> { (0, -10), (10, 0), (0, 10), (-10, 0) };
        Assert.Equal(50, Area(CellDecor.ClipPolygon(diamond, (0, 0, 20, 20))), 9);
        // Ligne qui sort puis rentre : deux morceaux ; longueur gardée.
        var line = new List<(double X, double Z)> { (-5, 1), (5, 1), (15, 1), (15, 9), (5, 9) };
        var pieces = CellDecor.ClipPolyline(line, (0, 0, 10, 10));
        Assert.Equal(2, pieces.Count);
        Assert.Equal(15, pieces.Sum(Length), 9);   // 10 m au sud, 5 m au nord
        Assert.Equal((0.0, 1.0), pieces[0][0]);
        Assert.Equal((10.0, 9.0), pieces[1][0]);
        // Case moins le trou : sans chevauchement, aire exacte ; aucune si le trou la couvre ; entière sans trou.
        var rects = CellDecor.Outside((0, 0, 128, 128), (30, 40, 60, 200));
        Assert.Equal(128 * 128 - 30 * 88, rects.Sum(r => (r.MaxX - r.MinX) * (r.MaxZ - r.MinZ)), 9);
        Assert.Empty(CellDecor.Outside((0, 0, 128, 128), (-1, -1, 200, 200)));
        Assert.Single(CellDecor.Outside((0, 0, 128, 128), null));
        Assert.Single(CellDecor.Outside((0, 0, 128, 128), (200, 200, 300, 300)));
    }

    [Fact]
    public void Decor_de_toute_la_region_sans_double_ni_manque()
    {
        AdventureMap whole = Whole();
        RegionGrid grid = whole.Region!;
        var sets = grid.Cells.Select(c => CellDecor.For(whole, grid, c)).ToList();
        // L'eau des cases fait l'eau de la région (coupée à ses bornes) ; les routes, leur longueur entière ; les rochers, chacun une fois.
        (double, double, double, double) bounds = (grid.OriginX, grid.OriginZ, grid.OriginX + grid.Columns * 128.0, grid.OriginZ + grid.Rows * 128.0);
        double water = whole.Obstacles.Where(o => o.Low && o.Id.StartsWith("riviere", StringComparison.Ordinal))
            .Sum(o => Area(CellDecor.ClipPolygon(o.Points.Select(p => (p.X.Raw / 1e4, p.Z.Raw / 1e4)).ToList(), bounds)));
        Assert.Equal(water, sets.Sum(s => s.Water.Sum(Area)), 3);
        Assert.Equal(whole.Roads.Sum(r => Length(r.Points)), sets.Sum(s => s.Roads.Sum(r => Length(r.Points))), 3);
        var rocks = sets.SelectMany(s => s.Rocks).Select(r => r.Id).ToList();
        Assert.Equal(rocks.Count, rocks.Distinct().Count());
        int expected = whole.Obstacles.Where(o => StyleRules.RockFace(o.Id))
            .Sum(o => StyleRules.RockFaceSpots(o.Points.Select(p => (p.X.Raw / 1e4, p.Z.Raw / 1e4)).ToList(), bounds.Item1, bounds.Item2, bounds.Item3 - 1e-9, bounds.Item4 - 1e-9, 0).Count());
        Assert.Equal(expected, rocks.Count);
        Assert.Contains(sets.SelectMany(s => s.Ravines), r => Area(r) > 100);
        // Arbres : la forêt profonde est une nappe (épicéas et pins) ; aucun sur une route ni dans un obstacle ; au plus le plafond par case.
        Assert.All(sets, s => Assert.InRange(s.Trees.Count, 0, CellDecor.MaxTreesPerCell));
        CellDecorSet deep = sets.Single(s => s.Cell.Name == "c8_r19");   // (−884, 818) : forêt des Loups profonde
        Assert.True(deep.Trees.Count > 150, deep.Trees.Count + " arbres");
        Assert.All(deep.Trees, t => Assert.True(t.Wolves));
        foreach (CellTree t in sets.SelectMany(s => s.Trees).Where((t, i) => i % 37 == 0))
        {
            Assert.True(TownRules.RoadAt(t.X, t.Z, whole.Roads).Edge >= CellDecor.RoadGap);
            Assert.DoesNotContain(whole.Obstacles, o => TownRules.BuildingFor(o.Id) != StyleBuilding.Woods
                && StyleRules.InsideDistance(t.X, t.Z, o.Points.Select(p => (p.X.Raw / 1e4, p.Z.Raw / 1e4)).ToList()) > -CellDecor.ObstacleGap);
        }
        // Déterministe.
        CellDecorSet again = CellDecor.For(whole, grid, deep.Cell);
        Assert.Equal(deep.Trees.Select(t => (t.X, t.Z, t.Scale)), again.Trees.Select(t => (t.X, t.Z, t.Scale)));
    }

    [Fact]
    public void Rien_sous_le_sol_deja_couvert_par_la_vue_de_la_zone()
    {
        AdventureMap whole = Whole();
        RegionGrid grid = whole.Region!;
        (double, double, double, double) hole = (-180, -142, 204, 242);   // la fenêtre du cœur
        foreach (RegionCell cell in grid.Cells.Where(c => c.Column >= 12 && c.Column <= 17 && c.Row >= 10 && c.Row <= 15))
        {
            CellDecorSet set = CellDecor.For(whole, grid, cell, hole);
            bool Inside(double x, double z) => x > -180 + 1e-6 && x < 204 - 1e-6 && z > -142 + 1e-6 && z < 242 - 1e-6;
            Assert.DoesNotContain(set.Trees, t => Inside(t.X, t.Z));
            Assert.DoesNotContain(set.Rocks, r => Inside(r.X, r.Z));
            Assert.DoesNotContain(set.Water.SelectMany(p => p), p => Inside(p.X, p.Z));
            Assert.DoesNotContain(set.Roads.SelectMany(r => r.Points), p => Inside(p.X, p.Z));
        }
        // La case du village (dans la fenêtre) n'a rien ; sans trou, elle a ses routes.
        RegionCell village = grid.Cells.Single(c => c.Name == "c14_r12");
        Assert.True(CellDecor.For(whole, grid, village, hole).IsEmpty);
        Assert.NotEmpty(CellDecor.For(whole, grid, village).Roads);
    }

    [Fact]
    public void Arbres_sans_double_entre_cases_meme_resultat_dans_tout_ordre_et_plafond_reparti()
    {
        // Relecture de la PR #173 : aucun arbre en double d'une case à l'autre (ni entre deux lieux de forêt qui se chevauchent) ; même décor
        // quel que soit l'ordre de chargement des cases ; le plafond garde un sous-échantillon uniforme (toute la case reste boisée).
        AdventureMap whole = Whole();
        RegionGrid grid = whole.Region!;
        var forward = grid.Cells.Select(c => CellDecor.For(whole, grid, c)).ToList();
        var backward = grid.Cells.Reverse().Select(c => CellDecor.For(whole, grid, c)).Reverse().ToList();
        var all = forward.SelectMany(s => s.Trees).Select(t => (Math.Round(t.X, 6), Math.Round(t.Z, 6))).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
        for (int i = 0; i < forward.Count; i++)
            Assert.Equal(forward[i].Trees.Select(t => (t.X, t.Z, t.Scale, t.Wolves)), backward[i].Trees.Select(t => (t.X, t.Z, t.Scale, t.Wolves)));
        // Plafond : une case de la forêt profonde réduite à 60 arbres en garde 60, dans chacun de ses quatre quarts.
        RegionCell deep = grid.Cells.Single(c => c.Name == "c8_r19");
        CellDecorSet full = CellDecor.For(whole, grid, deep), capped = CellDecor.For(whole, grid, deep, null, 60);
        Assert.True(full.Trees.Count > 60);
        Assert.Equal(60, capped.Trees.Count);
        Assert.All(capped.Trees, t => Assert.Contains(full.Trees, f => f.X == t.X && f.Z == t.Z));
        double mx = (capped.MinX + capped.MaxX) / 2, mz = (capped.MinZ + capped.MaxZ) / 2;
        foreach ((bool east, bool north) in new[] { (false, false), (true, false), (false, true), (true, true) })
            Assert.Contains(capped.Trees, t => (t.X >= mx) == east && (t.Z >= mz) == north);
        Assert.Empty(CellDecor.For(whole, grid, deep, null, 0).Trees);
        Assert.Throws<ArgumentOutOfRangeException>(() => CellDecor.For(whole, grid, deep, null, -1));
    }
}
