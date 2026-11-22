using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using EdS.Data.Feasts;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Domain.Time;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Les 12 fêtes d'Orsédia, sous-lot F2 (règles et sauvegarde ; décisions de Damien du 17 novembre 2026) : sauvegarde v27 (<c>character.feasts</c> :
/// portefeuille par fête, activités de l'édition, récompenses, achats, titre affiché), v26 encore lue ; règles pures du portefeuille
/// (<see cref="FeastRules"/>) vérifiées et atomiques : plafond, une récompense par édition, rattrapage à prix plein l'année d'après, titre de
/// fête ; titre de fête parmi les titres affichables (un seul affiché).
/// </summary>
[Trait("Category", "World")]
public class FetesSauvegardeTests
{
    private const string Semailles = "festival.foire", Veillee = "festival.veillee";
    private static readonly Lazy<StoryBook> Story = new(() => Book(Zone()));
    private static readonly Lazy<FeastBook> Feasts = new(() => FeastJsonReader.ReadFile(FeastJsonReader.PathIn(DataDirectory), Story.Value, Texts).Book!);

    private static SavedCharacter Char(SavedFeasts? feasts = null, SavedCards? cards = null) =>
        V11TestData.Hero().WithCards(cards).WithFeasts(feasts ?? SavedFeasts.Empty);

