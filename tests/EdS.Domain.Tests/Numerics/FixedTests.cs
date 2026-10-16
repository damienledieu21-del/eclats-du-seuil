using EdS.Domain.Numerics;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace EdS.Domain.Tests.Numerics;

[Trait("Category", "Numeric")]
public class FixedTests
{
    [Fact]
    public void Constantes()
    {
        Assert.Equal(10_000, Fixed.One.Raw);
        Assert.Equal(0, Fixed.Zero.Raw);
        Assert.Equal(1, Fixed.Epsilon.Raw);
        Assert.Equal(Fixed.FromInt(3), Fixed.FromRaw(30_000));
    }

    [Theory]
    [InlineData("1.5", "2", "3")]
    [InlineData("0.0001", "0.5", "0.0001")]      // 0,00005 → arrondi loin de zéro
    [InlineData("-0.0001", "0.5", "-0.0001")]    // symétrique pour les négatifs
    [InlineData("0.0001", "0.4999", "0")]
    [InlineData("1.2345", "-2", "-2.469")]
    [InlineData("1000", "1.5", "1500")]
    public void Multiplication_arrondie_au_plus_proche(string a, string b, string expected) =>
        Assert.Equal(Fixed.Parse(expected), Fixed.Parse(a) * Fixed.Parse(b));

    [Theory]
    [InlineData("1", "3", "0.3333")]
    [InlineData("2", "3", "0.6667")]
    [InlineData("-2", "3", "-0.6667")]
    [InlineData("10", "4", "2.5")]
    public void Division_arrondie_au_plus_proche(string a, string b, string expected) =>
        Assert.Equal(Fixed.Parse(expected), Fixed.Parse(a) / Fixed.Parse(b));

    [Fact]
    public void Division_par_zero_leve_une_exception() =>
        Assert.Throws<DivideByZeroException>(() => Fixed.One / Fixed.Zero);

    [Theory]
    [InlineData("2.5", RoundingMode.HalfAwayFromZero, 3)]
    [InlineData("-2.5", RoundingMode.HalfAwayFromZero, -3)]
    [InlineData("2.5", RoundingMode.HalfToEven, 2)]
    [InlineData("3.5", RoundingMode.HalfToEven, 4)]
    [InlineData("-2.5", RoundingMode.HalfToEven, -2)]
    [InlineData("2.9999", RoundingMode.TowardZero, 2)]
    [InlineData("-2.9999", RoundingMode.TowardZero, -2)]
    [InlineData("-2.0001", RoundingMode.Floor, -3)]
    [InlineData("2.0001", RoundingMode.Floor, 2)]
    [InlineData("2.0001", RoundingMode.Ceiling, 3)]
    [InlineData("-2.9999", RoundingMode.Ceiling, -2)]
    [InlineData("2.4999", RoundingMode.HalfAwayFromZero, 2)]
    [InlineData("7", RoundingMode.Ceiling, 7)]
    public void Arrondi_a_l_entier(string value, RoundingMode mode, long expected) =>
        Assert.Equal(expected, Fixed.Parse(value).RoundToInt(mode));

    [Fact]
    public void Facteur_d_armure_exemple_du_plan()
    {
        // 1000 / (1000 + armure) : armure 0 → 1 ; armure = constante → 0,5 ; armure 3000 → 0,25.
        Assert.Equal(Fixed.One, Fixed.FromRatio(1000, 1000 + 0));
        Assert.Equal(Fixed.Parse("0.5"), Fixed.FromRatio(1000, 1000 + 1000));
        Assert.Equal(Fixed.Parse("0.25"), Fixed.FromRatio(1000, 1000 + 3000));
        Assert.Equal(Fixed.Parse("0.6667"), Fixed.FromRatio(1000, 1500));
    }

    [Fact]
    public void Points_de_base()
    {
        Assert.Equal(Fixed.Parse("0.05"), Fixed.FromBasisPoints(500));
        Assert.Equal(Fixed.Parse("0.6"), Fixed.FromBasisPoints(6000));
    }

    [Fact]
    public void Depassements_detectes()
    {
        Assert.Throws<OverflowException>(() => Fixed.MaxValue + Fixed.Epsilon);
        Assert.Throws<OverflowException>(() => Fixed.MinValue - Fixed.Epsilon);
        Assert.Throws<OverflowException>(() => -Fixed.MinValue);
        Assert.Throws<OverflowException>(() => Fixed.MaxValue * Fixed.FromInt(2));
        Assert.Throws<OverflowException>(() => Fixed.FromInt(long.MaxValue / 1000));
        Assert.Throws<OverflowException>(() => Fixed.MinValue.Abs());
    }

