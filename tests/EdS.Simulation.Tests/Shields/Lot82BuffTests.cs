using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Combat;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Shields.Lot82Kit;

namespace EdS.Simulation.Tests.Shields;

/// <summary>
/// Sous-lot 8.2 (X3) : renforcements temporaires. Leurs modificateurs (dégâts subis, dégâts infligés, entretien de la garde) valent tant
/// que l'instance existe, s'ajoutent aux modificateurs permanents et conditionnels du porteur, sous les mêmes plafonds (réduction totale
/// des règles) ; l'entretien de la garde suit le taux du moment, tick par tick, sans dérive.
/// </summary>
[Trait("Category", "Shield")]
public class Lot82BuffTests
{
    private static (CombatResult Result, List<JsonElement> Events) Simulate(CombatSetup setup)
    {
        var (result, log) = Run(setup);
        return (result, Events(log));
    }

    private static long[] HitsOnBastion(List<JsonElement> events) =>
        events.OfKind("damage").Where(e => e.Str("target") == "bastion").Select(e => e.Long("amount")).ToArray();

    [Fact]
    public void Reduction_temporaire_des_degats_subis_puis_expiration()
    {
        // Fortifié (−30 %, 8 s) au lancer : coups de 100 à 1, 3, 5 et 7 s réduits à 70 ; il expire au tick 160 : le coup de 9 s fait 100.
        var (_, events) = Simulate(Fight(new[] { Cast() }, Protected(new[] { OnCast("fortifie") }), durationMs: 9100));
        Assert.Equal(new[] { 70L, 70L, 70L, 70L, 100L }, HitsOnBastion(events));
        JsonElement applied = events.OfKind("stateApplied").Single(e => e.Str("state") == "fortifie");
        Assert.Equal(("buff", 160L), (applied.Str("category"), applied.Long("untilTick")));
        Assert.Equal(160L, events.OfKind("stateExpired").Single(e => e.Str("state") == "fortifie").Tick());
    }

    [Fact]
    public void Renforcement_et_modificateurs_permanents_sous_le_plafond_total_des_regles()
    {
        // Passif −0,8 + Fortifié −0,3 : somme −1,1 ramenée à −1, réduction totale plafonnée à 85 % (règles) : 15. Plafond relevé à 0,9 : 10.
        ModifierDefinition[] heavy = { Mod("damageTaken", "-0.8") };
        var (_, capped) = Simulate(Fight(new[] { Cast() }, Protected(new[] { OnCast("fortifie") }, passiveModifiers: heavy), durationMs: 1100));
        Assert.Equal(new[] { 15L }, HitsOnBastion(capped));
        CombatRules raised = new()
        {
            MaxTotalDamageReduction = Fixed.Parse("0.9"),
            Statuses = Rules.Statuses,
        };
        var (_, higher) = Simulate(Fight(new[] { Cast() }, Protected(new[] { OnCast("fortifie") }, passiveModifiers: heavy), durationMs: 1100, rules: raised));
        Assert.Equal(new[] { 10L }, HitsOnBastion(higher));
        // Sans le renforcement, le passif seul : 100 × 0,2 = 20.
        var (_, alone) = Simulate(Fight(new[] { Cast() }, Protected(Array.Empty<TriggerDefinition>(), passiveModifiers: heavy), durationMs: 1100));
        Assert.Equal(new[] { 20L }, HitsOnBastion(alone));
    }

    [Fact]
    public void Renforcement_reduit_aussi_les_DOT()
    {
        // Saignement de 50 par tick : 35 sous Fortifié.
        AssailantSetup bleeder = Assailant(intervalMs: 60_000, attacks: Strike("entaille", 100, DamageType.Physique, false, Apply("bleed", "0.5")));
        var (_, events) = Simulate(Fight(new[] { Cast() }, Protected(new[] { OnCast("fortifie") }), bleeder, durationMs: 7500));
        Assert.Equal(new[] { 35L, 35L, 35L }, events.OfKind("dotDamage").Select(d => d.Long("amount")));
    }

