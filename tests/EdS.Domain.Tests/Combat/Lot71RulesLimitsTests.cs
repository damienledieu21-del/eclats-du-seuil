using EdS.Domain.Combat;

namespace EdS.Domain.Tests.Combat;

/// <summary>
/// Lot 7.1 : les cinq limites d'admission et de budget des règles, aux frontières de leurs plafonds techniques
/// (N − 1, N, N + 1), et la cohérence « événements par tick ≤ travail par tick ».
/// </summary>
[Trait("Category", "Engine")]
[Trait("Category", "Limits")]
public class Lot71RulesLimitsTests
{
    private static CombatRules With(string field, int value) => field switch
    {
        nameof(CombatRules.MaxCombatants) => new CombatRules { MaxCombatants = value },
        nameof(CombatRules.MaxCommands) => new CombatRules { MaxCommands = value },
        nameof(CombatRules.MaxCommandsPerActorPerTick) => new CombatRules { MaxCommandsPerActorPerTick = value },
        // Le budget d'événements (1000 par défaut) doit rester sous le budget de travail : on l'abaisse à 1 pour isoler la borne.
        nameof(CombatRules.MaxWorkPerTick) => new CombatRules { MaxWorkPerTick = value, MaxEventsPerTick = 1 },
        nameof(CombatRules.MaxPendingEvents) => new CombatRules { MaxPendingEvents = value },
        _ => throw new ArgumentException(field),
    };

    [Theory]
    [InlineData(nameof(CombatRules.MaxCombatants), CombatRules.AbsoluteMinCombatants, CombatRules.AbsoluteMaxCombatants)]
    [InlineData(nameof(CombatRules.MaxCommands), 0, CombatRules.AbsoluteMaxCommands)]
    [InlineData(nameof(CombatRules.MaxCommandsPerActorPerTick), 1, CombatRules.AbsoluteMaxCommandsPerActorPerTick)]
    [InlineData(nameof(CombatRules.MaxPendingEvents), 1, CombatRules.AbsoluteMaxPendingEvents)]
    [InlineData(nameof(CombatRules.MaxWorkPerTick), 1, CombatRules.AbsoluteMaxWorkPerTick)]
    public void Limite_aux_frontieres_de_son_plafond(string field, int min, int max)
    {
        foreach (int valid in new[] { min, min + 1, max - 1, max })
            Assert.DoesNotContain(With(field, valid).Validate(), v => v.Fields.Contains(field));
        foreach (int invalid in new[] { min - 1, max + 1, int.MinValue, int.MaxValue })
            Assert.Contains(With(field, invalid).Validate(), v => v.Fields.Contains(field));
    }

    [Theory]
    [InlineData(999, true)]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void Evenements_par_tick_au_plus_le_budget_de_travail(int events, bool valid)
    {
        var rules = new CombatRules { MaxWorkPerTick = 1000, MaxEventsPerTick = events };
        Assert.Equal(valid, !rules.Validate().Any(v => v.Fields.Contains(nameof(CombatRules.MaxEventsPerTick))));
    }

    [Fact]
    public void Aucune_contrainte_statique_entre_file_et_commandes()
    {
        // Correction du plan 7.1 : la capacité de la file est vérifiée sur les événements initiaux réels du scénario
        // (admission), pas par une règle « file ≥ commandes + combattants » imposée aux règles.
        Assert.Empty(new CombatRules { MaxPendingEvents = 1, MaxCommands = CombatRules.AbsoluteMaxCommands, MaxCombatants = 256 }.Validate());
    }

    [Fact]
    public void Valeurs_initiales_provisoires()
    {
        var rules = CombatRules.Provisional;
        Assert.Equal((64, 20_000, 4, 100_000, 10_000, 1000),
            (rules.MaxCombatants, rules.MaxCommands, rules.MaxCommandsPerActorPerTick, rules.MaxPendingEvents, rules.MaxWorkPerTick, rules.MaxEventsPerTick));
    }
}
