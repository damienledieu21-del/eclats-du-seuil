using EdS.Domain.Builds;
using EdS.Domain.Stats;

namespace EdS.Domain.Tests.Builds;

/// <summary>
/// Classe fictive « Essai », réservée aux tests du moteur générique de talents (aucun lien avec une vraie classe).
/// Chaque arbre : a (5 rangs), b (5, exige a rang 3), c (5), d (5), majeur (exige b rang 1),
/// et e (2 rangs) qui dépend du majeur : il ne compte pas dans le seuil de 18.
/// </summary>
internal static class FictionalClass
{
    public const string Id = "class.essai";
    public static readonly string[] TreeKeys = { "alpha", "beta", "gamma" };

    public static string T(string tree, string talent) => $"talent.essai.{tree}.{talent}";

    public static TalentTreeDefinition Tree(string key) => new($"tree.essai.{key}", $"Arbre {key}", new[]
    {
        new TalentDefinition(T(key, "a"), $"{key} A", 5),
        new TalentDefinition(T(key, "b"), $"{key} B", 5, prerequisites: new[] { new TalentRequirement(T(key, "a"), 3) }),
        new TalentDefinition(T(key, "c"), $"{key} C", 5),
        new TalentDefinition(T(key, "d"), $"{key} D", 5),
        new TalentDefinition(T(key, "majeur"), $"{key} Majeur", 1, isMajor: true, prerequisites: new[] { new TalentRequirement(T(key, "b")) }),
        new TalentDefinition(T(key, "e"), $"{key} E", 2, prerequisites: new[] { new TalentRequirement(T(key, "majeur")) }),
    });

    public static readonly SkillDefinition[] Skills =
    {
        new("skill.essai.coup", "Coup", SkillKind.Active),
        new("skill.essai.parade", "Parade", SkillKind.Active),
        new("skill.essai.charge", "Charge", SkillKind.Active),
        new("skill.essai.cri", "Cri", SkillKind.Active, minLevel: 5),
        new("skill.essai.frappe_beta", "Frappe bêta", SkillKind.Active, talentRequirements: new[] { new TalentRequirement(T("beta", "c"), 2) }),
        new("skill.essai.cataclysme", "Cataclysme", SkillKind.Ultimate, minLevel: 10),
        new("skill.essai.passif_1", "Passif 1", SkillKind.Passive),
        new("skill.essai.passif_2", "Passif 2", SkillKind.Passive),
        new("skill.essai.passif_3", "Passif 3", SkillKind.Passive),
        new("skill.essai.passif_4", "Passif 4", SkillKind.Passive),
        new("skill.essai.passif_5", "Passif 5", SkillKind.Passive, minLevel: 30),
    };

    public static ClassDefinition Create(Func<string, TalentTreeDefinition>? tree = null, IReadOnlyList<SkillDefinition>? skills = null) =>
        new(Id, "Essai", TreeKeys.Select(tree ?? Tree).ToArray(), skills ?? Skills);

    public static readonly ClassDefinition Definition = Create();

    public static readonly Attributes Attributes = new(10, 10, 10, 10, 10, 10);

    public static CharacterBuild Build(int level, IReadOnlyDictionary<string, int>? talents = null,
        IReadOnlyList<string>? actives = null, string? ultimate = null, IReadOnlyList<string>? passives = null) =>
        new(Id, level, Attributes, talents, actives, ultimate, passives);

    /// <summary>
    /// Build obtenu en acquérant les points un par un, dans un ordre légal (prérequis remplis, seuil du majeur atteint),
    /// comme le ferait un joueur ; les compétences sont tirées parmi celles qu'il peut équiper.
    /// </summary>
    public static CharacterBuild RandomLegal(ClassDefinition definition, ProgressionRules rules, int level, Random random)
    {
        var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
        int Rank(string id) => ranks.TryGetValue(id, out int r) ? r : 0;
        int budget = random.Next(rules.TalentPoints(level) + 1);
        int majors = 0;

        for (int spent = 0; spent < budget; spent++)
        {
            var candidates = definition.Trees.SelectMany(tree => tree.Talents.Select(talent => (tree, talent)))
                .Where(x => Rank(x.talent.Id) < x.talent.MaxRank && x.talent.Prerequisites.All(p => Rank(p.TalentId) >= p.MinRank))
                .Where(x => !x.talent.IsMajor || (majors < rules.MaxMajorTalents
                    && x.tree.Talents.Where(t => t.Id != x.talent.Id).Sum(t => Rank(t.Id)) >= rules.MajorTalentThreshold))
                .ToList();
            if (candidates.Count == 0) break;
            var (_, chosen) = candidates[random.Next(candidates.Count)];
            ranks[chosen.Id] = Rank(chosen.Id) + 1;
            if (chosen.IsMajor) majors++;
        }

        List<string> Pick(SkillKind kind, int slots) => definition.Skills
            .Where(s => s.Kind == kind && s.MinLevel <= level && s.TalentRequirements.All(r => Rank(r.TalentId) >= r.MinRank))
            .OrderBy(_ => random.Next())
            .Take(random.Next(slots + 1))
            .Select(s => s.Id)
            .ToList();

        string? ultimate = rules.IsUltimateUnlocked(level) && random.Next(2) == 0
            ? Pick(SkillKind.Ultimate, 1).FirstOrDefault()
            : null;
        return new CharacterBuild(definition.Id, level, Attributes, ranks,
            Pick(SkillKind.Active, rules.ActiveSlots(level)), ultimate, Pick(SkillKind.Passive, rules.PassiveSlots(level)));
    }
}
