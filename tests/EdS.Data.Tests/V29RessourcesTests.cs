using System;
using System.Linq;
using EdS.Runtime;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.9 (« vrais modèles et sons ») : reconnaissance des modèles et des sons déposés par Damien (noms réels du 7 novembre 2026),
/// cerfs à bois et biches, voix des bêtes (passages, rythme, variantes), allure du sanglier sans squelette. Affichage seulement.
/// </summary>
[Trait("Category", "Runtime")]
public sealed class V29RessourcesTests
{
    private static readonly string[] Models =
    {
        "Assets/Style/Animals/Quaternius/Wolf_Quaternius.fbx",
        "Assets/Style/Animals/Quaternius/Stag_Quaternius.fbx",
        "Assets/Style/Animals/Quaternius/Deer_Quaternius.fbx",
        "Assets/Style/Sketchfab/Animaux/Rabbit_Pneshik.fbx",
        "Assets/Style/Sketchfab/Animaux/Boar_Hanabi.fbx",
        "Assets/Style/Sketchfab/Animaux/Cat_Stripe_DreamNoms.fbx",
        "Assets/Style/LowPoly/Animals/FBX/Wolf.fbx",
        "Assets/Style/Farm/FBX/Pig.fbx",
        "Assets/Style/PolyHaven/catapult_wheel.fbx",
        "Assets/Style/Sketchfab/Batiments/Maison/location_house.fbx",
    };

    private static readonly string[] Sounds =
    {
        "Assets/Style/Sounds/Animaux/eds_miaou_1_BSB1889.ogg", "Assets/Style/Sounds/Animaux/eds_miaou_2_BSB1890.ogg",
        "Assets/Style/Sounds/Animaux/eds_ronron_BSB1010.ogg", "Assets/Style/Sounds/Animaux/eds_cloche_eglise_BSB0830.ogg",
        "Assets/Style/Sounds/Animaux/eds_cloche_BSB0135.ogg", "Assets/Style/Sounds/Animaux/eds_verre_eclate_BSB0148.ogg",
        "Assets/Style/Sounds/Animaux/eds_miroir_explose_BSB0387.ogg", "Assets/Style/Sounds/Animaux/eds_loups_hurlement_FS158780_Paresh.ogg",
        "Assets/Style/Sounds/Animaux/eds_loups_hurlement_FS762242_SachaJulien.ogg", "Assets/Style/Sounds/Animaux/eds_cerf_brame_FS757773_naturenotesuk.ogg",
        "Assets/Style/Sounds/Animaux/eds_sanglier_grogne_FS764944_Mastersoundboy2005.ogg", "Assets/Style/Sounds/Animaux/eds_lievre_herbe_FS635012_JiggleSticks.ogg",
        "Assets/Style/Sounds/Animaux/eds_faille_bourdon_FS670641_SnowFightStudios.ogg",
        "Assets/Style/Sounds/Kenney/impactGlass_heavy_000.ogg", "Assets/Style/Sounds/Kenney/impactPunch_medium_000.ogg",
        "Assets/Style/Sounds/creatures/wolf_growl_1.ogg",
    };

    [Fact]
    public void Les_modeles_deposes_sont_reconnus_par_leur_nom()
    {
        Assert.Equal("Assets/Style/Animals/Quaternius/Wolf_Quaternius.fbx", StyleRules.Candidates(Models, StyleSlot.Wolf).First());
        Assert.Equal(new[] { "Assets/Style/Animals/Quaternius/Stag_Quaternius.fbx" }, StyleRules.Candidates(Models, StyleSlot.Stag));
        Assert.Equal(new[] { "Assets/Style/Animals/Quaternius/Deer_Quaternius.fbx" }, StyleRules.Candidates(Models, StyleSlot.Doe));
        Assert.Equal(new[] { "Assets/Style/Sketchfab/Animaux/Rabbit_Pneshik.fbx" }, StyleRules.Candidates(Models, StyleSlot.Hare));
        Assert.Equal(new[] { "Assets/Style/Sketchfab/Animaux/Cat_Stripe_DreamNoms.fbx" }, StyleRules.Candidates(Models, StyleSlot.Cat));
        // Le sanglier de Sketchfab (réaliste) passe devant le cochon du premier pack, même sans animation.
        Assert.Equal("Assets/Style/Sketchfab/Animaux/Boar_Hanabi.fbx", StyleRules.Candidates(Models, StyleSlot.Boar).First());
        // Aucune bête n'est prise pour du décor, et « catapult » ou « location » ne sont pas des chats.
        foreach (StyleSlot decor in new[] { StyleSlot.Tree, StyleSlot.Rock, StyleSlot.Plant, StyleSlot.Debris, StyleSlot.Shrub, StyleSlot.Hero })
            Assert.DoesNotContain(StyleRules.Candidates(Models, decor), p => p.Contains("Quaternius") || p.Contains("Animaux"));
        Assert.False(StyleRules.Matches("Assets/Style/x/Scattered_Rocks.fbx", StyleSlot.Cat));
        Assert.True(StyleRules.Matches("Assets/Style/x/chat_roux.fbx", StyleSlot.Cat));
        Assert.False(StyleRules.Matches("Assets/Style/x/Reindeer.fbx", StyleSlot.Doe));
        Assert.Equal(60.7, StyleRules.ModelYaw("Assets/Style/Sketchfab/Animaux/Boar_Hanabi.fbx"));
        Assert.Equal(0, StyleRules.ModelYaw("Assets/Style/Animals/Quaternius/Wolf_Quaternius.fbx"));
    }

