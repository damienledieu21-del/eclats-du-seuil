using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;

namespace EdS.Domain.Tests.Combat;

/// <summary>
/// Sous-lot V1.3 : effets d'équipement (<see cref="EquipmentEffect"/>), leur validateur unique (<see cref="EquipmentEffect.Check"/>) et ses
/// bornes dans les règles (<c>combat.equipment</c>), aux frontières (N − 1, N, N + 1) et par propriétés sur toute une plage de valeurs ; la
/// condition <c>targetStunned</c> (<see cref="CombatantRules.DamageDealtAt"/>) ; l'origine <c>equipment</c> des déclencheurs et des
/// modificateurs d'un personnage (<see cref="CharacterSetup.ActiveTriggers"/>, <see cref="CharacterSetup.ActiveModifiers"/>).
/// </summary>
[Trait("Category", "Engine")]
[Trait("Category", "Limits")]
public class V13EquipmentEffectTests
{
    private static readonly CombatRules Rules = CombatRules.Provisional;
    private static Fixed F(string value) => Fixed.Parse(value);

    private static ModifierDefinition Mod(string key, string add, params ModifierCondition[] conditions) => new(key, F(add), conditions);

    private static TriggerDefinition Parry(long cooldownMs = 1000, int amount = 1) => new(TriggerEvent.PerfectParry,
        new[] { TriggerEffect.GainResource(TriggerResource.RampartCharges, Fixed.FromInt(amount)) }, TriggerRole.Target, cooldownMs: cooldownMs);

    private static TriggerDefinition Riposte(string coefficient, long cooldownMs = 2000) => new(TriggerEvent.Block,
        new[] { TriggerEffect.Strike(TriggerTarget.EventSource, F(coefficient)) }, TriggerRole.Target, cooldownMs: cooldownMs);

    private static EquipmentEffect Effect(string source = "item.lame_du_seuil", ModifierDefinition[]? modifiers = null, TriggerDefinition[]? triggers = null) =>
        new(source, modifiers, triggers);

    private static IReadOnlyList<string> Paths(EquipmentEffect e) => e.Check(Rules, "e").SelectMany(v => v.Fields).ToList();

    // --- Règles ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Bornes_livrees_coherentes()
    {
        Assert.Empty(Rules.Validate());
        Assert.Equal((F("0.25"), F("0.15"), F("0.15"), F("3"), 1000L, F("1"), 2, 1000L),
            (Rules.EquipmentMaxDamageDealt, Rules.EquipmentMaxDamageTaken, Rules.EquipmentMaxHealthPercent, Rules.EquipmentMaxEnduranceRegen,
             Rules.EquipmentMaxStatusDurationMs, Rules.EquipmentMaxStrikeCoefficient, Rules.EquipmentMaxGain, Rules.EquipmentMinTriggerCooldownMs));
    }

    [Fact]
    public void Regles_hors_enveloppe_refusees()
    {
        string[] Fields(CombatRules r) => r.Validate().SelectMany(v => v.Fields).ToArray();
        Assert.Contains(nameof(CombatRules.EquipmentMaxDamageDealt), Fields(new CombatRules { EquipmentMaxDamageDealt = F("-0.1") }));
        Assert.Contains(nameof(CombatRules.EquipmentMaxDamageTaken), Fields(new CombatRules { EquipmentMaxDamageTaken = F("1.0001") }));
        Assert.Contains(nameof(CombatRules.EquipmentMaxGain), Fields(new CombatRules { EquipmentMaxGain = -1 }));
        Assert.Contains(nameof(CombatRules.EquipmentMinTriggerCooldownMs), Fields(new CombatRules { EquipmentMinTriggerCooldownMs = CombatRules.AbsoluteMaxDurationMs + 1 }));
        Assert.Empty(Fields(new CombatRules { EquipmentMaxDamageDealt = Fixed.Zero, EquipmentMaxGain = 0 }));
    }

    // --- Validateur unique -------------------------------------------------------------------------------------------------

