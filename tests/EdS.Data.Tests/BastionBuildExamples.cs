using EdS.Domain.Builds;
using EdS.Domain.Stats;
using static EdS.Data.Tests.ClassTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Exemples de builds du Bastion, valides et invalides, validés contre les fichiers livrés
/// (<c>data/v0/classes/bastion.json</c> et la progression de <c>data/v0/rules.json</c>).
/// Les talents « [TEST] Palier » n'ont aucun effet : ils servent seulement à atteindre le seuil des majeurs.
/// </summary>
[Trait("Category", "Data")]
public class BastionBuildExamples
{
    private static readonly Attributes Attributes = new(14, 8, 6, 14, 10, 12);   // F5 : attributs explicites

    private static string R(string talent) => $"talent.bastion.rempart.{talent}";
    private static string C(string talent) => $"talent.bastion.contrecoup.{talent}";
    private static string G(string talent) => $"talent.bastion.gardien.{talent}";
    private static string S(string skill) => $"skill.bastion.{skill}";

    private static readonly string[] FourActives = { S("frappe_du_rempart"), S("heurt_de_bouclier"), S("provocation"), S("barriere_d_acier") };
    private static readonly string[] FourPassives = { S("test_passif_1"), S("test_passif_2"), S("test_passif_3"), S("test_passif_4") };

    private static CharacterBuild Build(int level, Dictionary<string, int> talents, string[]? actives = null, string? ultimate = null, string[]? passives = null) =>
        new("class.bastion", level, Attributes, talents, actives, ultimate, passives);

    /// <summary>Les 18 points minimum d'une branche (palier 3 complétant le total), puis son majeur.</summary>
    private static Dictionary<string, int> Major(Func<string, string> tree, string first, int firstRank, string second, int secondRank, string major, int? palier3 = null) => new()
    {
        [tree(first)] = firstRank, [tree(second)] = secondRank,
        [tree("test_palier_1")] = 5, [tree("test_palier_2")] = 5, [tree("test_palier_3")] = palier3 ?? 18 - 10 - firstRank - secondRank,
        [tree(major)] = 1,
    };

    private static Dictionary<string, int> RempartMajor(int? palier3 = null) => Major(R, "garde_stable", 3, "echo_defensif", 1, "rempart_eternel", palier3);
    private static Dictionary<string, int> ContrecoupMajor() => Major(C, "riposte_precise", 3, "fureur_contenue", 2, "jugement_du_colosse");
    private static Dictionary<string, int> GardienMajor() => Major(G, "egide_reactive", 3, "lien_protecteur", 1, "serment_du_gardien");

    private static Dictionary<string, int> With(Dictionary<string, int> talents, params (string Id, int Rank)[] more)
    {
        foreach (var (id, rank) in more) talents[id] = rank;
        return talents;
    }

    public static TheoryData<string, CharacterBuild> Valid() => new()
    {
        { "Rempart défensif, niveau 30 (29 points)",
            Build(30, With(RempartMajor(), (G("egide_reactive"), 3), (G("lien_protecteur"), 1), (G("test_palier_1"), 5), (C("riposte_precise"), 1)),
                FourActives, S("bastion_absolu"), FourPassives) },
        { "Contrecoup offensif, niveau 30 (28 points, 1 non dépensé)",
            Build(30, With(ContrecoupMajor(), (R("garde_stable"), 3), (R("echo_defensif"), 1), (R("test_palier_1"), 5)),
                new[] { S("frappe_du_rempart"), S("contrecoup"), S("onde_de_rupture"), S("heurt_de_bouclier") }, S("bastion_absolu"), FourPassives) },
        { "Gardien protecteur, niveau 30 (29 points)",
            Build(30, With(GardienMajor(), (R("garde_stable"), 3), (C("riposte_precise"), 3), (C("fureur_contenue"), 2), (C("test_palier_1"), 2)),
                new[] { S("provocation"), S("barriere_d_acier"), S("heurt_de_bouclier"), S("onde_de_rupture") }, S("bastion_absolu"), FourPassives) },
        { "Hybride sans majeur, niveau 30 (10 + 10 + 9)",
            Build(30, new() { [R("garde_stable")] = 3, [R("echo_defensif")] = 1, [R("test_palier_1")] = 5, [R("test_palier_2")] = 1,
                [C("riposte_precise")] = 3, [C("fureur_contenue")] = 2, [C("test_palier_1")] = 5, [G("egide_reactive")] = 3, [G("lien_protecteur")] = 1, [G("test_palier_1")] = 5 },
                FourActives, S("bastion_absolu"), FourPassives) },
        { "Débutant, niveau 1 (aucun point, 2 actives, 1 passif)",
            Build(1, new(), new[] { S("frappe_du_rempart"), S("provocation") }, null, new[] { S("test_passif_1") }) },
        { "Niveau 10, ultime débloquée",
            Build(10, new() { [R("garde_stable")] = 3, [R("echo_defensif")] = 1, [G("egide_reactive")] = 3 }, FourActives, S("bastion_absolu"), new[] { S("test_passif_1"), S("test_passif_2") }) },
    };

