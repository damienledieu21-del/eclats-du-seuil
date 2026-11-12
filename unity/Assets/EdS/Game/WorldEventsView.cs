using System.Collections.Generic;
using System.Linq;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.5 (événements du monde) : la faille du Seuil en formes simples (<see cref="EventLooks.Rift"/> : un cœur pâle, une couronne
    /// d'éclats sombres qui tournent, des lueurs qui montent), la fumée et les caisses brisées d'un hameau pillé
    /// (<see cref="EventLooks.Aftermath"/>), la cloche du hameau au départ d'une attaque et le cri de la faille (son de synthèse, sans
    /// fichier). Affichage seulement : rien ne change dans le monde.
    /// </summary>
    public sealed class WorldEventsView
    {
        private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();
        private static AudioClip _bell;

        private readonly GameObject _root;
        private readonly AudioSource _audio;
        private readonly Dictionary<string, List<Transform>> _rifts = new Dictionary<string, List<Transform>>();
        private readonly Dictionary<string, GameObject> _aftermaths = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, List<Transform>> _smokes = new Dictionary<string, List<Transform>>();
        private readonly List<string> _gone = new List<string>();
        private int _heard;
        private float _time;

        // Sous-lot V2.9 (vrais sons déposés par Damien) : tocsin frappé vite au hameau attaqué (deux sources qui se relaient, pour que la
        // cloche résonne entre deux coups), cloche d'église quand le hameau est défendu, verre qui éclate à l'ouverture d'une faille,
        // bourdonnement en boucle de chaque faille ouverte, miroir qui explose quand elle se referme. Sans fichier : la cloche faite par le code.
        private readonly AudioSource[] _bells = new AudioSource[2];
        private readonly AudioSource _burst;
        private readonly AudioSource _church;   // relecture de la PR #114 : à part, le tocsin ne la partage plus
        private readonly Dictionary<string, AudioSource> _hums = new Dictionary<string, AudioSource>();
        private readonly Dictionary<string, Vector3> _riftAt = new Dictionary<string, Vector3>();
        private int _strikesLeft;
        private float _nextStrike;
        private int _strike;
        private float _churchLength;
        private float _churchStarted;
        private int _churches;

        // Sous-lot V2.9.1 : la déchirure lumineuse de chaque faille (shader « EdS/Faille »), sa lumière qui bat, les lueurs aspirées ; plus
        // grande sur une cicatrice, selon son palier (EchoLooks.Rift). Sans le shader : les formes simples de la V2.5 seules.
        private sealed class Tear
        {
            public Transform Quad;
            public Material Material;
            public Light Light;
            public float Pull;
            public Transform Portal;   // sous-lot V2.11 : le cadre de portail de nickbroad, tourné avec la déchirure
        }

        private readonly Dictionary<string, Tear> _tears = new Dictionary<string, Tear>();
        private static readonly Color PullColor = new Color(0.75f, 0.5f, 1f, 0.8f);

        /// <summary>Sous-lot V2.9.1 : les effets en particules (lueurs aspirées par les failles) ; null : sans particules.</summary>
        public Effects Effects { get; set; }

        /// <summary>Matériau modèle des déchirures, emporté dans le jeu construit (<c>EdsBuildShaders</c>).</summary>
        public const string TearMaterialName = "EdsFaille";

        private static Material _tearTemplate;
        private static bool _tearLoaded;

        // Relecture de la PR #115 : identifiants pris une fois, caméra gardée.
        internal static readonly int OpeningId = Shader.PropertyToID("_Opening"), IntensityId = Shader.PropertyToID("_Intensity");
        private Camera _camera;

        /// <summary>Sous-lot V2.9.1 : le matériau modèle de « EdS/Faille », ou null s'il manque ou n'est pas pris en charge.</summary>
        internal static Material TearTemplate
        {
            get
            {
                if (_tearLoaded) return _tearTemplate;
                _tearLoaded = true;
                // Relecture de la PR #115 : sous-shader pour URP seulement (« FallBack Off ») : hors d'URP ou non pris en charge, formes simples.
                if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null) return _tearTemplate = null;
                Material loaded = Resources.Load<Material>(TearMaterialName);
                _tearTemplate = loaded != null && loaded.shader != null && loaded.shader.isSupported ? loaded : null;
                if (_tearTemplate == null)
                {
                    Shader shader = Shader.Find("EdS/Faille");
                    if (shader != null && shader.isSupported) _tearTemplate = new Material(shader);
                }
                return _tearTemplate;
            }
        }

        /// <summary>Sous-lot V2.9.1 : une copie du matériau des déchirures, réglée (null sans le shader). À détruire avec son objet.</summary>
        internal static Material NewTear(float seed, bool distort)
        {
            Material template = TearTemplate;
            if (template == null) return null;
            var m = new Material(template) { name = "Déchirure du Seuil" };
            m.SetFloat("_Seed", seed);
            // Distorsion de la scène seulement si le rendu actif garde l'image opaque de la scène (ordinateur, EdsStyle) : sans elle,
            // SampleSceneColor rendrait du noir (relecture de la PR #115) ; jamais sur téléphone.
            m.SetFloat("_Distort", distort && !Application.isMobilePlatform && OpaqueTexture ? 1f : 0f);
            m.renderQueue = 3050;
            return m;
        }

        /// <summary>Relecture de la PR #115 : le rendu URP actif garde-t-il l'image opaque de la scène ?</summary>
        private static bool OpaqueTexture =>
            UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp
            && urp.supportsCameraOpaqueTexture;

        /// <summary>Sous-lot V2.9.1 : un quad de déchirure sous <paramref name="parent"/>, sans ombre.</summary>
        internal static Transform TearQuad(string name, Transform parent, Material material)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.Destroy(quad.GetComponent<Collider>());
            Renderer renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            quad.transform.SetParent(parent, false);
            return quad.transform;
        }

        public WorldEventsView()
        {
            _root = new GameObject("Événements du monde (V2.5)");
            _audio = _root.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _audio.volume = 0.55f;
            for (int i = 0; i < _bells.Length; i++) _bells[i] = Source3D("Tocsin " + i, 0.7f);
            _burst = Source3D("Verre des failles", 1f);
            _church = Source3D("Cloche d'église", 0.7f);
        }

        private AudioSource Source3D(string name, float blend)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = blend;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 4f;
            source.maxDistance = Hearing(1f);
            return source;
        }

        /// <summary>Distance d'écoute réglée dans le catalogue (60 m sans lui), multipliée par <paramref name="factor"/>.</summary>
        private static float Hearing(float factor) => (StyleCatalog.Loaded != null ? StyleCatalog.Loaded.BeastHearing : 60f) * factor;

        private static float Volume => StyleCatalog.Loaded != null ? StyleCatalog.Loaded.BeastVolume : 0.8f;

        private static AudioClip Clip(StyleSound sound) => StyleCatalog.Loaded != null ? StyleCatalog.Loaded.Sound(sound, 0) : null;

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
            _rifts.Clear();
            foreach (Tear t in _tears.Values)
                if (t.Material != null) Object.Destroy(t.Material);
            _tears.Clear();
            _aftermaths.Clear();
            _smokes.Clear();
        }

        private static AudioClip Bell
        {
            get
            {
                if (_bell != null) return _bell;
                float[] data = EventLooks.Bell(22050);
                _bell = AudioClip.Create("cloche_du_hameau", data.Length, 1, 22050, false);
                _bell.SetData(data, 0);
                return _bell;
            }
        }

        /// <summary>À chaque image de l'aventure : failles ouvertes, conséquences d'un pillage, sons des annonces.</summary>
        public void Sync(AdventureSession a, float deltaTime)
        {
            if (a == null) return;
            _session = a;
            _time += deltaTime;
            WorldSession w = a.World;
            bool outside = w.InStoryZone && w.Definition != null;
            // Failles ouvertes.
            _gone.Clear();
            _gone.AddRange(_rifts.Keys);
            if (outside)
                foreach (WorldEventView v in w.WorldEvents)
                {
                    if (!(v.Rift is GroundVector at)) continue;
                    _gone.Remove(v.Event.Id);
                    if (!_rifts.TryGetValue(v.Event.Id, out List<Transform> pieces)) _rifts[v.Event.Id] = pieces = BuildRift(v.Event.Id, at);
                    float opening = Mathf.Clamp01((float)((w.Zone.Engine.CurrentTick - v.StartTick) / 60.0));   // trois secondes pour s'ouvrir
                    ShowRift(pieces, opening);
                    ShowTear(v.Event.Id, pieces, opening, EchoLooks.ScarStageAt(a.Scars, Raw(at.X), Raw(at.Z)), deltaTime);
                    if (pieces.Count > 0 && pieces[0] != null) _riftAt[v.Event.Id] = pieces[0].position;
                    Hum(v.Event.Id, pieces, opening);
                }
            foreach (string id in _gone)
            {
                foreach (Transform t in _rifts[id]) if (t != null) Object.Destroy(t.gameObject);
                _rifts.Remove(id);
                if (_tears.TryGetValue(id, out Tear tear) && tear.Material != null) Object.Destroy(tear.Material);
                _tears.Remove(id);
                _hums.Remove(id);   // la source était sur l'ancre, détruite avec elle
            }
            // Conséquences d'une attaque manquée : fumée et débris au lieu de l'événement.
            if (outside)
                foreach (WorldEventDefinition e in w.Definition.Events)
                {
                    if (e.Place == null) continue;
                    bool show = w.Aftermath(e.Id);
                    _aftermaths.TryGetValue(e.Id, out GameObject shown);
                    if (show && shown == null) _aftermaths[e.Id] = BuildAftermath(e, w.Definition);
                    else if (!show && shown != null)
                    {
                        Object.Destroy(shown);
                        _aftermaths.Remove(e.Id);
                        _smokes.Remove(e.Id);
                    }
                    else if (show && _smokes.TryGetValue(e.Id, out List<Transform> smokes)) AnimateSmoke(smokes, deltaTime);
                }
            // Sons : la cloche au départ d'une attaque, la cloche grave et lente (un cri de verre) à l'ouverture d'une faille.
            if (a.EventsAnnounced != _heard)
            {
                _heard = a.EventsAnnounced;
                WorldEventNotice n = a.LastEventNotice;
                WorldEventDefinition e = n == null || w.Definition == null ? null : w.Definition.EventOf(n.Event);
                if (e != null && (n.Kind == WorldEventNoticeKind.Started || n.Kind == WorldEventNoticeKind.Wave) && e.Kind != WorldEventKind.Hunt)
                {
                    if (!Announce(e, n.Kind, w.Definition))
                    {
                        _audio.pitch = e.Kind == WorldEventKind.Rift ? 0.45f : 1f;
                        _audio.PlayOneShot(Bell);
                    }
                }
                else if (e != null && e.Kind == WorldEventKind.Rift && (n.Kind == WorldEventNoticeKind.Won || n.Kind == WorldEventNoticeKind.Lost))
                    Burst(StyleSound.Shatter, _riftAt.TryGetValue(e.Id, out Vector3 at) ? at : (Vector3?)null, 1f);
                else if (e != null && e.Kind == WorldEventKind.Raid && n.Kind == WorldEventNoticeKind.Won) Church(e, w.Definition);
            }
            Ring(deltaTime);
        }

        /// <summary>
        /// Sous-lot V2.9 : le son d'un départ avec les fichiers déposés : tocsin au hameau attaqué (au départ seulement), verre qui éclate à la
        /// faille qui s'ouvre (et à chaque vague, plus bas). Faux sans fichier (la cloche faite par le code sonne alors).
        /// </summary>
        private bool Announce(WorldEventDefinition e, WorldEventNoticeKind kind, ZoneDefinition zone)
        {
            if (e.Kind == WorldEventKind.Rift)
            {
                Vector3? at = null;
                foreach (WorldEventView v in AdventureEvents(zone))
                    if (v.Event.Id == e.Id && v.Rift is GroundVector g) at = new Vector3(Raw(g.X), 1f, Raw(g.Z));
                // 2e vague : un portail qui s'ouvre (au départ seulement), avec le verre qui éclate.
                bool portal = kind == WorldEventNoticeKind.Started && Burst(StyleSound.Portal, at, 0.8f);
                // Sous-lot V2.9.2 (3e vague) : un souffle d'énergie magique quand la faille s'ouvre et à chaque vague qu'elle crache.
                bool surge = Burst(StyleSound.RiftSurge, at, kind == WorldEventNoticeKind.Wave ? 0.6f : 0.8f);
                return Burst(StyleSound.Glass, at, kind == WorldEventNoticeKind.Wave ? 0.5f : 1f) || portal || surge;
            }
            if (Clip(StyleSound.Tocsin) == null) return false;
            if (kind != WorldEventNoticeKind.Started) return true;   // une vague d'un pillage : le tocsin sonne déjà
            Vector3 center = PlaceCenter(e, zone) ?? Vector3.zero;
            foreach (AudioSource bell in _bells)
            {
                bell.transform.position = center + Vector3.up * 6f;
                bell.maxDistance = Hearing(3f);
            }
            // Sous-lot V2.9.2 : la grosse cloche de la tour (3e vague) sonne sept coups lents, la petite neuf coups rapides.
            (int strikes, double gap) = BeastCalls.TocsinRhythm(Clip(StyleSound.Tocsin).name);
            _strikesLeft = strikes;
            _strikeGap = (float)gap;
            _nextStrike = 0f;
            return true;
        }

        /// <summary>Les failles vues à cette image (null sans session) : pour placer le verre qui éclate.</summary>
        private IReadOnlyList<WorldEventView> AdventureEvents(ZoneDefinition zone) => _session != null ? _session.World.WorldEvents : NoEvents;

        private static readonly WorldEventView[] NoEvents = new WorldEventView[0];
        private AdventureSession _session;
        private float _strikeGap = (float)BeastCalls.TocsinGap;

        /// <summary>Centre du lieu de l'événement (le hameau), ou null.</summary>
        private static Vector3? PlaceCenter(WorldEventDefinition e, ZoneDefinition zone)
        {
            if (zone == null || e.Place == null) return null;
            foreach (Place p in zone.Places)
                if (p.Id == e.Place) return new Vector3((Raw(p.Min.X) + Raw(p.Max.X)) * 0.5f, 0f, (Raw(p.Min.Z) + Raw(p.Max.Z)) * 0.5f);
            return null;
        }

        /// <summary>Un son court de faille (verre, miroir) à sa place (ou à la caméra) ; faux sans fichier.</summary>
        private bool Burst(StyleSound sound, Vector3? at, float volume)
        {
            AudioClip clip = Clip(sound);
            if (clip == null || _burst == null) return false;
            _burst.spatialBlend = at.HasValue ? 1f : 0f;
            if (at.HasValue) _burst.transform.position = at.Value;
            _burst.maxDistance = Hearing(2f);
            _burst.PlayOneShot(clip, volume * Volume);
            return true;
        }

        /// <summary>
        /// Bourdonnement d'une faille ouverte : une source en boucle sur son ancre, qui monte avec l'ouverture (début du fichier pris d'après
        /// la faille, pour que deux failles ne bourdonnent pas à l'unisson).
        /// </summary>
        private void Hum(string id, List<Transform> pieces, float opening)
        {
            if (!_hums.TryGetValue(id, out AudioSource hum))
            {
                AudioClip clip = Clip(StyleSound.RiftHum);
                if (clip == null || pieces.Count == 0 || pieces[0] == null) return;
                hum = pieces[0].gameObject.AddComponent<AudioSource>();
                hum.playOnAwake = false;
                hum.loop = true;
                hum.spatialBlend = 1f;
                hum.dopplerLevel = 0f;
                hum.rolloffMode = AudioRolloffMode.Linear;
                hum.minDistance = 3f;
                hum.maxDistance = Hearing(0.7f);
                hum.clip = clip;
                hum.time = clip.length * StyleRules.Variant(id, 100) / 100f * 0.9f;
                hum.volume = 0f;
                hum.Play();
                _hums[id] = hum;
            }
            if (hum != null) hum.volume = 0.55f * Volume * opening;
        }

        /// <summary>Tocsin : un coup toutes les <see cref="BeastCalls.TocsinGap"/> secondes, sur deux sources qui se relaient.</summary>
        private void Ring(float deltaTime)
        {
            if (_churchLength > 0f)
            {
                // Relecture de la PR #114 : temps réel (timeScale à 0 en pause : le passage finit quand même).
                float t = Time.unscaledTime - _churchStarted;
                AudioSource church = _church;
                if (t >= _churchLength || church == null)
                {
                    if (church != null) church.Stop();
                    _churchLength = 0f;
                }
                else church.volume = 0.8f * Volume * (float)BeastCalls.Envelope(t, _churchLength);
            }
            if (_strikesLeft <= 0) return;
            _nextStrike -= deltaTime;
            if (_nextStrike > 0f) return;
            AudioClip clip = Clip(StyleSound.Tocsin);
            if (clip == null)
            {
                _strikesLeft = 0;
                return;
            }
            AudioSource bell = _bells[_strike++ % _bells.Length];
            bell.clip = clip;
            bell.time = 0f;
            bell.volume = 0.9f * Volume;
            bell.Play();
            _strikesLeft--;
            _nextStrike = _strikeGap;
        }

        /// <summary>Le hameau est défendu : la cloche d'église sonne une douzaine de secondes (un passage du long enregistrement).</summary>
        private void Church(WorldEventDefinition e, ZoneDefinition zone)
        {
            // 2e vague : une des cloches d'église déposées (quatre), mélangées.
            AudioClip clip = StyleCatalog.Loaded != null ? StyleCatalog.Loaded.Sound(StyleSound.ChurchBell, BeastCalls.Pick(_churches, 1 << 20)) : null;
            if (clip == null) return;
            AudioSource bell = _church;
            bell.transform.position = (PlaceCenter(e, zone) ?? Vector3.zero) + Vector3.up * 6f;
            bell.maxDistance = Hearing(3f);
            (double start, double length) = BeastCalls.Cue(clip.name, clip.length, _churches++, 12);
            bell.clip = clip;
            bell.time = Mathf.Clamp((float)start, 0f, Mathf.Max(0f, clip.length - 0.05f));
            bell.volume = 0.8f * Volume * (float)BeastCalls.Envelope(0, length);
            bell.Play();
            _churchStarted = Time.unscaledTime;
            _churchLength = (float)length;
        }

        private List<Transform> BuildRift(string id, GroundVector at)
        {
            var pieces = new List<Transform>();
            var anchor = new GameObject("Faille " + id);
            anchor.transform.SetParent(_root.transform, false);
            anchor.transform.position = StyleWorld.OnGround(new Vector3(Raw(at.X), 0f, Raw(at.Z)));   // V2.24 : sur le sol affiché
            pieces.Add(anchor.transform);
            foreach (WildlifePart part in EventLooks.Rift(0))
            {
                GameObject piece = GameObject.CreatePrimitive(part.Shape == WildlifeShape.Cube ? PrimitiveType.Cube : PrimitiveType.Sphere);
                piece.name = part.Name;
                Object.Destroy(piece.GetComponent<Collider>());
                Renderer renderer = piece.GetComponent<Renderer>();
                renderer.sharedMaterial = MaterialFor(new Color((float)part.R, (float)part.G, (float)part.B), part.Name.StartsWith("eclat") ? 0f : 1.4f);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                piece.transform.SetParent(anchor.transform, false);
                pieces.Add(piece.transform);
            }
            var light = new GameObject("Lueur de la faille").AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.6f, 0.45f, 1f);
            light.range = 9f;
            light.intensity = 2.2f;
            light.transform.SetParent(anchor.transform, false);
            light.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            // Sous-lot V2.9.1 : la déchirure remplace le cœur pâle et son bord (les éclats sombres qui tournent restent).
            Material material = NewTear(StyleRules.Variant(id, 1000) / 37f, true);
            if (material != null)
            {
                Transform quad = TearQuad("Déchirure", anchor.transform, material);
                quad.localPosition = new Vector3(0f, 1.6f, 0f);
                quad.localScale = new Vector3(1.6f, 3.6f, 1f);
                for (int i = 1; i < pieces.Count; i++)
                    if (pieces[i] != null && (pieces[i].name == "coeur" || pieces[i].name == "bord")) pieces[i].gameObject.SetActive(false);
                _tears[id] = new Tear { Quad = quad, Material = material, Light = light };
            }
            else _tears[id] = new Tear { Light = light };
            // Sous-lot V2.11 (5e vague) : un cadre de portail lumineux autour de la déchirure, et deux touffes de cristaux à ses pieds.
            GameObject portal = Wave5View.Place(anchor.transform, Wave5Role.Portal, "Portail " + id, anchor.transform.position, 0f);
            if (portal != null)
            {
                portal.transform.SetParent(anchor.transform, true);
                _tears[id].Portal = portal.transform;
            }
            for (int i = 0; i < 2; i++)
            {
                Vector3 side = anchor.transform.position + new Vector3(i == 0 ? -2.2f : 2.1f, 0f, i == 0 ? 0.6f : -0.5f);
                GameObject crystals = Wave5View.Place(anchor.transform, Wave5Role.Crystals, "Cristaux " + id + i, side, StyleRules.Variant(id + i, 360), 0.9f);
                if (crystals != null) crystals.transform.SetParent(anchor.transform, true);
            }
            return pieces;
        }

        /// <summary>
        /// Sous-lot V2.9.1 : la faille grandit avec le palier de la cicatrice où elle s'ouvre, sa lumière bat, la déchirure se tourne vers la
        /// caméra (autour de l'axe vertical), des lueurs sont aspirées vers elle.
        /// </summary>
        private void ShowTear(string id, List<Transform> pieces, float opening, int stage, float deltaTime)
        {
            if (!_tears.TryGetValue(id, out Tear tear) || pieces.Count == 0 || pieces[0] == null) return;
            Transform anchor = pieces[0];
            (double scale, double hz, double pullRate, double light) = EchoLooks.Rift(stage, Application.isMobilePlatform);
            float k = (float)scale;
            anchor.localScale = new Vector3(k, k, k);
            float pulse = (float)EchoLooks.Pulse(_time + StyleRules.Variant(id, 100) * 0.13f, hz);
            if (tear.Light != null)
            {
                tear.Light.intensity = (float)light * pulse * opening;
                tear.Light.range = 9f * k;
            }
            if (tear.Material != null)
            {
                tear.Material.SetFloat(OpeningId, opening);
                tear.Material.SetFloat(IntensityId, 0.7f + 0.5f * pulse);
            }
            if (_camera == null) _camera = Camera.main;
            Camera camera = _camera;
            if (tear.Quad != null && camera != null)
            {
                Vector3 to = camera.transform.position - tear.Quad.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f) tear.Quad.rotation = Quaternion.LookRotation(Vector3.zero - to);   // le quad de Unity se voit de face depuis -z
                if (to.sqrMagnitude > 0.01f && tear.Portal != null) tear.Portal.rotation = Quaternion.LookRotation(to);   // V2.11 : le portail, de face
            }
            if (Effects == null || deltaTime <= 0f || opening <= 0f) return;
            tear.Pull += deltaTime * (float)pullRate * opening;
            Vector3 center = anchor.position + Vector3.up * (1.6f * k);
            while (tear.Pull >= 1f)
            {
                tear.Pull -= 1f;
                Effects.Pull(center, 2.6f * k, PullColor);
            }
        }

        private void ShowRift(List<Transform> pieces, float opening)
        {
            IReadOnlyList<WildlifePart> parts = EventLooks.Rift(_time, opening);
            for (int i = 0; i < parts.Count && i + 1 < pieces.Count; i++)
            {
                Transform t = pieces[i + 1];
                if (t == null) continue;
                WildlifePart p = parts[i];
                t.localPosition = new Vector3((float)p.X, (float)p.Y, (float)p.Z);
                t.localRotation = Quaternion.Euler((float)p.Pitch, 0f, 0f);
                t.localScale = new Vector3((float)p.ScaleX, (float)p.ScaleY, (float)p.ScaleZ);
            }
        }

        private GameObject BuildAftermath(WorldEventDefinition e, ZoneDefinition zone)
        {
            var root = new GameObject("Pillage " + e.Id);
            root.transform.SetParent(_root.transform, false);
            Place place = zone.Places.FirstOrDefault(p => p.Id == e.Place);
            if (place == null) return root;
            var smokes = new List<Transform>();
            _smokes[e.Id] = smokes;
            int n = 0;
            foreach ((string kind, double x, double z) in EventLooks.Aftermath(place))
            {
                bool smoke = kind == "fumee";
                GameObject piece = GameObject.CreatePrimitive(smoke ? PrimitiveType.Sphere : PrimitiveType.Cube);
                piece.name = kind + n++;
                Object.Destroy(piece.GetComponent<Collider>());
                (double R, double G, double B) c = smoke ? EventLooks.Smoke : EventLooks.Debris;
                piece.GetComponent<Renderer>().sharedMaterial = MaterialFor(new Color((float)c.R, (float)c.G, (float)c.B), 0f);
                piece.transform.SetParent(root.transform, false);
                piece.transform.position = StyleWorld.OnGround(new Vector3((float)x, smoke ? 2f : 0.2f, (float)z));   // V2.24
                piece.transform.localScale = smoke ? new Vector3(1.6f, 3.5f, 1.6f) : new Vector3(0.7f, 0.4f, 0.5f);
                piece.transform.rotation = Quaternion.Euler(smoke ? 0f : 18f * n, 37f * n, smoke ? 0f : 9f * n);
                if (smoke) smokes.Add(piece.transform);
            }
            return root;
        }

        private static void AnimateSmoke(List<Transform> smokes, float deltaTime)
        {
            foreach (Transform t in smokes)
            {
                if (t == null) continue;
                Vector3 p = t.position;
                float ground = StyleWorld.StandHeight(p.x, p.z);   // V2.24 : la fumée monte au-dessus du sol affiché
                p.y += deltaTime * 0.6f;
                if (p.y - ground > 6f) p.y = ground + 1.5f;
                t.position = p;
                float s = 1.2f + (p.y - ground - 1.5f) * 0.25f;
                t.localScale = new Vector3(s, 3.5f, s);
            }
        }

        private static float Raw(EdS.Domain.Numerics.Fixed f) => f.Raw / 10000f;

        private static Material MaterialFor(Color color, float glow)
        {
            if (Materials.TryGetValue(color, out Material m) && m != null) return m;
            m = ArenaView.MakeMaterial(color);
            if (glow > 0f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * glow);
            }
            Materials[color] = m;
            return m;
        }
    }
}
