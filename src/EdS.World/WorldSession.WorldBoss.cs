using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Saves;
using EdS.Data.WorldBosses;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;

namespace EdS.World
{
    /// <summary>Grand Solitaire (2e partie) : ce qui vient d'arriver à un boss du monde pendant un pas de la partie.</summary>
    public enum WorldBossNoticeKind
    {
        /// <summary>Les signes commencent (sol, puis ciel, puis animaux) : il arrive dans <see cref="WorldBossDefinition.SignsMs"/>.</summary>
        Signs = 0,

        /// <summary>Il est apparu physiquement (le compte à rebours commence).</summary>
        Appeared = 1,

        /// <summary>Sa cible villageoise est annoncée.</summary>
        TargetAnnounced = 2,

        /// <summary>Le compte à rebours est fini : assaut, il marche vers sa cible.</summary>
        Assault = 3,

        /// <summary>Un objectif de sa cible est détruit.</summary>
        ObjectiveDestroyed = 4,

        /// <summary>Les joueurs l'ont vaincu.</summary>
        Defeated = 5,

        /// <summary>Il a réussi son assaut (mémoire du vainqueur : il reviendra enragé).</summary>
        Victorious = 6,

        /// <summary>Il est reparti (après sa victoire) : l'occurrence est finie.</summary>
        Gone = 7,

        /// <summary>Le butin de la victoire est donné au personnage.</summary>
        Rewarded = 8,

        /// <summary>Sa forme entre dans le moteur : le combat commence (ou reprend), dans la phase du moment.</summary>
        Engaged = 9,

        /// <summary>Changement de phase : la forme de la phase suivante prend la place de la précédente, avec la vie restante.</summary>
        PhaseChanged = 10,

        /// <summary>Il se dégage du combat (personnage loin, absent ou vaincu ; assaut) et reprend sa marche.</summary>
        Disengaged = 11,
    }

    public sealed class WorldBossNotice
    {
        public WorldBossNoticeKind Kind { get; }
        public string Boss { get; }
        public long Window { get; }

        /// <summary>Objets donnés (<see cref="WorldBossNoticeKind.Rewarded"/>), vide sinon.</summary>
        public IReadOnlyList<string> Items { get; }

