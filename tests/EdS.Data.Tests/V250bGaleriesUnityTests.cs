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
/// Chantier V2.50b : les galeries sous les pierres du Seuil dans Unity. Pièces du kit de galeries cristallines par le nom des obstacles
/// (<see cref="GalleryLooks"/>), ponts de pierre sur l'écart entre deux tronçons du gouffre, faille contre la paroi aux portes de genre faille,
/// entrée entre les pierres levées côté surface ; modèles branchés par leur nom (kit Tripo préparé, non dépensé ; formes simples en repli).
/// </summary>
[Trait("Category", "World")]
public class V250bGaleriesUnityTests
{
    private static ZoneDefinition Zone(string id) => V16TestData.Interiors.Single(z => z.Id == id);

    private static SpaceObstacle Rect(string id, double x0, double z0, double x1, double z1) =>
        new(id, new[] { V(x0, z0), V(x1, z0), V(x1, z1), V(x0, z1) });

    private static GroundVector V(double x, double z) => new(Fixed.FromRaw((long)Math.Round(x * 10000)), Fixed.FromRaw((long)Math.Round(z * 10000)));

    private static string Unity(params string[] path) => File.ReadAllText(Path.Combine(new[] { Lot93UnityPackageTests.Root, "unity" }.Concat(path).ToArray()));

    [Fact]
    public void Chaque_obstacle_des_galeries_a_sa_piece_et_c_est_une_galerie()
    {
        foreach (string id in V250aGaleriesTests.Galleries)
        {
            var obstacles = Zone(id).Setup.Space!.Obstacles;
            Assert.All(obstacles, o => Assert.NotEqual(GalleryPiece.None, GalleryLooks.PieceFor(o.Id)));
            Assert.True(GalleryLooks.IsGallery(obstacles));
            // Aucun autre kit ne la revendique : ni grotte, ni mine, ni crypte, ni égout.
            Assert.All(obstacles, o => Assert.Equal(CavePiece.None, CaveLooks.PieceFor(o.Id)));
            Assert.False(SewerLooks.IsSewer(obstacles));
            Assert.False(MineLooks.IsMine(obstacles));
            Assert.False(CryptLooks.IsCrypt(obstacles));
        }
        // Et aucun autre dessous n'est pris pour une galerie.
        foreach (ZoneDefinition z in V16TestData.Interiors.Where(z => z.Underground && !V250aGaleriesTests.Galleries.Contains(z.Id)))
            Assert.False(GalleryLooks.IsGallery(z.Setup.Space!.Obstacles), z.Id);
    }

    [Theory]
    [InlineData("paroi_cristal_entree_ouest", GalleryPiece.Wall)]
    [InlineData("eclat_geant_salle_3", GalleryPiece.GiantShard)]
    [InlineData("amas_cristal_galeries_1", GalleryPiece.Crystals)]
    [InlineData("pilier_cristal_salle_2", GalleryPiece.Pillar)]
    [InlineData("gouffre_cristal_centre", GalleryPiece.Chasm)]
    [InlineData("autel_eclats_coeur", GalleryPiece.Altar)]
    [InlineData("eboulis_cristal_galeries", GalleryPiece.Rubble)]
    [InlineData("eclat_geant", GalleryPiece.GiantShard)]
    [InlineData("eclat_geantx", GalleryPiece.None)]
    [InlineData("cristaux_seuil_1", GalleryPiece.None)]      // les cristaux du fond de la mine restent à la mine
    [InlineData("cristaux_grotte_1", GalleryPiece.None)]
    [InlineData("", GalleryPiece.None)]
    [InlineData(null, GalleryPiece.None)]
    public void Pieces_du_kit_par_le_nom_de_l_obstacle(string? id, GalleryPiece piece) => Assert.Equal(piece, GalleryLooks.PieceFor(id!));

    [Theory]
    [InlineData("Assets/EdS/Modeles/Resources/Galeries/eclat_geant.fbx", "eclat_geant")]
    [InlineData("Assets\\EdS\\Modeles\\Resources\\Galeries\\pont_pierre.FBX", "pont_pierre")]
    [InlineData("Assets/EdS/Modeles/Resources/Galeries/sous/eclat_geant.fbx", null)]
    [InlineData("Assets/EdS/Modeles/Resources/Galeries/eclat_geant.glb", null)]
    [InlineData("Assets/EdS/Modeles/Resources/Galeries/inconnu.fbx", null)]
    [InlineData("Assets/EdS/Modeles/Resources/Egouts/mur_egout.fbx", null)]
    [InlineData("", null)]
    public void Nom_d_un_modele_du_kit(string path, string? name) => Assert.Equal(name, GalleryLooks.NameOf(path));

