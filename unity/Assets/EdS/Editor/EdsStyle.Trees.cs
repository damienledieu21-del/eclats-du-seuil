using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdS.Runtime;
using UnityEditor;
using UnityEngine;

namespace EdS.Game.Editor
{
    /// <summary>
    /// V2.25.4 (arbres de l'atelier) : préparation des arbres de « Assets/EdS/Modeles/Arbres/&lt;essence&gt;/ » (FBX de jeu : LOD1, LOD2,
    /// quad d'imposteur, collision, feuilles au sol ; textures dans « textures ») : réglages d'import des textures (relief, transparence,
    /// tailles réduites sur téléphone), matières « Generated/&lt;essence&gt;_&lt;rôle&gt;.mat » au shader « EdS/Feuillage » (vent léger,
    /// alpha découpé, deux faces, couleur de sommet × 1,1, lumière à travers les feuilles), remap des matières des FBX par leur nom, et
    /// le catalogue (<see cref="StyleCatalog.AtelierTrees"/>, <see cref="StyleCatalog.Millenaire"/>).
    /// </summary>
    public static partial class EdsStyle
    {
        /// <summary>Taille d'import sur téléphone : écorces et reliefs, atlas de feuillage, imposteurs.</summary>
        public const int PhoneBark = 512, PhoneLeaves = 1024, PhoneImpostor = 512;

        private static void OwnTrees(StyleCatalog catalog, List<string> notes)
        {
            catalog.AtelierTrees = new TreeLook[0];
            catalog.Millenaire = null;
            if (!AssetDatabase.IsValidFolder(AtelierTrees.Folder)) return;
            Shader leaves = Shader.Find("EdS/Feuillage");
            if (leaves == null || !leaves.isSupported)
            {
                notes.Add("Arbres de l'atelier : shader EdS/Feuillage absent, arbres téléchargés gardés.");
                return;
            }
            var looks = new List<TreeLook>();
            foreach (TreeEssence essence in AtelierTrees.Essences)
            {
                TreeLook look = OwnTree(AtelierTrees.Name(essence), leaves, notes);
                if (look != null) looks.Add(look);
            }
            catalog.AtelierTrees = looks.ToArray();
            catalog.Millenaire = OwnTree(AtelierTrees.Millenaire, leaves, notes);
            if (looks.Count > 0)
                notes.Add("Arbres de l'atelier : " + string.Join(", ", looks.Select(l => l.Essence + " (" + l.Models.Length + ")"))
                    + (catalog.Millenaire != null ? " ; arbre millénaire livré, pas posé (grande carte)." : "."));
        }

        /// <summary>Une essence (ou l'arbre millénaire) : textures, matières, remap, modèles et imposteurs ; null sans modèle.</summary>
        private static TreeLook OwnTree(string essence, Shader leaves, List<string> notes)
        {
            string folder = AtelierTrees.Folder + "/" + essence;
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            List<string> models = (AssetDatabase.FindAssets("t:Model", new[] { folder }) ?? new string[0]).Select(AssetDatabase.GUIDToAssetPath)
                .Distinct().OrderBy(p => p, System.StringComparer.Ordinal).ToList();
            if (models.Count == 0) return null;
            List<string> maps = (AssetDatabase.FindAssets("t:Texture2D", new[] { folder }) ?? new string[0]).Select(AssetDatabase.GUIDToAssetPath)
                .Distinct().ToList();
            Texture2D Map(string name)
            {
                string path = maps.FirstOrDefault(p => string.Equals(Path.GetFileNameWithoutExtension(p), name, System.StringComparison.OrdinalIgnoreCase));
                if (path == null) return null;
                TreeTexture(path, name);
                return Texture2DAt(path);
            }
            var look = new TreeLook { Essence = essence };
            for (int s = 0; s < 4; s++)
            {
                look.Leaves[s] = Map(essence + "_feuillage_" + AtelierTrees.SeasonWords[s] + "_couleur");
                look.LeafNormals[s] = Map(essence + "_feuillage_" + AtelierTrees.SeasonWords[s] + "_normale");
            }
            Texture2D bark = Map(essence + "_ecorce_couleur"), barkNormal = Map(essence + "_ecorce_normale");
            Texture2D bark2 = Map(essence + "_ecorce2_couleur") ?? bark, bark2Normal = Map(essence + "_ecorce2_normale") ?? barkNormal;
            float wind = (float)AtelierTrees.Wind;
            var materials = new Dictionary<string, Material>
            {
                [essence + "_Ecorce"] = TreeMaterial(essence + "_Ecorce", leaves, bark, barkNormal, Map(essence + "_ecorce_emission"), 0f, wind, false, Color.white),
                [essence + "_Ecorce2"] = TreeMaterial(essence + "_Ecorce2", leaves, bark2, bark2Normal, Map(essence + "_ecorce2_emission"), 0f, wind, false, Color.white),
                [essence + "_Cavite"] = TreeMaterial(essence + "_Cavite", leaves, bark, barkNormal, null, 0f, wind, false, new Color(0.45f, 0.42f, 0.4f)),
                [essence + "_Feuillage"] = TreeMaterial(essence + "_Feuillage", leaves, look.Leaves[AtelierTrees.DefaultSeason],
                    look.LeafNormals[AtelierTrees.DefaultSeason], null, (float)AtelierTrees.Cutoff, wind, true, Color.white),
                [essence + "_FeuillesSol"] = TreeMaterial(essence + "_FeuillesSol", leaves, look.Leaves[2], look.LeafNormals[2], null,
                    (float)AtelierTrees.Cutoff, 0f, true, Color.white),
                [essence + "_Imposteur"] = TreeMaterial(essence + "_Imposteur", leaves, null, null, null, (float)AtelierTrees.Cutoff, 0f, false, Color.white),
            };
            materials[essence + "_Imposteur"].SetFloat("_Translucency", 0.2f);
            var found = new List<GameObject>();
            var impostors = new List<Texture2D>();
            foreach (string path in models)
            {
                RemapTree(path, materials, notes);
                GameObject model = Load(path);
                if (model == null) continue;
                found.Add(model);
                impostors.Add(Map(Path.GetFileNameWithoutExtension(path) + "_imposteur"));
            }
            // Ordre des âges (jeune, adulte a à d, vieux) ; un modèle inconnu va à la fin.
            int Rank(GameObject m)
            {
                for (int i = 0; i < AtelierTrees.Ages.Count; i++)
                    if (m.name == essence + "_" + AtelierTrees.Ages[i]) return i;
                return AtelierTrees.Ages.Count;
            }
            List<int> order = Enumerable.Range(0, found.Count).OrderBy(i => Rank(found[i])).ThenBy(i => found[i].name, System.StringComparer.Ordinal).ToList();
            look.Models = order.Select(i => found[i]).ToArray();
            look.Impostors = order.Select(i => impostors[i]).ToArray();
            if (look.Impostors.Any(t => t == null)) notes.Add("Arbres de l'atelier : imposteur manquant pour " + essence + ".");
            return look.Models.Length > 0 ? look : null;
        }

