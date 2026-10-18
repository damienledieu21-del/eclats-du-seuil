using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.BastionSkills.Lot84Kit;

namespace EdS.Simulation.Tests.BastionSkills;

/// <summary>
/// Sous-lot 8.4 : fiches du Bastion au combat, avec les altérations livrées. Provocation (+5 Résonance au lancement, plafonnée,
/// observable) ; Barrière d'acier (bouclier de 15 % des PV max du lanceur, sur soi ou un allié) ; Onde de rupture (zone et Fragilité) ;
/// ultime Bastion absolu (100 de Résonance, dégâts subis −30 % pour le Bastion et ses alliés, garde sans entretien pour le Bastion,
/// provocation de tous les assaillants, 8 s).
/// </summary>
[Trait("Category", "Bastion")]
public class Lot84SkillTests
{
    private static AssailantSetup Brute(string target = "allie", long raw = 100, long firstMs = 1000, long intervalMs = 2000, string id = "assaillant") =>
        new(id, "cibles", Fixed.Zero, Fixed.Zero, target, firstMs, intervalMs, new[] { Blow(raw) });

    private static CharacterSetup Ally(string id = "allie") => Hero(id, new[] { Charger, Contrecoup, Fixe, Provocation }, vitalite: 14);

    [Fact]
    public void Provocation_gagne_5_de_Resonance_au_lancement()
    {
        var events = Simulate(Fight(new[] { Cmd(0, Provocation, target: "assaillant") }, new CombatantSetup[] { Hero(), Ally(), Brute() }), out _);
        long cast = events.OfKind("skillCast").Single().Long("eid");
        JsonElement gained = events.OfKind("resourceGained").Single(g => g.Str("source") == "skill");
        Assert.Equal(("bastion", "resonance", 5L, cast), (gained.Str("actor"), gained.Str("resource"), gained.Long("amount"), gained.Long("cause")));
        Assert.Equal(("assaillant", "taunt", 60L), (events.OfKind("stateApplied").Single().Str("combatant"), events.OfKind("stateApplied").Single().Str("state"),
            events.OfKind("stateExpired").Single().Tick()));
        Assert.Equal(new[] { "bastion", "allie" }, events.OfKind("attackStarted").Select(a => a.Str("target")));
    }

    [Fact]
    public void Resonance_gagnee_meme_si_la_provocation_est_refusee()
    {
        // Interprétation 8.4 : le gain est celui du lancement ; sur un mannequin, la provocation est refusée (notApplicable).
        var events = Simulate(Fight(new[] { Cmd(0, Provocation) }, new CombatantSetup[] { Hero(), Dummy(armor: 0) }, durationMs: 100), out CombatResult result);
        Assert.Equal("notApplicable", events.OfKind("stateBlocked").Single().Str("reason"));
        Assert.Equal(5, result["bastion"].Resonance);
    }

    [Fact]
    public void Gain_plafonne_et_observable()
    {
        // 21 lancers (sans recharge dans le kit) : 100 de Résonance, le dernier gain est perdu ; un passif réagit à chaque gain observé.
        ClassDefinition definition = KitClass(passiveTriggers: new[]
        {
            new TriggerDefinition(TriggerEvent.ResourceGained, new[] { TriggerEffect.GainResource(TriggerResource.RampartCharges, Fixed.One) },
                filter: new TriggerFilter(resource: TriggerResource.Resonance)),
        });
        var commands = Enumerable.Range(0, 21).Select(i => Cmd(50L * i, Provocation)).ToArray();
        var events = Simulate(Fight(commands, new CombatantSetup[] { Hero(definition: definition, vitalite: 200), Dummy(armor: 0) }, durationMs: 1100,
            rules: new CombatRules { GlobalCooldownMs = 0, EnduranceRegenPerSecond = 1000, EnduranceRegenDelayMs = 0 }), out CombatResult result);
        JsonElement[] gains = events.OfKind("resourceGained").Where(g => g.Str("resource") == "resonance").ToArray();
        Assert.Equal(21, gains.Length);
        Assert.Equal((0L, 5L, 100L), (gains[^1].Long("amount"), gains[^1].Long("wasted"), gains[^1].Long("value")));
        Assert.Equal(100, result["bastion"].Resonance);
        Assert.Equal(5, result["bastion"].RampartCharges);   // une réaction par gain notifié (20 utiles, plafond de 5 Charges)
    }

    [Fact]
    public void Barriere_d_acier_sur_soi_ou_sur_un_allie()
    {
        // 2 100 PV × 0,15 = 315 ; recharge de 20 s : le second lancer est refusé.
        CharacterSetup hero = Hero(actives: new[] { Barriere, Contrecoup, Fixe, Provocation });
        var events = Simulate(Fight(new[] { Cmd(0, Barriere, target: "allie"), Cmd(500, Barriere, target: "bastion") },
            new CombatantSetup[] { hero, Ally(), Brute() }, durationMs: 1100), out CombatResult result);
        JsonElement applied = events.OfKind("stateApplied").Single();
        Assert.Equal(("allie", "steelBarrier", "shield", 315L), (applied.Str("combatant"), applied.Str("state"), applied.Str("category"), applied.Long("absorb")));
        Assert.Equal("cooldown", events.OfKind("commandRejected").Single().Str("reason"));
        Assert.Equal(100, result["allie"].DamageAbsorbed);
        var self = Simulate(Fight(new[] { Cmd(0, Barriere, target: "bastion") }, new CombatantSetup[] { hero, Brute("bastion") }, durationMs: 7000), out _);
        Assert.Equal(("bastion", 120L), (self.OfKind("stateApplied").Single().Str("combatant"), self.OfKind("stateExpired").Single(e => e.Str("state") == "steelBarrier").Tick()));
        Assert.Equal("targetNotAlly", Simulate(Fight(new[] { Cmd(0, Barriere, target: "assaillant") }, new CombatantSetup[] { hero, Brute("bastion") },
            durationMs: 100), out _).OfKind("commandRejected").Single().Str("reason"));
    }

