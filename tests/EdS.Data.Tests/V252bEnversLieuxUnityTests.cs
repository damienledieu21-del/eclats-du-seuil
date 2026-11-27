using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Runtime;
using EdS.World;

namespace EdS.Data.Tests;

/// <summary>
/// Chantier V2.52b : les douze lieux de l'Envers à l'écran, règles pures (<see cref="EnversLieux"/>) : pièces du kit Envers2 et leurs modèles,
/// sol local, construction par cases autour de la caméra selon la machine, secrets (coffre, stèle), créatures (chien de cendre animé,
/// spectre statique qui flotte), kit présent dans Unity avec son repli.
/// </summary>
[Trait("Category", "World")]
public sealed class V252bEnversLieuxUnityTests
{
    private static ZoneDefinition Envers => V16TestData.Interiors.Single(z => z.Envers);

    [Fact]
    public void Chaque_piece_de_l_Envers_est_reconnue_une_seule_fois()
    {
        var own = Envers.Setup.Space!.Obstacles.Where(o => o.Id.StartsWith("envers_", StringComparison.Ordinal)).ToList();
        Assert.True(own.Count > 100, $"{own.Count}");
        int lieux = 0;
        foreach (var o in own)
        {
            EnversLieuPiece lieu = EnversLieux.PieceFor(o.Id);
            EnversPiece first = EnversLook.PieceFor(o.Id);
            Assert.True((lieu == EnversLieuPiece.None) != (first == EnversPiece.None), o.Id);   // l'un ou l'autre, jamais les deux, jamais aucun
            if (lieu == EnversLieuPiece.None) continue;
            lieux++;
            Assert.Contains(EnversLieux.ModelFor(lieu)!, EnversLieux.Models);
            Assert.InRange(EnversLieux.HeightOf(lieu), 1.0, 25.0);
        }
        Assert.True(lieux >= 15, $"{lieux}");
        Assert.Equal(EnversLieuPiece.MillenaryTree, EnversLieux.PieceFor("envers_arbre_millenaire_mort"));
        Assert.Equal(EnversPiece.None, EnversLook.PieceFor("envers_arbre_millenaire_mort"));   // pas un « arbre mort » de la V2.51
        Assert.Equal(EnversLieuPiece.None, EnversLieux.PieceFor("envers_arbre_mort_foret_2"));
        Assert.Equal(EnversLieuPiece.None, EnversLieux.PieceFor(null));
        Assert.True(EnversLieux.Tiled(EnversLieuPiece.BurntPalisade));
        Assert.True(EnversLieux.Occludes(EnversLieuPiece.MillenaryTree) && !EnversLieux.Occludes(EnversLieuPiece.Barrels));
    }

    [Fact]
    public void Pieces_posees_sur_le_sol_affiche()
    {
        // Une pente : le pied d'une pièce posée sur le plus bas, une pièce qui flotte au-dessus du plus haut plus son écart.
        double Slope(double x, double z) => 0.1 * x + 0.05 * z;
        Assert.Equal(Slope(10, 20), EnversLieux.Ground(EnversLieuPiece.InvertedMill, 10, 20, 17, 27, Slope), 9);
        Assert.Equal(Slope(17, 27) + 0.6, EnversLieux.Ground(EnversLieuPiece.FloatingCairn, 10, 20, 17, 27, Slope), 9);
        Assert.Equal(Slope(17, 27) + 1.0, EnversLieux.Ground(EnversLieuPiece.BrokenBridge, 10, 20, 17, 27, Slope), 9);
        Assert.Equal(3.0, EnversLieux.Ground(EnversLieuPiece.Barrels, -5, -5, 5, 5, (x, z) => 3.0), 9);
        Assert.Throws<ArgumentNullException>(() => EnversLieux.Ground(EnversLieuPiece.Barrels, 0, 0, 1, 1, null!));
    }

