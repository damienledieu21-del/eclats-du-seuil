using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace EdS.Domain.Tests.Space;

/// <summary>
/// Sous-lot 11.4 : géométrie exacte du plan au sol (entiers seulement) : racines carrées, portée entre les bords des emprises au
/// 0,0001 m près, déplacement borné par la vitesse, bornes de l'espace.
/// </summary>
[Trait("Category", "Space")]
[Trait("Category", "Numeric")]
public class SpaceMathTests
{
    private static GroundVector P(string x, string z) => new(Fixed.Parse(x), Fixed.Parse(z));

    [Theory]
    [InlineData(0UL, 0UL)]
    [InlineData(1UL, 1UL)]
    [InlineData(2UL, 1UL)]
    [InlineData(3UL, 1UL)]
    [InlineData(4UL, 2UL)]
    [InlineData(99UL, 9UL)]
    [InlineData(100UL, 10UL)]
    [InlineData(80_000_000_000_000_000UL, 282_842_712UL)]
    [InlineData(ulong.MaxValue, 4_294_967_295UL)]
    public void Racine_carree_entiere_par_defaut(ulong n, ulong expected) => Assert.Equal(expected, SpaceMath.FloorSqrt(n));

    [Property(MaxTest = 5000)]
    public void Racine_carree_encadree(ulong n)
    {
        ulong r = SpaceMath.FloorSqrt(n);
        Assert.True((System.Numerics.BigInteger)r * r <= n);
        Assert.True((System.Numerics.BigInteger)(r + 1) * (r + 1) > n);
        ulong c = SpaceMath.CeilSqrt(n);
        Assert.True(c == r || c == r + 1);
        Assert.True((System.Numerics.BigInteger)c * c >= n);
    }

    [Fact]
    public void Portee_entre_les_bords_frontiere_incluse_au_dix_millieme()
    {
        // Centres à 4 m, rayons 0,5 et 0,6 : bords à 2,9 m. Portée 2,9 : à portée ; 2,8999 : hors de portée.
        GroundVector a = P("0", "0"), b = P("4", "0");
        Fixed ra = Fixed.Parse("0.5"), rb = Fixed.Parse("0.6");
        Assert.True(SpaceMath.WithinReach(a, ra, b, rb, Fixed.Parse("2.9")));
        Assert.False(SpaceMath.WithinReach(a, ra, b, rb, Fixed.Parse("2.8999")));
        // En diagonale (3, 4) : 5 m entre centres, 3,9 m entre bords ; exact sans racine.
        GroundVector c = P("3", "4");
        Assert.True(SpaceMath.WithinReach(a, ra, c, rb, Fixed.Parse("3.9")));
        Assert.False(SpaceMath.WithinReach(a, ra, c, rb, Fixed.Parse("3.8999")));
        // Emprises qui se chevauchent : à portée nulle.
        Assert.True(SpaceMath.WithinReach(a, ra, P("1", "0"), rb, Fixed.Zero));
    }

    [Property(MaxTest = 3000)]
    public void Portee_symetrique_et_monotone(int ax, int az, int bx, int bz, ushort range)
    {
        // Coordonnées dans ±10 000 m (au dix-millième), portée jusqu'à 6,5535 m.
        GroundVector a = new(Fixed.FromRaw(ax % 100_000_000), Fixed.FromRaw(az % 100_000_000));
        GroundVector b = new(Fixed.FromRaw(bx % 100_000_000), Fixed.FromRaw(bz % 100_000_000));
        Fixed r = Fixed.FromRaw(range), radius = Fixed.Parse("0.5");
        bool inReach = SpaceMath.WithinReach(a, radius, b, radius, r);
        Assert.Equal(inReach, SpaceMath.WithinReach(b, radius, a, radius, r));
        if (inReach) Assert.True(SpaceMath.WithinReach(a, radius, b, radius, r + Fixed.Epsilon));
    }

    [Property(MaxTest = 3000)]
    public void Deplacement_borne_par_la_longueur_et_dans_la_direction(int dx, int dz, ushort length)
    {
        var direction = new GroundVector(Fixed.FromRaw(dx % 100_000_000), Fixed.FromRaw(dz % 100_000_000));
        if (direction.IsZero) return;
        GroundVector step = SpaceMath.Scale(direction, length);
        // Jamais plus long que demandé (vitesse bornée) ; au plus un dix-millième par composante en dessous (arrondi vers zéro).
        Assert.True(SpaceMath.LengthSquaredRaw(step) <= (long)length * length);
        long norm = (long)SpaceMath.CeilSqrt((ulong)SpaceMath.LengthSquaredRaw(direction));
        double expected = (double)length * Math.Sqrt(SpaceMath.LengthSquaredRaw(direction)) / norm;
        Assert.True(Math.Sqrt(SpaceMath.LengthSquaredRaw(step)) >= expected - 2.0);   // deux unités brutes : une par composante
        // Même sens : jamais à rebours d'une composante.
        Assert.True(Math.Sign(step.X.Raw) == 0 || Math.Sign(step.X.Raw) == Math.Sign(direction.X.Raw));
        Assert.True(Math.Sign(step.Z.Raw) == 0 || Math.Sign(step.Z.Raw) == Math.Sign(direction.Z.Raw));
    }

    [Fact]
    public void Pas_de_marche_au_tick()
    {
        // 5 m/s × 50 ms = 0,25 m, droit ou en diagonale (0,1767 m par composante, arrondi vers zéro).
        Assert.Equal(P("0.25", "0"), SpaceMath.Scale(P("1", "0"), 2_500));
        Assert.Equal(P("0", "-0.25"), SpaceMath.Scale(P("0", "-3"), 2_500));
        Assert.Equal(P("0.1767", "0.1767"), SpaceMath.Scale(P("1", "1"), 2_500));
        Assert.Equal(P("1", "0"), SpaceMath.Normalize(P("7", "0")));
        Assert.Throws<ArgumentException>(() => SpaceMath.Scale(GroundVector.Zero, 2_500));
    }

    [Fact]
    public void Bornes_de_l_espace()
    {
        var space = new CombatSpace(Fixed.FromInt(-10), Fixed.FromInt(20), Fixed.FromInt(-10), Fixed.FromInt(10));
        Assert.Empty(space.Validate());
        Fixed r = Fixed.Parse("0.5");
        Assert.True(space.Contains(P("19.5", "9.5"), r));
        Assert.False(space.Contains(P("19.5001", "0"), r));
        Assert.Equal(P("19.5", "-9.5"), space.Clamp(P("25", "-12"), r));
        Assert.Equal(P("3", "4"), space.Clamp(P("3", "4"), r));
        Assert.Contains(new CombatSpace(Fixed.One, Fixed.One, Fixed.Zero, Fixed.One).Validate(), v => v.Fields.Contains("space.bounds.minX"));
        Assert.Contains(new CombatSpace(Fixed.Zero, Fixed.FromInt(10_001), Fixed.Zero, Fixed.One).Validate(),
            v => v.Kind == RuleViolationKind.NumericLimit && v.Fields.Contains("space.bounds.maxX"));
    }
}
