using System.IO;
using EdS.Runtime;
using UnityEditor;
using UnityEngine;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Sous-lot V2.1 (jour et nuit qui changent le monde) : pour l'essai de la nuit, avance la partie jusqu'au soir ou jusqu'à l'aube, sans
    /// attendre une heure de jeu. À lancer hors du mode Jeu (la partie en cours réécrirait sa sauvegarde). Seul le temps du monde avance.
    /// </summary>
    public static class EdsNightTrial
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "sauvegardes", "vallons.json");

        [MenuItem("Éclats du Seuil/Essai de la nuit : avancer la partie à 21 h 55")]
        private static void Dusk() => Move(21 * 60 + 55);

        [MenuItem("Éclats du Seuil/Essai de la nuit : avancer la partie à 4 h 55")]
        private static void Dawn() => Move(4 * 60 + 55);

        [MenuItem("Éclats du Seuil/Essai de la nuit : avancer la partie à minuit")]
        private static void Midnight() => Move(0);

        private static void Move(int minute)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Essai de la nuit", "Arrête d'abord le mode Jeu : la partie en cours réécrirait sa sauvegarde.", "OK");
                return;
            }
            EditorUtility.DisplayDialog("Essai de la nuit", DayTrial.MoveSave(SavePath, minute), "OK");
        }
    }
}
