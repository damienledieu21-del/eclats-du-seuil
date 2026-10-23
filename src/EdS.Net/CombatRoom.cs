using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using EdS.Data;
using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.Simulation.Engine;

namespace EdS.Net
{
    /// <summary>Réglages d'une salle.</summary>
    public sealed class RoomOptions
    {
        /// <summary>Délai pour revenir après une déconnexion pendant un combat (décision du 19/10/2026 : 30 s).</summary>
        public double ReconnectSeconds { get; set; } = 30;

        /// <summary>Délai pour se présenter (hello ou reconnect) après la connexion.</summary>
        public double HelloTimeoutSeconds { get; set; } = 5;

        /// <summary>Messages par seconde au plus par connexion, en régime établi (rafale du double admise).</summary>
        public double MaxMessagesPerSecond { get; set; } = 60;

        /// <summary>Connexions simultanées au plus (places comprises).</summary>
        public int MaxConnections { get; set; } = 16;

        /// <summary>Durée du combat ; null : celle du scénario de la salle (60 s pour <c>coop/duo_meute</c>).</summary>
        public long? DurationMs { get; set; }

        /// <summary>Ticks simulés au plus par appel à <see cref="CombatRoom.Advance"/> (un serveur en retard ralentit au lieu de rattraper d'un coup).</summary>
        public int MaxTicksPerAdvance { get; set; } = 5;

        /// <summary>Intervalle minimal entre deux historiques demandés par la même connexion.</summary>
        public double ResyncIntervalSeconds { get; set; } = 1;

        /// <summary>Source des jetons de reconnexion (tests) ; null : 128 bits aléatoires cryptographiques.</summary>
        public Func<string>? TokenSource { get; set; }

        /// <summary>
        /// Sous-lot 11.7 : arrivée pendant le combat. Le combat commence dès que les joueurs présents sont prêts (au moins un) ; les places
        /// libres restent hors du combat, et un joueur qui arrive pendant l'affrontement y entre au tick courant (<c>CombatEngine.Join</c>),
        /// à sa place du scénario ou, si elle est prise, à la plus proche place libre. Faux par défaut : la salle du lot 10 attend toutes les
        /// places.
        /// </summary>
        public bool JoinDuringCombat { get; set; }
    }

    /// <summary>
    /// Sous-lot 10.2 : salle de combat autoritaire. Une place par personnage du scénario (deux Bastions en coopération), une salle d'attente
    /// (présentation, build, prêt), puis le combat : le serveur range les intentions de chaque joueur dans le prochain tick, les soumet au
    /// moteur par groupes tout ou rien, simule le tick et diffuse les commandes admises avec l'empreinte du journal. Un joueur déconnecté
    /// laisse le combat continuer (personnage inactif, garde relâchée) et a <see cref="RoomOptions.ReconnectSeconds"/> pour revenir avec
    /// son jeton (état courant renvoyé, sous-lot 11.7). Le temps est celui des appels à <see cref="Advance"/> : aucun fil, aucune horloge ;
    /// un hôte (<see cref="RoomHost"/>) l'anime en temps réel, les tests en temps virtuel. Sous-lot 11.7 : dans un combat positionné, la
    /// marche voulue de chaque joueur devient <c>move</c> ou <c>stop</c> et sa cible <c>select</c> (<see cref="MovementIntent"/>, comme en
    /// solo) ; avec <see cref="RoomOptions.JoinDuringCombat"/>, un joueur peut arriver pendant l'affrontement.
    /// </summary>
    public sealed class CombatRoom
    {
        private sealed class Client
        {
            public INetConnection Connection = null!;
            public Seat? Seat;
            public double AcceptedAt;
            public double Tokens;
            public double TokensAt;
            public double LastResyncAt = double.NegativeInfinity;

            /// <summary>Refusée (erreur envoyée, connexion fermée) : ses messages encore en file ne sont plus lus.</summary>
            public bool Failed;
        }

        private sealed class Seat
        {
            public string Id = null!;
            public ClassDefinition Class = null!;
            public string? Player;
            public CharacterBuild? Build;
            public string? BuildName;
            public bool Ready;
            public Client? Client;
            public string? Token;
            public double? DisconnectedAt;
            public bool Abandoned;
            public bool Guard;
            public bool Parry;
            public int? Slot;
            public string? Target;
            public string? Ally;

            /// <summary>Sous-lot 11.7 : marche voulue et cible sélectionnée (combat positionné).</summary>
            public readonly MovementIntent Movement = new MovementIntent();

            /// <summary>Sous-lot 11.7 : son personnage est dans le combat en cours (au départ, ou arrivé pendant l'affrontement).</summary>
            public bool InCombat;

            public bool Taken => Player != null;

            public void ResetIntentions()
            {
                Guard = false;
                Parry = false;
                Slot = null;
                Movement.Wished = null;   // sous-lot 11.7 : un joueur coupé s'arrête
            }

            public void Free()
            {
                Player = null;
                Build = null;
                BuildName = null;
                Ready = false;
                Client = null;
                Token = null;
                DisconnectedAt = null;
                Abandoned = false;
                Target = null;
                Ally = null;
                InCombat = false;
                ResetIntentions();
                Movement.Reset();
            }
        }

        private readonly GameData _data;
        private readonly CombatSetup _scenario;
        private readonly RoomOptions _options;
        private readonly List<Seat> _seats;
        private readonly List<Client> _clients = new List<Client>();
        private readonly double _tickSeconds;
        private CombatEngine? _engine;
        private CombatSetup? _setup;
        private IReadOnlyList<ConvexPolygon>? _obstacles;

