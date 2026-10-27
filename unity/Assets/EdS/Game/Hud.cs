using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot 9.3 : outils de l'interface dessinée avec IMGUI (panneaux, barres, textes, boutons, champs), coordonnées dans un écran
    /// virtuel de 1080 pixels de haut. Essai réaliste 5 : cadres d'or et police Cinzel (<see cref="HudArt"/>).
    /// <para>
    /// Essai réaliste 6 : l'interface en jeu (sceau, compétences, cible, étiquettes, aide, carte, quête) est passée en uGUI
    /// (<see cref="HudView"/>) ; IMGUI ne sert plus qu'aux écrans (menus, salle d'attente, talents, sac, journal, atelier, dialogues, grande
    /// carte) et aux commandes tactiles, en attendant leur tour (étape 2 de l'essai 6).
    /// </para>
    /// </summary>
    public static class Hud
    {
        public const float VirtualHeight = 1080f;

        private static GUIStyle _label;
        private static GUIStyle _center;
        private static GUIStyle _small;
        private static GUIStyle _title;
        private static GUIStyle _button;
        private static GUIStyle _field;

        public static float Scale => Screen.height / VirtualHeight;
        public static float Width => Screen.width / Scale;

        private static void Styles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true };
            _label.normal.textColor = HudArt.Parchment;
            _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _small = new GUIStyle(_label) { fontSize = 16 };
            _title = new GUIStyle(_label) { fontSize = 26, fontStyle = FontStyle.Bold };
            _title.normal.textColor = HudArt.Gold;
            _button = new GUIStyle(GUI.skin.button) { fontSize = 19, richText = true };
            HudArt.StyleButton(_button);
            _field = new GUIStyle(GUI.skin.textField) { fontSize = 20 };
            HudArt.StyleField(_field);
            Font font = HudArt.Font;
            if (font != null) _title.font = font;
        }

        public static void Begin()
        {
            Styles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
        }

        public static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        /// <summary>Essai réaliste 5 : panneau (fond <paramref name="color"/>) dans un cadre d'or, pour les écrans et les fiches.</summary>
        public static void Panel(Rect rect, Color color)
        {
            Fill(rect, color);
            HudArt.Frame(rect);
        }

        public static void Bar(Rect rect, float fraction, Color color, string text)
        {
            HudArt.Bar(rect, fraction, color);
            if (string.IsNullOrEmpty(text)) return;
            // Ombre portée : texte lisible sur une barre claire (endurance jaune, essai de Damien du 18/10).
            Centered(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), text, new Color(0f, 0f, 0f, 0.9f));
            Centered(rect, text, Color.white);
        }

        public static void Text(Rect rect, string text) => GUI.Label(rect, text, _label);

        /// <summary>
        /// Sous-lot 9.4 : bouton ; <paramref name="available"/> faux le grise sans le désactiver, pour qu'un clic affiche la raison du refus.
        /// </summary>
        public static bool Button(Rect rect, string text, bool available)
        {
            Color previous = GUI.color;
            if (!available) GUI.color = new Color(1f, 1f, 1f, 0.35f);
            bool clicked = GUI.Button(rect, text, _button);
            GUI.color = previous;
            return clicked;
        }

        public static void Small(Rect rect, string text) => GUI.Label(rect, text, _small);

        /// <summary>
        /// Revue Codex de la PR #21 : texte venu d'ailleurs (nom d'un joueur, build, raison envoyée par le serveur, adresse tapée) rendu
        /// inoffensif pour le texte enrichi : ses chevrons ne peuvent plus former de balise (taille, couleur…).
        /// </summary>
        public static string Plain(string text) => text?.Replace("<", "‹").Replace(">", "›");

        /// <summary>Sous-lot 10.4 : champ de saisie d'une ligne (nom du joueur, adresse de l'hôte).</summary>
        public static string Field(Rect rect, string text, int maxLength) => GUI.TextField(rect, text ?? "", maxLength, _field);

        public static void Title(Rect rect, string text) => GUI.Label(rect, text, _title);

        public static void Centered(Rect rect, string text, Color color)
        {
            Color previous = _center.normal.textColor;
            _center.normal.textColor = color;
            GUI.Label(rect, text, _center);
            _center.normal.textColor = previous;
        }
    }
}
