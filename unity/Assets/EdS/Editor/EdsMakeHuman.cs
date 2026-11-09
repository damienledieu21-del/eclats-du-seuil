using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdS.Runtime;
using UnityEditor;
using UnityEngine;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Sous-lot V2.16 (souhait de Damien du 9 novembre 2026 : « designer des choses nous-mêmes comme les personnages ») : importe les
    /// personnages que Damien fait dans MakeHuman. Chaque dossier <c>Assets/Style/MakeHuman/&lt;nom&gt;/</c> (un FBX et ses textures) devient :
    /// un modèle Humanoid (le squelette « Game engine » de MakeHuman, compatible avec le retargeting des animations de Quaternius), des
    /// matières URP Lit faites de ses textures (cheveux, sourcils et cils découpés), des textures plafonnées pour le PC modeste (1024 ; 512 sur
    /// téléphone), un prefab dans <c>Assets/Style/Generated/MakeHuman</c>, et une ligne du catalogue. Le fichier <c>PNJ.txt</c> dit qui porte
    /// quel personnage (« gaspard = Gaspard.fbx » ; règles : <see cref="MadeHumans"/>). Relançable sans risque, comme la préparation de l'essai.
    /// Les animations sont celles des Universal Animation Library, que « Préparer l'essai de style » a déjà préparées.
    /// </summary>
    public static class EdsMakeHuman
    {
        public const string Root = EdsStyle.Root + "/" + MadeHumans.Folder;
        public const string MappingPath = Root + "/" + MadeHumans.MappingFile;
        private const string PrefabFolder = EdsStyle.Generated + "/" + MadeHumans.Folder;

        /// <summary>Modèle de PNJ.txt écrit au premier import (rien n'est écrasé ensuite).</summary>
        private const string MappingTemplate =
            "# Qui porte quel personnage fait dans MakeHuman ?\n"
            + "# Une ligne par PNJ : identifiant = fichier ou dossier du personnage.\n"
            + "# Exemples (retire le # pour les activer) :\n"
            + "# gaspard = Gaspard.fbx\n"
            + "# npc.odile = Odile\n"
            + "# heros = Aldric/Aldric.fbx\n"
            + "# Un dossier qui porte le prénom d'un PNJ (Assets/Style/MakeHuman/Gaspard) est pris sans ligne ici.\n";

        [MenuItem("Éclats du Seuil/Importer les personnages MakeHuman")]
        private static void ImportFromMenu()
        {
            if (!AssetDatabase.IsValidFolder(EdsStyle.Root))
            {
                EditorUtility.DisplayDialog("Personnages MakeHuman", "Dossier « " + EdsStyle.Root + " » introuvable : ouvre d'abord le projet Unity du jeu (dossier unity).", "OK");
                return;
            }
            if (!AssetDatabase.IsValidFolder(EdsStyle.CatalogFolder)) AssetDatabase.CreateFolder(EdsStyle.Root, "Resources");
            StyleCatalog catalog = AssetDatabase.LoadAssetAtPath<StyleCatalog>(EdsStyle.CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<StyleCatalog>();
                AssetDatabase.CreateAsset(catalog, EdsStyle.CatalogPath);
            }
            EditorUtility.SetDirty(catalog);
            var notes = new List<string>();
            string report = Prepare(catalog, notes);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("Personnages MakeHuman :\n" + report);
            EditorUtility.DisplayDialog("Personnages MakeHuman", report, "OK");
        }

        /// <summary>
        /// Importe les personnages de <c>Assets/Style/MakeHuman</c> dans <paramref name="catalog"/> (aussi appelé par « Préparer l'essai de style ») ;
        /// rend le rapport (une ligne par personnage, puis les remarques de <paramref name="notes"/>).
        /// </summary>
        internal static string Prepare(StyleCatalog catalog, List<string> notes)
        {
            if (!AssetDatabase.IsValidFolder(Root))
            {
                // Pas de dossier : rien à faire, et les personnages déjà importés restent (Damien peut avoir déplacé son dossier un instant).
                return "Personnages MakeHuman : aucun dossier " + Root + " (guide : docs/CREER_PERSONNAGES.md).";
            }
            EnsureTemplate();
            List<string> paths = AssetDatabase.FindAssets("t:Model", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();
            IReadOnlyList<MadeHumanModel> models = MadeHumans.Models(paths);
            var done = new List<MadeHumanLook>();
            var lines = new List<string>();
            bool modest = true;   // choix de Damien du 9 novembre 2026 : PC modeste (4 Go, GTX 470) ; un PC plus fort peut passer MadeHumans.StandardTextureCap
            foreach (MadeHumanModel model in models)
            {
                string problem = null;
                MadeHumanLook look = Import(model, catalog, modest, notes, out problem);
                if (look == null)
                {
                    lines.Add("  - " + model.Folder + " : refusé (" + problem + ")");
                    continue;
                }
                done.Add(look);
                lines.Add("  - " + model.Folder + " : prêt" + (problem != null ? " (" + problem + ")" : ""));
            }
            catalog.MadeLooks = done.ToArray();
            string text = AssetDatabase.LoadAssetAtPath<TextAsset>(MappingPath) is TextAsset asset ? asset.text : "";
            catalog.MadeMappingText = text;
            MadeHumanMapping mapping = MadeHumans.ParseMapping(text);
            var report = new List<string> { "Personnages MakeHuman : " + done.Count + " prêt(s) sur " + models.Count + " trouvé(s)." };
            report.AddRange(lines);
            foreach (MappingProblem p in mapping.Problems) report.Add("  ! " + p);
            foreach (string missing in MadeHumans.Missing(mapping, catalog.MadeModels)) report.Add("  ! PNJ.txt vise un personnage absent : " + missing);
            if (catalog.Humans == null || catalog.Humans.Clips == null || catalog.Humans.Clips.Length == 0)
                report.Add("  ! Pas d'animations humanoïdes : lance « Éclats du Seuil → Préparer l'essai de style » (animations de Quaternius), puis relance cet import.");
            if (done.Count > 0 && mapping.Entries.Count == 0)
                report.Add("  Astuce : sans ligne dans PNJ.txt, un dossier au prénom d'un PNJ (« Gaspard ») est pris tout seul ; le héros se choisit dans « Mon personnage… » (pause).");
            return string.Join("\n", report);
        }

        private static void EnsureTemplate()
        {
            string disk = Path.Combine(Directory.GetCurrentDirectory(), MappingPath);
            if (File.Exists(disk)) return;
            File.WriteAllText(disk, MappingTemplate, new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(MappingPath);
        }

        /// <summary>Prépare un personnage et rend sa ligne de catalogue ; null (avec le motif) s'il est refusé. <paramref name="problem"/> : une remarque s'il est accepté.</summary>
        private static MadeHumanLook Import(MadeHumanModel model, StyleCatalog catalog, bool modest, List<string> notes, out string problem)
        {
            problem = null;
            string path = model.Path;
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) { problem = "fichier illisible"; return null; }
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            // Textures d'abord (plafond, relief), puis le squelette Humanoid, puis les matières.
            foreach (string texture in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).Distinct()) Texture(texture, modest);
            if (importer.animationType != ModelImporterAnimationType.Human || importer.importAnimation)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = false;   // ses animations viennent des bibliothèques de Quaternius (retargeting)
                importer.SaveAndReimport();
            }
            if (!AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().Any(a => a != null && a.isValid && a.isHuman))
            {
                problem = "Unity n'a pas trouvé un squelette humanoïde : exporte de nouveau avec le squelette « Game engine » (guide, étape 5)";
                return null;
            }
            Manifest(model, folder, notes);
            int triangles = Triangles(path);
            if (MadeHumans.BudgetNote(model.Folder, triangles, modest) is string heavy)
            {
                notes.Add(heavy);
                if (MadeHumans.TooHeavy(triangles)) { problem = triangles + " triangles : trop lourd"; return null; }
                problem = triangles + " triangles : au-dessus du budget";
            }
            Materials(path, folder, notes);
            GameObject prefab = MakePrefab(model, path);
            if (prefab == null) { problem = "prefab non créé"; return null; }
            MadeHumanLook previous = catalog.MadeLooks != null ? catalog.MadeLooks.FirstOrDefault(m => m != null && m.Name == model.Name) : null;
            return new MadeHumanLook
            {
                Name = model.Name,
                Folder = model.Folder,
                Model = prefab,
                Height = previous != null && previous.Height > 0.5f ? previous.Height : 1.75f,   // réglage à la main gardé d'un import à l'autre
                Yaw = previous != null ? previous.Yaw : 0f,
            };
        }

        /// <summary>
        /// Manifeste du personnage (<see cref="MadeHumans.ManifestFile"/>) : écrit une fois s'il manque (à remplir), et sa licence est vérifiée
        /// (CC0 ou CC BY seulement : jamais NC, ND ni SA).
        /// </summary>
        private static void Manifest(MadeHumanModel model, string folder, List<string> notes)
        {
            string disk = Path.Combine(Directory.GetCurrentDirectory(), folder, MadeHumans.ManifestFile);
            if (!File.Exists(disk))
            {
                File.WriteAllText(disk, MadeHumans.ManifestTemplate(model.Folder), new System.Text.UTF8Encoding(false));
                AssetDatabase.ImportAsset(folder + "/" + MadeHumans.ManifestFile);
                notes.Add("« " + model.Folder + " » : " + MadeHumans.ManifestFile + " créé : remplis la liste des éléments (cheveux, vêtements) et leurs licences.");
                return;
            }
            string license = MadeHumans.ManifestLicense(File.ReadAllText(disk));
            if (!MadeHumans.IsAllowedLicense(license))
                notes.Add("« " + model.Folder + " » : licence « " + (license ?? "absente") + " » dans " + MadeHumans.ManifestFile + " : seuls CC0 et CC BY sont permis dans le jeu (jamais NC, ND ni SA).");
        }

        /// <summary>Triangles du fichier (toutes ses sous-parties).</summary>
        private static int Triangles(string path)
        {
            long total = 0;
            foreach (Mesh mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
                for (int i = 0; i < mesh.subMeshCount; i++) total += mesh.GetIndexCount(i) / 3;
            return (int)System.Math.Min(total, int.MaxValue);
        }

        /// <summary>Textures : relief marqué comme tel, plafond de taille (1024 en mode PC modeste, 512 sur téléphone).</summary>
        private static void Texture(string path, bool modest)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            bool changed = false;
            if (StyleRules.IsNormalMap(path) && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                changed = true;
            }
            int cap = MadeHumans.TextureCap(modest, false), phone = MadeHumans.TextureCap(modest, true);
            if (importer.maxTextureSize != cap)
            {
                importer.maxTextureSize = cap;
                changed = true;
            }
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            if (!android.overridden || android.maxTextureSize != phone)
            {
                android.overridden = true;
                android.maxTextureSize = phone;
                importer.SetPlatformTextureSettings(android);
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }

        /// <summary>
        /// Matières URP Lit du fichier : la texture de couleur (la sienne, ou celle qui porte son nom), le relief s'il y en a, et la découpe
        /// pour les cheveux, sourcils, cils et barbe (<see cref="MadeHumans.IsCutoutMaterial"/>). Rangées dans <c>Generated/MakeHuman</c>.
        /// </summary>
        private static void Materials(string modelPath, string folder, List<string> notes)
        {
            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer)) return;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                notes.Add("Rendu URP absent : matières de MakeHuman laissées telles quelles.");
                return;
            }
            Folder(PrefabFolder);
            List<string> textures = AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();
            string stem = Path.GetFileNameWithoutExtension(modelPath);
            bool changed = false;
            foreach (Material embedded in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().ToList())
            {
                Texture2D color = OwnTexture(embedded, "_BaseMap", "_MainTex");
                if (color == null && StyleRules.MaterialTexture(embedded.name, textures, false) is string named) color = Load(named);
                if (color == null) continue;
                Texture2D normal = OwnTexture(embedded, "_BumpMap");
                if (normal == null && StyleRules.MaterialTexture(embedded.name, textures, true) is string namedNormal) normal = Load(namedNormal);
                string path = PrefabFolder + "/" + stem + " " + embedded.name + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(lit);
                    AssetDatabase.CreateAsset(material, path);
                }
                material.SetTexture("_BaseMap", color);
                material.SetTexture("_MainTex", color);
                if (normal != null)
                {
                    material.SetTexture("_BumpMap", normal);
                    material.EnableKeyword("_NORMALMAP");
                }
                material.SetFloat("_Smoothness", 0.3f);
                if (MadeHumans.IsCutoutMaterial(embedded.name))
                {
                    material.SetFloat("_AlphaClip", 1f);
                    material.SetFloat("_Cutoff", 0.5f);
                    material.SetFloat("_Cull", 0f);   // vu des deux côtés (mèches fines)
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.renderQueue = 2450;
                }
                EditorUtility.SetDirty(material);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(embedded), material);
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }

        private static GameObject MakePrefab(MadeHumanModel model, string modelPath)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null) return null;
            Folder(PrefabFolder);
            string path = PrefabFolder + "/" + model.Name + ".prefab";
            GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null) return null;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static Texture2D Load(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        private static Texture2D OwnTexture(Material m, params string[] properties)
        {
            foreach (string property in properties)
                if (m.HasProperty(property) && m.GetTexture(property) is Texture2D texture) return texture;
            return null;
        }

        /// <summary>Crée le dossier (et ses parents) s'il manque.</summary>
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
