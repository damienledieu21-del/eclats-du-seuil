using EdS.Runtime;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.55 (sujet 13, écran titre vivant) : choix du décor selon la saison et l'heure du monde, fondu entre deux, Envers, placement du
/// personnage et du logo, fondus, images livrées. Sans Unity. Affichage seulement.
/// </summary>
[Trait("Category", "Runtime")]
public sealed class V255EcranTitreTests
{
    private static string Resources => Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Interface", "Resources");

    [Theory]
    [InlineData(0, 0, 3)]     // printemps, minuit : nuit
    [InlineData(0, 420, 1)]   // 7 h : aube
    [InlineData(0, 780, 2)]   // 13 h : jour
    [InlineData(1, 1100, 4)]  // été, 18 h 20 : le crépuscule reprend l'aube
    [InlineData(2, 1380, 9)]  // automne, 23 h : nuit
    [InlineData(3, 780, 11)]  // hiver, 13 h : jour
    [InlineData(3, 60, 12)]   // hiver, 1 h : nuit
    public void Le_decor_suit_la_saison_et_l_heure_sans_fondu_hors_des_frontieres(int season, int minute, int number)
    {
        TitleMix mix = TitleScreen.Pick(season, minute, false);
        Assert.Equal(number, mix.Primary.Number);
        Assert.Equal(0f, mix.Mix);
        Assert.Equal(number, mix.Secondary.Number);
    }

    [Fact]
    public void Chaque_saison_et_chaque_moment_ont_leur_numero_de_1_a_12()
    {
        var seen = new HashSet<int>();
        for (int s = 0; s < 4; s++)
            foreach (TitleMoment m in new[] { TitleMoment.Dawn, TitleMoment.Day, TitleMoment.Night })
                Assert.True(seen.Add(TitleScreen.Decor(s, m).Number));
        Assert.Equal(Enumerable.Range(1, 12), seen.OrderBy(n => n));
        Assert.Equal(2, TitleScreen.Decor(0, TitleMoment.Day).Number);
        Assert.Equal(12, TitleScreen.Decor(3, TitleMoment.Night).Number);
        Assert.Throws<ArgumentOutOfRangeException>(() => TitleScreen.Decor(4, TitleMoment.Day));
    }

    [Fact]
    public void Le_fondu_d_une_heure_passe_de_l_un_a_l_autre_sans_saut_et_tout_le_jour_est_couvert()
    {
        // Aube -> jour autour de 9 h (540) : de 8 h 30 à 9 h 30.
        TitleMix before = TitleScreen.Pick(0, 509, false), start = TitleScreen.Pick(0, 510, false), mid = TitleScreen.Pick(0, 540, false), end = TitleScreen.Pick(0, 569, false);
        Assert.Equal(0f, before.Mix);
        Assert.Equal(1, start.Primary.Number);
        Assert.Equal(2, start.Secondary.Number);
        Assert.Equal(0f, start.Mix);
        Assert.InRange(mid.Mix, 0.49f, 0.51f);
        Assert.InRange(end.Mix, 0.99f, 1f);
        float last = -1f;
        for (int m = 510; m <= 569; m++)
        {
            float t = TitleScreen.Pick(0, m, false).Mix;
            Assert.InRange(t, last, 1f);
            last = t;
        }
        for (int m = -1500; m < 3000; m++)
        {
            TitleMix x = TitleScreen.Pick(1, m, false);
            Assert.InRange(x.Mix, 0f, 1f);
            Assert.InRange(x.Primary.Number, 4, 6);
            Assert.InRange(x.Secondary.Number, 4, 6);
        }
        // La nuit passe à l'aube vers 5 h, le crépuscule à la nuit vers 21 h : fondu aux deux bouts.
        Assert.Equal(TitleMoment.Night, TitleScreen.Pick(0, 300, false).Primary.Moment);
        Assert.Equal(TitleMoment.Dawn, TitleScreen.Pick(0, 300, false).Secondary.Moment);
        Assert.Equal(TitleMoment.Dawn, TitleScreen.Pick(0, 1260, false).Primary.Moment);
        Assert.Equal(TitleMoment.Night, TitleScreen.Pick(0, 1260, false).Secondary.Moment);
    }

