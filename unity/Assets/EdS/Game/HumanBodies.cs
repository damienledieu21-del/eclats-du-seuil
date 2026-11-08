using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.9.2 : lecteur d'animations d'un corps, par leur nom : le composant Animation (« Legacy ») des modèles des packs, ou le
    /// graphe Humanoid des corps humains (<see cref="HumanMotion"/>).
    /// </summary>
    internal interface IClipPlayer
    {
        IEnumerable<string> Names { get; }
        bool Has(string name);
        float Length(string name);
        void CrossFade(string name, float fade);
        void SetTime(string name, float time);
        void SetSpeed(string name, float speed);
        void SetLoop(string name, bool loop);
        void Stop();
    }

    /// <summary>Sous-lot V2.9.2 : le composant Animation derrière <see cref="IClipPlayer"/> (comportement d'avant inchangé).</summary>
    internal sealed class LegacyClips : IClipPlayer
    {
        private readonly Animation _animation;
        private readonly List<string> _names = new List<string>();

        internal LegacyClips(Animation animation)
        {
            _animation = animation;
            foreach (AnimationState state in animation) _names.Add(state.name);
        }

        public IEnumerable<string> Names => _names;
        public bool Has(string name) => _animation[name] != null;
        public float Length(string name) => _animation[name] != null ? _animation[name].length : 0f;
        public void CrossFade(string name, float fade) => _animation.CrossFade(name, fade);

        public void SetTime(string name, float time)
        {
            AnimationState state = _animation[name];
            if (state != null) state.time = time;
        }

        public void SetSpeed(string name, float speed)
        {
            AnimationState state = _animation[name];
            if (state != null) state.speed = speed;
        }

        public void SetLoop(string name, bool loop)
        {
            AnimationState state = _animation[name];
            if (state != null) state.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
        }

        public void Stop() => _animation.Stop();
    }

    /// <summary>
    /// Sous-lot V2.9.2 (4e vague) : animations Humanoid d'un corps humain, sans contrôleur à préparer : un graphe d'animation (Playables) dont
    /// le mélangeur reçoit une entrée par animation des Universal Animation Library ; un fondu fait monter l'animation voulue et descendre les
    /// autres, les poids restent normalisés (pas de pose de repos qui transparaît pendant un fondu). Les animations éteintes sont en pause (rien
    /// à calculer) ; le graphe est détruit avec le corps (<see cref="HumanMotionDriver"/>). Les boucles sont réglées à l'import (préparation).
    /// </summary>
    public sealed class HumanMotion : IClipPlayer
    {
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private readonly List<AnimationClipPlayable> _playables = new List<AnimationClipPlayable>();
        private readonly List<string> _names = new List<string>();
        private readonly Dictionary<string, int> _index = new Dictionary<string, int>();
        private float[] _weight;
        private float[] _target;
        private float[] _length;
        private float _rate = 4f;
        private int _current = -1;

        // Relecture de la PR #117 : seules les entrées allumées ou en fondu sont parcourues à chaque image (pas les ~55), et rien du tout
        // une fois le fondu fini ; un instant posé avant le fondu (départ décalé d'un villageois) est gardé par lui.
        private readonly List<int> _active = new List<int>();
        private bool[] _preset;
        private bool _settled;

        private HumanMotion()
        {
        }

        /// <summary>Graphe des animations <paramref name="clips"/> sur <paramref name="animator"/> (avatar Humanoid), ou null s'il manque quelque chose.</summary>
        internal static HumanMotion Create(Animator animator, AnimationClip[] clips)
        {
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman || clips == null || clips.Length == 0) return null;
            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = null;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var motion = new HumanMotion();
            var usable = new List<AnimationClip>();
            foreach (AnimationClip clip in clips)
                if (clip != null && !motion._index.ContainsKey(clip.name))
                {
                    motion._index[clip.name] = usable.Count;
                    motion._names.Add(clip.name);
                    usable.Add(clip);
                }
            motion._graph = PlayableGraph.Create("Corps humain");
            motion._graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(motion._graph, "Animation", animator);
            motion._mixer = AnimationMixerPlayable.Create(motion._graph, usable.Count);
            output.SetSourcePlayable(motion._mixer);
            motion._weight = new float[usable.Count];
            motion._target = new float[usable.Count];
            motion._length = new float[usable.Count];
            motion._preset = new bool[usable.Count];
            for (int i = 0; i < usable.Count; i++)
            {
                AnimationClipPlayable playable = AnimationClipPlayable.Create(motion._graph, usable[i]);
                playable.SetApplyFootIK(true);
                motion._graph.Connect(playable, 0, motion._mixer, i);
                motion._mixer.SetInputWeight(i, 0f);
                playable.Pause();
                motion._playables.Add(playable);
                motion._length[i] = usable[i].length;
            }
            motion._graph.Play();
            HumanMotionDriver driver = animator.gameObject.AddComponent<HumanMotionDriver>();
            driver.Motion = motion;
            // L'attente d'abord, sans fondu : jamais de pose en T à l'apparition.
            if (motion._index.ContainsKey("Idle")) motion.CrossFade("Idle", 0f);
            motion.Tick(0f);
            return motion;
        }

        public IEnumerable<string> Names => _names;
        public bool Has(string name) => name != null && _index.ContainsKey(name);
        public float Length(string name) => Has(name) ? _length[_index[name]] : 0f;

        public void CrossFade(string name, float fade)
        {
            if (!Has(name)) return;
            int i = _index[name];
            if (i == _current) return;
            _current = i;
            _rate = fade > 0.01f ? 1f / fade : 1000f;
            foreach (int k in _active) _target[k] = 0f;
            _target[i] = 1f;
            if (!_active.Contains(i)) _active.Add(i);
            AnimationClipPlayable playable = _playables[i];
            if (_weight[i] <= 0f && !_preset[i]) playable.SetTime(0.0);
            _preset[i] = false;
            playable.Play();
            _settled = false;
        }

        public void SetTime(string name, float time)
        {
            if (!Has(name)) return;
            int i = _index[name];
            _playables[i].SetTime(time);
            _preset[i] = i != _current;   // gardé par le fondu qui suit
        }

        public void SetSpeed(string name, float speed)
        {
            if (Has(name)) _playables[_index[name]].SetSpeed(speed);
        }

        /// <summary>Les boucles sont réglées à l'import des Universal Animation Library (préparation) : rien à faire ici.</summary>
        public void SetLoop(string name, bool loop)
        {
        }

        /// <summary>Fige le corps dans sa pose (la mort sans animation de mort ne se produit pas : elles en ont une).</summary>
        public void Stop()
        {
            foreach (AnimationClipPlayable p in _playables) p.Pause();
        }

        /// <summary>À chaque image : poids vers leur cible, normalisés ; une animation éteinte est mise en pause.</summary>
        internal void Tick(float deltaTime)
        {
            if (_settled || !_graph.IsValid() || _weight == null) return;
            float step = deltaTime <= 0f ? 1f : _rate * deltaTime;
            float sum = 0f;
            bool moving = false;
            foreach (int i in _active)
            {
                _weight[i] = Mathf.MoveTowards(_weight[i], _target[i], deltaTime <= 0f && _target[i] > 0f ? 1f : step);
                moving |= _weight[i] != _target[i];
                sum += _weight[i];
            }
            for (int a = _active.Count - 1; a >= 0; a--)
            {
                int i = _active[a];
                float w = sum > 0.0001f ? _weight[i] / sum : (i == _current ? 1f : 0f);
                _mixer.SetInputWeight(i, w);
                if (_weight[i] <= 0f && _target[i] <= 0f)
                {
                    _playables[i].Pause();
                    _active.RemoveAt(a);
                }
            }
            _settled = !moving;
        }

        internal void Destroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }
    }

    /// <summary>
    /// Sous-lot V2.9.2 (4e vague) : habillage des corps humains : qui en prend un, sa tenue (matériau peint par la préparation), sa coiffure,
    /// sa barbe et la couleur de ses cheveux (os de la tête du corps), sa carrure, et ses animations Humanoid.
    /// </summary>
    public static class HumanBodies
    {
        /// <summary>Matériaux de cheveux teints, partagés (au plus deux matériaux × six couleurs).</summary>
        private static readonly Dictionary<(Material, int), Material> Tinted = new Dictionary<(Material, int), Material>();

        /// <summary>Les corps humains sont-ils prêts (fichiers préparés, avatar Humanoid valable) ?</summary>
        public static bool Usable(StyleCatalog catalog)
        {
            if (catalog == null || catalog.Humans == null || !catalog.Humans.Ready) return false;
            Animator male = catalog.Humans.Male.GetComponentInChildren<Animator>();
            Animator female = catalog.Humans.Female.GetComponentInChildren<Animator>();
            return male != null && male.avatar != null && male.avatar.isHuman && female != null && female.avatar != null && female.avatar.isHuman;
        }

        /// <summary>
        /// L'aspect humain voulu pour ce corps (<see cref="HumanLooks.PreferHuman"/>), ou null : il garde le modèle du catalogue.
        /// <paramref name="lookId"/> : l'identifiant qui décide de l'aspect (sinon <paramref name="id"/>).
        /// </summary>
        public static HumanLook? Wanted(StyleCatalog catalog, StyleSlot slot, string id, string lookId)
        {
            if (slot == StyleSlot.Hero)
                return catalog.Hero == null && catalog.HumanHero && Usable(catalog) ? HumanLooks.For("heros", "heros") : (HumanLook?)null;
            if (slot != StyleSlot.Character) return null;
            bool mixamo = catalog.Character(id) != null;
            string role = lookId != null && lookId.StartsWith("npc.", System.StringComparison.Ordinal) ? lookId : id;
            if (!HumanLooks.PreferHuman(role, mixamo, Usable(catalog), catalog.HumanVillagers)) return null;
            return HumanLooks.For(lookId ?? id, id);
        }

        /// <summary>
        /// Habille la copie <paramref name="model"/> d'un corps (homme ou femme) selon <paramref name="look"/> et rend son lecteur
        /// d'animations (null si l'avatar n'est pas Humanoid : le corps reste immobile, jamais invisible).
        /// </summary>
        internal static IClipPlayer Dress(GameObject model, HumanCatalog humans, HumanLook look)
        {
            Material outfit = humans.Outfit(look.Female, look.DarkSkin, look.Palette);
            Material hair1 = Tint(humans.Hair1, look.HairTone), hair2 = Tint(humans.Hair2, look.HairTone);
            foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>()) Paint(skin, outfit, hair1, hair2, humans.Eyes);
            var bones = new Dictionary<string, Transform>();
            foreach (Transform t in model.GetComponentsInChildren<Transform>())
                if (!bones.ContainsKey(t.name)) bones[t.name] = t;
            Attach(model, humans.Hair(look.Hair), bones, outfit, hair1, hair2, humans.Eyes);
            if (look.Beard) Attach(model, humans.Beard, bones, outfit, hair1, hair2, humans.Eyes);
            Vector3 s = model.transform.localScale;
            model.transform.localScale = new Vector3(s.x * (float)look.Width, s.y, s.z * (float)look.Width);
            return HumanMotion.Create(model.GetComponentInChildren<Animator>(), humans.Clips);
        }

        /// <summary>Matériaux d'un maillage d'après le nom de ceux du fichier : la peau devient la tenue, les cheveux leur couleur, les yeux.</summary>
        private static void Paint(SkinnedMeshRenderer skin, Material outfit, Material hair1, Material hair2, Material eyes)
        {
            Material[] materials = skin.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                string name = materials[i] != null ? materials[i].name.ToLowerInvariant() : string.Empty;
                // Relecture de la PR #117 : un matériau de sourcils garde le sien (« eyebrow » contient « eye »).
                if (name.Contains("eyebrow")) continue;
                Material want = name.Contains("superhero") ? outfit : name.Contains("hair_2") ? hair2 : name.Contains("hair") ? hair1
                    : name.Contains("eye") ? eyes : null;
                if (want == null) continue;
                materials[i] = want;
                changed = true;
            }
            if (changed) skin.sharedMaterials = materials;
        }

        /// <summary>
        /// Coiffure ou barbe (<paramref name="prefab"/>, faite sur le même squelette) posée sur le corps : ses maillages prennent les os du
        /// corps (par leur nom), puis sa copie est détruite.
        /// </summary>
        private static void Attach(GameObject model, GameObject prefab, Dictionary<string, Transform> bones, Material outfit, Material hair1, Material hair2,
            Material eyes)
        {
            if (prefab == null) return;
            GameObject copy = Object.Instantiate(prefab, model.transform, false);
            foreach (SkinnedMeshRenderer skin in copy.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                Transform[] old = skin.bones;
                var mapped = new Transform[old.Length];
                bool complete = true;
                for (int i = 0; i < old.Length; i++)
                {
                    if (old[i] != null && bones.TryGetValue(old[i].name, out Transform b)) mapped[i] = b;
                    else complete = false;
                }
                if (!complete) continue;
                skin.bones = mapped;
                if (skin.rootBone != null && bones.TryGetValue(skin.rootBone.name, out Transform root)) skin.rootBone = root;
                skin.updateWhenOffscreen = true;
                Paint(skin, outfit, hair1, hair2, eyes);
                skin.transform.SetParent(model.transform, false);
            }
            Object.Destroy(copy);
        }

        /// <summary>Copie partagée de <paramref name="hair"/> teinte de la couleur de cheveux <paramref name="tone"/>.</summary>
        private static Material Tint(Material hair, int tone)
        {
            if (hair == null) return null;
            if (Tinted.TryGetValue((hair, tone), out Material done) && done != null) return done;
            (byte r, byte g, byte b) = HumanLooks.HairTones[Mathf.Clamp(tone, 0, HumanLooks.HairTones.Length - 1)];
            var m = new Material(hair) { name = hair.name + " " + tone };
            ArenaView.ApplyColor(m, new Color(r / 255f, g / 255f, b / 255f));
            Tinted[(hair, tone)] = m;
            return m;
        }
    }
}
