using System.IO;
using EdS.Runtime;
using UnityEditor;
using UnityEngine;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Sous-lot V2.2 (météo) : pour l'essai de la météo. <b>En mode Jeu</b>, force un temps à l'affichage (pluie, brume, orage…) sans rien
    /// changer à la partie (les répliques des villageois suivent le vrai temps) ; « Temps du monde » rend le temps tiré du jour. <b>Hors du mode
    /// Jeu</b>, avance la sauvegarde jusqu'au prochain moment où il fait ce temps (seul le temps du monde avance, comme pour l'essai de la nuit) :
    /// on voit alors tout, répliques comprises.
    /// </summary>
    public static class EdsWeatherTrial
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "sauvegardes", "vallons.json");

        [MenuItem("Éclats du Seuil/Essai de la météo : pluie fine")]
        private static void Drizzle() => Try("drizzle", "pluie fine");

        [MenuItem("Éclats du Seuil/Essai de la météo : averse")]
        private static void Shower() => Try("shower", "averse");

        [MenuItem("Éclats du Seuil/Essai de la météo : orage")]
        private static void Storm() => Try("storm", "orage");

        [MenuItem("Éclats du Seuil/Essai de la météo : brume du matin")]
        private static void Mist() => Try("mist", "brume du matin");

        [MenuItem("Éclats du Seuil/Essai de la météo : ciel couvert")]
        private static void Cloudy() => Try("cloudy", "ciel couvert");

        [MenuItem("Éclats du Seuil/Essai de la météo : temps clair")]
        private static void Clear() => Try("clear", "temps clair");

        [MenuItem("Éclats du Seuil/Essai de la météo : temps du monde (ne plus forcer)")]
        private static void World()
        {
            StyleWeather.Force(null);
            EditorUtility.DisplayDialog("Essai de la météo", "Le temps suit de nouveau le monde.", "OK");
        }

        private static void Try(string id, string name)
        {
            if (EditorApplication.isPlaying)
            {
                // En mode Jeu : seulement l'affichage (la partie en cours ne change pas).
                StyleWeather.Force(id);
                EditorUtility.DisplayDialog("Essai de la météo", $"Temps forcé à l'affichage : {name}. Menu « temps du monde » pour revenir.", "OK");
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Essai de la météo", "Attends la fin du passage en mode Jeu.", "OK");
                return;
            }
            EditorUtility.DisplayDialog("Essai de la météo", WeatherTrial.MoveSave(SavePath, id), "OK");
        }
    }
}