        /// <summary>Sous-lot 11.9 : décor qui arrête la vue (obstacles bas exclus), pour la cible ; <see cref="_obstacles"/> pour les places.</summary>
        private IReadOnlyList<ConvexPolygon>? _sightObstacles;
        private HashingCombatLog? _log;
        private double _accumulated;

        /// <summary>
        /// Salle pour le scénario <paramref name="scenario"/> (ses commandes sont ignorées ; ses personnages sont les places, dans l'ordre) ;
        /// les builds demandés par les joueurs sont résolus dans <paramref name="data"/>.
        /// </summary>
        public CombatRoom(GameData data, CombatSetup scenario, RoomOptions? options = null)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            _options = options ?? new RoomOptions();
            _seats = scenario.Combatants.OfType<CharacterSetup>().Select(c => new Seat { Id = c.Id, Class = c.Class }).ToList();
            if (_seats.Count == 0) throw new ArgumentException("Scénario sans personnage : aucune place.", nameof(scenario));
            if (_seats.Count > Protocol.MaxSeats) throw new ArgumentException($"Au plus {Protocol.MaxSeats} places.", nameof(scenario));
            if (_seats.Any(s => !MessageCodec.IsId(s.Id))) throw new ArgumentException("Identifiant de personnage invalide pour le protocole.", nameof(scenario));
            // Revue Codex de la PR #20 (tour 4) : les ennemis aussi peuvent être nommés dans un message (cible) ; le client refuserait
            // l'en-tête, le serveur refuse donc un tel scénario dès la création de la salle.
            for (int i = 0; i < scenario.Combatants.Count; i++)
                if (!MessageCodec.IsId(scenario.Combatants[i].Id))
                    throw new ArgumentException($"Combattant {i} (combatants[{i}].id) : identifiant invalide pour le protocole (1 à {Protocol.MaxIdLength} caractères parmi a-z, A-Z, 0-9, « . », « _ », « - »).", nameof(scenario));
            TickMs = checked((int)scenario.Combat.TickMilliseconds);
            _tickSeconds = TickMs / 1000.0;
        }

        public RoomPhase Phase { get; private set; } = RoomPhase.Lobby;

        /// <summary>Temps écoulé de la salle (somme des appels à <see cref="Advance"/>), en secondes.</summary>
        public double Now { get; private set; }

        public int TickMs { get; }

        /// <summary>Identifiants des places (personnages du scénario), dans l'ordre.</summary>
        public IReadOnlyList<string> SeatIds => _seats.Select(s => s.Id).ToArray();

        /// <summary>Prochain tick à simuler du combat en cours ; 0 hors combat.</summary>
        public long Tick => _engine?.CurrentTick ?? 0;

        /// <summary>Empreinte du journal du combat en cours ; null hors combat.</summary>
        public string? Hash => _engine is null ? null : _log!.Hash;

        public int ConnectionCount => _clients.Count;

        /// <summary>Résultat du dernier combat terminé.</summary>
        public CombatResult? LastResult { get; private set; }

        /// <summary>
        /// Journal rejouable du dernier combat terminé : le scénario de départ complété des commandes admises, simulé de nouveau (son en-tête
        /// contient toutes les commandes ; ses autres lignes sont celles du combat joué).
        /// </summary>
        public IReadOnlyList<string>? LastJournal { get; private set; }

        /// <summary>Messages de diagnostic (console du serveur dédié).</summary>
        public Action<string>? Trace { get; set; }

        /// <summary>Appelé à la fin de chaque combat avec son journal rejouable.</summary>
        public Action<IReadOnlyList<string>>? CombatEnded { get; set; }

        /// <summary>Prend en charge une nouvelle connexion ; elle doit se présenter avant <see cref="RoomOptions.HelloTimeoutSeconds"/>.</summary>
        public void Accept(INetConnection connection)
        {
            if (connection is null) throw new ArgumentNullException(nameof(connection));
            var client = new Client { Connection = connection, AcceptedAt = Now, Tokens = 2 * _options.MaxMessagesPerSecond, TokensAt = Now };
            if (_clients.Count >= _options.MaxConnections)
            {
                Fail(client, $"trop de connexions : au plus {_options.MaxConnections}.");
                return;
            }
            _clients.Add(client);
            Trace?.Invoke($"connexion de {connection.RemoteAddress}");
        }

        /// <summary>Avance le temps de la salle : messages reçus, délais, ticks du combat.</summary>
        public void Advance(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            Now += seconds;
            // Revue Codex de la PR #19 : les délais sont appliqués au nouvel instant avant de lire les messages en attente ; un hello
            // ou un reconnect arrivé pendant cet intervalle mais lu après l'échéance n'est plus accepté.
            foreach (Client client in _clients.ToArray())
                if (client.Connection.IsOpen && client.Seat is null && Now - client.AcceptedAt >= _options.HelloTimeoutSeconds)
                    Fail(client, $"présentation attendue (hello ou reconnect) dans les {_options.HelloTimeoutSeconds} s.");
            ExpireDisconnected();
            foreach (Client client in _clients.ToArray())
                if (!client.Failed) Receive(client);
            foreach (Client client in _clients.ToArray())
                if (!client.Connection.IsOpen) Drop(client);
            ExpireDisconnected();
            if (Phase == RoomPhase.Combat)
            {
                _accumulated += seconds;
                int steps = 0;
                while (Phase == RoomPhase.Combat && _accumulated >= _tickSeconds)
                {
                    if (steps >= _options.MaxTicksPerAdvance)
                    {
                        _accumulated = 0;
                        break;
                    }
                    StepTick();
                    _accumulated -= _tickSeconds;
                    steps++;
                }
            }
        }

        // --- Messages --------------------------------------------------------------------------------------------------------

