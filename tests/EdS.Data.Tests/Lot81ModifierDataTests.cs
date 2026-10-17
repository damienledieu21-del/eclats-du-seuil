using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EdS.Data.Classes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.Lot81ModifierJson;

namespace EdS.Data.Tests;

// Sous-lot 8.1, couche données : lecture JSON des modificateurs de classe (X1, X2), forme canonique et empreinte, version de
// schéma 5, bout en bout (scénario, journal, rejeu) et immuabilité de la classe chargée. Correspondance avec la liste
// obligatoire des tests du sous-lot :
//   1. chaque clé au rang 1 et au rang maximal ......... Lot81ModifierDataReadingTests.Borne_de_chaque_cle_au_rang_1_et_au_rang_maximal
//                                                        (talent aux rangs 1 et 3, passif), Drapeau_un_exactement
//   2. bornes N−1, N et N+1 ............................ même théorie (|add| déclaré), Nombre_de_modificateurs_par_porteur_7_8_admis_9_refuse,
//                                                        Plafond_de_512_modificateurs_par_classe_* (511, 512, 513)
//   3. combinaisons de modificateurs ................... Lot81ModifierDataEndToEndTests : Scenario_de_fichier_… (cinq porteurs),
//                                                        Modificateurs_equivalents_aux_regles_de_base_correspondantes,
//                                                        Combinaisons_ecretees_dans_l_objet_rules, Conditionnels_agreges_… ; doublons
//                                                        et ensembles de conditions admis (Lot81ModifierDataReadingTests)
//   4. conditions vraies et fausses dans le même combat  hors de cette couche (moteur : EdS.Simulation.Tests) ; ici, la liste
//                                                        « conditional » du journal, lue au calcul des dégâts, est vérifiée exactement
//   5. agrégation indépendante de l'ordre .............. Ordre_des_sources_sans_effet_sur_le_combat_mais_dans_l_empreinte
//   6. immuabilité ..................................... Lot81ModifierDataImmutabilityTests
//   7. débordements refusés avant simulation ........... Debordement_d_une_valeur_par_rang_refuse_au_chargement ; une classe refusée ne
//                                                        donne aucune donnée de combat (Classe_refusee_aucune_donnee_chargee)
//   8. aucune mutation partielle ....................... Classe_refusee_aucune_donnee_chargee (tout ou rien), Lot81ModifierDataImmutabilityTests
//   9. déterminisme et rejeu ........................... Scenario_de_fichier_simule_journal_avec_regles_effectives_rejeu_identique (deux
//                                                        simulations, rejeu identique, journal altéré, données différentes), Relecture_meme_empreinte
//  10. non-régression des 5 scénarios .................. déjà couverte (Lot81ReferenceTests, Lot81EngineBaselineTests) ; ici, des
//                                                        modificateurs inactifs laissent le journal livré inchangé (Sources_inactives_…)

/// <summary>Sous-lot 8.1 : constructeurs JSON de modificateurs et lecture de bastion.json muté, partagés par les tests de ce fichier.</summary>
internal static class Lot81ModifierJson
{
    /// <summary>Talent « garde_stable » (arbre 0, talent 0) : 3 rangs, pris au rang 3 par entrainement_garde_parade.</summary>
    public const string TalentPath = "$.data.trees[0].talents[0].modifiers";

    /// <summary>Compétence passive « test_passif_1 » (compétence 7), équipée par entrainement_garde_parade.</summary>
    public const string PassivePath = "$.data.skills[7].modifiers";

    /// <summary>Modificateur <c>{ key, add, conditions }</c> ; <paramref name="add"/> est un texte JSON (nombre ou objet).</summary>
    public static JsonNode Mod(string key, string add, params string[] conditions)
    {
        var o = new JsonObject { ["key"] = key, ["add"] = JsonNode.Parse(add) };
        if (conditions.Length > 0) o["conditions"] = new JsonArray(conditions.Select(c => (JsonNode?)JsonValue.Create(c)).ToArray());
        return o;
    }

    public static JsonArray List(params JsonNode?[] modifiers) => new(modifiers);

    public static ClassLoadResult OnTalent(int tree, int talent, params JsonNode?[] modifiers) =>
        ClassTestData.ReadMutated(root => ClassTestData.Talent(root, tree, talent)["modifiers"] = List(modifiers));

    public static ClassLoadResult OnSkill(int skill, params JsonNode?[] modifiers) =>
        ClassTestData.ReadMutated(root => ClassTestData.Skill(root, skill)["modifiers"] = List(modifiers));

    /// <summary>Porteur désigné par son nom de test : « talent » (garde_stable) ou « passif » (test_passif_1).</summary>
    public static ClassLoadResult On(string owner, params JsonNode?[] modifiers) => owner == "passif" ? OnSkill(7, modifiers) : OnTalent(0, 0, modifiers);

    public static string PathOf(string owner) => owner == "passif" ? PassivePath : TalentPath;

    public static LoadedClass Valid(ClassLoadResult result)
    {
        Assert.True(result.IsValid, result.Report.ToString());
        Assert.Empty(result.Report.Warnings);
        return result.Class!;
    }

    public static void AssertSingleError(ClassLoadResult result, string path, ValidationCode code)
    {
        Assert.Null(result.Class);
        ValidationError error = Assert.Single(result.Report.Errors);
        Assert.Equal((path, code), (error.Path, error.Code));
    }

    public static string Fingerprint(Action<JsonObject> mutate)
    {
        ClassLoadResult result = ClassTestData.ReadMutated(mutate);
        Assert.True(result.IsValid, result.Report.ToString());
        return result.Class!.Fingerprint;
    }

    public static string ShippedFingerprint => ClassJsonReader.Read(ClassTestData.BastionJson, ClassTestData.Progression).Class!.Fingerprint;

    /// <summary>Nombre JSON d'un décimal exact (sans zéro final, 4 décimales au plus), écrit indépendamment de <see cref="Fixed"/>.</summary>
    public static string Num(decimal value) => value == 0m ? "0" : value.ToString("0.####", CultureInfo.InvariantCulture);
}

/// <summary>
/// Sous-lot 8.1 : lecture des modificateurs d'un talent ou d'une compétence passive (clé, valeur par rang, conditions), erreurs de
/// forme localisées par leur chemin JSON, erreurs de domaine (ClassDefinition.Validate, catalogue et tick au scénario) rapportées au
/// fichier, bornes déclarées de chaque clé aux rangs 1 et maximal, plafonds par porteur et par classe.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Data")]
public class Lot81ModifierDataReadingTests
{
    // --- Lecture ---------------------------------------------------------------------------

    [Fact]
    public void Classe_livree_au_schema_5_sans_modificateur()
    {
        LoadedClass shipped = ClassJsonReader.Read(ClassTestData.BastionJson, ClassTestData.Progression).Class!;
        Assert.Equal(5, shipped.SchemaVersion);
        Assert.All(shipped.Definition.Trees.SelectMany(t => t.Talents), t => Assert.Empty(t.Modifiers));
        Assert.All(shipped.Definition.Skills, s => Assert.Empty(s.Modifiers));
    }

    [Fact]
    public void Modificateurs_de_talent_lus_cle_valeur_par_rang_et_conditions()
    {
        // Exemple de la conception (D1) : valeur par rang { base, perRank } et nombre simple avec deux conditions.
        LoadedClass loaded = Valid(OnTalent(0, 0,
            Mod("guard.damageReduction", """{ "base": 0.04, "perRank": 0.04 }"""),
            Mod("damageTaken", "-0.005", "whileGuarding", "perRampartCharge")));
        IReadOnlyList<ModifierDefinition> modifiers = loaded.Definition.Trees[0].Talents[0].Modifiers;
        Assert.Equal(2, modifiers.Count);

        Assert.Equal("guard.damageReduction", modifiers[0].Key);
        Assert.Equal(new RankedValue(Fixed.FromRaw(400), Fixed.FromRaw(400)), modifiers[0].Add);
        Assert.Equal(Fixed.FromRaw(1200), modifiers[0].Add.At(3));   // 0,04 + 2 × 0,04 = 0,12 au rang 3
        Assert.Empty(modifiers[0].Conditions);
        Assert.Equal(ModifierConditions.None, modifiers[0].ConditionSet);

        Assert.Equal("damageTaken", modifiers[1].Key);
        Assert.Equal(new RankedValue(Fixed.FromRaw(-50), Fixed.Zero), modifiers[1].Add);
        Assert.Equal(new[] { ModifierCondition.WhileGuarding, ModifierCondition.PerRampartCharge }, modifiers[1].Conditions);
        Assert.Equal(ModifierConditions.WhileGuarding | ModifierConditions.PerRampartCharge, modifiers[1].ConditionSet);

        // Aucun autre porteur n'en reçoit.
        Assert.Equal(2, loaded.Definition.Trees.SelectMany(t => t.Talents).Sum(t => t.Modifiers.Count) + loaded.Definition.Skills.Sum(s => s.Modifiers.Count));
    }

    [Fact]
    public void Modificateurs_de_passif_lus()
    {
        LoadedClass loaded = Valid(OnSkill(7,
            Mod("endurance.max", "20"),
            Mod("damageDealt", """{ "base": 0.05 }""", "perResonanceStep"),
            Mod("guard.noUpkeepBreak", "1"),
            Mod("skill.bastion.frappe_du_rempart.cooldownMs", """{ "base": -1000, "perRank": 0 }"""),
            Mod("status.burn.durationDealt", "2000")));
        IReadOnlyList<ModifierDefinition> modifiers = loaded.Definition.Skills[7].Modifiers;
        Assert.Equal(new[] { "endurance.max", "damageDealt", "guard.noUpkeepBreak", "skill.bastion.frappe_du_rempart.cooldownMs", "status.burn.durationDealt" },
            modifiers.Select(m => m.Key));
        Assert.Equal(new[] { Fixed.FromInt(20), Fixed.FromRaw(500), Fixed.FromInt(1), Fixed.FromInt(-1000), Fixed.FromInt(2000) },
            modifiers.Select(m => m.Add.Base));
        Assert.All(modifiers, m => Assert.Equal(Fixed.Zero, m.Add.PerRank));   // perRank 0 explicite sur un passif : admis (aucune valeur par rang)
        Assert.Equal(new[] { ModifierCondition.PerResonanceStep }, modifiers[1].Conditions);
        Assert.All(modifiers.Where((_, i) => i != 1), m => Assert.Empty(m.Conditions));
    }

    // --- Erreurs de forme ------------------------------------------------------------------

