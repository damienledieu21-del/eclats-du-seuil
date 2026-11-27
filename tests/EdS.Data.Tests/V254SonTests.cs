using EdS.Runtime;

namespace EdS.Data.Tests;

/// <summary>
/// V2.54 (passe son des Vallons, demande de Damien) : règles pures du son (milieu, musique d'un lieu et d'un moment, replis, fondus, nappes,
/// pas selon le sol, mixage, limite de voix) et sons de synthèse joués sans fichier.
/// </summary>
public sealed class V254SonTests
{
    private const int Rate = 22050;

    [Theory]
    [InlineData("egouts_collecteur", SoundBiome.Sewer)]
    [InlineData("egouts_citerne_bete", SoundBiome.Sewer)]
    [InlineData("crypte_abbaye_gardien", SoundBiome.Crypt)]
    [InlineData("galeries_eclats", SoundBiome.Gallery)]
    [InlineData("galeries_seuil_ours", SoundBiome.Gallery)]
    [InlineData("mine_filons", SoundBiome.Gallery)]
    [InlineData("grottes_noyees_bassins", SoundBiome.Drowned)]
    [InlineData("grottes_noyees_epaves_echo", SoundBiome.Drowned)]
    [InlineData("grotte_cascade", SoundBiome.Cave)]
    [InlineData("envers_vallons", SoundBiome.Envers)]
    [InlineData("vallons", SoundBiome.Open)]
    public void Le_milieu_vient_de_la_zone(string zone, SoundBiome expected) =>
        Assert.Equal(expected, SoundRules.BiomeOf(zone, false, false, false, AmbiencePlace.None, null));

    [Fact]
    public void Le_milieu_du_dehors_vient_des_lieux()
    {
        Assert.Equal(SoundBiome.Envers, SoundRules.BiomeOf("vallons", false, false, true, AmbiencePlace.Town, null));
        Assert.Equal(SoundBiome.Indoors, SoundRules.BiomeOf("auberge", true, false, false, AmbiencePlace.TavernInside, null));
        Assert.Equal(SoundBiome.Cave, SoundRules.BiomeOf("dessous_inconnu", false, true, false, AmbiencePlace.None, null));
        Assert.Equal(SoundBiome.Marsh, SoundRules.BiomeOf("vallons", false, false, false, AmbiencePlace.Fields, new[] { "cabanes_sur_pilotis" }));
        Assert.Equal(SoundBiome.Heights, SoundRules.BiomeOf("vallons", false, false, false, AmbiencePlace.None, new[] { "col_des_cretes_de_fer" }));
        Assert.Equal(SoundBiome.Heights, SoundRules.BiomeOf("vallons", false, false, false, AmbiencePlace.None, new[] { "hautes_collines" }));
        Assert.Equal(SoundBiome.Town, SoundRules.BiomeOf("vallons", false, false, false, AmbiencePlace.Town, new[] { "place_du_bourg" }));
        Assert.Equal(SoundBiome.Forest, SoundRules.BiomeOf("vallons", false, false, false, AmbiencePlace.Forest, new[] { "foret_des_loups" }));
        Assert.Equal(SoundBiome.Lake, SoundRules.BiomeOf("vallons", false, false, false, AmbiencePlace.Lake | AmbiencePlace.Fields, new[] { "rives_du_lac" }));
        // Mots entiers : « collecte » n'est pas un col, « colline » seule non plus, « placement » n'est pas un étang.
        Assert.False(SoundRules.IsHeights("collecte_du_ble"));
        Assert.False(SoundRules.IsHeights("collines_basses"));
        Assert.True(SoundRules.IsMarsh("marais_du_sud"));
        Assert.False(SoundRules.IsMarsh(null));
        Assert.Equal(SoundBiome.Open, SoundRules.BiomeOf(null, false, false, false, AmbiencePlace.None, null));
    }

