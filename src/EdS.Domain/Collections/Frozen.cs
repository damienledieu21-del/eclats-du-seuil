using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EdS.Domain.Collections
{
    /// <summary>
    /// Copies défensives figées (correctif 6.1, M05). Une définition, une règle ou une description de combat copie
    /// toute collection reçue au moment de sa construction et n'expose que des enveloppes en lecture seule :
    /// modifier la collection d'origine après coup, ou transtyper la propriété exposée (<c>(IList&lt;T&gt;)</c>,
    /// <c>(T[])</c>, <c>(Dictionary&lt;,&gt;)</c>), ne change jamais ce que l'objet représente. Les éléments imbriqués
    /// sont eux-mêmes immuables (ils font leurs propres copies), si bien que l'ensemble reste figé à toute profondeur.
    /// </summary>
    public static class Frozen
    {
        /// <summary>Liste vide partagée, en lecture seule.</summary>
        public static IReadOnlyList<T> EmptyList<T>() => Empty<T>.List;

        /// <summary>Copie figée ; refuse une source nulle ou un élément nul.</summary>
        public static IReadOnlyList<T> List<T>(IEnumerable<T> source, string paramName)
        {
            if (source is null) throw new ArgumentNullException(paramName);
            T[] copy = source.ToArray();
            for (int i = 0; i < copy.Length; i++)
                if (copy[i] is null) throw new ArgumentException($"Élément nul à la position {i}.", paramName);
            return copy.Length == 0 ? Empty<T>.List : new ReadOnlyCollection<T>(copy);
        }

        /// <summary>Copie figée, ou liste vide si la source est nulle.</summary>
        public static IReadOnlyList<T> ListOrEmpty<T>(IEnumerable<T>? source, string paramName) =>
            source is null ? Empty<T>.List : List(source, paramName);

        /// <summary>Copie figée qui conserve une source nulle (pour qu'un validateur la signale) mais refuse un élément nul.</summary>
        public static IReadOnlyList<T>? ListOrNull<T>(IEnumerable<T>? source, string paramName) =>
            source is null ? null : List(source, paramName);

        /// <summary>Copie figée d'un dictionnaire à clés ordinales ; refuse une source nulle.</summary>
        public static IReadOnlyDictionary<string, TValue> Dictionary<TValue>(IEnumerable<KeyValuePair<string, TValue>> source, string paramName)
        {
            if (source is null) throw new ArgumentNullException(paramName);
            var copy = new Dictionary<string, TValue>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, TValue> entry in source)
            {
                if (entry.Key is null) throw new ArgumentException("Clé nulle.", paramName);
                if (copy.ContainsKey(entry.Key)) throw new ArgumentException($"Clé « {entry.Key} » en double.", paramName);
                copy.Add(entry.Key, entry.Value);
            }
            return new ReadOnlyDictionary<string, TValue>(copy);
        }

        private static class Empty<T>
        {
            public static readonly IReadOnlyList<T> List = new ReadOnlyCollection<T>(Array.Empty<T>());
        }
    }
}