        private void Receive(Client client)
        {
            while (client.Connection.TryReceive(MessageDirection.ClientToServer, out NetMessage? message, out string? error))
            {
                if (message is null)
                {
                    Trace?.Invoke($"{client.Connection.RemoteAddress} : message refusé, connexion fermée ({error})");
                    return;   // connexion déjà fermée par la lecture typée
                }
                if (!Spend(client))
                {
                    Fail(client, $"trop de messages : au plus {_options.MaxMessagesPerSecond} par seconde.");
                    return;
                }
                if (client.Seat is null) HandleNewcomer(client, message);
                else HandleSeated(client, client.Seat, message);
                if (!client.Connection.IsOpen) return;
            }
        }

        /// <summary>Seau à jetons : rafale de deux secondes de messages, puis le débit permis.</summary>
        private bool Spend(Client client)
        {
            double capacity = 2 * _options.MaxMessagesPerSecond;
            client.Tokens = Math.Min(capacity, client.Tokens + (Now - client.TokensAt) * _options.MaxMessagesPerSecond);
            client.TokensAt = Now;
            if (client.Tokens < 1) return false;
            client.Tokens -= 1;
            return true;
        }

        private void HandleNewcomer(Client client, NetMessage message)
        {
            switch (message)
            {
                case HelloMessage hello:
                    Hello(client, hello);
                    break;
                case ReconnectMessage reconnect:
                    Reconnect(client, reconnect);
                    break;
                case PingMessage ping:
                    Send(client, new PongMessage(ping.ClientTime));
                    break;
                default:
                    Fail(client, "présentation attendue (hello ou reconnect).");
                    break;
            }
        }

        private void Hello(Client client, HelloMessage hello)
        {
            if (hello.ProtocolVersion != Protocol.Version)
            {
                Fail(client, $"protocole {hello.ProtocolVersion} non pris en charge : version {Protocol.Version} attendue.");
                return;
            }
            if (Phase == RoomPhase.Combat && !_options.JoinDuringCombat)
            {
                Fail(client, "combat en cours : on ne rejoint pas une partie commencée (se reconnecter avec son jeton, ou attendre la fin).");
                return;
            }
            Seat? seat = _seats.FirstOrDefault(s => !s.Taken && !(Phase == RoomPhase.Combat && s.InCombat));
            if (seat is null)
            {
                Fail(client, "salle pleine.");
                return;
            }
            CharacterBuild? build = BuildChoices.Resolve(hello.Build, _data, seat.Class, _scenario.Progression, out string name, out string? error);
            if (build is null)
            {
                Fail(client, error!);
                return;
            }
            // Sous-lot 11.7 : pendant le combat, le personnage entre au tick courant, avant même l'accueil (un refus ne prend pas la place).
            if (Phase == RoomPhase.Combat && JoinCombat(seat, build) is { } refused)
            {
                Fail(client, refused);
                return;
            }
            seat.Player = hello.Name;
            seat.Build = build;
            seat.BuildName = Protocol.Text(name, Protocol.MaxIdLength);
            seat.Ready = Phase == RoomPhase.Combat;
            seat.Token = NewToken();
            Attach(seat, client);
            Trace?.Invoke($"{hello.Name} prend la place « {seat.Id} » ({seat.BuildName}){(Phase == RoomPhase.Combat ? " pendant le combat" : "")}");
            BroadcastLobby();
            // Tout le monde repart de l'état courant, qui contient le nouveau venu (pas de place réservée d'avance dans le combat de départ).
            if (Phase == RoomPhase.Combat)
            {
                StateMessage? state = CurrentState();
                foreach (Seat other in _seats)
                    if (other.Client != null) SendState(other.Client, state);
            }
        }

