using EdS.Data.Zones;
using static EdS.Data.Tests.GrandeCarteLieux;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lots V2.31 (vallée de la rivière et cascade) et V2.32 (forêt des Loups et arbre millénaire) de la grande carte : bassin et rochers
/// au pied de la cascade, moulin à foulon et cabane du pêcheur sur la rive est, pont suspendu au-dessus du gué nord (le passage reste
/// celui du gué), sentier et point sûr du pied de la cascade ; arbre millénaire et cercle de druides, tanière de la meute, cabane du
/// bûcheron, souches et troncs couchés, sentier et point sûr de l'arbre (rive ouest, fermée jusqu'à la fin du chapitre 1).
/// </summary>
[Trait("Category", "World")]
public class V231V232ValleeForetTests
{
    [Fact]
    public void Obstacles_dans_leurs_lieux_routes_et_points_surs_degages()
    {
        InPlaces("_cascade", "vallee_de_la_riviere", "cascade");
        InPlaces("_foret", "foret_des_loups_profonde");
        RoadsClear("_cascade");
        RoadsClear("_foret");
        SafePointClear("pied_de_la_cascade");
        SafePointClear("arbre_millenaire");
        ZoneDefinition zone = Vallons.Value;
        foreach (string id in new[] { "cascade", "moulin_a_foulon", "cabane_du_pecheur", "pont_suspendu" })
            Assert.Equal(10, zone.Places.Single(p => p.Id == id).MinLevel);
        foreach (string id in new[] { "arbre_millenaire", "taniere_de_la_meute", "cabane_du_bucheron", "lisiere_de_la_foret" })
            Assert.Equal(15, zone.Places.Single(p => p.Id == id).MinLevel);
        Assert.Equal(9, Tagged("bloc_cercle_des_druides").Count);
    }

    [Fact]
    public void Cascade_rive_est_ouverte_depuis_le_gue_de_la_vallee()
    {
        // La rive est n'est pas fermée par le verrou du chapitre 1 : du point sûr du gué au pied de la cascade, à pied, gués fermés.
        Assert.True(Walks((300, 580), (700, 1100), SafePoint("gue_de_la_vallee"), SafePoint("pied_de_la_cascade")));
        // Le moulin et la cabane se contournent : on atteint la berge entre eux et la rivière.
        Assert.True(Walks((300, 580), (700, 1100), SafePoint("pied_de_la_cascade"), (586 + 12, 1067 - 6)));
    }

    [Fact]
    public void Pont_suspendu_le_gue_nord_reste_le_passage()
    {
        // Gué fermé : la rive ouest ne s'atteint pas par le pont ; gué ouvert : les poteaux laissent passer.
        (double X, double Z) min = (420, 860), max = (600, 1020);
        Assert.False(Walks(min, max, (540, 920), (470, 960), 0.5));
        Assert.True(Walks(min, max, (540, 920), (470, 960), 0.5, "riviere_vallee_gue_nord"));
    }

    [Fact]
    public void Arbre_millenaire_dans_la_rive_ouest_fermee_et_cercle_ouvert()
    {
        var zone = Vallons.Value;
        var obstacles = zone.Setup.Space!.Obstacles;
        (double X, double Z) min = (zone.Region!.Min.X.Raw / 1e4, zone.Region.Min.Z.Raw / 1e4), max = (zone.Region.Max.X.Raw / 1e4, zone.Region.Max.Z.Raw / 1e4);
        var locked = V228cSurfaceTests.Flood(obstacles, min, max, (-20, 300), 2);
        Assert.False(locked.Edge);
        Assert.True(V228cSurfaceTests.Reaches(locked.Reached, min, SafePoint("arbre_millenaire"), 2));
        Assert.False(V228cSurfaceTests.Reaches(locked.Reached, min, SafePoint("pied_de_la_cascade"), 2));
        // Dans le cercle, au pied de l'arbre, par l'ouverture du sud-est.
        Assert.True(Walks((-900, 730), (-800, 830), SafePoint("arbre_millenaire"), (-850 + 7, 780 - 3), 0.5));
    }
}
