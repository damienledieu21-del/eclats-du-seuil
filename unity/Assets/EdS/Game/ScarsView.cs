using System.Collections.Generic;
using EdS.Runtime;
using EdS.World;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.6 (« le Seuil qui grandit ») : les cicatrices des failles du Seuil en formes simples : un disque d'herbe pâle et froide qui
    /// grandit avec le palier, un cœur gris, des pointes sombres (trois par palier) et des grains de brume froide qui montent ; près d'elles,
    /// les couleurs se fanent et la brume se rapproche (<see cref="StyleWorld.SetScarFade"/>, d'après <see cref="AdventureSession.FadeAt"/>
    /// à la place de la caméra). Affichage seulement : rien ne change dans le monde.
    /// </summary>
    public sealed class ScarsView
    {
        private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();
        private static readonly Color Pale = new Color(0.55f, 0.6f, 0.62f), Heart = new Color(0.32f, 0.34f, 0.4f), Spike = new Color(0.12f, 0.1f, 0.18f),
            Mist = new Color(0.72f, 0.8f, 0.9f);

        private sealed class Shown
        {
            public GameObject Root;
            public Transform Disc;
            public int Stage;
            public readonly List<Transform> Motes = new List<Transform>();
        }

        private static readonly ScarView[] NoScars = new ScarView[0];

        private readonly GameObject _root;
        private readonly Dictionary<string, Shown> _scars = new Dictionary<string, Shown>();
        private readonly List<string> _gone = new List<string>();
        private float _time;
        private float _fade;

        public ScarsView()
        {
            _root = new GameObject("Cicatrices du Seuil (V2.6)");
        }

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
            _scars.Clear();
            StyleWorld.SetScarFade(0f);
        }

        /// <summary>À chaque image de l'aventure : cicatrices montrées, grains de brume, couleurs fanées autour de la caméra.</summary>
        public void Sync(AdventureSession a, Vector3 camera, float deltaTime)
        {
            if (a == null) return;
            _time += deltaTime;
            WorldSession w = a.World;
            bool outside = w.InStoryZone && w.Definition != null && !a.InScene;
            IReadOnlyList<ScarView> scars = outside ? a.Scars : NoScars;
            _gone.Clear();
            _gone.AddRange(_scars.Keys);
            for (int i = 0; i < scars.Count; i++)   // relecture de la PR #110 : sans allocation par image
            {
                ScarView v = scars[i];
                _gone.Remove(v.Key);
                if (!_scars.TryGetValue(v.Key, out Shown s) || s.Stage != v.Stage)
                {
                    if (s != null && s.Root != null) Object.Destroy(s.Root);
                    _scars[v.Key] = s = Build(v);
                }
                Animate(s, deltaTime);
            }
            foreach (string key in _gone)
            {
                if (_scars[key].Root != null) Object.Destroy(_scars[key].Root);
                _scars.Remove(key);
            }
            // Couleurs fanées à la place de la caméra (un peu lissées, pour ne pas sauter d'un coup).
            var at = new EdS.Domain.Space.GroundVector(EdS.Domain.Numerics.Fixed.FromRaw((long)(camera.x * 10000f)), EdS.Domain.Numerics.Fixed.FromRaw((long)(camera.z * 10000f)));
            float target = outside ? AdventureSession.FadeAt(scars, at) / 1000f : 0f;
            _fade = Mathf.MoveTowards(_fade, target, deltaTime * 0.5f);
            StyleWorld.SetScarFade(_fade);
        }

        private Shown Build(ScarView v)
        {
            var s = new Shown { Stage = v.Stage, Root = new GameObject("Cicatrice " + v.Key) };
            s.Root.transform.SetParent(_root.transform, false);
            s.Root.transform.position = new Vector3(Raw(v.Position.X), 0f, Raw(v.Position.Z));
            float radius = Raw(v.Radius);
            // Le disque d'herbe pâle (posé à ras du sol), plus froid à chaque palier.
            s.Disc = Piece(PrimitiveType.Cylinder, "herbe_pale", Color.Lerp(Pale, Heart, 0.15f * v.Stage), s.Root.transform);
            s.Disc.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            s.Disc.localPosition = new Vector3(0f, 0.02f, 0f);
            Transform heart = Piece(PrimitiveType.Cylinder, "coeur", Heart, s.Root.transform);
            heart.localScale = new Vector3(2.2f, 0.03f, 2.2f);
            heart.localPosition = new Vector3(0f, 0.04f, 0f);
            // Pointes sombres : trois par palier, en spirale autour du cœur.
            for (int i = 0; i < 3 * v.Stage; i++)
            {
                float angle = i * 2.39996f, d = 1.2f + (radius - 1.2f) * ((i + 1f) / (3f * v.Stage + 1f));
                Transform spike = Piece(PrimitiveType.Cube, "pointe" + i, Spike, s.Root.transform);
                float h = 0.4f + 0.25f * v.Stage + 0.3f * ((i * 7) % 5) / 5f;
                spike.localScale = new Vector3(0.18f, h, 0.18f);
                spike.localPosition = new Vector3(Mathf.Cos(angle) * d, h * 0.5f, Mathf.Sin(angle) * d);
                spike.localRotation = Quaternion.Euler(12f * ((i % 3) - 1), 37f * i, 9f * ((i % 2) * 2 - 1));
            }
            // Grains de brume froide : quatre par palier.
            for (int i = 0; i < 4 * v.Stage; i++)
            {
                Transform mote = Piece(PrimitiveType.Sphere, "brume" + i, Mist, s.Root.transform);
                float angle = i * 1.7f, d = radius * (0.2f + 0.7f * ((i * 3) % 7) / 7f);
                mote.localPosition = new Vector3(Mathf.Cos(angle) * d, 0.3f + (i % 5) * 0.5f, Mathf.Sin(angle) * d);
                mote.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                s.Motes.Add(mote);
            }
            return s;
        }

        private void Animate(Shown s, float deltaTime)
        {
            for (int i = 0; i < s.Motes.Count; i++)
            {
                Transform t = s.Motes[i];
                if (t == null) continue;
                Vector3 p = t.localPosition;
                p.y += deltaTime * (0.25f + 0.05f * (i % 4));
                if (p.y > 3.5f) p.y = 0.2f;
                p.x += Mathf.Sin(_time * 0.7f + i) * deltaTime * 0.15f;
                t.localPosition = p;
                float size = 0.25f + 0.2f * Mathf.Sin(_time + i * 0.9f) * 0.5f + 0.1f;
                t.localScale = new Vector3(size, size, size);
            }
        }

        private static Transform Piece(PrimitiveType shape, string name, Color color, Transform parent)
        {
            GameObject piece = GameObject.CreatePrimitive(shape);
            piece.name = name;
            Object.Destroy(piece.GetComponent<Collider>());
            Renderer renderer = piece.GetComponent<Renderer>();
            renderer.sharedMaterial = MaterialFor(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            piece.transform.SetParent(parent, false);
            return piece.transform;
        }

        private static float Raw(EdS.Domain.Numerics.Fixed f) => f.Raw / 10000f;

        private static Material MaterialFor(Color color)
        {
            if (Materials.TryGetValue(color, out Material m) && m != null) return m;
            m = ArenaView.MakeMaterial(color);
            Materials[color] = m;
            return m;
        }
    }
}
