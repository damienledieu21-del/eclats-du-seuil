using System;
using System.Linq;
using EdS.Runtime;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.9.2 (« vraies animations, vrais corps, 3e et 4e vagues ») : le meilleur modèle par bête avec les FBX convertis de la 3e vague
/// et les bêtes de Quaternius en repli, les corps humains (Universal Base Characters) et leurs animations (Universal Animation Library), les
/// sons et musiques des 3e et 4e vagues. Affichage seulement.
/// </summary>
[Trait("Category", "Runtime")]
public sealed class V292AnimationsTests
{
    private const string A2 = "Assets/Style/Sketchfab/Animaux2/";
    private const string A3 = "Assets/Style/Sketchfab/Animaux3/";
    private const string UAA = "Assets/Style/Animals/Quaternius/";
    private const string Farm = "Assets/Style/Quaternius/FarmAnimals/";
    private const string Monde = "Assets/Style/Sounds/Monde/";

    private static readonly string[] Models =
    {
        A2 + "Elan_WildMesh.fbx", A2 + "CerfRouge_Suiteofart.fbx", A2 + "Faon_AssetsAnimated.fbx", A2 + "Vache_rukh3d.fbx", A2 + "Chien_quander.fbx",
        A2 + "Cheval_henry7.fbx", A2 + "Sanglier_Dakta.fbx",
        A3 + "Biche_WildMesh.fbx", A3 + "CerfCel_WildMesh.fbx", A3 + "Ours_WildMesh.fbx", A3 + "Ane_WildMesh.fbx", A3 + "Poule_kill6lucius.fbx",
        A3 + "Coq_rukh3d.fbx", A3 + "Vache_nataliekirk.fbx", A3 + "Chien_SharPei_artsiom.fbx",
        A3 + "Biche_WildMesh.glb", A3 + "Ane_WildMesh.glb",
        UAA + "Wolf_Quaternius.fbx", UAA + "Stag_Quaternius.fbx", UAA + "Deer_Quaternius.fbx", UAA + "Cow.fbx", UAA + "Bull.fbx", UAA + "Husky.fbx",
        UAA + "ShibaInu.fbx", UAA + "Donkey.fbx", UAA + "Horse.fbx",
        Farm + "Cow.fbx", Farm + "Horse.fbx", Farm + "Pig.fbx", Farm + "Pug.fbx", Farm + "Sheep.fbx",
        "Assets/Style/PolyHaven/plane_wing.fbx", "Assets/Style/PolyHaven/kitchen_table.fbx", "Assets/Style/PolyHaven/colours_rack.fbx",
        "Assets/Style/PolyHaven/ball_bearing.fbx",
    };

