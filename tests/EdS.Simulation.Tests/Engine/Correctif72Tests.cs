using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Engine;

/// <summary>
/// Correctif 7.2 : étourdissement en empilement <c>ignore</c> (immunité toujours accordée à l'expiration, sauf décès), fin
/// effective des DOT alignée sur leur cadence d'origine et journalisée, parade parfaite qui empêche les altérations de l'impact
/// paré, sources multiples, frontières d'expiration, majorant prudent de la file et déterminisme.
/// Personnages de test : 20 de résistance aux altérations, étourdissement de 29 ticks, gel de 78, immunité de 60 (non réduite).
/// </summary>
[Trait("Category", "Engine")]
[Trait("Category", "Status")]
public class Correctif72Tests
{
    private static AssailantAttack Stun(string id = "assomme", bool unblockable = false, long raw = 100) => Strike(id, raw, unblockable: unblockable, applies: Apply("stun"));
    private static AssailantAttack Freeze(string id = "gele") => Strike(id, 100, applies: Apply("freeze"));
    private static AssailantAttack Burn(string id = "brule", bool unblockable = false) =>
        Strike(id, 100, DamageType.Magique, unblockable, Apply("burn", "0.4"));

    private static DummySetup Target(string id = "mannequin") => new(id, "joueurs", Fixed.Zero, Fixed.Zero);

    private static (CombatResult Result, List<JsonElement> Events) Fight(IEnumerable<CombatCommand> commands, long durationMs,
        params CombatantSetup[] fighters)
    {
        var (result, log) = Run(Setup(commands, fighters, durationMs: durationMs, rules: NoUpkeep));
        return (result, Events(log));
    }

    private static List<(long Tick, string State, string Reason)> Blocked(IEnumerable<JsonElement> events) =>
        events.OfKind("stateBlocked").Select(e => (e.Tick(), e.Str("state")!, e.Str("reason")!)).ToList();

    private static List<long> Applied(IEnumerable<JsonElement> events, string state) =>
        events.OfKind("stateApplied").Where(e => e.Str("state") == state).Select(e => e.Tick()).ToList();

    private static List<long> Expired(IEnumerable<JsonElement> events, string state) =>
        events.OfKind("stateExpired").Where(e => e.Str("state") == state).Select(e => e.Tick()).ToList();

    /// <summary>Assaillant qui frappe « mannequin » à <paramref name="firstMs"/> puis toutes les <paramref name="intervalMs"/>.</summary>
    private static AssailantSetup Caster(long firstMs, long intervalMs, string id = "assaillant", params AssailantAttack[] attacks) =>
        Assailant(firstMs, intervalMs, id, "cibles", "mannequin", attacks);

    /// <summary>Attaques sans altération (identifiants distincts) qui complètent une séquence.</summary>
    private static IEnumerable<AssailantAttack> Pauses(int count) => Enumerable.Range(0, count).Select(i => Blow(id: $"pause_{i}"));

    // --- 1. Étourdissement en ignore ---------------------------------------------------------------

    [Fact]
    public void Etourdissements_repetes_refuses_actifs_puis_immunite_garantie()
    {
        // Un étourdissement tous les 5 ticks pendant 200 ticks. Appliqué au tick 20 (fin 49) : les ticks 25 à 45 sont refusés
        // « active », sans renouvellement ; il expire au tick 49 et accorde l'immunité (fin 109) : 50 à 105 refusés « immune ».
        // Nouvel étourdissement au tick 110 (fin 139), refus « active » de 115 à 135, immunité de 139 à 199, refus « immune » ensuite.
        var (_, events) = Fight(Array.Empty<CombatCommand>(), 10_000, Tank(), Assailant(1000, 250, attacks: Stun(raw: 1)));
        Assert.Equal(new[] { 20L, 110L }, Applied(events, "stun"));
        Assert.DoesNotContain(events.OfKind("stateApplied"), e => e.GetProperty("renewed").GetBoolean());
        Assert.Equal(new[] { 49L, 139L }, Expired(events, "stun"));
        Assert.Equal(new[] { 49L, 139L }, Applied(events, "stunImmunity"));

        var expected = new List<(long, string, string)>();
        for (long t = 25; t < 200; t += 5)
        {
            if (t == 110) continue;
            bool active = (t > 20 && t < 49) || (t > 110 && t < 139);
            expected.Add((t, "stun", active ? "active" : "immune"));
        }
        Assert.Equal(expected, Blocked(events));
        Assert.All(events.OfKind("stateBlocked"), e => Assert.Equal(("control", "assaillant"), (e.Str("category")!, e.Str("source")!)));
    }

