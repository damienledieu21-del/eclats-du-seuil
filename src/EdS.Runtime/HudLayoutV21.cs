using System;

namespace EdS.Runtime
{
    /// <summary>Rectangle de l'interface (origine en haut à gauche, y vers le bas), dans le repère du canevas (1 080 de haut).</summary>
    public readonly struct UiBox
    {
        public readonly float X, Y, W, H;

        public UiBox(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public float Right => X + W;
        public float Bottom => Y + H;
        public float CenterX => X + W / 2f;
        public float CenterY => Y + H / 2f;
        public bool IsEmpty => W <= 0f || H <= 0f;

        /// <summary>Les deux rectangles se touchent, ou sont à moins de <paramref name="gap"/> l'un de l'autre.</summary>
        public bool Overlaps(UiBox o, float gap = 0f) =>
            !IsEmpty && !o.IsEmpty && X < o.Right + gap && o.X < Right + gap && Y < o.Bottom + gap && o.Y < Bottom + gap;

        public bool Inside(UiBox outer, float tolerance = 0.01f) =>
            X >= outer.X - tolerance && Y >= outer.Y - tolerance && Right <= outer.Right + tolerance && Bottom <= outer.Bottom + tolerance;

        public bool Contains(float x, float y) => x >= X && x < Right && y >= Y && y < Bottom;

        public override string ToString() => $"({X:0.#}, {Y:0.#}, {W:0.#} × {H:0.#})";
    }

    /// <summary>Marges de la zone sûre (encoche, caméra, gestes du système), dans le repère du canevas.</summary>
    public readonly struct UiInsets
    {
        public readonly float Left, Top, Right, Bottom;

        public UiInsets(float left, float top, float right, float bottom)
        {
            Left = Math.Max(0f, left);
            Top = Math.Max(0f, top);
            Right = Math.Max(0f, right);
            Bottom = Math.Max(0f, bottom);
        }

        public static readonly UiInsets None = new UiInsets(0f, 0f, 0f, 0f);
    }

    /// <summary>Éléments du HUD de l'interface V2.1 (« 01 HUD &amp; commandes de combat », page 20 du dossier).</summary>
    public enum HudPart
    {
        Vitals, Signal, Target, Compass, MiniMap, Effects, Quests, Group, Damage, Toasts, Prompt, Joystick,

        /// <summary>Revue Codex de la PR #161 : coup annoncé de la cible, sur sa ligne, sous la cible (assez large pour le texte le plus long).</summary>
        Announce,

        /// <summary>Revue Codex de la PR #161 : état du monde (événement, cicatrice), sur sa ligne, entre la boussole et les annonces.</summary>
        WorldState,
    }

    /// <summary>Boutons des commandes au doigt (téléphone), dans l'ordre de priorité de leur rangée.</summary>
    public enum HudButton
    {
        Parry, Guard, Skill1, Skill2, Skill3, Skill4, Skill5, Skill6, Ultimate, Potion, Target, Act, Ally,
        Pause, Menu, View, Bag, Journal, Talents, Workshop, Restart, NextScene, Help, More, Map,

        /// <summary>
        /// Esquive de la maquette (sous la parade). Réponse de Damien du 14 novembre 2026 (21 h 03) : il veut une vraie esquive, règle de
        /// combat d'un chantier séparé ; sa place est prévue ici et reste masquée tant que la règle n'existe pas (<c>dodge</c> faux).
        /// </summary>
        Dodge,
    }

    /// <summary>Ce qui est en jeu : entraînement (seul), coopération en réseau, aventure dans les Vallons.</summary>
    public enum HudMode { Training, Network, Adventure }

    /// <summary>Rôle d'un texte du HUD : sa taille vient de la charte V2.1 (page 3), selon PC ou téléphone.</summary>
    public enum HudFont { Title, Body, Button, Essential, Meta }

    /// <summary>
    /// Interface V2.1 (dossier du 12 novembre 2026, qui fait foi) : disposition du HUD et des commandes au doigt, sans Unity, pour
    /// n'importe quel format d'écran (16:9, 18,5:9 du Galaxy Note8, 20:9, 4:3). Les cotes viennent de la maquette cotée de la page 20 :
    /// PC 1 920 × 1 080 pixels, téléphone 960 × 432 unités en paysage. Repère : le canevas de l'interface (1 080 de haut, largeur selon le
    /// format), origine en haut à gauche.
    /// <list type="bullet">
    /// <item>chaque élément s'accroche à un bord (gauche, droite) ou au milieu, en haut ou en bas, dans la zone sûre (encoche, gestes) ;</item>
    /// <item>téléphone : une unité vaut 1 080 / 432 = 2,5 du canevas ; sous 800 unités de large (16:9, 4:3), l'unité diminue pour garder
    /// 800 unités, sans jamais réduire une cible tactile en unités (charte : « ne pas réduire les cibles ») ;</item>
    /// <item>les éléments du milieu (cible, boussole, annonces, dégâts, indication) se glissent entre ceux des bords, et rétrécissent au
    /// besoin jusqu'à leur largeur minimale ;</item>
    /// <item>la rangée des boutons utiles (pause, vue, sac…) se range à gauche de la mini-carte ; ce qui n'y tient pas passe dans le
    /// tiroir « Plus » (grille sous la mini-carte).</item>
    /// </list>
    /// Aucune allocation après la création : <see cref="Update"/> réécrit des tableaux de taille fixe. Affichage seulement : aucune règle de
    /// jeu ne passe par ici.
    /// </summary>
    public sealed class HudLayoutV21
    {
        public const float PhoneWidth = 960f, PhoneHeight = 432f, PcWidth = 1920f, PcHeight = 1080f;

        /// <summary>Largeur minimale du téléphone en unités (charte, page 4 : « à 16:9 ou sous 800 × 360 unités », sans réduire les cibles : 768 unités, le 16:9 à 2,5).</summary>
        public const float PhoneMinWidth = 768f;

        /// <summary>Cibles tactiles (charte, page 4 et kit page 57) : 48 au moins, 56 à 72 en combat, 8 d'écart, joystick 128 (zone 144).</summary>
        public const float TouchMin = 48f, CombatMin = 56f, Spacing = 8f, Joystick = 128f, JoystickZone = 144f;

        /// <summary>Case du tiroir « Plus » (unités) : assez large pour « Journal », « Talents », « Atelier ».</summary>
        public const float DrawerCellWidth = 112f;

        public const int MaxDrawer = 12;

        private static readonly int PartCount = Enum.GetValues(typeof(HudPart)).Length;
        private static readonly int ButtonCount = Enum.GetValues(typeof(HudButton)).Length;

        private readonly UiBox[] _parts = new UiBox[PartCount];
        private readonly UiBox[] _buttons = new UiBox[ButtonCount];
        private readonly bool[] _used = new bool[ButtonCount];
        private readonly bool[] _inDrawer = new bool[ButtonCount];
        private readonly UiBox[] _skills = new UiBox[7];
        private readonly HudButton[] _utility = new HudButton[ButtonCount];
        private int _utilityCount;

        private float _width = -1f, _height = -1f;
        private UiInsets _safe;
        private bool _phone;
        private HudMode _mode;
        private bool _dodge;

        /// <summary>Change à chaque nouvelle disposition (format, zone sûre, mode) : l'affichage ne replace ses éléments qu'alors.</summary>
        public int Version { get; private set; }

        /// <summary>Disposition du téléphone (commandes au doigt) plutôt que du PC.</summary>
        public bool Phone => _phone;

        public HudMode Mode => _mode;

        /// <summary>Le bouton Esquive est montré (la règle d'esquive existe).</summary>
        public bool DodgeShown => _dodge;

        /// <summary>Pixels du canevas par unité de la maquette (PC : 1 ; téléphone : 2,5 au plus).</summary>
        public float Unit { get; private set; }

        /// <summary>Largeur et hauteur disponibles en unités de la maquette.</summary>
        public float DesignWidth { get; private set; }

        public float DesignHeight { get; private set; }

        public float CanvasWidth => _width;
        public float CanvasHeight => _height;

        /// <summary>Zone sûre, dans le repère du canevas.</summary>
        public UiBox Safe { get; private set; }

        /// <summary>Tiroir « Plus » ouvert (la grille des boutons qui n'ont pas tenu dans la rangée) ; vide sans tiroir.</summary>
        public UiBox Drawer { get; private set; }

        /// <summary>Un élément du milieu n'a pas tenu même à sa largeur minimale (les tests l'interdisent pour les formats visés).</summary>
        public bool Overflow { get; private set; }

        public HudLayoutV21() => Update(PcWidth, PcHeight, UiInsets.None, false, HudMode.Training);

        public UiBox this[HudPart part] => _parts[(int)part];

        /// <summary>Rectangle d'un bouton (vide s'il n'existe pas dans ce mode ou sur PC).</summary>
        public UiBox Button(HudButton button) => _used[(int)button] ? _buttons[(int)button] : default;

        public bool Has(HudButton button) => _used[(int)button];

        /// <summary>Le bouton est rangé dans le tiroir « Plus » (montré seulement quand celui-ci est ouvert).</summary>
        public bool InDrawer(HudButton button) => _used[(int)button] && _inDrawer[(int)button];

        /// <summary>
        /// Place de compétence : 0 à 5 pour les touches 1 à 6, 6 pour l'ultime. Téléphone : le bouton tactile lui-même ; PC : la barre en bas au
        /// milieu (charte : « compétences 1-4 · pas 96 », étendue aux six places actives des règles et à l'ultime).
        /// </summary>
        public UiBox Skill(int index) => index >= 0 && index < _skills.Length ? _skills[index] : default;

        /// <summary>Boutons de la rangée utile puis du tiroir, dans l'ordre de priorité (sans allocation).</summary>
        public int UtilityCount => _utilityCount;

        public HudButton UtilityAt(int i) => _utility[i];

        /// <summary>Taille d'un texte du HUD dans le canevas (charte page 3 : PC 32/30/28/24/22 px ; téléphone 22/18/18/16/14 unités).</summary>
        public float Font(HudFont font)
        {
            switch (font)
            {
                case HudFont.Title: return _phone ? 22f * Unit : 32f;
                case HudFont.Body: return _phone ? 18f * Unit : 30f;
                case HudFont.Button: return _phone ? 18f * Unit : 28f;
                case HudFont.Essential: return _phone ? 16f * Unit : 24f;
                default: return _phone ? 14f * Unit : 22f;
            }
        }

        /// <summary>
        /// Taille du libellé d'un bouton tactile : texte essentiel (16 unités) à partir de 56 unités de large, métadonnée (14, le minimum de
        /// la charte) pour les boutons de 48.
        /// </summary>
        public float LabelFont(UiBox box) => box.W < (CombatMin - 0.5f) * Unit ? Font(HudFont.Meta) : Font(HudFont.Essential);

        /// <summary>
        /// Refait la disposition pour un canevas de <paramref name="width"/> × <paramref name="height"/>, la zone sûre <paramref name="safe"/>,
        /// commandes au doigt ou non, et le mode de jeu. Rien ne change (ni la version) si rien n'a changé.
        /// </summary>
        public void Update(float width, float height, UiInsets safe, bool phone, HudMode mode, bool dodge = false)
        {
            if (width == _width && height == _height && phone == _phone && mode == _mode && dodge == _dodge && safe.Left == _safe.Left && safe.Top == _safe.Top
                && safe.Right == _safe.Right && safe.Bottom == _safe.Bottom) return;
            if (!(width > 0f) || !(height > 0f)) throw new ArgumentOutOfRangeException(nameof(width), "canevas vide.");
            _width = width;
            _height = height;
            _safe = safe;
            _phone = phone;
            _mode = mode;
            _dodge = dodge;
            Version++;
            Safe = new UiBox(safe.Left, safe.Top, Math.Max(1f, width - safe.Left - safe.Right), Math.Max(1f, height - safe.Top - safe.Bottom));
            Array.Clear(_used, 0, _used.Length);
            Array.Clear(_inDrawer, 0, _inDrawer.Length);
            Array.Clear(_parts, 0, _parts.Length);
            _utilityCount = 0;
            Drawer = default;
            Overflow = false;
            if (phone) LayoutPhone();
            else LayoutPc();
            // On joue le téléphone à l'horizontale (cadrage, sujet 19 : « paysage pour jouer ») : en portrait, les cibles deviendraient
            // trop petites ; la disposition le signale.
            if (phone && width < height) Overflow = true;
        }

        // --- Accroches ------------------------------------------------------------------------------------------------------------------

        private enum H { Left, Center, Right }

        private enum V { Top, Bottom }

        private float _refW, _refH;

        /// <summary>Rectangle d'une cote de la maquette (x, y, l, h en unités), accroché au bord indiqué de la zone sûre.</summary>
        private UiBox Anchor(float x, float y, float w, float h, H hor, V ver)
        {
            float u = Unit;
            float left = hor == H.Left ? Safe.X + x * u
                : hor == H.Right ? Safe.Right - (_refW - x) * u
                : Safe.CenterX - (_refW / 2f - x) * u;
            float top = ver == V.Top ? Safe.Y + y * u : Safe.Bottom - (_refH - y) * u;
            return new UiBox(left, top, w * u, h * u);
        }

        private void Set(HudPart part, UiBox box) => _parts[(int)part] = box;

        private void Set(HudButton button, UiBox box)
        {
            _buttons[(int)button] = box;
            _used[(int)button] = true;
        }

        /// <summary>
        /// Élément du milieu : centré si possible, sinon glissé entre les éléments des bords qui partagent sa hauteur (8 unités d'écart),
        /// puis rétréci jusqu'à <paramref name="minW"/> ; au-delà, <see cref="Overflow"/>.
        /// </summary>
        private UiBox Between(UiBox wanted, float minW, HudPart[] blockers, int blockerCount, bool withButtons)
        {
            float gap = Spacing * Unit;
            float lo = Safe.X, hi = Safe.Right;
            float middle = Safe.CenterX;
            for (int i = 0; i < blockerCount; i++) Narrow(_parts[(int)blockers[i]], wanted, gap, middle, ref lo, ref hi);
            if (withButtons)
                for (int b = 0; b < ButtonCount; b++)
                    if (_used[b] && !_inDrawer[b]) Narrow(_buttons[b], wanted, gap, middle, ref lo, ref hi);
            float w = wanted.W;
            float room = hi - lo;
            if (w > room) w = Math.Max(minW * Unit, room);
            if (w > room + 0.01f) Overflow = true;
            float x = Math.Min(Math.Max(wanted.CenterX - w / 2f, lo), Math.Max(lo, hi - w));
            return new UiBox(x, wanted.Y, w, wanted.H);
        }

        private static void Narrow(UiBox b, UiBox wanted, float gap, float middle, ref float lo, ref float hi)
        {
            if (b.IsEmpty || b.Y >= wanted.Bottom || wanted.Y >= b.Bottom) return;
            if (b.CenterX < middle) lo = Math.Max(lo, b.Right + gap);
            else hi = Math.Min(hi, b.X - gap);
        }

        // --- PC -------------------------------------------------------------------------------------------------------------------------

        private static readonly HudPart[] PcSides = { HudPart.Vitals, HudPart.Signal, HudPart.Effects, HudPart.MiniMap, HudPart.Quests, HudPart.Group };

        private void LayoutPc()
        {
            _refW = PcWidth;
            _refH = PcHeight;
            // Canevas de 1 080 de haut : une unité de la maquette PC vaut un pixel du canevas, quel que soit le format.
            Unit = 1f;
            DesignWidth = Safe.W;
            DesignHeight = Safe.H;
            Set(HudPart.Vitals, Anchor(64, 64, 400, 100, H.Left, V.Top));
            Set(HudPart.Signal, Anchor(484, 64, 76, 76, H.Left, V.Top));
            Set(HudPart.Effects, Anchor(64, 180, 400, 44, H.Left, V.Top));
            Set(HudPart.Group, Anchor(64, 310, 360, 550, H.Left, V.Top));
            Set(HudPart.MiniMap, Anchor(1616, 64, 240, 200, H.Right, V.Top));
            Set(HudPart.Quests, Anchor(1456, 300, 400, 210, H.Right, V.Top));
            Set(HudPart.Joystick, default);
            Set(HudPart.Target, Between(Anchor(712, 64, 496, 128, H.Center, V.Top), 400f, PcSides, PcSides.Length, false));
            // Revue Codex de la PR #161 : coup annoncé et état du monde ont chacun leur ligne réservée ; la boussole et les annonces descendent
            // d'autant (cotes de la maquette décalées, aucune ligne ne se recouvre).
            Set(HudPart.Announce, Between(Anchor(710, 196, 500, 34, H.Center, V.Top), 360f, PcSides, PcSides.Length, false));
            Set(HudPart.Compass, Between(Anchor(680, 234, 560, 70, H.Center, V.Top), 400f, PcSides, PcSides.Length, false));
            Set(HudPart.WorldState, Between(Anchor(660, 308, 600, 34, H.Center, V.Top), 400f, PcSides, PcSides.Length, false));
            Set(HudPart.Toasts, Between(Anchor(720, 346, 480, 120, H.Center, V.Top), 360f, PcSides, PcSides.Length, false));
            Set(HudPart.Damage, Between(Anchor(850, 420, 320, 180, H.Center, V.Top), 240f, PcSides, PcSides.Length, false));
            Set(HudPart.Prompt, Between(Anchor(784, 824, 352, 64, H.Center, V.Bottom), 280f, PcSides, PcSides.Length, false));
            // Barre de compétences en bas au milieu : six places de 80 au pas de 96, puis l'ultime à 32 de la sixième.
            const float size = 80f, step = 96f, ultimateGap = 32f;
            float total = 5 * step + size + ultimateGap + size;
            float x0 = Safe.CenterX - total / 2f, y = Safe.Bottom - (PcHeight - 932f);
            for (int i = 0; i < 6; i++) _skills[i] = new UiBox(x0 + i * step, y, size, size);
            _skills[6] = new UiBox(x0 + 5 * step + size + ultimateGap, y, size, size);
        }

        // --- Téléphone ------------------------------------------------------------------------------------------------------------------

        private static readonly HudPart[] PhoneSides = { HudPart.Vitals, HudPart.Signal, HudPart.Effects, HudPart.MiniMap, HudPart.Quests, HudPart.Group };

        private void LayoutPhone()
        {
            _refW = PhoneWidth;
            _refH = PhoneHeight;
            Unit = Math.Min(Safe.H / PhoneHeight, Safe.W / PhoneMinWidth);
            DesignWidth = Safe.W / Unit;
            DesignHeight = Safe.H / Unit;

            // Bords : vie et endurance, Résonance, effets, groupe ou aide à gauche ; mini-carte et quêtes à droite.
            Set(HudPart.Vitals, Anchor(32, 16, 226, 54, H.Left, V.Top));
            Set(HudPart.Signal, Anchor(272, 16, 48, 48, H.Left, V.Top));
            Set(HudPart.Effects, Anchor(32, 80, 226, 28, H.Left, V.Top));
            Set(HudPart.Group, Anchor(32, 120, 160, 88, H.Left, V.Top));
            Set(HudPart.MiniMap, Anchor(824, 16, 104, 88, H.Right, V.Top));
            Set(HudPart.Quests, Anchor(680, 112, 248, 76, H.Right, V.Top));
            // Joystick : la zone de 144 autour du cercle de 128 (le stick suit le pouce posé dans la moitié gauche).
            Set(HudPart.Joystick, Anchor(40, 256, JoystickZone, JoystickZone, H.Left, V.Bottom));

            // Pouce droit (charte : attaque 72, esquive 64 ; la parade et la garde, plus l'esquive depuis la décision de Damien du 14 novembre 2026, 21 h 05, qui remplace L11-D4).
            Set(HudButton.Parry, Anchor(856, 256, 72, 72, H.Right, V.Bottom));
            // Esquive (réponse de Damien du 14 novembre 2026) : à sa place de la maquette, sous la parade, quand la règle existera ; la garde
            // monte alors à gauche de la parade, au-dessus de la rangée Cible / Agir. Sans esquive, la garde garde cette place.
            if (_dodge)
            {
                Set(HudButton.Dodge, Anchor(860, 336, 64, 64, H.Right, V.Bottom));
                Set(HudButton.Guard, Anchor(784, 200, 64, 64, H.Right, V.Bottom));
            }
            else Set(HudButton.Guard, Anchor(860, 336, 64, 64, H.Right, V.Bottom));
            for (int i = 0; i < 4; i++) Set(HudButton.Skill1 + i, Anchor(600 + 64 * i, 344, CombatMin, CombatMin, H.Right, V.Bottom));
            // Les règles ouvrent six places actives (sous-lot V1.8) : la 5e et la 6e prolongent la rangée vers la gauche.
            Set(HudButton.Skill5, Anchor(472, 344, CombatMin, CombatMin, H.Right, V.Bottom));
            Set(HudButton.Skill6, Anchor(536, 344, CombatMin, CombatMin, H.Right, V.Bottom));
            Set(HudButton.Ultimate, Anchor(600, 280, TouchMin, TouchMin, H.Right, V.Bottom));
            if (_mode == HudMode.Adventure) Set(HudButton.Potion, Anchor(656, 280, TouchMin, TouchMin, H.Right, V.Bottom));
            Set(HudButton.Target, Anchor(720, 280, TouchMin, TouchMin, H.Right, V.Bottom));
            if (_mode == HudMode.Adventure) Set(HudButton.Act, Anchor(792, 280, TouchMin, TouchMin, H.Right, V.Bottom));
            if (_mode == HudMode.Network) Set(HudButton.Ally, Anchor(792, 280, TouchMin, TouchMin, H.Right, V.Bottom));
            for (int i = 0; i < 6; i++) _skills[i] = _buttons[(int)HudButton.Skill1 + i];
            _skills[6] = _buttons[(int)HudButton.Ultimate];
            if (_mode == HudMode.Adventure) Set(HudButton.Map, _parts[(int)HudPart.MiniMap]);

            // Rangée utile : deux places gardées à gauche de la mini-carte avant de poser la cible, puis autant qu'il en tient.
            UiBox map = _parts[(int)HudPart.MiniMap];
            float size = TouchMin * Unit, gap = Spacing * Unit;
            UiBox reserved = new UiBox(map.X - gap - 2 * size - gap, map.Y, 2 * size + gap, size);
            HudPart[] sides = PhoneSides;
            _parts[(int)HudPart.Prompt] = reserved;   // place provisoire : la rangée compte comme un bord pour la cible
            Set(HudPart.Target, Between(Anchor(360, 16, 240, 70, H.Center, V.Top), 180f, WithReserved(sides), sides.Length + 1, false));
            // Revue Codex de la PR #161 : lignes réservées, de haut en bas : coup annoncé (20), boussole (42), état du monde (22), trois
            // annonces de 20 ; l'indication d'action descend juste au-dessus des boutons.
            Set(HudPart.Announce, Between(Anchor(330, 88, 300, 20, H.Center, V.Top), 210f, sides, sides.Length, false));
            Set(HudPart.Compass, Between(Anchor(352, 110, 272, 42, H.Center, V.Top), 200f, sides, sides.Length, false));
            Set(HudPart.WorldState, Between(Anchor(300, 154, 360, 22, H.Center, V.Top), 200f, sides, sides.Length, false));
            Set(HudPart.Damage, Between(Anchor(430, 150, 150, 70, H.Center, V.Top), 120f, sides, sides.Length, false));
            Set(HudPart.Toasts, Between(Anchor(300, 178, 360, 60, H.Center, V.Top), 200f, sides, sides.Length, false));
            Set(HudPart.Prompt, default);
            UtilityRow(map, size, gap);
            Set(HudPart.Prompt, Between(Anchor(330, 244, 300, 32, H.Center, V.Bottom), 200f, sides, sides.Length, true));
        }

        private HudPart[] _withReserved = new HudPart[8];

        private HudPart[] WithReserved(HudPart[] sides)
        {
            if (_withReserved.Length < sides.Length + 1) _withReserved = new HudPart[sides.Length + 1];
            Array.Copy(sides, _withReserved, sides.Length);
            _withReserved[sides.Length] = HudPart.Prompt;
            return _withReserved;
        }

        /// <summary>Boutons utiles du mode, par priorité (ceux du combat d'abord ; les écrans de l'aventure ; les commandes de l'essai).</summary>
        private void Utilities()
        {
            _utilityCount = 0;
            void Add(HudButton b) => _utility[_utilityCount++] = b;
            if (_mode == HudMode.Network)
            {
                Add(HudButton.Menu);
                Add(HudButton.View);
                Add(HudButton.Help);
                return;
            }
            Add(HudButton.Pause);
            Add(HudButton.View);
            if (_mode == HudMode.Adventure)
            {
                Add(HudButton.Bag);
                Add(HudButton.Journal);
                Add(HudButton.Talents);
                Add(HudButton.Workshop);
            }
            Add(HudButton.Restart);
            Add(HudButton.NextScene);
            Add(HudButton.Help);
            Add(HudButton.Menu);
        }

        private void UtilityRow(UiBox map, float size, float gap)
        {
            Utilities();
            UiBox target = _parts[(int)HudPart.Target];
            float limit = target.Bottom > map.Y && target.Y < map.Y + size ? target.Right + gap : Safe.X;
            int slots = 0;
            for (float x = map.X - gap - size; x >= limit - 0.01f; x -= size + gap) slots++;
            // Relecture indépendante de la PR #161 : deux places au moins (une action et « Plus ») ; si la cible les a mangées, la
            // disposition le dit (Overflow) au lieu de poser la rangée sur la cible en silence.
            if (slots < 2)
            {
                Overflow = true;
                slots = 2;
            }
            bool drawer = _utilityCount > slots;
            int inRow = drawer ? slots - 1 : _utilityCount;
            for (int i = 0; i < inRow; i++) Set(_utility[i], new UiBox(map.X - (i + 1) * (size + gap), map.Y, size, size));
            if (!drawer) return;
            Set(HudButton.More, new UiBox(map.X - slots * (size + gap), map.Y, size, size));
            // Tiroir : grille de cases de 112 × 48 sous la mini-carte, alignée sur son bord droit, cinq colonnes au plus (deux rangées pour dix boutons).
            int rest = Math.Min(MaxDrawer, _utilityCount - inRow);
            int columns = Math.Min(5, rest), rows = (rest + columns - 1) / columns;
            float cellW = DrawerCellWidth * Unit, pad = gap;
            float w = columns * cellW + (columns - 1) * gap + 2 * pad, h = rows * size + (rows - 1) * gap + 2 * pad;
            Drawer = new UiBox(map.Right - w, map.Bottom + gap, w, h);
            for (int i = 0; i < rest; i++)
            {
                HudButton b = _utility[inRow + i];
                int col = i % columns, row = i / columns;
                Set(b, new UiBox(Drawer.X + pad + col * (cellW + gap), Drawer.Y + pad + row * (size + gap), cellW, size));
                _inDrawer[(int)b] = true;
            }
        }
    }
}
