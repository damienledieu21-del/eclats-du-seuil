using System;
using System.IO;
using System.Linq;
using EdS.Data.Caravans;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Runtime;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.41.2 : affichage des caravanes (côté cœur de ce que montre Unity) : modèles prévus branchés par leur nom, pièces de chaque
/// état (en route, attaquée, pillée), caravanes de l'aventure (aucune dans un intérieur).
/// </summary>
[Trait("Category", "World")]
public class V2412CaravanesUnityTests
{
    private static ZoneDefinition Zone { get; } = ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"),
        Path.Combine(AppContext.BaseDirectory, "scenarios"), ShippedData, ItemsCatalog).Zone!;

    private static Caravan Ambre { get; } = CaravanJsonReader.ReadFile(CaravanJsonReader.PathIn(DataDirectory), Zone, ItemsCatalog).Book!.Caravans[0];

    [Theory]
    [InlineData("Assets/EdS/Modeles/Caravanes/chariot_bache/chariot_bache.fbx", "chariot_bache")]
    [InlineData("Assets/EdS/Modeles/Caravanes/attelage/attelage.FBX", "attelage")]
    [InlineData("Assets\\EdS\\Modeles\\Caravanes\\coffres_marchandises\\coffres_marchandises.glb", "coffres_marchandises")]
    [InlineData("Assets/EdS/Modeles/Caravanes/attelage/boeufs.fbx", null)]
    [InlineData("Assets/EdS/Modeles/Caravanes/charrette/charrette.fbx", null)]
    [InlineData("Assets/EdS/Modeles/Caravanes/attelage.fbx", null)]
    [InlineData("Assets/EdS/Modeles/Fetes/attelage/attelage.fbx", null)]
    [InlineData("Assets/EdS/Modeles/Caravanes/attelage/attelage.png", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Modele_reconnu_par_son_nom(string? path, string? name) => Assert.Equal(name, CaravanLooks.NameOf(path));

    [Fact]
    public void Noms_et_tailles_ceux_de_la_liste_de_l_atelier()
    {
        Assert.Equal(new[] { "chariot_bache", "attelage", "coffres_marchandises" }, CaravanLooks.Models);
        Assert.Equal((4.2, 2.0, 2.8), CaravanLooks.Size(CaravanLooks.Wagon));
        Assert.Throws<ArgumentException>(() => CaravanLooks.Size("dragon"));
    }

    private static CaravanView View(CaravanState state)
    {
        for (long d = 1; d < 400; d++)
            if (WorldEventSchedule.InWindow(Zone.Id, Zone.Id, Ambre.Attack, d) is EventOccurrence o && o.Day == d)
            {
                long at = state == CaravanState.OnRoad ? (o.StartAt - 60) * 1000 : state == CaravanState.Attacked ? (o.StartAt + 10) * 1000 : (o.EndAt + 10) * 1000;
                return CaravanSchedule.At(Zone.Id, Zone.Id, Ambre, at, _ => state == CaravanState.Pillaged ? EventOutcome.Lost : (EventOutcome?)null)!;
            }
        throw new InvalidOperationException();
    }

    [Fact]
    public void Pieces_de_chaque_etat()
    {
        var road = CaravanLooks.Layout(View(CaravanState.OnRoad));
        Assert.Equal(new[] { "attelage", "chariot_bache", "attelage", "chariot_bache" }, road.Select(p => p.Model));
        Assert.All(road, p => Assert.Equal(0, p.Roll));
        // Chaque attelage est devant son chariot, sans le toucher ; le second chariot suit à 9 m.
        Assert.True(road[0].Forward - road[1].Forward >= (CaravanLooks.Size("chariot_bache").Length + CaravanLooks.Size("attelage").Length) / 2);
        Assert.Equal(-CaravanLooks.Spacing, road[3].Forward);
        var attacked = CaravanLooks.Layout(View(CaravanState.Attacked));
        Assert.Equal("coffres_marchandises", attacked.Last().Model);
        var pillaged = CaravanLooks.Layout(View(CaravanState.Pillaged));
        Assert.DoesNotContain(pillaged, p => p.Model == CaravanLooks.Team);   // les bêtes ont été emmenées
        Assert.Equal(90, pillaged[0].Roll);   // le chariot de tête couché sur le flanc
        Assert.Equal(2, pillaged.Count(p => p.Model == CaravanLooks.Goods));
        Assert.Throws<ArgumentNullException>(() => CaravanLooks.Layout(null!));
    }

    [Fact]
    public void Caravanes_de_l_aventure_au_dehors_seulement()
    {
        string dir = Path.Combine(Path.GetTempPath(), "caravanes_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            AdventureSession a = Adventure(Path.Combine(dir, "save.json"));
            Assert.NotNull(a.World.CaravanBook);
            // Partie neuve, jour 0 : la première caravane ne part que le jour 1 à 8 h.
            Assert.Equal(0, a.World.Zone.Time.Day);
            Assert.Empty(a.Caravans);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Modèles Tripo des caravanes (dépense validée par Damien le 21 novembre 2026) versés à l'endroit où le menu les cherche : chacun a son
    /// FBX reconnu par son nom, sa couleur et sa normale à côté ; les formes de remplacement restent pour un modèle absent.
    /// </summary>
    [Fact]
    public void Modeles_Tripo_verses_et_reconnus()
    {
        string root = Path.Combine(Lot93UnityPackageTests.Root, "unity");
        foreach (string name in CaravanLooks.Models)
        {
            string fbx = CaravanLooks.Folder + "/" + name + "/" + name + ".fbx";
            Assert.True(File.Exists(Path.Combine(root, fbx)), fbx);
            Assert.Equal(name, CaravanLooks.NameOf(fbx));
            Assert.True(File.Exists(Path.Combine(root, CaravanLooks.Folder, name, name + "_couleur.jpg")), name);
            Assert.True(File.Exists(Path.Combine(root, CaravanLooks.Folder, name, name + "_normale.png")), name);
            Assert.True(new FileInfo(Path.Combine(root, fbx)).Length < 2_000_000, name);   // allégé pour le mobile
        }
        // Aucun autre modèle dans le dossier (un nom inconnu ne serait pas branché).
        string[] all = Directory.GetFiles(Path.Combine(root, CaravanLooks.Folder), "*.fbx", SearchOption.AllDirectories);
        Assert.Equal(CaravanLooks.Models.Count, all.Length);
    }
}
