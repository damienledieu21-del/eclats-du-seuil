using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using EdS.Tools.Network;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 10.5 : mesures du jeu en réseau sur le réseau simulé (commande <c>eds reseau</c>) : reproductibles, sans divergence, et
/// conformes à ce que le rapport affirme (la parade à vue tombe avec la latence, la parade anticipée du ping tient ; une coupure se
/// répare dans les 30 s, pas au-delà).
/// </summary>
[Trait("Category", "Network")]
public class Lot105NetworkMeasureTests
{
    private static CombatSetup Duo => ScenarioJsonReader.ReadFile(ScenarioPath("coop/duo_meute"), ShippedData).Setup!;

    /// <summary>Les deux Bastions face à un mannequin qui ne frappe pas : le combat dure tout son temps.</summary>
    private static CombatSetup Calm(long durationMs)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(ScenarioPath("coop/duo_meute")))!.AsObject();
        root["data"]!["durationMs"] = durationMs;
        var combatants = root["data"]!["combatants"]!.AsArray();
        var players = combatants.Where(c => (string?)c!["type"] == "character").Select(c => c!.DeepClone()).ToList();
        combatants.Clear();
        foreach (var player in players) combatants.Add(player);
        combatants.Add(System.Text.Json.Nodes.JsonNode.Parse("{\"id\":\"mannequin\",\"type\":\"dummy\",\"team\":\"cibles\",\"initiative\":0,\"armor\":0,\"magicResistance\":0}"));
        ScenarioLoadResult result = ScenarioJsonReader.Read(root.ToJsonString(), ShippedData);
        Assert.True(result.Setup != null, result.Report.ToString());
        return result.Setup!;
    }

    private static NetworkMeasureResult Run(int latency, int jitter, ParryTiming timing, ulong seed = 1, double? cutAt = null, double cutSeconds = 3) =>
        NetworkMeasures.Run(ShippedData, Duo, new NetworkMeasureOptions
        {
            LatencyMs = latency,
            JitterMs = jitter,
            Timing = timing,
            Seed = seed,
            CutAtSeconds = cutAt,
            CutSeconds = cutSeconds,
        });

    [Fact]
    public void Memes_reglages_memes_mesures()
    {
        NetworkMeasureResult a = Run(100, 40, ParryTiming.Anticipated, seed: 2), b = Run(100, 40, ParryTiming.Anticipated, seed: 2);
        Assert.Equal((a.ParryPresses, a.PerfectParries, a.FailedParries, a.CommandsRejected, a.ActionDelayMeanMs, a.DisplayLagMeanMs, a.RttMs, a.EndTick),
            (b.ParryPresses, b.PerfectParries, b.FailedParries, b.CommandsRejected, b.ActionDelayMeanMs, b.DisplayLagMeanMs, b.RttMs, b.EndTick));
        // Une autre graine tire d'autres délais.
        NetworkMeasureResult c = Run(100, 40, ParryTiming.Anticipated, seed: 3);
        Assert.NotEqual(a.ActionDelayMeanMs, c.ActionDelayMeanMs);
    }

    [Fact]
    public void Parade_a_vue_tombe_avec_la_latence_parade_anticipee_tient()
    {
        NetworkMeasureResult perfect = Run(0, 0, ParryTiming.OnSight);
        NetworkMeasureResult late = Run(100, 20, ParryTiming.OnSight);
        NetworkMeasureResult anticipated = Run(100, 20, ParryTiming.Anticipated);
        foreach (NetworkMeasureResult r in new[] { perfect, late, anticipated })
        {
            Assert.True(r.HashesMatch);
            Assert.Equal(0, r.Desyncs);
            Assert.True(r.ParryPresses > 50, $"{r.ParryPresses} parades pressées");
        }
        Assert.True(perfect.ParrySuccess > 0.7, $"réseau parfait : {perfect.ParrySuccess:P1}");
        Assert.True(late.ParrySuccess < 0.1, $"100 ms à vue : {late.ParrySuccess:P1}");
        Assert.True(anticipated.ParrySuccess > 0.7, $"100 ms anticipée : {anticipated.ParrySuccess:P1}");
        // Les délais suivent la latence : affichage en retard d'environ une latence, action appliquée après la latence et le tick.
        Assert.InRange(late.DisplayLagMeanMs, 100, 140);
        Assert.InRange(late.ActionDelayMeanMs, 100, 175);
        Assert.InRange(late.RttMs, 200, 260);
        Assert.InRange(perfect.ActionDelayMeanMs, 0, 60);
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(25, true)]
    [InlineData(35, false)]
    public void Coupure_reparee_dans_les_30_secondes(double cutSeconds, bool back)
    {
        NetworkMeasureResult r = Run(50, 10, ParryTiming.Anticipated, cutAt: 5, cutSeconds: cutSeconds);
        Assert.True(r.EndTick * 0.05 > 5 + cutSeconds, $"combat fini à {r.EndTick * 0.05} s, avant la nouvelle connexion");
        Assert.Equal(back, r.Reconnected);
        Assert.True(r.HashesMatch);
        Assert.Equal(0, r.Desyncs);
        if (back)
        {
            Assert.Equal(1, r.Resyncs);
            Assert.InRange(r.ReconnectDelayMs!.Value, 50, 500);
            Assert.Null(r.ReconnectError);
        }
        else Assert.Equal("refusé par le serveur : jeton inconnu ou expiré.", r.ReconnectError);
    }

    [Theory]
    [InlineData(0.0, 0, 0, null, 3.0)]
    [InlineData(1e-9, 0, 0, null, 3.0)]
    [InlineData(0.000999, 0, 0, null, 3.0)]
    [InlineData(-0.01, 0, 0, null, 3.0)]
    [InlineData(double.NaN, 0, 0, null, 3.0)]
    [InlineData(double.PositiveInfinity, 0, 0, null, 3.0)]
    [InlineData(2.0, 0, 0, null, 3.0)]
    [InlineData(1.0, 0, 0, null, 3.0)]
    [InlineData(0.251, 0, 0, null, 3.0)]
    [InlineData(0.01, -1, 0, null, 3.0)]
    [InlineData(0.01, 0, -5, null, 3.0)]
    [InlineData(0.01, 0, 0, -1.0, 3.0)]
    [InlineData(0.01, 0, 0, double.NaN, 3.0)]
    [InlineData(0.01, 0, 0, 60.0, 3.0)]
    [InlineData(0.01, 0, 0, 75.0, 3.0)]
    [InlineData(0.01, 0, 0, 5.0, -1.0)]
    [InlineData(0.01, 0, 0, 5.0, double.PositiveInfinity)]
    [InlineData(0.01, 0, 0, 5.0, 120.001)]
    [InlineData(0.01, 5_001, 0, null, 3.0)]
    [InlineData(0.01, 0, 5_001, null, 3.0)]
    [InlineData(0.01, 268_435_456, 0, null, 3.0)]
    public void Reglages_invalides_refuses_avant_la_simulation(double step, int latency, int jitter, double? cutAt, double cutSeconds)
    {
        // Revue Codex de la PR #22 : un pas nul faisait tourner la boucle sans fin.
        Assert.Throws<ArgumentOutOfRangeException>(() => NetworkMeasures.Run(ShippedData, Duo, new NetworkMeasureOptions
        {
            StepSeconds = step,
            LatencyMs = latency,
            JitterMs = jitter,
            CutAtSeconds = cutAt,
            CutSeconds = cutSeconds,
        }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    public void Nombre_de_graines_hors_bornes_refuse(int seeds)
    {
        // Revue Codex de la PR #22 (tour 4) : zéro graine ne mesurait rien mais annonçait des empreintes égales.
        Assert.Throws<ArgumentOutOfRangeException>(() => NetworkReport.MeasureLinks(ShippedData, Duo, seeds));
        Assert.Throws<ArgumentOutOfRangeException>(() => NetworkReport.Write(new StringWriter(), Array.Empty<NetworkReportRow>(),
            Array.Empty<NetworkMeasureResult>(), seeds, "duo_meute", Duo.DurationMs));
        Assert.False(new NetworkReportRow().HashesMatch);   // une ligne sans partie n'affirme rien
    }

    [Fact]
    public void Pas_minimal_accepte_et_boucle_finie()
    {
        // Le plus petit pas accepté (1 ms) et le plus grand (5 ticks de 50 ms, ce que la salle simule par appel, revue Codex de la
        // PR #22, tour 5) mesurent un combat complet, en un nombre d'images fini.
        long reference = NetworkMeasures.Run(ShippedData, Calm(2_000), new NetworkMeasureOptions()).EndTick;
        foreach (double step in new[] { NetworkMeasures.MinStepSeconds, 0.25 })
        {
            NetworkMeasureResult r = NetworkMeasures.Run(ShippedData, Calm(2_000), new NetworkMeasureOptions { StepSeconds = step });
            Assert.True(r.HashesMatch, $"pas de {step} s");
            Assert.Equal(0, r.Desyncs);
            Assert.Equal(nameof(CombatEndReason.DurationReached), r.EndReason);
            Assert.Equal(reference, r.EndTick);   // même fin qu'avec le pas de 10 ms : tout le combat est joué
            // Revue Codex des PR #22 (tour 7) et #23 (tour 2) : l'instant de chaque tick simulé, de 0 à EndTick inclus, est relevé, ceux
            // de la dernière image et le tick final compris.
            Assert.Equal(r.EndTick + 1, r.TimedTicks);
        }
    }

    [Fact]
    public void Latence_extreme_combat_mesure_en_entier()
    {
        // Revue Codex de la PR #22 (tour 6) : le budget du combat part de son début (l'entrée dans la salle a son propre budget, borné
        // d'après la latence) ; une latence de plusieurs secondes est mesurée en entier.
        NetworkMeasureResult r = NetworkMeasures.Run(ShippedData, Calm(3_000), new NetworkMeasureOptions { LatencyMs = 4_000, JitterMs = 500 });
        Assert.Equal(nameof(CombatEndReason.DurationReached), r.EndReason);
        Assert.True(r.HashesMatch);
        Assert.Equal(0, r.Desyncs);
        Assert.InRange(r.RttMs, 8_000, 9_100);
        // Au-delà de ce que la salle attend pour une présentation (5 s), la mesure échoue avec la raison, jamais un résultat partiel, et
        // sans attendre la fin du budget (tour 7). Au-delà de 5 s d'un sens, le réglage est refusé d'emblée.
        var error = Assert.Throws<InvalidOperationException>(() =>
            NetworkMeasures.Run(ShippedData, Calm(3_000), new NetworkMeasureOptions { LatencyMs = 4_000, JitterMs = 5_000 }));
        Assert.StartsWith("Le combat n'a pas commencé", error.Message);
        Assert.Contains("présentation attendue", error.Message);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NetworkMeasures.Run(ShippedData, Calm(3_000), new NetworkMeasureOptions { LatencyMs = 11_000 }));
    }

    [Fact]
    public void Coupure_qui_depasse_la_fin_du_combat_mesure_complete()
    {
        // Revue Codex de la PR #22 (tour 6) : coupé à 1 s d'un combat de 3 s, le joueur revient à 6 s ; la mesure attend son retour et dit
        // pourquoi il n'a pas repris le combat, au lieu de s'arrêter sur un résultat partiel.
        NetworkMeasureResult r = NetworkMeasures.Run(ShippedData, Calm(3_000), new NetworkMeasureOptions { CutAtSeconds = 1, CutSeconds = 5 });
        Assert.Equal(nameof(CombatEndReason.DurationReached), r.EndReason);
        Assert.False(r.Reconnected);
        Assert.NotNull(r.ReconnectError);
        Assert.True(r.HashesMatch);   // l'autre joueur a vu tout le combat
    }

    [Fact]
    public void Coupures_d_un_scenario_court_pendant_le_combat()
    {
        // Revue Codex de la PR #22 (tour 7) : un scénario de 4 s est coupé à 2 s (la moitié), pas à 5 s ; toutes les coupures sont
        // mesurées, y compris celles qui dépassent la fin du combat.
        CombatSetup shortScenario = Calm(4_000);
        IReadOnlyList<NetworkMeasureResult> cuts = NetworkReport.MeasureCuts(ShippedData, shortScenario);
        Assert.Equal(NetworkReport.Cuts.Count, cuts.Count);
        Assert.All(cuts, c => Assert.Equal(2.0, c.Options.CutAtSeconds));
        Assert.True(cuts[0].Reconnected);   // 1 s de coupure : retour avant la fin
        Assert.All(cuts.Skip(1), c => Assert.NotNull(c.ReconnectError));
        var report = new StringWriter();
        NetworkReport.Write(report, Array.Empty<NetworkReportRow>(), cuts, 1, "court", shortScenario.DurationMs);
        Assert.Contains("Coupures du premier joueur à 2 s de combat", report.ToString(), StringComparison.Ordinal);
        Assert.Equal(5.0, NetworkReport.CutAt(Duo.DurationMs));
    }

    [Fact]
    public void Moment_de_parade_inconnu_et_scenario_incompatible_refuses()
    {
        // Revue Codex de la PR #22.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NetworkMeasures.Run(ShippedData, Duo, new NetworkMeasureOptions { Timing = (ParryTiming)2 }));
        CombatSetup alone = ScenarioJsonReader.ReadFile(ScenarioPath("mannequin"), ShippedData).Setup!;
        Assert.Throws<ArgumentException>(() => NetworkMeasures.Run(ShippedData, alone, new NetworkMeasureOptions()));
    }

    [Fact]
    public void Compteurs_des_places_reellement_attribuees()
    {
        // Revue Codex de la PR #22 : avec s4_protection, la seconde place s'appelle « allie » ; ses parades comptent aussi.
        CombatSetup protection = ScenarioJsonReader.ReadFile(ScenarioPath("mesures/s4_protection"), ShippedData).Setup!;
        NetworkMeasureResult r = NetworkMeasures.Run(ShippedData, protection, new NetworkMeasureOptions { Timing = ParryTiming.Anticipated });
        Assert.Equal(new[] { "bastion", "allie" }, r.Seats);
        Assert.True(r.HashesMatch);
        Assert.Equal(0, r.Desyncs);
        // Les pressions des deux joueurs finissent presque toutes parfaites, ratées ou refusées chez les places mesurées ; sans la place
        // « allie » dans les compteurs, la moitié manquerait.
        long outcomes = r.PerfectParries + r.FailedParries + r.CommandsRejected;
        Assert.True(r.PerfectParries <= r.ParryPresses && outcomes >= 0.75 * r.ParryPresses,
            $"{r.PerfectParries} parfaites, {r.FailedParries} ratées, {r.CommandsRejected} refusées, {r.ParryPresses} pressées");
        var report = new StringWriter();
        NetworkReport.Write(report, Array.Empty<NetworkReportRow>(), Array.Empty<NetworkMeasureResult>(), 1, "s4_protection", protection.DurationMs);
        Assert.Contains("scénario s4_protection, 90 s", report.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Ligne_du_rapport_agregee_sur_toutes_les_graines_listes_figees_tick_mesure()
    {
        // Revue Codex de la PR #22 : p95 et moyenne sur tous les délais réunis ; listes de réglages immuables ; durée de tick mesurée.
        NetworkMeasureResult Fake(params double[] delays) => new() { ActionDelaysMs = delays.OrderBy(d => d).ToArray(), TickMs = 50 };
        var row = new NetworkReportRow
        {
            Runs = new[] { Fake(Enumerable.Repeat(10.0, 19).Append(500).ToArray()), Fake(Enumerable.Repeat(10.0, 20).ToArray()), Fake(Enumerable.Repeat(20.0, 20).ToArray()) },
        };
        // 60 délais : le 95e centile est le 57e (20 ms) ; le plus grand p95 par graine aurait donné 500 ms.
        Assert.Equal(20, row.ActionDelayP95Ms);
        Assert.Equal((19 * 10 + 500 + 20 * 10 + 20 * 20) / 60.0, row.ActionDelayMs, 6);
        Assert.Throws<NotSupportedException>(() => ((IList<(int, int)>)NetworkReport.Links)[0] = (999, 0));
        Assert.Throws<NotSupportedException>(() => ((IList<double>)NetworkReport.Cuts)[0] = 999);
        Assert.Equal(50, NetworkMeasures.Run(ShippedData, Duo, new NetworkMeasureOptions()).TickMs);
    }

    [Fact]
    public void Rapport_complet_reproductible()
    {
        IReadOnlyList<NetworkReportRow> rows = NetworkReport.MeasureLinks(ShippedData, Duo, 1);
        IReadOnlyList<NetworkMeasureResult> cuts = NetworkReport.MeasureCuts(ShippedData, Duo);
        Assert.Equal(2 * NetworkReport.Links.Count, rows.Count);
        Assert.All(rows, r => Assert.True(r.HashesMatch && r.Desyncs == 0, $"{r.LatencyMs} ms {r.Timing}"));
        var first = new StringWriter();
        NetworkReport.Write(first, rows, cuts, 1, "duo_meute", Duo.DurationMs);
        var second = new StringWriter();
        NetworkReport.Write(second, NetworkReport.MeasureLinks(ShippedData, Duo, 1), NetworkReport.MeasureCuts(ShippedData, Duo), 1, "duo_meute", Duo.DurationMs);
        Assert.Equal(first.ToString(), second.ToString());
        Assert.Contains("| à vue | 0 ms |", first.ToString(), StringComparison.Ordinal);
        Assert.Contains("scénario duo_meute, 60 s", first.ToString(), StringComparison.Ordinal);
        Assert.Contains("| anticipée | 200 + 0 à 120 ms |", first.ToString(), StringComparison.Ordinal);
    }
}
