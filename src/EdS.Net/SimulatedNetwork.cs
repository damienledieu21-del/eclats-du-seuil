using System;
using System.Collections.Generic;
using EdS.Simulation.Randomness;

namespace EdS.Net
{
    /// <summary>Qualité d'un lien simulé : latence d'un sens et gigue, en millisecondes.</summary>
    public sealed class LinkProfile
    {
        public static readonly LinkProfile Perfect = new LinkProfile(0, 0);

        /// <summary>Latence d'un sens (le temps aller-retour en vaut le double).</summary>
        public double LatencyMs { get; }

        /// <summary>Retard supplémentaire tiré entre 0 et cette valeur pour chaque message (l'ordre d'arrivée est conservé, comme en TCP).</summary>
        public double JitterMs { get; }

        public LinkProfile(double latencyMs, double jitterMs)
        {
            if (double.IsNaN(latencyMs) || latencyMs < 0 || double.IsInfinity(latencyMs)) throw new ArgumentOutOfRangeException(nameof(latencyMs));
            if (double.IsNaN(jitterMs) || jitterMs < 0 || double.IsInfinity(jitterMs)) throw new ArgumentOutOfRangeException(nameof(jitterMs));
            LatencyMs = latencyMs;
            JitterMs = jitterMs;
        }
    }

    /// <summary>
    /// Lot 10 : réseau simulé, déterministe, pour les tests et les mesures : temps virtuel avancé par l'appelant, latence et gigue par
    /// lien (tirage <see cref="Pcg32"/>, même graine = mêmes délais), ordre d'arrivée conservé dans chaque sens comme en TCP, coupures
    /// à la demande, mêmes limites de taille que le transport TCP. Aucun fil, aucune horloge réelle.
    /// </summary>
    public sealed class SimulatedNetwork
    {
        private readonly Pcg32 _random;
        private readonly Dictionary<string, SimulatedListener> _listeners = new Dictionary<string, SimulatedListener>(StringComparer.Ordinal);
        private readonly List<InFlight> _inFlight = new List<InFlight>();
        private long _sequence;

        private sealed class InFlight
        {
            public double DeliverAt;
            public long Sequence;
            public SimulatedConnection To = null!;
            public byte[]? Payload;        // null : fermeture par l'autre côté
            public string? CloseReason;
        }

        public SimulatedNetwork(ulong seed) => _random = new Pcg32(seed);

        /// <summary>Temps virtuel écoulé, en secondes.</summary>
        public double Now { get; private set; }

        /// <summary>Ouvre une écoute à une adresse symbolique.</summary>
        public SimulatedListener Listen(string address, int maxReceivePayload = Protocol.MaxClientMessageBytes)
        {
            if (address is null) throw new ArgumentNullException(nameof(address));
            if (_listeners.ContainsKey(address)) throw new InvalidOperationException($"Adresse déjà écoutée : {address}.");
            var listener = new SimulatedListener(this, address, maxReceivePayload);
            _listeners.Add(address, listener);
            return listener;
        }

        /// <summary>
        /// Connecte un client à une écoute ; la connexion côté serveur est disponible dans l'écoute. Le lien vaut dans les deux sens.
        /// Une adresse sans écoute donne une connexion déjà fermée.
        /// </summary>
        public SimulatedConnection Connect(string address, LinkProfile profile, int maxReceivePayload = Protocol.MaxServerMessageBytes)
        {
            if (address is null) throw new ArgumentNullException(nameof(address));
            if (profile is null) throw new ArgumentNullException(nameof(profile));
            var client = new SimulatedConnection(this, "client→" + address, profile, maxReceivePayload);
            if (!_listeners.TryGetValue(address, out SimulatedListener? listener) || listener.Stopped)
            {
                client.MarkClosed("connexion refusée : aucune écoute.");
                return client;
            }
            var server = new SimulatedConnection(this, "serveur←" + address, profile, listener.MaxReceivePayload);
            client.Peer = server;
            server.Peer = client;
            listener.Pending.Enqueue(server);
            return client;
        }

        /// <summary>Coupe un lien sans prévenir : les deux côtés sont fermés tout de suite, les messages en route sont perdus.</summary>
        public void Cut(SimulatedConnection connection, string reason = "coupure réseau.")
        {
            if (connection is null) throw new ArgumentNullException(nameof(connection));
            _inFlight.RemoveAll(m => m.To == connection || m.To == connection.Peer);
            connection.MarkClosed(reason);
            connection.Peer?.MarkClosed(reason);
        }