        /// <summary>
        /// Sous-lot 11.7 : fait entrer le personnage de <paramref name="seat"/> dans le combat en cours, au tick courant : à sa place du
        /// scénario si elle est libre (aucune emprise présente et vivante dessus, ni arrivée déjà en file à ce tick), sinon à la plus proche place libre essayée (anneaux de
        /// 1,5 m, 3 m puis 4,5 m, huit directions), toujours dans l'aire, hors du décor et acceptée par la validation du combat complété.
        /// Rend null si le personnage est entré, la raison sinon.
        /// </summary>
        private string? JoinCombat(Seat seat, CharacterBuild build)
        {
            CombatEngine engine = _engine!;
            CharacterSetup scenario = _scenario.Combatants.OfType<CharacterSetup>().Single(c => c.Id == seat.Id);
            long atMs = checked(engine.CurrentTick * TickMs);
            // Revue Codex de la PR #35 (3e tour) : le majorant de l'état courant, vérifié au début du combat avec le build du scénario pour
            // les places libres, est revérifié avec le build choisi par l'arrivant (et le build du scénario pour les autres places libres),
            // avant de toucher au moteur : un build plus long qui le dépasserait est refusé ici, au lieu de couper tout le monde à l'envoi.
            CombatSetup current = engine.CompletedSetup();
            long lastMs = checked(Math.Max(1, current.DurationMs / TickMs) * TickMs);
            IEnumerable<CombatantSetup> worst = _scenario.Combatants.OfType<CharacterSetup>()
                .Where(c => (c.Id == seat.Id || _seats.Any(s => s.Id == c.Id && !s.Taken && !s.InCombat)) && !current.Combatants.Any(k => k.Id == c.Id))
                .Select(c => new CharacterSetup(c.Id, c.Team, c.Class, c.Id == seat.Id ? build : c.Build, c.Initiative, c.Placement, lastMs));
            long bound = StateBound(new CombatSetup(current.Seed, current.DurationMs, current.Stats, current.Damage, current.Progression, current.Combat,
                current.Combatants.Concat(worst), current.Commands, current.Data, current.Space, current.Zone), TickMs);
            // 4e tour : avec l'instantané courant, comme au début ; un état trop grand pour un instantané (37e tour de la PR #32) refuse l'arrivée.
            if (SnapshotText(engine) is not { } snapshotNow)
                return "entrée dans le combat refusée : état du combat trop grand pour un instantané.";
            bound = checked(bound + SnapshotExtra(snapshotNow));
            if (bound > Protocol.MaxServerMessageBytes)
                return $"entrée dans le combat refusée : avec ce build, un état courant pourrait atteindre {bound} octets, au-delà de la limite de " +
                    $"{Protocol.MaxServerMessageBytes} octets du protocole.";
            var candidates = new List<CombatantPlacement?> { scenario.Placement };
            if (scenario.Placement is { } p && _scenario.Space is { } space)
            {
                IReadOnlyList<ConvexPolygon> obstacles = _obstacles!;
                var occupied = engine.CombatantIds.Select(engine.View).Where(v => v.IsPresent && !v.IsDead && v.Position != null)
                    .Select(v => (Center: v.Position!.Value, Radius: v.Radius!.Value)).ToList();
                // Revue Codex de la PR #35 : une arrivée déjà en file pour ce tick (combattant du scénario, ou autre joueur entré à ce tick)
                // passe avant celle-ci et prendrait la place : la sienne est réservée comme une place occupée.
                foreach (CombatantSetup pending in engine.CompletedSetup().Combatants)
                    if (pending.ArrivalMs is long arrivalMs && arrivalMs / TickMs == engine.CurrentTick && pending.Placement is { } at
                        && !engine.View(pending.Id).IsPresent)
                        occupied.Add((at.Position, at.Radius));
                bool Free(GroundVector at)
                {
                    long r = p.Radius.Raw, x = at.X.Raw, z = at.Z.Raw;
                    if (x - r < space.Min.X.Raw || x + r > space.Max.X.Raw || z - r < space.Min.Z.Raw || z + r > space.Max.Z.Raw) return false;
                    if (obstacles.Any(o => o.OverlapsCircle(x, z, r))) return false;
                    return !occupied.Any(o => Planar.CirclesOverlap(x, z, r, o.Center.X.Raw, o.Center.Z.Raw, o.Radius.Raw));
                }
                candidates.Clear();
                foreach (int ring in new[] { 0, 15_000, 30_000, 45_000 })
                    for (int k = 0; k < (ring == 0 ? 1 : 8); k++)
                    {
                        GroundVector d = MoveInput.Direction(k * MoveInput.Headings / 8);
                        var at = new GroundVector(Fixed.FromRaw(checked(p.Position.X.Raw + d.X.Raw * ring / 10_000)),
                            Fixed.FromRaw(checked(p.Position.Z.Raw + d.Z.Raw * ring / 10_000)));
                        if (Free(at)) candidates.Add(new CombatantPlacement(at, p.Radius, p.Facing, p.Speed));
                    }
                if (candidates.Count == 0) return "aucune place libre autour de la place du scénario pour entrer dans le combat.";
            }
            string? last = null;
            // Revue Codex de la PR #35 (4e tour) : l'état courant réel (en-tête et instantané) qui suivra l'arrivée est mesuré sur une copie
            // du moteur (reprise de confiance de son propre instantané ; un refus d'arrivée n'y change rien), avant de toucher au vrai.
            CombatEngine probe = CombatEngine.Restore(engine.CompletedSetup(), engine.Snapshot(), NullCombatLog.Instance);
            foreach (CombatantPlacement? placement in candidates)
                try
                {
                    var joining = new CharacterSetup(scenario.Id, scenario.Team, scenario.Class, build, scenario.Initiative, placement, atMs);
                    probe.Join(joining);
                    long size = MessageCodec.Encode(new StateMessage(CombatLogHeader.Write(probe.CompletedSetup()), probe.Snapshot().Text, _log!.Hash)).Length;
                    if (size > Protocol.MaxServerMessageBytes)
                        return $"entrée dans le combat refusée : l'état courant atteindrait {size} octets, au-delà de la limite de " +
                            $"{Protocol.MaxServerMessageBytes} octets du protocole.";
                    engine.Join(joining);
                    seat.InCombat = true;
                    seat.Movement.Reset();
                    return null;
                }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
                {
                    last = e.Message;
                }
            return "entrée dans le combat refusée : " + last;
        }

        private void Reconnect(Client client, ReconnectMessage reconnect)
        {
            if (reconnect.ProtocolVersion != Protocol.Version)
            {
                Fail(client, $"protocole {reconnect.ProtocolVersion} non pris en charge : version {Protocol.Version} attendue.");
                return;
            }
            Seat? seat = _seats.FirstOrDefault(s => s.Token != null && s.Client is null && !s.Abandoned && FixedTimeEquals(s.Token, reconnect.Token));
            if (seat is null)
            {
                Fail(client, "jeton inconnu ou expiré.");
                return;
            }
            Attach(seat, client);
            Trace?.Invoke($"{seat.Player} revient sur la place « {seat.Id} »");
            BroadcastLobby();
            if (Phase == RoomPhase.Combat) SendState(client);
        }

        private void Attach(Seat seat, Client client)
        {
            seat.Client = client;
            seat.DisconnectedAt = null;
            client.Seat = seat;
            Send(client, new WelcomeMessage(Protocol.Version, seat.Id, seat.Token!, TickMs));
        }

