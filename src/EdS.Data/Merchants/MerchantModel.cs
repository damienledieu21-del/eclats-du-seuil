using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Items;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Domain.Collections;

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

        public List<OfferData?>? Offers { get; set; }
    }

    public sealed class OfferData
    {
        public string? Item { get; set; }

        /// <summary>Prix d'achat d'une unité, en pièces de cuivre.</summary>
        public long? Price { get; set; }

        /// <summary>Toujours en vente (sinon : tirée certains jours seulement).</summary>
        public bool? Always { get; set; }
    }

    // --- Modèle validé ---------------------------------------------------------------------------------

    /// <summary>Un objet que vend un marchand : prix d'une unité (cuivre), toujours en vente ou certains jours seulement.</summary>
    public sealed class MerchantOffer
    {
        public string Item { get; }
        public long Price { get; }
        public bool Always { get; }

        internal MerchantOffer(string item, long price, bool always)
        {
            Item = item;
            Price = price;
            Always = always;
        }
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

        internal Merchant(string id, string npc, string greeting, int sellPercent, IEnumerable<string> closed, int daily, IEnumerable<MerchantOffer> offers)
        {
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
        /// <c>sellPercent</c>, <c>closed</c>, <c>daily</c>, <c>offers</c> : <c>item</c>, <c>price</c>, <c>always</c>).
        /// </summary>
        public const int CurrentSchemaVersion = 1;

        public const int MaxValues = 1024, MaxMerchants = 32, MaxOffers = 32;

        /// <summary>Prix ou valeur d'une unité, au plus (cuivre : 10 000 or).</summary>
        public const long MaxPrice = 100_000_000;

        private static readonly Regex MerchantId = new Regex(@"^merchant(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);

        public static bool IsMerchantId(string? id) => id != null && id.Length <= 200 && MerchantId.IsMatch(id);

        public static MerchantsLoadResult Validate(MerchantsDocument document, TextCatalog texts, StoryBook story, ItemCatalog items, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (texts is null) throw new ArgumentNullException(nameof(texts));
            if (story is null) throw new ArgumentNullException(nameof(story));
            if (items is null) throw new ArgumentNullException(nameof(items));
            ValidationReport r = report ?? new ValidationReport();
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, CurrentSchemaVersion, r))
                return new MerchantsLoadResult(r, null);
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
                    if (CheckMerchant(m, p, texts, story, items, values, merchantIds, npcs, r) is Merchant merchant) merchants.Add(merchant);
                }

            if (!r.IsValid || document.ContentVersion is null) return new MerchantsLoadResult(r, null);
            return new MerchantsLoadResult(r, new MerchantBook(document.SchemaVersion!.Value, document.ContentVersion, values, merchants));
        }

        private static Merchant? CheckMerchant(MerchantData m, string p, TextCatalog texts, StoryBook story, ItemCatalog items, IReadOnlyDictionary<string, long> values,
            HashSet<string> merchantIds, HashSet<string> npcs, ValidationReport r)
        {
            bool ok = true;
            if (m.Id is null) { Missing(r, p + ".id", "identifiant « merchant.… »"); ok = false; }
            else if (!IsMerchantId(m.Id)) { r.Add(p + ".id", ValidationCode.InvalidValue, $"identifiant « {m.Id} » mal formé : « merchant.… »."); ok = false; }
            else if (!merchantIds.Add(m.Id)) { r.Add(p + ".id", ValidationCode.DuplicateId, $"marchand « {m.Id} » déjà décrit."); ok = false; }
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
                    if (offerOk) offers.Add(new MerchantOffer(o.Item!, o.Price!.Value, o.Always!.Value));
                    else ok = false;
                }

            int rotating = m.Offers?.Count(o => o?.Always == false) ?? 0;
            if (m.Daily is null) { Missing(r, p + ".daily", "offres tirées chaque jour"); ok = false; }
            else if (m.Daily < 0 || m.Daily > rotating) { r.Add(p + ".daily", ValidationCode.OutOfRange, $"offres tirées chaque jour hors de [0, {rotating}] (offres non permanentes)."); ok = false; }
            else if (ok && m.Daily == 0 && offers.All(o => !o.Always)) { r.Add(p + ".daily", ValidationCode.InvalidValue, "aucun objet en vente : ni offre permanente, ni offre tirée."); ok = false; }
            return ok ? new Merchant(m.Id!, m.Npc!, m.Greeting!, m.SellPercent!.Value, closed, m.Daily!.Value, offers) : null;
        }

        private static void Missing(ValidationReport report, string path, string what)
        {
            if (!report.HasErrorAtOrAbove(path)) report.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({what}).");
        }
    }
}
