using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Items;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Collections;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.Data.Corruption
{
    // --- Documents (avant validation) ------------------------------------------------------------------

    /// <summary>Sous-lot V2.42.1 : corruption qui s'étend et recule, telle que lue (<c>kind: "corruption"</c>, <c>data/v0/world/corruption.json</c>).</summary>
    public sealed class CorruptionDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
        public CorruptionData? Data { get; set; }
    }

    public sealed class CorruptionData
    {
        public string? Zone { get; set; }
        public CorruptionAreaData? Source { get; set; }
        public int? SpreadDays { get; set; }
        public int? GrowDays { get; set; }
        public int? MaxStage { get; set; }
        public int? RespiteDays { get; set; }
        public string? PurifyItem { get; set; }
        public long? PurifyXp { get; set; }
        public int? StrengthPercent { get; set; }
        public string? PurifiedFact { get; set; }
        public List<CorruptionAreaData?>? Areas { get; set; }
    }

    public sealed class CorruptionAreaData
    {
        public string? Id { get; set; }
        public string? Place { get; set; }
        public PointData? Position { get; set; }
        public Fixed? Radius { get; set; }
        public PointData? Altar { get; set; }
        public List<string?>? From { get; set; }
    }

    // --- Définitions validées ----------------------------------------------------------------------------

    /// <summary>
    /// Sous-lot V2.42.1 : une terre que la corruption peut gagner (un disque de la zone) et son autel. La corruption y arrive
    /// <see cref="CorruptionBook.SpreadDays"/> jours après la plus tôt corrompue de ses terres d'origine (<see cref="From"/> : la source ou des
    /// terres données avant elle) ; un autel purifié la tient propre <see cref="CorruptionBook.RespiteDays"/> jours.
    /// </summary>
    public sealed class CorruptionArea
    {
        public string Id { get; }

        /// <summary>Lieu de la zone où elle se trouve (pour les textes), ou null.</summary>
        public string? Place { get; }
        public GroundVector Position { get; }
        public Fixed Radius { get; }

        /// <summary>Place de l'autel (dans le disque).</summary>
        public GroundVector Altar { get; }

        /// <summary>Terres d'où vient la corruption (identifiants ; la source comprise), dans l'ordre du fichier.</summary>
        public IReadOnlyList<string> From { get; }

        /// <summary>Rang dans le fichier (la source : -1).</summary>
        public int Index { get; }

        internal CorruptionArea(string id, string? place, GroundVector position, Fixed radius, GroundVector altar, IEnumerable<string> from, int index)
        {
            Id = id;
            Place = place;
            Position = position;
            Radius = radius;
            Altar = altar;
            From = Frozen.List(from, nameof(from));
            Index = index;
        }
    }

    public sealed class CorruptionBook
    {
        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public string ZoneId { get; }

        /// <summary>La source (le cercle des pierres levées) : toujours corrompue, au palier maximal ; elle ne se purifie pas.</summary>
        public CorruptionArea Source { get; }
        public int SpreadDays { get; }
        public int GrowDays { get; }
        public int MaxStage { get; }
        public int RespiteDays { get; }
        public string PurifyItem { get; }
        public long PurifyXp { get; }
        public string? PurifiedFact { get; }

        /// <summary>Créatures plus fortes dans une terre corrompue : ce pourcentage de PV et de dégâts en plus par palier (décision de Damien du 21 novembre 2026 : 10).</summary>
        public int StrengthPercent { get; }

        /// <summary>Terres que la corruption peut gagner, dans l'ordre du fichier (chacune après ses terres d'origine).</summary>
        public IReadOnlyList<CorruptionArea> Areas { get; }

        internal CorruptionBook(int schemaVersion, string contentVersion, string zoneId, CorruptionArea source, int spreadDays, int growDays, int maxStage,
            int respiteDays, string purifyItem, long purifyXp, string? purifiedFact, int strengthPercent, IEnumerable<CorruptionArea> areas)
        {
            StrengthPercent = strengthPercent;
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            ZoneId = zoneId;
            Source = source;
            SpreadDays = spreadDays;
            GrowDays = growDays;
            MaxStage = maxStage;
            RespiteDays = respiteDays;
            PurifyItem = purifyItem;
            PurifyXp = purifyXp;
            PurifiedFact = purifiedFact;
            Areas = Frozen.List(areas, nameof(areas));
        }

        public CorruptionArea? Of(string id) => Areas.FirstOrDefault(a => a.Id == id);
    }

    public sealed class CorruptionLoadResult
    {
        public ValidationReport Report { get; }
        public CorruptionBook? Book { get; }
        public bool IsValid => Book != null;

        internal CorruptionLoadResult(ValidationReport report, CorruptionBook? book)
        {
            Report = report;
            Book = book;
        }
    }

    /// <summary>
    /// Sous-lot V2.42.1 : validateur unique de la corruption (<c>kind: "corruption"</c>, version 1), contre la zone et le catalogue d'objets.
    /// <c>zone</c> (celle de la validation) ; <c>source</c> et chaque terre de <c>areas</c> (1 à <see cref="MaxAreas"/>) : <c>id</c> (local,
    /// unique, 64 caractères au plus), <c>place</c> facultatif (un lieu de la zone), <c>position</c> et <c>altar</c> dans l'aire praticable,
    /// l'autel dans le disque, <c>radius</c> de <see cref="MinRadius"/> à <see cref="MaxRadius"/> m ; une terre a 1 à
    /// <see cref="MaxFrom"/> terres d'origine (<c>from</c>) : la source ou des terres données avant elle (jamais de boucle), sans doublon ;
    /// la source n'a ni <c>from</c> ni <c>altar</c>. Règles : <c>spreadDays</c>, <c>growDays</c> et <c>respiteDays</c> de 1 à
    /// <see cref="MaxDays"/>, <c>maxStage</c> de 1 à <see cref="MaxStages"/>, <c>purifyItem</c> (objet du catalogue), <c>purifyXp</c> de 0 à
    /// <see cref="MaxXp"/>, <c>purifiedFact</c> facultatif (« fact.… »), <c>strengthPercent</c> de 0 à <see cref="MaxStrengthPercent"/> (force des créatures par palier).
    /// </summary>
    public static class CorruptionValidator
    {
        public const string Kind = "corruption";

        /// <summary>Version 1 (sous-lot V2.42.1).</summary>
        public const int CurrentSchemaVersion = 1;

        public const int MaxAreas = SavedCorruption.MaxEntries, MaxFrom = 4, MaxDays = 30, MaxStages = 8;
        public const long MaxXp = 100_000;

        /// <summary>Force des créatures par palier, au plus (au palier 8 : 900 %, sous la borne de <see cref="ZoneScaling.Strengthen"/>).</summary>
        public const int MaxStrengthPercent = 100;

        public static readonly Fixed MinRadius = Fixed.FromInt(10), MaxRadius = Fixed.FromInt(400);

        private static readonly Regex LocalId = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

        public static CorruptionLoadResult Validate(CorruptionDocument document, ZoneDefinition zone, ItemCatalog items, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            if (items is null) throw new ArgumentNullException(nameof(items));
            ValidationReport r = report ?? new ValidationReport();
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, CurrentSchemaVersion, r))
                return new CorruptionLoadResult(r, null);
            if (!(document.Data is CorruptionData d)) { Missing(r, "$.data", "données"); return new CorruptionLoadResult(r, null); }
            bool ok = true;
            if (d.Zone is null) { Missing(r, "$.data.zone", "zone"); ok = false; }
            else if (d.Zone != zone.Id) { r.Add("$.data.zone", ValidationCode.UnknownReference, $"zone « {d.Zone} » : la corruption est validée contre « {zone.Id} »."); ok = false; }
            int? Range(int? value, string field, string what, int min, int max)
            {
                if (value is null) { Missing(r, "$.data." + field, what); ok = false; return null; }
                if (value < min || value > max) { r.Add("$.data." + field, ValidationCode.OutOfRange, $"{what} hors de [{min}, {max}]."); ok = false; return null; }
                return value;
            }
            int? spread = Range(d.SpreadDays, "spreadDays", "jours pour gagner une terre voisine", 1, MaxDays);
            int? grow = Range(d.GrowDays, "growDays", "jours par palier", 1, MaxDays);
            int? maxStage = Range(d.MaxStage, "maxStage", "palier maximal", 1, MaxStages);
            int? respite = Range(d.RespiteDays, "respiteDays", "jours de répit d'un autel purifié", 1, MaxDays);
            int? strength = Range(d.StrengthPercent, "strengthPercent", "pourcentage de force des créatures par palier", 0, MaxStrengthPercent);
            if (d.PurifyItem is null) { Missing(r, "$.data.purifyItem", "objet de purification"); ok = false; }
            else if (!items.TryGetItem(d.PurifyItem, out _)) { r.Add("$.data.purifyItem", ValidationCode.UnknownReference, $"objet « {d.PurifyItem} » absent du catalogue."); ok = false; }
            if (d.PurifyXp is null) { Missing(r, "$.data.purifyXp", "expérience d'une purification"); ok = false; }
            else if (d.PurifyXp < 0 || d.PurifyXp > MaxXp) { r.Add("$.data.purifyXp", ValidationCode.OutOfRange, $"expérience hors de [0, {MaxXp}]."); ok = false; }
            if (d.PurifiedFact != null && !StoryValidator.IsFactId(d.PurifiedFact))
            { r.Add("$.data.purifiedFact", ValidationCode.InvalidValue, $"fait « {d.PurifiedFact} » mal formé : « fact.… »."); ok = false; }

            CombatSpace? space = zone.Setup.Space;
            bool Inside(GroundVector p) => space is null || (p.X >= space.Min.X && p.X <= space.Max.X && p.Z >= space.Min.Z && p.Z <= space.Max.Z);
            var places = new HashSet<string>(zone.Places.Select(p => p.Id), StringComparer.Ordinal);
            var known = new HashSet<string>(StringComparer.Ordinal);
            GroundVector? Point(PointData? pd, string path, string what)
            {
                if (pd?.X is null || pd.Z is null) { Missing(r, path, what + " [x, z]"); return null; }
                var p = new GroundVector(pd.X.Value, pd.Z.Value);
                if (!Inside(p)) { r.Add(path, ValidationCode.OutOfRange, what + " hors de l'aire praticable de la zone."); return null; }
                return p;
            }
            CorruptionArea? Area(CorruptionAreaData? a, string path, int index)
            {
                if (a is null) { Missing(r, path, "terre"); return null; }
                bool good = true;
                bool source = index < 0;
                if (a.Id is null) { Missing(r, path + ".id", "identifiant"); good = false; }
                else if (a.Id.Length > 64 || !LocalId.IsMatch(a.Id)) { r.Add(path + ".id", ValidationCode.InvalidValue, $"identifiant « {a.Id} » invalide : minuscules, chiffres et « _ », 64 caractères au plus."); good = false; }
                else if (known.Contains(a.Id)) { r.Add(path + ".id", ValidationCode.DuplicateId, $"terre « {a.Id} » déjà donnée."); good = false; }
                if (a.Place != null && !places.Contains(a.Place)) { r.Add(path + ".place", ValidationCode.UnknownReference, $"lieu « {a.Place} » absent de la zone."); good = false; }
                GroundVector? position = Point(a.Position, path + ".position", "centre");
                if (position is null) good = false;
                Fixed radius = Fixed.Zero;
                if (a.Radius is null) { Missing(r, path + ".radius", "rayon"); good = false; }
                else if (a.Radius < MinRadius || a.Radius > MaxRadius) { r.Add(path + ".radius", ValidationCode.OutOfRange, $"rayon hors de [{MinRadius}, {MaxRadius}] m."); good = false; }
                else radius = a.Radius.Value;
                GroundVector altar = default;
                if (source)
                {
                    if (a.Altar != null) { r.Add(path + ".altar", ValidationCode.UnknownField, "la source n'a pas d'autel à purifier."); good = false; }
                    if (a.From != null) { r.Add(path + ".from", ValidationCode.UnknownField, "la source n'a pas de terre d'origine."); good = false; }
                }
                else
                {
                    if (Point(a.Altar, path + ".altar", "autel") is GroundVector al)
                    {
                        altar = al;
                        if (position is GroundVector c && radius > Fixed.Zero && Beyond(al, c, radius))
                        { r.Add(path + ".altar", ValidationCode.OutOfRange, "autel hors du disque de sa terre."); good = false; }
                    }
                    else good = false;
                    if (a.From is null) { Missing(r, path + ".from", "terres d'origine"); good = false; }
                    else if (a.From.Count < 1 || a.From.Count > MaxFrom) { r.Add(path + ".from", ValidationCode.OutOfRange, $"de 1 à {MaxFrom} terres d'origine."); good = false; }
                    else
                    {
                        var seen = new HashSet<string>(StringComparer.Ordinal);
                        for (int k = 0; k < a.From.Count; k++)
                        {
                            string fp = $"{path}.from[{k}]";
                            if (a.From[k] is not string f) { Missing(r, fp, "terre d'origine"); good = false; }
                            else if (!known.Contains(f)) { r.Add(fp, ValidationCode.UnknownReference, $"terre « {f} » inconnue : la source ou une terre donnée avant celle-ci."); good = false; }
                            else if (!seen.Add(f)) { r.Add(fp, ValidationCode.DuplicateId, $"terre « {f} » déjà citée."); good = false; }
                        }
                    }
                }
                if (!good) return null;
                known.Add(a.Id!);
                return new CorruptionArea(a.Id!, a.Place, position!.Value, radius, source ? position.Value : altar,
                    source ? Array.Empty<string>() : a.From!.Select(f => f!), index);
            }

            CorruptionArea? sourceArea = d.Source is null ? null : Area(d.Source, "$.data.source", -1);
            if (d.Source is null) Missing(r, "$.data.source", "source");
            if (sourceArea is null) ok = false;
            var areas = new List<CorruptionArea>();
            if (d.Areas is null) { Missing(r, "$.data.areas", "terres"); ok = false; }
            else if (d.Areas.Count < 1 || d.Areas.Count > MaxAreas) { r.Add("$.data.areas", ValidationCode.OutOfRange, $"de 1 à {MaxAreas} terres."); ok = false; }
            else if (sourceArea != null)
                for (int i = 0; i < d.Areas.Count; i++)
                {
                    if (Area(d.Areas[i], $"$.data.areas[{i}]", i) is CorruptionArea a) areas.Add(a);
                    else ok = false;
                }
            if (!ok || !r.IsValid) return new CorruptionLoadResult(r, null);
            return new CorruptionLoadResult(r, new CorruptionBook(document.SchemaVersion!.Value, document.ContentVersion!, zone.Id, sourceArea!, spread!.Value,
                grow!.Value, maxStage!.Value, respite!.Value, d.PurifyItem!, d.PurifyXp!.Value, d.PurifiedFact, strength!.Value, areas));
        }

        /// <summary>Le point <paramref name="p"/> est-il à plus de <paramref name="radius"/> de <paramref name="center"/> (calcul vérifié) ?</summary>
        public static bool Beyond(GroundVector p, GroundVector center, Fixed radius)
        {
            long dx = checked(p.X.Raw - center.X.Raw), dz = checked(p.Z.Raw - center.Z.Raw);
            return checked(dx * dx + dz * dz) > checked(radius.Raw * radius.Raw);
        }

        private static void Missing(ValidationReport r, string path, string what) => r.Add(path, ValidationCode.MissingField, $"champ obligatoire manquant : {what}.");
    }
}
