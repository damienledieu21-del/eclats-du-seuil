using System.Collections.Generic;
using EdS.Runtime;
using EdS.World;
using UnityEngine;
using UnityEngine.UI;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.32 : l'écran de partie des Trois Feux, hors du livre. Il s'ouvre quand on s'assoit (réponse du dialogue d'un PNJ qui joue,
    /// à la taverne le soir) et reste tant que la partie dure, puis montre l'issue jusqu'à « Se lever ». Le cœur est seul juge : l'écran ne
    /// fait que montrer la table (<see cref="AdventureSession.CardTable"/>) et poser la carte choisie.
    /// Sous-lot V2.32.1 : les vraies cartes. Le tapis (5 places par joueur, le cycle au centre), la main en cartes peintes (survol, touches 1 à
    /// 5, clic, toucher au premier contact), la carte du PNJ posée face cachée puis retournée. Les images (27 faces et un dos de l'édition du
    /// joueur, le tapis, le cycle) sont chargées à l'ouverture de la table et rendues en la quittant : rien n'est gardé entre deux tables et
    /// rien n'est alloué à chaque image.
    /// </summary>
    public sealed partial class AdventureBook
    {
        private const int CardSlots = 5;
        private const float MatWidth = 1920f, MatHeight = 1080f, SlotWidth = 222f, SlotHeight = 311f, TopRowY = 116f, BottomRowY = 652f;
        private const float LeaveY = 586f, LeaveHeight = 28f, PlayedWidth = 143f, PlayedHeight = 200f, PlayedY = 436f, Lift = 30f;
        private const float FlipBack = 0.5f, FlipHalf = 0.2f, FlipDone = FlipBack + 2f * FlipHalf;
        private static readonly float[] SlotX = { 280f, 564f, 848f, 1132f, 1416f };

        private RectTransform _cardsPanel, _cardsTable;
        private RawImage _cardsMat, _cardsCycle, _cardsPlayedPlayer, _cardsPlayedNpc;
        private UiText _cardsHeader, _cardsScore, _cardsPrompt, _cardsLast, _cardsRules;
        private readonly RawImage[] _cardsHand = new RawImage[CardSlots];
        private readonly UiText[] _cardsHandLabel = new UiText[CardSlots];
        private readonly UiText[] _cardsHandKey = new UiText[CardSlots];
        private readonly RawImage[] _cardsNpc = new RawImage[CardSlots];
        private readonly Image[] _cardsPips = new Image[CardSlots];
        private UiButton _cardsLeave;

        // Images de la table ouverte : tout est rendu quand elle ferme.
        private readonly Texture2D[] _cardsFaces = new Texture2D[CardMatch.DeckSize];
        private Texture2D _cardsBack, _cardsMatTexture, _cardsCycleTexture;
        private bool _cardsLoaded, _cardsGold;
        private string _cardsShownScore = "";
        private int _cardsSeen = -1, _cardsHover = -1, _cardsInputFrame = -10;
        private float _cardsFlip = FlipDone;

        private void BuildCards(Transform root)
        {
            _cardsPanel = UiKit.Node(root, "Trois Feux");
            UiKit.Fill(_cardsPanel);
            UiKit.Fill(UiKit.Image(_cardsPanel, "Ombre", null, new Color(0f, 0f, 0f, 0.7f)).rectTransform);
            _cardsTable = UiKit.Node(_cardsPanel, "Table");
            _cardsTable.anchorMin = new Vector2(0.5f, 0.5f);
            _cardsTable.anchorMax = new Vector2(0.5f, 0.5f);
            _cardsTable.pivot = new Vector2(0.5f, 0.5f);
            _cardsTable.sizeDelta = new Vector2(MatWidth, MatHeight);
            _cardsTable.anchoredPosition = Vector2.zero;
            _cardsMat = UiKit.Raw(_cardsTable, "Tapis", null, Color.white);
            UiKit.Fill(_cardsMat.rectTransform);

            for (int i = 0; i < CardSlots; i++)
            {
                _cardsNpc[i] = UiKit.Raw(_cardsTable, "Carte du PNJ", null, Color.white);
                UiKit.Place(_cardsNpc[i].rectTransform, new Vector2(SlotX[i], TopRowY), new Vector2(SlotWidth, SlotHeight), Vector2.zero);
            }
            _cardsPlayedNpc = UiKit.Raw(_cardsTable, "Carte posée du PNJ", null, Color.white);
            UiKit.Place(_cardsPlayedNpc.rectTransform, new Vector2(1077f, PlayedY), new Vector2(PlayedWidth, PlayedHeight), new Vector2(0f, 0f));
            _cardsPlayedPlayer = UiKit.Raw(_cardsTable, "Carte posée du joueur", null, Color.white);
            UiKit.Place(_cardsPlayedPlayer.rectTransform, new Vector2(700f, PlayedY), new Vector2(PlayedWidth, PlayedHeight), new Vector2(0f, 0f));
            for (int i = 0; i < CardSlots; i++)
            {
                _cardsPips[i] = UiKit.Image(_cardsTable, "Manche", null, Color.white);
                UiKit.Place(_cardsPips[i].rectTransform, new Vector2(891f + i * 30f, 618f), new Vector2(18f, 18f), Vector2.zero);
            }
            for (int i = 0; i < CardSlots; i++)
            {
                _cardsHand[i] = UiKit.Raw(_cardsTable, "Carte de la main", null, Color.white);
                _cardsHandLabel[i] = Text(_cardsHand[i].rectTransform, UiFace.Bold, 30f, Color.white, TextAnchor.MiddleCenter, 0f, 0f, SlotWidth, SlotHeight, 0f, 0f);
                _cardsHandKey[i] = Text(_cardsTable, UiFace.Bold, 22f, Color.white, TextAnchor.MiddleCenter, SlotX[i], BottomRowY + SlotHeight + 2f, SlotWidth, 26f, 0f, 0f);
            }

            // Textes de part et d'autre des cartes posées, sur une plaque sombre pour rester lisibles sur le tapis.
            UiKit.Place(UiKit.Image(_cardsTable, "Plaque gauche", null, new Color(0f, 0f, 0f, 0.4f)).rectTransform, new Vector2(280f, 436f), new Vector2(404f, 200f), Vector2.zero);
            UiKit.Place(UiKit.Image(_cardsTable, "Plaque droite", null, new Color(0f, 0f, 0f, 0.4f)).rectTransform, new Vector2(1236f, 436f), new Vector2(404f, 200f), Vector2.zero);
            _cardsHeader = Text(_cardsTable, UiFace.Bold, 18f, new Color(0.85f, 0.85f, 0.85f), TextAnchor.UpperLeft, 292f, 442f, 380f, 46f, 0f, 0f, 2f);
            _cardsHeader.Wrap();
            _cardsScore = Text(_cardsTable, UiFace.Bold, 22f, Color.white, TextAnchor.UpperLeft, 292f, 490f, 380f, 30f, 0f, 0f);
            _cardsPrompt = Text(_cardsTable, UiFace.Bold, 20f, UiKit.Dawn, TextAnchor.UpperLeft, 292f, 526f, 380f, 106f, 0f, 0f);
            _cardsPrompt.Wrap();
            _cardsLast = Text(_cardsTable, UiFace.Body, 18f, Color.white, TextAnchor.UpperLeft, 1248f, 442f, 380f, 70f, 0f, 0f);
            _cardsLast.Wrap();
            _cardsCycle = UiKit.Raw(_cardsTable, "Cycle", null, Color.white);
            UiKit.Place(_cardsCycle.rectTransform, new Vector2(1248f, 514f), new Vector2(84f, 84f), Vector2.zero);
            _cardsRules = Text(_cardsTable, UiFace.Body, 14f, new Color(0.85f, 0.85f, 0.85f), TextAnchor.UpperLeft, 1342f, 514f, 288f, 68f, 0f, 0f);
            _cardsRules.Wrap();
            _cardsLeave = UiKit.Button(_cardsTable, "Se lever", "bouton_sobre", "", UiFace.Bold, 15f, UiKit.Muted, () => _session?.LeaveCardTable());
            UiKit.Place(_cardsLeave.Rect, new Vector2(1248f, LeaveY), new Vector2(380f, LeaveHeight), Vector2.zero);
            UiKit.Show(_cardsPanel, false);
        }

        /// <summary>Charge les images de la table qui s'ouvre (édition dorée si le joueur a le jeu rare).</summary>
        private void LoadCards(bool gold)
        {
            _cardsGold = gold;
            for (int suit = 0; suit < 3; suit++)
                for (int value = 1; value <= CardMatch.MaxValue; value++)
                    _cardsFaces[suit * CardMatch.MaxValue + value - 1] = Resources.Load<Texture2D>(CardArt.Folder + CardArt.Face((CardSuit)suit, value, gold));
            _cardsBack = Resources.Load<Texture2D>(CardArt.Folder + CardArt.Back(gold));
            _cardsMatTexture = Resources.Load<Texture2D>(CardArt.Mat(Screen.height >= 800));
            _cardsCycleTexture = Resources.Load<Texture2D>(CardArt.Cycle);
            _cardsMat.texture = _cardsMatTexture;
            _cardsMat.color = _cardsMatTexture != null ? Color.white : new Color(0.06f, 0.22f, 0.13f);
            _cardsCycle.texture = _cardsCycleTexture;
            UiKit.Show(_cardsCycle, _cardsCycleTexture != null);
            for (int i = 0; i < CardSlots; i++)
            {
                _cardsNpc[i].texture = _cardsBack;
                _cardsHand[i].texture = null;
            }
            _cardsLoaded = true;
        }

        /// <summary>Rend les images à la fermeture de la table (ou du livre) : aucune ne reste en mémoire.</summary>
        private void ReleaseCards()
        {
            if (!_cardsLoaded) return;
            _cardsLoaded = false;
            _cardsSeen = -1;
            _cardsHover = -1;
            _cardsShownScore = "";
            _cardsFlip = FlipDone;
            for (int i = 0; i < CardSlots; i++)
            {
                _cardsHand[i].texture = null;
                _cardsNpc[i].texture = null;
            }
            _cardsPlayedPlayer.texture = null;
            _cardsPlayedNpc.texture = null;
            _cardsMat.texture = null;
            _cardsCycle.texture = null;
            for (int i = 0; i < _cardsFaces.Length; i++)
            {
                if (_cardsFaces[i] != null) Resources.UnloadAsset(_cardsFaces[i]);
                _cardsFaces[i] = null;
            }
            if (_cardsBack != null) Resources.UnloadAsset(_cardsBack);
            if (_cardsMatTexture != null) Resources.UnloadAsset(_cardsMatTexture);
            if (_cardsCycleTexture != null) Resources.UnloadAsset(_cardsCycleTexture);
            _cardsBack = null;
            _cardsMatTexture = null;
            _cardsCycleTexture = null;
        }

        /// <summary>Vrai tant que la carte du PNJ se retourne : aucune nouvelle carte n'est acceptée (souris, toucher, touches 1 à 5).</summary>
        public bool CardsFlipping => _cardsLoaded && _cardsFlip < FlipDone;

        private Texture2D FaceOf(CardFace face) => _cardsFaces[(int)face.Suit * CardMatch.MaxValue + face.Value - 1];

        /// <summary>Échelle du tapis sur l'écran (hauteur de référence 1080, réduit si l'écran est plus étroit que 16:9).</summary>
        private static float MatFit => Mathf.Min(1f, UiKit.Width / MatWidth);

        /// <summary>Place de la main sous le point d'écran <paramref name="screen"/> (pixels), ou -1.</summary>
        private static int HandSlotAt(Vector2 screen, int count, int lifted)
        {
            if (Screen.height <= 0) return -1;
            float scale = Screen.height / 1080f * MatFit;
            float x = (screen.x - Screen.width * 0.5f) / scale + MatWidth * 0.5f;
            float y = (Screen.height * 0.5f - screen.y) / scale + MatHeight * 0.5f;   // origine en haut, comme la mise en page
            // Au repos la zone est la place elle-même ; seule la carte soulevée (<paramref name="lifted"/>) l'étend vers le haut, de son
            // propre décalage : jamais sur le bouton « Se lever » (qui finit avant BottomRowY - Lift).
            for (int i = 0; i < count && i < CardSlots; i++)
            {
                if (x < SlotX[i] || x > SlotX[i] + SlotWidth) continue;
                float top = i == lifted ? BottomRowY - Lift : BottomRowY;
                return y >= top && y <= BottomRowY + SlotHeight ? i : -1;
            }
            return -1;
        }

        /// <summary>
        /// Souris et doigt sur la table (appelé à chaque image par le jeu quand elle est ouverte) : survol de la main, clic pour poser la carte.
        /// Au toucher, seul le premier contact (<see cref="TouchPhase.Began"/>) compte ; la souris que le système simule à partir du doigt est
        /// ignorée, pour ne poser qu'une carte.
        /// </summary>
        public void ReadCardsInput(AdventureSession a)
        {
            CardTableView table = a != null ? a.CardTable : null;
            // L'entrée de l'image où la table s'ouvre (le clic ou le toucher sur « [Cartes] Une partie… ») n'est pas pour les cartes : on ne
            // lit qu'à partir de l'image suivante, tant que la lecture se poursuit d'une image à l'autre.
            bool opening = _cardsInputFrame < Time.frameCount - 1;
            _cardsInputFrame = Time.frameCount;
            int lifted = _cardsHover;
            _cardsHover = -1;
            if (table == null || table.Hand.Count == 0 || opening || CardsFlipping) return;
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.phase != TouchPhase.Began) continue;
                    int slot = HandSlotAt(touch.position, table.Hand.Count, -1);
                    if (slot >= 0) a.PlayCardAt(slot);
                    return;
                }
                return;
            }
            Vector2 mouse = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
            _cardsHover = HandSlotAt(mouse, table.Hand.Count, lifted);
            if (_cardsHover >= 0 && Input.GetMouseButtonDown(0)) a.PlayCardAt(_cardsHover);
        }

        private static Color PipColor(int result) => result > 0 ? UiKit.Dawn : result < 0 ? UiKit.Rose : UiKit.Muted;

        /// <summary>Une image de la table ; rend vrai si elle est ouverte (le dialogue et le livre restent alors cachés derrière).</summary>
        private bool RefreshCards(AdventureSession a, float deltaTime)
        {
            CardTableView table = a != null ? a.CardTable : null;
            UiKit.Show(_cardsPanel, table != null);
            if (table == null)
            {
                ReleaseCards();
                return false;
            }
            if (!_cardsLoaded) LoadCards(table.Gold);
            _cardsTable.localScale = new Vector3(MatFit, MatFit, 1f);

            // Une manche de plus : la carte du PNJ est posée face cachée, puis retournée ; le résultat attend la fin du retournement.
            if (table.RoundsPlayed != _cardsSeen)
            {
                _cardsFlip = _cardsSeen < 0 ? FlipDone : 0f;
                _cardsSeen = table.RoundsPlayed;
            }
            else if (_cardsFlip < FlipDone) _cardsFlip += deltaTime;
            bool revealed = _cardsFlip >= FlipDone;

            _cardsHeader.Value = table.Header;
            // Le score (qui dit qui a gagné la manche) garde sa valeur d'avant tant que la carte du PNJ n'est pas retournée.
            if (revealed) _cardsShownScore = table.Score;
            _cardsScore.Value = _cardsShownScore;
            _cardsPrompt.Value = revealed || !table.Finished ? table.Prompt : "";
            _cardsLast.Value = revealed ? table.LastRound : "";
            _cardsRules.Value = table.Rules;

            for (int i = 0; i < CardSlots; i++)
            {
                bool used = i < table.Hand.Count;
                UiKit.Show(_cardsHand[i], used);
                UiKit.Show(_cardsHandKey[i].Rect, used);
                UiKit.Show(_cardsNpc[i], i < table.NpcCards);
                if (!used) continue;
                CardFace card = table.Hand[i];
                Texture2D face = FaceOf(card);
                _cardsHand[i].texture = face;
                _cardsHand[i].color = face != null ? Color.white : UiKit.Alpha(new Color(0.15f, 0.15f, 0.2f), 0.9f);
                UiKit.Show(_cardsHandLabel[i].Rect, face == null);
                if (face == null)
                {
                    _cardsHandLabel[i].Value = card.Label;
                    _cardsHandLabel[i].Color = SuitColor(card.Suit);
                }
                bool hover = i == _cardsHover;
                UiKit.Place(_cardsHand[i].rectTransform, new Vector2(SlotX[i], BottomRowY - (hover ? Lift : 0f)), new Vector2(SlotWidth, SlotHeight), Vector2.zero);
                _cardsHandKey[i].Value = (i + 1).ToString();
                _cardsHandKey[i].Color = hover ? UiKit.Dawn : Color.white;
            }

            // Cartes posées de la dernière manche.
            bool played = table.LastPlayer != null && table.LastNpc != null;
            UiKit.Show(_cardsPlayedPlayer, played);
            UiKit.Show(_cardsPlayedNpc, played);
            if (played)
            {
                Texture2D mine = FaceOf(table.LastPlayer);
                _cardsPlayedPlayer.texture = mine;
                _cardsPlayedPlayer.color = mine != null ? Color.white : UiKit.Alpha(new Color(0.15f, 0.15f, 0.2f), 0.9f);
                float flip = _cardsFlip;
                bool front = flip >= FlipBack + FlipHalf;
                float width = flip < FlipBack || flip >= FlipDone ? 1f : flip < FlipBack + FlipHalf ? 1f - (flip - FlipBack) / FlipHalf : (flip - FlipBack - FlipHalf) / FlipHalf;
                Texture2D theirs = front ? FaceOf(table.LastNpc) : _cardsBack;
                _cardsPlayedNpc.texture = theirs;
                _cardsPlayedNpc.color = theirs != null ? Color.white : UiKit.Alpha(new Color(0.15f, 0.15f, 0.2f), 0.9f);
                _cardsPlayedNpc.rectTransform.localScale = new Vector3(Mathf.Max(0.02f, width), 1f, 1f);
                UiKit.Place(_cardsPlayedNpc.rectTransform, new Vector2(1077f + PlayedWidth * 0.5f, PlayedY), new Vector2(PlayedWidth, PlayedHeight), new Vector2(0.5f, 0f));
            }
            for (int i = 0; i < CardSlots; i++)
            {
                bool done = i < table.RoundsPlayed && (revealed || i < table.RoundsPlayed - 1);
                _cardsPips[i].color = done ? PipColor(table.RoundResults[i]) : new Color(0f, 0f, 0f, 0.45f);
            }
            _cardsLeave.Text.Value = a.Text(table.Finished ? "text.ui.cards.stand" : "text.ui.cards.leave");
            return true;
        }

        /// <summary>Couleur du texte d'une carte (secours sans image) : soleil doré, lune turquoise, brume rose.</summary>
        private static Color SuitColor(CardSuit suit) => suit == CardSuit.Sun ? UiKit.Dawn : suit == CardSuit.Moon ? UiKit.Teal : UiKit.Rose;
    }
}
