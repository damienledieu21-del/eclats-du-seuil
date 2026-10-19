using System.Linq;
using EdS.Runtime;
using EdS.Simulation.Engine;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot 9.3 : interface provisoire dessinée avec IMGUI (aucun fichier d'interface à préparer) : barres de PV, d'endurance, de
    /// Résonance et de Charges, emplacements de compétences avec leur recharge, altérations, fiche de la cible, barres et télégraphie
    /// au-dessus des combattants, aide des touches. Coordonnées dans un écran virtuel de 1080 pixels de haut.
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
            _label.normal.textColor = Color.white;
            _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _small = new GUIStyle(_label) { fontSize = 16 };
            _title = new GUIStyle(_label) { fontSize = 26, fontStyle = FontStyle.Bold };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 20, richText = true };
            _field = new GUIStyle(GUI.skin.textField) { fontSize = 20 };
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

        public static void Bar(Rect rect, float fraction, Color color, string text)
        {
            Fill(rect, new Color(0f, 0f, 0f, 0.6f));
            Fill(new Rect(rect.x + 2, rect.y + 2, (rect.width - 4) * Mathf.Clamp01(fraction), rect.height - 4), color);
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

        /// <summary>Barres, emplacements et altérations du joueur, en bas de l'écran.</summary>
        public static void Player(PlayerState state)
        {
            CombatantView me = state.View;
            float width = Width, x = width / 2f - 360f, y = VirtualHeight - 230f;
            Fill(new Rect(x - 12, y - 12, 744, 220), new Color(0f, 0f, 0f, 0.35f));
            long health = me.Health ?? 0, maxHealth = me.MaxHealth ?? 1;
            Bar(new Rect(x, y, 720, 30), health / (float)maxHealth, new Color(0.75f, 0.15f, 0.15f), $"PV {health} / {maxHealth}");
            Bar(new Rect(x, y + 34, 720, 22), me.Endurance / (float)System.Math.Max(1, me.MaxEndurance), new Color(0.85f, 0.7f, 0.15f),
                $"Endurance {me.Endurance} / {me.MaxEndurance}");
            Bar(new Rect(x, y + 60, 470, 22), me.Resonance / (float)System.Math.Max(1, state.ResonanceMax), new Color(0.55f, 0.3f, 0.85f),
                $"Résonance {me.Resonance} / {state.ResonanceMax}");
            for (int i = 0; i < state.RampartChargesMax; i++)
                Fill(new Rect(x + 480 + i * 48, y + 60, 42, 22), i < me.RampartCharges ? new Color(0.35f, 0.75f, 1f) : new Color(0.2f, 0.2f, 0.25f, 0.8f));

            string guard = me.ParryOpen ? "<color=#ffd84a>PARADE</color>" : me.Guarding ? "<color=#b9c3d3>EN GARDE</color>" : "garde baissée";
            if (state.ParryRecoverySeconds > 0) guard += $"  <color=#ff8c3a>récupération {state.ParryRecoverySeconds:0.0} s</color>";
            Text(new Rect(x, y + 86, 720, 26), guard);

            for (int i = 0; i < state.Slots.Count; i++)
            {
                SkillSlot slot = state.Slots[i];
                var rect = new Rect(x + i * 145, y + 116, 138, 72);
                bool ready = slot.Unavailable == null;
                Fill(rect, ready ? new Color(0.18f, 0.32f, 0.22f, 0.9f) : new Color(0.15f, 0.15f, 0.17f, 0.9f));
                if (slot.RemainingSeconds > 0 && slot.CooldownSeconds > 0)
                    Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01((float)(slot.RemainingSeconds / slot.CooldownSeconds)), rect.height),
                        new Color(0f, 0f, 0f, 0.55f));
                string key = slot.IsUltimate ? "R" : (slot.Slot + 1).ToString();
                Small(new Rect(rect.x + 6, rect.y + 2, rect.width - 8, 22), $"<b>[{key}]</b> {slot.Name}");
                Small(new Rect(rect.x + 6, rect.y + 24, rect.width - 8, 44),
                    ready ? $"<color=#9be59b>prête</color> · {slot.EnduranceCost} end." : $"<color=#c8c8c8>{slot.Unavailable}</color>");
            }

            if (me.States.Count > 0)
                Small(new Rect(x, y - 36, 720, 26), "Altérations : " + string.Join(", ", me.States.Select(Labels.State)));
        }

        /// <summary>Fiche de la cible, en haut à droite.</summary>
        public static void Target(OtherState target)
        {
            if (target == null) return;
            float x = Width - 380f, y = 20f;
            Fill(new Rect(x - 10, y - 6, 370, 132), new Color(0f, 0f, 0f, 0.4f));
            Title(new Rect(x, y, 360, 34), "Cible : " + target.Id);
            CombatantView v = target.View;
            string health = v.Health is long h ? $"PV {h} / {v.MaxHealth}" : "PV infinis (entraînement)";
            Text(new Rect(x, y + 36, 360, 26), health);
            if (v.NextAttack != null && target.NextAttackSeconds is double next)
                Text(new Rect(x, y + 62, 360, 26),
                    $"Prochain coup : {v.NextAttack.Id} dans {next:0.0} s{(v.NextAttack.Unblockable ? " <color=#ff5a4a>IMPARABLE</color>" : "")}");
            if (v.States.Count > 0) Small(new Rect(x, y + 90, 360, 26), string.Join(", ", v.States.Select(Labels.State)));
        }

        /// <summary>Barre et télégraphie au-dessus d'un combattant (position à l'écran donnée en pixels réels).</summary>
        public static void Overhead(Vector3 screen, OtherState other, bool isTarget)
        {
            if (screen.z <= 0f) return;
            float x = screen.x / Scale, y = (Screen.height - screen.y) / Scale;
            CombatantView v = other.View;
            if (v.Health is long h && v.MaxHealth is long max && max > 0)
                Bar(new Rect(x - 60, y - 40, 120, 14), h / (float)max, new Color(0.75f, 0.15f, 0.15f), "");
            if (v.NextAttack != null && other.NextAttackSeconds is double next && !v.IsDead)
            {
                // Télégraphie : la barre se remplit à l'approche du coup (sur 2 s), rouge vif pour une attaque imparable.
                float fill = 1f - Mathf.Clamp01((float)next / 2f);
                Color color = v.NextAttack.Unblockable ? new Color(1f, 0.15f, 0.1f) : new Color(1f, 0.6f, 0.15f);
                Bar(new Rect(x - 60, y - 22, 120, 10), fill, color, "");
            }
            Centered(new Rect(x - 100, y - 70, 200, 26), (isTarget ? "» " : "") + other.Id, isTarget ? new Color(1f, 0.9f, 0.3f) : Color.white);
        }

        public static void Help(string scenario, string build, string camera, bool paused, bool network)
        {
            float y = 20f;
            Fill(new Rect(10, 10, 560, 324), new Color(0f, 0f, 0f, 0.4f));
            Title(new Rect(20, y, 540, 34), "Éclats du Seuil — prototype");
            Small(new Rect(20, y + 36, 540, 24), $"{(network ? "Réseau" : "Scénario")} : {scenario}  ·  Build : {build}");
            Small(new Rect(20, y + 60, 540, 24), $"Caméra : {camera}{(paused ? "  ·  <color=#ffd84a>PAUSE</color>" : "")}");
            // Sous-lot 10.4 : en réseau, pas de pause ni de scénario suivant (le serveur mène le combat) ; G vise l'autre joueur.
            Small(new Rect(20, y + 92, 540, 230),
                "ZQSD / flèches : se déplacer   ·   clic droit ou Maj : garde (maintenir)\n" +
                "Espace : parade parfaite (juste avant l'impact)   ·   1 à 4 : compétences   ·   R : ultime\n" +
                "Tab : changer de cible   ·   C : caméra   ·   A / E : tourner (isométrique)   ·   molette : zoom\n" +
                (network
                    ? "G : allié visé par les soutiens   ·   F1 à F5 et T (salle d'attente) : build\nM : menu réseau   ·   H : masquer l'aide"
                    : "N : scénario suivant   ·   F1 à F5 : build   ·   Retour arrière : recommencer\n" +
                      "T : talents   ·   Échap : pause   ·   M : jeu en réseau   ·   H : masquer l'aide"));
        }
    }
}
