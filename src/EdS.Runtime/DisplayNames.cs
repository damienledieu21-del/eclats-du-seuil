using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Runtime
{
    /// <summary>
    /// Essai réaliste 6 (essai de Damien du 27 octobre 2026 : la fiche de la cible affichait « SANGLIER_V2 ») : nom montré d'un combattant
    /// ou d'une attaque d'après son identifiant : la vie suivante (« _v2 »), le numéro (« sanglier2 », « loup_a », « coup_2 ») et la
    /// variante (« loup_cache ») ne se montrent pas ; les accents et les mots composés sont rendus (« rodeur » → « Rôdeur »,
    /// « gardien_du_seuil » → « Gardien du Seuil », « ruee » → « Ruée »). Affichage seulement : le cœur ne connaît que les identifiants.
    /// </summary>
    public static class DisplayNames
    {
        private static readonly Dictionary<string, string> Combatants = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["rodeur"] = "Rôdeur",
            ["gardien_du_seuil"] = "Gardien du Seuil",
            ["gardien_eveille"] = "Gardien éveillé",
            ["geant"] = "Géant",
            ["allie"] = "Allié",
            ["duelliste"] = "Duelliste",
            ["loup_cache"] = "Loup",
            ["pillard"] = "Pillard",
            // Sous-lot V1.6 : bandits, leur chef et le loup blanc.
            ["bandit_epee"] = "Bandit à l'épée",
            ["bandit_arc"] = "Bandit archer",
            ["chef_des_bandits"] = "Chef des bandits",
            ["chef_des_bandits_furieux"] = "Chef des bandits furieux",
            ["loup_blanc"] = "Loup blanc",
            ["le_borgne"] = "Le Borgne",   // sous-lot V1.7 : le bandit nommé (prime et épreuve de la guilde)
            ["grand_borgne"] = "Le Grand Borgne",   // sous-lot V1.9.4 : son aîné, la quête « La prime du Borgne »
            // Sous-lot V1.9.6 : le chef de la meute du pré et l'écho du souvenir de la Rupture.
            ["croc_gris"] = "Croc-Gris",
            ["croc_gris_enrage"] = "Croc-Gris enragé",
            ["echo_de_la_rupture"] = "Écho de la Rupture",
            ["voix_de_la_rupture"] = "Voix de la Rupture",   // sous-lot V1.9.9 : la seconde forme de l'écho du souvenir
        };

        private static readonly Dictionary<string, string> Attacks = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["brulure"] = "Brûlure",
            ["coup_bis"] = "Coup",
            ["coup_felant"] = "Coup fêlant",
            ["choeur"] = "Chœur",
            ["eclat"] = "Éclat",
            ["ecrasement"] = "Écrasement",
            ["fleche"] = "Flèche",
            ["onde_du_seuil"] = "Onde du Seuil",
            ["percee"] = "Percée",
            ["ruee"] = "Ruée",
            ["volee"] = "Volée",   // sous-lot V1.6
            ["charge_du_chef"] = "Charge du chef",
        };

        /// <summary>Nom à montrer pour le combattant <paramref name="id"/> (vide pour un identifiant vide).</summary>
        public static string Of(string id) => Show(id, Combatants);

        /// <summary>Nom à montrer pour l'attaque annoncée <paramref name="id"/> (« Coup » pour un identifiant vide).</summary>
        public static string Attack(string id)
        {
            string name = Show(id, Attacks);
            return name.Length == 0 ? "Coup" : name;
        }

        private static string Show(string id, Dictionary<string, string> known)
        {
            string key = (id ?? string.Empty).Trim().ToLowerInvariant();
            if (key.Length == 0) return string.Empty;
            // Vie suivante : « sanglier2_v3 » → « sanglier2 ».
            int life = key.LastIndexOf("_v", StringComparison.Ordinal);
            if (life > 0 && key.Substring(life + 2).Length > 0 && key.Substring(life + 2).All(char.IsDigit)) key = key.Substring(0, life);
            if (known.TryGetValue(key, out string name)) return name;
            // Numéro ou lettre d'exemplaire : « loup1 », « geant2 », « loup_a », « pillard_b », « coup_2 ».
            key = key.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9').TrimEnd('_');
            int part = key.LastIndexOf('_');
            if (part > 0 && key.Length - part == 2) key = key.Substring(0, part);
            if (known.TryGetValue(key, out name)) return name;
            string words = key.Replace('_', ' ').Trim();
            return words.Length == 0 ? id ?? string.Empty : char.ToUpperInvariant(words[0]) + words.Substring(1);
        }
    }
}
