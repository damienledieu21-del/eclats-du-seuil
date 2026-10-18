using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using static EdS.Domain.Tests.Combat.Lot81Classe;

namespace EdS.Domain.Tests.Combat;

// Sous-lot 8.1 : validation des modificateurs par la classe seule (ClassDefinition.Validate, conception D9 ; D10 fait foi sur D1–D9).
// Correspondance avec la liste obligatoire des tests du sous-lot :
//  [L1] chaque clé au rang 1 et au rang maximal          → Cle_aux_bornes_declarees_aux_rangs_1_et_maximal (théorie sur ModifierKeys.Specs),
//                                                          Drapeau_noUpkeepBreak_vaut_exactement_un_sans_perRank
//  [L2] bornes N−1, N, N+1                               → idem, Talent_au_rang_limite_100, Lot81ModifierLimitsTests (8/9, 512/513, débordement)
//  [L3] combinaisons de modificateurs                    → Meme_cle_*, Toutes_les_cles_ensemble_admises, Ensembles_de_conditions_*
//  [L4] conditions vraies et fausses dans un même combat → hors de portée de la validation (moteur) ; ici, ensembles de conditions admis/refusés
//  [L5] agrégation indépendante de l'ordre               → Validation_independante_de_l_ordre_de_declaration (les 24 permutations)
//  [L6] immuabilité                                      → Lot81ModifierImmutabilityTests
//  [L7] débordements refusés avant simulation            → Lot81ModifierLimitsTests.Debordement_par_rang_refuse_en_limite_numerique
//  [L8] aucune mutation partielle                        → Lot81ModifierImmutabilityTests.Validation_sans_effet_sur_la_classe, Liste_source_modifiee_*
//  [L9] déterminisme et rejeu                            → Lot81ModifierImmutabilityTests.Validation_deterministe
//  [L10] non-régression des 5 scénarios livrés           → Lot81ReferenceTests et Lot81EngineBaselineTests (non refait ici)

/// <summary>
/// Classe d'essai des tests de validation des modificateurs (sous-lot 8.1) et table des clés recopiée de la conception (D2, D10) :
/// les bornes attendues ne sont pas relues dans <see cref="ModifierKeys.Specs"/>, elles viennent du document.
/// </summary>
internal static class Lot81Classe
{
    public static readonly ProgressionRules Regles = ProgressionRules.Provisional;

    public const string Frappe = "skill.essai.frappe";      // skills[0] : active avec bloc combat
    public const string Inerte = "skill.essai.inerte";      // skills[1] : active sans bloc combat
    public const string Ultime = "skill.essai.ultime";      // skills[2] : ultime avec bloc combat
    public const string Passif = "skill.essai.passif";      // skills[3] : passive
    public const string PassifB = "skill.essai.passif_b";   // skills[4] : seconde passive

    public const string CheminAlpha = "trees[0].talents[0].modifiers";
    public const string CheminBeta = "trees[1].talents[0].modifiers";
    public const string CheminPassif = "skills[3].modifiers";
    public const string CheminPassifB = "skills[4].modifiers";

    public const ModifierCondition W = ModifierCondition.WhileGuarding;
    public const ModifierCondition C = ModifierCondition.PerRampartCharge;
    public const ModifierCondition R = ModifierCondition.PerResonanceStep;

    /// <summary>Ligne de la table D2 (bornes resserrées par D10) : clé d'exemple, motif de la spécification, nature, |add| max, conditionnelle.</summary>
    public sealed record Ligne(string Cle, string Motif, ModifierUnit Unite, string Borne, bool Conditionnelle);

    private const string Jour = "86400000";   // 24 h en ms

    public static readonly IReadOnlyDictionary<ModifierField, Ligne> Conception = new Dictionary<ModifierField, Ligne>
    {
        [ModifierField.GuardDamageReduction] = new("guard.damageReduction", "guard.damageReduction", ModifierUnit.Decimal, "1", false),           // D10 ; décision du 18/10/2026 : plafond des règles vérifié au combat (Lot81RulesCapTests)
        [ModifierField.GuardUpkeepPerSecond] = new("guard.upkeepPerSecond", "guard.upkeepPerSecond", ModifierUnit.Decimal, "1000000", false),
        [ModifierField.GuardCostPerBlock] = new("guard.costPerBlock", "guard.costPerBlock", ModifierUnit.Integer, "1000000", false),
        [ModifierField.GuardNoUpkeepBreak] = new("guard.noUpkeepBreak", "guard.noUpkeepBreak", ModifierUnit.Flag, "1", false),
        [ModifierField.GuardReductionAtZeroEndurance] = new("guard.reductionAtZeroEndurance", "guard.reductionAtZeroEndurance", ModifierUnit.Decimal, "1", false),      // idem
        [ModifierField.GuardBreakShortenPerCharge] = new("guard.breakShortenPerCharge", "guard.breakShortenPerCharge", ModifierUnit.Milliseconds, Jour, false),
        [ModifierField.ParryWindowMs] = new("parry.windowMs", "parry.windowMs", ModifierUnit.Milliseconds, "500", false),                       // D10
        [ModifierField.ParryFailureRecoveryMs] = new("parry.failureRecoveryMs", "parry.failureRecoveryMs", ModifierUnit.Milliseconds, Jour, false),
        [ModifierField.RampartChargesMax] = new("rampartCharges.max", "rampartCharges.max", ModifierUnit.Integer, "10", false),                  // D10
        [ModifierField.ResonancePerDirectHit] = new("resonance.perDirectHit", "resonance.perDirectHit", ModifierUnit.Integer, "1000000", false),
        [ModifierField.EnduranceMax] = new("endurance.max", "endurance.max", ModifierUnit.Integer, "1000000", false),
        [ModifierField.EnduranceRegenPerSecond] = new("endurance.regenPerSecond", "endurance.regenPerSecond", ModifierUnit.Decimal, "1000000", false),
        [ModifierField.MaxHealth] = new("maxHealth", "maxHealth", ModifierUnit.Decimal, "10", false),
        [ModifierField.StatusResistance] = new("statusResistance", "statusResistance", ModifierUnit.Decimal, "1000000", false),
        [ModifierField.DamageDealt] = new("damageDealt", "damageDealt", ModifierUnit.Decimal, "10", true),
        [ModifierField.DamageTaken] = new("damageTaken", "damageTaken", ModifierUnit.Decimal, "1", true),
        [ModifierField.StatusDurationDealt] = new("status.burn.durationDealt", "status.<id>.durationDealt", ModifierUnit.Milliseconds, Jour, false),
        [ModifierField.StatusDurationTaken] = new("status.burn.durationTaken", "status.<id>.durationTaken", ModifierUnit.Milliseconds, Jour, false),
        [ModifierField.SkillCoefficient] = new("skill.essai.frappe.coefficient", "skill.<id>.coefficient", ModifierUnit.Decimal, "100", false),
        [ModifierField.SkillCooldownMs] = new("skill.essai.frappe.cooldownMs", "skill.<id>.cooldownMs", ModifierUnit.Milliseconds, Jour, false),
    };

    /// <summary>Ligne de conception d'une clé ; une clé ajoutée à la liste fermée sans être ajoutée ici fait échouer le test qui la lit.</summary>
    public static Ligne De(ModifierField field)
    {
        if (!Conception.TryGetValue(field, out Ligne? ligne))
            Assert.Fail($"clé {field} absente de la table de conception de Lot81Classe : y ajouter sa clé, sa nature et sa borne (D2, D10).");
        return ligne!;
    }

    /// <summary>Valeur admise pour toute clé : 0,01 (décimal), 1 (entier, ms, drapeau), −0,01 pour damageTaken (réduction seulement).</summary>
    public static Fixed ValeurValide(ModifierField field) =>
        field == ModifierField.DamageTaken ? Fixed.Parse("-0.01")
        : De(field).Unite == ModifierUnit.Decimal ? Fixed.Parse("0.01")
        : Fixed.One;