        /// <summary>
        /// Import d'une texture d'arbre : relief en Normal map ; atlas de feuillage et imposteurs avec transparence (couverture gardée dans
        /// les mipmaps, imposteurs non répétés) ; taille réduite sur téléphone (Android et iOS). Ne réimporte que si un réglage change.
        /// </summary>
        private static void TreeTexture(string path, string name)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            bool changed = false;
            bool normal = name.EndsWith("_normale"), impostor = name.EndsWith("_imposteur"), alpha = impostor || name.Contains("_feuillage_") && !normal;
            if (normal && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                changed = true;
            }
            if (alpha && (!importer.alphaIsTransparency || !importer.mipMapsPreserveCoverage))
            {
                importer.alphaIsTransparency = true;
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = (float)AtelierTrees.Cutoff;
                changed = true;
            }
            if (impostor && importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                changed = true;
            }
            int phone = impostor ? PhoneImpostor : alpha ? PhoneLeaves : PhoneBark;
            foreach (string platform in new[] { "Android", "iPhone" })
            {
                TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
                if (settings == null || settings.overridden && settings.maxTextureSize == phone) continue;
                settings.overridden = true;
                settings.maxTextureSize = phone;
                importer.SetPlatformTextureSettings(settings);
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }

        /// <summary>Matière « Generated/&lt;nom&gt;.mat » au shader du feuillage (créée, ou mise à jour si elle existe).</summary>
        private static Material TreeMaterial(string name, Shader leaves, Texture2D baseMap, Texture2D normal, Texture2D emission, float cutoff,
            float wind, bool foliage, Color tint)
        {
            string path = Generated + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(leaves);
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.shader != leaves) m.shader = leaves;
            m.SetTexture("_BaseMap", baseMap);
            m.SetColor("_BaseColor", tint);
            if (normal != null) m.SetTexture("_BumpMap", normal);
            m.SetFloat("_Cutoff", cutoff);
            m.SetFloat("_Wind", wind);
            m.SetFloat("_Flutter", foliage ? 1f : 0f);
            m.SetFloat("_Translucency", foliage ? 0.6f : 0f);
            m.SetFloat("_VertexTint", foliage ? 1f : 0f);   // couleur de sommet × 1,1 (variation de teinte des cartes)
            m.SetFloat("_FaceCull", cutoff > 0f ? 0f : 2f);   // feuilles et imposteurs vus des deux côtés, écorce de face
            m.SetFloat("_Smoothness", 0.15f);
            if (emission != null)
            {
                m.SetTexture("_GlowMap", emission);
                m.SetColor("_GlowColor", Color.white * (float)AtelierDecor.SeuilGlowDay);
            }
            m.enableInstancing = true;
            m.renderQueue = cutoff > 0f ? 2450 : 2000;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Remap des matières d'un FBX d'arbre vers les matières préparées du même nom (« chene_Feuillage »), puis réimport.</summary>
        private static void RemapTree(string modelPath, Dictionary<string, Material> materials, List<string> notes, bool again = true)
        {
            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer)) return;
            bool changed = false;
            foreach (Material m in (AssetDatabase.LoadAllAssetsAtPath(modelPath) ?? new Object[0]).OfType<Material>()
                .Where(m => AssetDatabase.GetAssetPath(m) == modelPath).ToList())
            {
                if (!materials.TryGetValue(m.name, out Material target))
                {
                    notes.Add("Arbres de l'atelier : matière inconnue « " + m.name + " » dans " + Path.GetFileName(modelPath) + ".");
                    continue;
                }
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(m), target);
                changed = true;
            }
            // Relance après un « Generated » vidé : un remap vers une matière disparue est retiré, le FBX réimporté (ses matières
            // reviennent), puis remappé une fois de plus.
            bool stale = false;
            var map = importer.GetExternalObjectMap();
            if (map != null)
                foreach (var pair in map.ToList())
                    if (pair.Value == null)
                    {
                        importer.RemoveRemap(pair.Key);
                        stale = true;
                    }
            if (changed || stale) importer.SaveAndReimport();
            if (stale && again) RemapTree(modelPath, materials, notes, false);
        }
    }
}
