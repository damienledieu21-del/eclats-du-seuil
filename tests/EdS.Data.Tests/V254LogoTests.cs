using EdS.Runtime;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.54 (sujet 13, logo définitif) : durées et phases de l'ouverture du logo, choix de la version, chemins et dimensions des images
/// livrées. Sans Unity. Affichage seulement : rien ici ne touche à la partie.
/// </summary>
[Trait("Category", "Runtime")]
public sealed class V254LogoTests
{
    private static string Resources => Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Interface", "Resources");

    private static (int W, int H) PngSize(string file)
    {
        byte[] b = File.ReadAllBytes(file);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, b[..4]);
        int W = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19], H = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
        return (W, H);
    }

    [Fact]
    public void Les_durees_se_suivent_et_l_ouverture_de_la_felure_dure_entre_une_et_deux_secondes()
    {
        Assert.InRange(LogoIntro.CrackEnd - LogoIntro.CrackStart, 1f, 2f);
        Assert.True(LogoIntro.FadeInSeconds <= LogoIntro.CrackStart);
        Assert.True(LogoIntro.CrackEnd <= LogoIntro.MottoStart);
        Assert.True(LogoIntro.MottoStart < LogoIntro.MottoEnd && LogoIntro.MottoEnd < LogoIntro.OutStart);
        Assert.True(LogoIntro.OutStart < LogoIntro.TotalSeconds);
        Assert.True(LogoIntro.TotalSeconds < 6f);
        Assert.True(LogoIntro.SkipAfter < LogoIntro.CrackStart);
    }

    [Fact]
    public void La_felure_se_revele_de_0_a_1_sans_reculer()
    {
        Assert.Equal(0f, LogoIntro.CrackReveal(0f));
        Assert.Equal(0f, LogoIntro.CrackReveal(LogoIntro.CrackStart));
        Assert.Equal(1f, LogoIntro.CrackReveal(LogoIntro.CrackEnd));
        Assert.Equal(1f, LogoIntro.CrackReveal(99f));
        float middle = LogoIntro.CrackReveal((LogoIntro.CrackStart + LogoIntro.CrackEnd) / 2f);
        Assert.InRange(middle, 0.49f, 0.51f);
        float last = 0f;
        for (float t = 0f; t <= LogoIntro.TotalSeconds; t += 0.01f)
        {
            float r = LogoIntro.CrackReveal(t);
            Assert.InRange(r, last, 1f);
            last = r;
        }
        Assert.True(LogoIntro.CrackTop > 0f && LogoIntro.CrackTop < LogoIntro.CrackBottom && LogoIntro.CrackBottom < 1f);
    }

    [Fact]
    public void L_opacite_entre_reste_pleine_puis_sort_et_vaut_zero_hors_de_l_ouverture()
    {
        Assert.Equal(0f, LogoIntro.Overall(-0.1f));
        Assert.Equal(0f, LogoIntro.Overall(0f));
        Assert.Equal(1f, LogoIntro.Overall(LogoIntro.FadeInSeconds));
        Assert.Equal(1f, LogoIntro.Overall(LogoIntro.OutStart));
        Assert.InRange(LogoIntro.Overall((LogoIntro.OutStart + LogoIntro.TotalSeconds) / 2f), 0.49f, 0.51f);
        Assert.Equal(0f, LogoIntro.Overall(LogoIntro.TotalSeconds));
        Assert.True(LogoIntro.Active(0f) && LogoIntro.Active(LogoIntro.TotalSeconds - 0.01f));
        Assert.False(LogoIntro.Active(LogoIntro.TotalSeconds) || LogoIntro.Active(-1f));
    }

    [Fact]
    public void La_lueur_pulse_doucement_sans_saut_ni_extinction()
    {
        Assert.Equal(1f, LogoIntro.Glow(0f));
        Assert.Equal(1f, LogoIntro.Glow(LogoIntro.CrackEnd));
        float low = 1f;
        for (float t = LogoIntro.CrackEnd; t < LogoIntro.TotalSeconds; t += 0.005f)
        {
            float g = LogoIntro.Glow(t);
            Assert.InRange(g, 1f - LogoIntro.PulseDepth - 0.0001f, 1f);
            Assert.True(System.Math.Abs(g - LogoIntro.Glow(t + 0.005f)) < 0.01f);
            low = System.Math.Min(low, g);
        }
        Assert.InRange(low, 1f - LogoIntro.PulseDepth, 1f - LogoIntro.PulseDepth + 0.01f);
    }

    [Fact]
    public void La_devise_apparait_apres_l_ouverture_et_un_geste_saute_a_la_fin_apres_un_court_delai()
    {
        Assert.Equal(0f, LogoIntro.MottoOpacity(LogoIntro.MottoStart));
        Assert.Equal(1f, LogoIntro.MottoOpacity(LogoIntro.MottoEnd));
        Assert.Equal(0f, LogoIntro.MottoOpacity(0f));
        Assert.Equal(1f, LogoIntro.MottoOpacity(LogoIntro.TotalSeconds));
        Assert.False(LogoIntro.CanSkip(LogoIntro.SkipAfter - 0.01f));
        Assert.True(LogoIntro.CanSkip(LogoIntro.SkipAfter));
        Assert.True(LogoIntro.CanSkip(LogoIntro.OutStart - 0.01f));
        Assert.False(LogoIntro.CanSkip(LogoIntro.OutStart));
    }

    [Fact]
    public void Le_rattrapage_apres_un_geste_est_continu_sans_saut_d_opacite_et_s_arrete_au_fondu_de_sortie()
    {
        Assert.Equal(1f + 0.016f, LogoIntro.Advance(1f, 0.016f, false), 5);
        Assert.Equal(1f + 0.016f * LogoIntro.SkipSpeed, LogoIntro.Advance(1f, 0.016f, true), 5);
        Assert.Equal(LogoIntro.OutStart, LogoIntro.Advance(LogoIntro.OutStart - 0.001f, 0.016f, true));
        Assert.Equal(LogoIntro.OutStart + 0.016f, LogoIntro.Advance(LogoIntro.OutStart, 0.016f, true), 5);
        // À 60 images par seconde, de n'importe quel instant jusqu'à la fin : aucune des opacités ne bouge de plus de 0,15 d'une image à l'autre.
        for (float start = LogoIntro.SkipAfter; start < LogoIntro.OutStart; start += 0.37f)
        {
            float t = start;
            while (LogoIntro.Active(t))
            {
                float next = LogoIntro.Advance(t, 1f / 60f, true);
                Assert.True(System.Math.Abs(LogoIntro.Overall(next) - LogoIntro.Overall(t)) < 0.15f);
                Assert.True(System.Math.Abs(LogoIntro.CrackReveal(next) - LogoIntro.CrackReveal(t)) < 0.15f);
                Assert.True(System.Math.Abs(LogoIntro.MottoOpacity(next) - LogoIntro.MottoOpacity(t)) < 0.15f);
                t = next;
            }
        }
    }

    [Fact]
    public void Le_choix_de_version_depend_du_fond_et_de_la_taille()
    {
        Assert.Equal(LogoVersion.Motto, LogoIntro.Fixed(false, 1920f));
        Assert.Equal(LogoVersion.Motto, LogoIntro.Fixed(false, LogoIntro.MottoMinWidth));
        Assert.Equal(LogoVersion.Complete, LogoIntro.Fixed(false, LogoIntro.MottoMinWidth - 1f));
        Assert.Equal(LogoVersion.Complete, LogoIntro.Fixed(false, LogoIntro.WhiteMaxWidth));
        Assert.Equal(LogoVersion.White, LogoIntro.Fixed(false, LogoIntro.WhiteMaxWidth - 1f));
        Assert.Equal(LogoVersion.White, LogoIntro.Fixed(true, 1920f));
        Assert.True(LogoIntro.ShowsMotto(1280f));
        Assert.False(LogoIntro.ShowsMotto(400f));
    }

    [Fact]
    public void Les_chemins_suivent_la_taille_de_la_machine()
    {
        Assert.Equal("EdsInterface/Logo/2048/logo_06_devise", LogoIntro.ResourcePath(LogoVersion.Motto, DeviceTier.Full));
        Assert.Equal("EdsInterface/Logo/1024/logo_06_devise", LogoIntro.ResourcePath(LogoVersion.Motto, DeviceTier.Phone));
        Assert.Equal("EdsInterface/Logo/1024/logo_02_sans_felure", LogoIntro.ResourcePath(LogoVersion.Plain, DeviceTier.Modest));
        Assert.Equal("EdsInterface/Logo/2048/logo_03_felure", LogoIntro.ResourcePath(LogoVersion.Crack, DeviceTier.Full));
        Assert.Equal("EdsInterface/Logo/2048/logo_04_blanc", LogoIntro.ResourcePath(LogoVersion.White, DeviceTier.Full));
        Assert.Equal("EdsInterface/Logo/2048/logo_01_complet", LogoIntro.ResourcePath(LogoVersion.Complete, DeviceTier.Full));
        Assert.Throws<ArgumentOutOfRangeException>(() => LogoIntro.ResourcePath((LogoVersion)99, DeviceTier.Full));
    }

    [Fact]
    public void Les_cinq_logos_existent_en_deux_tailles_au_bon_format_et_l_icone_fait_1024()
    {
        foreach (LogoVersion version in new[] { LogoVersion.Complete, LogoVersion.Plain, LogoVersion.Crack, LogoVersion.White, LogoVersion.Motto })
            foreach (DeviceTier tier in new[] { DeviceTier.Full, DeviceTier.Phone })
            {
                string file = Path.Combine(Resources, LogoIntro.ResourcePath(version, tier) + ".png");
                Assert.True(File.Exists(file), file);
                (int w, int h) = PngSize(file);
                Assert.Equal(tier == DeviceTier.Full ? (2048, 1024) : (1024, 512), (w, h));
                Assert.True(new FileInfo(file).Length < 2_000_000, file);   // jamais le 4 096 × 2 048 d'origine
            }
        string icon = Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Icone", "eds_icone_1024.png");
        Assert.Equal((1024, 1024), PngSize(icon));
    }

    [Fact]
    public void La_provenance_est_dans_les_credits()
    {
        string credits = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "docs", "CREDITS.md"));
        Assert.Contains("Logo définitif", credits);
        Assert.Contains("Interface/Resources/EdsInterface/Logo", credits);
    }
}

[Trait("Category", "Runtime")]
public sealed class V254InputLockTests
{
    private sealed class Component { }

    [Fact]
    public void Le_verrou_coupe_une_fois_et_rend_exactement_le_composant_coupe_meme_s_il_n_est_plus_le_courant()
    {
        var gate = new InputLock<Component>();
        var events = new Component();
        Assert.Null(gate.Lock(null));          // pas encore de composant : on réessaiera
        Assert.False(gate.Held);
        Assert.Same(events, gate.Lock(events));
        Assert.True(gate.Held);
        Assert.Null(gate.Lock(events));        // une seule coupure
        Assert.Null(gate.Lock(null));          // le composant coupé n'est plus « le courant » : on garde la référence
        Assert.Same(events, gate.Unlock());    // et c'est lui qu'on rend
        Assert.False(gate.Held);
        Assert.Null(gate.Unlock());
        var other = new Component();
        Assert.Same(other, gate.Lock(other));  // un nouveau cycle (écran titre) coupe de nouveau
        Assert.Same(other, gate.Unlock());
    }
}