    private static SkillCombatDefinition Combat() => new(PowerType.Physique, DamageType.Physique, Fixed.Parse("1.2"), 10, 6000);

    /// <summary>
    /// Classe d'essai valide sans modificateur : trois arbres d'un talent chacun (alpha de <paramref name="rangAlpha"/> rangs, beta et
    /// gamma de 5), cinq compétences (voir les constantes) et d'éventuelles compétences supplémentaires après elles.
    /// </summary>
    public static ClassDefinition Classe(int rangAlpha = 5, IEnumerable<ModifierDefinition>? alpha = null, IEnumerable<ModifierDefinition>? beta = null,
        IEnumerable<ModifierDefinition>? passif = null, IEnumerable<ModifierDefinition>? passifB = null, IEnumerable<ModifierDefinition>? frappe = null,
        IEnumerable<ModifierDefinition>? inerte = null, IEnumerable<ModifierDefinition>? ultime = null, IEnumerable<SkillDefinition>? autres = null)
    {
        TalentTreeDefinition Arbre(string k, int rang, IEnumerable<ModifierDefinition>? m) =>
            new($"tree.essai.{k}", k, new[] { new TalentDefinition($"talent.essai.{k}.a", "A", rang, modifiers: m) });
        var skills = new List<SkillDefinition>
        {
            new(Frappe, "Frappe", SkillKind.Active, combat: Combat(), modifiers: frappe),
            new(Inerte, "Inerte", SkillKind.Active, modifiers: inerte),
            new(Ultime, "Ultime", SkillKind.Ultimate, minLevel: 10, combat: Combat(), modifiers: ultime),
            new(Passif, "Passif", SkillKind.Passive, modifiers: passif),
            new(PassifB, "Passif B", SkillKind.Passive, modifiers: passifB),
        };
        if (autres != null) skills.AddRange(autres);
        return new ClassDefinition("class.essai", "Essai", new[] { Arbre("alpha", rangAlpha, alpha), Arbre("beta", 5, beta), Arbre("gamma", 5, null) }, skills);
    }

    /// <summary>Modificateurs portés par le talent alpha (chemin <see cref="CheminAlpha"/>), de rang maximal donné.</summary>
    public static IReadOnlyList<RuleViolation> SurTalent(int rangMax, params ModifierDefinition[] modifiers) =>
        Classe(rangAlpha: rangMax, alpha: modifiers).Validate(Regles);

    /// <summary>Modificateurs portés par la compétence passive (chemin <see cref="CheminPassif"/>).</summary>
    public static IReadOnlyList<RuleViolation> SurPassif(params ModifierDefinition[] modifiers) => Classe(passif: modifiers).Validate(Regles);

    /// <summary>Les trois porteurs « au rang 1 » : talent à 1 rang, talent à 5 rangs (base seule), passif.</summary>
    public static IEnumerable<(string Nom, Func<ModifierDefinition, IReadOnlyList<RuleViolation>> Valider, string Chemin)> PorteursAuRang1() =>
        new (string, Func<ModifierDefinition, IReadOnlyList<RuleViolation>>, string)[]
        {
            ("talent à 1 rang", m => SurTalent(1, m), CheminAlpha),
            ("talent à 5 rangs", m => SurTalent(5, m), CheminAlpha),
            ("passif", m => SurPassif(m), CheminPassif),
        };

    /// <summary>Modificateur de test : <c>add</c> (et <c>perRank</c> facultatif) en texte décimal.</summary>
    public static ModifierDefinition M(string key, string add, string? perRank = null, params ModifierCondition[] conditions) =>
        new(key, new RankedValue(Fixed.Parse(add), perRank is null ? Fixed.Zero : Fixed.Parse(perRank)), conditions);

    public static ModifierDefinition Brut(string key, Fixed @base, Fixed perRank = default, IEnumerable<ModifierCondition>? conditions = null) =>
        new(key, new RankedValue(@base, perRank), conditions);

    public static void Admis(IReadOnlyList<RuleViolation> errors, string contexte = "") =>
        Assert.True(errors.Count == 0, $"{contexte} : aucune erreur attendue ; obtenu : {string.Join(" | ", errors)}");

    /// <summary>Exactement une erreur, de nature et de chemin donnés.</summary>
    public static void Refus(IReadOnlyList<RuleViolation> errors, RuleViolationKind kind, string path, string contexte = "")
    {
        Assert.True(errors.Count == 1, $"{contexte} : une erreur {kind} sur {path} attendue ; obtenu : {string.Join(" | ", errors)}");
        Assert.Equal((kind, path), (errors[0].Kind, Assert.Single(errors[0].Fields)));
    }

    public static (RuleViolationKind, string)[] Signatures(IEnumerable<RuleViolation> errors) =>
        errors.Select(e => (e.Kind, string.Join(",", e.Fields))).ToArray();
}

/// <summary>
/// Sous-lot 8.1 : validation de classe des modificateurs (D2, D9, D10) — liste fermée des clés, bornes déclarées aux rangs 1 et
/// maximal, entiers et drapeau, perRank, damageTaken réducteur, références de clé, conditions, doublons et porteurs admis.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Build")]
public class Lot81ModifierValidationTests
{
    private const RuleViolationKind Invalide = RuleViolationKind.InvalidValue;
    private const RuleViolationKind Inconnue = RuleViolationKind.UnknownReference;
    private const RuleViolationKind Doublon = RuleViolationKind.DuplicateId;

    private const string AddAlpha = CheminAlpha + "[0].add";

    /// <summary>Toutes les clés de la liste fermée, lues dans <see cref="ModifierKeys.Specs"/> : une nouvelle clé y entre d'office.</summary>
    public static TheoryData<ModifierField> ToutesLesCles()
    {
        var data = new TheoryData<ModifierField>();
        foreach (ModifierSpec spec in ModifierKeys.Specs) data.Add(spec.Field);
        return data;
    }

    /// <summary>
    /// Clés numériques (tout sauf le drapeau, testé à part) avec le signe des valeurs : |add| ≤ N dans les deux sens, sauf damageTaken
    /// qui n'admet que des valeurs négatives ou nulles (D10). Une clé absente de la table de conception est incluse et fait échouer le cas.
    /// </summary>
    public static TheoryData<ModifierField, int> ClesNumeriques()
    {
        var data = new TheoryData<ModifierField, int>();
        foreach (ModifierSpec spec in ModifierKeys.Specs)
        {
            if (Conception.TryGetValue(spec.Field, out Lot81Classe.Ligne? ligne) && ligne.Unite == ModifierUnit.Flag) continue;
            data.Add(spec.Field, -1);
            if (spec.Field != ModifierField.DamageTaken) data.Add(spec.Field, +1);
        }
        return data;
    }

    /// <summary>Clés entières ou en millisecondes (selon la conception).</summary>
    public static TheoryData<ModifierField> ClesEntieresOuMs()
    {
        var data = new TheoryData<ModifierField>();
        foreach (ModifierSpec spec in ModifierKeys.Specs)
            if (!Conception.TryGetValue(spec.Field, out Lot81Classe.Ligne? ligne) || ligne.Unite is ModifierUnit.Integer or ModifierUnit.Milliseconds)
                data.Add(spec.Field);
        return data;
    }

    [Fact]
    public void Classe_d_essai_valide_sans_modificateur()
    {
        // Témoin de tous les cas « admis » : la classe d'essai seule ne produit aucune erreur.
        Admis(Classe().Validate(Regles));
        Admis(Classe(rangAlpha: 1).Validate(Regles));
        Admis(Classe(rangAlpha: 100).Validate(Regles));
    }

