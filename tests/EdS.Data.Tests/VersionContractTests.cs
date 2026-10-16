using System.Text.Json.Nodes;
using EdS.Data.Classes;
using EdS.Data.Json;
using EdS.Data.Validation;

namespace EdS.Data.Tests;

/// <summary>Correctif 6.1 : contrat SemVer strict et refus explicite des formats de fichiers antérieurs.</summary>
[Trait("Category", "Data")]
[Trait("Category", "Regression")]
public class VersionContractTests
{
    [Theory]
    [InlineData("0.1.0")]
    [InlineData("0.2.0-provisoire")]
    [InlineData("1.0.0-alpha.1")]
    [InlineData("1.0.0-0.3.7")]
    [InlineData("1.0.0-x-y-z.--")]
    [InlineData("1.0.0+20261016.sha.5114f85")]
    [InlineData("10.20.30-rc.1+build.7")]
    public void SemVer_valide(string version) => Assert.True(HeaderValidator.IsSemVer(version));

    [Theory]
    [InlineData("0.1.0\n")]                  // « $ » acceptait le saut de ligne final
    [InlineData("0.1.0-provisoire\n")]
    [InlineData(" 0.1.0")]
    [InlineData("01.0.0")]
    [InlineData("1.02.0")]
    [InlineData("1.0.0-01")]                 // identifiant numérique de pré-version avec zéro initial
    [InlineData("1.0.0-alpha.01")]
    [InlineData("١.٠.٠")]     // chiffres arabes-indiens : acceptés par \d
    [InlineData("1.0.0-１")]             // chiffre pleine chasse
    [InlineData("1.0")]
    [InlineData("1.0.0-")]
    [InlineData("1.0.0+")]
    [InlineData("1.0.0-alpha..1")]
    [InlineData("v1.0.0")]
    [InlineData("")]
    public void SemVer_refuse(string version) => Assert.False(HeaderValidator.IsSemVer(version));

    [Fact]
    public void Classe_au_format_v1_refusee_explicitement()
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ClassTestData.BastionPath))!.AsObject();
        root["schemaVersion"] = 1;
        ClassLoadResult result = ClassJsonReader.Read(root.ToJsonString(), RulesJsonReader.ReadFile(RulesTestData.ShippedPath).Rules!.Progression);
        ValidationError error = Assert.Single(result.Report.Errors);
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), (error.Path, error.Code));
        Assert.Contains("aucune migration", error.Message);
    }

    [Fact]
    public void Version_de_contenu_avec_saut_de_ligne_refusee()
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ClassTestData.BastionPath))!.AsObject();
        root["contentVersion"] = "0.2.0-provisoire\n";
        ClassLoadResult result = ClassJsonReader.Read(root.ToJsonString(), RulesJsonReader.ReadFile(RulesTestData.ShippedPath).Rules!.Progression);
        Assert.Contains(result.Report.Errors, e => e.Path == "$.contentVersion" && e.Code == ValidationCode.InvalidContentVersion);
    }
}
