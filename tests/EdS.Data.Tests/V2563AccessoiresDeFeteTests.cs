using System.IO;
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

    // Revue Codex de la PR #237 : la pose effective (choix de l'accessoire à l'habillage, os d'attache) est vérifiée sans Unity.

    [Theory]
    [InlineData(false, true, true, "F07_P01")]
    [InlineData(true, true, true, null)]     // le héros
    [InlineData(false, false, true, null)]   // sans la vraie tenue de fête (HumanClothes faux)
    [InlineData(false, true, false, null)]   // hors fête d'Orsédia
    public void Pose_a_l_habillage_seulement_en_fete_et_en_tenue(bool hero, bool festiveClothes, bool festival, string? code)
    {
        Assert.Equal(code, FeastAccessories.Worn(7, HumanOutfit.Peasant, hero, festiveClothes, festival)?.Code);
        Assert.Null(FeastAccessories.Worn(0, HumanOutfit.Peasant, false, true, true));
        Assert.Null(FeastAccessories.Worn(7, HumanOutfit.Guard, false, true, true));
        Assert.Equal("F03_R01", FeastAccessories.Worn(3, HumanOutfit.Merchant, false, true, true)!.Code);
    }

    [Theory]
    [InlineData("Head", FeastAccessoryPlace.Face, true)]
    [InlineData("mixamorig:Head", FeastAccessoryPlace.Face, true)]
    [InlineData("HeadTop_End", FeastAccessoryPlace.Face, false)]
    [InlineData("hand_r", FeastAccessoryPlace.Face, false)]
    [InlineData("mixamorig:RightHand", FeastAccessoryPlace.Hand, true)]
    [InlineData("hand_r", FeastAccessoryPlace.Hand, true)]
    [InlineData("Hand.R", FeastAccessoryPlace.Hand, true)]
    [InlineData("mixamorig:LeftHand", FeastAccessoryPlace.Hand, false)]
    [InlineData("mixamorig:RightHandIndex1", FeastAccessoryPlace.Hand, false)]
    [InlineData("Head", FeastAccessoryPlace.Hand, false)]
    [InlineData(null, FeastAccessoryPlace.Hand, false)]
    public void Masque_sur_la_tete_plat_dans_la_main(string? bone, FeastAccessoryPlace place, bool expected) =>
        Assert.Equal(expected, FeastAccessories.IsBone(bone, place));

    [Fact]
    public void L_habillage_attache_vraiment_l_accessoire()
    {
        string game = Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game");
        string style = File.ReadAllText(Path.Combine(game, "StyleLook.cs")).Replace("\r\n", "\n");
        // Branche MakeHuman (DressMade) et branche Quaternius/Mixamo : l'appel, avec la décision de FeastAccessories.Worn.
        int made = style.IndexOf("private static StyleBody DressMade(", System.StringComparison.Ordinal);
        int madeEnd = style.IndexOf("\n        }\n", made, System.StringComparison.Ordinal);
        Assert.True(made > 0 && madeEnd > made);
        Assert.Contains("FeastAccessoryLook.Attach(model, FeastAccessories.Worn(HumanBodies.Feast, HumanOutfit.Peasant, slot == StyleSlot.Hero, catalog.HumanClothes, HumanBodies.Festival));",
            style.Substring(made, madeEnd - made));
        int dressed = style.IndexOf("HumanBodies.Dress(model, catalog.Humans, person, clothes);", System.StringComparison.Ordinal);
        int mixamo = style.IndexOf("FeastAccessoryLook.Attach(model, FeastAccessories.Worn(HumanBodies.Feast, person.Outfit, slot == StyleSlot.Hero, clothes != null && clothes.Festive, HumanBodies.Festival));", System.StringComparison.Ordinal);
        Assert.True(dressed > 0 && mixamo > dressed);
        Assert.Equal(2, CountOf(style, "FeastAccessoryLook.Attach("));

        // Attach : refus seulement sans accessoire, modèle, os ou rendu ; sinon rattaché à l'os trouvé par FeastAccessories.IsBone.
        string look = File.ReadAllText(Path.Combine(game, "FeastAccessoryLook.cs")).Replace("\r\n", "\n");
        Assert.Contains("GameObject prefab = accessory != null && catalog != null ? catalog.FeastModel(accessory.Code) : null;", look);
        Assert.Contains("Transform bone = Bone(model.transform, accessory.Place);", look);
        Assert.Contains("if (FeastAccessories.IsBone(t.name, place)) return t;", look);
        Assert.Contains("holder.transform.SetParent(bone, true);\n            return true;", look);
        Assert.Equal(3, CountOf(look, "return false;"));
        Assert.Equal(1, CountOf(look, "return true;"));
    }

    private static int CountOf(string text, string part)
    {
        int n = 0;
        for (int i = text.IndexOf(part, System.StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + part.Length, System.StringComparison.Ordinal)) n++;
        return n;
    }
}
