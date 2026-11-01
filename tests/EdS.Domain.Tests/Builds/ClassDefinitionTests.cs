using EdS.Domain.Builds;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;

namespace EdS.Domain.Tests.Builds;

/// <summary>Validation des définitions de classe sur des arbres fictifs, pour éprouver le moteur générique.</summary>
[Trait("Category", "Build")]
public class ClassDefinitionTests
{
    // Sous-lot V1.8 : ces tests jugent la mécanique des places (actives qui s'ouvrent en montant, ultime fermée avant son niveau) avec les
    // règles d'avant (2 actives au niveau 1, 3 au 5, 4 et l'ultime au 10) ; les règles livrées (4 et l'ultime au niveau 1) sont dans
    // ProgressionRulesTests.
    private static readonly ProgressionRules Rules = new ProgressionRules { ActiveSlotLevels = new[] { 1, 1, 5, 10 }, UltimateUnlockLevel = 10 };

    private static IReadOnlyList<RuleViolation> Validate(ClassDefinition definition) => definition.Validate(Rules);

    /// <summary>Remplace l'arbre « alpha » par une liste de talents donnée.</summary>
    private static ClassDefinition WithAlpha(params TalentDefinition[] talents) =>
        FictionalClass.Create(key => key == "alpha" ? new TalentTreeDefinition("tree.essai.alpha", "Alpha", talents) : FictionalClass.Tree(key));

    private static void AssertViolation(ClassDefinition definition, string field, RuleViolationKind kind = RuleViolationKind.InvalidValue)
    {
        var violations = Validate(definition);
        Assert.True(violations.Any(v => v.Fields.Contains(field) && v.Kind == kind),
            $"{field} [{kind}] attendu ; obtenu : {string.Join(" | ", violations)}");
    }

    private static string A(string talent) => FictionalClass.T("alpha", talent);

    [Fact]
    public void Classe_fictive_valide() => Assert.Empty(Validate(FictionalClass.Definition));

    [Fact]
    public void Identifiant_en_double()
    {
        var talents = FictionalClass.Tree("alpha").Talents.Append(new TalentDefinition(A("a"), "Copie", 1)).ToArray();
        AssertViolation(WithAlpha(talents), "trees[0].talents[6].id", RuleViolationKind.DuplicateId);
    }

    [Fact]
    public void Identifiant_mal_forme_ou_mal_prefixe()
    {
        AssertViolation(WithAlpha(FictionalClass.Tree("alpha").Talents.Select((t, i) => i == 0 ? new TalentDefinition("talent.Essai.A", "A", 5) : t).ToArray()),
            "trees[0].talents[0].id");
        AssertViolation(new ClassDefinition("tree.essai", "Essai", FictionalClass.Definition.Trees, FictionalClass.Skills), "id");
    }

    [Fact]
    public void Prerequis_inconnu()
    {
        var talents = FictionalClass.Tree("alpha").Talents.ToList();
        talents[1] = new TalentDefinition(A("b"), "B", 5, prerequisites: new[] { new TalentRequirement("talent.essai.alpha.fantome") });
        AssertViolation(WithAlpha(talents.ToArray()), "trees[0].talents[1].prerequisites[0].talent", RuleViolationKind.UnknownReference);
    }

