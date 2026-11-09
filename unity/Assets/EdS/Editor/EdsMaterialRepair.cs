using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdS.Runtime;
using UnityEditor;
using UnityEngine;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Sous-lot V2.12.7 (essai du 9 novembre 2026, PC modeste : four et comptoir du fournil tout blancs, petits objets roses sur la table
    /// voisine) : les modèles posés par le jeu (5e vague, Asset Store, objets, intérieurs, bâtiments du catalogue) gardent parfois les matières
    /// d'origine de leur pack : un shader de l'ancien rendu (« Standard », absent, ou « Hidden/InternalErrorShader ») sort rose avec URP, une
    /// matière sans texture ni couleur sort blanche. Cette passe, lancée à la fin de « Préparer l'essai de style » (règles pures :
    /// <see cref="MaterialRepairRules"/>) :
    /// (a) convertit toute matière hors URP en « Universal Render Pipeline/Lit » en reprenant sa texture, sa couleur, son relief et son mode ;
    /// (b) donne à une matière blanche sans texture la texture de même nom (ou du nom du modèle), sinon une couleur de repli d'après le genre
    /// d'objet (bois, brique, pierre, argile, métal, pain…), jamais du blanc pur ;
    /// (c) remplit une matière absente d'un prefab. Jamais de modification d'un fichier d'origine d'un pack : la matière réparée est toujours une
    /// copie dans <c>Generated/Reparees</c>, posée dans le slot du prefab, ou par remappage d'import (<c>AddRemap</c>) pour un fichier de modèle
    /// (matière interne ou externe). Le blanc ne compte que pour une matière créée à l'import d'un modèle (jamais pour un « .mat » d'URP voulu
    /// blanc). Relançable : une matière réparée n'est plus un défaut. <see cref="Problems"/> sert à la vérification de l'essai. Le comportement est le même en mode PC modeste.
    /// </summary>
    internal static class EdsMaterialRepair
    {
        private const string Folder = EdsStyle.Generated + "/Reparees";

        /// <summary>Un défaut vu par la vérification : fichier, matière, genre.</summary>
        internal sealed class Problem
        {
            public string File;
            public string Material;
            public MaterialFault Fault;
        }

        /// <summary>Ce que l'on sait d'une matière avant de la convertir.</summary>
        private sealed class Snapshot
        {
            public Texture Main;
            public Texture Bump;
            public Color Color = Color.white;
            public float Smoothness = 0.15f;
            public float Metallic;
            public float Mode;
            public float Cutoff = 0.5f;
            public Vector2 Scale = new Vector2(1f, 1f);
            public Vector2 Offset = Vector2.zero;
            public Vector2 BumpScale = new Vector2(1f, 1f);
            public Vector2 BumpOffset = Vector2.zero;
        }

        /// <summary>Chemins des modèles et prefabs que le jeu pose (sans ceux d'un scan, ni les lumineux, ni les personnages du catalogue).</summary>
        internal static List<string> Targets(StyleCatalog catalog)
        {
            var paths = new List<string>();
            void Add(GameObject model)
            {
                if (model == null) return;
                string path = AssetDatabase.GetAssetPath(model);
                if (!string.IsNullOrEmpty(path) && !paths.Contains(path)) paths.Add(path);
            }
            foreach (Wave5Look look in catalog.Wave5 ?? new Wave5Look[0])
            {
                if (look == null || look.Material != null || look.Model == null) continue;
                // Runes, cristaux, portail et feu follet : leur couleur est voulue (émission).
                if ((Wave5.FixesFor(look.Model.name) & Wave5Fix.Emissive) != 0) continue;
                Add(look.Model);
            }
            foreach (PropLook prop in catalog.Props ?? new PropLook[0]) if (prop != null) Add(prop.Model);
            foreach (InteriorLook furniture in catalog.Furniture ?? new InteriorLook[0]) if (furniture != null) Add(furniture.Model);
            foreach (BuildingLook building in catalog.Buildings ?? new BuildingLook[0]) if (building != null) Add(building.Model);
            return paths;
        }

        /// <summary>Répare les matières des modèles posés par le jeu ; rend un résumé pour le rapport de la préparation.</summary>
        internal static string Repair(StyleCatalog catalog, List<string> notes)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null || catalog == null) return "matières : rendu URP absent, rien réparé";
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(EdsStyle.Generated, "Reparees");
            int pink = 0, white = 0, files = 0;
            var textureCache = new Dictionary<string, List<string>>();
            int breathed = 0;
            foreach (string path in Targets(catalog))
            {
                int p = 0, w = 0;
                RepairModel(path, lit, textureCache, notes, ref p, ref w);
                if (p + w > 0) files++;
                pink += p;
                white += w;
                EdsStyle.Breathe(ref breathed);   // compteur gardé d'un modèle à l'autre : libération tous les BreatheEvery modèles
            }
            AssetDatabase.SaveAssets();
            return "matières réparées : " + pink + " roses ou en erreur converties en URP, " + white + " blanches texturées ou colorées (" + files + " fichiers)";
        }

        /// <summary>Les défauts qui restent (pour « Vérification de l'essai ») : un par matière rose ou blanche, avec son nom de fichier.</summary>
        internal static List<Problem> Problems(StyleCatalog catalog)
        {
            var found = new List<Problem>();
            if (catalog == null) return found;
            foreach (string path in Targets(catalog))
            {
                var seen = new HashSet<string>();
                foreach ((Material material, bool skinned) in Materials(path))
                {
                    MaterialFault fault = Check(material, skinned);
                    string name = material != null ? material.name : "(matière absente)";
                    if (fault != MaterialFault.None && seen.Add(name + "|" + fault))
                        found.Add(new Problem { File = Path.GetFileName(path), Material = name, Fault = fault });
                }
            }
            return found;
        }

        private static IEnumerable<(Material, bool)> Materials(string path)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) yield break;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer) continue;
                Material[] shared = renderer.sharedMaterials ?? new Material[0];
                if (shared.Length == 0) yield return (null, false);
                foreach (Material material in shared) yield return (material, renderer is SkinnedMeshRenderer);
            }
        }

        private static Texture MainTexture(Material m) =>
            m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null ? m.GetTexture("_BaseMap")
            : m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;

        private static Color TintOf(Material m) =>
            m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;

        /// <summary>
        /// Le défaut d'une matière : rose (shader hors URP, absent ou en erreur) ou blanche (ni texture, ni couleur). Le blanc ne compte que pour
        /// une matière créée à l'import d'un modèle (celle d'un fichier « .mat » d'URP peut être voulue blanche) et pas pour une peau (yeux, dents).
        /// </summary>
        private static MaterialFault Check(Material m, bool skinned)
        {
            if (m == null) return MaterialFault.Pink;
            string shader = m.shader != null ? m.shader.name : null;
            Color c = TintOf(m);
            MaterialFault fault = MaterialRepairRules.Fault(shader, c.r, c.g, c.b, MainTexture(m) != null);
            if (fault == MaterialFault.White && (skinned || !FromModelImport(m))) return MaterialFault.None;
            return fault;
        }

        /// <summary>La matière vient-elle de l'import d'un fichier de modèle (et non d'un « .mat » du projet) ?</summary>
        private static bool FromModelImport(Material m)
        {
            string owner = AssetDatabase.GetAssetPath(m);
            return !string.IsNullOrEmpty(owner) && AssetImporter.GetAtPath(owner) is ModelImporter;
        }

        private static void RepairModel(string path, Shader lit, Dictionary<string, List<string>> textureCache, List<string> notes, ref int pink, ref int white)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) return;
            string modelName = Path.GetFileNameWithoutExtension(path);
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            bool isPrefab = path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase);
            ModelImporter importer = isPrefab ? null : AssetImporter.GetAtPath(path) as ModelImporter;
            if (!isPrefab && importer == null) return;
            var replaced = new Dictionary<Material, Material>();
            bool remapped = false, changedPrefab = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer) continue;
                bool skinned = renderer is SkinnedMeshRenderer;
                Material[] shared = renderer.sharedMaterials ?? new Material[0];
                bool slotsChanged = false;
                for (int i = 0; i < shared.Length; i++)
                {
                    Material m = shared[i];
                    MaterialFault fault = Check(m, skinned);
                    if (fault == MaterialFault.None) continue;
                    if (m != null && replaced.TryGetValue(m, out Material done))
                    {
                        if (isPrefab)
                        {
                            shared[i] = done;
                            slotsChanged = true;
                        }
                        continue;
                    }
                    // Où la copie sera posée : dans le slot d'un prefab ; pour un fichier de modèle, par remappage d'import de la matière
                    // interne (le fichier) ou externe (celle que la table des matières externes nomme). Sinon rien n'est réparé (ni compté).
                    // Plusieurs identifiants sources peuvent pointer vers la même matière externe : tous sont remappés.
                    var keys = new List<AssetImporter.SourceAssetIdentifier>();
                    bool remap = false;
                    if (!isPrefab)
                    {
                        if (m == null) continue;
                        if (AssetDatabase.GetAssetPath(m) == path)
                        {
                            keys.Add(new AssetImporter.SourceAssetIdentifier(m));
                            remap = true;
                        }
                        else
                            foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, Object> pair in importer.GetExternalObjectMap() ?? new Dictionary<AssetImporter.SourceAssetIdentifier, Object>())
                                if (pair.Value == m)
                                {
                                    keys.Add(pair.Key);
                                    remap = true;
                                }
                        if (!remap) continue;
                    }
                    string name = m != null ? m.name : modelName + " " + i;
                    // Jamais le fichier d'origine : une copie dans Generated/Reparees.
                    // Unique par matière source : l'empreinte du chemin du modèle, de celui de la matière et de son nom (deux dossiers, mêmes noms).
                    string copy = Folder + "/" + Clean(modelName + " " + name) + " "
                        + MaterialRepairRules.ShortHash(path + "|" + (m != null ? AssetDatabase.GetAssetPath(m) : "") + "|" + name) + ".mat";
                    Material target = AssetDatabase.LoadAssetAtPath<Material>(copy);
                    if (target == null)
                    {
                        target = new Material(lit);
                        AssetDatabase.CreateAsset(target, copy);
                    }
                    ConvertAndFill(target, Snap(m), lit, modelName, name, folder, path, textureCache, notes);
                    if (fault == MaterialFault.Pink) pink++;
                    else white++;
                    if (m != null) replaced[m] = target;
                    if (remap)
                    {
                        foreach (AssetImporter.SourceAssetIdentifier key in keys) importer.AddRemap(key, target);
                        remapped = true;
                    }
                    else
                    {
                        shared[i] = target;
                        slotsChanged = true;
                    }
                }
                if (slotsChanged && isPrefab)
                {
                    renderer.sharedMaterials = shared;
                    EditorUtility.SetDirty(renderer);
                    changedPrefab = true;
                }
            }
            if (changedPrefab) EditorUtility.SetDirty(root);
            if (remapped) importer.SaveAndReimport();
            if (pink + white > 0) notes.Add(Path.GetFileName(path) + " : matières réparées (" + pink + " roses, " + white + " blanches).");
        }

        private static string Clean(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }

        private static Snapshot Snap(Material m)
        {
            var s = new Snapshot();
            if (m == null) return s;
            s.Main = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            if (s.Main == null && m.HasProperty("_BaseMap")) s.Main = m.GetTexture("_BaseMap");
            s.Bump = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
            s.Color = TintOf(m);
            if (m.HasProperty("_Glossiness")) s.Smoothness = m.GetFloat("_Glossiness");
            else if (m.HasProperty("_Smoothness")) s.Smoothness = m.GetFloat("_Smoothness");
            if (m.HasProperty("_Metallic")) s.Metallic = m.GetFloat("_Metallic");
            // Standard : « _Mode » ; anciens shaders sans lui (Legacy Shaders/Transparent/…) : le mode se déduit du nom du shader.
            s.Mode = m.HasProperty("_Mode") ? m.GetFloat("_Mode") : MaterialRepairRules.LegacyMode(m.shader != null ? m.shader.name : null);
            // Échelle et décalage de la texture (répétition d'un carrelage, d'un bardage).
            string tiled = m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null ? "_MainTex" : "_BaseMap";
            if (m.HasProperty(tiled))
            {
                s.Scale = m.GetTextureScale(tiled);
                s.Offset = m.GetTextureOffset(tiled);
            }
            if (m.HasProperty("_BumpMap"))
            {
                s.BumpScale = m.GetTextureScale("_BumpMap");
                s.BumpOffset = m.GetTextureOffset("_BumpMap");
            }
            if (m.HasProperty("_Cutoff")) s.Cutoff = m.GetFloat("_Cutoff");
            return s;
        }

        /// <summary>
        /// Met <paramref name="target"/> (une copie neuve ou déjà réparée, jamais un fichier d'un pack) sur URP Lit avec ce que
        /// <paramref name="before"/> savait de la matière d'origine ; sans texture ni couleur, la texture de même nom
        /// (<see cref="MaterialRepairRules.BestTexture"/>) ou la couleur de repli du genre d'objet (l'opacité d'origine est gardée).
        /// </summary>
        private static void ConvertAndFill(Material target, Snapshot before, Shader lit, string modelName, string name, string folder,
            string path, Dictionary<string, List<string>> textureCache, List<string> notes)
        {
            if (target.shader != lit) target.shader = lit;
            target.shaderKeywords = new string[0];
            target.renderQueue = -1;
            Texture main = before.Main;
            Color color = before.Color;
            if (main == null && color.r >= (float)MaterialRepairRules.WhiteFrom && color.g >= (float)MaterialRepairRules.WhiteFrom
                && color.b >= (float)MaterialRepairRules.WhiteFrom)
            {
                List<string> maps = TexturesAround(folder, textureCache);
                string chosen = MaterialRepairRules.BestTexture(name, modelName, maps);
                if (chosen != null)
                {
                    main = AssetDatabase.LoadAssetAtPath<Texture2D>(chosen);
                    string stem = StyleRules.TextureStem(chosen);
                    string normal = maps.FirstOrDefault(t => StyleRules.IsNormalMap(t) && StyleRules.TextureStem(t) == stem);
                    if (normal != null && before.Bump == null) before.Bump = AssetDatabase.LoadAssetAtPath<Texture2D>(normal);
                }
                if (main == null)
                {
                    (double r, double g, double b) = MaterialRepairRules.FallbackColor(name, modelName, Path.GetFileName(folder));
                    color = new Color((float)r, (float)g, (float)b, before.Color.a);   // l'opacité d'origine (verre, voile) est gardée
                    if ((r, g, b) == MaterialRepairRules.MetalColor)
                    {
                        before.Metallic = 0.7f;
                        before.Smoothness = 0.45f;
                    }
                    notes.Add(Path.GetFileName(path) + " : matière « " + name + " » sans texture ni couleur : couleur de repli.");
                }
            }
            target.SetTexture("_BaseMap", main);
            target.SetTexture("_MainTex", main);
            if (before.Main != null)   // une texture trouvée par le nom garde l'échelle d'URP (1, 1)
                foreach (string property in new[] { "_BaseMap", "_MainTex" })
                {
                    target.SetTextureScale(property, before.Scale);
                    target.SetTextureOffset(property, before.Offset);
                }
            target.SetColor("_BaseColor", color);
            target.SetColor("_Color", color);
            target.SetFloat("_Smoothness", Mathf.Clamp01(before.Smoothness));
            target.SetFloat("_Metallic", Mathf.Clamp01(before.Metallic));
            if (before.Bump != null)
            {
                target.SetTexture("_BumpMap", before.Bump);
                target.EnableKeyword("_NORMALMAP");
                target.SetTextureScale("_BumpMap", before.BumpScale);
                target.SetTextureOffset("_BumpMap", before.BumpOffset);
            }
            // Opaque d'abord (une copie déjà réparée peut avoir gardé un autre mode), puis le mode de l'ancien Standard : 1 découpé, 2 et 3 transparent.
            target.SetFloat("_Surface", 0f);
            target.SetFloat("_Blend", 0f);
            target.SetFloat("_SrcBlend", 1f);
            target.SetFloat("_DstBlend", 0f);
            target.SetFloat("_SrcBlendAlpha", 1f);
            target.SetFloat("_DstBlendAlpha", 0f);
            target.SetFloat("_ZWrite", 1f);
            target.SetFloat("_AlphaClip", 0f);
            target.SetFloat("_AlphaToMask", 0f);
            target.SetFloat("_Cull", 2f);
            if (before.Mode > 0.5f && before.Mode < 1.5f)
            {
                target.SetFloat("_AlphaClip", 1f);
                target.SetFloat("_Cutoff", before.Cutoff);
                target.SetFloat("_AlphaToMask", 1f);
                target.EnableKeyword("_ALPHATEST_ON");
                target.SetFloat("_Cull", 0f);
                target.renderQueue = 2450;
            }
            else if (before.Mode > 1.5f)
            {
                // Les mêmes réglages que SpaceView.Transparent (mélange alpha d'URP, sans écriture de profondeur).
                ParticleBlend blend = ParticleRules.Transparent(0);
                target.SetFloat("_Surface", blend.Surface);
                target.SetFloat("_Blend", blend.Blend);
                target.SetFloat("_SrcBlend", blend.Src);
                target.SetFloat("_DstBlend", blend.Dst);
                target.SetFloat("_SrcBlendAlpha", blend.SrcAlpha);
                target.SetFloat("_DstBlendAlpha", blend.DstAlpha);
                target.SetFloat("_ZWrite", 0f);
                target.SetOverrideTag("RenderType", "Transparent");
                target.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                target.renderQueue = blend.Queue;
            }
            EditorUtility.SetDirty(target);
        }

        /// <summary>Textures du dossier du modèle (et de son dossier parent, hors racine : les packs rangent « Textures » à côté de « Prefabs »).</summary>
        private static List<string> TexturesAround(string folder, Dictionary<string, List<string>> cache)
        {
            if (cache.TryGetValue(folder, out List<string> known)) return known;
            var roots = new List<string> { folder };
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && parent != "Assets" && parent != EdsStyle.Root && AssetDatabase.IsValidFolder(parent)) roots.Add(parent);
            List<string> found = AssetDatabase.FindAssets("t:Texture2D", roots.ToArray()).Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .Where(p => !p.StartsWith(EdsStyle.Generated + "/", System.StringComparison.Ordinal)).OrderBy(p => p, System.StringComparer.Ordinal).ToList();
            cache[folder] = found;
            return found;
        }
    }
}
