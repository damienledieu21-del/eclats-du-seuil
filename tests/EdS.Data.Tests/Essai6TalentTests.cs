using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Domain.Builds;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Essai réaliste 6 (essai de Damien du 27 octobre 2026 : « il y a aussi l'arbre des compétences a retravailler mais je ne peux pas
/// l'ouvrir ») : dans l'aventure, les points de talent se placent hors combat (MONDE.md), jugés à son niveau actuel par le validateur
/// unique des builds ; ils sont sauvegardés et prennent effet à la prochaine halte, comme l'équipement.
/// </summary>
[Trait("Category", "World")]
public class Essai6TalentTests : IDisposable
{
    private const string GardeStable = "talent.bastion.rempart.garde_stable";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_talents_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static CharacterProgression Shipped =>
        ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;

    /// <summary>Partie au niveau 4 (519 points d'expérience) ; <paramref name="wolfNearEntry"/> : un loup posé près du point sûr d'entrée.</summary>
    private AdventureSession Start(bool wolfNearEntry = false)
    {
        string scenarios = Path.Combine(AppContext.BaseDirectory, "scenarios");
        if (wolfNearEntry)
        {
            scenarios = Path.Combine(_folder, "scenarios");
            Directory.CreateDirectory(Path.Combine(scenarios, "zones"));
            JsonObject vallons = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
            foreach (JsonNode? c in vallons["data"]!["combatants"]!.AsArray())
                if ((string)c!["id"]! == "loup1") c["position"] = new JsonArray(-15, -12);
            File.WriteAllText(Path.Combine(scenarios, "zones", "vallons.json"), vallons.ToJsonString());
            File.Copy(ScenarioPath("zones/vallons_chef"), Path.Combine(scenarios, "zones", "vallons_chef.json"));
        }
        string savePath = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        SaveStore.Write(savePath, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(),
            PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, Shipped).WithXp(519)));
        AdventureSession a = AdventureSession.Load(DataDirectory, scenarios, savePath);
        a.DrainMessages();
        a.Advance(0.05);
        return a;
    }

    [Fact]
    public void Les_talents_se_placent_dans_l_aventure_et_prennent_effet_a_la_halte()
    {
        AdventureSession a = Start();
        Assert.Equal(4, a.World.Level);
        Assert.True(a.CanChangeTalents);
        TalentEditor editor = a.CreateTalentEditor();
        Assert.Equal(4, editor.Level);
        Assert.True(editor.PointsAvailable > 0);
        Assert.Equal(0, editor.PointsSpent);
        Assert.Null(editor.Add(GardeStable));
        Assert.Null(a.SetTalents(editor.Build));
        Assert.Contains("Talents gardés : effet à ta prochaine halte.", a.DrainMessages());
        Assert.Equal(1, a.World.Character.Talents[GardeStable]);
        // Le combat en cours garde le build d'entrée ; la halte (au point sûr d'entrée) le prend.
        Assert.Empty(a.World.Zone.Engine.View(AdventureSession.PlayerId).Character!.Build.TalentRanks);
        Assert.True(a.Rest());
        Assert.Equal(1, a.World.Zone.Engine.View(AdventureSession.PlayerId).Character!.Build.TalentRanks[GardeStable]);
        // Sauvegardés : une partie relue les garde.
        AdventureSession again = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), a.SavePath);
        Assert.Equal(1, again.World.Character.Talents[GardeStable]);
        Assert.Equal(1, again.CreateTalentEditor().Rank(GardeStable));
    }

    [Fact]
    public void Un_build_refuse_ne_change_rien()
    {
        AdventureSession a = Start();
        SavedCharacter before = a.World.Character;
        // Rang au-delà du maximum, talent inconnu, plus de points que le niveau n'en donne : le validateur refuse, rien ne change.
        Assert.NotNull(a.World.SetTalents(new Dictionary<string, int> { [GardeStable] = 99 }));
        Assert.NotNull(a.World.SetTalents(new Dictionary<string, int> { ["talent.bastion.inconnu"] = 1 }));
        TalentEditor editor = a.CreateTalentEditor();
        var all = new Dictionary<string, int>();
        foreach (TalentTreeDefinition tree in editor.Trees)
            foreach (TalentDefinition talent in tree.Talents)
                all[talent.Id] = talent.MaxRank;
        BuildViolation? why = a.World.SetTalents(all);
        Assert.NotNull(why);
        Assert.Same(before, a.World.Character);
        Assert.Throws<ArgumentNullException>(() => a.World.SetTalents(null!));
        Assert.Throws<ArgumentNullException>(() => a.SetTalents(null!));
    }

    [Fact]
    public void Pas_en_plein_combat()
    {
        AdventureSession a = Start(wolfNearEntry: true);
        Assert.False(a.CanChangeTalents);
        TalentEditor editor = a.CreateTalentEditor();
        Assert.Null(editor.Add(GardeStable));
        Assert.Equal("Pas en plein combat : éloigne-toi des ennemis pour changer tes talents.", a.SetTalents(editor.Build));
        Assert.Empty(a.World.Character.Talents);
    }
}
