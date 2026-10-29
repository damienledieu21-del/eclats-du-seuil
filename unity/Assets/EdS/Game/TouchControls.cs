using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot 11.8 (premier essai tactile) : commandes au doigt, dessinées avec l'interface provisoire (IMGUI) et lues sur
    /// <see cref="Input.touches"/> (téléphone, ou simulateur d'appareils de l'éditeur). Disposition pour un téléphone tenu à l'horizontale :
    /// <list type="bullet">
    /// <item>moitié gauche : <b>stick</b> du pouce gauche, qui apparaît là où le pouce se pose (marche relative à la caméra) ;</item>
    /// <item>à droite : <b>Garde</b> (maintenir), <b>Parade</b>, les quatre compétences et l'<b>ultime</b>, <b>Cible</b> (suivante),
    /// <b>Vue</b> (caméra remise derrière le personnage), <b>Allié</b> en réseau ;</item>
    /// <item>moitié droite hors des boutons : un doigt qui glisse <b>tourne et incline la vue</b>, deux doigts qui s'écartent ou se
    /// rapprochent <b>rapprochent ou éloignent la caméra</b> (essai réaliste 9) ;</item>
    /// <item>en haut à droite : <b>Menu</b> (réseau) et <b>Aide</b> ; seul, aussi <b>Pause</b>, <b>Rejouer</b> et <b>Scène</b> (suivante) ;</item>
    /// <item>sous-lot 12.8, dans l'aventure : <b>Agir</b> (parler, récolter) et <b>Potion</b> au-dessus de la parade, <b>Sac</b>,
    /// <b>Journal</b> et <b>Atelier</b> en haut, <b>Halte</b> à la place de « Rejouer ».</item>
    /// </list>
    /// Coordonnées dans l'écran virtuel de <see cref="Hud"/> (1080 de haut). Le traitement des doigts (stick, boutons tenus et touchés,
    /// regard, pincement, doigt perdu) est dans <see cref="TouchGestures"/> (sans Unity, testé) ; ce script lit les doigts, place et
    /// dessine les boutons (revue Codex de la PR #36, 6e tour). Les commandes ne font que des demandes, comme le clavier :
    /// le combat (moteur ou serveur) reste seul juge. Au PC, F8 les affiche et la souris fait un doigt (pour voir la disposition ; le
    /// simulateur d'appareils de l'éditeur, lui, simule de vrais doigts).
    /// </summary>
    public sealed class TouchControls
    {
        /// <summary>Taille des boutons d'action (pixels virtuels), au moins 9 mm sur l'écran d'un téléphone (<see cref="TouchGestures.SmallButton"/>).</summary>
        private const float Big = TouchGestures.BigButton, Small = TouchGestures.SmallButton;

        private enum ButtonId { Guard, Parry, Skill1, Skill2, Skill3, Skill4, Ultimate, Target, View, Ally, Menu, Help, Pause, Restart, NextScene, Act, Potion, Bag, Journal, Workshop, Map, Talents }

        private sealed class Button
        {
            public ButtonId Id;
            public Rect Rect;
            public string Label;
        }

        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<GestureButton> _zones = new List<GestureButton>();
        private readonly TouchGestures _gestures = new TouchGestures();
        private bool _forced;

        /// <summary>Commandes tactiles en service : écran tactile (téléphone, simulateur d'appareils), ou forcées au clavier (F8).</summary>
        public bool Active => _forced || Input.touchSupported;

        /// <summary>Marche voulue, dans le repère de l'écran (x vers la droite, y vers le haut), longueur de 0 à 1.</summary>
        public Vector2 Move => new Vector2(_gestures.MoveX, _gestures.MoveY);

        /// <summary>Rotation de la vue demandée depuis la dernière image, en degrés.</summary>
        public float YawDelta => _gestures.YawDelta;

        /// <summary>Rapprochement de la vue demandé depuis la dernière image (positif : plus près).</summary>
        public float ViewDelta => _gestures.ViewDelta;

        /// <summary>Essai réaliste 9 : inclinaison de la vue demandée au doigt, en degrés.</summary>
        public float PitchDelta => _gestures.PitchDelta;

        public bool GuardHeld => _gestures.IsHeld((int)ButtonId.Guard);
        public bool ParryPressed => Tapped(ButtonId.Parry);
        public bool TargetPressed => Tapped(ButtonId.Target);
        public bool ViewPressed => Tapped(ButtonId.View);
        public bool AllyPressed => Tapped(ButtonId.Ally);
        public bool MenuPressed => Tapped(ButtonId.Menu);
        public bool HelpPressed => Tapped(ButtonId.Help);
        public bool PausePressed => Tapped(ButtonId.Pause);
        public bool RestartPressed => Tapped(ButtonId.Restart);
        public bool NextScenePressed => Tapped(ButtonId.NextScene);

        // Sous-lot 12.8 : aventure (Rejouer y devient Halte : faire halte, ou reprendre après une défaite).
        public bool ActPressed => Tapped(ButtonId.Act);
        public bool PotionPressed => Tapped(ButtonId.Potion);
        public bool BagPressed => Tapped(ButtonId.Bag);
        public bool JournalPressed => Tapped(ButtonId.Journal);
        public bool WorkshopPressed => Tapped(ButtonId.Workshop);

        /// <summary>Essai réaliste 6, étape 2 : l'arbre des Éclats (talents) dans l'aventure.</summary>
        public bool TalentsPressed => Tapped(ButtonId.Talents);

        /// <summary>Sous-lot 12.9d : la petite carte touchée (l'ouvrir en grand).</summary>
        public bool MapPressed => Tapped(ButtonId.Map);

        private bool Tapped(ButtonId id) => _gestures.WasTapped((int)id);

        /// <summary>Emplacement de compétence touché à cette image (0 à 3, 4 : ultime), ou -1.</summary>
        public int SlotPressed =>
            Tapped(ButtonId.Skill1) ? 0 : Tapped(ButtonId.Skill2) ? 1 : Tapped(ButtonId.Skill3) ? 2
            : Tapped(ButtonId.Skill4) ? 3 : Tapped(ButtonId.Ultimate) ? 4 : -1;

        /// <summary>F8 : force les commandes tactiles à l'écran (essai à la souris sans simulateur) ; rend vrai si l'état a changé.</summary>
        public bool ToggleForced()
        {
            _forced = !_forced;
            Reset();
            return true;
        }

        /// <summary>Oublie les doigts posés (menu ouvert, perte du focus) : plus rien n'est tenu.</summary>
        public void Reset() => _gestures.Reset();

        /// <summary>
        /// Place les boutons pour la taille d'écran courante ; <paramref name="network"/> : bouton Allié ; <paramref name="adventure"/>
        /// (sous-lot 12.8) : Agir et Potion près du pouce droit, Sac, Journal et Atelier en haut.
        /// </summary>
        private void Layout(bool network, bool adventure)
        {
            _buttons.Clear();
            _zones.Clear();
            float w = Hud.Width, h = Hud.VirtualHeight, m = 24f;
            void Add(ButtonId id, float x, float y, float size, string label)
            {
                _buttons.Add(new Button { Id = id, Rect = new Rect(x, y, size, size), Label = label });
                _zones.Add(new GestureButton((int)id, x, y, size, size));
            }
            // Pouce droit : garde et parade au plus près, compétences en arc au-dessus, ultime au bout de l'arc.
            float gx = w - m - Big, gy = h - m - Big;
            Add(ButtonId.Guard, gx, gy, Big, "Garde");
            Add(ButtonId.Parry, gx - Big - m, gy, Big, "Parade");
            Add(ButtonId.Skill1, gx - 2 * (Big + m) + (Big - Small), gy + (Big - Small), Small, "1");
            Add(ButtonId.Skill2, gx - Big - m + (Big - Small) / 2, gy - Small - m, Small, "2");
            Add(ButtonId.Skill3, gx + (Big - Small) / 2, gy - Small - m, Small, "3");
            Add(ButtonId.Skill4, gx + (Big - Small) / 2, gy - 2 * (Small + m), Small, "4");
            Add(ButtonId.Ultimate, gx - Big - m + (Big - Small) / 2, gy - 2 * (Small + m), Small, "Ultime");
            Add(ButtonId.Target, gx + (Big - Small) / 2, gy - 3 * (Small + m), Small, "Cible");
            Add(ButtonId.View, gx - Big - m + (Big - Small) / 2, gy - 3 * (Small + m), Small, "Vue");
            if (network) Add(ButtonId.Ally, gx - 2 * (Big + m) + (Big - Small), gy - Small - m, Small, "Allié");
            if (adventure)
            {
                Add(ButtonId.Act, gx - 2 * (Big + m) + (Big - Small), gy - Small - m, Small, "Agir");
                Add(ButtonId.Potion, gx - 2 * (Big + m) + (Big - Small), gy - 2 * (Small + m), Small, "Potion");
            }
            // Revue Codex de la PR #36 (3e tour) : sous la fiche de la cible, pour ne pas cacher son compte à rebours ni ses altérations
            // (essai réaliste 6 : la cible est en haut au milieu, le minuteur en haut à droite).
            // Sous-lot 12.9d : dans l'aventure, la petite carte occupe le coin : la rangée descend dessous, et la carte elle-même est un bouton
            // (l'agrandir). Essai réaliste 6 : la carte peinte (HudView, réduite quand les commandes tactiles sont là) et le lieu dessous,
            // jusqu'à y = 300.
            Rect map = HudView.MiniMapRect(w, true);
            float top = adventure ? 300f : 160f;
            if (adventure) _zones.Add(new GestureButton((int)ButtonId.Map, map.x, map.y, map.width, map.height));
            Add(ButtonId.Menu, w - m - Small, top, Small, "Menu");
            Add(ButtonId.Help, w - 2 * (m + Small), top, Small, "Aide");
            if (!network)
            {
                Add(ButtonId.Pause, w - 3 * (m + Small), top, Small, "Pause");
                Add(ButtonId.Restart, w - 4 * (m + Small), top, Small, adventure ? "Halte" : "Rejouer");
                Add(ButtonId.NextScene, w - 5 * (m + Small), top, Small, "Scène");
            }
            if (adventure)
            {
                Add(ButtonId.Bag, w - 6 * (m + Small), top, Small, "Sac");
                Add(ButtonId.Journal, w - 7 * (m + Small), top, Small, "Journal");
                Add(ButtonId.Workshop, w - 8 * (m + Small), top, Small, "Atelier");
                Add(ButtonId.Talents, w - 9 * (m + Small), top, Small, "Talents");
            }
        }

        /// <summary>Doigts de cette image dans l'écran virtuel : écran tactile, ou souris quand les commandes sont forcées (F8) sans écran tactile.</summary>
        private List<FingerPoint> Fingers()
        {
            var fingers = new List<FingerPoint>();
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                fingers.Add(Virtual(t.fingerId, t.position, Phase(t.phase)));
            }
            // Essai au PC (F8) : le bouton gauche de la souris fait un doigt (un seul : pas de geste à deux doigts).
            if (fingers.Count == 0 && _forced && !Input.touchSupported)
            {
                var mouse = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
                if (Input.GetMouseButtonDown(0)) fingers.Add(Virtual(MouseFinger, mouse, FingerPhase.Began));
                else if (Input.GetMouseButtonUp(0)) fingers.Add(Virtual(MouseFinger, mouse, FingerPhase.Ended));
                else if (Input.GetMouseButton(0)) fingers.Add(Virtual(MouseFinger, mouse, FingerPhase.Moved));
            }
            return fingers;
        }

        private const int MouseFinger = 1000;

        private static FingerPoint Virtual(int id, Vector2 screen, FingerPhase phase) =>
            new FingerPoint(id, screen.x / Hud.Scale, (Screen.height - screen.y) / Hud.Scale, phase);

        private static FingerPhase Phase(TouchPhase phase)
        {
            switch (phase)
            {
                case TouchPhase.Began: return FingerPhase.Began;
                case TouchPhase.Moved: return FingerPhase.Moved;
                case TouchPhase.Stationary: return FingerPhase.Stationary;
                case TouchPhase.Ended: return FingerPhase.Ended;
                default: return FingerPhase.Canceled;
            }
        }

        /// <summary>
        /// Lit les doigts de cette image (à appeler une fois par image, avant de lire les commandes). <paramref name="covered"/> (revue Codex
        /// de la PR #49) : un panneau ou un dialogue de l'aventure couvre l'écran : aucun bouton ni geste n'est actif, les doigts vont à ses
        /// propres boutons (dont Fermer).
        /// </summary>
        public void Update(bool network, bool adventure = false, bool covered = false)
        {
            if (!Active || covered)
            {
                _gestures.Reset();
                return;
            }
            Layout(network, adventure);
            _gestures.Update(Fingers(), _zones, Hud.Width);
        }

        /// <summary>Dessine le stick et les boutons (dans <c>OnGUI</c>, après <see cref="Hud.Begin"/>) ; rien quand un panneau les couvre.</summary>
        public void Draw(bool network, bool adventure = false, bool covered = false)
        {
            if (!Active || covered) return;
            Layout(network, adventure);
            foreach (Button b in _buttons)
            {
                bool held = _gestures.IsHeld((int)b.Id);
                Hud.Panel(b.Rect, held ? new Color(1f, 0.85f, 0.3f, 0.55f) : new Color(0f, 0f, 0f, 0.35f));
                Hud.Centered(b.Rect, b.Label, Color.white);
            }
            if (_gestures.StickActive)
            {
                float r = TouchGestures.StickRadius;
                var origin = new Vector2(_gestures.StickOriginX, _gestures.StickOriginY);
                Hud.Fill(new Rect(origin.x - r, origin.y - r, 2 * r, 2 * r), new Color(1f, 1f, 1f, 0.12f));
                Vector2 knob = origin + new Vector2(Move.x, -Move.y) * r;
                Hud.Fill(new Rect(knob.x - 40f, knob.y - 40f, 80f, 80f), new Color(1f, 1f, 1f, 0.45f));
            }
            else Hud.Small(new Rect(40f, Hud.VirtualHeight - 70f, 520f, 30f), "Pouce gauche : marcher · glisser à droite : tourner · deux doigts : vue");
        }
    }
}
