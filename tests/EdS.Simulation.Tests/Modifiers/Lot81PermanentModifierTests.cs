using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Modifiers;

// Sous-lot 8.1, modificateurs permanents (X1) dans le moteur. Correspondance avec la liste obligatoire :
// - chaque clé au rang 1 et au rang maximal : Lot81PermanentModifierEquivalenceTests (talent alpha au rang 1 et au rang 5, perRank) ;
// - bornes N−1, N et N+1 : Lot81PermanentModifierClampTests (plafonds et planchers d'écrêtage, intervalle élargi à la base) ;
// - combinaisons de modificateurs : Lot81PermanentModifierAggregationTests (même clé de trois sources, huit clés à la fois) ;
// - agrégation indépendante de l'ordre : Lot81PermanentModifierAggregationTests (échanges, inversion, 120 permutations) ;
// - immuabilité : Lot81PermanentModifierAggregationTests (copies défensives, règles propres au porteur, rien ne fuit d'un combat) ;
// - déterminisme et rejeu : Lot81PermanentModifierAggregationTests ;
// - objet « rules » du journal : Lot81CombatantRulesLogTests.
// Les conditions vraies et fausses (X2), les débordements refusés, l'absence de mutation partielle et la non-régression des
// scénarios livrés relèvent d'autres fichiers (Lot81ReferenceTests, Lot81EngineBaselineTests, …).
//
// Oracle principal : l'équivalence. Un combat où le personnage porte « clé +x » avec la règle de base B produit exactement le même
// journal que le même combat sans modificateur avec la règle de base B + x (ligne « combatant » comparée sans son champ « rules »,
// qui diffère par construction). Chaque équivalence vérifie aussi que le scénario lit bien la règle : sans modificateur ni règle
// modifiée, le journal diffère.

/// <summary>Oracle d'équivalence et combats d'essai partagés par les tests des modificateurs permanents.</summary>
file static class X1
{
    public static readonly CombatRules Base = CombatRules.Provisional;

    public static Dictionary<string, int> Alpha(int rank) => new() { [TalentAlpha] = rank };

    /// <summary>Classe d'essai dont les définitions nommées portent les modificateurs (second passif présent).</summary>
    public static ClassDefinition With(params (string Owner, ModifierDefinition[] Modifiers)[] owners) =>
        Class(modifiers: owners.ToDictionary(o => o.Owner, o => o.Modifiers), secondPassive: true);

    public static ClassDefinition OnAlpha(params ModifierDefinition[] modifiers) => With((TalentAlpha, modifiers));

    public static ClassDefinition OnPassive(params ModifierDefinition[] modifiers) => With((Passif, modifiers));

    /// <summary>Classe d'essai sans modificateur, au même jeu de compétences (second passif compris) : l'en-tête est identique.</summary>
    public static readonly ClassDefinition Plain = Class(secondPassive: true);

    /// <summary>Personnage résistant (2100 PV, 100 d'endurance), Force 14 (puissance physique 28), Volonté 10 (résistance 20).</summary>
    public static CharacterSetup Bearer(ClassDefinition definition, IReadOnlyDictionary<string, int>? talents = null, string[]? passives = null,
        string[]? actives = null, Attributes? attributes = null, int level = 10, string id = "bastion") =>
        Character(id, attributes: attributes ?? Attrs(vitalite: 200), actives: actives, definition: definition, talents: talents,
            passives: passives, level: level);

    public static IReadOnlyList<string> Lines(CombatSetup setup) => Run(setup).Log.Lines;

    public static List<JsonElement> EventsOf(IReadOnlyList<string> lines) =>
        lines.Skip(1).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();

    /// <summary>Ligne comparable : la ligne « combatant » perd son champ « rules », toute autre ligne reste octet pour octet.</summary>
    public static string WithoutRules(string line)
    {
        JsonObject o = JsonNode.Parse(line)!.AsObject();
        if (o["event"]?.GetValue<string>() != "combatant") return line;
        o.Remove("rules");
        return o.ToJsonString();
    }

    /// <summary>En-tête sans champs volatils (<see cref="Lot8LogFields.StableHeader"/>) ni builds, quand les builds diffèrent.</summary>
    public static string HeaderWithoutBuilds(string header)
    {
        JsonObject o = JsonNode.Parse(Lot8LogFields.StableHeader(header))!.AsObject();
        foreach (JsonNode? c in o["scenario"]!["combatants"]!.AsArray()) c!.AsObject().Remove("build");
        return o.ToJsonString();
    }

    /// <summary>
    /// Oracle d'équivalence : mêmes lignes (après l'en-tête, « rules » retiré de la ligne « combatant ») ; en-tête identique octet pour
    /// octet, ou sans les builds quand ils diffèrent (points d'attribut). <paramref name="neutral"/> : le même combat sans modificateur
    /// ni règle modifiée, dont le journal doit différer (le scénario lit la règle).
    /// </summary>
    public static (IReadOnlyList<string> Modified, IReadOnlyList<string> Reference) AssertEquivalent(CombatSetup modified, CombatSetup reference,
        CombatSetup? neutral, bool buildsDiffer = false)
    {
        IReadOnlyList<string> a = Lines(modified), b = Lines(reference);
        Assert.Equal(b.Count, a.Count);
        if (buildsDiffer) Assert.Equal(HeaderWithoutBuilds(b[0]), HeaderWithoutBuilds(a[0]));
        else Assert.Equal(b[0], a[0]);
        for (int i = 1; i < a.Count; i++) Assert.Equal(WithoutRules(b[i]), WithoutRules(a[i]));
        if (neutral != null)
            Assert.False(Lines(neutral).Skip(1).Select(WithoutRules).SequenceEqual(a.Skip(1).Select(WithoutRules)),
                "scénario insensible à la règle : le journal sans modificateur est le même");
        return (a, b);
    }

    public static JsonElement CombatantLine(IReadOnlyList<string> lines, string id = "bastion") =>
        EventsOf(lines).OfKind("combatant").Single(e => e.Str("combatant") == id);

    /// <summary>Objet « rules » de la ligne « combatant », texte brut tel qu'écrit par le moteur.</summary>
    public static string RulesJson(IReadOnlyList<string> lines, string id = "bastion") => CombatantLine(lines, id).GetProperty("rules").GetRawText();

    /// <summary>Objet « rules » attendu : règles de base de <paramref name="rules"/>, puis les clés paramétrées données, avant « conditional ».</summary>
    public static string ExpectedRules(CombatRules rules, string parameterized = "") =>
        Lot8LogFields.BaseRulesJson(rules).Replace(",\"conditional\":[]", parameterized + ",\"conditional\":[]");

    /// <summary>
    /// Combat mêlé qui lit chaque règle X1 du moteur : frappes (coefficient, recharge, Résonance par impact direct), Recharge
    /// (Charges max), garde (réduction, coût de blocage, entretien), parades ratée puis retentées (fenêtre, récupération), dépenses
    /// puis régénération. Assaillant : 100 dégâts bruts chaque seconde dès 1 s.
    /// </summary>
    public static CombatSetup Mixed(ClassDefinition definition, CombatRules rules, IReadOnlyDictionary<string, int>? talents = null,
        string[]? passives = null, int level = 10)
    {
        var commands = new[]
        {
            Cmd(0, Frappe, target: "assaillant"), Cmd(1000, Recharge, target: "assaillant"), Cmd(2000, Recharge, target: "assaillant"),
            GuardStart(2000), GuardStop(4000), Parry(4200), Parry(4800), Parry(4950), Cmd(5000, Frappe, target: "assaillant"),
            Cmd(6000, Frappe, target: "assaillant"), Cmd(7000, Gratuite, target: "assaillant"), Cmd(8000, Heurt, target: "assaillant"),
        };
        return Setup(commands, new CombatantSetup[]
        {
            Bearer(definition, talents, passives, new[] { Frappe, Recharge, Gratuite, Heurt }, level: level), Assailant(1000, 1000),
        }, durationMs: 12_000, rules: rules);
    }
}

/// <summary>
/// Sous-lot 8.1 (X1, D4) : chaque clé permanente lue par le moteur, au rang 1 et au rang maximal d'un talent (valeur par rang),
/// équivaut à la règle de base (ou au coefficient, à la recharge, aux points d'attribut) augmentée de la même valeur ; valeurs exactes
/// vérifiées à la main (montants, ticks, champs du journal), y compris pour les taux fractionnaires d'entretien et de régénération.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Engine")]
public class Lot81PermanentModifierEquivalenceTests
{
    // --- Garde -------------------------------------------------------------------------------

    [Theory]
    [InlineData(1, "0.62", 38)]   // 0,6 + 0,02 ; 100 × (1 − 0,62) = 38
    [InlineData(5, "0.7", 30)]    // 0,6 + 0,02 + 4 × 0,02 ; 100 × 0,3 = 30
    public void Reduction_de_garde_equivalente_a_la_regle_de_base(int rank, string reduction, long blockedDamage)
    {
        ClassDefinition modified = X1.OnAlpha(Mod("guard.damageReduction", "0.02", "0.02"));
        var reference = new CombatRules { GuardDamageReduction = Fixed.Parse(reduction) };
        CombatSetup Fight(ClassDefinition c, CombatRules r) =>
            Setup(new[] { GuardStart(0) }, new CombatantSetup[] { X1.Bearer(c, X1.Alpha(rank)), Assailant(1000, 1000) }, rules: r);

        var (lines, _) = X1.AssertEquivalent(Fight(modified, X1.Base), Fight(X1.Plain, reference), Fight(X1.Plain, X1.Base));

        JsonElement first = X1.EventsOf(lines).OfKind("damage").First(e => e.Str("target") == "bastion");
        Assert.Equal((20L, "guard", blockedDamage), (first.Tick(), first.Str("defense")!, first.Int("amount")));
        Assert.Equal(X1.ExpectedRules(reference), X1.RulesJson(lines));
    }

    [Theory]
    [InlineData(1, 3)]   // 2 + 1
    [InlineData(5, 7)]   // 2 + 1 + 4 × 1
    public void Entretien_entier_equivalent_a_la_regle_de_base(int rank, int perSecond)
    {
        // Garde tenue 10 s sans impact : ⌊ taux × 200 ticks × 50 / 1000 ⌋ = 10 × taux au total.
        ClassDefinition modified = X1.OnAlpha(Mod("guard.upkeepPerSecond", "1", "1"));
        var reference = new CombatRules { GuardUpkeepPerSecond = perSecond };
        CombatSetup Fight(ClassDefinition c, CombatRules r) =>
            Setup(new[] { GuardStart(0) }, new CombatantSetup[] { X1.Bearer(c, X1.Alpha(rank)), Assailant(firstAttackMs: 20_000) }, rules: r);

        var (lines, _) = X1.AssertEquivalent(Fight(modified, X1.Base), Fight(X1.Plain, reference), Fight(X1.Plain, X1.Base));

        var upkeep = X1.EventsOf(lines).OfKind("guardUpkeep").ToList();
        Assert.Equal(10L * perSecond, upkeep.Sum(e => e.Int("amount")));
        Assert.Equal(100 - 10L * perSecond, upkeep.Last().Int("endurance"));
        Assert.Equal(X1.ExpectedRules(reference), X1.RulesJson(lines));
    }

