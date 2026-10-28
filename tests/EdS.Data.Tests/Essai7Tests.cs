using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Domain.Combat;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Essai réaliste 7 (demandes de Damien du 28 octobre 2026 : « les icônes sont juste trop gros », sons d'interface, effets en particules,
/// animations Mixamo variées, musique) : règles de l'essai de style sans Unity. Choix des animations (gestes des compétences, attaques
/// des adversaires, coups reçus), bruitages variés, musiques et ambiances, tremblement de la caméra.
/// </summary>
[Trait("Category", "Runtime")]
public class Essai7StyleTests
{
    /// <summary>Animations du paladin de Mixamo telles que la préparation les nomme (nom du fichier), dans l'ordre du catalogue.</summary>
    private static readonly string[] Paladin =
    {
        "Sword And Shield Block Idle", "Sword And Shield Casting", "Sword And Shield Death", "Sword And Shield Guard Impact",
        "Sword And Shield High Attack", "Sword And Shield Hilt Attack", "Sword And Shield Idle", "Sword And Shield Impact",
        "Sword And Shield Kick Attack", "Sword And Shield Power Slash", "Sword And Shield Run", "Sword And Shield Slash", "Sword And Shield Walk",
    };

    private static readonly string[] Gardien =
    {
        "Gardien Attack", "Gardien Attack 2", "Gardien Death", "Gardien Idle", "Gardien Impact", "Gardien Jump Attack", "Gardien Roar", "Gardien Walk",
    };

    /// <summary>Animations d'un animal de Quaternius (loup, cochon) : l'attente et la réaction aux coups ont des noms voisins.</summary>
    private static readonly string[] Animal =
    {
        "AnimalArmature|Attack_Headbutt", "AnimalArmature|Death", "AnimalArmature|Gallop", "AnimalArmature|Idle", "AnimalArmature|Idle_HitReact_Left",
        "AnimalArmature|Walk",
    };

    [Theory]
    [InlineData(StyleMotion.Idle, "Sword And Shield Idle")]
    [InlineData(StyleMotion.Walk, "Sword And Shield Walk")]
    [InlineData(StyleMotion.Run, "Sword And Shield Run")]
    [InlineData(StyleMotion.Death, "Sword And Shield Death")]
    [InlineData(StyleMotion.Block, "Sword And Shield Block Idle")]
    [InlineData(StyleMotion.Cast, "Sword And Shield Casting")]
    [InlineData(StyleMotion.Hurt, "Sword And Shield Impact")]
    [InlineData(StyleMotion.GuardHit, "Sword And Shield Guard Impact")]
    public void Chaque_mouvement_du_paladin_a_son_animation(StyleMotion motion, string clip) => Assert.Equal(clip, StyleRules.Clip(Paladin, motion));

    [Fact]
    public void Les_animations_reservees_ne_servent_qu_a_leur_mouvement()
    {
        // L'attaque ne prend ni un sort, ni un coup reçu ; l'attente ne prend ni la garde, ni la réaction aux coups.
        Assert.DoesNotContain(StyleRules.Clip(Paladin, StyleMotion.Attack), new[] { "Sword And Shield Casting", "Sword And Shield Impact", "Sword And Shield Guard Impact" });
        Assert.Equal("AnimalArmature|Idle", StyleRules.Clip(Animal, StyleMotion.Idle));
        Assert.Equal("AnimalArmature|Attack_Headbutt", StyleRules.Clip(Animal, StyleMotion.Attack));
        Assert.Equal("AnimalArmature|Idle_HitReact_Left", StyleRules.Clip(Animal, StyleMotion.Hurt));
        // Sans animation de coup reçu ni de sort : rien (le corps garde son allure), puis l'attaque pour un sort.
        string[] plain = { "Idle", "Walk", "Attack" };
        Assert.Null(StyleRules.Clip(plain, StyleMotion.Hurt));
        Assert.Null(StyleRules.Clip(plain, StyleMotion.GuardHit));
        Assert.Equal("Attack", StyleRules.Clip(plain, StyleMotion.Cast));
        Assert.Equal("Idle", StyleRules.Clip(plain, StyleMotion.Block));
    }

    [Theory]
    [InlineData("Sword And Shield Block Idle", true)]
    [InlineData("Sword And Shield Idle", true)]
    [InlineData("Gardien Idle", true)]
    [InlineData("AnimalArmature|Idle_HitReact_Left", false)]
    [InlineData("Sword And Shield Impact", false)]
    [InlineData("Sword And Shield Casting", false)]
    [InlineData("Gardien Roar", false)]
    [InlineData("Sword And Shield High Attack", false)]
    public void Seules_l_attente_et_l_allure_tournent_en_boucle(string clip, bool loops) => Assert.Equal(loops, StyleRules.Loops(clip));

    [Theory]
    [InlineData("skill.bastion.frappe_du_rempart", false, "Sword And Shield High Attack")]
    [InlineData("skill.bastion.heurt_de_bouclier", false, "Sword And Shield Hilt Attack")]
    [InlineData("skill.bastion.contrecoup", false, "Sword And Shield Power Slash")]
    [InlineData("skill.bastion.onde_de_rupture", false, "Sword And Shield Slash")]
    [InlineData("skill.bastion.provocation", false, "Sword And Shield Casting")]
    [InlineData("skill.bastion.barriere_d_acier", false, "Sword And Shield Casting")]
    [InlineData("skill.bastion.bastion_absolu", true, "Sword And Shield Casting")]
    public void Chaque_competence_du_Bastion_a_son_geste(string skill, bool ultimate, string clip)
    {
        (StyleMotion motion, string? prefer, int variant) = StyleRules.SkillAnimation(skill, ultimate);
        Assert.Equal(clip, StyleRules.Clip(Paladin, motion, prefer, variant));
    }

    [Fact]
    public void Une_autre_competence_prend_une_attaque_variee_et_stable()
    {
        var chosen = new HashSet<string>();
        foreach (string skill in new[] { "skill.x.a", "skill.x.b", "skill.x.c", "skill.x.d", "skill.x.e", "skill.x.f", "skill.x.g", "skill.x.h" })
        {
            (StyleMotion motion, string? prefer, int variant) = StyleRules.SkillAnimation(skill, false);
            Assert.Equal(StyleMotion.Attack, motion);
            Assert.Null(prefer);
            Assert.True(variant > 0);
            string clip = StyleRules.Clip(Paladin, motion, prefer, variant)!;
            Assert.Equal(clip, StyleRules.Clip(Paladin, motion, prefer, StyleRules.SkillAnimation(skill, false).Variant));   // stable
            Assert.Contains(clip, new[] { "Sword And Shield High Attack", "Sword And Shield Hilt Attack", "Sword And Shield Kick Attack",
                "Sword And Shield Power Slash", "Sword And Shield Slash" });
            chosen.Add(clip);
        }
        Assert.True(chosen.Count >= 2, "les compétences inconnues ne jouent pas toutes le même geste");
    }

    [Theory]
    [InlineData("onde_du_seuil", "Gardien Roar")]          // vraies attaques du Gardien (relecture de l'essai 7)
    [InlineData("ruee", "Gardien Jump Attack")]
    [InlineData("gardien.rugissement", "Gardien Roar")]
    [InlineData("gardien.bond", "Gardien Jump Attack")]
    [InlineData("gardien.ecrasement", "Gardien Jump Attack")]
    public void Le_Gardien_rugit_et_bondit(string attack, string clip)
    {
        (StyleMotion motion, string? prefer, int variant) = StyleRules.AttackAnimation(attack);
        Assert.Equal(clip, StyleRules.Clip(Gardien, motion, prefer, variant));
    }

    [Fact]
    public void Une_attaque_ordinaire_prend_un_geste_d_attaque_stable_jamais_le_saut()
    {
        var chosen = new HashSet<string>();
        foreach (string attack in new[] { "griffe", "morsure", "fleche", "eclat", "charge", "gardien.coup", "gardien.balayage" })
        {
            (StyleMotion motion, string? prefer, int variant) = StyleRules.AttackAnimation(attack);
            Assert.Equal(StyleMotion.Attack, motion);
            string clip = StyleRules.Clip(Gardien, motion, prefer, variant)!;
            Assert.Contains(clip, new[] { "Gardien Attack", "Gardien Attack 2" });
            (StyleMotion m2, string? p2, int v2) = StyleRules.AttackAnimation(attack);
            Assert.Equal(clip, StyleRules.Clip(Gardien, m2, p2, v2));   // stable
            chosen.Add(clip);
            Assert.Equal("AnimalArmature|Attack_Headbutt", StyleRules.Clip(Animal, motion, prefer, variant));
        }
        Assert.Equal(2, chosen.Count);
        // Le saut seul : on le garde plutôt que rien.
        Assert.Equal("Gardien Jump Attack", StyleRules.Clip(new[] { "Gardien Idle", "Gardien Jump Attack" }, StyleMotion.Attack, StyleRules.NotJump, 3));
    }

    [Fact]
    public void Chaque_attaque_des_Vallons_a_un_geste()
    {
        // Relecture de l'essai 7 : les identifiants des scénarios livrés, pas des noms inventés. Les ondes et la pluie sont des cris
        // (l'archer et le rôdeur, sans cri, prennent leur attaque), la ruée un saut.
        var attacks = new[] { "zones/vallons", "zones/vallons_chef" }
            .SelectMany(f => JsonNode.Parse(File.ReadAllText(ScenarioPath(f)))!["data"]!["combatants"]!.AsArray())
            .SelectMany(c => c!["attacks"]?.AsArray() ?? new JsonArray()).Select(a => (string)a!["id"]!).Distinct().OrderBy(a => a, StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { "charge", "eclat", "fleche", "griffe", "morsure", "onde", "onde_du_seuil", "pluie", "ruee" }, attacks);
        var expected = new Dictionary<string, (StyleMotion, string?)>
        {
            ["charge"] = (StyleMotion.Attack, StyleRules.NotJump), ["eclat"] = (StyleMotion.Attack, StyleRules.NotJump),
            ["fleche"] = (StyleMotion.Attack, StyleRules.NotJump), ["griffe"] = (StyleMotion.Attack, StyleRules.NotJump),
            ["morsure"] = (StyleMotion.Attack, StyleRules.NotJump), ["onde"] = (StyleMotion.Cast, null), ["onde_du_seuil"] = (StyleMotion.Cast, null),
            ["pluie"] = (StyleMotion.Cast, null), ["ruee"] = (StyleMotion.Attack, "jump"),
        };
        foreach (string attack in attacks)
        {
            (StyleMotion motion, string? prefer, _) = StyleRules.AttackAnimation(attack);
            Assert.Equal(expected[attack], (motion, prefer));
        }
        // Sans geste de cri (rôdeur, archer), l'attaque.
        string[] rodeur = { "Rodeur Attack", "Rodeur Attack 2", "Rodeur Idle", "Rodeur Impact", "Rodeur Walk" };
        Assert.StartsWith("Rodeur Attack", StyleRules.Clip(rodeur, StyleMotion.Cast, null, 0));
    }

    [Theory]
    [InlineData("skill.bastion.defi")]       // contient « onde » sans être l'Onde de rupture
    [InlineData("skill.test.besoin")]        // contient « soin » sans être un soin
    [InlineData("skill.test.refrapper")]
    public void Une_competence_se_reconnait_mot_a_mot(string skill)
    {
        (StyleMotion motion, string? prefer, int variant) = StyleRules.SkillAnimation(skill, false);
        Assert.Equal(StyleMotion.Attack, motion);
        Assert.Null(prefer);
        Assert.True(variant > 0);
    }

    [Fact]
    public void Les_bruitages_ont_leurs_variantes_sans_les_musiques()
    {
        string[] files =
        {
            "Assets/Style/Sounds/Combat/swish_1.ogg", "Assets/Style/Sounds/Combat/swish_11.ogg", "Assets/Style/Sounds/Combat/swish_3.ogg",
            "Assets/Style/Sounds/Combat/spell.ogg", "Assets/Style/Sounds/Combat/spell_magic.ogg",
            "Assets/Style/Sounds/Creatures/wolf_growl_1.ogg", "Assets/Style/Sounds/Creatures/wolf_growl_2.ogg",
            "Assets/Style/Sounds/Creatures/boar_grunt_1.ogg", "Assets/Style/Sounds/Creatures/roar_1.ogg",
            "Assets/Style/Sounds/Loot/coin_1.ogg", "Assets/Style/Sounds/Impact/impactPunch_medium_000.ogg",
            "Assets/Style/Music/Combat Battle Theme A.mp3", "Assets/Style/Music/Village Enchanted Festival Loop.ogg",
            "Assets/Style/Music/Exploration Woodland Fantasy.mp3", "Assets/Style/Music/Boss Heroic Demise.mp3",
            "Assets/Style/Ambience/Ambiance Riviere.ogg", "Assets/Style/Ambience/Ambiance Oiseaux.ogg", "Assets/Style/Ambience/Ambiance Vent.ogg",
        };
        Assert.Equal(new[] { "Assets/Style/Sounds/Combat/swish_1.ogg", "Assets/Style/Sounds/Combat/swish_11.ogg" }, StyleRules.PickSounds(files, StyleSound.Swing, 2));
        Assert.Equal(3, StyleRules.PickSounds(files, StyleSound.Swing, 6).Count);
        Assert.Equal(2, StyleRules.PickSounds(files, StyleSound.Cast, 6).Count);
        Assert.Equal(2, StyleRules.PickSounds(files, StyleSound.Growl, 6).Count);
        Assert.Single(StyleRules.PickSounds(files, StyleSound.Grunt, 6));
        Assert.Single(StyleRules.PickSounds(files, StyleSound.Roar, 6));
        Assert.Single(StyleRules.PickSounds(files, StyleSound.Coin, 6));
        Assert.Empty(StyleRules.PickSounds(files, StyleSound.Craft, 6));
        // Une musique ou une ambiance n'est jamais un bruitage (« Combat Battle Theme » n'est pas un coup).
        foreach (StyleSound sound in Enum.GetValues<StyleSound>())
        {
            Assert.DoesNotContain(StyleRules.PickSounds(files, sound, 20), p => p.Contains("/Music/") || p.Contains("/Ambience/"));
            Assert.False(StyleRules.PickSound(files, sound) is string one && (one.Contains("/Music/") || one.Contains("/Ambience/")));
        }
        Assert.Equal("Assets/Style/Sounds/Impact/impactPunch_medium_000.ogg", StyleRules.PickSound(files, StyleSound.Hit));
    }

    [Theory]
    [InlineData("Assets/Style/Music/Village Enchanted Festival Loop.ogg", StyleMusic.Village)]
    [InlineData("Assets/Style/Music/Exploration Woodland Fantasy.mp3", StyleMusic.Exploration)]
    [InlineData("Assets/Style/Music/Combat Battle Theme A.mp3", StyleMusic.Combat)]
    [InlineData("Assets/Style/Musique/Boss Heroic Demise.mp3", StyleMusic.Boss)]
    public void Les_musiques_sont_reconnues_dans_leur_dossier(string path, StyleMusic music)
    {
        Assert.Equal(music, StyleRules.MusicOf(path));
        Assert.Null(StyleRules.AmbienceOf(path));
        Assert.Null(StyleRules.MusicOf(path.Replace("/Music/", "/Sounds/").Replace("/Musique/", "/Sounds/")));
    }

    [Theory]
    [InlineData("Assets/Style/Ambience/Ambiance Oiseaux.ogg", StyleAmbience.Birds)]
    [InlineData("Assets/Style/Ambience/Ambiance Vent.ogg", StyleAmbience.Wind)]
    [InlineData("Assets/Style/Ambiance/park_ambience_river.ogg", StyleAmbience.River)]
    [InlineData("Assets/Style/Ambience/forest_river.ogg", StyleAmbience.River)]   // relecture de l'essai 7 : la rivière avant les oiseaux
    [InlineData("Assets/Style/Ambience/forest_wind.ogg", StyleAmbience.Wind)]
    public void Les_ambiances_sont_reconnues_dans_leur_dossier(string path, StyleAmbience ambience)
    {
        Assert.Equal(ambience, StyleRules.AmbienceOf(path));
        Assert.Null(StyleRules.MusicOf(path));
    }

    [Fact]
    public void Un_fichier_du_dossier_des_musiques_n_est_jamais_un_bruitage()
    {
        // Relecture de l'essai 7 : rangé avec les musiques mais au nom non reconnu, il n'est ni une musique jouée, ni un bruitage.
        const string odd = "Assets/Style/Music/swish_theme_long.ogg";
        Assert.Null(StyleRules.MusicOf(odd));
        Assert.True(StyleRules.InMusicFolder(odd));
        Assert.False(StyleRules.InAmbienceFolder(odd));
        Assert.True(StyleRules.InAmbienceFolder("Assets/Style/Ambiance/inconnu.ogg"));
        string[] files = { odd, "Assets/Style/Ambiance/swish_wind.ogg", "Assets/Style/Sounds/Combat/swish_1.ogg" };
        Assert.Equal("Assets/Style/Sounds/Combat/swish_1.ogg", StyleRules.PickSound(files, StyleSound.Swing));
        Assert.Equal(new[] { "Assets/Style/Sounds/Combat/swish_1.ogg" }, StyleRules.PickSounds(files, StyleSound.Swing, 6));
    }

    [Fact]
    public void La_musique_suit_le_Gardien_puis_le_combat_puis_le_village()
    {
        Assert.Equal(StyleMusic.Boss, StyleRules.MusicFor(true, true, true));
        Assert.Equal(StyleMusic.Combat, StyleRules.MusicFor(false, true, true));
        Assert.Equal(StyleMusic.Village, StyleRules.MusicFor(false, false, true));
        Assert.Equal(StyleMusic.Exploration, StyleRules.MusicFor(false, false, false));
    }

    [Fact]
    public void Les_ambiances_suivent_la_riviere_et_le_combat()
    {
        Assert.Equal(1, StyleRules.AmbienceVolume(StyleAmbience.River, 0, false, false));
        Assert.Equal(1, StyleRules.AmbienceVolume(StyleAmbience.River, 4, false, false));
        Assert.Equal(0, StyleRules.AmbienceVolume(StyleAmbience.River, 30, false, false));
        Assert.Equal(0, StyleRules.AmbienceVolume(StyleAmbience.River, 80, false, false));
        Assert.Equal(0, StyleRules.AmbienceVolume(StyleAmbience.River, double.NaN, false, false));
        Assert.InRange(StyleRules.AmbienceVolume(StyleAmbience.River, 17, false, false), 0.4, 0.6);
        Assert.True(StyleRules.AmbienceVolume(StyleAmbience.Birds, 99, false, true) < StyleRules.AmbienceVolume(StyleAmbience.Birds, 99, false, false));
        Assert.True(StyleRules.AmbienceVolume(StyleAmbience.Wind, 99, true, false) < StyleRules.AmbienceVolume(StyleAmbience.Wind, 99, false, false));
        foreach (StyleAmbience a in Enum.GetValues<StyleAmbience>())
            foreach (double d in new[] { -1, 0, 3, 10, 29, 31, 1e9 })
                foreach (bool v in new[] { false, true })
                    foreach (bool f in new[] { false, true })
                        Assert.InRange(StyleRules.AmbienceVolume(a, d, v, f), 0, 1);
    }

    [Fact]
    public void La_camera_tremble_selon_le_coup()
    {
        Assert.Equal(0.2, GameFeel.Shock(FeedbackKind.DamageTaken, true, 0, 370, false), 6);
        Assert.Equal(0.8, GameFeel.Shock(FeedbackKind.DamageTaken, true, 370, 370, false), 6);
        Assert.True(GameFeel.Shock(FeedbackKind.DamageTaken, true, 90, 370, false) > GameFeel.Shock(FeedbackKind.DamageTaken, true, 20, 370, false));
        Assert.Equal(0.2, GameFeel.Shock(FeedbackKind.DamageTaken, true, 50, 0, false), 6);   // vie maximale inconnue
        Assert.Equal(0.15, GameFeel.Shock(FeedbackKind.Blocked, true, 0, 370, false));
        Assert.Equal(0.3, GameFeel.Shock(FeedbackKind.PerfectParry, true, 0, 370, false));
        Assert.Equal(0.5, GameFeel.Shock(FeedbackKind.GuardBroken, true, 0, 370, false));
        Assert.Equal(0.12, GameFeel.Shock(FeedbackKind.CriticalHit, true, 0, 370, false));
        Assert.Equal(0, GameFeel.Shock(FeedbackKind.DamageTaken, false, 120, 0, false));   // un adversaire touché ne secoue pas la vue
        Assert.Equal(0.5, GameFeel.Shock(FeedbackKind.Died, false, 0, 0, true));
        Assert.Equal(0, GameFeel.Shock(FeedbackKind.Died, false, 0, 0, false));
        Assert.Equal(0, GameFeel.Shock(FeedbackKind.Unavailable, true, 0, 370, false));
    }

    [Fact]
    public void Le_choc_s_ajoute_borne_et_retombe_seul()
    {
        Assert.Equal(1, GameFeel.Add(0.8, 0.8));
        Assert.Equal(0.5, GameFeel.Add(0.5, -3));
        Assert.Equal(0, GameFeel.Settle(0.5, 10));
        Assert.Equal(0.5 - GameFeel.ShockDecay * 0.1, GameFeel.Settle(0.5, 0.1), 9);
        Assert.Equal(0.4, GameFeel.Settle(0.4, -1));
        Assert.Equal((0.0, 0.0, 0.0), GameFeel.Offset(0, 12.3));
        for (double t = 0; t < 5; t += 0.013)
        {
            (double right, double up, double roll) = GameFeel.Offset(1, t);
            Assert.InRange(Math.Abs(right), 0, GameFeel.MaxOffset);
            Assert.InRange(Math.Abs(up), 0, GameFeel.MaxOffset);
            Assert.InRange(Math.Abs(roll), 0, GameFeel.MaxRoll);
            (double r2, _, _) = GameFeel.Offset(0.5, t);
            Assert.Equal(right * 0.5, r2, 9);   // relecture de l'essai 7 : proportionnel au choc
        }
        Assert.Equal(GameFeel.Offset(1.7, 2), GameFeel.Offset(1, 2));   // borné à 1
        // Relecture de l'essai 7 : un coup gardé se voit (plus de 2 cm au plus fort) et dure plus d'un dixième de seconde.
        double guard = Enumerable.Range(0, 400).Max(i => Math.Abs(GameFeel.Offset(0.15, i * 0.01).Right));
        Assert.True(guard > 0.02, $"coup gardé : {guard} m");
        Assert.True(0.15 / GameFeel.ShockDecay > 0.1);
    }
}

/// <summary>
/// Essai réaliste 7 : ce qu'écoutent la musique et les ambiances (<see cref="AdventureSession.Mood"/>) : le village, le combat (la règle
/// de la halte), le Gardien et la rivière, sans rien changer au monde.
/// </summary>
[Trait("Category", "World")]
public class Essai7MoodTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_ambiance_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private AdventureSession Start(bool wolfNearEntry)
    {
        string scenarios = Path.Combine(AppContext.BaseDirectory, "scenarios");
        if (wolfNearEntry)
        {
            scenarios = Path.Combine(_folder, "scenarios");
            Directory.CreateDirectory(Path.Combine(scenarios, "zones"));
            JsonObject vallons = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
            foreach (JsonNode? c in vallons["data"]!["combatants"]!.AsArray())
                if ((string)c!["id"]! == "loup1") c["position"] = new JsonArray(-15, -12);
            File.WriteAllText(Path.Combine(scenarios, "zones", "vallons.json"), vallons.ToJsonString());
            File.Copy(ScenarioPath("zones/vallons_chef"), Path.Combine(scenarios, "zones", "vallons_chef.json"));
        }
        string savePath = Path.Combine(_folder, "partie", "sauvegarde.json");
        AdventureSession a = AdventureSession.Load(DataDirectory, scenarios, savePath);
        a.DrainMessages();
        a.Advance(0.05);
        return a;
    }

    [Fact]
    public void Au_depart_le_village_au_calme_et_la_riviere_proche()
    {
        AdventureSession a = Start(false);
        AdventureMood mood = a.Mood();
        Assert.True(mood.InVillage);
        Assert.False(mood.Fighting);
        Assert.False(mood.BossEngaged);
        Assert.False(double.IsNaN(mood.RiverDistance));
        Assert.InRange(mood.RiverDistance, 0, 40);
        // Rien ne change dans le monde : deux lectures donnent la même chose, le temps n'a pas avancé.
        long tick = a.Combat.Tick;
        AdventureMood again = a.Mood();
        Assert.Equal(mood.InVillage, again.InVillage);
        Assert.Equal(mood.RiverDistance, again.RiverDistance);
        Assert.Equal(tick, a.Combat.Tick);
    }

    [Fact]
    public void Un_loup_tout_proche_c_est_le_combat()
    {
        AdventureSession a = Start(true);
        AdventureMood mood = a.Mood();
        Assert.True(mood.Fighting);
        Assert.False(mood.BossEngaged);
        Assert.Equal(StyleMusic.Combat, StyleRules.MusicFor(mood.BossEngaged, mood.Fighting, mood.InVillage));
    }

    [Fact]
    public void Les_annonces_comptent_les_quetes_et_les_butins()
    {
        // Relecture de l'essai 7 : les compteurs avancent vraiment. Données copiées, le point d'arrivée à côté d'Odile ; la mission à son
        // retour, Odile en confiance : l'amulette de l'aube (un objet donné), puis la mission finie.
        string data = Path.Combine(_folder, "donnees");
        foreach (string file in Directory.GetFiles(DataDirectory, "*", SearchOption.AllDirectories))
        {
            string to = Path.Combine(data, Path.GetRelativePath(DataDirectory, file));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to);
        }
        string zonePath = Path.Combine(data, "world", "zones", "vallons.json");
        JsonObject zone = JsonNode.Parse(File.ReadAllText(zonePath))!.AsObject();
        zone["data"]!["safePoints"]![0]!["position"] = new JsonArray(-9, 2);
        File.WriteAllText(zonePath, zone.ToJsonString());
        var progression = ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(data), ShippedData).Progression!;
        var story = new SavedStory(null, new Dictionary<string, QuestProgress> { ["quest.traces_sous_le_moulin"] = new QuestProgress("retour") },
            new Dictionary<string, int> { ["npc.odile"] = 1 });
        string savePath = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        SaveStore.Write(savePath, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(),
            PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, progression).WithStory(story)));
        AdventureSession a = AdventureSession.Load(data, Path.Combine(AppContext.BaseDirectory, "scenarios"), savePath);
        a.Advance(0.05);
        a.DrainMessages();
        Assert.Equal((0, 0), (a.QuestsAnnounced, a.LootAnnounced));
        Assert.True(a.Interact());
        Assert.Equal(DialogueRefusal.None, a.Choose(0));
        Assert.Equal(1, a.LootAnnounced);
        a.Advance(0.05);
        Assert.Equal(1, a.QuestsAnnounced);
        Assert.Equal(1, a.LootAnnounced);
    }

    [Fact]
    public void Le_loup_qui_va_mordre_se_voit_avant_le_coup()
    {
        // Relecture de l'essai 7 : la morsure n'est jamais annoncée ; son geste part avant le tour du loup, cible à portée, et le coup tombe
        // bien à ce tour.
        AdventureSession a = Start(true);
        RealtimeCombat c = a.Combat;
        double lead = 0.45 / c.TickSeconds;
        long? bite = null;
        for (int i = 0; i < 600 && bite is null; i++)
        {
            CombatantView wolf = c.Others().First(o => o.Id == "loup1").View;
            if (GameFeel.Imminent(wolf, c.Player().View, c.Tick + c.TickProgress, lead) is AssailantAttack attack)
            {
                Assert.Equal("morsure", attack.Id);
                bite = wolf.NextAttackTick;
                Assert.InRange(bite!.Value - (c.Tick + c.TickProgress), 0, lead);
            }
            else a.Advance(0.05);
        }
        Assert.NotNull(bite);
        long before = c.Player().View.Counters.DamageTaken;
        while (c.Tick <= bite) a.Advance(0.05);
        Assert.True(c.Player().View.Counters.DamageTaken > before, "le loup n'a pas mordu au tour annoncé par le geste");
    }

    [Fact]
    public void Pas_de_geste_d_avance_pour_un_loup_loin_ni_pour_une_attaque_annoncee()
    {
        AdventureSession a = Start(false);
        RealtimeCombat c = a.Combat;
        double now = c.Tick + c.TickProgress;
        foreach (OtherState o in c.Others().Where(o => o.IsEnemy))
        {
            // Rien n'est engagé au départ, et rien n'est à portée du joueur au village.
            Assert.Null(GameFeel.Imminent(o.View, c.Player().View, now, 1e9));
            // La charge du sanglier (une ligne annoncée) a son geste par l'annonce, jamais par ce chemin.
            if (o.Id.StartsWith("sanglier", StringComparison.Ordinal)) Assert.Null(GameFeel.Imminent(o.View, o.View, o.View.NextAttackTick ?? 0, 1e9, 1e9));
        }
    }
}
