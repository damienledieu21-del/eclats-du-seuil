using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdS.Runtime;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.11 (5e vague de ressources branchée) : la liste partagée (<see cref="Wave5"/>), ses replis, les réglages des modèles, les
/// nouvelles musiques et ambiances, les intérieurs de Poly Haven, la correspondance des icônes de CraftPix (<see cref="ItemIcons"/>) et les
/// crédits. Affichage seulement.
/// </summary>
[Trait("Category", "Runtime")]
public sealed class V211Wave5Tests
{
    private static string Doc(params string[] path) => File.ReadAllText(Path.Combine(new[] { Lot93UnityPackageTests.Root }.Concat(path).ToArray())).Replace("\r\n", "\n");

    private static string Script(params string[] path) => Doc(new[] { "unity", "Assets", "EdS" }.Concat(path).ToArray());

    private static readonly IReadOnlyList<string> AllWave5 = Wave5.Entries.SelectMany(e => e.Files).Concat(Wave5.Crow.Files).ToList();

    [Fact]
    public void Chaque_role_a_une_seule_ressource_sous_Assets_Style_avec_son_credit()
    {
        var roles = Enum.GetValues<Wave5Role>().Where(r => r != Wave5Role.Crow).ToList();
        Assert.Equal(roles.OrderBy(r => r), Wave5.Entries.Select(e => e.Role).OrderBy(r => r));
        foreach (Wave5Entry e in Wave5.Entries.Concat(new[] { Wave5.Crow }))
        {
            Assert.Equal(e.Files.Count, e.Credits.Count);
            Assert.NotEmpty(e.Label);
            Assert.NotEmpty(e.Without);
            foreach (string f in e.Files)
            {
                Assert.StartsWith(EssaiManifest.Root + "/", f);
                Assert.EndsWith(".fbx", f);
            }
        }
        // Un nom de fichier n'appartient qu'à une ressource (sinon le choix par le nom serait ambigu).
        Assert.Equal(AllWave5.Count, AllWave5.Select(EssaiManifest.FileName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        // Les ressources branchées passent avant les prêtes dans la liste (l'ordre d'utilité).
        int lastLive = Wave5.Entries.ToList().FindLastIndex(e => e.Use == Wave5Use.Live);
        Assert.True(Wave5.Entries.Take(lastLive + 1).All(e => e.Use == Wave5Use.Live));
    }

    [Fact]
    public void Meilleur_repli_et_absent_comme_la_preparation()
    {
        Wave5Entry galeran = Wave5.Entry(Wave5Role.Galeran);
        string knight = galeran.Files[0], king = galeran.Files[1];
        // Un fichier déplacé dans un autre dossier compte : seul son nom est comparé.
        string moved = "Assets/Style/Ailleurs/" + EssaiManifest.FileName(king);
        Assert.Equal(knight, Wave5.Pick(galeran, new[] { moved, knight }, out int r0));
        Assert.Equal(0, r0);
        Assert.Equal(moved, Wave5.Pick(galeran, new[] { moved }, out int r1));
        Assert.Equal(1, r1);
        Assert.Null(Wave5.Pick(galeran, new[] { "Assets/Style/x/autre.fbx" }, out int r2));
        Assert.Equal(-1, r2);
        Assert.Equal(EssaiLevel.Ok, Wave5.Describe(galeran, knight, 0).Level);
        Assert.Equal(EssaiLevel.Warning, Wave5.Describe(galeran, moved, 1).Level);
        (EssaiLevel level, string detail, string fix) = Wave5.Describe(galeran, null, -1);
        Assert.Equal(EssaiLevel.Warning, level);
        Assert.Contains(galeran.Without, detail);
        Assert.Contains(EssaiManifest.FileName(knight), fix);
        // Une ressource prête et absente ne gêne rien aujourd'hui.
        Assert.Equal(EssaiLevel.Ok, Wave5.Describe(Wave5.Entry(Wave5Role.Lich), null, -1).Level);
    }

    [Fact]
    public void Les_nouveaux_modeles_ne_prennent_pas_la_place_d_une_bete_ou_d_un_decor()
    {
        // Épouvantail, loup-garou, grand loup, têtes sculptées, stèles, autel, liche, rat des rues et chevalier : aucun n'est reconnu pour une
        // case des bêtes ou du décor ; seul le corbeau animé l'est (pour le corbeau).
        foreach (string file in AllWave5)
            foreach (StyleSlot slot in Enum.GetValues<StyleSlot>().Where(s => s != StyleSlot.Character))
            {
                bool crow = EssaiManifest.FileName(file) == EssaiManifest.FileName(Wave5.Crow.Files[0]);
                Assert.True(StyleRules.Matches(file, slot) == (crow && slot == StyleSlot.Crow), file + " reconnu pour " + slot);
            }
        foreach (string head in new[] { "Assets/Style/PolyHaven/Decoration/horse_head/horse_head_1k.fbx", "Assets/Style/PolyHaven/Decoration/bull_head/bull_head_1k.fbx" })
        {
            Assert.False(StyleRules.Matches(head, StyleSlot.Horse));
            Assert.False(StyleRules.Matches(head, StyleSlot.Cow));
        }
        Assert.Empty(StyleRules.PropCandidates(AllWave5, StyleProp.Crate));
        Assert.Empty(StyleRules.PropCandidates(AllWave5, StyleProp.Bridge));
        Assert.Contains("Assets/Style/PolyHaven/Objets/wooden_military_crate/wooden_military_crate_1k.fbx",
            StyleRules.PropCandidates(new[] { "Assets/Style/PolyHaven/Objets/wooden_military_crate/wooden_military_crate_1k.fbx" }, StyleProp.Crate));
    }

    [Fact]
    public void Pains_scannes_de_Megascans_d_abord_puis_les_pains_de_Sketchfab()
    {
        Wave5Entry bread = Wave5.Entry(Wave5Role.Bread);
        Assert.Equal(9, bread.Files.Count(f => f.StartsWith(Wave5.MegascansFolder)));
        Assert.True(bread.Files.Take(9).All(f => f.StartsWith(Wave5.MegascansFolder) && f.EndsWith("_Mid.fbx")));
        Assert.True(bread.Has(Wave5Fix.ScannedMaterial));
        Assert.All(bread.Credits.Take(9), c => Assert.Equal(Wave5.FabLicense, c.License));
        Assert.All(bread.Credits.Take(9), c => Assert.False(c.Attribution));
        string sketchfab = bread.Files.First(f => f.Contains("/Sketchfab/"));
        Assert.Equal(sketchfab, Wave5.Pick(bread, new[] { sketchfab }));
        Assert.Equal(bread.Files[0], Wave5.Pick(bread, new[] { sketchfab, bread.Files[0] }));
        // Les braises du four (Sacha.Julien, n° 727387) passent devant le feu de bois.
        string embers = "Assets/Style/Sounds/Boulangerie/eds_FS727387_sachajulien_braises_four_a_pain.ogg";
        string fire = "Assets/Style/Sounds/Boulangerie/eds_boulangerie_FS484338_BonnyOrbit.ogg";
        Assert.Equal(embers, StyleRules.AmbienceFiles(new[] { fire, embers }, StyleAmbience.Bakery)[0]);
    }

    [Fact]
    public void Animations_de_metiers_a_leur_place_sans_devenir_un_personnage()
    {
        string talk = Wave5.TradeFolder + "/Talking.fbx", pick = Wave5.TradeFolder + "/Picking Up.fbx";
        Assert.True(Wave5.IsTradeClip(talk));
        Assert.Equal(pick, Wave5.TradeClip("Picking Up", new[] { talk, pick }));
        Assert.Null(Wave5.TradeClip("Kneeling", new[] { talk, pick }));
        Assert.Null(Wave5.TradeClip("Talking", new[] { "Assets/Style/Mixamo/Odile/Talking.fbx" }));
        Assert.Empty(StyleRules.Characters(new[] { talk, pick }));
        Assert.Equal(6, Wave5.TradeClips.Count);
    }

    [Fact]
    public void Croc_Gris_et_le_loup_blanc_prennent_le_grand_loup()
    {
        foreach (string id in new[] { "croc_gris", "croc_gris_enrage", "croc_gris_v2", "loup_blanc", "loup_blanc_v3" }) Assert.True(Wave5.IsDireWolf(id), id);
        foreach (string? id in new[] { "loup1", "loup_nuit2", "loup_echo1", "loup_brume1", "sanglier1", "", null }) Assert.False(Wave5.IsDireWolf(id!), id);
        Assert.True(Wave5.Entry(Wave5Role.DireWolf).Has(Wave5Fix.Animated));
    }

    [Fact]
    public void Reglages_de_la_conversion_retrouves_par_le_nom_du_modele()
    {
        Assert.True(Wave5.FixesFor("eds_SF_Animated_Crow_emptypen2477").HasFlag(Wave5Fix.AlphaClip));
        Assert.True(Wave5.FixesFor("eds_SF_Hay_bales_zbrojmistrz(Clone)").HasFlag(Wave5Fix.AlphaClip));
        Assert.True(Wave5.FixesFor("eds_SF_SPIDER_WEB_sammephi").HasFlag(Wave5Fix.AlphaClip));
        foreach (string glow in new[] { "eds_SF_Glowing_Crystals_ked182", "eds_SF_Menhirs_DragonJojo", "eds_SF_Magic_Portal_nickbroad" })
            Assert.True(Wave5.FixesFor(glow).HasFlag(Wave5Fix.Emissive), glow);
        foreach (string flat in new[] { "eds_SF_Ghost_csbr", "eds_SF_Wisp_soul_seyXR", "eds_SF_Hanging_Banner_awenram", "eds_SF_Sack_of_Flour_Low_Poly_gokhan_bayhan44" })
        {
            Assert.True(Wave5.FixesFor(flat).HasFlag(Wave5Fix.Untextured), flat);
            Assert.NotEqual((1d, 1d, 1d), Wave5.TintFor(flat));
        }
        Assert.True(Wave5.FixesFor("eds_SF_Lich_Spinnee").HasFlag(Wave5Fix.Floats));
        Assert.True(Wave5.FixesFor("eds_SF_Bat_Attack_LostModels2025").HasFlag(Wave5Fix.Floats));
        Assert.True(Wave5.FixesFor("eds_SF_Skeleton_Animated_one_boy").HasFlag(Wave5Fix.CutClips));
        Assert.Equal(Wave5Fix.None, Wave5.FixesFor("Loup_roo3d"));
        Assert.Equal(Wave5Fix.None, Wave5.FixesFor(""));
        // Tours en ruine, guirlande, demi-cape, gambison, marché : jamais sur téléphone (Note8) ; sur ordinateur, oui.
        foreach (Wave5Role heavy in new[] { Wave5Role.RuinedTowers, Wave5Role.Bunting, Wave5Role.HalfCape, Wave5Role.Gambeson, Wave5Role.MarketStalls })
        {
            Assert.False(Wave5.Shown(Wave5.Entry(heavy), true), heavy.ToString());
            Assert.True(Wave5.Shown(Wave5.Entry(heavy), false), heavy.ToString());
        }
        Assert.True(Wave5.Shown(Wave5.Entry(Wave5Role.Menhir), true));
        // Tout modèle de plus de 20 000 triangles est marqué lourd (CONVERSION_V5.md).
        foreach (Wave5Role role in new[] { Wave5Role.DireWolf, Wave5Role.Galeran, Wave5Role.Rat, Wave5Role.MarketStalls, Wave5Role.HalfCape, Wave5Role.Bunting,
            Wave5Role.StoneBridge, Wave5Role.Spider, Wave5Role.DungeonKit, Wave5Role.Gambeson, Wave5Role.RuinedTowers })
            Assert.True(Wave5.Entry(role).Has(Wave5Fix.Heavy), role.ToString());
    }

    [Fact]
    public void Squelette_de_one_boy_decoupe_dans_l_ordre_sans_chevauchement()
    {
        Assert.Contains(Wave5.SkeletonCuts, c => c.Name == "Idle" && c.Loop);
        Assert.Contains(Wave5.SkeletonCuts, c => c.Name == "Walk" && c.Loop);
        Assert.Contains(Wave5.SkeletonCuts, c => c.Name == "Attack" && !c.Loop);
        for (int i = 0; i < Wave5.SkeletonCuts.Count; i++)
        {
            Assert.True(Wave5.SkeletonCuts[i].First < Wave5.SkeletonCuts[i].Last);
            Assert.True(Wave5.SkeletonCuts[i].Last <= 1255);
            if (i > 0) Assert.True(Wave5.SkeletonCuts[i].First >= Wave5.SkeletonCuts[i - 1].Last);
        }
    }

    [Fact]
    public void Arme_en_main_tete_en_haut_ou_garde_en_bas()
    {
        Assert.Equal(Wave5Role.Mace, Wave5.WeaponRole(WeaponLook.Mace));
        Assert.Equal(Wave5Role.Axe, Wave5.WeaponRole(WeaponLook.Axe));
        Assert.Equal(Wave5Role.Sword, Wave5.WeaponRole(WeaponLook.Sword));
        Assert.Null(Wave5.WeaponRole(WeaponLook.Hammer));   // le marteau de la forge
        Assert.Null(Wave5.WeaponRole(WeaponLook.None));
        // Masse : les sommets sont vers le bout « max » (la tête) : max en haut. Épée : la garde est vers « max » : max en bas.
        Assert.Equal(1, Wave5.GripSign(0.3, -0.5, 0.5, true));
        Assert.Equal(-1, Wave5.GripSign(-0.3, -0.5, 0.5, true));
        Assert.Equal(-1, Wave5.GripSign(0.3, -0.5, 0.5, false));
        Assert.Equal(1, Wave5.GripSign(-0.3, -0.5, 0.5, false));
        Assert.Equal(0, Wave5.LongAxis(1.0, 0.19, 0.04));   // l'épée de lowseb, lame le long de x
        Assert.Equal(1, Wave5.LongAxis(0.1, 0.7, 0.2));
        Assert.Equal(2, Wave5.LongAxis(0.1, 0.2, 0.7));
        Assert.True(Wave5.WeaponLength(WeaponLook.Sword) > Wave5.WeaponLength(WeaponLook.Axe));
    }

    [Fact]
    public void Decor_pose_hors_des_obstacles_ou_pas_du_tout()
    {
        var house = new List<(double, double)> { (0, 0), (4, 0), (4, 4), (0, 4) };
        var obstacles = new IReadOnlyList<(double X, double Z)>[] { house };
        Assert.Equal((6.0, 2.0), Wave5.FreeSpot(new[] { (2.0, 2.0), (4.5, 2.0), (6.0, 2.0) }, obstacles, 1.5));
        Assert.Null(Wave5.FreeSpot(new[] { (2.0, 2.0), (4.2, 2.0) }, obstacles, 1.0));
        Assert.Equal((9.0, 9.0), Wave5.FreeSpot(new[] { (9.0, 9.0) }, Array.Empty<IReadOnlyList<(double X, double Z)>>(), 3));
        List<(double X, double Z)> around = Wave5.Around(0, 0, 90, 3).ToList();
        Assert.Equal(24, around.Count);
        Assert.Equal(3.0, around[0].X, 6);   // d'abord dans la direction voulue (90° : +x)
        Assert.Equal(0.0, around[0].Z, 6);
        Assert.Equal(around, Wave5.Around(0, 0, 90, 3).ToList());   // stable
    }

    [Fact]
    public void Musiques_de_la_forge_et_de_la_crypte_et_leurs_replis()
    {
        string forge = "Assets/Style/Music/ScottBuckley2/eds_musique_ScottBuckley_SongOfTheForge.ogg";
        string dark = "Assets/Style/Music/ScottBuckley2/eds_musique_ScottBuckley_TheLongDark.ogg";
        Assert.Equal(StyleMusic.Forge, StyleRules.MusicOf(forge));
        Assert.Equal(StyleMusic.Crypt, StyleRules.MusicOf(dark));
        Assert.Equal(StyleMusic.Crypt, StyleRules.MusicOf("Assets/Style/Music/OGA2/eds_OGA_Hitctrl_RPG_-_The_Graveyard.ogg"));
        Assert.Equal(StyleMusic.Inn, StyleRules.MusicOf("Assets/Style/Music/OGA2/eds_OGA_yd_Tavern_0.ogg"));
        Assert.Equal(StyleMusic.Threshold, StyleRules.MusicOf("Assets/Style/Music/ScottBuckley2/eds_musique_ScottBuckley_Penumbra.ogg"));
        // Les morceaux déjà choisis restent devant ceux de la 5e vague.
        string inn = "Assets/Style/Music/OGA/The_Old_Tower_Inn.ogg", tavern = "Assets/Style/Music/OGA2/eds_OGA_yd_Tavern_0.ogg";
        Assert.Equal(inn, StyleRules.MusicFiles(new[] { tavern, inn }, StyleMusic.Inn)[0]);
        string sanctum = "Assets/Style/Music/ScottBuckley/Sanctum.ogg", penumbra = "Assets/Style/Music/ScottBuckley2/eds_musique_ScottBuckley_Penumbra.ogg";
        Assert.Equal(sanctum, StyleRules.MusicFiles(new[] { penumbra, sanctum }, StyleMusic.Threshold)[0]);
        // Au village, le jour, près de la forge : la forge ; loin, la nuit ou en combat : comme avant. Une crypte l'emporte sur les lieux.
        Assert.Equal(StyleMusic.Forge, StyleRules.MusicFor(false, false, true, false, false, 0, false, false, 6, false));
        Assert.Equal(StyleMusic.Village, StyleRules.MusicFor(false, false, true, false, false, 0, false, false, 40, false));
        Assert.Equal(StyleMusic.VillageNight, StyleRules.MusicFor(false, false, true, false, false, 0.9, false, false, 6, false));
        Assert.Equal(StyleMusic.Combat, StyleRules.MusicFor(false, true, true, false, false, 0, false, false, 6, true));
        Assert.Equal(StyleMusic.Crypt, StyleRules.MusicFor(false, false, false, true, false, 0, false, false, double.PositiveInfinity, true));
        Assert.Equal(StyleMusic.Exploration, StyleRules.MusicFor(false, false, false, false, false, 0, false, false, double.NaN, false));
        Assert.Equal(StyleMusic.Village, StyleRules.MusicFallback(StyleMusic.Forge));
        Assert.Null(StyleRules.MusicFallback(StyleMusic.Crypt));
    }

    [Fact]
    public void Ambiances_du_four_du_moulin_des_fetes_et_de_la_crypte()
    {
        Assert.Equal(StyleAmbience.Bakery, StyleRules.AmbienceOf("Assets/Style/Sounds/Boulangerie/eds_boulangerie_FS484338_BonnyOrbit.ogg"));
        Assert.Equal(StyleAmbience.Mill, StyleRules.AmbienceOf("Assets/Style/Sounds/Boulangerie/eds_boulangerie_FS70201_JustinBW.ogg"));
        Assert.Equal(StyleAmbience.Festival, StyleRules.AmbienceOf("Assets/Style/Sounds/Fete/eds_fete_FS40133_inchadney.ogg"));
        Assert.Equal(StyleAmbience.Crypt, StyleRules.AmbienceOf("Assets/Style/Sounds/Donjon/eds_donjon_FS530161_Flamiffer.ogg"));
        // Les autres sons de ces dossiers (portes, chaînes, roue à eau) restent des bruitages, et aucun ne prend une nappe d'avant.
        foreach (string other in new[] { "Assets/Style/Sounds/Donjon/eds_donjon_FS613947_xkeril.ogg", "Assets/Style/Sounds/Donjon/eds_donjon_FS348973_freemaster2.ogg",
            "Assets/Style/Sounds/Boulangerie/eds_boulangerie_FS202764_morturn.ogg", "Assets/Style/Sounds/Fete/eds_fete_FS459162_LudwigMueller.ogg" })
            Assert.Null(StyleRules.AmbienceOf(other));
        Assert.Equal(AmbiencePlace.Mill, StyleRules.PlaceAmbience("moulin") & AmbiencePlace.Mill);
        Assert.Equal(AmbiencePlace.Crypt, StyleRules.PlaceAmbience("crypte_du_seuil") & AmbiencePlace.Crypt);
        Assert.Equal(AmbiencePlace.None, StyleRules.PlaceAmbience("village") & (AmbiencePlace.Mill | AmbiencePlace.Crypt));
        double noon = 12 * 60, night = 2 * 60;
        Assert.True(StyleRules.PlaceVolume(StyleAmbience.Mill, AmbiencePlace.Mill, noon, double.PositiveInfinity, false, false, 0, 0) > 0.5);
        Assert.Equal(0, StyleRules.PlaceVolume(StyleAmbience.Mill, AmbiencePlace.Mill, night, double.PositiveInfinity, false, false, 1, 0));
        Assert.Equal(0, StyleRules.PlaceVolume(StyleAmbience.Festival, AmbiencePlace.Town, noon, double.PositiveInfinity, false, false, 0, 0));
        Assert.True(StyleRules.PlaceVolume(StyleAmbience.Festival, AmbiencePlace.Town | AmbiencePlace.Festival, noon, double.PositiveInfinity, false, false, 0, 0) > 0.5);
        Assert.True(StyleRules.PlaceVolume(StyleAmbience.Crypt, AmbiencePlace.Crypt, night, double.PositiveInfinity, false, true, 0, 0) > 0.5);
        Assert.Equal(0, StyleRules.PlaceVolume(StyleAmbience.Bakery, AmbiencePlace.Town, noon, 0, false, false, 0, 0));   // joué sur le four
    }

    [Fact]
    public void Bruitages_de_la_5e_vague_completent_sans_remplacer()
    {
        string dragon = "Assets/Style/Sounds/FantasySFX_LittleRobot/eds_LRSF_Dragon_Growl_00.ogg";
        Assert.Empty(StyleRules.PickSounds(new[] { dragon }, StyleSound.Growl, 6));
        Assert.Equal(dragon, StyleRules.PickSound(new[] { dragon }, StyleSound.Roar));
        string gold = "Assets/Style/Sounds/FantasySFX_LittleRobot/eds_LRSF_Pickup_Gold_00.ogg", coin = "Assets/Style/Sounds/Kenney/handleCoins.ogg";
        Assert.Equal(gold, StyleRules.PickSound(new[] { gold }, StyleSound.Coin));
        Assert.Equal(coin, StyleRules.PickSound(new[] { gold, coin }, StyleSound.Coin));
        string clash = "Assets/Style/Sounds/Armes/eds_armes_FS361485_FunWithSound.ogg";
        Assert.Equal(clash, StyleRules.PickSound(new[] { clash }, StyleSound.Parry));
        Assert.Equal("Assets/Style/Sounds/Armes/eds_armes_FS812518_DinisNakamura.ogg",
            StyleRules.PickSound(new[] { "Assets/Style/Sounds/Armes/eds_armes_FS812518_DinisNakamura.ogg" }, StyleSound.Block));
    }

    [Fact]
    public void Interieurs_de_Poly_Haven_et_de_Sketchfab_reconnus()
    {
        (string Path, InteriorProp Prop)[] expected =
        {
            ("Assets/Style/PolyHaven/Interieurs2/dining_table/dining_table_1k.fbx", InteriorProp.Table),
            ("Assets/Style/PolyHaven/Interieurs2/folding_wooden_stool/folding_wooden_stool_1k.fbx", InteriorProp.Stool),
            ("Assets/Style/PolyHaven/Interieurs2/old_bed_frame/old_bed_frame_1k.fbx", InteriorProp.Bed),
            ("Assets/Style/PolyHaven/Interieurs2/painted_wooden_chair_01/painted_wooden_chair_01_1k.fbx", InteriorProp.Chair),
            ("Assets/Style/PolyHaven/Interieurs2/vintage_cabinet_01/vintage_cabinet_01_1k.fbx", InteriorProp.Cabinet),
            ("Assets/Style/PolyHaven/Interieurs2/painted_wooden_nightstand/painted_wooden_nightstand_1k.fbx", InteriorProp.Commode),
            ("Assets/Style/PolyHaven/Interieurs2/wooden_bookshelf_worn/wooden_bookshelf_worn_1k.fbx", InteriorProp.Shelves),
            ("Assets/Style/PolyHaven/Interieurs2/vintage_oil_lamp/vintage_oil_lamp_1k.fbx", InteriorProp.Lantern),
            ("Assets/Style/PolyHaven/Interieurs2/Lantern_01/Lantern_01_1k.fbx", InteriorProp.Lantern),
            ("Assets/Style/PolyHaven/Cuisine/brass_pot_02/brass_pot_02_1k.fbx", InteriorProp.Pot),
            ("Assets/Style/PolyHaven/Cuisine/jug_01/jug_01_1k.fbx", InteriorProp.Bottles),
            ("Assets/Style/PolyHaven/Cuisine/wooden_bowl_02/wooden_bowl_02_1k.fbx", InteriorProp.Bowl),
            ("Assets/Style/Sketchfab/Interieurs/eds_SF_Cauldron_DarrenMcnerney3D.fbx", InteriorProp.Pot),
            ("Assets/Style/Sketchfab/Interieurs/eds_SF_Medieval_Chest_aiv.fbx", InteriorProp.Chest),
            ("Assets/Style/Sketchfab/Interieurs/eds_SF_Medieval_Tankard_Kigha.fbx", InteriorProp.Goblets),
        };
        foreach ((string path, InteriorProp prop) in expected)
        {
            Assert.True(InteriorRules.IsInterior(path), path);
            Assert.Equal(prop, InteriorRules.PropOf(path));
            Assert.False(StyleRules.Matches(path, StyleSlot.Rock) || StyleRules.Matches(path, StyleSlot.Tree), path);
        }
        Assert.False(InteriorRules.IsInterior("Assets/Style/PolyHaven/Objets/wooden_picnic_table/wooden_picnic_table_1k.fbx"));
        foreach (Wave5Entry e in Wave5.Entries.Where(e => e.Use == Wave5Use.Interior))
            Assert.True(InteriorRules.IsInterior(e.Files[0]) && InteriorRules.PropOf(e.Files[0]) != InteriorProp.None, e.Files[0]);
    }

    private static readonly string[] Pack =
    {
        "Assets/CraftPix/6200 Fantasy RPG Icons Pack/Weapons/Sword_01.png",
        "Assets/CraftPix/6200 Fantasy RPG Icons Pack/Weapons/Sword_02.png",
        "Assets/CraftPix/6200 Fantasy RPG Icons Pack/Weapons/Axe_01.png",
        "Assets/CraftPix/6200 Fantasy RPG Icons Pack/Weapons/Hatchet_01.png",
        "Assets/CraftPix/6200 Fantasy RPG Icons Pack/Weapons/Mace_01.png",
        "Assets/CraftPix/6200 Fantasy RPG Icons Pack/Armor/Helmet/Icon12.png",
        "Assets/CraftPix/6200 Fantasy RPG Icons Pack/Potions/HealthPotion.png",
        "Assets/CraftPix/6200 Fantasy RPG Icons Pack/Food/Bread.png",
        "Assets/CraftPix/6200 Fantasy RPG Icons Pack/Rings/Ring_Gold.png",
        "Assets/CraftPix/6200 Fantasy RPG Icons Pack/Misc/Fang.png",
        "Assets/CraftPix/6200 Fantasy RPG Icons Pack/Hats/Hat_Witch.png",
        "Assets/Style/Interface/Kenney_game-icons/sword.png",   // pas le pack de CraftPix
    };

    [Fact]
    public void Icones_de_CraftPix_par_table_puis_par_regles_sinon_l_icone_du_jeu()
    {
        Assert.True(ItemIcons.IsPackPath(Pack[0]));
        Assert.True(ItemIcons.IsPackPath("Assets/Fantasy RPG Icons/x.png"));
        Assert.False(ItemIcons.IsPackPath(Pack[^1]));
        Assert.False(ItemIcons.IsPackPath("Assets/CraftPixel.png"));   // le nom du fichier ne suffit pas : un dossier du chemin
        Assert.Contains(ItemIcons.Pick("item.epee_de_garde", Pack), new[] { Pack[0], Pack[1] });
        Assert.Equal(Pack[2], ItemIcons.Pick("item.hache_du_bucheron", Pack));
        Assert.Equal(Pack[3], ItemIcons.Pick("item.hachette_du_bucheron", Pack));
        Assert.Equal(Pack[4], ItemIcons.Pick("item.masse_du_bourg", Pack));
        Assert.Equal(Pack[4], ItemIcons.Pick("item.fleau_du_hameau", Pack));   // fléau → flail, sinon mace
        Assert.Equal(Pack[5], ItemIcons.Pick("item.casque_de_fer", Pack));     // mot du dossier (« Helmet »)
        Assert.Equal(Pack[6], ItemIcons.Pick("item.potion_de_soin", Pack));    // table : healthpotion
        Assert.Equal(Pack[7], ItemIcons.Pick("item.miche_du_matin", Pack));
        Assert.Equal(Pack[8], ItemIcons.Pick("item.anneau_de_fer", Pack));
        Assert.Equal(Pack[9], ItemIcons.Pick("item.collier_de_croc_gris", Pack));
        Assert.Equal(Pack[10], ItemIcons.Pick("item.chapeau_de_lune", Pack));
        Assert.NotEqual(Pack[3], ItemIcons.Pick("item.chapeau_de_lune", Pack));   // « hat » n'est pas « hatchet »
        Assert.Null(ItemIcons.Pick("item.registre_de_garde", Pack));          // rien de tel : l'icône du jeu reste
        Assert.Null(ItemIcons.Pick("item.epee_de_garde", new[] { Pack[^1] })); // sans le pack
        Assert.Null(ItemIcons.Pick("item.epee_de_garde", null!));
        // Stable : même objet, même icône ; deux épées peuvent différer.
        Assert.Equal(ItemIcons.Pick("item.epee_du_capitaine", Pack), ItemIcons.Pick("item.epee_du_capitaine", Pack.Reverse()));
        IReadOnlyList<(string Item, string Icon)> map = ItemIcons.Map(new[] { "item.epee_de_garde", "item.registre_de_garde", "item.anneau_de_fer" }, Pack);
        Assert.Equal(new[] { "item.epee_de_garde", "item.anneau_de_fer" }, map.Select(m => m.Item));
        Assert.Contains("Asset Store", ItemIcons.Manifest("Assets/CraftPix", 2, 3));
        Assert.StartsWith(EssaiManifest.Root + "/Generated/", ItemIcons.LicenseManifest);   // hors du dépôt (Assets/Style est ignoré)
    }

    [Fact]
    public void Chaque_objet_du_jeu_a_des_mots_cherches()
    {
        string items = Doc("data", "v0", "world", "items.json");
        var ids = System.Text.RegularExpressions.Regex.Matches(items, "\"id\": *\"(item\\.[a-z_]+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.True(ids.Count > 100);
        var without = ids.Where(id => ItemIcons.Keywords(id).Count == 0).ToList();
        Assert.True(without.Count == 0, "sans mot : " + string.Join(", ", without));
    }

    [Fact]
    public void Credits_complets_dans_les_deux_fichiers_de_licences()
    {
        string credits = Doc("docs", "CREDITS.md"), licences = Script("Interface", "LICENCES.md");
        foreach (Wave5Credit c in Wave5.Entries.SelectMany(e => e.Credits).Concat(Wave5.Crow.Credits).Concat(Wave5.AudioCredits).Where(c => c.Attribution).Distinct())
        {
            Assert.True(credits.Contains(c.Url), "docs/CREDITS.md : " + c.Url);
            Assert.True(credits.Contains(c.Author), "docs/CREDITS.md : " + c.Author);
            Assert.True(licences.Contains(c.Url), "LICENCES.md : " + c.Url);
        }
        Assert.Contains("6200 Fantasy RPG Icons", credits);
        Assert.Contains("LICENCES_ASSET_STORE.md", credits);
        Assert.Contains("V2.11", Doc("docs", "ESSAI_V1.10.md"));
    }

    [Fact]
    public void La_preparation_et_le_verificateur_lisent_la_liste_partagee()
    {
        string wave5 = Script("Editor", "EdsWave5.cs"), check = Script("Editor", "EdsVerification.cs"), style = Script("Editor", "EdsStyle.cs");
        Assert.Contains("Wave5.Entries", wave5);
        Assert.Contains("Wave5.SkeletonCuts", wave5);
        Assert.Contains("ItemIcons.Map(", wave5);
        Assert.Contains("ItemIcons.LicenseManifest", wave5);
        Assert.Contains("EdsWave5.Prepare(", style);
        Assert.Contains("Wave5.Entries", check);
        Assert.Contains("Wave5.Describe(", check);
        Assert.DoesNotContain("eds_SF_", wave5);   // aucun nom de fichier recopié : la liste est dans EdS.Runtime
        Assert.DoesNotContain("eds_SF_", check);
    }
}
