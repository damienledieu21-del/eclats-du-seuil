using System.Security.Cryptography;
using System.Text;
using EdS.Data;
using EdS.Data.Json;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>Rejeu : seulement avec le même moteur et des données de même empreinte, puis comparaison octet par octet.</summary>
[Trait("Category", "Engine")]
public class CombatReplayTests
{
    private static MemoryCombatLog Simulate()
    {
        CombatSetup setup = ScenarioJsonReader.ReadFile(MannequinPath, ShippedData).Setup!;
        var log = new MemoryCombatLog();
        CombatEngine.Run(setup, log);
        return log;
    }

    private static GameData LoadCopy(Func<string, string, string> transform)
    {
        string directory = CopyData(transform);
        try
        {
            return GameDataLoader.Load(directory).Data!;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Rejeu_identique_avec_les_memes_donnees()
    {
        ReplayOutcome outcome = CombatLogReplayer.Replay(Simulate().Lines, ShippedData);
        Assert.Equal(ReplayStatus.Identical, outcome.Status);
        Assert.True(outcome.Comparison!.Identical);
    }

    [Fact]
    public void Rejeu_depuis_un_fichier()
    {
        string path = Path.Combine(Path.GetTempPath(), $"eds-{Guid.NewGuid():N}.jsonl");
        try
        {
            File.WriteAllText(path, Simulate().ToString());
            Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.ReplayFile(path, ShippedData).Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Rejeu_refuse_si_les_regles_ont_change()
    {
        GameData changed = LoadCopy((file, text) => file == "rules.json" ? text.Replace("\"maxHealth\": 100", "\"maxHealth\": 101") : text);
        ReplayOutcome outcome = CombatLogReplayer.Replay(Simulate().Lines, changed);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Contains(outcome.Problems, p => p.StartsWith("rules.json modifié", StringComparison.Ordinal));
        Assert.Null(outcome.Comparison);   // aucune simulation n'a été tentée
    }

    [Fact]
    public void Rejeu_refuse_si_la_classe_a_change()
    {
        GameData changed = LoadCopy((file, text) => file == "classes/bastion.json" ? text.Replace("\"cooldownMs\": 6000", "\"cooldownMs\": 5000") : text);
        ReplayOutcome outcome = CombatLogReplayer.Replay(Simulate().Lines, changed);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Contains(outcome.Problems, p => p.Contains("class.bastion") && p.Contains("empreinte"));
    }

    [Fact]
    public void Rejeu_refuse_si_seule_la_version_de_contenu_a_change()
    {
        GameData changed = LoadCopy((file, text) => file == "rules.json" ? text.Replace($"\"contentVersion\": \"{DataFormat.ProvisionalRulesContentVersion}\"", "\"contentVersion\": \"0.1.1\"") : text);
        Assert.Equal(changed.Rules.Fingerprint, ShippedData.Rules.Fingerprint);   // l'empreinte ignore la version…
        ReplayOutcome outcome = CombatLogReplayer.Replay(Simulate().Lines, changed);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);                // …mais le rejeu exige aussi la même version
    }

    [Fact]
    public void Rejeu_refuse_un_journal_au_format_1()
    {
        // Correctif 6.1 : format de journal 2 ; un journal du lot 6 (format 1) est refusé explicitement, sans migration.
        var lines = Simulate().Lines.ToArray();
        lines[0] = lines[0].Replace($"\"formatVersion\":{CombatEngine.LogFormatVersion}", "\"formatVersion\":1");
        ReplayOutcome outcome = CombatLogReplayer.Replay(lines, ShippedData);
        Assert.NotEqual(ReplayStatus.Identical, outcome.Status);
        Assert.Contains(outcome.Problems, p => p.Contains("format de journal 1") && p.Contains("sans migration"));
    }

    [Fact]
    public void Rejeu_refuse_avec_une_autre_version_du_moteur()
    {
        var lines = Simulate().Lines.ToArray();
        lines[0] = lines[0].Replace($"\"engineVersion\":\"{CombatEngine.Version}\"", "\"engineVersion\":\"0.4.9\"");
        ReplayOutcome outcome = CombatLogReplayer.Replay(lines, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Contains(outcome.Problems, p => p.Contains("0.4.9"));
    }

    [Fact]
    public void Journal_altere_detecte_a_la_bonne_ligne()
    {
        var lines = Simulate().Lines.ToArray();
        int index = Array.FindIndex(lines, l => l.Contains("\"event\":\"damage\""));
        lines[index] = lines[index].Replace("\"amount\":", "\"amount\":1");
        ReplayOutcome outcome = CombatLogReplayer.Replay(lines, ShippedData);
        Assert.Equal(ReplayStatus.Diverged, outcome.Status);
        Assert.Equal(index + 1, outcome.Comparison!.FirstDifferentLine);
    }

    [Fact]
    public void Journal_tronque_detecte()
    {
        var lines = Simulate().Lines;
        ReplayOutcome outcome = CombatLogReplayer.Replay(lines.Take(lines.Count - 1).ToArray(), ShippedData);
        Assert.Equal(ReplayStatus.Diverged, outcome.Status);
        Assert.Equal(lines.Count, outcome.Comparison!.FirstDifferentLine);
        Assert.Null(outcome.Comparison.Expected);
    }

    [Fact]
    public void En_tete_illisible()
    {
        Assert.Equal(ReplayStatus.Invalid, CombatLogReplayer.Replay(new[] { "pas du json" }, ShippedData).Status);
        Assert.Equal(ReplayStatus.Invalid, CombatLogReplayer.Replay(Array.Empty<string>(), ShippedData).Status);
        Assert.Equal(ReplayStatus.Invalid, CombatLogReplayer.Replay(new[] { "{\"type\":\"header\"}" }, ShippedData).Status);
    }

    [Fact]
    public void Journal_du_scenario_livre_stable()
    {
        // Référence du journal de scenarios/mannequin.json avec data/v0 : à mettre à jour sciemment si les données ou le moteur changent.
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Simulate().ToString()))).ToLowerInvariant();
        Assert.Equal(MannequinLogHash, hash);
    }

    internal const string MannequinLogHash = "e475db634236f5f1dc8de4de057b96c82d729329f02c3a24105fc18a35543319";
}
