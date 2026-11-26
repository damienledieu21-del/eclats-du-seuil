using EdS.Data.Zones;
using EdS.Runtime;
using EdS.World;

namespace EdS.Data.Tests;

/// <summary>
/// Chantier V2.51b : l'Envers des Vallons à l'écran, règles pures (<see cref="EnversLook"/>) : pièces de l'Envers et leurs modèles, variantes
/// sombres des matériaux, lumière qui respire, saison figée, temps montré, cendres, formes flottantes (déterministes, au-dessus des têtes,
/// nombre selon la machine), pose d'un modèle d'après ses bornes réelles (échelle avant le lacet), bourdon en boucle, musique du Seuil.
/// </summary>
[Trait("Category", "World")]
public sealed class V251bEnversUnityTests
{
    [Fact]
    public void Pieces_de_l_Envers_et_modeles_du_kit()
    {
        Assert.Equal(EnversPiece.DeadTree, EnversLook.PieceFor("envers_arbre_mort_3"));
        Assert.Equal(EnversPiece.InvertedRuin, EnversLook.PieceFor("envers_ruine_inversee_1"));
        Assert.Equal(EnversPiece.BlackShard, EnversLook.PieceFor("envers_eclat_noir_2"));
        Assert.Equal(EnversPiece.DawnAltar, EnversLook.PieceFor("envers_autel_de_l_aube"));
        Assert.Equal(EnversPiece.None, EnversLook.PieceFor("moulin"));
        Assert.Equal(EnversPiece.None, EnversLook.PieceFor(null));
        // Chaque obstacle propre de l'Envers livré est une pièce connue, avec son modèle et sa hauteur.
        ZoneDefinition envers = V16TestData.Interiors.Single(z => z.Envers);
        var own = envers.Setup.Space!.Obstacles.Where(o => o.Id.StartsWith("envers_", StringComparison.Ordinal)).ToList();
        Assert.Equal(10, own.Count);
        foreach (var o in own)
        {
            EnversPiece piece = EnversLook.PieceFor(o.Id);
            Assert.NotEqual(EnversPiece.None, piece);
            Assert.NotNull(EnversLook.ModelFor(piece));
            Assert.InRange(EnversLook.HeightOf(piece), 1.0, 8.0);
        }
        Assert.True(EnversLook.Occludes(EnversPiece.DeadTree) && EnversLook.Occludes(EnversPiece.InvertedRuin));
        Assert.False(EnversLook.Occludes(EnversPiece.BlackShard) || EnversLook.Occludes(EnversPiece.DawnAltar));
        // Les noms du kit sont ceux de l'AssetLab (Envers/modeles.json) : Resources/Envers/<nom>.
        Assert.Equal("Envers/", EnversLook.Folder);
        Assert.Equal(new[] { "rocher_flottant", "debris_flottants", "arche_brisee_flottante" }, EnversLook.FloatingModels);
        Assert.Equal(new[] { "faille_envers", "arbre_mort_inverse", "ruine_inversee", "eclat_noir", "autel_de_l_aube" },
            new[] { EnversLook.RiftModel, EnversLook.DeadTreeModel, EnversLook.RuinModel, EnversLook.ShardModel, EnversLook.AltarModel });
    }

    [Theory]
    [InlineData(1.0, 1.0, 1.0)]
    [InlineData(0.0, 0.0, 0.0)]
    [InlineData(0.9, 0.2, 0.1)]
    [InlineData(0.2, 0.8, 0.3)]
    [InlineData(0.95, 0.95, 0.6)]
    [InlineData(-3.0, 2.0, double.NaN)]
    public void Variante_sombre_jamais_plus_claire_et_tiree_vers_le_violet(double r, double g, double b)
    {
        (double dr, double dg, double db) = EnversLook.Darken(r, g, b);
        foreach (double c in new[] { dr, dg, db }) Assert.InRange(c, 0.0, 1.0);
        double before = EnversLook.Luminance(Math.Clamp(double.IsNaN(r) ? 0 : r, 0, 1), Math.Clamp(double.IsNaN(g) ? 0 : g, 0, 1), Math.Clamp(double.IsNaN(b) ? 0 : b, 0, 1));
        Assert.True(EnversLook.Luminance(dr, dg, db) <= before + 1e-12);
        if (before > 0.05) Assert.True(EnversLook.Luminance(dr, dg, db) < before * 0.8);   // nettement plus sombre
        // Un gris devient un gris violet : le bleu passe devant le vert.
        if (r == g && g == b && r > 0) Assert.True(db > dg);
    }

