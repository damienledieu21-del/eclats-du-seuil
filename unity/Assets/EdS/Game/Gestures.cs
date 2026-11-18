using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.54 (relecture) : un geste de lancement ou de saut (écrans passagers : logo, écran titre). Seulement le front d'un appui, image
    /// par image : une touche ou un doigt déjà posé avant l'apparition de l'écran ne compte jamais (ni un doigt qui reste posé).
    /// </summary>
    internal static class Gestures
    {
        /// <summary>Une touche, un bouton de souris ou un doigt vient d'être pressé dans cette image.</summary>
        public static bool Pressed()
        {
            if (Input.anyKeyDown || Input.GetMouseButtonDown(0)) return true;
            for (int i = 0; i < Input.touchCount; i++)
                if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
            return false;
        }
    }
}
