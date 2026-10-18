using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Builds;
using EdS.Data.Classes;
using EdS.Data.Rules;
using EdS.Domain.Combat;

namespace EdS.Data
{
    /// <summary>
    /// Ensemble cohérent de données validées : les règles, les classes disponibles et, depuis le sous-lot 8.5, les builds de référence
    /// (validés contre ces classes). Les builds n'entrent pas dans l'identité des données d'un combat (<see cref="Identity"/>) : un
    /// scénario recopie le build de chaque personnage.
    /// </summary>
    public sealed class GameData
    {
        public LoadedRules Rules { get; }

        /// <summary>Classes triées par identifiant.</summary>
        public IReadOnlyList<LoadedClass> Classes { get; }

        /// <summary>Fichiers de builds de référence, triés par classe (sous-lot 8.5) ; vide si aucun n'est fourni.</summary>
        public IReadOnlyList<LoadedBuildSet> BuildSets { get; }

        private readonly Dictionary<string, LoadedClass> _classes;
        private readonly Dictionary<string, ReferenceBuild> _builds = new Dictionary<string, ReferenceBuild>(StringComparer.Ordinal);

        public GameData(LoadedRules rules, IEnumerable<LoadedClass> classes, IEnumerable<LoadedBuildSet>? buildSets = null)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Classes = EdS.Domain.Collections.Frozen.List((classes ?? throw new ArgumentNullException(nameof(classes))).OrderBy(c => c.Definition.Id, StringComparer.Ordinal), nameof(classes));
            _classes = new Dictionary<string, LoadedClass>(StringComparer.Ordinal);
            foreach (LoadedClass c in Classes)
            {
                if (_classes.ContainsKey(c.Definition.Id))
                    throw new ArgumentException($"Classe « {c.Definition.Id} » fournie deux fois.", nameof(classes));
                _classes[c.Definition.Id] = c;
            }
            BuildSets = EdS.Domain.Collections.Frozen.List((buildSets ?? Enumerable.Empty<LoadedBuildSet>()).OrderBy(b => b.ClassId, StringComparer.Ordinal), nameof(buildSets));
            foreach (LoadedBuildSet set in BuildSets)
            {
                if (!_classes.ContainsKey(set.ClassId))
                    throw new ArgumentException($"Builds de la classe « {set.ClassId} », absente des données.", nameof(buildSets));
                foreach (ReferenceBuild build in set.Builds)
                {
                    if (_builds.ContainsKey(build.Id))
                        throw new ArgumentException($"Build « {build.Id} » fourni deux fois.", nameof(buildSets));
                    _builds[build.Id] = build;
                }
            }
        }

        public bool TryGetClass(string id, out LoadedClass loaded) => _classes.TryGetValue(id, out loaded!);

        /// <summary>Build de référence par identifiant (« build.bastion.rempart »).</summary>
        public bool TryGetBuild(string id, out ReferenceBuild build) => _builds.TryGetValue(id, out build!);

        public DataFileIdentity RulesIdentity => new DataFileIdentity("rules", Rules.ContentVersion, Rules.Fingerprint);

        public static DataFileIdentity IdentityOf(LoadedClass loaded) =>
            new DataFileIdentity(loaded.Definition.Id, loaded.ContentVersion, loaded.Fingerprint);

        /// <summary>Identité de toutes les données disponibles (règles et chaque classe).</summary>
        public CombatDataIdentity Identity => new CombatDataIdentity(RulesIdentity, Classes.Select(IdentityOf));
    }
}