    [Fact]
    public void Lumiere_qui_respire_crepuscule_perpetuel()
    {
        var all = Enumerable.Range(0, 1440).Select(EnversLook.Light).ToList();
        // Un crépuscule : jamais de plein jour, jamais le noir.
        Assert.All(all, l => Assert.InRange(l.SunIntensity, 0.3, 0.6));
        Assert.All(all, l => Assert.InRange(l.SunHeight, 10, 18));
        Assert.All(all, l => Assert.True(l.FogEnd > l.FogStart && l.FogStart >= 30));
        Assert.All(all, l => Assert.True(l.AmbientSky.B > l.AmbientSky.G));   // violet
        // Trois souffles par jour : la lumière respire (plus claire, puis plus sombre), et la même minute donne la même lumière.
        Assert.True(all.Max(l => l.SunIntensity) > all.Min(l => l.SunIntensity) * 1.2);
        Assert.Equal(EnversLook.Light(100).SunIntensity, EnversLook.Light(100 + 1440).SunIntensity);
        Assert.Equal(EnversLook.Light(-1).SunIntensity, EnversLook.Light(1439).SunIntensity);
        Assert.Equal(EnversLook.Light(0).SunIntensity, EnversLook.Light(480).SunIntensity, 9);
        // Le « soleil noir » est à l'opposé du soleil de la surface (vers le nord).
        Assert.All(all, l => Assert.InRange(l.SunTurn, 150, 210));
    }

    [Fact]
    public void Pas_de_saison_ni_de_neige_dans_l_Envers()
    {
        SeasonLook s = EnversLook.Season;
        Assert.Equal((0f, 0f, 0f), (s.Snow, s.Ice, s.PassSnow));
        Assert.Equal(2, s.Season);
    }

    [Fact]
    public void Le_temps_des_Vallons_montre_a_la_facon_de_l_Envers()
    {
        // Rien ne tombe dans l'Envers : aucune pluie montrée ; la pluie de la surface y monte en cendres.
        foreach (WeatherKind k in Enum.GetValues<WeatherKind>())
        {
            WeatherKind shown = EnversLook.ShownWeather(k);
            Assert.Contains(shown, new[] { WeatherKind.Cloudy, WeatherKind.Mist });
            Assert.Equal(0, WeatherCycle.Typical(shown).Rain);
            Assert.InRange(EnversLook.AshDensity(k), 0.2, 1.5);
        }
        Assert.True(EnversLook.AshDensity(WeatherKind.Storm) > EnversLook.AshDensity(WeatherKind.Shower));
        Assert.True(EnversLook.AshDensity(WeatherKind.Shower) > EnversLook.AshDensity(WeatherKind.Clear));
    }

    [Fact]
    public void Cendres_autour_de_la_camera_qui_montent_sans_etat()
    {
        Assert.Equal((12, 32, 80), (EnversLook.MaxAsh(DeviceTier.Phone), EnversLook.MaxAsh(DeviceTier.Modest), EnversLook.MaxAsh(DeviceTier.Full)));
        for (int i = 0; i < 80; i++)
        {
            var p = EnversLook.Ash(i, 1640, -455, 12.5);
            Assert.InRange(p.X, 1640 - EnversLook.AshBox / 2, 1640 + EnversLook.AshBox / 2);
            Assert.InRange(p.Z, -455 - EnversLook.AshBox / 2, -455 + EnversLook.AshBox / 2);
            Assert.InRange(p.Y, 0, EnversLook.AshHeight);
            Assert.Equal(p, EnversLook.Ash(i, 1640, -455, 12.5));   // aucun état
        }
        // Elle monte (avant de repartir du sol), et une cendre reste à sa place dans le monde quand la caméra bouge un peu.
        var a = EnversLook.Ash(3, 0, 0, 1.0);
        var b = EnversLook.Ash(3, 0, 0, 1.5);
        Assert.True(b.Y > a.Y || b.Y < 1);
        var c = EnversLook.Ash(3, 2, 1, 1.0);
        if (Math.Abs(c.X - a.X) < EnversLook.AshBox / 2) Assert.Equal(a.X, c.X, 6);
    }

