using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Saves;
using EdS.Data.WorldBosses;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Randomness;

namespace EdS.World
{
    /// <summary>Grand Solitaire : étape de l'événement.</summary>
    public enum WorldBossStage
    {
        /// <summary>Signes avant l'apparition (sol, ciel, animaux) ; le boss n'est pas encore là.</summary>
        Signs = 0,

        /// <summary>Il erre d'un emplacement à l'autre ; le compte à rebours court depuis l'apparition.</summary>
        Roaming = 1,

        /// <summary>La cible villageoise est annoncée (à 50 minutes) ; il erre encore.</summary>
        Announced = 2,

        /// <summary>Assaut (à 60 minutes) : il rejoint sa cible à pied, puis charge ses objectifs l'un après l'autre.</summary>
        Assault = 3,

        /// <summary>Les joueurs l'ont vaincu.</summary>
        Defeated = 4,

        /// <summary>Il a réussi son assaut ; il quitte le lieu par sa route.</summary>
        Victorious = 5,

        /// <summary>Parti (après sa victoire) : l'événement est clos.</summary>
        Gone = 6,
    }

    /// <summary>Grand Solitaire : signe d'arrivée du moment (dossier, p. 16).</summary>
    public enum WorldBossSign
    {
        None = 0,

        /// <summary>De T−180 à T−120 s : la terre réagit.</summary>
        Ground = 1,

        /// <summary>De T−120 à T−45 s : le ciel se refroidit.</summary>
        Sky = 2,

        /// <summary>De T−45 à 0 s : les animaux quittent la route.</summary>
        Animals = 3,
    }

    public enum WorldBossOutcome
    {
        None = 0,
        PlayersWon = 1,
        BossWon = 2,
    }

    /// <summary>Grand Solitaire : ce qui compte dans la participation (dégâts, soin, protection, objectifs ; pas le dernier coup seul).</summary>
    public enum WorldBossContribution
    {
        Damage = 0,
        Healing = 1,
        Protection = 2,
        Objective = 3,
    }

