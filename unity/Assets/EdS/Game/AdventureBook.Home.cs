using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace EdS.Game
{
    /// <summary>
    /// Maison du joueur, sous-lot V2.53.3 : le panneau de la maison (H dans la maison), au style de l'étal de fête. À gauche, les meubles à
    /// poser (bouton Poser : le panneau se ferme et le meuble suit la souris sur la grille de 0,25 m : R le tourne, clic gauche le pose,
    /// Échap annule) ; au milieu, les meubles posés (Tourner, Reprendre) ; à droite, le coffre (Prendre) et le sac (Ranger), puis Dormir. Tout
    /// passe par <see cref="AdventureSession"/> : un refus ne change rien et sa raison s'affiche dans les messages du jeu.
    /// </summary>
    public sealed partial class AdventureBook
    {
        private const int HomeRows = 10;

        /// <summary>
        /// Revue Codex de la PR #231 : trois colonnes qui tiennent dans le panneau (<see cref="PanelWidth"/>, 1 500) : meubles à poser de 60 à
        /// 500, meubles posés de 520 à 960, coffre et sac de 980 à 1 440 (boutons compris, à droite de leur colonne).
        /// </summary>
        internal const float ListWidth = 440f, PlacedX = 520f, ChestX = 980f, ChestWidth = 460f;

        private readonly List<(RectTransform Row, UiText Name, UiButton Act)> _homeToPlace = new List<(RectTransform, UiText, UiButton)>();
        private readonly List<(RectTransform Row, UiText Name, UiButton Turn, UiButton Take)> _homePlaced = new List<(RectTransform, UiText, UiButton, UiButton)>();
        private readonly List<(RectTransform Row, UiText Name, UiButton Act)> _homeChest = new List<(RectTransform, UiText, UiButton)>();
        private readonly List<(RectTransform Row, UiText Name, UiButton Act)> _homeBag = new List<(RectTransform, UiText, UiButton)>();
        private IReadOnlyList<AdventureSession.HomePieceLine> _homeLines = new AdventureSession.HomePieceLine[0];
        private IReadOnlyList<HomePieceLook> _homeLooks = new HomePieceLook[0];
        private int _homeOffset, _homePlacedOffset, _homeBagOffset, _homeChestOffset;
        private UiText _homeToPlaceTitle, _homePlacedTitle, _homeChestTitle, _homeBagTitle;
        private UiButton _homeMore, _homePlacedMore, _homeBagMore, _homeChestMore, _homeSleep;

        /// <summary>Le meuble choisi par Poser, pris une seule fois par le jeu (mode décoration) ; null sinon.</summary>
        public string TakeHomeCarry()
        {
            string piece = _homeCarry;
            _homeCarry = null;
            return piece;
        }

        private string _homeCarry;

        private (RectTransform, UiText, UiButton) HomeRow(RectTransform page, string name, float x, float y, float width, System.Action click)
        {
            RectTransform row = UiKit.Node(page, name);
            UiKit.Place(row, new Vector2(x, y), new Vector2(width, 50f), Vector2.zero);
            Image back = UiKit.Image(row, "Fond", UiKit.Sprite("bouton_sobre", 12f), new Color(1f, 1f, 1f, 0.55f), Image.Type.Sliced);
            UiKit.Fill(back.rectTransform);
            UiText label = Text(row, UiFace.Bold, 16f, UiKit.Ink, TextAnchor.MiddleLeft, 12f, 25f, width - 140f, 24f, 0f, 0.5f);
            UiButton act = UiKit.Button(row, "Agir", "bouton", "", UiFace.Bold, 14f, new Color(0.1f, 0.05f, 0.02f), click);
            UiKit.Place(act.Rect, new Vector2(width - 8f, 25f), new Vector2(120f, 36f), new Vector2(1f, 0.5f));
            return (row, label, act);
        }

        private void BuildHome(RectTransform page)
        {
            _homeToPlaceTitle = Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 60f, 168f, ListWidth, 20f, 0f, 0.5f, 14f, true);
            _homePlacedTitle = Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, PlacedX, 168f, ListWidth, 20f, 0f, 0.5f, 14f, true);
            _homeChestTitle = Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, ChestX, 168f, ChestWidth, 20f, 0f, 0.5f, 14f, true);
            _homeBagTitle = Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, ChestX, 500f, ChestWidth, 20f, 0f, 0.5f, 14f, true);
            for (int i = 0; i < HomeRows; i++)
            {
                int index = i;
                _homeToPlace.Add(HomeRow(page, "Meuble à poser", 60f, 190f + i * 56f, ListWidth, () => CarryHomeRow(index)));
                var placed = HomeRow(page, "Meuble posé", PlacedX, 190f + i * 56f, ListWidth, () => PickUpHomeRow(index));
                UiButton turn = UiKit.Button(placed.Item1, "Tourner", "bouton_sobre", "", UiFace.Bold, 14f, UiKit.Ink, () => TurnHomeRow(index));
                UiKit.Place(turn.Rect, new Vector2(ListWidth - 136f, 25f), new Vector2(110f, 36f), new Vector2(1f, 0.5f));
                placed.Item2.Rect.sizeDelta = new Vector2(ListWidth - 260f, placed.Item2.Rect.sizeDelta.y);   // le nom laisse la place aux deux boutons
                _homePlaced.Add((placed.Item1, placed.Item2, turn, placed.Item3));
            }
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                _homeChest.Add(HomeRow(page, "Place du coffre", ChestX, 190f + i * 56f, ChestWidth, () => _session?.ChestTake(_homeChestOffset + index)));
                _homeBag.Add(HomeRow(page, "Place du sac", ChestX, 522f + i * 56f, ChestWidth, () => _session?.ChestPut(_homeBagOffset + index)));
            }
            _homeMore = UiKit.Button(page, "Suivants", "bouton_sobre", "", UiFace.Bold, 14f, UiKit.Ink, () => _homeOffset = KeyBindings.NextPage(_homeOffset, _homeLines.Count, HomeRows));
            UiKit.Place(_homeMore.Rect, new Vector2(60f + ListWidth, 760f), new Vector2(160f, 36f), new Vector2(1f, 0.5f));
            _homePlacedMore = UiKit.Button(page, "Suivants", "bouton_sobre", "", UiFace.Bold, 14f, UiKit.Ink, () => _homePlacedOffset = KeyBindings.NextPage(_homePlacedOffset, _homeLooks.Count, HomeRows));
            UiKit.Place(_homePlacedMore.Rect, new Vector2(PlacedX + ListWidth, 760f), new Vector2(160f, 36f), new Vector2(1f, 0.5f));
            _homeBagMore = UiKit.Button(page, "Suivants", "bouton_sobre", "", UiFace.Bold, 14f, UiKit.Ink, () => _homeBagOffset = KeyBindings.NextPage(_homeBagOffset, _session != null ? _session.BagLines.Count : 0, 5));
            UiKit.Place(_homeBagMore.Rect, new Vector2(ChestX + ChestWidth, 810f), new Vector2(160f, 36f), new Vector2(1f, 0.5f));
            // Relecture de la PR #231 : le coffre a 8 places ou plus : « Suivants » comme pour le sac (rien n'y reste hors d'atteinte).
            _homeChestMore = UiKit.Button(page, "Suivants", "bouton_sobre", "", UiFace.Bold, 14f, UiKit.Ink,
                () => _homeChestOffset = KeyBindings.NextPage(_homeChestOffset, _session != null ? _session.HomeChest.Count : 0, 5));
            UiKit.Place(_homeChestMore.Rect, new Vector2(ChestX + ChestWidth, 478f), new Vector2(160f, 36f), new Vector2(1f, 0.5f));
            _homeSleep = UiKit.Button(page, "Dormir", "bouton", "", UiFace.Bold, 16f, new Color(0.1f, 0.05f, 0.02f), () => SleepAtHome());
            UiKit.Place(_homeSleep.Rect, new Vector2(ChestX + ChestWidth, 860f), new Vector2(200f, 44f), new Vector2(1f, 0.5f));
        }

        private void CarryHomeRow(int row)
        {
            int i = _homeOffset + row;
            if (_session == null || i >= _homeLines.Count) return;
            _homeCarry = _homeLines[i].Piece;
            _request = Request.Close;
        }

        private void PickUpHomeRow(int row)
        {
            int i = _homePlacedOffset + row;
            if (_session != null && i < _homeLooks.Count) _session.PickUpFurniture(_homeLooks[i].CellX, _homeLooks[i].CellZ);
        }

        private void TurnHomeRow(int row)
        {
            int i = _homePlacedOffset + row;
            if (_session != null && i < _homeLooks.Count) _session.RotateFurniture(_homeLooks[i].CellX, _homeLooks[i].CellZ);
        }

        private void SleepAtHome()
        {
            if (_session != null && _session.Sleep()) _request = Request.Close;
        }

        /// <summary>Une image du panneau de la maison : meubles à poser, meubles posés, coffre, sac.</summary>
        private void RefreshHome(AdventureSession a)
        {
            if (!a.AtHome)
            {
                _request = Request.Close;
                return;
            }
            if (_tipAnchor != null) ClearTip();
            _title.Value = a.Text("text.ui.home.panel_title");
            _subtitle.Value = a.Text("text.ui.home.panel_hint");
            _homeLines = a.HomePiecesToPlace;
            _homeLooks = a.HomePieces;
            if (_homeOffset >= _homeLines.Count) _homeOffset = 0;
            if (_homePlacedOffset >= _homeLooks.Count) _homePlacedOffset = 0;
            _homeToPlaceTitle.Value = a.Text("text.ui.home.to_place");
            _homePlacedTitle.Value = a.Text("text.ui.home.placed");
            for (int i = 0; i < HomeRows; i++)
            {
                int k = _homeOffset + i;
                var row = _homeToPlace[i];
                UiKit.Show(row.Row, k < _homeLines.Count);
                if (k < _homeLines.Count)
                {
                    AdventureSession.HomePieceLine line = _homeLines[k];
                    row.Name.Value = line.Available > 1 ? line.Name + " ×" + line.Available : line.Name;
                    row.Act.Text.Value = a.Text("text.ui.home.place");
                }
                int m = _homePlacedOffset + i;
                var placed = _homePlaced[i];
                UiKit.Show(placed.Row, m < _homeLooks.Count);
                if (m < _homeLooks.Count)
                {
                    placed.Name.Value = a.HomePieceNameOf(_homeLooks[m].Piece);
                    placed.Turn.Text.Value = a.Text("text.ui.home.turn");
                    placed.Take.Text.Value = a.Text("text.ui.home.take_back");
                }
            }
            UiKit.Show(_homeMore.Rect, _homeLines.Count > HomeRows);
            UiKit.Show(_homePlacedMore.Rect, _homeLooks.Count > HomeRows);
            _homeMore.Text.Value = _homePlacedMore.Text.Value = _homeBagMore.Text.Value = a.Text("text.ui.home.more");

            IReadOnlyList<(string Item, string Name, int Count)> chest = a.HomeChest;
            _homeChestTitle.Value = string.Format(a.Text("text.ui.home.chest"), chest.Count, a.HomeChestSize);
            if (_homeChestOffset >= chest.Count) _homeChestOffset = 0;
            for (int i = 0; i < _homeChest.Count; i++)
            {
                int k = _homeChestOffset + i;
                var row = _homeChest[i];
                UiKit.Show(row.Row, k < chest.Count);
                if (k >= chest.Count) continue;
                row.Name.Value = chest[k].Count > 1 ? chest[k].Name + " ×" + chest[k].Count : chest[k].Name;
                row.Act.Text.Value = a.Text("text.ui.home.chest_take");
            }
            IReadOnlyList<(string Name, int Count)> bag = a.BagLines;
            if (_homeBagOffset >= bag.Count) _homeBagOffset = 0;
            _homeBagTitle.Value = a.Text("text.ui.home.bag");
            for (int i = 0; i < _homeBag.Count; i++)
            {
                int k = _homeBagOffset + i;
                var row = _homeBag[i];
                UiKit.Show(row.Row, k < bag.Count);
                if (k >= bag.Count) continue;
                row.Name.Value = bag[k].Count > 1 ? bag[k].Name + " ×" + bag[k].Count : bag[k].Name;
                row.Act.Text.Value = a.Text("text.ui.home.chest_put");
            }
            UiKit.Show(_homeBagMore.Rect, bag.Count > _homeBag.Count);
            UiKit.Show(_homeChestMore.Rect, chest.Count > _homeChest.Count);
            _homeChestMore.Text.Value = a.Text("text.ui.home.more");
            _homeSleep.Text.Value = a.Text("text.ui.home.sleep");
        }
    }
}
