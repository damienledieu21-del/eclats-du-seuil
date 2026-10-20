using EdS.Data.Classes;
using EdS.Data.Json;
using EdS.Data.Validation;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 8.5 : contenu complet du Bastion en données (feuille de route, catalogue du Bastion). Chaque talent notable, chaque
/// intermédiaire et chaque passif porte exactement les effets du catalogue, écrits ici sous une forme lisible (clé, valeur au rang 1
/// puis par rang, conditions ; déclencheur : abonnement, rôle, recharge, filtre et effets) ; trois altérations du Gardien dans les
/// règles ; plus aucun « [TEST] ». Les interprétations retenues sont dans docs/LIMITES_LOT_8.md (sous-lot 8.5).
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Bastion")]
public class Lot85ContentDataTests
{
    private static string Modifier(ModifierDefinition m) =>
        $"{m.Key} {m.Add}" + (m.Conditions.Count == 0 ? "" : " [" + string.Join(", ", m.Conditions.Select(ModifierKeys.Of)) + "]");

    private static string Effect(TriggerEffect e) => TriggerIds.Of(e.Kind) + e.Kind switch
    {
        TriggerEffectKind.Strike => $" {TriggerIds.Of(e.Target!.Value)} {e.Coefficient} {e.PowerType.ToString()!.ToLowerInvariant()}/{e.DamageType.ToString()!.ToLowerInvariant()}",
        TriggerEffectKind.ApplyStatus => $" {TriggerIds.Of(e.Target!.Value)} {e.Status}" + (e.Coefficient is { } c ? $" {c}" : ""),
        _ => $" {TriggerIds.Of(e.Resource!.Value)} {e.Amount}",
    };

    private static string Trigger(TriggerDefinition t)
    {
        var parts = new List<string> { TriggerIds.Of(t.On) };
        if (t.Role is { } role) parts.Add(TriggerIds.Of(role));
        if (t.CooldownMs > 0) parts.Add($"recharge {t.CooldownMs}");
        if (t.Filter.Skills.Count > 0) parts.Add("compétences " + string.Join(", ", t.Filter.Skills));
        if (t.Filter.Defenses.Count > 0) parts.Add("défenses " + string.Join(", ", t.Filter.Defenses.Select(TriggerIds.Of)));
        return string.Join(" ", parts) + " : " + string.Join(" ; ", t.Effects.Select(Effect));
    }

    private static IEnumerable<string> Effects(IReadOnlyList<TriggerDefinition> triggers, IReadOnlyList<ModifierDefinition> modifiers) =>
        triggers.Select(Trigger).Concat(modifiers.Select(Modifier));

    private static TalentDefinition Talent(string tree, string id)
    {
        Assert.True(ClassTestData.Bastion.TryGetTalent($"talent.bastion.{tree}.{id}", out TalentDefinition talent, out _), id);
        return talent;
    }

    public static TheoryData<string, string, int, string[]> Talents() => new()
    {
        // Rempart
        { "rempart", "garde_stable", 3, new[] { "guard.damageReduction 0.04+0.04/rang", "guard.upkeepPerSecond -0.25+-0.25/rang" } },
        { "rempart", "echo_defensif", 1, new[] { "onBlock target : gainResource endurance 4", "onBlock target recharge 3000 : gainResource rampartCharges 1" } },
        { "rempart", "rempart_eternel", 1, new[] { "guard.noUpkeepBreak 1", "guard.reductionAtZeroEndurance 0.4" } },
        { "rempart", "peau_de_fer", 5, new[] { "damageTaken -0.015+-0.015/rang" } },
        { "rempart", "souffle_du_roc", 5, new[] { "endurance.max 4+4/rang", "endurance.regenPerSecond 1+1/rang" } },
        { "rempart", "poids_du_rempart", 5, new[] { "damageTaken -0.005+-0.005/rang [whileGuarding, perRampartCharge]" } },
        // Contrecoup
        { "contrecoup", "riposte_precise", 3, new[] { "onPerfectParry target : strike eventSource 0.75+0.25/rang physique/physique" } },
        { "contrecoup", "fureur_contenue", 2, new[] { "damageDealt 0.01+0.01/rang [perResonanceStep]" } },
        { "contrecoup", "jugement_du_colosse", 1, new[] { "onPerfectParry target recharge 3000 : strike allEnemies 1.2 physique/physique ; gainResource resonance 10" } },
        { "contrecoup", "main_lourde", 5, new[] { "damageDealt 0.02+0.02/rang" } },
        { "contrecoup", "elan", 5, new[] { "parry.failureRecoveryMs -50+-50/rang" } },
        { "contrecoup", "echo_de_guerre", 5, new[] { "onPerfectParry target : gainResource resonance 2+2/rang" } },
        // Gardien
        { "gardien", "egide_reactive", 3, new[] { "onHitTaken recharge 2000 défenses guard, parry : applyStatus bearer reactiveAegis 0.03+0.03/rang" } },
        { "gardien", "lien_protecteur", 1, new[] { "onSkillCast compétences skill.bastion.barriere_d_acier : applyStatus eventTarget protectiveLink" } },
        { "gardien", "serment_du_gardien", 1, new[] { "onHitTaken ally recharge 3000 : applyStatus eventSource oathTaunt",
            "protection.redirectRatio 0.15", "protection.redirectThroughGuard 1" } },
        { "gardien", "vigilance", 5, new[] { "maxHealth 0.02+0.02/rang" } },
        { "gardien", "presence", 5, new[] { "status.taunt.durationDealt 200+200/rang", "status.oathTaunt.durationDealt 200+200/rang" } },
        { "gardien", "rempart_vivant", 5, new[] { "shield.potencyDealt 0.04+0.04/rang" } },
    };

