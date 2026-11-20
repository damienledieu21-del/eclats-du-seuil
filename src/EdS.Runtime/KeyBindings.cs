using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Runtime
{
    /// <summary>Contexte des touches de l'aventure : ce qui est à l'écran décide de ce que fait une touche.</summary>
    public enum KeyContext
    {
        /// <summary>On marche et l'on combat dans le monde.</summary>
        Exploration = 0,

        /// <summary>Mode décoration de la maison : un meuble suit le pointeur ; les commandes de combat sont suspendues.</summary>
        Decoration = 1,

        /// <summary>Un panneau de l'aventure est ouvert (sac, journal, atelier, carte, compétences, maison…).</summary>
        Panel = 2,

        /// <summary>Un dialogue est ouvert.</summary>
        Dialogue = 3,
    }

    /// <summary>
    /// V2.53.3 (décision de Damien du 20 novembre 2026, « une touche, un seul usage », par contexte) : les touches du clavier de l'aventure,
    /// contexte par contexte (noms de <c>KeyCode</c> de Unity). Dans un même contexte, une touche n'a qu'un usage (testé, et chaque touche
    /// de la table est bien lue par le jeu). N est la maison (panneau « Chez soi », ou rentrer chez soi) ; H reste l'aide ; R est l'ultime en
    /// combat et tourne le meuble en mode décoration ; on quitte l'aventure par le menu de la pause.
    /// </summary>
    public static class KeyBindings
    {
        private static readonly (KeyContext Context, string Key, string Use)[] Table =
        {
            // Exploration : déplacement et vue (W A S D en QWERTY, Z Q S D en AZERTY : mêmes touches physiques), combat, écrans.
            (KeyContext.Exploration, "W", "avancer"), (KeyContext.Exploration, "A", "aller à gauche"), (KeyContext.Exploration, "S", "reculer"),
            (KeyContext.Exploration, "D", "aller à droite"), (KeyContext.Exploration, "Q", "tourner la vue à gauche"), (KeyContext.Exploration, "E", "tourner la vue à droite"),
            (KeyContext.Exploration, "Space", "parade"), (KeyContext.Exploration, "LeftControl", "esquive"),
            (KeyContext.Exploration, "Alpha1", "compétence 1"), (KeyContext.Exploration, "Alpha2", "compétence 2"), (KeyContext.Exploration, "Alpha3", "compétence 3"),
            (KeyContext.Exploration, "Alpha4", "compétence 4"), (KeyContext.Exploration, "Alpha5", "compétence 5"), (KeyContext.Exploration, "Alpha6", "compétence 6"),
            (KeyContext.Exploration, "R", "ultime"), (KeyContext.Exploration, "Tab", "cible suivante"),
            (KeyContext.Exploration, "O", "ordres au compagnon"), (KeyContext.Exploration, "Y", "attaque à deux"),
            (KeyContext.Exploration, "F", "agir"), (KeyContext.Exploration, "P", "potion"), (KeyContext.Exploration, "C", "vue derrière soi"),
            (KeyContext.Exploration, "U", "monture"), (KeyContext.Exploration, "I", "personnage"), (KeyContext.Exploration, "J", "journal"),
            (KeyContext.Exploration, "K", "atelier"), (KeyContext.Exploration, "L", "carte"), (KeyContext.Exploration, "B", "compétences"),
            (KeyContext.Exploration, "T", "talents"), (KeyContext.Exploration, "N", "maison"), (KeyContext.Exploration, "H", "aide"),
            (KeyContext.Exploration, "M", "jeu en réseau"), (KeyContext.Exploration, "Return", "halte"), (KeyContext.Exploration, "F6", "sauvegarder"),
            (KeyContext.Exploration, "F8", "commandes tactiles"), (KeyContext.Exploration, "Escape", "pause et réglages"),
            // Mode décoration : le meuble suit le pointeur (clic gauche : poser).
            (KeyContext.Decoration, "R", "tourner le meuble"), (KeyContext.Decoration, "Escape", "annuler la pose"),
            (KeyContext.Decoration, "N", "maison"), (KeyContext.Decoration, "H", "aide"),
            // Panneau ouvert.
            (KeyContext.Panel, "Escape", "fermer"), (KeyContext.Panel, "I", "personnage"), (KeyContext.Panel, "J", "journal"),
            (KeyContext.Panel, "K", "atelier"), (KeyContext.Panel, "L", "carte"), (KeyContext.Panel, "B", "compétences"), (KeyContext.Panel, "N", "maison"),
            // Dialogue ouvert.
            (KeyContext.Dialogue, "Escape", "fermer le dialogue"),
        };

        /// <summary>Les touches du contexte <paramref name="context"/>, avec leur usage, dans l'ordre de la table.</summary>
        public static IReadOnlyList<(string Key, string Use)> In(KeyContext context) =>
            Table.Where(t => t.Context == context).Select(t => (t.Key, t.Use)).ToList();

        /// <summary>Toutes les touches citées (sans doublon).</summary>
        public static IReadOnlyList<string> Keys => Table.Select(t => t.Key).Distinct(StringComparer.Ordinal).ToList();
    }
}
