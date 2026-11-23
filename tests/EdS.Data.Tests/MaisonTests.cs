using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Feasts;
using EdS.Data.Homes;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Rebuilds;
using EdS.Data.Saves;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Maison du joueur, sous-lot V2.53.1 (cœur ; décisions de Damien du 20 novembre 2026) : catalogue <c>kind: "home"</c> (version 1) et son
/// validateur à chemin précis ; règles pures (achat une fois, paliers comme un chantier, pose libre sur la grille de 0,25 m avec rotation,
/// sans chevauchement, reprise, coffre) ; sauvegarde v29 (<c>character.home</c>, v28 encore lue) ; dans la partie : refus atomiques.
/// </summary>
[Trait("Category", "World")]
public class MaisonTests
{
    private const string Plank = "item.planche", Stone = "item.pierre_taillee", Rubble = "item.pierre", Deco = "F01_D01";

    private static readonly Lazy<WorldZonesLoadResult> Linked = new(() =>
        ZoneJsonReader.ReadLinked(DataDirectory, "zone.vallons", Path.Combine(AppContext.BaseDirectory, "scenarios"), ShippedData, ItemsCatalog));

    private static readonly Lazy<FeastBook> Feasts = new(() => FeastJsonReader.ReadFile(FeastJsonReader.PathIn(DataDirectory), Book(Zone()), Texts).Book!);

    private static IReadOnlyList<ZoneDefinition> Zones => Linked.Value.Zones;

