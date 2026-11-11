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
        AdventureMap map = this.At(null).Map;
        // Vallons agrandis (28 octobre 2026) : 240 × 170 m.
        Assert.Equal((-100.0, -70.0, 140.0, 100.0), (map.MinX, map.MinZ, map.MaxX, map.MaxZ));
        // 240 × 170 m en 240 × 170 pixels : un pixel par mètre, la première ligne au sud.
        byte[] image = map.Render(240, 170);
        Assert.Equal(240 * 170 * 4, image.Length);
        byte[] Cell(double x, double z) => Pixel(image, 240, (int)Math.Floor(x) + 100, (int)Math.Floor(z) + 70);
        Assert.Equal(AdventureMap.Solid, Cell(-3, 9));            // le moulin, de [−6, 6] à [0, 12]
        Assert.Equal(AdventureMap.Water, Cell(5, 20));            // la rivière du nord
        Assert.Equal(AdventureMap.Ground, Cell(-18, -12));        // la place du village
        Assert.Equal(AdventureMap.Solid, Cell(58, 0));            // le front de taille de la carrière
        Assert.Equal(AdventureMap.Solid, Cell(-3, 19));           // la porte du moulin, fermée
        Assert.Equal(AdventureMap.Solid, Cell(-30, 7));           // une maison du bourg
        Assert.Equal(AdventureMap.Water, Cell(5, -54));           // le lac
        Assert.Equal(AdventureMap.Ground, Cell(-58, -5));         // la place pavée du bourg
        // Coordonnées de la carte : 0 à 1, nord en haut.
        Assert.Equal((0.0, 0.0), map.ToMap(-100, 100));
        Assert.Equal((1.0, 1.0), map.ToMap(140, -70));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.Render(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.Render(10, 2049));
        Assert.Equal(new[] { "village", "moulin", "riviere", "pre_nord", "ruines", "carriere", "seuil", "coeur_du_seuil", "forge", "bourg", "champs", "lac", "vergers",
            "hameau", "plaine_est", "hautes_pierres", "tour_de_guet", "camp_des_rodeurs", "foret_des_loups" }, map.Places.Select(p => p.Id));
        // Les routes viennent des données, dans leur ordre.
        Assert.Equal(new[] { "grand_rue", "chemin_de_la_place", "place_du_bourg", "grand_rue_ouest", "chemin_des_champs", "chemin_du_moulin", "passage_des_ruines", "chemin_de_l_est",
            "chemin_du_nord", "chemin_du_lac", "chemin_de_la_grange", "aire_de_la_grange" }, map.Roads.Select(r => r.Id));
        Assert.True(Assert.Single(map.Roads, r => r.Paved).Id == "place_du_bourg");
        Assert.Contains(map.Clearings, p => p == (-18.0, -12.0));   // point sûr de la place du village
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
        Assert.Equal(new[] { "Odile", "Maëlle", "Gaspard", "Garin", "Sauge", "Bertille", "Firmin", "Aldric", "Renart", "Lison", "Colombe", "Maître Josse", "Aubine", "Achille", "Garance", "Mahaut", "Thibaut", "Le capitaine Bérenger", "Colin", "Ysarn", "Gaut le receleur" }, markers.Where(m => m.Kind == MapMarkerKind.Npc).Select(m => m.Name.Split(',')[0]));   // V1.1 : marchands
        Assert.Equal(5, markers.Count(m => m.Kind == MapMarkerKind.SafePoint));   // Vallons agrandis : bourg, ferme, hameau
        MapMarker forge = Assert.Single(markers, m => m.Kind == MapMarkerKind.Workshop);
        Assert.Equal((-20.0, -4.0), (forge.X, forge.Z));
        Assert.Contains(markers, m => m.Kind == MapMarkerKind.Node);
        MapMarker me = Assert.Single(markers, m => m.Kind == MapMarkerKind.Player);
        Assert.True(Math.Abs(me.X + 18) < 1 && Math.Abs(me.Z + 12) < 1, $"({me.X}, {me.Z})");
        // Nouvelle partie : depuis V1.9.6, l'ouverture est suivie, son objectif est chez Odile.
        MapMarker objective = a.Objective!;
        Assert.True(Math.Abs(objective.X + 8) < 1.5 && Math.Abs(objective.Z - 3) < 1.5, $"({objective.X}, {objective.Z})");
    }

    [Theory]
    [InlineData("enquete", -8.0, 3.0)]     // apprendre le récit : Odile
    [InlineData("traces", -3.0, 11.5)]     // atteindre le moulin : centre du lieu
    [InlineData("passage", -20.0, -4.0)]   // la porte : Maëlle la pose
    [InlineData("pre", -10.0, 30.0)]       // le pré des loups
    [InlineData("gaspard", 27.0, -13.0)]   // V1.9.1 : fin du chapitre 1, Gaspard au camp de la carrière
    [InlineData("manifestation", 50.0, 32.0)]
    [InlineData("rodeur", 42.0, 26.0)]     // la place du rôdeur (relecture de la PR #83 : éloigné du Gardien)
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
    public void Une_porte_ouverte_change_la_signature_de_la_carte()
    {
        // Vallons agrandis : la carte peinte est gardée d'une halte à l'autre tant que sa signature ne change pas ; la porte du moulin
        // ouverte (passage gardé dans la sauvegarde) la retire de la carte, et l'image est repeinte.
        string closed = At(null).Map.Signature;
        var progression = ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;
        SaveStore.Write(SavePath, new WorldSave("zone.vallons", 0, 0, new[] { "zone.vallons/passage/moulin" },
            PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, progression)));
        AdventureMap open = AdventureSession.Load(DataDirectory, Scenarios, SavePath).Map;
        Assert.DoesNotContain(open.Obstacles, o => o.Id == "porte_moulin");
        Assert.NotEqual(closed, open.Signature);
        Assert.Contains("porte_moulin", closed, StringComparison.Ordinal);
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
        // Vallons agrandis : la halte donne une autre carte, mais la même image (même signature : l'image n'est pas repeinte).
        Assert.Equal(first.Signature, a.Map.Signature);
        AdventureSession again = AdventureSession.Load(DataDirectory, Scenarios, SavePath);
        Assert.Equal(1, again.Opened);
        Assert.NotSame(first, again.Map);
        // Essai réaliste 6 : la carte peinte (PaintedMap) remplace l'image du plan ; même clé (la carte elle-même).
        string script = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "PaintedMap.cs"));
        Assert.Contains("if (!ReferenceEquals(map, _source))", script, StringComparison.Ordinal);
        Assert.Contains("map.Signature == _source.Signature", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Opened", script, StringComparison.Ordinal);
    }
}

