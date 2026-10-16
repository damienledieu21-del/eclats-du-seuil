using System.Collections.Generic;
using EdS.Domain.Numerics;

namespace EdS.Data.Scenarios
{
    // Modèle brut d'un fichier de scénario (ex. scenarios/mannequin.json), avant validation.
    // Un champ absent reste null ; c'est le validateur commun qui le signale.

    public sealed class ScenarioDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
        public ScenarioData? Data { get; set; }
    }

    public sealed class ScenarioData
    {
        public long? Seed { get; set; }
        public long? DurationMs { get; set; }
        public List<CombatantData?>? Combatants { get; set; }
        public List<CommandData?>? Commands { get; set; }
    }

    public sealed class CombatantData
    {
        public string? Id { get; set; }

        /// <summary>« character » ou « dummy ».</summary>
        public string? Type { get; set; }

        public string? Team { get; set; }
        public int? Initiative { get; set; }

        // Personnage
        public string? Class { get; set; }
        public BuildData? Build { get; set; }

        // Mannequin
        public Fixed? Armor { get; set; }
        public Fixed? MagicResistance { get; set; }

        /// <summary>Champs effectivement présents dans le fichier, pour refuser ceux qui ne conviennent pas au type.</summary>
        public HashSet<string> Present { get; } = new HashSet<string>();
    }

    public sealed class BuildData
    {
        public int? Level { get; set; }

        /// <summary>Attributs par identifiant (« force »…), dans l'ordre du fichier.</summary>
        public List<KeyValuePair<string, int?>>? Attributes { get; set; }

        /// <summary>Rang par identifiant de talent, dans l'ordre du fichier.</summary>
        public List<KeyValuePair<string, int?>>? Talents { get; set; }

        public List<string?>? ActiveSkills { get; set; }

        /// <summary>Ultime équipée ; null si absente ou null dans le fichier.</summary>
        public string? Ultimate { get; set; }

        public List<string?>? PassiveSkills { get; set; }
    }

    public sealed class CommandData
    {
        public long? AtMs { get; set; }
        public string? Actor { get; set; }
        public string? Skill { get; set; }

        /// <summary>Cible ; null si absente ou null (compétence de zone).</summary>
        public string? Target { get; set; }
    }
}
