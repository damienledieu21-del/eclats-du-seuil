using EdS.Domain.Builds;
using static EdS.Domain.Tests.Builds.FictionalClass;

namespace EdS.Domain.Tests.Builds;

[Trait("Category", "Build")]
public class BuildValidatorTests
{
    private static readonly ProgressionRules Rules = ProgressionRules.Provisional;

    private static IReadOnlyList<BuildViolation> Validate(CharacterBuild build) => BuildValidator.Validate(build, Definition, Rules);

    private static void AssertOnly(CharacterBuild build, BuildViolationCode code, string? subject = null)
    {
        var violations = Validate(build);
        Assert.True(violations.Count > 0 && violations.All(v => v.Code == code) && (subject is null || violations.Any(v => v.Subject == subject)),
            $"{code} ({subject}) seul attendu ; obtenu : {string.Join(" | ", violations)}");
    }

    /// <summary>18 points dans alpha (a5 b5 c5 d3) puis le majeur : le minimum légal pour un majeur.</summary>
    private static Dictionary<string, int> AlphaMajor(int d = 3) => new()
    {
        [T("alpha", "a")] = 5, [T("alpha", "b")] = 5, [T("alpha", "c")] = 5, [T("alpha", "d")] = d, [T("alpha", "majeur")] = 1,
    };

    [Fact]
    public void Build_vide_valide_a_tout_niveau()
    {
        for (int level = 1; level <= Rules.MaxLevel; level++)
            Assert.Empty(Validate(Build(level)));
    }

    [Fact]
    public void Build_complet_niveau_30_valide()
    {
        var talents = AlphaMajor();
        talents[T("alpha", "e")] = 2;                         // dépend du majeur, ne compte pas dans le seuil
        talents[T("beta", "a")] = 5;
        talents[T("beta", "c")] = 3;                          // 18 + 1 + 2 + 8 = 29
        var build = Build(30, talents,
            new[] { "skill.essai.coup", "skill.essai.parade", "skill.essai.cri", "skill.essai.frappe_beta" },
            "skill.essai.cataclysme",
            new[] { "skill.essai.passif_1", "skill.essai.passif_2", "skill.essai.passif_3", "skill.essai.passif_5" });
        Assert.Empty(Validate(build));
    }

    [Fact]
    public void Points_non_depenses_autorises() => Assert.Empty(Validate(Build(30, new Dictionary<string, int> { [T("gamma", "c")] = 1 })));

    [Fact]
    public void Majeur_avec_17_points_refuse() => AssertOnly(Build(30, AlphaMajor(d: 2)), BuildViolationCode.MajorThresholdNotMet, T("alpha", "majeur"));

    [Fact]
    public void Talents_dependant_du_majeur_ne_comptent_pas_dans_le_seuil()
    {
        var talents = AlphaMajor(d: 1);
        talents[T("alpha", "e")] = 2;                          // 16 + 2 « après » : toujours 16 avant le majeur
        AssertOnly(Build(30, talents), BuildViolationCode.MajorThresholdNotMet, T("alpha", "majeur"));
    }

    [Fact]
    public void Deux_majeurs_refuses()
    {
        var talents = AlphaMajor();
        foreach (var (k, v) in new Dictionary<string, int> { ["a"] = 5, ["b"] = 5, ["c"] = 5, ["d"] = 3, ["majeur"] = 1 })
            talents[T("beta", k)] = v;
        var violations = Validate(Build(30, talents));
        Assert.Contains(violations, v => v.Code == BuildViolationCode.TooManyMajorTalents);
        Assert.Contains(violations, v => v.Code == BuildViolationCode.TooManyPoints);   // 38 points pour 29
    }

    [Fact]
    public void Deux_majeurs_refuses_meme_si_les_points_suffisaient()
    {
        // Règles modifiées pour isoler la règle « un seul majeur » de la limite de points.
        var rich = new ProgressionRules { TalentPointsPerLevel = 2 };
        var talents = AlphaMajor();
        foreach (var (k, v) in new Dictionary<string, int> { ["a"] = 5, ["b"] = 5, ["c"] = 5, ["d"] = 3, ["majeur"] = 1 })
            talents[T("beta", k)] = v;
        var violations = BuildValidator.Validate(Build(30, talents), Definition, rich);
        Assert.Equal(BuildViolationCode.TooManyMajorTalents, Assert.Single(violations).Code);
    }

    [Fact]
    public void Prerequis_de_rang_non_rempli() =>
        AssertOnly(Build(30, new Dictionary<string, int> { [T("alpha", "a")] = 2, [T("alpha", "b")] = 1 }), BuildViolationCode.PrerequisiteNotMet, T("alpha", "b"));

