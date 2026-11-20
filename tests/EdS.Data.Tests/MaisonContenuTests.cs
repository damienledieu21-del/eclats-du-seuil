using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Crafting;
using EdS.Data.Feasts;
using EdS.Data.Homes;
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
/// Maison du joueur, sous-lot V2.53.2 (contenu ; décisions de Damien du 20 novembre 2026) : la maison du Bourg (zone intérieure, porte sur la
/// maison_ville_86), ses paliers, ses meubles (dix objets fabriqués par le bûcheron et le tailleur de pierre, les 60 décorations des fêtes),
/// le maître d'œuvre qui la vend, la porte fermée tant qu'elle n'est pas achetée, le point sûr à soi, le lit et l'atelier de la maison.
/// </summary>
[Trait("Category", "World")]
public class MaisonContenuTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_maison_" + Guid.NewGuid().ToString("N"));
    private string SavePath => Path.Combine(_folder, "save.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static string Scenarios => Path.Combine(AppContext.BaseDirectory, "scenarios");

    private static readonly Lazy<WorldZonesLoadResult> Linked = new(() => ZoneJsonReader.ReadLinked(DataDirectory, "zone.vallons", Scenarios, ShippedData, ItemsCatalog));

    private static readonly Lazy<FeastBook> Feasts = new(() => FeastJsonReader.ReadFile(FeastJsonReader.PathIn(DataDirectory), Book(Zone()), Texts).Book!);

    private static readonly Lazy<HomeBook> Shipped = new(() =>
    {
        HomeLoadResult r = HomeJsonReader.ReadFile(HomeJsonReader.PathIn(DataDirectory), Linked.Value.Zones, ItemsCatalog, Feasts.Value);
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Book!;
    });

    [Fact]
    public void La_maison_livree()
    {
        HomeBook b = Shipped.Value;
        Assert.Equal(("zone.maison_joueur", "porte_maison_joueur", "zone.vallons", 8_000L), (b.Interior, b.Door, b.DoorZone, b.Price));
        Assert.Equal(new[] { "grande_salle", "seconde_piece", "grande_maison" }, b.Stages.Select(s => s.Id));
        Assert.Equal((18, 16, 8, 10), (b.Base.Width, b.Base.Depth, b.Base.Chest, b.Base.Pieces));   // 4,5 m × 4 m
        Assert.Equal((40, 32, 32, 32), (b.Tier(3).Width, b.Tier(3).Depth, b.Tier(3).Chest, b.Tier(3).Pieces));   // 10 m × 8 m
        Assert.Equal(70, b.Furniture.Count);
        Assert.Equal(60, b.Furniture.Count(f => f.FeastCode != null));   // toutes les décorations des fêtes
        Assert.Equal(new[] { HomeUse.Chest, HomeUse.Bed, HomeUse.Kitchen, HomeUse.Workbench },
            b.Furniture.Select(f => f.Use).Where(u => u != HomeUse.Decor).OrderBy(u => u));
        // La zone intérieure tient la maison agrandie au plus ; sa porte et la porte du Bourg se répondent.
        ZoneDefinition home = Linked.Value.Zones.Single(z => z.Id == b.Interior);
        Place floor = home.Places.Single();
        Assert.Equal((b.Tier(3).Width, b.Tier(3).Depth), ((int)((floor.Max.X - floor.Min.X).Raw / HomeValidator.Cell.Raw), (int)((floor.Max.Z - floor.Min.Z).Raw / HomeValidator.Cell.Raw)));
        Assert.Equal("porte_maison_joueur", home.DoorOf("sortie")!.ToDoor);
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "home.schema.json"))
            .IsValid(JsonDocument.Parse(File.ReadAllText(HomeJsonReader.PathIn(DataDirectory))).RootElement));
        // Chaque meuble-objet a sa recette, chez le bûcheron ou le tailleur de pierre.
        CraftingBook crafting = Crafting(Zone());
        foreach (HomePiece p in b.Furniture.Where(f => f.Item != null))
            Assert.Contains(crafting.Recipes, r => r.Output.Item == p.Item && (r.Profession == "bucheronnage" || r.Profession == "taille_de_pierre"));
    }

    private static CraftingBook Crafting(ZoneDefinition zone) => CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory), Texts, zone, ItemsCatalog).Book!;

    private static WorldSession Inside(SavedCharacter hero)
    {
        ZoneDefinition home = Linked.Value.Zones.Single(z => z.Id == "zone.maison_joueur");
        var w = new WorldSession(new WorldSave("zone.maison_joueur", 0, 0, new[] { PlayerCharacters.PlacesKey }, hero), home, ShippedData, Progression, ItemsCatalog,
            Book(Zone()), Crafting(Zone()));
        w.Step();
        w.UseHome(Shipped.Value);
        return w;
    }

    [Fact]
    public void Point_sur_lit_et_atelier_de_la_maison()
    {
        // Pas encore achetée : le point sûr de la maison est fermé (ni repos, ni retour).
        WorldSession stranger = Inside(Hero());
        Assert.True(stranger.IsSecret("chez_soi"));
        Assert.Equal(HomeRefusal.NotOwned, stranger.SleepInBed());

        var placed = new[] { new SavedPiece("meuble_lit", 0, 4, 0), new SavedPiece("meuble_etabli", 10, 4, 0) };
        SavedCharacter owner = Hero(0, new ItemStack("item.bois", 12)).WithProfessions(new Dictionary<string, long> { ["bucheronnage"] = 1 })
            .WithHome(new SavedHome(0, placed: placed));
        WorldSession w = Inside(owner);
        Assert.False(w.IsSecret("chez_soi"));
        Assert.Null(w.ComfortUntilMinute);
        Assert.Equal(HomeRefusal.None, w.SleepInBed());
        Assert.Equal(HomeRules.BedComfortMinutes, w.ComfortUntilMinute!.Value - (w.Zone.Time.Day * 1440 + w.Zone.Time.Minute));
        Assert.True(w.ComfortWanted);

        // L'établi : les planches se scient à la maison, plus lentement qu'en ville (30 s de jeu entre deux fabrications).
        Assert.Equal(CraftRefusal.None, w.Craft("recipe.planche", out _));
        Assert.Equal(2, w.Inventory.Count("item.planche"));
        Assert.Equal(CraftRefusal.HomeTooSoon, w.CanCraft("recipe.planche"));
        Assert.Equal(CraftRefusal.HomeTooSoon, w.Craft("recipe.planche", out _));
        Assert.Equal(9, w.Inventory.Count("item.bois"));   // refus : rien ne change
        for (long t = 0; t < HomeRules.WorkshopDelayTicks; t++) w.Step();
        Assert.Equal(CraftRefusal.None, w.Craft("recipe.planche", out _));
        Assert.Equal(CraftRefusal.NotHere, w.CanCraft("recipe.cuisine.pain_de_campagne"));   // pas de foyer posé : pas de cuisine

        // Sans établi, pas d'atelier.
        WorldSession bare = Inside(owner.WithHome(new SavedHome(0, placed: placed.Take(1))));
        Assert.Equal(CraftRefusal.NotHere, bare.CanCraft("recipe.planche"));
    }

    // --- Aventure ----------------------------------------------------------------------------------------------------------

    /// <summary>Données copiées dans le dossier du test, le point d'entrée des Vallons posé devant la maison du Bourg.</summary>
    private string Data()
    {
        string data = Path.Combine(_folder, "data", "v0");
        foreach (string file in Directory.GetFiles(DataDirectory, "*", SearchOption.AllDirectories))
        {
            string to = Path.Combine(data, Path.GetRelativePath(DataDirectory, file));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to);
        }
        string path = ZoneJsonReader.PathIn(data, "zone.vallons");
        JsonObject root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(-1061.0, 7.0);
        File.WriteAllText(path, root.ToJsonString());
        return data;
    }

    private AdventureSession Load(string data, SavedCharacter hero)
    {
        SaveStore.Write(SavePath, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero));
        AdventureSession a = AdventureSession.Load(data, Scenarios, SavePath);
        a.Advance(0.05);
        a.DrainMessages();
        return a;
    }

    private static SavedCharacter Player(long money) =>
        PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, Progression).WithMoney(money);

    [Fact]
    public void Porte_fermee_achat_chez_le_maitre_d_oeuvre_puis_retour_a_la_maison()
    {
        string data = Data();
        AdventureSession a = Load(data, Player(10_000));
        Assert.Equal("porte_maison_joueur", a.DoorInReach!.Id);
        Assert.StartsWith("Porte fermée (", a.DoorHint, StringComparison.Ordinal);
        Assert.False(a.UseDoor());
        Assert.Contains("Cette maison est à vendre : voyez Maître Bertin, le maître d'œuvre, devant la porte.", a.DrainMessages());

        Assert.True(a.TalkToHomeVendor());
        int buy = a.DialogueAnswers.ToList().FindIndex(s => s.StartsWith("[Maison] Acheter", StringComparison.Ordinal));
        Assert.True(buy >= 0, string.Join(" | ", a.DialogueAnswers));
        Assert.Equal(EdS.World.DialogueRefusal.None, a.Choose(buy));
        Assert.Contains("La maison est à vous. La clé est sur la porte.", a.DrainMessages());
        Assert.Equal(2_000, a.World.Character.Money);
        Assert.Equal(3, a.HomeAnswers.Count);   // or, planches, pierres du premier agrandissement
        int gold = a.DialogueAnswers.ToList().FindIndex(s => s.StartsWith("[Maison] Agrandir", StringComparison.Ordinal));
        Assert.Equal(EdS.World.DialogueRefusal.None, a.Choose(gold));
        Assert.Equal((0L, 2_000L), (a.World.Character.Money, a.Home!.Of("or")));
        a.CloseDialogue();

        Assert.True(a.UseDoor());
        Assert.Equal("zone.maison_joueur", a.World.Definition!.Id);
        Assert.True(a.AtHome);
        Assert.False(a.CanGoHome);

        // Relancé : la maison est gardée ; depuis le point sûr du Bourg, on rentre chez soi.
        AdventureSession b = Load(data, Player(0).WithHome(SavedHome.Bought));
        Assert.StartsWith("F : passer la porte (", b.DoorHint, StringComparison.Ordinal);
        Assert.True(b.CanGoHome);
        Assert.True(b.GoHome());
        Assert.Equal("zone.maison_joueur", b.World.Definition!.Id);
        Assert.Equal("zone.maison_joueur", SaveStore.Read(SavePath).Save!.Zone);
    }

    [Fact]
    public void Attente_de_l_atelier_gardee_en_sortant_puis_en_rentrant()
    {
        // Relecture indépendante de la PR #230 : sortir puis rentrer (ou rentrer chez soi) ne remet pas l'attente de l'atelier à zéro.
        string data = Data();
        var placed = new[] { new SavedPiece("meuble_etabli", 10, 4, 0) };
        SavedCharacter hero = Player(0).WithItems(new[] { new ItemStack("item.bois", 12) }, new Dictionary<string, string>())
            .WithHome(new SavedHome(0, placed: placed));
        AdventureSession a = Load(data, hero);
        a.Advance(0.05);
        Assert.True(a.UseDoor());
        Assert.Equal(CraftRefusal.None, a.World.Craft("recipe.planche", out _));
        Assert.Equal(CraftRefusal.HomeTooSoon, a.World.CanCraft("recipe.planche"));
        a.Advance(0.05);
        Assert.True(a.UseDoor());   // sortie
        Assert.Equal("zone.vallons", a.World.Definition!.Id);
        a.Advance(0.05);
        Assert.True(a.UseDoor());   // rentrée
        Assert.Equal("zone.maison_joueur", a.World.Definition!.Id);
        Assert.Equal(CraftRefusal.HomeTooSoon, a.World.Craft("recipe.planche", out _));
        Assert.Equal(9, a.World.Inventory.Count("item.bois"));   // refus : rien ne change
        a.Advance(0.05);
        Assert.True(a.UseDoor());
        a.Advance(0.05);
        Assert.True(a.GoHome());   // le retour à la maison non plus
        Assert.Equal(CraftRefusal.HomeTooSoon, a.World.CanCraft("recipe.planche"));
    }
}