    /// <summary>Modificateur mal formé, chemin relatif de l'erreur unique attendue et son code.</summary>
    private static readonly (string Json, string Path, ValidationCode Code)[] ShapeCases =
    {
        ("""{ "add": 0.1 }""", "[0].key", ValidationCode.MissingField),
        ("""{ "key": "damageDealt" }""", "[0].add", ValidationCode.MissingField),
        ("""{ "key": "damageDealt", "add": { "perRank": 0.01 } }""", "[0].add.base", ValidationCode.MissingField),
        ("""{ "key": "damageDealt", "add": { } }""", "[0].add.base", ValidationCode.MissingField),
        ("""{ "key": 5, "add": 0.1 }""", "[0].key", ValidationCode.WrongType),
        ("""{ "key": null, "add": 0.1 }""", "[0].key", ValidationCode.WrongType),
        ("""{ "key": ["damageDealt"], "add": 0.1 }""", "[0].key", ValidationCode.WrongType),
        ("""{ "key": "damageDealt", "add": "0.1" }""", "[0].add", ValidationCode.WrongType),
        ("""{ "key": "damageDealt", "add": null }""", "[0].add", ValidationCode.WrongType),
        ("""{ "key": "damageDealt", "add": [0.1] }""", "[0].add", ValidationCode.WrongType),
        ("""{ "key": "damageDealt", "add": true }""", "[0].add", ValidationCode.WrongType),
        ("""{ "key": "damageDealt", "add": { "base": "0.1" } }""", "[0].add.base", ValidationCode.WrongType),
        ("""{ "key": "damageDealt", "add": { "base": 0.1, "perRank": true } }""", "[0].add.perRank", ValidationCode.WrongType),
        ("""{ "key": "damageDealt", "add": 0.00001 }""", "[0].add", ValidationCode.InvalidNumber),
        ("""{ "key": "damageDealt", "add": 1e-2 }""", "[0].add", ValidationCode.InvalidNumber),
        ("""{ "key": "damageDealt", "add": { "base": 0.1, "perRank": 0.00001 } }""", "[0].add.perRank", ValidationCode.InvalidNumber),
        ("""{ "key": "damageDealt", "add": 0.1, "conditions": "whileGuarding" }""", "[0].conditions", ValidationCode.WrongType),
        ("""{ "key": "damageDealt", "add": 0.1, "conditions": { "whileGuarding": true } }""", "[0].conditions", ValidationCode.WrongType),
        ("""{ "key": "damageDealt", "add": 0.1, "conditions": [1] }""", "[0].conditions[0]", ValidationCode.WrongType),
        ("""{ "key": "damageDealt", "add": 0.1, "conditions": [null] }""", "[0].conditions[0]", ValidationCode.WrongType),
        ("""{ "key": "damageDealt", "add": 0.1, "conditions": ["whileGuarding", "whileBlocking"] }""", "[0].conditions[1]", ValidationCode.UnknownReference),
        ("""{ "key": "damageDealt", "add": 0.1, "conditions": ["WhileGuarding"] }""", "[0].conditions[0]", ValidationCode.UnknownReference),
        ("""{ "key": "damageDealt", "add": 0.1, "conditions": [""] }""", "[0].conditions[0]", ValidationCode.UnknownReference),
        ("""{ "key": "damageDealt", "add": 0.1, "chance": 0.5 }""", "[0].chance", ValidationCode.UnknownField),
        ("""{ "key": "damageDealt", "add": 0.1, "Key": "damageTaken" }""", "[0].Key", ValidationCode.UnknownField),
        ("""{ "key": "damageDealt", "add": { "base": 0.1, "max": 1 } }""", "[0].add.max", ValidationCode.UnknownField),
        ("null", "[0]", ValidationCode.WrongType),
        ("\"damageDealt\"", "[0]", ValidationCode.WrongType),
        ("[]", "[0]", ValidationCode.WrongType),
    };

    public static TheoryData<string, string, string, ValidationCode> ShapeErrors()
    {
        var data = new TheoryData<string, string, string, ValidationCode>();
        foreach (string owner in new[] { "talent", "passif" })
            foreach (var (json, path, code) in ShapeCases) data.Add(owner, json, path, code);
        return data;
    }

