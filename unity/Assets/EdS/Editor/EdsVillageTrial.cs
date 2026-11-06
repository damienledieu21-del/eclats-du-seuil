using System.IO;
using EdS.Runtime;
using UnityEditor;
using UnityEngine;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Sous-lot V2.3 (villageois qui suivent leur journée) : pour l'essai de la journée du village, avance la partie jusqu'à un moment
    /// marquant de la journée (Firmin au fournil à l'aube, le marché, midi, la taverne le soir, la nuit), ou jusqu'à la prochaine averse
    /// (les villageois s'abritent). À lancer hors du mode Jeu (la partie en cours réécrirait sa sauvegarde). Seul le temps du monde avance ;
    /// une copie de la sauvegarde est gardée avant l'essai.
    /// </summary>
    public static class EdsVillageTrial
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "sauvegardes", "vallons.json");

        [MenuItem("Éclats du Seuil/Essai de la journée du village : 5 h 30, l'aube (Firmin au fournil)")]
        private static void Dawn() => Move(5 * 60 + 25);

        [MenuItem("Éclats du Seuil/Essai de la journée du village : 9 h 15, le marché")]
        private static void Market() => Move(9 * 60 + 10);

        [MenuItem("Éclats du Seuil/Essai de la journée du village : 12 h 15, midi")]
        private static void Noon() => Move(12 * 60 + 10);

        [MenuItem("Éclats du Seuil/Essai de la journée du village : 14 h 30, l'après-midi")]
        private static void Afternoon() => Move(14 * 60 + 25);

        [MenuItem("Éclats du Seuil/Essai de la journée du village : 19 h 45, la taverne")]
        private static void Tavern() => Move(19 * 60 + 40);

        [MenuItem("Éclats du Seuil/Essai de la journée du village : 22 h 15, chacun rentre")]
        private static void Night() => Move(22 * 60 + 10);

        [MenuItem("Éclats du Seuil/Essai de la journée du village : la prochaine averse (on s'abrite)")]
        private static void Shower()
        {
            if (Busy()) return;
            EditorUtility.DisplayDialog("Essai de la journée du village", WeatherTrial.MoveSave(SavePath, "shower", System.DateTime.Now.ToString("yyyyMMdd-HHmmss")), "OK");
        }

        private static void Move(int minute)
        {
            if (Busy()) return;
            EditorUtility.DisplayDialog("Essai de la journée du village",
                DayTrial.MoveSave(SavePath, minute, stamp: System.DateTime.Now.ToString("yyyyMMdd-HHmmss")) + "\n\n" + VillageTrial.Guide(minute), "OK");
        }

        private static bool Busy()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return false;
            EditorUtility.DisplayDialog("Essai de la journée du village", "Arrête d'abord le mode Jeu : la partie en cours réécrirait sa sauvegarde.", "OK");
            return true;
        }
    }
}
