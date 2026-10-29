using System;
using System.Collections.Generic;

namespace EdS.Runtime
{
    /// <summary>Sous-lot 11.8 : étape d'un doigt dans l'image (comme <c>UnityEngine.TouchPhase</c>).</summary>
    public enum FingerPhase
    {
        Began,
        Moved,
        Stationary,
        Ended,
        Canceled,
    }

    /// <summary>Sous-lot 11.8 : un doigt de l'image, dans l'écran virtuel (x vers la droite, y vers le bas).</summary>
    public readonly struct FingerPoint
    {
        public FingerPoint(int id, float x, float y, FingerPhase phase)
        {
            Id = id;
            X = x;
            Y = y;
            Phase = phase;
        }

        public int Id { get; }
        public float X { get; }
        public float Y { get; }
        public FingerPhase Phase { get; }
    }

    /// <summary>Sous-lot 11.8 : zone d'un bouton tactile dans l'écran virtuel (coin haut gauche, largeur, hauteur), bords compris.</summary>
    public readonly struct GestureButton
    {
        public GestureButton(int id, float x, float y, float width, float height)
        {
            Id = id;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int Id { get; }
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }

        public bool Contains(float x, float y) => x >= X && x <= X + Width && y >= Y && y <= Y + Height;
    }

    /// <summary>
    /// Sous-lot 11.8 (revue Codex de la PR #36, 6e tour : extrait des commandes tactiles de Unity pour être testé) : ce que les doigts
    /// d'une image demandent. Un doigt posé sur un bouton le tient (et le touche à cette image) jusqu'à ce qu'il soit levé ; sur la moitié
    /// gauche hors des boutons, il devient le <b>stick</b> (origine là où il se pose, pleine vitesse à <see cref="StickRadius"/>) ; sur la
    /// moitié droite, il <b>tourne la vue</b> ; deux doigts sur la moitié droite <b>règlent la vue</b> par leur écart (le regard est alors
    /// suspendu). Un doigt mémorisé qui n'est plus sur l'écran (jeu suspendu sans fin de doigt) est relâché, comme levé ; le doigt de regard
    /// levé, un autre doigt resté sur la moitié droite reprend le regard aussitôt. Sans Unity (<c>netstandard2.1</c>) : le script
    /// <c>TouchControls</c> ne fait que lire les doigts, placer et dessiner les boutons.
    /// </summary>
    public sealed class TouchGestures
    {
        /// <summary>
        /// Taille des petits boutons (compétences, ultime, cible, vue, menu…), en pixels virtuels (écran de 1 080 de haut) : au moins 9 mm
        /// sur l'écran d'un téléphone (critère mobile du plan du lot 11). Revue Codex de la PR #36 (6e tour) : sur le Galaxy Note8 visé (1 440
        /// pixels à 522 ppp, environ 70 mm de haut à l'horizontale), 140 font environ 9,1 mm (112 n'en faisaient que 7,3).
        /// </summary>
        public const float SmallButton = 140f;

        /// <summary>Taille des grands boutons (garde, parade), en pixels virtuels : environ 9,7 mm sur le Note8.</summary>
        public const float BigButton = 150f;

        /// <summary>Hauteur de l'écran virtuel des commandes (pixels).</summary>
        public const float VirtualHeight = 1080f;

        /// <summary>Rayon du stick (pixels virtuels) : un pouce poussé jusqu'au bord marche à pleine vitesse.</summary>
        public const float StickRadius = 120f;

        /// <summary>Degrés de rotation de la vue par pixel virtuel glissé.</summary>
        public const float YawPerPixel = 0.25f;

        /// <summary>Degrés d'inclinaison de la vue par pixel virtuel glissé vers le haut ou le bas (essai réaliste 9).</summary>
        public const float PitchPerPixel = 0.2f;

        /// <summary>Écart (pixels virtuels) entre deux doigts qui fait passer de la caméra la plus lointaine à la plus proche.</summary>
        public const float PinchPixelsPerView = 600f;