    [Theory]
    [MemberData(nameof(ShapeErrors))]
    public void Erreur_de_forme_localisee_par_son_chemin_json(string owner, string modifier, string relativePath, ValidationCode code) =>
        AssertSingleError(On(owner, JsonNode.Parse(modifier)), PathOf(owner) + relativePath, code);

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("\"guard.damageReduction\"")]
    [InlineData("0")]
    public void Liste_de_modificateurs_qui_n_est_pas_un_tableau(string value)
    {
        AssertSingleError(ClassTestData.ReadMutated(root => ClassTestData.Talent(root, 0, 0)["modifiers"] = JsonNode.Parse(value)), TalentPath, ValidationCode.WrongType);
        AssertSingleError(ClassTestData.ReadMutated(root => ClassTestData.Skill(root, 7)["modifiers"] = JsonNode.Parse(value)), PassivePath, ValidationCode.WrongType);
    }

    [Fact]
    public void Champ_en_double_dans_un_modificateur()
    {
        JsonObject root = ClassTestData.Shipped();
        ClassTestData.Talent(root, 0, 0)["modifiers"] = List(JsonNode.Parse("""{ "key": "damageDealt", "add": 0.1, "doublon": 0.2 }"""));
        string json = ClassTestData.ToJson(root).Replace("\"doublon\"", "\"add\"", StringComparison.Ordinal);
        AssertSingleError(ClassJsonReader.Read(json, ClassTestData.Progression), TalentPath + "[0].add", ValidationCode.DuplicateField);
    }

    [Fact]
    public void Toutes_les_erreurs_de_forme_en_une_passe()
    {
        ClassLoadResult result = ClassTestData.ReadMutated(root =>
        {
            ClassTestData.Talent(root, 0, 0)["modifiers"] = List(JsonNode.Parse("""{ "add": 0.1 }"""), Mod("damageDealt", "0.1", "whileBlocking"));
            ClassTestData.Skill(root, 7)["modifiers"] = List(JsonNode.Parse("""{ "key": "endurance.max" }"""));
        });
        Assert.Null(result.Class);
        Assert.Equal(new[]
        {
            (TalentPath + "[0].key", ValidationCode.MissingField),
            (TalentPath + "[1].conditions[0]", ValidationCode.UnknownReference),
            (PassivePath + "[0].add", ValidationCode.MissingField),
        }, result.Report.Errors.Select(e => (e.Path, e.Code)));
    }

    // --- Erreurs de domaine rattachées au fichier ------------------------------------------

    /// <summary>Erreurs de ClassDefinition.Validate valables sur les deux porteurs (valeurs sans perRank).</summary>
    private static readonly (string Json, string Path, ValidationCode Code)[] DomainCases =
    {
        // Clé hors de la liste fermée (D2), dont les clés reportées à 8.2 et 8.3.
        ("""{ "key": "guard.reflect", "add": 0.1 }""", "[0].key", ValidationCode.UnknownReference),
        ("""{ "key": "shield.potencyDealt", "add": 0.1 }""", "[0].key", ValidationCode.UnknownReference),
        ("""{ "key": "protection.redirectRatio", "add": 0.1 }""", "[0].key", ValidationCode.UnknownReference),
        ("""{ "key": "Guard.damageReduction", "add": 0.1 }""", "[0].key", ValidationCode.UnknownReference),
        ("""{ "key": "", "add": 0.1 }""", "[0].key", ValidationCode.UnknownReference),
        ("""{ "key": "status.Burn.durationTaken", "add": 2000 }""", "[0].key", ValidationCode.UnknownReference),
        ("""{ "key": "status.burn.duration", "add": 2000 }""", "[0].key", ValidationCode.UnknownReference),
        // Référence de compétence : inconnue, sans bloc combat (active ou passive), ou identifiant d'un autre domaine.
        ("""{ "key": "skill.bastion.inconnue.coefficient", "add": 0.1 }""", "[0].key", ValidationCode.UnknownReference),
        ("""{ "key": "skill.bastion.provocation.cooldownMs", "add": -1000 }""", "[0].key", ValidationCode.UnknownReference),
        ("""{ "key": "skill.bastion.test_passif_1.coefficient", "add": 0.1 }""", "[0].key", ValidationCode.UnknownReference),
        ("""{ "key": "talent.bastion.rempart.garde_stable.coefficient", "add": 0.1 }""", "[0].key", ValidationCode.UnknownReference),
        // Valeurs entières et drapeau.
        ("""{ "key": "guard.costPerBlock", "add": 1.5 }""", "[0].add", ValidationCode.InvalidValue),
        ("""{ "key": "rampartCharges.max", "add": 0.5 }""", "[0].add", ValidationCode.InvalidValue),
        ("""{ "key": "parry.failureRecoveryMs", "add": 100.5 }""", "[0].add", ValidationCode.InvalidValue),
        ("""{ "key": "skill.bastion.frappe_du_rempart.cooldownMs", "add": -0.5 }""", "[0].add", ValidationCode.InvalidValue),
        ("""{ "key": "guard.noUpkeepBreak", "add": 2 }""", "[0].add", ValidationCode.InvalidValue),
        ("""{ "key": "guard.noUpkeepBreak", "add": 0 }""", "[0].add", ValidationCode.InvalidValue),
        ("""{ "key": "guard.noUpkeepBreak", "add": -1 }""", "[0].add", ValidationCode.InvalidValue),
        ("""{ "key": "guard.noUpkeepBreak", "add": 0.5 }""", "[0].add", ValidationCode.InvalidValue),
        // damageTaken : réduction seulement (D10).
        ("""{ "key": "damageTaken", "add": 0.0001 }""", "[0].add", ValidationCode.InvalidValue),
        // Conditions : seules damageDealt et damageTaken ; au plus une multiplicative ; sans doublon.
        ("""{ "key": "guard.damageReduction", "add": 0.1, "conditions": ["whileGuarding"] }""", "[0].conditions", ValidationCode.InvalidValue),
        ("""{ "key": "skill.bastion.frappe_du_rempart.coefficient", "add": 0.1, "conditions": ["perRampartCharge"] }""", "[0].conditions", ValidationCode.InvalidValue),
        ("""{ "key": "damageDealt", "add": 0.1, "conditions": ["perRampartCharge", "perResonanceStep"] }""", "[0].conditions", ValidationCode.InvalidValue),
        ("""{ "key": "damageTaken", "add": -0.1, "conditions": ["whileGuarding", "perResonanceStep", "perRampartCharge"] }""", "[0].conditions", ValidationCode.InvalidValue),
        ("""{ "key": "damageDealt", "add": 0.1, "conditions": ["whileGuarding", "whileGuarding"] }""", "[0].conditions[1]", ValidationCode.DuplicateId),
    };

    public static TheoryData<string, string, string, ValidationCode> DomainErrors()
    {
        var data = new TheoryData<string, string, string, ValidationCode>();
        foreach (string owner in new[] { "talent", "passif" })
            foreach (var (json, path, code) in DomainCases) data.Add(owner, json, path, code);
        return data;
    }

    [Theory]
    [MemberData(nameof(DomainErrors))]
    public void Erreur_de_domaine_rattachee_au_chemin_json(string owner, string modifier, string relativePath, ValidationCode code) =>
        AssertSingleError(On(owner, JsonNode.Parse(modifier)), PathOf(owner) + relativePath, code);

    [Theory]
    // damageTaken positive au rang maximal seulement (D10) : −0,01 + 2 × 0,01 = +0,01 au rang 3.
    [InlineData("""{ "key": "damageTaken", "add": { "base": -0.01, "perRank": 0.01 } }""", ValidationCode.InvalidValue)]
    // Drapeau : 1 exactement, sans perRank.
    [InlineData("""{ "key": "guard.noUpkeepBreak", "add": { "base": 1, "perRank": 1 } }""", ValidationCode.InvalidValue)]
    // Entier : perRank entier aussi.
    [InlineData("""{ "key": "guard.costPerBlock", "add": { "base": 1, "perRank": 0.5 } }""", ValidationCode.InvalidValue)]
    [InlineData("""{ "key": "parry.windowMs", "add": { "base": 50, "perRank": 0.5 } }""", ValidationCode.InvalidValue)]
    public void Erreur_de_valeur_par_rang_sur_un_talent(string modifier, ValidationCode code) =>
        AssertSingleError(OnTalent(0, 0, JsonNode.Parse(modifier)), TalentPath + "[0].add", code);

    [Theory]
    // Liste « débordements refusés avant simulation » : valeur au rang maximal (3) non représentable (D2 : débordement → NumericLimit,
    // rapporté OutOfRange). 9·10¹⁴ × 2 et 922 337 203 685 477 + 2 × 0,5 dépassent la plage de Fixed (≈ 9,22·10¹⁴).
    [InlineData("""{ "base": 0, "perRank": 900000000000000 }""")]
    [InlineData("""{ "base": 922337203685477, "perRank": 0.5 }""")]
    [InlineData("""{ "base": -922337203685477, "perRank": -0.5 }""")]
    public void Debordement_d_une_valeur_par_rang_refuse_au_chargement(string add) =>
        AssertSingleError(OnTalent(0, 0, Mod("damageDealt", add)), TalentPath + "[0].add", ValidationCode.OutOfRange);

    [Fact]
    public void Valeur_representable_mais_hors_bornes_refusee_comme_valeur_invalide()
    {
        // Contraste avec le débordement : même base, sans perRank, représentable aux deux rangs mais hors de [−10, 10].
        AssertSingleError(OnTalent(0, 0, Mod("damageDealt", """{ "base": 922337203685477, "perRank": 0 }""")), TalentPath + "[0].add", ValidationCode.InvalidValue);
    }

    [Fact]
    public void Valeur_par_rang_refusee_sur_un_passif_zero_admis()
    {
        AssertSingleError(OnSkill(7, Mod("damageDealt", """{ "base": 0.1, "perRank": 0.1 }""")), PassivePath + "[0].add", ValidationCode.InvalidValue);
        AssertSingleError(OnSkill(7, Mod("damageTaken", """{ "base": -0.1, "perRank": -0.0001 }""")), PassivePath + "[0].add", ValidationCode.InvalidValue);
        Valid(OnSkill(7, Mod("damageDealt", """{ "base": 0.1, "perRank": 0 }""")));
    }

    [Theory]
    [InlineData(0)]   // active
    [InlineData(1)]   // active
    [InlineData(6)]   // ultime
    public void Modificateurs_refuses_sur_une_competence_active_ou_ultime(int skill)
    {
        AssertSingleError(OnSkill(skill, Mod("damageDealt", "0.1")), $"$.data.skills[{skill}].modifiers", ValidationCode.InvalidValue);
        // Le contenu n'est pas examiné : une clé inconnue ne s'ajoute pas au refus du porteur.
        AssertSingleError(OnSkill(skill, Mod("cle.inconnue", "0.1"), Mod("damageDealt", "0.1")), $"$.data.skills[{skill}].modifiers", ValidationCode.InvalidValue);
        // Liste vide : rien à refuser.
        Valid(OnSkill(skill));
    }

    // --- Doublons et ensembles de conditions -----------------------------------------------

    [Fact]
    public void Doublon_meme_cle_et_memes_conditions_chez_un_porteur()
    {
        AssertSingleError(OnTalent(0, 0, Mod("damageDealt", "0.1"), Mod("damageDealt", "0.2")), TalentPath + "[1].key", ValidationCode.DuplicateId);
        // Mêmes conditions dans un autre ordre : même ensemble, doublon.
        AssertSingleError(OnSkill(7, Mod("damageTaken", "-0.01", "whileGuarding", "perRampartCharge"), Mod("damageTaken", "-0.02", "perRampartCharge", "whileGuarding")),
            PassivePath + "[1].key", ValidationCode.DuplicateId);
        AssertSingleError(OnTalent(0, 0, Mod("skill.bastion.frappe_du_rempart.coefficient", "0.1"), Mod("damageDealt", "0.1"), Mod("skill.bastion.frappe_du_rempart.coefficient", "0.2")),
            TalentPath + "[2].key", ValidationCode.DuplicateId);
    }

    [Fact]
    public void Meme_cle_avec_conditions_differentes_ou_sur_des_porteurs_differents_admise()
    {
        // D3 : permanent et les cinq ensembles admis {W}, {C}, {R}, {W,C}, {W,R} sur un même porteur.
        Valid(OnTalent(0, 0,
            Mod("damageTaken", "-0.01"),
            Mod("damageTaken", "-0.01", "whileGuarding"),
            Mod("damageTaken", "-0.01", "perRampartCharge"),
            Mod("damageTaken", "-0.01", "perResonanceStep"),
            Mod("damageTaken", "-0.01", "whileGuarding", "perRampartCharge"),
            Mod("damageTaken", "-0.01", "perResonanceStep", "whileGuarding")));
        // Clés paramétrées distinctes : pas de doublon.
        Valid(OnTalent(0, 0, Mod("status.burn.durationTaken", "2000"), Mod("status.bleed.durationTaken", "2000"), Mod("status.burn.durationDealt", "2000")));
        // Même clé sur deux sources : elles s'additionneront au combat.
        Valid(ClassTestData.ReadMutated(root =>
        {
            ClassTestData.Talent(root, 0, 0)["modifiers"] = List(Mod("guard.damageReduction", "0.05"));
            ClassTestData.Talent(root, 1, 0)["modifiers"] = List(Mod("guard.damageReduction", "0.05"));
            ClassTestData.Skill(root, 7)["modifiers"] = List(Mod("guard.damageReduction", "0.05"));
        }));
    }

    // --- Bornes déclarées de chaque clé (liste : chaque clé aux rangs 1 et maximal, N−1, N, N+1) ----------------------------

    /// <summary>
    /// Bornes déclarées |add| ≤ N de la conception (tableau D2, resserrées par D10), écrites ici indépendamment de ModifierKeys.Specs, et
    /// nature entière (pas de 1) ou décimale (pas de 0,0001). Clés paramétrées : références valides de la classe livrée. damageTaken :
    /// intervalle [−1, 0] (D10). Le drapeau guard.noUpkeepBreak a son propre test.
    /// </summary>
    private static readonly (string Key, decimal Bound, bool Integer)[] DeclaredBounds =
    {
        ("guard.damageReduction", 0.75m, false),
        ("guard.upkeepPerSecond", 1_000_000m, false),
        ("guard.costPerBlock", 1_000_000m, true),
        ("guard.reductionAtZeroEndurance", 0.75m, false),
        ("guard.breakShortenPerCharge", 86_400_000m, true),
        ("parry.windowMs", 500m, true),
        ("parry.failureRecoveryMs", 86_400_000m, true),
        ("rampartCharges.max", 10m, true),
        ("resonance.perDirectHit", 1_000_000m, true),
        ("endurance.max", 1_000_000m, true),
        ("endurance.regenPerSecond", 1_000_000m, false),
        ("maxHealth", 10m, false),
        ("statusResistance", 1_000_000m, false),
        ("damageDealt", 10m, false),
        ("damageTaken", 1m, false),
        ("status.burn.durationDealt", 86_400_000m, true),
        ("status.fragility.durationTaken", 86_400_000m, true),
        ("skill.bastion.frappe_du_rempart.coefficient", 100m, false),
        ("skill.bastion.heurt_de_bouclier.cooldownMs", 86_400_000m, true),
    };

    public static TheoryData<string, string, string, bool> Boundaries()
    {
        var data = new TheoryData<string, string, string, bool>();
        foreach (var (key, bound, integer) in DeclaredBounds)
        {
            decimal step = integer ? 1m : 0.0001m;
            // (limite, sens vers l'extérieur) : ±N, ou −1 et 0 pour damageTaken.
            var limits = key == "damageTaken" ? new[] { (-bound, -1m), (0m, 1m) } : new[] { (bound, 1m), (-bound, -1m) };
            foreach (var (limit, outward) in limits)
                foreach (int k in new[] { -1, 0, 1 })   // N−1, N, N+1 (k pas au-delà de la limite)
                {
                    decimal target = limit + outward * k * step;
                    bool valid = k <= 0;
                    // Rang 1 : nombre simple, sur un talent et sur un passif.
                    data.Add("talent, rang 1", key, Num(target), valid);
                    data.Add("passif", key, Num(target), valid);
                    // Rang maximal (garde_stable, 3 rangs) : base + 2 × perRank = cible, perRank d'un pas vers l'extérieur ; au rang 1 la
                    // base reste dans les bornes, seul le rang maximal décide.
                    decimal perRank = outward * step;
                    data.Add("talent, rang maximal", key, $$"""{ "base": {{Num(target - 2 * perRank)}}, "perRank": {{Num(perRank)}} }""", valid);
                }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Boundaries))]
    public void Borne_de_chaque_cle_au_rang_1_et_au_rang_maximal(string owner, string key, string add, bool valid)
    {
        ClassLoadResult result = owner == "passif" ? OnSkill(7, Mod(key, add)) : OnTalent(0, 0, Mod(key, add));
        if (valid) Valid(result);
        else AssertSingleError(result, PathOf(owner) + "[0].add", ValidationCode.InvalidValue);
    }

    [Fact]
    public void Liste_des_bornes_couvre_chaque_cle_sauf_le_drapeau()
    {
        // Garde-fou de la théorie ci-dessus : une clé par spécification de la liste fermée (D2), le drapeau à part.
        var covered = DeclaredBounds.Select(b => ModifierKeys.TryParse(b.Key, out ModifierKey k) ? k.Field : (ModifierField)(-1)).ToHashSet();
        Assert.Equal(Enum.GetValues<ModifierField>().Where(f => f != ModifierField.GuardNoUpkeepBreak).ToHashSet(), covered);
    }

    [Fact]
    public void Drapeau_un_exactement()
    {
        Valid(OnTalent(0, 0, Mod("guard.noUpkeepBreak", "1")));
        Valid(OnTalent(0, 0, Mod("guard.noUpkeepBreak", """{ "base": 1, "perRank": 0 }""")));
        Valid(OnSkill(7, Mod("guard.noUpkeepBreak", "1.0")));
        AssertSingleError(OnSkill(7, Mod("guard.noUpkeepBreak", "1.0001")), PassivePath + "[0].add", ValidationCode.InvalidValue);
        AssertSingleError(OnSkill(7, Mod("guard.noUpkeepBreak", "0.9999")), PassivePath + "[0].add", ValidationCode.InvalidValue);
    }

    // --- Plafonds (N−1, N, N+1) ------------------------------------------------------------

    /// <summary>Neuf modificateurs valides de clés distinctes (sans perRank, donc valables sur un passif).</summary>
    private static JsonNode[] Distinct(int count) => new[]
    {
        Mod("guard.damageReduction", "0.01"), Mod("guard.upkeepPerSecond", "1"), Mod("guard.costPerBlock", "1"), Mod("guard.noUpkeepBreak", "1"),
        Mod("guard.reductionAtZeroEndurance", "0.1"), Mod("rampartCharges.max", "1"), Mod("resonance.perDirectHit", "1"), Mod("endurance.max", "1"),
        Mod("statusResistance", "1"),
    }.Take(count).ToArray();

    [Theory]
    [InlineData("talent")]
    [InlineData("passif")]
    public void Nombre_de_modificateurs_par_porteur_7_8_admis_9_refuse(string owner)
    {
        Valid(On(owner, Distinct(7)));
        Valid(On(owner, Distinct(8)));
        AssertSingleError(On(owner, Distinct(9)), PathOf(owner), ValidationCode.LimitExceeded);
    }

    /// <summary>Classe livrée plus <paramref name="passives"/> compétences passives de 8 modificateurs et <paramref name="onTalent"/> sur garde_stable.</summary>
    private static ClassLoadResult WithVolume(int passives, int onTalent) => ClassTestData.ReadMutated(root =>
    {
        JsonArray skills = ClassTestData.DataOf(root)["skills"]!.AsArray();
        for (int n = 0; n < passives; n++)
            skills.Add(new JsonObject
            {
                ["id"] = $"skill.bastion.volume_{n}", ["name"] = $"Volume {n}", ["kind"] = "passive", ["minLevel"] = 1, ["placeholder"] = true,
                ["talentRequirements"] = new JsonArray(), ["modifiers"] = List(Distinct(8)),
            });
        if (onTalent > 0) ClassTestData.Talent(root, 0, 0)["modifiers"] = List(Distinct(onTalent));
    });

    [Fact]
    public void Plafond_de_512_modificateurs_par_classe_511_et_512_admis()
    {
        Valid(WithVolume(63, 7));    // 63 × 8 + 7 = 511
        Valid(WithVolume(64, 0));    // 64 × 8 = 512
    }

    [Fact]
    public void Plafond_de_512_modificateurs_par_classe_513_refuse_seul()
    {
        // 64 × 8 + 1 = 513 : plafond global signalé seul, rien d'autre n'est examiné (D2).
        ClassLoadResult result = WithVolume(64, 1);
        AssertSingleError(result, "$.data.skills", ValidationCode.LimitExceeded);
        Assert.Contains("513 modificateurs", result.Report.Errors[0].Message);
    }

    // --- Références connues seulement au combat (catalogue, tick) --------------------------

    private static ScenarioLoadResult ScenarioWith(params JsonNode[] modifiers)
    {
        LoadedClass loaded = Valid(OnTalent(0, 0, modifiers));
        var data = new GameData(ScenarioTestData.ShippedData.Rules, new[] { loaded });
        return ScenarioJsonReader.Read(File.ReadAllText(ScenarioTestData.ScenarioPath("entrainement_garde_parade")), data);
    }

    [Theory]
    // status.taunt.* : syntaxe admise, refusée tant que « taunt » n'est pas au catalogue (D2, X7 en 8.3).
    [InlineData("status.taunt.durationDealt", "1000", ValidationCode.UnknownReference, ".key")]
    // durationDealt exige une altération appliquée directement : ni immunité, ni rupture de garde.
    [InlineData("status.guardBreak.durationDealt", "1000", ValidationCode.InvalidValue, ".key")]
    [InlineData("status.stunImmunity.durationDealt", "1000", ValidationCode.InvalidValue, ".key")]
    // Durées : multiples du tick (50 ms), et de la période (2 000 ms) pour un DOT.
    [InlineData("parry.windowMs", "25", ValidationCode.InvalidValue, ".add")]
    [InlineData("guard.breakShortenPerCharge", "{ \"base\": 100, \"perRank\": 10 }", ValidationCode.InvalidValue, ".add")]
    [InlineData("status.burn.durationTaken", "1000", ValidationCode.InvalidValue, ".add")]
    [InlineData("status.bleed.durationDealt", "{ \"base\": 2000, \"perRank\": 1000 }", ValidationCode.InvalidValue, ".add")]
    public void Reference_au_catalogue_ou_au_tick_refusee_au_scenario(string key, string add, ValidationCode code, string field)
    {
        ScenarioLoadResult result = ScenarioWith(Mod(key, add));
        Assert.Null(result.Setup);
        ValidationError error = Assert.Single(result.Report.Errors);
        Assert.Equal(("$.data.combatants[0].class", code), (error.Path, error.Code));
        Assert.Contains("trees[0].talents[0].modifiers[0]" + field, error.Message);
    }

    [Theory]
    [InlineData("status.guardBreak.durationTaken", "1000")]   // durationTaken : toute application sur le porteur, rupture comprise
    [InlineData("status.stunImmunity.durationTaken", "-500")]
    [InlineData("status.fragility.durationDealt", "{ \"base\": 1000, \"perRank\": 50 }")]
    [InlineData("status.burn.durationTaken", "{ \"base\": -2000, \"perRank\": 2000 }")]
    [InlineData("parry.windowMs", "50")]
    public void Reference_au_catalogue_et_au_tick_admise_au_scenario(string key, string add)
    {
        ScenarioLoadResult result = ScenarioWith(Mod(key, add));
        Assert.True(result.IsValid, result.Report.ToString());
    }
}

