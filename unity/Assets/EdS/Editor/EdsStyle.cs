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
            // Essai réaliste 4 (choix de Damien du 27 octobre 2026) : un fichier de Poly Haven présent en plusieurs résolutions (1k de l'essai
            // 3, 2k ou 8k de l'essai 4) ne compte qu'une fois, dans sa plus haute résolution.
            List<string> textures = StyleRules.Highest(AssetDatabase.FindAssets("t:Texture2D", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.StartsWith(Generated + "/")).Distinct()).ToList();
            foreach (string path in textures) Texture(path);

            List<string> models = StyleRules.Highest(AssetDatabase.FindAssets("t:Model", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath).Distinct()).ToList();
            List<string> sounds = AssetDatabase.FindAssets("t:AudioClip", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();

            string hero = StyleRules.Candidates(models, StyleSlot.Hero).FirstOrDefault();
            string wolf = StyleRules.Candidates(models, StyleSlot.Wolf).FirstOrDefault();
            string boar = StyleRules.Candidates(models, StyleSlot.Boar).FirstOrDefault();
            foreach (string animated in new[] { hero, wolf, boar }.Where(p => p != null)) Legacy(animated, false);
            IReadOnlyList<string> heroClips = StyleRules.Companions(hero, models);
            foreach (string clip in heroClips) Legacy(clip, true);

            // Matériaux réalistes des modèles de Poly Haven (couleur, relief, transparence de l'herbe). Les personnages de Mixamo ont les leurs
            // (MixamoMaterials) : à une deuxième préparation, leurs textures déjà sorties ne doivent pas les remplacer toutes par une seule.
            // Essai réaliste 4 : arbres, arbustes et plantes bougent au vent (shader « EdS/Feuillage »), les arbres moins que les herbes.
            var wind = new Dictionary<string, float>();
            foreach (string p in StyleRules.ForCatalog(models, StyleSlot.Plant, 16).Concat(StyleRules.PropCandidates(models, StyleProp.Herb))) wind[p] = 1f;
            foreach (string p in StyleRules.ForCatalog(models, StyleSlot.Shrub, 6)) wind[p] = 0.6f;
            foreach (string p in StyleRules.ForCatalog(models, StyleSlot.Tree, 8)) wind[p] = 0.25f;
            // Arbres de Sketchfab (fichiers de Tree It, sans matière) : un modèle préparé par arbre, une matière par pièce (TreePrefab).
            var trees = new Dictionary<string, string>();
            foreach (string tree in StyleRules.ForCatalog(models, StyleSlot.Tree, 8).Where(IsSketchfab))
                if (TreePrefab(tree, notes) is string prefab) trees[tree] = prefab;
            foreach (string model in models.Where(p => StyleRules.IsRealistic(p) && !heroClips.Contains(p) && p != hero && !IsMixamo(p) && !trees.ContainsKey(p)))
                RealisticMaterial(model, wind.TryGetValue(model, out float w) ? w : 0f, notes);
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

            // Le moulin de Sketchfab a ses ailes animées : animation « Legacy », jouée en boucle par le jeu.
            string mill = StyleRules.PropCandidates(models, StyleProp.Mill).FirstOrDefault();
            if (mill != null) Legacy(mill, false);

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
            catalog.Trees = StyleRules.ForCatalog(models, StyleSlot.Tree, 8).Select(p => trees.TryGetValue(p, out string prefab) ? prefab : p).Select(Load)
                .Where(m => m != null).ToArray();
            catalog.Rocks = StyleRules.ForCatalog(models, StyleSlot.Rock, 6).Select(Load).Where(m => m != null).ToArray();
            catalog.Plants = StyleRules.ForCatalog(models, StyleSlot.Plant, 16).Select(Load).Where(m => m != null).ToArray();
            catalog.Debris = StyleRules.ForCatalog(models, StyleSlot.Debris, 6).Select(Load).Where(m => m != null).ToArray();
            // Essai réaliste (demandes de Damien du 27 octobre 2026) : arbustes des haies, objets de la forge, ponton, herbe de l'Aube, moulin,
            // et le matériau de l'eau. L'échelle et la rotation réglées à la main sont gardées.
            catalog.Shrubs = StyleRules.ForCatalog(models, StyleSlot.Shrub, 6).Select(Load).Where(m => m != null).ToArray();
            catalog.Props = System.Enum.GetValues(typeof(StyleProp)).Cast<StyleProp>()
                .Select(kind =>
                {
                    PropLook old = (catalog.Props ?? new PropLook[0]).FirstOrDefault(o => o != null && o.Kind == kind);
                    return new PropLook
                    {
                        Kind = kind, Model = Load(StyleRules.PropCandidates(models, kind).FirstOrDefault()),
                        Scale = old != null && old.Scale > 0f ? old.Scale : 1f, Yaw = old != null ? old.Yaw : 0f,
                    };
                }).Where(p => p.Model != null).ToArray();
            catalog.Water = WaterMaterial(notes);
            catalog.Grass = ShaderMaterial("EdS/Herbe", "Herbe", notes);
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
            catalog.Surfaces = new[] { StyleSurface.Stone, StyleSurface.Plaster, StyleSurface.Roof, StyleSurface.Wood, StyleSurface.Cobble, StyleSurface.Bark }
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
            // Essai réaliste 7 : variantes des bruitages, musiques (dossier « Music ») et ambiances (dossier « Ambience »), importées pour
            // le téléphone (musique lue en continu depuis le disque, ambiances compressées en mémoire, bruitages décompressés).
            // Relecture de l'essai 7 : un seul passage d'import pour tous les sons (pas un par fichier).
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in sounds) AudioImport(path);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            catalog.SoundSets = System.Enum.GetValues(typeof(StyleSound)).Cast<StyleSound>()
                .Select(kind => new SoundSet { Kind = kind, Clips = StyleRules.PickSounds(sounds, kind, 6).Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p)).Where(c => c != null).ToArray() })
                .Where(s => s.Clips.Length > 0).ToArray();
            catalog.Music = System.Enum.GetValues(typeof(StyleMusic)).Cast<StyleMusic>()
                .Select(kind => new MusicLook { Kind = kind, Clip = sounds.Where(p => StyleRules.MusicOf(p) == kind).OrderBy(p => p, System.StringComparer.Ordinal).Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p)).FirstOrDefault(c => c != null) })
                .Where(m => m.Clip != null).ToArray();
            catalog.Ambiences = System.Enum.GetValues(typeof(StyleAmbience)).Cast<StyleAmbience>()
                .Select(kind => new AmbienceLook { Kind = kind, Clip = sounds.Where(p => StyleRules.AmbienceOf(p) == kind).OrderBy(p => p, System.StringComparer.Ordinal).Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p)).FirstOrDefault(c => c != null) })
                .Where(a => a.Clip != null).ToArray();

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
            catalog.PhonePipeline = RenderQuality(notes);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            int found = new[] { catalog.Hit, catalog.Block, catalog.Parry, catalog.Death, catalog.Potion, catalog.Gather, catalog.Craft }.Count(c => c != null);
            int grounds = new[] { catalog.SoilColor, catalog.RockColor, catalog.GrassColor }.Count(t => t != null);
            string report = "Héros : " + Name(hero) + " (" + catalog.HeroClips.Length + " animations à part)\nLoup : " + Name(wolf) + "\nSanglier : " + Name(boar)
                + "\nArbres : " + catalog.Trees.Length + ", rochers : " + catalog.Rocks.Length + ", plantes : " + catalog.Plants.Length + ", bois mort : " + catalog.Debris.Length
                + "\nPersonnages : " + (catalog.Characters.Length == 0 ? "aucun" : string.Join(", ", catalog.Characters.Select(c => c.Id + " (" + c.Clips.Length + " animations)")))
                + "\nBâtiments : " + catalog.Surfaces.Length + " matières sur 6, arbustes des haies : " + catalog.Shrubs.Length
                + "\nObjets : " + (catalog.Props.Length == 0 ? "aucun" : string.Join(", ", catalog.Props.Select(p => PropName(p.Kind))))
                + "\nEau : " + (catalog.Water != null ? "vraie eau" : "eau simple") + ", herbe en brins : " + (catalog.Grass != null ? "oui" : "non")
                + ", rendu du téléphone : " + (catalog.PhonePipeline != null ? "oui" : "non")
                + "\nSons : " + found + " sur 7, bruitages variés : " + catalog.SoundSets.Length + " sur " + System.Enum.GetValues(typeof(StyleSound)).Length
                + ", musiques : " + catalog.Music.Length + " sur 4, ambiances : " + catalog.Ambiences.Length + " sur 3"
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
            changed |= PlatformSizes(importer, StyleRules.IsSky(path));
            if (changed) importer.SaveAndReimport();
        }

        /// <summary>
        /// Essai réaliste 4 (demande de Damien du 27 octobre 2026 : « deux packs de textures, un pour le PC et un pour le téléphone ») : un
        /// seul fichier, pleine taille sur ordinateur (le ciel jusqu'à 8k, le reste jusqu'à 2k), réduit par Unity à la construction pour
        /// Android (1k, le ciel 2k). Rend vrai si un réglage a changé.
        /// </summary>
        private static bool PlatformSizes(TextureImporter importer, bool sky)
        {
            bool changed = false;
            int desktop = sky ? 8192 : 2048, phone = sky ? 2048 : 1024;
            if (importer.maxTextureSize < desktop)
            {
                importer.maxTextureSize = desktop;
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
            return changed;
        }

        /// <summary>
        /// Matériau URP d'un modèle de Poly Haven ou de Sketchfab, d'après ses textures (dossier du modèle : couleur, relief, transparence) ;
        /// l'herbe et les feuilles reçoivent leur transparence (couleur et alpha réunis dans une texture de <c>Generated</c>), vues des deux
        /// côtés. Un modèle à plusieurs matières (le ponton : planches et pieux ; les arbres : écorce et feuilles) reçoit un matériau par
        /// matière, fait des textures qui lui correspondent (<see cref="StyleRules.MaterialTexture"/>, <see cref="StyleRules.AlphaFor"/>).
        /// Essai réaliste 4 : avec <paramref name="wind"/> positif (arbres, arbustes, plantes), le matériau prend le shader « EdS/Feuillage »
        /// (vent, contre-jour), s'il se compile ; sinon URP Lit.
        /// </summary>
        private static void RealisticMaterial(string modelPath, float wind, List<string> notes)
        {
            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer)) return;
            string folder = Path.GetDirectoryName(modelPath).Replace('\\', '/');
            List<string> maps = AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .Where(p => !StyleRules.IsSky(p)).OrderBy(p => p, System.StringComparer.Ordinal).ToList();
            maps = StyleRules.Highest(maps).ToList();
            string color = maps.FirstOrDefault(StyleRules.IsColorMap);
            if (color == null) return;
            string normal = maps.FirstOrDefault(StyleRules.IsNormalMap);
            string alpha = StyleRules.AlphaFor(color, maps) ?? (maps.Count(StyleRules.IsAlphaMap) == 1 ? maps.First(StyleRules.IsAlphaMap) : null);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                notes.Add("Rendu URP absent : matériaux de Poly Haven laissés tels quels.");
                return;
            }
            Shader leaves = Shader.Find("EdS/Feuillage");
            string name = Path.GetFileNameWithoutExtension(modelPath);
            // Essai du 27 octobre 2026 : le ponton a deux matières (planches, pieux) ; chacune prend les textures à son nom.
            List<Material> embeddedMaterials = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().ToList();
            if (embeddedMaterials.Count > 1)
            {
                // Revue Codex de la PR #58 (2e tour) : la carte de transparence du modèle (toiles du moulin) découpe aussi les matières qui
                // prennent sa texture de couleur ; couleur et alpha réunis une seule fois pour toutes.
                Texture2D cutout = null;
                foreach (Material embedded in embeddedMaterials)
                {
                    string own = StyleRules.MaterialTexture(embedded.name, maps, false) ?? color;
                    string ownNormal = StyleRules.MaterialTexture(embedded.name, maps, true) ?? (own == color ? normal : null);
                    // Transparence de même racine (feuilles), sinon celle du modèle pour les matières qui prennent sa couleur (toiles du moulin).
                    string ownAlpha = StyleRules.AlphaFor(own, maps) ?? (own == color ? alpha : null);
                    string path = Generated + "/" + name + " " + embedded.name + ".mat";
                    Material part = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (part == null)
                    {
                        part = new Material(lit);
                        AssetDatabase.CreateAsset(part, path);
                    }
                    // Couleur et alpha du modèle réunis une seule fois pour toutes les matières qui les partagent.
                    Texture2D partMap;
                    if (ownAlpha == null) partMap = Texture2DAt(own);
                    else if (own == color && ownAlpha == alpha)
                    {
                        if (cutout == null) cutout = WithAlpha(color, alpha, Generated + "/" + name + "_couleur_alpha.png", notes);
                        partMap = cutout;
                    }
                    else partMap = WithAlpha(own, ownAlpha, Generated + "/" + name + " " + embedded.name + "_couleur_alpha.png", notes);
                    Configure(part, lit, leaves, wind, partMap, Texture2DAt(ownNormal), ownAlpha != null);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(embedded), part);
                }
                importer.SaveAndReimport();
                return;
            }
            // Deuxième préparation d'un modèle à plusieurs matières (déjà reliées à leurs matériaux) : seuls le shader et le vent changent.
            List<Material> parts = AssetDatabase.FindAssets("t:Material", new[] { Generated }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileName(p).StartsWith(name + " ", System.StringComparison.Ordinal)).Select(p => AssetDatabase.LoadAssetAtPath<Material>(p))
                .Where(m => m != null).ToList();
            if (embeddedMaterials.Count == 0 && parts.Count > 1)
            {
                foreach (Material part in parts)
                {
                    Texture2D partMap = part.GetTexture("_BaseMap") as Texture2D;
                    Configure(part, lit, leaves, wind, partMap, part.GetTexture("_BumpMap") as Texture2D, part.GetFloat("_Cutoff") > 0f && partMap != null
                        && AssetDatabase.GetAssetPath(partMap).StartsWith(Generated + "/", System.StringComparison.Ordinal));
                }
                return;
            }
            string materialPath = Generated + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(lit);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            Texture2D baseMap = alpha != null ? WithAlpha(color, alpha, Generated + "/" + name + "_couleur_alpha.png", notes) : Texture2DAt(color);
            Configure(material, lit, leaves, wind, baseMap, Texture2DAt(normal), alpha != null);
            foreach (Material embedded in embeddedMaterials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(embedded), material);
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Réglage d'un matériau réaliste : URP Lit, ou « EdS/Feuillage » pour ce que le vent fait bouger (<paramref name="wind"/> positif,
        /// shader compilé). <paramref name="alpha"/> : transparence découpée, vue des deux côtés, feuilles qui frémissent.
        /// </summary>
        private static void Configure(Material material, Shader lit, Shader leaves, float wind, Texture2D baseMap, Texture2D normal, bool alpha)
        {
            bool foliage = wind > 0f && leaves != null && leaves.isSupported;
            Shader shader = foliage ? leaves : lit;
            if (material.shader != shader) material.shader = shader;
            material.SetTexture("_BaseMap", baseMap);
            material.SetTexture("_MainTex", baseMap);
            material.SetFloat("_Smoothness", 0.15f);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            if (foliage)
            {
                material.SetFloat("_Wind", wind);
                material.SetFloat("_Flutter", alpha ? 1f : 0f);
                material.SetFloat("_Cutoff", alpha ? 0.45f : 0f);
                material.enableInstancing = true;
                material.renderQueue = 2450;
            }
            else if (alpha)
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.45f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetFloat("_Cull", 0f);
                material.renderQueue = 2450;
            }
            EditorUtility.SetDirty(material);
        }

        /// <summary>
        /// Matériaux d'un modèle de Mixamo (le paladin, les personnages) : textures sorties du fichier dans « Textures » à côté de lui, puis
        /// modèle réimporté pour que Unity les relie ; un matériau resté sans texture reçoit un matériau d'URP fait de celles qui portent son
        /// nom (<see cref="StyleRules.MaterialTexture"/>), dans <c>Generated</c>. Revue Codex de la PR #57 (2e tour) : un matériau texturé
        /// mais resté sur un shader hors d'URP (il serait rose) est refait aussi, avec ses propres textures.
        /// </summary>
        private static void MixamoMaterials(string modelPath, List<string> notes)
        {
            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer)) return;
            EmbeddedTextures(modelPath, notes);
            if (Untextured(modelPath)) AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return;
            List<Material> redo = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().Where(m => !HasTexture(m) || !IsUrp(m.shader)).ToList();
            if (redo.Count == 0) return;
            string folder = Path.GetDirectoryName(modelPath).Replace('\\', '/');
            List<string> textures = AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();
            bool changed = false;
            foreach (Material embedded in redo)
            {
                // Ses propres textures si Unity les a reliées, sinon celles qui portent son nom.
                Texture2D color = OwnTexture(embedded, "_BaseMap", "_MainTex");
                if (color == null && StyleRules.MaterialTexture(embedded.name, textures, false) is string named) color = Texture2DAt(named);
                if (color == null) continue;
                Texture2D normal = OwnTexture(embedded, "_BumpMap");
                if (normal == null && StyleRules.MaterialTexture(embedded.name, textures, true) is string namedNormal) normal = Texture2DAt(namedNormal);
                string path = Generated + "/" + Path.GetFileNameWithoutExtension(modelPath) + " " + embedded.name + ".mat";
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

        /// <summary>Shader d'URP, compilé pour ce rendu (un matériau Standard importé serait rose).</summary>
        private static bool IsUrp(Shader shader) =>
            shader != null && shader.isSupported && shader.name.StartsWith("Universal Render Pipeline/", System.StringComparison.Ordinal);

        /// <summary>Première texture 2D posée sur l'une des propriétés du matériau, ou null.</summary>
        private static Texture2D OwnTexture(Material m, params string[] properties)
        {
            foreach (string property in properties)
                if (m.HasProperty(property) && m.GetTexture(property) is Texture2D texture) return texture;
            return null;
        }

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
                Cutout(importer);
                PlatformSizes(importer, false);
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

        /// <summary>
        /// Texture à transparence découpée (feuilles, herbes) : bords propres, et des réductions de loin (mipmaps) qui gardent la même part
        /// de feuillage au seuil du shader, pour que les arbres ne se dégarnissent pas au loin. Rend vrai si un réglage a changé.
        /// </summary>
        private static bool Cutout(TextureImporter importer)
        {
            if (importer.alphaIsTransparency && importer.mipMapsPreserveCoverage && Mathf.Approximately(importer.alphaTestReferenceValue, 0.45f)) return false;
            importer.alphaIsTransparency = true;
            importer.mipMapsPreserveCoverage = true;
            importer.alphaTestReferenceValue = 0.45f;
            return true;
        }

        /// <summary>Fichier rangé dans un dossier « Sketchfab ».</summary>
        private static bool IsSketchfab(string path) => ("/" + path.Replace('\\', '/').ToLowerInvariant()).Contains("/sketchfab/");

        /// <summary>
        /// Essai réaliste 4 : arbre de Sketchfab exporté par Tree It (FBX 6.1 sans matière : une texture posée sur chaque pièce). Un modèle
        /// préparé est enregistré dans <c>Generated</c> : chaque pièce y reçoit son matériau « EdS/Feuillage » (écorce, ou feuillage à
        /// transparence découpée qui frémit), d'après son nom (<see cref="StyleRules.PartTextures"/>). Rend son chemin, ou null.
        /// </summary>
        private static string TreePrefab(string modelPath, List<string> notes)
        {
            GameObject model = Load(modelPath);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit"), leaves = Shader.Find("EdS/Feuillage");
            if (model == null || lit == null) return null;
            string folder = Path.GetDirectoryName(modelPath).Replace('\\', '/');
            List<string> maps = AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();
            foreach (string map in maps.Where(m => StyleRules.IsFoliage(m) && StyleRules.IsColorMap(m)))
                if (AssetImporter.GetAtPath(map) is TextureImporter importer && Cutout(importer)) importer.SaveAndReimport();
            string name = Path.GetFileNameWithoutExtension(modelPath);
            GameObject instance = Object.Instantiate(model);
            int dressed = 0;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                if (!(StyleRules.PartTextures(renderer.gameObject.name, maps) is (string color, string normal))) continue;
                bool foliage = StyleRules.IsFoliage(color);
                string path = Generated + "/" + name + " " + Path.GetFileNameWithoutExtension(color) + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(lit);
                    AssetDatabase.CreateAsset(material, path);
                }
                // Revue Codex de la PR #59 : une opacité fournie à part (« oak_leaf_alpha.png ») est réunie à la couleur, que le shader découpe.
                string alpha = StyleRules.AlphaFor(color, maps);
                Texture2D baseMap = alpha != null
                    ? WithAlpha(color, alpha, Generated + "/" + name + " " + Path.GetFileNameWithoutExtension(color) + "_couleur_alpha.png", notes)
                    : Texture2DAt(color);
                Configure(material, lit, leaves, 0.25f, baseMap, Texture2DAt(normal), foliage || alpha != null);
                renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
                dressed++;
            }
            string prefabPath = Generated + "/" + name + " (arbre).prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            notes.Add("Arbre " + Path.GetFileName(modelPath) + " : " + dressed + " pièces habillées.");
            return dressed > 0 ? prefabPath : null;
        }

        /// <summary>Fichier rangé dans un dossier « Mixamo » (héros, personnages, leurs animations).</summary>
        private static bool IsMixamo(string path) => ("/" + path.Replace('\\', '/').ToLowerInvariant()).Contains("/mixamo/");

        /// <summary>
        /// Matériau de la vraie eau (shader « EdS/Eau » du dossier du jeu), créé une fois dans <c>Generated/Eau.mat</c> : ses réglages
        /// (couleurs, vagues, reflet) se changent ensuite à la main et sont gardés. Null si le shader ne se compile pas (eau simple).
        /// </summary>
        private static Material WaterMaterial(List<string> notes)
        {
            Shader shader = Shader.Find("EdS/Eau");
            if (shader == null || !shader.isSupported)
            {
                notes.Add("Shader de l'eau « EdS/Eau » indisponible : eau simple.");
                return null;
            }
            string path = Generated + "/Eau.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Matériau d'un shader du jeu (« EdS/Herbe »), créé une fois dans <c>Generated</c> ; ses réglages faits à la main sont gardés. Null
        /// si le shader ne se compile pas.
        /// </summary>
        private static Material ShaderMaterial(string shaderName, string file, List<string> notes)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null || !shader.isSupported)
            {
                notes.Add("Shader « " + shaderName + " » indisponible.");
                return null;
            }
            string path = Generated + "/" + file + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;
            // Essai réaliste 5 : quand Claude change les réglages par défaut du shader (version « _Reglages »), le matériau les reprend une
            // fois ; les réglages faits ensuite à la main sont gardés.
            if (material.HasProperty("_Reglages") && material.GetFloat("_Reglages") < ShaderSettingsVersion)
            {
                material.CopyPropertiesFromMaterial(new Material(shader));
                material.SetFloat("_Reglages", ShaderSettingsVersion);
                notes.Add("Réglages de « " + file + " » remis à ceux de l'essai (version " + ShaderSettingsVersion + ").");
            }
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Version des réglages par défaut des shaders du jeu (essai réaliste 5 : herbe plus claire, fleurs, plaques sèches).</summary>
        private const float ShaderSettingsVersion = 5f;

        /// <summary>
        /// Essai réaliste 4 (demande de Damien du 27 octobre 2026 : « le maximum de réalisme », réglages différents pour le PC et le
        /// téléphone) : le rendu du projet devient celui de l'ordinateur (ombres fines sur 90 m en quatre cascades, ombres douces, lissage
        /// des bords, occlusion ambiante de l'écran) ; un rendu léger est créé pour le téléphone dans <c>Generated</c> (ombres sur 35 m,
        /// deux cascades, sans occlusion), choisi par le jeu au lancement sur Android. Rend ce rendu léger, ou null sans URP.
        /// </summary>
        private static RenderPipelineAsset RenderQuality(List<string> notes)
        {
            var pipelines = new[] { GraphicsSettings.defaultRenderPipeline, QualitySettings.renderPipeline }.OfType<UniversalRenderPipelineAsset>().Distinct().ToList();
            if (pipelines.Count == 0)
            {
                notes.Add("Rendu URP absent : réglages de l'image laissés tels quels.");
                return null;
            }
            foreach (UniversalRenderPipelineAsset pipeline in pipelines)
                Tune(pipeline, 90f, 4, 4096, true, 4);
            int ssao = 0;
            foreach (string path in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.StartsWith(Generated + "/", System.StringComparison.Ordinal)))
                if (AmbientOcclusion(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path))) ssao++;
            notes.Add("Rendu de l'ordinateur : ombres fines et douces, bords lissés" + (ssao > 0 ? ", occlusion ambiante ajoutée." : "."));

            string rendererPath = Generated + "/EdS_Renderer_Telephone.asset", pipelinePath = Generated + "/EdS_URP_Telephone.asset";
            UniversalRendererData phoneRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (phoneRenderer == null)
            {
                phoneRenderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(phoneRenderer, rendererPath);
            }
            UniversalRenderPipelineAsset phone = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (phone == null)
            {
                phone = UniversalRenderPipelineAsset.Create(phoneRenderer);
                AssetDatabase.CreateAsset(phone, pipelinePath);
            }
            PostProcessData data = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
            if (data != null && phoneRenderer.postProcessData == null)
            {
                phoneRenderer.postProcessData = data;
                EditorUtility.SetDirty(phoneRenderer);
            }
            Tune(phone, 35f, 2, 1024, false, 2);
            AssetDatabase.SaveAssets();
            return phone;
        }

        /// <summary>Réglages d'ombres, de lissage et de lumières d'un rendu URP (champs absents de cette version d'URP : ignorés).</summary>
        private static void Tune(UniversalRenderPipelineAsset pipeline, float shadowDistance, int cascades, int shadowResolution, bool softShadows, int msaa)
        {
            var so = new SerializedObject(pipeline);
            void Set(string name, System.Action<SerializedProperty> apply)
            {
                SerializedProperty property = so.FindProperty(name);
                if (property != null) apply(property);
            }
            Set("m_ShadowDistance", p => p.floatValue = shadowDistance);
            Set("m_ShadowCascadeCount", p => p.intValue = cascades);
            Set("m_MainLightShadowmapResolution", p => p.intValue = shadowResolution);
            Set("m_MainLightShadowsSupported", p => p.boolValue = true);
            Set("m_SoftShadowsSupported", p => p.boolValue = softShadows);
            Set("m_SoftShadowQuality", p => p.intValue = softShadows ? 3 : 1);
            Set("m_MSAA", p => p.intValue = msaa);
            Set("m_SupportsHDR", p => p.boolValue = true);
            Set("m_AdditionalLightsRenderingMode", p => p.intValue = 1);   // LightRenderingMode.PerPixel (1 ; PerVertex vaut 2)
            Set("m_AdditionalLightsPerObjectLimit", p => p.intValue = 4);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }

        /// <summary>
        /// Ajoute l'occlusion ambiante de l'écran d'URP (coins et pieds des objets assombris) au renderer, s'il ne l'a pas : comme le bouton
        /// « Add Renderer Feature » de l'inspecteur (la fonction et son identifiant dans la liste du renderer). Rend vrai si elle a été ajoutée.
        /// </summary>
        private static bool AmbientOcclusion(UniversalRendererData renderer)
        {
            if (renderer == null) return false;
            System.Type type = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
            if (type == null || renderer.rendererFeatures.Any(f => f != null && f.GetType() == type)) return false;
            var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
            feature.name = "ScreenSpaceAmbientOcclusion";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
            var so = new SerializedObject(renderer);
            SerializedProperty features = so.FindProperty("m_RendererFeatures"), map = so.FindProperty("m_RendererFeatureMap");
            if (features == null || map == null) return false;
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static string PropName(StyleProp kind)
        {
            switch (kind)
            {
                case StyleProp.FirePit: return "foyer";
                case StyleProp.Hammer: return "marteau";
                case StyleProp.Crate: return "caisse";
                case StyleProp.Barrel: return "tonneau";
                case StyleProp.Anvil: return "enclume";
                case StyleProp.Bridge: return "ponton";
                case StyleProp.Herb: return "herbe de l'Aube";
                case StyleProp.Mill: return "moulin";
                default: return kind.ToString();
            }
        }

        private static GameObject Load(string path) => path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;

        private static Texture2D Texture2DAt(string path) => path != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(path) : null;

        /// <summary>
        /// Essai réaliste 7 : réglage d'import d'un son pour le téléphone. Une musique est lue en continu depuis le disque (quelques Mo de
        /// moins en mémoire), une ambiance reste compressée en mémoire, un bruitage court est décompressé au chargement (aucun retard).
        /// À appeler entre <c>StartAssetEditing</c> et <c>StopAssetEditing</c>.
        /// </summary>
        private static void AudioImport(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) return;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            // Relecture de l'essai 7 : d'après le dossier (un fichier de « Music » au nom non reconnu reste lu en continu, pas décompressé).
            AudioClipLoadType load = StyleRules.InMusicFolder(path) ? AudioClipLoadType.Streaming
                : StyleRules.InAmbienceFolder(path) ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            // Un réglage propre au téléphone (Android) masquerait celui-ci : il est retiré.
            bool android = importer.ContainsSampleSettingsOverride("Android");
            if (android) importer.ClearSampleSettingOverride("Android");
            if (!android && settings.loadType == load && settings.compressionFormat == AudioCompressionFormat.Vorbis) return;
            settings.loadType = load;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = load == AudioClipLoadType.DecompressOnLoad ? 0.7f : 0.5f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        private static AudioClip Sound(List<string> sounds, StyleSound sound) =>
            StyleRules.PickSound(sounds, sound) is string path ? AssetDatabase.LoadAssetAtPath<AudioClip>(path) : null;

        private static string Name(string path) => path != null ? System.IO.Path.GetFileName(path) : "aucun (forme simple)";
    }
}
