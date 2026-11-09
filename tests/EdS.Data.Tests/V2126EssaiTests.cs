using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Space;
using EdS.Runtime;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.12.6 (essai sous Unity du 9 novembre 2026, PC de Damien en mode « PC modeste ») : tonneaux tout blancs à la forge
/// (<see cref="PieceTextures"/>, <see cref="StyleRules.PropCandidates"/>), caméra qui entre dans les maisons
/// (<see cref="CameraRules.ClearDistance"/>), étiquettes empilées (<see cref="LabelLayout"/>), trajets des PNJ du bourg.
/// </summary>
[Trait("Category", "Runtime")]
public sealed class V2126EssaiTests
{
    private const string Barrels = "Assets/Style/PolyHaven/Objets/wooden_barrels_01/wooden_barrels_01_1k.fbx";
    private const string Tex = "Assets/Style/PolyHaven/Objets/wooden_barrels_01/textures/wooden_barrels_01_";

    private static readonly string[] BarrelMaps =
    {
        Tex + "barrel01_diff_1k.png", Tex + "barrel01_nor_gl_1k.exr", Tex + "barrel01_rough_1k.exr", Tex + "barrel01_metal_1k.exr",
        Tex + "barrel02_diff_1k.png", Tex + "barrel02_nor_gl_1k.exr", Tex + "barrel02_rough_1k.exr",
        Tex + "pieces_diff_1k.png", Tex + "pieces_nor_gl_1k.exr", Tex + "pieces_rough_1k.exr", Tex + "pieces_metal_1k.exr",
    };

    private static string Script(params string[] path) =>
        File.ReadAllText(Path.Combine(new[] { Lot93UnityPackageTests.Root, "unity", "Assets", "EdS" }.Concat(path).ToArray())).Replace("\r\n", "\n");

    // --- 1. Matières par pièce et tonneau seul ------------------------------------------------------------------------------------

    [Fact]
    public void Pieces_du_lot_de_tonneaux_trouvees_dans_les_noms_des_textures()
    {
        Assert.Equal("woodenbarrels01", PieceTextures.ModelKey(Barrels));
        Assert.Equal("barrel01", PieceTextures.PieceOf("woodenbarrels01", Tex + "barrel01_nor_gl_1k.exr"));
        Assert.Equal("pieces", PieceTextures.PieceOf("woodenbarrels01", Tex + "pieces_metal_1k.exr"));
        Assert.Null(PieceTextures.PieceOf("woodenbarrels01", "x/wooden_barrels_01_diff_1k.png"));   // texture du modèle entier
        Assert.Null(PieceTextures.PieceOf("woodenbarrels01", "x/barrel_03_diff_1k.png"));          // autre modèle
        Assert.Equal(new[] { "barrel01", "barrel02", "pieces" }, PieceTextures.Pieces(Barrels, BarrelMaps));
    }

    [Theory]
    [InlineData("barrel01", "barrel01")]
    [InlineData("wooden_barrels_01_barrel01", "barrel01")]
    [InlineData("Barrel_02.001", "barrel02")]
    [InlineData("wooden_barrels_01_pieces", "pieces")]
    [InlineData("Tonneau_Pieces", "pieces")]
    public void Chaque_matiere_prend_les_cartes_de_sa_piece(string material, string piece)
    {
        IReadOnlyDictionary<string, StylePieceMaps> links = PieceTextures.Link(Barrels, new[] { material }, BarrelMaps);
        StylePieceMaps maps = links[material];
        Assert.Equal(piece, maps.Piece);
        Assert.Equal(Tex + piece + "_diff_1k.png", maps.Color);
        Assert.Equal(Tex + piece + "_nor_gl_1k.exr", maps.Normal);
        Assert.Equal(Tex + piece + "_rough_1k.exr", maps.Rough);
        Assert.Equal(piece == "barrel02" ? null : Tex + piece + "_metal_1k.exr", maps.Metal);
    }

    [Fact]
    public void Matieres_sans_nom_parlant_prises_dans_l_ordre_si_autant_que_de_pieces()
    {
        IReadOnlyDictionary<string, StylePieceMaps> links = PieceTextures.Link(Barrels, new[] { "Material.002", "Material", "Material.001" }, BarrelMaps);
        Assert.Equal("barrel01", links["Material"].Piece);
        Assert.Equal("barrel02", links["Material.001"].Piece);
        Assert.Equal("pieces", links["Material.002"].Piece);
        // Une nommée, deux sans nom pour deux pièces libres.
        links = PieceTextures.Link(Barrels, new[] { "pieces", "Mat_A", "Mat_B" }, BarrelMaps);
        Assert.Equal(("barrel01", "barrel02", "pieces"), (links["Mat_A"].Piece, links["Mat_B"].Piece, links["pieces"].Piece));
        // Pas autant : on ne devine pas (la règle d'avant s'applique).
        links = PieceTextures.Link(Barrels, new[] { "Mat_A", "Mat_B" }, BarrelMaps);
        Assert.Empty(links);
    }