    [Fact]
    public void Les_sons_deposes_vont_chacun_a_sa_place()
    {
        Assert.Equal(2, StyleRules.PickSounds(Sounds, StyleSound.Meow, 6).Count);
        Assert.EndsWith("eds_ronron_BSB1010.ogg", StyleRules.PickSound(Sounds, StyleSound.Purr));
        Assert.EndsWith("eds_cloche_BSB0135.ogg", StyleRules.PickSound(Sounds, StyleSound.Tocsin));   // pas la cloche d'église
        Assert.Single(StyleRules.PickSounds(Sounds, StyleSound.Tocsin, 6));
        Assert.EndsWith("eds_cloche_eglise_BSB0830.ogg", StyleRules.PickSound(Sounds, StyleSound.ChurchBell));
        Assert.EndsWith("eds_verre_eclate_BSB0148.ogg", StyleRules.PickSound(Sounds, StyleSound.Glass));   // le verre déposé avant Kenney
        Assert.EndsWith("eds_miroir_explose_BSB0387.ogg", StyleRules.PickSound(Sounds, StyleSound.Shatter));
        Assert.Equal(2, StyleRules.PickSounds(Sounds, StyleSound.Howl, 6).Count);
        Assert.EndsWith("eds_cerf_brame_FS757773_naturenotesuk.ogg", StyleRules.PickSound(Sounds, StyleSound.Bellow));
        Assert.EndsWith("eds_sanglier_grogne_FS764944_Mastersoundboy2005.ogg", StyleRules.PickSound(Sounds, StyleSound.BoarGrumble));
        Assert.EndsWith("eds_lievre_herbe_FS635012_JiggleSticks.ogg", StyleRules.PickSound(Sounds, StyleSound.HareDash));
        Assert.EndsWith("eds_faille_bourdon_FS670641_SnowFightStudios.ogg", StyleRules.PickSound(Sounds, StyleSound.RiftHum));
        // Les sons longs ne remplacent pas les bruitages du combat (un grognement de 42 s n'est pas un cri d'attaque).
        Assert.EndsWith("wolf_growl_1.ogg", StyleRules.PickSound(Sounds, StyleSound.Growl));
        Assert.Null(StyleRules.PickSound(Sounds, StyleSound.Grunt));
        Assert.EndsWith("impactPunch_medium_000.ogg", StyleRules.PickSound(Sounds, StyleSound.Hit));
        foreach (StyleSound other in new[] { StyleSound.Cast, StyleSound.Death, StyleSound.Coin, StyleSound.Swing, StyleSound.Roar, StyleSound.Potion })
            Assert.Null(StyleRules.PickSound(Sounds, other));
    }

