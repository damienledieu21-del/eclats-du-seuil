using EdS.Runtime;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Essai réaliste 6 (essai de Damien du 27 octobre 2026 : « la mini Map […] ça fait vraiment schéma ») : la carte peinte des Vallons
/// (<see cref="MapPainter"/>) : taille, marge de forêt, et ce qu'on voit aux endroits connus (rivière, pont, toit de l'abri, moulin,
/// arbres, rochers de la carrière, forêt autour), mêmes pixels à chaque calcul.
/// </summary>
[Trait("Category", "World")]
public class MapPainterTests
{
    private static AdventureMap Map()
    {
        AdventureSession a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"),
            Path.Combine(Path.GetTempPath(), "eds_peinte_" + Guid.NewGuid().ToString("N"), "sauvegarde.json"));
        return a.Map;
    }

    private static (double R, double G, double B) At(byte[] image, int width, int height, AdventureMap map, double x, double z)
    {
        (double u, double v) = MapPainter.ToImage(map, x, z);
        int col = Math.Min(width - 1, (int)(u * width)), row = Math.Min(height - 1, (int)((1 - v) * height));   // lignes du sud au nord
        int i = (row * width + col) * 4;
        return (image[i] / 255.0, image[i + 1] / 255.0, image[i + 2] / 255.0);
    }

    [Fact]
    public void Carte_peinte_des_Vallons()
    {
        AdventureMap map = Map();
        (int width, int height) = MapPainter.Size(map);
        // Vallons agrandis : 240 × 170 m et 10 m de forêt de chaque côté, à 6 pixels par mètre.
        Assert.Equal((1560, 1140), (width, height));
        byte[] image = MapPainter.Paint(map, width, height);
        Assert.Equal(width * height * 4, image.Length);
        // Pour regarder l'image (EDS_CARTE=chemin) : RVBA brut, lignes du sud au nord.
        string? dump = Environment.GetEnvironmentVariable("EDS_CARTE");
        if (!string.IsNullOrEmpty(dump)) File.WriteAllBytes(dump, image);
        Assert.Equal(image, MapPainter.Paint(map, width, height));   // sans hasard
        int opaque = 0;
        for (int i = 3; i < image.Length; i += 4) if (image[i] == 255) opaque++;
        Assert.Equal(width * height, opaque);   // tout est opaque

        var river = At(image, width, height, map, 5.5, 20);           // milieu du bras nord
        Assert.True(river.B > river.R + 0.1 && river.B > river.G, $"rivière {river}");
        var deck = At(image, width, height, map, 5.5, -0.3);          // planches du pont, entre les deux bras
        Assert.True(deck.R > deck.B + 0.1 && deck.R > 0.25, $"pont {deck}");
        var roof = At(image, width, height, map, -14, 9.2);           // toit de tuiles de l'abri (piliers de −16 à −12, 8 à 12)
        Assert.True(roof.R > roof.G + 0.12 && roof.R > roof.B + 0.2, $"toit {roof}");
        var mill = At(image, width, height, map, -3, 9 + 2.6);        // crépi clair de la tour du moulin
        Assert.True(mill.R > 0.6 && mill.G > 0.55, $"moulin {mill}");
        var tree = At(image, width, height, map, -12, 4);             // arbre1
        Assert.True(tree.G > tree.R && tree.G > tree.B && tree.G < 0.6, $"arbre {tree}");
        // Hors de l'aire, à l'ouest (sous-bois et lisière), plus sombre en moyenne que le pré.
        double Mean(double xa, double za) => Enumerable.Range(0, 49).Select(i => At(image, width, height, map, xa + i % 7, za + i / 7)).Average(p => p.R + p.G + p.B);
        Assert.True(Mean(-109, -40) < Mean(28, 28) - 0.1, $"forêt {Mean(-109, -40)} pré {Mean(28, 28)}");
        var meadow = At(image, width, height, map, 32, 32);
        Assert.True(meadow.G > meadow.R && meadow.G > meadow.B, $"pré {meadow}");
        var quarry = At(image, width, height, map, 58, -4);           // rochers du front de taille
        Assert.True(Math.Abs(quarry.R - quarry.G) < 0.12 && Math.Abs(quarry.G - quarry.B) < 0.15, $"rochers {quarry}");

        // Vallons agrandis : routes, bâtiments, bois, lac, camp, champs.
        var dirt = At(image, width, height, map, -36, -1.2);           // grand-rue de terre battue
        Assert.True(dirt.R > dirt.G && dirt.G > dirt.B && dirt.R > 0.3, $"chemin {dirt}");
        var cobble = At(image, width, height, map, -63, -5);           // place pavée du bourg
        Assert.True(Math.Abs(cobble.R - cobble.G) < 0.1 && Math.Abs(cobble.G - cobble.B) < 0.1, $"pavés {cobble}");
        var inn = At(image, width, height, map, -69, 7.5);             // toit de tuiles sombres de l'auberge
        Assert.True(inn.R > inn.G + 0.08 && inn.R > inn.B + 0.12, $"auberge {inn}");
        var chapel = At(image, width, height, map, -71, -17);          // ardoises de la chapelle (pan est, hors du faîte)
        Assert.True(chapel.B > chapel.R && chapel.B > chapel.G - 0.02, $"chapelle {chapel}");
        var woods = Enumerable.Range(0, 25).Select(i => At(image, width, height, map, -64 + i % 5, 58 + i / 5)).ToList();   // cœur du bois du nord-ouest
        Assert.True(woods.Average(p => p.G) > woods.Average(p => p.R) && woods.Average(p => p.R + p.G + p.B) < 1.0, "bois");
        var lake = At(image, width, height, map, 5.5, -54);
        Assert.True(lake.B > lake.R + 0.1, $"lac {lake}");
        var tent = At(image, width, height, map, 112.4, 81.6);         // toile d'une tente du camp
        Assert.True(tent.R > 0.35 && tent.R > tent.B, $"tente {tent}");
        var tower = At(image, width, height, map, 28, 68);             // plancher de la tour de guet
        Assert.True(tower.R > tower.B + 0.1, $"tour {tower}");
        var fields = map.Places.Where(p => TownRules.Farmland(p.Id))
            .SelectMany(p => TownRules.Fields(p.MinX, p.MinZ, p.MaxX, p.MaxZ, map.Obstacles.Select(Box).ToList(), map.Roads, map.Clearings,
                (map.MinX, map.MinZ, map.MaxX, map.MaxZ))).ToList();
        Assert.InRange(fields.Count, 4, 30);
        StyleField wheat = fields.First(f => f.Crop == 0);
        var ripe = Enumerable.Range(0, 25).Select(i => At(image, width, height, map, wheat.MinX + 2 + i % 5 * 0.9, wheat.MinZ + 2 + i / 5 * 0.37)).ToList();
        Assert.True(ripe.Average(p => p.R - p.B) > 0.2, "blé mûr");

        // Coordonnées de l'image : la marge est comprise.
        Assert.Equal((0.0, 0.0), MapPainter.ToImage(map, -110, 110));
        Assert.Equal((1.0, 1.0), MapPainter.ToImage(map, 150, -80));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapPainter.Paint(map, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapPainter.Paint(map, 10, 2049));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapPainter.Paint(map, 10, 10, -1));
    }

    private static (double MinX, double MinZ, double MaxX, double MaxZ) Box(EdS.Domain.Space.SpaceObstacle o) =>
        (o.Points.Min(p => p.X.Raw) / 10000.0, o.Points.Min(p => p.Z.Raw) / 10000.0, o.Points.Max(p => p.X.Raw) / 10000.0, o.Points.Max(p => p.Z.Raw) / 10000.0);
}