    private static string Write(SavedCharacter c) => SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), c));

    private static readonly SavedFeasts Sample = new(new[]
    {
        new SavedFeast(Semailles, 120, 2027, new Dictionary<string, long> { ["cible"] = 740_000, ["concours"] = 739_999 }, 1, 1, new long[] { 2026, 2027 },
            new[] { "F04_C01", "F04_D01" }, 3),
        new SavedFeast(Veillee, 7),
    }, Semailles);

    private static SaveLoadResult Mutated(Action<JsonObject> mutate, SavedCharacter? hero = null)
    {
        JsonObject root = JsonNode.Parse(Write(hero ?? Char(Sample)))!.AsObject();
        mutate(root);
        return SaveJsonReader.Read(root.ToJsonString());
    }

    private static JsonObject Wallet(JsonObject root, string festival = Semailles) => root["data"]!["character"]!["feasts"]!["wallets"]![festival]!.AsObject();

    private static void Refused(SaveLoadResult r, string path, ValidationCode code)
    {
        Assert.False(r.IsValid);
        Assert.True(r.Report.Errors.Any(e => e.Path == path && e.Code == code), $"{path} ({code}) attendu ; {r.Report}");
    }

    private const string P = "$.data.character.feasts", W = "$.data.character.feasts.wallets.festival.foire";

    [Fact]
    public void Sauvegarde_v27_aller_retour_et_v26_encore_lue()
    {
        Assert.Equal((33, 27), (SaveValidator.CurrentSchemaVersion, SaveValidator.FeastsSince));
        string json = Write(Char(Sample));
        Assert.Contains(SaveVersion.Current, json, StringComparison.Ordinal);
        Assert.Contains("\"feasts\": {\"shown\": \"festival.foire\", \"wallets\": {\"festival.foire\": {\"coins\": 120, \"edition\": 2027, \"done\": {\"cible\": 740000, \"concours\": 739999}, "
            + "\"wins\": 1, \"bought\": 1, \"editions\": [2026, 2027], \"owned\": [\"F04_C01\", \"F04_D01\"], \"dishes\": 3}, \"festival.veillee\": {\"coins\": 7, \"wins\": 0, \"bought\": 0, "
            + "\"editions\": [], \"owned\": [], \"dishes\": 0}}},", json, StringComparison.Ordinal);
        SaveLoadResult back = SaveJsonReader.Read(json);
        Assert.True(back.IsValid, back.Report.ToString());
        Assert.Equal(Sample, back.Save!.Character!.Feasts);
        Assert.Equal(json, Write(back.Save.Character));   // forme canonique stable
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json"))
            .IsValid(System.Text.Json.JsonDocument.Parse(json).RootElement));   // le schéma JSON (documentation) accepte la v27
        // Sans fête : rien d'écrit.
        Assert.DoesNotContain("\"feasts\"", Write(Char()), StringComparison.Ordinal);
        // Migration : une sauvegarde v26 (sans fêtes) est lue telle quelle, sans fête, puis réécrite en v27.
        JsonObject old = JsonNode.Parse(Write(Char()))!.AsObject();
        old["schemaVersion"] = 26;
        SaveLoadResult v26 = SaveJsonReader.Read(old.ToJsonString());
        Assert.True(v26.IsValid, v26.Report.ToString());
        Assert.True(v26.Save!.Character!.Feasts.IsEmpty);
        Assert.Contains(SaveVersion.Current, Write(v26.Save.Character), StringComparison.Ordinal);
        // Une v26 n'a pas de fêtes.
        Refused(Mutated(r => r["schemaVersion"] = 26), P, ValidationCode.UnknownField);
    }

    [Fact]
    public void Sauvegarde_hors_format_refusee_avec_son_chemin()
    {
        Refused(Mutated(r => Wallet(r)["coins"] = SavedFeast.MaxCoins + 1), W + ".coins", ValidationCode.OutOfRange);
        Refused(Mutated(r => Wallet(r)["coins"] = -1), W + ".coins", ValidationCode.OutOfRange);
        Refused(Mutated(r => Wallet(r).Remove("edition")), W + ".edition", ValidationCode.InvalidValue);
        Refused(Mutated(r => Wallet(r).Remove("done")), W + ".done", ValidationCode.InvalidValue);
        Refused(Mutated(r => Wallet(r)["done"] = new JsonObject()), W + ".done", ValidationCode.OutOfRange);
        Refused(Mutated(r => Wallet(r)["done"]!["Cible!"] = 1), W + ".done.Cible!", ValidationCode.InvalidValue);
        Refused(Mutated(r => Wallet(r)["done"]!["cible"] = SavedFeast.MaxDayKey + 1), W + ".done.cible", ValidationCode.OutOfRange);
        Refused(Mutated(r => Wallet(r)["edition"] = -1), W + ".edition", ValidationCode.OutOfRange);
        Refused(Mutated(r => Wallet(r)["editions"] = new JsonArray(2027, 2026)), W + ".editions", ValidationCode.InvalidValue);
        Refused(Mutated(r => Wallet(r)["editions"] = new JsonArray(2025, 2026, 2027)), W + ".editions", ValidationCode.LimitExceeded);
        Refused(Mutated(r => { Wallet(r)["bought"] = 0; }), W + ".editions", ValidationCode.InvalidValue);   // 2 éditions pour une seule récompense
        Refused(Mutated(r => Wallet(r)["owned"] = new JsonArray("F04_D01", "F04_C01")), W + ".owned[1]", ValidationCode.InvalidValue);   // ordre
        Refused(Mutated(r => Wallet(r)["owned"] = new JsonArray("F04_C01", "F05_D01")), W + ".owned[1]", ValidationCode.InvalidValue);   // deux fêtes
        Refused(Mutated(r => Wallet(r)["owned"] = new JsonArray("F04_M01")), W + ".owned[0]", ValidationCode.InvalidValue);   // la monnaie ne s'achète pas
        Refused(Mutated(r => Wallet(r)["owned"] = new JsonArray("F04_R01")), W + ".owned[0]", ValidationCode.InvalidValue);
        // Revue Codex de la PR #215 : un code trop court, en première, deuxième ou dernière place : refusé avec son chemin, sans exception.
        Refused(Mutated(r => Wallet(r)["owned"] = new JsonArray("F", "F04_D01")), W + ".owned[0]", ValidationCode.InvalidValue);
        Refused(Mutated(r => Wallet(r)["owned"] = new JsonArray("F04_C01", "F", "F04_D01")), W + ".owned[1]", ValidationCode.InvalidValue);
        Refused(Mutated(r => Wallet(r)["owned"] = new JsonArray("F04_C01", "F04_D01", "")), W + ".owned[2]", ValidationCode.InvalidValue);
        Refused(Mutated(r => Wallet(r)["owned"] = new JsonArray("F04_C01", "F04_D01", "F04")), W + ".owned[2]", ValidationCode.InvalidValue);
        Refused(Mutated(r => Wallet(r)["dishes"] = SavedFeast.MaxDishes + 1), W + ".dishes", ValidationCode.OutOfRange);
        Refused(Mutated(r => Wallet(r)["wins"] = SavedFeast.MaxCount + 1), W + ".wins", ValidationCode.OutOfRange);
        Refused(Mutated(r => Wallet(r)["poids"] = 1), W + ".poids", ValidationCode.UnknownField);
        Refused(Mutated(r => Wallet(r, Veillee)["coins"] = 0), "$.data.character.feasts.wallets.festival.veillee", ValidationCode.InvalidValue);   // vide
        Refused(Mutated(r => r["data"]!["character"]!["feasts"]!["wallets"] = new JsonObject { ["fete.x"] = JsonNode.Parse("{\"coins\": 1, \"wins\": 0, \"bought\": 0, \"editions\": [], \"owned\": [], \"dishes\": 0}") }),
            "$.data.character.feasts.wallets.fete.x", ValidationCode.InvalidValue);
        Refused(Mutated(r => { var f = r["data"]!["character"]!["feasts"]!.AsObject(); f.Remove("shown"); f["wallets"] = new JsonObject(); }), P, ValidationCode.InvalidValue);
        Refused(Mutated(r => r["data"]!["character"]!["feasts"]!["shown"] = Veillee), P + ".shown", ValidationCode.InvalidValue);   // pas gagnée
        Refused(Mutated(r => r["data"]!["character"]!["feasts"]!["shown"] = null), P + ".shown", ValidationCode.InvalidValue);
        // Un seul titre affiché : pas avec « As des Trois Feux ».
        SavedCards ace = new SavedCards(1, 10, SavedCards.VictoriesForReward, true, 0, null, 0, null, shown: true);
        Assert.True(SaveJsonReader.Read(Write(Char(Sample.WithShown(null), ace))).IsValid);
        Refused(Mutated(r => r["data"]!["character"]!["feasts"]!["shown"] = Semailles, Char(Sample.WithShown(null), ace)), P + ".shown", ValidationCode.InvalidValue);
        // Construction : mêmes bornes.
        Assert.Throws<ArgumentOutOfRangeException>(() => new SavedFeast(Semailles, SavedFeast.MaxCoins + 1));
        Assert.Throws<ArgumentException>(() => new SavedFeast(Semailles, 0, 2027));   // édition sans activité
        Assert.Throws<ArgumentException>(() => new SavedFeast(Semailles, 0, null, null, 0, 0, new long[] { 2027 }));   // édition sans récompense
        Assert.Throws<ArgumentException>(() => new SavedFeasts(new[] { new SavedFeast(Semailles, 1) }, Semailles));   // titre non gagné
        Assert.Throws<ArgumentException>(() => new SavedFeast("fete", 1));
        Assert.True(new SavedFeasts(new[] { new SavedFeast(Semailles) }).IsEmpty);   // une entrée vide n'est pas gardée
    }

    [Fact]
    public void Activites_plafond_et_recompense_annuelle()
    {
        FeastBook b = Feasts.Value;
        SavedFeasts s = SavedFeasts.Empty;
        // Concours : 30 pièces, une fois par édition.
        Assert.Equal(FeastRefusal.None, FeastRules.Earn(s, b, Semailles, "concours", 2027, 740_000, out s, out FeastGain g));
        Assert.Equal((30L, false, false), (g.Gained, g.Capped, g.Won));
        Assert.Equal(FeastRefusal.AlreadyDone, FeastRules.Earn(s, b, Semailles, "concours", 2027, 740_001, out SavedFeasts same, out _));
        Assert.Same(s, same);
        // Activité d'un invité : une fois par jour de fête.
        Assert.Equal(FeastRefusal.None, FeastRules.Earn(s, b, Semailles, "cible", 2027, 740_000, out s, out g));
        Assert.Equal(FeastRefusal.AlreadyDone, FeastRules.Earn(s, b, Semailles, "cible", 2027, 740_000, out _, out _));
        Assert.Equal(FeastRefusal.None, FeastRules.Earn(s, b, Semailles, "cible", 2027, 740_001, out s, out g));
        Assert.False(g.Won);   // deux activités différentes : il en faut 3
        Assert.Equal(50, s.Of(Semailles).Coins);
        // Troisième activité différente : la fête est gagnée (récompense de l'édition 2027, titre).
        Assert.Equal(FeastRefusal.None, FeastRules.Earn(s, b, Semailles, "course", 2027, 740_001, out s, out g));
        Assert.True(g.Won && g.Title);
        Assert.Equal((1L, 0L), (s.Of(Semailles).Wins, s.Of(Semailles).Bought));
        Assert.Equal(new long[] { 2027 }, s.Of(Semailles).Editions);
        Assert.Equal(new[] { "title.fete.semailles" }, FeastRules.Titles(s, b));
        // Plus de récompense dans la même édition.
        Assert.Equal(FeastRefusal.None, FeastRules.Earn(s, b, Semailles, "patisserie", 2027, 740_002, out s, out g));
        Assert.False(g.Won);
        // Nouvelle édition : les activités repartent de zéro, la monnaie reste (gardée d'une année sur l'autre).
        long before = s.Of(Semailles).Coins;
        Assert.Equal(FeastRefusal.None, FeastRules.Earn(s, b, Semailles, "concours", 2028, 740_365, out s, out g));
        Assert.Equal((2028L, 1, before + 30), (s.Of(Semailles).Edition!.Value, s.Of(Semailles).Done.Count, s.Of(Semailles).Coins));
        // Refus : fête sans contenu, activité inconnue, jour hors bornes.
        Assert.Equal(FeastRefusal.NoFeast, FeastRules.Earn(s, b, "festival.marche", "concours", 2027, 1, out _, out _));
        Assert.Equal(FeastRefusal.UnknownActivity, FeastRules.Earn(s, b, Semailles, "bal", 2027, 1, out _, out _));
        Assert.Equal(FeastRefusal.NotNow, FeastRules.Earn(s, b, Semailles, "cible", -1, 1, out _, out _));
        Assert.Equal(FeastRefusal.NotNow, FeastRules.Earn(s, b, Semailles, "cible", 1, SavedFeast.MaxDayKey + 1, out _, out _));
    }

    [Fact]
    public void Plafond_du_portefeuille_aux_frontieres()
    {
        FeastBook b = Feasts.Value;
        int cap = b.WalletCap;
        var s = new SavedFeasts(new[] { new SavedFeast(Semailles, cap - 1) });
        Assert.Equal(FeastRefusal.None, FeastRules.Earn(s, b, Semailles, "cible", 1, 10, out s, out FeastGain g));
        Assert.Equal((1L, true, (long)cap), (g.Gained, g.Capped, s.Of(Semailles).Coins));   // le reste est retenu, et dit
        Assert.Equal(FeastRefusal.None, FeastRules.Earn(s, b, Semailles, "course", 1, 10, out s, out g));
        Assert.Equal((0L, true, (long)cap, 2), (g.Gained, g.Capped, s.Of(Semailles).Coins, s.Of(Semailles).Done.Count));   // l'activité compte quand même
        // Monnaie technique au-delà du plafond des données (plafond baissé) : jamais de gain négatif.
        var high = new SavedFeasts(new[] { new SavedFeast(Semailles, SavedFeast.MaxCoins) });
        Assert.Equal(FeastRefusal.None, FeastRules.Earn(high, b, Semailles, "cible", 1, 10, out high, out g));
        Assert.Equal((0L, SavedFeast.MaxCoins), (g.Gained, high.Of(Semailles).Coins));
    }

    [Fact]
    public void Achats_et_rattrapage_atomiques()
    {
        FeastBook b = Feasts.Value;
        var s = new SavedFeasts(new[] { new SavedFeast(Semailles, 200) });
        // Décoration une fois ; prix exact retiré.
        int price = b.Ref("F04_D01")!.Price!.Value;
        Assert.Equal(FeastRefusal.None, FeastRules.Buy(s, b, "F04_D01", 2027, out s));
        Assert.Equal(200 - price, s.Of(Semailles).Coins);
        Assert.Equal(FeastRefusal.AlreadyOwned, FeastRules.Buy(s, b, "F04_D01", 2027, out SavedFeasts same));
        Assert.Same(s, same);
        Assert.Equal(FeastRefusal.NotForSale, FeastRules.Buy(s, b, "F04_M01", 2027, out _));
        Assert.Equal(FeastRefusal.UnknownRef, FeastRules.Buy(s, b, "F04_D09", 2027, out _));
        // Pas assez : refusé, rien ne change.
        var poor = new SavedFeasts(new[] { new SavedFeast(Semailles, 119) });
        Assert.Equal(FeastRefusal.NotEnoughCoins, FeastRules.Buy(poor, b, "F04_C01", 2027, out same));
        Assert.Same(poor, same);
        Assert.Equal(FeastRefusal.None, FeastRules.Buy(new SavedFeasts(new[] { new SavedFeast(Semailles, 120) }), b, "F04_C01", 2027, out same));
        Assert.Equal(0, same.Of(Semailles).Coins);   // prix exactement égal à la bourse
        // Plats jusqu'au maximum.
        var dishes = new SavedFeasts(new[] { new SavedFeast(Semailles, 999, dishes: b.MaxDishes - 1) });
        Assert.Equal(FeastRefusal.None, FeastRules.Buy(dishes, b, "F04_P01", 2027, out dishes));
        Assert.Equal(FeastRefusal.DishesFull, FeastRules.Buy(dishes, b, "F04_P01", 2027, out _));
        // Rattrapage : la récompense de 2026, ratée, s'achète en 2027 à prix plein (150) ; pas celle de 2027 ; une seule fois.
        var late = new SavedFeasts(new[] { new SavedFeast(Semailles, 150) });
        Assert.True(FeastRules.CanCatchUp(late, Semailles, 2027));
        Assert.Equal(FeastRefusal.None, FeastRules.Buy(late, b, "F04_R01", 2027, out late));
        Assert.Equal((0L, 0L, 1L), (late.Of(Semailles).Coins, late.Of(Semailles).Wins, late.Of(Semailles).Bought));
        Assert.Equal(new long[] { 2026 }, late.Of(Semailles).Editions);
        Assert.Empty(FeastRules.Titles(late, b));   // un rattrapage ne donne pas le titre
        Assert.Equal(FeastRefusal.NothingToCatchUp, FeastRules.Buy(late, b, "F04_R01", 2027, out _));
        Assert.Equal(FeastRefusal.NothingToCatchUp, FeastRules.Buy(SavedFeasts.Empty, b, "F04_R01", 0, out _));   // pas d'édition d'avant
        Assert.Equal(FeastRefusal.NotEnoughCoins, FeastRules.Buy(new SavedFeasts(new[] { new SavedFeast(Semailles, 149) }), b, "F04_R01", 2027, out _));
        // Gagnée en 2027 après le rattrapage de 2026 : les deux éditions gardées ; en 2028, 2027 est prise, rien à rattraper.
        late = new SavedFeasts(new[] { new SavedFeast(Semailles, 0, null, null, 1, 1, new long[] { 2026, 2027 }) });
        Assert.Equal(FeastRefusal.NothingToCatchUp, FeastRules.Buy(late, b, "F04_R01", 2028, out _));
        Assert.True(FeastRules.CanCatchUp(late, Semailles, 2029));   // 2028 ratée : jamais perdue pour toujours
        // Les deux plus récentes seulement.
        Assert.Equal(new long[] { 2027, 2029 }, late.Of(Semailles).EditionsWith(2029));
    }

    [Fact]
    public void Edition_plus_ancienne_refusee_et_activites_a_cheval_sur_deux_annees()
    {
        // Relecture indépendante de la PR #215 : la date réelle disparaît (années réelles, puis années du jeu) : rien n'est écrasé, rien n'est regagné.
        FeastBook b = Feasts.Value;
        SavedFeasts s = SavedFeasts.Empty;
        foreach (string a in new[] { "concours", "cible", "course" }) FeastRules.Earn(s, b, Semailles, a, 2027, 740_000, out s, out _);
        Assert.Equal(1, s.Of(Semailles).Wins);
        Assert.Equal(FeastRefusal.NotNow, FeastRules.Earn(s, b, Semailles, "concours", 5, 600, out SavedFeasts same, out _));   // année du jeu 5 < 2027
        Assert.Same(s, same);
        Assert.Equal(FeastRefusal.NotNow, FeastRules.Earn(s, b, Semailles, "cible", 2026, 739_700, out _, out _));
        // Rattrapage : seulement depuis une édition connue ou plus récente.
        var rich = new SavedFeasts(new[] { s.Of(Semailles).With(coins: 999) });
        Assert.False(FeastRules.CanCatchUp(rich, Semailles, 6));
        Assert.Equal(FeastRefusal.NotNow, FeastRules.Buy(rich, b, "F04_R01", 6, out same));
        Assert.Same(rich, same);
        Assert.True(FeastRules.CanCatchUp(rich, Semailles, 2029));   // 2028 ratée
        // Une édition à cheval sur deux années réelles (une fête commencée en décembre) : ses jours de janvier comptent dans la même édition.
        SavedFeasts w = SavedFeasts.Empty;
        long dec30 = new CivilDate(2027, 12, 30).DayNumber, jan2 = new CivilDate(2028, 1, 2).DayNumber;
        Assert.Equal(FeastRefusal.None, FeastRules.Earn(w, b, Semailles, "cible", 2027, dec30, out w, out _));
        Assert.Equal(FeastRefusal.None, FeastRules.Earn(w, b, Semailles, "course", 2027, jan2, out w, out _));
        Assert.Equal(FeastRefusal.None, FeastRules.Earn(w, b, Semailles, "cible", 2027, jan2, out w, out FeastGain g));   // autre jour : encore une fois
        Assert.False(g.Won);   // deux activités différentes : il en faut 3
        Assert.Equal(FeastRefusal.None, FeastRules.Earn(w, b, Semailles, "concours", 2027, jan2, out w, out g));
        Assert.True(g.Won);
        Assert.Equal((2027L, 3), (w.Of(Semailles).Edition!.Value, w.Of(Semailles).Done.Count));
        Assert.Equal(new long[] { 2027 }, w.Of(Semailles).Editions);
    }

    [Fact]
    public void Regles_deterministes()
    {
        FeastBook b = Feasts.Value;
        SavedFeasts Run()
        {
            SavedFeasts s = SavedFeasts.Empty;
            foreach (var (a, d) in new[] { ("concours", 1L), ("cible", 1L), ("cible", 2L), ("course", 2L), ("patisserie", 3L) })
                FeastRules.Earn(s, b, Semailles, a, 5, d, out s, out _);
            FeastRules.Buy(s, b, "F04_D03", 5, out s);
            return s;
        }
        Assert.Equal(Run(), Run());
        Assert.Equal(Write(Char(Run())), Write(Char(Run())));
    }

    [Fact]
    public void Titre_de_fete_parmi_les_titres_affiches()
    {
        var won = new SavedFeasts(new[] { new SavedFeast(Semailles, 0, null, null, 1, 0, new long[] { 2026 }) });
        WorldSession w = Play(Zone(), Char(won));
        // Sans catalogue des fêtes : pas de titre de fête.
        Assert.Equal(EdS.World.GuildRefusal.UnknownTitle, w.ShowTitle("title.fete.semailles", out _));
        w.UseFeasts(Feasts.Value);
        Assert.Equal(new[] { "title.fete.semailles" }, w.FeastTitles);
        Assert.Equal(EdS.World.GuildRefusal.UnknownTitle, w.ShowTitle("title.fete.lanternes", out _));   // pas gagné
        Assert.Null(w.ActiveTitle);
        Assert.Equal(EdS.World.GuildRefusal.None, w.ShowTitle("title.fete.semailles", out IReadOnlyList<StoryEvent> ev));
        Assert.Equal(("title.fete.semailles", Semailles), (w.ActiveTitle, w.Character.Feasts.Shown));
        Assert.Single(ev);
        Assert.Equal(EdS.World.GuildRefusal.None, w.ShowTitle(null, out _));
        Assert.Null(w.ActiveTitle);
        Assert.Null(w.Character.Feasts.Shown);
        // L'état gardé doit aller avec le catalogue.
        WorldSession bad = Play(Zone(), Char(new SavedFeasts(new[] { new SavedFeast("festival.marche", 3) })));
        Assert.Throws<ArgumentException>(() => bad.UseFeasts(Feasts.Value));
        WorldSession foreign = Play(Zone(), Char(new SavedFeasts(new[] { new SavedFeast(Semailles, 0, owned: new[] { "F05_D01" }) })));
        Assert.Throws<ArgumentException>(() => foreign.UseFeasts(Feasts.Value));
    }

    [Fact]
    public void Edition_et_jour_de_fete()
    {
        WorldSession w = Play(Zone(), Char());
        static long Utc(int y, int mo, int d) => new DateTimeOffset(y, mo, d, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        // Date fournie : l'année réelle du premier jour et le jour réel (Semailles du 10 au 23 avril).
        w.SetCivilTime(Utc(2027, 4, 12));
        Assert.Equal(((long)2027, new CivilDate(2027, 4, 12).DayNumber), w.FeastDay(Semailles)!.Value);
        Assert.Null(w.FeastDay(Veillee));
        Assert.Null(w.FeastDay("festival.lanternes"));   // fête du calendrier seulement : pas d'édition
        Assert.Null(w.FeastDay("festival.marche"));   // fête de la semaine
        w.SetCivilTime(Utc(2027, 4, 24));
        Assert.Null(w.FeastDay(Semailles));
        // Fête à cheval sur deux années (Lanternes, ou Givre du 18 décembre) : l'édition est l'année du premier jour.
        w.SetCivilTime(Utc(2028, 1, 1));
        Assert.Null(w.FeastDay("festival.givre"));   // calendrier seulement en F2
    }
}
