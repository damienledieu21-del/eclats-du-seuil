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
    public void Les_effets_de_combat_viennent_des_packs_par_leur_nom()
    {
        // Noms des prefabs livrés par les trois packs (contenu affiché sur l'Asset Store le 28 octobre 2026).
        string[] prefabs =
        {
            "Assets/Matthew Guz/Hits Effects FREE/Prefab/Basic Hit .prefab",
            "Assets/Matthew Guz/Hits Effects FREE/Prefab/Basic Hit 2.prefab",
            "Assets/Matthew Guz/Hits Effects FREE/Prefab/1.2/Basic Hit 8 (NEW).prefab",
            "Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Holy hit.prefab",
            "Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Red energy explosion.prefab",
            "Assets/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks explode white.prefab",
            "Assets/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks flashing yellow.prefab",
            "Assets/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle.prefab",
            "Assets/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Healing circle.prefab",
            "Assets/Hovl Studio/Magic effects pack/Prefabs/Character auras/Healing.prefab",
            "Assets/Hovl Studio/Magic effects pack/Prefabs/Character auras/Buff.prefab",
            "Assets/Hovl Studio/Magic effects pack/Prefabs/Smoke effects/Dust puff.prefab",
            "Assets/Hovl Studio/Magic effects pack/Prefabs/Smoke effects/Dust ground.prefab",
            "Assets/Free Slash VFX/Prefabs/Slash VFX.prefab",
            "Assets/Free Slash VFX/Prefabs/Slash Fire VFX.prefab",
            "Assets/Free Slash VFX/Model/Slash/Slash .fbx",
        };
        Assert.Equal(prefabs[1], StyleRules.VfxPrefab(prefabs, StyleVfx.Hit));
        Assert.Equal(prefabs[2], StyleRules.VfxPrefab(prefabs, StyleVfx.Crit));
        Assert.Equal(prefabs[5], StyleRules.VfxPrefab(prefabs, StyleVfx.Block));
        Assert.Equal(prefabs[3], StyleRules.VfxPrefab(prefabs, StyleVfx.Parry));
        Assert.Equal(prefabs[4], StyleRules.VfxPrefab(prefabs, StyleVfx.GuardBroken));
        Assert.Equal(prefabs[13], StyleRules.VfxPrefab(prefabs, StyleVfx.Slash));
        Assert.Equal(prefabs[7], StyleRules.VfxPrefab(prefabs, StyleVfx.Cast));
        Assert.Equal(prefabs[9], StyleRules.VfxPrefab(prefabs, StyleVfx.Heal));
        Assert.Equal(prefabs[10], StyleRules.VfxPrefab(prefabs, StyleVfx.LevelUp));
        Assert.Equal(prefabs[11], StyleRules.VfxPrefab(prefabs, StyleVfx.Death));
        Assert.Equal(prefabs[12], StyleRules.VfxPrefab(prefabs, StyleVfx.Slam));
        Assert.Equal(prefabs[6], StyleRules.VfxPrefab(prefabs, StyleVfx.Charge));
        // Sans le premier choix, le suivant ; rien d'autre qu'un prefab ; rien sans pack.
        Assert.Equal(prefabs[0], StyleRules.VfxPrefab(prefabs.Where(p => !p.Contains("Hit 2")), StyleVfx.Hit));
        Assert.Null(StyleRules.VfxPrefab(new[] { "Assets/Free Slash VFX/Model/Slash/Slash VFX.fbx" }, StyleVfx.Slash));
        Assert.Null(StyleRules.VfxPrefab(Array.Empty<string>(), StyleVfx.Hit));
        Assert.Null(StyleRules.VfxPrefab(null!, StyleVfx.Hit));
        Assert.All(Enum.GetValues<StyleVfx>(), kind => Assert.NotNull(StyleRules.VfxPrefab(prefabs, kind)));
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

    /// <summary>Prefabs importés chez Damien le 28 octobre 2026 (Viking Village, sa végétation du Book of the Dead, un pack d'effets).</summary>
    private static readonly string[] SamplePrefabs =
    {
        "Assets/Viking Village/Prefabs/Props/pf_boulder_01.prefab",
        "Assets/Viking Village/Prefabs/Props/pf_boulder_01_040.prefab",
        "Assets/Viking Village/Prefabs/Props/pf_boulder_02_025.prefab",
        "Assets/Viking Village/Prefabs/Props/pf_menhir_01.prefab",
        "Assets/Viking Village/Prefabs/Props/pf_pillar_03.prefab",
        "Assets/Viking Village/Prefabs/Props/pf_barrel_01.prefab",
        "Assets/Viking Village/Prefabs/Buildings/pf_build_small_house_01.prefab",
        "Assets/Viking Village/Book of the Dead/Vegetation/Ferns/Fern_var01_Prefab.prefab",
        "Assets/Viking Village/Book of the Dead/Vegetation/MeadowGrass_01/Meadow_Grass_01_Var1_Prefab.prefab",
        "Assets/Viking Village/Book of the Dead/Vegetation/Plant_Perennials/PH_Plant_Perennials_a4_1x1x0_PF.prefab",
        "Assets/Viking Village/Book of the Dead/Vegetation/GreenBush/GreenBush_Var01_Prefab.prefab",
        "Assets/Viking Village/Book of the Dead/Vegetation/BroadleafShrub_01/Broadleaf_Shrub_01_Var4_Prefab.prefab",
        "Assets/Viking Village/Book of the Dead/Vegetation/Ferns/Fern_var01.FBX",
        "Assets/TerrainSampleAssets/Prefabs/Vegetation/BushDry.prefab",
        "Assets/Matthew Guz/Hits Effects FREE/Prefab/Stones hit.prefab",
        "Assets/Style/Generated/oak (arbre).prefab",
        "Assets/Style/PolyHaven/rock_moss_set_01.prefab",
        "Assets/Samples/Shader Graph/17.3.0/Production Ready Shaders/Prefabs/PlatformRock.prefab",
        "Assets/Samples/Shader Graph/17.3.0/Production Ready Shaders/Prefabs/TutorialStep5AddRocks.prefab",
        "Assets/Samples/Shader Graph/17.3.0/Production Ready Shaders/Prefabs/PlatformDetails.prefab",
    };

    [Fact]
    public void Les_rochers_et_la_vegetation_des_samples_sont_reconnus()
    {
        Assert.Equal(new[]
        {
            "Assets/Viking Village/Prefabs/Props/pf_boulder_01.prefab",
            "Assets/Viking Village/Prefabs/Props/pf_boulder_01_040.prefab",
            "Assets/Viking Village/Prefabs/Props/pf_boulder_02_025.prefab",
        }, StyleRules.SampleDecor(SamplePrefabs, StyleSlot.Rock, 6));
        Assert.Equal(new[]
        {
            "Assets/Viking Village/Book of the Dead/Vegetation/Ferns/Fern_var01_Prefab.prefab",
            "Assets/Viking Village/Book of the Dead/Vegetation/MeadowGrass_01/Meadow_Grass_01_Var1_Prefab.prefab",
            "Assets/Viking Village/Book of the Dead/Vegetation/Plant_Perennials/PH_Plant_Perennials_a4_1x1x0_PF.prefab",
        }, StyleRules.SampleDecor(SamplePrefabs, StyleSlot.Plant, 12));
        // Le buisson sec du Terrain Sample n'est pas une haie.
        Assert.Equal(new[]
        {
            "Assets/Viking Village/Book of the Dead/Vegetation/BroadleafShrub_01/Broadleaf_Shrub_01_Var4_Prefab.prefab",
            "Assets/Viking Village/Book of the Dead/Vegetation/GreenBush/GreenBush_Var01_Prefab.prefab",
        }, StyleRules.SampleDecor(SamplePrefabs, StyleSlot.Shrub, 4));
        Assert.Single(StyleRules.SampleDecor(SamplePrefabs, StyleSlot.Rock, 1));
        Assert.Empty(StyleRules.SampleDecor(SamplePrefabs, StyleSlot.Tree, 6));
        Assert.Empty(StyleRules.SampleDecor(null!, StyleSlot.Rock, 6));
        Assert.True(StyleRules.IsSample("Assets/TerrainSampleAssets/Prefabs/Vegetation/BushDry.prefab"));
        Assert.True(StyleRules.IsSample("Assets/Samples/Shader Graph/17.3.0/Production Ready Shaders/Water/Stream.mat"));
        Assert.False(StyleRules.IsSample("Assets/Style/PolyHaven/rock_moss_set_01.prefab"));
        // Le nom du fichier ne suffit pas : il faut le dossier d'un sample.
        Assert.False(StyleRules.IsSample("Viking Village.prefab"));
    }

    [Fact]
    public void Les_samples_donnent_leurs_plantes_chacun_leur_tour()
    {
        var paths = new[]
        {
            "Assets/TerrainSampleAssets/Prefabs/Fern_A.prefab",
            "Assets/TerrainSampleAssets/Prefabs/Fern_B.prefab",
            "Assets/TerrainSampleAssets/Prefabs/Grass_A.prefab",
            "Assets/TerrainSampleAssets/Prefabs/Bush_A.prefab",
            "Assets/Viking Village/Book of the Dead/Vegetation/Ferns/Fern_var01_Prefab.prefab",
            "Assets/Viking Village/Book of the Dead/Vegetation/Ferns/Fern_var02_Prefab.prefab",
        };
        Assert.Equal(new[]
        {
            "Assets/TerrainSampleAssets/Prefabs/Fern_A.prefab",
            "Assets/Viking Village/Book of the Dead/Vegetation/Ferns/Fern_var01_Prefab.prefab",
            "Assets/TerrainSampleAssets/Prefabs/Fern_B.prefab",
            "Assets/Viking Village/Book of the Dead/Vegetation/Ferns/Fern_var02_Prefab.prefab",
            "Assets/TerrainSampleAssets/Prefabs/Grass_A.prefab",
        }, StyleRules.SampleDecor(paths, StyleSlot.Plant, 12));
        Assert.Equal(paths.Take(1).Concat(paths.Skip(4).Take(1)), StyleRules.SampleDecor(paths, StyleSlot.Plant, 2));
        Assert.Equal(new[] { "Assets/TerrainSampleAssets/Prefabs/Bush_A.prefab" }, StyleRules.SampleDecor(paths, StyleSlot.Shrub, 4));
    }

    [Fact]
    public void Le_sol_prend_les_couches_de_terrain_du_terrain_sample()
    {
        string[] layers = new[] { "Black_Sand", "Grass_A", "Grass_B", "Grass_Dry", "Grass_Moss", "Grass_Soil", "Heather", "Muddy", "Pebbles_A", "Rock", "Sand", "Snow", "Soil_Rocks" }
            .Select(n => "Assets/TerrainSampleAssets/TerrainLayers/" + n + "_TerrainLayer.terrainlayer").ToArray();
        Assert.EndsWith("/Grass_A_TerrainLayer.terrainlayer", StyleRules.SampleGround(layers, StyleGround.Grass));
        Assert.EndsWith("/Muddy_TerrainLayer.terrainlayer", StyleRules.SampleGround(layers, StyleGround.Soil));
        Assert.EndsWith("/Rock_TerrainLayer.terrainlayer", StyleRules.SampleGround(layers, StyleGround.Rock));
        // Sans l'herbe verte ni la boue : la mousse, puis la terre caillouteuse ; la roche n'est jamais la terre caillouteuse.
        string[] fewer = layers.Where(l => !l.Contains("/Grass_A_") && !l.Contains("/Muddy_") && !l.Contains("/Rock_")).ToArray();
        Assert.EndsWith("/Grass_Moss_TerrainLayer.terrainlayer", StyleRules.SampleGround(fewer, StyleGround.Grass));
        Assert.EndsWith("/Soil_Rocks_TerrainLayer.terrainlayer", StyleRules.SampleGround(fewer, StyleGround.Soil));
        Assert.Null(StyleRules.SampleGround(fewer, StyleGround.Rock));
        // Les chemins et les champs gardent leurs textures ; une couche hors d'un sample ne compte pas.
        Assert.Null(StyleRules.SampleGround(layers, StyleGround.Road));
        Assert.Null(StyleRules.SampleGround(new[] { "Assets/Style/Generated/Rock_TerrainLayer.terrainlayer" }, StyleGround.Rock));
        Assert.Null(StyleRules.SampleGround(null!, StyleGround.Grass));
    }

    [Fact]
    public void L_eau_est_celle_du_lac_des_production_ready_shaders()
    {
        const string Water = "Assets/Samples/Shader Graph/17.3.0/Production Ready Shaders/Environment/Water/";
        var materials = new[]
        {
            Water + "Water.mat", Water + "WaterMuseum.mat", Water + "WaterStream.mat", Water + "WaterStreamFalls.mat", Water + "WaterLake.mat",
            "Assets/Viking Village/Materials/Effects/mat_fx_water_01.mat", "Assets/Style/Generated/Eau.mat",
        };
        Assert.Equal(Water + "WaterLake.mat", StyleRules.SampleWater(materials));
        // Sans le lac, l'eau « EdS/Eau » reste (le ruisseau coulerait de travers sur la surface de la rivière).
        Assert.Null(StyleRules.SampleWater(materials.Where(m => !m.EndsWith("WaterLake.mat"))));
        Assert.Null(StyleRules.SampleWater(new[] { "Assets/Style/WaterLake.mat" }));
        Assert.Null(StyleRules.SampleWater(null!));
    }

    [Fact]
    public void Les_particules_de_l_ancien_rendu_passent_sur_urp_comme_avec_le_convertisseur()
    {
        // Tous les matériaux de Magic Effects FREE sont en « fondu » (_Mode 2) : mélange alpha d'URP.
        ParticleBlend fade = ParticleRules.Blend(2);
        Assert.Equal((1, 0, false), (fade.Surface, fade.Blend, fade.AlphaClip));
        Assert.Equal((5, 10, 1, 10), (fade.Src, fade.Dst, fade.SrcAlpha, fade.DstAlpha));
        Assert.False(fade.ZWrite);
        Assert.Equal(ParticleRules.TransparentQueue, fade.Queue);
        Assert.Null(fade.Keyword);
        ParticleBlend opaque = ParticleRules.Blend(0);
        Assert.Equal((0, 1, 0, true, ParticleRules.OpaqueQueue), (opaque.Surface, opaque.Src, opaque.Dst, opaque.ZWrite, opaque.Queue));
        ParticleBlend cutout = ParticleRules.Blend(1);
        Assert.True(cutout.AlphaClip);
        Assert.Equal("_ALPHATEST_ON", cutout.Keyword);
        Assert.Equal(ParticleRules.CutoutQueue, cutout.Queue);
        Assert.Equal((1, "_ALPHAPREMULTIPLY_ON"), (ParticleRules.Blend(3).Blend, ParticleRules.Blend(3).Keyword));
        Assert.Equal((2, 5, 1), (ParticleRules.Blend(4).Blend, ParticleRules.Blend(4).Src, ParticleRules.Blend(4).Dst));
        Assert.Equal((3, 2, 0, "_ALPHAMODULATE_ON"), (ParticleRules.Blend(6).Blend, ParticleRules.Blend(6).Src, ParticleRules.Blend(6).Dst, ParticleRules.Blend(6).Keyword));
        // URP n'a pas de mélange soustractif ; un mode inconnu est un fondu.
        Assert.Equal(0, ParticleRules.Blend(5).Blend);
        Assert.Equal(0, ParticleRules.Blend(42).Blend);

        Assert.True(ParticleRules.IsBuiltInParticle("Particles/Standard Unlit"));
        Assert.True(ParticleRules.IsBuiltInParticle("Legacy Shaders/Particles/Additive"));
        Assert.True(ParticleRules.IsBuiltInParticle("Mobile/Particles/Alpha Blended"));
        Assert.False(ParticleRules.IsBuiltInParticle("Universal Render Pipeline/Particles/Unlit"));
        Assert.False(ParticleRules.IsBuiltInParticle("Shader Graphs/Slash"));
        Assert.False(ParticleRules.IsBuiltInParticle(null!));
        Assert.Equal("Universal Render Pipeline/Particles/Lit", ParticleRules.UrpShader("Particles/Standard Surface"));
        Assert.Equal("Universal Render Pipeline/Particles/Unlit", ParticleRules.UrpShader("Particles/Standard Unlit"));

        Assert.Equal(4, ParticleRules.LegacyMode("Legacy Shaders/Particles/Additive (Soft)"));
        Assert.Equal(3, ParticleRules.LegacyMode("Legacy Shaders/Particles/Alpha Blended Premultiply"));
        Assert.Equal(6, ParticleRules.LegacyMode("Legacy Shaders/Particles/Multiply"));
        Assert.Equal(2, ParticleRules.LegacyMode("Legacy Shaders/Particles/Alpha Blended"));

        Assert.Equal((null, 0f, 0f), ParticleRules.ColorMode(0));
        Assert.Equal(("_COLORADDSUBDIFF_ON", 1f, 0f), ParticleRules.ColorMode(1));
        Assert.Equal(("_COLORADDSUBDIFF_ON", -1f, 0f), ParticleRules.ColorMode(2));
        Assert.Equal(("_COLOROVERLAY_ON", 0f, 0f), ParticleRules.ColorMode(3));
        Assert.Equal(("_COLORCOLOR_ON", 0f, 0f), ParticleRules.ColorMode(4));
        Assert.Equal(("_COLORADDSUBDIFF_ON", -1f, 1f), ParticleRules.ColorMode(5));
    }

    [Fact]
    public void Les_samples_passent_devant_poly_haven_qui_complete()
    {
        var samples = new[] { "a.prefab", "b.prefab" };
        var others = new[] { "b.prefab", "c.fbx", "d.fbx" };
        Assert.Equal(new[] { "a.prefab", "b.prefab", "c.fbx" }, StyleRules.MergeDecor(samples, others, 3));
        Assert.Equal(new[] { "a.prefab", "b.prefab", "c.fbx", "d.fbx" }, StyleRules.MergeDecor(samples, others, 10));
        Assert.Equal(others, StyleRules.MergeDecor(Array.Empty<string>(), others, 10));
        Assert.Empty(StyleRules.MergeDecor(samples, others, 0));
        Assert.Equal(samples, StyleRules.MergeDecor(samples, null!, 5));
    }
}