    [Fact]
    public void Expiration_d_un_etourdissement_toujours_suivie_de_l_immunite()
    {
        // Quelles que soient la cadence et la source des tentatives, chaque étourdissement expiré est suivi, dans la même étape,
        // de l'application de l'immunité avec la même source.
        foreach (long interval in new long[] { 50, 100, 350, 1450, 1500, 2950, 3000 })
        {
            var (_, events) = Fight(Array.Empty<CombatCommand>(), 20_000, Tank(),
                Assailant(1000, interval, id: "a", attacks: Stun(raw: 1)), Assailant(1050, interval * 2, id: "b", attacks: Stun(raw: 1)));
            Assert.Empty(events.OfKind("died"));
            var expirations = events.OfKind("stateExpired").Where(e => e.Str("state") == "stun").ToList();
            Assert.NotEmpty(expirations);
            foreach (JsonElement expired in expirations)
            {
                JsonElement next = events.Single(e => e.Int("seq") == expired.Int("seq") + 1 && e.Tick() == expired.Tick());
                Assert.Equal(("stateApplied", "stunImmunity", expired.Str("source")),
                    (next.Str("event")!, next.Str("state")!, next.Str("source")));
            }
            Assert.DoesNotContain(events.OfKind("stateApplied"), e => e.Str("state") == "stun" && e.GetProperty("renewed").GetBoolean());
        }
    }

    [Fact]
    public void Mort_pendant_l_etourdissement_retire_sans_immunite()
    {
        // « cible » est étourdie au tick 20 et tuée au tick 30 : l'étourdissement est retiré (death), aucune immunité accordée.
        var stunner = Assailant(1000, 60_000, "assaillant", "cibles", "cible", Stun());
        var killer = Assailant(1500, 60_000, "tueur", "cibles", "cible", Blow(raw: 1_000_000, id: "fatal"));
        var (_, events) = Fight(Array.Empty<CombatCommand>(), 6000, Character("cible"), Tank(), stunner, killer);
        JsonElement removed = events.OfKind("stateRemoved").Single(e => e.Str("state") == "stun");
        Assert.Equal((30L, "death", "assaillant"), (removed.Tick(), removed.Str("reason")!, removed.Str("source")!));
        Assert.Empty(Expired(events, "stun"));
        Assert.Empty(Applied(events, "stunImmunity"));
    }

    // --- 2. Fin effective des DOT --------------------------------------------------------------------

    [Fact]
    public void Dot_renouvele_hors_cadence_fin_effective_annoncee_et_appliquee()
    {
        // Brûlure au tick 20 (ticks 60, 100, 140), renouvelée au tick 70 : fin demandée 190, prochain tick 100 ; fin effective
        // 100 + ⌊ 90 / 40 ⌋ × 40 = 180, annoncée par untilTick et réellement appliquée (dernier tick et expiration au tick 180).
        var (_, events) = Fight(Array.Empty<CombatCommand>(), 20_000, Target(),
            Caster(1000, 2500, attacks: new[] { Burn(), Burn("encore") }.Concat(Pauses(10)).ToArray()));
        var applied = events.OfKind("stateApplied").ToList();
        Assert.Equal(new[] { (20L, 140L, 60L, false), (70L, 180L, 100L, true) },
            applied.Select(a => (a.Tick(), a.Int("untilTick"), a.Int("nextTick"), a.GetProperty("renewed").GetBoolean())));
        Assert.Equal(new long[] { 60, 100, 140, 180 }, events.OfKind("dotDamage").Select(e => e.Tick()));
        Assert.Equal(new[] { 180L }, Expired(events, "burn"));
    }