    [Fact]
    public void Liste_fermee_conforme_a_la_conception()
    {
        IReadOnlyList<ModifierSpec> specs = ModifierKeys.Specs;
        Assert.Equal(Enum.GetValues(typeof(ModifierField)).Length, specs.Count);
        Assert.Equal(20, specs.Count);
        Assert.Equal(16, specs.Count(s => !s.Name.Contains("<id>")));   // 16 clés scalaires + 4 paramétrées
        for (int i = 0; i < specs.Count; i++)
        {
            ModifierSpec spec = specs[i];
            Assert.Equal((ModifierField)i, spec.Field);   // indexées par ModifierField (ModifierKey.Spec en dépend)
            Lot81Classe.Ligne ligne = De(spec.Field);
            Assert.Equal((ligne.Motif, ligne.Unite, Fixed.Parse(ligne.Borne), ligne.Conditionnelle), (spec.Name, spec.Unit, spec.MaxMagnitude, spec.Conditional));
            Assert.True(ModifierKeys.TryParse(ligne.Cle, out ModifierKey key), ligne.Cle);
            Assert.Equal(spec.Field, key.Field);
            Assert.Equal(ligne.Cle, key.ToString());   // texte canonique (journal)
        }
        Assert.Equal(new[] { ModifierField.GuardNoUpkeepBreak }, specs.Where(s => s.Unit == ModifierUnit.Flag).Select(s => s.Field));
        Assert.Equal(new[] { ModifierField.DamageDealt, ModifierField.DamageTaken }, specs.Where(s => s.Conditional).Select(s => s.Field));
    }

    [Theory]
    [MemberData(nameof(ClesNumeriques))]
    public void Cle_aux_bornes_declarees_aux_rangs_1_et_maximal(ModifierField field, int signe)
    {
        Lot81Classe.Ligne ligne = De(field);
        string cle = ligne.Cle;
        Fixed n = Fixed.Parse(ligne.Borne) * signe;
        // Un pas brut : 0,0001 pour un décimal, 1 pour un entier ou une durée en ms (le multiple du tick est vérifié au combat, D9).
        Fixed pas = (ligne.Unite == ModifierUnit.Decimal ? Fixed.Epsilon : Fixed.One) * signe;

        // [L1][L2] Rang 1 (base seule) chez chaque porteur : N et N − 1 pas admis, N + 1 pas refusé sur .add.
        foreach (var (nom, valider, chemin) in PorteursAuRang1())
        {
            Admis(valider(Brut(cle, n)), $"{cle} = {n} ({nom})");
            Admis(valider(Brut(cle, n - pas)), $"{cle} = {n - pas} ({nom})");
            Refus(valider(Brut(cle, n + pas)), Invalide, chemin + "[0].add", $"{cle} = {n + pas} ({nom})");
        }

        // [L1][L2] Rang maximal (5) : valeur au rang r = base + perRank × (r − 1), perRank = 1 pas.
        Admis(SurTalent(5, Brut(cle, n - pas * 4, pas)), $"{cle} rang 5 = N");                 // N − 4 pas + 4 pas = N
        Admis(SurTalent(5, Brut(cle, n - pas * 5, pas)), $"{cle} rang 5 = N − 1 pas");
        // Rang maximal seulement : base N − 3 pas (admise au rang 1), rang 5 = N + 1 pas.
        Refus(SurTalent(5, Brut(cle, n - pas * 3, pas)), Invalide, AddAlpha, $"{cle} rang 5 = N + 1 pas");
        // Témoin : le même modificateur sur un talent de 4 rangs vaut N au rang maximal, admis.
        Admis(SurTalent(4, Brut(cle, n - pas * 3, pas)), $"{cle} rang 4 = N");
        // Et la base seule hors bornes est refusée même si perRank la ramène dans l'intervalle au rang maximal.
        Refus(SurTalent(5, Brut(cle, n + pas, -pas)), Invalide, AddAlpha, $"{cle} rang 1 = N + 1 pas");
    }

    [Fact]
    public void Talent_au_rang_limite_100()
    {
        // guard.damageReduction (N = 1 pour la classe seule depuis la décision du 18/10/2026 ; le plafond des règles, 0,75, est vérifié
        // au combat) : base + 99 × perRank.
        Admis(SurTalent(100, M("guard.damageReduction", "0.01", "0.01")));                                             // 0,01 × 100 = 1
        Admis(SurTalent(100, M("guard.damageReduction", "0.0099", "0.01")));                                           // 0,9999
        Refus(SurTalent(100, M("guard.damageReduction", "0.0101", "0.01")), Invalide, AddAlpha);                      // 1,0001
        Refus(SurTalent(100, M("guard.damageReduction", "-0.0101", "-0.01")), Invalide, AddAlpha);                    // −1,0001
        // rampartCharges.max (N = 10, D10), entier : au rang 100, tout perRank non nul (au moins 1) ajoute au moins 99.
        Admis(SurTalent(100, M("rampartCharges.max", "10")));
        Refus(SurTalent(100, M("rampartCharges.max", "0", "1")), Invalide, AddAlpha);                                 // 0 + 99 × 1 = 99
    }

    [Fact]
    public void Drapeau_noUpkeepBreak_vaut_exactement_un_sans_perRank()
    {
        const string cle = "guard.noUpkeepBreak";
        foreach (var (nom, valider, _) in PorteursAuRang1()) Admis(valider(M(cle, "1")), nom);

        foreach (string valeur in new[] { "0", "2", "0.5", "-1", "0.9999", "1.0001" })
        {
            Refus(SurTalent(5, M(cle, valeur)), Invalide, AddAlpha, valeur);
            Refus(SurPassif(M(cle, valeur)), Invalide, CheminPassif + "[0].add", valeur);
        }
        // Tout perRank est refusé, même sur un talent à un seul rang où il serait sans effet.
        foreach (string perRank in new[] { "1", "-1", "0.0001" })
        {
            Refus(SurTalent(5, M(cle, "1", perRank)), Invalide, AddAlpha, perRank);
            Refus(SurTalent(1, M(cle, "1", perRank)), Invalide, AddAlpha, perRank);
        }
        // Sur un passif, perRank hors talent est signalé seul (revue 8.1 : une erreur par valeur), sans l'erreur de drapeau.
        Assert.Equal(new[] { (Invalide, CheminPassif + "[0].add") }, Signatures(SurPassif(M(cle, "1", "1"))));
    }

    [Theory]
    [MemberData(nameof(ClesEntieresOuMs))]
    public void Cle_entiere_ou_ms_refuse_base_ou_perRank_fractionnaire(ModifierField field)
    {
        string cle = De(field).Cle;
        foreach (string valeur in new[] { "1.5", "-1.5", "0.0001", "9.9999" })   // toutes dans les bornes (|v| ≤ 10)
        {
            Refus(SurTalent(5, M(cle, valeur)), Invalide, AddAlpha, valeur);
            Refus(SurPassif(M(cle, valeur)), Invalide, CheminPassif + "[0].add", valeur);
        }
        Refus(SurTalent(5, M(cle, "1", "0.5")), Invalide, AddAlpha);
        Refus(SurTalent(5, M(cle, "1", "-0.0001")), Invalide, AddAlpha);
        // Base et perRank doivent être entiers : 1 + 4 × 0,25 = 2 au rang 5, mais les rangs 2 à 4 seraient fractionnaires.
        Refus(SurTalent(5, M(cle, "1", "0.25")), Invalide, AddAlpha);
        // Témoins entiers.
        Admis(SurTalent(5, M(cle, "1", "1")));
        Admis(SurTalent(5, M(cle, "-3", "-1")));
    }

