using System;
using EdS.Data;
using EdS.Runtime;

namespace EdS.Net
{
    /// <summary>Où en est un client réseau.</summary>
    public enum ClientStatus
    {
        /// <summary>Présentation envoyée, accueil du serveur attendu.</summary>
        Connecting = 0,

        /// <summary>Dans la salle d'attente (avant un combat ou après).</summary>
        Lobby = 1,

        /// <summary>Combat en cours.</summary>
        Combat = 2,

        /// <summary>Connexion perdue pendant un combat : retour possible avec le jeton (<see cref="NetworkClient.Reconnect"/>).</summary>
        Disconnected = 3,

        /// <summary>Retour demandé, accueil et historique du serveur attendus.</summary>
        Reconnecting = 4,

        /// <summary>Refusé par le serveur, faute de protocole, connexion perdue hors combat, désynchronisation persistante.</summary>
        Failed = 5,

        /// <summary>Le joueur est parti (<see cref="NetworkClient.Leave"/>).</summary>
        Left = 6,
    }

    /// <summary>Réglages d'un client.</summary>
    public sealed class NetworkClientOptions
    {
        /// <summary>Intervalle entre deux mesures du temps aller-retour.</summary>
        public double PingIntervalSeconds { get; set; } = 1;

        /// <summary>
        /// Intervalle minimal entre deux envois d'intentions : les changements d'une même image et des suivantes sont regroupés, ce qui
        /// garde le client loin de la limite de débit du serveur (60 messages par seconde) sans retarder une parade de plus de 25 ms.
        /// </summary>
        public double MinInputIntervalSeconds { get; set; } = 0.025;

        /// <summary>Délai avant de redemander un historique qui n'arrive pas.</summary>
        public double ResyncTimeoutSeconds { get; set; } = 5;

        /// <summary>Délai avant de redemander un historique qui n'a pas suffi (le serveur en accepte un par seconde).</summary>
        public double ResyncRetrySeconds { get; set; } = 1.1;

        /// <summary>Demandes d'historique consécutives au plus sans retrouver l'empreinte du serveur ; au-delà, le client abandonne.</summary>
        public int MaxResyncAttempts { get; set; } = 3;

        /// <summary>Délai du serveur pour revenir après une déconnexion (décision du 19/10/2026 : 30 s), affiché au joueur.</summary>
        public double ReconnectSeconds { get; set; } = 30;
    }

    /// <summary>
    /// Sous-lot 10.3 : client du serveur autoritaire. Se présente (nom, build), suit la salle d'attente, se déclare prêt, puis joue le
    /// combat (<see cref="Combat"/>) : le moteur local rejoue les commandes du serveur et contrôle l'empreinte, les intentions du joueur
    /// partent au serveur. Mesure le temps aller-retour. Une connexion perdue pendant un combat laisse le jeton : <see cref="Reconnect"/>
    /// avec une nouvelle connexion reprend la même place et l'historique complet. Le temps est celui des appels à <see cref="Advance"/> :
    /// aucun fil, aucune horloge ; Unity l'appelle à chaque image, les tests en temps virtuel. Aucune exception sur des messages reçus.
    /// </summary>
    public sealed class NetworkClient
    {
        private readonly GameData _data;
        private INetConnection _connection;
        private double _lastPingAt = double.NegativeInfinity;
        private long _lastPingTime = long.MinValue;
        private long _pendingBuildPing;
        private bool _pendingBuildRejected;
        private double _lastInputAt = double.NegativeInfinity;
        private double? _disconnectedAt;

        /// <param name="connection">Connexion ouverte vers le serveur (TCP ou réseau simulé).</param>
        /// <param name="name">Nom du joueur (1 à 24 caractères, sans caractère de contrôle ni espace en bord).</param>
        public NetworkClient(GameData data, INetConnection connection, string name, BuildChoice build, NetworkClientOptions? options = null)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            if (build is null) throw new ArgumentNullException(nameof(build));
            if (!MessageCodec.IsText(name, Protocol.MaxNameLength))
                throw new ArgumentException($"Nom de joueur invalide : 1 à {Protocol.MaxNameLength} caractères, sans caractère de contrôle ni espace en bord.", nameof(name));
            Name = name;
            Options = options ?? new NetworkClientOptions();
            _connection.Send(Checked(new HelloMessage(Protocol.Version, name, build), nameof(build)));
            AcceptedBuild = build;
        }