    [Theory]
    [InlineData(1, "2.1", 21_000L, 21L)]   // 2 + 0,1 ; ⌊2,1 × 200 × 50 / 1000⌋ = 21
    [InlineData(5, "2.5", 25_000L, 25L)]   // 2 + 0,1 + 4 × 0,1 ; ⌊2,5 × 200 × 50 / 1000⌋ = 25
    public void Entretien_fractionnaire_cumule_sans_derive(int rank, string rate, long rateTenThousandths, long total)
    {
        // Aucune règle de base décimale : vérification à la main. Garde dès le tick 0, donc N = tick + 1 ticks gardés ; dû cumulé
        // ⌊ taux × N × 50 / 1000 ⌋ (taux en dix-millièmes, entiers exacts), une ligne à chaque point dû de plus.
        ClassDefinition modified = X1.OnAlpha(Mod("guard.upkeepPerSecond", "0.1", "0.1"));
        IReadOnlyList<string> lines = X1.Lines(Setup(new[] { GuardStart(0) },
            new CombatantSetup[] { X1.Bearer(modified, X1.Alpha(rank)), Assailant(firstAttackMs: 20_000) }));

        var expected = new List<(long Tick, long Amount)>();
        long paid = 0;
        for (long n = 1; n <= 200; n++)
        {
            long due = rateTenThousandths * n * 50 / (1000 * 10_000);
            if (due > paid) expected.Add((n - 1, due - paid));
            paid = due;
        }
        var upkeep = X1.EventsOf(lines).OfKind("guardUpkeep").ToList();
        Assert.Equal(expected, upkeep.Select(e => (e.Tick(), e.Int("amount"))));
        Assert.Equal(total, paid);
        Assert.Equal(100 - total, upkeep.Last().Int("endurance"));
        if (rank == 5)   // 2,5/s : un point tous les 8 ticks gardés, aux ticks 7, 15, …, 199
            Assert.Equal(Enumerable.Range(1, 25).Select(i => 8L * i - 1), upkeep.Select(e => e.Tick()));
        Assert.Contains($"\"guard.upkeepPerSecond\":{rate},", X1.RulesJson(lines));
    }

    [Theory]
    [InlineData("0.5", 25_000L, 16L)]    // 2,5/s : ⌊2,5 × 133 / 20⌋ = ⌊16,625⌋ = 16 (par garde : 2 + 1 + 12 = 15 seulement)
    [InlineData("-0.5", 15_000L, 9L)]    // 1,5/s : ⌊1,5 × 133 / 20⌋ = ⌊9,975⌋ = 9
    public void Entretien_fractionnaire_cumule_sur_plusieurs_gardes(string add, long rateTenThousandths, long total)
    {
        // Trois gardes : ticks 0 à 19, 40 à 52 et 100 à 199, soit 133 ticks gardés. Le dû se calcule sur le cumul des ticks gardés
        // du combat (M01), pas garde par garde : le reliquat fractionnaire n'est jamais perdu entre deux gardes.
        var commands = new[] { GuardStart(0), GuardStop(1000), GuardStart(2000), GuardStop(2650), GuardStart(5000) };
        IReadOnlyList<string> lines = X1.Lines(Setup(commands, new CombatantSetup[]
        {
            X1.Bearer(X1.OnPassive(Mod("guard.upkeepPerSecond", add)), passives: new[] { Passif }), Assailant(firstAttackMs: 20_000),
        }));

        long[] guarded = Enumerable.Range(0, 20).Concat(Enumerable.Range(40, 13)).Concat(Enumerable.Range(100, 100)).Select(t => (long)t).ToArray();
        var expected = new List<(long Tick, long Amount)>();
        long paid = 0;
        for (int k = 0; k < guarded.Length; k++)
        {
            long due = rateTenThousandths * (k + 1) * 50 / (1000 * 10_000);
            if (due > paid) expected.Add((guarded[k], due - paid));
            paid = due;
        }
        var upkeep = X1.EventsOf(lines).OfKind("guardUpkeep").ToList();
        Assert.Equal(expected, upkeep.Select(e => (e.Tick(), e.Int("amount"))));
        Assert.Equal(total, upkeep.Sum(e => e.Int("amount")));
    }

    [Theory]
    [InlineData(1, 15)]   // 10 + 5
    [InlineData(5, 35)]   // 10 + 5 + 4 × 5
    public void Cout_de_blocage_equivalent_a_la_regle_de_base(int rank, int cost)
    {
        // Sans entretien, un impact par seconde dès 1 s, combat de 6 s : impacts aux ticks 20, 40, 60, 80 et 100.
        ClassDefinition modified = X1.OnAlpha(Mod("guard.costPerBlock", "5", "5"));
        var reference = new CombatRules { GuardUpkeepPerSecond = 0, GuardCostPerBlock = cost };
        CombatSetup Fight(ClassDefinition c, CombatRules r) =>
            Setup(new[] { GuardStart(0) }, new CombatantSetup[] { X1.Bearer(c, X1.Alpha(rank)), Assailant(1000, 1000) }, durationMs: 6000, rules: r);

        var (lines, _) = X1.AssertEquivalent(Fight(modified, NoUpkeep), Fight(X1.Plain, reference), Fight(X1.Plain, NoUpkeep));

        var events = X1.EventsOf(lines);
        var blocked = events.OfKind("blocked").ToList();
        Assert.All(blocked, e => Assert.Equal(cost, e.Int("enduranceCost")));
        if (cost == 15)
        {
            // 100 → 85 → 70 → 55 → 40 → 25 : cinq blocages, garde tenue.
            Assert.Equal(new long[] { 85, 70, 55, 40, 25 }, blocked.Select(e => e.Int("endurance")));
            Assert.Empty(events.OfKind("guardBroken"));
        }
        else
        {
            // 100 → 65 → 30, puis 30 < 35 : rupture au troisième impact (tick 60).
            Assert.Equal(new long[] { 65, 30 }, blocked.Select(e => e.Int("endurance")));
            JsonElement broken = Assert.Single(events.OfKind("guardBroken"));
            Assert.Equal((60L, "insufficientEndurance"), (broken.Tick(), broken.Str("reason")!));
        }
        Assert.Equal(X1.ExpectedRules(reference), X1.RulesJson(lines));
    }

    // --- Parade -------------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 4)]   // 150 + 50 = 200 ms = 4 ticks
    [InlineData(5, 8)]   // 150 + 50 + 4 × 50 = 400 ms = 8 ticks
    public void Fenetre_de_parade_equivalente_a_la_regle_de_base(int rank, long windowTicks)
    {
        // Parade au tick 17, impact au tick 20 : hors de la fenêtre de base [17, 20[, dans la fenêtre élargie ; parade au tick 60
        // avec l'impact du même tick, réussie dans tous les cas.
        ClassDefinition modified = X1.OnAlpha(Mod("parry.windowMs", "50", "50"));
        var reference = new CombatRules { ParryWindowMs = windowTicks * 50 };
        CombatSetup Fight(ClassDefinition c, CombatRules r) =>
            Setup(new[] { Parry(850), Parry(3000) }, new CombatantSetup[] { X1.Bearer(c, X1.Alpha(rank)), Assailant(1000, 1000) }, durationMs: 5000, rules: r);

        var (lines, _) = X1.AssertEquivalent(Fight(modified, X1.Base), Fight(X1.Plain, reference), Fight(X1.Plain, X1.Base));

        var events = X1.EventsOf(lines);
        Assert.Equal(new[] { 17 + windowTicks, 60 + windowTicks }, events.OfKind("parryStarted").Select(e => e.Int("windowEndTick")));
        Assert.Equal(new long[] { 20, 60 }, events.OfKind("parrySucceeded").Select(e => e.Tick()));
        JsonElement first = events.OfKind("damage").First();
        Assert.Equal((20L, "parry", 10L), (first.Tick(), first.Str("defense")!, first.Int("amount")));   // 100 × (1 − 0,9)
        Assert.Equal(X1.ExpectedRules(reference), X1.RulesJson(lines));
    }

    [Theory]
    [InlineData(1, 12)]   // 500 + 100 = 600 ms = 12 ticks
    [InlineData(5, 20)]   // 500 + 100 + 4 × 100 = 1000 ms = 20 ticks
    public void Recuperation_de_parade_ratee_equivalente_a_la_regle_de_base(int rank, long recoveryTicks)
    {
        // Une parade commandée à chaque tick de 0 à 40, sans impact : chaque fenêtre (3 ticks) échoue ; la suivante n'est acceptée
        // qu'à la fin de la récupération, comptée depuis la fin de la fenêtre.
        ClassDefinition modified = X1.OnAlpha(Mod("parry.failureRecoveryMs", "100", "100"));
        var reference = new CombatRules { ParryFailureRecoveryMs = recoveryTicks * 50 };
        var commands = Enumerable.Range(0, 41).Select(i => Parry(i * 50L)).ToArray();
        CombatSetup Fight(ClassDefinition c, CombatRules r) =>
            Setup(commands, new CombatantSetup[] { X1.Bearer(c, X1.Alpha(rank)), Assailant(firstAttackMs: 20_000) }, durationMs: 3000, rules: r);

        var (lines, _) = X1.AssertEquivalent(Fight(modified, X1.Base), Fight(X1.Plain, reference), Fight(X1.Plain, X1.Base));

        var starts = new List<long>();
        for (long t = 0; t <= 40; t += 3 + recoveryTicks) starts.Add(t);
        var events = X1.EventsOf(lines);
        Assert.Equal(starts, events.OfKind("parryStarted").Select(e => e.Tick()));
        Assert.Equal(starts.Select(t => t + 3 + recoveryTicks), events.OfKind("parryEnded").Select(e => e.Int("recoveryUntilTick")));
        Assert.Equal(X1.ExpectedRules(reference), X1.RulesJson(lines));
    }

    // --- Ressources ---------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 6, new long[] { 3, 6, 6, 6, 6 })]     // 5 + 1
    [InlineData(5, 10, new long[] { 3, 6, 9, 10, 10 })]  // 5 + 1 + 4 × 1
    public void Charges_maximales_equivalentes_a_la_regle_de_base(int rank, int max, long[] values)
    {
        // Cinq Recharge (+3 Charges chacune) à une seconde d'intervalle : 15 Charges produites, l'excédent au-delà du maximum perdu.
        ClassDefinition modified = X1.OnAlpha(Mod("rampartCharges.max", "1", "1"));
        var reference = new CombatRules { RampartChargesMax = max };
        var commands = Enumerable.Range(0, 5).Select(i => Cmd(i * 1000L, Recharge)).ToArray();
        CombatSetup Fight(ClassDefinition c, CombatRules r) => Setup(commands, new CombatantSetup[]
        {
            X1.Bearer(c, X1.Alpha(rank), actives: new[] { Recharge, Frappe, Heurt, Gratuite }), Dummy(),
        }, durationMs: 6000, rules: r);

        var (lines, _) = X1.AssertEquivalent(Fight(modified, X1.Base), Fight(X1.Plain, reference), Fight(X1.Plain, X1.Base));

        var events = X1.EventsOf(lines);
        var gains = events.OfKind("resourceGained").Where(e => e.Str("resource") == "rampartCharges").ToList();
        Assert.Equal(values, gains.Select(e => e.Int("value")));
        Assert.Equal(15L - max, gains.Sum(e => e.Int("wasted")));
        Assert.Equal(max, events.OfKind("combatantSummary").Single(e => e.Str("combatant") == "bastion").Int("rampartCharges"));
        Assert.Equal(X1.ExpectedRules(reference), X1.RulesJson(lines));
    }

