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
/// branchés par leur nom (kit préparé, pas encore généré : formes simples en repli).
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
}
