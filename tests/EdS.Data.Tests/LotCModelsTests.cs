using System.Text.Json;
using EdS.Data.Items;
using EdS.Data.Zones;
using EdS.Data.Json;
using EdS.Runtime;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Lot C (16 novembre 2026) : modèles Tripo intégrés (« unity/Assets/EdS/Modeles/LotC ») et leur lien avec les vraies données : chaque
/// fichier est dans la table <see cref="LotCModels"/> et inversement ; chaque cible existe (objet du catalogue, nœud de récolte, meuble d'un
/// intérieur) ; un modèle sans cible dit pourquoi ; textures dans le budget (couleur 1024 au plus, 512 pour un petit objet ; normale 512).
/// </summary>
[Trait("Category", "World")]
public class LotCModelsTests
{
    private static string Folder => Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Modeles", "LotC");

    private static JsonElement Data(string file) => JsonDocument.Parse(File.ReadAllText(Path.Combine(DataDirectory, "world", file))).RootElement.GetProperty("data");

    [Fact]
    public void Chaque_fichier_est_dans_la_table_et_chaque_modele_a_ses_fichiers()
    {
        string[] fbx = Directory.GetFiles(Folder, "*.fbx", SearchOption.AllDirectories);
        foreach (string f in fbx)
            Assert.True(LotCModels.CodeOf(Path.GetRelativePath(Lot93UnityPackageTests.Root, f)) != null, "hors table : " + f);
        Assert.Equal(LotCModels.All.Count, fbx.Length);
        foreach (LotCModel m in LotCModels.All)
        {
            string dir = Path.Combine(Folder, m.Category, m.Code);
            Assert.True(File.Exists(Path.Combine(dir, m.Code + ".fbx")), m.Code);
            Assert.True(File.Exists(Path.Combine(dir, m.Code + "_couleur.jpg")), m.Code);
            Assert.Equal(m.Code == "C_IN2_M0_04" ? 2 : 3, Directory.GetFiles(dir).Length);   // le mannequin fait main n'a pas de normale
        }
    }

    [Fact]
    public void Textures_dans_le_budget()
    {
        foreach (LotCModel m in LotCModels.All)
        {
            string dir = Path.Combine(Folder, m.Category, m.Code);
            (int w, int h) = JpegSize(Path.Combine(dir, m.Code + "_couleur.jpg"));
            Assert.True(w == h && w <= 1024, m.Code + " : " + w + "×" + h);
            string normal = Path.Combine(dir, m.Code + "_normale.png");
            if (!File.Exists(normal)) continue;
            byte[] png = File.ReadAllBytes(normal);
            int pw = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
            Assert.True(pw <= 512, m.Code + " : normale " + pw);
        }
    }

