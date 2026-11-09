using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdS.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Sous-lot V2.9.3 : menu « Éclats du Seuil → Vérifier l'essai ». Dit en un clic si tout est prêt : Unity et le rendu URP, les shaders du
    /// jeu, le paquet <c>com.eclats.core</c>, la scène de démarrage, les données JSON (chargées par le jeu lui-même), puis chaque ressource
    /// attendue (<see cref="EssaiManifest"/>, la liste que suit la préparation) : présente, absente, repli utilisé ou mal importée, avec le
    /// chemin où déposer le fichier. Rapport en fenêtre (<see cref="EdsVerificationWindow"/>), dans la Console et dans
    /// <see cref="EssaiManifest.ReportPath"/> (non suivi par git). Ne modifie rien dans le projet, sauf ce rapport (le test
    /// <c>V293VerificationTests</c> refuse tout autre appel d'écriture dans ce fichier).
    /// <para>Deux parties : la base (Unity, rendu, compilation, paquet, données, dossier des ressources : tout ce qui peut bloquer) et la
    /// suite (scène, catalogue, ressources : ce que la préparation peut changer). « Préparer et lancer l'essai » fait la base avant la
    /// préparation, puis seulement la suite après, en reprenant la base : une seule passe de chaque.</para>
    /// </summary>
    public static class EdsVerification
    {
        private const string Unity = "Unity et rendu";
        private const string Project = "Projet, scène et données";
        private const string Beasts = "Modèles des bêtes et du héros";
        private const string Humans = "Corps humains (villageois)";
        private const string Noises = "Bruitages";
        private const string Tunes = "Musiques";
        private const string Places = "Ambiances";
        private const string Images = "Textures (ciel, sols, bâtiments)";
        private const string Wave = "5e vague (V2.11 : monstres, armes, Seuil, fournil, fêtes)";
        private const string Later = "5e vague prête pour plus tard (crypte, créatures)";
        private const string Icons = "Icônes et licences hors dépôt (V2.11)";
        private const string Store = "Packs de l'Asset Store (V2.12 : fournil, étals, forge, village, interface, sons)";
        private const string StoreLater = "Packs de l'Asset Store prêts pour plus tard (château, crypte, armures, bâtiments)";
        private const string Prepare = "Lance le menu Éclats du Seuil → Préparer l'essai de style.";

        /// <summary>Assemblées du paquet <c>com.eclats.core</c> (dossier <c>src/</c>) dont le jeu a besoin.</summary>
        private static readonly string[] CoreAssemblies = { "EdS.Domain", "EdS.Simulation", "EdS.Data", "EdS.Data.Json", "EdS.Runtime", "EdS.Net", "EdS.World" };

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        [MenuItem("Éclats du Seuil/Vérifier l'essai")]
        private static void FromMenu() => EdsVerificationWindow.ShowIfNeeded(Run());

        /// <summary>Toute la vérification (base et suite), rapport écrit (fichier et Console).</summary>
        internal static EssaiReport Run() => Run(null, true);

        /// <summary>Seulement la base (ce qui peut bloquer), rapport écrit : le contrôle d'avant la préparation.</summary>
        internal static EssaiReport RunBase() => Run(null, false);

        /// <summary>Seulement la suite (scène, catalogue, ressources), après les lignes de <paramref name="basis"/> (une base déjà faite).</summary>
        internal static EssaiReport RunAfter(EssaiReport basis) => Run(basis, true);

        private static EssaiReport Run(EssaiReport basis, bool after)
        {
            var report = new EssaiReport();
            try
            {
                if (basis == null)
                {
                    Step("Unity et rendu", 0.05f);
                    CheckUnity(report);
                    Step("Projet et données", 0.15f);
                    CheckProject(report);
                }
                else foreach (EssaiLine line in basis.Lines) report.Add(line.Section, line.Level, line.Label, line.Detail, line.Fix);
                if (after)
                {
                    CheckScene(report);
                    if (AssetDatabase.IsValidFolder(EssaiManifest.Root)) CheckResources(report);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                report.Add(Project, EssaiLevel.Blocking, "Vérification", "erreur inattendue : " + e.Message, "Envoie une capture de la Console à Claude.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            Write(report);
            return report;
        }

        private static void Step(string what, float progress) => EditorUtility.DisplayProgressBar("Vérification de l'essai", what + "…", progress);

        // --- Unity, rendu, shaders -------------------------------------------------------------------------------------------------

        private static void CheckUnity(EssaiReport report)
        {
            string versionFile = Path.Combine(ProjectRoot, "ProjectSettings", "ProjectVersion.txt");
            string expected = File.Exists(versionFile) ? EssaiManifest.ExpectedVersion(File.ReadAllText(versionFile)) : null;
            string problem = EssaiManifest.VersionProblem(Application.unityVersion, expected);
            // Sous-lot V2.12.4 : classe de la machine (mémoire vidéo, mémoire) et limite des textures que le jeu appliquera (choix du joueur compris).
            report.Add(Unity, EssaiLevel.Ok, "Machine et textures",
                GraphicsTier.Describe(DeviceQuality.Vram, DeviceQuality.Ram, Application.isMobilePlatform, DeviceQuality.Limit));
            if (problem == null) report.Add(Unity, EssaiLevel.Ok, "Version de Unity", Application.unityVersion);
            else report.Add(Unity, EssaiLevel.Warning, "Version de Unity", problem, "Ouvre le projet avec Unity " + expected + " (Unity Hub → Installs).");

            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                report.Add(Unity, EssaiLevel.Blocking, "Rendu URP", "inactif : objets roses, ni échos ni failles",
                    "Lance le menu Éclats du Seuil → Configurer le rendu URP, puis relance la vérification.");
            }
            else
            {
                report.Add(Unity, EssaiLevel.Ok, "Rendu URP", "actif (" + urp.name + ")");
                if (urp.supportsCameraOpaqueTexture) report.Add(Unity, EssaiLevel.Ok, "Image opaque de la scène", "gardée (distorsion des failles, eau)");
                else report.Add(Unity, EssaiLevel.Warning, "Image opaque de la scène", "coupée : les failles ne tordent pas le décor", Prepare);
            }

            foreach (string name in EssaiManifest.GameShaders)
            {
                Shader shader = Shader.Find(name);
                string file = "unity/Assets/EdS/Game/" + name.Substring(name.IndexOf('/') + 1) + ".shader";
                string without = name == EssaiManifest.EchoShader ? "les échos gardent leur teinte pâle"
                    : name == EssaiManifest.RiftShader ? "les failles gardent leurs formes simples" : "repli du jeu";
                if (shader == null)
                    report.Add(Unity, EssaiLevel.Warning, "Shader " + name, "introuvable : " + without,
                        "Sélectionne " + file + " dans Unity, regarde l'erreur dans l'Inspector et envoie une capture à Claude.");
                else if (!shader.isSupported)
                    report.Add(Unity, EssaiLevel.Warning, "Shader " + name, "non pris en charge par cette carte ou ce rendu : " + without,
                        urp == null ? "Configure d'abord le rendu URP." : "Envoie une capture de l'Inspector du shader à Claude.");
                else report.Add(Unity, EssaiLevel.Ok, "Shader " + name, "pris en charge");
            }

            if (File.Exists(Path.Combine(ProjectRoot, EdsInterfaceSetup.SettingsAsset))) report.Add(Unity, EssaiLevel.Ok, "TextMeshPro", "ressources importées");
            else report.Add(Unity, EssaiLevel.Warning, "TextMeshPro", "ressources absentes : textes un peu flous",
                "Lance le menu Éclats du Seuil → Préparer l'interface (TextMeshPro).");
        }

        // --- Paquet, scène, données ------------------------------------------------------------------------------------------------

        private static void CheckProject(EssaiReport report)
        {
            if (EditorUtility.scriptCompilationFailed)
                report.Add(Project, EssaiLevel.Blocking, "Compilation des scripts", "en échec (erreurs rouges dans la Console)",
                    "Envoie une capture des erreurs rouges de la Console à Claude, puis relance la vérification.");
            else report.Add(Project, EssaiLevel.Ok, "Compilation des scripts", "sans erreur");

            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.eclats.core/package.json");
            var loaded = new HashSet<string>(AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name));
            List<string> missing = CoreAssemblies.Where(a => !loaded.Contains(a)).ToList();
            if (package == null)
                report.Add(Project, EssaiLevel.Blocking, "Paquet com.eclats.core", "introuvable (le cœur du jeu, dossier src/)",
                    "Remets le dossier unity à côté de src/ et data/ dans le dépôt, puis rouvre le projet.");
            else if (missing.Count > 0)
                report.Add(Project, EssaiLevel.Blocking, "Paquet com.eclats.core", "non compilé (manque " + string.Join(", ", missing) + ")",
                    "Envoie une capture des erreurs rouges de la Console à Claude.");
            else report.Add(Project, EssaiLevel.Ok, "Paquet com.eclats.core", "compilé (" + CoreAssemblies.Length + " assemblées)");

            CheckData(report);

            if (!AssetDatabase.IsValidFolder(EssaiManifest.Root))
                report.Add(Project, EssaiLevel.Blocking, "Dossier des ressources", EssaiManifest.Root + " introuvable",
                    "Dézippe les packs (ou ta sauvegarde Documents/EdS-Sauvegardes) dans unity/Assets/Style, puis relance.");
            else report.Add(Project, EssaiLevel.Ok, "Dossier des ressources", EssaiManifest.Root);
        }

        /// <summary>Scène de démarrage, réglages de build et catalogue (« Préparer et lancer l'essai » les fait : vérifiés après).</summary>
        private static void CheckScene(EssaiReport report)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EdsStartupScene.ScenePath) == null)
                report.Add(Project, EssaiLevel.Warning, "Scène de démarrage", "absente (" + EdsStartupScene.ScenePath + ")",
                    "Lance « Préparer et lancer l'essai » : il la crée (ou le menu Éclats du Seuil → Créer la scène de démarrage).");
            else report.Add(Project, EssaiLevel.Ok, "Scène de démarrage", EdsStartupScene.ScenePath);
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes ?? new EditorBuildSettingsScene[0];
            if (scenes.Length > 0 && scenes[0].path == EdsStartupScene.ScenePath && scenes[0].enabled)
                report.Add(Project, EssaiLevel.Ok, "Réglages de build", "la scène de démarrage est en tête (" + scenes.Length + " scène" + (scenes.Length > 1 ? "s" : "") + ")");
            else report.Add(Project, EssaiLevel.Warning, "Réglages de build", "la scène de démarrage n'est pas en tête de la liste (téléphone, jeu construit)",
                "Lance le menu Éclats du Seuil → Créer la scène de démarrage.");

            if (!AssetDatabase.IsValidFolder(EssaiManifest.Root)) return;
            if (AssetDatabase.LoadAssetAtPath<StyleCatalog>(EdsStyle.CatalogPath) == null)
                report.Add(Project, EssaiLevel.Warning, "Catalogue de l'essai", "pas encore fait (" + EdsStyle.CatalogPath + ")", Prepare);
            else report.Add(Project, EssaiLevel.Ok, "Catalogue de l'essai", EdsStyle.CatalogPath);
        }

        /// <summary>Les données JSON du dépôt, chargées comme le fait le jeu (validateur du jeu, aucune sauvegarde lue ni écrite).</summary>
        private static void CheckData(EssaiReport report)
        {
            string data = Path.Combine(RepoRoot, "data", "v0"), scenarios = Path.Combine(RepoRoot, "scenarios");
            if (!Directory.Exists(data) || !Directory.Exists(scenarios))
            {
                report.Add(Project, EssaiLevel.Blocking, "Données du jeu", "dossiers data/v0 ou scenarios introuvables à côté de unity",
                    "Remets le dossier unity dans le dépôt, à côté de data/ et scenarios/.");
                return;
            }
            Step("Données du jeu", 0.25f);
            string noSave = Path.Combine(Path.GetTempPath(), "eds-verification-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                AdventureSession adventure = AdventureSession.Load(data, scenarios, noSave);
                report.Add(Project, EssaiLevel.Ok, "Données du jeu (JSON)", "chargées et validées (zone " + adventure.CurrentZone + ")");
            }
            catch (Exception e)
            {
                report.Add(Project, EssaiLevel.Blocking, "Données du jeu (JSON)", "refusées : " + e.Message,
                    "Fais un Pull dans GitHub Desktop ; si le message reste, envoie-le à Claude.");
            }
        }

        // --- Ressources attendues --------------------------------------------------------------------------------------------------

        private static void CheckResources(EssaiReport report)
        {
            Step("Ressources de " + EssaiManifest.Root, 0.4f);
            List<string> models = StyleRules.Highest(Find("t:Model", EssaiManifest.Root)).ToList();
            List<string> textures = StyleRules.Highest(Find("t:Texture2D", EssaiManifest.Root).Where(p => !p.StartsWith(EdsStyle.Generated + "/"))).ToList();
            List<string> sounds = Find("t:AudioClip", EssaiManifest.Root).ToList();
            List<string> prefabs = Find("t:Prefab", "Assets").ToList();

            for (int i = 0; i < EssaiManifest.Models.Count; i++)
            {
                EssaiModel model = EssaiManifest.Models[i];
                Step("Modèles : " + model.Label, 0.4f + 0.3f * i / EssaiManifest.Models.Count);
                // Le même filtre que la préparation (EdsStyle.PackAnimalCandidate) : un prefab d'un autre rendu qu'URP, ou sans modèle animé,
                // n'est pas pris ; il est signalé, et c'est le fichier suivant qui compte.
                string pack = null, refused = null;
                if (model.PackFirst)
                {
                    string candidate = EdsStyle.PackAnimalCandidate(prefabs, model.Slot, out string animated);
                    if (candidate != null && animated != null) pack = candidate;
                    else if (candidate != null) refused = EssaiManifest.FileName(candidate) + " (aucun modèle animé derrière lui)";
                    else if (StyleRules.PackAnimal(prefabs, model.Slot) is string other) refused = EssaiManifest.FileName(other) + " (matériaux hors URP : il sortirait rose)";
                }
                ModelPick pick = EssaiManifest.Pick(model, models, pack);
                (EssaiLevel level, string detail, string fix) = EssaiManifest.Describe(model, pick);
                if (refused != null)
                {
                    level = EssaiLevel.Warning;
                    detail = "prefab de l'Asset Store non pris : " + refused + " ; " + detail;
                    if (fix.Length == 0) fix = "Importe la variante URP du pack (dossier « URP »), puis " + Lower(Prepare);
                }
                if (pick.Path != null && pick.Path != pack)
                {
                    string import = ModelImport(EssaiManifest.ClipSources(model.Slot, pick.Path, models), pick.Rank < 0 || model.Animated[pick.Rank],
                        out string importFix, out int clips);
                    if (import != null)
                    {
                        level = EssaiLevel.Warning;
                        detail += " ; " + import;
                        fix = importFix;
                    }
                    else if (clips > 0) detail += " (" + clips + " animations)";
                }
                else if (pick.Path != null) detail = "loup ou sanglier réaliste de l'Asset Store : " + EssaiManifest.FileName(pick.Path);
                report.Add(Beasts, level, model.Label, detail, fix);
            }

            Step("Corps humains", 0.75f);
            CheckHumans(report, models, textures);

            Step("Vrais vêtements", 0.8f);
            CheckOutfits(report, models, textures);

            Step("Sons et musiques", 0.85f);
            // Comme la préparation : les fichiers dans l'ordre, le premier qui se charge comme un son.
            // Sous-lot V2.12 : comme la préparation, les sons médiévaux de Basic RPG Sounds en repli.
            List<string> storeSounds = AssetStore.MedievalSounds(Find("t:AudioClip", "Assets")).ToList();
            foreach (EssaiAudio audio in EssaiManifest.Sounds) AddAudio(report, Noises, audio, AssetStore.PickSounds(sounds, storeSounds, audio.Sound.Value, 6));
            foreach (EssaiAudio audio in EssaiManifest.Music) AddAudio(report, Tunes, audio, StyleRules.MusicFiles(sounds, audio.Music.Value));
            foreach (EssaiAudio audio in EssaiManifest.Ambiences) AddAudio(report, Places, audio, StyleRules.AmbienceFiles(sounds, audio.Ambience.Value));

            Step("5e vague", 0.9f);
            CheckWave5(report, models);

            Step("Packs de l'Asset Store", 0.92f);
            CheckAssetStore(report, storeSounds);

            Step("Textures", 0.95f);
            CheckTextures(report, textures);
        }

        /// <summary>
        /// Sous-lot V2.11 : les ressources de la 5e vague (liste partagée <see cref="Wave5"/>, mêmes choix que <c>EdsWave5</c>), les gestes de
        /// métier de Mixamo, le pack d'icônes de CraftPix et le manifeste des licences hors dépôt.
        /// </summary>
        private static void CheckWave5(EssaiReport report, List<string> models)
        {
            foreach (Wave5Entry entry in Wave5.Entries)
            {
                string found = Wave5.Pick(entry, models, out int rank);
                (EssaiLevel level, string detail, string fix) = Wave5.Describe(entry, found, rank);
                if (found != null && entry.Has(Wave5Fix.NotOnPhone)) detail += " ; jamais sur téléphone (Note8) ni sur PC modeste";
                else if (found != null && entry.Has(Wave5Fix.Heavy)) detail += " ; lourd pour le Note8 (plus de 20 000 triangles)";
                if (found != null && entry.Has(Wave5Fix.Animated) && AssetImporter.GetAtPath(found) is ModelImporter importer
                    && importer.animationType != ModelImporterAnimationType.Legacy)
                {
                    level = EssaiLevel.Warning;
                    detail += " ; pas encore préparé (animations « " + importer.animationType + " »)";
                    fix = Prepare;
                }
                report.Add(entry.Use == Wave5Use.Ready ? Later : Wave, level, entry.Label, detail, fix);
            }
            foreach ((string word, string clip, string use) in Wave5.TradeClips)
            {
                string file = Wave5.TradeClip(word, models);
                if (file != null) report.Add(Wave, EssaiLevel.Ok, "Geste de métier : " + use, EssaiManifest.FileName(file));
                else report.Add(Wave, EssaiLevel.Warning, "Geste de métier : " + use, "absent : les gestes de Quaternius",
                    "Dépose dans " + Wave5.TradeFolder + " un FBX de Mixamo dont le nom contient « " + word + " » (FBX for Unity, Without Skin).");
            }
            List<string> icons = Find("t:Texture2D", "Assets").Where(ItemIcons.IsIcon).ToList();
            if (icons.Count > 0) report.Add(Icons, EssaiLevel.Ok, "Icônes de CraftPix", icons.Count + " images du pack trouvées (objets reliés par la préparation)");
            else report.Add(Icons, EssaiLevel.Warning, "Icônes de CraftPix", "pack introuvable : les icônes du jeu restent",
                "Importe « 6200 Fantasy RPG Icons Pack » (Package Manager → My Assets), dans un dossier dont le nom contient « Fantasy RPG Icons » ou « CraftPix ».");
            string manifest = Path.Combine(ProjectRoot, ItemIcons.LicenseManifest);
            if (File.Exists(manifest)) report.Add(Icons, EssaiLevel.Ok, "Manifeste des licences (Asset Store, Fab)", ItemIcons.LicenseManifest + " (hors dépôt)");
            else report.Add(Icons, EssaiLevel.Warning, "Manifeste des licences (Asset Store, Fab)", "pas encore écrit", Prepare);
        }

        /// <summary>
        /// Sous-lot V2.12 : les packs de l'Asset Store (liste partagée <see cref="AssetStore"/>, mêmes choix que <c>EdsWave5</c>) : prefabs
        /// reconnus par la fin de leur chemin, sons médiévaux gardés, planches d'interface, icônes sans fond.
        /// </summary>
        private static void CheckAssetStore(EssaiReport report, List<string> storeSounds)
        {
            List<string> prefabs = Find("t:Prefab", "Assets").ToList();
            foreach (Wave5Entry entry in AssetStore.Entries)
            {
                IReadOnlyList<string> found = AssetStore.Found(entry, prefabs);
                (EssaiLevel level, string detail, string fix) = AssetStore.Describe(entry, found, false);
                report.Add(entry.Use == Wave5Use.Ready ? StoreLater : Store, level, entry.Label, detail, fix);
            }
            List<string> all = Find("t:AudioClip", "Assets").Where(AssetStore.IsStoreSound).ToList();
            if (all.Count > 0) report.Add(Store, EssaiLevel.Ok, "Sons de Basic RPG Sounds", storeSounds.Count + " gardés sur " + all.Count + " (arcade et modernes écartés), en repli des sons choisis");
            else report.Add(Store, EssaiLevel.Ok, "Sons de Basic RPG Sounds", "absent : les sons choisis restent");
            List<string> gui = Find("t:Texture2D", "Assets").Where(AssetStore.IsGuiPath).ToList();
            string stone = AssetStore.Board(gui, AssetStore.GuiBoard.Stone);
            report.Add(Store, stone != null || gui.Count == 0 ? EssaiLevel.Ok : EssaiLevel.Warning, "Interface en bois (fond des fenêtres)",
                stone != null ? "planche de pierre : " + EssaiManifest.FileName(stone) : gui.Count == 0 ? "absent : la dalle du jeu reste" : "planche « UI board … stone » introuvable : la dalle du jeu reste",
                stone != null || gui.Count == 0 ? "" : "Vérifie le dossier " + AssetStore.Gui.Folder + ".");
            List<string> icons = AssetStore.PreferNoBackground(Find("t:Texture2D", "Assets").Where(ItemIcons.IsIcon)).ToList();
            if (icons.Count > 0)
            {
                string items = Path.Combine(ProjectRoot, "..", "data", "v0", "world", "items.json");
                List<string> ids = File.Exists(items)
                    ? System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(items), "\"id\"\\s*:\\s*\"(item\\.[a-z0-9_]+)\"").Cast<System.Text.RegularExpressions.Match>()
                        .Select(m => m.Groups[1].Value).Distinct().ToList()
                    : new List<string>();
                int mapped = ItemIcons.Map(ids, icons).Count;
                report.Add(Icons, EssaiLevel.Ok, "Objets reliés à une icône de CraftPix", mapped + " sur " + ids.Count + " (" + icons.Count + " images, version sans fond d'abord)");
            }
        }

        /// <summary>
        /// Contrôle d'import d'un modèle choisi (<paramref name="sources"/>[0]) et des fichiers où la préparation prend ses animations
        /// (<see cref="EssaiManifest.ClipSources"/> : pour le héros, ses compagnons de Mixamo) : null si tout va bien, sinon le constat (et
        /// <paramref name="fix"/>). Réimporter n'est conseillé que si ni le fichier ni ses compagnons n'ont d'animation.
        /// </summary>
        private static string ModelImport(IReadOnlyList<string> sources, bool animated, out string fix, out int clipCount)
        {
            fix = "";
            clipCount = 0;
            string path = sources[0];
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            {
                fix = "Clic droit sur le fichier → Reimport ; s'il reste illisible, redemande le fichier à Claude.";
                return "mal importé (Unity ne le lit pas comme un modèle)";
            }
            List<string> unprepared = sources.Where(p => AssetImporter.GetAtPath(p) is ModelImporter m && m.animationType != ModelImporterAnimationType.Legacy).ToList();
            if (unprepared.Count > 0)
            {
                fix = Prepare;
                return "pas encore préparé (animations « " + importer.animationType + " » au lieu de « Legacy »"
                    + (sources.Count > 1 ? ", " + unprepared.Count + " fichier" + (unprepared.Count > 1 ? "s" : "") + " sur " + sources.Count : "") + ")";
            }
            clipCount = sources.Sum(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().Count(c => !c.name.StartsWith("__preview__")));
            if (animated && clipCount == 0)
            {
                fix = "Clic droit sur le fichier → Reimport, puis " + Lower(Prepare);
                return sources.Count > 1 ? "aucune animation, ni dans le fichier ni dans les " + (sources.Count - 1) + " fichiers à côté de lui" : "aucune animation dans le fichier";
            }
            string scale = EssaiManifest.ScaleProblem(Height(AssetDatabase.LoadAssetAtPath<GameObject>(path)));
            if (scale != null)
            {
                fix = "Sélectionne le fichier → onglet Model → Scale Factor 1 (ou « Convert Units »), Apply ; sinon envoie une capture à Claude.";
                return scale;
            }
            return null;
        }

        /// <summary>Plus grande dimension (m) des maillages d'un modèle importé, sans l'instancier dans la scène.</summary>
        private static double Height(GameObject model)
        {
            if (model == null) return 0;
            double best = 0;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
                best = Math.Max(best, Size(filter.sharedMesh, filter.transform));
            foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                best = Math.Max(best, Size(skin.sharedMesh, skin.transform));
            return best;
        }

        private static double Size(Mesh mesh, Transform at)
        {
            if (mesh == null || at == null) return 0;
            Vector3 size = mesh.bounds.size, scale = at.lossyScale;
            double s = Math.Max(Math.Abs(scale.x), Math.Max(Math.Abs(scale.y), Math.Abs(scale.z)));
            return Math.Max(size.x, Math.Max(size.y, size.z)) * s;
        }

        private static void CheckHumans(EssaiReport report, List<string> models, List<string> textures)
        {
            const string without = "absent : les villageois gardent leurs modèles d'avant";
            foreach ((string expected, bool female) in new[] { (EssaiManifest.MaleBody, false), (EssaiManifest.FemaleBody, true) })
            {
                string label = female ? "Corps de femme" : "Corps d'homme";
                string body = models.FirstOrDefault(p => HumanLooks.BodyOf(p) == female);
                if (body == null) report.Add(Humans, EssaiLevel.Warning, label, without, "Dépose " + EssaiManifest.FileName(expected) + " dans " + EssaiManifest.HumanFolder + " (un sous-dossier convient).");
                else AddHumanoid(report, label, body, false);
            }
            var clipNames = new HashSet<string>();
            foreach (string expected in EssaiManifest.AnimationLibraries)
            {
                string label = "Animations " + Path.GetFileNameWithoutExtension(expected);
                string library = models.FirstOrDefault(p => HumanLooks.IsAnimationLibrary(p)
                    && string.Equals(EssaiManifest.FileName(p), EssaiManifest.FileName(expected), StringComparison.OrdinalIgnoreCase));
                if (library == null)
                {
                    report.Add(Humans, EssaiLevel.Warning, label, "absente : moins d'animations", "Dépose " + EssaiManifest.FileName(expected) + " dans "
                        + EssaiManifest.Folder(expected) + ".");
                    continue;
                }
                AddHumanoid(report, label, library, true);
                foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(library).OfType<AnimationClip>())
                    if (!clip.name.StartsWith("__preview__")) clipNames.Add(clip.name);
            }
            // Revue Codex de la PR #118 (tour 2) : toujours vérifié (une bibliothèque sans animation importée laisse les corps non prêts).
            {
                IReadOnlyList<string> required = EssaiManifest.MissingHumanClips(clipNames);
                List<string> others = HumanLooks.CanonicalNames().Where(c => !clipNames.Contains(c) && !required.Contains(c)).ToList();
                if (required.Count > 0)
                    report.Add(Humans, EssaiLevel.Warning, "Animations des villageois", "manquent " + string.Join(", ", required) + " : corps humains non prêts",
                        "Vérifie que UAL1_Standard.fbx et UAL2_Standard.fbx sont là et que leur onglet Animation a « Import Animation » coché, puis " + Lower(Prepare));
                else if (others.Count > 0)
                    report.Add(Humans, EssaiLevel.Warning, "Animations des villageois", "manquent " + string.Join(", ", others) + " (un geste proche les remplace)",
                        "Vérifie que UAL1_Standard.fbx et UAL2_Standard.fbx sont là, puis " + Lower(Prepare));
                else report.Add(Humans, EssaiLevel.Ok, "Animations des villageois", "toutes là (" + HumanLooks.CanonicalNames().Count + ")");
            }
            string[] skinLabels = { "Peau claire (homme)", "Peau sombre (homme)", "Peau claire (femme)", "Peau sombre (femme)" };
            for (int i = 0; i < EssaiManifest.SkinTextures.Count; i++)
            {
                string found = HumanLooks.SkinTexture(textures, i >= 2, i % 2 == 1);
                if (found != null) report.Add(Humans, EssaiLevel.Ok, skinLabels[i], EssaiManifest.FileName(found));
                else report.Add(Humans, EssaiLevel.Warning, skinLabels[i], i % 2 == 0 ? without : "absente : peaux sombres plus pâles",
                    "Dépose " + EssaiManifest.FileName(EssaiManifest.SkinTextures[i]) + " dans " + EssaiManifest.Folder(EssaiManifest.SkinTextures[i]) + ".");
            }
            foreach (string expected in EssaiManifest.Hairs)
            {
                var want = HumanLooks.HairOf(expected);
                string found = models.FirstOrDefault(p => HumanLooks.HairOf(p) is var h && h != null && want != null && h.Value.Hair == want.Value.Hair && h.Value.Beard == want.Value.Beard);
                string label = "Coiffure " + Path.GetFileNameWithoutExtension(expected).Replace("Hair_", "");
                if (found != null) report.Add(Humans, EssaiLevel.Ok, label, EssaiManifest.FileName(found));
                else report.Add(Humans, EssaiLevel.Warning, label, "absente : une autre coiffure la remplace",
                    "Garde le dossier « " + EssaiManifest.HairFolder.Substring(EssaiManifest.HumanFolder.Length + 1) + " » dans " + EssaiManifest.HumanFolder + ".");
            }
        }

        /// <summary>
        /// Sous-lot V2.9.4 : vrais vêtements (pack Modular Character Outfits - Fantasy). Mêmes règles que la préparation (EdsHumans.Clothes) :
        /// fichiers du dossier des vêtements (<see cref="HumanWardrobe.IsWardrobePath"/>), pièces d'après les noms de leurs maillages
        /// (<see cref="HumanWardrobe.Pieces"/>), prêts si les places de base sont couvertes (<see cref="EssaiManifest.OutfitReady"/>) ; sinon les
        /// tenues peintes (repli). Rien n'est importé ni modifié.
        /// </summary>
        private static void CheckOutfits(EssaiReport report, List<string> models, List<string> textures)
        {
            const string label = "Vrais vêtements", painted = "absents : tenues peintes sur la peau";
            string deposit = "Copie « Exports/FBX (Unity) » et « Textures » du pack Modular Character Outfits - Fantasy (Quaternius) dans " + EssaiManifest.OutfitFolder + ".";
            List<string> files = models.Where(HumanWardrobe.IsWardrobePath).Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal).ToList();
            if (files.Count == 0)
            {
                report.Add(Humans, EssaiLevel.Warning, label, painted, deposit);
                return;
            }
            var listing = new List<(string Path, string[] Meshes)>();
            var unprepared = new List<string>();
            foreach (string file in files)
            {
                if (AssetImporter.GetAtPath(file) is ModelImporter importer && (importer.animationType != ModelImporterAnimationType.Generic || importer.importAnimation))
                    unprepared.Add(EssaiManifest.FileName(file));
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(file);
                SkinnedMeshRenderer[] skins = prefab != null ? prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true) : new SkinnedMeshRenderer[0];
                listing.Add((file, skins.Select(k => k.sharedMesh != null ? k.gameObject.name : null).ToArray()));
            }
            IReadOnlyList<WardrobePart> pieces = HumanWardrobe.Pieces(listing);
            if (!EssaiManifest.OutfitReady(pieces.Select(p => p.Item.Slot)))
            {
                List<WardrobeSlot> lacking = EssaiManifest.OutfitBasicSlots.Where(s => pieces.All(p => p.Item.Slot != s)).ToList();
                report.Add(Humans, EssaiLevel.Warning, label, pieces.Count + " pièces, il manque : " + string.Join(", ", lacking) + " : tenues peintes", deposit);
            }
            else if (unprepared.Count > 0)
                report.Add(Humans, EssaiLevel.Warning, label, "pas encore préparés (" + unprepared.Count + " fichiers pas en « Generic » sans animation)", Prepare);
            else report.Add(Humans, EssaiLevel.Ok, label, "prêts (" + pieces.Count + " pièces)");
            foreach ((WardrobeFamily family, IReadOnlyList<string> parts) in EssaiManifest.OutfitFamilies)
            {
                List<string> missing = parts.Where((p, i) => pieces.All(x => x.Item.Family != family || x.Item.Slot != EssaiManifest.OutfitBasicSlots[i])).ToList();
                if (missing.Count == 0) report.Add(Humans, EssaiLevel.Ok, "Vêtements " + family, "haut, bras, bas et pieds");
                else report.Add(Humans, EssaiLevel.Warning, "Vêtements " + family, "incomplets : une autre famille complète la tenue",
                    "Dépose " + string.Join(", ", missing.Select(EssaiManifest.FileName)) + " dans " + EssaiManifest.Folder(missing[0]) + ".");
            }
            List<string> fabrics = textures.Where(t => HumanWardrobe.IsWardrobePath(t) && HumanWardrobe.IsColorMap(t)).Select(EssaiManifest.FileName).ToList();
            foreach (string expected in EssaiManifest.OutfitTextures)
            {
                string name = EssaiManifest.FileName(expected);
                if (fabrics.Any(f => string.Equals(f, name, StringComparison.OrdinalIgnoreCase))) report.Add(Humans, EssaiLevel.Ok, "Étoffe " + name, "présente");
                else report.Add(Humans, EssaiLevel.Warning, "Étoffe " + name, "absente : pièces sans leur texture", "Dépose " + name + " dans " + EssaiManifest.Folder(expected) + ".");
            }
            for (int i = 0; i < EssaiManifest.OutfitSkins.Count; i++)
            {
                string found = HumanWardrobe.SkinTexture(textures, i == 1, true);
                string who = i == 1 ? "Peau des mains (femme)" : "Peau des mains (homme)";
                if (found != null) report.Add(Humans, EssaiLevel.Ok, who, EssaiManifest.FileName(found));
                else report.Add(Humans, EssaiLevel.Warning, who, "absente : mains sans leur peau", "Dépose " + EssaiManifest.FileName(EssaiManifest.OutfitSkins[i])
                    + " dans " + EssaiManifest.Folder(EssaiManifest.OutfitSkins[i]) + ".");
            }
        }

        /// <summary>Corps ou bibliothèque : import Humanoid et avatar valable. Vrai si tout va bien.</summary>
        private static bool AddHumanoid(EssaiReport report, string label, string path, bool library)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            {
                report.Add(Humans, EssaiLevel.Warning, label, "mal importé : " + EssaiManifest.FileName(path), "Clic droit sur le fichier → Reimport, puis relance la vérification.");
                return false;
            }
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                report.Add(Humans, EssaiLevel.Warning, label, "pas encore préparé (rig « " + importer.animationType + " »)", Prepare);
                return false;
            }
            bool human = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().Any(a => a != null && a.isValid && a.isHuman);
            if (!human)
            {
                report.Add(Humans, EssaiLevel.Warning, label, "avatar Humanoid invalide : Unity n'a pas reconnu le squelette",
                    "Sélectionne " + EssaiManifest.FileName(path) + " → onglet Rig → Humanoid → Configure, Apply, puis " + Lower(Prepare));
                return false;
            }
            int clips = library ? AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Count(c => !c.name.StartsWith("__preview__")) : 0;
            // Revue Codex de la PR #118 (tour 2) : avatar valable mais aucune animation importée (« Import Animation » décoché) : pas prêt.
            if (library && (!importer.importAnimation || clips == 0))
            {
                report.Add(Humans, EssaiLevel.Warning, label, "avatar Humanoid valable, mais aucune animation importée" + (importer.importAnimation ? "" : " (« Import Animation » décoché)"),
                    "Sélectionne " + EssaiManifest.FileName(path) + " → onglet Animation → coche « Import Animation », Apply, puis " + Lower(Prepare));
                return false;
            }
            report.Add(Humans, EssaiLevel.Ok, label, EssaiManifest.FileName(path) + ", avatar Humanoid valable" + (library ? ", " + clips + " animations" : ""));
            return true;
        }

        private static void AddAudio(EssaiReport report, string section, EssaiAudio audio, IReadOnlyList<string> files)
        {
            string found = files.FirstOrDefault(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p) != null);
            (EssaiLevel level, string detail, string fix) = EssaiManifest.Describe(audio, found);
            List<string> broken = files.TakeWhile(p => p != found).Select(EssaiManifest.FileName).ToList();
            if (broken.Count > 0)
            {
                level = EssaiLevel.Warning;
                detail = (found != null ? detail + " ; " : "") + "mal importé" + (broken.Count > 1 ? "s" : "") + " (Unity ne le lit pas comme un son) : " + string.Join(", ", broken);
                fix = "Clic droit sur " + broken[0] + " → Reimport ; sinon reconvertis-le en .ogg.";
            }
            report.Add(section, level, audio.Label, detail, fix);
        }

        private static string Lower(string sentence) => sentence.Substring(0, 1).ToLowerInvariant() + sentence.Substring(1);

        private static void CheckTextures(EssaiReport report, List<string> textures)
        {
            string sky = StyleRules.Sky(textures);
            if (sky != null) report.Add(Images, EssaiLevel.Ok, "Ciel", EssaiManifest.FileName(sky));
            else report.Add(Images, EssaiLevel.Warning, "Ciel", "absent : ciel de Unity", "Dépose un ciel de Poly Haven (« …puresky… ») dans " + EssaiManifest.SkyFolder + ".");
            List<string> layers = Find("t:TerrainLayer", "Assets").ToList();
            foreach ((StyleGround ground, string label) in new[] { (StyleGround.Grass, "Sol : herbe"), (StyleGround.Soil, "Sol : terre"), (StyleGround.Rock, "Sol : roche") })
            {
                string found = StyleRules.SampleGround(layers, ground) ?? StyleRules.GroundTexture(textures, ground, false);
                if (found != null) report.Add(Images, EssaiLevel.Ok, label, EssaiManifest.FileName(found));
                else report.Add(Images, EssaiLevel.Warning, label, "absent : couleur unie", "Dépose une texture de sol de Poly Haven dans " + EssaiManifest.GroundFolder + ".");
            }
            foreach ((StyleGround ground, string label, string word) in new[] { (StyleGround.Road, "Chemins (terre battue)", "chemin"), (StyleGround.Field, "Champs labourés", "champ") })
            {
                string found = StyleRules.GroundTexture(textures, ground, false);
                if (found != null) report.Add(Images, EssaiLevel.Ok, label, EssaiManifest.FileName(found));
                else report.Add(Images, EssaiLevel.Warning, label, "absent : couleur unie",
                    "Dépose dans " + EssaiManifest.RoadFolder + " une texture dont le nom commence par « " + word + " ».");
            }
            int surfaces = new[] { StyleSurface.Stone, StyleSurface.Plaster, StyleSurface.Roof, StyleSurface.Wood, StyleSurface.Cobble, StyleSurface.Bark }
                .Count(s => StyleRules.SurfaceTexture(textures, s, false) != null);
            if (surfaces == 6) report.Add(Images, EssaiLevel.Ok, "Matières des bâtiments", "6 sur 6");
            else report.Add(Images, EssaiLevel.Warning, "Matières des bâtiments", surfaces + " sur 6 : les autres en couleur unie",
                "Dépose les textures de Poly Haven (pierre, enduit, tuiles, planches, pavés, écorce) dans " + EssaiManifest.SurfaceFolder + ".");
        }

        private static IEnumerable<string> Find(string filter, string folder) =>
            (AssetDatabase.FindAssets(filter, new[] { folder }) ?? new string[0]).Select(AssetDatabase.GUIDToAssetPath).Distinct();

        // --- Rapport ----------------------------------------------------------------------------------------------------------------

        private static void Write(EssaiReport report)
        {
            string text = report.ToMarkdown(DateTime.Now, Application.unityVersion);
            try
            {
                File.WriteAllText(FullReportPath, text);
                AssetDatabase.ImportAsset(EssaiManifest.ReportPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Éclats du Seuil : rapport non écrit (" + EssaiManifest.ReportPath + ") : " + e.Message);
            }
            // Décision de Damien du 8 novembre 2026 : tout vert = une ligne dans la Console (la fenêtre ne s'ouvre pas) ; le fichier est écrit quand même.
            if (report.Overall == EssaiLevel.Ok) Debug.Log("Vérification de l'essai : ✅ tout est prêt (" + EssaiManifest.ReportPath + ").");
            else
            {
                string summary = "Vérification de l'essai\n" + report.Summary() + "\nRapport complet : " + EssaiManifest.ReportPath;
                if (report.Overall == EssaiLevel.Blocking) Debug.LogWarning(summary);
                else Debug.Log(summary);
            }
        }

        internal static string FullReportPath => Path.Combine(ProjectRoot, EssaiManifest.ReportPath);
    }
}
