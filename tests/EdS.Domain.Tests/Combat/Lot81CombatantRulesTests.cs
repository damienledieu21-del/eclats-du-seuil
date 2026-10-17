using System.Reflection;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Domain.Tests.Regression;
using FsCheck.Xunit;
using static EdS.Domain.Tests.Combat.Lot81Kit;

namespace EdS.Domain.Tests.Combat;

// Sous-lot 8.1 : tests unitaires des règles effectives par combattant (design81.md D2, D3, D6, D7 ; D10 fait foi). Aucune simulation :
// ModifierSet, CombatantRules, CombatantStats.ForCharacter et CombatantRules.TotalReduction seuls.
//
// Correspondance avec la liste obligatoire des tests 8.1 :
//   - chaque clé au rang 1 et au rang maximal ........... Lot81CombatantRulesBindingTests.Chaque_cle_au_rang_1_et_au_rang_maximal
//   - bornes N − 1, N et N + 1 .......................... Lot81CombatantRulesBoundsTests (chaque valeur écrêtée), Conditional (−1),
//                                                          TotalReduction (0,85), Stats (PV max −90 % et +1 000 %, endurance, résistance)
//   - combinaisons de modificateurs ..................... Aggregation (sommes, regroupement), Binding (toutes les clés à la fois),
//                                                          Bounds (deux sources pour dépasser), Conditional (ensembles combinés)
//   - conditions vraies et fausses dans le même combat .. Conditional (même combattant évalué dans les deux états, sans état gardé)
//   - agrégation indépendante de l'ordre ................ Aggregation (exemple explicite et propriété FsCheck par permutation)
//   - immutabilité ...................................... Immutability (collections en lecture seule, copies, aucun mutateur public)
//   - débordements refusés avant la simulation .......... Immutability (enveloppe déclarée sans débordement ; au-delà, exception vérifiée,
//                                                          jamais d'enroulement) ; le refus par le précontrôle du combat est testé ailleurs
//   - pas de mutation partielle ......................... Immutability (échec au milieu de l'agrégation sans effet observable)
//   - déterminisme et rejeu ............................. Immutability (fonction pure), propriétés FsCheck à graine fixe ; le rejeu des
//                                                          journaux est hors de ce fichier
//   - non-régression des 5 scénarios livrés ............. hors de ce fichier (Lot81ReferenceTests, Lot81EngineBaselineTests)

/// <summary>Outils communs aux tests de ce fichier : modificateurs de test, ensembles agrégés, lecture d'une valeur résultante par clé.</summary>
internal static class Lot81Kit
{
    public const string Frappe = "skill.essai.frappe";
    public const ModifierCondition W = ModifierCondition.WhileGuarding;
    public const ModifierCondition C = ModifierCondition.PerRampartCharge;
    public const ModifierCondition R = ModifierCondition.PerResonanceStep;

    /// <summary>Attributs du personnage de test : PV max 100 + 10 × 14 = 240, endurance max 100 + 2 × 0 = 100, résistance 2 × 10 = 20.</summary>
    public static readonly Attributes Attrs = new(14, 8, 6, 14, 10, 0);

    public static readonly CharacterBuild Build = new("class.essai", 10, Attrs);

    public static Fixed F(string text) => Fixed.Parse(text);

    public static ModifierDefinition Mod(string key, string add, string? perRank = null, params ModifierCondition[] conditions) =>
        new(key, new RankedValue(F(add), perRank is null ? Fixed.Zero : F(perRank)), conditions);

    public static ModifierDefinition Mod(string key, Fixed add, params ModifierCondition[] conditions) => new(key, new RankedValue(add), conditions);

    /// <summary>Modificateur rattaché à une source (talent de test) au rang donné.</summary>
    public static BoundModifier Bound(ModifierDefinition modifier, int source = 0, int rank = 1) =>
        new($"talent.essai.s{source}.a", 0, rank, modifier);

    /// <summary>Chaque modificateur sur sa propre source (talent distinct, rang 1) : des sources différentes s'additionnent (D2).</summary>
    public static ModifierSet Set(params ModifierDefinition[] modifiers) => ModifierSet.From(modifiers.Select((m, i) => Bound(m, i)));

    /// <summary>Une source par valeur de la liste « a;b;c », toutes sur la même clé ; liste vide = aucun modificateur.</summary>
    public static ModifierSet Set(string key, string adds) => Set(Split(adds).Select(a => Mod(key, a)).ToArray());

    public static string[] Split(string list) => list.Length == 0 ? Array.Empty<string>() : list.Split(';');

    public static CombatantRules For(CombatRules rules, params ModifierDefinition[] modifiers) => CombatantRules.For(rules, Set(modifiers));

    public static CombatantRules For(params ModifierDefinition[] modifiers) => For(CombatRules.Provisional, modifiers);

    /// <summary>Compétence lançable de test : coefficient 1,2 et recharge 6 s par défaut.</summary>
    public static SkillDefinition Skill(Fixed? coefficient = null, long cooldownMs = 6000) =>
        new(Frappe, "Frappe", SkillKind.Active,
            combat: new SkillCombatDefinition(PowerType.Physique, DamageType.Physique, coefficient ?? F("1.2"), 10, cooldownMs));

    public static ModifierKey Key(string text)
    {
        Assert.True(ModifierKeys.TryParse(text, out ModifierKey key), text);
        return key;
    }

    /// <summary>Valeur résultante d'une clé : règle effective, statistique du personnage ou valeur effective d'une compétence.</summary>
    public static string Observe(ModifierKey key, CombatantRules r, DerivedStats stats, SkillDefinition skill) => key.Field switch
    {
        ModifierField.GuardDamageReduction => r.GuardDamageReduction.ToString(),
        ModifierField.GuardUpkeepPerSecond => r.GuardUpkeepPerSecond.ToString(),
        ModifierField.GuardCostPerBlock => r.GuardCostPerBlock.ToString(),
        ModifierField.GuardNoUpkeepBreak => r.GuardNoUpkeepBreak ? "true" : "false",
        ModifierField.GuardReductionAtZeroEndurance => r.GuardReductionAtZeroEndurance.ToString(),
        ModifierField.GuardBreakShortenPerCharge => r.GuardBreakShortenPerChargeMs.ToString(),
        ModifierField.ParryWindowMs => r.ParryWindowMs.ToString(),
        ModifierField.ParryFailureRecoveryMs => r.ParryFailureRecoveryMs.ToString(),
        ModifierField.RampartChargesMax => r.RampartChargesMax.ToString(),
        ModifierField.ResonancePerDirectHit => r.ResonancePerDirectHit.ToString(),
        ModifierField.EnduranceMax => stats.MaxEndurance.ToString(),
        ModifierField.EnduranceRegenPerSecond => r.EnduranceRegenPerSecond.ToString(),
        ModifierField.MaxHealth => stats.MaxHealth.ToString(),
        ModifierField.StatusResistance => stats.StatusResistance.ToString(),
        ModifierField.DamageDealt => r.DamageDealt.ToString(),
        ModifierField.DamageTaken => r.DamageTaken.ToString(),
        ModifierField.StatusDurationDealt => r.DurationDealtMs(key.Parameter!).ToString(),
        ModifierField.StatusDurationTaken => r.DurationTakenMs(key.Parameter!).ToString(),
        ModifierField.SkillCoefficient => r.Coefficient(skill, skill.Combat!).ToString(),
        ModifierField.SkillCooldownMs => r.CooldownMs(skill, skill.Combat!).ToString(),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key.Field, null),
    };

    /// <summary>Valeur résultante d'une clé avec cet ensemble, sur des règles données et le personnage de test.</summary>
    public static string Value(string key, CombatRules rules, ModifierSet set, SkillDefinition? skill = null, StatRules? stats = null) =>
        Observe(Key(key), CombatantRules.For(rules, set), CombatantStats.ForCharacter(stats ?? StatRules.Provisional, Build, set), skill ?? Skill());

    /// <summary>Valeur résultante d'une clé avec une source par valeur de « a;b;c ».</summary>
    public static string Value(string key, string adds, CombatRules? rules = null, SkillDefinition? skill = null) =>
        Value(key, rules ?? CombatRules.Provisional, Set(key, adds), skill);

    /// <summary>
    /// Somme répartie en sources de même signe d'au plus 10⁶ chacune (borne déclarée des clés entières et des décimaux larges, D2) : un
    /// total proche de 2³¹ demande ainsi un peu plus de 2 000 sources, sous les 4 096 qu'un combattant peut avoir (D3).
    /// </summary>
    public static ModifierSet Chunked(string key, Fixed total)
    {
        long chunk = 1_000_000 * Fixed.Scale, left = total.Raw, sign = Math.Sign(left);
        var modifiers = new List<ModifierDefinition>();
        while (left != 0)
        {
            long part = sign * Math.Min(chunk, Math.Abs(left));
            modifiers.Add(Mod(key, Fixed.FromRaw(part)));
            left -= part;
        }
        return Set(modifiers.ToArray());
    }

    /// <summary>Empreinte de toutes les valeurs observables d'un combattant : deux constructions égales donnent la même empreinte.</summary>
    public static string Snapshot(CombatantRules r)
    {
        SkillDefinition skill = Skill();
        return string.Join("|", new object[]
        {
            r.GuardDamageReduction, r.GuardUpkeepPerSecond, r.GuardCostPerBlock, r.GuardNoUpkeepBreak, r.GuardReductionAtZeroEndurance,
            r.GuardBreakShortenPerChargeMs, r.ParryWindowMs, r.ParryFailureRecoveryMs, r.RampartChargesMax, r.ResonancePerDirectHit,
            r.EnduranceRegenPerSecond, r.DamageDealt, r.DamageTaken, r.Coefficient(skill, skill.Combat!), r.CooldownMs(skill, skill.Combat!),
            r.DurationDealtMs("burn"), r.DurationTakenMs("bleed"), r.DamageDealtAt(true, 3, 25), r.DamageDealtAt(false, 0, 0),
            r.DamageTakenAt(true, 3, 25), r.DamageTakenAt(false, 5, 99), r.WorstDamageDealt(100),
            string.Join(",", r.Modifiers.Keys.Select(k => $"{k}={r.Modifiers.Sum(k)}")),
            string.Join(",", r.Modifiers.Conditional.Select(c => $"{c.Field}/{c.Conditions}={c.Value}")),
        });
    }

    /// <summary>Statistiques comparables propriété par propriété.</summary>
    public static string StatsText(DerivedStats s) => string.Join("|", new object[]
    {
        s.PhysicalPower, s.AgilePower, s.MagicPower, s.Armor, s.MagicResistance, s.MaxHealth, s.MaxEndurance, s.StatusResistance,
        s.CriticalChanceBasisPoints, s.CriticalMultiplier,
    });

    /// <summary>Plancher entier d'une valeur positive, comme le moteur et la validation le prennent pour les PV max.</summary>
    public static long Floor(Fixed value) => value.Raw / Fixed.Scale;
}

/// <summary>
/// Sous-lot 8.1 (D3) : agrégation des modificateurs actifs (<see cref="ModifierSet"/>). Somme exacte par clé, clés en ordre ordinal,
/// conditionnels regroupés par (clé, ensemble de conditions) puis triés, ordre des sources sans effet (exemple et propriété) ; ensemble
/// vide = règles de base, propriété par propriété, y compris pour des règles hors des intervalles de jeu.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Engine")]
public class Lot81CombatantRulesAggregationTests
{
    [Fact]
    public void Sommes_exactes_par_cle_et_cles_en_ordre_ordinal()
    {
        ModifierSet set = Set(
            Mod("status.burn.durationDealt", "1000"),
            Mod("guard.damageReduction", "0.04"),
            Mod("skill.essai.frappe.coefficient", "0.2"),
            Mod("guard.damageReduction", "0.0275"),   // autre source, même clé : s'additionne (0,0675)
            Mod("parry.windowMs", "50"),
            Mod("status.bleed.durationDealt", "500"),
            Mod("damageDealt", "0.1"),
            Mod("parry.windowMs", "-100"));
        // Ordre ordinal du texte de la clé : « d » < « g » < « p » < « sk » < « st », puis « status.bleed » < « status.burn ».
        Assert.Equal(new[]
        {
            "damageDealt", "guard.damageReduction", "parry.windowMs", "skill.essai.frappe.coefficient",
            "status.bleed.durationDealt", "status.burn.durationDealt",
        }, set.Keys.Select(k => k.ToString()));
        Assert.Equal(F("0.0675"), set.Sum(ModifierField.GuardDamageReduction));
        Assert.Equal(F("-50"), set.Sum(ModifierField.ParryWindowMs));
        Assert.Equal(F("0.1"), set.Sum(ModifierField.DamageDealt));
        Assert.Equal(F("0.2"), set.Sum(ModifierField.SkillCoefficient, Frappe));
        Assert.Equal(F("1000"), set.Sum(ModifierField.StatusDurationDealt, "burn"));
        Assert.Equal(F("500"), set.Sum(ModifierField.StatusDurationDealt, "bleed"));
        // Clé non ciblée : somme nulle et Targets faux ; un paramètre ou un suffixe différent est une autre clé.
        Assert.Equal(Fixed.Zero, set.Sum(ModifierField.StatusDurationDealt, "stun"));
        Assert.False(set.Targets(new ModifierKey(ModifierField.StatusDurationTaken, "burn")));
        Assert.False(set.Targets(new ModifierKey(ModifierField.GuardCostPerBlock)));
        Assert.False(set.Flag(ModifierField.GuardNoUpkeepBreak));
        Assert.True(set.Targets(new ModifierKey(ModifierField.ParryWindowMs)));
        Assert.Empty(set.Conditional);
    }