    private static string Catalog(Action<JsonObject>? mutate = null)
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["kind"] = "home",
            ["contentVersion"] = "0.1.0-essai",
            ["data"] = new JsonObject
            {
                ["interior"] = "zone.chaumiere",
                ["door"] = "porte_chaumiere",
                ["price"] = 5_000,
                ["base"] = new JsonObject { ["floor"] = new JsonObject { ["width"] = 2, ["depth"] = 1.5 }, ["chest"] = 2, ["pieces"] = 3 },
                ["stages"] = new JsonArray(
                    new JsonObject
                    {
                        ["id"] = "aile",
                        ["needs"] = new JsonArray(
                            new JsonObject { ["id"] = "or", ["coins"] = 1_000 },
                            new JsonObject { ["id"] = "planches", ["item"] = Plank, ["count"] = 4 }),
                        ["floor"] = new JsonObject { ["width"] = 4, ["depth"] = 3 },
                        ["chest"] = 4,
                        ["pieces"] = 6,
                    },
                    new JsonObject
                    {
                        ["id"] = "etage",
                        ["needs"] = new JsonArray(new JsonObject { ["id"] = "pierre", ["item"] = Stone, ["count"] = 2 }),
                        ["floor"] = new JsonObject { ["width"] = 4, ["depth"] = 4 },
                        ["chest"] = 4,
                        ["pieces"] = 6,
                    }),
                ["furniture"] = new JsonArray(
                    new JsonObject { ["id"] = "table", ["item"] = Rubble, ["size"] = new JsonObject { ["width"] = 1, ["depth"] = 0.5 } },
                    new JsonObject { ["id"] = "lanterne", ["feast"] = Deco, ["size"] = new JsonObject { ["width"] = 0.25, ["depth"] = 0.25 }, ["use"] = "decor" }),
            },
        };
        mutate?.Invoke(root);
        return root.ToJsonString();
    }

    private static readonly Lazy<HomeBook> Sample = new(() =>
    {
        HomeLoadResult r = HomeJsonReader.Read(Catalog(), Zones, ItemsCatalog, Feasts.Value);
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Book!;
    });

    private static JsonObject Data(JsonObject root) => root["data"]!.AsObject();

    private static void Refused(Action<JsonObject> mutate, string path, ValidationCode code)
    {
        HomeLoadResult r = HomeJsonReader.Read(Catalog(mutate), Zones, ItemsCatalog, Feasts.Value);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == path && e.Code == code);
    }

    [Fact]
    public void Catalogue_valide_et_son_schema()
    {
        HomeBook b = Sample.Value;
        Assert.Equal(("zone.chaumiere", "porte_chaumiere", "zone.vallons", 5_000L), (b.Interior, b.Door, b.DoorZone, b.Price));
        Assert.Equal((8, 6, 2, 3), (b.Base.Width, b.Base.Depth, b.Base.Chest, b.Base.Pieces));   // 2 m × 1,5 m : 8 × 6 cases de 0,25 m
        Assert.Equal(new[] { "aile", "etage" }, b.Stages.Select(s => s.Id));
        Assert.Equal((16, 16), (b.Tier(2).Width, b.Tier(2).Depth));
        Assert.Same(b.Tiers[1], b.Tier(5));   // au-delà du dernier palier : le dernier
        HomePiece lanterne = b.Piece("lanterne")!;
        Assert.Equal(("festival.lanternes", 1, 1), (lanterne.Festival, lanterne.Width, lanterne.Depth));
        Assert.Equal((2, 4), b.Piece("table")!.Footprint(1));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "home.schema.json"))
            .IsValid(JsonDocument.Parse(Catalog()).RootElement));
    }

    [Fact]
    public void Catalogue_invalide_refuse_avec_son_chemin()
    {
        Refused(r => Data(r)["interior"] = "zone.vallons", "$.data.interior", ValidationCode.InvalidValue);   // pas un intérieur
        Refused(r => Data(r)["interior"] = "zone.nulle_part", "$.data.interior", ValidationCode.UnknownReference);
        Refused(r => Data(r)["door"] = "porte_moulin", "$.data.door", ValidationCode.UnknownReference);
        Refused(r => Data(r)["price"] = 0, "$.data.price", ValidationCode.OutOfRange);
        Refused(r => Data(r)["base"]!["floor"]!["width"] = 1.1, "$.data.base.floor.width", ValidationCode.OutOfRange);   // hors de la grille
        Refused(r => Data(r)["base"]!["floor"]!["width"] = 33, "$.data.base.floor.width", ValidationCode.OutOfRange);
        Refused(r => Data(r)["base"]!["chest"] = 65, "$.data.base.chest", ValidationCode.OutOfRange);
        Refused(r => Data(r)["stages"]![0]!["chest"] = 1, "$.data.stages[0]", ValidationCode.InvalidValue);   // moins que la maison achetée
        Refused(r => Data(r)["stages"]![1]!["floor"]!["depth"] = 2, "$.data.stages[1]", ValidationCode.InvalidValue);
        Refused(r => Data(r)["stages"]![1]!["id"] = "aile", "$.data.stages[1].id", ValidationCode.DuplicateId);
        Refused(r => Data(r)["stages"]![0]!["needs"]![0] = new JsonObject { ["id"] = "loups", ["defeat"] = 3 }, "$.data.stages[0].needs[0]", ValidationCode.InvalidValue);
        Refused(r => Data(r)["stages"]![0]!["needs"]![1]!["item"] = "item.lame_d_essai", "$.data.stages[0].needs[1].item", ValidationCode.UnknownReference);
        Refused(r => Data(r)["furniture"]![0]!["item"] = "item.nulle_part", "$.data.furniture[0].item", ValidationCode.UnknownReference);
        Refused(r => Data(r)["furniture"]![0]!.AsObject()["feast"] = Deco, "$.data.furniture[0]", ValidationCode.InvalidValue);   // deux sources
        Refused(r => Data(r)["furniture"]![1]!["feast"] = "F01_C01", "$.data.furniture[1].feast", ValidationCode.UnknownReference);   // un costume
        Refused(r => Data(r)["furniture"]![1]!["id"] = "table", "$.data.furniture[1].id", ValidationCode.DuplicateId);
        Refused(r => Data(r)["furniture"]![0]!["size"]!["width"] = 5, "$.data.furniture[0].size", ValidationCode.OutOfRange);   // ne tient pas
        Refused(r => Data(r)["furniture"]![0]!["use"] = "trone", "$.data.furniture[0].use", ValidationCode.InvalidValue);
        Refused(r => Data(r)["furniture"]![0]!["couleur"] = "rouge", "$.data.furniture[0].couleur", ValidationCode.UnknownField);
        Refused(r => r["schemaVersion"] = 2, "$.schemaVersion", ValidationCode.UnsupportedSchemaVersion);
        HomeLoadResult noFeasts = HomeJsonReader.Read(Catalog(), Zones, ItemsCatalog, null);
        Assert.Contains(noFeasts.Report.Errors, e => e.Path == "$.data.furniture[1].feast");
    }

    // --- Règles pures ----------------------------------------------------------------------------------

    [Fact]
    public void Achat_une_fois_et_paliers_comme_un_chantier()
    {
        HomeBook b = Sample.Value;
        Assert.Equal(HomeRefusal.NotEnoughMoney, HomeRules.Buy(null, b, 4_999, out SavedHome? none));
        Assert.Null(none);
        Assert.Equal(HomeRefusal.None, HomeRules.Buy(null, b, 5_000, out SavedHome? home));
        Assert.Equal(HomeRefusal.AlreadyOwned, HomeRules.Buy(home, b, 1_000_000, out _));

        SavedHome h = HomeRules.Give(home!, b, "or", 5_000, out IReadOnlyList<RebuildStage> done);   // plafonné à ce qui reste
        Assert.Equal((0, 1_000L), (h.Stage, h.Of("or")));
        Assert.Empty(done);
        h = HomeRules.Give(h, b, "planches", 4, out done);
        Assert.Equal(("aile", 1, 0), (done.Single().Id, h.Stage, h.Given.Count));
        Assert.Equal(16, HomeRules.Tier(h, b).Width);
        Assert.Throws<ArgumentException>(() => HomeRules.Give(h, b, "planches", 1, out _));   // hors du palier en cours
        h = HomeRules.Give(h, b, "pierre", 2, out done);
        Assert.Null(HomeRules.CurrentStage(h, b));
        Assert.Equal(2, h.Stage);
        Assert.Throws<ArgumentException>(() => HomeRules.Give(h, b, "pierre", 1, out _));   // agrandie jusqu'au dernier palier
    }

    [Fact]
    public void Pose_sur_la_grille_rotation_sans_chevauchement_et_reprise()
    {
        HomeBook b = Sample.Value;
        SavedHome h = SavedHome.Bought;   // 8 × 6 cases, 3 meubles au plus
        Assert.Equal(HomeRefusal.None, HomeRules.Place(h, b, "table", 0, 0, 0, out h));   // 4 × 2 cases
        Assert.Equal(HomeRefusal.Overlap, HomeRules.Place(h, b, "table", 3, 1, 0, out SavedHome same));
        Assert.Same(h, same);   // refus : rien ne change
        Assert.Equal(HomeRefusal.OutOfFloor, HomeRules.Place(h, b, "table", 5, 0, 0, out _));   // 5 + 4 > 8
        Assert.Equal(HomeRefusal.OutOfFloor, HomeRules.Place(h, b, "table", 0, 3, 1, out _));   // tournée : 2 × 4, 3 + 4 > 6
        Assert.Equal(HomeRefusal.OutOfFloor, HomeRules.Place(h, b, "table", -1, 3, 0, out _));
        Assert.Equal(HomeRefusal.BadTurn, HomeRules.Place(h, b, "table", 4, 4, 4, out _));
        Assert.Equal(HomeRefusal.UnknownPiece, HomeRules.Place(h, b, "trone", 4, 4, 0, out _));
        Assert.Equal(HomeRefusal.None, HomeRules.Place(h, b, "table", 4, 2, 0, out h));   // bord contre bord : pas de chevauchement
        Assert.Equal(HomeRefusal.None, HomeRules.Place(h, b, "lanterne", 7, 5, 0, out h));
        Assert.Equal(HomeRefusal.NotAvailable, HomeRules.Place(h, b, "lanterne", 0, 5, 0, out _));   // une décoration, posée une fois
        Assert.Equal(HomeRefusal.TooManyPieces, HomeRules.Place(h, b, "table", 0, 4, 0, out _));
        Assert.Equal(new[] { (0, 0), (4, 2), (7, 5) }, h.Placed.Select(p => (p.X, p.Z)));   // rangés par case

        Assert.Equal(1, HomeRules.PieceAt(h, b, 7, 3));   // la case (7, 3) est sous la deuxième table
        Assert.Equal(-1, HomeRules.PieceAt(h, b, 0, 5));
        Assert.Equal(HomeRefusal.None, HomeRules.Rotate(h, b, 1, 1, out SavedHome turned));   // autour de son coin : 2 × 4 en (0, 0)
        Assert.Equal((0, 0, 1), (turned.Placed[0].X, turned.Placed[0].Z, turned.Placed[0].Turn));
        Assert.Equal(HomeRefusal.Overlap, HomeRules.Move(h, b, 0, 0, 3, 1, 0, out _));   // sur la deuxième table
        Assert.Equal(HomeRefusal.None, HomeRules.Move(h, b, 0, 0, 0, 4, 0, out SavedHome moved));   // ignore sa propre place
        Assert.Equal(new[] { (4, 2), (0, 4), (7, 5) }, moved.Placed.Select(p => (p.X, p.Z)));
        Assert.Equal(HomeRefusal.NothingThere, HomeRules.Move(h, b, 0, 5, 1, 5, 0, out _));
        Assert.Equal(HomeRefusal.None, HomeRules.PickUp(h, b, 7, 5, out SavedHome taken, out HomePiece? piece));
        Assert.Equal(("lanterne", 2), (piece!.Id, taken.Placed.Count));
        Assert.Equal(HomeRefusal.NothingThere, HomeRules.PickUp(taken, b, 7, 5, out _, out _));
    }

    [Fact]
    public void Coffre_empile_puis_refuse_tout_ou_rien()
    {
        IReadOnlyList<ItemStack> chest = Array.Empty<ItemStack>();
        Assert.Equal(HomeRefusal.None, HomeRules.ChestPut(chest, new ItemStack(Plank, 15), 2, 20, out chest));
        Assert.Equal(HomeRefusal.None, HomeRules.ChestPut(chest, new ItemStack(Plank, 10), 2, 20, out chest));   // complète, puis une place
        Assert.Equal(new[] { 20, 5 }, chest.Select(s => s.Count));
        Assert.Equal(HomeRefusal.ChestFull, HomeRules.ChestPut(chest, new ItemStack(Plank, 16), 2, 20, out IReadOnlyList<ItemStack> same));
        Assert.Same(chest, same);   // tout ou rien
        Assert.Equal(HomeRefusal.None, HomeRules.ChestPut(chest, new ItemStack(Plank, 15), 2, 20, out chest));
        var masterwork = new ItemStack(Stone, 1, quality: ItemQuality.Masterwork, signedBy: "bastion");
        Assert.Equal(HomeRefusal.ChestFull, HomeRules.ChestPut(chest, masterwork, 2, 20, out _));
        Assert.Equal(HomeRefusal.NotEnough, HomeRules.ChestTake(chest, 1, 21, out _, out _));
        Assert.Equal(HomeRefusal.NothingThere, HomeRules.ChestTake(chest, 2, 1, out _, out _));
        Assert.Equal(HomeRefusal.None, HomeRules.ChestTake(chest, 0, 5, out chest, out ItemStack? got));
        Assert.Equal((5, 15), (got!.Count, chest[0].Count));
        Assert.Equal(HomeRefusal.None, HomeRules.ChestTake(chest, 0, 15, out chest, out _));
        Assert.Equal(HomeRefusal.None, HomeRules.ChestPut(chest, masterwork, 2, 20, out chest));   // une instance : sa propre place
        Assert.Same(masterwork, chest[1]);
    }

    // --- Sauvegarde ------------------------------------------------------------------------------------

    private const string P = "$.data.character.home";

    private static SavedHome Saved => new SavedHome(1, new[] { new KeyValuePair<string, long>("pierre", 1) },
        new[] { new SavedPiece("table", 4, 2, 1), new SavedPiece("lanterne", 0, 0, 0) }, new[] { new ItemStack(Plank, 12) });

    private static string Write(SavedCharacter c) => SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), c));

    private static string Mutated(Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(Write(Hero().WithHome(Saved)))!.AsObject();
        mutate(root);
        return root.ToJsonString();
    }

    private static void SaveRefused(string json, string path, ValidationCode code)
    {
        SaveLoadResult r = SaveJsonReader.Read(json);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == path && e.Code == code);
    }

    [Fact]
    public void Sauvegarde_v29_aller_retour_et_v28_encore_lue()
    {
        Assert.Equal((33, 29), (SaveValidator.CurrentSchemaVersion, SaveValidator.HomeSince));
        string json = Write(Hero().WithHome(Saved));
        Assert.Contains("\"home\": {\"stage\": 1, \"given\": {\"pierre\": 1}, \"placed\": [{\"piece\": \"lanterne\", \"x\": 0, \"z\": 0, \"turn\": 0}, "
            + "{\"piece\": \"table\", \"x\": 4, \"z\": 2, \"turn\": 1}], \"chest\": [{\"item\": \"item.planche\", \"count\": 12}]},", json, StringComparison.Ordinal);
        SaveLoadResult back = SaveJsonReader.Read(json);
        Assert.True(back.IsValid, back.Report.ToString());
        Assert.Equal(json, Write(back.Save!.Character!));   // écriture canonique, octet par octet
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json"))
            .IsValid(JsonDocument.Parse(json).RootElement));
        Assert.DoesNotContain("\"home\"", Write(Hero()), StringComparison.Ordinal);   // pas de maison : pas de « home »
        string bought = Write(Hero().WithHome(SavedHome.Bought));
        Assert.Contains("\"home\": {\"stage\": 0, \"given\": {}, \"placed\": [], \"chest\": []},", bought, StringComparison.Ordinal);
        Assert.Equal(bought, Write(SaveJsonReader.Read(bought).Save!.Character!));

        JsonObject old = JsonNode.Parse(Write(Hero()))!.AsObject();
        old["schemaVersion"] = 28;
        SaveLoadResult v28 = SaveJsonReader.Read(old.ToJsonString());
        Assert.True(v28.IsValid, v28.Report.ToString());
        Assert.Null(v28.Save!.Character!.Home);
        Assert.Contains(SaveVersion.Current, Write(v28.Save.Character), StringComparison.Ordinal);
        SaveRefused(Mutated(r => r["schemaVersion"] = 28), P, ValidationCode.UnknownField);
    }

    [Fact]
    public void Sauvegarde_invalide_refusee_avec_son_chemin()
    {
        static JsonObject H(JsonObject r) => r["data"]!["character"]!["home"]!.AsObject();
        SaveRefused(Mutated(r => H(r)["stage"] = 9), P + ".stage", ValidationCode.OutOfRange);
        SaveRefused(Mutated(r => H(r).Remove("chest")), P + ".chest", ValidationCode.MissingField);
        SaveRefused(Mutated(r => H(r)["given"]!["pierre"] = 0), P + ".given.pierre", ValidationCode.OutOfRange);
        SaveRefused(Mutated(r => H(r)["placed"]![0]!["turn"] = 4), P + ".placed[0].turn", ValidationCode.OutOfRange);
        SaveRefused(Mutated(r => H(r)["placed"]![1]!["x"] = 128), P + ".placed[1].x", ValidationCode.OutOfRange);
        SaveRefused(Mutated(r => H(r)["placed"]![0]!["z"] = 5), P + ".placed[1]", ValidationCode.InvalidValue);   // hors de l'ordre (z, x)
        SaveRefused(Mutated(r => H(r)["placed"]![0]!["piece"] = "Table"), P + ".placed[0].piece", ValidationCode.InvalidValue);
        SaveRefused(Mutated(r => H(r)["chest"]![0]!["count"] = 0), P + ".chest[0].count", ValidationCode.OutOfRange);
        SaveRefused(Mutated(r => H(r)["vue"] = true), P + ".vue", ValidationCode.UnknownField);
        Assert.Throws<ArgumentException>(() => new SavedHome(0, placed: new[] { new SavedPiece("table", 1, 1, 0), new SavedPiece("lanterne", 1, 1, 0) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SavedPiece("table", 0, 0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SavedHome(9));
    }

    [Fact]
    public void Maison_gardee_hors_du_catalogue_refusee_avec_son_chemin()
    {
        HomeBook b = Sample.Value;
        void Off(SavedHome h, string path, ValidationCode code)
        {
            var report = new ValidationReport();
            HomeRules.Check(h, b, ItemsCatalog, Owned, report, P);
            Assert.Contains(report.Errors, e => e.Path == path && e.Code == code);
            Assert.NotNull(HomeRules.Mismatch(h, b, ItemsCatalog, Owned));
        }
        Assert.Null(HomeRules.Mismatch(Saved.WithPlaced(new[] { new SavedPiece("table", 0, 0, 0) }), b, ItemsCatalog, Owned));
        Assert.Null(HomeRules.Mismatch(Saved, b, ItemsCatalog, Owned));
        // Relecture indépendante de la PR #228 : une décoration de fête posée doit être possédée (portefeuille de sa fête).
        var report = new ValidationReport();
        HomeRules.Check(Saved, b, ItemsCatalog, SavedFeasts.Empty, report, P);
        Assert.Contains(report.Errors, e => e.Path == P + ".placed[0].piece" && e.Code == ValidationCode.InvalidValue);
        Assert.NotNull(HomeRules.Mismatch(Saved, b, ItemsCatalog, new SavedFeasts(new[] { new SavedFeast("festival.lanternes", owned: new[] { "F01_D02" }) })));
        Off(new SavedHome(3), P + ".stage", ValidationCode.OutOfRange);
        Off(new SavedHome(1, new[] { new KeyValuePair<string, long>("or", 1) }), P + ".given.or", ValidationCode.UnknownReference);
        Off(new SavedHome(0, new[] { new KeyValuePair<string, long>("or", 1_001) }), P + ".given.or", ValidationCode.OutOfRange);
        Off(new SavedHome(2, new[] { new KeyValuePair<string, long>("pierre", 1) }), P + ".given.pierre", ValidationCode.InvalidValue);
        Off(new SavedHome(0, placed: new[] { new SavedPiece("trone", 0, 0, 0) }), P + ".placed[0].piece", ValidationCode.UnknownReference);
        Off(new SavedHome(0, placed: new[] { new SavedPiece("table", 5, 0, 0) }), P + ".placed[0]", ValidationCode.OutOfRange);   // déborde (8 cases)
        Off(new SavedHome(0, placed: new[] { new SavedPiece("table", 0, 0, 0), new SavedPiece("lanterne", 3, 1, 0) }), P + ".placed[1]", ValidationCode.InvalidValue);
        Off(new SavedHome(0, chest: new[] { new ItemStack(Plank, 1), new ItemStack(Plank, 1), new ItemStack(Plank, 1) }), P + ".chest", ValidationCode.LimitExceeded);
        Off(new SavedHome(0, chest: new[] { new ItemStack(Plank, 21) }), P + ".chest[0].count", ValidationCode.OutOfRange);
        Off(new SavedHome(0, chest: new[] { new ItemStack("item.nulle_part", 1) }), P + ".chest[0].item", ValidationCode.UnknownReference);
    }

    // --- Dans la partie --------------------------------------------------------------------------------

    private static SavedFeasts Owned => new SavedFeasts(new[] { new SavedFeast("festival.lanternes", owned: new[] { Deco }) });

    [Fact]
    public void Rotation_refusee_si_elle_sort_du_sol_ou_chevauche()
    {
        HomeBook b = Sample.Value;
        SavedHome h = new SavedHome(0, placed: new[] { new SavedPiece("table", 0, 0, 0), new SavedPiece("table", 4, 4, 0) });   // 8 × 6 cases
        Assert.Equal(HomeRefusal.OutOfFloor, HomeRules.Rotate(h, b, 4, 4, out SavedHome same));   // 2 × 4 en (4, 4) : 4 + 4 > 6
        Assert.Same(h, same);
        h = h.WithPlaced(h.Placed.Append(new SavedPiece("lanterne", 1, 3, 0)));
        Assert.Equal(HomeRefusal.Overlap, HomeRules.Rotate(h, b, 0, 0, out same));   // 2 × 4 en (0, 0) : sur la lanterne (1, 3)
        Assert.Same(h, same);
        Assert.Equal(0, h.Placed.Single(p => p.X == 0 && p.Z == 0).Turn);
    }

    [Fact]
    public void Reprise_refusee_inventaire_plein_le_meuble_reste_pose()
    {
        WorldSession w = Inside(Hero().WithFeasts(Owned).WithHome(new SavedHome(0, placed: new[] { new SavedPiece("table", 0, 0, 0), new SavedPiece("lanterne", 7, 5, 0) })));
        while (w.Inventory.FreePlaces > 0) Assert.Equal(InventoryRefusal.None, w.Inventory.Add(Plank, 20));
        var before = w.Inventory.Stacks.ToList();
        Assert.Equal(HomeRefusal.InventoryFull, w.PickUpFurniture(1, 1));
        Assert.Equal(2, w.HomeState!.Placed.Count);   // rien n'est perdu
        Assert.Equal(before, w.Inventory.Stacks);
        Assert.Equal(0, w.Inventory.Count(Rubble));
        Assert.Equal(HomeRefusal.None, w.PickUpFurniture(7, 5));   // une décoration ne prend pas de place dans l'inventaire
        Assert.Single(w.HomeState!.Placed);
    }

    private static WorldSession Outside(SavedCharacter hero)
    {
        var w = new WorldSession(new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, hero), Zone(), ShippedData, Progression, ItemsCatalog, Book(Zone()));
        w.Step();
        w.UseHome(Sample.Value);
        return w;
    }

    private static WorldSession Inside(SavedCharacter hero)
    {
        ZoneDefinition chaumiere = Zones.Single(z => z.Id == "zone.chaumiere");
        var w = new WorldSession(new WorldSave("zone.chaumiere", 0, 0, new[] { PlayerCharacters.PlacesKey }, hero), chaumiere, ShippedData, Progression, ItemsCatalog);
        w.Step();
        w.UseHome(Sample.Value);
        return w;
    }

    [Fact]
    public void Acheter_et_agrandir_dans_la_zone_de_la_porte()
    {
        WorldSession poor = Outside(Hero(4_999));
        Assert.True(poor.DoorAwaitsHome("porte_chaumiere"));
        Assert.Equal(HomeRefusal.NotOwned, poor.GiveToHome("or", out _, out _));
        Assert.Equal(HomeRefusal.NotEnoughMoney, poor.BuyHome());
        Assert.Equal((4_999L, (SavedHome?)null), (poor.Character.Money, poor.HomeState));

        WorldSession w = Outside(Hero(5_500, new ItemStack(Plank, 3)));
        Assert.Equal(HomeRefusal.None, w.BuyHome());
        Assert.Equal(500, w.Character.Money);
        Assert.False(w.DoorAwaitsHome("porte_chaumiere"));
        Assert.Equal(HomeRefusal.AlreadyOwned, w.BuyHome());
        Assert.Equal(HomeRefusal.None, w.GiveToHome("or", out long given, out _));
        Assert.Equal((500L, 0L, 500L), (given, w.Character.Money, w.HomeState!.Of("or")));
        Assert.Equal(HomeRefusal.NothingToGive, w.GiveToHome("or", out _, out _));
        Assert.Equal(HomeRefusal.None, w.GiveToHome("planches", out given, out IReadOnlyList<RebuildStage> done));
        Assert.Equal((3L, 0, 0), (given, w.Inventory.Count(Plank), done.Count));
        Assert.Equal(HomeRefusal.UnknownNeed, w.GiveToHome("pierre", out _, out _));

        Assert.Equal(HomeRefusal.NotThere, Inside(Hero(10_000)).BuyHome());   // on achète devant la porte, pas dedans
    }

    [Fact]
    public void Meubles_et_coffre_dans_la_maison_refus_atomiques()
    {
        SavedCharacter hero = Hero(0, new ItemStack(Rubble, 2), new ItemStack(Plank, 20), new ItemStack(Plank, 5)).WithFeasts(Owned).WithHome(SavedHome.Bought);
        Assert.Equal(HomeRefusal.NotThere, Outside(hero).PlaceFurniture("table", 0, 0, 0));
        WorldSession w = Inside(hero);
        Assert.Equal(HomeRefusal.None, w.PlaceFurniture("table", 0, 0, 0));
        Assert.Equal(1, w.Inventory.Count(Rubble));   // un objet quitte l'inventaire
        Assert.Equal(HomeRefusal.Overlap, w.PlaceFurniture("table", 2, 1, 0));
        Assert.Equal(1, w.Inventory.Count(Rubble));   // refus : rien ne change
        Assert.Equal(HomeRefusal.None, w.PlaceFurniture("lanterne", 7, 5, 0));
        Assert.Equal(HomeRefusal.NotAvailable, w.PlaceFurniture("lanterne", 6, 5, 0));
        Assert.Equal(HomeRefusal.None, w.RotateFurniture(0, 0));
        Assert.Equal(HomeRefusal.None, w.MoveFurniture(0, 0, 2, 2, 0));
        Assert.Equal(HomeRefusal.None, w.PickUpFurniture(3, 3));
        Assert.Equal(2, w.Inventory.Count(Rubble));
        Assert.Single(w.HomeState!.Placed);
        Assert.Equal(HomeRefusal.None, w.PickUpFurniture(7, 5));   // la décoration reste au portefeuille
        Assert.Equal(2, w.Inventory.Count(Rubble));

        int planks = w.Inventory.Stacks.ToList().FindIndex(s => s.Item == Plank);
        Assert.Equal(HomeRefusal.NotEnough, w.ChestPut(planks, 21));
        Assert.Equal(HomeRefusal.None, w.ChestPut(planks, 20));
        Assert.Equal(HomeRefusal.None, w.ChestPut(w.Inventory.Stacks.ToList().FindIndex(s => s.Item == Plank), 5));
        Assert.Equal((0, 2), (w.Inventory.Count(Plank), w.HomeState!.Chest.Count));
        int rubble = w.Inventory.Stacks.ToList().FindIndex(s => s.Item == Rubble);
        Assert.Equal(HomeRefusal.ChestFull, w.ChestPut(rubble, 1));   // 2 places
        Assert.Equal(2, w.Inventory.Count(Rubble));
        Assert.Equal(HomeRefusal.None, w.ChestTake(0, 20));
        Assert.Equal((20, 1), (w.Inventory.Count(Plank), w.HomeState!.Chest.Count));

        // La maison se garde dans la sauvegarde avec l'inventaire.
        SaveLoadResult back = SaveJsonReader.Read(SaveJsonReader.Write(w.Save()));
        Assert.True(back.IsValid, back.Report.ToString());
        Assert.Equal(5, back.Save!.Character!.Home!.Chest.Single().Count);

        WorldSession stranger = Inside(Hero(0, new ItemStack(Rubble, 1)));
        Assert.Equal(HomeRefusal.NotOwned, stranger.PlaceFurniture("table", 0, 0, 0));
        Assert.Throws<ArgumentException>(() => Inside(Hero().WithHome(new SavedHome(0, placed: new[] { new SavedPiece("trone", 0, 0, 0) }))));
    }
}
