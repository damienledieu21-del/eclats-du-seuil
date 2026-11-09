using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using EdS.Data.Merchants;
using EdS.Data.Saves;
using EdS.Data.Story;

namespace EdS.World
{
    /// <summary>
    /// Sous-lot V2.10 (marchands vivants) : le stock fini de chaque offre, qui s'épuise quand on achète et se regagne chaque jour du monde
    /// (<see cref="Merchant.Restock"/> % du stock plein par jour, arrondi au-dessus) ; ce que le joueur revend, que le marchand garde en vitrine
    /// et revend plus cher (au prix de l'offre, ou à <see cref="Merchant.Resale"/> % de la valeur), et qu'il écoule au même rythme ; les prix
    /// qui bougent avec la saison et le temps (<see cref="MerchantBook.Prices"/>) ; les contrecoups des événements du monde
    /// (<see cref="MerchantBook.Shocks"/> : l'attaque du hameau perdue vide les étals et fait monter les potions). Tout se recalcule du temps du
    /// monde et des lignes de stock sauvegardées (v16) : aucune horloge réelle, arithmétique vérifiée. Contrat de l'économie gardé : un prix
    /// d'achat n'est jamais sous la valeur de base, un prix de vente jamais au-dessus : acheter puis revendre ne rapporte jamais rien.
    /// </summary>
    public sealed partial class WorldSession
    {
        /// <summary>Unités regagnées (ou écoulées) par jour pour un stock de <paramref name="full"/> au rythme <paramref name="percent"/> % : au moins 1.</summary>
        public static int DailyRegain(int full, int percent) => full <= 0 ? 1 : Math.Max(1, checked(full * percent + 99) / 100);

        /// <summary>Ligne de stock sauvegardée de <paramref name="item"/> chez <paramref name="merchant"/>, ou null (tout est plein).</summary>
        private SavedStock? StockLine(string merchant, string item)
        {
            return LineIn(_character.Stock, merchant, item);
        }

        /// <summary>
        /// État du jour d'une ligne : unités qui manquent encore au stock plein <paramref name="full"/> et unités revendues encore en vitrine,
        /// après les jours écoulés depuis son dernier changement.
        /// </summary>
        private (int Missing, int Resold) LineToday(SavedStock? line, int full, int restock)
        {
            if (line is null) return (0, 0);
            // Relecture de la PR #120 : au-delà du jour le plus grand d'une sauvegarde (SavedEvent.MaxDay, 10^8 jours), le temps du stock s'arrête
            // (une ligne y est écrite au plafond) : ni regain ni écoulement de plus après une reprise.
            long days = Math.Max(0, Math.Min(Zone.Time.Day, SavedEvent.MaxDay) - line.Day);
            long regained = checked(DailyRegain(full, restock) * Math.Min(days, SavedStock.MaxUnits));
            long sold = checked(DailyRegain(line.Resold, restock) * Math.Min(days, SavedStock.MaxUnits));
            return ((int)Math.Max(0, line.Bought - regained), (int)Math.Max(0, line.Resold - sold));
        }

        /// <summary>Le contrecoup <paramref name="shock"/> est en cours : son événement réglé il y a moins de ses jours, ou son fait connu.</summary>
        public bool ShockActive(MarketShock shock)
        {
            if (shock is null) throw new ArgumentNullException(nameof(shock));
            if (shock.Fact != null) return Narrative is Narrative n && n.Knows(shock.Fact);
            if (shock.NotFact != null) return Narrative is Narrative story && !story.Knows(shock.NotFact);
            SavedEvent? entry = _character.Events.FirstOrDefault(e => e.Id == shock.Event);
            if (entry is null || entry.Last < 0 || entry.Outcome == EventOutcome.None) return false;
            if (shock.Outcome == "won" && entry.Outcome != EventOutcome.Won) return false;
            if (shock.Outcome == "lost" && entry.Outcome != EventOutcome.Lost) return false;
            long since = Zone.Time.Day - entry.Last;
            return since >= 0 && since < shock.Days;
        }

