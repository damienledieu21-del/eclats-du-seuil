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

        public WorldEventsView()
        {
            _root = new GameObject("Événements du monde (V2.5)");
            _audio = _root.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _audio.volume = 0.55f;
        }

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
            _rifts.Clear();
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
                }
            foreach (string id in _gone)
            {
                foreach (Transform t in _rifts[id]) if (t != null) Object.Destroy(t.gameObject);
                _rifts.Remove(id);
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
                    _audio.pitch = e.Kind == WorldEventKind.Rift ? 0.45f : 1f;
                    _audio.PlayOneShot(Bell);
                }
            }
        }

        private List<Transform> BuildRift(string id, GroundVector at)
        {
            var pieces = new List<Transform>();
            var anchor = new GameObject("Faille " + id);
            anchor.transform.SetParent(_root.transform, false);
            anchor.transform.position = new Vector3(Raw(at.X), 0f, Raw(at.Z));
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
            return pieces;
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
                piece.transform.position = new Vector3((float)x, smoke ? 2f : 0.2f, (float)z);
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
                p.y += deltaTime * 0.6f;
                if (p.y > 6f) p.y = 1.5f;
                t.position = p;
                float s = 1.2f + (p.y - 1.5f) * 0.25f;
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