    [Fact]
    public void Cerfs_a_bois_et_biches_lievres_et_hauteurs()
    {
        Assert.Equal(StyleSlot.Stag, StyleRules.ForCombatant("cerf1", false));   // le chef de la harde
        Assert.Equal(StyleSlot.Doe, StyleRules.ForCombatant("cerf2", false));
        Assert.Equal(StyleSlot.Doe, StyleRules.ForCombatant("cerf3", false));
        Assert.Equal(StyleSlot.Stag, StyleRules.ForCombatant("cerf4_v2", false));   // une vie suivante garde ses bois
        Assert.Equal(StyleSlot.Hare, StyleRules.ForCombatant("lievre3", false));
        Assert.Equal(StyleSlot.Wolf, StyleRules.ForCombatant("loup_blanc", false));
        Assert.True(StyleRules.BeastHeight("cerf1") > StyleRules.BeastHeight("cerf2"));
        Assert.InRange(StyleRules.BeastHeight("lievre1"), 0.3, 0.7);
        Assert.Equal(0.95, StyleRules.BeastHeight("loup1"));
        Assert.True(StyleRules.IsBeast(StyleSlot.Hare) && StyleRules.IsBeast(StyleSlot.Boar) && !StyleRules.IsBeast(StyleSlot.Cat) && !StyleRules.IsBeast(StyleSlot.Character));
    }

    [Fact]
    public void Les_animations_des_vrais_modeles_sont_trouvees_et_bouclent()
    {
        string[] quaternius =
        {
            "AnimalArmature|Attack_Headbutt", "AnimalArmature|Attack_Kick", "AnimalArmature|Death", "AnimalArmature|Eating", "AnimalArmature|Gallop",
            "AnimalArmature|Gallop_Jump", "AnimalArmature|Idle", "AnimalArmature|Idle_2", "AnimalArmature|Idle_Headlow",
            "AnimalArmature|Idle_HitReact_Left", "AnimalArmature|Idle_HitReact_Right", "AnimalArmature|Jump_toIdle", "AnimalArmature|Walk",
        };
        Assert.Equal("AnimalArmature|Idle", StyleRules.Clip(quaternius, StyleMotion.Idle));
        Assert.Equal("AnimalArmature|Walk", StyleRules.Clip(quaternius, StyleMotion.Walk));
        Assert.Equal("AnimalArmature|Gallop", StyleRules.Clip(quaternius, StyleMotion.Run));   // la fuite : le galop, pas le saut
        Assert.Equal("AnimalArmature|Eating", StyleRules.Clip(quaternius, StyleMotion.Graze));
        Assert.Equal("AnimalArmature|Death", StyleRules.Clip(quaternius, StyleMotion.Death));
        Assert.StartsWith("AnimalArmature|Attack", StyleRules.Clip(quaternius, StyleMotion.Attack));
        Assert.StartsWith("AnimalArmature|Idle_HitReact", StyleRules.Clip(quaternius, StyleMotion.Hurt));
        Assert.True(StyleRules.Loops("AnimalArmature|Eating"));
        Assert.True(StyleRules.Loops("AnimalArmature|Gallop"));
        Assert.False(StyleRules.Loops("AnimalArmature|Idle_HitReact_Left"));
        Assert.False(StyleRules.Loops("AnimalArmature|Death"));
        string[] rabbit = { "Arm_rabbit|dead|Base Layer", "Arm_rabbit|eat|Base Layer", "Arm_rabbit|idle_1|Base Layer", "Arm_rabbit|run|Base Layer", "Arm_rabbit|walk|Base Layer" };
        Assert.Equal("Arm_rabbit|run|Base Layer", StyleRules.Clip(rabbit, StyleMotion.Run));
        Assert.Equal("Arm_rabbit|eat|Base Layer", StyleRules.Clip(rabbit, StyleMotion.Graze));
        Assert.Equal("Arm_rabbit|dead|Base Layer", StyleRules.Clip(rabbit, StyleMotion.Death));
        Assert.Equal("Arm_rabbit|idle_1|Base Layer", StyleRules.Clip(rabbit, StyleMotion.Idle));
        Assert.Equal(StyleMotion.Graze, StyleRules.Motion(0, false, false, false, "graze"));
        Assert.Equal(StyleMotion.Run, StyleRules.Motion(6, false, false, false, "graze"));   // la fuite l'emporte
    }

