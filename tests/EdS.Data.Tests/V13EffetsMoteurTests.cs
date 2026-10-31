using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Rules;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.3 : effets d'équipement dans le moteur. Règles en schéma 14 (bloc <c>combat.equipment</c>, contenu 0.14.0), scénarios en
/// schéma 14 (<c>equipment</c> sur un personnage), moteur 0.26.0 et journal au format 20 (<c>equipment</c> dans l'en-tête, origine
/// <c>equipment</c> des déclencheurs, condition <c>targetStunned</c>). Nouvelle référence figée
/// <c>Fixtures/reference-0.26.0-contenu-0.6.0-regles-0.14.0</c> : le scénario à déclencheurs et <c>entrainement_bouclier</c> (corps identiques
/// à ceux du moteur 0.25.0, référence restée figée et refusée au rejeu) et <c>entrainement_equipement</c> (deux Bastions, l'un équipé de la
/// Lame du Seuil, de l'Amulette de l'aube et du premier palier de la Garde de la tour, l'autre sans, mêmes coups et mêmes commandes).
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Regression")]
public class V13EffetsMoteurTests
{
    private const string Equipement = "entrainement_equipement";
    private static string Reference(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference-0.26.0-contenu-0.6.0-regles-0.14.0", name + ".jsonl");
    private static string Frozen(string version, string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference-" + version, name + ".jsonl");

    private static CombatSetup Load(string name)
    {
        ScenarioLoadResult result = ScenarioJsonReader.ReadFile(ScenarioPath(name), ShippedData);
        Assert.True(result.IsValid, result.Report.ToString());
        return result.Setup!;
    }

    private static MemoryCombatLog Simulate(string name)
    {
        var log = new MemoryCombatLog();
        CombatEngine.Run(Load(name), log);
        return log;
    }

    private static ScenarioLoadResult Mutated(Action<JsonObject> mutate) => ReadMutated(mutate, scenario: Equipement);

    private static JsonArray Equipment(JsonObject root) => Combatant(root, 0)["equipment"]!.AsArray();

    private static void AssertError(ScenarioLoadResult result, string path, ValidationCode code)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Path == path && e.Code == code);
    }

    // --- Versions ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Versions_du_sous_lot()
    {
        Assert.Equal(("0.26.0", 20), (CombatEngine.Version, CombatEngine.LogFormatVersion));
        Assert.Equal((14, "0.14.0-provisoire"), (DataFormat.RulesSchemaVersion, DataFormat.ProvisionalRulesContentVersion));
        Assert.Equal(14, ScenarioValidator.CurrentSchemaVersion);
        Assert.Equal("0.14.0-provisoire", ShippedData.Rules.ContentVersion);
        CombatRules rules = ShippedData.Rules.Combat;
        Assert.Equal((Fixed.Parse("0.25"), Fixed.Parse("0.15"), Fixed.Parse("0.15"), Fixed.Parse("3"), 1000L, Fixed.One, 2, 1000L),
            (rules.EquipmentMaxDamageDealt, rules.EquipmentMaxDamageTaken, rules.EquipmentMaxHealthPercent, rules.EquipmentMaxEnduranceRegen,
             rules.EquipmentMaxStatusDurationMs, rules.EquipmentMaxStrikeCoefficient, rules.EquipmentMaxGain, rules.EquipmentMinTriggerCooldownMs));
    }

    [Fact]
    public void Regles_au_schema_13_refusees_et_bloc_equipment_obligatoire()
    {
        string rules = File.ReadAllText(Path.Combine(DataDirectory, "rules.json"));
        Assert.False(RulesJsonReader.Read(rules.Replace("\"schemaVersion\": 14", "\"schemaVersion\": 13")).IsValid);
        JsonObject root = JsonNode.Parse(rules)!.AsObject();
        root["data"]!["combat"]!.AsObject().Remove("equipment");
        RulesLoadResult missing = RulesJsonReader.Read(root.ToJsonString());
        Assert.Contains(missing.Report.Errors, e => e.Path.StartsWith("$.data.combat.equipment", StringComparison.Ordinal));
        root = JsonNode.Parse(rules)!.AsObject();
        root["data"]!["combat"]!["equipment"]!["maxDamageTaken"] = 1.5;   // une réduction au-delà de 1
        Assert.Contains(RulesJsonReader.Read(root.ToJsonString()).Report.Errors, e => e.Path == "$.data.combat.equipment.maxDamageTaken");
    }

    // --- Scénario ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Effets_lus_dans_le_scenario()
    {
        CombatSetup setup = Load(Equipement);
        var equipped = Assert.IsType<CharacterSetup>(setup.Combatants[0]);
        var bare = Assert.IsType<CharacterSetup>(setup.Combatants[1]);
        Assert.Equal(new[] { "item.lame_du_seuil", "item.amulette_de_l_aube", "set.garde_de_la_tour.2" }, equipped.Equipment.Select(e => e.Source));
        Assert.Empty(bare.Equipment);
        Assert.Equal(new[] { ModifierCondition.TargetStunned }, equipped.Equipment[0].Modifiers.Single().Conditions);
        BoundTrigger amulet = Assert.Single(equipped.ActiveTriggers(), t => t.Origin == TriggerOrigin.Equipment);
        Assert.Equal("item.amulette_de_l_aube/0", amulet.Label);
    }

