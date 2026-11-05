using EdS.Runtime;

namespace EdS.Data.Tests;

/// <summary>
/// V1.10 (essai 10 de Damien sous Unity, 5 novembre 2026) : règles sans Unity des corrections d'affichage : décor qui passe en fondu quand
/// il cache le joueur, poutres qui s'effacent quand la caméra arrive à leur hauteur, halo des feux des packs qui n'est plus dessiné.
/// </summary>
[Trait("Category", "Runtime")]
public class V110EssaiTests
{
    [Fact]
    public void Un_decor_qui_cache_le_joueur_passe_en_fondu()
    {
        Assert.Equal(1, OccluderRules.Target(false, false));
        Assert.Equal(OccluderRules.FadeAlpha, OccluderRules.Target(true, false));
        Assert.Equal(0, OccluderRules.Target(true, true));
        Assert.InRange(OccluderRules.FadeAlpha, 0.1, 0.5);   // on devine le mur, on voit le personnage
        // Un quart de seconde de l'opaque au transparent, par pas réguliers, sans dépasser la cible.
        double a = 1;
        for (int i = 0; i < 3; i++) a = OccluderRules.Step(a, OccluderRules.FadeAlpha, 0.05);
        Assert.Equal(0.4, a, 9);
        a = OccluderRules.Step(a, OccluderRules.FadeAlpha, 1);
        Assert.Equal(OccluderRules.FadeAlpha, a, 9);
        Assert.Equal(1, OccluderRules.Step(0.9, 1, 1), 9);
        Assert.Equal(0.5, OccluderRules.Step(0.5, 0, -1), 9);   // temps négatif : rien ne bouge
        Assert.Equal(1, OccluderRules.Step(double.NaN, 1, 0.1), 9);
        // Plein : matériaux d'origine ; en fondu : copies transparentes ; presque rien : plus dessiné.
        Assert.False(OccluderRules.Clear(1));
        Assert.True(OccluderRules.Clear(0.9));
        Assert.True(OccluderRules.Shown(OccluderRules.FadeAlpha));
        Assert.False(OccluderRules.Shown(0));
    }

    [Fact]
    public void Le_corps_entier_compte_et_pas_seulement_la_tete()
    {
        // Avant V1.10, seule la tête (milieu du corps + 1 m) comptait : un mur qui cachait les jambes et le buste restait plein.
        Assert.Equal(new[] { -0.7, 0.0, 1.0 }, OccluderRules.BodyPoints);
    }

    [Theory]
    [InlineData(4.6, 2.55, true)]    // caméra d'une maison au réglage par défaut (8 m, 22°) : au-dessus des poutres
    [InlineData(2.3, 2.55, true)]    // à moins de 35 cm sous la poutre : elle s'efface déjà (la caméra n'y entre jamais)
    [InlineData(2.1, 2.55, false)]   // vue basse, de près : les poutres restent au plafond
    [InlineData(3.0, 5.75, false)]   // chapelle : la charpente est haute
    public void Les_poutres_s_effacent_quand_la_camera_arrive_a_leur_hauteur(double cameraY, double beamBottom, bool hidden) =>
        Assert.Equal(hidden, OccluderRules.Overhead(cameraY, beamBottom));

    [Theory]
    [InlineData("Glow", "FlameMaterial", true)]
    [InlineData("Flames", "HeatDistortion", true)]
    [InlineData("Fire Halo", null, true)]
    [InlineData("Flames", "FlameRoundYellow", false)]
    [InlineData("Sparks", "SparkMaterial", false)]
    [InlineData(null, null, false)]
    public void Le_halo_d_un_feu_de_pack_n_est_plus_dessine(string? objectName, string? materialName, bool halo) =>
        Assert.Equal(halo, ParticleRules.AmbientHalo(objectName, materialName));
}