        /// <summary>
        /// Message vérifié avant l'envoi par le même lecteur strict que le serveur (identifiants, rangs, nombre de talents, taille) : une
        /// erreur de l'appelant est une exception ici, pas une déconnexion pour faute de protocole.
        /// </summary>
        private static NetMessage Checked(NetMessage message, string parameter)
        {
            byte[] payload = MessageCodec.Encode(message);
            if (payload.Length > Protocol.MaxClientMessageBytes)
                throw new ArgumentException($"Message de {payload.Length} octets, le serveur en accepte {Protocol.MaxClientMessageBytes} au plus.", parameter);
            if (MessageCodec.Decode(payload, MessageDirection.ClientToServer, out string? error) is null)
                throw new ArgumentException("Message refusé par le protocole : " + error, parameter);
            return message;
        }

        public string Name { get; }

        public NetworkClientOptions Options { get; }

        public ClientStatus Status { get; private set; } = ClientStatus.Connecting;

        /// <summary>Temps écoulé du client (somme des appels à <see cref="Advance"/>), en secondes.</summary>
        public double Now { get; private set; }

        /// <summary>Place attribuée par le serveur (identifiant du personnage) ; null avant l'accueil.</summary>
        public string? SeatId { get; private set; }

        /// <summary>Jeton de reconnexion ; à garder pour revenir après une coupure.</summary>
        public string? Token { get; private set; }

        public int TickMs { get; private set; }

        /// <summary>Dernier état de la salle reçu (places, joueurs, prêts).</summary>
        public LobbyMessage? Lobby { get; private set; }

        /// <summary>Combat en cours ou dernier combat ; null avant le premier.</summary>
        public NetworkCombat? Combat { get; private set; }

        /// <summary>Erreur qui a arrêté le client, raison de la dernière coupure, ou fin de combat non vérifiée (le client reste dans la salle).</summary>
        public string? Error { get; private set; }

        /// <summary>Dernier refus du serveur (build, prêt, commande…).</summary>
        public string? LastRejected { get; private set; }

        /// <summary>
        /// Revue Codex de la PR #21 (tour 4) : build que le serveur garde pour ce joueur d'après ses réponses (celui de la présentation au
        /// départ). Un build demandé n'y entre qu'une fois accepté : une demande refusée (par exemple arrivée au serveur après le début du
        /// combat) ne le change pas.
        /// </summary>
        public BuildChoice AcceptedBuild { get; private set; }

        /// <summary>Build demandé dont la réponse du serveur n'est pas encore arrivée ; null sinon.</summary>
        public BuildChoice? PendingBuild { get; private set; }

        /// <summary>Dernier temps aller-retour mesuré, en millisecondes ; null avant la première mesure.</summary>
        public double? RttMs { get; private set; }

        /// <summary>Temps aller-retour lissé (moyenne mobile, 1/8 par mesure), en millisecondes.</summary>
        public double? SmoothedRttMs { get; private set; }

        /// <summary>Secondes écoulées depuis la coupure ; null si le client n'est pas coupé.</summary>
        public double? DisconnectedSeconds => _disconnectedAt is double at ? Now - at : (double?)null;

        /// <summary>Un retour est encore possible : coupé pendant un combat, avec un jeton, dans le délai du serveur.</summary>
        public bool CanReconnect =>
            (Status == ClientStatus.Disconnected || Status == ClientStatus.Reconnecting) && Token != null && DisconnectedSeconds < Options.ReconnectSeconds;

        // --- Salle d'attente ----------------------------------------------------------------------------------------------

