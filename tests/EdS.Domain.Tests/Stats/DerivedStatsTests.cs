using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using FsCheck.Xunit;

namespace EdS.Domain.Tests.Stats;

[Trait("Category", "Formula")]
public class DerivedStatsTests
{
    private static readonly StatRules Rules = StatRules.Provisional;
    private static Fixed F(string s) => Fixed.Parse(s);

    [Fact]
    public void Exemple_Bastion_attributs_seuls()
    {
        // Force 50, Dextérité 10, Intelligence 5, Vitalité 40, Volonté 20, Endurance 30.
        var stats = DerivedStats.Calculate(Rules, new Attributes(50, 10, 5, 40, 20, 30));
        Assert.Equal(F("100"), stats.PhysicalPower);     // 50 × 2
        Assert.Equal(F("20"), stats.AgilePower);         // 10 × 2
        Assert.Equal(F("10"), stats.MagicPower);         // 5 × 2
        Assert.Equal(F("500"), stats.MaxHealth);         // 100 + 40 × 10 (E1)
        Assert.Equal(F("40"), stats.StatusResistance);   // 20 × 2
        Assert.Equal(F("160"), stats.MaxEndurance);      // 100 + 30 × 2
        Assert.Equal(Fixed.Zero, stats.Armor);           // base 0 (D3)
        Assert.Equal(Fixed.Zero, stats.MagicResistance);
        Assert.Equal(500, stats.CriticalChanceBasisPoints);
        Assert.Equal(F("1.5"), stats.CriticalMultiplier);
    }

    [Fact]
    public void Base_offensive_nulle_sans_attributs() // D1
    {
        var stats = DerivedStats.Calculate(Rules, Attributes.None);
        Assert.Equal(Fixed.Zero, stats.PhysicalPower);
        Assert.Equal(Fixed.Zero, stats.AgilePower);
        Assert.Equal(Fixed.Zero, stats.MagicPower);
    }

    [Fact]
    public void Equipement_ajoute_par_type_de_puissance() // D2
    {
        var equipment = new StatContributions { PhysicalPower = F("40"), AgilePower = F("7"), MagicPower = F("3.5") };
        var stats = DerivedStats.Calculate(Rules, new Attributes(50, 10, 5, 0, 0, 0), new[] { equipment });
        Assert.Equal(F("140"), stats.GetPower(PowerType.Physique));
        Assert.Equal(F("27"), stats.GetPower(PowerType.Agile));
        Assert.Equal(F("13.5"), stats.GetPower(PowerType.Magique));
    }

    [Fact]
    public void Defenses_independantes_et_plancher_effectif() // D3
    {
        var gear = new StatContributions { Armor = F("500"), MagicResistance = F("250") };
        var shred = new StatContributions { Armor = F("-800") };
        var stats = DerivedStats.Calculate(Rules, Attributes.None, new[] { gear, shred });
        Assert.Equal(F("-300"), stats.Armor);                                  // brute conservée
        Assert.Equal(Fixed.Zero, stats.GetEffectiveDefense(DamageType.Physique)); // jamais négative
        Assert.Equal(F("250"), stats.GetEffectiveDefense(DamageType.Magique));
    }

    [Fact]
    public void Coefficients_configurables()
    {
        var rules = new StatRules { PhysicalPowerPerForce = F("3"), BasePhysicalPower = F("10"), MaxHealthPerVitalite = F("12.5") };
        var stats = DerivedStats.Calculate(rules, new Attributes(10, 0, 0, 4, 0, 0));
        Assert.Equal(F("40"), stats.PhysicalPower);
        Assert.Equal(F("150"), stats.MaxHealth);        // 100 + 4 × 12,5
    }

    [Theory]
    [InlineData(10_000, 6000)]   // plafond 60 %
    [InlineData(-10_000, 0)]     // plancher 0 %
    [InlineData(1500, 2000)]
    public void Chance_critique_bornee(int bonus, int expected) =>
        Assert.Equal(expected, DerivedStats.Calculate(Rules, Attributes.None,
            new[] { new StatContributions { CriticalChanceBasisPoints = bonus } }).CriticalChanceBasisPoints);

    [Theory]
    [InlineData("5", "2.5")]     // plafond ×2,5
    [InlineData("-5", "1")]      // plancher ×1 : un critique ne réduit jamais les dégâts
    [InlineData("0.25", "1.75")]
    public void Multiplicateur_critique_borne(string bonus, string expected) =>
        Assert.Equal(F(expected), DerivedStats.Calculate(Rules, Attributes.None,
            new[] { new StatContributions { CriticalMultiplier = F(bonus) } }).CriticalMultiplier);

