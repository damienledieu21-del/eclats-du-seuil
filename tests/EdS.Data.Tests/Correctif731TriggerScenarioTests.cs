using System.Text.Json;
using EdS.Data;
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
/// <c>Fixtures/reference-0.24.0-contenu-0.6.0-regles-0.12.0/entrainement_declencheurs.jsonl</c> (29 octobre 2026 : moteur 0.24.0, format 18,
/// règles 0.12.0, Bastion 0.6.0 ; les références 0.9.1 à 0.24.0-contenu-0.6.0 restent figées et refusées au rejeu ; le moteur reproduit les lignes
/// d'événements des références 0.14.0, avec le contenu figé du 8.4, 0.14.0-contenu-0.4.0 avec celui du 8.5, et 0.14.0-contenu-0.5.0 avec
/// le contenu livré). Un assaillant n'a pas de puissance : ses frappes et sa brûlure de réaction infligent 0. Sous-lot 8.4 : les assertions
/// métier (mécanique des déclencheurs du correctif 7.3.1) se font sur le contenu historique du Bastion
/// (<see cref="ScenarioTestData.HistoricalData"/>) et, depuis le 8.5, sur le scénario figé du 8.4 ; avec le contenu livré, le Heurt de
/// bouclier étourdit l'assaillant et les talents du Bastion réagissent, ce que fige la référence du contenu 0.4.0.
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

    /// <summary>
    /// Référence du contenu livré (sous-lot V1.8d : moteur 0.27.0, format 21 ; Bastion 0.6.0, règles 0.15.0 : barre « 4 + ultime dès le début », sous-lot
    /// V1.8). Celle des règles 0.14.0 (bornes des effets d'équipement, sous-lot V1.3) reste figée.
    /// </summary>
    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference-0.29.0-contenu-0.6.0-regles-0.17.0", Name + ".jsonl");   // V2.4

    /// <summary>
    /// En-tête sans ce qui distingue légitimement deux moteurs (format et version, sous-lot 11.1) ni, sur demande, l'identité des règles
    /// et (sous-lot 11.4b) celle des classes : une classe figée au schéma 9 n'a plus l'empreinte qu'elle avait au schéma 8, et le Bastion
    /// livré a changé de contenu (portées, sans effet sur un combat sans espace). L'identifiant de chaque classe reste comparé.
    /// </summary>
    private static string StableHeader(string line, bool withoutRules = false, bool withoutClasses = false)
    {
        var h = System.Text.Json.Nodes.JsonNode.Parse(line)!.AsObject();
        Assert.True(h.Remove("formatVersion"));
        Assert.True(h.Remove("engineVersion"));
        if (withoutRules)
        {
            System.Text.Json.Nodes.JsonObject rules = h["data"]!["rules"]!.AsObject();
            Assert.True(rules.Remove("fingerprint"));
            Assert.True(rules.Remove("contentVersion"));
        }
        if (withoutClasses)
            foreach (System.Text.Json.Nodes.JsonNode? c in h["data"]!["classes"]!.AsArray())
            {
                Assert.True(c!.AsObject().Remove("fingerprint"));
                Assert.True(c.AsObject().Remove("contentVersion"));
            }
        return h.ToJsonString();
    }

    private static string FrozenPath(string version) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference-" + version, Name + ".jsonl");

    /// <summary>Contenu historique et scénario figé du 8.4 par défaut ; données explicites : scénario livré.</summary>
    private static ScenarioLoadResult Read(GameData? data = null) =>
        ScenarioJsonReader.ReadFile(data is null ? FrozenScenarioPath(Name) : ScenarioPath(Name), data ?? HistoricalData);

    private static CombatSetup Load(GameData? data = null)
    {
        ScenarioLoadResult result = Read(data);
        Assert.True(result.IsValid, result.Report.ToString());
        return result.Setup!;
    }

    /// <summary>Contenu historique par défaut (assertions métier) ; <paramref name="data"/> = données livrées pour la référence figée.</summary>
    private static MemoryCombatLog Simulate(GameData? data = null)
    {
        var log = new MemoryCombatLog();
        CombatEngine.Run(Load(data), log);
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
        Assert.Equal(16, graph.Cited.Count);   // sous-lots 8.4 et 8.5 : contenu livré (19 avec le contenu historique, référence 0.13.0)
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
        // Données livrées : c'est ce journal que la CI compare octet par octet (scénario de référence).
        string expected = File.ReadAllText(FixturePath);
        Assert.Equal(expected, Simulate(ShippedData).ToString());
        Assert.Equal(expected, Simulate(ShippedData).ToString());
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(File.ReadAllLines(FixturePath), ShippedData).Status);
    }

    [Fact]
    public void Reference_du_contenu_0_3_0_refusee_au_rejeu_et_reproduite_par_le_moteur_avec_le_contenu_fige()
    {
        // Sous-lot 8.5 : la référence 0.14.0 (contenu du 8.4) n'est plus rejouable avec les données livrées, dont l'identité a changé ;
        // le moteur la reproduit avec le contenu figé du 8.4, en-tête compris hormis l'identité des règles (trois altérations ajoutées au
        // catalogue, sans effet sur ce scénario). Sous-lot 11.1 : refusée aussi par sa version de moteur (0.14.0, format 8) ; le moteur
        // 0.15.0 en reproduit octet par octet les lignes d'événements, et l'en-tête hormis format et version.
        string[] frozen = File.ReadAllLines(FrozenPath("0.14.0"));
        ReplayOutcome outcome = CombatLogReplayer.Replay(frozen, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Null(outcome.Comparison);
        Assert.Contains(outcome.Problems, p => p.Contains("0.14.0", StringComparison.Ordinal));

        ScenarioLoadResult loaded = ScenarioJsonReader.ReadFile(FrozenScenarioPath(Name), FrozenData);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        var log = new MemoryCombatLog();
        CombatEngine.Run(loaded.Setup!, log);
        Assert.Equal(frozen.Skip(1), log.Lines.Skip(1));
        Assert.Equal(StableHeader(frozen[0], withoutRules: true, withoutClasses: true), StableHeader(log.Lines[0], withoutRules: true, withoutClasses: true));
    }

    [Fact]
    public void Reference_du_contenu_0_4_0_refusee_au_rejeu_et_reproduite_par_le_moteur_avec_le_contenu_fige()
    {
        // Sous-lot 8.7 : la référence du 8.5 (Bastion 0.4.0) n'est plus rejouable avec les données livrées (identité de la classe
        // changée) ; le moteur la reproduit avec le Bastion figé du 8.5 et les règles livrées (inchangées). Le combat ne lance pas de
        // Provocation : ses lignes d'événements sont aussi celles de la référence du 0.5.0. Sous-lot 11.1 : refusée aussi par sa version
        // de moteur, même avec le Bastion figé ; le moteur 0.15.0 en reproduit octet par octet les lignes d'événements, et l'en-tête hormis
        // format et version. Sous-lot 11.4a : les règles livrées ont changé d'identité (0.9.0, déplacement et contact, sans effet sur un
        // combat sans espace) : l'en-tête est comparé hors identité des règles.
        string[] frozen = File.ReadAllLines(FrozenPath("0.14.0-contenu-0.4.0"));
        ReplayOutcome outcome = CombatLogReplayer.Replay(frozen, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Null(outcome.Comparison);
        Assert.Contains(outcome.Problems, p => p.Contains("class.bastion", StringComparison.Ordinal));
        ReplayOutcome withFrozenClass = CombatLogReplayer.Replay(frozen, Content040Data);
        Assert.Equal(ReplayStatus.Refused, withFrozenClass.Status);
        Assert.Contains(withFrozenClass.Problems, p => p.Contains("0.14.0", StringComparison.Ordinal));

        ScenarioLoadResult loaded = ScenarioJsonReader.ReadFile(ScenarioPath(Name), Content040Data);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        var log = new MemoryCombatLog();
        CombatEngine.Run(loaded.Setup!, log);
        Assert.Equal(frozen.Skip(1), log.Lines.Skip(1));
        Assert.Equal(StableHeader(frozen[0], withoutRules: true, withoutClasses: true), StableHeader(log.Lines[0], withoutRules: true, withoutClasses: true));
        Assert.Equal(frozen.Skip(1), File.ReadAllLines(FixturePath).Skip(1));
    }

    [Fact]
    public void Reference_du_moteur_0_14_0_au_contenu_livre_refusee_et_corps_reproduit()
    {
        // Sous-lot 11.1 : la référence du contenu livré sous le moteur 0.14.0 (format 8) est refusée au rejeu par sa version ; la nouvelle
        // référence du moteur 0.15.0 (format 9) n'en diffère que par le format et la version de l'en-tête : aucun assaillant du scénario
        // n'a de PV, le déroulé du combat ne change pas. Sous-lot 11.4a : la référence courante (0.16.0) a aussi des règles d'une autre
        // identité (0.9.0) ; corps identique.
        string[] frozen = File.ReadAllLines(FrozenPath("0.14.0-contenu-0.5.0"));
        ReplayOutcome outcome = CombatLogReplayer.Replay(frozen, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Null(outcome.Comparison);
        Assert.Contains(outcome.Problems, p => p.Contains("0.14.0", StringComparison.Ordinal));
        string[] current = File.ReadAllLines(FixturePath);
        Assert.Equal(frozen.Skip(1), current.Skip(1));
        Assert.Equal(StableHeader(frozen[0], withoutRules: true, withoutClasses: true), StableHeader(current[0], withoutRules: true, withoutClasses: true));
        Assert.Equal((8, "0.14.0"), (JsonDocument.Parse(frozen[0]).RootElement.GetProperty("formatVersion").GetInt32(),
            JsonDocument.Parse(frozen[0]).RootElement.GetProperty("engineVersion").GetString()));
        Assert.Equal((CombatEngine.LogFormatVersion, CombatEngine.Version), (JsonDocument.Parse(current[0]).RootElement.GetProperty("formatVersion").GetInt32(),
            JsonDocument.Parse(current[0]).RootElement.GetProperty("engineVersion").GetString()));
    }

    [Fact]
    public void Reference_du_moteur_0_15_0_refusee_et_corps_reproduit_sans_espace()
    {
        // Sous-lot 11.4a : la référence du sous-lot 11.1 (moteur 0.15.0, format 9, règles 0.8.0) est refusée au rejeu ; le moteur 0.16.0
        // (format 10) en reproduit octet par octet les lignes d'événements : un combat sans espace ne passe jamais par la phase de
        // déplacement. L'en-tête ne diffère que par le format, la version et l'identité des règles (combat.movement et combat.contact) ;
        // sous-lot 11.4b : la référence courante est celle du moteur 0.17.0 (Bastion 0.6.0), au même corps.
        string[] frozen = File.ReadAllLines(FrozenPath("0.15.0-contenu-0.5.0"));
        ReplayOutcome outcome = CombatLogReplayer.Replay(frozen, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Null(outcome.Comparison);
        Assert.Contains(outcome.Problems, p => p.Contains("0.15.0", StringComparison.Ordinal));
        string[] current = File.ReadAllLines(FixturePath);
        Assert.Equal(frozen.Skip(1), current.Skip(1));
        Assert.Equal(StableHeader(frozen[0], withoutRules: true, withoutClasses: true), StableHeader(current[0], withoutRules: true, withoutClasses: true));
        Assert.Equal((9, "0.15.0", "0.8.0-provisoire"), (JsonDocument.Parse(frozen[0]).RootElement.GetProperty("formatVersion").GetInt32(),
            JsonDocument.Parse(frozen[0]).RootElement.GetProperty("engineVersion").GetString(),
            JsonDocument.Parse(frozen[0]).RootElement.GetProperty("data").GetProperty("rules").GetProperty("contentVersion").GetString()));
        Assert.Equal((CombatEngine.LogFormatVersion, CombatEngine.Version, DataFormat.ProvisionalRulesContentVersion), (JsonDocument.Parse(current[0]).RootElement.GetProperty("formatVersion").GetInt32(),
            JsonDocument.Parse(current[0]).RootElement.GetProperty("engineVersion").GetString(),
            JsonDocument.Parse(current[0]).RootElement.GetProperty("data").GetProperty("rules").GetProperty("contentVersion").GetString()));
    }

    [Fact]
    public void Reference_du_moteur_0_16_0_refusee_et_corps_reproduit_sans_portee()
    {
        // Sous-lot 11.4b : la référence du sous-lot 11.4a (moteur 0.16.0, format 10, règles 0.9.0, Bastion 0.5.0) est refusée au rejeu ; le
        // moteur 0.17.0 (format 11) avec le Bastion 0.6.0 (portées) en reproduit octet par octet les lignes d'événements : sans espace, une
        // portée est ignorée. L'en-tête ne diffère que par le format, la version et l'identité des règles et de la classe.
        string[] frozen = File.ReadAllLines(FrozenPath("0.16.0-contenu-0.5.0"));
        ReplayOutcome outcome = CombatLogReplayer.Replay(frozen, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Null(outcome.Comparison);
        Assert.Contains(outcome.Problems, p => p.Contains("0.16.0", StringComparison.Ordinal));
        string[] current = File.ReadAllLines(FixturePath);
        Assert.Equal(frozen.Skip(1), current.Skip(1));
        Assert.Equal(StableHeader(frozen[0], withoutRules: true, withoutClasses: true), StableHeader(current[0], withoutRules: true, withoutClasses: true));
        JsonElement before = JsonDocument.Parse(frozen[0]).RootElement, after = JsonDocument.Parse(current[0]).RootElement;
        static string ClassVersion(JsonElement h) => h.GetProperty("data").GetProperty("classes")[0].GetProperty("contentVersion").GetString()!;
        Assert.Equal((10, "0.16.0", "0.5.0-provisoire"), (before.GetProperty("formatVersion").GetInt32(), before.GetProperty("engineVersion").GetString(), ClassVersion(before)));
        Assert.Equal((CombatEngine.LogFormatVersion, CombatEngine.Version, "0.6.0-provisoire"), (after.GetProperty("formatVersion").GetInt32(), after.GetProperty("engineVersion").GetString(), ClassVersion(after)));
    }

    [Fact]
    public void Reference_du_moteur_0_17_0_refusee_et_corps_reproduit_sans_annonce()
    {
        // Sous-lot 11.4c : la référence du sous-lot 11.4b (moteur 0.17.0, format 11, règles 0.10.0) est refusée au rejeu ; le moteur 0.18.0
        // (format 12) en reproduit octet par octet les lignes d'événements : sans espace, ni forme d'attaque, ni arc du bouclier, ni garde
        // orientée. L'en-tête ne diffère que par le format, la version et l'identité des règles (arc du bouclier, réaction des annonces).
        string[] frozen = File.ReadAllLines(FrozenPath("0.17.0-contenu-0.6.0"));
        ReplayOutcome outcome = CombatLogReplayer.Replay(frozen, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Null(outcome.Comparison);
        Assert.Contains(outcome.Problems, p => p.Contains("0.17.0", StringComparison.Ordinal));
        string[] current = File.ReadAllLines(FixturePath);
        Assert.Equal(frozen.Skip(1), current.Skip(1));
        Assert.Equal(StableHeader(frozen[0], withoutRules: true), StableHeader(current[0], withoutRules: true));   // même Bastion 0.6.0
        JsonElement before = JsonDocument.Parse(frozen[0]).RootElement;
        Assert.Equal((11, "0.17.0", "0.10.0-provisoire"), (before.GetProperty("formatVersion").GetInt32(), before.GetProperty("engineVersion").GetString(),
            before.GetProperty("data").GetProperty("rules").GetProperty("contentVersion").GetString()));
    }

    [Fact]
    public void Reference_des_regles_0_11_0_refusee_et_corps_reproduit_avec_128_combattants()
    {
        // Décision de Damien du 29 octobre 2026 (plus de réapparitions dans les Vallons) : règles 0.12.0, combat.limits.maxCombatants de 64
        // à 128. La référence du sous-lot 12.6 (règles 0.11.0) est refusée au rejeu par l'identité des règles ; le même moteur en reproduit
        // octet par octet les lignes d'événements (deux combattants, loin des deux plafonds). L'en-tête ne diffère que par l'identité des
        // règles : ni le format, ni la version du moteur, ni la classe ne changent.
        string[] frozen = File.ReadAllLines(FrozenPath("0.24.0-contenu-0.6.0"));
        ReplayOutcome outcome = CombatLogReplayer.Replay(frozen, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Null(outcome.Comparison);
        Assert.Contains(outcome.Problems, p => p.Contains("0.11.0-provisoire", StringComparison.Ordinal));
        // Sous-lot V1.2 : la référence courante est celle du moteur 0.25.0 ; la comparaison se fait avec celle des règles 0.12.0 (même moteur
        // 0.24.0), restée figée.
        string[] current = File.ReadAllLines(FrozenPath("0.24.0-contenu-0.6.0-regles-0.12.0"));
        Assert.Equal(frozen.Skip(1), current.Skip(1));
        Assert.NotEqual(frozen[0], current[0]);
        Assert.Equal(StableHeader(frozen[0], withoutRules: true), StableHeader(current[0], withoutRules: true));
        static string RulesVersion(string header) => JsonDocument.Parse(header).RootElement.GetProperty("data").GetProperty("rules").GetProperty("contentVersion").GetString()!;
        Assert.Equal(("0.11.0-provisoire", "0.12.0-provisoire"), (RulesVersion(frozen[0]), RulesVersion(current[0])));
        JsonElement before = JsonDocument.Parse(frozen[0]).RootElement, after = JsonDocument.Parse(current[0]).RootElement;
        Assert.Equal((before.GetProperty("formatVersion").GetInt32(), before.GetProperty("engineVersion").GetString()),
            (after.GetProperty("formatVersion").GetInt32(), after.GetProperty("engineVersion").GetString()));
    }

    [Fact]
    public void Reference_du_moteur_0_26_0_refusee_et_corps_reproduit_sans_bonus_modifie()
    {
        // Sous-lot V1.8d (effet immédiat des bonus) : la référence du sous-lot V1.8 (moteur 0.26.0, format 20, règles 0.15.0) est refusée au
        // rejeu ; le moteur 0.27.0 (format 21) en reproduit octet par octet les lignes d'événements : sans commande « bonus modifié », rien ne
        // change. L'en-tête ne diffère que par le format et la version.
        string[] frozen = File.ReadAllLines(FrozenPath("0.26.0-contenu-0.6.0-regles-0.15.0"));
        ReplayOutcome outcome = CombatLogReplayer.Replay(frozen, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Null(outcome.Comparison);
        Assert.Contains(outcome.Problems, p => p.Contains("0.26.0", StringComparison.Ordinal));
        string[] current = File.ReadAllLines(FixturePath);
        Assert.Equal(frozen.Skip(1), current.Skip(1));
        Assert.Equal(StableHeader(frozen[0], withoutRules: true), StableHeader(current[0], withoutRules: true));   // V2.4 : règles 0.16.0 dans la référence courante
        JsonElement before = JsonDocument.Parse(frozen[0]).RootElement, after = JsonDocument.Parse(current[0]).RootElement;
        Assert.Equal((20, "0.26.0"), (before.GetProperty("formatVersion").GetInt32(), before.GetProperty("engineVersion").GetString()));
        Assert.Equal((23, "0.29.0"), (after.GetProperty("formatVersion").GetInt32(), after.GetProperty("engineVersion").GetString()));
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(current, ShippedData).Status);
    }

    [Fact]
    public void Reference_des_regles_0_14_0_refusee_et_corps_reproduit_avec_la_barre_de_V1_8()
    {
        // Sous-lot V1.8 (décision de Damien : « 4 + ultime dès le début, puis plus en montant ») : règles 0.15.0, places actives aux niveaux
        // 1, 1, 1, 1, 10 et 20, ultime au niveau 1. La référence du sous-lot V1.3 (règles 0.14.0) est refusée au rejeu par l'identité des
        // règles ; le même moteur 0.26.0 en reproduit octet par octet les lignes d'événements (la barre ne joue qu'à la validation des builds).
        // L'en-tête ne diffère que par l'identité des règles.
        string[] frozen = File.ReadAllLines(FrozenPath("0.26.0-contenu-0.6.0-regles-0.14.0"));
        ReplayOutcome outcome = CombatLogReplayer.Replay(frozen, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Null(outcome.Comparison);
        Assert.Contains(outcome.Problems, p => p.Contains("0.14.0-provisoire", StringComparison.Ordinal));
        string[] current = File.ReadAllLines(FrozenPath("0.26.0-contenu-0.6.0-regles-0.15.0"));   // sous-lot V1.8d : figée à son tour (moteur 0.26.0)
        Assert.Equal(frozen.Skip(1), current.Skip(1));
        Assert.NotEqual(frozen[0], current[0]);
        Assert.Equal(StableHeader(frozen[0], withoutRules: true), StableHeader(current[0], withoutRules: true));
        static string RulesVersion(string header) => JsonDocument.Parse(header).RootElement.GetProperty("data").GetProperty("rules").GetProperty("contentVersion").GetString()!;
        Assert.Equal(("0.14.0-provisoire", "0.15.0-provisoire"), (RulesVersion(frozen[0]), RulesVersion(current[0])));
        JsonElement before = JsonDocument.Parse(frozen[0]).RootElement, after = JsonDocument.Parse(current[0]).RootElement;
        Assert.Equal((before.GetProperty("formatVersion").GetInt32(), before.GetProperty("engineVersion").GetString()),
            (after.GetProperty("formatVersion").GetInt32(), after.GetProperty("engineVersion").GetString()));
    }

    [Fact]
    public void Reference_du_moteur_0_23_0_refusee_et_corps_reproduit_sans_soin()
    {
        // Sous-lot 12.6 : la référence du sous-lot 12.3 (moteur 0.23.0, format 17) est refusée au rejeu ; le moteur 0.24.0 (format 18) en
        // reproduit octet par octet les lignes d'événements : un personnage sans objet de soin ne change pas. L'en-tête ne diffère que par le
        // format et la version.
        string[] frozen = File.ReadAllLines(FrozenPath("0.23.0-contenu-0.6.0"));
        ReplayOutcome outcome = CombatLogReplayer.Replay(frozen, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Null(outcome.Comparison);
        Assert.Contains(outcome.Problems, p => p.Contains("0.23.0", StringComparison.Ordinal));
        string[] current = File.ReadAllLines(FixturePath);
        Assert.Equal(frozen.Skip(1), current.Skip(1));
        // Règles 0.12.0 (29 octobre 2026) : la référence courante a aussi des règles d'une autre identité.
        Assert.Equal(StableHeader(frozen[0], withoutRules: true), StableHeader(current[0], withoutRules: true));
        JsonElement before = JsonDocument.Parse(frozen[0]).RootElement;
        Assert.Equal((17, "0.23.0"), (before.GetProperty("formatVersion").GetInt32(), before.GetProperty("engineVersion").GetString()));
        JsonElement after = JsonDocument.Parse(current[0]).RootElement;
        Assert.Equal((23, "0.29.0"), (after.GetProperty("formatVersion").GetInt32(), after.GetProperty("engineVersion").GetString()));
    }

    [Fact]
    public void Reference_du_moteur_0_22_0_refusee_et_corps_reproduit_sans_equipement()
    {
        // Sous-lot 12.3 : la référence du sous-lot 12.1 (moteur 0.22.0, format 16) est refusée au rejeu ; le moteur 0.23.0 (format 17) en
        // reproduit octet par octet les lignes d'événements : un personnage sans équipement ne change pas. L'en-tête ne diffère que par le
        // format et la version.
        string[] frozen = File.ReadAllLines(FrozenPath("0.22.0-contenu-0.6.0"));
        ReplayOutcome outcome = CombatLogReplayer.Replay(frozen, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Null(outcome.Comparison);
        Assert.Contains(outcome.Problems, p => p.Contains("0.22.0", StringComparison.Ordinal));
        string[] current = File.ReadAllLines(FixturePath);
        Assert.Equal(frozen.Skip(1), current.Skip(1));
        Assert.Equal(StableHeader(frozen[0], withoutRules: true), StableHeader(current[0], withoutRules: true));   // règles 0.12.0 depuis le 29 octobre 2026
        JsonElement before = JsonDocument.Parse(frozen[0]).RootElement;
        Assert.Equal((16, "0.22.0"), (before.GetProperty("formatVersion").GetInt32(), before.GetProperty("engineVersion").GetString()));
        JsonElement after = JsonDocument.Parse(current[0]).RootElement;
        Assert.Equal((23, "0.29.0"), (after.GetProperty("formatVersion").GetInt32(), after.GetProperty("engineVersion").GetString()));
    }

    [Fact]
    public void Reference_du_moteur_0_21_0_refusee_et_corps_reproduit_hors_zone()
    {
        // Sous-lot 12.1 : la référence du sous-lot 11.9 (moteur 0.21.0, format 15) est refusée au rejeu ; le moteur 0.22.0 (format 16) en
        // reproduit octet par octet les lignes d'événements : un combat fermé (sans zone) ne change pas. L'en-tête ne diffère que par le
        // format et la version.
        string[] frozen = File.ReadAllLines(FrozenPath("0.21.0-contenu-0.6.0"));
        ReplayOutcome outcome = CombatLogReplayer.Replay(frozen, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Null(outcome.Comparison);
        Assert.Contains(outcome.Problems, p => p.Contains("0.21.0", StringComparison.Ordinal));
        string[] current = File.ReadAllLines(FixturePath);
        Assert.Equal(frozen.Skip(1), current.Skip(1));
        Assert.Equal(StableHeader(frozen[0], withoutRules: true), StableHeader(current[0], withoutRules: true));   // règles 0.12.0 depuis le 29 octobre 2026
        JsonElement before = JsonDocument.Parse(frozen[0]).RootElement;
        Assert.Equal((15, "0.21.0"), (before.GetProperty("formatVersion").GetInt32(), before.GetProperty("engineVersion").GetString()));
        JsonElement after = JsonDocument.Parse(current[0]).RootElement;
        Assert.Equal((23, "0.29.0"), (after.GetProperty("formatVersion").GetInt32(), after.GetProperty("engineVersion").GetString()));
    }

    [Fact]
    public void Reference_du_moteur_0_20_0_refusee_et_corps_reproduit_sans_reglage_11_9()
    {
        // Sous-lot 11.9 : la référence du sous-lot 11.5 (moteur 0.20.0, format 14) est refusée au rejeu ; le moteur 0.21.0 (format 15) en
        // reproduit octet par octet les lignes d'événements : sans obstacle bas, poussée, attaque sans arrêt ni ennemi hors meute, et sans
        // étourdissement pendant une annonce, rien ne change. L'en-tête ne diffère que par le format et la version.
        string[] frozen = File.ReadAllLines(FrozenPath("0.20.0-contenu-0.6.0"));
        ReplayOutcome outcome = CombatLogReplayer.Replay(frozen, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Null(outcome.Comparison);
        Assert.Contains(outcome.Problems, p => p.Contains("0.20.0", StringComparison.Ordinal));
        string[] current = File.ReadAllLines(FixturePath);
        Assert.Equal(frozen.Skip(1), current.Skip(1));
        Assert.Equal(StableHeader(frozen[0], withoutRules: true), StableHeader(current[0], withoutRules: true));   // règles 0.12.0 depuis le 29 octobre 2026
        JsonElement before = JsonDocument.Parse(frozen[0]).RootElement;
        Assert.Equal((14, "0.20.0"), (before.GetProperty("formatVersion").GetInt32(), before.GetProperty("engineVersion").GetString()));
        JsonElement after = JsonDocument.Parse(current[0]).RootElement;
        Assert.Equal((23, "0.29.0"), (after.GetProperty("formatVersion").GetInt32(), after.GetProperty("engineVersion").GetString()));
    }

    [Fact]
    public void Reference_du_moteur_0_19_0_refusee_et_corps_reproduit_sans_comportement()
    {
        // Sous-lot 11.5 : la référence du sous-lot 11.4d (moteur 0.19.0, format 13) est refusée au rejeu ; le moteur 0.20.0 (format 14) en
        // reproduit octet par octet les lignes d'événements : sans comportement ni charge, rien ne change. L'en-tête ne diffère que par le
        // format et la version.
        string[] frozen = File.ReadAllLines(FrozenPath("0.19.0-contenu-0.6.0"));
        ReplayOutcome outcome = CombatLogReplayer.Replay(frozen, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Null(outcome.Comparison);
        Assert.Contains(outcome.Problems, p => p.Contains("0.19.0", StringComparison.Ordinal));
        string[] current = File.ReadAllLines(FixturePath);
        Assert.Equal(frozen.Skip(1), current.Skip(1));
        Assert.Equal(StableHeader(frozen[0], withoutRules: true), StableHeader(current[0], withoutRules: true));   // règles 0.12.0 depuis le 29 octobre 2026
        JsonElement before = JsonDocument.Parse(frozen[0]).RootElement;
        Assert.Equal((13, "0.19.0"), (before.GetProperty("formatVersion").GetInt32(), before.GetProperty("engineVersion").GetString()));
    }

    [Fact]
    public void Reference_du_moteur_0_18_0_refusee_et_corps_reproduit_sans_arrivee()
    {
        // Sous-lot 11.4d : la référence du sous-lot 11.4c (moteur 0.18.0, format 12) est refusée au rejeu ; le moteur 0.19.0 (format 13) en
        // reproduit octet par octet les lignes d'événements : sans arrivée ni départ, rien ne change. L'en-tête ne diffère que par le format
        // et la version (mêmes règles 0.11.0, même Bastion 0.6.0).
        string[] frozen = File.ReadAllLines(FrozenPath("0.18.0-contenu-0.6.0"));
        ReplayOutcome outcome = CombatLogReplayer.Replay(frozen, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Null(outcome.Comparison);
        Assert.Contains(outcome.Problems, p => p.Contains("0.18.0", StringComparison.Ordinal));
        string[] current = File.ReadAllLines(FixturePath);
        Assert.Equal(frozen.Skip(1), current.Skip(1));
        Assert.Equal(StableHeader(frozen[0], withoutRules: true), StableHeader(current[0], withoutRules: true));   // règles 0.12.0 depuis le 29 octobre 2026
        JsonElement before = JsonDocument.Parse(frozen[0]).RootElement;
        Assert.Equal((12, "0.18.0"), (before.GetProperty("formatVersion").GetInt32(), before.GetProperty("engineVersion").GetString()));
    }

    [Theory]
    [InlineData("0.9.1")]
    [InlineData("0.10.0")]
    [InlineData("0.11.0")]
    [InlineData("0.12.0")]
    [InlineData("0.13.0")]
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
