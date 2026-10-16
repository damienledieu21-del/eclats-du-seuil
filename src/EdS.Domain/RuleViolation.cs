using System;
using System.Collections.Generic;

namespace EdS.Domain
{
    /// <summary>
    /// Incohérence d'un paramètre de gameplay, détectée par les règles du domaine elles-mêmes.
    /// <see cref="Fields"/> nomme les propriétés concernées (noms C#), pour qu'un chargeur de données
    /// puisse les rattacher à l'emplacement exact dans son fichier. C'est l'unique validation de gameplay :
    /// les chargeurs ne vérifient que la forme des données.
    /// </summary>
    public sealed class RuleViolation
    {
        public IReadOnlyList<string> Fields { get; }
        public string Message { get; }

        public RuleViolation(string message, params string[] fields)
        {
            if (fields is null || fields.Length == 0) throw new ArgumentException("Au moins un champ attendu.", nameof(fields));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Fields = fields;
        }

        public override string ToString() => $"{string.Join(", ", Fields)} : {Message}";
    }
}
