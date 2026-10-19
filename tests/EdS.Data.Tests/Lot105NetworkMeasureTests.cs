using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
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
    [InlineData(-0.01, 0, 0, null, 3.0)]
    [InlineData(double.NaN, 0, 0, null, 3.0)]
    [InlineData(double.PositiveInfinity, 0, 0, null, 3.0)]
    [InlineData(2.0, 0, 0, null, 3.0)]
    [InlineData(0.01, -1, 0, null, 3.0)]
    [InlineData(0.01, 0, -5, null, 3.0)]
    [InlineData(0.01, 0, 0, -1.0, 3.0)]
    [InlineData(0.01, 0, 0, double.NaN, 3.0)]
    [InlineData(0.01, 0, 0, 5.0, -1.0)]
    [InlineData(0.01, 0, 0, 5.0, double.PositiveInfinity)]
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

    [Fact]
    public void Rapport_complet_reproductible()
    {
        IReadOnlyList<NetworkReportRow> rows = NetworkReport.MeasureLinks(ShippedData, Duo, 1);
        IReadOnlyList<NetworkMeasureResult> cuts = NetworkReport.MeasureCuts(ShippedData, Duo);
        Assert.Equal(2 * NetworkReport.Links.Count, rows.Count);
        Assert.All(rows, r => Assert.True(r.HashesMatch && r.Desyncs == 0, $"{r.LatencyMs} ms {r.Timing}"));
        var first = new StringWriter();
        NetworkReport.Write(first, rows, cuts, 1);
        var second = new StringWriter();
        NetworkReport.Write(second, NetworkReport.MeasureLinks(ShippedData, Duo, 1), NetworkReport.MeasureCuts(ShippedData, Duo), 1);
        Assert.Equal(first.ToString(), second.ToString());
        Assert.Contains("| à vue | 0 ms |", first.ToString(), StringComparison.Ordinal);
        Assert.Contains("| anticipée | 200 + 0 à 120 ms |", first.ToString(), StringComparison.Ordinal);
    }
}
