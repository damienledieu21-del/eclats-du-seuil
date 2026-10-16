using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using EdS.Simulation.Tests.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Regression;

/// <summary>
/// Correctif 6.1.1, contre-audit CA-M01 : quatre configurations acceptées par la validation du correctif 6.1 faisaient
/// encore déborder le moteur en cours de combat (arrêt « overflow » au lieu d'un refus avant le premier tick).
/// <list type="bullet">
/// <item>A : constante de défense presque maximale, dénominateur constante × 10 000 + défense brute non représentable ;</item>
/// <item>B : défense d'un personnage (valeur de base des règles) hors limites, absente du précontrôle ;</item>
/// <item>C : recharge globale non bornée, dernier tick + recharge en ticks au-delà de 2⁶³ (tick de 1 ms) ;</item>
/// <item>D : délai de régénération non borné, même débordement à la dépense d'endurance.</item>
/// </list>
/// Pour chacun : valeur limite valide (acceptée et simulée sans arrêt), première valeur invalide et valeurs extrêmes
/// (refusées avant toute écriture de journal). Le chemin JSON et la CLI sont couverts dans EdS.Data.Tests.
/// </summary>
[Trait("Category", "Engine")]
[Trait("Category", "Regression")]
public class CaM01NumericEnvelopeTests
{
    private const long Day = CombatRules.AbsoluteMaxDurationMs;

    private static CombatSetup Make(CombatantSetup[] combatants, CombatCommand[] commands, StatRules? stats = null, DamageRules? damage = null,
        CombatRules? rules = null) =>
        new(42, 10_000, stats ?? StatRules.Provisional, damage ?? DamageRules.Provisional, ProgressionRules.Provisional,
            rules ?? CombatRules.Provisional, combatants, commands, Identity);

    private static RuleViolation AssertRefusedBeforeLog(CombatSetup setup, string field)
    {
        RuleViolation violation = Assert.Single(setup.Validate(), v => v.Fields.Contains(field));
        var log = new MemoryCombatLog();
        var error = Assert.Throws<CombatSetupException>(() => new CombatEngine(setup, log));
        Assert.Contains(error.Violations, v => v.Fields.Contains(field));
        Assert.Empty(log.Lines);   // aucun en-tête, aucune ligne : le refus précède toute écriture
        return violation;
    }

    private static CombatResult AssertAcceptedAndSimulated(CombatSetup setup)
    {
        Assert.Empty(setup.Validate());
        CombatResult result = Run(setup).Result;
        Assert.NotEqual(CombatEndReason.Aborted, result.EndReason);
        return result;
    }

    private static AssailantAttack Magic(long raw = 100) => new("sort", raw, DamageType.Magique, SkillTargeting.Enemy, false);

    /// <summary>Personnage contre assaillant : chacun frappe l'autre, avec des dégâts physiques et magiques.</summary>
    private static CombatSetup Duel(StatRules? stats = null, DamageRules? damage = null, Fixed? assailantDefense = null)
    {
        Fixed defense = assailantDefense ?? Fixed.Zero;
        var assailant = new AssailantSetup("assaillant", "cibles", defense, defense, "bastion", 50, 500, new[] { Blow(), Magic() });
        return Make(new CombatantSetup[] { Tank(), assailant }, new[] { Cmd(0, Frappe, target: "assaillant") }, stats, damage);
    }

    // --- Cas A : dénominateur de la défense ------------------------------------------------------

    [Fact]
    public void A_constantes_a_la_limite_avec_defenses_reelles_maximales_acceptees_et_simulees()
    {
        // Constantes maximales et défenses réelles maximales des deux côtés, pour les deux types de dégâts :
        // dénominateur 10¹³ + 10¹³, sans arrêt.
        var damage = new DamageRules { ArmorConstant = DamageRules.MaxDefenseConstant, MagicResistanceConstant = DamageRules.MaxDefenseConstant };
        var stats = new StatRules { BaseArmor = CombatantStats.MaxDefense, BaseMagicResistance = CombatantStats.MaxDefense };
        CombatResult result = AssertAcceptedAndSimulated(Duel(stats, damage, CombatantStats.MaxDefense));
        Assert.True(result.Combatants.Single(c => c.Id == "bastion").DamageTaken > 0);
        Assert.True(result.Combatants.Single(c => c.Id == "assaillant").DamageTaken > 0);
    }