    [Fact]
    public void Le_meilleur_modele_par_bete_avec_les_3e_et_4e_vagues()
    {
        Assert.EndsWith("Elan_WildMesh.fbx", StyleRules.Candidates(Models, StyleSlot.Stag)[0]);
        Assert.EndsWith("CerfRouge_Suiteofart.fbx", StyleRules.Candidates(Models, StyleSlot.Stag)[1]);
        Assert.EndsWith("CerfCel_WildMesh.fbx", StyleRules.Candidates(Models, StyleSlot.Stag)[2]);
        Assert.EndsWith("Stag_Quaternius.fbx", StyleRules.Candidates(Models, StyleSlot.Stag)[3]);
        Assert.EndsWith("Biche_WildMesh.fbx", StyleRules.Candidates(Models, StyleSlot.Doe)[0]);
        Assert.EndsWith("Faon_AssetsAnimated.fbx", StyleRules.Candidates(Models, StyleSlot.Doe)[1]);
        Assert.EndsWith("Vache_nataliekirk.fbx", StyleRules.Candidates(Models, StyleSlot.Cow)[0]);
        Assert.EndsWith("Vache_rukh3d.fbx", StyleRules.Candidates(Models, StyleSlot.Cow)[1]);
        Assert.Equal(UAA + "Cow.fbx", StyleRules.Candidates(Models, StyleSlot.Cow)[2]);   // animée (marche, galop…) avant celle de la ferme
        Assert.Equal(Farm + "Cow.fbx", StyleRules.Candidates(Models, StyleSlot.Cow)[3]);
        Assert.EndsWith("Chien_SharPei_artsiom.fbx", StyleRules.Candidates(Models, StyleSlot.Dog)[0]);
        Assert.EndsWith("Chien_quander.fbx", StyleRules.Candidates(Models, StyleSlot.Dog)[1]);
        Assert.Contains(UAA + "Husky.fbx", StyleRules.Candidates(Models, StyleSlot.Dog));
        Assert.EndsWith("Cheval_henry7.fbx", StyleRules.Candidates(Models, StyleSlot.Horse)[0]);
        Assert.Equal(UAA + "Horse.fbx", StyleRules.Candidates(Models, StyleSlot.Horse)[1]);
        Assert.EndsWith("Ane_WildMesh.fbx", StyleRules.Candidates(Models, StyleSlot.Donkey)[0]);
        Assert.Equal(UAA + "Donkey.fbx", StyleRules.Candidates(Models, StyleSlot.Donkey)[1]);
        Assert.EndsWith("Poule_kill6lucius.fbx", Assert.Single(StyleRules.Candidates(Models, StyleSlot.Hen)));
        Assert.EndsWith("Coq_rukh3d.fbx", Assert.Single(StyleRules.Candidates(Models, StyleSlot.Rooster)));
        Assert.EndsWith("Ours_WildMesh.fbx", Assert.Single(StyleRules.Candidates(Models, StyleSlot.Bear)));
        // Mots entiers : un avion, une cuisine, des couleurs ou un roulement ne sont pas des bêtes.
        foreach (StyleSlot slot in new[] { StyleSlot.Donkey, StyleSlot.Hen, StyleSlot.Rooster, StyleSlot.Bear })
            Assert.DoesNotContain(StyleRules.Candidates(Models, slot), p => p.Contains("PolyHaven"));
        Assert.True(StyleRules.Matches("Assets/Style/x/ane_gris.fbx", StyleSlot.Donkey));
        Assert.True(StyleRules.Matches("Assets/Style/x/brown_bear.fbx", StyleSlot.Bear));
        // Le cochon rose de la ferme n'est pas un sanglier ; le sanglier converti reste devant.
        Assert.DoesNotContain(Farm + "Pig.fbx", StyleRules.Candidates(Models, StyleSlot.Boar));
        Assert.EndsWith("Sanglier_Dakta.fbx", StyleRules.Candidates(Models, StyleSlot.Boar)[0]);
        // Aucune bête n'est prise pour du décor ou un personnage.
        foreach (StyleSlot decor in new[] { StyleSlot.Tree, StyleSlot.Rock, StyleSlot.Plant, StyleSlot.Debris, StyleSlot.Shrub, StyleSlot.Hero, StyleSlot.Cat })
            Assert.DoesNotContain(StyleRules.Candidates(Models, decor), p => p.Contains("Animaux3") || p.Contains("Quaternius"));
        // Les FBX passent devant les GLB laissés à côté (Unity ne lit pas ces derniers).
        Assert.EndsWith(".fbx", StyleRules.Candidates(Models, StyleSlot.Donkey)[0]);
        Assert.Equal(StyleSlot.Bear, StyleRules.ForCombatant("ours_brun", false));
        Assert.True(StyleRules.IsBeast(StyleSlot.Bear));
        Assert.Equal(1.6, StyleRules.BeastHeight("ours1"));
    }

