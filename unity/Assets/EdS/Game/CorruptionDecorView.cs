using System.Collections.Generic;
using EdS.Runtime;
using EdS.World;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.42.2 : la corruption à l'écran (affichage seulement, d'après <see cref="AdventureSession.Corruption"/>) : dans chaque terre
    /// corrompue, arbres morts, souches, rochers fendus et cristaux sombres posés par <see cref="CorruptionLooks.Layout"/> (plus nombreux à
    /// chaque palier), et l'autel de la terre, corrompu ou purifié. Les modèles prévus (atelier, « Corruption ») sont pris par leur nom dans le
    /// catalogue (<see cref="StyleCatalog.CorruptionModel"/>) ; sans eux, des formes de remplacement de la même taille, aux teintes de la
    /// corruption (bois calciné, pierre grise, éclats violets ; autel purifié pâle et lumineux). Seules les terres dont le bord est à moins de
    /// <see cref="CorruptionLooks.ShowRange"/> m de la caméra sont construites ; une terre est reconstruite quand son palier change.
    /// </summary>
    public sealed class CorruptionDecorView
    {
        private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();
        private static readonly Color Charred = new Color(0.12f, 0.1f, 0.1f), Ash = new Color(0.38f, 0.36f, 0.36f), Stone = new Color(0.3f, 0.29f, 0.31f),
            Violet = new Color(0.42f, 0.16f, 0.62f), Pale = new Color(0.86f, 0.84f, 0.78f), Light = new Color(1f, 0.97f, 0.82f);

        private sealed class Shown
        {
            public GameObject Root;
            public int Stage;
        }

        private readonly GameObject _root;
        private readonly Dictionary<string, Shown> _shown = new Dictionary<string, Shown>();
        private readonly List<string> _gone = new List<string>();
        private readonly List<string> _seen = new List<string>();

        public CorruptionDecorView()
        {
            _root = new GameObject("Corruption (V2.42)");
        }

        public void Destroy()
        {
            foreach (Shown s in _shown.Values) if (s.Root != null) Object.Destroy(s.Root);
            _shown.Clear();
            if (_root != null) Object.Destroy(_root);
        }

        /// <summary>À chaque image de l'aventure : terres proches construites (ou reconstruites si leur palier a changé), les autres retirées.</summary>
        public void Sync(AdventureSession a, Vector3 camera)
        {
            if (a == null) return;
            IReadOnlyList<EdS.World.CorruptionView> all = a.InScene ? null : a.Corruption;
            _seen.Clear();
            int count = all == null ? 0 : all.Count;
            for (int i = 0; i < count; i++)   // boucle indexée : chaque image, sans énumérateur
            {
                EdS.World.CorruptionView v = all[i];
                float dx = Raw(v.Position.X) - camera.x, dz = Raw(v.Position.Z) - camera.z;
                float reach = (float)CorruptionLooks.ShowRange + Raw(v.Radius);
                if (dx * dx + dz * dz > reach * reach) continue;
                string key = v.Area.Id;
                _seen.Add(key);
                Shown s;
                if (!_shown.TryGetValue(key, out s) || s.Stage != v.Stage)
                {
                    if (s != null && s.Root != null) Object.Destroy(s.Root);
                    _shown[key] = Build(v);
                }
            }
            _gone.Clear();
            foreach (string key in _shown.Keys) if (!_seen.Contains(key)) _gone.Add(key);
            foreach (string key in _gone)
            {
                if (_shown[key].Root != null) Object.Destroy(_shown[key].Root);
                _shown.Remove(key);
            }
        }

        private Shown Build(EdS.World.CorruptionView v)
        {
            var s = new Shown { Stage = v.Stage, Root = new GameObject("Terre " + v.Area.Id + " (palier " + v.Stage + ")") };
            s.Root.transform.SetParent(_root.transform, false);
            IReadOnlyList<CorruptionPiece> pieces = CorruptionLooks.Layout(v);
            for (int i = 0; i < pieces.Count; i++)
            {
                CorruptionPiece p = pieces[i];
                var holder = new GameObject(p.Model + i);
                holder.transform.SetParent(s.Root.transform, false);
                holder.transform.position = StyleWorld.OnGround(new Vector3((float)p.X, 0f, (float)p.Z));
                holder.transform.rotation = Quaternion.Euler(0f, (float)p.Yaw, 0f);
                holder.transform.localScale = Vector3.one * (float)p.Scale;
                var size = CorruptionLooks.Size(p.Model);
                GameObject model = StyleCatalog.Loaded != null ? StyleCatalog.Loaded.CorruptionModel(p.Model) : null;
                if (model != null) Fit(Object.Instantiate(model), holder.transform, size);
                else Stand(p.Model, holder.transform, size);
            }
            return s;
        }

        /// <summary>
        /// Le modèle à la <b>hauteur</b> demandée à l'atelier (relecture de la PR #244 : la finition, finir_corruption.py, met déjà chaque modèle
        /// Tripo à la hauteur de sa fiche ; leur emprise au sol s'en écarte, une mise à l'échelle sur la plus grande dimension les tasserait),
        /// posé sur le sol, rangé sous <paramref name="parent"/>.
        /// </summary>
        private static void Fit(GameObject g, Transform parent, (double Length, double Width, double Height) size)
        {
            Quaternion own = g.transform.localRotation;
            Vector3 ownScale = g.transform.localScale;
            g.transform.position = Vector3.zero;
            Renderer[] renderers = g.GetComponentsInChildren<Renderer>();
            float scale = 1f, lift = 0f;
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                float tallest = b.size.y;
                if (tallest > 0.01f) scale = (float)size.Height / tallest;
                lift = -b.min.y * scale;
            }
            g.transform.SetParent(parent, false);
            g.transform.localRotation = own;
            g.transform.localScale = ownScale * scale;
            g.transform.localPosition = new Vector3(0f, lift, 0f);
        }

        /// <summary>Formes de remplacement de la taille du modèle prévu.</summary>
        private static void Stand(string model, Transform parent, (double Length, double Width, double Height) size)
        {
            float l = (float)size.Length, w = (float)size.Width, h = (float)size.Height;
            if (model == CorruptionLooks.Tree1 || model == CorruptionLooks.Tree2)
            {
                bool split = model == CorruptionLooks.Tree2;
                Transform trunk = Box(PrimitiveType.Cylinder, "tronc", Charred, parent, new Vector3(0.45f, h * 0.5f, 0.45f), new Vector3(0f, h * 0.5f, 0f));
                if (split) trunk.localRotation = Quaternion.Euler(0f, 0f, 12f);
                for (int k = 0; k < 4; k++)
                {
                    Transform branch = Box(PrimitiveType.Cube, "branche" + k, Charred, parent, new Vector3(0.12f, h * 0.35f, 0.12f),
                        new Vector3((k % 2 == 0 ? -1f : 1f) * l * 0.18f, h * (0.6f + 0.08f * k), (k < 2 ? 1f : -1f) * w * 0.12f));
                    branch.localRotation = Quaternion.Euler((k < 2 ? 1f : -1f) * 25f, 0f, (k % 2 == 0 ? 1f : -1f) * 35f);
                }
                Box(PrimitiveType.Cube, "veine", Violet, parent, new Vector3(0.08f, h * 0.35f, 0.47f), new Vector3(0f, h * 0.35f, 0f));
            }
            else if (model == CorruptionLooks.Stump)
            {
                Box(PrimitiveType.Cylinder, "souche", Ash, parent, new Vector3(w * 0.5f, h * 0.5f, w * 0.5f), new Vector3(0f, h * 0.5f, 0f));
                for (int k = 0; k < 3; k++)
                    Box(PrimitiveType.Cube, "racine" + k, Charred, parent, new Vector3(0.2f, 0.15f, l * 0.5f), new Vector3(0f, 0.08f, 0f)).localRotation = Quaternion.Euler(0f, k * 120f, 0f);
                Box(PrimitiveType.Cube, "eclat", Violet, parent, new Vector3(0.25f, 0.5f, 0.25f), new Vector3(0f, h, 0f)).localRotation = Quaternion.Euler(10f, 30f, 15f);
            }
            else if (model == CorruptionLooks.Rock1 || model == CorruptionLooks.Rock2)
            {
                Box(PrimitiveType.Sphere, "pierre", Stone, parent, new Vector3(l, h * 1.4f, w), new Vector3(0f, h * 0.35f, 0f));
                Box(PrimitiveType.Cube, "eclat1", Violet, parent, new Vector3(0.18f, h * 0.7f, 0.18f), new Vector3(l * 0.15f, h * 0.8f, 0f)).localRotation = Quaternion.Euler(0f, 0f, -20f);
                Box(PrimitiveType.Cube, "eclat2", Violet, parent, new Vector3(0.14f, h * 0.5f, 0.14f), new Vector3(-l * 0.2f, h * 0.7f, w * 0.1f)).localRotation = Quaternion.Euler(15f, 0f, 25f);
            }
            else if (model == CorruptionLooks.Crystals)
            {
                for (int k = 0; k < 4; k++)
                {
                    Transform shard = Box(PrimitiveType.Cube, "cristal" + k, k == 0 ? Charred : Violet, parent, new Vector3(0.3f, h * (1f - 0.18f * k), 0.3f),
                        new Vector3((k % 2 == 0 ? 1f : -1f) * 0.2f * k, h * (0.5f - 0.09f * k), (k < 2 ? 1f : -1f) * 0.15f * k));
                    shard.localRotation = Quaternion.Euler((k % 2 == 0 ? 1f : -1f) * 12f * k, k * 40f, (k < 2 ? 1f : -1f) * 15f * k);
                }
            }
            else
            {
                bool purified = model == CorruptionLooks.AltarPurified;
                Color stone = purified ? Pale : Stone;
                Box(PrimitiveType.Cube, "marche", stone, parent, new Vector3(l, h * 0.2f, w), new Vector3(0f, h * 0.1f, 0f));
                Box(PrimitiveType.Cube, "bloc", stone, parent, new Vector3(l * 0.7f, h * 0.6f, w * 0.6f), new Vector3(0f, h * 0.5f, 0f));
                if (purified) Box(PrimitiveType.Sphere, "lumiere", Light, parent, new Vector3(0.35f, 0.35f, 0.35f), new Vector3(0f, h * 0.95f, 0f));
                else Box(PrimitiveType.Cube, "cristal", Violet, parent, new Vector3(0.3f, 0.6f, 0.3f), new Vector3(0f, h * 1.05f, 0f)).localRotation = Quaternion.Euler(10f, 45f, 8f);
            }
        }

        private static Transform Box(PrimitiveType shape, string name, Color color, Transform parent, Vector3 scale, Vector3 position)
        {
            GameObject piece = GameObject.CreatePrimitive(shape);
            piece.name = name;
            Object.Destroy(piece.GetComponent<Collider>());
            piece.GetComponent<Renderer>().sharedMaterial = MaterialFor(color);
            piece.transform.SetParent(parent, false);
            piece.transform.localScale = scale;
            piece.transform.localPosition = position;
            return piece.transform;
        }

        private static float Raw(EdS.Domain.Numerics.Fixed f) => f.Raw / 10000f;

        private static Material MaterialFor(Color color)
        {
            Material m;
            if (Materials.TryGetValue(color, out m) && m != null) return m;
            m = ArenaView.MakeMaterial(color);
            Materials[color] = m;
            return m;
        }
    }
}
