using System.IO;
using EdS.Runtime;
using UnityEditor;
using UnityEngine;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Sous-lot V2.10 (fêtes du bourg) : pour l'essai des fêtes, avance la partie jusqu'au prochain jour de marché, aux Semailles,
    /// à la Fête des Moissons ou à la Veillée des Contes (dates provisoires de l'histoire, contenu 0.24.0), ou au lendemain (le stock des
    /// marchands se regagne). À lancer hors du mode Jeu (la partie en cours réécrirait sa sauvegarde). Seul le temps du monde avance ; une
    /// copie de la sauvegarde est gardée avant l'essai.
    /// </summary>
    public static class EdsFestivalTrial
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "sauvegardes", "vallons.json");

        [MenuItem("Éclats du Seuil/Essai des fêtes : jour de marché, 8 h")]
        private static void Market() => Move(DayTrial.Weekday(5), 8 * 60);

        [MenuItem("Éclats du Seuil/Essai des fêtes : Semailles, 9 h")]
        private static void Fair() => Move(DayTrial.SeasonDay(0, 14), 9 * 60);

        [MenuItem("Éclats du Seuil/Essai des fêtes : Fête des Moissons, 10 h 30")]
        private static void Harvest() => Move(DayTrial.SeasonDay(2, 7), 10 * 60 + 30);

        [MenuItem("Éclats du Seuil/Essai des fêtes : Veillée des Contes, 19 h")]
        private static void Vigil() => Move(DayTrial.SeasonDay(3, 21), 19 * 60);

        [MenuItem("Éclats du Seuil/Essai des fêtes : le lendemain, 9 h (stock regagné)")]
        private static void NextDay() => Move(null, 9 * 60);

        private static void Move(System.Func<long, bool> day, int minute)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Essai des fêtes", "Arrête d'abord le mode Jeu : la partie en cours réécrirait sa sauvegarde.", "OK");
                return;
            }
            string stamp = System.DateTime.Now.ToString("yyyyMMdd-HHmmss");
            if (day == null)
            {
                // Le lendemain : au moins une nuit passe (9 h du jour suivant, ou du surlendemain si l'on est avant 9 h).
                EditorUtility.DisplayDialog("Essai des fêtes", DayTrial.MoveSave(SavePath, 23 * 60 + 59, stamp: stamp) + "\n" + DayTrial.MoveSave(SavePath, minute, stamp: stamp), "OK");
                return;
            }
            EditorUtility.DisplayDialog("Essai des fêtes", DayTrial.MoveSaveToDay(SavePath, day, minute, stamp: stamp), "OK");
        }
    }
}
