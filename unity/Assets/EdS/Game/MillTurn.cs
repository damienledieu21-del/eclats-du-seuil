using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// V2.25 : anime un modèle de l'atelier qui a des pièces mobiles (le moulin) : sa roue tourne en continu autour de son axe x, à
    /// <see cref="TownModels.MillWheelDegreesPerSecond"/> (aubes basses dans le sens du courant), et la texture de l'eau de son bief
    /// défile le long du courant (<see cref="TownModels.MillWaterOffset"/>). Affichage seulement : rien du moteur.
    /// </summary>
    public sealed class MillTurn : MonoBehaviour
    {
        internal Transform Axle;
        internal Material Water;
        private float _angle;
        private float _time;

        private void Update()
        {
            float dt = Time.deltaTime;
            _time += dt;
            if (Axle != null)
            {
                _angle = (_angle + dt * (float)TownModels.MillWheelDegreesPerSecond) % 360f;
                Axle.localRotation = Quaternion.Euler(_angle, 0f, 0f);
            }
            if (Water != null)
            {
                var offset = new Vector2(0f, (float)TownModels.MillWaterOffset(_time));
                Water.SetTextureOffset(Water.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex", offset);
            }
        }

        private void OnDestroy()
        {
            if (Water != null) Destroy(Water);
        }
    }
}
