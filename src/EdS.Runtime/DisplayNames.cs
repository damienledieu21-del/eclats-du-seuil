using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Runtime
{
    /// <summary>
    /// Essai réaliste 6 (essai de Damien du 27 octobre 2026 : la fiche de la cible affichait « SANGLIER_V2 ») : nom montré d'un combattant
    /// d'après son identifiant : la vie suivante (« _v2 »), le numéro (« sanglier2 », « loup_a ») et la variante (« loup_cache ») ne se
    /// montrent pas ; les accents et les mots composés sont rendus (« rodeur » → « Rôdeur », « gardien_du_seuil » → « Gardien du Seuil »).
    /// Affichage seulement : le cœur ne connaît que les identifiants.
    /// </summary>
    public static class DisplayNames
    {
        private static readonly Dictionary<string, string> Known = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["rodeur"] = "Rôdeur",
            ["gardien_du_seuil"] = "Gardien du Seuil",
            ["gardien_eveille"] = "Gardien éveillé",
            ["geant"] = "Géant",
            ["allie"] = "Allié",
            ["duelliste"] = "Duelliste",
            ["loup_cache"] = "Loup",
            ["pillard"] = "Pillard",
        };

        /// <summary>Nom à montrer pour l'identifiant <paramref name="id"/> (vide pour un identifiant vide).</summary>
        public static string Of(string id)
        {
            string key = (id ?? string.Empty).Trim().ToLowerInvariant();
            if (key.Length == 0) return string.Empty;
            // Vie suivante : « sanglier2_v3 » → « sanglier2 ».
            int life = key.LastIndexOf("_v", StringComparison.Ordinal);
            if (life > 0 && key.Substring(life + 2).Length > 0 && key.Substring(life + 2).All(char.IsDigit)) key = key.Substring(0, life);
            if (Known.TryGetValue(key, out string known)) return known;
            // Numéro ou lettre d'exemplaire : « loup1 », « geant2 », « loup_a », « pillard_b ».
            key = key.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            int part = key.LastIndexOf('_');
            if (part > 0 && key.Length - part == 2) key = key.Substring(0, part);
            if (Known.TryGetValue(key, out known)) return known;
            string words = key.Replace('_', ' ').Trim();
            return words.Length == 0 ? id ?? string.Empty : char.ToUpperInvariant(words[0]) + words.Substring(1);
        }
    }
}