    [Theory]
    [InlineData(6)]
    [InlineData(0)]
    [InlineData(-3)]
    public void Rang_hors_limites(int rank) =>
        AssertOnly(Build(30, new Dictionary<string, int> { [T("alpha", "c")] = rank }), BuildViolationCode.RankOutOfRange, T("alpha", "c"));

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void Niveau_hors_limites(int level) => AssertOnly(Build(level), BuildViolationCode.LevelOutOfRange, "level");

    [Fact]
    public void Trop_de_points_pour_le_niveau() =>
        AssertOnly(Build(10, new Dictionary<string, int> { [T("alpha", "c")] = 5, [T("beta", "c")] = 5 }), BuildViolationCode.TooManyPoints, "talents");

    [Fact]
    public void Talent_inconnu() =>
        AssertOnly(Build(30, new Dictionary<string, int> { ["talent.autre.x.y"] = 1 }), BuildViolationCode.UnknownTalent, "talent.autre.x.y");

    [Fact]
    public void Ultime_au_niveau_9_refusee()
    {
        var violations = Validate(Build(9, ultimate: "skill.essai.cataclysme"));
        Assert.Contains(violations, v => v.Code == BuildViolationCode.UltimateLocked && v.Subject == "ultimate");
    }

    [Fact]
    public void Ultime_au_niveau_10_acceptee() => Assert.Empty(Validate(Build(10, ultimate: "skill.essai.cataclysme")));

    [Fact]
    public void Trois_actives_au_niveau_4_refusees() =>
        AssertOnly(Build(4, actives: new[] { "skill.essai.coup", "skill.essai.parade", "skill.essai.charge" }), BuildViolationCode.TooManySkills, "activeSkills");

    [Fact]
    public void Trois_actives_au_niveau_5_acceptees() =>
        Assert.Empty(Validate(Build(5, actives: new[] { "skill.essai.coup", "skill.essai.parade", "skill.essai.charge" })));

    [Fact]
    public void Competence_en_double() =>
        AssertOnly(Build(10, actives: new[] { "skill.essai.coup", "skill.essai.coup" }), BuildViolationCode.DuplicateSkill, "activeSkills[1]");

    [Fact]
    public void Ultime_dans_un_emplacement_actif_refusee() =>
        AssertOnly(Build(10, actives: new[] { "skill.essai.cataclysme" }), BuildViolationCode.WrongSlotKind, "activeSkills[0]");

    [Fact]
    public void Passif_dans_l_emplacement_d_ultime_refuse() =>
        AssertOnly(Build(10, ultimate: "skill.essai.passif_1"), BuildViolationCode.WrongSlotKind, "ultimate");

    [Fact]
    public void Competence_inconnue() =>
        AssertOnly(Build(10, passives: new[] { "skill.essai.inexistant" }), BuildViolationCode.UnknownSkill, "passiveSkills[0]");

    [Fact]
    public void Trop_de_passifs() =>
        AssertOnly(Build(19, passives: new[] { "skill.essai.passif_1", "skill.essai.passif_2", "skill.essai.passif_3" }), BuildViolationCode.TooManySkills, "passiveSkills");

    [Fact]
    public void Niveau_minimal_de_competence() =>
        AssertOnly(Build(4, actives: new[] { "skill.essai.cri" }), BuildViolationCode.SkillLevelTooLow, "activeSkills[0]");

    [Fact]
    public void Prerequis_de_talent_d_une_competence()
    {
        AssertOnly(Build(10, actives: new[] { "skill.essai.frappe_beta" }), BuildViolationCode.SkillRequirementNotMet, "activeSkills[0]");
        Assert.Empty(Validate(Build(10, new Dictionary<string, int> { [T("beta", "c")] = 2 }, new[] { "skill.essai.frappe_beta" })));
    }

    [Fact]
    public void Mauvaise_classe() =>
        Assert.Contains(BuildValidator.Validate(new CharacterBuild("class.autre", 1, Attributes), Definition, Rules), v => v.Code == BuildViolationCode.WrongClass);

    [Fact]
    public void Toutes_les_erreurs_sont_rapportees()
    {
        var build = Build(4, new Dictionary<string, int> { [T("alpha", "c")] = 9, ["talent.x.y"] = 1 },
            new[] { "skill.essai.coup", "skill.essai.coup", "skill.essai.cataclysme" }, "skill.essai.cataclysme");
        var codes = Validate(build).Select(v => v.Code).ToHashSet();
        Assert.Superset(new HashSet<BuildViolationCode>
        {
            BuildViolationCode.RankOutOfRange, BuildViolationCode.UnknownTalent, BuildViolationCode.TooManyPoints,
            BuildViolationCode.TooManySkills, BuildViolationCode.DuplicateSkill, BuildViolationCode.UltimateLocked,
        }, codes);
    }

    [Fact]
    public void Points_par_arbre()
    {
        var build = Build(30, AlphaMajor());
        Assert.Equal(19, BuildValidator.PointsInTree(build, Definition.Trees[0]));
        Assert.Equal(0, BuildValidator.PointsInTree(build, Definition.Trees[1]));
    }
}
