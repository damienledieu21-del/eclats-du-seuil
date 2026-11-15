using System.Diagnostics;
using EdS.Domain.Space;
using EdS.Runtime;
using static EdS.Data.Tests.V228b2PassageTests;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.28c, relecture indépendante de la PR #166 : images des cartes à mémoire bornée (<see cref="MapImages"/>) et peinture de
/// l'aire entière d'une région, en temps et en mémoire bornés (grille de relief échantillonnée).
/// </summary>
[Trait("Category", "Sector")]
public class V228cImagesTests
{
    private static AdventureMap Area(double maxX, double maxZ) => new(new CombatSpace(At(0, 0), At(maxX, maxZ)), Array.Empty<MapPlace>());

    private static AdventureMap Window(AdventureMap whole, double minX, double maxX)
    {
        var map = new AdventureMap(new CombatSpace(At(minX, 0), At(maxX, whole.MaxZ)), Array.Empty<MapPlace>());
        map.Whole = whole;
        return map;
    }

    /// <summary>Calculs tenus à la main : chacun finit quand l'essai le dit, et relève s'il a été annulé.</summary>
    private sealed class Painter
    {
        public readonly List<(AdventureMap Map, TaskCompletionSource<byte[]> Done, CancellationToken Cancel)> Started = new();

        public Task<byte[]> Paint(AdventureMap map, int w, int h, CancellationToken cancel)
        {
            var done = new TaskCompletionSource<byte[]>();
            Started.Add((map, done, cancel));
            return done.Task;
        }

        public void Finish(AdventureMap map) => Started.Single(s => ReferenceEquals(s.Map, map)).Done.SetResult(new byte[4]);
    }

    [Fact]
    public void Aire_entiere_gardee_en_priorite_ancienne_fenetre_montree_jusqu_a_la_nouvelle_calcul_oublie_annule()
    {
        var painter = new Painter();
        var images = new MapImages(painter.Paint);
        var evicted = new List<AdventureMap>();
        images.Evicted += e => evicted.Add(e.Source);
        AdventureMap whole = Area(1100, 200), w1 = Window(whole, 0, 384), w2 = Window(whole, 200, 584), w3 = Window(whole, 400, 784);
        images.For(whole);
        painter.Finish(whole);
        Assert.Same(whole, images.Shown(w1)!.Source);   // la fenêtre se peint : l'aire entière, prête, est montrée
        painter.Finish(w1);
        Assert.Same(w1, images.Shown(w1)!.Source);
        // Nouvelle fenêtre : l'ancienne reste montrée pendant qu'elle se peint ; l'aire entière n'est jamais oubliée pour une fenêtre.
        Assert.Same(w1, images.Shown(w2)!.Source);
        Assert.Equal(3, images.Entries.Count);
        painter.Finish(w2);
        Assert.Same(w2, images.Shown(w2)!.Source);
        Assert.Equal(new[] { w1 }, evicted);   // la fenêtre périmée part une fois la nouvelle prête
        Assert.Contains(images.Entries, e => ReferenceEquals(e.Source, whole));
        // Deux passages coup sur coup : la fenêtre pas encore prête part (calcul annulé), la dernière prête reste montrée.
        images.For(w3);
        AdventureMap w4 = Window(whole, 600, 984);
        images.For(w4);
        Assert.Contains(w3, evicted);
        Assert.True(painter.Started.Single(s => ReferenceEquals(s.Map, w3)).Cancel.IsCancellationRequested);
        Assert.Same(w2, images.Shown(w4)!.Source);
        Assert.True(images.Entries.Count <= 1 + MapImages.MaxWindows);
        // Le calcul annulé qui finit quand même est ignoré.
        painter.Finish(w3);
        Assert.DoesNotContain(images.Entries, e => ReferenceEquals(e.Source, w3));
        // Une autre aire entière (autre zone) remplace la première.
        AdventureMap other = Area(300, 300);
        images.For(other);
        Assert.Contains(whole, evicted);
        Assert.Single(images.Entries, e => ReferenceEquals(e.Source.Whole, e.Source));
    }

    [Fact]
    public void Peinture_de_toute_la_region_bornee_en_temps_et_en_memoire()
    {
        // L'aire de toute la région des Vallons (4 096 × 3 072 m), avec des routes (relief non plat) : grille de relief d'au plus 512 cases de
        // côté (pas de 8 m), image d'au plus 2 048 pixels de côté ; mémoire et temps bornés.
        var space = new CombatSpace(At(-1908, -1614), At(2188, 1458),
            Enumerable.Range(0, 40).Select(i => new SpaceObstacle($"maison{i}", new[] { At(-1800 + 90 * i, 0), At(-1790 + 90 * i, 0), At(-1790 + 90 * i, 8), At(-1800 + 90 * i, 8) })));
        var roads = new[] { new StyleRoad("grand_rue", new[] { (-1800.0, -10.0), (2000.0, -10.0) }, 4, false) };
        var map = new AdventureMap(space, Array.Empty<MapPlace>(), roads);
        Assert.False(GroundRelief.For(map).IsFlat);
        double step = MapPainter.ReliefStep(map.Width + 20, map.Depth + 20);
        Assert.Equal(9.0, step);   // 4 116 m sur 512 cases au plus
        Assert.True((map.Width + 20) / step <= MapPainter.ReliefCells);
        (int w, int h) = MapPainter.Size(map);
        Assert.True(w <= 2048 && h <= 2048);
        // Mémoire gardée par la peinture (l'image elle-même, et rien d'autre une fois finie) : les essais tournent en parallèle, les
        // allocations cumulées des autres ne sont pas mesurables ici ; la grille de relief est bornée par son pas (ci-dessus).
        long before = GC.GetTotalMemory(true);
        var clock = Stopwatch.StartNew();
        byte[] pixels = MapPainter.Paint(map, w, h);
        clock.Stop();
        long kept = GC.GetTotalMemory(true) - before;
        Assert.Equal(w * h * 4, pixels.Length);
        Assert.True(kept < 64L * 1024 * 1024, $"{kept / (1024 * 1024)} Mo gardés");
        Assert.True(clock.Elapsed < TimeSpan.FromMinutes(3), clock.Elapsed.ToString());
    }
}