        /// <summary>
        /// Demande un build (salle d'attente seulement ; le serveur valide et peut refuser). Rend vrai si la demande est partie : faux hors
        /// de la salle d'attente ou si une demande attend encore sa réponse (<see cref="PendingBuild"/>).
        /// </summary>
        /// <remarks>
        /// Revue Codex de la PR #21 (tour 4) : une mesure du temps aller-retour part juste derrière la demande. Le serveur traite les
        /// messages dans l'ordre et répond au build (salle mise à jour, ou refus) avant de répondre à la mesure : à l'arrivée de cette
        /// réponse, le build est accepté (<see cref="AcceptedBuild"/>) sauf si un refus est arrivé entre-temps.
        /// </remarks>
        public bool SetBuild(BuildChoice build)
        {
            if (build is null) throw new ArgumentNullException(nameof(build));
            NetMessage message = Checked(new BuildMessage(build), nameof(build));
            if (Status != ClientStatus.Lobby || PendingBuild != null) return false;
            _connection.Send(message);
            _pendingBuildPing = SendPing();
            _pendingBuildRejected = false;
            PendingBuild = build;
            return true;
        }

        /// <summary>Envoie une mesure du temps aller-retour ; son instant (ms) croît strictement d'une mesure à l'autre.</summary>
        private long SendPing()
        {
            long time = checked((long)Math.Round(Now * 1000));
            if (time <= _lastPingTime) time = checked(_lastPingTime + 1);
            _lastPingTime = time;
            _lastPingAt = Now;
            _connection.Send(new PingMessage(time));
            return time;
        }

        public void SetReady(bool ready)
        {
            if (Status == ClientStatus.Lobby) _connection.Send(new ReadyMessage(ready));
        }

        /// <summary>Quitte la salle : la place est rendue (abandonnée si un combat est en cours).</summary>
        public void Leave()
        {
            if (Status == ClientStatus.Left) return;
            if (_connection.IsOpen)
            {
                _connection.Send(new ByeMessage());
                _connection.Close("départ.");
            }
            Status = ClientStatus.Left;
            _disconnectedAt = null;
        }

        /// <summary>
        /// Revient après une coupure avec une nouvelle connexion : même place, garde relâchée, historique complet renvoyé par le serveur.
        /// Refusé si le client n'est pas coupé pendant un combat ou n'a pas de jeton, et si un retour est déjà demandé (revue Codex de la
        /// PR #20, tour 5) : une tentative dont la connexion se ferme ramène à <see cref="ClientStatus.Disconnected"/>, d'où l'on recommence.
        /// </summary>
        public void Reconnect(INetConnection connection)
        {
            if (connection is null) throw new ArgumentNullException(nameof(connection));
            // Revue Codex de la PR #20 (tour 5) : pas de remplacement d'une demande de retour en cours ; le serveur refuserait le
            // jeton tant que l'ancienne connexion n'est pas détachée. Une demande qui échoue ramène le client à Disconnected.
            if (Status != ClientStatus.Disconnected)
                throw new InvalidOperationException($"Retour impossible : client {Status}, pas coupé pendant un combat (ou retour déjà demandé).");
            if (Token is null || Combat is null) throw new InvalidOperationException("Retour impossible : aucun jeton de reconnexion.");
            if (_connection.IsOpen) _connection.Close("remplacée par une nouvelle connexion.");
            _connection = connection;
            _lastPingAt = double.NegativeInfinity;
            Combat.PrepareReconnect();
            Status = ClientStatus.Reconnecting;
            _connection.Send(new ReconnectMessage(Protocol.Version, Token));
        }

        // --- Temps --------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Avance le temps du client : lit les messages reçus (ticks rejoués dans l'ordre), envoie les intentions et les mesures, détecte
        /// une coupure ; rend le nombre de ticks rejoués.
        /// </summary>
        public int Advance(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            Now += seconds;
            Combat?.Elapse(seconds);
            if (Status == ClientStatus.Failed || Status == ClientStatus.Left || Status == ClientStatus.Disconnected) return 0;
            int ticks = Receive();
            if (Status == ClientStatus.Failed) return ticks;
            if (!_connection.IsOpen)
            {
                Lost(_connection.CloseReason ?? "connexion fermée.");
                return ticks;
            }
            if ((Status == ClientStatus.Combat || Status == ClientStatus.Reconnecting) && Combat != null)
            {
                // Les messages partent après la demande de retour : le serveur les lit une fois la place reprise.
                if (Combat.AwaitingResync && Now >= Combat.ResyncRetryAt) RequestResync(Combat);
                if (Status == ClientStatus.Combat && Now - _lastInputAt >= Options.MinInputIntervalSeconds && Combat.TakeInput() is { } input)
                {
                    _connection.Send(input);
                    _lastInputAt = Now;
                }
            }
            if (Status != ClientStatus.Failed && Now - _lastPingAt >= Options.PingIntervalSeconds) SendPing();
            return ticks;
        }

