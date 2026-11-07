using System.IO;
using EdS.Runtime;
using UnityEditor;
using UnityEngine;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Sous-lot V2.4.1 (bêtes qui se promènent) : pour l'essai, avance la partie au matin (cerfs qui broutent en harde, loups qui patrouillent
    /// en file, sangliers qui fouillent) ou au crépuscule (lièvres qui sautillent par à-coups, puis loups de la nuit). À lancer hors du mode
    /// Jeu (la partie en cours réécrirait sa sauvegarde). Seul le temps du monde avance ; une copie de la sauvegarde est gardée.
    /// </summary>
    public static class EdsWanderTrial
    {
        private const string Title = "Essai des bêtes qui se promènent";

        private static string SavePath => Path.Combine(Application.persistentDataPath, "sauvegardes", "vallons.json");

        private static string Stamp => System.DateTime.Now.ToString("yyyyMMdd-HHmmss");

        [MenuItem("Éclats du Seuil/Essai des bêtes qui se promènent : le matin (harde, meute, sangliers)")]
        private static void Morning() => Run(() => WildlifeTrial.MoveToWanderMorning(SavePath, stamp: Stamp));

        [MenuItem("Éclats du Seuil/Essai des bêtes qui se promènent : le crépuscule (lièvres)")]
        private static void Dusk() => Run(() => WildlifeTrial.MoveToWanderDusk(SavePath, stamp: Stamp));

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
