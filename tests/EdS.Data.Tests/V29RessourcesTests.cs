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
        // Les longs enregistrements restent compressés en mémoire (téléphone) ; les miaulements, le verre et le lièvre sont décompressés.
        Assert.Equal(8, Sounds.Count(StyleRules.IsLongRecording));
        Assert.False(StyleRules.IsLongRecording("Assets/Style/Sounds/Animaux/eds_miaou_1_BSB1889.ogg"));
        Assert.False(StyleRules.IsLongRecording("Assets/Style/Ambience/pluie.ogg"));
        Assert.False(StyleRules.IsLongRecording(null!));
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

    // --- 2e vague (7 novembre 2026, au soir) : modèles réalistes convertis, ambiances par lieu, pas, ferme, musiques de Scott Buckley ---

    private static readonly string[] Models2 =
    {
        "Assets/Style/Sketchfab/Animaux2/CerfRouge_Suiteofart.fbx", "Assets/Style/Sketchfab/Animaux2/Elan_WildMesh.fbx",
        "Assets/Style/Sketchfab/Animaux2/Faon_AssetsAnimated.fbx", "Assets/Style/Sketchfab/Animaux2/Sanglier_Dakta.fbx",
        "Assets/Style/Sketchfab/Animaux2/Lievre_bluekiwi.fbx", "Assets/Style/Sketchfab/Animaux2/Loup_rhcreations.fbx",
        "Assets/Style/Sketchfab/Animaux2/Loup_roo3d.fbx", "Assets/Style/Sketchfab/Animaux2/Corbeau_alexanders823.fbx",
        "Assets/Style/Sketchfab/Animaux2/Vache_rukh3d.fbx", "Assets/Style/Sketchfab/Animaux2/Chien_quander.fbx",
        "Assets/Style/Sketchfab/Animaux2/Cheval_henry7.fbx",
    };

    private const string Monde = "Assets/Style/Sounds/Monde/";

    [Fact]
    public void Deuxieme_vague_le_meilleur_modele_par_bete()
    {
        string[] all = Models.Concat(Models2).ToArray();
        Assert.EndsWith("CerfRouge_Suiteofart.fbx", StyleRules.Candidates(all, StyleSlot.Stag).First());
        Assert.EndsWith("Elan_WildMesh.fbx", StyleRules.Candidates(all, StyleSlot.Stag)[1]);
        Assert.EndsWith("Faon_AssetsAnimated.fbx", StyleRules.Candidates(all, StyleSlot.Doe).First());
        Assert.DoesNotContain(StyleRules.Candidates(all, StyleSlot.Doe), p => p.Contains("Cerf") || p.Contains("Elan"));
        Assert.EndsWith("Sanglier_Dakta.fbx", StyleRules.Candidates(all, StyleSlot.Boar).First());
        Assert.EndsWith("Lievre_bluekiwi.fbx", StyleRules.Candidates(all, StyleSlot.Hare).First());
        Assert.EndsWith("Loup_roo3d.fbx", StyleRules.Candidates(all, StyleSlot.Wolf).First());
        Assert.DoesNotContain(StyleRules.Candidates(all, StyleSlot.Wolf), p => p.Contains("rhcreations"));
        Assert.EndsWith("Corbeau_alexanders823.fbx", Assert.Single(StyleRules.Candidates(all, StyleSlot.Crow)));
        Assert.EndsWith("Vache_rukh3d.fbx", Assert.Single(StyleRules.Candidates(all, StyleSlot.Cow)));
        Assert.EndsWith("Chien_quander.fbx", Assert.Single(StyleRules.Candidates(all, StyleSlot.Dog)));
        Assert.EndsWith("Cheval_henry7.fbx", Assert.Single(StyleRules.Candidates(all, StyleSlot.Horse)));
        foreach (StyleSlot decor in new[] { StyleSlot.Tree, StyleSlot.Rock, StyleSlot.Plant, StyleSlot.Debris, StyleSlot.Shrub, StyleSlot.Hero, StyleSlot.Cat })
            Assert.DoesNotContain(StyleRules.Candidates(all, decor), p => p.Contains("Animaux2"));
        // Clips des modèles convertis (noms donnés par Claude à la conversion).
        string[] boar = { "Idle", "Idle_2", "Walk", "Trot", "Run", "Eat", "Dig", "Attack", "Death", "Hurt", "Lie" };
        Assert.Equal("Eat", StyleRules.Clip(boar, StyleMotion.Graze));
        Assert.Equal("Run", StyleRules.Clip(boar, StyleMotion.Run));
        Assert.Equal("Hurt", StyleRules.Clip(boar, StyleMotion.Hurt));
        Assert.Equal("Idle", StyleRules.Clip(boar, StyleMotion.Idle));
        Assert.True(StyleRules.Loops("Dig") && StyleRules.Loops("Trot") && !StyleRules.Loops("Hurt"));
        Assert.Equal(1.35, StyleRules.BeastHeight("cerf2"));
    }

    [Fact]
    public void Deuxieme_vague_ambiances_et_bruitages_du_dossier_Monde()
    {
        Assert.Equal(StyleAmbience.Village, StyleRules.AmbienceOf(Monde + "eds_BSB1346_village_centre.ogg"));
        Assert.Equal(StyleAmbience.Village, StyleRules.AmbienceOf(Monde + "eds_BSB2969_conversations_dehors.ogg"));
        Assert.Equal(StyleAmbience.Village, StyleRules.AmbienceOf(Monde + "eds_FS578072_ramonmineiro_village_medieval.ogg"));
        Assert.Equal(StyleAmbience.Market, StyleRules.AmbienceOf(Monde + "eds_BSB2728_marche_exterieur_1.ogg"));
        Assert.Equal(StyleAmbience.Market, StyleRules.AmbienceOf(Monde + "eds_FS424790_bolkmar_marche_medieval.ogg"));
        Assert.Equal(StyleAmbience.Tavern, StyleRules.AmbienceOf(Monde + "eds_FS326313_ivolipa_taverne_rires.ogg"));
        Assert.Equal(StyleAmbience.Tavern, StyleRules.AmbienceOf(Monde + "eds_BSB0480_ambiance_bar.ogg"));
        Assert.Equal(StyleAmbience.Forge, StyleRules.AmbienceOf(Monde + "eds_FS386149_ldezem_forge_charbon.ogg"));
        Assert.Equal(StyleAmbience.Campfire, StyleRules.AmbienceOf(Monde + "eds_FS660298_AmbientX_feu_de_camp.ogg"));
        Assert.Equal(StyleAmbience.River, StyleRules.AmbienceOf(Monde + "eds_BSB2713_foret_ruisseau.ogg"));
        Assert.Equal(StyleAmbience.Forest, StyleRules.AmbienceOf(Monde + "eds_BSB0100_foret.ogg"));
        Assert.Equal(StyleAmbience.Countryside, StyleRules.AmbienceOf(Monde + "eds_BSB0097_campagne.ogg"));
        Assert.Equal(StyleAmbience.Wind, StyleRules.AmbienceOf(Monde + "eds_BSB0595_vent.ogg"));
        Assert.Equal(StyleAmbience.Night, StyleRules.AmbienceOf(Monde + "eds_FS852877_simous00_grillons_cigales.ogg"));
        foreach (string sfx in new[] { "eds_BSB1854_cheval_marche_chemin.ogg", "eds_BSB3589_enclume_1.ogg", "eds_BSB1764_hulotte_2.ogg", "eds_BSB0956_corbeaux.ogg",
                     "eds_BSB0137_pas_feuilles.ogg", "eds_BSB0292_petite_cloche.ogg", "eds_FS471304_marianabe22_ouverture_portail.ogg" })
            Assert.Null(StyleRules.AmbienceOf(Monde + sfx));
        Assert.True(StyleRules.IsLongRecording(Monde + "eds_FS326313_ivolipa_taverne_rires.ogg"));
        string[] world = new[]
        {
            "eds_FS386149_ldezem_forge_charbon.ogg", "eds_BSB3589_enclume_1.ogg", "eds_BSB0292_petite_cloche.ogg", "eds_FS646519_kevp888_cloche_village_FR.ogg",
            "eds_FS426642_Garuda1982_cloche_eglise.ogg", "eds_FS622816_klankbeeld_cloches_eglise.ogg", "eds_FS471304_marianabe22_ouverture_portail.ogg",
            "eds_FS768664_berkup_portail_ouverture.ogg", "eds_FS521587_Fission9_pas_herbe.ogg", "eds_FS223154_Yoyodaman234_pas_gravier.ogg",
            "eds_BSB0137_pas_feuilles.ogg", "eds_BSB0376_pas_parquet.ogg", "eds_BSB0956_corbeaux.ogg", "eds_BSB3463_corneille_3.ogg", "eds_BSB0440_coq.ogg",
            "eds_BSB0978_poules.ogg", "eds_BSB2343_mouton.ogg", "eds_BSB0546_vache_1.ogg", "eds_BSB2954_chien_aboie.ogg", "eds_BSB0284_cheval_hennit_1.ogg",
            "eds_BSB1764_hulotte_2.ogg", "eds_BSB1854_cheval_marche_chemin.ogg", "eds_BSB1795_whoosh_3.ogg",
        }.Select(f => Monde + f).Concat(Sounds).ToArray();
        Assert.EndsWith("eds_BSB0292_petite_cloche.ogg", StyleRules.PickSound(world, StyleSound.Tocsin));   // un seul coup, frappé vite
        Assert.Equal(4, StyleRules.PickSounds(world, StyleSound.ChurchBell, 6).Count);   // trois de la 2e vague et celle de la 1re
        Assert.Contains(StyleRules.PickSounds(world, StyleSound.ChurchBell, 6), p => p.Contains("kevp888"));
        Assert.EndsWith("enclume_1.ogg", StyleRules.PickSound(world, StyleSound.Craft));   // la forge est une ambiance, l'enclume un bruitage
        Assert.Equal(2, StyleRules.PickSounds(world, StyleSound.Portal, 6).Count);
        Assert.EndsWith("pas_herbe.ogg", StyleRules.PickSound(world, StyleSound.StepGrass));   // pas le lièvre dans l'herbe
        Assert.EndsWith("pas_gravier.ogg", StyleRules.PickSound(world, StyleSound.StepGravel));
        Assert.EndsWith("pas_feuilles.ogg", StyleRules.PickSound(world, StyleSound.StepLeaves));
        Assert.EndsWith("pas_parquet.ogg", StyleRules.PickSound(world, StyleSound.StepWood));
        Assert.EndsWith("corbeaux.ogg", StyleRules.PickSound(world, StyleSound.Caw));
        Assert.EndsWith("vache_1.ogg", StyleRules.PickSound(world, StyleSound.Moo));
        Assert.EndsWith("chien_aboie.ogg", StyleRules.PickSound(world, StyleSound.Bark));
        Assert.EndsWith("cheval_hennit_1.ogg", StyleRules.PickSound(world, StyleSound.Neigh));
        Assert.EndsWith("coq.ogg", StyleRules.PickSound(world, StyleSound.Rooster));
        Assert.EndsWith("poules.ogg", StyleRules.PickSound(world, StyleSound.Hens));
        Assert.EndsWith("mouton.ogg", StyleRules.PickSound(world, StyleSound.Sheep));
        Assert.EndsWith("hulotte_2.ogg", StyleRules.PickSound(world, StyleSound.Owl));
    }

    [Fact]
    public void Deuxieme_vague_musiques_de_Scott_Buckley()
    {
        const string music = "Assets/Style/Music/";
        Assert.Equal(StyleMusic.Exploration, StyleRules.MusicOf(music + "ScottBuckley/eds_musique_ScottBuckley_IntoTheWilds.mp3"));
        Assert.Equal(StyleMusic.Story, StyleRules.MusicOf(music + "ScottBuckley/eds_musique_ScottBuckley_LegendOfKvothe.mp3"));
        Assert.Equal(StyleMusic.Threshold, StyleRules.MusicOf(music + "ScottBuckley/eds_musique_ScottBuckley_Sanctum.mp3"));
        Assert.True(StyleRules.MusicRank(music + "ScottBuckley/eds_musique_ScottBuckley_IntoTheWilds.mp3")
            < StyleRules.MusicRank(music + "MatthewPablo/Woodland Fantasy.mp3"));
        Assert.Equal(int.MaxValue, StyleRules.MusicRank("Assets/Style/Sounds/x.ogg"));
        Assert.Equal(StyleMusic.Threshold, StyleRules.MusicFor(false, false, true, true, false));
        Assert.Equal(StyleMusic.Story, StyleRules.MusicFor(false, false, false, true, true));
        Assert.Equal(StyleMusic.Combat, StyleRules.MusicFor(false, true, false, true, true));
        Assert.Equal(StyleMusic.Village, StyleRules.MusicFor(false, false, true, false, false));
    }

    [Fact]
    public void Deuxieme_vague_ambiances_par_lieu_et_par_heure()
    {
        AmbiencePlace bourg = StyleRules.PlaceAmbience(new[] { "bourg" }, "zone.vallons", false);
        Assert.Equal(AmbiencePlace.Town, bourg);
        Assert.Equal(AmbiencePlace.TavernInside, StyleRules.PlaceAmbience(Array.Empty<string>(), "zone.auberge", true));
        Assert.Equal(AmbiencePlace.Camp | AmbiencePlace.Fields, StyleRules.PlaceAmbience(new[] { "camp_des_rodeurs", "pre_nord" }, "zone.vallons", false));
        Assert.Equal(AmbiencePlace.Forest, StyleRules.PlaceAmbience(new[] { "foret_des_loups" }, null, false));
        Assert.Equal(AmbiencePlace.Threshold, StyleRules.PlaceAmbience(new[] { "coeur_du_seuil" }, null, false));
        double V(StyleAmbience a, AmbiencePlace p, double hour, double forge = double.NaN, bool indoors = false, double night = 0, double rain = 0) =>
            StyleRules.PlaceVolume(a, p, hour * 60, forge, false, indoors, night, rain);
        Assert.True(V(StyleAmbience.Market, bourg, 9.5) > 0.7);          // le marché du matin
        Assert.InRange(V(StyleAmbience.Market, bourg, 15), 0.2, 0.3);   // plus calme l'après-midi
        Assert.Equal(0, V(StyleAmbience.Market, bourg, 22));
        Assert.True(V(StyleAmbience.Market, bourg, 9.5, rain: 1) < V(StyleAmbience.Market, bourg, 9.5));
        Assert.Equal(0, V(StyleAmbience.Tavern, bourg, 12));
        Assert.True(V(StyleAmbience.Tavern, bourg, 21) > 0.2);            // la taverne le soir, de loin
        Assert.Equal(0.9, V(StyleAmbience.Tavern, AmbiencePlace.TavernInside, 12, indoors: true));
        Assert.Equal(0, V(StyleAmbience.Village, bourg, 12, indoors: true));
        Assert.True(V(StyleAmbience.Forge, AmbiencePlace.None, 10, forge: 3) > 0.8);   // près de Maëlle, le jour
        Assert.Equal(0, V(StyleAmbience.Forge, AmbiencePlace.None, 10, forge: 40));
        Assert.Equal(0, V(StyleAmbience.Forge, AmbiencePlace.None, 23, forge: 3));
        Assert.True(V(StyleAmbience.Campfire, AmbiencePlace.Camp, 23, night: 1) > V(StyleAmbience.Campfire, AmbiencePlace.Camp, 12));
        Assert.Equal(0, V(StyleAmbience.Countryside, AmbiencePlace.Fields | AmbiencePlace.Town, 12));   // au hameau, la rumeur du village
        Assert.True(V(StyleAmbience.Forest, AmbiencePlace.Forest, 12) > V(StyleAmbience.Forest, AmbiencePlace.Forest, 1, night: 1));
        Assert.Equal(0, V(StyleAmbience.Birds, bourg, 12));
        Assert.True(StyleRules.IsPlaceAmbience(StyleAmbience.Market) && !StyleRules.IsPlaceAmbience(StyleAmbience.Night));
    }

    [Fact]
    public void Deuxieme_vague_pas_et_cris_de_la_ferme()
    {
        Assert.Equal(StyleSound.StepWood, StyleRules.StepSound(true, 0, 1, true));
        Assert.Equal(StyleSound.StepGravel, StyleRules.StepSound(false, 0.2, 1, true));
        Assert.Equal(StyleSound.StepLeaves, StyleRules.StepSound(false, 5, 1, true));
        Assert.Equal(StyleSound.StepGrass, StyleRules.StepSound(false, 5, 0.8, false));
        Assert.Equal(StyleSound.StepGravel, StyleRules.StepSound(false, 5, 0.1, false));
        Assert.True(double.IsPositiveInfinity(BeastCalls.StepLength(0.1)));
        Assert.Equal(0.75, BeastCalls.StepLength(1.5));
        Assert.Equal(1.4, BeastCalls.StepLength(9), 6);
        Assert.Contains(BeastCalls.Cue("eds_FS521587_Fission9_pas_herbe", 4.35, 3, 0.5).Start, new[] { 0.07, 0.77, 1.47, 2.245, 3.02, 3.77 });
        Assert.Equal((0.0, 0.6), BeastCalls.Cue("eds_FS223154_Yoyodaman234_pas_gravier", 0.69, 9, 0.5));
        Assert.Contains(BeastCalls.Cue("eds_FS646519_kevp888_cloche_village_FR", 72.5, 1, 12).Start, new[] { 17.0, 30.0, 44.0 });
        Assert.True(BeastCalls.CallTime(StyleSound.Rooster, 6 * 60) && !BeastCalls.CallTime(StyleSound.Rooster, 12 * 60));
        Assert.True(BeastCalls.CallTime(StyleSound.Owl, 23 * 60) && !BeastCalls.CallTime(StyleSound.Owl, 12 * 60));
        Assert.True(BeastCalls.CallTime(StyleSound.Moo, 12 * 60) && !BeastCalls.CallTime(StyleSound.Moo, 2 * 60));
        foreach (StyleSound farm in new[] { StyleSound.Caw, StyleSound.Moo, StyleSound.Bark, StyleSound.Neigh, StyleSound.Rooster, StyleSound.Hens, StyleSound.Sheep, StyleSound.Owl })
            for (int n = 0; n < 50; n++) Assert.InRange(BeastCalls.Interval(farm, n), 10, 80);
    }
}
