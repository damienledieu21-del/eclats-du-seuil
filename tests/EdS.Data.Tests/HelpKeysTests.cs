using EdS.Runtime;

namespace EdS.Data.Tests;

/// <summary>Essai réaliste 6 : l'aide des touches en lignes (touche, action) pour la dalle de l'interface.</summary>
[Trait("Category", "Runtime")]
public class HelpKeysTests
{
    [Fact]
    public void Decoupe_le_texte_du_monde()
    {
        IReadOnlyList<HelpKey> keys = HelpKeys.Parse("F parler ou récolter · I inventaire · Entrée halte · F6 sauvegarder");
        Assert.Equal(new[] { "F : Parler ou récolter", "I : Inventaire", "Entrée : Halte", "F6 : Sauvegarder" }, keys.Select(k => k.ToString()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("F · ·  · I")]
    public void Texte_vide_ou_sans_action(string? text) => Assert.Empty(HelpKeys.Parse(text!));

    [Fact]
    public void Aventure_avec_les_touches_des_donnees()
    {
        string root = Lot93UnityPackageTests.Root;
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "data", "v0", "world", "texts", "fr.json")));
        string? help = Find(doc.RootElement, "text.ui.help");
        Assert.NotNull(help);
        IReadOnlyList<HelpKey> keys = HelpKeys.Adventure(help!);
        foreach (string key in new[] { "Z Q S D", "Espace", "F", "I", "J", "K", "P", "Entrée", "F6", "L", "T", "B", "Échap", "H" })
            Assert.Contains(keys, k => k.Key == key);
        Assert.Equal(keys.Count, keys.Select(k => k.Key).Distinct().Count());
        Assert.All(keys, k => Assert.True(char.IsUpper(k.Action[0]) || char.IsDigit(k.Action[0]), k.ToString()));
        // Sous-lot V1.5 : B ouvre l'écran des compétences (K reste l'atelier), dit autrement que les touches 1 à 4.
        Assert.Equal("Choisir ses compétences", keys.Single(k => k.Key == "B").Action);
        Assert.Equal(keys.Count, keys.Select(k => k.Action).Distinct().Count());
    }

    [Fact]
    public void Entrainement_et_reseau()
    {
        Assert.Contains(HelpKeys.Training, k => k.Key == "T" && k.Action == "Talents");
        Assert.Contains(HelpKeys.Network, k => k.Key == "G");
        Assert.DoesNotContain(HelpKeys.Network, k => k.Key == "Échap");
        Assert.Equal(HelpKeys.Training.Count, HelpKeys.Training.Select(k => k.Key).Distinct().Count());
    }

    private static string? Find(System.Text.Json.JsonElement e, string name)
    {
        if (e.ValueKind == System.Text.Json.JsonValueKind.Object)
            foreach (var p in e.EnumerateObject())
            {
                if (p.Name == name && p.Value.ValueKind == System.Text.Json.JsonValueKind.String) return p.Value.GetString();
                if (Find(p.Value, name) is string found) return found;
            }
        return null;
    }
}