/// <summary>
/// Sous-lot 8.1 : forme canonique des modificateurs et empreinte de classe. Texte canonique exact d'une petite classe (empreinte
/// recalculée ici par SHA-256), équivalences de forme (conditions vides ou absentes, nombre ou { base }, perRank nul, ordre des
/// champs, mise en forme, liste vide) et différences de contenu (clé, valeur, conditions, porteur, ordre de déclaration).
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Data")]
public class Lot81ModifierDataFingerprintTests
{
    private const string SmallClass = """
        { "schemaVersion": 5, "kind": "class", "contentVersion": "0.1.0-test",
          "data": { "id": "class.essai", "name": "Essai",
            "trees": [ { "id": "tree.essai.a", "name": "A", "talents": [
              { "id": "talent.essai.a.t", "name": "T", "maxRank": 3, "major": false, "placeholder": false, "prerequisites": [],
                "modifiers": [ { "key": "guard.damageReduction", "add": { "base": 0.04, "perRank": 0.04 } } ],
                "triggers": [ { "on": "onBlock", "role": "target", "effects": [ { "type": "gainResource", "resource": "rampartCharges", "amount": 1 } ] } ] } ] } ],
            "skills": [
              { "id": "skill.essai.p", "name": "P", "kind": "passive", "minLevel": 1, "placeholder": false, "talentRequirements": [],
                "triggers": [ { "on": "onBlock", "role": "target", "effects": [ { "type": "gainResource", "resource": "rampartCharges", "amount": 1 } ] } ],
                "modifiers": [ { "key": "damageTaken", "add": -0.005, "conditions": ["whileGuarding", "perRampartCharge"] },
                               { "key": "skill.essai.f.cooldownMs", "add": -500 } ] },
              { "id": "skill.essai.f", "name": "F", "kind": "active", "minLevel": 1, "placeholder": false, "talentRequirements": [],
                "combat": { "powerType": "physique", "damageType": "physique", "coefficient": 1.2, "enduranceCost": 10, "cooldownMs": 6000,
                            "targeting": "enemy", "unblockable": false, "rampartChargeCost": 0, "rampartChargeGain": 0, "provisional": true, "applies": [] } } ] } }
        """;

    /// <summary>
    /// Déclencheur minimal (lot 7.3, contrat canonique 2) porté par le talent et par la passive, pour situer les modificateurs après lui
    /// quel que soit l'ordre des champs dans le fichier (le talent déclare ses modificateurs avant ses déclencheurs).
    /// </summary>
    private static string TriggerLines(string p) =>
        $"{p}.triggers.count=1\n{p}.triggers[0].on=onBlock\n{p}.triggers[0].role=target\n{p}.triggers[0].filter.skills.count=0\n" +
        $"{p}.triggers[0].filter.damageType?\n{p}.triggers[0].filter.defense.count=0\n{p}.triggers[0].filter.status?\n" +
        $"{p}.triggers[0].filter.category?\n{p}.triggers[0].filter.resource?\n{p}.triggers[0].filter.renewed?\n" +
        $"{p}.triggers[0].cooldownMs=0\n{p}.triggers[0].effects.count=1\n{p}.triggers[0].effects[0].type=gainResource\n" +
        $"{p}.triggers[0].effects[0].target?\n{p}.triggers[0].effects[0].coefficient?\n{p}.triggers[0].effects[0].powerType?\n" +
        $"{p}.triggers[0].effects[0].damageType?\n{p}.triggers[0].effects[0].unblockable=false\n{p}.triggers[0].effects[0].status?\n" +
        $"{p}.triggers[0].effects[0].resource=rampartCharges\n{p}.triggers[0].effects[0].amount=1\n";

    /// <summary>
    /// Texte canonique attendu, écrit à la main d'après D1 : après les prérequis et les déclencheurs d'un talent ; pour une compétence,
    /// après ses exigences et ses déclencheurs, avant le bloc combat (« combat? » d'une passive) ; rien pour une liste vide (active).
    /// </summary>
    private static readonly string SmallClassCanonical =
        "eds-class\ncanonical=2\nschemaVersion=5\nid=class.essai\nname=Essai\n" +
        "trees.count=1\ntrees[0].id=tree.essai.a\ntrees[0].name=A\ntrees[0].talents.count=1\n" +
        "trees[0].talents[0].id=talent.essai.a.t\ntrees[0].talents[0].name=T\ntrees[0].talents[0].maxRank=3\n" +
        "trees[0].talents[0].major=false\ntrees[0].talents[0].placeholder=false\ntrees[0].talents[0].prerequisites.count=0\n" +
        TriggerLines("trees[0].talents[0]") +
        "trees[0].talents[0].modifiers.count=1\n" +
        "trees[0].talents[0].modifiers[0].key=guard.damageReduction\n" +
        "trees[0].talents[0].modifiers[0].add=0.04+0.04/rank\n" +
        "trees[0].talents[0].modifiers[0].conditions.count=0\n" +
        "skills.count=2\n" +
        "skills[0].id=skill.essai.p\nskills[0].name=P\nskills[0].kind=passive\nskills[0].minLevel=1\nskills[0].placeholder=false\n" +
        "skills[0].talentRequirements.count=0\n" +
        TriggerLines("skills[0]") +
        "skills[0].modifiers.count=2\n" +
        "skills[0].modifiers[0].key=damageTaken\n" +
        "skills[0].modifiers[0].add=-0.005\n" +
        "skills[0].modifiers[0].conditions.count=2\n" +
        "skills[0].modifiers[0].conditions[0]=whileGuarding\n" +
        "skills[0].modifiers[0].conditions[1]=perRampartCharge\n" +
        "skills[0].modifiers[1].key=skill.essai.f.cooldownMs\n" +
        "skills[0].modifiers[1].add=-500\n" +
        "skills[0].modifiers[1].conditions.count=0\n" +
        "skills[0].combat?\n" +
        "skills[1].id=skill.essai.f\nskills[1].name=F\nskills[1].kind=active\nskills[1].minLevel=1\nskills[1].placeholder=false\n" +
        "skills[1].talentRequirements.count=0\n" +
        "skills[1].combat.powerType=physique\nskills[1].combat.damageType=physique\nskills[1].combat.coefficient=1.2\n" +
        "skills[1].combat.enduranceCost=10\nskills[1].combat.cooldownMs=6000\nskills[1].combat.targeting=enemy\n" +
        "skills[1].combat.unblockable=false\nskills[1].combat.rampartChargeCost=0\nskills[1].combat.rampartChargeGain=0\n" +
        "skills[1].combat.provisional=true\nskills[1].combat.applies.count=0\n";

    private static string Sha256(string text) =>
        ContentFingerprint.Prefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    [Fact]
    public void Forme_canonique_exacte_et_empreinte_recalculee()
    {
        ClassLoadResult result = ClassJsonReader.Read(SmallClass, new ProgressionRules { TreesPerClass = 1 });
        LoadedClass loaded = Valid(result);
        Assert.Equal(Sha256(SmallClassCanonical), loaded.Fingerprint);

        // La partie « modificateurs » seule, par l'écrivain partagé, à partir des définitions chargées.
        var text = new StringBuilder();
        Modifiers.ModifierShape.AppendCanonical(text, "m", loaded.Definition.Skills[0].Modifiers);
        Assert.Equal("m.count=2\nm[0].key=damageTaken\nm[0].add=-0.005\nm[0].conditions.count=2\nm[0].conditions[0]=whileGuarding\n" +
            "m[0].conditions[1]=perRampartCharge\nm[1].key=skill.essai.f.cooldownMs\nm[1].add=-500\nm[1].conditions.count=0\n", text.ToString());
        var empty = new StringBuilder();
        Modifiers.ModifierShape.AppendCanonical(empty, "m", loaded.Definition.Skills[1].Modifiers);
        Assert.Equal(string.Empty, empty.ToString());
    }

    [Fact]
    public void Ajouter_ou_changer_un_modificateur_change_l_empreinte()
    {
        string[] fingerprints =
        {
            ShippedFingerprint,
            Fingerprint(r => ClassTestData.Talent(r, 0, 0)["modifiers"] = List(Mod("damageDealt", "0.1"))),
            Fingerprint(r => ClassTestData.Talent(r, 0, 0)["modifiers"] = List(Mod("damageDealt", "0.2"))),                          // valeur
            Fingerprint(r => ClassTestData.Talent(r, 0, 0)["modifiers"] = List(Mod("damageDealt", """{ "base": 0.1, "perRank": 0.1 }"""))),   // perRank
            Fingerprint(r => ClassTestData.Talent(r, 0, 0)["modifiers"] = List(Mod("damageTaken", "-0.1"))),                         // clé
            Fingerprint(r => ClassTestData.Talent(r, 0, 0)["modifiers"] = List(Mod("damageDealt", "0.1", "whileGuarding"))),          // condition
            Fingerprint(r => ClassTestData.Talent(r, 0, 0)["modifiers"] = List(Mod("damageDealt", "0.1", "whileGuarding", "perRampartCharge"))),
            Fingerprint(r => ClassTestData.Talent(r, 0, 0)["modifiers"] = List(Mod("damageDealt", "0.1"), Mod("damageTaken", "-0.1"))),   // nombre
            Fingerprint(r => ClassTestData.Talent(r, 0, 1)["modifiers"] = List(Mod("damageDealt", "0.1"))),                          // autre talent
            Fingerprint(r => ClassTestData.Skill(r, 7)["modifiers"] = List(Mod("damageDealt", "0.1"))),                              // passif
            Fingerprint(r => ClassTestData.Skill(r, 8)["modifiers"] = List(Mod("damageDealt", "0.1"))),                              // autre passif
        };
        Assert.Equal(fingerprints.Length, fingerprints.Distinct().Count());
    }

    [Theory]
    [InlineData("talent")]
    [InlineData("passif")]
    [InlineData("active")]
    [InlineData("ultime")]
    public void Liste_vide_equivaut_a_liste_absente(string owner)
    {
        string fingerprint = Fingerprint(r =>
        {
            JsonObject node = owner switch
            {
                "talent" => ClassTestData.Talent(r, 0, 0),
                "passif" => ClassTestData.Skill(r, 7),
                "active" => ClassTestData.Skill(r, 0),
                _ => ClassTestData.Skill(r, 6),
            };
            node["modifiers"] = new JsonArray();
        });
        Assert.Equal(ShippedFingerprint, fingerprint);
    }

