using System.Text.Json;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Limits;

/// <summary>
/// Lot 7.1 : budgets d'exécution du moteur. Le coût de chaque traitement est réservé avant toute mutation ; un budget
/// dépassé arrête le combat proprement, au tick fautif, sans effet partiel du traitement refusé. Les frontières sont
/// testées en N − 1, N et N + 1 à partir du coût mesuré du scénario (les coûts réservés sont exacts, voir
/// <see cref="Cout_du_travail_conforme_au_modele"/>).
/// </summary>
[Trait("Category", "Engine")]
[Trait("Category", "Limits")]
public class Lot71BudgetTests
{
    /// <summary>Règles aux budgets donnés ; le budget de lignes reste au plus égal au budget de travail.</summary>
    private static CombatRules Budgets(int work = 10_000, int? events = null, int pending = 100_000) =>
        new() { MaxWorkPerTick = work, MaxEventsPerTick = events ?? Math.Min(1000, work), MaxPendingEvents = pending };

    private static (CombatResult Result, MemoryCombatLog Log) RunWith(Func<CombatRules, CombatSetup> scenario, CombatRules rules) =>
        Run(scenario(rules));

    /// <summary>Le majorant de saturation refuserait ces capacités : le filet de sécurité en cours de combat est testé sans lui.</summary>
    private static (CombatResult Result, MemoryCombatLog Log) RunUnbounded(Func<CombatRules, CombatSetup> scenario, CombatRules rules) =>
        RunWithoutQueueBound(scenario(rules));

    private static void AssertNotAborted(CombatResult result) =>
        Assert.True(result.EndReason != CombatEndReason.Aborted, result.AbortReason);

    /// <summary>Lignes de simulation avant l'arrêt (ni en-tête, ni combattants, ni enveloppe de finalisation).</summary>
    private static List<string> SimulationLines(MemoryCombatLog log, int combatants)
    {
        var lines = log.Lines.Skip(1 + combatants).ToList();
        int end = lines.FindIndex(l => l.Contains("\"event\":\"simulationAborted\"") || l.Contains("\"event\":\"combatantSummary\""));
        return lines.Take(end).ToList();
    }

    // --- Modèle de coût -----------------------------------------------------------------------

    [Fact]
    public void Cout_du_travail_conforme_au_modele()
    {
        // Tick sans rien : 2 combattants × 3 phases balayées (périodique, régénération, fin de tick) = 6, aucune ligne.
        var idle = Run(Setup(Array.Empty<CombatCommand>())).Result;
        Assert.Equal((6, 0, 0), (idle.Peaks.WorkPerTick, idle.Peaks.EventsPerTick, idle.Peaks.PendingEvents));

        // Tick d'une compétence gratuite (sans recharge) : retrait 1 + ligne skillCast 1 + impact 1 + ligne damage 1
        // + ligne de Résonance 1 + 6 passages = 11, dont 3 lignes ; une seule commande en file.
        var cast = Run(Setup(new[] { Cmd(500, Gratuite) })).Result;
        Assert.Equal((11, 3, 1), (cast.Peaks.WorkPerTick, cast.Peaks.EventsPerTick, cast.Peaks.PendingEvents));

        // Même tick avec une recharge (Frappe, 6 s) : + 1 mise en file ; la file atteint 1 pendant 6 s.
        var frappe = Run(Setup(new[] { Cmd(500, Frappe) })).Result;
        Assert.Equal((12, 3, 1), (frappe.Peaks.WorkPerTick, frappe.Peaks.EventsPerTick, frappe.Peaks.PendingEvents));
    }

    // --- Budget de travail : N − 1, N, N + 1 ---------------------------------------------------

    /// <summary>Quatre compétences gratuites au tick 10 et quatre parades ratées : un tick nettement plus chargé que les autres.</summary>
    private static CombatSetup Busy(CombatRules rules) => Setup(
        new[] { Cmd(500, Gratuite), Cmd(500, Gratuite), Cmd(500, Gratuite), Cmd(500, Gratuite), Parry(500, "gardien"), Parry(500, "gardien") },
        new CombatantSetup[] { Character(), Character("gardien", team: "cibles"), Dummy() }, rules: rules);

