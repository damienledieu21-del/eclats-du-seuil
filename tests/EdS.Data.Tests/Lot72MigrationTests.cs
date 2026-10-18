using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Lot 7.2 : non-régression de la migration de la Fragilité et de la rupture de garde vers les altérations génériques. Les
/// journaux des trois scénarios de référence produits par le moteur 0.7.0 (format 2, figés dans Fixtures/logs-0.7.0) sont
/// comparés structurellement aux journaux actuels : après suppression des seuls champs ajoutés par le format 3
/// (<c>category</c> et <c>source</c> sur les lignes d'état) et traduction de <c>appliesFragility</c> en <c>applies</c>, l'égalité
/// doit être exacte, ligne par ligne, y compris l'ordre des champs, des lignes et les numéros de séquence.
/// </summary>
[Trait("Category", "Regression")]
[Trait("Category", "Status")]
public class Lot72MigrationTests
{
    private static readonly string[] StateEvents = { "stateApplied", "stateExpired", "stateRemoved" };

    private static string[] Historical(string name) =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "logs-0.7.0", name + ".jsonl"));

    private static IReadOnlyList<string> Current(string name)
    {
        var log = new MemoryCombatLog();
        // Sous-lot 8.4 : contenu historique du Bastion (ScenarioTestData.HistoricalData) ; le contenu livré change le gameplay. Sous-lot
        // 8.5 : scénarios figés du 8.4 (passifs « [TEST] »).
        CombatEngine.Run(ScenarioJsonReader.ReadFile(FrozenScenarioPath(name), HistoricalData).Setup!, log);
        return log.Lines;
    }

    /// <summary>Ancienne écriture d'une attaque : appliesFragility vrai = une application de « fragility », sans coefficient.</summary>
    private static JsonArray AppliesOf(bool appliesFragility) =>
        appliesFragility ? new JsonArray(new JsonObject { ["status"] = "fragility" }) : new JsonArray();

    [Theory]
    [InlineData("mannequin")]
    [InlineData("entrainement_garde_parade")]
    [InlineData("entrainement_rupture")]
    public void Journaux_de_reference_identiques_apres_suppression_des_champs_du_format_3(string name)
    {
        string[] before = Historical(name);
        IReadOnlyList<string> after = Current(name);
        // Correctif 7.2 : une parade parfaite empêche désormais les altérations de l'impact paré. Le journal reste identique
        // jusqu'au premier impact paré qui portait la Fragilité, où stateApplied devient stateBlocked (raison parried).
        int parried = after.ToList().FindIndex(l => l.Contains("\"event\":\"stateBlocked\"") && l.Contains("\"reason\":\"parried\""));
        int compared = parried < 0 ? before.Length : parried;
        if (parried < 0) Assert.Equal(before.Length, after.Count);
        else
        {
            Assert.Equal("entrainement_garde_parade", name);
            JsonObject applied = JsonNode.Parse(before[parried])!.AsObject(), blocked = JsonNode.Parse(after[parried])!.AsObject();
            Assert.Equal(("stateApplied", "fragility"), (applied["event"]!.GetValue<string>(), applied["state"]!.GetValue<string>()));
            Assert.Equal(("fragility", applied["tick"]!.GetValue<long>()), (blocked["state"]!.GetValue<string>(), blocked["tick"]!.GetValue<long>()));
        }

        // En-tête : seules les versions, l'identité des données et l'écriture des attaques et résistances changent.
        JsonObject oldHeader = JsonNode.Parse(before[0])!.AsObject(), newHeader = JsonNode.Parse(after[0])!.AsObject();
        Assert.Equal((2, "0.7.0"), (oldHeader["formatVersion"]!.GetValue<int>(), oldHeader["engineVersion"]!.GetValue<string>()));
        Assert.Equal((CombatEngine.LogFormatVersion, CombatEngine.Version), (newHeader["formatVersion"]!.GetValue<int>(), newHeader["engineVersion"]!.GetValue<string>()));
        foreach (JsonObject header in new[] { oldHeader, newHeader })
            foreach (string key in new[] { "formatVersion", "engineVersion", "data" }) header.Remove(key);
        foreach (JsonNode? combatant in oldHeader["scenario"]!["combatants"]!.AsArray().ToList())
        {
            string type = combatant!["type"]!.GetValue<string>();
            if (type == "character") continue;
            // Ordre des champs du format actuel : statusResistance juste après magicResistance.
            var rebuilt = new JsonObject();
            foreach (var (key, value) in combatant.AsObject().ToList())
            {
                combatant.AsObject().Remove(key);
                rebuilt[key] = value;
                if (key == "magicResistance") rebuilt["statusResistance"] = 0;
            }
            foreach (JsonNode? attack in rebuilt["attacks"]?.AsArray() ?? new JsonArray())
            {
                bool fragility = attack!["appliesFragility"]!.GetValue<bool>();
                attack.AsObject().Remove("appliesFragility");
                attack["applies"] = AppliesOf(fragility);
            }
            combatant.ReplaceWith(rebuilt);
        }
        Assert.Equal(oldHeader.ToJsonString(), newHeader.ToJsonString());

        // Événements : égalité exacte après suppression de category et source sur les lignes d'état.
        int stateLines = 0;
        for (int i = 1; i < compared; i++)
        {
            JsonObject oldLine = JsonNode.Parse(before[i])!.AsObject(), newLine = EdS.TestSupport.Lot8LogFields.RemoveAdded(JsonNode.Parse(after[i])!.AsObject());
            string kind = newLine["event"]!.GetValue<string>();
            Assert.DoesNotContain(kind, new[] { "stateBlocked", "dotDamage" });
            // Lot 7.3 : champs de provenance du format 4, comparés à part (Lot73MigrationTests).
            foreach (string key in EdS.TestSupport.CausalGraph.ProvenanceFields) newLine.Remove(key);
            if (kind == "combatEnded") newLine.Remove("deferredDropped");
            if (StateEvents.Contains(kind))
            {
                stateLines++;
                Assert.Equal(oldLine["state"]!.GetValue<string>() == "fragility" ? "modifier" : "control", newLine["category"]!.GetValue<string>());
                Assert.True(newLine.ContainsKey("source"));
                newLine.Remove("category");
                newLine.Remove("source");
            }
            if (kind == "attackStarted")
            {
                bool fragility = oldLine["appliesFragility"]!.GetValue<bool>();
                Assert.Equal(fragility ? "[\"fragility\"]" : "[]", newLine["applies"]!.ToJsonString());
                oldLine.Remove("appliesFragility");
                newLine.Remove("applies");
            }
            Assert.Equal(oldLine.ToJsonString(), newLine.ToJsonString());
        }
        if (name != "mannequin") Assert.True(stateLines > 0, "le scénario doit exercer la Fragilité ou la rupture de garde");
    }

    [Theory]
    [InlineData("mannequin")]
    [InlineData("entrainement_rupture")]
    public void Journal_au_format_2_refuse_au_rejeu_sans_migration(string name)
    {
        ReplayOutcome outcome = CombatLogReplayer.Replay(Historical(name), ShippedData);
        Assert.NotEqual(ReplayStatus.Identical, outcome.Status);
        Assert.Contains(outcome.Problems, p => p.Contains("format de journal 2") && p.Contains("sans migration"));
    }

    [Fact]
    public void Source_des_lignes_d_etat_de_reference()
    {
        // La Fragilité est posée par l'assaillant ; la rupture de garde du scénario de rupture vient d'un impact de l'assaillant.
        foreach (string name in new[] { "entrainement_garde_parade", "entrainement_rupture" })
            foreach (JsonObject line in Current(name).Skip(1).Select(l => JsonNode.Parse(l)!.AsObject())
                .Where(l => StateEvents.Contains(l["event"]!.GetValue<string>())))
                Assert.Equal("assaillant", line["source"]?.GetValue<string>());
    }
}
