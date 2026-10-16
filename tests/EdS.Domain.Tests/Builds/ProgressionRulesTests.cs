using FsCheck.Xunit;
using EdS.Domain.Builds;

namespace EdS.Domain.Tests.Builds;

public class ProgressionRulesTests
{
    private static readonly ProgressionRules Rules = ProgressionRules.Provisional;

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(10, 9)]
    [InlineData(19, 18)]
    [InlineData(30, 29)]
    public void Points_de_talent_egaux_au_niveau_moins_un(int level, int points) =>
        Assert.Equal(points, Rules.TalentPoints(level));

    [Theory]
    [InlineData(1, 2, 1, false)]
    [InlineData(4, 2, 1, false)]
    [InlineData(5, 3, 2, false)]
    [InlineData(9, 3, 2, false)]
    [InlineData(10, 4, 2, true)]
    [InlineData(19, 4, 2, true)]
    [InlineData(20, 4, 3, true)]
    [InlineData(29, 4, 3, true)]
    [InlineData(30, 4, 4, true)]
    public void Emplacements_par_niveau(int level, int actives, int passives, bool ultimate)
    {
        Assert.Equal(actives, Rules.ActiveSlots(level));
        Assert.Equal(passives, Rules.PassiveSlots(level));
        Assert.Equal(ultimate, Rules.IsUltimateUnlocked(level));
    }

    [Fact]
    public void Un_seul_majeur_possible_au_niveau_maximum()
    {
        // 29 points : 18 + 1 pour un majeur, il en reste 10, moins que les 19 d'un second.
        int afterFirst = Rules.TalentPoints(Rules.MaxLevel) - (Rules.MajorTalentThreshold + 1);
        Assert.True(afterFirst < Rules.MajorTalentThreshold + 1);
        Assert.Equal(1, Rules.MaxMajorTalents);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(-1)]
    public void Niveau_hors_limites_refuse(int level)
    {
        Assert.False(Rules.IsValidLevel(level));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rules.TalentPoints(level));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rules.ActiveSlots(level));
    }

    [Fact]
    public void Regles_provisoires_coherentes() => Assert.Empty(Rules.Validate());

    [Fact]
    public void Regles_incoherentes_signalees()
    {
        var rules = new ProgressionRules
        {
            MaxLevel = 30,
            ActiveSlotLevels = new[] { 1, 10, 5 },
            PassiveSlotLevels = new[] { 1, 40 },
            UltimateUnlockLevel = 0,
            TreesPerClass = 0,
        };
        var fields = rules.Validate().SelectMany(v => v.Fields).ToList();
        Assert.Contains(nameof(ProgressionRules.ActiveSlotLevels), fields);
        Assert.Contains(nameof(ProgressionRules.PassiveSlotLevels), fields);
        Assert.Contains(nameof(ProgressionRules.UltimateUnlockLevel), fields);
        Assert.Contains(nameof(ProgressionRules.TreesPerClass), fields);
    }

    [Fact]
    public void Total_de_points_qui_deborde_signale()
    {
        var rules = new ProgressionRules { MaxLevel = 1_000_000, TalentPointsPerLevel = 1_000_000 };
        Assert.Contains(rules.Validate(), v => v.Fields.Contains(nameof(ProgressionRules.TalentPointsPerLevel)));
    }

    [Property(MaxTest = 500)]
    public bool Points_et_emplacements_croissants_avec_le_niveau(byte raw)
    {
        int level = 1 + raw % 29;
        return Rules.TalentPoints(level + 1) >= Rules.TalentPoints(level)
            && Rules.ActiveSlots(level + 1) >= Rules.ActiveSlots(level)
            && Rules.PassiveSlots(level + 1) >= Rules.PassiveSlots(level);
    }
}
