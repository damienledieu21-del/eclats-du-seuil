using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using EdS.Simulation.Tests.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Limits;

/// <summary>
/// Lot 7.1 : admission d'un scénario par <see cref="CombatSetup.Validate"/>. Pour chaque limite de volume, N − 1 et N sont
/// acceptés et simulés, N + 1 est refusé avec le code LimitExceeded, avant toute écriture de journal.
/// </summary>
[Trait("Category", "Engine")]
[Trait("Category", "Limits")]
public class Lot71AdmissionTests
{
    private static CombatSetup Make(IEnumerable<CombatantSetup> combatants, IEnumerable<CombatCommand> commands, CombatRules? rules = null, long durationMs = 10_000) =>
        new(42, durationMs, StatRules.Provisional, DamageRules.Provisional, ProgressionRules.Provisional, rules ?? CombatRules.Provisional,
            combatants, commands, Identity);

    internal static RuleViolation AssertRefusedBeforeLog(CombatSetup setup, string field)
    {
        RuleViolation violation = Assert.Single(setup.Validate(), v => v.Fields.Contains(field) && v.Kind == RuleViolationKind.LimitExceeded);
        var log = new MemoryCombatLog();
        Assert.Throws<CombatSetupException>(() => new CombatEngine(setup, log));
        Assert.Empty(log.Lines);
        return violation;
    }

    private static void AssertAcceptedAndSimulated(CombatSetup setup)
    {
        Assert.Empty(setup.Validate());
        Assert.NotEqual(CombatEndReason.Aborted, Run(setup).Result.EndReason);
    }

    private static CombatantSetup[] WithDummies(int count) =>
        new CombatantSetup[] { Character() }.Concat(Enumerable.Range(1, count - 1).Select(i => (CombatantSetup)Dummy($"mannequin_{i:000}"))).ToArray();

    // --- Combattants (valeur configurée initiale : 64) ---------------------------------------

    [Theory]
    [InlineData(63)]
    [InlineData(64)]
    public void Combattants_jusqu_a_la_limite_acceptes(int count) =>
        AssertAcceptedAndSimulated(Make(WithDummies(count), new[] { Cmd(0, Onde, target: null) }));

