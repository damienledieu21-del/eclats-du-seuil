using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using Xunit.Abstractions;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Lot 7.1 : capacités mesurées. Pics de charge (travail par tick, lignes par tick, taille de la file) des trois scénarios
/// de référence et de scénarios synthétiques au plafond d'admission, avec les règles livrées. Les valeurs sont reportées
/// dans docs/LIMITES_LOT_7.md ; un changement du moteur ou des données qui les modifie doit être fait sciemment.
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Limits")]
public class Lot71CapacityTests(ITestOutputHelper output)
{
    private (CombatResult Result, MemoryCombatLog Log) Measure(string label, CombatSetup setup)
    {
        var log = new MemoryCombatLog();
        CombatResult result = CombatEngine.Run(setup, log);
        // Correctif 7.2 : chaque scénario mesuré a passé le contrôle de saturation ; son pic de file reste sous le majorant.
        CombatSetup.QueueBound bound = setup.ComputeQueueBound();
        output.WriteLine($"{label} : fin {result.EndReason}{(result.AbortReason is null ? "" : " (" + result.AbortReason + ")")} au tick {result.EndTick}, " +
            $"travail {result.Peaks.WorkPerTick}, lignes {result.Peaks.EventsPerTick}, file {result.Peaks.PendingEvents} (majorant {bound.Total}), journal {log.Lines.Count} lignes");
        Assert.InRange(result.Peaks.PendingEvents, 0, bound.Total);
        return (result, log);
    }

    private static CombatSetup Load(JsonObject root)
    {
        ScenarioLoadResult loaded = ScenarioJsonReader.Read(Lot71Files.ToJson(root), ShippedData);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        return loaded.Setup!;
    }

    [Theory]
    [InlineData("mannequin", 14, 4, 18)]
    [InlineData("entrainement_garde_parade", 18, 7, 19)]   // correctif 7.2 : 20 → 18, la Fragilité parée ne s'applique plus
    [InlineData("entrainement_rupture", 20, 7, 7)]
    [InlineData("entrainement_alterations", 26, 8, 14)]
    public void Pics_des_scenarios_de_reference(string name, long work, long events, long pending)
    {
        var (result, _) = Measure(name, ScenarioJsonReader.ReadFile(ScenarioPath(name), ShippedData).Setup!);
        Assert.NotEqual(CombatEndReason.Aborted, result.EndReason);
        Assert.Equal((work, events, pending), (result.Peaks.WorkPerTick, result.Peaks.EventsPerTick, result.Peaks.PendingEvents));
    }

    private static JsonObject Bastion(JsonObject mannequin, string id, string team)
    {
        JsonObject b = DataOf(mannequin)["combatants"]![0]!.DeepClone().AsObject();
        b["id"] = id;
        b["team"] = team;
        return b;
    }

    private static JsonObject AssailantJson(string id, string? target, string targeting, bool fragility, long intervalMs) => new()
    {
        ["id"] = id, ["type"] = "assailant", ["team"] = "cibles", ["initiative"] = 0, ["armor"] = 0, ["magicResistance"] = 0,
        ["target"] = target, ["firstAttackMs"] = 0, ["intervalMs"] = intervalMs,
        ["attacks"] = new JsonArray(new JsonObject
        {
            ["id"] = "coup", ["rawDamage"] = 1, ["damageType"] = "physique", ["targeting"] = targeting,
            ["unblockable"] = false, ["applies"] = fragility ? new JsonArray(new JsonObject { ["status"] = "fragility" }) : new JsonArray(),
        }),
    };

    /// <summary>
    /// Plafond d'admission en commandes : 63 personnages contre un mannequin, 4 commandes par personnage et par tick
    /// (frappe, garde, parade, fin de garde) tant que le plafond de 20 000 commandes le permet.
    /// </summary>
    private static JsonObject CommandsAtCap()
    {
        JsonObject root = Shipped();
        var combatants = new JsonArray();
        for (int i = 0; i < 63; i++) combatants.Add(Bastion(root, $"bastion_{i:00}", "joueurs"));
        combatants.Add(DataOf(root)["combatants"]![1]!.DeepClone());
        var commands = new JsonArray();
        for (long tick = 0; commands.Count + 4 * 63 <= 20_000; tick++)
            for (int i = 0; i < 63; i++)
            {
                string actor = $"bastion_{i:00}";
                long at = tick * 50;
                commands.Add(new JsonObject { ["atMs"] = at, ["actor"] = actor, ["skill"] = "skill.bastion.frappe_du_rempart", ["target"] = "mannequin" });
                commands.Add(new JsonObject { ["atMs"] = at, ["actor"] = actor, ["action"] = "parry" });
                commands.Add(new JsonObject { ["atMs"] = at, ["actor"] = actor, ["action"] = "guardStart" });
                commands.Add(new JsonObject { ["atMs"] = at, ["actor"] = actor, ["action"] = "guardStop" });
            }
        DataOf(root)["combatants"] = combatants;
        DataOf(root)["commands"] = commands;
        DataOf(root)["durationMs"] = 30_000;
        return root;
    }

