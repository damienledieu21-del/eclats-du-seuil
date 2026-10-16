using EdS.Data.Validation;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Stats;

namespace EdS.Data.Rules
{
    /// <summary>Règles validées, prêtes pour le moteur, avec leur identité (versions, empreinte).</summary>
    public sealed class LoadedRules
    {
        public int SchemaVersion { get; }
        public string ContentVersion { get; }

        /// <summary>Empreinte SHA-256 des valeurs, indépendante de la mise en forme du fichier (voir <see cref="RulesFingerprint"/>).</summary>
        public string Fingerprint { get; }

        public StatRules Stats { get; }
        public DamageRules Damage { get; }
        public ProgressionRules Progression { get; }
        public CombatRules Combat { get; }

        internal LoadedRules(int schemaVersion, string contentVersion, string fingerprint, StatRules stats, DamageRules damage,
            ProgressionRules progression, CombatRules combat)
        {
            Combat = combat;
            Progression = progression;
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            Fingerprint = fingerprint;
            Stats = stats;
            Damage = damage;
        }
    }

    /// <summary>Résultat d'un chargement : des règles utilisables, ou le rapport qui explique pourquoi pas.</summary>
    public sealed class RulesLoadResult
    {
        public ValidationReport Report { get; }

        /// <summary>Non nul si et seulement si <see cref="ValidationReport.IsValid"/>.</summary>
        public LoadedRules? Rules { get; }

        public bool IsValid => Rules != null;

        internal RulesLoadResult(ValidationReport report, LoadedRules? rules)
        {
            Report = report;
            Rules = rules;
        }
    }
}