    [Fact]
    public void Decor_construit_par_cases_selon_la_machine()
    {
        RegionGrid region = Envers.Region!;
        var pieces = Envers.Setup.Space!.Obstacles.Where(o => EnversLieux.PieceFor(o.Id) != EnversLieuPiece.None)
            .Select(o => (X: o.Points.Average(p => p.X.Raw / 1e4), Z: o.Points.Average(p => p.Z.Raw / 1e4), Area: 1.0)).ToList();
        foreach (DeviceTier tier in new[] { DeviceTier.Phone, DeviceTier.Modest, DeviceTier.Full })
        {
            int total = 0;
            for (int c = 0; c < region.Columns; c++)
                for (int r = 0; r < region.Rows; r++)
                {
                    var cell = EnversLieux.CellPieces(region, c, r, pieces, tier);
                    Assert.True(cell.Count <= EnversLieux.MaxPiecesPerCell(tier));
                    Assert.All(cell, i => Assert.Equal((c, r), EnversLieux.CellOf(region, pieces[i].X, pieces[i].Z)));
                    total += cell.Count;
                }
            if (tier == DeviceTier.Full) Assert.Equal(pieces.Count, total);   // toutes, sur une bonne machine
            else Assert.True(total <= pieces.Count);
        }
        Assert.True(EnversLieux.MaxPiecesPerCell(DeviceTier.Phone) < EnversLieux.MaxPiecesPerCell(DeviceTier.Modest));
        Assert.True(EnversLieux.MaxPiecesPerCell(DeviceTier.Modest) < EnversLieux.MaxPiecesPerCell(DeviceTier.Full));
        // Les plus grandes d'abord, puis dans l'ordre donné.
        var three = new List<(double X, double Z, double Area)> { (0.5, 0.5, 1), (1, 1, 9), (2, 2, 4) };
        var grid = new RegionGrid(0, 0, 2, 2);
        Assert.Equal(new[] { 1, 2, 0 }, EnversLieux.CellPieces(grid, 0, 0, three, DeviceTier.Full));
        Assert.Equal((1, 1), EnversLieux.CellOf(grid, 9999, 9999));   // borné à la grille
        Assert.Equal((0, 0), EnversLieux.CellOf(grid, -9999, -9999));
        // Le décor n'est jamais construit sur toute l'aire : le rayon autour de la caméra suit la machine.
        Assert.True(EnversLook.StreamRadius(DeviceTier.Modest) < 4 * RegionGrid.CellMeters);
    }

    [Fact]
    public void Secrets_dessines_tous_par_case_avec_leur_objet()
    {
        RegionGrid region = Envers.Region!;
        var secrets = Envers.Secrets.Select(s => new MapSecret(s.Id, s.Kind, s.Position.X.Raw / 1e4, s.Position.Z.Raw / 1e4)).ToList();
        int total = 0;
        for (int c = 0; c < region.Columns; c++)
            for (int r = 0; r < region.Rows; r++) total += EnversLieux.CellSecrets(region, c, r, secrets).Count;
        Assert.Equal(secrets.Count, total);
        Assert.Equal("coffre_reflet", EnversLieux.SecretModel(SecretKind.Chest));
        Assert.Equal("stele_murmures", EnversLieux.SecretModel(SecretKind.Lore));
        Assert.Equal((1.4, 0.9, 1.0), EnversLieux.SecretSize(SecretKind.Chest));
        foreach (var s in secrets)
        {
            double yaw = EnversLieux.SecretYaw(s.Id);
            Assert.InRange(yaw, 0, 359);
            Assert.Equal(yaw, EnversLieux.SecretYaw(s.Id));   // stable
        }
        Assert.Equal(EnversLieux.StableHash("coffre_du_sommet"), EnversLieux.StableHash("coffre_du_sommet"));
        Assert.NotEqual(EnversLieux.StableHash("coffre_du_sommet"), EnversLieux.StableHash("stele_du_col"));
    }

