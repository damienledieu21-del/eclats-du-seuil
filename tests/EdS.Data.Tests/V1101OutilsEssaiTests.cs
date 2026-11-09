using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using EdS.Runtime;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>V1.10.1 : sauvegarde du dossier des ressources (vrais fichiers temporaires) et décision d'ouverture de la scène de démarrage.</summary>
public sealed class V1101OutilsEssaiTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "eds-essai-" + Guid.NewGuid().ToString("N"));
    private static readonly DateTime Now = new(2026, 11, 6, 14, 5, 9);

    public V1101OutilsEssaiTests() => Directory.CreateDirectory(_root);

    public void Dispose() { try { Directory.Delete(_root, true); } catch (IOException) { } }

    private string Source()
    {
        string src = Path.Combine(_root, "Style");
        Directory.CreateDirectory(Path.Combine(src, "Sketchfab", "Arbres"));
        File.WriteAllText(Path.Combine(src, "a.txt"), "alpha");
        File.WriteAllBytes(Path.Combine(src, "Sketchfab", "Arbres", "oak.bin"), new byte[] { 1, 2, 3, 0, 255 });
        File.WriteAllText(Path.Combine(src, "Sketchfab", "b é.txt"), "bêta");
        return src;
    }

    private static Dictionary<string, byte[]> Tree(string src) => Directory.GetFiles(src, "*", SearchOption.AllDirectories)
        .ToDictionary(f => "Style/" + f.Substring(src.Length).TrimStart(Path.DirectorySeparatorChar).Replace('\\', '/'), File.ReadAllBytes);

    [Fact]
    public void Sauvegarde_reussie_zip_identique_au_dossier_et_source_intacte()
    {
        string src = Source();
        Dictionary<string, byte[]> before = Tree(src);
        var steps = new List<(int, int)>();
        BackupResult r = EssaiTools.Backup(src, Path.Combine(_root, "out"), Now, (d, t) => steps.Add((d, t)));
        Assert.Equal(BackupOutcome.Success, r.Outcome);
        Assert.True(r.Succeeded);
        Assert.Equal(3, r.Entries);
        Assert.Equal(Path.Combine(_root, "out", "Style-2026-11-06_14-05-09.zip"), r.Path);
        Assert.False(File.Exists(r.Path + EssaiTools.DraftSuffix));
        Assert.Contains((0, 3), steps);
        using ZipArchive zip = ZipFile.OpenRead(r.Path!);
        var inZip = zip.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var ms = new MemoryStream();
            using (Stream s = e.Open()) s.CopyTo(ms);
            return ms.ToArray();
        });
        Assert.Equal(before.Keys.OrderBy(k => k, StringComparer.Ordinal), inZip.Keys.OrderBy(k => k, StringComparer.Ordinal));
        foreach (var kv in before) Assert.Equal(kv.Value, inZip[kv.Key]);
        Assert.Equal(before.Count, Tree(src).Count);   // rien supprimé
        foreach (var kv in before) Assert.Equal(kv.Value, File.ReadAllBytes(Path.Combine(src, kv.Key.Substring("Style/".Length))));
    }

    [Fact]
    public void Refus_d_ecraser_un_zip_ou_un_brouillon_existant()
    {
        string src = Source(), outDir = Path.Combine(_root, "out");
        BackupResult first = EssaiTools.Backup(src, outDir, Now);
        byte[] bytes = File.ReadAllBytes(first.Path!);
        BackupResult again = EssaiTools.Backup(src, outDir, Now);
        Assert.Equal(BackupOutcome.TargetExists, again.Outcome);
        Assert.Equal(bytes, File.ReadAllBytes(first.Path!));
        // Un brouillon laissé par une sauvegarde interrompue n'est ni écrasé ni supprimé.
        File.Delete(first.Path!);
        File.WriteAllText(first.Path + EssaiTools.DraftSuffix, "reste");
        BackupResult draft = EssaiTools.Backup(src, outDir, Now);
        Assert.Equal(BackupOutcome.TargetExists, draft.Outcome);
        Assert.Equal("reste", File.ReadAllText(first.Path + EssaiTools.DraftSuffix));
        Assert.Contains(EssaiTools.DraftSuffix, draft.Message);
    }

    [Fact]
    public void Dossier_source_absent_n_ecrit_rien()
    {
        string outDir = Path.Combine(_root, "out");
        BackupResult r = EssaiTools.Backup(Path.Combine(_root, "absent"), outDir, Now);
        Assert.Equal(BackupOutcome.SourceMissing, r.Outcome);
        Assert.False(Directory.Exists(outDir));
    }

    [Fact]
    public void Dossier_cible_impossible_echoue_clairement_sans_toucher_la_source()
    {
        string src = Source();
        string blocker = Path.Combine(_root, "fichier");   // un fichier là où il faudrait un dossier
        File.WriteAllText(blocker, "x");
        Dictionary<string, byte[]> before = Tree(src);
        BackupResult r = EssaiTools.Backup(src, Path.Combine(blocker, "sous"), Now);
        Assert.Equal(BackupOutcome.Incomplete, r.Outcome);
        Assert.StartsWith("Échec", r.Message);
        Assert.Contains("Rien du projet n'a été supprimé", r.Message);
        Assert.Equal("x", File.ReadAllText(blocker));
        Assert.Equal(before.Count, Tree(src).Count);
    }

    [Fact]
    public void Ecriture_interrompue_laisse_un_brouillon_nomme_et_garde_la_source()
    {
        string src = Source();
        string locked = Path.Combine(src, "verrou.bin");
        File.WriteAllText(locked, "v");
        string outDir = Path.Combine(_root, "out");
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            BackupResult r = EssaiTools.Backup(src, outDir, Now);
            if (OperatingSystem.IsWindows())   // sous Linux le verrou n'est que consultatif : la lecture réussit
            {
                Assert.Equal(BackupOutcome.Incomplete, r.Outcome);
                Assert.EndsWith(EssaiTools.DraftSuffix, r.Path);
                Assert.True(File.Exists(r.Path));
            }
        }
        Assert.True(File.Exists(locked));
        Assert.Equal("alpha", File.ReadAllText(Path.Combine(src, "a.txt")));
    }

    [Theory]
    [InlineData(@"C:\Users\d\OneDrive\Documents\EdS", true)]
    [InlineData(@"C:\Users\d\onedrive - perso\Documents", true)]
    [InlineData(@"C:\Users\d\Documents\EdS", false)]
    public void Detection_de_OneDrive(string path, bool expected) => Assert.Equal(expected, EssaiTools.IsInOneDrive(path));

    [Fact]
    public void Nom_horodate_stable() => Assert.Equal("Style-2026-11-06_14-05-09.zip", EssaiTools.BackupFileName(Now));

    [Theory]
    [InlineData("Assets/EdS/Scenes/Jeu.unity", 1, false)]    // déjà la seule scène : on ne touche à rien
    [InlineData("Assets/EdS/Scenes/Jeu.unity", 2, true)]     // scènes additives en trop : question puis ouverture seule
    [InlineData("Assets/Autre.unity", 1, true)]
    [InlineData("", 1, true)]
    [InlineData("", 3, true)]
    public void Ouverture_de_la_scene_seule(string active, int count, bool expected)
        => Assert.Equal(expected, EssaiTools.ShouldOpenStartupSceneAlone(active, count, "Assets/EdS/Scenes/Jeu.unity"));

    [Theory]
    [InlineData(StartupSceneState.Cancelled, true, LaunchStep.StopSilently)]    // une seule annulation arrête tout, sans message
    [InlineData(StartupSceneState.Cancelled, false, LaunchStep.StopSilently)]
    [InlineData(StartupSceneState.Failed, true, LaunchStep.StopWithMessage)]
    [InlineData(StartupSceneState.Ready, false, LaunchStep.StopWithMessage)]
    [InlineData(StartupSceneState.Ready, true, LaunchStep.Continue)]
    public void Suite_apres_la_scene_de_demarrage(StartupSceneState state, bool exists, LaunchStep expected)
        => Assert.Equal(expected, EssaiTools.AfterStartupScene(state, exists));

    [Theory]
    [InlineData(true, true, StartupSceneState.Ready, true, PreparedLaunchStep.AlreadyPlaying)]
    [InlineData(true, false, StartupSceneState.Cancelled, false, PreparedLaunchStep.AlreadyPlaying)]   // le jeu lancé passe avant tout
    [InlineData(false, false, StartupSceneState.Ready, true, PreparedLaunchStep.NeedsPreparation)]
    [InlineData(false, false, StartupSceneState.Failed, false, PreparedLaunchStep.NeedsPreparation)]
    [InlineData(false, true, StartupSceneState.Cancelled, true, PreparedLaunchStep.StopSilently)]
    [InlineData(false, true, StartupSceneState.Failed, true, PreparedLaunchStep.StopWithMessage)]
    [InlineData(false, true, StartupSceneState.Ready, false, PreparedLaunchStep.StopWithMessage)]
    [InlineData(false, true, StartupSceneState.Ready, true, PreparedLaunchStep.Play)]
    public void Lancement_deja_prepare(bool playing, bool catalog, StartupSceneState state, bool exists, PreparedLaunchStep expected)
        => Assert.Equal(expected, EssaiTools.PreparedLaunch(playing, catalog, state, exists));

    [Theory]
    [InlineData(true, LaunchStep.Continue)]
    [InlineData(false, LaunchStep.StopSilently)]   // annuler la question d'enregistrement arrête tout
    public void Suite_apres_la_question_denregistrement(bool accepted, LaunchStep expected)
        => Assert.Equal(expected, EssaiTools.AfterSceneSave(accepted));
}
