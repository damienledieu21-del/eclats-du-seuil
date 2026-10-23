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

    // Sous-lot 8.7 : la Provocation (lancée sur le mannequin, provocation refusée notApplicable) inflige désormais 0,8 : 461 dégâts au
    // lieu de 441, 56 de Résonance au lieu de 53. Sous-lot 11.1 : moteur 0.15.0, format 9 ; seul l'en-tête change (ancienne empreinte
    // 4c5f475b…). Sous-lot 11.4a : moteur 0.16.0, format 10, règles 0.9.0 ; combat sans espace : seul l'en-tête change (ancienne
    // empreinte 0d88a125…). Sous-lot 11.4b : moteur 0.17.0, format 11, règles 0.10.0, Bastion 0.6.0 (portées) ; combat sans espace :
    // seul l'en-tête change (ancienne empreinte b8bf4baa…). Sous-lot 11.4c : moteur 0.18.0, format 12, règles 0.11.0, scénario en
    // schéma 6 ; seul l'en-tête change (ancienne empreinte 08f267a9…). Sous-lot 11.4d : moteur 0.19.0, format 13, scénario en schéma 7 ;
    // seul l'en-tête change (ancienne empreinte 70ef6a7f…). Sous-lot 11.5 : moteur 0.20.0, format 14, schéma 8 ; seul l'en-tête change
    // (ancienne empreinte 1754b668…). Sous-lot 11.9 : moteur 0.21.0, format 15, schéma 9 ; seul l'en-tête change (ancienne empreinte
    // 9d42464b…). Sous-lot 12.1 : moteur 0.22.0, format 16, schéma 10 ; seul l'en-tête change (ancienne empreinte a3025f2f…).
    internal const string MannequinLogHash = "bcdf227b7e3fbf12644ee101357ffb82fef96eb6b153b2f8da0fe7fe28f7ccd4";
}
