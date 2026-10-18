using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Shields.Lot82Kit;

namespace EdS.Simulation.Tests.Shields;

/// <summary>
/// Sous-lot 8.2 (X4) : boucliers d'absorption. Valeur fixée à l'application (PV max de la source × coefficient × (1 + puissance de
/// boucliers)), un bouclier par source renouvelé au plus fort, consommation dans l'ordre de création, après la garde ou la parade et
/// avant les PV, sur les dégâts directs comme sur les DOT ; fin par durée (<c>stateExpired</c>) ou par épuisement (<c>stateRemoved</c>
/// <c>depleted</c> en fin de tick), retrait à la mort ; champ <c>absorbed</c> et détail <c>absorbedBy</c> au journal.
/// </summary>
[Trait("Category", "Shield")]
public class Lot82ShieldTests
{
    private static (CombatResult Result, List<JsonElement> Events) Simulate(CombatSetup setup)
    {
        var (result, log) = Run(setup);
        return (result, Events(log));
    }

    [Fact]
    public void Bouclier_absorbe_les_degats_directs_avant_les_PV()
    {
        // 2 100 PV × 0,1 = 210. Deux coups de 100 (1 s et 3 s) : 100 puis 100 absorbés, 10 restants ; aucun PV perdu.
        var (result, events) = Simulate(Fight(new[] { Cast() }, Protected(new[] { OnCast("egide", "0.1") })));
        JsonElement applied = events.OfKind("stateApplied").Single(e => e.Str("state") == "egide");
        Assert.Equal(("shield", "bastion", 210L, 80L, false), (applied.Str("category"), applied.Str("source"), applied.Long("absorb"), applied.Long("untilTick"),
            applied.GetProperty("renewed").GetBoolean()));
        JsonElement[] hits = events.OfKind("damage").Where(e => e.Str("target") == "bastion").ToArray();
        Assert.Equal(2, hits.Length);
        long instance = applied.Long("instance");
        Assert.Equal((100L, 100L, 2100L, 0L), (hits[0].Long("amount"), hits[0].Long("absorbed"), hits[0].Long("health"), hits[0].Long("damageTaken")));
        Assert.Equal(new[] { (instance, "egide", (string?)"bastion", 100L, 110L) }, AbsorbedBy(hits[0]));
        Assert.Equal(new[] { (instance, "egide", (string?)"bastion", 100L, 10L) }, AbsorbedBy(hits[1]));
        CombatantResult bastion = result["bastion"];
        Assert.Equal((0L, 200L, 2100L), (bastion.DamageTaken, bastion.DamageAbsorbed, bastion.Health!.Value));
        Assert.Equal(200, result["assaillant"].DamageDealt);   // la source compte le montant entier, absorbé compris
        JsonElement summary = events.OfKind("combatantSummary").Single(e => e.Str("combatant") == "bastion");
        Assert.Equal((0L, 200L), (summary.Long("damageTaken"), summary.Long("damageAbsorbed")));
    }

    [Fact]
    public void Bouclier_epuise_retire_en_fin_de_tick_avec_la_cause_de_son_epuisement()
    {
        // 2 100 × 0,02 = 42 : le coup de 100 en absorbe 42, 58 atteignent les PV ; le bouclier épuisé est retiré en phase 6 du même tick,
        // causé par la ligne de dégâts ; son échéance (tick 80) est périmée ; le coup suivant n'est plus absorbé.
        var (result, events) = Simulate(Fight(new[] { Cast() }, Protected(new[] { OnCast("egide", "0.02") })));
        JsonElement[] hits = events.OfKind("damage").Where(e => e.Str("target") == "bastion").ToArray();
        Assert.Equal((42L, 2042L, 58L), (hits[0].Long("absorbed"), hits[0].Long("health"), hits[0].Long("damageTaken")));
        Assert.Equal(0L, AbsorbedBy(hits[0]).Single().Remaining);
        JsonElement removed = Assert.Single(events.OfKind("stateRemoved"));
        Assert.Equal(("egide", "depleted", "endOfTick", 20L, hits[0].Long("eid")),
            (removed.Str("state"), removed.Str("reason"), removed.Str("phase"), removed.Tick(), removed.Long("cause")));
        Assert.DoesNotContain(events.OfKind("stateExpired"), e => e.Str("state") == "egide");
        Assert.Equal((0L, "[]"), (hits[1].Long("absorbed"), hits[1].GetProperty("absorbedBy").GetRawText()));
        Assert.Equal((158L, 42L), (result["bastion"].DamageTaken, result["bastion"].DamageAbsorbed));
    }

