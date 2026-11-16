using System.Text.Json;
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
            _ => LotCModels.VillageSpots.Contains(key) && markers.Contains("\"" + key + "\""),
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
        (double W, double D)[] models = { (0.6, 1.8), (0.55, 1.8), (0.96, 1.2), (0.79, 3.0), (1.6, 2.12), (0.75, 1.4), (0.65, 2.0), (2.4, 0.85), (0.3, 0.3) };
        int rooms = 0;
        foreach (string room in new[] { "auberge", "boulangerie", "guilde", "archives" })
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
        Assert.Equal(4, rooms);
        // Dos au mur : le comptoir de l'auberge regarde l'intérieur, sur une seule rangée de copies le long du grand côté.
        Assert.Equal(180, LotCModels.Fit(-1, 4, 1, 4.5, -7, -5, 7, 5, 1.8, 0.55)[0].Yaw);
        Assert.Equal(0, LotCModels.Fit(-1, -4.5, 1, -4, -7, -5, 7, 5, 1.8, 0.55)[0].Yaw);
        Assert.Empty(LotCModels.Fit(0, 0, 1, 1, -7, -5, 7, 5, 0, 1));
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
