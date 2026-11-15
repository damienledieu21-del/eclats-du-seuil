using System.Text.Json.Nodes;
using EdS.Data.Fishing;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Zones;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Vague 4 du plan des Vallons (« la vie à côté du combat ») : la pêche et son carnet. Données (<c>fishing.json</c>, schéma 1, validateur
/// unique), petit jeu (lancer, touche après un délai tiré au sort, ferrer pendant la fenêtre), espèces selon le poste, la saison et le
/// moment de la journée, carnet des prises (nombre, plus grande) dans la sauvegarde (v20).
/// </summary>
[Trait("Category", "World")]
public class PecheCarnetTests
{
    private static TextCatalog Texts => StoryJsonReader.ReadTextsFile(StoryJsonReader.TextsPathIn(DataDirectory)).Texts!;
    private static readonly Lazy<ItemCatalog> Items = new(() => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!);

    private static FishingLoadResult Load(Action<JsonObject>? mutate = null)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(FishingJsonReader.PathIn(DataDirectory)))!.AsObject();
        mutate?.Invoke(root);
        return FishingJsonReader.Read(root.ToJsonString(), Texts, Zone(), Items.Value);
    }

    private static JsonObject D(JsonObject root) => root["data"]!.AsObject();

    /// <summary>Livre de pêche avec un poste d'essai au point sûr de la place du village (où entre le personnage).</summary>
    private static readonly Lazy<FishingBook> AtVillage = new(() => Load(root =>
    {
        JsonObject spot = D(root)["spots"]![2]!.AsObject();
        spot["position"] = new JsonArray(-18, -12);
    }).Book!);

    private static WorldSession Open(FishingBook? book = null, WorldSave? save = null)
    {
        ZoneDefinition zone = Zone();
        var hero = PlayerCharacters.Create("bastion", "class.bastion", Progression);
        var w = new WorldSession(save ?? new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone, ShippedData, Progression,
            ItemsCatalog, Book(zone));
        w.UseFishing(book ?? AtVillage.Value);
        w.Step();
        return w;
    }

    private static string Spot => AtVillage.Value.Spots[2].Id;

    private static void Wait(WorldSession w, long tick)
    {
        while (w.Zone.WorldTick < tick) w.Step();
    }

    [Fact]
    public void Donnees_livrees_valides()
    {
        FishingLoadResult r = Load();
        Assert.True(r.IsValid, r.Report.ToString());
        FishingBook b = r.Book!;
        Assert.Equal((1, FishingValidator.CurrentSchemaVersion), (b.SchemaVersion, 1));
        Assert.Equal(new[] { "ponton_ouest", "rive_est", "gue_du_moulin" }, b.Spots.Select(s => s.Id));
        Assert.True(b.Fish.Count >= 5);
        Assert.All(b.Fish, f => Assert.True(f.MinCm <= f.MaxCm && f.Weight > 0));
        Assert.True(b.WindowMs < b.BiteMinMs);
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "fishing.schema.json"))
            .IsValid(System.Text.Json.JsonDocument.Parse(File.ReadAllText(FishingJsonReader.PathIn(DataDirectory))).RootElement));
    }

    [Fact]
    public void Donnees_invalides_refusees_avec_leur_chemin()
    {
        string First(Action<JsonObject> m) { FishingLoadResult r = Load(m); Assert.False(r.IsValid); return r.Report.Errors[0].Path; }
        Assert.Equal("$.data.biteMaxMs", First(root => D(root)["biteMaxMs"] = 1000));
        Assert.Equal("$.data.range", First(root => D(root)["range"] = 50));
        Assert.Equal("$.data.fish[0].item", First(root => D(root)["fish"]![0]!["item"] = "item.licorne"));
        Assert.Equal("$.data.fish[0].maxCm", First(root => D(root)["fish"]![0]!["maxCm"] = 1));
        Assert.Equal("$.data.fish[3].phases[0]", First(root => D(root)["fish"]![3]!["phases"]![0] = "midi"));
        Assert.Equal("$.data.spots[0].id", First(root => D(root)["spots"]![0]!["id"] = "Ponton"));
        Assert.Equal("$.schemaVersion", First(root => root["schemaVersion"] = 2));
    }

    [Fact]
    public void Lancer_touche_ferrer_prise_au_sac_et_au_carnet()
    {
        WorldSession w = Open();
        Assert.Equal(Spot, w.FishingSpotNear(w.Zone.Engine.View(w.CombatantId).Position)?.Id);
        Assert.Equal(FishingRefusal.NotCasting, w.Strike(out _, out _));
        Assert.Equal(FishingRefusal.Unknown, w.Cast("inconnu"));
        Assert.Equal(FishingRefusal.NotHere, w.Cast("ponton_ouest"));   // trop loin
        Assert.Equal(FishingRefusal.None, w.Cast(Spot));
        Assert.Equal(FishingRefusal.AlreadyCasting, w.Cast(Spot));
        long bite = w.BiteTick!.Value;
        long wait = bite - w.Zone.WorldTick;
        Assert.InRange(wait * 50, AtVillage.Value.BiteMinMs, AtVillage.Value.BiteMaxMs + 50);
        Assert.False(w.IsBiting);
        Wait(w, bite);
        Assert.True(w.IsBiting);
        Assert.Equal(FishingRefusal.None, w.Strike(out FishingOutcome outcome, out FishingCatch? caught));
        Assert.Equal(FishingOutcome.Caught, outcome);
        Assert.NotNull(caught);
        Assert.True(caught!.Record);
        Assert.Equal(1, w.Inventory.Count(caught.Fish));
        FishKind kind = AtVillage.Value.Kind(caught.Fish);
        Assert.InRange(caught.Cm, kind.MinCm, kind.MaxCm);
        Assert.Contains(caught.Fish, AtVillage.Value.Spots[2].Fish);
        Assert.Equal((1L, 1L, caught.Cm), (w.Fishing.Casts, w.Fishing.Of(caught.Fish)!.Count, w.Fishing.Of(caught.Fish)!.BestCm));
        Assert.Null(w.CastSpot);
    }

    [Fact]
    public void Trop_tot_ou_trop_tard_le_lancer_est_perdu()
    {
        WorldSession w = Open();
        w.Cast(Spot);
        Assert.Equal(FishingRefusal.None, w.Strike(out FishingOutcome early, out FishingCatch? none));
        Assert.Equal((FishingOutcome.TooEarly, (FishingCatch?)null), (early, none));
        Assert.Equal((1L, 0), (w.Fishing.Casts, w.Fishing.Log.Count));
        w.Cast(Spot);
        long end = w.BiteTick!.Value + (AtVillage.Value.WindowMs + 49) / 50 + 1;
        Wait(w, end);
        Assert.False(w.IsBiting);
        Assert.Equal(FishingRefusal.None, w.Strike(out FishingOutcome late, out _));
        Assert.Equal(FishingOutcome.TooLate, late);
        // Ligne ramenée : le lancer compte aussi.
        w.Cast(Spot);
        Assert.Equal(FishingRefusal.None, w.CancelCast());
        Assert.Equal(FishingRefusal.NotCasting, w.CancelCast());
        Assert.Equal((3L, 0), (w.Fishing.Casts, w.Fishing.Log.Count));
    }

    [Fact]
    public void Meme_lancer_meme_prise_et_carnet_relu_de_la_sauvegarde()
    {
        (string Fish, int Cm) Catch(WorldSession w)
        {
            w.Cast(Spot);
            Wait(w, w.BiteTick!.Value);
            w.Strike(out _, out FishingCatch? c);
            return (c!.Fish, c.Cm);
        }
        WorldSession a = Open(), b = Open();
        var first = Enumerable.Range(0, 6).Select(_ => Catch(a)).ToList();
        Assert.Equal(first, Enumerable.Range(0, 6).Select(_ => Catch(b)).ToList());   // déterministe (personnage, numéro du lancer)
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(a.Save())).Save!;
        Assert.Equal(20, SaveValidator.CurrentSchemaVersion);
        Assert.Equal(a.Fishing, saved.Character!.Fishing);
        Assert.Equal(6L, saved.Character.Fishing.Casts);
        foreach (var g in first.GroupBy(c => c.Fish))
            Assert.Equal((g.LongCount(), g.Max(c => c.Cm)), (saved.Character.Fishing.Of(g.Key)!.Count, saved.Character.Fishing.Of(g.Key)!.BestCm));
        // La partie reprise continue la suite des lancers (le septième, pas le premier).
        WorldSession again = Open(save: saved);
        Assert.Equal(Catch(a), Catch(again));
    }

    [Fact]
    public void Sauvegarde_v20_pecheur_facultatif_et_refuse_avant()
    {
        WorldSession w = Open();
        string text = SaveJsonReader.Write(w.Save());
        Assert.DoesNotContain("\"fishing\"", text);   // absente tant que le personnage n'a pas pêché
        w.Cast(Spot);
        w.CancelCast();
        text = SaveJsonReader.Write(w.Save());
        Assert.Contains("\"fishing\": {\"casts\": 1, \"log\": []}", text);
        Assert.True(SaveJsonReader.Read(text).IsValid);
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json")).IsValid(System.Text.Json.JsonDocument.Parse(text).RootElement));
        string v19 = text.Replace("\"schemaVersion\": 20", "\"schemaVersion\": 19");
        SaveLoadResult old = SaveJsonReader.Read(v19);
        Assert.False(old.IsValid);
        Assert.EndsWith(".fishing", old.Report.Errors[0].Path);
    }

    [Fact]
    public void Sac_plein_refuse_sans_rien_changer()
    {
        WorldSession w = Open();
        w.Cast(Spot);
        Wait(w, w.BiteTick!.Value);
        // Remplir le sac avec des objets qui ne s'empilent pas avec un poisson.
        int added = 0;
        foreach (ItemDefinition item in ItemsCatalog.Items.Where(i => i.Type == ItemType.Material && !AtVillage.Value.Fish.Any(f => f.Item == i.Id)))
            while (w.Inventory.Add(item.Id, 1) == InventoryRefusal.None && added++ < 10_000) { }
        long casts = w.Fishing.Casts;
        FishingRefusal r = w.Strike(out _, out FishingCatch? c);
        if (r == FishingRefusal.Full)
        {
            Assert.Null(c);
            Assert.Equal(casts, w.Fishing.Casts);
            Assert.NotNull(w.CastSpot);   // la ligne est toujours à l'eau : on peut faire de la place et ferrer pendant la fenêtre
        }
        else Assert.Equal(FishingRefusal.None, r);   // le poisson s'empile avec un objet déjà là
    }
}