    [Theory]
    [InlineData(1, 4)]   // 3 + 1
    [InlineData(5, 8)]   // 3 + 1 + 4 × 1
    public void Resonance_par_impact_direct_equivalente_a_la_regle_de_base(int rank, int perHit)
    {
        // Dix Gratuite (une par seconde, recharge globale de 1 s) sur le mannequin : dix gains « directHit ».
        ClassDefinition modified = X1.OnAlpha(Mod("resonance.perDirectHit", "1", "1"));
        var reference = new CombatRules { ResonancePerDirectHit = perHit };
        var commands = Enumerable.Range(0, 10).Select(i => Cmd(i * 1000L, Gratuite)).ToArray();
        CombatSetup Fight(ClassDefinition c, CombatRules r) =>
            Setup(commands, new CombatantSetup[] { X1.Bearer(c, X1.Alpha(rank)), Dummy() }, rules: r);

        var (lines, _) = X1.AssertEquivalent(Fight(modified, X1.Base), Fight(X1.Plain, reference), Fight(X1.Plain, X1.Base));

        var gains = X1.EventsOf(lines).OfKind("resourceGained").Where(e => e.Str("resource") == "resonance").ToList();
        Assert.All(gains, e => Assert.Equal("directHit", e.Str("source")));
        Assert.Equal(Enumerable.Repeat((long)perHit, 10), gains.Select(e => e.Int("amount")));
        Assert.Equal(10L * perHit, gains.Last().Int("value"));
        Assert.Equal(X1.ExpectedRules(reference), X1.RulesJson(lines));
    }

    /// <summary>Frappe (10), Heurt (15) et Onde (5) aux ticks 0, 20 et 40 : 30 d'endurance dépensés, régénération dès le tick 60.</summary>
    private static CombatSetup Spender(ClassDefinition c, CombatRules r, int rank) =>
        Setup(new[] { Cmd(0, Frappe), Cmd(1000, Heurt), Cmd(2000, Onde, target: null) },
            new CombatantSetup[] { X1.Bearer(c, X1.Alpha(rank)), Dummy() }, rules: r);

    [Theory]
    [InlineData(1, 12, 61L, 109L)]   // 10 + 2 = 12/s : 0,6 par tick ; 30 points au 50ᵉ tick régénéré (tick 60 + 49)
    [InlineData(5, 20, 60L, 89L)]    // 10 + 2 + 4 × 2 = 20/s : 1 par tick ; 30 points au 30ᵉ tick régénéré (tick 89)
    public void Regeneration_entiere_equivalente_a_la_regle_de_base(int rank, int perSecond, long firstTick, long fullTick)
    {
        ClassDefinition modified = X1.OnAlpha(Mod("endurance.regenPerSecond", "2", "2"));
        var reference = new CombatRules { EnduranceRegenPerSecond = perSecond };

        var (lines, _) = X1.AssertEquivalent(Spender(modified, X1.Base, rank), Spender(X1.Plain, reference, rank), Spender(X1.Plain, X1.Base, rank));

        var regen = X1.EventsOf(lines).OfKind("enduranceRegenerated").ToList();
        Assert.Equal(30, regen.Sum(e => e.Int("amount")));
        Assert.Equal((firstTick, fullTick, 100L), (regen[0].Tick(), regen.Last().Tick(), regen.Last().Int("endurance")));
        Assert.Equal(X1.ExpectedRules(reference), X1.RulesJson(lines));
    }

    [Theory]
    [InlineData(1, "10.1", 101_000L, 119L)]   // 10 + 0,1 ; ⌊0,505 × 59⌋ = 29, ⌊0,505 × 60⌋ = 30 : plein au tick 60 + 59
    [InlineData(5, "10.5", 105_000L, 117L)]   // 10 + 0,1 + 4 × 0,1 ; ⌊0,525 × 57⌋ = 29, ⌊0,525 × 58⌋ = 30 : plein au tick 117
    public void Regeneration_fractionnaire_cumulee_sans_derive(int rank, string rate, long rateTenThousandths, long fullTick)
    {
        // Accordé cumulé après N ticks régénérés (dès le tick 60) : ⌊ taux × N × 50 / 1000 ⌋, plafonné aux 30 points dépensés.
        ClassDefinition modified = X1.OnAlpha(Mod("endurance.regenPerSecond", "0.1", "0.1"));
        IReadOnlyList<string> lines = X1.Lines(Spender(modified, X1.Base, rank));

        var expected = new List<(long Tick, long Amount)>();
        long granted = 0;
        for (long n = 1; granted < 30; n++)
        {
            long target = Math.Min(30, rateTenThousandths * n * 50 / (1000 * 10_000));
            if (target > granted) expected.Add((59 + n, target - granted));
            granted = target;
        }
        var regen = X1.EventsOf(lines).OfKind("enduranceRegenerated").ToList();
        Assert.Equal(expected, regen.Select(e => (e.Tick(), e.Int("amount"))));
        Assert.Equal((fullTick, 100L), (regen.Last().Tick(), regen.Last().Int("endurance")));
        Assert.Contains($"\"endurance.regenPerSecond\":{rate},", X1.RulesJson(lines));
    }

    // --- Statistiques -------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 5, 110L)]    // +10 = 2 × 5 points d'Endurance
    [InlineData(5, 25, 150L)]   // +10 + 4 × 10 = +50 = 2 × 25 points
    public void Endurance_maximale_equivalente_aux_points_d_Endurance(int rank, int points, long maxEndurance)
    {
        // Garde de 0 à 4 s (blocages, entretien), frappes, puis régénération jusqu'au plein : le maximum plafonne la régénération.
        ClassDefinition modified = X1.OnAlpha(Mod("endurance.max", "10", "10"));
        var commands = new[] { GuardStart(0), GuardStop(4000), Cmd(4000, Frappe, target: "assaillant"), Cmd(5000, Heurt, target: "assaillant") };
        CombatSetup Fight(ClassDefinition c, int endurance) => Setup(commands, new CombatantSetup[]
        {
            X1.Bearer(c, X1.Alpha(rank), attributes: Attrs(vitalite: 200, endurance: endurance)), Assailant(1000, 1000),
        }, durationMs: 20_000);

        var (lines, _) = X1.AssertEquivalent(Fight(modified, 0), Fight(X1.Plain, points), Fight(X1.Plain, 0), buildsDiffer: true);

        JsonElement line = X1.CombatantLine(lines);
        Assert.Equal((maxEndurance, maxEndurance), (line.Int("maxEndurance"), line.Int("endurance")));   // 100 + valeur
        JsonElement summary = X1.EventsOf(lines).OfKind("combatantSummary").Single(e => e.Str("combatant") == "bastion");
        Assert.Equal(maxEndurance, summary.Int("endurance"));   // plein à la fin
        Assert.Equal(X1.ExpectedRules(X1.Base), X1.RulesJson(lines));   // statistique : hors de l'objet « rules »
    }

    [Theory]
    [InlineData(1, 20, 300L, 60L)]    // 240 × (1 + 0,25) = 300 = 100 + 10 × 20 ; mort au 3ᵉ impact (tick 60)
    [InlineData(5, 44, 540L, 120L)]   // 240 × (1 + 1,25) = 540 = 100 + 10 × 44 ; mort au 6ᵉ impact (tick 120)
    public void PV_max_en_pourcentage_equivalents_aux_points_de_Vitalite(int rank, int vitalite, long maxHealth, long deathTick)
    {
        // Vitalité 14 : 100 + 10 × 14 = 240 PV. Impacts de 100 chaque seconde, sans défense.
        ClassDefinition modified = X1.OnAlpha(Mod("maxHealth", "0.25", "0.25"));
        CombatSetup Fight(ClassDefinition c, int vit) => Setup(Array.Empty<CombatCommand>(), new CombatantSetup[]
        {
            X1.Bearer(c, X1.Alpha(rank), attributes: Attrs(vitalite: vit)), Assailant(1000, 1000),
        }, durationMs: 10_000);

        var (lines, _) = X1.AssertEquivalent(Fight(modified, 14), Fight(X1.Plain, vitalite), Fight(X1.Plain, 14), buildsDiffer: true);

        JsonElement line = X1.CombatantLine(lines);
        Assert.Equal((maxHealth, maxHealth), (line.Int("maxHealth"), line.Int("health")));
        Assert.Equal(deathTick, Assert.Single(X1.EventsOf(lines).OfKind("died")).Tick());
    }

    [Theory]
    [InlineData(1, 244L)]   // 240 × (1 + 0,02) = 244,8 → plancher 244
    [InlineData(5, 264L)]   // 240 × (1 + 0,02 + 4 × 0,02) = 264
    public void PV_max_en_pourcentage_arrondis_par_plancher(int rank, long maxHealth)
    {
        ClassDefinition modified = X1.OnAlpha(Mod("maxHealth", "0.02", "0.02"));
        IReadOnlyList<string> lines = X1.Lines(Setup(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { X1.Bearer(modified, X1.Alpha(rank), attributes: Attrs()), Dummy() }));
        JsonElement line = X1.CombatantLine(lines);
        Assert.Equal((maxHealth, maxHealth), (line.Int("maxHealth"), line.Int("health")));
    }

    [Theory]
    [InlineData(1, 50, "120", 26L)]    // 20 + 100 = 2 × (10 + 50) ; facteur ⌊10⁴ × 1000 / 1120⌋ = 8928 ; ⌊30 × 0,8928⌋ = 26
    [InlineData(5, 250, "520", 19L)]   // 20 + 100 + 4 × 100 = 2 × (10 + 250) ; facteur ⌊10⁴ × 1000 / 1520⌋ = 6578 ; ⌊30 × 0,6578⌋ = 19
    public void Resistance_aux_alterations_equivalente_aux_points_de_Volonte(int rank, int volonte, string resistance, long stunTicks)
    {
        // Étourdissement de 1,5 s (30 ticks) appliqué au tick 20, réduit par le facteur K / (K + résistance), K = 1000.
        ClassDefinition modified = X1.OnAlpha(Mod("statusResistance", "100", "100"));
        var assailant = Assailant(1000, 2500, attacks: new[] { Strike("assommoir", 10, applies: Apply("stun")) });
        CombatSetup Fight(ClassDefinition c, int extraVolonte) => Setup(Array.Empty<CombatCommand>(), new CombatantSetup[]
        {
            X1.Bearer(c, X1.Alpha(rank), attributes: new Attributes(14, 8, 6, 200, 10 + extraVolonte, 0)), assailant,
        }, durationMs: 10_000);

        var (lines, _) = X1.AssertEquivalent(Fight(modified, 0), Fight(X1.Plain, volonte), Fight(X1.Plain, 0), buildsDiffer: true);

        Assert.Equal(resistance, X1.CombatantLine(lines).GetProperty("statusResistance").GetRawText());
        JsonElement stun = X1.EventsOf(lines).OfKind("stateApplied").First(e => e.Str("state") == "stun");
        Assert.Equal((20L, 20 + stunTicks), (stun.Tick(), stun.Int("untilTick")));
    }

    // --- Compétences --------------------------------------------------------------------------

