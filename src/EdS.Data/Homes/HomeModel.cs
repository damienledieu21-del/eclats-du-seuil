using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Feasts;
using EdS.Data.Items;
using EdS.Data.Rebuilds;
using EdS.Data.Saves;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Collections;
using EdS.Domain.Numerics;

namespace EdS.Data.Homes
{
    // --- Documents (avant validation) ------------------------------------------------------------------

    /// <summary>Maison du joueur : catalogue tel que lu (<c>kind: "home"</c>, <c>data/v0/world/home.json</c>).</summary>
    public sealed class HomeDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
        public HomeData? Data { get; set; }
    }

    public sealed class HomeData
    {
        public string? Interior { get; set; }
        public string? Door { get; set; }
        public long? Price { get; set; }
        public HomeTierData? Base { get; set; }
        public List<HomeStageData?>? Stages { get; set; }
        public List<HomePieceData?>? Furniture { get; set; }
    }

    /// <summary>Ce qu'offre la maison à un palier : sol (en mètres), places du coffre, meubles posés au plus.</summary>
    public class HomeTierData
    {
        public HomeSizeData? Floor { get; set; }
        public int? Chest { get; set; }
        public int? Pieces { get; set; }
    }

    /// <summary>Un palier d'agrandissement : ses besoins (or, matériaux), comme un palier de chantier, et ce qu'il offre une fois fini.</summary>
    public sealed class HomeStageData : HomeTierData
    {
        public string? Id { get; set; }
        public List<RebuildNeedData?>? Needs { get; set; }
    }

    public sealed class HomeSizeData
    {
        public Fixed? Width { get; set; }
        public Fixed? Depth { get; set; }
    }

    /// <summary>Un meuble posable : exactement une source, un objet (<c>item</c>) ou une décoration de fête (<c>feast</c>).</summary>
    public sealed class HomePieceData
    {
        public string? Id { get; set; }
        public string? Item { get; set; }
        public string? Feast { get; set; }
        public HomeSizeData? Size { get; set; }
        public string? Use { get; set; }
    }

    // --- Modèle validé ---------------------------------------------------------------------------------

    /// <summary>Maison du joueur : à quoi sert un meuble (décision de Damien du 20 novembre 2026 : « la maison sert à tout »).</summary>
    public enum HomeUse
    {
        /// <summary>Décor seulement.</summary>
        Decor = 0,

        /// <summary>Coffre (l'accès au coffre de la maison, dessiné).</summary>
        Chest = 1,

        /// <summary>Lit : bonus de repos (valeur au sous-lot V2.53.2, question à Damien).</summary>
        Bed = 2,

        /// <summary>Cuisine : atelier de cuisine, plus lent qu'en ville (V2.53.2).</summary>
        Kitchen = 3,

        /// <summary>Établi : atelier de menuiserie et de taille, plus lent qu'en ville (V2.53.2).</summary>
        Workbench = 4,
    }

    /// <summary>Ce qu'offre la maison à un palier, en cases de la grille (<see cref="HomeValidator.Cell"/>).</summary>
    public sealed class HomeTier
    {
        /// <summary>Largeur (x) et profondeur (z) du sol, en cases.</summary>
        public int Width { get; }
        public int Depth { get; }

        /// <summary>Places du coffre.</summary>
        public int Chest { get; }

        /// <summary>Meubles posés au plus.</summary>
        public int Pieces { get; }

        internal HomeTier(int width, int depth, int chest, int pieces)
        {
            Width = width;
            Depth = depth;
            Chest = chest;
            Pieces = pieces;
        }
    }

    /// <summary>Un meuble posable : sa source, son emprise au sol (en cases, sans rotation) et son usage.</summary>
    public sealed class HomePiece
    {
        public string Id { get; }

        /// <summary>Objet du catalogue (une copie quitte l'inventaire à la pose, y revient à la reprise), ou null.</summary>
        public string? Item { get; }

        /// <summary>Décoration de fête possédée (code « F01_D03 ») et sa fête ; posée une fois au plus, sans quitter le portefeuille.</summary>
        public string? FeastCode { get; }
        public string? Festival { get; }

        public int Width { get; }
        public int Depth { get; }
        public HomeUse Use { get; }

        internal HomePiece(string id, string? item, string? feastCode, string? festival, int width, int depth, HomeUse use)
        {
            Id = id;
            Item = item;
            FeastCode = feastCode;
            Festival = festival;
            Width = width;
            Depth = depth;
            Use = use;
        }

        /// <summary>Emprise une fois tournée de <paramref name="turn"/> quarts de tour (largeur et profondeur échangées aux quarts impairs).</summary>
        public (int Width, int Depth) Footprint(int turn) => (turn & 1) == 0 ? (Width, Depth) : (Depth, Width);
    }

    /// <summary>Maison du joueur : catalogue validé, immuable.</summary>
    public sealed class HomeBook
    {
        public int SchemaVersion { get; }
        public string ContentVersion { get; }

        /// <summary>Zone intérieure de la maison (où l'on pose, ouvre le coffre).</summary>
        public string Interior { get; }

        /// <summary>Porte (dans sa zone, <see cref="DoorZone"/>) qui mène à l'intérieur ; fermée tant que la maison n'est pas achetée.</summary>
        public string Door { get; }
        public string DoorZone { get; }

        /// <summary>Prix d'achat, en cuivre, payé une fois.</summary>
        public long Price { get; }

        public HomeTier Base { get; }

        /// <summary>Paliers d'agrandissement, dans l'ordre (besoins de chantier : or et matériaux).</summary>
        public IReadOnlyList<RebuildStage> Stages { get; }

        /// <summary>Ce qu'offre chaque palier une fois fini (même ordre que <see cref="Stages"/>).</summary>
        public IReadOnlyList<HomeTier> Tiers { get; }

        public IReadOnlyList<HomePiece> Furniture { get; }

        internal HomeBook(int schemaVersion, string contentVersion, string interior, string door, string doorZone, long price, HomeTier @base,
            IEnumerable<RebuildStage> stages, IEnumerable<HomeTier> tiers, IEnumerable<HomePiece> furniture)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            Interior = interior;
            Door = door;
            DoorZone = doorZone;
            Price = price;
            Base = @base;
            Stages = Frozen.List(stages, nameof(stages));
            Tiers = Frozen.List(tiers, nameof(tiers));
            Furniture = Frozen.List(furniture, nameof(furniture));
        }

        /// <summary>Ce qu'offre la maison après <paramref name="stagesDone"/> paliers finis (le dernier palier connu au-delà).</summary>
        public HomeTier Tier(int stagesDone) => stagesDone <= 0 || Tiers.Count == 0 ? Base : Tiers[Math.Min(stagesDone, Tiers.Count) - 1];

        public HomePiece? Piece(string? id)
        {
            for (int i = 0; i < Furniture.Count; i++) if (Furniture[i].Id == id) return Furniture[i];
            return null;
        }
    }

    public sealed class HomeLoadResult
    {
        public ValidationReport Report { get; }
        public HomeBook? Book { get; }
        public bool IsValid => Book != null;

        internal HomeLoadResult(ValidationReport report, HomeBook? book)
        {
            Report = report;
            Book = book;
        }
    }

    /// <summary>
    /// Maison du joueur (sous-lot V2.53.1, décisions de Damien du 20 novembre 2026) : validateur unique du catalogue (<c>kind: "home"</c>),
    /// contre les zones (intérieur, porte), le catalogue d'objets (meubles, matériaux) et les fêtes (décorations). Une seule maison ; les
    /// paliers d'agrandissement ont les besoins d'un chantier (or ou matériaux seulement) et n'offrent jamais moins que le précédent (un meuble
    /// posé ou un objet rangé reste valable) ; les tailles sont des multiples de la grille de 0,25 m.
    /// </summary>
    public static class HomeValidator
    {
        public const string Kind = "home";

        /// <summary>Version 1 (sous-lot V2.53.1) : <c>interior</c>, <c>door</c>, <c>price</c>, <c>base</c>, <c>stages</c>, <c>furniture</c>.</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>Pas de la grille de pose : 0,25 m.</summary>
        public static readonly Fixed Cell = Fixed.FromRatio(1, 4);

        /// <summary>Côté au plus du sol (32 m), en cases ; côté au plus d'un meuble (8 m), en cases.</summary>
        public const int MaxSide = SavedHome.MaxCells, MaxPieceSide = 32;

        public const int MaxStages = SavedHome.MaxStages, MaxNeeds = SavedRebuild.MaxNeeds, MaxFurniture = 256;

        private static readonly Dictionary<string, HomeUse> Uses = new Dictionary<string, HomeUse>(StringComparer.Ordinal)
        {
            ["decor"] = HomeUse.Decor,
            ["chest"] = HomeUse.Chest,
            ["bed"] = HomeUse.Bed,
            ["kitchen"] = HomeUse.Kitchen,
            ["workbench"] = HomeUse.Workbench,
        };

        public static HomeLoadResult Validate(HomeDocument document, IReadOnlyList<ZoneDefinition> zones, ItemCatalog items, FeastBook? feasts,
            ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (zones is null) throw new ArgumentNullException(nameof(zones));
            if (items is null) throw new ArgumentNullException(nameof(items));
            ValidationReport r = report ?? new ValidationReport();
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, CurrentSchemaVersion, r))
                return new HomeLoadResult(r, null);
            if (!(document.Data is HomeData d)) { Missing(r, "$.data", "maison"); return new HomeLoadResult(r, null); }

            // Intérieur et porte : une zone intérieure, une porte d'une autre zone qui y mène.
            ZoneDefinition? interior = null;
            if (d.Interior is null) Missing(r, "$.data.interior", "zone intérieure de la maison");
            else if ((interior = zones.FirstOrDefault(z => z.Id == d.Interior)) is null)
                r.Add("$.data.interior", ValidationCode.UnknownReference, $"zone « {d.Interior} » inconnue.");
            else if (!interior.Indoor) r.Add("$.data.interior", ValidationCode.InvalidValue, $"« {d.Interior} » n'est pas un intérieur.");
            string? doorZone = null;
            if (d.Door is null) Missing(r, "$.data.door", "porte qui mène à la maison");
            else if (d.Interior != null)
            {
                ZoneDefinition? owner = zones.FirstOrDefault(z => z.Id != d.Interior && z.Doors.Any(x => x.Id == d.Door && x.To == d.Interior));
                if (owner is null) r.Add("$.data.door", ValidationCode.UnknownReference, $"aucune porte « {d.Door} » ne mène à « {d.Interior} ».");
                else doorZone = owner.Id;
            }
            if (d.Price is null) Missing(r, "$.data.price", "prix d'achat en cuivre");
            else if (d.Price < 1 || d.Price > Coins.Max) r.Add("$.data.price", ValidationCode.OutOfRange, $"prix hors de [1, {Coins.Max}] cuivre.");

            HomeTier? @base = d.Base is null ? null : Tier(d.Base, "$.data.base", null, r);
            if (d.Base is null) Missing(r, "$.data.base", "maison achetée : sol, coffre, meubles");

            var stages = new List<RebuildStage>();
            var tiers = new List<HomeTier>();
            if (d.Stages is null) Missing(r, "$.data.stages", "paliers d'agrandissement, éventuellement aucun");
            else if (d.Stages.Count > MaxStages) r.Add("$.data.stages", ValidationCode.LimitExceeded, $"au plus {MaxStages} paliers.");
            else
            {
                var stageIds = new HashSet<string>(StringComparer.Ordinal);
                HomeTier? previous = @base;
                for (int s = 0; s < d.Stages.Count; s++)
                {
                    string sp = $"$.data.stages[{s}]";
                    if (!(d.Stages[s] is HomeStageData st)) { Missing(r, sp, "palier"); previous = null; continue; }
                    bool ok = true;
                    if (!SavedRebuild.IsLocalId(st.Id)) { r.Add(sp + ".id", ValidationCode.InvalidValue, $"identifiant « {st.Id} » mal formé."); ok = false; }
                    else if (!stageIds.Add(st.Id!)) { r.Add(sp + ".id", ValidationCode.DuplicateId, $"palier « {st.Id} » déjà décrit."); ok = false; }
                    var needs = new List<RebuildNeed>();
                    if (st.Needs is null || st.Needs.Count == 0) { Missing(r, sp + ".needs", "au moins un besoin"); ok = false; }
                    else if (st.Needs.Count > MaxNeeds) { r.Add(sp + ".needs", ValidationCode.LimitExceeded, $"au plus {MaxNeeds} besoins."); ok = false; }
                    else
                    {
                        var needIds = new HashSet<string>(StringComparer.Ordinal);
                        for (int n = 0; n < st.Needs.Count; n++)
                        {
                            string np = $"{sp}.needs[{n}]";
                            RebuildNeedData? x = st.Needs[n];
                            // Décision de Damien du 20 novembre 2026 : on agrandit avec de l'or, des planches, de la pierre taillée.
                            if (x != null && (x.Defeat != null || x.Discover != null || x.Reach != null))
                            { r.Add(np, ValidationCode.InvalidValue, "un palier de la maison demande de l'or (coins) ou des matériaux (item, count)."); ok = false; continue; }
                            RebuildNeed? need = RebuildValidator.Need(x, np, needIds, items, new HashSet<string>(), r, null, null);
                            if (need is null) ok = false; else needs.Add(need);
                        }
                    }
                    HomeTier? tier = Tier(st, sp, previous, r);
                    if (tier is null) ok = false;
                    previous = tier;
                    if (ok) { stages.Add(new RebuildStage(st.Id!, needs)); tiers.Add(tier!); }
                }
            }

            int largest = Math.Min(tiers.Count > 0 ? tiers[tiers.Count - 1].Width : @base?.Width ?? MaxSide, tiers.Count > 0 ? tiers[tiers.Count - 1].Depth : @base?.Depth ?? MaxSide);
            int longest = Math.Max(tiers.Count > 0 ? tiers[tiers.Count - 1].Width : @base?.Width ?? MaxSide, tiers.Count > 0 ? tiers[tiers.Count - 1].Depth : @base?.Depth ?? MaxSide);
            var furniture = new List<HomePiece>();
            if (d.Furniture is null) Missing(r, "$.data.furniture", "meubles posables");
            else if (d.Furniture.Count > MaxFurniture) r.Add("$.data.furniture", ValidationCode.LimitExceeded, $"au plus {MaxFurniture} meubles.");
            else
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                var sources = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < d.Furniture.Count; i++)
                {
                    string p = $"$.data.furniture[{i}]";
                    if (!(d.Furniture[i] is HomePieceData x)) { Missing(r, p, "meuble"); continue; }
                    bool ok = true;
                    if (!SavedRebuild.IsLocalId(x.Id)) { r.Add(p + ".id", ValidationCode.InvalidValue, $"identifiant « {x.Id} » mal formé."); ok = false; }
                    else if (!ids.Add(x.Id!)) { r.Add(p + ".id", ValidationCode.DuplicateId, $"meuble « {x.Id} » déjà décrit."); ok = false; }
                    string? festival = null;
                    if ((x.Item is null) == (x.Feast is null))
                    { r.Add(p, ValidationCode.InvalidValue, "un meuble a exactement une source : item ou feast."); ok = false; }
                    else if (x.Item is string item)
                    {
                        if (!items.TryGetItem(item, out ItemDefinition def)) { r.Add(p + ".item", ValidationCode.UnknownReference, $"objet « {item} » inconnu."); ok = false; }
                        else if (def.Type == ItemType.Equipment) { r.Add(p + ".item", ValidationCode.InvalidValue, $"« {item} » est une pièce d'équipement, pas un meuble."); ok = false; }
                        else if (!sources.Add("item:" + item)) { r.Add(p + ".item", ValidationCode.DuplicateId, $"objet « {item} » déjà meuble."); ok = false; }
                    }
                    else
                    {
                        string code = x.Feast!;
                        if (feasts is null) { r.Add(p + ".feast", ValidationCode.UnknownReference, "aucun contenu des fêtes : pas de décoration de fête."); ok = false; }
                        else if (!(feasts.Ref(code) is FeastRef fr) || fr.Category != FeastCategory.Decoration)
                        { r.Add(p + ".feast", ValidationCode.UnknownReference, $"« {code} » n'est pas une décoration de fête."); ok = false; }
                        else if (!sources.Add("feast:" + code)) { r.Add(p + ".feast", ValidationCode.DuplicateId, $"décoration « {code} » déjà meuble."); ok = false; }
                        else festival = feasts.Feasts.First(f => f.Number == fr.Feast).Festival;
                    }
                    int w = 0, dp = 0;
                    if (x.Size is null) { Missing(r, p + ".size", "emprise au sol en mètres"); ok = false; }
                    else
                    {
                        ok &= Side(x.Size.Width, p + ".size.width", MaxPieceSide, r, out w);
                        ok &= Side(x.Size.Depth, p + ".size.depth", MaxPieceSide, r, out dp);
                        if (w > 0 && dp > 0 && (Math.Min(w, dp) > largest || Math.Max(w, dp) > longest))
                        { r.Add(p + ".size", ValidationCode.OutOfRange, "le meuble ne tient pas dans la maison, même agrandie."); ok = false; }
                    }
                    HomeUse use = HomeUse.Decor;
                    if (x.Use != null && !Uses.TryGetValue(x.Use, out use))
                    { r.Add(p + ".use", ValidationCode.InvalidValue, $"usage « {x.Use} » inconnu : decor, chest, bed, kitchen ou workbench."); ok = false; }
                    if (ok) furniture.Add(new HomePiece(x.Id!, x.Item, x.Feast, festival, w, dp, use));
                }
            }

            if (!r.IsValid || document.ContentVersion is null || interior is null || doorZone is null || @base is null) return new HomeLoadResult(r, null);
            return new HomeLoadResult(r, new HomeBook(document.SchemaVersion!.Value, document.ContentVersion, interior.Id, d.Door!, doorZone, d.Price!.Value,
                @base, stages, tiers, furniture));
        }

        /// <summary>Un palier (ou la maison achetée) : jamais moins que le palier précédent (<paramref name="previous"/>).</summary>
        private static HomeTier? Tier(HomeTierData x, string p, HomeTier? previous, ValidationReport r)
        {
            bool ok = true;
            int w = 0, d = 0;
            if (x.Floor is null) { Missing(r, p + ".floor", "sol en mètres"); ok = false; }
            else
            {
                ok &= Side(x.Floor.Width, p + ".floor.width", MaxSide, r, out w);
                ok &= Side(x.Floor.Depth, p + ".floor.depth", MaxSide, r, out d);
            }
            if (x.Chest is null) { Missing(r, p + ".chest", "places du coffre"); ok = false; }
            else if (x.Chest < 1 || x.Chest > SavedHome.MaxChest) { r.Add(p + ".chest", ValidationCode.OutOfRange, $"places hors de [1, {SavedHome.MaxChest}]."); ok = false; }
            if (x.Pieces is null) { Missing(r, p + ".pieces", "meubles posés au plus"); ok = false; }
            else if (x.Pieces < 1 || x.Pieces > SavedHome.MaxPieces) { r.Add(p + ".pieces", ValidationCode.OutOfRange, $"meubles hors de [1, {SavedHome.MaxPieces}]."); ok = false; }
            if (!ok) return null;
            if (previous != null && (w < previous.Width || d < previous.Depth || x.Chest < previous.Chest || x.Pieces < previous.Pieces))
            {
                r.Add(p, ValidationCode.InvalidValue, "un palier n'offre jamais moins que le précédent (sol, coffre, meubles).");
                return null;
            }
            return new HomeTier(w, d, x.Chest!.Value, x.Pieces!.Value);
        }

        /// <summary>Un côté en mètres : multiple de la grille, de 0,25 m à <paramref name="max"/> cases ; rend le nombre de cases.</summary>
        private static bool Side(Fixed? value, string p, int max, ValidationReport r, out int cells)
        {
            cells = 0;
            if (!(value is Fixed v)) { Missing(r, p, "mètres"); return false; }
            if (v.Raw <= 0 || v.Raw % Cell.Raw != 0 || v.Raw / Cell.Raw > max)
            {
                r.Add(p, ValidationCode.OutOfRange, $"côté hors de [0,25, {max / 4}] m ou hors de la grille de 0,25 m.");
                return false;
            }
            cells = (int)(v.Raw / Cell.Raw);
            return true;
        }

        private static void Missing(ValidationReport report, string path, string what)
        {
            if (!report.HasErrorAtOrAbove(path)) report.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({what}).");
        }
    }
}
