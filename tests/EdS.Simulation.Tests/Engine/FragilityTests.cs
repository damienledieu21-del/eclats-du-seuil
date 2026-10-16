using System.Text.Json;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Engine;

/// <summary>Fragilité (A19) : +10 % de dégâts directs reçus pendant 6 s, renouvelée sans cumul ; effets simultanés.</summary>
[Trait("Category", "Engine")]
public class FragilityTests
{
    private static List<JsonElement> Fight(IEnumerable<CombatCommand> commands, long durationMs, params CombatantSetup[] assailants)
    {
        var fighters = new List<CombatantSetup> { Tank() };
        fighters.AddRange(assailants);
        return Events(Run(Setup(commands, fighters, durationMs: durationMs, rules: NoUpkeep)).Log);
    }

    private static IEnumerable<(long Tick, long Amount, bool Fragile)> Hits(List<JsonElement> events) =>
        events.OfKind("damage").Select(e => (e.Tick(), e.Int("amount"), e.GetProperty("fragile").GetBoolean()));

    [Fact]
    public void Fragilite_appliquee_apres_l_impact_puis_10_pourcent_jusqu_a_l_echeance_exclue()
    {
        // Application au tick 20 (après les dégâts de cet impact) ; active sur [20, 140[.
        // Impacts aux ticks 20 et 139 (5950 ms plus tard, dernier tick fragile) : 100 puis 110.
        var fele = Assailant(intervalMs: 5950, attacks: Blow(fragility: true, id: "fele"));
        var events = Fight(Array.Empty<CombatCommand>(), 7000, fele);
        Assert.Equal(new[] { (20L, 100L, false), (139L, 110L, true) }, Hits(events));
        var applied = events.OfKind("stateApplied").First();
        Assert.Equal(("fragility", 140L, false), (applied.Str("state")!, applied.Int("untilTick"), applied.GetProperty("renewed").GetBoolean()));
        Assert.True(events.OfKind("damage").First().Int("seq") < applied.Int("seq"));
    }

    [Fact]
    public void Echeance_exacte_la_fragilite_expire_avant_la_resolution()
    {
        // Application au tick 20 ; second assaillant au tick 140 : l'expiration (phase 1) précède l'impact (phase 3).
        var fele = Assailant(intervalMs: 9000, attacks: Blow(fragility: true, id: "fele"));
        var events = Fight(Array.Empty<CombatCommand>(), 8000, fele, Assailant(firstAttackMs: 7000, intervalMs: 9000, id: "apres"));
        var expired = Assert.Single(events.OfKind("stateExpired"));
        Assert.Equal((140L, "fragility"), (expired.Tick(), expired.Str("state")!));
        Assert.Equal((140L, 100L, false), Hits(events).Last());
    }

    [Fact]
    public void Reapplication_renouvelle_la_duree_sans_cumul()
    {
        // Applications aux ticks 20 et 60 (échéance repoussée de 140 à 180) ; impacts suivants à +10 %, jamais +21 %.
        var fele = Assailant(intervalMs: 2000, attacks: new[] { Blow(fragility: true, id: "fele"), Blow(fragility: true, id: "fele_bis"), Blow(id: "simple") });
        var events = Fight(Array.Empty<CombatCommand>(), 6000, fele);
        var applied = events.OfKind("stateApplied").ToList();
        Assert.Equal(new[] { (140L, false), (180L, true) }, applied.Select(a => (a.Int("untilTick"), a.GetProperty("renewed").GetBoolean())));
        Assert.Equal(new[] { (20L, 100L, false), (60L, 110L, true), (100L, 110L, true) }, Hits(events));
        Assert.Empty(events.OfKind("stateExpired"));
    }

    [Fact]
    public void Fragilite_et_garde_se_composent()
    {
        // 100 × 1,1 × 0,4 = 44 ; avec la parade : 100 × 1,1 × 0,1 = 11.
        var fele = Assailant(intervalMs: 2000, attacks: Blow(fragility: true, id: "fele"));
        var events = Fight(new[] { GuardStart(1500), Parry(5000) }, 6000, fele);
        Assert.Equal(new[] { (20L, 100L, false), (60L, 44L, true), (100L, 11L, true) }, Hits(events));
    }

    [Fact]
    public void Impacts_simultanes_dans_l_ordre_d_initiative_puis_d_identifiant()
    {
        // Même tick : « a_fele » (identifiant plus petit) frappe d'abord et fragilise ; « b_coup » profite déjà de la Fragilité.
        // Avec une initiative supérieure, « b_coup » passe avant et frappe sans bonus.
        var events = Fight(Array.Empty<CombatCommand>(), 2000,
            Assailant(intervalMs: 9000, id: "a_fele", attacks: Blow(fragility: true, id: "fele")), Assailant(intervalMs: 9000, id: "b_coup"));
        Assert.Equal(new[] { ("a_fele", 100L), ("b_coup", 110L) }, events.OfKind("damage").Select(e => (e.Str("source")!, e.Int("amount"))));

        var fighters = new CombatantSetup[]
        {
            Tank(), new AssailantSetup("a_fele", "cibles", default, default, "bastion", 1000, 9000, new[] { Blow(fragility: true, id: "fele") }),
            new AssailantSetup("b_coup", "cibles", default, default, "bastion", 1000, 9000, new[] { Blow() }, initiative: 5),
        };
        var reordered = Events(Run(Setup(Array.Empty<CombatCommand>(), fighters, durationMs: 2000)).Log);
        Assert.Equal(new[] { ("b_coup", 100L), ("a_fele", 100L) }, reordered.OfKind("damage").Select(e => (e.Str("source")!, e.Int("amount"))));
    }
}
