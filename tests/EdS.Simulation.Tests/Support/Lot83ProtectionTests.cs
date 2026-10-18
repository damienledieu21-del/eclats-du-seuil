using System.Text.Json;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Support.Lot83Kit;

namespace EdS.Simulation.Tests.Support;

/// <summary>
/// Sous-lot 8.3 (X6) : protection et transfert de dégâts. Une part ⌊ montant × part ⌋ des dégâts subis par le protégé va au protecteur
/// (ligne <c>damageTransferred</c>, jamais notifiée ni retransférée), qui la reçoit sans ses modificateurs de dégâts subis, par sa garde
/// seulement avec <c>protection.redirectThroughGuard</c>, puis ses boucliers. La protection est retirée à la mort de sa source.
/// </summary>
[Trait("Category", "Support")]
public class Lot83ProtectionTests
{
    private static (JsonElement Hit, JsonElement Transfer) FirstHit(List<JsonElement> events) =>
        (events.OfKind("damage").First(d => d.Str("target") == "allie"), events.OfKind("damageTransferred").First());

    [Fact]
    public void Transfert_d_un_quart_conserve_les_degats()
    {
        var events = Simulate(Fight(new[] { Use(0, Lien, target: "allie") }, new CombatantSetup[] { Bastion(), Ally(), Brute() }, durationMs: 1100),
            out CombatResult result);
        JsonElement applied = events.OfKind("stateApplied").Single();
        Assert.Equal(("allie", "lien", "protection", "bastion", Fixed.Parse("0.25")),
            (applied.Str("combatant"), applied.Str("state"), applied.Str("category"), applied.Str("source"), Fixed.Parse(applied.GetProperty("redirectRatio").GetRawText())));
        var (hit, transfer) = FirstHit(events);
        Assert.Equal((100L, 25L, 165L, 75L), (hit.Long("amount"), hit.Long("transferred"), hit.Long("health"), hit.Long("damageTaken")));
        Assert.Equal(("assaillant", "allie", "bastion", "lien", applied.Long("instance")),
            (transfer.Str("source"), transfer.Str("protected"), transfer.Str("target"), transfer.Str("state"), transfer.Long("instance")));
        Assert.Equal((25L, "none", 25L, 0L, 2075L, 25L), (transfer.Long("amount"), transfer.Str("defense"), transfer.Long("taken"),
            transfer.Long("absorbed"), transfer.Long("health"), transfer.Long("damageTaken")));
        Assert.Equal(hit.Long("eid"), transfer.Long("cause"));
        Assert.Equal((100L, 75L, 25L, 25L, 25L), (result["assaillant"].DamageDealt, result["allie"].DamageTaken, result["allie"].DamageTransferred,
            result["bastion"].DamageTaken, result["bastion"].DamageRedirected));
    }

    [Theory]
    [InlineData(33, 8)]     // ⌊ 8,25 ⌋ : l'allié garde 25
    [InlineData(3, 0)]      // part nulle : ligne de transfert écrite quand même (coût indépendant des dégâts)
    [InlineData(400, 100)]
    public void Part_arrondie_vers_le_bas_le_reste_au_protege(long raw, long part)
    {
        var events = Simulate(Fight(new[] { Use(0, Lien, target: "allie") }, new CombatantSetup[] { Bastion(), Ally(), Brute(raw: raw) }, durationMs: 1100), out _);
        var (hit, transfer) = FirstHit(events);
        Assert.Equal((part, part), (hit.Long("transferred"), transfer.Long("amount")));
        Assert.Equal(Math.Max(0, 240 - (raw - part)), hit.Long("health"));
    }

    [Fact]
    public void Part_du_protecteur_ajoutee_par_modificateur()
    {
        // protection.redirectRatio +0,15 sur le protecteur : 40 %.
        var bastion = Hero("bastion", new[] { Lien, Barriere, Garde, Provocation }, 200, SupportClass(passiveModifiers: new[] { Mod("protection.redirectRatio", "0.15") }));
        var events = Simulate(Fight(new[] { Use(0, Lien, target: "allie") }, new CombatantSetup[] { bastion, Ally(), Brute() }, durationMs: 1100), out _);
        Assert.Equal(Fixed.Parse("0.4"), Fixed.Parse(events.OfKind("stateApplied").Single().GetProperty("redirectRatio").GetRawText()));
        Assert.Equal(40L, FirstHit(events).Transfer.Long("amount"));
    }

    [Theory]
    [InlineData(true, 10L, "guard")]    // ⌊ 25 × (1 − 0,6) ⌋, sans coût de blocage
    [InlineData(false, 25L, "none")]    // sans le drapeau, la garde du protecteur ne s'applique pas
    public void Transfert_par_la_garde_du_protecteur_selon_le_drapeau(bool flag, long taken, string defense)
    {
        var bastion = Hero("bastion", new[] { Lien, Barriere, Garde, Provocation }, 200,
            SupportClass(passiveModifiers: flag ? new[] { Mod("protection.redirectThroughGuard", "1") } : null));
        var events = Simulate(Fight(new[] { Use(0, Lien, target: "allie"), GuardStart(50) }, new CombatantSetup[] { bastion, Ally(), Brute() }, durationMs: 1100), out _);
        JsonElement transfer = FirstHit(events).Transfer;
        Assert.Equal((25L, defense, taken, 2100 - taken), (transfer.Long("amount"), transfer.Str("defense"), transfer.Long("taken"), transfer.Long("health")));
        Assert.Empty(events.OfKind("blocked"));
    }

