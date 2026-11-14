using EdS.Data.Json;
using EdS.Data.Story;
using EdS.Runtime;

namespace EdS.Data.Tests;

/// <summary>
/// Interface V2.1 (dossier du 12 novembre 2026, page 20 « HUD &amp; commandes de combat ») : disposition du HUD et des commandes au doigt
/// (<see cref="HudLayoutV21"/>) selon le format, tailles des zones tactiles sur les écrans visés, zone sûre, textes présents dans les
/// données (<see cref="HudTextsV21"/>), et mise à jour sans allocation.
/// </summary>
[Trait("Category", "Runtime")]
public class InterfaceV21Tests
{
    /// <summary>Format : largeur du canevas (1 080 de haut), hauteur réelle de l'écran en pixels et densité (pixels par pouce).</summary>
    public sealed record Format(string Name, float CanvasWidth, float ScreenHeight, float Dpi);

    private static readonly Format Note8 = new("Galaxy Note8 (18,5:9)", 1080f * 2960f / 1440f, 1440f, 522f);

    private static readonly Format[] Phones =
    {
        Note8,
        new("téléphone 16:9 (1080p, 5,5 pouces)", 1920f, 1080f, 401f),
        new("téléphone 20:9", 2400f, 1080f, 400f),
        new("tablette 4:3 (iPad 10,2 pouces)", 1440f, 1620f, 264f),
    };

    private static readonly float[] PcWidths = { 1920f, 1728f, 2520f, 1440f };   // 16:9, 16:10, 21:9, 4:3

    private static readonly HudMode[] Modes = { HudMode.Training, HudMode.Network, HudMode.Adventure };

    private static readonly UiInsets Notch = new(90f, 20f, 60f, 40f);

    private static readonly HudButton[] Combat =
    {
        HudButton.Parry, HudButton.Guard, HudButton.Skill1, HudButton.Skill2, HudButton.Skill3, HudButton.Skill4, HudButton.Skill5, HudButton.Skill6,
    };

    private static readonly HudPart[] Panels =
    {
        HudPart.Vitals, HudPart.Signal, HudPart.Target, HudPart.Compass, HudPart.MiniMap, HudPart.Effects, HudPart.Quests, HudPart.Prompt,
    };

    private static float Millimetres(Format f, float canvas) => canvas * f.ScreenHeight / 1080f / f.Dpi * 25.4f;

    private static IEnumerable<(Format F, HudMode Mode, UiInsets Safe, HudLayoutV21 L)> PhoneLayouts()
    {
        foreach (Format f in Phones)
            foreach (HudMode mode in Modes)
                foreach (UiInsets safe in new[] { UiInsets.None, Notch })
                {
                    var layout = new HudLayoutV21();
                    layout.Update(f.CanvasWidth, 1080f, safe, true, mode);
                    yield return (f, mode, safe, layout);
                }
    }

    private static IEnumerable<HudButton> Used(HudLayoutV21 l) => Enum.GetValues<HudButton>().Where(l.Has);

    [Fact]
    public void Telephone_tout_tient_dans_la_zone_sure_sans_rien_pousser_dehors()
    {
        foreach (var (f, mode, safe, l) in PhoneLayouts())
        {
            string where = $"{f.Name}, {mode}, encoche {safe.Left}";
            Assert.False(l.Overflow, where);
            foreach (HudButton b in Used(l)) Assert.True(l.Button(b).Inside(l.Safe), $"{where} : {b} {l.Button(b)} hors de {l.Safe}");
            foreach (HudPart p in Enum.GetValues<HudPart>())
                if (!l[p].IsEmpty) Assert.True(l[p].Inside(l.Safe), $"{where} : {p} {l[p]} hors de {l.Safe}");
            if (!l.Drawer.IsEmpty) Assert.True(l.Drawer.Inside(l.Safe), $"{where} : tiroir {l.Drawer}");
        }
    }