    [Fact]
    public void Plusieurs_boucliers_consommes_dans_l_ordre_de_creation()
    {
        // Barrière (105) posée avant l'Égide (42) : le coup de 100 consomme la Barrière (100, reste 5) ; le deuxième prend 5 à la
        // Barrière puis 42 à l'Égide, 53 atteignent les PV ; les deux sont retirés épuisés, dans l'ordre des instances.
        var (_, events) = Simulate(Fight(new[] { Cast() }, Protected(new[] { OnCast("barriere", "0.05"), OnCast("egide", "0.02") })));
        long barriere = events.OfKind("stateApplied").Single(e => e.Str("state") == "barriere").Long("instance");
        long egide = events.OfKind("stateApplied").Single(e => e.Str("state") == "egide").Long("instance");
        Assert.True(barriere < egide);
        JsonElement[] hits = events.OfKind("damage").Where(e => e.Str("target") == "bastion").ToArray();
        Assert.Equal(new[] { (barriere, "barriere", (string?)"bastion", 100L, 5L) }, AbsorbedBy(hits[0]));
        Assert.Equal(new[] { (barriere, "barriere", (string?)"bastion", 5L, 0L), (egide, "egide", (string?)"bastion", 42L, 0L) }, AbsorbedBy(hits[1]));
        Assert.Equal((47L, 53L), (hits[1].Long("absorbed"), hits[1].Long("damageTaken")));
        Assert.Equal(new[] { barriere, egide }, events.OfKind("stateRemoved").Select(e => e.Long("instance")));
        Assert.All(events.OfKind("stateRemoved"), e => Assert.Equal(("depleted", 60L), (e.Str("reason"), e.Tick())));
    }

    [Fact]
    public void Un_bouclier_par_source_renouvele_au_plus_fort_sans_changer_de_place()
    {
        // Égide 210 au lancer ; après le coup de 1 s il en reste 110, et la réaction à l'impact la renouvelle à 42 : la plus forte valeur
        // (110) est gardée, l'échéance repart (tick 20 + 80), même instance, génération suivante. Avec 420, la nouvelle valeur l'emporte.
        foreach (var (coefficient, expected) in new[] { ("0.02", 110L), ("0.2", 420L) })
        {
            var (_, events) = Simulate(Fight(new[] { Cast() }, Protected(new[] { OnCast("egide", "0.1"), OnHitTaken("egide", coefficient) }), durationMs: 1100));
            JsonElement[] applied = events.OfKind("stateApplied").Where(e => e.Str("state") == "egide").ToArray();
            Assert.Equal(2, applied.Length);
            Assert.Equal(applied[0].Long("instance"), applied[1].Long("instance"));
            Assert.Equal((true, expected, 100L, 2L), (applied[1].GetProperty("renewed").GetBoolean(), applied[1].Long("absorb"),
                applied[1].Long("untilTick"), applied[1].Long("generation")));
        }
    }

    [Fact]
    public void Boucliers_de_deux_sources_coexistent_sur_la_meme_cible()
    {
        // Un bouclier par (altération, source) : le Bastion se pose une Égide (210) ; un adversaire (Vitalité 100, 1 100 PV max) qui le
        // frappe lui en pose une autre (110) par réaction. Deux instances sur le Bastion, une par source ; le coup suivant de l'adversaire
        // consomme d'abord l'Égide du Bastion, créée la première.
        CharacterSetup rival = Character("rival", "cibles", attributes: Attrs(vitalite: 100), definition: Class(passiveTriggers: new[]
        {
            new TriggerDefinition(TriggerEvent.HitDealt, new[] { TriggerEffect.ApplyStatus(TriggerTarget.EventTarget, "egide", Fixed.Parse("0.1")) }, TriggerRole.Source),
        }), passives: new[] { Passif });
        var (_, events) = Simulate(Setup(new[] { Cmd(0, Gratuite, target: "rival"), Cmd(0, Gratuite, actor: "rival", target: "bastion"), Cmd(1000, Gratuite, actor: "rival", target: "bastion") },
            new CombatantSetup[] { Protected(new[] { OnCast("egide", "0.1") }), rival }, durationMs: 1100, rules: Rules));
        JsonElement[] onBastion = events.OfKind("stateApplied").Where(e => e.Str("state") == "egide" && e.Str("combatant") == "bastion").ToArray();
        Assert.Equal(new[] { ("bastion", 210L, false), ("rival", 110L, false), ("rival", 110L, true) },
            onBastion.Select(e => (e.Str("source")!, e.Long("absorb"), e.GetProperty("renewed").GetBoolean())));
        Assert.NotEqual(onBastion[0].Long("instance"), onBastion[1].Long("instance"));
        JsonElement second = events.OfKind("damage").Where(e => e.Str("target") == "bastion").Last();
        Assert.Equal("bastion", AbsorbedBy(second).Single().Source);
    }

