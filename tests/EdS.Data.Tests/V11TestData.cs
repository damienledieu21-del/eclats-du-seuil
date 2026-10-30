using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Merchants;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>Sous-lot V1.1 : données livrées du monde et parties prêtes à jouer pour les tests de la monnaie, des marchands et du voyage.</summary>
internal static class V11TestData
{
    public static string ScenariosDirectory => Path.Combine(AppContext.BaseDirectory, "scenarios");
    public static ItemCatalog ItemsCatalog { get; } = ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;
    public static CharacterProgression Progression { get; } = ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;
    public static TextCatalog Texts { get; } = StoryJsonReader.ReadTextsFile(StoryJsonReader.TextsPathIn(DataDirectory)).Texts!;
    public static string MerchantsPath => MerchantJsonReader.PathIn(DataDirectory);

    /// <summary>Les Vallons livrés, point sûr d'entrée déplacé en (<paramref name="x"/>, <paramref name="z"/>), scénario retouché au besoin.</summary>
    public static ZoneDefinition Zone(double x = -18, double z = -12, Action<JsonObject>? scenario = null)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(x, z);
        string directory = ScenariosDirectory;
        string? temporary = null;
        if (scenario != null)
        {
            temporary = directory = Path.Combine(Path.GetTempPath(), "eds_v11_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(directory, "zones"));
            JsonObject s = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
            scenario(s);
            File.WriteAllText(Path.Combine(directory, "zones", "vallons.json"), s.ToJsonString());
            File.Copy(ScenarioPath("zones/vallons_chef"), Path.Combine(directory, "zones", "vallons_chef.json"));
        }
        try
        {
            ZoneLoadResult loaded = ZoneJsonReader.Read(root.ToJsonString(), directory, ShippedData, ItemsCatalog);
            Assert.True(loaded.IsValid, loaded.Report.ToString());
            return loaded.Zone!;
        }
        finally
        {
            if (temporary != null) Directory.Delete(temporary, true);
        }
    }

    public static StoryBook Book(ZoneDefinition zone) => StoryJsonReader.ReadStoryFile(StoryJsonReader.StoryPathIn(DataDirectory), Texts, zone, ItemsCatalog).Story!;

    public static JsonObject MerchantsJson() => JsonNode.Parse(File.ReadAllText(MerchantsPath))!.AsObject();

    public static MerchantsLoadResult ReadMerchants(JsonObject root, ZoneDefinition? zone = null) =>
        MerchantJsonReader.Read(root.ToJsonString(), Texts, Book(zone ?? Zone()), ItemsCatalog);

    public static MerchantBook ShippedMerchants { get; } = MerchantJsonReader.ReadFile(MerchantsPath, Texts, Book(Zone()), ItemsCatalog).Book!;

    public static SavedCharacter Hero(long money = 0, params ItemStack[] items) =>
        PlayerCharacters.Create("bastion", "class.bastion", Progression).WithItems(items, new Dictionary<string, string>()).WithMoney(money);

    /// <summary>Partie ouverte sur la zone avec un commerce (une instance donnée, ou neuve), un tick simulé (le personnage est arrivé).</summary>
    public static WorldSession Play(ZoneDefinition zone, SavedCharacter hero, Market? market = null, IEnumerable<string>? consumed = null)
    {
        var session = new WorldSession(new WorldSave("zone.vallons", 0, 0, consumed ?? Array.Empty<string>(), hero), zone, ShippedData, Progression, ItemsCatalog,
            Book(zone), market: market ?? new Market(ShippedMerchants));
        session.Step();
        return session;
    }

    /// <summary>Position du personnage (zone de la partie).</summary>
    public static GroundVector Position(WorldSession w) => w.Zone.Engine.View("bastion").Position!.Value;

    /// <summary>Place où se tenir pour commercer avec chaque marchand livré (à 1,6 m de son PNJ, hors de tout obstacle).</summary>
    public static (double X, double Z) Near(string merchant) => merchant switch
    {
        "merchant.herboriste" => (-65.2, -6.4),
        "merchant.armurier" => (-54.5, -11),
        "merchant.epicier" => (-50.8, 4.4),
        _ => throw new ArgumentException(merchant),
    };

    public static WorldSession At(string merchant, SavedCharacter hero, Market? market = null)
    {
        (double x, double z) = Near(merchant);
        return Play(Zone(x, z), hero, market);
    }

    /// <summary>État observable d'une partie pour vérifier qu'un refus n'a rien changé : bourse, inventaire, équipement, rachats.</summary>
    public static string Snapshot(WorldSession w) =>
        $"{w.Money}|{string.Join(",", w.Inventory.Stacks.Select(s => s.ToString()))}|{string.Join(",", w.Inventory.Equipped.Select(e => e.Key + "=" + e.Value))}|"
        + string.Join(",", w.Market?.Buyback.Select(l => $"{l.Item}x{l.Count}:{l.Paid}") ?? Array.Empty<string>());

    public static IReadOnlyList<ValidationError> Errors(MerchantsLoadResult r) => r.Report.Errors;

    /// <summary>Aventure chargée depuis <paramref name="data"/> (données livrées par défaut), premier tick joué.</summary>
    public static AdventureSession Adventure(string savePath, string? data = null, string? scenarios = null)
    {
        AdventureSession a = AdventureSession.Load(data ?? DataDirectory, scenarios ?? ScenariosDirectory, savePath);
        a.Advance(0.05);
        return a;
    }
}
