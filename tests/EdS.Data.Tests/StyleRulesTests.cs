using EdS.Runtime;

namespace EdS.Data.Tests;

/// <summary>
/// Essai de style (décision de Damien du 25 octobre 2026) : règles sans Unity qui choisissent, dans les packs gratuits déposés par Damien,
/// les modèles, les sons et les animations, et qui disent quoi habiller. Noms tirés des packs visés (Quaternius, Kenney).
/// </summary>
[Trait("Category", "Runtime")]
public class StyleRulesTests
{
    [Theory]
    [InlineData("Assets/Style/Knight/FBX/Knight_Golden_Male.fbx", StyleSlot.Hero, true)]
    [InlineData("Assets/Style/Knight/FBX/Sword.fbx", StyleSlot.Hero, false)]
    [InlineData("Assets/Style/Knight/FBX/Knight_Shield.fbx", StyleSlot.Hero, false)]
    [InlineData("Assets/Style/Animals/Wolf.fbx", StyleSlot.Wolf, true)]
    [InlineData("Assets/Style/Wolf/Cat.fbx", StyleSlot.Wolf, false)]      // le dossier ne compte pas, seulement le fichier
    [InlineData("Assets/Style/Farm/Pig.fbx", StyleSlot.Boar, true)]
    [InlineData("Assets/Style/Nature/FBX/CommonTree_1.fbx", StyleSlot.Tree, true)]
    [InlineData("Assets/Style/Nature/FBX/DeadTree_3.fbx", StyleSlot.Tree, false)]
    [InlineData("Assets/Style/Nature/FBX/Rock_Medium_2.fbx", StyleSlot.Rock, true)]
    [InlineData("Assets/Style/Nature/FBX/RockPath_Square_Wide.fbx", StyleSlot.Rock, false)]
    [InlineData("Assets/Style/Nature/FBX/Pebble_Round_1.fbx", StyleSlot.Rock, false)]
    [InlineData("Assets/Style/Nature/FBX/Bush_Common.fbx", StyleSlot.Plant, true)]
    [InlineData("Assets/Style/Nature/FBX/Flower_3_Group.fbx", StyleSlot.Plant, true)]
    [InlineData("Assets/Style/Nature/FBX/Grass_Common_Tall.fbx", StyleSlot.Plant, false)]
    // Essai réaliste (26 octobre 2026) : Mixamo et Poly Haven.
    [InlineData("Assets/Style/Mixamo/Paladin WProp J Nordstrom.fbx", StyleSlot.Hero, true)]
    [InlineData("Assets/Style/Mixamo/Sword And Shield Idle.fbx", StyleSlot.Hero, false)]
    [InlineData("Assets/Style/PolyHaven/rock_moss_set_02/rock_moss_set_02_1k.fbx", StyleSlot.Rock, true)]
    [InlineData("Assets/Style/PolyHaven/grass_medium_01/grass_medium_01_1k.fbx", StyleSlot.Plant, true)]
    [InlineData("Assets/Style/PolyHaven/tree_stump_01/tree_stump_01_1k.fbx", StyleSlot.Tree, false)]
    [InlineData("Assets/Style/PolyHaven/pine_roots/pine_roots_1k.fbx", StyleSlot.Tree, false)]
    [InlineData("Assets/Style/PolyHaven/tree_stump_01/tree_stump_01_1k.fbx", StyleSlot.Debris, true)]
    [InlineData("Assets/Style/PolyHaven/dead_tree_trunk/dead_tree_trunk_1k.fbx", StyleSlot.Debris, true)]
    [InlineData("Assets/Style/PolyHaven/dead_tree_trunk/dead_tree_trunk_1k.fbx", StyleSlot.Tree, false)]
    [InlineData("Assets/Style/PolyHaven/rock_moss_set_01/rock_moss_set_01_1k.fbx", StyleSlot.Plant, false)]   // un rocher moussu n'est pas une plante
    [InlineData("Assets/Style/PolyHaven/moss_01/moss_01_1k.fbx", StyleSlot.Plant, true)]
    [InlineData("Assets/Style/PolyHaven/shrub_03/shrub_03_1k.fbx", StyleSlot.Plant, true)]
    [InlineData("Assets/Style/PolyHaven/celandine_01/celandine_01_1k.fbx", StyleSlot.Plant, true)]
    public void Modeles_reconnus_par_leur_nom(string path, StyleSlot slot, bool expected)
    {
        Assert.Equal(expected, StyleRules.Matches(path, slot));
    }

    [Fact]
    public void Candidats_dans_un_ordre_stable()
    {
        var paths = new[] { "Assets/Style/b/CommonTree_2.fbx", "Assets/Style/a/Pine_1.fbx", "Assets/Style/b/CommonTree_1.fbx", "Assets/Style/a/Wolf.fbx" };
        Assert.Equal(new[] { "Assets/Style/a/Pine_1.fbx", "Assets/Style/b/CommonTree_1.fbx", "Assets/Style/b/CommonTree_2.fbx" },
            StyleRules.Candidates(paths, StyleSlot.Tree));
        Assert.Equal(StyleRules.Candidates(paths.Reverse(), StyleSlot.Tree), StyleRules.Candidates(paths, StyleSlot.Tree));
        Assert.Empty(StyleRules.Candidates(null!, StyleSlot.Hero));
        // Les FBX (animés) passent devant les OBJ du même pack.
        Assert.Equal(new[] { "Assets/Style/Knight/FBX/Knight.FBX", "Assets/Style/Knight/OBJ/Knight.obj" },
            StyleRules.Candidates(new[] { "Assets/Style/Knight/OBJ/Knight.obj", "Assets/Style/Knight/FBX/Knight.FBX" }, StyleSlot.Hero));
    }

    [Fact]
    public void Sons_choisis_dans_les_packs_de_Kenney()
    {
        var sounds = new[]
        {
            "Assets/Style/Impact/impactBell_heavy_000.ogg", "Assets/Style/Impact/impactMetal_light_001.ogg", "Assets/Style/Impact/impactPlate_light_000.ogg",
            "Assets/Style/Impact/impactPunch_heavy_000.ogg", "Assets/Style/Impact/impactPunch_medium_002.ogg", "Assets/Style/Impact/impactSoft_heavy_001.ogg",
            "Assets/Style/Impact/impactMining_003.ogg", "Assets/Style/Impact/impactMetal_heavy_000.ogg", "Assets/Style/RPG/handleSmallLeather.ogg",
        };
        Assert.Equal("Assets/Style/Impact/impactPunch_medium_002.ogg", StyleRules.PickSound(sounds, StyleSound.Hit));
        Assert.Equal("Assets/Style/Impact/impactPlate_light_000.ogg", StyleRules.PickSound(sounds, StyleSound.Block));
        Assert.Equal("Assets/Style/Impact/impactBell_heavy_000.ogg", StyleRules.PickSound(sounds, StyleSound.Parry));
        Assert.Equal("Assets/Style/Impact/impactSoft_heavy_001.ogg", StyleRules.PickSound(sounds, StyleSound.Death));
        Assert.Equal("Assets/Style/RPG/handleSmallLeather.ogg", StyleRules.PickSound(sounds, StyleSound.Potion));
        Assert.Equal("Assets/Style/Impact/impactMining_003.ogg", StyleRules.PickSound(sounds, StyleSound.Gather));
        Assert.Equal("Assets/Style/Impact/impactMetal_heavy_000.ogg", StyleRules.PickSound(sounds, StyleSound.Craft));
        Assert.Null(StyleRules.PickSound(new[] { "Assets/Style/music.ogg" }, StyleSound.Hit));
    }

    [Fact]
    public void Animations_avec_repli()
    {
        var knight = new[] { "CharacterArmature|Idle", "CharacterArmature|Walk", "CharacterArmature|Run", "CharacterArmature|Sword_Attack", "CharacterArmature|Death" };
        Assert.Equal("CharacterArmature|Idle", StyleRules.Clip(knight, StyleMotion.Idle));
        Assert.Equal("CharacterArmature|Walk", StyleRules.Clip(knight, StyleMotion.Walk));
        Assert.Equal("CharacterArmature|Run", StyleRules.Clip(knight, StyleMotion.Run));
        Assert.Equal("CharacterArmature|Sword_Attack", StyleRules.Clip(knight, StyleMotion.Attack));
        Assert.Equal("CharacterArmature|Death", StyleRules.Clip(knight, StyleMotion.Death));
        // Un loup sans marche : la course la remplace ; sans attaque : l'attente.
        var wolf = new[] { "Idle", "Gallop", "Dying" };
        Assert.Equal("Gallop", StyleRules.Clip(wolf, StyleMotion.Walk));
        Assert.Equal("Idle", StyleRules.Clip(wolf, StyleMotion.Attack));
        Assert.Equal("Dying", StyleRules.Clip(wolf, StyleMotion.Death));
        Assert.Null(StyleRules.Clip(new[] { "Jump" }, StyleMotion.Idle));
        // Le chevalier livré (Lowpoly Animated Knight) : noms exacts d'abord ; le loup livré n'a que l'attente et la marche, le cochon
        // que l'attente et le saut : pas d'animation de mort (le jeu couche alors le modèle).
        var shipped = new[] { "HumanArmature|Idle_swordLeft", "HumanArmature|Death", "HumanArmature|Idle", "HumanArmature|Run_swordAttack",
            "HumanArmature|Run", "HumanArmature|Walking", "HumanArmature|swordAttackJump" };
        Assert.Equal("HumanArmature|Idle", StyleRules.Clip(shipped, StyleMotion.Idle));
        Assert.Equal("HumanArmature|Walking", StyleRules.Clip(shipped, StyleMotion.Walk));
        Assert.Equal("HumanArmature|Run", StyleRules.Clip(shipped, StyleMotion.Run));
        Assert.Equal("HumanArmature|Run_swordAttack", StyleRules.Clip(shipped, StyleMotion.Attack));
        Assert.False(StyleRules.Loops("HumanArmature|Run_swordAttack"));
        var wolfShipped = new[] { "WolfArmature|Idle", "WolfArmature|Walking" };
        Assert.Equal("WolfArmature|Walking", StyleRules.Clip(wolfShipped, StyleMotion.Run));
        Assert.Null(StyleRules.Clip(wolfShipped, StyleMotion.Death));
        Assert.Equal("Armature|Idle", StyleRules.Clip(new[] { "Armature|Idle", "Armature|Jump" }, StyleMotion.Walk));
        Assert.Null(StyleRules.Clip(null!, StyleMotion.Walk));
        // Boucles : attente, marche, course ; pas la mort ni l'attaque.
        Assert.True(StyleRules.Loops("Armature|Idle"));
        Assert.True(StyleRules.Loops("Walk"));
        Assert.True(StyleRules.Loops("Run"));
        Assert.False(StyleRules.Loops("Death"));
        Assert.False(StyleRules.Loops("Sword_Attack"));
        Assert.False(StyleRules.Loops("Idle_Attack"));
    }