    [Fact]
    public void Modele_sans_textures_par_piece_inchange()
    {
        string[] rock = { "a/rock_07/textures/rock_07_diff_1k.jpg", "a/rock_07/textures/rock_07_nor_gl_1k.exr" };
        Assert.Empty(PieceTextures.Link("a/rock_07/rock_07_1k.fbx", new[] { "rock_07" }, rock));
        Assert.Empty(PieceTextures.Link(Barrels, new[] { "barrel01" }, null!));
        Assert.Empty(PieceTextures.Link(Barrels, null!, BarrelMaps));
    }

    [Fact]
    public void Le_tonneau_de_la_forge_est_un_tonneau_seul()
    {
        var models = new[]
        {
            Barrels, "Assets/Style/PolyHaven/barrel_03/barrel_03_1k.fbx", "Assets/Style/PolyHaven/Objets/barrel_set/barrel_set_1k.fbx",
        };
        IReadOnlyList<string> barrels = StyleRules.PropCandidates(models, StyleProp.Barrel);
        Assert.Equal("Assets/Style/PolyHaven/barrel_03/barrel_03_1k.fbx", barrels[0]);
        Assert.Equal(3, barrels.Count);
        // Seul, le lot reste choisi (mieux qu'aucun tonneau).
        Assert.Equal(Barrels, StyleRules.PropCandidates(new[] { Barrels }, StyleProp.Barrel).Single());
        Assert.True(StyleRules.IsPropLot(Barrels, new[] { "barrel" }));
        Assert.False(StyleRules.IsPropLot("x/barrel_03_1k.fbx", new[] { "barrel" }));
    }

    [Fact]
    public void Preparation_relie_les_pieces_aux_deux_passages()
    {
        string style = Script("Editor", "EdsStyle.cs");
        Assert.Equal(2, CountOf(style, "PieceTextures.Link("));
        Assert.Contains("piece?.Color ?? StyleRules.MaterialTexture(embedded.name, maps, false)", style);
    }

    // --- 2. Caméra contre les maisons -------------------------------------------------------------------------------------------

    private static readonly CameraBox House = new CameraBox(-3, 0, 4, 3, 6, 10);   // mur sud à z = 4

    [Fact]
    public void La_camera_s_arrete_devant_le_mur_qui_s_interpose()
    {
        // Point visé à 1,6 m, caméra vers le nord (+z), à l'horizontale : le mur est à 4 m, la caméra à 4 - 0,3 m.
        double clear = CameraRules.ClearDistance((0, 1.6, 0), (0, 0, 1), new[] { House });
        Assert.Equal(4 - CameraRules.CollisionRadius, clear, 9);
        // Inclinée : la direction de Offset (distance 1) touche le mur plus loin, toujours avant 24 m.
        (double X, double Y, double Z) dir = CameraRules.Offset(180, 22, 1);
        Assert.InRange(CameraRules.ClearDistance((0, 1.6, 0), dir, new[] { House }), 3.5, 5.0);
        // Plusieurs maisons : la plus proche.
        Assert.Equal(1.7, CameraRules.ClearDistance((0, 1.6, 0), (0, 0, 1), new[] { House, new CameraBox(-1, 0, 2, 1, 3, 3) }), 9);
    }

    [Fact]
    public void Rien_ne_gene_au_dessus_des_toits_derriere_ou_dans_la_maison()
    {
        Assert.Equal(CameraRules.MaxDistance, CameraRules.ClearDistance((0, 1.6, 0), (0, 0, -1), new[] { House }));   // maison devant
        Assert.Equal(CameraRules.MaxDistance, CameraRules.ClearDistance((0, 1.6, 0), (0, 1, 0), new[] { House }));    // vue du dessus
        Assert.Equal(CameraRules.MaxDistance, CameraRules.ClearDistance((0, 7, 0), (0, 0, 1), new[] { House }));      // au-dessus du toit
        Assert.Equal(CameraRules.MaxDistance, CameraRules.ClearDistance((0, 1.6, 6), (0, 0, 1), new[] { House }));    // déjà dedans
        Assert.Equal(CameraRules.MaxDistance, CameraRules.ClearDistance((0, 1.6, 0), (0, 0, 1), null));
        Assert.Equal(CameraRules.MaxDistance, CameraRules.ClearDistance((double.NaN, 1.6, 0), (0, 0, 1), new[] { House }));
        Assert.Equal(CameraRules.MaxDistance, CameraRules.ClearDistance((0, 1.6, 0), (0, 0, 1), new[] { new CameraBox(-3, 0, 30, 3, 6, 40) }));
    }