    [Fact]
    public void Conditions_vides_et_absentes_meme_forme_et_meme_empreinte()
    {
        JsonNode Explicit() => JsonNode.Parse("""{ "key": "damageDealt", "add": 0.1, "conditions": [] }""")!;
        LoadedClass absent = Valid(OnTalent(0, 0, Mod("damageDealt", "0.1")));
        LoadedClass empty = Valid(OnTalent(0, 0, Explicit()));
        Assert.Equal(absent.Fingerprint, empty.Fingerprint);
        Assert.NotEqual(ShippedFingerprint, absent.Fingerprint);
        string Canonical(LoadedClass c)
        {
            var text = new StringBuilder();
            Modifiers.ModifierShape.AppendCanonical(text, "m", c.Definition.Trees[0].Talents[0].Modifiers);
            return text.ToString();
        }
        Assert.Equal("m.count=1\nm[0].key=damageDealt\nm[0].add=0.1\nm[0].conditions.count=0\n", Canonical(absent));
        Assert.Equal(Canonical(absent), Canonical(empty));
        // Idem sur un passif.
        Assert.Equal(Valid(OnSkill(7, Mod("damageDealt", "0.1"))).Fingerprint, Valid(OnSkill(7, Explicit())).Fingerprint);
        // Liste vide sur une clé qui n'admet pas de condition : permanent, admis, même empreinte que sans le champ.
        Assert.Equal(Valid(OnTalent(0, 0, Mod("guard.damageReduction", "0.05"))).Fingerprint,
            Valid(OnTalent(0, 0, JsonNode.Parse("""{ "key": "guard.damageReduction", "add": 0.05, "conditions": [] }""")!)).Fingerprint);
    }

    [Theory]
    [InlineData("""{ "base": 0.1 }""")]
    [InlineData("""{ "base": 0.1, "perRank": 0 }""")]   // perRank nul : même forme que sans perRank (« add=0.1 »)
    [InlineData("""{ "perRank": 0, "base": 0.1 }""")]
    [InlineData("""{ "base": 0.1000, "perRank": -0 }""")]
    [InlineData("0.10")]
    [InlineData("0.1000")]
    public void Valeur_simple_et_objet_base_meme_empreinte(string add)
    {
        string reference = Valid(OnTalent(0, 0, Mod("damageDealt", "0.1"))).Fingerprint;
        LoadedClass loaded = Valid(OnTalent(0, 0, Mod("damageDealt", add)));
        Assert.Equal(reference, loaded.Fingerprint);
        Assert.Equal(new RankedValue(Fixed.FromRaw(1000), Fixed.Zero), loaded.Definition.Trees[0].Talents[0].Modifiers[0].Add);
        // Sur un passif aussi (perRank nul admis).
        Assert.Equal(Valid(OnSkill(7, Mod("damageDealt", "0.1"))).Fingerprint, Valid(OnSkill(7, Mod("damageDealt", add))).Fingerprint);
    }

    [Fact]
    public void PerRank_non_nul_ecrit_dans_la_forme_canonique()
    {
        LoadedClass plain = Valid(OnTalent(0, 0, Mod("damageDealt", "0.1")));
        LoadedClass ranked = Valid(OnTalent(0, 0, Mod("damageDealt", """{ "base": 0.1, "perRank": 0.0001 }""")));
        Assert.NotEqual(plain.Fingerprint, ranked.Fingerprint);
        var text = new StringBuilder();
        Modifiers.ModifierShape.AppendCanonical(text, "m", ranked.Definition.Trees[0].Talents[0].Modifiers);
        Assert.Contains("m[0].add=0.1+0.0001/rank\n", text.ToString());
    }

    [Fact]
    public void Ordre_des_champs_json_sans_effet()
    {
        JsonNode KeyFirst() => new JsonObject
        {
            ["key"] = "damageTaken",
            ["add"] = new JsonObject { ["base"] = -0.01, ["perRank"] = -0.01 },
            ["conditions"] = new JsonArray("whileGuarding"),
        };
        JsonNode KeyLast() => new JsonObject
        {
            ["conditions"] = new JsonArray("whileGuarding"),
            ["add"] = new JsonObject { ["perRank"] = -0.01, ["base"] = -0.01 },
            ["key"] = "damageTaken",
        };
        Assert.NotEqual(KeyFirst().ToJsonString(), KeyLast().ToJsonString());
        Assert.Equal(Valid(OnTalent(0, 0, KeyFirst())).Fingerprint, Valid(OnTalent(0, 0, KeyLast())).Fingerprint);
        // Ordre des propriétés du talent lui-même : « modifiers » en tête.
        string moved = Fingerprint(r =>
        {
            JsonObject talent = ClassTestData.Talent(r, 0, 0);
            var copy = new JsonObject { ["modifiers"] = List(KeyFirst()) };
            foreach (var property in talent.ToList())
            {
                talent.Remove(property.Key);
                copy[property.Key] = property.Value;
            }
            JsonArray talents = ClassTestData.DataOf(r)["trees"]![0]!["talents"]!.AsArray();
            talents[0] = copy;
        });
        Assert.Equal(Valid(OnTalent(0, 0, KeyFirst())).Fingerprint, moved);
    }

    [Fact]
    public void Mise_en_forme_sans_effet()
    {
        JsonObject root = ClassTestData.Shipped();
        ClassTestData.Talent(root, 0, 0)["modifiers"] = List(Mod("guard.damageReduction", """{ "base": 0.04, "perRank": 0.04 }"""));
        ClassTestData.Skill(root, 7)["modifiers"] = List(Mod("damageTaken", "-0.005", "whileGuarding", "perRampartCharge"));
        string compact = root.ToJsonString();
        string indented = ClassTestData.ToJson(root);
        // Les sauts de ligne de la forme indentée ne sont qu'entre jetons : ajout d'espaces, de tabulations et de « \r ».
        string loose = indented.Replace("\n", "\r\n \t  ", StringComparison.Ordinal);
        string[] fingerprints = new[] { compact, indented, loose }.Select(json =>
        {
            ClassLoadResult result = ClassJsonReader.Read(json, ClassTestData.Progression);
            Assert.True(result.IsValid, result.Report.ToString());
            return result.Class!.Fingerprint;
        }).ToArray();
        Assert.Single(fingerprints.Distinct());
        Assert.NotEqual(ShippedFingerprint, fingerprints[0]);
    }

    [Fact]
    public void Ordre_de_declaration_fait_partie_du_contenu()
    {
        // D1 : modificateurs et conditions écrits à leur indice, dans l'ordre du fichier (comme les arbres, talents et déclencheurs).
        // Deux listes de même sens dans un autre ordre ont donc deux empreintes ; le combat, lui, agrège sans dépendre de l'ordre
        // (Lot81ModifierDataEndToEndTests.Ordre_des_sources_sans_effet_sur_le_combat_mais_dans_l_empreinte).
        Assert.NotEqual(
            Valid(OnTalent(0, 0, Mod("damageDealt", "0.1"), Mod("guard.damageReduction", "0.05"))).Fingerprint,
            Valid(OnTalent(0, 0, Mod("guard.damageReduction", "0.05"), Mod("damageDealt", "0.1"))).Fingerprint);
        Assert.NotEqual(
            Valid(OnTalent(0, 0, Mod("damageTaken", "-0.01", "whileGuarding", "perRampartCharge"))).Fingerprint,
            Valid(OnTalent(0, 0, Mod("damageTaken", "-0.01", "perRampartCharge", "whileGuarding"))).Fingerprint);
    }

    [Fact]
    public void Relecture_meme_empreinte()
    {
        // Liste « déterminisme » : deux lectures du même texte, même empreinte et mêmes définitions.
        static ClassLoadResult Read() => OnTalent(0, 0, Mod("guard.damageReduction", """{ "base": 0.04, "perRank": 0.04 }"""), Mod("damageTaken", "-0.005", "whileGuarding"));
        LoadedClass first = Valid(Read()), second = Valid(Read());
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(first.Definition.Trees[0].Talents[0].Modifiers.Select(m => (m.Key, m.Add, m.ConditionSet)),
            second.Definition.Trees[0].Talents[0].Modifiers.Select(m => (m.Key, m.Add, m.ConditionSet)));
    }
}

