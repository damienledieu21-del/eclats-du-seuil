using EdS.Data.Json;
using EdS.Data.Seasons;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.44.4 (vendanges, décisions de Damien du 20 novembre 2026) : rangs de vigne en trois états (vert, mûr avec grappes, vendangé
/// puis nu l'hiver), modèles du lot C reliés (grappe, hotte, panier), objets de vendange au bout des rangs pendant septembre.
/// </summary>
[Trait("Category", "World")]
public class SaisonsV2444Tests
{
    private static ZoneDefinition Zone => GrandeCarteLieux.Vallons.Value;

    [Fact]
    public void Trois_etats_de_la_vigne_selon_la_date()
    {
        VineRowLook green = VineLook.Look(VineState.Green, 1), ripe = VineLook.Look(VineState.Ripe, 2);
        Assert.Equal((1f, false), (green.Leaves, green.Grapes));
        Assert.Equal((1f, true), (ripe.Leaves, ripe.Grapes));
        Assert.True(ripe.G < green.G);   // feuillage plus sombre au mûrissement
        VineRowLook autumn = VineLook.Look(VineState.Harvested, 2), winter = VineLook.Look(VineState.Harvested, 3), march = VineLook.Look(VineState.Harvested, 0);
        Assert.False(autumn.Grapes);
        Assert.InRange(autumn.Leaves, 0.1f, 0.9f);   // feuillage d'automne clairsemé
        Assert.True(autumn.R > autumn.G);
        Assert.Equal(0f, winter.Leaves);   // cep nu
        Assert.Equal(0f, march.Leaves);
        Assert.Equal(green, VineLook.Look(null, 3));   // sans date : vert, comme avant
        // Avec le contenu livré : septembre mûr, octobre vendangé, août vert.
        SeasonBook book = SeasonJsonReader.ReadFile(SeasonJsonReader.PathIn(DataDirectory), Zone).Book!;
        Assert.True(VineLook.Look(book.VineOn(new EdS.Domain.Time.CivilDate(2026, 9, 10)), 2).Grapes);
        Assert.False(VineLook.Look(book.VineOn(new EdS.Domain.Time.CivilDate(2026, 10, 10)), 2).Grapes);
        Assert.False(VineLook.Look(book.VineOn(new EdS.Domain.Time.CivilDate(2026, 8, 10)), 1).Grapes);
        Assert.True(VineLook.PropsShown(VineState.Ripe));
        Assert.False(VineLook.PropsShown(VineState.Green) || VineLook.PropsShown(VineState.Harvested) || VineLook.PropsShown(null));
    }

    [Fact]
    public void Les_24_rangs_sont_ceux_du_contenu_saisonnier()
    {
        SeasonBook book = SeasonJsonReader.ReadFile(SeasonJsonReader.PathIn(DataDirectory), Zone).Book!;
        Assert.Equal(VineLook.RowPrefix, book.Vine!.Rows);
        var rows = Zone.Setup.Space!.Obstacles.Where(o => VineLook.IsRow(o.Id)).ToList();
        Assert.Equal(24, rows.Count);
        Assert.False(VineLook.IsRow("haie_enceinte_ouest_vignes"));
        Assert.False(VineLook.IsRow(null));
    }

    [Theory]
    [InlineData(0, 0, 40, 0.8, 5.0, 9)]     // rang de 40 m : piquets tous les 5 m, bouts compris
    [InlineData(0, 0, 40, 0.8, 2.5, 17)]
    [InlineData(0, 0, 0.8, 12, 5.0, 3)]     // rang nord-sud
    [InlineData(0, 0, 3, 0.8, 5.0, 2)]      // plus court qu'un écart : les deux bouts
    public void Places_le_long_d_un_rang(double x0, double z0, double x1, double z1, double spacing, int count)
    {
        var spots = VineLook.Along(x0, z0, x1, z1, spacing);
        Assert.Equal(count, spots.Count);
        bool alongX = x1 - x0 >= z1 - z0;
        Assert.Equal(alongX ? x0 : z0, alongX ? spots[0].X : spots[0].Z, 9);
        Assert.Equal(alongX ? x1 : z1, alongX ? spots[^1].X : spots[^1].Z, 9);
        Assert.All(spots, s => Assert.Equal(alongX ? (z0 + z1) / 2 : (x0 + x1) / 2, alongX ? s.Z : s.X, 9));
        Assert.Throws<ArgumentOutOfRangeException>(() => VineLook.Along(0, 0, 1, 1, 0));
    }

    [Fact]
    public void Hotte_et_panier_au_bout_des_rangs_hors_des_obstacles_et_des_noeuds()
    {
        Place vines = Zone.Places.Single(p => p.Id == "vignes_du_chateau");
        var crafting = CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory),
            StoryJsonReader.ReadTextsFile(StoryJsonReader.TextsPathIn(DataDirectory)).Texts!, Zone,
            ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!).Book!;
        foreach (VintageProp prop in VineLook.Props)
        {
            Assert.InRange(prop.X, vines.Min.X.Raw / 1e4, vines.Max.X.Raw / 1e4);
            Assert.InRange(prop.Z, vines.Min.Z.Raw / 1e4, vines.Max.Z.Raw / 1e4);
            foreach (SpaceObstacle o in Zone.Setup.Space!.Obstacles)
            {
                double ox0 = o.Points.Min(p => p.X.Raw) / 1e4, ox1 = o.Points.Max(p => p.X.Raw) / 1e4, oz0 = o.Points.Min(p => p.Z.Raw) / 1e4, oz1 = o.Points.Max(p => p.Z.Raw) / 1e4;
                double dx = Math.Max(0, Math.Max(ox0 - prop.X, prop.X - ox1)), dz = Math.Max(0, Math.Max(oz0 - prop.Z, prop.Z - oz1));
                Assert.True(Math.Sqrt(dx * dx + dz * dz) >= 1.0, $"{prop.Key} trop près de {o.Id}");
            }
            foreach (var n in crafting.Nodes.Where(n => n.Id.StartsWith("raisin_", StringComparison.Ordinal)))
            {
                double nx = n.Position.X.Raw / 1e4 - prop.X, nz = n.Position.Z.Raw / 1e4 - prop.Z;
                Assert.True(Math.Sqrt(nx * nx + nz * nz) >= 2.0, $"{prop.Key} sur le nœud {n.Id}");
            }
        }
    }

    [Fact]
    public void Modeles_du_lot_C_relies_aux_vendanges()
    {
        Assert.Equal("C_RE_S06", LotCModels.ForNode("raisin_vignes_ouest"));
        Assert.Equal("C_RE_S06", LotCModels.ForNode("raisin_vignes_est"));
        Assert.Equal("C_RE_S07", LotCModels.ForVillage("vendanges/hotte", "vendanges/hotte"));
        Assert.Equal("C_RE_S08", LotCModels.ForVillage("vendanges/panier", "vendanges/panier"));
        Assert.All(new[] { "C_RE_S06", "C_RE_S07", "C_RE_S08" }, c => Assert.Null(LotCModels.Find(c)!.Pending));
        Assert.Equal(VineLook.Props.Select(p => p.Key).OrderBy(k => k), new[] { "vendanges/hotte", "vendanges/panier" });
    }
}
