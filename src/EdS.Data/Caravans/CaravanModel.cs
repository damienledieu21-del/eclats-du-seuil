using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Items;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Collections;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.Data.Caravans
{
    // --- Documents (avant validation) ------------------------------------------------------------------

    /// <summary>Sous-lot V2.41.1 : caravanes et attaques des bandits, telles que lues (<c>kind: "caravans"</c>, <c>data/v0/world/caravans.json</c>).</summary>
    public sealed class CaravanDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
        public CaravanData? Data { get; set; }
    }

    public sealed class CaravanData
    {
        public List<CaravanEntryData?>? Caravans { get; set; }
    }

    public sealed class CaravanEntryData
    {
        public string? Id { get; set; }
        public string? Start { get; set; }
        public int? EveryDays { get; set; }
        public int? FirstDay { get; set; }
        public Fixed? MetersPerMinute { get; set; }
        public int? Wagons { get; set; }
        public int? Guards { get; set; }
        public List<PointData?>? Route { get; set; }
        public WorldEventData? Attack { get; set; }

        /// <summary>Sous-lot V2.41.3 (caravanes en version 2) : quête de reprise au fort après un pillage (facultative).</summary>
        public CaravanRecoveryData? Recovery { get; set; }
    }

    /// <summary>Sous-lot V2.41.3 : quête de reprise telle que lue, avant validation.</summary>
    public sealed class CaravanRecoveryData
    {
        public string? Place { get; set; }
        public int? Days { get; set; }
        public Fixed? GuardRadius { get; set; }
        public List<PointData?>? Chests { get; set; }
        public long? Xp { get; set; }
        public long? Money { get; set; }
    }

    // --- Définitions validées ----------------------------------------------------------------------------

    /// <summary>
    /// Sous-lot V2.41.1 : une caravane de marchands. Elle part de <see cref="Route"/>[0] à <see cref="StartMinute"/> les jours de caravane
    /// (jour <c>d</c> tel que <c>d ≥ FirstDay</c> et <c>d % EveryDays == 0</c>), avance à <see cref="MetersPerMinute"/> mètres par minute du
    /// monde le long de sa route et arrive au dernier point. Son attaque (<see cref="Attack"/>) l'arrête à la halte (<see cref="HaltIndex"/>).
    /// Affichage seulement : la caravane n'est pas un combattant (la position est une fonction pure du temps du monde et du registre).
    /// </summary>
    public sealed class Caravan
    {
        public string Id { get; }
        public int StartMinute { get; }
        public int EveryDays { get; }
        public int FirstDay { get; }
        public Fixed MetersPerMinute { get; }

        /// <summary>Chariots et gardes à montrer (affichage seulement).</summary>
        public int Wagons { get; }
        public int Guards { get; }

        public IReadOnlyList<GroundVector> Route { get; }

        /// <summary>Distance parcourue depuis le départ jusqu'à chaque point de la route (le premier : 0).</summary>
        public IReadOnlyList<Fixed> Along { get; }

        public Fixed Length => Along[Along.Count - 1];

        /// <summary>Point de la route où la caravane s'arrête quand elle est attaquée.</summary>
        public int HaltIndex { get; }

        public Fixed HaltDistance => Along[HaltIndex];

        /// <summary>L'attaque des bandits, événement du monde de sorte <see cref="WorldEventKind.Caravan"/>.</summary>
        public WorldEventDefinition Attack { get; }

        /// <summary>Sous-lot V2.41.3 : quête de reprise au fort après un pillage, ou null (caravanes en version 1).</summary>
        public CaravanRecovery? Recovery { get; }

        internal Caravan(string id, int startMinute, int everyDays, int firstDay, Fixed metersPerMinute, int wagons, int guards,
            IReadOnlyList<GroundVector> route, int haltIndex, WorldEventDefinition attack, CaravanRecovery? recovery = null)
        {
            Id = id;
            StartMinute = startMinute;
            EveryDays = everyDays;
            FirstDay = firstDay;
            MetersPerMinute = metersPerMinute;
            Wagons = wagons;
            Guards = guards;
            Route = Frozen.List(route, nameof(route));
            Along = Frozen.List(CaravanValidator.Cumulative(route), "along");
            HaltIndex = haltIndex;
            Attack = attack;
            Recovery = recovery;
        }

        /// <summary>Le jour <paramref name="day"/> est un jour de caravane.</summary>
        public bool Runs(long day) => day >= FirstDay && day % EveryDays == 0;

        /// <summary>
        /// Point de la route à la distance <paramref name="distance"/> du départ (bornée à la route), et la direction du tronçon (point d'arrivée
        /// moins point de départ du tronçon).
        /// </summary>
        public (GroundVector Point, GroundVector Heading) At(Fixed distance)
        {
            Fixed d = Fixed.Clamp(distance, Fixed.Zero, Length);
            int seg = 0;
            while (seg < Route.Count - 2 && Along[seg + 1] < d) seg++;
            GroundVector a = Route[seg], b = Route[seg + 1];
            Fixed len = checked(Along[seg + 1] - Along[seg]);
            Fixed into = checked(d - Along[seg]);
            Fixed x = a.X + Fixed.FromRaw(checked((b.X - a.X).Raw * into.Raw / len.Raw));
            Fixed z = a.Z + Fixed.FromRaw(checked((b.Z - a.Z).Raw * into.Raw / len.Raw));
            return (new GroundVector(x, z), new GroundVector(b.X - a.X, b.Z - a.Z));
        }
    }

    /// <summary>
    /// Sous-lot V2.41.3 (décision de Damien du 21 novembre 2026) : la quête de reprise au fort après un pillage. Pendant <see cref="Days"/>
    /// jours à partir du jour du pillage (le jour même compris), les coffres de marchandises volés attendent au fort ; le joueur les reprend
    /// un à un, à portée (<see cref="Reach"/>) et quand aucun ennemi n'est debout à moins de <see cref="GuardRadius"/> du coffre (il faut
    /// d'abord battre les bandits qui le gardent). Chaque coffre repris enlève un jour au contrecoup des marchands ; tous repris, il est
    /// annulé et la récompense est donnée.
    /// </summary>
    public sealed class CaravanRecovery
    {
        /// <summary>Portée pour prendre un coffre (écart entre le personnage et le coffre).</summary>
        public static readonly Fixed Reach = Fixed.FromInt(3);

        /// <summary>Lieu de la zone où sont les coffres (le fort des bandits).</summary>
        public string Place { get; }

        /// <summary>Jours où la quête est ouverte, à partir du jour du pillage compris.</summary>
        public int Days { get; }

        /// <summary>Aucun ennemi debout à moins de cette distance d'un coffre pour le prendre.</summary>
        public Fixed GuardRadius { get; }

        /// <summary>Places des coffres (au plus <see cref="CaravanValidator.MaxChests"/>), dans l'ordre du fichier.</summary>
        public IReadOnlyList<GroundVector> Chests { get; }

        /// <summary>Récompense quand tous les coffres sont repris : expérience et cuivre.</summary>
        public long Xp { get; }
        public long Money { get; }

        /// <summary>Masque de tous les coffres repris.</summary>
        public int AllChests => (1 << Chests.Count) - 1;

        internal CaravanRecovery(string place, int days, Fixed guardRadius, IReadOnlyList<GroundVector> chests, long xp, long money)
        {
            Place = place;
            Days = days;
            GuardRadius = guardRadius;
            Chests = Frozen.List(chests, nameof(chests));
            Xp = xp;
            Money = money;
        }
    }

    public sealed class CaravanBook
    {
        public int SchemaVersion { get; }
        public string ContentVersion { get; }

        /// <summary>Zone des routes (celle contre laquelle les caravanes ont été validées).</summary>
        public string ZoneId { get; }

        public IReadOnlyList<Caravan> Caravans { get; }

        internal CaravanBook(int schemaVersion, string contentVersion, string zoneId, IEnumerable<Caravan> caravans)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            ZoneId = zoneId;
            Caravans = Frozen.List(caravans, nameof(caravans));
        }

        public Caravan? Of(string id) => Caravans.FirstOrDefault(c => c.Id == id);

        /// <summary>Caravane dont <paramref name="attackId"/> est l'attaque, ou null.</summary>
        public Caravan? OfAttack(string attackId) => Caravans.FirstOrDefault(c => c.Attack.Id == attackId);

        /// <summary>
        /// La zone <paramref name="zone"/> (celle de la validation), avec les attaques des caravanes parmi ses événements du monde : elles en
        /// prennent toute la mécanique (annonce, bandits, issue, registre, récompense, secteur, contrecoups des marchands).
        /// </summary>
        public ZoneDefinition ApplyTo(ZoneDefinition zone)
        {
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            if (zone.Id != ZoneId) throw new ArgumentException($"Caravanes validées pour la zone {ZoneId}, pas pour {zone.Id}.", nameof(zone));
            if (Caravans.Count == 0 || zone.Events.Any(e => e.Kind == WorldEventKind.Caravan)) return zone;
            return zone.WithCaravanAttacks(Caravans.Select(c => c.Attack));
        }
    }

    public sealed class CaravanLoadResult
    {
        public ValidationReport Report { get; }
        public CaravanBook? Book { get; }
        public bool IsValid => Book != null;

        internal CaravanLoadResult(ValidationReport report, CaravanBook? book)
        {
            Report = report;
            Book = book;
        }
    }

    /// <summary>
    /// Sous-lot V2.41.1 : validateur unique des caravanes (<c>kind: "caravans"</c>, version 1), contre la zone de leurs routes. Une caravane :
    /// <c>id</c> (local, unique), <c>start</c> (« HH:MM »), <c>everyDays</c> (1 à 30), <c>firstDay</c> (0 à 1 000), <c>metersPerMinute</c>
    /// (1 à 120 m par minute du monde), <c>wagons</c> (1 à 4), <c>guards</c> (0 à 8), <c>route</c> (2 à 64 points dans l'aire, deux points de
    /// suite distants d'au moins 1 m), <c>attack</c> (un événement du monde sans sorte, fenêtre ni heure : celles de la caravane ; voir
    /// <see cref="ZoneValidator"/>), dont toutes les variantes ont pour place la même halte, un point de la route autre que le premier et le
    /// dernier. Le voyage entier (attaque comprise) tient en une journée du monde, et la caravane atteint sa halte avant minuit.
    /// </summary>
    public static class CaravanValidator
    {
        public const string Kind = "caravans";

        /// <summary>Version 1 (sous-lot V2.41.1).</summary>
        public const int CurrentSchemaVersion = 2;

        /// <summary>Version 1 (sous-lot V2.41.1), encore lue : sans quête de reprise.</summary>
        public const int Schema1 = 1;

        /// <summary>Sous-lot V2.41.3 : coffres d'une reprise, jours d'ouverture, rayon de garde, récompense, au plus.</summary>
        public const int MaxChests = 8, MaxRecoveryDays = 30;
        public static readonly Fixed MinGuardRadius = Fixed.FromInt(1), MaxGuardRadius = Fixed.FromInt(50);
        public const long MaxRecoveryXp = 1_000_000, MaxRecoveryMoney = 1_000_000;

        public const int MaxCaravans = 4, MinRoute = 2, MaxRoute = 64, MaxWagons = 4, MaxGuards = 8;

        /// <summary>Vitesse d'une caravane, en mètres par minute du monde (une minute du monde : 7,5 s de jeu).</summary>
        public static readonly Fixed MinSpeed = Fixed.FromInt(1), MaxSpeed = Fixed.FromInt(120);

        /// <summary>Distance la plus courte entre deux points de suite de la route.</summary>
        public static readonly Fixed MinSegment = Fixed.FromInt(1);

        private static readonly Regex LocalId = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

        /// <summary>Distances cumulées le long de <paramref name="route"/> (racine entière, arrondie en dessous, au dix-millième de mètre).</summary>
        internal static Fixed[] Cumulative(IReadOnlyList<GroundVector> route)
        {
            var along = new Fixed[route.Count];
            for (int i = 1; i < route.Count; i++)
            {
                long dx = checked(route[i].X.Raw - route[i - 1].X.Raw), dz = checked(route[i].Z.Raw - route[i - 1].Z.Raw);
                along[i] = along[i - 1] + Fixed.FromRaw(IntMath.Sqrt(checked(dx * dx + dz * dz)));
            }
            return along;
        }

        /// <summary>Minutes du monde (entières, arrondies au-dessus) pour parcourir <paramref name="distance"/> à <paramref name="speed"/>.</summary>
        public static long MinutesFor(Fixed distance, Fixed speed) => checked((distance.Raw + speed.Raw - 1) / speed.Raw);

        public static CaravanLoadResult Validate(CaravanDocument document, ZoneDefinition zone, ItemCatalog items, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            if (items is null) throw new ArgumentNullException(nameof(items));
            ValidationReport r = report ?? new ValidationReport();
            int expected = document.SchemaVersion == Schema1 ? Schema1 : CurrentSchemaVersion;   // la version 1 est encore lue
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, expected, r))
                return new CaravanLoadResult(r, null);
            if (document.Data?.Caravans is not List<CaravanEntryData?> data) { Missing(r, "$.data.caravans", "caravanes"); return new CaravanLoadResult(r, null); }
            if (data.Count < 1 || data.Count > MaxCaravans)
            { r.Add("$.data.caravans", ValidationCode.OutOfRange, $"de 1 à {MaxCaravans} caravanes."); return new CaravanLoadResult(r, null); }

            CombatSpace? space = zone.Setup.Space;
            bool Inside(GroundVector p) => space is null || (p.X >= space.Min.X && p.X <= space.Max.X && p.Z >= space.Min.Z && p.Z <= space.Max.Z);
            var caravans = new List<Caravan>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var attackIds = new HashSet<string>(zone.Events.Select(e => e.Id), StringComparer.Ordinal);
            for (int i = 0; i < data.Count; i++)
            {
                string path = $"$.data.caravans[{i}]";
                if (data[i] is not CaravanEntryData c) { Missing(r, path, "caravane"); continue; }
                bool ok = true;
                if (c.Id is null) { Missing(r, path + ".id", "identifiant"); ok = false; }
                else if (c.Id.Length > 40 || !LocalId.IsMatch(c.Id)) { r.Add(path + ".id", ValidationCode.InvalidValue, $"identifiant « {c.Id} » invalide : minuscules, chiffres et « _ », 40 caractères au plus."); ok = false; }
                else if (!ids.Add(c.Id)) { r.Add(path + ".id", ValidationCode.DuplicateId, $"caravane « {c.Id} » déjà donnée."); ok = false; }
                int? Range(int? value, string field, string what, int min, int max)
                {
                    if (value is null) { Missing(r, path + "." + field, what); ok = false; return null; }
                    if (value < min || value > max) { r.Add(path + "." + field, ValidationCode.OutOfRange, $"{what} hors de [{min}, {max}]."); ok = false; return null; }
                    return value;
                }
                int? start = null;
                if (c.Start is null) { Missing(r, path + ".start", "heure de départ « HH:MM »"); ok = false; }
                else if (!(ZoneValidator.ParseClock(c.Start) is int m)) { r.Add(path + ".start", ValidationCode.InvalidValue, $"heure « {c.Start} » mal formée : « HH:MM » de 00:00 à 23:59."); ok = false; }
                else start = m;
                int? every = Range(c.EveryDays, "everyDays", "jours entre deux caravanes", 1, ZoneValidator.MaxEventEveryDays);
                int? first = Range(c.FirstDay, "firstDay", "premier jour du monde", 0, ZoneValidator.MaxEventFirstDay);
                int? wagons = Range(c.Wagons, "wagons", "chariots", 1, MaxWagons);
                int? guards = Range(c.Guards, "guards", "gardes", 0, MaxGuards);
                Fixed speed = Fixed.Zero;
                if (c.MetersPerMinute is null) { Missing(r, path + ".metersPerMinute", "vitesse en mètres par minute du monde"); ok = false; }
                else if (c.MetersPerMinute < MinSpeed || c.MetersPerMinute > MaxSpeed)
                { r.Add(path + ".metersPerMinute", ValidationCode.OutOfRange, $"vitesse hors de [{MinSpeed}, {MaxSpeed}] m par minute du monde."); ok = false; }
                else speed = c.MetersPerMinute.Value;

                var route = new List<GroundVector>();
                if (c.Route is null) { Missing(r, path + ".route", "points de la route [x, z]"); ok = false; }
                else if (c.Route.Count < MinRoute || c.Route.Count > MaxRoute) { r.Add(path + ".route", ValidationCode.OutOfRange, $"{c.Route.Count} points, de {MinRoute} à {MaxRoute} attendus."); ok = false; }
                else
                    for (int j = 0; j < c.Route.Count; j++)
                    {
                        string pp = $"{path}.route[{j}]";
                        if (!(c.Route[j] is PointData pd) || pd.X is null || pd.Z is null) { Missing(r, pp, "point [x, z]"); ok = false; continue; }
                        var p = new GroundVector(pd.X.Value, pd.Z.Value);
                        if (!Inside(p)) { r.Add(pp, ValidationCode.OutOfRange, "point de la route hors de l'aire praticable de la zone."); ok = false; continue; }
                        if (route.Count > 0 && Cumulative(new[] { route[route.Count - 1], p })[1] < MinSegment)
                        { r.Add(pp, ValidationCode.InvalidValue, $"à moins de {MinSegment} m du point précédent."); ok = false; continue; }
                        route.Add(p);
                    }

                if (c.Attack is null) { Missing(r, path + ".attack", "attaque des bandits"); continue; }
                CaravanRecovery? recovery = null;
                if (c.Recovery != null)
                {
                    if (expected == Schema1) { r.Add(path + ".recovery", ValidationCode.UnknownField, $"champ inconnu en version {Schema1} (quête de reprise : version {CurrentSchemaVersion})."); ok = false; }
                    else if (!(CheckRecovery(c.Recovery, zone, path + ".recovery", r) is CaravanRecovery rec)) ok = false;
                    else recovery = rec;
                }
                if (!ok) continue;
                // La halte : la place commune des variantes, un point intérieur de la route.
                WorldEventData a = c.Attack;
                GroundVector? halt = a.Groups?.FirstOrDefault()?.Position is PointData hp && hp.X != null && hp.Z != null ? new GroundVector(hp.X.Value, hp.Z.Value) : (GroundVector?)null;
                int haltIndex = halt is GroundVector h ? route.IndexOf(h) : -1;
                if (haltIndex <= 0 || haltIndex >= route.Count - 1)
                { r.Add(path + ".attack.groups[0].position", ValidationCode.InvalidValue, "halte de la caravane : un point de sa route, ni le premier ni le dernier."); continue; }
                for (int g = 1; g < a.Groups!.Count; g++)
                    if (!(a.Groups[g]?.Position is PointData gp) || gp.X != halt!.Value.X || gp.Z != halt.Value.Z)
                    { r.Add($"{path}.attack.groups[{g}].position", ValidationCode.InvalidValue, "toutes les variantes ont la même halte."); ok = false; }
                Fixed[] along = Cumulative(route);
                long reach = MinutesFor(along[haltIndex], speed);
                long attackStart = checked(start!.Value + reach);
                if (attackStart >= 1440) { r.Add(path + ".start", ValidationCode.OutOfRange, $"la caravane atteint sa halte à la minute {attackStart} du jour : avant minuit attendu."); ok = false; }
                if (checked(MinutesFor(along[along.Length - 1], speed) + (a.DurationMinutes ?? 0)) >= 1440)
                { r.Add(path + ".metersPerMinute", ValidationCode.OutOfRange, "le voyage (attaque comprise) dure une journée du monde ou plus."); ok = false; }
                if (!ok) continue;
                // L'attaque : un événement du monde, avec la fenêtre et l'heure de la caravane.
                a.Kind = WorldEventDefinition.CaravanKindName;
                a.EveryDays = every;
                a.FirstDay = first;
                a.Start = $"{attackStart / 60:00}:{attackStart % 60:00}";
                a.SpreadMinutes = 0;
                a.Caravan = c.Id;
                if (a.Id != null && attackIds.Contains(a.Id)) { r.Add(path + ".attack.id", ValidationCode.DuplicateId, $"événement « {a.Id} » déjà dans la zone ou une autre caravane."); continue; }
                var events = new List<WorldEventDefinition>();
                ZoneValidator.CheckEvents(new List<WorldEventData?> { a }, space, zone.Places.ToList(), zone.Populations.ToList(), items,
                    zone.Setup.Combat.TickMilliseconds, r, events, zone.Setup.Progression.MaxLevel, true, zone.Id, true, path + ".attack");
                if (events.Count != 1) continue;
                if (zone.Region is RegionGrid region && space != null)
                {
                    int before = r.TotalCount;
                    ZoneValidator.CheckEventSector(events[0], 0, region, space, r, path + ".attack");
                    if (r.TotalCount != before) continue;
                }
                attackIds.Add(events[0].Id);
                caravans.Add(new Caravan(c.Id!, start.Value, every!.Value, first!.Value, speed, wagons!.Value, guards!.Value, route, haltIndex, events[0], recovery));
            }
            // Le registre des événements d'un personnage garde aussi les attaques de caravanes.
            int settles = checked(zone.Events.Count(e => e.Settles) + caravans.Count);
            if (settles > SavedEvent.MaxEntries)
                r.Add("$.data.caravans", ValidationCode.LimitExceeded, $"{settles} événements à issue avec les attaques de caravanes, au plus {SavedEvent.MaxEntries} (registre d'un personnage).");
            if (!r.IsValid || caravans.Count != data.Count) return new CaravanLoadResult(r, null);
            return new CaravanLoadResult(r, new CaravanBook(document.SchemaVersion!.Value, document.ContentVersion!, zone.Id, caravans));
        }

        /// <summary>Sous-lot V2.41.3 : la quête de reprise, à chemin précis ; null (erreurs au rapport) si elle est refusée.</summary>
        private static CaravanRecovery? CheckRecovery(CaravanRecoveryData x, ZoneDefinition zone, string path, ValidationReport r)
        {
            bool ok = true;
            Place? place = null;
            if (x.Place is null) { Missing(r, path + ".place", "lieu des coffres"); ok = false; }
            else if (!(zone.Places.FirstOrDefault(p => p.Id == x.Place) is Place found)) { r.Add(path + ".place", ValidationCode.UnknownReference, $"lieu « {x.Place} » absent de la zone."); ok = false; }
            else place = found;
            if (x.Days is null) { Missing(r, path + ".days", "jours d'ouverture"); ok = false; }
            else if (x.Days < 1 || x.Days > MaxRecoveryDays) { r.Add(path + ".days", ValidationCode.OutOfRange, $"jours hors de [1, {MaxRecoveryDays}]."); ok = false; }
            if (x.GuardRadius is null) { Missing(r, path + ".guardRadius", "rayon de garde des coffres"); ok = false; }
            else if (x.GuardRadius < MinGuardRadius || x.GuardRadius > MaxGuardRadius) { r.Add(path + ".guardRadius", ValidationCode.OutOfRange, $"rayon hors de [{MinGuardRadius}, {MaxGuardRadius}] m."); ok = false; }
            if (x.Xp is null) { Missing(r, path + ".xp", "expérience de la récompense"); ok = false; }
            else if (x.Xp < 0 || x.Xp > MaxRecoveryXp) { r.Add(path + ".xp", ValidationCode.OutOfRange, $"expérience hors de [0, {MaxRecoveryXp}]."); ok = false; }
            if (x.Money is null) { Missing(r, path + ".money", "cuivre de la récompense"); ok = false; }
            else if (x.Money < 0 || x.Money > MaxRecoveryMoney) { r.Add(path + ".money", ValidationCode.OutOfRange, $"cuivre hors de [0, {MaxRecoveryMoney}]."); ok = false; }
            var chests = new List<GroundVector>();
            if (x.Chests is null) { Missing(r, path + ".chests", "places des coffres [x, z]"); ok = false; }
            else if (x.Chests.Count < 1 || x.Chests.Count > MaxChests) { r.Add(path + ".chests", ValidationCode.OutOfRange, $"{x.Chests.Count} coffres, de 1 à {MaxChests} attendus."); ok = false; }
            else
                for (int j = 0; j < x.Chests.Count; j++)
                {
                    string pp = $"{path}.chests[{j}]";
                    if (!(x.Chests[j] is PointData pd) || pd.X is null || pd.Z is null) { Missing(r, pp, "place [x, z]"); ok = false; continue; }
                    var p = new GroundVector(pd.X.Value, pd.Z.Value);
                    if (place != null && (p.X < place.Min.X || p.X > place.Max.X || p.Z < place.Min.Z || p.Z > place.Max.Z))
                    { r.Add(pp, ValidationCode.OutOfRange, $"coffre hors du lieu « {place.Id} »."); ok = false; continue; }
                    if (chests.Any(q => Cumulative(new[] { q, p })[1] < CaravanRecovery.Reach))
                    { r.Add(pp, ValidationCode.InvalidValue, $"à moins de {CaravanRecovery.Reach} m d'un autre coffre."); ok = false; continue; }
                    chests.Add(p);
                }
            return ok ? new CaravanRecovery(place!.Id, x.Days!.Value, x.GuardRadius!.Value, chests, x.Xp!.Value, x.Money!.Value) : null;
        }

        private static void Missing(ValidationReport r, string path, string what) => r.Add(path, ValidationCode.MissingField, $"champ obligatoire manquant : {what}.");
    }
}
