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

        /// <summary>
        /// Relecture de l'essai 7 : animation choisie pour chaque demande (mouvement, préférence, variante), retenue : la liste des animations
        /// ne change plus après la construction, et le choix (expressions régulières) coûtait à chaque image et pour chaque corps.
        /// </summary>
        private readonly Dictionary<(StyleMotion, string, int), string> _picked = new Dictionary<(StyleMotion, string, int), string>();
        private bool _fallen;
        private string _playing;

        /// <summary>Matériaux du modèle (copies propres à ce corps) et leur couleur d'origine, pour la teinte.</summary>
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Color> _colors = new List<Color>();
        private bool _tinted;
        private Vector3 _last;
        private bool _started;
        private float _speed;

        // Sous-lot V2.9 : une bête (loup, sanglier, cerf, biche, lièvre) règle la cadence de sa marche et de son galop sur sa vitesse ; un
        // modèle sans squelette (le sanglier de Hanabi_Map) est animé par le code (BeastCalls.StaticGait).
        private bool _beast;
        private AnimationState _playingState;
        private StyleMotion _playingMotion;
        private bool _procedural;
        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private float _metersToLocal;
        private float _clock;

        // Essai réaliste 7 : geste en cours (attaque, sort, coup reçu) et sa fin.
        private float _actionUntil;
        private StyleMotion _actionMotion;
        private string _actionClip;

        /// <summary>Essai réaliste 7 : hauteur du modèle (mètres), pour placer les effets à hauteur de poitrine.</summary>
        public float Height { get; }

        internal StyleBody(Transform look, Animation animation, float height)
        {
            _look = look;
            _animation = animation;
            Height = height;
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
        /// Sous-lot V2.9 : une bête (<see cref="StyleRules.IsBeast"/>) ; sans animation, elle est animée par le code autour de sa place
        /// (<paramref name="basePosition"/>, <paramref name="baseRotation"/> : celles du modèle dans son corps ; <paramref name="metersToLocal"/> :
        /// un mètre dans les unités du corps).
        /// </summary>
        internal void Beast(Vector3 basePosition, Quaternion baseRotation, float metersToLocal)
        {
            _beast = true;
            _procedural = _animation == null || _clips.Count == 0;
            _basePosition = basePosition;
            _baseRotation = baseRotation;
            _metersToLocal = metersToLocal;
        }

        /// <summary>
        /// Essai réaliste 8 : un villageois joue en boucle l'animation de son activité (<see cref="TownModels.VillagerClip"/>), commencée à
        /// un moment propre à chacun (deux bavards ne bougent pas ensemble). Rend son nom, ou null s'il n'a pas d'animation.
        /// </summary>
        public string Loop(VillagerActivity activity, int variant)
        {
            if (_animation == null) return null;
            string clip = TownModels.VillagerClip(_clips, activity, variant);
            if (clip == null) return null;
            if (clip == _playing) return clip;
            AnimationState state = _animation[clip];
            if (state != null)
            {
                state.wrapMode = WrapMode.Loop;
                state.time = state.length * (Mathf.Abs(variant) % 10) / 10f;
            }
            _animation.CrossFade(clip, 0.25f);
            _playing = clip;
            return clip;
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
        public void Strike() => Act(StyleMotion.Attack, null, 0, 0.6f, 1.2f);

        /// <summary>
        /// Essai réaliste 7 (demande de Damien du 28 octobre 2026 : « coups variés ») : le geste de la compétence
        /// <paramref name="skillId"/> (<see cref="StyleRules.SkillAnimation"/>) : chaque frappe a le sien, l'ultime et les soutiens sont des
        /// sorts. Entre 0,6 et 1,4 seconde.
        /// </summary>
        public void Strike(string skillId, bool ultimate)
        {
            (StyleMotion motion, string prefer, int variant) = StyleRules.SkillAnimation(skillId ?? "", ultimate);
            Act(motion, prefer, variant, 0.6f, 1.4f);
        }

        /// <summary>
        /// Essai réaliste 7 : un adversaire porte l'attaque <paramref name="attackId"/> (annoncée au sol) : son geste
        /// (<see cref="StyleRules.AttackAnimation"/>), entre 0,6 et 1,6 seconde. Jusqu'ici, les adversaires ne jouaient jamais leur attaque.
        /// </summary>
        public void Attack(string attackId)
        {
            (StyleMotion motion, string prefer, int variant) = StyleRules.AttackAnimation(attackId ?? "");
            Act(motion, prefer, variant, 0.6f, 1.6f);
        }

        /// <summary>
        /// Essai réaliste 7 : coup reçu, sur la garde (<paramref name="guarded"/>) ou non. Il n'interrompt ni une attaque ni un sort ; sans
        /// animation de coup reçu, rien ne change.
        /// </summary>
        public void Hurt(bool guarded)
        {
            if (Time.time < _actionUntil && (_actionMotion == StyleMotion.Attack || _actionMotion == StyleMotion.Cast)) return;
            Act(guarded ? StyleMotion.GuardHit : StyleMotion.Hurt, null, 0, 0.3f, 0.7f);
        }

        private void Act(StyleMotion motion, string prefer, int variant, float shortest, float longest)
        {
            if (_fallen) return;
            if (_procedural)
            {
                // Sous-lot V2.9 : le sanglier sans squelette charge (le groin en avant) ou encaisse (un sursaut).
                _actionUntil = Time.time + (motion == StyleMotion.Hurt || motion == StyleMotion.GuardHit ? 0.3f : 0.55f);
                _actionMotion = motion;
                return;
            }
            if (_animation == null) return;
            string clip = Pick(motion, prefer, variant);
            if (clip == null) return;
            AnimationState state = _animation[clip];
            _actionUntil = Time.time + (state != null ? Mathf.Clamp(state.length, shortest, longest) : shortest);
            _actionMotion = motion;
            _actionClip = clip;
            // Deux coups de suite : le geste recommence. Relecture de l'essai 7 : toujours depuis le début, même redemandé pendant son fondu
            // de sortie (il ne repart pas de sa dernière image).
            if (state != null) state.time = 0f;
        }

        /// <summary>Animation pour cette demande (<see cref="StyleRules.Clip(IEnumerable{string}, StyleMotion, string, int)"/>), retenue.</summary>
        private string Pick(StyleMotion motion, string prefer = null, int variant = 0)
        {
            var key = (motion, prefer ?? string.Empty, variant);
            if (!_picked.TryGetValue(key, out string clip))
            {
                clip = StyleRules.Clip(_clips, motion, prefer, variant);
                _picked[key] = clip;
            }
            return clip;
        }

        /// <summary>À chaque image : allure d'après le déplacement affiché, puis l'animation qui va avec.</summary>
        /// <param name="guarding">
        /// Essai réaliste 6 (essai de Damien du 27 octobre 2026 : le disque du bouclier « c'est à corriger ») : garde levée ou parade ;
        /// immobile, le modèle joue son clip de garde (« Sword And Shield Block » de Mixamo), sinon il reste au repos.
        /// </param>
        /// <param name="wander">Sous-lot V2.4.1 : ce que fait une bête qui erre (<c>CombatantView.Wander</c>) : arrêtée, elle broute ou fouille.</param>
        public void Update(Vector3 position, bool dead, float deltaTime, bool guarding = false, string wander = null)
        {
            // Un modèle sans animation de mort (le loup et le cochon des packs) se couche sur le flanc, immobile.
            if (dead && (_animation == null || Pick(StyleMotion.Death) == null))
            {
                if (_fallen) return;
                _fallen = true;
                if (_animation != null) _animation.Stop();
                _look.localRotation = _look.localRotation * Quaternion.Euler(0f, 0f, 90f);
                return;
            }
            if (_animation == null && !_procedural) return;
            if (_started && deltaTime > 0f)
            {
                Vector3 d = position - _last;
                d.y = 0f;
                // Vitesse lissée : le corps glisse d'une place du moteur à la suivante, par à-coups d'un tick.
                _speed = Mathf.Lerp(_speed, d.magnitude / deltaTime, 1f - Mathf.Exp(-8f * deltaTime));
            }
            _last = position;
            _started = true;
            if (_procedural)
            {
                Procedural(dead, deltaTime, wander);
                return;
            }
            // Essai réaliste 7 : un geste en cours (attaque, sort, coup reçu) l'emporte sur l'allure, pas sur la mort.
            bool acting = !dead && Time.time < _actionUntil && _actionClip != null;
            StyleMotion motion = acting ? _actionMotion : StyleRules.Motion(_speed, dead, false, guarding, wander);
            string clip = acting ? _actionClip : Pick(motion);
            if (clip != null && clip != _playing)
            {
                _playing = clip;
                _playingMotion = motion;
                _playingState = _beast ? _animation[clip] : null;
                _animation.CrossFade(clip, acting ? (_actionMotion == StyleMotion.Hurt || _actionMotion == StyleMotion.GuardHit ? 0.06f : 0.1f) : 0.18f);
            }
            // Sous-lot V2.9 : la cadence de la marche et du galop suit la vitesse (moins de pattes qui glissent), d'après la taille de la bête.
            if (_playingState != null)
            {
                float stride = Mathf.Sqrt(Mathf.Max(0.2f, Height));
                float reference = _playingMotion == StyleMotion.Walk ? 1.2f * stride : _playingMotion == StyleMotion.Run ? 5.5f * stride : 0f;
                _playingState.speed = reference > 0f ? Mathf.Clamp(_speed / reference, 0.6f, 1.7f) : 1f;
            }
        }

        /// <summary>
        /// Sous-lot V2.9 : allure d'un modèle sans squelette (<see cref="BeastCalls.StaticGait"/>) : il se dandine au pas, bondit au trot,
        /// fouille le groin au sol, charge le groin en avant ; mort, il se couche sur le flanc (plus haut).
        /// </summary>
        private void Procedural(bool dead, float deltaTime, string wander)
        {
            if (dead || _look == null) return;
            _clock += deltaTime;
            (double bob, double pitch, double roll, double yaw) = BeastCalls.StaticGait(_speed, _clock, wander);
            float forward = 0f;
            if (Time.time < _actionUntil)
            {
                float left = Mathf.Clamp01((_actionUntil - Time.time) / 0.55f);
                float push = Mathf.Sin(Mathf.PI * (1f - left));
                if (_actionMotion == StyleMotion.Hurt || _actionMotion == StyleMotion.GuardHit) roll += 8.0 * push;
                else
                {
                    pitch += 14.0 * push;
                    forward = 0.25f * Height * push;
                }
            }
            _look.localPosition = _basePosition + new Vector3(0f, (float)bob * Height, forward) * _metersToLocal;
            _look.localRotation = _baseRotation * Quaternion.Euler((float)pitch, (float)yaw, (float)roll);
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
            bool beast = StyleRules.IsBeast(slot);
            GameObject look = Fit(prefab, height, 0f, null, false, out float bottom, out GameObject model, beast);
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
            // Essai réaliste 8 : le loup réaliste de l'Asset Store a ses textures sur « _MainTex » ou sur d'autres propriétés (pelage) : il
            // garde ses couleurs.
            // Sous-lot V2.9 : seuls les matériaux sans texture restés blancs la prennent (les bêtes de Quaternius ont leurs couleurs : pelage,
            // ventre clair, yeux, truffe) ; le loup blanc du contrat de la guilde est éclairci (sauf les yeux et la truffe, sombres).
            if (slot == StyleSlot.Wolf || slot == StyleSlot.Boar)
            {
                bool white = slot == StyleSlot.Wolf && StyleRules.Key(id ?? "").StartsWith("loupblanc", System.StringComparison.Ordinal);
                foreach (Renderer r in look.GetComponentsInChildren<Renderer>())
                    foreach (Material m in r.materials)
                    {
                        if (Textured(m, "_BaseMap") || Textured(m, "_MainTex") || Textured(m, "_BaseColorMap")) continue;
                        Color c = BaseColor(m);
                        if (white && c.maxColorComponent > 0.1f) ArenaView.ApplyColor(m, Color.Lerp(c, catalog.WhiteWolfColor, 0.8f));
                        else if (Mathf.Min(c.r, Mathf.Min(c.g, c.b)) > 0.85f) ArenaView.ApplyColor(m, slot == StyleSlot.Wolf ? catalog.WolfColor : catalog.BoarColor);
                    }
            }
            Animation animation = look.GetComponentInChildren<Animation>();
            // Essai réaliste 8 : un prefab de pack garde son Animator, qui ne sait pas jouer des animations « Legacy » : coupé, le
            // composant Animation joue.
            if (animation != null)
                foreach (Animator animator in look.GetComponentsInChildren<Animator>())
                {
                    animator.enabled = false;
                    Object.Destroy(animator);
                }
            // Essai réaliste : les personnages de Mixamo ont leurs animations dans des fichiers à part (catalogue), jouées par un composant
            // Animation posé sur la racine du modèle (les chemins des os partent de là).
            AnimationClip[] clips = catalog.Clips(slot, id);
            if (clips != null && clips.Length > 0)
            {
                if (animation == null) animation = model.AddComponent<Animation>();
                foreach (AnimationClip clip in clips)
                    if (clip != null && animation.GetClip(clip.name) == null) animation.AddClip(clip, clip.name);
            }
            var styled = new StyleBody(look.transform, animation, height);
            if (beast) styled.Beast(look.transform.localPosition, look.transform.localRotation, 1f / Mathf.Max(0.01f, s.y));
            return styled;
        }

        /// <summary>Couleur de base d'un matériau (URP ou ancien rendu), blanc sans couleur.</summary>
        internal static Color BaseColor(Material m) =>
            m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;

        /// <summary>Le matériau a-t-il une texture sur <paramref name="property"/> ?</summary>
        private static bool Textured(Material m, string property) => m.HasProperty(property) && m.GetTexture(property) != null;

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
        internal static GameObject Fit(GameObject prefab, float height, float width, string part, bool natural, out float bottom, out GameObject model,
            bool center = false)
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
            // Sous-lot V2.9 : une bête est ramenée au centre de son corps (le sanglier de Hanabi_Map et le lièvre de Pneshik sont posés loin
            // de leur origine dans leur fichier) : elle tourne sur elle-même et reste sous sa barre de vie.
            if (center && any) model.transform.localPosition -= new Vector3(bounds.center.x, 0f, bounds.center.z);
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
            // Le moulin de Sketchfab est livré avec sa version simplifiée posée à côté : seul l'exemplaire le plus détaillé reste.
            Renderer[] renderers = look.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 1)
            {
                var boxes = new List<(double, double, double, double, double, double)>();
                var detail = new List<int>();
                foreach (Renderer r in renderers)
                {
                    Bounds rb = r.bounds;
                    boxes.Add((rb.min.x, rb.min.y, rb.min.z, rb.max.x, rb.max.y, rb.max.z));
                    MeshFilter filter = r.GetComponent<MeshFilter>();
                    SkinnedMeshRenderer skin = r as SkinnedMeshRenderer;
                    Mesh mesh = filter != null ? filter.sharedMesh : skin != null ? skin.sharedMesh : null;
                    detail.Add(mesh != null ? mesh.vertexCount : 0);
                }
                IReadOnlyList<int> keep = StyleRules.Cluster(boxes, detail);
                if (keep.Count > 0 && keep.Count < renderers.Length) Keep(look, look.transform.GetChild(0).gameObject, renderers, keep);
            }
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

        /// <summary>Modèles déjà décrits dans le journal de Unity (une ligne par modèle, pour régler l'essai).</summary>
        private static readonly HashSet<string> Logged = new HashSet<string>();

        private static int _variant;

        /// <summary>
        /// Joue un son de l'essai (rien sans catalogue ou sans ce son). Essai réaliste 7 : une variante différente à chaque fois quand le
        /// catalogue en a plusieurs.
        /// </summary>
        public static void Play(AudioSource source, StyleSound sound, float volume = 0.8f)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            AudioClip clip = catalog != null ? catalog.Sound(sound, _variant++) : null;
            if (source != null && clip != null) source.PlayOneShot(clip, volume);
        }
    }
}
