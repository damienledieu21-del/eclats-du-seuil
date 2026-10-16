using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace EdS.Data
{
    /// <summary>
    /// Correctif 7.3.1 (A73-M02) : écriture canonique injective des empreintes de données. Une ligne par champ, dans un ordre fixé par
    /// l'appelant : <c>chemin=valeur</c> pour une valeur présente (échappée : barre oblique inverse, retour à la ligne, retour chariot),
    /// <c>chemin?</c> pour un champ facultatif absent ; une liste s'écrit <c>chemin.count=n</c> puis <c>chemin[i]=élément</c>. Aucune
    /// valeur textuelle ne sert de sentinelle : un champ absent ne peut plus être confondu avec un identifiant valide comme <c>none</c>,
    /// ni une liste avec un élément contenant une virgule. Deux configurations sémantiquement différentes donnent deux textes différents.
    /// </summary>
    public sealed class CanonicalWriter
    {
        /// <summary>
        /// Version du contrat d'écriture canonique, inscrite en tête du texte haché. 1 : lots 1 à 7.3 (sentinelle <c>none</c>, listes
        /// jointes par des virgules). 2 : correctif 7.3.1. Les empreintes changent toutes : un journal antérieur est refusé au rejeu.
        /// </summary>
        public const int Version = 2;

        private readonly StringBuilder _text;

        public CanonicalWriter(StringBuilder text) => _text = text;

        /// <summary>Valeur présente.</summary>
        public void Value(string path, string value) => _text.Append(path).Append('=').Append(Escape(value)).Append('\n');

        public void Value(string path, long value) => Value(path, value.ToString(CultureInfo.InvariantCulture));

        public void Value(string path, bool value) => Value(path, value ? "true" : "false");

        /// <summary>Champ facultatif : absent (<c>chemin?</c>) ou présent (<c>chemin=valeur</c>).</summary>
        public void Optional(string path, string? value)
        {
            if (value is null) _text.Append(path).Append("?\n");
            else Value(path, value);
        }

        /// <summary>Liste : nombre d'éléments, puis chacun à son indice.</summary>
        public void List(string path, IEnumerable<string> items)
        {
            var list = new List<string>(items);
            Value(path + ".count", list.Count);
            for (int i = 0; i < list.Count; i++) Value($"{path}[{i}]", list[i]);
        }

        public static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r");
    }
}
