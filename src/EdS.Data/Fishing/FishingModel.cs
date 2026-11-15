using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Items;
using EdS.Data.Scenarios;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Collections;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Time;

namespace EdS.Data.Fishing
{
    // --- Documents (avant validation) ------------------------------------------------------------------

    /// <summary>Pêche et carnet : la pêche telle que lue (<c>kind: "fishing"</c>, <c>data/v0/world/fishing.json</c>).</summary>
    public sealed class FishingDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
        public FishingData? Data { get; set; }
    }

    public sealed class FishingData
    {
        public long? BiteMinMs { get; set; }
        public long? BiteMaxMs { get; set; }
        public long? WindowMs { get; set; }
        public Fixed? Range { get; set; }
        public List<FishingSpotData?>? Spots { get; set; }
        public List<FishData?>? Fish { get; set; }
    }

    public sealed class FishingSpotData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public PointData? Position { get; set; }
        public List<string?>? Fish { get; set; }
    }

    public sealed class FishData
    {
        public string? Item { get; set; }
        public int? Weight { get; set; }
        public int? MinCm { get; set; }
        public int? MaxCm { get; set; }
        public List<string?>? Seasons { get; set; }
        public List<string?>? Phases { get; set; }
    }

    // --- Modèle validé ---------------------------------------------------------------------------------

    /// <summary>Pêche et carnet : une espèce : son objet, son poids dans le tirage, sa taille et, si elles sont données, ses saisons et ses moments.</summary>
    public sealed class FishKind
    {
        public string Item { get; }
        public int Weight { get; }
        public int MinCm { get; }
        public int MaxCm { get; }

        /// <summary>Saisons où elle mord (vide : toute l'année).</summary>
        public IReadOnlyList<Season> Seasons { get; }

        /// <summary>Moments de la journée où elle mord (« dawn », « day », « dusk », « night » ; vide : à toute heure).</summary>
        public IReadOnlyList<string> Phases { get; }

        internal FishKind(string item, int weight, int minCm, int maxCm, IEnumerable<Season> seasons, IEnumerable<string> phases)
        {
            Item = item;
            Weight = weight;
            MinCm = minCm;
            MaxCm = maxCm;
            Seasons = Frozen.List(seasons, nameof(seasons));
            Phases = Frozen.List(phases, nameof(phases));
        }

        /// <summary>Elle mord en saison <paramref name="season"/> (rang 0 à 3) au moment <paramref name="phase"/>.</summary>
        public bool Bites(int season, string phase) =>
            (Seasons.Count == 0 || Seasons.Contains((Season)season)) && (Phases.Count == 0 || Phases.Contains(phase));
    }

    /// <summary>Pêche et carnet : un poste de pêche (on s'y tient pour lancer) et les espèces qu'on y prend.</summary>
    public sealed class FishingSpot
    {
        public string Id { get; }
        public string Name { get; }
        public GroundVector Position { get; }
        public IReadOnlyList<string> Fish { get; }

        internal FishingSpot(string id, string name, GroundVector position, IEnumerable<string> fish)
        {
            Id = id;
            Name = name;
            Position = position;
            Fish = Frozen.List(fish, nameof(fish));
        }
    }

    /// <summary>
    /// Pêche et carnet (V4.1 du plan de l'alpha) : la pêche validée, immuable : délai avant la touche (tiré entre <see cref="BiteMinMs"/> et
    /// <see cref="BiteMaxMs"/>), fenêtre pour ferrer (<see cref="WindowMs"/>), distance au poste (<see cref="Range"/>), postes et espèces.
    /// </summary>
    public sealed class FishingBook
    {
        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public long BiteMinMs { get; }
        public long BiteMaxMs { get; }
        public long WindowMs { get; }
        public Fixed Range { get; }
        public IReadOnlyList<FishingSpot> Spots { get; }
        public IReadOnlyList<FishKind> Fish { get; }

        internal FishingBook(int schemaVersion, string contentVersion, long biteMin, long biteMax, long window, Fixed range, IEnumerable<FishingSpot> spots,
            IEnumerable<FishKind> fish)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            BiteMinMs = biteMin;
            BiteMaxMs = biteMax;
            WindowMs = window;
            Range = range;
            Spots = Frozen.List(spots, nameof(spots));
            Fish = Frozen.List(fish, nameof(fish));
        }

        public FishingSpot? Spot(string id) => Spots.FirstOrDefault(s => s.Id == id);
        public FishKind Kind(string item) => Fish.FirstOrDefault(f => f.Item == item) ?? throw new ArgumentException($"Poisson « {item} » inconnu.", nameof(item));
    }

    public sealed class FishingLoadResult
    {
        public ValidationReport Report { get; }
        public FishingBook? Book { get; }
        public bool IsValid => Book != null;

        internal FishingLoadResult(ValidationReport report, FishingBook? book)
        {
            Report = report;
            Book = book;
        }
    }

    /// <summary>
    /// Pêche et carnet : validateur unique (<c>kind: "fishing"</c>), contre les textes (noms des postes), la zone (postes dans l'aire, hors des
    /// obstacles : on s'y tient) et le catalogue (espèces : des objets de type matériau) : délais bornés, fenêtre plus courte que le délai
    /// le plus court ; espèces distinctes, poids de 1 à 1 000, tailles croissantes de 1 à <see cref="EdS.Data.Saves.SavedFishing.MaxCm"/> cm ;
    /// postes d'identifiants uniques, chacun avec au moins une espèce connue.
    /// </summary>
    public static class FishingValidator
    {
        public const string Kind = "fishing";

        /// <summary>Version 1 (pêche et carnet, 15 novembre 2026).</summary>
        public const int CurrentSchemaVersion = 1;

        public const int MaxSpots = 32, MaxFish = 32, MaxWeight = 1000;
        public const long MaxWaitMs = 60_000;

        public static readonly IReadOnlyList<string> PhaseIds = Array.AsReadOnly(new[] { "dawn", "day", "dusk", "night" });

        private static readonly Regex LocalId = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

        public static FishingLoadResult Validate(FishingDocument document, TextCatalog texts, ZoneDefinition zone, ItemCatalog items, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (texts is null) throw new ArgumentNullException(nameof(texts));
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            if (items is null) throw new ArgumentNullException(nameof(items));
            ValidationReport r = report ?? new ValidationReport();
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, CurrentSchemaVersion, r))
                return new FishingLoadResult(r, null);
            FishingData? d = document.Data;
            if (d is null) { Missing(r, "$.data", "biteMinMs, biteMaxMs, windowMs, range, spots, fish"); return new FishingLoadResult(r, null); }

            bool minOk = Long(d.BiteMinMs, "$.data.biteMinMs", 500, MaxWaitMs, "délai le plus court avant la touche (ms)", r);
            bool maxOk = Long(d.BiteMaxMs, "$.data.biteMaxMs", 500, MaxWaitMs, "délai le plus long avant la touche (ms)", r);
            if (minOk && maxOk && d.BiteMaxMs < d.BiteMinMs) r.Add("$.data.biteMaxMs", ValidationCode.InvalidValue, "plus court que biteMinMs.");
            if (Long(d.WindowMs, "$.data.windowMs", 200, MaxWaitMs, "fenêtre pour ferrer (ms)", r) && minOk && d.WindowMs >= d.BiteMinMs)
                r.Add("$.data.windowMs", ValidationCode.InvalidValue, "la fenêtre pour ferrer est plus courte que le délai le plus court.");
            if (d.Range is null) Missing(r, "$.data.range", "distance au poste (m)");
            else if (d.Range.Value < Fixed.One || d.Range.Value > Fixed.FromInt(10)) r.Add("$.data.range", ValidationCode.OutOfRange, "distance hors de [1, 10] m.");

            var fish = new List<FishKind>();
            if (d.Fish is null || d.Fish.Count == 0) Missing(r, "$.data.fish", "espèces");
            else if (d.Fish.Count > MaxFish) r.Add("$.data.fish", ValidationCode.LimitExceeded, $"au plus {MaxFish} espèces.");
            else
                for (int i = 0; i < d.Fish.Count; i++)
                {
                    string p = $"$.data.fish[{i}]";
                    if (d.Fish[i] is not FishData x) { Missing(r, p, "espèce"); continue; }
                    int before = r.Errors.Count;
                    if (x.Item is null) Missing(r, p + ".item", "objet de l'espèce");
                    else if (fish.Any(f => f.Item == x.Item) || d.Fish.Take(i).Any(f => f?.Item == x.Item)) r.Add(p + ".item", ValidationCode.DuplicateId, $"« {x.Item} » déjà décrit.");
                    else if (!(items.Items.FirstOrDefault(it => it.Id == x.Item) is ItemDefinition def)) r.Add(p + ".item", ValidationCode.UnknownReference, $"objet « {x.Item} » inconnu du catalogue.");
                    else if (def.Type != ItemType.Material) r.Add(p + ".item", ValidationCode.InvalidValue, $"« {x.Item} » n'est pas un matériau.");
                    Int(x.Weight, p + ".weight", 1, MaxWeight, "poids dans le tirage", r);
                    bool lo = Int(x.MinCm, p + ".minCm", 1, Saves.SavedFishing.MaxCm, "taille la plus petite (cm)", r);
                    if (Int(x.MaxCm, p + ".maxCm", 1, Saves.SavedFishing.MaxCm, "taille la plus grande (cm)", r) && lo && x.MaxCm < x.MinCm)
                        r.Add(p + ".maxCm", ValidationCode.InvalidValue, "plus petite que minCm.");
                    var seasons = new List<Season>();
                    if (x.Seasons != null)
                    {
                        if (x.Seasons.Count < 1 || x.Seasons.Count > 3) r.Add(p + ".seasons", ValidationCode.InvalidValue, "de une à trois saisons (absent : toute l'année).");
                        else
                            for (int j = 0; j < x.Seasons.Count; j++)
                                if (!(SeasonCalendar.Parse(x.Seasons[j]) is Season s) || seasons.Contains(s))
                                    r.Add($"{p}.seasons[{j}]", ValidationCode.InvalidValue, $"saison « {x.Seasons[j]} » inconnue ou en double ; possibles : spring, summer, autumn, winter.");
                                else seasons.Add(s);
                    }
                    var phases = new List<string>();
                    if (x.Phases != null)
                    {
                        if (x.Phases.Count < 1 || x.Phases.Count > 3) r.Add(p + ".phases", ValidationCode.InvalidValue, "de un à trois moments (absent : à toute heure).");
                        else
                            for (int j = 0; j < x.Phases.Count; j++)
                                if (x.Phases[j] is null || !PhaseIds.Contains(x.Phases[j]) || phases.Contains(x.Phases[j]!))
                                    r.Add($"{p}.phases[{j}]", ValidationCode.InvalidValue, $"moment « {x.Phases[j]} » inconnu ou en double ; possibles : {string.Join(", ", PhaseIds)}.");
                                else phases.Add(x.Phases[j]!);
                    }
                    if (r.Errors.Count == before) fish.Add(new FishKind(x.Item!, x.Weight!.Value, x.MinCm!.Value, x.MaxCm!.Value, seasons.OrderBy(s => s), phases));
                }

            var spots = new List<FishingSpot>();
            CombatSpace space = zone.Setup.Space!;
            if (d.Spots is null || d.Spots.Count == 0) Missing(r, "$.data.spots", "postes de pêche");
            else if (d.Spots.Count > MaxSpots) r.Add("$.data.spots", ValidationCode.LimitExceeded, $"au plus {MaxSpots} postes.");
            else
                for (int i = 0; i < d.Spots.Count; i++)
                {
                    string p = $"$.data.spots[{i}]";
                    if (d.Spots[i] is not FishingSpotData x) { Missing(r, p, "poste"); continue; }
                    int before = r.Errors.Count;
                    if (x.Id is null || x.Id.Length > 64 || !LocalId.IsMatch(x.Id)) r.Add(p + ".id", ValidationCode.InvalidValue, $"identifiant « {x.Id} » mal formé.");
                    else if (d.Spots.Take(i).Any(s => s?.Id == x.Id)) r.Add(p + ".id", ValidationCode.DuplicateId, $"poste « {x.Id} » déjà décrit.");
                    if (x.Name is null) Missing(r, p + ".name", "clé du nom affiché");
                    else if (!texts.Has(x.Name)) r.Add(p + ".name", ValidationCode.UnknownReference, $"texte « {x.Name} » inconnu.");
                    GroundVector at = default;
                    if (x.Position is null || x.Position.X is null || x.Position.Z is null) Missing(r, p + ".position", "[x, z]");
                    else
                    {
                        at = new GroundVector(x.Position.X.Value, x.Position.Z.Value);
                        // On se tient au poste : dans l'aire, hors des obstacles (emprise d'un personnage).
                        if (ZoneValidator.Clear(at, Fixed.FromRatio(1, 2), space, "d'un personnage") is string problem) r.Add(p + ".position", ValidationCode.OutOfRange, problem);
                    }
                    var kinds = new List<string>();
                    if (x.Fish is null || x.Fish.Count == 0) Missing(r, p + ".fish", "espèces du poste");
                    else
                        for (int j = 0; j < x.Fish.Count; j++)
                        {
                            string fp = $"{p}.fish[{j}]";
                            if (x.Fish[j] is null || !(d.Fish?.Any(f => f?.Item == x.Fish[j]) ?? false)) r.Add(fp, ValidationCode.UnknownReference, $"espèce « {x.Fish[j]} » inconnue de la pêche.");
                            else if (kinds.Contains(x.Fish[j]!)) r.Add(fp, ValidationCode.DuplicateId, $"« {x.Fish[j]} » en double.");
                            else kinds.Add(x.Fish[j]!);
                        }
                    if (r.Errors.Count == before) spots.Add(new FishingSpot(x.Id!, x.Name!, at, kinds));
                }

            if (!r.IsValid || document.ContentVersion is null) return new FishingLoadResult(r, null);
            return new FishingLoadResult(r, new FishingBook(document.SchemaVersion!.Value, document.ContentVersion, d.BiteMinMs!.Value, d.BiteMaxMs!.Value,
                d.WindowMs!.Value, d.Range!.Value, spots, fish));
        }

        private static bool Int(int? v, string path, int min, int max, string what, ValidationReport r)
        {
            if (v is null) { Missing(r, path, what); return false; }
            if (v < min || v > max) { r.Add(path, ValidationCode.OutOfRange, $"{what} hors de [{min}, {max}]."); return false; }
            return true;
        }

        private static bool Long(long? v, string path, long min, long max, string what, ValidationReport r)
        {
            if (v is null) { Missing(r, path, what); return false; }
            if (v < min || v > max) { r.Add(path, ValidationCode.OutOfRange, $"{what} hors de [{min}, {max}]."); return false; }
            return true;
        }

        private static void Missing(ValidationReport report, string path, string what)
        {
            if (!report.HasErrorAtOrAbove(path)) report.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({what}).");
        }
    }
}
