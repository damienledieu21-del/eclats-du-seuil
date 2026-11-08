using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdS.Runtime;
using UnityEditor;
using UnityEngine;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Sous-lot V2.9.2 (4e vague) : préparation des corps humains, appelée par « Préparer l'essai de style ». Elle trouve les Universal Base
    /// Characters (corps d'homme et de femme, coiffures « Rigged to Head Bone », textures) et les Universal Animation Library 1 et 2 de
    /// Quaternius déposés dans <c>Assets/Style/Quaternius</c> ; règle leur import en Humanoid (avatar fait depuis chaque modèle, les animations
    /// renommées et mises en boucle d'après <see cref="HumanLooks.CanonicalClip"/>, le mouvement de la racine gardé dans la pose) ; peint les
    /// tenues (<see cref="HumanLooks.Palettes"/>) sur la peau des corps, d'après les os qui portent chaque partie (tête et mains : la peau,
    /// buste et bras : la tunique, jambes : les chausses, pieds et bas des mollets : les bottes, une ceinture), en clair et en sombre ;
    /// fait les matériaux (tenues, cheveux, yeux) dans <c>Assets/Style/Generated/Humains</c>. Sans l'un des fichiers, les corps humains ne
    /// sont pas prêts et les personnages gardent leurs modèles d'avant (rien d'invisible).
    /// </summary>
    internal static class EdsHumans
    {
        private const string Folder = EdsStyle.Generated + "/Humains";

        /// <summary>Version de la peinture des tenues : une préparation suivante ne repeint que si elle change ou si un fichier manque.</summary>
        private const int BakeVersion = 1;

        /// <summary>Côté des textures de tenue (px).</summary>
        private const int Size = 1024;

        internal static string Prepare(List<string> models, List<string> textures, StyleCatalog catalog, List<string> notes)
        {
            string male = models.FirstOrDefault(p => HumanLooks.BodyOf(p) == false);
            string female = models.FirstOrDefault(p => HumanLooks.BodyOf(p) == true);
            List<string> libraries = models.Where(HumanLooks.IsAnimationLibrary).OrderBy(p => p, System.StringComparer.Ordinal).ToList();
            var missing = new List<string>();
            if (male == null) missing.Add("Superhero_Male_FullBody.fbx");
            if (female == null) missing.Add("Superhero_Female_FullBody.fbx");
            if (libraries.Count == 0) missing.Add("UAL1_Standard.fbx / UAL2_Standard.fbx");
            string skinMale = HumanLooks.SkinTexture(textures, false, false), skinFemale = HumanLooks.SkinTexture(textures, true, false);
            if (skinMale == null || skinFemale == null) missing.Add("textures des corps (T_Superhero_…)");
            if (missing.Count > 0)
            {
                catalog.Humans = new HumanCatalog();
                return "Corps humains : non (manque " + string.Join(", ", missing) + ")";
            }
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                catalog.Humans = new HumanCatalog();
                return "Corps humains : non (rendu URP absent)";
            }
            Humanoid(male, false);
            Humanoid(female, false);
            foreach (string library in libraries) Humanoid(library, true);
            var hairs = models.Where(p => HumanLooks.HairOf(p) != null).ToList();
            foreach (string hair in hairs) Generic(hair);
            foreach (string body in new[] { male, female }.Concat(libraries))
                if (!IsHuman(body))
                {
                    catalog.Humans = new HumanCatalog();
                    return "Corps humains : non (Unity n'a pas reconnu le squelette Humanoid de " + Path.GetFileName(body)
                        + " : régler « Rig → Humanoid → Configure » à la main, puis relancer)";
                }

            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(EdsStyle.Generated, "Humains");
            var humans = new HumanCatalog
            {
                Male = AssetDatabase.LoadAssetAtPath<GameObject>(male),
                Female = AssetDatabase.LoadAssetAtPath<GameObject>(female),
            };
            // Animations des bibliothèques, renommées à l'import ; la première bibliothèque gagne pour un même nom.
            var clips = new List<AnimationClip>();
            foreach (string library in libraries)
                foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(library).OfType<AnimationClip>())
                    if (!clip.name.StartsWith("__preview__") && clips.All(c => c.name != clip.name)) clips.Add(clip);
            humans.Clips = clips.ToArray();
            // Coiffures dans l'ordre de HumanHair, barbe.
            humans.Hairs = new[] { HumanHair.Buzzed, HumanHair.SimpleParted, HumanHair.Long, HumanHair.Buns, HumanHair.BuzzedFemale }
                .Select(h => hairs.FirstOrDefault(p => HumanLooks.HairOf(p)?.Hair == h)).Select(p => p != null ? AssetDatabase.LoadAssetAtPath<GameObject>(p) : null)
                .ToArray();
            string beard = hairs.FirstOrDefault(p => HumanLooks.HairOf(p)?.Beard == true);
            humans.Beard = beard != null ? AssetDatabase.LoadAssetAtPath<GameObject>(beard) : null;
            humans.Hair1 = SimpleMaterial("Cheveux_1", lit, HumanLooks.HairTexture(textures, 1), 0.3f);
            humans.Hair2 = SimpleMaterial("Cheveux_2", lit, HumanLooks.HairTexture(textures, 2), 0.3f);
            humans.Eyes = SimpleMaterial("Yeux", lit, HumanLooks.HairTexture(textures, 0), 0.6f);
            // Tenues peintes : homme clair, homme sombre, femme claire, femme sombre ; chacun dans l'ordre des palettes.
            bool repaint = catalog.HumanBake != BakeVersion;
            var outfits = new List<Material>();
            foreach (bool woman in new[] { false, true })
            {
                string skinPath = woman ? skinFemale : skinMale;
                Texture2D normal = Texture2DAt(HumanLooks.BodyNormal(textures, woman));
                Readable(skinPath);
                Texture2D skin = AssetDatabase.LoadAssetAtPath<Texture2D>(skinPath);
                Painter painter = null;
                foreach (bool dark in new[] { false, true })
                    for (int p = 0; p < HumanLooks.Palettes.Length; p++)
                    {
                        string name = "Tenue_" + (woman ? "F" : "H") + (dark ? "_sombre_" : "_claire_") + p.ToString("00");
                        string png = Folder + "/" + name + ".png";
                        if (repaint || !File.Exists(png))
                        {
                            painter = painter ?? Painter.For(woman ? humans.Female : humans.Male, skin);
                            if (painter == null)
                            {
                                catalog.Humans = new HumanCatalog();
                                return "Corps humains : non (maillage du corps illisible)";
                            }
                            Texture2D painted = painter.Paint(HumanLooks.Palettes[p], dark);
                            File.WriteAllBytes(png, painted.EncodeToPNG());
                            Object.DestroyImmediate(painted);
                            AssetDatabase.ImportAsset(png);
                            if (AssetImporter.GetAtPath(png) is TextureImporter importer)
                            {
                                importer.maxTextureSize = Size;
                                TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
                                android.overridden = true;
                                android.maxTextureSize = Size / 2;
                                importer.SetPlatformTextureSettings(android);
                                importer.SaveAndReimport();
                            }
                        }
                        Material m = MaterialAt(name, lit);
                        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(png));
                        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(png));
                        m.SetFloat("_Smoothness", 0.18f);
                        if (normal != null)
                        {
                            m.SetTexture("_BumpMap", normal);
                            m.EnableKeyword("_NORMALMAP");
                        }
                        m.enableInstancing = true;
                        EditorUtility.SetDirty(m);
                        outfits.Add(m);
                    }
            }
            humans.Outfits = outfits.ToArray();
            // Sous-lot V2.9.4 : vrais vêtements (Modular Character Outfits - Fantasy), s'ils sont déposés ; sinon les tenues peintes.
            string clothes = Clothes(models, textures, humans, lit, notes);
            catalog.Humans = humans;
            catalog.HumanBake = BakeVersion;
            AssetDatabase.SaveAssets();
            if (!humans.Ready) return "Corps humains : non (ni attente ni marche dans les bibliothèques d'animations)";
            int hairCount = humans.Hairs.Count(h => h != null);
            if (hairCount < 5) notes.Add("Corps humains : " + hairCount + " coiffures sur 5 (dossier « Rigged to Head Bone »).");
            return "Corps humains : prêts (" + outfits.Count + " tenues, " + clips.Count + " animations, " + hairCount + " coiffures"
                + (humans.Beard != null ? ", barbe" : "") + ") ; " + clothes;
        }

        /// <summary>
        /// Sous-lot V2.9.4 : les pièces des Modular Character Outfits - Fantasy de Quaternius (dossier « Outfits » ou « Modular Character
        /// Outfits… » sous <c>Assets/Style</c>) : import générique sans animation (leurs maillages prennent les os du corps, comme les
        /// coiffures), chaque maillage reconnu (<see cref="HumanWardrobe.Classify"/>), un matériau partagé par famille, corps et variante de
        /// texture, la peau des mains, et les corps réduits à leur tête (portés sous les vêtements). Rend la ligne de la fenêtre.
        /// </summary>
        private static string Clothes(List<string> models, List<string> textures, HumanCatalog humans, Shader lit, List<string> notes)
        {
            List<string> files = models.Where(HumanWardrobe.IsWardrobePath).Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, System.StringComparer.Ordinal).ToList();
            if (files.Count == 0)
                return "vrais vêtements : non (aucune pièce dans Assets/Style/Quaternius/Outfits : tenues peintes)";
            var pieces = new List<WardrobeLook>();
            foreach (string file in files)
            {
                Generic(file);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(file);
                if (prefab == null) continue;
                SkinnedMeshRenderer[] skins = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                // Un fichier d'une seule pièce : reconnue d'après son maillage ou son fichier ; un fichier de plusieurs : chaque maillage à part,
                // d'après son seul nom (relecture de la PR #119 : un maillage au nom inconnu n'est pas pris pour la pièce du fichier).
                for (int k = 0; k < skins.Length; k++)
                {
                    SkinnedMeshRenderer skin = skins[k];
                    if (skin.sharedMesh == null) continue;
                    WardrobeItem? found = HumanWardrobe.Classify(file, skin.gameObject.name, skins.Length > 1);
                    if (!(found is WardrobeItem item)) continue;
                    pieces.Add(new WardrobeLook
                    {
                        Model = prefab, Part = skin.gameObject.name, PartIndex = k, Family = item.Family, Slot = item.Slot,
                        Kind = item.Kind, Gender = item.Gender, Key = item.Key,
                    });
                }
            }
            humans.Clothes = pieces.ToArray();
            if (pieces.Count == 0)
                return "vrais vêtements : non (" + files.Count + " fichiers dans le dossier des vêtements, aucune pièce reconnue : tenues peintes)";

            // Étoffes (relecture de la PR #119) : les cartes de couleur du pack seulement, rangées par racine de nom (celle du matériau des
            // pièces : « MI_Ranger » ↔ « T_Ranger_2_BaseColor ») et par corps ; les variantes dans l'ordre des noms.
            var fabrics = new List<WardrobeFabric>();
            var colorMaps = textures.Where(t => HumanWardrobe.IsWardrobePath(t) && HumanWardrobe.IsColorMap(t)).OrderBy(t => t, System.StringComparer.Ordinal).ToList();
            foreach (var group in colorMaps.GroupBy(t =>
                {
                    string f = StyleRules.Key(Path.GetFileNameWithoutExtension(t));
                    int g = f.Contains("female") ? 2 : f.Contains("male") ? 1 : 0;
                    return (HumanWardrobe.FabricStem(t), g);
                }).Where(g => g.Key.Item1.Length > 0).OrderBy(g => g.Key.Item1, System.StringComparer.Ordinal).ThenBy(g => g.Key.Item2))
            {
                var variants = new List<Material>();
                int v = 0;
                foreach (string texture in group.Take(6))
                {
                    Material m = MaterialAt("Etoffe_" + group.Key.Item1 + "_" + group.Key.Item2 + "_" + v++, lit);
                    Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(texture);
                    m.SetTexture("_BaseMap", t);
                    m.SetTexture("_MainTex", t);
                    m.SetFloat("_Smoothness", 0.2f);
                    m.enableInstancing = true;
                    EditorUtility.SetDirty(m);
                    variants.Add(m);
                }
                fabrics.Add(new WardrobeFabric { Stem = group.Key.Item1, Gender = group.Key.Item2, Variants = variants.ToArray() });
            }
            humans.Fabrics = fabrics.ToArray();

            // Peau des mains : homme clair, homme sombre, femme claire, femme sombre.
            var skinsOut = new List<Material>();
            foreach (bool woman in new[] { false, true })
                foreach (bool dark in new[] { false, true })
                {
                    string texture = HumanWardrobe.SkinTexture(textures, woman, dark);
                    if (texture == null)
                    {
                        skinsOut.Add(null);
                        continue;
                    }
                    Material m = MaterialAt("Peau_" + (woman ? "F" : "H") + (dark ? "_sombre" : "_claire"), lit);
                    Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(texture);
                    m.SetTexture("_BaseMap", t);
                    m.SetTexture("_MainTex", t);
                    m.SetFloat("_Smoothness", 0.3f);
                    m.enableInstancing = true;
                    EditorUtility.SetDirty(m);
                    skinsOut.Add(m);
                }
            humans.Skins = skinsOut.ToArray();

            humans.MaleHead = HeadOnly(humans.Male, "Tete_H");
            humans.FemaleHead = HeadOnly(humans.Female, "Tete_F");
            // Relecture de la PR #119 : sans tête seule, le corps peint resterait sous les vêtements : pas de vrais vêtements.
            if (humans.MaleHead == null || humans.FemaleHead == null)
            {
                humans.Clothes = new WardrobeLook[0];
                return "vrais vêtements : non (tête seule du corps non faite : os de la tête introuvables ; tenues peintes)";
            }

            // Ce qui est prêt : par famille, les places couvertes ; une famille sans haut, bras, bas ou pieds se complète d'une autre.
            var lines = new List<string>();
            foreach (WardrobeFamily family in System.Enum.GetValues(typeof(WardrobeFamily)))
            {
                var mine = pieces.Where(p => p.Family == family).ToList();
                if (mine.Count == 0) continue;
                lines.Add(family + " " + mine.Count);
            }
            bool basic = new[] { WardrobeSlot.Body, WardrobeSlot.Arms, WardrobeSlot.Legs, WardrobeSlot.Feet }.All(slot => pieces.Any(p => p.Slot == slot));
            if (!basic)
                return "vrais vêtements : non (" + pieces.Count + " pièces, mais il manque un haut, des bras, un bas ou des pieds : tenues peintes)";
            int heads = pieces.Count(p => p.Slot == WardrobeSlot.Head), cloaks = pieces.Count(p => p.Slot == WardrobeSlot.Back);
            if (cloaks == 0) notes.Add("Vrais vêtements : aucune cape dans le pack déposé : pas de manteau l'hiver.");
            return "vrais vêtements : prêts (" + pieces.Count + " pièces : " + string.Join(", ", lines) + " ; " + heads + " coiffes, " + cloaks + " capes ; "
                + fabrics.Sum(f => f.Variants.Length) + " étoffes" + (skinsOut.Any(m => m != null) ? ", peau des mains" : "") + ")";
        }

        /// <summary>
        /// Le corps <paramref name="prefab"/> réduit à sa tête et à son cou (triangles dont au moins deux coins sont portés pour moitié par les
        /// os de la tête ou du cou), mêmes sommets, os et poids : porté sous les vrais vêtements. Rangé dans <c>Generated/Humains</c>.
        /// </summary>
        private static Mesh HeadOnly(GameObject prefab, string name)
        {
            if (prefab == null) return null;
            SkinnedMeshRenderer body = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(s => s.sharedMesh != null).OrderByDescending(s => s.sharedMesh.vertexCount).FirstOrDefault();
            if (body == null || body.bones == null) return null;
            Mesh source = body.sharedMesh;
            BoneWeight[] weights = source.boneWeights;
            if (weights == null || weights.Length != source.vertexCount) return null;
            var headBone = new bool[body.bones.Length];
            bool any = false;
            for (int i = 0; i < headBone.Length; i++)
            {
                string n = body.bones[i] != null ? body.bones[i].name.ToLowerInvariant() : string.Empty;
                headBone[i] = n.Contains("head") || n.Contains("neck");
                any |= headBone[i];
            }
            if (!any) return null;
            float Head(int v)
            {
                BoneWeight w = weights[v];
                float sum = 0f;
                if (w.boneIndex0 >= 0 && w.boneIndex0 < headBone.Length && headBone[w.boneIndex0]) sum += w.weight0;
                if (w.boneIndex1 >= 0 && w.boneIndex1 < headBone.Length && headBone[w.boneIndex1]) sum += w.weight1;
                if (w.boneIndex2 >= 0 && w.boneIndex2 < headBone.Length && headBone[w.boneIndex2]) sum += w.weight2;
                if (w.boneIndex3 >= 0 && w.boneIndex3 < headBone.Length && headBone[w.boneIndex3]) sum += w.weight3;
                return sum;
            }
            string path = Folder + "/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            Mesh head = Object.Instantiate(source);
            head.name = name;
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                int[] triangles = source.GetTriangles(sub);
                var kept = new List<int>();
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    int n = (Head(triangles[t]) >= 0.5f ? 1 : 0) + (Head(triangles[t + 1]) >= 0.5f ? 1 : 0) + (Head(triangles[t + 2]) >= 0.5f ? 1 : 0);
                    if (n < 2) continue;
                    kept.Add(triangles[t]);
                    kept.Add(triangles[t + 1]);
                    kept.Add(triangles[t + 2]);
                }
                head.SetTriangles(kept.ToArray(), sub);
            }
            AssetDatabase.CreateAsset(head, path);   // bornes du corps entier gardées (le rendu ne coupe pas la tête trop tôt)
            return head;
        }

        /// <summary>Import Humanoid (avatar fait depuis le modèle) ; pour une bibliothèque, les animations renommées et mises en boucle.</summary>
        private static void Humanoid(string path, bool library)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;
            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }
            if (importer.importAnimation != library)
            {
                importer.importAnimation = library;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
            if (!library) return;
            ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
            if (takes == null || takes.Length == 0) return;
            var kept = new List<ModelImporterClipAnimation>();
            foreach (ModelImporterClipAnimation take in takes)
            {
                if (!(HumanLooks.CanonicalClip(take.takeName ?? take.name) is (string Name, bool Loop) named)) continue;
                if (kept.Any(k => k.name == named.Name)) continue;
                take.name = named.Name;
                take.loopTime = named.Loop;
                take.loopPose = named.Loop;   // relecture de la PR #117 : la pose de fin raccordée à celle du début
                take.wrapMode = named.Loop ? WrapMode.Loop : WrapMode.ClampForever;
                // Le mouvement de la racine reste dans la pose : le jeu déplace le corps, les animations « Standard » sont faites sur place.
                take.lockRootRotation = true;
                take.lockRootHeightY = true;
                take.lockRootPositionXZ = true;
                take.keepOriginalOrientation = true;
                take.keepOriginalPositionY = true;
                take.keepOriginalPositionXZ = true;
                kept.Add(take);
            }
            ModelImporterClipAnimation[] now = importer.clipAnimations;
            if (now != null && now.Length == kept.Count && now.Select(c => c.name + c.loopTime).SequenceEqual(kept.Select(c => c.name + c.loopTime))) return;
            importer.clipAnimations = kept.ToArray();
            importer.SaveAndReimport();
        }

        /// <summary>Coiffures : squelette générique, sans animation (leurs maillages prennent les os du corps).</summary>
        private static void Generic(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;
            if (importer.animationType == ModelImporterAnimationType.Generic && !importer.importAnimation) return;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.importAnimation = false;
            importer.SaveAndReimport();
        }

        /// <summary>Unity a-t-il fait un avatar Humanoid valable pour ce fichier ?</summary>
        private static bool IsHuman(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().Any(a => a != null && a.isValid && a.isHuman);

        private static void Readable(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer) || importer.isReadable) return;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        private static Texture2D Texture2DAt(string path) => path != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(path) : null;

        private static Material MaterialAt(string name, Shader lit)
        {
            string path = Folder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(lit) { name = name };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Matériau URP d'une texture (cheveux, yeux), ou null sans texture.</summary>
        private static Material SimpleMaterial(string name, Shader lit, string texture, float smoothness)
        {
            Texture2D t = Texture2DAt(texture);
            if (t == null) return null;
            Material m = MaterialAt(name, lit);
            m.SetTexture("_BaseMap", t);
            m.SetTexture("_MainTex", t);
            m.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// Peintre des tenues d'un corps : chaque point de la texture reçoit la partie du corps du sommet le plus proche dans son triangle
        /// (d'après l'os qui le porte le plus) et sa hauteur dans la pose de repos.
        /// </summary>
        private sealed class Painter
        {
            private enum Part : byte { None, Skin, Tunic, Legs, Boots }

            private readonly Part[] _part = new Part[Size * Size];
            private readonly float[] _height = new float[Size * Size];
            private readonly Color32[] _source;
            private readonly int _sw, _sh;

            private Painter(Color32[] source, int width, int height)
            {
                _source = source;
                _sw = width;
                _sh = height;
            }

            internal static Painter For(GameObject prefab, Texture2D skin)
            {
                if (prefab == null || skin == null) return null;
                Color32[] source = skin.GetPixels32();
                if (source == null || source.Length == 0) return null;
                GameObject copy = Object.Instantiate(prefab);
                try
                {
                    SkinnedMeshRenderer body = copy.GetComponentsInChildren<SkinnedMeshRenderer>()
                        .Where(s => s.sharedMesh != null).OrderByDescending(s => s.sharedMesh.vertexCount).FirstOrDefault();
                    if (body == null) return null;
                    var baked = new Mesh();
                    body.BakeMesh(baked);
                    Vector3[] local = baked.vertices;
                    Vector2[] uv = body.sharedMesh.uv;
                    int[] triangles = body.sharedMesh.triangles;
                    BoneWeight[] weights = body.sharedMesh.boneWeights;
                    Transform[] bones = body.bones;
                    if (local == null || uv == null || triangles == null || weights == null || bones == null || uv.Length != local.Length
                        || weights.Length != local.Length) return null;
                    var world = new Vector3[local.Length];
                    float low = float.MaxValue, high = float.MinValue;
                    for (int i = 0; i < local.Length; i++)
                    {
                        world[i] = body.transform.TransformPoint(local[i]);
                        low = Mathf.Min(low, world[i].y);
                        high = Mathf.Max(high, world[i].y);
                    }
                    float span = Mathf.Max(0.01f, high - low);
                    var parts = new Part[local.Length];
                    var heights = new float[local.Length];
                    for (int i = 0; i < local.Length; i++)
                    {
                        heights[i] = (world[i].y - low) / span;
                        int bone = weights[i].boneIndex0;
                        string name = bone >= 0 && bone < bones.Length && bones[bone] != null ? bones[bone].name.ToLowerInvariant() : string.Empty;
                        parts[i] = PartOf(name);
                    }
                    var painter = new Painter(source, skin.width, skin.height);
                    for (int t = 0; t + 2 < triangles.Length; t += 3) painter.Fill(triangles[t], triangles[t + 1], triangles[t + 2], uv, parts, heights);
                    painter.Grow();
                    Object.DestroyImmediate(baked);
                    return painter;
                }
                finally
                {
                    Object.DestroyImmediate(copy);
                }
            }

            /// <summary>Partie du corps d'après l'os : la tête, le cou et les mains sont la peau ; le buste et les bras la tunique ; le reste les jambes.</summary>
            private static Part PartOf(string bone)
            {
                if (bone.Contains("head") || bone.Contains("neck") || bone.Contains("hand") || bone.Contains("thumb") || bone.Contains("index")
                    || bone.Contains("middle") || bone.Contains("ring") || bone.Contains("pinky")) return Part.Skin;
                if (bone.Contains("foot") || bone.Contains("ball")) return Part.Boots;
                if (bone.Contains("thigh") || bone.Contains("calf") || bone.Contains("pelvis")) return Part.Legs;
                if (bone.Contains("spine") || bone.Contains("clavicle") || bone.Contains("arm")) return Part.Tunic;
                return Part.Skin;
            }

            /// <summary>Remplit un triangle dans l'espace de la texture.</summary>
            private void Fill(int a, int b, int c, Vector2[] uv, Part[] parts, float[] heights)
            {
                float ax = uv[a].x * Size, ay = uv[a].y * Size, bx = uv[b].x * Size, by = uv[b].y * Size, cx = uv[c].x * Size, cy = uv[c].y * Size;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ax, Mathf.Min(bx, cx)))), x1 = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(ax, Mathf.Max(bx, cx))));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ay, Mathf.Min(by, cy)))), y1 = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(ay, Mathf.Max(by, cy))));
                float d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy);
                if (Mathf.Abs(d) < 1e-9f) return;
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        float wa = ((by - cy) * (px - cx) + (cx - bx) * (py - cy)) / d;
                        float wb = ((cy - ay) * (px - cx) + (ax - cx) * (py - cy)) / d;
                        float wc = 1f - wa - wb;
                        if (wa < -0.02f || wb < -0.02f || wc < -0.02f) continue;
                        int i = y * Size + x;
                        _part[i] = wa >= wb && wa >= wc ? parts[a] : wb >= wc ? parts[b] : parts[c];
                        _height[i] = wa * heights[a] + wb * heights[b] + wc * heights[c];
                    }
            }

            /// <summary>Les bords des îlots de la texture prennent la partie voisine (pas de couture de peau sur les vêtements).</summary>
            private void Grow()
            {
                for (int pass = 0; pass < 3; pass++)
                {
                    var next = (Part[])_part.Clone();
                    for (int y = 1; y < Size - 1; y++)
                        for (int x = 1; x < Size - 1; x++)
                        {
                            int i = y * Size + x;
                            if (_part[i] != Part.None) continue;
                            int[] around = { i - 1, i + 1, i - Size, i + Size };
                            foreach (int j in around)
                                if (_part[j] != Part.None)
                                {
                                    next[i] = _part[j];
                                    _height[i] = _height[j];
                                    break;
                                }
                        }
                    System.Array.Copy(next, _part, next.Length);
                }
            }

            /// <summary>
            /// La texture d'une tenue : la peau (assombrie pour une peau sombre), la tunique jusqu'à mi-cuisse avec une ceinture, les chausses,
            /// les bottes jusqu'au mollet ; les plis du corps peint gardés en ombre douce, un grain de tissu.
            /// </summary>
            internal Texture2D Paint((HumanOutfit Outfit, (byte R, byte G, byte B) Tunic, (byte R, byte G, byte B) Trousers, (byte R, byte G, byte B) Boots) palette, bool dark)
            {
                var pixels = new Color32[Size * Size];
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        int i = y * Size + x;
                        Color32 src = _source[Mathf.Min(_sh - 1, y * _sh / Size) * _sw + Mathf.Min(_sw - 1, x * _sw / Size)];
                        float lum = (0.3f * src.r + 0.59f * src.g + 0.11f * src.b) / 255f;
                        Part part = _part[i];
                        float h = _height[i];
                        // La tunique descend jusqu'à mi-cuisse ; les bottes montent au mollet ; la ceinture à la taille (hauteurs mesurées sous
                        // Blender sur les deux corps : bassin 0,53, taille 0,59, genou 0,30, cheville 0,05 de la hauteur).
                        if (part == Part.Legs && h > 0.42f) part = Part.Tunic;
                        if (part == Part.Legs && h < 0.18f) part = Part.Boots;
                        (byte R, byte G, byte B) cloth;
                        switch (part)
                        {
                            case Part.Tunic: cloth = h > 0.575f && h < 0.605f && palette.Outfit != HumanOutfit.Clergy ? ((byte)52, (byte)38, (byte)28) : palette.Tunic; break;
                            case Part.Legs: cloth = palette.Trousers; break;
                            case Part.Boots: cloth = palette.Boots; break;
                            default:
                                pixels[i] = dark ? new Color32((byte)(src.r * 0.62f), (byte)(src.g * 0.50f), (byte)(src.b * 0.44f), 255) : new Color32(src.r, src.g, src.b, 255);
                                continue;
                        }
                        // Ombre douce d'après le corps peint (plis, creux), grain de tissu stable.
                        float shade = Mathf.Clamp(0.78f + 0.4f * lum, 0.86f, 1.06f);
                        float grain = 0.96f + 0.08f * (((x * 73856093) ^ (y * 19349663)) & 255) / 255f;
                        float k = shade * grain;
                        pixels[i] = new Color32((byte)Mathf.Clamp(cloth.R * k, 0, 255), (byte)Mathf.Clamp(cloth.G * k, 0, 255), (byte)Mathf.Clamp(cloth.B * k, 0, 255), 255);
                    }
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
                texture.SetPixels32(pixels);
                texture.Apply();
                return texture;
            }
        }
    }
}