    /// <summary>32 personnages en garde contre 32 assaillants frappant à chaque tick, de zone ou non, avec ou sans Fragilité.</summary>
    private static JsonObject AssailantsAtCap(string targeting, bool fragility)
    {
        JsonObject root = Shipped();
        var combatants = new JsonArray();
        var commands = new JsonArray();
        for (int i = 0; i < 32; i++)
        {
            combatants.Add(Bastion(root, $"bastion_{i:00}", "joueurs"));
            commands.Add(new JsonObject { ["atMs"] = 0, ["actor"] = $"bastion_{i:00}", ["action"] = "guardStart" });
        }
        for (int i = 0; i < 32; i++)
            combatants.Add(AssailantJson($"assaillant_{i:00}", targeting == "enemy" ? $"bastion_{i:00}" : null, targeting, fragility, 50));
        DataOf(root)["combatants"] = combatants;
        DataOf(root)["commands"] = commands;
        DataOf(root)["durationMs"] = 30_000;
        return root;
    }

    [Fact]
    public void Plafond_d_admission_en_commandes_tient_dans_les_budgets()
    {
        var (result, _) = Measure("commandes au plafond (63 × 4 par tick)", Load(CommandsAtCap()));
        Assert.NotEqual(CombatEndReason.Aborted, result.EndReason);
        Assert.Equal((1011, 378, 19_908), (result.Peaks.WorkPerTick, result.Peaks.EventsPerTick, result.Peaks.PendingEvents));
    }

    [Fact]
    public void Assaillants_cible_unique_tiennent_dans_les_budgets()
    {
        var (result, _) = Measure("32 assaillants, cible unique, Fragilité", Load(AssailantsAtCap("enemy", fragility: true)));
        Assert.NotEqual(CombatEndReason.Aborted, result.EndReason);
        Assert.Equal((608, 192, 3872), (result.Peaks.WorkPerTick, result.Peaks.EventsPerTick, result.Peaks.PendingEvents));
    }

    [Fact]
    public void Assaillants_de_zone_depassent_le_budget_de_lignes()
    {
        // 32 attaques de zone × 32 cibles en garde : plus de 1000 lignes dès le premier tick.
        var (result, _) = Measure("32 assaillants, zone", Load(AssailantsAtCap("allEnemies", fragility: false)));
        Assert.Equal(("eventBudgetExceeded", 0L), (result.AbortReason, result.EndTick));
    }

    /// <summary>
    /// Pire cas pour la file avec le contenu actuel : la Fragilité est le seul état à échéance, et chaque application
    /// écrit au moins deux lignes (dégâts, état). Quinze assaillants de zone sur 32 personnages sans garde restent juste
    /// sous 1000 lignes par tick ; la file accumule les échéances renouvelées pendant les 6 s de la Fragilité.
    /// </summary>
    [Fact]
    public void Assaillants_de_zone_avec_Fragilite_sous_le_budget_de_lignes()
    {
        JsonObject root = AssailantsAtCap("allEnemies", fragility: true);
        JsonArray combatants = DataOf(root)["combatants"]!.AsArray();
        for (int i = combatants.Count - 1; i >= 32 + 15; i--) combatants.RemoveAt(i);
        foreach (JsonNode? c in combatants.Take(32))
            c!["build"]!["attributes"]!["vitalite"] = 200;   // survivre plus longtemps que la Fragilité
        DataOf(root)["commands"] = new JsonArray();
        var (result, _) = Measure("15 assaillants de zone, Fragilité, sans garde", Load(root));
        // La file atteint 57 615 éléments (58 % de 100 000), le travail 3082 (31 % de 10 000). Le budget de lignes
        // s'arrête au tick 139, celui où les 32 personnages meurent ensemble (died et stateRemoved : 64 lignes de plus).
        Assert.Equal((3082L, 999L, 57_615L), (result.Peaks.WorkPerTick, result.Peaks.EventsPerTick, result.Peaks.PendingEvents));
        Assert.Equal(("eventBudgetExceeded", 139L), (result.AbortReason, result.EndTick));
    }

    // --- Lot 7.2 : altérations -----------------------------------------------------------------

    private static JsonObject Application(string status, double? coefficient = null)
    {
        var application = new JsonObject { ["status"] = status };
        if (coefficient is { } c) application["coefficient"] = c;
        return application;
    }

