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
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.6 (« le Seuil qui grandit ») : zones en schéma 11 (cicatrice d'une faille, échos rôdeurs ; schéma 10 encore lu), sauvegarde
/// v14 (cicatrices ; v13 encore lue et mise à jour), cicatrice laissée par une faille restée ouverte, paliers gagnés avec les jours du monde,
/// échos plus forts et rôdeurs (hors de la réserve des quêtes), recul quand une faille est refermée, purification avec un éclat de Seuil
/// (refus atomiques), couleurs fanées, répliques des PNJ, essai.
/// </summary>
[Trait("Category", "World")]
public class V26SeuilGranditTests
{
    private static readonly ZoneDefinition Vallons = Zone();
    private static readonly WorldClock Clock = new(50);
    private const string Rift = "faille_du_seuil", Ruins = "zone.vallons/faille_du_seuil/ruines", Lake = "zone.vallons/faille_du_seuil/lac";
    private const string Shard = "item.eclat_de_seuil";

    private static JsonObject ZoneJson() => JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();

    private static ZoneLoadResult Mutated(Action<JsonObject> mutate)
    {
        JsonObject root = ZoneJson();
        mutate(root);
        return ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog);
    }

    private static JsonObject RiftJson(JsonObject root) => root["data"]!["events"]!.AsArray().Single(e => (string?)e!["id"] == Rift)!.AsObject();

    private static int RiftIndex(JsonObject root) => root["data"]!["events"]!.AsArray().Select((e, i) => ((string?)e!["id"], i)).Single(x => x.Item1 == Rift).i;

    private static void AssertError(ZoneLoadResult r, string path, ValidationCode? code = null)
    {
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == path && (code is null || e.Code == code));
    }

    /// <summary>Partie au tick <paramref name="tick"/>, entrée à la place du village (déplacée par <see cref="V11TestData.Zone"/>).</summary>
    private static WorldSession Session(ZoneDefinition zone, long tick, SavedCharacter hero) =>
        new(new WorldSave("zone.vallons", tick, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone, ShippedData, Progression, ItemsCatalog, Book(zone),
            safePoint: "place_du_village");

    private static long Noon(long day) => WorldEventSchedule.TickAt(Clock, day * 1440 + 12 * 60);

    private static List<WorldEventNotice> Run(WorldSession w, int ticks)
    {
        var notices = new List<WorldEventNotice>();
        for (int i = 0; i < ticks; i++) notices.AddRange(w.Step().Events);
        return notices;
    }

    private static SavedCharacter WithScar(SavedCharacter hero, string key, int stage, long since) => hero.WithScars(hero.Scars.Where(x => x.Id != key).Append(new SavedScar(key, stage, since, since)));

    // --- Formats ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Versions_du_sous_lot()
    {
        Assert.Equal((12, 10, 9), (ZoneValidator.CurrentSchemaVersion, ZoneValidator.EventsSchemaVersion, ZoneValidator.WeatherSchemaVersion));
        Assert.Equal((15, 14, 13), (SaveValidator.CurrentSchemaVersion, SaveValidator.ScarsSince, SaveValidator.EventsSince));
        Assert.Equal((12, "0.16.0-provisoire"), (Vallons.SchemaVersion, Vallons.ContentVersion));   // V2.6.1 : propagation
        Assert.Equal("0.22.0-provisoire", JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!["contentVersion"]!.GetValue<string>());
        Assert.Equal("0.22.0-provisoire", JsonNode.Parse(File.ReadAllText(StoryJsonReader.TextsPathIn(DataDirectory)))!["contentVersion"]!.GetValue<string>());
        // Ni le moteur, ni les règles, ni le journal, ni les scénarios ne changent (ceux de V2.4.1).
        Assert.Equal(("0.30.0", 24, 17), (CombatEngine.Version, CombatEngine.LogFormatVersion, ScenarioValidator.CurrentSchemaVersion));
    }

    [Fact]
    public void Faille_livree_avec_sa_cicatrice()
    {
        WorldEventDefinition rift = Vallons.EventOf(Rift)!;
        WorldEventScar s = rift.Scar!;
        Assert.Equal((2, 4, 15, 2, 120, Shard, 200L), (s.GrowDays, s.MaxStage, s.StrengthPercent, s.HauntStage, s.HauntRespawnMinutes, s.PurifyItem, s.PurifyXp));
        Assert.Equal((Fixed.FromInt(8), Fixed.FromInt(20)), (s.RadiusAt(1), s.RadiusAt(4)));
        Assert.Equal((100L, 145L), (s.StrengthAt(1), s.StrengthAt(4)));
        Assert.Equal((0, 1, 2, 3, 3), (s.HauntsAt(1, 3), s.HauntsAt(2, 3), s.HauntsAt(3, 3), s.HauntsAt(4, 3), s.HauntsAt(8, 3)));
        Assert.All(rift.Groups, g => Assert.Equal(3, g.Haunts.Count));
        Assert.Equal(new[] { "fact.event.faille_du_seuil.cicatrice", "fact.event.faille_du_seuil.cicatrice_rode", "fact.event.faille_du_seuil.cicatrice_purifiee" },
            new[] { s.BornFact, s.DeepFact, s.PurifiedFact });
        Assert.All(Vallons.Events.Where(e => e.Kind != WorldEventKind.Rift), e => Assert.Null(e.Scar));
    }

    [Fact]
    public void Schema_10_encore_lu_sans_cicatrice()
    {
        ZoneLoadResult r = Mutated(root =>
        {
            root["schemaVersion"] = 10;
            RiftJson(root).Remove("scar");
            foreach (JsonNode? g in RiftJson(root)["groups"]!.AsArray())
            {
                g!.AsObject().Remove("haunts");
                g.AsObject().Remove("spreadTo");   // V2.6.1 (schéma 12)
            }
        });
        Assert.True(r.IsValid, r.Report.ToString());
        Assert.Null(r.Zone!.EventOf(Rift)!.Scar);
        int i = RiftIndex(ZoneJson());
        AssertError(Mutated(root => root["schemaVersion"] = 10), $"$.data.events[{i}].scar", ValidationCode.InvalidValue);
    }

    [Theory]
    [InlineData("growDays", "0", ".scar.growDays", ValidationCode.OutOfRange)]
    [InlineData("maxStage", "9", ".scar.maxStage", ValidationCode.OutOfRange)]
    [InlineData("radius", "0", ".scar.radius", ValidationCode.OutOfRange)]
    [InlineData("radiusPerStage", "30", ".scar.radiusPerStage", ValidationCode.OutOfRange)]
    [InlineData("strengthPercent", "101", ".scar.strengthPercent", ValidationCode.OutOfRange)]
    [InlineData("hauntStage", "5", ".scar.hauntStage", ValidationCode.OutOfRange)]
    [InlineData("hauntRespawnMinutes", "5", ".scar.hauntRespawnMinutes", ValidationCode.OutOfRange)]
    [InlineData("purifyItem", "\"item.nulle_part\"", ".scar.purifyItem", ValidationCode.UnknownReference)]
    [InlineData("purifyXp", "-1", ".scar.purifyXp", ValidationCode.OutOfRange)]
    [InlineData("bornFact", "\"cicatrice\"", ".scar.bornFact", null)]
    [InlineData("deepFact", "\"fact.met.npc.odile\"", ".scar.deepFact", null)]
    [InlineData("couleur", "1", ".scar.couleur", null)]
    public void Cicatrice_refusee_au_chemin_precis(string field, string json, string path, ValidationCode? code)
    {
        int i = RiftIndex(ZoneJson());
        AssertError(Mutated(root => RiftJson(root)["scar"]![field] = JsonNode.Parse(json)), $"$.data.events[{i}]{path}", code);
    }

    [Fact]
    public void Echos_rodeurs_refuses_au_chemin_precis()
    {
        int i = RiftIndex(ZoneJson());
        // Une cicatrice seulement sur une faille ; des rôdeurs seulement sur une faille à cicatrice.
        AssertError(Mutated(root =>
        {
            JsonObject raid = root["data"]!["events"]![0]!.AsObject();
            raid["scar"] = RiftJson(root)["scar"]!.DeepClone();
        }), "$.data.events[0].scar", ValidationCode.InvalidValue);
        AssertError(Mutated(root => RiftJson(root).Remove("scar")), $"$.data.events[{i}].groups[0].haunts", ValidationCode.InvalidValue);
        AssertError(Mutated(root => RiftJson(root)["groups"]![0]!["haunts"] = new JsonArray()), $"$.data.events[{i}].groups[0].haunts", ValidationCode.OutOfRange);
        AssertError(Mutated(root => RiftJson(root)["groups"]![0]!["haunts"]![1]!["position"] = RiftJson(root)["groups"]![0]!["creatures"]![0]!["position"]!.DeepClone()),
            $"$.data.events[{i}].groups[0].haunts[1].position", ValidationCode.DuplicateId);
        AssertError(Mutated(root => RiftJson(root)["groups"]![0]!["haunts"]![0]!["like"] = "nulle_part"),
            $"$.data.events[{i}].groups[0].haunts[0].like", ValidationCode.UnknownReference);
    }

    [Fact]
    public void Paliers_recalcules_des_jours_du_monde()
    {
        WorldEventScar rule = Vallons.EventOf(Rift)!.Scar!;
        var scar = new SavedScar(Ruins, 1, 10, 10);
        Assert.Equal(new[] { 1, 1, 1, 2, 2, 3, 4, 4, 4 }, new long[] { 0, 10, 11, 12, 13, 14, 16, 17, 400 }.Select(d => WorldScarRules.StageAt(scar, rule, d)));
        Assert.Equal(new long?[] { 12, 14, 16, null }, new long[] { 10, 13, 15, 16 }.Select(d => WorldScarRules.NextStageDay(scar, rule, d)));
        // Un palier gardé au-delà du maximum de la zone (zone changée) est ramené au maximum.
        Assert.Equal(4, WorldScarRules.StageAt(new SavedScar(Ruins, 8, 10, 10), rule, 10));
    }

    // --- Sauvegarde v14 -------------------------------------------------------------------------------------------------------------

    private static JsonObject SaveJson(WorldSave save) => JsonNode.Parse(SaveJsonReader.Write(save))!.AsObject();

    private static WorldSave Saved(params SavedScar[] scars) => new("zone.vallons", 1000, 1, new[] { PlayerCharacters.PlacesKey }, Hero().WithScars(scars));

    [Fact]
    public void Sauvegarde_v14_aller_retour()
    {
        WorldSave save = Saved(new SavedScar(Ruins, 3, 12, 7), new SavedScar(Lake, 1, 4, 4));
        string json = SaveJsonReader.Write(save);
        SaveLoadResult r = SaveJsonReader.Read(json);
        Assert.True(r.IsValid, r.Report.ToString());
        Assert.Equal(json, SaveJsonReader.Write(r.Save!));
        Assert.Equal(new[] { Lake, Ruins }, r.Save!.Character!.Scars.Select(s => s.Id));
        Assert.Contains(SaveVersion.Current, json);   // V2.7 : 15 (réputation)
        Assert.Contains("{\"id\": \"zone.vallons/faille_du_seuil/ruines\", \"stage\": 3, \"since\": 12, \"born\": 7}", json);
        Assert.Contains("\"scars\": []", SaveJsonReader.Write(Saved()));
    }

    [Fact]
    public void Sauvegarde_v13_lue_et_mise_a_jour()
    {
        JsonObject root = SaveJson(Saved());
        root["schemaVersion"] = 13;
        root["data"]!["character"]!.AsObject().Remove("scars");
        root["data"]!["character"]!["story"]!.AsObject().Remove("reputation");   // V2.7 (version 15)
        SaveLoadResult r = SaveJsonReader.Read(root.ToJsonString());
        Assert.True(r.IsValid, r.Report.ToString());
        Assert.Empty(r.Save!.Character!.Scars);
        Assert.Contains(SaveVersion.Current, SaveJsonReader.Write(r.Save));   // V2.7 : 15
        JsonObject v13 = SaveJson(Saved());
        v13["schemaVersion"] = 13;
        Assert.Contains(SaveJsonReader.Read(v13.ToJsonString()).Report.Errors, e => e.Path == "$.data.character.scars" && e.Code == ValidationCode.UnknownField);
    }

    [Theory]
    [InlineData(null, "$.data.character.scars", ValidationCode.MissingField)]
    [InlineData("[{\"id\": \"zone.vallons/faille\", \"stage\": 1, \"since\": 0, \"born\": 0}]", "$.data.character.scars[0].id", ValidationCode.InvalidValue)]
    [InlineData("[{\"id\": \"zone.vallons/f/a\", \"stage\": 0, \"since\": 0, \"born\": 0}]", "$.data.character.scars[0].stage", ValidationCode.OutOfRange)]
    [InlineData("[{\"id\": \"zone.vallons/f/a\", \"stage\": 9, \"since\": 0, \"born\": 0}]", "$.data.character.scars[0].stage", ValidationCode.OutOfRange)]
    [InlineData("[{\"id\": \"zone.vallons/f/a\", \"stage\": 1, \"since\": -1, \"born\": 0}]", "$.data.character.scars[0].since", ValidationCode.OutOfRange)]
    [InlineData("[{\"id\": \"zone.vallons/f/a\", \"stage\": 1, \"since\": 3, \"born\": 4}]", "$.data.character.scars[0].born", ValidationCode.OutOfRange)]
    [InlineData("[{\"id\": \"zone.vallons/f/a\", \"stage\": 1, \"since\": 3}]", "$.data.character.scars[0].born", ValidationCode.MissingField)]
    [InlineData("[{\"id\": \"zone.vallons/f/b\", \"stage\": 1, \"since\": 3, \"born\": 3}, {\"id\": \"zone.vallons/f/a\", \"stage\": 1, \"since\": 3, \"born\": 3}]", "$.data.character.scars[1].id", ValidationCode.InvalidValue)]
    [InlineData("[{\"id\": \"zone.vallons/f/a\", \"stage\": 1, \"since\": 3, \"born\": 3, \"couleur\": 1}]", "$.data.character.scars[0].couleur", null)]
    public void Cicatrices_refusees_au_chemin_precis(string? scars, string path, ValidationCode? code)
    {
        JsonObject root = SaveJson(Saved());
        if (scars is null) root["data"]!["character"]!.AsObject().Remove("scars");
        else root["data"]!["character"]!["scars"] = JsonNode.Parse(scars);
        SaveLoadResult r = SaveJsonReader.Read(root.ToJsonString());
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == path && (code is null || e.Code == code));
    }

    [Fact]
    public void Cicatrices_bornees()
    {
        var many = Enumerable.Range(0, SavedScar.MaxEntries + 1).Select(i => new SavedScar($"zone.vallons/f/g{i:00}", 1, 0, 0)).ToList();
        Assert.Throws<ArgumentException>(() => Hero().WithScars(many));
        Assert.Throws<ArgumentException>(() => Hero().WithScars(new[] { new SavedScar(Ruins, 1, 0, 0), new SavedScar(Ruins, 2, 0, 0) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SavedScar(Ruins, 1, 3, 4));
        // Toutes les cicatrices possibles des zones reliées tiennent dans la sauvegarde.
        Assert.True(ZoneValidator.CheckEventRegistry(new[] { Vallons }).IsValid);
    }

    // --- Naissance, croissance, échos ---------------------------------------------------------------------------------------------

    /// <summary>Un personnage qui connaît déjà la faille (une ligne au registre, rien de réglé), et la première faille finie depuis.</summary>
    private static (SavedCharacter Hero, EventOccurrence Last, long Tick) Away()
    {
        WorldEventDefinition rift = Vallons.EventOf(Rift)!;
        EventOccurrence o = WorldEventSchedule.Next(Vallons.Id, Vallons.Id, rift, 1440)!;
        SavedCharacter hero = Hero().WithEvents(new[] { new SavedEvent(WorldEventSchedule.RegistryKey(Vallons.Id, Rift), 0, 0, -1, EventOutcome.None) });
        long tick = WorldEventSchedule.TickAt(Clock, checked(o.EndAt + 60));
        return (hero, o, tick);
    }

    [Fact]
    public void Faille_restee_ouverte_pendant_l_absence_laisse_une_cicatrice()
    {
        (SavedCharacter hero, EventOccurrence o, long tick) = Away();
        WorldSession w = Session(Vallons, tick, hero);
        List<WorldEventNotice> notices = Run(w, 2);
        Assert.Contains(notices, n => n.Kind == WorldEventNoticeKind.Missed && n.Event == Rift);
        WorldEventNotice born = Assert.Single(notices, n => n.Kind == WorldEventNoticeKind.ScarBorn);
        Assert.Equal((o.Group.Id, 1, o.Day), (born.Group, born.Wave, born.Day));
        SavedScar scar = Assert.Single(w.SavedScars);
        Assert.Equal(($"zone.vallons/faille_du_seuil/{o.Group.Id}", 1, o.Day, o.Day), (scar.Id, scar.Stage, scar.Since, scar.Born));
        ScarView view = Assert.Single(w.Scars);
        Assert.Equal((o.Group.Position!.Value, Fixed.FromInt(8)), (view.Position, view.Radius));
        Assert.Contains("fact.event.faille_du_seuil.cicatrice", w.Narrative!.ToSaved().Facts);
        // Gardée dans la sauvegarde, et pas recréée à la session suivante (occurrence réglée une seule fois).
        WorldSave saved = w.Save();
        Assert.Single(saved.Character!.Scars);
        WorldSession again = new(saved, Vallons, ShippedData, Progression, ItemsCatalog, Book(Vallons), safePoint: "place_du_village");
        Assert.DoesNotContain(Run(again, 2), n => n.Kind == WorldEventNoticeKind.ScarBorn || n.Kind == WorldEventNoticeKind.ScarGrew);
        Assert.Equal(1, Assert.Single(again.SavedScars).Stage);
    }

    [Fact]
    public void Une_autre_faille_restee_ouverte_creuse_la_cicatrice()
    {
        (SavedCharacter hero, EventOccurrence o, long tick) = Away();
        // Une cicatrice au palier 2 était déjà là, depuis le jour de la faille.
        hero = WithScar(hero, $"zone.vallons/faille_du_seuil/{o.Group.Id}", 2, o.Day);
        WorldSession w = Session(Vallons, tick, hero);
        WorldEventNotice grew = Assert.Single(Run(w, 2), n => n.Kind == WorldEventNoticeKind.ScarGrew);
        Assert.Equal(3, grew.Wave);
        Assert.Equal((3, o.Day), (Assert.Single(w.SavedScars).Stage, w.SavedScars[0].Since));
    }

    [Fact]
    public void La_cicatrice_grandit_avec_les_jours_et_le_dit()
    {
        // Née au jour 3, palier 1 ; la session commence au jour 4 et passe au jour 5 (palier 2).
        long start = WorldEventSchedule.TickAt(Clock, 4 * 1440 + 23 * 60 + 58);
        WorldSession w = Session(Zone(-18, -12), start, WithScar(Hero(), Lake, 1, 3));
        Assert.DoesNotContain(Run(w, 2), n => n.Kind == WorldEventNoticeKind.ScarGrew);   // premier tick : noté, pas annoncé
        Assert.Equal(1, w.Scars.Single().Stage);
        long midnight = WorldEventSchedule.TickAt(Clock, 5 * 1440) - w.Zone.StartWorldTick;
        var notices = Run(w, checked((int)(midnight - w.Zone.Engine.CurrentTick + 2)));
        WorldEventNotice grew = Assert.Single(notices, n => n.Kind == WorldEventNoticeKind.ScarGrew);
        Assert.Equal(("lac", 2), (grew.Group, grew.Wave));
        Assert.Equal((2, Fixed.FromInt(12)), (w.Scars.Single().Stage, w.Scars.Single().Radius));
        Assert.Equal(1, Assert.Single(w.SavedScars).Stage);   // rien d'écrit : le palier se recalcule du temps du monde
        Assert.Contains("fact.event.faille_du_seuil.cicatrice_rode", w.Narrative!.ToSaved().Facts);   // palier des échos rôdeurs
    }

    [Fact]
    public void Echos_rodeurs_plus_forts_hors_de_la_reserve_des_quetes_et_de_retour_apres_leur_mort()
    {
        // Palier 3 aux ruines : deux échos rôdeurs, 130 % des PV de leur modèle.
        WorldSession w = Session(Zone(-18, -12), Noon(6), WithScar(Hero(), Ruins, 3, 6));
        List<WorldEventNotice> notices = Run(w, 40);
        Assert.Single(notices, n => n.Kind == WorldEventNoticeKind.ScarStirs && n.Group == "ruines" && n.Wave == 3);
        ScarView v = w.Scars.Single();
        Assert.Equal(2, v.Haunts.Count);
        long model = w.Definition!.PopulationOf("loup_echo1")!.Template.Health!.Value;
        Assert.All(v.Haunts, id =>
        {
            Assert.True(w.IsEventCreature(id));
            Assert.Equal(model * 130 / 100, w.Zone.Engine.View(id).MaxHealth);
        });
        // Places réservées hors de la réserve des quêtes (même règle que les créatures d'événement).
        Assert.True(w.Zone.Engine.CombatantIds.Count + ZonePopulations.QuestReserve <= w.Zone.Setup.Combat.MaxCombatants);
        // Même partie, mêmes échos (déterministe).
        WorldSession twin = Session(Zone(-18, -12), Noon(6), WithScar(Hero(), Ruins, 3, 6));
        Run(twin, 40);
        Assert.Equal(v.Haunts, twin.Scars.Single().Haunts);
    }

    [Fact]
    public void Une_faille_qui_s_ouvre_sur_une_cicatrice_crache_des_echos_plus_forts()
    {
        WorldEventDefinition rift = Vallons.EventOf(Rift)!;
        EventOccurrence o = WorldEventSchedule.Next(Vallons.Id, Vallons.Id, rift, 1440)!;
        string key = $"zone.vallons/faille_du_seuil/{o.Group.Id}";
        WorldSession w = Session(Vallons, WorldEventSchedule.TickAt(Clock, o.StartAt) - 2, WithScar(Hero(), key, 4, o.Day));
        List<WorldEventNotice> notices = Run(w, 6);
        Assert.Contains(notices, n => n.Kind == WorldEventNoticeKind.Started);
        WorldEventView ev = w.WorldEvents.Single(x => x.Event.Id == Rift);
        long model = w.Definition!.PopulationOf("loup_echo1")!.Template.Health!.Value;
        Assert.NotEmpty(ev.Creatures);
        Assert.All(ev.Creatures, id => Assert.Equal(model * 145 / 100, w.Zone.Engine.View(id).MaxHealth));
        // Pendant que la faille est ouverte sur elle, la cicatrice ne se purifie pas.
        Assert.True(w.Scars.Single().RiftOpen);
    }

    // --- Relecture indépendante de la PR #110 ----------------------------------------------------------------------------------------

    [Fact]
    public void Rechargee_au_milieu_d_un_palier_rien_ne_change()
    {
        // Née au jour 3, palier 1 ; au jour 4 (milieu du palier), une session, une sauvegarde, une autre session : même palier, rien d'annoncé.
        WorldSession w = Session(Zone(-18, -12), Noon(4), WithScar(Hero(), Lake, 1, 3));
        Run(w, 3);
        ScarView before = w.Scars.Single();
        Assert.Same(w.Scars, w.Scars);   // vues gardées entre deux lectures
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!;
        var again = new WorldSession(saved, Zone(-18, -12), ShippedData, Progression, ItemsCatalog, Book(Vallons), safePoint: "place_du_village");
        Assert.DoesNotContain(Run(again, 3), n => n.Kind == WorldEventNoticeKind.ScarGrew || n.Kind == WorldEventNoticeKind.ScarBorn);
        ScarView after = again.Scars.Single();
        Assert.Equal((1, 5L, Fixed.FromInt(8)), (before.Stage, before.NextStageDay!.Value, before.Radius));
        Assert.Equal((before.Stage, before.NextStageDay, before.Radius), (after.Stage, after.NextStageDay, after.Radius));
        Assert.Equal(("lac", 1, 3L, 3L), again.SavedScars.Select(s => (s.Id.Split('/')[2], s.Stage, s.Since, s.Born)).Single());
    }

    [Fact]
    public void Recul_avec_echos_debout_ceux_de_trop_s_en_vont_et_les_autres_reviennent_a_leur_force()
    {
        WorldSession w = Session(Zone(-18, -12), Noon(6), WithScar(Hero(), Ruins, 3, 6));
        Run(w, 40);
        IReadOnlyList<string> first = w.Scars.Single().Haunts;
        Assert.Equal(2, first.Count);
        Assert.All(first, id => Assert.True(w.Zone.IsTransient(id)));   // morts jamais inscrites au registre (retours sans fin)
        long model = w.Definition!.PopulationOf("loup_echo1")!.Template.Health!.Value;
        WorldEventDefinition rift = w.Definition.EventOf(Rift)!;
        var notices = new List<WorldEventNotice>();
        w.ScarRecede(rift, "ruines", 6, notices);
        Assert.Equal(2, Assert.Single(notices, n => n.Kind == WorldEventNoticeKind.ScarReceded).Wave);
        Run(w, 400);   // plus de deux minutes du monde
        ScarView v = w.Scars.Single();
        Assert.Equal(2, v.Stage);
        string haunt = Assert.Single(v.Haunts);
        Assert.Equal(model * 115 / 100, w.Zone.Engine.View(haunt).MaxHealth);
        Assert.All(first, id => Assert.True(w.Zone.Engine.View(id).HasLeft, id));   // partis au calme, pas vaincus
        // Effacée tout à fait (deux reculs) : son écho s'en va, et rien ne se lève plus.
        w.ScarRecede(rift, "ruines", 6, notices);
        w.ScarRecede(rift, "ruines", 6, notices);
        Assert.Single(notices, n => n.Kind == WorldEventNoticeKind.ScarHealed);
        Run(w, 400);
        Assert.Empty(w.Scars);
        Assert.True(w.Zone.Engine.View(haunt).HasLeft);
        Assert.Empty(w.SavedScars);
    }

    [Fact]
    public void Au_palier_maximal_une_autre_faille_ratee_ne_depasse_pas_la_borne()
    {
        (SavedCharacter hero, EventOccurrence o, long tick) = Away();
        WorldSession w = Session(Vallons, tick, WithScar(hero, $"zone.vallons/faille_du_seuil/{o.Group.Id}", 4, o.Day));
        WorldEventNotice grew = Assert.Single(Run(w, 2), n => n.Kind == WorldEventNoticeKind.ScarGrew);
        Assert.Equal(4, grew.Wave);
        Assert.Equal(4, Assert.Single(w.SavedScars).Stage);
    }

    [Fact]
    public void Une_faille_sans_cicatrice_crache_des_echos_a_leur_force()
    {
        WorldEventDefinition rift = Vallons.EventOf(Rift)!;
        EventOccurrence o = WorldEventSchedule.Next(Vallons.Id, Vallons.Id, rift, 1440)!;
        WorldSession w = Session(Vallons, WorldEventSchedule.TickAt(Clock, o.StartAt) - 2, Hero());
        Run(w, 6);
        WorldEventView ev = w.WorldEvents.Single(x => x.Event.Id == Rift);
        long model = w.Definition!.PopulationOf("loup_echo1")!.Template.Health!.Value;
        Assert.NotEmpty(ev.Creatures);
        Assert.All(ev.Creatures, id => Assert.Equal(model, w.Zone.Engine.View(id).MaxHealth));
        Assert.All(ev.Creatures, id => Assert.False(w.Zone.IsTransient(id)));   // les créatures d'une faille restent inscrites
        Assert.Empty(w.Scars);
    }

    // --- Revue Codex de la PR #110 (tour 2) ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("radius", "922337203685477.5807", ".scar.radius")]
    [InlineData("radius", "-922337203685477.5808", ".scar.radius")]
    [InlineData("radiusPerStage", "922337203685477.5807", ".scar.radiusPerStage")]
    [InlineData("radiusPerStage", "-922337203685477.5808", ".scar.radiusPerStage")]
    public void Rayons_extremes_refuses_sans_debordement(string field, string json, string path)
    {
        int i = RiftIndex(ZoneJson());
        ZoneLoadResult r = Mutated(root => RiftJson(root)["scar"]![field] = JsonNode.Parse(json));
        AssertError(r, $"$.data.events[{i}]{path}", ValidationCode.OutOfRange);
        // Les deux à la fois : chacun son erreur, aucun calcul du rayon maximal.
        ZoneLoadResult both = Mutated(root =>
        {
            RiftJson(root)["scar"]!["radius"] = JsonNode.Parse("922337203685477.5807");
            RiftJson(root)["scar"]!["radiusPerStage"] = JsonNode.Parse("922337203685477.5807");
        });
        AssertError(both, $"$.data.events[{i}].scar.radius", ValidationCode.OutOfRange);
        AssertError(both, $"$.data.events[{i}].scar.radiusPerStage", ValidationCode.OutOfRange);
    }

    [Theory]
    [InlineData("haunts", "922337203685477.5807")]
    [InlineData("haunts", "-922337203685477.5808")]
    [InlineData("creatures", "922337203685477.5807")]
    [InlineData("position", "-922337203685477.5808")]
    public void Places_extremes_refusees_sans_debordement(string what, string extreme)
    {
        int i = RiftIndex(ZoneJson());
        JsonArray Far() => new JsonArray(JsonNode.Parse(extreme), JsonNode.Parse(extreme));
        ZoneLoadResult r = Mutated(root =>
        {
            JsonObject g = RiftJson(root)["groups"]![0]!.AsObject();
            if (what == "position") g["position"] = Far();
            else g[what]![0]!["position"] = Far();
        });
        string path = what == "position" ? $"$.data.events[{i}].groups[0].position" : $"$.data.events[{i}].groups[0].{what}[0].position";
        AssertError(r, path, ValidationCode.InvalidValue);
    }

    [Fact]
    public void Un_rodeur_qui_fuit_revient_apres_le_delai_des_rodeurs()
    {
        WorldSession w = Session(Zone(-18, -12), Noon(6), WithScar(Hero(), Ruins, 2, 6));
        Run(w, 40);
        string fled = Assert.Single(w.Scars.Single().Haunts);
        w.Zone.Leave(fled);   // comme une laisse dépassée : il quitte la zone
        long left = WorldEventSchedule.Absolute(w.Zone.Time);
        Run(w, 600);   // quatre minutes du monde : pas de retour une seconde plus tard
        Assert.True(w.Zone.Engine.View(fled).HasLeft);
        Assert.Empty(w.Scars.Single().Haunts);
        string? back = null;
        for (int t = 0; t < 30_000 && back is null; t++)
        {
            w.Step();
            if (t % 100 == 0 && w.Scars.Single().Haunts.Count > 0) back = w.Scars.Single().Haunts[0];
        }
        Assert.NotNull(back);
        Assert.NotEqual(fled, back);
        Assert.InRange(WorldEventSchedule.Absolute(w.Zone.Time) - left, 120, 125);   // hauntRespawnMinutes
    }

    // --- Revue Codex de la PR #110 (tour 3) : clés composées dans la borne de la sauvegarde ----------------------------------------

    private static readonly string LongZone = "zone." + new string('a', 59);   // 64 caractères, la borne d'un identifiant de zone

    private static ZoneLoadResult Keys(string eventId, string group)
    {
        Assert.Equal(64, LongZone.Length);
        return Mutated(root =>
        {
            root["data"]!["id"] = LongZone;
            JsonObject rift = RiftJson(root);
            string old = (string)rift["groups"]![0]!["id"]!;
            rift["groups"]![0]!["id"] = group;
            rift["id"] = eventId;
            // V2.6.1 : les voisines de propagation suivent le nouveau nom de la variante.
            foreach (JsonNode? other in rift["groups"]!.AsArray())
                if (other!["spreadTo"] is JsonArray to)
                    for (int k = 0; k < to.Count; k++)
                        if ((string?)to[k] == old) to[k] = group;
        });
    }

    [Fact]
    public void Cles_composees_aux_bornes_de_la_sauvegarde()
    {
        int i = RiftIndex(ZoneJson());
        // Registre : 64 + 1 + 63 = 128 caractères, accepté ; mais la cicatrice des ruines ferait 135 caractères : refusée à sa variante.
        ZoneLoadResult r = Keys(new string('f', 63), "ruines");
        AssertError(r, $"$.data.events[{i}].groups[0].id", ValidationCode.LimitExceeded);
        Assert.DoesNotContain(r.Report.Errors, e => e.Path == $"$.data.events[{i}].id");
        // Registre : 64 + 1 + 64 = 129 caractères : refusé à l'événement (même formule que le cœur).
        AssertError(Keys(new string('f', 64), "g"), $"$.data.events[{i}].id", ValidationCode.LimitExceeded);
        Assert.Equal(129, WorldEventSchedule.RegistryKey(LongZone, new string('f', 64)).Length);
        // Cicatrice : 64 + 1 + 20 + 1 + 42 = 128 caractères exactement : acceptée, et la partie l'écrit et la relit.
        string ev = new string('f', 20), g = new string('g', 42);
        ZoneLoadResult ok = Keys(ev, g);
        Assert.DoesNotContain(ok.Report.Errors, e => e.Path.StartsWith($"$.data.events[{i}]", StringComparison.Ordinal));
        string key = WorldScarRules.Key(LongZone, ev, g);
        Assert.Equal((128, ZoneValidator.ScarKey(LongZone, ev, g)), (key.Length, key));
        WorldSave save = Saved(new SavedScar(key, 1, 0, 0));
        Assert.True(SaveJsonReader.Read(SaveJsonReader.Write(save)).IsValid);
        Assert.Throws<ArgumentException>(() => new SavedScar(key + "x", 1, 0, 0));
        Assert.Throws<ArgumentException>(() => new SavedEvent(new string('a', 129), 0, 0, -1, EventOutcome.None));   // même borne que l'écrivain
    }

    // --- Purification --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Purifier_une_cicatrice_refus_atomiques_puis_un_eclat()
    {
        ZoneDefinition near = Zone(22.5, -7);   // à 1,5 m du cœur de la cicatrice des ruines
        // Loin du cœur : refusé.
        WorldSession far = Session(Zone(-18, -12), Noon(6), WithScar(Hero(0, new ItemStack(Shard, 1)), Ruins, 1, 6));
        Run(far, 2);
        Assert.Equal(ScarRefusal.TooFar, far.PurifyScar(Ruins));
        Assert.Equal(ScarRefusal.NoScar, far.PurifyScar(Lake));
        // Près du cœur, sans éclat : refusé, rien ne change.
        WorldSession poor = Session(near, Noon(6), WithScar(Hero(), Ruins, 1, 6));
        Run(poor, 2);
        string before = Snapshot(poor) + poor.Character.Xp + string.Join(",", poor.SavedScars.Select(s => s.Id + s.Stage));
        Assert.Equal(ScarRefusal.NoItem, poor.PurifyScar(Ruins));
        Assert.Equal(before, Snapshot(poor) + poor.Character.Xp + string.Join(",", poor.SavedScars.Select(s => s.Id + s.Stage)));
        // Palier 2 : un écho rôde ; tant qu'il est debout, refusé (l'éclat reste).
        WorldSession haunted = Session(near, Noon(6), WithScar(Hero(0, new ItemStack(Shard, 1)), Ruins, 2, 6));
        Run(haunted, 30);
        Assert.Single(haunted.Scars.Single().Haunts);
        before = Snapshot(haunted) + string.Join(",", haunted.SavedScars.Select(s => s.Id + s.Stage));
        Assert.Equal(ScarRefusal.HauntsRemain, haunted.PurifyScar(Ruins));
        Assert.Equal(before, Snapshot(haunted) + string.Join(",", haunted.SavedScars.Select(s => s.Id + s.Stage)));
        // Palier 1, un éclat : purifiée ; l'éclat est pris, l'expérience donnée, le fait appris, annoncé au tick suivant.
        WorldSession w = Session(near, Noon(6), WithScar(WithScar(Hero(0, new ItemStack(Shard, 1)), Ruins, 1, 6), Lake, 2, 6));
        Run(w, 2);
        long xp = w.Character.Xp;
        Assert.Equal(ScarRefusal.None, w.PurifyScar(Ruins));
        // Relecture de la PR #110 : le fait est appris tout de suite (une sauvegarde avant le tick suivant le garde).
        Assert.Contains("fact.event.faille_du_seuil.cicatrice_purifiee", w.Save().Character!.Story.Facts);
        Assert.Equal(0, w.Inventory.Count(Shard));
        Assert.Equal(xp + 200, w.Character.Xp);
        Assert.Equal(new[] { Lake }, w.SavedScars.Select(s => s.Id));
        WorldEventNotice purified = Assert.Single(Run(w, 1), n => n.Kind == WorldEventNoticeKind.ScarPurified);
        Assert.Equal(("ruines", 1, 200L), (purified.Group, purified.Wave, purified.Xp));
        Assert.Contains("fact.event.faille_du_seuil.cicatrice_purifiee", w.Narrative!.ToSaved().Facts);
        Assert.Equal(ScarRefusal.NoScar, w.PurifyScar(Ruins));
        Assert.DoesNotContain(w.Save().Character!.Scars, s => s.Id == Ruins);
    }

    // --- Recul quand une faille est refermée ---------------------------------------------------------------------------------------

    /// <summary>Vallons où la faille n'a qu'une vague d'un loup faible, à côté du camp de la carrière (sans échos rôdeurs).</summary>
    private static ZoneDefinition WeakRift()
    {
        ZoneLoadResult r = Mutated(root =>
        {
            JsonObject rift = RiftJson(root);
            rift["waves"] = 1;
            foreach (JsonNode? g in rift["groups"]!.AsArray())
            {
                g!["creatures"] = new JsonArray(new JsonObject { ["like"] = "loup1", ["position"] = new JsonArray(29, -12) });
                g["position"] = new JsonArray(26, -13);
                g.AsObject().Remove("haunts");
            }
        });
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Zone!;
    }

    [Fact]
    public void Refermer_une_faille_fait_reculer_sa_cicatrice_et_arrete_les_autres()
    {
        ZoneDefinition zone = WeakRift();
        WorldEventDefinition rift = zone.EventOf(Rift)!;
        EventOccurrence o = WorldEventSchedule.Next(zone.Id, zone.Id, rift, 1440)!;
        string here = $"zone.vallons/faille_du_seuil/{o.Group.Id}";
        string other = rift.Groups.Select(g => $"zone.vallons/faille_du_seuil/{g.Id}").First(k => k != here);
        // Cicatrice au palier 2 à la place de la faille ; une autre, née il y a longtemps, au palier 1 (déjà au palier 4 aujourd'hui).
        SavedCharacter hero = WithScar(WithScar(Inventory.Outfit(Hero(), ItemsCatalog).WithXp(300), here, 2, o.Day), other, 1, 0);
        var w = new WorldSession(new WorldSave("zone.vallons", WorldEventSchedule.TickAt(Clock, o.StartAt) - 2, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone,
            ShippedData, Progression, ItemsCatalog, Book(zone), safePoint: "camp_de_la_carriere");
        List<WorldEventNotice> notices = Run(w, 4);
        CombatEngine e = w.Zone.Engine;
        for (int i = 0; i < 2400 && w.CloseRift(Rift) != RiftRefusal.None; i++)
        {
            string? target = w.WorldEvents.SelectMany(v => v.Creatures).FirstOrDefault(t => e.View(t).IsPresent && !e.View(t).IsDead);
            long now = e.CurrentTick * 50;
            GroundVector me = e.View("bastion").Position!.Value;
            GroundVector to = target != null ? e.View(target).Position!.Value : new GroundVector(Fixed.FromInt(26), Fixed.FromInt(-13));
            double dx = (to.X.Raw - me.X.Raw) / 1e4, dz = (to.Z.Raw - me.Z.Raw) / 1e4, d = Math.Sqrt(dx * dx + dz * dz);
            try
            {
                if (target != null) e.Submit(new CombatCommand(now, "bastion", w.Character.ActiveSkills[i % w.Character.ActiveSkills.Count], target));
                e.Submit(d > 2 ? new CombatCommand(now, "bastion", new GroundVector(Fixed.FromRaw((long)(dx / d * 1e4)), Fixed.FromRaw((long)(dz / d * 1e4))))
                    : new CombatCommand(now, "bastion", CombatAction.Stop));
            }
            catch (ArgumentException) { }
            notices.AddRange(w.Step().Events);
        }
        notices.AddRange(Run(w, 2));
        Assert.Contains(notices, n => n.Kind == WorldEventNoticeKind.Won && n.Event == Rift);
        WorldEventNotice receded = Assert.Single(notices, n => n.Kind == WorldEventNoticeKind.ScarReceded);
        Assert.Equal((o.Group.Id, 1), (receded.Group, receded.Wave));
        long today = w.Zone.Time.Day;
        Assert.Equal((1, today), (w.SavedScars.Single(s => s.Id == here).Stage, w.SavedScars.Single(s => s.Id == here).Since));
        SavedScar paused = w.SavedScars.Single(s => s.Id == other);
        Assert.Equal((WorldScarRules.StageAt(new SavedScar(other, 1, 0, 0), rift.Scar!, today), today, 0L), (paused.Stage, paused.Since, paused.Born));
    }

    // --- Affichage, PNJ, essai -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Couleurs_fanees_selon_le_palier_et_la_distance()
    {
        WorldSession w = Session(Zone(-18, -12), Noon(10), WithScar(WithScar(Hero(), Ruins, 4, 10), Lake, 2, 10));
        IReadOnlyList<ScarView> scars = w.Scars;
        GroundVector Point(double x, double z) => new(Fixed.FromRaw((long)(x * 1e4)), Fixed.FromRaw((long)(z * 1e4)));
        Assert.Equal(1000, AdventureSession.FadeAt(scars, Point(21, -7)));    // au cœur, palier 4 sur 4
        Assert.Equal(500, AdventureSession.FadeAt(scars, Point(14, -42)));    // au cœur, palier 2 sur 4
        Assert.Equal(750, AdventureSession.FadeAt(scars, Point(31, -7)));     // à 10 m d'un rayon de 20 m : 1 - 1/4
        Assert.Equal(0, AdventureSession.FadeAt(scars, Point(41, -7)));       // au bord
        Assert.Equal(0, AdventureSession.FadeAt(Array.Empty<ScarView>(), Point(21, -7)));
        Assert.Equal(new[] { "cicatrice", "cicatrice_grandit", "cicatrice_recule", "cicatrice_guerie", "cicatrice_purifiee", "cicatrice_rode", "cicatrice_propagee" },
            Enum.GetValues<WorldEventNoticeKind>().Where(AdventureSession.IsScarNotice).Select(AdventureSession.Moment));
        foreach (string m in new[] { "cicatrice", "cicatrice_grandit", "cicatrice_recule", "cicatrice_guerie", "cicatrice_purifiee", "cicatrice_rode", "cicatrice_propagee" })
            Assert.True(Texts.Has("text.event.faille_du_seuil." + m), m);
        Assert.All(Vallons.EventOf(Rift)!.Groups, g => Assert.True(Texts.Has("text.event.faille_du_seuil.lieu." + g.Id), g.Id));
        Assert.All(new[] { "purify", "too_far", "haunts", "rift_open", "no_item", "purified", "label", "stage.1", "stage.4" }, k => Assert.True(Texts.Has("text.ui.scar." + k), k));
    }

    /// <summary>Partie à côté du PNJ (dehors, ou dans la chapelle pour Sœur Albe), à 10 h (Firmin est à son fournil, Sauge à sa boutique).</summary>
    private static WorldSession Near(string zone, double x, double z, SavedCharacter hero)
    {
        bool outside = zone == "zone.vallons";
        ZoneDefinition definition;
        if (outside) definition = Zone(x, z);
        else
        {
            JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, zone)))!.AsObject();
            root["data"]!["safePoints"]![0]!["position"] = new JsonArray(x, z);
            ZoneLoadResult loaded = ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog);
            Assert.True(loaded.IsValid, loaded.Report.ToString());
            definition = loaded.Zone!;
        }
        var w = new WorldSession(new WorldSave(zone, WorldEventSchedule.TickAt(Clock, 3 * 1440 + 10 * 60), 0, new[] { PlayerCharacters.PlacesKey }, hero), definition, ShippedData, Progression, ItemsCatalog,
            Book(Vallons), storyZone: outside ? null : "zone.vallons");
        w.Step();
        return w;
    }

    [Theory]
    [InlineData("npc.herboriste", "zone.vallons", -65.2, -6.4, "fact.event.faille_du_seuil.cicatrice", "v26_cicatrice")]
    [InlineData("npc.albe", "zone.chapelle", 3.2, 3.2, "fact.event.faille_du_seuil.cicatrice_rode", "v26_rode")]
    [InlineData("npc.boulanger", "zone.vallons", -42, 2, "fact.event.faille_du_seuil.cicatrice_purifiee", "v26_purifiee")]
    public void Les_PNJ_en_parlent_une_fois(string npc, string zone, double x, double z, string fact, string node)
    {
        // Le pain du matin déjà livré : la demande de Firmin (une offre de quête) passe avant ses autres mots.
        var done = new Dictionary<string, QuestProgress> { ["quest.pain_du_matin"] = new QuestProgress(QuestProgress.Done) };
        WorldSession w = Near(zone, x, z, Hero().WithStory(new SavedStory(new[] { fact }, done)));
        Assert.Equal(DialogueRefusal.None, w.Talk(npc, out DialogueView? view));
        Assert.Equal(node, view!.Node);
        Assert.Equal(DialogueRefusal.None, w.Choose(0, out _, out _));
        w.Step();
        w.Talk(npc, out view);
        Assert.NotEqual(node, view!.Node);
        // Sans le fait, jamais.
        WorldSession fresh = Near(zone, x, z, Hero().WithStory(new SavedStory(null, done)));
        Assert.Equal(DialogueRefusal.None, fresh.Talk(npc, out view));
        Assert.NotEqual(node, view!.Node);
    }

    [Fact]
    public void Essai_du_Seuil_qui_grandit()
    {
        string dir = Path.Combine(Path.GetTempPath(), "eds_v26_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "vallons.json");
            long tick = Noon(5);
            SaveStore.Write(path, new WorldSave("zone.vallons", tick, 1, new[] { PlayerCharacters.PlacesKey }, Hero()));
            Assert.Contains("Cicatrice créée", ScarTrial.Create(path, DataDirectory, ScenariosDirectory, "ruines", 3, "a"));
            Assert.Equal((Ruins, 3, 5L, 5L), SaveStore.Read(path).Save!.Character!.Scars.Select(s => (s.Id, s.Stage, s.Since, s.Born)).Single());
            Assert.Contains("rien n'a changé", ScarTrial.Create(path, DataDirectory, ScenariosDirectory, "nulle_part", 1, "b"));
            Assert.Contains("Partie avancée de 2 jours", ScarTrial.AdvanceDays(path, 2, 50, "c"));
            Assert.Equal(tick + 2 * Clock.TicksPerDay, SaveStore.Read(path).Save!.WorldTick);
            AdventureSession a = Adventure(path);
            Assert.Equal(4, a.Scars.Single().Stage);
            Assert.Contains("Cicatrices effacées", ScarTrial.Clear(path, "d"));
            Assert.Empty(SaveStore.Read(path).Save!.Character!.Scars);
            Assert.Equal(3, Directory.GetFiles(dir, "vallons.json.essai-*.bak").Length);   // trois copies d'avant l'essai (aucune quand rien ne change)
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