    [Theory]
    [MemberData(nameof(Talents))]
    public void Effets_des_talents_conformes_au_catalogue(string tree, string id, int maxRank, string[] expected)
    {
        TalentDefinition talent = Talent(tree, id);
        Assert.Equal(maxRank, talent.MaxRank);
        Assert.Equal(expected, Effects(talent.Triggers, talent.Modifiers));
    }

    [Fact]
    public void Valeurs_aux_rangs_du_catalogue()
    {
        // Garde stable 3 : +12 % de réduction, −0,75/s ; Riposte précise : 0,5 + 0,25 par rang (0,75 à 1,25) ; Fureur contenue 2 : +2 %
        // par tranche de 10 Résonance (+20 % à 100) ; Égide réactive 3 : 9 % des PV max ; intermédiaires au rang 5.
        Fixed At(string tree, string id, int rank, int index = 0, bool trigger = false)
        {
            TalentDefinition t = Talent(tree, id);
            return trigger ? (t.Triggers[0].Effects[index].Coefficient ?? t.Triggers[0].Effects[index].Amount)!.Value.At(rank) : t.Modifiers[index].Add.At(rank);
        }
        Assert.Equal(Fixed.Parse("0.12"), At("rempart", "garde_stable", 3));
        Assert.Equal(Fixed.Parse("-0.75"), At("rempart", "garde_stable", 3, 1));
        Assert.Equal(new[] { "0.75", "1", "1.25" }.Select(Fixed.Parse), new[] { 1, 2, 3 }.Select(r => At("contrecoup", "riposte_precise", r, trigger: true)));
        Assert.Equal(Fixed.Parse("0.02"), At("contrecoup", "fureur_contenue", 2));
        Assert.Equal(Fixed.Parse("0.09"), At("gardien", "egide_reactive", 3, trigger: true));
        Assert.Equal(Fixed.Parse("-0.075"), At("rempart", "peau_de_fer", 5));
        Assert.Equal((Fixed.FromInt(20), Fixed.FromInt(5)), (At("rempart", "souffle_du_roc", 5), At("rempart", "souffle_du_roc", 5, 1)));
        Assert.Equal(Fixed.Parse("-0.025"), At("rempart", "poids_du_rempart", 5));
        Assert.Equal(Fixed.Parse("0.1"), At("contrecoup", "main_lourde", 5));
        Assert.Equal(Fixed.FromInt(-250), At("contrecoup", "elan", 5));
        Assert.Equal(Fixed.FromInt(10), At("contrecoup", "echo_de_guerre", 5, trigger: true));
        Assert.Equal(Fixed.Parse("0.1"), At("gardien", "vigilance", 5));
        Assert.Equal(Fixed.FromInt(1000), At("gardien", "presence", 5));
        Assert.Equal(Fixed.Parse("0.2"), At("gardien", "rempart_vivant", 5));
    }

    public static TheoryData<string, string, string[]> Passives() => new()
    {
        { "endurance_du_roc", "Endurance du roc", new[] { "endurance.max 15" } },
        { "bouclier_lourd", "Bouclier lourd", new[] { "guard.costPerBlock -3" } },
        { "reserve_de_charges", "Réserve de Charges", new[] { "rampartCharges.max 1" } },
        { "oeil_du_duelliste", "Œil du duelliste", new[] { "parry.windowMs 50" } },
        { "ferveur", "Ferveur", new[] { "resonance.perDirectHit 1" } },
        { "posture_de_fer", "Posture de fer", new[] { "damageTaken -0.05 [whileGuarding]" } },
        { "sentinelle", "Sentinelle", new[] { "onHitTaken ally recharge 6000 : gainResource rampartCharges 1" } },
        { "tenacite", "Ténacité", new[] { "statusResistance 100" } },
    };

    [Theory]
    [MemberData(nameof(Passives))]
    public void Effets_des_passifs_conformes_au_catalogue(string id, string name, string[] expected)
    {
        Assert.True(ClassTestData.Bastion.TryGetSkill("skill.bastion." + id, out SkillDefinition skill), id);
        Assert.Equal((name, SkillKind.Passive, 1, false), (skill.Name, skill.Kind, skill.MinLevel, skill.IsPlaceholder));
        Assert.Null(skill.Combat);
        Assert.Equal(expected, Effects(skill.Triggers, skill.Modifiers));
    }

