using System.Collections.Generic;
using EdS.Data.Feasts;
using EdS.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot F4 (les 12 fêtes d'Orsédia dans le jeu) : l'étal de fête de Maître Florentin dans le grand panneau de l'aventure, au style de la
    /// boutique (<see cref="BuildMerchant"/>). On l'ouvre par la réponse « Voir l'étal de fête » de son dialogue. À gauche, ce qu'il vend ce
    /// mois-ci (icône, nom, sorte, prix en monnaie de la fête, bouton Acheter, grisé avec l'état quand l'achat n'est pas possible), la récompense
    /// de l'an dernier en rattrapage comprise ; à droite, la monnaie de la fête, où en est la fête, ses règles, les autres monnaies de fête, et
    /// le bouton Parler. Boutons uGUI (souris et toucher) ; le jeu est figé tant que le panneau est ouvert (pas de fuite d'entrées). Tout passe
    /// par <see cref="AdventureSession"/> : un refus ne change rien et sa raison s'affiche dans les messages du jeu.
    /// </summary>
    public sealed partial class AdventureBook
    {
        private const int FeastRows = 9;

        /// <summary>
        /// Revue Codex de la PR #218 : toutes les autres monnaies gardées sont visibles (onze au plus : douze fêtes moins celle en cours), sur
        /// deux colonnes de six lignes.
        /// </summary>
        private const int FeastWalletRows = 12, FeastWalletLines = 6;

        private readonly List<(RectTransform Row, Image Icon, UiText Name, UiText Kind, UiText Price, UiButton Buy)> _feastRows =
            new List<(RectTransform, Image, UiText, UiText, UiText, UiButton)>();
        private readonly List<(RectTransform Row, Image Icon, UiText Name, UiText Coins)> _feastWalletRows =
            new List<(RectTransform, Image, UiText, UiText)>();
        private IReadOnlyList<FeastShopLine> _feastLines = new FeastShopLine[0];
        private UiText _feastOffersTitle, _feastWalletTitle, _feastWallet, _feastProgress, _feastMotto, _feastRules, _feastWalletsTitle, _feastNoWallet;
        private Image _feastCoin;
        private UiButton _feastTalk;

        private void BuildFeast(RectTransform page)
        {
            _feastOffersTitle = Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 60f, 168f, 480f, 20f, 0f, 0.5f, 14f, true);
            for (int i = 0; i < FeastRows; i++)
            {
                int index = i;
                RectTransform row = UiKit.Node(page, "Offre de fête");
                UiKit.Place(row, new Vector2(60f, 192f + i * 66f), new Vector2(760f, 58f), Vector2.zero);
                Image back = UiKit.Image(row, "Fond", UiKit.Sprite("bouton_sobre", 12f), new Color(1f, 1f, 1f, 0.55f), Image.Type.Sliced);
                UiKit.Fill(back.rectTransform);
                Image icon = UiKit.Image(row, "Icône", null, Color.white);
                UiKit.Place(icon.rectTransform, new Vector2(8f, 5f), new Vector2(48f, 48f), Vector2.zero);
                UiText name = Text(row, UiFace.Bold, 19f, UiKit.Ink, TextAnchor.MiddleLeft, 68f, 20f, 330f, 24f, 0f, 0.5f);
                UiText kind = Text(row, UiFace.Body, 14f, UiKit.Muted, TextAnchor.MiddleLeft, 68f, 42f, 330f, 18f, 0f, 0.5f);
                UiText price = Text(row, UiFace.Bold, 17f, UiKit.Dawn, TextAnchor.MiddleRight, 590f, 29f, 180f, 24f, 1f, 0.5f);
                UiButton buy = UiKit.Button(row, "Acheter", "bouton", "", UiFace.Bold, 15f, new Color(0.1f, 0.05f, 0.02f), () => BuyFeastRow(index));
                UiKit.Place(buy.Rect, new Vector2(752f, 29f), new Vector2(150f, 40f), new Vector2(1f, 0.5f));
                _feastRows.Add((row, icon, name, kind, price, buy));
            }

            _feastWalletTitle = Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 870f, 168f, 560f, 20f, 0f, 0.5f, 14f, true);
            _feastCoin = UiKit.Image(page, "Monnaie", null, Color.white);
            UiKit.Place(_feastCoin.rectTransform, new Vector2(870f, 192f), new Vector2(72f, 72f), Vector2.zero);
            _feastWallet = Text(page, UiFace.Title, 26f, UiKit.Dawn, TextAnchor.MiddleLeft, 958f, 228f, 480f, 34f, 0f, 0.5f, 2f);
            _feastProgress = Text(page, UiFace.Bold, 17f, UiKit.Ink, TextAnchor.UpperLeft, 870f, 284f, 560f, 48f, 0f, 0f);
            _feastProgress.Wrap();
            _feastMotto = Text(page, UiFace.Body, 17f, UiKit.Muted, TextAnchor.UpperLeft, 870f, 340f, 560f, 48f, 0f, 0f);
            _feastMotto.Wrap();
            _feastRules = Text(page, UiFace.Body, 15f, UiKit.Muted, TextAnchor.UpperLeft, 870f, 400f, 560f, 90f, 0f, 0f);
            _feastRules.Wrap();

            _feastWalletsTitle = Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 870f, 500f, 560f, 20f, 0f, 0.5f, 14f, true);
            _feastNoWallet = Text(page, UiFace.Body, 16f, UiKit.Muted, TextAnchor.MiddleLeft, 870f, 528f, 560f, 22f, 0f, 0.5f);
            for (int i = 0; i < FeastWalletRows; i++)
            {
                RectTransform row = UiKit.Node(page, "Monnaie de fête");
                UiKit.Place(row, new Vector2(870f + i / FeastWalletLines * 285f, 520f + i % FeastWalletLines * 44f), new Vector2(275f, 40f), Vector2.zero);
                Image icon = UiKit.Image(row, "Icône", null, Color.white);
                UiKit.Place(icon.rectTransform, new Vector2(0f, 2f), new Vector2(36f, 36f), Vector2.zero);
                UiText name = Text(row, UiFace.Bold, 15f, UiKit.Ink, TextAnchor.MiddleLeft, 42f, 20f, 150f, 24f, 0f, 0.5f);
                UiText coins = Text(row, UiFace.Bold, 15f, UiKit.Dawn, TextAnchor.MiddleRight, 272f, 20f, 82f, 24f, 1f, 0.5f);
                _feastWalletRows.Add((row, icon, name, coins));
            }

            _feastTalk = UiKit.Button(page, "Parler", "bouton_sobre", "", UiFace.Bold, 16f, UiKit.Ink, () => _session?.TalkToFeastMerchant());
            UiKit.Place(_feastTalk.Rect, new Vector2(1440f, 834f), new Vector2(200f, 44f), new Vector2(1f, 0.5f));
        }

        /// <summary>Bouton Acheter de la ligne <paramref name="row"/>.</summary>
        private void BuyFeastRow(int row)
        {
            // Relecture indépendante de la PR #218 : un double clic n'achète qu'une fois (un plat s'achète plusieurs fois) : la ligne reste
            // bloquée jusqu'au rafraîchissement suivant du panneau, qui relit les offres.
            if (_session == null || row >= _feastLines.Count || _feastBuyLocked) return;
            _feastBuyLocked = true;
            _session.BuyFeastOffer(_feastLines[row].Code);
        }

        /// <summary>Un achat a été fait depuis le dernier rafraîchissement (les boutons sont bloqués jusqu'à lui).</summary>
        private bool _feastBuyLocked;

        /// <summary>Une image de l'étal ouvert : titre, offres du mois, monnaie de la fête, avancement, règles, autres monnaies.</summary>
        private void RefreshFeast(AdventureSession a)
        {
            if (!a.FeastStallOpen) return;
            _feastBuyLocked = false;   // les offres sont relues ci-dessous
            if (_tipAnchor != null) ClearTip();   // fiche restée d'une autre page
            _title.Value = a.FeastStallTitle ?? "";
            _subtitle.Value = a.FeastStallSubtitle ?? "";
            _feastOffersTitle.Value = a.Text("text.ui.fete.offers");
            _feastLines = a.FeastShopLines();
            for (int i = 0; i < _feastRows.Count; i++)
            {
                var row = _feastRows[i];
                bool used = i < _feastLines.Count;
                UiKit.Show(row.Row, used);
                if (!used) continue;
                FeastShopLine line = _feastLines[i];
                row.Icon.sprite = UiKit.Icon(line.Icon);
                UiKit.Show(row.Icon, row.Icon.sprite != null);
                row.Name.Value = line.Name;
                row.Name.Color = line.Category == FeastCategory.Reward ? RarityColor(EdS.Data.Items.ItemRarity.Rare) : UiKit.Ink;
                row.Kind.Value = line.Kind;
                row.Price.Value = line.PriceText;
                row.Price.Color = line.TooDear ? UiKit.Danger : line.Buyable ? UiKit.Dawn : UiKit.Muted;
                row.Buy.Text.Value = a.Text("text.ui.fete.buy");
                row.Buy.Enable(line.Buyable);
            }

            _feastWalletTitle.Value = a.Text("text.ui.fete.wallet");
            _feastWallet.Value = a.FeastWalletText ?? "";
            IReadOnlyList<FeastWalletLine> wallets = a.FeastWallets;
            _feastCoin.sprite = wallets.Count > 0 && wallets[0].Current ? UiKit.Icon(wallets[0].Icon) : null;
            UiKit.Show(_feastCoin, _feastCoin.sprite != null);
            _feastProgress.Value = a.FeastProgressText ?? "";
            _feastMotto.Value = a.FeastMotto ?? "";
            _feastRules.Value = a.Text("text.ui.fete.rules");

            // Les autres monnaies de fête gardées (celle de la fête en cours est au-dessus).
            _feastWalletsTitle.Value = a.Text("text.ui.fete.wallets");
            int shown = 0;
            foreach (FeastWalletLine w in wallets)
            {
                if (w.Current || shown >= _feastWalletRows.Count) continue;
                var row = _feastWalletRows[shown++];
                row.Icon.sprite = UiKit.Icon(w.Icon);
                UiKit.Show(row.Icon, row.Icon.sprite != null);
                row.Name.Value = w.Name;
                row.Coins.Value = w.CoinsText;
            }
            for (int i = shown; i < _feastWalletRows.Count; i++) UiKit.Show(_feastWalletRows[i].Row, false);
            for (int i = 0; i < shown; i++) UiKit.Show(_feastWalletRows[i].Row, true);
            _feastNoWallet.Value = shown == 0 ? a.Text("text.ui.fete.no_wallet") : "";
            _feastTalk.Text.Value = a.Text("text.ui.fete.talk");
        }

        /// <summary>Sous-lot F4 : la monnaie de la fête en cours, à la suite de la bourse (écran du personnage) ; rien hors fête.</summary>
        private static string FeastPurse(AdventureSession a)
        {
            IReadOnlyList<FeastWalletLine> wallets = a.FeastWallets;
            return wallets.Count > 0 && wallets[0].Current ? " · " + wallets[0].Name + " " + wallets[0].Coins : "";
        }

        // --- Sous-lot F6 : plats de fête dans l'écran du personnage ------------------------------------------------------------

        /// <summary>
        /// Revue Codex de la PR #220 : une case par fête qui a des plats gardés (douze au plus, une par fête), toutes visibles : deux lignes de
        /// six (case de 40, nom à côté).
        /// </summary>
        private const int DishCells = 12, DishColumns = 6;
        private readonly List<(Cell Cell, UiText Name)> _dishCells = new List<(Cell, UiText)>();
        private IReadOnlyList<AdventureSession.FeastDishLine> _dishes = new AdventureSession.FeastDishLine[0];

        /// <summary>Relecture indépendante de la PR #220 : la fête de chaque case (un clic mange le plat de sa case, jamais celui d'à côté).</summary>
        private readonly string[] _dishFestivals = new string[DishCells];
        private UiText _dishesTitle;

        /// <summary>Les plats de fête gardés, sous l'aide du sac : une case par plat (icône, nombre), un clic en mange un.</summary>
        private void BuildDishes(RectTransform page)
        {
            _dishesTitle = Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 730f, 770f, 712f, 20f, 0f, 0.5f, 14f, true);
            for (int i = 0; i < DishCells; i++)
            {
                int index = i;
                float x = 730f + i % DishColumns * 118f, y = 788f + i / DishColumns * 46f;
                Cell cell = MakeCell(page, x, y, 40f, false);
                UiPointer pointer = UiKit.Pointer(cell.Slab);
                pointer.Left = () => EatDish(index);
                UiText name = Text(page, UiFace.Body, 12f, UiKit.Ink, TextAnchor.MiddleLeft, x + 44f, y + 20f, 72f, 40f, 0f, 0.5f);
                name.Wrap();
                _dishCells.Add((cell, name));
            }
        }

        /// <summary>Clic sur un plat : mange un plat de la fête de cette case (vide : rien).</summary>
        private void EatDish(int index)
        {
            string festival = index < _dishFestivals.Length ? _dishFestivals[index] : null;
            if (_session != null && festival != null) _session.EatFeastDish(festival);
        }

        private void RefreshDishes(AdventureSession a)
        {
            _dishes = a.FeastDishes;
            bool any = _dishes.Count > 0;
            _dishesTitle.Value = any ? a.Text("text.ui.fete.dishes_title") + " · " + a.Text("text.ui.fete.eat_hint") : "";
            for (int i = 0; i < _dishCells.Count; i++)
            {
                bool used = i < _dishes.Count;
                if (used) _dishFestivals[i] = _dishes[i].Festival;
                else _dishFestivals[i] = null;
                UiKit.Show(_dishCells[i].Cell.Rect, used);
                UiKit.Show(_dishCells[i].Name.Rect, used);
                if (!used) continue;
                _dishCells[i].Cell.Icon.sprite = UiKit.Icon(_dishes[i].Icon);
                UiKit.Show(_dishCells[i].Cell.Icon, _dishCells[i].Cell.Icon.sprite != null);
                _dishCells[i].Cell.Count.Value = _dishes[i].Count > 1 ? _dishes[i].Count.ToString() : "";
                _dishCells[i].Cell.Edge.color = UiKit.Alpha(Color.white, 0.6f);
                _dishCells[i].Name.Value = _dishes[i].Name;
            }
        }
    }
}
