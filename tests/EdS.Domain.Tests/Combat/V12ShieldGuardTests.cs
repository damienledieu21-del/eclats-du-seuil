using EdS.Domain.Combat;
using EdS.Domain.Numerics;

namespace EdS.Domain.Tests.Combat;

/// <summary>
/// Sous-lot V1.2 : la garde propre d'un bouclier (<see cref="ShieldGuard"/>) et ses bornes dans les règles (<c>combat.shield</c>), aux
/// frontières (N − 1, N, N + 1) ; les règles effectives d'un porteur (<see cref="CombatantRules"/>) : le bouclier remplace les trois valeurs de
/// base, les modificateurs s'y ajoutent sous les mêmes plafonds, et sans bouclier rien ne change.
/// </summary>
[Trait("Category", "Engine")]
[Trait("Category", "Limits")]
public class V12ShieldGuardTests
{
    private static readonly CombatRules Rules = CombatRules.Provisional;
    private static Fixed F(string value) => Fixed.Parse(value);
    private static ShieldGuard Guard(string reduction = "0.6", int cost = 10, long window = 150) => new(F(reduction), cost, window);

    private static IReadOnlyList<string> Paths(ShieldGuard g) => g.Check(Rules, "shield").SelectMany(v => v.Fields).ToList();

    [Fact]
    public void Bornes_livrees_coherentes()
    {
        Assert.Empty(Rules.Validate());
        Assert.Equal((F("0.45"), F("0.7"), 4, 20, 50L), (Rules.ShieldMinDamageReduction, Rules.ShieldMaxDamageReduction,
            Rules.ShieldMinCostPerBlock, Rules.ShieldMaxCostPerBlock, Rules.ShieldParryWindowShiftMs));
        Assert.Empty(Paths(Guard()));
    }

    [Theory]
    [InlineData("0.4499", false)]
    [InlineData("0.45", true)]
    [InlineData("0.4501", true)]
    [InlineData("0.6999", true)]
    [InlineData("0.7", true)]
    [InlineData("0.7001", false)]
    [InlineData("-1", false)]
    [InlineData("1", false)]
    public void Part_bloquee_aux_frontieres(string reduction, bool valid) =>
        Assert.Equal(valid, !Paths(Guard(reduction: reduction)).Contains("shield.damageReduction"));

