using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Classes;
using EdS.Data.Json;
using EdS.Data.Rules;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 8.4 : fiches complètes des compétences du Bastion en données (feuille de route, catalogue du Bastion), altérations livrées,
/// champs de classe 8 (<c>rampartChargeSpendAll</c>, <c>coefficientPerRampartCharge</c>, <c>resonanceGain</c>), règles 9
/// (<c>combat.triggers.areaStrikeMinCooldownMs</c>, L8-D2 ; <c>guard.noUpkeep</c> sur un renforcement), et plus aucune compétence
/// non implémentée dans les scénarios livrés.
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Bastion")]
public class Lot84BastionDataTests
{
    private static SkillCombatDefinition Combat(string id)
    {
        Assert.True(ClassTestData.Bastion.TryGetSkill("skill.bastion." + id, out SkillDefinition skill), id);
        return skill.Combat!;
    }

    private static string? Coefficient(SkillCombatDefinition c) => c.HasDamage ? c.Coefficient.ToString() : null;

    [Fact]
    public void Fiches_des_competences_conformes_a_la_feuille_de_route()
    {
        // (coefficient, endurance, recharge, ciblage) de la feuille de route ; toutes provisoires.
        var expected = new (string Id, string? Coefficient, int Endurance, long CooldownMs, SkillTargeting Targeting)[]
        {
            ("frappe_du_rempart", "1.2", 10, 6000, SkillTargeting.Enemy),
            ("heurt_de_bouclier", "0.8", 15, 10_000, SkillTargeting.Enemy),
            ("onde_de_rupture", "0.5", 20, 12_000, SkillTargeting.AllEnemies),
            ("contrecoup", "0.6", 10, 8000, SkillTargeting.Enemy),
            ("provocation", null, 10, 12_000, SkillTargeting.Enemy),
            ("barriere_d_acier", null, 20, 20_000, SkillTargeting.Ally),
            ("bastion_absolu", null, 0, 0, SkillTargeting.Self),
        };
        foreach (var e in expected)
        {
            SkillCombatDefinition c = Combat(e.Id);
            Assert.Equal((e.Coefficient, e.Endurance, e.CooldownMs, e.Targeting, true), (Coefficient(c), c.EnduranceCost, c.CooldownMs, c.Targeting, c.IsProvisional));
        }
        Assert.Equal(new[] { "stun" }, Combat("heurt_de_bouclier").Applies.Select(a => a.Status));
        Assert.Equal(new[] { "fragility" }, Combat("onde_de_rupture").Applies.Select(a => a.Status));
        // Contrecoup : toutes les Charges, au moins une, 0,6 + 0,4 par Charge.
        SkillCombatDefinition contrecoup = Combat("contrecoup");
        Assert.Equal((1, true, Fixed.Parse("0.4")), (contrecoup.RampartChargeCost, contrecoup.RampartChargeSpendAll, contrecoup.CoefficientPerRampartCharge));
        // Provocation : 3 s sur l'assaillant, +5 Résonance. Barrière d'acier : bouclier de 15 % des PV max, 6 s, sur soi ou un allié.
        Assert.Equal((5, "taunt"), (Combat("provocation").ResonanceGain, Combat("provocation").Applies.Single().Status));
        StatusApplication barrier = Combat("barriere_d_acier").Applies.Single();
        Assert.Equal(("steelBarrier", (Fixed?)Fixed.Parse("0.15")), (barrier.Status, barrier.Coefficient));
        // Ultime : alliés −30 %, garde sans entretien pour le Bastion, provocation de tous les ennemis.
        Assert.Equal(new[] { ("bastionAbsolute", ApplicationTarget.AllAllies), ("bastionAbsoluteGuard", ApplicationTarget.Targets), ("bastionAbsoluteTaunt", ApplicationTarget.AllEnemies) },
            Combat("bastion_absolu").Applies.Select(a => (a.Status, a.Target)));
        Assert.Equal(100, ShippedData.Rules.Combat.UltimateResonanceCost);
    }

    [Fact]
    public void Alterations_du_Bastion_livrees()
    {
        StatusCatalog catalog = ShippedData.Rules.Combat.Statuses;
        Assert.Equal((StatusCategory.Taunt, 3000L), (catalog["taunt"].Category, catalog["taunt"].DurationMs));
        Assert.Equal((StatusCategory.Shield, 6000L), (catalog["steelBarrier"].Category, catalog["steelBarrier"].DurationMs));
        Assert.Equal((StatusCategory.Taunt, 8000L), (catalog["bastionAbsoluteTaunt"].Category, catalog["bastionAbsoluteTaunt"].DurationMs));
        foreach (var (id, key, value) in new[] { ("bastionAbsolute", "damageTaken", "-0.3"), ("bastionAbsoluteGuard", "guard.noUpkeep", "1") })
        {
            StatusDefinition d = catalog[id];
            Assert.Equal((StatusCategory.Buff, 8000L, StatusStacking.Refresh), (d.Category, d.DurationMs, d.Stacking));
            ModifierDefinition m = Assert.Single(d.Modifiers);
            Assert.Equal((key, Fixed.Parse(value)), (m.Key, m.Add.Base));
        }
    }