    [Fact]
    public void Conditionnels_regroupes_par_cle_et_ensemble_de_conditions_puis_tries()
    {
        // Déclarés dans le désordre, conditions écrites dans les deux ordres : regroupés par (clé, ensemble), triés par clé
        // (damageDealt < damageTaken) puis par ensemble ({W} = 1, {C} = 2, {W,C} = 3, {R} = 4, {W,R} = 5).
        ModifierSet set = Set(
            Mod("damageTaken", "-0.01", null, W),
            Mod("damageDealt", "0.02", null, R),
            Mod("damageDealt", "0.03", null, C, W),
            Mod("damageDealt", "0.01", null, W),
            Mod("damageDealt", "0.04", null, W, C),     // même ensemble que {C, W} : 0,03 + 0,04
            Mod("damageDealt", "0.05", null, C),
            Mod("damageDealt", "0.06", null, R, W),
            Mod("damageTaken", "-0.02", null, C),
            Mod("damageTaken", "-0.005", null, W),      // même ensemble que la première : −0,015
            Mod("damageDealt", "0.1"));                 // permanent : dans les sommes, pas dans les conditionnels
        Assert.Equal(new[]
        {
            (ModifierField.DamageDealt, ModifierConditions.WhileGuarding, F("0.01")),
            (ModifierField.DamageDealt, ModifierConditions.PerRampartCharge, F("0.05")),
            (ModifierField.DamageDealt, ModifierConditions.WhileGuarding | ModifierConditions.PerRampartCharge, F("0.07")),
            (ModifierField.DamageDealt, ModifierConditions.PerResonanceStep, F("0.02")),
            (ModifierField.DamageDealt, ModifierConditions.WhileGuarding | ModifierConditions.PerResonanceStep, F("0.06")),
            (ModifierField.DamageTaken, ModifierConditions.WhileGuarding, F("-0.015")),
            (ModifierField.DamageTaken, ModifierConditions.PerRampartCharge, F("-0.02")),
        }, set.Conditional.Select(c => (c.Field, c.Conditions, c.Value)));
        Assert.Equal(new[] { "damageDealt" }, set.Keys.Select(k => k.ToString()));
        Assert.Equal(F("0.1"), set.Sum(ModifierField.DamageDealt));
        Assert.False(set.Targets(new ModifierKey(ModifierField.DamageTaken)));
        CombatantRules r = CombatantRules.For(CombatRules.Provisional, set);
        Assert.Equal(F("0.1"), r.DamageDealt);
        Assert.Equal(Fixed.Zero, r.DamageTaken);
    }

    [Fact]
    public void Ordre_des_sources_sans_effet_exemple()
    {
        // Liste obligatoire : « agrégation indépendante de l'ordre », exemple explicite. Six sources (rangs différents, permanents et
        // conditionnels) dans l'ordre déclaré, à l'envers et décalées de trois.
        BoundModifier[] sources =
        {
            Bound(Mod("guard.damageReduction", "0.04", "0.02"), 0, 3),   // 0,08
            Bound(Mod("damageDealt", "0.02", null, W), 1),
            Bound(Mod("guard.damageReduction", "-0.01"), 2),
            Bound(Mod("parry.windowMs", "50", "50"), 3, 2),              // 100
            Bound(Mod("damageDealt", "0.03", null, W), 4),
            Bound(Mod("status.burn.durationDealt", "500", "250"), 5, 5), // 1500
        };
        ModifierSet declared = ModifierSet.From(sources);
        ModifierSet reversed = ModifierSet.From(Enumerable.Reverse(sources));
        ModifierSet rotated = ModifierSet.From(sources.Skip(3).Concat(sources.Take(3)));
        foreach (ModifierSet set in new[] { declared, reversed, rotated })
        {
            Assert.Equal(new[] { "guard.damageReduction", "parry.windowMs", "status.burn.durationDealt" }, set.Keys.Select(k => k.ToString()));
            Assert.Equal(F("0.07"), set.Sum(ModifierField.GuardDamageReduction));
            Assert.Equal(F("100"), set.Sum(ModifierField.ParryWindowMs));
            Assert.Equal(F("1500"), set.Sum(ModifierField.StatusDurationDealt, "burn"));
            ConditionalModifier only = Assert.Single(set.Conditional);
            Assert.Equal((ModifierField.DamageDealt, ModifierConditions.WhileGuarding, F("0.05")), (only.Field, only.Conditions, only.Value));
            Assert.Equal(Snapshot(CombatantRules.For(CombatRules.Provisional, declared)), Snapshot(CombatantRules.For(CombatRules.Provisional, set)));
        }
    }

    private static readonly string[] PermanentKeys =
    {
        "guard.damageReduction", "guard.upkeepPerSecond", "guard.costPerBlock", "guard.reductionAtZeroEndurance",
        "guard.breakShortenPerCharge", "parry.windowMs", "parry.failureRecoveryMs", "rampartCharges.max", "resonance.perDirectHit",
        "endurance.max", "endurance.regenPerSecond", "maxHealth", "statusResistance", "damageDealt", "damageTaken",
        "status.burn.durationDealt", "status.bleed.durationTaken", "skill.essai.frappe.coefficient", "skill.essai.frappe.cooldownMs",
    };

    /// <summary>Les cinq ensembles de conditions admis (D10 : {C, R} refusé).</summary>
    private static readonly ModifierCondition[][] ConditionSets = { new[] { W }, new[] { C }, new[] { R }, new[] { W, C }, new[] { W, R } };

    /// <summary>
    /// Sources aléatoires reproductibles : clé, valeur base + perRank × (rang − 1) calculée ici en entiers bruts, conditions dans un ordre
    /// déclaré quelconque. Le drapeau guard.noUpkeepBreak a toujours la valeur 1.
    /// </summary>
    internal static List<(BoundModifier Bound, string Key, int Flags, long Raw)> Generate(int seed)
    {
        var rng = new Random(seed);
        var result = new List<(BoundModifier, string, int, long)>();
        for (int i = 0, n = rng.Next(41); i < n; i++)
        {
            bool conditional = rng.Next(3) == 0;
            bool flag = !conditional && rng.Next(10) == 0;
            string key = flag ? "guard.noUpkeepBreak"
                : conditional ? (rng.Next(2) == 0 ? "damageDealt" : "damageTaken")
                : PermanentKeys[rng.Next(PermanentKeys.Length)];
            ModifierCondition[] conditions = conditional ? ConditionSets[rng.Next(ConditionSets.Length)] : Array.Empty<ModifierCondition>();
            if (rng.Next(2) == 0) conditions = Enumerable.Reverse(conditions).ToArray();
            int rank = flag ? 1 : 1 + rng.Next(5);
            long baseRaw = flag ? Fixed.Scale : rng.Next(-20_000, 20_001), perRankRaw = flag ? 0 : rng.Next(-5_000, 5_001);
            var definition = new ModifierDefinition(key, new RankedValue(Fixed.FromRaw(baseRaw), Fixed.FromRaw(perRankRaw)), conditions);
            int flags = conditions.Aggregate(0, (s, c) => s | (1 << (int)c));
            result.Add((new BoundModifier($"talent.essai.g{i}.a", 0, rank, definition), key, flags, baseRaw + perRankRaw * (rank - 1)));
        }
        return result;
    }

    [Property(MaxTest = 300, Replay = "(8101,8111)")]
    public void Ordre_des_sources_sans_effet_propriete(int seed, int shuffle)
    {
        // Liste obligatoire : « agrégation indépendante de l'ordre ». Toute permutation des mêmes sources donne les mêmes clés, les mêmes
        // sommes et les mêmes conditionnels, égaux à un calcul indépendant en entiers bruts (somme par clé ; par clé et ensemble de
        // conditions, trié par texte de clé puis par ensemble), et les mêmes règles effectives.
        var generated = Generate(seed);
        BoundModifier[] permuted = generated.Select(g => g.Bound).ToArray();
        var rng = new Random(shuffle);
        for (int i = permuted.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (permuted[i], permuted[j]) = (permuted[j], permuted[i]);
        }
        var expectedSums = generated.Where(g => g.Flags == 0).GroupBy(g => g.Key)
            .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => (g.Key, g.Sum(x => x.Raw))).ToArray();
        var expectedConditional = generated.Where(g => g.Flags != 0).GroupBy(g => (g.Key, g.Flags))
            .OrderBy(g => g.Key.Key, StringComparer.Ordinal).ThenBy(g => g.Key.Flags)
            .Select(g => (g.Key.Key, g.Key.Flags, g.Sum(x => x.Raw))).ToArray();

        ModifierSet a = ModifierSet.From(generated.Select(g => g.Bound));
        ModifierSet b = ModifierSet.From(permuted);
        foreach (ModifierSet set in new[] { a, b })
        {
            Assert.Equal(expectedSums.Select(e => e.Key), set.Keys.Select(k => k.ToString()));
            Assert.Equal(expectedSums.Select(e => e.Item2), set.Keys.Select(k => set.Sum(k).Raw));
            Assert.Equal(expectedConditional, set.Conditional.Select(c => (Text(c.Field), (int)c.Conditions, c.Value.Raw)));
        }
        Assert.Equal(Snapshot(CombatantRules.For(CombatRules.Provisional, a)), Snapshot(CombatantRules.For(CombatRules.Provisional, b)));
        Assert.Equal(StatsText(CombatantStats.ForCharacter(StatRules.Provisional, Build, a)),
            StatsText(CombatantStats.ForCharacter(StatRules.Provisional, Build, b)));

