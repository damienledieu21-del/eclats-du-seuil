using System.Collections.Generic;
using System.Linq;
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
            if (slot == StyleSlot.Character && catalog.Character(id) is CharacterLook character && character.Height > 0f) height = character.Height;
            GameObject look = Fit(prefab, height, 0f, null, false, out float bottom, out GameObject model);
            Vector3 s = body.transform.localScale;
            look.transform.SetParent(body.transform, false);
            float k = look.transform.localScale.y;
            look.transform.localScale = new Vector3(k / s.x, k / s.y, k / s.z);
            // Les pieds au sol : le bas de la capsule est à 1 m sous son centre (à l'échelle de hauteur du corps).
            look.transform.localPosition = new Vector3(0f, -1f - bottom * k / s.y, 0f);
            look.transform.localRotation = Quaternion.Euler(0f, catalog.Yaw(slot, id), 0f);
            Renderer own = body.GetComponent<Renderer>();
            if (own != null) own.enabled = false;
            // Essai du 26 octobre 2026 : les loups et les sangliers des packs n'ont pas de texture (couleur unique ou couleurs de sommets que
            // le rendu n'affiche pas) et sortaient blancs ; leurs matériaux sans texture prennent la couleur du catalogue.
            if (slot == StyleSlot.Wolf || slot == StyleSlot.Boar)
                foreach (Renderer r in look.GetComponentsInChildren<Renderer>())
                    foreach (Material m in r.materials)
                        if (!m.HasProperty("_BaseMap") || m.GetTexture("_BaseMap") == null)
                            ArenaView.ApplyColor(m, slot == StyleSlot.Wolf ? catalog.WolfColor : catalog.BoarColor);
            Animation animation = look.GetComponentInChildren<Animation>();
            // Essai réaliste : les personnages de Mixamo ont leurs animations dans des fichiers à part (catalogue), jouées par un composant
            // Animation posé sur la racine du modèle (les chemins des os partent de là).
            AnimationClip[] clips = catalog.Clips(slot, id);
            if (clips != null && clips.Length > 0)
            {
                if (animation == null) animation = model.AddComponent<Animation>();
                foreach (AnimationClip clip in clips)
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
        /// Essai réaliste : un élément de décor (<paramref name="part"/> non nul) tiré d'un modèle de Poly Haven, livré en lot de variantes
        /// avec leurs niveaux de détail, n'en garde qu'une, choisie d'après l'identifiant, ramenée au centre du support
        /// (<see cref="StyleRules.KeepParts"/>). <paramref name="natural"/> : taille réelle si elle est plausible
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
            bool lot = false;
            if (part != null && renderers.Length > 1)
            {
                var names = new List<string>();
                foreach (Renderer r in renderers) names.Add(r.gameObject.name);
                IReadOnlyList<int> keep = StyleRules.KeepParts(prefab.name, names, part);
                if (keep != null && keep.Count > 0 && keep.Count < renderers.Length)
                {
                    renderers = Keep(look, model, renderers, keep);
                    lot = true;
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
                    + " m, bas " + bottom + ", échelle " + k + (lot ? ", une variante du lot" : "") + ")");
            return look;
        }

        /// <summary>
        /// Ne montre que les pièces <paramref name="keep"/> du modèle (une variante d'un lot de Poly Haven, un tronçon du ponton), ramenées au
        /// centre du support ; rend leurs rendus.
        /// </summary>
        private static Renderer[] Keep(GameObject look, GameObject model, Renderer[] renderers, IReadOnlyList<int> keep)
        {
            // Les niveaux de détail importés ne choisissent plus : seule la pièce gardée s'affiche.
            foreach (LODGroup group in look.GetComponentsInChildren<LODGroup>()) group.enabled = false;
            var kept = new List<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
                if (keep.Contains(i)) kept.Add(renderers[i]);
                else renderers[i].gameObject.SetActive(false);
            Bounds piece = kept[0].bounds;
            foreach (Renderer r in kept) piece.Encapsulate(r.bounds);
            Vector3 center = piece.center;
            model.transform.localPosition = model.transform.localPosition - new Vector3(center.x, 0f, center.z);
            return kept.ToArray();
        }

        /// <summary>Boîte des rendus actifs d'un objet (dans le monde) ; false s'il n'en a aucun.</summary>
        public static bool VisibleBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.gameObject.activeInHierarchy) continue;
                if (!any) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                any = true;
            }
            return any;
        }

        /// <summary>
        /// Essai réaliste (27 octobre 2026) : objet de décor du catalogue (<see cref="StyleProp"/>) posé au sol en <paramref name="at"/>,
        /// tourné de <paramref name="yaw"/> degrés, à sa taille réelle multipliée par <paramref name="scale"/> (et par l'échelle du
        /// catalogue) ; <paramref name="oneVariant"/> : une seule variante d'un lot de Poly Haven (les pervenches). Null sans modèle.
        /// </summary>
        public static GameObject Prop(Transform parent, StyleProp kind, string id, Vector3 at, float yaw, float scale = 1f, bool oneVariant = false)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            PropLook prop = catalog != null ? catalog.Prop(kind) : null;
            if (prop == null) return null;
            var look = new GameObject(id + " (essai de style)");
            GameObject model = Object.Instantiate(prop.Model, look.transform, false);
            foreach (Collider c in look.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            Renderer[] renderers = look.GetComponentsInChildren<Renderer>();
            if (oneVariant && renderers.Length > 1)
            {
                var names = new List<string>();
                foreach (Renderer r in renderers) names.Add(r.gameObject.name);
                IReadOnlyList<int> keep = StyleRules.KeepParts(prop.Model.name, names, id);
                if (keep != null && keep.Count > 0 && keep.Count < renderers.Length) Keep(look, model, renderers, keep);
            }
            look.transform.SetParent(parent, false);
            look.transform.localScale = Vector3.one * scale * (prop.Scale > 0f ? prop.Scale : 1f);
            look.transform.rotation = Quaternion.Euler(0f, yaw + prop.Yaw, 0f);
            look.transform.position = at;
            Ground(look, at.y);
            return look;
        }

        /// <summary>
        /// Bâtiment du catalogue (le moulin de Sketchfab) posé sur une emprise carrée de <paramref name="width"/> mètres centrée en
        /// <paramref name="at"/> : ce qui touche le sol (la tour, pas les ailes qui dépassent) prend la largeur de l'emprise et y est centré ;
        /// son animation (les ailes du moulin) tourne en boucle s'il en a une. Null sans modèle.
        /// </summary>
        public static GameObject Building(Transform parent, StyleProp kind, string id, Vector3 at, float yaw, float width)
        {
            GameObject look = Prop(parent, kind, id, at, yaw);
            if (look == null) return null;
            if (Foot(look, out Bounds foot) && Mathf.Max(foot.size.x, foot.size.z) > 0.01f)
            {
                look.transform.localScale = look.transform.localScale * (width / Mathf.Max(foot.size.x, foot.size.z));
                Ground(look, at.y);
                Foot(look, out foot);
                look.transform.position += new Vector3(at.x - foot.center.x, 0f, at.z - foot.center.z);
            }
            foreach (Animation animation in look.GetComponentsInChildren<Animation>())
            {
                foreach (AnimationState state in animation) state.wrapMode = WrapMode.Loop;
                animation.Play();
            }
            return look;
        }

        /// <summary>Boîte des rendus actifs qui touchent le bas de l'objet (à 5 % de sa hauteur près) : la tour d'un moulin, sans ses ailes.</summary>
        private static bool Foot(GameObject look, out Bounds foot)
        {
            foot = default;
            if (!VisibleBounds(look, out Bounds all)) return false;
            bool any = false;
            foreach (Renderer r in look.GetComponentsInChildren<Renderer>())
            {
                if (!r.gameObject.activeInHierarchy || r.bounds.min.y > all.min.y + 0.05f * all.size.y) continue;
                if (!any) foot = r.bounds;
                else foot.Encapsulate(r.bounds);
                any = true;
            }
            return any;
        }

        /// <summary>Descend ou monte l'objet pour que le bas de ses rendus actifs soit à la hauteur <paramref name="y"/>.</summary>
        public static void Ground(GameObject look, float y)
        {
            if (VisibleBounds(look, out Bounds b)) look.transform.position += Vector3.up * (y - b.min.y);
        }

        /// <summary>
        /// Essai réaliste (demande de Damien du 27 octobre 2026 : « le pont n'est pas réaliste ») : pont fait d'un tronçon du ponton du
        /// catalogue (<see cref="StyleRules.BridgeSection"/>), tourné pour franchir la rivière suivant x, mis à l'échelle de la longueur du
        /// passage, son tablier (dessus des planches, <see cref="StyleRules.BridgeDeck"/>) à <paramref name="deck"/> mètres. Ses pieux
        /// descendent dans le lit creusé. Null sans ponton.
        /// </summary>
        public static GameObject Bridge(Transform parent, Vector3 center, float length, float deck)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            PropLook prop = catalog != null ? catalog.Prop(StyleProp.Bridge) : null;
            if (prop == null) return null;
            var look = new GameObject("Pont (essai de style)");
            GameObject model = Object.Instantiate(prop.Model, look.transform, false);
            foreach (Collider c in look.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            Renderer[] renderers = look.GetComponentsInChildren<Renderer>();
            var names = new List<string>();
            foreach (Renderer r in renderers) names.Add(r.gameObject.name);
            int section = StyleRules.BridgeSection(names), planks = StyleRules.BridgeDeck(names);
            if (section < 0) return Discard(look);
            // Hauteur du tablier dans le modèle : le dessus des planches (même hauteur pour tous les tronçons du ponton modulaire).
            float deckTop = planks >= 0 ? renderers[planks].bounds.max.y : renderers[section].bounds.max.y;
            float bottom = renderers[section].bounds.min.y;
            Keep(look, model, renderers, new[] { section });
            look.transform.SetParent(parent, false);
            VisibleBounds(look, out Bounds b);
            // Le plus long côté du tronçon franchit la rivière (suivant x).
            float yaw = (b.size.z > b.size.x ? 90f : 0f) + prop.Yaw;
            float k = length / Mathf.Max(0.1f, Mathf.Max(b.size.x, b.size.z)) * (prop.Scale > 0f ? prop.Scale : 1f);
            look.transform.localScale = Vector3.one * k;
            look.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            look.transform.position = center;
            VisibleBounds(look, out Bounds placed);
            look.transform.position += new Vector3(center.x - placed.center.x, deck - (placed.min.y + (deckTop - bottom) * k), center.z - placed.center.z);
            return look;
        }

        private static GameObject Discard(GameObject look)
        {
            Object.Destroy(look);
            return null;
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
