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

    /// <summary>Centre et bas du pied à l'échelle 1, comme les mesure l'éditeur (<c>EdsStyle.Foot</c>).</summary>
    private const double OffX = -0.02, OffY = 0, OffZ = 0.4485;

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

    private static ModelFit SouthFit() => TownModels.Fit(14.5, 11, (0, -1), ModelX, ModelZ, TownModels.Overflow(StyleBuilding.House));

    [Fact]
    public void La_lanterne_est_a_droite_de_la_porte_devant_la_facade()
    {
        var local = TownModels.OwnLantern("boulangerie_firmin");
        Assert.NotNull(local);
        ModelFit fit = SouthFit();
        var w = TownModels.OwnPoint(0, 0, (0, -1), fit, OffX, OffY, OffZ, local!.Value.X, local.Value.Y, local.Value.Z);
        // Porte au milieu du sud (x = 0) ; vue de dehors (on regarde vers +z), la droite est +x : 1,30 m × 0,979.
        Assert.Equal(1.253, w.X, 2);
        // Façade (z = 5,5 dans le modèle) posée à −4,55 ; la lanterne 0,36 m devant, dans l'emprise (−5,5).
        double facade = TownModels.OwnPoint(0, 0, (0, -1), fit, OffX, OffY, OffZ, 0, 0, 5.5).Z;
        Assert.Equal(-4.551, facade, 2);
        Assert.InRange(w.Z, -5.5, facade - 0.3);
        Assert.InRange(w.Y, 1.7, 2.0);
        Assert.Null(TownModels.OwnLantern("Olosta1"));
        Assert.Null(TownModels.OwnLantern(null));
    }

    [Fact]
    public void La_fumee_sort_de_la_cheminee_du_fournil()
    {
        var local = TownModels.OwnChimney("Boulangerie_Firmin");
        Assert.NotNull(local);
        var w = TownModels.OwnPoint(0, 0, (0, -1), SouthFit(), OffX, OffY, OffZ, local!.Value.X, local.Value.Y, local.Value.Z);
        // Le fournil est à droite de la boutique vue de la rue (+x quand la façade regarde le sud), la cheminée à l'arrière (+z).
        Assert.Equal(4.777, w.X, 2);
        Assert.Equal(2.251, w.Z, 2);
        Assert.Equal(7.45 * Math.Sqrt(SouthFit().ScaleX * SouthFit().ScaleZ), w.Y, 6);
    }

    private static (int W, int H) PngSize(byte[] b) => ((b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19], (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23]);

    private static (int W, int H) JpegSize(byte[] b)
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