        static string Text(ModifierField field) => field == ModifierField.DamageDealt ? "damageDealt" : "damageTaken";
    }

    public static TheoryData<string, CombatRules> BaseRules => new()
    {
        { "provisoires", CombatRules.Provisional },
        { "tick 40", new CombatRules { TickMilliseconds = 40, ParryWindowMs = 120, ParryFailureRecoveryMs = 1000 } },
        { "tick 1000", new CombatRules { TickMilliseconds = 1000, ParryWindowMs = 1000, ParryFailureRecoveryMs = 1000 } },
        {
            "planchers et bases hors des plafonds de jeu", new CombatRules
            {
                GuardDamageReduction = Fixed.Zero, GuardUpkeepPerSecond = 0, GuardCostPerBlock = 0, ParryWindowMs = 1000, ParryFailureRecoveryMs = 0,
                RampartChargesMax = 12, ResonancePerDirectHit = 0, EnduranceRegenPerSecond = 0,
            }
        },
        {
            "plafonds", new CombatRules
            {
                GuardDamageReduction = F("0.75"), GuardUpkeepPerSecond = int.MaxValue, GuardCostPerBlock = int.MaxValue, ParryWindowMs = 500,
                ParryFailureRecoveryMs = CombatRules.AbsoluteMaxDurationMs, RampartChargesMax = 10, ResonancePerDirectHit = int.MaxValue,
                EnduranceRegenPerSecond = int.MaxValue,
            }
        },
        { "non validées (garde 0,8, récupération 25 h)", new CombatRules { GuardDamageReduction = F("0.8"), ParryFailureRecoveryMs = 90_000_000 } },
    };

    [Theory]
    [MemberData(nameof(BaseRules))]
    public void Ensemble_vide_donne_exactement_les_regles_de_base(string name, CombatRules rules)
    {
        CombatantRules r = CombatantRules.For(rules, ModifierSet.Empty);
        Assert.Same(ModifierSet.Empty, r.Modifiers);
        Assert.Equal(rules.GuardDamageReduction, r.GuardDamageReduction);
        Assert.Equal(Fixed.FromInt(rules.GuardUpkeepPerSecond), r.GuardUpkeepPerSecond);
        Assert.Equal(rules.GuardCostPerBlock, r.GuardCostPerBlock);
        Assert.False(r.GuardNoUpkeepBreak);
        Assert.Equal(Fixed.Zero, r.GuardReductionAtZeroEndurance);
        Assert.Equal(0, r.GuardBreakShortenPerChargeMs);
        Assert.Equal(rules.ParryWindowMs, r.ParryWindowMs);
        Assert.Equal(rules.ParryFailureRecoveryMs, r.ParryFailureRecoveryMs);
        Assert.Equal(rules.RampartChargesMax, r.RampartChargesMax);
        Assert.Equal(rules.ResonancePerDirectHit, r.ResonancePerDirectHit);
        Assert.Equal(Fixed.FromInt(rules.EnduranceRegenPerSecond), r.EnduranceRegenPerSecond);
        Assert.Equal(Fixed.Zero, r.DamageDealt);
        Assert.Equal(Fixed.Zero, r.DamageTaken);
        // Méthodes : bases des compétences (même hors intervalles : coefficient 150, recharge 25 h), aucun ajout de durée, aucune valeur
        // conditionnelle, majorant nul.
        foreach (SkillDefinition skill in new[] { Skill(), Skill(F("150"), 90_000_000), Skill(Fixed.Zero, 0) })
        {
            Assert.Equal(skill.Combat!.Coefficient, r.Coefficient(skill, skill.Combat));
            Assert.Equal(skill.Combat.CooldownMs, r.CooldownMs(skill, skill.Combat));
        }
        Assert.Equal(0, r.DurationDealtMs("burn"));
        Assert.Equal(0, r.DurationTakenMs("stun"));
        foreach (bool guarding in new[] { false, true })
            foreach (long charges in new long[] { 0, 5, 12 })
                foreach (long resonance in new long[] { 0, 10, 100 })
                {
                    Assert.Equal(Fixed.Zero, r.DamageDealtAt(guarding, charges, resonance));
                    Assert.Equal(Fixed.Zero, r.DamageTakenAt(guarding, charges, resonance));
                }
        Assert.Equal(Fixed.Zero, r.WorstDamageDealt(rules.ResonanceMax));
        Assert.False(string.IsNullOrEmpty(name));
    }

    [Fact]
    public void Proprietes_de_CombatantRules_toutes_couvertes_par_le_test_d_ensemble_vide()
    {
        // Garde-fou : une nouvelle propriété publique doit être ajoutée au test précédent (et au journal, D8).
        string[] covered =
        {
            nameof(CombatantRules.Modifiers), nameof(CombatantRules.GuardDamageReduction), nameof(CombatantRules.GuardUpkeepPerSecond),
            nameof(CombatantRules.GuardCostPerBlock), nameof(CombatantRules.GuardNoUpkeepBreak), nameof(CombatantRules.GuardReductionAtZeroEndurance),
            nameof(CombatantRules.GuardBreakShortenPerChargeMs), nameof(CombatantRules.ParryWindowMs), nameof(CombatantRules.ParryFailureRecoveryMs),
            nameof(CombatantRules.RampartChargesMax), nameof(CombatantRules.ResonancePerDirectHit), nameof(CombatantRules.EnduranceRegenPerSecond),
            nameof(CombatantRules.DamageDealt), nameof(CombatantRules.DamageTaken),
        };
        Assert.Equal(covered.OrderBy(n => n, StringComparer.Ordinal),
            typeof(CombatantRules).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void Ensemble_vide_partage_et_cle_illisible_ignoree()
    {
        Assert.Same(ModifierSet.Empty, ModifierSet.From(Array.Empty<BoundModifier>()));
        Assert.Empty(ModifierSet.Empty.Keys);
        Assert.Empty(ModifierSet.Empty.Conditional);
        // Clé inconnue ou mal formée : ignorée à l'agrégation (la validation de la classe la refuse avant tout combat).
        Assert.Same(ModifierSet.Empty, Set(Mod("inconnue", "1"), Mod("status.Burn.durationDealt", "50"), Mod("skill.coefficient.x", "1")));
        ModifierSet mixed = Set(Mod("inconnue", "1"), Mod("guard.costPerBlock", "-2"));
        Assert.Equal(new[] { "guard.costPerBlock" }, mixed.Keys.Select(k => k.ToString()));
        Assert.Equal(F("-2"), mixed.Sum(ModifierField.GuardCostPerBlock));
    }

    [Fact]
    public void Sources_qui_s_annulent_laissent_la_base_meme_hors_intervalle()
    {
        // La clé reste ciblée (somme nulle) ; la valeur résultante est la base, même hors de l'intervalle de jeu (D2).
        var rules = new CombatRules { GuardDamageReduction = F("0.8"), ParryWindowMs = 1000 };
        CombatantRules r = For(rules, Mod("guard.damageReduction", "0.1"), Mod("guard.damageReduction", "-0.1"),
            Mod("parry.windowMs", "100"), Mod("parry.windowMs", "-100"));
        Assert.True(r.Modifiers.Targets(new ModifierKey(ModifierField.GuardDamageReduction)));
        Assert.Equal(Fixed.Zero, r.Modifiers.Sum(ModifierField.GuardDamageReduction));
        Assert.Equal(F("0.8"), r.GuardDamageReduction);
        Assert.Equal(1000, r.ParryWindowMs);
    }
}

/// <summary>
/// Sous-lot 8.1 (D2, D3) : modificateurs d'un personnage rattachés à leur source (<see cref="ModifierSet.For"/>) : talents de rang ≥ 1 à
/// leur rang, puis passifs équipés au rang 1 ; chaque clé au rang 1 et au rang maximal, par équivalence avec une règle de base décalée ;
/// toutes les clés à la fois sans interférence.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Build")]
public class Lot81CombatantRulesBindingTests
{
    private const string Talent = "talent.essai.alpha.a";

    private static (CharacterSetup Character, SkillDefinition Skill) Bearer(ModifierDefinition modifier, int rank)
    {
        SkillDefinition skill = Skill();
        var talent = new TalentDefinition(Talent, "A", 5, modifiers: new[] { modifier });
        var definition = new ClassDefinition("class.essai", "Essai",
            new[] { new TalentTreeDefinition("tree.essai.alpha", "alpha", new[] { talent }) }, new[] { skill });
        var build = new CharacterBuild("class.essai", 10, Attrs, new Dictionary<string, int> { [Talent] = rank }, new[] { Frappe });
        return (new CharacterSetup("bastion", "joueurs", definition, build), skill);
    }

    /// <summary>
    /// Règles, statistiques et compétence dont la base de la clé est décalée de <paramref name="x"/>, pour l'équivalence « modificateur x
    /// sur la base B » = « base B + x sans modificateur » ; null pour une clé sans règle de base (base nulle ou drapeau).
    /// </summary>
    private static (CombatRules Rules, StatRules Stats, SkillDefinition Skill)? Shifted(ModifierField field, Fixed x)
    {
        CombatRules p = CombatRules.Provisional;
        StatRules s = StatRules.Provisional;
        return field switch
        {
            ModifierField.GuardDamageReduction => (new CombatRules { GuardDamageReduction = p.GuardDamageReduction + x }, s, Skill()),
            ModifierField.GuardUpkeepPerSecond => (new CombatRules { GuardUpkeepPerSecond = p.GuardUpkeepPerSecond + N() }, s, Skill()),
            ModifierField.GuardCostPerBlock => (new CombatRules { GuardCostPerBlock = p.GuardCostPerBlock + N() }, s, Skill()),
            ModifierField.ParryWindowMs => (new CombatRules { ParryWindowMs = p.ParryWindowMs + N() }, s, Skill()),
            ModifierField.ParryFailureRecoveryMs => (new CombatRules { ParryFailureRecoveryMs = p.ParryFailureRecoveryMs + N() }, s, Skill()),
            ModifierField.RampartChargesMax => (new CombatRules { RampartChargesMax = p.RampartChargesMax + N() }, s, Skill()),
            ModifierField.ResonancePerDirectHit => (new CombatRules { ResonancePerDirectHit = p.ResonancePerDirectHit + N() }, s, Skill()),
            ModifierField.EnduranceRegenPerSecond => (new CombatRules { EnduranceRegenPerSecond = p.EnduranceRegenPerSecond + N() }, s, Skill()),
            ModifierField.EnduranceMax => (p, new StatRules { BaseMaxEndurance = s.BaseMaxEndurance + x }, Skill()),
            ModifierField.StatusResistance => (p, new StatRules { BaseStatusResistance = s.BaseStatusResistance + x }, Skill()),
            // PV max calculé 240 = 100 + 10 × 14 : une base de 100 + 240 × x donne 240 × (1 + x) sans modificateur.
            ModifierField.MaxHealth => (p, new StatRules { BaseMaxHealth = s.BaseMaxHealth + Fixed.FromInt(240) * x }, Skill()),
            ModifierField.SkillCoefficient => (p, s, Skill(F("1.2") + x)),
            ModifierField.SkillCooldownMs => (p, s, Skill(null, 6000 + N())),
            _ => null,
        };

        // Décalage d'une règle entière : l'ajout est lui-même entier (clés entières et durées, D2).
        int N()
        {
            Assert.Equal(0, x.Raw % Fixed.Scale);
            return checked((int)(x.Raw / Fixed.Scale));
        }
    }

    [Theory]
    // Liste obligatoire : « chaque clé au rang 1 et au rang maximal ». Talent de 5 rangs ; valeur = base + perRank × (rang − 1), sans
    // écrêtage (valeurs choisies dans les intervalles de jeu) ; règles provisoires (garde 0,6, entretien 2, coût 10, fenêtre 150 ms,
    // récupération 500 ms, 5 Charges, Résonance 3, régénération 10), personnage de test (PV 240, endurance 100, résistance 20),
    // compétence 1,2 / 6 000 ms.
    [InlineData("guard.damageReduction", "0.04", "0.02", 1, "0.64")]
    [InlineData("guard.damageReduction", "0.04", "0.02", 5, "0.72")]           // 0,6 + 0,04 + 4 × 0,02
    [InlineData("guard.upkeepPerSecond", "1", "1", 1, "3")]
    [InlineData("guard.upkeepPerSecond", "1", "1", 5, "7")]
    [InlineData("guard.costPerBlock", "-2", "-1", 1, "8")]
    [InlineData("guard.costPerBlock", "-2", "-1", 5, "4")]
    [InlineData("guard.noUpkeepBreak", "1", "0", 1, "true")]
    [InlineData("guard.noUpkeepBreak", "1", "0", 5, "true")]
    [InlineData("guard.reductionAtZeroEndurance", "0.1", "0.05", 1, "0")]    // inerte sans guard.noUpkeepBreak : 0 (valeur avec le
    [InlineData("guard.reductionAtZeroEndurance", "0.1", "0.05", 5, "0")]    // drapeau : Reduction_a_endurance_nulle_aux_rangs_1_et_5)
    [InlineData("guard.breakShortenPerCharge", "100", "100", 1, "100")]
    [InlineData("guard.breakShortenPerCharge", "100", "100", 5, "500")]
    [InlineData("parry.windowMs", "50", "50", 1, "200")]
    [InlineData("parry.windowMs", "50", "50", 5, "400")]
    [InlineData("parry.failureRecoveryMs", "-100", "-50", 1, "400")]
    [InlineData("parry.failureRecoveryMs", "-100", "-50", 5, "200")]
    [InlineData("rampartCharges.max", "1", "1", 1, "6")]
    [InlineData("rampartCharges.max", "1", "1", 5, "10")]                     // 5 + 5 : le plafond exactement
    [InlineData("resonance.perDirectHit", "1", "1", 1, "4")]
    [InlineData("resonance.perDirectHit", "1", "1", 5, "8")]
    [InlineData("endurance.max", "5", "5", 1, "105")]
    [InlineData("endurance.max", "5", "5", 5, "125")]
    [InlineData("endurance.regenPerSecond", "2", "1", 1, "12")]
    [InlineData("endurance.regenPerSecond", "2", "1", 5, "16")]
    [InlineData("maxHealth", "0.02", "0.02", 1, "244.8")]                      // 240 × 1,02
    [InlineData("maxHealth", "0.02", "0.02", 5, "264")]                        // 240 × 1,10
    [InlineData("statusResistance", "2", "1", 1, "22")]
    [InlineData("statusResistance", "2", "1", 5, "26")]
    [InlineData("damageDealt", "0.05", "0.05", 1, "0.05")]
    [InlineData("damageDealt", "0.05", "0.05", 5, "0.25")]
    [InlineData("damageTaken", "-0.02", "-0.01", 1, "-0.02")]
    [InlineData("damageTaken", "-0.02", "-0.01", 5, "-0.06")]
    [InlineData("status.burn.durationDealt", "1000", "500", 1, "1000")]
    [InlineData("status.burn.durationDealt", "1000", "500", 5, "3000")]
    [InlineData("status.stun.durationTaken", "-100", "-50", 1, "-100")]
    [InlineData("status.stun.durationTaken", "-100", "-50", 5, "-300")]
    [InlineData("skill.essai.frappe.coefficient", "0.1", "0.1", 1, "1.3")]
    [InlineData("skill.essai.frappe.coefficient", "0.1", "0.1", 5, "1.7")]
    [InlineData("skill.essai.frappe.cooldownMs", "-500", "-250", 1, "5500")]
    [InlineData("skill.essai.frappe.cooldownMs", "-500", "-250", 5, "4500")]
    public void Chaque_cle_au_rang_1_et_au_rang_maximal(string key, string add, string perRank, int rank, string expected)
    {
        ModifierDefinition modifier = Mod(key, add, perRank == "0" ? null : perRank);
        var (character, skill) = Bearer(modifier, rank);
        // Donnée légale (bornes déclarées aux rangs 1 et 5, entiers, drapeau, référence de compétence) : aucun écrêtage ne masque l'ajout.
        var errors = new List<RuleViolation>();
        ModifierDefinition.ValidateList(new[] { modifier }, "modifiers", 5, true, id => id == Frappe ? skill : null, errors);
        Assert.Empty(errors);

        ModifierSet set = ModifierSet.For(character);
        ModifierKey parsed = Key(key);
        Assert.Equal(new[] { key }, set.Keys.Select(k => k.ToString()));
        Fixed x = F(add) + F(perRank) * (rank - 1);
        Assert.Equal(x, set.Sum(parsed));
        string actual = Observe(parsed, CombatantRules.For(CombatRules.Provisional, set),
            CombatantStats.ForCharacter(StatRules.Provisional, character.Build, set), skill);
        Assert.Equal(expected, actual);

        if (Shifted(parsed.Field, x) is { } shifted)
            // Équivalence indépendante : même valeur avec la base décalée de x et aucun modificateur.
            Assert.Equal(expected, Observe(parsed, CombatantRules.For(shifted.Rules, ModifierSet.Empty),
                CombatantStats.ForCharacter(shifted.Stats, character.Build, ModifierSet.Empty), shifted.Skill));
        else
        {
            // Clé sans règle de base : valeur neutre sans modificateur, et la valeur résultante est l'ajout lui-même.
            string neutral = parsed.Field == ModifierField.GuardNoUpkeepBreak ? "false" : "0";
            Assert.Equal(neutral, Observe(parsed, CombatantRules.For(CombatRules.Provisional, ModifierSet.Empty),
                CombatantStats.ForCharacter(StatRules.Provisional, character.Build, ModifierSet.Empty), skill));
            // Sauf la réduction à endurance nulle, inerte (0) sans guard.noUpkeepBreak (revue 8.1).
            Assert.Equal(parsed.Field switch
            {
                ModifierField.GuardNoUpkeepBreak => "true",
                ModifierField.GuardReductionAtZeroEndurance => "0",
                _ => x.ToString(),
            }, expected);
        }
    }

    [Fact]
    public void Sources_du_personnage_talents_de_rang_positif_puis_passifs_equipes()
    {
        // Talents dans l'ordre de la classe (rang ≥ 1, valeur au rang du build), puis passifs équipés (rang 1) ; un talent de rang 0 ou
        // absent du build, un passif non équipé et une compétence active (même équipée) ne comptent pas (D2, D3).
        SkillDefinition frappe = new(Frappe, "Frappe", SkillKind.Active,
            combat: new SkillCombatDefinition(PowerType.Physique, DamageType.Physique, F("1.2"), 10, 6000),
            modifiers: new[] { Mod("guard.costPerBlock", "-100") });
        var definition = new ClassDefinition("class.essai", "Essai", new[]
        {
            new TalentTreeDefinition("tree.essai.alpha", "alpha", new[]
            {
                new TalentDefinition("talent.essai.alpha.a", "A", 5, modifiers: new[] { Mod("guard.costPerBlock", "-1", "-1"), Mod("damageDealt", "0.01", "0.01", W) }),
                new TalentDefinition("talent.essai.alpha.b", "B", 5, modifiers: new[] { Mod("guard.costPerBlock", "2", "1") }),
            }),
            new TalentTreeDefinition("tree.essai.beta", "beta", new[]
            {
                new TalentDefinition("talent.essai.beta.a", "A", 5, modifiers: new[] { Mod("guard.costPerBlock", "-1000") }),
                new TalentDefinition("talent.essai.beta.b", "B", 5, modifiers: new[] { Mod("guard.costPerBlock", "-1000") }),
            }),
        }, new[]
        {
            frappe,
            new SkillDefinition("skill.essai.passif", "Passif", SkillKind.Passive, modifiers: new[] { Mod("guard.costPerBlock", "-2"), Mod("damageDealt", "0.05", null, W) }),
            new SkillDefinition("skill.essai.passif_b", "Passif B", SkillKind.Passive, modifiers: new[] { Mod("guard.costPerBlock", "-1000") }),
        });
        var build = new CharacterBuild("class.essai", 10, Attrs,
            new Dictionary<string, int> { ["talent.essai.alpha.b"] = 5, ["talent.essai.alpha.a"] = 3, ["talent.essai.beta.a"] = 0 },
            new[] { Frappe }, null, new[] { "skill.essai.passif" });
        var character = new CharacterSetup("bastion", "joueurs", definition, build);

        Assert.Equal(new[]
        {
            ("talent.essai.alpha.a", 0, 3), ("talent.essai.alpha.a", 1, 3), ("talent.essai.alpha.b", 0, 5),
            ("skill.essai.passif", 0, 1), ("skill.essai.passif", 1, 1),
        }, character.ActiveModifiers().Select(m => (m.DefinitionId, m.Index, m.Rank)));

        ModifierSet set = ModifierSet.For(character);
        // Coût : (−1 − 1 × 2) + (2 + 1 × 4) + (−2) = −3 + 6 − 2 = 1 → 10 + 1 = 11 (les −100 et −1 000 des sources inactives absents).
        Assert.Equal(F("1"), set.Sum(ModifierField.GuardCostPerBlock));
        Assert.Equal(11, CombatantRules.For(CombatRules.Provisional, set).GuardCostPerBlock);
        // Conditionnel {W} : talent au rang 3 (0,01 + 2 × 0,01) + passif 0,05 = 0,08, regroupés.
        ConditionalModifier conditional = Assert.Single(set.Conditional);
        Assert.Equal((ModifierField.DamageDealt, ModifierConditions.WhileGuarding, F("0.08")), (conditional.Field, conditional.Conditions, conditional.Value));
        // Personnage sans aucun modificateur actif : l'ensemble vide partagé.
        var bare = new CharacterSetup("nu", "joueurs", definition, new CharacterBuild("class.essai", 10, Attrs, null, new[] { Frappe }));
        Assert.Same(ModifierSet.Empty, ModifierSet.For(bare));
    }

    [Fact]
    public void Combinaison_de_toutes_les_cles_sans_interference()
    {
        // Liste obligatoire : « combinaisons de modificateurs ». Toutes les clés à la fois, deux sources chacune : chaque valeur résultante
        // est celle obtenue avec les seuls modificateurs de sa clé, sauf la réduction à endurance nulle, ramenée à la réduction de garde
        // effective (D10), comparée avec les modificateurs de garde.
        var pairs = new (string Key, string A, string B, string Expected)[]
        {
            ("guard.damageReduction", "0.05", "0.03", "0.68"),
            ("guard.upkeepPerSecond", "1", "0.5", "3.5"),
            ("guard.costPerBlock", "-3", "1", "8"),
            ("guard.reductionAtZeroEndurance", "0.4", "0.4", "0.68"),         // 0,8 → 0,75 → min(0,75 ; 0,68)
            ("guard.breakShortenPerCharge", "100", "50", "150"),             // inerte (0) avec guard.noUpkeepBreak, voir plus bas
            ("parry.windowMs", "50", "100", "300"),
            ("parry.failureRecoveryMs", "-100", "-100", "300"),
            ("rampartCharges.max", "2", "1", "8"),
            ("resonance.perDirectHit", "2", "-1", "4"),
            ("endurance.max", "10", "15", "125"),
            ("endurance.regenPerSecond", "2.5", "0.5", "13"),
            ("maxHealth", "0.05", "0.05", "264"),
            ("statusResistance", "3", "1.5", "24.5"),
            ("damageDealt", "0.1", "0.15", "0.25"),
            ("damageTaken", "-0.1", "-0.05", "-0.15"),
            ("status.burn.durationDealt", "1000", "500", "1500"),
            ("status.stun.durationTaken", "-500", "250", "-250"),
            ("skill.essai.frappe.coefficient", "0.3", "0.2", "1.7"),
            ("skill.essai.frappe.cooldownMs", "-1000", "-500", "4500"),
        };
        ModifierSet all = Set(pairs.SelectMany(p => new[] { Mod(p.Key, p.A), Mod(p.Key, p.B) }).Append(Mod("guard.noUpkeepBreak", "1")).ToArray());
        Assert.Equal(pairs.Length + 1, all.Keys.Count);
        foreach (var (key, a, b, expected) in pairs)
        {
            // Le raccourci de rupture n'a rien à raccourcir quand la garde ne se brise plus (drapeau présent dans « all ») : 0.
            Assert.Equal(key == "guard.breakShortenPerCharge" ? "0" : expected, Value(key, CombatRules.Provisional, all));
            var alone = new List<ModifierDefinition> { Mod(key, a), Mod(key, b) };
            if (key == "guard.reductionAtZeroEndurance")
                alone.AddRange(new[] { Mod("guard.damageReduction", "0.05"), Mod("guard.damageReduction", "0.03"), Mod("guard.noUpkeepBreak", "1") });
            Assert.Equal(expected, Value(key, CombatRules.Provisional, Set(alone.ToArray())));
        }
        Assert.Equal("true", Value("guard.noUpkeepBreak", CombatRules.Provisional, all));
    }
}

/// <summary>
/// Sous-lot 8.1 (D2, D10) : écrêtage de chaque valeur résultante dans son intervalle de jeu, aux bornes N − 1, N et N + 1 : réduction de
/// garde [0 ; 0,75], réduction à endurance nulle ≤ garde effective, fenêtre de parade [1 tick ; ⌊500 / tick⌋ × tick], Charges
/// [0 ; max(base, 10)], durées [0 ; 24 h], coefficient [0 ; 100], entiers et décimaux [0 ; 2³¹ − 1] ; base hors intervalle = intervalle élargi.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Limits")]
public class Lot81CombatantRulesBoundsTests
{
    [Theory]
    // Base 0,6 (provisoire), une source : N − 1 = 0,7499 ; N = 0,75 ; N + 1 = 0,7501 écrêté.
    [InlineData("0.6", "0.1499", "0.7499")]
    [InlineData("0.6", "0.15", "0.75")]
    [InlineData("0.6", "0.1501", "0.75")]
    // Base 0 : une source vaut au plus 0,75 (borne déclarée, D10) ; dépasser demande deux sources.
    [InlineData("0", "0.7499", "0.7499")]
    [InlineData("0", "0.75", "0.75")]
    [InlineData("0", "0.75;0.0001", "0.75")]
    [InlineData("0", "0.75;0.75", "0.75")]
    // Plancher 0 : N + 1 = 0,0001, N = 0, N − 1 écrêté.
    [InlineData("0.6", "-0.5999", "0.0001")]
    [InlineData("0.6", "-0.6", "0")]
    [InlineData("0.6", "-0.6001", "0")]
    [InlineData("0.6", "-0.75;-0.75", "0")]
    // Plafond atteint exactement sur la base maximale légale.
    [InlineData("0.75", "0.0001", "0.75")]
    [InlineData("0.75", "-0.0001", "0.7499")]
    public void Reduction_de_garde_ecretee_entre_0_et_75_pour_cent(string @base, string adds, string expected)
    {
        var rules = new CombatRules { GuardDamageReduction = F(@base) };
        Assert.Empty(rules.Validate());
        Assert.Equal(expected, Value("guard.damageReduction", adds, rules));
    }

    [Theory]
    // Garde de base 0,6 : N − 1, N, N + 1 autour de la garde effective (D10 : jamais au-dessus).
    [InlineData("", "0.5999", "0.5999")]
    [InlineData("", "0.6", "0.6")]
    [InlineData("", "0.6001", "0.6")]
    // Garde portée à 0,75 : plafond absolu 0,75, deux sources pour le dépasser.
    [InlineData("0.15", "0.7499", "0.7499")]
    [InlineData("0.15", "0.75", "0.75")]
    [InlineData("0.15", "0.75;0.0001", "0.75")]
    // Garde abaissée à 0,4 : suit la garde effective.
    [InlineData("-0.2", "0.5", "0.4")]
    [InlineData("-0.2", "0.3999", "0.3999")]
    // Garde à 0 : 0. Plancher 0 sur la valeur propre.
    [InlineData("-0.6", "0.5", "0")]
    [InlineData("", "0.0001", "0.0001")]
    [InlineData("", "-0.1", "0")]
    [InlineData("", "0.1;-0.1", "0")]
    public void Reduction_a_endurance_nulle_jamais_au_dessus_de_la_garde_effective(string guardAdds, string adds, string expected)
    {
        ModifierSet set = Set(Split(guardAdds).Select(a => Mod("guard.damageReduction", a)).Concat(Split(adds).Select(a => Mod("guard.reductionAtZeroEndurance", a)))
            .Append(Mod("guard.noUpkeepBreak", "1")).ToArray());
        CombatantRules r = CombatantRules.For(CombatRules.Provisional, set);
        Assert.Equal(F(expected), r.GuardReductionAtZeroEndurance);
        Assert.True(r.GuardReductionAtZeroEndurance <= r.GuardDamageReduction);
    }

    [Theory]
    // Talent de 5 rangs avec le drapeau : 0,1 + 0,05 × (rang − 1), sous la garde provisoire 0,6.
    [InlineData(1, "0.1")]
    [InlineData(5, "0.3")]
    public void Reduction_a_endurance_nulle_aux_rangs_1_et_5(int rank, string expected)
    {
        ModifierSet set = ModifierSet.From(new[]
        {
            Bound(Mod("guard.reductionAtZeroEndurance", "0.1", "0.05"), 0, rank),
            Bound(Mod("guard.noUpkeepBreak", "1"), 1),
        });
        Assert.Equal(F(expected), CombatantRules.For(CombatRules.Provisional, set).GuardReductionAtZeroEndurance);
    }

    [Fact]
    public void Valeurs_inertes_ramenees_a_zero()
    {
        // Revue 8.1 : les deux combinaisons inertes du drapeau valent 0, pour que le journal (objet rules) ne les montre pas comme
        // appliquées. Sans le drapeau, la réduction à endurance nulle ne sert jamais (la garde se brise avant) ; avec le drapeau, aucune
        // rupture ne reste à raccourcir.
        CombatantRules withoutFlag = For(Mod("guard.reductionAtZeroEndurance", "0.5"), Mod("guard.breakShortenPerCharge", "500"));
        Assert.False(withoutFlag.GuardNoUpkeepBreak);
        Assert.Equal(Fixed.Zero, withoutFlag.GuardReductionAtZeroEndurance);
        Assert.Equal(500, withoutFlag.GuardBreakShortenPerChargeMs);
        CombatantRules withFlag = For(Mod("guard.reductionAtZeroEndurance", "0.5"), Mod("guard.breakShortenPerCharge", "500"), Mod("guard.noUpkeepBreak", "1"));
        Assert.True(withFlag.GuardNoUpkeepBreak);
        Assert.Equal(F("0.5"), withFlag.GuardReductionAtZeroEndurance);
        Assert.Equal(0, withFlag.GuardBreakShortenPerChargeMs);
        // Les clés restent visées (sommes intactes) : seule la valeur appliquée change.
        Assert.Equal(F("500"), withFlag.Modifiers.Sum(ModifierField.GuardBreakShortenPerCharge));
        Assert.Equal(F("0.5"), withoutFlag.Modifiers.Sum(ModifierField.GuardReductionAtZeroEndurance));
    }

    /// <summary>Règles légales pour un tick donné (récupération de parade 1 000 ms, multiple de 40, 50, 200 et 1 000).</summary>
    private static CombatRules Ticked(int tick, long window)
    {
        var rules = new CombatRules { TickMilliseconds = tick, ParryWindowMs = window, ParryFailureRecoveryMs = 1000 };
        Assert.Empty(rules.Validate());
        return rules;
    }

    [Theory]
    // Tick 50, base 150 (A2) : plafond ⌊500 / 50⌋ × 50 = 500.
    [InlineData(50, 150, "300", 450)]          // N − 1 tick
    [InlineData(50, 150, "350", 500)]          // N
    [InlineData(50, 150, "400", 500)]          // N + 1 tick, écrêté
    [InlineData(50, 150, "349", 499)]          // N − 1 ms (écrêtage seul : hors tick, refusé au combat)
    [InlineData(50, 150, "351", 500)]          // N + 1 ms
    [InlineData(50, 150, "350;350", 500)]      // deux sources légales (|add| ≤ 500, D10)
    // Tick 40, base 120 : plafond ⌊500 / 40⌋ × 40 = 480, et non 500.
    [InlineData(40, 120, "320", 440)]
    [InlineData(40, 120, "360", 480)]
    [InlineData(40, 120, "400", 480)]
    [InlineData(40, 120, "359", 479)]
    [InlineData(40, 120, "361", 480)]
    // Tick 200, base 200 : plafond ⌊500 / 200⌋ × 200 = 400 (N − 1 tick = 200 = base).
    [InlineData(200, 200, "200", 400)]
    [InlineData(200, 200, "400", 400)]
    [InlineData(200, 200, "199", 399)]
    [InlineData(200, 200, "201", 400)]
    // Tick 1000, base 1000 : ⌊500 / 1000⌋ × 1000 = 0 < base, plafond = base ; plancher 1 tick = base : la fenêtre ne bouge pas.
    [InlineData(1000, 1000, "500", 1000)]
    [InlineData(1000, 1000, "-500", 1000)]
    [InlineData(1000, 1000, "500;500", 1000)]
    public void Fenetre_de_parade_plafonnee_au_multiple_du_tick_sous_500_ms(int tick, long @base, string adds, long expected) =>
        Assert.Equal(expected.ToString(), Value("parry.windowMs", adds, Ticked(tick, @base)));

    [Theory]
    // Plancher 1 tick (D10 : min(1 tick, base)).
    [InlineData(50, 150, "-50", 100)]          // N + 1 tick
    [InlineData(50, 150, "-100", 50)]          // N
    [InlineData(50, 150, "-150", 50)]          // N − 1 tick (0 ms), écrêté
    [InlineData(50, 150, "-99", 51)]           // N + 1 ms
    [InlineData(50, 150, "-101", 50)]          // N − 1 ms
    [InlineData(50, 150, "-500;-500", 50)]
    [InlineData(40, 120, "-40", 80)]
    [InlineData(40, 120, "-80", 40)]
    [InlineData(40, 120, "-120", 40)]
    [InlineData(200, 200, "-200", 200)]        // base = 1 tick
    public void Fenetre_de_parade_jamais_sous_un_tick(int tick, long @base, string adds, long expected) =>
        Assert.Equal(expected.ToString(), Value("parry.windowMs", adds, Ticked(tick, @base)));

    [Theory]
    // Base 5 (provisoire), plafond max(base, 10) = 10 ; une source vaut au plus 10 (D10).
    [InlineData(5, "4", 9)]
    [InlineData(5, "5", 10)]
    [InlineData(5, "6", 10)]
    [InlineData(5, "10;10", 10)]
    [InlineData(5, "-4", 1)]
    [InlineData(5, "-5", 0)]
    [InlineData(5, "-6", 0)]
    [InlineData(5, "-10;-10", 0)]
    [InlineData(10, "1", 10)]
    [InlineData(10, "-1", 9)]
    [InlineData(0, "1", 1)]
    // Base 12 (légale) au-dessus du plafond de jeu : plafond = base.
    [InlineData(12, "1", 12)]
    [InlineData(12, "-1", 11)]
    [InlineData(12, "-10;-3", 0)]
    public void Charges_max_entre_0_et_max_base_10(int @base, string adds, int expected)
    {
        var rules = new CombatRules { RampartChargesMax = @base };
        Assert.Empty(rules.Validate());
        Assert.Equal(expected.ToString(), Value("rampartCharges.max", adds, rules));
    }

    [Theory]
    // Récupération de parade, base 500 : [0 ; 86 400 000].
    [InlineData("parry.failureRecoveryMs", "86399450", "86399950")]
    [InlineData("parry.failureRecoveryMs", "86399500", "86400000")]
    [InlineData("parry.failureRecoveryMs", "86399550", "86400000")]
    [InlineData("parry.failureRecoveryMs", "86400000;86400000", "86400000")]
    [InlineData("parry.failureRecoveryMs", "-450", "50")]
    [InlineData("parry.failureRecoveryMs", "-500", "0")]
    [InlineData("parry.failureRecoveryMs", "-550", "0")]
    // Raccourci de rupture par Charge, base 0 : une source vaut au plus 24 h ; dépasser demande deux sources.
    [InlineData("guard.breakShortenPerCharge", "86399950", "86399950")]
    [InlineData("guard.breakShortenPerCharge", "86400000", "86400000")]
    [InlineData("guard.breakShortenPerCharge", "86400000;50", "86400000")]
    [InlineData("guard.breakShortenPerCharge", "50", "50")]
    [InlineData("guard.breakShortenPerCharge", "50;-50", "0")]
    [InlineData("guard.breakShortenPerCharge", "-50", "0")]
    // Recharge de compétence, base 6 000.
    [InlineData("skill.essai.frappe.cooldownMs", "86393950", "86399950")]
    [InlineData("skill.essai.frappe.cooldownMs", "86394000", "86400000")]
    [InlineData("skill.essai.frappe.cooldownMs", "86394050", "86400000")]
    [InlineData("skill.essai.frappe.cooldownMs", "-5950", "50")]
    [InlineData("skill.essai.frappe.cooldownMs", "-6000", "0")]
    [InlineData("skill.essai.frappe.cooldownMs", "-6050", "0")]
    // Coefficient de compétence, base 1,2 : [0 ; 100] ; une source vaut au plus 100.
    [InlineData("skill.essai.frappe.coefficient", "98.7999", "99.9999")]
    [InlineData("skill.essai.frappe.coefficient", "98.8", "100")]
    [InlineData("skill.essai.frappe.coefficient", "98.8001", "100")]
    [InlineData("skill.essai.frappe.coefficient", "100;100", "100")]
    [InlineData("skill.essai.frappe.coefficient", "-1.1999", "0.0001")]
    [InlineData("skill.essai.frappe.coefficient", "-1.2", "0")]
    [InlineData("skill.essai.frappe.coefficient", "-1.2001", "0")]
    public void Durees_et_coefficient_ecretes(string key, string adds, string expected) =>
        Assert.Equal(expected, Value(key, adds));

    [Theory]
    // Plafond 2³¹ − 1 = 2 147 483 647, atteint par des sources d'au plus 10⁶ (environ 2 150 sources, sous les 4 096 possibles, D3).
    // Coût de blocage, base 10 (entier).
    [InlineData("guard.costPerBlock", "2147483636", "2147483646")]
    [InlineData("guard.costPerBlock", "2147483637", "2147483647")]
    [InlineData("guard.costPerBlock", "2147483638", "2147483647")]
    [InlineData("guard.costPerBlock", "-9", "1")]
    [InlineData("guard.costPerBlock", "-10", "0")]
    [InlineData("guard.costPerBlock", "-11", "0")]
    // Entretien de garde, base 2 (décimal à 4 décimales).
    [InlineData("guard.upkeepPerSecond", "2147483644.9999", "2147483646.9999")]
    [InlineData("guard.upkeepPerSecond", "2147483645", "2147483647")]
    [InlineData("guard.upkeepPerSecond", "2147483645.0001", "2147483647")]
    [InlineData("guard.upkeepPerSecond", "-1.9999", "0.0001")]
    [InlineData("guard.upkeepPerSecond", "-2", "0")]
    [InlineData("guard.upkeepPerSecond", "-2.0001", "0")]
    // Résonance par impact direct, base 3 (entier).
    [InlineData("resonance.perDirectHit", "2147483643", "2147483646")]
    [InlineData("resonance.perDirectHit", "2147483644", "2147483647")]
    [InlineData("resonance.perDirectHit", "2147483645", "2147483647")]
    [InlineData("resonance.perDirectHit", "-2", "1")]
    [InlineData("resonance.perDirectHit", "-3", "0")]
    [InlineData("resonance.perDirectHit", "-4", "0")]
    // Régénération d'endurance, base 10 (décimal).
    [InlineData("endurance.regenPerSecond", "2147483636.9999", "2147483646.9999")]
    [InlineData("endurance.regenPerSecond", "2147483637", "2147483647")]
    [InlineData("endurance.regenPerSecond", "2147483637.0001", "2147483647")]
    [InlineData("endurance.regenPerSecond", "-9.9999", "0.0001")]
    [InlineData("endurance.regenPerSecond", "-10", "0")]
    [InlineData("endurance.regenPerSecond", "-10.0001", "0")]
    public void Entiers_et_decimaux_ecretes_entre_0_et_2_puissance_31_moins_1(string key, string total, string expected)
    {
        ModifierSet set = Chunked(key, F(total));
        Assert.Equal(F(total), set.Sum(Key(key)));
        Assert.Equal(expected, Value(key, CombatRules.Provisional, set));
    }

    [Fact]
    public void Base_hors_de_l_intervalle_de_jeu_elargit_l_intervalle()
    {
        // D2 : quand la base sort de l'intervalle de jeu, l'intervalle est élargi jusqu'à elle ; sans modificateur, la base, toujours.
        // Fenêtre de parade de base 1 000 ms (légale) au-dessus du plafond 500 ms.
        var parry = new CombatRules { ParryWindowMs = 1000 };
        Assert.Empty(parry.Validate());
        Assert.Equal("1000", Value("parry.windowMs", "", parry));
        Assert.Equal("1000", Value("parry.windowMs", "100", parry));
        Assert.Equal("900", Value("parry.windowMs", "-100", parry));
        Assert.Equal("400", Value("parry.windowMs", "-300;-300", parry));
        Assert.Equal("50", Value("parry.windowMs", "-500;-500", parry));
        // Règles de test non validées (CombatRules.Validate les refuse) : réduction de garde 0,8 et récupération de parade 25 h.
        var outside = new CombatRules { GuardDamageReduction = F("0.8"), ParryFailureRecoveryMs = 90_000_000 };
        Assert.NotEmpty(outside.Validate());
        Assert.Equal("0.8", Value("guard.damageReduction", "", outside));
        Assert.Equal("0.8", Value("guard.damageReduction", "0.1", outside));
        Assert.Equal("0.7", Value("guard.damageReduction", "-0.1", outside));
        Assert.Equal("90000000", Value("parry.failureRecoveryMs", "50", outside));
        Assert.Equal("89999950", Value("parry.failureRecoveryMs", "-50", outside));
        // Réduction à endurance nulle sous une garde de base 0,8 : plafond propre 0,75, puis min avec la garde.
        Assert.Equal("0.75", Value("guard.reductionAtZeroEndurance", outside,
            Set(Mod("guard.reductionAtZeroEndurance", "0.75"), Mod("guard.reductionAtZeroEndurance", "0.05"), Mod("guard.noUpkeepBreak", "1"))));
        // Compétence hors intervalles (bloc combat non validé) : coefficient 150, recharge 25 h.
        SkillDefinition skill = Skill(F("150"), 90_000_000);
        Assert.Equal("150", Value("skill.essai.frappe.coefficient", "", skill: skill));
        Assert.Equal("150", Value("skill.essai.frappe.coefficient", "1", skill: skill));
        Assert.Equal("149", Value("skill.essai.frappe.coefficient", "-1", skill: skill));
        Assert.Equal("0", Value("skill.essai.frappe.coefficient", "-100;-51", skill: skill));
        Assert.Equal("90000000", Value("skill.essai.frappe.cooldownMs", "50", skill: skill));
        Assert.Equal("89999950", Value("skill.essai.frappe.cooldownMs", "-50", skill: skill));
    }
}

/// <summary>
/// Sous-lot 8.1 (D6, D7, D10) : évaluation des modificateurs conditionnels (X2) sur l'état du porteur — en garde, par Charge, par palier
/// de Résonance, ensembles combinés —, conditions vraies puis fausses sur le même combattant, dégâts subis écrêtés dans [−1 ; 0], majorant
/// du bonus de dégâts infligés pour le précontrôle.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Engine")]
public class Lot81CombatantRulesConditionalTests
{
    [Fact]
    public void En_garde_vrai_puis_faux_sur_le_meme_combattant()
    {
        // Liste obligatoire : « conditions vraies et fausses dans le même combat ». Même objet, états successifs puis retour au premier :
        // l'évaluation ne garde aucun état ; Charges et Résonance sans effet sur une condition tout ou rien.
        CombatantRules r = For(Mod("damageDealt", "0.1"), Mod("damageDealt", "0.2", null, W), Mod("damageTaken", "-0.15", null, W));
        Assert.Equal(F("0.3"), r.DamageDealtAt(true, 0, 0));
        Assert.Equal(F("0.1"), r.DamageDealtAt(false, 0, 0));
        Assert.Equal(F("0.3"), r.DamageDealtAt(true, 0, 0));
        Assert.Equal(F("-0.15"), r.DamageTakenAt(true, 0, 0));
        Assert.Equal(Fixed.Zero, r.DamageTakenAt(false, 0, 0));
        Assert.Equal(F("-0.15"), r.DamageTakenAt(true, 0, 0));
        Assert.Equal(F("0.3"), r.DamageDealtAt(true, 5, 100));
        Assert.Equal(F("0.1"), r.DamageDealtAt(false, 5, 100));
        // Les sommes permanentes ne bougent pas.
        Assert.Equal(F("0.1"), r.DamageDealt);
        Assert.Equal(Fixed.Zero, r.DamageTaken);
    }

    [Theory]
    // 0,03 par Charge détenue, en garde ou non ; 5 = maximum des règles provisoires.
    [InlineData(0, false, "0")]
    [InlineData(1, false, "0.03")]
    [InlineData(5, false, "0.15")]
    [InlineData(2, true, "0.06")]
    [InlineData(5, true, "0.15")]
    public void Par_charge_multiplie_par_les_charges_detenues(long charges, bool guarding, string expected)
    {
        CombatantRules r = For(Mod("damageDealt", "0.03", null, C), Mod("damageTaken", "-0.03", null, C));
        Assert.Equal(F(expected), r.DamageDealtAt(guarding, charges, 0));
        Assert.Equal(-F(expected), r.DamageTakenAt(guarding, charges, 0));
    }

    [Theory]
    // ⌊Résonance / 10⌋ paliers complets : 9 → 0 (N − 1), 10 → 1 (N), 11 → 1 (N + 1), 29 → 2, 30 → 3, 31 → 3, 100 → 10.
    [InlineData(0, "0")]
    [InlineData(9, "0")]
    [InlineData(10, "0.02")]
    [InlineData(11, "0.02")]
    [InlineData(29, "0.04")]
    [InlineData(30, "0.06")]
    [InlineData(31, "0.06")]
    [InlineData(100, "0.2")]
    public void Par_palier_de_resonance_multiplie_par_les_paliers_complets(long resonance, string expected)
    {
        CombatantRules r = For(Mod("damageDealt", "0.02", null, R), Mod("damageTaken", "-0.02", null, R));
        foreach (bool guarding in new[] { false, true })
        {
            Assert.Equal(F(expected), r.DamageDealtAt(guarding, 3, resonance));
            Assert.Equal(-F(expected), r.DamageTakenAt(guarding, 3, resonance));
        }
    }

    [Theory]
    // {whileGuarding, perRampartCharge} : en garde ET × Charges ; conditions déclarées dans les deux ordres.
    [InlineData(false, 0, "0")]
    [InlineData(false, 5, "0")]
    [InlineData(true, 0, "0")]
    [InlineData(true, 1, "-0.01")]
    [InlineData(true, 5, "-0.05")]
    public void En_garde_et_par_charge_combines(bool guarding, long charges, string expected)
    {
        foreach (ModifierCondition[] order in new[] { new[] { W, C }, new[] { C, W } })
        {
            CombatantRules r = For(Mod("damageTaken", "-0.01", null, order));
            Assert.Equal(F(expected), r.DamageTakenAt(guarding, charges, 50));
            Assert.Equal(Fixed.Zero, r.DamageDealtAt(guarding, charges, 50));
        }
    }

    [Fact]
    public void Tous_les_ensembles_de_conditions_a_la_fois()
    {
        // Permanent 0,1 ; {W} 0,2 ; {C} 0,03 ; {W,C} 0,01 ; {R} 0,02 ; {W,R} 0,005. État : 4 Charges, Résonance 25 (2 paliers).
        CombatantRules r = For(Mod("damageDealt", "0.1"), Mod("damageDealt", "0.2", null, W), Mod("damageDealt", "0.03", null, C),
            Mod("damageDealt", "0.01", null, W, C), Mod("damageDealt", "0.02", null, R), Mod("damageDealt", "0.005", null, W, R));
        // En garde : 0,1 + 0,2 + 0,03 × 4 + 0,01 × 4 + 0,02 × 2 + 0,005 × 2 = 0,51.
        Assert.Equal(F("0.51"), r.DamageDealtAt(true, 4, 25));
        // Hors garde : 0,1 + 0,12 + 0,04 = 0,26.
        Assert.Equal(F("0.26"), r.DamageDealtAt(false, 4, 25));
        // Les termes de dégâts infligés ne touchent pas les dégâts subis.
        Assert.Equal(Fixed.Zero, r.DamageTakenAt(true, 4, 25));
    }

    [Theory]
    // Permanent p, {W} −0,3, {C} −0,04 à 5 Charges : en garde p − 0,5 ; hors garde p − 0,2. N − 1 / N / N + 1 autour de −1.
    [InlineData("-0.4999", true, "-0.9999")]
    [InlineData("-0.5", true, "-1")]
    [InlineData("-0.5001", true, "-1")]
    [InlineData("-0.4999", false, "-0.6999")]
    [InlineData("-0.5", false, "-0.7")]
    [InlineData("-0.5001", false, "-0.7001")]
    public void Degats_subis_ecretes_a_moins_100_pour_cent(string permanent, bool guarding, string expected)
    {
        CombatantRules r = For(Mod("damageTaken", permanent), Mod("damageTaken", "-0.3", null, W), Mod("damageTaken", "-0.04", null, C));
        Assert.Equal(F(expected), r.DamageTakenAt(guarding, 5, 0));
        Assert.Equal(F(permanent), r.DamageTaken);   // somme permanente brute, écrêtée seulement à l'évaluation
    }

    [Fact]
    public void Degats_subis_jamais_hors_de_moins_1_et_0()
    {
        // Deux sources permanentes de −1 (chacune légale) : somme −2, évaluée −1.
        CombatantRules both = For(Mod("damageTaken", "-1"), Mod("damageTaken", "-1"));
        Assert.Equal(F("-2"), both.DamageTaken);
        Assert.Equal(-Fixed.One, both.DamageTakenAt(false, 0, 0));
        // Valeur positive (refusée par la validation de la classe, D10) : évaluée 0, une réduction seulement.
        CombatantRules positive = For(Mod("damageTaken", "0.2"), Mod("damageTaken", "0.1", null, W));
        Assert.Equal(Fixed.Zero, positive.DamageTakenAt(true, 0, 0));
        Assert.Equal(Fixed.Zero, positive.DamageTakenAt(false, 0, 0));
    }

    [Fact]
    public void Majorant_des_degats_infliges_conditionnels_positifs_au_facteur_maximal()
    {
        // D7 : permanent + Σ max(0, terme conditionnel maximal), Charges max effectives, ⌊Résonance max / 10⌋ ; le terme négatif {W,R}
        // est ignoré. Règles provisoires : 5 Charges, Résonance max 100 (10 paliers).
        ModifierDefinition[] terms =
        {
            Mod("damageDealt", "0.1"), Mod("damageDealt", "0.2", null, W), Mod("damageDealt", "0.03", null, C),
            Mod("damageDealt", "0.02", null, R), Mod("damageDealt", "0.01", null, W, C), Mod("damageDealt", "-0.04", null, W, R),
            Mod("damageTaken", "-0.5"), Mod("damageTaken", "0.3", null, W),   // dégâts subis : sans effet sur le majorant
        };
        CombatantRules r = For(terms);
        // 0,1 + 0,2 + 0,03 × 5 + 0,02 × 10 + 0,01 × 5 = 0,7.
        Assert.Equal(F("0.7"), r.WorstDamageDealt(100));
        Assert.Equal(r.DamageDealtAt(true, 5, 100) + F("0.4"), r.WorstDamageDealt(100));   // l'état réel perd 0,04 × 10 sur {W,R}
        // Charges max effectives portées à 10 : 0,1 + 0,2 + 0,3 + 0,2 + 0,1 = 0,9.
        Assert.Equal(F("0.9"), For(terms.Append(Mod("rampartCharges.max", "5")).ToArray()).WorstDamageDealt(100));
        // Résonance max 29 → 2 paliers : 0,1 + 0,2 + 0,15 + 0,04 + 0,05 = 0,54 ; négative → 0 palier : 0,5.
        Assert.Equal(F("0.54"), r.WorstDamageDealt(29));
        Assert.Equal(F("0.5"), r.WorstDamageDealt(-5));
        // Permanent négatif compté tel quel ; conditionnels seulement négatifs : le permanent.
        Assert.Equal(F("-0.05"), For(Mod("damageDealt", "-0.2"), Mod("damageDealt", "0.03", null, C)).WorstDamageDealt(100));
        Assert.Equal(F("0.1"), For(Mod("damageDealt", "0.1"), Mod("damageDealt", "-0.2", null, W), Mod("damageDealt", "-0.01", null, R)).WorstDamageDealt(100));
        Assert.Equal(Fixed.Zero, For(Mod("guard.damageReduction", "0.1")).WorstDamageDealt(100));
    }

    /// <summary>Termes de dégâts aléatoires reproductibles (permanents et conditionnels, positifs et négatifs) et Charges max modifiées.</summary>
    private static (ModifierDefinition[] Terms, int ResonanceMax) Terms(int seed)
    {
        var rng = new Random(seed);
        ModifierCondition[][] sets = { Array.Empty<ModifierCondition>(), new[] { W }, new[] { C }, new[] { R }, new[] { W, C }, new[] { W, R } };
        var terms = new List<ModifierDefinition>();
        for (int i = 0, n = rng.Next(13); i < n; i++)
            terms.Add(Mod(rng.Next(2) == 0 ? "damageDealt" : "damageTaken", Fixed.FromRaw(rng.Next(-3_000, 3_001)), sets[rng.Next(sets.Length)]));
        if (rng.Next(2) == 0) terms.Add(Mod("rampartCharges.max", Fixed.FromInt(rng.Next(-5, 6))));
        return (terms.ToArray(), rng.Next(0, 201));
    }

    [Property(MaxTest = 200, Replay = "(8102,8113)")]
    public void Majorant_jamais_depasse_et_degats_subis_conformes(int seed)
    {
        // Pour tout état atteignable (garde ou non, 0 à Charges max effectives, Résonance 0 à max) : bonus infligé ≤ majorant ; dégâts
        // subis égaux à un calcul indépendant sur les termes bruts, écrêté dans [−1 ; 0].
        var (terms, resonanceMax) = Terms(seed);
        CombatantRules r = For(terms);
        Fixed worst = r.WorstDamageDealt(resonanceMax);
        for (int charges = 0; charges <= r.RampartChargesMax; charges++)
            for (int resonance = 0; resonance <= resonanceMax; resonance++)
                foreach (bool guarding in new[] { false, true })
                {
                    Assert.True(r.DamageDealtAt(guarding, charges, resonance) <= worst);
                    long raw = 0;
                    foreach (ModifierDefinition t in terms.Where(t => t.Key == "damageTaken"))
                    {
                        if (t.Conditions.Contains(W) && !guarding) continue;
                        long factor = t.Conditions.Contains(C) ? charges : t.Conditions.Contains(R) ? resonance / 10 : 1;
                        raw += t.Add.Base.Raw * factor;
                    }
                    Assert.Equal(Math.Max(-Fixed.Scale, Math.Min(0, raw)), r.DamageTakenAt(guarding, charges, resonance).Raw);
                }
    }
}

/// <summary>
/// Sous-lot 8.1 (D6, D10) : réduction totale d'un impact direct, r = min(0,85 ; 1 − (1 − d)(1 + v)), une seule multiplication arrondie au
/// plus proche : v = 0 rend d exactement, bornes autour de 0,85 vérifiées à la main, d = 0 rend min(−v ; 0,85), jamais moins que d.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Numeric")]
public class Lot81CombatantRulesTotalReductionTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("0.0001")]
    [InlineData("0.3333")]
    [InlineData("0.6")]
    [InlineData("0.75")]
    public void Sans_modificateur_de_degats_subis_la_reduction_de_defense_exactement(string defense) =>
        Assert.Equal(F(defense), CombatantRules.TotalReduction(F(defense), Fixed.Zero));

    [Theory]
    // d = 0,7 : 1 − d = 0,3.
    //   v = −0,4997 : 0,3 × 0,5003 = 0,15009 → 0,1501 (4 décimales, au plus proche) → r = 0,8499   (N − 1)
    //   v = −0,4999 : 0,3 × 0,5001 = 0,15003 → 0,1500 → r = 0,85                                 (l'arrondi atteint N)
    //   v = −0,5    : 0,3 × 0,5    = 0,15            → r = 0,85                                   (N)
    //   v = −0,5003 : 0,3 × 0,4997 = 0,14991 → 0,1499 → 0,8501, plafonné à 0,85                    (N + 1)
    [InlineData("0.7", "-0.4997", "0.8499")]
    [InlineData("0.7", "-0.4999", "0.85")]
    [InlineData("0.7", "-0.5", "0.85")]
    [InlineData("0.7", "-0.5003", "0.85")]
    // d = 0,6 (garde provisoire) : 0,4 × 0,3753 = 0,15012 → 0,1501 → 0,8499 ; 0,4 × 0,375 = 0,15 → 0,85 ; 0,4 × 0,3747 = 0,14988 → 0,1499 → plafonné.
    [InlineData("0.6", "-0.6247", "0.8499")]
    [InlineData("0.6", "-0.625", "0.85")]
    [InlineData("0.6", "-0.6253", "0.85")]
    [InlineData("0.6", "-0.25", "0.7")]                 // 0,4 × 0,75 = 0,3
    // d = 0 : r = min(−v ; 0,85).
    [InlineData("0", "-0.3", "0.3")]
    [InlineData("0", "-0.8499", "0.8499")]
    [InlineData("0", "-0.85", "0.85")]
    [InlineData("0", "-0.8501", "0.85")]
    [InlineData("0", "-1", "0.85")]
    // d = 0,75 (plafond de garde) : 0,25 × 0,9999 = 0,249975 → 0,25 → r = d ; 0,25 × 0,9998 = 0,24995 → 0,25 (moitié, loin de zéro)
    // → r = d ; 0,25 × 0,9997 = 0,249925 → 0,2499 → 0,7501 ; v = −1 → 1, plafonné.
    [InlineData("0.75", "-0.0001", "0.75")]
    [InlineData("0.75", "-0.0002", "0.75")]
    [InlineData("0.75", "-0.0003", "0.7501")]
    [InlineData("0.75", "-1", "0.85")]
    public void Reduction_totale_plafonnee_a_85_pour_cent(string defense, string damageTaken, string expected) =>
        Assert.Equal(F(expected), CombatantRules.TotalReduction(F(defense), F(damageTaken)));

    [Property(MaxTest = 3000, Replay = "(8103,8115)")]
    public bool Reduction_totale_conforme_a_la_formule_et_jamais_sous_la_defense(ushort defense, ushort damageTaken)
    {
        // d ∈ [0 ; 0,75], v ∈ [−1 ; 0] (bruts au dix-millième). Calcul indépendant en entiers : produit (1 − d)(1 + v) au dix-millième,
        // arrondi au plus proche (moitié loin de zéro ; produit positif), puis min(0,85 ; 1 − produit).
        long d = defense % 7_501, v = -(damageTaken % 10_001);
        long product = ((Fixed.Scale - d) * (Fixed.Scale + v) + Fixed.Scale / 2) / Fixed.Scale;
        long expected = v == 0 ? d : Math.Min(8_500, Fixed.Scale - product);
        long actual = CombatantRules.TotalReduction(Fixed.FromRaw(d), Fixed.FromRaw(v)).Raw;
        return actual == expected && actual >= d && actual <= 8_500;
    }
}