        private readonly Dictionary<int, int> _pressed = new Dictionary<int, int>();
        private readonly HashSet<int> _held = new HashSet<int>();
        private readonly HashSet<int> _tapped = new HashSet<int>();
        private int _stickFinger = -1;
        private float _stickOriginX, _stickOriginY, _stickNowX, _stickNowY;
        private int _lookFinger = -1;
        private float _lookLastX, _lookLastY;
        private float _pinchLast = -1f;

        /// <summary>Marche voulue, x vers la droite de l'écran, y vers le haut (pousser le pouce vers le haut avance), longueur de 0 à 1.</summary>
        public float MoveX { get; private set; }

        /// <summary>Voir <see cref="MoveX"/>.</summary>
        public float MoveY { get; private set; }

        /// <summary>Rotation de la vue demandée à cette image, en degrés.</summary>
        public float YawDelta { get; private set; }

        /// <summary>Rapprochement de la vue demandé à cette image (positif : plus près ; une unité couvre toute la plage).</summary>
        public float ViewDelta { get; private set; }

        /// <summary>
        /// Essai réaliste 9 (relecture) : inclinaison de la vue demandée à cette image, en degrés (doigt qui descend : la caméra monte et
        /// regarde de plus haut ; doigt qui monte : elle descend derrière le personnage).
        /// </summary>
        public float PitchDelta { get; private set; }

        /// <summary>Un doigt tient le stick ; son origine est alors (<see cref="StickOriginX"/>, <see cref="StickOriginY"/>).</summary>
        public bool StickActive => _stickFinger >= 0;

        public float StickOriginX => _stickOriginX;
        public float StickOriginY => _stickOriginY;

        /// <summary>Un doigt au moins tient ce bouton.</summary>
        public bool IsHeld(int button) => _held.Contains(button);

        /// <summary>Un doigt s'est posé sur ce bouton à cette image.</summary>
        public bool WasTapped(int button) => _tapped.Contains(button);

        /// <summary>Oublie les doigts posés (menu ouvert, jeu suspendu, perte du focus) : plus rien n'est tenu ni demandé.</summary>
        public void Reset()
        {
            _pressed.Clear();
            _held.Clear();
            _tapped.Clear();
            _stickFinger = -1;
            _lookFinger = -1;
            _pinchLast = -1f;
            MoveX = MoveY = 0f;
            YawDelta = 0f;
            PitchDelta = 0f;
            ViewDelta = 0f;
        }

