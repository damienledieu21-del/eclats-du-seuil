using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace EdS.Data.Saves
{
    /// <summary>
    /// Sous-lot V2.10 (sauvegarde v16, marchands vivants) : l'état d'un objet chez un marchand (<c>merchant.herboriste/item.potion_de_soin</c>),
    /// recalculé du temps du monde : au jour <see cref="Day"/>, il manquait <see cref="Bought"/> unités au stock plein (achetées par le
    /// personnage), et le marchand gardait <see cref="Resold"/> unités revendues par le personnage. Chaque jour du monde, le marchand en
    /// regagne une part (règles des marchands) et écoule une part des objets revendus. Une ligne revenue à rien n'est plus écrite.
    /// </summary>
    public sealed class SavedStock
    {
        public string Id { get; }
        public int Bought { get; }
        public int Resold { get; }
        public long Day { get; }

        /// <summary>Lignes gardées au plus.</summary>
        public const int MaxEntries = 256;

        /// <summary>Unités manquantes ou revendues au plus (borne d'une offre).</summary>
        public const int MaxUnits = 999;

        public const int MaxKeyLength = 128;

        private static readonly Regex Key = new Regex(@"^merchant(\.[a-z0-9_]+)+/item(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);

        /// <summary>Clé bien formée « merchant.…/item.… ».</summary>
        public static bool IsKey(string? id) => id != null && id.Length <= MaxKeyLength && Key.IsMatch(id);

        public static string KeyOf(string merchant, string item) => merchant + "/" + item;

        public SavedStock(string id, int bought, int resold, long day)
        {
            if (!IsKey(id)) throw new ArgumentException($"Ligne de stock « {id} » hors du format des sauvegardes.", nameof(id));
            if (bought < 0 || bought > MaxUnits) throw new ArgumentOutOfRangeException(nameof(bought));
            if (resold < 0 || resold > MaxUnits) throw new ArgumentOutOfRangeException(nameof(resold));
            if (bought == 0 && resold == 0) throw new ArgumentException("Ligne de stock vide : elle n'est pas écrite.", nameof(bought));
            if (day < 0 || day > SavedEvent.MaxDay) throw new ArgumentOutOfRangeException(nameof(day));
            Id = id;
            Bought = bought;
            Resold = resold;
            Day = day;
        }

        public string Merchant => Id.Substring(0, Id.IndexOf('/'));
        public string Item => Id.Substring(Id.IndexOf('/') + 1);

        /// <summary>Lignes rangées par identifiant (forme canonique), une seule par identifiant.</summary>
        public static IReadOnlyList<SavedStock> Canonical(IEnumerable<SavedStock>? lines)
        {
            var list = (lines ?? Array.Empty<SavedStock>()).ToList();
            if (list.Any(e => e is null)) throw new ArgumentNullException(nameof(lines));
            if (list.Count > MaxEntries) throw new ArgumentException($"{list.Count} lignes de stock, au plus {MaxEntries}.", nameof(lines));
            if (list.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() != list.Count) throw new ArgumentException("Ligne de stock en double.", nameof(lines));
            return new ReadOnlyCollection<SavedStock>(list.OrderBy(e => e.Id, StringComparer.Ordinal).ToList());
        }
    }

    /// <summary>Sous-lot V2.10 (version 16) : une ligne de stock telle que lue, avant validation.</summary>
    public sealed class SavedStockData
    {
        public string? Id { get; set; }
        public int? Bought { get; set; }
        public int? Resold { get; set; }
        public long? Day { get; set; }
    }
}
