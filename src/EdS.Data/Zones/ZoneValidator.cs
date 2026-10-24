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

        /// <summary>Clé du registre du monde de la première victoire sur le chef (<c>zone.vallons/boss/gardien_du_seuil</c>).</summary>
        public string VictoryKey => Boss is { } b ? $"{Id}/boss/{b.Id}" : throw new InvalidOperationException("Zone sans chef.");

        internal ZoneDefinition(int schemaVersion, string contentVersion, string id, string name, string scenarioName, CombatSetup setup, SafePoint entry,
            IEnumerable<SafePoint> safePoints, IEnumerable<Place> places, IEnumerable<Population> populations, IEnumerable<Passage> passages, Boss? boss = null)
        {
            Boss = boss;
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

        /// <summary>
        /// Version 1 (sous-lot 12.4) : <c>id</c>, <c>name</c>, <c>scenario</c>, <c>entry</c>, <c>safePoints</c>, <c>places</c>, <c>populations</c>,
        /// <c>passages</c>. Version 2 (sous-lot 12.7) : <c>boss</c> facultatif (<c>id</c>, <c>scenario</c>, <c>forms</c>, <c>fact</c>, <c>rewards</c>,
        /// <c>loot</c>).
        /// </summary>
        public const int CurrentSchemaVersion = 2;

        public const int MaxSafePoints = 32, MaxPlaces = 64, MaxPopulations = 256, MaxPassages = 32;
        public const long MinRespawnMs = 1000, MaxRespawnMs = 3_600_000;
        public static readonly Fixed MaxLeashRange = Fixed.FromInt(100);

        /// <summary>Longueur maximale d'une créature peuplée (ses vies suivantes y ajoutent « _v&lt;nombre&gt; »).</summary>
        public const int MaxCreatureLength = 60;

        /// <summary>Rayon de l'emprise d'un personnage qui entre par un point sûr (provisoire, celui des scénarios).</summary>
        public static readonly Fixed CharacterRadius = Fixed.Parse("0.5");

        private static readonly Regex LocalId = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);
        private static readonly Regex ScenarioPath = new Regex(@"^[a-z0-9_]+(/[a-z0-9_]+)*\z", RegexOptions.CultureInvariant);

        /// <summary>Suffixe réservé aux vies suivantes d'une créature (<c>_v2</c>…).</summary>
        private static readonly Regex LifeSuffix = new Regex(@"_v[0-9]+\z", RegexOptions.CultureInvariant);

        public static bool IsScenarioPath(string? path) => path != null && path.Length <= 200 && ScenarioPath.IsMatch(path);

        /// <param name="scenario">Simulation de la zone, chargée depuis <c>data.scenario</c> (null si elle n'a pas pu l'être : l'erreur est déjà au rapport).</param>
        /// <param name="bossScenario">Sous-lot 12.7 : scénario des formes du chef, chargé depuis <c>data.boss.scenario</c> (null sans chef, ou s'il n'a
        /// pas pu l'être : l'erreur est déjà au rapport).</param>
        public static ZoneLoadResult Validate(ZoneDocument document, CombatSetup? scenario, ItemCatalog items, ValidationReport? report = null,
            CombatSetup? bossScenario = null)
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

            Boss? boss = d.Boss is { } b ? CheckBoss(b, scenario, bossScenario, items, report, safePoints) : null;
            if (!report.IsValid || scenario is null || entry is null || document.ContentVersion is null) return new ZoneLoadResult(report, null);
            return new ZoneLoadResult(report, new ZoneDefinition(document.SchemaVersion!.Value, document.ContentVersion, d.Id!, d.Name!, d.Scenario!, scenario,
                entry, safePoints, places, populations, passages, boss));
        }

        public const int MaxForms = 8, MaxRewards = 8;
        private static readonly Regex FactId = new Regex(@"^fact(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);

        private static Boss? CheckBoss(BossData b, CombatSetup? scenario, CombatSetup? forms, ItemCatalog items, ValidationReport report,
            IReadOnlyList<SafePoint> safePoints)
        {
            const string path = "$.data.boss";
            bool ok = true;
            if (b.Id is null || b.Id.Length > 64 || !LocalId.IsMatch(b.Id)) { report.Add(path + ".id", ValidationCode.InvalidValue, $"identifiant « {b.Id} » mal formé."); ok = false; }
            if (b.Scenario is null) { Missing(report, path + ".scenario", "scénario des formes du chef"); ok = false; }
            else if (!IsScenarioPath(b.Scenario)) { report.Add(path + ".scenario", ValidationCode.InvalidValue, $"chemin de scénario « {b.Scenario} » invalide."); ok = false; }
            var templates = new List<AssailantSetup>();
            if (b.Forms is null || b.Forms.Count < 1 || b.Forms.Count > MaxForms) { report.Add(path + ".forms", ValidationCode.OutOfRange, $"de 1 à {MaxForms} formes."); ok = false; }
            else if (forms != null)
                for (int i = 0; i < b.Forms.Count; i++)
                {
                    string p = $"{path}.forms[{i}]";
                    string? id = b.Forms[i];
                    if (id is null || forms.Combatants.FirstOrDefault(c => c.Id == id) is not AssailantSetup form || form.Health is null || form.Placement is null)
                    { report.Add(p, ValidationCode.UnknownReference, $"forme « {id} » absente du scénario du chef (assaillant mortel et placé attendu)."); ok = false; }
                    else if (templates.Any(t => t.Id == id)) { report.Add(p, ValidationCode.DuplicateId, $"forme « {id} » déjà donnée."); ok = false; }
                    else if (scenario != null && scenario.Combatants.Any(c => c.Id == id)) { report.Add(p, ValidationCode.DuplicateId, $"« {id} » est déjà une créature de la zone."); ok = false; }
                    else if (LifeSuffix.IsMatch(id) || id.Length > 60) { report.Add(p, ValidationCode.InvalidValue, "identifiant réservé (« _v<nombre> » final) ou trop long."); ok = false; }
                    // La forme arrive dans la simulation de la zone : sa place doit y tenir (aire, obstacles), sinon son arrivée serait refusée.
                    else if (scenario?.Space is { } zoneSpace && Clear(form.Placement.Position, form.Placement.Radius, zoneSpace, $"de la forme « {id} »") is string why)
                    { report.Add(p, ValidationCode.InvalidValue, why); ok = false; }
                    // Revue Codex de la PR #48 : une forme n'occupe pas un point sûr (le personnage y entre ou y revient) ; la première, qui
                    // arrive avec la session, ne chevauche pas non plus une créature de départ de la zone (son arrivée serait refusée).
                    else if (scenario != null && Crowded(form, i == 0, scenario, safePoints) is string crowd)
                    { report.Add(p, ValidationCode.InvalidValue, crowd); ok = false; }
                    // Revue Codex de la PR #48 : la forme arrive dans la simulation de la zone, qui revalide le combat complété : ses références
                    // (cible, déclencheurs…) doivent exister dans la zone ou parmi les formes, pas seulement dans le scénario du chef.
                    else if (scenario != null && ArrivalRefused(scenario, templates, form) is string refused)
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

        private static string? Crowded(AssailantSetup form, bool first, CombatSetup scenario, IReadOnlyList<SafePoint> safePoints)
        {
            CombatantPlacement f = form.Placement!;
            foreach (SafePoint sp in safePoints)
                if (scenario.Combat.AreSolid(true, CharacterRadius, false, f.Radius)
                    && Planar.CirclesOverlap(sp.Position.X.Raw, sp.Position.Z.Raw, CharacterRadius.Raw, f.Position.X.Raw, f.Position.Z.Raw, f.Radius.Raw))
                    return $"la place de la forme « {form.Id} » chevauche le point sûr « {sp.Id} ».";
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
