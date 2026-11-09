using System;
using System.IO;
using System.Linq;
using EdS.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EdS.Game.Editor
{
    /// <summary>
    /// V1.10.1 : deux outils d'essai.
    /// <list type="bullet">
    /// <item>« Sauvegarder le dossier des ressources » : copie <c>Assets/Style</c> (packs, Poly Haven, Mixamo, Sketchfab : ce que le projet
    /// utilise, non suivi par git) dans un zip horodaté <b>hors du projet</b> (Documents/EdS-Sauvegardes). Ne supprime et n'écrase jamais
    /// rien ; si le zip échoue, le fichier incomplet est laissé tel quel, renommé « .incomplet », pour que Damien le supprime lui-même.</item>
    /// <item>« Préparer et lancer l'essai » : prépare l'essai de style, ouvre la scène de démarrage puis lance le mode Jeu. Relançable :
    /// jeu déjà lancé = simple message ; scène modifiée = question habituelle de Unity, annulable.</item>
    /// </list>
    /// </summary>
    public static class EdsEssaiTools
    {
        private const string PlayFlag = "EdS.EssaiLancerLeJeu";
        private const string BackupFolderName = "EdS-Sauvegardes";

        [InitializeOnLoadMethod]
        private static void AfterReload() => StartPlayIfAsked();

        private static void StartPlayIfAsked()
        {
            if (!SessionState.GetBool(PlayFlag, false)) return;
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool(PlayFlag, false)) return;
                SessionState.SetBool(PlayFlag, false);   // une seule fois
                if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = true;
            };
        }

        [MenuItem("Éclats du Seuil/Sauvegarder le dossier des ressources")]
        private static void BackupFromMenu()
        {
            string message;
            bool ok = Backup(out message);
            Debug.Log("Éclats du Seuil : " + message);
            EditorUtility.DisplayDialog("Sauvegarde des ressources", message, "OK");
            if (!ok) Debug.LogWarning("Éclats du Seuil : sauvegarde des ressources en échec. " + message);
        }

        /// <summary>Zip horodaté de <c>Assets/Style</c> hors du projet (logique testée : <see cref="EssaiTools.Backup"/>).</summary>
        internal static bool Backup(out string message)
        {
            try
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(documents))
                {
                    message = "Échec : impossible de trouver ton dossier Documents. Rien n'a été copié ni supprimé.";
                    return false;
                }
                BackupResult result = EssaiTools.Backup(Path.Combine(Application.dataPath, "Style"), Path.Combine(documents, BackupFolderName), DateTime.Now,
                    (done, total) => EditorUtility.DisplayProgressBar("Sauvegarde des ressources", done + " / " + total + " fichiers", total == 0 ? 1f : (float)done / total));
                message = result.Message;
                return result.Succeeded;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Éclats du Seuil/Préparer et lancer l'essai")]
        private static void PrepareAndPlay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Essai", "Le jeu tourne déjà (ou démarre). Arrête-le avec le bouton Play, puis relance ce menu.", "OK");
                return;
            }
            // 0. Sous-lot V2.9.3 : vérification de la base (ce qui peut bloquer) ; un problème bloquant ouvre le rapport et demande s'il faut
            // lancer quand même. Le reste (scène, ressources) est vérifié une fois la préparation faite.
            EssaiReport check = EdsVerification.RunBase();
            if (check.Overall == EssaiLevel.Blocking) EdsVerificationWindow.Open(check);
            if (check.Overall == EssaiLevel.Blocking && !EditorUtility.DisplayDialog("Essai", "La vérification a trouvé un problème bloquant :\n\n"
                    + string.Join("\n", check.Lines.Where(l => l.Level == EssaiLevel.Blocking).Select(l => "• " + l.Label + " : " + l.Fix))
                    + "\n\nLe détail est dans la fenêtre « Vérification de l'essai ».", "Lancer quand même", "Arrêter"))
            {
                Debug.Log("Éclats du Seuil : lancement arrêté après la vérification. Rien n'a été fait.");
                return;
            }
            // 1. La scène de démarrage existe et est dans la liste de construction (sans effet si c'est déjà fait).
            if (!EnsureStartupScene()) return;
            // 2. Préparation de l'essai de style (relançable, rapport dans la Console).
            if (!EdsStyle.Prepare(false)) return;
            AssetDatabase.SaveAssets();
            // La suite du rapport (scène, catalogue, ressources) une fois la préparation faite, en reprenant la base : pas de seconde passe complète.
            EdsVerificationWindow.ShowIfNeeded(EdsVerification.RunAfter(check));
            check = null;   // V2.12.9 : seules des chaînes sont gardées, mais rien ne reste en main pendant le mode Jeu
            // V2.12.9 : libère la mémoire avant le mode Jeu (PC modeste : la préparation et la vérification ont chargé beaucoup de modèles).
            ReleaseMemory();
            OpenSceneAndPlay(true);
        }

        /// <summary>V2.12.9 : libère les ressources inutilisées avant le mode Jeu (« System out of memory » sur un PC de 4 Go).</summary>
        private static void ReleaseMemory()
        {
            EditorUtility.UnloadUnusedAssetsImmediate();
            System.GC.Collect();
        }

        /// <summary>Étape 1 : la scène de démarrage existe (false : arrêt, message déjà donné).</summary>
        private static bool EnsureStartupScene()
        {
            StartupSceneState state = EdsStartupScene.Ensure(false);
            LaunchStep step = EssaiTools.AfterStartupScene(state, AssetDatabase.LoadAssetAtPath<SceneAsset>(EdsStartupScene.ScenePath) != null);
            if (step == LaunchStep.StopSilently)
            {
                Debug.Log("Éclats du Seuil : lancement annulé (la scène ouverte est gardée). Rien d'autre n'a été fait.");
                return false;
            }
            if (step == LaunchStep.StopWithMessage)
            {
                EditorUtility.DisplayDialog("Essai", "La scène de démarrage (" + EdsStartupScene.ScenePath + ") n'a pas pu être préparée : regarde la Console, puis relance.", "OK");
                return false;
            }
            return true;
        }

        /// <summary>Étapes 3 et 4 : ouvre la scène de démarrage puis lance le mode Jeu.</summary>
        private static void OpenSceneAndPlay(bool prepared)
        {
            // 3. Ouvre la bonne scène ; une scène modifiée déclenche la question de Unity (annuler arrête tout, rien n'est perdu).
            if (EssaiTools.ShouldOpenStartupSceneAlone(SceneManager.GetActiveScene().path, SceneManager.sceneCount, EdsStartupScene.ScenePath))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.Log("Éclats du Seuil : lancement annulé (la scène ouverte est gardée)." + (prepared ? " La préparation, elle, est faite." : " Rien d'autre n'a été fait."));
                    return;
                }
                EditorSceneManager.OpenScene(EdsStartupScene.ScenePath);
            }
            // 4. Mode Jeu. Le drapeau survit à un rechargement de domaine (la préparation peut recompiler) : au chargement, il relance le mode Jeu une fois.
            SessionState.SetBool(PlayFlag, true);
            StartPlayIfAsked();
        }

        [MenuItem("Éclats du Seuil/Lancer l'essai (déjà préparé)")]
        private static void PlayPrepared()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Essai", "Le jeu tourne déjà (ou démarre). Arrête-le avec le bouton Play, puis relance ce menu.", "OK");
                return;
            }
            // V2.12.9 : ni préparation ni vérification complète (PC modeste) ; seul le catalogue de style doit exister.
            if (AssetDatabase.LoadAssetAtPath<StyleCatalog>(EdsStyle.CatalogPath) == null)
            {
                EditorUtility.DisplayDialog("Essai", "L'essai n'a pas encore été préparé (catalogue de style absent) : fais d'abord « Éclats du Seuil → Préparer l'essai de style ».", "OK");
                return;
            }
            if (!EnsureStartupScene()) return;
            ReleaseMemory();
            OpenSceneAndPlay(false);
        }
    }
}
