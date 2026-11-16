using EdS.Domain.Time;
using EdS.Runtime;

namespace EdS.Data.Tests;

/// <summary>Sous-lot V2.29.2 : panoramas peints de l'horizon (calage en azimut et en angle, saison et fondu, tailles, fichiers).</summary>
[Trait("Category", "Sector")]
public class V2292PanoramaTests
{
    [Fact]
    public void Azimut_nord_a_zero_est_au_quart_raccord_a_un_tour()
    {
        Assert.Equal(0.0, HorizonPanorama.U(0));
        Assert.Equal(4096.0, HorizonPanorama.U(90) * HorizonPanorama.ReferenceWidth, 6);
        Assert.Equal(8192.0, HorizonPanorama.U(180) * HorizonPanorama.ReferenceWidth, 6);
        Assert.Equal(12288.0, HorizonPanorama.U(270) * HorizonPanorama.ReferenceWidth, 6);
        Assert.Equal(HorizonPanorama.U(0), HorizonPanorama.U(360), 12);
        Assert.Equal(HorizonPanorama.U(350), HorizonPanorama.U(-10), 12);
        Assert.Equal(245.0, HorizonPanorama.Azimuth(HorizonPanorama.U(245)), 9);   // la mer, à l'ouest
        Assert.Equal(45.0, HorizonPanorama.Azimuth(HorizonPanorama.U(45)), 9);     // les Crêtes de Fer
        Assert.Equal(HorizonProfile.SeaAt, HorizonPanorama.Azimuth(HorizonPanorama.U(HorizonProfile.SeaAt)), 9);
    }

    [Fact]
    public void Angle_horizon_a_200_px_du_bas_45_51_px_par_degre()
    {
        Assert.Equal(0.1953125, HorizonPanorama.HorizonV, 12);
        Assert.Equal(HorizonPanorama.HorizonV, HorizonPanorama.V(0), 12);
        Assert.Equal(0.0, HorizonPanorama.V(HorizonPanorama.BottomAngle), 12);
        Assert.Equal(1.0, HorizonPanorama.V(HorizonPanorama.TopAngle), 12);
        Assert.Equal(22.5, HorizonPanorama.TopAngle - HorizonPanorama.BottomAngle, 9);
        // Les Crêtes de Fer montent à 8,5° : à 200 + 8,5 × 45,51 px du bas.
        Assert.Equal(200 + 8.5 * 45.511111111, HorizonPanorama.V(8.5) * HorizonPanorama.ReferenceHeight, 6);
        Assert.Equal(8.5, HorizonPanorama.AngleOf(HorizonPanorama.V(8.5)), 9);
        // Mêmes fractions sur la version téléphone (horizon à 50 px du bas de 256) et PC (100 px de 512).
        Assert.Equal(HorizonPanorama.HorizonV, 50 / 256.0, 12);
        Assert.Equal(HorizonPanorama.HorizonV, 100 / 512.0, 12);
    }

    [Theory]
    [InlineData(0.0, 6000.0)]
    [InlineData(2.0, 5520.0)]
    [InlineData(120.0, 1000.0)]
    [InlineData(850.0, 12000.0)]
    public void Angle_vu_juste_quels_que_soient_la_hauteur_de_camera_et_le_plan_lointain(double cameraY, double radius)
    {
        foreach (double angle in new[] { 0.0, 0.8, 2.0, 8.5, HorizonPanorama.TopAngle })
        {
            double y = HorizonPanorama.WorldY(angle, cameraY, radius);
            double seen = HorizonPanorama.SeenAngle(y, cameraY, radius);
            // Angle vu = angle peint, abaissé seulement de l'horizon qui s'abaisse avec l'altitude (atan(tan − a/6000)).
            double expected = Math.Atan(Math.Tan(angle * Math.PI / 180) - cameraY / HorizonPanorama.ReferenceRadius) * 180 / Math.PI;
            Assert.Equal(expected, seen, 9);
        }
        // Au sol, l'horizon (angle 0) est à l'horizontale de l'œil.
        Assert.Equal(0.0, HorizonPanorama.SeenAngle(HorizonPanorama.WorldY(0, 0, radius), 0, radius), 9);
        // Le même angle peint donne le même angle vu à deux plans lointains différents.
        Assert.Equal(HorizonPanorama.SeenAngle(HorizonPanorama.WorldY(8.5, cameraY, 900), cameraY, 900),
            HorizonPanorama.SeenAngle(HorizonPanorama.WorldY(8.5, cameraY, 3000), cameraY, 3000), 9);
    }

    [Fact]
    public void Saison_entiere_une_seule_texture()
    {
        for (int s = 0; s < 4; s++)
        {
            PanoramaChoice c = HorizonPanorama.Choose(s, 1f, false);
            Assert.Null(c.Under);
            Assert.Equal(new[] { "spring", "summer", "autumn", "winter" }[s], c.Top);
            Assert.Equal(1f, c.TopAlpha);
            Assert.Equal(0f, c.UnderAlpha);
        }
        Assert.Equal(new[] { "spring", "summer", "autumn", "winter", "reverse" }, HorizonPanorama.Names);
    }

