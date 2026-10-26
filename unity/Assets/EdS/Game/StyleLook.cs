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
        private readonly Transform _look;
        private readonly List<string> _clips = new List<string>();
        private bool _fallen;
        private string _playing;

        /// <summary>Matériaux du modèle (copies propres à ce corps) et leur couleur d'origine, pour la teinte.</summary>
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Color> _colors = new List<Color>();
        private bool _tinted;
        private Vector3 _last;
        private bool _started;
        private float _speed;
        private float _attackUntil;

        internal StyleBody(Transform look, Animation animation)
        {
            _look = look;
            _animation = animation;
            foreach (Renderer r in look.GetComponentsInChildren<Renderer>())
                foreach (Material m in r.materials)
                {
                    _materials.Add(m);
                    _colors.Add(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);
                }
            if (animation == null) return;
            foreach (AnimationState state in animation)
            {
                _clips.Add(state.name);
                state.wrapMode = StyleRules.Loops(state.name) ? WrapMode.Loop : WrapMode.ClampForever;
            }
        }

        /// <summary>
        /// Revue Codex de la PR #55 : même signal que le corps simple : assombri mort, clignotant jaune étourdi (chaque image tant que
        /// l'étourdissement dure), sinon les couleurs d'origine du modèle (rendues une fois au retour).
        /// </summary>
        public void Tint(bool dead, bool stunned)
        {
            if (!dead && !stunned && !_tinted) return;
            for (int i = 0; i < _materials.Count; i++) ArenaView.ApplyColor(_materials[i], ArenaView.Shade(_colors[i], dead, stunned));
            _tinted = dead || stunned;
        }

        /// <summary>
        /// Demande l'animation d'attaque pendant un court instant (compétence lancée par le joueur) : la durée de l'animation, entre 0,6 et
        /// 1,2 seconde (essai réaliste : le coup d'épée de Mixamo dure 1,5 seconde, on n'en garde que le geste).
        /// </summary>
        public void Strike()
        {
            string clip = _animation != null ? StyleRules.Clip(_clips, StyleMotion.Attack) : null;
            AnimationState state = clip != null ? _animation[clip] : null;
            _attackUntil = Time.time + (state != null ? Mathf.Clamp(state.length, 0.6f, 1.2f) : 0.6f);
            if (clip != null && clip == _playing) _animation.Rewind(clip);   // deux coups de suite : le geste recommence
        }

        /// <summary>À chaque image : allure d'après le déplacement affiché, puis l'animation qui va avec.</summary>
        public void Update(Vector3 position, bool dead, float deltaTime)
        {
            // Un modèle sans animation de mort (le loup et le cochon des packs) se couche sur le flanc, immobile.
            if (dead && (_animation == null || StyleRules.Clip(_clips, StyleMotion.Death) == null))
            {
                if (_fallen) return;
                _fallen = true;
                if (_animation != null) _animation.Stop();
                _look.localRotation = _look.localRotation * Quaternion.Euler(0f, 0f, 90f);
                return;
            }
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
            GameObject look = Fit(prefab, height, 0f, null, false, out float bottom, out GameObject model);
            Vector3 s = body.transform.localScale;
            look.transform.SetParent(body.transform, false);
            float k = look.transform.localScale.y;
            look.transform.localScale = new Vector3(k / s.x, k / s.y, k / s.z);
            // Les pieds au sol : le bas de la capsule est à 1 m sous son centre (à l'échelle de hauteur du corps).
            look.transform.localPosition = new Vector3(0f, -1f - bottom * k / s.y, 0f);
            look.transform.localRotation = Quaternion.Euler(0f, catalog.Yaw(slot), 0f);
            Renderer own = body.GetComponent<Renderer>();
            if (own != null) own.enabled = false;
            Animation animation = look.GetComponentInChildren<Animation>();
            // Essai réaliste : le héros de Mixamo arrive sans animation ; ses animations sont des fichiers à part (catalogue HeroClips),
            // jouées par un composant Animation posé sur la racine du modèle (les chemins des os partent de là).
            if (slot == StyleSlot.Hero && catalog.HeroClips != null && catalog.HeroClips.Length > 0)
            {
                if (animation == null) animation = model.AddComponent<Animation>();
                foreach (AnimationClip clip in catalog.HeroClips)
                    if (clip != null && animation.GetClip(clip.name) == null) animation.AddClip(clip, clip.name);
            }
            return new StyleBody(look.transform, animation);
        }

        /// <summary>
        /// Élément de décor du modèle de <paramref name="slot"/> posé au sol en <paramref name="center"/>, de hauteur <paramref name="height"/>
        /// ou, si <paramref name="width"/> est positif, de largeur <paramref name="width"/> (rochers sur l'emprise de l'obstacle), ou, si
        /// <paramref name="natural"/>, à sa taille réelle quand elle est plausible (plantes) ; null sans catalogue ou sans modèle.
        /// </summary>
        public static GameObject Decor(Transform parent, StyleSlot slot, string id, Vector3 center, float height, float width = 0f, bool natural = false)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            GameObject prefab = catalog != null ? catalog.Model(slot, id) : null;
            if (prefab == null) return null;
            GameObject look = Fit(prefab, height, width, id, natural, out float bottom, out GameObject model);
            float k = look.transform.localScale.y;
            look.name = id + " (essai de style)";
            look.transform.SetParent(parent, false);
            look.transform.position = new Vector3(center.x, -bottom * k, center.z);
            look.transform.rotation = Quaternion.Euler(0f, StyleRules.Variant(id, 360), 0f);
            return look;
        }

        /// <summary>
        /// Copie du modèle, sans collision, à l'échelle voulue ; <paramref name="bottom"/> : bas du modèle à l'échelle 1 ;
        /// <paramref name="model"/> : la copie, posée dans le support rendu.
        /// <para>
        /// Essai de Damien du 26 octobre 2026 (le chevalier, les loups et les sangliers restaient invisibles) : le modèle importé garde la
        /// rotation et l'échelle de sa racine (un modèle venu de Blender est redressé par elle) ; il est posé dans un support vide, et c'est
        /// le support qu'on met à l'échelle, place et tourne. Les maillages animés sont calculés même hors champ (pas de disparition après
        /// la mise à l'échelle).
        /// </para>
        /// <para>
        /// Essai réaliste : un élément de décor (<paramref name="part"/> non nul) tiré d'un lot de pièces séparées (les rochers et les
        /// touffes d'herbe de Poly Haven sont livrés par lots, <see cref="StyleRules.IsCollection"/>) n'en garde qu'une, choisie d'après
        /// l'identifiant, ramenée au centre du support. <paramref name="natural"/> : taille réelle si elle est plausible
        /// (<see cref="StyleRules.NaturalScale"/>), légèrement variée.
        /// </para>
        /// </summary>
        private static GameObject Fit(GameObject prefab, float height, float width, string part, bool natural, out float bottom, out GameObject model)
        {
            var look = new GameObject(prefab.name + " (essai de style)");
            model = Object.Instantiate(prefab, look.transform, false);
            foreach (Collider c in look.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            foreach (SkinnedMeshRenderer skin in look.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
            Renderer[] renderers = look.GetComponentsInChildren<Renderer>();
            if (part != null && renderers.Length > 1)
            {
                var boxes = new List<StyleBox>();
                foreach (Renderer r in renderers) boxes.Add(new StyleBox(r.bounds.min.x, r.bounds.min.z, r.bounds.max.x, r.bounds.max.z));
                if (StyleRules.IsCollection(boxes))
                {
                    int keep = StyleRules.Variant(part + "/piece", renderers.Length);
                    for (int i = 0; i < renderers.Length; i++)
                        if (i != keep) renderers[i].gameObject.SetActive(false);
                    Vector3 center = renderers[keep].bounds.center;
                    model.transform.localPosition = model.transform.localPosition - new Vector3(center.x, 0f, center.z);
                    renderers = new[] { renderers[keep] };
                }
            }
            bool any = false;
            Bounds bounds = default;
            foreach (Renderer r in renderers)
            {
                if (!any) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                any = true;
            }
            bottom = any ? bounds.min.y : 0f;
            float size = any ? bounds.size.y : 0f;
            float k;
            if (width > 0f && any) k = (float)StyleRules.Scale(Mathf.Max(bounds.size.x, bounds.size.z), width);
            else if (natural) k = (float)StyleRules.NaturalScale(size, height) * (0.85f + 0.003f * StyleRules.Variant(part ?? prefab.name, 100));
            else k = (float)StyleRules.Scale(size, height);
            look.transform.localScale = new Vector3(k, k, k);
            if (Logged.Add(prefab.name))
                Debug.Log("Essai de style : " + prefab.name + " (hauteur importée " + size + " m, largeur " + (any ? Mathf.Max(bounds.size.x, bounds.size.z) : 0f)
                    + " m, bas " + bottom + ", échelle " + k + (renderers.Length == 1 && part != null ? ", une pièce du lot" : "") + ")");
            return look;
        }

        /// <summary>Modèles déjà décrits dans le journal de Unity (une ligne par modèle, pour régler l'essai).</summary>
        private static readonly HashSet<string> Logged = new HashSet<string>();

        /// <summary>Joue un son de l'essai (rien sans catalogue ou sans ce son).</summary>
        public static void Play(AudioSource source, StyleSound sound, float volume = 0.8f)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            AudioClip clip = catalog != null ? catalog.Sound(sound) : null;
            if (source != null && clip != null) source.PlayOneShot(clip, volume);
        }
    }
}
