using System.IO;
using EdS.Runtime;
using UnityEditor;
using UnityEngine;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Sous-lot V2.6 (« le Seuil qui grandit ») : pour l'essai des cicatrices, crée une cicatrice dans la sauvegarde (aux ruines, au lac ou
    /// dans le pré, au palier 1 ou 3), avance les jours du monde (elle grandit d'un palier tous les deux jours), ou efface toutes les
    /// cicatrices ; sous-lot V2.6.1 : propager une cicatrice au lieu voisin, lancer la quête de Sœur Albe. À lancer hors du mode Jeu (la partie en cours réécrirait sa sauvegarde). Une copie de la sauvegarde est gardée.
    /// </summary>
    public static class EdsScarTrial
    {
        private const string Title = "Essai du Seuil qui grandit";

        private static string SavePath => Path.Combine(Application.persistentDataPath, "sauvegardes", "vallons.json");

        private static string Stamp => System.DateTime.Now.ToString("yyyyMMdd-HHmmss");

        private static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        [MenuItem("Éclats du Seuil/Essai du Seuil qui grandit : une cicatrice aux ruines (palier 1)")]
        private static void Ruins() => Create("ruines", 1);

        [MenuItem("Éclats du Seuil/Essai du Seuil qui grandit : une cicatrice aux ruines (palier 3, échos rôdeurs)")]
        private static void RuinsDeep() => Create("ruines", 3);

        [MenuItem("Éclats du Seuil/Essai du Seuil qui grandit : une cicatrice au lac (palier 1)")]
        private static void Lake() => Create("lac", 1);

        [MenuItem("Éclats du Seuil/Essai du Seuil qui grandit : une cicatrice dans le pré (palier 2)")]
        private static void Meadow() => Create("pre", 2);

        [MenuItem("Éclats du Seuil/Essai du Seuil qui grandit : avancer de 2 jours du monde")]
        private static void TwoDays() => Days(2);

        [MenuItem("Éclats du Seuil/Essai du Seuil qui grandit : avancer de 6 jours du monde")]
        private static void SixDays() => Days(6);

        // Sous-lot V2.6.1 (« suites du Seuil ») : propagation au lieu voisin, quête de Sœur Albe.
        [MenuItem("Éclats du Seuil/Essai des suites du Seuil : propager une cicatrice (des ruines vers le lac)")]
        private static void Spread() =>
            Run(() => ScarTrial.Propagate(SavePath, Path.Combine(RepoRoot, "data", "v0"), Path.Combine(RepoRoot, "scenarios"), Stamp));

        [MenuItem("Éclats du Seuil/Essai des suites du Seuil : lancer la quête de Sœur Albe (une cicatrice, deux éclats)")]
        private static void Quest() =>
            Run(() => ScarTrial.StartQuest(SavePath, Path.Combine(RepoRoot, "data", "v0"), Path.Combine(RepoRoot, "scenarios"), Stamp));

        [MenuItem("Éclats du Seuil/Essai du Seuil qui grandit : effacer toutes les cicatrices")]
        private static void Clear() => Run(() => ScarTrial.Clear(SavePath, Stamp));

        private static void Create(string group, int stage) =>
            Run(() => ScarTrial.Create(SavePath, Path.Combine(RepoRoot, "data", "v0"), Path.Combine(RepoRoot, "scenarios"), group, stage, Stamp));

        private static void Days(int days) => Run(() => ScarTrial.AdvanceDays(SavePath, days, 50, Stamp));

        private static void Run(System.Func<string> action)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(Title, "Arrête d'abord le mode Jeu : la partie en cours réécrirait sa sauvegarde.", "OK");
                return;
            }
            string message;
            try
            {
                message = action();
            }
            catch (System.Exception e)
            {
                message = "Données illisibles : " + e.Message;
            }
            EditorUtility.DisplayDialog(Title, message, "OK");
        }
    }
}
