using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using FsCheck.Xunit;

namespace EdS.Domain.Tests.Combat;

[Trait("Category", "Formula")]
public class DamageCalculatorTests
{
    private static readonly StatRules Stats = StatRules.Provisional;
    private static readonly DamageRules Rules = DamageRules.Provisional;
    private static Fixed F(string s) => Fixed.Parse(s);

    private static DerivedStats Combatant(Attributes attributes, StatContributions? gear = null) =>
        DerivedStats.Calculate(Stats, attributes, gear is null ? null : new[] { gear });

    private static DerivedStats Defender(string armor = "0", string magicResistance = "0") =>
        Combatant(Attributes.None, new StatContributions { Armor = F(armor), MagicResistance = F(magicResistance) });

    // Bastion de référence : Force 50 (+100 puissance physique) et arme +40 → 140.
    private static readonly DerivedStats Bastion =
        Combatant(new Attributes(50, 0, 0, 0, 0, 0), new StatContributions { PhysicalPower = F("40") });

    private static readonly DamageBonuses ExampleBonuses = DamageBonuses.None
        .Add(BonusCategory.Competence, F("0.20"))
        .Add(BonusCategory.Talent, F("0.10"))
        .Add(BonusCategory.Talent, F("-0.05"))
        .Add(BonusCategory.Alteration, F("0.10"));

    [Fact]
    public void Exemple_1_coup_critique_physique_detaille()
    {
        var input = new DamageInput(Bastion, Defender(armor: "500"), PowerType.Physique, DamageType.Physique,
            F("1.2"), ExampleBonuses, isCritical: true);
        var b = DamageCalculator.Compute(input, Rules);

        Assert.Equal(F("140"), b.Power);                    // 50 × 2 + 40
        Assert.Equal(F("168"), b.AfterCoefficient);         // 140 × 1,2
        Assert.Equal(F("1.2"), b.CompetenceMultiplier);     // 1 + 0,20
        Assert.Equal(F("1.05"), b.TalentMultiplier);        // 1 + 0,10 − 0,05
        Assert.Equal(F("1.1"), b.AlterationMultiplier);     // 1 + 0,10
        Assert.Equal(F("1.386"), b.BonusMultiplier);        // 1,2 × 1,05 × 1,1
        Assert.Equal(F("232.848"), b.AfterBonuses);         // 168 × 1,386
        Assert.Equal(F("1.5"), b.CriticalMultiplier);
        Assert.Equal(F("349.272"), b.AfterCritical);        // 232,848 × 1,5
        Assert.Equal(F("0.6667"), b.DefenseFactor);         // 1000 / (1000 + 500)
        Assert.Equal(F("232.848"), b.AfterDefense);         // 349,272 × 1000 / 1500, calcul exact
        Assert.Equal(233, b.FinalDamage);
    }

    [Theory]
    [InlineData("0", "232.848", 233)]      // sans protection : identique à l'exemple 1
    [InlineData("0.6", "93.1392", 93)]     // garde : −60 % après l'armure
    [InlineData("0.9", "23.2848", 23)]     // parade parfaite : −90 %
    [InlineData("1", "0", 0)]              // protection totale : aucun dégât, pas de minimum
    public void Exemple_10_garde_et_parade_apres_la_defense(string mitigation, string afterMitigation, long final)
    {
        var input = new DamageInput(Bastion, Defender(armor: "500"), PowerType.Physique, DamageType.Physique,
            F("1.2"), ExampleBonuses, isCritical: true, mitigation: F(mitigation));
        var b = DamageCalculator.Compute(input, Rules);
        Assert.Equal(F("232.848"), b.AfterDefense);
        Assert.Equal(F(mitigation), b.Mitigation);
        Assert.Equal(F(afterMitigation), b.AfterMitigation);
        Assert.Equal(final, b.FinalDamage);
    }

