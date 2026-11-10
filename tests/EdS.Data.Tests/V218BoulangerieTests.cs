using EdS.Runtime;

namespace EdS.Data.Tests;

/// <summary>
/// V2.18 : nouvelle boulangerie de Firmin (modèle Blender validé par Damien le 10 novembre 2026), version de jeu LOD1 cuite, exportée
/// recentrée sur l'emprise des murs (14,5 × 11 m) ; sans Unity.
/// </summary>
[Trait("Category", "World")]
public class V218BoulangerieTests
{
    /// <summary>Pied mesuré du FBX (m, repère Unity : x −7,87 à 7,83, z −6,02 à 6,92, sol à 0).</summary>
    public const double ModelX = 15.70, ModelZ = 12.943;

    private static string Folder => Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Modeles", "Batiments", "maison2");

    [Fact]
    public void Le_dossier_ne_contient_que_le_modele_et_ses_deux_textures()
    {
        string[] files = Directory.GetFiles(Folder).Select(Path.GetFileName).Where(f => !f!.EndsWith(".meta", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal).ToArray()!;
        Assert.Equal(new[] { "boulangerie_firmin.fbx", "boulangerie_firmin_couleur.jpg", "boulangerie_firmin_normale.png" }, files);
        Assert.Equal("maison2", TownModels.OwnBuildingOf("Assets/EdS/Modeles/Batiments/maison2/boulangerie_firmin.fbx"));
    }

    [Fact]
    public void Les_textures_font_2048()
    {
        Assert.Equal((2048, 2048), PngSize(File.ReadAllBytes(Path.Combine(Folder, "boulangerie_firmin_normale.png"))));
        Assert.Equal((2048, 2048), JpegSize(File.ReadAllBytes(Path.Combine(Folder, "boulangerie_firmin_couleur.jpg"))));
    }

    [Fact]
    public void Le_fbx_est_binaire_et_leger()
    {
        byte[] fbx = File.ReadAllBytes(Path.Combine(Folder, "boulangerie_firmin.fbx"));
        Assert.StartsWith("Kaydara FBX Binary", System.Text.Encoding.ASCII.GetString(fbx, 0, 18), StringComparison.Ordinal);
        Assert.InRange(fbx.Length, 10_000, 200_000);   // LOD1 de 619 triangles, sans texture embarquée
    }

    [Fact]
    public void La_lanterne_est_a_droite_de_la_porte_devant_la_facade()
    {
        var local = TownModels.OwnLantern("boulangerie_firmin");
        Assert.NotNull(local);
        // Emprise −7,25 à 7,25 × −5,5 à 5,5, porte au milieu du sud (x = 0) : échelle 1, la façade est le bord sud (z = −5,5).
        var w = TownModels.OwnPlace(0, 0, (0, -1), local!.Value.X, local.Value.Y, local.Value.Z);
        Assert.Equal(1.3, w.X, 6);   // vue de dehors (on regarde vers +z), la droite est +x
        Assert.Equal(-5.9, w.Z, 6);  // 0,40 m devant la façade
        Assert.Equal(1.94, w.Y, 6);
        Assert.Equal(-5.5, TownModels.OwnPlace(0, 0, (0, -1), 0, 0, 5.5).Z, 6);
        Assert.Null(TownModels.OwnLantern("Olosta1"));
        Assert.Null(TownModels.OwnLantern(null));
    }

    [Fact]
    public void La_fumee_sort_de_la_cheminee_du_fournil()
    {
        var local = TownModels.OwnChimney("Boulangerie_Firmin");
        Assert.NotNull(local);
        // Le fournil est à droite de la boutique vue de la rue (+x quand la façade regarde le sud), la cheminée à l'arrière (+z).
        var w = TownModels.OwnPlace(0, 0, (0, -1), local!.Value.X, local.Value.Y, local.Value.Z);
        Assert.Equal(4.9, w.X, 6);
        Assert.Equal(2.05, w.Z, 6);
        Assert.Equal(7.45, w.Y, 6);
        // Façade vers +x (porte à l'est) : vu de dehors (on regarde vers −x), la droite est +z ; l'arrière est −x.
        var e = TownModels.OwnPlace(10, 20, (1, 0), local.Value.X, local.Value.Y, local.Value.Z);
        Assert.Equal(10 - 2.05, e.X, 6);
        Assert.Equal(20 + 4.9, e.Z, 6);
    }

    internal static (int W, int H) PngSize(byte[] b) => ((b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19], (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23]);

    internal static (int W, int H) JpegSize(byte[] b)
    {
        int i = 2;
        while (i + 9 < b.Length)
        {
            if (b[i] != 0xFF) { i++; continue; }
            byte m = b[i + 1];
            int len = (b[i + 2] << 8) | b[i + 3];
            if (m >= 0xC0 && m <= 0xCF && m != 0xC4 && m != 0xC8 && m != 0xCC) return ((b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);
            i += 2 + len;
        }
        return (0, 0);
    }
}