    [Fact]
    public void La_musique_suit_le_lieu_et_le_moment()
    {
        // Le Gardien et les scènes l'emportent partout.
        Assert.Equal(StyleMusic.Boss, SoundRules.Music(StyleMusic.Boss, SoundBiome.Sewer, 1, true));
        Assert.Equal(StyleMusic.Story, SoundRules.Music(StyleMusic.Story, SoundBiome.Envers, 0, false));
        // Combat : sous terre et dans l'Envers, le combat des souterrains.
        Assert.Equal(StyleMusic.Combat, SoundRules.Music(StyleMusic.Combat, SoundBiome.Open, 0, false));
        Assert.Equal(StyleMusic.CombatDungeon, SoundRules.Music(StyleMusic.Combat, SoundBiome.Gallery, 0, false));
        Assert.Equal(StyleMusic.CombatDungeon, SoundRules.Music(StyleMusic.Combat, SoundBiome.Envers, 0, false));
        // Chaque souterrain a sa musique.
        Assert.Equal(StyleMusic.Sewers, SoundRules.Music(StyleMusic.Exploration, SoundBiome.Sewer, 0, false));
        Assert.Equal(StyleMusic.Crypt, SoundRules.Music(StyleMusic.Exploration, SoundBiome.Crypt, 0, false));
        Assert.Equal(StyleMusic.Galleries, SoundRules.Music(StyleMusic.Exploration, SoundBiome.Gallery, 0, false));
        Assert.Equal(StyleMusic.Drowned, SoundRules.Music(StyleMusic.Exploration, SoundBiome.Drowned, 0, false));
        Assert.Equal(StyleMusic.Caves, SoundRules.Music(StyleMusic.Exploration, SoundBiome.Cave, 0, false));
        Assert.Equal(StyleMusic.Envers, SoundRules.Music(StyleMusic.Threshold, SoundBiome.Envers, 0, false));
        // L'exploration la nuit, puis en hiver ; le bourg, l'auberge et les fêtes restent.
        Assert.Equal(StyleMusic.ExplorationNight, SoundRules.Music(StyleMusic.Exploration, SoundBiome.Open, 0.6, true));
        Assert.Equal(StyleMusic.ExplorationWinter, SoundRules.Music(StyleMusic.Exploration, SoundBiome.Forest, 0.2, true));
        Assert.Equal(StyleMusic.Exploration, SoundRules.Music(StyleMusic.Exploration, SoundBiome.Open, double.NaN, false));
        Assert.Equal(StyleMusic.VillageNight, SoundRules.Music(StyleMusic.VillageNight, SoundBiome.Town, 1, true));
        Assert.Equal(StyleMusic.Feast, SoundRules.Music(StyleMusic.Feast, SoundBiome.Town, 0, false));
        Assert.Equal(StyleMusic.Threshold, SoundRules.Music(StyleMusic.Threshold, SoundBiome.Open, 1, true));
    }

    [Fact]
    public void Une_musique_absente_prend_son_repli_sans_boucle()
    {
        Assert.Equal(StyleMusic.Crypt, SoundRules.MusicChain(StyleMusic.Drowned, m => m == StyleMusic.Crypt));
        Assert.Equal(StyleMusic.Caves, SoundRules.MusicChain(StyleMusic.Galleries, m => m == StyleMusic.Caves || m == StyleMusic.Crypt));
        Assert.Equal(StyleMusic.Threshold, SoundRules.MusicChain(StyleMusic.Envers, m => m == StyleMusic.Threshold));
        Assert.Equal(StyleMusic.Combat, SoundRules.MusicChain(StyleMusic.CombatDungeon, m => m == StyleMusic.Combat));
        Assert.Equal(StyleMusic.Exploration, SoundRules.MusicChain(StyleMusic.ExplorationNight, m => m == StyleMusic.Exploration));
        Assert.Equal(StyleMusic.Village, SoundRules.MusicChain(StyleMusic.Feast, m => m == StyleMusic.Village));
        Assert.Null(SoundRules.MusicChain(StyleMusic.Sewers, _ => false));
        // Les replis d'avant ne changent pas.
        foreach (StyleMusic m in Enum.GetValues<StyleMusic>().Where(m => m <= StyleMusic.Dance))
            Assert.Equal(StyleRules.MusicFallback(m), SoundRules.MusicFallback(m));
        Assert.Throws<ArgumentNullException>(() => SoundRules.MusicChain(StyleMusic.Combat, null!));
    }

