using System.Numerics;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using FsCheck.Xunit;

namespace EdS.Domain.Tests.Space;

/// <summary>
/// Sous-lot 11.4a : géométrie entière du moteur spatial. Racine carrée et comparaison de produits exactes (oracle BigInteger), normalisation
/// à l'unité brute près, chevauchement disque–segment exact (frontière : des bords qui se touchent ne se chevauchent pas), polygones convexes.
/// </summary>
[Trait("Category", "Space")]
public class Lot114aGeometryTests
{
    [Theory]
    [InlineData(0L, 0L)]
    [InlineData(1L, 1L)]
    [InlineData(3L, 1L)]
    [InlineData(4L, 2L)]
    [InlineData(99_999_999L, 9_999L)]
    [InlineData(100_000_000L, 10_000L)]
    [InlineData(long.MaxValue, 3_037_000_499L)]
    public void Racine_entiere_par_defaut(long n, long expected) => Assert.Equal(expected, IntMath.Sqrt(n));

    [Property(MaxTest = 5000)]
    public bool Racine_entiere_encadree(int hi, int lo, long small)
    {
        // Grandes valeurs construites à partir de deux entiers (le générateur par défaut reste petit), puis petites valeurs.
        foreach (long n in new[] { (((long)hi << 31) ^ lo) & long.MaxValue, Math.Abs(small == long.MinValue ? 0 : small) })
        {
            BigInteger s = IntMath.Sqrt(n);
            if (!(s * s <= n && (s + 1) * (s + 1) > n)) return false;
        }
        return true;
    }

    [Fact]
    public void Racine_d_un_negatif_refusee() => Assert.Throws<ArgumentOutOfRangeException>(() => IntMath.Sqrt(-1));

    private static long Uniform(Random random, long min, long max) => max == long.MaxValue ? random.NextInt64(min, max) : random.NextInt64(min, max + 1);

    [Fact]
    public void Comparaison_de_produits_exacte()
    {
        var random = new Random(1141);
        for (int i = 0; i < 20_000; i++)
        {
            long max = i % 2 == 0 ? long.MaxValue : 1_000;
            long a = Uniform(random, 0, max), b = Uniform(random, 0, max), c = i % 7 == 0 ? b : Uniform(random, 0, max), d = i % 7 == 0 ? a : Uniform(random, 0, max);
            Assert.Equal(((BigInteger)a * b).CompareTo((BigInteger)c * d), IntMath.CompareProducts(a, b, c, d));
        }
        Assert.Equal(0, IntMath.CompareProducts(long.MaxValue, 2, 2, long.MaxValue));
        Assert.Equal(1, IntMath.CompareProducts(long.MaxValue, long.MaxValue, long.MaxValue - 1, long.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => IntMath.CompareProducts(-1, 1, 1, 1));
    }

    [Fact]
    public void Normalisation_a_l_unite_brute_pres()
    {
        var random = new Random(1142);
        Assert.False(Planar.TryNormalize(0, 0, out _, out _));
        for (int i = 0; i < 20_000; i++)
        {
            long bound = i % 3 == 0 ? Planar.MaxComponent : i % 3 == 1 ? 20_000 : 30;
            long x = Uniform(random, -bound, bound), z = Uniform(random, -bound, bound);
            if (x == 0 && z == 0) continue;
            Assert.True(Planar.TryNormalize(x, z, out long ux, out long uz));
            long length = IntMath.Sqrt(Planar.LengthSquared(ux, uz));
            Assert.InRange(length, Planar.Unit - 1, Planar.Unit + 1);
            // Même direction : produit vectoriel au plus de l'ordre de l'arrondi des composantes.
            BigInteger cross = BigInteger.Abs((BigInteger)x * uz - (BigInteger)z * ux);
            Assert.True(cross <= 3 * (BigInteger)Math.Max(Math.Abs(x), Math.Abs(z)), $"({x}, {z}) → ({ux}, {uz})");
            Assert.True(Math.Sign(x) * Math.Sign(ux) >= 0 && Math.Sign(z) * Math.Sign(uz) >= 0);
        }
    }

    [Fact]
    public void Redimensionnement_a_la_longueur_demandee()
    {
        var random = new Random(1143);
        for (int i = 0; i < 20_000; i++)
        {
            long bound = i % 2 == 0 ? Planar.MaxComponent : 10_000;
            long x = Uniform(random, -bound, bound), z = Uniform(random, -bound, bound), length = Uniform(random, 1, 200_000);
            if (x == 0 && z == 0) continue;
            Assert.True(Planar.TryResize(x, z, length, out long rx, out long rz));
            Assert.InRange(IntMath.Sqrt(Planar.LengthSquared(rx, rz)), length - 2, length + 2);
        }
    }

    [Fact]
    public void Vecteur_hors_bornes_refuse() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Planar.TryNormalize(Planar.MaxComponent + 1, 0, out _, out _));

