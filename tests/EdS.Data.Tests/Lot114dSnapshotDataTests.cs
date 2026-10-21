using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 11.4d : instantané d'état sur tous les scénarios livrés (déclencheurs et réactions reportées, DOT, boucliers, protections,
/// provocations, garde et parade, combats positionnés, attaques annoncées, arrivées et départs). À chaque tick, l'instantané se relit
/// (contrôles de cohérence de l'état relu, puis réécriture à l'identique, dans <see cref="CombatEngine.Restore"/>) ; à cinq ticks
/// répartis et à chaque tick où une réaction reportée attend dans la file, la copie relue avance avec l'original jusqu'à la fin : mêmes
/// instantanés à chaque tick, mêmes lignes de journal, même résultat.
/// </summary>
[Trait("Category", "Presence")]
[Trait("Category", "Data")]
public class Lot114dSnapshotDataTests
{
    public static IEnumerable<object[]> Scenarios() =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "scenarios"), "*.json", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(Path.Combine(AppContext.BaseDirectory, "scenarios"), p).Replace('\\', '/'))
            .OrderBy(p => p, StringComparer.Ordinal).Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Instantanes_d_un_scenario_livre_relus_et_continues_a_l_identique(string file)
    {
        ScenarioLoadResult loaded = ScenarioJsonReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "scenarios", file), ShippedData);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        long ticks = loaded.Setup!.DurationMs / loaded.Setup.Combat.TickMilliseconds;
        var resumeAt = new HashSet<long>(Enumerable.Range(0, 5).Select(k => ticks * k / 5 + 3));
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(loaded.Setup, log);
        var copies = new List<(CombatEngine Copy, MemoryCombatLog Log, int From)>();
        int checkedSnapshots = 0;
        while (!engine.IsFinished)
        {
            long tick = engine.CurrentTick;
            string text = engine.Snapshot().Text;
            // Une réaction reportée (entrainement_declencheurs) est dans la file entre deux ticks : la copie continue aussi depuis là.
            bool resume = resumeAt.Contains(tick) || text.Contains("\"item\":\"reaction\"", StringComparison.Ordinal);
            var restoredLog = new MemoryCombatLog();
            CombatEngine restored = CombatEngine.Restore(engine.CompletedSetup(), CombatSnapshot.Parse(text), restoredLog);
            checkedSnapshots++;
            if (resume) copies.Add((restored, restoredLog, log.Lines.Count));
            engine.Step();
            foreach (var (copy, _, _) in copies)
            {
                copy.Step();
                if (!engine.IsFinished) Assert.Equal(engine.Snapshot().Text, copy.Snapshot().Text);
                Assert.Equal(engine.IsFinished, copy.IsFinished);
            }
        }
        Assert.True(checkedSnapshots > 3);
        foreach (var (copy, resumed, from) in copies)
        {
            Assert.Equal(log.Lines.Skip(from), resumed.Lines);
            Assert.Equal((engine.Result!.EndReason, engine.Result.EndTick), (copy.Result!.EndReason, copy.Result.EndTick));
        }
    }

    [Fact]
    public void Bouclier_epuise_ou_deja_vide_refuse()
    {
        // Revue Codex de la PR #32 (11e tour) : un bouclier épuisé est retiré en fin de tick ; entre deux ticks, un bouclier actif absorbe
        // encore (absorb > 0) et ne garde pas l'effet qui l'aurait épuisé (depletedBy). Scénario entrainement_declencheurs (Barrière d'acier).
        ScenarioLoadResult loaded = ScenarioJsonReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "scenarios", "entrainement_declencheurs.json"), ShippedData);
        var engine = new CombatEngine(loaded.Setup!, NullCombatLog.Instance);
        var shield = new System.Text.RegularExpressions.Regex("(\"state\":\"(steelBarrier|reactiveAegis)\"[^{}]*?\"absorb\":)[1-9][0-9]*");
        while (!engine.IsFinished && !shield.IsMatch(engine.Snapshot().Text)) engine.Step();
        Assert.False(engine.IsFinished, "bouclier actif entre deux ticks");
        string text = engine.Snapshot().Text;
        CombatSetup setup = engine.CompletedSetup();
        Assert.Equal(text, CombatEngine.Restore(setup, CombatSnapshot.Parse(text), NullCombatLog.Instance).Snapshot().Text);
        string drained = shield.Replace(text, "${1}0", 1);
        Assert.NotEqual(text, drained);
        Assert.Contains("absorption impossible", Assert.Throws<FormatException>(() => CombatEngine.Restore(setup, CombatSnapshot.Parse(drained),
            NullCombatLog.Instance)).Message);
        // 20e tour : une absorption au-delà de la plus forte application possible de ce bouclier est refusée.
        string inflated = shield.Replace(text, "${1}999999999", 1);
        Assert.Contains("absorption au-delà du plus fort bouclier possible", Assert.Throws<FormatException>(() => CombatEngine.Restore(setup,
            CombatSnapshot.Parse(inflated), NullCombatLog.Instance)).Message);
        var depleted = new System.Text.RegularExpressions.Regex("(\"state\":\"(steelBarrier|reactiveAegis)\"[^{}]*?\"depletedBy\":)null");
        string marked = depleted.Replace(text, "${1}{\"root\":\"x\",\"depth\":0,\"chain\":[],\"trigger\":null,\"cause\":null,\"source\":null,\"instance\":null,\"generation\":null}", 1);
        Assert.NotEqual(text, marked);
        Assert.Contains("absorption impossible", Assert.Throws<FormatException>(() => CombatEngine.Restore(setup, CombatSnapshot.Parse(marked),
            NullCombatLog.Instance)).Message);
    }

    [Fact]
    public void Recharge_de_declencheur_sans_activation_refusee()
    {
        // Revue Codex de la PR #32 (25e tour) : scénario entrainement_declencheurs, instantané après le tick 0 ; donner au premier
        // déclencheur de l'assaillant une recharge en cours (activation inventée au tick 0, qui n'a écrit aucune ligne) est refusé.
        ScenarioLoadResult loaded = ScenarioJsonReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "scenarios", "entrainement_declencheurs.json"), ShippedData);
        var engine = new CombatEngine(loaded.Setup!, NullCombatLog.Instance);
        engine.Step();
        string text = engine.Snapshot().Text;
        CombatSetup setup = engine.CompletedSetup();
        Assert.Equal(text, CombatEngine.Restore(setup, CombatSnapshot.Parse(text), NullCombatLog.Instance).Snapshot().Text);
        string line = text.Split('\n').First(l => l.StartsWith("{\"fighter\":\"assaillant\"", StringComparison.Ordinal));
        var triggers = System.Text.RegularExpressions.Regex.Match(line, "\"triggers\":\\[0");
        Assert.True(triggers.Success, line);
        string tampered = text.Replace(line, line.Replace(triggers.Value, "\"triggers\":[40", StringComparison.Ordinal), StringComparison.Ordinal);
        Exception? refused = Record.Exception(() => CombatEngine.Restore(setup, CombatSnapshot.Parse(tampered), NullCombatLog.Instance));
        Assert.True(refused is FormatException && refused.Message.Contains("sans activation possible", StringComparison.Ordinal), refused?.Message ?? "accepté");
    }

    [Fact]
    public void Recharge_retouchee_refusee()
    {
        // Revue Codex de la PR #32 (4e tour) : une recharge en cours est celle d'une compétence du Bastion, et son échéance est en file une
        // seule fois à son tick (une échéance en double écrirait deux fois cooldownReady).
        ScenarioLoadResult loaded = ScenarioJsonReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "scenarios", "entrainement_garde_parade.json"), ShippedData);
        var engine = new CombatEngine(loaded.Setup!, NullCombatLog.Instance);
        while (!engine.Snapshot().Text.Contains("\"item\":\"cooldown\"", StringComparison.Ordinal)) engine.Step();
        string text = engine.Snapshot().Text;
        CombatSetup setup = engine.CompletedSetup();
        Assert.Equal(text, CombatEngine.Restore(setup, CombatSnapshot.Parse(text), NullCombatLog.Instance).Snapshot().Text);
        List<string> lines = text.Split('\n').ToList();
        string cooldown = lines.First(l => l.Contains("\"item\":\"cooldown\"", StringComparison.Ordinal));
        string skill = System.Text.RegularExpressions.Regex.Match(cooldown, "\"skill\":\"([^\"]+)\"").Groups[1].Value;
        // Échéance en double (prochaine séquence, compte et pic de la file mis à jour).
        var head = System.Text.RegularExpressions.Regex.Match(lines[0], "\"next\":([0-9]+),\"count\":([0-9]+)");
        long next = long.Parse(head.Groups[1].Value), count = long.Parse(head.Groups[2].Value);
        var peak = System.Text.RegularExpressions.Regex.Match(lines[0], "\"peakPending\":([0-9]+)");
        var doubled = new List<string>(lines);
        doubled[0] = doubled[0].Replace(head.Value, $"\"next\":{next + 1},\"count\":{count + 1}")
            .Replace(peak.Value, $"\"peakPending\":{Math.Max(long.Parse(peak.Groups[1].Value), count + 1)}");
        doubled.Add("{\"entry\":" + next + cooldown.Substring(cooldown.IndexOf(',')));
        Assert.Contains("échéance(s)", Assert.Throws<FormatException>(() => CombatEngine.Restore(setup, CombatSnapshot.Parse(string.Join("\n", doubled)),
            NullCombatLog.Instance)).Message);
        // Revue Codex de la PR #32 (8e tour) : une échéance de recharge sans recharge en cours (compétence inconnue ou au repos), refusée.
        var fake = new List<string>(doubled);
        fake[fake.Count - 1] = fake[fake.Count - 1].Replace("\"skill\":\"" + skill + "\"", "\"skill\":\"skill.fausse\"");
        Assert.NotEqual(doubled[doubled.Count - 1], fake[fake.Count - 1]);
        Assert.Contains("sans recharge en cours", Assert.Throws<FormatException>(() => CombatEngine.Restore(setup, CombatSnapshot.Parse(string.Join("\n", fake)),
            NullCombatLog.Instance)).Message);
        // Recharge d'une compétence inconnue (et son échéance, renommée de même).
        string renamed = text.Replace("\"" + skill + "\"", "\"skill.inconnue\"");
        Assert.NotEqual(text, renamed);
        Assert.Contains("compétence inconnue", Assert.Throws<FormatException>(() => CombatEngine.Restore(setup, CombatSnapshot.Parse(renamed),
            NullCombatLog.Instance)).Message);
    }
}
