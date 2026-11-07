using System.IO;
using EdS.Runtime;
using UnityEditor;
using UnityEngine;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Sous-lot V2.8 (recherche de chemin) : pour l'essai, avance la partie à 11 h 30 (les PNJ quittent leur place du matin et traversent le
    /// bourg) ou à 19 h (passants et clients de la taverne qui vont jusqu'à la porte de l'auberge). À lancer hors du mode Jeu (la partie en
    /// cours réécrirait sa sauvegarde). Seul le temps du monde avance ; une copie de la sauvegarde est gardée.
    /// </summary>
    public static class EdsPathsTrial
    {
        private const string Title = "Essai des chemins";

        private static string SavePath => Path.Combine(Application.persistentDataPath, "sauvegardes", "vallons.json");

        private static string Stamp => System.DateTime.Now.ToString("yyyyMMdd-HHmmss");

        [MenuItem("Éclats du Seuil/Essai des chemins : 11 h 30 (les PNJ traversent le bourg)")]
        private static void Market() => Run(() => PathsTrial.MoveToMarket(SavePath, stamp: Stamp));

        [MenuItem("Éclats du Seuil/Essai des chemins : 19 h (portes de l'auberge, passants)")]
        private static void Evening() => Run(() => PathsTrial.MoveToEvening(SavePath, stamp: Stamp));

        private static void Run(System.Func<string> move)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(Title, "Arrête d'abord le mode Jeu : la partie en cours réécrirait sa sauvegarde.", "OK");
                return;
            }
            EditorUtility.DisplayDialog(Title, move(), "OK");
        }
    }
}