    /// <summary>Oracle exact de la distance au carré d'un point à un segment, en rationnels BigInteger : chevauchement strict si &lt; r².</summary>
    private static bool ReferenceOverlap(long ax, long az, long bx, long bz, long px, long pz, long r)
    {
        BigInteger ex = bx - ax, ez = bz - az, wx = px - ax, wz = pz - az, r2 = (BigInteger)r * r;
        BigInteger t = wx * ex + wz * ez, ee = ex * ex + ez * ez;
        if (t <= 0) return wx * wx + wz * wz < r2;
        if (t >= ee) return (BigInteger)(px - bx) * (px - bx) + (BigInteger)(pz - bz) * (pz - bz) < r2;
        BigInteger c = ex * wz - ez * wx;
        return c * c < r2 * ee;
    }

    [Theory]
    [InlineData(12, 12, 1144)]               // petites coordonnées : beaucoup de cas exactement tangents
    [InlineData(10_000_000, 50_000, 1145)]   // échelle de l'espace
    public void Chevauchement_disque_segment_exact(long coordinate, long radius, int seed)
    {
        var random = new Random(seed);
        int touching = 0;
        for (int i = 0; i < 40_000; i++)
        {
            long[] v = Enumerable.Range(0, 6).Select(_ => Uniform(random, -coordinate, coordinate)).ToArray();
            long r = Uniform(random, 0, radius);
            bool expected = ReferenceOverlap(v[0], v[1], v[2], v[3], v[4], v[5], r);
            Assert.Equal(expected, Planar.SegmentOverlapsCircle(v[0], v[1], v[2], v[3], v[4], v[5], r));
            if (!expected && ReferenceOverlap(v[0], v[1], v[2], v[3], v[4], v[5], r + 1)) touching++;
        }
        if (coordinate < 100) Assert.True(touching > 100, $"cas tangents {touching}");
    }

    [Fact]
    public void Disques_qui_se_touchent_ne_se_chevauchent_pas()
    {
        Assert.False(Planar.CirclesOverlap(0, 0, 5000, 11_000, 0, 6000));
        Assert.True(Planar.CirclesOverlap(0, 0, 5000, 10_999, 0, 6000));
        Assert.False(Planar.SegmentOverlapsCircle(0, 0, 10_000, 0, 5000, 5000, 5000));   // tangent au milieu
        Assert.True(Planar.SegmentOverlapsCircle(0, 0, 10_000, 0, 5000, 4999, 5000));
        Assert.False(Planar.SegmentOverlapsCircle(0, 0, 10_000, 0, -3000, 4000, 5000));  // tangent à l'extrémité (3-4-5)
    }

    private static GroundVector P(string x, string z) => GroundVector.Parse(x, z);

    private static readonly GroundVector[] Square = { P("0", "0"), P("2", "0"), P("2", "2"), P("0", "2") };

    [Fact]
    public void Polygone_convexe_dans_les_deux_sens_retourne_en_sens_trigonometrique()
    {
        Assert.True(ConvexPolygon.IsStrictlyConvex(Square, out bool clockwise));
        Assert.False(clockwise);
        GroundVector[] reversed = Square.Reverse().ToArray();
        Assert.True(ConvexPolygon.IsStrictlyConvex(reversed, out clockwise));
        Assert.True(clockwise);
        var polygon = new ConvexPolygon(reversed);
        Assert.Equal(new long[] { 0, 0, 20_000, 20_000 }, new[] { polygon.MinX, polygon.MinZ, polygon.MaxX, polygon.MaxZ });
        for (int i = 0; i < polygon.Count; i++)
        {
            int j = (i + 1) % polygon.Count, k = (i + 2) % polygon.Count;
            Assert.True(Planar.Cross(polygon.X(j) - polygon.X(i), polygon.Z(j) - polygon.Z(i), polygon.X(k) - polygon.X(i), polygon.Z(k) - polygon.Z(i)) > 0);
        }
    }

