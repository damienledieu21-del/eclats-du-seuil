using System;
using System.Collections.Generic;

namespace EdS.Data.Rules
{
    /// <summary>
    /// Contenu brut d'un fichier de règles, tel qu'un adaptateur l'a lu, avant validation.
    /// Un champ absent du fichier est simplement absent d'ici : c'est le validateur commun qui le signale.
    /// </summary>
    public sealed class RulesDocument
    {
        private readonly Dictionary<string, RuleValue> _values = new Dictionary<string, RuleValue>(StringComparer.Ordinal);

        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }

        /// <summary>Valeurs indexées par chemin sous <c>data</c> (voir <see cref="RulesCatalog"/>).</summary>
        public IReadOnlyDictionary<string, RuleValue> Values { get; }

        public RulesDocument() => Values = new System.Collections.ObjectModel.ReadOnlyDictionary<string, RuleValue>(_values);

        /// <summary>Bloc <c>statuses</c> (lot 7.2) : définitions des altérations dans l'ordre du fichier ; null si absent.</summary>
        public List<StatusData?>? Statuses { get; set; }

        public void Set(string path, RuleValue value) => _values[path] = value;

        public bool Remove(string path) => _values.Remove(path);
    }
}
