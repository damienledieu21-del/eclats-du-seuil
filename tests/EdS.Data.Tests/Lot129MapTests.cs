using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 12.9d (essai de Damien du 25 octobre 2026 : « une mini carte en haut à droite qu'on puisse agrandir si besoin ») : la carte
/// des Vallons calculée par le cœur (image du décor, nord en haut) et ses repères, dont l'objectif de l'étape en cours tiré des données.
/// </summary>
[Trait("Category", "World")]
public class Lot129MapTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_carte_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private string SavePath => Path.Combine(_folder, "partie", "sauvegarde.json");
    private static string Scenarios => Path.Combine(AppContext.BaseDirectory, "scenarios");

    private AdventureSession At(string? step)
    {
        if (step != null)
        {
            var progression = ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;
            var story = new SavedStory(null, new Dictionary<string, QuestProgress> { ["quest.traces_sous_le_moulin"] = new QuestProgress(step) }, null);
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
            SaveStore.Write(SavePath, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(),
                PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, progression).WithStory(story)));
        }
        AdventureSession a = AdventureSession.Load(DataDirectory, Scenarios, SavePath);
        a.Advance(0.05);
        return a;
    }

    private static byte[] Pixel(byte[] image, int width, int col, int row) => image.Skip((row * width + col) * 4).Take(4).ToArray();

    [Fact]
    public void Image_du_decor_nord_en_haut()
    {
        AdventureMap map = At(null).Map;
        Assert.Equal((-24.0, -18.0, 60.0, 40.0), (map.MinX, map.MinZ, map.MaxX, map.MaxZ));
        // 84 × 58 m en 84 × 58 pixels : un pixel par mètre, la première ligne au sud.
        byte[] image = map.Render(84, 58);
        Assert.Equal(84 * 58 * 4, image.Length);
        Assert.Equal(AdventureMap.Solid, Pixel(image, 84, -3 + 24, 9 + 18));            // le moulin, de [−6, 6] à [0, 12]
        Assert.Equal(AdventureMap.Water, Pixel(image, 84, 5 + 24, 20 + 18));            // la rivière du nord
        Assert.Equal(AdventureMap.Ground, Pixel(image, 84, -18 + 24, -12 + 18));        // la place du village
        Assert.Equal(AdventureMap.Solid, Pixel(image, 84, 58 + 24, 0 + 18));            // le front de taille de la carrière
        Assert.Equal(AdventureMap.Solid, Pixel(image, 84, -3 + 24, 19 + 18));           // la porte du moulin, fermée
        // Coordonnées de la carte : 0 à 1, nord en haut.
        Assert.Equal((0.0, 0.0), map.ToMap(-24, 40));
        Assert.Equal((1.0, 1.0), map.ToMap(60, -18));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.Render(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.Render(10, 2049));
        Assert.Equal(new[] { "village", "moulin", "riviere", "pre_nord", "ruines", "carriere", "seuil", "forge" }, map.Places.Select(p => p.Id));
    }

    [Fact]
    public void Point_dans_un_polygone_convexe_dans_les_deux_sens()
    {
        var square = new[] { (0.0, 0.0), (2.0, 0.0), (2.0, 2.0), (0.0, 2.0) };
        Assert.True(AdventureMap.Inside(square, 1, 1));
        Assert.True(AdventureMap.Inside(square.Reverse().ToArray(), 1, 1));
        Assert.True(AdventureMap.Inside(square, 2, 1));   // bord compris
        Assert.False(AdventureMap.Inside(square, 3, 1));
        Assert.False(AdventureMap.Inside(square.Reverse().ToArray(), -0.1, 1));
    }

    [Fact]
    public void Reperes_de_la_carte()
    {
        AdventureSession a = At(null);
        IReadOnlyList<MapMarker> markers = a.MapMarkers();
        Assert.Equal(new[] { "Odile", "Maëlle", "Gaspard" }, markers.Where(m => m.Kind == MapMarkerKind.Npc).Select(m => m.Name.Split(',')[0]));
        Assert.Equal(2, markers.Count(m => m.Kind == MapMarkerKind.SafePoint));
        MapMarker forge = Assert.Single(markers, m => m.Kind == MapMarkerKind.Workshop);
        Assert.Equal((-20.0, -4.0), (forge.X, forge.Z));
        Assert.Contains(markers, m => m.Kind == MapMarkerKind.Node);
        MapMarker me = Assert.Single(markers, m => m.Kind == MapMarkerKind.Player);
        Assert.True(Math.Abs(me.X + 18) < 1 && Math.Abs(me.Z + 12) < 1, $"({me.X}, {me.Z})");
        // Nouvelle partie : pas encore de quête, pas d'objectif.
        Assert.Null(a.Objective);
        Assert.DoesNotContain(markers, m => m.Kind == MapMarkerKind.Objective);
    }

    [Theory]
    [InlineData("enquete", -8.0, 3.0)]     // apprendre le récit : Odile
    [InlineData("traces", -3.0, 11.5)]     // atteindre le moulin : centre du lieu
    [InlineData("passage", -20.0, -4.0)]   // la porte : Maëlle la pose
    [InlineData("pre", -10.0, 30.0)]       // le pré des loups
    [InlineData("manifestation", 50.0, 32.0)]
    [InlineData("rodeur", 49.0, 32.0)]     // la place du rôdeur
    [InlineData("retour", -8.0, 3.0)]      // revenir chez Odile
    public void L_objectif_suit_l_etape_de_la_mission(string step, double x, double z)
    {
        MapMarker goal = At(step).Objective!;
        Assert.Equal((MapMarkerKind.Objective, "Objectif"), (goal.Kind, goal.Name));
        Assert.Equal((x, z), (goal.X, goal.Z));
    }

    [Fact]
    public void L_objectif_du_chef_est_sa_premiere_forme()
    {
        AdventureSession a = At("chef");
        MapMarker goal = a.Objective!;
        MapPlace seuil = a.Map.Places.First(p => p.Id == "seuil");
        Assert.InRange(goal.X, seuil.MinX, seuil.MaxX);
        Assert.InRange(goal.Z, seuil.MinZ, seuil.MaxZ);
        Assert.Contains(a.MapMarkers(), m => m.Kind == MapMarkerKind.Objective);
    }

    [Fact]
    public void Une_carte_par_session_et_par_aventure()
    {
        // Revue Codex de la PR #53 : l'image de Unity est gardée tant que la carte est la même ; une nouvelle session (halte) ou une nouvelle
        // aventure (retour de l'entraînement, Opened repart à 1) donne une autre carte, donc une image refaite.
        AdventureSession a = At(null);
        AdventureMap first = a.Map;
        Assert.Same(first, a.Map);
        Assert.True(a.Rest());
        Assert.NotSame(first, a.Map);
        AdventureSession again = AdventureSession.Load(DataDirectory, Scenarios, SavePath);
        Assert.Equal(1, again.Opened);
        Assert.NotSame(first, again.Map);
        // Essai réaliste 6 : la carte peinte (PaintedMap) remplace l'image du plan ; même clé (la carte elle-même).
        string script = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "PaintedMap.cs"));
        Assert.Contains("if (!ReferenceEquals(map, _source))", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Opened", script, StringComparison.Ordinal);
    }
}