    [Fact]
    public void Formes_flottantes_deterministes_au_dessus_des_tetes_et_selon_la_machine()
    {
        RegionGrid region = RegionGrid.Vallons();
        Assert.Equal((1, 2, 4), (EnversLook.FloatingPerCell(DeviceTier.Phone), EnversLook.FloatingPerCell(DeviceTier.Modest), EnversLook.FloatingPerCell(DeviceTier.Full)));
        int total = 0;
        var models = new HashSet<int>();
        for (int c = 0; c < region.Columns; c++)
            for (int r = 0; r < region.Rows; r++)
            {
                var strong = EnversLook.FloatingRocks(region, c, r, DeviceTier.Full);
                var modest = EnversLook.FloatingRocks(region, c, r, DeviceTier.Modest);
                // Celles d'une machine plus faible sont les premières d'une machine plus forte.
                Assert.Equal(modest.Select(x => (x.X, x.Y, x.Z)), strong.Take(modest.Count).Select(x => (x.X, x.Y, x.Z)));
                foreach (FloatingRock rock in strong)
                {
                    double x0 = region.OriginX + c * RegionGrid.CellMeters, z0 = region.OriginZ + r * RegionGrid.CellMeters;
                    Assert.InRange(rock.X, x0 + 8, x0 + RegionGrid.CellMeters - 8);
                    Assert.InRange(rock.Z, z0 + 8, z0 + RegionGrid.CellMeters - 8);
                    Assert.InRange(rock.Y, EnversLook.MinFloatHeight, EnversLook.MaxFloatHeight);
                    Assert.InRange(rock.Size, EnversLook.MinFloatSize, EnversLook.MaxFloatSize);
                    // Toujours au-dessus des têtes, même au creux de son oscillation et avec sa taille.
                    Assert.True(EnversLook.BobbedHeight(rock, 2.25) >= EnversLook.MinFloatHeight - EnversLook.BobMeters);
                    models.Add(rock.Model);
                    total++;
                }
            }
        Assert.Equal(32 * 24 * 4, total);
        Assert.Equal(new[] { 0, 1, 2 }, models.OrderBy(m => m));
        Assert.Equal(EnversLook.FloatingRocks(region, 13, 4, DeviceTier.Modest).Select(x => x.X), EnversLook.FloatingRocks(region, 13, 4, DeviceTier.Modest).Select(x => x.X));
        Assert.Throws<ArgumentOutOfRangeException>(() => EnversLook.FloatingRocks(region, 32, 0, DeviceTier.Phone));
    }

    [Theory]
    [InlineData(2.0, 3.0, 1.0, 6.0, 4.0, 6.0, false)]   // long côté du modèle en x
    [InlineData(1.0, 3.0, 2.0, 6.0, 4.0, 6.0, true)]    // long côté en z : un quart de tour, puis l'échelle dans les axes du modèle
    public void Pose_d_apres_les_bornes_reelles_echelle_avant_le_lacet(double mx, double my, double mz, double length, double width, double height, bool quarter)
    {
        var fit = EnversLook.FitScale(mx, my, mz, length, width, height);
        Assert.Equal(quarter, fit.QuarterTurn);
        // Dans les axes du modèle : le long côté du modèle donne la longueur, l'autre la largeur, la hauteur la hauteur.
        double along = quarter ? mz * fit.Z : mx * fit.X, across = quarter ? mx * fit.X : mz * fit.Z;
        Assert.Equal(length, along, 9);
        Assert.Equal(width, across, 9);
        Assert.Equal(height, my * fit.Y, 9);
        // Une borne nulle laisse son axe à 1 (pas de division par zéro).
        Assert.Equal(1.0, EnversLook.FitScale(0, 0, 0, 3, 2, 1).Y);
    }

    [Fact]
    public void Bourdon_en_boucle_sans_couture_et_borne()
    {
        float[] drone = EnversLook.Drone(8000);
        Assert.Equal(8000 * EnversLook.DroneSeconds, drone.Length);
        Assert.All(drone, s => Assert.InRange(s, -(float)EnversLook.DronePeak, (float)EnversLook.DronePeak));
        Assert.True(drone.Max() > 0.2f);
        // Chaque fréquence fait un nombre entier de tours dans la boucle : la fin rejoint le début.
        foreach (double f in EnversLook.DroneFrequencies) Assert.Equal(0, f * EnversLook.DroneSeconds % 1, 9);
        Assert.InRange(Math.Abs(drone[^1] - drone[0]), 0, 0.05);
        Assert.Throws<ArgumentOutOfRangeException>(() => EnversLook.Drone(10));
    }

