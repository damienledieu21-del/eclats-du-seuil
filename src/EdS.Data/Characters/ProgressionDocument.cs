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
        /// <summary>Niveau maximal du premier parcours (cadrage Q3 : 1 à 10).</summary>
        public int? MaxLevel { get; set; }

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
    }
}