    [Fact]
    public void Telephone_les_boutons_ne_se_touchent_pas_et_gardent_8_unites_d_ecart()
    {
        foreach (var (f, mode, _, l) in PhoneLayouts())
        {
            HudButton[] row = Used(l).Where(b => !l.InDrawer(b) && b != HudButton.Map).ToArray();
            float gap = HudLayoutV21.Spacing * l.Unit - 0.05f;
            for (int i = 0; i < row.Length; i++)
                for (int j = i + 1; j < row.Length; j++)
                    Assert.False(l.Button(row[i]).Overlaps(l.Button(row[j]), gap), $"{f.Name}, {mode} : {row[i]} {l.Button(row[i])} et {row[j]} {l.Button(row[j])}");
            HudButton[] drawer = Used(l).Where(l.InDrawer).ToArray();
            for (int i = 0; i < drawer.Length; i++)
                for (int j = i + 1; j < drawer.Length; j++)
                    Assert.False(l.Button(drawer[i]).Overlaps(l.Button(drawer[j]), gap), $"{f.Name}, {mode} : tiroir {drawer[i]} et {drawer[j]}");
            // Le tiroir ouvert ne cache aucune commande de combat ni le joystick.
            foreach (HudButton b in row.Where(b => b != HudButton.More && !IsUtility(b)))
                Assert.False(l.Drawer.Overlaps(l.Button(b)), $"{f.Name}, {mode} : le tiroir cache {b}");
            Assert.False(l.Drawer.Overlaps(l[HudPart.Joystick]), $"{f.Name}, {mode} : le tiroir cache le joystick");
        }
    }

    private static bool IsUtility(HudButton b) => b >= HudButton.Pause && b <= HudButton.More;

    [Fact]
    public void Telephone_les_boutons_ne_cachent_ni_les_barres_ni_la_cible_ni_la_carte_ni_le_joystick()
    {
        foreach (var (f, mode, _, l) in PhoneLayouts())
            foreach (HudButton b in Used(l).Where(b => !l.InDrawer(b) && b != HudButton.Map))
            {
                foreach (HudPart p in Panels)
                    Assert.False(l.Button(b).Overlaps(l[p]), $"{f.Name}, {mode} : {b} {l.Button(b)} cache {p} {l[p]}");
                Assert.False(l.Button(b).Overlaps(l[HudPart.Joystick]), $"{f.Name}, {mode} : {b} sur le joystick");
            }
    }

    [Fact]
    public void Telephone_les_panneaux_du_haut_ne_se_chevauchent_pas()
    {
        HudPart[] top = { HudPart.Vitals, HudPart.Signal, HudPart.Target, HudPart.Compass, HudPart.MiniMap, HudPart.Effects, HudPart.Quests };
        foreach (var (f, mode, _, l) in PhoneLayouts())
            for (int i = 0; i < top.Length; i++)
                for (int j = i + 1; j < top.Length; j++)
                    Assert.False(l[top[i]].Overlaps(l[top[j]]), $"{f.Name}, {mode} : {top[i]} {l[top[i]]} et {top[j]} {l[top[j]]}");
    }

    [Fact]
    public void Tailles_de_la_charte_en_unites_et_assez_grandes_pour_les_pouces()
    {
        foreach (var (f, mode, _, l) in PhoneLayouts())
        {
            foreach (HudButton b in Used(l).Where(b => b != HudButton.Map))
            {
                UiBox box = l.Button(b);
                float minUnits = Combat.Contains(b) ? HudLayoutV21.CombatMin : HudLayoutV21.TouchMin;
                Assert.True(box.W >= minUnits * l.Unit - 0.01f && box.H >= minUnits * l.Unit - 0.01f, $"{f.Name}, {mode} : {b} {box} sous {minUnits} unités");
                Assert.True(Millimetres(f, Math.Min(box.W, box.H)) >= 7f, $"{f.Name} : {b} fait {Millimetres(f, box.H):0.0} mm");
            }
            Assert.Equal(HudLayoutV21.JoystickZone * l.Unit, l[HudPart.Joystick].W, 3);
        }
        // Sur le Note8 : les commandes de combat font au moins 9 mm (comme au sous-lot 11.8), les autres au moins 7,5 mm.
        var note8 = new HudLayoutV21();
        note8.Update(Note8.CanvasWidth, 1080f, UiInsets.None, true, HudMode.Adventure);
        Assert.Equal(2.5f, note8.Unit, 3);
        foreach (HudButton b in Combat) Assert.True(Millimetres(Note8, note8.Button(b).H) >= 9f, $"{b} : {Millimetres(Note8, note8.Button(b).H):0.0} mm");
        foreach (HudButton b in Used(note8).Where(b => b != HudButton.Map)) Assert.True(Millimetres(Note8, note8.Button(b).H) >= 7.5f, b.ToString());
    }

