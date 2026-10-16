using System.Collections.Generic;
using EdS.Domain.Numerics;

namespace EdS.Data.Classes
{
    // Modèle brut d'un fichier de classe (ex. bastion.json), tel qu'un adaptateur l'a lu, avant validation.
    // Un champ absent du fichier reste null : c'est le validateur commun qui le signale.
    // La forme suit exactement le JSON, pour que les chemins d'erreur du domaine (trees[0].talents[2].maxRank)
    // désignent le bon endroit du fichier.

    /// <summary>Fichier de définition d'une classe.</summary>
    public sealed class ClassDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }

        /// <summary>Contenu de <c>data</c> ; null si absent ou illisible.</summary>
        public ClassData? Data { get; set; }
    }

    public sealed class ClassData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public List<TreeData?>? Trees { get; set; }
        public List<SkillData?>? Skills { get; set; }
    }

    public sealed class TreeData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public List<TalentData?>? Talents { get; set; }
    }

    public sealed class TalentData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public int? MaxRank { get; set; }
        public bool? Major { get; set; }
        public bool? Placeholder { get; set; }
        public List<RequirementData?>? Prerequisites { get; set; }
    }

    public sealed class SkillData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }

        /// <summary>« active », « ultimate » ou « passive ».</summary>
        public string? Kind { get; set; }

        public int? MinLevel { get; set; }
        public bool? Placeholder { get; set; }
        public List<RequirementData?>? TalentRequirements { get; set; }

        /// <summary>Données de lancement (facultatives) : sans elles, la compétence s'équipe mais ne se lance pas encore.</summary>
        public SkillCombatData? Combat { get; set; }
    }

    public sealed class SkillCombatData
    {
        /// <summary>« physique », « agile » ou « magique ».</summary>
        public string? PowerType { get; set; }

        /// <summary>« physique » ou « magique ».</summary>
        public string? DamageType { get; set; }

        public Fixed? Coefficient { get; set; }
        public int? EnduranceCost { get; set; }
        public long? CooldownMs { get; set; }

        /// <summary>« enemy » ou « allEnemies ».</summary>
        public string? Targeting { get; set; }

        public bool? Unblockable { get; set; }
        public int? RampartChargeCost { get; set; }
        public int? RampartChargeGain { get; set; }
        public bool? Provisional { get; set; }

        /// <summary>Altérations appliquées à chaque cible touchée (schéma 3, lot 7.2) ; liste éventuellement vide.</summary>
        public List<StatusApplicationData?>? Applies { get; set; }
    }

    /// <summary>Altération portée par une compétence ou une attaque d'assaillant : <c>{ "status": "burn", "coefficient": 0.5 }</c>.</summary>
    public sealed class StatusApplicationData
    {
        public string? Status { get; set; }

        /// <summary>DOT seulement ; absent pour les autres catégories.</summary>
        public Fixed? Coefficient { get; set; }
    }

    public sealed class RequirementData
    {
        public string? Talent { get; set; }
        public int? Rank { get; set; }
    }
}