    [Theory]
    [InlineData(3, false)]
    [InlineData(4, true)]
    [InlineData(5, true)]
    [InlineData(19, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    [InlineData(int.MinValue, false)]
    [InlineData(int.MaxValue, false)]
    public void Cout_d_un_blocage_aux_frontieres(int cost, bool valid) =>
        Assert.Equal(valid, !Paths(Guard(cost: cost)).Contains("shield.costPerBlock"));

    [Theory]
    [InlineData(50, false)]
    [InlineData(99, false)]    // pas un multiple du tick
    [InlineData(100, true)]
    [InlineData(150, true)]
    [InlineData(200, true)]
    [InlineData(201, false)]
    [InlineData(250, false)]
    [InlineData(0, false)]
    [InlineData(-50, false)]
    [InlineData(long.MaxValue, false)]
    public void Fenetre_de_parade_a_un_tick_pres(long window, bool valid) =>
        Assert.Equal(valid, !Paths(Guard(window: window)).Contains("shield.parryWindowMs"));

    [Fact]
    public void Regles_incoherentes_refusees()
    {
        static IEnumerable<string> Fields(CombatRules r) => r.Validate().SelectMany(v => v.Fields);
        Assert.Contains(nameof(CombatRules.ShieldMinDamageReduction), Fields(new CombatRules { ShieldMinDamageReduction = F("0.71") }));
        // Au-dessus du plafond de la garde (règles d'essai) : admis, la garde effective reste écrêtée au plafond.
        Assert.DoesNotContain(nameof(CombatRules.ShieldMaxDamageReduction), Fields(new CombatRules { ShieldMaxDamageReduction = F("0.9") }));
        Assert.Equal(Rules.MaxGuardDamageReduction, CombatantRules.For(new CombatRules { ShieldMaxDamageReduction = F("0.9") }, ModifierSet.Empty, Guard("0.9")).GuardDamageReduction);
        Assert.Contains(nameof(CombatRules.ShieldMaxDamageReduction), Fields(new CombatRules { ShieldMaxDamageReduction = F("1.1") }));
        Assert.Contains(nameof(CombatRules.ShieldMinCostPerBlock), Fields(new CombatRules { ShieldMinCostPerBlock = 21 }));
        Assert.Contains(nameof(CombatRules.ShieldMinCostPerBlock), Fields(new CombatRules { ShieldMinCostPerBlock = -1 }));
        Assert.Contains(nameof(CombatRules.ShieldMaxCostPerBlock), Fields(new CombatRules { ShieldMaxCostPerBlock = CombatRules.MaxShieldCostPerBlock + 1 }));
        Assert.Contains(nameof(CombatRules.ShieldParryWindowShiftMs), Fields(new CombatRules { ShieldParryWindowShiftMs = -1 }));
        Assert.Contains(nameof(CombatRules.ShieldParryWindowShiftMs), Fields(new CombatRules { ShieldParryWindowShiftMs = CombatantRules.MaxParryWindowMs + 1 }));
        Assert.DoesNotContain(nameof(CombatRules.ShieldParryWindowShiftMs), Fields(new CombatRules { ShieldParryWindowShiftMs = CombatantRules.MaxParryWindowMs }));
        Assert.DoesNotContain(nameof(CombatRules.ShieldParryWindowShiftMs), Fields(new CombatRules { ShieldParryWindowShiftMs = 0 }));
        // Un écart qui ne tombe pas sur le tick reste admis dans les règles : la fenêtre du bouclier, elle, doit y tomber.
        var odd = new CombatRules { ShieldParryWindowShiftMs = 75 };
        Assert.DoesNotContain(nameof(CombatRules.ShieldParryWindowShiftMs), Fields(odd));
        Assert.Contains("shield.parryWindowMs", new ShieldGuard(F("0.6"), 10, 225).Check(odd, "shield").SelectMany(v => v.Fields));
        Assert.Empty(new ShieldGuard(F("0.6"), 10, 200).Check(odd, "shield"));
        // Avec un écart de 150 ms, la fenêtre la plus étroite reste d'au moins un tick.
        Assert.Contains("shield.parryWindowMs", new ShieldGuard(F("0.6"), 10, 0).Check(new CombatRules { ShieldParryWindowShiftMs = 150 }, "shield").SelectMany(v => v.Fields));
        Assert.Empty(new ShieldGuard(F("0.6"), 10, 50).Check(new CombatRules { ShieldParryWindowShiftMs = 150 }, "shield"));
    }

    [Fact]
    public void Sans_bouclier_les_regles_de_base()
    {
        CombatantRules none = CombatantRules.For(Rules, ModifierSet.Empty), explicitNone = CombatantRules.For(Rules, ModifierSet.Empty, null);
        Assert.Equal((Rules.GuardDamageReduction, (long)Rules.GuardCostPerBlock, Rules.ParryWindowMs), (none.GuardDamageReduction, none.GuardCostPerBlock, none.ParryWindowMs));
        Assert.Equal((none.GuardDamageReduction, none.GuardCostPerBlock, none.ParryWindowMs), (explicitNone.GuardDamageReduction, explicitNone.GuardCostPerBlock, explicitNone.ParryWindowMs));
        Assert.Null(none.Shield);
        // L'écu de départ a exactement les valeurs des règles : le porter ne change rien au combat.
        CombatantRules ecu = CombatantRules.For(Rules, ModifierSet.Empty, Guard());
        Assert.Equal((none.GuardDamageReduction, none.GuardCostPerBlock, none.ParryWindowMs), (ecu.GuardDamageReduction, ecu.GuardCostPerBlock, ecu.ParryWindowMs));
    }

    [Theory]
    [InlineData("0.7", 14, 100)]
    [InlineData("0.5", 6, 200)]
    [InlineData("0.45", 4, 100)]
    [InlineData("0.7", 20, 200)]
    public void Le_bouclier_remplace_les_valeurs_de_base(string reduction, int cost, long window)
    {
        CombatantRules r = CombatantRules.For(Rules, ModifierSet.Empty, Guard(reduction, cost, window));
        Assert.Equal((F(reduction), (long)cost, window), (r.GuardDamageReduction, r.GuardCostPerBlock, r.ParryWindowMs));
        // Ce qui n'appartient pas au bouclier ne bouge pas.
        CombatantRules none = CombatantRules.For(Rules, ModifierSet.Empty);
        Assert.Equal((none.GuardUpkeepPerSecond, none.ParryFailureRecoveryMs, none.RampartChargesMax), (r.GuardUpkeepPerSecond, r.ParryFailureRecoveryMs, r.RampartChargesMax));
    }

    [Fact]
    public void Les_modificateurs_s_ajoutent_au_bouclier_sous_les_plafonds()
    {
        ModifierSet set = Lot81Kit.Set(
            Lot81Kit.Mod("guard.damageReduction", "0.1"),
            Lot81Kit.Mod("guard.costPerBlock", "-3"),
            Lot81Kit.Mod("parry.windowMs", "50"));
        CombatantRules heavy = CombatantRules.For(Rules, set, Guard("0.7", 14, 100));
        Assert.Equal(Rules.MaxGuardDamageReduction, heavy.GuardDamageReduction);   // 0,8 écrêté à 0,75
        Assert.Equal((11L, 150L), (heavy.GuardCostPerBlock, heavy.ParryWindowMs));
        CombatantRules light = CombatantRules.For(Rules, set, Guard("0.45", 4, 200));
        Assert.Equal((F("0.55"), 1L, 250L), (light.GuardDamageReduction, light.GuardCostPerBlock, light.ParryWindowMs));
        ModifierSet cheaper = Lot81Kit.Set(Lot81Kit.Mod("guard.costPerBlock", "-10"));
        Assert.Equal(0L, CombatantRules.For(Rules, cheaper, Guard("0.45", 4, 200)).GuardCostPerBlock);   // jamais négatif
        // La réduction à 0 d'endurance (Rempart éternel) n'est jamais meilleure que la garde du bouclier.
        ModifierSet eternal = Lot81Kit.Set(Lot81Kit.Mod("guard.noUpkeepBreak", "1"),
            Lot81Kit.Mod("guard.reductionAtZeroEndurance", "0.75"));
        Assert.Equal(F("0.45"), CombatantRules.For(Rules, eternal, Guard("0.45", 4, 200)).GuardReductionAtZeroEndurance);
    }

    [Theory]
    [InlineData("-1", -5, -100, "0", 0, 50)]
    [InlineData("2", int.MaxValue, long.MaxValue, "0.75", int.MaxValue, 500)]
    [InlineData("0.75", 0, 49, "0.75", 0, 50)]
    public void Bouclier_adverse_ramene_dans_les_plafonds_du_moteur(string reduction, int cost, long window, string reductionAfter, long costAfter, long windowAfter)
    {
        // Un bouclier hors bornes est refusé avant le combat (Check) ; s'il arrivait quand même au moteur, il ne sortirait pas des plafonds.
        CombatantRules r = CombatantRules.For(Rules, ModifierSet.Empty, new ShieldGuard(F(reduction), cost, window));
        Assert.Equal((F(reductionAfter), costAfter, windowAfter), (r.GuardDamageReduction, r.GuardCostPerBlock, r.ParryWindowMs));
    }

    [Fact]
    public void Garde_hors_bornes_refusee_par_la_validation_du_combat()
    {
        Assert.Contains("shield.parryWindowMs", Paths(Guard(window: 300)));
        // Égalité et code de hachage : par valeur.
        Assert.Equal(Guard("0.7", 14, 100), Guard("0.7", 14, 100));
        Assert.Equal(Guard("0.7", 14, 100).GetHashCode(), Guard("0.7", 14, 100).GetHashCode());
        Assert.NotEqual(Guard("0.7", 14, 100), Guard("0.7", 14, 150));
        Assert.Throws<ArgumentNullException>(() => Guard().Check(null!, "shield"));
    }
}