    /// <summary>
    /// Grand Solitaire : mémoire du vainqueur, gardée d'une apparition à l'autre. <see cref="Raged"/> : il a réussi son dernier assaut ; à la
    /// prochaine apparition, il commence enragé à 100 % de vie, sans bonus de dégâts permanent. Une victoire des joueurs clôt ce cycle.
    /// En solo, elle tient dans une ligne du registre des événements (<see cref="ToEvent"/>, <see cref="FromEvent"/>) : réussites des
    /// joueurs, victoires du boss, dernière occurrence (numéro de fenêtre) et son issue ; aucun format ne change.
    /// </summary>
    public readonly struct WorldBossMemory : IEquatable<WorldBossMemory>
    {
        public long PlayerWins { get; }
        public long BossWins { get; }

        /// <summary>Fenêtre de la dernière occurrence réglée, -1 si aucune.</summary>
        public long LastWindow { get; }

        public WorldBossOutcome LastOutcome { get; }

        public bool Raged => LastOutcome == WorldBossOutcome.BossWon;

        public WorldBossMemory(long playerWins, long bossWins, long lastWindow, WorldBossOutcome lastOutcome)
        {
            if (playerWins < 0 || playerWins > SavedEvent.MaxCount) throw new ArgumentOutOfRangeException(nameof(playerWins));
            if (bossWins < 0 || bossWins > SavedEvent.MaxCount) throw new ArgumentOutOfRangeException(nameof(bossWins));
            if (lastWindow < -1 || lastWindow > SavedEvent.MaxDay) throw new ArgumentOutOfRangeException(nameof(lastWindow));
            if (lastOutcome != WorldBossOutcome.None && lastWindow < 0) throw new ArgumentException("Une issue sans occurrence.", nameof(lastOutcome));
            PlayerWins = playerWins;
            BossWins = bossWins;
            LastWindow = lastWindow;
            LastOutcome = lastOutcome;
        }

        public static WorldBossMemory None => new WorldBossMemory(0, 0, -1, WorldBossOutcome.None);

        /// <summary>Mémoire après l'issue <paramref name="outcome"/> de l'occurrence de la fenêtre <paramref name="window"/> (réglée une seule fois).</summary>
        public WorldBossMemory After(long window, WorldBossOutcome outcome)
        {
            if (outcome == WorldBossOutcome.None) throw new ArgumentException("Aucune issue.", nameof(outcome));
            if (window <= LastWindow) return this;   // déjà réglée (ou plus ancienne) : rien ne change
            return new WorldBossMemory(outcome == WorldBossOutcome.PlayersWon ? checked(PlayerWins + 1) : PlayerWins,
                outcome == WorldBossOutcome.BossWon ? checked(BossWins + 1) : BossWins, window, outcome);
        }

        public SavedEvent ToEvent(string key) => new SavedEvent(key, PlayerWins, BossWins, LastWindow,
            LastOutcome == WorldBossOutcome.PlayersWon ? EventOutcome.Won : LastOutcome == WorldBossOutcome.BossWon ? EventOutcome.Lost : EventOutcome.None);

        public static WorldBossMemory FromEvent(SavedEvent? e) => e is null ? None : new WorldBossMemory(e.Won, e.Lost, e.Last,
            e.Outcome == EventOutcome.Won ? WorldBossOutcome.PlayersWon : e.Outcome == EventOutcome.Lost ? WorldBossOutcome.BossWon : WorldBossOutcome.None);

        public bool Equals(WorldBossMemory o) => PlayerWins == o.PlayerWins && BossWins == o.BossWins && LastWindow == o.LastWindow && LastOutcome == o.LastOutcome;
        public override bool Equals(object? obj) => obj is WorldBossMemory m && Equals(m);
        public override int GetHashCode() => unchecked((int)(PlayerWins * 31 + BossWins * 7 + LastWindow) ^ (int)LastOutcome);
    }

    /// <summary>Grand Solitaire : une occurrence planifiée : sa fenêtre et le début de ses signes (temps serveur, ms).</summary>
    public readonly struct WorldBossOccurrence
    {
        public long Window { get; }
        public long SignsStartMs { get; }

        internal WorldBossOccurrence(long window, long signsStartMs)
        {
            Window = window;
            SignsStartMs = signsStartMs;
        }
    }

    /// <summary>
    /// Grand Solitaire : planification déterministe en <b>temps serveur</b> (ms fournies par le serveur, jamais lues d'une horloge) : le
    /// temps est pris par fenêtres de <see cref="WorldBossDefinition.WindowMinutes"/> ; dans chaque fenêtre, une chance
    /// (<see cref="WorldBossDefinition.ChancePercent"/>) et un début tiré pour que signes, limite et assaut tiennent dans la fenêtre, par un
    /// <c>Pcg32</c> initialisé par la clé de la fenêtre.
    /// </summary>
    public static class WorldBossSchedule
    {
        public static long WindowMs(WorldBossDefinition boss) => checked(boss.WindowMinutes * 60_000L);

        public static long WindowOf(WorldBossDefinition boss, long serverMs)
        {
            if (serverMs < 0) throw new ArgumentOutOfRangeException(nameof(serverMs));
            return serverMs / WindowMs(boss);
        }

        /// <summary>Durée la plus longue d'une occurrence : signes, limite, puis chaque objectif chargé.</summary>
        public static long LongestMs(WorldBossDefinition boss) =>
            checked(boss.SignsMs + boss.LimitMs + boss.ObjectiveMs * WorldBossValidator.MaxObjectives);