    [Theory]
    [InlineData(1, "1.3", 36L, 55L)]   // 1,2 + 0,1 ; 28 × 1,3 = 36,4 → 36 ; critique × 1,5 : 54,6 → 55
    [InlineData(5, "1.7", 48L, 71L)]   // 1,2 + 0,1 + 4 × 0,1 ; 28 × 1,7 = 47,6 → 48 ; critique : 71,4 → 71
    public void Coefficient_de_competence_equivalent_au_coefficient_de_la_classe(int rank, string coefficient, long damage, long critical)
    {
        // Frappe aux ticks 0, 120 et 240 sur un mannequin sans armure (puissance physique 2 × 14 = 28).
        ClassDefinition modified = X1.OnAlpha(Mod(Frappe + ".coefficient", "0.1", "0.1"));
        ClassDefinition reference = Class(frappeCoefficient: coefficient, secondPassive: true);
        CombatSetup Fight(ClassDefinition c) => Setup(new[] { Cmd(0, Frappe), Cmd(6000, Frappe), Cmd(12_000, Frappe) },
            new CombatantSetup[] { X1.Bearer(c, X1.Alpha(rank)), Dummy(armor: 0) }, durationMs: 15_000);

        var (lines, _) = X1.AssertEquivalent(Fight(modified), Fight(reference), Fight(X1.Plain));

        var hits = X1.EventsOf(lines).OfKind("damage").ToList();
        Assert.Equal(3, hits.Count);
        Assert.All(hits, e => Assert.Equal(e.GetProperty("critical").GetBoolean() ? critical : damage, e.Int("amount")));
        Assert.Equal(X1.ExpectedRules(X1.Base, $",\"skill.essai.frappe.coefficient\":{coefficient}"), X1.RulesJson(lines));
    }

    [Theory]
    [InlineData(1, 5000L, new long[] { 0, 100 })]                                  // 6000 − 1000
    [InlineData(5, 1000L, new long[] { 0, 20, 40, 60, 80, 100, 120, 140, 160, 180 })]   // 6000 − 1000 − 4 × 1000
    public void Recharge_de_competence_equivalente_a_la_recharge_de_la_classe(int rank, long cooldownMs, long[] castTicks)
    {
        // Frappe commandée chaque seconde pendant 10 s (10 d'endurance par lancer, 100 au total au plus).
        ClassDefinition modified = X1.OnAlpha(Mod(Frappe + ".cooldownMs", "-1000", "-1000"));
        ClassDefinition reference = Class(frappeCooldownMs: cooldownMs, secondPassive: true);
        var commands = Enumerable.Range(0, 10).Select(i => Cmd(i * 1000L, Frappe)).ToArray();
        CombatSetup Fight(ClassDefinition c) => Setup(commands, new CombatantSetup[] { X1.Bearer(c, X1.Alpha(rank)), Dummy() });

        var (lines, _) = X1.AssertEquivalent(Fight(modified), Fight(reference), Fight(X1.Plain));

        var casts = X1.EventsOf(lines).OfKind("skillCast").ToList();
        Assert.Equal(castTicks, casts.Select(e => e.Tick()));
        Assert.All(casts, e => Assert.Equal(e.Tick() + cooldownMs / 50, e.Int("cooldownReadyTick")));
        Assert.Equal(X1.ExpectedRules(X1.Base, $",\"skill.essai.frappe.cooldownMs\":{cooldownMs}"), X1.RulesJson(lines));
    }
}

/// <summary>
/// Sous-lot 8.1 (D2, D10) : écrêtage des valeurs résultantes dans le moteur, aux bornes N−1, N et N+1 : plafond de 75 % de la garde,
/// fenêtre de parade (500 ms ramenés au multiple du tick, plancher d'un tick), Charges max (10), planchers à 0, coefficient [0, 100],
/// recharge [0, 24 h], PV max [−90 %, +1 000 %], endurance et résistance positives ; au-delà d'une borne, journal identique à la borne.
/// Intervalle élargi à une règle de base déjà hors de l'intervalle de jeu (règles de test).
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Engine")]
public class Lot81PermanentModifierClampTests
{
    private static CombatSetup GuardFight(ClassDefinition c, CombatRules r) =>
        Setup(new[] { GuardStart(0) }, new CombatantSetup[] { X1.Bearer(c, passives: new[] { Passif, PassifB }), Assailant(1000, 1000) }, rules: r);

    [Theory]
    [InlineData("0.14", "0.74", 26L)]   // N−1
    [InlineData("0.15", "0.75", 25L)]   // N : plafond B1
    [InlineData("0.16", "0.75", 25L)]   // N+1 : écrêté
    [InlineData("0.75", "0.75", 25L)]   // plus grand ajout déclarable (D10)
    public void Reduction_de_garde_autour_du_plafond_de_75_pourcent(string add, string reduction, long blockedDamage)
    {
        var reference = new CombatRules { GuardDamageReduction = Fixed.Parse(reduction) };
        var (lines, _) = X1.AssertEquivalent(GuardFight(X1.OnPassive(Mod("guard.damageReduction", add)), X1.Base), GuardFight(X1.Plain, reference),
            GuardFight(X1.Plain, X1.Base));
        Assert.Equal(blockedDamage, X1.EventsOf(lines).OfKind("damage").First().Int("amount"));
        Assert.Equal(X1.ExpectedRules(reference), X1.RulesJson(lines));
    }

    [Fact]
    public void Reduction_de_garde_au_plafond_et_au_dela_journaux_identiques()
    {
        // N = 0,75 et N+1 (un passif, ou deux passifs à +0,75 chacun : 2,1 avant écrêtage) : journaux identiques octet pour octet,
        // objet « rules » compris (valeur résultante 0,75).
        IReadOnlyList<string> atCap = X1.Lines(GuardFight(X1.OnPassive(Mod("guard.damageReduction", "0.15")), X1.Base));
        Assert.Equal(atCap, X1.Lines(GuardFight(X1.OnPassive(Mod("guard.damageReduction", "0.16")), X1.Base)));
        Assert.Equal(atCap, X1.Lines(GuardFight(X1.With((Passif, new[] { Mod("guard.damageReduction", "0.75") }),
            (PassifB, new[] { Mod("guard.damageReduction", "0.75") })), X1.Base)));
    }

    [Theory]
    [InlineData("-0.59", "0.01", 99L)]   // N−1
    [InlineData("-0.6", "0", 100L)]      // N : plancher 0
    [InlineData("-0.61", "0", 100L)]     // N+1 : écrêté
    public void Reduction_de_garde_autour_du_plancher_nul(string add, string reduction, long blockedDamage)
    {
        var reference = new CombatRules { GuardDamageReduction = Fixed.Parse(reduction) };
        var (lines, _) = X1.AssertEquivalent(GuardFight(X1.OnPassive(Mod("guard.damageReduction", add)), X1.Base), GuardFight(X1.Plain, reference),
            GuardFight(X1.Plain, X1.Base));
        JsonElement first = X1.EventsOf(lines).OfKind("damage").First();
        Assert.Equal(("guard", blockedDamage), (first.Str("defense")!, first.Int("amount")));   // bloqué (coût payé), sans réduction à 0
        Assert.Equal(X1.ExpectedRules(reference), X1.RulesJson(lines));
    }

    private static CombatSetup ParryFight(ClassDefinition c, CombatRules r, long parryMs = 850) =>
        Setup(new[] { Parry(parryMs) }, new CombatantSetup[] { X1.Bearer(c, passives: new[] { Passif, PassifB }), Assailant(1000, 1000) }, durationMs: 3000, rules: r);

    [Theory]
    [InlineData("300", 450L)]    // N−1 : 150 + 300
    [InlineData("350", 500L)]    // N : plafond de 500 ms
    [InlineData("400", 500L)]    // N+1 : écrêté
    [InlineData("500", 500L)]    // plus grand ajout déclarable (D10)
    [InlineData("-50", 100L)]    // au-dessus du plancher
    [InlineData("-100", 50L)]    // N : plancher d'un tick
    [InlineData("-150", 50L)]    // N+1 : 0 écrêté à un tick (jamais de fenêtre vide)
    public void Fenetre_de_parade_autour_du_plafond_et_du_plancher(string add, long windowMs)
    {
        var reference = new CombatRules { ParryWindowMs = windowMs };
        var (lines, _) = X1.AssertEquivalent(ParryFight(X1.OnPassive(Mod("parry.windowMs", add)), X1.Base), ParryFight(X1.Plain, reference),
            ParryFight(X1.Plain, X1.Base));
        Assert.Equal(17 + windowMs / 50, X1.EventsOf(lines).OfKind("parryStarted").Single().Int("windowEndTick"));
        Assert.Equal(X1.ExpectedRules(reference), X1.RulesJson(lines));
    }

    [Fact]
    public void Fenetre_de_parade_au_plafond_et_au_dela_journaux_identiques()
    {
        IReadOnlyList<string> atCap = X1.Lines(ParryFight(X1.OnPassive(Mod("parry.windowMs", "350")), X1.Base));
        Assert.Equal(atCap, X1.Lines(ParryFight(X1.OnPassive(Mod("parry.windowMs", "400")), X1.Base)));
        Assert.Equal(atCap, X1.Lines(ParryFight(X1.With((Passif, new[] { Mod("parry.windowMs", "500") }), (PassifB, new[] { Mod("parry.windowMs", "500") })),
            X1.Base)));
        IReadOnlyList<string> atFloor = X1.Lines(ParryFight(X1.OnPassive(Mod("parry.windowMs", "-100")), X1.Base));
        Assert.Equal(atFloor, X1.Lines(ParryFight(X1.OnPassive(Mod("parry.windowMs", "-150")), X1.Base)));
    }

    /// <summary>Tick de 40 ms : catalogue provisoire dont l'étourdissement passe à 1,6 s (multiple de 40 ms), le reste inchangé.</summary>
    private static readonly StatusCatalog Tick40Statuses = new(new[]
    {
        new StatusDefinition("burn", StatusCategory.Dot, 6000, StatusStacking.PerSourceKeepStronger, StatusResistanceMode.Defense,
            periodMs: 2000, damageType: DamageType.Magique),
        new StatusDefinition("bleed", StatusCategory.Dot, 6000, StatusStacking.PerSourceKeepStronger, StatusResistanceMode.Defense,
            periodMs: 2000, damageType: DamageType.Physique),
        new StatusDefinition("stun", StatusCategory.Control, 1600, StatusStacking.Ignore, StatusResistanceMode.Duration,
            blocks: StatusBlocks.Skills | StatusBlocks.Guard | StatusBlocks.Parry | StatusBlocks.AssailantAttacks,
            interrupts: StatusInterrupts.Guard | StatusInterrupts.Parry, blockReason: "stunned", grantsOnExpiry: "stunImmunity"),
        new StatusDefinition("stunImmunity", StatusCategory.Immunity, 3000, StatusStacking.Ignore, immuneTo: new[] { "stun" }),
        new StatusDefinition("freeze", StatusCategory.Control, 4000, StatusStacking.KeepLonger, StatusResistanceMode.Duration,
            movementSpeedModifier: Fixed.Parse("-0.3")),
        new StatusDefinition("fragility", StatusCategory.Modifier, 6000, StatusStacking.Refresh, damageTakenIncrease: Fixed.FromRatio(1, 10)),
        new StatusDefinition("guardBreak", StatusCategory.Control, 3000, StatusStacking.Ignore,
            blocks: StatusBlocks.Guard | StatusBlocks.Parry | StatusBlocks.Regeneration, blockReason: "guardBroken"),
    });