        /// <summary>Sous-lot V2.10 : contrecoups en cours (pour l'affichage : « les étals sont vides depuis l'attaque »).</summary>
        public IReadOnlyList<MarketShock> ActiveShocks =>
            Market is Market m ? m.Book.Shocks.Where(ShockActive).ToList().AsReadOnly() : (IReadOnlyList<MarketShock>)Array.Empty<MarketShock>();

        /// <summary>Stock plein du jour d'une offre de <paramref name="full"/> unités : réduit par les contrecoups en cours (le plus fort).</summary>
        private int FullToday(string merchant, string item, int full)
        {
            int percent = 100;
            foreach (MarketShock shock in NeedMarket().Book.Shocks)
                if (shock.Touches(merchant, item) && shock.StockPercent < percent && ShockActive(shock)) percent = shock.StockPercent;
            return checked(full * percent) / 100;
        }

        /// <summary>
        /// Sous-lot V2.10 : pour cent du prix de <paramref name="item"/> chez <paramref name="merchant"/> aujourd'hui, à cette heure : produit des
        /// règles de saison et de temps qui le touchent et des contrecoups en cours, borné à [<see cref="MerchantValidator.MinPercent"/>,
        /// <see cref="MerchantValidator.MaxPercent"/>] ; 100 sans règle.
        /// </summary>
        public int MarketPercent(string merchant, string item)
        {
            MerchantBook book = NeedMarket().Book;
            if (book.Prices.Count == 0 && book.Shocks.Count == 0) return 100;
            // Revue Codex de la PR #120 (tour 4) : produit exact de tous les facteurs (entier sans borne : au plus 64 facteurs de 300, aucun
            // débordement possible), arrondi vers le bas une seule fois, puis borné à [MinPercent, MaxPercent] : une réduction qui suit des
            // majorations n'est jamais faussée par un plafond intermédiaire (16 × 300 % puis 24 × 50 % : 256 %), et l'ordre des règles est sans effet.
            BigInteger product = 100;
            int factors = 0;
            string season = Season;
            WeatherState weather = Weather;
            foreach (PriceRule rule in book.Prices)
                if (rule.Touches(merchant, item) && (rule.Season is null || rule.Season == season) && (rule.Weather is null || WorldWeather.Matches(weather.Kind, rule.Weather)))
                {
                    product *= rule.Percent;
                    factors++;
                }
            foreach (MarketShock shock in book.Shocks)
                if (shock.Touches(merchant, item) && ShockActive(shock))
                {
                    product *= shock.PricePercent;
                    factors++;
                }
            BigInteger percent = BigInteger.Divide(product, BigInteger.Pow(100, factors));
            if (percent < MerchantValidator.MinPercent) return MerchantValidator.MinPercent;
            return percent > MerchantValidator.MaxPercent ? MerchantValidator.MaxPercent : (int)percent;
        }

        /// <summary>
        /// Prix d'achat d'une unité : prix de l'offre, puis la saison, le temps et les contrecoups (<see cref="MarketPercent"/>), puis la
        /// réputation (<see cref="PricePercentOf"/>) ; jamais sous la valeur de base.
        /// </summary>
        private long BuyPrice(string merchant, string item, long price)
        {
            Market market = NeedMarket();
            int market100 = MarketPercent(merchant, item);
            int reputation = PricePercentOf(merchant);
            long p = market100 == 100 ? price : ReputationPrice(price, market100);
            if (reputation != 100) p = ReputationPrice(p, reputation);
            return market.Book.TryGetValue(item, out long value) ? Math.Max(value, p) : p;
        }

        /// <summary>
        /// Sous-lot V2.10 : unités de <paramref name="item"/> en vente chez <paramref name="merchant"/> aujourd'hui (null : sans limite) :
        /// ce qui reste du stock plein du jour, plus ce que le joueur a revendu et que le marchand garde encore.
        /// </summary>
        public int? Left(string merchant, string item)
        {
            Merchant m = NeedMarket().Book.Merchant(merchant);
            MerchantOffer? offer = m.Offers.FirstOrDefault(o => o.Item == item);
            if (offer != null && offer.Stock is null) return null;
            int full = offer?.Stock ?? 0;
            (int missing, int resold) = LineToday(StockLine(merchant, item), full, m.Restock);
            return checked(Math.Max(0, FullToday(merchant, item, full) - missing) + resold);
        }

