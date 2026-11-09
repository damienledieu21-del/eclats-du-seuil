using System.Linq;
using EdS.Runtime;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.12.7 (essai du 9 novembre 2026 : four et comptoir blancs, objets roses, tonneau bleu, flaques noires) : règles pures de
/// réparation des matières (<see cref="MaterialRepairRules"/>) et des flaques (<see cref="PuddleLook"/>).
/// </summary>
[Trait("Category", "Runtime")]
public sealed class V2127MatieresTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("Hidden/InternalErrorShader", true)]
    [InlineData("Standard", true)]
    [InlineData("Standard (Specular setup)", true)]
    [InlineData("Legacy Shaders/Diffuse", true)]
    [InlineData("Mobile/Diffuse", true)]
    [InlineData("Universal Render Pipeline/Lit", false)]
    [InlineData("Universal Render Pipeline/Simple Lit", false)]
    [InlineData("Shader Graphs/Wood", false)]
    [InlineData("EdS/Feuillage", false)]
    [InlineData("Particles/Standard Unlit", false)]
    public void Un_shader_hors_URP_sortirait_rose(string? shader, bool pink) =>
        Assert.Equal(pink, MaterialRepairRules.NeedsUrp(shader));

    [Fact]
    public void Le_defaut_distingue_rose_blanc_et_correct()
    {
        Assert.Equal(MaterialFault.Pink, MaterialRepairRules.Fault("Hidden/InternalErrorShader", 0.5, 0.5, 0.5, true));
        Assert.Equal(MaterialFault.White, MaterialRepairRules.Fault("Universal Render Pipeline/Lit", 1, 1, 1, false));
        Assert.Equal(MaterialFault.None, MaterialRepairRules.Fault("Universal Render Pipeline/Lit", 1, 1, 1, true));
        Assert.Equal(MaterialFault.None, MaterialRepairRules.Fault("Universal Render Pipeline/Lit", 0.45, 0.3, 0.2, false));
    }

    [Theory]
    [InlineData("Brick_Bread_Oven", "eds_SF_Brick_Bread_Oven_torricane.fbx", 0.56)]   // brique
    [InlineData("Material.001", "TraderTable", 0.45)]                                  // bois par défaut
    [InlineData("Bread1", "Bread1.prefab", 0.74)]                                       // pain
    [InlineData("Metal", "Anvil_URP", 0.32)]
    public void La_couleur_de_repli_suit_le_genre_d_objet_et_n_est_jamais_blanche(string material, string model, double red)
    {
        (double r, double g, double b) = MaterialRepairRules.FallbackColor(material, model);
        Assert.Equal(red, r, 2);
        Assert.True(r < 0.9 || g < 0.9 || b < 0.9);
    }

    [Fact]
    public void Le_nom_de_la_matiere_passe_avant_celui_du_modele()
    {
        (double r, _, _) = MaterialRepairRules.FallbackColor("Cheese", "WoodenBox1 Cheese");
        Assert.Equal(0.88, r, 2);
    }

    [Fact]
    public void La_texture_de_meme_nom_est_reprise_mais_jamais_la_seule_du_dossier()
    {
        string[] maps =
        {
            "Assets/Style/Sketchfab/Boulangerie/tex/Pie_BaseColor.png",
            "Assets/Style/Sketchfab/Boulangerie/tex/Brick_Bread_Oven_torricane_albedo.png",
            "Assets/Style/Sketchfab/Boulangerie/tex/Brick_Bread_Oven_torricane_normal.png",
        };
        Assert.EndsWith("Brick_Bread_Oven_torricane_albedo.png",
            MaterialRepairRules.BestTexture("lambert1", "Assets/Style/Sketchfab/Boulangerie/eds_SF_Brick_Bread_Oven_torricane.fbx", maps));
        Assert.EndsWith("Pie_BaseColor.png", MaterialRepairRules.BestTexture("Pie", "eds_SF_Other.fbx", maps));
        Assert.Null(MaterialRepairRules.BestTexture("Material", "eds_SF_Sack_of_Flour.fbx", maps.Take(1)));
    }

    [Theory]
    [InlineData(0.45, 0.31, 0.19, false, true)]    // brun
    [InlineData(0.2, 0.3, 0.9, false, false)]      // bleu uni
    [InlineData(1.0, 1.0, 1.0, false, false)]      // blanc
    [InlineData(1.0, 1.0, 1.0, true, true)]        // texture blanche
    [InlineData(0.3, 0.4, 0.9, true, false)]       // texture mais teinte bleue
    public void Un_tonneau_de_bois_n_est_ni_bleu_ni_blanc(double r, double g, double b, bool textured, bool wood) =>
        Assert.Equal(wood, MaterialRepairRules.LooksLikeWood(r, g, b, textured));

    [Fact]
    public void Les_flaques_sont_transparentes_et_plus_legeres_sur_PC_modeste()
    {
        PuddleLook full = PuddleLook.For(false), light = PuddleLook.For(true);
        Assert.InRange(full.Alpha, 0.35, 0.5);
        Assert.InRange(light.Alpha, 0.3, 0.5);
        Assert.True(light.TextureSize < full.TextureSize);
        Assert.True(full.Lift > 0 && light.Lift > 0);
        // Reprend la couleur du ciel : jamais le noir presque opaque de l'essai du 9 novembre.
        (double r, double g, double b) = full.Tint(0.5, 0.6, 0.8, 0.3, 0.3, 0.3);
        Assert.True(r + g + b > 0.6);
    }
}
