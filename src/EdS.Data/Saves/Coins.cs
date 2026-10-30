using System;
using System.Collections.Generic;
using System.Globalization;

namespace EdS.Data.Saves
{
    /// <summary>
    /// Sous-lot V1.1 (choix de Damien du 29 octobre 2026, 12 h 57) : la monnaie du jeu, en trois pièces : cuivre, argent et or. 100 cuivre
    /// font 1 argent, 100 argent font 1 or. Elle est gardée avec le personnage comme un seul nombre de pièces de cuivre, borné
    /// (<see cref="Max"/>), et toute opération est vérifiée : un dépassement est refusé, jamais tronqué. Jamais d'argent réel (sujet 6).
    /// </summary>
    public static class Coins
    {
        public const long CopperPerSilver = 100, SilverPerGold = 100, CopperPerGold = CopperPerSilver * SilverPerGold;

        /// <summary>
        /// Plafond de la bourse (borne technique, réglable avec une nouvelle version de la sauvegarde) : 999 999 or 99 argent 99 cuivre.
        /// </summary>
        public const long Max = 9_999_999_999;

        /// <summary>La somme est dans les bornes de la bourse (de 0 à <see cref="Max"/>).</summary>
        public static bool IsValid(long copper) => copper >= 0 && copper <= Max;

        /// <summary>Or, argent et cuivre d'une somme en cuivre (« 34512 » : 3 or, 45 argent, 12 cuivre).</summary>
        /// <exception cref="ArgumentOutOfRangeException">Somme hors de [0, <see cref="Max"/>].</exception>
        public static (long Gold, int Silver, int Copper) Split(long copper)
        {
            if (!IsValid(copper)) throw new ArgumentOutOfRangeException(nameof(copper), copper, $"Somme hors de [0, {Max}] cuivre.");
            return (copper / CopperPerGold, (int)(copper / CopperPerSilver % SilverPerGold), (int)(copper % CopperPerSilver));
        }

        /// <summary>Somme en cuivre de <paramref name="gold"/> or, <paramref name="silver"/> argent et <paramref name="copper"/> cuivre.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Une part négative, ou une somme hors de la bourse.</exception>
        public static long Of(long gold, long silver, long copper)
        {
            if (gold < 0 || silver < 0 || copper < 0) throw new ArgumentOutOfRangeException(nameof(gold), "Parts positives ou nulles attendues.");
            long total;
            try
            {
                total = checked(gold * CopperPerGold + silver * CopperPerSilver + copper);
            }
            catch (OverflowException)
            {
                throw new ArgumentOutOfRangeException(nameof(gold), $"Somme au-delà de {Max} cuivre.");
            }
            if (total > Max) throw new ArgumentOutOfRangeException(nameof(gold), $"Somme au-delà de {Max} cuivre.");
            return total;
        }

        /// <summary>
        /// Bourse <paramref name="purse"/> plus <paramref name="change"/> (positif ou négatif), dans [0, <see cref="Max"/>] ; faux, sans
        /// rien rendre d'autre que <paramref name="purse"/>, si le résultat sort des bornes (bourse vide ou pleine).
        /// </summary>
        public static bool TryAdd(long purse, long change, out long result)
        {
            result = purse;
            if (!IsValid(purse)) return false;
            // Bourse bornée à 10^10 et changement quelconque : la somme ne déborde pas d'un long tant que le changement tient dans ±2^62.
            if (change > Max || change < -Max) return false;
            long sum = checked(purse + change);
            if (!IsValid(sum)) return false;
            result = sum;
            return true;
        }

        /// <summary>
        /// Texte d'une somme : les pièces non nulles, de l'or au cuivre (« 3 or 45 argent 12 cuivre », « 5 argent ») ; « 0 cuivre » pour
        /// une bourse vide. <paramref name="gold"/>, <paramref name="silver"/> et <paramref name="copper"/> sont les formats de chaque pièce
        /// (textes du monde : « {0} or »).
        /// </summary>
        public static string Format(long amount, string gold = "{0} or", string silver = "{0} argent", string copper = "{0} cuivre")
        {
            (long g, int s, int c) = Split(amount);
            var parts = new List<string>(3);
            if (g > 0) parts.Add(string.Format(CultureInfo.InvariantCulture, gold ?? "{0}", g));
            if (s > 0) parts.Add(string.Format(CultureInfo.InvariantCulture, silver ?? "{0}", s));
            if (c > 0 || parts.Count == 0) parts.Add(string.Format(CultureInfo.InvariantCulture, copper ?? "{0}", c));
            return string.Join(" ", parts);
        }
    }
}