    public static TheoryData<string, CharacterBuild, BuildViolationCode> Invalid() => new()
    {
        { "Deux talents majeurs", Build(30, With(RempartMajor(), ContrecoupMajor().Select(e => (e.Key, e.Value)).ToArray())), BuildViolationCode.TooManyMajorTalents },
        { "Majeur avec 17 points dans la branche", Build(30, RempartMajor(palier3: 3)), BuildViolationCode.MajorThresholdNotMet },
        { "Écho défensif sans Garde stable rang 3", Build(30, new() { [R("garde_stable")] = 2, [R("echo_defensif")] = 1 }), BuildViolationCode.PrerequisiteNotMet },
        { "Garde stable au rang 4 (maximum 3)", Build(30, new() { [R("garde_stable")] = 4 }), BuildViolationCode.RankOutOfRange },
        { "Niveau 31", Build(31, new()), BuildViolationCode.LevelOutOfRange },
        { "10 points au niveau 10 (9 disponibles)", Build(10, new() { [R("test_palier_1")] = 5, [G("test_palier_1")] = 5 }), BuildViolationCode.TooManyPoints },
        { "Ultime au niveau 9", Build(9, new(), ultimate: S("bastion_absolu")), BuildViolationCode.UltimateLocked },
        { "3 actives au niveau 4", Build(4, new(), new[] { S("frappe_du_rempart"), S("provocation"), S("contrecoup") }), BuildViolationCode.TooManySkills },
        { "Compétence équipée deux fois", Build(10, new(), new[] { S("provocation"), S("provocation") }), BuildViolationCode.DuplicateSkill },
        { "Ultime dans un emplacement actif", Build(10, new(), new[] { S("bastion_absolu") }), BuildViolationCode.WrongSlotKind },
        { "Active dans un emplacement passif", Build(10, new(), passives: new[] { S("provocation") }), BuildViolationCode.WrongSlotKind },
        { "Talent inconnu", Build(30, new() { ["talent.bastion.rempart.inexistant"] = 1 }), BuildViolationCode.UnknownTalent },
        { "Compétence inconnue", Build(10, new(), new[] { "skill.bastion.inexistante" }), BuildViolationCode.UnknownSkill },
        { "3 passifs au niveau 19", Build(19, new(), passives: new[] { S("test_passif_1"), S("test_passif_2"), S("test_passif_3") }), BuildViolationCode.TooManySkills },
    };

    [Theory]
    [MemberData(nameof(Valid))]
    public void Build_valide(string name, CharacterBuild build)
    {
        var violations = BuildValidator.Validate(build, Bastion, Progression);
        Assert.True(violations.Count == 0, $"{name} : {string.Join(" | ", violations)}");
    }

    [Theory]
    [MemberData(nameof(Invalid))]
    public void Build_invalide(string name, CharacterBuild build, BuildViolationCode expected)
    {
        var violations = BuildValidator.Validate(build, Bastion, Progression);
        Assert.True(violations.Any(v => v.Code == expected), $"{name} : {expected} attendu ; obtenu : {string.Join(" | ", violations)}");
    }

    [Fact]
    public void Profils_de_niveau_30_atteignent_chacun_un_majeur_different()
    {
        var majors = Valid().Select(row => (CharacterBuild)row[1])
            .SelectMany(b => b.TalentRanks.Keys.Where(id => Bastion.TryGetTalent(id, out var t, out _) && t.IsMajor))
            .ToList();
        Assert.Equal(new[] { R("rempart_eternel"), C("jugement_du_colosse"), G("serment_du_gardien") }, majors);
    }
}