    [Theory]
    [InlineData(true, 20L)]     // garde tenue sans endurance (guard.noUpkeepBreak) : réduction à 0 d'endurance, ⌊ 25 × (1 − 0,2) ⌋
    [InlineData(false, 10L)]    // sans le drapeau : un transfert ne paie rien ni ne brise la garde, réduction pleine
    public void Transfert_par_une_garde_affaiblie(bool unbreakable, long taken)
    {
        // Revue du 8.3 : le protecteur garde sans endurance (endurance max ramenée à 0) ; la réduction appliquée au transfert est celle
        // qu'aurait un blocage au même instant.
        var modifiers = new List<ModifierDefinition> { Mod("protection.redirectThroughGuard", "1"), Mod("endurance.max", "-1000") };
        if (unbreakable) modifiers.AddRange(new[] { Mod("guard.noUpkeepBreak", "1"), Mod("guard.reductionAtZeroEndurance", "0.2") });
        var bastion = Hero("bastion", new[] { Lien, Barriere, Garde, Provocation }, 200, SupportClass(passiveModifiers: modifiers.ToArray()));
        var events = Simulate(Fight(new[] { Use(0, Lien, target: "allie"), GuardStart(50) }, new CombatantSetup[] { bastion, Ally(), Brute() }, durationMs: 1100), out _);
        Assert.Equal(0L, events.OfKind("guardStarted").Single().Long("endurance"));
        JsonElement transfer = FirstHit(events).Transfer;
        Assert.Equal((25L, "guard", taken), (transfer.Long("amount"), transfer.Str("defense"), transfer.Long("taken")));
        Assert.Empty(events.OfKind("guardBroken"));
    }

    [Fact]
    public void Boucliers_du_protege_et_du_protecteur()
    {
        // Égide de 210 sur chacun : l'allié absorbe sa part (75), le Bastion la part transférée (25) ; personne ne perd de PV.
        // Le Bastion pose « lien » ; deux gardiens (2 100 PV) posent chacun une égide de 210, l'un sur l'allié, l'autre sur le Bastion.
        var events = Simulate(Fight(new[] { Use(0, Lien, target: "allie"), Use(0, Barriere, "gardien", "allie"), Use(0, Barriere, "gardien2", "bastion") },
            new CombatantSetup[] { Bastion(), Ally(), Hero("gardien", new[] { Lien, Barriere, Garde, Provocation }, 200),
                Hero("gardien2", new[] { Lien, Barriere, Garde, Provocation }, 200), Brute() }, durationMs: 1100), out _);
        var (hit, transfer) = FirstHit(events);
        Assert.Equal((25L, 75L, 240L), (hit.Long("transferred"), hit.Long("absorbed"), hit.Long("health")));
        Assert.Equal((25L, 25L, 2100L), (transfer.Long("taken"), transfer.Long("absorbed"), transfer.Long("health")));
        Assert.Equal(185L, transfer.GetProperty("absorbedBy")[0].Long("remaining"));
    }

    [Fact]
    public void Ticks_de_DOT_transferes_aussi()
    {
        // Saignement de l'assaillant (puissance 50) : chaque tick de 50 sur l'allié en transfère ⌊ 12,5 ⌋ = 12.
        var events = Simulate(Fight(new[] { Use(0, Lien, target: "allie") },
            new CombatantSetup[] { Bastion(), Ally(), Brute(intervalMs: 60_000, applies: Apply("bleed", "0.5")) }, durationMs: 5100), out _);
        JsonElement[] dots = events.OfKind("dotDamage").ToArray();
        Assert.Equal(new[] { 12L, 12L }, dots.Select(d => d.Long("transferred")));
        JsonElement[] transfers = events.OfKind("damageTransferred").Skip(1).ToArray();   // le premier suit l'impact
        Assert.Equal(dots.Select(d => d.Long("eid")), transfers.Select(t => t.Long("cause")));
    }

    [Fact]
    public void Transfert_ni_retransfere_ni_notifie()
    {
        // L'allié est protégé par le Bastion, le Bastion par le gardien : un coup sur l'allié ne produit qu'un transfert. Le Bastion
        // porte un déclencheur « impact subi » (+1 Charge) : le transfert ne le déclenche pas.
        var watcher = Hero("bastion", new[] { Lien, Barriere, Garde, Provocation }, 200, SupportClass(passiveTriggers: new[]
        {
            new TriggerDefinition(TriggerEvent.HitTaken, new[] { TriggerEffect.GainResource(TriggerResource.RampartCharges, Fixed.One) }),
        }));
        var events = Simulate(Fight(new[] { Use(0, Lien, target: "allie"), Use(0, Lien, "gardien", "bastion") },
            new CombatantSetup[] { watcher, Ally(), Hero("gardien", new[] { Lien, Barriere, Garde, Provocation }), Brute() }, durationMs: 1100), out _);
        JsonElement transfer = events.OfKind("damageTransferred").Single();
        Assert.Equal(("allie", "bastion"), (transfer.Str("protected"), transfer.Str("target")));
        Assert.DoesNotContain(events.OfKind("resourceGained"), g => g.Str("actor") == "bastion");
        Assert.DoesNotContain(events, e => e.TryGetProperty("depth", out _));
    }