    /// <summary>
    /// 32 personnages (vitalité 200) contre <paramref name="assailants"/> assaillants frappant à chaque tick, chacun enchaînant
    /// les attaques données (identifiants a0, a1…), de zone ou sur « son » personnage.
    /// </summary>
    private static JsonObject StatusesAtCap(int assailants, string targeting, params JsonArray[] attacks)
    {
        JsonObject root = Shipped();
        var combatants = new JsonArray();
        for (int i = 0; i < 32; i++)
        {
            JsonObject b = Bastion(root, $"bastion_{i:00}", "joueurs");
            b["build"]!["attributes"]!["vitalite"] = 200;
            combatants.Add(b);
        }
        for (int i = 0; i < assailants; i++)
        {
            JsonObject a = AssailantJson($"assaillant_{i:00}", targeting == "enemy" ? $"bastion_{i % 32:00}" : null, targeting, false, 50);
            var list = new JsonArray();
            for (int k = 0; k < attacks.Length; k++)
                list.Add(new JsonObject
                {
                    ["id"] = $"a{k}", ["rawDamage"] = 1, ["damageType"] = "physique", ["targeting"] = targeting, ["unblockable"] = false,
                    ["applies"] = attacks[k].DeepClone(),
                });
            a["attacks"] = list;
            combatants.Add(a);
        }
        DataOf(root)["combatants"] = combatants;
        DataOf(root)["commands"] = new JsonArray();
        DataOf(root)["durationMs"] = 30_000;
        return root;
    }

    private static JsonArray Dots() => new(Application("burn", 0.5), Application("bleed", 0.5));

    [Fact]
    public void Lot72_alterations_a_cible_unique_tiennent_dans_les_budgets()
    {
        // Brûlure et saignement, puis étourdissement et gel, en alternance à chaque tick : travail 928 (9 %), file 1376.
        // Correctif 7.2 : l'étourdissement actif refuse les nouvelles applications (stateBlocked active, une ligne de plus par
        // impact, sans échéance en file) et expire toujours, suivi de son immunité.
        var (alternating, _) = Measure("32 assaillants, cible unique, DOT puis contrôles", Load(StatusesAtCap(32, "enemy", Dots(),
            new JsonArray(Application("stun"), Application("freeze")))));
        Assert.Equal(CombatEndReason.DurationReached, alternating.EndReason);
        Assert.Equal((928, 256, 1376), (alternating.Peaks.WorkPerTick, alternating.Peaks.EventsPerTick, alternating.Peaks.PendingEvents));

        // Les quatre à chaque attaque. Correctif 7.2 : l'étourdissement en ignore n'est plus renouvelé ; il expire au bout de
        // 29 ticks et accorde toujours son immunité (avant : renouvelé sans fin, sans immunité). Travail 896 (9 %), file 2624 (3 %).
        var (all, log) = Measure("32 assaillants, cible unique, 2 DOT + gel + étourdissement", Load(StatusesAtCap(32, "enemy",
            new JsonArray(Application("burn", 0.5), Application("bleed", 0.5), Application("freeze"), Application("stun")))));
        Assert.Equal(CombatEndReason.DurationReached, all.EndReason);
        Assert.Equal((896, 256, 2624), (all.Peaks.WorkPerTick, all.Peaks.EventsPerTick, all.Peaks.PendingEvents));
        Assert.Contains(log.Lines, l => l.Contains("\"state\":\"stunImmunity\""));
        Assert.Contains(log.Lines, l => l.Contains("\"state\":\"stun\"") && l.Contains("\"reason\":\"active\""));
    }

    [Fact]
    public void Lot72_dot_de_zone_synchronises_depassent_le_budget_de_lignes()
    {
        // Les DOT appliqués au même tick gardent la même cadence : tous leurs ticks tombent ensemble tous les 40 ticks.
        // 10 assaillants de zone : 970 lignes par tick d'application, + 640 ticks de DOT au tick 40 : arrêt au tick 40.
        var (ten, _) = Measure("10 assaillants de zone, 2 DOT", Load(StatusesAtCap(10, "allEnemies", Dots())));
        Assert.Equal(("eventBudgetExceeded", 40L), (ten.AbortReason, ten.EndTick));
        Assert.Equal((2716, 1000, 650), (ten.Peaks.WorkPerTick, ten.Peaks.EventsPerTick, ten.Peaks.PendingEvents));

        // 5 assaillants : 485 lignes par tick, + 320 ticks de DOT tous les 40 ticks ; au tick 400, 17 personnages meurent
        // ensemble (died, 10 stateRemoved chacun) : arrêt.
        var (five, _) = Measure("5 assaillants de zone, 2 DOT", Load(StatusesAtCap(5, "allEnemies", Dots())));
        Assert.Equal(("eventBudgetExceeded", 400L), (five.AbortReason, five.EndTick));
        Assert.Equal((2708, 992, 325), (five.Peaks.WorkPerTick, five.Peaks.EventsPerTick, five.Peaks.PendingEvents));
    }

    [Fact]
    public void Mesures_deterministes()
    {
        var first = Measure("commandes au plafond, 1", Load(CommandsAtCap()));
        var second = Measure("commandes au plafond, 2", Load(CommandsAtCap()));
        Assert.Equal(first.Log.ToString(), second.Log.ToString());
    }
}
