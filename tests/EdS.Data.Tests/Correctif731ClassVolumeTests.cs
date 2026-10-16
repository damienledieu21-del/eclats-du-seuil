using System.Diagnostics;
using System.Text.Json.Nodes;
using EdS.Data.Classes;
using EdS.Data.Json;
using EdS.Data.Validation;
using static EdS.Data.Tests.ClassTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Correctif 7.3.1 (A73-M03) : classes de l'audit (bastion.json et 128, 256 ou 512 passifs de 8 ripostes, soit 1 024, 2 048 et 4 096
/// déclencheurs), lues par le lecteur JSON et la CLI. Avant le correctif, elles étaient valides et leur analyse des cycles prenait
/// 1,2 s, 8,1 s et plus de 15 s (CLI de l'audit). Elles sont désormais refusées par le plafond de 512 déclencheurs par classe, avant
/// l'analyse ; à la frontière (512), la classe est admise avec son avertissement de cycle, en temps borné.
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Limits")]
public class Correctif731ClassVolumeTests
{
    /// <summary>Même construction que le script de l'annexe de l'audit.</summary>
    private static string AuditClass(int skills)
    {
        JsonObject root = Shipped();
        JsonObject prototype = DataOf(root)["skills"]!.AsArray().Select(s => s!.AsObject()).Single(s => s["id"]!.GetValue<string>() == "skill.bastion.test_passif_1");
        for (int i = 0; i < skills; i++)
        {
            JsonObject skill = prototype.DeepClone().AsObject();
            skill["id"] = $"skill.bastion.audit{i}";
            skill["name"] = "Audit";
            skill["placeholder"] = false;
            var triggers = new JsonArray();
            for (int k = 0; k < 8; k++)
                triggers.Add(JsonNode.Parse("""{ "on": "onHitTaken", "effects": [ { "type": "strike", "target": "eventSource", "coefficient": 1, "powerType": "physique", "damageType": "physique" } ] }"""));
            skill["triggers"] = triggers;
            DataOf(root)["skills"]!.AsArray().Add(skill);
        }
        return root.ToJsonString();
    }

    [Theory]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(512)]
    public void Classes_de_l_audit_refusees_avant_l_analyse(int skills)
    {
        string json = AuditClass(skills);
        var clock = Stopwatch.StartNew();
        ClassLoadResult result = ClassJsonReader.Read(json, Progression);
        clock.Stop();
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Code == ValidationCode.LimitExceeded && e.Path == "$.data.skills"
            && e.Message.Contains($"{skills * 8} déclencheurs"));
        Assert.Empty(result.Report.Warnings);   // aucune analyse des cycles
        // Garde-fou contre un blocage (l'ancienne analyse prenait des minutes), pas une mesure (D3).
        Assert.True(clock.ElapsedMilliseconds <= 20_000, $"{skills * 8} déclencheurs, {json.Length} caractères : {clock.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Classe_au_plafond_admise_avec_son_avertissement()
    {
        string json = AuditClass(64);   // 512 déclencheurs, la classe livrée n'en a aucun
        var clock = Stopwatch.StartNew();
        ClassLoadResult result = ClassJsonReader.Read(json, Progression);
        clock.Stop();
        Assert.True(result.IsValid, string.Join("; ", result.Report.Errors.Select(e => e.ToString())));
        ValidationError warning = Assert.Single(result.Report.Warnings);
        Assert.Contains("… et 504 autres", warning.Message);
        Assert.True(clock.ElapsedMilliseconds <= 20_000, $"512 déclencheurs : {clock.ElapsedMilliseconds} ms");
        Assert.False(ClassJsonReader.Read(AuditClass(65), Progression).IsValid);   // 520
    }

    [Fact]
    public void Cli_refuse_la_classe_de_4096_declencheurs()
    {
        Lot71Files.InTempDirectory(directory =>
        {
            string path = Path.Combine(directory, "graph-class-512.json");
            File.WriteAllText(path, AuditClass(512));
            var clock = Stopwatch.StartNew();
            var run = Lot71Files.Cli(directory, "validate", path, "--rules", RulesTestData.ShippedPath);
            clock.Stop();
            Assert.NotEqual(0, run.ExitCode);
            Assert.Contains("4096 déclencheurs", run.Output + run.Error);
            Assert.True(clock.ElapsedMilliseconds <= 60_000, $"CLI : {clock.ElapsedMilliseconds} ms");
            return 0;
        });
    }
}