    [Fact]
    public void Recharge_minimale_des_ripostes_de_zone_lue_et_controlee()
    {
        Assert.Equal(3000, ShippedData.Rules.Combat.AreaStrikeMinCooldownMs);
        const string path = "$.data.combat.triggers.areaStrikeMinCooldownMs";
        RulesLoadResult missing = RulesTestData.ReadMutated(r => RulesTestData.Remove(r, "combat.triggers.areaStrikeMinCooldownMs"));
        Assert.Contains(missing.Report.Errors, e => e.Path == path && e.Code == ValidationCode.MissingField);
        foreach (long invalid in new[] { -50L, 3025L })
            Assert.Contains(RulesTestData.ReadMutated(r => RulesTestData.Set(r, "combat.triggers.areaStrikeMinCooldownMs", invalid)).Report.Errors, e => e.Path == path);
        // L'empreinte des règles dépend de la valeur ; 0 (configuration de test) est admis.
        RulesLoadResult zero = RulesTestData.ReadMutated(r => RulesTestData.Set(r, "combat.triggers.areaStrikeMinCooldownMs", 0));
        Assert.True(zero.IsValid, zero.Report.ToString());
        Assert.NotEqual(ShippedData.Rules.Fingerprint, zero.Rules!.Fingerprint);
    }

    private static JsonNode AreaRiposte(long cooldownMs) => JsonNode.Parse($$"""
        { "on": "onHitDealt", "cooldownMs": {{cooldownMs}},
          "effects": [ { "type": "strike", "target": "allEnemies", "coefficient": 1, "powerType": "physique", "damageType": "physique" } ] }
        """)!;

    [Theory]
    [InlineData(2950, false)]
    [InlineData(3000, true)]
    public void Riposte_de_zone_d_un_scenario_2950_ms_refusee_3000_acceptee(long cooldownMs, bool accepted)
    {
        ScenarioLoadResult result = ReadMutated(s => Combatant(s, 1)["triggers"] = new JsonArray(AreaRiposte(cooldownMs)), scenario: "entrainement_garde_parade");
        if (accepted)
        {
            Assert.True(result.IsValid, result.Report.ToString());
            return;
        }
        ValidationError error = Assert.Single(result.Report.Errors);
        Assert.Equal(("$.data.combatants[1].triggers[0].cooldownMs", ValidationCode.InvalidValue), (error.Path, error.Code));
        Assert.Contains("au moins 3000 ms", error.Message);
    }

    [Fact]
    public void Rupture_de_garde_refusee_pour_une_provocation()
    {
        // Revue Codex de la PR #7, corrigée au 8.4 : seulement un contrôle ou un modificateur.
        RulesLoadResult result = RulesTestData.ReadMutated(r => r["data"]!["combat"]!["guard"]!["breakStatus"] = "taunt");
        Assert.Contains(result.Report.Errors, e => e.Path == "$.data.combat.guard.breakStatus" && e.Code == ValidationCode.InvalidValue);
    }

    [Fact]
    public void Champs_de_Charges_et_de_Resonance_controles_avec_leur_chemin()
    {
        // skills[2] Provocation (soutien), skills[3] Contrecoup, skills[6] Bastion absolu (ultime).
        ClassLoadResult support = ClassTestData.ReadMutated(r => ClassTestData.Skill(r, 2)["combat"]!["coefficientPerRampartCharge"] = 0.4);
        Assert.Contains(support.Report.Errors, e => e.Path == "$.data.skills[2].combat.coefficientPerRampartCharge" && e.Code == ValidationCode.InvalidValue);
        ClassLoadResult ultimate = ClassTestData.ReadMutated(r => ClassTestData.Skill(r, 6)["combat"]!["resonanceGain"] = 5);
        Assert.Contains(ultimate.Report.Errors, e => e.Path == "$.data.skills[6].combat.resonanceGain" && e.Code == ValidationCode.InvalidValue);
        ClassLoadResult type = ClassTestData.ReadMutated(r => ClassTestData.Skill(r, 3)["combat"]!["rampartChargeSpendAll"] = "oui");
        Assert.Contains(type.Report.Errors, e => e.Path == "$.data.skills[3].combat.rampartChargeSpendAll" && e.Code == ValidationCode.WrongType);
        ClassLoadResult negative = ClassTestData.ReadMutated(r => ClassTestData.Skill(r, 2)["combat"]!["resonanceGain"] = -1);
        Assert.Contains(negative.Report.Errors, e => e.Path == "$.data.skills[2].combat.resonanceGain");
    }

