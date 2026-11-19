using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Items;
using EdS.Data.Merchants;
using EdS.Data.Json;
using EdS.Data.Rebuilds;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Lieux reconstruits, sous-lot V2.43.1 (cœur ; décisions de Damien du 19 novembre 2026) : catalogue des chantiers (<c>kind: "rebuilds"</c>,
/// version 1) et son validateur à chemin précis ; règles pures (apport plafonné, paliers finis, découvertes déjà faites, jamais de recul) ;
/// sauvegarde v28 (<c>character.rebuilds</c>, v27 encore lue) ; dans la partie : or et matériaux apportés sur place, combats autour du lieu
/// et découvertes comptés d'eux-mêmes, faits de l'histoire à chaque étape, refus atomiques.
/// </summary>
[Trait("Category", "World")]
public class LieuxReconstruitsTests
{
    private const string Iron = "item.minerai_de_fer", Traces = "discovery.traces_griffues";

    // Le hameau de la carrière : [64, 100] × [-42, -16] ; un bandit à l'épée s'y tient (90, -22) : on apporte loin de lui, on le combat de près.
    private const double InX = 68, InZ = -18, FightX = 88, FightZ = -22;

    private static string Catalog(Action<JsonObject>? mutate = null)
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["kind"] = "rebuilds",
            ["contentVersion"] = "0.1.0-essai",
            ["data"] = new JsonObject
            {
                ["sites"] = new JsonArray(
                    new JsonObject
                    {
                        ["id"] = "hameau",
                        ["place"] = "hameau",
                        ["margin"] = 10,
                        ["stages"] = new JsonArray(
                            new JsonObject
                            {
                                ["id"] = "deblaiement",
                                ["needs"] = new JsonArray(
                                    new JsonObject { ["id"] = "or", ["coins"] = 5_000 },
                                    new JsonObject { ["id"] = "minerai", ["item"] = Iron, ["count"] = 10 },
                                    new JsonObject { ["id"] = "bandits", ["defeat"] = 1 }),
                            },
                            new JsonObject
                            {
                                ["id"] = "toits",
                                ["needs"] = new JsonArray(
                                    new JsonObject { ["id"] = "releve", ["discover"] = Traces },
                                    new JsonObject { ["id"] = "or", ["coins"] = 100 }),
                            }),
                    },
                    new JsonObject
                    {
                        ["id"] = "moulin",
                        ["place"] = "moulin_a_foulon",
                        ["stages"] = new JsonArray(new JsonObject
                        {
                            ["id"] = "roue",
                            ["needs"] = new JsonArray(new JsonObject { ["id"] = "or", ["coins"] = 1 }),
                        }),
                    }),
            },
        };
        mutate?.Invoke(root);
        return root.ToJsonString();
    }

    private static readonly Lazy<ZoneDefinition> Vallons = new(() => Zone(InX, InZ));
    private static readonly Lazy<StoryBook> Story = new(() => Book(Vallons.Value));

    private static RebuildLoadResult Read(string json) => RebuildJsonReader.Read(json, Vallons.Value, ItemsCatalog, Story.Value);

    private static readonly Lazy<RebuildBook> Sample = new(() => { RebuildLoadResult r = Read(Catalog()); return r.Book ?? throw new InvalidOperationException(r.Report.ToString()); });

    private static RebuildSite Hameau => Sample.Value.Of("hameau")!;

    private static void Refused(RebuildLoadResult r, string path, ValidationCode code)
    {
        Assert.False(r.IsValid);
        Assert.True(r.Report.Errors.Any(e => e.Path == path && e.Code == code), $"{path} ({code}) attendu ; {r.Report}");
    }

    // --- Catalogue -------------------------------------------------------------------------------------

    [Fact]
    public void Catalogue_valide_et_son_schema()
    {
        RebuildBook b = Sample.Value;
        Assert.Equal((1, "zone.vallons"), (RebuildValidator.CurrentSchemaVersion, b.ZoneId));
        Assert.Equal(new[] { "hameau", "moulin" }, b.Sites.Select(s => s.Id));
        Assert.Equal("Hameau de la carrière", Hameau.Place.Name);
        Assert.Equal(new[] { RebuildNeedKind.Coins, RebuildNeedKind.Item, RebuildNeedKind.Defeat }, Hameau.Stages[0].Needs.Select(n => n.Kind));
        Assert.Equal((Traces, 1L), (Hameau.Stages[1].Needs[0].Target, Hameau.Stages[1].Needs[0].Amount));
        Assert.Equal(("fact.rebuild.hameau.started", "fact.rebuild.hameau.toits", "fact.rebuild.hameau.done"),
            (Hameau.StartedFact, Hameau.StageFact(Hameau.Stages[1]), Hameau.DoneFact));
        // Marge : 10 m autour du rectangle du lieu.
        Assert.True(Hameau.Around(new EdS.Domain.Space.GroundVector(EdS.Domain.Numerics.Fixed.FromInt(110), EdS.Domain.Numerics.Fixed.FromInt(-16))));
        Assert.False(Hameau.Around(new EdS.Domain.Space.GroundVector(EdS.Domain.Numerics.Fixed.FromInt(111), EdS.Domain.Numerics.Fixed.FromInt(-16))));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "rebuilds.schema.json"))
            .IsValid(JsonDocument.Parse(Catalog()).RootElement));
    }

    [Fact]
    public void Catalogue_invalide_refuse_avec_son_chemin()
    {
        static JsonObject Site(JsonObject r, int i = 0) => r["data"]!["sites"]![i]!.AsObject();
        static JsonObject Need(JsonObject r, int stage, int need) => Site(r)["stages"]![stage]!["needs"]![need]!.AsObject();
        Refused(Read(Catalog(r => Site(r)["place"] = "nulle_part")), "$.data.sites[0].place", ValidationCode.UnknownReference);
        Refused(Read(Catalog(r => Site(r, 1)["place"] = "hameau")), "$.data.sites[1].place", ValidationCode.DuplicateId);
        Refused(Read(Catalog(r => Site(r, 1)["id"] = "hameau")), "$.data.sites[1].id", ValidationCode.DuplicateId);
        Refused(Read(Catalog(r => Site(r)["margin"] = 257)), "$.data.sites[0].margin", ValidationCode.OutOfRange);
        Refused(Read(Catalog(r => Site(r)["stages"] = new JsonArray())), "$.data.sites[0].stages", ValidationCode.MissingField);
        Refused(Read(Catalog(r => Site(r)["stages"]![1]!["id"] = "done")), "$.data.sites[0].stages[1].id", ValidationCode.InvalidValue);
        Refused(Read(Catalog(r => Site(r)["stages"]![1]!["id"] = "deblaiement")), "$.data.sites[0].stages[1].id", ValidationCode.DuplicateId);
        Refused(Read(Catalog(r => Need(r, 0, 1)["id"] = "or")), "$.data.sites[0].stages[0].needs[1].id", ValidationCode.DuplicateId);
        Refused(Read(Catalog(r => Need(r, 0, 0)["defeat"] = 3)), "$.data.sites[0].stages[0].needs[0]", ValidationCode.InvalidValue);   // deux sortes
        Refused(Read(Catalog(r => Need(r, 0, 0).Remove("coins"))), "$.data.sites[0].stages[0].needs[0]", ValidationCode.InvalidValue);   // aucune
        Refused(Read(Catalog(r => Need(r, 0, 0)["coins"] = 0)), "$.data.sites[0].stages[0].needs[0].coins", ValidationCode.OutOfRange);
        Refused(Read(Catalog(r => Need(r, 0, 0)["count"] = 2)), "$.data.sites[0].stages[0].needs[0].count", ValidationCode.UnknownField);
        Refused(Read(Catalog(r => Need(r, 0, 1)["item"] = "item.inconnu")), "$.data.sites[0].stages[0].needs[1].item", ValidationCode.UnknownReference);
        Refused(Read(Catalog(r => Need(r, 0, 1)["item"] = "item.potion_de_soin")), "$.data.sites[0].stages[0].needs[1].item", ValidationCode.InvalidValue);
        Refused(Read(Catalog(r => Need(r, 0, 1).Remove("count"))), "$.data.sites[0].stages[0].needs[1].count", ValidationCode.MissingField);
        Refused(Read(Catalog(r => Need(r, 0, 1)["count"] = 10_000)), "$.data.sites[0].stages[0].needs[1].count", ValidationCode.OutOfRange);
        Refused(Read(Catalog(r => Need(r, 0, 2)["defeat"] = 1_000)), "$.data.sites[0].stages[0].needs[2].defeat", ValidationCode.OutOfRange);
        Refused(Read(Catalog(r => Need(r, 1, 0)["discover"] = "discovery.inconnue")), "$.data.sites[0].stages[1].needs[0].discover", ValidationCode.UnknownReference);
        Refused(Read(Catalog(r => Need(r, 0, 0)["prix"] = 1)), "$.data.sites[0].stages[0].needs[0].prix", ValidationCode.UnknownField);
        Assert.False(Read(Catalog(r => r["kind"] = "seasons")).IsValid);
        Assert.False(Read(Catalog(r => r["schemaVersion"] = 2)).IsValid);
    }

    // --- Règles pures ----------------------------------------------------------------------------------

    [Fact]
    public void Regles_apport_plafonne_paliers_finis_jamais_de_recul()
    {
        RebuildSite s = Hameau;
        static bool None(RebuildNeed _) => false;
        Assert.Equal(RebuildPhase.Ruin, RebuildRules.PhaseOf(null, s));
        Assert.Null(RebuildRules.Settle(null, s, None, out _));   // rien apporté : toujours en ruine

        SavedRebuild a = RebuildRules.Give(null, s, "or", 9_999, None, out IReadOnlyList<RebuildStage> f1);
        Assert.Equal((0, 5_000L), (a.Stage, a.Of("or")));   // plafonné à ce qui reste
        Assert.Empty(f1);
        Assert.Equal(RebuildPhase.Building, RebuildRules.PhaseOf(a, s));
        a = RebuildRules.Give(a, s, "minerai", 4, None, out _);
        a = RebuildRules.Give(a, s, "minerai", 6, None, out _);
        Assert.Equal(10, a.Of("minerai"));
        a = RebuildRules.Give(a, s, "bandits", 1, None, out IReadOnlyList<RebuildStage> f2);
        Assert.Equal((1, 0), (a.Stage, a.Given.Count));   // palier fini, le suivant commence à vide
        Assert.Equal("deblaiement", Assert.Single(f2).Id);
        Assert.Throws<ArgumentException>(() => RebuildRules.Give(a, s, "minerai", 1, None, out _));   // hors du palier en cours

        // La découverte déjà faite remplit son besoin dès qu'on touche au palier ; l'or le finit, et le lieu est reconstruit.
        SavedRebuild b = RebuildRules.Give(a, s, "or", 100, n => n.Target == Traces, out IReadOnlyList<RebuildStage> f3);
        Assert.Equal("toits", Assert.Single(f3).Id);
        Assert.Equal(RebuildPhase.Rebuilt, RebuildRules.PhaseOf(b, s));
        Assert.Null(RebuildRules.CurrentStage(b, s));
        Assert.Throws<ArgumentException>(() => RebuildRules.Give(b, s, "or", 1, None, out _));   // reconstruit : rien ne s'y apporte plus
        Assert.Throws<ArgumentOutOfRangeException>(() => RebuildRules.Give(a, s, "or", 0, None, out _));

        // Accord avec le catalogue.
        RebuildBook book = Sample.Value;
        Assert.Null(RebuildRules.Mismatch(new SavedRebuilds(new[] { a, new SavedRebuild("moulin", 1, done: true) }), book));
        Assert.Contains("inconnu", RebuildRules.Mismatch(new SavedRebuilds(new[] { new SavedRebuild("abbaye", 1, done: true) }), book));
        Assert.Contains("paliers finis", RebuildRules.Mismatch(new SavedRebuilds(new[] { new SavedRebuild("hameau", 3, done: true) }), book));
        Assert.Contains("hors du palier", RebuildRules.Mismatch(new SavedRebuilds(new[] { new SavedRebuild("hameau", 0, Given(("releve", 1))) }), book));
        Assert.Contains("en demande", RebuildRules.Mismatch(new SavedRebuilds(new[] { new SavedRebuild("hameau", 0, Given(("or", 5_001))) }), book));
        Assert.Contains("reconstruit", RebuildRules.Mismatch(new SavedRebuilds(new[] { new SavedRebuild("hameau", 2, Given(("or", 1))) }), book));   // tous les paliers finis sans « done »
        // Relecture indépendante de la PR #221 : chaque écart avec son chemin et son code (erreur de chargement propre).
        var report = new ValidationReport();
        RebuildRules.Check(new SavedRebuilds(new[] { new SavedRebuild("abbaye", 1, done: true), new SavedRebuild("hameau", 0, Given(("or", 5_001), ("releve", 1))),
            new SavedRebuild("moulin", 2, done: true) }), book, report, "$.data.character.rebuilds");
        Assert.Equal(new[]
        {
            ("$.data.character.rebuilds.abbaye", ValidationCode.UnknownReference),
            ("$.data.character.rebuilds.hameau.given.or", ValidationCode.OutOfRange),
            ("$.data.character.rebuilds.hameau.given.releve", ValidationCode.UnknownReference),
            ("$.data.character.rebuilds.moulin.stage", ValidationCode.OutOfRange),
        }, report.Errors.Select(e => (e.Path, e.Code)));
    }

    [Fact]
    public void Un_lieu_reconstruit_le_reste_meme_si_le_catalogue_lui_ajoute_un_palier()
    {
        // Revue Codex de la PR #221 : le moulin (un palier) est reconstruit ; le catalogue lui ajoute un palier plus tard.
        RebuildBook grown = Read(Catalog(r => r["data"]!["sites"]![1]!["stages"]!.AsArray().Add(new JsonObject
        {
            ["id"] = "foulons",
            ["needs"] = new JsonArray(new JsonObject { ["id"] = "or", ["coins"] = 10 }),
        }))).Book!;
        var saved = new SavedRebuilds(new[] { new SavedRebuild("moulin", 1, done: true) });
        Assert.Null(RebuildRules.Mismatch(saved, grown));
        Assert.Equal(RebuildPhase.Rebuilt, RebuildRules.PhaseOf(saved.Of("moulin"), grown.Of("moulin")!));
        Assert.Null(RebuildRules.CurrentStage(saved.Of("moulin"), grown.Of("moulin")!));
        WorldSession w = Session(466, 856, Hero(100).WithRebuilds(saved), catalog: false);
        w.UseRebuilds(grown);
        Assert.Equal(RebuildPhase.Rebuilt, w.RebuildPhaseOf("moulin"));
        Assert.Equal(RebuildRefusal.Rebuilt, w.GiveToRebuild("moulin", "or", out _, out _));
        Assert.Equal(100, w.Character.Money);
        // Sans « done », les paliers finis d'avant ne suffisent pas : la sauvegarde est refusée (jamais rouverte en silence).
        Assert.NotNull(RebuildRules.Mismatch(new SavedRebuilds(new[] { new SavedRebuild("moulin", 1, Given(("or", 1))) }), Sample.Value));
    }

    private static IEnumerable<KeyValuePair<string, long>> Given(params (string Need, long Amount)[] given) =>
        given.Select(g => new KeyValuePair<string, long>(g.Need, g.Amount));

    // --- Sauvegarde v28 --------------------------------------------------------------------------------

    private static string Write(SavedCharacter c) => SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), c));

    private static readonly SavedRebuilds Saved = new(new[]
    {
        new SavedRebuild("moulin", 1, done: true),
        new SavedRebuild("hameau", 0, Given(("or", 1_200), ("bandits", 1))),
    });

    private static SaveLoadResult Mutated(Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(Write(Hero().WithRebuilds(Saved)))!.AsObject();
        mutate(root);
        return SaveJsonReader.Read(root.ToJsonString());
    }

    private static void Refused(SaveLoadResult r, string path, ValidationCode code)
    {
        Assert.False(r.IsValid);
        Assert.True(r.Report.Errors.Any(e => e.Path == path && e.Code == code), $"{path} ({code}) attendu ; {r.Report}");
    }

    private const string P = "$.data.character.rebuilds";

    [Fact]
    public void Sauvegarde_v28_aller_retour_et_v27_encore_lue()
    {
        Assert.Equal((28, 28), (SaveValidator.CurrentSchemaVersion, SaveValidator.RebuildsSince));
        string json = Write(Hero().WithRebuilds(Saved));
        Assert.Contains("\"rebuilds\": {\"hameau\": {\"stage\": 0, \"given\": {\"bandits\": 1, \"or\": 1200}}, \"moulin\": {\"stage\": 1, \"given\": {}, \"done\": true}},", json, StringComparison.Ordinal);
        SaveLoadResult back = SaveJsonReader.Read(json);
        Assert.True(back.IsValid, back.Report.ToString());
        Assert.Equal(json, Write(back.Save!.Character!));   // écriture canonique, octet par octet
        Assert.Equal(1_200, back.Save.Character!.Rebuilds.Of("hameau")!.Of("or"));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json"))
            .IsValid(JsonDocument.Parse(json).RootElement));
        // Sans chantier : pas de « rebuilds ».
        Assert.DoesNotContain("rebuilds", Write(Hero()), StringComparison.Ordinal);

        // Une sauvegarde v27 se lit (tous les lieux en ruine) et s'écrit en v28 ; elle n'a pas de « rebuilds ».
        JsonObject old = JsonNode.Parse(Write(Hero()))!.AsObject();
        old["schemaVersion"] = 27;
        SaveLoadResult v27 = SaveJsonReader.Read(old.ToJsonString());
        Assert.True(v27.IsValid, v27.Report.ToString());
        Assert.True(v27.Save!.Character!.Rebuilds.IsEmpty);
        Assert.Contains(SaveVersion.Current, Write(v27.Save.Character), StringComparison.Ordinal);
        Refused(Mutated(r => r["schemaVersion"] = 27), P, ValidationCode.UnknownField);
    }

    [Fact]
    public void Sauvegarde_invalide_refusee_avec_son_chemin()
    {
        static JsonObject R(JsonObject r) => r["data"]!["character"]!["rebuilds"]!.AsObject();
        Refused(Mutated(r => r["data"]!["character"]!["rebuilds"] = new JsonObject()), P, ValidationCode.InvalidValue);
        Refused(Mutated(r => R(r)["hameau"]!["given"]!["or"] = 0), P + ".hameau.given.or", ValidationCode.OutOfRange);
        Refused(Mutated(r => R(r)["hameau"]!["given"] = new JsonObject()), P + ".hameau", ValidationCode.InvalidValue);   // en ruine : absent
        Refused(Mutated(r => R(r)["hameau"]!["stage"] = 9), P + ".hameau.stage", ValidationCode.OutOfRange);
        Refused(Mutated(r => R(r)["moulin"]!.AsObject().Remove("given")), P + ".moulin.given", ValidationCode.MissingField);
        Refused(Mutated(r => R(r)["Moulin"] = new JsonObject { ["stage"] = 1, ["given"] = new JsonObject() }), P + ".Moulin", ValidationCode.InvalidValue);
        Refused(Mutated(r =>
        {
            var g = R(r)["hameau"]!["given"]!.AsObject();
            g.Remove("bandits");
            g["bandits"] = 1;   // après « or » : hors de l'ordre ordinal
        }), P + ".hameau.given.bandits", ValidationCode.InvalidValue);
        Refused(Mutated(r => R(r)["hameau"]!["vu"] = true), P + ".hameau.vu", ValidationCode.UnknownField);
        Refused(Mutated(r => R(r)["moulin"]!["done"] = false), P + ".moulin.done", ValidationCode.InvalidValue);
        Refused(Mutated(r => R(r)["moulin"]!["given"] = new JsonObject { ["or"] = 1 }), P + ".moulin.given", ValidationCode.InvalidValue);
        Assert.Throws<ArgumentException>(() => new SavedRebuild("moulin", 1, Given(("or", 1)), done: true));
        Assert.Throws<ArgumentException>(() => new SavedRebuild("hameau", 0));
        Assert.Throws<ArgumentException>(() => new SavedRebuilds(new[] { new SavedRebuild("moulin", 1, done: true), new SavedRebuild("moulin", 1, done: true) }));
    }

    // --- Dans la partie --------------------------------------------------------------------------------

    private static WorldSession Session(double x, double z, SavedCharacter? hero = null, bool catalog = true, Action<JsonObject>? scenario = null)
    {
        ZoneDefinition zone = Zone(x, z, scenario);
        var w = new WorldSession(new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, hero ?? Hero(20_000)), zone, ShippedData, Progression,
            ItemsCatalog, Book(zone));
        w.Step();
        if (catalog) w.UseRebuilds(Sample.Value);
        return w;
    }

    [Fact]
    public void Or_et_materiaux_apportes_sur_place_faits_appris()
    {
        WorldSession w = Session(InX, InZ);
        w.Inventory.Add(Iron, 4);
        Assert.Equal(RebuildPhase.Ruin, w.RebuildPhaseOf("hameau"));
        Assert.Equal(RebuildRefusal.None, w.GiveToRebuild("hameau", "or", out long gold, out IReadOnlyList<StoryEvent> events));
        Assert.Equal((5_000L, 15_000L), (gold, w.Character.Money));   // au plus ce qui reste
        Assert.Contains(events, e => e.Kind == StoryEventKind.FactLearned && e.Id == "fact.rebuild.hameau.started");
        Assert.True(w.Narrative!.Knows("fact.rebuild.hameau.started"));
        Assert.Equal(RebuildPhase.Building, w.RebuildPhaseOf("hameau"));
        Assert.Equal(RebuildRefusal.NeedMet, w.GiveToRebuild("hameau", "or", out _, out _));
        Assert.Equal(RebuildRefusal.None, w.GiveToRebuild("hameau", "minerai", out long iron, out events));
        Assert.Equal((4L, 0), (iron, w.Inventory.Count(Iron)));   // tout ce qu'il avait
        Assert.Empty(events);
        Assert.Equal(RebuildRefusal.NothingToGive, w.GiveToRebuild("hameau", "minerai", out _, out _));
        Assert.Equal(RebuildRefusal.NotByHand, w.GiveToRebuild("hameau", "bandits", out _, out _));
        Assert.Equal(RebuildRefusal.UnknownNeed, w.GiveToRebuild("hameau", "releve", out _, out _));   // pas dans le palier en cours
        Assert.Equal(RebuildRefusal.UnknownSite, w.GiveToRebuild("abbaye", "or", out _, out _));
        Assert.Equal(RebuildRefusal.NotThere, w.GiveToRebuild("moulin", "or", out _, out _));   // le moulin à foulon est loin
        Assert.Equal((5_000L, 4L), (w.RebuildState.Of("hameau")!.Of("or"), w.RebuildState.Of("hameau")!.Of("minerai")));

        // L'état passe par la sauvegarde.
        SaveLoadResult back = SaveJsonReader.Read(SaveJsonReader.Write(w.Save()));
        Assert.True(back.IsValid, back.Report.ToString());
        Assert.Equal(4, back.Save!.Character!.Rebuilds.Of("hameau")!.Of("minerai"));

        // Sans catalogue, ou loin : refusé, rien ne change.
        Assert.Equal(RebuildRefusal.NoRebuilds, Session(InX, InZ, catalog: false).GiveToRebuild("hameau", "or", out _, out _));
        WorldSession far = Session(-18, -12);
        Assert.Equal(RebuildRefusal.NotThere, far.GiveToRebuild("hameau", "or", out _, out _));
        Assert.Equal(20_000, far.Character.Money);
        Assert.True(far.RebuildState.IsEmpty);
        // Un état gardé hors du catalogue est refusé à la mise en jeu, rien ne change.
        WorldSession odd = Session(InX, InZ, Hero().WithRebuilds(new SavedRebuilds(new[] { new SavedRebuild("abbaye", 1, done: true) })), catalog: false);
        Assert.Throws<ArgumentException>(() => odd.UseRebuilds(Sample.Value));
        Assert.Null(odd.RebuildBook);
    }

    [Fact]
    public void Combats_autour_du_lieu_et_decouvertes_comptent_d_eux_memes_jusqu_a_la_reconstruction()
    {
        var start = new SavedRebuilds(new[] { new SavedRebuild("hameau", 0, Given(("or", 5_000), ("minerai", 10))) });
        // Le bandit du hameau, affaibli pour l'essai (un coup ou deux).
        WorldSession w = Session(FightX, FightZ, Hero(100).WithRebuilds(start), scenario: sc =>
            sc["data"]!["combatants"]!.AsArray().Single(c => (string)c!["id"]! == "bandit_epee3")!["health"] = 10);
        bool down = false;
        for (int i = 0; i < 1200 && !down; i++)
        {
            if (i % 20 == 0)
                try { w.Zone.Engine.Submit(new CombatCommand(w.Zone.Engine.CurrentTick * 50, w.CombatantId, "skill.bastion.frappe_du_rempart", w.CurrentCreature("bandit_epee3"))); }
                catch (ArgumentException) { }
            down = w.Step().Experience.Count > 0;
        }
        string? foe = w.CurrentCreature("bandit_epee3");
        Assert.True(down, $"le bandit du hameau doit tomber : {foe} {(foe is null ? "" : w.Zone.Engine.View(foe).IsDead + " " + w.Zone.Engine.View(foe).Position)} moi {w.Zone.Engine.View(w.CombatantId).IsDead} {w.Zone.Engine.View(w.CombatantId).Position}");
        w.Step();
        Assert.Equal(1, w.RebuildState.Of("hameau")!.Stage);   // premier palier fini par le combat
        Assert.True(w.Narrative!.Knows("fact.rebuild.hameau.deblaiement"));
        Assert.False(w.Narrative.Knows("fact.rebuild.hameau.done"));

        // La découverte, faite n'importe où, remplit son besoin au tick suivant ; l'or finit le chantier.
        w.Narrative.Discover(Traces, new List<StoryEvent>());
        w.Step();
        Assert.Equal(1, w.RebuildState.Of("hameau")!.Of("releve"));
        Assert.Equal(RebuildRefusal.None, w.GiveToRebuild("hameau", "or", out long gold, out IReadOnlyList<StoryEvent> events));
        Assert.Equal(100, gold);
        Assert.Equal(new[] { "fact.rebuild.hameau.toits", "fact.rebuild.hameau.done" }, events.Where(e => e.Kind == StoryEventKind.FactLearned).Select(e => e.Id));
        Assert.Equal(RebuildPhase.Rebuilt, w.RebuildPhaseOf("hameau"));
        Assert.Equal(RebuildRefusal.Rebuilt, w.GiveToRebuild("hameau", "or", out _, out _));
        Assert.Equal((2, 0), (w.RebuildState.Of("hameau")!.Stage, w.RebuildState.Of("hameau")!.Given.Count));
    }

    // --- Revue Codex de la PR #221 : l'histoire et les marchands attendent les faits des chantiers ------------------------------

    [Fact]
    public void Histoire_et_marchands_attendent_les_faits_des_chantiers()
    {
        string path = Path.Combine(Path.GetTempPath(), "eds_rebuilds_" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, Catalog());
        IReadOnlyList<string> facts;
        try { facts = RebuildJsonReader.DeclaredFacts(path); }
        finally { File.Delete(path); }
        Assert.Equal(new[]
        {
            "fact.rebuild.hameau.started", "fact.rebuild.hameau.deblaiement", "fact.rebuild.hameau.toits", "fact.rebuild.hameau.done",
            "fact.rebuild.moulin.started", "fact.rebuild.moulin.roue", "fact.rebuild.moulin.done",
        }, facts);
        Assert.Empty(RebuildJsonReader.DeclaredFacts(path));   // fichier absent : aucun
        // Ce sont exactement les faits que la partie fait apprendre.
        Assert.Equal(facts, Sample.Value.Sites.SelectMany(s => new[] { s.StartedFact }.Concat(s.Stages.Select(s.StageFact)).Append(s.DoneFact)));

        // Une quête dont l'objectif attend le lieu reconstruit.
        ZoneDefinition zone = Vallons.Value;
        JsonNode story = JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!;
        story["data"]!["quests"]![0]!["steps"]![0]!["objective"] = new JsonObject { ["kind"] = "fact", ["id"] = "fact.rebuild.hameau.done" };
        StoryLoadResult without = StoryJsonReader.ReadStory(story.ToJsonString(), Texts, zone, ItemsCatalog, V16TestData.Interiors);
        Assert.False(without.IsValid);
        Assert.Contains(without.Report.Errors, e => e.Path == "$.data.quests[0].steps[0].objective.id" && e.Code == ValidationCode.UnknownReference);
        StoryLoadResult with = StoryJsonReader.ReadStory(story.ToJsonString(), Texts, zone, ItemsCatalog, V16TestData.Interiors, worldFacts: facts);
        Assert.True(with.IsValid, with.Report.ToString());
        Assert.True(with.Story!.CanLearn("fact.rebuild.hameau.done"));

        // Une boutique qui ouvre une fois le lieu reconstruit.
        JsonObject merchants = MerchantsJson();
        merchants["data"]!["merchants"]![0]!["fact"] = "fact.rebuild.hameau.done";
        Assert.False(MerchantJsonReader.Read(merchants.ToJsonString(), Texts, Story.Value, ItemsCatalog).IsValid);
        MerchantsLoadResult shop = MerchantJsonReader.Read(merchants.ToJsonString(), Texts, with.Story, ItemsCatalog);
        Assert.True(shop.IsValid, shop.Report.ToString());
    }
}

