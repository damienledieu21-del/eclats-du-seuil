using EdS.Domain.Builds;
using EdS.Domain.Stats;
using static EdS.Domain.Tests.Builds.FictionalClass;

namespace EdS.Domain.Tests.Regression;

/// <summary>
/// Correctif 6.1, M03 : le validateur de builds et la validation des classes ne lèvent jamais d'exception sur une entrée
/// extrême (<c>int.MinValue</c>, <c>int.MaxValue</c>, négatifs, sommes énormes) ; ils renvoient des diagnostics.
/// Avant le correctif, les sommes de rangs sur 32 bits levaient <see cref="OverflowException"/>.
/// </summary>
[Trait("Category", "Build")]
[Trait("Category", "Regression")]
public class M03ValidatorOverflowTests
{
    private static readonly ProgressionRules Rules = ProgressionRules.Provisional;

    private static IReadOnlyList<BuildViolation> Validate(CharacterBuild build, ClassDefinition? definition = null, ProgressionRules? rules = null) =>
        BuildValidator.Validate(build, definition ?? Definition, rules ?? Rules);

    public static TheoryData<int> Extremes => new() { int.MinValue, int.MinValue + 1, -1, 0, int.MaxValue - 1, int.MaxValue };

    [Theory]
    [MemberData(nameof(Extremes))]
    public void Rangs_extremes_dans_toute_la_branche_du_majeur(int rank)
    {
        // Tous les talents de la branche au même rang extrême, y compris le majeur : seuil, points et rangs calculés sur 64 bits.
        var talents = new[] { "a", "b", "c", "d", "majeur", "e" }.ToDictionary(t => T("alpha", t), _ => rank);
        var violations = Validate(Build(30, talents));
        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Code == BuildViolationCode.RankOutOfRange);
    }

    [Fact]
    public void Somme_des_rangs_au_dela_de_int_MaxValue()
    {
        // Avant : Sum() sur int levait OverflowException dans le seuil du majeur.
        var talents = new Dictionary<string, int>
        {
            [T("alpha", "a")] = int.MaxValue, [T("alpha", "b")] = int.MaxValue, [T("alpha", "c")] = int.MaxValue,
            [T("alpha", "d")] = int.MaxValue, [T("alpha", "majeur")] = 1,
        };
        var violations = Validate(Build(30, talents));
        var tooMany = Assert.Single(violations, v => v.Code == BuildViolationCode.TooManyPoints);
        Assert.Contains("8589934589", tooMany.Message);   // 4 × int.MaxValue + 1, exact
        Assert.DoesNotContain(violations, v => v.Code == BuildViolationCode.MajorThresholdNotMet);
        Assert.Equal(4L * int.MaxValue, BuildValidator.PointsInTree(Build(30, talents), Definition.Trees[0]) - 1);
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(int.MaxValue)]
    public void Niveau_extreme(int level) =>
        Assert.Equal(BuildViolationCode.LevelOutOfRange, Assert.Single(Validate(Build(level))).Code);

    [Fact]
    public void Attributs_extremes_refuses_par_un_diagnostic()
    {
        var build = new CharacterBuild(Id, 10, new Attributes(int.MaxValue, 0, BuildValidator.MaxAttributeValue, 0, BuildValidator.MaxAttributeValue + 1, 0));
        var violations = Validate(build);
        Assert.Equal(new[] { "attributes.force", "attributes.volonte" }, violations.Select(v => v.Subject));
        Assert.All(violations, v => Assert.Equal(BuildViolationCode.AttributeOutOfRange, v.Code));
    }

    private static readonly ProgressionRules[] InvalidProgressions =
    {
        new ProgressionRules { TalentPointsPerLevel = int.MaxValue },
        new ProgressionRules { MaxLevel = int.MaxValue },
        new ProgressionRules { MaxLevel = int.MinValue },
        new ProgressionRules { FirstTalentPointLevel = int.MinValue },
        new ProgressionRules { MajorTalentThreshold = int.MaxValue },
        new ProgressionRules { ActiveSlotLevels = null! },
        new ProgressionRules { PassiveSlotLevels = Enumerable.Repeat(1, ProgressionRules.MaxSlots + 1).ToArray() },
    };

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Regles_de_progression_extremes_diagnostic_sans_exception(int index)
    {
        ProgressionRules rules = InvalidProgressions[index];
        Assert.NotEmpty(rules.Validate());
        var violation = Assert.Single(Validate(Build(10), rules: rules));
        Assert.Equal(BuildViolationCode.InvalidProgressionRules, violation.Code);
    }

    [Fact]
    public void Bornes_de_progression_acceptees_a_la_limite()
    {
        var rules = new ProgressionRules
        {
            MaxLevel = ProgressionRules.MaxLevelLimit, TalentPointsPerLevel = ProgressionRules.MaxTalentPointsPerLevel,
            MajorTalentThreshold = ProgressionRules.MaxThreshold,
        };
        Assert.Empty(rules.Validate());
        Assert.Equal(999_000, rules.TalentPoints(ProgressionRules.MaxLevelLimit));
        Assert.Empty(Validate(Build(ProgressionRules.MaxLevelLimit), rules: rules));
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(0)]
    [InlineData(TalentDefinition.MaxRankLimit + 1)]
    [InlineData(int.MaxValue)]
    public void Rang_maximum_extreme_dans_une_classe(int maxRank)
    {
        // Avant : la faisabilité du majeur additionnait les rangs maximums sur 32 bits (OverflowException avec int.MaxValue).
        var definition = Create(key => key == "alpha"
            ? new TalentTreeDefinition("tree.essai.alpha", "Alpha", new[]
            {
                new TalentDefinition(T(key, "a"), "A", maxRank), new TalentDefinition(T(key, "c"), "C", maxRank),
                new TalentDefinition(T(key, "majeur"), "Majeur", 1, isMajor: true),
            })
            : Tree(key));
        var violations = definition.Validate(Rules);
        Assert.Contains(violations, v => v.Fields.Contains("trees[0].talents[0].maxRank"));
        Assert.Contains(violations, v => v.Fields.Contains("trees[0].talents[1].maxRank"));
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Prerequis_et_exigences_de_rang_extremes(int rank)
    {
        var definition = Create(key => new TalentTreeDefinition($"tree.essai.{key}", key, new[]
        {
            new TalentDefinition(T(key, "a"), "A", 5),
            new TalentDefinition(T(key, "b"), "B", 5, prerequisites: new[] { new TalentRequirement(T(key, "a"), rank) }),
        }), Skills.Append(new SkillDefinition("skill.essai.exigeante", "Exigeante", SkillKind.Active,
            talentRequirements: new[] { new TalentRequirement(T("alpha", "a"), rank) })).ToArray());
        var violations = definition.Validate(Rules);
        Assert.Contains(violations, v => v.Fields.Contains("trees[0].talents[1].prerequisites[0].rank"));
        Assert.Contains(violations, v => v.Fields.Any(f => f.EndsWith(".talentRequirements[0].rank")));

        // Le build est jugé sans exception, même avec une définition incohérente.
        var build = Build(30, new Dictionary<string, int> { [T("alpha", "a")] = int.MaxValue, [T("alpha", "b")] = int.MinValue }, actives: new[] { "skill.essai.exigeante" });
        Assert.NotEmpty(Validate(build, definition));
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Niveau_minimal_de_competence_extreme(int minLevel)
    {
        var definition = Create(skills: Skills.Append(new SkillDefinition("skill.essai.lointaine", "Lointaine", SkillKind.Active, minLevel)).ToArray());
        Assert.Contains(definition.Validate(Rules), v => v.Fields.Any(f => f.EndsWith(".minLevel")));
        var violations = Validate(Build(30, actives: new[] { "skill.essai.lointaine" }), definition);
        if (minLevel == int.MaxValue) Assert.Contains(violations, v => v.Code == BuildViolationCode.SkillLevelTooLow);
    }

    [Fact]
    public void Propriete_aucune_entree_aleatoire_ne_leve_d_exception()
    {
        var random = new Random(61);
        int[] pool = { int.MinValue, int.MinValue + 1, -2, -1, 0, 1, 2, 5, 18, 30, int.MaxValue - 1, int.MaxValue };
        int Pick() => random.Next(4) == 0 ? random.Next(int.MinValue, int.MaxValue) : pool[random.Next(pool.Length)];
        string[] ids = TreeKeys.SelectMany(k => new[] { "a", "b", "c", "d", "majeur", "e" }.Select(t => T(k, t))).Append("talent.inconnu.x").ToArray();
        for (int i = 0; i < 2000; i++)
        {
            var talents = ids.Where(_ => random.Next(3) == 0).ToDictionary(id => id, _ => Pick());
            var rules = random.Next(4) == 0
                ? new ProgressionRules { MaxLevel = Pick(), TalentPointsPerLevel = Pick(), MajorTalentThreshold = Pick(), FirstTalentPointLevel = Pick() }
                : Rules;
            var build = new CharacterBuild(Id, Pick(), new Attributes(Math.Abs(Math.Max(Pick(), -int.MaxValue)), 0, 0, 0, 0, 0), talents);
            _ = Validate(build, rules: rules);   // ne doit jamais lever
        }
    }
}
