using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Validation;
using EdS.Domain;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;

namespace EdS.Data.Rules
{
    /// <summary>
    /// Validateur commun à tous les adaptateurs. Il vérifie l'en-tête (versions, type de fichier), la présence et la
    /// forme de chaque paramètre, puis délègue la cohérence de gameplay aux règles du domaine
    /// (<see cref="StatRules.Validate"/>, <see cref="DamageRules.Validate"/>), dont il rattache les erreurs au bon
    /// chemin du fichier. Il n'existe donc qu'une seule validation de gameplay.
    /// </summary>
    public static class RulesValidator
    {
        // SemVer 2.0 : MAJEUR.MINEUR.CORRECTIF, suffixe de pré-version et métadonnées facultatifs.
        private static readonly Regex SemVer = new Regex(
            @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$",
            RegexOptions.CultureInvariant);

        /// <param name="document">Contenu lu par un adaptateur.</param>
        /// <param name="report">Rapport de l'adaptateur (erreurs de syntaxe et de type) à compléter ; nouveau si null.</param>
        public static RulesLoadResult Validate(RulesDocument document, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            report ??= new ValidationReport();

            ValidateHeader(document, report);
            ValidateFieldsShape(document, report);

            if (!report.IsValid) return new RulesLoadResult(report, null);

            StatRules stats = BuildStats(document);
            DamageRules damage = BuildDamage(document);
            AddViolations(report, typeof(StatRules), stats.Validate());
            AddViolations(report, typeof(DamageRules), damage.Validate());
            if (!report.IsValid) return new RulesLoadResult(report, null);

            int schemaVersion = document.SchemaVersion!.Value;
            var rules = new LoadedRules(schemaVersion, document.ContentVersion!, RulesFingerprint.Compute(schemaVersion, document), stats, damage);
            return new RulesLoadResult(report, rules);
        }

        private static void ValidateHeader(RulesDocument document, ValidationReport report)
        {
            if (document.SchemaVersion is null)
            {
                if (!report.HasErrorAtOrAbove("$.schemaVersion"))
                    report.Add("$.schemaVersion", ValidationCode.MissingField, "champ obligatoire absent (version du format, actuellement 1).");
            }
            else if (document.SchemaVersion != DataFormat.CurrentSchemaVersion)
            {
                report.Add("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion,
                    $"version de format {document.SchemaVersion} non prise en charge ; ce programme lit la version {DataFormat.CurrentSchemaVersion}.");
            }

            if (document.Kind is null)
            {
                if (!report.HasErrorAtOrAbove("$.kind"))
                    report.Add("$.kind", ValidationCode.MissingField, $"champ obligatoire absent (valeur attendue : « {RulesCatalog.Kind} »).");
            }
            else if (document.Kind != RulesCatalog.Kind)
            {
                report.Add("$.kind", ValidationCode.WrongKind, $"fichier de type « {document.Kind} », « {RulesCatalog.Kind} » attendu.");
            }

            if (document.ContentVersion is null)
            {
                if (!report.HasErrorAtOrAbove("$.contentVersion"))
                    report.Add("$.contentVersion", ValidationCode.MissingField, "champ obligatoire absent (version des valeurs, ex. « 0.1.0-provisoire »).");
            }
            else if (!SemVer.IsMatch(document.ContentVersion))
            {
                report.Add("$.contentVersion", ValidationCode.InvalidContentVersion,
                    $"« {document.ContentVersion} » n'est pas une version SemVer (ex. « 0.1.0 » ou « 0.1.0-provisoire »).");
            }
        }

        private static void ValidateFieldsShape(RulesDocument document, ValidationReport report)
        {
            foreach (RuleField field in RulesCatalog.Fields)
            {
                if (!document.Values.TryGetValue(field.Path, out RuleValue value))
                {
                    if (!report.HasErrorAtOrAbove(field.JsonPath))
                        report.Add(field.JsonPath, ValidationCode.MissingField, $"champ obligatoire absent ({Describe(field)}).");
                    continue;
                }

                switch (field.Kind)
                {
                    case RuleValueKind.Decimal:
                        if (value.Kind != RuleValueKind.Decimal)
                            report.Add(field.JsonPath, ValidationCode.WrongType, "nombre décimal attendu.");
                        break;
                    case RuleValueKind.Int32:
                    case RuleValueKind.Int64:
                        if (value.Kind != RuleValueKind.Int64 && value.Kind != RuleValueKind.Int32)
                            report.Add(field.JsonPath, ValidationCode.WrongType, "nombre entier attendu.");
                        else if (field.Kind == RuleValueKind.Int32 && (value.Integer < int.MinValue || value.Integer > int.MaxValue))
                            report.Add(field.JsonPath, ValidationCode.OutOfRange, $"{value.Integer} dépasse la plage d'un entier 32 bits.");
                        break;
                    case RuleValueKind.Token:
                        if (value.Kind != RuleValueKind.Token)
                            report.Add(field.JsonPath, ValidationCode.WrongType, "identifiant texte attendu.");
                        else if (!field.AllowedTokens.Contains(value.Token!))
                            report.Add(field.JsonPath, ValidationCode.UnknownReference,
                                $"valeur inconnue « {value.Token} » ; valeurs possibles : {string.Join(", ", field.AllowedTokens)}.");
                        break;
                }
            }

            foreach (string path in document.Values.Keys)
                if (!RulesCatalog.TryGetField(path, out _))
                    report.Add("$.data." + path, ValidationCode.UnknownField, "paramètre inconnu.");
        }

