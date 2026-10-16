using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Domain.Builds
{
    /// <summary>
    /// Règles de progression communes à toutes les classes : niveaux, points de talent, seuil des talents majeurs,
    /// déblocage des emplacements. Valeurs par défaut : règles validées du cahier des charges (16/10/2026).
    /// </summary>
    public sealed class ProgressionRules
    {
        public static readonly ProgressionRules Provisional = new ProgressionRules();

        public int MaxLevel { get; init; } = 30;

        /// <summary>Premier niveau qui rapporte des points de talent (2 : aucun point au niveau 1).</summary>
        public int FirstTalentPointLevel { get; init; } = 2;

        public int TalentPointsPerLevel { get; init; } = 1;

        /// <summary>Points déjà investis dans la branche, hors talent majeur lui-même, pour acquérir un talent majeur.</summary>
        public int MajorTalentThreshold { get; init; } = 18;

        public int MaxMajorTalents { get; init; } = 1;

        /// <summary>Nombre d'arbres de talents par classe.</summary>
        public int TreesPerClass { get; init; } = 3;

        /// <summary>Niveau de déblocage de chaque emplacement actif, dans l'ordre (2 au niveau 1, 3 au niveau 5, 4 au niveau 10).</summary>
        public IReadOnlyList<int> ActiveSlotLevels { get; init; } = new[] { 1, 1, 5, 10 };

        /// <summary>Niveau de déblocage de chaque emplacement passif, dans l'ordre.</summary>
        public IReadOnlyList<int> PassiveSlotLevels { get; init; } = new[] { 1, 5, 20, 30 };

        public int UltimateUnlockLevel { get; init; } = 10;

        public int TalentPoints(int level)
        {
            RequireLevel(level);
            return level < FirstTalentPointLevel ? 0 : checked((level - FirstTalentPointLevel + 1) * TalentPointsPerLevel);
        }

        public int ActiveSlots(int level)
        {
            RequireLevel(level);
            return ActiveSlotLevels.Count(l => l <= level);
        }

        public int PassiveSlots(int level)
        {
            RequireLevel(level);
            return PassiveSlotLevels.Count(l => l <= level);
        }

        public bool IsUltimateUnlocked(int level)
        {
            RequireLevel(level);
            return level >= UltimateUnlockLevel;
        }

        public bool IsValidLevel(int level) => level >= 1 && level <= MaxLevel;

        private void RequireLevel(int level)
        {
            if (!IsValidLevel(level))
                throw new ArgumentOutOfRangeException(nameof(level), level, $"Niveau attendu entre 1 et {MaxLevel}.");
        }

        /// <summary>Liste des incohérences ; vide si les règles sont utilisables.</summary>
        public IReadOnlyList<RuleViolation> Validate()
        {
            var errors = new List<RuleViolation>();
            if (MaxLevel < 1) errors.Add(new RuleViolation("le niveau maximum doit valoir au moins 1.", nameof(MaxLevel)));
            if (FirstTalentPointLevel < 1) errors.Add(new RuleViolation("doit valoir au moins 1.", nameof(FirstTalentPointLevel)));
            if (TalentPointsPerLevel < 0) errors.Add(new RuleViolation("ne peut pas être négatif.", nameof(TalentPointsPerLevel)));
            if (MajorTalentThreshold < 0) errors.Add(new RuleViolation("ne peut pas être négatif.", nameof(MajorTalentThreshold)));
            if (MaxMajorTalents < 0) errors.Add(new RuleViolation("ne peut pas être négatif.", nameof(MaxMajorTalents)));
            if (TreesPerClass < 1) errors.Add(new RuleViolation("une classe doit avoir au moins un arbre.", nameof(TreesPerClass)));
            CheckLevels(ActiveSlotLevels, nameof(ActiveSlotLevels), errors);
            CheckLevels(PassiveSlotLevels, nameof(PassiveSlotLevels), errors);
            if (UltimateUnlockLevel < 1 || UltimateUnlockLevel > MaxLevel)
                errors.Add(new RuleViolation($"doit être compris entre 1 et le niveau maximum ({MaxLevel}).", nameof(UltimateUnlockLevel)));
            if (MaxLevel >= 1 && FirstTalentPointLevel >= 1 && TalentPointsPerLevel >= 0)
            {
                long maxPoints = MaxLevel < FirstTalentPointLevel ? 0 : (long)(MaxLevel - FirstTalentPointLevel + 1) * TalentPointsPerLevel;
                if (maxPoints > int.MaxValue) errors.Add(new RuleViolation("le total de points de talent dépasse la plage d'un entier.", nameof(TalentPointsPerLevel)));
            }
            return errors;
        }

        private void CheckLevels(IReadOnlyList<int>? levels, string name, List<RuleViolation> errors)
        {
            if (levels is null)
            {
                errors.Add(new RuleViolation("liste obligatoire.", name));
                return;
            }
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i] < 1 || levels[i] > MaxLevel)
                    errors.Add(new RuleViolation($"niveau {levels[i]} hors de [1, {MaxLevel}] (position {i + 1}).", name));
                if (i > 0 && levels[i] < levels[i - 1])
                    errors.Add(new RuleViolation($"les niveaux doivent être dans l'ordre croissant ({levels[i - 1]} puis {levels[i]}).", name));
            }
        }
    }
}
