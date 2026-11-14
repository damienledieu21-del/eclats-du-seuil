using System.Collections.Generic;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.Simulation.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace EdS.Game
{
    /// <summary>
    /// L'interface en jeu, en uGUI, dans le style de l'<b>interface V2.1</b> (dossier du 12 novembre 2026, qui fait foi ; page 20 « HUD &amp;
    /// commandes de combat » : « survie, cible, action ; garder le centre de l'écran libre »). Elle remplace le sceau et les éclats de
    /// l'essai réaliste 6 (« Fragments du Seuil ») :
    /// <list type="bullet">
    /// <item>en haut à gauche : la vie et l'endurance en barres chiffrées, la Résonance dans sa case (« éclat au poignet »), les Charges de
    /// Rempart dessous, puis la garde et les altérations ;</item>
    /// <item>en haut au milieu : la cible (nom, vie, coup annoncé, altérations), la boussole (aventure : directions, heure) et l'état du monde
    /// (événement, cicatrice) ou le minuteur de l'entraînement ; les annonces dessous, trois au plus ;</item>
    /// <item>en haut à droite : la mini-carte peinte et le lieu, puis la quête suivie ;</item>
    /// <item>en bas : les compétences en cases carrées (PC : barre au milieu ; téléphone : aux rectangles mêmes des boutons tactiles) et
    /// l'indication d'action.</item>
    /// </list>
    /// Les places viennent de <see cref="HudLayoutV21"/> (sans Unity, testé) et ne sont refaites que quand la disposition change ; les textes
    /// fixes viennent des données (<see cref="UiTexts"/>) ; les matières sont dessinées par le code (<see cref="UiV21Art"/>). Les chiffres ne
    /// sont réécrits que quand ils changent (pas d'allocation à chaque image pour eux). Rien ne revient vers le moteur : l'interface montre
    /// l'état que le cœur donne.
    /// </summary>
    public sealed class HudView
    {
        private const float MapSize = 290f;
        private const float MapRadius = 20f;   // mètres montrés de chaque côté du joueur sur la petite carte
        private const int MaxToasts = 3;       // charte, page 56 : « file de 3 »

        private sealed class Shard
        {
            public RectTransform Node;
            public Image Icon, Cooldown, Flash, Ready, Dim;
            public UiText Key, Cost, Timer;
            public string SkillId;
            public double LastRemaining;
            public float FlashAlpha, ReadyAlpha;
            public long ShownCost = -1;
            public int ShownTenths = -1;
        }

        private sealed class Plate
        {
            public RectTransform Node;
            public UiText Name;
            public Image Fill, Telegraph;
            public string Id;
            public bool IsTarget;
        }

        private readonly Canvas _canvas;
        private readonly RectTransform _screen, _plates, _floats, _center;
        private HudLayoutV21 _layout;
        private int _placedVersion = -1;

        // Bandeau (pause), annonce au centre (défaite), indication d'action.
        private UiText _banner, _noticeText, _hint;
        private RectTransform _notice;
        private Image _noticeSlab, _hintPlate;

        // Vie, endurance, Résonance, Charges, garde et altérations.
        private RectTransform _vitals, _signal;
        private Image _healthTrack, _healthFill, _enduranceTrack, _enduranceFill, _resonanceFill;
        private UiText _healthText, _enduranceText, _resonanceText, _effects;
        private long _shownHealth = -1, _shownMaxHealth = -1, _shownEndurance = -1, _shownMaxEndurance = -1;
        private int _shownResonance = -1, _effectsKey = int.MinValue;
        private readonly List<Image> _charges = new List<Image>();

        /// <summary>Essai réaliste 7 : les Charges de Rempart sont montrées (au moins une détenue).</summary>
        public bool ChargesShown { get; private set; }
        private int _shownCharges = -1;
        private readonly List<Shard> _shards = new List<Shard>();

        // Cible.
        private RectTransform _target;
        private Image _targetTrack, _targetFill, _targetChunk, _targetFlash, _chip;
        private UiText _targetName, _targetHealth, _chipText, _targetStates;
        private float _chunk = 1f, _chunkHold;
        private string _targetId;
        private long _shownTargetHealth = -2, _shownTargetMax = -2;
        private int _chipKey = int.MinValue, _targetStatesKey = int.MinValue;

        // Boussole, état du monde ou minuteur.
        private RectTransform _compass;
        private readonly UiText[] _marks = new UiText[8];
        private UiText _compassSub, _clock;
        private bool _compassShown;

        // Carte, lieu, quête.
        private RectTransform _map, _markers, _quest;
        private RawImage _mapImage;
        private Image _you, _placePlate;
        private UiText _place, _placeSub, _questTitle, _questStep, _questProgress;
        private Image _questBar;
        private readonly List<Image> _markerPool = new List<Image>();
        private int _markerCount;
        private string _subZone, _subTime, _subFestival, _subMarket, _shownProgress;
        private IReadOnlyList<MapMarker> _mapMarkers;
        private AdventureSession _markersSession;
        private float _markersAt;
        private const float MarkersPeriod = 0.25f;

        // Aide, étiquettes, textes flottants, annonces.
        private RectTransform _help;
        private UiText _helpTitle, _helpContext;
        private IReadOnlyList<HelpKey> _helpRows;
        private readonly List<UiText> _helpKeys = new List<UiText>(), _helpLabels = new List<UiText>();
        private readonly List<Image> _helpPlates = new List<Image>();
        private readonly List<Plate> _platePool = new List<Plate>();
        private int _plateCount;
        private readonly List<UiText> _floatPool = new List<UiText>();
        private int _floatCount;
        private RectTransform _messages;
        private readonly UiText[] _toasts = new UiText[MaxToasts];
        private readonly string[] _toastValues = new string[MaxToasts];
        private int _toastCount;
        private readonly List<UiText> _labelPool = new List<UiText>();
        private int _labelCount;

        private float _time;
        private float _worldScale = 1f;

        // Essai réaliste 6, étape 3 : niveau gagné, vie basse, coup reçu par la cible.
        private RectTransform _levelUp;
        private UiText _levelUpTitle, _levelUpSub;
        private readonly List<Image> _levelUpRays = new List<Image>();
        private float _levelUpAge = 99f;
        private Image _dangerTop, _dangerBottom;
        private float _danger;
        private float _targetFlashAlpha;
        private float _lastTargetFraction = -1f;

        private HudView()
        {
            _canvas = UiKit.CreateCanvas("Interface (V2.1)", 10);
            Transform root = _canvas.transform;
            _plates = UiKit.Fill(UiKit.Node(root, "Étiquettes"));
            _floats = UiKit.Fill(UiKit.Node(root, "Textes flottants"));
            _screen = UiKit.Fill(UiKit.Node(root, "HUD"));
            _center = Anchored(root, "Centre", new Vector2(0.5f, 1f), new Vector2(900f, 1080f));
            BuildVitals();
            BuildTarget();
            BuildCompass();
            BuildMap();
            BuildHelp();
            BuildCenter();
            BuildSettings();
            _layout = new HudLayoutV21();
        }

        /// <summary>L'interface en jeu (un canevas pour toute la partie).</summary>
        public static HudView Create() => new HudView();

        /// <summary>Groupe ancré à un bord ou un coin de l'écran (point d'ancrage : 0 à 1, origine en bas à gauche, comme uGUI).</summary>
        private static RectTransform Anchored(Transform parent, string name, Vector2 anchor, Vector2 size)
        {
            RectTransform rect = UiKit.Node(parent, name);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private static RectTransform At(Component c, float x, float y, float w, float h, float px = 0f, float py = 0f) =>
            UiKit.Place(c is RectTransform r ? r : (RectTransform)c.transform, new Vector2(x, y), new Vector2(w, h), new Vector2(px, py));

        private static RectTransform At(Component c, UiBox b) => At(c, b.X, b.Y, b.W, b.H);

        private static Image Panel(Transform parent, string name) => UiKit.Image(parent, name, UiV21Art.Panel, Color.white, Image.Type.Sliced);

        private static Image Filled(Transform parent, string name, Color color, Image.FillMethod method)
        {
            Image bar = UiKit.Image(parent, name, UiV21Art.Plain, color, Image.Type.Filled);
            bar.fillMethod = method;
            if (method == Image.FillMethod.Horizontal) bar.fillOrigin = (int)Image.OriginHorizontal.Left;
            else bar.fillOrigin = (int)Image.OriginVertical.Bottom;
            return bar;
        }

        private static UiText Text(Transform parent, string name, UiFace face, Color color, TextAnchor anchor)
        {
            UiText t = UiText.Create(parent, name, face, 20f, color, anchor);
            t.Outline(new Color(0.02f, 0.03f, 0.04f, 0.95f), 0.18f);
            return t;
        }

        // --- Construction ----------------------------------------------------------------------------------------------------------

        private void BuildVitals()
        {
            _vitals = UiKit.Node(_screen, "Vie et endurance");
            UiKit.Fill(Panel(_vitals, "Cuir").rectTransform);
            _healthTrack = UiKit.Image(_vitals, "Rail de la vie", UiV21Art.Track, Color.white, Image.Type.Sliced);
            _healthFill = Filled(_healthTrack.rectTransform, "Vie", UiV21Art.Blood, Image.FillMethod.Horizontal);
            UiKit.Fill(_healthFill.rectTransform, 2f);
            _healthText = Text(_healthTrack.rectTransform, "Valeur", UiFace.Bold, UiV21Art.Parchment, TextAnchor.MiddleCenter);
            UiKit.Fill(_healthText.Rect);
            _enduranceTrack = UiKit.Image(_vitals, "Rail de l'endurance", UiV21Art.Track, Color.white, Image.Type.Sliced);
            _enduranceFill = Filled(_enduranceTrack.rectTransform, "Endurance", UiV21Art.Stamina, Image.FillMethod.Horizontal);
            UiKit.Fill(_enduranceFill.rectTransform, 2f);
            _enduranceText = Text(_enduranceTrack.rectTransform, "Valeur", UiFace.Bold, UiV21Art.Parchment, TextAnchor.MiddleCenter);
            UiKit.Fill(_enduranceText.Rect);

            // Résonance : la case de l'Éclat (« éclat au poignet / signal »), remplie de bas en haut d'une lueur du Seuil discrète.
            _signal = UiKit.Node(_screen, "Résonance");
            UiKit.Fill(Panel(_signal, "Cuir").rectTransform);
            _resonanceFill = Filled(_signal, "Lueur", UiKit.Alpha(UiV21Art.Seuil, 0.55f), Image.FillMethod.Vertical);
            _resonanceText = Text(_signal, "Valeur", UiFace.Number, UiV21Art.Parchment, TextAnchor.MiddleCenter);
            UiKit.Fill(_resonanceText.Rect);

            _effects = Text(_screen, "Garde et altérations", UiFace.Bold, UiV21Art.Parchment, TextAnchor.MiddleLeft);
        }

        private void BuildTarget()
        {
            _target = UiKit.Node(_screen, "Cible");
            UiKit.Fill(Panel(_target, "Cuir").rectTransform);
            _targetName = Text(_target, "Nom", UiFace.Bold, UiV21Art.Parchment, TextAnchor.MiddleCenter);
            _targetTrack = UiKit.Image(_target, "Rail", UiV21Art.Track, Color.white, Image.Type.Sliced);
            _targetChunk = Filled(_targetTrack.rectTransform, "Part perdue", UiKit.Alpha(new Color(1f, 0.95f, 0.85f), 0.9f), Image.FillMethod.Horizontal);
            UiKit.Fill(_targetChunk.rectTransform, 2f);
            _targetFill = Filled(_targetTrack.rectTransform, "Vie", UiV21Art.Blood, Image.FillMethod.Horizontal);
            UiKit.Fill(_targetFill.rectTransform, 2f);
            _targetFlash = Filled(_targetTrack.rectTransform, "Coup reçu", UiKit.Alpha(Color.white, 0f), Image.FillMethod.Horizontal);
            UiKit.Fill(_targetFlash.rectTransform, 2f);
            _targetFlash.fillAmount = 1f;
            _targetHealth = Text(_targetTrack.rectTransform, "Points de vie", UiFace.Bold, UiV21Art.Parchment, TextAnchor.MiddleCenter);
            UiKit.Fill(_targetHealth.Rect);
            _chip = UiKit.Image(_target, "Annonce", UiV21Art.ButtonSprite, Color.white, Image.Type.Sliced);
            _chipText = Text(_chip.rectTransform, "Annonce", UiFace.Bold, UiV21Art.BrightGold, TextAnchor.MiddleCenter);
            UiKit.Fill(_chipText.Rect, 4f);
            _targetStates = Text(_target, "Altérations", UiFace.Body, UiKit.Dawn, TextAnchor.MiddleLeft);
            UiKit.Show(_target, false);
        }

        private void BuildCompass()
        {
            _compass = UiKit.Node(_screen, "Boussole");
            Image strip = UiKit.Image(_compass, "Bande", UiV21Art.Track, UiKit.Alpha(Color.white, 0.75f), Image.Type.Sliced);
            UiKit.Fill(strip.rectTransform);
            for (int i = 0; i < _marks.Length; i++)
            {
                _marks[i] = Text(_compass, "Direction", i % 2 == 0 ? UiFace.Bold : UiFace.Body, i % 2 == 0 ? UiV21Art.BrightGold : UiV21Art.Parchment,
                    TextAnchor.MiddleCenter);
            }
            _compassSub = Text(_compass, "Heure", UiFace.Body, UiKit.Alpha(UiV21Art.Parchment, 0.85f), TextAnchor.MiddleCenter);
            _clock = Text(_screen, "État du monde ou minuteur", UiFace.Bold, UiV21Art.BrightGold, TextAnchor.MiddleCenter);
            UiKit.Show(_compass, false);
        }

        private void BuildMap()
        {
            _map = UiKit.Node(_screen, "Carte");
            RectTransform mask = UiKit.Fill(UiKit.Mask(_map, "Forme", UiKit.Sprite("carte_masque")));
            _mapImage = UiKit.Raw(mask, "Carte peinte", null, Color.white);
            UiKit.Fill(_mapImage.rectTransform);
            UiKit.Fill(UiKit.Image(mask, "Vignette", UiKit.Sprite("carte_vignette"), Color.white).rectTransform);
            _markers = UiKit.Fill(UiKit.Node(mask, "Repères"));
            _you = UiKit.Image(mask, "Toi", UiKit.Sprite("repere_joueur"), Color.white);
            At(_you, MapSize / 2f, MapSize / 2f, 20f, 24f, 0.5f, 0.5f);
            At(UiKit.Image(_map, "Cadre", UiKit.Sprite("carte_cadre"), Color.white), -30f, -30f, MapSize + 60f, MapSize + 60f);
            _placePlate = UiKit.Image(_screen, "Fond du lieu", UiV21Art.Panel, Color.white, Image.Type.Sliced);
            _place = Text(_placePlate.rectTransform, "Lieu", UiFace.Bold, UiV21Art.Parchment, TextAnchor.MiddleCenter);
            UiKit.Fill(_place.Rect, 2f);
            _placeSub = Text(_screen, "Zone et heure", UiFace.Body, UiKit.Muted, TextAnchor.MiddleCenter);

            _quest = UiKit.Node(_screen, "Quête");
            UiKit.Fill(Panel(_quest, "Cuir").rectTransform);
            _questTitle = Text(_quest, "Titre", UiFace.Bold, UiV21Art.BrightGold, TextAnchor.MiddleLeft);
            _questStep = Text(_quest, "Étape", UiFace.Body, UiV21Art.Parchment, TextAnchor.UpperLeft);
            _questStep.Wrap();
            _questProgress = Text(_quest, "Avancée", UiFace.Bold, UiKit.Dawn, TextAnchor.MiddleRight);
            _questBar = Filled(_quest, "Barre", UiV21Art.Gold, Image.FillMethod.Horizontal);

            _messages = UiKit.Node(_screen, "Annonces");
            for (int i = 0; i < MaxToasts; i++)
            {
                _toasts[i] = Text(_messages, "Annonce", UiFace.Body, new Color(1f, 0.95f, 0.8f), TextAnchor.MiddleCenter);
                _toasts[i].Show(false);
            }
        }

        private void BuildHelp()
        {
            _help = At(UiKit.Node(_screen, "Aide"), 24f, 24f, 400f, 318f);
            UiKit.Fill(Panel(_help, "Cuir").rectTransform);
            _helpTitle = UiText.Create(_help, "Titre", UiFace.Title, 18f, Color.white, TextAnchor.MiddleLeft, 2f);
            At(_helpTitle.Rect, 22f, 30f, 360f, 24f, 0f, 0.5f);
            _helpTitle.Gold();
            _helpContext = UiText.Create(_help, "Contexte", UiFace.Body, 16f, UiKit.Muted, TextAnchor.MiddleLeft, 2f);
            At(_helpContext.Rect, 22f, 56f, 360f, 22f, 0f, 0.5f);
        }

        private void HelpRow(int i)
        {
            Image plate = UiKit.Image(_help, "Touche", UiV21Art.ButtonSprite, Color.white, Image.Type.Sliced);
            UiText key = UiText.Create(plate.rectTransform, "Lettre", UiFace.Bold, 14f, UiKit.Dawn, TextAnchor.MiddleCenter);
            UiKit.Fill(key.Rect);
            UiText label = UiText.Create(_help, "Action", UiFace.Body, 17f, UiKit.Ink, TextAnchor.MiddleLeft);
            float x = 22f + (i % 2) * 186f, y = 76f + (i / 2) * 30f;
            At(plate, x, y, 66f, 24f);
            At(label.Rect, x + 74f, y + 12f, 112f, 24f, 0f, 0.5f);
            _helpPlates.Add(plate);
            _helpKeys.Add(key);
            _helpLabels.Add(label);
        }

        /// <summary>Case carrée d'une compétence (charte : icône, recharge en voile, coût en haut à droite, touche en haut à gauche).</summary>
        private Shard BuildShard(int index)
        {
            var shard = new Shard();
            shard.Node = UiKit.Node(_screen, index == 6 ? "Ultime" : "Compétence " + (index + 1));
            UiKit.Fill(UiKit.Image(shard.Node, "Cuir", UiV21Art.ButtonSprite, Color.white, Image.Type.Sliced).rectTransform);
            RectTransform inside = UiKit.Fill(UiKit.Mask(shard.Node, "Intérieur", null), 4f);
            shard.Icon = UiKit.Image(inside, "Icône", null, Color.white);
            UiKit.Fill(shard.Icon.rectTransform);
            shard.Dim = UiKit.Image(inside, "Indisponible", null, new Color(0.02f, 0.03f, 0.05f, 0f));
            UiKit.Fill(shard.Dim.rectTransform);
            shard.Cooldown = Filled(inside, "Recharge", new Color(0.02f, 0.03f, 0.05f, 0.78f), Image.FillMethod.Radial360);
            UiKit.Fill(shard.Cooldown.rectTransform);
            shard.Cooldown.fillOrigin = (int)Image.Origin360.Top;
            shard.Cooldown.fillClockwise = false;
            shard.Ready = UiKit.Image(inside, "Prête", UiV21Art.Plain, UiKit.Alpha(UiKit.Dawn, 0f));
            UiKit.Fill(shard.Ready.rectTransform);
            shard.Flash = UiKit.Image(inside, "Éclair", null, UiKit.Alpha(Color.white, 0f));
            UiKit.Fill(shard.Flash.rectTransform);
            shard.Timer = Text(shard.Node, "Recharge", UiFace.Number, Color.white, TextAnchor.MiddleCenter);
            UiKit.Fill(shard.Timer.Rect);
            shard.Cost = Text(shard.Node, "Coût", UiFace.Bold, UiKit.Dawn, TextAnchor.UpperRight);
            shard.Key = Text(shard.Node, "Touche", UiFace.Bold, UiV21Art.Parchment, TextAnchor.UpperLeft);
            return shard;
        }

        // --- Réglages (essai réaliste 9) -----------------------------------------------------------------------------------------

        private RectTransform _settings;
        private UiButton _mouseMode, _invertY, _textures;
        private UiText _sensitivity, _brightness;
        private float _shownSensitivity = float.NaN;
        private UiText _settingsTitle;
        private UiButton _slower, _faster, _darker, _brighter, _creatorButton;
        private System.Action _toggleClickLook, _toggleInvert;
        private System.Action<float> _stepSensitivity;
        private System.Action _openCreator;

        /// <summary>Sous-lot V2.16 : cadre du centre de l'écran, où le créateur de personnage pose sa fenêtre.</summary>
        public RectTransform CenterFrame => _center;

        /// <summary>Sous-lot V2.16 : action du bouton « Mon personnage » des réglages (ouvre le créateur de personnage).</summary>
        public void OnCreator(System.Action openCreator) => _openCreator = openCreator;

        /// <summary>Actions des boutons des réglages : souris libre ou clic maintenu, haut-bas inversé, sensibilité (pas de ±0,25).</summary>
        public void OnSettings(System.Action toggleClickLook, System.Action toggleInvert, System.Action<float> stepSensitivity)
        {
            _toggleClickLook = toggleClickLook;
            _toggleInvert = toggleInvert;
            _stepSensitivity = stepSensitivity;
        }

        /// <summary>
        /// Essai réaliste 9 (Damien, 29 octobre 2026 : « sans clic comme maintenant mais on peut le changer dans les réglages ») : les
        /// réglages de la caméra, montrés pendant la pause (le curseur y est rendu).
        /// </summary>
        public void Settings(bool shown, bool clickLook, bool invertY, float sensitivity)
        {
            UiKit.Show(_settings, shown);
            if (!shown) return;
            // Libellés lus dans les données à l'affichage (le HUD est construit avant le chargement des textes).
            _settingsTitle.Value = UiTexts.Get(HudTextsV21.SettingsTitle);
            _slower.Text.Value = _darker.Text.Value = UiTexts.Get(HudTextsV21.Minus);
            _faster.Text.Value = _brighter.Text.Value = UiTexts.Get(HudTextsV21.Plus);
            _creatorButton.Text.Value = UiTexts.Get(HudTextsV21.Creator);
            _mouseMode.Text.Value = UiTexts.Get(clickLook ? HudTextsV21.CameraClick : HudTextsV21.CameraFree);
            _invertY.Text.Value = UiTexts.Get(invertY ? HudTextsV21.InvertOn : HudTextsV21.InvertOff);
            if (sensitivity != _shownSensitivity)
            {
                _shownSensitivity = sensitivity;
                _sensitivity.Value = string.Format(UiTexts.Get(HudTextsV21.Sensitivity),
                    sensitivity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ','));
            }
            _textures.Text.Value = DeviceQuality.Label;   // sous-lot V2.12.4
            _brightness.Value = BrightnessSetting.Label;   // sous-lot V2.13
        }

        private void BuildSettings()
        {
            // Relecture : entre le bandeau « Pause » et l'indication du bas ; montré seulement sans annonce (fin de combat, défaite).
            _settings = At(UiKit.Node(_center, "Réglages"), 450f, 540f, 560f, 420f, 0.5f, 0.5f);   // V2.13 : luminosité ; V2.16 : Mon personnage
            UiKit.Fill(UiKit.Image(_settings, "Dalle", UiKit.WindowSprite(), Color.white, Image.Type.Sliced).rectTransform);
            _settingsTitle = UiText.Create(_settings, "Titre", UiFace.Title, 26f, Color.white, TextAnchor.MiddleCenter, 4f);
            At(_settingsTitle.Rect, 280f, 34f, 500f, 36f, 0.5f, 0.5f);
            _settingsTitle.Gold();
            _mouseMode = UiKit.Button(_settings, "Souris", "bouton_sobre", "", UiFace.Bold, 18f, UiKit.Ink, () => _toggleClickLook?.Invoke());
            At(_mouseMode.Rect, 280f, 86f, 440f, 44f, 0.5f, 0.5f);
            _invertY = UiKit.Button(_settings, "Haut-bas", "bouton_sobre", "", UiFace.Bold, 18f, UiKit.Ink, () => _toggleInvert?.Invoke());
            At(_invertY.Rect, 280f, 140f, 440f, 44f, 0.5f, 0.5f);
            _slower = UiKit.Button(_settings, "Moins sensible", "bouton_sobre", "", UiFace.Title, 24f, UiKit.Ink, () => _stepSensitivity?.Invoke(-0.25f));
            At(_slower.Rect, 120f, 196f, 52f, 44f, 0.5f, 0.5f);
            _sensitivity = UiText.Create(_settings, "Sensibilité", UiFace.Bold, 18f, UiKit.Dawn, TextAnchor.MiddleCenter, 6f, true);
            At(_sensitivity.Rect, 280f, 196f, 240f, 40f, 0.5f, 0.5f);
            _faster = UiKit.Button(_settings, "Plus sensible", "bouton", "", UiFace.Title, 24f, new Color(0.1f, 0.05f, 0.02f), () => _stepSensitivity?.Invoke(0.25f));
            At(_faster.Rect, 440f, 196f, 52f, 44f, 0.5f, 0.5f);
            // Sous-lot V2.12.4 : qualité des textures (auto, pleine, moitié, quart), sauvegardée et appliquée tout de suite.
            _textures = UiKit.Button(_settings, "Textures", "bouton_sobre", "", UiFace.Bold, 18f, UiKit.Ink, DeviceQuality.Cycle);
            At(_textures.Rect, 280f, 250f, 440f, 44f, 0.5f, 0.5f);
            // Sous-lot V2.13 (« le jeu est trop sombre ») : luminosité de 50 % à 200 %, par pas de 10, sauvegardée et appliquée en direct.
            _darker = UiKit.Button(_settings, "Moins lumineux", "bouton_sobre", "", UiFace.Title, 24f, UiKit.Ink, () => BrightnessSetting.Step(-1));
            At(_darker.Rect, 120f, 304f, 52f, 44f, 0.5f, 0.5f);
            _brightness = UiText.Create(_settings, "Luminosité", UiFace.Bold, 18f, UiKit.Dawn, TextAnchor.MiddleCenter, 6f, true);
            At(_brightness.Rect, 280f, 304f, 240f, 40f, 0.5f, 0.5f);
            _brighter = UiKit.Button(_settings, "Plus lumineux", "bouton", "", UiFace.Title, 24f, new Color(0.1f, 0.05f, 0.02f), () => BrightnessSetting.Step(1));
            At(_brighter.Rect, 440f, 304f, 52f, 44f, 0.5f, 0.5f);
            // Sous-lot V2.16 : le créateur de personnage (corps, coiffure, couleurs).
            _creatorButton = UiKit.Button(_settings, "Mon personnage", "bouton_sobre", "", UiFace.Bold, 18f, UiKit.Ink, () => _openCreator?.Invoke());
            At(_creatorButton.Rect, 280f, 358f, 440f, 44f, 0.5f, 0.5f);
            UiKit.Show(_settings, false);
        }

        private void BuildCenter()
        {
            _banner = UiText.Create(_center, "Bandeau", UiFace.Title, 44f, Color.white, TextAnchor.MiddleCenter, 12f);
            At(_banner.Rect, 450f, 330f, 900f, 60f, 0.5f, 0.5f);
            _banner.Gold();
            _notice = At(UiKit.Node(_center, "Annonce"), 450f, 470f, 840f, 150f, 0.5f, 0.5f);
            _noticeSlab = UiKit.Image(_notice, "Cuir", UiV21Art.Panel, Color.white, Image.Type.Sliced);
            UiKit.Fill(_noticeSlab.rectTransform);
            _noticeText = UiText.Create(_notice, "Texte", UiFace.Title, 26f, UiV21Art.Parchment, TextAnchor.MiddleCenter, 2f);
            UiKit.Fill(_noticeText.Rect, 24f);
            _hintPlate = UiKit.Image(_screen, "Fond de l'indication", UiV21Art.Panel, Color.white, Image.Type.Sliced);
            _hint = Text(_hintPlate.rectTransform, "Indication", UiFace.Bold, UiV21Art.BrightGold, TextAnchor.MiddleCenter);
            UiKit.Fill(_hint.Rect, 6f);

            // Étape 3 : niveau gagné (titre doré, rayons de lumière), puis bords rouges quand la vie est basse.
            _levelUp = At(UiKit.Node(_center, "Niveau gagné"), 450f, 250f, 900f, 200f, 0.5f, 0.5f);
            for (int i = 0; i < 10; i++)
            {
                Image ray = UiKit.Image(_levelUp, "Rayon", UiKit.Sprite("reflet"), UiKit.Alpha(i % 2 == 0 ? UiKit.Dawn : UiKit.Rose, 0f));
                At(ray, 450f, 100f, 40f, 260f, 0.5f, 0f);
                ray.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * 36f);
                _levelUpRays.Add(ray);
            }
            _levelUpTitle = UiText.Create(_levelUp, "Titre", UiFace.Title, 64f, Color.white, TextAnchor.MiddleCenter, 8f);
            At(_levelUpTitle.Rect, 450f, 90f, 900f, 80f, 0.5f, 0.5f);
            _levelUpTitle.Gold();
            _levelUpSub = UiText.Create(_levelUp, "Sous-titre", UiFace.Bold, 22f, UiKit.Teal, TextAnchor.MiddleCenter, 14f, true);
            At(_levelUpSub.Rect, 450f, 148f, 900f, 30f, 0.5f, 0.5f);
            UiKit.Show(_levelUp, false);
            // Relecture : voiles blancs (transparence seule) teintés de rouge.
            _dangerTop = UiKit.Image(_canvas.transform, "Vie basse (haut)", UiKit.Sprite("bord_haut"), UiKit.Alpha(UiKit.Health, 0f));
            _dangerTop.rectTransform.anchorMin = new Vector2(0f, 1f);
            _dangerTop.rectTransform.anchorMax = new Vector2(1f, 1f);
            _dangerTop.rectTransform.pivot = new Vector2(0.5f, 1f);
            _dangerTop.rectTransform.sizeDelta = new Vector2(0f, 220f);
            _dangerTop.rectTransform.anchoredPosition = new Vector2(0f, 60f);
            _dangerBottom = UiKit.Image(_canvas.transform, "Vie basse (bas)", UiKit.Sprite("bord_bas"), UiKit.Alpha(UiKit.Health, 0f));
            _dangerBottom.rectTransform.anchorMin = new Vector2(0f, 0f);
            _dangerBottom.rectTransform.anchorMax = new Vector2(1f, 0f);
            _dangerBottom.rectTransform.pivot = new Vector2(0.5f, 0f);
            _dangerBottom.rectTransform.sizeDelta = new Vector2(0f, 300f);
            _dangerBottom.rectTransform.SetAsFirstSibling();
            _dangerTop.rectTransform.SetAsFirstSibling();
        }

        // --- Disposition (interface V2.1) ------------------------------------------------------------------------------------------

        /// <summary>
        /// Disposition de l'image (format, zone sûre, téléphone ou PC, mode), tenue par le jeu et partagée avec les commandes au doigt ;
        /// les éléments ne sont replacés que quand elle change.
        /// </summary>
        public void Layout(HudLayoutV21 layout)
        {
            if (layout == null) return;
            _layout = layout;
            if (_placedVersion == layout.Version) return;
            _placedVersion = layout.Version;
            Place();
        }

        private void Place()
        {
            HudLayoutV21 l = _layout;
            float u = l.Unit, essential = l.Font(HudFont.Essential), meta = l.Font(HudFont.Meta), body = l.Font(HudFont.Body);
            // Téléphone : les textes de la scène (étiquettes des ennemis, chiffres de combat) et les fenêtres du centre grossissent avec
            // l'unité (correction « textes trop petits ») ; PC : tailles d'origine.
            _worldScale = l.Phone ? 1.6f * u / 2.5f : 1f;
            float centerScale = l.Phone ? 1.4f * u / 2.5f : 1f;
            Vector3 cs = new Vector3(centerScale, centerScale, 1f);
            _notice.localScale = cs;
            _settings.localScale = cs;
            _banner.Rect.localScale = cs;

            // Vie et endurance : deux barres chiffrées dans le panneau.
            UiBox v = l[HudPart.Vitals];
            At(_vitals, v);
            float pad = 6f * u * (l.Phone ? 1f : 1.2f), rowH = (v.H - 3f * pad) / 2f;
            At(_healthTrack, pad, pad, v.W - 2f * pad, rowH);
            At(_enduranceTrack, pad, 2f * pad + rowH, v.W - 2f * pad, rowH);
            _healthText.Size = essential;
            _enduranceText.Size = essential;
            _shownHealth = _shownEndurance = -1;
            At(_signal, l[HudPart.Signal]);
            UiKit.Fill(_resonanceFill.rectTransform, 3f * u);
            _resonanceText.Size = essential;
            UiBox e = l[HudPart.Effects];
            At(_effects.Rect, e);
            _effects.Size = meta;
            _shownCharges = -1;

            // Cible : nom, barre chiffrée, puis altérations à gauche et coup annoncé à droite.
            UiBox t = l[HudPart.Target];
            At(_target, t);
            float tp = 6f * u * (l.Phone ? 1f : 1.2f), nameH = t.H * 0.32f, barH = t.H * 0.3f, lowH = t.H - nameH - barH - 2f * tp;
            At(_targetName.Rect, tp, tp * 0.5f, t.W - 2f * tp, nameH);
            _targetName.Size = essential;
            At(_targetTrack, tp, tp * 0.5f + nameH, t.W - 2f * tp, barH);
            _targetHealth.Size = meta;
            At(_targetStates.Rect, tp, tp + nameH + barH, t.W * 0.5f, lowH);
            _targetStates.Size = meta;
            At(_chip, t.W * 0.42f, tp + nameH + barH, t.W * 0.58f - tp, lowH);
            _chipText.Size = meta;

            // Boussole : directions sur la bande, heure dessous ; l'état du monde (ou le minuteur) sous la boussole.
            UiBox c = l[HudPart.Compass];
            At(_compass, c);
            for (int i = 0; i < _marks.Length; i++)
            {
                At(_marks[i].Rect, 0f, 0f, 3f * essential, c.H * 0.58f, 0.5f, 0f);
                _marks[i].Size = i % 2 == 0 ? essential : meta;
                _marks[i].Value = UiTexts.Get(HudTextsV21.Compass[i]);
            }
            At(_compassSub.Rect, 0f, c.H * 0.56f, c.W, c.H * 0.44f);
            _compassSub.Size = meta;
            _clock.Size = essential;
            PlaceClock();

            // Mini-carte : la carte peinte au plus grand carré du cadre, le lieu sur sa bande basse ; sur PC, zone et heure dessous.
            UiBox m = l[HudPart.MiniMap];
            float side = Mathf.Min(m.W, m.H), k = side / (MapSize + 60f);
            At(_map, m.CenterX, m.CenterY, MapSize, MapSize, 0.5f, 0.5f);
            _map.localScale = new Vector3(k, k, 1f);
            float plateH = meta * 1.5f;
            At(_placePlate, m.X, m.Bottom - plateH, m.W, plateH);
            _place.Size = meta;
            At(_placeSub.Rect, m.X - m.W * 0.25f, m.Bottom + 4f * u, m.W * 1.5f, meta * 1.3f);
            _placeSub.Size = meta;

            // Quête suivie : titre en or, étape, avancée et sa barre.
            UiBox q = l[HudPart.Quests];
            At(_quest, q);
            float qp = 8f * u * (l.Phone ? 1f : 1.5f);
            At(_questTitle.Rect, qp, qp * 0.5f, q.W - 2f * qp, essential * 1.4f);
            _questTitle.Size = essential;
            At(_questStep.Rect, qp, qp * 0.5f + essential * 1.4f, q.W * 0.75f - qp, q.H - essential * 1.4f - 2f * qp);
            _questStep.Size = meta;
            At(_questProgress.Rect, q.W * 0.7f, qp * 0.5f + essential * 1.4f, q.W * 0.3f - qp, meta * 1.4f);
            _questProgress.Size = meta;
            At(_questBar, qp, q.H - qp - 3f * u, q.W - 2f * qp, 3f * u);

            // Annonces : trois lignes au plus, la plus récente en bas.
            UiBox a = l[HudPart.Toasts];
            At(_messages, a);
            float lineH = a.H / MaxToasts;
            for (int i = 0; i < MaxToasts; i++)
            {
                At(_toasts[i].Rect, 0f, i * lineH, a.W, lineH);
                _toasts[i].Size = Mathf.Min(l.Phone ? meta : body * 0.8f, lineH * 0.8f);
            }

            // Aide : sous la vie, à la place du groupe ; agrandie sur téléphone (fenêtre à la demande, au-dessus du jeu).
            UiBox g = l[HudPart.Group];
            float helpScale = l.Phone ? 0.88f * u : 1f;
            _help.localScale = new Vector3(helpScale, helpScale, 1f);
            At(_help, g.X, Mathf.Min(g.Y, Mathf.Max(l.Safe.Y, l.Safe.Bottom - 318f * helpScale)), 400f, _help.sizeDelta.y);

            // Compétences : cases carrées aux places de la disposition.
            while (_shards.Count < 7) _shards.Add(BuildShard(_shards.Count));
            for (int i = 0; i < _shards.Count; i++)
            {
                Shard s = _shards[i];
                UiBox b = l.Skill(i);
                At(s.Node, b);
                float corner = Mathf.Max(meta, b.W * 0.24f);
                s.Timer.Size = b.W * 0.36f;
                s.Cost.Size = corner * 0.85f;
                At(s.Cost.Rect, b.W - 4f * u - b.W, 2f * u, b.W, corner);
                s.Key.Size = corner * 0.85f;
                At(s.Key.Rect, 4f * u, 2f * u, b.W, corner);
                s.ShownCost = -1;
                s.ShownTenths = -1;
            }
            _hint.Size = essential;
            _hint.Value = "";   // l'indication se replace à son prochain affichage
        }

        // --- Mise à jour ------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Étape 3 : annonce d'un niveau gagné (« Niveau 5 », « Effet à ta prochaine halte ») : rayons de lumière qui tournent, titre doré
        /// qui grossit puis s'efface en trois secondes.
        /// </summary>
        public void LevelUp(int level, string detail)
        {
            _levelUpAge = 0f;
            _levelUpTitle.Value = string.Format(UiTexts.Get(HudTextsV21.Level), level);
            _levelUpSub.Value = detail ?? "";
            UiKit.Show(_levelUp, true);
            UiSounds.Play(UiCue.LevelUp);   // essai réaliste 7 : fanfare
        }

        /// <summary>Début d'une image : les étiquettes, textes flottants, repères et annonces se reposent depuis zéro.</summary>
        public void Begin(float deltaTime)
        {
            UiKit.Show(_canvas, true);
            _time += deltaTime;
            _plateCount = 0;
            _floatCount = 0;
            _markerCount = 0;
            _toastCount = 0;
            _labelCount = 0;
        }

        /// <summary>Cache toute l'interface en jeu (menus, salle d'attente, écran des talents, erreurs).</summary>
        public void Hide() => UiKit.Show(_canvas, false);

        /// <summary>Clé de comparaison d'une liste d'altérations (sans allocation) : nombre et empreinte des identifiants.</summary>
        private static int StatesKey(IReadOnlyList<string> states)
        {
            int key = states.Count;
            for (int i = 0; i < states.Count; i++) key = key * 31 + (states[i]?.GetHashCode() ?? 0);
            return key;
        }

        private static string JoinStates(IReadOnlyList<string> states, string separator)
        {
            if (states.Count == 0) return "";
            var b = new System.Text.StringBuilder();
            for (int i = 0; i < states.Count; i++)
            {
                if (i > 0) b.Append(separator);
                b.Append(Labels.State(states[i]));
            }
            return b.ToString();
        }

        /// <summary>La vie, l'endurance, la Résonance, les Charges, les compétences, la garde et les altérations du joueur.</summary>
        public void Player(PlayerState state, float deltaTime)
        {
            CombatantView me = state.View;
            long health = me.Health ?? 0, maxHealth = System.Math.Max(1, me.MaxHealth ?? 1);
            float healthFraction = Mathf.Clamp01(health / (float)maxHealth);
            _healthFill.fillAmount = healthFraction;
            _enduranceFill.fillAmount = Mathf.Clamp01(me.Endurance / (float)System.Math.Max(1, me.MaxEndurance));
            _danger = me.IsDead || me.Health == null ? 0f : Mathf.Clamp01((0.3f - healthFraction) / 0.3f);
            if (health != _shownHealth || maxHealth != _shownMaxHealth)
            {
                _shownHealth = health;
                _shownMaxHealth = maxHealth;
                _healthText.Value = UiTexts.Get(HudTextsV21.Health) + "  " + health + " / " + maxHealth;
            }
            if (me.Endurance != _shownEndurance || me.MaxEndurance != _shownMaxEndurance)
            {
                _shownEndurance = me.Endurance;
                _shownMaxEndurance = me.MaxEndurance;
                string label = UiTexts.Get(_layout.Phone ? HudTextsV21.EnduranceShort : HudTextsV21.Endurance);
                _enduranceText.Value = label + "  " + me.Endurance + " / " + me.MaxEndurance;
            }
            _resonanceFill.fillAmount = Mathf.Clamp01(me.Resonance / (float)System.Math.Max(1, state.ResonanceMax));
            if (me.Resonance != _shownResonance)
            {
                _shownResonance = me.Resonance;
                _resonanceText.Value = me.Resonance.ToString();
            }

            // Charges de Rempart : losanges sous la case de la Résonance (essai réaliste 7 : seulement avec au moins une Charge).
            int total = state.RampartChargesMax;
            ChargesShown = total > 0 && me.RampartCharges > 0;
            while (_charges.Count < total) _charges.Add(UiKit.Image(_screen, "Charge " + (_charges.Count + 1), UiKit.Sprite("losange_off"), Color.white));
            if (me.RampartCharges != _shownCharges || !ChargesShown)
            {
                UiBox sig = _layout[HudPart.Signal];
                float u = _layout.Unit, w = 10f * u, step = 12f * u;
                for (int i = 0; i < _charges.Count; i++)
                {
                    bool used = ChargesShown && i < total;
                    UiKit.Show(_charges[i], used);
                    if (!used) continue;
                    At(_charges[i], sig.X + i * step, sig.Bottom + 3f * u, w, w * 1.5f);
                    bool lit = i < me.RampartCharges;
                    _charges[i].sprite = UiKit.Sprite(lit ? "losange_on" : "losange_off");
                    _charges[i].color = lit ? Color.white : UiKit.Alpha(Color.white, 0.45f);
                }
                if (_shownCharges >= 0 && me.RampartCharges > _shownCharges) UiSounds.Play(UiCue.ChargeGained);
                _shownCharges = me.RampartCharges;
            }

            // Compétences : les six places actives (touches 1 à 6) puis l'ultime (R), chacune à sa place fixe (une place vide laisse un trou).
            while (_shards.Count < 7) _shards.Add(BuildShard(_shards.Count));
            for (int i = 0; i < _shards.Count; i++) UpdateShard(_shards[i], i == 6, i, state, me, deltaTime);

            // Garde : levée, parade, brisée ; récupération de parade en dixièmes de seconde ; puis les altérations.
            int guard = Contains(me.States, "guardBreak") ? 1 : me.ParryOpen ? 2 : me.Guarding ? 3 : state.ParryRecoverySeconds > 0 ? 4 : 5;
            int tenths = guard == 4 ? (int)System.Math.Ceiling(state.ParryRecoverySeconds * 10) : 0;
            int key = (guard * 1000 + tenths) * 7919 + StatesKey(me.States);
            if (key != _effectsKey)
            {
                _effectsKey = key;
                string g;
                switch (guard)
                {
                    case 1: g = UiTexts.Get(HudTextsV21.GuardBroken); _effects.Color = UiKit.Danger; break;
                    case 2: g = UiTexts.Get(HudTextsV21.ParryOpen); _effects.Color = UiV21Art.BrightGold; break;
                    case 3: g = UiTexts.Get(HudTextsV21.GuardUp); _effects.Color = UiKit.Teal; break;
                    case 4: g = string.Format(UiTexts.Get(HudTextsV21.ParryIn), (tenths / 10f).ToString("0.0")); _effects.Color = UiV21Art.Stamina; break;
                    default: g = UiTexts.Get(HudTextsV21.GuardIdle); _effects.Color = UiKit.Alpha(UiV21Art.Parchment, 0.7f); break;
                }
                _effects.Value = me.States.Count > 0 ? g + "  ·  " + JoinStates(me.States, " · ") : g;
            }
        }

        private static bool Contains(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == value) return true;
            return false;
        }

        private void UpdateShard(Shard shard, bool ultimatePlace, int index, PlayerState state, CombatantView me, float deltaTime)
        {
            SkillSlot slot = null;
            for (int i = 0; i < state.Slots.Count && slot == null; i++)
            {
                SkillSlot s = state.Slots[i];
                if (ultimatePlace ? s.IsUltimate : !s.IsUltimate && s.Slot == index) slot = s;
            }
            UiKit.Show(shard.Node, slot != null);
            if (slot == null) return;
            if (shard.SkillId != slot.SkillId)
            {
                shard.SkillId = slot.SkillId;
                shard.Icon.sprite = UiKit.Icon(IconName(slot.SkillId));
                shard.Icon.color = shard.Icon.sprite != null ? Color.white : new Color(0.25f, 0.3f, 0.4f);
                shard.LastRemaining = slot.RemainingSeconds;
                shard.Key.Value = slot.IsUltimate ? UiTexts.Get(HudTextsV21.UltimateKey) : (slot.Slot + 1).ToString();
            }
            if (slot.EnduranceCost != shard.ShownCost)
            {
                shard.ShownCost = slot.EnduranceCost;
                shard.Cost.Value = slot.EnduranceCost > 0 ? slot.EnduranceCost.ToString() : "";
            }
            bool cooling = slot.RemainingSeconds > 0.05 && slot.CooldownSeconds > 0;
            shard.Cooldown.fillAmount = cooling ? Mathf.Clamp01((float)(slot.RemainingSeconds / slot.CooldownSeconds)) : 0f;
            // Recharge : dixièmes sous 10 s, secondes au-dessus ; le texte n'est refait que quand ce qu'il montre change.
            int shown = !cooling ? -1 : slot.RemainingSeconds < 10 ? (int)System.Math.Ceiling(slot.RemainingSeconds * 10) : 1000 + (int)System.Math.Ceiling(slot.RemainingSeconds);
            if (shown != shard.ShownTenths)
            {
                shard.ShownTenths = shown;
                shard.Timer.Value = shown < 0 ? "" : shown < 1000 ? (shown / 10f).ToString("0.0") : (shown - 1000).ToString();
            }
            bool affordable = me.Endurance >= slot.EnduranceCost;
            bool ready = slot.Unavailable == null;
            shard.Dim.color = !ready && !cooling ? new Color(0.02f, 0.03f, 0.05f, 0.55f) : !affordable ? new Color(0.35f, 0f, 0.05f, 0.45f) : new Color(0f, 0f, 0f, 0f);
            shard.Cost.Color = affordable ? UiKit.Dawn : UiKit.Danger;
            // Lancée : la recharge repart (éclair bref) ; revenue : un liseré d'or passe (sans halo permanent, charte page 1).
            if (slot.RemainingSeconds > shard.LastRemaining + 0.2) shard.FlashAlpha = 0.9f;
            if (shard.LastRemaining > 0.05 && slot.RemainingSeconds <= 0.05)
            {
                shard.ReadyAlpha = 0.6f;
                UiSounds.Play(UiCue.SkillReady);
            }
            shard.LastRemaining = slot.RemainingSeconds;
            shard.FlashAlpha = Mathf.Max(0f, shard.FlashAlpha - deltaTime * 2.6f);
            shard.ReadyAlpha = Mathf.Max(0f, shard.ReadyAlpha - deltaTime * 2f);
            shard.Flash.color = UiKit.Alpha(Color.white, shard.FlashAlpha);
            shard.Ready.color = UiKit.Alpha(UiKit.Dawn, shard.ReadyAlpha);
        }

        /// <summary>Icône peinte d'une compétence d'après la fin de son identifiant (« skill.bastion.frappe_du_rempart »).</summary>
        public static string IconName(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return null;
            int dot = skillId.LastIndexOf('.');
            return dot >= 0 ? skillId.Substring(dot + 1) : skillId;
        }

        /// <summary>La cible en haut de l'écran (null : rien).</summary>
        public void Target(OtherState target, float deltaTime)
        {
            UiKit.Show(_target, target != null);
            if (target == null)
            {
                _targetId = null;
                return;
            }
            CombatantView v = target.View;
            float fraction = 1f;
            long h = -1, max = -1;
            if (v.Health is long hh && v.MaxHealth is long mm && mm > 0)
            {
                h = hh;
                max = mm;
                fraction = Mathf.Clamp01(h / (float)max);
            }
            if (_targetId != target.Id)
            {
                _targetId = target.Id;
                _targetName.Value = DisplayNames.Of(target.Id);
                _chunk = fraction;
                _lastTargetFraction = -1f;
                _shownTargetHealth = _shownTargetMax = -2;
                _chipKey = _targetStatesKey = int.MinValue;
            }
            if (h != _shownTargetHealth || max != _shownTargetMax)
            {
                _shownTargetHealth = h;
                _shownTargetMax = max;
                _targetHealth.Value = max > 0 ? h + " / " + max : UiTexts.Get(HudTextsV21.Infinite);
            }
            // La part perdue reste claire un instant, puis rejoint la barre.
            if (fraction < _chunk - 0.001f)
            {
                _chunkHold += deltaTime;
                if (_chunkHold > 0.4f) _chunk = Mathf.MoveTowards(_chunk, fraction, deltaTime * 0.8f);
            }
            else
            {
                _chunk = fraction;
                _chunkHold = 0f;
            }
            _targetFill.fillAmount = fraction;
            _targetChunk.fillAmount = _chunk;
            if (_lastTargetFraction >= 0f && fraction < _lastTargetFraction - 0.0005f) _targetFlashAlpha = 0.65f;
            _lastTargetFraction = fraction;
            bool announced = v.NextAttack != null && target.NextAttackSeconds is double && !v.IsDead;
            UiKit.Show(_chip, announced);
            if (announced)
            {
                double next = target.NextAttackSeconds ?? 0;
                bool unblockable = v.NextAttack.Unblockable;
                int key = (int)System.Math.Ceiling(next * 10) * 2 + (unblockable ? 1 : 0) + (v.NextAttack.Id?.GetHashCode() ?? 0) * 4099;
                if (key != _chipKey)
                {
                    _chipKey = key;
                    string seconds = string.Format(UiTexts.Get(HudTextsV21.Seconds), (System.Math.Ceiling(next * 10) / 10).ToString("0.0"));
                    _chipText.Value = DisplayNames.Attack(v.NextAttack.Id) + " · " + seconds + (unblockable ? " · " + UiTexts.Get(HudTextsV21.Unblockable) : "");
                    _chipText.Color = unblockable ? new Color(1f, 0.55f, 0.5f) : UiV21Art.BrightGold;
                }
                float urgency = Mathf.Clamp01(1f - (float)next / 2f);
                float pulse = 1f + 0.05f * urgency * Mathf.Sin(_time * 12f);
                _chip.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            }
            int statesKey = StatesKey(v.States);
            if (statesKey != _targetStatesKey)
            {
                _targetStatesKey = statesKey;
                _targetStates.Value = JoinStates(v.States, ", ");
            }
        }

        /// <summary>Étiquette au-dessus d'un combattant (position à l'écran en pixels réels, z négatif : derrière la caméra).</summary>
        public void Overhead(Vector3 screen, OtherState other, bool isTarget)
        {
            if (screen.z <= 0f) return;
            if (_plateCount == _platePool.Count) _platePool.Add(BuildPlate());
            Plate plate = _platePool[_plateCount++];
            UiKit.Show(plate.Node, true);
            float k = UiKit.ReferenceHeight / Mathf.Max(1, Screen.height);
            At(plate.Node, screen.x * k, (Screen.height - screen.y) * k, 180f, 60f, 0.5f, 1f);
            plate.Node.localScale = new Vector3(_worldScale, _worldScale, 1f);
            // Nom refait seulement quand l'étiquette change de combattant ou de rôle (cible ou non) : rien n'est alloué à chaque image.
            if (plate.Id != other.Id || plate.IsTarget != isTarget)
            {
                plate.Id = other.Id;
                plate.IsTarget = isTarget;
                string name = DisplayNames.Of(other.Id);
                plate.Name.Value = isTarget ? "» " + name : name;
                plate.Name.Color = isTarget ? UiKit.Dawn : UiKit.Ink;
            }
            CombatantView v = other.View;
            plate.Fill.fillAmount = v.Health is long h && v.MaxHealth is long max && max > 0 ? Mathf.Clamp01(h / (float)max) : 1f;
            bool telegraph = v.NextAttack != null && other.NextAttackSeconds is double && !v.IsDead;
            UiKit.Show(plate.Telegraph, telegraph);
            if (telegraph)
            {
                plate.Telegraph.fillAmount = 1f - Mathf.Clamp01((float)(other.NextAttackSeconds ?? 0) / 2f);
                plate.Telegraph.color = v.NextAttack.Unblockable ? UiKit.Danger : UiKit.Endurance;
            }
        }

        private Plate BuildPlate()
        {
            var plate = new Plate { Node = UiKit.Node(_plates, "Étiquette") };
            plate.Name = UiText.Create(plate.Node, "Nom", UiFace.Bold, 18f, UiKit.Ink, TextAnchor.MiddleCenter, 8f, true);
            At(plate.Name.Rect, 90f, 0f, 220f, 22f, 0.5f, 0f);
            At(UiKit.Image(plate.Node, "Rail", UiV21Art.Track, Color.white, Image.Type.Sliced), 90f, 24f, 124f, 12f, 0.5f, 0f);
            plate.Fill = Filled(plate.Node, "Vie", UiV21Art.Blood, Image.FillMethod.Horizontal);
            At(plate.Fill, 90f, 26f, 120f, 8f, 0.5f, 0f);
            plate.Telegraph = Filled(plate.Node, "Coup annoncé", UiKit.Endurance, Image.FillMethod.Horizontal);
            At(plate.Telegraph, 90f, 40f, 120f, 5f, 0.5f, 0f);
            return plate;
        }

        /// <summary>Texte flottant (retour de combat : dégâts, critiques, soins) : position à l'écran, couleur, âge de 0 (naissance) à 1 (effacé).</summary>
        public void Floating(Vector3 screen, string text, Color color, float age)
        {
            if (screen.z <= 0f) return;
            if (_floatCount == _floatPool.Count)
            {
                UiText t = UiText.Create(_floats, "Retour", UiFace.Title, 30f, Color.white, TextAnchor.MiddleCenter, 2f);
                t.Outline(new Color(0.02f, 0.03f, 0.04f), 0.15f);
                _floatPool.Add(t);
            }
            UiText f = _floatPool[_floatCount++];
            f.Show(true);
            float k = UiKit.ReferenceHeight / Mathf.Max(1, Screen.height);
            At(f.Rect, screen.x * k, (Screen.height - screen.y) * k, 360f, 40f, 0.5f, 0.5f);
            f.Value = text;
            f.Color = UiKit.Alpha(color, Mathf.Clamp01(1f - age));
            float pop = (age < 0.12f ? 1f + 1.6f * (0.12f - age) : 1f) * _worldScale;
            f.Rect.localScale = new Vector3(pop, pop, 1f);
        }

        /// <summary>Sous-lot V2.12.6 : taille de police et hauteur (unités de l'interface) d'une étiquette de repère.</summary>
        public const float LabelFont = 19f, LabelHeight = 26f;

        /// <summary>Sous-lot V2.12.6 : unités de l'interface par pixel de l'écran.</summary>
        public static float LabelScale => UiKit.ReferenceHeight / Mathf.Max(1, Screen.height);

        /// <summary>Nom d'un PNJ, d'un point sûr ou d'un nœud de récolte au-dessus de lui (position à l'écran en pixels réels).</summary>
        public void Label(Vector3 screen, string text, Color color)
        {
            if (screen.z <= 0f) return;
            if (_labelCount == _labelPool.Count)
            {
                UiText t = UiText.Create(_plates, "Nom", UiFace.Bold, LabelFont, Color.white, TextAnchor.MiddleCenter, 8f, true);
                t.Outline(new Color(0.02f, 0.03f, 0.04f, 0.9f), 0.18f);
                _labelPool.Add(t);
            }
            UiText label = _labelPool[_labelCount++];
            label.Show(true);
            float k = UiKit.ReferenceHeight / Mathf.Max(1, Screen.height);
            At(label.Rect, screen.x * k, (Screen.height - screen.y) * k - 16f, 400f, LabelHeight, 0.5f, 0.5f);
            label.Value = text;
            label.Color = color;
        }

        /// <summary>
        /// État du monde (aventure : événement en cours, cicatrice du Seuil) sous la boussole, ou minuteur de l'entraînement à la place de la
        /// boussole ; null : caché.
        /// </summary>
        public void Clock(string text)
        {
            _clock.Show(!string.IsNullOrEmpty(text));
            _clock.Value = text;
        }

        private void PlaceClock()
        {
            UiBox c = _layout[HudPart.Compass];
            float h = _layout.Font(HudFont.Essential) * 1.4f;
            if (_compassShown) At(_clock.Rect, c.X - c.W * 0.25f, c.Bottom + 2f * _layout.Unit, c.W * 1.5f, h);
            else At(_clock.Rect, c.X - c.W * 0.25f, c.Y, c.W * 1.5f, h);
        }

        /// <summary>Annonce (butin, quête, refus…) sous la boussole ; trois au plus, les plus récentes, la dernière en bas.</summary>
        public void Message(string text)
        {
            if (_toastCount < MaxToasts) _toastValues[_toastCount++] = text;
            else
            {
                for (int i = 1; i < MaxToasts; i++) _toastValues[i - 1] = _toastValues[i];
                _toastValues[MaxToasts - 1] = text;
            }
        }

        /// <summary>
        /// Aventure : la carte peinte centrée sur le joueur (nord en haut), ses repères, le lieu, la zone et l'heure, la boussole, la quête
        /// suivie. <paramref name="shown"/> faux : un écran de l'aventure ou un dialogue est ouvert (carte, boussole et quête se cachent).
        /// <paramref name="drawerOpen"/> : le tiroir « Plus » des commandes au doigt couvre la quête.
        /// </summary>
        public void Adventure(AdventureSession a, IReadOnlyList<MapEnemy> enemies, double facingYaw, bool shown, bool drawerOpen = false)
        {
            bool on = a != null && shown;
            UiKit.Show(_map, on);
            UiKit.Show(_placePlate, on);
            _placeSub.Show(on && !_layout.Phone);
            Compass(on, facingYaw);
            bool quest = on && !drawerOpen;
            if (!quest) UiKit.Show(_quest, false);
            if (!on) return;
            AdventureMap map = a.Map;
            Texture2D painted = PaintedMap.For(map);
            // Centre de la carte à chaque image sans liste ; les repères (liste construite par la session) sont relus 4 fois par seconde.
            if (!a.TryPlayerPosition(out double cx, out double cz))
            {
                cx = (map.MinX + map.MaxX) / 2;
                cz = (map.MinZ + map.MaxZ) / 2;
            }
            if (_mapMarkers == null || !ReferenceEquals(_markersSession, a) || _time >= _markersAt)
            {
                _mapMarkers = a.MapMarkers();
                _markersSession = a;
                _markersAt = _time + MarkersPeriod;
            }
            _mapImage.texture = painted;
            _mapImage.color = painted != null ? Color.white : UiKit.Night;
            _mapImage.uvRect = PaintedMap.Window(map, cx, cz, MapRadius);
            _you.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -(float)facingYaw);
            for (int i = 0; i < enemies.Count; i++)
            {
                MapEnemy e = enemies[i];
                Marker("repere_ennemi", e.X - cx, e.Z - cz, e.Boss ? 16f : 11f, e.Boss ? new Color(0.78f, 0.45f, 1f) : Color.white, false);
            }
            for (int i = 0; i < _mapMarkers.Count; i++)
            {
                MapMarker m = _mapMarkers[i];
                switch (m.Kind)
                {
                    case MapMarkerKind.Npc: Marker("repere_rond", m.X - cx, m.Z - cz, 11f, Color.white, false); break;
                    case MapMarkerKind.SafePoint: Marker("repere_rond", m.X - cx, m.Z - cz, 13f, UiKit.Teal, false); break;
                    case MapMarkerKind.Workshop: Marker("repere_rond", m.X - cx, m.Z - cz, 12f, UiKit.Dawn, false); break;
                    case MapMarkerKind.Node: Marker("repere_rond", m.X - cx, m.Z - cz, 7f, new Color(0.65f, 0.95f, 0.55f), false); break;
                    case MapMarkerKind.Door: Marker("repere_rond", m.X - cx, m.Z - cz, 10f, new Color(0.95f, 0.66f, 0.28f), false); break;   // intérieurs
                    case MapMarkerKind.Objective: Marker("repere_objectif", m.X - cx, m.Z - cz, 22f, Color.white, true); break;
                }
            }
            for (int i = _markerCount; i < _markerPool.Count; i++) UiKit.Show(_markerPool[i], false);
            _you.rectTransform.SetAsLastSibling();
            _place.Value = a.PlaceLabel;   // sous-lot V1.8 : niveau des créatures du lieu (zones en mélange)
            // Zone, heure, fête (V2.10) et grand jour de marché (V2.14) : texte refait seulement quand l'un d'eux change.
            string zone = a.ZoneName, time = a.TimeOfDay, festival = a.FestivalText, market = a.MarketLine;
            if (zone != _subZone || time != _subTime || festival != _subFestival || market != _subMarket)
            {
                _subZone = zone;
                _subTime = time;
                _subFestival = festival;
                _subMarket = market;
                _placeSub.Value = zone + (festival != null ? " · " + festival : "") + (market != null ? " · " + market : "");
                _compassSub.Value = time;
            }

            // Sous-lot V1.6 : la quête suivie (choisie dans le journal), sinon la première principale en cours.
            JournalEntry followed = quest ? a.Followed : null;
            UiKit.Show(_quest, followed != null);
            if (followed != null)
            {
                _questTitle.Value = followed.Name;
                _questStep.Value = followed.Step ?? "";
                if (followed.Progress != _shownProgress || _shownProgress == null)
                {
                    // Avancée « 1 / 3 » relue seulement quand elle change (Progress découpe le texte).
                    _shownProgress = followed.Progress;
                    _questProgress.Value = followed.Progress ?? "";
                    _questBar.fillAmount = Progress(followed.Progress);
                    UiKit.Show(_questBar, followed.Progress != null);
                }
            }
        }

        /// <summary>Boussole (aventure) : les huit directions glissent sur la bande selon l'orientation du personnage (0° : nord, sens horaire).</summary>
        private void Compass(bool shown, double facingYaw)
        {
            UiKit.Show(_compass, shown);
            if (shown != _compassShown)
            {
                _compassShown = shown;
                PlaceClock();
            }
            if (!shown) return;
            UiBox c = _layout[HudPart.Compass];
            float half = c.W / 2f, reach = half - 2f * _layout.Font(HudFont.Essential);
            for (int i = 0; i < _marks.Length; i++)
            {
                float delta = Angle(i * 45f - (float)facingYaw);
                bool visible = Mathf.Abs(delta) <= 80f;
                _marks[i].Show(visible);
                if (visible) _marks[i].Rect.anchoredPosition = new Vector2(half + delta / 80f * reach, 0f);
            }
        }

        /// <summary>Angle ramené entre -180 et 180 degrés.</summary>
        private static float Angle(float degrees)
        {
            float a = degrees % 360f;
            if (a > 180f) a -= 360f;
            else if (a < -180f) a += 360f;
            return a;
        }

        /// <summary>Avancée « 1/3 » ou « 1 / 3 » en fraction (0 si illisible).</summary>
        public static float Progress(string progress)
        {
            if (string.IsNullOrEmpty(progress)) return 0f;
            string[] parts = progress.Replace(" ", "").Split('/');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int done) || !int.TryParse(parts[1], out int total) || total <= 0) return 0f;
            return Mathf.Clamp01(done / (float)total);
        }

        private void Marker(string sprite, double dx, double dz, float size, Color color, bool clampToEdge)
        {
            float px = (float)(dx / MapRadius) * (MapSize / 2f), py = (float)(dz / MapRadius) * (MapSize / 2f);
            float r = Mathf.Sqrt(px * px + py * py), limit = MapSize / 2f - 18f;
            if (r > limit)
            {
                if (!clampToEdge) return;
                px *= limit / r;
                py *= limit / r;
            }
            if (_markerCount == _markerPool.Count) _markerPool.Add(UiKit.Image(_markers, "Repère", null, Color.white));
            Image marker = _markerPool[_markerCount++];
            UiKit.Show(marker, true);
            marker.sprite = UiKit.Sprite(sprite);
            marker.color = color;
            At(marker, MapSize / 2f + px, MapSize / 2f - py, size, size, 0.5f, 0.5f);
        }

        /// <summary>
        /// L'aide des touches (H) : titre (mode et lieu ou scénario), contexte (build, caméra) et une ligne par touche (<see cref="HelpKeys"/>).
        /// </summary>
        public void Help(bool shown, string title, string context, IReadOnlyList<HelpKey> rows)
        {
            UiKit.Show(_help, shown);
            if (!shown) return;
            _helpTitle.Value = title;
            _helpContext.Value = context;
            if (ReferenceEquals(rows, _helpRows)) return;
            _helpRows = rows;
            while (_helpPlates.Count < rows.Count) HelpRow(_helpPlates.Count);
            for (int i = 0; i < _helpPlates.Count; i++)
            {
                bool used = i < rows.Count;
                UiKit.Show(_helpPlates[i], used);
                _helpLabels[i].Show(used);
                if (!used) continue;
                _helpKeys[i].Value = rows[i].Key;
                _helpLabels[i].Value = rows[i].Action;
                _helpKeys[i].Size = rows[i].Key.Length > 7 ? 12f : 14f;
            }
            _help.sizeDelta = new Vector2(400f, 92f + 30f * ((rows.Count + 1) / 2));
        }

        /// <summary>
        /// Textes du centre : bandeau (« Pause »), annonce dans un panneau (défaite, fin de la session), indication d'action en bas
        /// (« F : parler à Maëlle »), à sa place de la maquette. Null : rien.
        /// </summary>
        public void Center(string banner, string notice, string hint)
        {
            _banner.Show(!string.IsNullOrEmpty(banner));
            _banner.Value = banner;
            UiKit.Show(_notice, !string.IsNullOrEmpty(notice));
            _noticeText.Value = notice;
            UiKit.Show(_hintPlate, !string.IsNullOrEmpty(hint));
            if (string.IsNullOrEmpty(hint) || hint == _hint.Value) return;
            _hint.Value = hint;
            UiBox p = _layout[HudPart.Prompt];
            float w = Mathf.Clamp(_layout.Font(HudFont.Essential) * 0.5f * hint.Length + 24f * _layout.Unit, p.W * 0.6f, p.W);
            At(_hintPlate, p.CenterX - w / 2f, p.Y, w, p.H);
        }

        /// <summary>Fin d'une image : ce qui n'a pas servi se cache, les annonces s'écrivent, les effets avancent.</summary>
        public void End(float deltaTime)
        {
            for (int i = _plateCount; i < _platePool.Count; i++) UiKit.Show(_platePool[i].Node, false);
            for (int i = _floatCount; i < _floatPool.Count; i++) _floatPool[i].Show(false);
            for (int i = _labelCount; i < _labelPool.Count; i++) _labelPool[i].Show(false);
            for (int i = 0; i < MaxToasts; i++)
            {
                // Les lignes du bas d'abord : la plus récente reste toujours sur la dernière ligne.
                int source = i - (MaxToasts - _toastCount);
                bool used = source >= 0;
                _toasts[i].Show(used);
                if (used) _toasts[i].Value = _toastValues[source];
            }
            Animate(deltaTime);
        }

        /// <summary>Effets : coup reçu par la cible, niveau gagné, vie basse.</summary>
        private void Animate(float deltaTime)
        {
            _targetFlashAlpha = Mathf.Max(0f, _targetFlashAlpha - deltaTime * 3f);
            _targetFlash.color = UiKit.Alpha(Color.white, _targetFlashAlpha);
            float beat = 0.5f + 0.5f * Mathf.Sin(_time * (4f + 4f * _danger));
            float alpha = _danger * (0.35f + 0.45f * beat);
            _dangerTop.color = UiKit.Alpha(UiKit.Health, alpha * 0.7f);
            _dangerBottom.color = UiKit.Alpha(UiKit.Health, alpha);
            if (_levelUpAge < 3.2f)
            {
                _levelUpAge += deltaTime;
                float t = _levelUpAge;
                float appear = Mathf.Clamp01(t / 0.35f), fade = t > 2.4f ? Mathf.Clamp01(1f - (t - 2.4f) / 0.8f) : 1f;
                float pop = 0.6f + 0.4f * appear + (t < 0.5f ? 0.15f * Mathf.Sin(t / 0.5f * Mathf.PI) : 0f);
                _levelUp.localScale = new Vector3(pop, pop, 1f);
                _levelUpTitle.Color = UiKit.Alpha(Color.white, appear * fade);
                _levelUpSub.Color = UiKit.Alpha(UiKit.Teal, appear * fade);
                for (int i = 0; i < _levelUpRays.Count; i++)
                {
                    _levelUpRays[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * 36f + t * 25f);
                    _levelUpRays[i].color = UiKit.Alpha(i % 2 == 0 ? UiKit.Dawn : UiKit.Rose, 0.35f * appear * fade);
                }
                if (_levelUpAge >= 3.2f) UiKit.Show(_levelUp, false);
            }
        }
    }
}
