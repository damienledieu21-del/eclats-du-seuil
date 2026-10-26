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
        Assert.Null(StyleRules.ForCombatant("archer", false));
        Assert.Null(StyleRules.ForCombatant("gardien_du_seuil", false));
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
}