        /// <summary>
        /// Lit les doigts d'une image (<paramref name="fingers"/>, dans l'ordre de l'appareil) avec les boutons placés pour cette image
        /// (le premier qui contient le doigt l'emporte) ; <paramref name="width"/> : largeur de l'écran virtuel (sa moitié sépare le stick du
        /// regard).
        /// </summary>
        public void Update(IReadOnlyList<FingerPoint> fingers, IReadOnlyList<GestureButton> buttons, float width)
        {
            if (fingers is null) throw new ArgumentNullException(nameof(fingers));
            if (buttons is null) throw new ArgumentNullException(nameof(buttons));
            if (float.IsNaN(width) || float.IsInfinity(width) || width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            _tapped.Clear();
            YawDelta = 0f;
            PitchDelta = 0f;
            ViewDelta = 0f;
            float half = width / 2f;
            Forget(fingers);
            var looking = new List<FingerPoint>();
            foreach (FingerPoint touch in fingers)
            {
                switch (touch.Phase)
                {
                    case FingerPhase.Began:
                        if (Hit(buttons, touch.X, touch.Y) is int id)
                        {
                            _pressed[touch.Id] = id;
                            _held.Add(id);
                            _tapped.Add(id);
                        }
                        else if (touch.X < half && _stickFinger < 0)
                        {
                            _stickFinger = touch.Id;
                            _stickOriginX = _stickNowX = touch.X;
                            _stickOriginY = _stickNowY = touch.Y;
                        }
                        else if (touch.X >= half && _lookFinger < 0)
                        {
                            _lookFinger = touch.Id;
                            _lookLastX = touch.X;
                            _lookLastY = touch.Y;
                        }
                        break;
                    case FingerPhase.Moved:
                    case FingerPhase.Stationary:
                        if (touch.Id == _stickFinger)
                        {
                            _stickNowX = touch.X;
                            _stickNowY = touch.Y;
                        }
                        else if (touch.Id == _lookFinger)
                        {
                            YawDelta += (touch.X - _lookLastX) * YawPerPixel;
                            PitchDelta += (touch.Y - _lookLastY) * PitchPerPixel;
                            _lookLastX = touch.X;
                            _lookLastY = touch.Y;
                        }
                        break;
                    case FingerPhase.Ended:
                    case FingerPhase.Canceled:
                        Release(touch.Id);
                        break;
                }
                if (touch.X >= half && !_pressed.ContainsKey(touch.Id) && touch.Id != _stickFinger
                    && touch.Phase != FingerPhase.Ended && touch.Phase != FingerPhase.Canceled) looking.Add(touch);
            }
            // Deux doigts sur la moitié droite : l'écart règle la distance de la vue (s'écarter : plus près).
            if (looking.Count >= 2)
            {
                float dx = looking[0].X - looking[1].X, dy = looking[0].Y - looking[1].Y;
                float spread = (float)Math.Sqrt(dx * dx + dy * dy);
                if (_pinchLast >= 0f) ViewDelta = (spread - _pinchLast) / PinchPixelsPerView;
                _pinchLast = spread;
                YawDelta = 0f;
                PitchDelta = 0f;
            }
            else _pinchLast = -1f;
            // Revue Codex de la PR #36 (4e tour) : le doigt de regard levé (fin d'un pincement), un autre doigt resté sur la moitié droite
            // reprend le regard aussitôt, sans avoir à être levé puis reposé.
            if (_lookFinger < 0 && looking.Count > 0)
            {
                _lookFinger = looking[0].Id;
                _lookLastX = looking[0].X;
                _lookLastY = looking[0].Y;
            }
            if (_stickFinger < 0) MoveX = MoveY = 0f;
            else
            {
                // Écran : y vers le bas ; marche : y vers le haut.
                float x = (_stickNowX - _stickOriginX) / StickRadius, y = (_stickOriginY - _stickNowY) / StickRadius;
                float length = (float)Math.Sqrt(x * x + y * y);
                if (length > 1f)
                {
                    x /= length;
                    y /= length;
                }
                MoveX = x;
                MoveY = y;
            }
        }

        private static int? Hit(IReadOnlyList<GestureButton> buttons, float x, float y)
        {
            foreach (GestureButton b in buttons)
                if (b.Contains(x, y)) return b.Id;
            return null;
        }

        private void Release(int finger)
        {
            if (_pressed.TryGetValue(finger, out int released))
            {
                _pressed.Remove(finger);
                if (!_pressed.ContainsValue(released)) _held.Remove(released);
            }
            if (finger == _stickFinger) _stickFinger = -1;
            if (finger == _lookFinger) _lookFinger = -1;
        }

        /// <summary>
        /// Revue Codex de la PR #36 (2e tour) : Android peut suspendre le jeu (verrouillage, accueil, autre application) sans livrer la fin
        /// des doigts posés. Un doigt mémorisé (stick, regard, bouton tenu) qui n'est plus sur l'écran est relâché, comme s'il était levé.
        /// </summary>
        private void Forget(IReadOnlyList<FingerPoint> fingers)
        {
            bool Present(int id)
            {
                foreach (FingerPoint f in fingers)
                    if (f.Id == id) return true;
                return false;
            }
            if (_stickFinger >= 0 && !Present(_stickFinger)) _stickFinger = -1;
            if (_lookFinger >= 0 && !Present(_lookFinger)) _lookFinger = -1;
            foreach (int id in new List<int>(_pressed.Keys))
                if (!Present(id)) Release(id);
        }
    }
}
