using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace EdS.Game.Editor
{
    /// <summary>
    /// V2.53 (relecture de la PR #265) : le jeu met le rendu du PC modeste (<c>Generated/EdS_URP_Modeste</c>) au réglage de qualité le
    /// temps d'une partie et le rend à la sortie du mode Jeu (<c>StyleWorld.RestoreProjectPipeline</c>). Si l'éditeur a planté ou a été
    /// tué en plein jeu, le rendu léger resterait au projet : à l'ouverture de l'éditeur (hors mode Jeu), un rendu généré au réglage de
    /// qualité est remplacé par le rendu du projet (<see cref="GraphicsSettings.defaultRenderPipeline"/>, ou aucun s'il est lui-même généré).
    /// </summary>
    [InitializeOnLoad]
    public static class EdsRenderRestore
    {
        static EdsRenderRestore()
        {
            EditorApplication.delayCall += () => Restore();
        }

        /// <summary>Remet le rendu du projet si un rendu généré (téléphone, PC modeste) est resté au réglage de qualité. Rend vrai s'il l'a fait.</summary>
        public static bool Restore()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
            RenderPipelineAsset active = QualitySettings.renderPipeline;
            if (active == null || !IsGenerated(active)) return false;
            RenderPipelineAsset project = GraphicsSettings.defaultRenderPipeline;
            QualitySettings.renderPipeline = project != null && !IsGenerated(project) ? project : null;
            Debug.Log("Éclats du Seuil : rendu « " + active.name + " » resté au réglage de qualité (éditeur fermé en plein jeu) ; rendu du projet remis.");
            return true;
        }

        private static bool IsGenerated(Object asset) =>
            AssetDatabase.GetAssetPath(asset).StartsWith(EdsStyle.Generated + "/", System.StringComparison.Ordinal);
    }
}