        public static WorldBossOccurrence? InWindow(WorldBossDefinition boss, long window)
        {
            if (boss is null) throw new ArgumentNullException(nameof(boss));
            if (window < 0) throw new ArgumentOutOfRangeException(nameof(window));
            var random = new Pcg32(Loot.Fnv1a($"worldboss:{boss.Id}:{window}"), Loot.Fnv1a("worldboss:" + boss.Id));
            if (!random.RollBasisPoints(boss.ChancePercent * 100)) return null;
            long room = WindowMs(boss) - LongestMs(boss);   // positif : vérifié par le validateur
            long offset = (long)(((ulong)random.NextUInt() << 32 | random.NextUInt()) % (ulong)room);
            return new WorldBossOccurrence(window, checked(window * WindowMs(boss) + offset));
        }
    }

    /// <summary>
    /// Grand Solitaire : l'événement d'une occurrence, en temps serveur (ms fournies de l'extérieur). Règles retenues : apparition à un
    /// emplacement admissible tiré au sort, loin des joueurs, après <see cref="WorldBossDefinition.SignsMs"/> de signes ; compte à rebours de
    /// <see cref="WorldBossDefinition.LimitMs"/> depuis l'apparition physique, que rien ne remet à zéro (pas même le combat) ; cible
    /// villageoise annoncée à <see cref="WorldBossDefinition.AnnounceMs"/> ; à la limite, assaut : il rejoint sa cible à pied (jamais de
    /// téléportation : chaque pas reste dans sa vitesse), puis charge ses objectifs l'un après l'autre ; trois phases selon sa vie (sauf
    /// mémoire du vainqueur : enragé d'emblée, à 100 % de vie, sans bonus de dégâts) ; vie selon les joueurs (jamais les compagnons), figée
    /// au début de chaque phase. Tout est entier et déterministe.
    /// </summary>
    public sealed class WorldBossEvent
    {
        private readonly WorldBossDefinition _boss;
        private readonly Pcg32 _random;
        private readonly Dictionary<string, long> _contributions = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly List<GroundVector> _path = new List<GroundVector>();
        private int _players;
        private int _plate;
        private long _objectiveSince = -1;

        public WorldBossDefinition Boss => _boss;
        public long Window { get; }
        public WorldBossMemory Memory { get; }

        /// <summary>Temps serveur du début des signes, de l'apparition physique (T0), de l'annonce et de la limite (ms).</summary>
        public long SignsStartMs { get; }
        public long AppearMs => checked(SignsStartMs + _boss.SignsMs);
        public long AnnounceAtMs => checked(AppearMs + _boss.AnnounceMs);
        public long LimitAtMs => checked(AppearMs + _boss.LimitMs);

        public long NowMs { get; private set; }
        public WorldBossStage Stage { get; private set; }
        public WorldBossOutcome Outcome { get; private set; }
        public WorldBossSite Site { get; }
        public GroundVector Position { get; private set; }

        /// <summary>Cible villageoise, choisie avant le terme et annoncée à 50 minutes ; null avant.</summary>
        public WorldBossTarget? Target { get; private set; }

        public int ObjectivesDestroyed { get; private set; }
        public long Health { get; private set; }
        public long MaxHealth { get; private set; }

        /// <summary>Aucun bonus de dégâts permanent, même après une victoire du boss (mémoire du vainqueur) : toujours 100 %.</summary>
        public int DamagePercent => 100;

        public WorldBossEvent(WorldBossDefinition boss, WorldBossMemory memory, WorldBossOccurrence occurrence, IReadOnlyList<GroundVector> playersAtSigns)
        {
            _boss = boss ?? throw new ArgumentNullException(nameof(boss));
            if (playersAtSigns is null) throw new ArgumentNullException(nameof(playersAtSigns));
            Memory = memory;
            Window = occurrence.Window;
            SignsStartMs = occurrence.SignsStartMs;
            NowMs = SignsStartMs;
            _random = new Pcg32(Loot.Fnv1a($"worldboss:{boss.Id}:{Window}:route"), Loot.Fnv1a("worldboss-route:" + boss.Id));
            Site = ChooseSite(boss, playersAtSigns, _random);
            Position = Site.Position;
            Stage = WorldBossStage.Signs;
        }

