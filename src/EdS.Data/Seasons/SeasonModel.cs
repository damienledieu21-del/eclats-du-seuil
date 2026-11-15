using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Collections;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Time;

namespace EdS.Data.Seasons
{
    // --- Documents (avant validation) ------------------------------------------------------------------

    /// <summary>Saisons : contenu qui dépend de la saison, tel que lu (<c>kind: "seasons"</c>, <c>data/v0/world/seasons.json</c>).</summary>
    public sealed class SeasonDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
        public SeasonData? Data { get; set; }
    }

    public sealed class SeasonData
    {
        public MistEchoesData? MistEchoes { get; set; }
        public List<SeasonPassageData?>? Passages { get; set; }
        public List<SeasonLockData?>? Locks { get; set; }
    }

    public sealed class MistEchoesData
    {
        /// <summary>Taux de présence d'un Écho dans la brume, par saison (clé : identifiant de saison).</summary>
        public Dictionary<string, Fixed?>? Presence { get; set; }

        public int? MaxPerVolume { get; set; }
    }

    public sealed class SeasonPassageData
    {
        public string? Id { get; set; }
        public string? Requires { get; set; }
    }

    public sealed class SeasonLockData
    {
        public string? Id { get; set; }
        public string? Passage { get; set; }

        /// <summary>Point d'accroche dans la zone ; absent tant que le lieu n'existe pas sur la carte.</summary>
        public PointData? Anchor { get; set; }
    }

    // --- Modèle validé ---------------------------------------------------------------------------------

    /// <summary>Saisons : condition d'ouverture d'un passage saisonnier.</summary>
    public enum PassageCondition
    {
        /// <summary>Ouvert seulement quand la glace du lac est praticable (<see cref="IcePhase.Walkable"/>).</summary>
        Ice = 0,
    }

    /// <summary>Saisons : passage saisonnier (le passage du lac gelé).</summary>
    public sealed class SeasonalPassage
    {
        public string Id { get; }
        public PassageCondition Requires { get; }

        internal SeasonalPassage(string id, PassageCondition requires)
        {
            Id = id;
            Requires = requires;
        }

        /// <summary>Ouvert avec la glace <paramref name="ice"/> (glace inconnue, date non fournie : fermé).</summary>
        public bool IsOpen(IcePhase? ice) => Requires == PassageCondition.Ice && ice == IcePhase.Walkable;
    }

    /// <summary>
    /// Saisons : verrou d'un lieu qui ne s'atteint que par un passage saisonnier (l'île secrète). <see cref="Anchor"/> est son point
    /// d'accroche dans la zone ; null tant que le lieu n'existe pas encore sur la carte (le verrou est prêt, le lieu viendra).
    /// </summary>
    public sealed class SeasonalLock
    {
        public string Id { get; }
        public string Passage { get; }
        public GroundVector? Anchor { get; }

        internal SeasonalLock(string id, string passage, GroundVector? anchor)
        {
            Id = id;
            Passage = passage;
            Anchor = anchor;
        }
    }

    /// <summary>
    /// Saisons : contenu saisonnier validé, immuable : taux de présence des Échos dans la brume par saison, passages et verrous saisonniers
    /// (les nœuds de récolte de saison sont dans les métiers, version 4). Les questions prennent la glace du moment, null si l'hôte n'a pas
    /// fourni de date : alors rien de saisonnier n'est ouvert (option prudente).
    /// </summary>
    public sealed class SeasonBook
    {
        private readonly Fixed[] _presence;

        public int SchemaVersion { get; }
        public string ContentVersion { get; }

        /// <summary>Échos au plus dans un même volume de brume.</summary>
        public int MaxEchoesPerVolume { get; }

        public IReadOnlyList<SeasonalPassage> Passages { get; }
        public IReadOnlyList<SeasonalLock> Locks { get; }

        internal SeasonBook(int schemaVersion, string contentVersion, Fixed[] presence, int maxPerVolume, IEnumerable<SeasonalPassage> passages,
            IEnumerable<SeasonalLock> locks)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            _presence = (Fixed[])presence.Clone();
            MaxEchoesPerVolume = maxPerVolume;
            Passages = Frozen.List(passages, nameof(passages));
            Locks = Frozen.List(locks, nameof(locks));
        }

        /// <summary>Taux de présence d'un Écho dans la brume pendant la saison <paramref name="season"/> (de 0 à <see cref="SeasonValidator.MaxEchoPresence"/>).</summary>
        public Fixed MistEchoPresence(Season season) => _presence[(int)season];

        public SeasonalPassage Passage(string id) =>
            Passages.FirstOrDefault(p => p.Id == id) ?? throw new ArgumentException($"Passage saisonnier « {id} » inconnu.", nameof(id));

        public SeasonalLock Lock(string id) =>
            Locks.FirstOrDefault(l => l.Id == id) ?? throw new ArgumentException($"Verrou saisonnier « {id} » inconnu.", nameof(id));

        /// <summary>Le verrou s'ouvre quand son passage est ouvert.</summary>
        public bool LockOpen(string id, IcePhase? ice) => Passage(Lock(id).Passage).IsOpen(ice);
    }

    public sealed class SeasonLoadResult
    {
        public ValidationReport Report { get; }
        public SeasonBook? Book { get; }
        public bool IsValid => Book != null;

        internal SeasonLoadResult(ValidationReport report, SeasonBook? book)
        {
            Report = report;
            Book = book;
        }
    }

    /// <summary>
    /// Saisons : validateur unique du contenu saisonnier (<c>kind: "seasons"</c>), contre la zone (points d'accroche dans l'aire
    /// praticable) : présence des Échos dans la
    /// brume donnée pour les quatre saisons, dans [0, <see cref="MaxEchoPresence"/>], plus forte en automne que dans toute autre saison
    /// (décision de Damien du 14 novembre 2026) ; passages d'identifiants uniques, à condition connue (<c>ice</c>) ; verrous liés à un passage
    /// connu.
    /// </summary>
    public static class SeasonValidator
    {
        public const string Kind = "seasons";

        /// <summary>Version 1 (saisons, 15 novembre 2026) : <c>mistEchoes</c>, <c>passages</c>, <c>locks</c> (les nœuds de saison sont dans les métiers, version 4).</summary>
        public const int CurrentSchemaVersion = 1;

        public const int MaxPassages = 16, MaxLocks = 16, MinPerVolume = 1, MaxPerVolume = 8;

        /// <summary>Taux de présence d'un Écho dans la brume, au plus (borne de sûreté : une chance sur deux à chaque essai).</summary>
        public static readonly Fixed MaxEchoPresence = Fixed.FromRatio(1, 2);

        private static readonly Regex LocalId = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

        public static bool IsLocalId(string? id) => id != null && id.Length <= 64 && LocalId.IsMatch(id);

        public static SeasonLoadResult Validate(SeasonDocument document, ZoneDefinition zone, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            ValidationReport r = report ?? new ValidationReport();
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, CurrentSchemaVersion, r))
                return new SeasonLoadResult(r, null);
            SeasonData? d = document.Data;
            if (d is null) { Missing(r, "$.data", "mistEchoes, passages, locks"); return new SeasonLoadResult(r, null); }

            var presence = new Fixed[4];
            int perVolume = 0;
            const string mp = "$.data.mistEchoes";
            if (d.MistEchoes is null) Missing(r, mp, "Échos dans la brume : presence, maxPerVolume");
            else
            {
                MistEchoesData m = d.MistEchoes;
                bool all = true;
                if (m.Presence is null) { Missing(r, mp + ".presence", "taux de présence par saison"); all = false; }
                else
                {
                    foreach (string key in m.Presence.Keys.OrderBy(k => k, StringComparer.Ordinal))
                        if (SeasonCalendar.Parse(key) is null)
                        { r.Add($"{mp}.presence.{key}", ValidationCode.UnknownReference, $"saison inconnue « {key} » ; possibles : spring, summer, autumn, winter."); all = false; }
                    foreach (Season s in new[] { Season.Spring, Season.Summer, Season.Autumn, Season.Winter })
                    {
                        string id = SeasonCalendar.Id(s), sp = $"{mp}.presence.{id}";
                        if (!m.Presence.TryGetValue(id, out Fixed? v) || v is null) { Missing(r, sp, "taux de présence de la saison"); all = false; }
                        else if (v.Value < Fixed.Zero || v.Value > MaxEchoPresence)
                        { r.Add(sp, ValidationCode.OutOfRange, $"taux de présence hors de [0, {MaxEchoPresence}]."); all = false; }
                        else presence[(int)s] = v.Value;
                    }
                    if (all)
                        foreach (Season s in new[] { Season.Spring, Season.Summer, Season.Winter })
                            if (presence[(int)Season.Autumn] <= presence[(int)s])
                            {
                                r.Add($"{mp}.presence.autumn", ValidationCode.InvalidValue,
                                    $"plus d'Échos dans la brume en automne : son taux doit dépasser celui de « {SeasonCalendar.Id(s)} » ({presence[(int)s]}).");
                                break;
                            }
                }
                if (m.MaxPerVolume is null) Missing(r, mp + ".maxPerVolume", "Échos au plus par volume de brume");
                else if (m.MaxPerVolume < MinPerVolume || m.MaxPerVolume > MaxPerVolume)
                    r.Add(mp + ".maxPerVolume", ValidationCode.OutOfRange, $"Échos par volume hors de [{MinPerVolume}, {MaxPerVolume}].");
                else perVolume = m.MaxPerVolume.Value;
            }

            var passages = new List<SeasonalPassage>();
            var passageIds = new HashSet<string>(StringComparer.Ordinal);
            if (d.Passages is null) Missing(r, "$.data.passages", "passages saisonniers");
            else if (d.Passages.Count > MaxPassages) r.Add("$.data.passages", ValidationCode.LimitExceeded, $"au plus {MaxPassages} passages.");
            else
                for (int i = 0; i < d.Passages.Count; i++)
                {
                    string p = $"$.data.passages[{i}]";
                    if (d.Passages[i] is not SeasonPassageData x) { Missing(r, p, "passage"); continue; }
                    bool ok = true;
                    if (!IsLocalId(x.Id)) { r.Add(p + ".id", ValidationCode.InvalidValue, $"identifiant « {x.Id} » mal formé."); ok = false; }
                    else if (!passageIds.Add(x.Id!)) { r.Add(p + ".id", ValidationCode.DuplicateId, $"passage « {x.Id} » déjà décrit."); ok = false; }
                    if (x.Requires is null) { Missing(r, p + ".requires", "condition d'ouverture (ice)"); ok = false; }
                    else if (x.Requires != "ice") { r.Add(p + ".requires", ValidationCode.UnknownReference, $"condition inconnue « {x.Requires} » ; possible : ice."); ok = false; }
                    if (ok) passages.Add(new SeasonalPassage(x.Id!, PassageCondition.Ice));
                }

            var locks = new List<SeasonalLock>();
            var lockIds = new HashSet<string>(StringComparer.Ordinal);
            CombatSpace space = zone.Setup.Space!;
            if (d.Locks is null) Missing(r, "$.data.locks", "verrous saisonniers");
            else if (d.Locks.Count > MaxLocks) r.Add("$.data.locks", ValidationCode.LimitExceeded, $"au plus {MaxLocks} verrous.");
            else
                for (int i = 0; i < d.Locks.Count; i++)
                {
                    string p = $"$.data.locks[{i}]";
                    if (d.Locks[i] is not SeasonLockData x) { Missing(r, p, "verrou"); continue; }
                    bool ok = true;
                    if (!IsLocalId(x.Id)) { r.Add(p + ".id", ValidationCode.InvalidValue, $"identifiant « {x.Id} » mal formé."); ok = false; }
                    else if (!lockIds.Add(x.Id!)) { r.Add(p + ".id", ValidationCode.DuplicateId, $"verrou « {x.Id} » déjà décrit."); ok = false; }
                    if (x.Passage is null) { Missing(r, p + ".passage", "passage qui ouvre le verrou"); ok = false; }
                    else if (!passageIds.Contains(x.Passage)) { r.Add(p + ".passage", ValidationCode.UnknownReference, $"passage « {x.Passage} » inconnu."); ok = false; }
                    GroundVector? anchor = null;
                    if (x.Anchor != null)
                    {
                        if (x.Anchor.X is null || x.Anchor.Z is null) { Missing(r, p + ".anchor", "[x, z]"); ok = false; }
                        else
                        {
                            var a = new GroundVector(x.Anchor.X.Value, x.Anchor.Z.Value);
                            if (a.X < space.Min.X || a.X > space.Max.X || a.Z < space.Min.Z || a.Z > space.Max.Z)
                            { r.Add(p + ".anchor", ValidationCode.OutOfRange, "point d'accroche hors de l'aire praticable de la zone."); ok = false; }
                            else anchor = a;
                        }
                    }
                    if (ok) locks.Add(new SeasonalLock(x.Id!, x.Passage!, anchor));
                }

            if (!r.IsValid || document.ContentVersion is null) return new SeasonLoadResult(r, null);
            return new SeasonLoadResult(r, new SeasonBook(document.SchemaVersion!.Value, document.ContentVersion, presence, perVolume, passages, locks));
        }

        private static void Missing(ValidationReport report, string path, string what)
        {
            if (!report.HasErrorAtOrAbove(path)) report.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({what}).");
        }
    }
}
