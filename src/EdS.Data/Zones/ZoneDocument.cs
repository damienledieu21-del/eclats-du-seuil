using System.Collections.Generic;
using EdS.Data.Scenarios;

namespace EdS.Data.Zones
{
    /// <summary>Sous-lot 12.4 : zone du monde telle que lue dans le fichier, avant validation.</summary>
    public sealed class ZoneDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
        public ZoneData? Data { get; set; }
    }

    public sealed class ZoneData
    {
        /// <summary>Identifiant de zone (« zone.vallons »).</summary>
        public string? Id { get; set; }

        public string? Name { get; set; }

        /// <summary>Scénario de zone (simulation : espace, créatures), chemin sans extension sous <c>scenarios/</c>.</summary>
        public string? Scenario { get; set; }

        /// <summary>Point sûr d'entrée dans la zone.</summary>
        public string? Entry { get; set; }

        public List<SafePointData?>? SafePoints { get; set; }
        public List<PlaceData?>? Places { get; set; }
        public List<PopulationData?>? Populations { get; set; }
        public List<PassageData?>? Passages { get; set; }

        /// <summary>Sous-lot 12.7 (schéma 2) : chef de la zone ; facultatif.</summary>
        public BossData? Boss { get; set; }
    }

    /// <summary>Sous-lot 12.7 : chef à formes successives et récompense de la première victoire.</summary>
    public sealed class BossData
    {
        public string? Id { get; set; }

        /// <summary>Scénario qui décrit les formes du chef (assaillants mortels et placés), chemin sous <c>scenarios/</c>.</summary>
        public string? Scenario { get; set; }

        /// <summary>Formes, dans l'ordre : quand l'une tombe, la suivante se relève à sa place.</summary>
        public List<string?>? Forms { get; set; }

        /// <summary>Fait appris à la première victoire (quêtes).</summary>
        public string? Fact { get; set; }

        /// <summary>Récompenses de la première victoire, attribuées une seule fois.</summary>
        public List<RewardData?>? Rewards { get; set; }

        /// <summary>Butin de chaque victoire (dernière forme) ; facultatif.</summary>
        public string? Loot { get; set; }
    }

    public sealed class RewardData
    {
        public string? Item { get; set; }
        public int? Count { get; set; }
    }

    /// <summary>Point sûr : lieu d'entrée et de retour (défaite, sous-lot 12.7).</summary>
    public sealed class SafePointData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public PointData? Position { get; set; }
    }

    /// <summary>Lieu nommé de la zone (village, moulin…) : un rectangle, pour les découvertes et les quêtes.</summary>
    public sealed class PlaceData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public PointData? Min { get; set; }
        public PointData? Max { get; set; }
    }

    /// <summary>Population : une créature du scénario qui réapparaît, avec sa laisse et son butin.</summary>
    public sealed class PopulationData
    {
        /// <summary>Créature du scénario de zone (modèle de chaque vie).</summary>
        public string? Creature { get; set; }

        /// <summary>Délai de réapparition après sa mort, en ms.</summary>
        public long? RespawnMs { get; set; }

        /// <summary>Laisse : écart (entre les bords des emprises) à sa cible au-delà duquel elle lâche, en mètres.</summary>
        public EdS.Domain.Numerics.Fixed? LeashRange { get; set; }

        /// <summary>Table de butin (catalogue des objets) ; facultative.</summary>
        public string? Loot { get; set; }
    }

    /// <summary>Passage fermé par un obstacle du scénario, ouvert pour de bon par une étape du monde (raccourci du moulin).</summary>
    public sealed class PassageData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Obstacle { get; set; }
    }
}