        /// <summary>
        /// Emplacement d'apparition : tiré au sort parmi les admissibles qui n'ont aucun joueur à moins de <see cref="WorldBossDefinition.Clearance"/>
        /// ; s'il n'y en a aucun, celui dont le joueur le plus proche est le plus loin (premier dans l'ordre des données en cas d'égalité).
        /// </summary>
        public static WorldBossSite ChooseSite(WorldBossDefinition boss, IReadOnlyList<GroundVector> players, Pcg32 random)
        {
            long clearance = boss.Clearance.Raw;
            long Nearest(WorldBossSite s) => players.Count == 0 ? long.MaxValue
                : players.Min(p => Planar.LengthSquared(checked(p.X.Raw - s.Position.X.Raw), checked(p.Z.Raw - s.Position.Z.Raw)));
            var free = boss.Sites.Where(s => Nearest(s) > checked(clearance * clearance)).ToList();
            if (free.Count > 0) return free[(int)random.NextBounded((uint)free.Count)];
            long best = boss.Sites.Max(Nearest);
            return boss.Sites.First(s => Nearest(s) == best);
        }

        /// <summary>Signe d'arrivée du moment (aucun hors des signes).</summary>
        public WorldBossSign Sign
        {
            get
            {
                if (Stage != WorldBossStage.Signs) return WorldBossSign.None;
                long left = AppearMs - NowMs;
                // Mêmes parts que le dossier (180, 120 et 45 s), à l'échelle de la durée des signes.
                if (left > _boss.SignsMs * 120 / 180) return WorldBossSign.Ground;
                if (left > _boss.SignsMs * 45 / 180) return WorldBossSign.Sky;
                return WorldBossSign.Animals;
            }
        }

        /// <summary>Temps restant avant l'assaut (0 une fois l'assaut lancé), ou la limite entière pendant les signes.</summary>
        public long RemainingMs => Stage == WorldBossStage.Signs ? _boss.LimitMs : Math.Max(0, LimitAtMs - NowMs);

        public bool IsPresent => Stage >= WorldBossStage.Roaming && Stage <= WorldBossStage.Victorious && Stage != WorldBossStage.Defeated;

        /// <summary>Le boss peut être combattu (présent, pas encore vaincu ni vainqueur).</summary>
        public bool IsFightable => Stage >= WorldBossStage.Roaming && Stage <= WorldBossStage.Assault;

        /// <summary>Phase selon la vie seulement : elle règle les ouvertures des plaques (0 : calme…).</summary>
        public int PlatePhase => _plate;

        /// <summary>Phase de comportement : celle de la vie, ou la dernière (enragé) si le boss revient vainqueur.</summary>
        public int Phase => Memory.Raged ? _boss.Phases.Count - 1 : _plate;

        public WorldBossPhase PhaseNow => _boss.Phases[Phase];

        /// <summary>Attaques de la phase de comportement.</summary>
        public IReadOnlyList<string> Attacks => PhaseNow.Attacks;

        /// <summary>
        /// Joueurs actifs de l'événement (les compagnons ne comptent jamais). La vie suit ce nombre au début de chaque phase (dans les
        /// bornes de 5 à 100 joueurs), pas en cours de phase.
        /// </summary>
        public void SetPlayers(int players)
        {
            if (players < 0 || players > 10_000) throw new ArgumentOutOfRangeException(nameof(players));
            _players = players;
        }

        public int Players => _players;

        /// <summary>
        /// Avance l'événement jusqu'au temps serveur <paramref name="serverMs"/> (jamais en arrière : refusé, rien ne change). Le boss
        /// marche à sa vitesse vers sa destination ; il ne saute jamais.
        /// </summary>
        public void Advance(long serverMs)
        {
            if (serverMs < NowMs) throw new ArgumentOutOfRangeException(nameof(serverMs), serverMs, "le temps serveur ne recule pas.");
            while (NowMs < serverMs && Stage != WorldBossStage.Gone && Stage != WorldBossStage.Defeated)
            {
                long next = NextMilestone(serverMs);
                Walk(next - NowMs);
                NowMs = next;
                Settle();
            }
            if (NowMs < serverMs) NowMs = serverMs;
        }

