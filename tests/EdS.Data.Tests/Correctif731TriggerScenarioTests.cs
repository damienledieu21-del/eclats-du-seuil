using System.Text.Json;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Correctif 7.3.1 (décision D4) : cinquième scénario de référence, <c>entrainement_declencheurs</c>, le seul livré avec des
/// déclencheurs. L'assaillant en porte quatre : riposte quand il est touché (0), acharnement sur ses propres coups (1, cycle
/// supprimé à la deuxième occurrence dans une chaîne), brûlure posée par réaction à chaque saignement (2, DOT de réaction) et
/// frappe sur chaque tick de DOT (3, reportée au tick suivant car la phase de résolution est passée). Les assertions métier
/// ci-dessous ne dépendent pas de l'empreinte du journal ; la comparaison octet par octet se fait contre le journal figé
/// <c>Fixtures/reference-0.13.0/entrainement_declencheurs.jsonl</c> (sous-lot 8.3 : moteur 0.13.0, format 7 ; les références 0.9.1, 0.10.0, 0.11.0 et 0.12.0 restent
/// figée, refusée au rejeu et reproduite au gameplay près par Lot81ReferenceTests). Un assaillant n'a pas de puissance : ses frappes et sa
/// brûlure de réaction infligent 0 (le lot 8 portera des déclencheurs sur le Bastion).
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Trigger")]
[Trait("Category", "Regression")]
public class Correctif731TriggerScenarioTests
{
    private const string Name = "entrainement_declencheurs";
    private const string Riposte = "assailant.assaillant/0@assaillant#0";
    private const string Acharnement = "assailant.assaillant/1@assaillant#0";
    private const string Brulure = "assailant.assaillant/2@assaillant#0";
    private const string Morsure = "assailant.assaillant/3@assaillant#0";

    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference-0.13.0", Name + ".jsonl");

    private static string FrozenPath(string version) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference-" + version, Name + ".jsonl");

    private static ScenarioLoadResult Read() => ScenarioJsonReader.ReadFile(ScenarioPath(Name), ShippedData);

    private static CombatSetup Load()
    {
        ScenarioLoadResult result = Read();
        Assert.True(result.IsValid, result.Report.ToString());
        return result.Setup!;
    }

    private static MemoryCombatLog Simulate()
    {
        var log = new MemoryCombatLog();
        CombatEngine.Run(Load(), log);
        return log;
    }

    /// <summary>Lignes d'événements du journal (sans l'en-tête), indexées par eid pour les lignes de jeu.</summary>
    private sealed class Events
    {
        public List<JsonElement> All { get; } = new();
        public Dictionary<long, JsonElement> ByEid { get; } = new();

        public Events(IEnumerable<string> lines)
        {
            foreach (string line in lines)
            {
                JsonElement e = JsonDocument.Parse(line).RootElement.Clone();
                if (e.TryGetProperty("type", out _)) continue;
                All.Add(e);
                if (e.TryGetProperty("eid", out JsonElement eid)) ByEid.Add(eid.GetInt64(), e);
            }
        }

        public IEnumerable<JsonElement> Of(string kind) => All.Where(e => e.GetProperty("event").GetString() == kind);

        public IEnumerable<JsonElement> By(string trigger, string? kind = null) =>
            All.Where(e => Str(e, "trigger") == trigger && e.GetProperty("event").GetString() != "triggerSuppressed"
                && (kind is null || e.GetProperty("event").GetString() == kind));

        public JsonElement Cause(JsonElement e) => ByEid[e.GetProperty("cause").GetInt64()];
    }

