using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.Simulation.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace EdS.Game
{
    /// <summary>
    /// Essai réaliste 6 : l'interface en jeu « Fragments du Seuil » (identité créée pour le jeu, choisie et retouchée par Damien le
    /// 27 octobre 2026 : « le reste est bien »), en uGUI. L'interface est faite des Éclats du Seuil : en bas, le sceau fendu du Bastion
    /// (la vie à gauche, l'endurance à droite, la Résonance qui brille dans la fissure, les Charges en éclats au-dessus) et ses
    /// compétences en éclats flottants (icônes peintes, recharge, coût, touche) ; en haut, la cible dans une fissure ; à droite, la carte
    /// peinte dans un éclat, le lieu, l'heure et la quête ; les noms et les barres au-dessus des combattants ; l'aide en dalle brisée.
    /// Animé : reflets qui passent, liquide qui ondule, braises, éclats de lumière, éclair au lancement et quand une compétence revient.
    /// Rien ne revient vers le moteur : l'interface montre l'état que le cœur donne.
    /// </summary>
    public sealed class HudView
    {
        /// <summary>
        /// Taille du sceau (dessin de 290 × 320). Essai réaliste 9 (Damien, 29 octobre 2026 : « la case où il y a les points de vie et
        /// l'endurance est trop grande, elle cache le perso ») : réduit d'environ moitié (0,78 avant), collé au bas de l'écran.
        /// </summary>
        public const float CrestScale = 0.42f;

        /// <summary>Haut du sceau dans le groupe du bas (320 de haut, mesuré depuis le haut du groupe) : les Charges sont juste au-dessus.</summary>
        private const float CrestTop = 322f - 320f * CrestScale;
        private const float MapSize = 290f;
        private const float MapRadius = 20f;   // mètres montrés de chaque côté du joueur sur la petite carte
        private const float CompactMapScale = 0.8f;

        /// <summary>
        /// Rectangle de la petite carte dans l'écran de 1080 de haut (origine en haut à gauche, comme IMGUI) : les commandes tactiles en font
        /// un bouton (l'agrandir) et rangent leur rangée du haut dessous.
        /// </summary>
        public static Rect MiniMapRect(float screenWidth, bool compact)
        {
            float k = compact ? CompactMapScale : 1f;
            return new Rect(screenWidth - (340f - 26f) * k, 20f * k, MapSize * k, MapSize * k);
        }

        private sealed class Shard
        {
            public RectTransform Node;
            public Image Icon, Cooldown, Flash, Ready, Dim, Shine;
            public UiText Key, Cost, Timer;
            public string SkillId;
            public double LastRemaining;
            public float FlashAlpha, ReadyAlpha, Phase;
        }

        private sealed class Plate
        {
            public RectTransform Node;
            public UiText Name;
            public Image Fill, Telegraph;
        }

        private sealed class Ember
        {
            public Image Image;
            public float Phase, X, Drift;
        }

        private readonly Canvas _canvas;
        private readonly RectTransform _bottom, _top, _right, _left, _plates, _floats, _messages, _center;

        // Bandeau (minuteur, pause), annonce au centre (défaite), indication d'action.
        private UiText _banner, _noticeText, _hint;
        private RectTransform _notice;
        private Image _noticeSlab, _hintPlate;

        // Sceau, Charges, compétences, garde.
        private RectTransform _crest, _healthLiquid, _enduranceLiquid;
        private RawImage _healthWave, _enduranceWave;
        private Image _crack, _healthShine, _enduranceShine;
        private UiText _healthValue, _healthMax, _enduranceValue, _enduranceMax, _resonance, _guard, _states;
        private readonly List<Image> _charges = new List<Image>(), _chargeGlows = new List<Image>();
        private readonly List<float> _chargePop = new List<float>();

        /// <summary>Essai réaliste 7 : les Charges de Rempart sont montrées (au moins une détenue).</summary>
        public bool ChargesShown { get; private set; }
        private int _shownCharges = -1;
        private readonly List<Shard> _shards = new List<Shard>();
        private readonly List<Ember> _embers = new List<Ember>();
        private readonly List<Image> _sparks = new List<Image>();

        // Cible.
        private Image _targetFill, _targetChunk, _chip;
        private UiText _targetName, _targetHealth, _chipText, _targetStates;
        private float _chunk = 1f, _chunkHold;
        private string _targetId;

        // Carte, lieu, quête.
        private RectTransform _map, _markers, _quest;
        private RawImage _mapImage;
        private Image _you;
        private UiText _place, _placeSub, _mapHint, _questTitle, _questStep, _questProgress;
        private Image _questBar;
        private readonly List<Image> _markerPool = new List<Image>();
        private int _markerCount;

        // Aide, étiquettes, textes flottants, messages.
        private RectTransform _help;
        private UiText _helpTitle, _helpContext;
        private IReadOnlyList<HelpKey> _helpRows;
        private readonly List<UiText> _helpKeys = new List<UiText>(), _helpLabels = new List<UiText>();
        private readonly List<Image> _helpPlates = new List<Image>();
        private readonly List<Plate> _platePool = new List<Plate>();
        private int _plateCount;
        private readonly List<UiText> _floatPool = new List<UiText>();
        private int _floatCount;
        private readonly List<UiText> _messagePool = new List<UiText>();
        private int _messageCount;
        private readonly List<UiText> _labelPool = new List<UiText>();
        private int _labelCount;
        private UiText _clock;

        private float _time;
        private bool _compact;

        // Essai réaliste 6, étape 3 : finitions animées (niveau gagné, vie basse, coup reçu par la cible).
        private RectTransform _levelUp;
        private UiText _levelUpTitle, _levelUpSub;
        private readonly List<Image> _levelUpRays = new List<Image>();
        private float _levelUpAge = 99f;
        private Image _dangerTop, _dangerBottom;
        private float _danger;
        private Image _targetFlash;
        private float _targetFlashAlpha;
        private float _lastTargetFraction = -1f;

        private HudView()
        {
            _canvas = UiKit.CreateCanvas("Interface (Fragments du Seuil)", 10);
            Transform root = _canvas.transform;
            _plates = UiKit.Fill(UiKit.Node(root, "Étiquettes"));
            _floats = UiKit.Fill(UiKit.Node(root, "Textes flottants"));
            _bottom = Anchored(root, "Bas", new Vector2(0.5f, 0f), new Vector2(1000f, 320f));
            _top = Anchored(root, "Haut", new Vector2(0.5f, 1f), new Vector2(760f, 170f));
            _right = Anchored(root, "Droite", new Vector2(1f, 1f), new Vector2(340f, 560f));
            _left = Anchored(root, "Gauche", new Vector2(0f, 1f), new Vector2(430f, 360f));
            _messages = Anchored(root, "Messages", new Vector2(0.5f, 1f), new Vector2(900f, 200f));
            _messages.anchoredPosition = new Vector2(0f, -176f);
            _center = Anchored(root, "Centre", new Vector2(0.5f, 1f), new Vector2(900f, 1080f));
            BuildBottom();
            BuildTop();
            BuildRight();
            BuildHelp();
            BuildCenter();
            BuildSettings();
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

        // --- Construction ----------------------------------------------------------------------------------------------------------

        private void BuildBottom()
        {
            // Essai réaliste 9 : voile plus bas, comme le sceau.
            At(UiKit.Image(_bottom, "Voile", UiKit.Sprite("voile_bas"), Color.white), 500f, 320f, 1000f, 210f, 0.5f, 1f);

            // Sceau : dessin de 290 × 320 (corps de 210 × 240 à 40 du bord), réduit pour laisser voir le chevalier en entier.
            _crest = At(UiKit.Node(_bottom, "Sceau"), 500f, 322f, 290f, 320f, 0.5f, 1f);
            _crest.localScale = new Vector3(CrestScale, CrestScale, 1f);
            UiKit.Fill(UiKit.Image(_crest, "Fond", UiKit.Sprite("sceau_fond"), Color.white).rectTransform);
            _healthLiquid = Liquid("Vie", "sceau_masque_gauche", UiKit.Health, out _healthWave, out _healthShine);
            _enduranceLiquid = Liquid("Endurance", "sceau_masque_droite", UiKit.Endurance, out _enduranceWave, out _enduranceShine);
            _crack = UiKit.Image(_crest, "Fissure (Résonance)", UiKit.Sprite("sceau_fissure"), Color.white, Image.Type.Filled);
            UiKit.Fill(_crack.rectTransform);
            _crack.fillMethod = Image.FillMethod.Vertical;
            _crack.fillOrigin = (int)Image.OriginVertical.Bottom;
            UiKit.Fill(UiKit.Image(_crest, "Contour", UiKit.Sprite("sceau_contour"), Color.white).rectTransform);
            // Essai réaliste 9 : chiffres dessinés plus gros pour rester lisibles dans le sceau réduit.
            // Relecture : 54 et 28, centres écartés, pour que « 2100 » et « 115 » ne se touchent pas de part et d'autre de la fissure.
            _healthValue = UiText.Create(_crest, "Vie", UiFace.Number, 54f, UiKit.Ink, TextAnchor.MiddleCenter);
            At(_healthValue.Rect, 88f, 152f, 130f, 64f, 0.5f, 0.5f);
            _healthMax = UiText.Create(_crest, "Vie maximale", UiFace.Body, 28f, UiKit.Alpha(UiKit.Ink, 0.8f), TextAnchor.MiddleCenter);
            At(_healthMax.Rect, 88f, 198f, 130f, 36f, 0.5f, 0.5f);
            _enduranceValue = UiText.Create(_crest, "Endurance", UiFace.Number, 54f, UiKit.Ink, TextAnchor.MiddleCenter);
            At(_enduranceValue.Rect, 202f, 152f, 130f, 64f, 0.5f, 0.5f);
            _enduranceMax = UiText.Create(_crest, "Endurance maximale", UiFace.Body, 28f, UiKit.Alpha(UiKit.Ink, 0.8f), TextAnchor.MiddleCenter);
            At(_enduranceMax.Rect, 202f, 198f, 130f, 36f, 0.5f, 0.5f);
            for (int i = 0; i < 16; i++)
            {
                Image dot = UiKit.Image(_crest, "Braise", UiKit.Sprite("braise"), UiKit.Dawn);
                At(dot, 145f, 60f, 7f, 7f, 0.5f, 0.5f);
                _embers.Add(new Ember { Image = dot, Phase = i / 16f + 0.37f * (i % 3), X = 70f + (i * 37) % 150, Drift = ((i * 53) % 80) - 40f });
            }
            Spark(_crest, 62f, 70f, 26f, 0f);
            Spark(_crest, 236f, 96f, 18f, 1.1f);
            Spark(_crest, 145f, 36f, 30f, 0.5f);

            _resonance = UiText.Create(_bottom, "Résonance", UiFace.Bold, 14f, UiKit.Dawn, TextAnchor.MiddleCenter, 18f, true);
            At(_resonance.Rect, 500f, 313f, 220f, 20f, 0.5f, 0.5f);   // essai réaliste 9 : sous le sceau réduit
            _resonance.Gradient(UiKit.Dawn, UiKit.Teal);
            _states = UiText.Create(_bottom, "Altérations", UiFace.Body, 18f, UiKit.Dawn, TextAnchor.MiddleCenter);
            At(_states.Rect, 500f, CrestTop - 60f, 900f, 26f, 0.5f, 0.5f);   // au-dessus des Charges
            _guard = UiText.Create(_bottom, "Garde", UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 18f, true);
            At(_guard.Rect, 96f, 288f, 200f, 22f, 0f, 0.5f);
        }

        /// <summary>Une moitié du sceau : masque à sa forme, liquide teinté, vague qui glisse à sa surface, reflet qui passe.</summary>
        private RectTransform Liquid(string name, string mask, Color color, out RawImage wave, out Image shine)
        {
            RectTransform shape = UiKit.Fill(UiKit.Mask(_crest, name, UiKit.Sprite(mask)));
            Image liquid = UiKit.Image(shape, "Liquide", UiKit.Sprite("liquide"), color);
            RectTransform rect = liquid.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 40f);
            rect.sizeDelta = new Vector2(0f, 240f);
            Texture2D waveTexture = UiKit.Texture("EdsInterface/Hud/vague");
            if (waveTexture != null) waveTexture.wrapMode = TextureWrapMode.Repeat;
            wave = UiKit.Raw(shape, "Vague", waveTexture, Color.Lerp(color, Color.white, 0.45f));
            RectTransform w = wave.rectTransform;
            w.anchorMin = new Vector2(0f, 0f);
            w.anchorMax = new Vector2(1f, 0f);
            w.pivot = new Vector2(0.5f, 0.5f);
            w.sizeDelta = new Vector2(0f, 16f);
            wave.uvRect = new Rect(0f, 0f, 2.6f, 1f);
            shine = UiKit.Image(shape, "Reflet", UiKit.Sprite("reflet"), UiKit.Alpha(Color.white, 0.55f));
            At(shine, -60f, -20f, 46f, 360f, 0.5f, 0f);
            shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            return rect;
        }

        private void Spark(Transform parent, float x, float y, float size, float phase)
        {
            Image spark = UiKit.Image(parent, "Éclat de lumière", UiKit.Sprite("etincelle"), Color.white);
            At(spark, x, y, size, size, 0.5f, 0.5f);
            spark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, phase * 40f);
            _sparks.Add(spark);
            _sparkPhase.Add(phase);
        }

        private readonly List<float> _sparkPhase = new List<float>();

        /// <summary>
        /// Places des éclats de compétence (centre, taille du dessin, inclinaison), dans le groupe du bas. Essai réaliste 7 (essai de
        /// Damien du 28 octobre 2026 : « les icônes sont juste trop gros, faut tous les rétrécir ») : chaque éclat est dessiné à sa taille
        /// d'origine puis réduit d'un quart (<see cref="ShardScale"/>), au plus près du sceau ; la touche, le coût et la recharge sont
        /// dessinés plus gros pour rester lisibles une fois réduits. Essai réaliste 9 : resserrés contre le sceau réduit, plus bas.
        /// </summary>
        private static readonly (float X, float Y, float Size, float Rot)[] ShardPlaces =
        {
            // Sous-lot V1.8 : six places actives (trois de chaque côté du sceau), puis l'ultime au bout, à droite.
            (262f, 280f, 88f, -9f), (330f, 268f, 88f, -6f), (398f, 256f, 88f, 4f), (602f, 256f, 88f, -4f), (670f, 268f, 88f, 6f),
            (738f, 280f, 88f, 9f), (830f, 266f, 106f, 3f),
        };

        /// <summary>Réduction des éclats de compétence (essai réaliste 7).</summary>
        public const float ShardScale = 0.74f;

        private Shard BuildShard(int index)
        {
            (float x, float y, float s, float rot) = ShardPlaces[System.Math.Min(index, ShardPlaces.Length - 1)];
            // Cinq formes d'éclat : les actives tournent sur les quatre premières, l'ultime (dernière place) prend la cinquième.
            string shape = "eclat_" + (index >= ShardPlaces.Length - 1 ? 5 : index % 4 + 1);
            var shard = new Shard { Phase = index * 0.7f };
            shard.Node = At(UiKit.Node(_bottom, "Compétence " + (index + 1)), x, y, s, s, 0.5f, 0.5f);
            shard.Node.localRotation = Quaternion.Euler(0f, 0f, -rot);
            shard.Node.localScale = new Vector3(ShardScale, ShardScale, 1f);
            Image glow = UiKit.Image(shard.Node, "Lueur", UiKit.Sprite("braise"), UiKit.Alpha(UiKit.Rose, 0.35f));
            UiKit.Fill(glow.rectTransform, -22f);
            RectTransform mask = UiKit.Fill(UiKit.Mask(shard.Node, "Forme", UiKit.Sprite(shape + "_masque")));
            shard.Icon = UiKit.Image(mask, "Icône", null, Color.white);
            UiKit.Fill(shard.Icon.rectTransform);
            shard.Dim = UiKit.Image(mask, "Indisponible", null, new Color(0.02f, 0.03f, 0.05f, 0f));
            UiKit.Fill(shard.Dim.rectTransform);
            shard.Cooldown = UiKit.Image(mask, "Recharge", UiKit.Sprite(shape + "_masque"), new Color(0.02f, 0.03f, 0.05f, 0.78f), Image.Type.Filled);
            UiKit.Fill(shard.Cooldown.rectTransform);
            shard.Cooldown.fillMethod = Image.FillMethod.Radial360;
            shard.Cooldown.fillOrigin = (int)Image.Origin360.Top;
            shard.Cooldown.fillClockwise = false;
            shard.Shine = UiKit.Image(mask, "Reflet", UiKit.Sprite("reflet"), UiKit.Alpha(Color.white, 0.6f));
            At(shard.Shine, -40f, -20f, s * 0.42f, s * 1.5f, 0.5f, 0f);
            shard.Shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            shard.Ready = UiKit.Image(mask, "Prête", UiKit.Sprite("braise"), UiKit.Alpha(UiKit.Dawn, 0f));
            UiKit.Fill(shard.Ready.rectTransform, -10f);
            shard.Flash = UiKit.Image(mask, "Éclair", null, UiKit.Alpha(Color.white, 0f));
            UiKit.Fill(shard.Flash.rectTransform);
            Image frame = UiKit.Image(shard.Node, "Cadre", UiKit.Sprite(shape + "_cadre"), Color.white);
            UiKit.Fill(frame.rectTransform, -12f);
            shard.Timer = UiText.Create(shard.Node, "Recharge", UiFace.Number, 34f, Color.white, TextAnchor.MiddleCenter);
            UiKit.Fill(shard.Timer.Rect);
            shard.Timer.Rect.localRotation = Quaternion.Euler(0f, 0f, rot);
            shard.Cost = UiText.Create(shard.Node, "Coût", UiFace.Bold, 20f, UiKit.Dawn, TextAnchor.UpperRight);
            At(shard.Cost.Rect, s - 4f, 0f, 48f, 26f, 1f, 0f);
            Image plate = UiKit.Image(shard.Node, "Touche", UiKit.Sprite("touche", 6f), Color.white, Image.Type.Sliced);
            At(plate, s / 2f, s + 8f, 36f, 28f, 0.5f, 0.5f);
            plate.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rot);
            shard.Key = UiText.Create(plate.rectTransform, "Lettre", UiFace.Bold, 20f, UiKit.Dawn, TextAnchor.MiddleCenter);
            UiKit.Fill(shard.Key.Rect);
            return shard;
        }

        private void BuildTop()
        {
            At(UiKit.Image(_top, "Voile", UiKit.Sprite("voile_haut"), Color.white), 380f, 64f, 820f, 150f, 0.5f, 0.5f);
            _targetName = UiText.Create(_top, "Nom", UiFace.Title, 28f, Color.white, TextAnchor.MiddleCenter, 4f);
            At(_targetName.Rect, 380f, 32f, 700f, 40f, 0.5f, 0.5f);
            _targetName.Gold();
            At(UiKit.Image(_top, "Cadre", UiKit.Sprite("cible_cadre"), Color.white), 380f, 48f, 620f, 52f, 0.5f, 0f);
            _targetChunk = Bar(_top, "Part perdue", UiKit.Alpha(new Color(1f, 0.95f, 0.85f), 0.9f));
            _targetFill = Bar(_top, "Vie", UiKit.Health);
            _targetFlash = Bar(_top, "Coup reçu", UiKit.Alpha(Color.white, 0f));
            _targetFlash.fillAmount = 1f;
            _targetHealth = UiText.Create(_top, "Points de vie", UiFace.Bold, 16f, UiKit.Ink, TextAnchor.MiddleCenter, 6f);
            At(_targetHealth.Rect, 380f, 75f, 600f, 22f, 0.5f, 0.5f);
            Image right = UiKit.Image(_top, "Aile droite", UiKit.Sprite("cible_aile"), Color.white);
            At(right, 682f, 52f, 70f, 32f);
            Image left = UiKit.Image(_top, "Aile gauche", UiKit.Sprite("cible_aile"), Color.white);
            At(left, 43f, 52f, 70f, 32f, 0.5f, 0f);   // retournée autour de son milieu : de 8 à 78
            left.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            Spark(_top, 684f, 60f, 20f, 0.9f);
            Spark(_top, 76f, 62f, 20f, 1.7f);
            _chip = UiKit.Image(_top, "Annonce", UiKit.Sprite("puce", 8f), Color.white, Image.Type.Sliced);
            At(_chip, 380f, 104f, 220f, 28f, 0.5f, 0f);
            _chipText = UiText.Create(_chip.rectTransform, "Annonce", UiFace.Bold, 17f, new Color(0.1f, 0.05f, 0f), TextAnchor.MiddleCenter, 12f, true);
            UiKit.Fill(_chipText.Rect);
            _targetStates = UiText.Create(_top, "Altérations", UiFace.Body, 17f, UiKit.Dawn, TextAnchor.MiddleCenter);
            At(_targetStates.Rect, 380f, 148f, 700f, 24f, 0.5f, 0.5f);
        }

        private static Image Bar(Transform parent, string name, Color color)
        {
            Image bar = UiKit.Image(parent, name, UiKit.Sprite("cible_remplissage"), color, Image.Type.Filled);
            At(bar, 380f - 310f + 18f, 48f + 19f, 584f, 20f);   // dans le cadre (620 × 52, posé à 70), à 18 et 19 de son coin
            bar.fillMethod = Image.FillMethod.Horizontal;
            bar.fillOrigin = (int)Image.OriginHorizontal.Left;
            return bar;
        }

        private void BuildRight()
        {
            _map = At(UiKit.Node(_right, "Carte"), 26f, 20f, MapSize, MapSize);
            RectTransform mask = UiKit.Fill(UiKit.Mask(_map, "Forme", UiKit.Sprite("carte_masque")));
            _mapImage = UiKit.Raw(mask, "Carte peinte", null, Color.white);
            UiKit.Fill(_mapImage.rectTransform);
            UiKit.Fill(UiKit.Image(mask, "Vignette", UiKit.Sprite("carte_vignette"), Color.white).rectTransform);
            _markers = UiKit.Fill(UiKit.Node(mask, "Repères"));
            _you = UiKit.Image(mask, "Toi", UiKit.Sprite("repere_joueur"), Color.white);
            At(_you, MapSize / 2f, MapSize / 2f, 20f, 24f, 0.5f, 0.5f);
            At(UiKit.Image(_map, "Cadre", UiKit.Sprite("carte_cadre"), Color.white), -30f, -30f, MapSize + 60f, MapSize + 60f);
            UiText north = UiText.Create(_map, "Nord", UiFace.Title, 15f, Color.white, TextAnchor.MiddleCenter);
            At(north.Rect, 118f, -34f, 30f, 20f, 0.5f, 0.5f);   // au-dessus de la flèche du cadre (de -22 à 0)
            north.Value = "N";
            Spark(_map, 120f, -26f, 30f, 0.3f);
            Spark(_map, 282f, 120f, 18f, 1.4f);
            Spark(_map, 18f, 230f, 14f, 2.0f);
            _place = UiText.Create(_right, "Lieu", UiFace.Title, 21f, Color.white, TextAnchor.MiddleCenter, 3f);
            At(_place.Rect, 171f, 338f, 330f, 28f, 0.5f, 0.5f);
            _place.Gold();
            _placeSub = UiText.Create(_right, "Zone et heure", UiFace.Body, 16f, UiKit.Muted, TextAnchor.MiddleCenter, 4f);
            At(_placeSub.Rect, 171f, 362f, 330f, 22f, 0.5f, 0.5f);
            _mapHint = UiText.Create(_right, "Agrandir", UiFace.Body, 14f, UiKit.Alpha(UiKit.Muted, 0.8f), TextAnchor.MiddleCenter, 4f);
            At(_mapHint.Rect, 171f, 384f, 330f, 18f, 0.5f, 0.5f);

            _quest = At(UiKit.Node(_right, "Quête"), 10f, 402f, 322f, 132f);
            UiKit.Fill(UiKit.Image(_quest, "Dalle", UiKit.Sprite("dalle", 40f), Color.white, Image.Type.Sliced).rectTransform);
            _questTitle = UiText.Create(_quest, "Titre", UiFace.Title, 18f, Color.white, TextAnchor.MiddleLeft, 2f);
            At(_questTitle.Rect, 22f, 30f, 280f, 24f, 0f, 0.5f);
            _questTitle.Gold();
            _questStep = UiText.Create(_quest, "Étape", UiFace.Body, 17f, UiKit.Ink, TextAnchor.UpperLeft);
            _questStep.Wrap();
            At(_questStep.Rect, 22f, 50f, 230f, 54f);
            _questProgress = UiText.Create(_quest, "Avancée", UiFace.Bold, 18f, UiKit.Dawn, TextAnchor.UpperRight);
            At(_questProgress.Rect, 300f, 50f, 60f, 24f, 1f, 0f);
            _questProgress.Gradient(UiKit.Dawn, UiKit.Teal);
            _questBar = UiKit.Image(_quest, "Barre", UiKit.Sprite("bouton"), Color.white, Image.Type.Filled);
            At(_questBar, 22f, 108f, 278f, 4f);
            _questBar.fillMethod = Image.FillMethod.Horizontal;
        }

        private void BuildHelp()
        {
            _help = At(UiKit.Node(_left, "Aide"), 24f, 24f, 400f, 318f);
            UiKit.Fill(UiKit.Image(_help, "Dalle", UiKit.Sprite("dalle", 40f), Color.white, Image.Type.Sliced).rectTransform);
            _helpTitle = UiText.Create(_help, "Titre", UiFace.Title, 18f, Color.white, TextAnchor.MiddleLeft, 2f);
            At(_helpTitle.Rect, 22f, 30f, 360f, 24f, 0f, 0.5f);
            _helpTitle.Gold();
            _helpContext = UiText.Create(_help, "Contexte", UiFace.Body, 16f, UiKit.Muted, TextAnchor.MiddleLeft, 2f);
            At(_helpContext.Rect, 22f, 56f, 360f, 22f, 0f, 0.5f);
        }

        private void HelpRow(int i)
        {
            Image plate = UiKit.Image(_help, "Touche", UiKit.Sprite("touche", 6f), Color.white, Image.Type.Sliced);
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

        // --- Réglages (essai réaliste 9) -----------------------------------------------------------------------------------------

        private RectTransform _settings;
        private UiButton _mouseMode, _invertY, _textures;
        private UiText _sensitivity, _brightness;
        private System.Action _toggleClickLook, _toggleInvert;
        private System.Action<float> _stepSensitivity;

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
            _mouseMode.Text.Value = clickLook ? "Caméra : clic gauche maintenu" : "Caméra : souris libre (sans clic)";
            _invertY.Text.Value = invertY ? "Haut-bas : inversé" : "Haut-bas : normal";
            _sensitivity.Value = "Sensibilité " + sensitivity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
            _textures.Text.Value = DeviceQuality.Label;   // sous-lot V2.12.4
            _brightness.Value = BrightnessSetting.Label;   // sous-lot V2.13
        }

        private void BuildSettings()
        {
            // Relecture : entre le bandeau « Pause » et l'indication du bas ; montré seulement sans annonce (fin de combat, défaite).
            _settings = At(UiKit.Node(_center, "Réglages"), 450f, 520f, 560f, 364f, 0.5f, 0.5f);
            UiKit.Fill(UiKit.Image(_settings, "Dalle", UiKit.WindowSprite(), Color.white, Image.Type.Sliced).rectTransform);
            UiText title = UiText.Create(_settings, "Titre", UiFace.Title, 26f, Color.white, TextAnchor.MiddleCenter, 4f);
            At(title.Rect, 280f, 34f, 500f, 36f, 0.5f, 0.5f);
            title.Gold();
            title.Value = "Réglages";
            _mouseMode = UiKit.Button(_settings, "Souris", "bouton_sobre", "", UiFace.Bold, 18f, UiKit.Ink, () => _toggleClickLook?.Invoke());
            At(_mouseMode.Rect, 280f, 86f, 440f, 44f, 0.5f, 0.5f);
            _invertY = UiKit.Button(_settings, "Haut-bas", "bouton_sobre", "", UiFace.Bold, 18f, UiKit.Ink, () => _toggleInvert?.Invoke());
            At(_invertY.Rect, 280f, 140f, 440f, 44f, 0.5f, 0.5f);
            UiButton slower = UiKit.Button(_settings, "Moins sensible", "bouton_sobre", "−", UiFace.Title, 24f, UiKit.Ink, () => _stepSensitivity?.Invoke(-0.25f));
            At(slower.Rect, 120f, 196f, 52f, 44f, 0.5f, 0.5f);
            _sensitivity = UiText.Create(_settings, "Sensibilité", UiFace.Bold, 18f, UiKit.Dawn, TextAnchor.MiddleCenter, 6f, true);
            At(_sensitivity.Rect, 280f, 196f, 240f, 40f, 0.5f, 0.5f);
            UiButton faster = UiKit.Button(_settings, "Plus sensible", "bouton", "+", UiFace.Title, 24f, new Color(0.1f, 0.05f, 0.02f), () => _stepSensitivity?.Invoke(0.25f));
            At(faster.Rect, 440f, 196f, 52f, 44f, 0.5f, 0.5f);
            // Sous-lot V2.12.4 : qualité des textures (auto, pleine, moitié, quart), sauvegardée et appliquée tout de suite.
            _textures = UiKit.Button(_settings, "Textures", "bouton_sobre", "", UiFace.Bold, 18f, UiKit.Ink, DeviceQuality.Cycle);
            At(_textures.Rect, 280f, 250f, 440f, 44f, 0.5f, 0.5f);
            // Sous-lot V2.13 (« le jeu est trop sombre ») : luminosité de 50 % à 200 %, par pas de 10, sauvegardée et appliquée en direct.
            UiButton darker = UiKit.Button(_settings, "Moins lumineux", "bouton_sobre", "−", UiFace.Title, 24f, UiKit.Ink, () => BrightnessSetting.Step(-1));
            At(darker.Rect, 120f, 304f, 52f, 44f, 0.5f, 0.5f);
            _brightness = UiText.Create(_settings, "Luminosité", UiFace.Bold, 18f, UiKit.Dawn, TextAnchor.MiddleCenter, 6f, true);
            At(_brightness.Rect, 280f, 304f, 240f, 40f, 0.5f, 0.5f);
            UiButton brighter = UiKit.Button(_settings, "Plus lumineux", "bouton", "+", UiFace.Title, 24f, new Color(0.1f, 0.05f, 0.02f), () => BrightnessSetting.Step(1));
            At(brighter.Rect, 440f, 304f, 52f, 44f, 0.5f, 0.5f);
            UiKit.Show(_settings, false);
        }

        private void BuildCenter()
        {
            _banner = UiText.Create(_center, "Bandeau", UiFace.Title, 44f, Color.white, TextAnchor.MiddleCenter, 12f);
            At(_banner.Rect, 450f, 330f, 900f, 60f, 0.5f, 0.5f);
            _banner.Gold();
            RectTransform corner = Anchored(_canvas.transform, "Minuteur", new Vector2(1f, 1f), new Vector2(200f, 60f));
            _clock = UiText.Create(corner, "Temps", UiFace.Title, 30f, Color.white, TextAnchor.MiddleRight, 4f);
            At(_clock.Rect, 176f, 36f, 200f, 40f, 1f, 0.5f);
            _clock.Gold();
            _notice = At(UiKit.Node(_center, "Annonce"), 450f, 470f, 840f, 150f, 0.5f, 0.5f);
            _noticeSlab = UiKit.Image(_notice, "Dalle", UiKit.Sprite("dalle", 40f), Color.white, Image.Type.Sliced);
            UiKit.Fill(_noticeSlab.rectTransform);
            _noticeText = UiText.Create(_notice, "Texte", UiFace.Title, 24f, Color.white, TextAnchor.MiddleCenter, 2f);
            UiKit.Fill(_noticeText.Rect, 24f);
            _hintPlate = UiKit.Image(_center, "Fond de l'indication", UiKit.Sprite("bouton_sobre", 12f), UiKit.Alpha(Color.white, 0.92f), Image.Type.Sliced);
            _hint = UiText.Create(_hintPlate.rectTransform, "Indication", UiFace.Bold, 21f, UiKit.Dawn, TextAnchor.MiddleCenter, 6f, true);
            UiKit.Fill(_hint.Rect);

            // Étape 3 : niveau gagné (titre doré, rayons de lumière, éclats), puis bords rouges quand la vie est basse.
            _levelUp = At(UiKit.Node(_center, "Niveau gagné"), 450f, 250f, 900f, 200f, 0.5f, 0.5f);
            for (int i = 0; i < 10; i++)
            {
                Image ray = UiKit.Image(_levelUp, "Rayon", UiKit.Sprite("reflet"), UiKit.Alpha(i % 2 == 0 ? UiKit.Dawn : UiKit.Rose, 0f));
                At(ray, 450f, 100f, 40f, 260f, 0.5f, 0f);
                ray.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * 36f);
                _levelUpRays.Add(ray);
            }
            Spark(_levelUp, 230f, 70f, 34f, 0.2f);
            Spark(_levelUp, 680f, 120f, 26f, 1.3f);
            Spark(_levelUp, 450f, 20f, 40f, 0.7f);
            _levelUpTitle = UiText.Create(_levelUp, "Titre", UiFace.Title, 64f, Color.white, TextAnchor.MiddleCenter, 8f);
            At(_levelUpTitle.Rect, 450f, 90f, 900f, 80f, 0.5f, 0.5f);
            _levelUpTitle.Gold();
            _levelUpSub = UiText.Create(_levelUp, "Sous-titre", UiFace.Bold, 20f, UiKit.Teal, TextAnchor.MiddleCenter, 14f, true);
            At(_levelUpSub.Rect, 450f, 148f, 900f, 28f, 0.5f, 0.5f);
            UiKit.Show(_levelUp, false);
            // Relecture : voiles blancs (transparence seule) teintés de rouge ; les voiles sombres du bas et du haut restaient noirs.
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

        /// <summary>
        /// Étape 3 : annonce d'un niveau gagné (« Niveau 5 », « Effet à ta prochaine halte ») : rayons de lumière qui tournent, titre doré
        /// qui grossit puis s'efface en trois secondes.
        /// </summary>
        public void LevelUp(int level, string detail)
        {
            _levelUpAge = 0f;
            _levelUpTitle.Value = "Niveau " + level;
            _levelUpSub.Value = detail ?? "";
            UiKit.Show(_levelUp, true);
            UiSounds.Play(UiCue.LevelUp);   // essai réaliste 7 : fanfare
        }

        // --- Mise à jour ------------------------------------------------------------------------------------------------------------

        /// <summary>Début d'une image : les étiquettes, textes flottants, repères et messages se reposent depuis zéro.</summary>
        public void Begin(float deltaTime)
        {
            UiKit.Show(_canvas, true);
            _time += deltaTime;
            _plateCount = 0;
            _floatCount = 0;
            _markerCount = 0;
            _messageCount = 0;
            _labelCount = 0;
        }

        /// <summary>Cache toute l'interface en jeu (menus, salle d'attente, écran des talents, erreurs).</summary>
        public void Hide() => UiKit.Show(_canvas, false);

        /// <summary>Le sceau, les Charges, les compétences, la garde et les altérations du joueur.</summary>
        public void Player(PlayerState state, bool compact, float deltaTime)
        {
            CombatantView me = state.View;
            if (compact != _compact)
            {
                _compact = compact;
                _bottom.localScale = compact ? new Vector3(0.85f, 0.85f, 1f) : Vector3.one;
            }
            long health = me.Health ?? 0, maxHealth = System.Math.Max(1, me.MaxHealth ?? 1);
            float healthFraction = Mathf.Clamp01(health / (float)maxHealth);
            float enduranceFraction = Mathf.Clamp01(me.Endurance / (float)System.Math.Max(1, me.MaxEndurance));
            SetLevel(_healthLiquid, _healthWave, healthFraction);
            _danger = me.IsDead || me.Health == null ? 0f : Mathf.Clamp01((0.3f - healthFraction) / 0.3f);
            SetLevel(_enduranceLiquid, _enduranceWave, enduranceFraction);
            _healthValue.Value = health.ToString();
            _healthMax.Value = "/ " + maxHealth;
            _enduranceValue.Value = me.Endurance.ToString();
            _enduranceMax.Value = "/ " + me.MaxEndurance;
            float resonance = Mathf.Clamp01(me.Resonance / (float)System.Math.Max(1, state.ResonanceMax));
            _crack.fillAmount = (40f + 240f * resonance) / 320f;
            _resonance.Value = "Résonance " + me.Resonance;

            // Charges de Rempart : éclats au-dessus du sceau, celui qui s'allume grossit un instant. Essai réaliste 7 (Damien, 28 octobre
            // 2026 : « c'est quoi les petits trucs juste au-dessus de la barre de vie ? ») : éteints, ils passaient pour des débris ; ils
            // n'apparaissent qu'avec au moins une Charge (gagnée par une parade parfaite), les Charges détenues brillent en or, les
            // places libres restent pâles, et un halo doré bat derrière celles qui sont allumées.
            int total = state.RampartChargesMax;
            ChargesShown = total > 0 && me.RampartCharges > 0;
            while (_charges.Count < total)
            {
                int i = _charges.Count;
                Image glow = UiKit.Image(_bottom, "Halo de la Charge " + (i + 1), UiKit.Sprite("braise"), UiKit.Alpha(UiKit.Dawn, 0f));
                _chargeGlows.Add(glow);
                Image charge = UiKit.Image(_bottom, "Charge " + (i + 1), UiKit.Sprite("losange_off"), Color.white);
                _charges.Add(charge);
                _chargePop.Add(0f);
            }
            for (int i = 0; i < _charges.Count; i++)
            {
                bool used = ChargesShown && i < total;
                UiKit.Show(_charges[i], used);
                UiKit.Show(_chargeGlows[i], used);
                if (!used) continue;
                float a = Mathf.Deg2Rad * (total == 1 ? 0f : -40f + 80f * i / (total - 1));
                // Essai réaliste 9 : en arc juste au-dessus du sceau réduit.
                float cx = 500f + Mathf.Sin(a) * 105f * CrestScale / 0.78f, cy = CrestTop - 6f - Mathf.Cos(a) * 12f;
                At(_charges[i], cx, cy, 17f, 27f, 0.5f, 0.5f);
                At(_chargeGlows[i], cx, cy, 44f, 44f, 0.5f, 0.5f);
                _charges[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg);
                bool lit = i < me.RampartCharges;
                _charges[i].sprite = UiKit.Sprite(lit ? "losange_on" : "losange_off");
                _charges[i].color = lit ? Color.white : UiKit.Alpha(Color.white, 0.45f);
                // Relecture de l'essai 7 : halo fixe (une couleur qui change à chaque image refait le dessin de tout le bas de l'écran).
                _chargeGlows[i].color = UiKit.Alpha(UiKit.Dawn, lit ? 0.45f : 0f);
                if (lit && _shownCharges >= 0 && i >= _shownCharges) _chargePop[i] = 1f;
                _chargePop[i] = Mathf.Max(0f, _chargePop[i] - deltaTime * 3.5f);
                float pop = 1f + 0.8f * _chargePop[i];
                _charges[i].rectTransform.localScale = new Vector3(pop, pop, 1f);
            }
            if (_shownCharges >= 0 && me.RampartCharges > _shownCharges) UiSounds.Play(UiCue.ChargeGained);
            _shownCharges = me.RampartCharges;

            // Compétences : les six places actives ouvertes par les règles 0.15.0 (touches 1 à 6), puis l'ultime (R), chacune à sa place
            // fixe (sous-lot V1.8 : la place 3 vide laisse un trou, l'ultime reste au bout). La 7e et la 8e place (emplacements 6 et 7 du
            // pont temps réel, réponse de Damien du 31 octobre 2026) n'ont pas encore d'éclat : aucune règle ne les ouvre. L'ultime se
            // reconnaît à IsUltimate (emplacement RealtimeCombat.UltimateSlot, 8), pas à son rang.
            while (_shards.Count < ShardPlaces.Length) _shards.Add(BuildShard(_shards.Count));
            for (int i = 0; i < _shards.Count; i++)
            {
                Shard shard = _shards[i];
                bool ultimatePlace = i == ShardPlaces.Length - 1;
                SkillSlot slot = state.Slots.FirstOrDefault(s => ultimatePlace ? s.IsUltimate : !s.IsUltimate && s.Slot == i);
                bool used = slot != null;
                UiKit.Show(shard.Node, used);
                if (!used) continue;
                if (shard.SkillId != slot.SkillId)
                {
                    shard.SkillId = slot.SkillId;
                    shard.Icon.sprite = UiKit.Icon(IconName(slot.SkillId));
                    shard.Icon.color = shard.Icon.sprite != null ? Color.white : new Color(0.25f, 0.3f, 0.4f);
                    shard.LastRemaining = slot.RemainingSeconds;
                }
                shard.Key.Value = slot.IsUltimate ? "R" : (slot.Slot + 1).ToString();
                shard.Cost.Value = slot.EnduranceCost > 0 ? slot.EnduranceCost.ToString() : "";
                bool cooling = slot.RemainingSeconds > 0.05 && slot.CooldownSeconds > 0;
                shard.Cooldown.fillAmount = cooling ? Mathf.Clamp01((float)(slot.RemainingSeconds / slot.CooldownSeconds)) : 0f;
                shard.Timer.Value = cooling ? slot.RemainingSeconds.ToString(slot.RemainingSeconds < 10 ? "0.0" : "0") : "";
                bool affordable = me.Endurance >= slot.EnduranceCost;
                bool ready = slot.Unavailable == null;
                shard.Dim.color = !ready && !cooling ? new Color(0.02f, 0.03f, 0.05f, 0.55f) : !affordable ? new Color(0.35f, 0f, 0.05f, 0.45f) : new Color(0f, 0f, 0f, 0f);
                shard.Cost.Color = affordable ? UiKit.Dawn : UiKit.Danger;
                // Lancée : la recharge repart ; revenue : elle s'arrête.
                if (slot.RemainingSeconds > shard.LastRemaining + 0.2) shard.FlashAlpha = 0.9f;
                if (shard.LastRemaining > 0.05 && slot.RemainingSeconds <= 0.05)
                {
                    shard.ReadyAlpha = 1f;
                    UiSounds.Play(UiCue.SkillReady);
                }
                shard.LastRemaining = slot.RemainingSeconds;
                shard.FlashAlpha = Mathf.Max(0f, shard.FlashAlpha - deltaTime * 2.6f);
                shard.ReadyAlpha = Mathf.Max(0f, shard.ReadyAlpha - deltaTime * 2f);
                shard.Flash.color = UiKit.Alpha(Color.white, shard.FlashAlpha);
                shard.Ready.color = UiKit.Alpha(UiKit.Dawn, shard.ReadyAlpha);
            }

            // Garde : levée (turquoise), parade (or), brisée (rouge) ; récupération de parade en secondes.
            bool broken = me.States.Contains("guardBreak");
            if (broken) Guard("Garde brisée", UiKit.Danger);
            else if (me.ParryOpen) Guard("Parade", UiKit.Dawn);
            else if (me.Guarding) Guard("Garde levée", UiKit.Teal);
            else if (state.ParryRecoverySeconds > 0) Guard($"Parade dans {state.ParryRecoverySeconds:0.0} s", UiKit.Endurance);
            else Guard("Garde", UiKit.Alpha(UiKit.Muted, 0.6f));
            _states.Value = me.States.Count > 0 ? string.Join("  ·  ", me.States.Select(Labels.State)) : "";
        }

        private void Guard(string text, Color color)
        {
            _guard.Value = text;
            _guard.Color = color;
        }

        /// <summary>Hauteur du liquide d'une moitié du sceau (corps de 240 de haut, 40 au-dessus du bas du dessin) et de sa vague.</summary>
        private static void SetLevel(RectTransform liquid, RawImage wave, float fraction)
        {
            float h = 240f * fraction;
            liquid.sizeDelta = new Vector2(0f, h);
            wave.rectTransform.anchoredPosition = new Vector2(0f, 40f + h);
            UiKit.Show(wave, fraction > 0.01f && fraction < 0.995f);
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
            UiKit.Show(_top, target != null);
            if (target == null)
            {
                _targetId = null;
                return;
            }
            CombatantView v = target.View;
            _targetName.Value = DisplayNames.Of(target.Id);
            float fraction = 1f;
            if (v.Health is long h && v.MaxHealth is long max && max > 0)
            {
                fraction = Mathf.Clamp01(h / (float)max);
                _targetHealth.Value = $"{h} / {max}";
            }
            else _targetHealth.Value = "PV infinis (entraînement)";
            if (_targetId != target.Id)
            {
                _targetId = target.Id;
                _chunk = fraction;
                _lastTargetFraction = -1f;
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
            // Étape 3 : la barre blanchit un instant quand la cible prend un coup.
            if (_lastTargetFraction >= 0f && fraction < _lastTargetFraction - 0.0005f) _targetFlashAlpha = 0.65f;
            _lastTargetFraction = fraction;
            bool announced = v.NextAttack != null && target.NextAttackSeconds is double && !v.IsDead;
            UiKit.Show(_chip, announced);
            if (announced)
            {
                double next = target.NextAttackSeconds ?? 0;
                bool unblockable = v.NextAttack.Unblockable;
                _chipText.Value = $"{DisplayNames.Attack(v.NextAttack.Id)} · {next:0.0} s{(unblockable ? " · imparable" : "")}";
                _chip.color = unblockable ? new Color(1f, 0.45f, 0.45f) : Color.white;
                float urgency = Mathf.Clamp01(1f - (float)next / 2f);
                float pulse = 1f + 0.06f * urgency * Mathf.Sin(_time * 12f);
                _chip.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
                _chip.rectTransform.sizeDelta = new Vector2(Mathf.Max(200f, 11f * _chipText.Value.Length + 40f), 28f);
            }
            _targetStates.Value = v.States.Count > 0 ? string.Join(", ", v.States.Select(Labels.State)) : "";
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
            plate.Name.Value = (isTarget ? "» " : "") + DisplayNames.Of(other.Id);
            plate.Name.Color = isTarget ? UiKit.Dawn : UiKit.Ink;
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
            At(UiKit.Image(plate.Node, "Cadre", UiKit.Sprite("etiquette_cadre"), Color.white), 90f, 24f, 124f, 14f, 0.5f, 0f);
            plate.Fill = UiKit.Image(plate.Node, "Vie", UiKit.Sprite("etiquette_remplissage"), UiKit.Health, Image.Type.Filled);
            At(plate.Fill, 90f, 28f, 116f, 6f, 0.5f, 0f);
            plate.Fill.fillMethod = Image.FillMethod.Horizontal;
            plate.Telegraph = UiKit.Image(plate.Node, "Coup annoncé", UiKit.Sprite("etiquette_remplissage"), UiKit.Endurance, Image.Type.Filled);
            At(plate.Telegraph, 90f, 40f, 116f, 5f, 0.5f, 0f);
            plate.Telegraph.fillMethod = Image.FillMethod.Horizontal;
            return plate;
        }

        /// <summary>Texte flottant (retour de combat) : position à l'écran, couleur, âge de 0 (naissance) à 1 (effacé).</summary>
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
            float pop = age < 0.12f ? 1f + 1.6f * (0.12f - age) : 1f;
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

        /// <summary>Minuteur de l'entraînement, en haut à droite (null : caché).</summary>
        public void Clock(string text)
        {
            _clock.Show(!string.IsNullOrEmpty(text));
            _clock.Value = text;
        }

        /// <summary>Messages au centre, sous la cible (les plus récents en bas).</summary>
        public void Message(string text)
        {
            if (_messageCount == _messagePool.Count)
                _messagePool.Add(UiText.Create(_messages, "Message", UiFace.Body, 21f, new Color(1f, 0.95f, 0.8f), TextAnchor.MiddleCenter));
            UiText m = _messagePool[_messageCount];
            m.Show(true);
            At(m.Rect, 450f, 14f + 30f * _messageCount, 900f, 28f, 0.5f, 0.5f);
            m.Value = text;
            _messageCount++;
        }

        /// <summary>
        /// Aventure : la carte peinte centrée sur le joueur (nord en haut), ses repères (ennemis en vue, PNJ, points sûrs, atelier,
        /// récoltes, objectif ramené au bord s'il est loin), le lieu, la zone et l'heure, la quête en cours. <paramref name="shown"/> faux :
        /// un écran de l'aventure ou un dialogue est ouvert (la carte et la quête se cachent). <paramref name="compact"/> : commandes
        /// tactiles affichées, leur rangée du haut passe sous la carte (plus petite, sans la quête : elle reste dans le journal).
        /// </summary>
        public void Adventure(AdventureSession a, IReadOnlyList<MapEnemy> enemies, double facingYaw, bool shown, bool compact)
        {
            UiKit.Show(_right, a != null && shown);
            if (a == null || !shown) return;
            float scale = compact ? CompactMapScale : 1f;
            _right.localScale = new Vector3(scale, scale, 1f);
            _placeSub.Show(!compact);
            _mapHint.Show(!compact);
            AdventureMap map = a.Map;
            Texture2D painted = PaintedMap.For(map);
            MapMarker me = a.MapMarkers().FirstOrDefault(m => m.Kind == MapMarkerKind.Player);
            double cx = me?.X ?? (map.MinX + map.MaxX) / 2, cz = me?.Z ?? (map.MinZ + map.MaxZ) / 2;
            _mapImage.texture = painted;
            _mapImage.color = painted != null ? Color.white : UiKit.Night;
            _mapImage.uvRect = PaintedMap.Window(map, cx, cz, MapRadius);
            _you.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -(float)facingYaw);
            foreach (MapEnemy e in enemies) Marker("repere_ennemi", e.X - cx, e.Z - cz, e.Boss ? 16f : 11f, e.Boss ? new Color(0.78f, 0.45f, 1f) : Color.white, false);
            foreach (MapMarker m in a.MapMarkers())
            {
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
            string festival = a.FestivalText;   // sous-lot V2.10 : la fête du bourg, en cours ou du jour
            string market = a.MarketLine;   // sous-lot V2.14 : le grand jour de marché (installation, marché et criée sur la place, démontage)
            _placeSub.Value = a.ZoneName + " · " + a.TimeOfDay + (festival != null ? " · " + festival : "") + (market != null ? " · " + market : "");
            _mapHint.Value = a.Text("text.ui.map.hint");

            // Sous-lot V1.6 : la quête suivie (choisie dans le journal), sinon la première principale en cours.
            JournalEntry quest = compact ? null : a.Followed;
            UiKit.Show(_quest, quest != null);
            if (quest != null)
            {
                _questTitle.Value = quest.Name;
                _questStep.Value = quest.Step ?? "";
                _questProgress.Value = quest.Progress ?? "";
                _questBar.fillAmount = Progress(quest.Progress);
                UiKit.Show(_questBar, quest.Progress != null);
            }
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
        /// Textes du centre : bandeau en haut (minuteur de l'entraînement, « Pause »), annonce dans une dalle (défaite, fin de la
        /// session), indication d'action en bas (« F : parler à Maëlle »). Null : rien.
        /// </summary>
        public void Center(string banner, string notice, string hint)
        {
            _banner.Show(!string.IsNullOrEmpty(banner));
            _banner.Value = banner;
            UiKit.Show(_notice, !string.IsNullOrEmpty(notice));
            _noticeText.Value = notice;
            UiKit.Show(_hintPlate, !string.IsNullOrEmpty(hint));
            if (string.IsNullOrEmpty(hint)) return;
            _hint.Value = hint;
            _hintPlate.rectTransform.sizeDelta = new Vector2(Mathf.Clamp(11f * hint.Length + 80f, 240f, 900f), 40f);
            At(_hintPlate, 450f, !string.IsNullOrEmpty(notice) ? 590f : 700f, _hintPlate.rectTransform.sizeDelta.x, 40f, 0.5f, 0.5f);
        }

        /// <summary>Fin d'une image : ce qui n'a pas servi se cache, les effets avancent.</summary>
        public void End(float deltaTime)
        {
            for (int i = _plateCount; i < _platePool.Count; i++) UiKit.Show(_platePool[i].Node, false);
            for (int i = _floatCount; i < _floatPool.Count; i++) _floatPool[i].Show(false);
            for (int i = _messageCount; i < _messagePool.Count; i++) _messagePool[i].Show(false);
            for (int i = _labelCount; i < _labelPool.Count; i++) _labelPool[i].Show(false);
            Animate(deltaTime);
        }

        /// <summary>Effets : vagues, fissure qui palpite, reflets qui passent, braises, éclats de lumière ; étape 3 : niveau gagné, vie basse, coup reçu.</summary>
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
            foreach (RawImage wave in new[] { _healthWave, _enduranceWave })
            {
                Rect uv = wave.uvRect;
                uv.x = (uv.x + deltaTime * 0.35f) % 1f;
                wave.uvRect = uv;
            }
            _crack.color = UiKit.Alpha(Color.white, 0.6f + 0.4f * (0.5f + 0.5f * Mathf.Sin(_time * 3.2f)));
            float crestSweep = Sweep(_time / 4f);
            foreach (Image shine in new[] { _healthShine, _enduranceShine })
                shine.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-60f, 350f, crestSweep), 20f);
            foreach (Shard shard in _shards)
            {
                float sweep = Sweep((_time + shard.Phase) / 3.2f);
                float s = shard.Node.sizeDelta.x;
                shard.Shine.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-s * 0.6f, s * 1.6f, sweep), 20f);
            }
            foreach (Ember ember in _embers)
            {
                float t = (_time / 3f + ember.Phase) % 1f;
                Color c = t * 3f % 3f < 1f ? UiKit.Dawn : t * 3f % 3f < 2f ? UiKit.Rose : UiKit.Teal;
                ember.Image.color = UiKit.Alpha(c, t < 0.12f ? t / 0.12f : 1f - (t - 0.12f) / 0.88f);
                ember.Image.rectTransform.anchoredPosition = new Vector2(ember.X + ember.Drift * t, -(70f - 160f * t));
                float size = 7f * (1f - 0.6f * t);
                ember.Image.rectTransform.sizeDelta = new Vector2(size, size);
            }
            for (int i = 0; i < _sparks.Count; i++)
            {
                float t = 0.5f + 0.5f * Mathf.Sin((_time + _sparkPhase[i]) * 2.6f);
                _sparks[i].color = UiKit.Alpha(Color.white, 0.25f + 0.75f * t);
                float k = 0.6f + 0.55f * t;
                _sparks[i].rectTransform.localScale = new Vector3(k, k, 1f);
            }
        }

        /// <summary>Avancée d'un reflet sur une période : il traverse pendant les 35 premiers pour cent, puis attend.</summary>
        private static float Sweep(float t)
        {
            float f = t - Mathf.Floor(t);
            return f < 0.35f ? f / 0.35f : 1.2f;
        }
    }
}