/// <summary>
/// Sous-lot 8.1 (D3, D10) : statistiques d'un personnage avec ses modificateurs (<see cref="CombatantStats.ForCharacter(StatRules, CharacterBuild, ModifierSet)"/>) :
/// endurance max et résistance aux altérations en contributions plates bornées à 0, PV max en pourcentage écrêté dans [−90 % ; +1 000 %]
/// (arrondi au plus proche, puis plancher entier), au moins 1 PV si le PV max calculé vaut au moins 10 ; sans modificateur, identique.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Build")]
public class Lot81CombatantRulesStatsTests
{
    [Fact]
    public void Sans_modificateur_de_statistique_identique_au_calcul_sans_contribution()
    {
        foreach (Attributes attributes in new[] { Attrs, Attributes.None, new Attributes(1, 2, 3, 4, 5, 6), new Attributes(100, 100, 100, 100, 100, 100) })
        {
            var build = new CharacterBuild("class.essai", 10, attributes);
            string expected = StatsText(DerivedStats.Calculate(StatRules.Provisional, attributes));
            Assert.Equal(expected, StatsText(CombatantStats.ForCharacter(StatRules.Provisional, build)));
            Assert.Equal(expected, StatsText(CombatantStats.ForCharacter(StatRules.Provisional, build, ModifierSet.Empty)));
            // Modificateurs qui ne visent aucune statistique, ou statistiques à somme nulle : aucun effet.
            Assert.Equal(expected, StatsText(CombatantStats.ForCharacter(StatRules.Provisional, build,
                Set(Mod("guard.damageReduction", "0.1"), Mod("damageDealt", "0.2", null, W), Mod("maxHealth", "0.05"), Mod("maxHealth", "-0.05"),
                    Mod("endurance.max", "7"), Mod("endurance.max", "-7")))));
        }
    }