    [Fact]
    public void Budget_de_travail_aux_frontieres()
    {
        long w = RunWith(Busy, Budgets()).Result.Peaks.WorkPerTick;
        AssertNotAborted(RunWith(Busy, Budgets(work: (int)w + 1)).Result);   // N − 1 : le tick le plus chargé coûte w < budget
        AssertNotAborted(RunWith(Busy, Budgets(work: (int)w)).Result);       // N : coût exactement égal au budget

        var (result, log) = RunWith(Busy, Budgets(work: (int)w - 1));        // N + 1 : une unité de trop
        Assert.Equal(CombatEndReason.Aborted, result.EndReason);
        Assert.Equal(("workBudgetExceeded", 10L), (result.AbortReason, result.EndTick));
        var aborted = Assert.Single(Events(log).OfKind("simulationAborted"));
        Assert.Equal((w - 1, 10L), (aborted.Int("limit"), aborted.Tick()));
        Assert.True(result.Peaks.WorkPerTick <= w - 1);
    }

    [Fact]
    public void Arret_sans_effet_partiel_et_journal_identique_avant_l_arret()
    {
        long w = RunWith(Busy, Budgets()).Result.Peaks.WorkPerTick;
        var full = RunWith(Busy, Budgets()).Log;
        var (result, log) = RunWith(Busy, Budgets(work: (int)w - 1));
        List<string> before = SimulationLines(log, 3);
        Assert.Equal(SimulationLines(full, 3).Take(before.Count), before);   // préfixe identique, octet par octet
        // Le traitement refusé n'a rien modifié : les compteurs finaux sont ceux des lignes écrites, ni plus ni moins.
        var events = Events(log);
        foreach (string id in new[] { "bastion", "gardien" })
        {
            JsonElement summary = events.OfKind("combatantSummary").Single(e => e.Str("combatant") == id);
            Assert.Equal(events.OfKind("skillCast").Count(e => e.Str("actor") == id), summary.Int("skillsCast"));
            Assert.Equal(events.OfKind("damage").Count(e => e.Str("source") == id), summary.Int("hits"));
        }
    }

    // --- Action composée : tout ou rien --------------------------------------------------------

    /// <summary>Une compétence de zone sur trois cibles, suivie de trois commandes refusées (temps global de recharge) au même tick.</summary>
    private static CombatSetup Area(CombatRules rules) => Setup(
        new[] { Cmd(500, Onde, target: null), Cmd(500, Gratuite, target: "cible_a"), Cmd(500, Gratuite, target: "cible_a"), Cmd(500, Gratuite, target: "cible_a") },
        new CombatantSetup[] { Character(), Dummy("cible_a"), Dummy("cible_b"), Dummy("cible_c") }, rules: rules);

    [Fact]
    public void Action_de_zone_refusee_en_entier_avant_le_premier_impact()
    {
        // Tick 10 : retraits 4 + lancement 2 (recharge, ligne) + 3 refus = 9 ; la résolution réserve 3 impacts × 2
        // + Résonance 1 = 7 (16) ; puis 4 combattants × 3 passages = 12 : 28 au total. Un tick ordinaire coûte 12.
        // Avec un budget de 15, la réserve de la résolution échoue : aucune des trois cibles n'est touchée.
        Assert.Equal(28, RunWith(Area, Budgets()).Result.Peaks.WorkPerTick);
        var (result, log) = RunWith(Area, Budgets(work: 15));
        Assert.Equal(("workBudgetExceeded", 10L), (result.AbortReason, result.EndTick));
        var events = Events(log);
        Assert.Single(events.OfKind("skillCast"));                 // la commande, traitement distinct de la phase 2, a eu lieu
        Assert.Equal(3, events.OfKind("commandRejected").Count());
        Assert.Empty(events.OfKind("damage"));                     // aucun impact partiel de la phase 3
        Assert.All(new[] { "cible_a", "cible_b", "cible_c" }, id => Assert.Equal(0, result[id].DamageTaken));
        Assert.Equal((0, 0L), (result["bastion"].Resonance, result["bastion"].Hits));
    }

