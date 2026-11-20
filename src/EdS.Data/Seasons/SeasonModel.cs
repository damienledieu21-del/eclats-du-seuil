using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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

        /// <summary>Schéma 2 (V2.44.2) : états de la vigne et vendanges ; obligatoire en version 2, refusé en version 1.</summary>
        public VineData? Vine { get; set; }

        /// <summary>Schéma 2 (V2.44.2) : neige au sol (plaine et col) ; obligatoire en version 2, refusé en version 1.</summary>
        public SnowData? Snow { get; set; }

        /// <summary>Schéma 2 (V2.44.2) : créatures de saison (populations de la zone) ; obligatoire en version 2, refusé en version 1.</summary>
        public List<SeasonCreatureData?>? Creatures { get; set; }
    }

    public sealed class SeasonCreatureData
    {
        public string? Creature { get; set; }
        public List<string?>? Seasons { get; set; }
    }

    /// <summary>Jour de l'année (mois, jour), sans année.</summary>
    public sealed class AnnualDateData
    {
        public int? Month { get; set; }
        public int? Day { get; set; }
    }

    public sealed class VineData
    {
        public AnnualDateData? Green { get; set; }
        public AnnualDateData? Ripe { get; set; }
        public AnnualDateData? Harvested { get; set; }
        public string? Rows { get; set; }
        public List<string?>? Nodes { get; set; }
    }

    public sealed class SnowSpanData
    {
        public AnnualDateData? From { get; set; }
        public AnnualDateData? To { get; set; }
    }

    public sealed class SnowPassData
    {
        public AnnualDateData? From { get; set; }
        public AnnualDateData? To { get; set; }
        public Fixed? Altitude { get; set; }
        public List<string?>? Places { get; set; }
    }

    public sealed class SnowData
    {
        public SnowSpanData? Lowland { get; set; }
        public SnowPassData? Pass { get; set; }
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

    /// <summary>Schéma 2 (V2.44.2) : jour de l'année (mois, jour), valable chaque année (pas de 29 février).</summary>
    public readonly struct AnnualDate : IEquatable<AnnualDate>, IComparable<AnnualDate>
    {
        public int Month { get; }
        public int Day { get; }

        internal AnnualDate(int month, int day)
        {
            Month = month;
            Day = day;
        }

        /// <summary>Le jour existe chaque année (mois 1 à 12, jour dans le mois d'une année ordinaire).</summary>
        public static bool IsValid(int month, int day) => month >= 1 && month <= 12 && day >= 1 && day <= CivilDate.DaysInMonth(2001, month);

        /// <summary>Rang du jour (mois, jour) dans l'année, pour comparer.</summary>
        public static int Ordinal(int month, int day) => month * 32 + day;

        /// <summary>Rang du jour de <paramref name="date"/> dans l'année ; le 29 février compte comme le 28 (même neige, même vigne).</summary>
        public static int OrdinalOf(CivilDate date) => Ordinal(date.Month, date.Month == 2 && date.Day == 29 ? 28 : date.Day);

        internal int Ordinal() => Ordinal(Month, Day);

        public int CompareTo(AnnualDate other) => Ordinal().CompareTo(other.Ordinal());
        public bool Equals(AnnualDate other) => Month == other.Month && Day == other.Day;
        public override bool Equals(object? obj) => obj is AnnualDate d && Equals(d);
        public override int GetHashCode() => Ordinal();
        public override string ToString() => $"{Day:00}/{Month:00}";
    }

    /// <summary>Schéma 2 (V2.44.2) : période de l'année, bornes comprises ; elle peut passer le 1er janvier (du 15 décembre au 28 février).</summary>
    public readonly struct AnnualSpan
    {
        public AnnualDate From { get; }
        public AnnualDate To { get; }

        internal AnnualSpan(AnnualDate from, AnnualDate to)
        {
            From = from;
            To = to;
        }

        public bool Contains(CivilDate date)
        {
            int d = AnnualDate.OrdinalOf(date), f = From.Ordinal(), t = To.Ordinal();
            return f <= t ? d >= f && d <= t : d >= f || d <= t;
        }
    }

    /// <summary>Schéma 2 (V2.44.2, décision de Damien du 20 novembre 2026) : état de la vigne (rangs du château).</summary>
    public enum VineState
    {
        /// <summary>Feuilles vertes, grappes vertes : rien à cueillir.</summary>
        Green = 0,

        /// <summary>Raisin mûr : c'est la saison des vendanges (septembre), les nœuds de la vigne se récoltent.</summary>
        Ripe = 1,

        /// <summary>Vendangé : grappes cueillies, puis vigne nue en hiver, jusqu'au retour du vert.</summary>
        Harvested = 2,
    }

    /// <summary>
    /// Schéma 2 (V2.44.2) : la vigne : verte de <see cref="Green"/> à la veille de <see cref="Ripe"/>, mûre jusqu'à la veille de
    /// <see cref="Harvested"/>, vendangée ensuite jusqu'à la veille de <see cref="Green"/> de l'année suivante. Ses nœuds de récolte
    /// (<see cref="Nodes"/>, dans les métiers) ne se récoltent que vigne mûre. <see cref="Rows"/> : début de l'identifiant des obstacles
    /// des rangs de vigne dans le scénario de la zone (affichage des trois états).
    /// </summary>
    public sealed class SeasonVine
    {
        public AnnualDate Green { get; }
        public AnnualDate Ripe { get; }
        public AnnualDate Harvested { get; }
        public string Rows { get; }
        public IReadOnlyList<string> Nodes { get; }

        internal SeasonVine(AnnualDate green, AnnualDate ripe, AnnualDate harvested, string rows, IEnumerable<string> nodes)
        {
            Green = green;
            Ripe = ripe;
            Harvested = harvested;
            Rows = rows;
            Nodes = Frozen.List(nodes, nameof(nodes));
        }

        public VineState StateOn(CivilDate date)
        {
            int d = AnnualDate.OrdinalOf(date);
            if (d < Green.Ordinal() || d >= Harvested.Ordinal()) return VineState.Harvested;
            return d >= Ripe.Ordinal() ? VineState.Ripe : VineState.Green;
        }
    }

    /// <summary>
    /// Schéma 2 (V2.44.2, décision de Damien du 20 novembre 2026) : neige au sol. En plaine pendant <see cref="Lowland"/> ; au col (lieux
    /// <see cref="PassPlaces"/> de la zone) pendant <see cref="Pass"/>, plus longue. <see cref="PassAltitude"/> (mètres) : la limite de la neige
    /// du col pour l'affichage (relief), le cœur ne connaît que les lieux. Hors des chemins, la neige ralentit la marche (hors combat).
    /// </summary>
    public sealed class SeasonSnow
    {
        public AnnualSpan Lowland { get; }
        public AnnualSpan Pass { get; }
        public Fixed PassAltitude { get; }
        public IReadOnlyList<Place> PassPlaces { get; }

        internal SeasonSnow(AnnualSpan lowland, AnnualSpan pass, Fixed altitude, IEnumerable<Place> places)
        {
            Lowland = lowland;
            Pass = pass;
            PassAltitude = altitude;
            PassPlaces = Frozen.List(places, nameof(places));
        }

        /// <summary>Le sol est enneigé en <paramref name="at"/> le jour <paramref name="date"/> (plaine, ou col dans ses lieux).</summary>
        public bool At(CivilDate date, GroundVector at)
        {
            if (Lowland.Contains(date)) return true;
            if (!Pass.Contains(date)) return false;
            foreach (Place p in PassPlaces)
                if (at.X >= p.Min.X && at.X <= p.Max.X && at.Z >= p.Min.Z && at.Z <= p.Max.Z) return true;
            return false;
        }

        /// <summary>
        /// <paramref name="at"/> est sur un chemin de la zone : à au plus une demi-largeur de l'axe d'une route (<see cref="Road"/>), bord
        /// compris. Calcul exact en entiers (unités brutes de <see cref="Fixed"/>).
        /// </summary>
        public static bool OnRoad(IReadOnlyList<Road> roads, GroundVector at)
        {
            if (roads is null) throw new ArgumentNullException(nameof(roads));
            long px = at.X.Raw, pz = at.Z.Raw;
            foreach (Road road in roads)
            {
                long half = road.Width.Raw / 2;
                for (int i = 0; i + 1 < road.Points.Count; i++)
                    if (WithinSegment(px, pz, road.Points[i], road.Points[i + 1], half)) return true;
                if (road.Points.Count == 1 && WithinSegment(px, pz, road.Points[0], road.Points[0], half)) return true;
            }
            return false;
        }

        /// <summary>
        /// Revue Codex de la PR #229 : <paramref name="px"/>, <paramref name="pz"/> est à au plus <paramref name="half"/> du segment, calcul exact
        /// en entiers vérifiés. Écarts et produits (bornés par les coordonnées de ±4 096 m) tiennent dans un <c>long</c> ; seul le test à la
        /// droite (cross² ≤ half² × len, jusqu'à 10³² environ) passe par <see cref="BigInteger"/>, sans division ni arrondi.
        /// </summary>
        private static bool WithinSegment(long px, long pz, GroundVector a, GroundVector b, long half)
        {
            checked
            {
                long ax = a.X.Raw, az = a.Z.Raw, dx = b.X.Raw - ax, dz = b.Z.Raw - az, wx = px - ax, wz = pz - az;
                long limit = half * half;
                long len = dx * dx + dz * dz, dot = wx * dx + wz * dz;
                if (len == 0 || dot <= 0) return wx * wx + wz * wz <= limit;
                if (dot >= len)
                {
                    long ex = px - b.X.Raw, ez = pz - b.Z.Raw;
                    return ex * ex + ez * ez <= limit;
                }
                // Distance au carré à la droite : cross² / len ≤ half²  ⇔  cross² ≤ half² × len (len > 0).
                long cross = wx * dz - wz * dx;
                return BigInteger.Multiply(cross, cross) <= BigInteger.Multiply(limit, len);
            }
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

        /// <summary>
        /// Relecture indépendante de la PR #229 : zone contre laquelle ce contenu a été validé (lieux du col, routes, créatures) ; la neige ne
        /// s'applique que dans elle.
        /// </summary>
        public string ZoneId { get; }

        /// <summary>Schéma 2 : la vigne ; null en version 1.</summary>
        public SeasonVine? Vine { get; }

        /// <summary>Schéma 2 : la neige au sol ; null en version 1 (jamais de neige).</summary>
        public SeasonSnow? Snow { get; }

        /// <summary>Schéma 2 : saisons des créatures de saison, par population de la zone (vide en version 1).</summary>
        public IReadOnlyDictionary<string, PopulationSeasons> CreatureSeasons { get; }

        /// <summary>
        /// Schéma 2 (V2.44.2, décision de Damien du 20 novembre 2026 : les ours hibernent, plus de loups l'hiver) : la zone
        /// <paramref name="zone"/> (celle contre laquelle ce contenu a été validé) dont les créatures de saison ne sont là qu'en leurs saisons.
        /// </summary>
        /// <exception cref="ArgumentException">Une créature de saison n'est pas une population de cette zone.</exception>
        public ZoneDefinition ApplyTo(ZoneDefinition zone)
        {
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            if (zone.Id != ZoneId) throw new ArgumentException($"Contenu saisonnier validé pour la zone {ZoneId}, pas pour {zone.Id}.", nameof(zone));
            if (CreatureSeasons.Count == 0) return zone;
            foreach (string creature in CreatureSeasons.Keys)
                if (!zone.Populations.Any(p => p.Creature == creature))
                    throw new ArgumentException($"Créature de saison « {creature} » absente des populations de la zone {zone.Id}.", nameof(zone));
            return zone.WithSeasons(CreatureSeasons);
        }
        internal SeasonBook(int schemaVersion, string contentVersion, Fixed[] presence, int maxPerVolume, IEnumerable<SeasonalPassage> passages,
            IEnumerable<SeasonalLock> locks, SeasonVine? vine = null, SeasonSnow? snow = null, IReadOnlyDictionary<string, PopulationSeasons>? creatures = null,
            string zoneId = "")
        {
            ZoneId = zoneId;
            CreatureSeasons = Frozen.Dictionary(creatures ?? new Dictionary<string, PopulationSeasons>(), nameof(creatures));
            Vine = vine;
            Snow = snow;
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

        /// <summary>Schéma 2 : état de la vigne le jour <paramref name="date"/>, ou null (version 1, ou date non fournie).</summary>
        public VineState? VineOn(CivilDate? date) => Vine != null && date is CivilDate d ? Vine.StateOn(d) : (VineState?)null;

        /// <summary>Schéma 2 : <paramref name="node"/> est un nœud de la vigne (récolte seulement vigne mûre).</summary>
        public bool IsVineNode(string node) => Vine != null && Vine.Nodes.Contains(node);

        /// <summary>Schéma 2 : le sol est enneigé en <paramref name="at"/> le jour <paramref name="date"/> (faux sans date ou en version 1).</summary>
        public bool SnowAt(CivilDate? date, GroundVector at) => Snow != null && date is CivilDate d && Snow.At(d, at);

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

        /// <summary>
        /// Version 1 (saisons, 15 novembre 2026) : <c>mistEchoes</c>, <c>passages</c>, <c>locks</c> (les nœuds de saison sont dans les métiers,
        /// version 4). Version 2 (V2.44.2, décisions de Damien du 20 novembre 2026) : <c>vine</c> (états de la vigne, rangs, nœuds des
        /// vendanges) et <c>snow</c> (neige en plaine et au col : dates, lieux, altitude) obligatoires. La version 1 est encore lue.
        /// </summary>
        public const int CurrentSchemaVersion = 2;
        public const int Schema1 = 1;
        public const int MaxPassages = 16, MaxLocks = 16, MinPerVolume = 1, MaxPerVolume = 8, MaxVineNodes = 16, MaxPassPlaces = 8, MaxCreatures = 64;

        /// <summary>Altitude de la limite de la neige au col, au plus (mètres).</summary>
        public static readonly Fixed MaxPassAltitude = Fixed.FromInt(500);

        /// <summary>Taux de présence d'un Écho dans la brume, au plus (borne de sûreté : une chance sur deux à chaque essai).</summary>
        public static readonly Fixed MaxEchoPresence = Fixed.FromRatio(1, 2);

        private static readonly Regex LocalId = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

        public static bool IsLocalId(string? id) => id != null && id.Length <= 64 && LocalId.IsMatch(id);

        public static SeasonLoadResult Validate(SeasonDocument document, ZoneDefinition zone, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            ValidationReport r = report ?? new ValidationReport();
            int expected = document.SchemaVersion == Schema1 ? Schema1 : CurrentSchemaVersion;   // la version 1 est encore lue
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, expected, r))
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

            SeasonVine? vine = null;
            SeasonSnow? snow = null;
            if (expected == Schema1)
            {
                if (d.Vine != null) r.Add("$.data.vine", ValidationCode.InvalidValue, "la vigne vient avec la version 2.");
                if (d.Snow != null) r.Add("$.data.snow", ValidationCode.InvalidValue, "la neige vient avec la version 2.");
                if (d.Creatures != null) r.Add("$.data.creatures", ValidationCode.InvalidValue, "les créatures de saison viennent avec la version 2.");
            }
            Dictionary<string, PopulationSeasons>? creatures = null;
            if (expected != Schema1)
            {
                vine = ValidateVine(d.Vine, zone, r);
                snow = ValidateSnow(d.Snow, zone, r);
                creatures = ValidateCreatures(d.Creatures, zone, r);
            }
            if (!r.IsValid || document.ContentVersion is null) return new SeasonLoadResult(r, null);
            return new SeasonLoadResult(r, new SeasonBook(document.SchemaVersion!.Value, document.ContentVersion, presence, perVolume, passages, locks, vine, snow, creatures, zone.Id));
        }

        private static AnnualDate? Date(AnnualDateData? x, string path, string what, ValidationReport r)
        {
            if (x is null) { Missing(r, path, what); return null; }
            if (x.Month is null || x.Day is null) { Missing(r, path + (x.Month is null ? ".month" : ".day"), what); return null; }
            if (!AnnualDate.IsValid(x.Month.Value, x.Day.Value))
            {
                r.Add(path, ValidationCode.OutOfRange, $"jour {x.Day}/{x.Month} inexistant (mois 1 à 12, jour qui existe chaque année, pas de 29 février).");
                return null;
            }
            return new AnnualDate(x.Month.Value, x.Day.Value);
        }

        private static Dictionary<string, PopulationSeasons>? ValidateCreatures(List<SeasonCreatureData?>? list, ZoneDefinition zone, ValidationReport r)
        {
            const string p = "$.data.creatures";
            if (list is null) { Missing(r, p, "créatures de saison (liste, éventuellement vide)"); return null; }
            if (list.Count > MaxCreatures) { r.Add(p, ValidationCode.LimitExceeded, $"au plus {MaxCreatures} créatures de saison."); return null; }
            var result = new Dictionary<string, PopulationSeasons>(StringComparer.Ordinal);
            bool ok = true;
            for (int i = 0; i < list.Count; i++)
            {
                string cp = $"{p}[{i}]";
                if (list[i] is not SeasonCreatureData c) { Missing(r, cp, "créature de saison"); ok = false; continue; }
                Population? population = c.Creature is null ? null : zone.Populations.FirstOrDefault(x => x.Creature == c.Creature);
                if (c.Creature is null) { Missing(r, cp + ".creature", "population de la zone"); ok = false; }
                else if (population is null) { r.Add(cp + ".creature", ValidationCode.UnknownReference, $"population « {c.Creature} » absente de la zone."); ok = false; }
                else if (population.Night != null) { r.Add(cp + ".creature", ValidationCode.InvalidValue, $"« {c.Creature} » est une créature rare de la nuit : pas de saison."); ok = false; }
                else if (result.ContainsKey(c.Creature)) { r.Add(cp + ".creature", ValidationCode.DuplicateId, $"créature « {c.Creature} » déjà donnée."); ok = false; }
                if (c.Seasons is null) { Missing(r, cp + ".seasons", "saisons (de 1 à 3)"); ok = false; continue; }
                if (c.Seasons.Count < 1 || c.Seasons.Count > 3)
                { r.Add(cp + ".seasons", ValidationCode.OutOfRange, $"{c.Seasons.Count} saisons, de 1 à 3 attendues (toute l'année : la retirer de la liste)."); ok = false; continue; }
                int mask = 0;
                for (int j = 0; j < c.Seasons.Count; j++)
                {
                    Season? s = SeasonCalendar.Parse(c.Seasons[j]);
                    if (s is null) { r.Add($"{cp}.seasons[{j}]", ValidationCode.UnknownReference, $"saison inconnue « {c.Seasons[j]} » ; possibles : spring, summer, autumn, winter."); ok = false; }
                    else if ((mask & (1 << (int)s.Value)) != 0) { r.Add($"{cp}.seasons[{j}]", ValidationCode.DuplicateId, $"saison « {c.Seasons[j]} » déjà donnée."); ok = false; }
                    else mask |= 1 << (int)s.Value;
                }
                if (ok && population != null) result[c.Creature!] = new PopulationSeasons(mask);
            }
            return ok ? result : null;
        }

        private static SeasonVine? ValidateVine(VineData? v, ZoneDefinition zone, ValidationReport r)
        {
            const string p = "$.data.vine";
            if (v is null) { Missing(r, p, "vigne : green, ripe, harvested, rows, nodes"); return null; }
            AnnualDate? green = Date(v.Green, p + ".green", "début du vert", r), ripe = Date(v.Ripe, p + ".ripe", "début des vendanges", r),
                harvested = Date(v.Harvested, p + ".harvested", "fin des vendanges", r);
            bool ok = green != null && ripe != null && harvested != null;
            if (ok && !(green!.Value.CompareTo(ripe!.Value) < 0 && ripe.Value.CompareTo(harvested!.Value) < 0))
            {
                r.Add(p, ValidationCode.InvalidValue, "dans l'année : green avant ripe, ripe avant harvested.");
                ok = false;
            }
            if (v.Rows is null) { Missing(r, p + ".rows", "début de l'identifiant des rangs de vigne"); ok = false; }
            else if (!IsLocalId(v.Rows) || !zone.Setup.Space!.Obstacles.Any(o => o.Id.StartsWith(v.Rows, StringComparison.Ordinal)))
            { r.Add(p + ".rows", ValidationCode.UnknownReference, $"aucun obstacle de la zone ne commence par « {v.Rows} »."); ok = false; }
            var nodes = new List<string>();
            if (v.Nodes is null) { Missing(r, p + ".nodes", "nœuds de récolte de la vigne"); ok = false; }
            else if (v.Nodes.Count < 1 || v.Nodes.Count > MaxVineNodes) { r.Add(p + ".nodes", ValidationCode.OutOfRange, $"de 1 à {MaxVineNodes} nœuds."); ok = false; }
            else
                for (int i = 0; i < v.Nodes.Count; i++)
                {
                    string? n = v.Nodes[i];
                    if (!IsLocalId(n)) { r.Add($"{p}.nodes[{i}]", ValidationCode.InvalidValue, $"identifiant « {n} » mal formé."); ok = false; }
                    else if (nodes.Contains(n!)) { r.Add($"{p}.nodes[{i}]", ValidationCode.DuplicateId, $"nœud « {n} » en double."); ok = false; }
                    else nodes.Add(n!);
                }
            return ok ? new SeasonVine(green!.Value, ripe!.Value, harvested!.Value, v.Rows!, nodes) : null;
        }

        private static SeasonSnow? ValidateSnow(SnowData? s, ZoneDefinition zone, ValidationReport r)
        {
            const string p = "$.data.snow";
            if (s is null) { Missing(r, p, "neige : lowland, pass"); return null; }
            bool ok = true;
            AnnualDate? lf = null, lt = null, pf = null, pt = null;
            if (s.Lowland is null) { Missing(r, p + ".lowland", "neige en plaine : from, to"); ok = false; }
            else
            {
                lf = Date(s.Lowland.From, p + ".lowland.from", "début", r);
                lt = Date(s.Lowland.To, p + ".lowland.to", "fin", r);
                ok &= lf != null && lt != null;
            }
            Fixed altitude = Fixed.Zero;
            var places = new List<Place>();
            if (s.Pass is null) { Missing(r, p + ".pass", "neige au col : from, to, altitude, places"); ok = false; }
            else
            {
                SnowPassData x = s.Pass;
                pf = Date(x.From, p + ".pass.from", "début", r);
                pt = Date(x.To, p + ".pass.to", "fin", r);
                ok &= pf != null && pt != null;
                if (x.Altitude is null) { Missing(r, p + ".pass.altitude", "altitude de la limite de la neige (m)"); ok = false; }
                else if (x.Altitude.Value < Fixed.Zero || x.Altitude.Value > MaxPassAltitude)
                { r.Add(p + ".pass.altitude", ValidationCode.OutOfRange, $"altitude hors de [0, {MaxPassAltitude}] m."); ok = false; }
                else altitude = x.Altitude.Value;
                if (x.Places is null) { Missing(r, p + ".pass.places", "lieux du col"); ok = false; }
                else if (x.Places.Count < 1 || x.Places.Count > MaxPassPlaces) { r.Add(p + ".pass.places", ValidationCode.OutOfRange, $"de 1 à {MaxPassPlaces} lieux."); ok = false; }
                else
                    for (int i = 0; i < x.Places.Count; i++)
                    {
                        Place? place = zone.Places.FirstOrDefault(q => q.Id == x.Places[i]);
                        if (place is null) { r.Add($"{p}.pass.places[{i}]", ValidationCode.UnknownReference, $"lieu « {x.Places[i]} » inconnu de la zone."); ok = false; }
                        else if (places.Contains(place)) { r.Add($"{p}.pass.places[{i}]", ValidationCode.DuplicateId, $"lieu « {x.Places[i]} » en double."); ok = false; }
                        else places.Add(place);
                    }
            }
            if (!ok) return null;
            var lowland = new AnnualSpan(lf!.Value, lt!.Value);
            var pass = new AnnualSpan(pf!.Value, pt!.Value);
            // Le col garde sa neige plus longtemps : chaque jour de neige en plaine en est un au col (année bissextile parcourue).
            for (int m = 1; m <= 12; m++)
                for (int day = 1; day <= CivilDate.DaysInMonth(2000, m); day++)
                {
                    var date = new CivilDate(2000, m, day);
                    if (lowland.Contains(date) && !pass.Contains(date))
                    {
                        r.Add(p + ".pass", ValidationCode.InvalidValue, $"le col doit être enneigé chaque jour où la plaine l'est (le {date.Day}/{date.Month} ne l'est pas).");
                        return null;
                    }
                }
            return new SeasonSnow(lowland, pass, altitude, places);
        }

        private static void Missing(ValidationReport report, string path, string what)
        {
            if (!report.HasErrorAtOrAbove(path)) report.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({what}).");
        }
    }
}
