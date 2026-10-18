using System.Collections.Generic;
using EdS.Data.Scenarios;

namespace EdS.Data.Builds
{
    /// <summary>Fichier de builds de référence (<c>kind: "builds"</c>, sous-lot 8.5) tel que lu, avant validation.</summary>
    public sealed class BuildSetDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
        public BuildSetData? Data { get; set; }
    }

    public sealed class BuildSetData
    {
        /// <summary>Identifiant de la classe de tous les builds du fichier.</summary>
        public string? Class { get; set; }

        public List<ReferenceBuildData?>? Builds { get; set; }
    }

    public sealed class ReferenceBuildData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }

        /// <summary><c>reference</c> (build de spécialisation comparé) ou <c>control</c> (build de contrôle).</summary>
        public string? Category { get; set; }

        /// <summary>Même forme que le build d'un personnage de scénario.</summary>
        public BuildData? Build { get; set; }
    }
}
