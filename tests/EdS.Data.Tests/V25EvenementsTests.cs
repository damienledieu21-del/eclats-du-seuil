using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.5 (événements du monde : attaque du hameau, faille du Seuil, chasse de l'aube) : zones en schéma 10 (bloc <c>events</c>,
/// schémas 9, 8 et 7 encore lus), sauvegarde v13 (registre des événements du personnage, v12 encore lue et mise à jour), calendrier tiré du
/// temps du monde, arrivées hors de la réserve des quêtes, issue réglée une seule fois par personnage, récompense de la première réussite
/// donnée une seule fois (due si le sac est plein), villageois au refuge, conséquences visibles d'un pillage, répliques des PNJ, essai.
/// </summary>
[Trait("Category", "World")]
public class V25EvenementsTests
{
    private static readonly ZoneDefinition Vallons = Zone();
    private static readonly WorldClock Clock = new(50);
    private const string Raid = "attaque_hameau", Rift = "faille_du_seuil", Hunt = "chasse_de_l_aube";

    private static JsonObject ZoneJson() => JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();

    private static ZoneLoadResult Read(JsonObject root) => ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog);

    private static ZoneLoadResult Mutated(Action<JsonObject> mutate)
    {
        JsonObject root = ZoneJson();
        mutate(root);
        return Read(root);
    }

    private static JsonObject Event(JsonObject root, int index) => root["data"]!["events"]![index]!.AsObject();

    private static void AssertError(ZoneLoadResult r, string path, ValidationCode? code = null)
    {
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == path && (code is null || e.Code == code));
    }

    /// <summary>Vallons où l'attaque (ou la faille) n'a qu'une bête faible du pré, à <paramref name="x"/>, <paramref name="z"/> ; la faille s'ouvre à côté.</summary>
    private static ZoneDefinition Weak(double x, double z, double riftX = 0, double riftZ = 0, int? riftWaves = null)
    {
        ZoneLoadResult r = Mutated(root =>
        {
            if (riftWaves is int waves) Event(root, 1)["waves"] = waves;
            Event(root, 0)["minLevel"] = 1;   // le personnage d'essai est de niveau 1 (l'attaque livrée demande le niveau 6)
            foreach (int i in new[] { 0, 1 })
                foreach (JsonNode? g in Event(root, i)["groups"]!.AsArray())
                {
                    g!["creatures"] = new JsonArray(new JsonObject { ["like"] = "loup1", ["position"] = new JsonArray(x, z) });
                    if (i == 1) g["position"] = new JsonArray(riftX, riftZ);
                }
        });
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Zone!;
    }

    private static WorldSession Session(ZoneDefinition zone, long tick, SavedCharacter? hero = null, string safePoint = "hameau_de_la_carriere") =>
        new(new WorldSave("zone.vallons", tick, 0, new[] { PlayerCharacters.PlacesKey }, hero ?? Hero()), zone, ShippedData, Progression, ItemsCatalog, Book(zone),
            safePoint: safePoint);

    /// <summary>Choix de Damien du 7 novembre 2026 : un personnage au niveau minimum de l'attaque du hameau (6).</summary>
    private static SavedCharacter Veteran()
    {
        long xp = 0;
        while (Progression.LevelFor(xp) < 6) xp += 10;
        return Hero().WithXp(xp);
    }

    private static EventOccurrence First(ZoneDefinition zone, string id, long fromMinute = 1440) =>
        WorldEventSchedule.Next(zone.Id, zone.Id, zone.EventOf(id)!, fromMinute)!;

    private static List<WorldEventNotice> Run(WorldSession w, int ticks)
    {
        var notices = new List<WorldEventNotice>();
        for (int i = 0; i < ticks; i++) notices.AddRange(w.Step().Events);
        return notices;
    }

    /// <summary>Marche vers (<paramref name="x"/>, <paramref name="z"/>) pendant au plus <paramref name="ticks"/> ticks (arrêt à 1 m).</summary>
    private static List<WorldEventNotice> WalkTo(WorldSession w, double x, double z, int ticks)
    {
        var notices = new List<WorldEventNotice>();
        CombatEngine e = w.Zone.Engine;
        for (int i = 0; i < ticks; i++)
        {
            GroundVector p = e.View("bastion").Position!.Value;
            double dx = x - p.X.Raw / 1e4, dz = z - p.Z.Raw / 1e4, d = Math.Sqrt(dx * dx + dz * dz);
            try
            {
                e.Submit(d < 1 ? new CombatCommand(e.CurrentTick * 50, "bastion", CombatAction.Stop)
                    : new CombatCommand(e.CurrentTick * 50, "bastion", new GroundVector(Fixed.FromRaw((long)(dx / d * 1e4)), Fixed.FromRaw((long)(dz / d * 1e4)))));
            }
            catch (ArgumentException) { }
            notices.AddRange(w.Step().Events);
        }
        return notices;
    }

    /// <summary>Combat simulé contre les créatures des événements (compétences dès qu'elles sont prêtes, marche vers la cible).</summary>
    private static List<WorldEventNotice> Fight(WorldSession session, int seconds = 120)
    {
        var notices = new List<WorldEventNotice>();
        CombatEngine e = session.Zone.Engine;
        for (int i = 0; i < seconds * 20; i++)
        {
            string? target = session.WorldEvents.SelectMany(v => v.Creatures).FirstOrDefault(t => e.View(t).IsPresent && !e.View(t).IsDead);
            CombatantView me = e.View("bastion");
            if (me.IsDead || target is null) break;
            CombatantView it = e.View(target);
            long now = e.CurrentTick * 50;
            string[] skills = session.Character.ActiveSkills.Append(session.Character.Ultimate).OfType<string>().ToArray();
            try { e.Submit(new CombatCommand(now, "bastion", skills[i % skills.Length], target)); }
            catch (ArgumentException) { }
            if (me.Position is GroundVector a && it.Position is GroundVector b)
            {
                double dx = (b.X.Raw - a.X.Raw) / 1e4, dz = (b.Z.Raw - a.Z.Raw) / 1e4, d = Math.Sqrt(dx * dx + dz * dz);
                try
                {
                    e.Submit(d > 2.2
                        ? new CombatCommand(now, "bastion", new GroundVector(Fixed.FromRaw((long)(dx / d * 1e4)), Fixed.FromRaw((long)(dz / d * 1e4))))
                        : new CombatCommand(now, "bastion", CombatAction.Stop));
                }
                catch (ArgumentException) { }
            }
            notices.AddRange(session.Step().Events);
        }
        return notices;
    }

    // --- Formats --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Versions_du_sous_lot()
    {
        // V2.6 : zones en schéma 11 (10 encore lu), sauvegarde v14, Vallons au contenu 0.14.0.
        Assert.Equal((11, 10, 9, 8, 7), (ZoneValidator.CurrentSchemaVersion, ZoneValidator.EventsSchemaVersion, ZoneValidator.WeatherSchemaVersion, ZoneValidator.PreviousSchemaVersion, ZoneValidator.OldestSchemaVersion));
        Assert.Equal((14, 13), (SaveValidator.CurrentSchemaVersion, SaveValidator.EventsSince));
        Assert.Equal((11, "0.14.0-provisoire"), (Vallons.SchemaVersion, Vallons.ContentVersion));
        // Ni le moteur, ni les règles, ni le journal ne changent.
        Assert.Equal(("0.28.0", 22, 16), (CombatEngine.Version, CombatEngine.LogFormatVersion, ScenarioValidator.CurrentSchemaVersion));
    }

    [Fact]
    public void Evenements_livres()
    {
        Assert.Equal(new[] { Raid, Rift, Hunt }, Vallons.Events.Select(e => e.Id));
        WorldEventDefinition raid = Vallons.EventOf(Raid)!, rift = Vallons.EventOf(Rift)!, hunt = Vallons.EventOf(Hunt)!;
        Assert.Equal((WorldEventKind.Raid, 2, 70, 20 * 60 + 30, 60, 90, 240, "hameau"), (raid.Kind, raid.EveryDays, raid.ChancePercent, raid.StartMinute, raid.SpreadMinutes,
            raid.AnnounceMinutes, raid.DurationMinutes, raid.Place));
        Assert.Equal((6, 1, 1), (raid.MinLevel, rift.MinLevel, hunt.MinLevel));   // choix de Damien du 7 novembre 2026
        Assert.Equal(new[] { "npc.doyen", "npc.herboriste_hameau" }, raid.Villagers);
        Assert.Equal(new[] { "bandits", "loups" }, raid.Groups.Select(g => g.Id));
        Assert.Equal((400L, 300L, "fact.event.attaque_hameau.defendu", "fact.event.attaque_hameau.pille"), (raid.Reward!.Xp, raid.Reward.Money, raid.Reward.Fact, raid.LostFact));
        Assert.Equal((WorldEventKind.Rift, 3, 30000L), (rift.Kind, rift.Waves, rift.WaveMs));
        Assert.All(rift.Groups, g => Assert.NotNull(g.Position));
        Assert.All(rift.Groups.SelectMany(g => g.Creatures), c => Assert.Equal("echo", Vallons.PopulationOf(c.Like)!.Family));
        Assert.Equal((WorldEventKind.Hunt, (WorldEventReward?)null, false), (hunt.Kind, hunt.Reward, hunt.Settles));
        Assert.All(hunt.Groups.SelectMany(g => g.Creatures), c => Assert.Equal("meute", Vallons.PopulationOf(c.Like)!.Template.Team));
        // Chaque créature de la chasse voit un sanglier des champs à sa distance d'engagement (7 m pour un loup) : la chasse commence seule.
        foreach (WorldEventCreature c in hunt.Groups.SelectMany(g => g.Creatures))
        {
            double best = new[] { "sanglier4", "sanglier5" }.Select(b => Vallons.PopulationOf(b)!.Template.Placement!.Position)
                .Min(p => Math.Sqrt(Math.Pow((p.X.Raw - c.Position.X.Raw) / 1e4, 2) + Math.Pow((p.Z.Raw - c.Position.Z.Raw) / 1e4, 2)));
            Assert.InRange(best, 2.0, 7.0);
        }
    }

    [Fact]
    public void Textes_de_chaque_moment()
    {
        var a = new[] { "victoire", "absence", "recompense", "attente", "merci" };
        foreach (WorldEventDefinition e in Vallons.Events)
        {
            Assert.True(Texts.Has($"text.event.{e.Id}.name"), e.Id);
            foreach (WorldEventGroup g in e.Groups)
            {
                Assert.True(Texts.Has($"text.event.{e.Id}.{g.Id}.debut") || Texts.Has($"text.event.{e.Id}.debut"), e.Id + g.Id);
                if (e.AnnounceMinutes > 0) Assert.True(Texts.Has($"text.event.{e.Id}.{g.Id}.annonce") || Texts.Has($"text.event.{e.Id}.annonce"), e.Id + g.Id);
                if (e.Settles) Assert.True(Texts.Has($"text.event.{e.Id}.{g.Id}.defaite") || Texts.Has($"text.event.{e.Id}.defaite"), e.Id + g.Id);
            }
            if (e.Settles) Assert.All(a, m => Assert.True(Texts.Has($"text.event.{e.Id}.{m}"), e.Id + m));
        }
        Assert.True(Texts.Has("text.event.faille_du_seuil.vague") && Texts.Has("text.event.chasse_de_l_aube.fin") && Texts.Has("text.activity.refuge"));
        Assert.All(new[] { "reward", "rift.close", "rift.too_far", "rift.echoes", "under_way", "coming" }, k => Assert.True(Texts.Has("text.ui.event." + k), k));
    }

    [Fact]
    public void Schema_9_encore_lu_sans_evenements()
    {
        ZoneLoadResult r = Mutated(root =>
        {
            root["schemaVersion"] = 9;
            root["data"]!.AsObject().Remove("events");
        });
        Assert.True(r.IsValid, r.Report.ToString());
        Assert.Empty(r.Zone!.Events);
        AssertError(Mutated(root => root["schemaVersion"] = 9), "$.data.events", ValidationCode.InvalidValue);
        AssertError(Mutated(root => root["data"]!["events"] = new JsonArray()), "$.data.events", ValidationCode.OutOfRange);
    }

    [Theory]
    [InlineData(0, "kind", "\"siege\"", ".kind")]
    [InlineData(0, "chancePercent", "0", ".chancePercent")]
    [InlineData(0, "everyDays", "31", ".everyDays")]
    [InlineData(0, "start", "\"25:00\"", ".start")]
    [InlineData(0, "durationMinutes", "5", ".durationMinutes")]
    [InlineData(0, "place", "\"nulle_part\"", ".place")]
    [InlineData(0, "waves", "2", ".waves")]
    [InlineData(0, "lostFact", "\"pille\"", ".lostFact")]
    [InlineData(2, "reward", "{\"xp\": 1, \"money\": 1}", ".reward")]
    [InlineData(2, "aftermathMinutes", "60", ".aftermathMinutes")]
    [InlineData(1, "waveMs", "1025", ".waveMs")]
    [InlineData(0, "weather", "{\"kinds\": [\"mist\"], \"sign\": \"text.x\"}", ".weather.sign")]
    [InlineData(0, "minLevel", "0", ".minLevel")]
    [InlineData(1, "minLevel", "1000", ".minLevel")]
    public void Refus_au_chemin_precis(int index, string field, string json, string path)
    {
        AssertError(Mutated(root => Event(root, index)[field] = JsonNode.Parse(json)), $"$.data.events[{index}]{path}");
    }

    [Fact]
    public void Refus_des_varietes_et_des_creatures()
    {
        string g0 = "$.data.events[0].groups[0]";
        AssertError(Mutated(root => Event(root, 0)["groups"]![0]!["creatures"]![0]!["like"] = "dragon"), g0 + ".creatures[0].like", ValidationCode.UnknownReference);
        AssertError(Mutated(root => Event(root, 0)["groups"]![0]!["creatures"]![0]!["like"] = "cerf1"), g0 + ".creatures[0].like", ValidationCode.InvalidValue);
        AssertError(Mutated(root => Event(root, 0)["groups"]![0]!["creatures"]![0]!["like"] = "loup_blanc"), g0 + ".creatures[0].like", ValidationCode.InvalidValue);
        AssertError(Mutated(root => Event(root, 2)["groups"]![0]!["creatures"]![0]!["like"] = "bandit_epee3"), "$.data.events[2].groups[0].creatures[0].like");
        // Dans une maison du hameau.
        AssertError(Mutated(root => Event(root, 0)["groups"]![0]!["creatures"]![0]!["position"] = new JsonArray(80, -29)), g0 + ".creatures[0].position");
        AssertError(Mutated(root => Event(root, 0)["groups"]![0]!["creatures"]![1]!["position"] = new JsonArray(97, -22)), g0 + ".creatures[1].position", ValidationCode.DuplicateId);
        AssertError(Mutated(root => Event(root, 0)["groups"]![0]!["position"] = new JsonArray(90, -30)), g0 + ".position");
        AssertError(Mutated(root => Event(root, 1)["groups"]![0]!.AsObject().Remove("position")), "$.data.events[1].groups[0].position");
        AssertError(Mutated(root => Event(root, 0)["groups"]![1]!["id"] = "bandits"), "$.data.events[0].groups[1].id", ValidationCode.DuplicateId);
        AssertError(Mutated(root => Event(root, 0)["groups"]![0]!["weight"] = 0), g0 + ".weight", ValidationCode.OutOfRange);
        AssertError(Mutated(root => Event(root, 0).Remove("villagers")), "$.data.events[0].villagers");
        AssertError(Mutated(root => Event(root, 1)["refuge"] = new JsonArray(0, 0)), "$.data.events[1].refuge");
        AssertError(Mutated(root => Event(root, 0)["villagers"]![1] = "npc.doyen"), "$.data.events[0].villagers[1]", ValidationCode.DuplicateId);
        AssertError(Mutated(root => Event(root, 0).Remove("reward")), "$.data.events[0].reward", ValidationCode.MissingField);
        AssertError(Mutated(root => Event(root, 0)["reward"]!["items"]![0]!["item"] = "item.rien"), "$.data.events[0].reward.items[0].item", ValidationCode.UnknownReference);
        AssertError(Mutated(root => Event(root, 0)["reward"]!["xp"] = -1), "$.data.events[0].reward.xp", ValidationCode.OutOfRange);
        AssertError(Mutated(root => Event(root, 1)["id"] = Raid), "$.data.events[1].id", ValidationCode.DuplicateId);
    }

    // --- Calendrier -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Calendrier_deterministe_et_borne()
    {
        WorldEventDefinition raid = Vallons.EventOf(Raid)!;
        int seen = 0;
        for (long w = 0; w < 300; w++)
        {
            EventOccurrence? a = WorldEventSchedule.InWindow(Vallons.Id, Vallons.Id, raid, w), b = WorldEventSchedule.InWindow(Vallons.Id, Vallons.Id, raid, w);
            Assert.Equal(a?.ToString(), b?.ToString());
            if (a is null) continue;
            seen++;
            Assert.InRange(a.Day, Math.Max(raid.FirstDay, w * 2), w * 2 + 1);
            Assert.InRange(a.StartAt % 1440, 20 * 60 + 30, 21 * 60 + 30);
            Assert.Equal((90L, 240L), (a.StartAt - a.AnnounceAt, a.EndAt - a.StartAt));
        }
        Assert.InRange(seen, 180, 240);   // 70 % des fenêtres
        // Aucune occurrence avant le premier jour.
        Assert.All(Vallons.Events, e => Assert.True(First(Vallons, e.Id, 0).Day >= e.FirstDay));
        // Une autre zone a d'autres tirages.
        Assert.NotEqual(Enumerable.Range(0, 40).Select(w => WorldEventSchedule.InWindow("zone.vallons", "zone.vallons", raid, w)?.Day),
            Enumerable.Range(0, 40).Select(w => WorldEventSchedule.InWindow("zone.autre", "zone.vallons", raid, w)?.Day));
    }

    [Fact]
    public void Tick_d_une_minute_du_monde_exact()
    {
        for (long m = 480; m < 480 + 3 * 1440; m += 37)
        {
            long t = WorldEventSchedule.TickAt(Clock, m);
            Assert.Equal(m, WorldEventSchedule.Absolute(Clock.At(t)));
            if (t > 0) Assert.True(WorldEventSchedule.Absolute(Clock.At(t - 1)) < m);
        }
        Assert.Equal(0, WorldEventSchedule.TickAt(Clock, 0));
    }

    // --- Attaque du hameau ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Annonce_cloche_refuge_puis_pillage_si_personne_ne_vient()
    {
        EventOccurrence o = First(Vallons, Raid);
        WorldSession w = Session(Vallons, WorldEventSchedule.TickAt(Clock, o.AnnounceAt), Veteran(), safePoint: "place_du_village");
        List<WorldEventNotice> notices = Run(w, 2);
        Assert.Equal((WorldEventNoticeKind.Announced, o.Group.Id), (notices.Single().Kind, notices.Single().Group));
        Assert.Equal(WorldEventPhase.Announced, w.WorldEvents.Single().Phase);
        Assert.Equal("Bientôt : L'attaque du hameau", string.Format(Texts.Get("text.ui.event.coming"), Texts.Get("text.event.attaque_hameau.name")));
        notices = Run(w, checked((int)(WorldEventSchedule.TickAt(Clock, o.StartAt) - WorldEventSchedule.TickAt(Clock, o.AnnounceAt))));
        Assert.Contains(notices, n => n.Kind == WorldEventNoticeKind.Started);
        Run(w, 3);
        WorldEventView v = w.WorldEvents.Single();
        Assert.Equal((WorldEventPhase.Running, 3), (v.Phase, v.Creatures.Count));
        Assert.All(v.Creatures, c => Assert.True(w.IsEventCreature(c) && c.Contains("_ev") && w.Zone.Engine.View(c).IsPresent));
        // Les villageois du hameau sont au refuge, au pied de la carrière.
        StoryBook book = w.Narrative!.Book;
        Assert.Equal(new GroundVector(Fixed.Parse("61.5"), Fixed.FromInt(-22)), w.NpcPosition(book.Npc("npc.doyen")));
        Assert.Equal(WorldSession.RefugeActivity, w.NpcActivity(book.Npc("npc.herboriste_hameau")));
        // Personne ne vient : à la fin, les assaillants repartent et le hameau est pillé (une seule fois).
        notices = Run(w, checked((int)(WorldEventSchedule.TickAt(Clock, o.EndAt) - WorldEventSchedule.TickAt(Clock, o.StartAt) + 5)));
        Assert.Equal(1, notices.Count(n => n.Kind == WorldEventNoticeKind.Lost));
        Assert.Empty(w.WorldEvents);
        Assert.All(v.Creatures, c => Assert.True(w.Zone.Engine.View(c).HasLeft));
        SavedEvent entry = w.EventRegistry.Single(e => e.Id == "zone.vallons/attaque_hameau");
        Assert.Equal((0L, 1L, o.Day, EventOutcome.Lost, false), (entry.Won, entry.Lost, entry.Last, entry.Outcome, entry.Owed));
        Assert.Contains("fact.event.attaque_hameau.pille", w.Narrative.ToSaved().Facts);
        Assert.True(w.Aftermath(Raid));
        Assert.NotEqual(w.NpcPosition(book.Npc("npc.doyen")), new GroundVector(Fixed.Parse("61.5"), Fixed.FromInt(-22)));
        // Les conséquences durent une journée du monde, puis disparaissent ; rechargée, la partie ne règle pas deux fois.
        WorldSession later = Session(Vallons, WorldEventSchedule.TickAt(Clock, o.EndAt + 1500), w.Save().Character);
        Assert.DoesNotContain(Run(later, 2), n => n.Kind is WorldEventNoticeKind.Lost or WorldEventNoticeKind.Missed);
        Assert.False(later.Aftermath(Raid));
        Assert.Equal(1L, later.EventRegistry.Single(e => e.Id == "zone.vallons/attaque_hameau").Lost);
    }

    [Fact]
    public void Defendre_le_hameau_recompense_une_seule_fois()
    {
        ZoneDefinition zone = Weak(86, -38);
        EventOccurrence o = First(zone, Raid);
        WorldSession w = Session(zone, WorldEventSchedule.TickAt(Clock, o.StartAt) - 5);
        long money = w.Money;
        List<WorldEventNotice> notices = Run(w, 10);
        notices.AddRange(Fight(w));
        notices.AddRange(Run(w, 3));
        Assert.Contains(notices, n => n.Kind == WorldEventNoticeKind.Won);
        WorldEventNotice reward = notices.Single(n => n.Kind == WorldEventNoticeKind.Rewarded);
        Assert.Equal(300L, reward.Money);
        Assert.Equal(new[] { ("item.remede_de_l_aube", 2) }, reward.Items);
        Assert.Equal(money + 300, w.Money);
        Assert.Equal(2, w.Inventory.Stacks.Where(s => s.Item == "item.remede_de_l_aube").Sum(s => s.Count));
        Assert.Contains("fact.event.attaque_hameau.defendu", w.Narrative!.ToSaved().Facts);
        SavedEvent entry = w.EventRegistry.Single(e => e.Id == "zone.vallons/attaque_hameau");
        Assert.Equal((1L, 0L, EventOutcome.Won, false), (entry.Won, entry.Lost, entry.Outcome, entry.Owed));
        Assert.Empty(w.WorldEvents);
        // Les villageois sortent du refuge dès la victoire.
        Assert.NotEqual(WorldSession.RefugeActivity, w.NpcActivity(w.Narrative!.Book.Npc("npc.doyen")));
        // La sauvegarde (v13) garde le registre ; la même occurrence n'est plus jamais réglée.
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!;
        WorldSession again = Session(zone, WorldEventSchedule.TickAt(Clock, o.StartAt) + 100, saved.Character);
        Assert.DoesNotContain(Run(again, 20), n => n.Kind != WorldEventNoticeKind.Announced);
        Assert.DoesNotContain(again.WorldEvents, x => x.Event.Id == Raid);
        // L'occurrence suivante, défendue aussi : des remerciements, plus de récompense.
        EventOccurrence next = First(zone, Raid, o.EndAt);
        WorldSession second = Session(zone, WorldEventSchedule.TickAt(Clock, next.StartAt) - 5, saved.Character);
        long before = second.Money;
        List<WorldEventNotice> n2 = Run(second, 10);
        n2.AddRange(Fight(second));
        n2.AddRange(Run(second, 3));
        Assert.Contains(n2, n => n.Kind == WorldEventNoticeKind.Thanks);
        Assert.DoesNotContain(n2, n => n.Kind == WorldEventNoticeKind.Rewarded);
        Assert.Equal(before, second.Money);
        Assert.Equal(2L, second.EventRegistry.Single(e => e.Id == "zone.vallons/attaque_hameau").Won);
    }

    [Fact]
    public void Sac_plein_la_recompense_attend_et_la_sauvegarde_la_garde()
    {
        ZoneDefinition zone = Weak(86, -38);
        EventOccurrence o = First(zone, Raid);
        int places = ItemsCatalog.InventorySize;
        int stack = ItemsCatalog.Item("item.potion_de_soin").Stack;
        SavedCharacter full = Hero(0, Enumerable.Range(0, places).Select(_ => new ItemStack("item.potion_de_soin", stack)).ToArray());
        WorldSession w = Session(zone, WorldEventSchedule.TickAt(Clock, o.StartAt) - 5, full);
        List<WorldEventNotice> notices = Run(w, 10);
        notices.AddRange(Fight(w));
        notices.AddRange(Run(w, 3));
        Assert.Contains(notices, n => n.Kind == WorldEventNoticeKind.Won);
        Assert.DoesNotContain(notices, n => n.Kind == WorldEventNoticeKind.Rewarded);
        Assert.Equal(1, notices.Count(n => n.Kind == WorldEventNoticeKind.RewardWaiting));
        Assert.Equal(0, w.Money);   // rien n'est donné en partie : en entier ou pas du tout
        SavedEvent entry = w.EventRegistry.Single(e => e.Id == "zone.vallons/attaque_hameau");
        Assert.True(entry.Owed);
        string json = SaveJsonReader.Write(w.Save());
        Assert.Contains("\"owed\": true", json);
        // Session suivante, sac vidé d'une place : la récompense est donnée au premier tick.
        WorldSave saved = SaveJsonReader.Read(json).Save!;
        SavedCharacter lighter = saved.Character!.WithItems(saved.Character.Inventory.Skip(1), saved.Character.Equipment);
        WorldSession next = Session(zone, saved.WorldTick, lighter);
        List<WorldEventNotice> n2 = Run(next, 2);
        Assert.Single(n2, n => n.Kind == WorldEventNoticeKind.Rewarded);
        Assert.Equal(300, next.Money);
        Assert.False(next.EventRegistry.Single(e => e.Id == "zone.vallons/attaque_hameau").Owed);
    }

    [Fact]
    public void Manque_pendant_l_absence_et_nouveau_personnage()
    {
        EventOccurrence o = First(Vallons, Raid);
        long after = WorldEventSchedule.TickAt(Clock, o.EndAt + 30);
        // Un personnage qui découvre les événements : rien de passé ne compte contre lui (première ligne du registre).
        WorldSession fresh = Session(Vallons, after, Veteran());
        Assert.DoesNotContain(Run(fresh, 2), n => n.Kind == WorldEventNoticeKind.Missed);
        Assert.Equal((o.Day, EventOutcome.None), (fresh.EventRegistry.Single(e => e.Id == "zone.vallons/attaque_hameau").Last,
            fresh.EventRegistry.Single(e => e.Id == "zone.vallons/attaque_hameau").Outcome));
        // Un personnage qui connaissait le hameau et n'est pas venu : manqué, annoncé à son retour.
        SavedCharacter known = Veteran().WithEvents(new[] { new SavedEvent("zone.vallons/attaque_hameau", 0, 0, o.Day - 1, EventOutcome.None) });
        WorldSession back = Session(Vallons, after, known);
        WorldEventNotice missed = Run(back, 2).Single(n => n.Event == Raid);
        Assert.Equal((WorldEventNoticeKind.Missed, o.Day), (missed.Kind, missed.Day));
        Assert.Equal("text.event.attaque_hameau.absence", new[] { "absence" }.Select(m => $"text.event.{Raid}.{m}").Single());
        Assert.Equal(1L, back.EventRegistry.Single(e => e.Id == "zone.vallons/attaque_hameau").Lost);
    }

    [Fact]
    public void Memes_entrees_memes_evenements()
    {
        EventOccurrence o = First(Vallons, Raid);
        string Trace()
        {
            WorldSession w = Session(Vallons, WorldEventSchedule.TickAt(Clock, o.StartAt) - 3, Veteran());
            var lines = Run(w, 400).Select(n => $"{n.Kind}:{n.Event}:{n.Group}:{n.Day}").ToList();
            return string.Join("|", lines) + "#" + string.Join(",", w.Zone.Engine.CombatantIds) + "#" + w.Zone.Engine.Snapshot().Text;
        }
        Assert.Equal(Trace(), Trace());
    }

    [Fact]
    public void Les_creatures_comptent_pour_leur_famille_jamais_pour_leur_modele()
    {
        // Une créature d'événement prend le butin et la famille de son modèle, sous une identité à elle (le meneur des bandits d'une quête
        // reste à vaincre lui-même).
        EventOccurrence o = First(Vallons, Raid);
        WorldSession w = Session(Vallons, WorldEventSchedule.TickAt(Clock, o.StartAt) - 2, Veteran());
        Run(w, 5);
        string c = w.WorldEvents.Single().Creatures[0];
        Assert.NotEqual(o.Group.Creatures[0].Like, c);
        Assert.StartsWith(o.Group.Creatures[0].Like + "_ev", c);
        Assert.Null(Vallons.PopulationOf(c));
    }

    // --- Faille du Seuil -----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Faille_refermee_apres_ses_echos()
    {
        // La faille s'ouvre à 2 m du camp de la carrière (point sûr), un loup faible en sort.
        ZoneDefinition zone = Weak(29, -12, 26, -13, riftWaves: 2);
        EventOccurrence o = First(zone, Rift);
        WorldSession w = Session(zone, WorldEventSchedule.TickAt(Clock, o.StartAt) - 2, Inventory.Outfit(Hero(), ItemsCatalog).WithXp(300), safePoint: "camp_de_la_carriere");
        List<WorldEventNotice> notices = Run(w, 4);
        Assert.Contains(notices, n => n.Kind == WorldEventNoticeKind.Started);
        WorldEventView v = w.WorldEvents.Single(x => x.Event.Id == Rift);
        Assert.Equal(new GroundVector(Fixed.FromInt(26), Fixed.FromInt(-13)), v.Rift);
        string before = Snapshot(w) + string.Join(",", w.EventRegistry.Select(x => $"{x.Id}{x.Won}{x.Last}")) + w.WorldEvents.Count;
        Assert.Equal(RiftRefusal.EchoesRemain, w.CloseRift(Rift));
        Assert.Equal(RiftRefusal.NoRift, w.CloseRift(Raid));
        Assert.Equal(before, Snapshot(w) + string.Join(",", w.EventRegistry.Select(x => $"{x.Id}{x.Won}{x.Last}")) + w.WorldEvents.Count);   // refus atomiques
        notices.AddRange(Fight(w));
        // Relecture de la PR #107 : la première vague vaincue ne suffit pas : la suivante sort aussitôt, et la faille ne se referme qu'après
        // toutes ses vagues, chaque écho vaincu.
        Assert.Equal(RiftRefusal.WavesRemain, w.CloseRift(Rift));
        notices.AddRange(Run(w, 1));
        Assert.Equal(RiftRefusal.EchoesRemain, w.CloseRift(Rift));   // la vague 2 sort aussitôt
        for (int k = 0; k < 2; k++)
        {
            notices.AddRange(WalkTo(w, 26, -13, 40));   // libérer la place de sortie des échos
            notices.AddRange(Fight(w));
        }
        Assert.Equal(new[] { 2 }, notices.Where(n => n.Kind == WorldEventNoticeKind.Wave).Select(n => n.Wave));
        // Revenir près de la faille, puis la refermer.
        CombatEngine e = w.Zone.Engine;
        for (int i = 0; i < 200 && w.CloseRift(Rift) == RiftRefusal.TooFar; i++)
        {
            GroundVector p = e.View("bastion").Position!.Value;
            double dx = 26 - p.X.Raw / 1e4, dz = -13 - p.Z.Raw / 1e4, d = Math.Sqrt(dx * dx + dz * dz);
            e.Submit(new CombatCommand(e.CurrentTick * 50, "bastion", new GroundVector(Fixed.FromRaw((long)(dx / d * 1e4)), Fixed.FromRaw((long)(dz / d * 1e4)))));
            notices.AddRange(w.Step().Events);
        }
        notices.AddRange(Run(w, 2));
        Assert.Contains(notices, n => n.Kind == WorldEventNoticeKind.Won && n.Event == Rift);
        Assert.Contains(notices, n => n.Kind == WorldEventNoticeKind.Rewarded && n.Event == Rift && n.Items.Single().Item == "item.eclat_de_seuil");
        Assert.DoesNotContain(w.WorldEvents, x => x.Event.Id == Rift);
        Assert.DoesNotContain(Run(w, 30 * 20 + 40), n => n.Kind == WorldEventNoticeKind.Wave);
        Assert.Equal(RiftRefusal.NoRift, w.CloseRift(Rift));
    }

    [Fact]
    public void Faille_non_refermee_vagues_puis_cicatrice()
    {
        EventOccurrence o = First(Vallons, Rift);
        WorldSession w = Session(Vallons, WorldEventSchedule.TickAt(Clock, o.StartAt) - 2, safePoint: "place_du_village");
        List<WorldEventNotice> notices = Run(w, 3 + 2 * 600 + 5);
        Assert.Equal(new[] { 2, 3 }, notices.Where(n => n.Kind == WorldEventNoticeKind.Wave).Select(n => n.Wave));
        // Personne ne les combat : les échos des vagues suivantes attendent que leur place se libère (aucune arrivée sur une autre).
        Assert.Equal(3, w.WorldEvents.Single(x => x.Event.Id == Rift).Creatures.Count);
        Assert.Equal(RiftRefusal.TooFar, w.CloseRift(Rift));
        notices = Run(w, checked((int)(WorldEventSchedule.TickAt(Clock, o.EndAt) - WorldEventSchedule.TickAt(Clock, o.StartAt))));
        Assert.Single(notices, n => n.Kind == WorldEventNoticeKind.Lost && n.Event == Rift);
        Assert.Contains("fact.event.faille_du_seuil.restee_ouverte", w.Narrative!.ToSaved().Facts);
    }

    // --- Chasse de l'aube ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Chasse_de_l_aube_sans_mort_ni_registre()
    {
        EventOccurrence o = First(Vallons, Hunt);
        WorldSession w = Session(Vallons, WorldEventSchedule.TickAt(Clock, o.StartAt) - 2, safePoint: "place_du_village");
        Run(w, 3);
        WorldEventView v = w.WorldEvents.Single(x => x.Event.Id == Hunt);
        Assert.Equal(2, v.Creatures.Count);
        CombatEngine e = w.Zone.Engine;
        var hunted = new HashSet<string>();
        List<WorldEventNotice> notices = new();
        int ticks = checked((int)(WorldEventSchedule.TickAt(Clock, o.EndAt) - WorldEventSchedule.TickAt(Clock, o.StartAt)) + 40);
        for (int i = 0; i < ticks; i++)
        {
            notices.AddRange(w.Step().Events);
            foreach (string c in v.Creatures)
                if (e.View(c).CurrentTarget is string t && t.StartsWith("sanglier", StringComparison.Ordinal)) hunted.Add(t);
        }
        Assert.NotEmpty(hunted);   // la meute s'en prend aux sangliers
        Assert.All(e.CombatantIds, id => Assert.False(e.View(id).IsDead, id));   // une bête ne tue jamais une autre
        Assert.Contains(notices, n => n.Kind == WorldEventNoticeKind.Ended);
        Assert.All(v.Creatures, c => Assert.True(e.View(c).HasLeft));
        Assert.DoesNotContain(w.EventRegistry, x => x.Id.EndsWith(Hunt, StringComparison.Ordinal));
    }

    // --- Sauvegarde v13 ---------------------------------------------------------------------------------------------------------------

    private static JsonObject SaveJson(WorldSave save) => JsonNode.Parse(SaveJsonReader.Write(save))!.AsObject();

    private static WorldSave Saved(params SavedEvent[] events) =>
        new("zone.vallons", 1000, 1, new[] { PlayerCharacters.PlacesKey }, Hero().WithEvents(events));

    [Fact]
    public void Sauvegarde_v13_aller_retour()
    {
        WorldSave save = Saved(new SavedEvent("zone.vallons/faille_du_seuil", 2, 1, 9, EventOutcome.Lost),
            new SavedEvent("zone.vallons/attaque_hameau", 1, 0, 4, EventOutcome.Won, owed: true));
        string json = SaveJsonReader.Write(save);
        SaveLoadResult r = SaveJsonReader.Read(json);
        Assert.True(r.IsValid, r.Report.ToString());
        Assert.Equal(json, SaveJsonReader.Write(r.Save!));
        Assert.Equal(new[] { "zone.vallons/attaque_hameau", "zone.vallons/faille_du_seuil" }, r.Save!.Character!.Events.Select(e => e.Id));
        Assert.Contains(SaveVersion.Current, json);   // V2.6 : 14
        Assert.Contains("\"events\": []", SaveJsonReader.Write(Saved()));
    }

    [Fact]
    public void Sauvegarde_v12_lue_et_mise_a_jour()
    {
        JsonObject root = SaveJson(Saved());
        root["schemaVersion"] = 12;
        root["data"]!["character"]!.AsObject().Remove("events");
        root["data"]!["character"]!.AsObject().Remove("scars");   // V2.6
        SaveLoadResult r = SaveJsonReader.Read(root.ToJsonString());
        Assert.True(r.IsValid, r.Report.ToString());
        Assert.Empty(r.Save!.Character!.Events);
        Assert.Contains(SaveVersion.Current, SaveJsonReader.Write(r.Save));
        // En version 12, le registre est un champ inconnu.
        JsonObject v12 = SaveJson(Saved());
        v12["schemaVersion"] = 12;
        v12["data"]!["character"]!.AsObject().Remove("scars");   // V2.6
        Assert.Contains(SaveJsonReader.Read(v12.ToJsonString()).Report.Errors, e => e.Path == "$.data.character.events" && e.Code == ValidationCode.UnknownField);
    }

    [Theory]
    [InlineData(null, "$.data.character.events", ValidationCode.MissingField)]
    [InlineData("[{\"id\": \"vallons\", \"won\": 0, \"lost\": 0, \"last\": -1, \"outcome\": \"none\"}]", "$.data.character.events[0].id", ValidationCode.InvalidValue)]
    [InlineData("[{\"id\": \"zone.vallons/a\", \"won\": -1, \"lost\": 0, \"last\": -1, \"outcome\": \"none\"}]", "$.data.character.events[0].won", ValidationCode.OutOfRange)]
    [InlineData("[{\"id\": \"zone.vallons/a\", \"won\": 0, \"lost\": 0, \"last\": -2, \"outcome\": \"none\"}]", "$.data.character.events[0].last", ValidationCode.OutOfRange)]
    [InlineData("[{\"id\": \"zone.vallons/a\", \"won\": 0, \"lost\": 0, \"last\": 3, \"outcome\": \"perdu\"}]", "$.data.character.events[0].outcome", ValidationCode.InvalidValue)]
    [InlineData("[{\"id\": \"zone.vallons/a\", \"won\": 0, \"lost\": 1, \"last\": -1, \"outcome\": \"lost\"}]", "$.data.character.events[0].outcome", ValidationCode.InvalidValue)]
    [InlineData("[{\"id\": \"zone.vallons/a\", \"won\": 0, \"lost\": 0, \"last\": 3, \"outcome\": \"none\", \"owed\": true}]", "$.data.character.events[0].owed", ValidationCode.InvalidValue)]
    [InlineData("[{\"id\": \"zone.vallons/a\", \"won\": 1, \"lost\": 0, \"last\": 3, \"outcome\": \"won\", \"owed\": false}]", "$.data.character.events[0].owed", ValidationCode.InvalidValue)]
    [InlineData("[{\"id\": \"zone.vallons/b\", \"won\": 0, \"lost\": 0, \"last\": 1, \"outcome\": \"none\"}, {\"id\": \"zone.vallons/a\", \"won\": 0, \"lost\": 0, \"last\": 1, \"outcome\": \"none\"}]", "$.data.character.events[1].id", ValidationCode.InvalidValue)]
    [InlineData("[{\"id\": \"zone.vallons/a\", \"won\": 0, \"lost\": 0, \"last\": 1, \"outcome\": \"none\", \"jour\": 1}]", "$.data.character.events[0].jour", null)]
    public void Registre_refuse_au_chemin_precis(string? events, string path, ValidationCode? code)
    {
        JsonObject root = SaveJson(Saved());
        if (events is null) root["data"]!["character"]!.AsObject().Remove("events");
        else root["data"]!["character"]!["events"] = JsonNode.Parse(events);
        SaveLoadResult r = SaveJsonReader.Read(root.ToJsonString());
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == path && (code is null || e.Code == code));
    }

    // --- Répliques des PNJ -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Josse_remercie_une_fois_puis_se_souvient_du_pillage()
    {
        long noon = WorldEventSchedule.TickAt(Clock, 3 * 1440 + 10 * 60);
        ZoneDefinition zone = Zone(83.5, -35.5);
        SavedCharacter hero = Hero().WithStory(new SavedStory(new[] { "fact.event.attaque_hameau.defendu" }));
        WorldSession w = new(new WorldSave("zone.vallons", noon, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone, ShippedData, Progression, ItemsCatalog, Book(zone));
        w.Step();
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.doyen", out DialogueView? view));
        Assert.Equal(("v25_merci", "text.v25.doyen.merci"), (view!.Node, view.Text));
        Assert.Equal(DialogueRefusal.None, w.Choose(0, out _, out _));
        w.Step();
        w.Talk("npc.doyen", out view);
        Assert.NotEqual("v25_merci", view!.Node);
        // Pillé (et jamais défendu) : Josse le dit, une fois.
        SavedCharacter robbed = Hero().WithStory(new SavedStory(new[] { "fact.event.attaque_hameau.pille" }));
        WorldSession r = new(new WorldSave("zone.vallons", noon, 0, new[] { PlayerCharacters.PlacesKey }, robbed), zone, ShippedData, Progression, ItemsCatalog, Book(zone));
        r.Step();
        r.Talk("npc.doyen", out view);
        Assert.Equal("v25_pille", view!.Node);
    }

    // --- Affichage et essai (EdS.Runtime) ---------------------------------------------------------------------------------------

    [Fact]
    public void Textes_d_un_moment_variante_puis_commun()
    {
        Assert.Equal(new[] { "annonce", "debut", "vague", "victoire", "defaite", "absence", "recompense", "attente", "merci", "fin" },
            Enum.GetValues<WorldEventNoticeKind>().Where(k => !AdventureSession.IsScarNotice(k)).Select(AdventureSession.Moment));   // V2.6 : les cicatrices à part
    }

    [Fact]
    public void Faille_en_formes_simples_et_cloche_de_synthese()
    {
        IReadOnlyList<WildlifePart> open = EventLooks.Rift(1.3), closed = EventLooks.Rift(1.3, 0);
        Assert.Equal(2 + EventLooks.RiftShards + EventLooks.RiftMotes, open.Count);
        Assert.Equal(open.Select(p => p.Name), EventLooks.Rift(7.9).Select(p => p.Name));
        Assert.All(closed, p => Assert.Equal(0, p.ScaleX));
        Assert.All(open, p => Assert.True(p.Y >= 0 && p.Y < 4.5, p.Name));
        float[] bell = EventLooks.Bell(22050);
        Assert.Equal((int)(EventLooks.BellSeconds * 22050), bell.Length);
        Assert.InRange(bell.Max(Math.Abs), 0.84f, 0.86f);
        Assert.Equal(bell, EventLooks.Bell(22050));
        Place hamlet = Vallons.Places.Single(p => p.Id == "hameau");
        IReadOnlyList<(string Kind, double X, double Z)> ruins = EventLooks.Aftermath(hamlet);
        Assert.Equal((3, 6), (ruins.Count(r => r.Kind == "fumee"), ruins.Count(r => r.Kind == "debris")));
        Assert.All(ruins, r => Assert.True(hamlet.Contains(new GroundVector(Fixed.FromRaw((long)(r.X * 1e4)), Fixed.FromRaw((long)(r.Z * 1e4))))));
    }

    [Fact]
    public void Essai_des_evenements_avance_la_sauvegarde()
    {
        string dir = Path.Combine(Path.GetTempPath(), "eds_v25_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "vallons.json");
            SaveStore.Write(path, new WorldSave("zone.vallons", 0, 1, new[] { PlayerCharacters.PlacesKey }, Veteran()));   // l'attaque demande le niveau 6
            foreach (string id in new[] { Hunt, Rift, Raid })
            {
                long before = SaveStore.Read(path).Save!.WorldTick;
                EventOccurrence o = EventTrial.NextOf(Vallons, id, Clock, before)!;
                string message = EventTrial.MoveSave(path, DataDirectory, ScenariosDirectory, id, "essai-" + id);
                WorldSave moved = SaveStore.Read(path).Save!;
                Assert.Equal(EventTrial.TrialTick(Clock, o), moved.WorldTick);
                Assert.Equal(o.StartAt - EventTrial.LeadMinutes, WorldEventSchedule.Absolute(Clock.At(moved.WorldTick)));
                Assert.Contains("Partie avancée", message);
                Assert.True(Directory.GetFiles(dir).Length > 1);
            }
            // La partie avancée entend la cloche : l'attaque part dans les cinq minutes du monde.
            AdventureSession a = Adventure(path);
            Assert.NotNull(a.EventBanner);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // --- Relecture indépendante de la PR #107 ------------------------------------------------------------------------------------

    [Fact]
    public void Regle_de_place_comptee_avec_les_places_reservees()
    {
        // 128 places, 64 aux quêtes : 60 combattants, 1 forme de chef à venir, 2 créatures d'événement en attente : il en reste une.
        Assert.True(WorldEvents.Fits(60, 2, 1, 1, 128));
        Assert.False(WorldEvents.Fits(60, 2, 1, 2, 128));
        Assert.False(WorldEvents.Fits(61, 2, 1, 1, 128));
        Assert.False(WorldEvents.Fits(60, 3, 1, 1, 128));
    }

    [Fact]
    public void Creature_dont_la_place_est_prise_garde_sa_reservation_puis_ne_vient_plus_apres_la_fin()
    {
        // L'assaillant (un seul) doit arriver là où le personnage se tient (point sûr du hameau) : il attend, sa place réservée.
        ZoneDefinition zone = Weak(80, -38);
        EventOccurrence o = First(zone, Raid);
        WorldSession w = Session(zone, WorldEventSchedule.TickAt(Clock, o.StartAt) - 2);
        Run(w, 40);
        Assert.Equal(1, w.EventsRuntime!.PendingPlaces);
        Assert.Empty(w.WorldEvents.Single().Creatures);
        List<WorldEventNotice> notices = Run(w, checked((int)(WorldEventSchedule.TickAt(Clock, o.EndAt) - WorldEventSchedule.TickAt(Clock, o.StartAt))));
        // Après la fin, aucune arrivée : la créature ne vient plus ; l'attaque est manquée, une fois.
        Assert.Equal(0, w.EventsRuntime.PendingPlaces);
        Assert.DoesNotContain(w.Zone.Engine.CombatantIds, id => id.Contains("_ev", StringComparison.Ordinal));
        Assert.Single(notices, n => n.Kind == WorldEventNoticeKind.Lost);
        Assert.DoesNotContain(Run(w, 200), n => n.Event == Raid);
    }

    /// <summary>Remplit la session de bandits immobiles au loin jusqu'à ne laisser que <paramref name="left"/> places hors de la réserve.</summary>
    private static void Fill(WorldSession w, int left) => FillTo(w, w.Zone.Setup.Combat.MaxCombatants - ZonePopulations.QuestReserve - left);

    /// <summary>Remplit la session de bandits immobiles au loin jusqu'à <paramref name="total"/> combattants.</summary>
    private static void FillTo(WorldSession w, int total)
    {
        CombatEngine e = w.Zone.Engine;
        AssailantSetup model = (AssailantSetup)w.Zone.Setup.Combatants.Single(c => c.Id == "bandit_epee3");
        int n = 0;
        for (int x = 66; x <= 138 && e.CombatantIds.Count < total; x += 3)
            for (int z = -68; z <= -46 && e.CombatantIds.Count < total; z += 3)
            {
                var place = new CombatantPlacement(new GroundVector(Fixed.FromInt(x), Fixed.FromInt(z)), model.Placement!.Radius);
                try
                {
                    e.Join(new AssailantSetup("remplissage" + n++, model.Team, model.Armor, model.MagicResistance, model.Target, model.FirstAttackMs, model.IntervalMs,
                        model.Attacks, model.Initiative, model.StatusResistance, model.Triggers, model.Health, place, e.CurrentTick * 50, model.Behavior));
                }
                catch (ArgumentException) { }
                w.Step();
            }
        Assert.Equal(total, e.CombatantIds.Count);
    }

    [Fact]
    public void Chasse_sans_place_tentee_une_seule_fois()
    {
        EventOccurrence o = First(Vallons, Hunt);
        WorldSession w = Session(Vallons, WorldEventSchedule.TickAt(Clock, o.StartAt) - 400, safePoint: "place_du_village");
        Fill(w, 1);   // une place : la chasse (deux loups) ne tient pas
        int count = w.Zone.Engine.CombatantIds.Count;
        List<WorldEventNotice> notices = Run(w, checked((int)(WorldEventSchedule.TickAt(Clock, o.EndAt) - WorldEventSchedule.TickAt(Clock, o.StartAt))) + 600);
        Assert.DoesNotContain(notices, n => n.Event == Hunt);
        Assert.DoesNotContain(w.Zone.Engine.CombatantIds, id => id.Contains("_ev", StringComparison.Ordinal));
        Assert.True(w.Zone.Engine.CombatantIds.Count <= count + 1);   // rien de la chasse (au plus une vie de population)
    }

    [Fact]
    public void Registre_plein_rien_n_est_regle_ni_donne()
    {
        ZoneDefinition zone = Weak(86, -38);
        EventOccurrence o = First(zone, Raid);
        SavedEvent[] others = Enumerable.Range(0, SavedEvent.MaxEntries).Select(i => new SavedEvent($"zone.autre/e{i:00}", 0, 0, -1, EventOutcome.None)).ToArray();
        WorldSession w = Session(zone, WorldEventSchedule.TickAt(Clock, o.StartAt) - 5, Hero().WithEvents(others));
        long money = w.Money;
        List<WorldEventNotice> notices = Run(w, 10);
        notices.AddRange(Fight(w));
        notices.AddRange(Run(w, 30));
        Assert.DoesNotContain(notices, n => n.Kind is WorldEventNoticeKind.Won or WorldEventNoticeKind.Rewarded or WorldEventNoticeKind.Thanks);
        Assert.Equal(money, w.Money);
        Assert.Equal(others.Select(x => x.Id), w.EventRegistry.Select(x => x.Id));
        Assert.DoesNotContain("fact.event.attaque_hameau.defendu", w.Narrative!.ToSaved().Facts);
        // Au chargement : des zones reliées dont les événements à issue dépassent le registre sont refusées.
        Assert.True(ZoneValidator.CheckEventRegistry(new[] { Vallons }).IsValid);
        ValidationReport over = ZoneValidator.CheckEventRegistry(Enumerable.Repeat(Vallons, SavedEvent.MaxEntries / 2 + 1).ToList());
        Assert.Contains(over.Errors, e => e.Path == "$.data.events" && e.Code == ValidationCode.LimitExceeded);
    }

    [Fact]
    public void Rechargee_pendant_l_attaque_une_seule_recompense()
    {
        ZoneDefinition zone = Weak(86, -38);
        EventOccurrence o = First(zone, Raid);
        WorldSession first = Session(zone, WorldEventSchedule.TickAt(Clock, o.StartAt) - 5);
        Run(first, 30);
        Assert.Single(first.WorldEvents.Single().Creatures);
        WorldSave mid = SaveJsonReader.Read(SaveJsonReader.Write(first.Save())).Save!;
        Assert.Equal(EventOutcome.None, mid.Character!.Events.Single(e => e.Id == "zone.vallons/attaque_hameau").Outcome);
        // Reprise : l'attaque continue (assaillants revenus), défendue, récompensée une fois.
        WorldSession second = Session(zone, mid.WorldTick, mid.Character);
        List<WorldEventNotice> notices = Run(second, 5);
        Assert.Equal(WorldEventPhase.Running, second.WorldEvents.Single().Phase);
        notices.AddRange(Fight(second));
        notices.AddRange(Run(second, 3));
        Assert.Single(notices, n => n.Kind == WorldEventNoticeKind.Rewarded);
        WorldSave after = SaveJsonReader.Read(SaveJsonReader.Write(second.Save())).Save!;
        // Rechargée encore, même tick ou plus tard pendant la même occurrence : plus rien.
        foreach (long tick in new[] { after.WorldTick, WorldEventSchedule.TickAt(Clock, o.EndAt) - 10 })
        {
            WorldSession again = Session(zone, tick, after.Character);
            Assert.DoesNotContain(Run(again, 20), n => n.Event == Raid);
            Assert.Equal(after.Character!.Money, again.Money);
            Assert.Equal((1L, 0L), (again.EventRegistry.Single(e => e.Id == "zone.vallons/attaque_hameau").Won, again.EventRegistry.Single(e => e.Id == "zone.vallons/attaque_hameau").Lost));
        }
    }

    [Fact]
    public void Deux_personnages_chacun_son_registre()
    {
        // Deux sauvegardes (deux personnages) devant la même occurrence : chacun a sa première récompense, une seule fois.
        ZoneDefinition zone = Weak(86, -38);
        EventOccurrence o = First(zone, Raid);
        long start = WorldEventSchedule.TickAt(Clock, o.StartAt) - 5;
        WorldSession a = Session(zone, start), b = Session(zone, start, Hero(50));
        List<WorldEventNotice> na = Run(a, 10), nb = Run(b, 10);
        na.AddRange(Fight(a));
        na.AddRange(Run(a, 3));
        Assert.Single(na, n => n.Kind == WorldEventNoticeKind.Rewarded);
        // Le second ne vient pas : son hameau est pillé, celui du premier défendu.
        nb.AddRange(Run(b, checked((int)(WorldEventSchedule.TickAt(Clock, o.EndAt) - start))));
        Assert.Single(nb, n => n.Kind == WorldEventNoticeKind.Lost);
        Assert.Equal(EventOutcome.Won, a.EventRegistry.Single(e => e.Id == "zone.vallons/attaque_hameau").Outcome);
        Assert.Equal(EventOutcome.Lost, b.EventRegistry.Single(e => e.Id == "zone.vallons/attaque_hameau").Outcome);
        Assert.True(b.Aftermath(Raid));
        Assert.False(a.Aftermath(Raid));
        // Le second défend l'occurrence suivante : sa première récompense, à lui.
        EventOccurrence next = First(zone, Raid, o.EndAt);
        WorldSession b2 = Session(zone, WorldEventSchedule.TickAt(Clock, next.StartAt) - 5, b.Save().Character);
        long money = b2.Money;
        List<WorldEventNotice> n2 = Run(b2, 10);
        n2.AddRange(Fight(b2));
        n2.AddRange(Run(b2, 3));
        Assert.Single(n2, n => n.Kind == WorldEventNoticeKind.Rewarded);
        Assert.Equal(money + 300, b2.Money);
    }

    // --- Revue Codex de la PR #107, tour 2 ------------------------------------------------------------------------------------------

    [Fact]
    public void Les_populations_ne_prennent_pas_la_place_reservee_d_une_creature_d_evenement()
    {
        // L'assaillant doit arriver sur une place tenue par un combattant immobile : il attend, sa place réservée. La session est remplie
        // jusqu'à cette seule place ; à 22 h, les loups de la nuit ne la prennent pas (épuisés), et l'assaillant arrive dès qu'elle se libère.
        ZoneDefinition zone = Weak(64.5, -69);
        EventOccurrence o = First(zone, Raid);
        WorldSession w = Session(zone, WorldEventSchedule.TickAt(Clock, o.StartAt) - 20);
        CombatEngine e = w.Zone.Engine;
        // Un cerf (il ne bouge que si un ennemi l'approche) tient la place.
        AssailantSetup model = zone.PopulationOf("cerf1")!.Template;
        e.Join(new AssailantSetup("bouchon", model.Team, model.Armor, model.MagicResistance, model.Target, model.FirstAttackMs, model.IntervalMs, model.Attacks,
            model.Initiative, model.StatusResistance, model.Triggers, model.Health, new CombatantPlacement(new GroundVector(Fixed.Parse("64.5"), Fixed.FromInt(-69)),
            model.Placement!.Radius), e.CurrentTick * 50, model.Behavior));
        Run(w, 60);
        Assert.Equal(1, w.EventsRuntime!.PendingPlaces);
        int max = w.Zone.Setup.Combat.MaxCombatants;
        FillTo(w, max - w.ReservedPlaces);
        long at22 = WorldEventSchedule.TickAt(Clock, o.Day * 1440 + 22 * 60 + 2);
        List<PopulationEvent> population = new();
        while (w.Zone.StartWorldTick + e.CurrentTick < at22) population.AddRange(w.Step().Population);
        Assert.Contains(population, p => p.Kind == PopulationEventKind.Exhausted && p.Population.StartsWith("loup_nuit", StringComparison.Ordinal));
        Assert.DoesNotContain(population, p => p.Kind == PopulationEventKind.Respawned);
        Assert.Equal(max - w.ReservedPlaces, e.CombatantIds.Count);
        Assert.Equal(1, w.EventsRuntime.PendingPlaces);
        // La place se libère : l'assaillant arrive, sans dépasser le plafond.
        w.Zone.Leave("bouchon");
        Run(w, 60);
        Assert.Equal(0, w.EventsRuntime.PendingPlaces);
        Assert.Single(w.WorldEvents.Single().Creatures);
        Assert.True(e.CombatantIds.Count <= max);
    }

    [Theory]
    [InlineData(0, "reward.fact", "fact.met.doyen")]
    [InlineData(1, "reward.fact", "fact.met.renart")]
    [InlineData(0, "lostFact", "fact.met.herboriste_hameau")]
    [InlineData(1, "lostFact", "fact.met.x")]
    public void Faits_deja_rencontre_reserves_refuses(int index, string field, string fact)
    {
        // Revue Codex de la PR #107 (tour 3) : un fait « déjà rencontré » n'est appris qu'au premier dialogue avec le PNJ : une réussite ou
        // un échec d'événement ne le pose jamais (le premier échange deviendrait des retrouvailles). Même règle que pour le fait d'un chef.
        ZoneLoadResult r = Mutated(root =>
        {
            if (field == "lostFact") Event(root, index)["lostFact"] = fact;
            else Event(root, index)["reward"]!["fact"] = fact;
        });
        AssertError(r, $"$.data.events[{index}].{field}", ValidationCode.InvalidValue);
        Assert.Contains(r.Report.Errors, e => e.Message.Contains("réservé", StringComparison.Ordinal));
        // Le fait du chef suit la même règle.
        AssertError(Mutated(root => root["data"]!["boss"]!["fact"] = "fact.met.gaspard"), "$.data.boss.fact", ValidationCode.InvalidValue);
        // Un fait voisin, non réservé, reste accepté.
        Assert.True(Mutated(root => Event(root, index)["lostFact"] = "fact.metaux.fondus").IsValid);
    }

    // --- Choix de Damien du 7 novembre 2026 : pas d'attaque du hameau avant le niveau 6 -------------------------------------------

    [Fact]
    public void Sous_le_niveau_minimum_ni_annonce_ni_attaque_ni_pillage()
    {
        EventOccurrence o = First(Vallons, Raid);
        WorldSession w = Session(Vallons, WorldEventSchedule.TickAt(Clock, o.AnnounceAt) - 2, Hero(), safePoint: "place_du_village");
        Assert.True(w.Level < 6);
        List<WorldEventNotice> notices = Run(w, checked((int)(WorldEventSchedule.TickAt(Clock, o.EndAt) - WorldEventSchedule.TickAt(Clock, o.AnnounceAt)) + 40));
        Assert.DoesNotContain(notices, n => n.Event == Raid);
        Assert.DoesNotContain(w.WorldEvents, v => v.Event.Id == Raid);
        Assert.DoesNotContain(w.Zone.Engine.CombatantIds, id => id.Contains("_ev", StringComparison.Ordinal) && !id.StartsWith("loup_echo", StringComparison.Ordinal));
        Assert.NotEqual(WorldSession.RefugeActivity, w.NpcActivity(w.Narrative!.Book.Npc("npc.doyen")));
        SavedEvent entry = w.EventRegistry.Single(e => e.Id == "zone.vallons/attaque_hameau");
        Assert.Equal((0L, 0L, o.Day, EventOutcome.None), (entry.Won, entry.Lost, entry.Last, entry.Outcome));
        Assert.False(w.Aftermath(Raid));
        Assert.DoesNotContain("fact.event.attaque_hameau.pille", w.Narrative.ToSaved().Facts);
        // Pendant l'absence non plus : un personnage de niveau 1 qui connaissait le hameau ne le retrouve pas pillé.
        SavedCharacter known = Hero().WithEvents(new[] { new SavedEvent("zone.vallons/attaque_hameau", 0, 0, o.Day - 1, EventOutcome.None) });
        WorldSession back = Session(Vallons, WorldEventSchedule.TickAt(Clock, o.EndAt + 30), known);
        Assert.DoesNotContain(Run(back, 3), n => n.Event == Raid);
        Assert.Equal((0L, o.Day, EventOutcome.None), (back.EventRegistry.Single(e => e.Id == "zone.vallons/attaque_hameau").Lost,
            back.EventRegistry.Single(e => e.Id == "zone.vallons/attaque_hameau").Last, back.EventRegistry.Single(e => e.Id == "zone.vallons/attaque_hameau").Outcome));
        Assert.False(back.Aftermath(Raid));
        // Au niveau 6, l'occurrence suivante a lieu.
        EventOccurrence next = First(Vallons, Raid, o.EndAt);
        WorldSession vet = Session(Vallons, WorldEventSchedule.TickAt(Clock, next.StartAt) - 2, Veteran().WithEvents(back.EventRegistry));
        Assert.Contains(Run(vet, 4), n => n.Kind == WorldEventNoticeKind.Started && n.Event == Raid);
    }

    /// <summary>Occurrences de toutes les fenêtres jusqu'au jour <paramref name="days"/> (vérité par force brute).</summary>
    private static List<EventOccurrence> All(WorldEventDefinition e, long days) =>
        Enumerable.Range(0, (int)(days / e.EveryDays) + 2).Select(w => WorldEventSchedule.InWindow(Vallons.Id, Vallons.Id, e, w)).OfType<EventOccurrence>().ToList();

    [Fact]
    public void Calendrier_aux_bornes_de_minuit_et_des_fenetres()
    {
        foreach (WorldEventDefinition e in Vallons.Events)
        {
            List<EventOccurrence> all = All(e, 400);
            for (long from = 1440; from < 300 * 1440; from += 97)
            {
                EventOccurrence? next = WorldEventSchedule.Next(Vallons.Id, Vallons.Id, e, from);
                EventOccurrence? truth = all.Where(x => x.StartAt >= from).OrderBy(x => x.StartAt).FirstOrDefault();
                Assert.Equal(truth?.StartAt, next?.StartAt);
                Assert.Equal(all.Where(x => x.AnnounceAt <= from && from < x.EndAt).OrderBy(x => x.StartAt).Select(x => x.StartAt),
                    WorldEventSchedule.AllAround(Vallons.Id, Vallons.Id, e, from).Select(x => x.StartAt));
                Assert.Equal(all.Where(x => x.EndAt <= from).Select(x => (long?)x.EndAt).DefaultIfEmpty(null).Max(),
                    WorldEventSchedule.LastEnded(Vallons.Id, Vallons.Id, e, from)?.EndAt);
            }
        }
        // Une faille qui part après minuit, le dernier jour de sa fenêtre : à minuit (fenêtre suivante), elle est toujours la prochaine.
        WorldEventDefinition rift = Vallons.EventOf(Rift)!;
        EventOccurrence late = All(rift, 400).First(x => x.StartAt >= (x.Day + 1) * 1440 + EventTrial.LeadMinutes && (x.Day + 1) % rift.EveryDays == 0);
        long midnight = (late.Day + 1) * 1440;
        Assert.Equal(late.StartAt, WorldEventSchedule.Next(Vallons.Id, Vallons.Id, rift, midnight)!.StartAt);
        Assert.Equal(late.StartAt, WorldEventSchedule.Next(Vallons.Id, Vallons.Id, rift, midnight - 1)!.StartAt);
        Assert.True(WorldEventSchedule.Next(Vallons.Id, Vallons.Id, rift, late.StartAt + 1)!.StartAt > late.StartAt);
        Assert.Equal(late.StartAt, EventTrial.NextOf(Vallons, Rift, Clock, WorldEventSchedule.TickAt(Clock, midnight))!.StartAt);
        Assert.Equal(late.StartAt, WorldEventSchedule.Around(Vallons.Id, Vallons.Id, rift, late.StartAt + 10)!.StartAt);
        Assert.Equal(late.StartAt, WorldEventSchedule.LastEnded(Vallons.Id, Vallons.Id, rift, late.EndAt)!.StartAt);
        // En session : à minuit passé, la faille d'avant minuit est suivie et s'ouvre à son heure.
        WorldSession w = Session(Vallons, WorldEventSchedule.TickAt(Clock, late.StartAt) - 3, safePoint: "place_du_village");
        Assert.Contains(Run(w, 6), n => n.Kind == WorldEventNoticeKind.Started && n.Event == Rift && n.Day == late.Day);
    }
}

/// <summary>Sous-lot V2.5 : le scénario d'événement figé (trois assaillants qui arrivent l'un après l'autre sur le Bastion), au moteur 0.28.0.</summary>
[Trait("Category", "Regression")]
public class V25EvenementScenarioTests
{
    private static string Frozen => Path.Combine(AppContext.BaseDirectory, "Fixtures", "evenements-0.28.0-regles-0.16.0", "evenement_hameau.jsonl");

    [Fact]
    public void Scenario_d_evenement_fige_identique_et_rejoue()
    {
        ScenarioLoadResult result = ScenarioJsonReader.ReadFile(ScenarioPath("evenement_hameau"), ShippedData);
        Assert.True(result.IsValid, result.Report.ToString());
        var log = new MemoryCombatLog();
        CombatEngine.Run(result.Setup!, log);
        string[] frozen = File.ReadAllLines(Frozen);
        Assert.Equal(frozen, log.Lines);
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(frozen, ShippedData).Status);
        Assert.Equal(3, frozen.Count(l => l.Contains("\"event\":\"combatantJoined\"")));
    }
}
