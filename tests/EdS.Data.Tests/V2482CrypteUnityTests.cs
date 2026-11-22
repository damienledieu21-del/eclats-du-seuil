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
/// Chantier V2.48.2 : les cryptes dans Unity. Pièces du kit de crypte par le nom des obstacles (<see cref="CryptLooks"/>), voûte sur l'écart
/// entre deux grilles, escalier de crypte sur les portes, dalle au sol de la chapelle (une trappe n'ouvre aucun mur) ; modèles branchés par
/// leur nom (kit préparé, non généré : formes simples en attendant).
/// </summary>
[Trait("Category", "World")]
public class V2482CrypteUnityTests
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
    public void Chaque_obstacle_des_cryptes_a_sa_piece_et_c_est_une_crypte()
    {
        foreach (string id in V2481CrypteTests.Crypts)
        {
            ZoneDefinition z = Zone(id);
            Assert.All(z.Setup.Space!.Obstacles, o => Assert.NotEqual(CryptPiece.None, CryptLooks.PieceFor(o.Id)));
            // Aucune pièce d'un autre kit : ni rideau d'eau, ni passerelle de la mine.
            Assert.All(z.Setup.Space!.Obstacles, o => Assert.True(CaveLooks.PieceFor(o.Id) == CavePiece.None && MineLooks.PieceFor(o.Id) == MinePiece.None, o.Id));
            Assert.True(CryptLooks.IsCrypt(z.Setup.Space!.Obstacles), id);
            Assert.False(MineLooks.IsMine(z.Setup.Space!.Obstacles), id);
        }
        foreach (string other in V2471MinesTests.Levels.Append("zone.grotte_cascade"))
            Assert.False(CryptLooks.IsCrypt(Zone(other).Setup.Space!.Obstacles), other);
        Assert.False(CryptLooks.IsCrypt(Array.Empty<SpaceObstacle>()));
        Assert.False(CryptLooks.IsCrypt(null!));
    }

    [Theory]
    [InlineData("mur_crypte_entree_ouest", CryptPiece.Wall)]
    [InlineData("mur_crypte", CryptPiece.Wall)]
    [InlineData("pilier_crypte_3", CryptPiece.Pillar)]
    [InlineData("gisant_crypte_7", CryptPiece.Effigy)]
    [InlineData("sarcophage_crypte_1", CryptPiece.Sarcophagus)]
    [InlineData("ossuaire_crypte_2", CryptPiece.Ossuary)]
    [InlineData("grille_crypte_tombeau_est", CryptPiece.Grille)]
    [InlineData("candelabre_crypte_5", CryptPiece.Candelabra)]
    [InlineData("bougies_crypte_1", CryptPiece.Candles)]
    [InlineData("autel_crypte_2", CryptPiece.Altar)]
    [InlineData("tombeau_abbe", CryptPiece.Tomb)]
    [InlineData("coffre_crypte_1", CryptPiece.Chest)]
    [InlineData("mur_cryptes", CryptPiece.None)]          // préfixe sans « _ » : rien
    [InlineData("tombeau_abbesse", CryptPiece.None)]
    [InlineData("coffre_mine", CryptPiece.None)]          // pièce du kit de mine
    [InlineData("eboulement_ruine_choeur_abbaye", CryptPiece.None)]
    [InlineData("", CryptPiece.None)]
    [InlineData(null, CryptPiece.None)]
    public void Pieces_du_kit_de_crypte_par_le_nom_de_l_obstacle(string? id, CryptPiece piece) => Assert.Equal(piece, CryptLooks.PieceFor(id!));

    [Theory]
    [InlineData("Assets/EdS/Modeles/Resources/Crypte/tombeau_abbe.fbx", "tombeau_abbe")]
    [InlineData("Assets\\EdS\\Modeles\\Resources\\Crypte\\voute_crypte.FBX", "voute_crypte")]
    [InlineData("Assets/EdS/Modeles/Resources/Crypte/sous/voute_crypte.fbx", null)]
    [InlineData("Assets/EdS/Modeles/Resources/Crypte/voute_crypte.glb", null)]
    [InlineData("Assets/EdS/Modeles/Resources/Mines/voute_crypte.fbx", null)]
    [InlineData("Assets/EdS/Modeles/Resources/Crypte/coffre_mine.fbx", null)]
    [InlineData("", null)]
    public void Nom_d_un_modele_du_kit_de_crypte(string path, string? name) => Assert.Equal(name, CryptLooks.NameOf(path));

    [Fact]
    public void Kit_de_treize_modeles_chaque_piece_a_son_modele()
    {
        Assert.Equal(13, CryptLooks.Models.Count);
        Assert.Equal(CryptLooks.Models.Count, CryptLooks.Models.Distinct().Count());
        Assert.Empty(CryptLooks.Models.Intersect(MineLooks.Models));
        Assert.Empty(CryptLooks.Models.Intersect(CaveLooks.Models));
        foreach (CryptPiece piece in Enum.GetValues<CryptPiece>())
        {
            string? model = CryptLooks.ModelFor(piece);
            if (piece == CryptPiece.None) Assert.Null(model);
            else if (piece == CryptPiece.Chest)
            {
                // Le coffre reprend celui du kit de mine, cherché dans son dossier.
                Assert.Equal("coffre_mine", model);
                Assert.Contains(model, MineLooks.Models);
                Assert.Equal(MineLooks.Folder, CryptLooks.FolderFor(piece));
            }
            else
            {
                Assert.Contains(model, CryptLooks.Models);
                Assert.Equal(CryptLooks.Folder, CryptLooks.FolderFor(piece));
            }
            Assert.True(CryptLooks.HeightOf(piece) > 0);
            Assert.Equal(piece is CryptPiece.Wall or CryptPiece.Pillar, CryptLooks.Occludes(piece));
        }
        // Chaque modèle du kit sert : par une pièce, sur les passages (voûte), aux portes (escalier) ou au sol de la chapelle (dalle).
        var used = Enum.GetValues<CryptPiece>().Select(CryptLooks.ModelFor).Append(CryptLooks.VaultModel).Append(CryptLooks.StairsModel).Append(CryptLooks.SlabModel);
        Assert.All(CryptLooks.Models, m => Assert.Contains(m, used));
        Assert.Equal("Crypte/", CryptLooks.Folder);
        Assert.EndsWith("Resources/" + CryptLooks.Folder.TrimEnd('/'), CryptLooks.AssetFolder);
    }

    [Fact]
    public void Voute_sur_l_ecart_entre_deux_grilles()
    {
        Assert.Equal(new[] { (-3.0, 20.0, 3.0, 21.5) }, CryptLooks.Arches(Zone("zone.crypte_abbaye").Setup.Space!.Obstacles));
        Assert.Equal(new[] { (-3.0, 14.0, 3.0, 15.5) }, CryptLooks.Arches(Zone("zone.crypte_tombeau").Setup.Space!.Obstacles));
        Assert.Empty(CryptLooks.Arches(Zone("zone.crypte_chapelle").Setup.Space!.Obstacles));   // pas de grille dans la petite crypte
    }

    [Fact]
    public void Voute_frontieres()
    {
        // Grilles jointives ou qui se chevauchent : pas d'écart ; bandes de Z disjointes ou juste jointives : rien.
        Assert.Empty(CryptLooks.Arches(new[] { Rect("grille_crypte_a", 0, 0, 1, 1), Rect("grille_crypte_b", 1, 0, 2, 1) }));
        Assert.Empty(CryptLooks.Arches(new[] { Rect("grille_crypte_a", 0, 0, 1, 1), Rect("grille_crypte_b", 0.5, 0, 2, 1) }));
        Assert.Empty(CryptLooks.Arches(new[] { Rect("grille_crypte_a", 0, 0, 1, 1), Rect("grille_crypte_b", 3, 1, 4, 2) }));
        // Ordre des données sans effet ; seul le Z commun compte ; un mur n'est pas une grille.
        var spans = CryptLooks.Arches(new[] { Rect("grille_crypte_b", 4, 0.5, 5, 2), Rect("mur_crypte", 1, 0, 4, 1), Rect("grille_crypte_a", 0, 0, 1, 1) });
        Assert.Equal(new[] { (1.0, 0.5, 4.0, 1.0) }, spans);
        // Deux passages sur deux bandes : deux voûtes.
        Assert.Equal(2, CryptLooks.Arches(new[] { Rect("grille_crypte_1", 0, 0, 1, 1), Rect("grille_crypte_2", 3, 0, 4, 1),
            Rect("grille_crypte_3", 0, 5, 1, 6), Rect("grille_crypte_4", 3, 5, 4, 6) }).Count);
        Assert.Empty(CryptLooks.Arches(null!));
    }

    [Theory]
    [InlineData(DoorKind.Stairs, "escalier_crypte")]
    [InlineData(DoorKind.Hatch, "escalier_crypte")]
    [InlineData(DoorKind.Door, "escalier_crypte")]
    [InlineData(DoorKind.Shaft, null)]
    public void Escalier_aux_portes_de_crypte(DoorKind kind, string? model) => Assert.Equal(model, CryptLooks.DoorModel(kind));

    [Fact]
    public void Portes_de_la_crypte_dans_la_carte_et_dalle_de_la_chapelle_au_sol()
    {
        string dir = Path.Combine(Path.GetTempPath(), "eds_v2482_" + Guid.NewGuid().ToString("N"));
        string save = Path.Combine(dir, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        try
        {
            SaveStore.Write(save, new WorldSave("zone.crypte_abbaye", 0, 0, Array.Empty<string>(), V11TestData.Hero()));
            AdventureSession a = V11TestData.Adventure(save);
            Assert.True(a.Map.Underground && a.Sheltered);
            Assert.Equal(new[] { ("sortie", DoorKind.Door, true), ("escalier_du_tombeau", DoorKind.Stairs, false) }, a.Map.Doors.Select(d => (d.Id, d.Kind, d.Outside)));
            Assert.True(CryptLooks.IsCrypt(a.Map.Obstacles));
        }
        finally { Directory.Delete(dir, true); }

        // La chapelle : la trappe n'a ni porte adossée ni ouverture de mur ; la sortie garde les siennes.
        ZoneDefinition chapelle = Zone("zone.chapelle");
        AdventureMap map = InterieursTests.MapOf(chapelle);
        MapDoor dalle = map.Doors.Single(d => d.Id == "secret_dalle");
        Assert.True(CryptLooks.OnFloor(dalle));
        Assert.False(CryptLooks.OnFloor(map.Doors.Single(d => d.Id == "sortie")));
        InteriorLayout layout = InteriorRules.Layout(map);
        Assert.Equal(new[] { "porte/sortie" }, layout.Pieces.Where(p => p.Prop == InteriorProp.Door).Select(p => p.Id));
        Assert.Throws<ArgumentNullException>(() => CryptLooks.OnFloor(null!));
    }

    [Fact]
    public void Pose_des_pieces_a_leur_hauteur_jamais_a_l_emprise()
    {
        // Propriété : un mur de 20 × 1,5 m, un gisant de 1 × 2,2 m ou un tombeau de 4 × 4 m gardent la hauteur de leur pièce.
        double[] sides = { 0.4, 1, 2.2, 4, 20 };
        foreach (CryptPiece piece in Enum.GetValues<CryptPiece>())
            foreach (double w in sides)
                foreach (double d in sides)
                {
                    var fit = MineLooks.Fit(2.0, 1.0, 1.0, w, d, CryptLooks.HeightOf(piece));
                    Assert.Equal(CryptLooks.HeightOf(piece), 1.0 * fit.Sy, 9);
                }
        Assert.Equal(CaveLooks.WallHeight, CryptLooks.HeightOf(CryptPiece.Wall));
        Assert.True(CryptLooks.VaultHeight > 3.4);   // le linteau de remplacement est sous la voûte
    }

    [Fact]
    public void Unity_cryptes_dessinees_et_kit_branche()
    {
        string cave = Unity("Assets", "EdS", "Game", "SpaceView.Cave.cs"), crypt = Unity("Assets", "EdS", "Game", "SpaceView.Crypt.cs");
        string interior = Unity("Assets", "EdS", "Game", "SpaceView.Interior.cs"), style = Unity("Assets", "EdS", "Editor", "EdsStyle.cs");
        Assert.Contains("_crypt = CryptLooks.IsCrypt(space.Obstacles);", cave);
        // La crypte passe avant la mine et avant le rideau d'eau.
        Assert.True(cave.IndexOf("CryptObstacle(o,", StringComparison.Ordinal) < cave.IndexOf("MineObstacle(o,", StringComparison.Ordinal));
        Assert.True(cave.IndexOf("CryptDoor(door,", StringComparison.Ordinal) < cave.IndexOf("MineDoor(door,", StringComparison.Ordinal));
        Assert.Contains("CryptArches(space, rock, rockTile)", cave);
        Assert.Contains("Resources.Load<GameObject>(key)", crypt);
        Assert.Contains("CryptLooks.FolderFor(piece), CryptLooks.ModelFor(piece)", crypt);
        Assert.Contains("CryptLooks.Occludes(piece)", crypt);   // murs et piliers : occulteurs
        Assert.Contains("Prism(o.Id, points, height, rock, true, rockTile)", crypt);   // et leurs formes de remplacement aussi
        Assert.Contains("CryptLooks.Arches(space.Obstacles)", crypt);
        Assert.Contains("_curtains.Add(", crypt);   // le jour de la nef suit le jour du dehors
        Assert.Contains("if (CryptLooks.OnFloor(door)) CryptSlab(door);", interior);
        Assert.Contains("!CryptLooks.OnFloor(door) && InteriorRules.NearestWall", interior);
        Assert.Contains("CryptPieces(notes);", style);
        Assert.Contains("CryptLooks.NameOf", style);
        // Aucun shader nouveau (mots réservés HLSL) ; scripts en C# 9 sans annotation de nullabilité.
        Assert.DoesNotContain("Shader.Find", crypt);
        Assert.DoesNotContain("#nullable", crypt);
        Assert.DoesNotContain("string?", crypt);
        Assert.DoesNotContain("GameObject?", crypt);
    }

    /// <summary>
    /// Kit de crypte préparé, pas généré (aucune dépense Tripo sans l'accord de Damien) : tant que le dossier manque, les formes simples ;
    /// s'il est là, il ne contient que des modèles reconnus, chacun avec sa couleur et sa normale.
    /// </summary>
    [Fact]
    public void Kit_de_crypte_reconnu_s_il_est_la()
    {
        string folder = Path.Combine(Lot93UnityPackageTests.Root, "unity", CryptLooks.AssetFolder);
        if (!Directory.Exists(folder)) return;
        foreach (string fbx in Directory.GetFiles(folder, "*.fbx"))
        {
            string? name = CryptLooks.NameOf(CryptLooks.AssetFolder + "/" + Path.GetFileName(fbx));
            Assert.NotNull(name);
            Assert.True(File.Exists(Path.Combine(folder, name + "_couleur.jpg")), name);
            Assert.True(File.Exists(Path.Combine(folder, name + "_normale.png")), name);
        }
    }
}