/// <summary>
/// Lieux reconstruits, sous-lot V2.43.2b (contenu des 5 lieux ; décisions de Damien du 19 novembre 2026) : catalogue livré
/// (<c>world/rebuilds.json</c>), besoin « aller à un endroit », PNJ et points sûrs qui n'existent qu'une fois le lieu reconstruit.
/// </summary>
[Trait("Category", "World")]
public class LieuxReconstruitsContenuTests
{
    private static readonly Lazy<ZoneDefinition> Vallons = new(() => Zone());
    private static readonly Lazy<StoryBook> Story = new(() => Book(Vallons.Value));

    private static RebuildBook Shipped(ZoneDefinition zone)
    {
        RebuildLoadResult r = RebuildJsonReader.ReadFile(RebuildJsonReader.PathIn(DataDirectory), zone, ItemsCatalog, Book(zone));
        return r.Book ?? throw new InvalidOperationException(r.Report.ToString());
    }

    private static WorldSession At(double x, double z, SavedRebuilds? rebuilds = null, long money = 0, params ItemStack[] items)
    {
        ZoneDefinition zone = Zone(x, z);
        var w = new WorldSession(new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, Hero(money, items).WithRebuilds(rebuilds ?? SavedRebuilds.Empty)),
            zone, ShippedData, Progression, ItemsCatalog, Book(zone));
        w.UseRebuilds(Shipped(zone));
        w.Step();
        return w;
    }

    [Fact]
    public void Les_cinq_lieux_livres()
    {
        RebuildBook b = Shipped(Vallons.Value);
        Assert.Equal(new[] { "hameau", "pont_effondre", "moulin_a_foulon", "tour_de_signal", "dortoir_de_l_abbaye" }, b.Sites.Select(s => s.Id));
        Assert.All(b.Sites, s => Assert.Single(s.RebuiltNpcs));
        Assert.Equal(new[] { "relais_du_pont", "pied_de_la_tour_de_signal" }, b.Sites.SelectMany(s => s.RebuiltSafePoints));
        // Les nouveaux matériaux servent (décision de Damien du 19 novembre 2026).
        var items = b.Sites.SelectMany(s => s.Stages).SelectMany(st => st.Needs).Where(n => n.Kind == RebuildNeedKind.Item).Select(n => n.Target).ToHashSet();
        Assert.Superset(new HashSet<string?> { "item.bois", "item.pierre", "item.planche", "item.pierre_taillee", "item.ferrure" }, items);
        Assert.Contains(b.Sites.SelectMany(s => s.Stages).SelectMany(st => st.Needs), n => n.Kind == RebuildNeedKind.Reach);
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "rebuilds.schema.json"))
            .IsValid(JsonDocument.Parse(File.ReadAllText(RebuildJsonReader.PathIn(DataDirectory))).RootElement));
    }

    [Fact]
    public void Pnj_et_point_sur_seulement_une_fois_le_lieu_reconstruit()
    {
        // Au relais du pont : fermé tant que le pont n'est pas reconstruit ; la passeuse n'est pas là.
        WorldSession ruin = At(1975, 178);
        Npc passeuse = ruin.Narrative!.Book.Npc("npc.passeuse_du_pont");
        Assert.True(ruin.NpcAway(passeuse));
        Assert.True(ruin.IsSecret("relais_du_pont"));
        for (int i = 0; i < 5; i++) ruin.Step();
        Assert.False(ruin.IsDiscovered(ruin.Definition!, "relais_du_pont"));
        Assert.Equal(TravelRefusal.NotDiscovered, ruin.CanTravel(ruin.Definition!, "relais_du_pont"));

        var rebuilt = new SavedRebuilds(new[] { new SavedRebuild("pont_effondre", 3, done: true) });
        WorldSession open = At(1975, 178, rebuilt);
        Assert.False(open.NpcAway(open.Narrative!.Book.Npc("npc.passeuse_du_pont")));
        Assert.False(open.IsSecret("relais_du_pont"));
        for (int i = 0; i < 5; i++) open.Step();
        Assert.True(open.IsDiscovered(open.Definition!, "relais_du_pont"));   // découvert sur place, donc voyage possible
    }

    [Fact]
    public void Aller_a_un_endroit_compte_de_lui_meme()
    {
        // Le moulin à foulon : le bief (480, 866) est le dernier besoin du premier palier.
        var start = new SavedRebuilds(new[] { new SavedRebuild("moulin_a_foulon", 0, new[] { new KeyValuePair<string, long>("planches", 15), new KeyValuePair<string, long>("ferrures", 4) }) });
        WorldSession far = At(466, 856, start);
        Assert.Equal(0, far.RebuildState.Of("moulin_a_foulon")!.Of("bief"));   // à plus de 4 m : rien
        WorldSession there = At(478, 864, start);
        Assert.Equal(1, there.RebuildState.Of("moulin_a_foulon")!.Stage);   // atteint : le palier « roue » est fini
        Assert.True(there.Narrative!.Knows("fact.rebuild.moulin_a_foulon.roue"));
        Assert.Equal(RebuildRefusal.NotByHand, far.GiveToRebuild("moulin_a_foulon", "bief", out _, out _));   // il faut y aller
    }
}
