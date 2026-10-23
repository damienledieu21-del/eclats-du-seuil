using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Sous-lot 11.9 (essai de Damien du 22 octobre 2026 : Unity demandait d'enregistrer une scène « Untitled » avant de construire le jeu) :
    /// scène de démarrage créée à l'ouverture du projet. Le jeu se construit par le code (<c>EdsBootstrap</c>) dans n'importe quelle
    /// scène ; il en faut seulement une, enregistrée et inscrite dans la liste de construction. <c>Assets/EdS/Scenes/Jeu.unity</c> est
    /// créée une fois (fichier généré, non suivi par git), placée en tête de la liste de construction, et ouverte si la scène ouverte est
    /// une scène vide jamais enregistrée. Une scène que l'on a modifiée n'est jamais remplacée. Relançable par le menu « Éclats du Seuil →
    /// Créer la scène de démarrage ».
    /// </summary>
    [InitializeOnLoad]
    public static class EdsStartupScene
    {
        public const string ScenePath = "Assets/EdS/Scenes/Jeu.unity";

        static EdsStartupScene()
        {
            EditorApplication.delayCall += () => Ensure(false);
        }

        [MenuItem("Éclats du Seuil/Créer la scène de démarrage")]
        private static void EnsureFromMenu() => Ensure(true);

        private static void Ensure(bool fromMenu)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
            try
            {
                Scene active = SceneManager.GetActiveScene();
                bool blank = string.IsNullOrEmpty(active.path) && !active.isDirty && SceneManager.sceneCount == 1;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                {
                    if (!AssetDatabase.IsValidFolder("Assets/EdS/Scenes")) AssetDatabase.CreateFolder("Assets/EdS", "Scenes");
                    if (blank)
                    {
                        // La scène vide ouverte devient la scène de démarrage.
                        EditorSceneManager.SaveScene(active, ScenePath);
                        blank = false;
                    }
                    else if (string.IsNullOrEmpty(active.path))
                    {
                        // Scène sans nom déjà modifiée : on n'y touche pas (Unity refuse d'ajouter une scène à côté d'elle).
                        if (fromMenu) Debug.LogWarning("Éclats du Seuil : enregistre d'abord la scène ouverte, puis relance ce menu.");
                        return;
                    }
                    else
                    {
                        Scene created = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
                        EditorSceneManager.SaveScene(created, ScenePath);
                        EditorSceneManager.CloseScene(created, true);
                    }
                    AssetDatabase.Refresh();
                }
                EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
                if (scenes.Length == 0 || scenes[0].path != ScenePath || !scenes[0].enabled)
                    EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                        .Concat(scenes.Where(s => s.path != ScenePath)).ToArray();
                if (blank) EditorSceneManager.OpenScene(ScenePath);
                if (fromMenu) Debug.Log("Éclats du Seuil : scène de démarrage prête (" + ScenePath + ").");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Éclats du Seuil : scène de démarrage impossible à préparer. " + e.Message);
            }
        }
    }
}
