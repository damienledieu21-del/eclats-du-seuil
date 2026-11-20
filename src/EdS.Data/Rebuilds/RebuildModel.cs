using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Items;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Collections;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.Data.Rebuilds
{
    // --- Documents (avant validation) ------------------------------------------------------------------

    /// <summary>Lieux reconstruits : catalogue des chantiers, tel que lu (<c>kind: "rebuilds"</c>, <c>data/v0/world/rebuilds.json</c>).</summary>
    public sealed class RebuildDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
        public RebuildData? Data { get; set; }
    }

    public sealed class RebuildData
    {
        public List<RebuildSiteData?>? Sites { get; set; }
    }

    public sealed class RebuildSiteData
    {
        public string? Id { get; set; }
        public string? Place { get; set; }
        public Fixed? Margin { get; set; }
        public List<RebuildStageData?>? Stages { get; set; }

        /// <summary>V2.43.2b : ce que le lieu apporte une fois reconstruit (facultatif).</summary>
        public RebuildRebuiltData? Rebuilt { get; set; }
    }

    /// <summary>V2.43.2b : PNJ qui ne sont dans le monde qu'une fois le lieu reconstruit.</summary>
    public sealed class RebuildRebuiltData
    {
        public List<string?>? Npcs { get; set; }
        public List<string?>? SafePoints { get; set; }

        /// <summary>V2.43.4 (version 2) : portes de la zone (vers l'intérieur du lieu) qui ne s'ouvrent qu'une fois le lieu reconstruit.</summary>
        public List<string?>? Doors { get; set; }
    }

    public sealed class RebuildStageData
    {
        public string? Id { get; set; }
        public List<RebuildNeedData?>? Needs { get; set; }
    }

    /// <summary>Un besoin : exactement une sorte parmi <c>coins</c>, <c>item</c> (avec <c>count</c>), <c>defeat</c>, <c>discover</c>.</summary>
    public sealed class RebuildNeedData
    {
        public string? Id { get; set; }
        public long? Coins { get; set; }
        public string? Item { get; set; }
        public int? Count { get; set; }
        public int? Defeat { get; set; }
        public string? Discover { get; set; }

        /// <summary>V2.43.2b : un endroit où aller, dans le lieu ou sa marge.</summary>
        public PointData? Reach { get; set; }
    }

    // --- Modèle validé ---------------------------------------------------------------------------------

    /// <summary>Lieux reconstruits : sorte d'un besoin de palier (décision de Damien du 19 novembre 2026 : contributions variées).</summary>
    public enum RebuildNeedKind
    {
        /// <summary>De l'or : une somme en cuivre, payée sur place (en plusieurs fois si besoin).</summary>
        Coins = 0,

        /// <summary>Des matériaux de métier : un objet du catalogue de type matériau, apporté sur place.</summary>
        Item = 1,

        /// <summary>Des combats : créatures vaincues pendant que le personnage est dans le lieu ou autour (marge du lieu).</summary>
        Defeat = 2,

        /// <summary>De l'exploration : une découverte de l'histoire, faite n'importe où.</summary>
        Discover = 3,

        /// <summary>V2.43.2b : de l'exploration autour du lieu : aller à un endroit (à <see cref="RebuildValidator.ReachRadius"/> près).</summary>
        Reach = 4,
    }

    /// <summary>Lieux reconstruits : un besoin d'un palier, et la quantité voulue (1 pour une découverte).</summary>
    public sealed class RebuildNeed
    {
        public string Id { get; }
        public RebuildNeedKind Kind { get; }

        /// <summary>Objet (sorte <see cref="RebuildNeedKind.Item"/>) ou découverte (sorte <see cref="RebuildNeedKind.Discover"/>), sinon null.</summary>
        public string? Target { get; }

        public long Amount { get; }

        /// <summary>Endroit où aller (sorte <see cref="RebuildNeedKind.Reach"/>), sinon null.</summary>
        public GroundVector? Point { get; }

        internal RebuildNeed(string id, RebuildNeedKind kind, string? target, long amount, GroundVector? point = null)
        {
            Point = point;
            Id = id;
            Kind = kind;
            Target = target;
            Amount = amount;
        }
    }

    /// <summary>Lieux reconstruits : un palier du chantier ; il est fini quand tous ses besoins sont remplis.</summary>
    public sealed class RebuildStage
    {
        public string Id { get; }
        public IReadOnlyList<RebuildNeed> Needs { get; }

        internal RebuildStage(string id, IEnumerable<RebuildNeed> needs)
        {
            Id = id;
            Needs = Frozen.List(needs, nameof(needs));
        }

        public RebuildNeed? Need(string id)
        {
            for (int i = 0; i < Needs.Count; i++) if (Needs[i].Id == id) return Needs[i];
            return null;
        }
    }

    /// <summary>Lieux reconstruits : un lieu de la zone qu'on peut reconstruire, et ses paliers dans l'ordre.</summary>
    public sealed class RebuildSite
    {
        public string Id { get; }

        /// <summary>Lieu nommé de la zone (rectangle) où l'on apporte or et matériaux, et où l'on combat pour le chantier.</summary>
        public Place Place { get; }

        /// <summary>Marge autour du lieu, en mètres (« autour du lieu »), de 0 à <see cref="RebuildValidator.MaxMargin"/>.</summary>
        public Fixed Margin { get; }

        public IReadOnlyList<RebuildStage> Stages { get; }

        /// <summary>V2.43.2b : PNJ de l'histoire qui ne sont dans le monde qu'une fois le lieu reconstruit.</summary>
        public IReadOnlyList<string> RebuiltNpcs { get; }

        /// <summary>V2.43.2b : points sûrs de la zone qui ne s'ouvrent (repos, retour, découverte, voyage) qu'une fois le lieu reconstruit.</summary>
        public IReadOnlyList<string> RebuiltSafePoints { get; }

        /// <summary>V2.43.4 : portes de la zone (vers l'intérieur du lieu) fermées tant que le lieu n'est pas reconstruit.</summary>
        public IReadOnlyList<string> RebuiltDoors { get; }

        internal RebuildSite(string id, Place place, Fixed margin, IEnumerable<RebuildStage> stages, IEnumerable<string>? rebuiltNpcs = null,
            IEnumerable<string>? rebuiltSafePoints = null, IEnumerable<string>? rebuiltDoors = null)
        {
            RebuiltDoors = Frozen.List(rebuiltDoors ?? Array.Empty<string>(), nameof(rebuiltDoors));
            RebuiltSafePoints = Frozen.List(rebuiltSafePoints ?? Array.Empty<string>(), nameof(rebuiltSafePoints));
            RebuiltNpcs = Frozen.List(rebuiltNpcs ?? Array.Empty<string>(), nameof(rebuiltNpcs));
            Id = id;
            Place = place;
            Margin = margin;
            Stages = Frozen.List(stages, nameof(stages));
        }

        /// <summary>Le point est dans le lieu ou dans sa marge (bords compris).</summary>
        public bool Around(GroundVector p) =>
            p.X >= Place.Min.X - Margin && p.X <= Place.Max.X + Margin && p.Z >= Place.Min.Z - Margin && p.Z <= Place.Max.Z + Margin;

        /// <summary>Fait appris à la fin du palier <paramref name="stage"/> (« fact.rebuild.hameau.deblaiement »).</summary>
        public string StageFact(RebuildStage stage) => $"fact.rebuild.{Id}.{stage.Id}";

        /// <summary>Fait appris au premier apport : le chantier est ouvert (échafaudages).</summary>
        public string StartedFact => $"fact.rebuild.{Id}.started";

        /// <summary>Fait appris quand tous les paliers sont finis : le lieu est reconstruit (pour toujours).</summary>
        public string DoneFact => $"fact.rebuild.{Id}.done";
    }

    /// <summary>Lieux reconstruits : catalogue validé, immuable.</summary>
    public sealed class RebuildBook
    {
        public int SchemaVersion { get; }
        public string ContentVersion { get; }

        /// <summary>Zone des lieux du catalogue (celle contre laquelle il a été validé).</summary>
        public string ZoneId { get; }

        public IReadOnlyList<RebuildSite> Sites { get; }

        internal RebuildBook(int schemaVersion, string contentVersion, string zoneId, IEnumerable<RebuildSite> sites)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            ZoneId = zoneId;
            Sites = Frozen.List(sites, nameof(sites));
        }

        public RebuildSite? Of(string id)
        {
            for (int i = 0; i < Sites.Count; i++) if (Sites[i].Id == id) return Sites[i];
            return null;
        }
    }

    public sealed class RebuildLoadResult
    {
        public ValidationReport Report { get; }
        public RebuildBook? Book { get; }
        public bool IsValid => Book != null;

        internal RebuildLoadResult(ValidationReport report, RebuildBook? book)
        {
            Report = report;
            Book = book;
        }
    }

    /// <summary>
    /// Lieux reconstruits : validateur unique du catalogue (<c>kind: "rebuilds"</c>), contre la zone (lieux nommés), le catalogue d'objets
    /// (matériaux) et l'histoire (découvertes). Identifiants uniques (lieux du catalogue, paliers d'un lieu, besoins d'un palier) ; un lieu
    /// de la zone n'a qu'un chantier ; chaque besoin a exactement une sorte et une quantité dans ses bornes.
    /// </summary>
    public static class RebuildValidator
    {
        public const string Kind = "rebuilds";

        /// <summary>Version 1 (sous-lot V2.43.1, décisions de Damien du 19 novembre 2026) : <c>sites</c>, <c>stages</c>, <c>needs</c>.</summary>
        public const int Schema1 = 1;

        /// <summary>
        /// Version 2 (sous-lot V2.43.4, décision de Damien du 20 novembre 2026) : <c>rebuilt.doors</c>, portes des intérieurs des lieux,
        /// fermées tant que le lieu n'est pas reconstruit. La version 1 est encore lue (sans <c>doors</c>).
        /// </summary>
        public const int CurrentSchemaVersion = 2;

        public const int MaxSites = SavedRebuilds.MaxEntries, MaxStages = SavedRebuild.MaxStages, MaxNeeds = SavedRebuild.MaxNeeds;

        /// <summary>Créatures au plus pour un besoin de combat, objets au plus pour un besoin de matériaux.</summary>
        public const int MaxDefeat = 999, MaxCount = 9_999;

        /// <summary>Marge au plus autour d'un lieu, en mètres.</summary>
        public static readonly Fixed MaxMargin = Fixed.FromInt(256);

        /// <summary>V2.43.2b : un endroit est atteint à 4 m près (écart entre les centres).</summary>
        public static readonly Fixed ReachRadius = Fixed.FromInt(4);

        /// <summary>V2.43.2b : PNJ au plus apportés par un lieu reconstruit.</summary>
        public const int MaxRebuiltNpcs = 8;

        public static RebuildLoadResult Validate(RebuildDocument document, ZoneDefinition zone, ItemCatalog items, StoryBook story, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            if (items is null) throw new ArgumentNullException(nameof(items));
            if (story is null) throw new ArgumentNullException(nameof(story));
            ValidationReport r = report ?? new ValidationReport();
            int expected = document.SchemaVersion == Schema1 ? Schema1 : CurrentSchemaVersion;   // la version 1 est encore lue
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, expected, r))
                return new RebuildLoadResult(r, null);
            if (document.Data?.Sites is not List<RebuildSiteData?> data) { Missing(r, "$.data.sites", "chantiers"); return new RebuildLoadResult(r, null); }
            if (data.Count > MaxSites) { r.Add("$.data.sites", ValidationCode.LimitExceeded, $"au plus {MaxSites} chantiers."); return new RebuildLoadResult(r, null); }

            var discoveries = new HashSet<string>(story.Discoveries.Select(d => d.Id), StringComparer.Ordinal);
            var sites = new List<RebuildSite>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var places = new HashSet<string>(StringComparer.Ordinal);
            var rebuiltNpcs = new HashSet<string>(StringComparer.Ordinal);
            var rebuiltSafePoints = new HashSet<string>(StringComparer.Ordinal);
            var rebuiltDoors = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < data.Count; i++)
            {
                string p = $"$.data.sites[{i}]";
                if (data[i] is not RebuildSiteData x) { Missing(r, p, "chantier"); continue; }
                bool ok = true;
                if (!SavedRebuild.IsLocalId(x.Id)) { r.Add(p + ".id", ValidationCode.InvalidValue, $"identifiant « {x.Id} » mal formé."); ok = false; }
                else if (!ids.Add(x.Id!)) { r.Add(p + ".id", ValidationCode.DuplicateId, $"chantier « {x.Id} » déjà décrit."); ok = false; }
                Place? place = null;
                if (x.Place is null) { Missing(r, p + ".place", "lieu nommé de la zone"); ok = false; }
                else if ((place = zone.Places.FirstOrDefault(z => z.Id == x.Place)) is null)
                { r.Add(p + ".place", ValidationCode.UnknownReference, $"lieu « {x.Place} » inconnu dans la zone « {zone.Id} »."); ok = false; }
                else if (!places.Add(x.Place)) { r.Add(p + ".place", ValidationCode.DuplicateId, $"le lieu « {x.Place} » a déjà un chantier."); ok = false; }
                Fixed margin = x.Margin ?? Fixed.Zero;
                bool marginOk = margin >= Fixed.Zero && margin <= MaxMargin;
                if (!marginOk) { r.Add(p + ".margin", ValidationCode.OutOfRange, $"marge hors de [0, {MaxMargin}] m."); ok = false; }

                var stages = new List<RebuildStage>();
                if (x.Stages is null || x.Stages.Count == 0) { Missing(r, p + ".stages", "au moins un palier"); ok = false; }
                else if (x.Stages.Count > MaxStages) { r.Add(p + ".stages", ValidationCode.LimitExceeded, $"au plus {MaxStages} paliers."); ok = false; }
                else
                {
                    var stageIds = new HashSet<string>(StringComparer.Ordinal);
                    for (int s = 0; s < x.Stages.Count; s++)
                    {
                        string sp = $"{p}.stages[{s}]";
                        if (x.Stages[s] is not RebuildStageData st) { Missing(r, sp, "palier"); ok = false; continue; }
                        if (!SavedRebuild.IsLocalId(st.Id) || st.Id == "started" || st.Id == "done")
                        { r.Add(sp + ".id", ValidationCode.InvalidValue, $"identifiant « {st.Id} » mal formé (« started » et « done » sont réservés)."); ok = false; }
                        else if (!stageIds.Add(st.Id!)) { r.Add(sp + ".id", ValidationCode.DuplicateId, $"palier « {st.Id} » déjà décrit."); ok = false; }
                        var needs = new List<RebuildNeed>();
                        if (st.Needs is null || st.Needs.Count == 0) { Missing(r, sp + ".needs", "au moins un besoin"); ok = false; }
                        else if (st.Needs.Count > MaxNeeds) { r.Add(sp + ".needs", ValidationCode.LimitExceeded, $"au plus {MaxNeeds} besoins."); ok = false; }
                        else
                        {
                            var needIds = new HashSet<string>(StringComparer.Ordinal);
                            for (int n = 0; n < st.Needs.Count; n++)
                            {
                                RebuildNeed? need = Need(st.Needs[n], $"{sp}.needs[{n}]", needIds, items, discoveries, r, place, marginOk ? margin : (Fixed?)null);
                                if (need is null) ok = false; else needs.Add(need);
                            }
                        }
                        if (ok) stages.Add(new RebuildStage(st.Id!, needs));
                    }
                }
                var npcs = new List<string>();
                if (x.Rebuilt != null)
                {
                    string rp = p + ".rebuilt.npcs";
                    if (x.Rebuilt.Npcs is null) { }   // facultatif
                    else if (x.Rebuilt.Npcs.Count > MaxRebuiltNpcs) { r.Add(rp, ValidationCode.LimitExceeded, $"au plus {MaxRebuiltNpcs} PNJ."); ok = false; }
                    else
                        for (int k = 0; k < x.Rebuilt.Npcs.Count; k++)
                        {
                            string? npc = x.Rebuilt.Npcs[k];
                            if (npc is null || !story.HasNpc(npc)) { r.Add($"{rp}[{k}]", ValidationCode.UnknownReference, $"PNJ « {npc} » inconnu dans l'histoire."); ok = false; }
                            else if (!rebuiltNpcs.Add(npc)) { r.Add($"{rp}[{k}]", ValidationCode.DuplicateId, $"PNJ « {npc} » déjà apporté par un lieu reconstruit."); ok = false; }
                            else npcs.Add(npc);
                        }
                }
                var safePoints = new List<string>();
                if (x.Rebuilt?.SafePoints is List<string?> sps)
                {
                    string sp = p + ".rebuilt.safePoints";
                    if (sps.Count > MaxRebuiltNpcs) { r.Add(sp, ValidationCode.LimitExceeded, $"au plus {MaxRebuiltNpcs} points sûrs."); ok = false; }
                    else
                        for (int k = 0; k < sps.Count; k++)
                        {
                            string? id = sps[k];
                            if (id is null || zone.SafePoints.All(s => s.Id != id)) { r.Add($"{sp}[{k}]", ValidationCode.UnknownReference, $"point sûr « {id} » inconnu dans la zone."); ok = false; }
                            else if (id == zone.Entry.Id) { r.Add($"{sp}[{k}]", ValidationCode.InvalidValue, "l'entrée de la zone est toujours ouverte."); ok = false; }
                            else if (!rebuiltSafePoints.Add(id)) { r.Add($"{sp}[{k}]", ValidationCode.DuplicateId, $"point sûr « {id} » déjà ouvert par un lieu reconstruit."); ok = false; }
                            else safePoints.Add(id);
                        }
                }
                var doors = new List<string>();
                if (x.Rebuilt?.Doors is List<string?> ds)
                {
                    string dp = p + ".rebuilt.doors";
                    if (document.SchemaVersion == Schema1) { r.Add(dp, ValidationCode.UnknownField, $"champ inconnu en version {Schema1} (portes des lieux : version {CurrentSchemaVersion})."); ok = false; }
                    else if (ds.Count > MaxRebuiltNpcs) { r.Add(dp, ValidationCode.LimitExceeded, $"au plus {MaxRebuiltNpcs} portes."); ok = false; }
                    else
                        for (int k = 0; k < ds.Count; k++)
                        {
                            string? id = ds[k];
                            if (id is null || zone.Doors.All(d => d.Id != id)) { r.Add($"{dp}[{k}]", ValidationCode.UnknownReference, $"porte « {id} » inconnue dans la zone."); ok = false; }
                            else if (!rebuiltDoors.Add(id)) { r.Add($"{dp}[{k}]", ValidationCode.DuplicateId, $"porte « {id} » déjà ouverte par un lieu reconstruit."); ok = false; }
                            else doors.Add(id);
                        }
                }
                if (ok) sites.Add(new RebuildSite(x.Id!, place!, margin, stages, npcs, safePoints, doors));
            }

            if (!r.IsValid || document.ContentVersion is null) return new RebuildLoadResult(r, null);
            return new RebuildLoadResult(r, new RebuildBook(document.SchemaVersion!.Value, document.ContentVersion, zone.Id, sites));
        }

        private static RebuildNeed? Need(RebuildNeedData? x, string p, HashSet<string> ids, ItemCatalog items, HashSet<string> discoveries, ValidationReport r,
            Place? place, Fixed? margin)
        {
            if (x is null) { Missing(r, p, "besoin"); return null; }
            bool ok = true;
            if (!SavedRebuild.IsLocalId(x.Id)) { r.Add(p + ".id", ValidationCode.InvalidValue, $"identifiant « {x.Id} » mal formé."); ok = false; }
            else if (!ids.Add(x.Id!)) { r.Add(p + ".id", ValidationCode.DuplicateId, $"besoin « {x.Id} » déjà décrit."); ok = false; }
            int kinds = (x.Coins != null ? 1 : 0) + (x.Item != null ? 1 : 0) + (x.Defeat != null ? 1 : 0) + (x.Discover != null ? 1 : 0) + (x.Reach != null ? 1 : 0);
            if (kinds != 1)
            {
                r.Add(p, ValidationCode.InvalidValue, "un besoin a exactement une sorte : coins, item (avec count), defeat, discover ou reach.");
                return null;
            }
            if (x.Count != null && x.Item is null) { r.Add(p + ".count", ValidationCode.UnknownField, "count va seulement avec item."); return null; }
            if (x.Coins is long coins)
            {
                if (coins < 1 || coins > Coins.Max) { r.Add(p + ".coins", ValidationCode.OutOfRange, $"somme hors de [1, {Coins.Max}] cuivre."); return null; }
                return ok ? new RebuildNeed(x.Id!, RebuildNeedKind.Coins, null, coins) : null;
            }
            if (x.Item is string item)
            {
                if (!items.TryGetItem(item, out ItemDefinition def)) { r.Add(p + ".item", ValidationCode.UnknownReference, $"objet « {item} » inconnu."); ok = false; }
                else if (def.Type != ItemType.Material) { r.Add(p + ".item", ValidationCode.InvalidValue, $"« {item} » n'est pas un matériau de métier."); ok = false; }
                if (x.Count is null) { Missing(r, p + ".count", "nombre d'objets"); ok = false; }
                else if (x.Count < 1 || x.Count > MaxCount) { r.Add(p + ".count", ValidationCode.OutOfRange, $"nombre hors de [1, {MaxCount}]."); ok = false; }
                return ok ? new RebuildNeed(x.Id!, RebuildNeedKind.Item, item, x.Count!.Value) : null;
            }
            if (x.Defeat is int defeat)
            {
                if (defeat < 1 || defeat > MaxDefeat) { r.Add(p + ".defeat", ValidationCode.OutOfRange, $"nombre hors de [1, {MaxDefeat}]."); return null; }
                return ok ? new RebuildNeed(x.Id!, RebuildNeedKind.Defeat, null, defeat) : null;
            }
            if (x.Reach is PointData reach)
            {
                if (reach.X is null || reach.Z is null) { Missing(r, p + ".reach", "[x, z]"); return null; }
                var at = new GroundVector(reach.X.Value, reach.Z.Value);
                // Revue Codex de la PR #224 : sans lieu ni marge valides (déjà signalés), pas de bornes à calculer (aucun débordement).
                if (place is null || !(margin is Fixed mg)) return null;
                if (at.X < place.Min.X - mg || at.X > place.Max.X + mg || at.Z < place.Min.Z - mg || at.Z > place.Max.Z + mg)
                { r.Add(p + ".reach", ValidationCode.OutOfRange, "endroit hors du lieu et de sa marge."); return null; }
                return ok ? new RebuildNeed(x.Id!, RebuildNeedKind.Reach, null, 1, at) : null;
            }
            string discovery = x.Discover!;
            if (!discoveries.Contains(discovery)) { r.Add(p + ".discover", ValidationCode.UnknownReference, $"découverte « {discovery} » inconnue dans l'histoire."); return null; }
            return ok ? new RebuildNeed(x.Id!, RebuildNeedKind.Discover, discovery, 1) : null;
        }

        private static void Missing(ValidationReport report, string path, string what)
        {
            if (!report.HasErrorAtOrAbove(path)) report.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({what}).");
        }
    }
}