    [Fact]
    public void Onde_de_rupture_frappe_tous_les_ennemis_et_les_fragilise()
    {
        CharacterSetup hero = Hero(actives: new[] { OndeDeRupture, Contrecoup, Fixe, Provocation });
        var events = Simulate(Fight(new[] { Cmd(0, OndeDeRupture, target: null) },
            new CombatantSetup[] { hero, Dummy("m1", armor: 0), Dummy("m2", armor: 0) }, durationMs: 100), out _);
        Assert.Equal(new[] { "m1", "m2" }, events.OfKind("damage").Select(d => d.Str("target")));
        Assert.All(events.OfKind("damage"), d => Assert.Equal(Expected(hero, "0.5", d.GetProperty("critical").GetBoolean()), d.Long("amount")));
        Assert.Equal(new[] { ("m1", "fragility"), ("m2", "fragility") }, events.OfKind("stateApplied").Select(a => (a.Str("combatant")!, a.Str("state")!)));
    }

    /// <summary>
    /// 20 Provocations (100 de Résonance) puis l'ultime au tick 20, garde prise au tick 21 ; allié et assaillant (frappe le Bastion à
    /// 2 s puis toutes les 2 s, 100 bruts) ; mannequin hostile.
    /// </summary>
    private static List<JsonElement> Ultimate(out CombatResult result, long durationMs = 12_000)
    {
        var commands = Enumerable.Range(0, 20).Select(i => Cmd(50L * i, Provocation, target: "assaillant"))
            .Append(Cmd(1000, Absolu, target: null)).Append(GuardStart(1050)).ToList();
        return Simulate(Fight(commands, new CombatantSetup[] { Hero(), Ally(), Brute("allie", firstMs: 2000), Dummy(armor: 0) }, durationMs: durationMs,
            rules: new CombatRules { GlobalCooldownMs = 0, EnduranceRegenPerSecond = 1000, EnduranceRegenDelayMs = 0 }), out result);
    }

    [Fact]
    public void Bastion_absolu_renforce_les_allies_et_provoque_les_assaillants()
    {
        var events = Ultimate(out CombatResult result);
        JsonElement cast = events.OfKind("skillCast").Single(c => c.Str("skill") == Absolu);
        Assert.Equal((20L, 100L, 0L), (cast.Tick(), cast.Long("resonanceCost"), cast.Long("resonance")));
        var applied = events.OfKind("stateApplied").Where(a => a.Long("cause") == cast.Long("eid")).Select(a => (a.Str("combatant")!, a.Str("state")!)).ToArray();
        // Applications aux cibles de l'action (soi) d'abord, puis les cibles propres dans l'ordre de la fiche (règle du 8.3).
        Assert.Equal(new[] { ("bastion", "bastionAbsoluteGuard"), ("allie", "bastionAbsolute"), ("bastion", "bastionAbsolute"), ("assaillant", "bastionAbsoluteTaunt") },
            applied);
        Assert.Contains(events.OfKind("stateBlocked"), b => b.Str("combatant") == "mannequin" && b.Str("reason") == "notApplicable");
        // L'assaillant, provoqué, frappe le Bastion à 2 s (100 × 0,4 de garde × 0,7) puis, après 8 s (tick 180), revient à l'allié.
        JsonElement[] attacks = events.OfKind("attackStarted").ToArray();
        Assert.Equal(new[] { "bastion", "bastion", "bastion", "bastion", "allie" }, attacks.Select(a => a.Str("target")));
        JsonElement first = events.OfKind("damage").First();
        Assert.Equal(("bastion", 28L), (first.Str("target"), first.Long("amount")));
        Assert.Equal(180L, events.OfKind("stateExpired").Single(e => e.Str("state") == "bastionAbsoluteGuard").Tick());
        Assert.Equal(8, result["bastion"].Resonance);   // dépensée par l'ultime, puis 4 blocages × 2
    }

    [Fact]
    public void Garde_sans_entretien_pendant_l_ultime()
    {
        // Garde du tick 21 au-delà de l'expiration (tick 180) : aucun entretien avant, 2/s ensuite (premier point au tick 189).
        var events = Ultimate(out _, durationMs: 10_000);
        JsonElement[] upkeep = events.OfKind("guardUpkeep").ToArray();
        Assert.NotEmpty(upkeep);
        Assert.All(upkeep, u => Assert.True(u.Tick() > 180));
        Assert.Equal(189L, upkeep[0].Tick());
        // Le Bastion seul en porte la garde sans entretien ; ses alliés, seulement la réduction de dégâts.
        Assert.DoesNotContain(events.OfKind("stateApplied"), a => a.Str("combatant") == "allie" && a.Str("state") == "bastionAbsoluteGuard");
    }

    [Fact]
    public void Ultime_refusee_sans_Resonance()
    {
        var events = Simulate(Fight(new[] { Cmd(0, Absolu, target: null) }, new CombatantSetup[] { Hero(), Brute("bastion") }, durationMs: 100), out _);
        Assert.Equal("insufficientResonance", events.OfKind("commandRejected").Single().Str("reason"));
        Assert.Empty(events.OfKind("stateApplied"));
    }
}
