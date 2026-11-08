using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.9.2 : fait avancer les fondus d'un corps humain (<see cref="HumanMotion"/>) à chaque image et détruit son graphe
    /// d'animation avec lui.
    /// </summary>
    public sealed class HumanMotionDriver : MonoBehaviour
    {
        internal HumanMotion Motion;

        private void Update()
        {
            if (Motion != null) Motion.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (Motion != null) Motion.Destroy();
            Motion = null;
        }
    }
}
