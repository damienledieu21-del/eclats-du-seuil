using EdS.Runtime;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// V2.17 (décision de Damien du 9 novembre 2026 : « créer nous-mêmes les modèles 3D ») : modèles de l'atelier dédiés à un obstacle,
/// rangés dans « Assets/EdS/Modeles/Batiments/&lt;id&gt;/ » ; sans Unity.
/// </summary>
[Trait("Category", "World")]
public class V217AtelierTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v217_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    [Theory]
    [InlineData("Assets/EdS/Modeles/Batiments/maison2/boulangerie_firmin.fbx", "maison2")]
    [InlineData(@"Assets\EdS\Modeles\Batiments\maison2\boulangerie_firmin.fbx", "maison2")]
    [InlineData("assets/eds/MODELES/batiments/Maison2/Boulangerie.FBX", "maison2")]
    [InlineData("Modeles/Batiments/forge/forge.fbx", "forge")]
    [InlineData("Assets/EdS/Modeles/Batiments/maison2/boulangerie_firmin_couleur.png", null)]
    [InlineData("Assets/EdS/Modeles/Batiments/boulangerie_firmin.fbx", null)]
    [InlineData("Assets/EdS/Modeles/Batiments/maison2/sous/boulangerie.fbx", null)]
    [InlineData("Assets/Style/Sketchfab/Batiments/Maison/Olosta1/Olosta1.fbx", null)]
    [InlineData("Assets/EdS/Modeles/Batiments//x.fbx", null)]
    [InlineData("Assets/EdS/Modeles/Batiments/maison2/.fbx", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Le_dossier_dit_l_obstacle_du_modele_dedie(string? path, string? id) => Assert.Equal(id, TownModels.OwnBuildingOf(path));

    [Fact]
    public void Un_modele_dedie_n_a_pas_de_genre_sketchfab() =>
        Assert.Equal(StyleBuilding.None, TownModels.KindOfModel("Assets/EdS/Modeles/Batiments/maison2/boulangerie_firmin.fbx"));

    [Fact]
    public void La_boulangerie_de_firmin_tient_sur_maison2_porte_au_sud()
    {
        AdventureSession a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), Path.Combine(_folder, "partie.json"));
        a.Advance(0.05);
        AdventureMap map = a.Map;
        var o = map.Obstacles.FirstOrDefault(x => x.Id == "maison2");
        Assert.NotNull(o);
        double minX = o.Points.Min(p => p.X.Raw / 10000.0), maxX = o.Points.Max(p => p.X.Raw / 10000.0);
        double minZ = o.Points.Min(p => p.Z.Raw / 10000.0), maxZ = o.Points.Max(p => p.Z.Raw / 10000.0);
        (int X, int Z) facing = TownRules.FacingOf("maison2", minX, minZ, maxX, maxZ, map.Roads, map.Doors);
        Assert.Equal((0, -1), facing);
        ModelFit fit = TownModels.Fit(maxX - minX, maxZ - minZ, facing, 14.0, 10.6, TownModels.Overflow(StyleBuilding.House));
        Assert.True(fit.Fits);
        Assert.True(TownModels.Usable(fit));
    }

    [Theory]
    [InlineData(14.0, 10.6, true)]
    [InlineData(13.5, 10.2, true)]
    [InlineData(6.0, 5.0, false)]
    public void Le_pied_convenu_tient_sur_l_emprise_de_maison2(double modelX, double modelZ, bool usable)
    {
        ModelFit fit = TownModels.Fit(14.5, 11, (0, -1), modelX, modelZ, TownModels.Overflow(StyleBuilding.House));
        Assert.Equal(180, fit.Yaw, 6);
        Assert.Equal(usable, TownModels.Usable(fit));
        if (usable) Assert.True(fit.Fits);
    }

    /// <summary>Pied du modèle livré le 9 novembre 2026 (Blender : x −7,46 à 7,50, y −6,45 à 5,19).</summary>
    [Fact]
    public void Le_modele_livre_tient_sur_maison2()
    {
        ModelFit fit = TownModels.Fit(14.5, 11, (0, -1), 14.96, 11.64, TownModels.Overflow(StyleBuilding.House));
        Assert.True(fit.Fits);
        Assert.InRange(fit.ScaleX, 0.95, 1.1);
        Assert.InRange(fit.ScaleZ, 0.95, 1.1);
    }

    [Theory]
    [InlineData("boulangerie_firmin", true)]
    [InlineData("Boulangerie_Firmin", true)]
    [InlineData("Olosta1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void La_cheminee_n_est_connue_que_des_modeles_de_l_atelier(string? model, bool known) =>
        Assert.Equal(known, TownModels.OwnChimney(model).HasValue);

    [Fact]
    public void Le_point_du_modele_suit_la_pose_de_la_vue()
    {
        // Échelle 1, façade vers +z : seul le décalage du pied compte.
        ModelFit straight = TownModels.Fit(10, 10, (0, 1), 10.6, 10.6, 1.06);
        var p = TownModels.OwnPoint(100, 50, (0, 1), straight, 0.5, -0.2, 1, 5, 7.6, -2);
        Assert.Equal(104.5, p.X, 6);
        Assert.Equal(7.8, p.Y, 6);
        Assert.Equal(47, p.Z, 6);
        // Façade vers −z (porte au sud) : demi-tour, x et z changent de signe.
        ModelFit south = TownModels.Fit(10, 10, (0, -1), 10.6, 10.6, 1.06);
        var q = TownModels.OwnPoint(100, 50, (0, -1), south, 0, 0, 0, 5, 7.6, -2);
        Assert.Equal(95, q.X, 6);
        Assert.Equal(52, q.Z, 6);
        // Façade vers +x : le +z du modèle va vers +x, son +x vers −z (Quaternion.Euler(0, 90, 0)).
        ModelFit east = TownModels.Fit(10, 10, (1, 0), 10.6, 10.6, 1.06);
        var r = TownModels.OwnPoint(100, 50, (1, 0), east, 0, 0, 0, 5, 0, -2);
        Assert.Equal(98, r.X, 6);
        Assert.Equal(45, r.Z, 6);
    }

    [Fact]
    public void La_lumiere_de_porte_est_a_droite_de_la_porte_devant_la_facade()
    {
        // Porte au sud (façade vers −z), porte en x = 3 : vue de dehors (on regarde vers +z), la droite est +x.
        NightLamp south = TownModels.OwnDoorLantern("maison2", -7.25, -5.5, 7.25, 5.5, (0, -1), 3, -5.5);
        Assert.Equal(3.4, south.X, 6);
        Assert.Equal(-5.9, south.Z, 6);
        Assert.Equal(NightLamps.DoorLanternHeight, south.Y, 6);
        Assert.Equal("maison2/lanterne", south.Id);
        // Façade vers +x sans porte connue : milieu de la façade, la droite (on regarde vers −x) est +z.
        NightLamp east = TownModels.OwnDoorLantern("b", 0, 0, 10, 6, (1, 0), null, null);
        Assert.Equal(10.4, east.X, 6);
        Assert.Equal(3.4, east.Z, 6);
    }

    [Fact]
    public void L_enseigne_et_la_cheminee_de_la_boulangerie_disent_leur_maison()
    {
        AdventureSession a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), Path.Combine(_folder, "partie.json"));
        a.Advance(0.05);
        Assert.Equal("maison2", a.ShopBuildingOf("enseigne/merchant.boulangerie"));
        Assert.Equal("maison2", a.ShopBuildingOf("cheminee/merchant.boulangerie"));
        Assert.Contains(a.ShopSigns(), s => a.ShopBuildingOf(s.Id) == "maison2");
        Assert.Null(a.ShopBuildingOf("enseigne/inconnu"));
        Assert.Null(a.ShopBuildingOf(null));
    }
}
