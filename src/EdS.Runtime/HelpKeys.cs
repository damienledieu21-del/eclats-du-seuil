using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Runtime
{
    /// <summary>Une ligne de l'aide : touche (« Z Q S D », « Entrée ») et ce qu'elle fait (« Se déplacer »).</summary>
    public sealed class HelpKey
    {
        public string Key { get; }
        public string Action { get; }

        public HelpKey(string key, string action)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public override string ToString() => Key + " : " + Action;
    }

    /// <summary>
    /// Essai réaliste 6 (essai de Damien du 27 octobre 2026 : « l'aide, tous ces petits détails ») : l'aide des touches en lignes
    /// (une touche, une action) pour la dalle de l'interface, au lieu d'un bloc de texte. Les touches de l'aventure viennent des textes
    /// du monde (<c>text.ui.help</c> : « F parler ou récolter · I inventaire · … »), découpés ici.
    /// </summary>
    public static class HelpKeys
    {
        private static readonly HelpKey[] Combat =
        {
            new HelpKey("Z Q S D", "Se déplacer"), new HelpKey("Clic droit", "Garde (maintenir)"), new HelpKey("Espace", "Parade parfaite"),
            new HelpKey("1 – 4", "Compétences"), new HelpKey("R", "Ultime"), new HelpKey("Tab", "Cible suivante"), new HelpKey("A / E", "Tourner la vue"),
            new HelpKey("Molette", "Vue 1 ou 2"),
        };

        /// <summary>Entraînement : combat, puis scénario suivant, build, recommencer, talents, pause, réseau, aide.</summary>
        public static IReadOnlyList<HelpKey> Training { get; } = Combat.Concat(new[]
        {
            new HelpKey("N", "Scène suivante"), new HelpKey("F1 – F5", "Build"), new HelpKey("Retour", "Recommencer"), new HelpKey("T", "Talents"),
            new HelpKey("Échap", "Pause"), new HelpKey("M", "Jeu en réseau"), new HelpKey("H", "Masquer l'aide"),
        }).ToList();

        /// <summary>Réseau : combat, puis l'allié visé par les soutiens, le menu et l'aide (pas de pause : le serveur mène le combat).</summary>
        public static IReadOnlyList<HelpKey> Network { get; } = Combat.Concat(new[]
        {
            new HelpKey("G", "Allié visé"), new HelpKey("M", "Menu réseau"), new HelpKey("H", "Masquer l'aide"),
        }).ToList();

        /// <summary>
        /// Aventure : combat, puis les touches des textes du monde (<paramref name="worldHelp"/>, éléments séparés par « · », la touche
        /// d'abord), la carte, la pause, le réseau et l'aide. Un élément sans action est ignoré.
        /// </summary>
        public static IReadOnlyList<HelpKey> Adventure(string worldHelp)
        {
            var list = new List<HelpKey>(Combat);
            foreach (HelpKey key in Parse(worldHelp)) list.Add(key);
            list.Add(new HelpKey("L", "Carte"));
            list.Add(new HelpKey("Échap", "Pause"));
            list.Add(new HelpKey("M", "Jeu en réseau"));
            list.Add(new HelpKey("H", "Masquer l'aide"));
            return list;
        }

        /// <summary>« F parler ou récolter · I inventaire » → (F, Parler ou récolter), (I, Inventaire).</summary>
        public static IReadOnlyList<HelpKey> Parse(string text)
        {
            var list = new List<HelpKey>();
            if (string.IsNullOrWhiteSpace(text)) return list;
            foreach (string raw in text.Split('·'))
            {
                string item = raw.Trim();
                int space = item.IndexOf(' ');
                if (space <= 0) continue;
                string key = item.Substring(0, space).Trim(), action = item.Substring(space + 1).Trim();
                if (action.Length == 0) continue;
                list.Add(new HelpKey(key, char.ToUpperInvariant(action[0]) + action.Substring(1)));
            }
            return list;
        }
    }
}
