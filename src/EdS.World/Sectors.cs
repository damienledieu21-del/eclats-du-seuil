using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;

namespace EdS.World
{
    /// <summary>
    /// Sous-lot V2.26 (secteurs de simulation, PLAN_GRANDE_CARTE.md § 2.3 et 2.4 ; rupture retenue par Damien le 11 novembre 2026) : découpage
    /// de l'aire d'une zone à région en secteurs de 256 m (2 × 2 cases de <see cref="RegionGrid"/>), chacun simulé par sa propre session de
    /// zone sur son espace : le secteur plus une marge de <see cref="MarginMeters"/> m de chaque côté, coupés aux bornes de l'aire. Fonction
    /// pure des données : mêmes entrées, mêmes secteurs.
    /// <list type="bullet">
    /// <item>Obstacles d'un secteur : ceux de l'aire dont la boîte touche son espace (marge comprise), dans l'ordre de l'aire.</item>
    /// <item>Créatures d'un secteur : celles de l'aire dont la place de départ est dans le secteur (case de <see cref="RegionGrid.CellOf"/>) :
    /// chaque créature appartient à un seul secteur.</item>
    /// <item>Secteur du cœur (<see cref="HomeSector"/>, celui du point d'entrée de la zone) : si l'aire entière tient dans son espace, sa
    /// simulation est <b>exactement</b> celle de la zone (même combat, même graine, mêmes clés) : les Vallons d'aujourd'hui ne changent pas.
    /// Un autre secteur tire sa graine de celle de l'aire, de l'empreinte de son nom et de son numéro de session.</item>
    /// </list>
    /// </summary>
    public sealed class SectorMap
    {
        /// <summary>Marge de l'espace d'un secteur au-delà de ses bords (32 m), hystérésis d'un passage (8 m), distance d'activation (96 m).</summary>
        public const int MarginMeters = 32, HysteresisMeters = 8, ActivationMeters = 96;

        /// <summary>Un secteur sans joueur à moins de <see cref="ActivationMeters"/> m depuis 60 s se ferme.</summary>
        public const long CloseAfterMs = 60_000;

        public RegionGrid Grid { get; }

        /// <summary>Simulation de l'aire entière (zone : bloc space, créatures) dont les secteurs sont découpés.</summary>
        public CombatSetup Area { get; }

        /// <summary>Secteur du cœur (point d'entrée de la zone).</summary>
        public string HomeSector { get; }

        private readonly Dictionary<string, (long MinX, long MinZ, long MaxX, long MaxZ)?> _spaces = new Dictionary<string, (long, long, long, long)?>(StringComparer.Ordinal);

        /// <exception cref="ArgumentException">Aire sans espace, hors de la région, ou point d'entrée hors de la région.</exception>
        public SectorMap(RegionGrid grid, CombatSetup area, GroundVector entry)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Area = area ?? throw new ArgumentNullException(nameof(area));
            if (area.Space is null) throw new ArgumentException("Une aire à secteurs est un combat positionné.", nameof(area));
            if (!grid.Contains(entry)) throw new ArgumentException("Point d'entrée hors de la région.", nameof(entry));
            HomeSector = grid.CellOf(entry).SectorName;
        }

        /// <summary>Découpage d'une zone à région (schéma 13 et suivants).</summary>
        public static SectorMap For(ZoneDefinition zone, CombatSetup? area = null) =>
            new SectorMap((zone ?? throw new ArgumentNullException(nameof(zone))).Region ?? throw new ArgumentException("Zone sans région.", nameof(zone)),
                area ?? zone.Setup, zone.Entry.Position);

        private static long M(int meters) => checked(meters * Fixed.Scale);

        /// <summary>Colonne et rangée d'un secteur d'après son nom (« s07_06 ») ; faux si le nom est mal formé ou hors de la grille.</summary>
        public bool TryParse(string? sector, out int column, out int row)
        {
            column = row = -1;
            if (!RegionCellNames.IsSectorName(sector)) return false;
            column = int.Parse(sector!.Substring(1, 2), CultureInfo.InvariantCulture);
            row = int.Parse(sector.Substring(4, 2), CultureInfo.InvariantCulture);
            return column < Grid.SectorColumns && row < Grid.SectorRows;
        }

        public static string NameOf(int column, int row) =>
            "s" + column.ToString("D2", CultureInfo.InvariantCulture) + "_" + row.ToString("D2", CultureInfo.InvariantCulture);

        /// <summary>Secteur d'un point (un point hors de la région prend le secteur le plus proche).</summary>
        public string SectorOf(GroundVector p)
        {
            Fixed x = Clamp(p.X, Grid.Min.X, Grid.Max.X), z = Clamp(p.Z, Grid.Min.Z, Grid.Max.Z);
            return Grid.CellOf(new GroundVector(x, z)).SectorName;
        }

        private static Fixed Clamp(Fixed v, Fixed lo, Fixed hi) => v < lo ? lo : v > hi ? hi : v;

        /// <summary>Rectangle d'un secteur (bornes brutes), sans la marge.</summary>
        public (long MinX, long MinZ, long MaxX, long MaxZ) Rect(string sector)
        {
            if (!TryParse(sector, out int column, out int row)) throw new ArgumentException($"Secteur inconnu « {sector} ».", nameof(sector));
            long minX = checked(M(Grid.OriginX) + column * M(RegionGrid.SectorMeters)), minZ = checked(M(Grid.OriginZ) + row * M(RegionGrid.SectorMeters));
            return (minX, minZ, checked(minX + M(RegionGrid.SectorMeters)), checked(minZ + M(RegionGrid.SectorMeters)));
        }