    [Theory]
    [MemberData(nameof(ClesNumeriques))]
    public void PerRank_refuse_sur_une_competence_passive(ModifierField field, int signe)
    {
        Lot81Classe.Ligne ligne = De(field);
        Fixed v = (ligne.Unite == ModifierUnit.Decimal ? Fixed.Parse("0.01") : Fixed.One) * signe;
        Refus(SurPassif(Brut(ligne.Cle, v, v)), Invalide, CheminPassif + "[0].add");
        Admis(SurPassif(Brut(ligne.Cle, v)));            // même base sans perRank
        Admis(SurTalent(5, Brut(ligne.Cle, v, v)));      // sur un talent : 5 × v au rang 5, dans les bornes de toute clé
    }

    [Theory]
    [MemberData(nameof(ClesNumeriques))]
    public void Valeur_nulle_a_chaque_rang_refusee(ModifierField field, int signe)
    {
        // Revue 8.1 : un modificateur nul à chaque rang est sans effet (il changerait l'empreinte et l'objet rules du journal sans rien
        // changer au combat) ; refusé, une seule erreur sur .add.
        Lot81Classe.Ligne ligne = De(field);
        Fixed v = (ligne.Unite == ModifierUnit.Decimal ? Fixed.Parse("0.01") : Fixed.One) * signe;
        Refus(SurTalent(5, Brut(ligne.Cle, Fixed.Zero)), Invalide, AddAlpha);
        Refus(SurTalent(1, Brut(ligne.Cle, Fixed.Zero)), Invalide, AddAlpha);
        Refus(SurPassif(Brut(ligne.Cle, Fixed.Zero)), Invalide, CheminPassif + "[0].add");
        // Nul au rang 1 seulement : admis (rang 5 : 4 × v).
        Admis(SurTalent(5, Brut(ligne.Cle, Fixed.Zero, v)));
    }