        private void HandleSeated(Client client, Seat seat, NetMessage message)
        {
            switch (message)
            {
                case HelloMessage _:
                case ReconnectMessage _:
                    Send(client, new RejectedMessage("déjà présenté dans la salle."));
                    break;
                case BuildMessage build:
                    if (Phase == RoomPhase.Combat)
                    {
                        Send(client, new RejectedMessage("combat en cours : le build se change en salle d'attente."));
                        break;
                    }
                    CharacterBuild? resolved = BuildChoices.Resolve(build.Build, _data, seat.Class, _scenario.Progression, out string name, out string? error);
                    if (resolved is null)
                    {
                        Send(client, new RejectedMessage(Protocol.Text(error)));
                        break;
                    }
                    seat.Build = resolved;
                    seat.BuildName = Protocol.Text(name, Protocol.MaxIdLength);
                    seat.Ready = false;
                    BroadcastLobby();
                    break;
                case ReadyMessage ready:
                    if (Phase == RoomPhase.Combat) break;
                    seat.Ready = ready.Ready;
                    BroadcastLobby();
                    TryStart();
                    break;
                case InputMessage input:
                    if (Phase != RoomPhase.Combat) break;
                    seat.Guard = input.Guard;
                    if (input.Parry) seat.Parry = true;
                    if (input.Slot != null) seat.Slot = input.Slot;
                    seat.Target = input.Target;
                    seat.Ally = input.Ally;
                    seat.Movement.Wished = input.Heading;   // sous-lot 11.7
                    seat.Movement.Backward = input.Backward;   // sous-lot 11.9
                    break;
                case PingMessage ping:
                    Send(client, new PongMessage(ping.ClientTime));
                    break;
                case ResyncRequestMessage _:
                    if (Phase != RoomPhase.Combat) break;
                    if (Now - client.LastResyncAt < _options.ResyncIntervalSeconds)
                    {
                        Send(client, new RejectedMessage($"état courant déjà demandé : au plus un par {_options.ResyncIntervalSeconds} s."));
                        break;
                    }
                    SendState(client);
                    break;
                case ByeMessage _:
                    Trace?.Invoke($"{seat.Player} quitte la place « {seat.Id} »");
                    client.Seat = null;
                    if (Phase == RoomPhase.Combat)
                    {
                        seat.Client = null;
                        seat.Abandoned = true;
                        seat.Token = null;
                        seat.ResetIntentions();
                    }
                    else seat.Free();
                    _clients.Remove(client);
                    client.Connection.Close("au revoir.");
                    BroadcastLobby();
                    EndIfEveryoneLeft();
                    break;
                default:
                    Fail(client, $"message « {message.Type} » inattendu.");
                    break;
            }
        }

        // --- Connexions perdues et délais ----------------------------------------------------------------------------------------

        private void Drop(Client client)
        {
            _clients.Remove(client);
            Seat? seat = client.Seat;
            client.Seat = null;
            Trace?.Invoke($"{client.Connection.RemoteAddress} déconnecté ({client.Connection.CloseReason})");
            if (seat is null || seat.Client != client) return;
            if (Phase == RoomPhase.Combat)
            {
                // Décision du 19/10/2026 : le combat continue, le personnage est inactif (garde relâchée) et la place reste réservée.
                seat.Client = null;
                seat.DisconnectedAt = Now;
                seat.ResetIntentions();
            }
            else seat.Free();
            BroadcastLobby();
        }

        private void ExpireDisconnected()
        {
            if (Phase != RoomPhase.Combat) return;
            bool changed = false;
            foreach (Seat seat in _seats)
                if (seat.Client is null && !seat.Abandoned && seat.DisconnectedAt is double at && Now - at >= _options.ReconnectSeconds)
                {
                    seat.Abandoned = true;
                    seat.Token = null;
                    changed = true;
                    Trace?.Invoke($"place « {seat.Id} » abandonnée : pas de retour en {_options.ReconnectSeconds} s");
                }
            if (changed)
            {
                BroadcastLobby();
                EndIfEveryoneLeft();
            }
        }

        /// <summary>Plus aucun joueur présent ni attendu : le combat s'arrête et la salle se vide.</summary>
        private void EndIfEveryoneLeft()
        {
            if (Phase != RoomPhase.Combat || _seats.Any(s => s.Client != null || (!s.Abandoned && s.Taken))) return;
            EndCombat("tous les joueurs sont partis.");
        }

        // --- Combat ----------------------------------------------------------------------------------------------------------