    [Fact]
    public void Malus_ne_rendent_pas_les_statistiques_negatives()
    {
        var curse = new StatContributions { PhysicalPower = F("-1000"), MaxHealth = F("-1000"), MaxEndurance = F("-1000"), StatusResistance = F("-1000") };
        var stats = DerivedStats.Calculate(Rules, new Attributes(10, 0, 0, 10, 10, 10), new[] { curse });
        Assert.Equal(Fixed.Zero, stats.PhysicalPower);
        Assert.Equal(Fixed.Zero, stats.MaxHealth);
        Assert.Equal(Fixed.Zero, stats.MaxEndurance);
        Assert.Equal(Fixed.Zero, stats.StatusResistance);
    }

    [Theory] // E1 : PV = 100 + 10 × Vitalité + contributions, plancher 0
    [InlineData(0, "0", "100")]
    [InlineData(1, "0", "110")]
    [InlineData(40, "0", "500")]
    [InlineData(40, "250", "750")]
    [InlineData(5, "-150", "0")]
    [InlineData(5, "-149.5", "0.5")]
    [InlineData(0, "-100", "0")]
    public void Points_de_vie_formule_et_bornes(int vitalite, string contribution, string expected) =>
        Assert.Equal(F(expected), DerivedStats.Calculate(Rules, new Attributes(0, 0, 0, vitalite, 0, 0),
            new[] { new StatContributions { MaxHealth = F(contribution) } }).MaxHealth);

    [Fact]
    public void Points_de_vie_maximaux_sans_debordement()
    {
        // Vitalité maximale d'un int : 100 + 10 × 2 147 483 647, encore représentable.
        var stats = DerivedStats.Calculate(Rules, new Attributes(0, 0, 0, int.MaxValue, 0, 0));
        Assert.Equal(Fixed.FromInt(100 + 10L * int.MaxValue), stats.MaxHealth);
    }

    [Fact]
    public void Attribut_negatif_refuse() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Attributes(-1, 0, 0, 0, 0, 0));

    [Fact]
    public void Regles_provisoires_valides() => Assert.Empty(StatRules.Provisional.Validate());

    [Fact]
    public void Regles_incoherentes_signalees()
    {
        var rules = new StatRules
        {
            PhysicalPowerPerForce = F("-1"),
            MaxCriticalChanceBasisPoints = 12_000,
            MinCriticalMultiplier = F("0.5"),
        };
        Assert.Equal(3, rules.Validate().Count);
    }

    // --- Propriétés ---------------------------------------------------------------

    private static StatContributions Random(int a, int b, int c, int d, int e) => new()
    {
        PhysicalPower = Fixed.FromRaw((long)a * 37 % 100_000_000),
        AgilePower = Fixed.FromRaw((long)b * 41 % 100_000_000),
        MagicPower = Fixed.FromRaw((long)c * 43 % 100_000_000),
        Armor = Fixed.FromRaw((long)d * 47 % 100_000_000),
        MaxHealth = Fixed.FromRaw((long)e * 53 % 100_000_000),
        CriticalChanceBasisPoints = a % 20_000,
        CriticalMultiplier = Fixed.FromRaw((long)b % 100_000),
    };

    private static Attributes Attr(byte f, byte d, byte i, byte v) => new(f, d, i, v, 0, 0);

    [Property(MaxTest = 2000)]
    public bool Statistiques_et_critiques_toujours_dans_leurs_bornes(byte f, byte d, int a, int b, int c, int e, int g)
    {
        var s = DerivedStats.Calculate(Rules, Attr(f, d, 0, 0), new[] { Random(a, b, c, e, g) });
        return s.PhysicalPower >= Fixed.Zero && s.AgilePower >= Fixed.Zero && s.MagicPower >= Fixed.Zero
            && s.MaxHealth >= Fixed.Zero
            && s.GetEffectiveDefense(DamageType.Physique) >= Fixed.Zero
            && s.CriticalChanceBasisPoints >= 0 && s.CriticalChanceBasisPoints <= 6000
            && s.CriticalMultiplier >= Fixed.One && s.CriticalMultiplier <= F("2.5");
    }

    [Property(MaxTest = 1000)]
    public bool Ordre_des_contributions_sans_effet(int a, int b, int c, int d, int e)
    {
        StatContributions x = Random(a, b, c, d, e), y = Random(e, d, c, b, a);
        var s1 = DerivedStats.Calculate(Rules, Attributes.None, new[] { x, y });
        var s2 = DerivedStats.Calculate(Rules, Attributes.None, new[] { y, x });
        return s1.PhysicalPower == s2.PhysicalPower && s1.Armor == s2.Armor && s1.MaxHealth == s2.MaxHealth
            && s1.CriticalChanceBasisPoints == s2.CriticalChanceBasisPoints && s1.CriticalMultiplier == s2.CriticalMultiplier;
    }

    [Property(MaxTest = 1000)]
    public bool Puissance_egale_attributs_plus_equipement(byte force, ushort equipment)
    {
        var gear = new StatContributions { PhysicalPower = Fixed.FromInt(equipment) };
        var s = DerivedStats.Calculate(Rules, new Attributes(force, 0, 0, 0, 0, 0), new[] { gear });
        return s.PhysicalPower == Fixed.FromInt(2 * force + equipment);
    }
}