    [Fact]
    public void Fondu_entre_la_saison_d_avant_et_la_nouvelle_pendant_la_transition()
    {
        PanoramaChoice start = HorizonPanorama.Choose(0, 0f, false);   // le 1er mars à 0 h : encore l'hiver
        Assert.Equal("winter", start.Under);
        Assert.Equal("spring", start.Top);
        Assert.Equal(1f, start.UnderAlpha);
        Assert.Equal(0f, start.TopAlpha);
        PanoramaChoice mid = HorizonPanorama.Choose(1, 0.5f, false);
        Assert.Equal("spring", mid.Under);
        Assert.Equal("summer", mid.Top);
        Assert.Equal(1f, mid.UnderAlpha);
        Assert.Equal(0.5f, mid.TopAlpha);
        PanoramaChoice late = HorizonPanorama.Choose(2, 0.9f, false);
        Assert.Equal(0.2f, late.UnderAlpha, 5);
        Assert.Equal(0.9f, late.TopAlpha, 5);
        // L'ensemble ne s'éclaircit jamais : l'opacité cumulée reste au moins celle du dessus et monte jusqu'à 1.
        float last = 0f;
        for (int i = 0; i <= 20; i++)
        {
            PanoramaChoice c = HorizonPanorama.Choose(3, i / 20f, false);
            Assert.InRange(c.TopAlpha, last, 1f);
            Assert.InRange(c.UnderAlpha, 0f, 1f);
            float cover = c.TopAlpha + c.UnderAlpha * (1 - c.TopAlpha);
            Assert.True(cover >= 0.75f - 1e-6f, "couverture " + cover);
            last = c.TopAlpha;
        }
        Assert.Equal("autumn", HorizonPanorama.Choose(3, 0.3f, false).Under);
        Assert.Equal("winter", HorizonPanorama.Choose(0, 0.3f, false).Under);   // printemps : l'hiver d'avant
        Assert.Equal(3, HorizonPanorama.Previous(0));
    }

    [Fact]
    public void Fondu_suit_le_calendrier_des_saisons_du_cur()
    {
        // 1er juin 0 h : tout l'été n'est pas encore là ; 8 jours plus tard : l'été seul (7 jours de transition).
        SeasonLook day0 = SeasonLook.Of(SeasonCalendar.At(new CivilDate(2027, 6, 1)));
        PanoramaChoice a = HorizonPanorama.Choose(day0.Season, day0.Blend, false);
        Assert.Equal("spring", a.Under);
        Assert.Equal("summer", a.Top);
        Assert.Equal(0f, a.TopAlpha);
        SeasonLook day3 = SeasonLook.Of(SeasonCalendar.At(new CivilDate(2027, 6, 4)));
        PanoramaChoice b = HorizonPanorama.Choose(day3.Season, day3.Blend, false);
        Assert.InRange(b.TopAlpha, 0.2f, 0.8f);
        SeasonLook day7 = SeasonLook.Of(SeasonCalendar.At(new CivilDate(2027, 6, 8)));
        Assert.Null(HorizonPanorama.Choose(day7.Season, day7.Blend, false).Under);
        // Le 1er décembre : l'hiver vient de l'automne ; en janvier, l'hiver entier.
        SeasonLook dec = SeasonLook.Of(SeasonCalendar.At(new CivilDate(2027, 12, 1)));
        Assert.Equal("autumn", HorizonPanorama.Choose(dec.Season, dec.Blend, false).Under);
        SeasonLook jan = SeasonLook.Of(SeasonCalendar.At(new CivilDate(2028, 1, 15)));
        PanoramaChoice w = HorizonPanorama.Choose(jan.Season, jan.Blend, false);
        Assert.Equal("winter", w.Top);
        Assert.Null(w.Under);
    }

    [Fact]
    public void Envers_seul_sans_fondu_et_jamais_par_defaut()
    {
        PanoramaChoice r = HorizonPanorama.Choose(2, 0.4f, true);
        Assert.Equal(HorizonPanorama.Reverse, r.Top);
        Assert.Null(r.Under);
        Assert.Equal(1f, r.TopAlpha);
        for (int s = 0; s < 4; s++) Assert.NotEqual(HorizonPanorama.Reverse, HorizonPanorama.Choose(s, 0.6f, false).Top);
        Assert.Throws<ArgumentOutOfRangeException>(() => HorizonPanorama.Choose(4, 1f, false));
    }