        private void TryStart()
        {
            if (Phase == RoomPhase.Combat) return;
            // Sous-lot 11.7 : avec l'arrivée pendant le combat, il commence dès que les joueurs présents sont prêts ; les places libres
            // restent hors du combat de départ (leur personnage entrera avec son joueur).
            bool ready = _options.JoinDuringCombat
                ? _seats.Any(s => s.Taken) && _seats.Where(s => s.Taken).All(s => s.Client != null && s.Ready)
                : _seats.All(s => s.Taken && s.Client != null && s.Ready);
            if (!ready) return;
            IEnumerable<CombatantSetup> combatants = _scenario.Combatants
                .Where(c => !(c is CharacterSetup free && _seats.Any(s => s.Id == free.Id && !s.Taken)))
                .Select(c => c is CharacterSetup ch && _seats.FirstOrDefault(s => s.Id == ch.Id) is { } seat
                    ? new CharacterSetup(ch.Id, ch.Team, ch.Class, seat.Build!, ch.Initiative, ch.Placement, ch.ArrivalMs)
                    : c);
            var setup = new CombatSetup(_scenario.Seed, _options.DurationMs ?? _scenario.DurationMs, _scenario.Stats, _scenario.Damage,
                _scenario.Progression, _scenario.Combat, combatants, Array.Empty<CombatCommand>(), _scenario.Data, _scenario.Space, _scenario.Zone);
            void Refuse(string reason)
            {
                foreach (Seat seat in _seats)
                {
                    seat.Ready = false;
                    if (seat.Client != null) Send(seat.Client, new RejectedMessage(Protocol.Text(Protocol.CombatRefused + reason)));
                }
                Trace?.Invoke("combat refusé : " + reason);
                BroadcastLobby();
            }
            IReadOnlyList<RuleViolation> violations = setup.Validate();
            if (violations.Count > 0)
            {
                Refuse(violations[0].ToString());
                return;
            }
            var log = new HashingCombatLog();
            var engine = new CombatEngine(setup, log);
            // Revue Codex de la PR #19 : l'état courant d'un retour (sous-lot 11.7 ; au lot 10, l'historique) doit tenir dans un message du
            // serveur ; sinon le combat est refusé avant de commencer, plutôt que de couper le joueur au moment de revenir. Le majorant
            // porte sur l'en-tête du combat complété (toutes les commandes admises) ; l'instantané, lui, est contrôlé à l'envoi.
            // Sous-lot 11.7 : avec l'arrivée pendant le combat, le majorant compte aussi les personnages des places libres (build du
            // scénario, arrivée au dernier tick) ; l'état réel reste contrôlé à l'envoi.
            CombatSetup bounded = setup;
            if (_options.JoinDuringCombat && _seats.Any(s => !s.Taken))
            {
                long last = checked(Math.Max(1, setup.DurationMs / TickMs) * TickMs);
                IEnumerable<CombatantSetup> joiners = _scenario.Combatants.OfType<CharacterSetup>().Where(c => _seats.Any(s => s.Id == c.Id && !s.Taken))
                    .Select(c => new CharacterSetup(c.Id, c.Team, c.Class, c.Build, c.Initiative, c.Placement, last));
                bounded = new CombatSetup(setup.Seed, setup.DurationMs, setup.Stats, setup.Damage, setup.Progression, setup.Combat,
                    setup.Combatants.Concat(joiners), setup.Commands, setup.Data, setup.Space, setup.Zone);
            }
            long bound = StateBound(bounded, TickMs);
            // Revue Codex de la PR #35 (4e tour) : le majorant porte sur l'en-tête ; l'instantané de départ y est ajouté tel qu'il serait
            // envoyé (sa croissance en cours de combat, avec la file, reste contrôlée à l'envoi).
            string? startSnapshot = SnapshotText(engine);
            bound = startSnapshot is null ? long.MaxValue : checked(bound + SnapshotExtra(startSnapshot));
            if (bound > Protocol.MaxServerMessageBytes)
            {
                Refuse($"un état courant pourrait atteindre {bound} octets, au-delà de la limite de {Protocol.MaxServerMessageBytes} octets du protocole " +
                    "(plafond de commandes ou identifiants trop longs).");
                return;
            }
            _setup = setup;
            _obstacles = CombatDisplay.Obstacles(setup.Space);
            _sightObstacles = CombatDisplay.SightObstacles(setup.Space);
            _log = log;
            _engine = engine;
            _accumulated = 0;
            Phase = RoomPhase.Combat;
            foreach (Seat seat in _seats)
            {
                seat.ResetIntentions();
                seat.Movement.Reset();
                seat.InCombat = seat.Taken;
                seat.Target = null;
                seat.Ally = null;
            }
            Trace?.Invoke($"combat commencé ({_seats.Count} joueurs, {setup.DurationMs} ms)");
            foreach (Seat seat in _seats)
                if (seat.Client != null) Send(seat.Client, new StartMessage(_log.Header!));
            BroadcastLobby();
        }

        private void StepTick()
        {
            CombatEngine engine = _engine!;
            long tick = engine.CurrentTick;
            long atMs = checked(tick * TickMs);
            int before = engine.SubmittedCommands.Count;
            foreach (Seat seat in _seats)
            {
                if (!seat.InCombat) continue;   // sous-lot 11.7 : place libre, personnage hors du combat
                CombatantView me = engine.View(seat.Id);
                int? slot = Available(engine, seat, me, tick);
                IReadOnlyList<CombatCommand[]> groups = IntentionPlanner.Plan(me, atMs, seat.Guard, seat.Parry, slot, skill => TargetFor(engine, seat, me, skill));
                seat.Parry = false;
                seat.Slot = null;
                bool TrySubmit(CombatCommand[] group)
                {
                    try
                    {
                        engine.SubmitAll(group);
                        return true;
                    }
                    catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
                    {
                        // Plafond, file pleine : le groupe entier est refusé, rien n'est soumis ; le joueur est prévenu.
                        if (seat.Client != null) Send(seat.Client, new RejectedMessage(Protocol.Text(e.Message)));
                        return false;
                    }
                }
                int used = 0;
                foreach (CombatCommand[] group in groups)
                    if (TrySubmit(group)) used += group.Length;
                // Sous-lot 11.7 : marche et cible sélectionnée, comme le pont solo, dans la place qui reste au tick.
                string? target = EnemyTarget(engine, seat, me, _sightObstacles);
                seat.Movement.Submit(me, _setup!.Space != null, atMs, target, _setup.Combat.MaxCommandsPerActorPerTick - used, TrySubmit);
            }
            engine.Step();
            CombatCommand[] admitted = engine.SubmittedCommands.Skip(before).ToArray();
            var commands = new TickCommands(tick, admitted);
            var message = new TickMessage(commands, _log!.Hash);
            foreach (Seat seat in _seats)
                if (seat.Client != null) Send(seat.Client, message);
            if (engine.IsFinished)
                EndCombat(engine.Result!.EndReason switch
                {
                    CombatEndReason.DurationReached => "durée atteinte.",
                    CombatEndReason.TeamDefeated => "une équipe vaincue.",
                    _ => "combat interrompu : " + (engine.Result.AbortReason ?? "limite atteinte") + ".",
                });
        }