    [Theory]
    [InlineData(DamageRules.MaxDefenseConstant + 1)]   // première valeur invalide
    [InlineData(922_337_203_685_477)]                    // ancien contre-exemple : constante × 10 000 tient, + défense déborde
    [InlineData(922_337_203_685_478)]                    // constante × 10 000 déborde
    [InlineData(long.MaxValue)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void A_constantes_de_defense_hors_limites_refusees(long constant)
    {
        AssertRefusedBeforeLog(Duel(damage: new DamageRules { ArmorConstant = constant }), "rules.damage.ArmorConstant");
        AssertRefusedBeforeLog(Duel(damage: new DamageRules { MagicResistanceConstant = constant }), "rules.damage.MagicResistanceConstant");
        // Chemin domaine direct.
        Assert.Contains(new DamageRules { ArmorConstant = constant }.Validate(), v => v.Fields.Contains(nameof(DamageRules.ArmorConstant)));
    }

    [Fact]
    public void A_contre_exemple_d_origine_refuse_avant_le_premier_tick()
    {
        // e37234b : validation vide, puis arrêt « overflow » au tick 0 au premier coup sur le mannequin (armure 100).
        var setup = Make(new CombatantSetup[] { Character(), Dummy(armor: 100) }, new[] { Cmd(0, Frappe) },
            damage: new DamageRules { ArmorConstant = 922_337_203_685_477 });
        AssertRefusedBeforeLog(setup, "rules.damage.ArmorConstant");
    }

    // --- Cas B : défenses calculées des personnages ----------------------------------------------

    public static TheoryData<string> InvalidDefenses => new() { "1000000000.0001", "922337203685477", "922337203685477.5807" };

    [Theory]
    [MemberData(nameof(InvalidDefenses))]
    public void B_defense_calculee_d_un_personnage_hors_limites_refusee_et_localisee(string value)
    {
        Fixed defense = Fixed.Parse(value);
        foreach (StatRules stats in new[] { new StatRules { BaseArmor = defense }, new StatRules { BaseMagicResistance = defense } })
        {
            RuleViolation violation = AssertRefusedBeforeLog(Duel(stats), "combatants[0]");
            Assert.Equal(RuleViolationKind.NumericLimit, violation.Kind);
            Assert.Contains("effective calculée", violation.Message);
        }
    }

    [Fact]
    public void B_defense_d_un_personnage_verifiee_meme_s_il_n_est_jamais_frappe()
    {
        // Le mannequin n'attaque pas : la défense du personnage est quand même contrôlée (tous les participants).
        var stats = new StatRules { BaseArmor = Fixed.FromInt(922_337_203_685_477) };
        var setup = Make(new CombatantSetup[] { Character(), Dummy() }, new[] { Cmd(0, Frappe) }, stats);
        AssertRefusedBeforeLog(setup, "combatants[0]");
        Assert.DoesNotContain(setup.Validate(), v => v.Fields.Any(f => f.StartsWith("combatants[1]", StringComparison.Ordinal)));
    }

    [Fact]
    public void B_defense_d_un_personnage_a_la_limite_acceptee_et_simulee()
    {
        var stats = new StatRules { BaseArmor = CombatantStats.MaxDefense, BaseMagicResistance = CombatantStats.MaxDefense };
        AssertAcceptedAndSimulated(Duel(stats));
    }

    [Fact]
    public void B_contre_exemple_d_origine_refuse_avant_le_premier_tick()
    {
        // e37234b : validation vide, puis arrêt « overflow » au tick 20, au premier coup de l'assaillant sur le personnage.
        var setup = Make(new CombatantSetup[] { Tank(), Assailant() }, Array.Empty<CombatCommand>(),
            new StatRules { BaseArmor = Fixed.FromInt(922_337_203_685_477) });
        AssertRefusedBeforeLog(setup, "combatants[0]");
    }

    // --- Cas C : recharge globale ----------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public void C_recharge_globale_a_la_limite_acceptee_jusqu_au_dernier_tick(int tickMs)
    {
        // Compétence lancée au dernier tick : dernier tick + recharge en ticks reste représentable, y compris à 1 ms.
        var rules = new CombatRules { TickMilliseconds = tickMs, GlobalCooldownMs = Day };
        long lastMs = 10_000 - tickMs;
        var setup = Make(new CombatantSetup[] { Character(), Dummy() }, new[] { Cmd(lastMs, Gratuite) }, rules: rules);
        AssertAcceptedAndSimulated(setup);
        var cast = Events(Run(setup).Log).OfKind("skillCast").Single();
        Assert.Equal(lastMs / tickMs + Day / tickMs, cast.Int("globalCooldownReadyTick"));
    }

    [Theory]
    [InlineData(1, Day + 1)]                                    // première valeur invalide (tick de 1 ms)
    [InlineData(50, Day + 50)]                                  // première valeur invalide multiple du tick de 50 ms
    [InlineData(1, long.MaxValue)]                              // ancien contre-exemple
    [InlineData(50, long.MaxValue - long.MaxValue % 50)]
    [InlineData(1, -1)]
    [InlineData(1, long.MinValue)]
    public void C_recharge_globale_hors_limites_refusee(int tickMs, long value)
    {
        var rules = new CombatRules { TickMilliseconds = tickMs, GlobalCooldownMs = value };
        AssertRefusedBeforeLog(Make(new CombatantSetup[] { Character(), Dummy() }, new[] { Cmd(tickMs, Frappe) }, rules: rules),
            "rules.combat.GlobalCooldownMs");
        Assert.Contains(rules.Validate(), v => v.Fields.Contains(nameof(CombatRules.GlobalCooldownMs)));
    }

    // --- Cas D : délai de régénération -----------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public void D_delai_de_regeneration_a_la_limite_accepte_jusqu_au_dernier_tick(int tickMs)
    {
        // Dépense d'endurance au dernier tick : l'échéance de régénération reste représentable, y compris à 1 ms.
        var rules = new CombatRules { TickMilliseconds = tickMs, EnduranceRegenDelayMs = Day };
        var setup = Make(new CombatantSetup[] { Character(), Dummy() }, new[] { Cmd(10_000 - tickMs, Frappe) }, rules: rules);
        CombatResult result = AssertAcceptedAndSimulated(setup);
        Assert.Equal(1, result.Combatants.Single(c => c.Id == "bastion").SkillsCast);
    }

    [Fact]
    public void D_regeneration_a_1_ms_avec_valeurs_extremes_sans_debordement()
    {
        // Pire produit de la régénération : régénération/s maximale × ticks régénérés × durée du tick, sur toute la durée.
        var rules = new CombatRules { TickMilliseconds = 1, EnduranceRegenDelayMs = 0, EnduranceRegenPerSecond = int.MaxValue };
        var setup = Make(new CombatantSetup[] { Character(attributes: Attrs(endurance: 50)), Dummy() }, new[] { Cmd(0, Frappe) }, rules: rules);
        AssertAcceptedAndSimulated(setup);
    }

    [Theory]
    [InlineData(1, Day + 1)]
    [InlineData(50, Day + 50)]
    [InlineData(1, long.MaxValue)]                              // ancien contre-exemple
    [InlineData(50, long.MaxValue - long.MaxValue % 50)]
    [InlineData(1, -1)]
    [InlineData(1, long.MinValue)]
    public void D_delai_de_regeneration_hors_limites_refuse(int tickMs, long value)
    {
        var rules = new CombatRules { TickMilliseconds = tickMs, EnduranceRegenDelayMs = value };
        AssertRefusedBeforeLog(Make(new CombatantSetup[] { Character(), Dummy() }, new[] { Cmd(tickMs, Frappe) }, rules: rules),
            "rules.combat.EnduranceRegenDelayMs");
        Assert.Contains(rules.Validate(), v => v.Fields.Contains(nameof(CombatRules.EnduranceRegenDelayMs)));
    }

    // --- Débordement tardif non anticipé ---------------------------------------------------------

    /// <summary>Journal qui lève une seule fois OverflowException à la première ligne qui vérifie la condition.</summary>
    private sealed class OverflowOnceLog : ICombatLog
    {
        private readonly Func<string, bool> _trigger;
        private bool _thrown;
        public List<string> Lines { get; } = new();
        public OverflowOnceLog(Func<string, bool> trigger) => _trigger = trigger;

        public void Append(string line)
        {
            if (!_thrown && _trigger(line))
            {
                _thrown = true;
                throw new OverflowException("débordement simulé");
            }
            Lines.Add(line);
        }
    }

    [Fact]
    public void Debordement_tardif_non_anticipe_arrete_proprement_le_combat()
    {
        // Aucun débordement connu ne passe plus le précontrôle ; on en simule un en pleine résolution pour vérifier
        // que le filet de sécurité du moteur reste en place : arrêt contrôlé, raison « overflow », journal complet.
        var log = new OverflowOnceLog(line => line.Contains("\"event\":\"damage\"", StringComparison.Ordinal));
        var setup = Make(new CombatantSetup[] { Character(), Dummy() }, new[] { Cmd(500, Frappe) });
        Assert.Empty(setup.Validate());
        CombatResult result = CombatEngine.Run(setup, log);
        Assert.Equal(CombatEndReason.Aborted, result.EndReason);
        Assert.Equal("overflow", result.AbortReason);
        Assert.Equal(10, result.EndTick);
        Assert.Contains(log.Lines, l => l.Contains("\"event\":\"simulationAborted\"", StringComparison.Ordinal) && l.Contains("\"reason\":\"overflow\"", StringComparison.Ordinal));
        Assert.Contains("\"event\":\"combatEnded\"", log.Lines[^1]);
    }
}
