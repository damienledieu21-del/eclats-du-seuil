using System.Text.Json;
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
