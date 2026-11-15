using System.Text.Json.Nodes;
using EdS.Data.Fishing;
using EdS.Data.Items;
using EdS.Data.Merchants;
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
/// moment de la journée, carnet des prises (nombre, plus grande) dans la sauvegarde (v21).
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

    private static WorldSession Open(FishingBook? book = null, WorldSave? save = null, ItemStack[]? baits = null)
    {
        ZoneDefinition zone = Zone();
        // Décision de Damien du 15 novembre 2026 : un appât par lancer ; le personnage d'essai a vingt vers de terre.
        var hero = PlayerCharacters.Create("bastion", "class.bastion", Progression).WithItems(baits ?? new[] { new ItemStack("item.ver_de_terre", 20) },
            new Dictionary<string, string>());
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
        Assert.Equal(21, SaveValidator.CurrentSchemaVersion);   // pêche et carnet : 21 ;
        Assert.Equal(a.Fishing, saved.Character!.Fishing);
        Assert.Equal(6L, saved.Character.Fishing.Casts);
        foreach (var g in first.GroupBy(c => c.Fish))
            Assert.Equal((g.LongCount(), g.Max(c => c.Cm)), (saved.Character.Fishing.Of(g.Key)!.Count, saved.Character.Fishing.Of(g.Key)!.BestCm));
        // La partie reprise continue la suite des lancers (le septième, pas le premier).
        WorldSession again = Open(save: saved);
        Assert.Equal(Catch(a), Catch(again));
    }

    [Fact]
    public void Sauvegarde_v21_peche_facultative_et_refusee_avant()
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
        string v20 = text.Replace("\"schemaVersion\": 21", "\"schemaVersion\": 20");
        SaveLoadResult old = SaveJsonReader.Read(v20);
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

    // --- Relecture indépendante de la PR #181 et décision de Damien du 15 novembre 2026 (appâts) -------------------------------------

    [Fact]
    public void Appats_un_par_lancer_dans_l_ordre_des_donnees_et_refus_sans_appat()
    {
        FishingBook b = AtVillage.Value;
        Assert.Equal(new[] { "item.ver_de_terre", "item.mouche_de_mai" }, b.Baits);
        // Sans appât : refusé, rien ne change.
        WorldSession none = Open(baits: Array.Empty<ItemStack>());
        Assert.Equal(FishingRefusal.NoBait, none.Cast(Spot));
        Assert.Equal((0L, (FishingSpot?)null, (string?)null), (none.Fishing.Casts, none.CastSpot, none.CastBait));
        // Vers et mouches : les vers d'abord (ordre des données), un par lancer, même ramené ou manqué.
        WorldSession w = Open(baits: new[] { new ItemStack("item.ver_de_terre", 1), new ItemStack("item.mouche_de_mai", 2) });
        Assert.Equal(FishingRefusal.None, w.Cast(Spot));
        Assert.Equal(("item.ver_de_terre", 0, 2), (w.CastBait, w.Inventory.Count("item.ver_de_terre"), w.Inventory.Count("item.mouche_de_mai")));
        Assert.Equal(FishingRefusal.AlreadyCasting, w.Cast(Spot));   // refusé : aucun appât de plus
        Assert.Equal(2, w.Inventory.Count("item.mouche_de_mai"));
        w.CancelCast();
        Assert.Equal(FishingRefusal.None, w.Cast(Spot));
        Assert.Equal(("item.mouche_de_mai", 1), (w.CastBait, w.Inventory.Count("item.mouche_de_mai")));
        w.Strike(out _, out _);   // trop tôt : l'appât est perdu
        Assert.Equal(FishingRefusal.None, w.Cast(Spot));
        Assert.Equal(FishingRefusal.None, w.CancelCast());
        Assert.Equal(FishingRefusal.NoBait, w.Cast(Spot));
        Assert.Equal(3L, w.Fishing.Casts);
        // Les autres refus passent avant celui de l'appât, et ne retirent rien.
        WorldSession far = Open();
        Assert.Equal(FishingRefusal.NotHere, far.Cast("ponton_ouest"));
        Assert.Equal(FishingRefusal.Unknown, far.Cast("inconnu"));
        Assert.Equal(20, far.Inventory.Count("item.ver_de_terre"));
    }

    [Fact]
    public void Appats_en_vente_a_l_epicerie_et_a_ramasser()
    {
        MerchantBook m = ShippedMerchants;
        var epicier = m.Merchants.Single(x => x.Id == "merchant.epicier").Offers;
        Assert.Equal((2L, 3L), (epicier.Single(o => o.Item == "item.ver_de_terre").Price, epicier.Single(o => o.Item == "item.mouche_de_mai").Price));
        EdS.Data.Crafting.CraftingBook crafting = CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory), Texts, Zone(), Items.Value).Book!;
        Assert.Contains(crafting.Professions, p => p.Id == "peche");
        Assert.Contains(crafting.Nodes, n => n.Item == "item.ver_de_terre" && n.Profession == "peche");
        Assert.Contains(crafting.Nodes, n => n.Item == "item.mouche_de_mai" && n.Profession == "peche");
    }

    [Fact]
    public void Relecture_donnees_des_appats_et_des_postes_refusees_avec_leur_chemin()
    {
        string First(Action<JsonObject> m) { FishingLoadResult r = Load(m); Assert.False(r.IsValid); return r.Report.Errors[0].Path; }
        Assert.Equal("$.data.baits", First(root => D(root).Remove("baits")));
        Assert.Equal("$.data.baits[0]", First(root => D(root)["baits"]![0] = "item.licorne"));
        Assert.Equal("$.data.baits[1]", First(root => D(root)["baits"]![1] = "item.ver_de_terre"));
        Assert.Equal("$.data.baits[0]", First(root => D(root)["baits"]![0] = "item.gardon"));   // une espèce n'est pas un appât
        Assert.Equal("$.data.baits[0]", First(root => D(root)["baits"]![0] = "item.potion_de_soin"));   // pas un matériau
        // Un poste sans espèce qui mord toute l'année et à toute heure (ici, seulement le brochet de l'aube et du soir).
        Assert.Equal("$.data.spots[0].fish", First(root => D(root)["spots"]![0]!["fish"] = new JsonArray("item.brochet")));
        FishingLoadResult w = Load(root => D(root)["windowMs"] = 5000);
        Assert.Contains("doit être plus courte", w.Report.Errors[0].Message);
    }

    [Fact]
    public void Espece_et_taille_figees_au_lancer()
    {
        (string, int) Catch(WorldSession w, long? civil)
        {
            Assert.Equal(FishingRefusal.None, w.Cast(Spot));
            if (civil is long utc) w.SetCivilTime(utc);   // la saison change après le lancer : la prise n'en dépend plus
            Wait(w, w.BiteTick!.Value);
            Assert.Equal(FishingRefusal.None, w.Strike(out _, out FishingCatch? c));
            return (c!.Fish, c.Cm);
        }
        WorldSession a = Open(), b = Open();
        long winterNight = new DateTimeOffset(2027, 1, 15, 23, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        for (int i = 0; i < 4; i++) Assert.Equal(Catch(a, null), Catch(b, winterNight + i));
        // Le filtre : saisons et moments de chaque espèce.
        FishKind brochet = AtVillage.Value.Kind("item.brochet"), gardon = AtVillage.Value.Kind("item.gardon");
        Assert.True(brochet.Bites(0, "dawn") && brochet.Bites(3, "dusk") && !brochet.Bites(1, "day") && !brochet.Bites(1, "night"));
        Assert.True(gardon.Bites(2, "night") && gardon.Bites(0, "day"));
    }

    [Fact]
    public void Record_et_plafonds_du_carnet_sans_exception()
    {
        WorldSession w = Open();
        var best = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < 12; i++)
        {
            w.Cast(Spot);
            Wait(w, w.BiteTick!.Value);
            w.Strike(out _, out FishingCatch? c);
            Assert.Equal(!best.TryGetValue(c!.Fish, out int old) || c.Cm > old, c.Record);   // record : plus grand que la plus grande d'avant
            best[c.Fish] = Math.Max(c.Cm, best.GetValueOrDefault(c.Fish));
        }
        Assert.True(best.Count < 12);   // au moins une espèce prise deux fois
        // Carnet plein (64 espèces) et lancers au plafond : le geste réussit, les compteurs saturent.
        var log = Enumerable.Range(0, SavedFishing.MaxLog).Select(i => new SavedCatch($"item.poisson_{i}", SavedFishing.MaxCount, 5)).ToList();
        var full = new SavedFishing(SavedFishing.MaxCasts, log);
        WorldSave save = new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey },
            PlayerCharacters.Create("bastion", "class.bastion", Progression).WithItems(new[] { new ItemStack("item.ver_de_terre", 2) }, new Dictionary<string, string>())
                .WithFishing(full));
        WorldSession capped = Open(save: save);
        capped.Cast(Spot);
        Wait(capped, capped.BiteTick!.Value);
        Assert.Equal(FishingRefusal.None, capped.Strike(out FishingOutcome outcome, out FishingCatch? fish));
        Assert.Equal(FishingOutcome.Caught, outcome);
        Assert.Equal(1, capped.Inventory.Count(fish!.Fish));
        Assert.Equal(full, capped.Fishing);
    }

    [Fact]
    public void Relecture_sauvegarde_v22_refusee()
    {
        string text = SaveJsonReader.Write(Open().Save());
        Assert.False(SaveJsonReader.Read(text.Replace("\"schemaVersion\": 21", "\"schemaVersion\": 22")).IsValid);
    }
}