    [Fact]
    public void Tous_les_talents_et_passifs_ont_un_effet_et_les_actives_aucun_modificateur()
    {
        Assert.All(ClassTestData.Bastion.Trees.SelectMany(t => t.Talents), t => Assert.NotEmpty(Effects(t.Triggers, t.Modifiers)));
        Assert.All(ClassTestData.Bastion.Skills.Where(s => s.Kind == SkillKind.Passive), s => Assert.NotEmpty(Effects(s.Triggers, s.Modifiers)));
        Assert.All(ClassTestData.Bastion.Skills.Where(s => s.Kind != SkillKind.Passive), s => Assert.Empty(Effects(s.Triggers, s.Modifiers)));
        Assert.Equal(18, ClassTestData.Bastion.Trees.Sum(t => t.Talents.Count));
        Assert.Equal(15, ClassTestData.Bastion.Skills.Count);   // 6 actives, 1 ultime, 8 passifs
    }

    [Fact]
    public void Classe_livree_au_contenu_0_4_0_et_son_avertissement_de_cycle()
    {
        // Riposte précise et Jugement du colosse frappent sur une parade parfaite : face à un autre Bastion qui pare, la frappe pourrait
        // être parée à son tour. Cycle statique entre deux porteurs : simple avertissement, la règle de cycle et la profondeur arrêtent
        // la chaîne en combat (Q1).
        // Sous-lot 8.7 : le contenu 0.4.0 est figé (Fixtures/content-0.4.0) ; le contenu livré 0.5.0 a son test (Lot87BalanceTests).
        ClassLoadResult result = ClassJsonReader.ReadFile(ScenarioTestData.Content040ClassPath, ClassTestData.Progression);
        Assert.True(result.IsValid, result.Report.ToString());
        Assert.Equal((9, "0.4.0-provisoire"), (result.Class!.SchemaVersion, result.Class.ContentVersion));   // sous-lot 11.4b : figé au schéma 9
        ValidationError warning = Assert.Single(result.Report.Warnings);
        Assert.Equal(("$.data.trees[1].talents[0].triggers[0]", ValidationCode.TriggerCycle), (warning.Path, warning.Code));
        Assert.Contains("talent.bastion.contrecoup.riposte_precise/0, talent.bastion.contrecoup.jugement_du_colosse/0", warning.Message);
        Assert.Equal("sha256:4d9810228108c1c3a761241b987a476415b71be97afcfbf1b12559edcaa282fc", result.Class.Fingerprint);
    }

    [Fact]
    public void Riposte_de_zone_livree_au_dessus_de_la_recharge_minimale()
    {
        // L8-D2 : la seule riposte automatique de zone livrée (Jugement du colosse) a exactement la recharge minimale des règles.
        var area = ClassTestData.Bastion.Trees.SelectMany(t => t.Talents).SelectMany(t => t.Triggers)
            .Concat(ClassTestData.Bastion.Skills.SelectMany(s => s.Triggers)).Where(t => t.IsAreaStrike).ToList();
        Assert.Equal(ShippedData.Rules.Combat.AreaStrikeMinCooldownMs, Assert.Single(area).CooldownMs);
    }

    [Fact]
    public void Alterations_des_talents_du_Gardien_livrees()
    {
        StatusCatalog catalog = ShippedData.Rules.Combat.Statuses;
        Assert.Equal((StatusCategory.Shield, 4000L, StatusStacking.PerSourceKeepStronger), (catalog["reactiveAegis"].Category, catalog["reactiveAegis"].DurationMs, catalog["reactiveAegis"].Stacking));
        StatusDefinition link = catalog["protectiveLink"];
        Assert.Equal((StatusCategory.Protection, 6000L, Fixed.Parse("0.25")), (link.Category, link.DurationMs, link.RedirectRatio));
        Assert.Equal((StatusCategory.Taunt, 2000L), (catalog["oathTaunt"].Category, catalog["oathTaunt"].DurationMs));
        Assert.All(new[] { "reactiveAegis", "protectiveLink", "oathTaunt" }, id => Assert.Equal(StatusResistanceMode.None, catalog[id].Resistance));
        // Le catalogue du domaine (StatusCatalog.Provisional) reprend les mêmes définitions, dans le même ordre.
        Assert.Equal(StatusCatalog.Provisional.Definitions.Select(d => (d.Id, d.Category, d.DurationMs, d.Stacking, d.RedirectRatio)),
            catalog.Definitions.Select(d => (d.Id, d.Category, d.DurationMs, d.Stacking, d.RedirectRatio)));
        Assert.Equal(DataFormat.ProvisionalRulesContentVersion, ShippedData.Rules.ContentVersion);   // 0.8.0 au 8.5, 0.9.0 au 11.4a (déplacement)
    }
}
