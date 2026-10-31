using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Items;
using EdS.Data.Merchants;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.World
{
    /// <summary>
    /// Sous-lot V1.1 : la bourse du personnage et le commerce. Achat, vente et rachat sont atomiques : la bourse et l'inventaire changent
    /// ensemble, ou rien ne change et le motif est rendu (<see cref="TradeRefusal"/>). Toute somme est vérifiée (<see cref="Coins.TryAdd"/>,
    /// arithmétique <c>checked</c>) : un dépassement est refusé, jamais tronqué.
    /// </summary>
    public sealed partial class WorldSession
    {
        /// <summary>Sous-lot V1.1 : marchands, stock du jour et lots rachetables (null : partie sans commerce).</summary>
        public Market? Market { get; }

        /// <summary>Bourse du personnage, en pièces de cuivre.</summary>
        public long Money => _character.Money;

        long INarrativeWorld.Money => Money;

        bool INarrativeWorld.GiveMoney(long copper)
        {
            if (copper < 0 || !Coins.TryAdd(_character.Money, copper, out long after)) return false;
            _character = _character.WithMoney(after);
            return true;
        }

        bool INarrativeWorld.TakeMoney(long copper)
        {
            if (copper < 0 || copper > _character.Money) return false;
            _character = _character.WithMoney(checked(_character.Money - copper));
            return true;
        }

        private Market NeedMarket() => Market ?? throw new InvalidOperationException("Partie sans commerce.");

        /// <summary>Marchand tenu par le PNJ <paramref name="npc"/>, ou null (ou sans commerce).</summary>
        public Merchant? MerchantOf(string npc) => Market?.Book.OfNpc(npc);

        /// <summary>Offres du marchand ce jour du monde (<see cref="Market.Stock"/>).</summary>
        /// <exception cref="ArgumentException">Marchand inconnu.</exception>
        public IReadOnlyList<MerchantOffer> Stock(string merchant) => NeedMarket().Stock(merchant, Zone.Time.Day);

        /// <summary>
        /// La boutique de <paramref name="merchant"/> est ouverte au personnage : présent et vivant, dans la zone de l'histoire, à
        /// <see cref="Narrative.TalkRange"/> de son PNJ (sinon <see cref="TradeRefusal.TooFar"/>), et pas à un moment de fermeture
        /// (<see cref="TradeRefusal.Closed"/>).
        /// </summary>
        /// <exception cref="ArgumentException">Marchand inconnu.</exception>
        public TradeRefusal CanTrade(string merchant)
        {
            Merchant m = NeedMarket().Book.Merchant(merchant);
            Npc npc = (Narrative ?? throw new InvalidOperationException("Commerce sans histoire.")).Book.Npc(m.Npc);
            if (!(((INarrativeWorld)this).PlayerPosition is GroundVector p) || ZonePopulations.Beyond(p, Fixed.Zero, npc.Position, Fixed.Zero, Narrative.TalkRange))
                return TradeRefusal.TooFar;
            if (m.Closed.Contains(Narrative.PhaseId(Zone.Time.Phase))) return TradeRefusal.Closed;
            return TradeRefusal.None;
        }

        /// <summary>
        /// Achète <paramref name="count"/> <paramref name="item"/> à <paramref name="merchant"/>, au prix du jour : boutique ouverte, objet du
        /// catalogue en vente aujourd'hui, bourse suffisante, place dans l'inventaire ; tout ou rien.
        /// </summary>
        /// <exception cref="ArgumentException">Marchand inconnu.</exception>
        public TradeRefusal Buy(string merchant, string item, int count = 1)
        {
            TradeRefusal open = CanTrade(merchant);
            if (open != TradeRefusal.None) return open;
            if (count < 1 || count > Market.MaxCount) return TradeRefusal.InvalidCount;
            if (item is null || !_items.TryGetItem(item, out _)) return TradeRefusal.UnknownItem;
            if (!(Stock(merchant).FirstOrDefault(o => o.Item == item) is MerchantOffer offer)) return TradeRefusal.NotForSale;
            long price = checked(offer.Price * count);   // au plus 10^8 × 999
            if (!Coins.TryAdd(Money, -price, out long left)) return TradeRefusal.NotEnoughMoney;
            Inventory trial = Inventory.Clone();
            if (trial.Add(item, count) != InventoryRefusal.None) return TradeRefusal.InventoryFull;
            Inventory.CopyFrom(trial);
            _character = _character.WithMoney(left);
            return TradeRefusal.None;
        }

        /// <summary>Vend <paramref name="count"/> <paramref name="item"/> à <paramref name="merchant"/> (voir <see cref="Sell(string, IReadOnlyList{ValueTuple{string, int}})"/>).</summary>
        public TradeRefusal Sell(string merchant, string item, int count = 1) => Sell(merchant, new[] { (item, count) });

        /// <summary>
        /// Vend d'un coup plusieurs lots (objet, quantité) à <paramref name="merchant"/>, chacun à <see cref="Market.UnitPrice"/> par unité :
        /// boutique ouverte ; objets du catalogue et valués ; quantités disponibles dans l'inventaire (ni portées, ni réservées) ; bourse qui
        /// ne déborde pas. Tout ou rien ; vendus, les lots deviennent rachetables au même prix (<see cref="BuyBack"/>).
        /// </summary>
        /// <exception cref="ArgumentException">Marchand inconnu.</exception>
        public TradeRefusal Sell(string merchant, IReadOnlyList<(string Item, int Count)> lots)
        {
            if (lots is null) throw new ArgumentNullException(nameof(lots));
            TradeRefusal open = CanTrade(merchant);
            if (open != TradeRefusal.None) return open;
            Market market = NeedMarket();
            if (lots.Count < 1 || lots.Count > Market.MaxSellLots) return TradeRefusal.InvalidCount;
            var totals = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach ((string item, int count) in lots)
            {
                if (count < 1 || count > Market.MaxCount) return TradeRefusal.InvalidCount;
                if (item is null || !_items.TryGetItem(item, out _)) return TradeRefusal.UnknownItem;
                if (!market.Book.TryGetValue(item, out _)) return TradeRefusal.NoValue;
                totals[item] = checked((totals.TryGetValue(item, out long already) ? already : 0) + count);
            }
            foreach (KeyValuePair<string, long> t in totals)
                if (Inventory.Available(t.Key) < t.Value)
                {
                    if (Inventory.Count(t.Key) >= t.Value) return TradeRefusal.Reserved;
                    return Inventory.Equipped.Values.Contains(t.Key) ? TradeRefusal.Equipped : TradeRefusal.NotEnough;
                }
            var paid = new long[lots.Count];
            long gain = 0;
            for (int i = 0; i < lots.Count; i++)
            {
                paid[i] = checked(market.UnitPrice(merchant, lots[i].Item) * lots[i].Count);
                gain = checked(gain + paid[i]);
            }
            if (!Coins.TryAdd(Money, gain, out long after)) return TradeRefusal.PurseFull;
            Inventory trial = Inventory.Clone();
            var removed = new IReadOnlyList<ItemStack>[lots.Count];
            for (int i = 0; i < lots.Count; i++)
                if (trial.Remove(lots[i].Item, lots[i].Count, out removed[i]) != InventoryRefusal.None) return TradeRefusal.NotEnough;
            Inventory.CopyFrom(trial);
            _character = _character.WithMoney(after);
            // Sous-lot V1.3 : un lot qui emporte des instances aux propriétés tirées les garde pour le rachat.
            for (int i = 0; i < lots.Count; i++)
                market.Record(new SoldLot(lots[i].Item, lots[i].Count, paid[i], removed[i].Any(x => x.IsRolled) ? removed[i] : null));
            return TradeRefusal.None;
        }

        /// <summary>
        /// Sous-lot V1.3 : vend à <paramref name="merchant"/> la place <paramref name="place"/> de l'inventaire telle qu'elle est : une instance
        /// aux propriétés tirées part elle-même (pas une autre du même objet), au prix de l'objet ; une pile d'objets de base, comme
        /// <see cref="Sell(string, string, int)"/>. Mêmes refus, tout ou rien ; rachetable avec ses propriétés.
        /// </summary>
        /// <exception cref="ArgumentException">Marchand inconnu.</exception>
        public TradeRefusal SellPlace(string merchant, int place, int count)
        {
            if (place < 0 || place >= Inventory.Stacks.Count) return TradeRefusal.NotEnough;
            ItemStack stack = Inventory.Stacks[place];
            if (!stack.IsRolled) return Sell(merchant, stack.Item, count);
            TradeRefusal open = CanTrade(merchant);
            if (open != TradeRefusal.None) return open;
            Market market = NeedMarket();
            if (count != 1) return TradeRefusal.InvalidCount;
            if (!market.Book.TryGetValue(stack.Item, out _)) return TradeRefusal.NoValue;
            if (Inventory.Available(stack.Item) < 1) return TradeRefusal.Reserved;
            long paid = market.UnitPrice(merchant, stack.Item);
            if (!Coins.TryAdd(Money, paid, out long after)) return TradeRefusal.PurseFull;
            Inventory trial = Inventory.Clone();
            if (trial.RemoveAt(place, out ItemStack? removed) != InventoryRefusal.None || removed is null) return TradeRefusal.NotEnough;
            Inventory.CopyFrom(trial);
            _character = _character.WithMoney(after);
            market.Record(new SoldLot(stack.Item, 1, paid, new[] { removed }));
            return TradeRefusal.None;
        }

        /// <summary>
        /// Rachète le lot de rang <paramref name="index"/> (<see cref="Market.Buyback"/>, le plus récent d'abord) au prix reçu pour lui :
        /// boutique ouverte, bourse suffisante, place dans l'inventaire ; tout ou rien. Racheté, le lot quitte la liste.
        /// </summary>
        /// <exception cref="ArgumentException">Marchand inconnu.</exception>
        public TradeRefusal BuyBack(string merchant, int index)
        {
            TradeRefusal open = CanTrade(merchant);
            if (open != TradeRefusal.None) return open;
            Market market = NeedMarket();
            if (index < 0 || index >= market.Buyback.Count) return TradeRefusal.NothingToBuyBack;
            SoldLot lot = market.Buyback[index];
            if (!Coins.TryAdd(Money, -lot.Paid, out long left)) return TradeRefusal.NotEnoughMoney;
            Inventory trial = Inventory.Clone();
            // Sous-lot V1.3 : les instances vendues reviennent telles quelles (propriétés tirées comprises).
            if (lot.Stacks.Count > 0)
            {
                foreach (ItemStack stack in lot.Stacks)
                    if (trial.Add(stack) != InventoryRefusal.None) return TradeRefusal.InventoryFull;
            }
            else if (trial.Add(lot.Item, lot.Count) != InventoryRefusal.None) return TradeRefusal.InventoryFull;
            Inventory.CopyFrom(trial);
            _character = _character.WithMoney(left);
            market.Forget(index);
            return TradeRefusal.None;
        }
    }
}
