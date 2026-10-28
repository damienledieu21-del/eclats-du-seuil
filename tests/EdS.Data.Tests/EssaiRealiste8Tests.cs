using EdS.Runtime;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Essai réaliste 8 (demande de Damien du 28 octobre 2026 : « je veux que tu ajoutes les points 1 à 5 ») : modèles de bâtiments de
/// Sketchfab posés sur les emprises (genre d'après le dossier, choix stable, face vers la route, pied ajusté), villageois d'affichage du
/// bourg, couches « chemin » et « champ » du terrain ; sans Unity.
/// </summary>
[Trait("Category", "World")]
public class EssaiRealiste8Tests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_essai8_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private AdventureMap Map()
    {
        AdventureSession a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), Path.Combine(_folder, "partie.json"));
        a.Advance(0.05);
        return a.Map;
    }

    /// <summary>Pieds (m) des modèles livrés à Damien le 28 octobre 2026, mesurés dans Blender.</summary>
    private static readonly Dictionary<StyleBuilding, (double X, double Z)[]> Delivered = new()
    {
        [StyleBuilding.House] = new[] { (10.03, 10.05), (6.35, 8.3), (8.0, 7.6), (3.05, 4.47), (4.2, 9.87), (11.18, 4.9), (14.4, 7.41), (16.57, 12.54) },
        [StyleBuilding.Inn] = new[] { (7.8, 10.0) },
        [StyleBuilding.Chapel] = new[] { (10.34, 25.17) },
        [StyleBuilding.Barn] = new[] { (13.59, 7.11) },
        [StyleBuilding.Well] = new[] { (1.58, 1.49) },
        [StyleBuilding.Stall] = new[] { (3.26, 1.66), (4.75, 3.09), (3.14, 1.68) },
        [StyleBuilding.Tower] = new[] { (7.88, 8.24) },
        [StyleBuilding.Tent] = new[] { (7.69, 7.46) },
    };

    [Theory]
    [InlineData("Assets/Style/Sketchfab/Batiments/Maison/Olosta1/Olosta1.fbx", StyleBuilding.House)]
    [InlineData("Assets/Style/Sketchfab/Batiments/Auberge/Olosta5/Olosta5.fbx", StyleBuilding.Inn)]
    [InlineData("Assets/Style/Sketchfab/Batiments/Chapelle/Fagnolle/Fagnolle.fbx", StyleBuilding.Chapel)]
    [InlineData("Assets/Style/Sketchfab/Batiments/Grange/Grange/Grange.fbx", StyleBuilding.Barn)]
    [InlineData("Assets/Style/Sketchfab/Batiments/Puits/Puits/Puits.fbx", StyleBuilding.Well)]
    [InlineData(@"Assets\Style\Sketchfab\Bâtiments\Étal\Etal1\Etal1.fbx", StyleBuilding.None)]
    [InlineData(@"Assets\Style\Sketchfab\Batiments\Etal\Etal1\Etal1.fbx", StyleBuilding.Stall)]
    [InlineData("Assets/Style/Sketchfab/Batiments/Tour/Fougeres/Fougeres.fbx", StyleBuilding.Tower)]
    [InlineData("Assets/Style/Sketchfab/Batiments/Tente/Tente/Tente.fbx", StyleBuilding.Tent)]
    [InlineData("Assets/Style/Sketchfab/Batiments/Maison.fbx", StyleBuilding.None)]
    [InlineData("Assets/Style/Sketchfab/Windmill/Windmill anim+LOD.fbx", StyleBuilding.None)]
    [InlineData("Assets/Style/Sketchfab/Batiments/Moulin/Moulin/Moulin.fbx", StyleBuilding.None)]
    [InlineData("", StyleBuilding.None)]
    [InlineData(null, StyleBuilding.None)]
    public void Le_dossier_dit_le_genre_du_batiment(string? path, StyleBuilding kind) => Assert.Equal(kind, TownModels.KindOfModel(path!));

    [Fact]
    public void La_face_du_modele_est_tournee_vers_la_route()
    {
        Assert.Equal(0, TownModels.FacingYaw((0, 1)), 9);
        Assert.Equal(90, TownModels.FacingYaw((1, 0)), 9);
        Assert.Equal(180, TownModels.FacingYaw((0, -1)), 9);
        Assert.Equal(-90, TownModels.FacingYaw((-1, 0)), 9);
        // Tourné d'un quart de tour, le pied du modèle change d'axe : 10 × 4 m tient alors sur une emprise de 4 × 10 m.
        ModelFit along = TownModels.Fit(4, 10, (0, 1), 10, 4, 1);
        ModelFit turned = TownModels.Fit(4, 10, (1, 0), 10, 4, 1);
        Assert.False(along.Fits);
        Assert.True(turned.Fits);
        Assert.Equal(1, turned.ScaleX, 9);
        Assert.Equal(1, turned.ScaleZ, 9);
        Assert.Equal(1, turned.Fill, 9);
        Assert.Equal(90, turned.Yaw, 9);
    }

    [Fact]
    public void Le_pied_remplit_l_emprise_sans_etre_trop_etire_ni_trop_reduit()
    {
        // 8 × 6 m pour un pied de 8 × 8 m : x garde 1, z descend à 0,75 ; étirement 1,33 ramené à 1,25 (x passe à 0,9375).
        ModelFit fit = TownModels.Fit(8, 6, (0, 1), 8, 8, 1);
        Assert.Equal(0.75, fit.ScaleZ, 9);
        Assert.Equal(0.9375, fit.ScaleX, 9);
        Assert.Equal(Math.Sqrt(0.75 * 0.9375), fit.ScaleY, 9);
        Assert.Equal(8 * 0.9375 * 8 * 0.75 / 48, fit.Fill, 9);
        Assert.True(fit.Fits);
        Assert.InRange(fit.ScaleX / fit.ScaleZ, 1, TownModels.MaxStretch + 1e-9);
        // Une ferme de 16,6 m sur une emprise de 7 m devient trop petite (portes plus basses que les personnages) : refusée.
        Assert.False(TownModels.Fit(7, 7, (0, 1), 16.57, 12.54, 1.06).Fits);
        // Une tour de 3 m sur 8 m serait grossie de plus de 1,4 : refusée.
        Assert.False(TownModels.Fit(8, 8, (0, 1), 3, 3, 1).Fits);
        // Entrées absurdes : rien ne tient.
        Assert.False(TownModels.Fit(0, 6, (0, 1), 8, 8, 1).Fits);
        Assert.False(TownModels.Fit(8, 6, (0, 1), double.NaN, 8, 1).Fits);
        Assert.False(TownModels.Fit(8, 6, (0, 1), 8, 8, 0).Fits);
    }

    [Fact]
    public void Le_choix_du_modele_est_stable_et_prefere_la_route()
    {
        var feet = new List<(double X, double Z)> { (8, 6), (6, 8), (14, 7) };
        var first = TownModels.Pick("maison1", 8, 6, (0, 1), feet, 1.06);
        Assert.Equal(first, TownModels.Pick("maison1", 8, 6, (0, 1), feet, 1.06));
        Assert.Equal((0, 1), first.Facing);
        Assert.True(first.Fit.Fits);
        // Aucun modèle ne tient face à la route mais l'un tient de côté : il est tourné (une chapelle longue sur une emprise longue).
        var side = TownModels.Pick("chapelle", 8, 14, (1, 0), new List<(double X, double Z)> { (8, 14) }, 1.0);
        Assert.Equal(0, side.Model);
        Assert.NotEqual((1, 0), side.Facing);
        Assert.Equal(0, side.Facing.X);
        Assert.True(side.Fit.Fits);
        // Rien ne tient : le modèle qui couvre le mieux, même s'il est trop réduit.
        var best = TownModels.Pick("maison8", 6, 6, (0, 1), new List<(double X, double Z)> { (16.57, 12.54), (1, 1) }, 1.06);
        Assert.Equal(0, best.Model);
        Assert.False(best.Fit.Fits);
        Assert.Equal(-1, TownModels.Pick("maison1", 8, 6, (0, 1), new List<(double X, double Z)>(), 1.06).Model);
        Assert.Equal(-1, TownModels.Pick("maison1", 8, 6, (0, 1), null!, 1.06).Model);
    }

    [Fact]
    public void Chaque_batiment_des_Vallons_recoit_un_modele_livre_qui_tient()
    {
        AdventureMap map = Map();
        int houses = 0;
        var used = new HashSet<int>();
        foreach (var o in map.Obstacles)
        {
            StyleBuilding kind = TownRules.BuildingFor(o.Id);
            if (!Delivered.ContainsKey(kind)) continue;
            double x0 = o.Points.Min(p => p.X.Raw) / 10000.0, x1 = o.Points.Max(p => p.X.Raw) / 10000.0;
            double z0 = o.Points.Min(p => p.Z.Raw) / 10000.0, z1 = o.Points.Max(p => p.Z.Raw) / 10000.0;
            var pick = TownModels.Pick(o.Id, x1 - x0, z1 - z0, TownRules.Facing(x0, z0, x1, z1, map.Roads), Delivered[kind], TownModels.Overflow(kind));
            Assert.InRange(pick.Model, 0, Delivered[kind].Length - 1);
            // La chapelle de Fagnolle (25 m de long) est la seule à être réduite sous l'échelle admise : elle reste la chapelle.
            if (kind != StyleBuilding.Chapel) Assert.True(pick.Fit.Fits, $"{o.Id} : aucun modèle ne tient");
            Assert.InRange(pick.Fit.ScaleY, 0.55, TownModels.MaxScale);
            if (kind == StyleBuilding.House)
            {
                houses++;
                used.Add(pick.Model);
            }
        }
        Assert.Equal(10, houses);
        Assert.True(used.Count >= 3, "les dix maisons doivent varier");
    }

    [Fact]
    public void Les_villageois_restent_hors_des_obstacles_et_loin_des_PNJ()
    {
        AdventureMap map = Map();
        var fields = map.Places.Where(p => TownRules.Farmland(p.Id)).SelectMany(p => TownRules.Fields(p.MinX, p.MinZ, p.MaxX, p.MaxZ,
            map.Obstacles.Select(o => (o.Points.Min(q => q.X.Raw) / 10000.0, o.Points.Min(q => q.Z.Raw) / 10000.0, o.Points.Max(q => q.X.Raw) / 10000.0,
                o.Points.Max(q => q.Z.Raw) / 10000.0)).ToList(), map.Roads, map.Clearings, (map.MinX, map.MinZ, map.MaxX, map.MaxZ))).ToList();
        IReadOnlyList<StyleVillager> villagers = TownModels.Villagers(map, fields, 30);
        Assert.InRange(villagers.Count, 8, 30);
        Assert.Equal(villagers.Count, villagers.Select(v => v.Id).Distinct().Count());
        Assert.All(villagers, v => Assert.Contains(v.Character, new[] { "paysan", "garde", "sentinelle" }));
        Assert.Contains(villagers, v => v.Activity == VillagerActivity.Talk);
        Assert.Contains(villagers, v => v.Activity == VillagerActivity.Look);
        Assert.Contains(villagers, v => v.Activity == VillagerActivity.Gather);
        Assert.Single(villagers, v => v.Walks);
        var polygons = map.Obstacles.Select(o => o.Points.Select(p => (p.X.Raw / 10000.0, p.Z.Raw / 10000.0)).ToList()).ToList();
        void Clear(double x, double z, string what)
        {
            Assert.InRange(x, map.MinX, map.MaxX);
            Assert.InRange(z, map.MinZ, map.MaxZ);
            foreach (var polygon in polygons)
                Assert.True(StyleRules.InsideDistance(x, z, polygon) <= -TownModels.VillagerMargin + 1e-9, $"{what} dans un obstacle");
            foreach ((double cx, double cz) in map.Clearings)
                Assert.True(Math.Sqrt((cx - x) * (cx - x) + (cz - z) * (cz - z)) >= TownModels.ClearingMargin - 1e-9, $"{what} trop près d'un repère");
        }
        foreach (StyleVillager v in villagers)
        {
            Clear(v.X, v.Z, v.Id);
            foreach (StyleVillager other in villagers.Where(o => o.Id != v.Id))
                Assert.True(Math.Sqrt((other.X - v.X) * (other.X - v.X) + (other.Z - v.Z) * (other.Z - v.Z)) >= TownModels.VillagerSpacing - 1e-9);
            if (!v.Walks) continue;
            for (int i = 0; i <= 100; i++) Clear(v.X + (v.ToX - v.X) * i / 100.0, v.Z + (v.ToZ - v.Z) * i / 100.0, v.Id + " en marchant");
        }
        // Toujours les mêmes ; plafonnés ; aucun sans place.
        Assert.Equal(villagers.Select(v => (v.Id, v.X, v.Z)), TownModels.Villagers(map, fields, 30).Select(v => (v.Id, v.X, v.Z)));
        Assert.Equal(3, TownModels.Villagers(map, fields, 3).Count);
        Assert.Empty(TownModels.Villagers(map, fields, 0));
        Assert.Empty(TownModels.Villagers(null!, fields, 10));
    }

    [Fact]
    public void L_animation_du_villageois_suit_son_activite()
    {
        string[] paysan = { "Paysan Idle", "Paysan Pick Fruit Idle", "Paysan Pull Plant Idle", "Paysan Talking Idle", "Paysan Walk" };
        Assert.Equal("Paysan Talking Idle", TownModels.VillagerClip(paysan, VillagerActivity.Talk, 0));
        Assert.Equal("Paysan Walk", TownModels.VillagerClip(paysan, VillagerActivity.Walk, 5));
        Assert.Equal("Paysan Idle", TownModels.VillagerClip(paysan, VillagerActivity.Idle, 0));
        Assert.Equal("Paysan Pick Fruit Idle", TownModels.VillagerClip(paysan, VillagerActivity.Gather, 0));
        Assert.Equal("Paysan Pull Plant Idle", TownModels.VillagerClip(paysan, VillagerActivity.Gather, 1));
        // Sans l'animation de l'activité : l'attente la plus simple.
        Assert.Equal("Paysan Idle", TownModels.VillagerClip(paysan, VillagerActivity.Look, 0));
        string[] garde = { "Garde Idle", "Garde Looking Around Idle", "Garde Walk" };
        Assert.Equal("Garde Looking Around Idle", TownModels.VillagerClip(garde, VillagerActivity.Look, 3));
        Assert.Equal("Garde Idle", TownModels.VillagerClip(garde, VillagerActivity.Talk, 0));
        Assert.Null(TownModels.VillagerClip(new string[0], VillagerActivity.Idle, 0));
        Assert.Null(TownModels.VillagerClip(null!, VillagerActivity.Idle, 0));
        // Les animations des villageois tournent en boucle (« Idle », « Walk » dans leur nom).
        Assert.All(paysan.Concat(garde), c => Assert.True(StyleRules.Loops(c), c));
    }

    [Fact]
    public void Chemins_et_champs_ont_leur_couche_du_sol()
    {
        Assert.Equal(StyleGround.Road, StyleRules.GroundOf("Assets/Style/AmbientCG/Ground/chemin_terre_battue_diff_2k.jpg"));
        Assert.Equal(StyleGround.Road, StyleRules.GroundOf("Assets/Style/AmbientCG/Ground/road_dirt_diff_2k.jpg"));
        Assert.Equal(StyleGround.Field, StyleRules.GroundOf("Assets/Style/AmbientCG/Ground/champ_labour_nor_gl_2k.jpg"));
        Assert.Equal(StyleGround.Field, StyleRules.GroundOf("Assets/Style/Ground/ploughed_field_diff_1k.jpg"));
        // Les textures d'avant gardent leur couche (la roche du sentier n'est pas un chemin).
        Assert.Equal(StyleGround.Rock, StyleRules.GroundOf("Assets/Style/PolyHaven2k/Ground/rocky_trail_diff_2k.jpg"));
        Assert.Equal(StyleGround.Soil, StyleRules.GroundOf("Assets/Style/PolyHaven2k/Ground/brown_mud_leaves_01_diff_2k.jpg"));
        Assert.Null(StyleRules.GroundOf("Assets/Style/AmbientCG/Surfaces/chemin_diff_2k.jpg"));
        var textures = new[]
        {
            "Assets/Style/AmbientCG/Ground/champ_labour_diff_2k.jpg", "Assets/Style/AmbientCG/Ground/champ_labour_nor_gl_2k.jpg",
            "Assets/Style/AmbientCG/Ground/chemin_terre_battue_diff_2k.jpg", "Assets/Style/AmbientCG/Ground/chemin_terre_battue_nor_gl_2k.jpg",
            "Assets/Style/PolyHaven2k/Ground/brown_mud_leaves_01_diff_2k.jpg",
        };
        Assert.Equal(textures[2], StyleRules.GroundTexture(textures, StyleGround.Road, false));
        Assert.Equal(textures[3], StyleRules.GroundTexture(textures, StyleGround.Road, true));
        Assert.Equal(textures[0], StyleRules.GroundTexture(textures, StyleGround.Field, false));
        // La terre reste celle de Poly Haven : les nouvelles textures ne la prennent pas.
        Assert.Equal(textures[4], StyleRules.GroundTexture(textures, StyleGround.Soil, false));
        // Les pavés d'ambientCG sont des pavés.
        Assert.Equal(StyleSurface.Cobble, StyleRules.SurfaceOf("Assets/Style/AmbientCG/Surfaces/paving_stones_142_diff_2k.jpg"));
        var mix = new StyleMix(0.2, 0.3, 0.5);
        Assert.Equal(0, mix[StyleGround.Road]);
        Assert.Equal(0, mix[StyleGround.Field]);
        Assert.Equal(0.5, mix[StyleGround.Grass]);
    }

    [Fact]
    public void La_part_du_chemin_et_de_la_berge_est_celle_d_avant()
    {
        var road = new StyleRoad("r", new List<(double X, double Z)> { (0, 0), (20, 0) }, 4, false);
        var roads = new List<StyleRoad> { road };
        var mix = new StyleMix(0.1, 0.2, 0.7);
        for (double z = -5; z <= 5; z += 0.37)
            for (double x = -2; x <= 22; x += 1.3)
            {
                RoadHit hit = TownRules.RoadAt(x, z, roads);
                double w = TownRules.RoadWeight(x, z, hit);
                Assert.InRange(w, 0, 1);
                StyleMix m = TownRules.RoadMix(mix, x, z, hit);
                Assert.Equal(mix.Soil * (1 - w) + w, m.Soil, 12);
                Assert.Equal(mix.Grass * (1 - w), m.Grass, 12);
            }
        Assert.Equal(1, TownRules.RoadWeight(10, 0, TownRules.RoadAt(10, 0, roads)), 9);
        Assert.Equal(0, TownRules.RoadWeight(10, 9, TownRules.RoadAt(10, 9, roads)), 9);
        for (double inside = -5; inside <= 5; inside += 0.5)
        {
            double wet = StyleRules.RiverWet(inside);
            Assert.Equal(mix.Soil * (1 - wet) + wet, StyleRules.RiverMix(mix, inside).Soil, 12);
        }
    }
}
