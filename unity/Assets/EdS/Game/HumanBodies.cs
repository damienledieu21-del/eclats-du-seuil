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

        /// <summary>Sous-lot V2.15 : vitesse de déplacement (m/s, à l'échelle du modèle) mesurée dans l'animation ; 0 si elle fait du sur-place.</summary>
        float Travel(string name);
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

        public float Travel(string name)
        {
            AnimationClip clip = _animation.GetClip(name);
            if (clip == null) return 0f;
            Vector3 v = clip.averageSpeed;
            return Mathf.Sqrt(v.x * v.x + v.z * v.z);
        }
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
        private float[] _travel;
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
            motion._travel = new float[usable.Count];
            // Sous-lot V2.15 : pieds posés sur le relief (OnAnimatorIK de HumanMotionDriver), sauf sur un PC modeste ou un téléphone.
            bool feet = !DeviceQuality.Light;
            motion._preset = new bool[usable.Count];
            for (int i = 0; i < usable.Count; i++)
            {
                AnimationClipPlayable playable = AnimationClipPlayable.Create(motion._graph, usable[i]);
                playable.SetApplyFootIK(true);
                if (feet) playable.SetApplyPlayableIK(true);
                motion._graph.Connect(playable, 0, motion._mixer, i);
                motion._mixer.SetInputWeight(i, 0f);
                playable.Pause();
                motion._playables.Add(playable);
                motion._length[i] = usable[i].length;
                Vector3 v = usable[i].averageSpeed;
                motion._travel[i] = Mathf.Sqrt(v.x * v.x + v.z * v.z);
            }
            motion._graph.Play();
            HumanMotionDriver driver = animator.gameObject.AddComponent<HumanMotionDriver>();
            driver.Motion = motion;
            driver.Animator = animator;
            driver.Feet = feet;
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

        public float Travel(string name) => Has(name) ? _travel[_index[name]] : 0f;

        /// <summary>
        /// Sous-lot V2.15 : mélange de deux cycles (marche et course) : <paramref name="b"/> pèse <paramref name="t"/>, <paramref name="a"/> le
        /// reste, les autres s'éteignent en fondu ; chacun à sa cadence. Le cycle qui entre part à la même phase que l'autre (pieds ensemble).
        /// </summary>
        public void Blend(string a, string b, float t, float rateA, float rateB, float fade)
        {
            if (!Has(a) || !Has(b) || a == b)
            {
                CrossFade(a, fade);
                SetSpeed(a, rateA);
                return;
            }
            int ia = _index[a], ib = _index[b];
            t = Mathf.Clamp01(t);
            _current = -2;   // un fondu suivant (attente, geste) part toujours
            _rate = fade > 0.01f ? 1f / fade : 1000f;
            foreach (int k in _active) _target[k] = 0f;
            Enter(ia, ib);
            Enter(ib, ia);
            _target[ia] = 1f - t;
            _target[ib] = t;
            _playables[ia].SetSpeed(rateA);
            _playables[ib].SetSpeed(rateB);
            _settled = false;
        }

        /// <summary>Allume l'entrée <paramref name="i"/> ; éteinte, elle reprend la phase de <paramref name="other"/>.</summary>
        private void Enter(int i, int other)
        {
            if (!_active.Contains(i)) _active.Add(i);
            if (_weight[i] <= 0f && _weight[other] > 0f && _length[other] > 0f)
            {
                double phase = _playables[other].GetTime() / _length[other];
                _playables[i].SetTime((phase - System.Math.Floor(phase)) * _length[i]);
            }
            else if (_weight[i] <= 0f) _playables[i].SetTime(0.0);
            _playables[i].Play();
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
                float w = sum > 0.0001f ? _weight[i] / sum : _target[i];   // V2.15 : la cible (le mélange n'a pas d'entrée « courante »)
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
        /// Sous-lot V2.16 : aspect du héros choisi dans le créateur de personnage (posé par le jeu à la reprise et au « Valider » du créateur), ou
        /// null : l'aspect par défaut. Affichage seulement.
        /// </summary>
        public static EdS.Data.Saves.SavedLook HeroLook { get; set; }

        /// <summary>
        /// Sous-lot V2.16 : le personnage de MakeHuman voulu pour ce corps, ou null. Le héros : celui que le joueur a choisi dans le créateur
        /// (« made.… »), ou la ligne « heros = … » de PNJ.txt ; un PNJ : sa ligne de PNJ.txt, ou le dossier à son prénom. Jamais pour un
        /// ennemi, une bête ni le Gardien.
        /// </summary>
        internal static MadeHumanLook MadeWanted(StyleCatalog catalog, StyleSlot slot, string id, string lookId)
        {
            if (catalog == null || !catalog.UseMadeHumans || catalog.MadeLooks == null || catalog.MadeLooks.Length == 0) return null;
            if (slot == StyleSlot.Hero)
            {
                if (HeroLook != null && HeroLooks.IsMade(HeroLook.Body)) return catalog.MadeByName(HeroLook.Body.Substring(HeroLooks.MadePrefix.Length));
                return HeroLook == null ? catalog.MadeFor(MadeHumans.HeroKey) : null;   // un choix de Quaternius l'emporte sur PNJ.txt
            }
            if (slot != StyleSlot.Character) return null;
            string who = lookId ?? id;
            string key = StyleRules.CharacterOf(who);
            if (key.Length == 0 || key == "gardien") return null;
            return catalog.MadeFor(who);
        }

        /// <summary>
        /// L'aspect humain voulu pour ce corps (<see cref="HumanLooks.PreferHuman"/>), ou null : il garde le modèle du catalogue.
        /// <paramref name="lookId"/> : l'identifiant qui décide de l'aspect (sinon <paramref name="id"/>).
        /// </summary>
        public static HumanLook? Wanted(StyleCatalog catalog, StyleSlot slot, string id, string lookId)
        {
            if (slot == StyleSlot.Hero)
            {
                if (!Usable(catalog)) return null;
                // Sous-lot V2.16 : un corps de Quaternius choisi par le joueur l'emporte sur le paladin de Mixamo.
                if (HeroLook != null && !HeroLooks.IsMade(HeroLook.Body)) return HeroLooks.ToHumanLook(HeroLook);
                return catalog.Hero == null && catalog.HumanHero ? HumanLooks.For("heros", "heros") : (HumanLook?)null;
            }
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
        internal static IClipPlayer Dress(GameObject model, HumanCatalog humans, HumanLook look, WardrobePlan clothes = null)
        {
            Material outfit = humans.Outfit(look.Female, look.DarkSkin, look.Palette);
            Material hair1 = Tint(humans.Hair1, look.HairTone), hair2 = Tint(humans.Hair2, look.HairTone);
            foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>()) Paint(skin, outfit, hair1, hair2, humans.Eyes);
            var bones = new Dictionary<string, Transform>();
            foreach (Transform t in model.GetComponentsInChildren<Transform>())
                if (!bones.ContainsKey(t.name)) bones[t.name] = t;
            // Sous-lot V2.9.4 : les vrais vêtements d'abord (le corps garde sa tenue peinte s'il en manque une pièce ou si l'une ne s'attache pas).
            HumanHair hair = look.Hair;
            if (clothes != null && Clothe(model, humans, look, clothes, bones, out string headgear)) hair = HumanWardrobe.HairUnder(headgear, look.Hair, look.Female);
            Attach(model, humans.Hair(hair), bones, outfit, hair1, hair2, humans.Eyes);
            if (look.Beard) Attach(model, humans.Beard, bones, outfit, hair1, hair2, humans.Eyes);
            Vector3 s = model.transform.localScale;
            model.transform.localScale = new Vector3(s.x * (float)look.Width, s.y, s.z * (float)look.Width);
            return HumanMotion.Create(model.GetComponentInChildren<Animator>(), humans.Clips);
        }

        /// <summary>
        /// Sous-lot V2.9.4 : la tenue voulue pour ce corps (<see cref="HumanWardrobe.Plan"/>), ou null : pas de vrais vêtements (réglage
        /// <c>Human Clothes</c> décoché, ou aucune pièce préparée). La saison est celle du jeu (<see cref="GameSeason"/>, sauf réglage
        /// <c>Human Season</c> forcé), la pluie le temps qu'il fait quand le corps apparaît, la fête le réglage <c>Human Festival</c> ou
        /// <see cref="Festival"/> ; sur téléphone, une seule pièce facultative (<see cref="HumanWardrobe.MaxOptional"/>).
        /// </summary>
        public static WardrobePlan Clothing(StyleCatalog catalog, StyleSlot slot, string id, string lookId, HumanLook look)
        {
            if (catalog == null || !catalog.HumanClothes || catalog.Humans == null || catalog.Humans.Clothes == null || catalog.Humans.Clothes.Length == 0)
                return null;
            string who = slot == StyleSlot.Hero ? "heros" : lookId ?? id;
            string role = slot == StyleSlot.Hero ? "heros" : id;
            bool festive = catalog.HumanFestival || Festival;
            WardrobePlan plan = HumanWardrobe.Plan(who, role, look, Season(catalog), HumanWardrobe.IsRain(StyleWorld.Weather.Kind), festive,
                HumanWardrobe.MaxOptional(Application.isMobilePlatform));
            // Sous-lot V2.16 : la couleur de tenue choisie dans le créateur teinte les vrais vêtements du héros (sauf un jour de fête).
            if (slot == StyleSlot.Hero && HeroLook != null && !festive)
                return new WardrobePlan(plan.Outfit, plan.Requests, plan.Fabric, HeroLooks.ClothesTint(HeroLook.Palette), plan.Wear, plan.Season, plan.Festive, plan.Rain,
                    plan.MaxOptional);
            return plan;
        }

        /// <summary>Sous-lot V2.9.4 : jour de fête au bourg (posé par les fêtes à venir) : les villageois qui apparaissent sortent leurs beaux habits.</summary>
        public static bool Festival { get; set; }

        /// <summary>
        /// Sous-lot V2.9.4 (décision de Damien du 8 novembre 2026 : la saison des tenues suit la saison du jeu) : point d'entrée du
        /// calendrier du jeu, qui pose la saison en cours ; le printemps tant qu'aucun calendrier ne la donne (le calendrier de V2.10 le branchera).
        /// </summary>
        public static WardrobeSeason GameSeason { get; set; } = WardrobeSeason.Spring;

        /// <summary>Saison des vêtements : celle du jeu (<see cref="GameSeason"/>), ou celle forcée par le réglage <c>Human Season</c> pour l'essai.</summary>
        public static WardrobeSeason Season(StyleCatalog catalog)
        {
            switch (catalog != null ? catalog.HumanSeason : StyleSeason.Auto)
            {
                case StyleSeason.Spring: return WardrobeSeason.Spring;
                case StyleSeason.Summer: return WardrobeSeason.Summer;
                case StyleSeason.Autumn: return WardrobeSeason.Autumn;
                case StyleSeason.Winter: return WardrobeSeason.Winter;
                default: return GameSeason;
            }
        }

        /// <summary>Étoffes et peaux teintes et usées, partagées (une copie par matériau, teinte et usure : jamais une par personnage).</summary>
        private static readonly Dictionary<(Material, int, int), Material> Worn = new Dictionary<(Material, int, int), Material>();

        /// <summary>
        /// Sous-lot V2.9.4 : habille le corps de vraies pièces (<paramref name="plan"/>) : chaque pièce prend les os du corps par leur nom.
        /// Faux, et rien n'est posé (le corps garde sa tenue peinte), si le corps réduit à sa tête manque ou ne va pas à ce corps (relecture de
        /// la PR #119 : sinon le corps peint resterait dessous), s'il manque une pièce obligatoire, ou si l'une ne s'attache pas. Une pièce
        /// facultative (coiffe, cape, épaulières, col, tablier) qui ne s'attache pas est seulement omise. Vrai : le corps ne garde que sa tête
        /// et son cou, et <paramref name="headgear"/> est le genre de la coiffe posée (vide sans coiffe).
        /// </summary>
        private static bool Clothe(GameObject model, HumanCatalog humans, HumanLook look, WardrobePlan plan, Dictionary<string, Transform> bones, out string headgear)
        {
            headgear = string.Empty;
            Mesh head = look.Female ? humans.FemaleHead : humans.MaleHead;
            SkinnedMeshRenderer body = null;
            foreach (SkinnedMeshRenderer s in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (s.sharedMesh != null && (body == null || s.sharedMesh.vertexCount > body.sharedMesh.vertexCount)) body = s;
            if (body == null || !HumanWardrobe.HeadFits(head != null ? head.vertexCount : (int?)null, body.sharedMesh.vertexCount)) return false;
            int[] picks = HumanWardrobe.Resolve(plan, humans.Items, look.Female);
            if (picks == null) return false;
            var placed = new List<GameObject>();
            Material skin = Skin(humans, look);
            string gear = string.Empty;
            for (int r = 0; r < picks.Length; r++)
            {
                if (picks[r] < 0) continue;
                WardrobeLook piece = humans.Clothes[picks[r]];
                bool ok = Wear(model, piece, bones, humans, look, skin, plan, placed);
                if (!ok && plan.Requests[r].Required)
                {
                    foreach (GameObject g in placed) Object.Destroy(g);
                    return false;
                }
                if (ok && piece.Slot == WardrobeSlot.Head) gear = piece.Kind ?? string.Empty;
            }
            body.sharedMesh = head;
            headgear = gear;
            return true;
        }

        /// <summary>
        /// Pose une pièce : son maillage (<see cref="WardrobeLook.PartIndex"/> dans le fichier, relecture de la PR #119) et ceux qui
        /// l'accompagnent (<see cref="WardrobeLook.Companions"/> : ceintures, brassards) prennent les os du corps par leur nom ; chaque matériau d'étoffe prend l'étoffe préparée de même nom (<see cref="HumanWardrobe.FabricStem"/> : une pièce
        /// à plusieurs matériaux garde chacun le sien), teinte et usée ; la peau des mains celle du corps. Faux si un os manque (rien n'est posé).
        /// </summary>
        private static bool Wear(GameObject model, WardrobeLook piece, Dictionary<string, Transform> bones, HumanCatalog humans, HumanLook look, Material skin,
            WardrobePlan plan, List<GameObject> placed)
        {
            if (piece == null || piece.Model == null) return false;
            GameObject copy = Object.Instantiate(piece.Model, model.transform, false);
            SkinnedMeshRenderer[] all = copy.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int index = piece.PartIndex < 0 ? 0 : piece.PartIndex;
            SkinnedMeshRenderer main = index < all.Length ? all[index] : null;
            if (main == null || main.sharedMesh == null || (!string.IsNullOrEmpty(piece.Part) && main.gameObject.name != piece.Part) || !Bind(main, bones, out Transform[] mainBones))
            {
                Object.Destroy(copy);
                return false;
            }
            // La pièce et ses maillages d'accompagnement (ceintures, brassards) ; un accompagnement qui ne s'attache pas est laissé.
            var worn = new List<(SkinnedMeshRenderer Skin, Transform[] Bones)> { (main, mainBones) };
            if (piece.Companions != null)
                foreach (int c in piece.Companions)
                    if (c >= 0 && c < all.Length && c != index && all[c] != null && all[c].sharedMesh != null && Bind(all[c], bones, out Transform[] extra))
                        worn.Add((all[c], extra));
            int gender = look.Female ? 2 : 1;
            foreach ((SkinnedMeshRenderer s, Transform[] mapped) in worn)
            {
                s.bones = mapped;
                if (s.rootBone != null && bones.TryGetValue(s.rootBone.name, out Transform root)) s.rootBone = root;
                s.updateWhenOffscreen = false;
                Material[] materials = s.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    if (HumanWardrobe.IsSkinMaterial(materials[i].name))
                    {
                        materials[i] = skin != null ? skin : look.DarkSkin ? Darkened(materials[i]) : materials[i];
                        continue;
                    }
                    Material fabric = humans.Fabric(HumanWardrobe.FabricStem(materials[i].name), gender, plan.Fabric);
                    materials[i] = Worn1(fabric != null ? fabric : materials[i], plan.Tint, plan.Wear);
                }
                s.sharedMaterials = materials;
                s.transform.SetParent(model.transform, false);
                placed.Add(s.gameObject);
            }
            Object.Destroy(copy);
            return true;
        }

        /// <summary>Les os du corps pour ceux du maillage <paramref name="s"/>, par leur nom ; faux s'il en manque un.</summary>
        private static bool Bind(SkinnedMeshRenderer s, Dictionary<string, Transform> bones, out Transform[] mapped)
        {
            mapped = null;
            Transform[] old = s.bones;
            if (old == null || old.Length == 0) return false;
            var now = new Transform[old.Length];
            for (int i = 0; i < old.Length; i++)
            {
                if (old[i] == null || !bones.TryGetValue(old[i].name, out Transform b)) return false;
                now[i] = b;
            }
            mapped = now;
            return true;
        }

        /// <summary>Peau des mains : celle du pack préparée pour ce corps et cette peau, sinon null (celle de la pièce, assombrie pour une peau sombre).</summary>
        private static Material Skin(HumanCatalog humans, HumanLook look)
        {
            int i = (look.Female ? 2 : 0) + (look.DarkSkin ? 1 : 0);
            Material prepared = humans.Skins != null && i < humans.Skins.Length ? humans.Skins[i] : null;
            return prepared;
        }

        /// <summary>Sans texture de peau sombre dans le pack : la peau claire de la pièce assombrie comme la peau peinte (0,62 ; 0,50 ; 0,44), partagée.</summary>
        private static Material Darkened(Material source)
        {
            if (source == null) return null;
            if (Worn.TryGetValue((source, -1, -1), out Material done) && done != null) return done;
            var m = new Material(source) { name = source.name + " sombre" };
            ArenaView.ApplyColor(m, new Color(0.62f, 0.50f, 0.44f));
            m.enableInstancing = true;
            Worn[(source, -1, -1)] = m;
            return m;
        }

        /// <summary>Copie partagée de <paramref name="source"/> teinte (<see cref="HumanWardrobe.Color"/>) et usée.</summary>
        private static Material Worn1(Material source, int tint, int wear)
        {
            if (source == null) return null;
            if (Worn.TryGetValue((source, tint, wear), out Material done) && done != null) return done;
            (double r, double g, double b) = HumanWardrobe.Color(tint, wear);
            var m = new Material(source) { name = source.name + " " + tint + "-" + wear };
            ArenaView.ApplyColor(m, new Color((float)r, (float)g, (float)b));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", (float)HumanWardrobe.Smoothness(wear));
            m.enableInstancing = true;
            Worn[(source, tint, wear)] = m;
            return m;
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