    [Fact]
    public void Prerequis_de_competence_inconnu()
    {
        var skills = FictionalClass.Skills.Append(new SkillDefinition("skill.essai.x", "X", SkillKind.Active,
            talentRequirements: new[] { new TalentRequirement("talent.essai.nulle_part") })).ToArray();
        AssertViolation(FictionalClass.Create(skills: skills), $"skills[{skills.Length - 1}].talentRequirements[0].talent", RuleViolationKind.UnknownReference);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Rang_exige_hors_limites(int rank)
    {
        var talents = FictionalClass.Tree("alpha").Talents.ToList();
        talents[1] = new TalentDefinition(A("b"), "B", 5, prerequisites: new[] { new TalentRequirement(A("a"), rank) });
        AssertViolation(WithAlpha(talents.ToArray()), "trees[0].talents[1].prerequisites[0].rank");
    }

    [Fact]
    public void Rang_maximum_invalide_et_majeur_a_plusieurs_rangs()
    {
        var talents = FictionalClass.Tree("alpha").Talents.ToList();
        talents[2] = new TalentDefinition(A("c"), "C", 0);
        talents[4] = new TalentDefinition(A("majeur"), "Majeur", 2, isMajor: true, prerequisites: new[] { new TalentRequirement(A("b")) });
        var definition = WithAlpha(talents.ToArray());
        AssertViolation(definition, "trees[0].talents[2].maxRank");
        AssertViolation(definition, "trees[0].talents[4].maxRank");
    }

    [Fact]
    public void Cycle_de_prerequis()
    {
        var definition = WithAlpha(
            new TalentDefinition(A("a"), "A", 5, prerequisites: new[] { new TalentRequirement(A("c")) }),
            new TalentDefinition(A("b"), "B", 5, prerequisites: new[] { new TalentRequirement(A("a")) }),
            new TalentDefinition(A("c"), "C", 5, prerequisites: new[] { new TalentRequirement(A("b")) }),
            new TalentDefinition(A("d"), "D", 5),
            new TalentDefinition(A("f"), "F", 5),
            new TalentDefinition(A("majeur"), "Majeur", 1, isMajor: true));
        var violations = Validate(definition);
        Assert.Contains(violations, v => v.Message.StartsWith("cycle de prérequis", StringComparison.Ordinal));
    }

    [Fact]
    public void Talent_qui_s_exige_lui_meme()
    {
        var talents = FictionalClass.Tree("alpha").Talents.ToList();
        talents[2] = new TalentDefinition(A("c"), "C", 5, prerequisites: new[] { new TalentRequirement(A("c")) });
        AssertViolation(WithAlpha(talents.ToArray()), "trees[0].talents[2].prerequisites[0].talent");
    }

    [Fact]
    public void Majeur_inaccessible_si_la_branche_offre_moins_de_18_points()
    {
        // 5 + 5 + 5 + 2 = 17 points hors majeur.
        var definition = WithAlpha(
            new TalentDefinition(A("a"), "A", 5),
            new TalentDefinition(A("b"), "B", 5),
            new TalentDefinition(A("c"), "C", 5),
            new TalentDefinition(A("d"), "D", 2),
            new TalentDefinition(A("majeur"), "Majeur", 1, isMajor: true));
        AssertViolation(definition, "trees[0].talents[4]");
    }

    [Fact]
    public void Talents_dependant_du_majeur_exclus_de_son_seuil()
    {
        // 18 points au total hors majeur, mais 3 dépendent du majeur : il n'en reste que 15 avant lui.
        var definition = WithAlpha(
            new TalentDefinition(A("a"), "A", 5),
            new TalentDefinition(A("b"), "B", 5),
            new TalentDefinition(A("c"), "C", 5),
            new TalentDefinition(A("majeur"), "Majeur", 1, isMajor: true),
            new TalentDefinition(A("apres"), "Après", 3, prerequisites: new[] { new TalentRequirement(A("majeur")) }));
        AssertViolation(definition, "trees[0].talents[3]");
    }

    [Fact]
    public void Majeur_ne_peut_pas_exiger_un_majeur()
    {
        var talents = FictionalClass.Tree("alpha").Talents.ToList();
        talents.Add(new TalentDefinition(A("second_majeur"), "Second majeur", 1, isMajor: true, prerequisites: new[] { new TalentRequirement(A("majeur")) }));
        AssertViolation(WithAlpha(talents.ToArray()), "trees[0].talents[6].prerequisites[0].talent");
    }

    [Fact]
    public void Nombre_d_arbres_impose_par_les_regles()
    {
        var definition = new ClassDefinition(FictionalClass.Id, "Essai", new[] { FictionalClass.Tree("alpha") }, FictionalClass.Skills);
        AssertViolation(definition, "trees");
    }

    [Fact]
    public void Arbre_vide_refuse() => AssertViolation(WithAlpha(), "trees[0].talents");

    [Fact]
    public void Ultime_avant_le_niveau_10_refusee()
    {
        var skills = FictionalClass.Skills.Select(s => s.Kind == SkillKind.Ultimate ? new SkillDefinition(s.Id, s.Name, s.Kind, 9) : s).ToArray();
        int index = Array.FindIndex(skills, s => s.Kind == SkillKind.Ultimate);
        AssertViolation(FictionalClass.Create(skills: skills), $"skills[{index}].minLevel");
    }

    [Fact]
    public void Niveau_minimal_de_competence_hors_limites()
    {
        var skills = FictionalClass.Skills.Append(new SkillDefinition("skill.essai.trop_tard", "Trop tard", SkillKind.Active, 31)).ToArray();
        AssertViolation(FictionalClass.Create(skills: skills), $"skills[{skills.Length - 1}].minLevel");
    }

    [Fact]
    public void Donnees_de_combat_incoherentes()
    {
        var bad = new SkillCombatDefinition(PowerType.Physique, DamageType.Physique, Fixed.Parse("-1"), -5, -1000,
            rampartChargeCost: -1, rampartChargeGain: -2);
        var skills = FictionalClass.Skills
            .Append(new SkillDefinition("skill.essai.mauvaise", "Mauvaise", SkillKind.Active, combat: bad))
            .Append(new SkillDefinition("skill.essai.passif_lance", "Passif lancé", SkillKind.Passive,
                combat: new SkillCombatDefinition(PowerType.Physique, DamageType.Physique, Fixed.One, 0, 0)))
            .ToArray();
        var definition = FictionalClass.Create(skills: skills);
        int invalid = skills.Length - 2, passive = skills.Length - 1;
        AssertViolation(definition, $"skills[{invalid}].combat.coefficient");
        AssertViolation(definition, $"skills[{invalid}].combat.enduranceCost");
        AssertViolation(definition, $"skills[{invalid}].combat.cooldownMs");
        AssertViolation(definition, $"skills[{invalid}].combat.rampartChargeCost");
        AssertViolation(definition, $"skills[{invalid}].combat.rampartChargeGain");
        AssertViolation(definition, $"skills[{passive}].combat");
    }

    [Fact]
    public void Dependants_transitifs()
    {
        ISet<string> dependents = FictionalClass.Definition.DependentsOf(A("a"));
        Assert.Equal(new[] { A("b"), A("e"), A("majeur") }.OrderBy(x => x), dependents.OrderBy(x => x));
    }
}