    [Fact]
    public void Mur_colle_au_dos_distance_minimale()
    {
        Assert.Equal(CameraRules.CollisionMinDistance, CameraRules.ClearDistance((0, 1.6, 3.65), (0, 0, 1), new[] { House }));
    }

    [Fact]
    public void Rapprochee_tout_de_suite_revenue_en_douceur()
    {
        Assert.Equal(3.0, CameraRules.SettleClear(24, 3, 0.016));
        double back = CameraRules.SettleClear(3, 24, 0.1);
        Assert.InRange(back, 3.1, 23.9);
        Assert.Equal(3 + 21 * (1 - Math.Exp(-0.4)), back, 9);
        Assert.Equal(3.0, CameraRules.SettleClear(3, 24, 0));
        Assert.Equal(5.0, CameraRules.SettleClear(double.NaN, 5, 0.1));
        Assert.Equal(5.0, CameraRules.SettleClear(5, double.NaN, 0.1));
        Assert.Equal(3.0, CameraRules.SettleClear(3, 24, -1));
        // Converge vers la distance libre sans la dépasser.
        double d = 1;
        for (int i = 0; i < 200; i++) d = CameraRules.SettleClear(d, 8, 0.05);
        Assert.InRange(d, 7.99, 8.0);
    }

    [Fact]
    public void Camera_branchee_sur_les_batiments_un_rayon_par_image()
    {
        string rig = Script("Game", "CameraRig.cs"), space = Script("Game", "SpaceView.cs"), game = Script("Game", "EdsGame.cs");
        Assert.Equal(1, CountOf(rig, "CameraRules.ClearDistance("));
        Assert.Contains("held = Mathf.Min(held, _clear);", rig);
        Assert.Equal(2, CountOf(space, "_cameraBoxes.Add("));
        Assert.Equal(3, CountOf(game, "_camera.Blockers = _arena.CameraBlockers;"));
        Assert.DoesNotContain("Physics.", rig);
    }

    // --- 3. Étiquettes ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Un_seul_nom_par_PNJ_et_par_texte()
    {
        var l = new LabelLayout();
        int a = l.Add("npc.maelle", "Maëlle, la forgeronne · à la forge", 100, 100, 200, 20, 3000 - 2);
        int b = l.Add("npc.maelle", "Maëlle, la forgeronne · à la forge", 500, 300, 200, 20, 3000 - 1);
        int c = l.Add("place_du_village", "Place du village", 900, 100, 200, 20, 1000);
        int d = l.Add(null, "Place du village", 900, 500, 200, 20, 500);
        l.Solve(20);
        Assert.False(l.Shown(a));   // le plus loin des deux
        Assert.True(l.Shown(b));
        Assert.True(l.Shown(c));
        Assert.False(l.Shown(d));
    }

    [Fact]
    public void Etiquettes_qui_se_recouvrent_decalees_puis_cachees_les_moins_importantes_d_abord()
    {
        var l = new LabelLayout();
        int forge = l.Add("forge", "Forge de Maëlle", 400, 300, 160, 26, 2000 - 1);
        int maelle = l.Add("npc.maelle", "Maëlle, la forgeronne", 410, 305, 200, 26, 3000 - 1);
        int colombe = l.Add("npc.apprentie", "Colombe, l'apprentie de Maëlle", 405, 305, 220, 26, 3000 - 3);
        int place = l.Add("place_du_village", "Place du village", 395, 298, 160, 26, 1000 - 8);
        int loin = l.Add("npc.odile", "Odile", 1200, 300, 60, 26, 3000 - 12);
        l.Solve(26, 1);
        Assert.True(l.Shown(maelle));
        Assert.Equal(0, l.OffsetY(maelle));
        Assert.True(l.Shown(colombe));
        Assert.Equal(26, l.OffsetY(colombe));   // montée d'un cran
        Assert.False(l.Shown(forge));            // plus de place
        Assert.False(l.Shown(place));
        Assert.True(l.Shown(loin));
        // Aucune paire montrée ne se recouvre.
        Assert.Equal(3, Enumerable.Range(0, l.Count).Count(l.Shown));
    }

    [Fact]
    public void Disposition_stable_et_reutilisable()
    {
        var l = new LabelLayout();
        for (int round = 0; round < 3; round++)
        {
            l.Clear();
            for (int i = 0; i < 40; i++) l.Add("k" + i, "Nom " + i, 10 * i, 0, 100, 20, 1);
            l.Solve(-20, 1);
            // Priorités égales : l'ordre d'ajout décide ; la première est à sa place, la deuxième décalée vers le haut (signe du repère).
            Assert.True(l.Shown(0));
            Assert.Equal(0, l.OffsetY(0));
            Assert.Equal(-20, l.OffsetY(1));
            Assert.False(l.Shown(2));
            Assert.True(l.Shown(10));
        }
        l.Clear();
        int empty = l.Add("x", "", 0, 0, 10, 10, 9);
        int nan = l.Add("y", "Y", 0, 0, 10, 10, double.NaN);
        l.Solve(10);
        Assert.False(l.Shown(empty));
        Assert.True(l.Shown(nan));
        Assert.False(l.Shown(-1));
        Assert.False(l.Shown(99));
        Assert.True(LabelLayout.TextWidth("Maëlle", 19) > LabelLayout.TextWidth("Ma", 19));
        Assert.Equal(0, LabelLayout.TextWidth(null, 19) - 19);
    }

