using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.9 (2e vague, demande de Damien : « les pas selon le sol ») : les pas du personnage, un pas tous les 0,75 m au pas (jusqu'à
    /// 1,4 m en courant, <see cref="BeastCalls.StepLength"/>), pris un à un dans les enregistrements déposés : herbe, gravier (routes et sol
    /// nu), feuilles (forêt), plancher (maisons) ; volume : <see cref="StyleCatalog.StepVolume"/>. Sans fichier : aucun pas. Aucune
    /// allocation par image.
    /// </summary>
    public sealed class Footsteps
    {
        private readonly SoundPool _pool;
        private Vector3 _last;
        private bool _seen;
        private float _walked;
        private float _speed;
        private int _steps;

        public Footsteps(Transform parent)
        {
            _pool = new SoundPool(parent, "Pas du personnage (V2.9)", 3, 0.5f);
        }

        public void Destroy() => _pool.Destroy();

        /// <summary>
        /// À chaque image : <paramref name="feet"/> (place du personnage), <paramref name="forest"/> (dans la forêt), <paramref name="walking"/>
        /// (il marche lui-même : ni pause, ni scène) ; <paramref name="arena"/> donne le sol.
        /// </summary>
        public void Update(ArenaView arena, Vector3 feet, bool forest, bool walking, float deltaTime)
        {
            _pool.Update();
            if (!walking || deltaTime <= 0f || arena == null)
            {
                _seen = false;
                _walked = 0f;
                return;
            }
            if (_seen)
            {
                Vector3 d = feet - _last;
                d.y = 0f;
                float step = d.magnitude;
                if (step > 3f) step = 0f;   // une porte, un voyage : pas de pas
                _speed = Mathf.Lerp(_speed, step / deltaTime, 1f - Mathf.Exp(-8f * deltaTime));
                _walked += step;
            }
            _last = feet;
            _seen = true;
            double length = BeastCalls.StepLength(_speed);
            if (double.IsInfinity(length))
            {
                _walked = 0f;
                return;
            }
            if (_walked < length) return;
            _walked = 0f;
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null || catalog.StepVolume <= 0f) return;
            StyleSound sound = arena.StepSound(feet, forest);
            _pool.Play(sound, _steps++, feet - Vector3.up, 0.5f, catalog.StepVolume, 0.4f, false);
        }
    }
}
