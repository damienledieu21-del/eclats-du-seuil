using UnityEditor;
using UnityEngine;

namespace EdS.Editor
{
    /// <summary>
    /// Sous-lot V2.29.2 : réglages d'import des panoramas peints de l'horizon (<c>Resources/EdsHorizon</c>) : taille maximale 8 192 (le défaut
    /// de 2 048 les écraserait), sans mipmaps ni mise à l'échelle, transparence, bords bloqués, compression normale.
    /// </summary>
    public sealed class EdsHorizonTextures : AssetPostprocessor
    {
        /// <summary>Version des réglages : la changer fait réimporter les images.</summary>
        public override uint GetVersion() => 1;

        /// <summary>Panorama de l'horizon (chemin du projet, séparateurs quelconques).</summary>
        public static bool IsPanorama(string path)
        {
            string p = (path ?? "").Replace('\\', '/');
            return p.Contains("/Resources/EdsHorizon/") && p.EndsWith(".png");
        }

        private void OnPreprocessTexture()
        {
            if (!IsPanorama(assetPath) || !(assetImporter is TextureImporter importer)) return;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 8192;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
        }
    }
}