        /// <summary>
        /// Revue Codex de la PR #19 : emplacement demandé seulement si sa compétence semble lançable, avec le même contrôle que le pont
        /// temps réel (<see cref="IntentionPlanner.Unavailable"/>) ; sinon le joueur reçoit un refus motivé et rien n'est planifié pour
        /// cet emplacement (la garde n'est pas quittée), comme en solo.
        /// </summary>
        private int? Available(CombatEngine engine, Seat seat, CombatantView me, long tick)
        {
            if (seat.Slot is not int slot) return null;
            SkillDefinition? skill = IntentionPlanner.SkillAt(me, slot);
            string? why = skill is null ? "emplacement vide"
                : IntentionPlanner.Unavailable(me, skill, slot == RealtimeCombat.UltimateSlot, tick, _tickSeconds, _setup!.Combat,
                    skill.Combat!.Targeting != SkillTargeting.Enemy || TargetFor(engine, seat, me, skill) != null);
            if (why is null) return slot;
            if (seat.Client != null) Send(seat.Client, new RejectedMessage(Protocol.Text(skill is null ? why : $"{skill.Name} : {why}")));
            return null;
        }

        /// <summary>Cible d'une compétence : choix du joueur s'il est valide, sinon le premier ennemi vivant ou l'autre joueur.</summary>
        private static bool Alive(CombatEngine engine, string? id, Func<CombatantView, bool> accept)
        {
            if (id is null || !engine.CombatantIds.Contains(id)) return false;
            CombatantView view = engine.View(id);
            return !view.IsDead && view.IsPresent && accept(view);
        }

        /// <summary>
        /// Cible ennemie du joueur : celle qu'il a choisie si elle est présente et vivante (son client ne propose que des ennemis en vue ;
        /// la ligne de vue d'une compétence est contrôlée par le moteur), sinon le premier ennemi présent, vivant et, dans un combat
        /// positionné, en vue du joueur (revue Codex de la PR #34 : pas de cible ni de sélection cachée) ; sinon aucune.
        /// </summary>
        private static string? EnemyTarget(CombatEngine engine, Seat seat, CombatantView me, IReadOnlyList<ConvexPolygon>? obstacles) =>
            Alive(engine, seat.Target, v => v.Team != me.Team)
                ? seat.Target
                : engine.CombatantIds.FirstOrDefault(id => Alive(engine, id, v => v.Team != me.Team && CombatDisplay.InSight(me, v, obstacles)));

        private string? TargetFor(CombatEngine engine, Seat seat, CombatantView me, SkillDefinition skill)
        {
            bool Alive(string? id, Func<CombatantView, bool> accept) => CombatRoom.Alive(engine, id, accept);
            IReadOnlyList<ConvexPolygon>? obstacles = _sightObstacles;
            switch (skill.Combat!.Targeting)
            {
                case SkillTargeting.Enemy:
                    return EnemyTarget(engine, seat, me, obstacles);
                case SkillTargeting.Ally:
                    if (Alive(seat.Ally, v => v.Team == me.Team && v.Kind == CombatantKind.Character)) return seat.Ally;
                    return engine.CombatantIds.FirstOrDefault(id => id != me.Id && Alive(id, v => v.Team == me.Team && v.Kind == CombatantKind.Character)) ?? me.Id;
                default:
                    return null;
            }
        }

        private void EndCombat(string reason)
        {
            CombatEngine engine = _engine!;
            long tick = engine.CurrentTick;
            if (engine.Result is null) LastResult = null;
            else LastResult = engine.Result;
            // Journal rejouable : le combat de départ complété des commandes admises (les lignes, hors en-tête, sont celles du combat joué).
            CombatSetup completed = engine.CompletedSetup();   // sous-lot 11.4d : arrivées comprises
            var journal = new MemoryCombatLog();
            if (engine.IsFinished) CombatEngine.Run(completed, journal);
            LastJournal = engine.IsFinished ? journal.Lines : null;
            _engine = null;
            _setup = null;
            Phase = RoomPhase.Ended;
            Trace?.Invoke($"combat terminé au tick {tick} : {reason}");
            var end = new EndMessage(tick, Protocol.Text(reason));
            foreach (Seat seat in _seats)
            {
                if (seat.Client != null) Send(seat.Client, end);
                else seat.Free();   // absents et abandons : la place se libère
                seat.Ready = false;
                seat.ResetIntentions();
            }
            BroadcastLobby();
            if (LastJournal != null) CombatEnded?.Invoke(LastJournal);
        }

        /// <summary>
        /// Revue Codex de la PR #19, sous-lot 11.7 : taille maximale d'un état courant (<see cref="StateMessage"/>), hors instantané, en
        /// octets : l'en-tête du combat complété au pire, c'est-à-dire avec le plafond de commandes des règles, chacune la plus longue
        /// possible (compétence aux identifiants les plus longs, ou marche), à l'instant le plus tardif. Majorant : un vrai en-tête est
        /// toujours plus petit. L'instantané est contrôlé à l'envoi (<see cref="SendState"/>). Les commandes déjà dans le combat sont comptées
        /// telles quelles (revue Codex de la PR #35), le reste du plafond au pire.
        /// </summary>
        /// <summary>
        /// Revue Codex de la PR #35 (4e tour) : octets qu'un instantané ajoute à un état courant, par rapport à l'instantané factice
        /// <c>-</c> de <see cref="StateBound"/> (texte tel qu'encodé dans le message).
        /// </summary>
        internal static long SnapshotExtra(string snapshot)
        {
            string hash = MessageCodec.HashPrefix + new string('f', 16);
            return MessageCodec.Encode(new StateMessage("", snapshot, hash)).Length - MessageCodec.Encode(new StateMessage("", "-", hash)).Length;
        }