    [Fact]
    public void Tailles_selon_la_machine_jamais_16384()
    {
        Assert.Equal(4096, HorizonPanorama.Width(DeviceTier.Phone));
        Assert.Equal(256, HorizonPanorama.Height(DeviceTier.Phone));
        Assert.Equal(8192, HorizonPanorama.Width(DeviceTier.Modest));
        Assert.Equal(8192, HorizonPanorama.Width(DeviceTier.Full));
        Assert.Equal(512, HorizonPanorama.Height(DeviceTier.Full));
        Assert.Equal("EdsHorizon/horizon_spring_8192", HorizonPanorama.ResourcePath("spring", DeviceTier.Full));
        Assert.Equal("EdsHorizon/horizon_reverse_4096", HorizonPanorama.ResourcePath("reverse", DeviceTier.Phone));
    }

    [Fact]
    public void Fichiers_livres_aux_bonnes_dimensions_et_avec_transparence()
    {
        string dir = Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Interface", "Resources", "EdsHorizon");
        foreach (DeviceTier tier in new[] { DeviceTier.Phone, DeviceTier.Full })
        {
            foreach (string name in HorizonPanorama.Names)
            {
                string file = Path.Combine(dir, Path.GetFileName(HorizonPanorama.ResourcePath(name, tier)) + ".png");
                Assert.True(File.Exists(file), file);
                byte[] head = new byte[26];
                using (FileStream f = File.OpenRead(file)) Assert.Equal(26, f.Read(head, 0, 26));
                Assert.Equal(0x89, head[0]);
                Assert.Equal((byte)'P', head[1]);
                Assert.Equal(HorizonPanorama.Width(tier), (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19]);
                Assert.Equal(HorizonPanorama.Height(tier), (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23]);
                Assert.Equal(8, head[24]);    // 8 bits par canal
                Assert.Equal(6, head[25]);    // RVBA : le ciel du jeu reste visible derrière
                (int w, int h, byte[] px) = DecodeRgba(file);
                Assert.Equal(HorizonPanorama.Width(tier), w);
                Assert.Equal(HorizonPanorama.Height(tier), h);
                for (int x = 0; x < w; x++)
                {
                    Assert.Equal(0, px[x * 4 + 3]);                                   // le haut : ciel transparent
                    Assert.True(px[((h - 1) * w + x) * 4 + 3] >= 250, file + " bas opaque, colonne " + x);   // le bas : terrain plein
                }
                for (int y = 0; y < h; y++)   // raccord : la dernière colonne rejoint la première
                    for (int c = 0; c < 4; c++)
                        Assert.InRange(Math.Abs(px[(y * w) * 4 + c] - px[(y * w + w - 1) * 4 + c]), 0, 24);
            }
        }
        Assert.Empty(Directory.GetFiles(dir, "*16384*"));
        Assert.Equal(10, Directory.GetFiles(dir, "*.png").Length);
    }

    /// <summary>Décodeur PNG minimal (RVBA 8 bits, sans entrelacement) : largeur, hauteur et octets RVBA.</summary>
    private static (int, int, byte[]) DecodeRgba(string file)
    {
        byte[] d = File.ReadAllBytes(file);
        int w = 0, h = 0, pos = 8;
        var idat = new MemoryStream();
        while (pos < d.Length)
        {
            int len = (d[pos] << 24) | (d[pos + 1] << 16) | (d[pos + 2] << 8) | d[pos + 3];
            string type = System.Text.Encoding.ASCII.GetString(d, pos + 4, 4);
            if (type == "IHDR")
            {
                w = (d[pos + 8] << 24) | (d[pos + 9] << 16) | (d[pos + 10] << 8) | d[pos + 11];
                h = (d[pos + 12] << 24) | (d[pos + 13] << 16) | (d[pos + 14] << 8) | d[pos + 15];
                Assert.Equal(0, d[pos + 8 + 12]);   // pas d'entrelacement
            }
            else if (type == "IDAT") idat.Write(d, pos + 8, len);
            pos += 12 + len;
        }
        idat.Position = 0;
        int stride = w * 4;
        var raw = new byte[(stride + 1) * h];
        using (var z = new System.IO.Compression.ZLibStream(idat, System.IO.Compression.CompressionMode.Decompress))
        {
            int read = 0;
            while (read < raw.Length)
            {
                int n = z.Read(raw, read, raw.Length - read);
                if (n <= 0) break;
                read += n;
            }
            Assert.Equal(raw.Length, read);
        }
        var px = new byte[stride * h];
        for (int y = 0; y < h; y++)
        {
            int filter = raw[y * (stride + 1)], src = y * (stride + 1) + 1, row = y * stride;
            for (int i = 0; i < stride; i++)
            {
                int a = i >= 4 ? px[row + i - 4] : 0, b = y > 0 ? px[row - stride + i] : 0, c = i >= 4 && y > 0 ? px[row - stride + i - 4] : 0;
                int pr = filter switch
                {
                    0 => 0,
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    _ => Paeth(a, b, c),
                };
                px[row + i] = (byte)(raw[src + i] + pr);
            }
        }
        return (w, h, px);
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
