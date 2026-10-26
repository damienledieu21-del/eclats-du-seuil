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
}
