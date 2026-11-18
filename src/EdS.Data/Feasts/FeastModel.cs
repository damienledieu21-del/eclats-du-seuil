using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Items;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Domain.Collections;
using EdS.Domain.Numerics;

namespace EdS.Data.Feasts
{
    // --- Documents (avant validation) ------------------------------------------------------------------

    /// <summary>
    /// Les 12 fêtes d'Orsédia (sous-lot F1) : le catalogue des fêtes tel que lu (<c>kind: "feasts"</c>, <c>data/v0/world/feasts.json</c>) :
    /// les 108 références du dossier des fêtes (décorations, costumes, plats, monnaies, récompenses), les titres, les activités qui rapportent
    /// la monnaie de chaque fête, le marchand de fête et les montants (proposition de Claude, à régler par Damien).
    /// </summary>
    public sealed class FeastDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
        public FeastData? Data { get; set; }
    }

    public sealed class FeastData
    {
        public FeastWalletData? Wallet { get; set; }
        public int? MaxDishes { get; set; }
        public string? Merchant { get; set; }
        public List<FeastEntryData?>? Feasts { get; set; }
    }

    public sealed class FeastWalletData
    {
        public int? Cap { get; set; }
    }

    public sealed class FeastEntryData
    {
        public int? Number { get; set; }
        public string? Festival { get; set; }
        public string? Motto { get; set; }
        public FeastTitleData? Title { get; set; }
        public List<FeastActivityData?>? Activities { get; set; }
        public int? RewardAfter { get; set; }
        public List<FeastRefData?>? Refs { get; set; }
    }

    public sealed class FeastTitleData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    public sealed class FeastActivityData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public int? Coins { get; set; }
        public string? Host { get; set; }
        public string? Contest { get; set; }
    }

    public sealed class FeastRefData
    {
        public string? Code { get; set; }
        public string? Category { get; set; }
        public string? Name { get; set; }
        public FeastSizeData? Size { get; set; }
        public string? Image { get; set; }
        public string? Icon { get; set; }
        public string? Rarity { get; set; }
        public int? Price { get; set; }
    }

    public sealed class FeastSizeData
    {
        public Fixed? X { get; set; }
        public Fixed? Y { get; set; }
        public Fixed? Z { get; set; }
    }

    // --- Modèle validé ---------------------------------------------------------------------------------

    /// <summary>Catégorie d'une référence du dossier des fêtes (D, C, P, M, R dans son code).</summary>
    public enum FeastCategory
    {
        /// <summary>Décoration (D01 à D05) : se pose dans le village ; cosmétique.</summary>
        Decoration = 0,

        /// <summary>Costume (C01) : surcouche portée sur les vêtements de tous les jours ; cosmétique, de rareté Commune.</summary>
        Costume = 1,

        /// <summary>Plat de fête (P01) : cosmétique, sans effet.</summary>
        Dish = 2,

        /// <summary>Monnaie de la fête (M01) : liée au personnage, ni échangeable ni convertible (décision de Damien du 17 novembre 2026).</summary>
        Currency = 3,

        /// <summary>Récompense annuelle (R01) : cosmétique, de rareté Rare (décision de Damien du 17 novembre 2026).</summary>
        Reward = 4,
    }

    /// <summary>Enveloppe d'une référence, en mètres (X largeur vue de face, Y profondeur, Z hauteur ; les cotes écrites font foi).</summary>
    public readonly struct FeastSize
    {
        public Fixed X { get; }
        public Fixed Y { get; }
        public Fixed Z { get; }

        public FeastSize(Fixed x, Fixed y, Fixed z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    /// <summary>
    /// Une des 108 références du dossier des fêtes (« F04_D01 ») : catégorie, nom, enveloppe, image, icône (plat, monnaie et récompense),
    /// rareté (toutes sauf la monnaie) et prix en monnaie de la fête (toutes sauf la monnaie). Cosmétique seulement : aucune statistique.
    /// </summary>
    public sealed class FeastRef
    {
        public string Code { get; }

        /// <summary>Numéro de la fête (1 à 12, janvier à décembre).</summary>
        public int Feast { get; }

        public FeastCategory Category { get; }

        /// <summary>Clé du nom affiché.</summary>
        public string Name { get; }

        public FeastSize Size { get; }

        /// <summary>Image de référence, relative au dossier de la fête (« Objets/… .png »).</summary>
        public string Image { get; }

        /// <summary>Icône 627 × 627 (« Icones/… .png ») ; null pour les décorations et le costume.</summary>
        public string? Icon { get; }

        /// <summary>Rareté ; null pour la monnaie.</summary>
        public ItemRarity? Rarity { get; }

        /// <summary>Prix chez le marchand de fête, en monnaie de la fête ; null pour la monnaie.</summary>
        public int? Price { get; }

        internal FeastRef(string code, int feast, FeastCategory category, string name, FeastSize size, string image, string? icon, ItemRarity? rarity, int? price)
        {
            Code = code;
            Feast = feast;
            Category = category;
            Name = name;
            Size = size;
            Image = image;
            Icon = icon;
            Rarity = rarity;
            Price = price;
        }
    }

    /// <summary>
    /// Une activité d'une fête : elle rapporte <see cref="Coins"/> pièces de la monnaie de la fête. Soit un concours de l'histoire
    /// (<see cref="Contest"/> : clé « fête » ou « fête.piste » fermée par un effet <c>contest</c>, une fois par édition), soit une activité
    /// auprès d'un invité de la fête (<see cref="Host"/>, une fois par jour de fête).
    /// </summary>
    public sealed class FeastActivity
    {
        public string Id { get; }
        public string Name { get; }
        public int Coins { get; }
        public string? Host { get; }
        public string? Contest { get; }

        internal FeastActivity(string id, string name, int coins, string? host, string? contest)
        {
            Id = id;
            Name = name;
            Coins = coins;
            Host = host;
            Contest = contest;
        }
    }

    /// <summary>
    /// Une des 12 fêtes d'Orsédia : la fête de l'histoire (<see cref="Festival"/>), son accroche, son titre (gagné avec la récompense de
    /// l'année, décision de Damien du 17 novembre 2026), ses activités et ses neuf références (cinq décorations, un costume, un plat, une
    /// monnaie, une récompense).
    /// </summary>
    public sealed class Feast
    {
        public int Number { get; }
        public string Festival { get; }
        public string Motto { get; }
        public string TitleId { get; }
        public string TitleName { get; }
        public IReadOnlyList<FeastActivity> Activities { get; }

        /// <summary>
        /// Activités différentes à faire dans une même édition pour gagner la fête (la récompense de l'année et le titre) ; 0 pour une fête
        /// sans activité (fête du calendrier seulement).
        /// </summary>
        public int RewardAfter { get; }

        public IReadOnlyList<FeastRef> Refs { get; }

        internal Feast(int number, string festival, string motto, string titleId, string titleName, IEnumerable<FeastActivity> activities, int rewardAfter,
            IEnumerable<FeastRef> refs)
        {
            Number = number;
            Festival = festival;
            Motto = motto;
            TitleId = titleId;
            TitleName = titleName;
            Activities = Frozen.List(activities, nameof(activities));
            RewardAfter = rewardAfter;
            Refs = Frozen.List(refs, nameof(refs));
        }

        public FeastRef Currency => Refs.Single(r => r.Category == FeastCategory.Currency);
        public FeastRef Reward => Refs.Single(r => r.Category == FeastCategory.Reward);
        public FeastRef Costume => Refs.Single(r => r.Category == FeastCategory.Costume);
        public FeastRef Dish => Refs.Single(r => r.Category == FeastCategory.Dish);
        public IEnumerable<FeastRef> Decorations => Refs.Where(r => r.Category == FeastCategory.Decoration);

        /// <summary>Activité <paramref name="id"/>, ou null.</summary>
        public FeastActivity? Activity(string id) => Activities.FirstOrDefault(a => a.Id == id);
    }

    /// <summary>
    /// Les 12 fêtes d'Orsédia, validées et immuables : plafond du portefeuille de chaque fête, plats gardés au plus, marchand de fête, les fêtes
    /// et leurs références.
    /// </summary>
    public sealed class FeastBook
    {
        private readonly Dictionary<string, Feast> _byFestival;
        private readonly Dictionary<string, FeastRef> _byCode;

        public int SchemaVersion { get; }
        public string ContentVersion { get; }

        /// <summary>Monnaie d'une fête gardée au plus par personnage (un gain s'arrête au plafond).</summary>
        public int WalletCap { get; }

        /// <summary>Plats d'une fête gardés au plus par personnage.</summary>
        public int MaxDishes { get; }

        /// <summary>Le marchand de fête (PNJ de passage, là pendant chaque fête qui a du contenu).</summary>
        public string Merchant { get; }

        public IReadOnlyList<Feast> Feasts { get; }

        internal FeastBook(int schemaVersion, string contentVersion, int walletCap, int maxDishes, string merchant, IEnumerable<Feast> feasts)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            WalletCap = walletCap;
            MaxDishes = maxDishes;
            Merchant = merchant;
            Feasts = Frozen.List(feasts.OrderBy(f => f.Number), nameof(feasts));
            _byFestival = Feasts.ToDictionary(f => f.Festival, StringComparer.Ordinal);
            _byCode = Feasts.SelectMany(f => f.Refs).ToDictionary(r => r.Code, StringComparer.Ordinal);
        }

        /// <summary>Toutes les références (108), dans l'ordre des fêtes puis du dossier.</summary>
        public IEnumerable<FeastRef> Refs => Feasts.SelectMany(f => f.Refs);

        /// <summary>La fête d'Orsédia de la fête de l'histoire <paramref name="festival"/>, ou null (fête de la semaine, par exemple).</summary>
        public Feast? Of(string? festival) => festival != null && _byFestival.TryGetValue(festival, out Feast f) ? f : null;

        public Feast Feast(string festival) => Of(festival) ?? throw new ArgumentException($"Fête « {festival} » sans contenu de fête.", nameof(festival));

        /// <summary>Référence <paramref name="code"/>, ou null.</summary>
        public FeastRef? Ref(string? code) => code != null && _byCode.TryGetValue(code, out FeastRef r) ? r : null;

        /// <summary>La fête dont le titre est <paramref name="title"/>, ou null.</summary>
        public Feast? ByTitle(string? title) => title is null ? null : Feasts.FirstOrDefault(f => f.TitleId == title);
    }

    public sealed class FeastLoadResult
    {
        public ValidationReport Report { get; }
        public FeastBook? Book { get; }
        public bool IsValid => Book != null;

        internal FeastLoadResult(ValidationReport report, FeastBook? book)
        {
            Report = report;
            Book = book;
        }
    }

    /// <summary>
    /// Les 12 fêtes d'Orsédia : validateur unique du catalogue des fêtes (<c>kind: "feasts"</c>), contre l'histoire (fêtes datées, invités,
    /// concours, marchand) et les textes. Erreurs avec chemin précis.
    /// </summary>
    public static class FeastValidator
    {
        public const string Kind = "feasts";

        /// <summary>Version 1 (sous-lot F1, 17 novembre 2026) : <c>wallet</c>, <c>maxDishes</c>, <c>merchant</c>, <c>feasts</c>.</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>Une fête par mois.</summary>
        public const int FeastCount = 12;

        /// <summary>Décorations par fête (D01 à D05) ; un costume, un plat, une monnaie et une récompense.</summary>
        public const int Decorations = 5, RefsPerFeast = 9;

        /// <summary>Bornes techniques : plafond du portefeuille, plats, activités, prix et gains.</summary>
        public const int MaxWalletCap = 99_999, MaxDishesCap = 99, MaxActivities = 8;

        /// <summary>Plus grande cote d'une référence, en mètres (borne de sûreté ; la plus grande du dossier fait 3,6 m).</summary>
        public static readonly Fixed MaxSize = Fixed.FromInt(20);

        private static readonly Regex Code = new Regex(@"^F(0[1-9]|1[0-2])_([DCPMR])(0[1-9])\z", RegexOptions.CultureInvariant);
        private static readonly Regex TitleId = new Regex(@"^title\.fete\.[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);
        private static readonly Regex LocalId = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);
        private static readonly Regex ImageFile = new Regex(@"^Objets/(?<code>F(0[1-9]|1[0-2])_[DCPMR]0[1-9])_(?<slug>[a-z0-9]+(_[a-z0-9]+)*)\.png\z", RegexOptions.CultureInvariant);

        private static readonly Dictionary<string, FeastCategory> Categories = new Dictionary<string, FeastCategory>(StringComparer.Ordinal)
        {
            ["decoration"] = FeastCategory.Decoration,
            ["costume"] = FeastCategory.Costume,
            ["dish"] = FeastCategory.Dish,
            ["currency"] = FeastCategory.Currency,
            ["reward"] = FeastCategory.Reward,
        };

        private static readonly Dictionary<char, FeastCategory> Letters = new Dictionary<char, FeastCategory>
        {
            ['D'] = FeastCategory.Decoration,
            ['C'] = FeastCategory.Costume,
            ['P'] = FeastCategory.Dish,
            ['M'] = FeastCategory.Currency,
            ['R'] = FeastCategory.Reward,
        };

        private static readonly Dictionary<string, ItemRarity> Rarities = new Dictionary<string, ItemRarity>(StringComparer.Ordinal)
        {
            ["common"] = ItemRarity.Common,
            ["rare"] = ItemRarity.Rare,
        };

        public static bool IsCode(string? code) => code != null && Code.IsMatch(code);

        public static bool IsTitle(string? id) => id != null && id.Length <= 200 && TitleId.IsMatch(id);

        public static bool IsActivityId(string? id) => id != null && id.Length <= 32 && LocalId.IsMatch(id);

        public static FeastLoadResult Validate(FeastDocument document, StoryBook story, TextCatalog texts, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (story is null) throw new ArgumentNullException(nameof(story));
            if (texts is null) throw new ArgumentNullException(nameof(texts));
            ValidationReport r = report ?? new ValidationReport();
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, CurrentSchemaVersion, r))
                return new FeastLoadResult(r, null);
            FeastData? d = document.Data;
            if (d is null) { Missing(r, "$.data", "wallet, maxDishes, merchant, feasts"); return new FeastLoadResult(r, null); }

            int cap = 0;
            if (d.Wallet is null) Missing(r, "$.data.wallet", "portefeuille : cap");
            else if (d.Wallet.Cap is null) Missing(r, "$.data.wallet.cap", "monnaie d'une fête gardée au plus");
            else if (d.Wallet.Cap < 1 || d.Wallet.Cap > MaxWalletCap) r.Add("$.data.wallet.cap", ValidationCode.OutOfRange, $"plafond hors de [1, {MaxWalletCap}].");
            else cap = d.Wallet.Cap.Value;

            int dishes = 0;
            if (d.MaxDishes is null) Missing(r, "$.data.maxDishes", "plats d'une fête gardés au plus");
            else if (d.MaxDishes < 1 || d.MaxDishes > MaxDishesCap) r.Add("$.data.maxDishes", ValidationCode.OutOfRange, $"plats hors de [1, {MaxDishesCap}].");
            else dishes = d.MaxDishes.Value;

            Npc? merchant = null;
            if (d.Merchant is null) Missing(r, "$.data.merchant", "PNJ marchand de fête");
            else if (!story.HasNpc(d.Merchant)) r.Add("$.data.merchant", ValidationCode.UnknownReference, $"PNJ inconnu « {d.Merchant} ».");
            else if ((merchant = story.Npc(d.Merchant)).Festivals.Count == 0)
            { r.Add("$.data.merchant", ValidationCode.InvalidValue, $"« {d.Merchant} » n'est pas un PNJ de passage (« festivals ») : le marchand de fête vient pour les fêtes."); merchant = null; }

            var feasts = new List<Feast>();
            if (d.Feasts is null) Missing(r, "$.data.feasts", "les 12 fêtes");
            else if (d.Feasts.Count != FeastCount) r.Add("$.data.feasts", ValidationCode.OutOfRange, $"{d.Feasts.Count} fêtes : {FeastCount} attendues, une par mois.");
            else
            {
                var numbers = new HashSet<int>();
                var festivals = new HashSet<string>(StringComparer.Ordinal);
                var titles = new HashSet<string>(StringComparer.Ordinal);
                var codes = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < d.Feasts.Count; i++)
                {
                    string p = $"$.data.feasts[{i}]";
                    if (d.Feasts[i] is not FeastEntryData x) { Missing(r, p, "fête"); continue; }
                    if (CheckFeast(x, p, story, texts, cap, merchant, numbers, festivals, titles, codes, r) is Feast f) feasts.Add(f);
                }
            }

            if (!r.IsValid || document.ContentVersion is null || merchant is null) return new FeastLoadResult(r, null);
            return new FeastLoadResult(r, new FeastBook(document.SchemaVersion!.Value, document.ContentVersion, cap, dishes, merchant.Id, feasts));
        }

        private static Feast? CheckFeast(FeastEntryData x, string p, StoryBook story, TextCatalog texts, int cap, Npc? merchant, HashSet<int> numbers,
            HashSet<string> festivals, HashSet<string> titles, HashSet<string> codes, ValidationReport r)
        {
            int errors = r.Errors.Count;
            int number = 0;
            if (x.Number is null) Missing(r, p + ".number", "numéro de la fête (1 à 12, son mois)");
            else if (x.Number < 1 || x.Number > FeastCount) r.Add(p + ".number", ValidationCode.OutOfRange, $"numéro hors de [1, {FeastCount}].");
            else if (!numbers.Add(x.Number.Value)) r.Add(p + ".number", ValidationCode.DuplicateId, $"fête n° {x.Number} déjà décrite.");
            else number = x.Number.Value;

            Festival? festival = null;
            if (x.Festival is null) Missing(r, p + ".festival", "fête de l'histoire « festival.… »");
            else if (!story.HasFestival(x.Festival)) r.Add(p + ".festival", ValidationCode.UnknownReference, $"fête inconnue de l'histoire « {x.Festival} ».");
            else if (!festivals.Add(x.Festival)) r.Add(p + ".festival", ValidationCode.DuplicateId, $"fête « {x.Festival} » déjà décrite.");
            else
            {
                festival = story.Festival(x.Festival);
                if (!festival.HasDate || festival.Weekday != null)
                { r.Add(p + ".festival", ValidationCode.InvalidValue, $"« {x.Festival} » n'est pas une fête datée de l'année (date réelle attendue)."); festival = null; }
                else if (number > 0 && festival.Month != number)
                { r.Add(p + ".festival", ValidationCode.InvalidValue, $"« {x.Festival} » tombe au mois {festival.Month} : la fête n° {number} est celle du mois {number}."); festival = null; }
            }

            Text(x.Motto, p + ".motto", "accroche de la fête", texts, r);

            const string tp = ".title";
            if (x.Title is null) Missing(r, p + tp, "titre : id, name");
            else
            {
                if (x.Title.Id is null) Missing(r, p + tp + ".id", "titre « title.fete.… »");
                else if (!IsTitle(x.Title.Id)) r.Add(p + tp + ".id", ValidationCode.InvalidValue, $"titre « {x.Title.Id} » mal formé : « title.fete.… ».");
                else if (!titles.Add(x.Title.Id)) r.Add(p + tp + ".id", ValidationCode.DuplicateId, $"titre « {x.Title.Id} » déjà donné par une autre fête.");
                Text(x.Title.Name, p + tp + ".name", "nom du titre", texts, r);
            }

            // Activités : aucune pour une fête du calendrier seulement (pas d'invités) ; au moins une sinon.
            var activities = new List<FeastActivity>();
            if (x.Activities is null) Missing(r, p + ".activities", "activités, éventuellement aucune");
            else if (x.Activities.Count > MaxActivities) r.Add(p + ".activities", ValidationCode.LimitExceeded, $"{x.Activities.Count} activités, au plus {MaxActivities}.");
            else
            {
                if (festival != null && festival.CalendarOnly && x.Activities.Count > 0)
                    r.Add(p + ".activities", ValidationCode.InvalidValue, $"« {festival.Id} » est une fête du calendrier seulement (sans invités) : aucune activité.");
                else if (festival != null && !festival.CalendarOnly && x.Activities.Count == 0)
                    r.Add(p + ".activities", ValidationCode.InvalidValue, $"« {festival.Id} » met le monde en fête : au moins une activité qui rapporte sa monnaie.");
                var ids = new HashSet<string>(StringComparer.Ordinal);
                var hosts = new HashSet<string>(StringComparer.Ordinal);
                var contests = new HashSet<string>(StringComparer.Ordinal);
                for (int k = 0; k < x.Activities.Count; k++)
                {
                    string ap = $"{p}.activities[{k}]";
                    if (x.Activities[k] is not FeastActivityData a) { Missing(r, ap, "activité"); continue; }
                    if (CheckActivity(a, ap, festival, story, texts, cap, ids, hosts, contests, r) is FeastActivity act) activities.Add(act);
                }
            }

            int after = 0;
            int count = x.Activities?.Count ?? 0;
            if (count == 0)
            {
                if (x.RewardAfter != null) r.Add(p + ".rewardAfter", ValidationCode.InvalidValue, "sans activité, pas de « rewardAfter ».");
            }
            else if (x.RewardAfter is null) Missing(r, p + ".rewardAfter", "activités différentes à faire dans l'édition pour gagner la fête");
            else if (x.RewardAfter < 1 || x.RewardAfter > count)
                r.Add(p + ".rewardAfter", ValidationCode.OutOfRange, $"activités pour gagner la fête hors de [1, {count}] (ses activités).");
            else after = x.RewardAfter.Value;

            // Le marchand de fête vient à chaque fête qui met le monde en fête.
            if (festival != null && merchant != null && !festival.CalendarOnly && !merchant.Festivals.Contains(festival.Id))
                r.Add(p + ".festival", ValidationCode.InvalidValue, $"le marchand de fête « {merchant.Id} » ne vient pas à « {festival.Id} » (ajouter la fête à ses « festivals »).");

            var refs = new List<FeastRef>();
            if (x.Refs is null) Missing(r, p + ".refs", "les 9 références de la fête");
            else if (x.Refs.Count != RefsPerFeast) r.Add(p + ".refs", ValidationCode.OutOfRange, $"{x.Refs.Count} références : {RefsPerFeast} attendues (5 décorations, un costume, un plat, une monnaie, une récompense).");
            else
            {
                for (int k = 0; k < x.Refs.Count; k++)
                {
                    string rp = $"{p}.refs[{k}]";
                    if (x.Refs[k] is not FeastRefData rd) { Missing(r, rp, "référence"); continue; }
                    if (CheckRef(rd, rp, number, texts, cap, codes, r) is FeastRef fr) refs.Add(fr);
                }
                if (refs.Count == RefsPerFeast)
                {
                    int decorations = refs.Count(z => z.Category == FeastCategory.Decoration);
                    foreach (FeastCategory c in new[] { FeastCategory.Costume, FeastCategory.Dish, FeastCategory.Currency, FeastCategory.Reward })
                        if (refs.Count(z => z.Category == c) != 1)
                            r.Add(p + ".refs", ValidationCode.InvalidValue, $"une seule référence de la catégorie « {Name(c)} » attendue.");
                    if (decorations != Decorations) r.Add(p + ".refs", ValidationCode.InvalidValue, $"{decorations} décorations : {Decorations} attendues.");
                }
            }

            if (r.Errors.Count > errors || festival is null || x.Title?.Id is null || x.Title.Name is null || x.Motto is null) return null;
            return new Feast(number, festival.Id, x.Motto, x.Title.Id, x.Title.Name, activities, after, refs);
        }

        private static FeastActivity? CheckActivity(FeastActivityData a, string ap, Festival? festival, StoryBook story, TextCatalog texts, int cap,
            HashSet<string> ids, HashSet<string> hosts, HashSet<string> contests, ValidationReport r)
        {
            int errors = r.Errors.Count;
            if (a.Id is null) Missing(r, ap + ".id", "identifiant de l'activité");
            else if (!IsActivityId(a.Id)) r.Add(ap + ".id", ValidationCode.InvalidValue, $"activité « {a.Id} » mal formée : minuscules, chiffres et « _ », 32 caractères au plus.");
            else if (!ids.Add(a.Id)) r.Add(ap + ".id", ValidationCode.DuplicateId, $"activité « {a.Id} » déjà décrite.");
            Text(a.Name, ap + ".name", "nom de l'activité", texts, r);
            if (a.Coins is null) Missing(r, ap + ".coins", "pièces de la fête gagnées");
            else if (a.Coins < 1 || (cap > 0 && a.Coins > cap)) r.Add(ap + ".coins", ValidationCode.OutOfRange, $"gain hors de [1, {(cap > 0 ? cap : MaxWalletCap)}] (le plafond du portefeuille).");
            if ((a.Host is null) == (a.Contest is null))
                r.Add(ap, ValidationCode.InvalidValue, "soit « host » (un invité de la fête), soit « contest » (un concours de l'histoire), un seul des deux.");
            else if (a.Host != null)
            {
                if (!story.HasNpc(a.Host)) r.Add(ap + ".host", ValidationCode.UnknownReference, $"PNJ inconnu « {a.Host} ».");
                else if (!hosts.Add(a.Host)) r.Add(ap + ".host", ValidationCode.DuplicateId, $"« {a.Host} » mène déjà une autre activité de la fête.");
                else if (festival != null && festival.Guest(a.Host) is null)
                    r.Add(ap + ".host", ValidationCode.InvalidValue, $"« {a.Host} » n'est pas un invité de « {festival.Id} ».");
            }
            else
            {
                string key = a.Contest!;
                if (!story.HasContestKey(key)) r.Add(ap + ".contest", ValidationCode.UnknownReference, $"concours « {key} » inconnu : aucun effet « contest » ne le ferme dans l'histoire.");
                else if (festival != null && key != festival.Id && !key.StartsWith(festival.Id + ".", StringComparison.Ordinal))
                    r.Add(ap + ".contest", ValidationCode.InvalidValue, $"concours « {key} » d'une autre fête que « {festival.Id} ».");
                else if (!contests.Add(key)) r.Add(ap + ".contest", ValidationCode.DuplicateId, $"concours « {key} » déjà lié à une activité.");
            }
            if (r.Errors.Count > errors) return null;
            return new FeastActivity(a.Id!, a.Name!, a.Coins!.Value, a.Host, a.Contest);
        }

        private static FeastRef? CheckRef(FeastRefData x, string rp, int number, TextCatalog texts, int cap, HashSet<string> codes, ValidationReport r)
        {
            int errors = r.Errors.Count;
            FeastCategory? category = null;
            if (x.Category is null) Missing(r, rp + ".category", "catégorie (decoration, costume, dish, currency, reward)");
            else if (!Categories.TryGetValue(x.Category, out FeastCategory c))
                r.Add(rp + ".category", ValidationCode.UnknownReference, $"catégorie inconnue « {x.Category} » ; possibles : {string.Join(", ", Categories.Keys)}.");
            else category = c;

            if (x.Code is null) Missing(r, rp + ".code", "code « F04_D01 »");
            else if (!(Code.Match(x.Code) is Match m && m.Success)) r.Add(rp + ".code", ValidationCode.InvalidValue, $"code « {x.Code} » mal formé : « F01_D01 » à « F12_R01 ».");
            else if (!codes.Add(x.Code)) r.Add(rp + ".code", ValidationCode.DuplicateId, $"référence « {x.Code} » déjà décrite.");
            else
            {
                int feast = int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                FeastCategory letter = Letters[m.Groups[2].Value[0]];
                int rank = int.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                if (number > 0 && feast != number) r.Add(rp + ".code", ValidationCode.InvalidValue, $"« {x.Code} » est une référence de la fête n° {feast}, pas de la n° {number}.");
                if (category != null && letter != category) r.Add(rp + ".code", ValidationCode.InvalidValue, $"« {x.Code} » : la lettre {m.Groups[2].Value} ne va pas avec la catégorie « {x.Category} ».");
                int most = letter == FeastCategory.Decoration ? Decorations : 1;
                if (rank > most) r.Add(rp + ".code", ValidationCode.OutOfRange, $"« {x.Code} » : rang hors de [1, {most}] pour sa catégorie.");
            }

            Text(x.Name, rp + ".name", "nom de la référence", texts, r);

            if (x.Size is null) Missing(r, rp + ".size", "enveloppe : x, y, z en mètres");
            else
            {
                Dimension(x.Size.X, rp + ".size.x", r);
                Dimension(x.Size.Y, rp + ".size.y", r);
                Dimension(x.Size.Z, rp + ".size.z", r);
            }

            // Relecture indépendante de la PR #213 : l'image est vérifiée en entier, nom compris : « Objets/<code>_<nom>.png », le nom en
            // minuscules ASCII et « _ » ; l'icône est exactement « Icones/<code>_<nom>_icone.png », du même nom que l'image.
            string? slug = null;
            if (x.Image is null) Missing(r, rp + ".image", "image de référence « Objets/<code>_<nom>.png »");
            else if (x.Image.Length > 200 || !(ImageFile.Match(x.Image) is Match im && im.Success))
                r.Add(rp + ".image", ValidationCode.InvalidValue, $"image « {x.Image} » mal formée : « Objets/<code>_<nom>.png » (nom en minuscules, chiffres et « _ »).");
            else if (x.Code != null && im.Groups["code"].Value != x.Code)
                r.Add(rp + ".image", ValidationCode.InvalidValue, $"image « {x.Image} » d'une autre référence que « {x.Code} ».");
            else slug = im.Groups["slug"].Value;

            // Icônes : le plat, la monnaie et la récompense en ont une (dossier des fêtes : 3 icônes par fête), pas les autres.
            bool iconed = category is FeastCategory k && (k == FeastCategory.Dish || k == FeastCategory.Currency || k == FeastCategory.Reward);
            if (x.Icon is null) { if (iconed) Missing(r, rp + ".icon", "icône « Icones/<code>_<nom>_icone.png » (plat, monnaie, récompense)"); }
            else if (category != null && !iconed) r.Add(rp + ".icon", ValidationCode.InvalidValue, "seuls le plat, la monnaie et la récompense ont une icône.");
            else if (x.Code != null && slug != null && x.Icon != $"Icones/{x.Code}_{slug}_icone.png")
                r.Add(rp + ".icon", ValidationCode.InvalidValue, $"icône « {x.Icon} » : « Icones/{x.Code}_{slug}_icone.png » attendue (même nom que l'image).");

            // Rareté (décision de Damien du 17 novembre 2026) : récompense Rare, costume Commun ; la monnaie n'en a pas.
            ItemRarity? rarity = null;
            if (category == FeastCategory.Currency)
            {
                if (x.Rarity != null) r.Add(rp + ".rarity", ValidationCode.InvalidValue, "la monnaie de la fête n'a pas de rareté.");
            }
            else if (x.Rarity is null) { if (category != null) Missing(r, rp + ".rarity", "rareté (common, rare)"); }
            else if (!Rarities.TryGetValue(x.Rarity, out ItemRarity rr))
                r.Add(rp + ".rarity", ValidationCode.UnknownReference, $"rareté inconnue « {x.Rarity} » ; possibles : common, rare.");
            else if (category == FeastCategory.Reward && rr != ItemRarity.Rare)
                r.Add(rp + ".rarity", ValidationCode.InvalidValue, "la récompense de la fête est de rareté Rare (décision de Damien du 17 novembre 2026).");
            else if (category == FeastCategory.Costume && rr != ItemRarity.Common)
                r.Add(rp + ".rarity", ValidationCode.InvalidValue, "le costume de la fête est de rareté Commune (décision de Damien du 17 novembre 2026).");
            else rarity = rr;

            // Prix en monnaie de la fête : tout s'achète chez le marchand de fête, sauf la monnaie elle-même.
            if (category == FeastCategory.Currency)
            {
                if (x.Price != null) r.Add(rp + ".price", ValidationCode.InvalidValue, "la monnaie ne s'achète pas (ni convertible ni échangeable).");
            }
            else if (x.Price is null) { if (category != null) Missing(r, rp + ".price", "prix en monnaie de la fête"); }
            else if (x.Price < 1 || (cap > 0 && x.Price > cap))
                r.Add(rp + ".price", ValidationCode.OutOfRange, $"prix hors de [1, {(cap > 0 ? cap : MaxWalletCap)}] : un prix au-delà du plafond ne se paierait jamais.");

            if (r.Errors.Count > errors || category is null) return null;
            return new FeastRef(x.Code!, number, category.Value, x.Name!, new FeastSize(x.Size!.X!.Value, x.Size.Y!.Value, x.Size.Z!.Value), x.Image!, x.Icon,
                rarity, x.Price);
        }

        private static void Dimension(Fixed? v, string path, ValidationReport r)
        {
            if (v is null) Missing(r, path, "cote en mètres");
            else if (v.Value <= Fixed.Zero || v.Value > MaxSize) r.Add(path, ValidationCode.OutOfRange, $"cote hors de ]0, {MaxSize}] m.");
        }

        private static void Text(string? key, string path, string what, TextCatalog texts, ValidationReport r)
        {
            if (key is null) Missing(r, path, what);
            else if (!texts.Has(key)) r.Add(path, ValidationCode.UnknownReference, $"texte inconnu « {key} ».");
        }

        private static string Name(FeastCategory c) => Categories.First(k => k.Value == c).Key;

        private static void Missing(ValidationReport report, string path, string what)
        {
            if (!report.HasErrorAtOrAbove(path)) report.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({what}).");
        }
    }
}
