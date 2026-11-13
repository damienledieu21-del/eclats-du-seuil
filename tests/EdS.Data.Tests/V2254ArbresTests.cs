using EdS.Domain.Space;
using EdS.Runtime;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// V2.25.4 (« arbres de l'atelier ») : les arbres faits à l'atelier remplacent ceux téléchargés. Placement déterministe (tiré de la
/// place), jamais sur une route, un point sûr, une porte ou un obstacle bâti, essence selon le lieu, LOD et imposteur câblés dans la vue,
/// saisons, imposteur sans allocation, exports livrés (sans LOD0) et taille ajoutée. Vraies données des Vallons ; zone et moteur
/// inchangés. Sans Unity.
/// </summary>
[Trait("Category", "World")]
public class V2254ArbresTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v2254_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static string Trees => Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Modeles", "Arbres");

    private static string Game => Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game");

    private static string Editor => Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Editor");

    private AdventureMap Vallons()
    {
        AdventureSession a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), Path.Combine(_folder, "partie.json"));
        a.Advance(0.05);
        return a.Map;
    }

    private static (double MinX, double MinZ, double MaxX, double MaxZ) Box(SpaceObstacle o) =>
        (o.Points.Min(p => p.X.Raw / 10000.0), o.Points.Min(p => p.Z.Raw / 10000.0), o.Points.Max(p => p.X.Raw / 10000.0), o.Points.Max(p => p.Z.Raw / 10000.0));

    private static (double X, double Z) Center(SpaceObstacle o)
    {
        var b = Box(o);
        return ((b.MinX + b.MaxX) / 2, (b.MinZ + b.MaxZ) / 2);
    }

    private static bool Inside((double MinX, double MinZ, double MaxX, double MaxZ) b, double x, double z, double margin = 0) =>
        x > b.MinX - margin && x < b.MaxX + margin && z > b.MinZ - margin && z < b.MaxZ + margin;

    private static string Body(string file, string start, string end)
    {
        string text = File.ReadAllText(file);
        int a = text.IndexOf(start, StringComparison.Ordinal), b = text.IndexOf(end, a + 1, StringComparison.Ordinal);
        Assert.True(a >= 0 && b > a, start);
        return text.Substring(a, b - a);
    }

    // --- Placement déterministe ----------------------------------------------------------------------------------------------------

    [Fact]
    public void Le_placement_est_deterministe_et_tire_de_la_place()
    {
        AdventureMap map = Vallons();
        var first = AtelierTrees.DecorTrees(map, map.Obstacles);
        var second = AtelierTrees.DecorTrees(Vallons(), Vallons().Obstacles);
        Assert.Equal(first.Select(s => (s.Model, s.X, s.Z, s.Yaw, s.Scale, s.Source)), second.Select(s => (s.Model, s.X, s.Z, s.Yaw, s.Scale, s.Source)));
        // Empreinte entière fixée : la même sur toutes les machines (aucun hasard, aucune horloge).
        Assert.Equal(AtelierTrees.Seed(12.34, -56.78, 7), AtelierTrees.Seed(12.3400001, -56.7799999, 7));
        Assert.Equal(2249698194u, AtelierTrees.Seed(12.34, -56.78, 7));
        Assert.NotEqual(AtelierTrees.Seed(12.34, -56.78, 7), AtelierTrees.Seed(12.35, -56.78, 7));
        // Variété : lacets et échelles différents, échelle légère (0,9 à 1,1), plusieurs âges.
        Assert.True(first.Select(s => s.Yaw).Distinct().Count() > first.Count / 2);
        Assert.All(first, s => Assert.InRange(s.Scale, 0.9, 1.1));
        Assert.All(first, s => Assert.InRange(s.Yaw, 0, 359));
        Assert.True(first.Select(s => s.Model).Distinct().Count() >= 6);
        // Les trois rôles sont posés.
        Assert.True(first.Count(s => s.Source == TreeSource.Orchard) >= 10, "vergers");
        Assert.True(first.Count(s => s.Source == TreeSource.Bank) >= 8, "berges");
        Assert.InRange(first.Count(s => s.Source == TreeSource.Seuil), 3, AtelierTrees.SeuilTries);
        // Écart entre deux arbres de décor.
        for (int i = 0; i < first.Count; i++)
            for (int j = i + 1; j < first.Count; j++)
                Assert.True(Math.Sqrt((first[i].X - first[j].X) * (first[i].X - first[j].X) + (first[i].Z - first[j].Z) * (first[i].Z - first[j].Z)) >= AtelierTrees.DecorSpacing - 1e-9);
    }

    [Fact]
    public void Aucun_arbre_sur_route_point_sur_porte_ni_obstacle_bati()
    {
        AdventureMap map = Vallons();
        int before = map.Obstacles.Count;
        var decor = AtelierTrees.DecorTrees(map, map.Obstacles);
        Assert.Equal(before, map.Obstacles.Count);   // décor seul : rien n'entre au moteur
        foreach (TreeSpot s in decor)
        {
            Assert.True(TownRules.RoadAt(s.X, s.Z, map.Roads).Edge >= AtelierTrees.RoadGap, s.Model + " sur une route");
            Assert.DoesNotContain(map.Clearings, c => Math.Sqrt((c.X - s.X) * (c.X - s.X) + (c.Z - s.Z) * (c.Z - s.Z)) < AtelierTrees.ClearingGap);
            Assert.DoesNotContain(map.Doors, d => Math.Sqrt((d.X - s.X) * (d.X - s.X) + (d.Z - s.Z) * (d.Z - s.Z)) < AtelierTrees.DoorGap);
            Assert.DoesNotContain(map.Obstacles, o => Inside(Box(o), s.X, s.Z, AtelierTrees.ObstacleGap));
            Assert.True(s.X > map.MinX && s.X < map.MaxX && s.Z > map.MinZ && s.Z < map.MaxZ);
            // Hors du cercle de cailloux des pierres du Seuil.
            var heart = AtelierDecor.SeuilCenter(map.Obstacles)!.Value;
            Assert.True(Math.Sqrt((s.X - heart.X) * (s.X - heart.X) + (s.Z - heart.Z) * (s.Z - heart.Z)) > AtelierDecor.PebbleRadius + 2);
        }
        // Points sûrs (dont la place du village) : aucun arbre de décor à moins de 6 m ; une place sur une route est refusée.
        var road = map.Roads.First(r => !r.Paved).Points[0];
        Assert.False(AtelierTrees.Free(road.X, road.Z, map, map.Obstacles, true));
        var maison = map.Obstacles.First(o => o.Id == "maison1");
        Assert.False(AtelierTrees.Free(Center(maison).X, Center(maison).Z, map, map.Obstacles, true));
        // Les arbres des bois gardés par la vue ne sont ni sur une route, ni sur un autre obstacle que leur bois.
        int kept = 0;
        foreach (SpaceObstacle wood in map.Obstacles.Where(o => o.Id.StartsWith("bois", StringComparison.Ordinal)))
            foreach (StyleWoodTree t in TownRules.WoodTrees(wood.Points.Select(p => (p.X.Raw / 10000.0, p.Z.Raw / 10000.0)).ToList(), wood.Id, MapPainter.WoodSpacing))
            {
                if (!AtelierTrees.WoodFree(t.X, t.Z, map, map.Obstacles)) continue;
                kept++;
                Assert.True(TownRules.RoadAt(t.X, t.Z, map.Roads).Edge >= 1);
                Assert.DoesNotContain(map.Obstacles, o => o.Id != wood.Id && !o.Id.StartsWith("bois") && !o.Id.StartsWith("riviere") && !o.Id.StartsWith("arbre")
                    && Inside(Box(o), t.X, t.Z, 1));
            }
        Assert.True(kept > 100, kept + " arbres des bois");
    }

    // --- Essence par lieu ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void L_essence_suit_le_lieu()
    {
        AdventureMap map = Vallons();
        var decor = AtelierTrees.DecorTrees(map, map.Obstacles);
        Assert.All(decor.Where(s => s.Source == TreeSource.Orchard), s => Assert.Contains(s.Essence, new[] { TreeEssence.Pommier, TreeEssence.Poirier }));
        Assert.All(decor.Where(s => s.Source == TreeSource.Bank), s => Assert.Contains(s.Essence, new[] { TreeEssence.Saule, TreeEssence.Aulne }));
        Assert.All(decor.Where(s => s.Source == TreeSource.Seuil), s => Assert.Equal(TreeEssence.Corrompu, s.Essence));
        Assert.Contains(decor, s => s.Essence == TreeEssence.Pommier);
        Assert.Contains(decor, s => s.Essence == TreeEssence.Poirier);
        // Les vergers sont dans le lieu « vergers ».
        MapPlace vergers = map.Places.Single(p => p.Id == "vergers");
        Assert.All(decor.Where(s => s.Source == TreeSource.Orchard), s => Assert.True(s.X >= vergers.MinX && s.X <= vergers.MaxX && s.Z >= vergers.MinZ && s.Z <= vergers.MaxZ));
        // Obstacles « arbre… » : vergers (arbre6 à 8), berge (arbre4, à 4,6 m de la rivière), village et ruines (pré).
        TreeEssence Of(string id) => AtelierTrees.Spot(Center(map.Obstacles.Single(o => o.Id == id)).X, Center(map.Obstacles.Single(o => o.Id == id)).Z,
            TreeSource.Obstacle, map, map.Obstacles).Essence;
        foreach (string id in new[] { "arbre6", "arbre7", "arbre8" }) Assert.Contains(Of(id), new[] { TreeEssence.Pommier, TreeEssence.Poirier });
        Assert.Contains(Of("arbre4"), new[] { TreeEssence.Saule, TreeEssence.Aulne });
        foreach (string id in new[] { "arbre1", "arbre2", "arbre3", "arbre5" }) Assert.Contains(Of(id), AtelierTrees.Mix("pre"));
        // Un obstacle est toujours adulte (le moteur connaît son tronc).
        Assert.All(new[] { "arbre1", "arbre4", "arbre7" }, id => Assert.Contains("_adulte_", AtelierTrees.Spot(Center(map.Obstacles.Single(o => o.Id == id)).X,
            Center(map.Obstacles.Single(o => o.Id == id)).Z, TreeSource.Obstacle, map, map.Obstacles).Model));
        // Bois de la forêt des Loups : épicéa, pin, hêtre, chêne, morts, foudroyés ; les six y sont.
        var wolves = new HashSet<TreeEssence>();
        foreach (string id in new[] { "bois_no1", "bois_no2", "bois_no3", "bois_no4" })
        {
            SpaceObstacle wood = map.Obstacles.Single(o => o.Id == id);
            foreach (StyleWoodTree t in TownRules.WoodTrees(wood.Points.Select(p => (p.X.Raw / 10000.0, p.Z.Raw / 10000.0)).ToList(), id, MapPainter.WoodSpacing))
                wolves.Add(AtelierTrees.Spot(t.X, t.Z, TreeSource.Woods, map, map.Obstacles).Essence);
        }
        Assert.Equal(AtelierTrees.Mix("foret").OrderBy(e => e), wolves.OrderBy(e => e));
        // Autres bois (nord-est, sud-est) : bosquet de feuillus.
        SpaceObstacle se = map.Obstacles.Single(o => o.Id == "bois_se1");
        Assert.Contains(AtelierTrees.Spot(Center(se).X, Center(se).Z, TreeSource.Woods, map, map.Obstacles).Essence, AtelierTrees.Mix("bosquet"));
        // Lisière au nord de la forêt des Loups (hors de l'aire) : essences de la forêt ; au sud des vergers : fruitiers.
        Assert.Contains(AtelierTrees.Spot(-50, 108, TreeSource.Edge, map, map.Obstacles).Essence, AtelierTrees.Mix("foret"));
        Assert.Contains(AtelierTrees.Spot(45, -78, TreeSource.Edge, map, map.Obstacles).Essence, AtelierTrees.Mix("verger"));
        // Près du Seuil : corrompu, quelle que soit l'origine ; aucun arbre corrompu loin du Seuil.
        Assert.Equal(TreeEssence.Corrompu, AtelierTrees.Spot(62, 30, TreeSource.Obstacle, map, map.Obstacles).Essence);
        Assert.DoesNotContain(decor, s => s.Essence == TreeEssence.Corrompu && s.Source != TreeSource.Seuil);
        // Pas de vignes dans les Vallons actuels : le cyprès n'est pas posé (règle prête pour la grande carte).
        Assert.DoesNotContain(map.Places, p => p.Id.Contains("vigne"));
        Assert.DoesNotContain(decor, s => s.Essence == TreeEssence.Cypres);
    }

    // --- LOD et imposteur ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Les_LOD_et_l_imposteur_sont_cables()
    {
        var phone = AtelierTrees.LodDistances(DeviceTier.Phone);
        var modest = AtelierTrees.LodDistances(DeviceTier.Modest);
        var full = AtelierTrees.LodDistances(DeviceTier.Full);
        Assert.True(phone.Lod2 < modest.Lod2 && modest.Lod2 < full.Lod2);
        Assert.True(phone.Impostor < modest.Impostor && modest.Impostor < full.Impostor);
        Assert.True(phone.Cull < modest.Cull && modest.Cull < full.Cull);
        foreach (DeviceTier tier in new[] { DeviceTier.Phone, DeviceTier.Modest, DeviceTier.Full })
            foreach (double size in new[] { 3.0, 8.0, 19.0, 40.0 })
            {
                var h = AtelierTrees.LodHeights(size, tier, 60);
                Assert.True(h.Lod1 > h.Lod2 && h.Lod2 > h.Impostor && h.Impostor > 0, $"{tier} {size}");
                Assert.True(h.Lod1 <= 1);
            }
        // 19 m vus à 120 m (champ de 60°) : 0,137 de l'écran.
        Assert.Equal(19 / (2 * 120 * Math.Tan(Math.PI / 6)), AtelierTrees.ScreenHeight(19, 120, 60), 9);
        Assert.Equal(1, AtelierTrees.ScreenHeight(19, 0, 60));
        // La vue : LODGroup LOD1 → LOD2 → imposteur → rien, distances de la machine ; collision retirée ; imposteur sans ombre.
        string trees = File.ReadAllText(Path.Combine(Game, "SpaceView.Trees.cs"));
        Assert.Contains("group.SetLODs(new[] { new LOD((float)h.Lod1, near.ToArray()), new LOD((float)h.Lod2, far.ToArray()), new LOD((float)h.Impostor, boards) });", trees, StringComparison.Ordinal);
        Assert.Contains("AtelierTrees.LodHeights(group.size * tree.transform.lossyScale.y, DeviceQuality.Tier,", trees, StringComparison.Ordinal);
        Assert.Contains("if (part == \"collision\")", trees, StringComparison.Ordinal);
        Assert.Contains("board.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;", trees, StringComparison.Ordinal);
        // Les trois places où le jeu posait des arbres téléchargés passent d'abord par l'arbre de l'atelier, plus le décor.
        string view = File.ReadAllText(Path.Combine(Game, "SpaceView.cs"));
        Assert.Contains("AtelierTree(AtelierTrees.Spot(foot.x, foot.z, TreeSource.Obstacle,", view, StringComparison.Ordinal);
        Assert.Contains("AtelierTree(AtelierTrees.Spot(spots[i].X, spots[i].Z, TreeSource.Edge,", view, StringComparison.Ordinal);
        Assert.Contains("AtelierTree(AtelierTrees.Spot(tree.X, tree.Z, TreeSource.Woods,", view, StringComparison.Ordinal);
        Assert.Contains("AtelierDecorTrees(space);", view, StringComparison.Ordinal);
        Assert.Contains("_space.Trees(camera);", File.ReadAllText(Path.Combine(Game, "ArenaView.cs")), StringComparison.Ordinal);
        // Imposteur : la colonne de l'angle le plus proche parmi 8 (caméra devant : +z, colonne 0 ; de +z vers +x : colonnes croissantes).
        Assert.Equal(0, AtelierTrees.ImpostorColumn(0, 10));
        Assert.Equal(2, AtelierTrees.ImpostorColumn(10, 0));
        Assert.Equal(4, AtelierTrees.ImpostorColumn(0, -10));
        Assert.Equal(6, AtelierTrees.ImpostorColumn(-10, 0));
        Assert.Equal(1, AtelierTrees.ImpostorColumn(7, 7));
        Assert.Equal(7, AtelierTrees.ImpostorColumn(-7, 7));
        Assert.Equal(0, AtelierTrees.ImpostorColumn(-1, 20));   // 357° : au plus proche, la colonne 0
        Assert.Equal(1, AtelierTrees.ImpostorColumn(Math.Sin(30 * Math.PI / 180), Math.Cos(30 * Math.PI / 180)));
        Assert.Equal(0, AtelierTrees.ImpostorColumn(0, 0));
        // Cellule : u = colonne / 8, v = − saison / 4 (le quad montre la cellule du printemps, colonne 0).
        Assert.Equal((0.375, -0.5), AtelierTrees.ImpostorOffset(3, 2));
        Assert.Equal((0.875, -0.75), AtelierTrees.ImpostorOffset(7, 3));
        Assert.Equal((0.0, -0.25), AtelierTrees.ImpostorOffset(8, 9));   // hors bornes : colonne 0, été
    }

    [Fact]
    public void L_imposteur_n_alloue_rien_a_chaque_image()
    {
        // Règles : aucune allocation. Sous-lot V2.28b (2) : test rendu stable (échec isolé en CI Debug, 1 472 octets) : la boucle est d'abord
        // jouée une fois (compilation à la volée et remplacement en cours de boucle, qui allouent au runtime, pas dans les règles), puis
        // mesurée trois fois ; une seule mesure à zéro suffit, les règles n'allouent jamais.
        int sum = 0;
        int Loop()
        {
            int s = 0;
            for (int i = 0; i < 20000; i++)
            {
                s += AtelierTrees.ImpostorColumn(Math.Sin(i), Math.Cos(i));
                s += (int)(AtelierTrees.ImpostorOffset(i, i % 4).U * 8);
                s += AtelierTrees.Shown(-1, i % 4);
            }
            return s;
        }
        sum += Loop();
        long least = long.MaxValue;
        for (int attempt = 0; attempt < 3 && least > 0; attempt++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            sum += Loop();
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        Assert.Equal(0, least);
        Assert.True(sum > 0);
        // Vue : la boucle de chaque image ne crée rien (seul un Vector4, une structure) ; bloc de propriétés réutilisé.
        string frame = Body(Path.Combine(Game, "SpaceView.Trees.cs"), "public void Trees(Vector3 camera)", "private void TreeSeason(int season)");
        Assert.Equal(1, frame.Split("new ").Length - 1);
        Assert.Contains("new Vector4(", frame, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach", frame, StringComparison.Ordinal);
        Assert.DoesNotContain("=>", frame, StringComparison.Ordinal);
        Assert.Contains("if (column == b.Column && season == b.Season) continue;", frame, StringComparison.Ordinal);
        Assert.Contains("private readonly MaterialPropertyBlock _boardBlock = new MaterialPropertyBlock();", File.ReadAllText(Path.Combine(Game, "SpaceView.Trees.cs")), StringComparison.Ordinal);
        foreach (string helper in new[] { "private void TreeSeason(int season)", "private void TreeGlow(float lamps)" })
        {
            string body = Body(Path.Combine(Game, "SpaceView.Trees.cs"), helper, "\n        }\n");
            Assert.DoesNotContain("new ", body.Replace("new Vector4(", ""), StringComparison.Ordinal);
            Assert.DoesNotContain("foreach", body, StringComparison.Ordinal);
        }
    }

    // --- Saisons -------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Les_saisons_changent_l_atlas_du_feuillage()
    {
        Assert.Equal(0, AtelierTrees.SeasonOfDay(0));
        Assert.Equal(1, AtelierTrees.SeasonOfDay(28));
        Assert.Equal(2, AtelierTrees.SeasonOfDay(56));
        Assert.Equal(3, AtelierTrees.SeasonOfDay(84));
        Assert.Equal(0, AtelierTrees.SeasonOfDay(112));
        Assert.Equal(1, AtelierTrees.DefaultSeason);   // été par défaut (arène, sans monde)
        Assert.Equal(1, AtelierTrees.Season(-1));
        Assert.Equal(3, AtelierTrees.Shown(3, 1));   // forcée par le menu d'essai
        Assert.Equal(0, AtelierTrees.Shown(-1, 0));   // sinon celle du monde
        Assert.Equal(new[] { false, false, true, false }, Enumerable.Range(0, 4).Select(AtelierTrees.GroundLeaves));
        Assert.Equal("chene_feuillage_automne_couleur", AtelierTrees.LeafTexture(TreeEssence.Chene, 2));
        // La partie donne la saison du monde ; le menu d'essai la force ; le feuillage, les imposteurs et les feuilles au sol suivent.
        Assert.Contains("_arena.Season((int)a.WardrobeSeason);", File.ReadAllText(Path.Combine(Game, "EdsGame.cs")), StringComparison.Ordinal);
        string trial = File.ReadAllText(Path.Combine(Editor, "EdsSeasonTrial.cs"));
        foreach (string s in new[] { "printemps", "été", "automne", "hiver", "saison du monde" })
            Assert.Contains("Essai des saisons : " + s, trial, StringComparison.Ordinal);
        string trees = File.ReadAllText(Path.Combine(Game, "SpaceView.Trees.cs"));
        Assert.Contains("if (season != _treeSeason) TreeSeason(season);", trees, StringComparison.Ordinal);
        Assert.Contains("_groundLeaves[i].SetActive(ground);", trees, StringComparison.Ordinal);
        // Veines du corrompu : la lueur des pierres du Seuil (discrète le jour, nette la nuit).
        Assert.Equal(AtelierDecor.SeuilGlow(0.7), AtelierTrees.CorruptGlow(0.7));
        Assert.Contains("TreeGlow(StyleWorld.Lamps);", trees, StringComparison.Ordinal);
        Assert.True(AtelierTrees.IsGlowingBark("corrompu_Ecorce2"));
        Assert.False(AtelierTrees.IsGlowingBark("chene_Ecorce"));
        // Shader du feuillage : couleur de sommet × 1,1, lueur, faces réglables ; défauts neutres pour les matières existantes.
        string shader = File.ReadAllText(Path.Combine(Game, "Feuillage.shader"));
        Assert.Contains("_VertexTint (\"Couleur de sommet × 1,1\", Range(0, 1)) = 0", shader, StringComparison.Ordinal);
        Assert.Contains("input.color.rgb * 1.1h", shader, StringComparison.Ordinal);
        Assert.Contains("_GlowColor.rgb", shader, StringComparison.Ordinal);
        Assert.Contains("_FaceCull (\"Faces éliminées\", Float) = 0", shader, StringComparison.Ordinal);
        Assert.DoesNotContain("Cull Off", shader, StringComparison.Ordinal);
    }

    // --- Exports livrés ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Les_exports_sont_livres_sans_LOD0_et_compresses()
    {
        long total = 0;
        foreach (TreeEssence essence in AtelierTrees.Essences)
        {
            string name = AtelierTrees.Name(essence), folder = Path.Combine(Trees, name), textures = Path.Combine(folder, "textures");
            Assert.Equal(AtelierTrees.Ages.Select(a => name + "_" + a + ".fbx").OrderBy(f => f, StringComparer.Ordinal),
                Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal));
            foreach (string age in AtelierTrees.Ages)
            {
                Assert.True(File.Exists(Path.Combine(textures, name + "_" + age + "_imposteur.png")), name + "_" + age);
                Assert.Equal((1024, 512), V218BoulangerieTests.PngSize(File.ReadAllBytes(Path.Combine(textures, name + "_" + age + "_imposteur.png"))));
                Assert.Equal(essence, AtelierTrees.EssenceOf(name + "_" + age));
            }
            for (int s = 0; s < 4; s++)
            {
                Assert.True(File.Exists(Path.Combine(textures, AtelierTrees.LeafTexture(essence, s) + ".png")), AtelierTrees.LeafTexture(essence, s));
                Assert.True(File.Exists(Path.Combine(textures, AtelierTrees.LeafNormal(essence, s) + ".jpg")));
            }
            Assert.True(File.Exists(Path.Combine(textures, name + "_ecorce_couleur.jpg")));
            Assert.Equal(essence == TreeEssence.Corrompu, File.Exists(Path.Combine(textures, name + "_ecorce_emission.jpg")));
            total += Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
        }
        // Arbre millénaire : livré (collision non convexe et cavité dans le FBX), pas posé : aucun lieu pour lui dans les Vallons actuels.
        string old = Path.Combine(Trees, AtelierTrees.Millenaire);
        Assert.True(File.Exists(Path.Combine(old, "millenaire.fbx")));
        total += Directory.GetFiles(old, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
        Assert.DoesNotContain(Directory.GetFiles(Game, "SpaceView*.cs"),
            f => File.ReadAllText(f).Contains("Millenaire", StringComparison.Ordinal));
        // Aucun LOD0, PNG seulement avec transparence (feuillage, imposteurs), JPEG pour le reste ; moins de 150 Mo ajoutés.
        string[] all = Directory.GetFiles(Trees, "*", SearchOption.AllDirectories);
        Assert.DoesNotContain(all, f => f.Contains("LOD0", StringComparison.Ordinal));
        Assert.All(all.Where(f => f.EndsWith(".png", StringComparison.Ordinal)), f => Assert.True(f.Contains("_imposteur") || f.Contains("_feuillage_") && f.Contains("_couleur"), f));
        Assert.All(all, f => Assert.Contains(Path.GetExtension(f), new[] { ".fbx", ".png", ".jpg" }));
        Assert.InRange(total, 50L << 20, 150L << 20);
        // Aucune donnée de zone touchée : pas d'arbre millénaire, les huit obstacles « arbre… » d'avant.
        Assert.Equal(8, Vallons().Obstacles.Count(o => o.Id.StartsWith("arbre", StringComparison.Ordinal)));
    }

    [Fact]
    public void Sur_telephone_un_seul_filtre_pour_les_arbres_de_l_atelier()
    {
        string view = System.IO.File.ReadAllText(System.IO.Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "SpaceView.cs"));
        Assert.Contains("if (!atelier && phone && !tree.Edge && i % 3 != 0) continue;", view, System.StringComparison.Ordinal);
    }
}