    [Theory]
    // Endurance max de base 100 (endurance 0) : contribution plate, plancher 0 (N + 1 = 1, N = 0, N − 1 écrêté).
    [InlineData("25", "125")]
    [InlineData("5;20", "125")]
    [InlineData("-99", "1")]
    [InlineData("-100", "0")]
    [InlineData("-101", "0")]
    [InlineData("-60;-60", "0")]
    public void Endurance_max_contribution_plate_bornee_a_0(string adds, string expected)
    {
        DerivedStats stats = CombatantStats.ForCharacter(StatRules.Provisional, Build, Set("endurance.max", adds));
        Assert.Equal(F(expected), stats.MaxEndurance);
        Assert.Equal(F("240"), stats.MaxHealth);
        Assert.Equal(F("20"), stats.StatusResistance);
    }

    [Theory]
    // Résistance aux altérations de base 20 (volonté 10) : contribution plate décimale, plancher 0.
    [InlineData("6.5", "26.5")]
    [InlineData("3;3.25", "26.25")]
    [InlineData("-19.9999", "0.0001")]
    [InlineData("-20", "0")]
    [InlineData("-20.0001", "0")]
    public void Resistance_aux_alterations_contribution_plate_bornee_a_0(string adds, string expected)
    {
        DerivedStats stats = CombatantStats.ForCharacter(StatRules.Provisional, Build, Set("statusResistance", adds));
        Assert.Equal(F(expected), stats.StatusResistance);
        Assert.Equal(F("100"), stats.MaxEndurance);
        Assert.Equal(F("240"), stats.MaxHealth);
    }

