using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Saves;
using EdS.Data.WorldBosses;
using EdS.Domain.Space;

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

        /// <summary>
        /// Met en jeu les boss du monde de <paramref name="book"/> (validé contre la zone de cette partie : ses emplacements et ses cibles
        /// sont dans son aire). Une seule fois par partie.
        /// </summary>
        /// <exception cref="InvalidOperationException">Boss du monde déjà en jeu, ou partie sans définition de zone.</exception>
        public void UseWorldBosses(WorldBossBook book)
        {
            if (book is null) throw new ArgumentNullException(nameof(book));
            if (_worldBosses != null) throw new InvalidOperationException("Boss du monde déjà en jeu.");
            if (Definition is null) throw new InvalidOperationException("Partie sans définition de zone : aucun boss du monde.");
            _worldBosses = book;
            _worldBossEvents = new WorldBossEvent?[book.Bosses.Count];
        }

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
            int i = WorldBossIndex(boss);
            return _worldBossEvents[i];
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
                e.Advance(now, players);
                Noticed(e, before, destroyed, ref notices);
                if (e.Outcome != WorldBossOutcome.None && memory.LastWindow < e.Window) SettleWorldBoss(b, key, e, memory, ref notices);
                if (e.Stage == WorldBossStage.Gone || e.Stage == WorldBossStage.Defeated) _worldBossEvents[i] = null;
            }
            _worldBossNotices = (IReadOnlyList<WorldBossNotice>?)notices ?? Array.Empty<WorldBossNotice>();
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
        /// Issue de l'occurrence réglée une seule fois : mémoire du vainqueur dans le registre (si le registre a la place) ; victoire des
        /// joueurs avec le personnage parmi les participants : butin (dû si le sac est plein).
        /// </summary>
        private void SettleWorldBoss(WorldBossDefinition b, string key, WorldBossEvent e, WorldBossMemory memory, ref List<WorldBossNotice>? notices)
        {
            bool has = _character.Events.Any(x => x.Id == key);
            if (!has && _character.Events.Count >= SavedEvent.MaxEntries) return;   // registre plein : l'issue n'est pas gardée (limite connue)
            WorldBossMemory after = memory.After(e.Window, e.Outcome);
            bool earned = e.Outcome == WorldBossOutcome.PlayersWon && e.Contributions.Any(c => c.Key == CombatantId && c.Value > 0);
            SavedEvent saved = after.ToEvent(key);
            if (earned) saved = new SavedEvent(key, saved.Won, saved.Lost, saved.Last, saved.Outcome, owed: true);
            _character = _character.WithEvents(_character.Events.Where(x => x.Id != key).Concat(new[] { saved }));
            if (earned) GiveWorldBossSpoils(b, key, saved, ref notices);
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