    [Theory]
    [InlineData("280", 440L)]   // N−1 : 160 + 280
    [InlineData("320", 480L)]   // N : plafond ⌊500 / 40⌋ × 40 = 480 ms (D10)
    [InlineData("360", 480L)]   // N+1 : 520 écrêté à 480, et non à 500
    [InlineData("400", 480L)]
    public void Fenetre_de_parade_plafonnee_au_multiple_du_tick_inferieur_a_500_ms(string add, long windowMs)
    {
        // Tick de 40 ms : parade au tick 17 (680 ms), impacts aux ticks 25, 50, … ; fenêtre de base 160 ms (4 ticks).
        var rules40 = new CombatRules { TickMilliseconds = 40, ParryWindowMs = 160, ParryFailureRecoveryMs = 480, Statuses = Tick40Statuses };
        var reference = new CombatRules { TickMilliseconds = 40, ParryWindowMs = windowMs, ParryFailureRecoveryMs = 480, Statuses = Tick40Statuses };
        var (lines, _) = X1.AssertEquivalent(ParryFight(X1.OnPassive(Mod("parry.windowMs", add)), rules40, 680), ParryFight(X1.Plain, reference, 680),
            ParryFight(X1.Plain, rules40, 680));
        Assert.Equal(17 + windowMs / 40, X1.EventsOf(lines).OfKind("parryStarted").Single().Int("windowEndTick"));
        Assert.Contains($"\"parry.windowMs\":{windowMs},", X1.RulesJson(lines));
    }

    /// <summary>Recharge (+3 Charges) chaque seconde : 3 × <paramref name="casts"/> Charges produites.</summary>
    private static CombatSetup ChargeFight(ClassDefinition c, CombatRules r, int casts = 5) =>
        Setup(Enumerable.Range(0, casts).Select(i => Cmd(i * 1000L, Recharge)).ToArray(), new CombatantSetup[]
        {
            X1.Bearer(c, passives: new[] { Passif, PassifB }, actives: new[] { Recharge, Frappe, Heurt, Gratuite }), Dummy(),
        }, durationMs: casts * 1000L + 1000, rules: r);

    [Theory]
    [InlineData("4", 9)]     // N−1 : 5 + 4
    [InlineData("5", 10)]    // N : plafond de 10 Charges
    [InlineData("6", 10)]    // N+1 : écrêté
    [InlineData("10", 10)]   // plus grand ajout déclarable (D10)
    [InlineData("-4", 1)]
    [InlineData("-5", 0)]    // N : plancher 0 (toute Charge produite est perdue)
    [InlineData("-6", 0)]    // N+1 : écrêté
    public void Charges_maximales_autour_du_plafond_et_du_plancher(string add, int max)
    {
        var reference = new CombatRules { RampartChargesMax = max };
        var (lines, _) = X1.AssertEquivalent(ChargeFight(X1.OnPassive(Mod("rampartCharges.max", add)), X1.Base), ChargeFight(X1.Plain, reference),
            ChargeFight(X1.Plain, X1.Base));
        var gains = X1.EventsOf(lines).OfKind("resourceGained").Where(e => e.Str("resource") == "rampartCharges").ToList();
        Assert.Equal(Math.Min(15, max), gains.Last().Int("value"));
        Assert.Equal(15L - max, gains.Sum(e => e.Int("wasted")));
        Assert.Equal(X1.ExpectedRules(reference), X1.RulesJson(lines));
    }

    [Fact]
    public void Charges_maximales_au_plafond_et_au_dela_journaux_identiques()
    {
        IReadOnlyList<string> atCap = X1.Lines(ChargeFight(X1.OnPassive(Mod("rampartCharges.max", "5")), X1.Base));
        Assert.Equal(atCap, X1.Lines(ChargeFight(X1.OnPassive(Mod("rampartCharges.max", "6")), X1.Base)));
        Assert.Equal(atCap, X1.Lines(ChargeFight(X1.With((Passif, new[] { Mod("rampartCharges.max", "10") }),
            (PassifB, new[] { Mod("rampartCharges.max", "10") })), X1.Base)));
        IReadOnlyList<string> atFloor = X1.Lines(ChargeFight(X1.OnPassive(Mod("rampartCharges.max", "-5")), X1.Base));
        Assert.Equal(atFloor, X1.Lines(ChargeFight(X1.OnPassive(Mod("rampartCharges.max", "-6")), X1.Base)));
    }

    [Theory]
    [InlineData(20, "1", 20)]      // base 20 hors de l'intervalle [0, 10] : intervalle élargi à 20, l'ajout est écrêté à la base
    [InlineData(20, "-1", 19)]     // une baisse s'applique
    [InlineData(20, "-10", 10)]
    public void Charges_maximales_base_hors_intervalle_de_jeu(int baseMax, string add, int max)
    {
        var rules = new CombatRules { RampartChargesMax = baseMax };
        var reference = new CombatRules { RampartChargesMax = max };
        // Huit Recharge : 24 Charges produites, au-delà de 10 comme de la base.
        var (lines, _) = X1.AssertEquivalent(ChargeFight(X1.OnPassive(Mod("rampartCharges.max", add)), rules, 8), ChargeFight(X1.Plain, reference, 8),
            max == baseMax ? null : ChargeFight(X1.Plain, rules, 8));
        Assert.Equal(max, X1.EventsOf(lines).OfKind("resourceGained").Last(e => e.Str("resource") == "rampartCharges").Int("value"));
        Assert.Equal(X1.ExpectedRules(reference), X1.RulesJson(lines));
    }

    [Theory]
    [InlineData(1000L, "100", 1000L)]   // base 1000 ms au-delà du plafond de 500 : intervalle élargi à la base
    [InlineData(1000L, "-100", 900L)]
    public void Fenetre_de_parade_base_hors_intervalle_de_jeu(long baseWindow, string add, long windowMs)
    {
        var rules = new CombatRules { ParryWindowMs = baseWindow };
        var reference = new CombatRules { ParryWindowMs = windowMs };
        var (lines, _) = X1.AssertEquivalent(ParryFight(X1.OnPassive(Mod("parry.windowMs", add)), rules, 0), ParryFight(X1.Plain, reference, 0),
            windowMs == baseWindow ? null : ParryFight(X1.Plain, rules, 0));
        Assert.Equal(windowMs / 50, X1.EventsOf(lines).OfKind("parryStarted").Single().Int("windowEndTick"));
        Assert.Equal(X1.ExpectedRules(reference), X1.RulesJson(lines));
    }

    [Theory]
    [InlineData("86399450", 86_399_950L)]   // N−1 : 500 + 86 399 450
    [InlineData("86399500", 86_400_000L)]   // N : plafond de 24 h
    [InlineData("86400000", 86_400_000L)]   // N+1 (plus grand ajout déclarable) : écrêté
    public void Recuperation_de_parade_autour_du_plafond_de_24_heures(string add, long recoveryMs)
    {
        // Parade ratée au tick 0 (aucun impact) : fenêtre close au tick 3, récupération jusqu'au tick 3 + récupération / 50.
        var reference = new CombatRules { ParryFailureRecoveryMs = recoveryMs };
        CombatSetup Fight(ClassDefinition c, CombatRules r) => Setup(new[] { Parry(0), Parry(1000) },
            new CombatantSetup[] { X1.Bearer(c, passives: new[] { Passif }), Assailant(firstAttackMs: 20_000) }, durationMs: 2000, rules: r);
        var (lines, _) = X1.AssertEquivalent(Fight(X1.OnPassive(Mod("parry.failureRecoveryMs", add)), X1.Base), Fight(X1.Plain, reference),
            Fight(X1.Plain, X1.Base));
        var events = X1.EventsOf(lines);
        Assert.Equal(3 + recoveryMs / 50, Assert.Single(events.OfKind("parryEnded")).Int("recoveryUntilTick"));
        Assert.Equal("parryRecovery", Assert.Single(events.OfKind("commandRejected")).Str("reason"));
        Assert.Equal(X1.ExpectedRules(reference), X1.RulesJson(lines));
    }

