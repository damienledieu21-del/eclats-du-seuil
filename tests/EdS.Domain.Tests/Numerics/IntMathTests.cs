using System.Numerics;
using EdS.Domain.Numerics;
using FsCheck.Xunit;

namespace EdS.Domain.Tests.Numerics;

[Trait("Category", "Numeric")]
public class IntMathTests
{
    [Theory]
    [InlineData(7, 2, RoundingMode.HalfAwayFromZero, 4)]
    [InlineData(-7, 2, RoundingMode.HalfAwayFromZero, -4)]
    [InlineData(7, -2, RoundingMode.Floor, -4)]
    [InlineData(-7, -2, RoundingMode.Floor, 3)]
    [InlineData(5, 2, RoundingMode.HalfToEven, 2)]
    [InlineData(long.MinValue, 1, RoundingMode.TowardZero, long.MinValue)]
    public void DivRound_cas_limites(long n, long d, RoundingMode mode, long expected) =>
        Assert.Equal(expected, IntMath.DivRound(n, d, mode));

    [Fact]
    public void DivRound_min_par_moins_un_deborde() =>
        Assert.Throws<OverflowException>(() => IntMath.DivRound(long.MinValue, -1, RoundingMode.TowardZero));

    [Fact]
    public void MulDiv_resultat_hors_plage_deborde() =>
        Assert.Throws<OverflowException>(() => IntMath.MulDiv(long.MaxValue, long.MaxValue, 2, RoundingMode.TowardZero));

    [Fact]
    public void MulDiv_produit_128_bits_exact() =>
        Assert.Equal(long.MaxValue, IntMath.MulDiv(long.MaxValue, long.MaxValue, long.MaxValue, RoundingMode.TowardZero));

    // Oracle : BigInteger (tests uniquement), division puis arrondi recalculés indépendamment.
    private static BigInteger Reference(BigInteger n, BigInteger d, RoundingMode mode)
    {
        BigInteger q = BigInteger.DivRem(n, d, out BigInteger r);   // tronqué vers zéro
        if (r.IsZero) return q;
        int sign = (n.Sign * d.Sign);
        BigInteger twiceR = BigInteger.Abs(r) * 2, absD = BigInteger.Abs(d);
        bool away = mode switch
        {
            RoundingMode.TowardZero => false,
            RoundingMode.Floor => sign < 0,
            RoundingMode.Ceiling => sign > 0,
            RoundingMode.HalfAwayFromZero => twiceR >= absD,
            RoundingMode.HalfToEven => twiceR > absD || (twiceR == absD && !q.IsEven),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
        return away ? q + sign : q;
    }

    [Property(MaxTest = 5000)]
    public bool MulDiv_conforme_a_BigInteger(long a, long b, long d, byte modeIndex)
    {
        if (d == 0) return true;
        var mode = (RoundingMode)(modeIndex % 5);
        BigInteger expected = Reference((BigInteger)a * b, d, mode);
        if (expected < long.MinValue || expected > long.MaxValue)
        {
            try { IntMath.MulDiv(a, b, d, mode); return false; }
            catch (OverflowException) { return true; }
        }
        return IntMath.MulDiv(a, b, d, mode) == (long)expected;
    }

    [Property(MaxTest = 5000)]
    public bool MulDiv_grandes_valeurs_conforme_a_BigInteger(int a1, int a2, int b1, int b2, int d1, byte modeIndex)
    {
        // Force des opérandes au-delà de 2^32 pour exercer le chemin 128 bits.
        long a = ((long)a1 << 31) ^ a2, b = ((long)b1 << 31) ^ b2, d = ((long)d1 << 31) | 1;
        var mode = (RoundingMode)(modeIndex % 5);
        BigInteger expected = Reference((BigInteger)a * b, d, mode);
        if (expected < long.MinValue || expected > long.MaxValue)
        {
            try { IntMath.MulDiv(a, b, d, mode); return false; }
            catch (OverflowException) { return true; }
        }
        return IntMath.MulDiv(a, b, d, mode) == (long)expected;
    }

    [Property(MaxTest = 5000)]
    public bool DivRound_conforme_a_BigInteger(long n, long d, byte modeIndex)
    {
        if (d == 0 || (n == long.MinValue && d == -1)) return true;
        var mode = (RoundingMode)(modeIndex % 5);
        return IntMath.DivRound(n, d, mode) == (long)Reference(n, d, mode);
    }
}
