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
        // 84 × 58 m et 10 m de forêt de chaque côté, à 6 pixels par mètre.
        Assert.Equal((624, 468), (width, height));
        byte[] image = MapPainter.Paint(map, width, height);
        Assert.Equal(width * height * 4, image.Length);
        // Pour regarder l'image (EDS_CARTE=chemin) : RVBA brut, lignes du sud au nord.
        string? dump = Environment.GetEnvironmentVariable("EDS_CARTE");
        if (!string.IsNullOrEmpty(dump)) File.WriteAllBytes(dump, image);
        Assert.Equal(image, MapPainter.Paint(map, width, height));   // sans hasard
        Assert.All(Enumerable.Range(0, width * height), i => Assert.Equal(255, image[i * 4 + 3]));

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
        // Hors de l'aire, à l'ouest (sous-bois et lisière), plus sombre en moyenne que le pré à l'ouest du Seuil.
        double Mean(double xa, double za) => Enumerable.Range(0, 49).Select(i => At(image, width, height, map, xa + i % 7, za + i / 7)).Average(p => p.R + p.G + p.B);
        Assert.True(Mean(-33, 5) < Mean(28, 28) - 0.1, $"forêt {Mean(-33, 5)} pré {Mean(28, 28)}");
        var meadow = At(image, width, height, map, 32, 32);
        Assert.True(meadow.G > meadow.R && meadow.G > meadow.B, $"pré {meadow}");
        var quarry = At(image, width, height, map, 58, -4);           // rochers du front de taille
        Assert.True(Math.Abs(quarry.R - quarry.G) < 0.12 && Math.Abs(quarry.G - quarry.B) < 0.15, $"rochers {quarry}");

        // Coordonnées de l'image : la marge est comprise.
        Assert.Equal((0.0, 0.0), MapPainter.ToImage(map, -34, 50));
        Assert.Equal((1.0, 1.0), MapPainter.ToImage(map, 70, -28));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapPainter.Paint(map, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapPainter.Paint(map, 10, 2049));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapPainter.Paint(map, 10, 10, -1));

    }
}
