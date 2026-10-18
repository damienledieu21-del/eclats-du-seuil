using System.Text.Json;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Support.Lot83Kit;

namespace EdS.Simulation.Tests.Support;

/// <summary>
/// Sous-lot 8.3 (X7) : provocation. Les attaques à cible unique d'un assaillant provoqué visent la source de sa provocation la plus
/// récente ; une provocation sur autre chose qu'un assaillant est refusée (<c>notApplicable</c>) ; à la mort du provocateur, elle est
/// retirée (<c>sourceDied</c>) et l'assaillant revient à sa cible.
/// </summary>
[Trait("Category", "Support")]
public class Lot83TauntTests
{
    private static string?[] AttackTargets(List<JsonElement> events) => events.OfKind("attackStarted").Select(a => a.Str("target")).ToArray();

    [Fact]
    public void Provocation_detourne_les_attaques_le_temps_de_sa_duree()
    {
        // Provocation de 3 s au tick 0 (échéance tick 60, phase 1, avant les commandes) : l'attaque de 1 s vise le Bastion, celle de 3 s
        // revient à l'allié, cible du scénario.
        var events = Simulate(Fight(new[] { Use(0, Provocation, target: "assaillant") }, new CombatantSetup[] { Bastion(), Ally(), Brute() }), out _);
        Assert.Equal(new[] { "bastion", "allie" }, AttackTargets(events));
        JsonElement applied = events.OfKind("stateApplied").Single();
        Assert.Equal(("assaillant", "provoc", "taunt", "bastion"), (applied.Str("combatant"), applied.Str("state"), applied.Str("category"), applied.Str("source")));
        Assert.Equal(60L, events.OfKind("stateExpired").Single().Tick());
        Assert.DoesNotContain(events.OfKind("damage"), d => d.Str("source") == "bastion");   // aucun impact : compétence de soutien
    }

    [Fact]
    public void Provocation_la_plus_recente_l_emporte()
    {
        // Bastion au tick 0, allié au tick 10 : l'attaque de 1 s vise l'allié. Le Bastion renouvelle au tick 30 : l'attaque de 3 s le vise.
        var events = Simulate(Fight(new[] { Use(0, Provocation, target: "assaillant"), Use(500, Provocation, "allie", "assaillant"),
                Use(1500, Provocation, target: "assaillant") },
            new CombatantSetup[] { Bastion(), Ally(), Brute("bastion") }), out _);
        Assert.Equal(new[] { "allie", "bastion" }, AttackTargets(events));
        Assert.Equal(new[] { (false, "bastion"), (false, "allie"), (true, "bastion") },
            events.OfKind("stateApplied").Select(a => (a.Bool("renewed"), a.Str("source")!)));
    }

    [Fact]
    public void Provocation_hors_assaillant_refusee()
    {
        // Sur soi (un personnage) et sur un mannequin : notApplicable ; l'assaillant, lui, est provoqué par la clameur.
        var events = Simulate(Fight(new[] { Use(0, Defi), Use(0, Clameur, "allie") },
            new CombatantSetup[] { Bastion(Defi, Clameur, Lien, Garde), Ally(Clameur, Lien, Barriere, Garde), Brute(), Dummy(armor: 0) }, durationMs: 1100), out _);
        // Ordre de résolution : l'allié (identifiant « allie ») avant le Bastion, à initiative égale.
        Assert.Equal(new[] { ("mannequin", "notApplicable"), ("bastion", "notApplicable") },
            events.OfKind("stateBlocked").Select(b => (b.Str("combatant")!, b.Str("reason")!)));
        Assert.Equal("assaillant", events.OfKind("stateApplied").Single().Str("combatant"));
        Assert.Equal(new[] { "allie" }, AttackTargets(events));   // provoqué par l'allié
    }

    [Fact]
    public void Mort_du_provocateur_retire_la_provocation()
    {
        // L'allié (240 PV) provoque l'assaillant (300 bruts), qui le tue à 1 s : la provocation est retirée en fin de tick, causée par la
        // mort ; l'attaque de 3 s revient au Bastion, cible du scénario.
        var events = Simulate(Fight(new[] { Use(0, Provocation, "allie", "assaillant") }, new CombatantSetup[] { Bastion(), Ally(), Brute("bastion", raw: 300) }), out _);
        Assert.Equal(new[] { "allie", "bastion" }, AttackTargets(events));
        JsonElement died = events.OfKind("died").Single();
        JsonElement removed = events.OfKind("stateRemoved").Single(r => r.Str("state") == "provoc");
        Assert.Equal(("assaillant", "sourceDied", died.Long("eid"), died.Tick()),
            (removed.Str("combatant"), removed.Str("reason"), removed.Long("cause"), removed.Tick()));
        Assert.DoesNotContain(events.OfKind("stateExpired"), e => e.Str("state") == "provoc");
    }

    [Fact]
    public void Majorant_de_file_admet_la_provocation()
    {
        // Avec une provocation au catalogue, toutes les cibles ennemies d'un assaillant sont possibles : le combat reste admis.
        CombatSetup setup = Fight(new[] { Use(0, Provocation, target: "assaillant") }, new CombatantSetup[] { Bastion(), Ally(), Brute() });
        Assert.Empty(setup.Validate());
        Assert.True(setup.ComputeQueueBound().Total > 0);
    }
}
