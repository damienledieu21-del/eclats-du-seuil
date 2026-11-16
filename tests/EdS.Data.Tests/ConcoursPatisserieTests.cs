using System.Text.Json.Nodes;
using EdS.Data.Crafting;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Cuisine (2), concours de pâtisserie aux fêtes (décision de Damien du 15 novembre 2026, CADRAGE § 7 nonies) : le joueur présente SA
/// couronne des moissons ou SA tarte renversée à Firmin ; la qualité du plat (normale, supérieure, chef-d'œuvre) décide du prix ; un concours
/// par fête et par an. Formats : catalogue des objets v8 (<c>craftQuality</c>), sauvegarde v24 (marque du pâtissier), histoire en schéma 15
/// (<c>hasSigned</c>, <c>present</c>).
/// </summary>
[Trait("Category", "World")]
public class ConcoursPatisserieTests
{
    private const string Couronne = "item.couronne_des_moissons", Tarte = "item.tarte_renversee_aux_pommes";
    private const string Moissons = "festival.moissons";
    private const string Me = "bastion";

    private static string ScenariosDirectory => Path.Combine(AppContext.BaseDirectory, "scenarios");

    private static ItemStack Signed(string item, ItemQuality quality, string signer = Me) => new ItemStack(item, 1, null, quality, signer);

    // --- Catalogue des objets (version 8) --------------------------------------------------------------------------------------------

    [Fact]
    public void Catalogue_v8_deux_patisseries_de_concours_seulement()
    {
        ItemCatalog c = ItemsCatalog;
        Assert.Equal((8, 8, "0.21.0-provisoire"), (c.SchemaVersion, ItemValidator.CurrentSchemaVersion, c.ContentVersion));
        Assert.Equal(new[] { Tarte, Couronne, "item.fouace_des_semailles" }, c.Items.Where(i => i.CraftQuality).Select(i => i.Id));
        Assert.All(new[] { Couronne, Tarte }, id => Assert.True(c.TakesQuality(c.Item(id)) && c.SignsAlways(c.Item(id))));
        // Un autre objet de soin ne prend pas de qualité ; un équipement prend une qualité mais n'est signé que chef-d'œuvre.
        Assert.False(c.TakesQuality(c.Item("item.potion_de_soin")));
        ItemDefinition gear = c.Items.First(i => c.TakesQuality(i) && i.Type == ItemType.Equipment);
        Assert.False(c.SignsAlways(gear));
        // Marque du pâtissier : admise à toute qualité sur une pâtisserie de concours, refusée ailleurs hors chef-d'œuvre.
        Assert.Null(c.CheckInstance(Signed(Tarte, ItemQuality.Normal)));
        Assert.Null(c.CheckInstance(Signed(Couronne, ItemQuality.Superior)));
        Assert.NotNull(c.CheckInstance(new ItemStack(gear.Id, 1, null, ItemQuality.Superior, Me)));
        Assert.NotNull(c.CheckInstance(Signed("item.potion_de_soin", ItemQuality.Normal)));
        // Règle sûre : la marque d'un objet qui n'en porte plus est retirée.
        ItemStack settled = c.Settle(new ItemStack(gear.Id, 1, null, ItemQuality.Superior, Me), out IReadOnlyList<string> changes);
        Assert.Equal((ItemQuality.Superior, (string?)null), (settled.Quality, settled.SignedBy));
        Assert.Single(changes);
    }

