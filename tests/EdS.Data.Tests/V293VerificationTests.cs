using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Runtime;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.9.3 (vérificateur d'essai dans Unity) : la liste des ressources attendues (<see cref="EssaiManifest"/>) couvre tout ce que
/// cherche le code de l'essai (lu dans les scripts), dans l'ordre où la préparation choisit ; le rapport et son résumé. Affichage seulement.
/// </summary>
[Trait("Category", "Runtime")]
public sealed class V293VerificationTests
{
    private static string Script(params string[] path) =>
        File.ReadAllText(Path.Combine(new[] { Lot93UnityPackageTests.Root, "unity", "Assets", "EdS" }.Concat(path).ToArray())).Replace("\r\n", "\n");

    private static IEnumerable<string> AllScripts() =>
        Directory.EnumerateFiles(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText);

    private static readonly IReadOnlyList<string> AllFiles = EssaiManifest.Models.SelectMany(m => m.Files).ToList();

    [Fact]
    public void Chaque_modele_attendu_est_reconnu_dans_l_ordre_de_la_preparation()
    {
        foreach (EssaiModel model in EssaiManifest.Models)
        {
            Assert.Equal(model.Files.Count, model.Animated.Count);
            foreach (string file in model.Files)
            {
                Assert.StartsWith(EssaiManifest.Root + "/", file);
                Assert.True(StyleRules.Matches(file, model.Slot), file + " n'est pas reconnu pour " + model.Slot);
            }
            // Parmi tous les fichiers attendus (toutes bêtes confondues), la préparation trouve exactement ceux de la bête, dans l'ordre déclaré.
            Assert.Equal(model.Files, StyleRules.Candidates(AllFiles, model.Slot));
        }
        Assert.Equal(EssaiManifest.Models.Count, EssaiManifest.Models.Select(m => m.Slot).Distinct().Count());
    }

    [Fact]
    public void La_liste_couvre_chaque_case_cherchee_par_la_preparation()
    {
        string style = Script("Editor", "EdsStyle.cs");
        var searched = Regex.Matches(style, @"StyleRules\.Candidates\(models, StyleSlot\.(\w+)\)").Select(m => Enum.Parse<StyleSlot>(m.Groups[1].Value)).ToHashSet();
        Assert.Contains(StyleSlot.Bear, searched);   // la lecture du script marche
        foreach (StyleSlot slot in searched) Assert.Contains(EssaiManifest.Models, m => m.Slot == slot);
        // Les seuls modèles du loup et du sanglier qui viennent d'un pack de l'Asset Store.
        var packs = Regex.Matches(style, @"PackAnimal\(allPrefabs, StyleSlot\.(\w+)").Select(m => Enum.Parse<StyleSlot>(m.Groups[1].Value)).ToHashSet();
        Assert.Equal(packs, EssaiManifest.Models.Where(m => m.PackFirst).Select(m => m.Slot).ToHashSet());
    }

    [Fact]
    public void Sons_musiques_et_ambiances_couvrent_chaque_valeur_une_fois()
    {
        Assert.Equal(Enum.GetValues<StyleSound>().OrderBy(s => s), EssaiManifest.Sounds.Select(a => a.Sound!.Value).OrderBy(s => s));
        Assert.Equal(Enum.GetValues<StyleMusic>().OrderBy(s => s), EssaiManifest.Music.Select(a => a.Music!.Value).OrderBy(s => s));
        Assert.Equal(Enum.GetValues<StyleAmbience>().OrderBy(s => s), EssaiManifest.Ambiences.Select(a => a.Ambience!.Value).OrderBy(s => s));
        foreach (EssaiAudio audio in EssaiManifest.Sounds.Concat(EssaiManifest.Music).Concat(EssaiManifest.Ambiences))
        {
            Assert.NotEmpty(audio.Words);
            Assert.StartsWith(EssaiManifest.Root + "/", audio.Folder);
        }
        // Un fichier rangé dans le dossier conseillé et nommé du premier mot est bien trouvé par la préparation.
        foreach (EssaiAudio audio in EssaiManifest.Sounds)
        {
            string file = audio.Folder.Split(' ')[0] + "/eds_" + audio.Words[0] + ".ogg";
            Assert.Equal(file, StyleRules.PickSound(new[] { file }, audio.Sound!.Value));
        }
        foreach (EssaiAudio audio in EssaiManifest.Music)
            Assert.Single(StyleRules.MusicFiles(new[] { audio.Folder + "/" + audio.Words[0] + ".ogg" }, audio.Music!.Value));
        foreach (EssaiAudio audio in EssaiManifest.Ambiences)
            Assert.Single(StyleRules.AmbienceFiles(new[] { audio.Folder + "/" + audio.Words[0] + ".ogg" }, audio.Ambience!.Value));
    }

    [Fact]
    public void La_preparation_lit_les_listes_partagees_au_lieu_de_les_recopier()
    {
        string style = Script("Editor", "EdsStyle.cs"), humans = Script("Editor", "EdsHumans.cs");
        Assert.Contains("EssaiManifest.Voices.Count(", style);
        Assert.Contains("EssaiManifest.WorldSounds.Count(", style);
        Assert.DoesNotContain("StyleSound.Meow, StyleSound.Purr", style);
        Assert.Contains("StyleRules.MusicFiles(sounds, kind)", style);
        Assert.Contains("StyleRules.AmbienceFiles(sounds, kind)", style);
        Assert.Contains("EssaiManifest.MaleBody", humans);
        Assert.Contains("EssaiManifest.AnimationLibraries", humans);
        Assert.DoesNotContain("\"Superhero_Male_FullBody.fbx\"", humans);
    }

    [Fact]
    public void Chaque_shader_cherche_par_le_jeu_est_verifie()
    {
        var searched = AllScripts().SelectMany(s => Regex.Matches(s, "Shader\\.Find\\(\"(EdS/[^\"]+)\"\\)").Select(m => m.Groups[1].Value)).ToHashSet();
        Assert.Contains(EssaiManifest.EchoShader, searched);
        Assert.Contains(EssaiManifest.RiftShader, searched);
        foreach (string name in searched) Assert.Contains(name, EssaiManifest.GameShaders);
        string game = Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game");
        foreach (string name in EssaiManifest.GameShaders)
        {
            string file = Path.Combine(game, name.Substring(4) + ".shader");
            Assert.True(File.Exists(file), file);
            Assert.Contains("Shader \"" + name + "\"", File.ReadAllText(file));
        }
    }

    [Fact]
    public void Les_corps_humains_attendus_sont_reconnus()
    {
        Assert.False(HumanLooks.BodyOf(EssaiManifest.MaleBody));
        Assert.True(HumanLooks.BodyOf(EssaiManifest.FemaleBody));
        Assert.All(EssaiManifest.AnimationLibraries, l => Assert.True(HumanLooks.IsAnimationLibrary(l)));
        IReadOnlyList<string> skins = EssaiManifest.SkinTextures;
        Assert.Equal(skins[0], HumanLooks.SkinTexture(skins, false, false));
        Assert.Equal(skins[1], HumanLooks.SkinTexture(skins, false, true));
        Assert.Equal(skins[2], HumanLooks.SkinTexture(skins, true, false));
        Assert.Equal(skins[3], HumanLooks.SkinTexture(skins, true, true));
        // Les coiffures que prend la préparation (EdsHumans), et la barbe.
        var hairs = EssaiManifest.Hairs.Select(h => HumanLooks.HairOf(h)).ToList();
        Assert.All(hairs, h => Assert.NotNull(h));
        foreach (HumanHair hair in new[] { HumanHair.Buzzed, HumanHair.SimpleParted, HumanHair.Long, HumanHair.Buns, HumanHair.BuzzedFemale })
            Assert.Contains(hairs, h => h!.Value.Hair == hair && !h.Value.Beard);
        Assert.Contains(hairs, h => h!.Value.Beard);
        foreach (HumanHair hair in Regex.Matches(Script("Editor", "EdsHumans.cs"), @"HumanHair\.(\w+)").Select(m => Enum.Parse<HumanHair>(m.Groups[1].Value)))
            Assert.Contains(hairs, h => h!.Value.Hair == hair);
        Assert.All(EssaiManifest.RequiredHumanClips, c => Assert.Contains(c, HumanLooks.CanonicalNames()));
        Assert.Equal(HumanLooks.CanonicalNames().Count, HumanLooks.CanonicalNames().Distinct().Count());
    }

    [Fact]
    public void Meilleur_modele_repli_absent_et_pack()
    {
        EssaiModel stag = EssaiManifest.Models.Single(m => m.Slot == StyleSlot.Stag);
        ModelPick best = EssaiManifest.Pick(stag, AllFiles.Concat(new[] { "Assets/Style/Ailleurs/Elan_WildMesh.fbx" }));
        Assert.Equal(ModelState.Best, best.State);
        ModelPick moved = EssaiManifest.Pick(stag, new[] { "Assets/Style/Ailleurs/Elan_WildMesh.fbx" });
        Assert.Equal(ModelState.Best, moved.State);   // seul le nom compte : un fichier rangé ailleurs est bien pris
        ModelPick fallback = EssaiManifest.Pick(stag, new[] { "Assets/Style/Animals/Quaternius/Stag_Quaternius.fbx" });
        Assert.Equal(ModelState.Fallback, fallback.State);
        Assert.Equal(3, fallback.Rank);
        (EssaiLevel level, string detail, string fix) = EssaiManifest.Describe(stag, fallback);
        Assert.Equal(EssaiLevel.Warning, level);
        Assert.Contains("Stag_Quaternius.fbx", detail);
        Assert.Contains("Elan_WildMesh.fbx dans Assets/Style/Sketchfab/Animaux2", fix);
        ModelPick other = EssaiManifest.Pick(stag, new[] { "Assets/Style/Pack/cerf_bizarre.fbx" });
        Assert.Equal((ModelState.Fallback, -1), (other.State, other.Rank));
        ModelPick none = EssaiManifest.Pick(stag, new[] { "Assets/Style/x/kitchen_table.fbx" });
        Assert.Equal(ModelState.Missing, none.State);
        Assert.Contains("formes simples", EssaiManifest.Describe(stag, none).Detail);
        EssaiModel wolf = EssaiManifest.Models.Single(m => m.Slot == StyleSlot.Wolf);
        Assert.Equal(ModelState.Best, EssaiManifest.Pick(wolf, Array.Empty<string>(), "Assets/Wolf/URP/Wolf/Prefab/Wolf_URP.prefab").State);
        Assert.Equal(ModelState.Missing, EssaiManifest.Pick(stag, Array.Empty<string>(), "Assets/Wolf/URP/Wolf/Prefab/Wolf_URP.prefab").State);
        EssaiAudio howl = EssaiManifest.Sounds.Single(s => s.Sound == StyleSound.Howl);
        Assert.Equal(EssaiLevel.Ok, EssaiManifest.Describe(howl, "Assets/Style/Sounds/Animaux/eds_loups_hurlement_FS158780_Paresh.ogg").Level);
        Assert.Contains("« hurlement »", EssaiManifest.Describe(howl, null).Fix);
    }

    [Fact]
    public void Resume_pret_avec_replis_ou_bloquant()
    {
        var report = new EssaiReport();
        Assert.StartsWith("✅", report.Summary());
        report.Add("Unity", EssaiLevel.Ok, "Rendu URP", "actif");
        Assert.Equal(EssaiLevel.Ok, report.Overall);
        report.Add("Bêtes", EssaiLevel.Warning, "Ours", "absent : ours en formes simples", "Dépose Ours_WildMesh.fbx dans Assets/Style/Sketchfab/Animaux3.");
        Assert.StartsWith("⚠️ Prêt avec replis", report.Summary());
        Assert.Contains("- Ours : absent", report.Summary());
        report.Add("Projet", EssaiLevel.Blocking, "Données du jeu (JSON)", "refusées | x", "Fais un Pull dans GitHub Desktop.");
        Assert.Equal(EssaiLevel.Blocking, report.Overall);
        string summary = report.Summary();
        Assert.StartsWith("❌ Bloquant : 1 problème à régler", summary);
        Assert.Contains("- Données du jeu (JSON) : Fais un Pull", summary);
        string md = report.ToMarkdown(new DateTime(2026, 11, 8, 14, 5, 0), "6000.3.26f1");
        Assert.Contains("Faite le 08/11/2026 à 14:05, Unity 6000.3.26f1", md);
        Assert.Contains("## Bêtes", md);
        Assert.Contains("| ⚠️ | Ours |", md);
        Assert.Contains("refusées \\| x", md);
        Assert.Equal(1, report.Count(EssaiLevel.Blocking));
    }

    [Fact]
    public void Version_de_Unity_du_projet()
    {
        string text = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "ProjectSettings", "ProjectVersion.txt"));
        string? expected = EssaiManifest.ExpectedVersion(text);
        Assert.NotNull(expected);
        Assert.Null(EssaiManifest.VersionProblem(expected!, expected));
        Assert.Null(EssaiManifest.VersionProblem("6000.3.99f1", "6000.3.26f1"));
        Assert.Contains("6000.3.26f1", EssaiManifest.VersionProblem("2022.3.10f1", "6000.3.26f1"));
        Assert.Null(EssaiManifest.ScaleProblem(1.4));
        Assert.NotNull(EssaiManifest.ScaleProblem(0));
        Assert.NotNull(EssaiManifest.ScaleProblem(140));
    }

    [Fact]
    public void Menu_lancement_et_rapport_hors_de_git()
    {
        string verification = Script("Editor", "EdsVerification.cs"), tools = Script("Editor", "EdsEssaiTools.cs");
        Assert.Contains("[MenuItem(\"Éclats du Seuil/Vérifier l'essai\")]", verification);
        Assert.Contains("AdventureSession.Load(", verification);   // le validateur du jeu, pas une copie
        int check = tools.IndexOf("EdsVerification.RunBase()", StringComparison.Ordinal), scene = tools.IndexOf("EdsStartupScene.Ensure(false)", StringComparison.Ordinal);
        int prepare = tools.IndexOf("EdsStyle.Prepare(false)", StringComparison.Ordinal), after = tools.IndexOf("EdsVerification.RunAfter(check)", StringComparison.Ordinal);
        Assert.True(check > 0 && check < scene, "la base est vérifiée avant tout le reste");
        Assert.True(after > prepare, "la suite est vérifiée une fois la préparation faite, en reprenant la base (une seule passe de chaque)");
        Assert.DoesNotContain("EdsVerification.Run()", tools);
        // Décision de Damien du 8 novembre 2026 : la fenêtre ne s'ouvre que s'il y a de l'orange ou du rouge.
        Assert.Contains("if (report.Overall != EssaiLevel.Ok) Open(report);", Script("Editor", "EdsVerificationWindow.cs"));
        Assert.Contains("✅ tout est prêt", verification);
        string ignore = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, ".gitignore"));
        Assert.Contains("unity/" + EssaiManifest.ReportPath + "\n", ignore.Replace("\r\n", "\n"));
        Assert.Contains("unity/" + EssaiManifest.ReportPath + ".meta\n", ignore.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Le_verificateur_n_ecrit_que_son_rapport()
    {
        // Relecture indépendante de la PR #118 : la vérification ne modifie rien dans le projet (ni import, ni fichier, ni actif),
        // sauf le rapport. Les appels d'écriture permis sont en liste blanche, mot pour mot.
        string[] allowed = { "File.WriteAllText(FullReportPath, text)", "AssetDatabase.ImportAsset(EssaiManifest.ReportPath)" };
        string[] writes =
        {
            "File.Write", "File.Append", "File.Delete", "File.Move", "File.Copy", "File.Create", "Directory.Create", "Directory.Delete", "Directory.Move",
            "CreateAsset", "SaveAssets", "SetDirty", "SaveAndReimport", "DeleteAsset", "MoveAsset", "CopyAsset", "ImportAsset", "ImportPackage",
            "CreateFolder", "ExtractTextures", "AddObjectToAsset", "SaveAsPrefabAsset", "Refresh(", "StartAssetEditing", "Legacy(", "EdsHumans.",
            "EdsStyle.Prepare", "EdsStartupScene.Ensure", "SaveScene", "OpenScene", "EditorBuildSettings.scenes =",
            "defaultRenderPipeline =", "renderPipeline =", ".animationType =", ".importAnimation =", ".clipAnimations =", "ApplyModifiedProperties",
            "Object.Destroy", "DestroyImmediate", "Instantiate(", "SessionState.Set", "isPlaying =",
        };
        foreach (string file in new[] { "EdsVerification.cs", "EdsVerificationWindow.cs" })
        {
            string code = string.Join("\n", Script("Editor", file).Split('\n').Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));
            foreach (string ok in allowed) code = code.Replace(ok, "");
            foreach (string w in writes) Assert.False(code.Contains(w, StringComparison.Ordinal), file + " écrit dans le projet : « " + w + " »");
        }
        // Les fonctions de la préparation qu'il emprunte ne font que lire.
        string style = Script("Editor", "EdsStyle.cs");
        int start = style.IndexOf("internal static string PackAnimalCandidate", StringComparison.Ordinal);
        string body = style.Substring(start, style.IndexOf("\n        }\n", start, StringComparison.Ordinal) - start);
        Assert.DoesNotContain("Legacy(", body);
        Assert.Contains("prefabs.Where(Renders)", body);
        Assert.Contains("EdsStyle.PackAnimalCandidate(", Script("Editor", "EdsVerification.cs"));
    }

    [Fact]
    public void Les_vrais_vetements_attendus_sont_reconnus_comme_par_la_preparation()
    {
        // Sous-lot V2.9.4 (pack Modular Character Outfits - Fantasy) : chaque pièce attendue est du dossier des vêtements, de sa famille
        // et à sa place ; les pièces d'une famille suffisent à rendre les vrais vêtements prêts (même règle que EdsHumans).
        foreach ((WardrobeFamily family, IReadOnlyList<string> parts) in EssaiManifest.OutfitFamilies)
        {
            Assert.Equal(EssaiManifest.OutfitBasicSlots.Count, parts.Count);
            var listing = parts.Select(p => (Path: p, Meshes: new[] { System.IO.Path.GetFileNameWithoutExtension(p) })).ToList();
            IReadOnlyList<WardrobePart> pieces = HumanWardrobe.Pieces(listing);
            for (int i = 0; i < parts.Count; i++)
            {
                Assert.True(HumanWardrobe.IsWardrobePath(parts[i]), parts[i]);
                Assert.Contains(pieces, x => x.Item.Family == family && x.Item.Slot == EssaiManifest.OutfitBasicSlots[i]);
            }
            Assert.True(EssaiManifest.OutfitReady(pieces.Select(x => x.Item.Slot)));
            Assert.False(EssaiManifest.OutfitReady(pieces.Select(x => x.Item.Slot).Where(x => x != WardrobeSlot.Feet)));
        }
        Assert.Equal(Enum.GetValues<WardrobeFamily>().Where(f => f == WardrobeFamily.Peasant || f == WardrobeFamily.Ranger), EssaiManifest.OutfitFamilies.Select(f => f.Family));
        Assert.All(EssaiManifest.OutfitTextures, t => Assert.True(HumanWardrobe.IsWardrobePath(t) && HumanWardrobe.IsColorMap(t), t));
        Assert.Equal(EssaiManifest.OutfitSkins[0], HumanWardrobe.SkinTexture(EssaiManifest.OutfitSkins, false, true));
        Assert.Equal(EssaiManifest.OutfitSkins[1], HumanWardrobe.SkinTexture(EssaiManifest.OutfitSkins, true, true));
        string humans = Script("Editor", "EdsHumans.cs");
        Assert.Contains("EssaiManifest.OutfitReady(pieces.Select(p => p.Slot))", humans);
        Assert.Contains("EssaiManifest.OutfitFolder", humans);
        Assert.DoesNotContain("new[] { WardrobeSlot.Body, WardrobeSlot.Arms, WardrobeSlot.Legs, WardrobeSlot.Feet }", humans);
        string verification = Script("Editor", "EdsVerification.cs");
        Assert.Contains("HumanWardrobe.Pieces(listing)", verification);
        Assert.Contains("EssaiManifest.OutfitReady(", verification);
    }
}
