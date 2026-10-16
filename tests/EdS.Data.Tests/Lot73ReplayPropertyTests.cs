using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using FsCheck.Xunit;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Lot 7.3.7 : propriété de rejeu. Les assaillants des scénarios d'entraînement livrés reçoivent des déclencheurs générés au hasard
/// (tous types d'abonnement, rôles, effets et cibles, avec les données livrées) : chaque journal produit se rejoue à l'identique,
/// octet par octet, son graphe causal se reconstruit, et aucune ligne de jeu ne dépasse la profondeur 4.
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Trigger")]
public class Lot73ReplayPropertyTests
{
    private static readonly string[] Scenarios = { "entrainement_garde_parade", "entrainement_rupture", "entrainement_alterations" };
    private static readonly string[] Statuses = { "fragility", "bleed", "burn", "stun", "freeze" };
    private static readonly string[] Targets = { "eventSource", "eventTarget", "allEnemies" };

    private static T Pick<T>(Random rng, IReadOnlyList<T> items) => items[rng.Next(items.Count)];

    private static JsonObject Effect(Random rng)
    {
        switch (rng.Next(3))
        {
            case 0:
                return new JsonObject
                {
                    ["type"] = "strike", ["target"] = Pick(rng, Targets), ["coefficient"] = 0.05,
                    ["powerType"] = Pick(rng, new[] { "physique", "magique" }), ["damageType"] = Pick(rng, new[] { "physique", "magique" }),
                    ["unblockable"] = rng.Next(2) == 0,
                };
            case 1:
                string status = Pick(rng, Statuses);
                var apply = new JsonObject { ["type"] = "applyStatus", ["target"] = Pick(rng, Targets.Append("bearer").ToArray()), ["status"] = status };
                if (status is "bleed" or "burn") apply["coefficient"] = 0.1;
                return apply;
            default:
                return new JsonObject { ["type"] = "gainResource", ["resource"] = "resonance", ["amount"] = 1 };
        }
    }

    private static JsonArray Triggers(Random rng)
    {
        var list = new JsonArray();
        for (int i = 0, n = 1 + rng.Next(3); i < n; i++)
        {
            string on = Pick(rng, TriggerIds.Events.Keys.ToArray());
            var trigger = new JsonObject { ["on"] = on };
            IReadOnlyList<TriggerRole> roles = TriggerDefinition.AllowedRoles(TriggerIds.Events[on]);
            if (roles.Count > 1)
            {
                TriggerRole role = Pick(rng, roles);
                trigger["role"] = TriggerIds.Roles.Single(r => r.Value == role).Key;
            }
            trigger["cooldownMs"] = rng.Next(2) * 500;
            var effects = new JsonArray();
            for (int k = 0, m = 1 + rng.Next(2); k < m; k++) effects.Add(Effect(rng));
            trigger["effects"] = effects;
            list.Add(trigger);
        }
        return list;
    }

    private static ScenarioLoadResult Generate(int seed)
    {
        var rng = new Random(seed);
        return ReadMutated(root =>
        {
            foreach (JsonNode? c in DataOf(root)["combatants"]!.AsArray())
                if (c!["type"]?.GetValue<string>() == "assailant") c["triggers"] = Triggers(rng);
        }, scenario: Pick(rng, Scenarios));
    }

    [Fact]
    public void Generateur_produit_surtout_des_scenarios_valides()
    {
        int valid = 0, withReactions = 0;
        for (int seed = 0; seed < 50; seed++)
        {
            if (Generate(seed).Setup is not { } setup) continue;
            valid++;
            var log = new MemoryCombatLog();
            CombatEngine.Run(setup, log);
            if (log.Lines.Any(l => l.Contains("\"depth\":"))) withReactions++;
        }
        Assert.InRange(valid, 35, 50);            // 47 au gel du test
        Assert.InRange(withReactions, 20, 50);    // 35 au gel du test
    }

    [Property(MaxTest = 60)]
    public bool Journal_a_declencheurs_generes_rejoue_octet_par_octet(int seed)
    {
        ScenarioLoadResult loaded = Generate(seed);
        if (loaded.Setup is not { } setup) return true;   // combinaison refusée au chargement : hors propriété
        var log = new MemoryCombatLog();
        CombatEngine.Run(setup, log);
        CausalGraph graph = CausalGraph.Build(log.Lines);
        Assert.All(graph.Nodes.Values, n => Assert.InRange(n.Depth, 0, 4));
        return CombatLogReplayer.Replay(log.Lines, ShippedData).Status == ReplayStatus.Identical;
    }
}
