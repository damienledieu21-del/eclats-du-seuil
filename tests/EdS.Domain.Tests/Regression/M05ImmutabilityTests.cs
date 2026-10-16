using EdS.Domain.Builds;
using EdS.Domain.Combat;
using static EdS.Domain.Tests.Builds.FictionalClass;

namespace EdS.Domain.Tests.Regression;

/// <summary>
/// Correctif 6.1, M05 : définitions, builds, règles et descriptions de combat copient les collections reçues et
/// n'exposent que des vues en lecture seule. Ni la modification de la collection d'origine, ni un transtypage de la
/// propriété exposée ne peuvent changer les données après construction, après validation ou pendant un combat.
/// </summary>
[Trait("Category", "Build")]
[Trait("Category", "Regression")]
public class M05ImmutabilityTests
{
    /// <summary>La vue exposée n'est ni un tableau ni une liste modifiable, et toute écriture par transtypage échoue.</summary>
    internal static void AssertFrozen<T>(IReadOnlyList<T> list)
    {
        Assert.False(list is T[], "tableau interne exposé");
        Assert.False(list is List<T>, "liste interne exposée");
        if (list is IList<T> writable && writable.Count > 0)
        {
            Assert.True(writable.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => writable[0] = writable[0]);
            Assert.Throws<NotSupportedException>(() => writable.Add(writable[0]));
            Assert.Throws<NotSupportedException>(() => writable.Clear());
        }
    }

    internal static void AssertFrozen<TValue>(IReadOnlyDictionary<string, TValue> dictionary)
    {
        Assert.False(dictionary is Dictionary<string, TValue>, "dictionnaire interne exposé");
        if (dictionary is IDictionary<string, TValue> writable)
        {
            Assert.True(writable.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => writable["intrus"] = default!);
        }
    }

    [Fact]
    public void Arbre_talent_competence_copies_a_la_construction()
    {
        var talents = new[] { new TalentDefinition("talent.essai.x.a", "A", 5), new TalentDefinition("talent.essai.x.b", "B", 5) };
        var requirements = new List<TalentRequirement> { new("talent.essai.x.a", 2) };
        var tree = new TalentTreeDefinition("tree.essai.x", "X", talents);
        var talent = new TalentDefinition("talent.essai.x.c", "C", 1, prerequisites: requirements);
        var skill = new SkillDefinition("skill.essai.y", "Y", SkillKind.Active, talentRequirements: requirements);

        talents[0] = new TalentDefinition("talent.essai.x.intrus", "Intrus", 99);
        requirements.Clear();

        Assert.Equal("talent.essai.x.a", tree.Talents[0].Id);
        Assert.Single(talent.Prerequisites);
        Assert.Single(skill.TalentRequirements);
        AssertFrozen(tree.Talents);
        AssertFrozen(talent.Prerequisites);
        AssertFrozen(skill.TalentRequirements);
    }

    [Fact]
    public void Classe_index_et_validation_figes()
    {
        TalentTreeDefinition[] trees = TreeKeys.Select(Tree).ToArray();
        var skills = Skills.ToList();
        var definition = new ClassDefinition(Id, "Essai", trees, skills);
        var before = definition.Validate(ProgressionRules.Provisional).Select(v => v.ToString()).ToList();

        trees[0] = new TalentTreeDefinition("tree.essai.intrus", "Intrus", new[] { new TalentDefinition("talent.essai.intrus.a", "A", 1) });
        skills.Clear();

        Assert.Equal("tree.essai.alpha", definition.Trees[0].Id);
        Assert.Equal(Skills.Length, definition.Skills.Count);
        Assert.True(definition.TryGetTalent(T("alpha", "a"), out _, out _));
        Assert.False(definition.TryGetTalent("talent.essai.intrus.a", out _, out _));
        Assert.True(definition.TryGetSkill("skill.essai.coup", out _));
        Assert.Equal(before, definition.Validate(ProgressionRules.Provisional).Select(v => v.ToString()));
        AssertFrozen(definition.Trees);
        AssertFrozen(definition.Skills);
    }

    [Fact]
    public void Build_fige_avant_et_apres_validation()
    {
        var talents = new Dictionary<string, int> { [T("alpha", "a")] = 3 };
        var actives = new List<string> { "skill.essai.coup" };
        var passives = new[] { "skill.essai.passif_1" };
        var build = new CharacterBuild(Id, 10, Attributes, talents, actives, null, passives);

        talents[T("alpha", "a")] = 99;                // après construction
        actives.Add("skill.essai.inconnue");
        Assert.Empty(BuildValidator.Validate(build, Definition, ProgressionRules.Provisional));

        talents[T("beta", "a")] = int.MaxValue;       // après validation
        passives[0] = "skill.essai.inconnue";
        Assert.Equal(3, build.TalentRanks[T("alpha", "a")]);
        Assert.Single(build.TalentRanks);
        Assert.Equal(new[] { "skill.essai.coup" }, build.ActiveSkills);
        Assert.Equal(new[] { "skill.essai.passif_1" }, build.PassiveSkills);
        Assert.Empty(BuildValidator.Validate(build, Definition, ProgressionRules.Provisional));
        AssertFrozen(build.TalentRanks);
        AssertFrozen(build.ActiveSkills);
        AssertFrozen(build.PassiveSkills);
    }

    [Fact]
    public void Elements_nuls_refuses_a_la_construction()
    {
        Assert.Throws<ArgumentException>(() => new TalentTreeDefinition("tree.essai.x", "X", new TalentDefinition[] { null! }));
        Assert.Throws<ArgumentException>(() => new CharacterBuild(Id, 10, Attributes, activeSkills: new string[] { null! }));
        Assert.Throws<ArgumentNullException>(() => new ClassDefinition(Id, "Essai", null!, Skills));
    }

    [Fact]
    public void Regles_de_progression_copiees()
    {
        int[] active = { 1, 1, 5, 10 };
        var rules = new ProgressionRules { ActiveSlotLevels = active };
        active[0] = 30;
        Assert.Equal(2, rules.ActiveSlots(1));
        AssertFrozen(rules.ActiveSlotLevels);
        AssertFrozen(ProgressionRules.Provisional.PassiveSlotLevels);
    }

    [Fact]
    public void Description_de_combat_et_assaillant_copies()
    {
        var attacks = new[] { new AssailantAttack("coup", 100) };
        var assailant = new AssailantSetup("assaillant", "cibles", default, default, "bastion", 1000, 2000, attacks);
        attacks[0] = new AssailantAttack("enorme", CombatantStats.MaxRawDamage);
        Assert.Equal("coup", assailant.Attacks[0].Id);
        AssertFrozen(assailant.Attacks);

        var classes = new List<DataFileIdentity> { new("class.b", "1", "x"), new("class.a", "1", "y") };
        var identity = new CombatDataIdentity(new DataFileIdentity("rules", "1", "z"), classes);
        classes.Clear();
        Assert.Equal(new[] { "class.a", "class.b" }, identity.Classes.Select(c => c.Id));
        AssertFrozen(identity.Classes);
        AssertFrozen(CombatActionIds.Defensive);

        var fields = new[] { "a", "b" };
        var violation = new RuleViolation("m", fields);
        fields[0] = "intrus";
        Assert.Equal(new[] { "a", "b" }, violation.Fields);
        AssertFrozen(violation.Fields);
    }
}