    [Fact]
    public void Bouclier_absorbe_aussi_les_DOT_et_expire_a_sa_duree()
    {
        // Saignement de l'assaillant (100 × 0,5 = 50 par tick, ticks 60, 100, 140) ; Barrière 210 jusqu'au tick 120. Coup de 1 s : 100
        // absorbés (110) ; DOT 60 : 50 (60) ; DOT 100 : 50 (10) ; expiration au tick 120 (10 perdus) ; DOT 140 : 50 sur les PV.
        AssailantSetup bleeder = Assailant(intervalMs: 60_000, attacks: Strike("entaille", 100, DamageType.Physique, false, Apply("bleed", "0.5")));
        var (result, events) = Simulate(Fight(new[] { Cast() }, Protected(new[] { OnCast("barriere", "0.1") }), bleeder, durationMs: 7500));
        JsonElement[] dots = events.OfKind("dotDamage").ToArray();
        Assert.Equal(new[] { (60L, 50L, 50L), (100L, 50L, 50L), (140L, 50L, 0L) }, dots.Select(d => (d.Tick(), d.Long("amount"), d.Long("absorbed"))));
        Assert.Equal(new[] { 60L, 10L }, dots.Take(2).Select(d => AbsorbedBy(d).Single().Remaining));
        JsonElement expired = events.OfKind("stateExpired").Single(e => e.Str("state") == "barriere");
        Assert.Equal((120L, "expirations"), (expired.Tick(), expired.Str("phase")));
        Assert.Equal((50L, 200L), (result["bastion"].DamageTaken, result["bastion"].DamageAbsorbed));
    }

    [Fact]
    public void Garde_puis_bouclier_puis_PV()
    {
        // Garde à partir du tick 1 : le coup de 100 est réduit à 40 par la garde (−60 %), puis absorbé par l'Égide.
        var (_, events) = Simulate(Fight(new[] { Cast(), GuardStart(50) }, Protected(new[] { OnCast("egide", "0.1") }), durationMs: 1100));
        JsonElement hit = events.OfKind("damage").Single(e => e.Str("target") == "bastion");
        Assert.Equal(("guard", 40L, 40L, 2100L), (hit.Str("defense"), hit.Long("amount"), hit.Long("absorbed"), hit.Long("health")));
        Assert.Equal(170L, AbsorbedBy(hit).Single().Remaining);
    }

    [Fact]
    public void Parade_puis_bouclier()
    {
        // Parade parfaite au tick 20 : 100 réduit à 10 (−90 %), puis absorbé.
        var (_, events) = Simulate(Fight(new[] { Cast(), Parry(1000) }, Protected(new[] { OnCast("egide", "0.1") }), durationMs: 1100));
        JsonElement hit = events.OfKind("damage").Single(e => e.Str("target") == "bastion");
        Assert.Equal(("parry", 10L, 10L), (hit.Str("defense"), hit.Long("amount"), hit.Long("absorbed")));
    }

    [Fact]
    public void Mort_du_porteur_bouclier_retire_avec_la_raison_death()
    {
        // Vitalité 0 : 100 PV, Égide 50. Coup de 200 : 50 absorbés, 150 sur 100 PV, excédent 50 ; mort en fin de tick, l'Égide (épuisée)
        // est retirée avec les autres états, raison death, sans retrait depleted.
        AssailantSetup heavy = Assailant(intervalMs: 60_000, attacks: Blow(200));
        var (result, events) = Simulate(Fight(new[] { Cast() }, Protected(new[] { OnCast("egide", "0.5") }, vitalite: 0), heavy, durationMs: 2000));
        JsonElement hit = events.OfKind("damage").Single(e => e.Str("target") == "bastion");
        Assert.Equal((200L, 50L, 0L, 50L), (hit.Long("amount"), hit.Long("absorbed"), hit.Long("health"), hit.Long("overkill")));
        Assert.True(result["bastion"].IsDead);
        JsonElement removed = Assert.Single(events.OfKind("stateRemoved"));
        Assert.Equal(("egide", "death"), (removed.Str("state"), removed.Str("reason")));
    }