    [Fact]
    public void Formats_etroits_le_16_9_garde_l_unite_et_le_4_3_la_baisse_pour_garder_768_unites()
    {
        var l = new HudLayoutV21();
        l.Update(1920f, 1080f, UiInsets.None, true, HudMode.Adventure);   // 16:9
        Assert.Equal(768f, l.DesignWidth, 2);
        Assert.Equal(2.5f, l.Unit, 3);
        l.Update(1440f, 1080f, UiInsets.None, true, HudMode.Adventure);   // 4:3
        Assert.Equal(1.875f, l.Unit, 3);
        Assert.Equal(576f, l.DesignHeight, 2);
        Assert.Equal(72f, l.Button(HudButton.Parry).W / l.Unit, 2);
        l.Update(2400f, 1080f, UiInsets.None, true, HudMode.Adventure);   // 20:9 : la maquette telle quelle
        Assert.Equal(960f, l.DesignWidth, 2);
        Assert.Equal(new UiBox(32f * 2.5f, 16f * 2.5f, 226f * 2.5f, 54f * 2.5f), l[HudPart.Vitals]);
        Assert.Equal((824f + 104f) * 2.5f, l[HudPart.MiniMap].Right, 2);
        Assert.Equal(480f * 2.5f, l[HudPart.Target].CenterX, 2);
    }

    [Fact]
    public void Rangee_utile_et_tiroir_chaque_bouton_du_mode_a_sa_place()
    {
        foreach (var (f, mode, _, l) in PhoneLayouts())
        {
            Assert.True(l.UtilityCount > 0);
            for (int i = 0; i < l.UtilityCount; i++) Assert.True(l.Has(l.UtilityAt(i)), $"{f.Name}, {mode} : {l.UtilityAt(i)} sans place");
            int inRow = Enumerable.Range(0, l.UtilityCount).Count(i => !l.InDrawer(l.UtilityAt(i)));
            Assert.True(inRow >= 1, $"{f.Name}, {mode}");
            Assert.Equal(l.Has(HudButton.More), !l.Drawer.IsEmpty);
            Assert.True(l.UtilityCount - inRow <= HudLayoutV21.MaxDrawer);
            // La pause (ou le menu en réseau) reste toujours dans la rangée, hors du tiroir.
            Assert.False(l.InDrawer(mode == HudMode.Network ? HudButton.Menu : HudButton.Pause), $"{f.Name}, {mode}");
        }
        var adventure = new HudLayoutV21();
        adventure.Update(Note8.CanvasWidth, 1080f, UiInsets.None, true, HudMode.Adventure);
        Assert.True(adventure.Has(HudButton.Potion) && adventure.Has(HudButton.Act) && adventure.Has(HudButton.Map) && !adventure.Has(HudButton.Ally));
        var network = new HudLayoutV21();
        network.Update(Note8.CanvasWidth, 1080f, UiInsets.None, true, HudMode.Network);
        Assert.True(network.Has(HudButton.Ally) && !network.Has(HudButton.Potion) && !network.Has(HudButton.Bag) && !network.Has(HudButton.Pause));
    }

    [Fact]
    public void PC_maquette_cotee_et_formats_larges_ou_etroits()
    {
        var l = new HudLayoutV21();
        l.Update(1920f, 1080f, UiInsets.None, false, HudMode.Adventure);
        Assert.Equal(new UiBox(64f, 64f, 400f, 100f), l[HudPart.Vitals]);
        Assert.Equal(new UiBox(712f, 64f, 496f, 128f), l[HudPart.Target]);
        Assert.Equal(new UiBox(1616f, 64f, 240f, 200f), l[HudPart.MiniMap]);
        Assert.Equal(new UiBox(1456f, 300f, 400f, 210f), l[HudPart.Quests]);
        Assert.False(l.Has(HudButton.Parry));   // pas de commandes au doigt sur PC
        HudPart[] panels = { HudPart.Vitals, HudPart.Signal, HudPart.Target, HudPart.Compass, HudPart.MiniMap, HudPart.Effects, HudPart.Quests, HudPart.Toasts, HudPart.Prompt };
        foreach (float w in PcWidths)
        {
            l.Update(w, 1080f, UiInsets.None, false, HudMode.Adventure);
            Assert.False(l.Overflow, $"PC {w}");
            for (int i = 0; i < panels.Length; i++)
            {
                Assert.True(l[panels[i]].Inside(l.Safe), $"PC {w} : {panels[i]}");
                for (int j = i + 1; j < panels.Length; j++)
                    Assert.False(l[panels[i]].Overlaps(l[panels[j]]), $"PC {w} : {panels[i]} {l[panels[i]]} et {panels[j]} {l[panels[j]]}");
            }
            for (int s = 0; s < 7; s++)
            {
                Assert.True(l.Skill(s).Inside(l.Safe), $"PC {w} : compétence {s}");
                Assert.False(l.Skill(s).Overlaps(l[HudPart.Prompt]), $"PC {w} : compétence {s} sous l'indication");
                if (s > 0) Assert.False(l.Skill(s).Overlaps(l.Skill(s - 1), 15.9f), $"PC {w} : compétences {s - 1} et {s}");
            }
        }
    }