    public static TheoryData<string, GroundVector[]> Refused => new()
    {
        { "deux sommets", new[] { P("0", "0"), P("1", "0") } },
        { "sommets alignés", new[] { P("0", "0"), P("1", "0"), P("2", "0"), P("1", "1") } },
        { "sommet répété", new[] { P("0", "0"), P("1", "0"), P("1", "0"), P("0", "1") } },
        { "concave", new[] { P("0", "0"), P("2", "0"), P("1", "0.5"), P("2", "2"), P("0", "2") } },
        { "côtés croisés", new[] { P("0", "0"), P("2", "2"), P("2", "0"), P("0", "2") } },
        { "pentagramme", new[] { P("0", "1"), P("0.588", "-0.809"), P("-0.951", "0.309"), P("0.951", "0.309"), P("-0.588", "-0.809") } },
        { "hors bornes", new[] { P("0", "0"), P("1000.0001", "0"), P("0", "1") } },
        { "dix-sept sommets", Enumerable.Range(0, 17).Select(i => P((i * 3).ToString(), (i * i).ToString())).ToArray() },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public void Polygones_refuses(string label, GroundVector[] points)
    {
        Assert.False(ConvexPolygon.IsStrictlyConvex(points, out _), label);
        Assert.Throws<ArgumentException>(() => new ConvexPolygon(points));
    }

    [Fact]
    public void Contenu_bord_compris_et_chevauchement_strict()
    {
        var square = new ConvexPolygon(Square);
        Assert.True(square.Contains(10_000, 10_000));
        Assert.True(square.Contains(0, 10_000));       // bord
        Assert.True(square.Contains(20_000, 20_000));  // sommet
        Assert.False(square.Contains(-1, 10_000));
        Assert.False(square.OverlapsCircle(-5000, 10_000, 5000));   // tangent à la face ouest
        Assert.True(square.OverlapsCircle(-4999, 10_000, 5000));
        Assert.False(square.OverlapsCircle(-3000, -4000, 5000));    // tangent au sommet (0, 0)
        Assert.True(square.OverlapsCircle(-3000, -3999, 5000));
        Assert.True(square.OverlapsCircle(10_000, 10_000, 1));      // centre dedans
        Assert.False(square.OverlapsCircle(100_000, 100_000, 5000)); // écarté par la boîte englobante
        square.ClosestBoundaryPoint(-7000, 3000, out long qx, out long qz);
        Assert.Equal((0L, 3000L), (qx, qz));
        square.ClosestBoundaryPoint(25_000, 26_000, out qx, out qz);
        Assert.Equal((20_000L, 20_000L), (qx, qz));
    }

    [Fact]
    public void Disque_balaye_contre_un_polygone_exact()
    {
        // Revue Codex de la PR #33 : une gélule (disque balayé de A à B) qui longe une face ou frôle un coin entre deux points libres.
        var square = new ConvexPolygon(Square);
        Assert.False(square.OverlapsCapsule(-10_000, -6_000, 30_000, -6_000, 5_000));   // à 0,6 m de la face du bas
        Assert.False(square.OverlapsCapsule(-10_000, -5_000, 30_000, -5_000, 5_000));   // la touche seulement : pas de chevauchement
        Assert.True(square.OverlapsCapsule(-10_000, -4_000, 30_000, -4_000, 5_000));    // à 0,4 m : chevauche, extrémités libres
        Assert.False(square.OverlapsCircle(-10_000, -4_000, 5_000));
        Assert.False(square.OverlapsCircle(30_000, -4_000, 5_000));
        Assert.True(square.OverlapsCapsule(-10_000, 10_000, 30_000, 10_000, 1));       // traverse
        // Coin d'un obstacle mince frôlé entre deux échantillons d'une charge (sous-pas de 0,25 m, rayon 0,5 m).
        var sliver = new ConvexPolygon(new[] { P("2.1", "0.49"), P("2.12", "0.49"), P("2.12", "3"), P("2.1", "3") });
        Assert.False(sliver.OverlapsCircle(20_000, 0, 5_000));
        Assert.False(sliver.OverlapsCircle(22_500, 0, 5_000));
        Assert.True(sliver.OverlapsCapsule(20_000, 0, 22_500, 0, 5_000));
        Assert.False(sliver.OverlapsCapsule(20_000, -2_000, 22_500, -2_000, 5_000));
    }
}
