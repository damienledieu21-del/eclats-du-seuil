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
    /// créée une fois (fichier généré, non suivi par git) et placée en tête de la liste de construction. Revue Codex de la PR #39 : à la
    /// création, la scène sans nom ouverte est enregistrée sous ce nom, même marquée modifiée (Unity 6 marque ainsi une scène neuve) : rien
    /// n'est perdu, elle est seulement enregistrée. Si <c>Jeu.unity</c> existe déjà, elle est ouverte à la place d'une scène sans nom
    /// intacte ; une scène sans nom modifiée n'est pas fermée (la liste de construction suffit à construire). Relançable par le menu
    /// « Éclats du Seuil → Créer la scène de démarrage ».
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
                bool untitled = string.IsNullOrEmpty(active.path);
                bool blank = untitled && !active.isDirty && SceneManager.sceneCount == 1;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                {
                    if (!AssetDatabase.IsValidFolder("Assets/EdS/Scenes")) AssetDatabase.CreateFolder("Assets/EdS", "Scenes");
                    if (untitled)
                    {
                        // La scène sans nom ouverte devient la scène de démarrage, même marquée modifiée (Unity 6 marque ainsi une scène
                        // neuve) : elle est enregistrée, rien n'est perdu.
                        EditorSceneManager.SaveScene(active, ScenePath);
                        blank = false;
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