    [Fact]
    public void Effets_du_contenu_acceptes()
    {
        Assert.Empty(Paths(Effect(modifiers: new[] { Mod("damageDealt", "0.1", ModifierCondition.TargetStunned) })));
        Assert.Empty(Paths(Effect("item.amulette_de_l_aube", triggers: new[] { Parry() })));
        Assert.Empty(Paths(Effect("set.garde_de_la_tour.6", new[] { Mod("maxHealth", "0.05") },
            new[] { new TriggerDefinition(TriggerEvent.Block, new[] { TriggerEffect.GainResource(TriggerResource.RampartCharges, Fixed.One) }, TriggerRole.Target, cooldownMs: 3000) })));
        Assert.Empty(Paths(Effect("set.tenue_du_veneur.4", new[] { Mod("damageDealt", "0.05", ModifierCondition.TargetStunned), Mod("endurance.regenPerSecond", "1") })));
        Assert.Empty(Paths(Effect(modifiers: new[] { Mod("damageTaken", "-0.05", ModifierCondition.WhileGuarding), Mod("status.stun.durationDealt", "500") })));
    }

    [Theory]
    [InlineData("0.2499", true)]
    [InlineData("0.25", true)]
    [InlineData("0.2501", false)]
    [InlineData("0", false)]
    [InlineData("-0.1", false)]
    public void Degats_infliges_aux_frontieres(string value, bool valid) =>
        Assert.Equal(valid, !Paths(Effect(modifiers: new[] { Mod("damageDealt", value) })).Contains("e.modifiers[0].add"));

    [Theory]
    [InlineData("-0.1499", true)]
    [InlineData("-0.15", true)]
    [InlineData("-0.1501", false)]
    [InlineData("0.05", false)]
    public void Degats_subis_aux_frontieres(string value, bool valid) =>
        Assert.Equal(valid, !Paths(Effect(modifiers: new[] { Mod("damageTaken", value) })).Contains("e.modifiers[0].add"));

    [Theory]
    [InlineData("maxHealth", "0.15", true)]
    [InlineData("maxHealth", "0.1501", false)]
    [InlineData("endurance.regenPerSecond", "3", true)]
    [InlineData("endurance.regenPerSecond", "3.0001", false)]
    [InlineData("status.stun.durationDealt", "1000", true)]
    [InlineData("status.stun.durationDealt", "1050", false)]
    public void Autres_cles_aux_frontieres(string key, string value, bool valid) =>
        Assert.Equal(valid, !Paths(Effect(modifiers: new[] { Mod(key, value) })).Contains("e.modifiers[0].add"));

    [Theory]
    [InlineData("guard.damageReduction", "0.05")]
    [InlineData("parry.windowMs", "50")]
    [InlineData("rampartCharges.max", "1")]
    [InlineData("guard.noUpkeep", "1")]
    [InlineData("skill.bastion.frappe_du_rempart.coefficient", "0.1")]
    [InlineData("cle.inconnue", "1")]
    public void Cle_non_admise_refusee_avec_son_chemin(string key, string value)
    {
        RuleViolation v = Assert.Single(Effect(modifiers: new[] { Mod(key, value) }).Check(Rules, "e"));
        Assert.Equal(("e.modifiers[0].key", RuleViolationKind.UnknownReference), (v.Fields.Single(), v.Kind));
    }

    [Fact]
    public void Conditions_multiplicatives_refusees_et_cible_etourdie_pour_les_degats_infliges_seulement()
    {
        Assert.Contains("e.modifiers[0].conditions", Paths(Effect(modifiers: new[] { Mod("damageDealt", "0.05", ModifierCondition.PerRampartCharge) })));
        Assert.Contains("e.modifiers[0].conditions", Paths(Effect(modifiers: new[] { Mod("damageTaken", "-0.05", ModifierCondition.TargetStunned) })));
        Assert.Empty(Paths(Effect(modifiers: new[] { Mod("damageDealt", "0.05", ModifierCondition.WhileGuarding, ModifierCondition.TargetStunned) })));
        // Même règle au niveau des modificateurs (classes, règles) : targetStunned ne vaut que pour damageDealt.
        var errors = new List<RuleViolation>();
        ModifierDefinition.ValidateList(new[] { Mod("damageTaken", "-0.05", ModifierCondition.TargetStunned) }, "m", 1, false, _ => null, errors);
        Assert.Equal("m[0].conditions", Assert.Single(errors).Fields.Single());
    }

