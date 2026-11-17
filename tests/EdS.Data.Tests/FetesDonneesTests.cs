using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Feasts;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Domain.Numerics;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Les 12 fêtes d'Orsédia, sous-lot F1 (données et schémas ; décisions de Damien du 17 novembre 2026) : les 108 références du dossier des
/// fêtes en données (<c>kind: "feasts"</c>, version 1), leurs textes en français, le validateur unique avec des erreurs à chemin précis :
/// une monnaie par fête, récompense Rare, costume Commun, prix et gains sous le plafond, activités liées à un invité ou à un concours de
/// l'histoire, marchand de fête de passage.
/// </summary>
[Trait("Category", "World")]
public class FetesDonneesTests
{
    private static readonly Lazy<StoryBook> Story = new(() => Book(Zone()));

    private static JsonObject Json() => JsonNode.Parse(File.ReadAllText(FeastJsonReader.PathIn(DataDirectory)))!.AsObject();

    private static FeastLoadResult Read(JsonObject root) => FeastJsonReader.Read(root.ToJsonString(), Story.Value, Texts);

    private static FeastLoadResult Mutated(Action<JsonObject> mutate)
    {
        JsonObject root = Json();
        mutate(root);
        return Read(root);
    }

    private static JsonObject Feast(JsonObject root, int index) => root["data"]!["feasts"]![index]!.AsObject();

    private static JsonObject Ref(JsonObject root, int feast, int index) => Feast(root, feast)["refs"]![index]!.AsObject();

    private static void Refused(FeastLoadResult r, string path, ValidationCode code)
    {
        Assert.False(r.IsValid);
        Assert.True(r.Report.Errors.Any(e => e.Path == path && e.Code == code), $"{path} ({code}) attendu ; {r.Report}");
    }

    private static FeastBook Shipped => FeastJsonReader.ReadFile(FeastJsonReader.PathIn(DataDirectory), Story.Value, Texts).Book!;

