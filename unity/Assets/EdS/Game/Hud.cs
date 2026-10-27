using System.Linq;
using EdS.Runtime;
using EdS.Simulation.Engine;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot 9.3 : interface dessinée avec IMGUI (aucun fichier d'interface à préparer) : barres de PV, d'endurance, de Résonance et de
    /// Charges, emplacements de compétences avec leur recharge, altérations, fiche de la cible, barres et télégraphie au-dessus des
    /// combattants, aide des touches. Coordonnées dans un écran virtuel de 1080 pixels de haut.
    /// <para>
    /// Essai réaliste 5 (demande de Damien du 27 octobre 2026 : « améliorer complètement l'esthétisme de l'interface », style choisi :
    /// Diablo 4, avec des icônes au lieu de texte) : globes de vie et d'endurance de chaque côté, compétences en icônes au milieu (touche
    /// dans le coin, recharge en arc, coût d'endurance), Résonance en barre fine et Charges en losanges, cadres d'or, police Cinzel pour
    /// les titres et les chiffres (<see cref="HudArt"/>).
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

        /// <summary>Essai réaliste 5 : chiffres des globes et des recharges, touches des cases, noms (Cinzel).</summary>
        private static GUIStyle _number;
        private static GUIStyle _key;
        private static GUIStyle _name;

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
            _number = new GUIStyle(_center) { fontSize = 24 };
            _key = new GUIStyle(_center) { fontSize = 15 };
            _name = new GUIStyle(_center) { fontSize = 17 };
            Font font = HudArt.Font;
            if (font != null)
            {
                _title.font = font;
                _number.font = font;
                _key.font = font;
                _name.font = font;
            }
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

        /// <summary>Couleur du liquide des globes et des barres (essai réaliste 5).</summary>
        private static readonly Color HealthColor = new Color(0.78f, 0.07f, 0.05f), EnduranceColor = new Color(0.95f, 0.66f, 0.12f),
            ResonanceColor = new Color(0.62f, 0.32f, 0.95f), ChargeColor = new Color(0.4f, 0.78f, 1f);

        /// <summary>
        /// Essai réaliste 5 (style Diablo 4, choix de Damien) : en bas de l'écran, globe de vie à gauche et d'endurance à droite, entre eux
        /// les compétences en icônes (touches 1 à 4, puis l'ultime R), la Résonance et les Charges au-dessus, la garde en icône à côté du
        /// globe de vie, les altérations au-dessus de tout. <paramref name="compact"/> : plus petit et plus serré, quand les commandes
        /// tactiles occupent les coins (téléphone).
        /// </summary>
        public static void Player(PlayerState state, bool compact = false)
        {
            CombatantView me = state.View;
            float width = Width, cx = width / 2f, h = VirtualHeight;
            float radius = compact ? 64f : 86f, size = compact ? 66f : 82f, gap = compact ? 8f : 10f, ultimateGap = compact ? 12f : 20f;
            int count = state.Slots.Count;
            int regular = state.Slots.Count(s => !s.IsUltimate);
            float barWidth = regular * size + System.Math.Max(0, regular - 1) * gap + (count > regular ? ultimateGap + size : 0f);
            float plateHalf = barWidth / 2f + 26f;
            float slotY = h - size - (compact ? 14f : 22f);

            // Socle sombre qui relie les deux globes.
            float plateTop = slotY - (compact ? 40f : 48f);
            Panel(new Rect(cx - plateHalf, plateTop, 2f * plateHalf, h - plateTop - 6f), new Color(0.04f, 0.035f, 0.03f, 0.78f));

            long health = me.Health ?? 0, maxHealth = me.MaxHealth ?? 1;
            var left = new Vector2(cx - plateHalf - radius + 14f, h - radius - (compact ? 10f : 14f));
            var right = new Vector2(cx + plateHalf + radius - 14f, left.y);
            HudArt.Orb(left, radius, health / (float)System.Math.Max(1, maxHealth), HealthColor, $"{health}", _number);
            HudArt.Orb(right, radius, me.Endurance / (float)System.Math.Max(1, me.MaxEndurance), EnduranceColor, $"{me.Endurance}", _number);
            HudArt.Shadowed(new Rect(left.x - radius, left.y + 14f, 2f * radius, 22f), $"/ {maxHealth}", _key, new Color(0.9f, 0.8f, 0.75f, 0.85f));
            HudArt.Shadowed(new Rect(right.x - radius, right.y + 14f, 2f * radius, 22f), $"/ {me.MaxEndurance}", _key, new Color(0.95f, 0.88f, 0.7f, 0.85f));

            // Compétences : icônes, touche, recharge en arc, coût.
            float x = cx - barWidth / 2f;
            foreach (SkillSlot slot in state.Slots.Where(s => !s.IsUltimate).Concat(state.Slots.Where(s => s.IsUltimate)))
            {
                if (slot.IsUltimate) x += ultimateGap - gap;
                var rect = new Rect(x, slotY, size, size);
                bool ready = slot.Unavailable == null;
                bool affordable = me.Endurance >= slot.EnduranceCost;
                string key = slot.IsUltimate ? "R" : (slot.Slot + 1).ToString();
                HudArt.Slot(rect, HudArt.Icon(slot.SkillId), key, slot.RemainingSeconds, slot.CooldownSeconds, ready, slot.IsUltimate, slot.EnduranceCost, affordable,
                    _key, _number);
                x += size + gap;
            }

            // Résonance (barre fine) et Charges de Rempart (losanges), au-dessus des compétences.
            float charges = state.RampartChargesMax * (compact ? 18f : 22f);
            float resonanceWidth = barWidth - charges - (state.RampartChargesMax > 0 ? 10f : 0f);
            float rowY = slotY - (compact ? 28f : 32f);
            HudArt.Bar(new Rect(cx - barWidth / 2f, rowY, resonanceWidth, compact ? 14f : 16f), me.Resonance / (float)System.Math.Max(1, state.ResonanceMax), ResonanceColor);
            HudArt.Shadowed(new Rect(cx - barWidth / 2f, rowY - 1f, resonanceWidth, compact ? 16f : 18f), $"{me.Resonance} / {state.ResonanceMax}", _key,
                new Color(1f, 1f, 1f, 0.9f));
            for (int i = 0; i < state.RampartChargesMax; i++)
            {
                float d = compact ? 16f : 20f;
                HudArt.Diamond(new Rect(cx - barWidth / 2f + resonanceWidth + 10f + i * (d + 2f), rowY - (d - (compact ? 14f : 16f)) / 2f, d, d), i < me.RampartCharges,
                    ChargeColor);
            }

            // Garde : bouclier éteint, levé (argent) ou parade (or), au-dessus du globe de vie ; récupération de parade en chiffres.
            float icon = compact ? 34f : 42f;
            var guardRect = new Rect(left.x + radius * 0.55f, left.y - radius - icon * 0.55f, icon, icon);
            Color guardTint = me.ParryOpen ? new Color(1f, 0.84f, 0.3f) : me.Guarding ? new Color(0.82f, 0.86f, 0.95f) : new Color(0.35f, 0.33f, 0.3f, 0.85f);
            Fill(guardRect, new Color(0f, 0f, 0f, 0.55f));
            HudArt.DrawIcon(new Rect(guardRect.x + 4f, guardRect.y + 4f, icon - 8f, icon - 8f), HudArt.Icon(me.ParryOpen ? "parade" : "garde"), guardTint);
            HudArt.Frame(guardRect);
            if (state.ParryRecoverySeconds > 0)
                HudArt.Shadowed(new Rect(guardRect.x + icon + 4f, guardRect.y, 80f, icon), $"{state.ParryRecoverySeconds:0.0} s", _key, new Color(1f, 0.55f, 0.23f));

            if (me.States.Count > 0)
                Centered(new Rect(cx - 400f, plateTop - 30f, 800f, 26f), string.Join("  ·  ", me.States.Select(Labels.State)), new Color(1f, 0.9f, 0.7f));
        }

        /// <summary>Fiche de la cible, en haut à droite (y de 14 à 146 : les commandes tactiles se rangent dessous).</summary>
        /// <param name="right">Sous-lot 12.9d : place laissée à droite (la petite carte de l'aventure).</param>
        public static void Target(OtherState target, float right = 0f)
        {
            if (target == null) return;
            float x = Width - 380f - right, y = 20f;
            Panel(new Rect(x - 10, y - 6, 370, 132), new Color(0.04f, 0.035f, 0.03f, 0.72f));
            HudArt.Shadowed(new Rect(x, y, 350, 30), Plain(target.Id), _name, HudArt.Gold);
            CombatantView v = target.View;
            if (v.Health is long h && v.MaxHealth is long max && max > 0)
            {
                HudArt.Bar(new Rect(x, y + 32, 350, 22), h / (float)max, HealthColor);
                Centered(new Rect(x, y + 32, 350, 22), $"{h} / {max}", Color.white);
            }
            else Text(new Rect(x, y + 30, 350, 26), "PV infinis (entraînement)");
            if (v.NextAttack != null && target.NextAttackSeconds is double next)
                Text(new Rect(x, y + 60, 360, 26),
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
                HudArt.Bar(new Rect(x - 60, y - 42, 120, 14), h / (float)max, HealthColor);
            if (v.NextAttack != null && other.NextAttackSeconds is double next && !v.IsDead)
            {
                // Télégraphie : la barre se remplit à l'approche du coup (sur 2 s), rouge vif pour une attaque imparable.
                float fill = 1f - Mathf.Clamp01((float)next / 2f);
                Color color = v.NextAttack.Unblockable ? new Color(1f, 0.15f, 0.1f) : new Color(1f, 0.6f, 0.15f);
                HudArt.Bar(new Rect(x - 60, y - 24, 120, 10), fill, color);
            }
            HudArt.Shadowed(new Rect(x - 110, y - 72, 220, 26), (isTarget ? "» " : "") + Plain(other.Id), _name,
                isTarget ? new Color(1f, 0.86f, 0.4f) : HudArt.Parchment);
        }

        /// <param name="adventure">Sous-lot 12.8 : touches de l'aventure (texte par clé), à la place de celles de l'entraînement ; null hors aventure.</param>
        public static void Help(string scenario, string build, string camera, bool paused, bool network, string adventure = null)
        {
            float y = 20f;
            Panel(new Rect(10, 10, 560, adventure != null ? 420 : 348), new Color(0.04f, 0.035f, 0.03f, 0.72f));
            Title(new Rect(20, y, 540, 34), "Éclats du Seuil — prototype");
            Small(new Rect(20, y + 36, 540, 24), $"{(adventure != null ? "Aventure" : network ? "Réseau" : "Scénario")} : {scenario}  ·  {(adventure != null ? "" : "Build : ")}{build}");
            Small(new Rect(20, y + 60, 540, 24), $"Caméra : {camera}{(paused ? "  ·  <color=#ffd84a>PAUSE</color>" : "")}");
            // Sous-lot 10.4 : en réseau, pas de pause ni de scénario suivant (le serveur mène le combat) ; G vise l'autre joueur.
            Small(new Rect(20, y + 92, 540, adventure != null ? 320 : 254),
                "ZQSD (WASD en QWERTY) / flèches : se déplacer   ·   clic droit ou Maj : garde (maintenir)\n" +
                "Espace : parade parfaite (juste avant l'impact)   ·   1 à 4 : compétences   ·   R : ultime\n" +
                "Tab : changer de cible   ·   molette : de la vue 1 à la vue 2   ·   C : rappel de l'autre vue\n" +
                "A / E (Q / E en QWERTY) : tourner   ·   bouton du milieu maintenu : tourner librement\n" +
                (adventure != null
                    ? adventure + "\nÉchap : pause   ·   M : jeu en réseau   ·   H : masquer l'aide"
                    : network
                    ? "G : allié visé par les soutiens   ·   F1 à F5 et T (salle d'attente) : build\nM : menu réseau   ·   H : masquer l'aide"
                    : "N : scénario suivant (puis l'aventure)   ·   F1 à F5 : build   ·   Retour arrière : recommencer\n" +
                      "T : talents   ·   Échap : pause   ·   M : jeu en réseau   ·   H : masquer l'aide"));
        }
    }
}
