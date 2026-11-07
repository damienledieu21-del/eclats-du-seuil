using System;
using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.2.1 : essai du compagnon sous Unity (menu « Essai du compagnon »). Affichage seulement : la partie, le sac et la sauvegarde
    /// ne changent pas.
    /// </summary>
    public static class CompanionTrial
    {
        /// <summary>Compagnon montré même sans son objet au sac (identifiant du catalogue), ou null.</summary>
        public static string Forced { get; set; }
    }

    /// <summary>
    /// Sous-lot V2.2.1 : les compagnons d'affichage (<see cref="Companions"/>) : une bête suit le joueur tant que son objet est dans le sac.
    /// Ni combat, ni sauvegarde : le mouvement vient de <see cref="CompanionFollower"/> (portable, testé), la bête est faite de formes
    /// simples, sans collision (rien ne la heurte, la caméra la traverse), et part en rapetissant quand l'objet quitte le sac.
    /// </summary>
    public sealed class CompanionsView
    {
        private readonly Dictionary<string, (CompanionFollower Follower, CatBody Body)> _shown = new Dictionary<string, (CompanionFollower, CatBody)>();
        private readonly List<string> _gone = new List<string>();
        private string _place;
        private float _night;
        private int _meows;
        private int _purrs;

        /// <summary>
        /// Une image. <paramref name="active"/> : compagnons à montrer ; <paramref name="hidden"/> : mise en scène (souvenir) en cours, la bête
        /// se cache et reparaît ensuite derrière le joueur ; <paramref name="place"/> : la zone et le voyage montrés (une porte ou un voyage
        /// la replace derrière le joueur) ; <paramref name="deltaTime"/> : 0 en pause.
        /// </summary>
        public void Sync(IReadOnlyList<CompanionDefinition> active, Transform player, bool hidden, bool combat, bool night, string place, float deltaTime)
        {
            bool newPlace = place != _place;
            _place = place;
            _night = Mathf.MoveTowards(_night, night ? 1f : 0f, deltaTime * 0.25f);
            foreach (CompanionDefinition definition in active)
            {
                if (_shown.TryGetValue(definition.Id, out var kept)) kept.Follower.Stay();
                else _shown[definition.Id] = (new CompanionFollower(definition), new CatBody(definition));
            }
            _gone.Clear();
            foreach (KeyValuePair<string, (CompanionFollower Follower, CatBody Body)> entry in _shown)
            {
                CompanionFollower follower = entry.Value.Follower;
                CatBody body = entry.Value.Body;
                bool wanted = false;
                foreach (CompanionDefinition d in active) wanted |= d.Id == entry.Key;
                if (!wanted) follower.Leave();
                if (newPlace || hidden || player == null) follower.Replace();
                if (hidden || player == null)
                {
                    body.Show(false);
                    if (follower.Leaving) _gone.Add(entry.Key);   // parti pendant la scène : rien à montrer
                    continue;
                }
                Vector3 at = player.position;
                follower.Step(at.x, at.z, player.eulerAngles.y, deltaTime, combat);
                if (follower.Gone)
                {
                    _gone.Add(entry.Key);
                    continue;
                }
                // Hauteur : celle des pieds du joueur (son corps est placé 1 m plus haut, comme ArenaView.Feet : revue Codex de la PR #102),
                // plus la différence du sol (le pont cintré) entre lui et la bête.
                float x = (float)follower.X, z = (float)follower.Z;
                float y = at.y - ArenaView.FeetOffset + StyleWorld.StandHeight(x, z) - StyleWorld.StandHeight(at.x, at.z);
                body.Show(follower.Placed);   // relecture de la PR #102 : jamais à une place périmée
                if (!follower.Placed) continue;
                body.Apply(follower, new Vector3(x, y, z), _night, deltaTime);
                Play(body, follower.Cue);
            }
            foreach (string id in _gone)
            {
                _shown[id].Body.Destroy();
                _shown.Remove(id);
            }
        }

        private void Play(CatBody body, CompanionCue cue)
        {
            switch (cue)
            {
                case CompanionCue.Appear:
                case CompanionCue.Meow:
                    // Sous-lot V2.9 : un des miaulements déposés, mélangés mais toujours dans le même ordre (BeastCalls.Pick) ; un vrai
                    // miaulement garde presque sa hauteur, celui fait par le code varie davantage.
                    AudioClip meow = Clip(StyleSound.Meow, BeastCalls.Pick(_meows, 1 << 20));
                    float pitch = meow == CatSounds.Meow ? 0.92f + 0.06f * (_meows % 4) : 0.97f + 0.02f * (_meows % 4);
                    _meows++;
                    body.Meow(meow, cue == CompanionCue.Appear ? 0.3f : 0.4f, pitch);
                    break;
                case CompanionCue.Purr:
                    body.Purr(Clip(StyleSound.Purr, _purrs), _purrs++);
                    break;
            }
        }

        /// <summary>Le son déposé par Damien (mots « miaou », « ronron »…), sinon celui fait par le code.</summary>
        private AudioClip Clip(StyleSound sound, int variant)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            AudioClip clip = catalog != null ? catalog.Sound(sound, variant) : null;
            if (clip != null) return clip;
            return sound == StyleSound.Purr ? CatSounds.Purr : CatSounds.Meow;
        }

        public void Destroy()
        {
            foreach (var entry in _shown.Values) entry.Body.Destroy();
            _shown.Clear();
            _place = null;
        }
    }

    /// <summary>Sous-lot V2.2.1 : miaulement et ronronnement faits par le code (une fois), quand Damien n'a déposé aucun fichier.</summary>
    internal static class CatSounds
    {
        private static AudioClip _meow;
        private static AudioClip _purr;

        public static AudioClip Meow => _meow != null ? _meow : (_meow = MakeMeow());

        public static AudioClip Purr => _purr != null ? _purr : (_purr = MakePurr());

        /// <summary>« Mi-a-ou » : une voix qui monte puis retombe, des harmoniques qui s'ouvrent (le « a ») puis se ferment (le « ou »).</summary>
        private static AudioClip MakeMeow()
        {
            const int rate = 22050;
            const float seconds = 0.62f;
            int n = (int)(rate * seconds);
            var data = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / rate;
                double u = t / seconds;
                double f = 520 + 420 * Math.Sin(Math.PI * Math.Min(1.0, u * 1.35)) - 120 * Math.Max(0, u - 0.6);
                phase += 2 * Math.PI * f / rate;
                double open = Math.Sin(Math.PI * Math.Min(1.0, u * 1.2));   // la bouche s'ouvre puis se ferme
                double env = Math.Min(1.0, t / 0.04) * Math.Min(1.0, (seconds - t) / 0.12);
                data[i] = (float)(env * (Math.Sin(phase) + 0.55 * open * Math.Sin(2 * phase) + 0.35 * open * Math.Sin(3 * phase)
                    + 0.15 * open * Math.Sin(4 * phase)));
            }
            return Make("Miaulement (V2.2.1)", data, rate, 0.8f);
        }

        /// <summary>Ronronnement : un grondement grave (souffle filtré) battu environ 26 fois par seconde, en deux souffles.</summary>
        private static AudioClip MakePurr()
        {
            const int rate = 22050;
            const float seconds = 2.6f;
            int n = (int)(rate * seconds);
            var data = new float[n];
            uint noise = 22050u;
            double low = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / rate;
                unchecked { noise = noise * 1664525u + 1013904223u; }
                double white = (noise >> 8) / (double)(1 << 24) * 2 - 1;
                low += (white - low) * 0.04;   // souffle grave
                double beat = 0.5 + 0.5 * Math.Sin(2 * Math.PI * 26 * t);
                double breath = 0.55 + 0.45 * Math.Sin(2 * Math.PI * t / 1.3 - Math.PI / 2);   // inspire, expire
                double env = Math.Min(1.0, t / 0.3) * Math.Min(1.0, (seconds - t) / 0.4);
                data[i] = (float)(env * breath * beat * (low * 3 + 0.25 * Math.Sin(2 * Math.PI * 52 * t)));
            }
            return Make("Ronronnement (V2.2.1)", data, rate, 0.7f);
        }

        private static AudioClip Make(string name, float[] data, int rate, float peak)
        {
            float max = 0f;
            foreach (float v in data) max = Mathf.Max(max, Mathf.Abs(v));
            if (max > 0f)
                for (int i = 0; i < data.Length; i++) data[i] *= peak / max;
            AudioClip clip = AudioClip.Create(name, data.Length, 1, rate, false);
            if (clip != null) clip.SetData(data, 0);
            return clip;
        }
    }

    /// <summary>
    /// Sous-lot V2.2.1 : un chat fait de formes simples (capsules et sphères, sans collision, sur le calque « Ignore Raycast »), animé
    /// d'après la posture : pattes qui marchent au rythme du chemin parcouru, queue dressée qui ondule, assis la queue autour des pattes,
    /// toilette, regard qui fait le tour, étirement, couché en boule ; yeux qui luisent un peu la nuit. Les postures se fondent en douceur.
    /// </summary>
    internal sealed class CatBody
    {
        private const int TailSegments = 6;
        private const float Leg = 0.2f;

        private readonly GameObject _root;
        private readonly Transform _scale;
        private readonly Transform _hips;
        private readonly Transform _head;
        private readonly Transform[] _legs = new Transform[4];   // avant gauche, avant droite, arrière gauche, arrière droite
        private readonly Transform[] _tail = new Transform[TailSegments];
        private readonly Transform[] _ears = new Transform[2];
        private readonly List<Material> _materials = new List<Material>();
        private Material _eyes;
        private readonly Color _eyeColor;
        private readonly AudioSource _voice;
        private readonly AudioSource _purr;   // relecture de la PR #102 : à part, le pitch des miaulements ne le change pas
        private readonly float _length;
        private float _clock;

        // Sous-lot V2.9 : le vrai chat (catalogue), animé par ses clips ; sans lui, le chat en formes simples.
        private readonly Animation _animation;
        private readonly string[] _poseClips = new string[6];   // marche, course, attente, queue, regard, tapi
        private string _playing;
        private AnimationState _playingState;
        private float _purrStarted;
        private float _purrLength;

        private const int ClipWalk = 0, ClipRun = 1, ClipIdle = 2, ClipTail = 3, ClipLook = 4, ClipCrouch = 5;
        private static readonly string[] ClipKeys = { "walk", "run", "idle", "idletailflick", "idlelookaround", "crouchidle" };

        // Posture affichée (fondue vers la posture voulue).
        private float _hipY = Leg, _pitch, _front, _hind, _headPitch, _headYaw, _tailLift = 40f, _tailCurl, _tailSide, _paw, _curl;

        public CatBody(CompanionDefinition definition)
        {
            _length = (float)definition.Length;
            Color coat = Tint(definition.Coat), marks = Tint(definition.Marks);
            _eyeColor = Tint(definition.Eyes);

            _root = new GameObject("Compagnon : " + definition.Name);
            _root.layer = 2;   // « Ignore Raycast »
            _scale = Node("Taille", _root.transform, Vector3.zero);
            // Sous-lot V2.9 : le vrai chat du catalogue (Stripe the Cat), teint aux couleurs du compagnon ; sinon les formes simples.
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog != null && catalog.Cat != null)
            {
                _animation = Model(catalog, definition, coat, marks);
                _voice = Voice(_root, 0.75f);
                _purr = Voice(_root, 0.85f);
                _scale.localScale = Vector3.zero;
                return;
            }
            Material fur = Track(ArenaView.MakeMaterial(coat));
            Material dark = Track(ArenaView.MakeMaterial(marks));
            Material cream = Track(ArenaView.MakeMaterial(Color.Lerp(coat, Color.white, 0.65f)));
            Material nose = Track(ArenaView.MakeMaterial(new Color(0.85f, 0.45f, 0.5f)));
            _eyes = Track(ArenaView.MakeMaterial(_eyeColor));
            _hips = Node("Hanches", _scale, new Vector3(0f, Leg, -_length * 0.4f));

            // Le corps : une capsule couchée, un ventre clair, trois rayures sur le dos.
            Part(PrimitiveType.Capsule, _hips, new Vector3(0f, 0.03f, _length * 0.42f), new Vector3(0.19f, _length * 0.55f, 0.17f), fur, new Vector3(90f, 0f, 0f));
            Part(PrimitiveType.Capsule, _hips, new Vector3(0f, -0.015f, _length * 0.42f), new Vector3(0.15f, _length * 0.45f, 0.13f), cream, new Vector3(90f, 0f, 0f));
            for (int i = 0; i < 3; i++)
                Part(PrimitiveType.Cube, _hips, new Vector3(0f, 0.115f, _length * (0.2f + 0.2f * i)), new Vector3(0.15f, 0.012f, 0.035f), dark, Vector3.zero);

            // La tête : ronde, deux oreilles pointues, un museau clair, un nez rose, deux yeux.
            _head = Node("Tête", _hips, new Vector3(0f, 0.09f, _length * 0.85f));
            Part(PrimitiveType.Sphere, _head, new Vector3(0f, 0.04f, 0.05f), new Vector3(0.16f, 0.14f, 0.15f), fur, Vector3.zero);
            Part(PrimitiveType.Sphere, _head, new Vector3(0f, 0.01f, 0.12f), new Vector3(0.08f, 0.055f, 0.06f), cream, Vector3.zero);
            Part(PrimitiveType.Sphere, _head, new Vector3(0f, 0.035f, 0.15f), new Vector3(0.022f, 0.016f, 0.016f), nose, Vector3.zero);
            for (int side = 0; side < 2; side++)
            {
                float s = side == 0 ? -1f : 1f;
                _ears[side] = Node("Oreille", _head, new Vector3(0.045f * s, 0.1f, 0.04f));
                Part(PrimitiveType.Cube, _ears[side], new Vector3(0f, 0.015f, 0f), new Vector3(0.045f, 0.045f, 0.015f), fur, new Vector3(0f, 0f, 45f));
                Part(PrimitiveType.Sphere, _head, new Vector3(0.035f * s, 0.06f, 0.115f), new Vector3(0.025f, 0.03f, 0.012f), _eyes, Vector3.zero);
            }

            // Les pattes : un pivot à l'épaule ou à la hanche, une capsule qui pend, un bout clair.
            Vector3[] roots =
            {
                new Vector3(-0.055f, -0.01f, _length * 0.72f), new Vector3(0.055f, -0.01f, _length * 0.72f),
                new Vector3(-0.06f, -0.01f, 0.05f), new Vector3(0.06f, -0.01f, 0.05f),
            };
            for (int i = 0; i < 4; i++)
            {
                _legs[i] = Node("Patte", _hips, roots[i]);
                Part(PrimitiveType.Capsule, _legs[i], new Vector3(0f, -Leg * 0.5f, 0f), new Vector3(0.05f, Leg * 0.55f, 0.05f), fur, Vector3.zero);
                Part(PrimitiveType.Sphere, _legs[i], new Vector3(0f, -Leg + 0.01f, 0.01f), new Vector3(0.055f, 0.035f, 0.065f), cream, Vector3.zero);
            }

            // La queue : six morceaux, chacun accroché au précédent, le bout foncé.
            Transform parent = _hips;
            Vector3 at = new Vector3(0f, 0.06f, -0.02f);
            for (int i = 0; i < TailSegments; i++)
            {
                _tail[i] = Node("Queue", parent, at);
                float r = 0.042f - 0.004f * i;
                Part(PrimitiveType.Capsule, _tail[i], new Vector3(0f, 0f, -0.035f), new Vector3(r, 0.045f, r), i == TailSegments - 1 ? dark : fur, new Vector3(90f, 0f, 0f));
                parent = _tail[i];
                at = new Vector3(0f, 0f, -0.065f);
            }

            _voice = Voice(_root, 0.75f);   // on l'entend de son côté, sans qu'il disparaisse quand la caméra recule
            _purr = Voice(_root, 0.85f);
            _scale.localScale = Vector3.zero;
        }

        private static AudioSource Voice(GameObject root, float blend)
        {
            AudioSource source = root.AddComponent<AudioSource>();
            if (source == null) return null;
            source.playOnAwake = false;
            source.spatialBlend = blend;
            source.loop = false;
            source.pitch = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;   // sous-lot V2.9 : entendu jusqu'à 30 m
            source.minDistance = 2f;
            source.maxDistance = 30f;
            return source;
        }

        /// <summary>
        /// Sous-lot V2.9 : le modèle du chat, à la taille du compagnon (<see cref="StyleCatalog.CatHeight"/> pour 48 cm de long), centré,
        /// sans collision, teint (pelage : <c>MainColor</c> ; rayures : <c>DarkerColor</c> ; yeux : <c>Glass</c>, qui luisent la nuit) ;
        /// ses clips (marche, course, attentes, tapi) tournent en boucle. Rend son composant Animation, ou null.
        /// </summary>
        private Animation Model(StyleCatalog catalog, CompanionDefinition definition, Color coat, Color marks)
        {
            float height = catalog.CatHeight * Mathf.Max(0.2f, _length / 0.48f);
            GameObject look = StyleLook.Fit(catalog.Cat, height, 0f, null, false, out float bottom, out GameObject _, true);
            float k = look.transform.localScale.y;
            look.transform.SetParent(_scale, false);
            look.transform.localPosition = new Vector3(0f, -bottom * k, 0f);
            look.transform.localRotation = Quaternion.Euler(0f, catalog.CatYaw, 0f);
            foreach (Transform t in look.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            foreach (Renderer r in look.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                foreach (Material m in r.materials)
                {
                    Track(m);
                    string name = m.name;
                    if (name.StartsWith("Glass", StringComparison.Ordinal))
                    {
                        ArenaView.ApplyColor(m, _eyeColor);
                        _eyes = m;
                    }
                    else if (!catalog.TintCat) continue;
                    else if (name.StartsWith("MainColor", StringComparison.Ordinal)) ArenaView.ApplyColor(m, coat);
                    else if (name.StartsWith("DarkerColor", StringComparison.Ordinal)) ArenaView.ApplyColor(m, marks);
                }
            }
            Animation animation = look.GetComponentInChildren<Animation>();
            if (animation == null) return null;
            foreach (AnimationState state in animation)
            {
                state.wrapMode = WrapMode.Loop;
                string name = state.name;
                int bar = name.LastIndexOf('|');
                string key = StyleRules.Key(bar >= 0 ? name.Substring(bar + 1) : name);
                for (int i = 0; i < ClipKeys.Length; i++)
                    if (key == ClipKeys[i] && _poseClips[i] == null) _poseClips[i] = name;
            }
            // Un clip absent prend l'attente, puis la marche, puis le premier.
            string any = null;
            foreach (AnimationState state in animation)
            {
                any = state.name;
                break;
            }
            for (int i = 0; i < _poseClips.Length; i++)
                if (_poseClips[i] == null) _poseClips[i] = _poseClips[ClipIdle] ?? _poseClips[ClipWalk] ?? any;
            return animation;
        }

        public void Show(bool shown)
        {
            if (_root != null && _root.activeSelf != shown) _root.SetActive(shown);
        }

        public void Apply(CompanionFollower f, Vector3 at, float night, float deltaTime)
        {
            _clock += deltaTime;
            _root.transform.position = at;
            _root.transform.rotation = Quaternion.Euler(0f, (float)f.Yaw, 0f);
            // Apparition : elle grandit avec un petit rebond ; départ : elle rapetisse.
            float p = (float)f.Presence;
            float pop = f.Leaving ? p : p * (1f + 0.18f * Mathf.Sin(Mathf.PI * p));
            _scale.localScale = new Vector3(pop, pop, pop);
            FadePurr();
            if (_hips == null)
            {
                Animate(f, night);
                return;
            }

            Target(f, out float hipY, out float pitch, out float front, out float hind, out float headPitch, out float headYaw,
                out float tailLift, out float tailCurl, out float tailSide, out float paw, out float curl);
            float k = 1f - Mathf.Exp(-8f * deltaTime);
            _hipY = Mathf.Lerp(_hipY, hipY, k);
            _pitch = Mathf.Lerp(_pitch, pitch, k);
            _front = Mathf.Lerp(_front, front, k);
            _hind = Mathf.Lerp(_hind, hind, k);
            _headPitch = Mathf.Lerp(_headPitch, headPitch, k);
            _headYaw = Mathf.Lerp(_headYaw, headYaw, k);
            _tailLift = Mathf.Lerp(_tailLift, tailLift, k);
            _tailCurl = Mathf.Lerp(_tailCurl, tailCurl, k);
            _tailSide = Mathf.Lerp(_tailSide, tailSide, k);
            _paw = Mathf.Lerp(_paw, paw, k);
            _curl = Mathf.Lerp(_curl, curl, k);

            // La marche : pattes en diagonale (avant gauche avec arrière droite), au rythme du chemin parcouru ; un petit balancement.
            float gait = Mathf.Clamp01((float)f.Gait * 2.2f);
            float phase = (float)(f.Stride / (_length * 1.1f)) * Mathf.PI * 2f;
            float swing = 32f * gait;
            float bob = 0.012f * gait * Mathf.Abs(Mathf.Sin(phase));
            _hips.localPosition = new Vector3(0f, _hipY + bob, -_length * 0.4f);
            _hips.localRotation = Quaternion.Euler(_pitch, _curl * 0.3f, -_curl * 0.15f);
            _legs[0].localRotation = Quaternion.Euler(_front + swing * Mathf.Sin(phase) - _paw, 0f, 0f);
            _legs[1].localRotation = Quaternion.Euler(_front - swing * Mathf.Sin(phase), 0f, 0f);
            _legs[2].localRotation = Quaternion.Euler(_hind - swing * Mathf.Sin(phase), 0f, 0f);
            _legs[3].localRotation = Quaternion.Euler(_hind + swing * Mathf.Sin(phase), 0f, 0f);
            float groomNod = f.Pose == CompanionPose.Groom ? 8f * Mathf.Sin(_clock * 9f) : 0f;
            _head.localRotation = Quaternion.Euler(_headPitch + groomNod - 3f * gait * Mathf.Sin(phase * 2f), _headYaw, 0f);

            // Les oreilles : un frémissement de temps en temps.
            float twitch = Mathf.Sin(_clock * 0.9f) > 0.97f ? 18f * Mathf.Sin(_clock * 40f) : 0f;
            _ears[0].localRotation = Quaternion.Euler(0f, 0f, 8f + twitch);
            _ears[1].localRotation = Quaternion.Euler(0f, 0f, -8f);

            // La queue : chaque morceau ajoute sa courbe et une onde qui la parcourt (plus vive en marchant).
            float wave = Mathf.Lerp(10f, 6f, gait);
            for (int i = 0; i < TailSegments; i++)
            {
                float lift = i == 0 ? _tailLift : _tailCurl;
                float side = _tailSide + wave * Mathf.Sin(_clock * (1.6f + 2.4f * gait) - i * 0.7f);
                _tail[i].localRotation = Quaternion.Euler(lift, side, 0f);
            }

            // Les yeux luisent un peu la nuit.
            if (_eyes != null)
            {
                _eyes.EnableKeyword("_EMISSION");
                if (_eyes.HasProperty("_EmissionColor")) _eyes.SetColor("_EmissionColor", _eyeColor * (0.15f + 1.6f * night));
            }
        }

        /// <summary>La posture voulue (angles en degrés ; X positif : le nez ou la patte vers le bas ou vers l'avant).</summary>
        private void Target(CompanionFollower f, out float hipY, out float pitch, out float front, out float hind, out float headPitch, out float headYaw,
            out float tailLift, out float tailCurl, out float tailSide, out float paw, out float curl)
        {
            hipY = Leg;
            pitch = 0f;
            front = 0f;
            hind = 0f;
            headPitch = 0f;
            headYaw = 0f;
            tailLift = 55f;    // queue dressée…
            tailCurl = -9f;    // …au bout recourbé
            tailSide = 0f;
            paw = 0f;
            curl = 0f;
            float t = (float)f.PoseSeconds;
            switch (f.Pose)
            {
                case CompanionPose.Walk:
                    tailLift = Mathf.Lerp(55f, 30f, (float)f.Gait);   // au trot, la queue s'allonge
                    tailCurl = -4f;
                    break;
                case CompanionPose.Stand:
                    headPitch = -8f;
                    break;
                case CompanionPose.Sit:
                case CompanionPose.Groom:
                case CompanionPose.Look:
                    // Assis : l'arrière au sol, l'avant relevé, les pattes avant droites, la queue enroulée autour des pattes.
                    hipY = 0.06f;
                    pitch = -38f;
                    front = 38f;
                    hind = -50f;
                    headPitch = 22f;   // la tête revient à l'horizontale
                    tailLift = 28f;    // le corps penché de 38° : la queue part presque à plat vers l'arrière, posée au sol…
                    tailCurl = 2f;
                    tailSide = 26f;    // …et fait le tour des pattes
                    if (f.Pose == CompanionPose.Groom)
                    {
                        headPitch = 55f;
                        headYaw = -20f;
                        paw = 70f;   // la patte avant gauche levée vers la bouche
                    }
                    else if (f.Pose == CompanionPose.Look)
                    {
                        headYaw = 55f * Mathf.Sin(t * 1.4f);
                        headPitch = 12f;
                    }
                    break;
                case CompanionPose.Stretch:
                    // L'avant bas, les pattes avant allongées devant, l'arrière haut, la queue en l'air.
                    float s = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.6f));
                    hipY = Leg + 0.02f * s;
                    pitch = 20f * s;
                    front = -60f * s;
                    headPitch = -10f * s;
                    tailLift = 70f;
                    break;
                case CompanionPose.Curl:
                    // En boule : bas, les pattes repliées sous lui, la tête tournée vers la queue qui fait le tour.
                    hipY = 0.07f;
                    front = -80f;
                    hind = -80f;
                    headPitch = 25f;
                    headYaw = -70f;
                    tailLift = -8f;
                    tailCurl = 2f;
                    tailSide = -32f;
                    curl = 40f;
                    break;
            }
        }

        public void Meow(AudioClip clip, float volume, float pitch)
        {
            if (_voice == null || clip == null) return;
            _voice.pitch = pitch;
            _voice.PlayOneShot(clip, volume);
        }

        /// <summary>
        /// Sous-lot V2.9 : le vrai chat suit sa posture avec ses clips : marche ou course (cadence d'après sa vitesse), attente debout, queue
        /// qui fouette assis ou en s'étirant, regard qui fait le tour, tapi pour la boule ; yeux qui luisent la nuit.
        /// </summary>
        private void Animate(CompanionFollower f, float night)
        {
            if (_eyes != null)
            {
                _eyes.EnableKeyword("_EMISSION");
                if (_eyes.HasProperty("_EmissionColor")) _eyes.SetColor("_EmissionColor", _eyeColor * (0.15f + 1.6f * night));
            }
            if (_animation == null) return;
            float speed = (float)f.Speed;
            int pose;
            switch (f.Pose)
            {
                case CompanionPose.Walk: pose = speed < 0.05f ? ClipIdle : speed < 2.5f ? ClipWalk : ClipRun; break;
                case CompanionPose.Sit:
                case CompanionPose.Stretch: pose = ClipTail; break;
                case CompanionPose.Look: pose = ClipLook; break;
                case CompanionPose.Curl: pose = ClipCrouch; break;
                default: pose = ClipIdle; break;
            }
            string clip = _poseClips[pose];
            if (clip == null) return;
            if (clip != _playing)
            {
                _playing = clip;
                _playingState = _animation[clip];
                _animation.CrossFade(clip, 0.25f);
            }
            if (_playingState != null)
                _playingState.speed = pose == ClipWalk ? Mathf.Clamp(speed / 0.9f, 0.6f, 1.6f) : pose == ClipRun ? Mathf.Clamp(speed / 4f, 0.7f, 1.6f) : 1f;
        }

        /// <summary>
        /// Ronronnement : un long enregistrement (plus de 8 s) n'est joué que par passages de 7 s, avec un fondu (<see cref="BeastCalls.Cue"/>) ;
        /// le ronronnement fait par le code, en entier.
        /// </summary>
        public void Purr(AudioClip clip, int n)
        {
            if (_purr == null || clip == null) return;
            if (clip.length <= 8f)
            {
                _purr.PlayOneShot(clip, 0.35f);
                return;
            }
            (double start, double length) = BeastCalls.Cue(clip.name, clip.length, n, 7);
            _purr.clip = clip;
            _purr.volume = 0.35f * (float)BeastCalls.Envelope(0, length);
            _purr.time = Mathf.Clamp((float)start, 0f, Mathf.Max(0f, clip.length - 0.05f));
            _purr.Play();
            _purrStarted = Time.unscaledTime;   // relecture de la PR #114 : le passage finit même en pause (timeScale à 0)
            _purrLength = (float)length;
        }

        private void FadePurr()
        {
            if (_purrLength <= 0f || _purr == null) return;
            float t = Time.unscaledTime - _purrStarted;
            if (t >= _purrLength)
            {
                _purr.Stop();
                _purr.volume = 1f;
                _purrLength = 0f;
                return;
            }
            _purr.volume = 0.35f * (float)BeastCalls.Envelope(t, _purrLength);
        }

        public void Destroy()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
            foreach (Material m in _materials) UnityEngine.Object.Destroy(m);
            _materials.Clear();
        }

        private Material Track(Material m)
        {
            _materials.Add(m);
            return m;
        }

        private static Color Tint(StyleTint t) => new Color((float)t.R, (float)t.G, (float)t.B);

        private Transform Node(string name, Transform parent, Vector3 local)
        {
            var node = new GameObject(name);
            node.layer = 2;
            node.transform.SetParent(parent, false);
            node.transform.localPosition = local;
            return node.transform;
        }

        private void Part(PrimitiveType type, Transform parent, Vector3 local, Vector3 scale, Material material, Vector3 euler)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            UnityEngine.Object.Destroy(part.GetComponent<Collider>());   // ni collision, ni gêne pour la caméra
            part.layer = 2;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = local;
            part.transform.localRotation = Quaternion.Euler(euler.x, euler.y, euler.z);
            part.transform.localScale = scale;
            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }
}
