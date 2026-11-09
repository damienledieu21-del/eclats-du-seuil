using System;
using System.Globalization;
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

        /// <summary>Sous-lot V1.7 : rang de la guilde que demande l'offre, traduit (« Rang F »), ou null.</summary>
        public string? Rank { get; }

        /// <summary>Sous-lot V2.10 : unités en vente aujourd'hui (null : sans limite).</summary>
        public int? Left { get; }

        /// <summary>Sous-lot V2.10 : ce qu'en dit la boutique (« 3 en stock », « revendu par toi »), ou null.</summary>
        public string? StockText { get; }

        internal ShopOffer(string item, string name, long price, string priceText, TradeRefusal refusal, string? rank = null, int? left = null, string? stockText = null)
        {
            Left = left;
            StockText = stockText;
            Item = item;
            Name = name;
            Price = price;
            PriceText = priceText;
            Refusal = refusal;
            Rank = rank;
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

        /// <summary>
        /// Offres du jour de la boutique ouverte, avec le refus qu'aurait l'achat d'une unité maintenant ; vide sans boutique. Relecture
        /// indépendante de la PR #74 : une boutique devenue fermée (ou trop loin) le dit sur chaque offre, comme <see cref="WorldSession.Buy"/>.
        /// </summary>
        public IReadOnlyList<ShopOffer> ShopOffers()
        {
            if (!(Shop is string id)) return Array.Empty<ShopOffer>();
            var list = new List<ShopOffer>();
            TradeRefusal shop = World.CanTrade(id);
            foreach (MerchantOffer o in World.Stock(id))
            {
                TradeRefusal refusal = shop != TradeRefusal.None ? shop
                    : !World.HasRank(o.Rank) ? TradeRefusal.RankTooLow   // sous-lot V1.7 : offre réservée à un rang de la guilde
                    : o.Left is int none && none < 1 ? TradeRefusal.OutOfStock   // sous-lot V2.10 : rupture
                    : World.Money < o.Price ? TradeRefusal.NotEnoughMoney
                    : !World.Inventory.CanAdd(o.Item, 1) ? TradeRefusal.InventoryFull : TradeRefusal.None;
                string? stock = o.Left is int left ? string.Format(CultureInfo.InvariantCulture, Text("text.ui.trade.left"), left) : null;
                if (o.Resold) stock = (stock is null ? "" : stock + " · ") + Text("text.ui.trade.resold");
                list.Add(new ShopOffer(o.Item, ItemName(o.Item), o.Price, MoneyText(o.Price), refusal, o.Rank is null ? null : RankName(o.Rank), o.Left, stock));
            }
            return list;
        }

        /// <summary>Pièces que paie la boutique ouverte pour une unité de <paramref name="item"/> (0 : il ne se vend pas, ou pas de boutique).</summary>
        public long SellPrice(string item) => Shop is string id ? World.SellUnit(id, item) : 0;   // sous-lot V2.7 : selon la réputation

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
        /// objets de soin emportés au combat, <see cref="Inventory.Reserved"/>) ; refus seulement si elle est nulle. Relecture de la PR #78 : la
        /// vente retire toujours de <b>cette place</b> (<see cref="WorldSession.SellPlace"/>), jamais d'une autre place du même objet (une
        /// instance aux propriétés tirées ne part pas à la place d'une copie de base) ; tout ou rien.
        /// </summary>
        public TradeRefusal SellPlace(int place, int? count = null)
        {
            if (place < 0 || place >= World.Inventory.Stacks.Count) return Refused(TradeRefusal.NotEnough);
            ItemStack stack = World.Inventory.Stacks[place];
            if (!(Shop is string shop)) return Refused(TradeRefusal.TooFar);
            int wanted = count ?? stack.Count;
            int sold = wanted;
            if (!stack.IsUnique && wanted >= 1)
            {
                // Pile de base : au plus la pile, et seulement la part vendable ; rien de vendable (tout est emporté au combat) : le monde
                // dit pourquoi, sans rien changer. Une quantité impossible (< 1) garde son motif, venu du monde.
                int sellable = Math.Min(Math.Min(wanted, stack.Count), World.Inventory.Available(stack.Item));
                sold = sellable > 0 ? sellable : Math.Min(wanted, stack.Count);
            }
            long before = World.Money;
            TradeRefusal refusal = World.SellPlace(shop, place, sold);
            if (refusal != TradeRefusal.None) return Refused(refusal);
            _dirty = true;
            Say("text.ui.trade.sold", ItemName(stack.Item), sold, MoneyText(World.Money - before));
            return refusal;
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

        // --- Sous-lot V2.14 : vraies boutiques et grand jour de marché ----------------------------------------------------

        /// <summary>Heure du monde en mots (« 6 h », « 19 h 30 »).</summary>
        public static string HourText(int minute) =>
            minute % 60 == 0 ? string.Format(CultureInfo.InvariantCulture, "{0} h", minute / 60)
                : string.Format(CultureInfo.InvariantCulture, "{0} h {1:00}", minute / 60, minute % 60);

        /// <summary>Nom du jour de la semaine <paramref name="weekday"/> (0 : lundi … 6 : dimanche).</summary>
        public string WeekdayName(int weekday) => Text("text.v214.weekday." + weekday.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// Sous-lot V2.14 : enseigne et horaires d'une boutique (« Boulangerie de Firmin · ouverte de 5 h 30 à 19 h 30 · fermée le lundi ») ;
        /// null pour un étal sans horaires.
        /// </summary>
        public string? ShopHoursText(string merchant)
        {
            if (!(_market.Book.Merchant(merchant).Shop is EdS.Data.Merchants.Shop shop)) return null;
            string text = Text(shop.Sign) + " · " + string.Format(CultureInfo.InvariantCulture, Text("text.v214.shop.hours"), HourText(shop.Open), HourText(shop.Close));
            if (shop.Rest.Count > 0)
                text += " · " + string.Format(CultureInfo.InvariantCulture, Text("text.v214.shop.rest"), string.Join(", ", shop.Rest.Select(WeekdayName)));
            return text;
        }

        /// <summary>
        /// Sous-lot V2.14 : pourquoi la boutique de <paramref name="merchant"/> est fermée et quand elle rouvre (« Fermée : jour de repos.
        /// Réouverture mardi à 5 h 30. ») ; null si elle est ouverte ou sans horaires.
        /// </summary>
        public string? ClosedText(string merchant)
        {
            ShopState state = World.ShopStateOf(merchant);
            if (state == ShopState.Open || state == ShopState.AtDoor || _market.Book.Merchant(merchant).Shop is null) return null;
            string text = Text(state == ShopState.RestDay ? "text.v214.shop.closed_rest" : "text.v214.shop.closed_hours");
            if (World.NextOpening(merchant) is (long day, int minute))
                text += " " + string.Format(CultureInfo.InvariantCulture, Text(day == World.Zone.Time.Day ? "text.v214.shop.reopens_today" : "text.v214.shop.reopens"),
                    WeekdayName(EdS.Data.Story.WorldCalendar.WeekdayOf(day)), HourText(minute));
            return text;
        }

        /// <summary>
        /// Sous-lot V2.14 : bandeau de la boutique ouverte : enseigne et horaires, puis l'instant (à la porte, pains chauds, prochaine
        /// fournée, rupture du soir, marchandage accepté) ; null sans boutique.
        /// </summary>
        public string? ShopNotice
        {
            get
            {
                if (!(OpenMerchant is Merchant m)) return null;
                var parts = new List<string>();
                if (ShopHoursText(m.Id) is string hours) parts.Add(hours);
                if (World.AtDoor(m.Id) && m.Shop is EdS.Data.Merchants.Shop shop)
                    parts.Add(shop.Door == DoorService.Clerk
                        ? string.Format(CultureInfo.InvariantCulture, Text("text.v214.shop.door_clerk"), Text(shop.Clerk!))
                        : string.Format(CultureInfo.InvariantCulture, Text("text.v214.shop.door_patron"), Text(_story.Npc(m.Npc).Name)));
                if (m.Bakes.Count > 0)
                {
                    int minute = World.Zone.Time.Minute;
                    if (World.WarmBread(m.Id)) parts.Add(Text("text.v214.bakery.warm"));
                    else if (minute < m.Bakes[0].At) parts.Add(string.Format(CultureInfo.InvariantCulture, Text("text.v214.bakery.first"), HourText(m.Bakes[0].At)));
                    else if (minute >= m.SoldOut) parts.Add(string.Format(CultureInfo.InvariantCulture, Text("text.v214.bakery.soldout"), HourText(m.Bakes[0].At)));
                    else if (World.NextBake(m.Id) is int next) parts.Add(string.Format(CultureInfo.InvariantCulture, Text("text.v214.bakery.next"), HourText(next)));
                }
                if (World.HagglePercent(m.Id) is int percent && percent != 100)
                    parts.Add(string.Format(CultureInfo.InvariantCulture, Text("text.v214.haggle.done"), 100 - percent));
                else if (World.HaggleChance(m.Id) > 0) parts.Add(Text("text.v214.haggle.can"));
                return parts.Count == 0 ? null : string.Join(" · ", parts);
            }
        }

        /// <summary>Sous-lot V2.14 : on peut marchander avec la boutique ouverte (étal du marché, marché ouvert, réputation suffisante, pas encore fait).</summary>
        public bool CanHaggle => Shop is string id && World.HaggleChance(id) > 0 && World.HagglePercent(id) == 100;

        /// <summary>Sous-lot V2.14 : marchande avec la boutique ouverte ; un message dit la réponse.</summary>
        public HaggleRefusal Haggle()
        {
            if (!(Shop is string id)) return HaggleRefusal.Closed;
            HaggleRefusal answer = World.Haggle(id);
            if (answer == HaggleRefusal.None) Say("text.v214.haggle.accepted", Text(_story.Npc(_market.Book.Merchant(id).Npc).Name), 100 - World.HagglePercent(id));
            else Say("text.v214.haggle." + answer.ToString().ToLowerInvariant());
            return answer;
        }

        /// <summary>
        /// Sous-lot V2.14 : où en est la grande place (« Jour de marché : les étals s'installent », « Grand marché sur la place jusqu'à
        /// 17 h », « On démonte les étals ») ; hors marché, le prochain jour de marché ; null sans marché.
        /// </summary>
        public string? MarketNotice
        {
            get
            {
                if (!(World.MarketDay is MarketDay md)) return null;
                EdS.Data.Story.Festival? today = World.MarketFestivalToday;
                switch (World.MarketPhase)
                {
                    case MarketPhase.Setup: return string.Format(CultureInfo.InvariantCulture, Text("text.v214.market.setup"), HourText(today!.From));
                    case MarketPhase.Open: return string.Format(CultureInfo.InvariantCulture, Text("text.v214.market.open"), HourText(Math.Min(today!.To, 1439)));
                    case MarketPhase.Teardown: return Text("text.v214.market.teardown");
                }
                if (World.NextMarketDay() is (long day, EdS.Data.Story.Festival f) && day > World.Zone.Time.Day)
                    return string.Format(CultureInfo.InvariantCulture, Text("text.v214.market.next"), WeekdayName(EdS.Data.Story.WorldCalendar.WeekdayOf(day)), HourText(f.From));
                return null;
            }
        }

        /// <summary>Sous-lot V2.14 : criée du moment sur la place (traduite), ou null.</summary>
        public string? MarketCry => World.MarketCry is string key ? Text(key) : null;

        private TradeRefusal Refused(TradeRefusal refusal)
        {
            Say("text.ui.trade." + refusal.ToString().ToLowerInvariant());
            return refusal;
        }
    }
}