        /// <summary>Offres du jour : celles du tirage, à leur prix du jour et avec ce qui en reste, puis les objets revendus en vitrine.</summary>
        private IReadOnlyList<MerchantOffer> LivingStock(string merchant)
        {
            Market market = NeedMarket();
            Merchant m = market.Book.Merchant(merchant);
            var list = new List<MerchantOffer>();
            foreach (MerchantOffer o in market.Stock(merchant, Zone.Time.Day))
                list.Add(o.WithPrice(BuyPrice(merchant, o.Item, o.Price)).WithLeft(Left(merchant, o.Item)));
            // Ce que le joueur a revendu et qui n'est pas dans le tirage du jour : au prix de l'offre s'il en a une, sinon à son prix de revente.
            foreach (SavedStock line in _character.Stock)
            {
                if (line.Merchant != merchant || list.Any(o => o.Item == line.Item) || !market.Book.TryGetValue(line.Item, out long value)) continue;
                MerchantOffer? offer = m.Offers.FirstOrDefault(o => o.Item == line.Item);
                (_, int resold) = LineToday(line, offer?.Stock ?? 0, m.Restock);
                if (resold <= 0 || !_items.TryGetItem(line.Item, out _)) continue;
                long price = offer?.Price ?? Math.Max(value, checked(value * m.Resale) / 100);
                // Revue Codex de la PR #120 : un objet revendu garde le rang de guilde de l'offre d'origine (la réserve de la guilde).
                list.Add(MerchantOffer.ResoldOffer(line.Item, BuyPrice(merchant, line.Item, price), resold, offer?.Rank));
            }
            return list.AsReadOnly();
        }

        /// <summary>Ligne de <paramref name="item"/> chez <paramref name="merchant"/> dans <paramref name="lines"/>, ou null.</summary>
        private static SavedStock? LineIn(IReadOnlyList<SavedStock> lines, string merchant, string item)
        {
            string key = SavedStock.KeyOf(merchant, item);
            return lines.FirstOrDefault(x => x.Id == key);
        }

        /// <summary>
        /// Lignes de stock <paramref name="lines"/> avec celle de <paramref name="item"/> chez <paramref name="merchant"/> écrite au jour du monde
        /// (vide : retirée), ou null si elle ne peut pas l'être (clé hors format, stock sauvegardé plein). Calcul pur, sans rien changer.
        /// </summary>
        /// <remarks>Revue Codex de la PR #120 (tours 3 et 4) : les lignes revenues à rien (et celles d'un marchand ou d'un objet disparu) sont
        /// d'abord retirées, comme à la sauvegarde : leur place sert à d'autres sans sauvegarde ni rechargement. Sans effet sur ce qu'on voit (une
        /// ligne vide aujourd'hui le reste les jours suivants : manque et vitrine ne font que baisser).</remarks>
        private IReadOnlyList<SavedStock>? StockWith(IReadOnlyList<SavedStock> lines, string merchant, string item, int missing, int resold)
        {
            string key = SavedStock.KeyOf(merchant, item);
            if (!SavedStock.IsKey(key)) return null;   // jamais atteint avec des marchands validés (clé tenue au chargement), refusé sinon
            var rest = StockToSave(lines).Where(x => x.Id != key).ToList();
            if (missing == 0 && resold == 0) return rest.AsReadOnly();
            if (rest.Count >= SavedStock.MaxEntries) return null;
            rest.Add(new SavedStock(key, Math.Min(SavedStock.MaxUnits, missing), Math.Min(SavedStock.MaxUnits, resold), Math.Min(SavedEvent.MaxDay, Zone.Time.Day)));
            return rest.AsReadOnly();
        }

