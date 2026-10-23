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
    /// créée une fois (fichier généré, non suivi par git) et placée en tête de la liste de construction. Revue Codex de la PR #39 :
    /// <list type="bullet">
    /// <item>à la création, la scène sans nom ouverte est enregistrée sous ce nom, même marquée modifiée (Unity 6 marque ainsi une scène
    /// neuve) : rien n'est perdu, elle est seulement enregistrée ;</item>
    /// <item>si <c>Jeu.unity</c> existe déjà, elle remplace une scène sans nom intacte ; une scène sans nom marquée modifiée n'est fermée
    /// qu'après la question habituelle de Unity (enregistrer ou non, annuler la garde ouverte) : aucun travail ne se perd sans
    /// question (3e tour, accord de Damien du 23 octobre 2026) ;</item>
    /// <item>un enregistrement qui échoue (disque plein, dossier en lecture seule) arrête la préparation avec un avertissement : rien n'est
    /// inscrit dans la liste de construction.</item>
    /// </list>
    /// Relançable par le menu « Éclats du Seuil → Créer la scène de démarrage ».
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
                bool replaceUntitled = untitled && SceneManager.sceneCount == 1;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                {
                    if (!AssetDatabase.IsValidFolder("Assets/EdS/Scenes")) AssetDatabase.CreateFolder("Assets/EdS", "Scenes");
                    bool saved;
                    if (untitled)
                    {
                        // La scène sans nom ouverte devient la scène de démarrage, même marquée modifiée : elle est enregistrée, rien
                        // n'est perdu.
                        saved = EditorSceneManager.SaveScene(active, ScenePath);
                        replaceUntitled = false;
                    }
                    else
                    {
                        Scene created = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
                        saved = EditorSceneManager.SaveScene(created, ScenePath);
                        EditorSceneManager.CloseScene(created, true);
                    }
                    if (!saved)
                    {
                        Debug.LogWarning("Éclats du Seuil : impossible d'enregistrer la scène de démarrage (" + ScenePath
                            + ") ; vérifie la place sur le disque et les droits du dossier, puis relance le menu « Éclats du Seuil → Créer la scène de démarrage ».");
                        return;
                    }
                    AssetDatabase.Refresh();
                }
                EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
                if (scenes.Length == 0 || scenes[0].path != ScenePath || !scenes[0].enabled)
                    EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                        .Concat(scenes.Where(s => s.path != ScenePath)).ToArray();
                // Une scène sans nom modifiée n'est fermée qu'après la question de Unity (annulée : elle reste ouverte).
                if (replaceUntitled && (!active.isDirty || EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()))
                    EditorSceneManager.OpenScene(ScenePath);
                if (fromMenu) Debug.Log("Éclats du Seuil : scène de démarrage prête (" + ScenePath + ").");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Éclats du Seuil : scène de démarrage impossible à préparer. " + e.Message);
            }
        }
    }
}
