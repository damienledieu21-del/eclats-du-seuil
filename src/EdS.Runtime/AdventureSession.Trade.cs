using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Items;
using EdS.Data.Merchants;
using EdS.Data.Saves;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V1.1 : un objet en vente aujourd'hui, tel que la boutique le montre.</summary>
    public sealed class ShopOffer
    {
        public string Item { get; }
        public string Name { get; }

        /// <summary>Prix d'une unité, en cuivre, et son texte (« 1 argent 20 cuivre »).</summary>
        public long Price { get; }

        public string PriceText { get; }

        /// <summary>Refus qu'aurait l'achat d'une unité maintenant (<see cref="TradeRefusal.None"/> : possible) ; rien ne change.</summary>
        public TradeRefusal Refusal { get; }

        internal ShopOffer(string item, string name, long price, string priceText, TradeRefusal refusal)
        {
            Item = item;
            Name = name;
            Price = price;
            PriceText = priceText;
            Refusal = refusal;
        }
    }

    /// <summary>Sous-lot V1.1 : un lot vendu, rachetable au même prix, tel que la boutique le montre.</summary>
    public sealed class ShopLot
    {
        /// <summary>Rang du lot pour <see cref="AdventureSession.BuyBack"/> (0 : le plus récent).</summary>
        public int Index { get; }

        public string Item { get; }
        public string Name { get; }
        public int Count { get; }
        public long Paid { get; }
        public string PaidText { get; }

        internal ShopLot(int index, string item, string name, int count, long paid, string paidText)
        {
            Index = index;
            Item = item;
            Name = name;
            Count = count;
            Paid = paid;
            PaidText = paidText;
        }
    }

    /// <summary>
    /// Sous-lot V1.1 : la bourse et les boutiques dans l'aventure. Parler à un marchand (F) ouvre sa boutique (<see cref="Shop"/>) ; achat,
    /// vente et rachat passent par la session (<see cref="WorldSession.Buy"/>…), seule juge : un refus ne change rien et son motif s'affiche
    /// (textes <c>text.ui.trade.…</c>). Les lots vendus restent rachetables d'une session de zone à l'autre (halte, porte, voyage), jusqu'à
    /// ce qu'on quitte le jeu.
    /// </summary>
    public sealed partial class AdventureSession
    {
        private readonly Market _market;

        /// <summary>Textes qui font sonner les pièces (comme un butin).</summary>
        private static readonly HashSet<string> CoinKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "text.ui.money_given", "text.ui.trade.sold", "text.ui.trade.bought", "text.ui.trade.bought_back",
        };

        /// <summary>Marchands, stock du jour et lots rachetables.</summary>
        public Market Market => _market;

        /// <summary>Bourse du personnage, en cuivre.</summary>
        public long Money => World.Money;

        /// <summary>Texte d'une somme (« 3 or 45 argent 12 cuivre »), avec les noms des pièces des textes du monde.</summary>
        public string MoneyText(long copper) =>
            Coins.Format(copper, Text("text.ui.money.gold"), Text("text.ui.money.silver"), Text("text.ui.money.copper"));

        /// <summary>Texte de la bourse du personnage.</summary>
        public string Purse => MoneyText(World.Money);

        /// <summary>Boutique ouverte (identifiant du marchand), ou null.</summary>
        public string? Shop { get; private set; }

        /// <summary>Le PNJ <paramref name="npc"/> tient une boutique.</summary>
        public bool IsMerchant(string npc) => World.MerchantOf(npc) != null;

        /// <summary>
        /// Ouvre la boutique de <paramref name="merchant"/> si elle l'est pour le personnage (à portée de son PNJ, pas fermée) ; sinon le
        /// motif s'affiche et rien ne change.
        /// </summary>
        public bool OpenShop(string merchant)
        {
            TradeRefusal refusal = World.CanTrade(merchant);
            if (refusal != TradeRefusal.None)
            {
                Refused(refusal);
                return false;
            }
            CloseDialogue();
            Shop = merchant;
            return true;
        }

        public void CloseShop() => Shop = null;

        private Merchant? OpenMerchant => Shop is string id ? _market.Book.Merchant(id) : null;

        /// <summary>Nom du marchand de la boutique ouverte (traduit), ou null.</summary>
        public string? ShopName => OpenMerchant is Merchant m ? Text(_story.Npc(m.Npc).Name) : null;

        /// <summary>Accueil de la boutique ouverte (traduit), ou null.</summary>
        public string? ShopGreeting => OpenMerchant is Merchant m ? Text(m.Greeting) : null;

        /// <summary>Offres du jour de la boutique ouverte, avec le refus qu'aurait l'achat d'une unité maintenant ; vide sans boutique.</summary>
        public IReadOnlyList<ShopOffer> ShopOffers()
        {
            if (!(Shop is string id)) return Array.Empty<ShopOffer>();
            var list = new List<ShopOffer>();
            foreach (MerchantOffer o in World.Stock(id))
            {
                TradeRefusal refusal = World.Money < o.Price ? TradeRefusal.NotEnoughMoney
                    : !World.Inventory.CanAdd(o.Item, 1) ? TradeRefusal.InventoryFull : TradeRefusal.None;
                list.Add(new ShopOffer(o.Item, ItemName(o.Item), o.Price, MoneyText(o.Price), refusal));
            }
            return list;
        }

        /// <summary>Pièces que paie la boutique ouverte pour une unité de <paramref name="item"/> (0 : il ne se vend pas, ou pas de boutique).</summary>
        public long SellPrice(string item) => Shop is string id ? _market.UnitPrice(id, item) : 0;

        /// <summary>Lots vendus, rachetables au même prix (le plus récent d'abord).</summary>
        public IReadOnlyList<ShopLot> Buyback() =>
            _market.Buyback.Select((lot, i) => new ShopLot(i, lot.Item, ItemName(lot.Item), lot.Count, lot.Paid, MoneyText(lot.Paid))).ToList();

        /// <summary>Achète <paramref name="count"/> <paramref name="item"/> à la boutique ouverte ; un message dit le résultat ou le refus.</summary>
        public TradeRefusal Buy(string item, int count = 1)
        {
            if (!(Shop is string id)) return Refused(TradeRefusal.TooFar);
            long before = World.Money;
            TradeRefusal refusal = World.Buy(id, item, count);
            if (refusal != TradeRefusal.None) return Refused(refusal);
            _dirty = true;
            Say("text.ui.trade.bought", ItemName(item), count, MoneyText(before - World.Money));
            return refusal;
        }

        /// <summary>
        /// Vend à la boutique ouverte la pile de la place <paramref name="place"/> du sac, ou <paramref name="count"/> de ses objets (null :
        /// toute la pile) ; un message dit le résultat ou le refus. Relecture de la PR #72 : seule la part vendable de cet objet part (hors
        /// objets de soin emportés au combat, <see cref="Inventory.Reserved"/>) ; refus seulement si elle est nulle. Les objets d'un même
        /// identifiant sont identiques : la vente en retire le nombre exact (des dernières places, comme tout retrait), tout ou rien.
        /// </summary>
        public TradeRefusal SellPlace(int place, int? count = null)
        {
            if (place < 0 || place >= World.Inventory.Stacks.Count) return Refused(TradeRefusal.NotEnough);
            ItemStack stack = World.Inventory.Stacks[place];
            int wanted = count ?? stack.Count;
            if (wanted < 1) return Sell(stack.Item, wanted);   // quantité impossible : le motif vient du monde
            int sellable = Math.Min(Math.Min(wanted, stack.Count), World.Inventory.Available(stack.Item));
            // Rien de vendable (toute la quantité est emportée au combat) : le monde dit pourquoi, sans rien changer.
            return Sell(stack.Item, sellable > 0 ? sellable : Math.Min(wanted, stack.Count));
        }

        /// <summary>Vend <paramref name="count"/> <paramref name="item"/> à la boutique ouverte ; un message dit le résultat ou le refus.</summary>
        public TradeRefusal Sell(string item, int count = 1)
        {
            if (!(Shop is string id)) return Refused(TradeRefusal.TooFar);
            long before = World.Money;
            TradeRefusal refusal = World.Sell(id, item, count);
            if (refusal != TradeRefusal.None) return Refused(refusal);
            _dirty = true;
            Say("text.ui.trade.sold", ItemName(item), count, MoneyText(World.Money - before));
            return refusal;
        }

        /// <summary>Rachète le lot de rang <paramref name="index"/> à la boutique ouverte ; un message dit le résultat ou le refus.</summary>
        public TradeRefusal BuyBack(int index)
        {
            if (!(Shop is string id)) return Refused(TradeRefusal.TooFar);
            SoldLot? lot = index >= 0 && index < _market.Buyback.Count ? _market.Buyback[index] : null;
            TradeRefusal refusal = World.BuyBack(id, index);
            if (refusal != TradeRefusal.None) return Refused(refusal);
            _dirty = true;
            Say("text.ui.trade.bought_back", ItemName(lot!.Item), lot.Count, MoneyText(lot.Paid));
            return refusal;
        }

        /// <summary>Ferme la boutique et parle à son marchand (son dialogue de l'histoire) ; faux s'il n'a rien à dire.</summary>
        public bool TalkToShopkeeper()
        {
            if (!(OpenMerchant is Merchant m)) return false;
            Shop = null;
            DialogueRefusal refusal = World.Talk(m.Npc, out _);
            if (refusal == DialogueRefusal.NothingToSay) Say("text.ui.nothing_to_say", Text(_story.Npc(m.Npc).Name));
            else if (refusal == DialogueRefusal.TooFar) Say("text.ui.too_far");
            return refusal == DialogueRefusal.None;
        }

        private TradeRefusal Refused(TradeRefusal refusal)
        {
            Say("text.ui.trade." + refusal.ToString().ToLowerInvariant());
            return refusal;
        }
    }
}
