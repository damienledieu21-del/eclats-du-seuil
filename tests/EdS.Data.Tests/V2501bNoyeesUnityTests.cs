using EdS.Data.Json;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Chantier V2.50.1b : les grottes noyées du lac dans Unity. Pièces du kit reconnues au nom des obstacles (<see cref="DrownedLooks"/>,
/// calculs purs), eau des passages noyés devant les portes d'eau, qui suit la marée, entrée dans les roselières côté marais ; pénombre d'un
/// dessous, sans pluie ni neige ; modèles pris dans <c>Resources/Noyees</c> avec repli ; aucun shader nouveau (l'eau du jeu).
/// </summary>
[Trait("Category", "World")]
public sealed class V2501bNoyeesUnityTests
{
    private static IEnumerable<ZoneDefinition> Caves => V2501NoyeesTests.Caves.Select(id => V16TestData.Interiors.Single(z => z.Id == id));

    private static string Unity(params string[] path) => File.ReadAllText(Path.Combine(new[] { Lot93UnityPackageTests.Root, "unity" }.Concat(path).ToArray()));

    private static (double X0, double Z0, double X1, double Z1) Box(SpaceObstacle o) =>
        (o.Points.Min(p => p.X.Raw) / 1e4, o.Points.Min(p => p.Z.Raw) / 1e4, o.Points.Max(p => p.X.Raw) / 1e4, o.Points.Max(p => p.Z.Raw) / 1e4);

    [Fact]
    public void Chaque_obstacle_des_grottes_a_sa_piece_et_aucun_autre_kit_ne_le_prend()
    {
        foreach (ZoneDefinition z in Caves)
        {
            var obstacles = z.Setup.Space!.Obstacles;
            Assert.True(DrownedLooks.IsDrowned(obstacles), z.Id);
            Assert.False(SewerLooks.IsSewer(obstacles) || CryptLooks.IsCrypt(obstacles) || MineLooks.IsMine(obstacles), z.Id);
            foreach (SpaceObstacle o in obstacles)
            {
                DrownedPiece piece = DrownedLooks.PieceFor(o.Id);
                Assert.NotEqual(DrownedPiece.None, piece);
                Assert.Equal(CavePiece.None, CaveLooks.PieceFor(o.Id));   // la grotte de la cascade ne le dessine pas avant elles
                Assert.NotNull(DrownedLooks.ModelFor(piece));
                Assert.Equal(DrownedLooks.HeightOf(piece) <= 1.2, o.Low);   // bas dans les données, bas à l'image (bassins, algues, coffres)
            }
        }
        // Les autres dessous ne sont pas des grottes noyées.
        foreach (ZoneDefinition z in V16TestData.Interiors.Where(z => z.Underground && !z.Id.StartsWith("zone.grottes_noyees", StringComparison.Ordinal)))
            Assert.False(DrownedLooks.IsDrowned(z.Setup.Space!.Obstacles), z.Id);
        Assert.Equal(DrownedPiece.None, DrownedLooks.PieceFor("mur_noyeesque"));
        Assert.Equal(DrownedPiece.None, DrownedLooks.PieceFor(""));
        Assert.Equal(DrownedPiece.Vault, DrownedLooks.PieceFor("voute_noyee_pilier_2"));
    }

    [Fact]
    public void Kit_de_douze_modeles_occulteurs_et_eau()
    {
        Assert.Equal(12, DrownedLooks.Models.Count);
        Assert.Equal(DrownedLooks.Models.Count, DrownedLooks.Models.Distinct().Count());
        Assert.All(Enum.GetValues<DrownedPiece>().Where(p => p != DrownedPiece.None), p => Assert.Contains(DrownedLooks.ModelFor(p)!, DrownedLooks.Models));
        Assert.Equal(new[] { DrownedPiece.Wall, DrownedPiece.Vault, DrownedPiece.Rock, DrownedPiece.Wreck, DrownedPiece.Lair, DrownedPiece.Anchor }, Enum.GetValues<DrownedPiece>().Where(DrownedLooks.Occludes));
        Assert.Equal(new[] { DrownedPiece.Basin }, Enum.GetValues<DrownedPiece>().Where(DrownedLooks.IsWater));
        Assert.Equal(new[] { DrownedPiece.Wall }, Enum.GetValues<DrownedPiece>().Where(DrownedLooks.Tiled));
        Assert.Equal(3.0, DrownedLooks.HeightOf(DrownedPiece.Vault));   // voûte basse
        Assert.Equal("mur_noyee", DrownedLooks.NameOf("Assets/EdS/Modeles/Resources/Noyees/mur_noyee.fbx"));
        Assert.Equal("ancre_noyee", DrownedLooks.NameOf("Assets\\EdS\\Modeles\\Resources\\Noyees\\ancre_noyee.fbx"));
        Assert.Null(DrownedLooks.NameOf("Assets/EdS/Modeles/Resources/Noyees/mur_noyee_couleur.jpg"));
        Assert.Null(DrownedLooks.NameOf("Assets/EdS/Modeles/Resources/Egouts/mur_egout.fbx"));
        // La liste de l'atelier : 12 modèles nommés comme dans le jeu, à 30 crédits, 360 au plus (aucun lancé).
        string lab = Path.Combine(Path.GetDirectoryName(Lot93UnityPackageTests.Root)!, "Eclats_du_Seuil_AssetLab", "Noyees", "modeles.json");
        if (File.Exists(lab))
        {
            var names = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(lab))!["modeles"]!.AsArray().Select(m => (string)m!["nom"]!).ToList();
            Assert.Equal(DrownedLooks.Models, names);
        }
    }

    /// <summary>Revue Codex de la PR #254 : les parois sont tuilées d'après les bornes réelles du modèle, bout à bout, sans trou.</summary>
    [Fact]
    public void Parois_tuilees_sans_trou()
    {
        int walls = 0;
        foreach (ZoneDefinition z in Caves)
            foreach (SpaceObstacle o in z.Setup.Space!.Obstacles.Where(o => DrownedLooks.Tiled(DrownedLooks.PieceFor(o.Id))))
            {
                (double x0, double z0, double x1, double z1) = Box(o);
                bool alongX = x1 - x0 >= z1 - z0;
                // Bornes mesurées d'un modèle quelconque (8 × 1,5 m, celles de la fiche, ou d'autres) : la couverture ne dépend pas d'elles.
                foreach ((double longSide, double shortSide) in new[] { (8.0, 1.5), (5.3, 2.1), (11.0, 0.9) })
                {
                    var tiles = SewerLooks.WallTiles(x0, z0, x1, z1, longSide, shortSide);
                    Assert.NotEmpty(tiles);
                    double reach = alongX ? x0 : z0;
                    foreach (var t in tiles)
                    {
                        Assert.Equal(reach, alongX ? t.MinX : t.MinZ, 9);
                        reach = alongX ? t.MaxX : t.MaxZ;
                        Assert.Equal(alongX ? (z0, z1) : (x0, x1), alongX ? (t.MinZ, t.MaxZ) : (t.MinX, t.MaxX));
                    }
                    Assert.Equal(alongX ? x1 : z1, reach, 9);
                }
                walls++;
            }
        Assert.Equal(3, walls);   // deux parois aux bassins, une à la salle des épaves
    }

    [Fact]
    public void Eau_des_passages_noyes_devant_les_portes_d_eau_cote_paroi()
    {
        foreach (ZoneDefinition z in Caves)
        {
            (double minX, double minZ) = (z.Setup.Space!.Min.X.Raw / 1e4, z.Setup.Space!.Min.Z.Raw / 1e4);
            (double maxX, double maxZ) = (z.Setup.Space!.Max.X.Raw / 1e4, z.Setup.Space!.Max.Z.Raw / 1e4);
            foreach (Door d in z.Doors.Where(d => d.Water != null))
            {
                double x = d.Position.X.Raw / 1e4, zz = d.Position.Z.Raw / 1e4;
                var pool = DrownedLooks.PoolAt(x, zz, minX, minZ, maxX, maxZ);
                Assert.True(pool.HasValue, z.Id + " / " + d.Id);
                var p = pool!.Value;
                // Dans l'aire, sans couvrir la place de la porte (on s'y tient au sec), tout près d'elle.
                Assert.True(p.MinX >= minX && p.MaxX <= maxX && p.MinZ >= minZ && p.MaxZ <= maxZ, d.Id);
                Assert.False(x >= p.MinX && x <= p.MaxX && zz >= p.MinZ && zz <= p.MaxZ, d.Id);
                double gap = Math.Max(Math.Max(p.MinX - x, x - p.MaxX), Math.Max(p.MinZ - zz, zz - p.MaxZ));
                Assert.Equal(DrownedLooks.PoolGap, gap, 9);
            }
        }
        // Une porte à 0,5 m de la paroi : l'eau n'y tient plus (moins de 0,2 m).
        Assert.Null(DrownedLooks.PoolAt(0, 43.5, -30, -4, 30, 44));
        var south = DrownedLooks.PoolAt(0, 1.2, -30, 0, 30, 44)!.Value;   // vers le sud, coupée au bord de l'aire
        Assert.Equal((-2.0, 0.0, 2.0), (south.MinX, south.MinZ, south.MaxX));
        Assert.Equal(0.4, south.MaxZ, 9);
    }

    [Theory]
    [InlineData(true, true, true, true)]      // chenal à marée haute : l'eau se voit
    [InlineData(true, true, false, false)]    // chenal à marée basse : à sec
    [InlineData(true, false, false, true)]    // siphon : toujours noyé
    [InlineData(true, false, true, true)]
    [InlineData(false, false, true, false)]   // porte sans eau
    public void Eau_du_passage_selon_la_maree(bool water, bool tideOnly, bool high, bool shown) =>
        Assert.Equal(shown, DrownedLooks.PoolShown(water, tideOnly, high));

    [Fact]
    public void Entree_dans_les_roselieres_cote_marais()
    {
        Assert.Equal(DrownedLooks.ReedsModel, DrownedLooks.SurfaceModel("porte_grottes_noyees"));
        Assert.Null(DrownedLooks.SurfaceModel("porte_egouts_marche"));
        Assert.Null(DrownedLooks.SurfaceModel("roselieres"));
        Assert.Contains(UnderDoors, d => DrownedLooks.SurfaceModel(d) != null);
    }

    [Fact]
    public void Partie_reprise_dans_les_grottes_a_l_abri_sans_pluie_ni_neige_et_eau_des_portes_sur_la_carte()
    {
        string dir = Path.Combine(Path.GetTempPath(), "eds_v2501b_" + Guid.NewGuid().ToString("N"));
        string save = Path.Combine(dir, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        try
        {
            SaveStore.Write(save, new Data.Saves.WorldSave("zone.grottes_noyees_bassins", 0, 2, Array.Empty<string>(), V11TestData.Hero(12)));
            AdventureSession a = V11TestData.Adventure(save);
            Assert.True(a.Underground && a.Sheltered && !a.Indoors);   // à l'abri : ni pluie, ni neige, ni glace à l'affichage
            Assert.True(a.Map.Underground && !a.Map.Indoor);
            Assert.Equal(new[] { ("roselieres", true, false, (long?)null), ("chenal", false, true, (long?)12_000), ("siphon", false, false, (long?)25_000) },
                a.Map.Doors.Select(d => (d.Id, d.Outside, d.Water?.Tide == true, d.Water?.DiveMs)));
            Assert.True(a.Mood().Indoors);       // ambiance sonore à l'abri
            Assert.Equal(0, a.Mood().Rain);      // la pluie ne s'entend pas sous terre
            Assert.True(DrownedLooks.IsDrowned(a.Map.Obstacles));
            Assert.Equal("Marée basse : elle monte dans 1 h 00", a.TideHint);   // 8 h 00 du monde (un tick plus tard) : marée basse jusqu'à 9 h
        }
        finally { Directory.Delete(dir, true); }
        // Ailleurs, pas d'indication de marée.
        string other = Path.Combine(Path.GetTempPath(), "eds_v2501b_" + Guid.NewGuid().ToString("N"));
        string save2 = Path.Combine(other, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(save2)!);
        try
        {
            SaveStore.Write(save2, new Data.Saves.WorldSave("zone.egouts_citerne", 0, 2, Array.Empty<string>(), V11TestData.Hero(8)));
            Assert.Null(V11TestData.Adventure(save2).TideHint);
        }
        finally { Directory.Delete(other, true); }
    }

    [Fact]
    public void Unity_grottes_noyees_dessinees_et_kit_branche()
    {
        string cave = Unity("Assets", "EdS", "Game", "SpaceView.Cave.cs");
        string view = Unity("Assets", "EdS", "Game", "SpaceView.cs");
        string drowned = Unity("Assets", "EdS", "Game", "SpaceView.Drowned.cs");
        string game = Unity("Assets", "EdS", "Game", "EdsGame.cs");
        string style = Unity("Assets", "EdS", "Editor", "EdsStyle.cs");
        Assert.Contains("_drowned = DrownedLooks.IsDrowned(space.Obstacles);", cave);
        Assert.Contains("DrownedObstacle(o, rock, rockTile, wood, woodTile)", cave);
        Assert.Contains("DrownedDoor(door, minX, minZ, maxX, maxZ, rock, rockTile);", cave);
        Assert.Contains("StyleWorld.SetCave(true);", cave);                             // pénombre : ni pluie, ni neige, ni leur son
        Assert.Contains("DrownedEntrances();", view);
        // Modèles chargés depuis Resources (CryptModel : Resources.Load, une fois), avec repli en formes simples.
        Assert.Contains("CryptModel(DrownedLooks.Folder, DrownedLooks.ModelFor(piece))", drowned);
        Assert.Contains("CryptModel(DrownedLooks.Folder, DrownedLooks.PassageModel)", drowned);
        Assert.Contains("// Formes de remplacement.", drowned);
        Assert.Contains("Resources.Load<GameObject>", Unity("Assets", "EdS", "Game", "SpaceView.Crypt.cs"));
        // Occulteurs, tuiles d'après le modèle mesuré, pose sans étirement (échelle avant lacet, FitSewerModel).
        Assert.Contains("DrownedLooks.Occludes(piece)", drowned);
        Assert.Contains("Prism(o.Id, points, height, limestone, true, rockTile)", drowned);
        Assert.Contains("SewerLooks.WallTiles(", drowned);
        Assert.Contains("Footprint(model, out float modelLong, out float modelShort)", drowned);
        Assert.DoesNotContain("localScale", drowned);   // toute mise à l'échelle passe par FitSewerModel
        string sewer = Unity("Assets", "EdS", "Game", "SpaceView.Sewer.cs");
        int scale = sewer.IndexOf("pose.transform.localScale", StringComparison.Ordinal), yaw = sewer.IndexOf("look.transform.localRotation", StringComparison.Ordinal);
        Assert.True(scale > 0 && yaw > scale, "l'échelle vient avant le lacet");
        // L'eau : celle du jeu (StyleWorld.Water, « EdS/Eau »), jamais un shader nouveau ; elle suit la marée.
        Assert.Contains("_water, false)", drowned);
        Assert.DoesNotContain("Shader.Find", drowned);
        Assert.Contains("_arena.Tide(a.World.Tide == EdS.World.TideLevel.High);", game);
        Assert.Contains("if (a.TideHint is string tide) return tide;", game);
        Assert.Contains("DrownedPieces(notes);", style);
        Assert.Empty(Directory.GetFiles(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets"), "*Noyee*.shader", SearchOption.AllDirectories));
    }

    /// <summary>Kit Tripo du 26 novembre 2026 (240 crédits) : les 12 modèles finis dans Blender, à plat dans <c>Resources/Noyees</c>.</summary>
    [Fact]
    public void Kit_de_grotte_noyee_present_et_reconnu_avec_repli()
    {
        string folder = Path.Combine(Lot93UnityPackageTests.Root, "unity", DrownedLooks.AssetFolder);
        foreach (string name in DrownedLooks.Models)
        {
            string fbx = Path.Combine(folder, name + ".fbx");
            Assert.True(File.Exists(fbx), fbx);
            Assert.True(new FileInfo(fbx).Length > 10_000, fbx);
            Assert.True(File.Exists(Path.Combine(folder, name + "_couleur.jpg")), name);
            Assert.True(File.Exists(Path.Combine(folder, name + "_normale.png")), name);
            Assert.Equal(name, DrownedLooks.NameOf(DrownedLooks.AssetFolder + "/" + name + ".fbx"));
        }
        Assert.Equal(DrownedLooks.Models.Count, Directory.GetFiles(folder, "*.fbx").Length);   // rien d'autre, rien d'oublié
        Assert.EndsWith("Resources/" + DrownedLooks.Folder.TrimEnd('/'), DrownedLooks.AssetFolder);
        // Repli : formes simples si un modèle manque (pièces, passage noyé, stalactites, entrée des roselières).
        string drowned = Unity("Assets", "EdS", "Game", "SpaceView.Drowned.cs");
        foreach (string fallback in new[] { "// Formes de remplacement.", "Box(\"Passage noyé (voûte, \"", "Box(\"Stalactites (\"", "Box(\"Grottes noyées (bouche, \"" })
            Assert.Contains(fallback, drowned);
    }

    [Fact]
    public void Stalactites_repaire_et_ancre_dans_le_decor()
    {
        ZoneDefinition[] caves = Caves.ToArray();
        // Le repaire des pilleurs au nord des bassins, l'ancre à côté du coffre de la cale de l'épave : des obstacles, pas du décor traversable.
        Assert.Single(caves[0].Setup.Space!.Obstacles, o => DrownedLooks.PieceFor(o.Id) == DrownedPiece.Lair);
        Assert.Single(caves[1].Setup.Space!.Obstacles, o => DrownedLooks.PieceFor(o.Id) == DrownedPiece.Anchor);
        Assert.Equal((DrownedLooks.LairModel, DrownedLooks.AnchorModel), (DrownedLooks.ModelFor(DrownedPiece.Lair), DrownedLooks.ModelFor(DrownedPiece.Anchor)));
        Assert.True(DrownedLooks.Occludes(DrownedPiece.Lair) && DrownedLooks.Occludes(DrownedPiece.Anchor));   // V2.55 : occulteurs (3 m et 2,2 m)
        foreach (ZoneDefinition z in caves)
        {
            var obstacles = z.Setup.Space!.Obstacles;
            var spots = DrownedLooks.Stalactites(obstacles);
            Assert.Equal(obstacles.Count(o => DrownedLooks.PieceFor(o.Id) is DrownedPiece.Rock or DrownedPiece.Vault), spots.Count);
            foreach (var spot in spots)
            {
                (double x0, double z0, double x1, double z1) = Box(obstacles.Single(o => o.Id == spot.Id));
                Assert.Equal(((x0 + x1) / 2, (z0 + z1) / 2), (spot.X, spot.Z));
            }
            Assert.True(spots.Count >= 3, z.Id);
        }
        // Pendues au plafond, au-dessus des têtes (2,5 m) ; le plafond au-dessus du haut des parois des rochers.
        Assert.Equal(2.5, DrownedLooks.StalactiteBase);
        Assert.True(DrownedLooks.CeilingHeight >= DrownedLooks.HeightOf(DrownedPiece.Rock));
        Assert.Empty(DrownedLooks.Stalactites(Array.Empty<SpaceObstacle>()));
        string cave = Unity("Assets", "EdS", "Game", "SpaceView.Cave.cs"), drowned = Unity("Assets", "EdS", "Game", "SpaceView.Drowned.cs");
        Assert.Contains("if (_drowned) DrownedStalactites(space, rock, rockTile);", cave);
        Assert.Contains("CryptModel(DrownedLooks.Folder, DrownedLooks.StalactiteModel)", drowned);
        Assert.Contains("DrownedLooks.LanternIntensity", drowned);   // la lanterne du repaire
    }

    /// <summary>Revue Codex de la PR #259 (P3) : la margelle de repli des bassins entoure l'eau sans jamais la couvrir.</summary>
    [Fact]
    public void Margelle_de_repli_autour_de_l_eau_sans_la_couvrir()
    {
        int basins = 0;
        foreach (ZoneDefinition z in Caves)
            foreach (SpaceObstacle o in z.Setup.Space!.Obstacles.Where(o => DrownedLooks.PieceFor(o.Id) == DrownedPiece.Basin))
            {
                (double x0, double z0, double x1, double z1) = Box(o);
                var rims = DrownedLooks.BasinRim(x0, z0, x1, z1);
                Assert.Equal(4, rims.Count);
                double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue, area = 0;
                foreach (var r in rims)
                {
                    double rx0 = r.Cx - r.Sx / 2, rx1 = r.Cx + r.Sx / 2, rz0 = r.Cz - r.Sz / 2, rz1 = r.Cz + r.Sz / 2;
                    // Aucune bordure n'empiète sur l'eau (recouvrement d'aire nul).
                    double ox = Math.Min(rx1, x1) - Math.Max(rx0, x0), oz = Math.Min(rz1, z1) - Math.Max(rz0, z0);
                    Assert.False(ox > 1e-9 && oz > 1e-9, o.Id);
                    (minX, minZ, maxX, maxZ) = (Math.Min(minX, rx0), Math.Min(minZ, rz0), Math.Max(maxX, rx1), Math.Max(maxZ, rz1));
                    area += r.Sx * r.Sz;
                }
                // Elles ferment l'anneau autour de l'eau : bornes de l'eau élargies de la margelle, aire de l'anneau exacte (sans chevauchement ni trou).
                double w = DrownedLooks.RimWidth;
                Assert.Equal(x0 - w, minX, 9);
                Assert.Equal(z0 - w, minZ, 9);
                Assert.Equal(x1 + w, maxX, 9);
                Assert.Equal(z1 + w, maxZ, 9);
                Assert.Equal((x1 - x0 + 2 * w) * (z1 - z0 + 2 * w) - (x1 - x0) * (z1 - z0), area, 9);
                basins++;
            }
        Assert.Equal(2, basins);
        // Plus haute que l'eau (0,08 m), mais seulement sur son pourtour.
        Assert.True(DrownedLooks.RimHeight > 0.08);
        string drowned = Unity("Assets", "EdS", "Game", "SpaceView.Drowned.cs");
        Assert.Contains("DrownedLooks.BasinRim(x0, z0, x1, z1)", drowned);
        Assert.DoesNotContain("(margelle)\"", drowned);
    }
}