    [Theory]
    // PV max calculé 240. Une source vaut au plus 10 (+1 000 %) : dépasser le plafond demande deux sources.
    [InlineData("0.02", "244.8", 244)]
    [InlineData("5;4.9999", "2639.976", 2639)]     // N − 1 : 240 × 9,9999 = 2 399,976
    [InlineData("5;5", "2640", 2640)]              // N : 240 × 10
    [InlineData("10;0.0001", "2640", 2640)]        // N + 1 : écrêté à +1 000 %
    [InlineData("10;10", "2640", 2640)]
    [InlineData("-0.8999", "24.024", 24)]          // N + 1 au-dessus du plancher : 240 × −0,8999 = −215,976
    [InlineData("-0.9", "24", 24)]                 // N : −90 %
    [InlineData("-0.9001", "24", 24)]              // N − 1 : écrêté à −90 %
    [InlineData("-10", "24", 24)]
    public void PV_max_en_pourcentage_ecrete_entre_moins_90_et_plus_1000_pour_cent(string adds, string expected, long floor)
    {
        DerivedStats stats = CombatantStats.ForCharacter(StatRules.Provisional, Build, Set("maxHealth", adds));
        Assert.Equal(F(expected), stats.MaxHealth);
        Assert.Equal(floor, Floor(stats.MaxHealth));
        Assert.Equal(F("100"), stats.MaxEndurance);
    }

