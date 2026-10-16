using EdS.Data.Classes;
using EdS.Data.Rules;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Correctif 6.1, M05 côté données : catalogues, index et données chargées n'exposent aucune collection modifiable.
/// Les définitions, règles et empreintes chargées représentent toujours les mêmes données figées.
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Regression")]
public class M05DataImmutabilityTests
{
    private static void AssertReadOnly<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> dictionary) where TKey : notnull
    {
        Assert.False(dictionary is Dictionary<TKey, TValue>);
        Assert.True(((IDictionary<TKey, TValue>)dictionary).IsReadOnly);
    }

    private static void AssertReadOnly<T>(IReadOnlyList<T> list)
    {
        Assert.False(list is T[]);
        Assert.False(list is List<T>);
        Assert.True(((IList<T>)list).IsReadOnly);
    }

    [Fact]
    public void Catalogues_statiques_non_modifiables()
    {
        AssertReadOnly(ClassValidator.SkillKinds);
        AssertReadOnly(ClassValidator.PowerTypes);
        AssertReadOnly(ClassValidator.DamageTypes);
        AssertReadOnly(ClassValidator.Targetings);
        AssertReadOnly(RulesCatalog.ReferenceMaps);
        AssertReadOnly(RulesCatalog.Fields);
        AssertReadOnly(RulesCatalog.RoundingTokens);
        AssertReadOnly(ScenarioValidator.CombatantTypes);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, Domain.Builds.SkillKind>)ClassValidator.SkillKinds)["intrus"] = default);
        Assert.Throws<NotSupportedException>(() => ((IList<RuleField>)RulesCatalog.Fields).RemoveAt(0));
    }

    [Fact]
    public void Donnees_chargees_figees()
    {
        GameData data = ShippedData;
        AssertReadOnly(data.Classes);
        AssertReadOnly(data.Identity.Classes);
        AssertReadOnly(data.Classes[0].Definition.Trees);
        AssertReadOnly(data.Classes[0].Definition.Trees[0].Talents);
        AssertReadOnly(data.Rules.Progression.ActiveSlotLevels);
        var report = new ValidationReport();
        AssertReadOnly(report.Errors);
    }

    [Fact]
    public void Index_et_empreintes_restent_coherents_apres_mutation_de_la_source()
    {
        var classes = new List<LoadedClass>(ShippedData.Classes);
        var data = new GameData(ShippedData.Rules, classes);
        CombatDataIdentity before = data.Identity;
        classes.Clear();
        Assert.Equal(ShippedData.Classes.Count, data.Classes.Count);
        Assert.True(data.TryGetClass("class.bastion", out LoadedClass bastion));
        Assert.Same(data.Classes.Single(), bastion);
        Assert.Equal(before.Classes.Select(c => c.Fingerprint), data.Identity.Classes.Select(c => c.Fingerprint));
    }

    [Fact]
    public void Liste_de_valeurs_de_regle_copiee()
    {
        var source = new List<long> { 1, 1, 5, 10 };
        RuleValue value = RuleValue.FromIntegerList(source);
        string canonical = value.ToCanonicalString();
        source[0] = 30;
        Assert.Equal(canonical, value.ToCanonicalString());
        AssertReadOnly(value.List!);
    }
}