    [Fact]
    public void Allure_echelle_et_variantes()
    {
        Assert.Equal(StyleMotion.Idle, StyleRules.Motion(0.1, false, false));
        Assert.Equal(StyleMotion.Walk, StyleRules.Motion(2.5, false, false));   // garde : moitié de 5 m/s
        Assert.Equal(StyleMotion.Run, StyleRules.Motion(5, false, false));
        Assert.Equal(StyleMotion.Attack, StyleRules.Motion(5, false, true));
        Assert.Equal(StyleMotion.Death, StyleRules.Motion(5, true, true));
        Assert.Equal(0.9, StyleRules.Scale(2, 1.8), 6);
        Assert.Equal(1.0, StyleRules.Scale(0, 1.8));
        Assert.Equal(1.0, StyleRules.Scale(2, 0));
        Assert.Equal(-1, StyleRules.Variant("arbre1", 0));
        for (int n = 1; n <= 8; n++)
            foreach (string id in new[] { "arbre1", "arbre2", "bloc3", "plante17", "" })
            {
                int v = StyleRules.Variant(id, n);
                Assert.InRange(v, 0, n - 1);
                Assert.Equal(v, StyleRules.Variant(id, n));
            }
        Assert.NotEqual(StyleRules.Variant("arbre1", 360), StyleRules.Variant("arbre2", 360));
    }

    [Fact]
    public void Ce_qui_est_habille()
    {
        Assert.Equal(StyleSlot.Hero, StyleRules.ForCombatant("bastion", true));
        Assert.Equal(StyleSlot.Wolf, StyleRules.ForCombatant("loup1", false));
        Assert.Equal(StyleSlot.Wolf, StyleRules.ForCombatant("loup4_v3", false));   // une vie suivante
        Assert.Equal(StyleSlot.Boar, StyleRules.ForCombatant("sanglier2", false));
        // Essai réaliste (demande de Damien du 26 octobre 2026) : les autres prennent leur personnage Mixamo (s'il existe dans le catalogue).
        Assert.Equal(StyleSlot.Character, StyleRules.ForCombatant("archer", false));
        Assert.Equal(StyleSlot.Character, StyleRules.ForCombatant("gardien_du_seuil", false));
        Assert.Equal("archer", StyleRules.CharacterOf("archer"));
        Assert.Equal("rodeur", StyleRules.CharacterOf("rodeur_v2"));
        Assert.Equal("gardien", StyleRules.CharacterOf("gardien_du_seuil"));
        Assert.Equal("gardien", StyleRules.CharacterOf("gardien_eveille"));
        Assert.Equal("odile", StyleRules.CharacterOf("npc.odile"));
        Assert.Equal("maelle", StyleRules.CharacterOf("npc.maelle"));
        Assert.Equal(string.Empty, StyleRules.CharacterOf("42"));
        Assert.Equal(string.Empty, StyleRules.CharacterOf(null!));
        Assert.Equal(StyleSlot.Tree, StyleRules.ForObstacle("arbre4"));
        Assert.Equal(StyleSlot.Rock, StyleRules.ForObstacle("bloc2"));
        Assert.Equal(StyleSlot.Rock, StyleRules.ForObstacle("pierre_seuil1"));
        Assert.Null(StyleRules.ForObstacle("moulin"));
        Assert.Null(StyleRules.ForObstacle("riviere_nord"));
    }