    [Theory]
    [InlineData("-0.1")]
    [InlineData("1.0001")]
    public void Protection_hors_de_0_1_refusee(string mitigation) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new DamageInput(Bastion, Defender(), PowerType.Physique, DamageType.Physique,
            Fixed.One, mitigation: F(mitigation)));

    [Fact]
    public void Exemple_2_meme_coup_sans_critique()
    {
        var input = new DamageInput(Bastion, Defender(armor: "500"), PowerType.Physique, DamageType.Physique,
            F("1.2"), ExampleBonuses);
        var b = DamageCalculator.Compute(input, Rules);
        Assert.Equal(F("155.232"), b.AfterDefense);         // 232,848 × 2/3
        Assert.Equal(155, b.FinalDamage);
    }

    [Fact]
    public void Exemple_3_degats_magiques_contre_resistance_magique()
    {
        // Intelligence 30 (+60) et focus +15 → 75 ; coefficient 2 → 150 ; résistance 250 → × 1000/1250.
        var mage = Combatant(new Attributes(0, 0, 30, 0, 0, 0), new StatContributions { MagicPower = F("15") });
        var b = DamageCalculator.Compute(
            new DamageInput(mage, Defender(armor: "9999", magicResistance: "250"), PowerType.Magique, DamageType.Magique, F("2")), Rules);
        Assert.Equal(F("0.8"), b.DefenseFactor);            // l'armure n'intervient pas
        Assert.Equal(120, b.FinalDamage);
    }

    [Fact]
    public void Exemple_4_armure_negative_traitee_comme_zero()
    {
        var b = DamageCalculator.Compute(new DamageInput(
            Combatant(new Attributes(50, 0, 0, 0, 0, 0)), Defender(armor: "-300"), PowerType.Physique, DamageType.Physique, Fixed.One), Rules);
        Assert.Equal(Fixed.Zero, b.EffectiveDefense);
        Assert.Equal(Fixed.One, b.DefenseFactor);
        Assert.Equal(100, b.FinalDamage);
    }

    [Theory]
    [InlineData("0", "1")]
    [InlineData("1000", "0.5")]
    [InlineData("3000", "0.25")]
    [InlineData("1500", "0.4")]
    public void Exemple_5_facteur_d_armure(string armor, string factor)
    {
        var b = DamageCalculator.Compute(new DamageInput(Bastion, Defender(armor: armor), PowerType.Physique, DamageType.Physique, Fixed.One), Rules);
        Assert.Equal(F(factor), b.DefenseFactor);
    }

    [Fact]
    public void Exemple_6_constantes_de_defense_reglables_separement()
    {
        var rules = new DamageRules { ArmorConstant = 2000, MagicResistanceConstant = 500 };
        var target = Defender(armor: "500", magicResistance: "500");
        var physical = DamageCalculator.Compute(new DamageInput(Bastion, target, PowerType.Physique, DamageType.Physique, Fixed.One), rules);
        var magical = DamageCalculator.Compute(new DamageInput(Bastion, target, PowerType.Physique, DamageType.Magique, Fixed.One), rules);
        Assert.Equal(F("0.8"), physical.DefenseFactor);     // 2000 / 2500
        Assert.Equal(F("0.5"), magical.DefenseFactor);      // 500 / 1000
        Assert.Equal(112, physical.FinalDamage);
        Assert.Equal(70, magical.FinalDamage);
    }

    [Fact]
    public void Exemple_7_malus_ramenant_une_categorie_a_zero_annule_les_degats()
    {
        var bonuses = DamageBonuses.None.Add(BonusCategory.Competence, F("-1.5"));   // 1 − 1,5 → borné à 0
        var b = DamageCalculator.Compute(new DamageInput(Bastion, Defender(), PowerType.Physique, DamageType.Physique, Fixed.One, bonuses), Rules);
        Assert.Equal(Fixed.Zero, b.CompetenceMultiplier);
        Assert.Equal(Fixed.Zero, b.BonusMultiplier);
        Assert.Equal(0, b.FinalDamage);                     // pas de minimum : immunité possible
    }

    [Fact]
    public void Exemple_8_minimum_de_1_pour_un_coup_qui_touche()
    {
        var weak = Combatant(Attributes.None, new StatContributions { PhysicalPower = F("0.3") });
        var b = DamageCalculator.Compute(new DamageInput(weak, Defender(), PowerType.Physique, DamageType.Physique, Fixed.One), Rules);
        Assert.Equal(F("0.3"), b.AfterDefense);
        Assert.Equal(1, b.FinalDamage);
    }

    [Fact]
    public void Exemple_9_arrondi_final_moitie_vers_le_haut()
    {
        var attacker = Combatant(Attributes.None, new StatContributions { PhysicalPower = F("5") });
        var b = DamageCalculator.Compute(new DamageInput(attacker, Defender(), PowerType.Physique, DamageType.Physique, F("0.5")), Rules);
        Assert.Equal(F("2.5"), b.AfterDefense);
        Assert.Equal(3, b.FinalDamage);
        var floor = DamageCalculator.Compute(new DamageInput(attacker, Defender(), PowerType.Physique, DamageType.Physique, F("0.5")),
            new DamageRules { FinalRounding = RoundingMode.Floor });
        Assert.Equal(2, floor.FinalDamage);
    }

    [Fact]
    public void Exemple_10_puissance_agile_avec_degats_physiques()
    {
        var rogue = Combatant(new Attributes(0, 40, 0, 0, 0, 0));
        var b = DamageCalculator.Compute(new DamageInput(rogue, Defender(armor: "1000"), PowerType.Agile, DamageType.Physique, Fixed.One), Rules);
        Assert.Equal(F("80"), b.Power);
        Assert.Equal(40, b.FinalDamage);
    }

    [Fact]
    public void Plafond_de_categorie_applique()
    {
        var bonuses = DamageBonuses.None.Add(BonusCategory.Talent, Fixed.FromInt(1_000_000));
        Assert.Equal(Fixed.FromInt(10), DamageCalculator.CategoryMultiplier(bonuses.Talent, Rules));
    }

    [Fact]
    public void Debordement_detecte_au_lieu_d_un_resultat_faux()
    {
        var titan = Combatant(Attributes.None, new StatContributions { PhysicalPower = Fixed.FromInt(100_000_000_000_000) });
        var input = new DamageInput(titan, Defender(), PowerType.Physique, DamageType.Physique, Fixed.FromInt(1000));
        Assert.Throws<OverflowException>(() => DamageCalculator.Compute(input, Rules));
        Assert.Throws<OverflowException>(() => DamageBonuses.None.Add(BonusCategory.Talent, Fixed.MaxValue).Add(BonusCategory.Talent, Fixed.One));
    }

    [Fact]
    public void Coefficient_negatif_refuse() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DamageInput(Bastion, Defender(), PowerType.Physique, DamageType.Physique, F("-0.1")));

    [Fact]
    public void Regles_provisoires_valides() => Assert.Empty(DamageRules.Provisional.Validate());

    [Fact]
    public void Regles_incoherentes_signalees()
    {
        var rules = new DamageRules { ArmorConstant = 0, MagicResistanceConstant = -5, MinCategoryMultiplier = F("-0.5"), MinimumDamage = -1 };
        Assert.Equal(4, rules.Validate().Count);
    }

    // --- Propriétés ---------------------------------------------------------------

    private static DerivedStats Attacker(uint power, int critBonus) =>
        Combatant(Attributes.None, new StatContributions
        {
            PhysicalPower = Fixed.FromRaw(power % 100_000_000),          // 0 à 10 000
            CriticalMultiplier = Fixed.FromRaw(critBonus % 20_000),
        });

    private static DamageBonuses Bonuses(int a, int b, int c) => DamageBonuses.None
        .Add(BonusCategory.Competence, Fixed.FromRaw(a % 50_000))           // −5 à +5
        .Add(BonusCategory.Talent, Fixed.FromRaw(b % 50_000))
        .Add(BonusCategory.Alteration, Fixed.FromRaw(c % 50_000));

    private static long Damage(DerivedStats attacker, long armorRaw, uint coefRaw, DamageBonuses bonuses, bool crit) =>
        DamageCalculator.Compute(new DamageInput(attacker,
            Combatant(Attributes.None, new StatContributions { Armor = Fixed.FromRaw(armorRaw) }),
            PowerType.Physique, DamageType.Physique, Fixed.FromRaw(coefRaw % 100_000), bonuses, crit), Rules).FinalDamage;

    [Property(MaxTest = 3000)]
    public bool Degats_jamais_negatifs(uint power, int crit, int armor, uint coef, int a, int b, int c, bool isCrit) =>
        Damage(Attacker(power, crit), armor % 100_000_000L, coef, Bonuses(a, b, c), isCrit) >= 0;

    [Property(MaxTest = 3000)]
    public bool Multiplicateur_de_bonus_jamais_negatif_ni_au_dela_du_plafond(int a, int b, int c)
    {
        var bonuses = Bonuses(a, b, c);
        var m = DamageCalculator.CategoryMultiplier(bonuses.Competence, Rules)
              * DamageCalculator.CategoryMultiplier(bonuses.Talent, Rules)
              * DamageCalculator.CategoryMultiplier(bonuses.Alteration, Rules);
        return m >= Fixed.Zero && m <= Fixed.FromInt(1000);
    }

    [Property(MaxTest = 3000)]
    public bool Plus_d_armure_jamais_plus_de_degats(uint power, uint coef, ushort armor, ushort extra, int a, int b, int c, bool isCrit)
    {
        var attacker = Attacker(power, 0);
        return Damage(attacker, (long)armor * Fixed.Scale + extra * Fixed.Scale, coef, Bonuses(a, b, c), isCrit)
            <= Damage(attacker, (long)armor * Fixed.Scale, coef, Bonuses(a, b, c), isCrit);
    }

    [Property(MaxTest = 3000)]
    public bool Plus_de_puissance_jamais_moins_de_degats(uint power, ushort extra, int armor, uint coef, int a, int b, int c)
    {
        uint p = power % 100_000_000;
        return Damage(Attacker(p + extra, 0), armor % 100_000_000L, coef, Bonuses(a, b, c), false)
            >= Damage(Attacker(p, 0), armor % 100_000_000L, coef, Bonuses(a, b, c), false);
    }

    [Property(MaxTest = 3000)]
    public bool Critique_jamais_moins_que_coup_normal(uint power, int crit, int armor, uint coef, int a, int b, int c)
    {
        var attacker = Attacker(power, crit);
        return Damage(attacker, armor % 100_000_000L, coef, Bonuses(a, b, c), true)
            >= Damage(attacker, armor % 100_000_000L, coef, Bonuses(a, b, c), false);
    }

    [Property(MaxTest = 3000)]
    public bool Facteur_de_defense_entre_0_et_1(int armor)
    {
        var b = DamageCalculator.Compute(new DamageInput(Bastion,
            Combatant(Attributes.None, new StatContributions { Armor = Fixed.FromRaw(armor) }),
            PowerType.Physique, DamageType.Physique, Fixed.One), Rules);
        return b.DefenseFactor > Fixed.Zero && b.DefenseFactor <= Fixed.One;
    }

    [Property(MaxTest = 2000)]
    public bool Calcul_deterministe(uint power, int crit, int armor, uint coef, int a, int b, int c, bool isCrit)
    {
        var attacker = Attacker(power, crit);
        return Damage(attacker, armor % 100_000_000L, coef, Bonuses(a, b, c), isCrit)
            == Damage(attacker, armor % 100_000_000L, coef, Bonuses(a, b, c), isCrit);
    }
}
