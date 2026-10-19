using System.Globalization;
using EdS.Data;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;

namespace EdS.Tools.Network;

/// <summary>Une ligne du rapport : un réglage de réseau, mesuré sur plusieurs graines.</summary>
public sealed class NetworkReportRow
{
    public int LatencyMs { get; init; }
    public int JitterMs { get; init; }
    public ParryTiming Timing { get; init; }
    public IReadOnlyList<NetworkMeasureResult> Runs { get; init; } = Array.Empty<NetworkMeasureResult>();

    public int Presses => Runs.Sum(r => r.ParryPresses);
    public long Perfect => Runs.Sum(r => r.PerfectParries);
    public long Failed => Runs.Sum(r => r.FailedParries);
    public long Rejected => Runs.Sum(r => r.CommandsRejected);
    public double Success => Presses == 0 ? 0 : (double)Perfect / Presses;
    // Revue Codex de la PR #22 : moyenne et 95e centile sur tous les délais de la ligne réunis (toutes graines), pas par graine.
    private IReadOnlyList<double> AllDelays => Runs.SelectMany(r => r.ActionDelaysMs).OrderBy(d => d).ToList();
    public double ActionDelayMs => AllDelays.Count == 0 ? 0 : AllDelays.Average();
    public double ActionDelayP95Ms => NetworkMeasures.Percentile95(AllDelays);
    public double DisplayLagMs => Runs.Count == 0 ? 0 : Runs.Average(r => r.DisplayLagMeanMs);
    public double RttMs => Runs.Count == 0 ? 0 : Runs.Average(r => r.RttMs);
    public int Desyncs => Runs.Sum(r => r.Desyncs);
    public bool HashesMatch => Runs.All(r => r.HashesMatch);
    public int Victories => Runs.Count(r => r.EndReason == nameof(CombatEndReason.TeamDefeated) && r.Winner == "joueurs");
}

/// <summary>
/// Sous-lot 10.5 : rapport des mesures du réseau (commande <c>eds reseau</c>) : réussite des parades selon la latence et le moment de
/// parade, délais, désynchronisations, coupures. Texte Markdown, reproductible : mêmes réglages, même rapport.
/// </summary>
public static class NetworkReport
{
    /// <summary>Latence d'un sens et gigue mesurées (ms) : de 0 à 200 ms, puis deux gigues fortes.</summary>
    public static readonly IReadOnlyList<(int Latency, int Jitter)> Links = Array.AsReadOnly(new[]
    {
        (0, 0), (25, 5), (50, 10), (75, 15), (100, 20), (150, 30), (200, 40), (100, 60), (200, 120),
    });   // revue Codex de la PR #22 : listes publiées immuables (pas de tableau modifiable derrière)

    /// <summary>Coupures du premier joueur (latence 50 + 0 à 10 ms, parade anticipée), à 5 s de combat : durée avant la nouvelle connexion.</summary>
    public static readonly IReadOnlyList<double> Cuts = Array.AsReadOnly(new[] { 1.0, 3.0, 10.0, 25.0, 35.0 });

    public static IReadOnlyList<NetworkReportRow> MeasureLinks(GameData data, CombatSetup scenario, int seeds)
    {
        var rows = new List<NetworkReportRow>();
        foreach (ParryTiming timing in new[] { ParryTiming.OnSight, ParryTiming.Anticipated })
            foreach ((int latency, int jitter) in Links)
                rows.Add(new NetworkReportRow
                {
                    LatencyMs = latency,
                    JitterMs = jitter,
                    Timing = timing,
                    Runs = Enumerable.Range(1, seeds).Select(seed => NetworkMeasures.Run(data, scenario,
                        new NetworkMeasureOptions { LatencyMs = latency, JitterMs = jitter, Seed = (ulong)seed, Timing = timing })).ToList(),
                });
        return rows;
    }

    public static IReadOnlyList<NetworkMeasureResult> MeasureCuts(GameData data, CombatSetup scenario) =>
        Cuts.Select(seconds => NetworkMeasures.Run(data, scenario,
            new NetworkMeasureOptions { LatencyMs = 50, JitterMs = 10, Seed = 1, Timing = ParryTiming.Anticipated, CutAtSeconds = 5, CutSeconds = seconds })).ToList();

    /// <param name="scenario">Nom du scénario mesuré (affiché).</param>
    /// <param name="durationMs">Durée de ses combats (affichée).</param>
    public static void Write(TextWriter w, IReadOnlyList<NetworkReportRow> rows, IReadOnlyList<NetworkMeasureResult> cuts, int seeds, string scenario,
        long durationMs)
    {
        ArgumentNullException.ThrowIfNull(w);
        // Virgule décimale à la française, sans dépendre des cultures installées (mode de globalisation invariant).
        string F(double value, int decimals = 0) =>
            value.ToString(decimals == 0 ? "0" : "0." + new string('0', decimals), CultureInfo.InvariantCulture).Replace('.', ',');
        // Revue Codex de la PR #22 : scénario et durée réellement mesurés, pas ceux du scénario par défaut.
        w.WriteLine($"Parades (deux joueurs scriptés, {seeds} graine(s) par réglage, scénario {scenario}, {F(durationMs / 1000.0, durationMs % 1000 == 0 ? 0 : 1)} s) :");
        w.WriteLine();
        w.WriteLine("| Parade | Latence (un sens) | Pressées | Parfaites | Réussite | Ratées | Refusées | Délai d'action moyen | p95 | Retard d'affichage | Aller-retour mesuré | Désync. | Empreintes |");
        w.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|");
        foreach (NetworkReportRow row in rows)
        {
            string timing = row.Timing == ParryTiming.OnSight ? "à vue" : "anticipée";
            string link = row.JitterMs == 0 ? $"{row.LatencyMs} ms" : $"{row.LatencyMs} + 0 à {row.JitterMs} ms";
            w.WriteLine($"| {timing} | {link} | {row.Presses} | {row.Perfect} | {F(100 * row.Success, 1)} % | {row.Failed} | {row.Rejected} | " +
                $"{F(row.ActionDelayMs)} ms | {F(row.ActionDelayP95Ms)} ms | {F(row.DisplayLagMs)} ms | {F(row.RttMs)} ms | {row.Desyncs} | {(row.HashesMatch ? "égales" : "**différentes**")} |");
        }
        w.WriteLine();
        w.WriteLine("Coupures du premier joueur à 5 s de combat (latence 50 + 0 à 10 ms, parade anticipée ; le serveur garde la place 30 s) :");
        w.WriteLine();
        w.WriteLine("| Coupure | Retour | Reprise après la nouvelle connexion | Resynchronisations | Désync. | Fin du combat | Empreintes |");
        w.WriteLine("|---:|---|---:|---:|---:|---|---|");
        foreach (NetworkMeasureResult cut in cuts)
        {
            string back = cut.Reconnected == true ? "réussi" : "refusé : " + (cut.ReconnectError ?? "aucun");
            string delay = cut.ReconnectDelayMs is double d ? $"{F(d)} ms" : "—";
            string end = $"{cut.EndReason}, {F(cut.EndTick * (double)cut.TickMs / 1000, 1)} s";   // durée du tick mesurée (PR #22)
            w.WriteLine($"| {F(cut.Options.CutSeconds)} s | {back} | {delay} | {cut.Resyncs} | {cut.Desyncs} | {end} | {(cut.HashesMatch ? "égales" : "**différentes**")} |");
        }
    }
}
