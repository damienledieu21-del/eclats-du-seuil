using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 8.6 (X10) : compteurs de mesure du résultat du moteur, hors journal. Chaque compteur est recoupé avec le journal ou avec un
/// combat témoin sans défense ; le journal lui-même ne change pas (aucun champ ajouté, moteur 0.14.0).
/// </summary>
[Trait("Category", "Scenario")]
[Trait("Category", "Engine")]
public class Lot86CounterTests
{
    /// <summary>
    /// Entraînement garde et parade figé du sous-lot 8.4 (Bastion sans déclencheur ni modificateur) : un assaillant qui frappe
    /// <paramref name="raw"/> toutes les 2 s à partir de 1 s, éventuellement avec un saignement, et les commandes données.
    /// </summary>
    private static CombatSetup Duel(long durationMs, JsonArray commands, int raw = 100, double? bleed = null, bool shipped = false)
    {
        Action<JsonObject> mutate = root =>
        {
            DataOf(root)["durationMs"] = durationMs;
            DataOf(root)["commands"] = commands;
            var applies = new JsonArray();
            if (bleed is double c) applies.Add(new JsonObject { ["status"] = "bleed", ["coefficient"] = c });
            Combatant(root, 1)["attacks"] = new JsonArray(new JsonObject
            {
                ["id"] = "coup", ["rawDamage"] = raw, ["damageType"] = "physique", ["targeting"] = "enemy", ["unblockable"] = false, ["applies"] = applies,
            });
            if (shipped)
            {
                // Contenu livré, Posture de fer seule (dégâts subis −5 % en garde) : aucun bouclier ni déclencheur.
                Combatant(root, 0)["build"]!["talents"] = new JsonObject();
                Combatant(root, 0)["build"]!["passiveSkills"] = new JsonArray("skill.bastion.posture_de_fer");
            }
        };
        ScenarioLoadResult loaded = shipped ? ReadMutated(mutate, scenario: "entrainement_garde_parade") : ReadFrozenMutated(mutate, scenario: "entrainement_garde_parade");
        Assert.True(loaded.Setup != null, string.Join("\n", loaded.Report.Errors));
        return loaded.Setup!;
    }

    private static JsonObject Cmd(long atMs, string action) => new() { ["atMs"] = atMs, ["actor"] = "bastion", ["action"] = action };

    private static (CombatResult Result, List<string> Lines) Run(CombatSetup setup)
    {
        var log = new MemoryCombatLog();
        CombatResult result = CombatEngine.Run(setup, log);
        return (result, log.Lines.ToList());
    }

    [Fact]
    public void Degats_attenues_par_la_garde_egaux_a_l_ecart_avec_un_combat_sans_garde()
    {
        var (guarded, _) = Run(Duel(8000, new JsonArray(Cmd(0, "guardStart"))));
        var (open, _) = Run(Duel(8000, new JsonArray()));
        CombatantResult g = guarded["bastion"], o = open["bastion"];
        Assert.Equal(4, g.Blocks);
        Assert.True(g.DamageMitigated > 0);
        Assert.Equal(0, o.DamageMitigated);
        Assert.Equal(o.DamageTaken, g.DamageTaken + g.DamageMitigated);
    }

    [Fact]
    public void Parade_parfaite_comptee_entierement_comme_attenuee()
    {
        var (parried, _) = Run(Duel(1500, new JsonArray(Cmd(0, "guardStart"), Cmd(1000, "parry"))));
        var (open, _) = Run(Duel(1500, new JsonArray()));
        CombatantResult p = parried["bastion"], o = open["bastion"];
        Assert.Equal(1, p.PerfectParries);
        Assert.True(o.DamageTaken > 0);
        Assert.Equal(o.DamageTaken, p.DamageTaken + p.DamageMitigated);
    }

    [Fact]
    public void Ticks_de_DOT_attenues_par_les_reductions_de_degats_subis()
    {
        // Posture de fer (−5 % en garde) réduit aussi les ticks de saignement : l'identité tient impacts et DOT compris.
        var (guarded, guardedLog) = Run(Duel(8000, new JsonArray(Cmd(0, "guardStart")), bleed: 0.5, shipped: true));
        var (open, _) = Run(Duel(8000, new JsonArray(), bleed: 0.5, shipped: true));
        Assert.Contains(guardedLog, l => l.Contains("\"event\":\"dotDamage\"", StringComparison.Ordinal));
        CombatantResult g = guarded["bastion"], o = open["bastion"];
        Assert.Equal(0, g.DamageAbsorbed);
        Assert.Equal(o.DamageTaken, g.DamageTaken + g.DamageMitigated);

        // Sans garde ni réduction, rien n'est atténué, DOT compris.
        Assert.Equal(0, o.DamageMitigated);
    }