    [Theory]
    [MemberData(nameof(ClesNumeriques))]
    public void PerRank_refuse_sur_un_talent_a_un_seul_rang(ModifierField field, int signe)
    {
        // Revue 8.1 : sur un talent à un seul rang, perRank n'aurait aucun effet (seul le rang 1 existe) mais changerait l'empreinte.
        Lot81Classe.Ligne ligne = De(field);
        Fixed v = (ligne.Unite == ModifierUnit.Decimal ? Fixed.Parse("0.01") : Fixed.One) * signe;
        Refus(SurTalent(1, Brut(ligne.Cle, v, v)), Invalide, AddAlpha);
        Refus(SurTalent(1, Brut(ligne.Cle, v, Fixed.FromInt(99_999))), Invalide, AddAlpha);   // jamais borné : refusé quand même
        Admis(SurTalent(1, Brut(ligne.Cle, v)));
        Admis(SurTalent(2, Brut(ligne.Cle, v, v)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Rang_maximal_invalide_signale_seul(int rangMax)
    {
        // Contre-vérification 8.1 : un rang maximal inférieur à 1 est refusé sur .maxRank. Les contrôles « sans effet » qui dépendent
        // des rangs déclarés (perRank d'un talent à un seul rang, valeur nulle aux rangs déclarés malgré un perRank) n'ajoutent pas
        // une seconde erreur qui n'existe pas. Une valeur nulle sans perRank est nulle à tout rang : elle reste signalée.
        const string RangAlpha = "trees[0].talents[0].maxRank";
        Refus(SurTalent(rangMax, M("damageDealt", "0.1", "0.1")), Invalide, RangAlpha);
        Refus(SurTalent(rangMax, M("damageDealt", "0", "0.1")), Invalide, RangAlpha);
        Assert.Equal(new[] { (Invalide, RangAlpha), (Invalide, AddAlpha) }, Signatures(SurTalent(rangMax, M("damageDealt", "0"))));
        // Vérification finale 8.1 : une valeur avec perRank est examinée aux rangs 1 et 2, le rang 2 existant pour tout rang maximal
        // qui admet un perRank. Une valeur fausse pour tout rang maximal légal est donc signalée dès le premier passage.
        IReadOnlyList<RuleViolation> horsBorne = SurTalent(rangMax, M("damageDealt", "0.1", "1000"));
        Assert.Equal(new[] { (Invalide, RangAlpha), (Invalide, AddAlpha) }, Signatures(horsBorne));
        Assert.Contains("(1 à 2)", horsBorne[1].Message);
        IReadOnlyList<RuleViolation> positif = SurTalent(rangMax, M("damageTaken", "0", "0.1"));
        Assert.Equal(new[] { (Invalide, RangAlpha), (Invalide, AddAlpha) }, Signatures(positif));
        Assert.Contains("damageTaken", positif[1].Message);
        // Rang 2 dans la borne : aucune seconde erreur, même si un rang plus élevé la dépasserait (5 × 3 > 10).
        Refus(SurTalent(rangMax, M("damageDealt", "5", "3")), Invalide, RangAlpha);
    }

    [Fact]
    public void DamageTaken_reduction_seulement()
    {
        const string cle = "damageTaken";
        // Admis : négatif, −1 (borne), perRank négatif, perRank positif tant que la valeur reste ≤ 0 à chaque rang (0 à un rang
        // seulement). Une valeur nulle à chaque rang est sans effet, refusée comme toute clé (revue 8.1).
        Refus(SurTalent(5, M(cle, "0")), Invalide, AddAlpha);
        Admis(SurTalent(5, M(cle, "-0.005")));
        Admis(SurTalent(5, M(cle, "-1")));
        Admis(SurTalent(5, M(cle, "0", "-0.01")));          // rang 5 : −0,04
        Admis(SurTalent(5, M(cle, "-0.04", "0.01")));        // rang 5 : 0 exactement
        Admis(SurPassif(M(cle, "-0.1", null, W)));
        // Refusé (InvalidValue, .add) : positif au rang 1.
        Refus(SurTalent(5, M(cle, "0.0001")), Invalide, AddAlpha);
        Refus(SurTalent(1, M(cle, "0.0001")), Invalide, AddAlpha);
        Refus(SurPassif(M(cle, "0.5")), Invalide, CheminPassif + "[0].add");
        Refus(SurTalent(5, M(cle, "0.01", "-0.01")), Invalide, AddAlpha);           // rang 1 : +0,01, rang 5 : −0,03
        Refus(SurTalent(5, M(cle, "0.01", null, C)), Invalide, AddAlpha);           // conditionnel positif
        // Refusé : positif au rang maximal seulement : −0,04 + 4 × 0,0101 = +0,0004.
        Refus(SurTalent(5, M(cle, "-0.04", "0.0101")), Invalide, AddAlpha);
        Admis(SurTalent(4, M(cle, "-0.04", "0.0101")));                              // rang 4 : −0,04 + 3 × 0,0101 = −0,0097
    }

    [Theory]
    [InlineData("guard.inconnue")]
    [InlineData("")]
    [InlineData("GUARD.DAMAGEREDUCTION")]
    [InlineData("guard.damagereduction")]
    [InlineData(" guard.damageReduction")]
    [InlineData("guard.damageReduction\n")]
    [InlineData("shield.potencyDealt")]          // reportée au sous-lot 8.2 (X4)
    [InlineData("protection.redirectRatio")]     // reportée au sous-lot 8.3 (X6)
    [InlineData("damage")]
    [InlineData("status.burn")]
    [InlineData("status.burn.duration")]
    [InlineData("status..durationDealt")]
    [InlineData("status.durationTaken")]
    [InlineData("skill.essai.frappe")]
    [InlineData("skill.essai.frappe.damage")]
    [InlineData("skill.Essai.frappe.coefficient")]
    [InlineData(".coefficient")]
    [InlineData("talent.essai.alpha.a.coefficient")]
    public void Cle_inconnue_refusee(string cle)
    {
        Refus(SurTalent(5, M(cle, "1")), Inconnue, CheminAlpha + "[0].key");
        Refus(SurPassif(M(cle, "1")), Inconnue, CheminPassif + "[0].key");
    }

    [Theory]
    [InlineData("status.Burn.durationDealt")]
    [InlineData("status.burn_x.durationDealt")]
    [InlineData("status.burn.x.durationTaken")]
    [InlineData("status.1burn.durationTaken")]
    [InlineData("status.bu rn.durationDealt")]
    [InlineData("status.burn-x.durationTaken")]
    [InlineData("status.burn\n.durationDealt")]
    public void Identifiant_d_alteration_mal_forme_refuse(string cle) =>
        Refus(SurTalent(5, M(cle, "1000")), Inconnue, CheminAlpha + "[0].key");

    [Theory]
    [InlineData("status.burn.durationDealt")]
    [InlineData("status.fragility.durationTaken")]
    [InlineData("status.guardBroken.durationTaken")]
    [InlineData("status.taunt.durationDealt")]   // inconnue du catalogue : vérifiée au combat (D9), pas par la classe seule
    public void Identifiant_d_alteration_bien_forme_admis_par_la_classe(string cle) => Admis(SurTalent(5, M(cle, "1000")));

    [Theory]
    [InlineData("skill.essai.inconnue.coefficient")]            // absente de la classe
    [InlineData("skill.essai.inconnue.cooldownMs")]
    [InlineData("skill.essai.inerte.coefficient")]              // active sans bloc combat
    [InlineData("skill.essai.inerte.cooldownMs")]
    [InlineData("skill.essai.passif.coefficient")]              // passive : jamais lancée
    [InlineData("skill.essai.frappe.coefficient.coefficient")]  // « skill.essai.frappe.coefficient » n'est pas une compétence
    [InlineData("skill.autre.frappe.cooldownMs")]
    public void Competence_visee_inconnue_ou_sans_bloc_combat_refusee(string cle)
    {
        Refus(SurTalent(5, M(cle, "1")), Inconnue, CheminAlpha + "[0].key");
        Refus(SurPassif(M(cle, "1")), Inconnue, CheminPassif + "[0].key");
    }

    [Theory]
    [InlineData("skill.essai.frappe.coefficient")]
    [InlineData("skill.essai.frappe.cooldownMs")]
    [InlineData("skill.essai.ultime.coefficient")]
    [InlineData("skill.essai.ultime.cooldownMs")]
    public void Competence_de_la_classe_avec_bloc_combat_admise(string cle)
    {
        Admis(SurTalent(5, M(cle, "1", "1")));
        Admis(SurPassif(M(cle, "-1")));
    }

    [Fact]
    public void Reference_inconnue_et_valeur_hors_bornes_signalees_ensemble()
    {
        // La valeur est contrôlée même quand la compétence visée n'existe pas : chaque erreur porte son chemin.
        Assert.Equal(new[] { (Inconnue, CheminAlpha + "[0].key"), (Invalide, AddAlpha) },
            Signatures(SurTalent(5, M("skill.essai.inconnue.coefficient", "100.0001"))));
    }

    [Theory]
    [MemberData(nameof(ToutesLesCles))]
    public void Conditions_reservees_aux_cles_lues_au_calcul_des_degats(ModifierField field)
    {
        Lot81Classe.Ligne ligne = De(field);
        ModifierDefinition conditionnel = Brut(ligne.Cle, ValeurValide(field), conditions: new[] { W });
        if (ligne.Conditionnelle) Admis(SurTalent(5, conditionnel));
        else Refus(SurTalent(5, conditionnel), Invalide, CheminAlpha + "[0].conditions");
        // Témoin : sans condition, la même valeur est admise pour toute clé.
        Admis(SurTalent(5, Brut(ligne.Cle, ValeurValide(field))));
    }

    private static ModifierCondition[] Ensemble(string codes) => codes.Select(c => c switch
    {
        'W' => W,
        'C' => C,
        'R' => R,
        _ => throw new ArgumentOutOfRangeException(nameof(codes), codes, null),
    }).ToArray();

    private static string ValeurSigne(string cle) => cle == "damageTaken" ? "-0.01" : "0.01";

    [Theory]
    [InlineData("damageDealt", "W")]
    [InlineData("damageDealt", "C")]
    [InlineData("damageDealt", "R")]
    [InlineData("damageDealt", "WC")]
    [InlineData("damageDealt", "WR")]
    [InlineData("damageDealt", "CW")]
    [InlineData("damageDealt", "RW")]
    [InlineData("damageTaken", "W")]
    [InlineData("damageTaken", "C")]
    [InlineData("damageTaken", "R")]
    [InlineData("damageTaken", "WC")]
    [InlineData("damageTaken", "WR")]
    [InlineData("damageTaken", "CW")]
    [InlineData("damageTaken", "RW")]
    public void Ensembles_de_conditions_admis(string cle, string codes)
    {
        Admis(SurTalent(5, M(cle, ValeurSigne(cle), ValeurSigne(cle), Ensemble(codes))));
        Admis(SurPassif(M(cle, ValeurSigne(cle), null, Ensemble(codes))));
    }

    [Theory]
    [InlineData("CR", RuleViolationKind.InvalidValue, "conditions")]       // au plus une condition multiplicative (D10)
    [InlineData("RC", RuleViolationKind.InvalidValue, "conditions")]
    [InlineData("WCR", RuleViolationKind.InvalidValue, "conditions")]
    [InlineData("RWC", RuleViolationKind.InvalidValue, "conditions")]
    [InlineData("WW", RuleViolationKind.DuplicateId, "conditions[1]")]
    [InlineData("CC", RuleViolationKind.DuplicateId, "conditions[1]")]
    [InlineData("RR", RuleViolationKind.DuplicateId, "conditions[1]")]
    [InlineData("WCW", RuleViolationKind.DuplicateId, "conditions[2]")]
    public void Ensembles_de_conditions_refuses(string codes, RuleViolationKind kind, string champ)
    {
        foreach (string cle in new[] { "damageDealt", "damageTaken" })
        {
            Refus(SurTalent(5, M(cle, ValeurSigne(cle), null, Ensemble(codes))), kind, $"{CheminAlpha}[0].{champ}", cle);
            Refus(SurPassif(M(cle, ValeurSigne(cle), null, Ensemble(codes))), kind, $"{CheminPassif}[0].{champ}", cle);
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-1)]
    [InlineData(99)]
    public void Condition_hors_enumeration_refusee(int brute)
    {
        var inconnue = (ModifierCondition)brute;
        Refus(SurTalent(5, Brut("damageDealt", Fixed.Parse("0.01"), conditions: new[] { inconnue })), Invalide, CheminAlpha + "[0].conditions[0]");
        Refus(SurTalent(5, Brut("damageTaken", Fixed.Parse("-0.01"), conditions: new[] { W, inconnue })), Invalide, CheminAlpha + "[0].conditions[1]");
    }

    [Fact]
    public void Liste_de_conditions_vide_equivaut_a_absente()
    {
        var vide = new ModifierDefinition("guard.damageReduction", new RankedValue(Fixed.Parse("0.1")), Array.Empty<ModifierCondition>());
        var absente = new ModifierDefinition("guard.damageReduction", new RankedValue(Fixed.Parse("0.2")));
        Assert.Empty(vide.Conditions);
        Assert.Equal(ModifierConditions.None, vide.ConditionSet);
        Assert.Equal(ModifierConditions.None, absente.ConditionSet);
        // Clé non conditionnelle : une liste vide n'est pas une condition.
        Admis(SurTalent(5, vide));
        // Même clé, même ensemble (vide) : doublon.
        Refus(SurTalent(5, absente, vide), Doublon, CheminAlpha + "[1].key");
        // damageDealt : vide (permanent) et {whileGuarding} sont deux ensembles différents.
        Admis(SurTalent(5, new ModifierDefinition("damageDealt", new RankedValue(Fixed.Parse("0.1")), new List<ModifierCondition>()), M("damageDealt", "0.1", null, W)));
    }

    [Fact]
    public void Doublon_meme_cle_memes_conditions_chez_un_meme_porteur()
    {
        Refus(SurTalent(5, M("guard.damageReduction", "0.1"), M("guard.damageReduction", "0.2")), Doublon, CheminAlpha + "[1].key");
        Refus(SurPassif(M("parry.windowMs", "100"), M("parry.windowMs", "100")), Doublon, CheminPassif + "[1].key");
        // Ensemble de conditions : l'ordre de déclaration n'en fait pas un autre ensemble.
        Refus(SurTalent(5, M("damageDealt", "0.1", null, W, C), M("damageDealt", "0.2", null, C, W)), Doublon, CheminAlpha + "[1].key");
        Refus(SurTalent(5, M("damageTaken", "-0.1", null, R), M("damageTaken", "-0.2", null, R)), Doublon, CheminAlpha + "[1].key");
        // Clés paramétrées : même altération ou même compétence.
        Refus(SurTalent(5, M("status.burn.durationDealt", "500"), M("status.burn.durationDealt", "1000")), Doublon, CheminAlpha + "[1].key");
        Refus(SurTalent(5, M("skill.essai.frappe.coefficient", "0.1"), M("skill.essai.frappe.coefficient", "0.1")), Doublon, CheminAlpha + "[1].key");
        // Trois fois : chaque répétition est signalée, la première occurrence reste la référence.
        Assert.Equal(new[] { (Doublon, CheminAlpha + "[1].key"), (Doublon, CheminAlpha + "[2].key") },
            Signatures(SurTalent(5, M("maxHealth", "0.02"), M("maxHealth", "0.02"), M("maxHealth", "0.02"))));
        // Le doublon sur le second passif porte son propre chemin.
        Refus(Classe(passifB: new[] { M("statusResistance", "1"), M("endurance.max", "5"), M("statusResistance", "2") }).Validate(Regles),
            Doublon, CheminPassifB + "[2].key");
    }

    [Fact]
    public void Meme_cle_avec_d_autres_conditions_ou_d_autres_parametres_admise()
    {
        // damageDealt : permanent et les cinq ensembles admis, six lignes chez un même porteur ; idem damageTaken.
        Admis(SurTalent(5, M("damageDealt", "0.1"), M("damageDealt", "0.1", null, W), M("damageDealt", "0.1", null, C), M("damageDealt", "0.1", null, R),
            M("damageDealt", "0.1", null, W, C), M("damageDealt", "0.1", null, W, R)));
        Admis(SurPassif(M("damageTaken", "-0.1"), M("damageTaken", "-0.1", null, W), M("damageTaken", "-0.1", null, C), M("damageTaken", "-0.1", null, R),
            M("damageTaken", "-0.1", null, W, C), M("damageTaken", "-0.1", null, W, R)));
        // Clés paramétrées distinctes.
        Admis(SurTalent(5, M("status.burn.durationDealt", "500"), M("status.burn.durationTaken", "500"), M("status.bleed.durationDealt", "500"),
            M("skill.essai.frappe.coefficient", "0.1"), M("skill.essai.frappe.cooldownMs", "-1000"), M("skill.essai.ultime.coefficient", "0.1")));
    }

    [Fact]
    public void Meme_cle_sur_des_porteurs_differents_admise()
    {
        // Chacun à sa borne : la somme (4 × 0,75 = 3) dépasse le plafond, elle sera écrêtée au combat (D2), jamais une erreur de classe.
        var m = new[] { M("guard.damageReduction", "0.75"), M("damageDealt", "10", null, W), M("rampartCharges.max", "10") };
        Admis(Classe(alpha: m, beta: m, passif: m, passifB: m).Validate(Regles));
        Admis(Classe(rangAlpha: 4, alpha: new[] { M("guard.damageReduction", "0.6", "0.05") }, beta: new[] { M("guard.damageReduction", "0.75") }).Validate(Regles));
    }

    [Fact]
    public void Toutes_les_cles_ensemble_admises()
    {
        // [L3] Les 20 clés de la liste fermée, une fois chacune, réparties sur trois porteurs (au plus 8 chacun).
        ModifierDefinition[] toutes = ModifierKeys.Specs.Select(s => Brut(De(s.Field).Cle, ValeurValide(s.Field))).ToArray();
        Assert.Equal(20, toutes.Length);
        Admis(Classe(alpha: toutes.Take(8), beta: toutes.Skip(8).Take(8), passif: toutes.Skip(16)).Validate(Regles));
        // Et chacune à sa borne (−1 pour damageTaken), sauf le drapeau qui vaut 1.
        ModifierDefinition[] bornes = ModifierKeys.Specs.Select(s => Brut(De(s.Field).Cle,
            Fixed.Parse(De(s.Field).Borne) * (s.Field == ModifierField.DamageTaken ? -1 : 1))).ToArray();
        Admis(Classe(alpha: bornes.Take(8), beta: bornes.Skip(8).Take(8), passif: bornes.Skip(16)).Validate(Regles));
    }

    [Fact]
    public void Modificateurs_refuses_sur_une_active_ou_une_ultime()
    {
        var m = new[] { M("guard.damageReduction", "0.1") };
        Refus(Classe(frappe: m).Validate(Regles), Invalide, "skills[0].modifiers");
        Refus(Classe(inerte: m).Validate(Regles), Invalide, "skills[1].modifiers");
        Refus(Classe(ultime: m).Validate(Regles), Invalide, "skills[2].modifiers");
        // Contenu non examiné : une seule erreur, sur la liste, même si elle est invalide par ailleurs (9 clés inconnues, perRank, positif).
        Refus(Classe(frappe: Enumerable.Repeat(M("cle.inconnue", "1"), 9)).Validate(Regles), Invalide, "skills[0].modifiers");
        Refus(Classe(ultime: new[] { M("damageTaken", "0.5", "0.1", C, R) }).Validate(Regles), Invalide, "skills[2].modifiers");
        // Liste vide : rien à refuser.
        Admis(Classe(frappe: Array.Empty<ModifierDefinition>(), ultime: new List<ModifierDefinition>()).Validate(Regles));
    }

    private static IEnumerable<int[]> Permutations(int n)
    {
        if (n == 0)
        {
            yield return Array.Empty<int>();
            yield break;
        }
        foreach (int[] reste in Permutations(n - 1))
            for (int position = 0; position <= reste.Length; position++)
            {
                var p = new List<int>(reste);
                p.Insert(position, n - 1);
                yield return p.ToArray();
            }
    }

    [Fact]
    public void Validation_independante_de_l_ordre_de_declaration()
    {
        // [L5] Les 24 ordres de quatre modificateurs : le verdict ne dépend que du contenu, chaque erreur suit son élément.
        ModifierDefinition a = M("guard.damageReduction", "0.1");
        ModifierDefinition b = M("parry.windowMs", "501");            // N + 1 (D10 : 500 ms)
        ModifierDefinition c = M("guard.damageReduction", "0.2");     // même clé que a, mêmes conditions (aucune)
        ModifierDefinition d = M("guard.inconnue", "1");
        ModifierDefinition[] valides = { M("guard.damageReduction", "0.75"), M("parry.windowMs", "500"), M("damageDealt", "0.1", null, W), M("status.burn.durationDealt", "1000") };
        ModifierDefinition[] invalides = { a, b, c, d };
        int vus = 0;
        foreach (int[] ordre in Permutations(4))
        {
            vus++;
            Admis(SurTalent(5, ordre.Select(i => valides[i]).ToArray()), string.Join(",", ordre));
            ModifierDefinition[] liste = ordre.Select(i => invalides[i]).ToArray();
            int Position(ModifierDefinition m) => Array.IndexOf(liste, m);
            var attendu = new[]
            {
                (Invalide, $"{CheminAlpha}[{Position(b)}].add"),
                (Inconnue, $"{CheminAlpha}[{Position(d)}].key"),
                (Doublon, $"{CheminAlpha}[{Math.Max(Position(a), Position(c))}].key"),   // la seconde occurrence, quelle qu'elle soit
            };
            Assert.Equal(attendu.OrderBy(x => x.Item2, StringComparer.Ordinal), Signatures(SurTalent(5, liste)).OrderBy(x => x.Item2, StringComparer.Ordinal));
        }
        Assert.Equal(24, vus);
    }
}

/// <summary>
/// Sous-lot 8.1 : plafonds de volume des modificateurs (8 par porteur, 512 par classe, signalé seul avec les plafonds globaux) et
/// débordement des valeurs par rang, refusé en NumericLimit par la validation, avant toute simulation.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Limits")]
public class Lot81ModifierLimitsTests
{
    /// <summary>Huit modificateurs valides et distincts (aucun doublon), dont le drapeau.</summary>
    private static ModifierDefinition[] Huit() => new[]
    {
        M("guard.damageReduction", "0.01"), M("guard.upkeepPerSecond", "0.01"), M("guard.costPerBlock", "1"), M("guard.noUpkeepBreak", "1"),
        M("guard.reductionAtZeroEndurance", "0.01"), M("guard.breakShortenPerCharge", "1"), M("parry.windowMs", "1"), M("parry.failureRecoveryMs", "1"),
    };

    private static ModifierDefinition Neuvieme() => M("rampartCharges.max", "1");

    private static ModifierDefinition[] Neuf() => Huit().Append(Neuvieme()).ToArray();

    private static IEnumerable<SkillDefinition> Passifs(int nombre, Func<ModifierDefinition[]>? modifiers = null) =>
        Enumerable.Range(0, nombre).Select(i => new SkillDefinition($"skill.essai.volume_{i}", "Volume", SkillKind.Passive, modifiers: (modifiers ?? Huit)()));

    [Fact]
    public void Huit_modificateurs_par_porteur_admis_neuf_refuses()
    {
        Admis(SurTalent(5, Huit()));
        Admis(SurPassif(Huit()));
        IReadOnlyList<RuleViolation> talent = SurTalent(5, Neuf());
        Refus(talent, RuleViolationKind.LimitExceeded, CheminAlpha);
        Assert.StartsWith("9 modificateurs", talent[0].Message);
        Refus(SurPassif(Neuf()), RuleViolationKind.LimitExceeded, CheminPassif);
        // Plafond par porteur : huit chez chacun de quatre porteurs (32 dans la classe) est admis.
        Admis(Classe(alpha: Huit(), beta: Huit(), passif: Huit(), passifB: Huit()).Validate(Regles));
        // Neuf chez deux porteurs : une erreur pour chacun, sur sa liste.
        Assert.Equal(new[] { (RuleViolationKind.LimitExceeded, CheminBeta), (RuleViolationKind.LimitExceeded, CheminPassifB) },
            Signatures(Classe(alpha: Huit(), beta: Neuf(), passifB: Neuf()).Validate(Regles)));
    }

    private static void Refus513(ClassDefinition definition)
    {
        IReadOnlyList<RuleViolation> errors = definition.Validate(Regles);
        Refus(errors, RuleViolationKind.LimitExceeded, "skills");   // plafond global : seul signalé, rien d'autre n'est examiné
        Assert.StartsWith("513 modificateurs", errors[0].Message);
    }

    [Fact]
    public void Cinq_cent_douze_modificateurs_par_classe_admis_513_refuses_seuls()
    {
        Assert.Equal(512, ClassDefinition.MaxModifiers);
        // 64 passifs × 8 = 512 : admis.
        Admis(Classe(autres: Passifs(64)).Validate(Regles));
        // Talents et compétences comptent ensemble : 8 (alpha) + 8 (beta) + 62 × 8 = 512.
        Admis(Classe(alpha: Huit(), beta: Huit(), autres: Passifs(62)).Validate(Regles));
        // 513e sur un talent.
        Refus513(Classe(alpha: new[] { Neuvieme() }, autres: Passifs(64)));
        // 513e sur le passif, avec 16 portés par des talents.
        Refus513(Classe(alpha: Huit(), beta: Huit(), passif: new[] { Neuvieme() }, autres: Passifs(62)));
        // 513e sur une active (refusée par ailleurs) : elle compte, et seul le plafond global est signalé.
        Refus513(Classe(frappe: new[] { Neuvieme() }, autres: Passifs(64)));
        // 57 passifs × 9 = 513 : aucun des 57 dépassements par porteur n'est signalé.
        Refus513(Classe(autres: Passifs(57, Neuf)));
        // Autre erreur présente (clé inconnue) : non examinée.
        Refus513(Classe(alpha: new[] { M("cle.inconnue", "1") }, autres: Passifs(64)));
    }

    [Fact]
    public void Debordement_par_rang_refuse_en_limite_numerique()
    {
        const RuleViolationKind limite = RuleViolationKind.NumericLimit;
        string add = CheminAlpha + "[0].add";
        // perRank × (5 − 1) déborde 64 bits.
        Refus(SurTalent(5, Brut("guard.damageReduction", Fixed.Zero, Fixed.MaxValue)), limite, add);
        Refus(SurTalent(5, Brut("damageTaken", Fixed.Zero, Fixed.MinValue)), limite, add);
        // base + perRank déborde (rang 2), dans les deux sens.
        Refus(SurTalent(2, Brut("endurance.max", Fixed.MaxValue, Fixed.One)), limite, add);
        Refus(SurTalent(2, Brut("endurance.max", Fixed.MinValue, -Fixed.One)), limite, add);
        // Frontière au rang 100 : perRank × 99 avec P = ⌊(2⁶³ − 1) / 99⌋ = 93 165 374 109 644 200 (reste 7).
        // P et −P ne débordent pas (valeur hors bornes : InvalidValue) ; P + 1 et −(P + 1) débordent ((P + 1) × 99 = 2⁶³ + 91).
        const long p = 93_165_374_109_644_200;
        Assert.Equal(long.MaxValue / 99, p);
        Refus(SurTalent(100, Brut("damageDealt", Fixed.Zero, Fixed.FromRaw(p))), RuleViolationKind.InvalidValue, add);
        Refus(SurTalent(100, Brut("damageDealt", Fixed.Zero, Fixed.FromRaw(-p))), RuleViolationKind.InvalidValue, add);
        Refus(SurTalent(100, Brut("damageDealt", Fixed.Zero, Fixed.FromRaw(p + 1))), limite, add);
        Refus(SurTalent(100, Brut("damageDealt", Fixed.Zero, Fixed.FromRaw(-p - 1))), limite, add);
        // Valeur extrême sans débordement au rang 1 : simple valeur hors bornes.
        Refus(SurTalent(5, Brut("maxHealth", Fixed.MaxValue)), RuleViolationKind.InvalidValue, add);
        Refus(SurTalent(5, Brut("maxHealth", Fixed.MinValue)), RuleViolationKind.InvalidValue, add);
    }
}

/// <summary>
/// Sous-lot 8.1 : immuabilité des modificateurs et des listes qui les portent (copies défensives, vues en lecture seule), validation
/// sans effet sur la classe et déterministe (même classe ou classe reconstruite à l'identique : mêmes erreurs, même ordre).
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Regression")]
public class Lot81ModifierImmutabilityTests
{
    /// <summary>Vue en lecture seule : ni tableau ni liste interne, et toute écriture par transtypage en <see cref="IList{T}"/> échoue.</summary>
    private static void LectureSeule<T>(IReadOnlyList<T> list, T element)
    {
        Assert.False(list is T[], "tableau interne exposé");
        Assert.False(list is List<T>, "liste interne exposée");
        IList<T> writable = Assert.IsAssignableFrom<IList<T>>(list);
        Assert.True(writable.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => writable.Add(element));
        Assert.Throws<NotSupportedException>(() => writable.Clear());
        if (writable.Count > 0) Assert.Throws<NotSupportedException>(() => writable[0] = element);
    }

    [Fact]
    public void Conditions_copiees_a_la_construction_et_en_lecture_seule()
    {
        var source = new List<ModifierCondition> { C, W };
        var m = new ModifierDefinition("damageTaken", new RankedValue(Fixed.Parse("-0.01")), source);
        source[0] = R;
        source.Add(R);
        Assert.Equal(new[] { C, W }, m.Conditions);   // ordre déclaré conservé, mutation de la source sans effet
        Assert.Equal(ModifierConditions.PerRampartCharge | ModifierConditions.WhileGuarding, m.ConditionSet);
        LectureSeule(m.Conditions, R);
        Assert.Equal(new[] { C, W }, m.Conditions);
        // Sans condition : liste vide, en lecture seule elle aussi.
        LectureSeule(new ModifierDefinition("maxHealth", new RankedValue(Fixed.One)).Conditions, W);
    }

    [Fact]
    public void Listes_de_modificateurs_des_talents_et_competences_copiees()
    {
        ModifierDefinition a = M("guard.damageReduction", "0.1"), b = M("damageDealt", "0.1", null, W), intrus = M("cle.inconnue", "1");
        var source = new List<ModifierDefinition> { a, b };
        var talent = new TalentDefinition("talent.essai.alpha.a", "A", 5, modifiers: source);
        var passif = new SkillDefinition(Passif, "Passif", SkillKind.Passive, modifiers: source);
        source[0] = intrus;
        source.Add(intrus);
        foreach (IReadOnlyList<ModifierDefinition> exposee in new[] { talent.Modifiers, passif.Modifiers })
        {
            Assert.Equal(2, exposee.Count);
            Assert.Same(a, exposee[0]);
            Assert.Same(b, exposee[1]);
            LectureSeule(exposee, intrus);
            Assert.Equal(2, exposee.Count);
        }
        // Sans modificateurs : liste vide en lecture seule.
        LectureSeule(new TalentDefinition("talent.essai.alpha.b", "B", 1).Modifiers, a);
        LectureSeule(new SkillDefinition(Frappe, "Frappe", SkillKind.Active).Modifiers, a);
    }

    [Fact]
    public void Liste_source_modifiee_apres_coup_sans_effet_sur_la_validation()
    {
        // Source valide, rendue invalide après la construction : la classe reste valide.
        var alpha = new List<ModifierDefinition> { M("guard.damageReduction", "1") };
        var passif = new List<ModifierDefinition> { M("parry.windowMs", "500") };
        ClassDefinition valide = Classe(alpha: alpha, passif: passif);
        alpha[0] = M("guard.damageReduction", "1.0001");
        passif.Add(M("parry.windowMs", "500"));
        Admis(valide.Validate(Regles));
        // Source invalide, corrigée après la construction : la classe reste invalide, avec la même erreur.
        var fautive = new List<ModifierDefinition> { M("guard.damageReduction", "1.0001") };
        ClassDefinition invalide = Classe(alpha: fautive);
        fautive[0] = M("guard.damageReduction", "1");
        Refus(invalide.Validate(Regles), RuleViolationKind.InvalidValue, CheminAlpha + "[0].add");
    }

    [Fact]
    public void Constructeurs_refusent_cle_ou_element_nuls()
    {
        Assert.Throws<ArgumentNullException>(() => new ModifierDefinition(null!, new RankedValue(Fixed.One)));
        Assert.Throws<ArgumentException>(() => new TalentDefinition("talent.essai.alpha.a", "A", 5, modifiers: new ModifierDefinition[] { null! }));
        Assert.Throws<ArgumentException>(() => new SkillDefinition(Passif, "Passif", SkillKind.Passive, modifiers: new[] { M("maxHealth", "0.1"), null! }));
    }

    [Fact]
    public void Liste_fermee_et_conditions_exposees_en_lecture_seule()
    {
        LectureSeule(ModifierKeys.Specs, ModifierKeys.Specs[0]);
        Assert.False(ModifierKeys.Conditions is Dictionary<string, ModifierCondition>, "dictionnaire interne exposé");
        IDictionary<string, ModifierCondition> conditions = Assert.IsAssignableFrom<IDictionary<string, ModifierCondition>>(ModifierKeys.Conditions);
        Assert.Throws<NotSupportedException>(() => conditions["intrus"] = W);
        Assert.Equal(new[] { "perRampartCharge", "perResonanceStep", "whileGuarding" }, ModifierKeys.Conditions.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    /// <summary>Classe à cinq erreurs connues, reconstruite à l'identique à chaque appel.</summary>
    private static ClassDefinition Fautive() => Classe(
        alpha: new[] { M("guard.damageReduction", "1.1"), M("inconnue", "1"), M("guard.damageReduction", "0.1") },
        frappe: new[] { M("damageDealt", "0.1") },
        passif: new[] { M("damageTaken", "0.01") });

    [Fact]
    public void Validation_deterministe()
    {
        // [L9] Même classe validée deux fois, et classe reconstruite : mêmes erreurs (nature, chemin, message), dans le même ordre,
        // celui des données (talents dans l'ordre des arbres, puis compétences).
        ClassDefinition definition = Fautive();
        IReadOnlyList<RuleViolation> premiere = definition.Validate(Regles);
        string[] Texte(IEnumerable<RuleViolation> errors) => errors.Select(e => $"{e.Kind}|{string.Join(",", e.Fields)}|{e.Message}").ToArray();
        Assert.Equal(Texte(premiere), Texte(definition.Validate(Regles)));
        Assert.Equal(Texte(premiere), Texte(Fautive().Validate(Regles)));
        Assert.Equal(new[]
        {
            (RuleViolationKind.InvalidValue, CheminAlpha + "[0].add"),           // 1,1 > 1 (fraction)
            (RuleViolationKind.UnknownReference, CheminAlpha + "[1].key"),
            (RuleViolationKind.DuplicateId, CheminAlpha + "[2].key"),
            (RuleViolationKind.InvalidValue, "skills[0].modifiers"),             // active
            (RuleViolationKind.InvalidValue, CheminPassif + "[0].add"),          // damageTaken positif
        }, Signatures(premiere));
    }

    [Fact]
    public void Validation_sans_effet_sur_la_classe()
    {
        // [L8] Une validation qui échoue ne touche à rien : mêmes listes, mêmes éléments, mêmes valeurs.
        ClassDefinition definition = Fautive();
        ModifierDefinition[] avant = definition.Trees.SelectMany(t => t.Talents).SelectMany(t => t.Modifiers)
            .Concat(definition.Skills.SelectMany(s => s.Modifiers)).ToArray();
        var valeurs = avant.Select(m => (m.Key, m.Add, string.Join(",", m.Conditions))).ToArray();
        Assert.NotEmpty(definition.Validate(Regles));
        ModifierDefinition[] apres = definition.Trees.SelectMany(t => t.Talents).SelectMany(t => t.Modifiers)
            .Concat(definition.Skills.SelectMany(s => s.Modifiers)).ToArray();
        Assert.Equal(avant.Length, apres.Length);
        for (int i = 0; i < avant.Length; i++) Assert.Same(avant[i], apres[i]);
        Assert.Equal(valeurs, apres.Select(m => (m.Key, m.Add, string.Join(",", m.Conditions))));
    }
}