    [Fact]
    public void Les_fondus_sont_courts_vers_le_combat_et_longs_vers_l_Envers()
    {
        Assert.Equal(1, SoundRules.FadeSeconds(StyleMusic.Exploration, StyleMusic.Combat));
        Assert.Equal(1, SoundRules.FadeSeconds(null, StyleMusic.CombatDungeon));
        Assert.Equal(4, SoundRules.FadeSeconds(StyleMusic.Boss, StyleMusic.Exploration));
        Assert.Equal(5, SoundRules.FadeSeconds(StyleMusic.Exploration, StyleMusic.Envers));
        Assert.Equal(3, SoundRules.FadeSeconds(StyleMusic.Exploration, StyleMusic.ExplorationNight));
        Assert.Equal(2.5, SoundRules.FadeSeconds(StyleMusic.Village, StyleMusic.Inn));
    }

    [Fact]
    public void Les_nappes_des_milieux_suivent_le_milieu()
    {
        Assert.True(SoundRules.BedVolume(StyleAmbience.Sewer, SoundBiome.Sewer, false, false, 0, 0) > 0);
        Assert.Equal(0, SoundRules.BedVolume(StyleAmbience.Sewer, SoundBiome.Cave, false, false, 0, 0));
        Assert.True(SoundRules.BedVolume(StyleAmbience.Cave, SoundBiome.Cave, true, false, 0, 0) < SoundRules.BedVolume(StyleAmbience.Cave, SoundBiome.Cave, false, false, 0, 0));
        Assert.True(SoundRules.BedVolume(StyleAmbience.Gusts, SoundBiome.Heights, false, false, 0, 1) > SoundRules.BedVolume(StyleAmbience.Gusts, SoundBiome.Heights, false, false, 0, 0));
        Assert.Equal(0.8, SoundRules.BedVolume(StyleAmbience.Envers, SoundBiome.Envers, true, false, 0, 0));
        // Sous l'eau, seule la nappe sous l'eau joue.
        Assert.Equal(0, SoundRules.BedVolume(StyleAmbience.Drowned, SoundBiome.Drowned, false, true, 0, 0));
        Assert.True(SoundRules.BedVolume(StyleAmbience.Underwater, SoundBiome.Drowned, false, true, 0, 0) > 0);
        Assert.Equal(0, SoundRules.BedVolume(StyleAmbience.Underwater, SoundBiome.Drowned, false, false, 0, 0));
        Assert.Equal(0, SoundRules.BedVolume(StyleAmbience.Birds, SoundBiome.Open, false, false, 0, 0));
        foreach (StyleAmbience a in Enum.GetValues<StyleAmbience>())
            foreach (SoundBiome b in Enum.GetValues<SoundBiome>())
                foreach (double v in new[] { double.NaN, -1, 0, 0.5, 1, 9 })
                {
                    Assert.InRange(SoundRules.BedVolume(a, b, v > 0.5, false, v, v), 0, 1);
                    Assert.InRange(SoundRules.BedVolume(a, b, false, true, v, v), 0, 1);
                }
        Assert.True(SoundRules.MutesWorld(SoundBiome.Envers, false));
        Assert.True(SoundRules.MutesWorld(SoundBiome.Sewer, false));
        Assert.False(SoundRules.MutesWorld(SoundBiome.Crypt, false));
        Assert.False(SoundRules.MutesWorld(SoundBiome.Forest, false));
        Assert.True(SoundRules.MutesWorld(SoundBiome.Open, true));
        Assert.Equal(new[] { StyleAmbience.Marsh, StyleAmbience.Cave, StyleAmbience.Sewer, StyleAmbience.Gallery, StyleAmbience.Drowned, StyleAmbience.Envers,
            StyleAmbience.Underwater, StyleAmbience.Gusts }, Enum.GetValues<StyleAmbience>().Where(SoundRules.IsBed));
        // Les nappes de lieu d'avant ne jouent pas les nouvelles (elles passent par BedVolume).
        foreach (StyleAmbience a in Enum.GetValues<StyleAmbience>().Where(SoundRules.IsBed))
            Assert.Equal(0, StyleRules.PlaceVolume(a, (AmbiencePlace)0x3FF, 720, 1, false, false, 0, 0));
    }