    [Fact]
    public void Les_clips_des_FBX_convertis_sont_trouves()
    {
        // Noms donnés par Claude à la conversion (biche, cerf cel shading, ours, âne, poule, coq, vache, chien).
        string[] bear = { "Idle", "Idle2", "Walk", "Trot", "Run", "Sprint", "Eat", "Attack", "Attack2", "Hurt", "Death", "Lie", "Sit", "Rear", "Roar" };
        Assert.Equal("Idle", StyleRules.Clip(bear, StyleMotion.Idle));
        Assert.Equal("Walk", StyleRules.Clip(bear, StyleMotion.Walk));
        Assert.Equal("Run", StyleRules.Clip(bear, StyleMotion.Run));
        Assert.Equal("Attack", StyleRules.Clip(bear, StyleMotion.Attack));
        Assert.Equal("Attack2", StyleRules.Clip(bear, StyleMotion.Attack, null, 1));
        Assert.Equal("Roar", StyleRules.Clip(bear, StyleMotion.Cast));   // le rugissement : un cri
        Assert.Equal("Hurt", StyleRules.Clip(bear, StyleMotion.Hurt));
        Assert.Equal("Death", StyleRules.Clip(bear, StyleMotion.Death));
        Assert.Equal("Eat", StyleRules.Clip(bear, StyleMotion.Graze));
        string[] donkey = { "Idle", "Idle2", "Walk", "Trot", "Run", "Sprint", "Eat", "Attack", "Death", "Lie", "Call" };
        Assert.Equal("Walk", StyleRules.Clip(donkey, StyleMotion.Walk));
        Assert.Equal("Eat", StyleRules.Clip(donkey, StyleMotion.Graze));
        string[] hen = { "Idle", "Eat", "Walk", "Run", "Flee", "Sit", "Flap" };
        Assert.Equal("Walk", StyleRules.Clip(hen, StyleMotion.Walk));
        Assert.Equal("Eat", StyleRules.Clip(hen, StyleMotion.Graze));
        string[] rooster = { "Idle", "Eat" };   // la marche et la course du coq étaient abîmées (ergot étiré) : non gardées
        Assert.Equal("Idle", StyleRules.Clip(rooster, StyleMotion.Walk));
        Assert.True(StyleRules.Loops("Idle2") && StyleRules.Loops("Sprint") && StyleRules.Loops("Eat") && !StyleRules.Loops("Attack2") && !StyleRules.Loops("Death"));
        // Pattes de la vache de nataliekirk : le haut seulement (« _1 »).
        Assert.Equal((true, true), BeastCalls.LegOf("L_Front_1_05"));
        Assert.Equal((false, false), BeastCalls.LegOf("R_Hind_1_09"));
        Assert.Null(BeastCalls.LegOf("L_Front_2_06"));
        Assert.Null(BeastCalls.LegOf("R_Hind_3_011"));
        Assert.Null(BeastCalls.LegOf("Hip_2_02"));
        Assert.Equal((true, true), BeastCalls.LegOf("LEGF.L_037"));   // la vache de rukh3d inchangée
    }

