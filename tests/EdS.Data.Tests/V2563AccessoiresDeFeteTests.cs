using System.Linq;
using EdS.Data.Feasts;
using EdS.Runtime;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// V2.56.3 : l'accessoire de fête des habitants (décision de Damien du 19 novembre 2026) : un par fête : le plat de la fête (P01) en main,
/// sauf le masque du Carnaval (décision du 21 novembre 2026), porté par ceux qui prennent les teintes de la fête (ni gardes, ni bandits, ni le héros).
/// </summary>
[Trait("Category", "World")]
public class V2563AccessoiresDeFeteTests
{
    [Fact]
    public void Le_plat_de_la_fete_sauf_le_masque_du_carnaval()
    {
        Assert.Equal(Enumerable.Range(1, 12), FeastAccessories.All.Select(a => a.Feast));
        Assert.All(FeastAccessories.All, a => Assert.Equal(a.Feast == 3 ? "F03_R01" : $"F{a.Feast:00}_P01", a.Code));
        Assert.All(FeastAccessories.All, a => Assert.True(FeastLooks.IsCode(a.Code), a.Code));
        // Chaque plat est celui de sa fête dans le catalogue ; le masque, la récompense du Carnaval ; aucune autre récompense portée.
        var refs = FetesF4EtalTests.Feasts.Value.Refs;
        var dishes = refs.Where(r => r.Category == FeastCategory.Dish).Select(r => r.Code).ToHashSet();
        Assert.All(FeastAccessories.All.Where(a => a.Feast != 3), a => Assert.Contains(a.Code, dishes));
        Assert.Contains("F03_R01", refs.Where(r => r.Category == FeastCategory.Reward).Select(r => r.Code));
        Assert.Equal(new[] { 3 }, FeastAccessories.All.Where(a => a.Place != FeastAccessoryPlace.Hand).Select(a => a.Feast));
        Assert.Equal(FeastAccessoryPlace.Face, FeastAccessories.Of(3)!.Place);
        Assert.Single(FeastAccessories.All, a => a.Code.EndsWith("_R01", System.StringComparison.Ordinal));
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
        Assert.Equal("F07_P01", FeastAccessories.For(7, HumanOutfit.Merchant)!.Code);
    }
}
