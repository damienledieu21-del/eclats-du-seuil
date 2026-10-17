using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Modifiers;

/// <summary>
/// Sous-lot 8.1, moteur : modificateurs conditionnels (X2) lus au calcul des dégâts, catégorie talent des dégâts infligés, dégâts
/// subis sur les impacts directs et les ticks de DOT, plafonds de 75 % (garde) et de 85 % (réduction totale, parade hors plafond),
/// drapeaux de garde (<c>guard.noUpkeepBreak</c>, <c>guard.reductionAtZeroEndurance</c>, <c>guard.breakShortenPerCharge</c>) et
/// durées d'altération (<c>status.&lt;id&gt;.durationDealt</c> et <c>durationTaken</c>). Conception : D4 à D6 et D10 (qui fait foi).
/// <para>Correspondance avec la liste obligatoire du 8.1 (rappelée sur chaque test) : [rangs] chaque clé au rang 1 et au rang
/// maximal ; [bornes] N−1, N, N+1 ; [combinaisons] plusieurs modificateurs ensemble ; [conditions] vraies et fausses dans le même
/// combat ; [ordre] agrégation indépendante de l'ordre ; [déterminisme] déterminisme et rejeu. Immuabilité, débordements refusés
/// avant la simulation et absence de mutation partielle relèvent du domaine et du précontrôle (autres fichiers) ; ici, chaque
/// combat est simulé en Debug, où toute réserve de budget est vérifiée exacte à chaque traitement, et va à son terme sans
/// <c>simulationAborted</c> (<see cref="Fight"/>). La non-régression des cinq scénarios livrés est couverte par Lot81ReferenceTests
/// et Lot81EngineBaselineTests.</para>
/// <para>Repères des combattants d'essai : Force 14, soit 28 de puissance physique et 12 de puissance magique ; Volonté 10, soit
/// 20 de résistance aux altérations (facteur ⌊1000 / 1020⌋ à 4 décimales = 0,9803) ; armure et résistance magique nulles ; 100
/// d'endurance. L'assaillant frappe 100 bruts toutes les 2 s à partir du tick 20 ; sans défense, l'impact inflige 100.</para>
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Engine")]
[Trait("Category", "Status")]
public class Lot81ConditionalAndGuardModifierTests
{
    private static readonly DerivedStats Unarmored = CombatantStats.ForDefenses(StatRules.Provisional, Fixed.Zero, Fixed.Zero);

    private static Dictionary<string, ModifierDefinition[]> Mods(params (string Holder, ModifierDefinition[] Modifiers)[] entries) =>
        entries.ToDictionary(e => e.Holder, e => e.Modifiers, StringComparer.Ordinal);

    /// <summary>Personnage porteur de modificateurs (classe d'essai avec second passif), passif <see cref="Passif"/> équipé par défaut.</summary>
    private static CharacterSetup Bearer(IReadOnlyDictionary<string, ModifierDefinition[]>? modifiers, string id = "bastion", string team = "joueurs",
        int vitalite = 200, string[]? actives = null, int talentAlpha = 0, string[]? passives = null, IEnumerable<TriggerDefinition>? triggers = null,
        long frappeCooldownMs = 6000) =>
        Character(id, team, attributes: Attrs(vitalite: vitalite), actives: actives,
            definition: Class(frappeCooldownMs, passiveTriggers: triggers, modifiers: modifiers, secondPassive: true),
            talents: talentAlpha > 0 ? new Dictionary<string, int> { [TalentAlpha] = talentAlpha } : null,
            passives: passives ?? new[] { Passif });

    /// <summary>
    /// Simule un combat de test. En Debug (configuration par défaut des tests), chaque réserve de budget est vérifiée exacte : un coût
    /// mal prévu lève une exception et fait échouer le test. Le combat doit aller à son terme sans arrêt.
    /// </summary>
    private static (CombatResult Result, List<JsonElement> Events, MemoryCombatLog Log) Fight(IEnumerable<CombatCommand> commands, CombatRules? rules,
        long durationMs, params CombatantSetup[] fighters)
    {
        var (result, log) = Run(Setup(commands, fighters, durationMs: durationMs, rules: rules));
        List<JsonElement> events = Events(log);
        Assert.NotEqual(CombatEndReason.Aborted, result.EndReason);
        Assert.Empty(events.OfKind("simulationAborted"));
        return (result, events, log);
    }

    private static List<(long Tick, string Defense, long Amount)> HitsOn(List<JsonElement> events, string target) =>
        events.OfKind("damage").Where(e => e.Str("target") == target).Select(e => (e.Tick(), e.Str("defense")!, e.Int("amount"))).ToList();

    private static List<JsonElement> HitsFrom(List<JsonElement> events, string source) =>
        events.OfKind("damage").Where(e => e.Str("source") == source).ToList();

    private static List<(long Tick, string State, long Amount)> Dots(List<JsonElement> events) =>
        events.OfKind("dotDamage").Select(e => (e.Tick(), e.Str("state")!, e.Int("amount"))).ToList();

    private static JsonElement RulesOf(List<JsonElement> events, string id = "bastion") =>
        events.OfKind("combatant").Single(e => e.Str("combatant") == id).GetProperty("rules");

    private static JsonElement Applied(List<JsonElement> events, string state, string combatant) =>
        events.OfKind("stateApplied").Single(e => e.Str("state") == state && e.Str("combatant") == combatant);

    private static JsonElement Summary(List<JsonElement> events, string id = "bastion") =>
        events.OfKind("combatantSummary").Single(e => e.Str("combatant") == id);

    private static bool Critical(JsonElement hit) => hit.GetProperty("critical").GetBoolean();

    /// <summary>
    /// Dégâts attendus d'un impact de personnage, calculés indépendamment du moteur par la formule du domaine avec des entrées
    /// explicites : statistiques sans modificateur, coefficient de la compétence, bonus de catégorie talent, critique lu au journal.
    /// </summary>
    private static long Expected(DerivedStats attacker, DerivedStats defender, string coefficient, Fixed talent, bool critical,
        PowerType power = PowerType.Physique, DamageType type = DamageType.Physique, Fixed alteration = default, Fixed mitigation = default) =>
        DamageCalculator.Compute(new DamageInput(attacker, defender, power, type, Fixed.Parse(coefficient),
            DamageBonuses.None.Add(BonusCategory.Talent, talent).Add(BonusCategory.Alteration, alteration), critical, mitigation), DamageRules.Provisional).FinalDamage;

    private static DerivedStats StatsOf(CharacterSetup character) => CombatantStats.ForCharacter(StatRules.Provisional, character.Build);

    private static string CoefficientOf(string skill) => skill switch
    {
        Frappe => "1.2",
        Recharge => "0.1",
        Rempart => "0.5",
        Percee => "1",
        Gratuite => "0.1",
        _ => throw new ArgumentOutOfRangeException(nameof(skill), skill, null),
    };

    /// <summary>Frappe de réaction de 0,5 (physique) sur l'auteur de chaque impact subi par le porteur.</summary>
    private static readonly TriggerDefinition Riposte =
        new(TriggerEvent.HitTaken, new[] { TriggerEffect.Strike(TriggerTarget.EventSource, Fixed.Parse("0.5")) }, TriggerRole.Target);

    private static readonly CombatRules NoUpkeepNoRegen = new() { GuardUpkeepPerSecond = 0, EnduranceRegenPerSecond = 0 };

    // =====================================================================================================================
    // Conditions (X2) vraies et fausses dans le même combat
    // =====================================================================================================================

    [Fact]
    public void Degats_subis_pendant_la_garde_reduits_sous_garde_seulement_et_jamais_sur_l_impact_qui_la_brise()
    {
        // [conditions] [bornes : impact qui brise la garde]. Garde 0,6, damageTaken −0,2 {whileGuarding}, 40 d'endurance par blocage,
        // ni entretien ni régénération. Impacts de 100 aux ticks 20, 60, 100, 140 et 180 :
        //   20  garde (100 → 60) : r = 1 − (1 − 0,6)(1 − 0,2) = 1 − 0,4 × 0,8 = 0,68 → 100 × 0,32 = 32
        //   60  hors garde : la condition est fausse, v = 0 → 100
        //  100  garde (60 → 20) → 32
        //  140  20 < 40 : rupture ; au calcul des dégâts le porteur n'est plus en garde, la condition est fausse → 100
        //  180  sous rupture (fin au tick 200) → 100
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, GuardCostPerBlock = 40, EnduranceRegenPerSecond = 0 };
        var commands = new[] { GuardStart(0), GuardStop(2500), GuardStart(4500) };
        var mods = Mods((Passif, new[] { Mod("damageTaken", "-0.2", null, ModifierCondition.WhileGuarding) }));
        var (result, events, _) = Fight(commands, rules, 10_000, Bearer(mods), Assailant());

        var expected = new[] { (20L, "guard", 32L), (60L, "none", 100L), (100L, "guard", 32L), (140L, "guardBroken", 100L), (180L, "none", 100L) };
        Assert.Equal(expected, HitsOn(events, "bastion"));
        JsonElement broken = Assert.Single(events.OfKind("guardBroken"));
        Assert.Equal((140L, "insufficientEndurance", 20L, 0L), (broken.Tick(), broken.Str("reason")!, broken.Int("endurance"), broken.Int("rampartChargesSpent")));
        Assert.Equal(2L, result["bastion"].Blocks);
        Assert.Equal("[{\"key\":\"damageTaken\",\"conditions\":[\"whileGuarding\"],\"value\":-0.2}]", RulesOf(events).GetProperty("conditional").GetRawText());

