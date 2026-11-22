using System.IO;
using System.Linq;
using EdS.Runtime;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// Demande de Damien du 21 novembre 2026 (« graphismes bas » sur son PC : 1 224 Mo de mémoire vidéo, 4 Go de RAM) : réglage « Qualité »
/// (Automatique, Basse, Moyenne, Haute) qui force le niveau à la place de la détection (<see cref="MachineTier.Level"/>), retenu dans les
/// préférences, appliqué tout de suite pour les textures et en relançant le jeu pour le reste, avec un avertissement en Moyenne et Haute sur une
/// petite machine. Remplace le choix des textures du sous-lot V2.12.4.
/// </summary>
[Trait("Category", "Runtime")]
public sealed class QualiteForceeTests
{
    private static string Script(params string[] path) =>
        File.ReadAllText(Path.Combine(new[] { Lot93UnityPackageTests.Root, "unity", "Assets", "EdS" }.Concat(path).ToArray())).Replace("\r\n", "\n");

    private static QualityLevel Damien(QualityChoice choice) => MachineTier.Level(choice, 1224, 4096, false);

    [Fact]
    public void Automatique_garde_la_detection_du_PC_de_Damien()
    {
        QualityLevel auto = Damien(QualityChoice.Auto);
        Assert.Equal(DeviceTier.Modest, auto.Tier);
        Assert.True(auto.Light);
        Assert.Equal(2, auto.MipLimit);
        Assert.Equal(1, StreamingProfile.For(auto.Tier).DetailRadius);
        Assert.Equal(900, StreamingProfile.For(auto.Tier).ViewMeters);
        QualityLevel strong = MachineTier.Level(QualityChoice.Auto, 8192, 16384, false);
        Assert.Equal(DeviceTier.Full, strong.Tier);
        Assert.False(strong.Light);
        Assert.Equal(0, strong.MipLimit);
    }

    [Theory]
    [InlineData(QualityChoice.Low, DeviceTier.Modest, true, 2)]
    [InlineData(QualityChoice.Medium, DeviceTier.Full, true, 1)]
    [InlineData(QualityChoice.High, DeviceTier.Full, false, 0)]
    public void Un_niveau_force_passe_avant_la_detection(QualityChoice choice, DeviceTier tier, bool light, int mip)
    {
        foreach (var (vram, ram) in new[] { (1224, 4096), (8192, 16384), (0, 0) })
        {
            QualityLevel level = MachineTier.Level(choice, vram, ram, false);
            Assert.Equal(tier, level.Tier);
            Assert.Equal(light, level.Light);
            Assert.Equal(mip, level.MipLimit);
        }
    }

    [Fact]
    public void Haute_sur_le_PC_de_Damien_charge_plus_loin()
    {
        StreamingProfile high = StreamingProfile.For(Damien(QualityChoice.High).Tier);
        Assert.Equal(2, high.DetailRadius);
        Assert.True(high.ViewMeters > 900);
        Assert.Equal(DeviceTier.Phone, MachineTier.Level(QualityChoice.Low, 512, 2048, true).Tier);   // basse ne relève pas un téléphone
        QualityLevel phoneMedium = MachineTier.Level(QualityChoice.Medium, 512, 2048, true);        // moyenne non plus
        Assert.Equal(DeviceTier.Phone, phoneMedium.Tier);
        Assert.True(phoneMedium.Light);
        Assert.Equal(1, phoneMedium.MipLimit);
    }

    [Fact]
    public void Avertissement_en_haute_sur_une_petite_machine_seulement()
    {
        Assert.True(MachineTier.Risky(QualityChoice.High, 1224, 4096, false));
        Assert.True(MachineTier.Risky(QualityChoice.High, 512, 2048, true));
        Assert.False(MachineTier.Risky(QualityChoice.High, 8192, 16384, false));
        Assert.True(MachineTier.Risky(QualityChoice.Medium, 1224, 4096, false));   // le PC de Damien (erreur de mémoire du 9 novembre)
        Assert.True(MachineTier.Risky(QualityChoice.Medium, 512, 2048, true));
        Assert.False(MachineTier.Risky(QualityChoice.Medium, 8192, 16384, false));
        Assert.False(MachineTier.Risky(QualityChoice.Low, 1224, 4096, false));
        Assert.False(MachineTier.Risky(QualityChoice.Auto, 1224, 4096, false));
        Assert.Equal("peut ralentir sur cette machine", MachineTier.SlowWarning);
    }

    [Fact]
    public void Le_bouton_tourne_et_les_preferences_se_relisent()
    {
        Assert.Equal(QualityChoice.Low, MachineTier.Next(QualityChoice.Auto));
        Assert.Equal(QualityChoice.Medium, MachineTier.Next(QualityChoice.Low));
        Assert.Equal(QualityChoice.High, MachineTier.Next(QualityChoice.Medium));
        Assert.Equal(QualityChoice.Auto, MachineTier.Next(QualityChoice.High));
        Assert.Equal(QualityChoice.Auto, MachineTier.Choice(0));
        Assert.Equal(QualityChoice.High, MachineTier.Choice(3));
        Assert.Equal(QualityChoice.Auto, MachineTier.Choice(-1));
        Assert.Equal(QualityChoice.Auto, MachineTier.Choice(4));
    }

