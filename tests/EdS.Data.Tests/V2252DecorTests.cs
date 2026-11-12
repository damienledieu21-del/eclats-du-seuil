using EdS.Runtime;
using EdS.Domain.Space;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// V2.25.2 : pierres du Seuil (faces gravées vers le centre du triangle, fissures qui luisent la nuit, cercle de cailloux, touffes),
/// barrière du moulin (vantaux qui s'ouvrent avec le passage), ruines (murs, piliers, débris) et carrière (front de taille, six blocs,
/// accessoires) faits à l'atelier. Emprises vérifiées sur les vraies données de zone (scenarios/zones/vallons.json) ; rien ne sort sur une
/// route ou un autre obstacle, sauf l'écart signalé (vantail droit ouvert). Données de zone et moteur inchangés. Sans Unity.
/// </summary>
[Trait("Category", "World")]
public class V2252DecorTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v2252_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static string Models => Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Modeles");

    private static string Game => Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game");

    private AdventureSession Vallons()
    {
        AdventureSession a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), Path.Combine(_folder, "partie.json"));
        a.Advance(0.05);
        return a;
    }

    private static (double MinX, double MaxX, double MinZ, double MaxZ) Box(SpaceObstacle o) =>
        (o.Points.Min(p => p.X.Raw / 10000.0), o.Points.Max(p => p.X.Raw / 10000.0), o.Points.Min(p => p.Z.Raw / 10000.0), o.Points.Max(p => p.Z.Raw / 10000.0));

    /// <summary>Points (grille 9 × 9) d'un rectangle du repère de Blender recentré (X0..X1, Y0..Y1) posé comme la vue (x = −X, z = −Y).</summary>
    private static List<(double X, double Z)> Footprint(double cx, double cz, double yaw, double x0, double x1, double y0, double y1)
    {
        var points = new List<(double, double)>();
        double a = yaw * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a);
        for (int i = 0; i <= 8; i++)
            for (int j = 0; j <= 8; j++)
            {
                double lx = -(x0 + (x1 - x0) * i / 8), lz = -(y0 + (y1 - y0) * j / 8);
                points.Add((cx + lx * cos + lz * sin, cz - lx * sin + lz * cos));
            }
        return points;
    }

    /// <summary>Plus petit écart au bord d'une route (négatif : sur la route).</summary>
    private static double RoadEdge(AdventureMap map, IEnumerable<(double X, double Z)> points) => points.Min(p => TownRules.RoadAt(p.X, p.Z, map.Roads).Edge);

    /// <summary>Obstacles (autres que <paramref name="own"/>) dont la boîte contient un des points.</summary>
    private static string[] Hits(AdventureMap map, IEnumerable<(double X, double Z)> points, params string[] own)
    {
        var list = points.ToList();
        return map.Obstacles.Where(o => !own.Contains(o.Id)).Where(o =>
        {
            var b = Box(o);
            return list.Any(p => p.X > b.MinX && p.X < b.MaxX && p.Z > b.MinZ && p.Z < b.MaxZ);
        }).Select(o => o.Id).ToArray();
    }

    // --- Fichiers ------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("pierre_seuil1", "pierre_seuil_m1", true)]
    [InlineData("pierre_seuil2", "pierre_seuil_m2", true)]
    [InlineData("pierre_seuil3", "pierre_seuil_m3", true)]
    [InlineData("porte_moulin", "barriere_moulin", false)]
    [InlineData("mur_nord", "ruines_mur_nord", false)]
    [InlineData("mur_sud", "ruines_mur_sud", false)]
    [InlineData("pilier1", "ruines_pilier1", false)]
    [InlineData("pilier2", "ruines_pilier2", false)]
    [InlineData("pilier3", "ruines_pilier3", false)]
    [InlineData("pilier4", "ruines_pilier4", false)]
    [InlineData("front_de_taille", "carriere_front_de_taille", false)]
    [InlineData("bloc1", "carriere_bloc1_m1", false)]
    [InlineData("bloc2", "carriere_bloc2_m2", false)]
    [InlineData("bloc3", "carriere_bloc3_m3", false)]
    [InlineData("bloc4", "carriere_bloc4_g1", false)]
    [InlineData("bloc5", "carriere_bloc5_g2", false)]
    [InlineData("bloc6", "carriere_bloc6_g3", false)]
    public void Chaque_obstacle_a_son_modele_et_ses_textures(string id, string name, bool emission)
    {
        string folder = Path.Combine(Models, "Batiments", id);
        string[] files = Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal).ToArray()!;
        var expected = new List<string> { name + ".fbx", name + "_couleur.jpg" };
        if (emission) expected.Add(name + "_emission.png");
        expected.Add(name + "_normale.png");
        Assert.Equal(expected, files);
        string path = "Assets/EdS/Modeles/Batiments/" + id + "/" + name + ".fbx";
        Assert.Equal(id, TownModels.OwnBuildingOf(path));
        Assert.Null(TownModels.OwnPartOf(path));
        Assert.Null(AtelierDecor.OwnDecorOf(path));
        Assert.Equal(StyleBuilding.None, TownModels.KindOfModel(path));
        // Pas un bâtiment du bourg : la vue passe par SpaceView.OwnPiece, avant le décor fait par le code.
        Assert.Equal(StyleBuilding.None, TownRules.BuildingFor(id));
        Fbx(Path.Combine(folder, name + ".fbx"));
        Textures(folder, name);
        if (emission) Assert.Equal((2048, 2048), V218BoulangerieTests.PngSize(File.ReadAllBytes(Path.Combine(folder, name + "_emission.png"))));
    }

    [Theory]
    [InlineData("pierres_seuil_accessoires", "caillou_g,caillou_m,caillou_p,touffe")]
    [InlineData("carriere", "chevre,outils,tas_eclats,traineau")]
    public void Les_petits_decors_partagent_l_atlas_de_leur_groupe(string group, string pieces)
    {
        string folder = Path.Combine(Models, "Decor", group);
        string[] files = Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal).ToArray()!;
        var expected = pieces.Split(',').Select(p => group + "_" + p + ".fbx").Concat(new[] { group + "_couleur.jpg", group + "_normale.png" })
            .OrderBy(f => f, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, files);
        foreach (string piece in pieces.Split(','))
        {
            string path = "Assets/EdS/Modeles/Decor/" + group + "/" + group + "_" + piece + ".fbx";
            Assert.Equal(group, AtelierDecor.OwnDecorOf(path));
            Assert.Null(TownModels.OwnBuildingOf(path));
            Assert.True(AtelierDecor.IsPiece(group + "_" + piece, piece));
            Fbx(Path.Combine(folder, group + "_" + piece + ".fbx"));
        }
        Textures(folder, group);
        Assert.Equal(group, AtelierDecor.OwnDecorOf("assets\\eds\\modeles\\DECOR\\" + group.ToUpperInvariant() + "\\x.FBX")!.ToLowerInvariant());
        Assert.Null(AtelierDecor.OwnDecorOf("Assets/EdS/Modeles/Decor/" + group + "/x.png"));
        Assert.False(AtelierDecor.IsPiece("pierres_seuil_accessoires_caillou_p", "caillou_m"));
        Assert.True(AtelierDecor.DoubleSided("pierres_seuil_accessoires"));
        Assert.False(AtelierDecor.DoubleSided("carriere"));
    }

    private static void Fbx(string path)
    {
        byte[] fbx = File.ReadAllBytes(path);
        Assert.StartsWith("Kaydara FBX Binary", System.Text.Encoding.ASCII.GetString(fbx, 0, 18), StringComparison.Ordinal);
        Assert.InRange(fbx.Length, 10_000, 200_000);
    }

    private static void Textures(string folder, string name)
    {
        Assert.Equal((1024, 1024), V218BoulangerieTests.PngSize(File.ReadAllBytes(Path.Combine(folder, name + "_normale.png"))));
        Assert.Equal((2048, 2048), V218BoulangerieTests.JpegSize(File.ReadAllBytes(Path.Combine(folder, name + "_couleur.jpg"))));
    }

    // --- Pierres du Seuil ----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Les trois pierres (coins (44 ; 28), (52 ; 30), (48 ; 36), 1 × 1) : centre du triangle (48,50 ; 31,83) ; lacets d'Unity qui tournent
    /// la face gravée (+z du modèle) vers lui : 50,2°, −71,6° et 180° (ceux que proposait le rapport de l'atelier).
    /// </summary>
    [Fact]
    public void Les_faces_gravees_regardent_le_centre_du_triangle()
    {
        AdventureMap map = Vallons().Map;
        (double X, double Z) heart = AtelierDecor.SeuilCenter(map.Obstacles)!.Value;
        Assert.Equal((48.5, 31.8333), (Math.Round(heart.X, 4), Math.Round(heart.Z, 4)));
        var expected = new Dictionary<string, ((double, double, double, double) Box, double Yaw)>
        {
            ["pierre_seuil1"] = ((44, 45, 28, 29), 50.194),
            ["pierre_seuil2"] = ((52, 53, 30, 31), -71.565),
            ["pierre_seuil3"] = ((48, 49, 36, 37), 180.0),
        };
        foreach (var (id, (box, yaw)) in expected)
        {
            var b = Box(map.Obstacles.Single(o => o.Id == id));
            Assert.Equal(box, b);
            Assert.True(AtelierDecor.IsSeuilStone(id));
            double cx = (b.MinX + b.MaxX) / 2, cz = (b.MinZ + b.MaxZ) / 2;
            double y = AtelierDecor.SeuilStoneYaw(cx, cz, heart);
            Assert.Equal(yaw, Math.Round(y, 3), 3);
            // Le +z du modèle tourné de ce lacet (sin, cos) va vers le centre.
            double a = y * Math.PI / 180, dx = heart.X - cx, dz = heart.Z - cz, d = Math.Sqrt(dx * dx + dz * dz);
            Assert.Equal(1.0, (Math.Sin(a) * dx + Math.Cos(a) * dz) / d, 9);
        }
        Assert.False(AtelierDecor.IsSeuilStone("pierre"));
        Assert.Null(AtelierDecor.SeuilCenter(Array.Empty<SpaceObstacle>()));
        // M2, le plus haut (3,20 m), est sur pierre_seuil2, la plus éloignée du village.
        Assert.True(File.Exists(Path.Combine(Models, "Batiments", "pierre_seuil2", "pierre_seuil_m2.fbx")));
    }

    [Fact]
    public void Les_fissures_luisent_doucement_le_jour_et_nettement_la_nuit()
    {
        Assert.Equal(AtelierDecor.SeuilGlowDay, AtelierDecor.SeuilGlow(0));
        Assert.Equal(AtelierDecor.SeuilGlowNight, AtelierDecor.SeuilGlow(1));
        Assert.Equal(AtelierDecor.SeuilGlowNight, AtelierDecor.SeuilGlow(7));
        Assert.Equal(AtelierDecor.SeuilGlowDay, AtelierDecor.SeuilGlow(-1));
        Assert.Equal(AtelierDecor.SeuilGlowDay, AtelierDecor.SeuilGlow(double.NaN));
        Assert.True(AtelierDecor.SeuilGlowDay < 0.3 && AtelierDecor.SeuilGlowNight <= 2.0, "discrète le jour, sans halo excessif la nuit");
        for (double l = 0; l < 1; l += 0.05) Assert.True(AtelierDecor.SeuilGlow(l + 0.05) > AtelierDecor.SeuilGlow(l));
        // Elle suit les lanternes du monde, relue à chaque image dehors, sans lumière ajoutée.
        string night = File.ReadAllText(Path.Combine(Game, "SpaceView.Night.cs"));
        int glow = night.IndexOf("SeuilGlow(StyleWorld.Lamps);", StringComparison.Ordinal);
        Assert.True(glow > 0 && glow < night.IndexOf("if (_nightRoot == null) return;", StringComparison.Ordinal));
    }

    /// <summary>
    /// Cercle de cailloux : 67 places à pas égal (0,61 m) sur le rayon de 6,50 m, toutes libres (ni obstacle ni route à moins de 0,30 m),
    /// les trois tailles, les mêmes à chaque fois ; six touffes par pierre, à 0,80 m de son centre, hors de son emprise.
    /// </summary>
    [Fact]
    public void Le_cercle_de_cailloux_et_les_touffes_ne_touchent_rien()
    {
        AdventureMap map = Vallons().Map;
        (double X, double Z) heart = AtelierDecor.SeuilCenter(map.Obstacles)!.Value;
        var pebbles = AtelierDecor.SeuilPebbles(heart, map.Obstacles, map.Roads);
        Assert.Equal(67, pebbles.Count);
        Assert.Equal(pebbles.Select(p => (p.Piece, p.X, p.Z, p.Yaw)), AtelierDecor.SeuilPebbles(heart, map.Obstacles, map.Roads).Select(p => (p.Piece, p.X, p.Z, p.Yaw)));
        Assert.Equal(new[] { "caillou_g", "caillou_m", "caillou_p" }, pebbles.Select(p => p.Piece).Distinct().OrderBy(p => p, StringComparer.Ordinal));
        foreach (DecorSpot p in pebbles)
            Assert.Equal(6.5, Math.Sqrt((p.X - heart.X) * (p.X - heart.X) + (p.Z - heart.Z) * (p.Z - heart.Z)), 9);
        Assert.Equal(0.6093, Math.Round(Math.Sqrt(Math.Pow(pebbles[1].X - pebbles[0].X, 2) + Math.Pow(pebbles[1].Z - pebbles[0].Z, 2)), 4));
        // Caillou G : 0,23 × 0,15 m ; écart de 0,30 m gardé autour de son centre.
        var spots = pebbles.Select(p => (p.X, p.Z)).ToList();
        Assert.Empty(Hits(map, spots));
        Assert.True(RoadEdge(map, spots) > 0.3);
        // Une place sur un obstacle ou sur une route n'a pas de caillou.
        var blocker = new SpaceObstacle("bloc_test", new[] { G(42, 31), G(43, 31), G(43, 33), G(42, 33) });
        Assert.Equal(63, AtelierDecor.SeuilPebbles(heart, map.Obstacles.Append(blocker).ToList(), map.Roads).Count);
        var road = new StyleRoad("chemin_test", new[] { (heart.X + 6.5, heart.Z - 10), (heart.X + 6.5, heart.Z + 10) }, 1.0, false);
        Assert.InRange(AtelierDecor.SeuilPebbles(heart, map.Obstacles, map.Roads.Append(road).ToList()).Count, 50, 64);

        var tufts = AtelierDecor.SeuilTufts(map.Obstacles);
        Assert.Equal(18, tufts.Count);
        Assert.All(tufts, t => Assert.Equal("touffe", t.Piece));
        Assert.Equal(new[] { 0.5, 0.75, 1.0 }, tufts.Select(t => t.Scale).Distinct().OrderBy(s => s));
        foreach (SpaceObstacle stone in map.Obstacles.Where(o => AtelierDecor.IsSeuilStone(o.Id)))
        {
            var b = Box(stone);
            var mine = tufts.Where(t => Math.Abs(t.X - (b.MinX + b.MaxX) / 2) < 1 && Math.Abs(t.Z - (b.MinZ + b.MaxZ) / 2) < 1).ToList();
            Assert.Equal(6, mine.Count);
            foreach (DecorSpot t in mine)
            {
                Assert.Equal(0.8, Math.Sqrt(Math.Pow(t.X - (b.MinX + b.MaxX) / 2, 2) + Math.Pow(t.Z - (b.MinZ + b.MaxZ) / 2, 2)), 9);
                // Au pied de la pierre : le centre de la touffe hors de son emprise (à 0,066 m au moins de ses côtés, en diagonale).
                Assert.True(Math.Max(Math.Max(b.MinX - t.X, t.X - b.MaxX), Math.Max(b.MinZ - t.Z, t.Z - b.MaxZ)) >= 0.06);
            }
        }
        Assert.Empty(Hits(map, tufts.Select(t => (t.X, t.Z))));
        Assert.True(RoadEdge(map, tufts.Select(t => (t.X, t.Z))) > 1);
    }

    private static GroundVector G(double x, double z) => new GroundVector(EdS.Domain.Numerics.Fixed.FromRaw((long)(x * 10000)), EdS.Domain.Numerics.Fixed.FromRaw((long)(z * 10000)));

    // --- Barrière du moulin --------------------------------------------------------------------------------------------------------

    /// <summary>
    /// La barrière (6 × 1, porte_moulin) a son avant au nord ; ses vantaux tournent sur leurs axes vers le moulin (sud). Fermée, elle tient
    /// dans l'emprise ; ouverte, ses vantaux restent hors de tout obstacle, mais le vantail droit (ouest) touche de 0,15 m le bord du chemin
    /// du moulin (écart signalé à Damien : la réservation de 3,05 m côté moulin n'est pas tout à fait libre).
    /// </summary>
    [Fact]
    public void Les_vantaux_s_ouvrent_vers_le_moulin()
    {
        AdventureMap map = Vallons().Map;
        var b = Box(map.Obstacles.Single(o => o.Id == "porte_moulin"));
        Assert.Equal((-6.0, 0.0, 19.0, 20.0), b);
        (int X, int Z) facing = TownModels.OwnFacing("porte_moulin", TownRules.FacingOf("porte_moulin", b.MinX, b.MinZ, b.MaxX, b.MaxZ, map.Roads, map.Doors));
        Assert.Equal((0, 1), facing);
        double yaw = TownModels.FacingYaw(facing), cx = -3, cz = 19.5;
        // Fermée (export : X −3 … 3, Y −0,50 … −0,13) : dans l'emprise.
        var closed = Footprint(cx, cz, yaw, -3, 3, -0.5, -0.13);
        Assert.All(closed, p => Assert.True(p.X >= b.MinX - 1e-9 && p.X <= b.MaxX + 1e-9 && p.Z >= b.MinZ && p.Z <= b.MaxZ));
        // Bout de chaque vantail (repère d'Unity : x = −X, z = −Y), ouvert de 90° : vers le sud (le moulin, z 6 – 12).
        var mill = Box(map.Obstacles.Single(o => o.Id == "moulin"));
        foreach (var (leaf, tipX) in new[] { ("G", 0.02), ("D", -0.02) })
        {
            (double px, double pz) = AtelierDecor.GatePivot(leaf)!.Value;
            var closedTip = AtelierDecor.GateTurn(leaf, 0, tipX, pz);
            Assert.Equal((tipX, pz), (Math.Round(closedTip.X, 9), Math.Round(closedTip.Z, 9)));
            var open = AtelierDecor.GateTurn(leaf, AtelierDecor.GateOpenDegrees, tipX, pz);
            Assert.Equal(px, open.X, 9);
            Assert.Equal(pz - Math.Abs(tipX - px), open.Z, 9);
            double worldZ = cz + open.Z;   // lacet 0 : z du modèle = z du monde
            Assert.True(worldZ < cz && worldZ > mill.MaxZ, "le vantail ouvert va vers le moulin sans l'atteindre");
        }
        // Vantaux ouverts (fiche : G X 0,31 – 0,60, D X 5,40 – 5,69, Y 0,31 – 2,98 ; recentrés de −3 et −0,5).
        var leafG = Footprint(cx, cz, yaw, -2.69, -2.40, -0.19, 2.481);
        var leafD = Footprint(cx, cz, yaw, 2.40, 2.69, -0.19, 2.479);
        Assert.Empty(Hits(map, leafG.Concat(leafD), "porte_moulin", "haie_ouest", "haie_est"));
        Assert.True(RoadEdge(map, leafG) > 0.8);
        Assert.Equal(-0.15, Math.Round(RoadEdge(map, leafD), 2), 2);
    }

    [Fact]
    public void L_ouverture_est_douce_et_ne_depend_pas_de_la_cadence()
    {
        Assert.Equal(0.0, AtelierDecor.GateAngle(0));
        Assert.Equal(90.0, AtelierDecor.GateAngle(1));
        Assert.Equal(45.0, AtelierDecor.GateAngle(0.5), 9);
        Assert.Equal(0.0, AtelierDecor.GateAngle(double.NaN));
        for (double p = 0; p < 1; p += 0.01) Assert.True(AtelierDecor.GateAngle(p + 0.01) >= AtelierDecor.GateAngle(p));
        Assert.True(AtelierDecor.GateAngle(0.02) < 0.2, "départ en douceur");
        foreach (int frames in new[] { 3, 30, 180, 1000 })
        {
            double p = 0;
            for (int i = 0; i < frames; i++) p = AtelierDecor.GateProgress(p, true, AtelierDecor.GateSeconds / frames);
            Assert.Equal(1.0, p, 9);
        }
        double half = 0;
        for (int i = 0; i < 90; i++) half = AtelierDecor.GateProgress(half, true, 1.0 / 60);
        Assert.Equal(0.5, half, 9);
        Assert.Equal(0.0, AtelierDecor.GateProgress(0.2, false, 10));
        Assert.Equal(0.3, AtelierDecor.GateProgress(0.3, true, -1));
        Assert.Equal(3.0, AtelierDecor.GateSeconds);
        Assert.Equal(-1, AtelierDecor.GateLeafSign("G"));
        Assert.Equal(1, AtelierDecor.GateLeafSign("D"));
        Assert.Equal(0, AtelierDecor.GateLeafSign("x"));
        Assert.Null(AtelierDecor.GatePivot("x"));
        foreach (var (name, part) in new[] { ("Vantail_G", "G"), ("Vantail_D", "D"), ("Chaine_Fermee", "fermee"), ("Chaine_Ouverte", "ouverte"),
                     ("Poteaux", "poteaux"), ("Vantail_G_LOD1", "G"), ("barriere_moulin", null) })
            Assert.Equal(part, AtelierDecor.GatePart(name));
        // GateSwing : pas une allocation par image, dt du moteur d'Unity, la chaîne fermée cachée dès l'ouverture.
        string swing = File.ReadAllText(Path.Combine(Game, "GateSwing.cs"));
        string update = swing.Substring(swing.IndexOf("private void Update()", StringComparison.Ordinal));
        update = update.Substring(0, update.IndexOf("private void Apply()", StringComparison.Ordinal));
        Assert.Contains("Time.deltaTime", update, StringComparison.Ordinal);
        Assert.DoesNotContain("new ", update, StringComparison.Ordinal);
        Assert.Contains("AtelierDecor.GateProgress", update, StringComparison.Ordinal);
    }

    /// <summary>
    /// La quête ouvre le passage du moulin (registre du monde) : la barrière s'ouvre aussitôt ; la carte garde l'obstacle du passage dans
    /// ses barrières (<see cref="AdventureMap.Gates"/>), pour qu'à la session suivante, l'obstacle retiré, la barrière soit posée ouverte.
    /// </summary>
    [Fact]
    public void La_barriere_suit_le_passage_ouvert_par_la_quete()
    {
        AdventureSession a = Vallons();
        Assert.Equal(new[] { "porte_moulin" }, a.Map.Gates.Select(g => g.Id));
        Assert.Equal(Box(a.Map.Obstacles.Single(o => o.Id == "porte_moulin")), Box(a.Map.Gates[0]));
        Assert.False(a.GateOpen("porte_moulin"));
        Assert.False(a.GateOpen("haie_est"));
        Assert.True(a.World.OpenPassage("moulin"));
        Assert.True(a.GateOpen("porte_moulin"));
        Assert.False(a.GateOpen("haie_est"));
        // Relecture de la PR #147 : une vue construite (ou refaite) après l'ouverture, l'obstacle encore là, pose la barrière ouverte d'un
        // coup à sa première lecture du registre ; seul un passage fermé → ouvert pendant que la vue existe s'anime.
        Assert.True(AtelierDecor.GateSnaps(false));
        Assert.False(AtelierDecor.GateSnaps(true));
        Assert.Contains(a.Map.Obstacles, o => o.Id == "porte_moulin");
        string view = File.ReadAllText(Path.Combine(Game, "SpaceView.cs"));
        string gates = view.Substring(view.IndexOf("public void Gates(AdventureSession adventure, float deltaTime)", StringComparison.Ordinal));
        gates = gates.Substring(0, gates.IndexOf("\n        }\n", StringComparison.Ordinal));
        Assert.Contains("bool snap = AtelierDecor.GateSnaps(_gatesSynced);", gates, StringComparison.Ordinal);
        Assert.Contains("if (snap) swing.Snap(open);", gates, StringComparison.Ordinal);
        Assert.Contains("else swing.Open = open;", gates, StringComparison.Ordinal);
        Assert.Contains("if (_gatePoll > 0f && _gatesSynced) return;", gates, StringComparison.Ordinal);
        Assert.Contains("OwnPiece(gate.Id, gate.Points.Select(", view, StringComparison.Ordinal);
        Assert.Contains("_arena.Gates(a, Time.deltaTime);", File.ReadAllText(Path.Combine(Game, "EdsGame.cs")), StringComparison.Ordinal);
    }

    // --- Ruines ----------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Murs (1 × 12,5 et 1 × 13,5, tournés d'un quart de tour, ancienne face intérieure à l'est) et débris (objets du même modèle, bornes
    /// de l'export) : hors des routes (passage des ruines, chemin de l'est) et des autres obstacles ; le lieu « ruines » est le leur (les
    /// tours de l'Asset Store n'y sont plus posées).
    /// </summary>
    [Theory]
    [InlineData("mur_nord", 6.25, 17.0, 18.0, 1.5, 14.0)]
    [InlineData("mur_sud", 6.75, 17.0, 18.0, -14.0, -0.5)]
    public void Les_murs_en_ruine_et_leurs_debris_ne_touchent_rien(string id, double half, double x0, double x1, double z0, double z1)
    {
        AdventureMap map = Vallons().Map;
        var b = Box(map.Obstacles.Single(o => o.Id == id));
        Assert.Equal((x0, x1, z0, z1), b);
        (int X, int Z) facing = TownModels.OwnFacing(id, TownRules.FacingOf(id, b.MinX, b.MinZ, b.MaxX, b.MaxZ, map.Roads, map.Doors));
        Assert.Equal((1, 0), facing);
        double yaw = TownModels.FacingYaw(facing), cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
        var wall = Footprint(cx, cz, yaw, -half, half, -0.5, 0.5);
        Assert.All(wall, p => Assert.True(p.X >= x0 - 1e-9 && p.X <= x1 + 1e-9 && p.Z >= z0 - 1e-9 && p.Z <= z1 + 1e-9));
        // Débris (centre X, Y et demi-côtés, repère recentré) : N1 – N6 ou S1 – S6.
        double[][] debris = id == "mur_nord"
            ? new[] { new[] { -4.75, -0.85, .275, .2 }, new[] { -0.45, -0.9, .3, .25 }, new[] { 0.95, -0.8, .175, .175 }, new[] { 4.55, -0.85, .25, .2 }, new[] { -3.25, 0.85, .225, .2 }, new[] { 3.25, 0.85, .275, .2 } }
            : new[] { new[] { -4.95, -0.85, .275, .2 }, new[] { -2.15, -0.9, .3, .25 }, new[] { 1.75, -0.95, .275, .25 }, new[] { 4.95, -0.85, .3, .2 }, new[] { -3.55, 0.85, .225, .2 }, new[] { 2.85, 0.85, .275, .2 } };
        foreach (double[] d in debris)
        {
            var pts = Footprint(cx, cz, yaw, d[0] - d[2], d[0] + d[2], d[1] - d[3], d[1] + d[3]);
            Assert.Empty(Hits(map, pts));
            Assert.True(RoadEdge(map, pts) > 0.5, id + " : débris trop près d'une route");
        }
        Assert.True(RoadEdge(map, wall) >= 0.2 - 1e-9);
        MapPlace ruins = map.Places.Single(p => p.Id == "ruines");
        Assert.True(ruins.MinX <= x0 && x1 <= ruins.MaxX && ruins.MinZ <= z0 && z1 <= ruins.MaxZ);
        Assert.True(cx < (ruins.MinX + ruins.MaxX) / 2, "face intérieure (+x) vers le centre du lieu");
    }

    /// <summary>
    /// Piliers en ruine (0,5 × 0,5, éclats au pied ; export : X −0,25 … 0,42, Y −0,43 … 0,25) sur pilier1 à pilier4 : hors des routes et
    /// des autres obstacles, quelle que soit leur façade. Ils ne sont pas dans le lieu « ruines » (ils portaient l'abri fait par le code,
    /// qui n'est plus posé sur eux : question à Damien).
    /// </summary>
    [Fact]
    public void Les_piliers_en_ruine_ne_touchent_rien_et_l_abri_n_est_plus_pose()
    {
        AdventureMap map = Vallons().Map;
        MapPlace ruins = map.Places.Single(p => p.Id == "ruines");
        foreach (string id in new[] { "pilier1", "pilier2", "pilier3", "pilier4" })
        {
            var b = Box(map.Obstacles.Single(o => o.Id == id));
            Assert.Equal(0.5, b.MaxX - b.MinX, 9);
            Assert.Equal(0.5, b.MaxZ - b.MinZ, 9);
            Assert.False(b.MinX >= ruins.MinX && b.MaxX <= ruins.MaxX && b.MinZ >= ruins.MinZ && b.MaxZ <= ruins.MaxZ);
            (int X, int Z) facing = TownModels.OwnFacing(id, TownRules.FacingOf(id, b.MinX, b.MinZ, b.MaxX, b.MaxZ, map.Roads, map.Doors));
            var pts = Footprint((b.MinX + b.MaxX) / 2, (b.MinZ + b.MaxZ) / 2, TownModels.FacingYaw(facing), -0.25, 0.42, -0.43, 0.25);
            Assert.Empty(Hits(map, pts, id));
            Assert.True(RoadEdge(map, pts) > 0, id + " : sur une route");
        }
        string view = File.ReadAllText(Path.Combine(Game, "SpaceView.cs"));
        Assert.Contains("pillars.Any(o => catalog.OwnBuilding(o.Id) != null)) return;", view, StringComparison.Ordinal);
        Assert.Contains("bool ownRuins = catalog != null && catalog.OwnBuilding(\"mur_nord\") != null;", File.ReadAllText(Path.Combine(Game, "WorldMarkers.cs")), StringComparison.Ordinal);
    }

    // --- Carrière --------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Front de taille (28 × 4, tourné d'un quart de tour, côté exploité à l'ouest, vers le fond de la carrière) et six blocs (3 × 3 et
    /// 4 × 4) : chacun tient dans son emprise.
    /// </summary>
    [Theory]
    [InlineData("front_de_taille", 14.0, 2.0, 56, 60, -18, 10)]
    [InlineData("bloc1", 1.5, 1.5, 32, 35, -10, -7)]
    [InlineData("bloc2", 1.5, 1.5, 40, 43, 4, 7)]
    [InlineData("bloc3", 1.5, 1.5, 50, 53, -14, -11)]
    [InlineData("bloc4", 2.0, 2.0, 70, 74, 30, 34)]
    [InlineData("bloc5", 2.0, 2.0, 86, 90, 45, 49)]
    [InlineData("bloc6", 2.0, 2.0, 102, 106, 26, 30)]
    public void Le_front_et_les_blocs_tiennent_dans_leur_emprise(string id, double hx, double hy, double x0, double x1, double z0, double z1)
    {
        AdventureMap map = Vallons().Map;
        var b = Box(map.Obstacles.Single(o => o.Id == id));
        Assert.Equal((x0, x1, z0, z1), b);
        (int X, int Z) facing = TownModels.OwnFacing(id, TownRules.FacingOf(id, b.MinX, b.MinZ, b.MaxX, b.MaxZ, map.Roads, map.Doors));
        if (id == "front_de_taille") Assert.Equal((-1, 0), facing);
        var pts = Footprint((x0 + x1) / 2, (z0 + z1) / 2, TownModels.FacingYaw(facing), -hx, hx, -hy, hy);
        Assert.All(pts, p => Assert.True(p.X >= x0 - 1e-9 && p.X <= x1 + 1e-9 && p.Z >= z0 - 1e-9 && p.Z <= z1 + 1e-9));
        if (id == "front_de_taille")
        {
            // Le côté exploité (Blender −Y, +z du modèle) regarde l'ouest, vers le fond de la carrière (lieu x 28 – 60).
            var front = Footprint(58, -4, TownModels.FacingYaw(facing), 0, 0, -2, -2)[0];
            Assert.Equal((56.0, -4.0), (Math.Round(front.X, 9), Math.Round(front.Z, 9)));
        }
    }

    /// <summary>
    /// Accessoires (chèvre, traîneau, outils, tas d'éclats ; bornes de l'export) devant le front : dans la carrière, hors des routes et
    /// des obstacles, à 2,5 m au moins des nœuds de récolte, points sûrs, ateliers et PNJ.
    /// </summary>
    [Fact]
    public void Les_accessoires_de_la_carriere_ne_genent_rien()
    {
        AdventureSession a = Vallons();
        AdventureMap map = a.Map;
        var b = Box(map.Obstacles.Single(o => o.Id == "front_de_taille"));
        var props = AtelierDecor.QuarryProps(b.MinX, b.MinZ, b.MaxX, b.MaxZ, (-1, 0));
        Assert.Equal(new[] { "chevre", "traineau", "outils", "tas_eclats" }, props.Select(p => p.Piece));
        Assert.Equal((52.0, -3.0, 90.0), (props[0].X, props[0].Z, props[0].Yaw));
        var half = new Dictionary<string, (double X, double Y)> { ["chevre"] = (1.29, 1.39), ["traineau"] = (0.7, 1.5), ["outils"] = (0.8, 0.68), ["tas_eclats"] = (1.6, 1.2) };
        var keep = a.Nodes().Concat(a.SafePoints()).Concat(a.Workshops()).Concat(a.Npcs()).Select(m => (m.Id, m.X, m.Z)).ToList();
        Assert.Contains(keep, k => k.Id == "filon_est");
        MapPlace quarry = map.Places.Single(p => p.Id == "carriere");
        foreach (DecorSpot p in props)
        {
            var (hx, hy) = half[p.Piece];
            var pts = Footprint(p.X, p.Z, p.Yaw, -hx, hx, -hy, hy);
            Assert.Empty(Hits(map, pts));
            Assert.True(RoadEdge(map, pts) > 2, p.Piece + " : trop près d'une route");
            Assert.All(pts, q => Assert.True(q.X >= quarry.MinX && q.X <= quarry.MaxX && q.Z >= quarry.MinZ && q.Z <= quarry.MaxZ, p.Piece + " hors de la carrière"));
            foreach (var k in keep) Assert.True(pts.Min(q => Math.Sqrt((q.X - k.X) * (q.X - k.X) + (q.Z - k.Z) * (q.Z - k.Z))) >= 2.5, p.Piece + " gêne " + k.Id);
        }
        // Les accessoires ne se chevauchent pas (boîtes de leurs emprises tournées, 0,5 m d'écart au moins), tas d'éclats compris.
        Assert.Equal((53.5, -16.3), (props[3].X, props[3].Z));
        var boxes = props.Select(p =>
        {
            var (hx, hy) = half[p.Piece];
            var pts = Footprint(p.X, p.Z, p.Yaw, -hx, hx, -hy, hy);
            return (p.Piece, X0: pts.Min(q => q.X), X1: pts.Max(q => q.X), Z0: pts.Min(q => q.Z), Z1: pts.Max(q => q.Z));
        }).ToList();
        for (int i = 0; i < boxes.Count; i++)
            for (int j = i + 1; j < boxes.Count; j++)
            {
                double gap = Math.Max(Math.Max(boxes[j].X0 - boxes[i].X1, boxes[i].X0 - boxes[j].X1), Math.Max(boxes[j].Z0 - boxes[i].Z1, boxes[i].Z0 - boxes[j].Z1));
                Assert.True(gap >= 0.5, boxes[i].Piece + " chevauche " + boxes[j].Piece + " (" + gap + ")");
            }
        string view = File.ReadAllText(Path.Combine(Game, "SpaceView.cs"));
        int own = view.IndexOf("OwnPiece(id, points)) return;", StringComparison.Ordinal);
        Assert.True(own > 0 && own < view.IndexOf("QuarryFace(id, points)) return;", StringComparison.Ordinal)
            && own < view.IndexOf("Wave5Role.Menhir", StringComparison.Ordinal), "les modèles de l'atelier avant le front, les menhirs et les rochers faits par le code");
    }

    // --- Relief (V2.24) ----------------------------------------------------------------------------------------------------------

    /// <summary>Plus grand écart de hauteur du sol affiché entre les points et <paramref name="level"/>.</summary>
    private static double Spread(GroundRelief relief, IEnumerable<(double X, double Z)> points, double level) =>
        points.Max(p => Math.Abs(relief.Height(p.X, p.Z) - level));

    /// <summary>Hauteur où la vue pose un obstacle (SpaceView.Ground : le plus bas du centre et de quatre points à 0,4 m).</summary>
    private static double Seat(GroundRelief relief, double x, double z, double r) =>
        new[] { (x, z), (x - r, z), (x + r, z), (x, z - r), (x, z + r) }.Min(p => relief.Height(p.Item1, p.Item2));

    /// <summary>
    /// Relief fusionné (V2.24) : chaque obstacle de V2.25.2 est bâti (replat horizontal jusqu'à 1,5 m de son emprise) ; la vue le monte
    /// tout entier à ce niveau (SpaceView.Obstacle) : le modèle, ses débris, éclats et vantaux ouverts (qui ne sortent pas de 1,5 m) sont au
    /// sol, ni enterrés ni flottants (5 mm près). Les vantaux tournent autour d'axes verticaux (rotation autour de y seulement).
    /// </summary>
    [Fact]
    public void Les_obstacles_de_l_atelier_sont_sur_leur_replat()
    {
        AdventureMap map = Vallons().Map;
        GroundRelief relief = GroundRelief.For(map);
        Assert.False(relief.IsFlat);
        var reach = new Dictionary<string, (double X0, double X1, double Y0, double Y1)>
        {
            ["pierre_seuil1"] = (-0.5, 0.5, -0.5, 0.5), ["pierre_seuil2"] = (-0.5, 0.5, -0.5, 0.5), ["pierre_seuil3"] = (-0.5, 0.5, -0.5, 0.5),
            ["porte_moulin"] = (-3, 3, -0.5, -0.13), ["mur_nord"] = (-6.25, 6.25, -1.15, 1.05), ["mur_sud"] = (-6.75, 6.75, -1.2, 1.05),
            ["pilier1"] = (-0.25, 0.42, -0.43, 0.25), ["pilier2"] = (-0.25, 0.42, -0.43, 0.25), ["pilier3"] = (-0.25, 0.42, -0.43, 0.25),
            ["pilier4"] = (-0.25, 0.42, -0.43, 0.25), ["front_de_taille"] = (-14, 14, -2, 2), ["bloc1"] = (-1.5, 1.5, -1.5, 1.5),
            ["bloc2"] = (-1.5, 1.5, -1.5, 1.5), ["bloc3"] = (-1.5, 1.5, -1.5, 1.5), ["bloc4"] = (-2, 2, -2, 2), ["bloc5"] = (-2, 2, -2, 2),
            ["bloc6"] = (-2, 2, -2, 2),
        };
        foreach (var (id, r) in reach)
        {
            SpaceObstacle o = map.Obstacles.Single(x => x.Id == id);
            Assert.True(GroundRelief.Builds(o), id + " : pas de replat");
            var b = Box(o);
            double cx = (b.MinX + b.MaxX) / 2, cz = (b.MinZ + b.MaxZ) / 2;
            (int X, int Z) facing = TownModels.OwnFacing(id, TownRules.FacingOf(id, b.MinX, b.MinZ, b.MaxX, b.MaxZ, map.Roads, map.Doors));
            // Les pierres tournent d'un lacet quelconque : leur cercle de 0,71 m tient dans le replat.
            double yaw = AtelierDecor.IsSeuilStone(id) ? AtelierDecor.SeuilStoneYaw(cx, cz, AtelierDecor.SeuilCenter(map.Obstacles)!.Value) : TownModels.FacingYaw(facing);
            var pts = Footprint(cx, cz, yaw, r.X0, r.X1, r.Y0, r.Y1);
            Assert.All(pts, p => Assert.True(Math.Max(Math.Max(b.MinX - p.X, p.X - b.MaxX), Math.Max(b.MinZ - p.Z, p.Z - b.MaxZ)) <= 1.5 + 1e-9, id + " sort du replat"));
            double seat = Seat(relief, cx, cz, 0.4);
            Assert.True(Spread(relief, pts, seat) < 0.005, id + " : " + Spread(relief, pts, seat));
        }
        // Vantaux ouverts (bout à 2 m de l'emprise, au-delà du replat de 1,5 m) : moins de 1 mm d'écart au sol (mesuré).
        double gateSeat = Seat(relief, -3, 19.5, 0.4);
        double leaves = Spread(relief, Footprint(-3, 19.5, 0, -2.69, -2.40, -0.19, 2.481).Concat(Footprint(-3, 19.5, 0, 2.40, 2.69, -0.19, 2.479)), gateSeat);
        Assert.True(leaves < 0.005, "vantaux : " + leaves);
        // Les pierres sont sur la butte du Seuil (plus haut que la plaine alentour).
        Assert.True(Seat(relief, 44.5, 28.5, 0.4) > relief.Height(44.5, 10) + 0.5);
        string view = File.ReadAllText(Path.Combine(Game, "SpaceView.cs"));
        Assert.Contains("Lift(children, occluders, boxes, Ground(cx, cz, 0.4f));", view, StringComparison.Ordinal);
        string swing = File.ReadAllText(Path.Combine(Game, "GateSwing.cs"));
        Assert.Contains("Quaternion.Euler(0f, AtelierDecor.GateLeafSign(\"G\") * angle, 0f)", swing, StringComparison.Ordinal);
        Assert.Contains("Quaternion.Euler(0f, AtelierDecor.GateLeafSign(\"D\") * angle, 0f)", swing, StringComparison.Ordinal);
    }

    /// <summary>
    /// Passage ouvert (session suivante : l'obstacle retiré) : la porte garde son replat (<see cref="AdventureMap.Gates"/>), la barrière
    /// ouverte y est posée au même niveau qu'avant l'ouverture (SpaceView.OpenGates la monte comme un obstacle).
    /// </summary>
    [Fact]
    public void La_barriere_ouverte_garde_son_replat()
    {
        AdventureMap map = Vallons().Map;
        SpaceObstacle gate = map.Gates.Single();
        var space = new CombatSpace(G(map.MinX, map.MinZ), G(map.MaxX, map.MaxZ), map.Obstacles.Where(o => o.Id != gate.Id));
        var open = new AdventureMap(space, map.Places, map.Roads, map.Clearings, map.Doors, false, null, map.Gates);
        Assert.DoesNotContain(open.Obstacles, o => o.Id == gate.Id);
        GroundRelief before = GroundRelief.For(map), after = GroundRelief.For(open);
        Assert.Contains(after.Pads, p => p.Id == "porte_moulin");
        double level = Seat(before, -3, 19.5, 0.4);
        Assert.Equal(level, Seat(after, -3, 19.5, 0.4), 3);
        var leaves = Footprint(-3, 19.5, 0, -3, 3, -0.5, 2.5);
        Assert.True(Spread(after, leaves, Seat(after, -3, 19.5, 0.4)) < 0.005);
        string view = File.ReadAllText(Path.Combine(Game, "SpaceView.cs"));
        int gates = view.IndexOf("private void OpenGates(", StringComparison.Ordinal);
        Assert.True(view.IndexOf("Lift(children, occluders, boxes, Ground(", gates, StringComparison.Ordinal) > gates);
    }

    /// <summary>
    /// Cailloux, touffes et accessoires de la carrière, posés par la vue au plus bas du sol affiché sous leur pied
    /// (<see cref="AtelierDecor.Foot"/>) : rien ne flotte, et le sol ne monte pas de plus de quelques centimètres sous eux (rien d'enfoui).
    /// </summary>
    [Fact]
    public void Les_petits_decors_suivent_le_sol()
    {
        AdventureSession a = Vallons();
        AdventureMap map = a.Map;
        GroundRelief relief = GroundRelief.For(map);
        (double X, double Z) heart = AtelierDecor.SeuilCenter(map.Obstacles)!.Value;
        var b = Box(map.Obstacles.Single(o => o.Id == "front_de_taille"));
        var spots = AtelierDecor.SeuilPebbles(heart, map.Obstacles, map.Roads).Concat(AtelierDecor.SeuilTufts(map.Obstacles))
            .Concat(AtelierDecor.QuarryProps(b.MinX, b.MinZ, b.MaxX, b.MaxZ, (-1, 0))).ToList();
        double worst = 0;
        foreach (DecorSpot p in spots)
        {
            double r = AtelierDecor.Foot(p.Piece) * p.Scale, seat = Seat(relief, p.X, p.Z, AtelierDecor.Foot(p.Piece));
            var under = Footprint(p.X, p.Z, 0, -r, r, -r, r);
            double buried = under.Max(q => relief.Height(q.X, q.Z)) - seat;
            Assert.True(seat <= relief.Height(p.X, p.Z) + 1e-9, p.Piece + " flotte");
            worst = Math.Max(worst, buried / Math.Max(r, 0.1));
            Assert.True(buried < (r > 0.5 ? 0.15 : 0.03), p.Piece + " enfoui de " + buried);
        }
        Assert.True(worst < 0.2);
        Assert.Equal(new[] { 0.09, 0.11, 0.12, 0.11, 1.39, 1.5, 0.8, 1.6 },
            new[] { "caillou_p", "caillou_m", "caillou_g", "touffe", "chevre", "traineau", "outils", "tas_eclats" }.Select(AtelierDecor.Foot));
        string view = File.ReadAllText(Path.Combine(Game, "SpaceView.cs"));
        Assert.Contains("Ground(x, z, (float)AtelierDecor.Foot(spot.Piece))", view, StringComparison.Ordinal);
    }
}