        private long NextMilestone(long serverMs)
        {
            long next = serverMs;
            foreach (long m in new[] { AppearMs, AnnounceAtMs, LimitAtMs })
                if (m > NowMs && m < next) next = m;
            if (_objectiveSince >= 0 && checked(_objectiveSince + _boss.ObjectiveMs) is long done && done > NowMs && done < next) next = done;
            // Pas d'au plus une seconde : chaque arrivée à destination est vue à temps (la marche se fait par petits pas).
            return Math.Min(next, checked(NowMs + 1000));
        }

        private void Settle()
        {
            if (Stage == WorldBossStage.Signs && NowMs >= AppearMs)
            {
                Stage = WorldBossStage.Roaming;
                MaxHealth = _boss.MaxHealthFor(_players);
                Health = MaxHealth;   // mémoire du vainqueur : enragé d'emblée, mais à 100 % de vie
                _plate = 0;
                PickNextSite();
            }
            if (Stage == WorldBossStage.Roaming && NowMs >= AnnounceAtMs)
            {
                Target = _boss.Targets[(int)_random.NextBounded((uint)_boss.Targets.Count)];
                Stage = WorldBossStage.Announced;
            }
            if (Stage == WorldBossStage.Announced && NowMs >= LimitAtMs)
            {
                Stage = WorldBossStage.Assault;
                _path.Clear();
                _path.AddRange(Target!.Route);
                _path.Add(Target.Objectives[0].Position);
            }
            if ((Stage == WorldBossStage.Roaming || Stage == WorldBossStage.Announced) && _path.Count == 0) PickNextSite();
            if (Stage == WorldBossStage.Assault && _path.Count == 0)
            {
                if (_objectiveSince < 0) _objectiveSince = NowMs;   // arrivé devant l'objectif : il prépare sa charge
                else if (NowMs >= checked(_objectiveSince + _boss.ObjectiveMs))
                {
                    ObjectivesDestroyed++;
                    _objectiveSince = -1;
                    if (ObjectivesDestroyed >= Target!.Objectives.Count)
                    {
                        Stage = WorldBossStage.Victorious;
                        Outcome = WorldBossOutcome.BossWon;
                        // Il quitte le lieu par sa route, à rebours.
                        _path.AddRange(Target.Route.Reverse());
                    }
                    else _path.Add(Target.Objectives[ObjectivesDestroyed].Position);
                }
            }
            if (Stage == WorldBossStage.Victorious && _path.Count == 0) Stage = WorldBossStage.Gone;
        }

        private void PickNextSite()
        {
            var others = _boss.Sites.Where(s => s.Position != Position).ToList();
            if (others.Count == 0) return;   // un seul emplacement : il reste sur place
            _path.Add(others[(int)_random.NextBounded((uint)others.Count)].Position);
        }

        /// <summary>Marche de <paramref name="ms"/> le long du chemin, à sa vitesse ; le reste d'un pas continue vers le point suivant.</summary>
        private void Walk(long ms)
        {
            if (!IsPresent || ms <= 0) return;
            long budget = checked(_boss.WalkSpeed.Raw * ms / 1000);   // unités brutes
            while (budget > 0 && _path.Count > 0)
            {
                GroundVector to = _path[0];
                long dx = checked(to.X.Raw - Position.X.Raw), dz = checked(to.Z.Raw - Position.Z.Raw);
                long dist = IntMath.Sqrt(Planar.LengthSquared(dx, dz));
                if (dist <= budget)
                {
                    Position = to;
                    budget -= dist;
                    _path.RemoveAt(0);
                }
                else
                {
                    Planar.TryResize(dx, dz, budget, out long sx, out long sz);
                    Position = GroundVector.FromRaw(checked(Position.X.Raw + sx), checked(Position.Z.Raw + sz));
                    budget = 0;
                }
            }
        }