    [Fact]
    public void L_Envers_remplace_tout_quand_il_est_la()
    {
        foreach (int minute in new[] { 0, 420, 780, 1380 })
        {
            TitleMix mix = TitleScreen.Pick(2, minute, true);
            Assert.True(mix.Primary.IsEnvers && mix.Secondary.IsEnvers);
            Assert.Equal(13, mix.Primary.Number);
            Assert.Equal(0f, mix.Mix);
        }
        Assert.False(TitleScreen.Pick(2, 780, false).Primary.IsEnvers);
        Assert.Equal("EdsInterface/Titre/1920/titre_13_envers", TitleScreen.ResourcePath(TitleScreen.Envers, DeviceTier.Full));
    }

    [Fact]
    public void Les_chemins_suivent_la_taille_de_la_machine()
    {
        TitleDecor d = TitleScreen.Decor(3, TitleMoment.Dawn);
        Assert.Equal("EdsInterface/Titre/1920/titre_10_hiver_aube", TitleScreen.ResourcePath(d, DeviceTier.Full));
        Assert.Equal("EdsInterface/Titre/1280/titre_10_hiver_aube", TitleScreen.ResourcePath(d, DeviceTier.Phone));
        Assert.Equal("EdsInterface/Titre/1280/titre_05_ete_jour", TitleScreen.ResourcePath(TitleScreen.Decor(1, TitleMoment.Day), DeviceTier.Modest));
    }

    [Fact]
    public void Les_treize_decors_existent_en_deux_tailles_en_jpeg_16_9()
    {
        var decors = new List<TitleDecor> { TitleScreen.Envers };
        for (int s = 0; s < 4; s++)
            foreach (TitleMoment m in new[] { TitleMoment.Dawn, TitleMoment.Day, TitleMoment.Night }) decors.Add(TitleScreen.Decor(s, m));
        Assert.Equal(13, decors.Count);
        foreach (TitleDecor decor in decors)
            foreach ((DeviceTier tier, int w, int h) in new[] { (DeviceTier.Full, 1920, 1080), (DeviceTier.Phone, 1280, 720) })
            {
                string file = Path.Combine(Resources, TitleScreen.ResourcePath(decor, tier) + ".jpg");
                Assert.True(File.Exists(file), file);
                Assert.True(new FileInfo(file).Length < 1_000_000, file);   // jamais le 4K d'origine
                byte[] b = File.ReadAllBytes(file);
                Assert.Equal(0xFF, b[0]);
                Assert.Equal(0xD8, b[1]);
                (int fw, int fh) = JpegSize(b);
                Assert.Equal((w, h), (fw, fh));
            }
    }

