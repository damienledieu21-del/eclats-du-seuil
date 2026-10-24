using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Items;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Collections;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.Data.Zones
{
    public sealed class SafePoint
    {
        public string Id { get; }
        public string Name { get; }
        public GroundVector Position { get; }

        internal SafePoint(string id, string name, GroundVector position)
        {
            Id = id;
            Name = name;
            Position = position;
        }
    }

    public sealed class Place
    {
        public string Id { get; }
        public string Name { get; }
        public GroundVector Min { get; }
        public GroundVector Max { get; }

        internal Place(string id, string name, GroundVector min, GroundVector max)
        {
            Id = id;
            Name = name;
            Min = min;
            Max = max;
        }

        /// <summary>Le point est dans le lieu (bords compris).</summary>
        public bool Contains(GroundVector point) => point.X >= Min.X && point.X <= Max.X && point.Z >= Min.Z && point.Z <= Max.Z;
    }

    public sealed class Population
    {
        /// <summary>Créature modèle du scénario ; ses vies suivantes s'appellent <c>{Creature}_v2</c>, <c>_v3</c>…</summary>
        public string Creature { get; }

        public AssailantSetup Template { get; }
        public long RespawnMs { get; }
        public Fixed LeashRange { get; }
        public string? Loot { get; }

        internal Population(string creature, AssailantSetup template, long respawnMs, Fixed leashRange, string? loot)
        {
            Creature = creature;
            Template = template;
            RespawnMs = respawnMs;
            LeashRange = leashRange;
            Loot = loot;
        }
    }

    public sealed class Passage
    {
        public string Id { get; }
        public string Name { get; }
        public string Obstacle { get; }

        internal Passage(string id, string name, string obstacle)
        {
            Id = id;
            Name = name;
            Obstacle = obstacle;
        }
    }

    /// <summary>
    /// Sous-lot 12.4 : zone du monde validée. Sa simulation (scénario de zone : espace, créatures de départ), ses points sûrs, ses lieux
    /// nommés, ses populations (réapparition, laisse, butin) et ses passages (obstacles qu'une étape du monde ouvre pour de bon). Valeurs
    /// provisoires de la tranche.
    /// </summary>
    public sealed class ZoneDefinition
    {
        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public string Id { get; }
        public string Name { get; }
        public string ScenarioName { get; }

        /// <summary>Simulation de la zone, tous passages fermés.</summary>
        public CombatSetup Setup { get; }

        public SafePoint Entry { get; }
        public IReadOnlyList<SafePoint> SafePoints { get; }
        public IReadOnlyList<Place> Places { get; }
        public IReadOnlyList<Population> Populations { get; }
        public IReadOnlyList<Passage> Passages { get; }

        internal ZoneDefinition(int schemaVersion, string contentVersion, string id, string name, string scenarioName, CombatSetup setup, SafePoint entry,
            IEnumerable<SafePoint> safePoints, IEnumerable<Place> places, IEnumerable<Population> populations, IEnumerable<Passage> passages)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            Id = id;
            Name = name;
            ScenarioName = scenarioName;
            Setup = setup;
            Entry = entry;
            SafePoints = Frozen.List(safePoints, nameof(safePoints));
            Places = Frozen.List(places, nameof(places));
            Populations = Frozen.List(populations, nameof(populations));
            Passages = Frozen.List(passages, nameof(passages));
        }

        /// <summary>Clé du registre du monde qui marque un passage ouvert (<c>zone.vallons/passage/moulin</c>).</summary>
        public string PassageKey(string passage) => $"{Id}/passage/{passage}";

        public Population? PopulationOf(string creature) => Populations.FirstOrDefault(p => p.Creature == creature);

        /// <summary>Lieux qui contiennent ce point, dans l'ordre du fichier.</summary>
        public IEnumerable<Place> PlacesAt(GroundVector point) => Places.Where(p => p.Contains(point));
    }

    public sealed class ZoneLoadResult
    {
        public ValidationReport Report { get; }
        public ZoneDefinition? Zone { get; }
        public bool IsValid => Zone != null;

        internal ZoneLoadResult(ValidationReport report, ZoneDefinition? zone)
        {
            Report = report;
            Zone = zone;
        }
    }

    /// <summary>
    /// Validateur unique des zones (<c>kind: "zone"</c>, sous-lot 12.4), contre leur scénario (déjà validé : une simulation de zone
    /// positionnée) et le catalogue des objets : identifiants uniques, points sûrs et lieux dans l'aire praticable, populations de créatures
    /// mortelles du scénario (réapparition de 1 s à 1 h, multiple du tick ; laisse de 1 à 100 m ; table de butin connue), passages sur des
    /// obstacles du scénario.
    /// </summary>
    public static class ZoneValidator
    {
        public const string Kind = "zone";

        /// <summary>Version 1 (sous-lot 12.4) : <c>id</c>, <c>name</c>, <c>scenario</c>, <c>entry</c>, <c>safePoints</c>, <c>places</c>, <c>populations</c>, <c>passages</c>.</summary>
        public const int CurrentSchemaVersion = 1;

        public const int MaxSafePoints = 32, MaxPlaces = 64, MaxPopulations = 256, MaxPassages = 32;
        public const long MinRespawnMs = 1000, MaxRespawnMs = 3_600_000;
        public static readonly Fixed MaxLeashRange = Fixed.FromInt(100);

        /// <summary>Longueur maximale d'une créature peuplée (ses vies suivantes y ajoutent « _v&lt;nombre&gt; »).</summary>
        public const int MaxCreatureLength = 60;

        private static readonly Regex LocalId = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);
        private static readonly Regex ScenarioPath = new Regex(@"^[a-z0-9_]+(/[a-z0-9_]+)*\z", RegexOptions.CultureInvariant);

        /// <summary>Suffixe réservé aux vies suivantes d'une créature (<c>_v2</c>…).</summary>
        private static readonly Regex LifeSuffix = new Regex(@"_v[0-9]+\z", RegexOptions.CultureInvariant);

        public static bool IsScenarioPath(string? path) => path != null && path.Length <= 200 && ScenarioPath.IsMatch(path);

        /// <param name="scenario">Simulation de la zone, chargée depuis <c>data.scenario</c> (null si elle n'a pas pu l'être : l'erreur est déjà au rapport).</param>
        public static ZoneLoadResult Validate(ZoneDocument document, CombatSetup? scenario, ItemCatalog items, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (items is null) throw new ArgumentNullException(nameof(items));
            report ??= new ValidationReport();
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, CurrentSchemaVersion, report))
                return new ZoneLoadResult(report, null);
            ZoneData? d = document.Data;
            if (d is null)
            {
                Missing(report, "$.data", "zone");
                return new ZoneLoadResult(report, null);
            }
            if (d.Id is null) Missing(report, "$.data.id", "identifiant « zone.… »");
            else if (!SaveValidator.IsZoneId(d.Id)) report.Add("$.data.id", ValidationCode.InvalidValue, $"identifiant de zone « {d.Id} » invalide : « zone.<nom> » attendu.");
            if (string.IsNullOrWhiteSpace(d.Name)) Missing(report, "$.data.name", "nom affiché");
            if (d.Scenario is null) Missing(report, "$.data.scenario", "scénario de zone, chemin sous scenarios/ sans extension");
            else if (!IsScenarioPath(d.Scenario)) report.Add("$.data.scenario", ValidationCode.InvalidValue, $"chemin de scénario « {d.Scenario} » invalide.");
            else if (scenario != null && (!scenario.Zone || scenario.Space is null))
                report.Add("$.data.scenario", ValidationCode.InvalidValue, "le scénario d'une zone doit être une simulation de zone positionnée (zone: true, bloc space).");
            CombatSpace? space = scenario is { Zone: true } ? scenario.Space : null;

            bool Inside(GroundVector p) => space is null || (p.X >= space.Min.X && p.X <= space.Max.X && p.Z >= space.Min.Z && p.Z <= space.Max.Z);

            var safePoints = new List<SafePoint>();
            if (d.SafePoints is null) Missing(report, "$.data.safePoints", "points sûrs");
            else
            {
                if (d.SafePoints.Count < 1 || d.SafePoints.Count > MaxSafePoints)
                    report.Add("$.data.safePoints", ValidationCode.OutOfRange, $"{d.SafePoints.Count} points sûrs, de 1 à {MaxSafePoints} attendus.");
                for (int i = 0; i < d.SafePoints.Count; i++)
                {
                    string path = $"$.data.safePoints[{i}]";
                    if (d.SafePoints[i] is not SafePointData s) { Missing(report, path, "point sûr"); continue; }
                    bool ok = CheckId(s.Id, path + ".id", safePoints.Select(x => x.Id), report) & CheckName(s.Name, path + ".name", report);
                    if (!CheckPoint(s.Position, path + ".position", "[x, z]", report)) ok = false;
                    else if (!Inside(Point(s.Position!))) { report.Add(path + ".position", ValidationCode.OutOfRange, "point sûr hors de l'aire praticable de la zone."); ok = false; }
                    if (ok) safePoints.Add(new SafePoint(s.Id!, s.Name!, Point(s.Position!)));
                }
            }
            SafePoint? entry = null;
            if (d.Entry is null) Missing(report, "$.data.entry", "point sûr d'entrée");
            else if ((entry = safePoints.FirstOrDefault(p => p.Id == d.Entry)) is null && report.IsValid)
                report.Add("$.data.entry", ValidationCode.UnknownReference, $"point sûr inconnu « {d.Entry} ».");

            var places = new List<Place>();
            if (d.Places is null) Missing(report, "$.data.places", "lieux nommés, éventuellement vide");
            else
            {
                if (d.Places.Count > MaxPlaces) report.Add("$.data.places", ValidationCode.LimitExceeded, $"{d.Places.Count} lieux, au plus {MaxPlaces}.");
                for (int i = 0; i < d.Places.Count; i++)
                {
                    string path = $"$.data.places[{i}]";
                    if (d.Places[i] is not PlaceData p) { Missing(report, path, "lieu"); continue; }
                    bool ok = CheckId(p.Id, path + ".id", places.Select(x => x.Id), report) & CheckName(p.Name, path + ".name", report);
                    if (!CheckPoint(p.Min, path + ".min", "coin sud-ouest [x, z]", report)) ok = false;
                    if (!CheckPoint(p.Max, path + ".max", "coin nord-est [x, z]", report)) ok = false;
                    if (ok)
                    {
                        GroundVector min = Point(p.Min!), max = Point(p.Max!);
                        if (min.X >= max.X || min.Z >= max.Z) { report.Add(path + ".max", ValidationCode.InvalidValue, "coin nord-est attendu au nord-est du coin sud-ouest."); ok = false; }
                        else if (!Inside(min) || !Inside(max)) { report.Add(path, ValidationCode.OutOfRange, "lieu hors de l'aire praticable de la zone."); ok = false; }
                        if (ok) places.Add(new Place(p.Id!, p.Name!, min, max));
                    }
                }
            }

            var populations = new List<Population>();
            if (d.Populations is null) Missing(report, "$.data.populations", "populations, éventuellement vide");
            else
            {
                if (d.Populations.Count > MaxPopulations) report.Add("$.data.populations", ValidationCode.LimitExceeded, $"{d.Populations.Count} populations, au plus {MaxPopulations}.");
                long tick = scenario?.Combat.TickMilliseconds ?? 1;
                var creatures = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < d.Populations.Count; i++)
                {
                    string path = $"$.data.populations[{i}]";
                    if (d.Populations[i] is not PopulationData p) { Missing(report, path, "population"); continue; }
                    bool ok = true;
                    AssailantSetup? template = null;
                    if (p.Creature is null) { Missing(report, path + ".creature", "créature du scénario"); ok = false; }
                    else if (LifeSuffix.IsMatch(p.Creature) || p.Creature.Length > MaxCreatureLength)
                    {
                        report.Add(path + ".creature", ValidationCode.InvalidValue, $"identifiant réservé : « _v<nombre> » final désigne les vies suivantes d'une créature ({MaxCreatureLength} caractères au plus).");
                        ok = false;
                    }
                    else if (!creatures.Add(p.Creature)) { report.Add(path + ".creature", ValidationCode.DuplicateId, $"créature « {p.Creature} » déjà peuplée plus haut."); ok = false; }
                    else if (scenario != null)
                    {
                        template = scenario.Combatants.FirstOrDefault(c => c.Id == p.Creature) as AssailantSetup;
                        if (template is null) { report.Add(path + ".creature", ValidationCode.UnknownReference, $"créature « {p.Creature} » absente du scénario (assaillant attendu)."); ok = false; }
                        else if (template.Health is null || template.Placement is null)
                        {
                            report.Add(path + ".creature", ValidationCode.InvalidValue, "une créature peuplée est mortelle (health) et placée.");
                            ok = false;
                        }
                    }
                    if (p.RespawnMs is null) { Missing(report, path + ".respawnMs", "délai de réapparition en ms"); ok = false; }
                    else if (p.RespawnMs < MinRespawnMs || p.RespawnMs > MaxRespawnMs)
                    { report.Add(path + ".respawnMs", ValidationCode.OutOfRange, $"réapparition hors de [{MinRespawnMs}, {MaxRespawnMs}] ms."); ok = false; }
                    else if (p.RespawnMs % tick != 0)
                    { report.Add(path + ".respawnMs", ValidationCode.InvalidValue, $"{p.RespawnMs} ms n'est pas un multiple de la durée du tick ({tick} ms)."); ok = false; }
                    if (p.LeashRange is null) { Missing(report, path + ".leashRange", "laisse en mètres"); ok = false; }
                    else if (p.LeashRange <= Fixed.Zero || p.LeashRange > MaxLeashRange)
                    { report.Add(path + ".leashRange", ValidationCode.OutOfRange, $"laisse hors de ]0, {MaxLeashRange}] m."); ok = false; }
                    if (p.Loot != null && !items.TryGetLoot(p.Loot, out _))
                    { report.Add(path + ".loot", ValidationCode.UnknownReference, $"table de butin inconnue « {p.Loot} »."); ok = false; }
                    if (ok && template != null) populations.Add(new Population(p.Creature!, template, p.RespawnMs!.Value, p.LeashRange!.Value, p.Loot));
                }
            }

            var passages = new List<Passage>();
            if (d.Passages is null) Missing(report, "$.data.passages", "passages, éventuellement vide");
            else
            {
                if (d.Passages.Count > MaxPassages) report.Add("$.data.passages", ValidationCode.LimitExceeded, $"{d.Passages.Count} passages, au plus {MaxPassages}.");
                for (int i = 0; i < d.Passages.Count; i++)
                {
                    string path = $"$.data.passages[{i}]";
                    if (d.Passages[i] is not PassageData p) { Missing(report, path, "passage"); continue; }
                    bool ok = CheckId(p.Id, path + ".id", passages.Select(x => x.Id), report) & CheckName(p.Name, path + ".name", report);
                    if (p.Obstacle is null) { Missing(report, path + ".obstacle", "obstacle du scénario qui ferme le passage"); ok = false; }
                    else if (passages.Any(x => x.Obstacle == p.Obstacle)) { report.Add(path + ".obstacle", ValidationCode.DuplicateId, $"obstacle « {p.Obstacle} » déjà un passage."); ok = false; }
                    else if (space != null && space.Obstacles.All(o => o.Id != p.Obstacle))
                    { report.Add(path + ".obstacle", ValidationCode.UnknownReference, $"obstacle « {p.Obstacle} » absent de l'espace du scénario."); ok = false; }
                    if (ok) passages.Add(new Passage(p.Id!, p.Name!, p.Obstacle!));
                }
            }

            if (!report.IsValid || scenario is null || entry is null || document.ContentVersion is null) return new ZoneLoadResult(report, null);
            return new ZoneLoadResult(report, new ZoneDefinition(document.SchemaVersion!.Value, document.ContentVersion, d.Id!, d.Name!, d.Scenario!, scenario,
                entry, safePoints, places, populations, passages));
        }

        private static bool CheckId(string? id, string path, IEnumerable<string> previous, ValidationReport report)
        {
            if (id is null) { Missing(report, path, "identifiant"); return false; }
            if (id.Length > 64 || !LocalId.IsMatch(id)) { report.Add(path, ValidationCode.InvalidValue, $"identifiant « {id} » invalide : minuscules, chiffres et « _ », commençant par une lettre."); return false; }
            if (previous.Contains(id)) { report.Add(path, ValidationCode.DuplicateId, $"identifiant « {id} » déjà donné plus haut."); return false; }
            return true;
        }

        private static bool CheckName(string? name, string path, ValidationReport report)
        {
            if (!string.IsNullOrWhiteSpace(name)) return true;
            Missing(report, path, "nom affiché");
            return false;
        }

        /// <summary>Point présent et complet (les deux coordonnées), y compris pour un document construit hors du lecteur JSON.</summary>
        private static bool CheckPoint(PointData? p, string path, string what, ValidationReport report)
        {
            if (p is null || p.X is null || p.Z is null) { Missing(report, path, what); return false; }
            return true;
        }

        private static GroundVector Point(PointData p) => new GroundVector(p.X!.Value, p.Z!.Value);

        private static void Missing(ValidationReport report, string path, string what)
        {
            if (!report.HasErrorAtOrAbove(path)) report.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({what}).");
        }
    }
}
