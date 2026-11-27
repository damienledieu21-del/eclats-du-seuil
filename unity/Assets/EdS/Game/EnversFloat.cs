using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Chantier V2.52b : le spectre de l'Envers (moines renversés, l'abbé renversé) flotte au-dessus de sa place en ondulant
    /// (<see cref="EnversLieux.WraithBob"/>) : la marche bipède a échoué chez Tripo ; depuis V2.55 ses bras et sa robe ondulent (animations faites dans Blender), la hauteur reste au code.
    /// </summary>
    public sealed class EnversFloat : MonoBehaviour
    {
        private string _id;
        private Vector3 _rest;
        private float _scale = 1f;
        private StyleBody _body;
        private bool _stopped;

        /// <summary>Le spectre <paramref name="id"/>, posé en <paramref name="rest"/> (local) ; <paramref name="scale"/> : mètres vers l'échelle locale.</summary>
        public void Setup(string id, Vector3 rest, float scale)
        {
            _id = id;
            _rest = rest;
            _scale = scale;
        }

        /// <summary>Le corps du spectre : mort ou tombé, il ne bouge plus (relecture indépendante de la PR #263).</summary>
        public void Follow(StyleBody body) => _body = body;

        private void Update()
        {
            if (_stopped) return;
            if (!EnversLieux.WraithMoves(_body != null && _body.Dead))
            {
                transform.localPosition = _rest;   // retombé à sa place, sans ondulation, une seule fois
                _stopped = true;
                return;
            }
            transform.localPosition = _rest + Vector3.up * ((float)EnversLieux.WraithBob(_id, Time.time) * _scale);
        }
    }
}
