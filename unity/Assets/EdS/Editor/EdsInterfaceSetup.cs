using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Essai réaliste 6 (interface « Fragments du Seuil » en uGUI) : TextMeshPro, qui donne un texte net à toute taille avec contour et
    /// ombre douce, a besoin de ses « ressources essentielles » (réglages, shaders) dans le projet. Elles sont livrées avec Unity, dans le
    /// paquet <c>com.unity.ugui</c> : à l'ouverture du projet, si elles manquent, l'éditeur les importe sans rien demander (dossier
    /// <c>Assets/TextMesh Pro</c>, non suivi par git). Relançable par le menu « Éclats du Seuil → Préparer l'interface ». Sans elles, le jeu
    /// affiche le texte ordinaire de uGUI (<c>UiFonts.UseTmp</c>).
    /// </summary>
    [InitializeOnLoad]
    public static class EdsInterfaceSetup
    {
        /// <summary>Fichier présent quand les ressources essentielles sont importées.</summary>
        public const string SettingsAsset = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        private const string EssentialsFile = "TMP Essential Resources.unitypackage";

        static EdsInterfaceSetup()
        {
            EditorApplication.delayCall += () => Import(false);
        }

        [MenuItem("Éclats du Seuil/Préparer l'interface (TextMeshPro)")]
        private static void ImportFromMenu() => Import(true);

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static void Import(bool fromMenu)
        {
            if (File.Exists(Path.Combine(ProjectRoot, SettingsAsset)))
            {
                if (fromMenu) Debug.Log("Éclats du Seuil : les ressources de TextMeshPro sont déjà dans le projet.");
                return;
            }
            string package = FindEssentials();
            if (package == null)
            {
                Debug.LogWarning("Éclats du Seuil : ressources de TextMeshPro introuvables (paquet com.unity.ugui) ; l'interface garde le texte ordinaire.");
                return;
            }
            AssetDatabase.ImportPackage(package, false);
            Debug.Log("Éclats du Seuil : ressources de TextMeshPro importées (" + package + ").");
        }

        /// <summary>
        /// Fichier des ressources essentielles dans le paquet de Unity 6 (<c>com.unity.ugui</c>), ou dans l'ancien paquet de TextMeshPro ;
        /// null s'il n'est nulle part.
        /// </summary>
        private static string FindEssentials()
        {
            foreach (string name in new[] { "com.unity.ugui", "com.unity.textmeshpro" })
            {
                string root;
                try
                {
                    root = Path.GetFullPath("Packages/" + name);   // Unity résout le dossier du paquet (cache des paquets)
                }
                catch (System.Exception)
                {
                    continue;
                }
                if (!Directory.Exists(root)) continue;
                string found = Directory.GetFiles(root, EssentialsFile, SearchOption.AllDirectories).OrderBy(f => f, System.StringComparer.Ordinal).FirstOrDefault();
                if (found != null) return found;
            }
            return null;
        }
    }

    /// <summary>
    /// Essai réaliste 6 : réglages d'import des images de l'interface (<c>Resources/EdsInterface/Hud</c> et <c>Peintes</c>) : taille
    /// exacte (pas d'agrandissement à une puissance de deux, qui fausserait les bords des cadres étirés), sans réduction en cascade ni
    /// compression (dégradés et filets nets), bords bloqués.
    /// </summary>
    public sealed class EdsInterfaceTextures : AssetPostprocessor
    {
        /// <summary>Version des réglages : la changer fait réimporter les images.</summary>
        public override uint GetVersion() => 1;

        /// <summary>Image de l'interface « Fragments du Seuil » (chemin du projet, séparateurs quelconques).</summary>
        public static bool IsInterfaceImage(string path)
        {
            string p = (path ?? "").Replace('\\', '/');
            return p.Contains("/Resources/EdsInterface/Hud/") || p.Contains("/Resources/EdsInterface/Peintes/");
        }

        private void OnPreprocessTexture()
        {
            if (!IsInterfaceImage(assetPath) || !(assetImporter is TextureImporter importer)) return;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
        }
    }
}
