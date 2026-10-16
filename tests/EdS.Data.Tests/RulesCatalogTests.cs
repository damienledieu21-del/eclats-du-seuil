using System.Reflection;
using System.Text.Json;
using EdS.Data.Rules;
using EdS.Data.Validation;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;

namespace EdS.Data.Tests;

[Trait("Category", "Data")]
public class RulesCatalogTests
{
    [Theory]
    [InlineData(typeof(StatRules))]
    [InlineData(typeof(DamageRules))]
    public void Chaque_parametre_du_domaine_a_exactement_un_champ_dans_rules_json(Type owner)
    {
        var settable = owner.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite).Select(p => p.Name).ToList();
        Assert.NotEmpty(settable);
        foreach (string property in settable)
            Assert.Single(RulesCatalog.Fields, f => f.Owner == owner && f.Property == property);
        Assert.All(RulesCatalog.Fields.Where(f => f.Owner == owner), f => Assert.Contains(f.Property, settable));
    }

    [Fact]
    public void Chemins_uniques() =>
        Assert.Equal(RulesCatalog.Fields.Count, RulesCatalog.Fields.Select(f => f.Path).Distinct().Count());

    [Fact]
    public void Identifiants_stables()
    {
        Assert.Equal("force", RulesCatalog.Id(AttributeKind.Force));
        Assert.Equal("vitalite", RulesCatalog.Id(AttributeKind.Vitalite));
        Assert.Equal("physique", RulesCatalog.Id(DamageType.Physique));
        Assert.Equal("magique", RulesCatalog.Id(DamageType.Magique));
        Assert.Equal("halfAwayFromZero", RulesCatalog.Id(RoundingMode.HalfAwayFromZero));
    }

    [Fact]
    public void Schema_JSON_conforme_au_catalogue()
    {
        // Le schéma de data/schemas sert aux éditeurs ; il doit décrire exactement les champs du catalogue.
        string path = Path.Combine(AppContext.BaseDirectory, "data", "schemas", "rules.schema.json");
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(path));
        var leaves = new List<string>();
        void Walk(JsonElement node, string prefix)
        {
            Assert.False(node.GetProperty("additionalProperties").GetBoolean());
            var required = node.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToHashSet();
            foreach (JsonProperty p in node.GetProperty("properties").EnumerateObject())
            {
                Assert.Contains(p.Name, required);
                string childPath = prefix.Length == 0 ? p.Name : prefix + "." + p.Name;
                if (p.Value.TryGetProperty("properties", out _)) Walk(p.Value, childPath);
                else leaves.Add(childPath);
            }
        }
        Walk(schema.RootElement.GetProperty("properties").GetProperty("data"), string.Empty);
        Assert.Equal(RulesCatalog.Fields.Select(f => f.Path).OrderBy(p => p), leaves.OrderBy(p => p));
    }

    [Fact]
    public void Validateur_commun_utilisable_sans_JSON()
    {
        // Chemin d'un futur chargeur Unity : il remplit un RulesDocument et appelle le même validateur.
        var document = new RulesDocument { SchemaVersion = DataFormat.CurrentSchemaVersion, Kind = "rules", ContentVersion = "0.1.0" };
        foreach (RuleField field in RulesCatalog.Fields)
        {
            object provisional = field.Owner == typeof(StatRules) ? StatRules.Provisional
                : field.Owner == typeof(DamageRules) ? DamageRules.Provisional
                : field.Owner == typeof(CombatRules) ? CombatRules.Provisional
                : ProgressionRules.Provisional;
            object value = field.Owner.GetProperty(field.Property)!.GetValue(provisional)!;
            document.Set(field.Path, value switch
            {
                Fixed f => RuleValue.FromDecimal(f),
                int i => RuleValue.FromInteger(i),
                long l => RuleValue.FromInteger(l),
                RoundingMode m => RuleValue.FromToken(RulesCatalog.Id(m)),
                IReadOnlyList<int> list => RuleValue.FromIntegerList(list.Select(x => (long)x)),
                _ => throw new InvalidOperationException(field.Path),
            });
        }

        RulesLoadResult result = RulesValidator.Validate(document);
        Assert.True(result.IsValid, result.Report.ToString());

        document.Set("stats.base.armor", RuleValue.FromToken("beaucoup"));
        document.Set("stats.inconnu", RuleValue.FromInteger(1));
        RulesLoadResult invalid = RulesValidator.Validate(document);
        Assert.Contains(invalid.Report.Errors, e => e.Path == "$.data.stats.base.armor" && e.Code == ValidationCode.WrongType);
        Assert.Contains(invalid.Report.Errors, e => e.Path == "$.data.stats.inconnu" && e.Code == ValidationCode.UnknownField);
    }

    [Fact]
    public void Message_d_erreur_lisible()
    {
        var error = new ValidationError("$.data.stats.base.armor", ValidationCode.WrongType, "nombre attendu.", 12);
        Assert.Equal("$.data.stats.base.armor (ligne 12) : nombre attendu. [WrongType]", error.ToString());
    }
}
