using System.Collections.Generic;
using EdS.Runtime;
using EdS.World;
using UnityEngine;
using UnityEngine.UI;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.32 : l'écran de partie des Trois Feux, au centre de l'écran, hors du livre. Il s'ouvre quand on s'assoit (réponse du
    /// dialogue d'un PNJ qui joue, à la taverne le soir) et reste tant que la partie dure, puis montre l'issue jusqu'à « Se lever ». Le cœur
    /// est seul juge : l'écran ne fait que montrer la table (<see cref="AdventureSession.CardTable"/>) et poser la carte choisie.
    /// </summary>
    public sealed partial class AdventureBook
    {
        private const int CardSlots = 5;
        private RectTransform _cardsPanel;
        private UiText _cardsTitle, _cardsHeader, _cardsScore, _cardsPrompt, _cardsLast, _cardsRules;
        private readonly List<UiButton> _cardsHand = new List<UiButton>();
        private UiButton _cardsLeave;

        private void BuildCards(Transform root)
        {
            _cardsPanel = UiKit.Node(root, "Trois Feux");
            _cardsPanel.anchorMin = new Vector2(0.5f, 0.5f);
            _cardsPanel.anchorMax = new Vector2(0.5f, 0.5f);
            _cardsPanel.pivot = new Vector2(0.5f, 0.5f);
            _cardsPanel.sizeDelta = new Vector2(1100f, 520f);
            _cardsPanel.anchoredPosition = Vector2.zero;
            UiKit.Fill(UiKit.Image(_cardsPanel, "Dalle", UiKit.WindowSprite(), Color.white, Image.Type.Sliced).rectTransform);
            _cardsTitle = Text(_cardsPanel, UiFace.Title, 34f, Color.white, TextAnchor.MiddleLeft, 40f, 50f, 600f, 44f, 0f, 0.5f, 3f);
            _cardsTitle.Gold();
            _cardsHeader = Text(_cardsPanel, UiFace.Bold, 17f, UiKit.Muted, TextAnchor.MiddleLeft, 40f, 92f, 1020f, 24f, 0f, 0.5f, 8f, true);
            _cardsScore = Text(_cardsPanel, UiFace.Bold, 22f, UiKit.Ink, TextAnchor.MiddleLeft, 40f, 132f, 1020f, 30f, 0f, 0.5f);
            _cardsLast = Text(_cardsPanel, UiFace.Body, 19f, UiKit.Ink, TextAnchor.UpperLeft, 40f, 160f, 1020f, 50f, 0f, 0f);
            _cardsLast.Wrap();
            _cardsPrompt = Text(_cardsPanel, UiFace.Bold, 20f, UiKit.Dawn, TextAnchor.UpperLeft, 40f, 214f, 1020f, 50f, 0f, 0f);
            _cardsPrompt.Wrap();
            for (int i = 0; i < CardSlots; i++)
            {
                int index = i;
                UiButton card = UiKit.Button(_cardsPanel, "Carte", "bouton_sobre", "", UiFace.Bold, 20f, UiKit.Ink, () => PlayCardAt(index));
                UiKit.Place(card.Rect, new Vector2(40f + i * 206f, 280f), new Vector2(196f, 110f), Vector2.zero);
                _cardsHand.Add(card);
            }
            _cardsRules = Text(_cardsPanel, UiFace.Body, 15f, UiKit.Muted, TextAnchor.UpperLeft, 40f, 404f, 1020f, 44f, 0f, 0f);
            _cardsRules.Wrap();
            _cardsLeave = UiKit.Button(_cardsPanel, "Se lever", "bouton_sobre", "", UiFace.Bold, 15f, UiKit.Muted, () => _session?.LeaveCardTable());
            UiKit.Place(_cardsLeave.Rect, new Vector2(1060f, 460f), new Vector2(320f, 40f), new Vector2(1f, 0f));
            UiKit.Show(_cardsPanel, false);
        }

        private void PlayCardAt(int index)
        {
            if (_session != null) _session.PlayCardAt(index);
        }

        /// <summary>Couleur du texte d'une carte : soleil doré, lune turquoise, brume rose.</summary>
        private static Color SuitColor(CardSuit suit) => suit == CardSuit.Sun ? UiKit.Dawn : suit == CardSuit.Moon ? UiKit.Teal : UiKit.Rose;

        /// <summary>Une image de la table ; rend vrai si elle est ouverte (le dialogue et le livre restent alors cachés derrière).</summary>
        private bool RefreshCards(AdventureSession a)
        {
            CardTableView table = a != null ? a.CardTable : null;
            UiKit.Show(_cardsPanel, table != null);
            if (table == null) return false;
            _cardsTitle.Value = a.Text("text.ui.cards.title");
            _cardsHeader.Value = table.Header;
            _cardsScore.Value = table.Score;
            _cardsLast.Value = table.LastRound;
            _cardsPrompt.Value = table.Prompt;
            _cardsRules.Value = table.Rules;
            for (int i = 0; i < _cardsHand.Count; i++)
            {
                bool used = i < table.Hand.Count;
                UiKit.Show(_cardsHand[i].Rect, used);
                if (!used) continue;
                _cardsHand[i].Text.Value = (i + 1) + ". " + table.Hand[i].Label;
                _cardsHand[i].Text.Color = SuitColor(table.Hand[i].Suit);
            }
            _cardsLeave.Text.Value = a.Text(table.Finished ? "text.ui.cards.stand" : "text.ui.cards.leave");
            return true;
        }
    }
}
