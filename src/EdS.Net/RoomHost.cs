using System;
using System.Diagnostics;
using System.Threading;

namespace EdS.Net
{
    /// <summary>
    /// Sous-lot 10.2 : anime une <see cref="CombatRoom"/> en temps réel sur un fil dédié : accepte les connexions de l'écoute et avance la
    /// salle du temps écoulé (horloge du système, ici seulement : le combat reste réglé par ses ticks). Utilisé par le serveur dédié et par
    /// l'hôte Unity. Une erreur inattendue arrête le fil et reste lisible dans <see cref="Error"/>.
    /// </summary>
    public sealed class RoomHost : IDisposable
    {
        /// <summary>Pause entre deux passages de la boucle (le tick du combat est de 50 ms).</summary>
        public const int LoopSleepMs = 2;

        private readonly CombatRoom _room;
        private readonly INetListener _listener;
        private readonly object _gate = new object();
        private readonly Thread _thread;
        private volatile bool _running;
        private Exception? _error;

        public RoomHost(CombatRoom room, INetListener listener)
        {
            _room = room ?? throw new ArgumentNullException(nameof(room));
            _listener = listener ?? throw new ArgumentNullException(nameof(listener));
            _thread = new Thread(Loop) { IsBackground = true, Name = "EdS.Net salle" };
        }

        /// <summary>Erreur qui a arrêté la boucle ; null tant qu'elle tourne normalement.</summary>
        public Exception? Error
        {
            get { lock (_gate) return _error; }
        }

        public bool IsRunning => _running;

        public void Start()
        {
            _running = true;
            _thread.Start();
        }

        /// <summary>Lecture cohérente de l'état de la salle depuis un autre fil (affichage).</summary>
        public T Read<T>(Func<CombatRoom, T> read)
        {
            if (read is null) throw new ArgumentNullException(nameof(read));
            lock (_gate) return read(_room);
        }

        private void Loop()
        {
            var clock = Stopwatch.StartNew();
            double last = 0;
            try
            {
                while (_running)
                {
                    double now = clock.Elapsed.TotalSeconds;
                    lock (_gate)
                    {
                        while (_listener.TryAccept(out INetConnection connection)) _room.Accept(connection);
                        _room.Advance(now - last);
                    }
                    last = now;
                    Thread.Sleep(LoopSleepMs);
                }
            }
            catch (Exception e)
            {
                // Aucune exception ne sort d'un fil : elle arrêterait le processus (Unity compris).
                lock (_gate) _error = e;
                _running = false;
            }
        }

        /// <summary>
        /// Arrête la boucle et l'écoute, puis ferme les connexions des joueurs avec une raison (revue Codex de la PR #19 : un hôte arrêté,
        /// dans Unity notamment, ne laisse aucune connexion ouverte ni fil de lecture bloqué).
        /// </summary>
        public void Dispose()
        {
            _running = false;
            if (_thread.IsAlive) _thread.Join(1000);
            _listener.Dispose();
            lock (_gate) _room.CloseAll("serveur arrêté.");
        }
    }
}
