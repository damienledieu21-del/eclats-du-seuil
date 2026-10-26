using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdS.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Essai de style (décision de Damien du 25 octobre 2026) : menu « Éclats du Seuil → Préparer l'essai de style ». Il parcourt les packs
    /// déposés dans <c>Assets/Style</c> (n'importe quels sous-dossiers), choisit les modèles et les sons par leur nom (<see cref="StyleRules"/>),
    /// règle l'import des modèles animés (animations « Legacy », attente, marche et course en boucle) et écrit le catalogue
    /// <c>Assets/Style/Resources/StyleCatalog.asset</c>, que le jeu lit au lancement. Relançable : il remplit de nouveau le catalogue.
    /// <para>
    /// Essai réaliste (26 octobre 2026) : il prépare aussi les fichiers de Poly Haven (cartes de relief, matériaux avec transparence pour
    /// l'herbe), les animations Mixamo du héros (fichiers rangés à côté de son modèle, renommées d'après le fichier), le ciel (matériau
    /// panoramique), le matériau du terrain et les textures de sol (dossier « Ground »), et active les effets d'image d'URP s'ils sont
    /// coupés. Les fichiers créés vont dans <c>Assets/Style/Generated</c>.
    /// </para>
    /// </summary>
    public static class EdsStyle
    {
        public const string Root = "Assets/Style";
        public const string CatalogFolder = Root + "/Resources";
        public const string CatalogPath = CatalogFolder + "/" + StyleCatalog.ResourceName + ".asset";
        public const string Generated = Root + "/Generated";

        /// <summary>Données d'effets d'image livrées avec URP (ce que coche « Post-processing » dans l'inspecteur du renderer).</summary>
        private const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        [MenuItem("Éclats du Seuil/Préparer l'essai de style")]
        private static void Prepare()
        {
            if (!AssetDatabase.IsValidFolder(Root))
            {
                EditorUtility.DisplayDialog("Essai de style", "Dossier « " + Root + " » introuvable : dézippe d'abord les packs dans unity/Assets/Style.", "OK");
                return;
            }
            if (!AssetDatabase.IsValidFolder(Generated)) AssetDatabase.CreateFolder(Root, "Generated");
            var notes = new List<string>();

            // Textures d'abord : cartes de relief, ciel (les matériaux et le terrain s'en servent).
            List<string> textures = AssetDatabase.FindAssets("t:Texture2D", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.StartsWith(Generated + "/")).Distinct().ToList();
            foreach (string path in textures) Texture(path);

            List<string> models = AssetDatabase.FindAssets("t:Model", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();
            List<string> sounds = AssetDatabase.FindAssets("t:AudioClip", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();

            string hero = StyleRules.Candidates(models, StyleSlot.Hero).FirstOrDefault();
            string wolf = StyleRules.Candidates(models, StyleSlot.Wolf).FirstOrDefault();
            string boar = StyleRules.Candidates(models, StyleSlot.Boar).FirstOrDefault();
            foreach (string animated in new[] { hero, wolf, boar }.Where(p => p != null)) Legacy(animated, false);
            IReadOnlyList<string> heroClips = StyleRules.Companions(hero, models);
            foreach (string clip in heroClips) Legacy(clip, true);

            // Matériaux réalistes des modèles de Poly Haven (couleur, relief, transparence de l'herbe).
            foreach (string model in models.Where(p => StyleRules.IsRealistic(p) && !heroClips.Contains(p) && p != hero)) RealisticMaterial(model, notes);
            // Essai de Damien du 26 octobre 2026 : le paladin de Mixamo était tout blanc (ses textures restent dans son fichier) ; elles sont
            // sorties à côté de lui, puis chaque matériau reçoit les siennes.
            if (hero != null && StyleRules.IsRealistic(hero)) MixamoMaterials(hero, notes);
            // Personnages de Mixamo rangés chacun dans son dossier (PNJ, archer, rôdeur, Gardien ; demande de Damien du 26 octobre 2026).
            var characters = new List<(string Id, string Model, List<AnimationClip> Clips)>();
            foreach (KeyValuePair<string, IReadOnlyList<string>> folder in StyleRules.Characters(models))
            {
                foreach (string file in folder.Value) Legacy(file, true);
                string model = folder.Value.FirstOrDefault(f => Load(f) is GameObject g && g.GetComponentInChildren<SkinnedMeshRenderer>() != null);
                if (model == null)
                {
                    notes.Add("Personnage « " + folder.Key + " » : aucun modèle avec sa peau dans son dossier.");
                    continue;
                }
                MixamoMaterials(model, notes);
                characters.Add((folder.Key, model, folder.Value.SelectMany(Clips).ToList()));
            }
            // Essai du 26 octobre 2026 : des buissons du MegaKit sortaient blancs (textures manquantes, ajoutées depuis) ; un modèle des packs
            // dont un matériau n'a pas de texture est réimporté, pour retrouver celles qui sont arrivées depuis.
            foreach (string model in models.Where(p => !StyleRules.IsRealistic(p) && Untextured(p))) AssetDatabase.ImportAsset(model, ImportAssetOptions.ForceUpdate);

            if (!AssetDatabase.IsValidFolder(CatalogFolder)) AssetDatabase.CreateFolder(Root, "Resources");
            StyleCatalog catalog = AssetDatabase.LoadAssetAtPath<StyleCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<StyleCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Hero = Load(hero);
            catalog.Wolf = Load(wolf);
            catalog.Boar = Load(boar);
            catalog.HeroClips = heroClips.SelectMany(Clips).ToArray();
            catalog.Trees = StyleRules.ForCatalog(models, StyleSlot.Tree, 8).Select(Load).Where(m => m != null).ToArray();
            catalog.Rocks = StyleRules.ForCatalog(models, StyleSlot.Rock, 6).Select(Load).Where(m => m != null).ToArray();
            catalog.Plants = StyleRules.ForCatalog(models, StyleSlot.Plant, 16).Select(Load).Where(m => m != null).ToArray();
            catalog.Debris = StyleRules.ForCatalog(models, StyleSlot.Debris, 6).Select(Load).Where(m => m != null).ToArray();
            // Les réglages faits à la main (hauteur, rotation) sont gardés d'une préparation à l'autre.
            catalog.Characters = characters.Select(c =>
            {
                CharacterLook old = (catalog.Characters ?? new CharacterLook[0]).FirstOrDefault(o => o != null && o.Id == c.Id);
                return new CharacterLook
                {
                    Id = c.Id, Model = Load(c.Model), Clips = c.Clips.ToArray(),
                    Height = old != null && old.Height > 0f ? old.Height : c.Id == "gardien" ? 2.7f : 1.8f, Yaw = old != null ? old.Yaw : 0f,
                };
            }).ToArray();
            catalog.Surfaces = new[] { StyleSurface.Stone, StyleSurface.Plaster, StyleSurface.Roof, StyleSurface.Wood, StyleSurface.Cobble }
                .Select(kind =>
                {
                    SurfaceLook old = (catalog.Surfaces ?? new SurfaceLook[0]).FirstOrDefault(o => o != null && o.Kind == kind);
                    return new SurfaceLook
                    {
                        Kind = kind, Color = Texture2DAt(StyleRules.SurfaceTexture(textures, kind, false)),
                        Normal = Texture2DAt(StyleRules.SurfaceTexture(textures, kind, true)), Tile = old != null && old.Tile > 0.1f ? old.Tile : 2.5f,
                    };
                }).Where(l => l.Color != null).ToArray();
            catalog.Hit = Sound(sounds, StyleSound.Hit);
            catalog.Block = Sound(sounds, StyleSound.Block);
            catalog.Parry = Sound(sounds, StyleSound.Parry);
            catalog.Death = Sound(sounds, StyleSound.Death);
            catalog.Potion = Sound(sounds, StyleSound.Potion);
            catalog.Gather = Sound(sounds, StyleSound.Gather);
            catalog.Craft = Sound(sounds, StyleSound.Craft);

            catalog.SoilColor = Texture2DAt(StyleRules.GroundTexture(textures, StyleGround.Soil, false));
            catalog.SoilNormal = Texture2DAt(StyleRules.GroundTexture(textures, StyleGround.Soil, true));
            catalog.RockColor = Texture2DAt(StyleRules.GroundTexture(textures, StyleGround.Rock, false));
            catalog.RockNormal = Texture2DAt(StyleRules.GroundTexture(textures, StyleGround.Rock, true));
            catalog.GrassColor = Texture2DAt(StyleRules.GroundTexture(textures, StyleGround.Grass, false));
            catalog.GrassNormal = Texture2DAt(StyleRules.GroundTexture(textures, StyleGround.Grass, true));
            catalog.Sky = SkyMaterial(StyleRules.Sky(textures));
            if (catalog.AmbienceVersion < StyleCatalog.AmbienceDefaults)
            {
                catalog.ResetAmbience();
                notes.Add("Lumière et couleurs remises aux réglages de l'essai (version " + StyleCatalog.AmbienceDefaults + ").");
            }
            catalog.Terrain = TerrainMaterial();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            PostProcessing(notes);

            int found = new[] { catalog.Hit, catalog.Block, catalog.Parry, catalog.Death, catalog.Potion, catalog.Gather, catalog.Craft }.Count(c => c != null);
            int grounds = new[] { catalog.SoilColor, catalog.RockColor, catalog.GrassColor }.Count(t => t != null);
            string report = "Héros : " + Name(hero) + " (" + catalog.HeroClips.Length + " animations à part)\nLoup : " + Name(wolf) + "\nSanglier : " + Name(boar)
                + "\nArbres : " + catalog.Trees.Length + ", rochers : " + catalog.Rocks.Length + ", plantes : " + catalog.Plants.Length + ", bois mort : " + catalog.Debris.Length
                + "\nPersonnages : " + (catalog.Characters.Length == 0 ? "aucun" : string.Join(", ", catalog.Characters.Select(c => c.Id + " (" + c.Clips.Length + " animations)")))
                + "\nBâtiments : " + catalog.Surfaces.Length + " matières sur 5"
                + "\nSons : " + found + " sur 7"
                + "\nCiel : " + (catalog.Sky != null ? "oui" : "non") + ", sols : " + grounds + " sur 3"
                + (notes.Count > 0 ? "\n\n" + string.Join("\n", notes) : "")
                + "\n\n" + models.Count + " modèles, " + textures.Count + " textures et " + sounds.Count + " sons trouvés dans Assets/Style. Appuie sur Play."
                + "\nLumière, brume et couleurs se règlent pendant le jeu dans Assets/Style/Resources/StyleCatalog.";
            Debug.Log("Essai de style\n" + report);
            EditorUtility.DisplayDialog("Essai de style", report, "OK");
        }

        /// <summary>
        /// Animations « Legacy » (lues par le composant Animation), attente, marche et course en boucle. <paramref name="rename"/> : les
        /// animations prennent le nom du fichier (Mixamo les appelle toutes « mixamo.com »).
        /// </summary>
        private static void Legacy(string path, bool rename)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;
            if (importer.animationType != ModelImporterAnimationType.Legacy || !importer.importAnimation)
            {
                importer.animationType = ModelImporterAnimationType.Legacy;
                importer.importAnimation = true;
                importer.SaveAndReimport();
            }
            ModelImporterClipAnimation[] clips = importer.clipAnimations != null && importer.clipAnimations.Length > 0
                ? importer.clipAnimations : importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            for (int i = 0; i < clips.Length; i++)
            {
                ModelImporterClipAnimation clip = clips[i];
                if (rename) clip.name = clips.Length == 1 ? StyleRules.ClipName(path) : StyleRules.ClipName(path) + " " + (i + 1);
                bool loop = StyleRules.Loops(clip.name);
                clip.loopTime = loop;
                clip.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        /// <summary>Animations d'un fichier (sans les aperçus de l'éditeur).</summary>
        private static IEnumerable<AnimationClip> Clips(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__"));

        /// <summary>Réglage d'import d'une texture : cartes de relief en « Normal map », ciel sans mipmaps et sans répétition.</summary>
        private static void Texture(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            bool changed = false;
            if (StyleRules.IsNormalMap(path) && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                changed = true;
            }
            if (StyleRules.IsSky(path) && (importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp))
            {
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }

        /// <summary>
        /// Matériau URP d'un modèle de Poly Haven, d'après ses textures (dossier « textures » à côté du modèle : couleur, relief,
        /// transparence) ; l'herbe reçoit sa transparence (couleur et alpha réunis dans une texture de <c>Generated</c>), vue des deux
        /// côtés. Tous les matériaux du modèle sont remplacés par celui-ci.
        /// </summary>
        private static void RealisticMaterial(string modelPath, List<string> notes)
        {
            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer)) return;
            string folder = Path.GetDirectoryName(modelPath).Replace('\\', '/');
            List<string> maps = AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .OrderBy(p => p, System.StringComparer.Ordinal).ToList();
            string color = maps.FirstOrDefault(p => StyleRules.IsColorMap(p) && !StyleRules.IsSky(p));
            if (color == null) return;
            string normal = maps.FirstOrDefault(StyleRules.IsNormalMap);
            string alpha = maps.FirstOrDefault(StyleRules.IsAlphaMap);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                notes.Add("Rendu URP absent : matériaux de Poly Haven laissés tels quels.");
                return;
            }
            string name = Path.GetFileNameWithoutExtension(modelPath);
            string materialPath = Generated + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(lit);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            Texture2D baseMap = alpha != null ? WithAlpha(color, alpha, Generated + "/" + name + "_couleur_alpha.png", notes) : Texture2DAt(color);
            material.SetTexture("_BaseMap", baseMap);
            material.SetTexture("_MainTex", baseMap);
            material.SetFloat("_Smoothness", 0.15f);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", Texture2DAt(normal));
                material.EnableKeyword("_NORMALMAP");
            }
            if (alpha != null)
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.45f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetFloat("_Cull", 0f);
                material.renderQueue = 2450;
            }
            EditorUtility.SetDirty(material);
            foreach (Material embedded in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>())
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(embedded), material);
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Matériaux d'un modèle de Mixamo (le paladin, les personnages) : textures sorties du fichier dans « Textures » à côté de lui, puis
        /// modèle réimporté pour que Unity les relie ; un matériau resté sans texture reçoit un matériau d'URP fait de celles qui portent son
        /// nom (<see cref="StyleRules.MaterialTexture"/>), dans <c>Generated</c>.
        /// </summary>
        private static void MixamoMaterials(string modelPath, List<string> notes)
        {
            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer)) return;
            EmbeddedTextures(modelPath, notes);
            if (Untextured(modelPath)) AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            if (!Untextured(modelPath)) return;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return;
            string folder = Path.GetDirectoryName(modelPath).Replace('\\', '/');
            List<string> textures = AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();
            bool changed = false;
            foreach (Material embedded in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().Where(m => !HasTexture(m)))
            {
                string color = StyleRules.MaterialTexture(embedded.name, textures, false);
                if (color == null) continue;
                string normal = StyleRules.MaterialTexture(embedded.name, textures, true);
                string path = Generated + "/" + Path.GetFileNameWithoutExtension(modelPath) + " " + embedded.name + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(lit);
                    AssetDatabase.CreateAsset(material, path);
                }
                material.SetTexture("_BaseMap", Texture2DAt(color));
                material.SetTexture("_MainTex", Texture2DAt(color));
                if (normal != null)
                {
                    material.SetTexture("_BumpMap", Texture2DAt(normal));
                    material.EnableKeyword("_NORMALMAP");
                }
                material.SetFloat("_Smoothness", 0.25f);
                EditorUtility.SetDirty(material);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(embedded), material);
                changed = true;
            }
            if (changed)
            {
                importer.SaveAndReimport();
                notes.Add("Matériaux de " + Path.GetFileName(modelPath) + " refaits avec leurs textures.");
            }
        }

        /// <summary>Un matériau du modèle est-il sans texture de couleur ?</summary>
        private static bool Untextured(string modelPath) => AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().Any(m => !HasTexture(m));

        private static bool HasTexture(Material m) =>
            m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null || m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null;

        /// <summary>
        /// Textures rangées dans le fichier d'un modèle (Mixamo les y met) : sorties une fois dans un dossier « Textures » à côté de lui,
        /// réglées comme les autres (cartes de relief).
        /// </summary>
        private static void EmbeddedTextures(string modelPath, List<string> notes)
        {
            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer)) return;
            string parent = Path.GetDirectoryName(modelPath).Replace('\\', '/');
            string folder = parent + "/Textures";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(parent, "Textures");
            if (AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Length == 0 && importer.ExtractTextures(folder))
            {
                AssetDatabase.Refresh();
                notes.Add("Textures de " + Path.GetFileName(modelPath) + " sorties dans " + folder + ".");
            }
            foreach (string texture in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)) Texture(texture);
        }

        /// <summary>Texture de couleur avec la transparence d'une autre (même taille), écrite en PNG ; la couleur seule sinon.</summary>
        private static Texture2D WithAlpha(string colorPath, string alphaPath, string target, List<string> notes)
        {
            Readable(colorPath);
            Readable(alphaPath);
            Texture2D color = Texture2DAt(colorPath), alpha = Texture2DAt(alphaPath);
            if (color == null || alpha == null || color.width != alpha.width || color.height != alpha.height)
            {
                notes.Add("Transparence non réunie pour " + Path.GetFileName(colorPath) + " (tailles différentes).");
                return color;
            }
            Color32[] rgb = color.GetPixels32(), a = alpha.GetPixels32();
            for (int i = 0; i < rgb.Length; i++) rgb[i].a = a[i].r;
            var merged = new Texture2D(color.width, color.height, TextureFormat.RGBA32, true);
            merged.SetPixels32(rgb);
            merged.Apply();
            File.WriteAllBytes(target, merged.EncodeToPNG());
            Object.DestroyImmediate(merged);
            AssetDatabase.ImportAsset(target);
            if (AssetImporter.GetAtPath(target) is TextureImporter importer)
            {
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return Texture2DAt(target);
        }

        private static void Readable(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }

        /// <summary>Matériau de ciel panoramique (image HDR en latitude-longitude), ou null sans ciel.</summary>
        private static Material SkyMaterial(string skyPath)
        {
            Texture2D sky = Texture2DAt(skyPath);
            Shader shader = Shader.Find("Skybox/Panoramic");
            if (sky == null || shader == null) return null;
            string path = Generated + "/Ciel.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_MainTex", sky);
            material.SetFloat("_Mapping", 1f);     // latitude-longitude
            material.SetFloat("_ImageType", 0f);   // 360 degrés
            material.SetFloat("_Exposure", 1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Matériau du terrain d'URP (emporté dans le jeu construit par le catalogue), ou null sans URP.</summary>
        private static Material TerrainMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader == null) return null;
            string path = Generated + "/Terrain.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        /// <summary>
        /// Effets d'image d'URP (couleurs, halo) : le renderer créé par <see cref="EdsProjectSetup"/> les a coupés (pas de données
        /// d'effets) ; ils sont rebranchés sur les données livrées avec URP.
        /// </summary>
        private static void PostProcessing(List<string> notes)
        {
            PostProcessData data = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
            if (data == null)
            {
                string found = AssetDatabase.FindAssets("t:PostProcessData", new[] { "Packages/com.unity.render-pipelines.universal" })
                    .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
                if (found != null) data = AssetDatabase.LoadAssetAtPath<PostProcessData>(found);
            }
            foreach (string path in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (renderer == null || renderer.postProcessData != null) continue;
                if (data == null)
                {
                    notes.Add("Effets d'image d'URP introuvables : couleurs de l'image sans retouche.");
                    return;
                }
                renderer.postProcessData = data;
                EditorUtility.SetDirty(renderer);
                notes.Add("Effets d'image activés dans " + Path.GetFileName(path) + ".");
            }
            AssetDatabase.SaveAssets();
        }

        private static GameObject Load(string path) => path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;

        private static Texture2D Texture2DAt(string path) => path != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(path) : null;

        private static AudioClip Sound(List<string> sounds, StyleSound sound) =>
            StyleRules.PickSound(sounds, sound) is string path ? AssetDatabase.LoadAssetAtPath<AudioClip>(path) : null;

        private static string Name(string path) => path != null ? System.IO.Path.GetFileName(path) : "aucun (forme simple)";
    }
}
