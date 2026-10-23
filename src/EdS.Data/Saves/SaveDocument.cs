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
    }
}
