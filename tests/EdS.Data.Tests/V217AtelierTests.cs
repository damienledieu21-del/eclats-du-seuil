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
}