        internal WorldBossNotice(WorldBossNoticeKind kind, string boss, long window, IReadOnlyList<string>? items = null)
        {
            Kind = kind;
            Boss = boss;
            Window = window;
            Items = items ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Grand Solitaire (2e partie, décisions de Damien du 15 novembre 2026) : les boss du monde dans la partie solo. Le temps serveur est
    /// celui que fournit l'hôte ou le serveur (<see cref="SetCivilTime"/>, projeté par les ticks du monde depuis), jamais une horloge lue par
    /// le cœur ; sans date fournie, aucun boss du monde (option prudente). Fenêtres de 6 h, une chance sur deux (données) ; mode solo : sa vie
    /// compte pour un seul joueur tant que le monde n'est pas partagé ; mémoire du vainqueur dans une ligne du registre des événements de la
    /// sauvegarde (aucun format ne change) ; butin de la victoire (trophée et apparence à la première, en attendant la quête de l'apparence),
    /// dû et redonné au pas suivant si le sac est plein.
    /// </summary>
    public sealed partial class WorldSession
    {
        private WorldBossBook? _worldBosses;
        private WorldBossEvent?[] _worldBossEvents = Array.Empty<WorldBossEvent?>();
        private IReadOnlyList<WorldBossNotice> _worldBossNotices = Array.Empty<WorldBossNotice>();

        /// <summary>Forme de chaque boss dans le moteur : identité, session, vie lue au dernier pas, phase jouée ; null hors combat.</summary>
        private sealed class BossFormSlot
        {
            public string Id = "";
            public Zone Session = null!;
            public long Health;
            public int Phase;

            /// <summary>Changement de phase en cours : l'ancienne forme part à ce tick, la suivante arrive au pas suivant à cette place.</summary>
            public CombatantPlacement? Swap;
        }

        private BossFormSlot?[] _worldBossForms = Array.Empty<BossFormSlot?>();
        private bool[] _worldBossUnsaved = Array.Empty<bool>();

        /// <summary>
        /// Relecture indépendante de la PR #178 : entrées de formes de boss du monde au plus par session de zone (engagement, changement de
        /// phase) : chaque entrée prend une identité de combattant à la session, bornée par le moteur ; au-delà, le boss ne s'y engage plus
        /// (il le fera dans la session suivante). Il ne s'engage pas non plus s'il ne reste pas assez d'identités pour les places réservées
        /// (chefs, événements) et la réserve des quêtes.
        /// </summary>
        public const int MaxWorldBossJoins = 16;

        private Zone? _worldBossJoinZone;
        private int _worldBossJoinCount;

        /// <summary>Pour les essais : entrées de formes déjà faites dans la session du personnage.</summary>
        internal void SetWorldBossJoinsForTest(int joins)
        {
            _worldBossJoinZone = Zone;
            _worldBossJoinCount = joins;
        }
        private readonly HashSet<string> _worldBossFormIds = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Distance du personnage au boss sous laquelle sa forme entre dans le moteur (celle du point d'émergence, 30 m).</summary>
        public static Fixed EngageDistance(WorldBossDefinition boss) => boss.Clearance;

        /// <summary>Distance au-delà de laquelle il se dégage (le double).</summary>
        public static Fixed LeashDistance(WorldBossDefinition boss) => Fixed.FromRaw(checked(boss.Clearance.Raw * 2));

        /// <summary>Le combattant est une forme d'un boss du monde (ni expérience ni butin de créature à sa mort : son butin est celui de l'événement).</summary>
        public bool IsWorldBossForm(string combatant) => _worldBossFormIds.Contains(combatant);

        /// <summary>Identité de la forme du boss <paramref name="boss"/> dans le moteur, ou null hors combat.</summary>
        public string? WorldBossForm(string boss) => _worldBossForms[WorldBossIndex(boss)]?.Id;

        /// <summary>
        /// Met en jeu les boss du monde de <paramref name="book"/> (validé contre la zone de cette partie : ses emplacements et ses cibles
        /// sont dans son aire). Une seule fois par partie.
        /// </summary>
        /// <exception cref="InvalidOperationException">Boss du monde déjà en jeu, ou partie sans définition de zone.</exception>
        public void UseWorldBosses(WorldBossBook book) => UseWorldBosses(book, null);

        /// <summary>
        /// Comme <see cref="UseWorldBosses(WorldBossBook)"/>, en reprenant les occurrences en cours <paramref name="carried"/> d'une session
        /// précédente de la même partie (<see cref="WorldBossEvents"/> : passage, porte, halte, même livre) : elles continuent telles quelles
        /// (vie, participation, place), dégagées du moteur (leur forme est restée dans l'ancienne session).
        /// </summary>
        /// <exception cref="ArgumentException">Occurrences d'un autre nombre de boss que le livre.</exception>
        public void UseWorldBosses(WorldBossBook book, IReadOnlyList<WorldBossEvent?>? carried)
        {
            if (book is null) throw new ArgumentNullException(nameof(book));
            if (_worldBosses != null) throw new InvalidOperationException("Boss du monde déjà en jeu.");
            if (Definition is null) throw new InvalidOperationException("Partie sans définition de zone : aucun boss du monde.");
            if (carried != null && (carried.Count != book.Bosses.Count || carried.Where((e, k) => e != null && !ReferenceEquals(e.Boss, book.Bosses[k])).Any()))
                throw new ArgumentException("Occurrences d'un autre livre de boss du monde.", nameof(carried));
            _worldBosses = book;
            _worldBossEvents = new WorldBossEvent?[book.Bosses.Count];
            _worldBossForms = new BossFormSlot?[book.Bosses.Count];
            _worldBossUnsaved = new bool[book.Bosses.Count];
            for (int k = 0; carried != null && k < carried.Count; k++)
            {
                carried[k]?.Disengage();
                _worldBossEvents[k] = carried[k];
            }
        }

        /// <summary>Occurrences en cours de chaque boss (dans l'ordre du livre), à reprendre dans la session suivante de la partie.</summary>
        public IReadOnlyList<WorldBossEvent?> WorldBossEvents => _worldBossEvents;

        /// <summary>Boss du monde en jeu, ou null.</summary>
        public WorldBossBook? WorldBosses => _worldBosses;

        /// <summary>
        /// Temps serveur du moment (ms) : l'instant fourni par l'hôte, projeté par les ticks du monde depuis ; null sans date fournie.
        /// </summary>
        public long? ServerMs => CivilUtcSeconds is long utc
            ? checked(utc * 1000 + (Zone.WorldTick - (_serverUtc == utc ? _serverTick : Zone.WorldTick)) * Zone.Clock.TickMilliseconds)
            : (long?)null;

        // Instant fourni par l'hôte vu au dernier pas des boss du monde, et le tick du monde où il l'a été (ancre de la projection).
        private long? _serverUtc;
        private long _serverTick;

        /// <summary>Occurrence en cours du boss du monde <paramref name="boss"/> (des signes à son départ), ou null.</summary>
        /// <exception cref="ArgumentException">Boss inconnu.</exception>
        public WorldBossEvent? WorldBossNow(string boss)
        {
            WorldBossEvent? e = _worldBossEvents[WorldBossIndex(boss)];
            // Finie (vaincu ou reparti), elle reste gardée jusqu'à la fin de sa fenêtre (sans revenir), mais n'est plus en cours.
            return e != null && (e.Stage == WorldBossStage.Gone || e.Stage == WorldBossStage.Defeated) ? null : e;
        }

        /// <summary>Ce qui est arrivé aux boss du monde au dernier pas.</summary>
        public IReadOnlyList<WorldBossNotice> WorldBossNotices => _worldBossNotices;

        /// <summary>Clé de la ligne du registre des événements qui garde la mémoire du boss (« zone.…/worldboss.… »).</summary>
        public string WorldBossKey(string boss) => $"{Definition!.Id}/worldboss.{_worldBosses!.Bosses[WorldBossIndex(boss)].Id}";

        /// <summary>Mémoire du vainqueur du boss <paramref name="boss"/>, lue du registre (aucune : <see cref="WorldBossMemory.None"/>).</summary>
        public WorldBossMemory WorldBossMemoryOf(string boss)
        {
            string key = WorldBossKey(boss);
            return WorldBossMemory.FromEvent(_character.Events.FirstOrDefault(e => e.Id == key));
        }

        private int WorldBossIndex(string boss)
        {
            if (boss is null) throw new ArgumentNullException(nameof(boss));
            if (_worldBosses is null) throw new ArgumentException("Aucun boss du monde en jeu.", nameof(boss));
            for (int i = 0; i < _worldBosses.Bosses.Count; i++) if (_worldBosses.Bosses[i].Id == boss) return i;
            throw new ArgumentException($"Boss du monde « {boss} » inconnu.", nameof(boss));
        }

        /// <summary>Places des joueurs (le personnage, s'il est là et vivant).</summary>
        private IReadOnlyList<GroundVector> WorldBossPlayers()
        {
            var me = Zone.Engine.View(CombatantId);
            return me.IsPresent && !me.IsDead && me.Position is GroundVector p ? new[] { p } : Array.Empty<GroundVector>();
        }

        /// <summary>Un pas des boss du monde (après les événements du monde). Ne lève pas pour une raison de jeu.</summary>
        private void StepWorldBosses()
        {
            if (CivilUtcSeconds is long utc && _serverUtc != utc)
            {
                _serverUtc = utc;
                _serverTick = Zone.WorldTick;
            }
            if (_worldBosses is null || !(ServerMs is long now))
            {
                _worldBossNotices = Array.Empty<WorldBossNotice>();
                return;
            }
            List<WorldBossNotice>? notices = null;
            IReadOnlyList<GroundVector>? players = null;
            for (int i = 0; i < _worldBosses.Bosses.Count; i++)
            {
                WorldBossDefinition b = _worldBosses.Bosses[i];
                string key = WorldBossKey(b.Id);
                SavedEvent? entry = _character.Events.FirstOrDefault(e => e.Id == key);
                WorldBossMemory memory = WorldBossMemory.FromEvent(entry);
                // Butin dû (sac plein à la victoire) : redonné dès qu'il y a la place.
                if (entry != null && entry.Owed) GiveWorldBossSpoils(b, key, entry, ref notices);
                WorldBossEvent? e = _worldBossEvents[i];
                if (e != null && e.Outcome != WorldBossOutcome.None && (e.Stage == WorldBossStage.Gone || e.Stage == WorldBossStage.Defeated))
                {
                    // Occurrence finie : son issue est réglée une seule fois (relecture indépendante de la PR #178 : un butin dû d'avant n'est
                    // jamais écrasé, le règlement attend qu'il soit donné) ; elle reste là, sans revenir, jusqu'à la fin de sa fenêtre, même si
                    // le registre plein n'a pas pu garder son issue.
                    if (memory.LastWindow < e.Window && !_worldBossUnsaved[i] && SettleWorldBoss(b, key, e, ref notices) == false) continue;
                    if (WorldBossSchedule.WindowOf(b, now) <= e.Window) continue;
                    _worldBossEvents[i] = null;   // sa fenêtre est passée : l'occurrence suivante peut venir dès ce pas
                    _worldBossUnsaved[i] = false;
                    e = null;
                    memory = WorldBossMemory.FromEvent(_character.Events.FirstOrDefault(x => x.Id == key));
                }
                if (e is null)
                {
                    long window = WorldBossSchedule.WindowOf(b, now);
                    if (memory.LastWindow >= window || !(WorldBossSchedule.InWindow(b, window) is WorldBossOccurrence occ)) continue;
                    if (now < occ.SignsStartMs || now >= checked(occ.SignsStartMs + WorldBossSchedule.LongestMs(b))) continue;
                    players ??= WorldBossPlayers();
                    e = new WorldBossEvent(b, memory, occ, players, shared: false);
                    e.SetPlayers(1);
                    _worldBossEvents[i] = e;
                    (notices ??= new List<WorldBossNotice>()).Add(new WorldBossNotice(WorldBossNoticeKind.Signs, b.Id, e.Window));
                }
                // Le temps serveur ne recule jamais pour l'événement : une horloge reculée par l'hôte le laisse en attente.
                if (now < e.NowMs) continue;
                WorldBossStage before = e.Stage;
                int destroyed = e.ObjectivesDestroyed;
                players ??= WorldBossPlayers();
                // Grand Solitaire, 2e partie : le combat dans le moteur d'abord (dégâts du tick, place de la forme), puis le temps serveur.
                FightWorldBoss(i, e, ref notices);
                e.Advance(now, players);
                if (e.Stage >= WorldBossStage.Assault && _worldBossForms[i] != null) Release(i, e, ref notices);
                Noticed(e, before, destroyed, ref notices);
                if ((e.Stage == WorldBossStage.Gone || e.Stage == WorldBossStage.Defeated) && _worldBossForms[i] != null) Release(i, e, ref notices);
                if (e.Outcome != WorldBossOutcome.None && memory.LastWindow < e.Window) SettleWorldBoss(b, key, e, ref notices);
            }
            _worldBossNotices = (IReadOnlyList<WorldBossNotice>?)notices ?? Array.Empty<WorldBossNotice>();
        }

        /// <summary>
        /// Combat dans le moteur (boss à formes, schéma 2) : les dégâts que sa forme a reçus depuis le dernier pas comptent pour l'événement
        /// (participation du personnage) ; sa place suit la forme ; à un changement de phase, la forme suivante prend sa place avec la vie
        /// restante ; il se dégage si le personnage est loin, absent ou vaincu, ou si la session de la forme n'est plus celle du personnage ;
        /// il s'engage quand le personnage s'approche (avant l'assaut). Une arrivée refusée par le moteur (place prise) est retentée au pas suivant.
        /// </summary>
        private void FightWorldBoss(int i, WorldBossEvent e, ref List<WorldBossNotice>? notices)
        {
            WorldBossDefinition b = e.Boss;
            if (!b.Fightable) return;
            CombatantView me = Zone.Engine.View(CombatantId);
            bool heroHere = me.IsPresent && !me.IsDead && me.Position is GroundVector;
            if (_worldBossForms[i] is BossFormSlot f)
            {
                if (!ReferenceEquals(f.Session, Zone) || f.Session.IsOver)
                {
                    // Session remplacée ou finie (passage, renouvellement) : la forme est partie avec elle ; il reprend sa marche d'où il était.
                    _worldBossForms[i] = null;
                    e.Disengage();
                    Add(ref notices, WorldBossNoticeKind.Disengaged, e);
                    return;
                }
                if (f.Swap is CombatantPlacement swap)
                {
                    _worldBossForms[i] = null;
                    if (e.IsFightable && JoinForm(i, e, swap)) Add(ref notices, WorldBossNoticeKind.PhaseChanged, e);
                    else { e.Disengage(); Add(ref notices, WorldBossNoticeKind.Disengaged, e); }
                    return;
                }
                CombatantView v = f.Session.Engine.View(f.Id);
                if (!v.IsPresent && !v.IsDead && !v.HasLeft)
                {
                    // Arrivée à venir, ou refusée par le moteur au moment d'arriver (place prise) : on attend, ou l'on se dégage et réessaie.
                    if (v.ArrivalTick is long due && due >= f.Session.Engine.CurrentTick) return;
                    _worldBossForms[i] = null;
                    e.Disengage();
                    Add(ref notices, WorldBossNoticeKind.Disengaged, e);
                    return;
                }
                if (v.Health is long hp && hp < f.Health && e.IsFightable) e.Damage(CombatantId, f.Health - hp);
                if (v.Health is long now) f.Health = now;
                if (v.Position is GroundVector at && e.Engaged) e.Follow(at);
                if (!e.IsFightable) return;   // vaincu : la forme quitte le moteur après le pas du temps serveur
                bool far = !heroHere || !(v.Position is GroundVector p) || !Near(me.Position!.Value, p, LeashDistance(b));
                if (v.IsDead || v.HasLeft || far)
                {
                    Release(i, e, ref notices);
                    return;
                }
                if (e.Phase != f.Phase)
                {
                    // Changement de phase : la forme suivante, à la même place, avec la vie restante.
                    CombatantPlacement place = new CombatantPlacement(v.Position!.Value, v.Radius ?? b.Radius, v.Facing);
                    // Le départ prend effet en fin de tick : la forme suivante entre au pas suivant, à la place libérée.
                    f.Session.Leave(f.Id);
                    f.Swap = place;
                }
                return;
            }
            if (!e.IsFightable || e.Stage >= WorldBossStage.Assault || !heroHere || Zone.IsOver) return;
            if (_host != null && _host.Map.OwnerOf(e.Position) != _zoneSector) return;   // sur l'hôte des secteurs : dans le secteur du personnage
            if (!Near(me.Position!.Value, e.Position, EngageDistance(b))) return;
            // Place libre la plus proche de la sienne (règle d'arrivée : sa marche en ligne droite peut longer un obstacle).
            if (!(SectorWorld.FreeSpotOf(Zone, e.Position, b.Radius, character: false) is GroundVector free)) return;
            var spot = new CombatantPlacement(free, b.Radius, Facing(free, me.Position!.Value));
            if (!JoinForm(i, e, spot)) return;
            e.Engage();
            e.Follow(free);
            Add(ref notices, WorldBossNoticeKind.Engaged, e);
        }

        /// <summary>Fait entrer la forme de la phase du moment, vie maximale et restante de l'événement ; faux si le moteur la refuse (rien ne change).</summary>
        private bool JoinForm(int i, WorldBossEvent e, CombatantPlacement place)
        {
            if (!ReferenceEquals(_worldBossJoinZone, Zone))
            {
                _worldBossJoinZone = Zone;
                _worldBossJoinCount = 0;
            }
            if (_worldBossJoinCount >= MaxWorldBossJoins
                || Zone.Engine.IdentitiesLeft <= checked(ReservedPlaces + ZonePopulations.QuestReserve + 1)) return false;
            AssailantSetup t = e.PhaseNow.Form!;
            string id = t.Id;
            for (int n = 2; Zone.Engine.CombatantIds.Contains(id); n++) id = $"{t.Id}_v{n}";
            var form = new AssailantSetup(id, t.Team, t.Armor, t.MagicResistance, null, t.FirstAttackMs, t.IntervalMs, t.Attacks, t.Initiative, t.StatusResistance,
                t.Triggers, e.MaxHealth, place, checked(Zone.Engine.CurrentTick * Zone.Engine.TickMilliseconds), t.Behavior,
                e.Health < e.MaxHealth ? new CarriedState(health: e.Health) : null);
            try
            {
                Zone.Engine.Join(form);
            }
            catch (Exception x) when (x is ArgumentException || x is InvalidOperationException)
            {
                return false;   // place prise, file pleine : refus atomique, nouvel essai au pas suivant
            }
            Zone.Transient(id);   // aucune clé de mort au registre : l'issue est celle de l'événement
            _worldBossFormIds.Add(id);
            _worldBossJoinCount++;
            _worldBossForms[i] = new BossFormSlot { Id = id, Session = Zone, Health = e.Health, Phase = e.Phase };
            return true;
        }

        /// <summary>La forme quitte le moteur (si elle y est encore) et le boss reprend sa marche.</summary>
        private void Release(int i, WorldBossEvent e, ref List<WorldBossNotice>? notices)
        {
            if (!(_worldBossForms[i] is BossFormSlot f)) return;
            _worldBossForms[i] = null;
            if (f.Swap is null && ReferenceEquals(f.Session, Zone) && !f.Session.IsOver)
            {
                CombatantView v = f.Session.Engine.View(f.Id);
                if (v.IsPresent && !v.IsDead) f.Session.Leave(f.Id);
            }
            bool was = e.Engaged;
            e.Disengage();
            if (was || e.IsFightable) Add(ref notices, WorldBossNoticeKind.Disengaged, e);
        }

        private static void Add(ref List<WorldBossNotice>? notices, WorldBossNoticeKind kind, WorldBossEvent e) =>
            (notices ??= new List<WorldBossNotice>()).Add(new WorldBossNotice(kind, e.Boss.Id, e.Window));

        private static bool Near(GroundVector a, GroundVector b, Fixed distance) =>
            Planar.LengthSquared(checked(a.X.Raw - b.X.Raw), checked(a.Z.Raw - b.Z.Raw)) <= checked(distance.Raw * distance.Raw);

        private static GroundVector Facing(GroundVector from, GroundVector to)
        {
            long dx = checked(to.X.Raw - from.X.Raw), dz = checked(to.Z.Raw - from.Z.Raw);
            return Planar.TryNormalize(dx, dz, out long ax, out long az) ? GroundVector.FromRaw(ax, az) : new GroundVector(Fixed.One, Fixed.Zero);
        }

        private static void Noticed(WorldBossEvent e, WorldBossStage before, int destroyed, ref List<WorldBossNotice>? notices)
        {
            string id = e.Boss.Id;
            var list = notices ??= new List<WorldBossNotice>();
            void Add(WorldBossNoticeKind k) => list.Add(new WorldBossNotice(k, id, e.Window));
            // Dans l'ordre des étapes (un seul pas peut en franchir plusieurs, par exemple après une longue absence).
            if (before < WorldBossStage.Roaming && e.Stage >= WorldBossStage.Roaming && e.Stage != WorldBossStage.Defeated) Add(WorldBossNoticeKind.Appeared);
            if (before < WorldBossStage.Announced && e.Target != null) Add(WorldBossNoticeKind.TargetAnnounced);
            if (before < WorldBossStage.Assault && e.Stage >= WorldBossStage.Assault && e.Stage != WorldBossStage.Defeated) Add(WorldBossNoticeKind.Assault);
            for (int k = destroyed; k < e.ObjectivesDestroyed; k++) Add(WorldBossNoticeKind.ObjectiveDestroyed);
            if (before != WorldBossStage.Defeated && e.Stage == WorldBossStage.Defeated) Add(WorldBossNoticeKind.Defeated);
            if (before < WorldBossStage.Victorious && (e.Stage == WorldBossStage.Victorious || e.Stage == WorldBossStage.Gone)) Add(WorldBossNoticeKind.Victorious);
            if (before != WorldBossStage.Gone && e.Stage == WorldBossStage.Gone) Add(WorldBossNoticeKind.Gone);
            if (list.Count == 0) notices = null;
        }

        /// <summary>
        /// Issue de l'occurrence réglée une seule fois : mémoire du vainqueur dans le registre ; victoire des joueurs avec le personnage parmi
        /// les participants : butin (dû si le sac est plein). Rend vrai si c'est réglé ; faux si un butin dû d'une victoire d'avant attend
        /// encore sa place (relecture indépendante de la PR #178 : il n'est jamais écrasé ; le règlement est retenté au pas suivant) ; null si
        /// le registre est plein (l'issue n'est pas gardée ; l'occurrence ne revient pas dans sa fenêtre).
        /// </summary>
        private bool? SettleWorldBoss(WorldBossDefinition b, string key, WorldBossEvent e, ref List<WorldBossNotice>? notices)
        {
            SavedEvent? entry = _character.Events.FirstOrDefault(x => x.Id == key);
            if (entry != null && entry.Owed) return false;
            if (entry is null && _character.Events.Count >= SavedEvent.MaxEntries)
            {
                _worldBossUnsaved[Array.IndexOf(_worldBossEvents, e)] = true;
                return null;
            }
            WorldBossMemory after = WorldBossMemory.FromEvent(entry).After(e.Window, e.Outcome);
            bool earned = e.Outcome == WorldBossOutcome.PlayersWon && e.Contributions.Any(c => c.Key == CombatantId && c.Value > 0);
            SavedEvent saved = after.ToEvent(key);
            if (earned) saved = new SavedEvent(key, saved.Won, saved.Lost, saved.Last, saved.Outcome, owed: true);
            _character = _character.WithEvents(_character.Events.Where(x => x.Id != key).Concat(new[] { saved }));
            if (earned) GiveWorldBossSpoils(b, key, saved, ref notices);
            return true;
        }

        /// <summary>
        /// Donne le butin dû de la dernière victoire (recalculé tel quel : fenêtre, personnage, victoires d'avant) ; tout ou rien : si un
        /// objet ne rentre pas, rien n'est donné et le butin reste dû.
        /// </summary>
        private void GiveWorldBossSpoils(WorldBossDefinition b, string key, SavedEvent entry, ref List<WorldBossNotice>? notices)
        {
            WorldBossAward award = WorldBossSpoils.For(b, entry.Last, CombatantId, checked(entry.Won - 1));
            var given = new List<string>();
            foreach (string item in award.Items)
            {
                if (Inventory.Add(item, 1) == InventoryRefusal.None) { given.Add(item); continue; }
                foreach (string back in given) Inventory.Remove(back, 1);
                return;
            }
            var paid = new SavedEvent(key, entry.Won, entry.Lost, entry.Last, entry.Outcome);
            _character = _character.WithEvents(_character.Events.Where(x => x.Id != key).Concat(new[] { paid }));
            (notices ??= new List<WorldBossNotice>()).Add(new WorldBossNotice(WorldBossNoticeKind.Rewarded, b.Id, entry.Last, given));
        }
    }
}