    [Fact]
    public void Les_fichiers_des_corps_humains_sont_reconnus()
    {
        const string ubc = "Assets/Style/Quaternius/BaseCharacters/";
        Assert.False(HumanLooks.BodyOf(ubc + "Superhero_Male_FullBody.fbx"));
        Assert.True(HumanLooks.BodyOf(ubc + "Superhero_Female_FullBody.fbx"));
        Assert.Null(HumanLooks.BodyOf(ubc + "Superhero_Female_FullBody.gltf"));
        Assert.Null(HumanLooks.BodyOf("Assets/Style/Mixamo/Paladin.fbx"));
        Assert.Equal((HumanHair.Long, false, false), HumanLooks.HairOf(ubc + "Hairstyles/Rigged to Head Bone/FBX (Unity)/Hair_Long.fbx"));
        Assert.Equal((HumanHair.None, true, false), HumanLooks.HairOf(ubc + "Hairstyles/Rigged to Head Bone/FBX (Unity)/Hair_Beard.fbx"));
        Assert.Equal((HumanHair.None, false, true), HumanLooks.HairOf(ubc + "Hairstyles/Rigged to Head Bone/FBX (Unity)/Eyebrows_Female.fbx"));
        Assert.Null(HumanLooks.HairOf(ubc + "Hairstyles/Origin at 0/FBX (Unity)/Hair_Long.fbx"));   // sans os : ne suivrait pas la tête
        Assert.True(HumanLooks.IsAnimationLibrary("Assets/Style/Quaternius/UAL1/UAL1_Standard.fbx"));
        Assert.True(HumanLooks.IsAnimationLibrary("Assets/Style/Quaternius/UAL2/UAL2_Standard.fbx"));
        Assert.False(HumanLooks.IsAnimationLibrary("Assets/Style/Quaternius/UAL2/UAL2_Standard_RM.fbx"));
        string[] textures =
        {
            ubc + "Textures/T_Superhero_Male_Dark.png", ubc + "Textures/T_Superhero_Male_Ligh.png", ubc + "Textures/T_Superhero_Male_Normal.png",
            ubc + "Textures/T_Superhero_Female_Dark_BaseColor.png", ubc + "Textures/T_Superhero_Female_Light_BaseColor.png",
            ubc + "Textures/T_Superhero_Female_Normal.png", ubc + "Textures/T_Superhero_Female_Roughness.png", ubc + "Textures/T_Hair_1_BaseColor.png",
            ubc + "Textures/T_Hair_2_BaseColor.png", ubc + "Textures/T_Hair_1_Normal.png", ubc + "Textures/T_Eye_Brown.png",
        };
        Assert.EndsWith("T_Superhero_Male_Ligh.png", HumanLooks.SkinTexture(textures, false, false));
        Assert.EndsWith("T_Superhero_Male_Dark.png", HumanLooks.SkinTexture(textures, false, true));
        Assert.EndsWith("T_Superhero_Female_Light_BaseColor.png", HumanLooks.SkinTexture(textures, true, false));
        Assert.EndsWith("T_Superhero_Female_Dark_BaseColor.png", HumanLooks.SkinTexture(textures, true, true));
        Assert.EndsWith("T_Superhero_Female_Normal.png", HumanLooks.BodyNormal(textures, true));
        Assert.EndsWith("T_Hair_2_BaseColor.png", HumanLooks.HairTexture(textures, 2));
        Assert.EndsWith("T_Eye_Brown.png", HumanLooks.HairTexture(textures, 0));
    }