    [Fact]
    public void Le_texte_dit_quand_le_reglage_compte_et_avertit()
    {
        QualityLevel auto = Damien(QualityChoice.Auto);
        Assert.Equal("Qualité : automatique (textures au quart)", MachineTier.Label(QualityChoice.Auto, auto, auto, false));
        // Basse sur le PC de Damien : même classe que l'automatique, seules les textures comptent : tout de suite.
        Assert.Equal("Qualité : basse", MachineTier.Label(QualityChoice.Low, Damien(QualityChoice.Low), auto, false));
        Assert.Equal("Qualité : moyenne · relancer le jeu · peut ralentir sur cette machine",
            MachineTier.Label(QualityChoice.Medium, Damien(QualityChoice.Medium), auto, MachineTier.Risky(QualityChoice.Medium, 1224, 4096, false)));
        Assert.Equal("Qualité : moyenne · relancer le jeu", MachineTier.Label(QualityChoice.Medium, Damien(QualityChoice.Medium), auto, false));
        Assert.Equal("Qualité : haute · relancer le jeu · peut ralentir sur cette machine",
            MachineTier.Label(QualityChoice.High, Damien(QualityChoice.High), auto, true));
        // Après relance en haute : plus de « relancer le jeu », l'avertissement reste ; jamais de saut de ligne (rangée de l'écran titre).
        QualityLevel high = Damien(QualityChoice.High);
        Assert.Equal("Qualité : haute · peut ralentir sur cette machine", MachineTier.Label(QualityChoice.High, high, high, true));
        Assert.DoesNotContain("\n", MachineTier.Label(QualityChoice.High, high, Damien(QualityChoice.Auto), true));
        QualityLevel full = MachineTier.Level(QualityChoice.Auto, 8192, 16384, false);
        Assert.Equal("Qualité : automatique (textures pleines)", MachineTier.Label(QualityChoice.Auto, full, full, false));
    }

    [Fact]
    public void La_ligne_du_verificateur_dit_le_niveau_force()
    {
        Assert.Equal(MachineTier.Describe(1224, 4096, false, 2), MachineTier.Describe(1224, 4096, false, 2, QualityChoice.Auto));
        Assert.Equal("PC modeste (mémoire vidéo 1224 Mo, mémoire 4096 Mo) ; qualité forcée : haute (réglages d'un PC complet), peut ralentir sur cette machine ; textures : pleine (limite de mipmaps 0)",
            MachineTier.Describe(1224, 4096, false, 0, QualityChoice.High));
        Assert.Equal("PC modeste (mémoire vidéo 1224 Mo, mémoire 4096 Mo) ; qualité forcée : moyenne (réglages d'un PC complet), peut ralentir sur cette machine ; textures : moitié (limite de mipmaps 1) ; ressources lourdes (« jamais sur téléphone ») écartées",
            MachineTier.Describe(1224, 4096, false, 1, QualityChoice.Medium));
        Assert.Equal("PC complet (mémoire vidéo 8192 Mo, mémoire 16384 Mo) ; qualité forcée : basse (réglages d'un PC modeste) ; textures : quart (limite de mipmaps 2) ; ressources lourdes (« jamais sur téléphone ») écartées",
            MachineTier.Describe(8192, 16384, false, 2, QualityChoice.Low));
    }

    [Fact]
    public void Le_jeu_branche_le_reglage()
    {
        string quality = Script("Game", "DeviceQuality.cs");
        Assert.Contains("\"eds.qualite\"", quality);
        Assert.Contains("PlayerPrefs.SetInt(ChoiceKey, (int)MachineTier.Next(Choice));", quality);
        Assert.Contains("PlayerPrefs.Save();", quality);
        Assert.Contains("MachineTier.Level(Choice, Vram, Ram, Application.isMobilePlatform)", quality);
        Assert.Contains("public static DeviceTier Tier => Session.Tier;", quality);
        Assert.Contains("public static bool Light => Session.Light;", quality);
        Assert.Contains("QualitySettings.globalTextureMipmapLimit = Limit", quality);
        Assert.DoesNotContain("TextureChoice", quality);
        Assert.Contains("DeviceQuality.Cycle", Script("Game", "HudView.cs"));
        Assert.Contains("_textures.Text.Value = DeviceQuality.Label;", Script("Game", "HudView.cs"));
        Assert.Contains("_qualityRow = DeviceQuality.Label;", Script("Game", "TitleView.cs"));
        Assert.Contains("DeviceQuality.Cycle();", Script("Game", "TitleView.cs"));
        string verification = Script("Editor", "EdsVerification.cs");
        Assert.Contains("DeviceQuality.Limit, DeviceQuality.Choice)", verification);
        Assert.Contains("StreamingProfile.For(DeviceQuality.Wanted.Tier)", verification);
        Assert.Equal("Qualité", TitleMenu.Label(TitleAction.Textures));
    }
}