        private int Receive()
        {
            int ticks = 0;
            while (_connection.TryReceive(MessageDirection.ServerToClient, out NetMessage? message, out string? error))
            {
                if (message is null)
                {
                    Fail("message du serveur refusé : " + error);
                    return ticks;
                }
                ticks += Handle(message);
                if (Status == ClientStatus.Failed) return ticks;
            }
            return ticks;
        }

        /// <summary>
        /// Traite un message du serveur ; rend le nombre de ticks rejoués et retenus (1 pour un tick, tous ceux de l'historique pour une
        /// resynchronisation réussie, revue Codex de la PR #20).
        /// </summary>
        private int Handle(NetMessage message)
        {
            switch (message)
            {
                case WelcomeMessage welcome:
                    if (welcome.ProtocolVersion != Protocol.Version)
                    {
                        Fail($"protocole {welcome.ProtocolVersion} du serveur, version {Protocol.Version} attendue.");
                        return 0;
                    }
                    if (SeatId != null && welcome.Seat != SeatId)
                    {
                        Fail($"place « {welcome.Seat} » rendue au retour, « {SeatId} » attendue.");
                        return 0;
                    }
                    SeatId = welcome.Seat;
                    Token = welcome.Token;
                    TickMs = welcome.TickMs;
                    if (Status == ClientStatus.Connecting) Status = ClientStatus.Lobby;
                    // Place reprise : le délai du serveur repart de zéro à la prochaine coupure.
                    if (Status == ClientStatus.Reconnecting) _disconnectedAt = null;
                    return 0;
                case LobbyMessage lobby:
                    Lobby = lobby;
                    // Retour après la fin du combat : plus d'historique à attendre.
                    if (Status == ClientStatus.Reconnecting && lobby.Phase != RoomPhase.Combat) Back(ClientStatus.Lobby);
                    return 0;
                case StartMessage start:
                    if (SeatId is null)
                    {
                        Fail("début de combat avant l'accueil.");
                        return 0;
                    }
                    NetworkCombat? combat = NetworkCombat.Create(this, _data, start.Header, SeatId, out string? error);
                    if (combat is null)
                    {
                        Fail(error!);
                        return 0;
                    }
                    Combat = combat;
                    Status = ClientStatus.Combat;
                    Error = null;   // une fin non vérifiée du combat précédent ne concerne plus celui-ci
                    _lastInputAt = double.NegativeInfinity;
                    return 0;
                case TickMessage tick:
                    if (Status != ClientStatus.Combat || Combat is null) return 0;   // en attente de l'historique, ou hors combat
                    return Combat.ApplyTick(tick) ? 1 : 0;
                case ResyncMessage resync:
                    return ApplyResync(resync);
                case RejectedMessage rejected:
                    LastRejected = rejected.Reason;
                    // Un refus arrivé entre la demande de build et la réponse à sa mesure est celui du build, sauf un combat refusé au départ.
                    if (PendingBuild != null && !rejected.Reason.StartsWith(Protocol.CombatRefused, StringComparison.Ordinal)) _pendingBuildRejected = true;
                    if (Status == ClientStatus.Combat && Combat != null && SeatId != null)
                        Combat.AddFeedback(new CombatFeedback(FeedbackKind.Rejected, SeatId, 0, rejected.Reason));
                    return 0;
                case PongMessage pong:
                    if (PendingBuild != null && pong.ClientTime >= _pendingBuildPing && pong.ClientTime <= _lastPingTime)
                    {
                        if (!_pendingBuildRejected) AcceptedBuild = PendingBuild;
                        PendingBuild = null;
                    }
                    double rtt = Now * 1000 - pong.ClientTime;
                    if (rtt < 0 || rtt > 3_600_000) return 0;   // réponse qui ne vient pas d'un ping de ce client
                    RttMs = rtt;
                    SmoothedRttMs = SmoothedRttMs is double s ? s + (rtt - s) / 8 : rtt;
                    return 0;
                case EndMessage end:
                    if (Combat != null && (Status == ClientStatus.Combat || Status == ClientStatus.Reconnecting))
                    {
                        Combat.End(end.Tick, end.Reason);
                        Back(ClientStatus.Lobby);
                        // Revue Codex de la PR #20 : une fin qui ne concorde pas avec le combat local n'est pas acceptée en silence.
                        if (Combat.EndProblem != null) Error = Protocol.Text("fin du combat non vérifiée : " + Combat.EndProblem);
                    }
                    return 0;
                case ErrorMessage failure:
                    Fail("refusé par le serveur : " + failure.Reason);
                    return 0;
                default:
                    Fail($"message « {message.Type} » inattendu du serveur.");
                    return 0;
            }
        }

