using EdS.Domain.Builds;
using EdS.Domain.Combat;

namespace EdS.Domain.Tests.Regression;

/// <summary>
/// Correctif 6.1 : les identifiants doivent correspondre en entier au contrat (minuscules ASCII, chiffres, « _ »).
/// Avant, « $ » acceptait un saut de ligne final (« class.bastion\n »).
/// </summary>
[Trait("Category", "Build")]
[Trait("Category", "Regression")]
public class IdentifierContractTests
{
    [Theory]
    [InlineData("class.bastion", true)]
    [InlineData("talent.bastion.rempart.garde_stable", true)]
    [InlineData("skill.a1.b_2", true)]
    [InlineData("class.bastion\n", false)]
    [InlineData("class.bastion\r\n", false)]
    [InlineData("\nclass.bastion", false)]
    [InlineData("class.bastion ", false)]
    [InlineData("Class.bastion", false)]
    [InlineData("class.bastıon", false)]         // i sans point (U+0131)
    [InlineData("class.baKion", false)]     // signe kelvin, minuscule « k » en Unicode
    [InlineData("class.bastion٣", false)]   // chiffre arabe-indien
    [InlineData("class", false)]
    [InlineData("class.", false)]
    [InlineData("class..bastion", false)]
    [InlineData("", false)]
    public void Identifiant_stable(string id, bool valid) => Assert.Equal(valid, StableId.IsValid(id));

    [Theory]
    [InlineData("bastion", true)]
    [InlineData("a_1", true)]
    [InlineData("bastion\n", false)]
    [InlineData("bastion\r", false)]
    [InlineData("bästion", false)]
    [InlineData("bastion１", false)]         // chiffre pleine chasse
    [InlineData("1bastion", false)]
    [InlineData("_bastion", false)]
    [InlineData("Bastion", false)]
    [InlineData("", false)]
    public void Nom_de_combattant_ou_d_equipe(string name, bool valid) => Assert.Equal(valid, CombatSetup.IsValidName(name));
}