        internal static long StateBound(CombatSetup setup, int tickMs)
        {
            if (setup is null) throw new ArgumentNullException(nameof(setup));
            string Longest(IEnumerable<string> ids) => ids.OrderByDescending(id => id.Length).ThenBy(id => id, StringComparer.Ordinal).First();
            string actor = Longest(setup.Combatants.OfType<CharacterSetup>().Select(c => c.Id).DefaultIfEmpty("x"));
            string target = Longest(setup.Combatants.Select(c => c.Id));
            string skill = Longest(setup.Combatants.OfType<CharacterSetup>()
                .SelectMany(c => c.Build.ActiveSkills.Concat(c.Build.Ultimate is null ? Array.Empty<string>() : new[] { c.Build.Ultimate })).DefaultIfEmpty("x"));
            long lastTick = Math.Max(1, setup.DurationMs / tickMs);
            long atMs = checked(lastTick * tickMs);
            var diagonal = Fixed.Parse("-0.7071");
            CombatCommand[] worst =
            {
                new CombatCommand(atMs, actor, skill, target),
                new CombatCommand(atMs, actor, new GroundVector(diagonal, diagonal)),
            };
            string hash = MessageCodec.HashPrefix + new string('f', 16);
            // Revue Codex de la PR #35 (tour suivant) : les commandes déjà dans le combat (scénario, ou soumises avant une arrivée) sont
            // gardées telles quelles ; seules les places restantes sous le plafond sont comptées au pire (une commande pilotée vient
            // toujours d'un personnage, d'où le choix de l'acteur et de la compétence parmi eux).
            long Size(params CombatCommand[] added) => MessageCodec.Encode(new StateMessage(CombatLogHeader.Write(new CombatSetup(setup.Seed, setup.DurationMs,
                setup.Stats, setup.Damage, setup.Progression, setup.Combat, setup.Combatants, setup.Commands.Concat(added).ToList(), setup.Data, setup.Space, setup.Zone)),
                "-", hash)).Length;
            long remaining = Math.Max(0, (long)setup.Combat.MaxCommands - setup.Commands.Count);
            if (remaining == 0) return Size();
            CombatCommand longest = worst.OrderByDescending(c => Size(c)).First();
            long one = Size(longest);
            long two = Size(longest, longest);
            return checked(one + (remaining - 1) * (two - one));
        }

        /// <summary>
        /// Sous-lot 11.7 : état courant du combat (en-tête du combat complété, instantané, empreinte du journal) ; refusé s'il ne tient pas
        /// dans un message du serveur (connexion fermée avec la raison).
        /// </summary>
        private StateMessage? CurrentState()
        {
            CombatEngine engine = _engine!;
            return SnapshotText(engine) is { } snapshot ? new StateMessage(CombatLogHeader.Write(engine.CompletedSetup()), snapshot, _log!.Hash) : null;
        }

        /// <summary>
        /// Revue Codex de la PR #32 (37e tour) : texte de l'instantané du moteur, ou null si l'état est trop grand pour un instantané relisible
        /// (<c>CombatEngine.Snapshot</c> le refuse) : la salle refuse alors l'arrivée, le combat ou l'envoi, sans tomber.
        /// </summary>
        private static string? SnapshotText(CombatEngine engine)
        {
            try
            {
                return engine.Snapshot().Text;
            }
            catch (InvalidOperationException) when (!engine.IsFinished)
            {
                return null;
            }
        }

        private void SendState(Client client, StateMessage? state = null)
        {
            client.LastResyncAt = Now;
            state ??= CurrentState();
            if (state is null || MessageCodec.Encode(state).Length > Protocol.MaxServerMessageBytes)
            {
                Fail(client, $"état courant du combat au-delà de la limite de {Protocol.MaxServerMessageBytes} octets du protocole.");
                return;
            }
            Send(client, state);
        }

        /// <summary>
        /// Revue Codex de la PR #19 : arrêt de la salle (hôte fermé) : chaque connexion reçoit une erreur avec la raison, puis est fermée ;
        /// la salle ne garde plus aucune connexion.
        /// </summary>
        public void CloseAll(string reason)
        {
            string text = Protocol.Text(reason);
            foreach (Client client in _clients)
            {
                client.Connection.Send(new ErrorMessage(text));
                client.Connection.Close(text);
            }
            _clients.Clear();
        }

        // --- Envoi -----------------------------------------------------------------------------------------------------------

        private void BroadcastLobby()
        {
            var message = new LobbyMessage(Phase, _seats.Select(s => new SeatInfo(s.Id, s.Player, s.Client != null, s.Ready, s.BuildName)).ToArray());
            foreach (Client client in _clients)
                if (client.Seat != null) Send(client, message);
        }

        private static void Send(Client client, NetMessage message) => client.Connection.Send(message);

        /// <summary>Refus définitif : message d'erreur, puis fermeture (les messages en file partent encore).</summary>
        private void Fail(Client client, string reason)
        {
            string text = Protocol.Text(reason);
            client.Failed = true;
            Trace?.Invoke($"{client.Connection.RemoteAddress} refusé : {text}");
            client.Connection.Send(new ErrorMessage(text));
            client.Connection.Close(text);
        }

        private string NewToken()
        {
            if (_options.TokenSource != null)
            {
                string token = _options.TokenSource();
                if (!MessageCodec.IsToken(token)) throw new InvalidOperationException("Jeton de reconnexion mal formé.");
                return token;
            }
            var bytes = new byte[Protocol.TokenLength / 2];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            return string.Concat(bytes.Select(b => b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
        }

        /// <summary>Comparaison des jetons en temps constant (pas d'indice par la durée de réponse).</summary>
        private static bool FixedTimeEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
