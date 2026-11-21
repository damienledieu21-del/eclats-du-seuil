using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Caravans;
using EdS.Data.Json;
using EdS.Data.Merchants;
using EdS.Data.Saves;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.41.1 : caravanes et attaques des bandits (cœur). Données <c>caravans.json</c> (version 1) et leur validateur à chemin précis ;
/// place d'une caravane, fonction pure du temps du monde et du registre (halte pendant l'attaque, épave si pillée, reprise si sauvée) ;
/// l'attaque est un événement du monde de la zone (sorte « caravan ») : issue tirée quand le joueur ne la sauve pas (gardes), prime à chaque
/// caravane sauvée après la première, contrecoup des marchands quand elle est pillée.
/// </summary>
[Trait("Category", "World")]
public class V241CaravanesTests
{
    private const string Attack = "attaque_caravane_d_ambre";

    private static ZoneDefinition Zone { get; } = ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"),
        Path.Combine(AppContext.BaseDirectory, "scenarios"), ShippedData, ItemsCatalog).Zone!;

    private static string Shipped => File.ReadAllText(CaravanJsonReader.PathIn(DataDirectory));

    private static CaravanLoadResult Read(Action<JsonObject>? mutate = null)
    {
        JsonObject root = JsonNode.Parse(Shipped)!.AsObject();
        mutate?.Invoke(root["data"]!["caravans"]![0]!.AsObject());
        return CaravanJsonReader.Read(root.ToJsonString(), Zone, ItemsCatalog);
    }

    private static CaravanBook Caravans { get; } = Read().Book!;
    private static Caravan Ambre => Caravans.Caravans[0];

    private static GroundVector P(double x, double z) => new GroundVector(Fixed.Parse(x.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        Fixed.Parse(z.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Donnees_livrees_valides_et_attaque_ajoutee_a_la_zone()
    {
        CaravanLoadResult r = Read();
        Assert.True(r.IsValid, r.Report.ToString());
        Assert.Equal("caravane_d_ambre", Ambre.Id);
        Assert.Equal(8 * 60, Ambre.StartMinute);
        Assert.Equal(P(1310, 870), Ambre.Route[Ambre.HaltIndex]);
        WorldEventDefinition e = Ambre.Attack;
        Assert.Equal((WorldEventKind.Caravan, "caravane_d_ambre", 50, 0), (e.Kind, e.Caravan, e.GuardsPercent, e.SpreadMinutes));
        // L'attaque part à la minute où la caravane atteint sa halte (arrondie au-dessus).
        Assert.Equal(Ambre.StartMinute + CaravanValidator.MinutesFor(Ambre.HaltDistance, Ambre.MetersPerMinute), e.StartMinute);
        Assert.Equal(200, e.Reward!.Bounty);
        ZoneDefinition applied = Caravans.ApplyTo(Zone);
        Assert.Same(e, applied.EventOf(Attack));
        Assert.Equal(Zone.Events.Count + 1, applied.Events.Count);
        Assert.Same(applied, Caravans.ApplyTo(applied));   // une seule fois
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "caravans.schema.json"))
            .IsValid(JsonDocument.Parse(Shipped).RootElement));
    }

    [Theory]
    [InlineData("halte_hors_route", "$.data.caravans[0].attack.groups[0].position")]
    [InlineData("halte_premier_point", "$.data.caravans[0].attack.groups[0].position")]
    [InlineData("variantes_deux_haltes", "$.data.caravans[0].attack.groups[1].position")]
    [InlineData("vitesse_nulle", "$.data.caravans[0].metersPerMinute")]
    [InlineData("vitesse_trop_grande", "$.data.caravans[0].metersPerMinute")]
    [InlineData("voyage_trop_long", "$.data.caravans[0].metersPerMinute")]
    [InlineData("halte_apres_minuit", "$.data.caravans[0].start")]
    [InlineData("point_hors_aire", "$.data.caravans[0].route[0]")]
    [InlineData("points_confondus", "$.data.caravans[0].route[1]")]
    [InlineData("id_d_un_evenement", "$.data.caravans[0].attack.id")]
    [InlineData("gardes_absents", "$.data.caravans[0].attack.guardsPercent")]
    [InlineData("gardes_101", "$.data.caravans[0].attack.guardsPercent")]
    [InlineData("prime_trop_grande", "$.data.caravans[0].attack.reward.bounty")]
    [InlineData("prime_negative", "$.data.caravans[0].attack.reward.bounty")]
    [InlineData("chance_nulle", "$.data.caravans[0].attack.chancePercent")]
    [InlineData("creature_inconnue", "$.data.caravans[0].attack.groups[0].creatures[0].like")]
    [InlineData("sorte_donnee", "$.data.caravans[0].attack.kind")]
    [InlineData("heure_donnee", "$.data.caravans[0].attack.start")]
    [InlineData("chariots_0", "$.data.caravans[0].wagons")]
    [InlineData("tous_les_31_jours", "$.data.caravans[0].everyDays")]
    public void Donnees_invalides_refusees_avec_leur_chemin(string cas, string chemin)
    {
        CaravanLoadResult r = Read(c =>
        {
            JsonObject a = c["attack"]!.AsObject();
            JsonArray route = c["route"]!.AsArray();
            switch (cas)
            {
                case "halte_hors_route": foreach (JsonNode? g in a["groups"]!.AsArray()) g!["position"] = new JsonArray(1311, 870); break;
                case "halte_premier_point": foreach (JsonNode? g in a["groups"]!.AsArray()) g!["position"] = route[0]!.DeepClone(); break;
                case "variantes_deux_haltes": a["groups"]![1]!["position"] = new JsonArray(1170, 880); break;
                case "vitesse_nulle": c["metersPerMinute"] = 0; break;
                case "vitesse_trop_grande": c["metersPerMinute"] = 120.0001; break;
                case "voyage_trop_long": c["metersPerMinute"] = 2; c["start"] = "00:00"; break;
                case "halte_apres_minuit": c["start"] = "23:30"; break;
                case "point_hors_aire": route[0] = new JsonArray(-9000, 0); break;
                case "points_confondus": route[1] = route[0]!.DeepClone(); break;
                case "id_d_un_evenement": a["id"] = "attaque_hameau"; break;
                case "gardes_absents": a.Remove("guardsPercent"); break;
                case "gardes_101": a["guardsPercent"] = 101; break;
                case "prime_trop_grande": a["reward"]!["bounty"] = ZoneValidator.MaxEventBounty + 1; break;
                case "prime_negative": a["reward"]!["bounty"] = -1; break;
                case "chance_nulle": a["chancePercent"] = 0; break;
                case "creature_inconnue": a["groups"]![0]!["creatures"]![0]!["like"] = "dragon"; break;
                case "sorte_donnee": a["kind"] = "raid"; break;
                case "heure_donnee": a["start"] = "12:00"; break;
                case "chariots_0": c["wagons"] = 0; break;
                case "tous_les_31_jours": c["everyDays"] = 31; break;
            }
        });
        Assert.False(r.IsValid);
        Assert.Null(r.Book);
        Assert.Contains(r.Report.Errors, e => e.Path == chemin);
    }

    [Fact]
    public void Une_prime_ou_des_gardes_refuses_dans_un_fichier_de_zone()
    {
        // Le lecteur des zones ne connaît ni « guardsPercent » ni « bounty » : la forme d'une zone ne change pas.
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        root["data"]!["events"]![0]!["guardsPercent"] = 50;
        ZoneLoadResult r = ZoneJsonReader.Read(root.ToJsonString(), Path.Combine(AppContext.BaseDirectory, "scenarios"), ShippedData, ItemsCatalog);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.events[0].guardsPercent");
    }

    private static long Milli(long day, long minute) => checked((day * 1440 + minute) * CaravanSchedule.Milli);

    /// <summary>Premier jour de caravane avec (ou sans) attaque.</summary>
    private static long DayWith(bool attacked)
    {
        for (long d = Ambre.FirstDay; d < 400; d++)
            if (Ambre.Runs(d) && (WorldEventSchedule.InWindow(Zone.Id, Zone.Id, Ambre.Attack, d / Ambre.Attack.EveryDays)?.Day == d) == attacked) return d;
        throw new InvalidOperationException("aucun jour");
    }

    private static CaravanView? At(long now, EventOutcome? outcome = null) => CaravanSchedule.At(Zone.Id, Zone.Id, Ambre, now, _ => outcome);

    [Fact]
    public void Caravane_sans_attaque_part_avance_et_arrive()
    {
        long d = DayWith(false);
        Assert.Null(At(Milli(d, Ambre.StartMinute) - 1));
        CaravanView start = At(Milli(d, Ambre.StartMinute))!;
        Assert.Equal((CaravanState.OnRoad, Fixed.Zero, Ambre.Route[0]), (start.State, start.Distance, start.Position));
        // 10 minutes du monde : 112,5 m le long de la route ; une demi-minute : la moitié de 11,25 m.
        Assert.Equal(Fixed.Parse("112.5"), At(Milli(d, Ambre.StartMinute + 10))!.Distance);
        Assert.Equal(Fixed.Parse("5.625"), At(Milli(d, Ambre.StartMinute) + 500)!.Distance);
        long total = CaravanValidator.MinutesFor(Ambre.Length, Ambre.MetersPerMinute);
        Assert.NotNull(At(Milli(d, Ambre.StartMinute + total) - 1000));
        Assert.Null(At(Milli(d, Ambre.StartMinute + total)));   // arrivée
        Assert.Null(At(Milli(d, 1439)));
        Assert.Null(At(Milli(0, Ambre.StartMinute + 5)));   // avant le premier jour
    }

    [Fact]
    public void Place_au_tronçon_le_long_de_la_route()
    {
        // Juste avant et juste après un point de la route : sur l'un puis l'autre tronçon ; bornée aux deux bouts.
        Fixed at = Ambre.Along[3];
        Assert.Equal(Ambre.Route[3], Ambre.At(at).Point);
        Assert.Equal(Ambre.Route[0], Ambre.At(-Fixed.FromInt(5)).Point);
        Assert.Equal(Ambre.Route[Ambre.Route.Count - 1], Ambre.At(Ambre.Length + Fixed.FromInt(5)).Point);
        (GroundVector p, GroundVector h) = Ambre.At(Ambre.HaltDistance - Fixed.FromInt(1));
        Assert.True(h.X > Fixed.Zero && p.X < Fixed.FromInt(1310));
    }

    [Fact]
    public void Jour_d_attaque_halte_puis_epave_ou_reprise()
    {
        long d = DayWith(true);
        EventOccurrence o = WorldEventSchedule.InWindow(Zone.Id, Zone.Id, Ambre.Attack, d)!;
        Assert.Equal(d, o.Day);
        Assert.Equal(CaravanState.OnRoad, At(o.StartAt * 1000 - 1)!.State);
        CaravanView halted = At(o.StartAt * 1000 + 60_000)!;
        Assert.Equal((CaravanState.Attacked, P(1310, 870)), (halted.State, halted.Position));
        Assert.Equal(CaravanState.Attacked, At(o.EndAt * 1000 - 1, EventOutcome.Lost)!.State);
        // Pas encore réglée après la fin : toujours arrêtée.
        Assert.Equal(CaravanState.Attacked, At(o.EndAt * 1000 + 5_000)!.State);
        // Pillée : l'épave reste à la halte jusqu'à la fin des conséquences.
        CaravanView wreck = At(o.EndAt * 1000 + 5_000, EventOutcome.Lost)!;
        Assert.Equal((CaravanState.Pillaged, P(1310, 870)), (wreck.State, wreck.Position));
        Assert.Equal(CaravanState.Pillaged, At((o.EndAt + Ambre.Attack.AftermathMinutes) * 1000 - 1, EventOutcome.Lost)!.State);
        Assert.Null(At((o.EndAt + Ambre.Attack.AftermathMinutes) * 1000, EventOutcome.Lost));
        // Sauvée (ou passée, ou sans issue) : elle repart de la halte à la fin de l'attaque.
        foreach (EventOutcome outcome in new[] { EventOutcome.Won, EventOutcome.None })
        {
            Assert.Equal(Ambre.HaltDistance, At(o.EndAt * 1000, outcome)!.Distance);
            Assert.Equal(Ambre.HaltDistance + Fixed.Parse("11.25"), At((o.EndAt + 1) * 1000, outcome)!.Distance);
        }
    }

    [Fact]
    public void Gardes_tirage_fixe_et_proche_de_la_chance()
    {
        int held = 0, n = 0;
        for (long w = 0; w < 2000; w++)
            if (WorldEventSchedule.InWindow(Zone.Id, Zone.Id, Ambre.Attack, w) is EventOccurrence o)
            {
                n++;
                bool h = CaravanSchedule.GuardsHold(Zone.Id, Ambre.Attack, o);
                Assert.Equal(h, CaravanSchedule.GuardsHold(Zone.Id, Ambre.Attack, o));
                if (h) held++;
            }
        // 35 % des jours attaqués ; les gardes tiennent une fois sur deux (à 5 points près).
        Assert.InRange(n, 600, 800);
        Assert.InRange(held * 100 / n, 45, 55);
    }

    [Fact]
    public void Horloge_en_milliemes_de_minute_suit_l_heure_du_monde()
    {
        var clock = new WorldClock(50);
        foreach (long tick in new long[] { 0, 1, 149, 150, 151, 215_999, 216_000, 1_000_001 })
            Assert.Equal(WorldEventSchedule.Absolute(clock.At(tick)), CaravanSchedule.MilliAt(clock, tick) / CaravanSchedule.Milli);
        Assert.Throws<ArgumentOutOfRangeException>(() => CaravanSchedule.MilliAt(clock, -1));
    }

    [Fact]
    public void Dans_la_partie_issue_tiree_prime_et_contrecoup()
    {
        string dir = Path.Combine(Path.GetTempPath(), "caravanes_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            AdventureSession a = Adventure(Path.Combine(dir, "save.json"));
            WorldSession w = a.World;
            Assert.NotNull(w.CaravanBook);
            WorldEventDefinition e = w.Definition!.EventOf(Attack)!;
            Assert.Equal(WorldEventKind.Caravan, e.Kind);
            // Deux occurrences, l'une où les gardes tiennent et l'autre non.
            var all = Enumerable.Range(0, 400).Select(k => WorldEventSchedule.InWindow(w.Definition.Id, w.Definition.Id, e, k)).OfType<EventOccurrence>().ToList();
            EventOccurrence hold = all.First(o => CaravanSchedule.GuardsHold(w.Definition.Id, e, o));
            EventOccurrence fall = all.First(o => !CaravanSchedule.GuardsHold(w.Definition.Id, e, o));
            var notices = new List<WorldEventNotice>();
            foreach (EventOccurrence o in new[] { hold, fall }.OrderBy(o => o.Day))
            {
                notices.Clear();
                w.SettleEvent(e, o, EventOutcome.Lost, WorldEventNoticeKind.Missed, notices);
                SavedEvent entry = w.EventEntry(e);
                bool held = ReferenceEquals(o, hold);
                Assert.Equal(held ? EventOutcome.None : EventOutcome.Lost, entry.Outcome);
                Assert.Contains(notices, n => n.Kind == (held ? WorldEventNoticeKind.Escaped : WorldEventNoticeKind.Missed));
                Assert.Equal(held ? 0 : 1, notices.Count(n => n.Kind == WorldEventNoticeKind.Missed));
            }
            // Le texte de chaque moment existe.
            foreach (string moment in new[] { "name", "annonce", "victoire", "defaite", "absence", "passee", "recompense", "attente", "merci", "embuscade.debut", "archers.debut" })
                Assert.True(a.Texts.Has($"text.event.{Attack}.{moment}"), moment);
            Assert.Equal("passee", AdventureSession.Moment(WorldEventNoticeKind.Escaped));
            // Sauvée deux fois : récompense entière la première, prime seule la seconde ; un jour déjà réglé ne paie pas.
            EventOccurrence[] later = all.Where(o => o.Day > Math.Max(hold.Day, fall.Day)).Take(2).ToArray();
            long money = w.Money;
            w.SettleEvent(e, later[0], EventOutcome.Won, null, notices);
            Assert.Equal(money + e.Reward!.Money, w.Money);
            money = w.Money;
            notices.Clear();
            w.SettleEvent(e, later[1], EventOutcome.Won, null, notices);
            Assert.Equal(money + 200, w.Money);
            Assert.Contains(notices, n => n.Kind == WorldEventNoticeKind.Rewarded && n.Money == 200);
            w.SettleEvent(e, later[1], EventOutcome.Won, null, notices);
            Assert.Equal(money + 200, w.Money);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Contrecoup_des_marchands_quand_elle_est_pillee()
    {
        MerchantsLoadResult r = MerchantJsonReader.ReadFile(MerchantsPath, Texts, Book(Zone), ItemsCatalog);
        Assert.True(r.IsValid, r.Report.ToString());
        MarketShock s = r.Book!.Shocks.Single(x => x.Id == "caravane_pillee");
        Assert.Equal(("zone.vallons/" + Attack, "lost", 40, 125), (s.Event, s.Outcome, s.StockPercent, s.PricePercent));
        Assert.True(s.Touches("merchant.marchand_epices", "item.epices_du_sud"));
        Assert.True(s.Touches("merchant.mercerie", "item.bobine_de_fil"));
        Assert.False(s.Touches("merchant.mercerie", "item.cape_de_voyageur"));
    }

    // ---- Relecture indépendante de la PR #240 ----

    /// <summary>Le registre borne les événements à issue de toutes les zones reliées, attaques des caravanes comprises : refus au chargement.</summary>
    [Fact]
    public void Registre_des_zones_reliees_borne_avec_les_caravanes()
    {
        int own = Zone.Events.Count(e => e.Settles);
        Assert.Equal(2, own);
        // Zones reliées faites d'attaques du hameau sans cicatrice : 16 au plus par zone : la zone de départ (2 + 1 attaque), 16, 16, 16 et 13
        // font 64, tient ; 16, 16, 16 et 14 font 65, refusé.
        IReadOnlyList<ZoneDefinition> fits = AdventureSession.WithCaravans(new[] { Zone, Raids(16), Raids(16), Raids(16), Raids(13) }, Caravans);
        Assert.Equal(SavedEvent.MaxEntries, fits.Sum(z => z.Events.Count(e => e.Settles)));
        Assert.Same(Ambre.Attack, fits[0].EventOf(Attack));
        InvalidDataException x = Assert.Throws<InvalidDataException>(() => AdventureSession.WithCaravans(new[] { Zone, Raids(16), Raids(16), Raids(16), Raids(14) }, Caravans));
        Assert.Contains("caravanes", x.Message);
        Assert.Contains("65 événements à issue", x.Message);
    }

    /// <summary>La zone des Vallons avec <paramref name="n"/> copies de l'attaque du hameau pour seuls événements.</summary>
    private static ZoneDefinition Raids(int n)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        JsonArray events = root["data"]!["events"]!.AsArray();
        JsonNode raid = events.Single(e => (string?)e!["id"] == "attaque_hameau")!.DeepClone();
        events.Clear();
        for (int i = 0; i < n; i++)
        {
            JsonNode copy = raid.DeepClone();
            copy["id"] = $"attaque_hameau_{i:00}";
            events.Add(copy);
        }
        ZoneLoadResult r = ZoneJsonReader.Read(root.ToJsonString(), Path.Combine(AppContext.BaseDirectory, "scenarios"), ShippedData, ItemsCatalog);
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Zone!;
    }

    /// <summary>Décision de Damien du 21 novembre 2026 : le joueur présent qui perd laisse piller la caravane, même un jour où les gardes tiendraient.</summary>
    [Fact]
    public void Joueur_present_qui_perd_caravane_pillee()
    {
        string dir = Path.Combine(Path.GetTempPath(), "caravanes_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WorldSession w = Adventure(Path.Combine(dir, "save.json")).World;
            WorldEventDefinition e = w.Definition!.EventOf(Attack)!;
            EventOccurrence hold = Enumerable.Range(0, 400).Select(k => WorldEventSchedule.InWindow(w.Definition.Id, w.Definition.Id, e, k))
                .OfType<EventOccurrence>().First(o => CaravanSchedule.GuardsHold(w.Definition.Id, e, o));
            var notices = new List<WorldEventNotice>();
            w.SettleEvent(e, hold, EventOutcome.Lost, WorldEventNoticeKind.Lost, notices);
            Assert.Equal((EventOutcome.Lost, 1L), (w.EventEntry(e).Outcome, w.EventEntry(e).Lost));
            Assert.Contains(notices, n => n.Kind == WorldEventNoticeKind.Lost);
            Assert.DoesNotContain(notices, n => n.Kind == WorldEventNoticeKind.Escaped);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Une_caravane_tous_les_deux_jours()
    {
        CaravanLoadResult r = Read(c => c["everyDays"] = 2);
        Assert.True(r.IsValid, r.Report.ToString());
        Caravan c2 = r.Book!.Caravans[0];
        Assert.Equal(2, c2.Attack.EveryDays);
        Assert.True(c2.Runs(2));
        Assert.False(c2.Runs(3));
        Assert.False(c2.Runs(0));   // avant le premier jour
        for (long w = 0; w < 200; w++)
            if (WorldEventSchedule.InWindow(Zone.Id, Zone.Id, c2.Attack, w) is EventOccurrence o) Assert.True(c2.Runs(o.Day), $"jour {o.Day}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ZoneValidator.MaxEventBounty)]
    public void Prime_aux_bornes(int bounty)
    {
        CaravanLoadResult r = Read(c => c["attack"]!["reward"]!["bounty"] = bounty);
        Assert.True(r.IsValid, r.Report.ToString());
        Assert.Equal(bounty, r.Book!.Caravans[0].Attack.Reward!.Bounty);
    }

    [Fact]
    public void Prime_refusee_quand_la_bourse_est_pleine()
    {
        string dir = Path.Combine(Path.GetTempPath(), "caravanes_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WorldSession w = Adventure(Path.Combine(dir, "save.json")).World;
            WorldEventDefinition e = w.Definition!.EventOf(Attack)!;
            EventOccurrence[] days = Enumerable.Range(0, 400).Select(k => WorldEventSchedule.InWindow(w.Definition.Id, w.Definition.Id, e, k))
                .OfType<EventOccurrence>().Take(2).ToArray();
            var notices = new List<WorldEventNotice>();
            w.SettleEvent(e, days[0], EventOutcome.Won, null, notices);   // récompense entière
            // La bourse ne tient plus la prime (200 cuivre) : rien n'est payé, l'issue est gardée.
            Assert.True(((INarrativeWorld)w).GiveMoney(Coins.Max - 100 - w.Money));
            long money = w.Money;
            notices.Clear();
            w.SettleEvent(e, days[1], EventOutcome.Won, null, notices);
            Assert.Equal(money, w.Money);
            Assert.DoesNotContain(notices, n => n.Kind == WorldEventNoticeKind.Rewarded && n.Money == 200);
            Assert.Equal((EventOutcome.Won, 2L), (w.EventEntry(e).Outcome, w.EventEntry(e).Won));
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>Halte atteinte à la minute 1439 : acceptée ; à 1440 : refusée. Voyage (attaque comprise) de 1439 minutes : accepté ; 1440 : refusé.</summary>
    [Theory]
    [InlineData(1439, true)]
    [InlineData(1440, false)]
    public void Halte_et_voyage_avant_minuit(int minute, bool valid)
    {
        long reach = CaravanValidator.MinutesFor(Ambre.HaltDistance, Ambre.MetersPerMinute);
        long start = minute - reach;
        CaravanLoadResult halt = Read(c => c["start"] = $"{start / 60:00}:{start % 60:00}");
        Assert.Equal(valid, halt.IsValid);
        if (!valid) Assert.Contains(halt.Report.Errors, x => x.Path == "$.data.caravans[0].start");
        // Vitesse telle que la route entière dure minute − 90 (durée de l'attaque) minutes.
        long travel = minute - Ambre.Attack.DurationMinutes;
        long raw = (Ambre.Length.Raw + travel - 1) / travel;
        Fixed speed = Fixed.Parse((raw / 10000m).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(travel, CaravanValidator.MinutesFor(Ambre.Length, speed));
        CaravanLoadResult trip = Read(c => { c["start"] = "00:00"; c["metersPerMinute"] = raw / 10000m; });
        Assert.Equal(valid, trip.IsValid);
        if (!valid) Assert.Contains(trip.Report.Errors, x => x.Path == "$.data.caravans[0].metersPerMinute");
    }
}
