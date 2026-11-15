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

namespace EdS.Data.WorldBosses
{
    // --- Documents (avant validation) ------------------------------------------------------------------

    /// <summary>Grand Solitaire : boss du monde tels que lus (<c>kind: "worldBosses"</c>, <c>data/v0/world/worldbosses.json</c>).</summary>
    public sealed class WorldBossDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
        public WorldBossesData? Data { get; set; }
    }

    public sealed class WorldBossesData
    {
        public List<WorldBossData?>? Bosses { get; set; }
    }

    public sealed class WorldBossData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public Fixed? Radius { get; set; }
        public long? HealthBase { get; set; }
        public long? HealthPerPlayer { get; set; }
        public int? MinPlayers { get; set; }
        public int? MaxPlayers { get; set; }
        public int? WindowMinutes { get; set; }
        public int? ChancePercent { get; set; }
        public long? SignsMs { get; set; }
        public long? LimitMs { get; set; }
        public long? AnnounceMs { get; set; }
        public long? ObjectiveMs { get; set; }
        public Fixed? WalkSpeed { get; set; }
        public Fixed? Clearance { get; set; }
        public List<WorldBossPhaseData?>? Phases { get; set; }
        public List<WorldBossAttackData?>? Attacks { get; set; }
        public List<WorldBossSiteData?>? Sites { get; set; }
        public List<WorldBossTargetData?>? Targets { get; set; }
        public int? CompanionsInCombat { get; set; }
        public PointData? Fallback { get; set; }
        public WorldBossLootData? Loot { get; set; }
    }

    public sealed class WorldBossPhaseData
    {
        public string? Id { get; set; }
        public int? FromPercent { get; set; }
        public List<string?>? Attacks { get; set; }
    }

    public sealed class WorldBossAttackData
    {
        public string? Id { get; set; }
        public string? Shape { get; set; }
        public Fixed? Range { get; set; }
        public int? Angle { get; set; }
        public Fixed? Width { get; set; }
        public Fixed? Radius { get; set; }
        public Fixed? Inner { get; set; }
        public Fixed? Outer { get; set; }
        public int? CountMin { get; set; }
        public int? CountMax { get; set; }
        public long? StaggerMs { get; set; }
        public string? Anchor { get; set; }
        public long? AnnounceMs { get; set; }
        public long? LockMs { get; set; }
        public long? RecoveryMs { get; set; }
    }

    public sealed class WorldBossSiteData
    {
        public string? Id { get; set; }
        public string? Kind { get; set; }
        public PointData? Position { get; set; }
    }

    public sealed class WorldBossTargetData
    {
        public string? Id { get; set; }
        public string? Place { get; set; }
        public List<PointData?>? Route { get; set; }
        public List<WorldBossObjectiveData?>? Objectives { get; set; }
    }

    public sealed class WorldBossObjectiveData
    {
        public string? Id { get; set; }
        public PointData? Position { get; set; }
    }

    public sealed class WorldBossLootData
    {
        public string? Trophy { get; set; }
        public string? Appearance { get; set; }
        public string? Mount { get; set; }
        public int? MountChanceBasisPoints { get; set; }
    }

    // --- Modèle validé ---------------------------------------------------------------------------------

    /// <summary>Grand Solitaire : forme au sol d'une attaque annoncée (dossier, p. 24 et 25).</summary>
    public enum WorldBossShape
    {
        /// <summary>Secteur frontal (balayage des cornes) : rayon <c>range</c>, ouverture <c>angle</c>.</summary>
        Cone = 0,

        /// <summary>Couloir droit devant (charge) : longueur <c>range</c>, largeur <c>width</c>, bout arrondi.</summary>
        Line = 1,

        /// <summary>Anneau autour du boss (anneau du Seuil) : de <c>inner</c> à <c>outer</c> ; le centre est sûr.</summary>
        Ring = 2,

        /// <summary>Disques de rayon <c>radius</c>, de <c>countMin</c> à <c>countMax</c> selon le nombre de joueurs, décalés de <c>staggerMs</c>.</summary>
        Circles = 3,
    }

    /// <summary>Grand Solitaire : où tombent les disques d'une attaque <see cref="WorldBossShape.Circles"/>.</summary>
    public enum CircleAnchor
    {
        /// <summary>Devant le boss, sous les sabots avant (double piétinement), répartis de part et d'autre de son axe.</summary>
        Front = 0,

        /// <summary>Sur des joueurs choisis (éclats retombants), une marque par cible, sans en poser sous chaque joueur.</summary>
        Targets = 1,
    }

    public sealed class WorldBossAttack
    {
        public string Id { get; }
        public WorldBossShape Shape { get; }
        public Fixed Range { get; }
        public int Angle { get; }
        public Fixed Width { get; }
        public Fixed Radius { get; }
        public Fixed Inner { get; }
        public Fixed Outer { get; }
        public int CountMin { get; }
        public int CountMax { get; }
        public long StaggerMs { get; }
        public CircleAnchor Anchor { get; }

        /// <summary>Annonce au sol avant l'impact (ms) ; la même de 5 à 100 joueurs.</summary>
        public long AnnounceMs { get; }

        /// <summary>Verrouillage avant l'impact (ms) : l'axe ou la position de la marque est figé.</summary>
        public long LockMs { get; }

        /// <summary>Reprise d'appui après l'impact (ms) : fenêtre de riposte.</summary>
        public long RecoveryMs { get; }

        internal WorldBossAttack(string id, WorldBossShape shape, Fixed range, int angle, Fixed width, Fixed radius, Fixed inner, Fixed outer, int countMin,
            int countMax, long staggerMs, CircleAnchor anchor, long announceMs, long lockMs, long recoveryMs)
        {
            Anchor = anchor;
            Id = id;
            Shape = shape;
            Range = range;
            Angle = angle;
            Width = width;
            Radius = radius;
            Inner = inner;
            Outer = outer;
            CountMin = countMin;
            CountMax = countMax;
            StaggerMs = staggerMs;
            AnnounceMs = announceMs;
            LockMs = lockMs;
            RecoveryMs = recoveryMs;
        }
    }

    public sealed class WorldBossPhase
    {
        public string Id { get; }

        /// <summary>Pour cent de vie où la phase commence (100 pour la première).</summary>
        public int FromPercent { get; }

        public IReadOnlyList<string> Attacks { get; }

        internal WorldBossPhase(string id, int fromPercent, IEnumerable<string> attacks)
        {
            Id = id;
            FromPercent = fromPercent;
            Attacks = Frozen.List(attacks, nameof(attacks));
        }
    }

    public sealed class WorldBossSite
    {
        public string Id { get; }
        public string Kind { get; }
        public GroundVector Position { get; }

        internal WorldBossSite(string id, string kind, GroundVector position)
        {
            Id = id;
            Kind = kind;
            Position = position;
        }
    }

    public sealed class WorldBossObjective
    {
        public string Id { get; }
        public GroundVector Position { get; }

        internal WorldBossObjective(string id, GroundVector position)
        {
            Id = id;
            Position = position;
        }
    }

    /// <summary>Grand Solitaire : cible villageoise : lieu de la zone, route d'approche (sans téléportation) et objectifs de défense successifs.</summary>
    public sealed class WorldBossTarget
    {
        public string Id { get; }
        public string Place { get; }
        public IReadOnlyList<GroundVector> Route { get; }
        public IReadOnlyList<WorldBossObjective> Objectives { get; }

        internal WorldBossTarget(string id, string place, IEnumerable<GroundVector> route, IEnumerable<WorldBossObjective> objectives)
        {
            Id = id;
            Place = place;
            Route = Frozen.List(route, nameof(route));
            Objectives = Frozen.List(objectives, nameof(objectives));
        }
    }

    public sealed class WorldBossLoot
    {
        public string Trophy { get; }
        public string Appearance { get; }
        public string Mount { get; }

        /// <summary>Chance de la monture par victoire et par participant, en points de base (100 : 1 sur 100).</summary>
        public int MountChanceBasisPoints { get; }

        internal WorldBossLoot(string trophy, string appearance, string mount, int mountChance)
        {
            Trophy = trophy;
            Appearance = appearance;
            Mount = mount;
            MountChanceBasisPoints = mountChance;
        }
    }

    /// <summary>
    /// Grand Solitaire (dossier « Grand Solitaire et compagnons », p. 3 à 33 ; règles retenues, valeurs à valider) : un boss du monde
    /// validé, immuable : vie (base et par joueur), joueurs de <see cref="MinPlayers"/> à <see cref="MaxPlayers"/>, planification
    /// (fenêtre et chance), signes (<see cref="SignsMs"/>), limite (<see cref="LimitMs"/>), annonce de la cible (<see cref="AnnounceMs"/>),
    /// marche, phases et attaques annoncées, emplacements d'apparition admissibles, cibles villageoises, compagnons et butin.
    /// </summary>
    public sealed class WorldBossDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public Fixed Radius { get; }
        public long HealthBase { get; }
        public long HealthPerPlayer { get; }
        public int MinPlayers { get; }
        public int MaxPlayers { get; }
        public int WindowMinutes { get; }
        public int ChancePercent { get; }
        public long SignsMs { get; }
        public long LimitMs { get; }
        public long AnnounceMs { get; }
        public long ObjectiveMs { get; }
        public Fixed WalkSpeed { get; }
        public Fixed Clearance { get; }
        public IReadOnlyList<WorldBossPhase> Phases { get; }
        public IReadOnlyList<WorldBossAttack> Attacks { get; }
        public IReadOnlyList<WorldBossSite> Sites { get; }
        public IReadOnlyList<WorldBossTarget> Targets { get; }
        public int CompanionsInCombat { get; }
        public GroundVector Fallback { get; }
        public WorldBossLoot Loot { get; }

        internal WorldBossDefinition(string id, string name, Fixed radius, long healthBase, long healthPerPlayer, int minPlayers, int maxPlayers,
            int windowMinutes, int chancePercent, long signsMs, long limitMs, long announceMs, long objectiveMs, Fixed walkSpeed, Fixed clearance,
            IEnumerable<WorldBossPhase> phases, IEnumerable<WorldBossAttack> attacks, IEnumerable<WorldBossSite> sites, IEnumerable<WorldBossTarget> targets,
            int companionsInCombat, GroundVector fallback, WorldBossLoot loot)
        {
            Id = id;
            Name = name;
            Radius = radius;
            HealthBase = healthBase;
            HealthPerPlayer = healthPerPlayer;
            MinPlayers = minPlayers;
            MaxPlayers = maxPlayers;
            WindowMinutes = windowMinutes;
            ChancePercent = chancePercent;
            SignsMs = signsMs;
            LimitMs = limitMs;
            AnnounceMs = announceMs;
            ObjectiveMs = objectiveMs;
            WalkSpeed = walkSpeed;
            Clearance = clearance;
            Phases = Frozen.List(phases, nameof(phases));
            Attacks = Frozen.List(attacks, nameof(attacks));
            Sites = Frozen.List(sites, nameof(sites));
            Targets = Frozen.List(targets, nameof(targets));
            CompanionsInCombat = companionsInCombat;
            Fallback = fallback;
            Loot = loot;
        }

        public WorldBossAttack Attack(string id) => Attacks.FirstOrDefault(a => a.Id == id) ?? throw new ArgumentException($"Attaque « {id} » inconnue.", nameof(id));
        public WorldBossTarget Target(string id) => Targets.FirstOrDefault(t => t.Id == id) ?? throw new ArgumentException($"Cible « {id} » inconnue.", nameof(id));

        /// <summary>
        /// Vie maximale pour <paramref name="players"/> joueurs (les compagnons ne comptent jamais), ramenés dans [<see cref="MinPlayers"/>,
        /// <see cref="MaxPlayers"/>] : base + par joueur × joueurs.
        /// </summary>
        public long MaxHealthFor(int players)
        {
            if (players < 0) throw new ArgumentOutOfRangeException(nameof(players));
            int n = Math.Min(MaxPlayers, Math.Max(MinPlayers, players));
            return checked(HealthBase + HealthPerPlayer * n);
        }
    }

    public sealed class WorldBossBook
    {
        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public IReadOnlyList<WorldBossDefinition> Bosses { get; }

        internal WorldBossBook(int schemaVersion, string contentVersion, IEnumerable<WorldBossDefinition> bosses)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            Bosses = Frozen.List(bosses, nameof(bosses));
        }

        public WorldBossDefinition Boss(string id) => Bosses.FirstOrDefault(b => b.Id == id) ?? throw new ArgumentException($"Boss du monde « {id} » inconnu.", nameof(id));
    }

    public sealed class WorldBossLoadResult
    {
        public ValidationReport Report { get; }
        public WorldBossBook? Book { get; }
        public bool IsValid => Book != null;

        internal WorldBossLoadResult(ValidationReport report, WorldBossBook? book)
        {
            Report = report;
            Book = book;
        }
    }

    /// <summary>
    /// Grand Solitaire : validateur unique des boss du monde (<c>kind: "worldBosses"</c>), contre les textes (nom), la zone (lieux des cibles ;
    /// emplacements, routes, objectifs et point de repli dans l'aire et hors des obstacles, à la taille du boss) et le catalogue des objets
    /// (butin). Règles imposées vérifiées : joueurs de 5 à 100 au plus (bornes), annonce de la cible avant la limite, annonces des attaques
    /// plus longues que leur verrouillage, phases dans l'ordre (100 % d'abord, seuils décroissants), un compagnon au plus au combat.
    /// </summary>
    public static class WorldBossValidator
    {
        public const string Kind = "worldBosses";

        /// <summary>Version 1 (Grand Solitaire, 15 novembre 2026).</summary>
        public const int CurrentSchemaVersion = 1;

        public const int MaxBosses = 16, MaxPhases = 8, MaxAttacks = 16, MaxSites = 32, MaxTargets = 8, MaxRoute = 32, MaxObjectives = 8, MaxCircles = 16;

        /// <summary>Bornes des joueurs (décision : de 5 à 100 joueurs).</summary>
        public const int PlayersFloor = 1, PlayersCeiling = 100;

        public const long MaxHealth = 1_000_000_000_000, MaxDurationMs = 24L * 3600 * 1000, MaxAttackMs = 60_000;

        public static readonly IReadOnlyList<string> SiteKinds = Array.AsReadOnly(new[] { "meadow", "clearing", "ford", "plateau" });
        public static readonly IReadOnlyList<string> ShapeNames = Array.AsReadOnly(new[] { "cone", "line", "ring", "circles" });

        private static readonly Regex LocalId = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

        private static bool IsLocalId(string? id) => id != null && id.Length <= 64 && LocalId.IsMatch(id);

        public static WorldBossLoadResult Validate(WorldBossDocument document, TextCatalog texts, ZoneDefinition zone, ItemCatalog items, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (texts is null) throw new ArgumentNullException(nameof(texts));
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            if (items is null) throw new ArgumentNullException(nameof(items));
            ValidationReport r = report ?? new ValidationReport();
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, CurrentSchemaVersion, r))
                return new WorldBossLoadResult(r, null);
            if (document.Data?.Bosses is null) { Missing(r, "$.data.bosses", "boss du monde"); return new WorldBossLoadResult(r, null); }
            List<WorldBossData?> list = document.Data.Bosses;
            var bosses = new List<WorldBossDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (list.Count > MaxBosses) r.Add("$.data.bosses", ValidationCode.LimitExceeded, $"au plus {MaxBosses} boss du monde.");
            else
                for (int i = 0; i < list.Count; i++)
                {
                    string p = $"$.data.bosses[{i}]";
                    if (list[i] is not WorldBossData b) { Missing(r, p, "boss du monde"); continue; }
                    if (Boss(b, p, texts, zone, items, ids, r) is WorldBossDefinition d) bosses.Add(d);
                }
            if (!r.IsValid || document.ContentVersion is null) return new WorldBossLoadResult(r, null);
            return new WorldBossLoadResult(r, new WorldBossBook(document.SchemaVersion!.Value, document.ContentVersion, bosses));
        }

        private static WorldBossDefinition? Boss(WorldBossData b, string p, TextCatalog texts, ZoneDefinition zone, ItemCatalog items, HashSet<string> ids, ValidationReport r)
        {
            int before = r.Errors.Count;
            CombatSpace space = zone.Setup.Space!;
            if (!IsLocalId(b.Id)) r.Add(p + ".id", ValidationCode.InvalidValue, $"identifiant « {b.Id} » mal formé.");
            else if (!ids.Add(b.Id!)) r.Add(p + ".id", ValidationCode.DuplicateId, $"boss « {b.Id} » déjà décrit.");
            if (b.Name is null) Missing(r, p + ".name", "clé du nom affiché");
            else if (!texts.Has(b.Name)) r.Add(p + ".name", ValidationCode.UnknownReference, $"texte « {b.Name} » inconnu.");
            Fixed radius = Fixed.Zero;
            if (b.Radius is null) Missing(r, p + ".radius", "rayon de l'emprise du boss (m)");
            else if (b.Radius.Value < Fixed.One || b.Radius.Value > Fixed.FromInt(10)) r.Add(p + ".radius", ValidationCode.OutOfRange, "rayon hors de [1, 10] m.");
            else radius = b.Radius.Value;
            Long(b.HealthBase, p + ".healthBase", 1, MaxHealth, "vie de base", r);
            Long(b.HealthPerPlayer, p + ".healthPerPlayer", 0, MaxHealth, "vie par joueur", r);
            Int(b.MinPlayers, p + ".minPlayers", PlayersFloor, PlayersCeiling, "joueurs au moins", r);
            Int(b.MaxPlayers, p + ".maxPlayers", PlayersFloor, PlayersCeiling, "joueurs au plus", r);
            if (b.MinPlayers is int mn && b.MaxPlayers is int mx && mn > mx) r.Add(p + ".maxPlayers", ValidationCode.InvalidValue, "moins que minPlayers.");
            if (b.HealthBase is long hb && b.HealthPerPlayer is long hp && b.MaxPlayers is int top && hb + hp * top > MaxHealth)
                r.Add(p + ".healthPerPlayer", ValidationCode.OutOfRange, $"vie au plus fort au-delà de {MaxHealth}.");
            Int(b.WindowMinutes, p + ".windowMinutes", 60, 7 * 24 * 60, "fenêtre de planification (minutes de temps serveur)", r);
            Int(b.ChancePercent, p + ".chancePercent", 0, 100, "chance par fenêtre (pour cent)", r);
            Long(b.SignsMs, p + ".signsMs", 0, 30L * 60 * 1000, "signes avant l'apparition (ms)", r);
            Long(b.LimitMs, p + ".limitMs", 60_000, MaxDurationMs, "temps pour l'arrêter (ms)", r);
            Long(b.AnnounceMs, p + ".announceMs", 0, MaxDurationMs, "annonce de la cible (ms après l'apparition)", r);
            if (b.AnnounceMs is long an && b.LimitMs is long lim && an >= lim) r.Add(p + ".announceMs", ValidationCode.InvalidValue, "l'annonce de la cible vient avant la limite.");
            Long(b.ObjectiveMs, p + ".objectiveMs", 1000, 30L * 60 * 1000, "préparation de la charge contre un objectif (ms)", r);
            if (b.WindowMinutes is int w && b.SignsMs is long sg && b.LimitMs is long lm && b.ObjectiveMs is long om
                && checked(sg + lm + om * MaxObjectives) >= w * 60_000L)
                r.Add(p + ".windowMinutes", ValidationCode.InvalidValue, "la fenêtre doit contenir les signes, la limite et l'assaut entier.");
            if (b.WalkSpeed is null) Missing(r, p + ".walkSpeed", "vitesse de marche (m/s)");
            else if (b.WalkSpeed.Value < SpaceLimits.MinSpeed || b.WalkSpeed.Value > SpaceLimits.MaxSpeed)
                r.Add(p + ".walkSpeed", ValidationCode.OutOfRange, $"vitesse hors de [{SpaceLimits.MinSpeed}, {SpaceLimits.MaxSpeed}] m/s.");
            if (b.Clearance is null) Missing(r, p + ".clearance", "distance des joueurs au point d'émergence (m)");
            else if (b.Clearance.Value < Fixed.Zero || b.Clearance.Value > Fixed.FromInt(200)) r.Add(p + ".clearance", ValidationCode.OutOfRange, "distance hors de [0, 200] m.");

            List<WorldBossAttack> attacks = Attacks(b.Attacks, p + ".attacks", r);
            List<WorldBossPhase> phases = Phases(b.Phases, p + ".phases", attacks, r);

            var sites = new List<WorldBossSite>();
            if (b.Sites is null || b.Sites.Count == 0) Missing(r, p + ".sites", "emplacements d'apparition admissibles");
            else if (b.Sites.Count > MaxSites) r.Add(p + ".sites", ValidationCode.LimitExceeded, $"au plus {MaxSites} emplacements.");
            else
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < b.Sites.Count; i++)
                {
                    string sp = $"{p}.sites[{i}]";
                    if (b.Sites[i] is not WorldBossSiteData s) { Missing(r, sp, "emplacement"); continue; }
                    bool ok = true;
                    if (!IsLocalId(s.Id)) { r.Add(sp + ".id", ValidationCode.InvalidValue, $"identifiant « {s.Id} » mal formé."); ok = false; }
                    else if (!seen.Add(s.Id!)) { r.Add(sp + ".id", ValidationCode.DuplicateId, $"emplacement « {s.Id} » déjà décrit."); ok = false; }
                    if (s.Kind is null || !SiteKinds.Contains(s.Kind))
                    { r.Add(sp + ".kind", ValidationCode.UnknownReference, $"sorte « {s.Kind} » inconnue ; possibles : {string.Join(", ", SiteKinds)}."); ok = false; }
                    GroundVector? at = Point(s.Position, sp + ".position", radius, space, r);
                    if (ok && at is GroundVector a) sites.Add(new WorldBossSite(s.Id!, s.Kind!, a));
                }
            }

            var targets = new List<WorldBossTarget>();
            if (b.Targets is null || b.Targets.Count == 0) Missing(r, p + ".targets", "cibles villageoises");
            else if (b.Targets.Count > MaxTargets) r.Add(p + ".targets", ValidationCode.LimitExceeded, $"au plus {MaxTargets} cibles.");
            else
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < b.Targets.Count; i++)
                {
                    string tp = $"{p}.targets[{i}]";
                    if (b.Targets[i] is not WorldBossTargetData t) { Missing(r, tp, "cible"); continue; }
                    int errors = r.Errors.Count;
                    if (!IsLocalId(t.Id)) r.Add(tp + ".id", ValidationCode.InvalidValue, $"identifiant « {t.Id} » mal formé.");
                    else if (!seen.Add(t.Id!)) r.Add(tp + ".id", ValidationCode.DuplicateId, $"cible « {t.Id} » déjà décrite.");
                    if (t.Place is null) Missing(r, tp + ".place", "lieu visé de la zone");
                    else if (!zone.Places.Any(x => x.Id == t.Place)) r.Add(tp + ".place", ValidationCode.UnknownReference, $"lieu « {t.Place} » inconnu de la zone.");
                    var route = new List<GroundVector>();
                    if (t.Route is null || t.Route.Count == 0) Missing(r, tp + ".route", "route d'approche (au moins un point)");
                    else if (t.Route.Count > MaxRoute) r.Add(tp + ".route", ValidationCode.LimitExceeded, $"au plus {MaxRoute} points.");
                    else
                        for (int j = 0; j < t.Route.Count; j++)
                            if (Point(t.Route[j], $"{tp}.route[{j}]", radius, space, r) is GroundVector g) route.Add(g);
                    var objectives = new List<WorldBossObjective>();
                    if (t.Objectives is null || t.Objectives.Count == 0) Missing(r, tp + ".objectives", "objectifs de défense");
                    else if (t.Objectives.Count > MaxObjectives) r.Add(tp + ".objectives", ValidationCode.LimitExceeded, $"au plus {MaxObjectives} objectifs.");
                    else
                    {
                        var oseen = new HashSet<string>(StringComparer.Ordinal);
                        for (int j = 0; j < t.Objectives.Count; j++)
                        {
                            string op = $"{tp}.objectives[{j}]";
                            if (t.Objectives[j] is not WorldBossObjectiveData o) { Missing(r, op, "objectif"); continue; }
                            if (!IsLocalId(o.Id)) r.Add(op + ".id", ValidationCode.InvalidValue, $"identifiant « {o.Id} » mal formé.");
                            else if (!oseen.Add(o.Id!)) r.Add(op + ".id", ValidationCode.DuplicateId, $"objectif « {o.Id} » déjà décrit.");
                            // Un objectif est un point de la défense (palissade, porte) : seul son emplacement est vérifié, dans l'aire.
                            if (Point(o.Position, op + ".position", Fixed.Zero, space, r, obstacles: false) is GroundVector g && IsLocalId(o.Id))
                                objectives.Add(new WorldBossObjective(o.Id!, g));
                        }
                    }
                    if (r.Errors.Count == errors) targets.Add(new WorldBossTarget(t.Id!, t.Place!, route, objectives));
                }
            }

            Int(b.CompanionsInCombat, p + ".companionsInCombat", 0, 1, "compagnons au combat par joueur (décision du 14 novembre 2026 : un seul)", r);
            GroundVector? fallback = Point(b.Fallback, p + ".fallback", Fixed.FromRatio(1, 2), space, r);

            WorldBossLoot? loot = null;
            if (b.Loot is null) Missing(r, p + ".loot", "butin : trophy, appearance, mount, mountChanceBasisPoints");
            else
            {
                string lp = p + ".loot";
                bool ok = Item(b.Loot.Trophy, lp + ".trophy", items, r) & Item(b.Loot.Appearance, lp + ".appearance", items, r) & Item(b.Loot.Mount, lp + ".mount", items, r);
                ok &= Int(b.Loot.MountChanceBasisPoints, lp + ".mountChanceBasisPoints", 0, 10_000, "chance de la monture (points de base)", r);
                if (ok) loot = new WorldBossLoot(b.Loot.Trophy!, b.Loot.Appearance!, b.Loot.Mount!, b.Loot.MountChanceBasisPoints!.Value);
            }

            if (r.Errors.Count != before || loot is null || fallback is null) return null;
            return new WorldBossDefinition(b.Id!, b.Name!, radius, b.HealthBase!.Value, b.HealthPerPlayer!.Value, b.MinPlayers!.Value, b.MaxPlayers!.Value,
                b.WindowMinutes!.Value, b.ChancePercent!.Value, b.SignsMs!.Value, b.LimitMs!.Value, b.AnnounceMs!.Value, b.ObjectiveMs!.Value, b.WalkSpeed!.Value,
                b.Clearance!.Value, phases, attacks, sites, targets, b.CompanionsInCombat!.Value, fallback.Value, loot);
        }

        private static List<WorldBossAttack> Attacks(List<WorldBossAttackData?>? list, string p, ValidationReport r)
        {
            var attacks = new List<WorldBossAttack>();
            if (list is null || list.Count == 0) { Missing(r, p, "attaques annoncées"); return attacks; }
            if (list.Count > MaxAttacks) { r.Add(p, ValidationCode.LimitExceeded, $"au plus {MaxAttacks} attaques."); return attacks; }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            Fixed maxReach = Fixed.FromInt(100);
            for (int i = 0; i < list.Count; i++)
            {
                string ap = $"{p}[{i}]";
                if (list[i] is not WorldBossAttackData a) { Missing(r, ap, "attaque"); continue; }
                int before = r.Errors.Count;
                if (!IsLocalId(a.Id)) r.Add(ap + ".id", ValidationCode.InvalidValue, $"identifiant « {a.Id} » mal formé.");
                else if (!seen.Add(a.Id!)) r.Add(ap + ".id", ValidationCode.DuplicateId, $"attaque « {a.Id} » déjà décrite.");
                int shape = a.Shape is null ? -1 : ShapeNames.ToList().IndexOf(a.Shape);
                if (shape < 0) r.Add(ap + ".shape", ValidationCode.UnknownReference, $"forme « {a.Shape} » inconnue ; possibles : {string.Join(", ", ShapeNames)}.");
                Fixed range = Fixed.Zero, width = Fixed.Zero, radius = Fixed.Zero, inner = Fixed.Zero, outer = Fixed.Zero;
                int angle = 0, cmin = 0, cmax = 0;
                CircleAnchor anchor = CircleAnchor.Front;
                long stagger = 0;
                bool Length(Fixed? v, string field, string what, out Fixed value)
                {
                    value = Fixed.Zero;
                    if (v is null) { Missing(r, ap + "." + field, what); return false; }
                    if (v.Value <= Fixed.Zero || v.Value > maxReach) { r.Add(ap + "." + field, ValidationCode.OutOfRange, $"{what} hors de ]0, {maxReach}] m."); return false; }
                    value = v.Value;
                    return true;
                }
                switch ((WorldBossShape)shape)
                {
                    case WorldBossShape.Cone when shape >= 0:
                        Length(a.Range, "range", "rayon du secteur", out range);
                        if (a.Angle is null) Missing(r, ap + ".angle", "ouverture (degrés)");
                        else if (a.Angle < 1 || a.Angle > 180) r.Add(ap + ".angle", ValidationCode.OutOfRange, "ouverture hors de [1, 180] degrés.");
                        else angle = a.Angle.Value;
                        break;
                    case WorldBossShape.Line when shape >= 0:
                        Length(a.Range, "range", "longueur du couloir", out range);
                        Length(a.Width, "width", "largeur du couloir", out width);
                        break;
                    case WorldBossShape.Ring when shape >= 0:
                        Length(a.Inner, "inner", "rayon intérieur (zone sûre)", out inner);
                        if (Length(a.Outer, "outer", "rayon extérieur", out outer) && inner > Fixed.Zero && outer <= inner)
                            r.Add(ap + ".outer", ValidationCode.InvalidValue, "le rayon extérieur dépasse l'intérieur.");
                        break;
                    case WorldBossShape.Circles when shape >= 0:
                        Length(a.Radius, "radius", "rayon d'un disque", out radius);
                        if (Int(a.CountMin, ap + ".countMin", 1, MaxCircles, "disques au moins", r)) cmin = a.CountMin!.Value;
                        if (Int(a.CountMax, ap + ".countMax", 1, MaxCircles, "disques au plus", r)) cmax = a.CountMax!.Value;
                        if (cmin > 0 && cmax > 0 && cmax < cmin) r.Add(ap + ".countMax", ValidationCode.InvalidValue, "moins que countMin.");
                        if (Long(a.StaggerMs, ap + ".staggerMs", 0, MaxAttackMs, "écart entre deux impacts (ms)", r)) stagger = a.StaggerMs!.Value;
                        if (a.Anchor == "front") anchor = CircleAnchor.Front;
                        else if (a.Anchor == "targets") anchor = CircleAnchor.Targets;
                        else if (a.Anchor is null) Missing(r, ap + ".anchor", "où tombent les disques (front, targets)");
                        else r.Add(ap + ".anchor", ValidationCode.UnknownReference, $"ancrage « {a.Anchor} » inconnu ; possibles : front, targets.");
                        break;
                }
                Long(a.AnnounceMs, ap + ".announceMs", 100, MaxAttackMs, "annonce au sol (ms)", r);
                Long(a.LockMs, ap + ".lockMs", 0, MaxAttackMs, "verrouillage avant l'impact (ms)", r);
                Long(a.RecoveryMs, ap + ".recoveryMs", 0, MaxAttackMs, "reprise d'appui (ms)", r);
                if (a.AnnounceMs is long an && a.LockMs is long lk && lk >= an) r.Add(ap + ".lockMs", ValidationCode.InvalidValue, "le verrouillage tient dans l'annonce.");
                if (r.Errors.Count == before)
                    attacks.Add(new WorldBossAttack(a.Id!, (WorldBossShape)shape, range, angle, width, radius, inner, outer, cmin, cmax, stagger, anchor,
                        a.AnnounceMs!.Value, a.LockMs!.Value, a.RecoveryMs!.Value));
            }
            return attacks;
        }

        private static List<WorldBossPhase> Phases(List<WorldBossPhaseData?>? list, string p, List<WorldBossAttack> attacks, ValidationReport r)
        {
            var phases = new List<WorldBossPhase>();
            if (list is null || list.Count == 0) { Missing(r, p, "phases (calme, blessé, enragé)"); return phases; }
            if (list.Count > MaxPhases) { r.Add(p, ValidationCode.LimitExceeded, $"au plus {MaxPhases} phases."); return phases; }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int previous = 101;
            for (int i = 0; i < list.Count; i++)
            {
                string pp = $"{p}[{i}]";
                if (list[i] is not WorldBossPhaseData x) { Missing(r, pp, "phase"); continue; }
                int before = r.Errors.Count;
                if (!IsLocalId(x.Id)) r.Add(pp + ".id", ValidationCode.InvalidValue, $"identifiant « {x.Id} » mal formé.");
                else if (!seen.Add(x.Id!)) r.Add(pp + ".id", ValidationCode.DuplicateId, $"phase « {x.Id} » déjà décrite.");
                if (x.FromPercent is null) Missing(r, pp + ".fromPercent", "pour cent de vie où la phase commence");
                else if (i == 0 && x.FromPercent != 100) r.Add(pp + ".fromPercent", ValidationCode.InvalidValue, "la première phase commence à 100 %.");
                else if (x.FromPercent < 1 || x.FromPercent >= previous) r.Add(pp + ".fromPercent", ValidationCode.InvalidValue, "seuils décroissants, de 100 à 1 %.");
                else previous = x.FromPercent.Value;
                var ids = new List<string>();
                if (x.Attacks is null || x.Attacks.Count == 0) Missing(r, pp + ".attacks", "attaques de la phase");
                else
                    for (int j = 0; j < x.Attacks.Count; j++)
                    {
                        string id = x.Attacks[j] ?? "";
                        if (!attacks.Any(a => a.Id == id)) r.Add($"{pp}.attacks[{j}]", ValidationCode.UnknownReference, $"attaque « {x.Attacks[j]} » inconnue.");
                        else if (ids.Contains(id)) r.Add($"{pp}.attacks[{j}]", ValidationCode.DuplicateId, $"attaque « {id} » en double.");
                        else ids.Add(id);
                    }
                if (r.Errors.Count == before) phases.Add(new WorldBossPhase(x.Id!, x.FromPercent!.Value, ids));
            }
            return phases;
        }

        private static GroundVector? Point(PointData? point, string path, Fixed radius, CombatSpace space, ValidationReport r, bool obstacles = true)
        {
            if (point is null || point.X is null || point.Z is null) { Missing(r, path, "[x, z]"); return null; }
            var g = new GroundVector(point.X.Value, point.Z.Value);
            if (obstacles)
            {
                if (ZoneValidator.Clear(g, radius, space, "du boss") is string problem) { r.Add(path, ValidationCode.OutOfRange, problem); return null; }
            }
            else if (g.X < space.Min.X || g.X > space.Max.X || g.Z < space.Min.Z || g.Z > space.Max.Z)
            { r.Add(path, ValidationCode.OutOfRange, "point hors de l'aire praticable de la zone."); return null; }
            return g;
        }

        private static bool Item(string? id, string path, ItemCatalog items, ValidationReport r)
        {
            if (id is null) { Missing(r, path, "objet"); return false; }
            if (!items.Items.Any(i => i.Id == id)) { r.Add(path, ValidationCode.UnknownReference, $"objet « {id} » inconnu du catalogue."); return false; }
            return true;
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