    private static (int W, int H) JpegSize(byte[] b)
    {
        int i = 2;
        while (i + 9 < b.Length)
        {
            if (b[i] != 0xFF) { i++; continue; }
            int marker = b[i + 1];
            if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                return ((b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);
            i += 2 + ((b[i + 2] << 8) | b[i + 3]);
        }
        throw new InvalidDataException("pas de cadre JPEG");
    }

    [Fact]
    public void Le_recadrage_couvre_la_fenetre_sans_deformer_et_garde_la_bande_de_securite()
    {
        TitleScreen.Cover(1920f, 1080f, out float s, out float ox, out float oy);
        Assert.Equal(0.5f, s);
        Assert.Equal(0f, ox);
        Assert.Equal(0f, oy);
        // Fenêtre plus large (21 : 9) : l'image est recadrée en hauteur ; la bande de sécurité (15 % à 85 %) reste visible.
        TitleScreen.Cover(2520f, 1080f, out s, out ox, out oy);
        Assert.Equal(2520f / 3840f, s, 4);
        Assert.True(oy < 0f);
        Assert.True(oy + TitleScreen.SafeTop * s >= -0.5f && oy + TitleScreen.SafeBottom * s <= 1080.5f);
        // Fenêtre plus haute (4 : 3) : recadrée en largeur.
        TitleScreen.Cover(1440f, 1080f, out s, out ox, out oy);
        Assert.Equal(0.5f, s);
        Assert.True(ox < 0f);
        Assert.Throws<ArgumentOutOfRangeException>(() => TitleScreen.Cover(0f, 10f, out _, out _, out _));
    }

    [Fact]
    public void Les_pieds_du_heros_tombent_au_bas_de_son_enveloppe_et_le_logo_reste_en_haut_a_droite()
    {
        Assert.Equal(TitleScreen.FeetY, TitleScreen.HeroY + TitleScreen.HeroHeight);
        Assert.Equal(TitleScreen.FeetX, TitleScreen.HeroX + TitleScreen.HeroWidth / 2f);
        Assert.Equal(TitleScreen.ReferenceHeight / 3f, TitleScreen.HeroHeight);
        TitleRect hero = TitleScreen.Place(TitleScreen.HeroX, TitleScreen.HeroY, TitleScreen.HeroWidth, TitleScreen.HeroHeight, 1920f, 1080f);
        Assert.Equal(480f, hero.X);
        Assert.Equal(528f, hero.Y);
        Assert.Equal(888f, hero.Y + hero.Height);
        TitleRect logo = TitleScreen.LogoRect(1920f, 1080f, 0f);
        Assert.Equal(1440f, logo.X);
        Assert.Equal(135f, logo.Y);
        Assert.Equal(450f, logo.Width);
        Assert.Equal(225f, logo.Height);
        Assert.True(logo.X > hero.X + hero.Width);   // le logo ne touche pas le héros
        // Petite fenêtre : le logo garde sa largeur minimale, sans sortir à droite.
        TitleRect small = TitleScreen.LogoRect(800f, 360f, 420f);
        Assert.Equal(420f, small.Width);
        Assert.True(small.X + small.Width <= 800.01f && small.X >= 0f);
    }

    [Fact]
    public void Les_fondus_et_le_lancement_sont_sans_a_coup()
    {
        Assert.Equal(0f, TitleScreen.Opacity(-1f, -1f));
        Assert.Equal(0f, TitleScreen.Opacity(0f, -1f));
        Assert.Equal(1f, TitleScreen.Opacity(TitleScreen.FadeInSeconds, -1f));
        Assert.Equal(1f, TitleScreen.Opacity(10f, -1f));
        Assert.InRange(TitleScreen.Opacity(10f, TitleScreen.FadeOutSeconds / 2f), 0.49f, 0.51f);
        Assert.Equal(0f, TitleScreen.Opacity(10f, TitleScreen.FadeOutSeconds));
        Assert.False(TitleScreen.GestureStarts(TitleScreen.StartAfter - 0.01f));
        Assert.True(TitleScreen.GestureStarts(TitleScreen.StartAfter));
        Assert.False(TitleScreen.Finished(-1f) || TitleScreen.Finished(TitleScreen.FadeOutSeconds - 0.01f));
        Assert.True(TitleScreen.Finished(TitleScreen.FadeOutSeconds));
        for (float t = 0f; t < 120f; t += 0.5f)
        {
            Assert.InRange(TitleScreen.PromptPulse(t), 0.44f, 1.001f);
            Assert.InRange(TitleScreen.Drift(t), 1f, 1.02f);
        }
    }

    [Fact]
    public void La_lumiere_du_heros_suit_le_decor_et_se_melange_avec_lui()
    {
        TitleScreen.LightFor(TitleScreen.Decor(0, TitleMoment.Night), out float nr, out _, out float nb, out float ni);
        TitleScreen.LightFor(TitleScreen.Decor(0, TitleMoment.Dawn), out float dr, out _, out float db, out float di);
        Assert.True(nb > nr && dr > db);   // la nuit est bleue, l'aube est chaude
        Assert.True(ni < di);
        TitleScreen.LightFor(TitleScreen.Pick(0, 300, false), out float mr, out _, out _, out float mi);
        Assert.InRange(mr, nr, dr);
        Assert.InRange(mi, ni, di);
        TitleScreen.LightFor(TitleScreen.Envers, out float er, out _, out float eb, out _);
        Assert.True(eb > er);
    }

    [Fact]
    public void La_provenance_est_dans_les_credits()
    {
        string credits = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "docs", "CREDITS.md"));
        Assert.Contains("Écran titre vivant", credits);
        Assert.Contains("EdsInterface/Titre", credits);
    }
}