    [Fact]
    public void PV_max_arrondi_au_plus_proche_puis_plancher()
    {
        // PV max calculé 240,0001 (base 100,0001 + 140) : 240,0001 × 0,5 = 120,00005 → 120,0001 (moitié loin de zéro) → 360,0002 → 360 PV ;
        // 240,0001 × −0,5 = −120,00005 → −120,0001 → 120 exactement.
        var rules = new StatRules { BaseMaxHealth = F("100.0001") };
        Assert.Equal(F("360.0002"), CombatantStats.ForCharacter(rules, Build, Set(Mod("maxHealth", "0.5"))).MaxHealth);
        Assert.Equal(F("120"), CombatantStats.ForCharacter(rules, Build, Set(Mod("maxHealth", "-0.5"))).MaxHealth);
        // Toutes les statistiques à la fois : le pourcentage s'ajoute aux contributions plates sans les perdre.
        DerivedStats all = CombatantStats.ForCharacter(StatRules.Provisional, Build,
            Set(Mod("endurance.max", "25"), Mod("statusResistance", "6"), Mod("maxHealth", "0.1")));
        Assert.Equal((F("264"), F("125"), F("26")), (all.MaxHealth, all.MaxEndurance, all.StatusResistance));
    }

    [Theory]
    // Vitalité 0 : PV max calculé = base. D10 : au moins 1 PV si ≥ 10 ; en dessous, 0 possible.
    [InlineData("10", "-0.9", "1", 1)]
    [InlineData("10", "-0.95", "1", 1)]            // écrêté à −90 %
    [InlineData("10.0001", "-0.9", "1", 1)]        // 10,0001 × −0,9 = −9,00009 → −9,0001
    [InlineData("11", "-0.9", "1.1", 1)]
    [InlineData("9.9999", "-0.9", "1", 1)]         // −8,99991 → −8,9999 : encore 1 PV
    [InlineData("9.5", "-0.9", "0.95", 0)]         // sous 10 : 0 possible
    [InlineData("9", "-0.9", "0.9", 0)]
    public void Au_moins_1_PV_si_le_PV_max_calcule_vaut_au_moins_10(string maxHealth, string percent, string expected, long floor)
    {
        var build = new CharacterBuild("class.essai", 10, Attributes.None);
        DerivedStats stats = CombatantStats.ForCharacter(new StatRules { BaseMaxHealth = F(maxHealth) }, build, Set(Mod("maxHealth", percent)));
        Assert.Equal(F(expected), stats.MaxHealth);
        Assert.Equal(floor, Floor(stats.MaxHealth));
    }