    [Fact]
    public void Fin_annoncee_egale_a_l_expiration_reelle_pour_toute_cadence_de_renouvellement()
    {
        // Pour chaque écart de renouvellement de 1 à 130 ticks : la dernière fin annoncée est alignée sur la cadence (multiple de
        // 40 ticks après le premier tick), c'est le tick de l'expiration réelle et celui du dernier tick de dégâts.
        for (long gap = 1; gap <= 130; gap++)
        {
            var (_, events) = Fight(Array.Empty<CombatCommand>(), 30_000, Target(),
                Caster(1000, gap * 50, attacks: new[] { Burn(), Burn("encore") }.Concat(Pauses(20)).ToArray()));
            var instances = events.OfKind("stateApplied").GroupBy(a => a.Int("instance"));
            foreach (var instance in instances)
            {
                JsonElement last = instance.Last();
                long first = instance.First().Int("nextTick");
                Assert.Equal(0, (last.Int("untilTick") - first) % 40);
                Assert.Equal(0, (last.Int("untilTick") - last.Int("nextTick")) % 40);
                JsonElement? expired = events.OfKind("stateExpired").Where(e => e.Int("instance") == instance.Key).Cast<JsonElement?>().SingleOrDefault();
                if (expired is null) { Assert.True(last.Int("untilTick") >= 600, $"écart {gap} : instance {instance.Key} sans expiration"); continue; }
                Assert.Equal(last.Int("untilTick"), expired.Value.Tick());
                Assert.Equal(expired.Value.Tick(), events.OfKind("dotDamage").Last(e => e.Int("seq") < expired.Value.Int("seq")).Tick());
            }
        }
    }

    // --- 3. Frontières d'expiration ----------------------------------------------------------------

    [Theory]
    [InlineData(6000, true)]    // tick 140 : renouvelée avant son dernier tick (résolution avant périodique), elle continue
    [InlineData(6050, false)]   // tick 141 : expirée au tick 140, nouvelle instance
    public void Dot_reapplique_a_la_frontiere_de_son_dernier_tick(long intervalMs, bool renewed)
    {
        var (_, events) = Fight(Array.Empty<CombatCommand>(), 20_000, Target(),
            Caster(1000, intervalMs, attacks: new[] { Burn(), Burn("encore") }.Concat(Pauses(10)).ToArray()));
        var applied = events.OfKind("stateApplied").ToList();
        Assert.Equal(renewed, applied[1].GetProperty("renewed").GetBoolean());
        if (renewed)
        {
            Assert.Equal((applied[0].Int("instance"), 260L, 140L), (applied[1].Int("instance"), applied[1].Int("untilTick"), applied[1].Int("nextTick")));
            Assert.Equal(new long[] { 60, 100, 140, 180, 220, 260 }, events.OfKind("dotDamage").Select(e => e.Tick()));
            Assert.Equal(new[] { 260L }, Expired(events, "burn"));
        }
        else
        {
            Assert.NotEqual(applied[0].Int("instance"), applied[1].Int("instance"));
            Assert.Equal(new[] { 140L, 261L }, Expired(events, "burn"));
        }
    }

    [Theory]
    [InlineData(1400, 48L, "active")]   // un tick avant l'expiration : encore actif
    [InlineData(1450, 49L, "immune")]   // au tick d'expiration : l'immunité est déjà accordée (phase 1)
    public void Etourdissement_reapplique_a_la_frontiere_de_l_expiration(long intervalMs, long tick, string reason)
    {
        var (_, events) = Fight(Array.Empty<CombatCommand>(), 3000, Tank(), Assailant(1000, intervalMs, attacks: new[] { Stun(), Stun("encore") }.Concat(Pauses(5)).ToArray()));
        Assert.Equal(new[] { (tick, "stun", reason) }, Blocked(events));
        Assert.Equal(new[] { 49L }, Expired(events, "stun"));
        Assert.Equal(new[] { 49L }, Applied(events, "stunImmunity"));
    }

