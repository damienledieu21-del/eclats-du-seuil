using EdS.Domain.Combat;
using EdS.Domain.Numerics;

namespace EdS.Domain.Tests.Combat;

/// <summary>
/// Sous-lot 8.2 : validation des renforcements (catégorie <c>buff</c>, modificateurs portés) et des boucliers (catégorie <c>shield</c>) dans
/// le catalogue des altérations, applications autorisées et majorants partagés avec le précontrôle numérique.
/// </summary>
[Trait("Category", "Shield")]
[Trait("Category", "Status")]
public class Lot82StatusCatalogTests
{
    private static readonly StatusCatalog Shipped = StatusCatalog.Provisional;
    private const string P = "statuses[7]";   // première définition ajoutée après les sept altérations livrées

    private static ModifierDefinition Mod(string key, string add, string perRank = "0", params ModifierCondition[] conditions) =>
        new(key, new RankedValue(Fixed.Parse(add), Fixed.Parse(perRank)), conditions);

    private static StatusDefinition Buff(string id = "fortifie", StatusStacking stacking = StatusStacking.Refresh, params ModifierDefinition[] modifiers) =>
        new(id, StatusCategory.Buff, 8000, stacking, modifiers: modifiers);

    private static StatusDefinition Shield(string id = "egide", StatusStacking stacking = StatusStacking.PerSourceKeepStronger,
        StatusResistanceMode resistance = StatusResistanceMode.None, string? grants = null) =>
        new(id, StatusCategory.Shield, 4000, stacking, resistance, grantsOnExpiry: grants);

    private static StatusCatalog With(params StatusDefinition[] extra) => new(Shipped.Definitions.Concat(extra));

    private static IReadOnlyList<RuleViolation> Errors(params StatusDefinition[] extra) => With(extra).Validate(50);

    private static void AssertSingle(string field, string fragment, params StatusDefinition[] extra)
    {
        RuleViolation violation = Assert.Single(Errors(extra));
        Assert.Equal(field, Assert.Single(violation.Fields));
        Assert.Contains(fragment, violation.Message);
    }

    [Fact]
    public void Categories_buff_et_shield_reconnues()
    {
        Assert.Equal(StatusCategory.Buff, StatusIds.Categories["buff"]);
        Assert.Equal(StatusCategory.Shield, StatusIds.Categories["shield"]);
        Assert.Equal(new[] { ModifierField.DamageDealt, ModifierField.DamageTaken, ModifierField.GuardUpkeepPerSecond }, StatusDefinition.BuffKeys);
        Assert.False(Shipped.HasShields);
        Assert.Equal(Fixed.Zero, Shipped.TotalBuffDamageDealt());
    }

    [Fact]
    public void Renforcement_valide_avec_chaque_cle_admise()
    {
        Assert.Empty(Errors(Buff(modifiers: Mod("damageTaken", "-0.3"))));
        Assert.Empty(Errors(Buff(modifiers: Mod("damageDealt", "0.5"))));
        Assert.Empty(Errors(Buff(modifiers: Mod("damageDealt", "-0.2"))));   // un renforcement peut aussi affaiblir
        Assert.Empty(Errors(Buff(modifiers: Mod("guard.upkeepPerSecond", "-2"))));
        Assert.Empty(Errors(Buff(modifiers: new[] { Mod("damageTaken", "-1"), Mod("damageDealt", "1"), Mod("guard.upkeepPerSecond", "1000000") })));
        Assert.Empty(Errors(Buff(stacking: StatusStacking.KeepLonger, modifiers: Mod("damageTaken", "-0.3"))));
        Assert.Empty(Errors(Buff(stacking: StatusStacking.Ignore, modifiers: Mod("damageTaken", "-0.3"))));
    }

    [Fact]
    public void Renforcement_sans_modificateur_ou_modificateurs_hors_renforcement_refuses()
    {
        AssertSingle(P + ".modifiers", "au moins un modificateur", Buff());
        AssertSingle(P + ".modifiers", "seul un renforcement",
            new StatusDefinition("marque", StatusCategory.Modifier, 4000, StatusStacking.Refresh, modifiers: new[] { Mod("damageTaken", "-0.1") }));
        AssertSingle(P + ".modifiers", "seul un renforcement",
            new StatusDefinition("egide", StatusCategory.Shield, 4000, StatusStacking.PerSourceKeepStronger, modifiers: new[] { Mod("damageTaken", "-0.1") }));
    }

