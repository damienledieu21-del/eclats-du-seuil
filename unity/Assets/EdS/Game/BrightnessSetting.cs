using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.13 (essai de Damien du 9 novembre 2026 : « le jeu est trop sombre ») : curseur de luminosité des réglages de la pause
    /// (50 % à 200 %, <see cref="BrightnessRules"/>), gardé dans les préférences du joueur comme les autres réglages et appliqué en direct :
    /// <see cref="StyleWorld"/> ajoute <see cref="ExposureOffset"/> à l'exposition du post-traitement, dehors comme dans les pièces.
    /// Affichage seulement.
    /// </summary>
    public static class BrightnessSetting
    {
        private const string Key = "eds.brightness";
        private static int _percent = -1;

        /// <summary>Luminosité choisie (pourcentage ; 100 par défaut, lue une fois des préférences).</summary>
        public static int Percent
        {
            get
            {
                if (_percent < 0) _percent = BrightnessRules.Clamp(PlayerPrefs.GetInt(Key, BrightnessRules.DefaultPercent));
                return _percent;
            }
        }

        /// <summary>Décalage d'exposition (en vues) du choix du joueur.</summary>
        public static float ExposureOffset => (float)BrightnessRules.ExposureOffset(Percent);

        /// <summary>Un pas de plus (+1) ou de moins (-1) : sauvegardé et appliqué tout de suite.</summary>
        public static void Step(int direction)
        {
            _percent = BrightnessRules.Step(Percent, direction);
            PlayerPrefs.SetInt(Key, _percent);
            PlayerPrefs.Save();
            StyleWorld.Refresh();
        }

        /// <summary>Texte du réglage.</summary>
        public static string Label => BrightnessRules.Label(Percent);
    }
}
