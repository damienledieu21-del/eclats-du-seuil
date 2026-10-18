using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Builds;
using EdS.Data.Json.Measures;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using EdS.Simulation.Pilot;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 8.6 : scénarios de mesure S1 à S5, mesures des builds de référence, objectifs provisoires et commande <c>eds report</c>.
/// Les valeurs mesurées ne sont pas figées ici (le sous-lot 8.7 les fait bouger) : on vérifie les contrats (combats complets, aucun
/// refus, budgets, rejeu, déterminisme du rapport) et le calcul des mesures et des objectifs sur des valeurs connues.
/// </summary>
[Trait("Category", "Scenario")]
public class Lot86MeasureTests
{
    private static string MeasuresDirectory => Path.Combine(AppContext.BaseDirectory, "scenarios", "mesures");

    private static IReadOnlyList<MeasureScenario> Scenarios()
    {
        var failures = new List<(string Path, ScenarioLoadResult Result)>();
        IReadOnlyList<MeasureScenario> scenarios = Measures.LoadScenarios(MeasuresDirectory, ShippedData, failures);
        Assert.Empty(failures);
        return scenarios;
    }

    private static IReadOnlyList<ReferenceBuild> Builds() => ShippedData.BuildSets.Single(b => b.ClassId == "class.bastion").Builds;

    private static readonly Lazy<(IReadOnlyList<MeasureResult> Results, Dictionary<string, MemoryCombatLog> Logs)> All = new(() =>
    {
        var logs = new Dictionary<string, MemoryCombatLog>(StringComparer.Ordinal);
        IReadOnlyList<MeasureResult> results = Measures.RunAll(Scenarios(), Builds(), Measures.Profiles.Values.ToList(), (s, b, p) =>
        {
            var log = new MemoryCombatLog();
            logs[MeasureReport.LogFileName(s, b, p)] = log;
            return log;
        });
        return (results, logs);
    });

    [Fact]
    public void Cinq_scenarios_de_mesure_conformes_a_la_feuille_de_route()
    {
        IReadOnlyList<MeasureScenario> scenarios = Scenarios();
        Assert.Equal(new[] { "S1", "S2", "S3", "S4", "S5" }, scenarios.Select(s => s.Id));
        Assert.Equal(new[] { 60000L, 120000, 60000, 90000, 90000 }, scenarios.Select(s => s.Setup.DurationMs));
        foreach (MeasureScenario s in scenarios)
        {
            CharacterSetup bastion = s.Setup.Combatants.OfType<CharacterSetup>().Single(c => c.Id == Measures.MeasuredId);
            Assert.Equal("class.bastion", bastion.Class.Id);
            Assert.Empty(s.Setup.Commands);
        }
        List<AssailantSetup> Assailants(string id) => scenarios.Single(s => s.Id == id).Setup.Combatants.OfType<AssailantSetup>().ToList();

        // S1 : un mannequin sans armure ; S2 : un assaillant toutes les 1,5 s avec Fragilité périodique ; S3 : trois assaillants, zone et
        // saignement ; S4 : un allié et deux assaillants qui le visent ; S5 : attaques imparables régulières.
        Assert.Empty(Assailants("S1"));
        Assert.Single(scenarios[0].Setup.Combatants.OfType<DummySetup>());
        AssailantSetup duel = Assert.Single(Assailants("S2"));
        Assert.Equal(1500, duel.IntervalMs);
        Assert.Contains(duel.Attacks, a => a.Applies.Any(x => x.Status == "fragility"));
        Assert.Equal(3, Assailants("S3").Count);
        Assert.Contains(Assailants("S3"), a => a.Attacks.Any(x => x.Targeting == SkillTargeting.AllEnemies));
        Assert.Contains(Assailants("S3"), a => a.Attacks.Any(x => x.Applies.Any(y => y.Status == "bleed")));
        Assert.Equal(2, Assailants("S4").Count);
        Assert.All(Assailants("S4"), a => Assert.Equal("allie", a.Target));
        Assert.Contains(scenarios[3].Setup.Combatants.OfType<CharacterSetup>(), c => c.Id == "allie" && c.Team == "joueurs");
        Assert.Contains(Assert.Single(Assailants("S5")).Attacks, a => a.Unblockable);
    }