    [Theory]
    [InlineData(950, false)]
    [InlineData(1000, true)]
    [InlineData(1050, true)]
    [InlineData(0, false)]
    public void Recharge_interne_d_un_declencheur_aux_frontieres(long cooldownMs, bool valid) =>
        Assert.Equal(valid, !Paths(Effect(triggers: new[] { Parry(cooldownMs) })).Contains("e.triggers[0].cooldownMs"));

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void Gain_d_un_declencheur_aux_frontieres(int amount, bool valid) =>
        Assert.Equal(valid, !Paths(Effect(triggers: new[] { Parry(amount: amount) })).Contains("e.triggers[0].effects[0].amount"));

    [Theory]
    [InlineData("0.9999", true)]
    [InlineData("1", true)]
    [InlineData("1.0001", false)]
    public void Frappe_d_un_declencheur_aux_frontieres(string coefficient, bool valid) =>
        Assert.Equal(valid, !Paths(Effect(triggers: new[] { Riposte(coefficient) })).Contains("e.triggers[0].effects[0].coefficient"));

    [Fact]
    public void Forme_et_nombres_bornes()
    {
        Assert.Equal(new[] { "e" }, Paths(Effect()));   // ni modificateur ni déclencheur
        Assert.Contains("e.id", Paths(Effect("objet.mal_nomme", new[] { Mod("damageDealt", "0.1") })));
        Assert.Contains("e.id", Paths(Effect("item.Majuscule", new[] { Mod("damageDealt", "0.1") })));
        Assert.Contains("e.modifiers", Paths(Effect(modifiers: Enumerable.Range(0, EquipmentEffect.MaxModifiers + 1).Select(_ => Mod("damageDealt", "0.01")).ToArray())));
        Assert.Contains("e.triggers", Paths(Effect(triggers: Enumerable.Range(0, EquipmentEffect.MaxTriggers + 1).Select(_ => Parry()).ToArray())));
        // Un doublon (même clé, mêmes conditions) est refusé par la forme générale des modificateurs.
        Assert.Contains("e.modifiers[1].key", Paths(Effect(modifiers: new[] { Mod("damageDealt", "0.1"), Mod("damageDealt", "0.1") })));
        // Une référence inconnue (altération) est refusée.
        Assert.Contains("e.modifiers[0].key", Paths(Effect(modifiers: new[] { Mod("status.inconnue.durationDealt", "500") })));
    }

    [Fact]
    public void Liste_d_un_personnage_bornee_et_sans_doublon()
    {
        EquipmentEffect lame = Effect(modifiers: new[] { Mod("damageDealt", "0.1") });
        Assert.Empty(EquipmentEffect.CheckList(new[] { lame, Effect("item.amulette_de_l_aube", triggers: new[] { Parry() }) }, Rules, "eq"));
        RuleViolation twice = Assert.Single(EquipmentEffect.CheckList(new[] { lame, lame }, Rules, "eq"));
        Assert.Equal(("eq[1].id", RuleViolationKind.DuplicateId), (twice.Fields.Single(), twice.Kind));
        var many = Enumerable.Range(0, EquipmentEffect.MaxPerCharacter + 1).Select(i => Effect("item.objet_" + i, new[] { Mod("damageDealt", "0.01") })).ToArray();
        Assert.Equal(RuleViolationKind.LimitExceeded, Assert.Single(EquipmentEffect.CheckList(many, Rules, "eq")).Kind);
        Assert.Empty(EquipmentEffect.CheckList(many.Take(EquipmentEffect.MaxPerCharacter).ToArray(), Rules, "eq"));
    }

    [Fact]
    public void Propriete_le_validateur_suit_la_borne_des_regles_sur_toute_la_plage()
    {
        // Pour chaque valeur brute de −0,30 à +0,30 (pas de 0,0001), damageDealt est admis si et seulement si 0 < v ≤ maxDamageDealt, et
        // damageTaken si et seulement si −maxDamageTaken ≤ v < 0 ; aucune exception.
        for (long raw = -3000; raw <= 3000; raw++)
        {
            Fixed v = Fixed.FromRaw(raw);
            bool dealt = !Paths(Effect(modifiers: new[] { new ModifierDefinition("damageDealt", v) })).Any();
            bool taken = !Paths(Effect(modifiers: new[] { new ModifierDefinition("damageTaken", v) })).Any();
            Assert.Equal(v > Fixed.Zero && v <= Rules.EquipmentMaxDamageDealt, dealt);
            Assert.Equal(v < Fixed.Zero && v >= -Rules.EquipmentMaxDamageTaken, taken);
        }
    }

