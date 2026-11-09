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
                int max = Wave5.Variants(entry.Role);
                List<string> found = Found(entry, models).Take(max).ToList();
                if (found.Count > 0 && entry.Use == Wave5Use.Live) liveFound++;
                if (found.Count > 0 && entry.Use == Wave5Use.Ready) ready++;
                foreach (string path in found)
                {
                    if (entry.Has(Wave5Fix.CutClips)) Cut(path);
                    else if (entry.Has(Wave5Fix.Animated)) EdsStyle.Legacy(path, false);
                    // Relecture de la PR #122 : une ressource prête (sans lieu) n'entre pas au catalogue (qui est dans Resources : tout ce qu'il
                    // nomme part dans le jeu construit, téléphone compris) ; elle y entrera quand elle sera posée.
                    if (entry.Use == Wave5Use.Ready) continue;
                    GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (model == null) continue;
                    Wave5Look before = old.FirstOrDefault(o => o != null && o.Role == entry.Role && o.Model == model);
                    var look = new Wave5Look
                    {
                        Role = entry.Role, Model = model, Scale = before != null && before.Scale > 0f ? before.Scale : 1f, Yaw = before != null ? before.Yaw : 0f,
                        Offset = before != null ? before.Offset : Vector3.zero,
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
            // Sous-lot V2.12 : les packs de l'Asset Store (prefabs à la racine de Assets), dans la même liste du catalogue.
            List<string> prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();
            string store = PrepareStore(prefabs, old, looks, out int storePrefabs);
            catalog.Wave5 = looks.ToArray();
            string gui = Gui(catalog, notes);
            // Le grand loup de Croc-Gris et du loup blanc (la rotation réglée à la main est gardée).
            string dire = Wave5.Pick(Wave5.Entry(Wave5Role.DireWolf), models);
            catalog.DireWolf = dire != null ? AssetDatabase.LoadAssetAtPath<GameObject>(dire) : null;
            // Le squelette de one_boy, s'il est là, est déjà découpé ; le corbeau animé est pris avec les bêtes (EdsStyle).

            string trades = TradeClips(models, catalog, notes);
            string icons = Icons(catalog, notes, out int mapped, out string pack);
            int fab = looks.Count(l => l.Material != null);
            WriteManifest(pack, mapped, fab, prefabs);
            return "5e vague : " + liveFound + " ressources branchées sur " + live + " trouvées, " + ready + " prêtes pour plus tard"
                + (catalog.DireWolf != null ? ", grand loup de Croc-Gris" : "") + (fab > 0 ? ", " + fab + " pains scannés" : "")
                + " ; " + trades + " ; " + icons + " ; " + store + " ; " + gui;
        }

        /// <summary>
        /// Sous-lot V2.12 : les ressources de l'Asset Store (<see cref="AssetStore.Entries"/>, reconnues par la fin de leur chemin), ajoutées à
        /// <paramref name="looks"/> avec les réglages faits à la main (taille, rotation, décalage). Les intérieurs sont pris par <see cref="StoreFurniture"/>.
        /// </summary>
        private static string PrepareStore(List<string> prefabs, Wave5Look[] old, List<Wave5Look> looks, out int count)
        {
            int live = 0, liveFound = 0, ready = 0;
            count = 0;
            foreach (Wave5Entry entry in AssetStore.Entries)
            {
                if (entry.Use == Wave5Use.Interior) continue;
                if (entry.Use == Wave5Use.Live) live++;
                List<string> found = AssetStore.Found(entry, prefabs).Take(AssetStore.Variants(entry.Role)).ToList();
                if (found.Count > 0 && entry.Use == Wave5Use.Live) liveFound++;
                if (found.Count > 0 && entry.Use == Wave5Use.Ready) ready++;
                if (entry.Use == Wave5Use.Ready) continue;   // relecture de la PR #122 : hors du catalogue tant qu'elle n'est pas posée
                foreach (string path in found)
                {
                    GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (model == null) continue;
                    Wave5Look before = old.FirstOrDefault(o => o != null && o.Role == entry.Role && o.Model == model);
                    looks.Add(new Wave5Look
                    {
                        Role = entry.Role, Model = model, Scale = before != null && before.Scale > 0f ? before.Scale : 1f, Yaw = before != null ? before.Yaw : 0f,
                        Offset = before != null ? before.Offset : Vector3.zero,
                    });
                    count++;
                }
            }
            return "Asset Store : " + liveFound + " ressources branchées sur " + live + ", " + ready + " prêtes pour plus tard (" + count + " prefabs au catalogue)";
        }

        /// <summary>
        /// Sous-lot V2.12 : objets d'intérieur de l'Asset Store ajoutés à <paramref name="furniture"/> pour les sortes qui y manquent seulement
        /// (<see cref="AssetStore.InteriorFallbacks"/>) ; rotation et échelle réglées à la main gardées (<paramref name="old"/>).
        /// </summary>
        internal static InteriorLook[] StoreFurniture(InteriorLook[] furniture, InteriorLook[] old, List<string> prefabs)
        {
            Wave5Entry entry = AssetStore.Entries.First(e => e.Use == Wave5Use.Interior);
            var list = (furniture ?? new InteriorLook[0]).ToList();
            foreach ((string path, InteriorProp kind) in AssetStore.InteriorFallbacks(AssetStore.Found(entry, prefabs), list.Select(f => f.Kind)))
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;
                InteriorLook before = (old ?? new InteriorLook[0]).FirstOrDefault(o => o != null && o.Model == model);
                list.Add(new InteriorLook
                {
                    Kind = kind, Model = model, Yaw = before != null ? before.Yaw : (float)InteriorRules.DefaultYaw(kind), Scale = before != null && before.Scale > 0f ? before.Scale : 1f,
                });
            }
            return list.ToArray();
        }

        /// <summary>
        /// Sous-lot V2.12 : planches de pierre et de parchemin de Fantasy Wooden GUI pour le fond des fenêtres (importées en Sprite, sans
        /// réduction en puissance de deux) ; sans le pack, la dalle du jeu reste.
        /// </summary>
        private static string Gui(StyleCatalog catalog, List<string> notes)
        {
            List<string> images = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Where(AssetStore.IsGuiPath).ToList();
            string stone = AssetStore.Board(images, AssetStore.GuiBoard.Stone), parchment = AssetStore.Board(images, AssetStore.GuiBoard.Parchment);
            catalog.GuiStone = stone != null ? Sprite(stone) : null;
            catalog.GuiParchment = parchment != null ? Sprite(parchment) : null;
            if (images.Count == 0) return "interface en bois : pack introuvable (dalle du jeu)";
            notes.Add("Interface : planche de pierre " + (stone != null ? EssaiManifest.FileName(stone) : "absente") + ", parchemin "
                + (parchment != null ? EssaiManifest.FileName(parchment) : "absent") + " ; réglage « Gui Board Kind » du catalogue (" + catalog.GuiBoardKind + ").");
            return "interface : " + catalog.GuiBoardKind + (catalog.GuiBoard() != null ? "" : " (planche absente : dalle du jeu)");
        }

        /// <summary>Une image d'interface importée en Sprite (pas de réduction, pas de mipmaps), chargée.</summary>
        private static Texture2D Sprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
            // Sous-lot V2.12 : la version « No Background » d'une image d'abord (« Assets/Fantasy RPG Icons Pack/Asset/<Catégorie> Icons/… »).
            List<string> icons = AssetStore.PreferNoBackground(AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Where(ItemIcons.IsIcon)).ToList();
            pack = icons.Count > 0 ? PackFolder(icons[0]) : null;
            List<string> items = ItemIds();
            IReadOnlyList<(string Item, string Icon)> map = ItemIcons.Map(items, icons);
            catalog.ItemIcons = map.Select(m => new ItemIconLook { Item = m.Item, Icon = AssetDatabase.LoadAssetAtPath<Texture2D>(m.Icon) })
                .Where(i => i.Icon != null).ToArray();
            mapped = catalog.ItemIcons.Length;
            if (icons.Count == 0) return "icônes de CraftPix : pack introuvable (icônes du jeu)";
            int clean = icons.Count(i => ("/" + i.Replace('\\', '/').ToLowerInvariant()).Contains("/no background/"));
            notes.Add("Icônes de CraftPix (" + pack + ") : " + mapped + " objets reliés sur " + items.Count + " (" + icons.Count + " images, dont " + clean
                + " sans fond) ; les autres gardent l'icône du jeu.");
            Debug.Log("Éclats du Seuil — icônes de CraftPix : " + mapped + " objets reliés sur " + items.Count + ".");
            return "icônes de CraftPix : " + mapped + " objets reliés sur " + items.Count;
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
            return Regex.Matches(File.ReadAllText(file), "\"id\"\\s*:\\s*\"(item\\.[a-z0-9_]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
        }

        /// <summary>Le manifeste des licences hors dépôt (Asset Store, Fab), dans <c>Assets/Style/Generated</c> (non suivi par git).</summary>
        private static void WriteManifest(string pack, int mapped, int fab, List<string> prefabs)
        {
            // Sous-lot V2.12 : une ligne par pack de l'Asset Store (prefabs reconnus, sons gardés, images d'interface).
            List<string> audio = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();
            List<string> images = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();
            int Taken(AssetStorePack p) =>
                p == AssetStore.Sounds ? AssetStore.MedievalSounds(audio).Count
                : p == AssetStore.Gui ? images.Count(AssetStore.IsGuiPath)
                : AssetStore.Entries.Where(e => AssetStore.PackOf(e) == p || e.Files.Any(f => f.StartsWith(p.Folder + "/", System.StringComparison.Ordinal)))
                    .SelectMany(e => AssetStore.Found(e, prefabs).Where(f => e.Files.Any(x => AssetStore.Same(f, x) && x.StartsWith(p.Folder + "/", System.StringComparison.Ordinal)))).Distinct().Count();
            string text = ItemIcons.Manifest(pack, mapped, ItemIds().Count)
                + "| Pains scannés (9) | Quixel Megascans (Fab) | " + Wave5.FabLicense + " | " + Wave5.MegascansFolder + " | vitrine du fournil et étals : "
                + fab + " posés (voir MANIFESTE_FAB.md de Damien) |\n"
                + AssetStore.ManifestRows(Taken);
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ItemIcons.LicenseManifest));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            if (!File.Exists(full) || File.ReadAllText(full) != text) File.WriteAllText(full, text);
        }
    }
}
