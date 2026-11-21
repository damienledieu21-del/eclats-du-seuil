using System.Linq;
using EdS.Data.Feasts;
using EdS.Runtime;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// V2.56.3 : l'accessoire de fête des habitants (décision de Damien du 19 novembre 2026) : un par fête, la récompense de la fête (R01),
/// portée par ceux qui prennent les teintes de la fête (ni gardes, ni bandits, ni le héros).
/// </summary>
[Trait("Category", "World")]
public class V2563AccessoiresDeFeteTests
{
    [Fact]
    public void Un_accessoire_par_fete_la_recompense_de_la_fete()
    {
        Assert.Equal(Enumerable.Range(1, 12), FeastAccessories.All.Select(a => a.Feast));
        Assert.All(FeastAccessories.All, a => Assert.Equal($"F{a.Feast:00}_R01", a.Code));
        Assert.All(FeastAccessories.All, a => Assert.True(FeastLooks.IsCode(a.Code), a.Code));
        // Chaque code est bien la récompense de sa fête dans le catalogue des fêtes.
        var rewards = FetesF4EtalTests.Feasts.Value.Refs.Where(r => r.Category == FeastCategory.Reward).Select(r => r.Code).ToHashSet();
        Assert.All(FeastAccessories.All, a => Assert.Contains(a.Code, rewards));
        Assert.Equal(FeastAccessoryPlace.Face, FeastAccessories.Of(3)!.Place);   // le masque du Carnaval
        Assert.Equal(new[] { 2, 5 }, FeastAccessories.All.Where(a => a.Place == FeastAccessoryPlace.Chest).Select(a => a.Feast));
        Assert.Equal(9, FeastAccessories.All.Count(a => a.Place == FeastAccessoryPlace.Hand));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(13)]
    public void Rien_hors_d_une_fete(int feast)
    {
        Assert.Null(FeastAccessories.Of(feast));
        Assert.Null(FeastAccessories.For(feast, HumanOutfit.Peasant));
    }

    [Fact]
    public void Ni_gardes_ni_bandits_ni_heros()
    {
        foreach (HumanOutfit o in System.Enum.GetValues(typeof(HumanOutfit)).Cast<HumanOutfit>())
        {
            bool none = o == HumanOutfit.Guard || o == HumanOutfit.Bandit || o == HumanOutfit.Hero;
            Assert.Equal(none, FeastAccessories.For(7, o) is null);
        }
        Assert.Equal("F07_R01", FeastAccessories.For(7, HumanOutfit.Merchant)!.Code);
    }
}