    [Fact]
    public void Propriete_des_regles_plus_strictes_refusent_ce_qu_elles_bornent()
    {
        // Abaisser une borne des règles ne fait qu'ajouter des refus : un effet admis par des règles strictes l'est par les règles livrées.
        var strict = new CombatRules { EquipmentMaxDamageDealt = F("0.05"), EquipmentMaxGain = 1, EquipmentMinTriggerCooldownMs = 2000 };
        var effects = new[]
        {
            Effect(modifiers: new[] { Mod("damageDealt", "0.04") }), Effect(modifiers: new[] { Mod("damageDealt", "0.1") }),
            Effect(triggers: new[] { Parry(1000, 1) }), Effect(triggers: new[] { Parry(2000, 1) }), Effect(triggers: new[] { Parry(2000, 2) }),
        };
        foreach (EquipmentEffect e in effects)
            if (e.Check(strict, "e").Count == 0) Assert.Empty(e.Check(Rules, "e"));
        Assert.Equal(new[] { true, false, false, true, false }, effects.Select(e => e.Check(strict, "e").Count == 0));
    }

    // --- Condition et origine ----------------------------------------------------------------------------------------------

    [Fact]
    public void Cible_etourdie_compte_seulement_quand_elle_l_est()
    {
        ModifierSet set = ModifierSet.From(new[] { new BoundModifier("item.lame_du_seuil", 0, 1, Mod("damageDealt", "0.1", ModifierCondition.TargetStunned)) });
        CombatantRules rules = CombatantRules.For(Rules, set);
        Assert.Equal(Fixed.Zero, rules.DamageDealtAt(false, 0, 0));
        Assert.Equal(Fixed.Zero, rules.DamageDealtAt(true, 3, 50, targetStunned: false));
        Assert.Equal(F("0.1"), rules.DamageDealtAt(false, 0, 0, targetStunned: true));
        // Majorant du précontrôle : le conditionnel positif compte une fois.
        Assert.Equal(F("0.1"), rules.WorstDamageDealt(Rules.ResonanceMax));
        // Les dégâts subis ignorent la cible étourdie (condition propre à l'attaquant).
        Assert.Equal(Fixed.Zero, rules.DamageTakenAt(false, 0, 0));
    }

    [Fact]
    public void Origine_equipment_pour_les_declencheurs_et_modificateurs_du_personnage()
    {
        ClassDefinition definition = TestClass();
        var build = new CharacterBuild(definition.Id, 10, new Attributes(14, 8, 6, 14, 10, 0), null, new[] { "skill.essai.frappe" });
        var amulette = Effect("item.amulette_de_l_aube", new[] { Mod("damageDealt", "0.05") }, new[] { Parry() });
        var setup = new CharacterSetup("bastion", "joueurs", definition, build, equipment: new[] { amulette });
        BoundTrigger trigger = Assert.Single(setup.ActiveTriggers());
        Assert.Equal((TriggerOrigin.Equipment, "item.amulette_de_l_aube", 0, 1, "item.amulette_de_l_aube/0"),
            (trigger.Origin, trigger.DefinitionId, trigger.Index, trigger.Rank, trigger.Label));
        BoundModifier modifier = Assert.Single(setup.ActiveModifiers());
        Assert.Equal(("item.amulette_de_l_aube", F("0.05")), (modifier.DefinitionId, modifier.Value));
        Assert.Equal(F("0.05"), setup.EffectiveRules(Rules).DamageDealt);
        // Sans équipement, rien ne change.
        var bare = new CharacterSetup("bastion", "joueurs", definition, build);
        Assert.Empty(bare.Equipment);
        Assert.Empty(bare.ActiveTriggers());
        Assert.Equal(Fixed.Zero, bare.EffectiveRules(Rules).DamageDealt);
    }

    private static ClassDefinition TestClass()
    {
        var frappe = new SkillDefinition("skill.essai.frappe", "Frappe", SkillKind.Active,
            combat: new SkillCombatDefinition(PowerType.Physique, DamageType.Physique, F("1.2"), 10, 6000));
        return new ClassDefinition("class.essai", "Essai", new[] { new TalentTreeDefinition("tree.essai.alpha", "alpha", new[] { new TalentDefinition("talent.essai.alpha.a", "A", 5) }) },
            new[] { frappe });
    }
}
