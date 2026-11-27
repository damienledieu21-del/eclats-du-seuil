using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Chantier V2.52b : le spectre de l'Envers (moines renversés, l'abbé renversé) flotte au-dessus de sa place en ondulant
    /// (<see cref="EnversLieux.WraithBob"/>) : son modèle est statique (la marche bipède a échoué chez Tripo).
    /// </summary>
    public sealed class EnversFloat : MonoBehaviour
    {
        private string _id;
        private Vector3 _rest;
        private float _scale = 1f;

        /// <summary>Le spectre <paramref name="id"/>, posé en <paramref name="rest"/> (local) ; <paramref name="scale"/> : mètres vers l'échelle locale.</summary>
        public void Setup(string id, Vector3 rest, float scale)
        {
            _id = id;
            _rest = rest;
            _scale = scale;
        }

        private void Update()
        {
            transform.localPosition = _rest + Vector3.up * ((float)EnversLieux.WraithBob(_id, Time.time) * _scale);
        }
    }
}
