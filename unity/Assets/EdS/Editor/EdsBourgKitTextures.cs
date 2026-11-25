using EdS.Runtime;
using UnityEditor;
using UnityEngine;

namespace EdS.Editor
{
    /// <summary>
    /// Kit du Bourg (25 novembre 2026, relecture de la PR #255) : la texture palette du kit (<see cref="BourgKit.Palette"/>, 32 × 40 px, une
    /// case de 4 px par teinte) est réglée dès son premier import : filtrage point, sans mipmaps (elles mêleraient les teintes voisines au
    /// loin), sans compression, bords bloqués. Plus besoin de « Préparer l'essai de style » pour elle.
    /// </summary>
    public sealed class EdsBourgKitTextures : AssetPostprocessor
    {
        /// <summary>Version des réglages : la changer fait réimporter la palette.</summary>
        public override uint GetVersion() => 1;

        /// <summary>La palette du kit (chemin du projet, séparateurs quelconques).</summary>
        public static bool IsPalette(string path)
        {
            string p = (path ?? "").Replace('\\', '/');
            return p.EndsWith("/" + BourgKit.Palette + ".png");
        }

        private void OnPreprocessTexture()
        {
            if (!IsPalette(assetPath) || !(assetImporter is TextureImporter importer)) return;
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
