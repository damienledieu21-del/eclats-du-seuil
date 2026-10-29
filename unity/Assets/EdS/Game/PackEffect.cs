using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Essai réaliste 8 (relecture indépendante) : un effet de pack posé par <see cref="Effects"/>. Les auras en boucle (cercle magique,
    /// soin, renforcement) ne s'arrêtent pas seules : à la fin de leur durée, elles cessent d'émettre, et l'effet est retiré
    /// <see cref="Fade"/> secondes plus tard, une fois ses dernières particules éteintes, au lieu de disparaître d'un coup.
    /// </summary>
    public sealed class PackEffect : MonoBehaviour
    {
        /// <summary>Temps (s) laissé aux dernières particules pour s'éteindre.</summary>
        public const float Fade = 1.5f;

        private float _left;
        private bool _stopping;
        private ParticleSystem[] _systems = new ParticleSystem[0];

        /// <summary>Durée (s) de l'effet avant qu'il cesse d'émettre (au moins 0,3 s).</summary>
        public void Begin(float life)
        {
            _left = Mathf.Max(0.3f, life);
            _systems = GetComponentsInChildren<ParticleSystem>();
        }

        private void Update()
        {
            if (_stopping) return;
            _left -= Time.deltaTime;
            if (_left > 0f) return;
            _stopping = true;
            foreach (ParticleSystem system in _systems)
                if (system != null) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(gameObject, Fade);
        }
    }
}