    [Fact]
    public void Combattant_au_dela_de_la_limite_refuse_avant_journal()
    {
        RuleViolation v = AssertRefusedBeforeLog(Make(WithDummies(65), new[] { Cmd(0, Onde, target: null) }), "combatants");
        Assert.Contains("65 combattants, au plus 64", v.Message);
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void Combattants_avec_une_limite_configuree(int count, bool accepted)
    {
        var setup = Make(WithDummies(count), Array.Empty<CombatCommand>(), new CombatRules { MaxCombatants = 3 });
        if (accepted) AssertAcceptedAndSimulated(setup);
        else AssertRefusedBeforeLog(setup, "combatants");
    }

    [Fact]
    public void Scenario_hors_limites_non_analyse_element_par_element()
    {
        // 300 combattants aux identifiants invalides : une seule erreur de volume, aucune erreur par élément.
        var many = Enumerable.Range(0, 300).Select(i => (CombatantSetup)Dummy("X" + i)).ToArray();
        Assert.Single(Make(many, Array.Empty<CombatCommand>()).Validate());
    }

    // --- Commandes (valeur configurée initiale : 20 000) -------------------------------------

    private static CombatCommand[] Commands(int count) =>
        Enumerable.Range(0, count).Select(i => Cmd(i / 2 * 50L, Gratuite)).ToArray();   // 2 par tick, sous la limite par acteur

    [Theory]
    [InlineData(19_999)]
    [InlineData(20_000)]
    public void Commandes_jusqu_a_la_limite_acceptees(int count) =>
        AssertAcceptedAndSimulated(Make(new CombatantSetup[] { Character(), Dummy() }, Commands(count), durationMs: 600_000));

    [Fact]
    public void Commande_au_dela_de_la_limite_refusee_avant_journal() =>
        AssertRefusedBeforeLog(Make(new CombatantSetup[] { Character(), Dummy() }, Commands(20_001), durationMs: 600_000), "commands");

    // --- Commandes par acteur et par tick (valeur configurée initiale : 4) -------------------

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void Commandes_simultanees_d_un_acteur_jusqu_a_la_limite(int count) =>
        AssertAcceptedAndSimulated(Make(new CombatantSetup[] { Character(), Dummy() }, Enumerable.Repeat(Cmd(500, Gratuite), count)));

    [Fact]
    public void Cinquieme_commande_simultanee_refusee_a_son_index_une_seule_fois()
    {
        // Commandes du même acteur au même tick, mêlées à d'autres : erreur sur la 5ᵉ (index 6), pas sur la 6ᵉ.
        var commands = new[]
        {
            Cmd(500, Gratuite), Cmd(550, Gratuite), Cmd(500, Gratuite), Cmd(500, Gratuite), Cmd(550, Gratuite),
            Cmd(500, Gratuite), Cmd(500, Gratuite), Cmd(500, Gratuite),
        };
        var setup = Make(new CombatantSetup[] { Character(), Dummy() }, commands);
        RuleViolation v = AssertRefusedBeforeLog(setup, "commands[6].atMs");
        Assert.Contains("5ᵉ commande de « bastion » au tick 10", v.Message);
        Assert.Single(setup.Validate());
    }

    [Fact]
    public void Limite_par_acteur_independante_entre_acteurs_et_ticks()
    {
        // Quatre commandes pour chacun de deux acteurs au même tick, et quatre au tick suivant : tout est admis.
        var commands = new List<CombatCommand>();
        foreach (string actor in new[] { "alpha", "beta" })
            foreach (long at in new[] { 500L, 550L })
                for (int k = 0; k < 4; k++) commands.Add(Cmd(at, Gratuite, actor: actor));
        var setup = Make(new CombatantSetup[] { Character("alpha"), Character("beta"), Dummy() }, commands);
        AssertAcceptedAndSimulated(setup);
    }

    [Fact]
    public void Commandes_simultanees_executees_dans_un_ordre_stable()
    {
        // Même tick : initiative décroissante, puis identifiant, puis ordre du fichier, quel que soit l'ordre d'écriture.
        CombatantSetup[] fighters = { Character("beta", initiative: 0), Character("alpha", initiative: 0), Character("gamma", initiative: 5), Dummy() };
        CombatCommand[] commands = { Cmd(500, Gratuite, actor: "beta"), Cmd(500, Gratuite, actor: "alpha"), Cmd(500, Gratuite, actor: "gamma") };
        string[] Order(CombatCommand[] cmds) => Events(Run(Make(fighters, cmds)).Log).OfKind("skillCast").Select(e => e.Str("actor")!).ToArray();
        Assert.Equal(new[] { "gamma", "alpha", "beta" }, Order(commands));
        Assert.Equal(new[] { "gamma", "alpha", "beta" }, Order(commands.Reverse().ToArray()));
    }

    // --- Capacité de la file à l'initialisation ----------------------------------------------

    [Theory]
    [InlineData(8, true)]    // 8 commandes + 1 premier tour = 9  (N − 1)
    [InlineData(9, true)]    // 10 = N
    [InlineData(10, false)]  // 11 = N + 1
    public void Evenements_initiaux_compares_a_la_capacite_reelle(int commandCount, bool accepted)
    {
        var rules = new CombatRules { MaxPendingEvents = 10 };
        var setup = Make(new CombatantSetup[] { Tank(), Assailant() },
            Enumerable.Range(0, commandCount).Select(i => GuardStart(i * 50L)), rules);
        Assert.Equal(commandCount + 1, setup.InitialEventCount());
        if (accepted) Assert.Empty(setup.Validate());
        else Assert.Contains("capacité de la file 10", AssertRefusedBeforeLog(setup, "commands").Message);
    }

    [Fact]
    public void Premier_tour_apres_la_fin_non_compte()
    {
        var setup = Make(new CombatantSetup[] { Tank(), Assailant(firstAttackMs: 10_000) }, Array.Empty<CombatCommand>(),
            new CombatRules { MaxPendingEvents = 1 });
        Assert.Equal(0, setup.InitialEventCount());
        Assert.Empty(setup.Validate());
    }
}
