using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Runtime;
using UnityEditor;
using UnityEngine;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Sous-lot V2.11 (5e vague de ressources) : la part de « Préparer l'essai de style » qui choisit les modèles de la liste partagée
    /// <see cref="Wave5"/> (du meilleur au repli, reconnus par leur nom), règle leur import (animations « Legacy », squelette de one_boy
    /// découpé), mesure la prise des armes, fait le matériau des pains scannés (Megascans), importe les gestes de métier de Mixamo pour les
    /// corps humains, fait la correspondance des icônes de CraftPix et écrit le manifeste des licences de l'Asset Store et de Fab (hors dépôt).
    /// Relançable sans risque : les réglages faits à la main (taille, rotation, prise) sont gardés.
    /// </summary>
    public static class EdsWave5
    {
        private const string Folder = EdsStyle.Generated + "/Wave5";

        /// <summary>Rôles posés en plusieurs exemplaires différents (vitrine du fournil) : jusqu'à ce nombre de fichiers trouvés.</summary>
        private static readonly Dictionary<Wave5Role, int> Several = new Dictionary<Wave5Role, int> { [Wave5Role.Bread] = 6, [Wave5Role.Pastry] = 4 };

        internal static string Prepare(List<string> models, List<string> textures, StyleCatalog catalog, List<string> notes)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(EdsStyle.Generated, "Wave5");
            Wave5Look[] old = catalog.Wave5 ?? new Wave5Look[0];
            var looks = new List<Wave5Look>();
            int live = 0, liveFound = 0, ready = 0;
            foreach (Wave5Entry entry in Wave5.Entries)
            {
                if (entry.Use == Wave5Use.Interior) continue;   // pris par les intérieurs (EdsStyle, InteriorRules)
                if (entry.Use == Wave5Use.Live) live++;
                int max = Several.TryGetValue(entry.Role, out int n) ? n : 1;
                List<string> found = Found(entry, models).Take(max).ToList();
                if (found.Count > 0 && entry.Use == Wave5Use.Live) liveFound++;
                if (found.Count > 0 && entry.Use == Wave5Use.Ready) ready++;
                foreach (string path in found)
                {
                    if (entry.Has(Wave5Fix.CutClips)) Cut(path);
                    else if (entry.Has(Wave5Fix.Animated)) EdsStyle.Legacy(path, false);
                    GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (model == null) continue;
                    Wave5Look before = old.FirstOrDefault(o => o != null && o.Role == entry.Role && o.Model == model);
                    var look = new Wave5Look
                    {
                        Role = entry.Role, Model = model, Scale = before != null && before.Scale > 0f ? before.Scale : 1f, Yaw = before != null ? before.Yaw : 0f,
                    };
                    if (entry.Role == Wave5Role.Mace || entry.Role == Wave5Role.Axe || entry.Role == Wave5Role.Sword)
                    {
                        if (before != null) (look.GripAxis, look.GripSign) = (before.GripAxis, before.GripSign);
                        else (look.GripAxis, look.GripSign) = Grip(model, entry.Role != Wave5Role.Sword);
                    }
                    if (entry.Has(Wave5Fix.ScannedMaterial) && path.StartsWith(Wave5.MegascansFolder, System.StringComparison.Ordinal))
                        look.Material = ScannedMaterial(path, textures, notes);
                    looks.Add(look);
                }
            }
            catalog.Wave5 = looks.ToArray();
            // Le grand loup de Croc-Gris et du loup blanc (la rotation réglée à la main est gardée).
            string dire = Wave5.Pick(Wave5.Entry(Wave5Role.DireWolf), models);
            catalog.DireWolf = dire != null ? AssetDatabase.LoadAssetAtPath<GameObject>(dire) : null;
            // Le squelette de one_boy, s'il est là, est déjà découpé ; le corbeau animé est pris avec les bêtes (EdsStyle).

            string trades = TradeClips(models, catalog, notes);
            string icons = Icons(catalog, notes, out int mapped, out string pack);
            int fab = looks.Count(l => l.Material != null);
            WriteManifest(pack, mapped, fab);
            return "5e vague : " + liveFound + " ressources branchées sur " + live + " trouvées, " + ready + " prêtes pour plus tard"
                + (catalog.DireWolf != null ? ", grand loup de Croc-Gris" : "") + (fab > 0 ? ", " + fab + " pains scannés" : "")
                + " ; " + trades + " ; " + icons;
        }

        /// <summary>Fichiers trouvés pour <paramref name="entry"/>, dans l'ordre de la liste (le meilleur d'abord), chacun une fois.</summary>
        private static IEnumerable<string> Found(Wave5Entry entry, List<string> models)
        {
            var byName = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (string p in models.OrderBy(p => p, System.StringComparer.Ordinal))
                if (!byName.ContainsKey(EssaiManifest.FileName(p))) byName[EssaiManifest.FileName(p)] = p;
            foreach (string file in entry.Files)
                if (byName.TryGetValue(EssaiManifest.FileName(file), out string path)) yield return path;
        }

        /// <summary>Le squelette de one_boy : animations « Legacy » découpées dans sa longue prise (<see cref="Wave5.SkeletonCuts"/>).</summary>
        private static void Cut(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;
            ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
            if (takes == null || takes.Length == 0) return;
            string take = takes[0].takeName;
            ModelImporterClipAnimation[] cuts = Wave5.SkeletonCuts.Select(c => new ModelImporterClipAnimation
            {
                name = c.Name, takeName = take, firstFrame = c.First, lastFrame = c.Last, loopTime = c.Loop,
                wrapMode = c.Loop ? WrapMode.Loop : WrapMode.ClampForever,
            }).ToArray();
            ModelImporterClipAnimation[] now = importer.clipAnimations;
            bool same = importer.animationType == ModelImporterAnimationType.Legacy && now != null && now.Length == cuts.Length
                && now.Select(c => c.name + c.firstFrame + c.lastFrame).SequenceEqual(cuts.Select(c => c.name + c.firstFrame + c.lastFrame));
            if (same) return;
            importer.animationType = ModelImporterAnimationType.Legacy;
            importer.importAnimation = true;
            importer.clipAnimations = cuts;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Prise d'une arme : son grand axe (boîte de ses sommets) et le bout du haut (<see cref="Wave5.GripSign"/> : le côté le plus chargé de
        /// sommets en haut pour une masse ou une hache, en bas pour une épée).
        /// </summary>
        private static (int Axis, int Sign) Grip(GameObject model, bool headHeavy)
        {
            var points = new List<Vector3>();
            foreach (MeshFilter f in model.GetComponentsInChildren<MeshFilter>())
                if (f.sharedMesh != null)
                    foreach (Vector3 v in f.sharedMesh.vertices) points.Add(f.transform.TransformPoint(v));
            foreach (SkinnedMeshRenderer r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (r.sharedMesh != null)
                    foreach (Vector3 v in r.sharedMesh.vertices) points.Add(r.transform.TransformPoint(v));
            if (points.Count == 0) return (1, 1);
            Vector3 min = points[0], max = points[0], sum = Vector3.zero;
            foreach (Vector3 p in points)
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
                sum += p;
            }
            Vector3 mean = sum / points.Count;
            int axis = Wave5.LongAxis(max.x - min.x, max.y - min.y, max.z - min.z);
            float Get(Vector3 v) => axis == 0 ? v.x : axis == 2 ? v.z : v.y;
            return (axis, Wave5.GripSign(Get(mean), Get(min), Get(max), headHeavy));
        }

        /// <summary>
        /// Matériau URP Lit d'un pain de Megascans, fait de ses textures rangées à côté de lui (« …_Mid_2K_BaseColor.jpg », « _Normal »,
        /// « _AO ») ; sa rugosité, uniforme sur un pain, est rendue par un poli faible. Null sans sa couleur.
        /// </summary>
        private static Material ScannedMaterial(string modelPath, List<string> textures, List<string> notes)
        {
            string dir = EssaiManifest.Folder(modelPath) + "/";
            string Map(string word) => textures.FirstOrDefault(t => t.StartsWith(dir, System.StringComparison.Ordinal)
                && Path.GetFileNameWithoutExtension(t).EndsWith("_" + word, System.StringComparison.OrdinalIgnoreCase));
            string color = Map("BaseColor"), normal = Map("Normal"), ao = Map("AO");
            if (color == null) return null;
            if (normal != null && AssetImporter.GetAtPath(normal) is TextureImporter n && n.textureType != TextureImporterType.NormalMap)
            {
                n.textureType = TextureImporterType.NormalMap;
                n.SaveAndReimport();
            }
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return null;
            string path = Folder + "/" + Path.GetFileNameWithoutExtension(modelPath) + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(lit);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = lit;
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(color));
            if (normal != null)
            {
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
                material.EnableKeyword("_NORMALMAP");
            }
            if (ao != null)
            {
                material.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ao));
                material.EnableKeyword("_OCCLUSIONMAP");
            }
            material.SetFloat("_Smoothness", 0.18f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Gestes de métier de Mixamo (<see cref="Wave5.TradeFolder"/>) : import Humanoid, clip renommé (« Trade_Counter »…), ajoutés aux
        /// animations des corps humains (lus par <see cref="HumanLooks.WorkClip"/>). Sans eux, les gestes de Quaternius restent.
        /// </summary>
        private static string TradeClips(List<string> models, StyleCatalog catalog, List<string> notes)
        {
            var clips = new List<AnimationClip>();
            var missing = new List<string>();
            foreach ((string word, string name, string use) in Wave5.TradeClips)
            {
                string path = Wave5.TradeClip(word, models);
                if (path == null)
                {
                    missing.Add(word);
                    continue;
                }
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;
                ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
                bool changed = importer.animationType != ModelImporterAnimationType.Human || !importer.importAnimation;
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                if (takes != null && takes.Length > 0)
                {
                    ModelImporterClipAnimation take = takes[0];
                    take.name = name;
                    take.loopTime = true;
                    take.loopPose = true;
                    take.wrapMode = WrapMode.Loop;
                    take.lockRootRotation = true;
                    take.lockRootHeightY = true;
                    take.lockRootPositionXZ = true;
                    take.keepOriginalOrientation = true;
                    take.keepOriginalPositionY = true;
                    take.keepOriginalPositionXZ = true;
                    ModelImporterClipAnimation[] now = importer.clipAnimations;
                    if (now == null || now.Length != 1 || now[0].name != name) changed = true;
                    importer.clipAnimations = new[] { take };
                }
                if (changed) importer.SaveAndReimport();
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
                if (clip != null) clips.Add(clip);
            }
            catalog.TradeClips = clips.ToArray();
            if (catalog.Humans != null && catalog.Humans.Clips != null && clips.Count > 0)
                catalog.Humans.Clips = catalog.Humans.Clips.Where(c => c != null && clips.All(t => t.name != c.name)).Concat(clips).ToArray();
            if (missing.Count > 0 && clips.Count > 0) notes.Add("Gestes de métier : manquent " + string.Join(", ", missing) + " (les gestes de Quaternius les remplacent).");
            return "gestes de métier : " + clips.Count + " sur " + Wave5.TradeClips.Count;
        }

        /// <summary>Icônes du pack de CraftPix (où qu'il soit dans Assets) pour chaque objet du jeu ; vide sans le pack.</summary>
        private static string Icons(StyleCatalog catalog, List<string> notes, out int mapped, out string pack)
        {
            List<string> icons = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Where(ItemIcons.IsIcon).Distinct().ToList();
            pack = icons.Count > 0 ? PackFolder(icons[0]) : null;
            List<string> items = ItemIds();
            IReadOnlyList<(string Item, string Icon)> map = ItemIcons.Map(items, icons);
            catalog.ItemIcons = map.Select(m => new ItemIconLook { Item = m.Item, Icon = AssetDatabase.LoadAssetAtPath<Texture2D>(m.Icon) })
                .Where(i => i.Icon != null).ToArray();
            mapped = catalog.ItemIcons.Length;
            if (icons.Count == 0) return "icônes de CraftPix : pack introuvable (icônes du jeu)";
            notes.Add("Icônes de CraftPix (" + pack + ") : " + mapped + " objets sur " + items.Count + " ; les autres gardent l'icône du jeu.");
            return "icônes de CraftPix : " + mapped + " objets sur " + items.Count;
        }

        /// <summary>Dossier du pack : le premier dossier du chemin qui le nomme (« Assets/CraftPix/6200 Fantasy RPG Icons Pack »).</summary>
        private static string PackFolder(string icon)
        {
            string[] parts = icon.Split('/');
            for (int i = 0; i < parts.Length - 1; i++)
                if (ItemIcons.IsPackPath(string.Join("/", parts.Take(i + 1)) + "/x.png")) return string.Join("/", parts.Take(i + 1));
            return EssaiManifest.Folder(icon);
        }

        /// <summary>Identifiants des objets du jeu (catalogue data/v0/world/items.json du dépôt).</summary>
        private static List<string> ItemIds()
        {
            string file = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "data", "v0", "world", "items.json"));
            if (!File.Exists(file)) return new List<string>();
            return Regex.Matches(File.ReadAllText(file), "\"id\"\\s*:\\s*\"(item\\.[a-z_]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
        }

        /// <summary>Le manifeste des licences hors dépôt (Asset Store, Fab), dans <c>Assets/Style/Generated</c> (non suivi par git).</summary>
        private static void WriteManifest(string pack, int mapped, int fab)
        {
            string text = ItemIcons.Manifest(pack, mapped, ItemIds().Count)
                + "| Pains scannés (9) | Quixel Megascans (Fab) | " + Wave5.FabLicense + " | " + Wave5.MegascansFolder + " | vitrine du fournil et étals : "
                + fab + " posés (voir MANIFESTE_FAB.md de Damien) |\n";
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ItemIcons.LicenseManifest));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            if (!File.Exists(full) || File.ReadAllText(full) != text) File.WriteAllText(full, text);
        }
    }
}
