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
    private static WorldSession Open(double x, double z, long day, SavedCharacter hero, bool banditsAway = true, Action<JsonObject>? more = null)
    {
        Action<JsonObject>? scenario = banditsAway || more != null ? root => { if (banditsAway) BanditsAway(root); more?.Invoke(root); } : null;
        ZoneDefinition zone = Zone(x, z, scenario);
        CaravanBook book = CaravanJsonReader.ReadFile(CaravanJsonReader.PathIn(DataDirectory), zone, ItemsCatalog).Book!;
        zone = book.ApplyTo(zone);
        var session = new WorldSession(new WorldSave("zone.vallons", TickAt(day, 9 * 60) - 1, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone, ShippedData,
            Progression, ItemsCatalog, Book(zone), market: new Market(ShippedMerchants));
        session.UseCaravans(book);
        session.Step();
        return session;
    }

    private static SavedCharacter Pillaged(long money = 0, int recovered = 0, long lostDay = J, bool once = false) =>
        Hero(money).WithEvents(new[] { new SavedEvent(Key, 0, 1, lostDay, EventOutcome.Lost, lostDay: lostDay, recovered: recovered, recoveredOnce: once) });

    /// <summary>Place le combattant <paramref name="id"/> du scénario au point (x, z).</summary>
    private static Action<JsonObject> Put(string id, double x, double z) => root =>
    {
        foreach (JsonNode? c in root["data"]!["combatants"]!.AsArray())
            if (c!["id"]!.GetValue<string>() == id) c["position"] = new JsonArray(x, z);
    };

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
        // Reprise déjà faite (décision de Damien du 21 novembre 2026) : écrit seulement vrai, avec un pillage.
        string once = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, Pillaged(recovered: 5, once: true)));
        Assert.Contains("\"recovered\": 5, \"recoveredOnce\": true}", once);
        Assert.True(SaveJsonReader.Read(once).Save!.Character!.Events.Single().RecoveredOnce);
        Assert.Contains(SaveJsonReader.Read(once.Replace("\"recoveredOnce\": true", "\"recoveredOnce\": false")).Report.Errors, x => x.Path.EndsWith(".recoveredOnce", StringComparison.Ordinal));
        Assert.Contains(SaveJsonReader.Read(once.Replace("\"schemaVersion\": 31", "\"schemaVersion\": 30")).Report.Errors,
            x => x.Path.EndsWith(".recoveredOnce", StringComparison.Ordinal) && x.Code == ValidationCode.UnknownField);
        Assert.Throws<ArgumentException>(() => new SavedEvent(Key, 1, 0, J, EventOutcome.Won, recoveredOnce: true));
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
        // Revue Codex de la PR #243 : des bits en trop (24 : ni le coffre 0, ni le 1, ni le 2) ne raccourcissent pas le contrecoup ; reprendre
        // ensuite un vrai coffre l'écourte d'un jour, sans le réactiver.
        Assert.True(Shock(Open(-1097.2, -40.4, J + 2, Pillaged(recovered: 24))));
        WorldSession w = Open(Chests[0].X, Chests[0].Z, J + 1, Pillaged(recovered: 24));
        Assert.True(Shock(w));
        Assert.Equal(ChestRefusal.None, w.TakeCaravanChest(Ambre, 0));
        Assert.True(Shock(w));
        Assert.False(Shock(Open(-1097.2, -40.4, J + 2, Pillaged(recovered: 24 | 1))));
    }

    [Fact]
    public void Dernier_coffre_annule_le_contrecoup_et_recompense()
    {
        WorldSession w = Open(Chests[2].X, Chests[2].Z, J, Pillaged(money: 1000, recovered: 3));   // deux coffres repris : contrecoup le jour du pillage seulement
        long xp = w.Save().Character!.Xp;
        Assert.True(Shock(w));
        Assert.Equal(ChestRefusal.None, w.TakeCaravanChest(Ambre, 2));
        Assert.Equal((7, 1200L, xp + 400, true), (Entry(w).Recovered, w.Money, w.Save().Character!.Xp, Entry(w).RecoveredOnce));
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
        Assert.False(entry.RecoveredOnce);
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

    // ---- Relecture indépendante de la PR #243 ----

    /// <summary>Décision de Damien du 21 novembre 2026 (anti-triche) : la deuxième reprise complète donne l'or seul (2 or, sans expérience).</summary>
    [Fact]
    public void Deuxieme_reprise_complete_l_or_seul()
    {
        WorldSession w = Open(Chests[2].X, Chests[2].Z, J, Pillaged(money: 1000, recovered: 3, once: true));
        long xp = w.Save().Character!.Xp;
        Assert.Equal(ChestRefusal.None, w.TakeCaravanChest(Ambre, 2));
        Assert.Equal((1200L, xp, true), (w.Money, w.Save().Character!.Xp, Entry(w).RecoveredOnce));
        Assert.Contains(w.Step().Events, n => n.Kind == WorldEventNoticeKind.Recovered && n.Xp == 0 && n.Money == 200);
        Assert.False(Shock(w));
    }

    /// <summary>Des bits en trop dans le masque (au-delà des coffres de la quête) ne la bloquent pas et sont effacés à la reprise suivante.</summary>
    [Fact]
    public void Bits_en_trop_du_masque_ignores()
    {
        WorldSession w = Open(Chests[0].X, Chests[0].Z, J, Pillaged(recovered: 8 | 16));
        Assert.Equal(0, Assert.Single(w.CaravanRecoveries).Taken);
        Assert.Equal(ChestRefusal.None, w.TakeCaravanChest(Ambre, 0));
        Assert.Equal(1, Entry(w).Recovered);
        Assert.Empty(Open(-18, -12, J, Pillaged(recovered: 255)).CaravanRecoveries);   // les trois coffres repris (bits en trop compris)
    }

    [Fact]
    public void Portee_a_trois_metres_pile_et_au_dela()
    {
        Assert.Equal(ChestRefusal.None, Open(Chests[0].X + 3, Chests[0].Z, J, Pillaged()).TakeCaravanChest(Ambre, 0));
        Assert.Equal(ChestRefusal.NotHere, Open(Chests[0].X + 3.01, Chests[0].Z, J, Pillaged()).TakeCaravanChest(Ambre, 0));
    }

    [Fact]
    public void Garde_a_douze_metres_pile_et_au_dela_ennemi_qui_fuit_ignore()
    {
        WorldSession at12 = Open(Chests[0].X, Chests[0].Z, J, Pillaged(), more: Put("bandit_fort1", Chests[0].X, Chests[0].Z - 12));
        Assert.Equal(Fixed.FromInt(-12), at12.Zone.Engine.View("bandit_fort1").Position!.Value.Z - Fixed.FromInt(1016));
        Assert.Equal(ChestRefusal.Guarded, at12.TakeCaravanChest(Ambre, 0));
        Assert.Equal(ChestRefusal.None, Open(Chests[0].X, Chests[0].Z, J, Pillaged(), more: Put("bandit_fort1", Chests[0].X, Chests[0].Z - 12.01))
            .TakeCaravanChest(Ambre, 0));
        // Un cerf (bête qui fuit) à 5 m du coffre ne le garde pas.
        WorldSession deer = Open(Chests[0].X, Chests[0].Z, J, Pillaged(), more: Put("cerf1", Chests[0].X, Chests[0].Z - 5));
        Assert.True(deer.Zone.Engine.View("cerf1").Flees);
        Assert.Equal(ChestRefusal.None, deer.TakeCaravanChest(Ambre, 0));
    }

    [Fact]
    public void Personnage_tombe_refuse()
    {
        // Les bandits du fort, restés à leur place, ont raison du héros débutant.
        WorldSession w = Open(Chests[0].X, Chests[0].Z, J, Pillaged(), banditsAway: false);
        for (int i = 0; i < 20_000 && !w.IsDefeated; i++) w.Step();
        Assert.True(w.IsDefeated);
        Assert.Equal(ChestRefusal.NotHere, w.TakeCaravanChest(Ambre, 0));
        Assert.Equal(0, Entry(w).Recovered);
    }

    /// <summary>
    /// Registre plein : la ligne du pillage existe toujours quand la quête est ouverte, la reprise n'en ajoute pas (le refus Full reste une
    /// défense) ; sans ligne de pillage, la quête est fermée, rien ne change.
    /// </summary>
    [Fact]
    public void Registre_plein()
    {
        IEnumerable<SavedEvent> Others(int n) => Enumerable.Range(0, n).Select(k => new SavedEvent($"zone.vallons/autre_{k:00}", 0, 0, -1, EventOutcome.None));
        SavedCharacter full = Hero().WithEvents(Others(SavedEvent.MaxEntries - 1).Concat(new[] { new SavedEvent(Key, 0, 1, J, EventOutcome.Lost, lostDay: J) }));
        WorldSession w = Open(Chests[0].X, Chests[0].Z, J, full);
        Assert.Equal(ChestRefusal.None, w.TakeCaravanChest(Ambre, 0));
        Assert.Equal((SavedEvent.MaxEntries, 1), (w.Save().Character!.Events.Count, Entry(w).Recovered));
        WorldSession none = Open(Chests[0].X, Chests[0].Z, J, Hero().WithEvents(Others(SavedEvent.MaxEntries)));
        string before = SaveJsonReader.Write(none.Save());
        Assert.Equal(ChestRefusal.Closed, none.TakeCaravanChest(Ambre, 0));
        Assert.Equal(before, SaveJsonReader.Write(none.Save()));
    }

    /// <summary>Sauvegarde v29 (jour du pillage inconnu, lostDay -1) : la quête part de la dernière issue pillée ; la reprise écrit le jour.</summary>
    [Fact]
    public void Sauvegarde_v29_sans_jour_de_pillage()
    {
        WorldSession w = Open(Chests[0].X, Chests[0].Z, J + 1, Hero().WithEvents(new[] { new SavedEvent(Key, 0, 1, J, EventOutcome.Lost) }));
        Assert.Equal(J, Assert.Single(w.CaravanRecoveries).PillageDay);
        Assert.Equal(ChestRefusal.None, w.TakeCaravanChest(Ambre, 0));
        Assert.Equal((J, 1), (Entry(w).LostDay, Entry(w).Recovered));
    }

    [Fact]
    public void Sauvegarde_et_rechargement_avec_un_puis_deux_coffres()
    {
        WorldSession w = Open(Chests[0].X, Chests[0].Z, J, Pillaged());
        Assert.Equal(ChestRefusal.None, w.TakeCaravanChest(Ambre, 0));
        SavedCharacter one = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!.Character!;
        WorldSession again = Open(Chests[1].X, Chests[1].Z, J, one);
        Assert.Equal(1, Assert.Single(again.CaravanRecoveries).Taken);
        Assert.Equal(ChestRefusal.None, again.TakeCaravanChest(Ambre, 1));
        SavedCharacter two = SaveJsonReader.Read(SaveJsonReader.Write(again.Save())).Save!.Character!;
        WorldSession third = Open(Chests[0].X, Chests[0].Z, J, two);
        Assert.Equal(3, Assert.Single(third.CaravanRecoveries).Taken);
        Assert.Equal(ChestRefusal.Taken, third.TakeCaravanChest(Ambre, 0));
    }

    private static void CopyTree(string from, string to)
    {
        foreach (string dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(dir.Replace(from, to));
        Directory.CreateDirectory(to);
        foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories)) File.Copy(file, file.Replace(from, to));
    }

    /// <summary>
    /// Revue Codex de la PR #243 (P1) : dans l'aventure, la touche d'action (Interact, touche F et action tactile) reprend le coffre à portée,
    /// et l'indice « F : reprendre le coffre » existe. Le premier point sûr est déplacé sur le premier coffre, les bandits du fort éloignés.
    /// </summary>
    [Fact]
    public void Touche_d_action_reprend_le_coffre()
    {
        string root = Path.Combine(Path.GetTempPath(), "reprise_" + Guid.NewGuid().ToString("N"));
        string data = Path.Combine(root, "data"), scenarios = Path.Combine(root, "scenarios");
        try
        {
            CopyTree(DataDirectory, data);
            CopyTree(ScenariosDirectory, scenarios);
            string zonePath = ZoneJsonReader.PathIn(data, "zone.vallons");
            JsonObject zone = JsonNode.Parse(File.ReadAllText(zonePath))!.AsObject();
            zone["data"]!["safePoints"]![0]!["position"] = new JsonArray(Chests[0].X, Chests[0].Z);
            File.WriteAllText(zonePath, zone.ToJsonString());
            string scenarioPath = Path.Combine(scenarios, "zones", "vallons.json");
            JsonObject scenario = JsonNode.Parse(File.ReadAllText(scenarioPath))!.AsObject();
            BanditsAway(scenario);
            File.WriteAllText(scenarioPath, scenario.ToJsonString());
            string save = Path.Combine(root, "save.json");
            SaveStore.Write(save, new WorldSave("zone.vallons", TickAt(J, 9 * 60) - 1, 0, Array.Empty<string>(), Pillaged(),
                resume: new SavedResume(zone["data"]!["safePoints"]![0]!["id"]!.GetValue<string>())));
            EdS.Runtime.AdventureSession a = EdS.Runtime.AdventureSession.Load(data, scenarios, save);
            a.Advance(0.05);
            Assert.Equal((Ambre, 0), a.ChestInReach);
            Assert.True(a.Interact());
            Assert.Equal(1, Entry(a.World).Recovered);
            Assert.Null(a.ChestInReach);
            Assert.True(Texts.Has("text.ui.caravan.chest.take"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Textes_des_annonces_de_la_reprise()
    {
        Assert.True(Texts.Has($"text.event.{Attack}.reprise_sans_or"));
        foreach (string moment in new[] { "reprise_ouverte", "coffre", "reprise" })
            Assert.True(Texts.Has($"text.event.{Attack}.{moment}"), moment);
        Assert.True(Texts.Has("text.ui.caravan.chest.guarded") && Texts.Has("text.ui.caravan.chest.too_far"));
        Assert.Equal(("reprise_ouverte", "coffre", "reprise"), (EdS.Runtime.AdventureSession.Moment(WorldEventNoticeKind.RecoveryOpened),
            EdS.Runtime.AdventureSession.Moment(WorldEventNoticeKind.ChestRecovered), EdS.Runtime.AdventureSession.Moment(WorldEventNoticeKind.Recovered)));
    }
}