    private static ItemCatalogLoadResult MutatedItems(Action<JsonObject> change)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ItemJsonReader.PathIn(DataDirectory)))!.AsObject();
        change(root);
        return ItemJsonReader.Read(root.ToJsonString(), ShippedData);
    }

    private static JsonObject ItemNode(JsonObject root, string id) =>
        root["data"]!["items"]!.AsArray().Select(x => x!.AsObject()).Single(x => (string?)x["id"] == id);

    [Fact]
    public void Catalogue_craftQuality_refuse_hors_d_un_objet_de_soin_et_en_version_7()
    {
        int index = JsonNode.Parse(File.ReadAllText(ItemJsonReader.PathIn(DataDirectory)))!["data"]!["items"]!.AsArray()
            .Select((x, i) => ((string?)x!["id"], i)).Single(p => p.Item1 == "item.farine_de_ble").i;
        ItemCatalogLoadResult material = MutatedItems(r => ItemNode(r, "item.farine_de_ble")["craftQuality"] = true);
        Assert.False(material.IsValid);
        Assert.Contains(material.Report.Errors, e => e.Path == $"$.data.items[{index}].craftQuality");
        ItemCatalogLoadResult old = MutatedItems(r => r["schemaVersion"] = 7);
        Assert.False(old.IsValid);
        Assert.Contains(old.Report.Errors, e => e.Path == "$.schemaVersion" && e.Code == ValidationCode.UnsupportedSchemaVersion);
    }

    // --- Au four de Firmin : la qualité et la marque -----------------------------------------------------------------------------------

    private static ZoneDefinition ZoneAt(double x, double z)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(x, z);
        return ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog).Zone!;
    }

    private static CraftingBook Crafting(ZoneDefinition zone) =>
        CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory), Texts, zone, ItemsCatalog).Book!;

    private static WorldSession Oven(params ItemStack[] items)
    {
        ZoneDefinition zone = ZoneAt(-1075.5, -31.5);
        SavedCharacter hero = Hero(0, items).WithProfessions(new Dictionary<string, long> { ["cuisine"] = 1_000_000 });
        var session = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero), zone, ShippedData, Progression, ItemsCatalog, null, Crafting(zone));
        session.Step();
        return session;
    }

    [Fact]
    public void Les_deux_patisseries_se_cuisent_a_qualite_et_portent_la_marque_du_patissier()
    {
        CraftingBook book = Crafting(ZoneAt(-1075.5, -31.5));
        Assert.Equal("0.12.0-provisoire", book.ContentVersion);
        Recipe[] cuisine = book.Recipes.Where(r => r.Profession == "cuisine").ToArray();
        Assert.Equal(new[] { "recipe.cuisine.tarte_renversee_aux_pommes", "recipe.cuisine.couronne_des_moissons", "recipe.cuisine.fouace_des_semailles" },
            cuisine.Where(r => r.Quality).Select(r => r.Id));
        Assert.All(cuisine, r => Assert.Equal(r.Quality, r.Signed));
        // Une recette à qualité demande le petit jeu (les trois gestes) ; sans lui, refusée et rien ne change.
        var stock = new[] { new ItemStack("item.pate_feuilletee", 10), new ItemStack("item.pomme_du_verger", 20), new ItemStack("item.beurre_de_baratte", 10),
            new ItemStack("item.sucre_roux", 20) };
        WorldSession w = Oven(stock);
        Assert.Equal(CraftRefusal.BadStrikes, w.Craft("recipe.cuisine.tarte_renversee_aux_pommes", out _));
        Assert.Equal(10, w.Inventory.Count("item.pate_feuilletee"));
        // Cinq tartes, gestes parfaits : chacune est de la main du personnage, quelle que soit sa qualité ; aucune ne s'empile.
        var qualities = new List<ItemQuality>();
        for (int k = 0; k < 5; k++)
        {
            Assert.Equal(CraftRefusal.None, w.Craft("recipe.cuisine.tarte_renversee_aux_pommes", new ForgeStrikes(100, 100, 100), out CraftEvent? done));
            Assert.Equal(CraftEventKind.Crafted, done!.Kind);
            qualities.Add(done.Quality);
        }
        ItemStack[] tartes = w.Inventory.Stacks.Where(s => s.Item == Tarte).ToArray();
        Assert.Equal(5, tartes.Length);
        Assert.All(tartes, t => Assert.Equal((1, Me), (t.Count, t.SignedBy)));
        Assert.Equal(qualities.OrderBy(q => q), tartes.Select(t => t.Quality).OrderBy(q => q));
        Assert.Contains(qualities, q => q != ItemQuality.Normal);   // gestes parfaits, métier au plus haut
        // La sauvegarde (version 24) garde la marque et la qualité.
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!;
        Assert.Equal(tartes, saved.Character!.Inventory.Where(s => s.Item == Tarte));
    }

    // --- Sauvegarde (version 24) -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Sauvegarde_v24_marque_du_patissier_relue_et_refusee_avant()
    {
        Assert.Equal((26, 24), (SaveValidator.CurrentSchemaVersion, SaveValidator.BakerMarkSince));
        WorldSession w = Oven(Signed(Tarte, ItemQuality.Normal), Signed(Couronne, ItemQuality.Superior), Signed(Couronne, ItemQuality.Masterwork),
            new ItemStack(Couronne, 2));
        string json = SaveJsonReader.Write(w.Save());
        Assert.Contains($"\"item\": \"{Tarte}\", \"count\": 1, \"signedBy\": \"{Me}\"", json);
        Assert.Contains($"\"item\": \"{Couronne}\", \"count\": 1, \"quality\": \"superior\", \"signedBy\": \"{Me}\"", json);
        WorldSave back = SaveJsonReader.Read(json).Save!;
        Assert.Equal(new[] { (ItemQuality.Normal, (string?)Me), (ItemQuality.Superior, Me), (ItemQuality.Masterwork, Me), (ItemQuality.Normal, null) },
            back.Character!.Inventory.Where(s => s.Item == Tarte || s.Item == Couronne).Select(s => (s.Quality, s.SignedBy)));
        // En version 22 (monture) et avant, seul un chef-d'œuvre est signé.
        JsonObject root = JsonNode.Parse(json)!.AsObject();
        root["schemaVersion"] = 22;
        SaveLoadResult old = SaveJsonReader.Read(root.ToJsonString());
        Assert.False(old.IsValid);
        Assert.Contains(old.Report.Errors, e => e.Path.EndsWith(".signedBy", StringComparison.Ordinal));
        // Un chef-d'œuvre reste toujours signé.
        Assert.Throws<ArgumentException>(() => new ItemStack(Couronne, 1, null, ItemQuality.Masterwork, null));
        Assert.True(Signed(Tarte, ItemQuality.Normal).IsUnique);
    }

    // --- Le concours de Firmin (histoire en schéma 15) ---------------------------------------------------------------------------------

    private static long TickAt(long day, int minute)
    {
        var clock = new WorldClock(50);
        long tick = (day * WorldClock.MinutesPerDay + minute - WorldClock.DefaultStartMinute) * clock.TicksPerDay / WorldClock.MinutesPerDay;
        while (tick > 0 && clock.At(tick - 1).Equals(new WorldTime(day, minute))) tick--;
        while (!clock.At(tick).Equals(new WorldTime(day, minute))) tick++;
        return tick;
    }

    /// <summary>Sur la place, à la Fête des Moissons (jour 63 du calendrier du jeu, 10 h 30), près de Firmin.</summary>
    private static WorldSession AtFestival(SavedCharacter hero, long day = 63, double x = -1087, double z = -30.9)
    {
        ZoneDefinition zone = Zone(x, z);
        var session = new WorldSession(new WorldSave("zone.vallons", TickAt(day, 10 * 60 + 30) - 1, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone,
            ShippedData, Progression, ItemsCatalog, Book(zone), market: new Market(ShippedMerchants));
        session.Step();
        return session;
    }

    private static DialogueView Talk(WorldSession w)
    {
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.boulanger", out DialogueView? view));
        return view!;
    }

    /// <summary>Présente la pâtisserie <paramref name="pastry"/> (« couronne » ou « tarte ») de la qualité <paramref name="quality"/>.</summary>
    private static DialogueView Present(WorldSession w, string pastry, string quality, out IReadOnlyList<StoryEvent> events)
    {
        DialogueView view = Talk(w);
        Assert.Equal("v215_concours", view.Node);
        Assert.Equal(DialogueRefusal.None, w.Choose(view.Options.Single(o => o.Text == "text.v215.boulanger." + pastry).Index, out DialogueView? which, out _));
        Assert.Equal("v215_" + pastry, which!.Node);
        Assert.Equal(DialogueRefusal.None, w.Choose(which.Options.Single(o => o.Text == $"text.v215.boulanger.{pastry}.{quality}").Index, out DialogueView? verdict, out events));
        return verdict!;
    }

    private static SavedCharacter Baker(params ItemStack[] items)
    {
        var pain = new Dictionary<string, QuestProgress> { ["quest.pain_du_matin"] = new QuestProgress(QuestProgress.Done) };
        return Hero(0, items).WithStory(new SavedStory(new[] { "fact.met.boulanger" }, pain));
    }

    [Fact]
    public void Firmin_juge_la_patisserie_de_sa_main_et_la_qualite_decide_du_prix()
    {
        WorldSession w = AtFestival(Baker(Signed(Couronne, ItemQuality.Masterwork), Signed(Tarte, ItemQuality.Normal), new ItemStack(Couronne, 1)));
        DialogueView view = Talk(w);
        Assert.Equal("v215_concours", view.Node);
        Assert.Contains(view.Options, o => o.Text == "text.v215.boulanger.couronne");
        Assert.Contains(view.Options, o => o.Text == "text.v215.boulanger.tarte");
        Assert.Equal(DialogueRefusal.None, w.Choose(view.Options.Single(o => o.Text == "text.v215.boulanger.couronne").Index, out DialogueView? which, out _));
        // Seules les qualités qu'il a de sa main : la couronne achetée chez Firmin (sans marque) ne compte pas.
        Assert.Equal(new[] { "text.v215.boulanger.couronne.masterwork", "text.v215.boulanger.retour" }, which!.Options.Select(o => o.Text));
        Assert.Equal(DialogueRefusal.None, w.Choose(which.Options[0].Index, out DialogueView? verdict, out IReadOnlyList<StoryEvent> events));
        Assert.Equal("v215_chef", verdict!.Node);
        Assert.Contains(events, e => e.Kind == StoryEventKind.ItemPresented && e.Id == Couronne && e.Step == "masterwork");
        Assert.Contains(events, e => e.Kind == StoryEventKind.ContestEntered && e.Id == Moissons);
        Assert.Equal((600L, 20), (w.Money, w.Narrative!.Reputation("villagers")));
        // La couronne de sa main est partie ; la couronne achetée et la tarte restent.
        Assert.Equal(0, w.Inventory.CountSigned(Couronne, Me, null));
        Assert.Equal((1, 1), (w.Inventory.Count(Couronne), w.Inventory.CountSigned(Tarte, Me, ItemQuality.Normal)));
        // Un concours par fête : fermé jusqu'à l'an prochain (Firmin reprend son accueil de fête).
        Assert.Equal("v210_moissons", Talk(w).Node);
        SavedCharacter saved = w.Save().Character!;
        WorldSession nextYear = AtFestival(saved, 63 + WorldCalendar.DaysPerYear);
        verdict = Present(nextYear, "tarte", "normal", out _);
        Assert.Equal(("v215_normale", 680L), (verdict.Node, nextYear.Money));
    }

    [Theory]
    [InlineData(Couronne, ItemQuality.Masterwork, "couronne", "masterwork", 600L, 20)]
    [InlineData(Couronne, ItemQuality.Superior, "couronne", "superior", 300L, 12)]
    [InlineData(Couronne, ItemQuality.Normal, "couronne", "normal", 150L, 6)]
    [InlineData(Tarte, ItemQuality.Masterwork, "tarte", "masterwork", 300L, 15)]
    [InlineData(Tarte, ItemQuality.Superior, "tarte", "superior", 150L, 9)]
    [InlineData(Tarte, ItemQuality.Normal, "tarte", "normal", 80L, 4)]
    public void Grille_des_prix(string item, ItemQuality quality, string pastry, string choice, long prize, int reputation)
    {
        WorldSession w = AtFestival(Baker(Signed(item, quality)));
        Present(w, pastry, choice, out _);
        Assert.Equal((prize, 0, reputation), (w.Money, w.Inventory.Count(item), w.Narrative!.Reputation("villagers")));
    }

    // --- Concours des Semailles (décision de Damien du 16 novembre 2026) --------------------------------------------------------------

    private const string Fouace = "item.fouace_des_semailles";

    /// <summary>Sur la place, aux Semailles (jour 14 du calendrier du jeu, 10 h 30), près de l'étal de Firmin (−54,5 ; 0).</summary>
    private static WorldSession AtSemailles(SavedCharacter hero) => AtFestival(hero, 14, -1086.5, -32.5);

    [Theory]
    [InlineData(ItemQuality.Masterwork, "masterwork", "v231_chef", 600L, 20)]
    [InlineData(ItemQuality.Superior, "superior", "v231_superieure", 300L, 12)]
    [InlineData(ItemQuality.Normal, "normal", "v231_normale", 150L, 6)]
    public void Semailles_la_fouace_de_sa_main_au_bareme_de_la_couronne(ItemQuality quality, string choice, string node, long prize, int reputation)
    {
        WorldSession w = AtSemailles(Baker(Signed(Fouace, quality), new ItemStack(Fouace, 1)));
        Assert.Equal("festival.foire", w.Festival?.Id);
        DialogueView view = Talk(w);
        Assert.Equal("v231_semailles", view.Node);
        Assert.Equal(DialogueRefusal.None, w.Choose(view.Options.Single(o => o.Text == "text.v231.boulanger.fouace").Index, out DialogueView? which, out _));
        Assert.Equal(new[] { "text.v215.boulanger.couronne." + choice, "text.v215.boulanger.retour" }, which!.Options.Select(o => o.Text));
        Assert.Equal(DialogueRefusal.None, w.Choose(which.Options[0].Index, out DialogueView? verdict, out IReadOnlyList<StoryEvent> events));
        Assert.Equal(node, verdict!.Node);
        Assert.Contains(events, e => e.Kind == StoryEventKind.ItemPresented && e.Id == Fouace && e.Step == choice);
        Assert.Contains(events, e => e.Kind == StoryEventKind.ContestEntered && e.Id == "festival.foire");
        // La fouace de sa main est un trophée parti chez le juge ; la fouace sans marque reste ; le prix, la réputation.
        Assert.Equal((prize, reputation, 0, 1), (w.Money, w.Narrative!.Reputation("villagers"), w.Inventory.CountSigned(Fouace, Me, null), w.Inventory.Count(Fouace)));
        // Un concours par fête et par an : fermé jusqu'à l'an prochain (Firmin reprend son accueil des Semailles).
        Assert.Equal("v210_foire", Talk(w).Node);
    }

    /// <summary>
    /// Décision de Damien du 16 novembre 2026 (vers 2 h 20) : à une même fête, le tir d'Aldric (ou la tourte) et le concours de pâtisserie de
    /// Firmin ne se ferment plus l'un l'autre : la pâtisserie a sa piste (« lane » : patisserie, histoire en schéma 16), clé « fête.patisserie ».
    /// </summary>
    [Theory]
    [InlineData("festival.foire", 14L)]
    [InlineData("festival.moissons", 63L)]
    public void Le_concours_de_patisserie_ne_ferme_plus_l_autre_concours_de_la_fete(string festival, long day)
    {
        bool spring = festival == "festival.foire";
        ItemStack pastry = Signed(spring ? Fouace : Couronne, ItemQuality.Normal);
        WorldSession Open(SavedCharacter hero) => spring ? AtSemailles(hero) : AtFestival(hero);
        // Le concours principal de la fête (tir d'Aldric, tourte de Firmin) déjà fait : le concours de pâtisserie reste ouvert.
        SavedCharacter baker = Baker(pastry);
        SavedCharacter done = baker.WithStory(baker.Story.WithFestivals(new Dictionary<string, long> { [festival] = day }));
        WorldSession w = Open(done);
        Assert.Equal(spring ? "v231_semailles" : "v215_concours", Talk(w).Node);
        // Et l'inverse : la pâtisserie présentée, le concours principal reste ouvert ; la pâtisserie se ferme pour l'année.
        WorldSession x = Open(baker);
        DialogueView view = Talk(x);
        Assert.Equal(DialogueRefusal.None, x.Choose(view.Options.Single(o => o.Text == (spring ? "text.v231.boulanger.fouace" : "text.v215.boulanger.couronne")).Index, out DialogueView? which, out _));
        Assert.Equal(DialogueRefusal.None, x.Choose(which!.Options[0].Index, out _, out IReadOnlyList<StoryEvent> events));
        Assert.Contains(events, e => e.Kind == StoryEventKind.ContestEntered && e.Id == festival);
        Assert.Equal((-1L, day), (x.Narrative!.ContestDay(festival), x.Narrative.ContestDay(festival + ".patisserie")));
        Assert.False(x.Contested(festival));
        SavedStory saved = x.Save().Character!.Story;
        Assert.Equal(day, saved.Festivals[festival + ".patisserie"]);
        Assert.False(saved.Festivals.ContainsKey(festival));
        Assert.Equal(spring ? "v210_foire" : "v210_moissons", Talk(Open(x.Save().Character!)).Node);   // relu : la pâtisserie reste faite
    }

    /// <summary>
    /// Revue Codex de la PR #189 : une sauvegarde aux seize clés de pistes inconnues (« festival.foire.a » à « .p ») se charge, ces clés
    /// sont oubliées, et la partie se sauvegarde encore après un concours (au plus seize clés, celles des effets « contest »).
    /// </summary>
    [Fact]
    public void Pistes_inconnues_de_la_sauvegarde_oubliees_et_sauvegarde_toujours_possible()
    {
        SavedCharacter baker = Baker(Signed(Fouace, ItemQuality.Normal));
        var junk = Enumerable.Range(0, SavedStory.MaxFestivals).ToDictionary(i => "festival.foire." + (char)('a' + i), _ => 1L);
        WorldSession w = AtSemailles(baker.WithStory(baker.Story.WithFestivals(junk)));
        DialogueView view = Talk(w);
        Assert.Equal(DialogueRefusal.None, w.Choose(view.Options.Single(o => o.Text == "text.v231.boulanger.fouace").Index, out DialogueView? which, out _));
        Assert.Equal(DialogueRefusal.None, w.Choose(which!.Options[0].Index, out _, out _));
        SavedStory saved = w.Save().Character!.Story;
        Assert.Equal(new[] { "festival.foire.patisserie" }, saved.Festivals.Keys);
        Assert.True(w.Narrative!.Book.HasContestKey("festival.moissons"));
        Assert.True(w.Narrative.Book.HasContestKey("festival.moissons.patisserie"));
        Assert.False(w.Narrative.Book.HasContestKey("festival.foire.a"));
        Assert.InRange(w.Narrative.Book.ContestKeyCount, 1, SavedStory.MaxFestivals);
    }

    /// <summary>Revue Codex de la PR #189 : la clé « fête.piste » ne dépasse pas les 200 caractères que relit la sauvegarde.</summary>
    [Fact]
    public void Cle_fete_piste_bornee_a_200_caracteres()
    {
        string longId = "festival.foire" + new string('x', 200 - "festival.foire".Length);   // 200 caractères : une fête valide
        JsonObject root = JsonNode.Parse(StoryJson().ToJsonString().Replace("\"festival.foire\"", "\"" + longId + "\""))!.AsObject();
        StoryLoadResult read = ReadStory(root);
        Assert.False(read.IsValid);
        Assert.Contains(read.Report.Errors, e => e.Path.EndsWith(".lane") && e.Code == ValidationCode.OutOfRange);
        Assert.DoesNotContain(read.Report.Errors, e => !e.Path.EndsWith(".lane"));   // seule la piste est refusée
        Assert.Equal(200, SavedStory.MaxFestivalKeyLength);
    }

    [Fact]
    public void Piste_de_concours_en_schema_16_seulement_et_au_format()
    {
        JsonObject root = StoryJson();
        root["schemaVersion"] = 15;
        StoryLoadResult old = ReadStory(root);
        Assert.False(old.IsValid);
        Assert.Contains(old.Report.Errors, e => e.Path.EndsWith(".lane") && e.Message.Contains("version 16"));
        var (pastry, path, choice) = FirstPastryChoice();
        choice["effects"]!.AsArray().Select(x => x!.AsObject()).Single(x => (string?)x["do"] == "contest")["lane"] = "autre";
        StoryLoadResult mismatch = ReadStory(pastry);
        Assert.False(mismatch.IsValid);
        Assert.Contains(mismatch.Report.Errors, e => e.Path.StartsWith(path + ".effects") && e.Message.Contains("même piste"));
        (pastry, path, choice) = FirstPastryChoice();
        choice["effects"]!.AsArray().Select(x => x!.AsObject()).Single(x => (string?)x["do"] == "giveMoney")["lane"] = "patisserie";
        Assert.Contains(ReadStory(pastry).Report.Errors, e => e.Path.StartsWith(path + ".effects") && e.Path.EndsWith(".lane"));
    }

    [Fact]
    public void Semailles_sans_fouace_de_sa_main_ni_aux_moissons()
    {
        Assert.Equal("v210_foire", Talk(AtSemailles(Baker(new ItemStack(Fouace, 2)))).Node);
        Assert.Equal("v210_foire", Talk(AtSemailles(Baker(Signed(Couronne, ItemQuality.Masterwork)))).Node);   // la couronne est des Moissons
        Assert.Equal("v210_moissons", Talk(AtFestival(Baker(Signed(Fouace, ItemQuality.Masterwork)))).Node);   // la fouace est des Semailles
        // « Parlons d'autre chose » ramène à l'accueil des Semailles.
        WorldSession w = AtSemailles(Baker(Signed(Fouace, ItemQuality.Normal)));
        DialogueView view = Talk(w);
        Assert.Equal(DialogueRefusal.None, w.Choose(view.Options.Single(o => o.Text == "text.v215.boulanger.autre").Index, out DialogueView? next, out _));
        Assert.Equal("v210_foire", next!.Node);
    }

    [Fact]
    public void Sans_patisserie_de_sa_main_Firmin_garde_son_accueil_de_fete()
    {
        // Achetée, ou faite par un autre : pas de concours de pâtisserie.
        Assert.Equal("v210_moissons", Talk(AtFestival(Baker(new ItemStack(Couronne, 2)))).Node);
        Assert.Equal("v210_moissons", Talk(AtFestival(Baker(Signed(Tarte, ItemQuality.Masterwork, "autre")))).Node);
        // Hors de la fête, rien.
        WorldSession after = AtFestival(Baker(Signed(Tarte, ItemQuality.Normal)), 70);
        Assert.False(after.Talk("npc.boulanger", out DialogueView? ordinary) == DialogueRefusal.None && ordinary!.Node == "v215_concours");
        // « Parlons d'autre chose » ramène à l'accueil de la fête (le concours des tourtes y reste).
        WorldSession w = AtFestival(Baker(Signed(Tarte, ItemQuality.Normal)));
        DialogueView view = Talk(w);
        Assert.Equal(DialogueRefusal.None, w.Choose(view.Options.Single(o => o.Text == "text.v215.boulanger.autre").Index, out DialogueView? next, out _));
        Assert.Equal("v210_moissons", next!.Node);
    }

    // --- Validateur de l'histoire (schéma 15) ------------------------------------------------------------------------------------------

    private static JsonObject StoryJson() => JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!.AsObject();

    private static StoryLoadResult ReadStory(JsonObject root) => StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Zone(), ItemsCatalog, V16TestData.Interiors);

    private static (JsonObject Root, string Path, JsonObject Choice) FirstPastryChoice()
    {
        JsonObject root = StoryJson();
        JsonArray dialogues = root["data"]!["dialogues"]!.AsArray();
        int d = dialogues.Select((x, i) => ((string?)x!["id"], i)).Single(p => p.Item1 == "dialogue.boulanger").i;
        JsonArray nodes = dialogues[d]!["nodes"]!.AsArray();
        int n = nodes.Select((x, i) => ((string?)x!["id"], i)).Single(p => p.Item1 == "v215_couronne").i;
        return (root, $"$.data.dialogues[{d}].nodes[{n}].choices[0]", nodes[n]!["choices"]![0]!.AsObject());
    }

    [Fact]
    public void Histoire_schema_15_et_ses_refus()
    {
        Assert.Equal((16, 15), (StoryValidator.CurrentSchemaVersion, StoryValidator.Schema15));
        Assert.True(ReadStory(StoryJson()).IsValid);
        // Présenter une qualité que la réponse ne demande pas : refusé au chemin de l'effet.
        var (root, path, choice) = FirstPastryChoice();
        choice["effects"]![0]!["quality"] = "normal";
        Assert.Contains(ReadStory(root).Report.Errors, e => e.Path == path + ".effects[0].id");
        // Une pâtisserie qui n'en est pas une (objet de soin ordinaire).
        (root, path, choice) = FirstPastryChoice();
        choice["conditions"]![1]!["id"] = "item.potion_de_soin";
        Assert.Contains(ReadStory(root).Report.Errors, e => e.Path == path + ".conditions[1].id");
        // Une qualité inconnue, puis absente de l'effet.
        (root, path, choice) = FirstPastryChoice();
        choice["conditions"]![1]!["quality"] = "excellent";
        Assert.Contains(ReadStory(root).Report.Errors, e => e.Path == path + ".conditions[1].quality");
        (root, path, choice) = FirstPastryChoice();
        choice["effects"]![0]!.AsObject().Remove("quality");
        Assert.Contains(ReadStory(root).Report.Errors, e => e.Path == path + ".effects[0].quality");
        // La qualité reste interdite aux autres conditions.
        (root, path, choice) = FirstPastryChoice();
        choice["conditions"]![0]!["quality"] = "normal";
        Assert.Contains(ReadStory(root).Report.Errors, e => e.Path == path + ".conditions[0].quality");
        // En version 14, hasSigned et present sont inconnus.
        (root, path, _) = FirstPastryChoice();
        root["schemaVersion"] = 14;
        StoryLoadResult old = ReadStory(root);
        Assert.Contains(old.Report.Errors, e => e.Path == path + ".conditions[1].if");
        Assert.Contains(old.Report.Errors, e => e.Path == path + ".effects[0].do");
    }

    // --- Relecture indépendante de la PR #186 ----------------------------------------------------------------------------------------

    [Fact]
    public void La_patisserie_marquee_ne_part_pas_au_combat_et_ne_part_jamais_a_la_place_d_une_copie_de_base()
    {
        // Trois couronnes achetées (emportées au combat) et une de sa main : seules les trois achetées sont réservées.
        WorldSession w = AtFestival(Baker(new ItemStack(Couronne, 3), Signed(Couronne, ItemQuality.Masterwork)));
        Inventory bag = w.Inventory;
        int basePlace = bag.Stacks.ToList().FindIndex(s => s.Item == Couronne && s.SignedBy is null);
        int signedPlace = bag.Stacks.ToList().FindIndex(s => s.Item == Couronne && s.SignedBy == Me);
        Assert.Equal((3, 0, 0), (bag.Reserved(Couronne), bag.Available(Couronne), bag.BaseAvailable(Couronne)));
        // Ni la vente d'une copie de base réservée, ni un retrait par identifiant (qui prendrait la pâtisserie marquée) : refusés, rien ne change.
        Assert.Equal((0, 1), (bag.AvailableAt(basePlace), bag.AvailableAt(signedPlace)));
        Assert.Equal(InventoryRefusal.NotEnough, bag.RemoveAt(basePlace, 1, out _));
        Assert.Equal(InventoryRefusal.NotEnough, bag.Remove(Couronne, 1));
        Assert.Equal((4, 1), (bag.Count(Couronne), bag.CountSigned(Couronne, Me, ItemQuality.Masterwork)));
        // Seule une pâtisserie de sa main dans le sac : rien n'est emporté au combat.
        Assert.Equal(0, AtFestival(Baker(Signed(Tarte, ItemQuality.Normal))).Inventory.Reserved(Tarte));
        // Présenter une pâtisserie absente : refusé sans rien changer (le validateur l'empêche dans les données : réponse gardée par hasSigned).
        Assert.Equal(InventoryRefusal.NotEnough, bag.RemoveSigned(Couronne, Me, ItemQuality.Superior));
        Assert.Equal(4, bag.Count(Couronne));
    }

    [Fact]
    public void Tourte_et_patisserie_a_la_meme_fete_decision_du_16_novembre()
    {
        // Décision de Damien du 16 novembre 2026 (vers 2 h 20) : la pâtisserie ne ferme plus la tourte (avant : « tourte ou pâtisserie »).
        WorldSession w = AtFestival(Baker(Signed(Tarte, ItemQuality.Normal), new ItemStack("item.herbe_de_l_aube", 8)));
        Present(w, "tarte", "normal", out _);
        DialogueView after = Talk(w);
        Assert.Equal("v210_moissons", after.Node);
        Assert.Contains(after.Options, o => o.Text == "text.v210.boulanger.tourte");
        Assert.Equal(80L, w.Money);
    }

    [Fact]
    public void Present_refuse_hors_d_un_concours()
    {
        var (root, path, choice) = FirstPastryChoice();
        JsonArray effects = choice["effects"]!.AsArray();
        effects.Remove(effects.Single(e => (string?)e!["do"] == "contest"));
        Assert.Contains(ReadStory(root).Report.Errors, e => e.Path == path + ".effects[0].do" && e.Message.Contains("contest", StringComparison.Ordinal));
    }

    [Fact]
    public void Sauvegarde_v21_relue_puis_reecrite_en_version_courante()
    {
        WorldSession w = Oven(Signed(Couronne, ItemQuality.Masterwork), new ItemStack(Couronne, 2));
        JsonObject root = JsonNode.Parse(SaveJsonReader.Write(w.Save()))!.AsObject();
        root["schemaVersion"] = 21;   // un chef-d'œuvre signé se lisait déjà en version 21
        root["data"]!["character"]!.AsObject().Remove("explored");   // cases explorées : version 23 (brouillard)
        SaveLoadResult old = SaveJsonReader.Read(root.ToJsonString());
        Assert.True(old.IsValid, old.Report.ToString());
        Assert.True(old.Save!.Migrated);
        string rewritten = SaveJsonReader.Write(old.Save);
        Assert.Contains($"\"schemaVersion\": {SaveValidator.CurrentSchemaVersion}", rewritten);
        Assert.Equal(new[] { (ItemQuality.Masterwork, (string?)Me, 1), (ItemQuality.Normal, null, 2) },
            SaveJsonReader.Read(rewritten).Save!.Character!.Inventory.Where(s => s.Item == Couronne).Select(s => (s.Quality, s.SignedBy, s.Count)));
    }

    [Fact]
    public void Settle_retire_la_marque_quand_la_patisserie_n_en_prend_plus()
    {
        ItemCatalog without = MutatedItems(r => ItemNode(r, Tarte).Remove("craftQuality")).Catalog!;
        ItemStack normal = without.Settle(Signed(Tarte, ItemQuality.Normal), out IReadOnlyList<string> n);
        ItemStack superior = without.Settle(Signed(Tarte, ItemQuality.Superior), out IReadOnlyList<string> s);
        Assert.Equal((ItemQuality.Normal, (string?)null, false), (normal.Quality, normal.SignedBy, normal.IsUnique));
        Assert.Equal((ItemQuality.Normal, (string?)null, false), (superior.Quality, superior.SignedBy, superior.IsUnique));
        Assert.Single(n);
        Assert.Single(s);
        Assert.Null(without.CheckInstance(normal));
    }

    [Fact]
    public void Une_patisserie_de_qualite_sans_marque_se_charge_et_ne_part_pas_au_combat()
    {
        // Revue Codex de la PR #186 : une tarte supérieure sans marque (admise par le catalogue) ne fait pas échouer l'entrée en session.
        var odd = new ItemStack(Tarte, 1, null, ItemQuality.Superior);
        Assert.Null(ItemsCatalog.CheckInstance(odd));
        WorldSession w = AtFestival(Baker(odd, new ItemStack(Tarte, 2)));
        Assert.Equal((2, 1, 3), (w.Inventory.Reserved(Tarte), w.Inventory.Available(Tarte), w.Inventory.Count(Tarte)));
        Assert.Equal(0, AtFestival(Baker(odd)).Inventory.Reserved(Tarte));
    }
}
