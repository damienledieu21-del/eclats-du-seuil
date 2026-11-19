using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Crafting;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Zones;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Bois et pierre, sous-lot V2.43.2a (décision de Damien du 19 novembre 2026, pour les lieux reconstruits) : matériaux bruts (bois,
/// pierre) récoltés aux nœuds existants (forêt des Loups et cabane du bûcheron ; carrière et gradins de la grande carrière), matériaux
/// travaillés (planches, pierres taillées) par une recette des métiers existants, à la cabane du bûcheron et aux gradins de la carrière.
/// </summary>
[Trait("Category", "World")]
public class BoisEtPierreTests
{
    private static ZoneDefinition At(double x, double z) => Zone(x, z);

    private static CraftingBook Crafting(ZoneDefinition zone) => CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory), Texts, zone, ItemsCatalog).Book!;

    private static WorldSession Play(ZoneDefinition zone, params ItemStack[] items)
    {
        SavedCharacter hero = PlayerCharacters.Create("bastion", "class.bastion", Progression).WithItems(items, new Dictionary<string, string>());
        var w = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero), zone, ShippedData, Progression, ItemsCatalog, null, Crafting(zone));
        w.Step();
        return w;
    }

    [Fact]
    public void Matieres_recettes_et_noeuds()
    {
        foreach (string id in new[] { "item.bois", "item.pierre", "item.planche", "item.pierre_taillee" })
        {
            Assert.True(ItemsCatalog.TryGetItem(id, out ItemDefinition item), id);
            Assert.Equal(ItemType.Material, item.Type);
            Assert.True(ShippedMerchants.Values.ContainsKey(id), id);   // se vend comme les autres matériaux
        }
        CraftingBook book = Crafting(Zone());
        Assert.Equal(new[] { "bois_du_bucheron", "bois_foret_est", "bois_foret_ouest" },
            book.Nodes.Where(n => n.Item == "item.bois").Select(n => n.Id).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(3, book.Nodes.Count(n => n.Item == "item.pierre" && n.Profession == "taille_de_pierre"));
        Assert.All(book.Nodes.Where(n => n.Item == "item.bois"), n => Assert.Equal("bucheronnage", n.Profession));
        // Deux métiers nouveaux (décision de Damien du 19 novembre 2026), avec la progression commune des métiers.
        Assert.Contains(book.Professions, p => p.Id == "bucheronnage");
        Assert.Contains(book.Professions, p => p.Id == "taille_de_pierre");
        Assert.Equal(("Bûcheron", "Tailleur de pierre"), (Texts.Get("text.profession.bucheronnage"), Texts.Get("text.profession.taille_de_pierre")));
        Assert.Contains(book.Recipes, r => r.Id == "recipe.planche" && r.Place == "cabane_du_bucheron" && r.Profession == "bucheronnage");
        Assert.Contains(book.Recipes, r => r.Id == "recipe.pierre_taillee" && r.Place == "gradins_de_la_carriere" && r.Profession == "taille_de_pierre");
    }

    [Fact]
    public void On_recolte_la_pierre_et_on_la_taille_aux_gradins()
    {
        // Récolte au nœud de la petite carrière.
        WorldSession w = Play(At(48, -14));
        Assert.Equal(CraftRefusal.None, w.Gather("pierre_carriere", out _));
        Assert.Equal(1, w.Inventory.Count("item.pierre"));
        Assert.Equal(12, w.Save().Character!.Professions["taille_de_pierre"]);   // l'expérience va au métier du nœud
        // Taille aux gradins de la grande carrière.
        WorldSession g = Play(At(930, 300), new ItemStack("item.pierre", 3));
        Assert.Equal(CraftRefusal.None, g.Craft("recipe.pierre_taillee", out _));
        Assert.Equal((0, 1), (g.Inventory.Count("item.pierre"), g.Inventory.Count("item.pierre_taillee")));
        Assert.Equal(25, g.Save().Character!.Professions["taille_de_pierre"]);
    }

    [Fact]
    public void On_recolte_le_bois_et_on_scie_des_planches_chez_le_bucheron()
    {
        WorldSession w = Play(At(-57, 90));
        Assert.Equal(CraftRefusal.None, w.Gather("bois_foret_ouest", out _));
        Assert.Equal(1, w.Inventory.Count("item.bois"));
        Assert.Equal(12, w.Save().Character!.Professions["bucheronnage"]);
        WorldSession b = Play(At(-530, 480), new ItemStack("item.bois", 3));
        Assert.Equal(CraftRefusal.None, b.Craft("recipe.planche", out _));
        Assert.Equal((0, 2), (b.Inventory.Count("item.bois"), b.Inventory.Count("item.planche")));
        Assert.Equal(20, b.Save().Character!.Professions["bucheronnage"]);
        // La sauvegarde garde l'expérience des nouveaux métiers (format inchangé : métiers par identifiant).
        SaveLoadResult back = SaveJsonReader.Read(SaveJsonReader.Write(b.Save()));
        Assert.True(back.IsValid, back.Report.ToString());
        Assert.Equal(20, back.Save!.Character!.Professions["bucheronnage"]);
    }

    // --- Relecture indépendante de #222 et décision de Damien du 19 novembre 2026 : ateliers faciles près du village, refus ----------

    [Fact]
    public void Ateliers_faciles_pres_du_village()
    {
        CraftingBook book = Crafting(Zone());
        Assert.Equal(new[] { "cabane_du_bucheron", "moulin" }, book.Recipes.Where(r => r.Output.Item == "item.planche").Select(r => r.Place));
        Assert.Equal(new[] { "gradins_de_la_carriere", "carriere" }, book.Recipes.Where(r => r.Output.Item == "item.pierre_taillee").Select(r => r.Place));
        // Planches au moulin du village (niveau 1), pierre taillée à la petite carrière (niveau 6).
        WorldSession m = Play(At(-9, 5), new ItemStack("item.bois", 3));
        Assert.Equal(CraftRefusal.None, m.Craft("recipe.planche_du_moulin", out _));
        Assert.Equal(2, m.Inventory.Count("item.planche"));
        WorldSession c = Play(At(48, -14), new ItemStack("item.pierre", 3));
        Assert.Equal(CraftRefusal.None, c.Craft("recipe.pierre_taillee_de_la_carriere", out _));
        Assert.Equal(1, c.Inventory.Count("item.pierre_taillee"));
        Assert.Equal(CraftRefusal.NotHere, c.Craft("recipe.planche_du_moulin", out _));   // chaque recette à son atelier
    }

    [Theory]
    [InlineData("recipe.planche", "item.bois", -530.0, 480.0)]
    [InlineData("recipe.planche_du_moulin", "item.bois", -9.0, 5.0)]
    [InlineData("recipe.pierre_taillee", "item.pierre", 930.0, 300.0)]
    [InlineData("recipe.pierre_taillee_de_la_carriere", "item.pierre", 48.0, -14.0)]
    public void Pas_assez_de_matiere_refuse_rien_ne_change(string recipe, string raw, double x, double z)
    {
        WorldSession w = Play(At(x, z), new ItemStack(raw, 2));
        string before = SaveJsonReader.Write(w.Save());
        Assert.Equal(CraftRefusal.NotEnough, w.Craft(recipe, out CraftEvent? done));
        Assert.Null(done);
        Assert.Equal(2, w.Inventory.Count(raw));
        Assert.Equal(before, SaveJsonReader.Write(w.Save()));   // ni objet, ni expérience, ni compteur de fabrication
    }
}