    [Fact]
    public void Impact_compose_rupture_et_fragilite_refuse_en_entier()
    {
        // Un impact sur un personnage en garde qui ne peut pas payer le blocage : rupture (ligne, état, mise en file),
        // dégâts (impact, ligne), Fragilité (état, mise en file, ligne). Tick 100 : retrait 1 + tour 1 + impact 9
        // + 2 × 3 passages = 17 ; un tick ordinaire coûte 6. Avec 10, la réserve de l'impact (2 + 9 > 10) échoue.
        CombatSetup Scenario(CombatRules rules) => Setup(new[] { GuardStart(0) },
            new CombatantSetup[] { Tank(), Assailant(firstAttackMs: 5000, intervalMs: 60_000, attacks: Blow(fragility: true)) },
            rules: new CombatRules
            {
                MaxWorkPerTick = rules.MaxWorkPerTick, MaxEventsPerTick = rules.MaxEventsPerTick, GuardCostPerBlock = 1000, GuardUpkeepPerSecond = 0,
            });
        var reference = RunWith(Scenario, Budgets());
        Assert.Single(Events(reference.Log).OfKind("guardBroken"));
        Assert.Equal(17, reference.Result.Peaks.WorkPerTick);

        var (result, log) = RunWith(Scenario, Budgets(work: 10));
        Assert.Equal(("workBudgetExceeded", 100L), (result.AbortReason, result.EndTick));
        var events = Events(log);
        Assert.Single(events.OfKind("attackStarted"));
        Assert.Empty(events.OfKind("guardBroken"));
        Assert.Empty(events.OfKind("stateApplied"));
        Assert.Empty(events.OfKind("damage"));
        Assert.Equal((0L, 0L), (result["bastion"].GuardBreaks, result["bastion"].DamageTaken));
    }

    // --- Travail silencieux --------------------------------------------------------------------

    [Fact]
    public void Travail_silencieux_consomme_le_budget_sans_ecrire_de_ligne()
    {
        // 64 combattants inactifs : chaque tick coûte 64 × 3 passages = 192 unités et n'écrit aucune ligne. Le budget de
        // lignes ne verrait rien ; le budget de travail arrête le combat dès le tick 0.
        CombatSetup Idle(CombatRules rules) => Setup(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { Character() }.Concat(Enumerable.Range(1, 63).Select(i => (CombatantSetup)Dummy($"m{i:00}"))), rules: rules);
        var reference = RunWith(Idle, Budgets()).Result;
        Assert.Equal((192, 0), (reference.Peaks.WorkPerTick, reference.Peaks.EventsPerTick));
        AssertNotAborted(RunWith(Idle, Budgets(work: 192, events: 1)).Result);
        var (result, log) = RunWith(Idle, Budgets(work: 191, events: 1));
        Assert.Equal(("workBudgetExceeded", 0L), (result.AbortReason, result.EndTick));
        Assert.Empty(SimulationLines(log, 64));   // arrêt sans aucune ligne de simulation écrite
    }

