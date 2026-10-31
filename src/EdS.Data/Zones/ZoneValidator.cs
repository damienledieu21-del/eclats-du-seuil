using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Items;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain;
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

        /// <summary>Sous-lot V1.8 (schéma 7, zones en mélange) : niveau minimum du lieu (1 par défaut).</summary>
        public int MinLevel { get; }

        internal Place(string id, string name, GroundVector min, GroundVector max, int minLevel = 1)
        {
            Id = id;
            Name = name;
            Min = min;
            Max = max;
            MinLevel = minLevel;
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

        /// <summary>Sous-lot V1.6 : famille de la créature (objectifs <c>defeatFamily</c>).</summary>
        public string Family { get; }

        /// <summary>Famille par défaut : l'identifiant sans ses chiffres finaux (« loup12 » donne « loup »).</summary>
        public static string DefaultFamily(string creature) => creature.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');

        /// <summary>
        /// Réponses de Damien du 31 octobre 2026 (schéma 6) : créature rare de la nuit, ou null. Absente au début d'une session, elle apparaît
        /// après un signe, certaines nuits du monde (tirage par la clé de la nuit), ou quand une quête ou un contrat en cours la demande.
        /// </summary>
        public NightSpawn? Night { get; }

        internal Population(string creature, AssailantSetup template, long respawnMs, Fixed leashRange, string? loot, string? family = null, NightSpawn? night = null)
        {
            Night = night;
            Family = family ?? DefaultFamily(creature);
            Creature = creature;
            Template = template;
            RespawnMs = respawnMs;
            LeashRange = leashRange;
            Loot = loot;
        }
    }

    /// <summary>Réponses de Damien du 31 octobre 2026 (schéma 6) : apparition d'une créature rare de la nuit.</summary>
    public sealed class NightSpawn
    {
        /// <summary>Chance d'apparaître une nuit donnée, en pour cent (1 à 100).</summary>
        public int ChancePercent { get; }

        /// <summary>Délai entre le signe et l'arrivée, en ms (multiple du tick).</summary>
        public long SignMs { get; }

        /// <summary>Clé du texte du signe.</summary>
        public string Sign { get; }

        internal NightSpawn(int chancePercent, long signMs, string sign)
        {
            ChancePercent = chancePercent;
            SignMs = signMs;
            Sign = sign;
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

    /// <summary>Vallons agrandis (schéma 3) : genre d'une route.</summary>
    public enum RoadKind
    {
        /// <summary>Chemin de terre battue (« terre »).</summary>
        Dirt = 0,

        /// <summary>Rue pavée (« pave »).</summary>
        Cobble = 1,
    }

    /// <summary>
    /// Vallons agrandis (schéma 3, demande de Damien du 28 octobre 2026) : une route ou un chemin de la zone, ligne brisée de largeur
    /// constante dans l'aire praticable. Affichage seulement : le moteur ne la connaît pas (on y marche comme ailleurs).
    /// </summary>
    public sealed class Road
    {
        public string Id { get; }
        public RoadKind Kind { get; }
        public Fixed Width { get; }
        public IReadOnlyList<GroundVector> Points { get; }

        internal Road(string id, RoadKind kind, Fixed width, IEnumerable<GroundVector> points)
        {
            Id = id;
            Kind = kind;
            Width = width;
            Points = Frozen.List(points, nameof(points));
        }
    }

    /// <summary>
    /// Intérieurs (schéma 4, choix de Damien du 28 octobre 2026 : « on fait la façon 1 ») : une porte vers une autre zone. Le personnage s'y
    /// tient pour passer (<see cref="Position"/>, à portée) et y arrive en venant de la porte <see cref="ToDoor"/> de la zone <see cref="To"/>,
    /// qui ramène à celle-ci.
    /// </summary>
    public sealed class Door
    {
        public string Id { get; }
        public string Name { get; }
        public GroundVector Position { get; }
        public string To { get; }
        public string ToDoor { get; }

        /// <summary>Bâtiment (obstacle du scénario) dont c'est la porte, ou null.</summary>
        public string? Building { get; }

        internal Door(string id, string name, GroundVector position, string to, string toDoor, string? building)
        {
            Id = id;
            Name = name;
            Position = position;
            To = to;
            ToDoor = toDoor;
            Building = building;
        }
    }

    /// <summary>
    /// Sous-lot 12.7 : chef de la zone. Ses formes se succèdent (quand une forme tombe, la suivante se relève à sa place) ; la première
    /// victoire sur la dernière forme donne le fait et les récompenses, une seule fois (registre du monde, clé <see cref="VictoryKey"/>) ;
    /// chaque victoire donne le butin.
    /// </summary>
    public sealed class Boss
    {
        public string Id { get; }
        public IReadOnlyList<AssailantSetup> Forms { get; }
        public string Fact { get; }
        public IReadOnlyList<(string Item, int Count)> Rewards { get; }
        public string? Loot { get; }

        internal Boss(string id, IEnumerable<AssailantSetup> forms, string fact, IEnumerable<(string, int)> rewards, string? loot)
        {
            Id = id;
            Forms = Frozen.List(forms, nameof(forms));
            Fact = fact;
            Rewards = Frozen.List(rewards, nameof(rewards));
            Loot = loot;
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

        /// <summary>Sous-lot 12.7 : chef de la zone, ou null.</summary>
        public Boss? Boss { get; }

        /// <summary>Sous-lot V1.6 (schéma 5) : autres chefs de la zone (le chef des bandits), dans l'ordre du fichier.</summary>
        public IReadOnlyList<Boss> Chiefs { get; }

        /// <summary>Sous-lot V1.6 : le chef, puis les autres chefs.</summary>
        public IReadOnlyList<Boss> Bosses { get; }

        /// <summary>Sous-lot V1.6 : clé du registre du monde de la première victoire sur le chef <paramref name="boss"/>.</summary>
        public string VictoryKeyOf(Boss boss) => $"{Id}/boss/{(boss ?? throw new ArgumentNullException(nameof(boss))).Id}";

        /// <summary>Vallons agrandis (schéma 3) : routes et chemins, dans l'ordre du fichier (vide sans routes).</summary>
        public IReadOnlyList<Road> Roads { get; }

        /// <summary>Intérieurs (schéma 4) : la zone est un intérieur (une pièce, sans ciel). Affichage seulement.</summary>
        public bool Indoor { get; }

        /// <summary>Intérieurs (schéma 4) : portes vers d'autres zones, dans l'ordre du fichier (vide sans porte).</summary>
        public IReadOnlyList<Door> Doors { get; }

        /// <summary>Porte <paramref name="id"/> de la zone, ou null.</summary>
        public Door? DoorOf(string id) => Doors.FirstOrDefault(d => d.Id == id);

        /// <summary>Clé du registre du monde de la première victoire sur le chef (<c>zone.vallons/boss/gardien_du_seuil</c>).</summary>
        public string VictoryKey => Boss is { } b ? $"{Id}/boss/{b.Id}" : throw new InvalidOperationException("Zone sans chef.");

        internal ZoneDefinition(int schemaVersion, string contentVersion, string id, string name, string scenarioName, CombatSetup setup, SafePoint entry,
            IEnumerable<SafePoint> safePoints, IEnumerable<Place> places, IEnumerable<Population> populations, IEnumerable<Passage> passages, Boss? boss = null,
            IEnumerable<Road>? roads = null, bool indoor = false, IEnumerable<Door>? doors = null, IEnumerable<Boss>? chiefs = null,
            ZoneScaling? scaling = null)
        {
            Scaling = scaling;
            Boss = boss;
            Chiefs = Frozen.List(chiefs ?? Array.Empty<Boss>(), nameof(chiefs));
            Bosses = Frozen.List((boss is null ? Array.Empty<Boss>() : new[] { boss }).Concat(Chiefs), nameof(boss));
            Roads = Frozen.List(roads ?? Array.Empty<Road>(), nameof(roads));
            Indoor = indoor;
            Doors = Frozen.List(doors ?? Array.Empty<Door>(), nameof(doors));
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

        /// <summary>
        /// Sous-lot V1.1 : clé du registre du monde qui marque un point sûr découvert (<c>zone.vallons/safe/place_du_bourg</c>), destination
        /// du voyage rapide.
        /// </summary>
        public string SafePointKey(string safePoint) => $"{Id}/safe/{safePoint}";

        public Population? PopulationOf(string creature) => Populations.FirstOrDefault(p => p.Creature == creature);

        /// <summary>Lieux qui contiennent ce point, dans l'ordre du fichier.</summary>
        public IEnumerable<Place> PlacesAt(GroundVector point) => Places.Where(p => p.Contains(point));

        /// <summary>Sous-lot V1.8 (schéma 7) : zones en mélange, ou null (les créatures gardent leurs chiffres).</summary>
        public ZoneScaling? Scaling { get; }

        /// <summary>
        /// Sous-lot V1.8 : niveau minimum en ce point : le plus haut des lieux qui le contiennent (un lieu dangereux dans un lieu calme
        /// l'emporte), 1 hors de tout lieu.
        /// </summary>
        public int MinLevelAt(GroundVector point)
        {
            int level = 1;
            foreach (Place p in Places) if (p.Contains(point) && p.MinLevel > level) level = p.MinLevel;
            return level;
        }

        /// <summary>
        /// Sous-lot V1.8 (sujet 4, zones en mélange) : la zone telle qu'un personnage de niveau <paramref name="playerLevel"/> la trouve. Chaque
        /// créature (populations, créatures du scénario, formes des chefs) monte au niveau <see cref="ZoneScaling.CreatureLevel"/> ; ses PV et
        /// ses dégâts grandissent de <see cref="ZoneScaling.Scale(AssailantSetup, int, int)"/>. Sans <see cref="Scaling"/>, la zone telle quelle.
        /// Rien n'est modifié : une nouvelle définition est rendue.
        /// </summary>
        public ZoneDefinition ForLevel(int playerLevel)
        {
            if (playerLevel < 1) throw new ArgumentOutOfRangeException(nameof(playerLevel));
            if (Scaling is not ZoneScaling s) return this;
            AssailantSetup One(AssailantSetup a, bool boss) =>
                a.Placement is CombatantPlacement at
                    ? s.Scale(a, boss ? s.BossBaseLevel : s.BaseLevel, s.CreatureLevel(MinLevelAt(at.Position), playerLevel))
                    : a;
            var bossForms = new HashSet<string>(Bosses.SelectMany(b => b.Forms).Select(f => f.Id), StringComparer.Ordinal);
            var scaled = new Dictionary<string, AssailantSetup>(StringComparer.Ordinal);
            foreach (CombatantSetup c in Setup.Combatants)
                if (c is AssailantSetup a) scaled[a.Id] = One(a, bossForms.Contains(a.Id));
            CombatSetup setup = new CombatSetup(Setup.Seed, Setup.DurationMs, Setup.Stats, Setup.Damage, Setup.Progression, Setup.Combat,
                Setup.Combatants.Select(c => c is AssailantSetup a ? scaled[a.Id] : c).ToList(), Setup.Commands, Setup.Data, Setup.Space, Setup.Zone);
            var populations = Populations.Select(p => new Population(p.Creature, scaled.TryGetValue(p.Template.Id, out AssailantSetup t) ? t : One(p.Template, false),
                p.RespawnMs, p.LeashRange, p.Loot, p.Family, p.Night)).ToList();
            Boss? ScaleBoss(Boss? b) => b is null ? null : new Boss(b.Id, b.Forms.Select(f => One(f, true)), b.Fact, b.Rewards, b.Loot);
            return new ZoneDefinition(SchemaVersion, ContentVersion, Id, Name, ScenarioName, setup, Entry, SafePoints, Places, populations, Passages,
                ScaleBoss(Boss), Roads, Indoor, Doors, Chiefs.Select(c => ScaleBoss(c)!).ToList(), Scaling);
        }
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

        /// <summary>
        /// Version 1 (sous-lot 12.4) : <c>id</c>, <c>name</c>, <c>scenario</c>, <c>entry</c>, <c>safePoints</c>, <c>places</c>, <c>populations</c>,
        /// <c>passages</c>. Version 2 (sous-lot 12.7) : <c>boss</c> facultatif (<c>id</c>, <c>scenario</c>, <c>forms</c>, <c>fact</c>, <c>rewards</c>,
        /// <c>loot</c>). Version 3 (Vallons agrandis, 28 octobre 2026) : <c>roads</c> facultatif (<c>id</c>, <c>kind</c> « terre » ou « pave »,
        /// <c>width</c> de 1 à 20 m, <c>points</c> : de 2 à 32 points dans l'aire praticable). Version 4 (intérieurs, 28 octobre 2026) :
        /// <c>indoor</c> facultatif (booléen) et <c>doors</c> facultatif (<c>id</c>, <c>name</c>, <c>position</c>, <c>to</c>, <c>toDoor</c>,
        /// <c>building</c> facultatif). Version 5 (sous-lot V1.6) : <c>chiefs</c> facultatif (autres chefs à formes, même forme que
        /// <c>boss</c>, au plus <see cref="MaxChiefs"/>) et <c>family</c> facultatif sur une population (objectifs <c>defeatFamily</c>). Version 6
        /// (réponses de Damien du 31 octobre 2026) : <c>night</c> facultatif sur une population (créature rare de la nuit : <c>chancePercent</c>,
        /// <c>signMs</c>, <c>sign</c>). Version 7 (sous-lot V1.8, zones en mélange) : <c>minLevel</c> facultatif sur un lieu (1 par défaut) et
        /// <c>scaling</c> facultatif (<c>baseLevel</c>, <c>bossBaseLevel</c>, <c>healthPerLevelPercent</c>, <c>damagePerLevelPercent</c>,
        /// <c>easeLevels</c>).
        /// </summary>
        public const int CurrentSchemaVersion = 7;

        /// <summary>Schéma 6 : délai au plus entre le signe d'une créature de la nuit et son arrivée (10 min de jeu).</summary>
        public const long MaxSignMs = 600_000;

        /// <summary>Sous-lot V1.6 : autres chefs d'une zone, au plus.</summary>
        public const int MaxChiefs = 4;

        public const int MaxSafePoints = 32, MaxPlaces = 64, MaxPopulations = 256, MaxPassages = 32, MaxRoads = 32, MaxRoadPoints = 32;

        /// <summary>Intérieurs : portes d'une zone, au plus ; zones reliées par des portes, au plus (<see cref="CheckDoors"/>).</summary>
        public const int MaxDoors = 16, MaxLinkedZones = 64;

        /// <summary>Intérieurs : écart au plus entre une porte et la boîte de son bâtiment (provisoire).</summary>
        public static readonly Fixed MaxDoorGap = Fixed.FromInt(2);
        public static readonly Fixed MinRoadWidth = Fixed.One, MaxRoadWidth = Fixed.FromInt(20);
        public const long MinRespawnMs = 1000, MaxRespawnMs = 3_600_000;
        public static readonly Fixed MaxLeashRange = Fixed.FromInt(100);

        /// <summary>Longueur maximale d'une créature peuplée (ses vies suivantes y ajoutent « _v&lt;nombre&gt; »).</summary>
        public const int MaxCreatureLength = 60;

        /// <summary>Rayon de l'emprise d'un personnage qui entre par un point sûr (provisoire, celui des scénarios).</summary>
        public static readonly Fixed CharacterRadius = Fixed.Parse("0.5");

        private static readonly Regex LocalId = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);
        private static readonly Regex ScenarioPath = new Regex(@"^[a-z0-9_]+(/[a-z0-9_]+)*\z", RegexOptions.CultureInvariant);

        /// <summary>Schéma 6 : clé de texte du signe d'une créature de la nuit (« text.… »).</summary>
        private static readonly Regex TextKey = new Regex(@"^text(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);

        /// <summary>Suffixe réservé aux vies suivantes d'une créature (<c>_v2</c>…).</summary>
        private static readonly Regex LifeSuffix = new Regex(@"_v[0-9]+\z", RegexOptions.CultureInvariant);

        public static bool IsScenarioPath(string? path) => path != null && path.Length <= 200 && ScenarioPath.IsMatch(path);

        /// <param name="scenario">Simulation de la zone, chargée depuis <c>data.scenario</c> (null si elle n'a pas pu l'être : l'erreur est déjà au rapport).</param>
        /// <param name="bossScenario">Sous-lot 12.7 : scénario des formes du chef, chargé depuis <c>data.boss.scenario</c> (null sans chef, ou s'il n'a
        /// pas pu l'être : l'erreur est déjà au rapport).</param>
        /// <param name="chiefScenarios">Sous-lot V1.6 : scénario des formes de chaque autre chef (<c>data.chiefs</c>), dans l'ordre (null pour un
        /// scénario qui n'a pas pu être chargé).</param>
        public static ZoneLoadResult Validate(ZoneDocument document, CombatSetup? scenario, ItemCatalog items, ValidationReport? report = null,
            CombatSetup? bossScenario = null, IReadOnlyList<CombatSetup?>? chiefScenarios = null)
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
                    else if (scenario is { Zone: true } && space != null && Footprint(Point(s.Position!), scenario, space) is string why)
                    { report.Add(path + ".position", ValidationCode.InvalidValue, why); ok = false; }
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
                        // Sous-lot V1.8 (schéma 7) : niveau minimum, de 1 au niveau maximal des règles.
                        int maxLevel = scenario?.Progression.MaxLevel ?? EdS.Domain.Builds.ProgressionRules.MaxLevelLimit;
                        if (p.MinLevel is int ml && (ml < 1 || ml > maxLevel))
                        {
                            report.Add(path + ".minLevel", ValidationCode.OutOfRange, $"niveau minimum hors de [1, {maxLevel}].");
                            ok = false;
                        }
                        if (ok) places.Add(new Place(p.Id!, p.Name!, min, max, p.MinLevel ?? 1));
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
                    if (p.Family != null && (p.Family.Length > 64 || !LocalId.IsMatch(p.Family)))
                    { report.Add(path + ".family", ValidationCode.InvalidValue, $"famille « {p.Family} » mal formée."); ok = false; }
                    // Réponses de Damien du 31 octobre 2026 (schéma 6) : créature rare de la nuit.
                    NightSpawn? night = null;
                    if (p.Night is NightData n)
                    {
                        string np = path + ".night";
                        bool nok = true;
                        if (n.ChancePercent is null) { Missing(report, np + ".chancePercent", "chance par nuit, en pour cent"); nok = false; }
                        else if (n.ChancePercent < 1 || n.ChancePercent > 100) { report.Add(np + ".chancePercent", ValidationCode.OutOfRange, "chance hors de [1, 100] %."); nok = false; }
                        if (n.SignMs is null) { Missing(report, np + ".signMs", "délai entre le signe et l'arrivée, en ms"); nok = false; }
                        else if (n.SignMs < 0 || n.SignMs > MaxSignMs) { report.Add(np + ".signMs", ValidationCode.OutOfRange, $"délai hors de [0, {MaxSignMs}] ms."); nok = false; }
                        else if (n.SignMs % tick != 0) { report.Add(np + ".signMs", ValidationCode.InvalidValue, $"{n.SignMs} ms n'est pas un multiple de la durée du tick ({tick} ms)."); nok = false; }
                        if (n.Sign is null) { Missing(report, np + ".sign", "clé du texte du signe"); nok = false; }
                        else if (n.Sign.Length > 200 || !TextKey.IsMatch(n.Sign)) { report.Add(np + ".sign", ValidationCode.InvalidValue, $"clé de texte « {n.Sign} » mal formée : « text.… »."); nok = false; }
                        if (nok) night = new NightSpawn((int)n.ChancePercent!.Value, n.SignMs!.Value, n.Sign!);
                        else ok = false;
                    }
                    if (ok && template != null) populations.Add(new Population(p.Creature!, template, p.RespawnMs!.Value, p.LeashRange!.Value, p.Loot, p.Family, night));
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

            var roads = new List<Road>();
            if (d.Roads != null)
            {
                if (d.Roads.Count > MaxRoads) report.Add("$.data.roads", ValidationCode.LimitExceeded, $"{d.Roads.Count} routes, au plus {MaxRoads}.");
                for (int i = 0; i < d.Roads.Count; i++)
                {
                    string path = $"$.data.roads[{i}]";
                    if (d.Roads[i] is not RoadData r) { Missing(report, path, "route"); continue; }
                    bool ok = CheckId(r.Id, path + ".id", roads.Select(x => x.Id), report);
                    RoadKind kind = RoadKind.Dirt;
                    if (r.Kind is null) { Missing(report, path + ".kind", "genre : « terre » ou « pave »"); ok = false; }
                    else if (r.Kind == "terre") kind = RoadKind.Dirt;
                    else if (r.Kind == "pave") kind = RoadKind.Cobble;
                    else { report.Add(path + ".kind", ValidationCode.InvalidValue, $"genre de route « {r.Kind} » inconnu : « terre » ou « pave »."); ok = false; }
                    if (r.Width is null) { Missing(report, path + ".width", "largeur en mètres"); ok = false; }
                    else if (r.Width < MinRoadWidth || r.Width > MaxRoadWidth)
                    { report.Add(path + ".width", ValidationCode.OutOfRange, $"largeur hors de [{MinRoadWidth}, {MaxRoadWidth}] m."); ok = false; }
                    var points = new List<GroundVector>();
                    if (r.Points is null) { Missing(report, path + ".points", "points de la route [x, z]"); ok = false; }
                    else if (r.Points.Count < 2 || r.Points.Count > MaxRoadPoints)
                    { report.Add(path + ".points", ValidationCode.OutOfRange, $"{r.Points.Count} points, de 2 à {MaxRoadPoints} attendus."); ok = false; }
                    else
                        for (int j = 0; j < r.Points.Count; j++)
                        {
                            string pp = $"{path}.points[{j}]";
                            if (!CheckPoint(r.Points[j], pp, "[x, z]", report)) { ok = false; continue; }
                            GroundVector point = Point(r.Points[j]!);
                            if (!Inside(point)) { report.Add(pp, ValidationCode.OutOfRange, "point de route hors de l'aire praticable de la zone."); ok = false; }
                            else if (points.Count > 0 && points[points.Count - 1] == point) { report.Add(pp, ValidationCode.InvalidValue, "deux points de suite confondus."); ok = false; }
                            else points.Add(point);
                        }
                    if (ok) roads.Add(new Road(r.Id!, kind, r.Width!.Value, points));
                }
            }

            var doors = new List<Door>();
            if (d.Doors != null)
            {
                if (d.Doors.Count > MaxDoors) report.Add("$.data.doors", ValidationCode.LimitExceeded, $"{d.Doors.Count} portes, au plus {MaxDoors}.");
                for (int i = 0; i < d.Doors.Count; i++)
                {
                    string path = $"$.data.doors[{i}]";
                    if (d.Doors[i] is not DoorData door) { Missing(report, path, "porte"); continue; }
                    bool ok = CheckId(door.Id, path + ".id", doors.Select(x => x.Id), report) & CheckName(door.Name, path + ".name", report);
                    bool placed = false;
                    if (!CheckPoint(door.Position, path + ".position", "place de la porte [x, z]", report)) ok = false;
                    else if (!Inside(Point(door.Position!))) { report.Add(path + ".position", ValidationCode.OutOfRange, "porte hors de l'aire praticable de la zone."); ok = false; }
                    // On arrive par la porte comme par un point sûr : l'emprise d'un personnage doit y tenir.
                    else if (scenario is { Zone: true } && space != null && Footprint(Point(door.Position!), scenario, space) is string why)
                    { report.Add(path + ".position", ValidationCode.InvalidValue, why); ok = false; }
                    else placed = true;
                    if (door.To is null) { Missing(report, path + ".to", "zone où mène la porte"); ok = false; }
                    else if (!SaveValidator.IsZoneId(door.To)) { report.Add(path + ".to", ValidationCode.InvalidValue, $"identifiant de zone « {door.To} » invalide : « zone.<nom> » attendu."); ok = false; }
                    else if (door.To == d.Id) { report.Add(path + ".to", ValidationCode.InvalidValue, "une porte mène à une autre zone."); ok = false; }
                    if (door.ToDoor is null) { Missing(report, path + ".toDoor", "porte d'arrivée dans l'autre zone"); ok = false; }
                    else if (door.ToDoor.Length > 64 || !LocalId.IsMatch(door.ToDoor)) { report.Add(path + ".toDoor", ValidationCode.InvalidValue, $"identifiant de porte « {door.ToDoor} » invalide."); ok = false; }
                    if (door.Building != null)
                    {
                        SpaceObstacle? building = space?.Obstacles.FirstOrDefault(o => o.Id == door.Building);
                        if (space != null && building is null) { report.Add(path + ".building", ValidationCode.UnknownReference, $"obstacle « {door.Building} » absent de l'espace du scénario."); ok = false; }
                        // Relecture indépendante : l'écart n'est mesuré que pour une porte posée dans l'aire (une place hors de l'aire est déjà refusée).
                        else if (building != null && placed && Gap(Point(door.Position!), building) > MaxDoorGap)
                        { report.Add(path + ".position", ValidationCode.OutOfRange, $"porte à plus de {MaxDoorGap} m de son bâtiment « {door.Building} »."); ok = false; }
                    }
                    if (ok) doors.Add(new Door(door.Id!, door.Name!, Point(door.Position!), door.To!, door.ToDoor!, door.Building));
                }
            }

            // Relecture indépendante : on arrive aussi par une porte ; une forme du chef n'occupe pas plus sa place que celle d'un point sûr.
            // Sous-lot V1.6 : toutes les formes de tous les chefs comptent ensemble (plafond des combattants, identités, places de départ).
            int allForms = (d.Boss?.Forms?.Count ?? 0) + (d.Chiefs?.Sum(c => c?.Forms?.Count ?? 0) ?? 0);
            var admitted = new List<Boss>();
            Boss? boss = d.Boss is { } b
                ? CheckBoss(b, "$.data.boss", scenario, bossScenario, items, report, safePoints, doors, allForms, admitted)
                : null;
            if (boss != null) admitted.Add(boss);
            var chiefs = new List<Boss>();
            if (d.Chiefs != null)
            {
                if (d.Chiefs.Count > MaxChiefs) report.Add("$.data.chiefs", ValidationCode.LimitExceeded, $"{d.Chiefs.Count} chefs, au plus {MaxChiefs}.");
                for (int i = 0; i < d.Chiefs.Count; i++)
                {
                    string path = $"$.data.chiefs[{i}]";
                    if (d.Chiefs[i] is not BossData c) { Missing(report, path, "chef"); continue; }
                    if (admitted.Any(x => x.Id == c.Id)) { report.Add(path + ".id", ValidationCode.DuplicateId, $"chef « {c.Id} » déjà donné."); continue; }
                    CombatSetup? forms = chiefScenarios != null && i < chiefScenarios.Count ? chiefScenarios[i] : null;
                    if (CheckBoss(c, path, scenario, forms, items, report, safePoints, doors, allForms, admitted) is Boss chief)
                    {
                        chiefs.Add(chief);
                        admitted.Add(chief);
                    }
                }
            }
            if (!report.IsValid || scenario is null || entry is null || document.ContentVersion is null) return new ZoneLoadResult(report, null);
            ZoneScaling? scaling = d.Scaling is null ? null : CheckScaling(d.Scaling, scenario.Progression.MaxLevel, report);
            if (!report.IsValid) return new ZoneLoadResult(report, null);
            return new ZoneLoadResult(report, new ZoneDefinition(document.SchemaVersion!.Value, document.ContentVersion, d.Id!, d.Name!, d.Scenario!, scenario,
                entry, safePoints, places, populations, passages, boss, roads, d.Indoor ?? false, doors, chiefs, scaling));
        }

        /// <summary>Écart (m) entre un point et la boîte d'un obstacle (0 dedans ou sur le bord).</summary>
        private static Fixed Gap(GroundVector p, SpaceObstacle o)
        {
            Fixed minX = o.Points.Min(q => q.X), maxX = o.Points.Max(q => q.X), minZ = o.Points.Min(q => q.Z), maxZ = o.Points.Max(q => q.Z);
            long dx = Math.Max(0, Math.Max(checked(minX.Raw - p.X.Raw), checked(p.X.Raw - maxX.Raw)));
            long dz = Math.Max(0, Math.Max(checked(minZ.Raw - p.Z.Raw), checked(p.Z.Raw - maxZ.Raw)));
            // Plus loin que l'écart admis sur un axe : inutile d'élever au carré (aucun dépassement possible ensuite).
            if (dx > MaxDoorGap.Raw || dz > MaxDoorGap.Raw) return Fixed.FromRaw(Math.Max(dx, dz));
            // Écart réel arrondi par excès au dix-millième : comparé à MaxDoorGap sans racine en virgule flottante.
            long d2 = checked(dx * dx + dz * dz);
            long root = (long)Math.Sqrt(d2);
            while (checked(root * root) < d2) root++;
            while (root > 0 && checked((root - 1) * (root - 1)) >= d2) root--;
            return Fixed.FromRaw(root);
        }

        /// <summary>
        /// Intérieurs : les portes des zones reliées se répondent. Chaque porte mène à une zone de <paramref name="zones"/>, à une porte qui y
        /// existe et qui ramène à elle (même zone, même porte) ; au plus <see cref="MaxLinkedZones"/> zones. Rend le rapport (valide s'il est vide).
        /// </summary>
        public static ValidationReport CheckDoors(IReadOnlyList<ZoneDefinition> zones, ValidationReport? report = null)
        {
            if (zones is null) throw new ArgumentNullException(nameof(zones));
            report ??= new ValidationReport();
            if (zones.Count > MaxLinkedZones) report.Add("$", ValidationCode.LimitExceeded, $"{zones.Count} zones reliées, au plus {MaxLinkedZones}.");
            var byId = new Dictionary<string, ZoneDefinition>(StringComparer.Ordinal);
            foreach (ZoneDefinition z in zones)
                if (!byId.ContainsKey(z.Id)) byId.Add(z.Id, z);
                else report.Add("$", ValidationCode.DuplicateId, $"zone « {z.Id} » donnée deux fois.");
            foreach (ZoneDefinition z in zones) CheckDoorsOf(z, byId, report);
            return report;
        }

        /// <summary>
        /// Portes de la seule zone <paramref name="zone"/> parmi <paramref name="zones"/> (même contrôle que <see cref="CheckDoors"/>) : les
        /// chemins des erreurs sont ceux du fichier de cette zone.
        /// </summary>
        public static ValidationReport CheckDoorsOf(ZoneDefinition zone, IReadOnlyList<ZoneDefinition> zones, ValidationReport? report = null)
        {
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            if (zones is null) throw new ArgumentNullException(nameof(zones));
            var byId = new Dictionary<string, ZoneDefinition>(StringComparer.Ordinal);
            foreach (ZoneDefinition z in zones)
                if (!byId.ContainsKey(z.Id)) byId.Add(z.Id, z);
            return CheckDoorsOf(zone, byId, report ?? new ValidationReport());
        }

        private static ValidationReport CheckDoorsOf(ZoneDefinition z, IReadOnlyDictionary<string, ZoneDefinition> byId, ValidationReport report)
        {
            for (int i = 0; i < z.Doors.Count; i++)
            {
                Door door = z.Doors[i];
                string path = $"$.data.doors[{i}]";
                if (!byId.TryGetValue(door.To, out ZoneDefinition? target))
                    report.Add(path + ".to", ValidationCode.UnknownReference, $"{z.Id}, porte « {door.Id} » : zone « {door.To} » inconnue.");
                else if (target.DoorOf(door.ToDoor) is not Door back)
                    report.Add(path + ".toDoor", ValidationCode.UnknownReference, $"{z.Id}, porte « {door.Id} » : porte « {door.ToDoor} » absente de {target.Id}.");
                else if (back.To != z.Id || back.ToDoor != door.Id)
                    report.Add(path + ".toDoor", ValidationCode.InvalidValue,
                        $"{z.Id}, porte « {door.Id} » : la porte « {back.Id} » de {target.Id} mène à « {back.To} », porte « {back.ToDoor} », et non ici.");
            }
            return report;
        }

        public const int MaxForms = 8, MaxRewards = 8;
        private static readonly Regex FactId = new Regex(@"^fact(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);

        private static Boss? CheckBoss(BossData b, string path, CombatSetup? scenario, CombatSetup? forms, ItemCatalog items, ValidationReport report,
            IReadOnlyList<SafePoint> safePoints, IReadOnlyList<Door> doors, int allForms, IReadOnlyList<Boss> others)
        {
            bool ok = true;
            if (b.Id is null || b.Id.Length > 64 || !LocalId.IsMatch(b.Id)) { report.Add(path + ".id", ValidationCode.InvalidValue, $"identifiant « {b.Id} » mal formé."); ok = false; }
            if (b.Scenario is null) { Missing(report, path + ".scenario", "scénario des formes du chef"); ok = false; }
            else if (!IsScenarioPath(b.Scenario)) { report.Add(path + ".scenario", ValidationCode.InvalidValue, $"chemin de scénario « {b.Scenario} » invalide."); ok = false; }
            var templates = new List<AssailantSetup>();
            if (b.Forms is null || b.Forms.Count < 1 || b.Forms.Count > MaxForms) { report.Add(path + ".forms", ValidationCode.OutOfRange, $"de 1 à {MaxForms} formes."); ok = false; }
            // Revue Codex de la PR #48 (3e tour) : la session compte les créatures de la zone, le personnage qui entre et toutes les formes.
            else if (scenario != null && checked(scenario.Combatants.Count + 1 + allForms) > scenario.Combat.MaxCombatants)
            {
                report.Add(path + ".forms", ValidationCode.LimitExceeded,
                    $"{scenario.Combatants.Count} créatures, le personnage et {allForms} formes de chefs : plus que les {scenario.Combat.MaxCombatants} combattants d'une session.");
                ok = false;
            }
            else if (forms != null)
                for (int i = 0; i < b.Forms.Count; i++)
                {
                    string p = $"{path}.forms[{i}]";
                    string? id = b.Forms[i];
                    if (id is null || forms.Combatants.FirstOrDefault(c => c.Id == id) is not AssailantSetup form || form.Health is null || form.Placement is null)
                    { report.Add(p, ValidationCode.UnknownReference, $"forme « {id} » absente du scénario du chef (assaillant mortel et placé attendu)."); ok = false; }
                    else if (templates.Any(t => t.Id == id) || others.Any(o => o.Forms.Any(f => f.Id == id))) { report.Add(p, ValidationCode.DuplicateId, $"forme « {id} » déjà donnée."); ok = false; }
                    // Sous-lot V1.6 : la première forme de chaque chef arrive avec la session et y reste tant qu'on ne la combat pas. Relecture de la
                    // PR #76 : aucune forme d'un chef ne se pose sur la première forme d'un chef déjà admis (son arrivée serait refusée) ; la
                    // première forme ne se pose sur aucune forme d'un chef déjà admis.
                    else if (others.FirstOrDefault(o => (i == 0 ? o.Forms : o.Forms.Take(1)).Any(f => Overlap(f.Placement!, form.Placement))) is Boss near)
                    { report.Add(p, ValidationCode.InvalidValue, $"la place de la forme « {id} » chevauche celle d'une forme du chef « {near.Id} »."); ok = false; }
                    else if (scenario != null && scenario.Combatants.Any(c => c.Id == id)) { report.Add(p, ValidationCode.DuplicateId, $"« {id} » est déjà une créature de la zone."); ok = false; }
                    else if (LifeSuffix.IsMatch(id) || id.Length > 60) { report.Add(p, ValidationCode.InvalidValue, "identifiant réservé (« _v<nombre> » final) ou trop long."); ok = false; }
                    // La forme arrive dans la simulation de la zone : sa place doit y tenir (aire, obstacles), sinon son arrivée serait refusée.
                    else if (scenario?.Space is { } zoneSpace && Clear(form.Placement.Position, form.Placement.Radius, zoneSpace, $"de la forme « {id} »") is string why)
                    { report.Add(p, ValidationCode.InvalidValue, why); ok = false; }
                    // Revue Codex de la PR #48 : une forme n'occupe pas un point sûr (le personnage y entre ou y revient) ; la première, qui
                    // arrive avec la session, ne chevauche pas non plus une créature de départ de la zone (son arrivée serait refusée).
                    else if (scenario != null && Crowded(form, i == 0, scenario, safePoints, doors) is string crowd)
                    { report.Add(p, ValidationCode.InvalidValue, crowd); ok = false; }
                    // Revue Codex de la PR #48 : la forme arrive dans la simulation de la zone, qui revalide le combat complété : ses références
                    // (cible, déclencheurs…) doivent exister dans la zone ou parmi les formes, pas seulement dans le scénario du chef.
                    // Relecture de la PR #76 : avec les formes de tous les chefs déjà admis.
                    else if (scenario != null && ArrivalRefused(scenario, others.SelectMany(o => o.Forms).Concat(templates), form) is string refused)
                    { report.Add(p, ValidationCode.InvalidValue, $"forme refusée par la simulation de la zone : {refused}"); ok = false; }
                    else templates.Add(form);
                }
            if (b.Fact is null || b.Fact.Length > 200 || !FactId.IsMatch(b.Fact)) { report.Add(path + ".fact", ValidationCode.InvalidValue, $"fait « {b.Fact} » mal formé : « fact.… »."); ok = false; }
            var rewards = new List<(string, int)>();
            if (b.Rewards is null) { Missing(report, path + ".rewards", "récompenses de la première victoire, éventuellement vide"); ok = false; }
            else if (b.Rewards.Count > MaxRewards) { report.Add(path + ".rewards", ValidationCode.LimitExceeded, $"au plus {MaxRewards} récompenses."); ok = false; }
            else
                for (int i = 0; i < b.Rewards.Count; i++)
                {
                    string p = $"{path}.rewards[{i}]";
                    if (b.Rewards[i]?.Item is not string item || !items.TryGetItem(item, out ItemDefinition def)) { report.Add(p + ".item", ValidationCode.UnknownReference, $"objet inconnu « {b.Rewards[i]?.Item} »."); ok = false; }
                    else if (b.Rewards[i]!.Count is not int count || count < 1 || count > def.Stack) { report.Add(p + ".count", ValidationCode.OutOfRange, $"quantité hors de [1, {def.Stack}]."); ok = false; }
                    else rewards.Add((item, count));
                }
            if (b.Loot != null && !items.TryGetLoot(b.Loot, out _)) { report.Add(path + ".loot", ValidationCode.UnknownReference, $"table de butin inconnue « {b.Loot} »."); ok = false; }
            return ok && forms != null ? new Boss(b.Id!, templates, b.Fact!, rewards, b.Loot) : null;
        }

        private static bool Overlap(CombatantPlacement a, CombatantPlacement b) =>
            Planar.CirclesOverlap(a.Position.X.Raw, a.Position.Z.Raw, a.Radius.Raw, b.Position.X.Raw, b.Position.Z.Raw, b.Radius.Raw);

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

        /// <summary>
        /// Revue Codex de la PR #45 : emprise d'un personnage (<see cref="CharacterRadius"/>) posée sur un point sûr, comme la simulation la
        /// placera : entière dans l'aire praticable, hors de tout obstacle (passages fermés compris), sans chevaucher un combattant de départ
        /// qui lui est solide (son arrivée serait refusée). Null si elle tient, sinon le motif.
        /// </summary>
        private static string? Footprint(GroundVector p, CombatSetup scenario, CombatSpace space)
        {
            Fixed r = CharacterRadius;
            if (Clear(p, r, space, "d'un personnage") is string why) return why;
            foreach (CombatantSetup c in scenario.Combatants)
            {
                if (c.ArrivalMs != null || c.Placement is not CombatantPlacement q) continue;
                if (scenario.Combat.AreSolid(true, r, c is CharacterSetup, q.Radius)
                    && Planar.CirclesOverlap(p.X.Raw, p.Z.Raw, r.Raw, q.Position.X.Raw, q.Position.Z.Raw, q.Radius.Raw))
                    return $"l'emprise d'un personnage ({r} m) chevauche la place de « {c.Id} ».";
            }
            return null;
        }

        /// <summary>Premier motif de refus du combat de la zone complété par les formes déjà admises et celle-ci (arrivées), ou null.</summary>
        private static string? ArrivalRefused(CombatSetup scenario, IEnumerable<AssailantSetup> earlier, AssailantSetup form)
        {
            static AssailantSetup Arriving(AssailantSetup t) =>
                new AssailantSetup(t.Id, t.Team, t.Armor, t.MagicResistance, t.Target, t.FirstAttackMs, t.IntervalMs, t.Attacks, t.Initiative, t.StatusResistance,
                    t.Triggers, t.Health, t.Placement, 0, t.Behavior);
            IReadOnlyList<RuleViolation> violations = scenario.Completed(earlier.Append(form).Select(Arriving), Array.Empty<CombatCommand>()).Validate();
            return violations.Count == 0 ? null : $"{string.Join(", ", violations[0].Fields)} : {violations[0].Message}";
        }

        private static string? Crowded(AssailantSetup form, bool first, CombatSetup scenario, IReadOnlyList<SafePoint> safePoints, IReadOnlyList<Door> doors)
        {
            CombatantPlacement f = form.Placement!;
            foreach (SafePoint sp in safePoints)
                if (scenario.Combat.AreSolid(true, CharacterRadius, false, f.Radius)
                    && Planar.CirclesOverlap(sp.Position.X.Raw, sp.Position.Z.Raw, CharacterRadius.Raw, f.Position.X.Raw, f.Position.Z.Raw, f.Radius.Raw))
                    return $"la place de la forme « {form.Id} » chevauche le point sûr « {sp.Id} ».";
            // Relecture indépendante : on arrive aussi par une porte (intérieurs).
            foreach (Door door in doors)
                if (scenario.Combat.AreSolid(true, CharacterRadius, false, f.Radius)
                    && Planar.CirclesOverlap(door.Position.X.Raw, door.Position.Z.Raw, CharacterRadius.Raw, f.Position.X.Raw, f.Position.Z.Raw, f.Radius.Raw))
                    return $"la place de la forme « {form.Id} » chevauche la porte « {door.Id} ».";
            if (first)
                foreach (CombatantSetup c in scenario.Combatants)
                {
                    if (c.ArrivalMs != null || c.Placement is not CombatantPlacement q) continue;
                    if (scenario.Combat.AreSolid(c is CharacterSetup, q.Radius, false, f.Radius)
                        && Planar.CirclesOverlap(q.Position.X.Raw, q.Position.Z.Raw, q.Radius.Raw, f.Position.X.Raw, f.Position.Z.Raw, f.Radius.Raw))
                        return $"la place de la première forme « {form.Id} » chevauche celle de « {c.Id} », présente au début de la session.";
                }
            return null;
        }

        /// <summary>Emprise (centre, rayon) entière dans l'aire praticable et hors de tout obstacle (passages fermés compris) ; null si elle tient.</summary>
        private static string? Clear(GroundVector p, Fixed r, CombatSpace space, string who)
        {
            if (p.X - r < space.Min.X || p.X + r > space.Max.X || p.Z - r < space.Min.Z || p.Z + r > space.Max.Z)
                return $"l'emprise {who} ({r} m) déborde de l'aire praticable de la zone.";
            foreach (SpaceObstacle o in space.Obstacles)
                if (o.Points.Count >= 3 && ConvexPolygon.IsStrictlyConvex(o.Points, out _) && new ConvexPolygon(o.Points).OverlapsCircle(p.X.Raw, p.Z.Raw, r.Raw))
                    return $"l'emprise {who} ({r} m) chevauche l'obstacle « {o.Id} ».";
            return null;
        }

        /// <summary>Point présent et complet (les deux coordonnées), y compris pour un document construit hors du lecteur JSON.</summary>
        /// <summary>Sous-lot V1.8 (schéma 7) : zones en mélange (niveaux de 1 au niveau maximal des règles, pour cent et facilité bornés).</summary>
        private static ZoneScaling? CheckScaling(ZoneScalingData d, int maxLevel, ValidationReport report)
        {
            const string path = "$.data.scaling";
            int? Read(int? value, string field, string what, int min, int max)
            {
                if (value is null) { Missing(report, $"{path}.{field}", what); return null; }
                if (value < min || value > max) { report.Add($"{path}.{field}", ValidationCode.OutOfRange, $"{what} hors de [{min}, {max}]."); return null; }
                return value;
            }
            int? baseLevel = Read(d.BaseLevel, "baseLevel", "niveau des chiffres des créatures", 1, maxLevel);
            int? bossLevel = Read(d.BossBaseLevel, "bossBaseLevel", "niveau des chiffres des chefs", 1, maxLevel);
            int? health = Read(d.HealthPerLevelPercent, "healthPerLevelPercent", "PV en plus par niveau (pour cent)", 0, ZoneScaling.MaxPercentPerLevel);
            int? damage = Read(d.DamagePerLevelPercent, "damagePerLevelPercent", "dégâts en plus par niveau (pour cent)", 0, ZoneScaling.MaxPercentPerLevel);
            int? ease = Read(d.EaseLevels, "easeLevels", "niveaux sous le joueur", 0, ZoneScaling.MaxEaseLevels);
            return baseLevel is int b && bossLevel is int bb && health is int h && damage is int dm && ease is int e ? new ZoneScaling(b, bb, h, dm, e) : null;
        }

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