    [Fact]
    public void Chaque_cible_existe_dans_les_vraies_donnees()
    {
        var items = Data("items.json").GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()).ToHashSet();
        var nodes = Data("crafting.json").GetProperty("nodes").EnumerateArray().Select(n => n.GetProperty("id").GetString()).ToHashSet();
        var missing = new List<string>();
        foreach (LotCModel m in LotCModels.All)
        {
            Assert.True(m.Linked != (m.Pending != null), m.Code + " : relié ou en attente, pas les deux");
            foreach (string t in m.Targets)
            {
                int colon = t.IndexOf(':');
                string kind = t.Substring(0, colon), id = t.Substring(colon + 1);
                switch (kind)
                {
                    case "item": Assert.True(items.Contains(id), m.Code + " : objet inconnu " + id); break;
                    case "node": Assert.True(nodes.Contains(id), m.Code + " : nœud inconnu " + id); break;
                    case "room":
                    {
                        string[] rp = id.Split('/');
                        Assert.True(RoomObstacles(rp[0]).Contains(rp[1]), m.Code + " : meuble inconnu " + id);
                        Assert.Equal(m.Code, LotCModels.ForRoom(rp[0], rp[1]));
                        continue;
                    }
                    case "piece":
                    {
                        string[] pp = id.Split('/');
                        Assert.True(Enum.TryParse(pp[1], out InteriorProp prop) && prop.ToString() == pp[1], m.Code + " : sorte inconnue " + id);
                        if (!Layout(pp[0]).Pieces.Any(x => x.Prop == prop)) missing.Add(m.Code + " : l'aménagement de " + pp[0] + " ne pose pas de " + pp[1]);
                        Assert.Equal(m.Code, LotCModels.ForPiece(pp[0], prop));
                        continue;
                    }
                    case "village": Assert.True(VillageKnown(id), m.Code + " : décor du village inconnu " + id); continue;
                    case "obstacle":
                        string[] hits = VallonsObstacles.Where(o => id.EndsWith("*") ? o.StartsWith(id.TrimEnd('*')) : o == id).ToArray();
                        Assert.True(hits.Length > 0, m.Code + " : obstacle inconnu " + id);
                        foreach (string o in hits) Assert.Equal(m.Code, LotCModels.ForObstacle(o));
                        continue;
                    default: Assert.Fail(m.Code + " : cible inconnue " + t); break;
                }
                Assert.Equal(m.Code, kind == "item" ? LotCModels.ForItem(id) : LotCModels.ForNode(id));
            }
        }
        Assert.True(missing.Count == 0, string.Join("\n", missing));
    }

    [Fact]
    public void Recoltes_et_butins_relies()
    {
        Assert.Equal("C_RE_S01", LotCModels.ForNode("primevere_ruisseau"));
        Assert.Equal("C_RE_F01", LotCModels.ForNode("herbes_lac"));
        Assert.Equal("C_RE_MI_D03", LotCModels.ForNode("filon_est"));
        Assert.Equal("C_BU_GS_A08", LotCModels.ForItem("item.trophee_des_trois_bornes"));
        Assert.Null(LotCModels.ForNode("champignons_pre"));   // chapeau de lune : pas de modèle du lot C
        // Tous les filons de fer et toutes les herbes de l'Aube ont leur modèle.
        foreach (JsonElement n in Data("crafting.json").GetProperty("nodes").EnumerateArray())
        {
            string item = n.GetProperty("item").GetString()!;
            if (item is "item.minerai_de_fer" or "item.herbe_de_l_aube") Assert.NotNull(LotCModels.ForNode(n.GetProperty("id").GetString()));
        }
        Assert.Equal(1.0, LotCModels.NodeScale("C_RE_MI_D01"));
        Assert.Equal(2.0, LotCModels.NodeScale("C_RE_S01"));
        Assert.True(LotCModels.Find("C_EQ051")!.HandHeld);
        Assert.True(LotCModels.Find("C_BU_L03_02")!.HandHeld);
        Assert.False(LotCModels.Find("C_BU_L03_03")!.HandHeld);
        // Relecture de la PR #196 : index construit une fois, mêmes réponses que la table.
        foreach (LotCModel m in LotCModels.All) Assert.Same(m, LotCModels.Find(m.Code));
        Assert.Null(LotCModels.Find(null));
        Assert.Null(LotCModels.Find("C_INCONNU"));
        Assert.False(LotCModels.Find("C_EQ051")!.Linked);   // en attente : pas chargé au catalogue (EdsStyle.LotCPieces)
    }

    private static string Game => Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game");

    private static string[] VallonsObstacles =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "scenarios", "zones", "vallons.json"))).RootElement
            .GetProperty("data").GetProperty("space").GetProperty("obstacles").EnumerateArray().Select(o => o.GetProperty("id").GetString()!).ToArray();

    /// <summary>Un décor du village posé par le jeu : rôle de la 5e vague ou accessoire du catalogue réellement posé par la vue, ou place fixe connue.</summary>
    private static bool VillageKnown(string key)
    {
        string markers = File.ReadAllText(Path.Combine(Game, "WorldMarkers.cs"));
        string[] p = key.Split('/');
        return p[0] switch
        {
            "wave5" => Enum.TryParse(p[1], out Wave5Role r) && r.ToString() == p[1] && markers.Contains("Wave5Role." + p[1] + ","),
            "prop" => Enum.TryParse(p[1], out StyleProp sp) && sp.ToString() == p[1] && markers.Contains("StyleProp." + p[1] + ","),
            // Lot C (décision du 16 novembre 2026) : les pièces autour de la forge sont posées par la table LotCModels.ForgeYard, les outils
            // par LotCModels.ForgeToolCodes.
            _ => LotCModels.VillageSpots.Contains(key) && (markers.Contains("\"" + key + "\"")
                || (LotCModels.ForgeYard.Any(p => p.Key == key) && markers.Contains("LotCModels.ForgeYard"))
                || (key == LotCModels.ForgeTools && markers.Contains("LotCModels.ForgeToolCodes"))
                || (key == LotCModels.ForgeAwning.Key && markers.Contains("LotCModels.ForgeAwning"))),
        };
    }

    /// <summary>Zone d'intérieur dont le premier lieu est <paramref name="room"/> (« auberge »).</summary>
    private static string ZoneOfRoom(string room)
    {
        foreach (string f in Directory.GetFiles(Path.Combine(DataDirectory, "world", "zones"), "*.json"))
        {
            JsonElement d = JsonDocument.Parse(File.ReadAllText(f)).RootElement.GetProperty("data");
            if (d.TryGetProperty("indoor", out JsonElement indoor) && indoor.GetBoolean() && d.GetProperty("places")[0].GetProperty("id").GetString() == room)
                return d.GetProperty("id").GetString()!;
        }
        throw new InvalidOperationException("aucun intérieur pour le lieu " + room);
    }

    private static AdventureMap Map(string room)
    {
        string zone = ZoneOfRoom(room);
        JsonElement d = JsonDocument.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, zone))).RootElement.GetProperty("data");
        JsonElement safe = d.GetProperty("safePoints")[0].GetProperty("position");
        return InterieursTests.MapOf(V231TestData.Interior(zone, safe[0].GetDouble(), safe[1].GetDouble()));
    }

    private static InteriorLayout Layout(string room) => InteriorRules.Layout(Map(room));

    private static string[] RoomObstacles(string room) => Map(room).Obstacles.Select(o => o.Id).ToArray();

    [Fact]
    public void Meubles_poses_dans_leur_emprise_sans_deborder()
    {
        // Empreintes types des meubles du lot C (table, comptoir, tonneaux, rayonnage, âtre, coffre, caisses).
        (double W, double D)[] models = { (0.6, 1.8), (0.55, 1.8), (0.96, 1.2), (0.79, 3.0), (1.6, 2.12), (0.75, 1.4), (0.65, 2.0), (2.4, 0.85), (0.3, 0.3), (1.0, 1.02), (1.02, 1.0) };
        int rooms = 0;
        foreach (string room in new[] { "auberge", "boulangerie", "guilde", "archives", "chapelle", "epicerie", "herboristerie", "mercerie", "maison",
            "forge_couverte", "grange", "salle_du_moulin", "etage_des_meules", "salle_de_garde", "maison_paysanne", "cuisine_auberge", "chambres_auberge", "logement_auberge", "combles_auberge" })
        {
            AdventureMap map = Map(room);
            rooms++;
            foreach (var o in map.Obstacles)
            {
                double x0 = o.Points.Min(p => p.X.Raw) / 10000.0, x1 = o.Points.Max(p => p.X.Raw) / 10000.0;
                double z0 = o.Points.Min(p => p.Z.Raw) / 10000.0, z1 = o.Points.Max(p => p.Z.Raw) / 10000.0;
                foreach ((double w, double d) in models)
                {
                    IReadOnlyList<LotCCopy> copies = LotCModels.Fit(x0, z0, x1, z1, map.MinX, map.MinZ, map.MaxX, map.MaxZ, w, d);
                    Assert.NotEmpty(copies);
                    double used = 0;
                    foreach (LotCCopy k in copies)
                    {
                        Assert.True(k.Scale > 0 && k.Scale <= LotCModels.MaxScale + 1e-9, room + "/" + o.Id);
                        Assert.Contains(k.Yaw, new[] { 0.0, 90, 180, 270 });
                        bool turned = k.Yaw == 90 || k.Yaw == 270;
                        double hx = (turned ? d * k.ThinZ : w * k.ThinX) * k.Scale / 2, hz = (turned ? w * k.ThinX : d * k.ThinZ) * k.Scale / 2;
                        Assert.True(k.X - hx >= x0 - 1e-6 && k.X + hx <= x1 + 1e-6 && k.Z - hz >= z0 - 1e-6 && k.Z + hz <= z1 + 1e-6,
                            $"{room}/{o.Id} : copie hors de l'emprise ({w}×{d})");
                        used += 2 * (x1 - x0 >= z1 - z0 ? hx : hz);
                    }
                    Assert.True(used <= Math.Max(x1 - x0, z1 - z0) + 1e-6, room + "/" + o.Id + " : copies qui se chevauchent");
                }
            }
        }
        Assert.Equal(19, rooms);   // lot C (16 novembre 2026) : et les huit intérieurs de métier
        // Dos au mur : le comptoir de l'auberge regarde l'intérieur, sur une seule rangée de copies le long du grand côté.
        Assert.Equal(180, LotCModels.Fit(-1, 4, 1, 4.5, -7, -5, 7, 5, 1.8, 0.55)[0].Yaw);
        Assert.Equal(0, LotCModels.Fit(-1, -4.5, 1, -4, -7, -5, 7, 5, 1.8, 0.55)[0].Yaw);
        Assert.Empty(LotCModels.Fit(0, 0, 1, 1, -7, -5, 7, 5, 0, 1));
    }

    [Fact]
    public void Modele_presque_carre_aminci_sur_le_petit_cote_de_l_emprise()
    {
        // Relecture de la PR #200 : un modèle presque carré (1,00 × 1,02 m) reste sans quart de tour ; l'amincissement doit porter sur l'axe
        // du modèle posé le long du petit côté de l'emprise, pas sur son propre petit côté.
        // Emprise longue en x contre le mur sud (pas tournée) : petit côté selon z du monde, donc z du modèle.
        LotCCopy a = LotCModels.Fit(4, 0, 6, 0.5, 0, 0, 10, 10, 1.0, 1.02)[0];
        Assert.Equal(0, a.Yaw);
        Assert.Equal(1.0, a.ThinX);
        Assert.True(a.ThinZ < 1);
        Assert.True(1.02 * a.Scale * a.ThinZ <= 0.5 + 1e-9);
        // Emprise longue en z contre le mur ouest (tournée d'un quart) : petit côté selon x du monde, donc z du modèle.
        LotCCopy b = LotCModels.Fit(0, 4, 0.5, 6, 0, 0, 10, 10, 1.02, 1.0)[0];
        Assert.Equal(90, b.Yaw);
        Assert.Equal(1.0, b.ThinX);
        Assert.True(1.0 * b.Scale * b.ThinZ <= 0.5 + 1e-9);
    }

    [Fact]
    public void Le_modele_remplace_le_corps_du_meuble_et_garde_sieges_et_vaisselle()
    {
        InteriorLayout layout = Layout("auberge");
        InteriorPiece[] table = layout.Pieces.Where(p => p.Id == "table1" || p.Id.StartsWith("table1/")).ToArray();
        Assert.Contains(table, p => LotCModels.Replaces("table1", p));
        Assert.All(table.Where(p => p.Prop is InteriorProp.Bench or InteriorProp.Stool), p => Assert.False(LotCModels.Replaces("table1", p)));
        Assert.All(table.Where(p => p.Parent >= 0), p => Assert.False(LotCModels.Replaces("table1", p)));
        Assert.DoesNotContain(layout.Pieces, p => LotCModels.Replaces("table", p) && !p.Id.StartsWith("table/") && p.Id != "table");   // pas « table1 » pour « table »
        Assert.Equal("C_IN1_A0_02", LotCModels.ForRoom("auberge", "table3"));
        Assert.Equal("C_IN1_P17", LotCModels.ForPiece("archives", InteriorProp.Candlestick));
        Assert.Null(LotCModels.ForPiece("auberge", InteriorProp.None));
        Assert.Null(LotCModels.ForRoom("boulangerie", "four"));   // le four voûté fait par le code reste
    }

    [Fact]
    public void Decors_du_village_relies()
    {
        Assert.Equal("C_OV_A03", LotCModels.ForVillage("prop/FirePit", "forge/foyer"));
        Assert.Equal("C_OV_A02", LotCModels.ForVillage("forge/enclume", "forge"));
        Assert.Equal("C_OV_C14", LotCModels.ForObstacle("panneau_hameau"));
        Assert.Null(LotCModels.ForObstacle("puits"));
        Assert.Null(LotCModels.ForVillage("wave5/Bunting", "guirlande"));   // pas de mâts : la guirlande des packs reste
        Assert.Null(LotCModels.ForVillage(null, "x"));
        // Trois sacs de farine : tirés de l'identifiant, toujours le même pour le même identifiant.
        var flour = new[] { "farine 1", "farine 2", "farine 3", "farine 4", "farine 5" }.Select(i => LotCModels.ForVillage("wave5/FlourSack", i)).ToList();
        Assert.All(flour, c => Assert.StartsWith("C_OV_D02_0", c));
        Assert.Equal(flour[0], LotCModels.ForVillage("wave5/FlourSack", "farine 1"));
        // Relecture de la PR #197 : le rang demandé (variant de Wave5View.Place) choisit parmi les trois, modulo, même négatif.
        Assert.Equal("C_OV_D02_01", LotCModels.ForVillage("wave5/FlourSack", "x", 0));
        Assert.Equal("C_OV_D02_02", LotCModels.ForVillage("wave5/FlourSack", "x", 4));
        Assert.Equal("C_OV_D02_03", LotCModels.ForVillage("wave5/FlourSack", "x", -1));
        Assert.Equal("C_OV_A02", LotCModels.ForVillage("forge/enclume", "x", 7));
    }

    /// <summary>Emprise au sol (x, z en mètres) des modèles reliés aux intérieurs de métier, d'après leurs exports (manifeste de l'atelier).</summary>
    private static readonly Dictionary<string, (double W, double D)> TradeSizes = new()
    {
        ["C_IN1_A0_11"] = (0.904, 1.284),
        ["C_IN1_A0_12"] = (0.613, 1.001),
        ["C_IN1_A0_13"] = (0.599, 2.292),
        ["C_IN1_A1_01"] = (2.003, 1.28),
        ["C_IN1_A1_02"] = (0.443, 0.7),
        ["C_IN1_A1_04"] = (0.478, 0.8),
        ["C_IN1_A1_06"] = (0.551, 0.7),
        ["C_IN1_A1_07"] = (1.797, 1.141),
        ["C_IN1_A2_01"] = (2.001, 1.499),
        ["C_IN1_A2_02"] = (0.479, 0.9),
        ["C_IN1_A2_03"] = (0.766, 1.401),
        ["C_IN1_A2_04"] = (0.446, 0.459),
        ["C_IN1_A2_05"] = (1.901, 1.343),
        ["C_IN1_A2_10"] = (0.877, 1.05),
        ["C_IN1_A3_01"] = (0.485, 1.997),
        ["C_IN1_A3_02"] = (0.8, 1.201),
        ["C_IN1_A3_03"] = (0.438, 2.008),
        ["C_IN1_A3_04"] = (0.651, 1.994),
        ["C_IN2_F0_01"] = (1.269, 1.775),
        ["C_IN2_F0_02"] = (0.498, 1.304),
        ["C_IN2_F0_03"] = (0.519, 0.719),
        ["C_IN2_F0_04"] = (0.906, 1.8),
        ["C_IN2_F0_05"] = (0.651, 0.643),
        ["C_IN2_F0_06"] = (0.436, 1.256),
        ["C_IN2_G0_01"] = (3.797, 2.017),
        ["C_IN2_G0_02"] = (3.577, 2.205),
        ["C_IN2_G0_03"] = (0.868, 1.722),
        ["C_IN2_G0_04"] = (0.788, 1.401),
        ["C_IN2_G0_05"] = (0.573, 2.701),
        ["C_IN2_O0_01"] = (1.701, 1.489),
        ["C_IN2_O0_02"] = (0.691, 0.9),
        ["C_IN2_O0_03"] = (0.855, 1.1),
        ["C_IN2_O0_04"] = (1.051, 0.927),
        ["C_IN2_O0_05"] = (0.505, 0.638),
        ["C_IN2_O1_01"] = (1.55, 1.482),
        ["C_IN2_O1_04"] = (0.792, 1.0),
        ["C_IN2_T0_01"] = (1.291, 1.966),
        ["C_IN2_T0_02"] = (0.557, 1.199),
        ["C_IN2_T0_03"] = (0.378, 0.912),
        ["C_IN2_T0_05"] = (0.73, 1.001),
        ["C_IN2_V0_01"] = (1.8, 1.02),
        ["C_IN2_V0_02"] = (0.933, 1.181),
        ["C_IN2_V0_03"] = (0.606, 1.1),
        ["C_IN2_V0_04"] = (0.493, 0.897),
        ["C_IN2_V0_05"] = (0.344, 1.034),
        ["C_IN2_V0_06"] = (1.067, 0.629),
    };

    [Fact]
    public void Interieurs_de_metier_meubles_a_la_mesure_de_leurs_modeles()
    {
        // Décisions de Damien du 16 novembre 2026 : chaque meuble des huit intérieurs de métier a son modèle du lot C, posé une seule fois
        // dans son emprise cotée, presque à sa taille (échelle de 0,75 à 1,15, à peine aminci) ; portes à l'écart des meubles.
        string[] rooms = { "forge_couverte", "grange", "salle_du_moulin", "etage_des_meules", "salle_de_garde", "maison_paysanne", "cuisine_auberge", "chambres_auberge", "logement_auberge", "combles_auberge" };
        int linked = 0;
        foreach (string room in rooms)
        {
            AdventureMap map = Map(room);
            foreach (var o in map.Obstacles)
            {
                string? code = LotCModels.ForRoom(room, o.Id);
                if (code == null)
                {
                    Assert.True(o.Id == "escalier", room + "/" + o.Id + " sans modèle");   // escaliers de la tour et de l'auberge : ceux du code
                    continue;
                }
                linked++;
                (double w, double d) = TradeSizes[code];
                double x0 = o.Points.Min(p => p.X.Raw) / 10000.0, x1 = o.Points.Max(p => p.X.Raw) / 10000.0;
                double z0 = o.Points.Min(p => p.Z.Raw) / 10000.0, z1 = o.Points.Max(p => p.Z.Raw) / 10000.0;
                LotCCopy k = Assert.Single(LotCModels.Fit(x0, z0, x1, z1, map.MinX, map.MinZ, map.MaxX, map.MaxZ, w, d));
                // 17 novembre 2026 : emprises cotées des fiches ; un modèle exporté plus grand que sa fiche est réduit (jusqu'aux trois quarts).
                Assert.True(k.Scale >= 0.75 && k.Scale <= LotCModels.MaxScale + 1e-9, room + "/" + o.Id + " : échelle " + k.Scale);
                Assert.True(Math.Min(k.ThinX, k.ThinZ) >= 0.85, room + "/" + o.Id + " trop aminci");
            }
            foreach (MapDoor door in map.Doors)
                foreach (var o in map.Obstacles)
                {
                    double dx = Math.Max(0, Math.Max(o.Points.Min(p => p.X.Raw) / 10000.0 - door.X, door.X - o.Points.Max(p => p.X.Raw) / 10000.0));
                    double dz = Math.Max(0, Math.Max(o.Points.Min(p => p.Z.Raw) / 10000.0 - door.Z, door.Z - o.Points.Max(p => p.Z.Raw) / 10000.0));
                    Assert.True(Math.Sqrt(dx * dx + dz * dz) >= 0.55, room + " : porte " + door.Id + " contre " + o.Id);
                }
        }
        Assert.Equal(TradeSizes.Count, TradeSizes.Keys.Count(c => LotCModels.Find(c)!.Linked));
        Assert.True(linked >= 50, linked.ToString());
        // Plus rien en attente pour ces lieux (forge, grange, moulin, tour, maison paysanne, cuisine et chambres de l'auberge).
        Assert.DoesNotContain(LotCModels.All, m => m.Pending != null && (m.Code.StartsWith("C_IN2_F0", StringComparison.Ordinal)
            || m.Code.StartsWith("C_IN2_G0", StringComparison.Ordinal) || m.Code.StartsWith("C_IN2_O", StringComparison.Ordinal)
            || m.Code.StartsWith("C_IN2_T0", StringComparison.Ordinal) || m.Code.StartsWith("C_IN2_V0", StringComparison.Ordinal)
            || m.Code.StartsWith("C_IN1_A1", StringComparison.Ordinal) || m.Code.StartsWith("C_IN1_A2", StringComparison.Ordinal)
            || m.Code.StartsWith("C_IN1_A3", StringComparison.Ordinal) || m.Code is "C_IN1_A0_11" or "C_IN1_A0_12"));
    }

    [Fact]
    public void Forge_de_Maelle_toutes_les_pieces_sans_bloquer_le_passage()
    {
        // Décision de Damien du 16 novembre 2026 : toutes les pièces de forge du lot C autour de la forge en plein air de Maëlle.
        string[] forgeCodes = { "C_OV_A04", "C_OV_A05", "C_OV_A06_01", "C_OV_A06_03", "C_OV_A06_04", "C_OV_A06_05", "C_OV_A06_06", "C_OV_A06_07",
            "C_OV_A06_08", "C_OV_A07", "C_OV_A08", "C_OV_A09", "C_OV_A10", "C_OV_A11" };
        Assert.All(forgeCodes, c => Assert.True(LotCModels.Find(c)!.Linked, c));
        Assert.Equal(7, LotCModels.ForgeToolCodes.Count);
        Assert.Equal(7, LotCModels.ForgeYard.Count);
        Assert.Equal("C_OV_A06_02", LotCModels.ForVillage("prop/Hammer", "forge/marteau"));   // le marteau moyen reste sur l'enclume
        // Centre du lieu « forge » (le marqueur de l'atelier) ; Maëlle et son apprentie à la forge (emplois du temps).
        JsonElement vallons = JsonDocument.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"))).RootElement.GetProperty("data");
        JsonElement place = vallons.GetProperty("places").EnumerateArray().Single(p => p.GetProperty("id").GetString() == "forge");
        double px0 = place.GetProperty("min")[0].GetDouble(), pz0 = place.GetProperty("min")[1].GetDouble();
        double px1 = place.GetProperty("max")[0].GetDouble(), pz1 = place.GetProperty("max")[1].GetDouble();
        double cx = (px0 + px1) / 2, cz = (pz0 + pz1) / 2;
        JsonElement story = Data("story.json");
        var smiths = story.GetProperty("npcs").EnumerateArray().Where(n => n.GetProperty("id").GetString() is "npc.maelle" or "npc.apprentie")
            .SelectMany(n => n.GetProperty("schedule").EnumerateArray()).Where(s => !s.TryGetProperty("zone", out _)
                && s.GetProperty("position")[0].GetDouble() is var x && x > px0 && x < px1 && s.GetProperty("position")[1].GetDouble() is var z && z > pz0 && z < pz1)
            .Select(s => (s.GetProperty("position")[0].GetDouble(), s.GetProperty("position")[1].GetDouble())).Distinct().ToList();
        Assert.Equal(2, smiths.Count);
        // Emprises : celles déjà posées par WorldMarkers.Forge (enclume, foyer, tonneau, caisse), puis la table.
        var boxes = new List<(string Id, double X0, double Z0, double X1, double Z1)>();
        void Add(string id, string code, double x, double z, double yaw)
        {
            (double w, double d) = LotCModels.ForgeYardSize(code);
            Assert.True(w > 0 && d > 0, code);
            if (yaw == 90 || yaw == 270) (w, d) = (d, w);
            boxes.Add((id, x - w / 2, z - d / 2, x + w / 2, z + d / 2));
        }
        double ax = cx + 0.2, az = cz + 1.6;   // l'enclume, un pas derrière Maëlle
        Add("enclume", "C_OV_A02", ax, az, 0);
        Add("foyer", "C_OV_A03", ax + 1.9, az - 0.2, 0);
        Add("tonneau", "C_OV_C04", ax - 1.5, az + 0.3, 0);
        boxes.Add(("caisse", ax - 1.7 - 0.32, az - 1.6 - 0.32, ax - 1.7 + 0.32, az - 1.6 + 0.32));   // tournée de 75° : son cercle
        foreach (LotCYardPiece p in LotCModels.ForgeYard)
            Add(p.Key, LotCModels.ForVillage(p.Key, "forge")!, cx + p.Dx, cz + p.Dz, p.Yaw);
        // Rien ne se chevauche (10 cm de jeu) ; rien à moins de 60 cm de Maëlle ou de son apprentie ; tout dans le lieu à 0,8 m près.
        for (int i = 0; i < boxes.Count; i++)
        {
            var a = boxes[i];
            Assert.True(a.X0 >= px0 - 0.8 && a.X1 <= px1 + 0.8 && a.Z0 >= pz0 - 0.8 && a.Z1 <= pz1 + 0.8, a.Id + " loin de la forge");
            for (int j = i + 1; j < boxes.Count; j++)
            {
                var b = boxes[j];
                Assert.False(a.X0 < b.X1 + 0.1 && b.X0 < a.X1 + 0.1 && a.Z0 < b.Z1 + 0.1 && b.Z0 < a.Z1 + 0.1, a.Id + " chevauche " + b.Id);
            }
            foreach ((double sx, double sz) in smiths)
            {
                double dx = Math.Max(0, Math.Max(a.X0 - sx, sx - a.X1)), dz = Math.Max(0, Math.Max(a.Z0 - sz, sz - a.Z1));
                Assert.True(Math.Sqrt(dx * dx + dz * dz) >= 0.6, a.Id + " contre un forgeron");
            }
        }
        // Un passage de 1,2 m du bord sud du lieu jusqu'à Maëlle reste libre.
        (double mx, double mz) = smiths.OrderBy(s => Math.Abs(s.Item1 - cx) + Math.Abs(s.Item2 - cz)).First();
        Assert.DoesNotContain(boxes, b => b.X0 < mx + 0.6 && b.X1 > mx - 0.6 && b.Z0 < mz && b.Z1 > pz0 - 0.7);
        // Décision du 17 novembre 2026 : l'auvent (C_OV_A01) couvre Maëlle ; ses quatre poteaux (30 cm aux coins) ne touchent aucune pièce (10 cm
        // de jeu), ni le passage, ni un forgeron (60 cm) ; rien de plus haut que l'égout sous le toit (le foyer et sa hotte restent au nord).
        Assert.Equal("C_OV_A01", LotCModels.ForVillage(LotCModels.ForgeAwning.Key, "forge"));
        (double aw, double ad) = LotCModels.ForgeYardSize("C_OV_A01");
        if (LotCModels.ForgeAwning.Yaw is 90 or 270) (aw, ad) = (ad, aw);
        double rx0 = cx + LotCModels.ForgeAwning.Dx - aw / 2, rx1 = rx0 + aw, rz0 = cz + LotCModels.ForgeAwning.Dz - ad / 2, rz1 = rz0 + ad;
        Assert.True(mx > rx0 && mx < rx1 && mz > rz0 && mz < rz1, "Maëlle hors de l'auvent");
        var heights = new Dictionary<string, double> { ["foyer"] = 4.405, ["forge/ratelier"] = 2.562 };
        foreach (var b in boxes.Where(b => heights.TryGetValue(b.Id, out double h) && h >= LotCModels.ForgeAwningEave))
            Assert.False(b.X0 < rx1 && rx0 < b.X1 && b.Z0 < rz1 && rz0 < b.Z1, b.Id + " plus haut que l'égout, sous l'auvent");
        var posts = new[] { (rx0, rz0), (rx1 - 0.3, rz0), (rx0, rz1 - 0.3), (rx1 - 0.3, rz1 - 0.3) }
            .Select((p, i) => ("forge/auvent/poteau" + i, p.Item1, p.Item2, p.Item1 + 0.3, p.Item2 + 0.3)).ToList();
        foreach (var p in posts)
        {
            Assert.DoesNotContain(boxes, b => p.Item2 < b.X1 + 0.1 && b.X0 < p.Item4 + 0.1 && p.Item3 < b.Z1 + 0.1 && b.Z0 < p.Item5 + 0.1);
            Assert.False(p.Item2 < mx + 0.6 && p.Item4 > mx - 0.6 && p.Item3 < mz && p.Item5 > pz0 - 0.8, p.Item1 + " dans le passage");
            foreach ((double sx, double sz) in smiths)
            {
                double dx = Math.Max(0, Math.Max(p.Item2 - sx, sx - p.Item4)), dz = Math.Max(0, Math.Max(p.Item3 - sz, sz - p.Item5));
                Assert.True(Math.Sqrt(dx * dx + dz * dz) >= 0.6, p.Item1 + " contre un forgeron");
            }
        }
        boxes.AddRange(posts);
        // Ni sur un obstacle des Vallons ni sur un chemin.
        JsonElement space = JsonDocument.Parse(File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "scenarios", "zones", "vallons.json"))).RootElement
            .GetProperty("data").GetProperty("space");
        foreach (JsonElement o in space.GetProperty("obstacles").EnumerateArray())
        {
            double ox0 = o.GetProperty("points").EnumerateArray().Min(p => p[0].GetDouble()), ox1 = o.GetProperty("points").EnumerateArray().Max(p => p[0].GetDouble());
            double oz0 = o.GetProperty("points").EnumerateArray().Min(p => p[1].GetDouble()), oz1 = o.GetProperty("points").EnumerateArray().Max(p => p[1].GetDouble());
            Assert.DoesNotContain(boxes, b => b.X0 < ox1 && ox0 < b.X1 && b.Z0 < oz1 && oz0 < b.Z1);
        }
        ZoneDefinition zone = ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"), Path.Combine(AppContext.BaseDirectory, "scenarios"),
            ShippedData, ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!).Zone!;
        IReadOnlyList<StyleRoad> roads = AdventureSession.Roads(zone);
        foreach (var b in boxes.Where(b => b.Id.StartsWith("forge/", StringComparison.Ordinal)))   // l'enclume et le foyer d'avant sont sur le chemin
            for (double x = b.X0; x <= b.X1 + 1e-9; x += 0.2)
                for (double z = b.Z0; z <= b.Z1 + 1e-9; z += 0.2)
                    Assert.True(TownRules.RoadAt(x, z, roads).Edge > 0.2, b.Id + " sur un chemin");
    }

    [Fact]
    public void Code_d_un_chemin()
    {
        Assert.Equal("C_RE_S01", LotCModels.CodeOf("Assets/EdS/Modeles/LotC/08_Recoltes/C_RE_S01/C_RE_S01.fbx"));
        Assert.Equal("C_RE_S01", LotCModels.CodeOf(@"Assets\EdS\modeles\lotc\08_Recoltes\C_RE_S01\C_RE_S01.FBX"));
        Assert.Null(LotCModels.CodeOf("Assets/EdS/Modeles/LotC/07_Butins/C_RE_S01/C_RE_S01.fbx"));   // mauvaise catégorie
        Assert.Null(LotCModels.CodeOf("Assets/EdS/Modeles/LotC/08_Recoltes/C_RE_S01/C_RE_S02.fbx"));
        Assert.Null(LotCModels.CodeOf("Assets/EdS/Modeles/Decor/08_Recoltes/C_RE_S01/C_RE_S01.fbx"));
        Assert.Null(LotCModels.CodeOf("Assets/EdS/Modeles/LotC/08_Recoltes/C_ZZ/C_ZZ.fbx"));
        Assert.Null(LotCModels.CodeOf(null));
        Assert.Null(LotCModels.CodeOf("C_RE_S01.fbx"));
    }

    /// <summary>Largeur et hauteur d'un JPEG (premier marqueur SOF).</summary>
    private static (int W, int H) JpegSize(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        int i = 2;
        while (i + 9 < b.Length)
        {
            if (b[i] != 0xFF) { i++; continue; }
            byte marker = b[i + 1];
            int length = (b[i + 2] << 8) | b[i + 3];
            if (marker >= 0xC0 && marker <= 0xC3) return ((b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);
            i += 2 + length;
        }
        throw new InvalidDataException(path);
    }
}
