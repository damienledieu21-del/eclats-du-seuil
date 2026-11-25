using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.GrandeCarteLieux;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Chantier V2.49.2 : les égouts du Bourg dans Unity. Pièces du kit d'égout par le nom des obstacles (<see cref="SewerLooks"/>), passerelles sur
/// l'écart entre deux tronçons du canal, voûtes sur les grilles et les passerelles, échelle aux portes qui montent, bouche d'égout et déversoir
/// côté Bourg ; modèles branchés par leur nom (kit Tripo généré, validé par Damien le 23 novembre 2026 ; formes simples en repli).
/// </summary>
[Trait("Category", "World")]
public class V2492EgoutsUnityTests
{
    private static readonly string Scenarios = Path.Combine(AppContext.BaseDirectory, "scenarios");

    private static ZoneDefinition Zone(string id) =>
        ZoneJsonReader.Read(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, id)), Scenarios, ShippedData,
            ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!).Zone!;

    private static SpaceObstacle Rect(string id, double x0, double z0, double x1, double z1) =>
        new(id, new[] { V(x0, z0), V(x1, z0), V(x1, z1), V(x0, z1) });

    private static GroundVector V(double x, double z) => new(Fixed.FromRaw((long)Math.Round(x * 10000)), Fixed.FromRaw((long)Math.Round(z * 10000)));

    private static string Unity(params string[] path) => File.ReadAllText(Path.Combine(new[] { Lot93UnityPackageTests.Root, "unity" }.Concat(path).ToArray()));

    [Fact]
    public void Chaque_obstacle_des_egouts_a_sa_piece_et_c_est_un_egout()
    {
        foreach (string id in new[] { "zone.egouts_collecteur", "zone.egouts_citerne" })
        {
            var obstacles = Zone(id).Setup.Space!.Obstacles;
            Assert.All(obstacles, o => Assert.NotEqual(SewerPiece.None, SewerLooks.PieceFor(o.Id)));
            Assert.True(SewerLooks.IsSewer(obstacles));
            // Ni crypte, ni mine, ni grotte : les égouts ont leur kit.
            Assert.False(CryptLooks.IsCrypt(obstacles));
            Assert.False(MineLooks.IsMine(obstacles));
            Assert.All(obstacles, o => Assert.Equal(CavePiece.None, CaveLooks.PieceFor(o.Id)));
        }
        // Les autres dessous ne sont pas des égouts.
        foreach (string id in new[] { "zone.crypte_abbaye", "zone.mine_galeries", "zone.grotte_cascade" })
            Assert.False(SewerLooks.IsSewer(Zone(id).Setup.Space!.Obstacles), id);
        Assert.False(SewerLooks.IsSewer(null!));
    }

    [Theory]
    [InlineData("mur_egout_sud_centre", SewerPiece.Wall)]
    [InlineData("mur_egout", SewerPiece.Wall)]
    [InlineData("pilier_egout_citerne_3", SewerPiece.Pillar)]
    [InlineData("canal_egout_est", SewerPiece.Canal)]
    [InlineData("bassin_egout_citerne", SewerPiece.Basin)]
    [InlineData("grille_egout_salle", SewerPiece.Grate)]
    [InlineData("tuyau_egout_1", SewerPiece.Pipe)]
    [InlineData("deversoir_egout_citerne", SewerPiece.Outfall)]
    [InlineData("caisses_egout_2", SewerPiece.Crates)]
    [InlineData("repaire_egout_passeur", SewerPiece.Lair)]
    [InlineData("mur_egouts", SewerPiece.None)]        // pas de « _ » après le préfixe
    [InlineData("vieux_mur_egout", SewerPiece.None)]   // le préfixe est au début
    [InlineData("Mur_egout_1", SewerPiece.None)]       // la casse compte
    [InlineData("mur_crypte_1", SewerPiece.None)]
    [InlineData("", SewerPiece.None)]
    [InlineData(null, SewerPiece.None)]
    public void Pieces_du_kit_d_egout_par_le_nom_de_l_obstacle(string? id, SewerPiece piece) => Assert.Equal(piece, SewerLooks.PieceFor(id!));

    [Theory]
    [InlineData("Assets/EdS/Modeles/Resources/Egouts/canal_egout.fbx", "canal_egout")]
    [InlineData("Assets/EdS/Modeles/Resources/Egouts/bouche_egout.FBX", "bouche_egout")]
    [InlineData("Assets\\EdS\\Modeles\\Resources\\Egouts\\voute_egout.fbx", "voute_egout")]
    [InlineData("Assets/EdS/Modeles/Resources/Egouts/sous/canal_egout.fbx", null)]
    [InlineData("Assets/EdS/Modeles/Resources/Egouts/canal_egout.glb", null)]
    [InlineData("Assets/EdS/Modeles/Resources/Egouts/inconnu.fbx", null)]
    [InlineData("Assets/EdS/Modeles/Resources/Crypte/mur_crypte.fbx", null)]
    [InlineData("", null)]
    public void Nom_d_un_modele_du_kit_d_egout(string path, string? name) => Assert.Equal(name, SewerLooks.NameOf(path));

    [Fact]
    public void Kit_de_treize_modeles_chaque_piece_a_son_modele()
    {
        Assert.Equal(new[] { "mur_egout", "voute_egout", "canal_egout", "passerelle_egout", "grille_egout", "echelle_egout", "bouche_egout", "tuyau_egout",
            "deversoir_egout", "caisses_egout", "repaire_egout", "pilier_egout", "bassin_egout" }, SewerLooks.Models);
        Assert.Equal(SewerLooks.Models.Count, SewerLooks.Models.Distinct().Count());
        foreach (SewerPiece piece in Enum.GetValues<SewerPiece>().Where(p => p != SewerPiece.None))
        {
            Assert.Contains(SewerLooks.ModelFor(piece)!, SewerLooks.Models);
            Assert.InRange(SewerLooks.HeightOf(piece), 0.5, CaveLooks.WallHeight);
        }
        Assert.Null(SewerLooks.ModelFor(SewerPiece.None));
        Assert.Equal(new[] { SewerPiece.Wall, SewerPiece.Pillar }, Enum.GetValues<SewerPiece>().Where(SewerLooks.Occludes));
        Assert.Equal(new[] { SewerPiece.Canal, SewerPiece.Basin }, Enum.GetValues<SewerPiece>().Where(SewerLooks.IsWater));
        // La liste de l'atelier : 13 modèles nommés comme dans le jeu, à 30 crédits, 390 au plus (aucun lancé).
        string lab = Path.Combine(Path.GetDirectoryName(Lot93UnityPackageTests.Root)!, "Eclats_du_Seuil_AssetLab", "Egouts", "modeles.json");
        if (File.Exists(lab))
        {
            var names = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(lab))!["modeles"]!.AsArray().Select(m => (string)m!["nom"]!).ToList();
            Assert.Equal(SewerLooks.Models, names);
        }
    }

    [Fact]
    public void Passerelles_et_voutes_du_collecteur_et_de_la_citerne()
    {
        var collecteur = Zone("zone.egouts_collecteur").Setup.Space!.Obstacles;
        var citerne = Zone("zone.egouts_citerne").Setup.Space!.Obstacles;
        Assert.Equal(new[] { (-20.0, 14.0, -16.0, 18.0), (16.0, 14.0, 20.0, 18.0) }, SewerLooks.Bridges(collecteur));
        Assert.Equal(SewerLooks.Bridges(collecteur), SewerLooks.Arches(collecteur));   // une seule grille au collecteur : voûtes sur les passerelles
        Assert.Empty(SewerLooks.Bridges(citerne));
        Assert.Equal(new[] { (-3.4, 28.0, 3.4, 29.5) }, SewerLooks.Arches(citerne));   // la grille du repaire de la bête
        Assert.Empty(SewerLooks.Arches(null!));
        // Frontières : tronçons collés (écart nul) ou sans bande de Z commune, rien.
        Assert.Empty(SewerLooks.Bridges(new[] { Rect("canal_egout_a", 0, 0, 4, 2), Rect("canal_egout_b", 4, 0, 8, 2) }));
        Assert.Empty(SewerLooks.Bridges(new[] { Rect("canal_egout_a", 0, 0, 4, 2), Rect("canal_egout_b", 5, 2, 8, 4) }));
        Assert.Equal(new[] { (4.0, 1.0, 4.0001, 2.0) }, SewerLooks.Bridges(new[] { Rect("canal_egout_a", 0, 0, 4, 2), Rect("canal_egout_b", 4.0001, 1, 8, 3) }));
        Assert.True(SewerLooks.VaultHeight > 3.2);   // le linteau de remplacement est sous la voûte
    }

    private static void Near((double, double, double, double) expected, (double MinX, double MinZ, double MaxX, double MaxZ)? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Item1, actual!.Value.MinX, 9);
        Assert.Equal(expected.Item2, actual.Value.MinZ, 9);
        Assert.Equal(expected.Item3, actual.Value.MaxX, 9);
        Assert.Equal(expected.Item4, actual.Value.MaxZ, 9);
    }

    [Fact]
    public void Echelle_aux_portes_qui_montent_pas_au_deversoir()
    {
        Assert.Equal(SewerLooks.LadderModel, SewerLooks.DoorModel(new MapDoor("echelle", "x", null, 0, 0, kind: DoorKind.Shaft)));
        Assert.Equal(SewerLooks.LadderModel, SewerLooks.DoorModel(new MapDoor("bouche_du_marche", "x", null, 0, 0, true)));
        Assert.Null(SewerLooks.DoorModel(new MapDoor("deversoir", "x", null, 0, 0, true)));
        Assert.Null(SewerLooks.DoorModel(new MapDoor("x", "x", null, 0, 0, kind: DoorKind.Stairs)));
        Assert.Null(SewerLooks.DoorModel(new MapDoor("x", "x", null, 0, 0, kind: DoorKind.Hatch)));
        Assert.Throws<ArgumentNullException>(() => SewerLooks.DoorModel(null!));

        // Contre le bord le plus proche, centrée sur la porte, sans toucher la place d'arrivée.
        Near((-24.6, -4.0, -23.4, -3.6), SewerLooks.LadderAt(-24, -2.5, -30, -4, 30, 40));   // bouche du marché : bord sud
        Near((25.4, 39.6, 26.6, 40.0), SewerLooks.LadderAt(26, 38, -30, -4, 30, 40));       // échelle de la citerne : bord nord
        Near((-10.0, 4.4, -9.6, 5.6), SewerLooks.LadderAt(-8.5, 5, -10, -10, 10, 10));       // bord ouest
        Near((9.6, -5.6, 10.0, -4.4), SewerLooks.LadderAt(8.5, -5, -10, -10, 10, 10));       // bord est
        Assert.Null(SewerLooks.LadderAt(0, -3.6, -10, -4, 10, 10));   // porte à 0,4 m du bord : pas la place
        Assert.NotNull(SewerLooks.LadderAt(0, -3.5999, -10, -4, 10, 10));
        foreach (ZoneDefinition z in new[] { Zone("zone.egouts_collecteur"), Zone("zone.egouts_citerne") })
            foreach (Door d in z.Doors)
            {
                double x = d.Position.X.Raw / 1e4, zz = d.Position.Z.Raw / 1e4;
                var s = z.Setup.Space!;
                var l = SewerLooks.LadderAt(x, zz, s.Min.X.Raw / 1e4, s.Min.Z.Raw / 1e4, s.Max.X.Raw / 1e4, s.Max.Z.Raw / 1e4);
                Assert.NotNull(l);
                double gap = Math.Max(Math.Max(l!.Value.MinX - x, x - l.Value.MaxX), Math.Max(l.Value.MinZ - zz, zz - l.Value.MaxZ));
                Assert.True(gap >= 1.0, $"{z.Id} / {d.Id} : {gap}");   // la place d'arrivée reste libre
            }
    }

    [Fact]
    public void Pose_sans_etirement_et_a_la_hauteur_de_la_fiche()
    {
        // Le modèle suit l'emprise tant qu'il n'est pas étiré de plus de 1,5 fois sur un axe par rapport à l'autre.
        Assert.Equal((false, 2.0, 1.0, 2.0), SewerLooks.Fit(8, 1, 5, 16, 10, 1));   // bassin à l'échelle 2, sans déformation
        var grate = SewerLooks.Fit(2, 3, 0.4, 2, 1.5, 3);   // grille de la salle : 2 × 1,5 m
        Assert.False(grate.Turn);
        Assert.Equal(1.0, grate.Sx, 6);
        Assert.Equal(1.5, grate.Sz, 6);   // plafonnée à 1,5 fois l'autre axe (et non 3,75)
        Assert.Equal(1.0, grate.Sy, 6);
        var turned = SewerLooks.Fit(2, 2.5, 1.5, 1.4, 4, 2.5);   // déversoir de la citerne, le long d'un mur nord-sud
        Assert.True(turned.Turn);
        Assert.True(Math.Max(turned.Sx, turned.Sz) <= Math.Min(turned.Sx, turned.Sz) * CryptLooks.MaxStretch + 1e-9);
        Assert.Equal((false, 1.0, 1.0, 1.0), SewerLooks.Fit(0, 1, 1, 1, 1, 1));
        Assert.Equal((false, 1.0, 1.0, 1.0), SewerLooks.Fit(1, 1, 1, 1, -1, 1));
        // Le mur et le canal en tuiles (au plus 1,5 fois le modèle) ; la hauteur ne suit jamais l'emprise.
        Assert.Equal((8.0, 1.5), SewerLooks.TileOf(SewerPiece.Wall));
        Assert.Equal((8.0, 4.0), SewerLooks.TileOf(SewerPiece.Canal));
        Assert.Null(SewerLooks.TileOf(SewerPiece.Basin));
        var tiles = CryptLooks.Tiles(-16, 14, 16, 18, 8, 4);   // le tronçon central du canal
        Assert.Equal(4, tiles.Count);
        Assert.All(tiles, t => Assert.InRange(t.MaxX - t.MinX, 8 / CryptLooks.MaxStretch, 8 * CryptLooks.MaxStretch));
        Assert.Equal(CaveLooks.WallHeight, SewerLooks.HeightOf(SewerPiece.Wall));
    }

    [Fact]
    public void Entrees_du_Bourg_bouche_et_deversoir()
    {
        Assert.Equal(SewerLooks.ManholeModel, SewerLooks.SurfaceModel("porte_egouts_marche"));
        Assert.Equal(SewerLooks.OutfallModel, SewerLooks.SurfaceModel("porte_egouts_deversoir"));
        Assert.Null(SewerLooks.SurfaceModel("porte_crypte_abbaye"));
        Assert.Null(SewerLooks.SurfaceModel(""));
        // Chaque porte des Vallons qui descend aux égouts a sa pièce côté Bourg.
        var under = GrandeCarteLieux.Vallons.Value.Doors.Where(d => d.To == "zone.egouts_collecteur").Select(d => d.Id).ToList();
        Assert.Equal(new[] { "porte_egouts_marche", "porte_egouts_deversoir" }, under);
        Assert.All(under, d => Assert.NotNull(SewerLooks.SurfaceModel(d)));
    }

    [Fact]
    public void Partie_reprise_dans_les_egouts_a_l_abri_sans_pluie_ni_neige()
    {
        string dir = Path.Combine(Path.GetTempPath(), "eds_v2492_" + Guid.NewGuid().ToString("N"));
        string save = Path.Combine(dir, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        try
        {
            SaveStore.Write(save, new WorldSave("zone.egouts_collecteur", 0, 2, Array.Empty<string>(), V11TestData.Hero(8)));
            AdventureSession a = V11TestData.Adventure(save);
            Assert.True(a.Underground && a.Sheltered && !a.Indoors);   // à l'abri : ni pluie, ni neige, ni glace à l'affichage
            Assert.True(a.Map.Underground && !a.Map.Indoor);
            Assert.Equal(new[] { ("bouche_du_marche", DoorKind.Door, true), ("deversoir", DoorKind.Door, true), ("echelle_de_la_citerne", DoorKind.Shaft, false) },
                a.Map.Doors.Select(d => (d.Id, d.Kind, d.Outside)));
            Assert.True(a.Mood().Indoors);       // ambiance sonore à l'abri
            Assert.Equal(0, a.Mood().Rain);      // la pluie ne s'entend pas sous terre
            Assert.True(SewerLooks.IsSewer(a.Map.Obstacles));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Unity_egouts_dessines_et_kit_branche()
    {
        string cave = Unity("Assets", "EdS", "Game", "SpaceView.Cave.cs"), sewer = Unity("Assets", "EdS", "Game", "SpaceView.Sewer.cs");
        string view = Unity("Assets", "EdS", "Game", "SpaceView.cs"), style = Unity("Assets", "EdS", "Editor", "EdsStyle.cs");
        Assert.Contains("_sewer = SewerLooks.IsSewer(space.Obstacles);", cave);
        // Les égouts passent avant la crypte, la mine et le rideau d'eau.
        Assert.True(cave.IndexOf("SewerObstacle(o,", StringComparison.Ordinal) < cave.IndexOf("CryptObstacle(o,", StringComparison.Ordinal));
        Assert.True(cave.IndexOf("SewerDoor(door,", StringComparison.Ordinal) < cave.IndexOf("CryptDoor(door,", StringComparison.Ordinal));
        Assert.Contains("SewerBridgesAndArches(space, rock, rockTile, wood, woodTile)", cave);
        Assert.Contains("SewerEntrances();", view);
        Assert.Contains("CryptModel(SewerLooks.Folder, SewerLooks.ModelFor(piece))", sewer);
        Assert.Contains("SewerLooks.Occludes(piece)", sewer);                    // murs et piliers : occulteurs
        Assert.Contains("Prism(o.Id, points, height, brick, true, rockTile)", sewer);   // et leurs formes de remplacement aussi
        Assert.Contains("CryptLooks.Tiles(", sewer);                              // grands murs et canal en tuiles
        Assert.Contains("SewerLooks.Fit(", sewer);                                // jamais étirés
        Assert.Contains("SewerLooks.Arches(space.Obstacles)", sewer);
        Assert.Contains("SewerLooks.Bridges(space.Obstacles)", sewer);
        Assert.Contains("_curtains.Add(", sewer);   // le jour du Bourg suit le jour du dehors
        Assert.Contains("SewerPieces(notes);", style);
        Assert.Contains("SewerLooks.NameOf", style);
        // Aucun shader nouveau (mots réservés HLSL) ; scripts en C# 9 sans annotation de nullabilité.
        Assert.DoesNotContain("Shader", sewer);
        Assert.DoesNotContain("#nullable", sewer);
        Assert.DoesNotContain("string?", sewer);
        Assert.DoesNotContain("GameObject?", sewer);
    }

    /// <summary>
    /// Kit d'égout livré (décision de Damien du 23 novembre 2026 : 390 crédits validés, dépense faite) : les treize modèles finis dans
    /// Blender sont dans Resources/Egouts, chacun avec sa couleur et sa normale, et reconnus par leur nom ; les formes simples restent en repli.
    /// </summary>
    [Fact]
    public void Kit_d_egout_present_et_reconnu()
    {
        string folder = Path.Combine(Lot93UnityPackageTests.Root, "unity", SewerLooks.AssetFolder);
        foreach (string name in SewerLooks.Models)
        {
            string fbx = Path.Combine(folder, name + ".fbx");
            Assert.True(File.Exists(fbx), fbx);
            Assert.True(new FileInfo(fbx).Length > 10_000, fbx);
            Assert.True(File.Exists(Path.Combine(folder, name + "_couleur.jpg")), name);
            Assert.True(File.Exists(Path.Combine(folder, name + "_normale.png")), name);
            Assert.Equal(name, SewerLooks.NameOf(SewerLooks.AssetFolder + "/" + name + ".fbx"));
        }
        Assert.Equal(13, SewerLooks.Models.Count);
        Assert.Equal(SewerLooks.Models.Count, Directory.GetFiles(folder, "*.fbx").Length);   // rien d'autre, rien d'oublié
        Assert.Equal("Egouts/", SewerLooks.Folder);
        Assert.EndsWith("Resources/" + SewerLooks.Folder.TrimEnd('/'), SewerLooks.AssetFolder);
        // Repli : formes simples si un modèle manque (bouche, déversoir, pièces des égouts).
        string sewer = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "SpaceView.Sewer.cs"));
        Assert.Contains("Box(\"Bouche d'égout (\"", sewer);
        Assert.Contains("Box(\"Déversoir (linteau, \"", sewer);
    }

    /// <summary>Demande du 23 novembre 2026 : le déversoir regarde le Bourg (son ouverture vers le centre de la palissade).</summary>
    [Fact]
    public void Deversoir_tourne_vers_le_Bourg()
    {
        (double x, double z) = P(GrandeCarteLieux.Vallons.Value.DoorOf("porte_egouts_deversoir")!.Position);
        double yaw = SewerLooks.OutfallYaw(x, z) * Math.PI / 180;
        (double fx, double fz) = (Math.Sin(yaw), Math.Cos(yaw));   // avant du modèle (+Z) tourné du lacet
        (double bx, double bz) = (SewerLooks.BourgCentre.X - x, SewerLooks.BourgCentre.Z - z);
        double len = Math.Sqrt(bx * bx + bz * bz);
        Assert.True((fx * bx + fz * bz) / len > 0.9999);   // face au Bourg
        Assert.True(z < SewerLooks.BourgCentre.Z);          // au sud du Bourg : il regarde vers le nord
        Assert.InRange(SewerLooks.OutfallYaw(x, z), 0, 45);
        // Proportions du modèle fini (1,73 × 2,5 × 1,29 m) : jamais tourné d'un quart par la pose sans étirement.
        Assert.False(SewerLooks.Fit(1.73, 1.29, 2.5, SewerLooks.OutfallWidth, SewerLooks.OutfallDepth, SewerLooks.OutfallHeight).Turn);
        Assert.Equal(0.0, SewerLooks.OutfallYaw(SewerLooks.BourgCentre.X, SewerLooks.BourgCentre.Z - 10), 9);
        Assert.Equal(90.0, SewerLooks.OutfallYaw(SewerLooks.BourgCentre.X - 10, SewerLooks.BourgCentre.Z), 9);
    }

    /// <summary>Demande du 23 novembre 2026 : la bouche d'égout n'est jamais noyée sous les pavés, même en pente.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(12.37)]
    [InlineData(-3.5)]
    public void Bouche_d_egout_au_dessus_des_paves(double highest)
    {
        double foot = SewerLooks.ManholeBase(highest), top = foot + SewerLooks.ManholeHeight;
        Assert.True(top > highest + TownShapes.PavementHeight, $"{top} sous les pavés");
        Assert.Equal(SewerLooks.ManholeProud, top - highest - TownShapes.PavementHeight, 9);
        Assert.True(foot < highest);   // le reste est enfoui : elle ne flotte pas
        string sewer = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "SpaceView.Sewer.cs"));
        Assert.Contains("SewerLooks.ManholeBase(GroundHigh(", sewer);
        Assert.Contains("SewerLooks.OutfallYaw(x, z)", sewer);
    }

    /// <summary>
    /// Relecture indépendante de la PR #254 : le déversoir tourné (lacet non nul) garde ses proportions. L'échelle de la pose est appliquée
    /// dans les axes du modèle, puis le lacet : chaque arête du modèle garde la longueur voulue par la pose et les arêtes restent à angle
    /// droit. L'ancien ordre (lacet sous l'échelle) cisaillait : environ 30 % de moins en X à 18,8°.
    /// </summary>
    [Theory]
    [InlineData(18.8)]
    [InlineData(-37.0)]
    [InlineData(90.0)]
    public void Deversoir_tourne_sans_cisaillement(double yawDegrees)
    {
        var fit = SewerLooks.Fit(1.73, 1.29, 2.5, SewerLooks.OutfallWidth, SewerLooks.OutfallDepth, SewerLooks.OutfallHeight);
        Assert.False(fit.Turn);
        double a = yawDegrees * Math.PI / 180;
        (double X, double Z) Yaw((double X, double Z) v) => (v.X * Math.Cos(a) + v.Z * Math.Sin(a), -v.X * Math.Sin(a) + v.Z * Math.Cos(a));
        (double X, double Z) Scale((double X, double Z) v) => (v.X * fit.Sx, v.Z * fit.Sz);
        double Len((double X, double Z) v) => Math.Sqrt(v.X * v.X + v.Z * v.Z);
        (double, double) ex = (1.73, 0), ez = (0, 2.5);
        // Nouvel ordre : échelle de la pose, puis lacet de l'ensemble.
        var nx = Yaw(Scale(ex)); var nz = Yaw(Scale(ez));
        Assert.Equal(1.73 * fit.Sx, Len(nx), 9);
        Assert.Equal(2.5 * fit.Sz, Len(nz), 9);
        Assert.Equal(0.0, nx.X * nz.X + nx.Z * nz.Z, 9);   // à angle droit
        Assert.Equal(2.5 * fit.Sz / (1.73 * fit.Sx), Len(nz) / Len(nx), 9);
        // Ancien ordre (lacet sous l'échelle) : cisaillé dès que le lacet n'est pas un multiple de 90°.
        var ox = Scale(Yaw(ex)); var oz = Scale(Yaw(ez));
        if (Math.Abs(Math.Sin(2 * a)) > 0.1 && Math.Abs(fit.Sx - fit.Sz) > 1e-3)
            Assert.NotEqual(0.0, Math.Round(ox.X * oz.X + ox.Z * oz.Z, 6));
        // Le code Unity suit le nouvel ordre : échelle sur la pose, lacet sur l'ensemble, jamais sur la copie.
        string sewer = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "SpaceView.Sewer.cs"));
        int scale = sewer.IndexOf("pose.transform.localScale = ", StringComparison.Ordinal), turn = sewer.IndexOf("look.transform.localRotation = Quaternion.Euler(0f, yaw, 0f)", StringComparison.Ordinal);
        Assert.True(scale > 0 && turn > scale, "échelle puis lacet");
        Assert.DoesNotContain("look.transform.localScale", sewer);
        Assert.DoesNotContain("copy.transform.localRotation = Quaternion.Euler(0f, yaw", sewer);
        Assert.True(sewer.IndexOf("/// Côté Bourg (la surface)", StringComparison.Ordinal) > sewer.IndexOf("private static float GroundHigh", StringComparison.Ordinal));
    }

    /// <summary>
    /// Demande du 25 novembre 2026 : le rat animé de Tripo (rat géant rigué, sa marche) habille les rats et les rats géants ; présent dans
    /// Resources/Rats avec sa couleur et sa normale ; sans lui, le loup reste en repli (même place, même code).
    /// </summary>
    [Fact]
    public void Rat_anime_present_et_loup_en_repli()
    {
        string folder = Path.Combine(Lot93UnityPackageTests.Root, "unity", SewerLooks.RatAssetFolder);
        string fbx = Path.Combine(folder, SewerLooks.RatModel + ".fbx");
        Assert.True(File.Exists(fbx), fbx);
        Assert.True(new FileInfo(fbx).Length > 100_000, fbx);
        Assert.True(File.Exists(Path.Combine(folder, SewerLooks.RatModel + "_couleur.jpg")));
        Assert.True(File.Exists(Path.Combine(folder, SewerLooks.RatModel + "_normale.png")));
        Assert.Single(Directory.GetFiles(folder, "*.fbx"));
        Assert.EndsWith("Resources/" + SewerLooks.RatFolder.TrimEnd('/'), SewerLooks.RatAssetFolder);
        // Le squelette et la marche sont dans le FBX (prise « …|Walk »).
        Assert.Contains("Walk", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(fbx)), StringComparison.Ordinal);
        // Repli : la place reste le loup ; le jeu charge le rat, sinon le modèle de la place.
        Assert.Equal(StyleSlot.Wolf, StyleRules.ForCombatant("rat_egout1", false));
        string look = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "StyleLook.cs"));
        Assert.Contains("Resources.Load<GameObject>(SewerLooks.RatFolder + SewerLooks.RatModel)", look);
        Assert.Contains("rat != null ? rat : catalog.Model(slot, id)", look);
        string style = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Editor", "EdsStyle.cs"));
        Assert.Contains("RatModel(notes);", style);
        // Le petit rat est plus gris (même modèle) ; le rat géant garde ses couleurs.
        Assert.True(SewerLooks.IsSmallRat("rat_egout2") && SewerLooks.IsSmallRat("rat_egout5_v2"));
        Assert.False(SewerLooks.IsSmallRat("rat_geant_egout1") || SewerLooks.IsSmallRat("loup_echo_egout1") || SewerLooks.IsSmallRat("ratelier_bois"));
        Assert.True(SewerLooks.SmallRatTint.R < 1 && SewerLooks.SmallRatTint.B >= SewerLooks.SmallRatTint.R);
    }

    /// <summary>
    /// Le rat n'a que sa marche : à l'arrêt, la marche figée (jamais un pas sur place) ; la morsure, un élan par le code (pas d'animation
    /// d'attaque) ; la mort, couché sur le flanc (pas d'animation de mort).
    /// </summary>
    [Fact]
    public void Rat_attente_morsure_et_mort_a_partir_de_la_marche()
    {
        var clips = new[] { "rat_egout_squelette|rat_egout_squelette|Walk" };
        Assert.Equal(clips[0], StyleRules.Clip(clips, StyleMotion.Walk));
        Assert.True(StyleRules.Loops(clips[0]));
        Assert.Null(StyleRules.Clip(clips, StyleMotion.Idle));     // pas d'attente : la marche figée
        Assert.Null(StyleRules.Clip(clips, StyleMotion.Attack));   // pas d'attaque : l'élan par le code (Overlay)
        Assert.Null(StyleRules.Clip(clips, StyleMotion.Death));    // pas de mort : couché sur le flanc
        Assert.InRange(Locomotion.StillWalkRate, 0.01, 0.1);
        string look = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "StyleLook.cs"));
        Assert.Contains("_animation.SetSpeed(_walkClip, (float)Locomotion.StillWalkRate);", look);
    }

    /// <summary>Demande du 25 novembre 2026 : les rats ne grognent ni ne hurlent comme le loup (aucun son de rat dans le projet : rien).</summary>
    [Fact]
    public void Rats_sans_voix_de_loup()
    {
        Assert.False(StyleRules.Voiced("rat_egout1"));
        Assert.False(StyleRules.Voiced("rat_geant_egout2_v3"));
        Assert.True(StyleRules.Voiced("loup_echo_egout1"));
        Assert.True(StyleRules.Voiced("ratelier_bois"));
        string game = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "EdsGame.cs"));
        Assert.Contains("slot == StyleSlot.Wolf && StyleRules.Voiced(actor)", game);
        string voices = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "BeastVoices.cs"));
        Assert.Contains("!StyleRules.Voiced(id)", voices);
    }
}
