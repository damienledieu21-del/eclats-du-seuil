using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Validation;
using EdS.Domain;
using EdS.Domain.Builds;
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
        /// <param name="document">Contenu lu par un adaptateur.</param>
        /// <param name="report">Rapport de l'adaptateur (erreurs de syntaxe et de type) à compléter ; nouveau si null.</param>
        public static RulesLoadResult Validate(RulesDocument document, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            report ??= new ValidationReport();

            if (!ValidateHeader(document, report))
                return new RulesLoadResult(report, null);   // structure d'une autre version : refusée sans analyse du contenu
            ValidateFieldsShape(document, report);

            if (!report.IsValid) return new RulesLoadResult(report, null);

            StatRules stats = BuildStats(document);
            DamageRules damage = BuildDamage(document);
            ProgressionRules progression = BuildProgression(document);
            CombatRules combat = BuildCombat(document);
            AddViolations(report, typeof(StatRules), stats.Validate());
            AddViolations(report, typeof(DamageRules), damage.Validate());
            AddViolations(report, typeof(ProgressionRules), progression.Validate());
            AddViolations(report, typeof(CombatRules), combat.Validate());
            if (!report.IsValid) return new RulesLoadResult(report, null);

            int schemaVersion = document.SchemaVersion!.Value;
            var rules = new LoadedRules(schemaVersion, document.ContentVersion!, RulesFingerprint.Compute(schemaVersion, document), stats, damage, progression, combat);
            return new RulesLoadResult(report, rules);
        }

        private static bool ValidateHeader(RulesDocument document, ValidationReport report) =>
            HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion,
                RulesCatalog.Kind, DataFormat.RulesSchemaVersion, report);

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
                    case RuleValueKind.Int32List:
                        if (value.Kind != RuleValueKind.Int32List)
                            report.Add(field.JsonPath, ValidationCode.WrongType, "liste d'entiers attendue.");
                        else if (value.List!.Any(v => v < int.MinValue || v > int.MaxValue))
                            report.Add(field.JsonPath, ValidationCode.OutOfRange, "une valeur dépasse la plage d'un entier 32 bits.");
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
                RuleValueKind.Int32List => "liste d'entiers",
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

        private static ProgressionRules BuildProgression(RulesDocument d)
        {
            Type p = typeof(ProgressionRules);
            int I(string property) => (int)Int(d, P(p, property));
            int[] L(string property) => d.Values[P(p, property)].List!.Select(v => (int)v).ToArray();
            return new ProgressionRules
            {
                MaxLevel = I(nameof(ProgressionRules.MaxLevel)),
                FirstTalentPointLevel = I(nameof(ProgressionRules.FirstTalentPointLevel)),
                TalentPointsPerLevel = I(nameof(ProgressionRules.TalentPointsPerLevel)),
                MajorTalentThreshold = I(nameof(ProgressionRules.MajorTalentThreshold)),
                MaxMajorTalents = I(nameof(ProgressionRules.MaxMajorTalents)),
                TreesPerClass = I(nameof(ProgressionRules.TreesPerClass)),
                ActiveSlotLevels = L(nameof(ProgressionRules.ActiveSlotLevels)),
                PassiveSlotLevels = L(nameof(ProgressionRules.PassiveSlotLevels)),
                UltimateUnlockLevel = I(nameof(ProgressionRules.UltimateUnlockLevel)),
            };
        }

        private static CombatRules BuildCombat(RulesDocument d)
        {
            Type c = typeof(CombatRules);
            return new CombatRules
            {
                TickMilliseconds = (int)Int(d, P(c, nameof(CombatRules.TickMilliseconds))),
                GlobalCooldownMs = Int(d, P(c, nameof(CombatRules.GlobalCooldownMs))),
                EnduranceRegenPerSecond = (int)Int(d, P(c, nameof(CombatRules.EnduranceRegenPerSecond))),
                EnduranceRegenDelayMs = Int(d, P(c, nameof(CombatRules.EnduranceRegenDelayMs))),
                MaxDurationMs = Int(d, P(c, nameof(CombatRules.MaxDurationMs))),
                MaxEventsPerTick = (int)Int(d, P(c, nameof(CombatRules.MaxEventsPerTick))),
                MaxCombatants = (int)Int(d, P(c, nameof(CombatRules.MaxCombatants))),
                MaxCommands = (int)Int(d, P(c, nameof(CombatRules.MaxCommands))),
                MaxCommandsPerActorPerTick = (int)Int(d, P(c, nameof(CombatRules.MaxCommandsPerActorPerTick))),
                MaxPendingEvents = (int)Int(d, P(c, nameof(CombatRules.MaxPendingEvents))),
                MaxWorkPerTick = (int)Int(d, P(c, nameof(CombatRules.MaxWorkPerTick))),
                GuardDamageReduction = Dec(d, P(c, nameof(CombatRules.GuardDamageReduction))),
                GuardUpkeepPerSecond = (int)Int(d, P(c, nameof(CombatRules.GuardUpkeepPerSecond))),
                GuardCostPerBlock = (int)Int(d, P(c, nameof(CombatRules.GuardCostPerBlock))),
                GuardBreakDurationMs = Int(d, P(c, nameof(CombatRules.GuardBreakDurationMs))),
                ParryWindowMs = Int(d, P(c, nameof(CombatRules.ParryWindowMs))),
                ParryDamageReduction = Dec(d, P(c, nameof(CombatRules.ParryDamageReduction))),
                ParryFailureRecoveryMs = Int(d, P(c, nameof(CombatRules.ParryFailureRecoveryMs))),
                RampartChargesPerParry = (int)Int(d, P(c, nameof(CombatRules.RampartChargesPerParry))),
                RampartChargesMax = (int)Int(d, P(c, nameof(CombatRules.RampartChargesMax))),
                ResonanceMax = (int)Int(d, P(c, nameof(CombatRules.ResonanceMax))),
                ResonancePerDirectHit = (int)Int(d, P(c, nameof(CombatRules.ResonancePerDirectHit))),
                ResonancePerPerfectParry = (int)Int(d, P(c, nameof(CombatRules.ResonancePerPerfectParry))),
                ResonancePerBlock = (int)Int(d, P(c, nameof(CombatRules.ResonancePerBlock))),
                UltimateResonanceCost = (int)Int(d, P(c, nameof(CombatRules.UltimateResonanceCost))),
                FragilityDurationMs = Int(d, P(c, nameof(CombatRules.FragilityDurationMs))),
                FragilityDamageTakenIncrease = Dec(d, P(c, nameof(CombatRules.FragilityDamageTakenIncrease))),
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