        /// <summary>
        /// Espace simulé d'un secteur : son rectangle plus la marge, coupé aux bornes de l'aire ; null si le secteur ne touche pas l'aire (rien
        /// à simuler : il n'est jamais ouvert).
        /// </summary>
        public (long MinX, long MinZ, long MaxX, long MaxZ)? Space(string sector)
        {
            if (_spaces.TryGetValue(sector, out var cached)) return cached;
            if (!TryParse(sector, out _, out _)) return null;
            var r = Rect(sector);
            CombatSpace area = Area.Space!;
            long minX = Math.Max(checked(r.MinX - M(MarginMeters)), area.Min.X.Raw), minZ = Math.Max(checked(r.MinZ - M(MarginMeters)), area.Min.Z.Raw);
            long maxX = Math.Min(checked(r.MaxX + M(MarginMeters)), area.Max.X.Raw), maxZ = Math.Min(checked(r.MaxZ + M(MarginMeters)), area.Max.Z.Raw);
            // Le secteur doit toucher l'aire par son intérieur : un secteur dont seule la marge déborde sur l'aire n'a rien à lui.
            bool owns = Math.Max(r.MinX, area.Min.X.Raw) < Math.Min(r.MaxX, area.Max.X.Raw) && Math.Max(r.MinZ, area.Min.Z.Raw) < Math.Min(r.MaxZ, area.Max.Z.Raw);
            (long, long, long, long)? space = owns && minX < maxX && minZ < maxZ ? (minX, minZ, maxX, maxZ) : ((long, long, long, long)?)null;
            _spaces[sector] = space;
            return space;
        }

        /// <summary>Distance (brute) d'un point au rectangle d'un secteur (0 dedans).</summary>
        public long DistanceTo(string sector, GroundVector p)
        {
            var r = Rect(sector);
            long dx = Math.Max(0, Math.Max(checked(r.MinX - p.X.Raw), checked(p.X.Raw - r.MaxX)));
            long dz = Math.Max(0, Math.Max(checked(r.MinZ - p.Z.Raw), checked(p.Z.Raw - r.MaxZ)));
            return IntMath.Sqrt(Planar.LengthSquared(dx, dz));
        }

        /// <summary>De combien (brut) le point dépasse-t-il le rectangle du secteur, sur l'axe où il dépasse le plus (0 dedans).</summary>
        public long Overshoot(string sector, GroundVector p)
        {
            var r = Rect(sector);
            return Math.Max(0, Math.Max(Math.Max(checked(r.MinX - p.X.Raw), checked(p.X.Raw - r.MaxX)), Math.Max(checked(r.MinZ - p.Z.Raw), checked(p.Z.Raw - r.MaxZ))));
        }