    [Fact]
    public void Grands_produits_sans_depassement_intermediaire()
    {
        // Les valeurs brutes dépassent 2^32 : chemin 128 bits.
        var a = Fixed.FromInt(5_000_000_000);
        var b = Fixed.Parse("0.0002");
        Assert.Equal(Fixed.FromInt(1_000_000), a * b);
        Assert.Equal(Fixed.FromInt(25_000_000_000), Fixed.FromInt(5_000_000) * Fixed.FromInt(5_000));
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("12", 120_000)]
    [InlineData("-0.05", -500)]
    [InlineData("+1.2345", 12_345)]
    [InlineData("3.10", 31_000)]
    [InlineData("-922337203685477.5808", long.MinValue)]
    [InlineData("922337203685477.5807", long.MaxValue)]
    public void Lecture_valide(string text, long raw) => Assert.Equal(raw, Fixed.Parse(text).Raw);

    [Theory]
    [InlineData("")]
    [InlineData("1.23456")]      // 5 décimales : refus plutôt qu'arrondi silencieux
    [InlineData("1,5")]          // virgule française refusée : format invariant uniquement
    [InlineData("1e3")]
    [InlineData(" 1")]
    [InlineData(".5")]
    [InlineData("1.")]
    [InlineData("-")]
    [InlineData("922337203685477.5808")]
    [InlineData("99999999999999999999999")]
    public void Lecture_invalide(string text)
    {
        Assert.False(Fixed.TryParse(text, out _));
        Assert.Throws<FormatException>(() => Fixed.Parse(text));
    }

    [Theory]
    [InlineData(12_500, "1.25")]
    [InlineData(-5_000, "-0.5")]
    [InlineData(30_000, "3")]
    [InlineData(1, "0.0001")]
    [InlineData(long.MinValue, "-922337203685477.5808")]
    public void Ecriture_canonique(long raw, string expected) =>
        Assert.Equal(expected, Fixed.FromRaw(raw).ToString());

    [Fact]
    public void Clamp_et_bornes()
    {
        var min = Fixed.Zero;
        var max = Fixed.FromInt(5);
        Assert.Equal(max, Fixed.Clamp(Fixed.FromInt(7), min, max));
        Assert.Equal(min, Fixed.Clamp(Fixed.FromInt(-1), min, max));
        Assert.Throws<ArgumentException>(() => Fixed.Clamp(Fixed.One, max, min));
    }

    // --- Propriétés ---------------------------------------------------------------

    // Valeurs « de jeu » : |x| < 10^7, bien en deçà des limites, pour tester l'algèbre sans dépassement.
    private static Fixed Game(long raw) => Fixed.FromRaw(raw % 100_000_000_000L);

    [Property(MaxTest = 2000)]
    public bool Ecriture_puis_lecture_identique(long raw) =>
        Fixed.Parse(Fixed.FromRaw(raw).ToString()).Raw == raw;

    [Property(MaxTest = 2000)]
    public bool Addition_soustraction_reversibles(long a, long b)
    {
        Fixed x = Game(a), y = Game(b);
        return x + y - y == x;
    }

    [Property(MaxTest = 2000)]
    public bool Multiplication_commutative(long a, long b)
    {
        Fixed x = Game(a), y = Game(b);
        return x * y == y * x;
    }

    [Property(MaxTest = 2000)]
    public bool Multiplication_par_un_neutre(long a) => Game(a) * Fixed.One == Game(a);

    [Property(MaxTest = 5000)]
    public bool Multiplication_conforme_a_decimal(long a, long b)
    {
        // Oracle : System.Decimal (calcul logiciel exact) arrondi au plus proche, moitié loin de zéro.
        Fixed x = Game(a), y = Fixed.FromRaw(b % 100_000_000L);
        decimal expected = Math.Round((decimal)x.Raw * y.Raw / Fixed.Scale, MidpointRounding.AwayFromZero);
        return (x * y).Raw == (long)expected;
    }

    [Property(MaxTest = 5000)]
    public Property Division_conforme_a_decimal(long a, long b)
    {
        Fixed x = Game(a), y = Fixed.FromRaw(b % 100_000_000L);
        return (y.Raw != 0).When(() =>
        {
            decimal expected = Math.Round((decimal)x.Raw * Fixed.Scale / y.Raw, MidpointRounding.AwayFromZero);
            return (x / y).Raw == (long)expected;
        });
    }

    [Property(MaxTest = 2000)]
    public bool Arrondi_entier_borne(long raw)
    {
        var x = Fixed.FromRaw(raw % 1_000_000_000_000_000L);
        long floor = x.RoundToInt(RoundingMode.Floor);
        long ceiling = x.RoundToInt(RoundingMode.Ceiling);
        long nearest = x.RoundToInt(RoundingMode.HalfAwayFromZero);
        long even = x.RoundToInt(RoundingMode.HalfToEven);
        bool ordered = Fixed.FromInt(floor) <= x && x <= Fixed.FromInt(ceiling) && ceiling - floor <= 1;
        bool nearestOk = nearest == floor || nearest == ceiling;
        bool evenOk = even == floor || even == ceiling;
        return ordered && nearestOk && evenOk;
    }

    [Property(MaxTest = 2000)]
    public bool Comparaison_coherente_avec_les_bruts(long a, long b) =>
        (Fixed.FromRaw(a) < Fixed.FromRaw(b)) == (a < b)
        && Fixed.FromRaw(a).CompareTo(Fixed.FromRaw(b)) == a.CompareTo(b);
}
