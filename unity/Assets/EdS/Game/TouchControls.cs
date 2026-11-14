using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Commandes au doigt (sous-lot 11.8), disposées selon l'interface V2.1 (dossier du 12 novembre 2026, page 20) par
    /// <see cref="HudLayoutV21"/> (sans Unity, testé : zone sûre, 8 unités d'écart, 56 à 72 unités pour le combat, 48 pour le reste,
    /// formats 16:9 à 4:3). Téléphone tenu à l'horizontale :
    /// <list type="bullet">
    /// <item>moitié gauche : le <b>stick</b> apparaît là où le pouce se pose (marche relative à la caméra) ; au repos, son cercle de 128 unités
    /// est dessiné à sa place de la maquette ;</item>
    /// <item>pouce droit : <b>Parade</b> (72) et <b>Garde</b> à maintenir (64) à la place « attaque / esquive » de la maquette (cadrage
    /// L11-D4 : pas d'esquive universelle) ; les compétences 1 à 6 en rangée de 56, l'<b>Ultime</b>, la <b>Potion</b>, <b>Cible</b> et
    /// <b>Agir</b> (ou <b>Allié</b> en réseau) au-dessus ;</item>
    /// <item>en haut à droite, à gauche de la mini-carte : la rangée utile (<b>Pause</b>, <b>Vue</b>…) ; ce qui n'y tient pas va dans le tiroir
    /// <b>Plus</b> (Sac, Journal, Talents, Atelier, Halte ou Rejouer, Scène, Aide, Menu) ; la mini-carte elle-même s'agrandit au toucher ;</item>
    /// <item>moitié droite hors des boutons : un doigt qui glisse tourne et incline la vue, deux doigts rapprochent ou éloignent la caméra.</item>
    /// </list>
    /// Les compétences sont dessinées par le HUD (<see cref="HudView"/>, icône et recharge) aux mêmes rectangles : ce script n'y dessine
    /// rien, il en lit seulement les doigts. Libellés : données de textes (<see cref="HudTextsV21"/>). Les commandes ne font que des
    /// demandes, comme le clavier : le combat (moteur ou serveur) reste seul juge. Au PC, F8 les affiche et la souris fait un doigt.
    /// Aucune allocation par image : boutons et zones ne sont refaits que quand la disposition ou le tiroir changent.
    /// </summary>
    public sealed class TouchControls
    {
        private readonly List<GestureButton> _zones = new List<GestureButton>();
        private readonly List<FingerPoint> _fingers = new List<FingerPoint>();
        private readonly TouchGestures _gestures = new TouchGestures();
        private readonly string[] _labels = new string[System.Enum.GetValues(typeof(HudButton)).Length];
        private readonly float[] _labelSizes = new float[System.Enum.GetValues(typeof(HudButton)).Length];
        private HudLayoutV21 _layout;
        private int _zonesVersion = -1;
        private bool _zonesDrawer;
        private bool _forced;
        private bool _drawerOpen;
        private GUIStyle _label;

        /// <summary>Commandes tactiles en service : écran tactile (téléphone, simulateur d'appareils), ou forcées au clavier (F8).</summary>
        public bool Active => _forced || Input.touchSupported;

        /// <summary>Le tiroir « Plus » est ouvert (le HUD cache alors la liste des quêtes qu'il recouvre).</summary>
        public bool DrawerOpen => _drawerOpen;

        /// <summary>Marche voulue, dans le repère de l'écran (x vers la droite, y vers le haut), longueur de 0 à 1.</summary>
        public Vector2 Move => new Vector2(_gestures.MoveX, _gestures.MoveY);

        /// <summary>Rotation de la vue demandée depuis la dernière image, en degrés.</summary>
        public float YawDelta => _gestures.YawDelta;

        /// <summary>Rapprochement de la vue demandé depuis la dernière image (positif : plus près).</summary>
        public float ViewDelta => _gestures.ViewDelta;

        /// <summary>Essai réaliste 9 : inclinaison de la vue demandée au doigt, en degrés.</summary>
        public float PitchDelta => _gestures.PitchDelta;

        public bool GuardHeld => _gestures.IsHeld((int)HudButton.Guard);
        public bool ParryPressed => Tapped(HudButton.Parry);
        public bool TargetPressed => Tapped(HudButton.Target);
        public bool ViewPressed => Tapped(HudButton.View);
        public bool AllyPressed => Tapped(HudButton.Ally);
        public bool MenuPressed => Tapped(HudButton.Menu);
        public bool HelpPressed => Tapped(HudButton.Help);
        public bool PausePressed => Tapped(HudButton.Pause);
        public bool RestartPressed => Tapped(HudButton.Restart);
        public bool NextScenePressed => Tapped(HudButton.NextScene);

        // Sous-lot 12.8 : aventure (Rejouer y devient Halte : faire halte, ou reprendre après une défaite).
        public bool ActPressed => Tapped(HudButton.Act);
        public bool PotionPressed => Tapped(HudButton.Potion);
        public bool BagPressed => Tapped(HudButton.Bag);
        public bool JournalPressed => Tapped(HudButton.Journal);
        public bool WorkshopPressed => Tapped(HudButton.Workshop);

        /// <summary>Essai réaliste 6, étape 2 : l'arbre des Éclats (talents) dans l'aventure.</summary>
        public bool TalentsPressed => Tapped(HudButton.Talents);

        /// <summary>Sous-lot 12.9d : la petite carte touchée (l'ouvrir en grand).</summary>
        public bool MapPressed => Tapped(HudButton.Map);

        private bool Tapped(HudButton id) => _gestures.WasTapped((int)id);

        /// <summary>
        /// Sous-lot V1.9.8 : bouton mis en valeur pour l'étape du tutoriel, désigné par son ancien libellé (« Garde », « Parade », « Potion »,
        /// « 1 », « Agir ») ; ou null.
        /// </summary>
        public string Highlight { get; set; }

        /// <summary>Emplacement de compétence touché à cette image (0 à 5 ; l'ultime : <see cref="RealtimeCombat.UltimateSlot"/>), ou -1.</summary>
        public int SlotPressed =>
            Tapped(HudButton.Skill1) ? 0 : Tapped(HudButton.Skill2) ? 1 : Tapped(HudButton.Skill3) ? 2
            : Tapped(HudButton.Skill4) ? 3 : Tapped(HudButton.Skill5) ? 4 : Tapped(HudButton.Skill6) ? 5
            : Tapped(HudButton.Ultimate) ? RealtimeCombat.UltimateSlot : -1;

        /// <summary>F8 : force les commandes tactiles à l'écran (essai à la souris sans simulateur) ; rend vrai si l'état a changé.</summary>
        public bool ToggleForced()
        {
            _forced = !_forced;
            Reset();
            return true;
        }

        /// <summary>Oublie les doigts posés (menu ouvert, perte du focus) : plus rien n'est tenu, le tiroir se ferme.</summary>
        public void Reset()
        {
            _gestures.Reset();
            _drawerOpen = false;
        }

        /// <summary>Ancien libellé du tutoriel → bouton.</summary>
        private static bool Highlighted(string highlight, HudButton b)
        {
            if (highlight == null) return false;
            switch (b)
            {
                case HudButton.Guard: return highlight == "Garde";
                case HudButton.Parry: return highlight == "Parade";
                case HudButton.Potion: return highlight == "Potion";
                case HudButton.Act: return highlight == "Agir";
                case HudButton.Skill1: return highlight == "1";
                case HudButton.Skill2: return highlight == "2";
                case HudButton.Skill3: return highlight == "3";
                case HudButton.Skill4: return highlight == "4";
                case HudButton.Skill5: return highlight == "5";
                case HudButton.Skill6: return highlight == "6";
                default: return false;
            }
        }

        /// <summary>Boutons dessinés par le HUD (icônes de compétence) ou zone sans dessin (la mini-carte).</summary>
        private static bool DrawnByHud(HudButton b) => (b >= HudButton.Skill1 && b <= HudButton.Ultimate) || b == HudButton.Map;

        /// <summary>Zones touchables, refaites seulement quand la disposition ou le tiroir changent.</summary>
        private void Zones()
        {
            if (_zonesVersion == _layout.Version && _zonesDrawer == _drawerOpen) return;
            _zonesVersion = _layout.Version;
            _zonesDrawer = _drawerOpen;
            _zones.Clear();
            for (int i = 0; i < _labels.Length; i++)
            {
                var b = (HudButton)i;
                if (!_layout.Has(b) || (_layout.InDrawer(b) && !_drawerOpen)) continue;
                UiBox box = _layout.Button(b);
                _zones.Add(new GestureButton(i, box.X, box.Y, box.W, box.H));
                string key = HudTextsV21.Button(b, _layout.Mode);
                _labels[i] = key != null ? UiTexts.Get(key) : b >= HudButton.Skill1 && b <= HudButton.Skill6 ? (b - HudButton.Skill1 + 1).ToString() : null;
                _labelSizes[i] = _layout.LabelFont(box);
            }
        }

        /// <summary>Doigts de cette image dans l'écran virtuel : écran tactile, ou souris quand les commandes sont forcées (F8) sans écran tactile.</summary>
        private List<FingerPoint> Fingers()
        {
            _fingers.Clear();
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                _fingers.Add(Virtual(t.fingerId, t.position, Phase(t.phase)));
            }
            // Essai au PC (F8) : le bouton gauche de la souris fait un doigt (un seul : pas de geste à deux doigts).
            if (_fingers.Count == 0 && _forced && !Input.touchSupported)
            {
                var mouse = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
                if (Input.GetMouseButtonDown(0)) _fingers.Add(Virtual(MouseFinger, mouse, FingerPhase.Began));
                else if (Input.GetMouseButtonUp(0)) _fingers.Add(Virtual(MouseFinger, mouse, FingerPhase.Ended));
                else if (Input.GetMouseButton(0)) _fingers.Add(Virtual(MouseFinger, mouse, FingerPhase.Moved));
            }
            return _fingers;
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
        /// Lit les doigts de cette image (une fois par image, avant de lire les commandes), avec la disposition de l'image
        /// (<paramref name="layout"/>, mise à jour par le jeu). <paramref name="covered"/> (revue Codex de la PR #49) : un panneau ou un
        /// dialogue couvre l'écran : aucun bouton ni geste n'est actif, les doigts vont à ses propres boutons (dont Fermer).
        /// </summary>
        public void Update(HudLayoutV21 layout, bool covered = false)
        {
            _layout = layout;
            if (!Active || covered || layout == null || !layout.Phone)
            {
                Reset();
                return;
            }
            Zones();
            _gestures.Update(Fingers(), _zones, Hud.Width);
            // Tiroir « Plus » : le toucher l'ouvre ou le ferme ; un bouton du tiroir touché le referme après son action.
            if (Tapped(HudButton.More)) _drawerOpen = !_drawerOpen;
            else if (_drawerOpen)
                for (int i = 0; i < _layout.UtilityCount; i++)
                    if (_layout.InDrawer(_layout.UtilityAt(i)) && Tapped(_layout.UtilityAt(i))) _drawerOpen = false;
        }

        /// <summary>Dessine le stick, les boutons et le tiroir (dans <c>OnGUI</c>, après <see cref="Hud.Begin"/>) ; rien quand un panneau les couvre.</summary>
        public void Draw(bool covered = false)
        {
            if (!Active || covered || _layout == null || !_layout.Phone) return;
            Zones();
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                _label.normal.textColor = UiV21Art.Parchment;
                Font barlow = Resources.Load<Font>("EdsInterface/BarlowCondensed-SemiBold");
                if (barlow != null) _label.font = barlow;
            }
            if (_drawerOpen && !_layout.Drawer.IsEmpty) UiV21Art.DrawPanel(Box(_layout.Drawer));
            for (int i = 0; i < _labels.Length; i++)
            {
                var b = (HudButton)i;
                if (!_layout.Has(b) || (_layout.InDrawer(b) && !_drawerOpen)) continue;
                Rect rect = Box(_layout.Button(b));
                if (Highlighted(Highlight, b))
                {
                    // Sous-lot V1.9.8 : un cadre d'or qui bat autour du bouton à toucher (sans couvrir l'icône du HUD).
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
                    float grow = 6f + 10f * pulse;
                    HudArt.Frame(new Rect(rect.x - grow, rect.y - grow, rect.width + 2f * grow, rect.height + 2f * grow));
                }
                if (DrawnByHud(b)) continue;
                bool held = _gestures.IsHeld(i) || (b == HudButton.More && _drawerOpen);
                UiV21Art.DrawButton(rect, held);
                if (_labels[i] == null) continue;
                _label.fontSize = Mathf.RoundToInt(_labelSizes[i]);
                GUI.Label(rect, _labels[i], _label);
            }
            UiBox zone = _layout[HudPart.Joystick];
            if (_gestures.StickActive)
            {
                float r = TouchGestures.StickRadius;
                UiV21Art.DrawRing(new Rect(_gestures.StickOriginX - r, _gestures.StickOriginY - r, 2 * r, 2 * r), false);
                float k = HudLayoutV21.Joystick * _layout.Unit * 0.3f;
                float kx = _gestures.StickOriginX + Move.x * r, ky = _gestures.StickOriginY - Move.y * r;
                UiV21Art.DrawRing(new Rect(kx - k, ky - k, 2 * k, 2 * k), true);
            }
            else if (!zone.IsEmpty)
            {
                // Au repos : le cercle de 128 unités (course 40) à sa place de la maquette, centré dans sa zone de 144.
                float d = HudLayoutV21.Joystick * _layout.Unit, inset = (zone.W - d) / 2f;
                UiV21Art.DrawRing(new Rect(zone.X + inset, zone.Y + inset, d, d), false);
                float k = d * 0.3f;
                UiV21Art.DrawRing(new Rect(zone.CenterX - k, zone.CenterY - k, 2 * k, 2 * k), true);
            }
        }

        private static Rect Box(UiBox b) => new Rect(b.X, b.Y, b.W, b.H);
    }
}