    /// <summary>Règles de base dont la règle visée par la clé vaut 0.</summary>
    private static CombatRules Zeroed(string key) => key switch
    {
        "guard.upkeepPerSecond" => new CombatRules { GuardUpkeepPerSecond = 0 },
        "guard.costPerBlock" => new CombatRules { GuardCostPerBlock = 0 },
        "endurance.regenPerSecond" => new CombatRules { EnduranceRegenPerSecond = 0 },
        "parry.failureRecoveryMs" => new CombatRules { ParryFailureRecoveryMs = 0 },
        "resonance.perDirectHit" => new CombatRules { ResonancePerDirectHit = 0 },
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, null),
    };

    [Theory]
    [InlineData("guard.upkeepPerSecond", "-2", "-2.5")]          // 2/s : N = −2, N+1 = −2,5
    [InlineData("guard.costPerBlock", "-10", "-11")]
    [InlineData("endurance.regenPerSecond", "-10", "-10.5")]
    [InlineData("parry.failureRecoveryMs", "-500", "-550")]
    [InlineData("resonance.perDirectHit", "-3", "-4")]
    public void Plancher_nul_atteint_et_depasse_equivaut_a_la_regle_nulle(string key, string atFloor, string beyond)
    {
        var (lines, _) = X1.AssertEquivalent(X1.Mixed(X1.OnPassive(Mod(key, atFloor)), X1.Base, passives: new[] { Passif }),
            X1.Mixed(X1.Plain, Zeroed(key), passives: new[] { Passif }), X1.Mixed(X1.Plain, X1.Base, passives: new[] { Passif }));
        Assert.Equal(lines, X1.Lines(X1.Mixed(X1.OnPassive(Mod(key, beyond)), X1.Base, passives: new[] { Passif })));
        Assert.Equal(X1.ExpectedRules(Zeroed(key)), X1.RulesJson(lines));
    }

    [Theory]
    [InlineData("-1.1", "0.1")]     // N−1
    [InlineData("-1.2", "0")]       // N : plancher 0
    [InlineData("-1.3", "0")]       // N+1 : écrêté
    [InlineData("98.7", "99.9")]    // N−1
    [InlineData("98.8", "100")]     // N : plafond 100
    [InlineData("98.9", "100")]     // N+1 : écrêté
    public void Coefficient_autour_du_plancher_et_du_plafond(string add, string coefficient)
    {
        CombatSetup Fight(ClassDefinition c) => Setup(new[] { Cmd(0, Frappe), Cmd(6000, Frappe) },
            new CombatantSetup[] { X1.Bearer(c, passives: new[] { Passif }), Dummy(armor: 0) });
        var (lines, _) = X1.AssertEquivalent(Fight(X1.OnPassive(Mod(Frappe + ".coefficient", add))),
            Fight(Class(frappeCoefficient: coefficient, secondPassive: true)), Fight(X1.Plain));
        Assert.Equal(X1.ExpectedRules(X1.Base, $",\"skill.essai.frappe.coefficient\":{coefficient}"), X1.RulesJson(lines));
        if (coefficient == "0") Assert.All(X1.EventsOf(lines).OfKind("damage"), e => Assert.Equal(0, e.Int("amount")));
    }

    [Theory]
    [InlineData("-5950", 50L)]            // N−1
    [InlineData("-6000", 0L)]             // N : plancher 0
    [InlineData("-6050", 0L)]             // N+1 : écrêté
    [InlineData("86393950", 86_399_950L)] // N−1
    [InlineData("86394000", 86_400_000L)] // N : plafond de 24 h
    [InlineData("86400000", 86_400_000L)] // N+1 (plus grand ajout déclarable) : écrêté
    public void Recharge_autour_du_plancher_et_du_plafond(string add, long cooldownMs)
    {
        CombatSetup Fight(ClassDefinition c) => Setup(Enumerable.Range(0, 5).Select(i => Cmd(i * 1000L, Frappe)).ToArray(),
            new CombatantSetup[] { X1.Bearer(c, passives: new[] { Passif }), Dummy() }, durationMs: 6000);
        var (lines, _) = X1.AssertEquivalent(Fight(X1.OnPassive(Mod(Frappe + ".cooldownMs", add))),
            Fight(Class(frappeCooldownMs: cooldownMs, secondPassive: true)), Fight(X1.Plain));
        JsonElement first = X1.EventsOf(lines).OfKind("skillCast").First();
        Assert.Equal(cooldownMs / 50, first.Int("cooldownReadyTick"));
        Assert.Equal(X1.ExpectedRules(X1.Base, $",\"skill.essai.frappe.cooldownMs\":{cooldownMs}"), X1.RulesJson(lines));
    }

    private static IReadOnlyList<string> StatLines(ClassDefinition c, Attributes? attributes = null, params string[] passives) =>
        X1.Lines(Setup(new[] { Cmd(0, Gratuite, target: "assaillant") }, new CombatantSetup[]
        {
            X1.Bearer(c, passives: passives, attributes: attributes ?? Attrs()),
            Assailant(1000, 2500, attacks: new[] { Strike("assommoir", 10, applies: Apply("stun")) }),
        }, durationMs: 5000));

    [Theory]
    [InlineData("-0.5", 120L)]     // 240 × 0,5
    [InlineData("-0.89", 26L)]     // N−1 : 240 × 0,11 = 26,4 → 26
    [InlineData("-0.9", 24L)]      // N : plancher de −90 %
    [InlineData("-0.91", 24L)]     // N+1 : écrêté
    [InlineData("9.9", 2616L)]     // N−1 : 240 × 10,9
    [InlineData("10", 2640L)]      // N : plafond de +1 000 %
    public void PV_max_autour_du_plancher_et_du_plafond(string add, long maxHealth)
    {
        IReadOnlyList<string> lines = StatLines(X1.OnPassive(Mod("maxHealth", add)), null, Passif);
        JsonElement line = X1.CombatantLine(lines);
        Assert.Equal((maxHealth, maxHealth), (line.Int("maxHealth"), line.Int("health")));
    }

    [Theory]
    [InlineData("0.5", 200L)]    // 133,3333 × 0,5 = 66,66665 → 66,6667 (au plus proche) ; 133,3333 + 66,6667 = 200 (et non 199,9999 → 199)
    [InlineData("0.25", 166L)]   // 133,3333 × 0,25 = 33,333325 → 33,3333 ; 166,6666 → 166
    public void PV_max_en_pourcentage_multiplication_arrondie_au_plus_proche_puis_plancher(string add, long maxHealth)
    {
        // PV max calculé non entier (règles de statistiques de test : 133,3333 de base, Vitalité 0).
        var stats = new StatRules { BaseMaxHealth = Fixed.Parse("133.3333") };
        var setup = new CombatSetup(42, 1000, stats, DamageRules.Provisional, ProgressionRules.Provisional, CombatRules.Provisional,
            new CombatantSetup[] { X1.Bearer(X1.OnPassive(Mod("maxHealth", add)), passives: new[] { Passif }, attributes: Attrs(vitalite: 0)), Dummy() },
            Array.Empty<CombatCommand>(), Identity);
        JsonElement line = X1.CombatantLine(X1.Lines(setup));
        Assert.Equal((maxHealth, maxHealth), (line.Int("maxHealth"), line.Int("health")));
    }

    [Theory]
    [InlineData("10", 1L)]   // PV max calculé 10 : 10 × (1 − 0,9) = 1, au moins 1 PV (D10)
    [InlineData("9", 0L)]    // PV max calculé 9 : 0,9 → plancher 0 ; mort relevée à la fin du premier tick
    public void PV_max_au_plancher_sur_un_petit_PV_max_calcule(string baseHealth, long maxHealth)
    {
        var stats = new StatRules { BaseMaxHealth = Fixed.Parse(baseHealth) };
        var setup = new CombatSetup(42, 1000, stats, DamageRules.Provisional, ProgressionRules.Provisional, CombatRules.Provisional,
            new CombatantSetup[] { X1.Bearer(X1.OnPassive(Mod("maxHealth", "-0.9")), passives: new[] { Passif }, attributes: Attrs(vitalite: 0)), Dummy() },
            Array.Empty<CombatCommand>(), Identity);
        IReadOnlyList<string> lines = X1.Lines(setup);
        JsonElement line = X1.CombatantLine(lines);
        Assert.Equal((maxHealth, maxHealth), (line.Int("maxHealth"), line.Int("health")));
        var died = X1.EventsOf(lines).OfKind("died").ToList();
        if (maxHealth == 0) Assert.Equal(0, Assert.Single(died).Tick());
        else Assert.Empty(died);
    }

    [Fact]
    public void PV_max_au_dela_des_bornes_journaux_identiques()
    {
        // N+1 du plancher (−0,91) et du plafond (+10 et +0,5 de deux passifs : 10,5) : journaux identiques à la borne.
        Assert.Equal(StatLines(X1.OnPassive(Mod("maxHealth", "-0.9")), null, Passif, PassifB),
            StatLines(X1.OnPassive(Mod("maxHealth", "-0.91")), null, Passif, PassifB));
        Assert.Equal(StatLines(X1.OnPassive(Mod("maxHealth", "10")), null, Passif, PassifB),
            StatLines(X1.With((Passif, new[] { Mod("maxHealth", "10") }), (PassifB, new[] { Mod("maxHealth", "0.5") })), null, Passif, PassifB));
    }

    [Theory]
    [InlineData("-99", 1L)]    // N−1
    [InlineData("-100", 0L)]   // N : plancher 0
    [InlineData("-101", 0L)]   // N+1 : écrêté (statistique dérivée positive)
    public void Endurance_maximale_autour_du_plancher_nul(string add, long maxEndurance)
    {
        IReadOnlyList<string> lines = StatLines(X1.OnPassive(Mod("endurance.max", add)), null, Passif);
        JsonElement line = X1.CombatantLine(lines);
        Assert.Equal((maxEndurance, maxEndurance), (line.Int("maxEndurance"), line.Int("endurance")));
        if (maxEndurance == 0)
            Assert.Equal(StatLines(X1.OnPassive(Mod("endurance.max", "-100")), null, Passif), lines);
    }

    [Theory]
    [InlineData("-19", "1", 29L)]   // N−1 : facteur ⌊10⁴ × 1000 / 1001⌋ = 9990 ; ⌊30 × 0,999⌋ = 29
    [InlineData("-20", "0", 30L)]   // N : résistance nulle, durée entière
    [InlineData("-21", "0", 30L)]   // N+1 : écrêtée à 0
    [InlineData("100.5", "120.5", 26L)]   // décimale plate : ⌊10¹¹ / 11 205 000⌋ = 8924 ; ⌊30 × 0,8924⌋ = 26
    public void Resistance_aux_alterations_autour_du_plancher_nul(string add, string resistance, long stunTicks)
    {
        IReadOnlyList<string> lines = StatLines(X1.OnPassive(Mod("statusResistance", add)), null, Passif);
        Assert.Equal(resistance, X1.CombatantLine(lines).GetProperty("statusResistance").GetRawText());
        JsonElement stun = X1.EventsOf(lines).OfKind("stateApplied").First(e => e.Str("state") == "stun");
        Assert.Equal(20 + stunTicks, stun.Int("untilTick"));
        if (resistance == "0")
            Assert.Equal(StatLines(X1.OnPassive(Mod("statusResistance", "-20")), null, Passif), lines);
    }
}

/// <summary>
/// Sous-lot 8.1 (D3) : agrégation des modificateurs permanents. Même clé de plusieurs sources (talents à leur rang, passifs) et
/// plusieurs clés à la fois, par équivalence ; ordre des sources sans effet (échanges talent/passif, inversion d'une liste, toutes
/// les permutations sur cinq porteurs) ; immuabilité (copies défensives, règles propres au porteur, aucun état partagé entre combats) ;
/// déterminisme et rejeu.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Engine")]
public class Lot81PermanentModifierAggregationTests
{
    [Fact]
    public void Meme_cle_d_un_talent_et_de_deux_passifs_s_additionne()
    {
        // Talent alpha rang 3 : 1 + 2 × 1 = 3 ; passif : 2 ; second passif : 4. Coût de blocage 10 + 9 = 19.
        ClassDefinition modified = X1.With((TalentAlpha, new[] { Mod("guard.costPerBlock", "1", "1") }),
            (Passif, new[] { Mod("guard.costPerBlock", "2") }), (PassifB, new[] { Mod("guard.costPerBlock", "4") }));
        var reference = new CombatRules { GuardUpkeepPerSecond = 0, GuardCostPerBlock = 19 };
        CombatSetup Fight(ClassDefinition c, CombatRules r) => Setup(new[] { GuardStart(0) }, new CombatantSetup[]
        {
            X1.Bearer(c, X1.Alpha(3), new[] { Passif, PassifB }), Assailant(1000, 1000),
        }, durationMs: 8000, rules: r);

        var (lines, _) = X1.AssertEquivalent(Fight(modified, NoUpkeep), Fight(X1.Plain, reference), Fight(X1.Plain, NoUpkeep));

        var events = X1.EventsOf(lines);
        // 100 → 81 → 62 → 43 → 24 → 5, puis 5 < 19 : rupture au sixième impact (tick 120).
        Assert.Equal(new long[] { 81, 62, 43, 24, 5 }, events.OfKind("blocked").Select(e => e.Int("endurance")));
        Assert.Equal(120, Assert.Single(events.OfKind("guardBroken")).Tick());
        Assert.Equal(X1.ExpectedRules(reference), X1.RulesJson(lines));
    }

    [Fact]
    public void Trois_talents_au_rang_maximal_au_niveau_30()
    {
        // Niveau 30 (29 points) : alpha, bêta et gamma au rang 5, chacun 0,01 + 4 × 0,01 = 0,05 ; 0,6 + 0,15 = 0,75 (plafond atteint).
        var talents = new Dictionary<string, int> { [TalentAlpha] = 5, [TalentBeta] = 5, [TalentGamma] = 5 };
        ClassDefinition modified = X1.With((TalentAlpha, new[] { Mod("guard.damageReduction", "0.01", "0.01") }),
            (TalentBeta, new[] { Mod("guard.damageReduction", "0.01", "0.01") }), (TalentGamma, new[] { Mod("guard.damageReduction", "0.01", "0.01") }));
        var reference = new CombatRules { GuardDamageReduction = Fixed.Parse("0.75") };
        CombatSetup Fight(ClassDefinition c, CombatRules r) => Setup(new[] { GuardStart(0) }, new CombatantSetup[]
        {
            X1.Bearer(c, talents, level: 30), Assailant(1000, 1000),
        }, rules: r);

        var (lines, _) = X1.AssertEquivalent(Fight(modified, X1.Base), Fight(X1.Plain, reference), Fight(X1.Plain, X1.Base));

        Assert.Equal(25, X1.EventsOf(lines).OfKind("damage").First().Int("amount"));   // 100 × 0,25
        Assert.Equal(X1.ExpectedRules(reference), X1.RulesJson(lines));
    }