    [Fact]
    public void Degats_infliges_temporaires_categorie_alteration()
    {
        // Furie (+50 %, catégorie altération) posée par le premier lancer de Frappe : la Frappe est résolue après la pose (commande puis
        // résolution), donc renforcée. Montant attendu calculé par la formule du domaine, critique compris.
        CombatSetup setup = Setup(new[] { Cmd(0, Frappe) }, new CombatantSetup[] { Protected(new[] { OnCast("furie") }), Dummy(armor: 0) },
            durationMs: 100, rules: Rules);
        var (_, events) = Simulate(setup);
        JsonElement hit = events.OfKind("damage").Single();
        bool critical = hit.GetProperty("critical").GetBoolean();
        DerivedStats bastion = CombatantStats.ForCharacter(StatRules.Provisional, ((CharacterSetup)setup.Combatants[0]).Build, ModifierSet.Empty);
        DerivedStats dummy = CombatantStats.ForDefenses(StatRules.Provisional, Fixed.Zero, Fixed.Zero);
        long Expected(DamageBonuses bonuses) => DamageCalculator.Compute(new DamageInput(bastion, dummy, PowerType.Physique, DamageType.Physique,
            Fixed.Parse("1.2"), bonuses, critical, Fixed.Zero), DamageRules.Provisional).FinalDamage;
        Assert.Equal(Expected(DamageBonuses.None.Add(BonusCategory.Alteration, Fixed.Parse("0.5"))), hit.Long("amount"));
        Assert.True(hit.Long("amount") > Expected(DamageBonuses.None));
        Assert.False(hit.GetProperty("fragile").GetBoolean());   // « fragile » reste la Fragilité de la cible
    }

    [Fact]
    public void Entretien_de_garde_temporaire_suit_le_taux_du_moment()
    {
        // Garde du tick 1 au tick 59. Sans renforcement : ⌊ 2 × 59 × 50 / 1000 ⌋ = 5 points d'entretien. Avec « sans entretien » (−2/s),
        // posé au tick 0 et expiré au tick 20 (phase des expirations, avant l'entretien) : les ticks 1 à 19 ne coûtent rien, puis 40 ticks
        // (20 à 59) à 0,1 point chacun : 4 points, le premier au tick 29.
        long Paid(IEnumerable<TriggerDefinition> triggers, out List<JsonElement> events)
        {
            var (_, e) = Simulate(Setup(new[] { Cmd(0, Gratuite), GuardStart(50) }, new CombatantSetup[] { Protected(triggers), Dummy() },
                durationMs: 3000, rules: Rules));
            events = e;
            return e.OfKind("guardUpkeep").Sum(u => u.Long("amount"));
        }
        Assert.Equal(5, Paid(Array.Empty<TriggerDefinition>(), out _));
        Assert.Equal(4, Paid(new[] { OnCast("sansEntretien") }, out List<JsonElement> buffed));
        Assert.Equal(29L, buffed.OfKind("guardUpkeep").First().Tick());
    }

    [Fact]
    public void Entretien_cumule_identique_au_calcul_des_lots_precedents_a_taux_constant()
    {
        // Sans renforcement, l'entretien cumulé tick par tick vaut ⌊ coût/s × ticks × durée du tick / 1000 ⌋ (taux décimal compris) : comparé
        // au calcul direct pour chaque tick d'une longue garde.
        var definition = Class(modifiers: new Dictionary<string, ModifierDefinition[]> { [Passif] = new[] { Mod("guard.upkeepPerSecond", "0.3337") } });
        var (_, events) = Simulate(Setup(new[] { GuardStart(0) }, new CombatantSetup[] { Character(attributes: Attrs(vitalite: 200, endurance: 200), definition: definition, passives: new[] { Passif }), Dummy() },
            durationMs: 60_000, rules: Rules));
        long rateRaw = Fixed.Parse("2.3337").Raw;   // 2 de base + 0,3337
        long paid = 0;
        foreach (JsonElement u in events.OfKind("guardUpkeep"))
        {
            paid += u.Long("amount");
            long ticks = u.Tick() + 1;   // garde du tick 0 au tick courant compris
            Assert.Equal(rateRaw * ticks * 50 / (1000 * Fixed.Scale), paid);
        }
        Assert.True(paid > 100);
    }

    [Fact]
    public void Renforcement_renouvele_une_seule_instance()
    {
        // Refresh : deux lancers (0 et 1 s) renouvellent la même instance, l'échéance repart ; un seul effet (pas de cumul).
        var (_, events) = Simulate(Fight(new[] { Cast(), Cast(1000) }, Protected(new[] { OnCast("fortifie") }), durationMs: 1100));
        JsonElement[] applied = events.OfKind("stateApplied").Where(e => e.Str("state") == "fortifie").ToArray();
        Assert.Equal(new[] { (false, 160L), (true, 180L) }, applied.Select(a => (a.GetProperty("renewed").GetBoolean(), a.Long("untilTick"))));
        Assert.Equal(applied[0].Long("instance"), applied[1].Long("instance"));
        Assert.Equal(new[] { 70L }, HitsOnBastion(events));
    }
}