    [Property(MaxTest = 2000, Replay = "(8104,8117)")]
    public bool Plancher_moins_90_pour_cent_garde_1_PV_des_10_PV(long maxHealth, int percent)
    {
        // PV max calculé M ∈ [10 ; 10⁸ + 10], pourcentage ≤ −90 % (deux sources pour passer sous −10, aucune au-delà de la borne déclarée).
        long m = 100_000 + (long)((ulong)maxHealth % 1_000_000_000_000UL);
        long p = 9_000 + (long)((uint)percent % 191_001u);
        var build = new CharacterBuild("class.essai", 10, Attributes.None);
        ModifierSet set = p <= 100_000 ? Set(Mod("maxHealth", Fixed.FromRaw(-p))) : Set(Mod("maxHealth", Fixed.FromRaw(-100_000)), Mod("maxHealth", Fixed.FromRaw(100_000 - p)));
        DerivedStats stats = CombatantStats.ForCharacter(new StatRules { BaseMaxHealth = Fixed.FromRaw(m) }, build, set);
        return Floor(stats.MaxHealth) >= 1;
    }
}

/// <summary>
/// Sous-lot 8.1 (D3) : immutabilité et pureté. <see cref="ModifierSet"/> et <see cref="CombatantRules"/> n'exposent que des collections en
/// lecture seule et aucun mutateur ; les copies sont faites à la construction ; un échec en cours d'agrégation n'a aucun effet partiel ;
/// l'enveloppe déclarée ne déborde jamais et, au-delà, l'arithmétique vérifiée lève une exception au lieu de s'enrouler ; deux
/// constructions identiques donnent les mêmes valeurs.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Regression")]
public class Lot81CombatantRulesImmutabilityTests
{
    [Fact]
    public void Collections_exposees_en_lecture_seule()
    {
        CombatantRules r = For(Mod("guard.damageReduction", "0.1"), Mod("parry.windowMs", "50"), Mod("damageDealt", "0.1", null, W),
            Mod("damageTaken", "-0.1", null, W, C));
        M05ImmutabilityTests.AssertFrozen(r.Modifiers.Keys);
        M05ImmutabilityTests.AssertFrozen(r.Modifiers.Conditional);
        M05ImmutabilityTests.AssertFrozen(ModifierSet.Empty.Keys);
        M05ImmutabilityTests.AssertFrozen(ModifierSet.Empty.Conditional);
        // Aucun mutateur public ni champ d'instance public sur les types du sous-lot.
        foreach (Type type in new[] { typeof(CombatantRules), typeof(ModifierSet), typeof(ConditionalModifier), typeof(ModifierDefinition), typeof(BoundModifier) })
        {
            Assert.All(type.GetProperties(BindingFlags.Public | BindingFlags.Instance), p => Assert.False(p.SetMethod is { IsPublic: true }, $"{type.Name}.{p.Name}"));
            Assert.Empty(type.GetFields(BindingFlags.Public | BindingFlags.Instance));
        }
        // La seule valeur publique statique de ModifierSet (Empty) est en lecture seule.
        Assert.True(typeof(ModifierSet).GetField(nameof(ModifierSet.Empty))!.IsInitOnly);
    }

    [Fact]
    public void Copies_a_la_construction()
    {
        // Sources : modifier la liste passée après l'agrégation ne change rien.
        var sources = new List<BoundModifier> { Bound(Mod("guard.damageReduction", "0.1")), Bound(Mod("damageDealt", "0.02", null, R), 1) };
        ModifierSet set = ModifierSet.From(sources);
        string before = Snapshot(CombatantRules.For(CombatRules.Provisional, set));
        sources.Add(Bound(Mod("guard.damageReduction", "0.05"), 2));
        sources[0] = Bound(Mod("parry.windowMs", "100"));
        Assert.Equal(before, Snapshot(CombatantRules.For(CombatRules.Provisional, set)));
        Assert.Equal(F("0.7"), CombatantRules.For(CombatRules.Provisional, set).GuardDamageReduction);
        // Conditions : modifier le tableau passé ne change ni la définition ni l'ensemble agrégé.
        var conditions = new[] { W, C };
        var modifier = new ModifierDefinition("damageTaken", new RankedValue(F("-0.01")), conditions);
        ModifierSet conditional = Set(modifier);
        conditions[0] = R;
        conditions[1] = R;
        Assert.Equal(new[] { W, C }, modifier.Conditions);
        Assert.Equal(ModifierConditions.WhileGuarding | ModifierConditions.PerRampartCharge, modifier.ConditionSet);
        M05ImmutabilityTests.AssertFrozen(modifier.Conditions);
        Assert.Equal(ModifierConditions.WhileGuarding | ModifierConditions.PerRampartCharge, Assert.Single(conditional.Conditional).Conditions);
        // Construire des règles effectives ne modifie pas l'ensemble.
        _ = CombatantRules.For(new CombatRules { RampartChargesMax = 1 }, conditional);
        Assert.Equal(F("-0.01"), Assert.Single(conditional.Conditional).Value);
    }

    [Fact]
    public void Echec_en_cours_d_agregation_sans_effet_partiel()
    {
        // Liste obligatoire : « pas de mutation partielle ». Une source qui échoue après deux autres : aucune agrégation partielle n'est
        // observable, ni dans l'ensemble vide partagé, ni dans un ensemble construit avant, ni dans un ensemble reconstruit après.
        ModifierSet built = Set(Mod("guard.damageReduction", "0.1"));
        string before = Snapshot(CombatantRules.For(CombatRules.Provisional, built));
        static IEnumerable<BoundModifier> Failing()
        {
            yield return Bound(Mod("guard.damageReduction", "0.05"));
            yield return Bound(Mod("damageDealt", "0.1", null, W), 1);
            throw new InvalidOperationException("source illisible");
        }
        Assert.Throws<InvalidOperationException>(() => ModifierSet.From(Failing()));
        Assert.Empty(ModifierSet.Empty.Keys);
        Assert.Empty(ModifierSet.Empty.Conditional);
        Assert.Equal(before, Snapshot(CombatantRules.For(CombatRules.Provisional, built)));
        Assert.Equal(before, Snapshot(CombatantRules.For(CombatRules.Provisional, Set(Mod("guard.damageReduction", "0.1")))));
        Assert.Equal(Snapshot(CombatantRules.For(CombatRules.Provisional, ModifierSet.Empty)),
            Snapshot(CombatantRules.For(CombatRules.Provisional, ModifierSet.From(Array.Empty<BoundModifier>()))));
        // Arguments nuls refusés avant toute construction.
        Assert.Throws<ArgumentNullException>(() => CombatantRules.For(null!, ModifierSet.Empty));
        Assert.Throws<ArgumentNullException>(() => CombatantRules.For(CombatRules.Provisional, null!));
        Assert.Throws<ArgumentNullException>(() => ModifierSet.For(null!));
        Assert.Throws<ArgumentNullException>(() => CombatantStats.ForCharacter(StatRules.Provisional, Build, null!));
    }

    [Fact]
    public void Enveloppe_declaree_sans_debordement_et_au_dela_exception_jamais_enroulement()
    {
        // D3 : au plus 4 096 modificateurs de |valeur| ≤ 8,64·10⁷ par combattant ; la somme exacte tient dans un long (N).
        ModifierSet widest = Chunked8640("status.burn.durationDealt", 4096);
        Assert.Equal(Fixed.FromInt(4096L * 86_400_000), widest.Sum(ModifierField.StatusDurationDealt, "burn"));
        Assert.Equal(4096L * 86_400_000, CombatantRules.For(CombatRules.Provisional, widest).DurationDealtMs("burn"));
        Assert.Equal(86_400_000, CombatantRules.For(CombatRules.Provisional, Chunked8640("skill.essai.frappe.cooldownMs", 4096))
            .CooldownMs(Skill(), Skill().Combat!));
        // D6 : évaluation conditionnelle au pire légal, 4 096 sources de +10 par palier, Résonance 2³¹ − 1 (214 748 364 paliers) :
        // 40 960 × 214 748 364 = 8 796 092 989 440, sans débordement.
        ModifierSet steps = Set(Enumerable.Range(0, 4096).Select(_ => Mod("damageDealt", "10", null, R)).ToArray());
        Assert.Equal(Fixed.FromInt(8_796_092_989_440), CombatantRules.For(CombatRules.Provisional, steps).DamageDealtAt(false, 0, int.MaxValue));
        // Au-delà de l'enveloppe (données refusées par la validation) : l'addition vérifiée lève OverflowException (N + 1), jamais un
        // enroulement silencieux.
        Assert.Throws<OverflowException>(() => Set(Mod("guard.costPerBlock", Fixed.MaxValue), Mod("guard.costPerBlock", Fixed.Epsilon)));
        Assert.Throws<OverflowException>(() => Set(Mod("damageTaken", Fixed.MinValue, W), Mod("damageTaken", -Fixed.Epsilon, W)));
        CombatantRules huge = For(Mod("damageDealt", Fixed.FromRaw(long.MaxValue / 2), R));
        Assert.Throws<OverflowException>(() => huge.DamageDealtAt(false, 0, 30));
        Assert.Throws<OverflowException>(() => huge.WorstDamageDealt(30));

        static ModifierSet Chunked8640(string key, int count) =>
            Set(Enumerable.Range(0, count).Select(_ => Mod(key, Fixed.FromInt(86_400_000))).ToArray());
    }

    [Fact]
    public void Construction_pure_et_deterministe()
    {
        // Deux constructions indépendantes à partir de définitions neuves : mêmes valeurs ; évaluer dans un autre ordre ne change rien.
        static CombatantRules Make() => For(new CombatRules { RampartChargesMax = 3 },
            Mod("guard.damageReduction", "0.1"), Mod("rampartCharges.max", "2"), Mod("damageDealt", "0.02", null, C),
            Mod("damageTaken", "-0.05", null, W), Mod("skill.essai.frappe.cooldownMs", "-500"), Mod("status.bleed.durationTaken", "1000"));
        CombatantRules first = Make(), second = Make();
        Assert.NotSame(first, second);
        Assert.Equal(Snapshot(first), Snapshot(second));
        var forward = new[] { (true, 5L, 30L), (false, 0L, 0L), (true, 2L, 99L) }.Select(s => (first.DamageDealtAt(s.Item1, s.Item2, s.Item3), first.DamageTakenAt(s.Item1, s.Item2, s.Item3))).ToArray();
        var backward = new[] { (true, 2L, 99L), (false, 0L, 0L), (true, 5L, 30L) }.Select(s => (second.DamageDealtAt(s.Item1, s.Item2, s.Item3), second.DamageTakenAt(s.Item1, s.Item2, s.Item3))).Reverse().ToArray();
        Assert.Equal(forward, backward);
        Assert.Equal((F("0.1"), F("-0.05")), forward[0]);   // 0,02 × 5 Charges (max effectif 3 + 2) ; en garde : −0,05
        Assert.Equal(Snapshot(first), Snapshot(second));
    }
}
