using EdS.Runtime;

namespace EdS.Data.Tests;

/// <summary>Essai réaliste 6 (essai de Damien du 27 octobre 2026 : la fiche de la cible affichait « SANGLIER_V2 ») : noms montrés.</summary>
[Trait("Category", "Runtime")]
public class DisplayNamesTests
{
    [Theory]
    [InlineData("sanglier", "Sanglier")]
    [InlineData("sanglier2", "Sanglier")]
    [InlineData("sanglier2_v2", "Sanglier")]
    [InlineData("loup1_v13", "Loup")]
    [InlineData("loup_a", "Loup")]
    [InlineData("loup_cache", "Loup")]
    [InlineData("rodeur", "Rôdeur")]
    [InlineData("rodeur_v2", "Rôdeur")]
    [InlineData("gardien_du_seuil", "Gardien du Seuil")]
    [InlineData("gardien_eveille", "Gardien éveillé")]
    [InlineData("geant2", "Géant")]
    [InlineData("pillard_b", "Pillard")]
    [InlineData("archer", "Archer")]
    [InlineData("mannequin", "Mannequin")]
    [InlineData("bastion2", "Bastion")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Nom_montre(string? id, string shown) => Assert.Equal(shown, DisplayNames.Of(id!));

    /// <summary>Nom de l'attaque annoncée sous la cible (« Ruée · 1,5 s ») : accents, exemplaires et variantes.</summary>
    [Theory]
    [InlineData("charge", "Charge")]
    [InlineData("morsure", "Morsure")]
    [InlineData("coup_2", "Coup")]
    [InlineData("coup_bis", "Coup")]
    [InlineData("coup_bis_2", "Coup")]
    [InlineData("taille_3", "Taille")]
    [InlineData("coup_lourd", "Coup lourd")]
    [InlineData("coup_felant", "Coup fêlant")]
    [InlineData("ruee", "Ruée")]
    [InlineData("brulure", "Brûlure")]
    [InlineData("onde_du_seuil", "Onde du Seuil")]
    [InlineData("ecrasement", "Écrasement")]
    [InlineData("", "Coup")]
    [InlineData(null, "Coup")]
    public void Attaque_montree(string? id, string shown) => Assert.Equal(shown, DisplayNames.Attack(id!));

    /// <summary>Toutes les attaques des scénarios et du monde ont un nom propre (ni tiret bas, ni chiffre, ni minuscule au début).</summary>
    [Fact]
    public void Toutes_les_attaques_ont_un_nom_propre()
    {
        string root = Lot93UnityPackageTests.Root;
        var ids = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "scenarios"), "*.json", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "data", "v0", "world"), "*.json", SearchOption.AllDirectories)))
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
            Collect(doc.RootElement, ids);
        }
        Assert.NotEmpty(ids);
        foreach (string id in ids)
        {
            string name = DisplayNames.Attack(id);
            Assert.False(name.Contains('_') || name.Any(char.IsDigit) || !char.IsUpper(name[0]), $"{id} → {name}");
        }
    }

    private static void Collect(System.Text.Json.JsonElement e, ISet<string> ids)
    {
        if (e.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            if (e.TryGetProperty("unblockable", out _) && e.TryGetProperty("id", out var id) && id.ValueKind == System.Text.Json.JsonValueKind.String)
                ids.Add(id.GetString()!);
            foreach (var p in e.EnumerateObject()) Collect(p.Value, ids);
        }
        else if (e.ValueKind == System.Text.Json.JsonValueKind.Array)
            foreach (var item in e.EnumerateArray()) Collect(item, ids);
    }
}
