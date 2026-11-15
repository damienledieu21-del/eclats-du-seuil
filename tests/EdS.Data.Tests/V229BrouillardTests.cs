using EdS.Data.Characters;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using System.Linq;
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
        // Une seule fois, même après une sauvegarde, un rechargement et un retour au sommet.
        long after = a.World.Character.Xp;
        for (int i = 0; i < 40; i++) a.Advance(0.05);
        Assert.Equal(after, a.World.Character.Xp);
        a.Save();
        AdventureSession again = AdventureSession.Load(DataDirectory, Scenarios, SavePath);
        again.Advance(0.05);
        Assert.True(again.World.Narrative!.HasDiscovered("discovery.point_de_vue_du_sommet"));
        long before = again.World.Character.Xp;
        Assert.True(again.Combat.CallMount());
        for (int i = 0; i < 22; i++) again.Advance(0.05);
        Assert.True(Ride(again, (500.0, 1290.0), (190.0, 1225.0), (180.0, 1360.0)), "sommet non atteint au retour");
        for (int i = 0; i < 40; i++) again.Advance(0.05);
        Assert.Equal(before, again.World.Character.Xp);   // la découverte, sauvegardée, marque la visite : pas de seconde récompense
    }

    // --- Relecture indépendante de la PR #185 -----------------------------------------------------------------------------

    [Fact]
    public void Chaines_mal_formees_ignorees_ou_refusees()
    {
        var grid = RegionGrid.Vallons();
        Assert.Equal(192, ExploredCells.HexLength(grid));
        string ok = new string('0', 190) + "01";
        Assert.Equal(1, new ExploredCells(grid, ok).Count);
        foreach (string bad in new[] { "0", "abc", new string('0', 191), new string('0', 190) + "0G", new string('0', 190) + "0F", new string('0', 194), "ff" })
        {
            var cells = new ExploredCells(grid, bad);
            Assert.True(cells.Discarded, bad);
            Assert.Equal(0, cells.Count);
        }
        Assert.False(new ExploredCells(grid, null).Discarded);
        // Le personnage refuse des cases hors format (l'écrivain les recopierait telles quelles).
        Assert.Throws<ArgumentException>(() => Hero().WithExplored(new Dictionary<string, string> { ["zone.vallons"] = "0\"" }));
        Assert.Throws<ArgumentException>(() => Hero().WithExplored(new Dictionary<string, string> { ["Vallons"] = "00" }));
        Assert.Throws<ArgumentException>(() => Hero().WithExplored(new Dictionary<string, string> { ["zone." + new string('a', 60)] = "00" }));
        Assert.Throws<ArgumentException>(() => Hero().WithExplored(Enumerable.Range(0, 17).ToDictionary(k => "zone.z" + k, _ => "00")));
    }

    [Fact]
    public void Sauvegarde_refuse_trop_de_zones_trop_long_et_null()
    {
        string text = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero().WithExplored(new Dictionary<string, string> { ["zone.vallons"] = "01" })));
        string zones = "\"explored\": {" + string.Join(", ", Enumerable.Range(0, 17).Select(k => "\"zone.z" + k + "\": \"01\"")) + "}";
        Assert.False(SaveJsonReader.Read(text.Replace("\"explored\": {\"zone.vallons\": \"01\"}", zones)).IsValid);
        Assert.Contains(SaveJsonReader.Read(text.Replace("\"01\"", "\"" + new string('0', 1026) + "\"")).Report.Errors, e => e.Path == "$.data.character.explored.zone.vallons");
        Assert.False(SaveJsonReader.Read(text.Replace("\"01\"", "null")).IsValid);
        Assert.False(SaveJsonReader.Read(text.Replace("\"explored\": {\"zone.vallons\": \"01\"}", "\"explored\": null")).IsValid);
        Assert.False(SaveJsonReader.Read(text.Replace("\"zone.vallons\": \"01\"", "\"zone." + new string('a', 60) + "\": \"01\"")).IsValid);
    }

    [Fact]
    public void Migration_d_un_vrai_fichier_v22()
    {
        AdventureSession a = Start(new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, Hero()));
        a.Save();
        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(SavePath))!.AsObject();
        root["schemaVersion"] = 22;
        root["data"]!["character"]!.AsObject().Remove("explored");
        File.WriteAllText(SavePath, root.ToJsonString());
        SaveReadResult read = SaveStore.Read(SavePath);
        Assert.False(read.Unreadable, read.Problem);
        Assert.True(read.Save!.Migrated);
        AdventureSession again = AdventureSession.Load(DataDirectory, Scenarios, SavePath);
        again.Advance(0.05);
        Assert.True(again.World.Explored!.Count > 0);
        again.Save();
        Assert.Contains(SaveVersion.Current, File.ReadAllText(SavePath));
        Assert.Contains("\"explored\": {\"zone.vallons\"", File.ReadAllText(SavePath));
    }

    [Fact]
    public void Interieur_sans_brouillard_et_repere_toujours_visible()
    {
        AdventureSession inside = Start(new WorldSave("zone.auberge", 0, 0, new[] { PlayerCharacters.PlacesKey }, Hero()));
        Assert.Null(inside.World.Explored);
        Assert.True(MapFog.Visible(null, 0, 0));
        var cells = new ExploredCells(RegionGrid.Vallons());
        cells.Reveal(At(-18, -12), 1);
        Assert.True(MapFog.Visible(cells, -18, -12));
        Assert.False(MapFog.Visible(cells, 1000, 1000));   // sous le brouillard : caché (décision du 15 novembre 2026, 21 h 15)
        Assert.True(MapFog.Visible(cells, 9000, 0));       // hors de la grille
    }

    [Fact]
    public void Points_de_vue_coherents_avec_les_donnees()
    {
        AdventureSession a = Start(new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, Hero()));
        foreach (Viewpoint v in Viewpoint.All)
        {
            Assert.True(a.World.Narrative!.HasDiscovery(v.Discovery), v.Discovery);
            Assert.True(RegionGrid.Vallons().Contains(v.Position));
            Assert.Equal("zone.vallons", v.Zone);
        }
        Assert.False(a.World.Narrative!.Discover("discovery.inconnue", new List<StoryEvent>()));   // jamais d'exception pendant un tick
    }

    /// <summary>Va de point en point (à moins de 3 m de chacun) ; rend vrai si le dernier est atteint.</summary>
    private static bool Ride(AdventureSession a, params (double X, double Z)[] points)
    {
        foreach ((double x, double z) in points)
        {
            bool reached = false;
            for (int i = 0; i < 3000 && !reached; i++)
            {
                GroundVector p = a.World.Zone.Engine.View(a.World.CombatantId).Position!.Value;
                double dx = x - p.X.Raw / 1e4, dz = z - p.Z.Raw / 1e4;
                if (Math.Sqrt(dx * dx + dz * dz) < 3) reached = true;
                else
                {
                    a.Combat.SetMove(dx, dz);
                    a.Advance(0.05);
                }
            }
            if (!reached) return false;
        }
        a.Combat.SetMove(0, 0);
        return true;
    }
}
