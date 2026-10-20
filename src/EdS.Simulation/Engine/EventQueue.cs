using System;
using System.Collections.Generic;

namespace EdS.Simulation.Engine
{
    /// <summary>
    /// Phases d'un tick, dans leur ordre d'exécution (arbitrage A10).
    /// Une recharge qui expire au tick t est levée en phase 1, avant les commandes du même tick.
    /// </summary>
    public enum CombatPhase
    {
        Setup = 0,
        Expirations = 1,
        Commands = 2,

        /// <summary>Sous-lot 11.4 : déplacement des combattants d'un combat positionné, après les commandes et avant les impacts.</summary>
        Movement = 3,
        Resolution = 4,
        Periodic = 5,
        Regeneration = 6,
        EndOfTick = 7,
    }

    /// <summary>
    /// File de priorité déterministe, ordonnée par (tick, phase, séquence). La séquence est un compteur attribué
    /// à l'insertion : deux éléments de même tick et même phase sortent dans l'ordre où ils sont entrés.
    /// Tas binaire maison (netstandard2.1 n'a pas de PriorityQueue).
    /// </summary>
    public sealed class EventQueue<T>
    {
        private readonly struct Entry
        {
            public readonly long Tick;
            public readonly CombatPhase Phase;
            public readonly long Sequence;
            public readonly T Item;

            public Entry(long tick, CombatPhase phase, long sequence, T item)
            {
                Tick = tick;
                Phase = phase;
                Sequence = sequence;
                Item = item;
            }

            public bool Before(in Entry other) =>
                Tick != other.Tick ? Tick < other.Tick
                : Phase != other.Phase ? Phase < other.Phase
                : Sequence < other.Sequence;
        }

        private readonly List<Entry> _heap = new List<Entry>();
        private long _nextSequence;

        // Position (tick, phase) la plus avancée déjà demandée à TryDequeue. Correctif 6.1 : un événement programmé avant
        // elle resterait en tête du tas et bloquerait silencieusement toute la file ; il est refusé (voir LIMITES_LOT_7.md).
        private bool _started;
        private long _cursorTick;
        private CombatPhase _cursorPhase;

        public int Count => _heap.Count;

        /// <summary>Ajoute un élément et renvoie sa séquence.</summary>
        public long Enqueue(long tick, CombatPhase phase, T item)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), tick, "Tick négatif.");
            if (_started && (tick < _cursorTick || (tick == _cursorTick && phase < _cursorPhase)))
                throw new InvalidOperationException(
                    $"Événement programmé dans une phase déjà passée ({tick}, {phase}) alors que la file est en ({_cursorTick}, {_cursorPhase}).");
            long sequence = _nextSequence;
            _nextSequence = checked(_nextSequence + 1);   // revue Codex de la PR #32 (7e tour) : aucun débordement silencieux
            _heap.Add(new Entry(tick, phase, sequence, item));
            int i = _heap.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (!_heap[i].Before(_heap[parent])) break;
                (_heap[i], _heap[parent]) = (_heap[parent], _heap[i]);
                i = parent;
            }
            return sequence;
        }

        /// <summary>Retire le premier élément s'il est prévu exactement à ce tick et dans cette phase.</summary>
        public bool TryDequeue(long tick, CombatPhase phase, out T item)
        {
            if (!TryPeek(tick, phase, out item)) return false;
            Dequeue();
            return true;
        }

        /// <summary>
        /// Lit sans le retirer le premier élément s'il est prévu exactement à ce tick et dans cette phase (lot 7.1 : le
        /// moteur réserve le budget de son traitement avant de le retirer). Avance le curseur comme <see cref="TryDequeue"/>.
        /// </summary>
        public bool TryPeek(long tick, CombatPhase phase, out T item)
        {
            if (!_started || tick > _cursorTick || (tick == _cursorTick && phase > _cursorPhase))
            {
                _started = true;
                _cursorTick = tick;
                _cursorPhase = phase;
            }
            if (_heap.Count == 0 || _heap[0].Tick != tick || _heap[0].Phase != phase)
            {
                item = default!;
                return false;
            }
            item = _heap[0].Item;
            return true;
        }

        /// <summary>Retire le premier élément, quel qu'il soit.</summary>
        public T Dequeue()
        {
            if (_heap.Count == 0) throw new InvalidOperationException("File vide.");
            T item = _heap[0].Item;
            int last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int left = 2 * i + 1, right = left + 1, smallest = i;
                if (left < _heap.Count && _heap[left].Before(_heap[smallest])) smallest = left;
                if (right < _heap.Count && _heap[right].Before(_heap[smallest])) smallest = right;
                if (smallest == i) break;
                (_heap[i], _heap[smallest]) = (_heap[smallest], _heap[i]);
                i = smallest;
            }
            return item;
        }

        /// <summary>Tick du prochain élément, ou null si la file est vide.</summary>
        public long? NextTick => _heap.Count == 0 ? (long?)null : _heap[0].Tick;

        /// <summary>Sous-lot 11.4d (instantané d'état) : éléments dans l'ordre de sortie (tick, phase, séquence).</summary>
        internal List<(long Tick, CombatPhase Phase, long Sequence, T Item)> Entries()
        {
            var entries = new List<(long, CombatPhase, long, T)>(_heap.Count);
            foreach (Entry e in _heap) entries.Add((e.Tick, e.Phase, e.Sequence, e.Item));
            entries.Sort((a, b) => a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) : a.Item2 != b.Item2 ? a.Item2.CompareTo(b.Item2) : a.Item3.CompareTo(b.Item3));
            return entries;
        }

        /// <summary>Sous-lot 11.4d : curseur (position la plus avancée déjà demandée) et prochaine séquence.</summary>
        internal (bool Started, long Tick, CombatPhase Phase, long NextSequence) Cursor => (_started, _cursorTick, _cursorPhase, _nextSequence);

        /// <summary>
        /// Sous-lot 11.4d : recharge une file vide à l'identique (éléments, séquences, curseur). L'ordre de sortie ne dépend que de
        /// (tick, phase, séquence), toutes distinctes : la file rechargée rend les éléments dans le même ordre que l'originale.
        /// </summary>
        internal void Load(IEnumerable<(long Tick, CombatPhase Phase, long Sequence, T Item)> entries, bool started, long cursorTick, CombatPhase cursorPhase,
            long nextSequence)
        {
            if (_heap.Count != 0) throw new InvalidOperationException("File déjà remplie.");
            var seen = new HashSet<long>();
            foreach (var (tick, phase, sequence, item) in entries)
            {
                if (tick < 0 || sequence < 0 || sequence >= nextSequence || !seen.Add(sequence))
                    throw new FormatException($"Élément de file invalide (tick {tick}, séquence {sequence}).");
                _heap.Add(new Entry(tick, phase, sequence, item));
                int i = _heap.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (!_heap[i].Before(_heap[parent])) break;
                    (_heap[i], _heap[parent]) = (_heap[parent], _heap[i]);
                    i = parent;
                }
            }
            _started = started;
            _cursorTick = cursorTick;
            _cursorPhase = cursorPhase;
            _nextSequence = nextSequence;
        }
    }
}