    [Fact]
    public void Plusieurs_cles_a_la_fois_equivalent_aux_regles_et_a_la_classe_modifiees()
    {
        // Huit clés de règle sur le passif (plafond de 8 par porteur) et deux clés de compétence sur le talent alpha au rang 2.
        ClassDefinition modified = X1.With(
            (TalentAlpha, new[] { Mod(Frappe + ".coefficient", "0.1", "0.1"), Mod(Frappe + ".cooldownMs", "-1000", "-1000") }),
            (Passif, new[]
            {
                Mod("guard.damageReduction", "0.05"), Mod("guard.costPerBlock", "-5"), Mod("guard.upkeepPerSecond", "1"),
                Mod("parry.windowMs", "100"), Mod("parry.failureRecoveryMs", "-200"), Mod("rampartCharges.max", "2"),
                Mod("resonance.perDirectHit", "2"), Mod("endurance.regenPerSecond", "3"),
            }));
        var reference = new CombatRules
        {
            GuardDamageReduction = Fixed.Parse("0.65"), GuardCostPerBlock = 5, GuardUpkeepPerSecond = 3, ParryWindowMs = 250,
            ParryFailureRecoveryMs = 300, RampartChargesMax = 7, ResonancePerDirectHit = 5, EnduranceRegenPerSecond = 13,
        };
        // Coefficient 1,2 + 0,1 + 0,1 = 1,4 ; recharge 6000 − 1000 − 1000 = 4000.
        ClassDefinition referenceClass = Class(frappeCooldownMs: 4000, frappeCoefficient: "1.4", secondPassive: true);
        string[] passives = { Passif };

        var (lines, _) = X1.AssertEquivalent(X1.Mixed(modified, X1.Base, X1.Alpha(2), passives), X1.Mixed(referenceClass, reference, X1.Alpha(2), passives),
            X1.Mixed(X1.Plain, X1.Base, X1.Alpha(2), passives));

        Assert.Equal(X1.ExpectedRules(reference, ",\"skill.essai.frappe.coefficient\":1.4,\"skill.essai.frappe.cooldownMs\":4000"), X1.RulesJson(lines));
    }

    /// <summary>Modificateurs de clés distinctes ou répétées, à valeur unique (sans perRank : rang 1 partout).</summary>
    private static readonly ModifierDefinition[] Five =
    {
        Mod("guard.damageReduction", "0.03"), Mod("guard.damageReduction", "0.02"), Mod("parry.windowMs", "100"),
        Mod("rampartCharges.max", "1"), Mod("guard.costPerBlock", "-2"),
    };

    private static readonly string[] Owners = { TalentAlpha, TalentBeta, TalentGamma, Passif, PassifB };
    private static readonly Dictionary<string, int> AllTalents = new() { [TalentAlpha] = 1, [TalentBeta] = 1, [TalentGamma] = 1 };
    private static readonly string[] BothPassives = { Passif, PassifB };

    [Fact]
    public void Echange_des_modificateurs_entre_talent_et_passif_journaux_identiques()
    {
        ModifierDefinition[] a = { Mod("guard.damageReduction", "0.05"), Mod(Frappe + ".coefficient", "0.1") };
        ModifierDefinition[] b = { Mod("guard.costPerBlock", "5"), Mod("parry.windowMs", "100"), Mod("resonance.perDirectHit", "1") };
        IReadOnlyList<string> first = X1.Lines(X1.Mixed(X1.With((TalentAlpha, a), (Passif, b)), X1.Base, X1.Alpha(1), new[] { Passif }));
        IReadOnlyList<string> swapped = X1.Lines(X1.Mixed(X1.With((TalentAlpha, b), (Passif, a)), X1.Base, X1.Alpha(1), new[] { Passif }));
        Assert.Equal(first, swapped);
    }

    [Fact]
    public void Ordre_inverse_dans_une_meme_liste_journaux_identiques()
    {
        ModifierDefinition[] list =
        {
            Mod("guard.damageReduction", "0.05"), Mod("guard.costPerBlock", "-3"), Mod("parry.windowMs", "50"), Mod("rampartCharges.max", "1"),
            Mod("resonance.perDirectHit", "2"), Mod("endurance.regenPerSecond", "1.5"), Mod(Frappe + ".coefficient", "0.2"),
            Mod(Heurt + ".cooldownMs", "-2000"),
        };
        IReadOnlyList<string> forward = X1.Lines(X1.Mixed(X1.OnPassive(list), X1.Base, passives: new[] { Passif }));
        IReadOnlyList<string> reversed = X1.Lines(X1.Mixed(X1.OnPassive(list.Reverse().ToArray()), X1.Base, passives: new[] { Passif }));
        Assert.Equal(forward, reversed);
        // Et répartis entre les deux passifs, dans l'autre ordre.
        IReadOnlyList<string> split = X1.Lines(X1.Mixed(X1.With((PassifB, list.Take(4).ToArray()), (Passif, list.Skip(4).Reverse().ToArray())),
            X1.Base, passives: BothPassives));
        Assert.Equal(forward.Skip(1), split.Skip(1));   // l'en-tête diffère par le second passif équipé
    }

    private static IEnumerable<int[]> Permutations(int[] items)
    {
        if (items.Length <= 1)
        {
            yield return items;
            yield break;
        }
        for (int i = 0; i < items.Length; i++)
            foreach (int[] rest in Permutations(items.Where((_, k) => k != i).ToArray()))
                yield return new[] { items[i] }.Concat(rest).ToArray();
    }

    [Fact]
    public void Toutes_les_repartitions_sur_cinq_porteurs_journaux_identiques()
    {
        // 5! = 120 répartitions du même ensemble de modificateurs sur trois talents (rang 1) et deux passifs : la somme exacte par clé
        // est commutative, le journal est le même octet pour octet ; et il équivaut aux règles de base modifiées des sommes.
        IReadOnlyList<string>? first = null;
        int count = 0;
        foreach (int[] permutation in Permutations(new[] { 0, 1, 2, 3, 4 }))
        {
            var owners = permutation.Select((m, k) => (Owners[k], new[] { Five[m] })).ToArray();
            IReadOnlyList<string> lines = X1.Lines(X1.Mixed(X1.With(owners), X1.Base, AllTalents, BothPassives));
            if (first is null) first = lines;
            else Assert.Equal(first, lines);
            count++;
        }
        Assert.Equal(120, count);
        // 0,6 + 0,03 + 0,02 = 0,65 ; 150 + 100 = 250 ; 5 + 1 = 6 ; 10 − 2 = 8.
        var reference = new CombatRules { GuardDamageReduction = Fixed.Parse("0.65"), ParryWindowMs = 250, RampartChargesMax = 6, GuardCostPerBlock = 8 };
        X1.AssertEquivalent(X1.Mixed(X1.With(Owners.Select((o, k) => (o, new[] { Five[k] })).ToArray()), X1.Base, AllTalents, BothPassives),
            X1.Mixed(X1.Plain, reference, AllTalents, BothPassives), X1.Mixed(X1.Plain, X1.Base, AllTalents, BothPassives));
        Assert.Equal(X1.ExpectedRules(reference), X1.RulesJson(first!));
    }

    [Fact]
    public void Determinisme_et_rejeu_avec_modificateurs()
    {
        ClassDefinition Build() => X1.With(
            (TalentAlpha, new[] { Mod("guard.damageReduction", "0.02", "0.02"), Mod(Frappe + ".cooldownMs", "-500", "-500") }),
            (Passif, new[] { Mod("guard.upkeepPerSecond", "0.5"), Mod("endurance.regenPerSecond", "0.5"), Mod("rampartCharges.max", "1") }),
            (PassifB, new[] { Mod("parry.windowMs", "50"), Mod("resonance.perDirectHit", "1") }));
        CombatSetup setup = X1.Mixed(Build(), X1.Base, X1.Alpha(4), BothPassives);

        IReadOnlyList<string> first = X1.Lines(setup);
        Assert.True(CombatReplay.Compare(first, X1.Lines(setup)).Identical);                                         // même objet
        Assert.True(CombatReplay.Compare(first, X1.Lines(X1.Mixed(Build(), X1.Base, X1.Alpha(4), BothPassives))).Identical);   // reconstruit

        // Pas à pas et journal texte : mêmes octets.
        var stepped = new MemoryCombatLog();
        var engine = new CombatEngine(setup, stepped);
        while (engine.Step()) { }
        Assert.Equal(first, stepped.Lines);
        var text = new StringWriter();
        CombatEngine.Run(setup, new TextWriterCombatLog(text));
        Assert.Equal(string.Concat(first.Select(l => l + "\n")), text.ToString());
    }

    [Fact]
    public void Copies_defensives_des_modificateurs()
    {
        // Modifier les tableaux et listes après construction de la classe ne change ni ses modificateurs ni le combat.
        var modifiers = new[] { Mod("guard.damageReduction", "0.1") };
        var map = new Dictionary<string, ModifierDefinition[]> { [Passif] = modifiers };
        ClassDefinition definition = Class(modifiers: map, secondPassive: true);
        modifiers[0] = Mod("guard.damageReduction", "-0.5");
        map[Passif] = new[] { Mod("guard.costPerBlock", "50") };

        var conditions = new List<ModifierCondition> { ModifierCondition.WhileGuarding };
        var conditional = new ModifierDefinition("damageDealt", new RankedValue(Fixed.Parse("0.1")), conditions);
        conditions[0] = ModifierCondition.PerResonanceStep;
        conditions.Add(ModifierCondition.PerRampartCharge);
        Assert.Equal(new[] { ModifierCondition.WhileGuarding }, conditional.Conditions);

        Assert.Equal("guard.damageReduction", definition.Skills.Single(s => s.Id == Passif).Modifiers.Single().Key);
        var reference = new CombatRules { GuardDamageReduction = Fixed.Parse("0.7") };
        CombatSetup Fight(ClassDefinition c, CombatRules r) =>
            Setup(new[] { GuardStart(0) }, new CombatantSetup[] { X1.Bearer(c, passives: new[] { Passif }), Assailant(1000, 1000) }, rules: r);
        var (lines, _) = X1.AssertEquivalent(Fight(definition, X1.Base), Fight(X1.Plain, reference), Fight(X1.Plain, X1.Base));
        Assert.Equal(X1.ExpectedRules(reference), X1.RulesJson(lines));
    }

    [Fact]
    public void Seul_le_porteur_recoit_ses_regles_effectives()
    {
        // Même classe pour deux personnages : seul « bastion » a le talent. Chacun garde face à son assaillant.
        ClassDefinition shared = X1.OnAlpha(Mod("guard.damageReduction", "0.1"));
        var combatants = new CombatantSetup[]
        {
            X1.Bearer(shared, X1.Alpha(1)), X1.Bearer(shared, id: "allie"),
            Assailant(1000, 1000), Assailant(1000, 1000, id: "assaillant2", target: "allie"),
        };
        IReadOnlyList<string> lines = X1.Lines(Setup(new[] { GuardStart(0), GuardStart(0, "allie") }, combatants, durationMs: 3000));

        var events = X1.EventsOf(lines);
        Assert.Equal(30, events.OfKind("damage").First(e => e.Str("target") == "bastion").Int("amount"));   // 100 × (1 − 0,7)
        Assert.Equal(40, events.OfKind("damage").First(e => e.Str("target") == "allie").Int("amount"));     // 100 × (1 − 0,6)
        Assert.Equal(X1.ExpectedRules(new CombatRules { GuardDamageReduction = Fixed.Parse("0.7") }), X1.RulesJson(lines));
        Assert.Equal(Lot8LogFields.BaseRulesJson(X1.Base), X1.RulesJson(lines, "allie"));
    }

