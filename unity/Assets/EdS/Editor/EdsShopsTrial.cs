using System.IO;
using EdS.Runtime;
using UnityEditor;
using UnityEngine;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Sous-lot V2.14 (vraies boutiques et grand jour de marché) : pour l'essai, avance la partie au prochain mercredi de marché (6 h 30 :
    /// installation ; 9 h : marché ; 18 h : démontage), au prochain jeudi (place calme), au prochain lundi (jour de repos du fournil) ou au
    /// prochain matin à 5 h (avant la première fournée) et à 6 h (pains chauds). À lancer hors du mode Jeu (la partie en cours réécrirait sa
    /// sauvegarde). Seul le temps du monde avance ; une copie de la sauvegarde est gardée avant l'essai.
    /// </summary>
    public static class EdsShopsTrial
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "sauvegardes", "vallons.json");

        [MenuItem("Éclats du Seuil/Essai des boutiques : mercredi de marché, 6 h 30 (installation)")]
        private static void Setup() => Move(DayTrial.Weekday(2), 6 * 60 + 30);

        [MenuItem("Éclats du Seuil/Essai des boutiques : mercredi de marché, 9 h (marché)")]
        private static void Market() => Move(DayTrial.Weekday(2), 9 * 60);

        [MenuItem("Éclats du Seuil/Essai des boutiques : mercredi de marché, 18 h (démontage)")]
        private static void Teardown() => Move(DayTrial.Weekday(2), 18 * 60);

        [MenuItem("Éclats du Seuil/Essai des boutiques : jeudi, 10 h (place calme)")]
        private static void Calm() => Move(DayTrial.Weekday(3), 10 * 60);

        [MenuItem("Éclats du Seuil/Essai des boutiques : mardi, 5 h (avant la première fournée)")]
        private static void Dawn() => Move(DayTrial.Weekday(1), 5 * 60);

        [MenuItem("Éclats du Seuil/Essai des boutiques : mardi, 6 h (pains chauds)")]
        private static void Warm() => Move(DayTrial.Weekday(1), 6 * 60);

        [MenuItem("Éclats du Seuil/Essai des boutiques : mardi, 19 h 15 (rupture du soir)")]
        private static void SoldOut() => Move(DayTrial.Weekday(1), 19 * 60 + 15);

        [MenuItem("Éclats du Seuil/Essai des boutiques : lundi, 10 h (jour de repos du fournil)")]
        private static void Monday() => Move(DayTrial.Weekday(0), 10 * 60);

        [MenuItem("Éclats du Seuil/Essai des boutiques : dimanche, 10 h (Garin et Bertille fermés)")]
        private static void Sunday() => Move(DayTrial.Weekday(6), 10 * 60);

        private static void Move(System.Func<long, bool> day, int minute)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Essai des boutiques", "Arrête d'abord le mode Jeu : la partie en cours réécrirait sa sauvegarde.", "OK");
                return;
            }
            string stamp = System.DateTime.Now.ToString("yyyyMMdd-HHmmss");
            EditorUtility.DisplayDialog("Essai des boutiques", DayTrial.MoveSaveToDay(SavePath, day, minute, stamp: stamp), "OK");
        }
    }
}
