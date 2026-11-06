using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Guild;
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
        /// La boutique de <paramref name="merchant"/> est ouverte au personnage : présent et vivant, dans la zone de son PNJ (sous-lot V1.7 : un
        /// intérieur aussi, la boutique de la guilde), à <see cref="Narrative.TalkRange"/> de lui (sinon <see cref="TradeRefusal.TooFar"/>), pas
        /// à un moment de fermeture (<see cref="TradeRefusal.Closed"/>), et (sous-lot V1.7) au rang de la guilde que demande la boutique
        /// (<see cref="TradeRefusal.RankTooLow"/>), et (sous-lot V1.9.3b) quand le personnage sait le fait que demande la boutique
        /// (<see cref="TradeRefusal.NotYetOpen"/> : le PNJ parle alors comme un autre).
        /// </summary>
        /// <exception cref="ArgumentException">Marchand inconnu.</exception>
        public TradeRefusal CanTrade(string merchant)
        {
            Merchant m = NeedMarket().Book.Merchant(merchant);
            Npc npc = (Narrative ?? throw new InvalidOperationException("Commerce sans histoire.")).Book.Npc(m.Npc);
            // Sous-lot V2.1 : la boutique est à l'étal (la place du PNJ dans l'histoire) ; la nuit, un marchand qui rentre chez lui se tient
            // devant sa porte. À l'étal comme à sa porte, une boutique fermée à cette heure le dit ; ouverte, on achète auprès du marchand.
            GroundVector? at = ((INarrativeWorld)this).PlayerPositionIn(npc.Zone);
            bool nearNpc = at is GroundVector p && !ZonePopulations.Beyond(p, Fixed.Zero, NpcPosition(npc), Fixed.Zero, Narrative.TalkRange);
            bool nearStall = at is GroundVector q && !ZonePopulations.Beyond(q, Fixed.Zero, npc.Position, Fixed.Zero, Narrative.TalkRange);
            if (!nearNpc && !nearStall) return TradeRefusal.TooFar;
            if (m.Closed.Contains(Narrative.PhaseId(Zone.Time.Phase))) return TradeRefusal.Closed;
            if (!nearNpc) return TradeRefusal.TooFar;
            if (!HasRank(m.Rank)) return TradeRefusal.RankTooLow;
            if (m.Fact != null && !Narrative.Knows(m.Fact)) return TradeRefusal.NotYetOpen;   // sous-lot V1.9.3b : l'étal de Renart, après son aveu
            return TradeRefusal.None;
        }

        /// <summary>Sous-lot V1.7 : le personnage a au moins le rang de la guilde <paramref name="rank"/> (null : aucun rang demandé).</summary>
        public bool HasRank(string? rank) => rank is null || (Narrative != null && Narrative.GuildRankIndex >= GuildRanks.IndexOf(rank));

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
            if (!HasRank(offer.Rank)) return TradeRefusal.RankTooLow;   // sous-lot V1.7 : offre de la boutique de la guilde réservée à un rang
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
            Inventory trial = Inventory.Clone();
            var removed = new IReadOnlyList<ItemStack>[lots.Count];
            for (int i = 0; i < lots.Count; i++)
                if (trial.Remove(lots[i].Item, lots[i].Count, out removed[i]) != InventoryRefusal.None) return TradeRefusal.NotEnough;
            // Sous-lot V1.8 (réponse de Damien du 31 octobre 2026) : une pièce usée se vend moins cher, selon sa solidité (WornPrice).
            var paid = new long[lots.Count];
            long gain = 0;
            for (int i = 0; i < lots.Count; i++)
            {
                long unit = market.UnitPrice(merchant, lots[i].Item);
                foreach (ItemStack s in removed[i]) paid[i] = checked(paid[i] + WornPrice(unit, s) * s.Count);
                gain = checked(gain + paid[i]);
            }
            if (!Coins.TryAdd(Money, gain, out long after)) return TradeRefusal.PurseFull;
            Inventory.CopyFrom(trial);
            _character = _character.WithMoney(after);
            // Sous-lot V1.3 : un lot qui emporte des instances aux propriétés tirées les garde pour le rachat.
            for (int i = 0; i < lots.Count; i++)
                market.Record(new SoldLot(lots[i].Item, lots[i].Count, paid[i], removed[i].Any(x => x.IsUnique) ? removed[i] : null));
            return TradeRefusal.None;
        }

        /// <summary>
        /// Sous-lot V1.3 : vend à <paramref name="merchant"/> <paramref name="count"/> objets de la place <paramref name="place"/> de
        /// l'inventaire, au prix de l'objet. Relecture de la PR #78 : toujours <b>de cette place exacte</b>, jamais d'une autre place du même
        /// objet : une instance aux propriétés tirées part elle-même (quantité 1), une pile d'objets de base perd <paramref name="count"/>
        /// objets (toute la pile ou une partie) sans qu'une instance tirée du même objet ailleurs dans l'inventaire ne parte à sa place.
        /// Refus : boutique fermée ou trop loin, quantité hors de [1, pile] (<see cref="TradeRefusal.InvalidCount"/>, ou
        /// <see cref="TradeRefusal.NotEnough"/> au-delà de la pile de base), objet sans valeur, quantité qui entamerait les objets emportés au
        /// combat (<see cref="TradeRefusal.Reserved"/>), bourse pleine. Tout ou rien ; rachetable (une instance tirée, avec ses propriétés).
        /// </summary>
        /// <exception cref="ArgumentException">Marchand inconnu.</exception>
        public TradeRefusal SellPlace(string merchant, int place, int count)
        {
            if (place < 0 || place >= Inventory.Stacks.Count) return TradeRefusal.NotEnough;
            ItemStack stack = Inventory.Stacks[place];
            TradeRefusal open = CanTrade(merchant);
            if (open != TradeRefusal.None) return open;
            Market market = NeedMarket();
            if (count < 1 || count > Market.MaxCount || (stack.IsUnique && count != 1)) return TradeRefusal.InvalidCount;
            if (count > stack.Count) return TradeRefusal.NotEnough;
            if (!market.Book.TryGetValue(stack.Item, out _)) return TradeRefusal.NoValue;
            if (Inventory.Available(stack.Item) < count) return TradeRefusal.Reserved;
            long paid = checked(WornPrice(market.UnitPrice(merchant, stack.Item), stack) * count);   // sous-lot V1.8 : pièce usée moins chère
            if (!Coins.TryAdd(Money, paid, out long after)) return TradeRefusal.PurseFull;
            Inventory trial = Inventory.Clone();
            if (trial.RemoveAt(place, count, out ItemStack? removed) != InventoryRefusal.None || removed is null) return TradeRefusal.NotEnough;
            Inventory.CopyFrom(trial);
            _character = _character.WithMoney(after);
            market.Record(new SoldLot(stack.Item, count, paid, removed.IsUnique ? new[] { removed } : null));
            return TradeRefusal.None;
        }

        /// <summary>
        /// Sous-lot V1.8 (réponse de Damien du 31 octobre 2026 : « une pièce usée se vend moins cher, proportionnellement à sa solidité, avec
        /// un minimum d'une pièce de cuivre ») : prix d'une unité de <paramref name="stack"/> pour un prix plein <paramref name="unit"/> :
        /// <c>unit × solidité restante / solidité pleine</c>, arrondi vers le bas, au moins 1 (si le prix plein n'est pas nul). Une pièce
        /// neuve (usure absente) se vend au prix plein. Le rachat se fait au prix payé.
        /// </summary>
        public long WornPrice(long unit, ItemStack stack)
        {
            if (stack is null) throw new ArgumentNullException(nameof(stack));
            if (unit <= 0 || stack.Wear is null) return unit;
            return Math.Max(1, checked(unit * _items.WearLeft(stack)) / _items.WearMax);
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
