using System.IO;
using System.Linq;
using EdS.Runtime;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.12.4 (essai de Damien du 9 novembre 2026 : « System out of memory », GTX 470 à 1224 Mo, 4 Go de RAM) : la règle partagée
/// <see cref="MachineTier"/> (classe de la machine, limite de mipmaps), ses frontières, le choix du joueur et son branchement dans le jeu.
/// </summary>
[Trait("Category", "Runtime")]
public sealed class V2124PcModesteTests
{
    private static string Script(params string[] path) =>
        File.ReadAllText(Path.Combine(new[] { Lot93UnityPackageTests.Root, "unity", "Assets", "EdS" }.Concat(path).ToArray())).Replace("\r\n", "\n");

    [Fact]
    public void Le_PC_de_Damien_est_modeste_avec_des_textures_au_quart()
    {
        Assert.Equal(DeviceTier.Modest, MachineTier.Detect(1224, 4096, false));
        Assert.Equal(2, MachineTier.AutoMipLimit(1224, 4096, false));
        Assert.True(MachineTier.Light(DeviceTier.Modest));
    }

    [Theory]
    [InlineData(3072, 6144, DeviceTier.Full)]
    [InlineData(3071, 6144, DeviceTier.Modest)]
    [InlineData(3072, 6143, DeviceTier.Modest)]
    [InlineData(8192, 16384, DeviceTier.Full)]
    [InlineData(0, 0, DeviceTier.Full)]          // mémoires inconnues : comme avant
    [InlineData(0, 4096, DeviceTier.Modest)]     // mémoire vidéo inconnue : la RAM décide
    [InlineData(2048, 0, DeviceTier.Modest)]
    [InlineData(-1, 16384, DeviceTier.Full)]
    public void Classe_aux_frontieres(int vram, int ram, DeviceTier expected) => Assert.Equal(expected, MachineTier.Detect(vram, ram, false));

    [Theory]
    [InlineData(1536, 8192, 2)]
    [InlineData(1537, 8192, 1)]
    [InlineData(3071, 4096, 2)]
    [InlineData(3071, 4097, 1)]
    [InlineData(4096, 4097, 1)]
    [InlineData(4096, 4096, 2)]
    [InlineData(3072, 6144, 0)]
    [InlineData(0, 0, 0)]
    [InlineData(0, 5000, 1)]
    public void Limite_automatique_aux_frontieres(int vram, int ram, int expected) => Assert.Equal(expected, MachineTier.AutoMipLimit(vram, ram, false));

    [Fact]
    public void Le_telephone_est_inchange()
    {
        Assert.Equal(DeviceTier.Phone, MachineTier.Detect(512, 2048, true));
        Assert.Equal(DeviceTier.Phone, MachineTier.Detect(16384, 16384, true));
        Assert.Equal(0, MachineTier.AutoMipLimit(512, 2048, true));
        Assert.True(MachineTier.Light(DeviceTier.Phone));
        Assert.False(MachineTier.Light(DeviceTier.Full));
    }

    [Fact]
    public void Le_choix_du_joueur_passe_avant_l_automatique()
    {
        Assert.Equal(0, MachineTier.MipLimit(TextureChoice.Full, 1224, 4096, false));
        Assert.Equal(1, MachineTier.MipLimit(TextureChoice.Half, 8192, 16384, false));
        Assert.Equal(2, MachineTier.MipLimit(TextureChoice.Quarter, 8192, 16384, true));
        Assert.Equal(2, MachineTier.MipLimit(TextureChoice.Auto, 1224, 4096, false));
        Assert.Equal(TextureChoice.Full, MachineTier.Next(TextureChoice.Auto));
        Assert.Equal(TextureChoice.Half, MachineTier.Next(TextureChoice.Full));
        Assert.Equal(TextureChoice.Quarter, MachineTier.Next(TextureChoice.Half));
        Assert.Equal(TextureChoice.Auto, MachineTier.Next(TextureChoice.Quarter));
        Assert.Equal(TextureChoice.Auto, MachineTier.Choice(-1));
        Assert.Equal(TextureChoice.Auto, MachineTier.Choice(4));
        Assert.Equal(TextureChoice.Quarter, MachineTier.Choice(3));
        Assert.Equal("Qualité des textures : auto (quart)", MachineTier.Label(TextureChoice.Auto, 2));
        Assert.Equal("Qualité des textures : moitié", MachineTier.Label(TextureChoice.Half, 1));
    }

    [Fact]
    public void La_ligne_du_verificateur_dit_le_niveau_les_memoires_et_la_limite()
    {
        Assert.Equal("PC modeste (mémoire vidéo 1224 Mo, mémoire 4096 Mo) ; textures : quart (limite de mipmaps 2) ; ressources lourdes (« jamais sur téléphone ») écartées",
            MachineTier.Describe(1224, 4096, false, 2));
        Assert.Equal("PC complet (mémoire vidéo inconnue, mémoire inconnue) ; textures : pleine (limite de mipmaps 0)", MachineTier.Describe(0, 0, false, 0));
    }

    [Fact]
    public void Le_jeu_branche_la_regle()
    {
        string quality = Script("Game", "DeviceQuality.cs");
        Assert.Contains("QualitySettings.globalTextureMipmapLimit = Limit", quality);
        Assert.Contains("SystemInfo.graphicsMemorySize", quality);
        Assert.Contains("SystemInfo.systemMemorySize", quality);
        Assert.Contains("DeviceQuality.Apply();", Script("Game", "EdsGame.cs"));
        Assert.Contains("EdS.Runtime.Wave5.Shown(entry, DeviceQuality.Light)", Script("Game", "StyleCatalog.cs"));
        Assert.DoesNotContain("Wave5.Shown(entry, Application.isMobilePlatform)", Script("Game", "StyleCatalog.cs"));
        Assert.Contains("DeviceQuality.Cycle", Script("Game", "HudView.cs"));
        Assert.Contains("MachineTier.Describe(", Script("Editor", "EdsVerification.cs"));
    }
}