    [Fact]
    public void Kit_de_onze_modeles_chaque_piece_a_son_modele()
    {
        Assert.Equal(new[] { "paroi_cristal", "eclat_geant", "amas_cristal", "pilier_cristal", "gouffre_cristal", "pont_pierre", "autel_eclats", "eboulis_cristal",
            "faille_eclats", "entree_galeries", "voute_cristal" }, GalleryLooks.Models);
        Assert.Equal(GalleryLooks.Models.Count, GalleryLooks.Models.Distinct().Count());
        foreach (GalleryPiece piece in Enum.GetValues<GalleryPiece>().Where(p => p != GalleryPiece.None))
        {
            Assert.Contains(GalleryLooks.ModelFor(piece)!, GalleryLooks.Models);
            Assert.InRange(GalleryLooks.HeightOf(piece), 0.5, CaveLooks.WallHeight);
        }
        Assert.Null(GalleryLooks.ModelFor(GalleryPiece.None));
        Assert.Equal(new[] { GalleryPiece.Wall, GalleryPiece.GiantShard, GalleryPiece.Pillar }, Enum.GetValues<GalleryPiece>().Where(GalleryLooks.Occludes));
        // Les pièces hautes du rendu sont exactement les obstacles qui ne sont pas bas (on tire par-dessus les autres).
        foreach (string id in V250aGaleriesTests.Galleries)
            foreach (SpaceObstacle o in Zone(id).Setup.Space!.Obstacles)
                Assert.Equal(!o.Low, GalleryLooks.Occludes(GalleryLooks.PieceFor(o.Id)));
        // La liste de l'atelier : 11 modèles nommés comme dans le jeu, à 30 crédits, 330 au plus (lancé par Damien le 26 novembre 2026).
        string lab = Path.Combine(Path.GetDirectoryName(Lot93UnityPackageTests.Root)!, "Eclats_du_Seuil_AssetLab", "Galeries", "modeles.json");
        if (File.Exists(lab))
        {
            var names = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(lab))!["modeles"]!.AsArray().Select(m => (string)m!["nom"]!).ToList();
            Assert.Equal(GalleryLooks.Models, names);
        }
    }

    [Fact]
    public void Ponts_de_pierre_sur_le_gouffre()
    {
        var bridges = GalleryLooks.Bridges(Zone("zone.galeries_seuil").Setup.Space!.Obstacles);
        Assert.Equal(new[] { (-18.0, 18.0, -14.0, 21.0), (14.0, 18.0, 18.0, 21.0) }, bridges);
        // Le pont couvre tout l'écart et déborde d'un demi-mètre sur chaque rive.
        Assert.All(bridges, b => Assert.Equal((b.MinX, b.MinZ - 0.5, b.MaxX, b.MaxZ + 0.5), GalleryLooks.BridgeSpan(b)));
        Assert.Empty(GalleryLooks.Bridges(Zone("zone.galeries_eclats").Setup.Space!.Obstacles));
        // Deux tronçons qui se touchent ou ne partagent aucune bande : pas de pont.
        Assert.Empty(GalleryLooks.Bridges(new[] { Rect("gouffre_cristal_a", 0, 0, 4, 2), Rect("gouffre_cristal_b", 4, 0, 8, 2) }));
        Assert.Empty(GalleryLooks.Bridges(new[] { Rect("gouffre_cristal_a", 0, 0, 4, 2), Rect("gouffre_cristal_b", 6, 3, 8, 5) }));
        Assert.Empty(GalleryLooks.Bridges(null!));
    }

    [Fact]
    public void Faille_aux_portes_de_genre_faille_seulement()
    {
        Assert.Equal(GalleryLooks.RiftModel, GalleryLooks.DoorModel(new MapDoor("faille", "x", null, 0, 0, kind: DoorKind.Rift)));
        Assert.Null(GalleryLooks.DoorModel(new MapDoor("sortie", "x", null, 0, 0, true)));
        Assert.Null(GalleryLooks.DoorModel(new MapDoor("x", "x", null, 0, 0, kind: DoorKind.Stairs)));
        Assert.Throws<ArgumentNullException>(() => GalleryLooks.DoorModel(null!));
        // Contre le bord le plus proche, centrée sur la porte, sans toucher la place d'arrivée.
        foreach (ZoneDefinition z in V250aGaleriesTests.Galleries.Select(Zone))
            foreach (Door d in z.Doors.Where(d => d.Kind == DoorKind.Rift))
            {
                double x = d.Position.X.Raw / 1e4, zz = d.Position.Z.Raw / 1e4;
                var s = z.Setup.Space!;
                var r = GalleryLooks.RiftAt(x, zz, s.Min.X.Raw / 1e4, s.Min.Z.Raw / 1e4, s.Max.X.Raw / 1e4, s.Max.Z.Raw / 1e4);
                Assert.NotNull(r);
                Assert.Equal(GalleryLooks.RiftWidth, Math.Max(r!.Value.MaxX - r.Value.MinX, r.Value.MaxZ - r.Value.MinZ), 9);
                double gap = Math.Max(Math.Max(r.Value.MinX - x, x - r.Value.MaxX), Math.Max(r.Value.MinZ - zz, zz - r.Value.MaxZ));
                Assert.True(gap >= 0.5, $"{z.Id} / {d.Id} : {gap}");   // la place d'arrivée reste libre
                // Collée à la paroi du bord : l'un de ses côtés est sur les bornes de l'aire.
                Assert.True(r.Value.MinX == s.Min.X.Raw / 1e4 || r.Value.MaxX == s.Max.X.Raw / 1e4 || r.Value.MinZ == s.Min.Z.Raw / 1e4 || r.Value.MaxZ == s.Max.Z.Raw / 1e4);
            }
        Assert.Null(GalleryLooks.RiftAt(0, -3.0, -10, -4, 10, 10));     // porte à 1 m du bord : pas la place
        Assert.NotNull(GalleryLooks.RiftAt(0, -2.9999, -10, -4, 10, 10));
    }

    [Fact]
    public void Entree_entre_les_pierres_levees_tournee_vers_le_cercle()
    {
        Assert.Equal(GalleryLooks.EntranceModel, GalleryLooks.SurfaceModel("porte_galeries_seuil"));
        Assert.Null(GalleryLooks.SurfaceModel("porte_egouts_marche"));
        Assert.Null(GalleryLooks.SurfaceModel(""));
        var under = GrandeCarteLieux.Vallons.Value.Doors.Where(d => d.To == "zone.galeries_seuil").Select(d => d.Id).ToList();
        Assert.Equal(new[] { "porte_galeries_seuil" }, under);
        // Au nord du centre du cercle, les marches descendent vers le sud (vers le centre) : lacet proche de 180° (le cercle est un peu à l'ouest : environ −160°).
        Door door = GrandeCarteLieux.Vallons.Value.DoorOf("porte_galeries_seuil")!;
        double yaw = GalleryLooks.EntranceYaw(door.Position.X.Raw / 1e4, door.Position.Z.Raw / 1e4);
        Assert.InRange(Math.Abs(yaw), 150, 180);
        Assert.Equal(90.0, GalleryLooks.EntranceYaw(1500, -625), 9);    // à l'ouest du cercle : vers l'est
        Assert.Equal(0.0, GalleryLooks.EntranceYaw(1565, -700), 9);     // au sud : vers le nord
    }

    [Fact]
    public void Paroi_et_gouffre_tuiles_sans_trou_les_autres_pieces_sans_etirement()
    {
        int tiled = 0;
        foreach (ZoneDefinition z in V250aGaleriesTests.Galleries.Select(Zone))
            foreach (SpaceObstacle o in z.Setup.Space!.Obstacles)
            {
                GalleryPiece piece = GalleryLooks.PieceFor(o.Id);
                double x0 = o.Points.Min(p => p.X.Raw) / 1e4, x1 = o.Points.Max(p => p.X.Raw) / 1e4, z0 = o.Points.Min(p => p.Z.Raw) / 1e4, z1 = o.Points.Max(p => p.Z.Raw) / 1e4;
                if (GalleryLooks.TileOf(piece) is not (double, double) model)
                {
                    // Pièce d'un seul tenant : jamais étirée de plus de 1,5 fois sur un axe par rapport à l'autre.
                    var fit = SewerLooks.Fit(2, 2, 2, x1 - x0, z1 - z0, GalleryLooks.HeightOf(piece));
                    Assert.True(Math.Max(fit.Sx, fit.Sz) <= Math.Min(fit.Sx, fit.Sz) * CryptLooks.MaxStretch + 1e-9, o.Id);
                    continue;
                }
                bool alongX = x1 - x0 >= z1 - z0;
                var tiles = SewerLooks.WallTiles(x0, z0, x1, z1, model.Item1, model.Item2);
                Assert.NotEmpty(tiles);
                double reach = alongX ? x0 : z0;
                foreach (var t in tiles)
                {
                    Assert.Equal(reach, alongX ? t.MinX : t.MinZ, 9);   // bout à bout : aucun trou
                    reach = alongX ? t.MaxX : t.MaxZ;
                    Assert.Equal(alongX ? (z0, z1) : (x0, x1), alongX ? (t.MinZ, t.MaxZ) : (t.MinX, t.MaxX));   // toute l'épaisseur
                    var fit = SewerLooks.TileFit(model.Item1, 4.0, model.Item2, t.MaxX - t.MinX, t.MaxZ - t.MinZ, 4.0);
                    double placedX = (fit.Turn ? model.Item2 : model.Item1) * fit.Sx, placedZ = (fit.Turn ? model.Item1 : model.Item2) * fit.Sz;
                    Assert.Equal(t.MaxX - t.MinX, placedX, 9);
                    Assert.Equal(t.MaxZ - t.MinZ, placedZ, 9);
                }
                Assert.Equal(alongX ? x1 : z1, reach, 9);
                tiled++;
            }
        Assert.Equal(2 + 3 + 2, tiled);   // deux parois et trois tronçons du gouffre ; deux parois dans la salle
        Assert.Equal((8.0, 3.12), GalleryLooks.TileOf(GalleryPiece.Wall));   // mesurées sur les modèles finis
        Assert.Equal((7.7, 5.06), GalleryLooks.TileOf(GalleryPiece.Chasm));
        Assert.Null(GalleryLooks.TileOf(GalleryPiece.GiantShard));
    }

    /// <summary>
    /// Kit de décor généré par Damien le 26 novembre 2026 (G01 à G11, 220 crédits réels), fini dans Blender (Galeries/finir_galeries.py :
    /// un maillage, allégé de moitié, pivot au sol, textures de 1024 px) : les onze modèles sont dans Resources/Galeries, chacun avec sa couleur
    /// et sa normale, reconnus par leur nom ; les formes simples restent en repli si l'un manque.
    /// </summary>
    [Fact]
    public void Kit_de_galeries_present_et_reconnu()
    {
        string folder = Path.Combine(Lot93UnityPackageTests.Root, "unity", GalleryLooks.AssetFolder);
        foreach (string name in GalleryLooks.Models)
        {
            string fbx = Path.Combine(folder, name + ".fbx");
            Assert.True(File.Exists(fbx), fbx);
            Assert.InRange(new FileInfo(fbx).Length, 10_000, 2_000_000);   // allégé (les GLB bruts font 3 à 5 Mo)
            Assert.True(File.Exists(Path.Combine(folder, name + "_couleur.jpg")), name);
            Assert.True(File.Exists(Path.Combine(folder, name + "_normale.png")), name);
            Assert.Equal(name, GalleryLooks.NameOf(GalleryLooks.AssetFolder + "/" + name + ".fbx"));
        }
        Assert.Equal(11, GalleryLooks.Models.Count);
        Assert.Equal(GalleryLooks.Models.Count, Directory.GetFiles(folder, "*.fbx").Length);   // rien d'autre, rien d'oublié
        Assert.EndsWith("Resources/" + GalleryLooks.Folder.TrimEnd('/'), GalleryLooks.AssetFolder);
        // Repli : formes simples si un modèle manque.
        string gallery = Unity("Assets", "EdS", "Game", "SpaceView.Gallery.cs");
        Assert.Contains("GiantShardShape(o.Id, cx, cz", gallery);
        Assert.Contains("Box(\"Pont de pierre (dalle)\"", gallery);
        Assert.Contains("Box(\"Faille (\"", gallery);
        Assert.Contains("Box(\"Entrée des galeries (\"", gallery);
        Assert.Contains("GalleryLooks.ChasmPose()", gallery);
        Assert.Contains("GalleryLooks.BridgeSlabs(", gallery);
    }

    [Fact]
    public void Gouffre_enfonce_dans_le_sol_levres_au_ras()
    {
        var pose = GalleryLooks.ChasmPose();
        Assert.Equal(-GalleryLooks.ChasmDepth, pose.Base);
        Assert.Equal(GalleryLooks.ChasmLip, pose.Base + pose.Height, 9);   // le haut du modèle juste au-dessus du sol
        Assert.InRange(GalleryLooks.ChasmLip, 0.05, 0.3);
        Assert.True(pose.Base < 0);
    }

    /// <summary>
    /// Relecture indépendante des PR #256 et #258 : le sol plein de la grotte recouvrait le gouffre enfoncé. Le sol des galeries est découpé
    /// autour de chaque tronçon : les morceaux ne se chevauchent pas, couvrent tout le sol hors du gouffre et rien du gouffre ; les ponts
    /// franchissent donc un vrai creux.
    /// </summary>
    [Fact]
    public void Sol_decoupe_autour_du_gouffre()
    {
        var space = Zone("zone.galeries_seuil").Setup.Space!;
        double minX = space.Min.X.Raw / 1e4 - 2, minZ = space.Min.Z.Raw / 1e4 - 2, maxX = space.Max.X.Raw / 1e4 + 2, maxZ = space.Max.Z.Raw / 1e4 + 2;
        var holes = GalleryLooks.ChasmHoles(space.Obstacles);
        Assert.Equal(new[] { (-28.0, 18.0, -18.0, 21.0), (-14.0, 18.0, 14.0, 21.0), (18.0, 18.0, 28.0, 21.0) }, holes.OrderBy(h => h.MinX));
        var floor = GalleryLooks.FloorPieces(minX, minZ, maxX, maxZ, holes);
        Assert.All(floor, f => Assert.True(f.MaxX > f.MinX && f.MaxZ > f.MinZ));
        double area = floor.Sum(f => (f.MaxX - f.MinX) * (f.MaxZ - f.MinZ));
        double holeArea = holes.Sum(h => (h.MaxX - h.MinX) * (h.MaxZ - h.MinZ));
        Assert.Equal((maxX - minX) * (maxZ - minZ) - holeArea, area, 6);   // tout le sol hors des trous…
        for (int i = 0; i < floor.Count; i++)                                // …sans chevauchement…
            for (int j = i + 1; j < floor.Count; j++)
                Assert.False(floor[i].MinX < floor[j].MaxX && floor[j].MinX < floor[i].MaxX && floor[i].MinZ < floor[j].MaxZ && floor[j].MinZ < floor[i].MaxZ);
        foreach (var f in floor)                                             // …et rien dans le gouffre
            Assert.DoesNotContain(holes, h => f.MinX < h.MaxX && h.MinX < f.MaxX && f.MinZ < h.MaxZ && h.MinZ < f.MaxZ);
        // Le milieu de chaque pont (écart entre deux tronçons) reste du sol ; le milieu d'un tronçon, non.
        foreach (var b in GalleryLooks.Bridges(space.Obstacles))
            Assert.Contains(floor, f => f.MinX <= (b.MinX + b.MaxX) / 2 && (b.MinX + b.MaxX) / 2 <= f.MaxX && f.MinZ <= 19.5 && 19.5 <= f.MaxZ);
        Assert.DoesNotContain(floor, f => f.MinX <= 0 && 0 <= f.MaxX && f.MinZ < 19.5 && 19.5 < f.MaxZ);
        // Sans trou : le sol entier ; un trou qui dépasse est rogné ; emprise vide : rien.
        Assert.Equal(new[] { (0.0, 0.0, 4.0, 4.0) }, GalleryLooks.FloorPieces(0, 0, 4, 4, Array.Empty<(double, double, double, double)>()));
        Assert.Equal(16 - 4, GalleryLooks.FloorPieces(0, 0, 4, 4, new[] { (-1.0, -1.0, 2.0, 2.0) }).Sum(f => (f.MaxX - f.MinX) * (f.MaxZ - f.MinZ)), 9);
        Assert.Empty(GalleryLooks.FloorPieces(0, 0, 0, 4, null!));
        Assert.Single(GalleryLooks.FloorPieces(0, 0, 4, 4, null!));
        // La salle n'a pas de gouffre : un seul morceau ; Unity découpe le sol des galeries seulement.
        var salle = Zone("zone.galeries_eclats").Setup.Space!;
        Assert.Single(GalleryLooks.FloorPieces(-26, -6, 26, 42, GalleryLooks.ChasmHoles(salle.Obstacles)));
        string cave = Unity("Assets", "EdS", "Game", "SpaceView.Cave.cs"), gallery = Unity("Assets", "EdS", "Game", "SpaceView.Gallery.cs");
        Assert.Contains("GalleryLooks.FloorPieces(minX - 2f, minZ - 2f, maxX + 2f, maxZ + 2f, GalleryLooks.ChasmHoles(space.Obstacles))", cave);
        Assert.True(cave.IndexOf("_gallery = GalleryLooks.IsGallery", StringComparison.Ordinal) < cave.IndexOf("GalleryLooks.FloorPieces(", StringComparison.Ordinal));
        Assert.Contains("ChasmPit(o.Id, x0, z0, x1, z1, rock, rockTile)", gallery);
    }

    [Fact]
    public void Pont_de_pierre_en_dalles_cote_a_cote_sans_trou()
    {
        // Le modèle fini : une passerelle de 4 × 0,89 m ; le pont de 4 m de large (écart du gouffre) et 4 m de long (3 + deux débords).
        foreach (var gap in GalleryLooks.Bridges(Zone("zone.galeries_seuil").Setup.Space!.Obstacles))
        {
            var span = GalleryLooks.BridgeSpan(gap);
            var slabs = GalleryLooks.BridgeSlabs(span.MinX, span.MinZ, span.MaxX, span.MaxZ, 4.0, 0.89);
            Assert.Equal(4, slabs.Count);
            double reach = span.MinX;
            foreach (var t in slabs)
            {
                Assert.Equal(reach, t.MinX, 9);   // côte à côte : aucun trou
                reach = t.MaxX;
                Assert.Equal((span.MinZ, span.MaxZ), (t.MinZ, t.MaxZ));   // toute la longueur
                double stretch = (t.MaxX - t.MinX) / (0.89 * (t.MaxZ - t.MinZ) / 4.0);
                Assert.InRange(stretch, 2.0 / 3 - 1e-9, 1.5 + 1e-9);
                // Posée par TileFit : le modèle tourné d'un quart (long côté selon Z) remplit exactement la dalle.
                var fit = SewerLooks.TileFit(4.0, 0.64, 0.89, t.MaxX - t.MinX, t.MaxZ - t.MinZ, GalleryLooks.BridgeHeight);
                Assert.True(fit.Turn);
                Assert.Equal(t.MaxX - t.MinX, 0.89 * fit.Sx, 9);
                Assert.Equal(t.MaxZ - t.MinZ, 4.0 * fit.Sz, 9);
            }
            Assert.Equal(span.MaxX, reach, 9);
        }
        Assert.Single(GalleryLooks.BridgeSlabs(0, 0, 0.5, 4, 4.0, 0.89));
        Assert.Empty(GalleryLooks.BridgeSlabs(0, 0, 0, 4, 4.0, 0.89));
        Assert.Empty(GalleryLooks.BridgeSlabs(0, 0, 4, 4, 0, 0.89));
    }

    [Fact]
    public void Lueur_violette_des_cristaux_du_seuil()
    {
        Assert.True(GalleryLooks.GlowOf(GalleryPiece.GiantShard).Intensity > 0);
        Assert.True(GalleryLooks.GlowOf(GalleryPiece.Altar).Range >= GalleryLooks.GlowOf(GalleryPiece.GiantShard).Range);
        Assert.Equal((0.0, 0.0), GalleryLooks.GlowOf(GalleryPiece.Wall));
        Assert.Equal((0.0, 0.0), GalleryLooks.GlowOf(GalleryPiece.Rubble));
        var c = GalleryLooks.GlowColor;
        Assert.True(c.B > c.R && c.R > c.G);   // violet
    }

    [Fact]
    public void Partie_reprise_dans_les_galeries_a_l_abri_sans_pluie_ni_neige()
    {
        string dir = Path.Combine(Path.GetTempPath(), "eds_v250b_" + Guid.NewGuid().ToString("N"));
        string save = Path.Combine(dir, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        try
        {
            SaveStore.Write(save, new WorldSave("zone.galeries_seuil", 0, 2, Array.Empty<string>(), V11TestData.Hero(22)));
            AdventureSession a = V11TestData.Adventure(save);
            Assert.True(a.Underground && a.Sheltered && !a.Indoors);   // à l'abri : ni pluie, ni neige, ni glace à l'affichage
            Assert.True(a.Map.Underground && !a.Map.Indoor);
            Assert.Equal(new[] { ("sortie", DoorKind.Door, true), ("faille_des_eclats", DoorKind.Rift, false) }, a.Map.Doors.Select(d => (d.Id, d.Kind, d.Outside)));
            Assert.True(a.Mood().Indoors);       // ambiance sonore à l'abri
            Assert.Equal(0, a.Mood().Rain);      // la pluie ne s'entend pas sous terre
            Assert.True(GalleryLooks.IsGallery(a.Map.Obstacles));
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Décision de Damien du 26 novembre 2026 : créatures de cristal. Leur modèle animé est cherché dans Resources/Galeries/Creatures ; sans
    /// lui, le modèle de leur place (loup pour l'araignée, ours pour le golem), teinté de cristal. La liste de l'atelier : trois modèles animés,
    /// 65 crédits chacun (30 + 25 + 10), 195 au plus, aucun lancé.
    /// </summary>
    [Fact]
    public void Creatures_de_cristal_modele_anime_et_repli_teinte()
    {
        Assert.Equal(GalleryLooks.SpiderModel, GalleryLooks.CreatureModel("araignee_cristal_galerie2"));
        Assert.Equal(GalleryLooks.SpiderModel, GalleryLooks.CreatureModel("araignee_cristal_salle1_v2"));
        Assert.Equal(GalleryLooks.GolemModel, GalleryLooks.CreatureModel("golem_cristal_salle1"));
        Assert.Null(GalleryLooks.CreatureModel("loup_echo_galerie1"));
        Assert.Equal(GalleryLooks.WatcherModel, GalleryLooks.CreatureModel("loup_echo_veilleur_eclats"));
        Assert.Equal(GalleryLooks.WatcherModel, GalleryLooks.CreatureModel("loup_echo_veilleur_eclats_dechaine_v2"));
        Assert.Null(GalleryLooks.CreatureModel("loup_echo_gardien_cristaux"));
        Assert.Null(GalleryLooks.CreatureModel("ours_galerie1"));
        Assert.Null(GalleryLooks.CreatureModel("rat_egout1"));
        Assert.StartsWith(GalleryLooks.AssetFolder + "/", GalleryLooks.CreatureAssetFolder + "/");
        Assert.Equal("Galeries/Creatures/", GalleryLooks.CreatureFolder);
        Assert.Null(GalleryLooks.NameOf(GalleryLooks.CreatureAssetFolder + "/golem_cristal.fbx"));   // pas une pièce du décor
        var t = GalleryLooks.CrystalTint;
        Assert.True(t.B > t.R && t.R > t.G);   // violet
        // Chaque créature de cristal des galeries a son modèle.
        foreach (string id in V250aGaleriesTests.Galleries.SelectMany(z => Zone(z).Setup.Combatants).Select(c => c.Id).Where(StyleRules.IsCrystalCreature))
            Assert.NotNull(GalleryLooks.CreatureModel(id));
        string look = Unity("Assets", "EdS", "Game", "StyleLook.cs"), style = Unity("Assets", "EdS", "Editor", "EdsStyle.cs");
        Assert.Contains("Resources.Load<GameObject>(GalleryLooks.CreatureFolder + crystalName)", look);
        Assert.Contains("GalleryLooks.CrystalTint", look);
        Assert.Contains("GalleryLooks.CreatureAssetFolder", style);
        Assert.Contains("StyleRules.IsCrystalCreature(id)", look);   // la teinte de cristal seulement pour l'araignée et le golem en repli
        Assert.Contains("if (rat != null && crystalName != null) clips = null;", look);   // pas les clips du loup ou de l'ours
        string lab = Path.Combine(Path.GetDirectoryName(Lot93UnityPackageTests.Root)!, "Eclats_du_Seuil_AssetLab", "Galeries", "Creatures", "modeles.json");
        if (File.Exists(lab))
        {
            var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(lab))!;
            Assert.Equal(new[] { GalleryLooks.SpiderModel, GalleryLooks.GolemModel, "veilleur_cristal" }, root["modeles"]!.AsArray().Select(m => (string)m!["nom"]!));
            Assert.Equal(195, (int)root["cout_total"]!);
        }
    }

    /// <summary>
    /// Créatures de cristal générées le 26 novembre 2026 (155 crédits réels), finies dans Blender (Galeries/Creatures/finir_creatures.py) :
    /// l'araignée et le veilleur avec leur marche, le golem statique (sa marche bipède a échoué chez Tripo, remboursée). Elles sont dans
    /// Resources/Galeries/Creatures avec leur couleur et leur normale ; sans elles, le repli (loup, ours teintés ; loup-écho du veilleur) reste.
    /// </summary>
    [Fact]
    public void Creatures_de_cristal_presentes_marche_et_golem_statique()
    {
        string folder = Path.Combine(Lot93UnityPackageTests.Root, "unity", GalleryLooks.CreatureAssetFolder);
        Assert.Equal(new[] { "araignee_cristal", "golem_cristal", "veilleur_cristal" }, GalleryLooks.CreatureModels);
        foreach (string name in GalleryLooks.CreatureModels)
        {
            string fbx = Path.Combine(folder, name + ".fbx");
            Assert.True(File.Exists(fbx), fbx);
            Assert.InRange(new FileInfo(fbx).Length, 10_000, 2_000_000);
            Assert.True(File.Exists(Path.Combine(folder, name + "_couleur.jpg")), name);
            Assert.True(File.Exists(Path.Combine(folder, name + "_normale.png")), name);
            // La marche est dans le FBX (nom de prise « Walk », que le jeu reconnaît) ; le golem n'a aucune animation.
            bool walk = File.ReadAllBytes(fbx).AsSpan().IndexOf("Walk"u8) >= 0;
            Assert.Equal(GalleryLooks.CreatureWalks(name), walk);
        }
        Assert.Equal(GalleryLooks.CreatureModels.Count, Directory.GetFiles(folder, "*.fbx").Length);
        Assert.False(GalleryLooks.CreatureWalks(GalleryLooks.GolemModel));
        // Toutes les formes du veilleur prennent le veilleur en cristal ; il reste un écho de faille (rendu violet), plus massif.
        foreach (Boss chief in Zone("zone.galeries_eclats").Chiefs)
            foreach (var form in chief.Forms)
            {
                Assert.Equal(GalleryLooks.WatcherModel, GalleryLooks.CreatureModel(form.Id));
                Assert.Equal(EchoKind.Rift, EchoLooks.Of(form.Id));
                Assert.Equal(1.4, StyleRules.BeastHeight(form.Id));
                Assert.False(StyleRules.IsCrystalCreature(form.Id));   // pas teinté en repli : le loup-écho de faille
            }
    }

    [Fact]
    public void Unity_galeries_dessinees_et_kit_branche()
    {
        string cave = Unity("Assets", "EdS", "Game", "SpaceView.Cave.cs"), gallery = Unity("Assets", "EdS", "Game", "SpaceView.Gallery.cs");
        string view = Unity("Assets", "EdS", "Game", "SpaceView.cs"), style = Unity("Assets", "EdS", "Editor", "EdsStyle.cs");
        Assert.Contains("_gallery = GalleryLooks.IsGallery(space.Obstacles);", cave);
        // Les galeries passent avant les égouts, la crypte, la mine et le rideau d'eau.
        Assert.True(cave.IndexOf("GalleryObstacle(o,", StringComparison.Ordinal) < cave.IndexOf("SewerObstacle(o,", StringComparison.Ordinal));
        Assert.True(cave.IndexOf("GalleryDoor(door,", StringComparison.Ordinal) < cave.IndexOf("SewerDoor(door,", StringComparison.Ordinal));
        Assert.Contains("GalleryBridges(space, rock, rockTile)", cave);
        Assert.Contains("GalleryEntrance();", view);
        Assert.Contains("CryptModel(GalleryLooks.Folder, GalleryLooks.ModelFor(piece))", gallery);
        Assert.Contains("GalleryLooks.Occludes(piece)", gallery);                       // parois, piliers, éclats géants : occulteurs
        Assert.Contains("Prism(o.Id, points, height, basalt, true, rockTile)", gallery);   // et leurs formes de remplacement aussi
        Assert.Contains("AddModelOccluders(turn)", gallery);                           // l'éclat géant de remplacement aussi
        Assert.Contains("SewerLooks.WallTiles(", gallery);                             // paroi et gouffre en tuiles, d'après le modèle mesuré
        Assert.Contains("Footprint(model, out float modelLong, out float modelShort)", gallery);
        Assert.Contains("FitSewerModel(", gallery);                                    // échelle avant le lacet, jamais étirés
        Assert.Contains("GalleryLooks.Bridges(space.Obstacles)", gallery);
        Assert.Contains("_curtains.Add(", gallery);   // le jour des pierres levées suit le jour du dehors
        Assert.Contains("GalleryPieces(notes);", style);
        Assert.Contains("GalleryLooks.NameOf", style);
        // L'éclat de remplacement : l'échelle sur l'enfant, le lacet sur le parent (jamais d'échelle non uniforme au-dessus d'un enfant tourné).
        Assert.Contains("turn.transform.rotation = Quaternion.Euler(", gallery);
        Assert.DoesNotContain("turn.transform.localScale", gallery);
        // Aucun shader nouveau (mots réservés HLSL) ; scripts en C# 9 sans annotation de nullabilité.
        Assert.DoesNotContain("Shader", gallery);
        Assert.DoesNotContain("#nullable", gallery);
        Assert.DoesNotContain("string?", gallery);
        Assert.DoesNotContain("GameObject?", gallery);
    }
}