    [Fact]
    public void Textes_assez_grands_et_libelles_qui_tiennent_dans_leur_bouton()
    {
        TextCatalog texts = Texts();
        foreach (var (f, mode, _, l) in PhoneLayouts())
        {
            // Charte : 16 unités au moins pour l'essentiel, 14 pour les métadonnées ; au moins 2 mm de hauteur de corps sur l'écran.
            Assert.True(l.Font(HudFont.Essential) >= 16f * l.Unit - 0.01f);
            Assert.True(Millimetres(f, l.Font(HudFont.Meta)) >= 2f, $"{f.Name} : {Millimetres(f, l.Font(HudFont.Meta)):0.00} mm");
            foreach (HudButton b in Used(l))
            {
                string? key = HudTextsV21.Button(b, mode);
                if (key == null) continue;
                string label = texts.Get(key);
                float width = label.Length * 0.5f * l.LabelFont(l.Button(b));   // Barlow Condensed : moins d'une demi-capitale par lettre
                Assert.True(width <= l.Button(b).W - 4f * l.Unit, $"{f.Name}, {mode} : « {label} » ({width:0}) déborde de {b} ({l.Button(b).W:0})");
            }
        }
        var pc = new HudLayoutV21();
        Assert.Equal((32f, 30f, 28f, 24f, 22f), (pc.Font(HudFont.Title), pc.Font(HudFont.Body), pc.Font(HudFont.Button), pc.Font(HudFont.Essential), pc.Font(HudFont.Meta)));
    }

    [Fact]
    public void Les_textes_du_HUD_viennent_des_donnees_et_sont_tous_presents()
    {
        TextCatalog texts = Texts();
        string[] keys = HudTextsV21.AllKeys();
        Assert.True(keys.Length >= 35, keys.Length.ToString());
        Assert.Equal(keys.Length, keys.Distinct().Count());
        foreach (string key in keys)
        {
            Assert.True(texts.Has(key), $"texte manquant : {key}");
            Assert.False(string.IsNullOrWhiteSpace(texts.Get(key)), key);
        }
        Assert.Equal(8, HudTextsV21.Compass.Length);
        Assert.Contains("{0}", texts.Get(HudTextsV21.ParryIn));
        Assert.Equal("Halte", texts.Get(HudTextsV21.Button(HudButton.Restart, HudMode.Adventure)!));
        Assert.Equal("Rejouer", texts.Get(HudTextsV21.Button(HudButton.Restart, HudMode.Training)!));
    }

    [Fact]
    public void Disposition_refaite_seulement_quand_le_format_change_et_sans_allocation()
    {
        var l = new HudLayoutV21();
        l.Update(2220f, 1080f, UiInsets.None, true, HudMode.Adventure);
        int version = l.Version;
        l.Update(2220f, 1080f, UiInsets.None, true, HudMode.Adventure);
        Assert.Equal(version, l.Version);
        l.Update(2220f, 1080f, Notch, true, HudMode.Adventure);
        Assert.Equal(version + 1, l.Version);
        // Chaque image : même appel, aucune allocation ; changement de format (rotation, simulateur) : aucune allocation non plus.
        l.Update(1920f, 1080f, UiInsets.None, false, HudMode.Network);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++)
        {
            l.Update(2220f, 1080f, UiInsets.None, true, HudMode.Adventure);
            l.Update(2220f, 1080f, UiInsets.None, true, HudMode.Adventure);
            l.Update(1920f, 1080f, Notch, i % 2 == 0, (HudMode)(i % 3));
            _ = l[HudPart.Target];
            _ = l.Button(HudButton.Parry);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Throws<ArgumentOutOfRangeException>(() => l.Update(0f, 1080f, UiInsets.None, true, HudMode.Adventure));
    }

    private static TextCatalog Texts()
    {
        TextsLoadResult loaded = StoryJsonReader.ReadTextsFile(StoryJsonReader.TextsPathIn(ScenarioTestData.DataDirectory));
        Assert.True(loaded.Report.IsValid, loaded.Report.ToString());
        return loaded.Texts!;
    }
}