    [Fact]
    public void Les_108_references_sont_en_donnees_valides()
    {
        FeastLoadResult r = FeastJsonReader.ReadFile(FeastJsonReader.PathIn(DataDirectory), Story.Value, Texts);
        Assert.True(r.IsValid, r.Report.ToString());
        FeastBook b = r.Book!;
        Assert.Equal((1, FeastValidator.CurrentSchemaVersion, "0.1.0-provisoire"), (b.SchemaVersion, FeastValidator.CurrentSchemaVersion, b.ContentVersion));
        Assert.Equal(12, b.Feasts.Count);
        Assert.Equal(108, b.Refs.Count());
        Assert.Equal(Enumerable.Range(1, 12), b.Feasts.Select(f => f.Number));
        // Chaque fête : 5 décorations, un costume, un plat, une monnaie (la sienne), une récompense ; sa fête de l'histoire tombe à son mois.
        foreach (Feast f in b.Feasts)
        {
            Assert.Equal(5, f.Decorations.Count());
            Assert.Equal($"F{f.Number:00}_M01", f.Currency.Code);
            Assert.Equal($"F{f.Number:00}_R01", f.Reward.Code);
            Assert.Equal(f.Number, Story.Value.Festival(f.Festival).Month);
            Assert.StartsWith("title.fete.", f.TitleId, StringComparison.Ordinal);
            Assert.True(Texts.Has(f.Motto) && Texts.Has(f.TitleName));
            Assert.All(f.Refs, x => Assert.True(Texts.Has(x.Name), x.Code));
        }
        Assert.Equal(12, b.Feasts.Select(f => f.Currency.Code).Distinct().Count());
        Assert.Equal(12, b.Feasts.Select(f => f.TitleId).Distinct().Count());
        // Décisions de Damien du 17 novembre 2026 : récompenses Rares, costumes Communs ; la monnaie ne s'achète pas et n'a pas de rareté.
        Assert.All(b.Refs.Where(x => x.Category == FeastCategory.Reward), x => Assert.Equal(ItemRarity.Rare, x.Rarity));
        Assert.All(b.Refs.Where(x => x.Category == FeastCategory.Costume), x => Assert.Equal(ItemRarity.Common, x.Rarity));
        Assert.All(b.Refs.Where(x => x.Category == FeastCategory.Currency), x => Assert.Null(x.Price ?? (object?)x.Rarity));
        Assert.All(b.Refs.Where(x => x.Category != FeastCategory.Currency), x => Assert.InRange(x.Price!.Value, 1, b.WalletCap));
        // Icônes : plat, monnaie, récompense (36 en tout).
        Assert.Equal(36, b.Refs.Count(x => x.Icon != null));
        Assert.All(b.Refs.Where(x => x.Icon != null), x => Assert.Contains(x.Category, new[] { FeastCategory.Dish, FeastCategory.Currency, FeastCategory.Reward }));
        // Cotes du dossier (les cotes écrites font foi) : la Lanterne des vœux, l'Estrade des musiciens.
        FeastRef lantern = b.Ref("F01_D01")!;
        Assert.Equal((Fixed.FromRatio(32, 100), Fixed.FromRatio(32, 100), Fixed.FromRatio(64, 100)), (lantern.Size.X, lantern.Size.Y, lantern.Size.Z));
        Assert.Equal("Lanterne des vœux", Texts.Get(lantern.Name));
        Assert.Equal(Fixed.FromRatio(36, 10), b.Ref("F06_D02")!.Size.X);
        Assert.Equal("Graine de Griffon", Texts.Get(b.Ref("F04_M01")!.Name));
        Assert.Equal("Icones/F04_M01_graine_de_griffon_icone.png", b.Ref("F04_M01")!.Icon);
        // Semailles, Moissons et Veillée ont des activités (concours de l'histoire et invités) ; les neuf autres sont du calendrier seulement.
        Assert.Equal(new[] { "festival.foire", "festival.moissons", "festival.veillee" },
            b.Feasts.Where(f => f.Activities.Count > 0).Select(f => f.Festival).OrderBy(x => x, StringComparer.Ordinal));
        Assert.All(b.Feasts.Where(f => f.Activities.Count == 0), f => Assert.True(Story.Value.Festival(f.Festival).CalendarOnly));
        Feast semailles = b.Feast("festival.foire");
        Assert.Equal("festival.foire.patisserie", semailles.Activity("patisserie")!.Contest);
        Assert.Equal("npc.lison", semailles.Activity("course")!.Host);
        Assert.Equal(3, semailles.RewardAfter);
        Assert.Equal("npc.colporteur", b.Merchant);
        Assert.Equal((999, 20), (b.WalletCap, b.MaxDishes));
        Assert.Same(semailles, b.ByTitle("title.fete.semailles"));
        Assert.Null(b.Of("festival.marche"));
        Assert.Null(b.Ref("F13_D01"));
        Assert.Throws<ArgumentException>(() => b.Feast("festival.marche"));
        // Le schéma JSON (documentation) accepte le fichier livré.
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "feasts.schema.json"))
            .IsValid(JsonDocument.Parse(File.ReadAllText(FeastJsonReader.PathIn(DataDirectory))).RootElement));
    }

    [Fact]
    public void Les_references_suivent_le_catalogue_du_dossier()
    {
        // Le catalogue CSV du dossier des fêtes (4 trimestres) est la source : codes, noms, cotes et icônes recopiés tels quels.
        string? lab = Environment.GetEnvironmentVariable("EDS_FETES_DIR");
        if (lab is null || !Directory.Exists(lab)) return;   // le dossier des fêtes n'est pas dans le dépôt : vérifié quand il est fourni
        FeastBook b = Shipped;
        var rows = Directory.GetFiles(lab, "Catalogue_Fetes.csv", SearchOption.AllDirectories).SelectMany(f => File.ReadAllLines(f).Skip(1))
            .Where(l => l.Length > 0).Select(l => l.Split(';')).ToList();
        Assert.Equal(108, rows.Count);
        foreach (string[] c in rows)
        {
            FeastRef x = b.Ref(c[0])!;
            Assert.Equal(c[4].Replace('’', '\''), Texts.Get(x.Name));
            Assert.Equal(c[8], x.Image);
            Assert.Equal(c[9].Length == 0 ? null : c[9], x.Icon);
        }
    }

    [Fact]
    public void Lecture_deterministe()
    {
        FeastBook a = Shipped, b = Shipped;
        Assert.Equal(a.Refs.Select(r => (r.Code, r.Price, r.Size.X.Raw, r.Size.Y.Raw, r.Size.Z.Raw)), b.Refs.Select(r => (r.Code, r.Price, r.Size.X.Raw, r.Size.Y.Raw, r.Size.Z.Raw)));
        Assert.Equal(a.Feasts.SelectMany(f => f.Activities).Select(x => (x.Id, x.Coins, x.Host, x.Contest)), b.Feasts.SelectMany(f => f.Activities).Select(x => (x.Id, x.Coins, x.Host, x.Contest)));
    }

    [Fact]
    public void Contenu_invalide_refuse_avec_son_chemin()
    {
        const string f0 = "$.data.feasts[0]", f3 = "$.data.feasts[3]";
        Refused(Mutated(r => r["schemaVersion"] = 2), "$.schemaVersion", ValidationCode.UnsupportedSchemaVersion);
        Refused(Mutated(r => r["kind"] = "seasons"), "$.kind", ValidationCode.WrongKind);
        Refused(Mutated(r => r["data"]!["inconnu"] = 1), "$.data.inconnu", ValidationCode.UnknownField);
        Refused(Mutated(r => Ref(r, 0, 0)["poids"] = 1), f0 + ".refs[0].poids", ValidationCode.UnknownField);
        Refused(Mutated(r => r["data"]!["feasts"]!.AsArray().RemoveAt(11)), "$.data.feasts", ValidationCode.OutOfRange);
        Refused(Mutated(r => Feast(r, 1)["number"] = 1), "$.data.feasts[1].number", ValidationCode.DuplicateId);
        Refused(Mutated(r => Feast(r, 0)["number"] = 13), f0 + ".number", ValidationCode.OutOfRange);
        Refused(Mutated(r => Feast(r, 0)["festival"] = "festival.inconnue"), f0 + ".festival", ValidationCode.UnknownReference);
        Refused(Mutated(r => Feast(r, 1)["festival"] = "festival.lanternes"), "$.data.feasts[1].festival", ValidationCode.DuplicateId);
        Refused(Mutated(r => Feast(r, 0)["festival"] = "festival.marche"), f0 + ".festival", ValidationCode.InvalidValue);   // fête de la semaine
        Refused(Mutated(r => { Feast(r, 0)["festival"] = "festival.givre"; Feast(r, 11)["festival"] = "festival.lanternes"; }), f0 + ".festival", ValidationCode.InvalidValue);   // mauvais mois
        Refused(Mutated(r => Feast(r, 0)["motto"] = "text.fetes.absent"), f0 + ".motto", ValidationCode.UnknownReference);
        Refused(Mutated(r => Feast(r, 0)["title"]!["id"] = "title.lanternes"), f0 + ".title.id", ValidationCode.InvalidValue);
        Refused(Mutated(r => Feast(r, 1)["title"]!["id"] = "title.fete.lanternes"), "$.data.feasts[1].title.id", ValidationCode.DuplicateId);
        Refused(Mutated(r => Feast(r, 0).Remove("title")), f0 + ".title", ValidationCode.MissingField);
        // Références.
        Refused(Mutated(r => Feast(r, 0)["refs"]!.AsArray().RemoveAt(0)), f0 + ".refs", ValidationCode.OutOfRange);
        Refused(Mutated(r => Ref(r, 0, 0)["code"] = "F01_X01"), f0 + ".refs[0].code", ValidationCode.InvalidValue);
        Refused(Mutated(r => Ref(r, 0, 0)["code"] = "F02_D01"), f0 + ".refs[0].code", ValidationCode.InvalidValue);   // code d'une autre fête
        Refused(Mutated(r => Ref(r, 0, 1)["code"] = "F01_D01"), f0 + ".refs[1].code", ValidationCode.DuplicateId);
        Refused(Mutated(r => Ref(r, 0, 0)["code"] = "F01_D06"), f0 + ".refs[0].code", ValidationCode.OutOfRange);
        Refused(Mutated(r => Ref(r, 0, 5)["code"] = "F01_C02"), f0 + ".refs[5].code", ValidationCode.OutOfRange);
        Refused(Mutated(r => Ref(r, 0, 0)["category"] = "costume"), f0 + ".refs[0].code", ValidationCode.InvalidValue);   // lettre D, catégorie costume
        Refused(Mutated(r => Ref(r, 0, 0)["category"] = "statue"), f0 + ".refs[0].category", ValidationCode.UnknownReference);
        Refused(Mutated(r => Ref(r, 0, 0)["name"] = "text.fetes.absent"), f0 + ".refs[0].name", ValidationCode.UnknownReference);
        Refused(Mutated(r => Ref(r, 0, 0)["image"] = "Objets/F02_D01_banniere.png"), f0 + ".refs[0].image", ValidationCode.InvalidValue);
        Refused(Mutated(r => Ref(r, 0, 0)["image"] = "../secret.png"), f0 + ".refs[0].image", ValidationCode.InvalidValue);
        Refused(Mutated(r => Ref(r, 0, 0)["icon"] = "Icones/F01_D01_x_icone.png"), f0 + ".refs[0].icon", ValidationCode.InvalidValue);   // une décoration n'a pas d'icône
        Refused(Mutated(r => Ref(r, 0, 6).Remove("icon")), f0 + ".refs[6].icon", ValidationCode.MissingField);   // le plat en a une
        Refused(Mutated(r => Ref(r, 0, 8)["rarity"] = "common"), f0 + ".refs[8].rarity", ValidationCode.InvalidValue);   // récompense Rare
        Refused(Mutated(r => Ref(r, 0, 5)["rarity"] = "rare"), f0 + ".refs[5].rarity", ValidationCode.InvalidValue);   // costume Commun
        Refused(Mutated(r => Ref(r, 0, 0)["rarity"] = "epic"), f0 + ".refs[0].rarity", ValidationCode.UnknownReference);
        Refused(Mutated(r => Ref(r, 0, 7)["rarity"] = "common"), f0 + ".refs[7].rarity", ValidationCode.InvalidValue);   // la monnaie n'en a pas
        Refused(Mutated(r => Ref(r, 0, 7)["price"] = 1), f0 + ".refs[7].price", ValidationCode.InvalidValue);   // la monnaie ne s'achète pas
        Refused(Mutated(r => Ref(r, 0, 0).Remove("price")), f0 + ".refs[0].price", ValidationCode.MissingField);
        Refused(Mutated(r => Ref(r, 0, 0)["size"]!["x"] = 0), f0 + ".refs[0].size.x", ValidationCode.OutOfRange);
        Refused(Mutated(r => Ref(r, 0, 0)["size"]!["z"] = -1), f0 + ".refs[0].size.z", ValidationCode.OutOfRange);
        Refused(Mutated(r => Ref(r, 0, 0)["size"]!["y"] = 20.0001), f0 + ".refs[0].size.y", ValidationCode.OutOfRange);
        Refused(Mutated(r => Ref(r, 0, 0)["size"]!.AsObject().Remove("y")), f0 + ".refs[0].size.y", ValidationCode.MissingField);
        Refused(Mutated(r => { Ref(r, 0, 0)["code"] = "F01_C01"; Ref(r, 0, 0)["category"] = "costume"; Ref(r, 0, 5)["code"] = "F01_D01"; Ref(r, 0, 5)["category"] = "decoration";
            Ref(r, 0, 0).Remove("price"); }), f0 + ".refs[0].price", ValidationCode.MissingField);
        // Activités, marchand, plafond.
        Refused(Mutated(r => Feast(r, 0)["activities"]!.AsArray().Add(new JsonObject { ["id"] = "x", ["name"] = "text.fetes.f01.titre", ["coins"] = 5, ["host"] = "npc.lison" })),
            f0 + ".activities", ValidationCode.InvalidValue);   // fête du calendrier seulement
        Refused(Mutated(r => Feast(r, 3)["activities"] = new JsonArray()), f3 + ".activities", ValidationCode.InvalidValue);
        Refused(Mutated(r => Feast(r, 3)["activities"]![2]!["host"] = "npc.maelle"), f3 + ".activities[2].host", ValidationCode.InvalidValue);   // pas invitée
        Refused(Mutated(r => Feast(r, 3)["activities"]![2]!["host"] = "npc.personne"), f3 + ".activities[2].host", ValidationCode.UnknownReference);
        Refused(Mutated(r => Feast(r, 3)["activities"]![3]!["host"] = "npc.lison"), f3 + ".activities[3].host", ValidationCode.DuplicateId);
        Refused(Mutated(r => Feast(r, 3)["activities"]![0]!["contest"] = "festival.foire.course"), f3 + ".activities[0].contest", ValidationCode.UnknownReference);
        Refused(Mutated(r => Feast(r, 3)["activities"]![0]!["contest"] = "festival.moissons"), f3 + ".activities[0].contest", ValidationCode.InvalidValue);
        Refused(Mutated(r => Feast(r, 3)["activities"]![1]!["contest"] = "festival.foire"), f3 + ".activities[1].contest", ValidationCode.DuplicateId);
        Refused(Mutated(r => Feast(r, 3)["activities"]![0]!["host"] = "npc.lison"), f3 + ".activities[0]", ValidationCode.InvalidValue);   // les deux
        Refused(Mutated(r => Feast(r, 3)["activities"]![2]!.AsObject().Remove("host")), f3 + ".activities[2]", ValidationCode.InvalidValue);   // aucun
        Refused(Mutated(r => Feast(r, 3)["activities"]![1]!["id"] = "concours"), f3 + ".activities[1].id", ValidationCode.DuplicateId);
        Refused(Mutated(r => Feast(r, 3)["activities"]![1]!["id"] = "Concours"), f3 + ".activities[1].id", ValidationCode.InvalidValue);
        Refused(Mutated(r => Feast(r, 3)["activities"]![1]!["coins"] = 0), f3 + ".activities[1].coins", ValidationCode.OutOfRange);
        Refused(Mutated(r => Feast(r, 3)["activities"]![1]!["coins"] = 1000), f3 + ".activities[1].coins", ValidationCode.OutOfRange);   // au-delà du plafond (999)
        Refused(Mutated(r => Feast(r, 3)["rewardAfter"] = 5), f3 + ".rewardAfter", ValidationCode.OutOfRange);
        Refused(Mutated(r => Feast(r, 3)["rewardAfter"] = 0), f3 + ".rewardAfter", ValidationCode.OutOfRange);
        Refused(Mutated(r => Feast(r, 3).Remove("rewardAfter")), f3 + ".rewardAfter", ValidationCode.MissingField);
        Refused(Mutated(r => Feast(r, 0)["rewardAfter"] = 1), f0 + ".rewardAfter", ValidationCode.InvalidValue);
        Refused(Mutated(r => r["data"]!["merchant"] = "npc.lison"), "$.data.merchant", ValidationCode.InvalidValue);   // pas de passage
        Refused(Mutated(r => r["data"]!["merchant"] = "npc.personne"), "$.data.merchant", ValidationCode.UnknownReference);
        Refused(Mutated(r => r["data"]!["wallet"]!["cap"] = 0), "$.data.wallet.cap", ValidationCode.OutOfRange);
        Refused(Mutated(r => r["data"]!["wallet"]!["cap"] = FeastValidator.MaxWalletCap + 1), "$.data.wallet.cap", ValidationCode.OutOfRange);
        Refused(Mutated(r => r["data"]!["maxDishes"] = 100), "$.data.maxDishes", ValidationCode.OutOfRange);
        Refused(Mutated(r => r["data"]!["wallet"]!["cap"] = 89), "$.data.feasts[0].refs[1].price", ValidationCode.OutOfRange);   // l'arche (90) au-delà du plafond
    }

    [Fact]
    public void Frontieres_numeriques_acceptees()
    {
        // Plafond le plus haut, prix égal au plafond, cote la plus petite et la plus grande, plats de 1 à 99.
        FeastLoadResult r = Mutated(root =>
        {
            root["data"]!["wallet"]!["cap"] = FeastValidator.MaxWalletCap;
            root["data"]!["maxDishes"] = FeastValidator.MaxDishesCap;
            Ref(root, 0, 0)["price"] = FeastValidator.MaxWalletCap;
            Ref(root, 0, 0)["size"]!["x"] = 0.0001;
            Ref(root, 0, 0)["size"]!["z"] = 20;
            Feast(root, 3)["activities"]![2]!["coins"] = FeastValidator.MaxWalletCap;
        });
        Assert.True(r.IsValid, r.Report.ToString());
        Assert.Equal(Fixed.Epsilon, r.Book!.Ref("F01_D01")!.Size.X);
        Assert.True(Mutated(root => { root["data"]!["wallet"]!["cap"] = 150; root["data"]!["maxDishes"] = 1; }).IsValid);   // la plus chère des références : 150
    }
}
