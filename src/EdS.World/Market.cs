using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EdS.Data.Merchants;
using EdS.Simulation.Randomness;

namespace EdS.World
{
    /// <summary>Motif d'un refus d'achat, de vente ou de rachat (rien n'a changé : ni la bourse, ni l'inventaire).</summary>
    public enum TradeRefusal
    {
        None = 0,

        /// <summary>Or insuffisant : la bourse ne couvre pas le prix.</summary>
        NotEnoughMoney = 1,

        /// <summary>Inventaire plein : l'objet acheté ou racheté ne tient pas en entier.</summary>
        InventoryFull = 2,

        /// <summary>L'objet à vendre est porté (équipement) et l'inventaire n'en a pas assez.</summary>
        Equipped = 3,

        /// <summary>L'objet à vendre est réservé (objet de soin emporté au combat, jusqu'à ce qu'il soit bu).</summary>
        Reserved = 4,

        /// <summary>Objet inconnu du catalogue.</summary>
        UnknownItem = 5,

        /// <summary>Boutique fermée à ce moment de la journée.</summary>
        Closed = 6,

        /// <summary>Trop loin du marchand (ou personnage absent, tombé, dans un intérieur).</summary>
        TooFar = 7,

        /// <summary>Le marchand ne vend pas cet objet aujourd'hui.</summary>
        NotForSale = 8,

        /// <summary>Quantité absente de l'inventaire.</summary>
        NotEnough = 9,

        /// <summary>Bourse pleine : la vente la ferait dépasser son plafond.</summary>
        PurseFull = 10,

        /// <summary>Objet sans valeur (objet de quête) : aucun marchand n'en veut.</summary>
        NoValue = 11,

        /// <summary>Rien à racheter à ce rang.</summary>
        NothingToBuyBack = 12,

        /// <summary>Quantité impossible (moins de 1, plus que <see cref="Market.MaxCount"/>), ou vente vide ou trop longue.</summary>
        InvalidCount = 13,

        /// <summary>Sous-lot V1.7 : rang de la Guilde des aventuriers trop bas pour cette boutique ou cette offre (la boutique de la guilde).</summary>
        RankTooLow = 14,
    }

    /// <summary>Un lot vendu, rachetable au même prix pendant la session de jeu : objet, quantité, pièces reçues (cuivre).</summary>
    public sealed class SoldLot
    {
        public string Item { get; }
        public int Count { get; }

        /// <summary>Pièces reçues pour ce lot (cuivre) : le prix de son rachat.</summary>
        public long Paid { get; }

        internal SoldLot(string item, int count, long paid)
        {
            Item = item;
            Count = count;
            Paid = paid;
        }
    }

    /// <summary>
    /// Sous-lot V1.1 : le commerce du monde. Les marchands et la valeur des objets (<see cref="MerchantBook"/>) ; le stock du jour de chaque
    /// marchand, tiré de façon déterministe sur la clé du jour (<see cref="DayKey"/>, générateur <see cref="Pcg32"/>, aucune horloge réelle) ;
    /// le prix de revente ; et les lots vendus, rachetables au même prix tant que dure la session de jeu (au plus <see cref="MaxBuyback"/>,
    /// les plus anciens oubliés d'abord ; non sauvegardés). Une même instance passe d'une session de zone à l'autre (halte, porte, voyage).
    /// Achats, ventes et rachats se font par <see cref="WorldSession"/>, qui les rend atomiques.
    /// </summary>
    public sealed class Market
    {
        /// <summary>Lots rachetables gardés au plus.</summary>
        public const int MaxBuyback = 12;

        /// <summary>Quantité d'un achat, d'un lot vendu ou d'une vente entière, au plus.</summary>
        public const int MaxCount = 999;

        /// <summary>Lots d'une même vente, au plus (une vente de tout un sac).</summary>
        public const int MaxSellLots = 64;

        private readonly List<SoldLot> _buyback = new List<SoldLot>();

        public MerchantBook Book { get; }

        public Market(MerchantBook book)
        {
            Book = book ?? throw new ArgumentNullException(nameof(book));
        }

        /// <summary>Lots rachetables, le plus récent d'abord.</summary>
        public IReadOnlyList<SoldLot> Buyback => _buyback.AsReadOnly();

        /// <summary>Clé du stock d'un marchand un jour du monde (<c>merchant.armurier/day/3</c>).</summary>
        public static string DayKey(string merchant, long day) =>
            (merchant ?? throw new ArgumentNullException(nameof(merchant))) + "/day/" + day.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Offres du marchand <paramref name="merchant"/> le jour <paramref name="day"/> du monde : toutes ses offres permanentes, et
        /// <see cref="Merchant.Daily"/> offres tirées parmi les autres, dans l'ordre du fichier. Le tirage (mélange partiel de Fisher-Yates)
        /// est fait par un <see cref="Pcg32"/> initialisé par l'empreinte FNV-1a de <see cref="DayKey"/> : le même jour donne toujours le même
        /// stock.
        /// </summary>
        /// <exception cref="ArgumentException">Marchand inconnu.</exception>
        public IReadOnlyList<MerchantOffer> Stock(string merchant, long day)
        {
            if (day < 0) throw new ArgumentOutOfRangeException(nameof(day));
            Merchant m = Book.Merchant(merchant);
            var rotating = m.Offers.Where(o => !o.Always).ToList();
            var chosen = new HashSet<MerchantOffer>();
            if (m.Daily > 0)
            {
                var random = new Pcg32(Loot.Fnv1a(DayKey(m.Id, day)), Loot.Fnv1a("merchant:" + m.Id));
                for (int i = 0; i < m.Daily; i++)
                {
                    int j = i + (int)random.NextBounded((uint)(rotating.Count - i));
                    (rotating[i], rotating[j]) = (rotating[j], rotating[i]);
                    chosen.Add(rotating[i]);
                }
            }
            return m.Offers.Where(o => o.Always || chosen.Contains(o)).ToList().AsReadOnly();
        }

        /// <summary>
        /// Pièces payées par le marchand pour une unité de <paramref name="item"/> : la part <see cref="Merchant.SellPercent"/> de sa valeur
        /// de base, arrondie au cuivre inférieur, au moins 1 cuivre ; 0 pour un objet sans valeur (il ne se vend pas). Jamais plus que la
        /// valeur, ni donc que le prix de vente d'un marchand.
        /// </summary>
        public long UnitPrice(string merchant, string item)
        {
            Merchant m = Book.Merchant(merchant);
            if (!Book.TryGetValue(item, out long value)) return 0;
            return Math.Max(1, checked(value * m.SellPercent) / 100);
        }

        /// <summary>Garde un lot vendu (le plus récent d'abord) ; au-delà de <see cref="MaxBuyback"/>, le plus ancien est oublié.</summary>
        internal void Record(SoldLot lot)
        {
            _buyback.Insert(0, lot ?? throw new ArgumentNullException(nameof(lot)));
            if (_buyback.Count > MaxBuyback) _buyback.RemoveAt(_buyback.Count - 1);
        }

        internal void Forget(int index) => _buyback.RemoveAt(index);
    }
}
