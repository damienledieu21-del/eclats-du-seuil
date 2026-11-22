using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Chantier V2.47.2 : la mine de la carrière dans Unity. Pièces du kit de mine par le nom des obstacles (<see cref="MineLooks"/>), passerelle
/// sur l'écart de la rivière souterraine, ascenseur et échelles d'après le genre des portes, pas de rideau d'eau à la bouche ; modèles
/// branchés par leur nom (kit fait le 22 novembre 2026, formes simples en repli si un modèle manque).
/// </summary>
[Trait("Category", "World")]
public class V2472MinesUnityTests
{
    private static readonly string Scenarios = Path.Combine(AppContext.BaseDirectory, "scenarios");

    private static ZoneDefinition Level(string id) =>
        ZoneJsonReader.Read(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, id)), Scenarios, ShippedData,
            ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!).Zone!;

    private static SpaceObstacle Rect(string id, double x0, double z0, double x1, double z1) =>
        new(id, new[] { V(x0, z0), V(x1, z0), V(x1, z1), V(x0, z1) }, low: true);

    private static GroundVector V(double x, double z) => new(Fixed.FromRaw((long)Math.Round(x * 10000)), Fixed.FromRaw((long)Math.Round(z * 10000)));

    [Fact]
    public void Chaque_obstacle_de_la_mine_a_sa_piece_et_c_est_la_mine()
    {
        foreach (string id in V2471MinesTests.Levels)
        {
            ZoneDefinition z = Level(id);
            Assert.All(z.Setup.Space!.Obstacles, o =>
                Assert.True(MineLooks.PieceFor(o.Id) != MinePiece.None || CaveLooks.PieceFor(o.Id) != CavePiece.None, id + " : " + o.Id));
            Assert.True(MineLooks.IsMine(z.Setup.Space!.Obstacles), id);
            // Une pièce n'est jamais des deux kits à la fois (la mine passe après la grotte).
            Assert.All(z.Setup.Space!.Obstacles, o => Assert.False(MineLooks.PieceFor(o.Id) != MinePiece.None && CaveLooks.PieceFor(o.Id) != CavePiece.None, o.Id));
        }
        Assert.False(MineLooks.IsMine(Level("zone.grotte_cascade").Setup.Space!.Obstacles));   // la grotte garde son rideau d'eau
        Assert.False(MineLooks.IsMine(Array.Empty<SpaceObstacle>()));
    }

    [Theory]
    [InlineData("paroi_mine", MinePiece.Wall)]
    [InlineData("paroi_mine_sud_ouest", MinePiece.Wall)]
    [InlineData("etai_mine_3", MinePiece.Prop)]
    [InlineData("rails_mine_1", MinePiece.Rails)]
    [InlineData("wagonnet_mine_2", MinePiece.Cart)]
    [InlineData("filon_mine_1", MinePiece.Vein)]
    [InlineData("effondrement_mine_1", MinePiece.Collapse)]
    [InlineData("caisses_mine_1", MinePiece.Crates)]
    [InlineData("cristaux_seuil_6", MinePiece.Crystals)]
    [InlineData("coffre_mine", MinePiece.Chest)]
    [InlineData("riviere_mine_est", MinePiece.River)]
    [InlineData("paroi_mineur", MinePiece.None)]        // préfixe sans « _ » : rien
    [InlineData("paroi_grotte_mine_nord_est", MinePiece.None)]   // pièce du kit de grotte
    [InlineData("riviere_vallee1", MinePiece.None)]
    [InlineData("", MinePiece.None)]
    [InlineData(null, MinePiece.None)]
    public void Pieces_du_kit_de_mine_par_le_nom_de_l_obstacle(string? id, MinePiece piece) => Assert.Equal(piece, MineLooks.PieceFor(id!));

    [Theory]
    [InlineData("Assets/EdS/Modeles/Resources/Mines/ascenseur_mine.fbx", "ascenseur_mine")]
    [InlineData("Assets\\EdS\\Modeles\\Resources\\Mines\\paroi_mine.FBX", "paroi_mine")]
    [InlineData("Assets/EdS/Modeles/Resources/Mines/sous/paroi_mine.fbx", null)]
    [InlineData("Assets/EdS/Modeles/Resources/Mines/paroi_mine.glb", null)]
    [InlineData("Assets/EdS/Modeles/Resources/Grotte/paroi_mine.fbx", null)]
    [InlineData("Assets/EdS/Modeles/Resources/Mines/inconnu.fbx", null)]
    [InlineData("", null)]
    public void Nom_d_un_modele_du_kit_de_mine(string path, string? name) => Assert.Equal(name, MineLooks.NameOf(path));

    [Fact]
    public void Kit_de_treize_modeles_chaque_piece_a_son_modele()
    {
        Assert.Equal(13, MineLooks.Models.Count);
        Assert.Equal(MineLooks.Models.Count, MineLooks.Models.Distinct().Count());
        Assert.Empty(MineLooks.Models.Intersect(CaveLooks.Models));
        foreach (MinePiece piece in Enum.GetValues<MinePiece>())
        {
            string? model = MineLooks.ModelFor(piece);
            if (piece is MinePiece.None or MinePiece.River) Assert.Null(model);   // la rivière est de l'eau
            else Assert.Contains(model, MineLooks.Models);
            Assert.True(MineLooks.HeightOf(piece) >= 0);
        }
        Assert.Contains(MineLooks.LiftModel, MineLooks.Models);
        Assert.Contains(MineLooks.LaddersModel, MineLooks.Models);
        Assert.Contains(MineLooks.BridgeModel, MineLooks.Models);
        Assert.Contains(MineLooks.LanternModel, MineLooks.Models);
    }

    [Fact]
    public void Passerelle_sur_l_ecart_de_la_riviere_souterraine()
    {
        var spans = MineLooks.Bridges(Level("zone.mine_cristaux").Setup.Space!.Obstacles);
        Assert.Equal(new[] { (-1.5, 14.0, 1.5, 18.0) }, spans);
        Assert.Empty(MineLooks.Bridges(Level("zone.mine_galeries").Setup.Space!.Obstacles));   // pas de rivière au premier niveau
    }

    [Fact]
    public void Passerelle_frontieres()
    {
        // Bras qui se touchent ou se chevauchent : pas d'écart, pas de passerelle ; bandes de Z disjointes ou juste jointives : rien.
        Assert.Empty(MineLooks.Bridges(new[] { Rect("riviere_mine_a", 0, 0, 5, 2), Rect("riviere_mine_b", 5, 0, 9, 2) }));
        Assert.Empty(MineLooks.Bridges(new[] { Rect("riviere_mine_a", 0, 0, 5, 2), Rect("riviere_mine_b", 4, 0, 9, 2) }));
        Assert.Empty(MineLooks.Bridges(new[] { Rect("riviere_mine_a", 0, 0, 5, 2), Rect("riviere_mine_b", 6, 2, 9, 4) }));
        // Ordre des données sans effet ; seul le Z commun compte ; les autres obstacles ne sont pas de l'eau.
        var spans = MineLooks.Bridges(new[] { Rect("riviere_mine_b", 6, 1, 9, 3), Rect("paroi_mine", 5, 0, 6, 9), Rect("riviere_mine_a", 0, 0, 5, 2) });
        Assert.Equal(new[] { (5.0, 1.0, 6.0, 2.0) }, spans);
        // Trois bras : deux passerelles, d'ouest en est.
        Assert.Equal(2, MineLooks.Bridges(new[] { Rect("riviere_mine_1", 0, 0, 2, 1), Rect("riviere_mine_2", 3, 0, 5, 1), Rect("riviere_mine_3", 7, 0, 9, 1) }).Count);
        Assert.Empty(MineLooks.Bridges(null!));
    }

    [Fact]
    public void Portes_de_la_mine_avec_leur_genre_dans_la_carte()
    {
        string dir = Path.Combine(Path.GetTempPath(), "eds_v2472_" + Guid.NewGuid().ToString("N"));
        string save = Path.Combine(dir, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        try
        {
            SaveStore.Write(save, new WorldSave("zone.mine_filons", 0, 0, Array.Empty<string>(), V11TestData.Hero()));
            AdventureSession a = V11TestData.Adventure(save);
            Assert.True(a.Map.Underground);
            Assert.Equal(new[] { ("ascenseur", DoorKind.Shaft, false), ("echelles", DoorKind.Stairs, false) }, a.Map.Doors.Select(d => (d.Id, d.Kind, d.Outside)));
            Assert.True(MineLooks.IsMine(a.Map.Obstacles));
        }
        finally { Directory.Delete(dir, true); }
        Assert.Equal(DoorKind.Door, new MapDoor("porte", "Porte", null, 0, 0).Kind);   // par défaut : une porte
    }

    [Fact]
    public void Unity_mine_dessinee_sans_rideau_d_eau()
    {
        static string Game(string name) => File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", name));
        string cave = Game("SpaceView.Cave.cs"), mine = Game("SpaceView.Mine.cs");
        string style = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Editor", "EdsStyle.cs"));
        Assert.Contains("_mine = MineLooks.IsMine(space.Obstacles);", cave);
        Assert.Contains("if (piece == CavePiece.None && MineObstacle(o, rock, rockTile, wood, woodTile)) continue;", cave);
        // Dans la mine, chaque porte passe par MineDoor avant le rideau d'eau.
        Assert.True(cave.IndexOf("MineDoor(door,", StringComparison.Ordinal) < cave.IndexOf("if (!door.Outside) continue;", StringComparison.Ordinal));
        Assert.Contains("Resources.Load<GameObject>(MineLooks.Folder + name)", mine);
        Assert.Contains("door.Kind == DoorKind.Shaft ? MineLooks.LiftModel", mine);
        Assert.Contains("MineLooks.Bridges(space.Obstacles)", mine);
        Assert.Contains("_waters.Add(", mine);   // la rivière ondule comme l'eau du dehors
        Assert.Contains("MinePieces(notes);", style);
        Assert.Contains("MineLooks.NameOf", style);
        // Aucun shader nouveau (mots réservés HLSL) : la mine n'utilise que les matériaux existants.
        Assert.DoesNotContain("Shader.Find", mine);
    }

    /// <summary>
    /// Kit de mine livré (décision de Damien du 22 novembre 2026 : 390 crédits validés, dépense faite) : les treize modèles finis dans
    /// Blender sont dans Resources/Mines, chacun avec sa couleur et sa normale, et reconnus par leur nom ; la préparation leur donne leur matière.
    /// </summary>
    [Fact]
    public void Kit_de_mine_present_et_reconnu()
    {
        string folder = Path.Combine(Lot93UnityPackageTests.Root, "unity", MineLooks.AssetFolder);
        foreach (string name in MineLooks.Models)
        {
            string fbx = Path.Combine(folder, name + ".fbx");
            Assert.True(File.Exists(fbx), fbx);
            Assert.True(new FileInfo(fbx).Length > 10_000, fbx);
            Assert.True(File.Exists(Path.Combine(folder, name + "_couleur.jpg")), name);
            Assert.True(File.Exists(Path.Combine(folder, name + "_normale.png")), name);
            Assert.Equal(name, MineLooks.NameOf(MineLooks.AssetFolder + "/" + name + ".fbx"));
        }
        Assert.Equal(13, MineLooks.Models.Count);
        Assert.Equal(MineLooks.Models.Count, Directory.GetFiles(folder, "*.fbx").Length);   // rien d'autre, rien d'oublié
        Assert.Equal("Mines/", MineLooks.Folder);
        Assert.EndsWith("Resources/" + MineLooks.Folder.TrimEnd('/'), MineLooks.AssetFolder);   // Resources.Load(Folder + nom) trouve le fichier
    }

    /// <summary>
    /// Pose d'un modèle (<see cref="MineLooks.Fit"/>) : la hauteur ne suit jamais l'emprise. Les rails (4 × 0,25 × 1,4 m, longs sur X) sur
    /// la voie de 1,4 × 14 m (le long de Z) tournent d'un quart de tour et restent à 0,25 m ; la passerelle reste à 1,2 m sur 5 m de long ;
    /// une paroi de 25 × 20 m reste à la hauteur des murs.
    /// </summary>
    [Theory]
    [InlineData(4.0, 0.25, 1.4, 1.4, 14.0, MinePiece.Rails, true, 1.0, 1.0, 3.5)]
    [InlineData(4.0, 0.25, 1.4, 22.0, 1.2, MinePiece.Rails, false, 5.5, 1.0, 1.2 / 1.4)]
    [InlineData(8.0, 6.57, 4.06, 25.0, 20.0, MinePiece.Wall, false, 3.125, 6.0 / 6.57, 20.0 / 4.06)]
    [InlineData(0.8, 3.2, 0.8, 0.6, 0.6, MinePiece.Prop, false, 0.75, 1.0, 0.75)]
    public void Pose_d_un_modele_hauteur_par_piece(double mx, double my, double mz, double width, double depth, MinePiece piece, bool turn, double sx, double sy, double sz)
    {
        var fit = MineLooks.Fit(mx, my, mz, width, depth, MineLooks.ModelHeightOf(piece));
        Assert.Equal(turn, fit.Turn);
        Assert.Equal(sx, fit.Sx, 9);
        Assert.Equal(sy, fit.Sy, 9);
        Assert.Equal(sz, fit.Sz, 9);
        Assert.Equal(MineLooks.ModelHeightOf(piece), my * fit.Sy, 9);   // hauteur posée = hauteur de la pièce
    }

    [Fact]
    public void Pose_d_un_modele_jamais_plus_haut_que_sa_piece()
    {
        // Propriété : pour toute emprise (de 0,1 à 60 m de côté), la hauteur posée est celle de la pièce, quelle que soit l'emprise.
        double[] sides = { 0.1, 0.6, 1.4, 3, 5, 14, 25, 60 };
        foreach (MinePiece piece in Enum.GetValues<MinePiece>())
            foreach (double w in sides)
                foreach (double d in sides)
                {
                    var fit = MineLooks.Fit(4.0, 0.25, 1.4, w, d, MineLooks.ModelHeightOf(piece));
                    Assert.Equal(MineLooks.ModelHeightOf(piece), 0.25 * fit.Sy, 9);
                    double footX = fit.Turn ? 1.4 : 4.0, footZ = fit.Turn ? 4.0 : 1.4;
                    Assert.Equal(w, footX * fit.Sx, 9);
                    Assert.Equal(d, footZ * fit.Sz, 9);
                }
        // Passerelle : 5 m de long sur X dans le modèle, posée le long de Z sur la rivière.
        var bridge = MineLooks.Fit(5.0, 1.33, 1.81, 3.0, 6.0, MineLooks.BridgeHeight);
        Assert.True(bridge.Turn);
        Assert.Equal(MineLooks.BridgeHeight, 1.33 * bridge.Sy, 9);
        // Tailles nulles : aucune pose.
        Assert.Equal((false, 1.0, 1.0, 1.0), MineLooks.Fit(0, 1, 1, 1, 1, 1));
        Assert.Equal((false, 1.0, 1.0, 1.0), MineLooks.Fit(1, 1, 1, 1, 1, 0));
    }

    [Fact]
    public void Unity_pose_les_modeles_a_la_hauteur_de_leur_piece()
    {
        string mine = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "SpaceView.Mine.cs"));
        Assert.Contains("MineLooks.Fit(b.size.x, b.size.y, b.size.z, x1 - x0, z1 - z0, height)", mine);
        Assert.Contains("(float)MineLooks.ModelHeightOf(piece)", mine);
        Assert.Contains("(float)MineLooks.BridgeHeight", mine);
        Assert.DoesNotContain("Mathf.Max(sx, sz)", mine);   // l'ancienne hauteur, étirée au plus grand côté de l'emprise
    }
}
