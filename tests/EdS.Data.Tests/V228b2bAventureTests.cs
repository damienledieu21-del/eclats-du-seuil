using System.Text.Json.Nodes;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;
using static EdS.Data.Tests.V228b2PassageTests;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.28b (2b) (PLAN_GRANDE_CARTE.md § 7) : l'aventure (<see cref="AdventureSession"/>) et le pont temps réel
/// (<see cref="RealtimeCombat"/>) suivent le personnage à chaque passage de secteur (moteur et identité du moment), et une session
/// terminée est renouvelée sans coupure sous la partie solo (zone sans chef ni événement du monde).
/// Zones d'essai : celle du V2.28b (2a) (512 × 256 m, deux secteurs) et des Vallons agrandis vers l'est (deux secteurs, sans chef ni événement).
/// </summary>
[Trait("Category", "Sector")]
public class V228b2bAventureTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v228b2b_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private string SavePath => Path.Combine(_folder, "partie", "sauvegarde.json");

    /// <summary>Pont temps réel sur la partie, branché comme celui de l'aventure.</summary>
    private static RealtimeCombat Bridge(WorldSession w) =>
        new(w.Zone.Setup, w.Zone.Engine, w.CombatantId, () => w.Step(), Fixed.FromInt(20),
            () => new CombatBinding(w.Zone.Setup, w.Zone.Engine, w.CombatantId), () => w.RenewsSessions);

    private static ZoneDefinition Short(long durationMs = 2_000) => Essai(scenario: s => s["data"]!["durationMs"] = durationMs);

    private static double X(RealtimeCombat c) => c.Player().View.Position!.Value.X.Raw / 1e4;

    // --- Pont temps réel ------------------------------------------------------------------------------------------------

    [Fact]
    public void Pont_temps_reel_suit_le_personnage_d_un_secteur_a_l_autre()
    {
        WorldSession w = Open(TwoSectors.Value);
        RealtimeCombat c = Bridge(w);
        c.SetMove(1, 0);   // vers l'est ; la marche est emportée d'une session à l'autre
        for (int i = 0; i < 4000 && w.Passages == 0; i++) c.StepOnce();
        Assert.Equal(1, w.Passages);
        Assert.Equal((w.CombatantId, 1), (c.PlayerId, c.Rebinds));
        Assert.Equal(w.Zone.Engine.CurrentTick, c.Tick);   // le temps du pont est celui du moteur d'arrivée
        c.StepOnce();   // arrivée au tick qui suit le passage
        PlayerState me = c.Player();
        Assert.Equal(w.CombatantId, me.View.Id);
        Assert.True(me.View.IsPresent);
        Assert.Contains(c.Others(), o => o.Id == "loup_est");
        Assert.DoesNotContain(c.Others(), o => o.Id == "loup_ouest");
        double x = X(c);
        for (int i = 0; i < 40; i++) c.StepOnce();
        Assert.True(X(c) > x + 5, "la marche continue de l'autre côté");
        // Près du loup de l'est, il devient la cible (cible choisie dans le moteur d'arrivée).
        c.SetMove(0, 0);
        for (int i = 0; i < 4000 && X(c) < 385; i++)
        {
            GroundVector p = c.Player().View.Position!.Value;
            c.SetMove(390 - p.X.Raw / 1e4, 128 - p.Z.Raw / 1e4);
            c.StepOnce();
        }
        Assert.Equal("loup_est", c.TargetId);
        // Retour à l'ouest : l'identité y a déjà servi, le pont suit bastion_p1.
        for (int i = 0; i < 6000 && w.Passages < 2; i++)
        {
            c.SetMove(-1, 0);
            c.StepOnce();
        }
        Assert.Equal(2, w.Passages);
        Assert.Equal(("bastion_p1", 2), (c.PlayerId, c.Rebinds));
        Assert.Contains(c.Others(), o => o.Id == "loup_ouest");
        Assert.False(c.IsFinished);
    }

    [Fact]
    public void Retours_du_dernier_tick_de_l_ancien_moteur_releves_avant_le_rebranchement()
    {
        // Ordre au passage : relever (retours du dernier tick de l'ancien moteur), propager, puis remplacer. Le rebranchement a lieu au tick
        // même où un coup porté au mannequin donne ses premiers retours : ils ne sont pas perdus, et rien n'est compté deux fois ensuite.
        TrainingSession training = TrainingSession.Load(DataDirectory, MannequinPath);
        CombatSetup setup = training.Setup;
        var plain = new RealtimeCombat(setup, training.PlayerId);
        long first = -1;
        IReadOnlyList<CombatFeedback> expected = Array.Empty<CombatFeedback>();
        for (int i = 0; i < 400 && first < 0; i++)
        {
            plain.UseSlot(0);
            plain.StepOnce();
            IReadOnlyList<CombatFeedback> got = plain.DrainFeedback();
            if (got.Any(f => f.Kind != FeedbackKind.Unavailable && f.Kind != FeedbackKind.Rejected))
            {
                first = plain.Tick;
                expected = got;
            }
        }
        Assert.True(first > 0, "aucun retour du coup porté");

        var a = new CombatEngine(setup, NullCombatLog.Instance);
        var b = new CombatEngine(setup, NullCombatLog.Instance);
        CombatEngine current = a;
        var c = new RealtimeCombat(setup, a, training.PlayerId, () =>
            {
                current.Step();
                if (ReferenceEquals(current, a) && a.CurrentTick == first) current = b;
            }, null, () => new CombatBinding(setup, current, training.PlayerId), null);
        IReadOnlyList<CombatFeedback> atSwitch = Array.Empty<CombatFeedback>();
        while (c.Rebinds == 0)
        {
            if (c.Rebinds == 0) c.UseSlot(0);
            c.StepOnce();
            atSwitch = c.DrainFeedback();
        }
        Assert.Equal(1, c.Rebinds);
        Assert.Equal(0, c.Tick);   // le moteur d'arrivée (neuf)
        Assert.Equal(expected.Select(f => (f.Kind, f.CombatantId, f.Amount)), atSwitch.Select(f => (f.Kind, f.CombatantId, f.Amount)));
        c.StepOnce();
        Assert.DoesNotContain(c.DrainFeedback(), f => f.Kind != FeedbackKind.Unavailable && f.Kind != FeedbackKind.Rejected);
    }

    [Fact]
    public void Session_terminee_renouvelee_sans_coupure_sous_le_pont()
    {
        ZoneDefinition zone = Short();
        WorldSession w = Open(zone);
        Assert.True(w.RenewsSessions);
        RealtimeCombat c = Bridge(w);
        long health = c.Player().View.Health!.Value;
        long start = w.Zone.WorldTick;
        Zone firstSession = w.Zone;
        for (int i = 0; i < 100; i++)
        {
            c.StepOnce();
            Assert.False(c.IsFinished);
        }
        // 40 ticks par session : deux renouvellements, sans tick perdu (horloge du monde continue), sans passage.
        Assert.Equal(2, w.Renewals);
        Assert.Equal((0, 2), (w.Passages, c.Rebinds));
        Assert.NotSame(firstSession, w.Zone);
        Assert.Equal(start + 100, w.Zone.WorldTick);
        Assert.Equal(w.Zone.Engine.CurrentTick, c.Tick);
        Assert.Equal(w.CombatantId, c.PlayerId);
        Assert.True(c.Player().View.IsPresent);
        Assert.Equal(health, c.Player().View.Health);
        Assert.Contains(c.Others(), o => o.Id == "loup_ouest");
        // La sauvegarde après un renouvellement se relit et reprend au même tick du monde.
        WorldSave save = w.Save();
        Assert.Equal(w.Zone.WorldTick, save.WorldTick);
        WorldSession again = Open(zone, save);
        Assert.Equal("s00_00", again.Sector);
    }

    [Fact]
    public void Zone_avec_evenement_du_monde_pas_de_renouvellement_le_pont_se_termine()
    {
        // Comme avant le V2.28b (2b) : les Vallons (événements du monde, liés à leur session jusqu'au V2.28b (2c)) ne renouvellent pas une
        // session terminée ; la partie reprend au dernier point sûr.
        ZoneDefinition zone = Zone(scenario: s => s["data"]!["durationMs"] = 2_000);
        WorldSession w = Play(zone, Hero());
        Assert.True(w.OnSectors);
        Assert.False(w.RenewsSessions);
        RealtimeCombat c = Bridge(w);
        for (int i = 0; i < 100 && !c.IsFinished; i++) c.StepOnce();
        Assert.True(c.IsFinished);
        Assert.Equal((0, 0), (w.Renewals, c.Rebinds));
    }

    [Fact]
    public void Sans_combattant_le_pont_ne_leve_pas()
    {
        // Reprise d'un passage, refus pour de bon, et reprise pendant un renouvellement : à chaque tick, le pont avance et se lit sans lever.
        foreach (string mode in new[] { "reprise", "refus", "renouvellement" })
        {
            WorldSession w = Open(mode == "renouvellement" ? Short() : TwoSectors.Value);
            RealtimeCombat c = Bridge(w);
            SectorWorld host = w.Host!;
            for (int i = 0; i < 30; i++) c.StepOnce();
            if (mode == "refus") host.RefuseForTest("bastion", "essai");
            else host.ResumeForTest("bastion");
            long tick = host.WorldTick;
            for (int i = 0; i < 40; i++)
            {
                c.SetMove(1, 0);
                c.SetGuard(i % 2 == 0);
                c.UseSlot(0);
                c.NextTarget();
                c.StepOnce();
                _ = c.Player();
                _ = c.Others();
                _ = c.Announced();
                _ = c.DrainFeedback();
            }
            Assert.Equal(tick + 40, host.WorldTick);
            if (mode == "refus")
            {
                Assert.Equal("essai", w.SectorRefusal);
                Assert.False(host.IsFollowed("bastion"));
                continue;
            }
            Assert.Null(w.SectorRefusal);
            Assert.True(host.IsFollowed("bastion"));
            Assert.Equal(w.CombatantId, c.PlayerId);
            Assert.True(c.Player().View.IsPresent);
            if (mode == "renouvellement") Assert.Equal(1, w.Renewals);
        }
    }

    [Fact]
    public void Renouvellement_au_plafond_du_registre_tick_refuse_entier()
    {
        // Atomicité : le tick qui renouvelle la session est tout ou rien près du plafond du registre (borne de l'hôte, tous les secteurs
        // ouverts) ; refusé, il ne change ni la partie, ni le pont.
        foreach (int spare in new[] { 0, -1 })
        {
            ZoneDefinition zone = Short();
            var keys = Enumerable.Range(0, SaveValidator.MaxConsumed - 500).Select(i => $"zone.essai/test/k{i:D6}").Append(PlayerCharacters.PlacesKey);
            var w = new WorldSession(new WorldSave(zone.Id, 0, 0, keys, Hero()), zone, ShippedData, Progression, ItemsCatalog);
            RealtimeCombat c = Bridge(w);
            while (!w.Zone.IsOver) c.StepOnce();
            SectorWorld host = w.Host!;
            long room = SaveValidator.MaxConsumed - host.NextTickRegistryNeed;
            for (int i = 0; i < room - spare; i++) Assert.True(w.Zone.Consume($"zone.essai/test/plein{i:D4}"));
            Assert.Equal(spare, SaveValidator.MaxConsumed - host.NextTickRegistryNeed);
            Zone before = w.Zone;
            long tick = host.WorldTick, bridgeTick = c.Tick;
            if (spare == 0)
            {
                c.StepOnce();
                Assert.Equal((1, 1), (w.Renewals, c.Rebinds));
                continue;
            }
            Assert.Throws<InvalidOperationException>(() => c.StepOnce());
            Assert.Throws<InvalidOperationException>(() => c.StepOnce());
            Assert.Same(before, w.Zone);
            Assert.Equal((tick, bridgeTick, 0, 0), (host.WorldTick, c.Tick, w.Renewals, c.Rebinds));
        }
    }

    // --- Aventure ------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Données et scénarios copiés, Vallons agrandis vers l'est (jusqu'à x = 300 : deux secteurs, s07_06 et s08_06), sans chef ni événement
    /// du monde (sinon une seule session), avec un point sûr « seuil_est » près du bord ; durée de session réglable.
    /// </summary>
    private (string Data, string Scenarios) TwoSectorVallons(long? durationMs = null)
    {
        string data = Path.Combine(_folder, "data", "v0"), scenarios = Path.Combine(_folder, "scenarios");
        foreach ((string from, string to) in new[] { (DataDirectory, data), (ScenariosDirectory, scenarios) })
            foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(to, Path.GetRelativePath(from, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, true);
            }
        string scenarioPath = Path.Combine(scenarios, "zones", "vallons.json");
        JsonObject scenario = JsonNode.Parse(File.ReadAllText(scenarioPath))!.AsObject();
        scenario["data"]!["space"]!["bounds"]!["max"]![0] = 300;
        if (durationMs is long d) scenario["data"]!["durationMs"] = d;
        // Le bord du secteur du cœur (x = 140) passe à 6 m d'un archer bandit : il est reculé de 8 m (une population part de l'intérieur).
        scenario["data"]!["combatants"]!.AsArray().Single(c => (string)c!["id"]! == "bandit_arc1")!["position"] = new JsonArray(126, 68);
        File.WriteAllText(scenarioPath, scenario.ToJsonString());
        string zonePath = Path.Combine(data, "world", "zones", "vallons.json");
        JsonObject zone = JsonNode.Parse(File.ReadAllText(zonePath))!.AsObject();
        foreach (string key in new[] { "events", "boss", "chiefs" }) zone["data"]!.AsObject().Remove(key);
        zone["data"]!["safePoints"]!.AsArray().Add(new JsonObject { ["id"] = "seuil_est", ["name"] = "Seuil de l'est", ["position"] = new JsonArray(124, 0) });
        File.WriteAllText(zonePath, zone.ToJsonString());
        // Chefs et événements du monde restent liés à leur session jusqu'au V2.28b (2c) : ils sont retirés de la zone, et de l'histoire et de la
        // Guilde, des marchands et des métiers ce qui en parle (l'élément de liste le plus proche qui les mentionne, la propriété qui porte leur nom).
        // Ce qui en découle (faits que plus rien n'apprend, références devenues inconnues) est retiré de proche en proche, d'après le
        // diagnostic du validateur.
        var removed = new HashSet<string>(Removed, StringComparer.Ordinal);
        for (int round = 0; ; round++)
        {
            foreach (string file in new[] { "story.json", "guild.json", "merchants.json", "crafting.json" })
            {
                string path = Path.Combine(data, "world", file);
                JsonNode node = JsonNode.Parse(File.ReadAllText(path))!;
                Scrub(node, removed);
                File.WriteAllText(path, node.ToJsonString());
            }
            string probe = Path.Combine(_folder, "essai", "sonde.json");
            try
            {
                AdventureSession.Load(data, scenarios, probe);
                break;
            }
            catch (InvalidDataException e) when (round < 30
                && System.Text.RegularExpressions.Regex.Match(e.Message, "« ([^»]+) » (jamais appris|inconnu)") is { Success: true } m && removed.Add(m.Groups[1].Value))
            {
            }
        }
        return (data, scenarios);
    }

    private static readonly HashSet<string> Removed = new(StringComparer.Ordinal)
    {
        "attaque_hameau", "faille_du_seuil", "chasse_de_l_aube", "gardien_du_seuil", "gardien_eveille", "fact.chef.vaincu", "chef_des_bandits", "croc_gris",
    };

    private static bool Mentions(JsonNode? node, HashSet<string> ids) => node switch
    {
        JsonValue v => v.TryGetValue(out string? text) && ids.Any(id => text.Contains(id, StringComparison.Ordinal)),
        JsonObject o => o.Any(p => ids.Contains(p.Key) || Mentions(p.Value, ids)),
        JsonArray a => a.Any(x => Mentions(x, ids)),
        _ => false,
    };

    /// <summary>Une liste sous <paramref name="node"/> (lui exclu) a-t-elle un élément qui mentionne l'un des <paramref name="ids"/> ?</summary>
    private static bool NestedListMentions(JsonNode? node, HashSet<string> ids) => node switch
    {
        JsonObject o => o.Any(p => p.Value is JsonArray a ? a.Any(x => Mentions(x, ids)) : NestedListMentions(p.Value, ids)),
        JsonArray a => a.Any(x => Mentions(x, ids)),
        _ => false,
    };

    private static void Scrub(JsonNode? node, HashSet<string> ids)
    {
        if (node is JsonObject o)
        {
            foreach (string key in o.Select(p => p.Key).ToList())
                if (ids.Contains(key)) o.Remove(key);
                else Scrub(o[key], ids);
        }
        else if (node is JsonArray a)
        {
            for (int i = a.Count - 1; i >= 0; i--)
                if (Mentions(a[i], ids) && !NestedListMentions(a[i], ids)) a.RemoveAt(i);
                else Scrub(a[i], ids);
        }
    }

    /// <summary>Partie relancée au point sûr « seuil_est » (point de reprise de la sauvegarde).</summary>
    private AdventureSession AtEastThreshold(string data, string scenarios)
    {
        Adventure(SavePath, data, scenarios).Save();
        JsonObject root = JsonNode.Parse(File.ReadAllText(SavePath))!.AsObject();
        root["data"]!["resume"] = new JsonObject { ["safePoint"] = "seuil_est", ["sector"] = "s07_06" };
        File.WriteAllText(SavePath, root.ToJsonString());
        AdventureSession a = Adventure(SavePath, data, scenarios);
        Assert.True(a.World.OnSectors, $"{a.World.Definition?.Bosses.Count} {a.World.Definition?.Events.Count} {a.World.Definition?.Region is null} {(a.World.Definition is null ? "" : SectorMap.For(a.World.Definition).HomeHoldsArea.ToString())}");
        Assert.Equal("s07_06", a.World.Sector);
        return a;
    }

    private static double AdventureX(AdventureSession a) => a.Combat.Player().View.Position!.Value.X.Raw / 1e4;

    private static void Walk(AdventureSession a, double x, double z, Func<bool> until, int max = 4000)
    {
        for (int i = 0; i < max && !until(); i++)
        {
            if (a.Combat.Player().View.Position is GroundVector p) a.Combat.SetMove(x - p.X.Raw / 1e4, z - p.Z.Raw / 1e4);
            a.Advance(0.05);
            Assert.False(a.MustContinue);
        }
        Assert.True(until(), $"arrêté en x = {AdventureX(a)}");
    }

    [Fact]
    public void Aventure_suit_le_personnage_a_l_est_puis_de_retour_halte_possible()
    {
        (string data, string scenarios) = TwoSectorVallons();
        AdventureSession a = AtEastThreshold(data, scenarios);
        AdventureMap westMap = a.Map;
        Walk(a, 175, 0, () => a.World.Passages == 1);
        Assert.Equal("s08_06", a.World.Sector);
        Assert.Equal((a.World.CombatantId, 1), (a.Combat.PlayerId, a.Combat.Rebinds));
        Assert.NotSame(westMap, a.Map);   // carte de la session du moment
        Walk(a, 175, 0, () => AdventureX(a) > 170);
        Assert.False(a.CanRest);   // aucun point sûr ici
        // Retour au point sûr du bord, dans la session de l'ouest où son identité a servi : bastion_p1. La halte y est possible.
        Walk(a, 124, 0, () => a.World.Passages == 2 && AdventureX(a) < 125);
        Assert.True(a.CanRest, $"{a.World.CombatantId} {a.Combat.PlayerId} {AdventureX(a)} {a.Combat.Player().View.Position}");
        Assert.Equal("bastion_p1", a.World.CombatantId);
        Assert.Equal("bastion_p1", a.Combat.PlayerId);
        Assert.Equal(1, a.Opened);
        Assert.True(a.Rest());
        Assert.Equal(2, a.Opened);
    }

    [Fact]
    public void Aventure_session_terminee_renouvelee_sans_coupure()
    {
        (string data, string scenarios) = TwoSectorVallons(3_000);
        AdventureSession a = AtEastThreshold(data, scenarios);
        Assert.True(a.World.RenewsSessions);
        a.DrainMessages();
        for (int i = 0; i < 150; i++)
        {
            a.Advance(0.05);
            Assert.False(a.MustContinue);
        }
        Assert.Equal(2, a.World.Renewals);
        Assert.Equal((1, 2), (a.Opened, a.Combat.Rebinds));
        Assert.DoesNotContain(a.DrainMessages(), m => m.Contains("Tu reprends", StringComparison.Ordinal));
        Assert.True(a.CanRest);
        a.Save();
        Assert.Equal(a.World.Zone.WorldTick, SaveStore.Read(SavePath).Save!.WorldTick);
    }

    [Fact]
    public void Aventure_passage_refuse_pour_de_bon_reprise_au_dernier_point_sur()
    {
        (string data, string scenarios) = TwoSectorVallons();
        AdventureSession a = AtEastThreshold(data, scenarios);
        a.World.Host!.RefuseForTest("bastion", "essai");
        a.Advance(0.05);
        Assert.True(a.Stranded);
        Assert.True(a.MustContinue);
        Assert.Contains(a.Text("text.ui.sector_refused"), a.DrainMessages());
        Assert.True(a.Continue());
        Assert.False(a.MustContinue);
        Assert.Equal(2, a.Opened);
        a.Advance(0.05);
        Assert.Null(a.World.SectorRefusal);
        Assert.True(a.CanRest);   // de retour au point sûr de reprise
    }
}