    [Fact]
    public void Fichiers_hors_motif_ignores()
    {
        string directory = Path.Combine(Path.GetTempPath(), "eds-mesures-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.Copy(Path.Combine(MeasuresDirectory, "s1_mannequin.json"), Path.Combine(directory, "s1_mannequin.json"));
            File.Copy(Path.Combine(MeasuresDirectory, "s1_mannequin.json"), Path.Combine(directory, "sx_brouillon.json"));
            File.Copy(Path.Combine(MeasuresDirectory, "s1_mannequin.json"), Path.Combine(directory, "s2.json"));
            File.WriteAllText(Path.Combine(directory, "s9_casse.json"), "{}");
            var failures = new List<(string Path, ScenarioLoadResult Result)>();
            IReadOnlyList<MeasureScenario> scenarios = Measures.LoadScenarios(directory, ShippedData, failures);
            Assert.Equal("S1", Assert.Single(scenarios).Id);
            Assert.EndsWith("s9_casse.json", Assert.Single(failures).Path, StringComparison.Ordinal);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void Build_remplace_pour_le_seul_personnage_mesure()
    {
        MeasureScenario s4 = Scenarios().Single(s => s.Id == "S4");
        ReferenceBuild gardien = Builds().Single(b => b.Id == Measures.Gardien);
        CombatSetup setup = Measures.WithBuild(s4.Setup, gardien);
        Assert.Same(gardien.Build, setup.Combatants.OfType<CharacterSetup>().Single(c => c.Id == "bastion").Build);
        CharacterSetup ally = setup.Combatants.OfType<CharacterSetup>().Single(c => c.Id == "allie");
        Assert.Same(s4.Setup.Combatants.OfType<CharacterSetup>().Single(c => c.Id == "allie"), ally);
        Assert.Equal(s4.Setup.Seed, setup.Seed);

        CombatSetup noBastion = ReadMutated(root =>
        {
            Combatant(root, 0)["id"] = "autre";
            DataOf(root)["commands"] = new JsonArray();
        }).Setup!;
        Assert.Throws<ArgumentException>(() => Measures.WithBuild(noBastion, gardien));
    }

    [Fact]
    public void Tous_les_combats_mesures_vont_au_bout_sans_refus_sous_la_moitie_des_budgets()
    {
        IReadOnlyList<MeasureResult> results = All.Value.Results;
        Assert.Equal(5 * Builds().Count * 3, results.Count);
        Assert.All(results, r =>
        {
            Assert.False(r.Aborted, $"{r.Scenario} {r.Build} {r.Profile} : {r.EndReason}");
            Assert.Equal(0, r.Rejected);
            Assert.True(r.WorkPeak < 50 && r.EventsPeak < 50 && r.QueuePeak < 50, $"{r.Scenario} {r.Build} {r.Profile}");
            Assert.True(r.HealthLost >= 0 && r.HealthLost <= r.MaxHealth);
            Assert.Equal(r.Mitigated + r.Absorbed + r.Transferred, r.Avoided);
            Assert.True(r.Survival > 0 && r.Survival <= r.DurationMs * 10 / 1000m);
            Assert.Equal(r.Scenario == "S4", r.AllySurvival != null);
        });
        // Le débutant ne pare jamais ; le joueur ne garde pas face au seul mannequin (S1 : aucun dégât subi, aucun bloc).
        Assert.All(results.Where(r => r.Profile == PilotProfile.Beginner), r => Assert.Equal(0, r.PerfectParries));
        Assert.All(results.Where(r => r.Scenario == "S1"), r => { Assert.Equal(0, r.HealthLost); Assert.Equal(0, r.Blocks); Assert.True(r.Dps > 0); });
    }

    [Fact]
    public void Journaux_des_combats_mesures_rejoues_octet_par_octet()
    {
        foreach (var (name, log) in All.Value.Logs)
        {
            ReplayOutcome replay = CombatLogReplayer.Replay(log.Lines.ToList(), ShippedData);
            Assert.True(replay.Status == ReplayStatus.Identical, name + " : " + string.Join("\n", replay.Problems));
        }
    }

    [Fact]
    public void Survie_instant_de_mort_ou_estimation_plafonnee()
    {
        CombatResult result = CombatEngine.Run(ReadFrozenMutated(root =>
        {
            DataOf(root)["durationMs"] = 10000;
            DataOf(root)["commands"] = new JsonArray();
        }, scenario: "entrainement_garde_parade").Setup!, NullCombatLog.Instance);
        CombatantResult b = result["bastion"];
        long lost = b.MaxHealth!.Value - b.Health!.Value;
        Assert.True(lost > 0 && !b.IsDead);
        decimal expected = Math.Min(100m, Math.Round(10m * b.MaxHealth.Value / lost, 1));
        Assert.Equal(expected, Measures.SurvivalSeconds(b, b.MaxHealth.Value, 10000, 50));

        CombatResult untouched = CombatEngine.Run(ReadFrozenMutated(root =>
        {
            DataOf(root)["durationMs"] = 500;
            DataOf(root)["commands"] = new JsonArray();
        }, scenario: "entrainement_garde_parade").Setup!, NullCombatLog.Instance);
        Assert.Equal(5m, Measures.SurvivalSeconds(untouched["bastion"], untouched["bastion"].MaxHealth!.Value, 500, 50));   // plafond : 10 × 0,5 s

        MeasureResult dead = All.Value.Results.First(r => r.Died);
        Assert.True(dead.Survival * 1000 < dead.DurationMs);
    }

    private static MeasureResult R(string scenario, string build, decimal dps = 0, decimal survival = 0, long avoided = 0, decimal? ally = null,
        PilotProfile profile = PilotProfile.Standard, decimal peak = 0) => new()
    {
        Scenario = scenario, Build = build, Profile = profile, Dps = dps, Survival = survival, Avoided = avoided, AllySurvival = ally,
        AllyDied = ally is null ? null : false, WorkPeak = peak, EventsPeak = 0, QueuePeak = 0,
    };

    private static ObjectiveResult Objective(IReadOnlyList<MeasureResult> results, string id) => Measures.Objectives(results).Single(o => o.Id == id);

    [Fact]
    public void Objectifs_aux_frontieres()
    {
        // S1 : écart (max − min) / max ≤ 15 %.
        var s1 = new List<MeasureResult> { R("S1", Measures.Rempart, dps: 100), R("S1", Measures.Contrecoup, dps: 85), R("S1", Measures.Gardien, dps: 90) };
        Assert.True(Objective(s1, "S1.dps").Passed);
        s1[1] = R("S1", Measures.Contrecoup, dps: 84.99m);
        Assert.False(Objective(s1, "S1.dps").Passed);

        // Rapports : ≥ 1,25 (S2 DPS, S2/S5 survie et dégâts évités) ; ≥ 1,5 pour l'allié de S4 contre le meilleur des deux autres.
        var ratios = new List<MeasureResult>
        {
            R("S2", Measures.Rempart, dps: 80, survival: 125, avoided: 1250), R("S2", Measures.Contrecoup, dps: 100, survival: 100, avoided: 1000),
            R("S5", Measures.Rempart, survival: 124.9m, avoided: 1249), R("S5", Measures.Contrecoup, survival: 100, avoided: 1000),
            R("S4", Measures.Gardien, ally: 30), R("S4", Measures.Rempart, ally: 20), R("S4", Measures.Contrecoup, ally: 10),
        };
        Assert.True(Objective(ratios, "S2.dps").Passed);
        Assert.True(Objective(ratios, "S2.survie").Passed);
        Assert.True(Objective(ratios, "S2.evites").Passed);
        Assert.False(Objective(ratios, "S5.survie").Passed);
        Assert.False(Objective(ratios, "S5.evites").Passed);
        Assert.True(Objective(ratios, "S4.allie").Passed);
        ratios[5] = R("S4", Measures.Rempart, ally: 20.01m);
        Assert.False(Objective(ratios, "S4.allie").Passed);

        // Budgets : strictement moins de 50 %, tous profils confondus.
        var budgets = new List<MeasureResult> { R("S1", Measures.Rempart, peak: 49.9m), R("S1", Measures.Rempart, profile: PilotProfile.Expert, peak: 12) };
        Assert.True(Objective(budgets, "budgets").Passed);
        budgets.Add(R("S3", Measures.Gardien, profile: PilotProfile.Beginner, peak: 50));
        Assert.False(Objective(budgets, "budgets").Passed);
        Assert.Contains("S3", Objective(budgets, "budgets").Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Objectifs_non_mesures_et_profil_des_objectifs()
    {
        IReadOnlyList<ObjectiveResult> none = Measures.Objectives(Array.Empty<MeasureResult>());
        Assert.Equal(new[] { "S1.dps", "S2.dps", "S2.survie", "S2.evites", "S5.survie", "S5.evites", "S4.allie", "budgets" }, none.Select(o => o.Id));
        Assert.All(none, o => Assert.Null(o.Passed));

        // Seuls les combats du profil standard comptent pour les objectifs de gameplay.
        var expertOnly = new List<MeasureResult> { R("S2", Measures.Rempart, dps: 1, profile: PilotProfile.Expert), R("S2", Measures.Contrecoup, dps: 100, profile: PilotProfile.Expert) };
        Assert.Null(Objective(expertOnly, "S2.dps").Passed);

        // Dénominateur nul : un numérateur positif passe, nul aussi (rapport 1) pour un seuil ≤ 1, sinon échoue.
        var zero = new List<MeasureResult> { R("S2", Measures.Rempart, dps: 0), R("S2", Measures.Contrecoup, dps: 3) };
        Assert.True(Objective(zero, "S2.dps").Passed);
        zero[1] = R("S2", Measures.Contrecoup, dps: 0);
        Assert.False(Objective(zero, "S2.dps").Passed);
    }

    [Fact]
    public void Rapport_json_deterministe_et_complet()
    {
        IReadOnlyList<MeasureScenario> scenarios = Scenarios();
        IReadOnlyList<ReferenceBuild> builds = Builds();
        IReadOnlyList<MeasureResult> results = All.Value.Results;
        byte[] a = MeasureReport.ToJson(ShippedData, scenarios, builds, results, Measures.Objectives(results));
        IReadOnlyList<MeasureResult> again = Measures.RunAll(scenarios, builds, Measures.Profiles.Values.ToList());
        byte[] b = MeasureReport.ToJson(ShippedData, scenarios, builds, again, Measures.Objectives(again));
        Assert.Equal(a, b);
        JsonNode report = JsonNode.Parse(a)!;
        Assert.Equal("measureReport", report["kind"]!.GetValue<string>());
        Assert.Equal(MeasureReport.FormatVersion, report["formatVersion"]!.GetValue<int>());
        Assert.Equal(CombatEngine.Version, report["engineVersion"]!.GetValue<string>());
        Assert.Equal(ShippedData.Rules.Fingerprint, report["data"]!["rulesFingerprint"]!.GetValue<string>());
        Assert.Equal(results.Count, report["results"]!.AsArray().Count);
        Assert.Equal(8, report["objectives"]!.AsArray().Count);
        Assert.Equal((byte)'\n', a[^1]);
    }

    [Fact]
    public void Rapport_texte_avec_chaque_scenario_et_les_objectifs()
    {
        IReadOnlyList<MeasureResult> results = All.Value.Results;
        var writer = new StringWriter();
        MeasureReport.WriteText(writer, ShippedData, Scenarios(), Builds(), results, Measures.Objectives(results));
        string text = writer.ToString();
        foreach (string s in new[] { "S1 s1_mannequin", "S2 s2_duel", "S3 s3_meute", "S4 s4_protection", "S5 s5_briseur", "Objectifs provisoires (profil standard" })
            Assert.Contains(s, text, StringComparison.Ordinal);
        Assert.Contains($"= {results.Count} combats", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Combat_interrompu_exclu_des_objectifs_de_gameplay_et_echec_des_budgets()
    {
        // Revue Codex de la PR #10 : des mesures partielles ne donnent aucun verdict de gameplay.
        var results = new List<MeasureResult>
        {
            R("S2", Measures.Rempart, dps: 10, survival: 200, avoided: 2000), R("S2", Measures.Contrecoup, dps: 100, survival: 10, avoided: 10),
        };
        Assert.True(Objective(results, "S2.dps").Passed);
        results[1] = new MeasureResult
        {
            Scenario = "S2", Build = Measures.Contrecoup, Profile = PilotProfile.Standard, Dps = 100, Survival = 10, Avoided = 10,
            Aborted = true, EndReason = "workBudgetExceeded",
        };
        Assert.Null(Objective(results, "S2.dps").Passed);
        Assert.Null(Objective(results, "S2.survie").Passed);
        ObjectiveResult budgets = Objective(results, "budgets");
        Assert.False(budgets.Passed);
        Assert.Contains("workBudgetExceeded", budgets.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Numero_de_scenario_en_double_refuse()
    {
        string directory = Path.Combine(Path.GetTempPath(), "eds-mesures-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.Copy(Path.Combine(MeasuresDirectory, "s1_mannequin.json"), Path.Combine(directory, "s1_a.json"));
            File.Copy(Path.Combine(MeasuresDirectory, "s1_mannequin.json"), Path.Combine(directory, "s1_b.json"));
            var failures = new List<(string Path, ScenarioLoadResult Result)>();
            var e = Assert.Throws<ArgumentException>(() => Measures.LoadScenarios(directory, ShippedData, failures));
            Assert.Equal("Numéro de scénario de mesure en double : S1 (s1_a.json et s1_b.json).", e.Message);
            var cli = Lot71Files.Cli(AppContext.BaseDirectory, "report", directory, "--data", DataDirectory);
            Assert.Equal(1, cli.ExitCode);
            Assert.Contains("en double : S1", cli.Error, StringComparison.Ordinal);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void Table_des_profils_figee()
    {
        Assert.IsNotType<Dictionary<string, PilotProfile>>(Measures.Profiles);
        var asCollection = (ICollection<KeyValuePair<string, PilotProfile>>)Measures.Profiles;
        Assert.True(asCollection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => asCollection.Clear());
        Assert.Equal(new[] { "beginner", "standard", "expert" }, Measures.Profiles.Keys);
    }

    [Fact]
    public void Rapport_texte_aux_fins_de_ligne_LF_quelle_que_soit_la_plateforme()
    {
        IReadOnlyList<MeasureResult> results = All.Value.Results;
        var writer = new StringWriter { NewLine = "\r\n" };
        MeasureReport.WriteText(writer, ShippedData, Scenarios(), Builds(), results, Measures.Objectives(results));
        string text = writer.ToString();
        Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
        Assert.EndsWith("\n", text, StringComparison.Ordinal);
    }
}

/// <summary>Sous-lot 8.6 : la commande <c>eds report</c>, lancée telle quelle (processus séparé).</summary>
[Trait("Category", "Scenario")]
public class Lot86ReportCliTests
{
    private static string MeasuresDirectory => Path.Combine(AppContext.BaseDirectory, "scenarios", "mesures");

    private static (int ExitCode, string Output, string Error) Report(params string[] args) =>
        Lot71Files.Cli(AppContext.BaseDirectory, new[] { "report", MeasuresDirectory, "--data", DataDirectory }.Concat(args).ToArray());

    [Fact]
    public void Rapport_json_et_code_de_sortie_strict_selon_les_objectifs()
    {
        var json = Report("--json");
        Assert.True(json.ExitCode == 0, json.Error);
        JsonNode report = JsonNode.Parse(json.Output)!;
        Assert.Equal(5 * 5 * 3, report["results"]!.AsArray().Count);
        bool failed = report["objectives"]!.AsArray().Any(o => o!["passed"]?.GetValue<bool>() == false);

        var strict = Report("--json", "--strict");
        Assert.Equal(failed ? 3 : 0, strict.ExitCode);
        Assert.Equal(json.Output, strict.Output);
    }

    [Fact]
    public void Selection_de_builds_et_de_profils_et_rapport_texte()
    {
        var text = Report("--build", Measures.Rempart, "--build", Measures.Contrecoup, "--profile", "standard", "--profile", "standard");
        Assert.True(text.ExitCode == 0, text.Error);
        Assert.Contains("5 scénarios × 2 builds × 1 profils = 10 combats", text.Output, StringComparison.Ordinal);
        Assert.Contains("[—]", text.Output, StringComparison.Ordinal);   // objectifs du Gardien non mesurés
        Assert.DoesNotContain("build.bastion.gardien", text.Output.Split("Objectifs")[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Journaux_ecrits_et_rejouables()
    {
        string logs = Path.Combine(Path.GetTempPath(), "eds-journaux-" + Guid.NewGuid().ToString("N"));
        try
        {
            var run = Report("--build", Measures.Gardien, "--profile", "expert", "--logs", logs);
            Assert.True(run.ExitCode == 0, run.Error);
            string[] files = Directory.GetFiles(logs).Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal).ToArray()!;
            Assert.Equal(new[]
            {
                "s1_mannequin__build.bastion.gardien__expert.jsonl", "s2_duel__build.bastion.gardien__expert.jsonl", "s3_meute__build.bastion.gardien__expert.jsonl",
                "s4_protection__build.bastion.gardien__expert.jsonl", "s5_briseur__build.bastion.gardien__expert.jsonl",
            }, files);
            var replay = Lot71Files.Cli(AppContext.BaseDirectory, "replay", Path.Combine(logs, files[3]), "--data", DataDirectory);
            Assert.True(replay.ExitCode == 0, replay.Error);
            Assert.Contains("rejeu identique", replay.Output, StringComparison.Ordinal);
        }
        finally { if (Directory.Exists(logs)) Directory.Delete(logs, true); }
    }

    [Theory]
    [InlineData("--profile", "heroique", "Profil inconnu")]
    [InlineData("--build", "build.bastion.inconnu", "Build de référence inconnu")]
    [InlineData("--option", "x", "Option inconnue")]
    public void Options_invalides_refusees(string option, string value, string message)
    {
        var run = Report(option, value);
        Assert.Equal(1, run.ExitCode);
        Assert.Contains(message, run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Dossier_absent_ou_vide_refuse()
    {
        var missing = Lot71Files.Cli(AppContext.BaseDirectory, "report", Path.Combine(MeasuresDirectory, "absent"), "--data", DataDirectory);
        Assert.Equal(1, missing.ExitCode);
        Assert.Contains("introuvable", missing.Error, StringComparison.Ordinal);
        string empty = Path.Combine(Path.GetTempPath(), "eds-vide-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            var none = Lot71Files.Cli(AppContext.BaseDirectory, "report", empty, "--data", DataDirectory);
            Assert.Equal(1, none.ExitCode);
            Assert.Contains("aucun scénario de mesure", none.Error, StringComparison.Ordinal);
        }
        finally { Directory.Delete(empty, true); }
    }
}