    [Fact]
    public void Protection_hors_allie_personnage_refusee()
    {
        // Réseau pose « lien » sur tous les alliés : l'allié est protégé, le lanceur lui-même refusé (notApplicable). Un mannequin allié
        // (sans PV) ne l'est pas non plus.
        var events = Simulate(Fight(new[] { Use(0, Reseau) },
            new CombatantSetup[] { Bastion(Reseau, Lien, Barriere, Garde), Ally(), Brute(), Dummy("poteau", "joueurs") }, durationMs: 100), out _);
        Assert.Equal("allie", events.OfKind("stateApplied").Single().Str("combatant"));
        Assert.Equal(new[] { ("bastion", "notApplicable"), ("poteau", "notApplicable") },
            events.OfKind("stateBlocked").Select(b => (b.Str("combatant")!, b.Str("reason")!)));
    }

    [Fact]
    public void Protection_la_plus_recente_l_emporte()
    {
        var events = Simulate(Fight(new[] { Use(0, Lien, target: "allie"), Use(500, Lien, "gardien", "allie") },
            new CombatantSetup[] { Bastion(), Ally(), Hero("gardien", new[] { Lien, Barriere, Garde, Provocation }), Brute() }, durationMs: 1100), out _);
        Assert.Equal("gardien", events.OfKind("damageTransferred").Single().Str("target"));
    }

    [Fact]
    public void Mort_du_protecteur_retire_la_protection()
    {
        // Le gardien (240 PV) protège le Bastion ; l'assaillant (300 bruts) tue le gardien à 1 s : « lien » est retiré du Bastion
        // (sourceDied) ; le coup de 3 s sur le Bastion n'est plus transféré.
        var events = Simulate(Fight(new[] { Use(0, Lien, "gardien", "bastion") },
            new CombatantSetup[] { Bastion(), Hero("gardien", new[] { Lien, Barriere, Garde, Provocation }), new AssailantSetup("assaillant", "cibles",
                Fixed.Zero, Fixed.Zero, "gardien", 1000, 2000, new[] { new AssailantAttack("coup", 300, Domain.Stats.DamageType.Physique, Domain.Builds.SkillTargeting.Enemy, false, null) }) },
            durationMs: 3100), out _);
        JsonElement removed = events.OfKind("stateRemoved").Single(r => r.Str("state") == "lien");
        Assert.Equal(("bastion", "sourceDied"), (removed.Str("combatant"), removed.Str("reason")));
        // Après la mort du gardien, l'assaillant n'a plus de cible : son attaque de 3 s est sautée ; aucun transfert n'a eu lieu.
        Assert.Empty(events.OfKind("damageTransferred"));
    }

    [Fact]
    public void Protecteur_tue_par_un_transfert()
    {
        // Le gardien (240 PV) protège le Bastion à 100 % ; un coup de 300 sur le Bastion lui est entièrement transféré : il meurt,
        // la cause de sa mort est la ligne de transfert.
        // L'assaillant porte un déclencheur « mort » (rôle source) : la mort par transfert, au nom du protecteur, ne le déclenche pas.
        var brute = new AssailantSetup("assaillant", "cibles", Fixed.Zero, Fixed.Zero, "bastion", 1000, 2000,
            new[] { new AssailantAttack("coup", 300, Domain.Stats.DamageType.Physique, Domain.Builds.SkillTargeting.Enemy, false, null) },
            triggers: new[] { new TriggerDefinition(TriggerEvent.Death, new[] { TriggerEffect.ApplyStatus(TriggerTarget.Bearer, "fortifie") }) });
        var events = Simulate(Fight(new[] { Use(0, LienTotal, "gardien", "bastion") },
            new CombatantSetup[] { Bastion(), Hero("gardien", new[] { LienTotal, Barriere, Garde, Provocation }), brute }, durationMs: 1100),
            out CombatResult result);
        Assert.DoesNotContain(events.OfKind("stateApplied"), a => a.Str("state") == "fortifie");
        JsonElement transfer = events.OfKind("damageTransferred").Single();
        Assert.Equal((300L, 60L, 0L), (transfer.Long("amount"), transfer.Long("overkill"), transfer.Long("health")));
        JsonElement died = events.OfKind("died").Single();
        Assert.Equal(("gardien", transfer.Long("eid")), (died.Str("combatant"), died.Long("cause")));
        Assert.Equal((0L, 2100L), (result["bastion"].DamageTaken, events.OfKind("damage").Single().Long("health")));
    }
}
