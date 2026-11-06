using System.IO;
using EdS.Runtime;
using UnityEditor;
using UnityEngine;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Sous-lot V2.4 (animaux paisibles et bêtes qui se battent) : pour l'essai des animaux, avance la partie jusqu'au moment où l'on voit les
    /// bêtes : l'aube des cerfs, le crépuscule des lièvres, ou le prochain matin de brume (loups gris et échos). À lancer hors du mode Jeu (la
    /// partie en cours réécrirait sa sauvegarde). Seul le temps du monde avance ; une copie de la sauvegarde est gardée.
    /// </summary>
    public static class EdsWildlifeTrial
    {
        private const string Title = "Essai des animaux";

        private static string SavePath => Path.Combine(Application.persistentDataPath, "sauvegardes", "vallons.json");

        private static string Stamp => System.DateTime.Now.ToString("yyyyMMdd-HHmmss");

        [MenuItem("Éclats du Seuil/Essai des animaux : l'aube des cerfs")]
        private static void Deer() => Run(() => WildlifeTrial.MoveToDeer(SavePath, stamp: Stamp));

        [MenuItem("Éclats du Seuil/Essai des animaux : le crépuscule des lièvres")]
        private static void Hares() => Run(() => WildlifeTrial.MoveToHares(SavePath, stamp: Stamp));

        [MenuItem("Éclats du Seuil/Essai des animaux : la brume (loups gris et échos)")]
        private static void Mist() => Run(() => WildlifeTrial.MoveToMist(SavePath, stamp: Stamp));

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