    [Fact]
    public void Le_pas_suit_le_sol()
    {
        Assert.Equal(StyleSound.StepWater, SoundRules.Step(StyleSound.StepGrass, SoundBiome.Drowned, 0, false));
        Assert.Equal(StyleSound.StepWater, SoundRules.Step(StyleSound.StepGrass, SoundBiome.Open, 0, true));
        Assert.Equal(StyleSound.StepStone, SoundRules.Step(StyleSound.StepGrass, SoundBiome.Sewer, 0, false));
        Assert.Equal(StyleSound.StepStone, SoundRules.Step(StyleSound.StepGrass, SoundBiome.Envers, 1, false));
        Assert.Equal(StyleSound.StepWood, SoundRules.Step(StyleSound.StepGrass, SoundBiome.Indoors, 1, false));
        Assert.Equal(StyleSound.StepMud, SoundRules.Step(StyleSound.StepGrass, SoundBiome.Marsh, 0, false));
        Assert.Equal(StyleSound.StepSnow, SoundRules.Step(StyleSound.StepGravel, SoundBiome.Heights, 0.7, false));
        Assert.Equal(StyleSound.StepWood, SoundRules.Step(StyleSound.StepWood, SoundBiome.Open, 0.9, false));   // un ponton sous la neige
        Assert.Equal(StyleSound.StepLeaves, SoundRules.Step(StyleSound.StepLeaves, SoundBiome.Forest, double.NaN, false));
        Assert.True(SoundRules.IsStep(StyleSound.StepMud));
        Assert.False(SoundRules.IsStep(StyleSound.Hit));
    }

    [Fact]
    public void Le_mixage_a_quatre_volumes_bornes()
    {
        Assert.Equal(70, SoundRules.DefaultPercent(SoundBus.Music));
        Assert.Equal(90, SoundRules.DefaultPercent(SoundBus.Effects));
        Assert.Equal(100, SoundRules.StepPercent(100, 1));
        Assert.Equal(0, SoundRules.StepPercent(0, -1));
        Assert.Equal(60, SoundRules.StepPercent(70, -1));
        Assert.Equal(50, SoundRules.ClampPercent(47));
        Assert.Equal(0, SoundRules.ClampPercent(int.MinValue));
        Assert.Equal(100, SoundRules.ClampPercent(int.MaxValue));
        Assert.Equal(0, SoundRules.Gain(0));
        Assert.Equal(1, SoundRules.Gain(100));
        Assert.Equal(0.25, SoundRules.Gain(50), 6);
        Assert.Equal(SoundBus.Music, SoundRules.NextBus(SoundBus.Interface));
        Assert.Equal("Ambiance : 80 %", SoundRules.Label(SoundBus.Ambience, 80));
    }