        /// <summary>Avance le temps virtuel et livre les messages échus, dans l'ordre de leur arrivée.</summary>
        public void Advance(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0 || double.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            Now += seconds;
            while (true)
            {
                InFlight? next = null;
                foreach (InFlight m in _inFlight)
                    if (m.DeliverAt <= Now && (next is null || m.DeliverAt < next.DeliverAt || (m.DeliverAt == next.DeliverAt && m.Sequence < next.Sequence)))
                        next = m;
                if (next is null) return;
                _inFlight.Remove(next);
                if (next.Payload is null) next.To.MarkClosed(next.CloseReason ?? "connexion fermée par l'autre côté.");
                else next.To.Deliver(next.Payload);
            }
        }

        /// <summary>Messages en route (tests).</summary>
        public int InFlightCount => _inFlight.Count;

        internal void Post(SimulatedConnection from, byte[]? payload, string? closeReason)
        {
            SimulatedConnection to = from.Peer!;
            LinkProfile profile = from.Profile;
            double jitter = profile.JitterMs > 0 ? profile.JitterMs * (_random.NextUInt() / (double)uint.MaxValue) : 0;
            double at = Now + (profile.LatencyMs + jitter) / 1000.0;
            // Ordre conservé dans chaque sens : jamais avant le message précédent du même sens.
            if (at < from.LastDeliverAt) at = from.LastDeliverAt;
            from.LastDeliverAt = at;
            _inFlight.Add(new InFlight { DeliverAt = at, Sequence = _sequence++, To = to, Payload = payload, CloseReason = closeReason });
        }
    }

    /// <summary>Extrémité d'un lien simulé.</summary>
    public sealed class SimulatedConnection : INetConnection
    {
        private readonly SimulatedNetwork _network;
        private readonly Queue<byte[]> _inbox = new Queue<byte[]>();
        private readonly int _maxReceive;

        internal SimulatedConnection(SimulatedNetwork network, string address, LinkProfile profile, int maxReceivePayload)
        {
            _network = network;
            RemoteAddress = address;
            Profile = profile;
            _maxReceive = maxReceivePayload;
        }

        internal SimulatedConnection? Peer { get; set; }
        internal LinkProfile Profile { get; }
        internal double LastDeliverAt { get; set; }

        public string RemoteAddress { get; }
        public bool IsOpen => CloseReason is null;
        public string? CloseReason { get; private set; }

        /// <summary>Messages reçus pas encore lus.</summary>
        public int Pending => _inbox.Count;

        public bool Send(byte[] payload)
        {
            if (payload is null) throw new ArgumentNullException(nameof(payload));
            if (payload.Length == 0) throw new ArgumentException("Message vide.", nameof(payload));
            if (!IsOpen || Peer is null) return false;
            if (payload.Length > Peer._maxReceive)
            {
                // Comme en TCP : l'autre côté refuse l'en-tête et ferme ; ici, les deux côtés sont fermés tout de suite.
                string reason = $"message de {payload.Length} octets à envoyer, l'autre côté en accepte {Peer._maxReceive} au plus.";
                MarkClosed(reason);
                Peer.MarkClosed("faute de protocole : " + reason);
                return false;
            }
            _network.Post(this, (byte[])payload.Clone(), null);
            return true;
        }

        public bool TryReceive(out byte[] payload)
        {
            if (_inbox.Count == 0)
            {
                payload = Array.Empty<byte>();
                return false;
            }
            payload = _inbox.Dequeue();
            return true;
        }

        public void Close(string reason)
        {
            if (!IsOpen) return;
            CloseReason = reason ?? "fermée.";
            // L'autre côté l'apprend après les messages déjà partis, comme une fin de flux TCP.
            if (Peer != null && Peer.IsOpen) _network.Post(this, null, "connexion fermée par l'autre côté.");
        }

        public void Dispose() => Close("fermée.");

        internal void Deliver(byte[] payload) => _inbox.Enqueue(payload);

        internal void MarkClosed(string reason)
        {
            if (CloseReason is null) CloseReason = reason;
        }
    }

    /// <summary>Écoute d'un réseau simulé.</summary>
    public sealed class SimulatedListener : INetListener
    {
        private readonly SimulatedNetwork _network;

        internal SimulatedListener(SimulatedNetwork network, string address, int maxReceivePayload)
        {
            _network = network;
            Address = address;
            MaxReceivePayload = maxReceivePayload;
        }

        public string Address { get; }
        internal int MaxReceivePayload { get; }
        internal Queue<SimulatedConnection> Pending { get; } = new Queue<SimulatedConnection>();
        internal bool Stopped { get; private set; }

        public bool TryAccept(out INetConnection connection)
        {
            if (Pending.Count == 0)
            {
                connection = null!;
                return false;
            }
            connection = Pending.Dequeue();
            return true;
        }

        public void Dispose() => Stopped = true;
    }
}
