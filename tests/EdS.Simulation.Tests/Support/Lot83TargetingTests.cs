using System.Text.Json;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Support.Lot83Kit;

namespace EdS.Simulation.Tests.Support;

/// <summary>
/// Sous-lot 8.3 (X5) : compétences de soutien (soi, un allié, tous les alliés), sans impact ni Résonance ; applications à cible propre
/// (soi, tous les alliés, tous les ennemis), appliquées une fois après les impacts ; refus d'un allié mort ou hostile.
/// </summary>
[Trait("Category", "Support")]
public class Lot83TargetingTests
{
    private static string Line(JsonElement e) => e.GetRawText();

    [Fact]
    public void Soutien_sur_soi_sans_impact_ni_Resonance()
    {
        var events = Simulate(Fight(new[] { Use(0, Garde) }, new CombatantSetup[] { Bastion(Garde, Lien, Barriere, Provocation), Brute("bastion") }, durationMs: 100), out _);
        JsonElement cast = events.OfKind("skillCast").Single();
        Assert.Equal(JsonValueKind.Null, cast.GetProperty("target").ValueKind);
        JsonElement applied = events.OfKind("stateApplied").Single();
        Assert.Equal(("bastion", "fortifie", "bastion", cast.Long("eid")), (applied.Str("combatant"), applied.Str("state"), applied.Str("source"), applied.Long("cause")));
        Assert.Empty(events.OfKind("damage"));
        Assert.DoesNotContain(events.OfKind("resourceGained"), g => g.Str("source") == "directHit");
    }

    [Fact]
    public void Bouclier_sur_un_allie_et_sur_soi_aux_PV_max_du_lanceur()
    {
        // 2 100 PV × 0,1 = 210, posé sur l'allié puis, après le temps de recharge global (1 s), sur le lanceur (un allié désigné peut être
        // le lanceur lui-même).
        var events = Simulate(Fight(new[] { Use(0, Barriere, target: "allie"), Use(1000, Barriere, target: "bastion") },
            new CombatantSetup[] { Bastion(), Ally(), Brute() }, durationMs: 1100), out CombatResult result);
        Assert.Equal(new[] { ("allie", 210L), ("bastion", 210L) },
            events.OfKind("stateApplied").Select(a => (a.Str("combatant")!, a.Long("absorb"))));
        // Le coup de 100 sur l'allié est absorbé par son égide.
        JsonElement hit = events.OfKind("damage").Single();
        Assert.Equal(("allie", 100L, 240L), (hit.Str("target"), hit.Long("absorbed"), hit.Long("health")));
        Assert.Equal(100, result["allie"].DamageAbsorbed);
    }

    [Fact]
    public void Allie_absent_hostile_ou_mort_refuse()
    {
        // L'assaillant (300 bruts) tue l'allié (240 PV) à 1 s ; le lancer à 2 s sur lui est refusé (targetDead). Un refus ne déclenche pas
        // le temps de recharge global.
        var events = Simulate(Fight(new[] { Use(0, Barriere), Use(100, Barriere, target: "assaillant"), Use(2000, Barriere, target: "allie") },
            new CombatantSetup[] { Bastion(), Ally(), Brute(raw: 300) }, durationMs: 2100), out _);
        Assert.Equal(new[] { "targetRequired", "targetNotAlly", "targetDead" }, events.OfKind("commandRejected").Select(r => r.Str("reason")));
        Assert.Empty(events.OfKind("stateApplied"));
        // Un ciblage sans cible désignée refuse une cible.
        var self = Simulate(Fight(new[] { Use(0, Garde, target: "allie") }, new CombatantSetup[] { Bastion(Garde, Lien, Barriere, Provocation), Ally(), Brute() }, durationMs: 100), out _);
        Assert.Equal("targetNotAllowed", self.OfKind("commandRejected").Single().Str("reason"));
    }

    [Fact]
    public void Tous_les_allies_vivants_dans_l_ordre_des_identifiants()
    {
        var events = Simulate(Fight(new[] { Use(0, Ralliement) },
            new CombatantSetup[] { Bastion(Ralliement, Lien, Barriere, Garde), Ally(), Brute() }, durationMs: 1100), out _);
        JsonElement[] applied = events.OfKind("stateApplied").ToArray();
        Assert.Equal(new[] { "allie", "bastion" }, applied.Select(a => a.Str("combatant")));
        Assert.NotEqual(applied[0].Long("instance"), applied[1].Long("instance"));
        // « fortifié » (−30 %) réduit le coup sur l'allié à 70.
        Assert.Equal(70L, events.OfKind("damage").Single().Long("amount"));
    }

    [Fact]
    public void Application_a_cible_propre_une_fois_apres_l_impact()
    {
        // Coup (1.0) sur l'assaillant : l'impact, la Résonance d'impact direct, puis « fortifié » sur le lanceur, causé par le lancer.
        var events = Simulate(Fight(new[] { new CombatCommand(0, "bastion", Coup, "assaillant") },
            new CombatantSetup[] { Bastion(Coup, Lien, Barriere, Garde), Brute("bastion") }, durationMs: 100), out _);
        long cast = events.OfKind("skillCast").Single().Long("eid");
        string[] kinds = events.Where(e => e.TryGetProperty("cause", out JsonElement c) && c.ValueKind == JsonValueKind.Number && c.GetInt64() == cast)
            .Select(e => e.Str("event")!).ToArray();
        Assert.Equal(new[] { "damage", "resourceGained", "stateApplied" }, kinds);
        JsonElement applied = events.OfKind("stateApplied").Single();
        Assert.Equal(("bastion", "fortifie"), (applied.Str("combatant"), applied.Str("state")));
    }

    [Fact]
    public void Ultime_de_groupe_renforce_les_allies_et_provoque_les_ennemis()
    {
        var events = Simulate(Fight(new[] { Use(0, Absolu) },
            new CombatantSetup[] { Bastion(Absolu, Lien, Barriere, Garde), Ally(), Brute(), Dummy(armor: 0) }, durationMs: 1100), out _);
        Assert.Equal(new[] { ("allie", "fortifie"), ("bastion", "fortifie"), ("assaillant", "provoc") },
            events.OfKind("stateApplied").Select(a => (a.Str("combatant")!, a.Str("state")!)));
        // Le mannequin n'est pas un assaillant : provocation refusée.
        JsonElement blocked = events.OfKind("stateBlocked").Single();
        Assert.Equal(("mannequin", "provoc", "notApplicable"), (blocked.Str("combatant"), blocked.Str("state"), blocked.Str("reason")));
        // L'assaillant provoqué frappe le Bastion, renforcé : 70.
        JsonElement hit = events.OfKind("damage").Single();
        Assert.Equal(("bastion", 70L), (hit.Str("target"), hit.Long("amount")));
    }

    [Fact]
    public void Journal_rejoue_a_l_identique()
    {
        CombatSetup setup = Fight(new[] { Use(0, Absolu), Use(1000, Lien, target: "allie"), Use(1500, Barriere, target: "allie") },
            new CombatantSetup[] { Bastion(Absolu, Lien, Barriere, Garde), Ally(), Brute(raw: 150) }, durationMs: 6000);
        var (_, log) = Run(setup);
        var (_, again) = Run(setup);
        Assert.True(CombatReplay.Compare(log.Lines, again.Lines).Identical);
        Assert.Contains(Events(log).OfKind("damageTransferred"), t => t.Long("amount") > 0);
    }
}
