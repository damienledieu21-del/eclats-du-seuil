using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Essai de style : un combattant habillé d'un modèle des packs. Le corps simple reste (place, rotation, cible, barres) mais n'est plus
    /// dessiné ; le modèle, enfant du corps, joue l'animation qui suit l'allure affichée (attente, marche, course), l'attaque quand on la
    /// lui demande, la mort à la fin. Rien ne revient vers le moteur : l'aspect ne change aucun combat.
    /// </summary>
    public sealed class StyleBody
    {
        private readonly Animation _animation;
        private readonly List<string> _clips = new List<string>();
        private string _playing;
        private Vector3 _last;
        private bool _started;
        private float _speed;
        private float _attackUntil;

        internal StyleBody(Animation animation)
        {
            _animation = animation;
            if (animation == null) return;
            foreach (AnimationState state in animation)
            {
                _clips.Add(state.name);
                state.wrapMode = StyleRules.Loops(state.name) ? WrapMode.Loop : WrapMode.ClampForever;
            }
        }

        /// <summary>Demande l'animation d'attaque pendant un court instant (compétence lancée par le joueur).</summary>
        public void Strike() => _attackUntil = Time.time + 0.6f;

        /// <summary>À chaque image : allure d'après le déplacement affiché, puis l'animation qui va avec.</summary>
        public void Update(Vector3 position, bool dead, float deltaTime)
        {
            if (_animation == null) return;
            if (_started && deltaTime > 0f)
            {
                Vector3 d = position - _last;
                d.y = 0f;
                // Vitesse lissée : le corps glisse d'une place du moteur à la suivante, par à-coups d'un tick.
                _speed = Mathf.Lerp(_speed, d.magnitude / deltaTime, 1f - Mathf.Exp(-8f * deltaTime));
            }
            _last = position;
            _started = true;
            string clip = StyleRules.Clip(_clips, StyleRules.Motion(_speed, dead, Time.time < _attackUntil));
            if (clip == null || clip == _playing) return;
            _playing = clip;
            _animation.CrossFade(clip, 0.15f);
        }
    }

    /// <summary>Essai de style : habillage des corps et du décor par les modèles du <see cref="StyleCatalog"/>.</summary>
    public static class StyleLook
    {
        /// <summary>
        /// Habille <paramref name="body"/> (capsule de 2 m de haut à l'échelle 1, centrée à 1 m du sol) du modèle de <paramref name="slot"/>,
        /// à la hauteur <paramref name="height"/> ; rend null (rien ne change) sans catalogue ou sans modèle.
        /// </summary>
        public static StyleBody Dress(GameObject body, StyleSlot slot, string id, float height)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            GameObject prefab = catalog != null ? catalog.Model(slot, id) : null;
            if (prefab == null) return null;
            GameObject look = Fit(prefab, height, 0f, out float bottom);
            Vector3 s = body.transform.localScale;
            look.transform.SetParent(body.transform, false);
            float k = look.transform.localScale.y;
            look.transform.localScale = new Vector3(k / s.x, k / s.y, k / s.z);
            // Les pieds au sol : le bas de la capsule est à 1 m sous son centre (à l'échelle de hauteur du corps).
            look.transform.localPosition = new Vector3(0f, -1f - bottom * k / s.y, 0f);
            look.transform.localRotation = Quaternion.Euler(0f, catalog.Yaw(slot), 0f);
            Renderer own = body.GetComponent<Renderer>();
            if (own != null) own.enabled = false;
            return new StyleBody(look.GetComponentInChildren<Animation>());
        }

        /// <summary>
        /// Élément de décor du modèle de <paramref name="slot"/> posé au sol en <paramref name="center"/>, de hauteur <paramref name="height"/>
        /// ou, si <paramref name="width"/> est positif, de largeur <paramref name="width"/> (rochers sur l'emprise de l'obstacle) ; null sinon.
        /// </summary>
        public static GameObject Decor(Transform parent, StyleSlot slot, string id, Vector3 center, float height, float width = 0f)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            GameObject prefab = catalog != null ? catalog.Model(slot, id) : null;
            if (prefab == null) return null;
            GameObject look = Fit(prefab, height, width, out float bottom);
            float k = look.transform.localScale.y;
            look.name = id + " (essai de style)";
            look.transform.SetParent(parent, false);
            look.transform.position = new Vector3(center.x, -bottom * k, center.z);
            look.transform.rotation = Quaternion.Euler(0f, StyleRules.Variant(id, 360), 0f);
            return look;
        }

        /// <summary>Copie du modèle, sans collision, à l'échelle voulue ; <paramref name="bottom"/> : bas du modèle à l'échelle 1.</summary>
        private static GameObject Fit(GameObject prefab, float height, float width, out float bottom)
        {
            GameObject look = Object.Instantiate(prefab);
            foreach (Collider c in look.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            look.transform.position = Vector3.zero;
            look.transform.rotation = Quaternion.identity;
            look.transform.localScale = new Vector3(1f, 1f, 1f);
            bool any = false;
            Bounds bounds = default;
            foreach (Renderer r in look.GetComponentsInChildren<Renderer>())
            {
                if (!any) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                any = true;
            }
            bottom = any ? bounds.min.y : 0f;
            float k = width > 0f && any
                ? (float)StyleRules.Scale(Mathf.Max(bounds.size.x, bounds.size.z), width)
                : (float)StyleRules.Scale(any ? bounds.size.y : 0f, height);
            look.transform.localScale = new Vector3(k, k, k);
            return look;
        }

        /// <summary>Joue un son de l'essai (rien sans catalogue ou sans ce son).</summary>
        public static void Play(AudioSource source, StyleSound sound, float volume = 0.8f)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            AudioClip clip = catalog != null ? catalog.Sound(sound) : null;
            if (source != null && clip != null) source.PlayOneShot(clip, volume);
        }
    }
}
