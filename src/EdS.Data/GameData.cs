using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Classes;
using EdS.Data.Rules;
using EdS.Simulation.Engine;

namespace EdS.Data
{
    /// <summary>Ensemble cohérent de données validées : les règles et les classes disponibles.</summary>
    public sealed class GameData
    {
        public LoadedRules Rules { get; }

        /// <summary>Classes triées par identifiant.</summary>
        public IReadOnlyList<LoadedClass> Classes { get; }

        private readonly Dictionary<string, LoadedClass> _classes;

        public GameData(LoadedRules rules, IEnumerable<LoadedClass> classes)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Classes = (classes ?? throw new ArgumentNullException(nameof(classes))).OrderBy(c => c.Definition.Id, StringComparer.Ordinal).ToArray();
            _classes = new Dictionary<string, LoadedClass>(StringComparer.Ordinal);
            foreach (LoadedClass c in Classes)
            {
                if (_classes.ContainsKey(c.Definition.Id))
                    throw new ArgumentException($"Classe « {c.Definition.Id} » fournie deux fois.", nameof(classes));
                _classes[c.Definition.Id] = c;
            }
        }

        public bool TryGetClass(string id, out LoadedClass loaded) => _classes.TryGetValue(id, out loaded!);

        public DataFileIdentity RulesIdentity => new DataFileIdentity("rules", Rules.ContentVersion, Rules.Fingerprint);

        public static DataFileIdentity IdentityOf(LoadedClass loaded) =>
            new DataFileIdentity(loaded.Definition.Id, loaded.ContentVersion, loaded.Fingerprint);

        /// <summary>Identité de toutes les données disponibles (règles et chaque classe).</summary>
        public CombatDataIdentity Identity => new CombatDataIdentity(RulesIdentity, Classes.Select(IdentityOf));
    }
}
