using System.Collections.Generic;

namespace EdS.Data.Saves
{
    /// <summary>Sous-lot 12.1 : sauvegarde du monde telle que lue dans le fichier, avant validation (<see cref="SaveValidator"/>).</summary>
    public sealed class SaveDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
        public SaveData? Data { get; set; }
    }

    public sealed class SaveData
    {
        /// <summary>Zone où se trouve le joueur (identifiant de zone, par exemple « zone.vallons »).</summary>
        public string? Zone { get; set; }

        /// <summary>Temps du monde, en ticks de simulation depuis le début de la partie.</summary>
        public long? WorldTick { get; set; }

        /// <summary>Nombre de sessions de zone déjà jouées (chaque session a sa propre simulation).</summary>
        public long? Sessions { get; set; }

        /// <summary>Événements consommables déjà attribués (morts, étapes, récompenses), par clé unique.</summary>
        public List<string?>? Consumed { get; set; }

        /// <summary>Sous-lot 12.2 (version 2) : personnage du joueur ; null tant qu'aucun n'a été créé.</summary>
        public CharacterData? Character { get; set; }

        /// <summary>Le champ <c>character</c> est présent (null compris).</summary>
        public bool HasCharacter { get; set; }
    }

    /// <summary>Sous-lot 12.2 : personnage persistant (le niveau se déduit de l'expérience, les attributs de la classe et du niveau).</summary>
    public sealed class CharacterData
    {
        public string? Id { get; set; }
        public string? Class { get; set; }
        public long? Xp { get; set; }
        public List<KeyValuePair<string, int?>>? Talents { get; set; }
        public List<string?>? ActiveSkills { get; set; }
        public string? Ultimate { get; set; }

        /// <summary>Revue Codex de la PR #43 : le champ <c>ultimate</c> est présent (null explicite compris).</summary>
        public bool HasUltimate { get; set; }

        public List<string?>? PassiveSkills { get; set; }

        /// <summary>Sous-lot 12.3 (version 3) : places occupées de l'inventaire, dans l'ordre.</summary>
        public List<ItemStackData?>? Inventory { get; set; }

        /// <summary>Sous-lot 12.3 (version 3) : objet porté par emplacement.</summary>
        public List<KeyValuePair<string, string?>>? Equipment { get; set; }

        /// <summary>Champs effectivement présents (selon la version).</summary>
        public HashSet<string> Present { get; } = new HashSet<string>();
    }

    /// <summary>Sous-lot 12.3 : pile d'objets d'une place de l'inventaire.</summary>
    public sealed class ItemStackData
    {
        public string? Item { get; set; }
        public int? Count { get; set; }
    }
}