        /// <summary>
        /// Sous-lot V2.10 : lignes de stock à sauvegarder : celles d'un marchand et d'un objet connus qui ne sont pas revenues à rien
        /// aujourd'hui (une ligne d'un marchand ou d'un objet disparu des données est oubliée).
        /// </summary>
        private IEnumerable<SavedStock> StockToSave(IReadOnlyList<SavedStock> lines)
        {
            MerchantBook book = NeedMarket().Book;
            foreach (SavedStock line in lines)
            {
                if (!book.TryGetMerchant(line.Merchant, out Merchant m) || !book.TryGetValue(line.Item, out _)) continue;
                MerchantOffer? offer = m.Offers.FirstOrDefault(o => o.Item == line.Item);
                if (offer != null && offer.Stock is null) continue;
                (int missing, int resold) = LineToday(line, offer?.Stock ?? 0, m.Restock);
                if (missing > 0 || resold > 0) yield return line;
            }
        }

        /// <summary>
        /// Le stock peut-il donner <paramref name="count"/> unités de l'offre <paramref name="offer"/> (de <see cref="LivingStock"/>) ? Si oui,
        /// <paramref name="stock"/> reçoit les lignes après la prise (null : offre sans limite, rien à écrire). Ne change rien : l'appelant écrit
        /// ces lignes avec le reste de la transaction, après tous ses contrôles (revue Codex de la PR #120, tour 4 : refus atomique, aucune
        /// écriture qui puisse échouer après une mutation).
        /// </summary>
        /// <remarks>Revue Codex de la PR #120 (tour 3) : la quantité se juge contre l'offre réellement proposée : un objet revendu hors du tirage
        /// du jour n'offre que sa vitrine, jamais le stock de l'offre absente.</remarks>
        private bool TryTake(string merchant, MerchantOffer offer, int count, out IReadOnlyList<SavedStock>? stock)
        {
            stock = null;
            if (!(offer.Left is int left)) return true;
            if (left < count) return false;
            string item = offer.Item;
            Merchant m = NeedMarket().Book.Merchant(merchant);
            MerchantOffer? data = m.Offers.FirstOrDefault(o => o.Item == item);
            (int missing, int resold) = LineToday(LineIn(_character.Stock, merchant, item), data?.Stock ?? 0, m.Restock);
            int fromResold = Math.Min(resold, count);
            if (offer.Resold && fromResold < count) return false;   // une offre de vitrine ne prend que dans la vitrine
            stock = StockWith(_character.Stock, merchant, item, checked(missing + count - fromResold), resold - fromResold);
            return stock != null;
        }

        /// <summary>
        /// Lignes <paramref name="lines"/> après que le marchand a gardé en vitrine <paramref name="count"/> unités revendues de
        /// <paramref name="item"/> (bornées), s'il le vend à stock fini ou ne le vend pas d'ordinaire ; sinon (offre sans limite, objet sans
        /// valeur, stock sauvegardé plein) les mêmes lignes. Calcul pur.
        /// </summary>
        private IReadOnlyList<SavedStock> Kept(IReadOnlyList<SavedStock> lines, string merchant, string item, int count)
        {
            Merchant m = NeedMarket().Book.Merchant(merchant);
            MerchantOffer? offer = m.Offers.FirstOrDefault(o => o.Item == item);
            if ((offer != null && offer.Stock is null) || !NeedMarket().Book.TryGetValue(item, out _)) return lines;
            (int missing, int resold) = LineToday(LineIn(lines, merchant, item), offer?.Stock ?? 0, m.Restock);
            return StockWith(lines, merchant, item, missing, Math.Min(SavedStock.MaxUnits, checked(resold + count))) ?? lines;
        }

        /// <summary>Lignes <paramref name="lines"/> après un rachat : le marchand reprend le lot de sa vitrine (autant qu'il en garde encore).</summary>
        private IReadOnlyList<SavedStock> Unkept(IReadOnlyList<SavedStock> lines, string merchant, string item, int count)
        {
            Merchant m = NeedMarket().Book.Merchant(merchant);
            MerchantOffer? offer = m.Offers.FirstOrDefault(o => o.Item == item);
            (int missing, int resold) = LineToday(LineIn(lines, merchant, item), offer?.Stock ?? 0, m.Restock);
            if (resold <= 0) return lines;
            return StockWith(lines, merchant, item, missing, resold - Math.Min(resold, count)) ?? lines;
        }
    }
}
