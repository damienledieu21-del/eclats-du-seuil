using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using EdS.Data.Caravans;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.41.3 (décision de Damien du 21 novembre 2026) : quête de reprise au fort après le pillage d'une caravane. Données
/// (caravanes en version 2, <c>recovery</c>), sauvegarde v31 (<c>events[].recovered</c>), coffres gardés par les bandits du fort, contrecoup
/// raccourci d'un jour par coffre et annulé quand tous sont repris, récompense ; frontières : jour 3, deuxième pillage pendant la quête.
/// </summary>
[Trait("Category", "World")]
public class V2413RepriseFortTests
{
    private const string Attack = "attaque_caravane_d_ambre", Key = "zone.vallons/" + Attack, Ambre = "caravane_d_ambre";
    private const long J = 10;

    private static ZoneDefinition Plain { get; } = ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"),
        Path.Combine(AppContext.BaseDirectory, "scenarios"), ShippedData, ItemsCatalog).Zone!;

    private static string Shipped => File.ReadAllText(CaravanJsonReader.PathIn(DataDirectory));

    private static CaravanLoadResult Read(Action<JsonObject>? mutate = null, ZoneDefinition? zone = null)
    {
        JsonObject root = JsonNode.Parse(Shipped)!.AsObject();
        mutate?.Invoke(root);
        return CaravanJsonReader.Read(root.ToJsonString(), zone ?? Plain, ItemsCatalog);
    }

    private static JsonObject Rec(JsonObject root) => root["data"]!["caravans"]![0]!["recovery"]!.AsObject();

    private static long TickAt(long day, int minute)
    {
        var clock = new WorldClock(50);
        long tick = (day * WorldClock.MinutesPerDay + minute - WorldClock.DefaultStartMinute) * clock.TicksPerDay / WorldClock.MinutesPerDay;
        while (tick > 0 && clock.At(tick - 1).Equals(new WorldTime(day, minute))) tick--;
        while (!clock.At(tick).Equals(new WorldTime(day, minute))) tick++;
        return tick;
    }

    /// <summary>Les bandits du fort éloignés des coffres (25 m plus au sud), pour reprendre un coffre sans combat.</summary>
    private static void BanditsAway(JsonObject scenario)
    {
        foreach (JsonNode? c in scenario["data"]!["combatants"]!.AsArray())
            if (c!["id"]!.GetValue<string>() is string id && (id.StartsWith("bandit_fort", StringComparison.Ordinal) || id.StartsWith("bandit_arc_fort", StringComparison.Ordinal)))
                c["position"] = new JsonArray(c["position"]![0]!.GetValue<double>(), c["position"]![1]!.GetValue<double>() - 25);
    }

    /// <summary>Partie au jour <paramref name="day"/> (9 h), le personnage au point (x, z), caravanes en jeu et marchands.</summary>
    private static WorldSession Open(double x, double z, long day, SavedCharacter hero, bool banditsAway = true)
    {
        ZoneDefinition zone = Zone(x, z, banditsAway ? BanditsAway : null);
        CaravanBook book = CaravanJsonReader.ReadFile(CaravanJsonReader.PathIn(DataDirectory), zone, ItemsCatalog).Book!;
        zone = book.ApplyTo(zone);
        var session = new WorldSession(new WorldSave("zone.vallons", TickAt(day, 9 * 60) - 1, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone, ShippedData,
            Progression, ItemsCatalog, Book(zone), market: new Market(ShippedMerchants));
        session.UseCaravans(book);
        session.Step();
        return session;
    }

    private static SavedCharacter Pillaged(long money = 0, int recovered = 0, long lostDay = J) =>
        Hero(money).WithEvents(new[] { new SavedEvent(Key, 0, 1, lostDay, EventOutcome.Lost, lostDay: lostDay, recovered: recovered) });

    private static SavedEvent Entry(WorldSession w) => w.Save().Character!.Events.Single(e => e.Id == Key);

    private static bool Shock(WorldSession w) => w.ActiveShocks.Any(s => s.Id == "caravane_pillee");

    // Coffres livrés : (1180, 1016), (1206, 994), (1240, 1018).
    private static readonly (double X, double Z)[] Chests = { (1180, 1016), (1206, 994), (1240, 1018) };

    // ---- Données ----

    [Fact]
    public void Donnees_livrees_version_2_et_version_1_encore_lue()
    {
        CaravanLoadResult r = Read();
        Assert.True(r.IsValid, r.Report.ToString());
        Assert.Equal(2, CaravanValidator.CurrentSchemaVersion);
        CaravanRecovery rec = r.Book!.Caravans[0].Recovery!;
        Assert.Equal(("enceinte_du_fort", 3, 3, 12L, 400L, 200L, 7), (rec.Place, rec.Days, rec.Chests.Count, rec.GuardRadius.Raw / 10_000, rec.Xp, rec.Money, rec.AllChests));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "caravans.schema.json"))
            .IsValid(System.Text.Json.JsonDocument.Parse(Shipped).RootElement));
        // Version 1 : sans reprise, encore lue ; avec, champ inconnu.
        CaravanLoadResult v1 = Read(root => { root["schemaVersion"] = 1; ((JsonObject)root["data"]!["caravans"]![0]!).Remove("recovery"); });
        Assert.True(v1.IsValid, v1.Report.ToString());
        Assert.Null(v1.Book!.Caravans[0].Recovery);
        CaravanLoadResult bad = Read(root => root["schemaVersion"] = 1);
        Assert.Contains(bad.Report.Errors, e => e.Path == "$.data.caravans[0].recovery" && e.Code == ValidationCode.UnknownField);
        Assert.Contains(Read(root => root["schemaVersion"] = 3).Report.Errors, e => e.Path == "$.schemaVersion");
    }

    [Theory]
    [InlineData("place", "$.data.caravans[0].recovery.place")]
    [InlineData("hors", "$.data.caravans[0].recovery.chests[0]")]
    [InlineData("proches", "$.data.caravans[0].recovery.chests[1]")]
    [InlineData("jours0", "$.data.caravans[0].recovery.days")]
    [InlineData("jours31", "$.data.caravans[0].recovery.days")]
    [InlineData("rayon", "$.data.caravans[0].recovery.guardRadius")]
    [InlineData("aucun", "$.data.caravans[0].recovery.chests")]
    [InlineData("neuf", "$.data.caravans[0].recovery.chests")]
    [InlineData("xp", "$.data.caravans[0].recovery.xp")]
    [InlineData("money", "$.data.caravans[0].recovery.money")]
    [InlineData("inconnu", "$.data.caravans[0].recovery.chef")]
    [InlineData("manque", "$.data.caravans[0].recovery.days")]
    public void Reprise_invalide_refusee_avec_son_chemin(string cas, string chemin)
    {
        CaravanLoadResult r = Read(root =>
        {
            JsonObject x = Rec(root);
            switch (cas)
            {
                case "place": x["place"] = "nulle_part"; break;
                case "hors": x["chests"]![0] = new JsonArray(1100, 1016); break;
                case "proches": x["chests"]![1] = new JsonArray(1181, 1017); break;
                case "jours0": x["days"] = 0; break;
                case "jours31": x["days"] = 31; break;
                case "rayon": x["guardRadius"] = 0.5; break;
                case "aucun": x["chests"] = new JsonArray(); break;
                case "neuf": x["chests"] = new JsonArray(Enumerable.Range(0, 9).Select(k => (JsonNode)new JsonArray(1140 + 10 * k, 1000)).ToArray()); break;
                case "xp": x["xp"] = -1; break;
                case "money": x["money"] = 1_000_001; break;
                case "inconnu": x["chef"] = true; break;
                case "manque": x.Remove("days"); break;
            }
        });
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == chemin);
    }

    // ---- Sauvegarde v31 ----

    [Fact]
    public void Coffres_repris_format_version_31()
    {
        Assert.Equal((31, 31, 30), (SaveValidator.CurrentSchemaVersion, SaveValidator.EventRecoveredSince, SaveValidator.EventLostDaySince));
        string json = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, Pillaged(recovered: 5)));
        Assert.Contains("\"lostDay\": 10, \"recovered\": 5}", json);
        SaveLoadResult ok = SaveJsonReader.Read(json);
        Assert.True(ok.IsValid, ok.Report.ToString());
        Assert.Equal(5, ok.Save!.Character!.Events.Single().Recovered);
        // Aucun coffre : rien d'écrit.
        Assert.DoesNotContain("recovered", SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, Pillaged())));
        foreach ((string text, ValidationCode code) in new[] { ("0", ValidationCode.OutOfRange), ("256", ValidationCode.OutOfRange), ("-1", ValidationCode.OutOfRange) })
        {
            SaveLoadResult bad = SaveJsonReader.Read(json.Replace("\"recovered\": 5", "\"recovered\": " + text));
            Assert.Contains(bad.Report.Errors, x => x.Path.EndsWith(".recovered", StringComparison.Ordinal) && x.Code == code);
        }
        SaveLoadResult noDay = SaveJsonReader.Read(json.Replace("\"lostDay\": 10, ", ""));
        Assert.Contains(noDay.Report.Errors, x => x.Path.EndsWith(".recovered", StringComparison.Ordinal) && x.Code == ValidationCode.InvalidValue);
        // Version 30 : champ inconnu ; sans lui, encore lue.
        string v30 = json.Replace("\"schemaVersion\": 31", "\"schemaVersion\": 30");
        Assert.Contains(SaveJsonReader.Read(v30).Report.Errors, x => x.Path.EndsWith(".recovered", StringComparison.Ordinal) && x.Code == ValidationCode.UnknownField);
        Assert.True(SaveJsonReader.Read(v30.Replace(", \"recovered\": 5", "")).IsValid);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SavedEvent(Key, 0, 1, J, EventOutcome.Lost, lostDay: J, recovered: 256));
        Assert.Throws<ArgumentException>(() => new SavedEvent(Key, 0, 1, J, EventOutcome.Lost, recovered: 1));
    }

    // ---- Dans la partie ----

    [Fact]
    public void Quete_ouverte_trois_jours_a_partir_du_pillage()
    {
        // Jour du pillage, puis J+2 : ouverte jusqu'au soir du troisième jour ; J+3 : fermée (frontière), coffre refusé.
        foreach (long day in new[] { J, J + 2 })
        {
            CaravanRecoveryView v = Assert.Single(Open(-18, -12, day, Pillaged()).CaravanRecoveries);
            Assert.Equal((J, J + 2, 0), (v.PillageDay, v.LastDay, v.Taken));
        }
        WorldSession late = Open(Chests[0].X, Chests[0].Z, J + 3, Pillaged());
        Assert.Empty(late.CaravanRecoveries);
        Assert.Equal(ChestRefusal.Closed, late.TakeCaravanChest(Ambre, 0));
        // Pas de pillage : rien.
        Assert.Empty(Open(-18, -12, J, Hero()).CaravanRecoveries);
        Assert.Equal(ChestRefusal.Closed, Open(Chests[0].X, Chests[0].Z, J, Hero()).TakeCaravanChest(Ambre, 0));
    }

    [Fact]
    public void Coffre_garde_par_les_bandits_refus_atomique()
    {
        WorldSession w = Open(Chests[0].X, Chests[0].Z, J + 1, Pillaged(), banditsAway: false);
        string before = SaveJsonReader.Write(w.Save());
        Assert.Equal(ChestRefusal.Guarded, w.TakeCaravanChest(Ambre, 0));
        Assert.Equal(before, SaveJsonReader.Write(w.Save()));
        Assert.True(Shock(w));
    }

    [Fact]
    public void Refus_coffre_inconnu_loin_ou_deja_repris()
    {
        WorldSession w = Open(Chests[0].X, Chests[0].Z, J + 1, Pillaged());
        string before = SaveJsonReader.Write(w.Save());
        Assert.Equal(ChestRefusal.Unknown, w.TakeCaravanChest(Ambre, 3));
        Assert.Equal(ChestRefusal.Unknown, w.TakeCaravanChest(Ambre, -1));
        Assert.Equal(ChestRefusal.Unknown, w.TakeCaravanChest("caravane_du_nord", 0));
        Assert.Equal(ChestRefusal.NotHere, w.TakeCaravanChest(Ambre, 2));   // à 60 m
        Assert.Equal(before, SaveJsonReader.Write(w.Save()));
        Assert.Equal(ChestRefusal.None, w.TakeCaravanChest(Ambre, 0));
        Assert.Equal(ChestRefusal.Taken, w.TakeCaravanChest(Ambre, 0));
        Assert.Equal((1, J), (Entry(w).Recovered, Entry(w).LostDay));
    }

    [Fact]
    public void Chaque_coffre_enleve_un_jour_au_contrecoup()
    {
        // Un coffre repris : le contrecoup (3 jours) s'arrête un jour plus tôt (actif à J+1, fini à J+2).
        Assert.True(Shock(Open(-1097.2, -40.4, J + 1, Pillaged(recovered: 1))));
        Assert.False(Shock(Open(-1097.2, -40.4, J + 2, Pillaged(recovered: 1))));
        Assert.True(Shock(Open(-1097.2, -40.4, J + 2, Pillaged())));
        // Deux : seulement le jour du pillage ; trois : annulé dès ce jour.
        Assert.True(Shock(Open(-1097.2, -40.4, J, Pillaged(recovered: 6))));
        Assert.False(Shock(Open(-1097.2, -40.4, J + 1, Pillaged(recovered: 6))));
        Assert.False(Shock(Open(-1097.2, -40.4, J, Pillaged(recovered: 7))));
    }

    [Fact]
    public void Dernier_coffre_annule_le_contrecoup_et_recompense()
    {
        WorldSession w = Open(Chests[2].X, Chests[2].Z, J, Pillaged(money: 1000, recovered: 3));   // deux coffres repris : contrecoup le jour du pillage seulement
        long xp = w.Save().Character!.Xp;
        Assert.True(Shock(w));
        Assert.Equal(ChestRefusal.None, w.TakeCaravanChest(Ambre, 2));
        Assert.Equal((7, 1200L, xp + 400), (Entry(w).Recovered, w.Money, w.Save().Character!.Xp));
        Assert.False(Shock(w));
        Assert.Empty(w.CaravanRecoveries);
        Assert.Equal(ChestRefusal.Closed, w.TakeCaravanChest(Ambre, 0));
        IReadOnlyList<WorldEventNotice> notices = w.Step().Events;
        Assert.Contains(notices, n => n.Kind == WorldEventNoticeKind.ChestRecovered && n.Wave == 2);
        Assert.Contains(notices, n => n.Kind == WorldEventNoticeKind.Recovered && n.Xp == 400 && n.Money == 200);
        // Écrit et relu : les coffres repris sont gardés.
        SaveLoadResult again = SaveJsonReader.Read(SaveJsonReader.Write(w.Save()));
        Assert.True(again.IsValid, again.Report.ToString());
        Assert.Equal(7, again.Save!.Character!.Events.Single(e => e.Id == Key).Recovered);
    }

    [Fact]
    public void Bourse_pleine_l_experience_seule()
    {
        WorldSession w = Open(Chests[0].X, Chests[0].Z, J, Pillaged(money: Coins.Max, recovered: 6));
        Assert.Equal(ChestRefusal.None, w.TakeCaravanChest(Ambre, 0));
        Assert.Equal(Coins.Max, w.Money);
        Assert.Contains(w.Step().Events, n => n.Kind == WorldEventNoticeKind.Recovered && n.Money == 0 && n.Xp == 400);
    }

    [Fact]
    public void Deuxieme_pillage_pendant_la_quete_la_remet_a_zero()
    {
        WorldSession w = Open(-18, -12, J + 1, Pillaged(recovered: 1));
        WorldEventDefinition e = w.Definition!.EventOf(Attack)!;
        EventOccurrence second = Enumerable.Range(0, 400).Select(k => WorldEventSchedule.InWindow(w.Definition.Id, w.Definition.Id, e, k))
            .OfType<EventOccurrence>().First(o => o.Day == J + 1 || o.Day > J);
        WorldSession at = Open(-18, -12, second.Day, Pillaged(recovered: 1));
        var notices = new List<WorldEventNotice>();
        at.SettleEvent(e, second, EventOutcome.Lost, WorldEventNoticeKind.Lost, notices);   // joueur présent qui perd : pas de tirage des gardes
        SavedEvent entry = Entry(at);
        Assert.Equal((second.Day, 0, 2L), (entry.LostDay, entry.Recovered, entry.Lost));
        Assert.Contains(notices, n => n.Kind == WorldEventNoticeKind.RecoveryOpened);
        CaravanRecoveryView v = Assert.Single(at.CaravanRecoveries);
        Assert.Equal((second.Day, second.Day + 2), (v.PillageDay, v.LastDay));
        // Une caravane sauvée ensuite garde les coffres repris du pillage en cours.
        EventOccurrence third = Enumerable.Range(0, 400).Select(k => WorldEventSchedule.InWindow(w.Definition.Id, w.Definition.Id, e, k))
            .OfType<EventOccurrence>().First(o => o.Day > second.Day);
        WorldSession later = Open(-18, -12, third.Day, Hero().WithEvents(new[] { new SavedEvent(Key, 0, 2, second.Day, EventOutcome.Lost, lostDay: second.Day, recovered: 2) }));
        later.SettleEvent(e, third, EventOutcome.Won, null, notices);
        Assert.Equal((third.Day, second.Day, 2), (Entry(later).Last, Entry(later).LostDay, Entry(later).Recovered));
    }

    [Fact]
    public void Textes_des_annonces_de_la_reprise()
    {
        foreach (string moment in new[] { "reprise_ouverte", "coffre", "reprise" })
            Assert.True(Texts.Has($"text.event.{Attack}.{moment}"), moment);
        Assert.True(Texts.Has("text.ui.caravan.chest.guarded") && Texts.Has("text.ui.caravan.chest.too_far"));
        Assert.Equal(("reprise_ouverte", "coffre", "reprise"), (EdS.Runtime.AdventureSession.Moment(WorldEventNoticeKind.RecoveryOpened),
            EdS.Runtime.AdventureSession.Moment(WorldEventNoticeKind.ChestRecovered), EdS.Runtime.AdventureSession.Moment(WorldEventNoticeKind.Recovered)));
    }
}