    [Fact]
    public void Effets_hors_des_bornes_refuses_avec_leur_chemin()
    {
        AssertError(Mutated(r => Equipment(r)[0]!["modifiers"]![0]!["add"] = 0.26), "$.data.combatants[0].equipment[0].modifiers[0].add", ValidationCode.InvalidValue);
        AssertError(Mutated(r => Equipment(r)[0]!["modifiers"]![0]!["key"] = "guard.damageReduction"), "$.data.combatants[0].equipment[0].modifiers[0].key",
            ValidationCode.UnknownReference);
        AssertError(Mutated(r => Equipment(r)[1]!["triggers"]![0]!["cooldownMs"] = 950), "$.data.combatants[0].equipment[1].triggers[0].cooldownMs", ValidationCode.InvalidValue);
        AssertError(Mutated(r => Equipment(r)[1]!["triggers"]![0]!["effects"]![0]!["amount"] = 3), "$.data.combatants[0].equipment[1].triggers[0].effects[0].amount",
            ValidationCode.InvalidValue);
        AssertError(Mutated(r => Equipment(r)[2]!["id"] = "item.lame_du_seuil"), "$.data.combatants[0].equipment[2].id", ValidationCode.DuplicateId);
        AssertError(Mutated(r => Equipment(r)[0]!.AsObject().Remove("id")), "$.data.combatants[0].equipment[0].id", ValidationCode.MissingField);
        Assert.True(Mutated(r => Equipment(r)[0]!["modifiers"]![0]!["add"] = 0.25).IsValid);
    }

    [Fact]
    public void Effets_mal_places_ou_condition_inconnue_refuses()
    {
        // Sur un assaillant : champ réservé aux personnages.
        AssertError(Mutated(r => Combatant(r, 2)["equipment"] = Equipment(r).DeepClone()), "$.data.combatants[2].equipment", ValidationCode.InvalidValue);
        // targetStunned n'existe que pour l'équipement : un assaillant ne porte pas de modificateur, et une condition inconnue est refusée.
        AssertError(Mutated(r => Equipment(r)[0]!["modifiers"]![0]!["conditions"] = new JsonArray("perRampartCharge")),
            "$.data.combatants[0].equipment[0].modifiers[0].conditions", ValidationCode.InvalidValue);
        Assert.False(Mutated(r => Equipment(r)[0]!["modifiers"]![0]!["conditions"] = new JsonArray("cibleAuSol")).IsValid);
        Assert.False(Mutated(r => r["schemaVersion"] = 13).IsValid);   // un fichier de schéma 13 ne connaît pas l'équipement : mis à jour à la main
    }

    // --- Moteur ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Les_effets_changent_le_combat_a_commandes_egales()
    {
        CombatResult result = CombatEngine.Run(Load(Equipement), new MemoryCombatLog());
        CombatantResult equipped = result["bastion_equipe"], bare = result["bastion_nu"];
        // Mêmes coups (100 bruts, toutes les 2 s), mêmes commandes : Heurt de bouclier (étourdit 1,5 s), Frappe du rempart sur la cible
        // étourdie (+10 % avec la Lame du Seuil), garde levée, une parade parfaite (+1 Charge avec l'Amulette de l'aube), deux blocages (−5 %
        // en garde avec la Garde de la tour à 2 pièces).
        Assert.Equal((59L, 56L), (equipped.DamageDealt, bare.DamageDealt));
        Assert.Equal((86L, 90L), (equipped.DamageTaken, bare.DamageTaken));
        Assert.Equal(((int?)2, (int?)1), (equipped.RampartCharges, bare.RampartCharges));
        Assert.Equal((1L, 1L), (equipped.PerfectParries, bare.PerfectParries));
        Assert.Equal((2L, 2L), (equipped.Blocks, bare.Blocks));
    }

    [Fact]
    public void Reaction_d_equipement_journalisee_avec_son_origine_et_conditionnels_dans_les_regles_effectives()
    {
        MemoryCombatLog log = Simulate(Equipement);
        List<JsonElement> lines = log.Lines.Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();
        JsonElement gain = Assert.Single(lines, e => e.TryGetProperty("trigger", out JsonElement t) && t.GetString() == "item.amulette_de_l_aube/0@bastion_equipe#0");
        Assert.Equal("resourceGained", gain.GetProperty("event").GetString());
        JsonElement rules = lines.Single(e => e.TryGetProperty("event", out JsonElement ev) && ev.GetString() == "combatant"
            && e.GetProperty("combatant").GetString() == "bastion_equipe").GetProperty("rules");
        Assert.Equal("[{\"key\":\"damageDealt\",\"conditions\":[\"targetStunned\"],\"value\":0.1},{\"key\":\"damageTaken\",\"conditions\":[\"whileGuarding\"],\"value\":-0.05}]",
            rules.GetProperty("conditional").GetRawText());
    }

