using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Sous-lot 9.3 : configuration du rendu URP à la première ouverture du projet. Si aucun pipeline n'est assigné, crée un asset URP
    /// et son renderer dans <c>Assets/EdS/Settings</c> (fichiers générés, non suivis par git) et l'assigne au projet. En cas d'échec, le
    /// rendu intégré de Unity reste actif : le prototype fonctionne quand même (matériaux « Standard »). Relançable par le menu
    /// « Éclats du Seuil → Configurer le rendu URP ».
    /// </summary>
    [InitializeOnLoad]
    public static class EdsProjectSetup
    {
        private const string Folder = "Assets/EdS/Settings";

        static EdsProjectSetup()
        {
            EditorApplication.delayCall += () => Configure(false);
        }

        [MenuItem("Éclats du Seuil/Configurer le rendu URP")]
        private static void ConfigureFromMenu() => Configure(true);

        private static void Configure(bool fromMenu)
        {
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                if (fromMenu) Debug.Log("Éclats du Seuil : un rendu est déjà configuré (" + GraphicsSettings.defaultRenderPipeline.name + ").");
                return;
            }
            try
            {
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/EdS", "Settings");
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, Folder + "/EdS_Renderer.asset");
                UniversalRenderPipelineAsset pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, Folder + "/EdS_URP.asset");
                GraphicsSettings.defaultRenderPipeline = pipeline;
                QualitySettings.renderPipeline = pipeline;
                AssetDatabase.SaveAssets();
                Debug.Log("Éclats du Seuil : rendu URP configuré (" + Folder + ").");
            }
            catch (Exception e)
            {
                Debug.LogWarning("Éclats du Seuil : configuration URP impossible, le rendu intégré reste actif. " + e.Message);
            }
        }
    }
}
