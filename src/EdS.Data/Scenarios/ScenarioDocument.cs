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

        /// <summary>« character », « dummy » ou « assailant ».</summary>
        public string? Type { get; set; }

        public string? Team { get; set; }
        public int? Initiative { get; set; }

        // Personnage
        public string? Class { get; set; }
        public BuildData? Build { get; set; }

        // Mannequin et assaillant
        public Fixed? Armor { get; set; }
        public Fixed? MagicResistance { get; set; }

        /// <summary>Résistance aux altérations (schéma 2, lot 7.2, D12) ; facultative, 0 par défaut.</summary>
        public Fixed? StatusResistance { get; set; }

        // Assaillant
        /// <summary>Sous-lot 11.1 (schéma 4) : PV d'un assaillant mortel ; absent : PV infinis (entraînement, A21).</summary>
        public long? Health { get; set; }

        public string? Target { get; set; }
        public long? FirstAttackMs { get; set; }
        public long? IntervalMs { get; set; }
        public List<AttackData?>? Attacks { get; set; }

        /// <summary>Déclencheurs d'un assaillant (lot 7.3, Q4) ; facultatifs.</summary>
        public List<Triggers.TriggerData?>? Triggers { get; set; }

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

    public sealed class AttackData
    {
        public string? Id { get; set; }
        public long? RawDamage { get; set; }

        /// <summary>« physique » ou « magique ».</summary>
        public string? DamageType { get; set; }

        /// <summary>« enemy » ou « allEnemies ».</summary>
        public string? Targeting { get; set; }

        public bool? Unblockable { get; set; }

        /// <summary>Altérations appliquées à chaque cible touchée (schéma 2, lot 7.2 ; remplace <c>appliesFragility</c>).</summary>
        public List<Classes.StatusApplicationData?>? Applies { get; set; }
    }

    public sealed class CommandData
    {
        public long? AtMs { get; set; }
        public string? Actor { get; set; }

        /// <summary>Compétence lancée ; absente pour une garde ou une parade.</summary>
        public string? Skill { get; set; }

        /// <summary>« guardStart », « guardStop » ou « parry » ; absente pour une compétence.</summary>
        public string? Action { get; set; }

        /// <summary>Cible ; null si absente ou null (compétence de zone).</summary>
        public string? Target { get; set; }

        /// <summary>Champs effectivement présents dans le fichier.</summary>
        public HashSet<string> Present { get; } = new HashSet<string>();
    }
}