    [Theory]
    [InlineData("parry.windowMs", "100", "n'est pas admise sur un renforcement")]
    [InlineData("maxHealth", "0.1", "n'est pas admise sur un renforcement")]
    [InlineData("shield.potencyDealt", "0.1", "n'est pas admise sur un renforcement")]
    [InlineData("guard.damageReduction", "0.1", "n'est pas admise sur un renforcement")]
    [InlineData("inconnue", "1", "clé inconnue")]
    [InlineData("damageTaken", "0.1", "ne fait que réduire")]
    [InlineData("damageTaken", "-1.0001", "valeur hors de")]
    [InlineData("damageDealt", "0", "sans effet")]
    public void Cle_ou_valeur_refusee_sur_un_renforcement_une_erreur(string key, string add, string fragment)
    {
        string field = fragment.Contains("admise") || fragment.Contains("inconnue") ? P + ".modifiers[0].key" : P + ".modifiers[0].add";
        AssertSingle(field, fragment, Buff(modifiers: Mod(key, add)));
    }

    [Fact]
    public void Renforcement_sans_condition_ni_perRank_ni_doublon()
    {
        // Condition sur une clé conditionnelle ailleurs (damageDealt) : refusée ici ; sur une clé jamais conditionnelle, une seule erreur.
        AssertSingle(P + ".modifiers[0].conditions", "pas de condition", Buff(modifiers: Mod("damageDealt", "0.5", "0", ModifierCondition.WhileGuarding)));
        AssertSingle(P + ".modifiers[0].conditions", "ne peut pas être conditionnelle",
            Buff(modifiers: Mod("guard.upkeepPerSecond", "-1", "0", ModifierCondition.WhileGuarding)));
        AssertSingle(P + ".modifiers[0].add", "perRank", Buff(modifiers: Mod("damageTaken", "-0.1", "-0.1")));
        AssertSingle(P + ".modifiers[1].key", "déjà déclaré", Buff(modifiers: new[] { Mod("damageTaken", "-0.1"), Mod("damageTaken", "-0.2") }));
    }

    [Fact]
    public void Bouclier_cumul_par_source_seul_admis()
    {
        Assert.Empty(Errors(Shield()));
        Assert.Empty(Errors(Shield(), Shield("barriere")));
        foreach (StatusStacking other in new[] { StatusStacking.Refresh, StatusStacking.KeepLonger, StatusStacking.Ignore })
            AssertSingle(P + ".stacking", "un bouclier se cumule par source", Shield(stacking: other));
        // Le cumul par source reste refusé aux autres catégories non périodiques, renforcements compris.
        AssertSingle(P + ".stacking", "réservé aux altérations périodiques, aux boucliers", Buff(stacking: StatusStacking.PerSourceKeepStronger, modifiers: Mod("damageTaken", "-0.1")));
    }

    [Fact]
    public void Bouclier_sans_resistance_et_jamais_accorde()
    {
        AssertSingle(P + ".resistance", "réservée aux altérations périodiques", Shield(resistance: StatusResistanceMode.Defense));
        AssertSingle(P + ".resistance", "ne réduit que la durée des contrôles", Shield(resistance: StatusResistanceMode.Duration));
        // Un bouclier n'a pas de source qui fixe sa valeur à l'expiration d'une autre altération.
        var accorde = new StatusDefinition("voile", StatusCategory.Modifier, 2000, StatusStacking.Refresh, grantsOnExpiry: "egide");
        RuleViolation violation = Assert.Single(Errors(Shield(), accorde));
        Assert.Equal(("statuses[8].grantsOnExpiry", true), (violation.Fields.Single(), violation.Message.Contains("bouclier ne peut pas être accordé")));
        // Un bouclier peut, lui, accorder une altération à son expiration (épuisé, il est retiré sans rien accorder : voir le moteur).
        Assert.Empty(Errors(Shield(grants: "fragility")));
    }