        private static string Describe(RuleField field) =>
            field.Kind switch
            {
                RuleValueKind.Decimal => "nombre décimal",
                RuleValueKind.Int32 => "entier",
                RuleValueKind.Int64 => "entier",
                _ => "un parmi : " + string.Join(", ", field.AllowedTokens),
            };

        private static void AddViolations(ValidationReport report, Type owner, IReadOnlyList<RuleViolation> violations)
        {
            foreach (RuleViolation violation in violations)
            {
                string[] paths = violation.Fields.Select(f => RulesCatalog.FieldFor(owner, f).JsonPath).ToArray();
                foreach (string path in paths)
                {
                    string related = paths.Length > 1 ? $" (avec {string.Join(", ", paths.Where(p => p != path))})" : string.Empty;
                    report.Add(path, ValidationCode.InvalidValue, violation.Message + related);
                }
            }
        }

        private static Fixed Dec(RulesDocument d, string path) => d.Values[path].Decimal;
        private static long Int(RulesDocument d, string path) => d.Values[path].Integer;
        private static string P(Type owner, string property) => RulesCatalog.FieldFor(owner, property).Path;

        private static StatRules BuildStats(RulesDocument d)
        {
            Type s = typeof(StatRules);
            return new StatRules
            {
                BasePhysicalPower = Dec(d, P(s, nameof(StatRules.BasePhysicalPower))),
                BaseAgilePower = Dec(d, P(s, nameof(StatRules.BaseAgilePower))),
                BaseMagicPower = Dec(d, P(s, nameof(StatRules.BaseMagicPower))),
                BaseArmor = Dec(d, P(s, nameof(StatRules.BaseArmor))),
                BaseMagicResistance = Dec(d, P(s, nameof(StatRules.BaseMagicResistance))),
                BaseMaxHealth = Dec(d, P(s, nameof(StatRules.BaseMaxHealth))),
                BaseMaxEndurance = Dec(d, P(s, nameof(StatRules.BaseMaxEndurance))),
                BaseStatusResistance = Dec(d, P(s, nameof(StatRules.BaseStatusResistance))),
                PhysicalPowerPerForce = Dec(d, P(s, nameof(StatRules.PhysicalPowerPerForce))),
                AgilePowerPerDexterite = Dec(d, P(s, nameof(StatRules.AgilePowerPerDexterite))),
                MagicPowerPerIntelligence = Dec(d, P(s, nameof(StatRules.MagicPowerPerIntelligence))),
                MaxHealthPerVitalite = Dec(d, P(s, nameof(StatRules.MaxHealthPerVitalite))),
                StatusResistancePerVolonte = Dec(d, P(s, nameof(StatRules.StatusResistancePerVolonte))),
                MaxEndurancePerEndurance = Dec(d, P(s, nameof(StatRules.MaxEndurancePerEndurance))),
                BaseCriticalChanceBasisPoints = (int)Int(d, P(s, nameof(StatRules.BaseCriticalChanceBasisPoints))),
                MinCriticalChanceBasisPoints = (int)Int(d, P(s, nameof(StatRules.MinCriticalChanceBasisPoints))),
                MaxCriticalChanceBasisPoints = (int)Int(d, P(s, nameof(StatRules.MaxCriticalChanceBasisPoints))),
                BaseCriticalMultiplier = Dec(d, P(s, nameof(StatRules.BaseCriticalMultiplier))),
                MinCriticalMultiplier = Dec(d, P(s, nameof(StatRules.MinCriticalMultiplier))),
                MaxCriticalMultiplier = Dec(d, P(s, nameof(StatRules.MaxCriticalMultiplier))),
            };
        }

        private static DamageRules BuildDamage(RulesDocument d)
        {
            Type t = typeof(DamageRules);
            string rounding = d.Values[P(t, nameof(DamageRules.FinalRounding))].Token!;
            return new DamageRules
            {
                ArmorConstant = Int(d, P(t, nameof(DamageRules.ArmorConstant))),
                MagicResistanceConstant = Int(d, P(t, nameof(DamageRules.MagicResistanceConstant))),
                MinCategoryMultiplier = Dec(d, P(t, nameof(DamageRules.MinCategoryMultiplier))),
                MaxCategoryMultiplier = Dec(d, P(t, nameof(DamageRules.MaxCategoryMultiplier))),
                FinalRounding = ((RoundingMode[])Enum.GetValues(typeof(RoundingMode))).First(m => RulesCatalog.Id(m) == rounding),
                MinimumDamage = Int(d, P(t, nameof(DamageRules.MinimumDamage))),
            };
        }
    }
}