    [Fact]
    public void Etiquettes_du_jeu_et_de_la_carte_passent_par_la_disposition()
    {
        string markers = Script("Game", "WorldMarkers.cs"), book = Script("Game", "AdventureBook.cs");
        Assert.Contains("_layout.Solve(", markers);
        Assert.Contains("_mapLayout.Solve(", book);
        Assert.Contains("ScreenPointToLocalPointInRectangle(_mapMarkers", book);   // nom du repère sous la souris
    }

    // --- 4. Trajets des PNJ du bourg ----------------------------------------------------------------------------------------------

    [Fact]
    public void PNJ_du_bourg_entre_leurs_places_sans_traverser_de_maison()
    {
        ScenarioLoadResult result = ScenarioJsonReader.ReadFile(ScenarioPath("zones/vallons"), ShippedData);
        Assert.True(result.IsValid, result.Report.ToString());
        CombatSpace space = result.Setup!.Space!;
        var paths = new WalkPaths(space);
        long r = (long)Math.Round(WalkPaths.DefaultRadius * 1e4);
        JsonNode story = JsonNode.Parse(File.ReadAllText(Path.Combine(DataDirectory, "world", "story.json")))!["data"]!;
        JsonNode zone = JsonNode.Parse(File.ReadAllText(Path.Combine(DataDirectory, "world", "zones", "vallons.json")))!["data"]!;
        var doors = zone["doors"]!.AsArray().ToDictionary(d => (string)d!["to"]!, d => ((double)d!["position"]![0]!, (double)d["position"]![1]!));
        int walks = 0;
        foreach (JsonNode? npc in story["npcs"]!.AsArray())
        {
            string id = (string)npc!["id"]!;
            var places = new List<(double X, double Z)> { ((double)npc["position"]![0]!, (double)npc["position"]![1]!) };
            var away = new List<(double X, double Z)>();
            foreach (JsonNode? step in npc["schedule"]?.AsArray() ?? new JsonArray())
                if (step!["zone"] is null && step["position"] is JsonArray p) places.Add(((double)p[0]!, (double)p[1]!));
                else if (step["zone"] is JsonNode z && doors.TryGetValue((string)z!, out (double, double) door)) away.Add(door);
            if (places.Count < 2) continue;
            // Seulement les PNJ du bourg (Garin, Sauge, Bertille…) : entre leurs places, et vers la porte de chaque sortie de leur journée.
            if (places[0].X > -24 || places[0].X < -100 || places[0].Z < -24 || places[0].Z > 19) continue;
            var hops = new List<((double X, double Z) A, (double X, double Z) B)>();
            for (int i = 0; i + 1 < places.Count; i++) hops.Add((places[i], places[i + 1]));
            foreach ((double, double) door in away) hops.Add((places[0], door));   // vers la porte où il sort (12 h : Garin à la taverne)
            foreach (((double ax, double az), (double bx, double bz)) in hops)
            {
                var walker = new Walker { StraightWhenBlocked = true };
                double x = ax, z = az;
                for (int s = 0; s < 20_000; s++)
                {
                    (double tx, double tz) = walker.Next(paths, x, z, bx, bz);
                    double dx = tx - x, dz = tz - z, d = Math.Sqrt(dx * dx + dz * dz);
                    if (d < 1e-9) break;
                    double move = Math.Min(d, 0.07);
                    double nx = x + dx / d * move, nz = z + dz / d * move;
                    Assert.True(paths.Navigator.Clear((long)Math.Round(x * 1e4), (long)Math.Round(z * 1e4), (long)Math.Round(nx * 1e4), (long)Math.Round(nz * 1e4), r / 4),
                        $"{id} : ({ax}, {az}) → ({bx}, {bz}) dans un mur en ({x:0.00}, {z:0.00})");
                    (x, z) = (nx, nz);
                }
                walks++;
            }
        }
        Assert.True(walks >= 6, walks.ToString());
    }

    private static int CountOf(string text, string what)
    {
        int n = 0;
        for (int i = text.IndexOf(what, StringComparison.Ordinal); i >= 0; i = text.IndexOf(what, i + what.Length, StringComparison.Ordinal)) n++;
        return n;
    }
}
