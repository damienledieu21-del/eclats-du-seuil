using System.Collections.Generic;
using EdS.Data.Items;
using EdS.Data.Saves;
using EdS.Runtime;
using EdS.World;
using UnityEngine;
using UnityEngine.UI;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V1.1 : la boutique d'un marchand dans le grand panneau de l'aventure, au style « Fragments du Seuil ». On l'ouvre en parlant
    /// au marchand (F). À gauche, ce qu'il vend aujourd'hui (prix, bouton Acheter, grisé avec la raison quand l'achat n'est pas possible) ;
    /// à droite, le sac (clic : vendre un objet, clic droit : toute la pile ; la fiche dit le prix de revente) et les lots à racheter au même
    /// prix ; en bas, la bourse et le bouton Parler. Tout passe par <see cref="AdventureSession"/> : un refus ne change rien et sa raison
    /// s'affiche dans les messages du jeu. Relecture de la PR #72 : au-delà de neuf offres (un marchand peut en avoir 32), des pages.
    /// </summary>
    public sealed partial class AdventureBook
    {
        private const int OfferRows = 9, BuybackCells = 12;

        /// <summary>Ce que la fiche d'objet montre en plus dans la boutique : rien, une offre, une place du sac, un lot à racheter.</summary>
        private enum ShopTip { None, Offer, Bag, Buyback }

        private readonly List<(RectTransform Row, Image Icon, UiText Name, UiText Price, UiButton Buy)> _offerRows =
            new List<(RectTransform, Image, UiText, UiText, UiButton)>();
        private readonly List<Cell> _shopBag = new List<Cell>();
        private readonly List<Cell> _shopBuyback = new List<Cell>();
        private IReadOnlyList<ShopOffer> _offers = new ShopOffer[0];
        private IReadOnlyList<ShopLot> _lots = new ShopLot[0];
        private UiText _shopPurse, _shopBagTitle, _shopBuybackEmpty, _shopNote;
        private UiButton _shopTalk;
        private Pager _offerPager;
        private string _offerShop;
        private ShopTip _shopTip;
        private int _shopTipIndex;

        /// <summary>Case de la boutique sous la souris (la fiche d'une autre page n'y reste pas, et inversement).</summary>
        private RectTransform _shopTipAnchor;

        private void BuildMerchant(RectTransform page)
        {
            Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 60f, 168f, 480f, 20f, 0f, 0.5f, 14f, true).Value = "En vente aujourd'hui";
            _offerPager = new Pager(page, OfferRows, 620f, 168f, 30f);
            for (int i = 0; i < OfferRows; i++)
            {
                int index = i;
                RectTransform row = UiKit.Node(page, "Offre");
                UiKit.Place(row, new Vector2(60f, 192f + i * 66f), new Vector2(640f, 58f), Vector2.zero);
                Image back = UiKit.Image(row, "Fond", UiKit.Sprite("bouton_sobre", 12f), new Color(1f, 1f, 1f, 0.55f), Image.Type.Sliced);
                UiKit.Fill(back.rectTransform);
                UiPointer pointer = UiKit.Pointer(back);
                pointer.Enter = () => ShowShopTip(ShopTip.Offer, _offerPager.First + index, row);
                pointer.Exit = () => HideTip(row);
                Image icon = UiKit.Image(row, "Icône", null, Color.white);
                UiKit.Place(icon.rectTransform, new Vector2(8f, 5f), new Vector2(48f, 48f), Vector2.zero);
                UiText name = Text(row, UiFace.Bold, 19f, UiKit.Ink, TextAnchor.MiddleLeft, 68f, 29f, 300f, 26f, 0f, 0.5f);
                UiText price = Text(row, UiFace.Bold, 17f, UiKit.Dawn, TextAnchor.MiddleRight, 470f, 29f, 220f, 24f, 1f, 0.5f);
                UiButton buy = UiKit.Button(row, "Acheter", "bouton", "", UiFace.Bold, 15f, new Color(0.1f, 0.05f, 0.02f), () => BuyOffer(index));
                UiKit.Place(buy.Rect, new Vector2(632f, 29f), new Vector2(150f, 40f), new Vector2(1f, 0.5f));
                _offerRows.Add((row, icon, name, price, buy));
            }

            _shopBagTitle = Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 740f, 168f, 700f, 20f, 0f, 0.5f, 14f, true);
            for (int i = 0; i < 24; i++)
            {
                int place = i;
                Cell cell = MakeCell(page, 740f + (i % 8) * 88f, 192f + (i / 8) * 88f, 80f, false);
                UiPointer pointer = UiKit.Pointer(cell.Slab);
                pointer.Left = () => SellPlace(place, false);
                pointer.Right = () => SellPlace(place, true);
                pointer.Enter = () => ShowShopTip(ShopTip.Bag, place, cell.Rect);
                pointer.Exit = () => HideTip(cell.Rect);
                _shopBag.Add(cell);
            }

            Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 740f, 470f, 700f, 20f, 0f, 0.5f, 14f, true).Value = "Rachat";
            _shopBuybackEmpty = Text(page, UiFace.Body, 16f, UiKit.Muted, TextAnchor.MiddleLeft, 740f, 496f, 700f, 22f, 0f, 0.5f);
            for (int i = 0; i < BuybackCells; i++)
            {
                int index = i;
                Cell cell = MakeCell(page, 740f + (i % 6) * 88f, 516f + (i / 6) * 88f, 80f, false);
                UiPointer pointer = UiKit.Pointer(cell.Slab);
                pointer.Left = () =>
                {
                    if (_session != null && index < _lots.Count) _session.BuyBack(_lots[index].Index);
                };
                pointer.Enter = () => ShowShopTip(ShopTip.Buyback, index, cell.Rect);
                pointer.Exit = () => HideTip(cell.Rect);
                _shopBuyback.Add(cell);
            }

            Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 60f, 806f, 200f, 20f, 0f, 0.5f, 14f, true).Value = "Bourse";
            _shopPurse = Text(page, UiFace.Title, 24f, UiKit.Dawn, TextAnchor.MiddleLeft, 60f, 834f, 640f, 30f, 0f, 0.5f, 2f);
            _shopNote = Text(page, UiFace.Body, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 740f, 712f, 700f, 40f, 0f, 0.5f);
            _shopNote.Wrap();
            _shopTalk = UiKit.Button(page, "Parler", "bouton_sobre", "", UiFace.Bold, 16f, UiKit.Ink, () => _session?.TalkToShopkeeper());
            UiKit.Place(_shopTalk.Rect, new Vector2(1440f, 834f), new Vector2(200f, 44f), new Vector2(1f, 0.5f));
            BuildShopRepair(page);   // sous-lot V1.4 : réparer chez l'armurier (AdventureBook.Forge.cs)
        }

        /// <summary>Bouton Acheter de la ligne <paramref name="row"/> de la page affichée.</summary>
        private void BuyOffer(int row)
        {
            int index = _offerPager.First + row;
            if (_session != null && index < _offers.Count) _session.Buy(_offers[index].Item);
        }

        /// <summary>Clic sur une place du sac : vend un objet ; clic droit : toute la pile.</summary>
        private void SellPlace(int place, bool all)
        {
            if (_session == null || place >= _session.World.Inventory.Stacks.Count) return;
            _session.SellPlace(place, all ? (int?)null : 1);
        }

        private void ShowShopTip(ShopTip kind, int index, RectTransform anchor)
        {
            string item = null;
            if (_session != null)
            {
                if (kind == ShopTip.Offer && index < _offers.Count) item = _offers[index].Item;
                else if (kind == ShopTip.Bag && index < _session.World.Inventory.Stacks.Count) item = _session.World.Inventory.Stacks[index].Item;
                else if (kind == ShopTip.Buyback && index < _lots.Count) item = _lots[index].Item;
            }
            _shopTip = item == null ? ShopTip.None : kind;
            _shopTipIndex = index;
            _shopTipAnchor = anchor;
            ShowTip(item, anchor, false);
        }

        /// <summary>Une image de la boutique ouverte : titre, offres du jour, sac, rachat et bourse.</summary>
        private void RefreshMerchant(AdventureSession a)
        {
            if (a.Shop == null) return;
            if (_tipAnchor != null && _tipAnchor != _shopTipAnchor) ClearTip();   // fiche restée d'une autre page
            _title.Value = a.ShopName;
            _subtitle.Value = a.ShopGreeting;
            _offers = a.ShopOffers();
            if (_offerShop != a.Shop)
            {
                _offerShop = a.Shop;
                _offerPager.Reset();   // une autre boutique s'ouvre à sa première page
            }
            _offerPager.Refresh(_offers.Count);
            for (int i = 0; i < _offerRows.Count; i++)
            {
                var row = _offerRows[i];
                int index = _offerPager.First + i;
                bool used = index < _offers.Count;
                UiKit.Show(row.Row, used);
                if (!used) continue;
                ShopOffer o = _offers[index];
                ItemDefinition item = a.Item(o.Item);
                row.Icon.sprite = UiKit.Icon(item.Icon);
                UiKit.Show(row.Icon, row.Icon.sprite != null);
                row.Name.Value = o.Name;
                row.Name.Color = RarityColor(item.Rarity);
                // Sous-lot V1.7 : une offre de la réserve de la guilde réservée à un rang plus haut le dit à la place du prix.
                bool rank = o.Refusal == TradeRefusal.RankTooLow && o.Rank != null;
                row.Price.Value = rank ? string.Format(a.Text("text.ui.shop.rank"), o.Rank) : o.PriceText;
                row.Price.Color = o.Refusal == TradeRefusal.NotEnoughMoney || rank ? UiKit.Danger : UiKit.Dawn;
                row.Buy.Text.Value = a.Text("text.ui.shop.buy");
                row.Buy.Enable(o.Refusal == TradeRefusal.None);
            }

            IReadOnlyList<ItemStack> stacks = a.World.Inventory.Stacks;
            int size = a.World.Inventory.Catalog.InventorySize;
            _shopBagTitle.Value = a.Text("text.ui.shop.bag") + $" · {stacks.Count} / {size}";
            for (int i = 0; i < _shopBag.Count; i++)
            {
                Cell cell = _shopBag[i];
                UiKit.Show(cell.Rect, i < size);
                bool used = i < stacks.Count;
                ItemDefinition item = used ? a.Item(stacks[i].Item) : null;
                cell.Icon.sprite = item != null ? UiKit.Icon(item.Icon) : null;
                UiKit.Show(cell.Icon, cell.Icon.sprite != null);
                cell.Count.Value = used && stacks[i].Count > 1 ? stacks[i].Count.ToString() : "";
                bool sells = item != null && a.SellPrice(item.Id) > 0;
                cell.Edge.color = item == null ? UiKit.Alpha(Color.white, 0.35f) : sells ? RarityColor(item.Rarity) : UiKit.Alpha(UiKit.Muted, 0.6f);
                // Un objet qui ne se vend pas (objet de quête), ou réservé pour le combat, paraît éteint.
                cell.Icon.color = item != null && (!sells || a.World.Inventory.Available(item.Id) < 1) ? new Color(0.5f, 0.5f, 0.55f) : Color.white;
            }

            _lots = a.Buyback();
            _shopBuybackEmpty.Value = _lots.Count == 0 ? a.Text("text.ui.shop.empty") : a.Text("text.ui.shop.buyback_hint");
            for (int i = 0; i < _shopBuyback.Count; i++)
            {
                Cell cell = _shopBuyback[i];
                bool used = i < _lots.Count;
                UiKit.Show(cell.Rect, used);
                if (!used) continue;
                ShopLot lot = _lots[i];
                ItemDefinition item = a.Item(lot.Item);
                cell.Icon.sprite = UiKit.Icon(item.Icon);
                UiKit.Show(cell.Icon, cell.Icon.sprite != null);
                cell.Count.Value = lot.Count > 1 ? lot.Count.ToString() : "";
                cell.Edge.color = a.Money >= lot.Paid ? RarityColor(item.Rarity) : UiKit.Danger;
                cell.Icon.color = Color.white;
            }

            _shopPurse.Value = a.Purse;
            _shopNote.Value = a.Text("text.ui.shop.bag_hint");
            _shopTalk.Text.Value = a.Text("text.ui.shop.talk");
            RefreshRepairButton(a, _shopRepair, true);
            RefreshShopTip(a);
        }

        /// <summary>La fiche d'objet de la boutique : celle de l'inventaire, avec le prix (achat, revente ou rachat) et l'action du clic.</summary>
        private void RefreshShopTip(AdventureSession a)
        {
            if (_tipItem == null || _tipAnchor == null || _shopTip == ShopTip.None) return;
            RefreshTip(a);
            string price = null, action = "";
            switch (_shopTip)
            {
                case ShopTip.Offer:
                    if (_shopTipIndex < _offers.Count) price = "Prix : " + _offers[_shopTipIndex].PriceText;
                    action = "Bouton : acheter";
                    break;
                case ShopTip.Bag:
                    long unit = a.SellPrice(_tipItem);
                    price = unit > 0 ? string.Format(a.Text("text.ui.shop.sell_price"), a.MoneyText(unit)) : a.Text("text.ui.shop.no_value");
                    action = unit > 0 ? "Clic : vendre un · clic droit : la pile" : "";
                    break;
                case ShopTip.Buyback:
                    if (_shopTipIndex < _lots.Count) price = $"Rachat ×{_lots[_shopTipIndex].Count} : {_lots[_shopTipIndex].PaidText}";
                    action = "Clic : racheter";
                    break;
            }
            if (price != null) _tipLines.Value = string.IsNullOrEmpty(_tipLines.Value) ? price : _tipLines.Value + "\n" + price;
            _tipAction.Value = action;
        }

        // --- Bourse dans l'écran du personnage ------------------------------------------------------------------------------------

        private UiText _purse;

        /// <summary>Sous-lot V1.1 : la bourse, à droite du titre du sac (écran du personnage).</summary>
        private void BuildPurse(RectTransform page)
        {
            _purse = Text(page, UiFace.Bold, 17f, UiKit.Dawn, TextAnchor.MiddleRight, 1442f, 168f, 520f, 22f, 1f, 0.5f, 4f);
        }

        private void RefreshPurse(AdventureSession a)
        {
            _purse.Value = a.Text("text.ui.purse") + " · " + a.Purse;
            if (_tipAnchor != null && _tipAnchor == _shopTipAnchor) ClearTip();   // fiche restée de la boutique
        }

        private void ClearTip()
        {
            _tipItem = null;
            _tipAnchor = null;
            _shopTipAnchor = null;
            _shopTip = ShopTip.None;
            UiKit.Show(_tip, false);
        }

        /// <summary>
        /// Relecture de la PR #72 : pages d'une liste plus longue que ses lignes (offres d'un marchand, points sûrs du voyage rapide) : boutons
        /// précédente et suivante autour de « 2 / 4 », montrés seulement quand il y a plus d'une page. À la souris, sans touche de plus.
        /// </summary>
        private sealed class Pager
        {
            private readonly int _rows;
            private readonly UiButton _previous, _next;
            private readonly UiText _label;
            private int _pages = 1;

            /// <summary>Page affichée (0 : la première).</summary>
            public int Page { get; private set; }

            /// <summary>Rang, dans la liste, de la première ligne de la page affichée.</summary>
            public int First => Page * _rows;

            /// <summary>Contrôles centrés en (<paramref name="x"/>, <paramref name="y"/>), boutons de hauteur <paramref name="height"/>.</summary>
            public Pager(Transform parent, int rows, float x, float y, float height)
            {
                _rows = rows;
                _previous = UiKit.Button(parent, "Page précédente", "bouton_sobre", "<", UiFace.Bold, 16f, UiKit.Ink, () => Turn(-1));
                UiKit.Place(_previous.Rect, new Vector2(x - 36f, y), new Vector2(44f, height), new Vector2(1f, 0.5f));
                _label = Text(parent, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleCenter, x, y, 64f, height, 0.5f, 0.5f);
                _next = UiKit.Button(parent, "Page suivante", "bouton_sobre", ">", UiFace.Bold, 16f, UiKit.Ink, () => Turn(1));
                UiKit.Place(_next.Rect, new Vector2(x + 36f, y), new Vector2(44f, height), new Vector2(0f, 0.5f));
            }

            private void Turn(int by)
            {
                int page = Page + by;
                if (page >= 0 && page < _pages) Page = page;
            }

            public void Reset() => Page = 0;

            /// <summary>Ajuste la page à une liste de <paramref name="count"/> entrées (la dernière si la liste a raccourci).</summary>
            public void Refresh(int count)
            {
                _pages = count <= 0 ? 1 : (count + _rows - 1) / _rows;
                if (Page >= _pages) Page = _pages - 1;
                bool shown = _pages > 1;
                UiKit.Show(_previous.Rect, shown);
                UiKit.Show(_next.Rect, shown);
                UiKit.Show(_label.Rect, shown);
                _label.Value = (Page + 1) + " / " + _pages;
                _previous.Enable(Page > 0);
                _next.Enable(Page + 1 < _pages);
            }
        }
    }
}
