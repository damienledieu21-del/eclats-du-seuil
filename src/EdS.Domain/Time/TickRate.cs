using System;

namespace EdS.Domain.Time
{
    /// <summary>
    /// Seul point de conversion entre millisecondes (données de conception) et ticks logiques (moteur).
    /// Le moteur ne connaît que des ticks entiers ; changer la durée du tick (50 ms aujourd'hui,
    /// 20 ms à l'essai plus tard) ne touche que cette classe et la configuration.
    /// </summary>
    public sealed class TickRate : IEquatable<TickRate>
    {
        /// <summary>Durée de tick initiale validée : 50 ms (20 ticks par seconde).</summary>
        public const int DefaultMillisecondsPerTick = 50;

        public static readonly TickRate Default = new TickRate(DefaultMillisecondsPerTick);

        public int MillisecondsPerTick { get; }

        public TickRate(int millisecondsPerTick)
        {
            if (millisecondsPerTick <= 0 || 1000 % millisecondsPerTick != 0)
                throw new ArgumentOutOfRangeException(nameof(millisecondsPerTick), millisecondsPerTick,
                    "La durée du tick doit être un diviseur strictement positif de 1000 ms.");
            MillisecondsPerTick = millisecondsPerTick;
        }

        public int TicksPerSecond => 1000 / MillisecondsPerTick;

        /// <summary>
        /// Convertit une durée en ticks, uniquement si elle tombe exactement sur un tick.
        /// Une durée non multiple (ex. 150 ms avec des ticks de 20 ms) est une erreur de données :
        /// on refuse plutôt que d'arrondir en silence.
        /// </summary>
        public bool TryToTicks(long milliseconds, out long ticks)
        {
            if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds), milliseconds, "Durée négative.");
            ticks = milliseconds / MillisecondsPerTick;
            return milliseconds % MillisecondsPerTick == 0;
        }

        /// <summary>Comme <see cref="TryToTicks"/>, mais lève <see cref="ArgumentException"/> si la durée n'est pas exacte.</summary>
        public long ToTicks(long milliseconds)
        {
            if (TryToTicks(milliseconds, out long ticks)) return ticks;
            throw new ArgumentException(
                $"{milliseconds} ms n'est pas un multiple de la durée du tick ({MillisecondsPerTick} ms).", nameof(milliseconds));
        }

        public long ToMilliseconds(long ticks)
        {
            if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks), ticks, "Nombre de ticks négatif.");
            return checked(ticks * MillisecondsPerTick);
        }

        public bool Equals(TickRate? other) => other is not null && other.MillisecondsPerTick == MillisecondsPerTick;
        public override bool Equals(object? obj) => Equals(obj as TickRate);
        public override int GetHashCode() => MillisecondsPerTick;
        public override string ToString() => $"{MillisecondsPerTick} ms/tick";
    }
}