    [Fact]
    public void Bouclier_d_une_source_sans_PV_max_refuse_empty()
    {
        // Un assaillant (PV infinis) qui se pose une Égide : valeur nulle, application refusée et journalisée (stateBlocked, empty).
        var shielder = new AssailantSetup("assaillant", "cibles", Fixed.Zero, Fixed.Zero, "bastion", 1000, 60_000, new[] { Blow() },
            triggers: new[] { new TriggerDefinition(TriggerEvent.HitDealt, new[] { TriggerEffect.ApplyStatus(TriggerTarget.Bearer, "egide", Fixed.Parse("0.5")) }, TriggerRole.Source) });
        var (_, events) = Simulate(Fight(Array.Empty<CombatCommand>(), Protected(Array.Empty<TriggerDefinition>()), shielder, durationMs: 1100));
        JsonElement blocked = Assert.Single(events.OfKind("stateBlocked"));
        Assert.Equal(("assaillant", "egide", "shield", "empty"), (blocked.Str("combatant"), blocked.Str("state"), blocked.Str("category"), blocked.Str("reason")));
        Assert.Empty(events.OfKind("stateApplied"));
    }

    [Fact]
    public void Puissance_de_boucliers_du_poseur()
    {
        // shield.potencyDealt +0,5 sur le passif : 210 × 1,5 = 315 ; −1 (plancher) : bouclier nul, refusé (empty).
        var (_, events) = Simulate(Fight(new[] { Cast() }, Protected(new[] { OnCast("egide", "0.1") }, passiveModifiers: new[] { Mod("shield.potencyDealt", "0.5") }), durationMs: 100));
        Assert.Equal(315L, events.OfKind("stateApplied").Single().Long("absorb"));
        Assert.Contains("\"shield.potencyDealt\":0.5", events.OfKind("combatant").Single(e => e.Str("combatant") == "bastion").GetRawText());
        var (_, none) = Simulate(Fight(new[] { Cast() }, Protected(new[] { OnCast("egide", "0.1") }, passiveModifiers: new[] { Mod("shield.potencyDealt", "-1") }), durationMs: 100));
        Assert.Equal("empty", Assert.Single(none.OfKind("stateBlocked")).Str("reason"));
    }

    [Fact]
    public void Valeur_d_un_bouclier_partagee_avec_le_precontrole()
    {
        // ⌊ 2 100 × 0,1 × 1,5 ⌋ = 315 ; ⌊ 2 100 × 0,0333 ⌋ = ⌊ 69,93 ⌋ = 69 ; source sans PV max, coefficient nul ou puissance −1 : 0.
        Assert.Equal(315, EdS.Domain.Combat.Shields.Value(2100, Fixed.Parse("0.1"), Fixed.Parse("0.5")));
        Assert.Equal(69, EdS.Domain.Combat.Shields.Value(2100, Fixed.Parse("0.0333"), Fixed.Zero));
        Assert.Equal(0, EdS.Domain.Combat.Shields.Value(null, Fixed.Parse("0.5"), Fixed.Zero));
        Assert.Equal(0, EdS.Domain.Combat.Shields.Value(2100, Fixed.Zero, Fixed.Zero));
        Assert.Equal(0, EdS.Domain.Combat.Shields.Value(2100, Fixed.Parse("0.5"), -Fixed.One));
        Assert.Throws<OverflowException>(() => EdS.Domain.Combat.Shields.Value(long.MaxValue / 10_000, Fixed.FromInt(100), Fixed.FromInt(10)));
    }

    [Fact]
    public void Journal_rejoue_et_graphe_causal_complet()
    {
        // Boucliers, épuisement et DOT absorbés : le journal se rejoue à l'identique et chaque effet a sa cause (vérifié par Run).
        AssailantSetup bleeder = Assailant(intervalMs: 2000, attacks: Strike("entaille", 100, DamageType.Physique, false, Apply("bleed", "0.5")));
        CombatSetup setup = Fight(new[] { Cast(), Cast(2000) }, Protected(new[] { OnCast("barriere", "0.05"), OnCast("egide", "0.02") }), bleeder, durationMs: 8000);
        var (_, log) = Run(setup);
        var (_, again) = Run(setup);
        Assert.True(CombatReplay.Compare(log.Lines, again.Lines).Identical);
        List<JsonElement> events = Events(log);
        Assert.Contains(events.OfKind("stateRemoved"), e => e.Str("reason") == "depleted");
        Assert.Contains(events.OfKind("dotDamage"), d => d.Long("absorbed") > 0);
    }
}
