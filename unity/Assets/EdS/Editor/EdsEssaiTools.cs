using System;
using System.IO;
using System.IO.Compression;
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
        private const string BackupFolderName = "EdS-Sauvegardes";

        [MenuItem("Éclats du Seuil/Sauvegarder le dossier des ressources")]
        private static void BackupFromMenu()
        {
            string message;
            bool ok = Backup(out message);
            Debug.Log("Éclats du Seuil : " + message);
            EditorUtility.DisplayDialog("Sauvegarde des ressources", message, "OK");
            if (!ok) Debug.LogWarning("Éclats du Seuil : sauvegarde des ressources en échec. " + message);
        }

        /// <summary>Zip horodaté de <c>Assets/Style</c> hors du projet. Renvoie vrai si le zip est complet ; le message dit où ou pourquoi pas.</summary>
        internal static bool Backup(out string message)
        {
            string tmp = null;
            try
            {
                string source = Path.Combine(Application.dataPath, "Style");
                if (!Directory.Exists(source))
                {
                    message = "Échec : le dossier des ressources est introuvable (" + source + "). Rien n'a été copié ni supprimé.";
                    return false;
                }
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(documents))
                {
                    message = "Échec : impossible de trouver ton dossier Documents. Rien n'a été copié ni supprimé.";
                    return false;
                }
                string folder = Path.Combine(documents, BackupFolderName);
                Directory.CreateDirectory(folder);
                string target = Path.Combine(folder, "Style-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".zip");
                if (File.Exists(target))
                {
                    message = "Échec : « " + target + " » existe déjà ; attends une seconde et relance. Rien n'a été écrasé.";
                    return false;
                }
                tmp = target + ".incomplet";
                if (File.Exists(tmp))
                {
                    message = "Échec : « " + tmp + " » existe déjà (sauvegarde interrompue plus tôt) ; supprime-le toi-même puis relance. Rien n'a été écrasé.";
                    tmp = null;
                    return false;
                }
                ZipFile.CreateFromDirectory(source, tmp, System.IO.Compression.CompressionLevel.Fastest, true);
                File.Move(tmp, target);
                tmp = null;
                message = "Réussi : sauvegarde du dossier Assets/Style enregistrée dans\n" + target + "\n(hors du projet ; rien n'a été supprimé).";
                return true;
            }
            catch (Exception e)
            {
                message = "Échec : " + e.Message + (tmp != null ? "\nFichier incomplet laissé en place (tu peux le supprimer toi-même) : " + tmp : "")
                    + "\nVérifie la place libre sur le disque et les droits du dossier Documents. Rien du projet n'a été supprimé.";
                return false;
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
            // 1. La scène de démarrage existe et est dans la liste de construction (sans effet si c'est déjà fait).
            EdsStartupScene.Ensure(false);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EdsStartupScene.ScenePath) == null)
            {
                EditorUtility.DisplayDialog("Essai", "La scène de démarrage (" + EdsStartupScene.ScenePath + ") n'a pas pu être créée : regarde la Console, puis relance.", "OK");
                return;
            }
            // 2. Préparation de l'essai de style (relançable, rapport dans la Console).
            if (!EdsStyle.Prepare(false)) return;
            AssetDatabase.SaveAssets();
            // 3. Ouvre la bonne scène ; une scène modifiée déclenche la question de Unity (annuler arrête tout, rien n'est perdu).
            if (SceneManager.GetActiveScene().path != EdsStartupScene.ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.Log("Éclats du Seuil : lancement annulé (la scène ouverte est gardée). La préparation, elle, est faite.");
                    return;
                }
                EditorSceneManager.OpenScene(EdsStartupScene.ScenePath);
            }
            // 4. Mode Jeu, une fois Unity redevenu disponible.
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = true;
            };
        }
    }
}
