using System.Collections.Generic;
using System.Linq;

namespace EdS.Data.Validation
{
    /// <summary>
    /// Accumule toutes les erreurs au lieu de s'arrêter à la première, pour qu'un fichier se corrige en une passe.
    /// </summary>
    public sealed class ValidationReport
    {
        private readonly List<ValidationError> _errors = new List<ValidationError>();

        public IReadOnlyList<ValidationError> Errors { get; }

        public ValidationReport() => Errors = _errors.AsReadOnly();
        public bool IsValid => _errors.Count == 0;

        public void Add(string path, ValidationCode code, string message, long? line = null) =>
            _errors.Add(new ValidationError(path, code, message, line));

        /// <summary>Vrai si une erreur porte déjà sur ce chemin ou sur l'un de ses parents.</summary>
        public bool HasErrorAtOrAbove(string path) =>
            _errors.Any(e => path == e.Path || path.StartsWith(e.Path + ".", System.StringComparison.Ordinal));

        public override string ToString() =>
            IsValid ? "Aucune erreur." : string.Join("\n", _errors.Select(e => e.ToString()));
    }
}