    [Fact]
    public void Les_animations_UAL_ont_des_noms_que_les_regles_comprennent()
    {
        // Vrais noms des prises (lus dans UAL1_Standard.fbx et UAL2_Standard.fbx le 8 novembre 2026).
        string[] takes =
        {
            "Armature|Armature|A_TPose", "Armature|Armature|Idle_Loop", "Armature|Armature|Idle_Talking_Loop", "Armature|Armature|Walk_Loop",
            "Armature|Armature|Walk_Formal_Loop", "Armature|Armature|Jog_Fwd_Loop", "Armature|Armature|Sprint_Loop", "Armature|Armature|Death01",
            "Armature|Armature|Hit_Chest", "Armature|Armature|Hit_Head", "Armature|Armature|Sword_Attack", "Armature|Armature|Sword_Idle",
            "Armature|Armature|Spell_Simple_Shoot", "Armature|Armature|Sitting_Talking_Loop", "Armature|Armature|Pistol_Shoot",
            "Armature|Armature|Farm_Harvest", "Armature|Armature|TreeChopping_Loop", "Armature|Armature|Idle_FoldArms_Loop", "Armature|Armature|Sword_Block",
            "Armature|Armature|Sword_Regular_A", "Armature|Armature|Sword_Regular_B", "Armature|Armature|Sword_Heavy_Combo", "Armature|Armature|Hit_Knockback",
            "Armature|Armature|Idle_Shield_Loop", "Armature|Armature|Idle_Shield_Break", "Armature|Armature|Zombie_Walk_Fwd_Loop", "Armature|Armature|Yes",
            "Armature|Armature|Walk_Carry_Loop", "Armature|Armature|Idle_No_Loop",
        };
        var named = takes.Select(HumanLooks.CanonicalClip).Where(c => c != null).Select(c => c!.Value).ToList();
        Assert.Null(HumanLooks.CanonicalClip("Armature|Armature|A_TPose"));
        Assert.Null(HumanLooks.CanonicalClip("Armature|Armature|Pistol_Shoot"));
        Assert.Null(HumanLooks.CanonicalClip("Armature|Armature|Zombie_Walk_Fwd_Loop"));
        Assert.Equal(("Idle", true), HumanLooks.CanonicalClip("Idle_Loop"));
        string[] clips = named.Select(c => c.Name).ToArray();
        Assert.Equal(clips.Length, clips.Distinct().Count());
        Assert.Equal("Idle", StyleRules.Clip(clips, StyleMotion.Idle));
        Assert.Equal("Walk", StyleRules.Clip(clips, StyleMotion.Walk));
        Assert.Equal("Run", StyleRules.Clip(clips, StyleMotion.Run));   // le petit trot (« Jog »), le sprint en variante
        Assert.Equal("Death", StyleRules.Clip(clips, StyleMotion.Death));
        Assert.StartsWith("Hurt_", StyleRules.Clip(clips, StyleMotion.Hurt));
        Assert.StartsWith("Block_", StyleRules.Clip(clips, StyleMotion.Block));
        Assert.Equal("Guard_Break", StyleRules.Clip(clips, StyleMotion.GuardHit));
        Assert.Equal("Cast", StyleRules.Clip(clips, StyleMotion.Cast));
        // Un coup reçu n'est jamais pris pour une attaque, quelle que soit la variante.
        for (int v = 0; v < 12; v++)
            Assert.StartsWith("Attack_", StyleRules.Clip(clips, StyleMotion.Attack, null, v));
        // Villageois : parler, regarder, cueillir, marcher.
        Assert.StartsWith("Talk", TownModels.VillagerClip(clips, VillagerActivity.Talk, 0));
        for (int v = 0; v < 6; v++) Assert.StartsWith("Talk", TownModels.VillagerClip(clips, VillagerActivity.Talk, v));   // jamais assis
        Assert.StartsWith("Look_", TownModels.VillagerClip(clips, VillagerActivity.Look, 0));
        Assert.StartsWith("Gather_", TownModels.VillagerClip(clips, VillagerActivity.Gather, 0));
        Assert.StartsWith("Walk", TownModels.VillagerClip(clips, VillagerActivity.Walk, 3));
        Assert.Equal("Idle", TownModels.VillagerClip(clips, VillagerActivity.Idle, 0));
        Assert.Equal("Work_Chop", HumanLooks.WorkClip(clips, HumanOutfit.Smith, 0));
        Assert.Equal("Gather_Harvest", HumanLooks.WorkClip(clips, HumanOutfit.Peasant, 0));
        Assert.Null(HumanLooks.WorkClip(clips, HumanOutfit.Guard, 0));
        // Boucles : attentes, marches, travaux ; pas les gestes.
        foreach ((string name, bool loop) in named)
            Assert.Equal(loop, name.StartsWith("Idle") || name.StartsWith("Walk") || name.StartsWith("Talk") || name.StartsWith("Look") || name.StartsWith("Lean") || name.StartsWith("Gather")
                || name.StartsWith("Work") || name.StartsWith("Seated") || name == "Run" || name == "Sprint" || name == "Ready_Sword" || name == "Block_Shield");
    }