    [Fact]
    public void Les_packs_ne_partent_pas_sur_GitHub()
    {
        // Les packs sont déposés par Damien pour son essai, dans unity/Assets/Style : ce dossier n'est pas suivi par git.
        string ignore = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, ".gitignore"));
        Assert.Contains("unity/Assets/Style/", ignore, StringComparison.Ordinal);
        Assert.Contains("unity/Assets/Style.meta", ignore, StringComparison.Ordinal);
    }

    [Fact]
    public void Les_modeles_du_decor_gardent_la_coupe_devant_le_joueur()
    {
        // Revue Codex de la PR #55 : un arbre ou un rocher des packs, entre la caméra et le Bastion, est retiré comme le décor simple.
        string view = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "SpaceView.cs"));
        Assert.Contains("AddModelOccluders(tree);", view, StringComparison.Ordinal);
        Assert.Contains("AddModelOccluders(rock);", view, StringComparison.Ordinal);
        Assert.Contains("_occluders.Add(new Occluder { Transform = r.transform, Renderer = r, Lower = false, Min = box.min, Max = box.max });", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Les_sons_ont_un_ecouteur()
    {
        // Revue Codex de la PR #55 : la scène de démarrage est vide ; la caméra créée par le code reçoit un écouteur s'il n'y en a aucun.
        string game = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "EdsGame.cs"));
        Assert.Contains("if (UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null) _camera.Camera.gameObject.AddComponent<AudioListener>();", game, StringComparison.Ordinal);
    }

    [Fact]
    public void Les_modeles_gardent_le_signal_d_etourdissement()
    {
        // Revue Codex de la PR #55 : un loup ou un sanglier habillé clignote en jaune quand il est étourdi (et s'assombrit mort), comme le
        // corps simple, à partir des couleurs d'origine de son modèle.
        string arena = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "ArenaView.cs"));
        string look = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "StyleLook.cs"));
        Assert.Contains("if (_looks.TryGetValue(id, out StyleBody look)) look.Tint(dead, stunned);", arena, StringComparison.Ordinal);
        Assert.Contains("ArenaView.ApplyColor(_materials[i], ArenaView.Shade(_colors[i], dead, stunned));", look, StringComparison.Ordinal);
        Assert.Contains("foreach (Material m in r.materials)", look, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_modele_n_attaque_que_si_la_competence_part()
    {
        // Revue Codex de la PR #55 : une compétence refusée (recharge, emplacement vide, étourdi) ne joue pas l'attaque ; les trois chemins
        // d'entrée (doigt, touches 1 à 4, ultime) passent par le résultat de UseSlot.
        string game = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "EdsGame.cs"));
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(game, @"_arena\.Strike\(\)").Count);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(game, @"&& _combat\.UseSlot\([^;]*\)\)\s*_arena\.Strike\(\);").Count);
    }

    [Fact]
    public void Les_modeles_gardent_leur_racine_importee()
    {
        // Essai de Damien du 26 octobre 2026 : le chevalier, les loups et les sangliers restaient invisibles : la racine du modèle importé
        // (redressée par l'import depuis Blender) était remise à zéro. Le modèle est posé tel quel dans un support, qu'on met à l'échelle.
        string look = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "StyleLook.cs"));
        Assert.Contains("Object.Instantiate(prefab, look.transform, false);", look, StringComparison.Ordinal);
        Assert.DoesNotContain("look.transform.rotation = Quaternion.identity;", look, StringComparison.Ordinal);
        Assert.Contains("skin.updateWhenOffscreen = true;", look, StringComparison.Ordinal);
    }
    [Fact]
    public void Les_modeles_realistes_passent_devant()
    {
        // Essai réaliste : le paladin de Mixamo avant le chevalier des packs, les rochers et les plantes de Poly Haven seuls s'il y en a
        // (essai du 27 octobre 2026 : les buissons rouges du MegaKit détonnaient parmi les plantes réalistes).
        var models = new[]
        {
            "Assets/Style/Knight/KnightCharacter.fbx", "Assets/Style/Mixamo/Paladin WProp J Nordstrom.fbx", "Assets/Style/Mixamo/Sword And Shield Idle.fbx",
            "Assets/Style/Nature/FBX/Rock_Medium_1.fbx", "Assets/Style/PolyHaven/rock_moss_set_02/rock_moss_set_02_1k.fbx",
            "Assets/Style/Nature/FBX/Bush_Common.fbx", "Assets/Style/PolyHaven/grass_medium_01/grass_medium_01_1k.fbx",
        };
        Assert.Equal("Assets/Style/Mixamo/Paladin WProp J Nordstrom.fbx", StyleRules.Candidates(models, StyleSlot.Hero)[0]);
        Assert.Equal(new[] { "Assets/Style/PolyHaven/rock_moss_set_02/rock_moss_set_02_1k.fbx" }, StyleRules.ForCatalog(models, StyleSlot.Rock, 6));
        Assert.Equal(new[] { "Assets/Style/PolyHaven/grass_medium_01/grass_medium_01_1k.fbx" }, StyleRules.ForCatalog(models, StyleSlot.Plant, 12));
        // Sans plante réaliste, celles des packs restent.
        Assert.Equal(new[] { "Assets/Style/Nature/FBX/Bush_Common.fbx" }, StyleRules.ForCatalog(models.Where(m => !m.Contains("PolyHaven")), StyleSlot.Plant, 12));
        Assert.Single(StyleRules.ForCatalog(models, StyleSlot.Plant, 1));
        Assert.Empty(StyleRules.ForCatalog(models, StyleSlot.Plant, -3));
        // Sans rocher réaliste, ceux des packs restent.
        Assert.Equal(new[] { "Assets/Style/Nature/FBX/Rock_Medium_1.fbx" }, StyleRules.ForCatalog(models.Where(m => !m.Contains("PolyHaven")), StyleSlot.Rock, 6));
        // Sans modèle réaliste : le paladin seul n'a pas d'avance sur un autre héros des packs, l'ordre des mots décide (paladin, knight).
        Assert.Equal("Assets/Style/z/Paladin.fbx", StyleRules.Candidates(new[] { "Assets/Style/a/Knight.fbx", "Assets/Style/z/Paladin.fbx" }, StyleSlot.Hero)[0]);
        Assert.True(StyleRules.IsRealistic("Assets\\Style\\Poly Haven\\x.fbx"));
        Assert.False(StyleRules.IsRealistic("Assets/Style/Nature/FBX/Pine_1.fbx"));
    }

    [Fact]
    public void Animations_de_Mixamo_a_cote_du_heros()
    {
        var models = new[]
        {
            "Assets/Style/Mixamo/Sword And Shield Walk.fbx", "Assets/Style/Mixamo/Paladin WProp J Nordstrom.fbx", "Assets/Style/Mixamo/Sword And Shield Idle.fbx",
            "Assets/Style/Mixamo/Sword And Shield Death.fbx", "Assets/Style/Mixamo/notes.txt", "Assets/Style/Animals/Wolf.fbx",
            "Assets/Style/Mixamo/Sous/Sword And Shield Run.fbx",
        };
        Assert.Equal(new[] { "Assets/Style/Mixamo/Sword And Shield Death.fbx", "Assets/Style/Mixamo/Sword And Shield Idle.fbx", "Assets/Style/Mixamo/Sword And Shield Walk.fbx" },
            StyleRules.Companions("Assets/Style/Mixamo/Paladin WProp J Nordstrom.fbx", models));
        Assert.Empty(StyleRules.Companions(null!, models));
        Assert.Empty(StyleRules.Companions("Assets/Style/Animals/Wolf.fbx", models));
        Assert.Equal("Sword And Shield Idle", StyleRules.ClipName("Assets/Style/Mixamo/Sword And Shield Idle.fbx"));
        Assert.Equal("Sans extension", StyleRules.ClipName("Sans extension"));
        // Les animations renommées d'après leur fichier : attente, marche et course en boucle, le coup d'épée pour l'attaque, la mort.
        var clips = new[] { "Sword And Shield Idle", "Sword And Shield Walk", "Sword And Shield Run", "Sword And Shield Slash", "Sword And Shield Death", "mixamo.com" };
        Assert.Equal("Sword And Shield Idle", StyleRules.Clip(clips, StyleMotion.Idle));
        Assert.Equal("Sword And Shield Walk", StyleRules.Clip(clips, StyleMotion.Walk));
        Assert.Equal("Sword And Shield Run", StyleRules.Clip(clips, StyleMotion.Run));
        Assert.Equal("Sword And Shield Slash", StyleRules.Clip(clips, StyleMotion.Attack));
        Assert.Equal("Sword And Shield Death", StyleRules.Clip(clips, StyleMotion.Death));
        Assert.True(StyleRules.Loops("Sword And Shield Idle"));
        Assert.True(StyleRules.Loops("Sword And Shield Walk"));
        Assert.True(StyleRules.Loops("Sword And Shield Run"));
        Assert.False(StyleRules.Loops("Sword And Shield Slash"));
        Assert.False(StyleRules.Loops("Sword And Shield Death"));
        Assert.False(StyleRules.Loops("mixamo.com"));
    }

    [Fact]
    public void Textures_de_sol_et_ciel()
    {
        var textures = new[]
        {
            "Assets/Style/PolyHaven/Ground/forest_ground_04_diff_1k.jpg", "Assets/Style/PolyHaven/Ground/forest_ground_04_nor_gl_1k.jpg",
            "Assets/Style/PolyHaven/Ground/rocky_trail_diff_1k.jpg", "Assets/Style/PolyHaven/Ground/rocky_trail_nor_gl_1k.jpg",
            "Assets/Style/PolyHaven/Ground/rocky_trail_rough_1k.jpg", "Assets/Style/PolyHaven/rock_moss_set_02/textures/rock_moss_set_02_diff_1k.jpg",
            "Assets/Style/PolyHaven/Sky/kloofendal_48d_partly_cloudy_puresky_1k.hdr", "Assets/Style/PolyHaven/grass_medium_01/textures/grass_medium_01_nor_gl_1k.exr",
        };
        Assert.Equal(StyleGround.Soil, StyleRules.GroundOf("Assets/Style/PolyHaven/Ground/forest_ground_04_diff_1k.jpg"));
        Assert.Equal(StyleGround.Rock, StyleRules.GroundOf("Assets/Style/PolyHaven/Ground/rocky_trail_diff_1k.jpg"));
        Assert.Equal(StyleGround.Grass, StyleRules.GroundOf("Assets/Style/PolyHaven/Ground/leafy_grass_diff_1k.jpg"));
        Assert.Null(StyleRules.GroundOf("Assets/Style/PolyHaven/rock_moss_set_02/textures/rock_moss_set_02_diff_1k.jpg"));   // texture d'un modèle
        Assert.Null(StyleRules.GroundOf("Assets/Style/Ground.jpg"));   // le dossier compte, pas le nom
        Assert.Equal("Assets/Style/PolyHaven/Ground/forest_ground_04_diff_1k.jpg", StyleRules.GroundTexture(textures, StyleGround.Soil, false));
        Assert.Equal("Assets/Style/PolyHaven/Ground/forest_ground_04_nor_gl_1k.jpg", StyleRules.GroundTexture(textures, StyleGround.Soil, true));
        Assert.Equal("Assets/Style/PolyHaven/Ground/rocky_trail_diff_1k.jpg", StyleRules.GroundTexture(textures, StyleGround.Rock, false));
        Assert.Equal("Assets/Style/PolyHaven/Ground/rocky_trail_nor_gl_1k.jpg", StyleRules.GroundTexture(textures, StyleGround.Rock, true));
        Assert.Null(StyleRules.GroundTexture(textures, StyleGround.Grass, false));
        Assert.Null(StyleRules.GroundTexture(null!, StyleGround.Grass, false));
        Assert.True(StyleRules.IsNormalMap("grass_medium_01_nor_gl_1k.exr"));
        Assert.True(StyleRules.IsNormalMap("Bark_NormalTree_Normal.png"));
        Assert.False(StyleRules.IsNormalMap("Bark_NormalTree.png"));
        Assert.False(StyleRules.IsColorMap("rocky_trail_rough_1k.jpg"));
        Assert.False(StyleRules.IsColorMap("grass_medium_01_alpha_1k.png"));
        Assert.True(StyleRules.IsColorMap("grass_medium_01_diff_1k.jpg"));
        Assert.True(StyleRules.IsAlphaMap("grass_medium_01_alpha_1k.png"));
        Assert.False(StyleRules.IsAlphaMap("grass_medium_01_diff_1k.jpg"));
        Assert.Equal("Assets/Style/PolyHaven/Sky/kloofendal_48d_partly_cloudy_puresky_1k.hdr", StyleRules.Sky(textures));
        Assert.False(StyleRules.IsSky("Assets/Style/PolyHaven/grass_medium_01/textures/grass_medium_01_nor_gl_1k.exr"));   // une carte, pas un ciel
        Assert.True(StyleRules.IsSky("Assets/Style/Sky/meadow_2k.exr"));
        Assert.False(StyleRules.IsSky("Assets/Style/Sky/sky.jpg"));
        Assert.Null(StyleRules.Sky(null!));
    }

    [Fact]
    public void Couches_du_sol()
    {
        var spots = new[] { new StyleSpot(0, 0, 2, StyleGround.Rock), new StyleSpot(20, 0, 1, StyleGround.Soil) };
        for (double x = -30; x <= 50; x += 1.7)
            for (double z = -30; z <= 30; z += 2.3)
            {
                StyleMix mix = StyleRules.GroundMix(x, z, spots);
                Assert.Equal(1.0, mix.Soil + mix.Rock + mix.Grass, 9);
                Assert.InRange(mix.Soil, 0, 1);
                Assert.InRange(mix.Rock, 0, 1);
                Assert.InRange(mix.Grass, 0, 1);
                Assert.Equal(mix.Rock, StyleRules.GroundMix(x, z, spots).Rock);   // sans hasard
            }
        Assert.Equal(1.0, StyleRules.GroundMix(1, 1, spots).Rock);                 // dans le rayon : roche pleine
        Assert.Equal(1.0, StyleRules.GroundMix(1, 1, spots)[StyleGround.Rock]);
        Assert.InRange(StyleRules.GroundMix(3.5, 0, spots).Rock, 0.49, 0.51);       // à mi-chemin de l'effacement
        Assert.Equal(0.0, StyleRules.GroundMix(6, 0, spots).Rock);                 // au-delà
        Assert.True(StyleRules.GroundMix(20, 0.5, spots).Soil >= 0.85 - 1e-9);    // sous l'arbre : terre
        // Loin de tout : surtout de l'herbe, avec quelques taches de terre.
        double grass = 0, soilPatches = 0;
        int n = 0;
        for (double x = 100; x < 300; x += 1.3)
            for (double z = 100; z < 300; z += 1.3)
            {
                StyleMix mix = StyleRules.GroundMix(x, z, null!);
                grass += mix.Grass;
                if (mix.Soil > 0.3) soilPatches++;
                n++;
            }
        Assert.InRange(grass / n, 0.75, 0.99);
        Assert.True(soilPatches > 0);
        for (double x = -5; x < 5; x += 0.37)
            Assert.InRange(StyleRules.Noise(x, x * 0.7), 0, 1);
    }

    [Fact]
    public void Une_variante_par_lot_de_Poly_Haven()
    {
        // Essai de Damien du 26 octobre 2026 : rangées de pissenlits, arbustes alignés, arbres du MegaKit coupés. Noms tirés des fichiers
        // livrés (Poly Haven, 1k).
        var dandelion = new[]
        {
            "dandelion_01_a_LOD0", "dandelion_01_c_LOD0", "dandelion_01_d_LOD0", "dandelion_01_b_LOD0", "dandelion_01_e_LOD0",
            "dandelion_01_a_LOD1", "dandelion_01_c_LOD1", "dandelion_01_d_LOD1", "dandelion_01_b_LOD1", "dandelion_01_e_LOD1",
        };
        IReadOnlyList<int>? keep = StyleRules.KeepParts("dandelion_01_1k", dandelion, "plante3");
        Assert.NotNull(keep);
        Assert.Single(keep!);
        Assert.EndsWith("_LOD0", dandelion[keep![0]], StringComparison.Ordinal);   // le meilleur niveau de détail
        Assert.Equal(keep, StyleRules.KeepParts("dandelion_01_1k", dandelion, "plante3"));   // même choix à chaque lancement
        var chosen = new HashSet<string>();
        for (int i = 0; i < 40; i++) chosen.Add(dandelion[StyleRules.KeepParts("dandelion_01_1k", dandelion, "plante" + i)![0]]);
        Assert.True(chosen.Count >= 3, "les éléments du décor varient d'une variante à l'autre");
        // Un rocher seul et ses niveaux de détail : le niveau 0 seulement.
        var rock = new[] { "rock_07_LOD0", "rock_07_LOD1", "rock_07_LOD2", "rock_07_LOD3" };
        Assert.Equal(new[] { 0 }, StyleRules.KeepParts("rock_07_1k", rock, "bloc1"));
        // Un lot de rochers sans niveaux de détail : un rocher.
        var set = new[] { "rock_moss_set_01_rock01", "rock_moss_set_01_rock02", "rock_moss_set_01_rock03", "rock_moss_set_01_rock04" };
        Assert.Single(StyleRules.KeepParts("rock_moss_set_01_1k", set, "bloc2")!);
        // Hors de Poly Haven (MegaKit, Mixamo) ou une seule pièce : tout garder.
        Assert.Null(StyleRules.KeepParts("Pine_1", new[] { "Pine_1_Trunk", "Pine_1_Leaves" }, "arbre1"));
        Assert.Null(StyleRules.KeepParts("dead_tree_trunk_1k", new[] { "dead_tree_trunk" }, "lisiere3"));
        Assert.Null(StyleRules.KeepParts("dandelion_01_1k", null!, "x"));
        Assert.True(StyleRules.IsPolyHavenName("shrub_02_1k"));
        Assert.True(StyleRules.IsPolyHavenName("rock_07_4K"));
        Assert.False(StyleRules.IsPolyHavenName("Pine_1"));
        Assert.False(StyleRules.IsPolyHavenName("Paladin WProp J Nordstrom"));
        // Tailles : la mousse (4 cm) et une touffe d'herbe gardent leur taille réelle ; une plante de 4 m est ramenée à la hauteur voulue.
        Assert.Equal(1.0, StyleRules.NaturalScale(0.04, 0.8));
        Assert.Equal(1.0, StyleRules.NaturalScale(0.4, 0.8));
        Assert.Equal(0.2, StyleRules.NaturalScale(4, 0.8), 9);
        Assert.Equal(1.0, StyleRules.NaturalScale(0, 0.8));
        // Textures de Mixamo : la carte spéculaire n'est pas une couleur.
        Assert.False(StyleRules.IsColorMap("Paladin_specular.png"));
        Assert.True(StyleRules.IsColorMap("Paladin_diffuse.png"));
        Assert.True(StyleRules.IsNormalMap("Paladin_normal.png"));
    }

    [Fact]
    public void Foret_autour_de_l_aire()
    {
        IReadOnlyList<(double X, double Z)> forest = StyleRules.Forest(-30, -20, 30, 20, 45, 110);
        Assert.InRange(forest.Count, 60, 110);
        foreach ((double x, double z) in forest)
        {
            Assert.False(x > -34 && x < 34 && z > -24 && z < 24, $"arbre dans l'aire ou trop près du bord : {x}, {z}");
            Assert.InRange(x, -75, 75);
            Assert.InRange(z, -65, 65);
        }
        for (int i = 0; i < forest.Count; i++)
            for (int j = i + 1; j < forest.Count; j++)
                Assert.True((forest[i].X - forest[j].X) * (forest[i].X - forest[j].X) + (forest[i].Z - forest[j].Z) * (forest[i].Z - forest[j].Z) >= 3.5 * 3.5 - 1e-9);
        Assert.Equal(forest, StyleRules.Forest(-30, -20, 30, 20, 45, 110));   // mêmes places à chaque lancement
        Assert.Empty(StyleRules.Forest(-30, -20, 30, 20, 45, 0));
        Assert.Empty(StyleRules.Forest(-30, -20, 30, 20, 3, 50));             // bande plus étroite que l'écart au bord
        Assert.Equal(StyleGround.Rock, StyleRules.GroundFor("bloc3"));
        Assert.Equal(StyleGround.Rock, StyleRules.GroundFor("pierre_seuil2"));
        Assert.Equal(StyleGround.Soil, StyleRules.GroundFor("mur_est"));          // palissade : terre battue au pied (27 octobre 2026)
        Assert.Equal(StyleGround.Soil, StyleRules.GroundFor("haie_ouest"));
        Assert.Equal(StyleGround.Rock, StyleRules.GroundFor("pilier1"));
        Assert.Equal(StyleGround.Soil, StyleRules.GroundFor("arbre7"));
        Assert.Equal(StyleGround.Soil, StyleRules.GroundFor("moulin"));
        Assert.Null(StyleRules.GroundFor("riviere_nord"));
    }

    [Fact]
    public void Le_jeu_branche_l_essai_realiste()
    {
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Lot93UnityPackageTests.Root }.Concat(parts).ToArray()));
        string view = Read("unity", "Assets", "EdS", "Game", "SpaceView.cs");
        string game = Read("unity", "Assets", "EdS", "Game", "EdsGame.cs");
        string world = Read("unity", "Assets", "EdS", "Game", "StyleWorld.cs");
        string look = Read("unity", "Assets", "EdS", "Game", "StyleLook.cs");
        string editor = Read("unity", "Assets", "EdS", "Editor", "EdsStyle.cs");
        // Le terrain remplace le sol simple (et ses données sont rendues), la forêt pousse seulement sur le terrain.
        Assert.Contains("_terrain = StyleWorld.Ground(_root.transform, minX, minZ, maxX, maxZ, Spots(space),", view, StringComparison.Ordinal);
        Assert.Contains("if (terrain) Forest(space);", view, StringComparison.Ordinal);
        Assert.Contains("? StyleLook.Decor(_root.transform, StyleSlot.Debris, id, at, 1f, 0f, true)", view, StringComparison.Ordinal);
        // Revue Codex de la PR #56 : les arbres de la lisière sont coupés devant le Bastion comme ceux des obstacles.
        Assert.Contains("if (look != null) AddModelOccluders(look);", view, StringComparison.Ordinal);
        Assert.Contains("StyleWorld.Release(_terrain);", view, StringComparison.Ordinal);
        // Ambiance posée au démarrage, réglable pendant le jeu dans l'éditeur ; le halo seulement sur ordinateur.
        Assert.Contains("StyleWorld.Ambience(_camera.Camera);", game, StringComparison.Ordinal);
        Assert.Contains("if (Application.isEditor) StyleWorld.Refresh();", game, StringComparison.Ordinal);
        Assert.Contains("_bloom.active = !Application.isMobilePlatform && catalog.Bloom > 0f;", world, StringComparison.Ordinal);
        // Les couches du terrain suivent l'ordre de StyleGround.
        Assert.Contains("maps[row, col, (int)StyleGround.Soil] = (float)mix.Soil;", world, StringComparison.Ordinal);
        Assert.Equal(0, (int)StyleGround.Soil);
        Assert.Equal(1, (int)StyleGround.Rock);
        Assert.Equal(2, (int)StyleGround.Grass);
        // Animations Mixamo ajoutées au héros ; une seule pièce d'un lot de Poly Haven.
        Assert.Contains("animation.AddClip(clip, clip.name);", look, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<int> keep = StyleRules.KeepParts(prefab.name, names, part);", look, StringComparison.Ordinal);
        Assert.Contains("foreach (LODGroup group in look.GetComponentsInChildren<LODGroup>()) group.enabled = false;", look, StringComparison.Ordinal);
        Assert.Contains("else renderers[i].gameObject.SetActive(false);", look, StringComparison.Ordinal);
        // Essai du 26 octobre 2026 : paladin blanc (textures sorties de son fichier), image trop claire (réglages remis une fois par version).
        Assert.Contains("MixamoMaterials(hero, notes);", editor, StringComparison.Ordinal);
        Assert.Contains("if (catalog.AmbienceVersion < StyleCatalog.AmbienceDefaults)", editor, StringComparison.Ordinal);
        // L'outil de préparation rebranche les effets d'image d'URP (coupés par le renderer créé au sous-lot 9.3).
        Assert.Contains("renderer.postProcessData = data;", editor, StringComparison.Ordinal);
        Assert.Contains("Legacy(clip, true);", editor, StringComparison.Ordinal);
        // Dossier temporaire de Unity 6 vu chez Damien : jamais suivi.
        Assert.Contains("unity/[Uu]tmp/", Read(".gitignore"), StringComparison.Ordinal);
    }
    [Fact]
    public void Personnages_de_Mixamo_par_dossier()
    {
        var models = new[]
        {
            "Assets/Style/Mixamo/Paladin WProp J Nordstrom.fbx", "Assets/Style/Mixamo/Sword And Shield Idle.fbx",
            "Assets/Style/Mixamo/Archer/Archer Walk.fbx", "Assets/Style/Mixamo/Archer/Archer Idle.fbx", "Assets/Style/Mixamo/Archer/Archer Death.fbx",
            "Assets/Style/Mixamo/Odile/Odile Idle.fbx", "Assets/Style/Mixamo/Textures/x.fbx", "Assets/Style/Mixamo/Odile/notes.txt",
            "Assets\\Style\\Mixamo\\Gardien\\Gardien Idle.fbx", "Assets/Style/Animals/Wolf.fbx",
        };
        IReadOnlyDictionary<string, IReadOnlyList<string>> characters = StyleRules.Characters(models);
        Assert.Equal(new[] { "archer", "gardien", "odile" }, characters.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(new[] { "Assets/Style/Mixamo/Archer/Archer Death.fbx", "Assets/Style/Mixamo/Archer/Archer Idle.fbx", "Assets/Style/Mixamo/Archer/Archer Walk.fbx" },
            characters["archer"]);
        Assert.Single(characters["odile"]);
        Assert.Empty(StyleRules.Characters(null!));
        // Les fichiers des personnages, nommés d'après leur mouvement, donnent leurs animations ; le héros n'en prend aucune.
        Assert.DoesNotContain(StyleRules.Companions("Assets/Style/Mixamo/Paladin WProp J Nordstrom.fbx", models), m => m.Contains("Archer"));
        var archerClips = new[] { "Archer Idle", "Archer Walk", "Archer Death" };
        Assert.Equal("Archer Idle", StyleRules.Clip(archerClips, StyleMotion.Idle));
        Assert.Equal("Archer Walk", StyleRules.Clip(archerClips, StyleMotion.Run));
        Assert.Equal("Archer Death", StyleRules.Clip(archerClips, StyleMotion.Death));
        Assert.Equal("Archer Idle", StyleRules.Clip(archerClips, StyleMotion.Attack));   // pas d'attaque : l'attente
        Assert.True(StyleRules.Loops("Archer Walk"));
        Assert.True(StyleRules.Loops("Gardien Idle"));
        Assert.False(StyleRules.Loops("Gardien Death"));
        // « Gardien » contient « die » au milieu d'un mot : ce n'est pas une mort.
        Assert.Null(StyleRules.Clip(new[] { "Gardien Idle", "Gardien Walk" }, StyleMotion.Death));
        Assert.Equal("Gardien Walk", StyleRules.Clip(new[] { "Gardien Idle", "Gardien Walk" }, StyleMotion.Run));
        // Textures des matériaux de Mixamo (noms tirés des fichiers livrés).
        var archer = new[] { "Textures/Akai_DIFF.png", "Textures/Akai_NM.png", "Textures/Body_DIFF.jpg", "Textures/Body_NM.jpg", "Textures/Bow_DIFF.jpg", "Textures/Bow_NM.jpg" };
        Assert.Equal("Textures/Body_DIFF.jpg", StyleRules.MaterialTexture("Body_MAT1", archer, false));
        Assert.Equal("Textures/Body_NM.jpg", StyleRules.MaterialTexture("Body_MAT1", archer, true));
        Assert.Equal("Textures/Bow_DIFF.jpg", StyleRules.MaterialTexture("Bow_MAT", archer, false));
        Assert.Null(StyleRules.MaterialTexture("Cape_MAT", archer, false));                       // plusieurs textures, aucune à son nom
        var warrok = new[] { "Textures/bear_diffuse.png", "Textures/bear_normal.png", "Textures/bear_specular.png" };
        Assert.Equal("Textures/bear_diffuse.png", StyleRules.MaterialTexture("warrok_MAT", warrok, false));   // la seule couleur
        Assert.Equal("Textures/bear_normal.png", StyleRules.MaterialTexture("warrok_MAT", warrok, true));
        Assert.Equal("t/maria_diffuse.png", StyleRules.MaterialTexture("maria_M1", new[] { "t/maria_diffuse.png", "t/maria_normal.png", "t/other_diffuse.png" }, false));
        Assert.False(StyleRules.IsColorMap("Nightshade_glow.png"));
        Assert.True(StyleRules.IsNormalMap("Bow_NM.jpg"));
        Assert.Null(StyleRules.MaterialTexture("x", null!, false));
    }

    [Fact]
    public void Matieres_des_batiments()
    {
        var textures = new[]
        {
            "Assets/Style/PolyHaven/Surfaces/rustic_stone_wall_02_diff_1k.jpg", "Assets/Style/PolyHaven/Surfaces/rustic_stone_wall_02_nor_gl_1k.jpg",
            "Assets/Style/PolyHaven/Surfaces/plastered_stone_wall_diff_1k.jpg", "Assets/Style/PolyHaven/Surfaces/clay_roof_tiles_02_diff_1k.jpg",
            "Assets/Style/PolyHaven/Surfaces/weathered_brown_planks_diff_1k.jpg", "Assets/Style/PolyHaven/Surfaces/cobblestone_floor_04_diff_1k.jpg",
            "Assets/Style/PolyHaven/Ground/rocky_trail_diff_1k.jpg",
        };
        Assert.Equal(StyleSurface.Stone, StyleRules.SurfaceOf(textures[0]));
        Assert.Equal(StyleSurface.Plaster, StyleRules.SurfaceOf(textures[2]));   // « plastered_stone_wall » : du crépi
        Assert.Equal(StyleSurface.Roof, StyleRules.SurfaceOf(textures[3]));
        Assert.Equal(StyleSurface.Wood, StyleRules.SurfaceOf(textures[4]));
        Assert.Equal(StyleSurface.Cobble, StyleRules.SurfaceOf(textures[5]));    // « cobblestone » : des pavés
        Assert.Null(StyleRules.SurfaceOf(textures[6]));                          // un sol, pas un bâtiment
        Assert.Null(StyleRules.SurfaceOf("Assets/Style/Surfaces/readme_1k.jpg"));
        Assert.Equal(textures[0], StyleRules.SurfaceTexture(textures, StyleSurface.Stone, false));
        Assert.Equal(textures[1], StyleRules.SurfaceTexture(textures, StyleSurface.Stone, true));
        Assert.Null(StyleRules.SurfaceTexture(textures, StyleSurface.Roof, true));
        Assert.Null(StyleRules.SurfaceTexture(null!, StyleSurface.Roof, false));
        Assert.Equal(StyleSurface.Bark, StyleRules.SurfaceFor("mur_nord"));       // palissade de rondins (choix de Damien du 27 octobre 2026)
        Assert.Equal(StyleSurface.Bark, StyleRules.SurfaceFor("porte_moulin"));
        Assert.Equal(StyleSurface.Wood, StyleRules.SurfaceFor("pilier3"));
        Assert.Null(StyleRules.SurfaceFor("moulin"));
        Assert.Null(StyleRules.SurfaceFor("riviere_sud"));
    }

    [Fact]
    public void Camera_a_la_souris_libre()
    {
        // Choix de Damien du 26 octobre 2026 (« Souris libre (comme ESO) ») : sans clic, hors des écrans, Alt rend le curseur.
        Assert.True(PointerInput.FreeLook(touchCount: 0, touchControls: false, alt: false, covered: false, focused: true));
        Assert.False(PointerInput.FreeLook(0, false, true, false, true));    // Alt tenu : curseur pour cliquer
        Assert.False(PointerInput.FreeLook(0, false, false, true, true));    // écran ou menu ouvert
        Assert.False(PointerInput.FreeLook(0, true, false, false, true));    // commandes tactiles affichées
        Assert.False(PointerInput.FreeLook(1, false, false, false, true));   // un doigt sur l'écran
        Assert.False(PointerInput.FreeLook(0, false, false, false, false));  // fenêtre du jeu inactive
        string game = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "EdsGame.cs"));
        string rig = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "CameraRig.cs"));
        Assert.Contains("Cursor.lockState = _freeLook ? CursorLockMode.Locked : CursorLockMode.None;", game, StringComparison.Ordinal);
        Assert.Contains("bool covered = _talents != null || _menuOpen || _paused || AdventureCovered || LobbyShown;", game, StringComparison.Ordinal);
        // Revue Codex de la PR #57 : la salle d'attente du réseau garde le curseur pour ses boutons ; même condition que son affichage.
        Assert.Contains("if (LobbyShown)\n            {\n                // Salle d'attente", game.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("private bool LobbyShown => Network && _talents == null && (_net.Client == null || _net.Client.Status != ClientStatus.Combat", game, StringComparison.Ordinal);
        Assert.Contains("_freeLook = PointerInput.FreeLook(Input.touchCount, _touch.Active, cursorWanted, covered, Application.isFocused);", game, StringComparison.Ordinal);
        Assert.Contains("if (Input.GetKeyDown(KeyCode.Escape)) _cursorReleased = true;", game, StringComparison.Ordinal);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(game, @"Time\.deltaTime, _freeLook\)").Count);
        Assert.Contains("_yaw += Input.GetAxis(\"Mouse X\") * 3f;", rig, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_jeu_habille_personnages_batiments_et_riviere()
    {
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Lot93UnityPackageTests.Root }.Concat(parts).ToArray()));
        string markers = Read("unity", "Assets", "EdS", "Game", "WorldMarkers.cs");
        string view = Read("unity", "Assets", "EdS", "Game", "SpaceView.cs");
        string look = Read("unity", "Assets", "EdS", "Game", "StyleLook.cs");
        string editor = Read("unity", "Assets", "EdS", "Editor", "EdsStyle.cs");
        Assert.Contains("StyleBody look = StyleLook.Dress(m.Body, StyleSlot.Character, npc.Id, 1.75f);", markers, StringComparison.Ordinal);
        Assert.Contains("_water = StyleWorld.Water() ?? ArenaView.MakeMaterial(", view, StringComparison.Ordinal);
        Assert.Contains("StyleWorld.AnimateWater(Time.time);", view, StringComparison.Ordinal);
        Assert.Contains("mesh.uv = uvs.ToArray();", view, StringComparison.Ordinal);
        foreach (string surface in new[] { "StyleSurface.Cobble", "StyleSurface.Plaster", "StyleSurface.Roof", "StyleSurface.Wood", "StyleSurface.Stone" })
            Assert.Contains("Built(" + surface, view, StringComparison.Ordinal);
        Assert.Contains("ArenaView.ApplyColor(m, slot == StyleSlot.Wolf ? catalog.WolfColor : catalog.BoarColor);", look, StringComparison.Ordinal);
        Assert.Contains("AnimationClip[] clips = catalog.Clips(slot, id);", look, StringComparison.Ordinal);
        Assert.Contains("foreach (KeyValuePair<string, IReadOnlyList<string>> folder in StyleRules.Characters(models))", editor, StringComparison.Ordinal);
        Assert.Contains("MixamoMaterials(hero, notes);", editor, StringComparison.Ordinal);
        Assert.Contains("AssetDatabase.ImportAsset(model, ImportAssetOptions.ForceUpdate);", editor, StringComparison.Ordinal);
        // Revue Codex de la PR #57 (2e tour) : un matériau Mixamo texturé mais hors d'URP est refait aussi (il serait rose), avec ses
        // propres textures ; les prismes texturés ont leurs tangentes (cartes de relief des murs et des vagues).
        Assert.Contains("Where(m => !HasTexture(m) || !IsUrp(m.shader))", editor, StringComparison.Ordinal);
        Assert.Contains("shader.isSupported && shader.name.StartsWith(\"Universal Render Pipeline/\", System.StringComparison.Ordinal)", editor, StringComparison.Ordinal);
        Assert.Contains("Texture2D color = OwnTexture(embedded, \"_BaseMap\", \"_MainTex\");", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("if (!Untextured(modelPath)) return;", editor, StringComparison.Ordinal);
        Assert.Contains("mesh.RecalculateNormals();\n            // Revue Codex de la PR #57 (2e tour) : les cartes de relief (murs, tuiles, planches, vagues) ont besoin des tangentes.\n            mesh.RecalculateTangents();",
            view.Replace("\r\n", "\n"), StringComparison.Ordinal);
    }

    [Fact]
    public void Objets_de_la_forge_du_pont_et_de_l_herbe_de_l_Aube()
    {
        // Demandes de Damien du 27 octobre 2026 : forge, minerais, herbe de l'Aube, pont, moulin. Noms des fichiers téléchargés.
        var models = new[]
        {
            "Assets/Style/PolyHaven/stone_fire_pit/stone_fire_pit_1k.fbx", "Assets/Style/PolyHaven/cross_pein_hammer/cross_pein_hammer_1k.fbx",
            "Assets/Style/PolyHaven/wooden_crate_02/wooden_crate_02_1k.fbx", "Assets/Style/PolyHaven/barrel_03/barrel_03_1k.fbx",
            "Assets/Style/PolyHaven/modular_wooden_pier/modular_wooden_pier_1k.fbx", "Assets/Style/PolyHaven/periwinkle_plant/periwinkle_plant_1k.fbx",
            "Assets/Style/Sketchfab/Windmill/windmill.fbx", "Assets/Style/PolyHaven/rock_07/rock_07_1k.fbx", "Assets/Style/Nature/FBX/Bush_Common.fbx",
            "Assets/Style/PolyHaven/shrub_02/shrub_02_1k.fbx",
        };
        Assert.Equal(new[] { models[0] }, StyleRules.PropCandidates(models, StyleProp.FirePit));
        Assert.Equal(new[] { models[1] }, StyleRules.PropCandidates(models, StyleProp.Hammer));
        Assert.Equal(new[] { models[2] }, StyleRules.PropCandidates(models, StyleProp.Crate));
        Assert.Equal(new[] { models[3] }, StyleRules.PropCandidates(models, StyleProp.Barrel));
        Assert.Equal(new[] { models[4] }, StyleRules.PropCandidates(models, StyleProp.Bridge));
        Assert.Equal(new[] { models[5] }, StyleRules.PropCandidates(models, StyleProp.Herb));
        Assert.Equal(new[] { models[6] }, StyleRules.PropCandidates(models, StyleProp.Mill));
        Assert.Empty(StyleRules.PropCandidates(models, StyleProp.Anvil));     // pas d'enclume : faite par le code
        Assert.Empty(StyleRules.PropCandidates(null!, StyleProp.Mill));
        Assert.True(StyleRules.IsRealistic(models[6]));                         // Sketchfab : source réaliste
        // Le foyer « stone_fire_pit » n'est pas un rocher ; la pervenche n'est pas une plante du décor.
        Assert.Equal(new[] { models[7] }, StyleRules.ForCatalog(models, StyleSlot.Rock, 6));
        Assert.DoesNotContain(models[5], StyleRules.ForCatalog(models, StyleSlot.Plant, 16));
        // Arbustes des haies : ceux de Poly Haven seuls (pas le buisson rouge du MegaKit).
        Assert.Equal(new[] { models[9] }, StyleRules.ForCatalog(models, StyleSlot.Shrub, 6));
        Assert.Equal(new[] { models[8] }, StyleRules.ForCatalog(models.Where(m => !m.Contains("PolyHaven")), StyleSlot.Shrub, 6));
        Assert.False(StyleRules.Matches("Assets/Style/Mixamo/Odile/Odile Idle.fbx", StyleSlot.Character));
        // Textures du ponton (deux matières) et de la pervenche : la translucidité n'est pas une couleur.
        var pier = new[]
        {
            "textures/modular_wooden_pier_planks_diff_1k.png", "textures/modular_wooden_pier_planks_nor_gl_1k.png", "textures/modular_wooden_pier_planks_rough_1k.png",
            "textures/modular_wooden_pier_poles_diff_1k.png", "textures/modular_wooden_pier_poles_nor_gl_1k.png",
        };
        Assert.Equal(pier[3], StyleRules.MaterialTexture("modular_wooden_pier_poles", pier, false));
        Assert.Equal(pier[1], StyleRules.MaterialTexture("modular_wooden_pier_planks", pier, true));
        Assert.False(StyleRules.IsColorMap("periwinkle_plant_translucency_1k.png"));
        Assert.Equal(StyleSurface.Bark, StyleRules.SurfaceOf("Assets/Style/PolyHaven/Surfaces/bark_brown_02_diff_1k.jpg"));
        Assert.True(StyleRules.IsNormalMap("Assets/Style/PolyHaven/Surfaces/bark_brown_02_nor_gl_1k.jpg"));
    }

    [Fact]
    public void Palissade_de_rondins_et_haie()
    {
        // Choix de Damien du 27 octobre 2026 : le mur près de l'archer devient une palissade de rondins taillés en pointe.
        Assert.Equal(StyleFence.Palisade, StyleRules.FenceFor("mur_nord"));
        Assert.Equal(StyleFence.Palisade, StyleRules.FenceFor("porte_moulin"));
        Assert.Equal(StyleFence.Hedge, StyleRules.FenceFor("haie_est"));
        Assert.Equal(StyleFence.None, StyleRules.FenceFor("moulin"));
        Assert.Equal(StyleFence.None, StyleRules.FenceFor("pilier2"));
        // « mur_nord » des Vallons : x 17..18, z 1,5..14 (1 m d'épaisseur, 12,5 m de long).
        IReadOnlyList<StyleLog> logs = StyleRules.Palisade(17, 1.5, 18, 14, "mur_nord");
        Assert.True(logs.Count > 70);
        foreach (StyleLog log in logs)
        {
            Assert.InRange(log.X - log.Radius, 17 - 1e-9, 18 + 1e-9);                // dans l'emprise du moteur
            Assert.InRange(log.Z - log.Radius, 1.5 - 1e-9, 14 + 1e-9);
            Assert.InRange(log.Z + log.Radius, 1.5 - 1e-9, 14 + 1e-9);
            Assert.InRange(log.Height, 2.6, 3.1 + 1e-9);
        }
        Assert.Equal(2, logs.Select(l => Math.Round(l.X, 6)).Distinct().Count());      // deux rangs décalés
        Assert.Equal(logs, StyleRules.Palisade(17, 1.5, 18, 14, "mur_nord"));         // les mêmes à chaque lancement
        // Emprise mince : un seul rang au milieu ; le long de x si c'est le grand côté ; rien si trop petite.
        IReadOnlyList<StyleLog> thin = StyleRules.Palisade(0, 0, 6, 0.3, "porte");
        Assert.All(thin, l => Assert.Equal(0.15, l.Z, 6));
        Assert.Empty(StyleRules.Palisade(0, 0, 0.2, 0.2, "x"));
        Assert.Empty(StyleRules.Palisade(0, 0, 5, 0, "x"));
        // Haie : un arbuste par mètre environ, au milieu de l'emprise.
        var row = StyleRules.Row(-24, 19, -6, 20, 1.1);
        Assert.Equal(16, row.Count);
        Assert.All(row, p => Assert.Equal(19.5, p.Z, 6));
        Assert.InRange(row[0].X, -24, -23);
        Assert.InRange(row[row.Count - 1].X, -7, -6);
        Assert.Single(StyleRules.Row(0, 0, 0.5, 0.5, 2));
    }

    [Fact]
    public void Lit_de_la_riviere_et_berges()
    {
        // Essai de Damien du 27 octobre 2026 (« l'eau n'est pas du tout réaliste ») : lit creusé sous la rivière, berges en pente douce.
        var square = new List<(double X, double Z)> { (4, -18), (7, -18), (7, -2), (4, -2) };
        var reversed = square.AsEnumerable().Reverse().ToList();
        Assert.Equal(1.5, StyleRules.InsideDistance(5.5, -10, square), 9);
        Assert.Equal(1.5, StyleRules.InsideDistance(5.5, -10, reversed), 9);     // sens des sommets indifférent
        Assert.Equal(0.5, StyleRules.InsideDistance(4.5, -10, square), 9);
        Assert.Equal(-2, StyleRules.InsideDistance(2, -10, square), 9);
        Assert.True(double.IsNegativeInfinity(StyleRules.InsideDistance(0, 0, new List<(double X, double Z)> { (0, 0), (1, 0) })));
        Assert.Equal(0, StyleRules.ChannelDepth(-1));
        Assert.Equal(0, StyleRules.ChannelDepth(0));
        Assert.Equal(StyleRules.RiverBed, StyleRules.ChannelDepth(StyleRules.RiverBank), 9);
        Assert.Equal(StyleRules.RiverBed, StyleRules.ChannelDepth(5), 9);
        double last = 0;
        for (double d = 0; d <= StyleRules.RiverBank; d += 0.05)
        {
            Assert.True(StyleRules.ChannelDepth(d) >= last);
            last = StyleRules.ChannelDepth(d);
        }
        // La surface de l'eau est sous le sol où l'on marche, au-dessus du lit.
        Assert.InRange(StyleRules.WaterLevel, -StyleRules.RiverBed, -0.05);
        // Terre mouillée sur la berge et dans le lit, sans effet loin du bord ; la somme reste 1.
        var grass = new StyleMix(0, 0, 1);
        Assert.Equal(1, StyleRules.RiverMix(grass, 2).Soil, 9);
        Assert.Equal(1, StyleRules.RiverMix(grass, -2).Grass, 9);
        StyleMix shore = StyleRules.RiverMix(new StyleMix(0.2, 0.3, 0.5), 0);
        Assert.Equal(1, shore.Soil + shore.Rock + shore.Grass, 9);
        Assert.True(shore.Soil > 0.2);
    }

    [Fact]
    public void Les_PNJ_se_tournent_vers_le_joueur()
    {
        // Essai de Damien du 27 octobre 2026 : « Odile est tournée du mauvais côté ».
        Assert.Equal(0, StyleRules.FaceYaw(0, 0, 0, 5), 9);       // +z
        Assert.Equal(90, StyleRules.FaceYaw(0, 0, 3, 0), 9);      // +x
        Assert.Equal(180, Math.Abs(StyleRules.FaceYaw(0, 0, 0, -1)), 9);
        Assert.Equal(-90, StyleRules.FaceYaw(2, 2, 1, 2), 9);
        Assert.Equal(30, StyleRules.Turn(0, 90, 30), 9);
        Assert.Equal(90, StyleRules.Turn(80, 90, 30), 9);         // n'outrepasse pas la cible
        Assert.Equal(-170, StyleRules.Turn(170, -170, 30), 9);    // par le plus court, à travers 180
        Assert.Equal(180, StyleRules.Turn(-170, 170, 10), 9);     // -170 → 170 : 20 degrés par -180, pas 340 dans l'autre sens
        Assert.Equal(-60, StyleRules.Turn(300, 0, 0), 9);         // ramené dans ]-180, 180]
        Assert.True(StyleRules.FaceRange > 3);
    }

    [Fact]
    public void Pont_fait_d_un_troncon_du_ponton()
    {
        var parts = new[]
        {
            "modular_wooden_pier_section_03", "modular_wooden_pier_planks", "modular_wooden_pier_section_01", "modular_wooden_pier_poles",
            "modular_wooden_pier_section_05",
        };
        Assert.Equal(2, StyleRules.BridgeSection(parts));
        Assert.Equal(1, StyleRules.BridgeDeck(parts));
        Assert.Equal(0, StyleRules.BridgeSection(new[] { "deck", "rail" }));
        Assert.Equal(-1, StyleRules.BridgeSection(new string[0]));
        Assert.Equal(-1, StyleRules.BridgeDeck(new[] { "deck" }));
        Assert.Equal(-1, StyleRules.BridgeSection(null!));
    }

    [Fact]
    public void Le_jeu_habille_forge_pont_eau_et_palissade()
    {
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Lot93UnityPackageTests.Root }.Concat(parts).ToArray()));
        string view = Read("unity", "Assets", "EdS", "Game", "SpaceView.cs");
        string markers = Read("unity", "Assets", "EdS", "Game", "WorldMarkers.cs");
        string game = Read("unity", "Assets", "EdS", "Game", "EdsGame.cs");
        string world = Read("unity", "Assets", "EdS", "Game", "StyleWorld.cs");
        string editor = Read("unity", "Assets", "EdS", "Editor", "EdsStyle.cs");
        string shader = Read("unity", "Assets", "EdS", "Game", "Eau.shader");
        // Vraie eau : shader d'URP, lit creusé, surface sous le sol, rivière sous le pont.
        Assert.Contains("Shader \"EdS/Eau\"", shader, StringComparison.Ordinal);
        Assert.Contains("\"RenderPipeline\" = \"UniversalPipeline\"", shader, StringComparison.Ordinal);
        Assert.Contains("\"LightMode\" = \"UniversalForward\"", shader, StringComparison.Ordinal);
        Assert.Contains("catalog.Water = WaterMaterial(notes);", editor, StringComparison.Ordinal);
        Assert.Contains("double dig = banks.Count > 0 ? StyleRules.ChannelDepth(StyleRules.UnionDistance(x, z, rivers, banks)) : 0;", world, StringComparison.Ordinal);
        Assert.Contains("data.SetHeights(0, 0, heights);", world, StringComparison.Ordinal);
        Assert.Contains("layer.diffuseRemapMax = new Vector4(tint.r, tint.g, tint.b, 1f);", world, StringComparison.Ordinal);
        Assert.Contains("WaterSurface(id, points);", view, StringComparison.Ordinal);
        Assert.Contains("WaterSurface(\"Rivière (sous le pont)\"", view, StringComparison.Ordinal);
        // Pont, palissade, haie, moulin.
        Assert.Contains("StyleLook.Bridge(_root.transform,", view, StringComparison.Ordinal);
        Assert.Contains("Palisade(id, points);", view, StringComparison.Ordinal);
        // Revue Codex de la PR #58 (3e tour) : la palissade calcule ses tangentes (relief de l'écorce).
        Assert.Contains("mesh.RecalculateTangents();   // revue Codex de la PR #58 (3e tour)", view, StringComparison.Ordinal);
        Assert.Contains("if (fence == StyleFence.Hedge && Hedge(id, points)) return;", view, StringComparison.Ordinal);
        Assert.Contains("StyleLook.Building(_root.transform, StyleProp.Mill, id, Center(points), 0f, side);", view, StringComparison.Ordinal);
        Assert.Contains("_occluders.Add(new Occluder { Transform = go.transform, Renderer = renderer, Lower = true, Min = new Vector3(x0, 0f, z0), Max = new Vector3(x1, top, z1) });", view, StringComparison.Ordinal);
        // Forge, herbe de l'Aube, filons ; les PNJ se tournent vers le joueur.
        Assert.Contains("StyleProp.FirePit", markers, StringComparison.Ordinal);
        Assert.Contains("StyleLook.Prop(holder, StyleProp.Herb, m.Data.Id, holder.position,", markers, StringComparison.Ordinal);
        Assert.Contains("StyleLook.Decor(holder, StyleSlot.Rock, m.Data.Id, holder.position, 0f, 1.1f);", markers, StringComparison.Ordinal);
        Assert.Contains("_markers.Face(_arena.Player.position, Time.deltaTime);", game, StringComparison.Ordinal);
        // Une deuxième préparation ne remplace plus les matières des personnages de Mixamo par une seule.
        Assert.Contains("p != hero && !IsMixamo(p) && !trees.ContainsKey(p)))", editor, StringComparison.Ordinal);   // les arbres de Sketchfab ont leur préfabriqué (essai 4)
    }

    [Fact]
    public void Moulin_de_Sketchfab()
    {
        // « Windmill anim+LOD.fbx » de Tim0 : le moulin détaillé (tour et ailes) et sa version simplifiée, posée à 21 m.
        Assert.Equal(new[] { "Assets/Style/Sketchfab/Windmill/Windmill anim+LOD.fbx" },
            StyleRules.PropCandidates(new[] { "Assets/Style/Sketchfab/Windmill/Windmill anim+LOD.fbx", "Assets/Style/Nature/FBX/Rock_1.fbx" }, StyleProp.Mill));
        var boxes = new List<(double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ)>
        {
            (-5.2, 0, -6.2, 5.2, 16.7, 6.2),       // tour détaillée
            (-26.7, 0, -15.5, -16.3, 16.7, -3.1),  // tour simplifiée
            (-9.5, 9, 6, 9.6, 28, 6.5),            // ailes détaillées (touchent la tour)
            (-31, 9, -3.4, -12, 28, -2.9),         // ailes simplifiées (touchent leur tour)
        };
        Assert.Equal(new[] { 0, 2 }, StyleRules.Cluster(boxes, new[] { 596, 390, 684, 302 }));
        Assert.Equal(new[] { 1, 3 }, StyleRules.Cluster(boxes, new[] { 1, 900, 1, 1 }));
        Assert.Equal(new[] { 0, 1, 2, 3 }, StyleRules.Cluster(boxes, new[] { 1, 1, 1, 1 }, 30));   // tout se touche à 30 m près
        Assert.Equal(new[] { 0 }, StyleRules.Cluster(boxes.Take(1).ToList(), null!));
        Assert.Empty(StyleRules.Cluster(null!, null!));
        // Textures du moulin : « opass » est sa transparence (trous des toiles des ailes), pas une couleur.
        Assert.True(StyleRules.IsAlphaMap("textures/Windmill_opass.png"));
        Assert.False(StyleRules.IsColorMap("textures/Windmill_opass.png"));
        Assert.True(StyleRules.IsColorMap("textures/Windmill_albedo.png"));
        Assert.False(StyleRules.IsColorMap("textures/Windmill_AO.png"));
        string look = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "StyleLook.cs"));
        Assert.Contains("IReadOnlyList<int> keep = StyleRules.Cluster(boxes, detail);", look, StringComparison.Ordinal);
        // Revue Codex de la PR #58 (2e tour) : un modèle à plusieurs matières garde sa transparence (toiles du moulin) sur celles qui
        // prennent sa texture de couleur : couleur et alpha réunis, découpe, vu des deux côtés.
        string editor = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Editor", "EdsStyle.cs"));
        int many = editor.IndexOf("if (embeddedMaterials.Count > 1)", StringComparison.Ordinal);
        int single = editor.IndexOf("string materialPath = Generated + \"/\" + name + \".mat\";", StringComparison.Ordinal);
        Assert.True(many > 0 && single > many);
        string parts = editor.Substring(many, single - many);
        // Essai réaliste 4 : transparence de même racine d'abord (feuilles), réglages de découpe dans Configure.
        Assert.Contains("string ownAlpha = StyleRules.AlphaFor(own, maps) ?? (own == color ? alpha : null);", parts, StringComparison.Ordinal);
        Assert.Contains("if (cutout == null) cutout = WithAlpha(color, alpha, Generated + \"/\" + name + \"_couleur_alpha.png\", notes);", parts, StringComparison.Ordinal);
        Assert.Contains("Configure(part, lit, leaves, wind, partMap, Texture2DAt(ownNormal), ownAlpha != null);", parts, StringComparison.Ordinal);
        Assert.Contains("material.EnableKeyword(\"_ALPHATEST_ON\");", editor, StringComparison.Ordinal);
        Assert.Contains("material.SetFloat(\"_Cull\", 0f);", editor, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_seul_lit_sous_les_bras_de_la_riviere_et_le_pont()
    {
        // Revue Codex de la PR #58 : les deux bras de la rivière des Vallons et le passage sous le pont sont trois rectangles voisins ;
        // creusés chacun pour soi, leurs côtés communs restaient des berges (deux bandes de terre en travers de la rivière).
        var south = new List<(double X, double Z)> { (4, -18), (7, -18), (7, -2), (4, -2) };
        var gap = new List<(double X, double Z)> { (4, -2), (7, -2), (7, 1.5), (4, 1.5) };
        var north = new List<(double X, double Z)> { (4, 34), (4, 1.5), (7, 1.5), (7, 34) };   // autre sens des sommets
        var rivers = new List<IReadOnlyList<(double X, double Z)>> { south, gap, north };
        var banks = StyleRules.UnionBoundary(rivers);
        // Le bord de la réunion est celui d'un seul rectangle de 3 m sur 52 m.
        double length = banks.Sum(s => Math.Sqrt((s.X1 - s.X0) * (s.X1 - s.X0) + (s.Z1 - s.Z0) * (s.Z1 - s.Z0)));
        Assert.Equal(2 * (3 + 52), length, 4);
        foreach (double z in new[] { -2.0, -1.75, 0.0, 1.5, 1.25, -10.0, 20.0 })
        {
            Assert.Equal(1.5, StyleRules.UnionDistance(5.5, z, rivers, banks), 6);
            Assert.Equal(StyleRules.RiverBed, StyleRules.ChannelDepth(StyleRules.UnionDistance(5.5, z, rivers, banks)), 6);   // lit continu
        }
        Assert.Equal(0.5, StyleRules.UnionDistance(4.5, -2, rivers, banks), 6);
        Assert.Equal(-2, StyleRules.UnionDistance(2, -2, rivers, banks), 6);          // dehors : négatif
        Assert.Equal(-1, StyleRules.UnionDistance(5.5, 35, rivers, banks), 6);        // au-delà du bout nord
        // Un seul polygone : la même chose que sa distance au bord.
        var alone = new List<IReadOnlyList<(double X, double Z)>> { south };
        var aloneBanks = StyleRules.UnionBoundary(alone);
        Assert.Equal(4, aloneBanks.Count);
        Assert.Equal(StyleRules.InsideDistance(4.7, -9, south), StyleRules.UnionDistance(4.7, -9, alone, aloneBanks), 9);
        // Polygones qui se chevauchent : le bord intérieur disparaît aussi.
        var a = new List<(double X, double Z)> { (0, 0), (4, 0), (4, 2), (0, 2) };
        var b = new List<(double X, double Z)> { (3, 0), (7, 0), (7, 2), (3, 2) };
        var overlap = new List<IReadOnlyList<(double X, double Z)>> { a, b };
        Assert.Equal(1, StyleRules.UnionDistance(3.5, 1, overlap, StyleRules.UnionBoundary(overlap)), 6);
        Assert.Empty(StyleRules.UnionBoundary(null!));
        Assert.True(double.IsNegativeInfinity(StyleRules.UnionDistance(0, 0, rivers, new List<(double, double, double, double)>())));
    }

    [Fact]
    public void Plus_haute_resolution_et_textures_par_appareil()
    {
        // Essai réaliste 4 (choix de Damien du 27 octobre 2026) : les fichiers 2k et le ciel 8k remplacent ceux de l'essai 3, restés sur
        // le disque ; ce qui n'a pas de résolution dans son nom reste.
        var paths = new[]
        {
            "Assets/Style/PolyHaven/rock_07/rock_07_1k.fbx", "Assets/Style/PolyHaven2k/rock_07/rock_07_2k.fbx",
            "Assets/Style/PolyHaven/Ground/leafy_grass_diff_1k.jpg", "Assets/Style/PolyHaven2k/Ground/leafy_grass_diff_2k.jpg",
            "Assets/Style/PolyHaven/Sky/kloofendal_48d_partly_cloudy_puresky_1k.hdr", "Assets/Style/PolyHaven2k/Sky/kloofendal_48d_partly_cloudy_puresky_8k.hdr",
            "Assets/Style/PolyHaven/barrel_03/barrel_03_1k.fbx", "Assets/Style/Sketchfab/Windmill/textures/Windmill_albedo.png",
            "Assets/Style/Mixamo/Paladin WProp J Nordstrom.fbx",
        };
        Assert.Equal(new[]
        {
            "Assets/Style/PolyHaven2k/rock_07/rock_07_2k.fbx", "Assets/Style/PolyHaven2k/Ground/leafy_grass_diff_2k.jpg",
            "Assets/Style/PolyHaven2k/Sky/kloofendal_48d_partly_cloudy_puresky_8k.hdr", "Assets/Style/PolyHaven/barrel_03/barrel_03_1k.fbx",
            "Assets/Style/Sketchfab/Windmill/textures/Windmill_albedo.png", "Assets/Style/Mixamo/Paladin WProp J Nordstrom.fbx",
        }, StyleRules.Highest(paths));
        Assert.Equal("Assets/Style/PolyHaven2k/Sky/kloofendal_48d_partly_cloudy_puresky_8k.hdr", StyleRules.Sky(StyleRules.Highest(paths)));
        Assert.Empty(StyleRules.Highest(null!));
        // Les modèles 2k restent des modèles de Poly Haven (une variante par lot).
        Assert.True(StyleRules.IsPolyHavenName("rock_07_2k"));
        string editor = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Editor", "EdsStyle.cs"));
        Assert.Contains("int desktop = sky ? 8192 : 2048, phone = sky ? 2048 : 1024;", editor, StringComparison.Ordinal);
        Assert.Contains("TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings(\"Android\");", editor, StringComparison.Ordinal);
        Assert.Contains("List<string> models = StyleRules.Highest(", editor, StringComparison.Ordinal);
    }

    [Fact]
    public void Arbres_realistes_ecorce_et_feuilles()
    {
        // Les arbres de Sketchfab d'evolveduk (chêne, pin, épicéa, bouleau) : réalistes, ils remplacent ceux du MegaKit.
        var models = new[]
        {
            "Assets/Style/Sketchfab/Arbres/Oak/oak.fbx", "Assets/Style/Sketchfab/Arbres/Pine/Pine.fbx", "Assets/Style/Sketchfab/Arbres/Spruce/Spruce.fbx",
            "Assets/Style/Sketchfab/Arbres/Birch/birch.fbx", "Assets/Style/Nature/FBX/CommonTree_1.fbx", "Assets/Style/PolyHaven/stone_fire_pit/stone_fire_pit_1k.fbx",
        };
        // Ordre des mots cherchés : pine, birch, oak, spruce ; le chêne commun du MegaKit n'y est plus.
        Assert.Equal(new[] { models[1], models[3], models[0], models[2] }, StyleRules.ForCatalog(models, StyleSlot.Tree, 8));
        Assert.False(StyleRules.Matches(models[5], StyleSlot.Tree));                 // « fir » dans « fire » : pas un sapin
        // Matières nommées librement : la texture qui parle de la même chose.
        var pine = new[] { "Pine/bark09.png", "Pine/bark09_normal.png", "Pine/pine branch.png", "Pine/pine branch_Normal.png", "Pine/pine branch_alpha.png",
            "Pine/pine branch_Roughness.png", "Pine/pine branch_Transmission.png" };
        Assert.Equal("Pine/pine branch.png", StyleRules.MaterialTexture("leaves", pine, false));
        Assert.Equal("Pine/pine branch_Normal.png", StyleRules.MaterialTexture("Leaves_MAT", pine, true));
        Assert.Equal("Pine/bark09.png", StyleRules.MaterialTexture("Bark", pine, false));
        Assert.Equal("Pine/bark09_normal.png", StyleRules.MaterialTexture("trunk", pine, true));
        Assert.Null(StyleRules.MaterialTexture("metal", pine, false));
        // Transparence de même racine.
        Assert.Equal("Pine/pine branch_alpha.png", StyleRules.AlphaFor("Pine/pine branch.png", pine));
        Assert.Null(StyleRules.AlphaFor("Pine/bark09.png", pine));
        Assert.Equal("t/oak_leaf_alpha.png", StyleRules.AlphaFor("t/oak leaf.png", new[] { "t/oak leaf.png", "t/oak_leaf_alpha.png", "t/mossybark02.png" }));
        Assert.Equal("t/fern_02_alpha_2k.jpg", StyleRules.AlphaFor("t/fern_02_diff_2k.jpg", new[] { "t/fern_02_diff_2k.jpg", "t/fern_02_alpha_2k.jpg" }));
        Assert.Equal("fern02", StyleRules.TextureStem("t/fern_02_diff_2k.jpg"));
        Assert.Equal("branch06", StyleRules.TextureStem("Spruce/branch06_alpha.png"));
        Assert.Null(StyleRules.AlphaFor("x.png", null!));
    }

    [Fact]
    public void Pieces_des_arbres_de_Sketchfab()
    {
        // Les fichiers de Tree It n'ont pas de matière : une pièce par texture, nommée d'après elle (« oak leaf_1 ») ou « Tree_0 ».
        // Les noms et les fichiers ci-dessous sont ceux des quatre arbres d'evolveduk téléchargés le 27 octobre 2026.
        string[] Files(string folder, params string[] names) => names.Select(n => "Assets/Style/Sketchfab/Arbres/" + folder + "/" + n).ToArray();
        string[] oak = Files("Oak", "mossybark02.png", "mossybark02_normal.png", "mossybark02_roughness.png", "oak leaf.png", "oak leaf_Normal.png",
            "oak leaf_Roughness.png", "oak leaf_Translucency.png", "oak.fbx", "oak_leaf_alpha.png");
        string[] pine = Files("Pine", "Pine.fbx", "bark09.png", "bark09_normal.png", "bark09_roughness.png", "pine branch.png", "pine branch_Normal.png",
            "pine branch_Roughness.png", "pine branch_Transmission.png", "pine branch_alpha.png");
        string[] spruce = Files("Spruce", "Spruce.fbx", "bark08.png", "bark08_normal.png", "bark08_roughness.png", "branch06.png", "branch06_alpha.png",
            "branch06_normal.png", "branch06_roughness.png", "branch06_transmission.png", "spruce branch.png", "spruce branch_Normal.png",
            "spruce branch_Roughness.png", "spruce branch_Transmission.png", "spruce_branch_alpha.png");
        string[] birch = Files("Birch", "bark11.png", "bark11_normal.png", "bark11_roughness.png", "birch leaf.png", "birch leaf_Normal.png",
            "birch leaf_Roughness.png", "birch.fbx", "birch_leaf_Translucency.png", "birch_leaf_alpha.png");

        void Part(string part, string[] textures, string color, string normal, string? alpha)
        {
            (string Color, string? Normal)? found = StyleRules.PartTextures(part, textures);
            Assert.NotNull(found);
            Assert.Equal(color, Path.GetFileName(found!.Value.Color));
            Assert.Equal(normal, Path.GetFileName(found.Value.Normal));
            Assert.Equal(alpha, Path.GetFileName(StyleRules.AlphaFor(found.Value.Color, textures)));
            Assert.Equal(alpha != null, StyleRules.IsFoliage(found.Value.Color));   // seul le feuillage est découpé
        }

        // Pièce nommée d'après sa texture.
        Part("mossybark02_0", oak, "mossybark02.png", "mossybark02_normal.png", null);
        Part("oak leaf_1", oak, "oak leaf.png", "oak leaf_Normal.png", "oak_leaf_alpha.png");
        Part("bark11_0", birch, "bark11.png", "bark11_normal.png", null);
        Part("birch leaf_1", birch, "birch leaf.png", "birch leaf_Normal.png", "birch_leaf_alpha.png");
        Part("bark08_0", spruce, "bark08.png", "bark08_normal.png", null);
        Part("spruce branch_1", spruce, "spruce branch.png", "spruce branch_Normal.png", "spruce_branch_alpha.png");
        Part("branch06_2", spruce, "branch06.png", "branch06_normal.png", "branch06_alpha.png");
        // Pin : pièces « Tree_0 » et « Tree_1 » : l'écorce pour la pièce 0, le feuillage pour les autres.
        Part("Tree_0", pine, "bark09.png", "bark09_normal.png", null);
        Part("Tree_1", pine, "pine branch.png", "pine branch_Normal.png", "pine branch_alpha.png");
        // Sans numéro : comme la pièce 0 ; rien à proposer : null.
        Part("Tree", pine, "bark09.png", "bark09_normal.png", null);
        Assert.Null(StyleRules.PartTextures("Tree_0", new[] { "x/rock.fbx", "x/pine branch_alpha.png" }));
        Assert.Null(StyleRules.PartTextures("Tree_1", null!));
        Assert.Null(StyleRules.PartTextures(null!, Array.Empty<string>()));
        // Les cartes de rugosité, de transparence et de translucidité ne sont jamais prises pour la couleur.
        Assert.All(new[] { oak, pine, spruce, birch }.SelectMany(t => t).Where(t => t.EndsWith(".png")), t =>
            Assert.Equal(!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(t), "(?i)normal|rough|alpha|transl|transm"), StyleRules.IsColorMap(t)));
    }

    [Fact]
    public void Herbe_en_brins()
    {
        // Essai réaliste 4 : touffes d'herbe en brins, environ « densité » par mètre carré, mêmes places à chaque lancement.
        IReadOnlyList<StyleRules.GrassSpot> spots = StyleRules.GrassSpots(-24, -18, 60, 40, 4);
        Assert.InRange(spots.Count, 84 * 58 * 4 * 0.95, 84 * 58 * 4 * 1.08);
        Assert.All(spots, g =>
        {
            Assert.InRange(g.X, -24, 60);
            Assert.InRange(g.Z, -18, 40);
            Assert.InRange(g.Yaw, 0, 360);
            Assert.InRange(g.Scale, 0.75, 1.3);
        });
        Assert.Equal(spots, StyleRules.GrassSpots(-24, -18, 60, 40, 4));
        Assert.Empty(StyleRules.GrassSpots(0, 0, 10, 10, 0));
        Assert.Empty(StyleRules.GrassSpots(0, 0, 0, 10, 4));
        // Plafond : une grande emprise reçoit une herbe plus clairsemée, mais partout, jusqu'à ses bords.
        IReadOnlyList<StyleRules.GrassSpot> capped = StyleRules.GrassSpots(0, 0, 100, 100, 50, 100);
        Assert.InRange(capped.Count, 70, 100);
        Assert.True(capped.Max(g => g.Z) > 85 && capped.Min(g => g.Z) < 15 && capped.Max(g => g.X) > 85 && capped.Min(g => g.X) < 15);
        IReadOnlyList<StyleRules.GrassSpot> large = StyleRules.GrassSpots(-300, -300, 300, 300, 4);
        Assert.InRange(large.Count, 150000, 200000);
        Assert.True(large.Max(g => g.Z) > 290);
        Assert.Empty(StyleRules.GrassSpots(0, 0, 10, 10, 4, 0));
    }

    [Fact]
    public void Le_jeu_branche_l_essai_realiste_4()
    {
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Lot93UnityPackageTests.Root }.Concat(parts).ToArray()));
        string leaves = Read("unity", "Assets", "EdS", "Game", "Feuillage.shader");
        string grassShader = Read("unity", "Assets", "EdS", "Game", "Herbe.shader");
        string grass = Read("unity", "Assets", "EdS", "Game", "StyleGrass.cs");
        string view = Read("unity", "Assets", "EdS", "Game", "SpaceView.cs");
        string world = Read("unity", "Assets", "EdS", "Game", "StyleWorld.cs");
        string water = Read("unity", "Assets", "EdS", "Game", "Eau.shader");
        string editor = Read("unity", "Assets", "EdS", "Editor", "EdsStyle.cs");
        // Feuillages au vent, avec leurs ombres (le vent aussi dans l'ombre projetée) et l'occlusion ambiante.
        Assert.Contains("Shader \"EdS/Feuillage\"", leaves, StringComparison.Ordinal);
        foreach (string pass in new[] { "\"LightMode\" = \"UniversalForward\"", "\"LightMode\" = \"ShadowCaster\"", "\"LightMode\" = \"DepthOnly\"", "\"LightMode\" = \"DepthNormals\"" })
            Assert.Contains(pass, leaves, StringComparison.Ordinal);
        Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(leaves, @"Windy\(TransformObjectToWorld").Count);
        Assert.Contains("_SCREEN_SPACE_OCCLUSION", leaves, StringComparison.Ordinal);
        Assert.Contains("RealisticMaterial(model, wind.TryGetValue(model, out float w) ? w : 0f, notes);", editor, StringComparison.Ordinal);
        // Herbe en brins dessinée par paquets, près de la caméra seulement.
        Assert.Contains("Shader \"EdS/Herbe\"", grassShader, StringComparison.Ordinal);
        Assert.Contains("#pragma multi_compile_instancing", grassShader, StringComparison.Ordinal);
        // Revue Codex de la PR #59 : paquets de 511 touffes au plus, échelle uniforme déclarée dans les trois passes.
        Assert.Contains("private const int Batch = 511;", grass, StringComparison.Ordinal);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(grassShader, "#pragma instancing_options assumeuniformscaling").Count);
        Assert.Contains("Graphics.RenderMeshInstanced(parameters, _mesh, 0, cell.Items);", grass, StringComparison.Ordinal);
        Assert.Contains("_grass?.Draw(camera);", view, StringComparison.Ordinal);
        Assert.Contains("if (terrain) Grass(space);", view, StringComparison.Ordinal);
        Assert.Contains("catalog.Grass = ShaderMaterial(\"EdS/Herbe\", \"Herbe\", notes);", editor, StringComparison.Ordinal);
        // Reflets des objets dans l'eau ; rendu du téléphone choisi au lancement ; ombres fines et occlusion ambiante sur ordinateur.
        Assert.Contains("StyleWorld.Reflections(_root.transform,", view, StringComparison.Ordinal);
        Assert.Contains("probe.cullingMask = ~(1 << WaterLayer);", world, StringComparison.Ordinal);
        Assert.Contains("sky = lerp(sky, probe, _ProbeWeight);", water, StringComparison.Ordinal);
        Assert.Contains("if (Application.isMobilePlatform && catalog.PhonePipeline != null) QualitySettings.renderPipeline = catalog.PhonePipeline;", world, StringComparison.Ordinal);
        Assert.Contains("Tune(pipeline, 90f, 4, 4096, true, 4);", editor, StringComparison.Ordinal);
        Assert.Contains("Tune(phone, 35f, 2, 1024, false, 2);", editor, StringComparison.Ordinal);
        Assert.Contains("\"UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion\"", editor, StringComparison.Ordinal);
        // Lumières des feux par pixel : LightRenderingMode.PerPixel vaut 1 (PerVertex vaut 2).
        Assert.Contains("Set(\"m_AdditionalLightsRenderingMode\", p => p.intValue = 1);", editor, StringComparison.Ordinal);
        // Les feuillages reçoivent aussi le feu de la forge en Forward+ (Unity 6.0, puis 6.1 et après).
        Assert.Contains("#pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP", leaves, StringComparison.Ordinal);
        // L'herbe écrit sa profondeur (sans quoi elle disparaîtrait quand URP prépare la profondeur) avec le même vent que son image.
        foreach (string pass in new[] { "\"LightMode\" = \"UniversalForward\"", "\"LightMode\" = \"DepthOnly\"", "\"LightMode\" = \"DepthNormals\"" })
            Assert.Contains(pass, grassShader, StringComparison.Ordinal);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(grassShader, @"Bent\(input\.positionOS\.xyz, input\.uv, pivot\)").Count);
        // Feuilles découpées qui ne se dégarnissent pas au loin (réductions qui gardent la part de feuillage au seuil du shader).
        Assert.Contains("importer.mipMapsPreserveCoverage = true;", editor, StringComparison.Ordinal);
        Assert.Contains("importer.alphaTestReferenceValue = 0.45f;", editor, StringComparison.Ordinal);
        // Revue Codex de la PR #59 : les arbres réunissent leur carte d'opacité à la couleur avant de régler le matériau.
        int tree = editor.IndexOf("private static string TreePrefab(", StringComparison.Ordinal);
        string treePrefab = editor.Substring(tree, editor.IndexOf("PrefabUtility.SaveAsPrefabAsset", tree, StringComparison.Ordinal) - tree);
        Assert.Contains("string alpha = StyleRules.AlphaFor(color, maps);", treePrefab, StringComparison.Ordinal);
        Assert.Contains("? WithAlpha(color, alpha, Generated + \"/\" + name + \" \" + Path.GetFileNameWithoutExtension(color) + \"_couleur_alpha.png\", notes)", treePrefab, StringComparison.Ordinal);
        Assert.Contains("Configure(material, lit, leaves, 0.25f, baseMap, Texture2DAt(normal), foliage || alpha != null);", treePrefab, StringComparison.Ordinal);
    }
}
