using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Guild;
using EdS.Data.Items;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Domain.Collections;
using EdS.Domain.Space;

namespace EdS.Data.Merchants
{
    // --- Documents (avant validation) ------------------------------------------------------------------

    /// <summary>Sous-lot V1.1 : marchands et valeurs des objets tels que lus (<c>kind: "merchants"</c>, <c>data/v0/world/merchants.json</c>).</summary>
    public sealed class MerchantsDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
        public MerchantsData? Data { get; set; }
    }

    public sealed class MerchantsData
    {
        /// <summary>Valeur de base de chaque objet qui se vend ou s'achète, en pièces de cuivre.</summary>
        public List<ItemValueData?>? Values { get; set; }

        public List<MerchantData?>? Merchants { get; set; }

        /// <summary>Sous-lot V2.10 (schéma 5) : prix qui bougent avec la saison et le temps, facultatifs.</summary>
        public List<PriceRuleData?>? Prices { get; set; }

        /// <summary>Sous-lot V2.10 (schéma 5) : contrecoups des événements du monde sur le stock et les prix, facultatifs.</summary>
        public List<ShockData?>? Shocks { get; set; }

        /// <summary>Sous-lot V2.14 (schéma 6) : le grand jour de marché sur la place du bourg, facultatif.</summary>
        public MarketDayData? Market { get; set; }
    }

    /// <summary>Sous-lot V2.10 (schéma 5) : une règle de prix, avant validation.</summary>
    public sealed class PriceRuleData
    {
        public string? Season { get; set; }
        public string? Weather { get; set; }
        public List<string?>? Items { get; set; }
        public List<string?>? Merchants { get; set; }
        public int? Percent { get; set; }

        /// <summary>Sous-lot V2.14 (schéma 6) : la règle ne vaut que pendant le grand jour de marché.</summary>
        public bool? Market { get; set; }
    }

    /// <summary>Sous-lot V2.10 (schéma 5) : un contrecoup, avant validation.</summary>
    public sealed class ShockData
    {
        public string? Id { get; set; }
        public string? Event { get; set; }
        public string? Outcome { get; set; }
        public int? Days { get; set; }
        public string? Fact { get; set; }

        /// <summary>Fait dont l'absence fait durer le contrecoup (la route coupée tant que la bande tient le camp).</summary>
        public string? NotFact { get; set; }
        public List<string?>? Items { get; set; }
        public List<string?>? Merchants { get; set; }
        public int? StockPercent { get; set; }
        public int? PricePercent { get; set; }
    }

    public sealed class ItemValueData
    {
        public string? Item { get; set; }
        public long? Value { get; set; }
    }

    public sealed class MerchantData
    {
        public string? Id { get; set; }

        /// <summary>PNJ de l'histoire qui tient la boutique (on lui parle pour l'ouvrir).</summary>
        public string? Npc { get; set; }

        /// <summary>Clé du texte d'accueil de la boutique.</summary>
        public string? Greeting { get; set; }

        /// <summary>Part de la valeur de base payée au joueur qui vend un objet, en pour cent.</summary>
        public int? SellPercent { get; set; }

        /// <summary>Moments de la journée où la boutique est fermée (« night »…), éventuellement aucun.</summary>
        public List<string?>? Closed { get; set; }

        /// <summary>Nombre d'offres tournantes (non permanentes) tirées chaque jour du monde.</summary>
        public int? Daily { get; set; }

        /// <summary>Sous-lot V1.7 (schéma 2) : rang de la guilde demandé pour ouvrir la boutique (la boutique de la guilde), ou null.</summary>
        public string? Rank { get; set; }

        /// <summary>Sous-lot V1.9.3b (schéma 3) : fait de l'histoire demandé pour ouvrir la boutique (l'étal de Renart, après son aveu), ou null.</summary>
        public string? Fact { get; set; }

        /// <summary>Sous-lot V2.7 (schéma 4) : faction du marchand, dont la réputation règle ses prix (absente : « villagers »).</summary>
        public string? Faction { get; set; }

        /// <summary>Sous-lot V2.7 (schéma 4) : palier de sa faction demandé pour ouvrir la boutique (le marché noir des bandits), ou null.</summary>
        public string? Standing { get; set; }

        /// <summary>Sous-lot V2.10 (schéma 5) : part du stock plein regagnée chaque jour du monde, en pour cent (absente : 50).</summary>
        public int? Restock { get; set; }

        /// <summary>Sous-lot V2.10 (schéma 5) : prix d'un objet revendu par le joueur et qu'il ne vend pas d'ordinaire, en pour cent de sa valeur (absent : 125).</summary>
        public int? Resale { get; set; }

        /// <summary>Sous-lot V2.10 (schéma 5) : étal ambulant : fêtes où il est ouvert (absentes : toujours, à ses heures).</summary>
        public List<string?>? Festivals { get; set; }

        /// <summary>Sous-lot V2.14 (schéma 6) : vraie boutique (genre, enseigne, bâtiment, horaires, jours de repos, porte), facultative.</summary>
        public ShopData? Shop { get; set; }

        /// <summary>Sous-lot V2.14 (schéma 6) : fournées du jour (heures et parts), facultatives.</summary>
        public List<BakeData?>? Bakes { get; set; }

        /// <summary>Sous-lot V2.14 (schéma 6) : heure de la rupture du soir (tout le frais est parti), avec <see cref="Bakes"/>.</summary>
        public string? SoldOut { get; set; }

        public List<OfferData?>? Offers { get; set; }
    }

    public sealed class OfferData
    {
        public string? Item { get; set; }

        /// <summary>Prix d'achat d'une unité, en pièces de cuivre.</summary>
        public long? Price { get; set; }

        /// <summary>Toujours en vente (sinon : tirée certains jours seulement).</summary>
        public bool? Always { get; set; }

        /// <summary>Sous-lot V1.7 (schéma 2) : rang de la guilde demandé pour acheter cette offre, ou null.</summary>
        public string? Rank { get; set; }

        /// <summary>Sous-lot V2.10 (schéma 5) : unités au stock plein (absent : sans limite).</summary>
        public int? Stock { get; set; }

        /// <summary>Sous-lot V2.14 (schéma 6) : offre fraîche, qui suit les fournées du marchand.</summary>
        public bool? Fresh { get; set; }

        /// <summary>Sous-lot V2.14 (schéma 6) : vendue aussi à la porte quand la boutique est fermée (jamais de quête bloquée par un horaire).</summary>
        public bool? Door { get; set; }
    }

    // --- Modèle validé ---------------------------------------------------------------------------------

    /// <summary>Un objet que vend un marchand : prix d'une unité (cuivre), toujours en vente ou certains jours seulement.</summary>
    public sealed class MerchantOffer
    {
        public string Item { get; }
        public long Price { get; }
        public bool Always { get; }

        /// <summary>Sous-lot V1.7 : rang de la guilde demandé pour l'acheter (« f »…), ou null.</summary>
        public string? Rank { get; }

        /// <summary>Sous-lot V2.10 (schéma 5) : unités au stock plein, ou null (sans limite).</summary>
        public int? Stock { get; }

        /// <summary>
        /// Sous-lot V2.10 : unités en vente aujourd'hui (stock vivant : ce qui reste du stock plein, plus ce que le joueur a revendu), ou null
        /// (sans limite). Donné par la partie (<c>WorldSession.Stock</c>), jamais par le fichier.
        /// </summary>
        public int? Left { get; }

        /// <summary>Sous-lot V2.10 : objet revendu par le joueur que le marchand ne vend pas d'ordinaire (prix de revente du marchand).</summary>
        public bool Resold { get; }

        /// <summary>Sous-lot V2.14 : offre fraîche (pains, viennoiseries) : elle suit les fournées du marchand.</summary>
        public bool Fresh { get; private set; }

        /// <summary>Sous-lot V2.14 : vendue aussi à la porte quand la boutique est fermée.</summary>
        public bool Door { get; private set; }

        internal MerchantOffer WithShop(bool fresh, bool door)
        {
            Fresh = fresh;
            Door = door;
            return this;
        }

        private MerchantOffer Same(MerchantOffer copy)
        {
            copy.Fresh = Fresh;
            copy.Door = Door;
            return copy;
        }

        internal MerchantOffer(string item, long price, bool always, string? rank = null, int? stock = null, int? left = null, bool resold = false)
        {
            Item = item;
            Price = price;
            Always = always;
            Rank = rank;
            Stock = stock;
            Left = left;
            Resold = resold;
        }

        /// <summary>Sous-lot V2.7 : la même offre à un autre prix (réputation auprès des villageois).</summary>
        public MerchantOffer WithPrice(long price) => price == Price ? this : Same(new MerchantOffer(Item, price, Always, Rank, Stock, Left, Resold));

        /// <summary>Sous-lot V2.10 : la même offre avec ce qui en reste aujourd'hui.</summary>
        public MerchantOffer WithLeft(int? left) => left == Left ? this : Same(new MerchantOffer(Item, Price, Always, Rank, Stock, left, Resold));

        /// <summary>Sous-lot V2.10 : offre d'un objet revendu par le joueur (prix de revente, <paramref name="left"/> unités).</summary>
        public static MerchantOffer ResoldOffer(string item, long price, int left, string? rank = null) => new MerchantOffer(item, price, false, rank, null, left, true);
    }

    /// <summary>
    /// Sous-lot V1.1 : un marchand. Son PNJ (on lui parle pour ouvrir la boutique), son accueil, la part de la valeur de base qu'il paie
    /// quand on lui vend un objet, ses heures de fermeture et ses offres : les permanentes, et <see cref="Daily"/> offres tirées chaque jour
    /// parmi les autres.
    /// </summary>
    public sealed class Merchant
    {
        public string Id { get; }
        public string Npc { get; }
        public string Greeting { get; }
        public int SellPercent { get; }

        /// <summary>Moments de la journée de fermeture (« dawn », « day », « dusk », « night »), dans l'ordre du fichier.</summary>
        public IReadOnlyList<string> Closed { get; }

        public int Daily { get; }
        public IReadOnlyList<MerchantOffer> Offers { get; }

        /// <summary>Sous-lot V1.7 : rang de la guilde demandé pour ouvrir la boutique (la boutique de la guilde), ou null.</summary>
        public string? Rank { get; }

        /// <summary>Sous-lot V1.9.3b : fait de l'histoire demandé pour ouvrir la boutique (appris par le personnage), ou null.</summary>
        public string? Fact { get; }

        /// <summary>Sous-lot V2.7 : faction dont la réputation règle les prix et l'accueil (« villagers » par défaut).</summary>
        public string Faction { get; }

        /// <summary>Sous-lot V2.7 : palier de <see cref="Faction"/> demandé pour ouvrir la boutique, ou null.</summary>
        public string? Standing { get; }

        /// <summary>Sous-lot V2.10 : part du stock plein regagnée chaque jour du monde, en pour cent (et part des objets revendus écoulée).</summary>
        public int Restock { get; private set; } = MerchantValidator.DefaultRestock;

        /// <summary>Sous-lot V2.10 : prix d'un objet revendu qu'il ne vend pas d'ordinaire, en pour cent de sa valeur (au moins 100).</summary>
        public int Resale { get; private set; } = MerchantValidator.DefaultResale;

        /// <summary>Sous-lot V2.10 : étal ambulant ouvert seulement pendant ces fêtes ; vide : toujours (à ses heures).</summary>
        public IReadOnlyList<string> Festivals { get; private set; } = Array.Empty<string>();

        /// <summary>Sous-lot V2.14 : vraie boutique (horaires, enseigne, porte), ou null (étal sans horaires : seulement <see cref="Closed"/>).</summary>
        public Shop? Shop { get; private set; }

        /// <summary>Sous-lot V2.14 : fournées du jour, dans l'ordre des heures (vide : rien de frais).</summary>
        public IReadOnlyList<Bake> Bakes { get; private set; } = Array.Empty<Bake>();

        /// <summary>Sous-lot V2.14 : minute de la rupture du soir des offres fraîches (avec <see cref="Bakes"/>).</summary>
        public int SoldOut { get; private set; }

        internal Merchant WithShop(Shop? shop, IEnumerable<Bake> bakes, int soldOut)
        {
            Shop = shop;
            Bakes = Frozen.List(bakes, nameof(bakes));
            SoldOut = soldOut;
            return this;
        }

        internal Merchant WithLiving(int restock, int resale, IEnumerable<string> festivals)
        {
            Restock = restock;
            Resale = resale;
            Festivals = Frozen.List(festivals, nameof(festivals));
            return this;
        }

        internal Merchant(string id, string npc, string greeting, int sellPercent, IEnumerable<string> closed, int daily, IEnumerable<MerchantOffer> offers,
            string? rank = null, string? fact = null, string faction = "villagers", string? standing = null)
        {
            Faction = faction;
            Standing = standing;
            Rank = rank;
            Fact = fact;
            Id = id;
            Npc = npc;
            Greeting = greeting;
            SellPercent = sellPercent;
            Closed = Frozen.List(closed, nameof(closed));
            Daily = daily;
            Offers = Frozen.List(offers, nameof(offers));
        }
    }

    /// <summary>
    /// Sous-lot V1.1 : les marchands du monde et la valeur de base des objets (en cuivre). Un objet sans valeur ne se vend pas (objets de
    /// quête). Contrat vérifié par le validateur : aucun objet n'est vendu sous sa valeur de base, et un marchand ne rend jamais plus que
    /// cette valeur ; acheter puis revendre ne fait donc jamais gagner d'argent.
    /// </summary>
    public sealed class MerchantBook
    {
        private readonly IReadOnlyDictionary<string, long> _values;

        public int SchemaVersion { get; }
        public string ContentVersion { get; }

        /// <summary>Valeur de base des objets, par identifiant (cuivre).</summary>
        public IReadOnlyDictionary<string, long> Values => _values;

        public IReadOnlyList<Merchant> Merchants { get; }

        /// <summary>Sous-lot V2.10 (schéma 5) : prix qui bougent avec la saison et le temps.</summary>
        public IReadOnlyList<PriceRule> Prices { get; private set; } = Array.Empty<PriceRule>();

        /// <summary>Sous-lot V2.10 (schéma 5) : contrecoups des événements du monde.</summary>
        public IReadOnlyList<MarketShock> Shocks { get; private set; } = Array.Empty<MarketShock>();

        /// <summary>Sous-lot V2.14 (schéma 6) : le grand jour de marché, ou null.</summary>
        public MarketDay? Market { get; private set; }

        internal MerchantBook WithMarket(MarketDay? market)
        {
            Market = market;
            return this;
        }

        internal MerchantBook WithLiving(IEnumerable<PriceRule> prices, IEnumerable<MarketShock> shocks)
        {
            Prices = Frozen.List(prices, nameof(prices));
            Shocks = Frozen.List(shocks, nameof(shocks));
            return this;
        }

        internal MerchantBook(int schemaVersion, string contentVersion, IEnumerable<KeyValuePair<string, long>> values, IEnumerable<Merchant> merchants)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            _values = Frozen.Dictionary(values, nameof(values));
            Merchants = Frozen.List(merchants, nameof(merchants));
        }

        public bool TryGetValue(string item, out long value) => _values.TryGetValue(item ?? string.Empty, out value);

        public bool TryGetMerchant(string id, out Merchant merchant)
        {
            merchant = Merchants.FirstOrDefault(m => m.Id == id)!;
            return merchant != null;
        }

        /// <exception cref="ArgumentException">Marchand inconnu.</exception>
        public Merchant Merchant(string id) =>
            Merchants.FirstOrDefault(m => m.Id == id) ?? throw new ArgumentException($"Marchand « {id} » inconnu.", nameof(id));

        /// <summary>Marchand tenu par ce PNJ, ou null.</summary>
        public Merchant? OfNpc(string npc) => Merchants.FirstOrDefault(m => m.Npc == npc);
    }

    /// <summary>
    /// Sous-lot V2.10 (schéma 5) : un prix qui bouge : pendant la saison <see cref="Season"/> (ou toute l'année) et sous le temps
    /// <see cref="Weather"/> (ou par tous les temps), les objets <see cref="Items"/> (vide : tous) des marchands <see cref="Merchants"/> (vide :
    /// tous) se vendent à <see cref="Percent"/> % de leur prix. Jamais sous la valeur de base de l'objet.
    /// </summary>
    public sealed class PriceRule
    {
        public string? Season { get; }
        public string? Weather { get; }
        public IReadOnlyList<string> Items { get; }
        public IReadOnlyList<string> Merchants { get; }
        public int Percent { get; }

        /// <summary>Sous-lot V2.14 : la règle ne vaut que pendant le grand jour de marché (marché ouvert).</summary>
        public bool Market { get; }

        internal PriceRule(string? season, string? weather, IEnumerable<string> items, IEnumerable<string> merchants, int percent, bool market = false)
        {
            Market = market;
            Season = season;
            Weather = weather;
            Items = Frozen.List(items, nameof(items));
            Merchants = Frozen.List(merchants, nameof(merchants));
            Percent = percent;
        }

        /// <summary>La règle touche cet objet de ce marchand.</summary>
        public bool Touches(string merchant, string item) =>
            (Merchants.Count == 0 || Merchants.Contains(merchant)) && (Items.Count == 0 || Items.Contains(item));
    }

    /// <summary>
    /// Sous-lot V2.10 (schéma 5) : un contrecoup d'un événement du monde : tant que dure sa cause (l'occurrence de l'événement <see cref="Event"/>
    /// réglée par l'issue <see cref="Outcome"/> il y a moins de <see cref="Days"/> jours, le fait <see cref="Fact"/> connu, ou le fait
    /// <see cref="NotFact"/> pas encore appris), le stock plein
    /// des objets touchés tombe à <see cref="StockPercent"/> % (0 : rupture) et leur prix monte à <see cref="PricePercent"/> %.
    /// </summary>
    public sealed class MarketShock
    {
        public string Id { get; }

        /// <summary>Clé du registre des événements (« zone.vallons/attaque_hameau »), ou null.</summary>
        public string? Event { get; }

        /// <summary>« won », « lost » ou « any » (pour un événement).</summary>
        public string Outcome { get; }

        public int Days { get; }
        public string? Fact { get; }
        public string? NotFact { get; }
        public IReadOnlyList<string> Items { get; }
        public IReadOnlyList<string> Merchants { get; }
        public int StockPercent { get; }
        public int PricePercent { get; }

        internal MarketShock(string id, string? @event, string outcome, int days, string? fact, IEnumerable<string> items, IEnumerable<string> merchants,
            int stockPercent, int pricePercent, string? notFact = null)
        {
            NotFact = notFact;
            Id = id;
            Event = @event;
            Outcome = outcome;
            Days = days;
            Fact = fact;
            Items = Frozen.List(items, nameof(items));
            Merchants = Frozen.List(merchants, nameof(merchants));
            StockPercent = stockPercent;
            PricePercent = pricePercent;
        }

        public bool Touches(string merchant, string item) =>
            (Merchants.Count == 0 || Merchants.Contains(merchant)) && (Items.Count == 0 || Items.Contains(item));
    }

    public sealed class MerchantsLoadResult
    {
        public ValidationReport Report { get; }
        public MerchantBook? Book { get; }
        public bool IsValid => Book != null;

        internal MerchantsLoadResult(ValidationReport report, MerchantBook? book)
        {
            Report = report;
            Book = book;
        }
    }

    /// <summary>
    /// Validateur unique des marchands (<c>kind: "merchants"</c>, sous-lot V1.1), contre les textes, l'histoire (PNJ) et le catalogue des
    /// objets : valeurs de base d'objets connus, une fois chacun ; marchands d'identifiants uniques, chacun tenu par un PNJ différent, avec
    /// son accueil, sa part de revente, ses heures de fermeture, ses offres (objets connus et valués, une fois chacun, jamais sous leur
    /// valeur de base) et un nombre d'offres tirées chaque jour qui laisse toujours quelque chose en vente.
    /// </summary>
    public static class MerchantValidator
    {
        public const string Kind = "merchants";

        /// <summary>
        /// Version 1 (sous-lot V1.1) : <c>values</c> (<c>item</c>, <c>value</c>) et <c>merchants</c> (<c>id</c>, <c>npc</c>, <c>greeting</c>,
        /// <c>sellPercent</c>, <c>closed</c>, <c>daily</c>, <c>offers</c> : <c>item</c>, <c>price</c>, <c>always</c>). Version 2 (sous-lot V1.7) :
        /// <c>rank</c> facultatif d'un marchand (rang de la Guilde des aventuriers demandé pour ouvrir sa boutique : la boutique de la guilde)
        /// et d'une offre (rang demandé pour l'acheter), au moins celui de la boutique. Version 3 (sous-lot V1.9.3b) : <c>fact</c> facultatif
        /// d'un marchand (fait de l'histoire demandé pour ouvrir sa boutique, posé par au moins un effet <c>setFact</c> de l'histoire : l'étal
        /// de Renart, après son aveu). Un fichier en version 2 (<see cref="PreviousSchemaVersion"/>) reste lu (sans <c>fact</c>) ; une version
        /// plus ancienne est refusée (pré-alpha). Version 4 (sous-lot V2.7, réputation) : <c>faction</c> facultative d'un marchand (une faction de
        /// <see cref="Reputations.Factions"/> ; absente : « villagers ») et <c>standing</c> facultatif (palier de cette faction demandé pour ouvrir
        /// la boutique : le marché noir des bandits). Les versions 2 et 3 restent lues.
        /// </summary>
        /// <remarks>Version 5 (sous-lot V2.10, marchands vivants et fêtes) : <c>stock</c> facultatif d'une offre (unités au stock plein, de 1 à
        /// <see cref="MaxStock"/> ; absent : sans limite) ; <c>restock</c> (part regagnée par jour, de 1 à 100 %, 50 par défaut), <c>resale</c>
        /// (prix d'un objet revendu, de 100 à <see cref="MaxPercent"/> % de sa valeur, 125 par défaut) et <c>festivals</c> (étal ambulant :
        /// fêtes de l'histoire où il est ouvert) facultatifs d'un marchand ; sections facultatives <c>prices</c> (saison, temps, objets,
        /// marchands, pour cent) et <c>shocks</c> (événement du monde et issue sur quelques jours, ou fait connu : stock et prix des objets
        /// touchés). Les versions 2 à 4 restent lues.
        /// Version 6 (sous-lot V2.14, vraies boutiques et grand jour de marché) : <c>shop</c> facultatif d'un marchand (genre de
        /// <see cref="ShopRules.Kinds"/>, enseigne, bâtiment, horaires <c>open</c>/<c>close</c> « HH:MM », jours de repos <c>rest</c>, porte
        /// <c>door</c> « patron » ou « clerk » et son commis <c>clerk</c>) ; <c>bakes</c> (fournées : heure et part, croissantes, 100 % en
        /// tout) et <c>soldOut</c> (rupture du soir) facultatifs ; <c>fresh</c> (suit les fournées) et <c>door</c> (vendue à la porte) facultatifs
        /// d'une offre ; <c>market</c> facultatif d'une règle de prix (seulement pendant le marché) ; section facultative <c>market</c> (fêtes
        /// hebdomadaires du marché, installation et démontage, foule, criées, étals, marchandage). Les versions 2 à 5 restent lues. Un objet
        /// dont l'histoire a besoin (objet tenu, livré ou ramassé) vendu par une boutique à horaires se vend aussi à la porte.</remarks>
        public const int CurrentSchemaVersion = 6;

        /// <summary>Sous-lot V2.14 : version 5, encore lue (sans boutiques ni marché).</summary>
        public const int Schema5 = 5;

        /// <summary>Sous-lot V2.14 : fournées au plus ; étals, criées au plus ; foule au plus ; jours de marché par semaine au plus (décision de Damien : 1 ou 2).</summary>
        public const int MaxBakes = 6, MaxStalls = 24, MaxCries = 16, MaxCrowd = 64, MaxMarketDays = 2;

        /// <summary>Sous-lot V2.14 : coordonnée d'un étal au plus (mètres, en valeur absolue).</summary>
        public const int MaxStallCoordinate = 1000;

        private static readonly Regex StallId = new Regex(@"^stall(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);
        private static readonly Regex BuildingId = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

        /// <summary>Sous-lot V2.10 : version 4, encore lue (sans stock vivant ni fêtes).</summary>
        public const int Schema4 = 4;

        /// <summary>Sous-lot V2.10 : part regagnée par jour et prix de revente par défaut (pour cent).</summary>
        public const int DefaultRestock = 50, DefaultResale = 125;

        /// <summary>Sous-lot V2.10 : unités d'une offre au plus ; bornes d'un pour cent de prix (règle ou contrecoup, et leur produit).</summary>
        public const int MaxStock = 999, MinPercent = 50, MaxPercent = 300;

        /// <summary>Sous-lot V2.10 : règles de prix et contrecoups au plus ; objets ou marchands nommés par une règle au plus ; jours d'un contrecoup au plus.</summary>
        public const int MaxRules = 32, MaxNamed = 32, MaxShockDays = 28;

        /// <summary>Sous-lot V2.10 : issues d'un événement que lit un contrecoup.</summary>
        public static IReadOnlyList<string> Outcomes { get; } = new System.Collections.ObjectModel.ReadOnlyCollection<string>(new[] { "won", "lost", "any" });

        /// <summary>Sous-lot V2.7 : version 3, encore lue (sans <c>faction</c> ni <c>standing</c>).</summary>
        public const int Schema3 = 3;

        /// <summary>Sous-lot V1.9.3b : version précédente, encore lue (sous-ensemble de la version 3, sans <c>fact</c>).</summary>
        public const int PreviousSchemaVersion = 2;

        public const int MaxValues = 1024, MaxMerchants = 32, MaxOffers = 32;

        /// <summary>Prix ou valeur d'une unité, au plus (cuivre : 10 000 or).</summary>
        public const long MaxPrice = 100_000_000;

        private static readonly Regex MerchantId = new Regex(@"^merchant(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);
        private static readonly Regex ShockId = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

        public static bool IsMerchantId(string? id) => id != null && id.Length <= 200 && MerchantId.IsMatch(id);

        public static MerchantsLoadResult Validate(MerchantsDocument document, TextCatalog texts, StoryBook story, ItemCatalog items, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (texts is null) throw new ArgumentNullException(nameof(texts));
            if (story is null) throw new ArgumentNullException(nameof(story));
            if (items is null) throw new ArgumentNullException(nameof(items));
            ValidationReport r = report ?? new ValidationReport();
            // Sous-lot V1.9.3b : la version 2 est encore lue (sous-ensemble de la version 3).
            int supported = document.SchemaVersion == PreviousSchemaVersion || document.SchemaVersion == Schema3 || document.SchemaVersion == Schema4
                || document.SchemaVersion == Schema5 ? document.SchemaVersion.Value : CurrentSchemaVersion;
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, supported, r))
                return new MerchantsLoadResult(r, null);
            // Revue Codex de la PR #91 : version absente ou mal typée, déjà signalée par l'en-tête : refus structuré, jamais d'exception.
            if (!(document.SchemaVersion is int schema)) return new MerchantsLoadResult(r, null);
            MerchantsData? d = document.Data;
            if (d is null) { Missing(r, "$.data", "valeurs des objets et marchands"); return new MerchantsLoadResult(r, null); }

            var values = new Dictionary<string, long>(StringComparer.Ordinal);
            if (d.Values is null) Missing(r, "$.data.values", "valeur de base des objets");
            else if (d.Values.Count > MaxValues) r.Add("$.data.values", ValidationCode.LimitExceeded, $"{d.Values.Count} valeurs, au plus {MaxValues}.");
            else
                for (int i = 0; i < d.Values.Count; i++)
                {
                    string p = $"$.data.values[{i}]";
                    if (d.Values[i] is not ItemValueData v) { Missing(r, p, "objet et valeur"); continue; }
                    bool ok = true;
                    if (v.Item is null) { Missing(r, p + ".item", "objet"); ok = false; }
                    else if (!items.TryGetItem(v.Item, out _)) { r.Add(p + ".item", ValidationCode.UnknownReference, $"objet inconnu « {v.Item} »."); ok = false; }
                    // Même règle que la revue Codex de la PR #44 : un objet valué deux fois est en double même si sa première valeur est invalide.
                    else if (values.ContainsKey(v.Item) || d.Values.Take(i).Any(o => o?.Item == v.Item))
                    { r.Add(p + ".item", ValidationCode.DuplicateId, $"objet « {v.Item} » déjà valué plus haut."); ok = false; }
                    if (v.Value is null) { Missing(r, p + ".value", "valeur en cuivre"); ok = false; }
                    else if (v.Value < 1 || v.Value > MaxPrice) { r.Add(p + ".value", ValidationCode.OutOfRange, $"valeur hors de [1, {MaxPrice}] cuivre."); ok = false; }
                    if (ok) values[v.Item!] = v.Value!.Value;
                }

            var merchants = new List<Merchant>();
            var merchantIds = new HashSet<string>(StringComparer.Ordinal);
            var npcs = new HashSet<string>(StringComparer.Ordinal);
            if (d.Merchants is null) Missing(r, "$.data.merchants", "marchands");
            else if (d.Merchants.Count < 1 || d.Merchants.Count > MaxMerchants)
                r.Add("$.data.merchants", d.Merchants.Count < 1 ? ValidationCode.OutOfRange : ValidationCode.LimitExceeded, $"de 1 à {MaxMerchants} marchands.");
            else
                for (int i = 0; i < d.Merchants.Count; i++)
                {
                    string p = $"$.data.merchants[{i}]";
                    if (d.Merchants[i] is not MerchantData m) { Missing(r, p, "marchand"); continue; }
                    if (CheckMerchant(m, p, texts, story, items, values, merchantIds, npcs, r, schema) is Merchant merchant) merchants.Add(merchant);
                }

            // Sous-lot V2.10 (schéma 5) : prix qui bougent et contrecoups des événements.
            List<PriceRule> prices = CheckPrices(d.Prices, "$.data.prices", items, merchantIds, r, schema);
            List<MarketShock> shocks = CheckShocks(d.Shocks, "$.data.shocks", items, story, merchantIds, r, schema);
            // Sous-lot V2.14 (schéma 6) : le grand jour de marché ; aucun objet dont l'histoire a besoin bloqué par un horaire.
            MarketDay? market = CheckMarket(d.Market, "$.data.market", texts, story, merchants, merchantIds, r, schema);
            CheckDoors(d.Merchants, merchants, story, r);
            if (!r.IsValid || document.ContentVersion is null) return new MerchantsLoadResult(r, null);
            return new MerchantsLoadResult(r, new MerchantBook(document.SchemaVersion!.Value, document.ContentVersion, values, merchants).WithLiving(prices, shocks)
                .WithMarket(market));
        }

        private static bool Since5(string field, string path, ValidationReport r, int schema) => Since(Schema5, field, path, r, schema);

        private static bool Since6(string field, string path, ValidationReport r, int schema) => Since(CurrentSchemaVersion, field, path, r, schema);

        private static bool Since(int version, string field, string path, ValidationReport r, int schema)
        {
            if (schema >= version) return true;
            r.Add(path, ValidationCode.InvalidValue, $"champ « {field} » inconnu en version {schema} du format : version {version} demandée.");
            return false;
        }

        /// <summary>Sous-lot V2.14 : minute d'une heure « HH:MM » obligatoire, ou null (erreur au chemin).</summary>
        private static int? Clock(string? at, string path, string what, ValidationReport r)
        {
            if (at is null) { Missing(r, path, what + " « HH:MM »"); return null; }
            int? minute = ShopRules.MinuteOf(at);
            if (minute is null) r.Add(path, ValidationCode.InvalidValue, $"heure « {at} » mal formée : « HH:MM » de 00:00 à 23:59.");
            return minute;
        }

        private static bool Text(string? key, string path, string what, TextCatalog texts, ValidationReport r)
        {
            if (key is null) { Missing(r, path, what); return false; }
            if (texts.Has(key)) return true;
            r.Add(path, ValidationCode.UnknownReference, $"texte « {key} » absent des textes ({texts.Language}).");
            return false;
        }

        /// <summary>
        /// Sous-lot V2.14 (schéma 6) : la boutique d'un marchand : genre connu, enseigne (texte), bâtiment bien formé (facultatif), horaires
        /// d'ouverture d'une fenêtre non vide, jours de repos (de 0 à 6, une fois chacun, au plus 6 : au moins un jour ouvert), porte
        /// (« patron » ou « clerk », avec le texte du commis pour « clerk »). Avec les moments de fermeture <c>closed</c>, au moins une minute
        /// ouverte dans la semaine.
        /// </summary>
        private static Shop? CheckShop(ShopData x, string p, IReadOnlyList<string> closed, TextCatalog texts, ValidationReport r)
        {
            bool ok = true;
            if (x.Kind is null) { Missing(r, p + ".kind", string.Join(", ", ShopRules.Kinds)); ok = false; }
            else if (!ShopRules.Kinds.Contains(x.Kind)) { r.Add(p + ".kind", ValidationCode.UnknownReference, $"genre de boutique inconnu « {x.Kind} » ; possibles : {string.Join(", ", ShopRules.Kinds)}."); ok = false; }
            ok &= Text(x.Sign, p + ".sign", "enseigne (clé de texte)", texts, r);
            if (x.Building != null && (x.Building.Length > 64 || !BuildingId.IsMatch(x.Building)))
            { r.Add(p + ".building", ValidationCode.InvalidValue, $"bâtiment « {x.Building} » mal formé : minuscules, chiffres et « _ »."); ok = false; }
            int? open = Clock(x.Open, p + ".open", "ouverture", r), close = Clock(x.Close, p + ".close", "fermeture", r);
            if (open is null || close is null) ok = false;
            else if (open == close) { r.Add(p + ".close", ValidationCode.InvalidValue, "fermeture à l'heure de l'ouverture : boutique jamais ouverte."); ok = false; }
            var rest = new List<int>();
            if (x.Rest != null)
            {
                if (x.Rest.Count < 1 || x.Rest.Count >= WorldCalendar.DaysPerWeek)
                { r.Add(p + ".rest", ValidationCode.OutOfRange, $"de 1 à {WorldCalendar.DaysPerWeek - 1} jours de repos (absent : aucun)."); ok = false; }
                else
                    for (int i = 0; i < x.Rest.Count; i++)
                    {
                        int? d = x.Rest[i];
                        if (d is null || d < 0 || d >= WorldCalendar.DaysPerWeek) { r.Add($"{p}.rest[{i}]", ValidationCode.OutOfRange, $"jour de la semaine hors de [0, {WorldCalendar.DaysPerWeek - 1}]."); ok = false; }
                        else if (rest.Contains(d.Value)) { r.Add($"{p}.rest[{i}]", ValidationCode.DuplicateId, $"jour {d} déjà donné."); ok = false; }
                        else rest.Add(d.Value);
                    }
            }
            DoorService door = DoorService.None;
            if (x.Door != null)
            {
                if (x.Door == "patron") door = DoorService.Patron;
                else if (x.Door == "clerk") door = DoorService.Clerk;
                else { r.Add(p + ".door", ValidationCode.InvalidValue, $"porte « {x.Door} » inconnue ; possibles : patron, clerk."); ok = false; }
            }
            if (door == DoorService.Clerk) ok &= Text(x.Clerk, p + ".clerk", "commis (clé de texte)", texts, r);
            else if (x.Clerk != null) { r.Add(p + ".clerk", ValidationCode.InvalidValue, "un commis ne va qu'avec « door »: « clerk »."); ok = false; }
            if (!ok) return null;
            var shop = new Shop(x.Kind!, x.Sign!, x.Building, open!.Value, close!.Value, rest, door, x.Clerk);
            // Au moins une minute ouverte dans la semaine, avec les moments de fermeture.
            bool any = false;
            for (int day = 0; day < WorldCalendar.DaysPerWeek && !any; day++)
                for (int m = 0; m < ShopRules.MinutesPerDay && !any; m += 1)
                    any = shop.IsOpen(day, m) && !closed.Contains(StoryValidator.Phases[PhaseIndex(m)]);
            if (!any) { r.Add(p, ValidationCode.InvalidValue, "boutique jamais ouverte : horaires, jours de repos et moments de fermeture ne laissent aucune minute."); return null; }
            return shop;
        }

        /// <summary>Rang dans <see cref="StoryValidator.Phases"/> du moment de la journée à la minute <paramref name="minute"/>.</summary>
        private static int PhaseIndex(int minute)
        {
            string phase = minute < 5 * 60 ? "night" : minute < 7 * 60 ? "dawn" : minute < 19 * 60 ? "day" : minute < 22 * 60 ? "dusk" : "night";
            return StoryValidator.Phases.ToList().IndexOf(phase);
        }

        /// <summary>
        /// Sous-lot V2.14 (schéma 6) : fournées : de 1 à <see cref="MaxBakes"/>, heures strictement croissantes, parts de 1 à 100 % qui font
        /// 100 % en tout ; rupture du soir après la dernière fournée (même jour).
        /// </summary>
        private static List<Bake>? CheckBakes(List<BakeData?> data, string? soldOut, string p, ValidationReport r, out int soldOutMinute)
        {
            soldOutMinute = 0;
            var bakes = new List<Bake>();
            if (data.Count < 1 || data.Count > MaxBakes) { r.Add(p + ".bakes", ValidationCode.OutOfRange, $"de 1 à {MaxBakes} fournées."); return null; }
            bool ok = true;
            int sum = 0, last = -1;
            for (int i = 0; i < data.Count; i++)
            {
                string bp = $"{p}.bakes[{i}]";
                if (data[i] is not BakeData b) { Missing(r, bp, "fournée"); ok = false; continue; }
                int? at = Clock(b.At, bp + ".at", "heure de la fournée", r);
                bool good = at != null & Percent(b.Percent, bp + ".percent", 1, 100, "part de la fournée", r);
                if (at != null && at <= last) { r.Add(bp + ".at", ValidationCode.InvalidValue, "fournées dans l'ordre des heures, deux à la même heure interdites."); good = false; }
                if (!good) { ok = false; continue; }
                last = at!.Value;
                sum += b.Percent!.Value;
                bakes.Add(new Bake(at.Value, b.Percent.Value));
            }
            if (ok && sum != 100) { r.Add(p + ".bakes", ValidationCode.InvalidValue, $"parts des fournées : {sum} % en tout, 100 % demandés."); ok = false; }
            int? end = Clock(soldOut, p + ".soldOut", "rupture du soir", r);
            if (end is null) return null;
            if (ok && end <= last) { r.Add(p + ".soldOut", ValidationCode.InvalidValue, "rupture du soir avant la dernière fournée."); ok = false; }
            soldOutMinute = end.Value;
            return ok ? bakes : null;
        }

        /// <summary>
        /// Sous-lot V2.14 (schéma 6) : le grand jour de marché : de 1 à <see cref="MaxMarketDays"/> fêtes hebdomadaires de l'histoire, sur des
        /// jours différents ; installation avant le début de chacune, démontage après sa fin ; foule de 0 à <see cref="MaxCrowd"/> ; criées
        /// (textes) ; étals d'identifiants uniques, de genre connu, chacun tenu par un marchand différent (facultatif), ouvert pendant ces
        /// fêtes ; marchandage (palier connu, prix de 50 à 99 %, chance et bonus).
        /// </summary>
        private static MarketDay? CheckMarket(MarketDayData? x, string p, TextCatalog texts, StoryBook story, List<Merchant> merchants, HashSet<string> merchantIds,
            ValidationReport r, int schema)
        {
            if (x is null || !Since6("market", p, r, schema)) return null;
            bool ok = true;
            var festivals = new List<Festival>();
            if (x.Festivals is null) { Missing(r, p + ".festivals", "fêtes hebdomadaires du marché"); ok = false; }
            else if (x.Festivals.Count < 1 || x.Festivals.Count > MaxMarketDays)
            { r.Add(p + ".festivals", ValidationCode.OutOfRange, $"de 1 à {MaxMarketDays} jours de marché par semaine."); ok = false; }
            else
                for (int i = 0; i < x.Festivals.Count; i++)
                {
                    string fp = $"{p}.festivals[{i}]";
                    Festival? f = story.Festivals.FirstOrDefault(y => y.Id == x.Festivals[i]);
                    if (f is null) { r.Add(fp, ValidationCode.UnknownReference, $"fête inconnue « {x.Festivals[i]} »."); ok = false; }
                    else if (f.Weekday is null) { r.Add(fp, ValidationCode.InvalidValue, $"fête « {f.Id} » annuelle : le marché se tient chaque semaine (« weekday »)."); ok = false; }
                    else if (festivals.Any(g => g.Weekday == f.Weekday)) { r.Add(fp, ValidationCode.DuplicateId, $"jour de la semaine {f.Weekday} déjà jour de marché."); ok = false; }
                    else festivals.Add(f);
                }
            int? setup = Clock(x.Setup, p + ".setup", "installation des étals", r), teardown = Clock(x.Teardown, p + ".teardown", "fin du démontage", r);
            if (setup is null || teardown is null) ok = false;
            else
                foreach (Festival f in festivals)
                {
                    if (setup >= f.From) { r.Add(p + ".setup", ValidationCode.InvalidValue, $"installation à {x.Setup}, pas avant l'ouverture de « {f.Id} » ({ShopRules.Clock(f.From)})."); ok = false; }
                    if (teardown <= f.To && f.To < ShopRules.MinutesPerDay)
                    { r.Add(p + ".teardown", ValidationCode.InvalidValue, $"démontage fini à {x.Teardown}, pas après la fin de « {f.Id} » ({ShopRules.Clock(Math.Min(f.To, ShopRules.MinutesPerDay - 1))})."); ok = false; }
                }
            int crowd = x.Crowd ?? 0;
            if (crowd < 0 || crowd > MaxCrowd) { r.Add(p + ".crowd", ValidationCode.OutOfRange, $"foule hors de [0, {MaxCrowd}]."); ok = false; }
            var cries = new List<string>();
            if (x.Cries != null)
            {
                if (x.Cries.Count > MaxCries) { r.Add(p + ".cries", ValidationCode.LimitExceeded, $"au plus {MaxCries} criées."); ok = false; }
                else
                    for (int i = 0; i < x.Cries.Count; i++)
                    {
                        if (!Text(x.Cries[i], $"{p}.cries[{i}]", "criée (clé de texte)", texts, r)) ok = false;
                        else if (cries.Contains(x.Cries[i]!)) { r.Add($"{p}.cries[{i}]", ValidationCode.DuplicateId, "criée déjà donnée."); ok = false; }
                        else cries.Add(x.Cries[i]!);
                    }
            }
            var stalls = new List<MarketStall>();
            if (x.Stalls is null) { Missing(r, p + ".stalls", "étals du marché"); ok = false; }
            else if (x.Stalls.Count < 1 || x.Stalls.Count > MaxStalls) { r.Add(p + ".stalls", ValidationCode.OutOfRange, $"de 1 à {MaxStalls} étals."); ok = false; }
            else
                for (int i = 0; i < x.Stalls.Count; i++)
                {
                    string sp = $"{p}.stalls[{i}]";
                    if (x.Stalls[i] is not StallData s) { Missing(r, sp, "étal"); ok = false; continue; }
                    bool good = true;
                    if (s.Id is null) { Missing(r, sp + ".id", "identifiant « stall.… »"); good = false; }
                    else if (s.Id.Length > 64 || !StallId.IsMatch(s.Id)) { r.Add(sp + ".id", ValidationCode.InvalidValue, $"identifiant « {s.Id} » mal formé : « stall.… »."); good = false; }
                    else if (stalls.Any(o => o.Id == s.Id) || x.Stalls.Take(i).Any(o => o?.Id == s.Id)) { r.Add(sp + ".id", ValidationCode.DuplicateId, $"étal « {s.Id} » déjà décrit."); good = false; }
                    if (s.Kind is null) { Missing(r, sp + ".kind", string.Join(", ", ShopRules.StallKinds)); good = false; }
                    else if (!ShopRules.StallKinds.Contains(s.Kind)) { r.Add(sp + ".kind", ValidationCode.UnknownReference, $"genre d'étal inconnu « {s.Kind} » ; possibles : {string.Join(", ", ShopRules.StallKinds)}."); good = false; }
                    if (s.Position?.X is null || s.Position.Z is null) { Missing(r, sp + ".position", "place [x, z] sur la place du bourg"); good = false; }
                    else if (Math.Abs(s.Position.X.Value.Raw) > MaxStallCoordinate * (long)EdS.Domain.Numerics.Fixed.Scale
                        || Math.Abs(s.Position.Z.Value.Raw) > MaxStallCoordinate * (long)EdS.Domain.Numerics.Fixed.Scale)
                    { r.Add(sp + ".position", ValidationCode.OutOfRange, $"place hors de [-{MaxStallCoordinate}, {MaxStallCoordinate}] m."); good = false; }
                    if (s.Sign != null) good &= Text(s.Sign, sp + ".sign", "enseigne (clé de texte)", texts, r);
                    if (s.Merchant != null)
                    {
                        Merchant? m = merchants.FirstOrDefault(y => y.Id == s.Merchant);
                        if (!merchantIds.Contains(s.Merchant)) { r.Add(sp + ".merchant", ValidationCode.UnknownReference, $"marchand inconnu « {s.Merchant} »."); good = false; }
                        else if (x.Stalls.Take(i).Any(o => o?.Merchant == s.Merchant)) { r.Add(sp + ".merchant", ValidationCode.DuplicateId, $"marchand « {s.Merchant} » tient déjà un étal."); good = false; }
                        else if (m != null && m.Festivals.Count > 0 && !festivals.All(f => m.Festivals.Contains(f.Id)))
                        { r.Add(sp + ".merchant", ValidationCode.InvalidValue, $"marchand « {s.Merchant} » pas ouvert tous les jours de marché (ses fêtes)."); good = false; }
                    }
                    if (good) stalls.Add(new MarketStall(s.Id!, s.Merchant, s.Kind!, new GroundVector(s.Position!.X!.Value, s.Position.Z!.Value), s.Sign));
                    else ok = false;
                }
            HaggleRule? haggle = null;
            if (x.Haggle is HaggleData h)
            {
                string hp = p + ".haggle";
                bool good = true;
                if (h.Standing is null) { Missing(r, hp + ".standing", "palier demandé"); good = false; }
                else if (Reputations.TierIndex(h.Standing) < 0) { r.Add(hp + ".standing", ValidationCode.UnknownReference, $"palier inconnu « {h.Standing} » ; possibles : {string.Join(", ", Reputations.Tiers)}."); good = false; }
                good &= Percent(h.Percent, hp + ".percent", MinPercent, 99, "prix marchandé", r);
                good &= Percent(h.Chance, hp + ".chance", 1, 100, "chance de réussite", r);
                if (h.Bonus != null) good &= Percent(h.Bonus, hp + ".bonus", 0, 100, "chance de plus par palier", r);
                if (good) haggle = new HaggleRule(h.Standing!, h.Percent!.Value, h.Chance!.Value, h.Bonus ?? 0);
                else ok = false;
            }
            return ok ? new MarketDay(festivals.Select(f => f.Id), setup!.Value, teardown!.Value, crowd, cries, stalls, haggle) : null;
        }

        /// <summary>
        /// Sous-lot V2.14 (décision de Damien : les quêtes ne sont jamais bloquées par des horaires) : un objet dont l'histoire a besoin (tenu
        /// pour une condition, livré, ramassé) vendu par une boutique à horaires se vend aussi à la porte (<c>door</c>), et la boutique a
        /// quelqu'un pour répondre.
        /// </summary>
        private static void CheckDoors(List<MerchantData?>? data, List<Merchant> merchants, StoryBook story, ValidationReport r)
        {
            if (data is null) return;
            IEnumerable<StoryCondition> conditions = story.Dialogues.SelectMany(d => d.Entries.SelectMany(e => e.Conditions)
                    .Concat(d.Nodes.SelectMany(n => n.Choices.SelectMany(c => c.Conditions).Concat(n.Lines.SelectMany(l => l.Conditions)))))
                .Concat(story.Quests.SelectMany(q => q.Requires));
            var needed = new HashSet<string>(conditions.Where(c => c.Kind == ConditionKind.HasItem).Select(c => c.Id), StringComparer.Ordinal);
            foreach (Objective o in story.Quests.SelectMany(q => q.Steps).Select(s => s.Objective))
                if (o.Kind == ObjectiveKind.Deliver || o.Kind == ObjectiveKind.Collect) needed.Add(o.Id);
            for (int i = 0; i < data.Count; i++)
            {
                if (!(data[i] is MerchantData md) || merchants.FirstOrDefault(m => m.Id == md.Id) is not Merchant m || m.Shop is null) continue;
                for (int k = 0; k < m.Offers.Count; k++)
                {
                    MerchantOffer o = m.Offers[k];
                    if (!needed.Contains(o.Item) || o.Door) continue;
                    int index = md.Offers?.FindIndex(x => x?.Item == o.Item) ?? k;
                    r.Add($"$.data.merchants[{i}].offers[{index}].door", ValidationCode.InvalidValue,
                        $"objet « {o.Item} » utile à l'histoire, vendu dans une boutique à horaires : à vendre aussi à la porte (« door »: true, et « shop.door »).");
                }
            }
        }

        private static bool Percent(int? value, string path, int min, int max, string what, ValidationReport r)
        {
            if (value is null) { Missing(r, path, what); return false; }
            if (value >= min && value <= max) return true;
            r.Add(path, ValidationCode.OutOfRange, $"{what} hors de [{min}, {max}] %.");
            return false;
        }

        /// <summary>Sous-lot V2.10 : objets ou marchands nommés (vide : tous) : connus, une fois chacun, au plus <see cref="MaxNamed"/>.</summary>
        private static List<string>? Named(List<string?>? list, string path, Func<string, bool> known, string what, ValidationReport r)
        {
            var names = new List<string>();
            if (list is null) return names;
            if (list.Count < 1 || list.Count > MaxNamed) { r.Add(path, ValidationCode.OutOfRange, $"de 1 à {MaxNamed} {what}s (absent : tous)."); return null; }
            bool ok = true;
            for (int i = 0; i < list.Count; i++)
            {
                string? x = list[i];
                if (x is null || !known(x)) { r.Add($"{path}[{i}]", ValidationCode.UnknownReference, $"{what} inconnu « {x} »."); ok = false; }
                else if (names.Contains(x)) { r.Add($"{path}[{i}]", ValidationCode.DuplicateId, $"{what} « {x} » déjà nommé."); ok = false; }
                else names.Add(x);
            }
            return ok ? names : null;
        }

        /// <summary>
        /// Sous-lot V2.10 (schéma 5) : règles de prix : au plus <see cref="MaxRules"/> ; une saison connue et/ou un temps connu (au moins l'un
        /// des deux) ; objets et marchands connus (absents : tous) ; un pour cent de <see cref="MinPercent"/> à <see cref="MaxPercent"/>.
        /// </summary>
        private static List<PriceRule> CheckPrices(List<PriceRuleData?>? data, string path, ItemCatalog items, HashSet<string> merchants, ValidationReport r, int schema)
        {
            var rules = new List<PriceRule>();
            if (data is null || !Since5("prices", path, r, schema)) return rules;
            if (data.Count > MaxRules) { r.Add(path, ValidationCode.LimitExceeded, $"au plus {MaxRules} règles."); return rules; }
            for (int i = 0; i < data.Count; i++)
            {
                string p = $"{path}[{i}]";
                if (data[i] is not PriceRuleData x) { Missing(r, p, "règle de prix"); continue; }
                bool ok = true;
                bool market = x.Market == true;
                if (x.Market != null && !Since6("market", p + ".market", r, schema)) ok = false;
                if (x.Season is null && x.Weather is null && !market)
                { r.Add(p, ValidationCode.MissingField, "une saison (« season »), un temps (« weather ») ou le jour de marché (« market »), ou plusieurs."); ok = false; }
                if (x.Season != null && WorldCalendar.SeasonIndex(x.Season) < 0)
                { r.Add(p + ".season", ValidationCode.UnknownReference, $"saison inconnue « {x.Season} » ; possibles : {string.Join(", ", WorldCalendar.Seasons)}."); ok = false; }
                if (x.Weather != null && !StoryValidator.Weathers.Contains(x.Weather))
                { r.Add(p + ".weather", ValidationCode.UnknownReference, $"temps inconnu « {x.Weather} » ; possibles : {string.Join(", ", StoryValidator.Weathers)}."); ok = false; }
                List<string>? named = Named(x.Items, p + ".items", id => items.TryGetItem(id, out _), "objet", r);
                List<string>? who = Named(x.Merchants, p + ".merchants", merchants.Contains, "marchand", r);
                ok &= Percent(x.Percent, p + ".percent", MinPercent, MaxPercent, "pour cent du prix", r);
                if (ok && named != null && who != null) rules.Add(new PriceRule(x.Season, x.Weather, named, who, x.Percent!.Value, market));
            }
            return rules;
        }

        /// <summary>
        /// Sous-lot V2.10 (schéma 5) : contrecoups : au plus <see cref="MaxRules"/>, identifiants uniques ; soit un événement (clé du registre
        /// « zone.…/événement », issue de <see cref="Outcomes"/>, durée de 1 à <see cref="MaxShockDays"/> jours), soit un fait (« fact.… ») ;
        /// objets et marchands connus (absents : tous) ; stock plein de 0 à 100 %, prix de <see cref="MinPercent"/> à <see cref="MaxPercent"/> %.
        /// </summary>
        private static List<MarketShock> CheckShocks(List<ShockData?>? data, string path, ItemCatalog items, StoryBook story, HashSet<string> merchants, ValidationReport r, int schema)
        {
            var shocks = new List<MarketShock>();
            if (data is null || !Since5("shocks", path, r, schema)) return shocks;
            if (data.Count > MaxRules) { r.Add(path, ValidationCode.LimitExceeded, $"au plus {MaxRules} contrecoups."); return shocks; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < data.Count; i++)
            {
                string p = $"{path}[{i}]";
                if (data[i] is not ShockData x) { Missing(r, p, "contrecoup"); continue; }
                bool ok = true;
                if (x.Id is null) { Missing(r, p + ".id", "identifiant"); ok = false; }
                else if (x.Id.Length > 64 || !ShockId.IsMatch(x.Id)) { r.Add(p + ".id", ValidationCode.InvalidValue, $"identifiant « {x.Id} » mal formé : minuscules, chiffres et « _ »."); ok = false; }
                else if (!ids.Add(x.Id)) { r.Add(p + ".id", ValidationCode.DuplicateId, $"contrecoup « {x.Id} » déjà décrit."); ok = false; }
                if ((x.Event != null ? 1 : 0) + (x.Fact != null ? 1 : 0) + (x.NotFact != null ? 1 : 0) != 1)
                { r.Add(p, ValidationCode.InvalidValue, "un seul de : un événement (« event »), un fait connu (« fact »), un fait pas encore appris (« notFact »)."); ok = false; }
                else if (x.Event != null)
                {
                    if (!EdS.Data.Saves.SaveValidator.IsEventKey(x.Event) || x.Event.Contains("/") == false || x.Event.Split('/').Length != 2)
                    { r.Add(p + ".event", ValidationCode.InvalidValue, $"événement « {x.Event} » mal formé : « zone.<nom>/<événement> » attendu."); ok = false; }
                    if (x.Outcome is null) { Missing(r, p + ".outcome", string.Join(", ", Outcomes)); ok = false; }
                    else if (!Outcomes.Contains(x.Outcome)) { r.Add(p + ".outcome", ValidationCode.InvalidValue, $"issue « {x.Outcome} » inconnue ; possibles : {string.Join(", ", Outcomes)}."); ok = false; }
                    if (x.Days is null) { Missing(r, p + ".days", "jours que dure le contrecoup"); ok = false; }
                    else if (x.Days < 1 || x.Days > MaxShockDays) { r.Add(p + ".days", ValidationCode.OutOfRange, $"durée hors de [1, {MaxShockDays}] jours."); ok = false; }
                }
                else
                {
                    string factField = x.Fact != null ? "fact" : "notFact";
                    if (!StoryValidator.IsFactId(x.Fact ?? x.NotFact)) { r.Add(p + "." + factField, ValidationCode.InvalidValue, $"fait « {x.Fact ?? x.NotFact} » mal formé : « fact.… »."); ok = false; }
                    // Revue Codex de la PR #120 (tour 5) : un fait que le jeu n'apprend jamais rendrait le contrecoup sans effet (fact) ou permanent (notFact).
                    else if (!story.CanLearn(x.Fact ?? x.NotFact))
                    {
                        r.Add(p + "." + factField, ValidationCode.UnknownReference, $"fait « {x.Fact ?? x.NotFact} » jamais appris en jeu : le contrecoup serait "
                            + (x.Fact != null ? "toujours sans effet." : "permanent."));
                        ok = false;
                    }
                    foreach ((string field, object? value) in new (string, object?)[] { ("outcome", x.Outcome), ("days", x.Days) })
                        if (value != null) { r.Add(p + "." + field, ValidationCode.InvalidValue, $"« {field} » ne va qu'avec un événement."); ok = false; }
                }
                List<string>? named = Named(x.Items, p + ".items", id => items.TryGetItem(id, out _), "objet", r);
                List<string>? who = Named(x.Merchants, p + ".merchants", merchants.Contains, "marchand", r);
                ok &= Percent(x.StockPercent, p + ".stockPercent", 0, 100, "stock plein", r);
                ok &= Percent(x.PricePercent, p + ".pricePercent", MinPercent, MaxPercent, "pour cent du prix", r);
                if (ok && named != null && who != null)
                    shocks.Add(new MarketShock(x.Id!, x.Event, x.Outcome ?? "any", x.Days ?? 0, x.Fact, named, who, x.StockPercent!.Value, x.PricePercent!.Value, x.NotFact));
            }
            return shocks;
        }

        private static Merchant? CheckMerchant(MerchantData m, string p, TextCatalog texts, StoryBook story, ItemCatalog items, IReadOnlyDictionary<string, long> values,
            HashSet<string> merchantIds, HashSet<string> npcs, ValidationReport r, int schema)
        {
            bool ok = true;
            if (m.Id is null) { Missing(r, p + ".id", "identifiant « merchant.… »"); ok = false; }
            else if (!IsMerchantId(m.Id)) { r.Add(p + ".id", ValidationCode.InvalidValue, $"identifiant « {m.Id} » mal formé : « merchant.… »."); ok = false; }
            else if (!merchantIds.Add(m.Id)) { r.Add(p + ".id", ValidationCode.DuplicateId, $"marchand « {m.Id} » déjà décrit."); ok = false; }
            // Revue Codex de la PR #120 : la ligne de stock « marchand/objet » (sauvegarde v16) doit tenir pour chaque objet valué.
            else if (values.Keys.Any(item => !EdS.Data.Saves.SavedStock.IsKey(EdS.Data.Saves.SavedStock.KeyOf(m.Id, item))))
            {
                r.Add(p + ".id", ValidationCode.InvalidValue,
                    $"identifiant « {m.Id} » trop long : « {m.Id}/item.… » dépasserait {EdS.Data.Saves.SavedStock.MaxKeyLength} caractères dans une sauvegarde.");
                ok = false;
            }
            if (m.Npc is null) { Missing(r, p + ".npc", "PNJ de l'histoire"); ok = false; }
            else if (!story.HasNpc(m.Npc)) { r.Add(p + ".npc", ValidationCode.UnknownReference, $"PNJ inconnu « {m.Npc} »."); ok = false; }
            else if (!npcs.Add(m.Npc)) { r.Add(p + ".npc", ValidationCode.DuplicateId, $"PNJ « {m.Npc} » tient déjà une autre boutique."); ok = false; }
            if (m.Greeting is null) { Missing(r, p + ".greeting", "clé de texte « text.… »"); ok = false; }
            else if (!texts.Has(m.Greeting)) { r.Add(p + ".greeting", ValidationCode.UnknownReference, $"texte « {m.Greeting} » absent des textes ({texts.Language})."); ok = false; }
            if (m.SellPercent is null) { Missing(r, p + ".sellPercent", "part de la valeur payée à la revente, en pour cent"); ok = false; }
            else if (m.SellPercent < 1 || m.SellPercent > 100) { r.Add(p + ".sellPercent", ValidationCode.OutOfRange, "part hors de [1, 100] %."); ok = false; }

            var closed = new List<string>();
            if (m.Closed is null) { Missing(r, p + ".closed", "moments de fermeture, éventuellement aucun"); ok = false; }
            else
            {
                for (int k = 0; k < m.Closed.Count; k++)
                {
                    string cp = $"{p}.closed[{k}]";
                    string? phase = m.Closed[k];
                    if (phase is null || !StoryValidator.Phases.Contains(phase))
                    { r.Add(cp, ValidationCode.UnknownReference, $"moment de la journée inconnu « {phase} » ; possibles : {string.Join(", ", StoryValidator.Phases)}."); ok = false; }
                    else if (closed.Contains(phase)) { r.Add(cp, ValidationCode.DuplicateId, $"moment « {phase} » déjà donné."); ok = false; }
                    else closed.Add(phase);
                }
                if (ok && closed.Count == StoryValidator.Phases.Count) { r.Add(p + ".closed", ValidationCode.InvalidValue, "boutique toujours fermée."); ok = false; }
            }

            var offers = new List<MerchantOffer>();
            if (m.Offers is null) { Missing(r, p + ".offers", "objets en vente"); ok = false; }
            else if (m.Offers.Count < 1 || m.Offers.Count > MaxOffers)
            { r.Add(p + ".offers", m.Offers.Count < 1 ? ValidationCode.OutOfRange : ValidationCode.LimitExceeded, $"de 1 à {MaxOffers} offres."); ok = false; }
            else
                for (int k = 0; k < m.Offers.Count; k++)
                {
                    string op = $"{p}.offers[{k}]";
                    if (m.Offers[k] is not OfferData o) { Missing(r, op, "offre"); ok = false; continue; }
                    bool offerOk = true;
                    long value = 0;
                    if (o.Item is null) { Missing(r, op + ".item", "objet"); offerOk = false; }
                    else if (!items.TryGetItem(o.Item, out _)) { r.Add(op + ".item", ValidationCode.UnknownReference, $"objet inconnu « {o.Item} »."); offerOk = false; }
                    else if (!values.TryGetValue(o.Item, out value)) { r.Add(op + ".item", ValidationCode.UnknownReference, $"objet « {o.Item} » sans valeur de base (values)."); offerOk = false; }
                    else if (m.Offers.Take(k).Any(x => x?.Item == o.Item)) { r.Add(op + ".item", ValidationCode.DuplicateId, $"objet « {o.Item} » déjà en vente chez ce marchand."); offerOk = false; }
                    if (o.Price is null) { Missing(r, op + ".price", "prix d'une unité en cuivre"); offerOk = false; }
                    else if (o.Price < 1 || o.Price > MaxPrice) { r.Add(op + ".price", ValidationCode.OutOfRange, $"prix hors de [1, {MaxPrice}] cuivre."); offerOk = false; }
                    // Contrat de l'économie : jamais sous la valeur de base, donc jamais racheté plus cher qu'il n'a été vendu (part ≤ 100 %).
                    else if (offerOk && o.Price < value)
                    { r.Add(op + ".price", ValidationCode.InvalidValue, $"prix {o.Price} sous la valeur de base {value} : la revente rapporterait plus que l'achat."); offerOk = false; }
                    if (o.Always is null) { Missing(r, op + ".always", "toujours en vente (vrai) ou certains jours (faux)"); offerOk = false; }
                    // Sous-lot V1.7 : rang demandé pour l'offre, au moins celui de la boutique (sinon il ne servirait à rien).
                    if (o.Rank != null)
                    {
                        if (!GuildRanks.IsRank(o.Rank)) { r.Add(op + ".rank", ValidationCode.UnknownReference, $"rang inconnu « {o.Rank} » ; possibles : {GuildRanks.Known}."); offerOk = false; }
                        else if (m.Rank != null && GuildRanks.IsRank(m.Rank) && GuildRanks.IndexOf(o.Rank) <= GuildRanks.IndexOf(m.Rank))
                        { r.Add(op + ".rank", ValidationCode.InvalidValue, $"rang « {o.Rank} » pas au-dessus de celui de la boutique (« {m.Rank} ») : à retirer."); offerOk = false; }
                    }
                    // Sous-lot V2.10 (schéma 5) : stock plein de l'offre.
                    if (o.Stock != null)
                    {
                        if (!Since5("stock", op + ".stock", r, schema)) offerOk = false;
                        else if (o.Stock < 1 || o.Stock > MaxStock) { r.Add(op + ".stock", ValidationCode.OutOfRange, $"stock hors de [1, {MaxStock}] (absent : sans limite)."); offerOk = false; }
                    }
                    // Sous-lot V2.14 (schéma 6) : offre fraîche (fournées, stock fini) et vendue à la porte (boutique avec quelqu'un à la porte).
                    if (o.Fresh != null && !Since6("fresh", op + ".fresh", r, schema)) offerOk = false;
                    else if (o.Fresh == true && (m.Bakes is null || o.Stock is null))
                    { r.Add(op + ".fresh", ValidationCode.InvalidValue, "une offre fraîche demande des fournées (« bakes ») et un stock (« stock »)."); offerOk = false; }
                    if (o.Door != null && !Since6("door", op + ".door", r, schema)) offerOk = false;
                    else if (o.Door == true && (m.Shop?.Door is null))
                    { r.Add(op + ".door", ValidationCode.InvalidValue, "une offre à la porte demande une boutique avec quelqu'un à la porte (« shop.door »)."); offerOk = false; }
                    if (offerOk) offers.Add(new MerchantOffer(o.Item!, o.Price!.Value, o.Always!.Value, o.Rank, o.Stock).WithShop(o.Fresh == true, o.Door == true));
                    else ok = false;
                }

            int rotating = m.Offers?.Count(o => o?.Always == false) ?? 0;
            if (m.Daily is null) { Missing(r, p + ".daily", "offres tirées chaque jour"); ok = false; }
            else if (m.Daily < 0 || m.Daily > rotating) { r.Add(p + ".daily", ValidationCode.OutOfRange, $"offres tirées chaque jour hors de [0, {rotating}] (offres non permanentes)."); ok = false; }
            else if (ok && m.Daily == 0 && offers.All(o => !o.Always)) { r.Add(p + ".daily", ValidationCode.InvalidValue, "aucun objet en vente : ni offre permanente, ni offre tirée."); ok = false; }
            // Sous-lot V1.7 : rang de la guilde demandé pour ouvrir la boutique.
            if (m.Rank != null && !GuildRanks.IsRank(m.Rank)) { r.Add(p + ".rank", ValidationCode.UnknownReference, $"rang inconnu « {m.Rank} » ; possibles : {GuildRanks.Known}."); ok = false; }
            // Sous-lot V1.9.3b (schéma 3) : fait demandé pour ouvrir la boutique, posé quelque part dans l'histoire (sinon jamais ouverte).
            if (m.Fact != null)
            {
                if (schema < Schema3)
                { r.Add(p + ".fact", ValidationCode.InvalidValue, $"champ « fact » inconnu en version {schema} du format : version {Schema3} demandée."); ok = false; }
                else if (!StoryValidator.IsFactId(m.Fact)) { r.Add(p + ".fact", ValidationCode.InvalidValue, $"fait « {m.Fact} » mal formé : « fact.… »."); ok = false; }
                else if (!FactsSet(story).Contains(m.Fact))
                { r.Add(p + ".fact", ValidationCode.UnknownReference, $"fait « {m.Fact} » jamais posé par l'histoire (aucun setFact) : la boutique ne s'ouvrirait jamais."); ok = false; }
            }
            // Sous-lot V2.7 (schéma 4) : faction et palier demandé.
            foreach ((string field, string? value) in new[] { ("faction", m.Faction), ("standing", m.Standing) })
                if (value != null && schema < Schema4)
                { r.Add(p + "." + field, ValidationCode.InvalidValue, $"champ « {field} » inconnu en version {schema} du format : version {Schema4} demandée."); ok = false; }
            if (m.Faction != null && !Reputations.IsFaction(m.Faction))
            { r.Add(p + ".faction", ValidationCode.UnknownReference, $"faction inconnue « {m.Faction} » ; possibles : {string.Join(", ", Reputations.Factions)}."); ok = false; }
            if (m.Standing != null && Reputations.TierIndex(m.Standing) < 0)
            { r.Add(p + ".standing", ValidationCode.UnknownReference, $"palier inconnu « {m.Standing} » ; possibles : {string.Join(", ", Reputations.Tiers)}."); ok = false; }
            // Sous-lot V2.10 (schéma 5) : rythme du stock, prix de revente, étal ambulant.
            if (m.Restock != null && (!Since5("restock", p + ".restock", r, schema) || !Percent(m.Restock, p + ".restock", 1, 100, "part regagnée par jour", r))) ok = false;
            if (m.Resale != null && (!Since5("resale", p + ".resale", r, schema) || !Percent(m.Resale, p + ".resale", 100, MaxPercent, "prix de revente", r))) ok = false;
            var festivals = new List<string>();
            if (m.Festivals != null)
            {
                if (!Since5("festivals", p + ".festivals", r, schema)) ok = false;
                else if (m.Festivals.Count < 1) { r.Add(p + ".festivals", ValidationCode.OutOfRange, "au moins une fête (absent : toujours ouvert)."); ok = false; }
                else if (Named(m.Festivals, p + ".festivals", story.HasFestival, "fête", r) is List<string> f) festivals = f;
                else ok = false;
            }
            // Sous-lot V2.14 (schéma 6) : vraie boutique et fournées.
            Shop? shop = null;
            if (m.Shop != null)
            {
                if (!Since6("shop", p + ".shop", r, schema)) ok = false;
                else if (festivals.Count > 0) { r.Add(p + ".shop", ValidationCode.InvalidValue, "un étal ambulant (« festivals ») n'a pas de boutique."); ok = false; }
                else if ((shop = CheckShop(m.Shop, p + ".shop", closed, texts, r)) is null) ok = false;
            }
            List<Bake>? bakes = new List<Bake>();
            int soldOut = 0;
            if (m.Bakes != null)
            {
                if (!Since6("bakes", p + ".bakes", r, schema)) ok = false;
                else if (m.Offers?.Any(o => o?.Fresh == true) != true) { r.Add(p + ".bakes", ValidationCode.InvalidValue, "des fournées sans offre fraîche (« fresh »)."); ok = false; }
                else if ((bakes = CheckBakes(m.Bakes, m.SoldOut, p, r, out soldOut)) is null) ok = false;
            }
            else if (m.SoldOut != null) { r.Add(p + ".soldOut", ValidationCode.InvalidValue, "« soldOut » ne va qu'avec des fournées (« bakes »)."); ok = false; }
            if (shop != null && shop.Door != DoorService.None && !offers.Any(o => o.Door))
            { r.Add(p + ".shop.door", ValidationCode.InvalidValue, "quelqu'un à la porte, mais aucune offre à la porte (« door »: true)."); ok = false; }
            return ok ? new Merchant(m.Id!, m.Npc!, m.Greeting!, m.SellPercent!.Value, closed, m.Daily!.Value, offers, m.Rank, m.Fact, m.Faction ?? "villagers", m.Standing)
                .WithLiving(m.Restock ?? DefaultRestock, m.Resale ?? DefaultResale, festivals).WithShop(shop, bakes ?? new List<Bake>(), soldOut) : null;
        }

        /// <summary>Sous-lot V1.9.3b : faits posés par l'histoire (effets <c>setFact</c> des réponses, des étapes et des quêtes).</summary>
        private static HashSet<string> FactsSet(StoryBook story)
        {
            IEnumerable<StoryEffect> effects = story.Dialogues.SelectMany(d => d.Nodes).SelectMany(n => n.Choices).SelectMany(c => c.Effects)
                .Concat(story.Quests.SelectMany(q => q.Steps).SelectMany(s => s.Effects))
                .Concat(story.Quests.SelectMany(q => q.Effects));
            return new HashSet<string>(effects.Where(e => e.Kind == EffectKind.SetFact).Select(e => e.Id), StringComparer.Ordinal);
        }

        private static void Missing(ValidationReport report, string path, string what)
        {
            if (!report.HasErrorAtOrAbove(path)) report.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({what}).");
        }
    }
}
