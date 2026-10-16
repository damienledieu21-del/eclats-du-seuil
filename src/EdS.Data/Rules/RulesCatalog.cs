using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;

namespace EdS.Data.Rules
{
    /// <summary>Un paramètre de <c>rules.json</c> et la propriété du domaine qu'il alimente.</summary>
    public sealed class RuleField
    {
        /// <summary>Chemin sous <c>data</c>, segments séparés par des points (ex. <c>stats.base.maxHealth</c>).</summary>
        public string Path { get; }

        /// <summary>Chemin JSON complet, ex. <c>$.data.stats.base.maxHealth</c>.</summary>
        public string JsonPath => "$.data." + Path;

        public RuleValueKind Kind { get; }

        /// <summary>Classe du domaine alimentée : <c>StatRules</c>, <c>DamageRules</c> ou <c>ProgressionRules</c>.</summary>
        public Type Owner { get; }

        /// <summary>Nom de la propriété C# alimentée dans <see cref="Owner"/>.</summary>
        public string Property { get; }

        /// <summary>Valeurs autorisées pour un <see cref="RuleValueKind.Token"/>.</summary>
        public IReadOnlyList<string> AllowedTokens { get; }

        internal RuleField(string path, RuleValueKind kind, Type owner, string property, IReadOnlyList<string>? allowedTokens = null)
        {
            Path = path;
            Kind = kind;
            Owner = owner;
            Property = property;
            AllowedTokens = allowedTokens ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Description unique de <c>rules.json</c> : chaque paramètre, son type, son emplacement et la propriété du domaine
    /// qu'il alimente. Les adaptateurs (System.Text.Json aujourd'hui, Unity plus tard) et le validateur commun
    /// s'appuient tous sur ce catalogue ; il n'existe pas d'autre description du format.
    /// </summary>
    public static class RulesCatalog
    {
        /// <summary>Valeur attendue du champ <c>kind</c>.</summary>
        public const string Kind = "rules";

        /// <summary>Identifiant stable d'un attribut dans les données (« force », « dexterite »…).</summary>
        public static string Id(AttributeKind kind) => kind.ToString().ToLowerInvariant();

        /// <summary>Identifiant stable d'un type de dégâts dans les données (« physique », « magique »).</summary>
        public static string Id(DamageType type) => type.ToString().ToLowerInvariant();

        /// <summary>Identifiant d'un mode d'arrondi dans les données (« halfAwayFromZero »…).</summary>
        public static string Id(RoundingMode mode)
        {
            string name = mode.ToString();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        public static IReadOnlyList<string> RoundingTokens { get; } =
            ((RoundingMode[])Enum.GetValues(typeof(RoundingMode))).Select(Id).ToArray();

        /// <summary>
        /// Objets dont les clés sont des identifiants du domaine : une clé inconnue y est une référence invalide,
        /// pas une simple faute de frappe. Valeur : libellé utilisé dans les messages.
        /// </summary>
        public static IReadOnlyDictionary<string, string> ReferenceMaps { get; } = new Dictionary<string, string>
        {
            ["stats.perAttribute"] = "attribut",
            ["damage.defenseConstants"] = "type de dégâts",
        };

        private static readonly Type S = typeof(StatRules);
        private static readonly Type D = typeof(DamageRules);
        private static readonly Type P = typeof(ProgressionRules);

        public static IReadOnlyList<RuleField> Fields { get; } = new[]
        {
            new RuleField("stats.base.physicalPower", RuleValueKind.Decimal, S, nameof(StatRules.BasePhysicalPower)),
            new RuleField("stats.base.agilePower", RuleValueKind.Decimal, S, nameof(StatRules.BaseAgilePower)),
            new RuleField("stats.base.magicPower", RuleValueKind.Decimal, S, nameof(StatRules.BaseMagicPower)),
            new RuleField("stats.base.armor", RuleValueKind.Decimal, S, nameof(StatRules.BaseArmor)),
            new RuleField("stats.base.magicResistance", RuleValueKind.Decimal, S, nameof(StatRules.BaseMagicResistance)),
            new RuleField("stats.base.maxHealth", RuleValueKind.Decimal, S, nameof(StatRules.BaseMaxHealth)),
            new RuleField("stats.base.maxEndurance", RuleValueKind.Decimal, S, nameof(StatRules.BaseMaxEndurance)),
            new RuleField("stats.base.statusResistance", RuleValueKind.Decimal, S, nameof(StatRules.BaseStatusResistance)),

            new RuleField($"stats.perAttribute.{Id(AttributeKind.Force)}.physicalPower", RuleValueKind.Decimal, S, nameof(StatRules.PhysicalPowerPerForce)),
            new RuleField($"stats.perAttribute.{Id(AttributeKind.Dexterite)}.agilePower", RuleValueKind.Decimal, S, nameof(StatRules.AgilePowerPerDexterite)),
            new RuleField($"stats.perAttribute.{Id(AttributeKind.Intelligence)}.magicPower", RuleValueKind.Decimal, S, nameof(StatRules.MagicPowerPerIntelligence)),
            new RuleField($"stats.perAttribute.{Id(AttributeKind.Vitalite)}.maxHealth", RuleValueKind.Decimal, S, nameof(StatRules.MaxHealthPerVitalite)),
            new RuleField($"stats.perAttribute.{Id(AttributeKind.Volonte)}.statusResistance", RuleValueKind.Decimal, S, nameof(StatRules.StatusResistancePerVolonte)),
            new RuleField($"stats.perAttribute.{Id(AttributeKind.Endurance)}.maxEndurance", RuleValueKind.Decimal, S, nameof(StatRules.MaxEndurancePerEndurance)),

            new RuleField("stats.critical.baseChanceBasisPoints", RuleValueKind.Int32, S, nameof(StatRules.BaseCriticalChanceBasisPoints)),
            new RuleField("stats.critical.minChanceBasisPoints", RuleValueKind.Int32, S, nameof(StatRules.MinCriticalChanceBasisPoints)),
            new RuleField("stats.critical.maxChanceBasisPoints", RuleValueKind.Int32, S, nameof(StatRules.MaxCriticalChanceBasisPoints)),
            new RuleField("stats.critical.baseMultiplier", RuleValueKind.Decimal, S, nameof(StatRules.BaseCriticalMultiplier)),
            new RuleField("stats.critical.minMultiplier", RuleValueKind.Decimal, S, nameof(StatRules.MinCriticalMultiplier)),
            new RuleField("stats.critical.maxMultiplier", RuleValueKind.Decimal, S, nameof(StatRules.MaxCriticalMultiplier)),

            new RuleField($"damage.defenseConstants.{Id(DamageType.Physique)}", RuleValueKind.Int64, D, nameof(DamageRules.ArmorConstant)),
            new RuleField($"damage.defenseConstants.{Id(DamageType.Magique)}", RuleValueKind.Int64, D, nameof(DamageRules.MagicResistanceConstant)),
            new RuleField("damage.categoryMultiplier.min", RuleValueKind.Decimal, D, nameof(DamageRules.MinCategoryMultiplier)),
            new RuleField("damage.categoryMultiplier.max", RuleValueKind.Decimal, D, nameof(DamageRules.MaxCategoryMultiplier)),
            new RuleField("damage.finalRounding", RuleValueKind.Token, D, nameof(DamageRules.FinalRounding), RoundingTokens),
            new RuleField("damage.minimumDamage", RuleValueKind.Int64, D, nameof(DamageRules.MinimumDamage)),

            new RuleField("progression.maxLevel", RuleValueKind.Int32, P, nameof(ProgressionRules.MaxLevel)),
            new RuleField("progression.talentPoints.firstLevel", RuleValueKind.Int32, P, nameof(ProgressionRules.FirstTalentPointLevel)),
            new RuleField("progression.talentPoints.perLevel", RuleValueKind.Int32, P, nameof(ProgressionRules.TalentPointsPerLevel)),
            new RuleField("progression.majorTalents.threshold", RuleValueKind.Int32, P, nameof(ProgressionRules.MajorTalentThreshold)),
            new RuleField("progression.majorTalents.max", RuleValueKind.Int32, P, nameof(ProgressionRules.MaxMajorTalents)),
            new RuleField("progression.treesPerClass", RuleValueKind.Int32, P, nameof(ProgressionRules.TreesPerClass)),
            new RuleField("progression.slots.activeUnlockLevels", RuleValueKind.Int32List, P, nameof(ProgressionRules.ActiveSlotLevels)),
            new RuleField("progression.slots.passiveUnlockLevels", RuleValueKind.Int32List, P, nameof(ProgressionRules.PassiveSlotLevels)),
            new RuleField("progression.slots.ultimateUnlockLevel", RuleValueKind.Int32, P, nameof(ProgressionRules.UltimateUnlockLevel)),
        };

        private static readonly Dictionary<string, RuleField> ByPath = Fields.ToDictionary(f => f.Path, StringComparer.Ordinal);

        public static bool TryGetField(string path, out RuleField field) => ByPath.TryGetValue(path, out field!);

        public static RuleField FieldFor(Type owner, string property) =>
            Fields.FirstOrDefault(f => f.Owner == owner && f.Property == property)
            ?? throw new ArgumentException($"Aucun paramètre de rules.json n'alimente {owner.Name}.{property}.");
    }
}
