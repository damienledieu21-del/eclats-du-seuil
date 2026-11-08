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

        /// <summary>
        /// Sous-lot V1.3 (version 9) : propriétés tirées des objets portés, par emplacement (un objet porté écrit sous sa forme d'instance,
        /// <c>{ "item": …, "properties": […] }</c>) ; les emplacements écrits sous la forme simple n'y figurent pas.
        /// </summary>
        public List<KeyValuePair<string, ItemStackData>>? EquipmentInstances { get; set; }

        /// <summary>Sous-lot 12.6 (version 5) : expérience de chaque métier.</summary>
        public List<KeyValuePair<string, long?>>? Professions { get; set; }

        /// <summary>Sous-lot 12.5 (version 4) : état narratif personnel.</summary>
        public StoryStateData? Story { get; set; }

        /// <summary>Sous-lot V1.1 (version 6) : bourse du personnage, en pièces de cuivre.</summary>
        public long? Money { get; set; }

        /// <summary>Sous-lot V1.4 (version 11) : fabrications tentées (compteur de la clé du tirage).</summary>
        public long? Crafts { get; set; }

        /// <summary>Sous-lot V1.8c (version 12) : bonus actifs (plat, élixir, huile ou pierre d'arme), dans l'ordre des places.</summary>
        public List<BoonData?>? Boons { get; set; }

        /// <summary>Sous-lot V2.5 (version 13) : registre des événements du monde.</summary>
        public List<SavedEventData?>? Events { get; set; }

        /// <summary>Sous-lot V2.6 (version 14) : cicatrices des failles restées ouvertes.</summary>
        public List<SavedScarData?>? Scars { get; set; }

        /// <summary>Sous-lot V2.10 (version 16) : stock des marchands (facultatif : absent quand tout est plein).</summary>
        public List<SavedStockData?>? Stock { get; set; }

        /// <summary>Champs effectivement présents (selon la version).</summary>
        public HashSet<string> Present { get; } = new HashSet<string>();
    }

    /// <summary>Sous-lot 12.3 : pile d'objets d'une place de l'inventaire. Sous-lot V1.3 (version 9) : instance d'objet, propriétés tirées.</summary>
    public sealed class ItemStackData
    {
        public string? Item { get; set; }
        public int? Count { get; set; }

        /// <summary>Sous-lot V1.3 (version 9) : propriétés tirées ; absentes pour un objet de base.</summary>
        public List<PropertyRollData?>? Properties { get; set; }

        /// <summary>Sous-lot V1.4 (version 11) : qualité de fabrication (« superior », « masterwork ») ; absente : normale.</summary>
        public string? Quality { get; set; }

        /// <summary>Sous-lot V1.4 (version 11) : personnage qui a signé un chef-d'œuvre ; absent pour une autre qualité.</summary>
        public string? SignedBy { get; set; }

        /// <summary>Sous-lot V1.4 (version 11) : points de solidité qui restent ; absent : usure pleine.</summary>
        public int? Wear { get; set; }

        /// <summary>Champs de l'instance effectivement présents (<c>quality</c>, <c>signedBy</c>, <c>wear</c>), pour refuser ceux d'une version qui ne les a pas.</summary>
        public HashSet<string> Present { get; } = new HashSet<string>();
    }

    /// <summary>Sous-lot V1.8c : bonus actif lu, avant validation.</summary>
    public sealed class BoonData
    {
        /// <summary>Place : « meal », « elixir » ou « weapon ».</summary>
        public string? Kind { get; set; }

        /// <summary>Objet consommé (« item.… »).</summary>
        public string? Item { get; set; }

        /// <summary>Ticks logiques qui restent.</summary>
        public int? Ticks { get; set; }
    }

    /// <summary>Sous-lot V1.3 : propriété tirée d'une instance d'objet, telle que lue.</summary>
    public sealed class PropertyRollData
    {
        public string? Id { get; set; }
        public int? Value { get; set; }
    }

    /// <summary>Sous-lot 12.5 : état narratif lu, avant validation.</summary>
    public sealed class StoryStateData
    {
        public List<string?>? Facts { get; set; }
        public List<(string Id, QuestProgressData? Value)>? Quests { get; set; }
        public List<KeyValuePair<string, int?>>? Trust { get; set; }
        public List<string?>? Discoveries { get; set; }

        /// <summary>Sous-lot V1.6 (version 8) : quête suivie, ou null.</summary>
        public string? Tracked { get; set; }

        /// <summary>Le champ <c>tracked</c> est présent (null compris).</summary>
        public bool HasTracked { get; set; }

        /// <summary>Sous-lot V1.7 (version 10) : carte d'aventurier, ou null (absente : pas encore inscrit).</summary>
        public GuildCardData? Guild { get; set; }

        /// <summary>Le champ <c>guild</c> est présent (null compris : refusé, la carte est absente tant qu'on n'est pas inscrit).</summary>
        public bool HasGuild { get; set; }

        /// <summary>Sous-lot V1.7 (version 10, relecture de la PR #79) : créatures rares déjà vaincues, ou null (absent).</summary>
        public List<string?>? Hunted { get; set; }

        /// <summary>Sous-lot V2.7 (version 15) : réputation auprès des factions, ou null (absente).</summary>
        public List<KeyValuePair<string, int?>>? Reputation { get; set; }

        /// <summary>Sous-lot V2.10 (version 16) : dernier jour du monde où le personnage a concouru à chaque fête (facultatif).</summary>
        public List<KeyValuePair<string, int?>>? Festivals { get; set; }
    }

    /// <summary>Sous-lot V1.7 : carte d'aventurier lue, avant validation.</summary>
    public sealed class GuildCardData
    {
        public string? Rank { get; set; }
        public long? Points { get; set; }
        public long? Contracts { get; set; }
        public List<string?>? Titles { get; set; }
        public string? Title { get; set; }

        /// <summary>Le champ <c>title</c> est présent (null compris).</summary>
        public bool HasTitle { get; set; }

        /// <summary>Contrats en cours : avancement par identifiant, dans l'ordre du fichier.</summary>
        public List<KeyValuePair<string, long?>>? Active { get; set; }

        public long? Day { get; set; }
        public List<string?>? Taken { get; set; }
    }

    public sealed class QuestProgressData
    {
        public string? Step { get; set; }
        public long? Progress { get; set; }
    }
}
