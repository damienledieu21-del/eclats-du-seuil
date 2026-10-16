using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Engine;

[Trait("Category", "Engine")]
public class IsolationAndLimitsTests
{
    private static IEnumerable<CombatCommand> Busy(long durationMs) =>
        Enumerable.Range(0, (int)(durationMs / 1000)).SelectMany(i => new[] { Cmd(i * 1000L, Frappe), Cmd(i * 1000L, Gratuite), Cmd(i * 1000L, Heurt) });

    // --- Aucun état partagé entre combats ---------------------------------------------------

    [Fact]
    public void Combats_entrelaces_identiques_aux_combats_separes()
    {
        CombatSetup a = Setup(Busy(10_000), seed: 1);
        CombatSetup b = Setup(Busy(10_000), seed: 2);
        string aloneA = Run(a).Log.ToString(), aloneB = Run(b).Log.ToString();

        var logA = new MemoryCombatLog();
        var logB = new MemoryCombatLog();
        var engineA = new CombatEngine(a, logA);
        var engineB = new CombatEngine(b, logB);
        bool runningA = true, runningB = true;
        while (runningA || runningB)
        {
            if (runningB) runningB = engineB.Step();
            if (runningA) runningA = engineA.Step();
        }
        Assert.Equal(aloneA, logA.ToString());
        Assert.Equal(aloneB, logB.ToString());
    }

    [Fact]
    public void Combats_paralleles_identiques()
    {
        CombatSetup setup = Setup(Busy(10_000), seed: 99);
        string reference = Run(setup).Log.ToString();
        string[] logs = new string[16];
        Parallel.For(0, logs.Length, i => logs[i] = Run(setup).Log.ToString());
        Assert.All(logs, log => Assert.Equal(reference, log));
    }

    [Fact]
    public void Le_combat_ne_modifie_ni_la_configuration_ni_les_definitions()
    {
        CombatSetup setup = Setup(Busy(5_000));
        string header = CombatLogHeader.Write(setup);
        Run(setup);
        Assert.Equal(header, CombatLogHeader.Write(setup));
        Assert.Equal(15, setup.Commands.Count);
        Assert.Equal(6000, Essai.Skills[0].Combat!.CooldownMs);
    }

    // --- Limites et débordements ------------------------------------------------------------

    [Fact]
    public void Budget_d_evenements_par_tick_interrompt_proprement()
    {
        // Cinq commandes simultanées : la limite d'admission par acteur et par tick (lot 7.1) est relevée à 5.
        var rules = new CombatRules { MaxEventsPerTick = 3, MaxCommandsPerActorPerTick = 5 };
        var commands = Enumerable.Range(0, 5).Select(_ => Cmd(0, Inerte));
        var (result, log) = Run(Setup(commands, new CombatantSetup[] { Character(actives: new[] { Inerte }), Dummy() }, rules: rules));
        Assert.Equal(CombatEndReason.Aborted, result.EndReason);
        Assert.Equal("eventBudgetExceeded", result.AbortReason);
        var events = Events(log);
        Assert.Equal(3, events.OfKind("commandRejected").Count());
        Assert.Equal(3, Assert.Single(events.OfKind("simulationAborted")).Int("limit"));
        Assert.Equal("aborted", events[^1].Str("reason"));
    }

    [Fact]
    public void Debordement_numerique_refuse_avant_le_premier_tick()
    {
        // Correctif 6.1 (M04) : un coefficient démesuré était accepté puis interrompait le combat (« overflow »).
        // Il est désormais refusé par la validation, avant toute construction de l'état du combat.
        var huge = Class(frappeCoefficient: "1000000000");
        var setup = Setup(new[] { Cmd(0, Frappe) }, new CombatantSetup[] { Character(definition: huge, attributes: Attrs(force: 1_000_000)), Dummy() });
        var log = new MemoryCombatLog();
        var error = Assert.Throws<CombatSetupException>(() => new CombatEngine(setup, log));
        Assert.Contains(error.Violations, v => v.Fields.Any(f => f.EndsWith(".coefficient")));
        Assert.Empty(log.Lines);   // rien n'est journalisé : le combat n'a pas commencé
    }

    [Fact]
    public void Duree_maximale_des_regles()
    {
        var rules = new CombatRules { MaxDurationMs = 5_000 };
        var error = Assert.Throws<CombatSetupException>(() => new CombatEngine(Setup(Array.Empty<CombatCommand>(), durationMs: 5_050, rules: rules), new MemoryCombatLog()));
        Assert.Contains(error.Violations, v => v.Fields.Contains("durationMs"));
    }

    [Theory]
    [InlineData(10_000)]   // à la fin exacte du combat (exclue)
    [InlineData(-50)]
    [InlineData(1025)]     // pas un multiple de 50 ms
    public void Instant_de_commande_invalide(long atMs)
    {
        var violations = Setup(new[] { Cmd(atMs, Frappe) }).Validate();
        Assert.Contains(violations, v => v.Fields.Contains("commands[0].atMs"));
    }

    [Fact]
    public void Recharge_non_multiple_du_tick_refusee()
    {
        var setup = Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Character(definition: Class(frappeCooldownMs: 6025)), Dummy() });
        Assert.Contains(setup.Validate(), v => v.Fields.Contains("combatants[0].class"));
    }

    [Fact]
    public void Configuration_incoherente_refusee_avant_le_combat()
    {
        CombatantSetup[] duplicate = { Character(), Character(), Dummy() };
        CombatantSetup[] oneTeam = { Character(), Dummy(team: "joueurs") };
        Assert.Contains(Setup(Array.Empty<CombatCommand>(), duplicate).Validate(), v => v.Fields.Contains("combatants[1].id"));
        Assert.Contains(Setup(Array.Empty<CombatCommand>(), oneTeam).Validate(), v => v.Fields.Contains("combatants"));
        Assert.Contains(Setup(new[] { Cmd(0, Frappe, actor: "personne") }).Validate(), v => v.Fields.Contains("commands[0].actor"));
        Assert.Contains(Setup(new[] { Cmd(0, Frappe, target: "personne") }).Validate(), v => v.Fields.Contains("commands[0].target"));
        Assert.Contains(Setup(Array.Empty<CombatCommand>(), seed: -1).Validate(), v => v.Fields.Contains("seed"));
        Assert.Contains(Setup(Array.Empty<CombatCommand>(), rules: new CombatRules { TickMilliseconds = 30 }).Validate(),
            v => v.Fields.Contains("rules.combat.TickMilliseconds"));
    }

    [Fact]
    public void Build_invalide_refuse()
    {
        // Cinq actives au niveau 10 (quatre emplacements).
        var setup = Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Character(actives: new[] { Frappe, Heurt, Onde, Gratuite, Inerte }), Dummy() });
        Assert.Contains(setup.Validate(), v => v.Fields.Contains("combatants[0].build") && v.Message.Contains("TooManySkills"));
    }
}