        /// <summary>Secteurs simulables à au plus <see cref="ActivationMeters"/> m du point (le sien compris), dans l'ordre des noms.</summary>
        public IReadOnlyList<string> Near(GroundVector p)
        {
            var result = new List<string>();
            if (!TryParse(SectorOf(p), out int column, out int row)) return result;
            for (int r = Math.Max(0, row - 1); r <= Math.Min(Grid.SectorRows - 1, row + 1); r++)
                for (int c = Math.Max(0, column - 1); c <= Math.Min(Grid.SectorColumns - 1, column + 1); c++)
                {
                    string name = NameOf(c, r);
                    if (Space(name) != null && DistanceTo(name, p) <= M(ActivationMeters)) result.Add(name);
                }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        /// <summary>L'aire entière tient-elle dans l'espace du secteur du cœur ? (sa session est alors exactement celle de la zone)</summary>
        public bool HomeHoldsArea
        {
            get
            {
                var s = Space(HomeSector);
                CombatSpace a = Area.Space!;
                return s is { } h && h.MinX <= a.Min.X.Raw && h.MinZ <= a.Min.Z.Raw && h.MaxX >= a.Max.X.Raw && h.MaxZ >= a.Max.Z.Raw;
            }
        }

        /// <summary>Le combattant de l'aire appartient-il au secteur (place de départ dans le secteur) ?</summary>
        public bool Owns(string sector, CombatantSetup combatant) =>
            combatant.Placement is { } p && SectorOf(p.Position) == sector;

        /// <summary>
        /// Simulation du secteur pour sa session <paramref name="session"/> : espace avec marge, obstacles qui le touchent, créatures qui lui
        /// appartiennent ; graine de l'aire pour le secteur du cœur, sinon tirée de l'empreinte du nom et de la session. Le secteur du cœur qui
        /// tient toute l'aire rend l'aire elle-même.
        /// </summary>
        /// <exception cref="ArgumentException">Secteur inconnu ou hors de l'aire.</exception>
        public CombatSetup SetupFor(string sector, long session) => SetupFor(sector, session, reopened: false);

        /// <param name="reopened">Revue Codex de la PR #150 : le cœur rouvert après une fermeture prend une graine tirée de son nom et de sa
        /// session, comme un autre secteur (sa première ouverture reste exactement la zone).</param>
        public CombatSetup SetupFor(string sector, long session, bool reopened)
        {
            if (sector == HomeSector && HomeHoldsArea && !reopened)
                return Area;
            if (sector == HomeSector && HomeHoldsArea)
            {
                CombatSetup a = Area;
                return new CombatSetup(SeedOf(a.Seed, sector, session), a.DurationMs, a.Stats, a.Damage, a.Progression, a.Combat, a.Combatants, a.Commands,
                    a.Data, a.Space, a.Zone, a.Truces);
            }
            var space = Space(sector) ?? throw new ArgumentException($"Secteur « {sector} » hors de l'aire.", nameof(sector));
            CombatSpace area = Area.Space!;
            var obstacles = area.Obstacles.Where(o =>
            {
                var poly = new ConvexPolygon(o.Points);
                return poly.MaxX >= space.MinX && poly.MinX <= space.MaxX && poly.MaxZ >= space.MinZ && poly.MinZ <= space.MaxZ;
            });
            var cut = new CombatSpace(new GroundVector(Fixed.FromRaw(space.MinX), Fixed.FromRaw(space.MinZ)),
                new GroundVector(Fixed.FromRaw(space.MaxX), Fixed.FromRaw(space.MaxZ)), obstacles);
            var combatants = Area.Combatants.Where(c => Owns(sector, c)).ToList();
            long seed = sector == HomeSector && !reopened ? Area.Seed : SeedOf(Area.Seed, sector, session);
            return new CombatSetup(seed, Area.DurationMs, Area.Stats, Area.Damage, Area.Progression, Area.Combat, combatants, Area.Commands,
                Area.Data, cut, true, Area.Truces);
        }

        /// <summary>Graine d'un secteur : FNV-1a 64 du nom et du numéro de session, mêlée à celle de l'aire ; positive.</summary>
        public static long SeedOf(long areaSeed, string sector, long session)
        {
            ulong hash = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes(sector + "/" + session.ToString(CultureInfo.InvariantCulture)))
                unchecked { hash = (hash ^ b) * 1099511628211UL; }
            return (long)(unchecked(hash ^ (ulong)areaSeed) & long.MaxValue);
        }
    }

    /// <summary>Sous-lot V2.26 : ce qui s'est passé entre les secteurs pendant un tick du monde.</summary>
    public enum SectorEventKind
    {
        /// <summary>Un secteur s'ouvre (une session de zone commence) au tick du monde donné.</summary>
        Opened = 0,

        /// <summary>Un secteur sans joueur proche depuis 60 s se ferme (registre repris par le monde).</summary>
        Closed = 1,

        /// <summary>Un joueur quitte son secteur en fin de tick (hors de toute rencontre, 8 m au-delà du bord).</summary>
        Departed = 2,

        /// <summary>Le joueur arrive dans le secteur voisin, au tick suivant, avec son état de combat.</summary>
        Arrived = 3,

        /// <summary>Au bout de la marge, encore engagé : une créature de la rencontre lâche (comme pour la laisse).</summary>
        Released = 4,
    }

    public sealed class SectorEvent
    {
        public SectorEventKind Kind { get; }
        public string Sector { get; }

        /// <summary>Joueur (départ, arrivée), créature (lâcher) ou null (ouverture, fermeture).</summary>
        public string? Subject { get; }

        /// <summary>Secteur d'arrivée d'un départ, secteur de départ d'une arrivée ; null sinon.</summary>
        public string? Other { get; }

        public long WorldTick { get; }

        internal SectorEvent(SectorEventKind kind, string sector, string? subject, string? other, long worldTick)
        {
            Kind = kind;
            Sector = sector;
            Subject = subject;
            Other = other;
            WorldTick = worldTick;
        }

        public override string ToString() => $"{WorldTick} {Kind} {Sector} {Subject} {Other}";
    }

    /// <summary>
    /// Sous-lot V2.26 (PLAN_GRANDE_CARTE.md § 2.3 et 2.4) : hôte des secteurs d'une région (serveur du lot 13, ou hôte local) pour un ou
    /// plusieurs joueurs. Déterministe : les mêmes entrées donnent les mêmes sessions, tick après tick.
    /// <list type="bullet">
    /// <item><b>Activation</b> : un secteur s'ouvre (session de zone neuve, au tick du monde courant) dès qu'un joueur est à moins de
    /// <see cref="SectorMap.ActivationMeters"/> m de son bord (au plus 4 par joueur) ; il se ferme après
    /// <see cref="SectorMap.CloseAfterMs"/> ms sans joueur aussi proche. Tous les secteurs ouverts avancent au même tick du monde, dans l'ordre
    /// de leurs noms.</item>
    /// <item><b>Passage</b> : un joueur à plus de <see cref="SectorMap.HysteresisMeters"/> m au-delà du bord de son secteur, hors de toute
    /// rencontre, le quitte en fin du tick t (commande <c>leave</c>) et arrive au tick t + 1 dans le voisin, avec tout son état de combat
    /// (<see cref="CombatEngine.Carried"/>), sa place et son orientation ; plusieurs passages au même tick : ordre du secteur de départ, puis
    /// du joueur. Sa place est prise ? La plus proche place libre (anneaux de 0,5 m). Chaque passage donne au joueur une identité nouvelle
    /// dans la session d'arrivée si la sienne y a déjà servi (<c>&lt;joueur&gt;_p&lt;n&gt;</c> : jamais réutilisée).</item>
    /// <item><b>En combat</b> : il reste dans son secteur, jusqu'au bout de la marge ; s'il y arrive encore engagé, les créatures de la rencontre
    /// lâchent (<see cref="ZonePopulations.Release"/>), puis il passe quand il n'est plus dans aucune rencontre.</item>
    /// <item><b>Clés</b> : le secteur du cœur garde les clés de la zone (« zone.vallons/3/death/loup1 ») ; un autre ajoute son nom
    /// (« zone.vallons/s08_05/1/death/loup1 »). Le registre est partagé : chaque session part du registre du monde et y rend ses clés à sa
    /// fermeture.</item>
    /// <item><b>Joueurs</b> : entrées et sorties demandées pendant un tick sont appliquées au début du suivant, dans l'ordre des identifiants
    /// (rien ne dépend de l'ordre d'arrivée des messages).</item>
    /// </list>
    /// Ce qui n'est pas simulé : un secteur fermé (ses créatures repartent d'une session neuve à sa réouverture) ; les montures, compagnons et
    /// caravanes ne passent pas encore (lots suivants) ; les altérations ne passent pas (limite du moteur 0.31.0).
    /// </summary>
    public sealed class SectorWorld
    {
        private sealed class Open
        {
            public string Name = string.Empty;
            public Zone Zone = null!;
            public ZonePopulations Populations = null!;
            public long IdleTicks;
            public ObstacleIndex Walls = null!;
        }

        private sealed class Player
        {
            public string Id = string.Empty;
            public CharacterSetup Setup = null!;
            public string? Sector;
            public string Combatant = string.Empty;
            public string? PendingTo;
            public GroundVector? PendingMove;

            /// <summary>Revue Codex de la PR #150 : arrivée programmée (secteur de départ et personnage complet), pour la rattraper si elle est refusée.</summary>
            public string? ArrivingFrom;

            /// <summary>Revue Codex de la PR #150 (2e tour) : entré dans la région sans place encore (il réessaie à chaque tick).</summary>
            public bool Waiting;
            public CharacterSetup? Arrival;
            public bool PendingBackward;
            public int Passages;
        }

        private readonly SectorMap _map;
        private readonly string _zoneId;
        private readonly long _session;
        private readonly IReadOnlyList<Population> _populations;
        private readonly Func<string, long, ICombatLog>? _logs;
        private readonly SortedDictionary<string, Open> _open = new SortedDictionary<string, Open>(StringComparer.Ordinal);
        private readonly SortedDictionary<string, Player> _players = new SortedDictionary<string, Player>(StringComparer.Ordinal);
        private readonly SortedDictionary<string, CharacterSetup> _entering = new SortedDictionary<string, CharacterSetup>(StringComparer.Ordinal);
        private readonly SortedSet<string> _exiting = new SortedSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _consumed;
        private readonly SortedDictionary<string, long> _sectorSessions;
        private readonly long _closeAfterTicks;
        private bool _homeOpened;

        public SectorMap Map => _map;
        public string ZoneId => _zoneId;

        /// <summary>Temps du monde du prochain tick à simuler.</summary>
        public long WorldTick { get; private set; }

        /// <param name="session">Session du monde (celle du secteur du cœur, comme une session de zone d'avant).</param>
        /// <param name="sectorSessions">Dernière session de chaque autre secteur (sauvegarde) : une réouverture prend la suivante.</param>
        /// <param name="logs">Journal de chaque session de secteur (nom, session) ; null : aucun.</param>
        public SectorWorld(SectorMap map, string zoneId, long startWorldTick, long session, IEnumerable<string>? consumed = null,
            IEnumerable<Population>? populations = null, IEnumerable<KeyValuePair<string, long>>? sectorSessions = null, Func<string, long, ICombatLog>? logs = null)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _zoneId = zoneId ?? throw new ArgumentNullException(nameof(zoneId));
            if (session < 1) throw new ArgumentOutOfRangeException(nameof(session));
            if (startWorldTick < 0) throw new ArgumentOutOfRangeException(nameof(startWorldTick));
            _session = session;
            WorldTick = startWorldTick;
            _consumed = new HashSet<string>(consumed ?? Array.Empty<string>(), StringComparer.Ordinal);
            _populations = (populations ?? Array.Empty<Population>()).ToArray();
            _sectorSessions = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, long> s in sectorSessions ?? Array.Empty<KeyValuePair<string, long>>())
            {
                if (!RegionCellNames.IsSectorName(s.Key) || s.Value < 0) throw new ArgumentException($"Session de secteur « {s.Key} » invalide.", nameof(sectorSessions));
                _sectorSessions[s.Key] = s.Value;
            }
            _logs = logs;
            _closeAfterTicks = SectorMap.CloseAfterMs / map.Area.Combat.TickMilliseconds;
        }

        /// <summary>Hôte des secteurs d'une zone à région, avec ses populations (chacune dans son secteur).</summary>
        public static SectorWorld For(ZoneDefinition zone, long startWorldTick, long session, IEnumerable<string>? consumed = null,
            IEnumerable<KeyValuePair<string, long>>? sectorSessions = null, Func<string, long, ICombatLog>? logs = null)
        {
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            string[] keys = (consumed ?? Array.Empty<string>()).ToArray();
            CombatSetup area = ZoneSetups.For(zone, keys);
            return new SectorWorld(SectorMap.For(zone, area), zone.Id, startWorldTick, session, keys, zone.Populations, sectorSessions, logs);
        }

        /// <summary>Secteurs ouverts, dans l'ordre des noms.</summary>
        public IReadOnlyList<string> OpenSectors => _open.Keys.ToArray();

        /// <summary>Session de zone d'un secteur ouvert, ou null.</summary>
        public Zone? SectorZone(string sector) => _open.TryGetValue(sector, out Open? o) ? o.Zone : null;

        /// <summary>Dernière session de chaque secteur autre que celui du cœur (à sauvegarder).</summary>
        public IReadOnlyDictionary<string, long> SectorSessions => _sectorSessions;

        /// <summary>Registre du monde : le sien et celui des secteurs ouverts, dans l'ordre ordinal.</summary>
        public IReadOnlyList<string> Consumed
        {
            get
            {
                var all = new HashSet<string>(_consumed, StringComparer.Ordinal);
                foreach (Open o in _open.Values) all.UnionWith(o.Zone.Consumed);
                return all.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            }
        }

        /// <summary>Joueurs (dans l'ordre des identifiants).</summary>
        public IReadOnlyList<string> Players => _players.Keys.ToArray();

        /// <summary>Secteur où se trouve le joueur (null s'il n'est pas encore entré).</summary>
        public string? SectorOf(string player) => _players.TryGetValue(player, out Player? p) ? p.Sector : null;

        /// <summary>Identité du joueur dans la session de son secteur (la sienne, ou <c>&lt;joueur&gt;_p&lt;n&gt;</c> après un passage).</summary>
        public string CombatantOf(string player) => _players.TryGetValue(player, out Player? p)
            ? (p.Waiting ? throw new InvalidOperationException($"Joueur {player} en attente d'une place.") : p.Combatant)
            : throw new ArgumentException($"Joueur inconnu : {player}.", nameof(player));

        /// <summary>Revue Codex de la PR #150 (2e tour) : vrai si le joueur est entré dans la région sans place libre encore (il réessaie à chaque tick).</summary>
        public bool IsWaiting(string player) => _players.TryGetValue(player, out Player? p) ? p.Waiting : throw new ArgumentException($"Joueur inconnu : {player}.", nameof(player));

        /// <summary>Moteur du secteur du joueur, pour ses commandes (déplacement, compétences).</summary>
        public CombatEngine EngineOf(string player) =>
            SectorOf(player) is string s && _open.TryGetValue(s, out Open? o) ? o.Zone.Engine : throw new InvalidOperationException($"{player} n'est dans aucun secteur ouvert.");

        /// <summary>Vue du joueur dans son secteur.</summary>
        public CombatantView ViewOf(string player) => EngineOf(player).View(CombatantOf(player));

        /// <summary>Un joueur entre dans la région à sa place (appliqué au début du prochain tick, dans l'ordre des identifiants).</summary>
        /// <exception cref="ArgumentException">Joueur sans place, déjà là ou déjà annoncé.</exception>
        public void Enter(CharacterSetup player)
        {
            if (player is null) throw new ArgumentNullException(nameof(player));
            if (player.Placement is null) throw new ArgumentException("Un joueur entre à une place.", nameof(player));
            if (_players.ContainsKey(player.Id) || _entering.ContainsKey(player.Id)) throw new ArgumentException($"Joueur déjà là : {player.Id}.", nameof(player));
            CombatSpace area = _map.Area.Space!;
            GroundVector at = player.Placement.Position;
            if (at.X < area.Min.X || at.X > area.Max.X || at.Z < area.Min.Z || at.Z > area.Max.Z || _map.Space(_map.SectorOf(at)) is null)
                throw new ArgumentException("Place hors de l'aire.", nameof(player));
            _entering[player.Id] = player;
        }

        /// <summary>Un joueur quitte la région (départ au prochain tick, dans l'ordre des identifiants).</summary>
        public void Exit(string player)
        {
            if (!_players.ContainsKey(player)) throw new ArgumentException($"Joueur inconnu : {player}.", nameof(player));
            _exiting.Add(player);
        }

        private Open OpenSector(string name, List<SectorEvent> events)
        {
            if (_open.TryGetValue(name, out Open? open)) return open;
            bool home = name == _map.HomeSector;
            long session;
            bool reopened = false;
            if (home && !_homeOpened && !_sectorSessions.ContainsKey(name)) session = _session;
            else
            {
                // Revue Codex de la PR #150 : le cœur rouvert prend aussi une session nouvelle (clés de mort et journal nouveaux, format des
                // clés du cœur gardé) ; elle part au-delà de la session du monde.
                _sectorSessions.TryGetValue(name, out long last);
                session = checked(Math.Max(last, home ? _session : 0) + 1);
                reopened = home;
            }
            if (home) _homeOpened = true;
            if (!home || reopened) _sectorSessions[name] = session;
            CombatSetup setup = _map.SetupFor(name, session, reopened);
            var zone = new Zone(_zoneId, setup, session, WorldTick, _consumed, _logs?.Invoke(name, session), sector: home ? null : name);
            var populations = new ZonePopulations(_populations.Where(p => p.Template.Placement != null && _map.SectorOf(p.Template.Placement.Position) == name
                && setup.Combatants.Any(c => c.Id == p.Creature)));
            open = new Open { Name = name, Zone = zone, Populations = populations, Walls = new ObstacleIndex(setup.Space!.Obstacles.Select(o => new ConvexPolygon(o.Points)).ToArray()) };
            _open[name] = open;
            events.Add(new SectorEvent(SectorEventKind.Opened, name, null, null, WorldTick));
            return open;
        }

        private void CloseSector(Open open, List<SectorEvent> events)
        {
            _consumed.UnionWith(open.Zone.Consumed);
            _open.Remove(open.Name);
            events.Add(new SectorEvent(SectorEventKind.Closed, open.Name, null, null, WorldTick));
        }

        /// <summary>Identité libre du joueur dans une session : la sienne, sinon <c>&lt;joueur&gt;_p&lt;n&gt;</c> (n croissant, jamais reprise).</summary>
        private static string IdentityIn(Zone zone, Player p)
        {
            IReadOnlyList<string> ids = zone.Engine.CombatantIds;
            if (!ids.Contains(p.Id)) return p.Id;
            while (true)
            {
                p.Passages = checked(p.Passages + 1);
                string id = p.Id + "_p" + p.Passages.ToString(CultureInfo.InvariantCulture);
                if (!ids.Contains(id)) return id;
            }
        }

        /// <summary>
        /// Place libre la plus proche de <paramref name="at"/> pour l'emprise du joueur dans le secteur : dans l'espace, hors du décor, sans
        /// chevaucher une emprise solide présente (règle de contact) ; anneaux de 0,5 m (jusqu'à 8 m), huit directions dans un ordre fixe.
        /// Null s'il n'y en a aucune.
        /// </summary>
        /// <summary>Place libre la plus proche dans un secteur ouvert (null s'il n'est pas ouvert ou s'il n'y en a pas) ; pour les tests.</summary>
        internal GroundVector? FreeSpotIn(string sector, GroundVector at, Fixed radius) => _open.TryGetValue(sector, out Open? o) ? FreeSpot(o, at, radius) : null;

        private GroundVector? FreeSpot(Open open, GroundVector at, Fixed radius)
        {
            CombatEngine engine = open.Zone.Engine;
            CombatSpace space = open.Zone.Setup.Space!;
            long r = radius.Raw;
            // Revue Codex de la PR #150 : les arrivées déjà programmées à ce tick (réapparitions, autres passages) comptent comme présentes.
            var others = engine.CombatantIds.Select(engine.View).Where(v => (v.IsPresent || (!v.HasLeft && !v.IsDead && v.ArrivalTick == engine.CurrentTick))
                && !v.IsDead && v.Position != null && v.Radius != null
                && open.Zone.Setup.Combat.AreSolid(true, radius, v.Kind == CombatantKind.Character, v.Radius.Value)).ToList();
            bool Free(long x, long z) =>
                x - r >= space.Min.X.Raw && x + r <= space.Max.X.Raw && z - r >= space.Min.Z.Raw && z + r <= space.Max.Z.Raw
                && !open.Walls.AnyOverlapsCircle(x, z, r)
                && !others.Any(v => Planar.CirclesOverlap(x, z, r, v.Position!.Value.X.Raw, v.Position.Value.Z.Raw, v.Radius!.Value.Raw));
            if (Free(at.X.Raw, at.Z.Raw)) return at;
            long[] dx = { 10_000, 7_071, 0, -7_071, -10_000, -7_071, 0, 7_071 }, dz = { 0, 7_071, 10_000, 7_071, 0, -7_071, -10_000, -7_071 };
            for (int ring = 1; ring <= 16; ring++)
                for (int d = 0; d < 8; d++)
                {
                    long x = checked(at.X.Raw + dx[d] * ring / 2), z = checked(at.Z.Raw + dz[d] * ring / 2);
                    if (Free(x, z)) return new GroundVector(Fixed.FromRaw(x), Fixed.FromRaw(z));
                }
            return null;
        }

        /// <summary>
        /// Fait arriver le joueur dans le secteur <paramref name="sector"/> au tick courant de sa session : place libre la plus proche, place et
        /// identité disponibles, arrivée admise par le moteur ; faux sans rien changer sinon.
        /// </summary>
        private bool Place(Player p, string sector, GroundVector at, Fixed radius, GroundVector? facing, IReadOnlyList<ConsumableSetup>? consumables,
            CarriedState? carried, GroundVector? move, List<SectorEvent> events, string? from = null, bool announce = true)
        {
            if (!_open.TryGetValue(sector, out Open? target) || target.Zone.IsOver) return false;
            CombatEngine engine = target.Zone.Engine;
            if (engine.FreePlaces == 0 || engine.IdentitiesLeft == 0) return false;
            if (FreeSpot(target, at, radius) is not GroundVector spot) return false;
            int passages = p.Passages;
            string id = IdentityIn(target.Zone, p);
            long atMs = checked(engine.CurrentTick * engine.TickMilliseconds);
            CharacterSetup arrival = Arriving(p.Setup, id, new CombatantPlacement(spot, radius, facing), atMs, consumables, carried);
            try
            {
                target.Zone.Enter(arrival);
            }
            catch (ArgumentException)
            {
                p.Passages = passages;
                return false;
            }
            // La marche en cours continue de l'autre côté (même direction, dès le tick d'arrivée).
            if (move is GroundVector m) engine.Submit(new CombatCommand(atMs, id, m, p.PendingBackward));
            if (announce) events.Add(new SectorEvent(SectorEventKind.Arrived, sector, p.Id, from ?? p.Sector, WorldTick));   // sector == from : retour, passage impossible
            p.ArrivingFrom = from ?? p.ArrivingFrom;
            p.Arrival = arrival;
            p.Sector = sector;
            p.Combatant = id;
            return true;
        }

        private static CharacterSetup Arriving(CharacterSetup c, string id, CombatantPlacement placement, long atMs, IReadOnlyList<ConsumableSetup>? consumables,
            CarriedState? carried) =>
            new CharacterSetup(id, c.Team, c.Class, c.Build, c.Initiative, placement, atMs, c.Gear, consumables ?? c.Consumables, c.Shield, c.Equipment, carried);

        private static bool Leave(Zone zone, string combatant)
        {
            CombatantView v = zone.Engine.View(combatant);
            if (!v.IsPresent || v.IsDead) return false;
            try
            {
                zone.Leave(combatant);
                return true;
            }
            catch (ArgumentException)
            {
                return false;   // départ refusé par le pilote (plafond de commandes) : nouvel essai au tick suivant
            }
        }

        private static bool InEncounter(Zone zone, string combatant) => zone.Encounters().Any(e => e.Characters.Contains(combatant));

        /// <summary>
        /// Simule un tick du monde : entrées et sorties demandées (ordre des joueurs), tous les secteurs ouverts (ordre des noms), passages
        /// commencés au tick précédent menés à terme, ouvertures pour le tick suivant, nouveaux passages (ordre du secteur de départ, puis du
        /// joueur), lâchers au bout de la marge, fermetures. Rend les événements entre secteurs ; ceux de chaque session (morts, populations)
        /// sont dans <paramref name="zoneEvents"/> et <paramref name="populationEvents"/> si donnés.
        /// </summary>
        public IReadOnlyList<SectorEvent> Step(List<(string Sector, ZoneEvent Event)>? zoneEvents = null, List<(string Sector, PopulationEvent Event)>? populationEvents = null)
        {
            var events = new List<SectorEvent>();
            // 0. Sorties, puis entrées, dans l'ordre des joueurs.
            // Revue Codex de la PR #150 (2e tour) : un joueur dont l'arrivée est programmée à ce tick (passage tout juste fait) sort aussi :
            // son arrivée a lieu au début du tick, son départ en fin du même tick ; il ne reste jamais un combattant sans suivi.
            foreach (string id in _exiting)
                if (_players.TryGetValue(id, out Player? p) && !p.Waiting && p.Sector is string s && _open.TryGetValue(s, out Open? o))
                {
                    CombatantView v = o.Zone.Engine.View(p.Combatant);
                    bool arriving = !v.IsPresent && !v.HasLeft && !v.IsDead && v.ArrivalTick == o.Zone.Engine.CurrentTick;
                    if ((v.IsPresent && !v.IsDead) || arriving) o.Zone.Leave(p.Combatant);
                }
            foreach (string id in _exiting) _players.Remove(id);
            _exiting.Clear();
            foreach (Player p in _players.Values.Where(x => x.Waiting).OrderBy(x => x.Id, StringComparer.Ordinal).ToList())
            {
                OpenSector(p.Sector!, events);
                if (Place(p, p.Sector!, p.Setup.Placement!.Position, p.Setup.Placement.Radius, p.Setup.Placement.Facing, null, p.Setup.Carried, null, events, announce: false))
                    p.Waiting = false;
            }
            foreach (CharacterSetup c in _entering.Values)
            {
                // Revue Codex de la PR #150 (2e tour) : première arrivée dans la région par la même voie qu'un passage : place libre la plus
                // proche (arrivées déjà programmées comprises), place et identité disponibles ; sinon le joueur attend et réessaie à chaque tick.
                string sector = _map.SectorOf(c.Placement!.Position);
                OpenSector(sector, events);
                var p = new Player { Id = c.Id, Setup = c, Sector = sector };   // un joueur peut entrer avec un état (reprise, autre région)
                _players[c.Id] = p;
                p.Waiting = !Place(p, sector, c.Placement.Position, c.Placement.Radius, c.Placement.Facing, null, c.Carried, null, events, announce: false);
            }
            _entering.Clear();

            // 1. Tous les secteurs ouverts, au même tick du monde.
            foreach (Open o in _open.Values)
            {
                if (o.Zone.WorldTick != WorldTick) throw new InvalidOperationException($"Secteur {o.Name} au tick {o.Zone.WorldTick}, monde au tick {WorldTick}.");
                foreach (ZoneEvent e in o.Zone.Step()) zoneEvents?.Add((o.Name, e));
                foreach (PopulationEvent e in o.Populations.Step(o.Zone)) populationEvents?.Add((o.Name, e));
            }
            WorldTick = checked(WorldTick + 1);

            // 1 bis. Revue Codex de la PR #150 : une arrivée refusée malgré tout (place prise au début du tick) : le joueur repart de son secteur
            // de départ (ou réessaie au tick suivant), avec le même état : il n'est jamais perdu.
            foreach (Player p in _players.Values.Where(x => x.Arrival != null).OrderBy(x => x.Id, StringComparer.Ordinal).ToList())
            {
                CombatantView v = _open[p.Sector!].Zone.Engine.View(p.Combatant);
                if (v.IsPresent || v.IsDead || v.HasLeft || v.ArrivalTick is not long due || due >= _open[p.Sector!].Zone.Engine.CurrentTick)
                {
                    if (v.IsPresent || v.IsDead || v.HasLeft) { p.Arrival = null; p.ArrivingFrom = null; }
                    continue;
                }
                CharacterSetup again = p.Arrival!;
                string back = p.ArrivingFrom != null && _open.ContainsKey(p.ArrivingFrom) ? p.ArrivingFrom : p.Sector!;
                if (!Place(p, back, again.Placement!.Position, again.Placement.Radius, again.Placement.Facing, again.Consumables, again.Carried, null, events))
                    Place(p, p.Sector!, again.Placement.Position, again.Placement.Radius, again.Placement.Facing, again.Consumables, again.Carried, null, events);
            }

            // 2. Passages commencés : départ effectif en fin du tick simulé, arrivée au tick suivant dans le voisin.
            foreach (Player p in _players.Values.Where(x => x.PendingTo != null).OrderBy(x => x.Sector, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal).ToList())
            {
                Open from = _open[p.Sector!];
                CombatantView left = from.Zone.Engine.View(p.Combatant);
                string to = p.PendingTo!;
                p.PendingTo = null;
                GroundVector? move = p.PendingMove;
                p.PendingMove = null;
                if (!left.HasLeft || left.IsDead) continue;   // mort pendant le tick : le départ est annulé (leaveCancelled)
                CarriedState carried = from.Zone.Engine.Carried(p.Combatant);
                IReadOnlyList<ConsumableSetup> consumables = from.Zone.Engine.CarriedConsumables(p.Combatant);
                OpenSector(to, events);
                // Relecture indépendante et revue Codex de la PR #150 : passage atomique. Places, identités et endroit libre ont été vérifiés avant
                // le départ ; si, entre-temps, l'arrivée est devenue impossible, le joueur revient dans son secteur de départ (son départ y a
                // libéré sa place), avec le même état ; sinon il réessaie au tick suivant.
                CarriedState? state = carried.IsEmpty ? null : carried;
                if (!Place(p, to, left.Position!.Value, left.Radius!.Value, left.Facing, consumables, state, move, events, from: from.Name)
                    && !Place(p, from.Name, left.Position.Value, left.Radius.Value, left.Facing, consumables, state, move, events, from: from.Name))
                {
                    p.Arrival = Arriving(p.Setup, p.Combatant, new CombatantPlacement(left.Position.Value, left.Radius.Value, left.Facing), 0, consumables, state);
                    p.ArrivingFrom = from.Name;
                }
            }

            // 3. Ouvertures : chaque secteur à moins de 96 m d'un joueur, pour le tick suivant.
            foreach (Player p in _players.Values)
            {
                if (p.Waiting || p.Sector is null || !_open.TryGetValue(p.Sector, out Open? o)) continue;
                CombatantView v = o.Zone.Engine.View(p.Combatant);
                if (v.Position is GroundVector pos)
                    foreach (string near in _map.Near(pos)) OpenSector(near, events);
            }

            // 4. Nouveaux passages et lâchers au bout de la marge (ordre du secteur, puis du joueur).
            foreach (Player p in _players.Values.OrderBy(x => x.Sector, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal))
            {
                if (p.Waiting || p.Sector is null || !_open.TryGetValue(p.Sector, out Open? o)) continue;
                CombatantView v = o.Zone.Engine.View(p.Combatant);
                if (!v.IsPresent || v.IsDead || v.Position is not GroundVector pos || o.Zone.IsOver) continue;
                if (_map.Overshoot(p.Sector, pos) <= checked(SectorMap.HysteresisMeters * Fixed.Scale)) continue;
                string to = _map.SectorOf(pos);
                if (to == p.Sector || _map.Space(to) is null) continue;
                if (InEncounter(o.Zone, p.Combatant))
                {
                    if (!AtMarginEnd(o, pos, v.Radius!.Value)) continue;
                    foreach (Encounter e in o.Zone.Encounters().Where(e => e.Characters.Contains(p.Combatant)))
                        foreach (string creature in e.Creatures)
                        {
                            bool gone;
                            if (o.Populations.Release(o.Zone, creature) is PopulationEvent released)
                            {
                                populationEvents?.Add((o.Name, released));
                                gone = true;
                            }
                            else gone = Leave(o.Zone, creature);   // créature hors population : elle s'en va simplement
                            if (gone) events.Add(new SectorEvent(SectorEventKind.Released, o.Name, creature, p.Id, WorldTick));
                        }
                    continue;   // il passera au prochain tick sans rencontre
                }
                // Relecture indépendante de la PR #150 : pas de départ sans place libre (places et endroit) dans le secteur d'arrivée ; le joueur
                // reste où il est (au pire au bout de la marge) et réessaie au tick suivant.
                if (!_open.TryGetValue(to, out Open? next) || next.Zone.IsOver || next.Zone.Engine.FreePlaces == 0 || next.Zone.Engine.IdentitiesLeft == 0
                    || FreeSpot(next, pos, v.Radius!.Value) is null) continue;
                p.PendingMove = v.MoveDirection;
                p.PendingBackward = v.MoveBackward;
                o.Zone.Leave(p.Combatant);
                p.PendingTo = to;
                events.Add(new SectorEvent(SectorEventKind.Departed, p.Sector, p.Id, to, WorldTick));
            }

            // 5. Fermetures : 60 s sans joueur à moins de 96 m (et aucun joueur dedans).
            foreach (Open o in _open.Values.ToList())
            {
                bool near = _players.Values.Any(p => p.Sector == o.Name || p.PendingTo == o.Name
                    || (!p.Waiting && p.Sector != null && _open.TryGetValue(p.Sector, out Open? po) && po.Zone.Engine.View(p.Combatant).Position is GroundVector pos
                        && _map.DistanceTo(o.Name, pos) <= checked(SectorMap.ActivationMeters * Fixed.Scale)));
                o.IdleTicks = near ? 0 : checked(o.IdleTicks + 1);
                if (o.IdleTicks >= _closeAfterTicks) CloseSector(o, events);
            }
            return events;
        }

        /// <summary>
        /// Au bout de la marge : le centre du joueur est à moins de son emprise plus 0,5 m d'un bord de l'espace de son secteur qui n'est pas un
        /// bord de l'aire (au-delà, un autre secteur).
        /// </summary>
        private bool AtMarginEnd(Open o, GroundVector pos, Fixed radius)
        {
            CombatSpace s = o.Zone.Setup.Space!, a = _map.Area.Space!;
            long reach = checked(radius.Raw + Fixed.Scale / 2);
            return (s.Min.X > a.Min.X && pos.X.Raw - s.Min.X.Raw <= reach) || (s.Max.X < a.Max.X && s.Max.X.Raw - pos.X.Raw <= reach)
                || (s.Min.Z > a.Min.Z && pos.Z.Raw - s.Min.Z.Raw <= reach) || (s.Max.Z < a.Max.Z && s.Max.Z.Raw - pos.Z.Raw <= reach);
        }
    }
}
