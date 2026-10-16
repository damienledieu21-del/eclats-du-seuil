using EdS.Domain.Builds;
using FsCheck.Xunit;

namespace EdS.Domain.Tests.Builds;

/// <summary>
/// Propriétés du validateur de builds. Les builds aléatoires sont construits à partir d'une graine,
/// ce qui rend chaque contre-exemple reproductible.
/// </summary>
[Trait("Category", "Build")]
public class BuildValidatorPropertyTests
{
    private static readonly ProgressionRules Rules = ProgressionRules.Provisional;
    private static readonly ClassDefinition Definition = FictionalClass.Definition;

    private static int Level(int raw) => 1 + (int)((uint)raw % (uint)Rules.MaxLevel);

    [Property(MaxTest = 1000)]
    public bool Acquisition_pas_a_pas_toujours_valide(int seed, int rawLevel)
    {
        CharacterBuild build = FictionalClass.RandomLegal(Definition, Rules, Level(rawLevel), new Random(seed));
        return BuildValidator.IsValid(build, Definition, Rules);
    }

    [Property(MaxTest = 500)]
    public bool Build_valide_reste_valide_a_un_niveau_superieur(int seed, int rawLevel, int rawGain)
    {
        int level = Level(rawLevel);
        CharacterBuild build = FictionalClass.RandomLegal(Definition, Rules, level, new Random(seed));
        int higher = level + (int)((uint)rawGain % (uint)(Rules.MaxLevel - level + 1));
        var promoted = new CharacterBuild(build.ClassId, higher, build.Attributes, build.TalentRanks, build.ActiveSkills, build.Ultimate, build.PassiveSkills);
        return BuildValidator.IsValid(promoted, Definition, Rules);
    }

    /// <summary>
    /// Build arbitraire : un build légal auquel on applique de 0 à 3 altérations au hasard (rang, talent ou compétence
    /// quelconque, niveau hors limites…), pour obtenir un mélange de builds valides et invalides.
    /// </summary>
    private static CharacterBuild Arbitrary(int seed, int rawLevel)
    {
        var random = new Random(seed);
        CharacterBuild legal = FictionalClass.RandomLegal(Definition, Rules, Level(rawLevel), random);
        var talentIds = Definition.Trees.SelectMany(t => t.Talents).Select(t => t.Id).Append("talent.inconnu.x").ToArray();
        var skillIds = Definition.Skills.Select(s => s.Id).Append("skill.inconnu.x").ToArray();
        int level = legal.Level;
        var ranks = new Dictionary<string, int>(legal.TalentRanks);
        var actives = legal.ActiveSkills.ToList();
        var passives = legal.PassiveSkills.ToList();
        string? ultimate = legal.Ultimate;
        for (int i = random.Next(4); i > 0; i--)
        {
            switch (random.Next(5))
            {
                case 0: level = random.Next(-1, 33); break;
                case 1: ranks[talentIds[random.Next(talentIds.Length)]] = random.Next(-1, 7); break;
                case 2: actives.Add(skillIds[random.Next(skillIds.Length)]); break;
                case 3: passives.Add(skillIds[random.Next(skillIds.Length)]); break;
                default: ultimate = skillIds[random.Next(skillIds.Length)]; break;
            }
        }
        return new CharacterBuild(FictionalClass.Id, level, FictionalClass.Attributes, ranks, actives, ultimate, passives);
    }

    [Property(MaxTest = 2000)]
    public bool Ne_leve_jamais_d_exception(int seed, int rawLevel)
    {
        BuildValidator.Validate(Arbitrary(seed, rawLevel), Definition, Rules);
        return true;
    }

    [Property(MaxTest = 2000)]
    public bool Build_valide_respecte_les_invariants(int seed, int rawLevel)
    {
        // Implication écrite à la main : les builds invalides passent sans être écartés du décompte.
        CharacterBuild build = Arbitrary(seed, rawLevel);
        if (!BuildValidator.IsValid(build, Definition, Rules)) return true;
        {
            int spent = build.TalentRanks.Values.Sum();
            int majors = build.TalentRanks.Keys.Count(id => Definition.TryGetTalent(id, out var t, out _) && t.IsMajor);
            var equipped = build.ActiveSkills.Concat(build.PassiveSkills).Concat(build.Ultimate is null ? Array.Empty<string>() : new[] { build.Ultimate }).ToList();
            return spent <= Rules.TalentPoints(build.Level)
                && majors <= Rules.MaxMajorTalents
                && build.TalentRanks.All(e => Definition.TryGetTalent(e.Key, out var t, out _) && e.Value >= 1 && e.Value <= t.MaxRank)
                && build.ActiveSkills.Count <= Rules.ActiveSlots(build.Level)
                && build.PassiveSkills.Count <= Rules.PassiveSlots(build.Level)
                && (build.Ultimate is null || Rules.IsUltimateUnlocked(build.Level))
                && equipped.Distinct().Count() == equipped.Count;
        }
    }

    [Fact]
    public void Generateur_arbitraire_melange_builds_valides_et_invalides()
    {
        // Garde-fou : la propriété sur les invariants n'a de sens que si des builds valides sont générés.
        int valid = Enumerable.Range(0, 500).Count(seed => BuildValidator.IsValid(Arbitrary(seed, seed * 7), Definition, Rules));
        Assert.InRange(valid, 100, 400);
    }

    [Property(MaxTest = 500)]
    public bool Validation_deterministe(int seed, int rawLevel)
    {
        CharacterBuild build = Arbitrary(seed, rawLevel);
        var first = BuildValidator.Validate(build, Definition, Rules).Select(v => v.ToString());
        var second = BuildValidator.Validate(build, Definition, Rules).Select(v => v.ToString());
        return first.SequenceEqual(second);
    }
}
