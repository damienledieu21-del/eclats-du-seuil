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
}
