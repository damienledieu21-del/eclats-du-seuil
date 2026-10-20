using System.Collections.Generic;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot 11.8 (premier essai tactile) : commandes au doigt, dessinées avec l'interface provisoire (IMGUI) et lues sur
    /// <see cref="Input.touches"/> (téléphone, ou simulateur d'appareils de l'éditeur). Disposition pour un téléphone tenu à l'horizontale :
    /// <list type="bullet">
    /// <item>moitié gauche : <b>stick</b> du pouce gauche, qui apparaît là où le pouce se pose (marche relative à la caméra) ;</item>
    /// <item>à droite : <b>Garde</b> (maintenir), <b>Parade</b>, les quatre compétences et l'<b>ultime</b>, <b>Cible</b> (suivante),
    /// <b>Vue</b> (rappel entre les vues 1 et 2), <b>Allié</b> en réseau ;</item>
    /// <item>moitié droite hors des boutons : un doigt qui glisse <b>tourne la vue</b>, deux doigts qui s'écartent ou se rapprochent la
    /// <b>règlent entre les vues 1 et 2</b> ;</item>
    /// <item>en haut à droite : <b>Menu</b> (réseau) et <b>Aide</b> ; seul, aussi <b>Pause</b>, <b>Rejouer</b> et <b>Scène</b> (suivante).</item>
    /// </list>
    /// Coordonnées dans l'écran virtuel de <see cref="Hud"/> (1080 de haut). Les commandes ne font que des demandes, comme le clavier :
    /// le combat (moteur ou serveur) reste seul juge. Au PC, F8 les affiche et la souris fait un doigt (pour voir la disposition ; le
    /// simulateur d'appareils de l'éditeur, lui, simule de vrais doigts).
    /// </summary>
    public sealed class TouchControls
    {
        /// <summary>Rayon du stick (pixels virtuels) : un pouce poussé jusqu'au bord marche à pleine vitesse.</summary>
        private const float StickRadius = 120f;

        /// <summary>Taille des boutons d'action (pixels virtuels) : au moins 9 mm sur l'écran d'un téléphone.</summary>
        private const float Big = 150f, Small = 112f;

        private enum ButtonId { Guard, Parry, Skill1, Skill2, Skill3, Skill4, Ultimate, Target, View, Ally, Menu, Help, Pause, Restart, NextScene }

        private sealed class Button
        {
            public ButtonId Id;
            public Rect Rect;
            public string Label;
        }

        private readonly List<Button> _buttons = new List<Button>();
        private readonly Dictionary<int, ButtonId> _pressed = new Dictionary<int, ButtonId>();
        private readonly HashSet<ButtonId> _held = new HashSet<ButtonId>();
        private readonly HashSet<ButtonId> _tapped = new HashSet<ButtonId>();
        private int _stickFinger = -1;
        private Vector2 _stickOrigin, _stickNow;
        private int _lookFinger = -1;
        private Vector2 _lookLast;
        private float _pinchLast = -1f;
        private bool _forced;

        /// <summary>Commandes tactiles en service : écran tactile (téléphone, simulateur d'appareils), ou forcées au clavier (F8).</summary>
        public bool Active => _forced || Input.touchSupported;

        /// <summary>Marche voulue, dans le repère de l'écran (x vers la droite, y vers le haut), longueur de 0 à 1.</summary>
        public Vector2 Move { get; private set; }

        /// <summary>Rotation de la vue demandée depuis la dernière image, en degrés.</summary>
        public float YawDelta { get; private set; }

        /// <summary>Réglage de la vue demandé depuis la dernière image (positif : vers la vue 2).</summary>
        public float ViewDelta { get; private set; }

        public bool GuardHeld => _held.Contains(ButtonId.Guard);
        public bool ParryPressed => _tapped.Contains(ButtonId.Parry);
        public bool TargetPressed => _tapped.Contains(ButtonId.Target);
        public bool ViewPressed => _tapped.Contains(ButtonId.View);
        public bool AllyPressed => _tapped.Contains(ButtonId.Ally);
        public bool MenuPressed => _tapped.Contains(ButtonId.Menu);
        public bool HelpPressed => _tapped.Contains(ButtonId.Help);
        public bool PausePressed => _tapped.Contains(ButtonId.Pause);
        public bool RestartPressed => _tapped.Contains(ButtonId.Restart);
        public bool NextScenePressed => _tapped.Contains(ButtonId.NextScene);

        /// <summary>Emplacement de compétence touché à cette image (0 à 3, 4 : ultime), ou -1.</summary>
        public int SlotPressed =>
            _tapped.Contains(ButtonId.Skill1) ? 0 : _tapped.Contains(ButtonId.Skill2) ? 1 : _tapped.Contains(ButtonId.Skill3) ? 2
            : _tapped.Contains(ButtonId.Skill4) ? 3 : _tapped.Contains(ButtonId.Ultimate) ? 4 : -1;

        /// <summary>F8 : force les commandes tactiles à l'écran (essai à la souris sans simulateur) ; rend vrai si l'état a changé.</summary>
        public bool ToggleForced()
        {
            _forced = !_forced;
            Reset();
            return true;
        }

        /// <summary>Oublie les doigts posés (menu ouvert, perte du focus) : plus rien n'est tenu.</summary>
        public void Reset()
        {
            _pressed.Clear();
            _held.Clear();
            _tapped.Clear();
            _stickFinger = -1;
            _lookFinger = -1;
            _pinchLast = -1f;
            Move = Vector2.zero;
            YawDelta = 0f;
            ViewDelta = 0f;
        }

        /// <summary>
        /// Revue Codex de la PR #36 : Android peut suspendre le jeu (verrouillage, accueil, autre application) sans livrer la fin des doigts
        /// posés. Un doigt mémorisé (stick, regard, bouton tenu) qui n'est plus sur l'écran est relâché, comme s'il était levé.
        /// </summary>
        private void Forget(List<Finger> fingers)
        {
            bool Present(int id)
            {
                foreach (Finger f in fingers)
                    if (f.fingerId == id) return true;
                return false;
            }
            if (_stickFinger >= 0 && !Present(_stickFinger)) _stickFinger = -1;
            if (_lookFinger >= 0 && !Present(_lookFinger)) _lookFinger = -1;
            foreach (int id in new List<int>(_pressed.Keys))
            {
                if (Present(id)) continue;
                ButtonId released = _pressed[id];
                _pressed.Remove(id);
                if (!_pressed.ContainsValue(released)) _held.Remove(released);
            }
        }

        /// <summary>Place les boutons pour la taille d'écran courante ; <paramref name="network"/> : bouton Allié.</summary>
        private void Layout(bool network)
        {
            _buttons.Clear();
            float w = Hud.Width, h = Hud.VirtualHeight, m = 24f;
            void Add(ButtonId id, float x, float y, float size, string label) =>
                _buttons.Add(new Button { Id = id, Rect = new Rect(x, y, size, size), Label = label });
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
            Add(ButtonId.Menu, w - m - Small, m + 60f, Small, "Menu");
            Add(ButtonId.Help, w - 2 * (m + Small), m + 60f, Small, "Aide");
            if (!network)
            {
                Add(ButtonId.Pause, w - 3 * (m + Small), m + 60f, Small, "Pause");
                Add(ButtonId.Restart, w - 4 * (m + Small), m + 60f, Small, "Rejouer");
                Add(ButtonId.NextScene, w - 5 * (m + Small), m + 60f, Small, "Scène");
            }
        }

        private static Vector2 Virtual(Vector2 screen) => new Vector2(screen.x / Hud.Scale, (Screen.height - screen.y) / Hud.Scale);

        /// <summary>Un doigt de cette image : écran tactile, ou souris quand les commandes sont forcées (F8) sans écran tactile.</summary>
        private struct Finger
        {
            public int fingerId;
            public Vector2 position;
            public TouchPhase phase;
        }

        private List<Finger> Fingers()
        {
            var fingers = new List<Finger>();
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                fingers.Add(new Finger { fingerId = t.fingerId, position = t.position, phase = t.phase });
            }
            // Essai au PC (F8) : le bouton gauche de la souris fait un doigt (un seul : pas de geste à deux doigts).
            if (fingers.Count == 0 && _forced && !Input.touchSupported)
            {
                var mouse = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
                if (Input.GetMouseButtonDown(0)) fingers.Add(new Finger { fingerId = MouseFinger, position = mouse, phase = TouchPhase.Began });
                else if (Input.GetMouseButtonUp(0)) fingers.Add(new Finger { fingerId = MouseFinger, position = mouse, phase = TouchPhase.Ended });
                else if (Input.GetMouseButton(0)) fingers.Add(new Finger { fingerId = MouseFinger, position = mouse, phase = TouchPhase.Moved });
            }
            return fingers;
        }

        private const int MouseFinger = 1000;

        private ButtonId? Hit(Vector2 at)
        {
            foreach (Button b in _buttons)
                if (at.x >= b.Rect.x && at.x <= b.Rect.x + b.Rect.width && at.y >= b.Rect.y && at.y <= b.Rect.y + b.Rect.height) return b.Id;
            return null;
        }

        /// <summary>Lit les doigts de cette image (à appeler une fois par image, avant de lire les commandes).</summary>
        public void Update(bool network)
        {
            _tapped.Clear();
            YawDelta = 0f;
            ViewDelta = 0f;
            if (!Active)
            {
                if (_held.Count > 0 || _stickFinger >= 0) Reset();
                return;
            }
            Layout(network);
            float half = Hud.Width / 2f;
            var looking = new List<Vector2>();
            List<Finger> fingers = Fingers();
            Forget(fingers);
            for (int i = 0; i < fingers.Count; i++)
            {
                Finger touch = fingers[i];
                Vector2 at = Virtual(touch.position);
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        if (Hit(at) is ButtonId id)
                        {
                            _pressed[touch.fingerId] = id;
                            _held.Add(id);
                            _tapped.Add(id);
                        }
                        else if (at.x < half && _stickFinger < 0)
                        {
                            _stickFinger = touch.fingerId;
                            _stickOrigin = at;
                            _stickNow = at;
                        }
                        else if (at.x >= half && _lookFinger < 0)
                        {
                            _lookFinger = touch.fingerId;
                            _lookLast = at;
                        }
                        break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        if (touch.fingerId == _stickFinger) _stickNow = at;
                        else if (touch.fingerId == _lookFinger)
                        {
                            YawDelta += (at.x - _lookLast.x) * 0.25f;
                            _lookLast = at;
                        }
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        if (_pressed.TryGetValue(touch.fingerId, out ButtonId released))
                        {
                            _pressed.Remove(touch.fingerId);
                            if (!_pressed.ContainsValue(released)) _held.Remove(released);
                        }
                        if (touch.fingerId == _stickFinger) _stickFinger = -1;
                        if (touch.fingerId == _lookFinger) _lookFinger = -1;
                        break;
                }
                if (at.x >= half && !_pressed.ContainsKey(touch.fingerId) && touch.fingerId != _stickFinger
                    && touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled) looking.Add(at);
            }
            // Deux doigts sur la moitié droite : l'écart règle la vue (s'écarter : vers la vue 2, plus proche).
            if (looking.Count >= 2)
            {
                float spread = Vector2.Distance(looking[0], looking[1]);
                if (_pinchLast >= 0f) ViewDelta = (spread - _pinchLast) / 600f;
                _pinchLast = spread;
                YawDelta = 0f;
            }
            else _pinchLast = -1f;
            if (_stickFinger < 0) Move = Vector2.zero;
            else
            {
                // Écran : y vers le bas ; marche : y vers le haut (pousser le pouce vers le haut avance).
                Vector2 d = new Vector2(_stickNow.x - _stickOrigin.x, _stickOrigin.y - _stickNow.y) / StickRadius;
                Move = d.magnitude > 1f ? d.normalized : d;
            }
        }

        /// <summary>Dessine le stick et les boutons (dans <c>OnGUI</c>, après <see cref="Hud.Begin"/>).</summary>
        public void Draw(bool network)
        {
            if (!Active) return;
            Layout(network);
            foreach (Button b in _buttons)
            {
                bool held = _held.Contains(b.Id);
                Hud.Fill(b.Rect, held ? new Color(1f, 0.85f, 0.3f, 0.55f) : new Color(0f, 0f, 0f, 0.35f));
                Hud.Centered(b.Rect, b.Label, Color.white);
            }
            if (_stickFinger >= 0)
            {
                Hud.Fill(new Rect(_stickOrigin.x - StickRadius, _stickOrigin.y - StickRadius, 2 * StickRadius, 2 * StickRadius), new Color(1f, 1f, 1f, 0.12f));
                Vector2 knob = _stickOrigin + new Vector2(Move.x, -Move.y) * StickRadius;
                Hud.Fill(new Rect(knob.x - 40f, knob.y - 40f, 80f, 80f), new Color(1f, 1f, 1f, 0.45f));
            }
            else Hud.Small(new Rect(40f, Hud.VirtualHeight - 70f, 520f, 30f), "Pouce gauche : marcher · glisser à droite : tourner · deux doigts : vue");
        }
    }
}