    [Fact]
    public void Dans_l_Envers_musique_du_Seuil_saison_figee_et_temps_montre()
    {
        string folder = Path.Combine(Path.GetTempPath(), "eds_v251b_" + Guid.NewGuid().ToString("N"));
        try
        {
            string save = Path.Combine(folder, "partie", "sauvegarde.json");
            Directory.CreateDirectory(Path.GetDirectoryName(save)!);
            SaveStore.Write(save, new EdS.Data.Saves.WorldSave("zone.envers_vallons", 0, 0, Array.Empty<string>(), V11TestData.Hero()));
            AdventureSession a = V11TestData.Adventure(save);
            Assert.True(a.InEnvers);
            Assert.True(a.Map.Envers);
            Assert.True((a.Mood().Places & AmbiencePlace.Threshold) != 0);
            Assert.Equal(EnversLook.Season, a.SeasonShown);
            Assert.Equal(EnversLook.ShownWeather(a.SurfaceWeather), a.Weather().Kind);
            // La carte de l'Envers garde les routes de la surface (cases en reflet).
            Assert.Equal(AdventureSession.Roads(GrandeCarteLieux.Vallons.Value).Count, a.Map.Whole.Roads.Count);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    /// <summary>
    /// Kit Tripo du 26 novembre 2026 (14 modèles réussis sur 16, 20 crédits chacun) : finis dans Blender (un maillage, allégés, pivot au sol,
    /// textures de 1 024 px), à plat dans <c>Resources/Envers</c> ; l'herbe morte et les racines suspendues (échouées chez Tripo) restent
    /// dessinées par le code ; chaque pièce garde son repli.
    /// </summary>
    [Fact]
    public void Kit_de_l_Envers_present_reconnu_et_repli_garde()
    {
        string folder = Path.Combine(Lot93UnityPackageTests.Root, "unity", EnversLook.AssetFolder);
        Assert.Equal(14, EnversLook.Models.Count);
        Assert.Equal(EnversLook.Models.Count, EnversLook.Models.Distinct().Count());
        foreach (string name in EnversLook.Models)
        {
            string fbx = Path.Combine(folder, name + ".fbx");
            Assert.True(File.Exists(fbx), fbx);
            Assert.True(new FileInfo(fbx).Length > 10_000, fbx);
            Assert.True(File.Exists(Path.Combine(folder, name + "_couleur.jpg")), name);
            Assert.True(File.Exists(Path.Combine(folder, name + "_normale.png")), name);
            Assert.Equal(name, EnversLook.NameOf(EnversLook.AssetFolder + "/" + name + ".fbx"));
        }
        Assert.Equal(EnversLook.Models.Count, Directory.GetFiles(folder, "*.fbx").Length);   // rien d'autre, rien d'oublié
        Assert.EndsWith("Resources/" + EnversLook.Folder.TrimEnd('/'), EnversLook.AssetFolder);
        Assert.Null(EnversLook.NameOf(EnversLook.AssetFolder + "/eclat_noir_couleur.jpg"));
        Assert.Null(EnversLook.NameOf(EnversLook.AssetFolder + "/herbe_morte.fbx"));   // jamais livrée : toujours par le code
        Assert.Equal("eclat_noir", EnversLook.NameOf("Assets\\EdS\\Modeles\\Resources\\Envers\\eclat_noir.fbx"));
        // Toutes les pièces, les formes flottantes et la faille ont un modèle livré ; le semis aussi, sauf l'herbe morte.
        foreach (EnversPiece p in Enum.GetValues<EnversPiece>().Where(p => p != EnversPiece.None)) Assert.Contains(EnversLook.ModelFor(p)!, EnversLook.Models);
        Assert.All(EnversLook.FloatingModels.Append(EnversLook.RiftModel), m => Assert.Contains(m, EnversLook.Models));
        Assert.Equal(new[] { "herbe_morte", "racines_suspendues" }, EnversLook.Procedural);
        Assert.All(EnversLook.Procedural, m => Assert.DoesNotContain(m, EnversLook.Models));
        // La liste de l'atelier, moins les deux fiches échouées, est celle du jeu, dans l'ordre.
        string lab = Path.Combine(Path.GetDirectoryName(Lot93UnityPackageTests.Root)!, "Eclats_du_Seuil_AssetLab", "Envers", "modeles.json");
        if (File.Exists(lab))
        {
            var names = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(lab))!["modeles"]!.AsArray().Select(m => (string)m!["nom"]!).ToList();
            Assert.Equal(EnversLook.Models, names.Where(n => !EnversLook.Procedural.Contains(n)));
        }
        // Repli : formes simples si un modèle manque, herbe morte et racines par le code.
        string code = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "SpaceView.Envers.cs"));
        foreach (string fallback in new[] { "// Formes de remplacement", "Envers flottant (rocher)", "Envers semis (herbe morte)", "Racines suspendues ", "SewerLooks.WallTiles(" })
            Assert.Contains(fallback, code);
    }

    [Fact]
    public void Tuiles_et_pieces_du_palier_2()
    {
        Assert.True(EnversLook.Tiled(EnversPiece.InvertedRuin) && EnversLook.Tiled(EnversPiece.CollapsedHouse));
        Assert.False(EnversLook.Tiled(EnversPiece.DeadTree) || EnversLook.Tiled(EnversPiece.BlackShard));
        Assert.Equal(EnversPiece.InvertedStatue, EnversLook.PieceFor("envers_statue_inversee_1"));
        Assert.Equal(EnversPiece.CollapsedHouse, EnversLook.PieceFor("envers_maison_effondree_2"));
        Assert.Equal(EnversPiece.SplitStone, EnversLook.PieceFor("envers_pierre_levee_fendue_1"));
        Assert.Equal(EnversPiece.VioletBrazier, EnversLook.PieceFor("envers_brasier_violet_1"));
        Assert.True(EnversLook.Glows(EnversPiece.VioletBrazier) && EnversLook.Glows(EnversPiece.DawnAltar));
        // Tuiles sur les bornes réelles : une ruine de 20 m faite d'un modèle de 5,8 m couvre tout l'obstacle, sans trou.
        var tiles = SewerLooks.WallTiles(0, 0, 20, 4, 5.78, 5.75);
        Assert.Equal(0, tiles.Min(t => t.MinX), 6);
        Assert.Equal(20, tiles.Max(t => t.MaxX), 6);
        double covered = tiles.Sum(t => t.MaxX - t.MinX);
        Assert.True(covered >= 20 - 1e-6);
    }

    [Fact]
    public void Semis_au_sol_deterministe_et_selon_la_machine()
    {
        RegionGrid region = RegionGrid.Vallons();
        Assert.Equal((2, 4, 10), (EnversLook.ScatterPerCell(DeviceTier.Phone), EnversLook.ScatterPerCell(DeviceTier.Modest), EnversLook.ScatterPerCell(DeviceTier.Full)));
        var kinds = new HashSet<int>();
        for (int c = 0; c < region.Columns; c += 3)
            for (int r = 0; r < region.Rows; r += 3)
            {
                var full = EnversLook.Scatter(region, c, r, DeviceTier.Full);
                var modest = EnversLook.Scatter(region, c, r, DeviceTier.Modest);
                Assert.Equal(modest, full.Take(modest.Count));
                Assert.Equal(full, EnversLook.Scatter(region, c, r, DeviceTier.Full));
                double x0 = region.OriginX + c * RegionGrid.CellMeters, z0 = region.OriginZ + r * RegionGrid.CellMeters;
                foreach (var s in full)
                {
                    Assert.InRange(s.X, x0 + 4, x0 + RegionGrid.CellMeters - 4);
                    Assert.InRange(s.Z, z0 + 4, z0 + RegionGrid.CellMeters - 4);
                    Assert.InRange(s.Scale, 0.7, 1.3);
                    kinds.Add(s.Kind);
                }
            }
        Assert.Equal(new[] { 0, 1, 2 }, kinds.OrderBy(k => k));
        Assert.Equal(new[] { "souche_cendre", "champignons_lumineux", "herbe_morte" }, EnversLook.ScatterModels);
    }

    // --- Relecture indépendante des PR #260 et #261 ------------------------------------------------------------------------

    /// <summary>
    /// Performances (le PC de Damien saccade déjà) : formes flottantes et semis construits seulement par case autour de la caméra, plus sur
    /// toute l'aire (768 cases) ; au plus deux cases par image ; défaites au-delà du rayon plus 64 m.
    /// </summary>
    [Fact]
    public void Cases_construites_seulement_autour_de_la_camera()
    {
        RegionGrid region = RegionGrid.Vallons();
        Assert.Equal((128.0, 192.0, 320.0), (EnversLook.StreamRadius(DeviceTier.Phone), EnversLook.StreamRadius(DeviceTier.Modest), EnversLook.StreamRadius(DeviceTier.Full)));
        Assert.Equal(2, EnversLook.StreamCellsPerFrame);
        foreach (DeviceTier tier in new[] { DeviceTier.Phone, DeviceTier.Modest, DeviceTier.Full })
        {
            var near = EnversLook.CellsNear(region, 1640, -455, EnversLook.StreamRadius(tier));
            Assert.InRange(near.Count, 1, 40);
            Assert.All(near, c => Assert.True(EnversLook.CellDistance(region, c.Column, c.Row, 1640, -455) <= EnversLook.StreamRadius(tier)));
            // La plus proche d'abord : la case de la caméra.
            Assert.Equal(region.CellOf(new EdS.Domain.Space.GroundVector(EdS.Domain.Numerics.Fixed.FromInt(1640), EdS.Domain.Numerics.Fixed.FromInt(-455))),
                new RegionCell(near[0].Column, near[0].Row));
            var d = near.Select(c => EnversLook.CellDistance(region, c.Column, c.Row, 1640, -455)).ToList();
            Assert.Equal(d.OrderBy(x => x), d);
            if (tier == DeviceTier.Modest)
            {
                // PC modeste : au plus une vingtaine de cases, donc au plus 40 formes flottantes et 80 semis (contre 1 536 et 3 072 sur l'aire).
                Assert.InRange(near.Count, 9, 20);
                Assert.True(near.Count * EnversLook.FloatingPerCell(tier) <= 40 && near.Count * EnversLook.ScatterPerCell(tier) <= 80);
            }
        }
        // Bornée à la grille (coin de la région).
        Assert.All(EnversLook.CellsNear(region, -1908, -1614, 300), c => Assert.True(c.Column >= 0 && c.Row >= 0));
        Assert.Empty(EnversLook.CellsNear(region, -9000, -9000, 100));
        // Hystérésis : une case juste sortie du rayon reste, défaite au-delà de 64 m de plus.
        Assert.True(EnversLook.CellKept(region, 0, 0, -1908 - 200, -1614, 192));
        Assert.False(EnversLook.CellKept(region, 0, 0, -1908 - 257, -1614, 192));
        Assert.Throws<ArgumentOutOfRangeException>(() => EnversLook.CellsNear(region, 0, 0, -1));
        // Le décor les construit au fil des images, plus d'un bloc au chargement.
        string code = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "SpaceView.Envers.cs"));
        Assert.Contains("EnversLook.CellsNear(_enversRegion, camera.x, camera.z, radius)", code);
        Assert.Contains("EnversLook.CellKept(", code);
        Assert.DoesNotContain("for (int c = c0; c <= c1; c++)", code);
    }

    /// <summary>L'autel de l'Aube, les brasiers et les failles (modèle, enfants compris, et lame « EdS/Faille ») gardent leur clarté.</summary>
    [Fact]
    public void Autel_et_failles_jamais_assombris()
    {
        string code = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "SpaceView.Envers.cs"));
        Assert.Contains("KeepBrightFrom(first);   // la faille garde sa lumière", code);
        Assert.Contains("if (drawn && EnversLook.Glows(EnversLook.PieceFor(o.Id))) KeepBrightFrom(first);", code);
        Assert.Contains("if (KeptBright(renderer.transform)) continue;", code);
        Assert.Contains("for (Transform at = t; at != null && at != _root.transform; at = at.parent)", code);   // en remontant les parents
        Assert.DoesNotContain("StartsWith(\"envers_autel\"", code);
        Assert.True(EnversLook.Glows(EnversPiece.DawnAltar) && EnversLook.Glows(EnversPiece.VioletBrazier));
        Assert.False(EnversLook.Glows(EnversPiece.DeadTree));
    }
}