    [Fact]
    public void Champs_facultatifs_dans_la_forme_canonique()
    {
        string shipped = ClassJsonReader.Read(ClassTestData.BastionJson, ClassTestData.Progression).Class!.Fingerprint;
        string Fingerprint(Action<JsonObject> mutate) => ClassTestData.ReadMutated(mutate).Class!.Fingerprint;
        string[] fingerprints =
        {
            shipped,
            Fingerprint(r => ClassTestData.Skill(r, 3)["combat"]!["coefficientPerRampartCharge"] = 0.5),
            Fingerprint(r => ClassTestData.Skill(r, 3)["combat"]!.AsObject().Remove("rampartChargeSpendAll")),
            Fingerprint(r => ClassTestData.Skill(r, 3)["combat"]!["rampartChargeSpendAll"] = false),
            Fingerprint(r => ClassTestData.Skill(r, 2)["combat"]!["resonanceGain"] = 6),
            Fingerprint(r => ClassTestData.Skill(r, 2)["combat"]!.AsObject().Remove("resonanceGain")),
        };
        Assert.Equal(fingerprints.Length, fingerprints.Distinct().Count());
    }

    [Fact]
    public void Schema_de_classe_conforme_aux_fiches()
    {
        MiniJsonSchema schema = MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "class.schema.json"));
        bool Valid(JsonObject root) => schema.IsValid(JsonDocument.Parse(root.ToJsonString()).RootElement);
        Assert.True(Valid(ClassTestData.Shipped()));
        // Coefficient par Charge sans dégâts directs, ou sans dépense de Charges : refusé par le schéma comme par le programme.
        JsonObject support = ClassTestData.Shipped();
        ClassTestData.Skill(support, 2)["combat"]!["coefficientPerRampartCharge"] = 0.4;
        Assert.False(Valid(support));
        JsonObject noSpend = ClassTestData.Shipped();
        JsonObject frappe = ClassTestData.Skill(noSpend, 0)["combat"]!.AsObject();
        frappe["coefficientPerRampartCharge"] = 0.4;
        Assert.False(Valid(noSpend));
        frappe["rampartChargeSpendAll"] = true;
        Assert.True(Valid(noSpend));
        Assert.False(ClassTestData.ReadMutated(r => ClassTestData.Skill(r, 0)["combat"]!["coefficientPerRampartCharge"] = 0.4).IsValid);
        Assert.True(ClassTestData.ReadMutated(r =>
        {
            ClassTestData.Skill(r, 0)["combat"]!["coefficientPerRampartCharge"] = 0.4;
            ClassTestData.Skill(r, 0)["combat"]!["rampartChargeSpendAll"] = true;
        }).IsValid);
    }

    [Theory]
    [InlineData("mannequin")]
    [InlineData("entrainement_garde_parade")]
    [InlineData("entrainement_rupture")]
    [InlineData("entrainement_alterations")]
    [InlineData("entrainement_declencheurs")]
    public void Plus_aucune_competence_non_implementee(string name)
    {
        var log = new MemoryCombatLog();
        CombatEngine.Run(ScenarioJsonReader.ReadFile(ScenarioPath(name), ShippedData).Setup!, log);
        Assert.DoesNotContain(log.Lines, l => l.Contains("skillNotImplemented", StringComparison.Ordinal));
    }

    [Fact]
    public void Provocation_du_mannequin_lancee_et_refusee_notApplicable()
    {
        // Le scénario du mannequin lance la Provocation (refusée « skillNotImplemented » jusqu'au 8.3) : le lancement passe, la
        // provocation est refusée (le mannequin n'est pas un assaillant), la Résonance de la fiche est gagnée.
        var log = new MemoryCombatLog();
        CombatResult result = CombatEngine.Run(ScenarioJsonReader.ReadFile(MannequinPath, ShippedData).Setup!, log);
        List<JsonElement> events = log.Lines.Skip(1).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();
        static string? Str(JsonElement e, string name) => e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        static long? Num(JsonElement e, string name) => e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : null;
        JsonElement cast = events.Single(e => Str(e, "event") == "skillCast" && Str(e, "skill") == "skill.bastion.provocation");
        long? eid = Num(cast, "eid");
        // stateBlocked est un diagnostic (sans provenance) : même tick que le lancement.
        Assert.Contains(events, e => Str(e, "event") == "stateBlocked" && Str(e, "state") == "taunt" && Str(e, "reason") == "notApplicable"
            && Num(e, "tick") == Num(cast, "tick"));
        Assert.Contains(events, e => Str(e, "event") == "resourceGained" && Str(e, "source") == "skill" && Num(e, "amount") == 5 && Num(e, "cause") == eid);
        Assert.Equal((17, 1), (result["bastion"].SkillsCast, result["bastion"].CommandsRejected));
    }
}
