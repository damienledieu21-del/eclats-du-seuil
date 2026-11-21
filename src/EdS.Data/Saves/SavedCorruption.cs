using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Data.Saves
{
    /// <summary>
    /// Sous-lot V2.42.1 (sauvegarde v32) : autels purifiés du personnage, par terre (<c>corruption.json</c>) : le jour du monde de la dernière
    /// purification de chacun. Rien d'autre n'est gardé : où en est la corruption se recalcule de ces jours et du temps du monde. Une terre
    /// que le catalogue n'a plus est gardée telle quelle, sans effet ; l'accord avec le catalogue n'est pas exigé (comme les cicatrices).
    /// </summary>
    public sealed class SavedCorruption
    {
        /// <summary>Terres au plus (borne technique, la même que celle du catalogue).</summary>
        public const int MaxEntries = 32;

        public static readonly SavedCorruption Empty = new SavedCorruption(Array.Empty<KeyValuePair<string, long>>());

        /// <summary>Jour de la dernière purification de l'autel de chaque terre, en ordre ordinal des terres.</summary>
        public IReadOnlyList<KeyValuePair<string, long>> Purified { get; }

        public bool IsEmpty => Purified.Count == 0;

        public SavedCorruption(IEnumerable<KeyValuePair<string, long>> purified)
        {
            if (purified is null) throw new ArgumentNullException(nameof(purified));
            var list = purified.OrderBy(e => e.Key, StringComparer.Ordinal).ToList();
            if (list.Count > MaxEntries) throw new ArgumentException($"{list.Count} autels purifiés, au plus {MaxEntries}.", nameof(purified));
            for (int i = 0; i < list.Count; i++)
            {
                if (!SavedRebuild.IsLocalId(list[i].Key)) throw new ArgumentException($"Terre « {list[i].Key} » hors du format des sauvegardes.", nameof(purified));
                if (i > 0 && list[i - 1].Key == list[i].Key) throw new ArgumentException($"Terre « {list[i].Key} » en double.", nameof(purified));
                if (list[i].Value < 0 || list[i].Value > SavedEvent.MaxDay) throw new ArgumentOutOfRangeException(nameof(purified), $"Jour hors de [0, {SavedEvent.MaxDay}].");
            }
            Purified = list.AsReadOnly();
        }

        /// <summary>Jour de la dernière purification de l'autel de <paramref name="area"/>, ou null (jamais purifié).</summary>
        public long? DayOf(string area)
        {
            foreach (KeyValuePair<string, long> e in Purified) if (e.Key == area) return e.Value;
            return null;
        }

        /// <summary>Même état, l'autel de <paramref name="area"/> purifié le jour <paramref name="day"/>.</summary>
        public SavedCorruption With(string area, long day) =>
            new SavedCorruption(Purified.Where(e => e.Key != area).Append(new KeyValuePair<string, long>(area, day)));
    }
}
