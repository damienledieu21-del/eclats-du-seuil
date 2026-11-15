using EdS.Data.Characters;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.29 (2) : brouillard de carte à dissiper (sujet 30) et point de vue du sommet (décision de Damien du 15 novembre 2026 : à la
/// première visite, 200 XP, une entrée au carnet et le brouillard dissipé autour) ; sauvegarde en version 23 (<c>character.explored</c>).
/// </summary>
[Trait("Category", "World")]
public class V229BrouillardTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v229_brouillard_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private string SavePath => Path.Combine(_folder, "partie", "sauvegarde.json");
    private static string Scenarios => Path.Combine(AppContext.BaseDirectory, "scenarios");

    private static GroundVector At(int x, int z) => new GroundVector(Fixed.FromInt(x), Fixed.FromInt(z));

    private static SavedCharacter Hero()
    {
        CharacterProgression progression = ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;
        ItemCatalog items = ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;
        return Inventory.Outfit(PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, progression), items);
    }

    private AdventureSession Start(WorldSave save)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        SaveStore.Write(SavePath, save);
        AdventureSession a = AdventureSession.Load(DataDirectory, Scenarios, SavePath);
        a.Advance(0.05);
        return a;
    }

    // --- Cases vues -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Cases_vues_autour_d_un_point_et_aller_retour_hexadecimal()
    {
        var cells = new ExploredCells(RegionGrid.Vallons());
        Assert.Equal(0, cells.Count);
        Assert.Null(cells.ToHex());
        // Place du village (−18, −12), dans la case c14_r12 ([−116, −78] à [12, 50]) : à 128 m, les cases dont le rectangle touche le disque.
        int added = cells.Reveal(At(-18, -12), ExploredCells.SightMeters);
        Assert.Equal(cells.Count, added);
        Assert.True(cells.IsSeen(14, 12) && cells.IsSeen(13, 12) && cells.IsSeen(15, 12) && cells.IsSeen(14, 11) && cells.IsSeen(14, 13));
        Assert.False(cells.IsSeen(16, 12));   // à plus de 128 m (bord ouest à x = 140)
        Assert.False(cells.IsSeen(14, 14));   // bord sud à z = 178 : 190 m
        Assert.Equal(0, cells.Reveal(At(-18, -12), ExploredCells.SightMeters));   // rien de neuf
        Assert.Equal(0, cells.Reveal(At(5000, 0), 128));   // hors de la région
        string hex = cells.ToHex()!;
        Assert.Equal(32 * 24 / 8 * 2, hex.Length);
        Assert.Matches("^[0-9a-f]+$", hex);
        var back = new ExploredCells(RegionGrid.Vallons(), hex);
        Assert.Equal(cells.Count, back.Count);
        Assert.Equal(hex, back.ToHex());
        Assert.False(back.IsSeen(-1, 0));
        Assert.False(back.IsSeen(32, 0));
    }

    [Fact]
    public void Point_de_vue_du_sommet_dissipe_huit_cases_autour()
    {
        Viewpoint top = Assert.Single(Viewpoint.All);
        Assert.Equal(("zone.vallons", 200L, 1024), (top.Zone, top.Xp, top.RevealMeters));
        var cells = new ExploredCells(RegionGrid.Vallons());
        int added = cells.Reveal(top.Position, top.RevealMeters);
        Assert.InRange(added, 120, 260);
        RegionCell summit = RegionGrid.Vallons().CellOf(top.Position);
        Assert.True(cells.IsSeen(summit.Column, summit.Row));
        Assert.True(cells.IsSeen(summit.Column - 7, summit.Row));
        Assert.False(cells.IsSeen(summit.Column, 0));   // le sud de la région reste dans le brouillard
    }

    // --- Sauvegarde v23 -----------------------------------------------------------------------------------------------

    [Fact]
    public void Version_23_cases_explorees_ecrites_relues_et_controlees()
    {
        Assert.Equal((23, 23, 22), (SaveValidator.CurrentSchemaVersion, SaveValidator.ExploredSince, SaveValidator.MountSince));
        var cells = new ExploredCells(RegionGrid.Vallons());
        cells.Reveal(At(-18, -12), 128);
        var map = new Dictionary<string, string> { ["zone.vallons"] = cells.ToHex()! };
        string text = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero().WithExplored(map)));
        Assert.Contains("\"explored\": {\"zone.vallons\": \"", text);
        SaveLoadResult read = SaveJsonReader.Read(text);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.Equal(map["zone.vallons"], read.Save!.Character!.Explored["zone.vallons"]);
        Assert.Equal(text, SaveJsonReader.Write(read.Save));
        MiniJsonSchema schema = MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json"));
        Assert.True(schema.IsValid(System.Text.Json.JsonDocument.Parse(text).RootElement));
        // Chaîne mal formée, zone mal formée, champ trop tôt.
        Assert.Contains(SaveJsonReader.Read(text.Replace(map["zone.vallons"], "ABC")).Report.Errors, e => e.Path == "$.data.character.explored.zone.vallons");
        Assert.Contains(SaveJsonReader.Read(text.Replace("\"zone.vallons\": \"" + map["zone.vallons"], "\"Vallons\": \"" + map["zone.vallons"])).Report.Errors,
            e => e.Path.StartsWith("$.data.character.explored", StringComparison.Ordinal));
        Assert.Contains(SaveJsonReader.Read(text.Replace(SaveVersion.Current, "\"schemaVersion\": 22")).Report.Errors,
            e => e.Path == "$.data.character.explored" && e.Message.Contains("version 23"));
        // Rien d'exploré : pas de champ.
        Assert.DoesNotContain("explored", SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero())));
    }

    // --- Dans la partie -----------------------------------------------------------------------------------------------

    [Fact]
    public void La_partie_voit_les_cases_autour_et_les_garde()
    {
        AdventureSession a = Start(new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, Hero()));
        ExploredCells cells = a.World.Explored!;
        Assert.True(cells.Count > 0);
        Assert.True(cells.IsSeen(14, 12));
        string hex = a.World.Save().Character!.Explored["zone.vallons"];
        Assert.Equal(cells.ToHex(), hex);
        // Le brouillard de la carte : transparent sur une case vue, parchemin ailleurs.
        byte[] fog = MapFog.Rgba(cells);
        Assert.Equal(32 * 24 * 4, fog.Length);
        Assert.Equal(0, fog[(14 + 12 * 32) * 4 + 3]);
        Assert.Equal(MapFog.Alpha, fog[3]);
    }

    [Fact]
    public void Sommet_premiere_visite_200_xp_carnet_et_brouillard()
    {
        // Partie reprise au pied du fort (1 170, 874), puis par le chemin des cairns jusqu'au sommet (180, 1 360), au nord de la falaise de la crête.
        // À cheval (fin du chapitre 1), pour aller vite.
        SavedCharacter hero = Hero().WithStory(new SavedStory(quests: new Dictionary<string, QuestProgress>
        {
            [PlayerCharacters.HorseQuest] = new QuestProgress(QuestProgress.Done),
        }));
        WorldSave save = new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey, "zone.vallons/safe/pied_du_fort" }, hero,
            resume: new SavedResume("pied_du_fort", "s12_09"));
        AdventureSession a = Start(save);
        Assert.True(a.Combat.CallMount());
        for (int i = 0; i < 22; i++) a.Advance(0.05);
        Assert.True(a.Combat.Mounted);
        long xp = a.World.Character.Xp;
        int seen = a.World.Explored!.Count;
        var points = new[] { (900.0, 1150.0), (500.0, 1290.0), (190.0, 1225.0), (180.0, 1360.0) };
        bool found = false;
        var trace = new List<string>();
        foreach ((double x, double z) in points)
        {
            trace.Add($"{a.World.Zone.Engine.View(a.World.CombatantId).Position} t{a.World.Zone.Engine.CurrentTick} {a.MustContinue} {a.Combat.Mounted}");
            for (int i = 0; i < 3000 && !found; i++)
            {
                GroundVector p = a.World.Zone.Engine.View(a.World.CombatantId).Position!.Value;
                double dx = x - p.X.Raw / 1e4, dz = z - p.Z.Raw / 1e4;
                if (Math.Sqrt(dx * dx + dz * dz) < 3) break;
                a.Combat.SetMove(dx, dz);
                a.Advance(0.05);
                found = a.World.Narrative!.HasDiscovered("discovery.point_de_vue_du_sommet");
            }
        }
        Assert.True(found, $"sommet non atteint : {a.World.Zone.Engine.View(a.World.CombatantId).Position} PV {a.World.Zone.Engine.View(a.World.CombatantId).Health} {a.World.Explored!.Count} " + string.Join(" | ", trace));
        Assert.True(a.World.Character.Xp >= xp + 200);
        Assert.True(a.World.Explored!.Count >= seen + 100);
        Assert.Contains("discovery.point_de_vue_du_sommet", a.World.Save().Character!.Story.Discoveries);
        // Une seule fois.
        long after = a.World.Character.Xp;
        for (int i = 0; i < 40; i++) a.Advance(0.05);
        Assert.Equal(after, a.World.Character.Xp);
    }
}