    [Fact]
    public void La_limite_de_voix_protege_le_PC_modeste()
    {
        Assert.Equal(16, SoundRules.VoiceLimit(DeviceTier.Phone));
        Assert.Equal(24, SoundRules.VoiceLimit(DeviceTier.Modest));
        Assert.True(SoundRules.Admit(SoundPriority.Low, 17, 24));
        Assert.False(SoundRules.Admit(SoundPriority.Low, 18, 24));
        Assert.True(SoundRules.Admit(SoundPriority.Normal, 23, 24));
        Assert.False(SoundRules.Admit(SoundPriority.Normal, 24, 24));
        Assert.True(SoundRules.Admit(SoundPriority.High, 500, 24));
        Assert.True(SoundRules.Admit(SoundPriority.Low, 0, 0));
        Assert.Equal(SoundPriority.Low, SoundRules.PriorityOf(StyleSound.StepGrass));
        Assert.Equal(SoundPriority.Low, SoundRules.PriorityOf(StyleSound.Howl));
        Assert.Equal(SoundPriority.High, SoundRules.PriorityOf(StyleSound.Death));
        Assert.Equal(SoundPriority.Normal, SoundRules.PriorityOf(StyleSound.Hit));
        Assert.True(SoundRules.MinGap(StyleSound.StepStone) > SoundRules.MinGap(StyleSound.Hit));
    }

