using System.IO;
using EdS.Runtime;
using UnityEditor;
using UnityEngine;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Sous-lot V2.3.1 (PNJ qui changent de lieu) : pour l'essai, avance la partie jusqu'à quelques minutes avant un départ marquant (Rosalie
    /// qui va prier à la chapelle, le puits et la salle de l'auberge à midi, Mathurin qui sort fumer et Frère Anselme en tournée, la salle
    /// qui se remplit le soir), pour voir le PNJ marcher jusqu'à la porte, disparaître, puis apparaître de l'autre côté. À lancer hors du
    /// mode Jeu (la partie en cours réécrirait sa sauvegarde). Seul le temps du monde avance ; une copie de la sauvegarde est gardée.
    /// </summary>
    public static class EdsRoamingTrial
    {
        private const string Title = "Essai des PNJ qui changent de lieu";

        private static string SavePath => Path.Combine(Application.persistentDataPath, "sauvegardes", "vallons.json");

        [MenuItem("Éclats du Seuil/Essai des PNJ qui changent de lieu : 6 h 25, Rosalie va prier à la chapelle")]
        private static void Chapel() => Move(6 * 60 + 25);

        [MenuItem("Éclats du Seuil/Essai des PNJ qui changent de lieu : 11 h 55, le puits et la salle à midi")]
        private static void Noon() => Move(11 * 60 + 55);

        [MenuItem("Éclats du Seuil/Essai des PNJ qui changent de lieu : 15 h 25, la tournée de Frère Anselme")]
        private static void Round() => Move(15 * 60 + 25);

        [MenuItem("Éclats du Seuil/Essai des PNJ qui changent de lieu : 15 h 55, Mathurin sort fumer")]
        private static void Pipe() => Move(15 * 60 + 55);

        [MenuItem("Éclats du Seuil/Essai des PNJ qui changent de lieu : 18 h 25, la salle de l'auberge se remplit")]
        private static void Evening() => Move(18 * 60 + 25);

        private static void Move(int minute)
        {
            if (Busy()) return;
            EditorUtility.DisplayDialog(Title,
                DayTrial.MoveSave(SavePath, minute, stamp: System.DateTime.Now.ToString("yyyyMMdd-HHmmss")) + "\n\n" + RoamingTrial.Guide(minute + 10), "OK");
        }

        private static bool Busy()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return false;
            EditorUtility.DisplayDialog(Title, "Arrête d'abord le mode Jeu : la partie en cours réécrirait sa sauvegarde.", "OK");
            return true;
        }
    }
}