    [Fact]
    public void Tick_de_mort_et_PV_max_conformes_au_journal()
    {
        var (result, lines) = Run(Duel(60000, new JsonArray(), raw: 400));
        CombatantResult b = result["bastion"];
        Assert.True(b.IsDead);
        string died = Assert.Single(lines, l => l.Contains("\"event\":\"died\"", StringComparison.Ordinal) && l.Contains("\"combatant\":\"bastion\"", StringComparison.Ordinal));
        Assert.Equal(JsonNode.Parse(died)!["tick"]!.GetValue<long>(), b.DeathTick);
        JsonNode setupLine = JsonNode.Parse(lines.First(l => l.Contains("\"event\":\"combatant\"", StringComparison.Ordinal) && l.Contains("\"combatant\":\"bastion\"", StringComparison.Ordinal)))!;
        Assert.Equal(setupLine["maxHealth"]!.GetValue<long>(), b.MaxHealth);
        Assert.Null(result["assaillant"].MaxHealth);
        Assert.Null(result["assaillant"].DeathTick);
    }

    [Fact]
    public void Vivant_a_la_fin_sans_tick_de_mort()
    {
        var (result, _) = Run(Duel(5000, new JsonArray(Cmd(0, "guardStart"))));
        Assert.False(result["bastion"].IsDead);
        Assert.Null(result["bastion"].DeathTick);
    }

    [Fact]
    public void Ticks_gardes_comptes_de_la_prise_de_garde_a_la_fin()
    {
        // Mannequin seul (aucun impact) : garde prise à 1 s, quittée à 3 s, reprise à 4 s jusqu'à la fin (5 s).
        var (result, _) = Run(Duel(5000, new JsonArray(Cmd(1000, "guardStart"), Cmd(3000, "guardStop"), Cmd(4000, "guardStart")), raw: 0));
        Assert.Equal((3000 - 1000) / 50 + (5000 - 4000) / 50, result["bastion"].GuardTicks);
        var (none, _) = Run(Duel(5000, new JsonArray()));
        Assert.Equal(0, none["bastion"].GuardTicks);
    }

    [Fact]
    public void Ultimes_comptees_comme_les_lignes_skillCast_de_l_ultime()
    {
        ScenarioLoadResult loaded = ReadMutated(root =>
        {
            DataOf(root)["durationMs"] = 20000;
            Combatant(root, 0)["build"]!["talents"] = new JsonObject();
            DataOf(root)["commands"] = new JsonArray(
                new JsonObject { ["atMs"] = 0, ["actor"] = "bastion", ["skill"] = "skill.bastion.bastion_absolu" },
                new JsonObject { ["atMs"] = 1000, ["actor"] = "bastion", ["skill"] = "skill.bastion.frappe_du_rempart", ["target"] = "assaillant" });
        }, scenario: "entrainement_garde_parade");
        Assert.True(loaded.Setup != null, string.Join("\n", loaded.Report.Errors));
        var (result, lines) = Run(loaded.Setup!);
        int casts = lines.Count(l => l.Contains("\"event\":\"skillCast\"", StringComparison.Ordinal) && l.Contains("\"skill\":\"skill.bastion.bastion_absolu\"", StringComparison.Ordinal));
        Assert.Equal(casts, result["bastion"].UltimatesCast);
        Assert.True(result["bastion"].SkillsCast > result["bastion"].UltimatesCast);
    }

    [Fact]
    public void Journal_inchange_par_les_compteurs()
    {
        // Aucun champ ajouté : le scénario de référence se rejoue toujours à l'identique contre le journal figé du moteur courant (contenu
        // livré : 0.5.0 depuis le sous-lot 8.7 ; moteur 0.15.0 depuis le sous-lot 11.1).
        string frozen = Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference-0.15.0-contenu-0.5.0", "entrainement_declencheurs.jsonl");
        ScenarioLoadResult loaded = ScenarioJsonReader.ReadFile(ScenarioPath("entrainement_declencheurs"), ShippedData);
        var log = new MemoryCombatLog();
        CombatEngine.Run(loaded.Setup!, log);
        Assert.Equal(File.ReadAllText(frozen), log.ToString());
        Assert.DoesNotContain("damageMitigated", log.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("guardTicks", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Ultime_comptee_meme_a_cout_de_Resonance_nul()
    {
        // Revue Codex de la PR #10 : la nature de la compétence (emplacement d'ultime), pas son coût, fait l'ultime.
        string directory = CopyFrozenData((path, text) => path == "rules.json" ? text.Replace("\"ultimateCost\": 100", "\"ultimateCost\": 0", StringComparison.Ordinal) : text);
        try
        {
            GameData data = GameDataLoader.Load(directory).Data!;
            Assert.Equal(0, data.Rules.Combat.UltimateResonanceCost);
            ScenarioLoadResult loaded = ReadFrozenMutated(root =>
            {
                DataOf(root)["durationMs"] = 2000;
                DataOf(root)["commands"] = new JsonArray(new JsonObject { ["atMs"] = 0, ["actor"] = "bastion", ["skill"] = "skill.bastion.bastion_absolu" });
            }, data, "entrainement_garde_parade");
            Assert.True(loaded.Setup != null, string.Join("\n", loaded.Report.Errors));
            var (result, lines) = Run(loaded.Setup!);
            Assert.Single(lines, l => l.Contains("\"event\":\"skillCast\"", StringComparison.Ordinal) && l.Contains("skill.bastion.bastion_absolu", StringComparison.Ordinal));
            Assert.Equal(1, result["bastion"].UltimatesCast);
        }
        finally { Directory.Delete(directory, true); }
    }
}