    [Fact]
    public void Chaque_villageois_a_son_aspect_stable_et_varie()
    {
        HumanLook odile = HumanLooks.For("npc.odile", "paysan");
        Assert.True(odile.Female);
        Assert.Equal(odile.Hair, HumanLooks.For("npc.odile", "paysan").Hair);   // toujours le même
        Assert.False(HumanLooks.For("npc.gaspard", "paysan").Female);
        Assert.True(HumanLooks.For("npc.albe", "paysan").Female);
        Assert.Equal(HumanOutfit.Clergy, HumanLooks.For("npc.albe", "paysan").Outfit);
        Assert.Equal(HumanOutfit.Smith, HumanLooks.For("npc.maelle", "paysan").Outfit);
        Assert.Equal(HumanOutfit.Guard, HumanLooks.For("bourg/garde1", "garde").Outfit);
        Assert.Equal(HumanOutfit.Sentinel, HumanLooks.For("bourg/garde2", "sentinelle").Outfit);
        Assert.Equal(HumanOutfit.Guard, HumanLooks.For("npc.archer_garde", "paysan").Outfit);
        Assert.Equal(HumanOutfit.Bandit, HumanLooks.For("bandit2", "bandit").Outfit);
        Assert.Equal(5, HumanLooks.For("npc.doyen", "paysan").HairTone);   // cheveux gris
        int females = 0, dark = 0, beards = 0;
        var hairs = new System.Collections.Generic.HashSet<HumanHair>();
        var palettes = new System.Collections.Generic.HashSet<int>();
        for (int n = 0; n < 400; n++)
        {
            HumanLook l = HumanLooks.For("bourg/passant" + n, "paysan");
            if (l.Female) females++;
            if (l.DarkSkin) dark++;
            if (l.Beard) beards++;
            Assert.False(l.Female && l.Beard);
            Assert.InRange(l.Height, l.Female ? 1.62 : 1.72, l.Female ? 1.72 : 1.86);
            Assert.InRange(l.Width, 0.95, 1.07);
            Assert.InRange(l.HairTone, 0, HumanLooks.HairTones.Length - 1);
            Assert.Equal(HumanOutfit.Peasant, HumanLooks.Palettes[l.Palette].Outfit);
            Assert.True(l.Female ? l.Hair == HumanHair.Long || l.Hair == HumanHair.Buns || l.Hair == HumanHair.BuzzedFemale
                : l.Hair == HumanHair.Buzzed || l.Hair == HumanHair.SimpleParted || l.Hair == HumanHair.Long);
            hairs.Add(l.Hair);
            palettes.Add(l.Palette);
        }
        Assert.InRange(females, 150, 250);
        Assert.InRange(dark, 50, 130);
        Assert.InRange(beards, 50, 140);
        Assert.Equal(5, hairs.Count);
        Assert.Equal(5, palettes.Count);   // les cinq tenues de paysans
        // Qui prend un corps humain.
        Assert.True(HumanLooks.PreferHuman("paysan", true, true, true));
        Assert.False(HumanLooks.PreferHuman("paysan", true, true, false));    // réglage coupé : Mixamo
        Assert.True(HumanLooks.PreferHuman("paysan", false, true, false));    // sans Mixamo : le corps humain
        Assert.True(HumanLooks.PreferHuman("npc.odile", true, true, true));
        Assert.False(HumanLooks.PreferHuman("bandit", true, true, true));     // un bandit de Mixamo le reste
        Assert.True(HumanLooks.PreferHuman("bandit", false, true, true));
        Assert.False(HumanLooks.PreferHuman("gardien_eveille", false, true, true));
        Assert.False(HumanLooks.PreferHuman("paysan", false, false, true));   // pas de corps prêts : rien ne change
    }

