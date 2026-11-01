using System.Collections.Generic;
using System.Linq;

namespace EdS.Data.Validation
{
    /// <summary>
    /// Accumule toutes les erreurs au lieu de s'arrêter à la première, pour qu'un fichier se corrige en une passe.
    /// <para>Lot 7.1 : seules les <see cref="InputLimits.MaxReportedErrors"/> premières erreurs sont conservées ; les
    /// suivantes sont comptées dans <see cref="OmittedCount"/> et résumées par une dernière ligne « … et N autres erreurs ».</para>
    /// </summary>
    public sealed class ValidationReport
    {
        private readonly List<ValidationError> _errors = new List<ValidationError>();

        // Chemins de toutes les erreurs, conservées ou non, pour que HasErrorAtOrAbove ne dépende pas du plafond.
        private readonly HashSet<string> _paths = new HashSet<string>(System.StringComparer.Ordinal);

        /// <summary>Erreurs conservées, au plus <see cref="InputLimits.MaxReportedErrors"/>, dans l'ordre de détection.</summary>
        public IReadOnlyList<ValidationError> Errors { get; }

        /// <summary>Nombre total d'erreurs détectées, y compris celles qui n'ont pas été conservées.</summary>
        public int TotalCount { get; private set; }

        /// <summary>Erreurs détectées au-delà du plafond, comptées mais non conservées.</summary>
        public int OmittedCount => TotalCount - _errors.Count;

        private readonly List<ValidationError> _warnings = new List<ValidationError>();

        /// <summary>
        /// Avertissements (lot 7.3 : cycles statiques de déclencheurs, A15) : ils n'empêchent pas le chargement et ne comptent pas
        /// dans <see cref="TotalCount"/>. Au plus <see cref="InputLimits.MaxReportedErrors"/> conservés.
        /// </summary>
        public IReadOnlyList<ValidationError> Warnings { get; }

        public ValidationReport()
        {
            Errors = _errors.AsReadOnly();
            Warnings = _warnings.AsReadOnly();
        }

        public void AddWarning(string path, string message) => AddWarning(path, ValidationCode.TriggerCycle, message);

        /// <summary>Avertissement d'une autre nature que les cycles de déclencheurs (sous-lot V1.8 : compétence en réserve, niveau au-dessus du plafond).</summary>
        public void AddWarning(string path, ValidationCode code, string message)
        {
            if (_warnings.Count < InputLimits.MaxReportedErrors) _warnings.Add(new ValidationError(path, code, message));
        }
        public bool IsValid => TotalCount == 0;

        public void Add(string path, ValidationCode code, string message, long? line = null)
        {
            TotalCount++;
            _paths.Add(path);
            if (_errors.Count < InputLimits.MaxReportedErrors) _errors.Add(new ValidationError(path, code, message, line));
        }

        /// <summary>Vrai si une erreur porte déjà sur ce chemin ou sur l'un de ses parents.</summary>
        public bool HasErrorAtOrAbove(string path)
        {
            if (_paths.Contains(path)) return true;
            for (int i = path.IndexOf('.'); i >= 0; i = path.IndexOf('.', i + 1))
                if (_paths.Contains(path.Substring(0, i))) return true;
            return false;
        }

        /// <summary>Dernière ligne d'un rapport tronqué, ou null s'il est complet.</summary>
        public string? OmittedSummary =>
            OmittedCount == 0 ? null : OmittedCount == 1 ? "… et 1 autre erreur" : $"… et {OmittedCount} autres erreurs";

        public override string ToString() =>
            IsValid ? "Aucune erreur." : string.Join("\n", _errors.Select(e => e.ToString()).Concat(OmittedSummary is { } s ? new[] { s } : new string[0]));
    }
}