    [Fact]
    public void Les_nappes_de_synthese_bouclent_sans_claquer()
    {
        foreach (StyleAmbience kind in Enum.GetValues<StyleAmbience>().Where(SoundSynth.HasBed))
        {
            float[] a = SoundSynth.Bed(kind, Rate, 54);
            Assert.Equal(a, SoundSynth.Bed(kind, Rate, 54));   // toujours la même
            Assert.All(a, v => Assert.True(!float.IsNaN(v) && Math.Abs(v) <= 1f));
            Assert.True(Rms(a) > 0.02, kind + " est muette");
            // La couture : le dernier échantillon et le premier sont proches (pas de claquement).
            Assert.True(Math.Abs(a[^1] - a[0]) < 0.25, kind + " claque à la couture");
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => SoundSynth.Bed(StyleAmbience.Birds, Rate, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SoundSynth.Bed(StyleAmbience.Cave, 100, 1));
    }

    [Fact]
    public void Les_bruitages_de_synthese_sont_courts_bornes_et_varies()
    {
        int count = 0;
        foreach (StyleSound sound in Enum.GetValues<StyleSound>().Where(SoundSynth.HasCue))
        {
            count++;
            float[] a = SoundSynth.Cue(sound, Rate, 0), b = SoundSynth.Cue(sound, Rate, 1);
            Assert.Equal(a, SoundSynth.Cue(sound, Rate, 0));
            Assert.InRange(a.Length, Rate / 50, Rate * 3 / 2);
            Assert.All(a, v => Assert.True(!float.IsNaN(v) && Math.Abs(v) <= 1f));
            Assert.True(Rms(a) > 0.01, sound + " est muet");
            Assert.NotEqual(a, b);   // deux variantes ne sont pas identiques
        }
        Assert.True(count >= 25);
        // Tous les pas, y compris ceux de la passe son, ont un repli synthétique.
        foreach (StyleSound s in Enum.GetValues<StyleSound>().Where(SoundRules.IsStep)) Assert.True(SoundSynth.HasCue(s));
        Assert.Throws<ArgumentOutOfRangeException>(() => SoundSynth.Cue(StyleSound.Howl, Rate, 0));
    }

    [Fact]
    public void Les_fichiers_nommes_de_SONS_md_sont_reconnus()
    {
        const string root = "Assets/Style/";
        Assert.Equal(StyleMusic.ExplorationNight, StyleRules.MusicOf(root + "Music/PasseSon/eds_musique_exploration_nuit.ogg"));
        Assert.Equal(StyleMusic.ExplorationWinter, StyleRules.MusicOf(root + "Music/PasseSon/eds_musique_exploration_hiver.ogg"));
        Assert.Equal(StyleMusic.Sewers, StyleRules.MusicOf(root + "Music/PasseSon/eds_musique_egouts.ogg"));
        Assert.Equal(StyleMusic.Caves, StyleRules.MusicOf(root + "Music/PasseSon/eds_musique_grottes.ogg"));
        Assert.Equal(StyleMusic.Galleries, StyleRules.MusicOf(root + "Music/PasseSon/eds_musique_galeries.ogg"));
        Assert.Equal(StyleMusic.Drowned, StyleRules.MusicOf(root + "Music/PasseSon/eds_musique_noyees.ogg"));
        Assert.Equal(StyleMusic.Envers, StyleRules.MusicOf(root + "Music/PasseSon/eds_musique_envers.ogg"));
        Assert.Equal(StyleMusic.CombatDungeon, StyleRules.MusicOf(root + "Music/PasseSon/eds_musique_combat_donjon.ogg"));
        // Les musiques déjà déposées gardent leur sens.
        Assert.Equal(StyleMusic.Exploration, StyleRules.MusicOf(root + "Music/ScottBuckley/Into The Wilds.ogg"));
        Assert.Equal(StyleMusic.Combat, StyleRules.MusicOf(root + "Music/combat_theme.ogg"));
        Assert.Equal(StyleMusic.Crypt, StyleRules.MusicOf(root + "Music/ScottBuckley2/The Long Dark.ogg"));
        foreach ((string file, StyleAmbience kind) in new[]
                 {
                     ("eds_nappe_marais", StyleAmbience.Marsh), ("eds_nappe_grotte", StyleAmbience.Cave), ("eds_nappe_egouts", StyleAmbience.Sewer),
                     ("eds_nappe_galeries", StyleAmbience.Gallery), ("eds_nappe_noyees", StyleAmbience.Drowned), ("eds_nappe_envers", StyleAmbience.Envers),
                     ("eds_nappe_sous_eau", StyleAmbience.Underwater), ("eds_nappe_rafales", StyleAmbience.Gusts),
                 })
            Assert.Equal(kind, StyleRules.AmbienceOf(root + "Ambience/PasseSon/" + file + ".ogg"));
        var sounds = new[]
        {
            root + "Sounds/PasseSon/eds_pas_pierre_01.ogg", root + "Sounds/PasseSon/eds_pas_eau_01.ogg", root + "Sounds/PasseSon/eds_pas_neige_01.ogg",
            root + "Sounds/PasseSon/eds_pas_boue_01.ogg", root + "Sounds/PasseSon/eds_coffre_01.ogg", root + "Sounds/PasseSon/eds_porte_ouvre_01.ogg",
            root + "Sounds/PasseSon/eds_plongee_01.ogg", root + "Sounds/PasseSon/eds_remontee_01.ogg", root + "Sounds/PasseSon/eds_blesse_01.ogg",
        };
        StyleSound[] expected = { StyleSound.StepStone, StyleSound.StepWater, StyleSound.StepSnow, StyleSound.StepMud, StyleSound.ChestOpen, StyleSound.DoorOpen,
            StyleSound.Dive, StyleSound.Emerge, StyleSound.Hurt };
        for (int i = 0; i < expected.Length; i++) Assert.Equal(sounds[i], StyleRules.PickSound(sounds, expected[i]));
        Assert.Equal(StyleAmbience.Crypt, StyleRules.AmbienceOf(root + "Ambience/PasseSon/eds_nappe_crypte_dungeonair.ogg"));
        // Les remplaçants de docs/SONS.md passent devant les sons déjà déposés, et la faille qui s'ouvre n'est pas son bourdonnement.
        var replacing = new[]
        {
            root + "Sounds/Kenney/impactPunch_medium_000.ogg", root + "Sounds/PasseSon/eds_coup_epee_01.ogg", root + "Sounds/PasseSon/eds_bouclier_01.ogg",
            root + "Sounds/PasseSon/eds_parade_01.ogg", root + "Sounds/PasseSon/eds_chute_01.ogg", root + "Sounds/PasseSon/eds_elan_01.ogg",
            root + "Sounds/PasseSon/eds_sort_01.ogg", root + "Sounds/PasseSon/eds_pieces_01.ogg", root + "Sounds/PasseSon/eds_faille_ouvre_01.ogg",
            root + "Sounds/PasseSon/eds_faille_ferme_01.ogg", root + "Sounds/Animaux/faille_bourdon.ogg", root + "Sounds/PasseSon/eds_ecole_feu_01.ogg",
        };
        Assert.EndsWith("eds_coup_epee_01.ogg", StyleRules.PickSound(replacing, StyleSound.Hit));
        Assert.EndsWith("eds_bouclier_01.ogg", StyleRules.PickSound(replacing, StyleSound.Block));
        Assert.EndsWith("eds_parade_01.ogg", StyleRules.PickSound(replacing, StyleSound.Parry));
        Assert.EndsWith("eds_chute_01.ogg", StyleRules.PickSound(replacing, StyleSound.Death));
        Assert.EndsWith("eds_elan_01.ogg", StyleRules.PickSound(replacing, StyleSound.Swing));
        Assert.EndsWith("eds_sort_01.ogg", StyleRules.PickSound(replacing, StyleSound.Cast));
        Assert.EndsWith("eds_pieces_01.ogg", StyleRules.PickSound(replacing, StyleSound.Coin));
        Assert.EndsWith("eds_faille_ouvre_01.ogg", StyleRules.PickSound(replacing, StyleSound.Glass));
        Assert.EndsWith("eds_faille_ferme_01.ogg", StyleRules.PickSound(replacing, StyleSound.Shatter));
        Assert.EndsWith("faille_bourdon.ogg", StyleRules.PickSound(replacing, StyleSound.RiftHum));
        // Chaque musique et chaque ambiance du manifeste de l'essai a son entrée.
        Assert.Equal(Enum.GetValues<StyleMusic>().Length, EssaiManifest.Music.Count);
        Assert.Equal(Enum.GetValues<StyleAmbience>().Length, EssaiManifest.Ambiences.Count);
    }

    [Fact]
    public void Le_gain_est_neutre_au_volume_par_defaut()
    {
        // Revue de la PR #267 : qui n'a rien réglé entend le jeu comme avant (gain 1 à la valeur par défaut de chaque canal).
        foreach (SoundBus bus in Enum.GetValues<SoundBus>())
        {
            Assert.Equal(1, SoundRules.BusGain(bus, SoundRules.DefaultPercent(bus)), 9);
            Assert.Equal(0, SoundRules.BusGain(bus, 0));
            Assert.True(SoundRules.BusGain(bus, 100) > 1);
            Assert.True(SoundRules.BusGain(bus, 100) <= SoundRules.MaxGain);
            double last = -1;
            for (int p = 0; p <= 100; p += 10)
            {
                double g = SoundRules.BusGain(bus, p);
                Assert.True(g > last || (g == last && g == SoundRules.MaxGain));
                last = g;
            }
        }
        Assert.Equal(0.25, SoundRules.Gain(40, 80), 9);
        Assert.Equal(SoundRules.MaxGain, SoundRules.Gain(100, 50));
        Assert.Equal(SoundRules.MaxGain, SoundRules.Gain(10, 0));
        Assert.Equal(0, SoundRules.Gain(0, 0));
    }

    [Fact]
    public void Les_gues_et_le_deversoir_font_des_pas_dans_l_eau()
    {
        Assert.True(SoundRules.IsWading(new[] { "gue_de_la_vallee" }));
        Assert.True(SoundRules.IsWading(new[] { "place", "deversoir" }));
        Assert.True(SoundRules.IsWading(new[] { "rigole_est" }));
        Assert.False(SoundRules.IsWading(new[] { "guerite_du_fort", "guet", "tour_de_guet" }));
        Assert.False(SoundRules.IsWading(null));
        Assert.False(SoundRules.IsWading(Array.Empty<string>()));
        Assert.Equal(StyleSound.StepWater, SoundRules.Step(StyleSound.StepGravel, SoundBiome.Sewer, 0, SoundRules.IsWading(new[] { "deversoir" })));
    }

    private static double Rms(float[] a) => Math.Sqrt(a.Sum(v => (double)v * v) / a.Length);
}
