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
    private static readonly string[] Portable = { "EdS.Domain", "EdS.Simulation", "EdS.Data", "EdS.Data.Json", "EdS.Runtime", "EdS.Net" };
    private static readonly string[] Tools = { "EdS.Tools", "EdS.Cli", "EdS.Server" };

    /// <summary>Racine du dépôt (dossier qui contient EdS.sln), retrouvée depuis le dossier des tests.</summary>
    private static string Root
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
        Assert.StartsWith("m_EditorVersion: 6000.3.", File.ReadAllText(Path.Combine(Root, "unity", "ProjectSettings", "ProjectVersion.txt")));
    }

    [Theory]
    [InlineData("EdS.Domain")]
    [InlineData("EdS.Simulation")]
    [InlineData("EdS.Data")]
    [InlineData("EdS.Data.Json")]
    [InlineData("EdS.Runtime")]
    [InlineData("EdS.Net")]
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
        Assert.Equal(Portable.OrderBy(n => n, StringComparer.Ordinal),
            game["references"]!.AsArray().Select(r => r!.GetValue<string>()).OrderBy(r => r, StringComparer.Ordinal));
        JsonObject editor = Json(Path.Combine(Root, "unity", "Assets", "EdS", "Editor", "EdS.Editor.asmdef"));
        Assert.Equal(new[] { "Editor" }, editor["includePlatforms"]!.AsArray().Select(p => p!.GetValue<string>()));
        // Scénarios proposés par le prototype : tous présents dans le dépôt.
        string gameScript = File.ReadAllText(Path.Combine(Root, "unity", "Assets", "EdS", "Game", "EdsGame.cs"));
        foreach (string scenario in new[] { "entrainement_garde_parade.json", "mesures/s1_mannequin.json", "mesures/s2_duel.json", "mesures/s3_meute.json", "mesures/s4_protection.json", "mesures/s5_briseur.json" })
        {
            Assert.Contains($"\"{scenario}\"", gameScript, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(Root, "scenarios", scenario)), scenario);
        }
    }
}