/// <summary>
/// Sous-lot 8.1 : version de schéma 5 des fichiers de classe (refus des autres versions avec un message explicite) et cohérence du
/// schéma JSON livré (data/schemas/class.schema.json) avec le programme : version, forme d'un modificateur, motif des clés comparé à
/// la liste fermée (ModifierKeys) par System.Text.RegularExpressions.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Data")]
public class Lot81ModifierDataSchemaTests
{
    private static JsonObject Schema() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "class.schema.json")))!.AsObject();

    private static JsonObject Def(string name) => Schema()["$defs"]![name]!.AsObject();

    private static Regex KeyPattern() => new(Def("modifier")["properties"]!["key"]!["pattern"]!.GetValue<string>(), RegexOptions.CultureInvariant);

    [Fact]
    public void Version_de_schema_des_classes_5()
    {
        Assert.Equal(5, DataFormat.ClassSchemaVersion);
        Assert.Equal(5, ClassValidator.CurrentSchemaVersion);
        Assert.Equal(5, JsonNode.Parse(ClassTestData.BastionJson)!["schemaVersion"]!.GetValue<int>());
        Assert.Equal(5, Schema()["properties"]!["schemaVersion"]!["const"]!.GetValue<int>());
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(4, true)]
    [InlineData(3, true)]
    [InlineData(6, true)]
    public void Classe_d_une_autre_version_refusee_avec_un_message_explicite(int version, bool withModifiers)
    {
        ClassLoadResult result = ClassTestData.ReadMutated(root =>
        {
            root["schemaVersion"] = version;
            // Contenu non analysé : une clé inconnue sans « add » ne s'ajoute pas au refus de la version.
            if (withModifiers) ClassTestData.Talent(root, 0, 0)["modifiers"] = List(JsonNode.Parse("""{ "key": "cle.inconnue" }"""));
        });
        AssertSingleError(result, "$.schemaVersion", ValidationCode.UnsupportedSchemaVersion);
        string message = result.Report.Errors[0].Message;
        Assert.Contains($"version de format {version}", message);
        if (version < 5)
        {
            Assert.Contains("antérieure à la version 5", message);
            Assert.Contains("aucune migration", message);
        }
        else Assert.Contains("ce programme lit la version 5", message);
    }

    [Fact]
    public void Fichier_de_classe_au_schema_4_refuse_au_chargement_du_dossier()
    {
        string directory = ScenarioTestData.CopyData((relative, text) =>
        {
            if (relative != "classes/bastion.json") return text;
            JsonObject root = JsonNode.Parse(text)!.AsObject();
            root["schemaVersion"] = 4;
            return root.ToJsonString();
        });
        try
        {
            GameDataLoadResult loaded = GameDataLoader.Load(directory);
            Assert.Null(loaded.Data);
            var (path, report) = Assert.Single(loaded.Failures);
            Assert.EndsWith("bastion.json", path);
            ValidationError error = Assert.Single(report.Errors);
            Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (error.Path, error.Code));
            Assert.Contains("version de format 4 antérieure à la version 5", error.Message);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Schema_json_decrit_la_forme_d_un_modificateur()
    {
        JsonObject modifier = Def("modifier");
        Assert.Equal("object", modifier["type"]!.GetValue<string>());
        Assert.False(modifier["additionalProperties"]!.GetValue<bool>());
        Assert.Equal(new[] { "key", "add" }, modifier["required"]!.AsArray().Select(n => n!.GetValue<string>()));
        Assert.Equal(new[] { "key", "add", "conditions" }, modifier["properties"]!.AsObject().Select(p => p.Key));
        Assert.Equal("#/$defs/rankedValue", modifier["properties"]!["add"]!["$ref"]!.GetValue<string>());

        // Conditions : les trois jetons du programme, au plus deux, sans doublon.
        JsonObject conditions = modifier["properties"]!["conditions"]!.AsObject();
        string[] tokens = conditions["items"]!["enum"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray();
        Assert.Equal(new[] { "perRampartCharge", "perResonanceStep", "whileGuarding" }, tokens.OrderBy(t => t, StringComparer.Ordinal));
        Assert.Equal(tokens.OrderBy(t => t, StringComparer.Ordinal), ModifierKeys.Conditions.Keys.OrderBy(t => t, StringComparer.Ordinal));
        Assert.Equal(2, conditions["maxItems"]!.GetValue<int>());
        Assert.True(conditions["uniqueItems"]!.GetValue<bool>());

        // Valeur : nombre, ou { base, perRank } avec base obligatoire.
        JsonArray oneOf = Def("rankedValue")["oneOf"]!.AsArray();
        Assert.Equal("number", oneOf[0]!["type"]!.GetValue<string>());
        Assert.Equal(new[] { "base" }, oneOf[1]!["required"]!.AsArray().Select(n => n!.GetValue<string>()));
        Assert.Equal(new[] { "base", "perRank" }, oneOf[1]!["properties"]!.AsObject().Select(p => p.Key));
        Assert.False(oneOf[1]!["additionalProperties"]!.GetValue<bool>());

        // Porteurs : talents et compétences, au plus 8 (plafond du programme).
        foreach (string owner in new[] { "talent", "skill" })
        {
            JsonObject list = Def(owner)["properties"]!["modifiers"]!.AsObject();
            Assert.Equal("#/$defs/modifier", list["items"]!["$ref"]!.GetValue<string>());
            Assert.Equal(8, list["maxItems"]!.GetValue<int>());
            Assert.Equal(ModifierDefinition.MaxPerOwner, list["maxItems"]!.GetValue<int>());
            Assert.DoesNotContain("modifiers", Def(owner)["required"]!.AsArray().Select(n => n!.GetValue<string>()));
        }
    }

    /// <summary>Les vingt clés de la liste fermée (tableau D2), écrites indépendamment du programme.</summary>
    private static readonly string[] DesignKeys =
    {
        "guard.damageReduction", "guard.upkeepPerSecond", "guard.costPerBlock", "guard.noUpkeepBreak", "guard.reductionAtZeroEndurance",
        "guard.breakShortenPerCharge", "parry.windowMs", "parry.failureRecoveryMs", "rampartCharges.max", "resonance.perDirectHit",
        "endurance.max", "endurance.regenPerSecond", "maxHealth", "statusResistance", "damageDealt", "damageTaken",
        "status.<id>.durationDealt", "status.<id>.durationTaken", "skill.<id>.coefficient", "skill.<id>.cooldownMs",
    };

    /// <summary>Exemple concret d'une spécification : identifiant d'altération ou de compétence de la classe livrée à la place de &lt;id&gt;.</summary>
    private static string Example(ModifierSpec spec) =>
        spec.Name.Replace("status.<id>", "status.fragility", StringComparison.Ordinal).Replace("skill.<id>", "skill.bastion.frappe_du_rempart", StringComparison.Ordinal);

    [Fact]
    public void Motif_de_cle_du_schema_accepte_chaque_cle_de_la_liste_fermee()
    {
        Assert.Equal(DesignKeys, ModifierKeys.Specs.Select(s => s.Name));
        Regex pattern = KeyPattern();
        foreach (ModifierSpec spec in ModifierKeys.Specs)
        {
            string key = Example(spec);
            Assert.True(pattern.IsMatch(key), $"motif du schéma : « {key} » refusée");
            Assert.True(ModifierKeys.TryParse(key, out ModifierKey parsed), $"programme : « {key} » refusée");
            Assert.Equal(spec.Field, parsed.Field);
            Assert.Equal(key, parsed.ToString());
        }
    }

    [Theory]
    [InlineData("status.stunImmunity.durationTaken")]       // identifiant camelCase
    [InlineData("status.b2.durationDealt")]
    [InlineData("skill.bastion.bastion_absolu.cooldownMs")]
    [InlineData("skill.a1.b_2.coefficient")]
    [InlineData("skill.x.coefficient")]
    public void Motif_de_cle_du_schema_et_programme_acceptent_les_cles_parametrees(string key)
    {
        Assert.Matches(KeyPattern(), key);
        Assert.True(ModifierKeys.TryParse(key, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("guard")]
    [InlineData("guard.")]
    [InlineData("guard.damagereduction")]
    [InlineData("Guard.damageReduction")]
    [InlineData("guard.damageReduction.extra")]
    [InlineData(" guard.damageReduction")]
    [InlineData("damageDealt ")]
    [InlineData("maxhealth")]
    [InlineData("statusResistance.max")]
    [InlineData("shield.potencyDealt")]                     // 8.2
    [InlineData("protection.redirectRatio")]                // 8.3
    [InlineData("status.fragility.duration")]
    [InlineData("status.Fragility.durationTaken")]
    [InlineData("status.1burn.durationTaken")]
    [InlineData("status..durationTaken")]
    [InlineData("status.durationTaken")]
    [InlineData("status.burn.bleed.durationTaken")]
    [InlineData("status.burn_x.durationTaken")]
    [InlineData("skill.coefficient")]
    [InlineData("skill.Bastion.frappe.coefficient")]
    [InlineData("skill.bastion.frappe-du-rempart.coefficient")]
    [InlineData("skill.bastion.frappe_du_rempart")]
    [InlineData("skill.bastion.frappe_du_rempart.cooldown")]
    [InlineData("skill..coefficient")]
    [InlineData("talent.bastion.rempart.garde_stable.coefficient")]
    public void Motif_de_cle_du_schema_et_programme_refusent_les_cles_invalides(string key)
    {
        Assert.DoesNotMatch(KeyPattern(), key);
        Assert.False(ModifierKeys.TryParse(key, out _));
    }
}

/// <summary>
/// Sous-lot 8.1, bout en bout : classe à modificateurs écrite dans un dossier de données temporaire, scénario lu depuis un fichier,
/// combat simulé deux fois (déterminisme), objet <c>rules</c> du journal (ligne <c>combatant</c>) attendu exactement, rejeu
/// identique, journal altéré détecté, rejeu refusé avec d'autres données. Équivalence « modificateur sur la base B » et « règle de
/// base B + x sans modificateur », et indépendance de l'ordre des sources.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Engine")]
public class Lot81ModifierDataEndToEndTests
{
    private const string Scenario = "entrainement_garde_parade";

    /// <summary>Copie de data/v0 avec bastion.json et rules.json modifiés ; le dossier est à supprimer par l'appelant.</summary>
    private static string DataDirectory(Action<JsonObject>? bastion = null, Action<JsonObject>? rules = null) =>
        ScenarioTestData.CopyData((relative, text) =>
        {
            Action<JsonObject>? mutate = relative == "classes/bastion.json" ? bastion : relative == "rules.json" ? rules : null;
            if (mutate is null) return text;
            JsonObject root = JsonNode.Parse(text)!.AsObject();
            mutate(root);
            return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        });

    private static GameData Load(string directory)
    {
        GameDataLoadResult loaded = GameDataLoader.Load(directory);
        Assert.True(loaded.Data != null, string.Join("; ", loaded.Failures.SelectMany(f => f.Report.Errors.Select(e => e.ToString()))));
        return loaded.Data!;
    }

    private static GameData LoadAndDelete(Action<JsonObject>? bastion = null, Action<JsonObject>? rules = null)
    {
        string directory = DataDirectory(bastion, rules);
        try
        {
            return Load(directory);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static IReadOnlyList<string> Run(GameData data)
    {
        ScenarioLoadResult loaded = ScenarioJsonReader.Read(File.ReadAllText(ScenarioTestData.ScenarioPath(Scenario)), data);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        var log = new MemoryCombatLog();
        CombatEngine.Run(loaded.Setup!, log);
        return log.Lines;
    }

    private static int CombatantLine(IReadOnlyList<string> lines, string id)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            JsonObject o = JsonNode.Parse(lines[i])!.AsObject();
            if (o["event"]?.GetValue<string>() == "combatant" && o["combatant"]!.GetValue<string>() == id) return i;
        }
        throw new InvalidOperationException($"ligne combatant « {id} » absente");
    }

    /// <summary>
    /// Modificateurs sur cinq porteurs actifs du build d'entrainement_garde_parade (garde_stable 3, riposte_precise 2, egide_reactive 3,
    /// test_passif_1 et test_passif_2), plus un talent non pris et un passif non équipé, sans effet attendu.
    /// </summary>
    private static void FiveSources(JsonObject bastion)
    {
        ClassTestData.Talent(bastion, 0, 0)["modifiers"] = List(Mod("guard.damageReduction", """{ "base": 0.04, "perRank": 0.04 }"""));
        ClassTestData.Talent(bastion, 1, 0)["modifiers"] = List(Mod("skill.bastion.frappe_du_rempart.coefficient", """{ "base": 0.1, "perRank": 0.05 }"""));
        ClassTestData.Talent(bastion, 2, 0)["modifiers"] = List(Mod("damageTaken", """{ "base": -0.01, "perRank": -0.01 }""", "whileGuarding"));
        ClassTestData.Talent(bastion, 0, 3)["modifiers"] = List(Mod("resonance.perDirectHit", "5"));          // test_palier_1 : rang 0
        ClassTestData.Skill(bastion, 7)["modifiers"] = List(Mod("status.fragility.durationTaken", "-1000"), Mod("damageTaken", "-0.02", "whileGuarding"));
        ClassTestData.Skill(bastion, 8)["modifiers"] = List(Mod("guard.costPerBlock", "-4"), Mod("endurance.max", "20"));
        ClassTestData.Skill(bastion, 9)["modifiers"] = List(Mod("guard.damageReduction", "0.03"));             // test_passif_3 : non équipé
    }

    /// <summary>
    /// Objet <c>rules</c> attendu, calculé à la main depuis rules.json (garde 0,6, entretien 2, blocage 10, parade 150 et 500 ms, Charges
    /// 5, Résonance 3, régénération 10) : réduction 0,6 + 0,04 + 2 × 0,04 = 0,72 ; blocage 10 − 4 = 6 ; coefficient de Frappe 1,2 + 0,1 +
    /// 1 × 0,05 = 1,35 (rang 2) ; ajout de −1 000 ms à la Fragilité subie ; conditionnel « en garde » −0,01 − 2 × 0,01 − 0,02 = −0,05
    /// (deux sources sommées). Ni le talent non pris (Résonance) ni le passif non équipé (garde) ne comptent.
    /// </summary>
    private const string FiveSourcesRules =
        "{\"guard.damageReduction\":0.72,\"guard.upkeepPerSecond\":2,\"guard.costPerBlock\":6,\"guard.noUpkeepBreak\":false," +
        "\"guard.reductionAtZeroEndurance\":0,\"guard.breakShortenPerCharge\":0,\"parry.windowMs\":150,\"parry.failureRecoveryMs\":500," +
        "\"rampartCharges.max\":5,\"resonance.perDirectHit\":3,\"endurance.regenPerSecond\":10,\"damageDealt\":0,\"damageTaken\":0," +
        "\"skill.bastion.frappe_du_rempart.coefficient\":1.35,\"status.fragility.durationTaken\":-1000," +
        "\"conditional\":[{\"key\":\"damageTaken\",\"conditions\":[\"whileGuarding\"],\"value\":-0.05}]}";

    [Fact]
    public void Scenario_de_fichier_simule_journal_avec_regles_effectives_rejeu_identique()
    {
        string directory = DataDirectory(FiveSources);
        try
        {
            GameData data = Load(directory);
            LoadedClass bastion = data.Classes.Single();
            Assert.NotEqual(ScenarioTestData.ShippedData.Classes.Single().Fingerprint, bastion.Fingerprint);
            Assert.Equal(9, bastion.Definition.Trees.SelectMany(t => t.Talents).Sum(t => t.Modifiers.Count) + bastion.Definition.Skills.Sum(s => s.Modifiers.Count));

            // Scénario écrit dans le dossier temporaire, lu depuis le fichier.
            string scenarioPath = Path.Combine(directory, "scenario.json");
            File.WriteAllText(scenarioPath, File.ReadAllText(ScenarioTestData.ScenarioPath(Scenario)));
            ScenarioLoadResult scenario = ScenarioJsonReader.ReadFile(scenarioPath, data);
            Assert.True(scenario.IsValid, scenario.Report.ToString());
            var first = new MemoryCombatLog();
            CombatEngine.Run(scenario.Setup!, first);
            var second = new MemoryCombatLog();
            CombatEngine.Run(ScenarioJsonReader.ReadFile(scenarioPath, data).Setup!, second);
            Assert.Equal(first.Lines, second.Lines);   // déterminisme : deux lectures, deux simulations, même journal

            // En-tête : identité de la classe modifiée.
            JsonObject header = JsonNode.Parse(first.Lines[0])!.AsObject();
            Assert.Equal(bastion.Fingerprint, header["data"]!["classes"]![0]!["fingerprint"]!.GetValue<string>());

            // Ligne combatant du personnage : objet rules exact, octet par octet ; null pour l'assaillant.
            int line = CombatantLine(first.Lines, "bastion");
            Assert.EndsWith(",\"rules\":" + FiveSourcesRules + "}", first.Lines[line], StringComparison.Ordinal);
            Assert.EndsWith(",\"rules\":null}", first.Lines[CombatantLine(first.Lines, "assaillant")], StringComparison.Ordinal);

            // endurance.max : +20 sur l'endurance maximale du même build sans modificateur (champ du combattant, pas de rules).
            IReadOnlyList<string> shipped = Run(ScenarioTestData.ShippedData);
            long Endurance(IReadOnlyList<string> lines) => JsonNode.Parse(lines[CombatantLine(lines, "bastion")])!["maxEndurance"]!.GetValue<long>();
            Assert.Equal(Endurance(shipped) + 20, Endurance(first.Lines));

            // Rejeu du fichier journal : identique.
            string logPath = Path.Combine(directory, "journal.jsonl");
            File.WriteAllText(logPath, first.ToString());
            ReplayOutcome replay = CombatLogReplayer.ReplayFile(logPath, data);
            Assert.Equal(ReplayStatus.Identical, replay.Status);
            Assert.Empty(replay.Problems);

            // Journal altéré sur l'objet rules (valeur scalaire, puis valeur conditionnelle) : rejeu divergent à cette ligne.
            foreach (var (from, to) in new[] { ("\"guard.damageReduction\":0.72", "\"guard.damageReduction\":0.6"), ("\"value\":-0.05", "\"value\":-0.04") })
            {
                string tampered = first.Lines[line].Replace(from, to, StringComparison.Ordinal);
                Assert.NotEqual(first.Lines[line], tampered);
                File.WriteAllText(logPath, string.Concat(first.Lines.Select((l, i) => (i == line ? tampered : l) + "\n")));
                ReplayOutcome altered = CombatLogReplayer.ReplayFile(logPath, data);
                Assert.Equal(ReplayStatus.Diverged, altered.Status);
                Assert.Equal(line + 1, altered.Comparison!.FirstDifferentLine);
                Assert.Equal(tampered, altered.Comparison.Expected);
                Assert.Equal(first.Lines[line], altered.Comparison.Actual);
            }

            // Données livrées (classe sans modificateur) : rejeu refusé avant simulation, empreinte de classe en cause.
            ReplayOutcome refused = CombatLogReplayer.Replay(first.Lines, ScenarioTestData.ShippedData);
            Assert.Equal(ReplayStatus.Refused, refused.Status);
            Assert.Null(refused.Comparison);
            Assert.Contains(refused.Problems, p => p.Contains(bastion.Fingerprint, StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Modificateurs_equivalents_aux_regles_de_base_correspondantes()
    {
        // Liste « combinaisons » : modificateurs de cinq sources (talents de rang 3, 1 et 2, deux passifs) sur huit règles scalaires,
        // comparés à des règles de base déjà égales aux valeurs résultantes, sans modificateur. Hors en-tête (empreintes), les deux
        // journaux sont identiques octet par octet, objet rules compris : le moteur lit partout les valeurs du combattant (D4).
        GameData modified = LoadAndDelete(bastion: b =>
        {
            ClassTestData.Talent(b, 0, 0)["modifiers"] = List(Mod("guard.damageReduction", """{ "base": 0.04, "perRank": 0.04 }"""));   // 0,6 + 0,12
            ClassTestData.Talent(b, 0, 1)["modifiers"] = List(Mod("rampartCharges.max", "2"), Mod("guard.upkeepPerSecond", "1"));         // 5 + 2 ; 2 + 1
            ClassTestData.Talent(b, 1, 0)["modifiers"] = List(Mod("parry.failureRecoveryMs", """{ "base": -50, "perRank": -50 }"""));     // 500 − 100 (rang 2)
            ClassTestData.Skill(b, 7)["modifiers"] = List(Mod("guard.costPerBlock", "-4"), Mod("endurance.regenPerSecond", "5"));         // 10 − 4 ; 10 + 5
            ClassTestData.Skill(b, 8)["modifiers"] = List(Mod("resonance.perDirectHit", "2"), Mod("parry.windowMs", "50"));               // 3 + 2 ; 150 + 50
        });
        GameData baseline = LoadAndDelete(rules: r =>
        {
            RulesTestData.Set(r, "combat.guard.damageReduction", JsonNode.Parse("0.72"));
            RulesTestData.Set(r, "combat.rampartCharges.max", 7);
            RulesTestData.Set(r, "combat.guard.upkeepPerSecond", 3);
            RulesTestData.Set(r, "combat.parry.failureRecoveryMs", 400);
            RulesTestData.Set(r, "combat.guard.costPerBlock", 6);
            RulesTestData.Set(r, "combat.endurance.regenPerSecond", 15);
            RulesTestData.Set(r, "combat.resonance.perDirectHit", 5);
            RulesTestData.Set(r, "combat.parry.windowMs", 200);
        });
        IReadOnlyList<string> withModifiers = Run(modified), withRules = Run(baseline), shipped = Run(ScenarioTestData.ShippedData);
        Assert.NotEqual(withModifiers[0], withRules[0]);
        Assert.Equal(withRules.Skip(1), withModifiers.Skip(1));
        Assert.Contains("\"rules\":{\"guard.damageReduction\":0.72,\"guard.upkeepPerSecond\":3,\"guard.costPerBlock\":6,", withModifiers[CombatantLine(withModifiers, "bastion")]);
        // Test non vide : le combat diffère de celui des données livrées ailleurs que sur les lignes de mise en place.
        int setup = withModifiers.Count(l => l.Contains("\"phase\":\"setup\"", StringComparison.Ordinal));
        Assert.False(withModifiers.Skip(1 + setup).SequenceEqual(shipped.Skip(1 + setup)), "les modificateurs doivent changer le combat");
    }

    [Fact]
    public void Ordre_des_sources_sans_effet_sur_le_combat_mais_dans_l_empreinte()
    {
        // Liste « agrégation indépendante de l'ordre » : mêmes sommes (réduction 0,09, blocage −5) réparties autrement entre un talent
        // et un passif, et dans un autre ordre dans chaque liste. Empreintes différentes (l'ordre du fichier est du contenu), combat
        // identique hors en-tête.
        GameData a = LoadAndDelete(bastion: b =>
        {
            ClassTestData.Talent(b, 0, 0)["modifiers"] = List(Mod("guard.damageReduction", "0.04"), Mod("guard.costPerBlock", "-2"));
            ClassTestData.Skill(b, 7)["modifiers"] = List(Mod("guard.damageReduction", "0.05"), Mod("guard.costPerBlock", "-3"));
        });
        GameData b = LoadAndDelete(bastion: b =>
        {
            ClassTestData.Talent(b, 0, 0)["modifiers"] = List(Mod("guard.costPerBlock", "-3"), Mod("guard.damageReduction", "0.05"));
            ClassTestData.Skill(b, 7)["modifiers"] = List(Mod("guard.costPerBlock", "-2"), Mod("guard.damageReduction", "0.04"));
        });
        Assert.NotEqual(a.Classes.Single().Fingerprint, b.Classes.Single().Fingerprint);
        IReadOnlyList<string> logA = Run(a), logB = Run(b);
        Assert.Equal(logA.Skip(1), logB.Skip(1));
        Assert.Contains("\"rules\":{\"guard.damageReduction\":0.69,\"guard.upkeepPerSecond\":2,\"guard.costPerBlock\":5,", logA[CombatantLine(logA, "bastion")]);
        // Chaque journal se rejoue avec ses propres données, et pas avec les autres.
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(logA, a).Status);
        Assert.Equal(ReplayStatus.Refused, CombatLogReplayer.Replay(logA, b).Status);
    }

    /// <summary>Données livrées avec la classe mutée, en mémoire (sans dossier temporaire).</summary>
    private static GameData InMemory(Action<JsonObject> bastion) =>
        new(ScenarioTestData.ShippedData.Rules, new[] { Valid(ClassTestData.ReadMutated(bastion)) });

    private static string RulesOf(IReadOnlyList<string> lines)
    {
        string line = lines[CombatantLine(lines, "bastion")];
        int start = line.IndexOf(",\"rules\":", StringComparison.Ordinal) + ",\"rules\":".Length;
        Assert.EndsWith("}", line, StringComparison.Ordinal);
        return line[start..^1];
    }

    [Fact]
    public void Sources_inactives_sans_effet_journal_identique_aux_donnees_livrees()
    {
        // D3 : seuls les talents de rang ≥ 1 du build et les passifs équipés comptent. Modificateurs sur un talent non pris
        // (test_palier_1) et deux passifs non équipés (test_passif_3 et 4) : règles de base, journal identique hors en-tête.
        GameData data = InMemory(b =>
        {
            ClassTestData.Talent(b, 0, 3)["modifiers"] = List(Mod("guard.damageReduction", """{ "base": 0.1, "perRank": 0.01 }"""), Mod("endurance.max", "50"));
            ClassTestData.Skill(b, 9)["modifiers"] = List(Mod("damageTaken", "-0.5", "whileGuarding"), Mod("maxHealth", "1"));
            ClassTestData.Skill(b, 10)["modifiers"] = List(Mod("skill.bastion.frappe_du_rempart.coefficient", "1"), Mod("status.fragility.durationTaken", "-1000"));
        });
        IReadOnlyList<string> log = Run(data), shipped = Run(ScenarioTestData.ShippedData);
        Assert.NotEqual(shipped[0], log[0]);
        Assert.Equal(shipped.Skip(1), log.Skip(1));
        Assert.Equal(EdS.TestSupport.Lot8LogFields.BaseRulesJson(ScenarioTestData.ShippedData.Rules.Combat), RulesOf(log));
    }

    [Fact]
    public void Combinaisons_ecretees_dans_l_objet_rules()
    {
        // Liste « combinaisons » et « bornes » : chaque modificateur seul est légal, leur somme dépasse les plafonds ou planchers de
        // jeu et la valeur résultante est écrêtée (D2), jamais refusée. Valeurs attendues à la main depuis rules.json :
        //   réduction 0,6 + 0,75 + 0,75 → 0,75 ; blocage 10 − 10⁶ → 0 ; fenêtre 150 + 2 × 500 → 500 ; Charges 5 + 2 × 10 → 10 ;
        //   régénération 10 − 10⁶ → 0 ; coefficient de Frappe 1,2 + 2 × 100 → 100 ; recharge de Heurt 10 000 − 86 400 000 → 0 ;
        //   ajout de durée de brûlure infligée : 2 × 86 400 000 (somme brute, écrêtée à l'application, D5).
        GameData high = InMemory(b =>
        {
            ClassTestData.Talent(b, 0, 0)["modifiers"] = List(Mod("guard.damageReduction", """{ "base": 0.25, "perRank": 0.25 }"""),
                Mod("rampartCharges.max", "10"), Mod("parry.windowMs", "500"), Mod("skill.bastion.frappe_du_rempart.coefficient", "100"),
                Mod("status.burn.durationDealt", "86400000"));
            ClassTestData.Skill(b, 7)["modifiers"] = List(Mod("guard.damageReduction", "0.75"), Mod("rampartCharges.max", "10"), Mod("parry.windowMs", "500"),
                Mod("skill.bastion.frappe_du_rempart.coefficient", "100"), Mod("status.burn.durationDealt", "86400000"), Mod("guard.costPerBlock", "-1000000"),
                Mod("endurance.regenPerSecond", "-1000000"), Mod("skill.bastion.heurt_de_bouclier.cooldownMs", "-86400000"));
        });
        Assert.Equal(
            "{\"guard.damageReduction\":0.75,\"guard.upkeepPerSecond\":2,\"guard.costPerBlock\":0,\"guard.noUpkeepBreak\":false," +
            "\"guard.reductionAtZeroEndurance\":0,\"guard.breakShortenPerCharge\":0,\"parry.windowMs\":500,\"parry.failureRecoveryMs\":500," +
            "\"rampartCharges.max\":10,\"resonance.perDirectHit\":3,\"endurance.regenPerSecond\":0,\"damageDealt\":0,\"damageTaken\":0," +
            "\"skill.bastion.frappe_du_rempart.coefficient\":100,\"skill.bastion.heurt_de_bouclier.cooldownMs\":0," +
            "\"status.burn.durationDealt\":172800000,\"conditional\":[]}",
            RulesOf(Run(high)));

        // Planchers et révisions D10 : réduction à endurance nulle ramenée à la réduction de garde effective (min(0,7 ; 0,6)) ;
        // fenêtre 150 − 2 × 500 → 1 tick (50 ms) ; Charges 5 − 10 → 0 ; Résonance 3 − 3 → 0 ; drapeau actif ; raccourci de rupture.
        GameData low = InMemory(b =>
        {
            ClassTestData.Talent(b, 0, 0)["modifiers"] = List(Mod("guard.reductionAtZeroEndurance", "0.7"), Mod("parry.windowMs", "-500"),
                Mod("rampartCharges.max", "-10"), Mod("guard.noUpkeepBreak", "1"));
            ClassTestData.Skill(b, 7)["modifiers"] = List(Mod("parry.windowMs", "-500"), Mod("resonance.perDirectHit", "-3"), Mod("guard.breakShortenPerCharge", "1000"));
        });
        Assert.Equal(
            "{\"guard.damageReduction\":0.6,\"guard.upkeepPerSecond\":2,\"guard.costPerBlock\":10,\"guard.noUpkeepBreak\":true," +
            "\"guard.reductionAtZeroEndurance\":0.6,\"guard.breakShortenPerCharge\":1000,\"parry.windowMs\":50,\"parry.failureRecoveryMs\":500," +
            "\"rampartCharges.max\":0,\"resonance.perDirectHit\":0,\"endurance.regenPerSecond\":10,\"damageDealt\":0,\"damageTaken\":0,\"conditional\":[]}",
            RulesOf(Run(low)));
    }

    [Fact]
    public void Conditionnels_agreges_par_ensemble_et_tries_dans_l_objet_rules()
    {
        // Sommes par (clé, ensemble de conditions), quel que soit l'ordre déclaré des conditions ; liste triée par clé (damageDealt
        // avant damageTaken) puis par ensemble ({W} < {C} < {W,C} < {R}…, ordre des drapeaux). garde_stable au rang 3, riposte_precise 2.
        GameData data = InMemory(b =>
        {
            ClassTestData.Talent(b, 0, 0)["modifiers"] = List(Mod("damageTaken", """{ "base": -0.01, "perRank": -0.01 }""", "perRampartCharge", "whileGuarding"),
                Mod("damageDealt", "0.02", "perResonanceStep"));
            ClassTestData.Talent(b, 1, 0)["modifiers"] = List(Mod("damageTaken", "-0.04", "whileGuarding", "perRampartCharge"), Mod("damageDealt", "0.1"),
                Mod("damageTaken", "-0.05", "whileGuarding"));
            ClassTestData.Skill(b, 8)["modifiers"] = List(Mod("damageDealt", "0.01", "perResonanceStep"), Mod("damageTaken", "-0.1"));
        });
        Assert.Equal(
            "{\"guard.damageReduction\":0.6,\"guard.upkeepPerSecond\":2,\"guard.costPerBlock\":10,\"guard.noUpkeepBreak\":false," +
            "\"guard.reductionAtZeroEndurance\":0,\"guard.breakShortenPerCharge\":0,\"parry.windowMs\":150,\"parry.failureRecoveryMs\":500," +
            "\"rampartCharges.max\":5,\"resonance.perDirectHit\":3,\"endurance.regenPerSecond\":10,\"damageDealt\":0.1,\"damageTaken\":-0.1," +
            "\"conditional\":[{\"key\":\"damageDealt\",\"conditions\":[\"perResonanceStep\"],\"value\":0.03}," +
            "{\"key\":\"damageTaken\",\"conditions\":[\"whileGuarding\"],\"value\":-0.05}," +
            "{\"key\":\"damageTaken\",\"conditions\":[\"whileGuarding\",\"perRampartCharge\"],\"value\":-0.07}]}",
            RulesOf(Run(data)));
    }

    [Fact]
    public void Statistiques_du_combattant_avec_modificateurs()
    {
        // Clés de statistiques : sur la ligne combatant (maxHealth, maxEndurance, statusResistance), pas dans rules (D10). Valeurs
        // attendues depuis le même build sans modificateur : PV max × (1 + %) avec le % écrêté dans [−90 %, +1 000 %] (D2) ;
        // endurance et résistance en contributions plates, endurance ≥ 0.
        JsonObject Bastion(IReadOnlyList<string> lines) => JsonNode.Parse(lines[CombatantLine(lines, "bastion")])!.AsObject();
        JsonObject shipped = Bastion(Run(ScenarioTestData.ShippedData));
        long health = shipped["maxHealth"]!.GetValue<long>();
        decimal resistance = shipped["statusResistance"]!.GetValue<decimal>();
        Assert.Equal(0, health % 10);   // multiplications exactes ci-dessous : l'arrondi n'intervient pas

        JsonObject high = Bastion(Run(InMemory(b =>
        {
            ClassTestData.Talent(b, 0, 0)["modifiers"] = List(Mod("maxHealth", "10"), Mod("statusResistance", "5.5"));
            ClassTestData.Skill(b, 7)["modifiers"] = List(Mod("maxHealth", "10"), Mod("endurance.max", "-1000000"));
        })));
        Assert.Equal((health * 11, health * 11), (high["maxHealth"]!.GetValue<long>(), high["health"]!.GetValue<long>()));   // +2 000 % → +1 000 %
        Assert.Equal((0L, 0L), (high["maxEndurance"]!.GetValue<long>(), high["endurance"]!.GetValue<long>()));
        Assert.Equal(resistance + 5.5m, high["statusResistance"]!.GetValue<decimal>());
        Assert.Equal(EdS.TestSupport.Lot8LogFields.BaseRulesJson(ScenarioTestData.ShippedData.Rules.Combat), high["rules"]!.ToJsonString());

        JsonObject low = Bastion(Run(InMemory(b =>
        {
            ClassTestData.Talent(b, 0, 0)["modifiers"] = List(Mod("maxHealth", """{ "base": -0.3, "perRank": -0.3 }"""));   // −0,9 au rang 3
            ClassTestData.Skill(b, 8)["modifiers"] = List(Mod("maxHealth", "-0.5"));
        })));
        Assert.Equal(health / 10, low["maxHealth"]!.GetValue<long>());   // −140 % → −90 %
    }

    [Fact]
    public void Classe_refusee_aucune_donnee_chargee()
    {
        // Liste « aucune mutation partielle » : une classe au modificateur invalide fait refuser tout le dossier ; aucune donnée
        // partielle n'est rendue, et le rapport localise l'erreur dans le fichier.
        string directory = DataDirectory(b => ClassTestData.Skill(b, 7)["modifiers"] = List(Mod("damageTaken", "0.01")));
        try
        {
            GameDataLoadResult loaded = GameDataLoader.Load(directory);
            Assert.False(loaded.IsValid);
            Assert.Null(loaded.Data);
            var (path, report) = Assert.Single(loaded.Failures);
            Assert.EndsWith("bastion.json", path);
            ValidationError error = Assert.Single(report.Errors);
            Assert.Equal((PassivePath + "[0].add", ValidationCode.InvalidValue), (error.Path, error.Code));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}

/// <summary>
/// Sous-lot 8.1, M05 côté données : les listes de modificateurs d'une classe chargée, leurs conditions et les catalogues de clés ne
/// sont pas modifiables ; les définitions copient les collections reçues.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Data")]
[Trait("Category", "Regression")]
public class Lot81ModifierDataImmutabilityTests
{
    private static void AssertReadOnly<T>(IReadOnlyList<T> list)
    {
        Assert.False(list is T[]);
        Assert.False(list is List<T>);
        Assert.True(((IList<T>)list).IsReadOnly);
    }

    private static LoadedClass Loaded() => Valid(ClassTestData.ReadMutated(root =>
    {
        ClassTestData.Talent(root, 0, 0)["modifiers"] = List(Mod("guard.damageReduction", """{ "base": 0.04, "perRank": 0.04 }"""));
        ClassTestData.Skill(root, 7)["modifiers"] = List(Mod("endurance.max", "20"), Mod("damageTaken", "-0.02", "whileGuarding", "perRampartCharge"));
    }));

    [Fact]
    public void Listes_de_modificateurs_chargees_non_modifiables()
    {
        LoadedClass loaded = Loaded();
        string fingerprint = loaded.Fingerprint;
        IReadOnlyList<ModifierDefinition> talent = loaded.Definition.Trees[0].Talents[0].Modifiers;
        IReadOnlyList<ModifierDefinition> passive = loaded.Definition.Skills[7].Modifiers;
        AssertReadOnly(talent);
        AssertReadOnly(passive);
        AssertReadOnly(loaded.Definition.Skills[0].Modifiers);              // liste vide d'une active
        AssertReadOnly(loaded.Definition.Trees[1].Talents[0].Modifiers);    // liste vide d'un talent
        AssertReadOnly(passive[1].Conditions);
        AssertReadOnly(passive[0].Conditions);

        ModifierDefinition intruder = new("damageDealt", new RankedValue(Fixed.FromInt(10)));
        Assert.Throws<NotSupportedException>(() => ((IList<ModifierDefinition>)talent).Add(intruder));
        Assert.Throws<NotSupportedException>(() => ((IList<ModifierDefinition>)talent)[0] = intruder);
        Assert.Throws<NotSupportedException>(() => ((IList<ModifierDefinition>)passive).RemoveAt(0));
        Assert.Throws<NotSupportedException>(() => ((IList<ModifierDefinition>)passive).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<ModifierDefinition>)loaded.Definition.Skills[0].Modifiers).Add(intruder));
        Assert.Throws<NotSupportedException>(() => ((IList<ModifierCondition>)passive[1].Conditions).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<ModifierCondition>)passive[1].Conditions)[0] = ModifierCondition.PerResonanceStep);

        // Après les tentatives : même contenu, même empreinte qu'une nouvelle lecture.
        Assert.Equal(new[] { "guard.damageReduction" }, talent.Select(m => m.Key));
        Assert.Equal(new[] { "endurance.max", "damageTaken" }, passive.Select(m => m.Key));
        Assert.Equal(new[] { ModifierCondition.WhileGuarding, ModifierCondition.PerRampartCharge }, passive[1].Conditions);
        Assert.Equal(fingerprint, Loaded().Fingerprint);
    }

    [Fact]
    public void Catalogues_de_cles_et_de_conditions_non_modifiables()
    {
        AssertReadOnly(ModifierKeys.Specs);
        Assert.False(ModifierKeys.Conditions is Dictionary<string, ModifierCondition>);
        Assert.True(((IDictionary<string, ModifierCondition>)ModifierKeys.Conditions).IsReadOnly);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, ModifierCondition>)ModifierKeys.Conditions)["whileBlocking"] = ModifierCondition.WhileGuarding);
        Assert.Throws<NotSupportedException>(() => ((IList<ModifierSpec>)ModifierKeys.Specs).RemoveAt(0));
        Assert.Equal(3, ModifierKeys.Conditions.Count);
        Assert.Equal(20, ModifierKeys.Specs.Count);
    }

    [Fact]
    public void Definitions_copient_les_collections_recues()
    {
        var conditions = new List<ModifierCondition> { ModifierCondition.WhileGuarding };
        var modifier = new ModifierDefinition("damageTaken", new RankedValue(Fixed.FromRaw(-100)), conditions);
        conditions.Add(ModifierCondition.PerRampartCharge);
        conditions[0] = ModifierCondition.PerResonanceStep;
        Assert.Equal(new[] { ModifierCondition.WhileGuarding }, modifier.Conditions);
        Assert.Equal(ModifierConditions.WhileGuarding, modifier.ConditionSet);

        var source = new List<ModifierDefinition> { modifier };
        var talent = new TalentDefinition("talent.essai.a.t", "T", 3, modifiers: source);
        var skill = new SkillDefinition("skill.essai.p", "P", SkillKind.Passive, modifiers: source);
        source.Clear();
        Assert.Same(modifier, Assert.Single(talent.Modifiers));
        Assert.Same(modifier, Assert.Single(skill.Modifiers));
        AssertReadOnly(talent.Modifiers);
        AssertReadOnly(skill.Modifiers);
    }
}