    [Fact]
    public void Sons_et_musiques_des_3e_et_4e_vagues()
    {
        // Noms possibles des fichiers déposés : reconnus par leur numéro Freesound.
        string[] world = new[]
        {
            "eds_FS137836_thefilmbakery_forge_atelier.ogg", "eds_FS386149_ldezem_forge_charbon.ogg", "eds_FS480724_craigsmith_diner_restaurant.ogg",
            "eds_FS437103_craigsmith_cheval_paves.ogg", "eds_FS847110_TSPTalk_cloche_courte.ogg", "eds_FS760390_KristofferAndersson_cloche_tour_Falun.ogg",
            "eds_FS522704_julianmateo_magie_teleportation.ogg", "eds_FS836064_newlocknew_pluie_jardin_toit.ogg", "eds_FS164895_timkahn_ruisseau.ogg",
            "eds_FS593439_kevp888_vent_foret.ogg", "eds_FS733274_Soundrack_bord_du_lac.ogg", "eds_BSB0292_petite_cloche.ogg",
            "eds_FS622816_klankbeeld_cloches_eglise.ogg", "eds_BSB2713_foret_ruisseau.ogg", "eds_BSB0595_vent.ogg", "eds_FS326313_ivolipa_taverne_rires.ogg",
        }.Select(f => Monde + f).ToArray();
        Assert.Equal(StyleAmbience.Forge, StyleRules.AmbienceOf(world[0]));
        Assert.True(StyleRules.AmbienceRank(world[0]) < StyleRules.AmbienceRank(world[1]));   // l'atelier de forge avant le charbon
        Assert.Equal(StyleAmbience.Dining, StyleRules.AmbienceOf(world[2]));
        Assert.Equal(StyleAmbience.Rain, StyleRules.AmbienceOf(world[7]));
        Assert.Equal(StyleAmbience.River, StyleRules.AmbienceOf(world[8]));
        Assert.True(StyleRules.AmbienceRank(world[8]) < StyleRules.AmbienceRank(world[13]));
        Assert.Equal(StyleAmbience.Wind, StyleRules.AmbienceOf(world[9]));   // le vent dans la forêt, pas la forêt
        Assert.True(StyleRules.AmbienceRank(world[9]) < StyleRules.AmbienceRank(world[14]));
        Assert.Equal(StyleAmbience.Lake, StyleRules.AmbienceOf(world[10]));
        Assert.Equal(StyleAmbience.Tavern, StyleRules.AmbienceOf(world[15]));
        Assert.Equal(int.MaxValue, StyleRules.AmbienceRank(world[3]));
        foreach (int sfx in new[] { 3, 4, 5, 6, 11, 12 }) Assert.Null(StyleRules.AmbienceOf(world[sfx]));
        Assert.EndsWith("cheval_paves.ogg", StyleRules.PickSound(world, StyleSound.Hooves));
        Assert.EndsWith("magie_teleportation.ogg", StyleRules.PickSound(world, StyleSound.RiftSurge));
        Assert.EndsWith("cloche_tour_Falun.ogg", StyleRules.PickSound(world, StyleSound.Tocsin));   // la grosse cloche d'abord
        Assert.DoesNotContain(StyleRules.PickSounds(world, StyleSound.ChurchBell, 6), p => p.Contains("760390") || p.Contains("petite"));
        Assert.Contains(StyleRules.PickSounds(world, StyleSound.ChurchBell, 6), p => p.Contains("klankbeeld"));
        Assert.Null(StyleRules.PickSound(world, StyleSound.Bray));
        Assert.True(StyleRules.IsLongRecording(world[3]));   // les sabots : un long enregistrement, joué par passages
        Assert.Equal((7, 1.4), BeastCalls.TocsinRhythm("eds_FS760390_KristofferAndersson_cloche_tour_Falun"));
        Assert.Equal((BeastCalls.TocsinStrikes, BeastCalls.TocsinGap), BeastCalls.TocsinRhythm("eds_BSB0292_petite_cloche"));
        Assert.InRange(BeastCalls.Interval(StyleSound.Bray, 3), 45, 105);
        // Musiques de la 4e vague (et The Old Tower Inn de la 3e).
        const string oga = "Assets/Style/Music/OGA/";
        Assert.Equal(StyleMusic.Boss, StyleRules.MusicOf(oga + "eds_musique_OGA_EmmaMA_DeterminedPursuit.ogg"));
        Assert.True(StyleRules.MusicRank(oga + "eds_musique_OGA_EmmaMA_DeterminedPursuit.ogg") < StyleRules.MusicRank("Assets/Style/Music/MatthewPablo/Heroic Demise.mp3"));
        Assert.Equal(StyleMusic.VillageNight, StyleRules.MusicOf(oga + "eds_musique_OGA_Amacnaughton_NightTown.ogg"));
        Assert.Equal(StyleMusic.Inn, StyleRules.MusicOf(oga + "eds_musique_OGA_RandomMind_TheOldTowerInn.ogg"));
        Assert.Equal(StyleMusic.Inn, StyleRules.MusicOf("Assets/Style/Music/Medieval - The Old Tower Inn.mp3"));
        // Qui joue quoi.
        Assert.Equal(StyleMusic.Boss, StyleRules.MusicFor(false, true, true, false, false, 0, false, true));      // combat pendant une attaque
        Assert.Equal(StyleMusic.Combat, StyleRules.MusicFor(false, true, true, false, false, 0, false, false));
        Assert.Equal(StyleMusic.Village, StyleRules.MusicFor(false, false, true, false, false, 0, false, true));   // attaque sans combat
        Assert.Equal(StyleMusic.VillageNight, StyleRules.MusicFor(false, false, true, false, false, 0.8, false, false));
        Assert.Equal(StyleMusic.Inn, StyleRules.MusicFor(false, false, true, false, false, 0.8, true, false));
        Assert.Equal(StyleMusic.Story, StyleRules.MusicFor(false, false, true, false, true, 0.8, true, false));
        Assert.Equal(StyleMusic.Exploration, StyleRules.MusicFor(false, false, false, false, false, 1, false, false));
        Assert.Equal(StyleRules.MusicFor(true, false, true, true, false), StyleRules.MusicFor(true, false, true, true, false, 0, false, false));
        Assert.Equal(StyleMusic.Village, StyleRules.MusicFallback(StyleMusic.VillageNight));
        Assert.Equal(StyleMusic.Village, StyleRules.MusicFallback(StyleMusic.Inn));
        Assert.Equal(StyleMusic.Combat, StyleRules.MusicFallback(StyleMusic.Boss));
        Assert.Null(StyleRules.MusicFallback(StyleMusic.Story));
        // Ambiances de lieu : la salle à table aux heures des repas, le bord du lac.
        AmbiencePlace inn = StyleRules.PlaceAmbience("bourg", "zone.auberge", true);
        Assert.True(StyleRules.PlaceVolume(StyleAmbience.Dining, inn, 12.5 * 60, double.NaN, false, true, 0, 0) > 0.7);
        Assert.InRange(StyleRules.PlaceVolume(StyleAmbience.Dining, inn, 16 * 60, double.NaN, false, true, 0, 0), 0.2, 0.3);
        Assert.Equal(0, StyleRules.PlaceVolume(StyleAmbience.Dining, AmbiencePlace.Town, 12.5 * 60, double.NaN, false, false, 0, 0));
        AmbiencePlace lake = StyleRules.PlaceAmbience("lac");
        Assert.Equal(AmbiencePlace.Fields | AmbiencePlace.Lake, lake);
        Assert.Equal(AmbiencePlace.None, StyleRules.PlaceAmbience("place") & AmbiencePlace.Lake);
        Assert.True(StyleRules.PlaceVolume(StyleAmbience.Lake, lake, 12 * 60, double.NaN, false, false, 0, 0) > 0.7);
        Assert.True(StyleRules.PlaceVolume(StyleAmbience.Lake, lake, 2 * 60, double.NaN, false, false, 1, 0) < 0.4);
        Assert.Equal(0, StyleRules.PlaceVolume(StyleAmbience.Lake, AmbiencePlace.Fields, 12 * 60, double.NaN, false, false, 0, 0));
        Assert.True(StyleRules.IsPlaceAmbience(StyleAmbience.Dining) && StyleRules.IsPlaceAmbience(StyleAmbience.Lake));
    }

    [Fact]
    public void Les_auteurs_CC_BY_des_3e_et_4e_vagues_sont_cites()
    {
        string root = Lot93UnityPackageTests.Root;
        string credits = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "docs", "CREDITS.md"));
        string licences = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "unity", "Assets", "EdS", "Interface", "LICENCES.md"));
        foreach (string author in new[] { "WildMesh_3D", "kill6lucius", "rukh3d", "nataliekirk", "artsiom", "Kristoffer_Andersson", "newlocknew", "tim.kahn",
                     "kevp888", "Soundrack", "Angus Macnaughton", "Quaternius", "Emma_MA", "RandomMind" })
        {
            Assert.Contains(author, credits, StringComparison.Ordinal);
            Assert.Contains(author, licences, StringComparison.Ordinal);
        }
    }
}