        /// <summary>
        /// Coup porté au boss par le joueur <paramref name="player"/> (participation comptée). Refusé (rien ne change) hors combat : pas encore
        /// là, déjà vaincu, ou vainqueur. À 0 PV, les joueurs ont gagné, même pendant l'assaut.
        /// </summary>
        public bool Damage(string player, long amount)
        {
            if (player is null) throw new ArgumentNullException(nameof(player));
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!IsFightable) return false;
            long dealt = Math.Min(amount, Health);
            Health -= dealt;
            Contribute(player, WorldBossContribution.Damage, dealt);
            if (Health == 0)
            {
                Stage = WorldBossStage.Defeated;
                Outcome = WorldBossOutcome.PlayersWon;
                return true;
            }
            int plate = PlateOf(Health, MaxHealth);
            if (plate != _plate)
            {
                // Début d'une phase : la vie maximale suit le nombre de joueurs présents, la part de vie restante est gardée.
                long max = _boss.MaxHealthFor(_players);
                Health = Math.Max(1, IntMath.MulDiv(Health, max, MaxHealth, RoundingMode.Floor));
                MaxHealth = max;
                _plate = plate;
            }
            return true;
        }

        private int PlateOf(long health, long max)
        {
            int phase = 0;
            for (int i = 1; i < _boss.Phases.Count; i++)
                if (checked(health * 100) <= checked(max * _boss.Phases[i].FromPercent)) phase = i;
            return phase;
        }