    [Theory]
    [InlineData(3850, 97L, true)]    // un tick avant : même instance, fin repoussée (keepLonger)
    [InlineData(3900, 98L, false)]   // au tick d'expiration : expiré en phase 1, nouvelle instance
    public void Gel_reapplique_a_la_frontiere_de_l_expiration(long intervalMs, long tick, bool renewed)
    {
        var (_, events) = Fight(Array.Empty<CombatCommand>(), 10_000, Tank(), Assailant(1000, intervalMs, attacks: new[] { Freeze(), Freeze("encore") }.Concat(Pauses(3)).ToArray()));
        var applied = events.OfKind("stateApplied").ToList();
        Assert.Equal((tick, tick + 78, renewed), (applied[1].Tick(), applied[1].Int("untilTick"), applied[1].GetProperty("renewed").GetBoolean()));
        Assert.Equal(renewed, applied[0].Int("instance") == applied[1].Int("instance"));
        Assert.Equal(renewed ? new[] { tick + 78 } : new[] { 98L, tick + 78 }, Expired(events, "freeze"));
    }

    // --- 4. Parade parfaite, garde normale, attaques imparables --------------------------------------

    [Theory]
    [InlineData("parry", false, "parry", false)]          // parade parfaite : altérations empêchées
    [InlineData("guard", false, "guard", true)]           // garde normale : altérations conservées
    [InlineData("parry", true, "unblockable", true)]      // imparable pendant une parade : altérations conservées
    [InlineData("guard", true, "unblockable", true)]      // imparable pendant la garde : altérations conservées
    [InlineData("lateParry", false, "none", true)]        // parade ratée (fenêtre close) : altérations conservées
    public void Alterations_selon_la_defense(string defense, bool unblockable, string expected, bool applied)
    {
        CombatCommand[] commands = defense switch
        {
            "parry" => new[] { Parry(1000) },
            "guard" => new[] { GuardStart(500) },
            _ => new[] { Parry(500) },
        };
        var attack = new AssailantAttack("mixte", 100, DamageType.Magique, SkillTargeting.Enemy, unblockable,
            new[] { Apply("burn", "0.4"), Apply("stun"), Apply("fragility") });
        var (_, events) = Fight(commands, 8000, Tank(), Assailant(1000, 60_000, attacks: attack));
        JsonElement damage = Assert.Single(events.OfKind("damage"));
        Assert.Equal(expected, damage.TryGetProperty("defense", out JsonElement d) && d.ValueKind == JsonValueKind.String ? d.GetString() : "none");
        var states = new[] { "burn", "stun", "fragility" };
        if (applied)
        {
            Assert.Equal(states, events.OfKind("stateApplied").Where(e => e.Tick() == 20).Select(e => e.Str("state")!));
            Assert.Empty(events.OfKind("stateBlocked"));
            Assert.NotEmpty(events.OfKind("dotDamage"));
        }
        else
        {
            Assert.Equal(states.Select(s => (20L, s, "parried")), Blocked(events));
            Assert.Empty(events.OfKind("stateApplied"));
            Assert.Empty(events.OfKind("dotDamage"));
        }
    }

    // --- 5. Sources multiples, mort de la source -------------------------------------------------------

    [Fact]
    public void Deux_sources_d_etourdissement_la_seconde_refusee_active()
    {
        // « a » étourdit au tick 20 ; « b » au tick 25 est refusé « active » à son nom ; l'immunité porte la source de « a ».
        var (_, events) = Fight(Array.Empty<CombatCommand>(), 4000, Tank(),
            Assailant(1000, 60_000, id: "a", attacks: Stun()), Assailant(1250, 60_000, id: "b", attacks: Stun()));
        JsonElement blocked = Assert.Single(events.OfKind("stateBlocked"));
        Assert.Equal((25L, "b", "active"), (blocked.Tick(), blocked.Str("source")!, blocked.Str("reason")!));
        Assert.Equal("a", events.OfKind("stateApplied").Single(e => e.Str("state") == "stunImmunity").Str("source"));
    }