        private int ApplyResync(ResyncMessage resync)
        {
            if (SeatId is null || (Status != ClientStatus.Combat && Status != ClientStatus.Reconnecting)) return 0;
            if (Combat is null || Combat.Header != resync.Header)
            {
                // Retour dans un combat commencé sans nous (client relancé) : il se lit dans l'en-tête de l'historique.
                NetworkCombat? combat = NetworkCombat.Create(this, _data, resync.Header, SeatId, out string? error);
                if (combat is null)
                {
                    Fail(error!);
                    return 0;
                }
                Combat = combat;
            }
            if (Combat.ApplyResync(resync, out int replayed))
            {
                Back(ClientStatus.Combat);
                return replayed;
            }
            if (Combat.ResyncAttempts >= Options.MaxResyncAttempts)
                Fail($"désynchronisation persistante après {Combat.ResyncAttempts} historiques : {Combat.LastDesync}");
            return 0;
        }

        /// <summary>Demande l'historique complet au serveur (divergence, tick manquant, historique perdu).</summary>
        internal void RequestResync(NetworkCombat combat)
        {
            if (combat != Combat || Status == ClientStatus.Failed) return;
            if (combat.ResyncAttempts >= Options.MaxResyncAttempts)
            {
                Fail($"désynchronisation persistante après {combat.ResyncAttempts} historiques : {combat.LastDesync}");
                return;
            }
            combat.MarkResyncRequested();
            _connection.Send(new ResyncRequestMessage());
        }

        private void Back(ClientStatus status)
        {
            Status = status;
            _disconnectedAt = null;
            Error = null;
        }

        private void Lost(string reason)
        {
            Error = reason;
            // Revue Codex de la PR #21 (tour 5) : une demande de build sans réponse à la coupure n'a pas été acceptée. Le serveur traite build,
            // mesure puis prêt dans l'ordre : un build accepté remet « prêt » à faux, et le combat ne peut commencer qu'après un nouveau
            // « prêt », donc après la réponse à la mesure. Un combat commencé avant cette réponse a refusé le build (ou ne l'a jamais reçu).
            // Sans cela, une mesure envoyée sur la connexion du retour validerait ce build.
            PendingBuild = null;
            bool inCombat = (Status == ClientStatus.Combat || Status == ClientStatus.Reconnecting) && Combat != null && !Combat.IsFinished && Token != null;
            if (!inCombat)
            {
                Status = ClientStatus.Failed;
                return;
            }
            if (_disconnectedAt is null) _disconnectedAt = Now;
            Status = ClientStatus.Disconnected;
        }

        private void Fail(string reason)
        {
            Error = Protocol.Text(reason);
            Status = ClientStatus.Failed;
            _disconnectedAt = null;
            if (_connection.IsOpen) _connection.Close(Error);
        }
    }
}