    [Theory]
    [InlineData("egide", "0.1", null)]
    [InlineData("egide", "0", null)]     // admis : valeur nulle, refusée au combat (stateBlocked « empty »)
    [InlineData("egide", null, "est un bouclier : coefficient obligatoire")]
    [InlineData("fortifie", null, null)]
    [InlineData("fortifie", "1", "ni périodique ni un bouclier")]
    [InlineData("stun", "1", "ni périodique ni un bouclier")]
    public void Applications_d_un_bouclier_et_d_un_renforcement(string status, string? coefficient, string? problem)
    {
        StatusCatalog catalog = With(Shield(), Buff(modifiers: Mod("damageTaken", "-0.3")));
        string? actual = catalog.CheckApplication(new StatusApplication(status, coefficient is null ? null : Fixed.Parse(coefficient)), "guardBreak");
        if (problem is null) Assert.Null(actual);
        else Assert.Contains(problem, actual);
    }

    [Fact]
    public void Majorants_partages_avec_le_precontrole()
    {
        // Dégâts infligés des renforcements : parts positives de chaque définition (une instance par définition et par porteur).
        StatusCatalog catalog = With(Buff("furie", modifiers: new[] { Mod("damageDealt", "0.5"), Mod("damageTaken", "-0.1") }),
            Buff("rage", modifiers: Mod("damageDealt", "0.2")), Buff("faiblesse", modifiers: Mod("damageDealt", "-0.4")), Shield());
        Assert.Empty(catalog.Validate(50));
        Assert.Equal(Fixed.Parse("0.7"), catalog.TotalBuffDamageDealt());
        Assert.True(catalog.HasShields);
        Assert.Equal(Fixed.Parse("-0.1"), catalog["furie"].ModifierSum(ModifierField.DamageTaken));
        Assert.Equal(Fixed.Zero, catalog["furie"].ModifierSum(ModifierField.GuardUpkeepPerSecond));
        Assert.Equal(Fixed.Zero, catalog["egide"].ModifierSum(ModifierField.DamageDealt));
        Assert.Equal(Fixed.Parse("0.1"), Shipped.TotalDamageTakenIncrease());   // la Fragilité seule, inchangée
    }

    [Theory]
    [InlineData(2100, "0.1", "0.5", 315)]
    [InlineData(2100, "0.0333", "0", 69)]       // ⌊ 69,93 ⌋
    [InlineData(2100, "0.1", "-0.5", 105)]
    [InlineData(2100, "0.1", "-1", 0)]           // puissance −100 % : bouclier vide
    [InlineData(2100, "0.1", "-2", 0)]           // facteur ramené à 0, jamais négatif
    [InlineData(2100, "0", "0", 0)]
    [InlineData(0, "0.5", "0", 0)]
    [InlineData(1, "0.9999", "0", 0)]            // ⌊ 0,9999 ⌋
    [InlineData(1, "100", "10", 1100)]           // coefficient et puissance maximaux
    public void Valeur_d_un_bouclier(long maxHealth, string coefficient, string potency, long expected) =>
        Assert.Equal(expected, Shields.Value(maxHealth, Fixed.Parse(coefficient), Fixed.Parse(potency)));

    [Fact]
    public void Valeur_d_un_bouclier_sans_PV_max_ou_hors_limites()
    {
        Assert.Equal(0, Shields.Value(null, Fixed.One, Fixed.Zero));
        Assert.Equal(0, Shields.Value(-5, Fixed.One, Fixed.Zero));
        // 10¹³ PV × 100 dépasse la plage de Fixed (≈ 9,22 × 10¹⁴) : débordement signalé, jamais une valeur tronquée.
        Assert.Throws<OverflowException>(() => Shields.Value(10_000_000_000_000, Fixed.FromInt(100), Fixed.Zero));
        Assert.Equal(900_000_000_000_000, Shields.Value(9_000_000_000_000, Fixed.FromInt(100), Fixed.Zero));
    }
}
