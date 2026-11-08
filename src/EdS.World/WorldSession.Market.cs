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

        /// <summary>
        /// Offres du marchand ce jour du monde (<see cref="Market.Stock"/>). Sous-lot V2.10 : au prix du jour (saison, temps, contrecoups, puis
        /// réputation ; jamais sous la valeur de base), avec ce qui en reste (<see cref="MerchantOffer.Left"/>), puis les objets revendus par le
        /// joueur que le marchand garde en vitrine (<see cref="MerchantOffer.Resold"/>).
        /// </summary>
        /// <exception cref="ArgumentException">Marchand inconnu.</exception>
        public IReadOnlyList<MerchantOffer> Stock(string merchant) => LivingStock(merchant);

        /// <summary>
        /// Sous-lot V2.7 : prix d'achat chez les marchands des villageois, en pour cent du prix du jour (<see cref="PricePercentOf"/>).
        /// </summary>
        public int PricePercent => Narrative is Narrative n ? n.Book.Reputation.PricePercent(n.StandingIndex("villagers")) : 100;

        /// <summary>
        /// Sous-lot V2.7 : prix d'achat chez <paramref name="merchant"/>, en pour cent du prix du jour, selon le palier de la réputation auprès de
        /// sa faction (<see cref="EdS.Data.Story.ReputationRules.Prices"/> ; 100 sans règle ni histoire).
        /// </summary>
        /// <exception cref="ArgumentException">Marchand inconnu.</exception>
        public int PricePercentOf(string merchant) =>
            Narrative is Narrative n ? n.Book.Reputation.PricePercent(n.StandingIndex(NeedMarket().Book.Merchant(merchant).Faction)) : 100;

        /// <summary>
        /// Sous-lot V2.7 (réponse de Damien du 7 novembre 2026) : ce que paie <paramref name="merchant"/> à la vente, en pour cent, selon le
        /// palier de sa faction (<see cref="EdS.Data.Story.ReputationRules.SellPrices"/> ; 100 sans règle).
        /// </summary>
        public int SellPercentOf(string merchant) =>
            Narrative is Narrative n ? n.Book.Reputation.SellPercent(n.StandingIndex(NeedMarket().Book.Merchant(merchant).Faction)) : 100;

        /// <summary>
        /// Sous-lot V2.7 : pièces que paie <paramref name="merchant"/> pour une unité de <paramref name="item"/> (pièce en bon état), selon la
        /// réputation, jamais au-dessus de la valeur de base de l'objet ; 0 s'il ne se vend pas.
        /// </summary>
        public long SellUnit(string merchant, string item)
        {
            Market market = NeedMarket();
            long unit = market.UnitPrice(merchant, item);
            int percent = SellPercentOf(merchant);
            if (unit <= 0 || percent == 100 || !market.Book.TryGetValue(item, out long value)) return unit;
            return Math.Min(Math.Max(1, value), ReputationPrice(unit, percent));
        }

        /// <summary>Sous-lot V2.7 : prix au pour cent <paramref name="percent"/>, arrondi au plus proche, une pièce de cuivre au moins.</summary>
        public static long ReputationPrice(long price, int percent) => price <= 0 ? price : Math.Max(1, checked(price * percent + 50) / 100);

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
            // Relecture indépendante de la PR #109 : l'étal est dans la zone où vit le PNJ, le marchand dans celle de son créneau (V2.3.1) :
            // devant un étal fermé, la boutique dit qu'elle est fermée même si le marchand est à l'auberge.
            GroundVector? atNpc = ((INarrativeWorld)this).PlayerPositionIn(NpcZone(npc));
            GroundVector? atStall = ((INarrativeWorld)this).PlayerPositionIn(npc.Zone);
            bool nearNpc = atNpc is GroundVector p && !ZonePopulations.Beyond(p, Fixed.Zero, NpcPosition(npc), Fixed.Zero, Narrative.TalkRange);
            bool nearStall = atStall is GroundVector q && !ZonePopulations.Beyond(q, Fixed.Zero, npc.Position, Fixed.Zero, Narrative.TalkRange);
            if (!nearNpc && !nearStall) return TradeRefusal.TooFar;
            TradeRefusal status = TradeStatus(merchant);
            if (status == TradeRefusal.Closed) return TradeRefusal.Closed;
            if (!nearNpc) return TradeRefusal.TooFar;
            return status;
        }

        /// <summary>
        /// Relecture indépendante de la PR #103 : ce que dirait la boutique <paramref name="merchant"/> auprès du marchand, sans regarder la
        /// distance : fermée à cette heure, rang de guilde trop bas, pas encore ouverte, ou ouverte (<see cref="TradeRefusal.None"/>).
        /// </summary>
        public TradeRefusal TradeStatus(string merchant)
        {
            Merchant m = NeedMarket().Book.Merchant(merchant);
            // Sous-lot V2.10 : un étal ambulant n'ouvre que pendant ses fêtes.
            if (m.Festivals.Count > 0 && !(Festival is Festival f && m.Festivals.Contains(f.Id))) return TradeRefusal.NotFestival;
            if (m.Closed.Contains(Narrative.PhaseId(Zone.Time.Phase))) return TradeRefusal.Closed;
            if (!HasRank(m.Rank)) return TradeRefusal.RankTooLow;
            if (m.Fact != null && !(Narrative ?? throw new InvalidOperationException("Commerce sans histoire.")).Knows(m.Fact)) return TradeRefusal.NotYetOpen;   // sous-lot V1.9.3b : l'étal de Renart, après son aveu
            // Sous-lot V2.7 (réponses de Damien du 7 novembre 2026) : haï de sa faction, le marchand refuse ; le marché noir des bandits ne s'ouvre
            // qu'au palier qu'il demande (le receleur parle sinon comme un autre).
            if (Narrative is Narrative n)
            {
                int tier = n.StandingIndex(m.Faction);
                if (m.Standing != null && tier < Reputations.TierIndex(m.Standing)) return TradeRefusal.NotYetOpen;
                if (tier == 0) return TradeRefusal.Shunned;
            }
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
            // Sous-lot V2.10 : stock vivant ; revue Codex de la PR #120 (tour 4) : les lignes après la prise sont calculées avant toute mutation.
            if (!TryTake(merchant, offer, count, out IReadOnlyList<SavedStock>? stock)) return TradeRefusal.OutOfStock;
            long price = checked(offer.Price * count);   // au plus 10^8 × 999
            if (!Coins.TryAdd(Money, -price, out long left)) return TradeRefusal.NotEnoughMoney;
            Inventory trial = Inventory.Clone();
            if (trial.Add(item, count) != InventoryRefusal.None) return TradeRefusal.InventoryFull;
            Inventory.CopyFrom(trial);
            _character = _character.WithMoney(left);
            if (stock != null) _character = _character.WithStock(stock);
            if (NeedMarket().Book.Merchant(merchant).Faction == "bandits") Narrative?.Fenced(count);   // sous-lot V2.7 : le recel
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
                long unit = SellUnit(merchant, lots[i].Item);   // sous-lot V2.7 : selon la réputation
                foreach (ItemStack s in removed[i]) paid[i] = checked(paid[i] + WornPrice(unit, s) * s.Count);
                gain = checked(gain + paid[i]);
            }
            if (!Coins.TryAdd(Money, gain, out long after)) return TradeRefusal.PurseFull;
            // Sous-lot V2.10 : en vitrine, revendu plus cher ; revue Codex de la PR #120 (tour 4) : lignes calculées avant toute mutation.
            IReadOnlyList<SavedStock> kept = _character.Stock;
            for (int i = 0; i < lots.Count; i++) kept = Kept(kept, merchant, lots[i].Item, lots[i].Count);
            Inventory.CopyFrom(trial);
            _character = _character.WithMoney(after).WithStock(kept);
            // Sous-lot V1.3 : un lot qui emporte des instances aux propriétés tirées les garde pour le rachat.
            for (int i = 0; i < lots.Count; i++)
            {
                market.Record(new SoldLot(lots[i].Item, lots[i].Count, paid[i], removed[i].Any(x => x.IsUnique) ? removed[i] : null, merchant));
            }
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
            long paid = checked(WornPrice(SellUnit(merchant, stack.Item), stack) * count);   // sous-lot V2.7 : selon la réputation   // sous-lot V1.8 : pièce usée moins chère
            if (!Coins.TryAdd(Money, paid, out long after)) return TradeRefusal.PurseFull;
            Inventory trial = Inventory.Clone();
            if (trial.RemoveAt(place, count, out ItemStack? removed) != InventoryRefusal.None || removed is null) return TradeRefusal.NotEnough;
            IReadOnlyList<SavedStock> kept = Kept(_character.Stock, merchant, stack.Item, count);   // sous-lot V2.10 : en vitrine, revendu plus cher
            Inventory.CopyFrom(trial);
            _character = _character.WithMoney(after).WithStock(kept);
            market.Record(new SoldLot(stack.Item, count, paid, removed.IsUnique ? new[] { removed } : null, merchant));
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
            IReadOnlyList<SavedStock> stock = lot.Merchant != null && market.Book.TryGetMerchant(lot.Merchant, out _)
                ? Unkept(_character.Stock, lot.Merchant, lot.Item, lot.Count) : _character.Stock;   // sous-lot V2.10
            Inventory.CopyFrom(trial);
            _character = _character.WithMoney(left).WithStock(stock);
            market.Forget(index);
            return TradeRefusal.None;
        }
    }
}
