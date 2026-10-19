using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace EdS.Net
{
    /// <summary>
    /// Lot 10 : connexion TCP à messages encadrés (<see cref="Framing"/>), sans bibliothèque. Un fil lit le flux et range les messages
    /// complets ; un autre écrit la file d'envoi. Les deux files sont bornées : un pair qui inonde ou qui ne lit plus est déconnecté au lieu
    /// de faire grossir la mémoire. Un message annoncé au-delà de la limite de réception ferme la connexion avant d'être lu.
    /// </summary>
    public sealed class TcpNetConnection : INetConnection
    {
        /// <summary>Messages au plus en attente de lecture par l'application.</summary>
        public const int MaxPendingReceived = 4096;

        /// <summary>Messages au plus en attente d'envoi.</summary>
        public const int MaxPendingSend = 4096;

        /// <summary>Délai d'écriture : un pair qui ne lit plus est déconnecté.</summary>
        public const int SendTimeoutMs = 10_000;

        private readonly TcpClient _client;
        private readonly Socket _socket;
        private readonly NetworkStream _stream;
        private readonly int _maxReceive;
        private readonly int _maxSend;
        private readonly ConcurrentQueue<byte[]> _received = new ConcurrentQueue<byte[]>();
        private readonly BlockingCollection<byte[]> _outgoing = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>(), MaxPendingSend);
        private readonly object _gate = new object();
        private string? _closeReason;

        /// <param name="maxReceivePayload">Plus gros message accepté de l'autre côté.</param>
        /// <param name="maxSendPayload">Plus gros message que l'autre côté accepte.</param>
        public TcpNetConnection(TcpClient client, int maxReceivePayload, int maxSendPayload)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            if (maxReceivePayload < 1) throw new ArgumentOutOfRangeException(nameof(maxReceivePayload));
            if (maxSendPayload < 1) throw new ArgumentOutOfRangeException(nameof(maxSendPayload));
            _maxReceive = maxReceivePayload;
            _maxSend = maxSendPayload;
            _client.NoDelay = true;
            _client.SendTimeout = SendTimeoutMs;
            _socket = _client.Client;
            _stream = _client.GetStream();
            RemoteAddress = _client.Client.RemoteEndPoint?.ToString() ?? "?";
            new Thread(ReadLoop) { IsBackground = true, Name = "EdS.Net lecture " + RemoteAddress }.Start();
            new Thread(WriteLoop) { IsBackground = true, Name = "EdS.Net écriture " + RemoteAddress }.Start();
        }

        /// <summary>Connexion à un serveur, sans bloquer l'appelant (Unity relève la tâche à chaque image).</summary>
        public static Task<TcpNetConnection> ConnectAsync(string host, int port, int maxReceivePayload, int maxSendPayload)
        {
            if (host is null) throw new ArgumentNullException(nameof(host));
            return Task.Run(async () =>
            {
                var client = new TcpClient();
                try
                {
                    await client.ConnectAsync(host, port).ConfigureAwait(false);
                    return new TcpNetConnection(client, maxReceivePayload, maxSendPayload);
                }
                catch
                {
                    client.Dispose();
                    throw;
                }
            });
        }

        public string RemoteAddress { get; }

        public bool IsOpen
        {
            get { lock (_gate) return _closeReason is null; }
        }

        public string? CloseReason
        {
            get { lock (_gate) return _closeReason; }
        }

        public bool Send(byte[] payload)
        {
            if (payload is null) throw new ArgumentNullException(nameof(payload));
            if (payload.Length == 0) throw new ArgumentException("Message vide.", nameof(payload));
            if (!IsOpen) return false;
            if (payload.Length > _maxSend)
            {
                Close($"message de {payload.Length} octets à envoyer, l'autre côté en accepte {_maxSend} au plus.");
                return false;
            }
            try
            {
                if (_outgoing.TryAdd(Framing.Frame(payload))) return true;
            }
            catch (InvalidOperationException)
            {
                return false;   // file fermée entre-temps
            }
            Close("file d'envoi pleine : l'autre côté ne lit plus.");
            return false;
        }

        public bool TryReceive(out byte[] payload) => _received.TryDequeue(out payload!);

        public void Close(string reason)
        {
            lock (_gate)
            {
                if (_closeReason != null) return;
                _closeReason = reason ?? "fermée.";
            }
            // Les messages déjà en file partent encore ; le fil d'écriture ferme ensuite la socket.
            _outgoing.CompleteAdding();
        }

        public void Dispose()
        {
            Close("fermée.");
            Shutdown();
        }

        /// <summary>Fermeture immédiate de la socket (débloque le fil de lecture).</summary>
        private void Shutdown()
        {
            try
            {
                _client.Close();
            }
            catch (Exception)
            {
                // déjà fermée
            }
        }

        /// <summary>Fermeture vue d'un fil : la raison est gardée seulement si personne n'a fermé avant.</summary>
        private void Fail(string reason)
        {
            Close(reason);
            Shutdown();
        }

        private void ReadLoop()
        {
            var reader = new FrameReader(_maxReceive);
            var buffer = new byte[64 * 1024];
            try
            {
                while (true)
                {
                    int read = _stream.Read(buffer, 0, buffer.Length);
                    if (read == 0)
                    {
                        Fail("connexion fermée par l'autre côté.");
                        return;
                    }
                    foreach (byte[] message in reader.Push(buffer, 0, read))
                    {
                        if (_received.Count >= MaxPendingReceived)
                        {
                            Fail("trop de messages en attente de lecture.");
                            return;
                        }
                        _received.Enqueue(message);
                    }
                    if (reader.Error != null)
                    {
                        Fail("faute de protocole : " + reader.Error);
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                // Aucune exception ne sort d'un fil : elle arrêterait le processus (Unity compris).
                Fail("connexion perdue : " + e.Message);
            }
        }

        private void WriteLoop()
        {
            try
            {
                foreach (byte[] frame in _outgoing.GetConsumingEnumerable())
                    _stream.Write(frame, 0, frame.Length);
                // File close et vidée : fin d'envoi propre, puis fermeture.
                try
                {
                    _socket.Shutdown(SocketShutdown.Send);
                }
                catch (Exception e) when (e is SocketException || e is ObjectDisposedException)
                {
                    // déjà fermée
                }
                // Laisse au pair le temps de lire avant de libérer la socket ; le fil de lecture s'arrête à la fermeture.
                Thread.Sleep(100);
                Shutdown();
            }
            catch (Exception e)
            {
                // Aucune exception ne sort d'un fil : elle arrêterait le processus (Unity compris).
                Fail("envoi impossible : " + e.Message);
            }
        }
    }

    /// <summary>Écoute TCP : un fil accepte les connexions entrantes et les range ; <see cref="TryAccept"/> ne bloque pas.</summary>
    public sealed class TcpNetListener : INetListener
    {
        /// <summary>Connexions acceptées au plus en attente de prise en charge.</summary>
        public const int MaxPendingAccepted = 64;

        private readonly TcpListener _listener;
        private readonly int _maxReceive;
        private readonly int _maxSend;
        private readonly ConcurrentQueue<TcpNetConnection> _accepted = new ConcurrentQueue<TcpNetConnection>();
        private volatile bool _stopped;

        /// <param name="address">Adresse d'écoute (<see cref="IPAddress.Any"/> : tout le réseau local ; <see cref="IPAddress.Loopback"/> : ce poste).</param>
        /// <param name="port">Port ; 0 pour un port libre choisi par le système (tests).</param>
        public TcpNetListener(IPAddress address, int port, int maxReceivePayload = Protocol.MaxClientMessageBytes, int maxSendPayload = Protocol.MaxServerMessageBytes)
        {
            _maxReceive = maxReceivePayload;
            _maxSend = maxSendPayload;
            _listener = new TcpListener(address ?? throw new ArgumentNullException(nameof(address)), port);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            new Thread(AcceptLoop) { IsBackground = true, Name = "EdS.Net écoute " + Port }.Start();
        }

        /// <summary>Port effectivement écouté.</summary>
        public int Port { get; }

        public bool TryAccept(out INetConnection connection)
        {
            bool found = _accepted.TryDequeue(out TcpNetConnection? accepted);
            connection = accepted!;
            return found;
        }

        private void AcceptLoop()
        {
            while (!_stopped)
            {
                TcpClient client;
                try
                {
                    client = _listener.AcceptTcpClient();
                }
                catch (Exception)
                {
                    return;   // écoute arrêtée (aucune exception ne sort d'un fil)
                }
                if (_stopped || _accepted.Count >= MaxPendingAccepted)
                {
                    client.Dispose();
                    continue;
                }
                try
                {
                    _accepted.Enqueue(new TcpNetConnection(client, _maxReceive, _maxSend));
                }
                catch (Exception)
                {
                    client.Dispose();
                }
            }
        }

        public void Dispose()
        {
            _stopped = true;
            _listener.Stop();
            while (_accepted.TryDequeue(out TcpNetConnection? pending)) pending.Dispose();
        }
    }
}