    [Fact]
    public void Mort_de_la_source_l_etourdissement_expire_et_accorde_l_immunite()
    {
        // « fragile » assomme l'assaillant au tick 0 puis meurt au tick 10 : l'étourdissement va à son terme (tick 30, sans
        // résistance) et accorde l'immunité, toutes deux à la source morte.
        var killer = Assailant(500, 60_000, "tueur", "cibles", "fragile", Blow(raw: 1_000_000));
        var (_, events) = Fight(new[] { Cmd(0, Assommer, actor: "fragile", target: "assaillant") }, 6000,
            Character("fragile", actives: new[] { Assommer, Frappe, Heurt, Onde }), Tank(), Assailant(60_000, 60_000), killer);
        Assert.Equal(10L, events.OfKind("died").Single().Tick());
        JsonElement expired = events.OfKind("stateExpired").Single(e => e.Str("state") == "stun");
        Assert.Equal((30L, "fragile"), (expired.Tick(), expired.Str("source")!));
        JsonElement immunity = events.OfKind("stateApplied").Single(e => e.Str("state") == "stunImmunity");
        Assert.Equal((30L, "fragile", "assaillant"), (immunity.Tick(), immunity.Str("source")!, immunity.Str("combatant")!));
    }

    [Fact]
    public void Deux_sources_de_dot_dont_une_morte_chacune_creditee()
    {
        // « fragile » (saignement au tick 0, mort au tick 10) et l'assaillant (brûlure au tick 20) : les deux DOT vont à leur
        // terme, chacun crédité à sa source, y compris la source morte.
        var killer = Assailant(500, 60_000, "tueur", "cibles", "fragile", Blow(raw: 1_000_000));
        var (result, events) = Fight(new[] { Cmd(0, Entaille, actor: "fragile") }, 10_000,
            Character("fragile", actives: new[] { Entaille, Frappe, Heurt, Onde }), Tank(), new DummySetup("mannequin", "cibles", default, default),
            Assailant(1000, 60_000, "brulure", "joueurs", "mannequin", Burn()), killer);
        Assert.Equal(new long[] { 40, 80, 120 }, events.OfKind("dotDamage").Where(e => e.Str("source") == "fragile").Select(e => e.Tick()));
        Assert.Equal(new long[] { 60, 100, 140 }, events.OfKind("dotDamage").Where(e => e.Str("source") == "brulure").Select(e => e.Tick()));
        long credited(string id) => events.OfKind("damage").Where(e => e.Str("source") == id).Sum(e => e.Int("amount"))
            + events.OfKind("dotDamage").Where(e => e.Str("source") == id).Sum(e => e.Int("amount"));
        Assert.Equal((credited("fragile"), credited("brulure")), (result["fragile"].DamageDealt, result["brulure"].DamageDealt));
    }

    // --- 6. Déterminisme --------------------------------------------------------------------------------

    [Fact]
    public void Journaux_du_correctif_deterministes()
    {
        static string Once() => Run(Setup(new[] { Parry(1000), Parry(3000) },
            new CombatantSetup[] { Tank(), Assailant(1000, 250, id: "a", attacks: new[] { Stun(), Burn(), Freeze() }), Assailant(1050, 700, id: "b", attacks: Burn("b")) },
            durationMs: 15_000, rules: NoUpkeep)).Log.ToString();
        string first = Once();
        Assert.Equal(first, Once());
        foreach (string expected in new[] { "\"reason\":\"active\"", "\"reason\":\"immune\"", "\"reason\":\"parried\"", "\"nextTick\":", "\"state\":\"stunImmunity\"" })
            Assert.Contains(expected, first);
    }
}
