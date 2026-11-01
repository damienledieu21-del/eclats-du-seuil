using System.Collections.Generic;
using EdS.Data.Scenarios;

namespace EdS.Data.Characters
{
    /// <summary>Sous-lot 12.2 : progression du personnage persistant telle que lue dans le fichier, avant validation.</summary>
    public sealed class ProgressionDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
        public ProgressionData? Data { get; set; }
    }

    public sealed class ProgressionData
    {
        /// <summary>Niveau le plus haut décrit par les données (cadrage Q3 : 10 ; sous-lot V1.8 : 30).</summary>
        public int? MaxLevel { get; set; }

        /// <summary>Sous-lot V1.8 (schéma 3, sujet 54) : plafond de niveau actuel, au plus <see cref="MaxLevel"/>.</summary>
        public int? LevelCap { get; set; }

        /// <summary>Sous-lot V1.8 (schéma 3) : premier niveau de chaque chapitre de la quête principale.</summary>
        public List<int?>? ChapterStartLevels { get; set; }

        /// <summary>Expérience cumulée qu'il faut pour être à chaque niveau, du niveau 1 (0) au niveau maximal.</summary>
        public List<long?>? XpToLevel { get; set; }

        /// <summary>Expérience d'une créature vaincue : 1 point par tranche de ces PV maximaux (au moins 1).</summary>
        public long? KillXpPerHealth { get; set; }

        public List<ClassProgressionData?>? Classes { get; set; }
    }

    public sealed class ClassProgressionData
    {
        public string? Class { get; set; }

        /// <summary>Attributs de base à chaque niveau, du niveau 1 au niveau maximal (cadrage Q20 : fixés par la classe et le niveau).</summary>
        public List<List<KeyValuePair<string, int?>>?>? Attributes { get; set; }

        /// <summary>Compétences d'un nouveau personnage (niveau 1).</summary>
        public List<string?>? StartActiveSkills { get; set; }

        public List<string?>? StartPassiveSkills { get; set; }

        /// <summary>Sous-lot 12.9b (schéma 2) : compétences gagnées en montant de niveau, équipées d'elles-mêmes.</summary>
        public List<SkillUnlockData?>? SkillUnlocks { get; set; }
    }

    /// <summary>Sous-lot 12.9b : une compétence gagnée à un niveau.</summary>
    public sealed class SkillUnlockData
    {
        public int? Level { get; set; }
        public string? Skill { get; set; }
    }
}