    [Fact]
    public void Echeances_perimees_consomment_du_travail_sans_ligne()
    {
        // Trois assaillants appliquent la Fragilité au même tick : trois échéances, dont deux périmées par le renouvellement.
        // Au tick d'expiration : 2 retraits périmés + (retrait, retrait d'état, ligne) + 4 combattants × 3 passages = 17,
        // pour une seule ligne.
        var assailants = Enumerable.Range(0, 3)
            .Select(i => (CombatantSetup)Assailant(firstAttackMs: 1000, intervalMs: 60_000, id: $"assaillant_{i}", attacks: Blow(fragility: true)));
        var setup = Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Tank() }.Concat(assailants), durationMs: 10_000);
        long expiryTick = (1000 + CombatRules.Provisional.Statuses["fragility"].DurationMs) / 50;
        var engine = new CombatEngine(setup, new MemoryCombatLog());
        while (engine.CurrentTick < expiryTick) engine.Step();
        engine.Step();
        Assert.Equal((17, 1), (engine.LastTickWork, engine.LastTickEvents));
        engine.Step();
        Assert.Equal((12, 0), (engine.LastTickWork, engine.LastTickEvents));   // tick suivant : passages seuls
    }

    // --- Capacité de la file : N − 1, N, N + 1 -------------------------------------------------

    /// <summary>Un assaillant frappe à chaque tick avec Fragilité : chaque frappe met en file son prochain tour et une échéance.</summary>
    private static CombatSetup Flood(CombatRules rules) => Setup(Array.Empty<CombatCommand>(),
        new CombatantSetup[] { Tank(), Assailant(firstAttackMs: 0, intervalMs: 50, attacks: Blow(raw: 1, fragility: true)) },
        durationMs: 10_000, rules: rules);

    [Fact]
    public void Capacite_de_la_file_aux_frontieres()
    {
        long p = RunWith(Flood, Budgets()).Result.Peaks.PendingEvents;
        Assert.True(p > 1);   // atteinte en cours de combat, pas à la construction (1 élément initial)
        AssertNotAborted(RunUnbounded(Flood, Budgets(pending: (int)p + 1)).Result);
        AssertNotAborted(RunUnbounded(Flood, Budgets(pending: (int)p)).Result);

        var (result, log) = RunUnbounded(Flood, Budgets(pending: (int)p - 1));
        Assert.Equal(("queueLimitExceeded", CombatEndReason.Aborted), (result.AbortReason, result.EndReason));
        Assert.Equal(p - 1, Assert.Single(Events(log).OfKind("simulationAborted")).Int("limit"));
        Assert.Equal(p - 1, result.Peaks.PendingEvents);   // la file n'a jamais dépassé sa capacité
    }

    [Fact]
    public void Capacite_insuffisante_pour_les_evenements_initiaux_refusee_avant_journal()
    {
        var setup = Setup(new[] { Cmd(0, Gratuite), Cmd(50, Gratuite) }, rules: Budgets(pending: 1));
        var log = new MemoryCombatLog();
        Assert.Throws<CombatSetupException>(() => new CombatEngine(setup, log));
        Assert.Empty(log.Lines);
    }

    // --- Priorité entre budgets ----------------------------------------------------------------

    [Theory]
    [InlineData(7, 1, "workBudgetExceeded")]           // travail, file et lignes dépassés : le travail l'emporte
    [InlineData(1000, 1, "queueLimitExceeded")]        // file et lignes : la file l'emporte
    [InlineData(1000, 100, "eventBudgetExceeded")]     // lignes seules
    public void Priorite_travail_puis_file_puis_lignes(int work, int pending, string reason)
    {
        // Tick 0 : retrait (1), tour de l'assaillant (prochain tour + ligne : 2), puis l'impact avec Fragilité réserve
        // 5 unités, 2 lignes et 1 place. Avec 2 lignes au plus et une seule place, les trois budgets échouent ensemble
        // quand le travail est limité à 7 (3 + 5 > 7).
        var setup = Setup(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { Tank(), Assailant(firstAttackMs: 0, intervalMs: 50, attacks: Blow(fragility: true)) },
            rules: Budgets(work: work, events: 2, pending: pending));
        var (result, _) = RunWithoutQueueBound(setup);
        Assert.Equal((reason, 0L), (result.AbortReason, result.EndTick));
    }

    // --- Déterminisme et enveloppes ------------------------------------------------------------

    [Theory]
    [InlineData("work")]
    [InlineData("queue")]
    public void Arret_deterministe_octet_par_octet(string budget)
    {
        Func<(CombatResult, MemoryCombatLog)> run = budget == "work"
            ? () => RunWith(Busy, Budgets(work: 20))
            : () => RunUnbounded(Flood, Budgets(pending: 10));
        var (first, firstLog) = run();
        var (second, secondLog) = run();
        Assert.Equal(CombatEndReason.Aborted, first.EndReason);
        Assert.Equal((first.AbortReason, first.EndTick), (second.AbortReason, second.EndTick));
        Assert.Equal(firstLog.ToString(), secondLog.ToString());
    }

    [Fact]
    public void Enveloppes_separees_sur_un_arret()
    {
        var (_, log) = RunWith(Busy, Budgets(work: 20));
        const int n = 3;
        var kinds = log.Lines.Select(l => JsonDocument.Parse(l).RootElement)
            .Select(e => e.TryGetProperty("event", out var k) ? k.GetString()! : e.GetProperty("type").GetString()!).ToList();
        Assert.Equal("header", kinds[0]);
        Assert.All(kinds.Skip(1).Take(n), k => Assert.Equal("combatant", k));        // initialisation : 1 + N lignes
        Assert.Equal(new[] { "simulationAborted" }.Concat(Enumerable.Repeat("combatantSummary", n)).Append("combatEnded"),
            kinds.Skip(kinds.Count - (2 + n)));                                       // finalisation : 2 + N lignes
        Assert.DoesNotContain("combatant", kinds.Skip(1 + n));
    }

    // --- Pics ---------------------------------------------------------------------------------

    [Fact]
    public void Pics_hors_journal()
    {
        var (result, log) = RunWith(Busy, Budgets());
        Assert.True(result.Peaks.WorkPerTick > 0);
        Assert.DoesNotContain(log.Lines, l => l.Contains("peak", StringComparison.OrdinalIgnoreCase));
    }
}
