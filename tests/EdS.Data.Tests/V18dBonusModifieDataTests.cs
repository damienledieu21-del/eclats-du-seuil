using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.8d : le « bonus modifié » dans les fichiers. Scénarios en schéma 15 (action <c>gear</c> avec son <c>gear</c>), moteur 0.27.0
/// et journal au format 21 (<c>gear</c> d'une commande dans l'en-tête, ligne <c>gearChanged</c>). Nouvelle référence figée
/// <c>Fixtures/reference-0.27.0-contenu-0.6.0-regles-0.15.0</c> : les trois scénarios de la référence précédente (corps identiques, celle-ci
/// restée figée et refusée au rejeu) et <c>entrainement_bonus</c> (un Bastion dont le bonus monte, est remplacé, puis revient à l'entrée).
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Regression")]
public class V18dBonusModifieDataTests
{
    private const string Bonus = "entrainement_bonus";
    private const string Latest = "reference-0.27.0-contenu-0.6.0-regles-0.15.0", Previous = "reference-0.26.0-contenu-0.6.0-regles-0.15.0";

    private static string Fixture(string set, string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", set, name + ".jsonl");

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

    private static ScenarioLoadResult Mutated(Action<JsonObject> mutate) => ReadMutated(mutate, scenario: Bonus);

    private static JsonObject Command(JsonObject root, int index) => root["data"]!["commands"]![index]!.AsObject();

    private static void AssertError(ScenarioLoadResult result, string path, ValidationCode code)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Path == path && e.Code == code);
    }

    [Fact]
    public void Versions_du_sous_lot()
    {
        Assert.Equal(("0.27.0", 21, 15, 4), (CombatEngine.Version, CombatEngine.LogFormatVersion, ScenarioValidator.CurrentSchemaVersion, CombatSnapshot.FormatVersion));
        Assert.Equal("gear", CombatActionIds.Of(CombatAction.Gear));
        Assert.DoesNotContain("gear", CombatActionIds.Named);   // écrite à part, comme consume
    }

    [Fact]
    public void Scenario_lu_commandes_gear_construites()
    {
        CombatSetup setup = Load(Bonus);
        CombatCommand[] gears = setup.Commands.Where(c => c.Action == CombatAction.Gear).ToArray();
        Assert.Equal(3, gears.Length);
        Assert.Equal(new GearBonus(physicalPower: 7, armor: 20, maxHealth: 140), gears[0].Gear);
        Assert.Equal(new GearBonus(physicalPower: 7, armor: 45, maxHealth: 100, statusResistance: 10), gears[1].Gear);
        Assert.Equal(((CharacterSetup)setup.Combatants[0]).Gear, gears[2].Gear);   // retour au bonus de l'entrée (fin des bonus)
        Assert.All(setup.Commands.Where(c => c.Action != CombatAction.Gear), c => Assert.Null(c.Gear));
    }

    [Fact]
    public void Formes_refusees_avec_leur_chemin()
    {
        AssertError(Mutated(r => Command(r, 1).Remove("gear")), "$.data.commands[1].gear", ValidationCode.MissingField);
        AssertError(Mutated(r => Command(r, 1)["gear"]!["vitesse"] = 3), "$.data.commands[1].gear.vitesse", ValidationCode.UnknownReference);
        AssertError(Mutated(r => Command(r, 1)["gear"]!["armor"] = -1), "$.data.commands[1].gear.armor", ValidationCode.OutOfRange);
        AssertError(Mutated(r => Command(r, 1)["gear"]!["armor"] = GearBonus.MaxValue + 1), "$.data.commands[1].gear.armor", ValidationCode.OutOfRange);
        AssertError(Mutated(r => Command(r, 1)["target"] = "assaillant"), "$.data.commands[1].target", ValidationCode.InvalidValue);
        AssertError(Mutated(r => Command(r, 1)["item"] = "item.potion_de_soin"), "$.data.commands[1].item", ValidationCode.InvalidValue);
        AssertError(Mutated(r => Command(r, 0)["gear"] = new JsonObject { ["armor"] = 1 }), "$.data.commands[0].gear", ValidationCode.InvalidValue);
        AssertError(Mutated(r => Command(r, 1)["actor"] = "assaillant"), "$.data.commands[1].actor", ValidationCode.InvalidValue);
        // Bornes exactes : 0 et la limite du combat sont admis.
        Assert.True(Mutated(r => Command(r, 1)["gear"] = new JsonObject()).IsValid);
        Assert.True(Mutated(r => Command(r, 1)["gear"]!["magicResistance"] = GearBonus.MaxValue).IsValid);
    }

    [Fact]
    public void Version_14_refusee_sans_migration()
    {
        AssertError(Mutated(r => r["schemaVersion"] = 14), "$.schemaVersion", ValidationCode.UnsupportedSchemaVersion);
    }

    [Fact]
    public void Schema_JSON_de_l_action_gear()
    {
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "scenario.schema.json")));
        JsonElement command = schema.RootElement.GetProperty("$defs").GetProperty("command");
        Assert.Equal(15, schema.RootElement.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetInt32());
        JsonElement gear = command.GetProperty("properties").GetProperty("gear");
        Assert.Equal(GearBonus.Fields, gear.GetProperty("properties").EnumerateObject().Select(p => p.Name));
        Assert.All(gear.GetProperty("properties").EnumerateObject(), p => Assert.Equal(GearBonus.MaxValue, p.Value.GetProperty("maximum").GetInt32()));
        Assert.Contains(command.GetProperty("allOf").EnumerateArray(), c => c.GetProperty("if").TryGetProperty("properties", out JsonElement p)
            && p.GetProperty("action").GetProperty("const").GetString() == "gear" && c.GetProperty("then").GetProperty("required")[0].GetString() == "gear");
    }

    [Fact]
    public void Journal_identique_a_la_reference_figee_et_rejoue()
    {
        string[] frozen = File.ReadAllLines(Fixture(Latest, Bonus));
        Assert.Equal(frozen, Simulate(Bonus).Lines);
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(frozen, ShippedData).Status);
        List<JsonElement> changes = frozen.Skip(1).Select(l => JsonDocument.Parse(l).RootElement.Clone())
            .Where(e => e.GetProperty("event").GetString() == "gearChanged").ToList();
        Assert.Equal(new[] { 30L, 70L, 150L }, changes.Select(e => e.GetProperty("tick").GetInt64()));
        Assert.All(changes, e => Assert.Equal("setup", e.GetProperty("phase").GetString()));
        // En-tête : le bonus de chaque commande gear, champs non nuls seulement.
        JsonElement header = JsonDocument.Parse(frozen[0]).RootElement;
        JsonElement logged = header.GetProperty("scenario").GetProperty("commands")[1];
        Assert.Equal(("gear", 7), (logged.GetProperty("action").GetString(), logged.GetProperty("gear").GetProperty("physicalPower").GetInt32()));
        Assert.False(logged.GetProperty("gear").TryGetProperty("agilePower", out _));
    }

    [Fact]
    public void Effet_visible_dans_les_degats_des_que_le_bonus_change()
    {
        List<JsonElement> events = Simulate(Bonus).Lines.Skip(1).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();
        long[] hits = events.Where(e => e.GetProperty("event").GetString() == "damage" && e.GetProperty("source").GetString() == "bastion_bonus"
            && e.GetProperty("skill").GetString() == "skill.bastion.frappe_du_rempart").Select(e => e.GetProperty("amount").GetInt64()).ToArray();
        Assert.Equal(2, hits.Length);
        Assert.True(hits[1] > hits[0], $"frappe avec +7 de puissance : {hits[1]}, sans : {hits[0]}");
    }

    [Theory]
    [InlineData("entrainement_declencheurs")]
    [InlineData("entrainement_bouclier")]
    [InlineData("entrainement_equipement")]
    public void Ancienne_reference_refusee_et_corps_reproduit(string name)
    {
        string[] old = File.ReadAllLines(Fixture(Previous, name)), latest = File.ReadAllLines(Fixture(Latest, name));
        ReplayOutcome outcome = CombatLogReplayer.Replay(old, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Contains(outcome.Problems, p => p.Contains("format de journal 20", StringComparison.Ordinal));
        Assert.Equal(old.Skip(1), latest.Skip(1));
        Assert.Equal(latest, Simulate(name).Lines);
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(latest, ShippedData).Status);
    }
}
