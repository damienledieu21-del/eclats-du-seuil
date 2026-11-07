using System.IO;
using EdS.Runtime;
using UnityEditor;
using UnityEngine;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Sous-lot V2.5 (événements du monde) : pour l'essai des événements, avance la partie jusqu'à quelques minutes du monde avant la
    /// prochaine attaque du hameau, la prochaine faille du Seuil ou la prochaine chasse de l'aube (on entend l'annonce, puis la cloche). À
    /// lancer hors du mode Jeu (la partie en cours réécrirait sa sauvegarde). Seul le temps du monde avance ; une copie de la sauvegarde est
    /// gardée.
    /// </summary>
    public static class EdsEventTrial
    {
        private const string Title = "Essai des événements";

        private static string SavePath => Path.Combine(Application.persistentDataPath, "sauvegardes", "vallons.json");

        private static string Stamp => System.DateTime.Now.ToString("yyyyMMdd-HHmmss");

        /// <summary>Le dépôt : le parent du projet Unity (données <c>data/v0</c> et <c>scenarios</c>).</summary>
        private static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        [MenuItem("Éclats du Seuil/Essai des événements : l'attaque du hameau maintenant")]
        private static void Raid() => Run("attaque_hameau");

        [MenuItem("Éclats du Seuil/Essai des événements : ouvrir une faille du Seuil")]
        private static void Rift() => Run("faille_du_seuil");

        [MenuItem("Éclats du Seuil/Essai des événements : la chasse de l'aube (loups et sangliers)")]
        private static void Hunt() => Run("chasse_de_l_aube");

        private static void Run(string eventId)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(Title, "Arrête d'abord le mode Jeu : la partie en cours réécrirait sa sauvegarde.", "OK");
                return;
            }
            string message;
            try
            {
                message = EventTrial.MoveSave(SavePath, Path.Combine(RepoRoot, "data", "v0"), Path.Combine(RepoRoot, "scenarios"), eventId, Stamp);
            }
            catch (System.Exception e)
            {
                message = "Données illisibles : " + e.Message;
            }
            EditorUtility.DisplayDialog(Title, message, "OK");
        }
    }
}