    [Fact]
    public void Aucun_etat_partage_entre_combats()
    {
        // Un combat sans modificateur rend le même journal avant et après un combat avec modificateurs (aucun cache des règles).
        CombatSetup Plain() => X1.Mixed(X1.Plain, X1.Base, X1.Alpha(5), BothPassives);
        IReadOnlyList<string> before = X1.Lines(Plain());
        X1.Lines(X1.Mixed(X1.With((TalentAlpha, new[] { Mod("guard.damageReduction", "0.02", "0.02") }), (Passif, new[] { Mod("rampartCharges.max", "5") })),
            X1.Base, X1.Alpha(5), BothPassives));
        Assert.Equal(before, X1.Lines(Plain()));
        Assert.Equal(Lot8LogFields.BaseRulesJson(X1.Base), X1.RulesJson(before));
    }
}

/// <summary>
/// Sous-lot 8.1 (D8, D10) : objet « rules » de la ligne « combatant » d'un personnage avec modificateurs, comparé à un JSON attendu
/// écrit à la main : clés de règle scalaires à leur valeur résultante (écrêtée), clés paramétrées des seuls modificateurs actifs en
/// ordre ordinal (coefficient et recharge effectifs, ajout en ms des durées), liste « conditional » ; « rules » nul pour un mannequin
/// et un assaillant.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Engine")]
public class Lot81CombatantRulesLogTests
{
    private static IReadOnlyList<string> Log(ClassDefinition definition, IReadOnlyDictionary<string, int>? talents, string[]? passives) =>
        X1.Lines(Setup(Array.Empty<CombatCommand>(), new CombatantSetup[]
        {
            X1.Bearer(definition, talents, passives), Dummy(), Assailant(firstAttackMs: 20_000),
        }, durationMs: 1000));

    [Fact]
    public void Objet_rules_complet_valeurs_resultantes_cles_parametrees_et_conditionnels()
    {
        ClassDefinition definition = X1.With(
            (TalentAlpha, new[] { Mod("guard.damageReduction", "0.01", "0.02") }),   // rang 3 : 0,01 + 2 × 0,02 = 0,05
            (TalentBeta, new[] { Mod(Heurt + ".coefficient", "0.1", "0.1") }),        // rang 2 : 0,2 → 0,8 + 0,2 = 1
            (Passif, new[]
            {
                Mod(Frappe + ".cooldownMs", "-1000"), Mod("status.stun.durationDealt", "500"), Mod("damageDealt", "0.1"),
                Mod("damageDealt", "0.05", null, ModifierCondition.WhileGuarding),
                Mod("damageTaken", "-0.01", null, ModifierCondition.WhileGuarding, ModifierCondition.PerRampartCharge),
            }),
            (PassifB, new[]
            {
                Mod(Frappe + ".coefficient", "0.3"), Mod("guard.upkeepPerSecond", "0.5"), Mod("resonance.perDirectHit", "2"),
                Mod("damageDealt", "0.05", null, ModifierCondition.WhileGuarding), Mod("status.burn.durationTaken", "2000"),
                Mod("guard.noUpkeepBreak", "1"), Mod("guard.reductionAtZeroEndurance", "0.7"), Mod("guard.breakShortenPerCharge", "500"),
            }));
        IReadOnlyList<string> lines = Log(definition, new Dictionary<string, int> { [TalentAlpha] = 3, [TalentBeta] = 2 }, new[] { Passif, PassifB });

        // Réduction à endurance nulle : min(0,7, 0,65) = 0,65 (D10, jamais au-dessus de la réduction de garde effective). Raccourci de
        // rupture inerte avec le drapeau (aucune rupture ne reste) : 0 (revue 8.1 : le journal écrit ce qui s'applique).
        const string expected = "{\"guard.damageReduction\":0.65,\"guard.upkeepPerSecond\":2.5,\"guard.costPerBlock\":10,"
            + "\"guard.noUpkeepBreak\":true,\"guard.reductionAtZeroEndurance\":0.65,\"guard.breakShortenPerCharge\":0,\"parry.windowMs\":150,"
            + "\"parry.failureRecoveryMs\":500,\"rampartCharges.max\":5,\"resonance.perDirectHit\":5,\"endurance.regenPerSecond\":10,"
            + "\"damageDealt\":0.1,\"damageTaken\":0,"
            + "\"skill.essai.frappe.coefficient\":1.5,\"skill.essai.frappe.cooldownMs\":5000,\"skill.essai.heurt.coefficient\":1,"
            + "\"status.burn.durationTaken\":2000,\"status.stun.durationDealt\":500,"
            + "\"conditional\":[{\"key\":\"damageDealt\",\"conditions\":[\"whileGuarding\"],\"value\":0.1},"
            + "{\"key\":\"damageTaken\",\"conditions\":[\"whileGuarding\",\"perRampartCharge\"],\"value\":-0.01}]}";
        Assert.Equal(expected, X1.RulesJson(lines));
    }

    [Fact]
    public void Objet_rules_raccourci_de_rupture_sans_drapeau_ecrit_tel_qu_il_s_applique()
    {
        // Contre-vérification 8.1 : sans guard.noUpkeepBreak, le raccourci de rupture s'applique et le journal l'écrit : talent alpha au
        // rang 3 (100 + 2 × 100 = 300) + passif 500 = 800 ms par Charge. La réduction à endurance nulle, sans le drapeau, reste inerte : 0.
        ClassDefinition definition = X1.With(
            (TalentAlpha, new[] { Mod("guard.breakShortenPerCharge", "100", "100") }),
            (Passif, new[] { Mod("guard.breakShortenPerCharge", "500"), Mod("guard.reductionAtZeroEndurance", "0.3") }));
        IReadOnlyList<string> lines = Log(definition, X1.Alpha(3), new[] { Passif });
        const string expected = "{\"guard.damageReduction\":0.6,\"guard.upkeepPerSecond\":2,\"guard.costPerBlock\":10,"
            + "\"guard.noUpkeepBreak\":false,\"guard.reductionAtZeroEndurance\":0,\"guard.breakShortenPerCharge\":800,\"parry.windowMs\":150,"
            + "\"parry.failureRecoveryMs\":500,\"rampartCharges.max\":5,\"resonance.perDirectHit\":3,\"endurance.regenPerSecond\":10,"
            + "\"damageDealt\":0,\"damageTaken\":0,\"conditional\":[]}";
        Assert.Equal(expected, X1.RulesJson(lines));
    }

    [Fact]
    public void Objet_rules_valeurs_ecretees_aux_plafonds_et_aux_planchers()
    {
        ClassDefinition definition = X1.With(
            (TalentAlpha, new[]
            {
                Mod("guard.damageReduction", "0.15"), Mod("rampartCharges.max", "2", "2"), Mod("parry.windowMs", "100", "100"),   // rang 5 : +10, +500
            }),
            (Passif, new[]
            {
                Mod("guard.damageReduction", "0.75"), Mod("guard.costPerBlock", "-20"), Mod("guard.upkeepPerSecond", "-5"),
                Mod("endurance.regenPerSecond", "-15"), Mod("parry.failureRecoveryMs", "-1000"), Mod("resonance.perDirectHit", "-5"),
                Mod(Frappe + ".cooldownMs", "-10000"), Mod(Frappe + ".coefficient", "-2"),
            }),
            (PassifB, new[] { Mod(Heurt + ".coefficient", "100"), Mod(Heurt + ".cooldownMs", "86400000") }));
        IReadOnlyList<string> lines = Log(definition, X1.Alpha(5), new[] { Passif, PassifB });

        // 0,6 + 0,9 → 0,75 ; 5 + 10 → 10 ; 150 + 500 → 500 ; planchers à 0 ; coefficients 0 et 100 ; recharges 0 et 24 h.
        const string expected = "{\"guard.damageReduction\":0.75,\"guard.upkeepPerSecond\":0,\"guard.costPerBlock\":0,"
            + "\"guard.noUpkeepBreak\":false,\"guard.reductionAtZeroEndurance\":0,\"guard.breakShortenPerCharge\":0,\"parry.windowMs\":500,"
            + "\"parry.failureRecoveryMs\":0,\"rampartCharges.max\":10,\"resonance.perDirectHit\":0,\"endurance.regenPerSecond\":0,"
            + "\"damageDealt\":0,\"damageTaken\":0,"
            + "\"skill.essai.frappe.coefficient\":0,\"skill.essai.frappe.cooldownMs\":0,"
            + "\"skill.essai.heurt.coefficient\":100,\"skill.essai.heurt.cooldownMs\":86400000,\"conditional\":[]}";
        Assert.Equal(expected, X1.RulesJson(lines));
    }

    [Fact]
    public void Cles_parametrees_des_seuls_modificateurs_actifs()
    {
        // Talent gamma au rang 0 et second passif non équipé : inactifs, absents de l'objet et sans effet. Deux sources actives de
        // somme nulle sur le coefficient de Frappe : clé ciblée, présente à la valeur de base 1,2.
        ClassDefinition definition = X1.With(
            (TalentAlpha, new[] { Mod(Frappe + ".coefficient", "0.1") }), (Passif, new[] { Mod(Frappe + ".coefficient", "-0.1") }),
            (TalentGamma, new[] { Mod(Heurt + ".coefficient", "0.5"), Mod("guard.damageReduction", "0.1") }),
            (PassifB, new[] { Mod("status.stun.durationTaken", "500"), Mod("parry.windowMs", "100") }));
        CombatSetup Fight(ClassDefinition c) => Setup(new[] { Cmd(0, Frappe, target: "assaillant"), Cmd(1000, Heurt, target: "assaillant"), GuardStart(2000), Parry(3000) },
            new CombatantSetup[] { X1.Bearer(c, X1.Alpha(1), new[] { Passif }), Assailant(1000, 1000) }, durationMs: 5000);

        var (lines, _) = X1.AssertEquivalent(Fight(definition), Fight(X1.Plain), null);

        Assert.Equal(X1.ExpectedRules(X1.Base, ",\"skill.essai.frappe.coefficient\":1.2"), X1.RulesJson(lines));
    }

    [Fact]
    public void Mannequin_et_assaillant_gardent_rules_nul()
    {
        ClassDefinition definition = X1.With((TalentAlpha, new[] { Mod("guard.damageReduction", "0.1") }),
            (Passif, new[] { Mod("statusResistance", "50"), Mod("damageDealt", "0.1", null, ModifierCondition.WhileGuarding) }));
        IReadOnlyList<string> lines = Log(definition, X1.Alpha(1), new[] { Passif });

        var combatants = X1.EventsOf(lines).OfKind("combatant").ToDictionary(e => e.Str("combatant")!);
        Assert.Equal(new[] { "assaillant", "bastion", "mannequin" }, combatants.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(JsonValueKind.Object, combatants["bastion"].GetProperty("rules").ValueKind);
        foreach (string id in new[] { "mannequin", "assaillant" })
        {
            Assert.Equal(JsonValueKind.Null, combatants[id].GetProperty("rules").ValueKind);
            Assert.Equal("0", combatants[id].GetProperty("statusResistance").GetRawText());   // champ présent pour tous
        }
        Assert.Equal("70", combatants["bastion"].GetProperty("statusResistance").GetRawText());   // 2 × 10 + 50
        Assert.Null(Lot8LogFields.NeutralProblem(lines.Skip(1).First(l => l.Contains("\"combatant\":\"mannequin\"")), _ => null));
        Assert.Null(Lot8LogFields.NeutralProblem(lines.Skip(1).First(l => l.Contains("\"combatant\":\"assaillant\"")), _ => null));
    }
}
