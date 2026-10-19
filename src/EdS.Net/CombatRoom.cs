using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using EdS.Data;
using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
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
    }

    /// <summary>
    /// Sous-lot 10.2 : salle de combat autoritaire. Une place par personnage du scénario (deux Bastions en coopération), une salle d'attente
    /// (présentation, build, prêt), puis le combat : le serveur range les intentions de chaque joueur dans le prochain tick, les soumet au
    /// moteur par groupes tout ou rien, simule le tick et diffuse les commandes admises avec l'empreinte du journal. Un joueur déconnecté
    /// laisse le combat continuer (personnage inactif, garde relâchée) et a <see cref="RoomOptions.ReconnectSeconds"/> pour revenir avec
    /// son jeton (historique complet renvoyé). Le temps est celui des appels à <see cref="Advance"/> : aucun fil, aucune horloge ; un
    /// hôte (<see cref="RoomHost"/>) l'anime en temps réel, les tests en temps virtuel.
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

            public bool Taken => Player != null;

            public void ResetIntentions()
            {
                Guard = false;
                Parry = false;
                Slot = null;
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
                ResetIntentions();
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
        private HashingCombatLog? _log;
        private readonly List<TickCommands> _history = new List<TickCommands>();
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
            if (Phase == RoomPhase.Combat)
            {
                Fail(client, "combat en cours : on ne rejoint pas une partie commencée (se reconnecter avec son jeton, ou attendre la fin).");
                return;
            }
            Seat? seat = _seats.FirstOrDefault(s => !s.Taken);
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
            seat.Player = hello.Name;
            seat.Build = build;
            seat.BuildName = Protocol.Text(name, Protocol.MaxIdLength);
            seat.Ready = false;
            seat.Token = NewToken();
            Attach(seat, client);
            Trace?.Invoke($"{hello.Name} prend la place « {seat.Id} » ({seat.BuildName})");
            BroadcastLobby();
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
            if (Phase == RoomPhase.Combat) SendResync(client);
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
                    break;
                case PingMessage ping:
                    Send(client, new PongMessage(ping.ClientTime));
                    break;
                case ResyncRequestMessage _:
                    if (Phase != RoomPhase.Combat) break;
                    if (Now - client.LastResyncAt < _options.ResyncIntervalSeconds)
                    {
                        Send(client, new RejectedMessage($"historique déjà demandé : au plus un par {_options.ResyncIntervalSeconds} s."));
                        break;
                    }
                    SendResync(client);
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
            if (Phase == RoomPhase.Combat || !_seats.All(s => s.Taken && s.Client != null && s.Ready)) return;
            IEnumerable<CombatantSetup> combatants = _scenario.Combatants.Select(c => c is CharacterSetup ch && _seats.FirstOrDefault(s => s.Id == ch.Id) is { } seat
                ? new CharacterSetup(ch.Id, ch.Team, ch.Class, seat.Build!, ch.Initiative)
                : c);
            var setup = new CombatSetup(_scenario.Seed, _options.DurationMs ?? _scenario.DurationMs, _scenario.Stats, _scenario.Damage,
                _scenario.Progression, _scenario.Combat, combatants, Array.Empty<CombatCommand>(), _scenario.Data);
            void Refuse(string reason)
            {
                foreach (Seat seat in _seats)
                {
                    seat.Ready = false;
                    if (seat.Client != null) Send(seat.Client, new RejectedMessage(Protocol.Text("combat refusé : " + reason)));
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
            // Revue Codex de la PR #19 : l'historique d'un retour doit tenir dans un message du serveur ; sinon le combat est refusé
            // avant de commencer, plutôt que de couper le joueur au moment de revenir.
            long bound = ResyncBound(setup, log.Header!, TickMs);
            if (bound > Protocol.MaxServerMessageBytes)
            {
                Refuse($"un historique pourrait atteindre {bound} octets, au-delà de la limite de {Protocol.MaxServerMessageBytes} octets du protocole " +
                    "(plafond de commandes ou identifiants trop longs).");
                return;
            }
            _setup = setup;
            _log = log;
            _engine = engine;
            _history.Clear();
            _accumulated = 0;
            Phase = RoomPhase.Combat;
            foreach (Seat seat in _seats)
            {
                seat.ResetIntentions();
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
                CombatantView me = engine.View(seat.Id);
                int? slot = Available(engine, seat, me, tick);
                IReadOnlyList<CombatCommand[]> groups = IntentionPlanner.Plan(me, atMs, seat.Guard, seat.Parry, slot, skill => TargetFor(engine, seat, me, skill));
                seat.Parry = false;
                seat.Slot = null;
                foreach (CombatCommand[] group in groups)
                {
                    try
                    {
                        engine.SubmitAll(group);
                    }
                    catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
                    {
                        // Plafond, file pleine : le groupe entier est refusé, rien n'est soumis ; le joueur est prévenu.
                        if (seat.Client != null) Send(seat.Client, new RejectedMessage(Protocol.Text(e.Message)));
                    }
                }
            }
            engine.Step();
            CombatCommand[] admitted = engine.SubmittedCommands.Skip(before).ToArray();
            var commands = new TickCommands(tick, admitted);
            if (admitted.Length > 0) _history.Add(commands);
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
        private static string? TargetFor(CombatEngine engine, Seat seat, CombatantView me, SkillDefinition skill)
        {
            bool Alive(string? id, Func<CombatantView, bool> accept)
            {
                if (id is null || !engine.CombatantIds.Contains(id)) return false;
                CombatantView view = engine.View(id);
                return !view.IsDead && accept(view);
            }
            switch (skill.Combat!.Targeting)
            {
                case SkillTargeting.Enemy:
                    if (Alive(seat.Target, v => v.Team != me.Team)) return seat.Target;
                    return engine.CombatantIds.FirstOrDefault(id => Alive(id, v => v.Team != me.Team));
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
            var setup = _setup!;
            var completed = new CombatSetup(setup.Seed, setup.DurationMs, setup.Stats, setup.Damage, setup.Progression, setup.Combat, setup.Combatants,
                setup.Commands.Concat(engine.SubmittedCommands), setup.Data);
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
        /// Revue Codex de la PR #19 : taille maximale de l'historique (<see cref="ResyncMessage"/>) d'un combat, en octets : son en-tête,
        /// puis le plafond de commandes des règles, chacune dans son propre tick, avec les identifiants les plus longs du combat et
        /// l'instant le plus tardif. Majorant : un vrai historique est toujours plus petit.
        /// </summary>
        internal static long ResyncBound(CombatSetup setup, string header, int tickMs)
        {
            if (setup is null) throw new ArgumentNullException(nameof(setup));
            if (header is null) throw new ArgumentNullException(nameof(header));
            string Longest(IEnumerable<string> ids) => ids.OrderByDescending(id => id.Length).ThenBy(id => id, StringComparer.Ordinal).First();
            string actor = Longest(setup.Combatants.OfType<CharacterSetup>().Select(c => c.Id));
            string target = Longest(setup.Combatants.Select(c => c.Id));
            string skill = Longest(setup.Combatants.OfType<CharacterSetup>()
                .SelectMany(c => c.Build.ActiveSkills.Concat(c.Build.Ultimate is null ? Array.Empty<string>() : new[] { c.Build.Ultimate })).DefaultIfEmpty("x"));
            long lastTick = Math.Max(1, setup.DurationMs / tickMs);
            var worst = new CombatCommand(checked(lastTick * tickMs), actor, skill, target);
            string hash = MessageCodec.HashPrefix + new string('f', 16);
            long Size(params TickCommands[] history) => MessageCodec.Encode(new ResyncMessage(header, lastTick, history, hash)).Length;
            long empty = Size();
            long one = Size(new TickCommands(lastTick, new[] { worst }));
            long two = Size(new TickCommands(lastTick - 1, new[] { worst }), new TickCommands(lastTick, new[] { worst }));
            long commands = Math.Max(1, setup.Combat.MaxCommands);
            return checked(one + (commands - 1) * (two - one));
        }

        private void SendResync(Client client)
        {
            client.LastResyncAt = Now;
            Send(client, new ResyncMessage(_log!.Header!, _engine!.CurrentTick, _history.ToArray(), _log.Hash));
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
