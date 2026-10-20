using System;

namespace EdS.Simulation.Randomness
{
    /// <summary>
    /// Générateur PCG32 (PCG-XSH-RR 64/32, M. E. O'Neill, pcg-random.org), reproduit à l'identique
    /// de l'implémentation de référence « pcg32_srandom_r / pcg32_random_r ».
    /// Même graine + même flux = même suite de nombres, sur toutes les plateformes.
    /// </summary>
    public sealed class Pcg32 : IRandomSource
    {
        private const ulong Multiplier = 6364136223846793005UL;

        /// <summary>Flux utilisé quand aucun n'est précisé (valeur de l'exemple de référence).</summary>
        public const ulong DefaultStream = 54UL;

        private ulong _state;
        private readonly ulong _increment;

        /// <param name="seed">Graine (« initstate » dans la référence).</param>
        /// <param name="stream">Identifiant de flux (« initseq ») : deux flux différents donnent des suites indépendantes
        /// pour la même graine, ce qui permet un flux par combattant.</param>
        public Pcg32(ulong seed, ulong stream = DefaultStream)
        {
            _increment = (stream << 1) | 1UL;
            _state = 0UL;
            Step();
            unchecked { _state += seed; }
            Step();
        }

        private Pcg32(Pcg32State state)
        {
            if ((state.Increment & 1UL) == 0) throw new ArgumentException("L'incrément PCG32 doit être impair.", nameof(state));
            _state = state.State;
            _increment = state.Increment;
        }

        /// <summary>Instantané de l'état interne, pour sauvegarder et rejouer un combat à partir d'un point précis.</summary>
        public Pcg32State Capture() => new Pcg32State(_state, _increment);

        public static Pcg32 Restore(Pcg32State state) => new Pcg32(state);

        public uint NextUInt()
        {
            ulong old = _state;
            Step();
            uint xorShifted = unchecked((uint)(((old >> 18) ^ old) >> 27));
            int rotation = (int)(old >> 59);
            return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
        }

        public uint NextBounded(uint bound)
        {
            if (bound == 0) throw new ArgumentOutOfRangeException(nameof(bound), "La borne doit être strictement positive.");
            // Rejet des valeurs du segment incomplet, comme pcg32_boundedrand_r : aucun biais modulo.
            uint threshold = unchecked(0u - bound) % bound;
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold) return r % bound;
            }
        }

        public bool RollBasisPoints(int chanceBasisPoints)
        {
            if (chanceBasisPoints < 0 || chanceBasisPoints > 10_000)
                throw new ArgumentOutOfRangeException(nameof(chanceBasisPoints), chanceBasisPoints, "Chance attendue entre 0 et 10 000 points de base.");
            // Un tirage est toujours consommé, même à 0 % ou 100 %, pour que la suite ne dépende pas des valeurs.
            return NextBounded(10_000) < (uint)chanceBasisPoints;
        }

        /// <summary>
        /// Revue Codex de la PR #32 (21e tour) : nombre de pas du générateur pour aller de <paramref name="from"/> à <paramref name="to"/>
        /// (même flux), modulo 2⁶⁴ ; chaque tirage avance d'au moins un pas. Algorithme de distance des générateurs congruentiels de la
        /// référence PCG (bit par bit, 64 tours) : l'incrément est impair et le multiplicateur ≡ 1 (mod 4), la période est donc pleine et la
        /// distance existe toujours.
        /// </summary>
        public static ulong Distance(Pcg32State from, Pcg32State to)
        {
            if (from.Increment != to.Increment) throw new ArgumentException("Deux flux PCG32 différents n'ont pas de distance.", nameof(to));
            if ((from.Increment & 1UL) == 0) throw new ArgumentException("L'incrément PCG32 doit être impair.", nameof(from));
            unchecked
            {
                ulong state = from.State, multiplier = Multiplier, plus = from.Increment, bit = 1UL, distance = 0UL;
                while (state != to.State)
                {
                    if ((state & bit) != (to.State & bit))
                    {
                        state = state * multiplier + plus;
                        distance |= bit;
                    }
                    bit <<= 1;
                    plus = (multiplier + 1UL) * plus;
                    multiplier *= multiplier;
                }
                return distance;
            }
        }

        private void Step()
        {
            unchecked { _state = _state * Multiplier + _increment; }
        }
    }

    /// <summary>État complet d'un <see cref="Pcg32"/>, sérialisable dans un journal.</summary>
    public readonly struct Pcg32State : IEquatable<Pcg32State>
    {
        public ulong State { get; }
        public ulong Increment { get; }

        public Pcg32State(ulong state, ulong increment)
        {
            State = state;
            Increment = increment;
        }

        public bool Equals(Pcg32State other) => State == other.State && Increment == other.Increment;
        public override bool Equals(object? obj) => obj is Pcg32State other && Equals(other);
        public override int GetHashCode() => (State ^ (Increment * 31UL)).GetHashCode();
    }
}