        /// <summary>Soin, protection ou objectif accompli : comptés dans la participation comme les dégâts (pendant le combat seulement).</summary>
        public bool Contribute(string player, WorldBossContribution kind, long amount)
        {
            if (player is null) throw new ArgumentNullException(nameof(player));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!IsFightable) return false;
            _contributions[player] = checked((_contributions.TryGetValue(player, out long c) ? c : 0) + amount);
            return true;
        }

        /// <summary>Participation de chaque joueur, triée par identifiant.</summary>
        public IReadOnlyList<KeyValuePair<string, long>> Contributions => _contributions.OrderBy(c => c.Key, StringComparer.Ordinal).ToList();

        /// <summary>Mémoire après cette occurrence (inchangée tant qu'elle n'a pas d'issue).</summary>
        public WorldBossMemory MemoryAfter => Outcome == WorldBossOutcome.None ? Memory : Memory.After(Window, Outcome);

        /// <summary>
        /// Annonce au sol d'une attaque de la phase, lancée maintenant depuis la place du boss, tournée selon <paramref name="facing"/> (non
        /// nul) ; pour des éclats retombants, une marque par cible de <paramref name="targets"/>, au plus le nombre du moment (de 3 à 8
        /// selon les joueurs), jamais une sous chaque joueur.
        /// </summary>
        public WorldBossTelegraph Telegraph(string attack, GroundVector facing, IReadOnlyList<GroundVector> targets)
        {
            if (!IsFightable) throw new InvalidOperationException("Le boss ne combat pas.");
            if (!Attacks.Contains(attack)) throw new ArgumentException($"Attaque « {attack} » hors de la phase.", nameof(attack));
            return WorldBossTelegraph.Build(_boss, _boss.Attack(attack), NowMs, Position, facing, targets, _players);
        }
    }

    /// <summary>
    /// Grand Solitaire : annonce d'une attaque au sol (dossier, p. 24) : début, verrouillage (l'axe ou la position se fige), impact (et
    /// impacts décalés des disques), fin de la reprise d'appui ; zones au sol. Les durées sont les mêmes de 5 à 100 joueurs ; seul le
    /// nombre de marques des éclats change (de <c>countMin</c> à <c>countMax</c>).
    /// </summary>
    public sealed class WorldBossTelegraph
    {
        public WorldBossAttack Attack { get; }
        public long StartMs { get; }
        public long LockAtMs { get; }
        public long ImpactAtMs { get; }
        public long EndMs { get; }
        public GroundVector Origin { get; }

        /// <summary>Axe unitaire brut (longueur <see cref="Planar.Unit"/>).</summary>
        public long AxisX { get; }
        public long AxisZ { get; }

        /// <summary>Centres des disques (éclats, piétinement), dans l'ordre de leurs impacts ; vide pour les autres formes.</summary>
        public IReadOnlyList<GroundVector> Circles { get; }

        private WorldBossTelegraph(WorldBossAttack attack, long start, GroundVector origin, long ax, long az, IReadOnlyList<GroundVector> circles)
        {
            Attack = attack;
            StartMs = start;
            ImpactAtMs = checked(start + attack.AnnounceMs);
            LockAtMs = checked(ImpactAtMs - attack.LockMs);
            EndMs = checked(ImpactAtMs + attack.StaggerMs * Math.Max(0, circles.Count - 1) + attack.RecoveryMs);
            Origin = origin;
            AxisX = ax;
            AxisZ = az;
            Circles = circles;
        }

        /// <summary>Nombre de disques pour <paramref name="players"/> joueurs : de <c>countMin</c> (5 joueurs) à <c>countMax</c> (100), en ligne droite.</summary>
        public static int CircleCount(WorldBossDefinition boss, WorldBossAttack attack, int players)
        {
            int n = Math.Min(boss.MaxPlayers, Math.Max(boss.MinPlayers, players));
            if (boss.MaxPlayers == boss.MinPlayers) return attack.CountMax;
            return attack.CountMin + (attack.CountMax - attack.CountMin) * (n - boss.MinPlayers) / (boss.MaxPlayers - boss.MinPlayers);
        }

        internal static WorldBossTelegraph Build(WorldBossDefinition boss, WorldBossAttack attack, long now, GroundVector origin, GroundVector facing,
            IReadOnlyList<GroundVector> targets, int players)
        {
            if (targets is null) throw new ArgumentNullException(nameof(targets));
            if (!Planar.TryNormalize(facing.X.Raw, facing.Z.Raw, out long ax, out long az)) throw new ArgumentException("Direction nulle.", nameof(facing));
            var circles = new List<GroundVector>();
            if (attack.Shape == WorldBossShape.Circles)
            {
                int count = CircleCount(boss, attack, players);
                if (attack.Anchor == CircleAnchor.Targets) circles.AddRange(targets.Take(count));
                else
                {
                    // Sous les sabots avant : devant le boss (son rayon), répartis de part et d'autre de son axe.
                    long forward = boss.Radius.Raw, side = boss.Radius.Raw / 2;
                    for (int i = 0; i < count; i++)
                    {
                        long offset = count == 1 ? 0 : checked(side * (2 * i - (count - 1)) / (count - 1));
                        circles.Add(GroundVector.FromRaw(checked(origin.X.Raw + ax * forward / Planar.Unit - az * offset / Planar.Unit),
                            checked(origin.Z.Raw + az * forward / Planar.Unit + ax * offset / Planar.Unit)));
                    }
                }
            }
            return new WorldBossTelegraph(attack, now, origin, ax, az, circles);
        }

        /// <summary>L'emprise (centre <paramref name="point"/>, rayon <paramref name="radius"/>) touche-t-elle la zone de l'attaque (frontière comprise) ?</summary>
        public bool Touches(GroundVector point, Fixed radius)
        {
            long px = point.X.Raw, pz = point.Z.Raw, r = radius.Raw;
            switch (Attack.Shape)
            {
                case WorldBossShape.Cone:
                    return new AttackZone(AttackShape.Cone, Origin.X.Raw, Origin.Z.Raw, AxisX, AxisZ, Attack.Range.Raw, Attack.Angle).Touches(px, pz, r);
                case WorldBossShape.Line:
                    return new AttackZone(AttackShape.Line, Origin.X.Raw, Origin.Z.Raw, AxisX, AxisZ, Attack.Range.Raw, 0, Attack.Width.Raw / 2).Touches(px, pz, r);
                case WorldBossShape.Ring:
                {
                    // Dans l'anneau : le bord extérieur atteint, et l'emprise pas entièrement dans la zone sûre du centre.
                    long dx = checked(px - Origin.X.Raw), dz = checked(pz - Origin.Z.Raw);
                    long d2 = Planar.LengthSquared(dx, dz);
                    long outer = checked(Attack.Outer.Raw + r), inner = Attack.Inner.Raw - r;
                    return d2 <= checked(outer * outer) && (inner <= 0 || d2 >= checked(inner * inner));
                }
                default:
                    return Circles.Any(c => Planar.WithinDistance(c.X.Raw, c.Z.Raw, px, pz, checked(Attack.Radius.Raw + r)));
            }
        }
    }

    /// <summary>
    /// Grand Solitaire : compagnons pendant l'événement (décision de Damien du 14 novembre 2026, 23 h 45) : chaque joueur garde au plus
    /// <see cref="WorldBossDefinition.CompanionsInCombat"/> compagnon au combat (le premier de sa liste) ; les autres attendent au point de
    /// repli. Les compagnons ne comptent jamais parmi les joueurs.
    /// </summary>
    public static class WorldBossCompanions
    {
        public static (IReadOnlyList<string> InCombat, IReadOnlyList<string> AtFallback) Assign(WorldBossDefinition boss, IReadOnlyList<string> companions)
        {
            if (boss is null) throw new ArgumentNullException(nameof(boss));
            if (companions is null) throw new ArgumentNullException(nameof(companions));
            return (companions.Take(boss.CompanionsInCombat).ToList(), companions.Skip(boss.CompanionsInCombat).ToList());
        }
    }

    /// <summary>Grand Solitaire : butin d'un joueur après une victoire des joueurs.</summary>
    public sealed class WorldBossAward
    {
        public string Player { get; }
        public IReadOnlyList<string> Items { get; }

        internal WorldBossAward(string player, IReadOnlyList<string> items)
        {
            Player = player;
            Items = items;
        }
    }

    /// <summary>
    /// Grand Solitaire : butin (dossier, p. 26 à 32) après une victoire des joueurs, pour chaque joueur qui a participé utilement (dégâts,
    /// soin, protection, objectifs ; jamais réservé au dernier coup) : trophée et apparence à sa première victoire éligible ; monture
    /// très rare, tirée à chaque victoire (points de base), par un <c>Pcg32</c> initialisé par la clé (boss, fenêtre, joueur).
    /// </summary>
    public static class WorldBossSpoils
    {
        /// <param name="priorWins">Victoires éligibles déjà obtenues par le joueur (registre).</param>
        public static IReadOnlyList<WorldBossAward> Award(WorldBossEvent e, Func<string, long> priorWins)
        {
            if (e is null) throw new ArgumentNullException(nameof(e));
            if (priorWins is null) throw new ArgumentNullException(nameof(priorWins));
            var awards = new List<WorldBossAward>();
            if (e.Outcome != WorldBossOutcome.PlayersWon) return awards;
            WorldBossLoot loot = e.Boss.Loot;
            foreach (KeyValuePair<string, long> c in e.Contributions)
            {
                if (c.Value <= 0) continue;
                var items = new List<string>();
                if (priorWins(c.Key) == 0)
                {
                    items.Add(loot.Trophy);
                    items.Add(loot.Appearance);
                }
                var random = new Pcg32(Loot.Fnv1a($"worldboss:{e.Boss.Id}:{e.Window}:{c.Key}"), Loot.Fnv1a("worldboss-loot:" + e.Boss.Id));
                if (random.RollBasisPoints(loot.MountChanceBasisPoints)) items.Add(loot.Mount);
                awards.Add(new WorldBossAward(c.Key, items));
            }
            return awards;
        }
    }
}
