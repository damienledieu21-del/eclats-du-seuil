using System;
using System.Collections.Generic;

namespace EdS.Domain
{
    /// <summary>Nature d'une incohérence, pour que les chargeurs choisissent le bon code d'erreur.</summary>
    public enum RuleViolationKind
    {
        /// <summary>Valeur bien formée mais refusée (borne, incohérence entre champs).</summary>
        InvalidValue = 0,

        /// <summary>Identifiant qui ne désigne aucune définition connue.</summary>
        UnknownReference = 1,

        /// <summary>Identifiant déjà utilisé par une autre définition.</summary>
        DuplicateId = 2,
    }

    /// <summary>
    /// Incohérence de gameplay détectée par le domaine lui-même. <see cref="Fields"/> nomme les éléments concernés :
    /// noms de propriétés C# pour les règles (ex. <c>ArmorConstant</c>), chemins logiques pour le contenu
    /// (ex. <c>trees[0].talents[2].maxRank</c>, même forme que les fichiers de données). Un chargeur rattache ainsi
    /// chaque erreur à l'emplacement exact de son fichier. C'est l'unique validation de gameplay :
    /// les chargeurs ne vérifient que la forme des données.
    /// </summary>
    public sealed class RuleViolation
    {
        public IReadOnlyList<string> Fields { get; }
        public string Message { get; }
        public RuleViolationKind Kind { get; }

        public RuleViolation(string message, params string[] fields)
            : this(RuleViolationKind.InvalidValue, message, fields)
        {
        }

        public RuleViolation(RuleViolationKind kind, string message, params string[] fields)
        {
            if (fields is null || fields.Length == 0) throw new ArgumentException("Au moins un champ attendu.", nameof(fields));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Fields = Collections.Frozen.List(fields, nameof(fields));
            Kind = kind;
        }

        public override string ToString() => $"{string.Join(", ", Fields)} : {Message}";
    }
}