        // Équivalence indépendante : une garde de base de 0,68 sans modificateur donne exactement les mêmes impacts (et une garde de
        // base de 0,6 sans modificateur, 40 sous garde : le modificateur compte bien).
        var equivalent = new CombatRules { GuardUpkeepPerSecond = 0, GuardCostPerBlock = 40, EnduranceRegenPerSecond = 0, GuardDamageReduction = Fixed.Parse("0.68") };
        Assert.Equal(expected, HitsOn(Fight(commands, equivalent, 10_000, Bearer(null), Assailant()).Events, "bastion"));
        Assert.Equal(new[] { 40L, 100L, 40L, 100L, 100L }, HitsOn(Fight(commands, rules, 10_000, Bearer(null), Assailant()).Events, "bastion").Select(h => h.Amount));
    }

    [Fact]
    public void Degats_infliges_par_charge_de_rempart_lus_sur_les_charges_detenues_a_chaque_impact()
    {
        // [conditions]. damageDealt +0,1 {perRampartCharge}, catégorie talent. Recharge produit 3 Charges à son lancement (avant son
        // impact), Rempart en consomme 2 au sien ; Frappe (recharge ramenée à 1 s) entre les deux. Charges détenues à chaque impact :
        // 0, 3, 3, 1, 1, 4, 4, 2, 0, 0 (vraie puis fausse dans le même combat). Hors critique : Frappe sans Charge 28 × 1,2 = 33,6 → 34 ;
        // avec 3 Charges 33,6 × 1,3 = 43,68 → 44 ; avec 4 Charges 33,6 × 1,4 = 47,04 → 47.
        string[] sequence = { Frappe, Recharge, Frappe, Rempart, Frappe, Recharge, Frappe, Rempart, Rempart, Frappe };
        int[] charges = { 0, 3, 3, 1, 1, 4, 4, 2, 0, 0 };
        var mods = Mods((Passif, new[] { Mod("damageDealt", "0.1", null, ModifierCondition.PerRampartCharge) }));
        CharacterSetup bastion = Bearer(mods, actives: new[] { Frappe, Recharge, Rempart, Gratuite }, frappeCooldownMs: 1000);
        var (_, events, _) = Fight(sequence.Select((s, i) => Cmd(i * 1000L, s)), null, 10_000, bastion, Dummy(armor: 0));

        Assert.Empty(events.OfKind("commandRejected"));
        List<JsonElement> hits = HitsFrom(events, "bastion");
        Assert.Equal(sequence, hits.Select(h => h.Str("skill")));
        // Charges relues au journal (après le coût du lancement, puis le gain) : la séquence écrite à la main est bien celle du combat.
        var observed = new List<int>();
        long current = 0;
        foreach (JsonElement e in events)
        {
            string kind = e.Str("event")!;
            if (kind == "skillCast") current = e.Int("rampartCharges");
            else if (kind == "resourceGained" && e.Str("resource") == "rampartCharges") current = e.Int("value");
            else if (kind == "damage" && e.Str("source") == "bastion") observed.Add((int)current);
        }
        Assert.Equal(charges, observed);
        DerivedStats stats = StatsOf(bastion);
        Assert.Equal(hits.Select((h, i) => Expected(stats, Unarmored, CoefficientOf(sequence[i]), Fixed.Parse("0.1") * charges[i], Critical(h))),
            hits.Select(h => h.Int("amount")));
        Assert.Equal(new long[] { 34, 44, 47 }, new[] { 0, 3, 4 }.Select(c => Expected(stats, Unarmored, "1.2", Fixed.Parse("0.1") * c, false)));
    }

    [Fact]
    public void Degats_infliges_par_palier_de_resonance_lus_sur_la_resonance_avant_l_impact()
    {
        // [conditions]. damageDealt +0,1 {perResonanceStep} (palier de 10). Percée (1,0, sans coût ni recharge) chaque seconde :
        // +3 de Résonance après chaque impact direct, donc 0, 3, 6, …, 27 avant les impacts et ⌊R / 10⌋ = 0, 0, 0, 0, 1, 1, 1, 2, 2, 2.
        // Hors critique : 28, 28 × 1,1 = 30,8 → 31, 28 × 1,2 = 33,6 → 34.
        int[] steps = { 0, 0, 0, 0, 1, 1, 1, 2, 2, 2 };
        var mods = Mods((Passif, new[] { Mod("damageDealt", "0.1", null, ModifierCondition.PerResonanceStep) }));
        CharacterSetup bastion = Bearer(mods, actives: new[] { Percee, Frappe, Heurt, Gratuite });
        var (result, events, _) = Fight(Enumerable.Range(0, 10).Select(i => Cmd(i * 1000L, Percee)), null, 10_000, bastion, Dummy(armor: 0));

        List<JsonElement> hits = HitsFrom(events, "bastion");
        Assert.Equal(10, hits.Count);
        var gains = events.OfKind("resourceGained").Where(e => e.Str("resource") == "resonance").ToList();
        Assert.Equal(Enumerable.Range(1, 10).Select(i => ("directHit", 3L * i)), gains.Select(g => (g.Str("source")!, g.Int("value"))));
        Assert.All(hits.Zip(gains), p => Assert.True(p.First.Int("seq") < p.Second.Int("seq")));   // la Résonance de l'impact vient après lui
        DerivedStats stats = StatsOf(bastion);
        Assert.Equal(hits.Select((h, i) => Expected(stats, Unarmored, "1", Fixed.Parse("0.1") * steps[i], Critical(h))), hits.Select(h => h.Int("amount")));
        Assert.Equal(new long[] { 28, 31, 34 }, new[] { 0, 1, 2 }.Select(s => Expected(stats, Unarmored, "1", Fixed.Parse("0.1") * s, false)));
        Assert.Equal(30, result["bastion"].Resonance);
    }

    /// <summary>Combat de référence de {whileGuarding, perRampartCharge} : garde sans Charge, Charges hors garde, garde avec 3 puis 1 Charge.</summary>
    private static CombatSetup GuardChargeCombat(IReadOnlyDictionary<string, ModifierDefinition[]>? mods, int talentAlpha = 0, string[]? passives = null,
        CombatRules? rules = null) =>
        Setup(new[]
            {
                GuardStart(0), GuardStop(1500), Cmd(1500, Recharge, target: "assaillant"), GuardStart(4000),
                GuardStop(5500), Cmd(5500, Rempart, target: "assaillant"), GuardStart(6000),
            },
            new CombatantSetup[] { Bearer(mods, actives: new[] { Recharge, Rempart, Frappe, Gratuite }, talentAlpha: talentAlpha, passives: passives), Assailant() },
            durationMs: 10_000, rules: rules ?? NoUpkeep);

    [Fact]
    public void Degats_subis_pendant_la_garde_et_par_charge_combines_dans_le_meme_combat()
    {
        // [conditions] [combinaisons]. damageTaken −0,05 {whileGuarding, perRampartCharge} : v = −0,05 × Charges, seulement sous garde.
        //   20  garde, 0 Charge : v = 0 → r = 0,6 → 40
        //   60  hors garde, 3 Charges : condition fausse → 100
        //  100  garde, 3 Charges : v = −0,15 → r = 1 − 0,4 × 0,85 = 0,66 → 34
        //  140  garde, 1 Charge (Rempart) : v = −0,05 → r = 1 − 0,4 × 0,95 = 0,62 → 38
        //  180  idem → 38
        var mods = Mods((Passif, new[] { Mod("damageTaken", "-0.05", null, ModifierCondition.WhileGuarding, ModifierCondition.PerRampartCharge) }));
        var (result, log) = Run(GuardChargeCombat(mods));
        List<JsonElement> events = Events(log);
        Assert.NotEqual(CombatEndReason.Aborted, result.EndReason);
        Assert.Empty(events.OfKind("commandRejected"));
        Assert.Equal(new[] { (20L, "guard", 40L), (60L, "none", 100L), (100L, "guard", 34L), (140L, "guard", 38L), (180L, "guard", 38L) },
            HitsOn(events, "bastion"));
        Assert.Equal("[{\"key\":\"damageTaken\",\"conditions\":[\"whileGuarding\",\"perRampartCharge\"],\"value\":-0.05}]",
            RulesOf(events).GetProperty("conditional").GetRawText());
    }

    [Theory]
    [InlineData(1, "-0.02", 26L, 23L)]
    [InlineData(5, "-0.1", 24L, 20L)]
    public void Combinaison_permanent_garde_et_charges_additionnee_au_rang_1_et_au_rang_maximal(int rank, string whileGuarding, long guardedEmpty, long guardedCharged)
    {
        // [combinaisons] [rangs]. Talent alpha : damageTaken {whileGuarding} −0,02 par rang (−0,02 au rang 1, −0,10 au rang 5) ;
        // passif : damageTaken −0,1 permanent et guard.damageReduction +0,1 (garde 0,7) ; passif B : damageTaken −0,04
        // {whileGuarding, perRampartCharge}.
        //   20  garde, 0 Charge : v = −0,1 − w ; rang 1 : r = 1 − 0,3 × 0,88 = 0,736 → 26,4 → 26 ; rang 5 : 1 − 0,3 × 0,8 = 0,76 → 24
        //   60  hors garde, 3 Charges : v = −0,1 → r = 0,1 → 90
        //  100  garde, 3 Charges : v = −0,1 − w − 0,12 ; rang 1 : 1 − 0,3 × 0,76 = 0,772 → 22,8 → 23 ; rang 5 : 1 − 0,3 × 0,68 = 0,796 → 20,4 → 20
        var mods = Mods(
            (TalentAlpha, new[] { Mod("damageTaken", "-0.02", "-0.02", ModifierCondition.WhileGuarding) }),
            (Passif, new[] { Mod("damageTaken", "-0.1"), Mod("guard.damageReduction", "0.1") }),
            (PassifB, new[] { Mod("damageTaken", "-0.04", null, ModifierCondition.WhileGuarding, ModifierCondition.PerRampartCharge) }));
        string[] passives = { Passif, PassifB };
        var (result, log) = Run(GuardChargeCombat(mods, rank, passives));
        List<JsonElement> events = Events(log);
        Assert.NotEqual(CombatEndReason.Aborted, result.EndReason);
        var hits = HitsOn(events, "bastion");
        Assert.Equal(new[] { (20L, "guard", guardedEmpty), (60L, "none", 90L), (100L, "guard", guardedCharged) }, hits.Take(3));

        JsonElement rules = RulesOf(events);
        Assert.Equal((0.7m, -0.1m), (rules.GetProperty("guard.damageReduction").GetDecimal(), rules.GetProperty("damageTaken").GetDecimal()));
        Assert.Equal($"[{{\"key\":\"damageTaken\",\"conditions\":[\"whileGuarding\"],\"value\":{whileGuarding}}},"
            + "{\"key\":\"damageTaken\",\"conditions\":[\"whileGuarding\",\"perRampartCharge\"],\"value\":-0.04}]", rules.GetProperty("conditional").GetRawText());

        // Équivalence : guard.damageReduction +0,1 sur une base de 0,6 ≡ base de 0,7 sans ce modificateur.
        var withoutGuardModifier = Mods(
            (TalentAlpha, mods[TalentAlpha]), (Passif, new[] { Mod("damageTaken", "-0.1") }), (PassifB, mods[PassifB]));
        var (_, equivalent) = Run(GuardChargeCombat(withoutGuardModifier, rank, passives, new CombatRules { GuardUpkeepPerSecond = 0, GuardDamageReduction = Fixed.Parse("0.7") }));
        Assert.Equal(hits, HitsOn(Events(equivalent), "bastion"));
    }

    [Fact]
    public void Degats_subis_par_palier_de_resonance_lus_avant_les_gains_de_l_impact()
    {
        // D6 : conditions évaluées après la résolution de la défense, avant les gains de l'impact. Règles de test : +10 de Résonance
        // par blocage. damageTaken −0,1 {perResonanceStep} : 1er blocage à 0 de Résonance → v = 0 → 40 ; il rapporte 10 ;
        // 2e blocage à 10 → v = −0,1 → r = 1 − 0,4 × 0,9 = 0,64 → 36 ; 3e à 20 → v = −0,2 → 1 − 0,4 × 0,8 = 0,68 → 32.
        var mods = Mods((Passif, new[] { Mod("damageTaken", "-0.1", null, ModifierCondition.PerResonanceStep) }));
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, ResonancePerBlock = 10 };
        var (_, events, _) = Fight(new[] { GuardStart(0) }, rules, 6000, Bearer(mods), Assailant());
        Assert.Equal(new[] { (20L, "guard", 40L), (60L, "guard", 36L), (100L, "guard", 32L) }, HitsOn(events, "bastion"));
        Assert.Equal(new long[] { 10, 20, 30 }, events.OfKind("resourceGained").Where(e => e.Str("source") == "block").Select(e => e.Int("value")));
    }

    [Theory]
    [InlineData(false, 70L, 3L)]
    [InlineData(true, 100L, 0L)]
    public void Degats_subis_par_charge_lus_apres_la_consommation_des_charges_par_la_rupture(bool shorten, long amount, long chargesAfter)
    {
        // D6 : l'état lu est celui laissé par la résolution de la défense. damageTaken −0,1 {perRampartCharge} (sans whileGuarding),
        // 3 Charges, impact qui brise la garde : sans raccourci, les Charges restent → v = −0,3 → 70 ; avec guard.breakShortenPerCharge,
        // la rupture les consomme avant le calcul des dégâts → v = 0 → 100.
        var list = new List<ModifierDefinition> { Mod("damageTaken", "-0.1", null, ModifierCondition.PerRampartCharge) };
        if (shorten) list.Add(Mod("guard.breakShortenPerCharge", "500"));
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, GuardCostPerBlock = 101 };
        var commands = new[] { Cmd(0, Recharge, target: "assaillant"), GuardStart(0) };
        var (_, events, _) = Fight(commands, rules, 2000, Bearer(Mods((Passif, list.ToArray())), actives: ChargeActives), Assailant(intervalMs: 60_000));
        Assert.Equal(new[] { (20L, "guardBroken", amount) }, HitsOn(events, "bastion"));
        Assert.Equal(3L - chargesAfter, Assert.Single(events.OfKind("guardBroken")).Int("rampartChargesSpent"));
        Assert.Equal(chargesAfter, Summary(events).Int("rampartCharges"));
    }

    [Fact]
    public void Degats_infliges_de_l_attaquant_degats_subis_du_defenseur_et_fragilite_sur_le_meme_impact()
    {
        // [combinaisons]. alpha : damageDealt +0,5 (talent) ; beta : damageTaken −0,3 ; un assaillant pose la Fragilité (+0,1, catégorie
        // altération) sur beta au tick 20. Percée d'alpha sur beta aux ticks 0 et 40 : hors critique 28 × 1,5 × 0,7 = 29,4 → 29, puis
        // fragile 28 × 1,5 × 1,1 × 0,7 = 32,34 → 32. L'impact de l'assaillant sur beta au tick 20 (avant la Fragilité) : 100 × 0,7 = 70.
        var mods = Mods((Passif, new[] { Mod("damageDealt", "0.5") }), (PassifB, new[] { Mod("damageTaken", "-0.3") }));
        CharacterSetup alpha = Bearer(mods, "alpha", actives: new[] { Percee, Frappe, Heurt, Gratuite });
        CharacterSetup beta = Bearer(mods, "beta", "rivaux", passives: new[] { PassifB });
        var assailant = Assailant(intervalMs: 60_000, target: "beta", attacks: Blow(fragility: true));
        var (_, events, _) = Fight(new[] { Cmd(0, Percee, "alpha", "beta"), Cmd(2000, Percee, "alpha", "beta") }, null, 3000, alpha, beta, assailant);

        List<JsonElement> hits = HitsFrom(events, "alpha");
        Assert.Equal(new[] { (0L, false), (40L, true) }, hits.Select(h => (h.Tick(), h.GetProperty("fragile").GetBoolean())));
        DerivedStats stats = StatsOf(alpha);
        Assert.Equal(new[]
        {
            Expected(stats, Unarmored, "1", Fixed.Parse("0.5"), Critical(hits[0]), mitigation: Fixed.Parse("0.3")),
            Expected(stats, Unarmored, "1", Fixed.Parse("0.5"), Critical(hits[1]), alteration: Fixed.Parse("0.1"), mitigation: Fixed.Parse("0.3")),
        }, hits.Select(h => h.Int("amount")));
        Assert.Equal((29L, 32L), (Expected(stats, Unarmored, "1", Fixed.Parse("0.5"), false, mitigation: Fixed.Parse("0.3")),
            Expected(stats, Unarmored, "1", Fixed.Parse("0.5"), false, alteration: Fixed.Parse("0.1"), mitigation: Fixed.Parse("0.3"))));
        Assert.Equal(70L, Assert.Single(HitsFrom(events, "assaillant")).Int("amount"));
    }

    [Fact]
    public void Agregation_des_conditionnels_independante_de_la_repartition_des_sources()
    {
        // [ordre]. Même build (talent alpha au rang 1, passifs A et B), mêmes sommes par (clé, conditions) : permanent −0,1,
        // {whileGuarding} −0,06, {whileGuarding, perRampartCharge} −0,04, réparties autrement entre talent et passifs. Le journal est
        // identique octet par octet (en-tête compris : il ne contient pas le contenu de la classe).
        Dictionary<string, ModifierDefinition[]>[] arrangements =
        {
            Mods(
                (TalentAlpha, new[] { Mod("damageTaken", "-0.02", null, ModifierCondition.WhileGuarding, ModifierCondition.PerRampartCharge) }),
                (Passif, new[] { Mod("damageTaken", "-0.02", null, ModifierCondition.PerRampartCharge, ModifierCondition.WhileGuarding),
                    Mod("damageTaken", "-0.06", null, ModifierCondition.WhileGuarding), Mod("damageTaken", "-0.1") })),
            Mods(
                (TalentAlpha, new[] { Mod("damageTaken", "-0.06", null, ModifierCondition.WhileGuarding) }),
                (Passif, new[] { Mod("damageTaken", "-0.05") }),
                (PassifB, new[] { Mod("damageTaken", "-0.04", null, ModifierCondition.WhileGuarding, ModifierCondition.PerRampartCharge), Mod("damageTaken", "-0.05") })),
            Mods(
                (TalentAlpha, new[] { Mod("damageTaken", "-0.1"), Mod("damageTaken", "-0.03", null, ModifierCondition.WhileGuarding),
                    Mod("damageTaken", "-0.01", null, ModifierCondition.WhileGuarding, ModifierCondition.PerRampartCharge) }),
                (PassifB, new[] { Mod("damageTaken", "-0.03", null, ModifierCondition.WhileGuarding),
                    Mod("damageTaken", "-0.03", null, ModifierCondition.WhileGuarding, ModifierCondition.PerRampartCharge) })),
        };
        string[] passives = { Passif, PassifB };
        var logs = arrangements.Select(m => Run(GuardChargeCombat(m, 1, passives)).Log.Lines.ToList()).ToList();
        foreach (List<string> other in logs.Skip(1))
        {
            ReplayComparison comparison = CombatReplay.Compare(logs[0], other);
            Assert.True(comparison.Identical, $"ligne {comparison.FirstDifferentLine} : {comparison.Expected} ≠ {comparison.Actual}");
        }
        // Valeurs : 20 garde 0 Charge v = −0,16 → r = 1 − 0,4 × 0,84 = 0,664 → 33,6 → 34 ; 60 hors garde v = −0,1 → 90 ;
        // 100 garde 3 Charges v = −0,28 → r = 1 − 0,4 × 0,72 = 0,712 → 28,8 → 29 ; 140 et 180 garde 1 Charge v = −0,2 → 1 − 0,4 × 0,8 → 32.
        Assert.Equal(new long[] { 34, 90, 29, 32, 32 }, HitsOn(Events(Run(GuardChargeCombat(arrangements[0], 1, passives)).Log), "bastion").Select(h => h.Amount));
    }

    // =====================================================================================================================
    // Dégâts infligés : catégorie talent des impacts de compétence et des frappes de réaction, jamais des DOT
    // =====================================================================================================================

    [Fact]
    public void Degats_infliges_en_categorie_talent_pour_les_competences_et_les_frappes_de_reaction()
    {
        // damageDealt +0,5 permanent. Frappe (1,2) sur l'assaillant au tick 0 : hors critique 28 × 1,2 × 1,5 = 50,4 → 50 ;
        // riposte (frappe de réaction de 0,5, sans critique) à l'impact subi au tick 20 : 28 × 0,5 × 1,5 = 21 (14 sans modificateur).
        var mods = Mods((Passif, new[] { Mod("damageDealt", "0.5") }));
        CharacterSetup bastion = Bearer(mods, triggers: new[] { Riposte });
        var commands = new[] { Cmd(0, Frappe, target: "assaillant") };
        var (_, events, _) = Fight(commands, NoUpkeep, 2000, bastion, Assailant(intervalMs: 60_000));
        List<JsonElement> hits = HitsFrom(events, "bastion");
        Assert.Equal(new[] { (0L, Frappe), (20L, (string?)null) }, hits.Select(h => (h.Tick(), h.Str("skill"))));
        DerivedStats stats = StatsOf(bastion);
        Assert.Equal(Expected(stats, Unarmored, "1.2", Fixed.Parse("0.5"), Critical(hits[0])), hits[0].Int("amount"));
        Assert.False(Critical(hits[1]));
        Assert.Equal(21L, hits[1].Int("amount"));
        Assert.Equal(21L, Expected(stats, Unarmored, "0.5", Fixed.Parse("0.5"), false));
        Assert.Equal(1, hits[1].GetProperty("depth").GetInt32());

        // Sans le modificateur, mêmes tirages (le critique ne dépend pas des modificateurs) : Frappe sans bonus, riposte à 14.
        List<JsonElement> plain = HitsFrom(Fight(commands, NoUpkeep, 2000, Bearer(null, triggers: new[] { Riposte }), Assailant(intervalMs: 60_000)).Events, "bastion");
        Assert.Equal(Critical(hits[0]), Critical(plain[0]));
        Assert.Equal(new[] { Expected(stats, Unarmored, "1.2", Fixed.Zero, Critical(hits[0])), 14L }, plain.Select(h => h.Int("amount")));
    }

    [Fact]
    public void Degats_infliges_pendant_la_garde_lus_sur_l_etat_de_l_attaquant_pour_une_frappe_de_reaction()
    {
        // [conditions]. damageDealt +0,5 {whileGuarding} chez le porteur de la riposte : la condition est lue sur l'attaquant (le
        // porteur), en garde au tick 20 (riposte 21), plus en garde au tick 60 (riposte 14).
        var mods = Mods((Passif, new[] { Mod("damageDealt", "0.5", null, ModifierCondition.WhileGuarding) }));
        var (_, events, _) = Fight(new[] { GuardStart(0), GuardStop(2500) }, NoUpkeep, 4000, Bearer(mods, triggers: new[] { Riposte }), Assailant());
        Assert.Equal(new[] { (20L, "guard", 40L), (60L, "none", 100L) }, HitsOn(events, "bastion"));
        Assert.Equal(new[] { (20L, "none", 21L), (60L, "none", 14L) }, HitsOn(events, "assaillant"));
    }

    [Theory]
    [InlineData("8.9", 277L)]
    [InlineData("9", 280L)]
    [InlineData("9.1", 280L)]
    [InlineData("10", 280L)]
    [InlineData("-0.5", 14L)]
    [InlineData("-1", 0L)]
    [InlineData("-10", 0L)]
    public void Degats_infliges_bornes_par_le_multiplicateur_de_la_categorie_talent(string dealt, long nonCritical)
    {
        // D2, D6 ; [bornes] N−1, N, N+1 du plafond de la catégorie (×10, pas de 0,1) et plancher 0. Percée (28) sur une cible sans
        // défense, hors critique : +8,9 → 28 × 9,9 = 277,2 → 277 ; +9 → ×10 = 280 ; +9,1 → ×10,1 ramené à ×10 → 280 (et non 283) ;
        // −0,5 → 14 ; −1 et au-delà → ×0 → 0. Les impacts critiques suivent la même formule (critique lu au journal).
        var mods = Mods((Passif, new[] { Mod("damageDealt", dealt) }));
        CharacterSetup bastion = Bearer(mods, actives: new[] { Percee, Frappe, Heurt, Gratuite });
        var (_, events, _) = Fight(Enumerable.Range(0, 5).Select(i => Cmd(i * 1000L, Percee)), null, 6000, bastion, Dummy(armor: 0));
        List<JsonElement> hits = HitsFrom(events, "bastion");
        Assert.Equal(5, hits.Count);
        DerivedStats stats = StatsOf(bastion);
        Assert.Equal(nonCritical, Expected(stats, Unarmored, "1", Fixed.Parse(dealt), false));
        Assert.Equal(hits.Select(h => Critical(h) ? Expected(stats, Unarmored, "1", Fixed.Parse(dealt), true) : nonCritical), hits.Select(h => h.Int("amount")));
    }

    [Fact]
    public void Degats_infliges_sans_effet_sur_les_ticks_de_dot()
    {
        // A12, D6 : damageDealt +0,5 entre dans les impacts directs de Brasier (magique 0,5 : 12 × 0,5 × 1,5 = 9 hors critique) et
        // d'Entaille (physique 0,5 : 28 × 0,5 × 1,5 = 21), pas dans les ticks de leurs DOT, puissance figée sans bonus : brûlure
        // 12 × 0,5 = 6, saignement 28 × 0,5 = 14 par tick (cible sans défense). Ticks identiques à ceux du combat sans modificateur.
        var mods = Mods((Passif, new[] { Mod("damageDealt", "0.5") }));
        string[] actives = { Brasier, Entaille, Frappe, Gratuite };
        var commands = new[] { Cmd(0, Brasier), Cmd(1000, Entaille) };
        CharacterSetup bastion = Bearer(mods, actives: actives);
        var (_, events, _) = Fight(commands, null, 8000, bastion, Dummy(armor: 0));
        var (_, plain, _) = Fight(commands, null, 8000, Bearer(null, actives: actives), Dummy(armor: 0));

        DerivedStats stats = StatsOf(bastion);
        List<JsonElement> hits = HitsFrom(events, "bastion");
        Assert.Equal(new[]
        {
            Expected(stats, Unarmored, "0.5", Fixed.Parse("0.5"), Critical(hits[0]), PowerType.Magique, DamageType.Magique),
            Expected(stats, Unarmored, "0.5", Fixed.Parse("0.5"), Critical(hits[1])),
        }, hits.Select(h => h.Int("amount")));
        long burn = DamageCalculator.ComputePeriodic(Fixed.FromInt(6), Unarmored, DamageType.Magique, true, DamageRules.Provisional);
        long bleed = DamageCalculator.ComputePeriodic(Fixed.FromInt(14), Unarmored, DamageType.Physique, true, DamageRules.Provisional);
        Assert.Equal((6L, 14L), (burn, bleed));
        var expectedDots = new[] { (40L, "burn", 6L), (60L, "bleed", 14L), (80L, "burn", 6L), (100L, "bleed", 14L), (120L, "burn", 6L), (140L, "bleed", 14L) };
        Assert.Equal(expectedDots, Dots(events));
        Assert.Equal(expectedDots, Dots(plain));
    }

    // =====================================================================================================================
    // Dégâts subis : impacts directs et ticks de DOT, plafond de 85 %, parade hors plafond, plafond de 75 % de la garde
    // =====================================================================================================================

    private static AssailantAttack Bleeding(long raw = 100) => Strike("entaille", raw, DamageType.Physique, applies: Apply("bleed", "0.4"));
    private static AssailantAttack Burning(long raw = 100) => Strike("brule", raw, DamageType.Magique, applies: Apply("burn", "0.4"));

    [Fact]
    public void Degats_subis_reduisent_les_impacts_directs_et_les_ticks_de_dot()
    {
        // D10 : damageTaken −0,3 permanent. Impacts directs de 100 (physique puis magique, en alternance) : 100 × 0,7 = 70. Ticks de
        // saignement et de brûlure de puissance 100 × 0,4 = 40 : r = min(0,85 ; 0,3) → 40 × 0,7 = 28. Sans modificateur : 100 et 40.
        // Ticks : saignement posé au tick 20 (60, 100, 140, renouvelé au tick 100), brûlure posée au tick 60 (100, 140, renouvelée au 140).
        var mods = Mods((Passif, new[] { Mod("damageTaken", "-0.3") }));
        AssailantSetup assailant = Assailant(attacks: new[] { Bleeding(), Burning() });
        var (_, events, _) = Fight(Array.Empty<CombatCommand>(), NoUpkeep, 8000, Bearer(mods), assailant);
        var (_, plain, _) = Fight(Array.Empty<CombatCommand>(), NoUpkeep, 8000, Bearer(null), assailant);

        Assert.Equal(new[] { 20L, 60L, 100L, 140L }.Select(t => (t, "none", 70L)), HitsOn(events, "bastion"));
        Assert.Equal(new[] { 20L, 60L, 100L, 140L }.Select(t => (t, "none", 100L)), HitsOn(plain, "bastion"));
        var ticks = new[] { (60L, "bleed"), (100L, "burn"), (100L, "bleed"), (140L, "burn"), (140L, "bleed") };
        Assert.Equal(ticks.Select(t => (t.Item1, t.Item2, 28L)), Dots(events));
        Assert.Equal(ticks.Select(t => (t.Item1, t.Item2, 40L)), Dots(plain));
        // Calcul indépendant du tick réduit : même formule que les dégâts directs, réduction après la défense.
        Assert.Equal(28L, DamageCalculator.ComputePeriodic(Fixed.FromInt(40), Unarmored, DamageType.Physique, true, DamageRules.Provisional, Fixed.Parse("0.3")));
    }

    [Fact]
    public void Degats_subis_pendant_la_garde_sur_un_tick_de_dot_lus_sur_l_etat_au_tick()
    {
        // [conditions]. damageTaken −0,5 {whileGuarding} : un tick de DOT n'est ni bloqué ni paré, mais la condition est lue sur l'état
        // de la cible au tick (D10). Saignement de 40 posé au tick 20 hors garde (impact 100) ; garde des ticks 50 à 90 : tick 60
        // sous garde → 40 × 0,5 = 20 ; ticks 100 et 140 hors garde → 40.
        var mods = Mods((Passif, new[] { Mod("damageTaken", "-0.5", null, ModifierCondition.WhileGuarding) }));
        var (_, events, _) = Fight(new[] { GuardStart(2500), GuardStop(4500) }, NoUpkeep, 8000, Bearer(mods), Assailant(intervalMs: 60_000, attacks: Bleeding()));
        Assert.Equal(new[] { (20L, "none", 100L) }, HitsOn(events, "bastion"));
        Assert.Equal(new[] { (60L, "bleed", 20L), (100L, "bleed", 40L), (140L, "bleed", 40L) }, Dots(events));
    }

    [Theory]
    [InlineData("-0.84", 1600L, 640L)]
    [InlineData("-0.85", 1500L, 600L)]
    [InlineData("-0.86", 1500L, 600L)]
    [InlineData("-1", 1500L, 600L)]
    public void Plafond_de_85_pourcent_sur_les_impacts_directs_et_les_ticks_de_dot(string damageTaken, long direct, long tick)
    {
        // [bornes] N−1, N, N+1 du plafond de 85 % (pas de 0,01) et valeur extrême −1. Impact de 10 000 sans garde : 10 000 × (1 − r) ;
        // saignement de 10 000 × 0,4 = 4 000 par tick : 4 000 × (1 − r). r = min(0,85 ; −v) : 0,84 → 1 600 et 640 ; au-delà, 1 500 et 600.
        var mods = Mods((Passif, new[] { Mod("damageTaken", damageTaken) }));
        var (_, events, _) = Fight(Array.Empty<CombatCommand>(), NoUpkeep, 8000, Bearer(mods, vitalite: 2000), Assailant(intervalMs: 60_000, attacks: Bleeding(10_000)));
        Assert.Equal(new[] { (20L, "none", direct) }, HitsOn(events, "bastion"));
        Assert.Equal(new[] { 60L, 100L, 140L }.Select(t => (t, "bleed", tick)), Dots(events));
    }

    [Fact]
    public void Parade_hors_plafond_et_degats_subis_ignores_sur_un_impact_pare()
    {
        // D6 : sur un impact paré, la réduction est celle de la parade (0,9) et les dégâts subis ne s'appliquent pas, même si la garde
        // est tenue (whileGuarding vraie). damageTaken −0,5 permanent et −0,3 {whileGuarding} : impact paré au tick 20 → 100 × 0,1 = 10
        // (et non 15 au plafond, ni 100 × 0,1 × 0,2 = 2) ; impact bloqué au tick 60 : r = min(0,85 ; 1 − 0,4 × 0,2 = 0,92) = 0,85 → 15.
        var mods = Mods((Passif, new[] { Mod("damageTaken", "-0.5"), Mod("damageTaken", "-0.3", null, ModifierCondition.WhileGuarding) }));
        var (result, events, _) = Fight(new[] { GuardStart(0), Parry(950) }, NoUpkeep, 4000, Bearer(mods), Assailant());
        Assert.Equal(new[] { (20L, "parry", 10L), (60L, "guard", 15L) }, HitsOn(events, "bastion"));
        Assert.Equal((1L, 1L), (result["bastion"].PerfectParries, result["bastion"].Blocks));
    }

    [Theory]
    [InlineData(null, 2500L)]
    [InlineData("-0.39", 1525L)]
    [InlineData("-0.4", 1500L)]
    [InlineData("-0.41", 1500L)]
    [InlineData("-1", 1500L)]
    public void Plafond_de_85_pourcent_avec_une_garde_de_75_pourcent(string? damageTaken, long amount)
    {
        // [bornes] N−1, N, N+1 du plafond de 85 % en combat réel. Garde de base 0,75 (plafond B1), impact de 10 000 bloqué :
        // r = min(0,85 ; 1 − 0,25 (1 + v)) ; v = 0 → 0,75 → 2 500 ; v = −0,39 → 1 − 0,25 × 0,61 = 0,8475 → 1 525 ;
        // v = −0,4 → exactement 0,85 → 1 500 ; v = −0,41 → 0,8525 ramenée à 0,85 → 1 500 (et non 1 475) ; v = −1 → 1 500.
        var rules = new CombatRules { GuardDamageReduction = Fixed.Parse("0.75"), GuardUpkeepPerSecond = 0 };
        var mods = damageTaken is null ? null : Mods((Passif, new[] { Mod("damageTaken", damageTaken) }));
        var (_, events, _) = Fight(new[] { GuardStart(0) }, rules, 2000, Bearer(mods, vitalite: 2000), Assailant(intervalMs: 60_000, attacks: Blow(10_000)));
        Assert.Equal(new[] { (20L, "guard", amount) }, HitsOn(events, "bastion"));
    }

    [Theory]
    [InlineData("0.14", 0.74, 2600L)]
    [InlineData("0.15", 0.75, 2500L)]
    [InlineData("0.16", 0.75, 2500L)]
    [InlineData("0.75", 0.75, 2500L)]
    public void Plafond_de_75_pourcent_de_la_garde_atteint_par_modificateur(string bonus, double effective, long amount)
    {
        // [bornes] N−1, N, N+1 du plafond de la garde. Base 0,6 + guard.damageReduction : 0,74 → 10 000 × 0,26 = 2 600 ; 0,75 et
        // au-delà ramenés à 0,75 → 2 500. La valeur effective est journalisée dans les règles du combattant.
        var mods = Mods((Passif, new[] { Mod("guard.damageReduction", bonus) }));
        var (_, events, _) = Fight(new[] { GuardStart(0) }, NoUpkeep, 2000, Bearer(mods, vitalite: 2000), Assailant(intervalMs: 60_000, attacks: Blow(10_000)));
        Assert.Equal(new[] { (20L, "guard", amount) }, HitsOn(events, "bastion"));
        Assert.Equal((decimal)effective, RulesOf(events).GetProperty("guard.damageReduction").GetDecimal());
    }

    [Fact]
    public void Plafond_de_la_garde_sur_la_somme_de_deux_sources()
    {
        // [combinaisons]. +0,4 (passif A) et +0,35 (passif B), chacune légale seule : 0,6 + 0,75 = 1,35 ramenée à 0,75 → 2 500.
        var mods = Mods((Passif, new[] { Mod("guard.damageReduction", "0.4") }), (PassifB, new[] { Mod("guard.damageReduction", "0.35") }));
        var (_, events, _) = Fight(new[] { GuardStart(0) }, NoUpkeep, 2000, Bearer(mods, vitalite: 2000, passives: new[] { Passif, PassifB }),
            Assailant(intervalMs: 60_000, attacks: Blow(10_000)));
        Assert.Equal(new[] { (20L, "guard", 2500L) }, HitsOn(events, "bastion"));
        Assert.Equal(0.75m, RulesOf(events).GetProperty("guard.damageReduction").GetDecimal());
    }

    // =====================================================================================================================
    // guard.noUpkeepBreak et guard.reductionAtZeroEndurance
    // =====================================================================================================================

    [Fact]
    public void Garde_sans_rupture_les_blocages_affaiblis_paient_ce_qui_reste()
    {
        // D4, D10. Drapeau + reductionAtZeroEndurance 0,3 ; 30 d'endurance par blocage, sans entretien. Cinq impacts de 100 :
        // blocages payés 30, 30, 30 (endurance 70, 40, 10 ; 100 × 0,4 = 40), puis affaiblis : 10 payés (→ 0) puis 0, réduction 0,3 → 70.
        // Jamais de rupture ; chaque blocage, affaibli ou non, rapporte +2 de Résonance. Garde quittée au tick 185 : la dernière vraie
        // dépense date du tick 140 (délai jusqu'au tick 160) et le blocage gratuit du tick 180 ne relance pas le délai : la régénération
        // compte dès le tick 185 et le premier point arrive au tick 186 (et non 201).
        var mods = Mods((Passif, new[] { Mod("guard.noUpkeepBreak", "1"), Mod("guard.reductionAtZeroEndurance", "0.3") }));
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, GuardCostPerBlock = 30 };
        var (result, events, _) = Fight(new[] { GuardStart(0), GuardStop(9250) }, rules, 11_000, Bearer(mods), Assailant());

        Assert.Equal(new[] { (20L, 30L, 70L), (60L, 30L, 40L), (100L, 30L, 10L), (140L, 10L, 0L), (180L, 0L, 0L) },
            events.OfKind("blocked").Select(e => (e.Tick(), e.Int("enduranceCost"), e.Int("endurance"))));
        Assert.Equal(new[] { (20L, "guard", 40L), (60L, "guard", 40L), (100L, "guard", 40L), (140L, "guard", 70L), (180L, "guard", 70L) }, HitsOn(events, "bastion"));
        Assert.Empty(events.OfKind("guardBroken"));
        Assert.Equal(new long[] { 2, 4, 6, 8, 10 }, events.OfKind("resourceGained").Where(e => e.Str("source") == "block").Select(e => e.Int("value")));
        Assert.Equal((5L, 0L, 10), (result["bastion"].Blocks, result["bastion"].GuardBreaks, result["bastion"].Resonance!.Value));
        JsonElement regen = events.OfKind("enduranceRegenerated").First();
        Assert.Equal((186L, 1L, 1L), (regen.Tick(), regen.Int("amount"), regen.Int("endurance")));
        JsonElement rulesLine = RulesOf(events);
        Assert.True(rulesLine.GetProperty("guard.noUpkeepBreak").GetBoolean());
        Assert.Equal(0.3m, rulesLine.GetProperty("guard.reductionAtZeroEndurance").GetDecimal());
    }

    [Fact]
    public void Garde_sans_rupture_l_entretien_paie_ce_qui_reste_et_le_drapeau_seul_bloque_sans_reduction()
    {
        // D4, D10. Drapeau seul (reductionAtZeroEndurance 0) ; entretien de 800/s = 40 par tick ; garde du tick 0 au tick 30.
        // Ticks 0 et 1 : 40 payés (60, 20) ; tick 2 : 20 payés sur 40 (→ 0), le reste est abandonné ; ensuite 0 payé à chaque tick,
        // ligne « guardUpkeep » toujours émise (dû positif), sans rupture. La garde tient : l'impact du tick 20 est bloqué gratuitement
        // avec 0 % de réduction → 100. Un entretien nul n'est pas une dépense : la dernière date du tick 2 (délai jusqu'au tick 22),
        // la régénération compte dès la fin de la garde au tick 30 et le premier point arrive au tick 31 (et non 50) ; 5 en fin de combat.
        var mods = Mods((Passif, new[] { Mod("guard.noUpkeepBreak", "1") }));
        var rules = new CombatRules { GuardUpkeepPerSecond = 800 };
        var (result, events, _) = Fight(new[] { GuardStart(0), GuardStop(1500) }, rules, 2000, Bearer(mods), Assailant());

        var upkeep = events.OfKind("guardUpkeep").ToList();
        Assert.Equal(Enumerable.Range(0, 30).Select(t => (long)t), upkeep.Select(e => e.Tick()));
        Assert.Equal(new long[] { 40, 40, 20 }.Concat(Enumerable.Repeat(0L, 27)), upkeep.Select(e => e.Int("amount")));
        Assert.Equal(new long[] { 60, 20 }.Concat(Enumerable.Repeat(0L, 28)), upkeep.Select(e => e.Int("endurance")));
        Assert.Empty(events.OfKind("guardBroken"));
        JsonElement stopped = Assert.Single(events.OfKind("guardStopped"));
        Assert.Equal((30L, JsonValueKind.Number), (stopped.Tick(), stopped.GetProperty("command").ValueKind));
        JsonElement blocked = Assert.Single(events.OfKind("blocked"));
        Assert.Equal((20L, 0L, 0L), (blocked.Tick(), blocked.Int("enduranceCost"), blocked.Int("endurance")));
        Assert.Equal(new[] { (20L, "guard", 100L) }, HitsOn(events, "bastion"));
        JsonElement regen = events.OfKind("enduranceRegenerated").First();
        Assert.Equal((31L, 1L, 1L), (regen.Tick(), regen.Int("amount"), regen.Int("endurance")));
        Assert.Equal((1L, 0L, 2, 5L), (result["bastion"].Blocks, result["bastion"].GuardBreaks, result["bastion"].Resonance!.Value, result["bastion"].Endurance!.Value));
    }

    [Fact]
    public void Blocage_affaibli_notifie_onBlock_comme_tout_blocage()
    {
        // D10 : un blocage affaibli est un blocage normal pour les déclencheurs. Le passif gagne 1 Charge à chaque blocage ; blocage à
        // 101 avec 100 d'endurance, drapeau actif : affaibli (100 payés), la réaction a lieu (profondeur 1, source « trigger »).
        var onBlock = new TriggerDefinition(TriggerEvent.Block, new[] { TriggerEffect.GainResource(TriggerResource.RampartCharges, Fixed.One) }, TriggerRole.Target);
        var mods = Mods((Passif, new[] { Mod("guard.noUpkeepBreak", "1") }));
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, GuardCostPerBlock = 101 };
        var (_, events, _) = Fight(new[] { GuardStart(0) }, rules, 2000, Bearer(mods, triggers: new[] { onBlock }), Assailant(intervalMs: 60_000));
        JsonElement blocked = Assert.Single(events.OfKind("blocked"));
        Assert.Equal(100L, blocked.Int("enduranceCost"));
        JsonElement gain = events.OfKind("resourceGained").Single(e => e.Str("source") == "trigger");
        Assert.Equal((20L, "rampartCharges", 1L, blocked.Int("eid"), 1), (gain.Tick(), gain.Str("resource")!, gain.Int("value"), gain.GetProperty("cause").GetInt64(), gain.GetProperty("depth").GetInt32()));
    }

    [Theory]
    [InlineData(null, "0.7", 0.6, 40L)]
    [InlineData("0.1", "0.7", 0.7, 30L)]
    [InlineData(null, "0.3", 0.3, 70L)]
    [InlineData("0.15", "0.75", 0.75, 25L)]
    public void Reduction_a_endurance_nulle_ramenee_a_la_reduction_de_garde_effective(string? guardBonus, string atZero, double effective, long amount)
    {
        // D10 : reductionAtZeroEndurance effective = min(valeur écrêtée, guard.damageReduction effective) ; [combinaisons]. Blocage
        // à 101 d'endurance avec 100 : affaibli, il paie les 100 restants et réduit de la valeur effective : 0,7 avec une garde de 0,6
        // → 0,6 → 40 ; avec une garde de 0,7 → 30 ; 0,3 → 70 ; 0,75 avec une garde de 0,75 → 25.
        var list = new List<ModifierDefinition> { Mod("guard.noUpkeepBreak", "1"), Mod("guard.reductionAtZeroEndurance", atZero) };
        if (guardBonus != null) list.Add(Mod("guard.damageReduction", guardBonus));
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, GuardCostPerBlock = 101 };
        var (_, events, _) = Fight(new[] { GuardStart(0) }, rules, 2000, Bearer(Mods((Passif, list.ToArray()))), Assailant(intervalMs: 60_000));
        JsonElement blocked = Assert.Single(events.OfKind("blocked"));
        Assert.Equal((100L, 0L), (blocked.Int("enduranceCost"), blocked.Int("endurance")));
        Assert.Equal(new[] { (20L, "guard", amount) }, HitsOn(events, "bastion"));
        Assert.Equal((decimal)effective, RulesOf(events).GetProperty("guard.reductionAtZeroEndurance").GetDecimal());
        Assert.Empty(events.OfKind("guardBroken"));
    }

    [Theory]
    [InlineData(1, 90L)]
    [InlineData(5, 70L)]
    public void Reduction_a_endurance_nulle_au_rang_1_et_au_rang_maximal(int rank, long amount)
    {
        // [rangs]. Talent alpha : reductionAtZeroEndurance 0,1 + 0,05 par rang (0,1 au rang 1, 0,3 au rang 5) ; drapeau sur le passif.
        // Blocage affaibli : 100 × 0,9 = 90 ; 100 × 0,7 = 70.
        var mods = Mods((TalentAlpha, new[] { Mod("guard.reductionAtZeroEndurance", "0.1", "0.05") }), (Passif, new[] { Mod("guard.noUpkeepBreak", "1") }));
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, GuardCostPerBlock = 101 };
        var (_, events, _) = Fight(new[] { GuardStart(0) }, rules, 2000, Bearer(mods, talentAlpha: rank), Assailant(intervalMs: 60_000));
        Assert.Equal(new[] { (20L, "guard", amount) }, HitsOn(events, "bastion"));
    }

    // =====================================================================================================================
    // guard.breakShortenPerCharge
    // =====================================================================================================================

    private static readonly string[] ChargeActives = { Recharge, Rempart, Frappe, Gratuite };

    [Theory]
    [InlineData(1, 50L)]
    [InlineData(5, 26L)]
    public void Rupture_par_impact_consomme_toutes_les_charges_et_raccourcit_la_rupture(int rank, long untilTick)
    {
        // D4, D10 ; [rangs]. Talent alpha : 500 ms + 100 ms par rang et par Charge (500 au rang 1, 900 au rang 5). 3 Charges (Recharge
        // au tick 0), garde, blocage à 101 avec 100 d'endurance au tick 20 : rupture par impact, les 3 Charges sont consommées.
        // Rupture : rang 1 : 3 000 − 3 × 500 = 1 500 ms = 30 ticks → fin au tick 50 ; rang 5 : 3 000 − 3 × 900 = 300 ms = 6 ticks → 26.
        // La garde est refusée au tick précédant la fin, acceptée au tick de la fin.
        var mods = Mods((TalentAlpha, new[] { Mod("guard.breakShortenPerCharge", "500", "100") }));
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, GuardCostPerBlock = 101 };
        var commands = new[] { Cmd(0, Recharge, target: "assaillant"), GuardStart(0), GuardStart((untilTick - 1) * 50), GuardStart(untilTick * 50) };
        var (result, events, _) = Fight(commands, rules, 4000, Bearer(mods, actives: ChargeActives, talentAlpha: rank), Assailant(intervalMs: 60_000));

        JsonElement broken = Assert.Single(events.OfKind("guardBroken"));
        Assert.Equal((20L, "insufficientEndurance", 3L), (broken.Tick(), broken.Str("reason")!, broken.Int("rampartChargesSpent")));
        JsonElement state = Applied(events, "guardBreak", "bastion");
        Assert.Equal((20L, untilTick), (state.Tick(), state.Int("untilTick")));
        Assert.Equal(untilTick, events.OfKind("stateExpired").Single(e => e.Str("state") == "guardBreak").Tick());
        JsonElement rejected = Assert.Single(events.OfKind("commandRejected"));
        Assert.Equal((untilTick - 1, "guardBroken"), (rejected.Tick(), rejected.Str("reason")!));
        Assert.Equal(new[] { 0L, untilTick }, events.OfKind("guardStarted").Select(e => e.Tick()));
        Assert.Equal(0L, Summary(events).Int("rampartCharges"));
        Assert.Equal(0, result["bastion"].RampartCharges);
    }

    [Theory]
    [InlineData("2900", 42L)]
    [InlineData("2950", 41L)]
    [InlineData("3000", 41L)]
    [InlineData("86400000", 41L)]
    public void Rupture_raccourcie_jamais_sous_un_tick(string perCharge, long untilTick)
    {
        // D10 ; [bornes] N−1, N, N+1 du plancher d'un tick. 1 Charge (Recharge puis Rempart), rupture par impact au tick 40 :
        // 3 000 − 2 900 = 100 ms = 2 ticks → 42 ; 3 000 − 2 950 = 50 ms = 1 tick → 41 ; 0 ms ou moins → ramené à 1 tick → 41.
        var mods = Mods((Passif, new[] { Mod("guard.breakShortenPerCharge", perCharge) }));
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, GuardCostPerBlock = 101 };
        var commands = new[] { Cmd(0, Recharge, target: "assaillant"), Cmd(1000, Rempart, target: "assaillant"), GuardStart(1000) };
        var (_, events, _) = Fight(commands, rules, 4000, Bearer(mods, actives: ChargeActives), Assailant(firstAttackMs: 2000, intervalMs: 60_000));
        JsonElement broken = Assert.Single(events.OfKind("guardBroken"));
        Assert.Equal((40L, 1L), (broken.Tick(), broken.Int("rampartChargesSpent")));
        Assert.Equal(untilTick, Applied(events, "guardBreak", "bastion").Int("untilTick"));
        Assert.Empty(events.OfKind("stateBlocked"));
    }

    [Fact]
    public void Rupture_par_entretien_ne_consomme_aucune_charge()
    {
        // D10 : seule une rupture par impact consomme les Charges. Entretien de 1 000/s : 50, 50, puis rupture au tick 2 (raison
        // upkeep) ; 0 Charge consommée, rupture de durée pleine (3 s → fin au tick 62), 3 Charges en fin de combat.
        var mods = Mods((Passif, new[] { Mod("guard.breakShortenPerCharge", "500") }));
        var commands = new[] { Cmd(0, Recharge, target: "assaillant"), GuardStart(0) };
        var (_, events, _) = Fight(commands, new CombatRules { GuardUpkeepPerSecond = 1000 }, 2000, Bearer(mods, actives: ChargeActives), Assailant(firstAttackMs: 9000));
        JsonElement broken = Assert.Single(events.OfKind("guardBroken"));
        Assert.Equal((2L, "upkeep", 0L), (broken.Tick(), broken.Str("reason")!, broken.Int("rampartChargesSpent")));
        Assert.Equal(62L, Applied(events, "guardBreak", "bastion").Int("untilTick"));
        Assert.Equal(3L, Summary(events).Int("rampartCharges"));
    }

    [Fact]
    public void Raccourci_effectif_nul_ne_consomme_aucune_charge()
    {
        // D10 ; [combinaisons]. +500 (passif A) et −500 (passif B) : valeur effective 0, aucune Charge consommée par la rupture par
        // impact, rupture de durée pleine (fin au tick 80), 3 Charges en fin de combat ; règles journalisées à 0.
        var mods = Mods((Passif, new[] { Mod("guard.breakShortenPerCharge", "500") }), (PassifB, new[] { Mod("guard.breakShortenPerCharge", "-500") }));
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, GuardCostPerBlock = 101 };
        var commands = new[] { Cmd(0, Recharge, target: "assaillant"), GuardStart(0) };
        var (_, events, _) = Fight(commands, rules, 2000, Bearer(mods, actives: ChargeActives, passives: new[] { Passif, PassifB }), Assailant(intervalMs: 60_000));
        JsonElement broken = Assert.Single(events.OfKind("guardBroken"));
        Assert.Equal((20L, "insufficientEndurance", 0L), (broken.Tick(), broken.Str("reason")!, broken.Int("rampartChargesSpent")));
        Assert.Equal(80L, Applied(events, "guardBreak", "bastion").Int("untilTick"));
        Assert.Equal(3L, Summary(events).Int("rampartCharges"));
        Assert.Equal(0L, RulesOf(events).GetProperty("guard.breakShortenPerCharge").GetInt64());
    }

    [Theory]
    [InlineData(false, 100L)]
    [InlineData(true, 70L)]
    public void Duree_subie_sur_la_rupture_de_garde_cumulee_au_raccourci(bool shorten, long untilTick)
    {
        // D5 ; [combinaisons]. status.guardBreak.durationTaken +1 000 : la rupture, application sans auteur direct, reçoit la durée
        // subie de son porteur : 3 000 + 1 000 = 4 000 ms → fin au tick 20 + 80 = 100 ; avec le raccourci de 500 ms par Charge et
        // 3 Charges : 3 000 + 1 000 − 1 500 = 2 500 ms → 50 ticks → fin au tick 70.
        var list = new List<ModifierDefinition> { Mod("status.guardBreak.durationTaken", "1000") };
        if (shorten) list.Add(Mod("guard.breakShortenPerCharge", "500"));
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, GuardCostPerBlock = 101 };
        var commands = new[] { Cmd(0, Recharge, target: "assaillant"), GuardStart(0) };
        var (_, events, _) = Fight(commands, rules, 6000, Bearer(Mods((Passif, list.ToArray())), actives: ChargeActives), Assailant(intervalMs: 60_000));
        Assert.Equal(untilTick, Applied(events, "guardBreak", "bastion").Int("untilTick"));
        Assert.Equal(untilTick, events.OfKind("stateExpired").Single(e => e.Str("state") == "guardBreak").Tick());
    }

    [Fact]
    public void Garde_sans_rupture_rend_le_raccourci_sans_objet()
    {
        // D10 (conséquence) ; [combinaisons]. Drapeau et raccourci sur le même porteur : le blocage à 101 avec 100 d'endurance est
        // affaibli (100 payés, réduction 0 → 100), aucune rupture, les 3 Charges restent.
        var mods = Mods((Passif, new[] { Mod("guard.noUpkeepBreak", "1"), Mod("guard.breakShortenPerCharge", "500") }));
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, GuardCostPerBlock = 101 };
        var commands = new[] { Cmd(0, Recharge, target: "assaillant"), GuardStart(0) };
        var (_, events, _) = Fight(commands, rules, 2000, Bearer(mods, actives: ChargeActives), Assailant(intervalMs: 60_000));
        Assert.Empty(events.OfKind("guardBroken"));
        Assert.Equal(new[] { (20L, "guard", 100L) }, HitsOn(events, "bastion"));
        Assert.Equal(3L, Summary(events).Int("rampartCharges"));
    }

    // =====================================================================================================================
    // Durées d'altération : durationDealt (auteur direct), durationTaken (cible), écrêtage [1 tick ou 1 période, 24 h]
    // =====================================================================================================================

    [Fact]
    public void Duree_infligee_ajoutee_par_l_auteur_direct_d_une_competence()
    {
        // D5. Gel (4 s) +1 000 ms et brûlure (6 s, période 2 s) +2 000 ms chez l'auteur, sur un mannequin sans résistance :
        // gel posé au tick 0 jusqu'au tick 100 (et non 80) ; brûlure posée au tick 20, 8 s = 4 ticks (60, 100, 140, 180).
        // Équivalence indépendante : un catalogue où le gel dure 5 s et la brûlure 8 s, sans modificateur, donne les mêmes lignes.
        string[] actives = { Givre, Brasier, Frappe, Gratuite };
        var commands = new[] { Cmd(0, Givre), Cmd(1000, Brasier) };
        var mods = Mods((Passif, new[] { Mod("status.freeze.durationDealt", "1000"), Mod("status.burn.durationDealt", "2000") }));
        var (_, events, _) = Fight(commands, null, 10_000, Bearer(mods, actives: actives), Dummy(armor: 0));
        Assert.Equal((0L, 100L), (Applied(events, "freeze", "mannequin").Tick(), Applied(events, "freeze", "mannequin").Int("untilTick")));
        JsonElement burn = Applied(events, "burn", "mannequin");
        Assert.Equal((20L, 60L, 180L), (burn.Tick(), burn.Int("nextTick"), burn.Int("untilTick")));
        Assert.Equal(new long[] { 60, 100, 140, 180 }, Dots(events).Select(d => d.Tick));

        var longer = new StatusCatalog(StatusCatalog.Provisional.Definitions.Select(d => d.Id switch
        {
            "freeze" => new StatusDefinition("freeze", StatusCategory.Control, 5000, StatusStacking.KeepLonger, StatusResistanceMode.Duration,
                movementSpeedModifier: Fixed.Parse("-0.3")),
            "burn" => new StatusDefinition("burn", StatusCategory.Dot, 8000, StatusStacking.PerSourceKeepStronger, StatusResistanceMode.Defense,
                periodMs: 2000, damageType: DamageType.Magique),
            _ => d,
        }));
        var (_, equivalent, _) = Fight(commands, new CombatRules { Statuses = longer }, 10_000, Bearer(null, actives: actives), Dummy(armor: 0));
        static List<string> States(List<JsonElement> e) =>
            e.Where(x => x.Str("event") is "stateApplied" or "stateExpired" or "dotDamage").Select(x => x.GetRawText()).ToList();
        Assert.Equal(States(equivalent), States(events));
    }

    [Theory]
    [InlineData(1, 90L)]
    [InlineData(5, 130L)]
    public void Duree_infligee_au_rang_1_et_au_rang_maximal(int rank, long untilTick)
    {
        // [rangs]. Talent alpha : gel +500 ms + 500 ms par rang (500 au rang 1 → 4,5 s = 90 ticks ; 2 500 au rang 5 → 6,5 s = 130 ticks).
        var mods = Mods((TalentAlpha, new[] { Mod("status.freeze.durationDealt", "500", "500") }));
        var (_, events, _) = Fight(new[] { Cmd(0, Givre) }, null, 2000, Bearer(mods, actives: new[] { Givre, Frappe, Heurt, Gratuite }, talentAlpha: rank), Dummy(armor: 0));
        Assert.Equal(untilTick, Applied(events, "freeze", "mannequin").Int("untilTick"));
    }

    [Fact]
    public void Duree_infligee_ajoutee_par_le_porteur_d_un_declencheur_qui_applique()
    {
        // D10 : pour l'effet applyStatus d'un déclencheur, l'auteur direct est le porteur. Au lancement de Gratuite (tick 0), le passif
        // pose le gel sur la cible de la commande : 4 s + 1 000 ms = 100 ticks (et non 80), source = porteur, profondeur 1.
        var trigger = new TriggerDefinition(TriggerEvent.SkillCast, new[] { TriggerEffect.ApplyStatus(TriggerTarget.EventTarget, "freeze") }, TriggerRole.Self);
        var mods = Mods((Passif, new[] { Mod("status.freeze.durationDealt", "1000") }));
        var (_, events, _) = Fight(new[] { Cmd(0, Gratuite) }, null, 2000, Bearer(mods, triggers: new[] { trigger }), Dummy(armor: 0));
        JsonElement freeze = Applied(events, "freeze", "mannequin");
        Assert.Equal((0L, 100L, "bastion", 1), (freeze.Tick(), freeze.Int("untilTick"), freeze.Str("source")!, freeze.GetProperty("depth").GetInt32()));
        var (_, plain, _) = Fight(new[] { Cmd(0, Gratuite) }, null, 2000, Bearer(null, triggers: new[] { trigger }), Dummy(armor: 0));
        Assert.Equal(80L, Applied(plain, "freeze", "mannequin").Int("untilTick"));
    }

    [Fact]
    public void Duree_subie_ajoutee_sur_la_cible()
    {
        // D5 : durationTaken s'applique à toute application sur le porteur, ici par un assaillant (sans modificateur). Fragilité (6 s)
        // +1 000 ms posée au tick 20 → fin au tick 160 (et non 140) ; saignement (6 s, période 2 s) +2 000 ms posé au tick 60 → 8 s :
        // prochain tick 100, fin au tick 220 (et non 180).
        var mods = Mods((Passif, new[] { Mod("status.fragility.durationTaken", "1000"), Mod("status.bleed.durationTaken", "2000") }));
        var (_, events, _) = Fight(Array.Empty<CombatCommand>(), NoUpkeep, 4000, Bearer(mods), Assailant(attacks: new[] { Blow(fragility: true), Bleeding() }));
        Assert.Equal((20L, 160L), (Applied(events, "fragility", "bastion").Tick(), Applied(events, "fragility", "bastion").Int("untilTick")));
        JsonElement bleed = Applied(events, "bleed", "bastion");
        Assert.Equal((60L, 100L, 220L), (bleed.Tick(), bleed.Int("nextTick"), bleed.Int("untilTick")));
    }

    [Fact]
    public void Durees_infligee_et_subie_cumulees_puis_resistance()
    {
        // D5 ; [combinaisons]. alpha (durationDealt : brûlure +2 000, gel +1 000) attaque beta (durationTaken : brûlure +2 000, gel +2 000).
        // Brûlure posée au tick 0 : 6 + 2 + 2 = 10 s = 5 périodes, fin au tick 200. Gel posé au tick 20 : 4 + 1 + 2 = 7 s = 140 ticks,
        // puis la résistance de beta (20) : ⌊140 × 0,9803⌋ = ⌊137,24⌋ = 137 → fin au tick 157.
        var mods = Mods(
            (Passif, new[] { Mod("status.burn.durationDealt", "2000"), Mod("status.freeze.durationDealt", "1000") }),
            (PassifB, new[] { Mod("status.burn.durationTaken", "2000"), Mod("status.freeze.durationTaken", "2000") }));
        CharacterSetup alpha = Bearer(mods, "alpha", actives: new[] { Brasier, Givre, Frappe, Gratuite });
        CharacterSetup beta = Bearer(mods, "beta", "rivaux", passives: new[] { PassifB });
        var (_, events, _) = Fight(new[] { Cmd(0, Brasier, "alpha", "beta"), Cmd(1000, Givre, "alpha", "beta") }, null, 12_000, alpha, beta);
        JsonElement burn = Applied(events, "burn", "beta");
        Assert.Equal((0L, 40L, 200L), (burn.Tick(), burn.Int("nextTick"), burn.Int("untilTick")));
        Assert.Equal(new long[] { 40, 80, 120, 160, 200 }, Dots(events).Select(d => d.Tick));
        JsonElement freeze = Applied(events, "freeze", "beta");
        Assert.Equal((20L, 157L), (freeze.Tick(), freeze.Int("untilTick")));
    }

    [Theory]
    [InlineData("86395950", 1_727_999L)]
    [InlineData("86396000", 1_728_000L)]
    [InlineData("86396050", 1_728_000L)]
    [InlineData("86400000", 1_728_000L)]
    public void Duree_modifiee_plafonnee_a_24_heures(string dealt, long untilTick)
    {
        // D10 ; [bornes] N−1, N, N+1 du plafond de 24 h (pas d'un tick). Gel (4 000 ms) + durationDealt sur un mannequin sans
        // résistance : 4 000 + 86 395 950 = 86 399 950 ms = 1 727 999 ticks ; 86 400 000 ms = 1 728 000 ticks ; au-delà, ramené à 24 h.
        var mods = Mods((Passif, new[] { Mod("status.freeze.durationDealt", dealt) }));
        var (_, events, _) = Fight(new[] { Cmd(0, Givre) }, null, 2000, Bearer(mods, actives: new[] { Givre, Frappe, Heurt, Gratuite }), Dummy(armor: 0));
        Assert.Equal(untilTick, Applied(events, "freeze", "mannequin").Int("untilTick"));
    }

    [Fact]
    public void Duree_plafonnee_a_24_heures_avant_la_resistance()
    {
        // D5 : durée ajustée écrêtée à 24 h, puis conversion en ticks, puis facteur de résistance. alpha +86 400 000 (durationDealt),
        // beta +86 400 000 (durationTaken) : 24 h = 1 728 000 ticks, × 0,9803 → ⌊1 693 958,4⌋ = 1 693 958 ticks (gel posé au tick 0).
        var mods = Mods((Passif, new[] { Mod("status.freeze.durationDealt", "86400000") }), (PassifB, new[] { Mod("status.freeze.durationTaken", "86400000") }));
        CharacterSetup alpha = Bearer(mods, "alpha", actives: new[] { Givre, Frappe, Heurt, Gratuite });
        CharacterSetup beta = Bearer(mods, "beta", "rivaux", passives: new[] { PassifB });
        var (_, events, _) = Fight(new[] { Cmd(0, Givre, "alpha", "beta") }, null, 2000, alpha, beta);
        Assert.Equal(1_693_958L, Applied(events, "freeze", "beta").Int("untilTick"));
    }

    [Theory]
    [InlineData("-5900", 22L)]
    [InlineData("-5950", 21L)]
    [InlineData("-6000", 21L)]
    [InlineData("-86400000", 21L)]
    public void Duree_modifiee_jamais_sous_un_tick(string taken, long untilTick)
    {
        // D10 ; [bornes] N−1, N, N+1 du plancher d'un tick. Fragilité (6 000 ms) posée au tick 20 avec durationTaken : 100 ms = 2 ticks
        // → 22 ; 50 ms = 1 tick → 21 ; 0 ms ou moins → ramenée à 1 tick → 21. Jamais « resisted » : la ligne est un stateApplied.
        var mods = Mods((Passif, new[] { Mod("status.fragility.durationTaken", taken) }));
        var (_, events, _) = Fight(Array.Empty<CombatCommand>(), NoUpkeep, 2000, Bearer(mods), Assailant(intervalMs: 60_000, attacks: Blow(fragility: true)));
        Assert.Equal(untilTick, Applied(events, "fragility", "bastion").Int("untilTick"));
        Assert.Equal(untilTick, events.OfKind("stateExpired").Single(e => e.Str("state") == "fragility").Tick());
        Assert.Empty(events.OfKind("stateBlocked"));
    }

    [Theory]
    [InlineData("-2000", 100L, new long[] { 60, 100 })]
    [InlineData("-4000", 60L, new long[] { 60 })]
    [InlineData("-6000", 60L, new long[] { 60 })]
    [InlineData("-86400000", 60L, new long[] { 60 })]
    public void Duree_de_dot_modifiee_jamais_sous_une_periode(string taken, long untilTick, long[] ticks)
    {
        // D10 ; [bornes] N−1, N, N+1 du plancher d'une période (pas d'une période). Saignement (6 s, période 2 s) posé au tick 20 :
        // −2 000 → 4 s, 2 ticks (60, 100) ; −4 000 → exactement une période, 1 tick (60) ; au-delà, ramené à une période.
        var mods = Mods((Passif, new[] { Mod("status.bleed.durationTaken", taken) }));
        var (_, events, _) = Fight(Array.Empty<CombatCommand>(), NoUpkeep, 8000, Bearer(mods), Assailant(intervalMs: 60_000, attacks: Bleeding()));
        JsonElement bleed = Applied(events, "bleed", "bastion");
        Assert.Equal((60L, untilTick), (bleed.Int("nextTick"), bleed.Int("untilTick")));
        Assert.Equal(ticks, Dots(events).Select(d => d.Tick));
        Assert.All(Dots(events), d => Assert.Equal(40L, d.Amount));
    }

    /// <summary>Catalogue de test : l'étourdissement accorde à son expiration le gel, altération aussi applicable directement (Givre).</summary>
    private static readonly StatusCatalog StunGrantsFreeze = new(StatusCatalog.Provisional.Definitions.Select(d => d.Id == "stun"
        ? new StatusDefinition("stun", StatusCategory.Control, 1500, StatusStacking.Ignore, StatusResistanceMode.Duration,
            blocks: StatusBlocks.Skills | StatusBlocks.Guard | StatusBlocks.Parry | StatusBlocks.AssailantAttacks,
            interrupts: StatusInterrupts.Guard | StatusInterrupts.Parry, blockReason: "stunned", grantsOnExpiry: "freeze")
        : d));

    [Fact]
    public void Etat_accorde_a_l_expiration_sans_la_duree_infligee_de_l_auteur_d_origine()
    {
        // D10 : un état accordé ne reçoit jamais le durationDealt de l'auteur de l'état d'origine. Gel +1 000 ms chez l'auteur.
        // Givre sur cible1 au tick 0 : application directe, 5 s → fin au tick 100. Assommer sur cible2 au tick 20 : étourdissement de
        // 30 ticks, expiré au tick 50, qui accorde le gel (source = l'auteur d'origine) de durée de base : 80 ticks → fin au tick 130
        // (et non 150). Le même combat sans modificateur donne le même gel accordé.
        string[] actives = { Givre, Assommer, Frappe, Gratuite };
        var commands = new[] { Cmd(0, Givre, target: "cible1"), Cmd(1000, Assommer, target: "cible2") };
        var rules = new CombatRules { Statuses = StunGrantsFreeze };
        Assert.Empty(rules.Validate());
        var mods = Mods((Passif, new[] { Mod("status.freeze.durationDealt", "1000") }));
        var (_, events, _) = Fight(commands, rules, 8000, Bearer(mods, actives: actives), Dummy("cible1", armor: 0), Dummy("cible2", armor: 0));

        Assert.Equal(100L, Applied(events, "freeze", "cible1").Int("untilTick"));
        Assert.Equal((20L, 50L), (Applied(events, "stun", "cible2").Tick(), Applied(events, "stun", "cible2").Int("untilTick")));
        JsonElement granted = Applied(events, "freeze", "cible2");
        Assert.Equal((50L, "expirations", "bastion", 130L), (granted.Tick(), granted.Str("phase")!, granted.Str("source")!, granted.Int("untilTick")));

        var (_, plain, _) = Fight(commands, rules, 8000, Bearer(null, actives: actives), Dummy("cible1", armor: 0), Dummy("cible2", armor: 0));
        Assert.Equal((80L, 130L), (Applied(plain, "freeze", "cible1").Int("untilTick"), Applied(plain, "freeze", "cible2").Int("untilTick")));
    }

    [Fact]
    public void Etat_accorde_recoit_la_duree_subie_de_son_porteur_mais_pas_la_duree_infligee()
    {
        // D5, D10 : durationTaken s'applique à toute application sur le porteur, état accordé compris. alpha (gel +1 000 infligé) étourdit
        // beta (gel +1 000 subi) au tick 20 : ⌊30 × 0,9803⌋ = 29 ticks → expiration au tick 49 ; gel accordé : 4 + 1 = 5 s = 100 ticks,
        // ⌊100 × 0,9803⌋ = 98 → fin au tick 147 (avec la durée infligée, à tort : 6 s → ⌊120 × 0,9803⌋ = 117 → 166).
        var mods = Mods((Passif, new[] { Mod("status.freeze.durationDealt", "1000") }), (PassifB, new[] { Mod("status.freeze.durationTaken", "1000") }));
        CharacterSetup alpha = Bearer(mods, "alpha", actives: new[] { Assommer, Givre, Frappe, Gratuite });
        CharacterSetup beta = Bearer(mods, "beta", "rivaux", passives: new[] { PassifB });
        var (_, events, _) = Fight(new[] { Cmd(1000, Assommer, "alpha", "beta") }, new CombatRules { Statuses = StunGrantsFreeze }, 8000, alpha, beta);
        Assert.Equal(49L, Applied(events, "stun", "beta").Int("untilTick"));
        JsonElement granted = Applied(events, "freeze", "beta");
        Assert.Equal((49L, "alpha", 147L), (granted.Tick(), granted.Str("source")!, granted.Int("untilTick")));
    }

    [Theory]
    [InlineData(9_000_000L, null, null)]
    [InlineData(9_000_000L, "86396000", 172L)]
    [InlineData(1_000_000_000L, "86396000", null)]
    public void Resiste_seulement_par_la_resistance(long resistance, string? dealt, long? untilTick)
    {
        // D10 : seule la résistance peut ramener une application à 0 tick (« resisted ») ; un modificateur allonge avant le facteur de
        // résistance. Résistance de 9·10⁶ : facteur ⌊1000 / 9 001 000⌋ à 4 décimales = 0,0001 ; gel de base : ⌊80 × 0,0001⌋ = 0 →
        // résisté ; gel de 24 h : ⌊1 728 000 × 0,0001⌋ = 172 ticks. Résistance de 10⁹ : facteur 0, résisté même à 24 h.
        var mods = dealt is null ? null : Mods((Passif, new[] { Mod("status.freeze.durationDealt", dealt) }));
        var target = new DummySetup("mannequin", "cibles", Fixed.Zero, Fixed.Zero, statusResistance: Fixed.FromInt(resistance));
        var (_, events, _) = Fight(new[] { Cmd(0, Givre) }, null, 2000, Bearer(mods, actives: new[] { Givre, Frappe, Heurt, Gratuite }), target);
        if (untilTick is long until)
        {
            Assert.Equal(until, Applied(events, "freeze", "mannequin").Int("untilTick"));
            Assert.Empty(events.OfKind("stateBlocked"));
        }
        else
        {
            JsonElement blocked = Assert.Single(events.OfKind("stateBlocked"));
            Assert.Equal(("freeze", "resisted"), (blocked.Str("state")!, blocked.Str("reason")!));
            Assert.Empty(events.OfKind("stateApplied"));
        }
    }

    // =====================================================================================================================
    // Déterminisme et rejeu
    // =====================================================================================================================

    [Fact]
    public void Combat_avec_modificateurs_conditionnels_et_drapeaux_deterministe_et_rejouable()
    {
        // [déterminisme]. Conditionnels de dégâts subis et infligés, drapeaux de garde et durées dans un même combat : deux simulations
        // donnent le même journal octet par octet, et le journal d'une simulation pas à pas est celui de CombatEngine.Run.
        var mods = Mods(
            (TalentAlpha, new[] { Mod("damageTaken", "-0.02", "-0.02", ModifierCondition.WhileGuarding, ModifierCondition.PerRampartCharge) }),
            (Passif, new[] { Mod("guard.noUpkeepBreak", "1"), Mod("guard.reductionAtZeroEndurance", "0.2"), Mod("damageDealt", "0.05", null, ModifierCondition.PerResonanceStep) }),
            (PassifB, new[] { Mod("status.bleed.durationTaken", "2000"), Mod("damageTaken", "-0.1", null, ModifierCondition.WhileGuarding) }));
        string[] passives = { Passif, PassifB };
        CombatSetup Build() => Setup(new[]
            {
                Cmd(0, Recharge, target: "assaillant"), GuardStart(0), GuardStop(3500), Cmd(3500, Percee, target: "assaillant"),
                Cmd(4500, Percee, target: "assaillant"), GuardStart(5000),
            },
            new CombatantSetup[]
            {
                Bearer(mods, actives: new[] { Recharge, Percee, Rempart, Gratuite }, talentAlpha: 5, passives: passives, triggers: new[] { Riposte }),
                Assailant(attacks: new[] { Bleeding(), Burning() }),
            }, durationMs: 10_000, rules: new CombatRules { GuardCostPerBlock = 40 });
        var (first, log1) = Run(Build());
        var (_, log2) = Run(Build());
        Assert.NotEqual(CombatEndReason.Aborted, first.EndReason);
        Assert.True(CombatReplay.Compare(log1.Lines.ToList(), log2.Lines.ToList()).Identical);
        var stepped = new MemoryCombatLog();
        var engine = new CombatEngine(Build(), stepped);
        while (engine.Step()) { }
        Assert.True(CombatReplay.Compare(log1.Lines.ToList(), stepped.Lines.ToList()).Identical);
        Assert.NotEmpty(Events(log1).OfKind("blocked"));
    }
}