    [Fact]
    public void Les_passages_des_longs_enregistrements_tombent_sur_la_bete()
    {
        for (int n = 0; n < 40; n++)
        {
            (double start, double length) = BeastCalls.Cue("eds_cerf_brame_FS757773_naturenotesuk", 148.6, n, 6);
            Assert.Contains(start, new[] { 4.25, 32.25, 60.75, 90.5, 109.75, 134.0 });
            Assert.True(start + length <= 148.6);
            (double s2, double l2) = BeastCalls.Cue("son_inconnu", 40, n, 8);
            Assert.InRange(s2, 0, 32);
            Assert.Equal(8, l2);
            Assert.Equal((s2, l2), BeastCalls.Cue("son_inconnu", 40, n, 8));   // toujours le même pour le même n
        }
        Assert.Equal((0, 2.86), BeastCalls.Cue("eds_lievre_herbe", 2.86, 3, 6));   // court : en entier
        Assert.Equal((0, 0), BeastCalls.Cue("x", double.NaN, 0, 6));
        Assert.Equal((0, 0), BeastCalls.Cue("x", 10, 0, 0));
        Assert.Equal(4, Enumerable.Range(0, 60).Select(n => BeastCalls.Cue("eds_loups_hurlement_FS158780_Paresh", 33.3, n, 8).Start).Distinct().Count());
    }

    [Fact]
    public void Rythme_des_voix_variantes_et_moments()
    {
        for (int n = 0; n < 200; n++)
        {
            Assert.InRange(BeastCalls.Interval(StyleSound.Howl, n), 35, 80);
            Assert.InRange(BeastCalls.Interval(StyleSound.Bellow, n), 100, 220);
            Assert.InRange(BeastCalls.Interval(StyleSound.BoarGrumble, n), 9, 22);
            Assert.InRange(BeastCalls.Pick(n, 6), 0, 5);
        }
        Assert.Equal(-1, BeastCalls.Pick(3, 0));
        Assert.Equal(6, Enumerable.Range(0, 60).Select(n => BeastCalls.Pick(n, 6)).Distinct().Count());   // les six miaulements servent
        Assert.NotEqual(Enumerable.Range(0, 6).Select(n => BeastCalls.Pick(n, 6)), Enumerable.Range(0, 6));   // pas dans l'ordre
        Assert.True(BeastCalls.BellowTime(5 * 60 + 10) && BeastCalls.BellowTime(19 * 60 + 10) && BeastCalls.BellowTime(1440 + 300));
        Assert.False(BeastCalls.BellowTime(12 * 60) || BeastCalls.BellowTime(23 * 60) || BeastCalls.BellowTime(-60));
        Assert.True(BeastCalls.HowlTime(true, false) && BeastCalls.HowlTime(false, true) && !BeastCalls.HowlTime(false, false));
        // Le lièvre détale une fois, puis seulement après s'être calmé.
        (bool dash, bool calm) = BeastCalls.HareDash(true, 5);
        Assert.True(dash && !calm);
        (dash, calm) = BeastCalls.HareDash(calm, 6);
        Assert.False(dash);
        (dash, calm) = BeastCalls.HareDash(calm, 0.5);
        Assert.True(calm && !dash);
        Assert.False(BeastCalls.HareDash(true, double.NaN).Dash);
        Assert.Equal(0, BeastCalls.Envelope(-1, 5));
        Assert.Equal(0, BeastCalls.Envelope(5, 5));
        Assert.Equal(1, BeastCalls.Envelope(2, 5));
        Assert.InRange(BeastCalls.Envelope(0.1, 5), 0.3, 0.4);
    }

    [Fact]
    public void Le_sanglier_sans_squelette_se_dandine_fouille_et_reste_borne()
    {
        Assert.Equal((0, 0, 0, 0), BeastCalls.StaticGait(double.NaN, 1, null));
        Assert.Equal((0, 0, 0, 0), BeastCalls.StaticGait(1, double.PositiveInfinity, null));
        double maxRoll = 0, maxPitch = 0, minRootPitch = 99;
        for (int i = 0; i < 400; i++)
        {
            double t = i * 0.05;
            var walk = BeastCalls.StaticGait(1.5, t, null);
            maxRoll = Math.Max(maxRoll, Math.Abs(walk.Roll));
            var trot = BeastCalls.StaticGait(50, t, null);   // vitesse absurde : bornée
            maxPitch = Math.Max(maxPitch, Math.Abs(trot.Pitch));
            Assert.InRange(trot.Bob, 0, 0.05);
            var root = BeastCalls.StaticGait(0, t, "root");
            minRootPitch = Math.Min(minRootPitch, root.Pitch);
            Assert.InRange(BeastCalls.StaticGait(0, t, null).Pitch, 0, 0);   // à l'arrêt, il respire sans piquer du nez
        }
        Assert.InRange(maxRoll, 1.5, 3.1);   // il se dandine au pas
        Assert.InRange(maxPitch, 1, 8);
        Assert.True(minRootPitch > 0);   // il fouille le groin vers le sol
    }
}