    [Fact]
    public void Effets_dans_l_en_tete_et_rejeu_identique()
    {
        MemoryCombatLog log = Simulate(Equipement);
        JsonElement header = JsonDocument.Parse(log.Lines[0]).RootElement;
        JsonElement equipment = header.GetProperty("scenario").GetProperty("combatants")[0].GetProperty("equipment");
        Assert.Equal(3, equipment.GetArrayLength());
        Assert.Equal("{\"id\":\"item.lame_du_seuil\",\"modifiers\":[{\"key\":\"damageDealt\",\"add\":0.1,\"conditions\":[\"targetStunned\"]}]}", equipment[0].GetRawText());
        Assert.False(header.GetProperty("scenario").GetProperty("combatants")[1].TryGetProperty("equipment", out _));
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines, ShippedData).Status);
        CombatSetup? again = CombatLogReplayer.ReadSetup(log.Lines[0], ShippedData, out IReadOnlyList<string> problems);
        Assert.Empty(problems);
        Assert.Equal(3, Assert.IsType<CharacterSetup>(again!.Combatants[0]).Equipment.Count);
    }

    [Fact]
    public void Sans_effet_rien_ne_change()
    {
        // Le Bastion « équipé » sans son équipement se bat exactement comme le Bastion nu, et le Bastion nu fait le même combat qu'à côté d'un
        // Bastion équipé (chaque Bastion a son assaillant).
        ScenarioLoadResult plain = Mutated(r => Combatant(r, 0).Remove("equipment"));
        Assert.True(plain.IsValid, plain.Report.ToString());
        CombatResult without = CombatEngine.Run(plain.Setup!, new MemoryCombatLog()), with = CombatEngine.Run(Load(Equipement), new MemoryCombatLog());
        static (long, long, int?, long, long) Of(CombatantResult r) => (r.DamageDealt, r.DamageTaken, r.RampartCharges, r.Blocks, r.PerfectParries);
        Assert.Equal(Of(without["bastion_nu"]), Of(without["bastion_equipe"]));
        Assert.Equal(Of(with["bastion_nu"]), Of(without["bastion_nu"]));
        // Sans équipement, l'en-tête n'écrit pas de champ equipment.
        var log = new MemoryCombatLog();
        CombatEngine.Run(plain.Setup!, log);
        Assert.DoesNotContain("\"equipment\"", log.Lines[0], StringComparison.Ordinal);
    }

    // --- Références figées -------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("entrainement_declencheurs")]
    [InlineData("entrainement_bouclier")]
    [InlineData(Equipement)]
    public void Journaux_identiques_octet_par_octet_aux_references_figees(string name)
    {
        Assert.Equal(File.ReadAllText(Reference(name)), Simulate(name).ToString());
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(File.ReadAllLines(Reference(name)), ShippedData).Status);
    }

    [Theory]
    [InlineData("entrainement_declencheurs")]
    [InlineData("entrainement_bouclier")]
    public void Reference_du_moteur_0_25_0_refusee_et_corps_reproduit(string name)
    {
        // La référence du moteur 0.25.0 (règles 0.13.0) reste figée ; elle est refusée au rejeu (moteur et règles) ; le moteur 0.26.0 en
        // reproduit octet par octet les lignes d'événements : ces scénarios n'ont pas d'effet d'équipement.
        string[] frozen = File.ReadAllLines(Frozen("0.25.0-contenu-0.6.0-regles-0.13.0", name));
        ReplayOutcome outcome = CombatLogReplayer.Replay(frozen, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Contains(outcome.Problems, p => p.Contains("0.25.0", StringComparison.Ordinal));
        Assert.Contains(outcome.Problems, p => p.Contains("0.13.0-provisoire", StringComparison.Ordinal));
        string[] current = File.ReadAllLines(Reference(name));
        Assert.Equal(frozen.Skip(1), current.Skip(1));
        JsonElement before = JsonDocument.Parse(frozen[0]).RootElement, after = JsonDocument.Parse(current[0]).RootElement;
        Assert.Equal((19, "0.25.0"), (before.GetProperty("formatVersion").GetInt32(), before.GetProperty("engineVersion").GetString()));
        Assert.Equal((20, "0.26.0"), (after.GetProperty("formatVersion").GetInt32(), after.GetProperty("engineVersion").GetString()));
        Assert.Equal(before.GetProperty("scenario").GetRawText(), after.GetProperty("scenario").GetRawText());
        Assert.Equal(before.GetProperty("data").GetProperty("classes").GetRawText(), after.GetProperty("data").GetProperty("classes").GetRawText());
    }
}