    private static string? Str(JsonElement e, string field) =>
        e.TryGetProperty(field, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static long Num(JsonElement e, string field) => e.GetProperty(field).GetInt64();

    private static int Depth(JsonElement e) => e.TryGetProperty("depth", out JsonElement d) ? d.GetInt32() : 0;

    [Fact]
    public void Scenario_lu_avec_quatre_declencheurs_et_deux_cycles_statiques_signales()
    {
        ScenarioLoadResult result = Read();
        Assert.True(result.IsValid, result.Report.ToString());
        var assailant = Assert.IsType<AssailantSetup>(result.Setup!.Combatants[1]);
        Assert.Equal(
            new[] { TriggerEvent.HitTaken, TriggerEvent.HitDealt, TriggerEvent.StateApplied, TriggerEvent.DotTick },
            assailant.Triggers.Select(t => t.On));
        Assert.Equal(new[] { "$.data.combatants[1].triggers[0]", "$.data.combatants[1].triggers[2]" },
            result.Report.Warnings.Where(w => w.Code == ValidationCode.TriggerCycle).Select(w => w.Path));
    }

    [Fact]
    public void Bilan_du_combat()
    {
        CombatResult result = CombatEngine.Run(Load(), new MemoryCombatLog());
        CombatantResult bastion = result["bastion"], assailant = result["assaillant"];
        Assert.Equal(CombatEndReason.DurationReached, result.EndReason);
        Assert.Equal((4L, 0L), (bastion.SkillsCast, bastion.CommandsRejected));
        Assert.Equal((3L, 1L, 0L, 0L), (bastion.Blocks, bastion.PerfectParries, bastion.FailedParries, bastion.GuardBreaks));
        Assert.False(bastion.IsDead);
        Assert.Equal(1190, bastion.DamageTaken);
        Assert.Equal(1190, assailant.DamageDealt);
        Assert.Equal(38, assailant.Hits);   // 10 attaques et 28 frappes de réaction
    }

    [Fact]
    public void Ripostes_au_coup_du_bastion()
    {
        var events = new Events(Simulate().Lines);
        List<JsonElement> ripostes = events.By(Riposte, "damage").ToList();
        Assert.Equal(4, ripostes.Count);   // un par compétence du Bastion ; recharge interne de 2 s
        Assert.All(ripostes, r =>
        {
            Assert.Equal(1, Depth(r));
            JsonElement observed = events.Cause(r);
            Assert.Equal(("damage", "bastion", "assaillant"), (observed.GetProperty("event").GetString(), Str(observed, "source"), Str(observed, "target")));
            Assert.Equal(("assaillant", "bastion"), (Str(r, "source"), Str(r, "target")));
        });
    }

    [Fact]
    public void Cycles_supprimes_a_la_deuxieme_occurrence_dans_la_chaine()
    {
        IReadOnlyList<string> lines = Simulate().Lines;
        var events = new Events(lines);
        CausalGraph graph = CausalGraph.Build(lines);
        List<JsonElement> suppressed = events.Of("triggerSuppressed").ToList();
        Assert.Equal(19, suppressed.Count);
        Assert.All(suppressed, s =>
        {
            Assert.Equal(("cycle", Acharnement), (Str(s, "reason"), Str(s, "trigger")));
            // L'effet cité a été produit par l'acharnement lui-même, ou en descend : le déclencheur est déjà dans sa chaîne.
            CausalGraph.Node cited = graph[Num(s, "cause")];
            Assert.Contains(Acharnement, cited.Chain);
            Assert.Equal(cited.Depth + 1, Depth(s));
        });
        // Une chaîne riposte → acharnement atteint la profondeur 2 avant d'être coupée (suppression à la profondeur 3).
        Assert.Contains(graph.Nodes.Values, n => n.Chain.SequenceEqual(new[] { Riposte, Acharnement }));
        Assert.Contains(suppressed, s => Depth(s) == 3);
        Assert.InRange(graph.Nodes.Values.Max(n => n.Depth), 2, 2);
    }

    [Fact]
    public void Brulure_posee_par_reaction_et_ses_ticks_gardent_la_provenance()
    {
        var events = new Events(Simulate().Lines);
        List<JsonElement> burns = events.Of("stateApplied").Where(e => Str(e, "state") == "burn").ToList();
        Assert.Equal(4, burns.Count);
        Assert.All(burns, b =>
        {
            Assert.Equal((1, Brulure), (Depth(b), Str(b, "trigger")));
            JsonElement bleed = events.Cause(b);
            Assert.Equal(("stateApplied", "bleed", 0), (bleed.GetProperty("event").GetString(), Str(bleed, "state"), Depth(bleed)));
            Assert.Equal(Num(bleed, "tick"), Num(b, "tick"));
        });
        List<JsonElement> ticks = events.Of("dotDamage").ToList();
        Assert.Equal(18, ticks.Count);
        Assert.All(ticks.Where(t => Str(t, "state") == "burn"), t =>
        {
            Assert.Equal((1, Brulure), (Depth(t), Str(t, "trigger")));
            Assert.Equal("burn", Str(events.Cause(t), "state"));
        });
        Assert.All(ticks.Where(t => Str(t, "state") == "bleed"), t => Assert.Equal(0, Depth(t)));
    }

    [Fact]
    public void Frappes_sur_tick_de_dot_reportees_au_tick_suivant()
    {
        var events = new Events(Simulate().Lines);
        List<JsonElement> deferred = events.All.Where(e => e.TryGetProperty("deferredFromTick", out _)).ToList();
        Assert.Equal(5, deferred.Count);   // recharge interne de 3 s
        Assert.All(deferred, d =>
        {
            Assert.Equal(Morsure, Str(d, "trigger"));
            Assert.Equal((Num(d, "deferredFromTick") + 1, "resolution"), (Num(d, "tick"), Str(d, "phase")));
            JsonElement tick = events.Cause(d);
            Assert.Equal(("dotDamage", "periodic", Num(d, "deferredFromTick")), (tick.GetProperty("event").GetString(), Str(tick, "phase"), Num(tick, "tick")));
        });
        Assert.Equal(deferred.Count, events.By(Morsure).Count());
        JsonElement ended = Assert.Single(events.Of("combatEnded"));
        Assert.Equal(0, Num(ended, "deferredDropped"));
    }

    [Fact]
    public void Frappes_de_reaction_soumises_a_la_garde_et_a_la_parade()
    {
        var events = new Events(Simulate().Lines);
        List<string?> defenses = events.All.Where(e => Depth(e) > 0 && e.GetProperty("event").GetString() == "damage").Select(e => Str(e, "defense")).ToList();
        Assert.Contains("guard", defenses);
        Assert.Contains("parry", defenses);
        Assert.Contains("unblockable", defenses);
        Assert.Empty(events.Of("reactionCancelled"));
        Assert.Empty(events.Of("simulationAborted"));
    }

    [Fact]
    public void Graphe_causal_reconstruit_depuis_le_journal_fige()
    {
        string[] lines = File.ReadAllLines(FixturePath);
        CausalGraph graph = CausalGraph.Build(lines);
        var events = new Events(lines);
        Assert.Equal(events.ByEid.Count, graph.Nodes.Count);
        Assert.Equal(19, graph.Cited.Count);
        Assert.Contains("command:0", graph.Roots.Keys);
        Assert.Contains("assailantTurn:assaillant:0", graph.Roots.Keys);
        foreach (CausalGraph.Node node in graph.Nodes.Values.Where(n => n.Depth > 0))
        {
            Assert.Equal(node.Trigger, node.Chain[^1]);
            Assert.Equal(node.Root, graph.Ancestors(node.Eid).Last().Root);
            Assert.Null(graph.Ancestors(node.Eid).Last().Cause);
        }
    }

    [Fact]
    public void Journal_identique_octet_par_octet_au_journal_fige_et_rejoue()
    {
        string expected = File.ReadAllText(FixturePath);
        Assert.Equal(expected, Simulate().ToString());
        Assert.Equal(expected, Simulate().ToString());
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(File.ReadAllLines(FixturePath), ShippedData).Status);
    }

    [Theory]
    [InlineData("0.9.1")]
    [InlineData("0.10.0")]
    [InlineData("0.11.0")]
    [InlineData("0.12.0")]
    public void Reference_d_un_moteur_anterieur_refusee_au_rejeu(string version)
    {
        // Sous-lot 8.1 : politique de compatibilité inchangée, un journal d'un autre format ou d'un autre moteur est refusé, pas migré.
        // Sous-lot 8.2 : les références figées des moteurs 0.10.0 et 0.11.0 aussi.
        ReplayOutcome outcome = CombatLogReplayer.Replay(File.ReadAllLines(FrozenPath(version)), ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Null(outcome.Comparison);
        Assert.Contains(outcome.Problems, p => p.Contains(version, StringComparison.Ordinal));
    }
}