    [Fact]
    public void Carte_de_l_Envers_avec_ses_secrets()
    {
        string folder = Path.Combine(Path.GetTempPath(), "eds_v252b_" + Guid.NewGuid().ToString("N"));
        try
        {
            string save = Path.Combine(folder, "sauvegarde.json");
            Directory.CreateDirectory(folder);
            SaveStore.Write(save, new WorldSave("zone.envers_vallons", 0, 0, Array.Empty<string>(), V11TestData.Hero()));
            AdventureSession a = V11TestData.Adventure(save);
            Assert.True(a.Map.Envers);
            Assert.Equal(Envers.Secrets.Select(s => (s.Id, s.Kind)), a.Map.Secrets.Select(s => (s.Id, s.Kind)));
            // Saison d'affichage de l'Envers : l'automne mort, transition finie, quelle que soit la saison réelle.
            Assert.All(new[] { 0, 1, 2, 3 }, s => Assert.Equal(EnversLook.Season.Season, EnversLook.DecorSeason(true, s)));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Creatures_reflets_et_leurs_modeles()
    {
        foreach (string id in new[] { "moine_reflet1", "moine_reflet4_v2", "moine_reflet_abbe", "moine_reflet_abbe_dechire_v3" })
        {
            Assert.Equal(EnversLieux.WraithModel, EnversLieux.CreatureModel(id));
            Assert.True(EnversLieux.Floats(EnversLieux.WraithModel));
        }
        Assert.Equal(EnversLieux.HoundModel, EnversLieux.CreatureModel("chien_reflet2_v2"));
        Assert.True(EnversLieux.CreatureWalks(EnversLieux.HoundModel));
        Assert.False(EnversLieux.CreatureWalks(EnversLieux.WraithModel));
        foreach (string id in new[] { "moine_echo_crypte1", "chien1", "loup_echo_envers1", "bandit_reflet2", "ours_reflet_brunot" })
            Assert.Null(EnversLieux.CreatureModel(id));
        Assert.Equal(2.2, EnversLieux.WraithHeight("moine_reflet_abbe_courrouce"));
        Assert.Equal(1.85, EnversLieux.WraithHeight("moine_reflet2"));
        // Le spectre flotte entre 0,18 et 0,42 m, déphasé d'un moine à l'autre, et revient au même point après une période.
        for (double t = 0; t < 10; t += 0.37)
            Assert.InRange(EnversLieux.WraithBob("moine_reflet1", t), EnversLieux.WraithLift - EnversLieux.WraithBobMeters - 1e-9, EnversLieux.WraithLift + EnversLieux.WraithBobMeters + 1e-9);
        Assert.Equal(EnversLieux.WraithBob("moine_reflet1", 1.0), EnversLieux.WraithBob("moine_reflet1", 1.0 + EnversLieux.WraithBobSeconds), 9);
        Assert.NotEqual(EnversLieux.WraithBob("moine_reflet1", 1.0), EnversLieux.WraithBob("moine_reflet2", 1.0));
    }

    /// <summary>
    /// Kit Tripo Envers2 du 27 novembre 2026 (13 modèles statiques et 2 créatures, dépense validée d'avance par Damien) : finis dans Blender
    /// (un maillage, allégés, pivot au sol, textures de 1 024 px), à plat dans <c>Resources/Envers2</c> et <c>Resources/Envers2/Creatures</c>.
    /// </summary>
    [Fact]
    public void Kit_Envers2_present_reconnu_et_repli_garde()
    {
        string folder = Path.Combine(Lot93UnityPackageTests.Root, "unity", EnversLieux.AssetFolder);
        Assert.Equal(13, EnversLieux.Models.Count);
        foreach (string name in EnversLieux.Models)
        {
            string fbx = Path.Combine(folder, name + ".fbx");
            Assert.True(File.Exists(fbx), fbx);
            Assert.True(new FileInfo(fbx).Length > 10_000, fbx);
            Assert.True(File.Exists(Path.Combine(folder, name + "_couleur.jpg")), name);
            Assert.True(File.Exists(Path.Combine(folder, name + "_normale.png")), name);
            Assert.Equal(name, EnversLieux.NameOf(EnversLieux.AssetFolder + "/" + name + ".fbx"));
        }
        Assert.Equal(EnversLieux.Models.Count, Directory.GetFiles(folder, "*.fbx").Length);
        string creatures = Path.Combine(Lot93UnityPackageTests.Root, "unity", EnversLieux.CreatureAssetFolder);
        foreach (string name in EnversLieux.CreatureModels)
        {
            Assert.True(File.Exists(Path.Combine(creatures, name + ".fbx")), name);
            Assert.True(File.Exists(Path.Combine(creatures, name + "_couleur.jpg")), name);
        }
        Assert.EndsWith("Resources/" + EnversLieux.Folder.TrimEnd('/'), EnversLieux.AssetFolder);
        Assert.EndsWith("Resources/" + EnversLieux.CreatureFolder.TrimEnd('/'), EnversLieux.CreatureAssetFolder);
        Assert.Null(EnversLieux.NameOf(EnversLieux.AssetFolder + "/coffre_reflet_couleur.jpg"));
        // Toutes les pièces ont un modèle livré.
        foreach (EnversLieuPiece p in Enum.GetValues<EnversLieuPiece>().Where(p => p != EnversLieuPiece.None)) Assert.Contains(EnversLieux.ModelFor(p)!, EnversLieux.Models);
        // La liste de l'atelier est celle du jeu, dans l'ordre.
        string lab = Path.Combine(Path.GetDirectoryName(Lot93UnityPackageTests.Root)!, "Eclats_du_Seuil_AssetLab", "Envers2", "modeles.json");
        if (File.Exists(lab))
        {
            var names = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(lab))!["modeles"]!.AsArray().Select(m => (string)m!["nom"]!).ToList();
            Assert.Equal(EnversLieux.Models, names);
        }
        // Le repli reste dans le code (formes simples), sans modèle : vérifié par la compilation des scripts Unity et le dessin du kit absent.
        string view = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "SpaceView.EnversLieux.cs"));
        Assert.Contains("Repli (modèle absent)", view, StringComparison.Ordinal);
        Assert.Contains("EnversLieux.CellPieces", view, StringComparison.Ordinal);
    }
}
