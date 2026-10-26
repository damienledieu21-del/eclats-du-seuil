using EdS.Runtime;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 9.3 : Unity compile le cœur directement depuis <c>src/</c>, paquet local <c>com.eclats.core</c> (définitions d'assemblage
/// à côté des projets .NET). Ces tests gardent les deux descriptions d'accord : mêmes assemblages, mêmes références, aucun moteur Unity
/// dans le cœur, outils .NET exclus de Unity, aucune sortie de compilation dans <c>src/</c>.
/// </summary>
[Trait("Category", "Runtime")]
public class Lot93UnityPackageTests
{
    private static readonly string[] Portable = { "EdS.Domain", "EdS.Simulation", "EdS.Data", "EdS.Data.Json", "EdS.Runtime", "EdS.Net", "EdS.World" };   // sous-lot 12.1 : EdS.World
    private static readonly string[] Tools = { "EdS.Tools", "EdS.Cli", "EdS.Server" };

    /// <summary>Racine du dépôt (dossier qui contient EdS.sln), retrouvée depuis le dossier des tests.</summary>
    internal static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "EdS.sln"))) directory = directory.Parent;
            Assert.NotNull(directory);
            return directory!.FullName;
        }
    }

    private static JsonObject Json(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    private static JsonObject Asmdef(string project) => Json(Path.Combine(Root, "src", project, project + ".asmdef"));

    /// <summary>Projets référencés par un .csproj (noms de fichier sans extension).</summary>
    private static string[] CsprojReferences(string project) =>
        XDocument.Load(Path.Combine(Root, "src", project, project + ".csproj")).Descendants("ProjectReference")
            .Select(r => Path.GetFileNameWithoutExtension(r.Attribute("Include")!.Value.Replace('\\', '/'))).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    [Fact]
    public void Paquet_local_du_coeur_declare_et_reference_par_le_projet_Unity()
    {
        JsonObject package = Json(Path.Combine(Root, "src", "package.json"));
        Assert.Equal("com.eclats.core", package["name"]!.GetValue<string>());
        Assert.Equal("6000.3", package["unity"]!.GetValue<string>());
        JsonObject manifest = Json(Path.Combine(Root, "unity", "Packages", "manifest.json"));
        JsonObject dependencies = manifest["dependencies"]!.AsObject();
        Assert.Equal("file:../../src", dependencies["com.eclats.core"]!.GetValue<string>());
        Assert.True(dependencies.ContainsKey("com.unity.render-pipelines.universal"));
        // Essai de style : les modèles animés (composant Animation) et les sons (AudioSource) demandent ces modules intégrés ; sans le
        // premier, Unity refusait de compiler le jeu (essai de Damien du 26 octobre 2026, erreur CS1069 sur Animation).
        Assert.Equal("1.0.0", dependencies["com.unity.modules.animation"]!.GetValue<string>());
        Assert.Equal("1.0.0", dependencies["com.unity.modules.audio"]!.GetValue<string>());
        // Essai réaliste (26 octobre 2026) : le sol en terrain (Terrain, TerrainData) demande le module intégré du terrain.
        Assert.Equal("1.0.0", dependencies["com.unity.modules.terrain"]!.GetValue<string>());
        Assert.StartsWith("m_EditorVersion: 6000.3.", File.ReadAllText(Path.Combine(Root, "unity", "ProjectSettings", "ProjectVersion.txt")));
    }

    [Theory]
    [InlineData("EdS.Domain")]
    [InlineData("EdS.Simulation")]
    [InlineData("EdS.Data")]
    [InlineData("EdS.Data.Json")]
    [InlineData("EdS.Runtime")]
    [InlineData("EdS.Net")]
    [InlineData("EdS.World")]
    public void Assemblage_Unity_conforme_au_projet_dotnet(string project)
    {
        JsonObject asmdef = Asmdef(project);
        Assert.Equal(project, asmdef["name"]!.GetValue<string>());
        Assert.True(asmdef["noEngineReferences"]!.GetValue<bool>(), "le cœur ne dépend pas d'UnityEngine");
        Assert.Empty(asmdef["defineConstraints"]!.AsArray());
        Assert.Empty(asmdef["precompiledReferences"]!.AsArray());
        Assert.False(asmdef["allowUnsafeCode"]!.GetValue<bool>());

        // Références directes : celles du .csproj, plus leurs références transitives (Unity ne les ajoute pas d'elles-mêmes).
        var expected = new SortedSet<string>(StringComparer.Ordinal);
        void Add(string p)
        {
            foreach (string r in CsprojReferences(p))
                if (expected.Add(r)) Add(r);
        }
        Add(project);
        Assert.Equal(expected, asmdef["references"]!.AsArray().Select(r => r!.GetValue<string>()).OrderBy(r => r, StringComparer.Ordinal));
        Assert.Equal("-nullable:enable", File.ReadAllText(Path.Combine(Root, "src", project, "csc.rsp")).Trim());
    }

    [Theory]
    [InlineData("EdS.Tools")]
    [InlineData("EdS.Cli")]
    [InlineData("EdS.Server")]
    public void Outils_dotnet_exclus_de_Unity(string project) =>
        Assert.Equal(new[] { "EDS_OUTIL_DOTNET_HORS_UNITY" }, Asmdef(project)["defineConstraints"]!.AsArray().Select(c => c!.GetValue<string>()));

    [Fact]
    public void Tout_projet_de_src_a_sa_definition_d_assemblage()
    {
        string[] projects = Directory.GetDirectories(Path.Combine(Root, "src")).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()!;
        Assert.Equal(Portable.Concat(Tools).OrderBy(n => n, StringComparer.Ordinal), projects);
        foreach (string project in projects) Assert.True(File.Exists(Path.Combine(Root, "src", project, project + ".asmdef")), project);
    }

    [Fact]
    public void Aucune_sortie_de_compilation_dans_src()
    {
        // UseArtifactsOutput (Directory.Build.props) : bin et obj vont dans artifacts/ ; Unity compilerait sinon leurs .cs générés et
        // importerait leurs .dll en double.
        foreach (string project in Directory.GetDirectories(Path.Combine(Root, "src")))
        {
            Assert.False(Directory.Exists(Path.Combine(project, "bin")), project + "/bin");
            Assert.False(Directory.Exists(Path.Combine(project, "obj")), project + "/obj");
        }
        Assert.Contains("<UseArtifactsOutput>true</UseArtifactsOutput>", File.ReadAllText(Path.Combine(Root, "Directory.Build.props")), StringComparison.Ordinal);
    }

    [Fact]
    public void Version_de_l_essai_et_fichiers_generes_par_Unity_non_suivis()
    {
        // Sous-lot 9.3.1 (essai de Damien du 18/10) : version de l'éditeur de l'essai, réglages par défaut créés par URP non suivis
        // (sinon GitHub Desktop les propose à chaque ouverture). Seuls manifest.json, ProjectVersion.txt et Assets/EdS sont suivis.
        Assert.Equal(new[] { "m_EditorVersion: 6000.3.26f1", "m_EditorVersionWithRevision: 6000.3.26f1 (de8167e7440c)" },
            File.ReadAllLines(Path.Combine(Root, "unity", "ProjectSettings", "ProjectVersion.txt")));
        string[] ignored = File.ReadAllLines(Path.Combine(Root, ".gitignore"));
        foreach (string rule in new[]
        {
            "unity/[Ll]ibrary/", "unity/ProjectSettings/*", "!unity/ProjectSettings/ProjectVersion.txt", "unity/Assets/EdS/Settings/",
            "unity/Assets/DefaultVolumeProfile.asset", "unity/Assets/UniversalRenderPipelineGlobalSettings.asset",
        })
            Assert.Contains(rule, ignored);
    }

    [Fact]
    public void Scripts_du_jeu_dans_un_assemblage_qui_reference_le_coeur()
    {
        JsonObject game = Json(Path.Combine(Root, "unity", "Assets", "EdS", "Game", "EdS.Game.asmdef"));
        // Essai réaliste (26 octobre 2026) : le jeu règle aussi les couleurs de l'image (volume d'URP), d'où les deux assemblages d'URP.
        Assert.Equal(Portable.Concat(new[] { "Unity.RenderPipelines.Core.Runtime", "Unity.RenderPipelines.Universal.Runtime" }).OrderBy(n => n, StringComparer.Ordinal),
            game["references"]!.AsArray().Select(r => r!.GetValue<string>()).OrderBy(r => r, StringComparer.Ordinal));
        JsonObject editor = Json(Path.Combine(Root, "unity", "Assets", "EdS", "Editor", "EdS.Editor.asmdef"));
        Assert.Equal(new[] { "Editor" }, editor["includePlatforms"]!.AsArray().Select(p => p!.GetValue<string>()));
        // Scénarios proposés par le prototype : tous présents dans le dépôt.
        string gameScript = File.ReadAllText(Path.Combine(Root, "unity", "Assets", "EdS", "Game", "EdsGame.cs"));
        foreach (string scenario in new[] { "vallons_demo.json", "meute_a_vaincre.json", "entrainement_garde_parade.json", "mesures/s1_mannequin.json", "mesures/s2_duel.json", "mesures/s3_meute.json", "mesures/s4_protection.json", "mesures/s5_briseur.json" })
        {
            Assert.Contains($"\"{scenario}\"", gameScript, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(Root, "scenarios", scenario)), scenario);
        }
        // Scénarios du jeu en réseau (sous-lots 11.1 et 11.7).
        foreach (string scenario in new[] { "coop/meute_a_vaincre.json", "coop/vallons_duo.json" })
        {
            Assert.Contains($"\"{scenario}\"", gameScript, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(Root, "scenarios", scenario)), scenario);
        }
    }

    [Fact]
    public void Donnees_emportees_dans_le_jeu_construit()
    {
        // Sous-lot 11.8 (revue Codex de la PR #36) : la copie de construction (BundledData.CopyTo, appelée par EdsBuildData avant la
        // construction) est exercée pour de vrai : tous les .json de data/v0 et de scenarios, mêmes octets, liste avec l'empreinte du
        // contenu, relue telle quelle par le jeu (BundledData.ParseIndex, dans GameFiles).
        string target = Path.Combine(Path.GetTempPath(), "eds-emportees-" + Guid.NewGuid().ToString("N"));
        try
        {
            IReadOnlyList<string> files = BundledData.CopyTo(Root, target);
            List<string> expected = BundledData.Sources.SelectMany(s => Directory.GetFiles(Path.Combine(Root, s), "*.json", SearchOption.AllDirectories))
                .Select(f => Path.GetRelativePath(Root, f).Replace('\\', '/')).OrderBy(f => f, StringComparer.Ordinal).ToList();
            Assert.Equal(expected, files);
            Assert.Contains("data/v0/rules.json", files);
            Assert.Contains("scenarios/coop/vallons_duo.json", files);
            foreach (string f in files) Assert.Equal(File.ReadAllBytes(Path.Combine(Root, f)), File.ReadAllBytes(Path.Combine(target, f)));
            string index = File.ReadAllText(Path.Combine(target, BundledData.IndexFile));
            Assert.Matches("^# empreinte [0-9a-f]{64}\n", index);
            Assert.Equal(files, BundledData.ParseIndex(index));
            // Même contenu, même liste ; un octet changé aux mêmes chemins, une autre liste (la copie Android est alors refaite).
            Assert.Equal(index, BundledData.Index(files, f => File.ReadAllBytes(Path.Combine(Root, f))));
            Assert.NotEqual(index, BundledData.Index(files, f => f == files[0] ? new byte[] { 1 } : File.ReadAllBytes(Path.Combine(Root, f))));
            // Une nouvelle copie retire l'ancienne : un fichier retiré depuis ne reste pas.
            File.WriteAllText(Path.Combine(target, "data", "v0", "retire.json"), "{}");
            BundledData.CopyTo(Root, target);
            Assert.False(File.Exists(Path.Combine(target, "data", "v0", "retire.json")));
            // Une liste retouchée qui ferait écrire hors du dossier de l'application est refusée.
            foreach (string bad in new[] { "../x.json", "/etc/x.json", "data\\v0\\x.json" })
                Assert.Throws<FormatException>(() => BundledData.ParseIndex("# empreinte 00\n" + bad + "\n"));
        }
        finally
        {
            if (Directory.Exists(target)) Directory.Delete(target, true);
        }
        // La construction et le jeu passent par BundledData ; la copie dans le projet Unity n'est jamais suivie par git.
        string build = File.ReadAllText(Path.Combine(Root, "unity", "Assets", "EdS", "Editor", "EdsBuildData.cs"));
        string gameFiles = File.ReadAllText(Path.Combine(Root, "unity", "Assets", "EdS", "Game", "GameFiles.cs"));
        Assert.Contains("BundledData.CopyTo(Repo, Target)", build, StringComparison.Ordinal);
        Assert.Contains("\"StreamingAssets\", BundledData.Folder", build, StringComparison.Ordinal);
        Assert.Contains("BundledData.ParseIndex(index)", gameFiles, StringComparison.Ordinal);
        Assert.Contains("Directory.Delete(target, true)", gameFiles, StringComparison.Ordinal);
        Assert.Contains("\"EdS.Runtime\"", File.ReadAllText(Path.Combine(Root, "unity", "Assets", "EdS", "Editor", "EdS.Editor.asmdef")), StringComparison.Ordinal);
        string ignore = File.ReadAllText(Path.Combine(Root, ".gitignore"));
        Assert.Contains("unity/Assets/StreamingAssets/", ignore, StringComparison.Ordinal);
        // Revue Codex de la PR #36 (2e tour) : une écriture impossible (stockage plein) est rendue comme une erreur ; les doigts posés sont
        // oubliés quand le jeu est suspendu, et un doigt mémorisé qui n'est plus sur l'écran est relâché.
        Assert.Contains("copie des données du jeu impossible", gameFiles, StringComparison.Ordinal);
        string game = File.ReadAllText(Path.Combine(Root, "unity", "Assets", "EdS", "Game", "EdsGame.cs"));
        string touch = File.ReadAllText(Path.Combine(Root, "unity", "Assets", "EdS", "Game", "TouchControls.cs"));
        Assert.Contains("private void OnApplicationPause(bool paused)", game, StringComparison.Ordinal);
        Assert.Contains("private void OnApplicationFocus(bool focus)", game, StringComparison.Ordinal);
        // Revue Codex de la PR #36 (6e tour) : le traitement des doigts (doigt perdu compris) est dans TouchGestures, testé par Lot118TouchTests.
        Assert.Contains("_gestures.Update(Fingers(), _zones, Hud.Width);", touch, StringComparison.Ordinal);
        // Tout ce que le jeu ouvre est un .json de data/v0 ou de scenarios : la copie n'en oublie aucun.
        Assert.All(Directory.GetFiles(Path.Combine(Root, "scenarios"), "*", SearchOption.AllDirectories), f => Assert.EndsWith(".json", f, StringComparison.Ordinal));
    }

    [Fact]
    public void Joueur_absent_ni_deplace_ni_vise_a_l_affichage()
    {
        // Revue Codex de la PR #32 (30e tour) : scripts Unity compilés seulement (contre des bouchons) ; le joueur absent (avant son
        // arrivée, après son départ) ne bouge ni ne se tourne à l'affichage, et les ennemis ne se tournent pas vers lui. Essai : Damien.
        string game = File.ReadAllText(Path.Combine(Root, "unity", "Assets", "EdS", "Game", "EdsGame.cs"));
        string arena = File.ReadAllText(Path.Combine(Root, "unity", "Assets", "EdS", "Game", "ArenaView.cs"));
        int move = game.IndexOf("private void Move(PlayerState player)", StringComparison.Ordinal);
        Assert.True(move > 0);
        Assert.True(game.IndexOf("if (!player.View.IsPresent)", move, StringComparison.Ordinal) is int guard && guard > move
            && guard < game.IndexOf("_arena.MovePlayer(delta, _playerYaw);", move, StringComparison.Ordinal), "garde d'absence avant le déplacement");
        Assert.Contains("if (me.IsPresent && other.IsEnemy", arena, StringComparison.Ordinal);
        Assert.Contains("_player.SetActive(me.IsPresent);", arena, StringComparison.Ordinal);
        // Revue Codex de la PR #34 : en pause, la marche voulue d'un combat positionné n'est pas transmise.
        int spatial = game.IndexOf("if (_arena.Spatial)", move, StringComparison.Ordinal);
        Assert.True(spatial > 0 && game.IndexOf("if (_paused)", spatial, StringComparison.Ordinal) is int paused && paused > spatial
            && paused < game.IndexOf("_combat.SetMove(wish.x, wish.z, backward);", spatial, StringComparison.Ordinal), "pause avant la marche voulue");
    }

    [Fact]
    public void Recul_en_vue_2_et_scene_de_demarrage()
    {
        // Sous-lot 11.9 (essai de Damien du 22 octobre 2026) : scripts Unity compilés seulement (contre des bouchons) ; essai : Damien.
        // En vue 2, reculer marche à reculons (le personnage garde son orientation) ; en vue 1, il se tourne vers sa marche.
        string game = File.ReadAllText(Path.Combine(Root, "unity", "Assets", "EdS", "Game", "EdsGame.cs"));
        Assert.Contains("bool backward = _camera.Setting > 0.5f && z < 0f;", game, StringComparison.Ordinal);
        Assert.Contains("_combat.SetMove(wish.x, wish.z, backward);", game, StringComparison.Ordinal);
        Assert.Contains("if (!backward) _playerYaw = Quaternion.LookRotation(direction).eulerAngles.y;", game, StringComparison.Ordinal);
        // Scène de démarrage créée à l'ouverture du projet, en tête de la liste de construction, fichier généré non suivi par git.
        string scene = File.ReadAllText(Path.Combine(Root, "unity", "Assets", "EdS", "Editor", "EdsStartupScene.cs"));
        Assert.Contains("public const string ScenePath = \"Assets/EdS/Scenes/Jeu.unity\";", scene, StringComparison.Ordinal);
        Assert.Contains("[InitializeOnLoad]", scene, StringComparison.Ordinal);
        // Revue Codex de la PR #39 : la scène sans nom ouverte (Unity 6 la marque modifiée dès sa création) est enregistrée comme scène de
        // démarrage, au lieu d'abandonner la préparation.
        Assert.Contains("bool untitled = string.IsNullOrEmpty(active.path);", scene, StringComparison.Ordinal);
        Assert.Contains("if (untitled)", scene, StringComparison.Ordinal);
        Assert.DoesNotContain("enregistre d'abord la scène ouverte", scene, StringComparison.Ordinal);
        // 3e tour (accord de Damien du 23 octobre 2026) : une scène sans nom modifiée n'est fermée qu'après la question de Unity ; un
        // enregistrement qui échoue arrête la préparation avant d'inscrire la scène dans la liste de construction.
        Assert.Contains("(!active.isDirty || EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())", scene, StringComparison.Ordinal);
        Assert.DoesNotContain("GetRootGameObjects", scene, StringComparison.Ordinal);
        int failed = scene.IndexOf("if (!saved)", StringComparison.Ordinal);
        Assert.True(failed > 0 && failed < scene.IndexOf("EditorBuildSettings.scenes = ", StringComparison.Ordinal), "échec d'enregistrement vérifié avant la liste de construction");
        Assert.Contains("EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }", scene, StringComparison.Ordinal);
        string ignore = File.ReadAllText(Path.Combine(Root, ".gitignore"));
        Assert.Contains("unity/Assets/EdS/Scenes/", ignore, StringComparison.Ordinal);
    }

    [Fact]
    public void Decor_coupe_ramene_a_12_cm_quelle_que_soit_sa_hauteur()
    {
        // Revue Codex de la PR #34 : un prisme coupé (mur, tronc, moulin) garde 0,12 m de haut, sous les annonces au sol (0,16 m) : l'échelle
        // appliquée est cette hauteur rapportée à la sienne, pas 12 % de sa hauteur. Scripts Unity compilés seulement ; l'affichage, par l'essai.
        string space = File.ReadAllText(Path.Combine(Root, "unity", "Assets", "EdS", "Game", "SpaceView.cs"));
        Assert.Contains("private const float CutHeight = 0.12f;", space, StringComparison.Ordinal);
        Assert.Contains("o.Lower && height > CutHeight ? CutHeight / height : CutHeight", space, StringComparison.Ordinal);
    }
}
