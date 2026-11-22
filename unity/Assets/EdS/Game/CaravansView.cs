using System.Collections.Generic;
using EdS.Runtime;
using EdS.World;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.41.2 : les caravanes sur les routes (affichage seulement, d'après <see cref="AdventureSession.Caravans"/>) : chariots bâchés,
    /// attelages et coffres de marchandises posés par <see cref="CaravanLooks.Layout"/>. Les modèles prévus (atelier, « Caravanes ») sont pris
    /// par leur nom dans le catalogue (<see cref="StyleCatalog.CaravanModel"/>) ; sans eux, des formes simples de la même taille : plateau et
    /// roues de bois, bâche claire, deux bœufs bruns, coffres cerclés. Seules les caravanes à moins de <see cref="CaravanLooks.ShowRange"/> m de
    /// la caméra sont construites.
    /// </summary>
    public sealed class CaravansView
    {
        private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();
        private static readonly Color Wood = new Color(0.45f, 0.31f, 0.18f), Canvas = new Color(0.86f, 0.82f, 0.7f), Ox = new Color(0.36f, 0.24f, 0.15f),
            Iron = new Color(0.25f, 0.25f, 0.27f), Cloth = new Color(0.62f, 0.16f, 0.14f);

        private sealed class Shown
        {
            public GameObject Root;
            public CaravanState State;
        }

        private readonly GameObject _root;
        private readonly Dictionary<string, Shown> _shown = new Dictionary<string, Shown>();
        private readonly List<string> _gone = new List<string>();
        private readonly List<string> _seen = new List<string>();

        /// <summary>Sous-lot V2.41.3 : coffres à reprendre au fort affichés, par clé (<see cref="FortChest.Key"/>).</summary>
        private readonly Dictionary<string, GameObject> _chests = new Dictionary<string, GameObject>();

        public CaravansView()
        {
            _root = new GameObject("Caravanes (V2.41)");
        }

        public void Destroy()
        {
            foreach (Shown s in _shown.Values) if (s.Root != null) Object.Destroy(s.Root);
            _shown.Clear();
            foreach (GameObject g in _chests.Values) if (g != null) Object.Destroy(g);
            _chests.Clear();
            if (_root != null) Object.Destroy(_root);
        }

        /// <summary>À chaque image de l'aventure : caravanes proches construites, posées sur le sol affiché et tournées dans le sens de la marche.</summary>
        public void Sync(AdventureSession a, Vector3 camera)
        {
            if (a == null) return;
            IReadOnlyList<CaravanView> list = a.Caravans;
            _seen.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                CaravanView v = list[i];
                float x = Raw(v.Position.X), z = Raw(v.Position.Z);
                float dx = x - camera.x, dz = z - camera.z;
                if (dx * dx + dz * dz > (float)(CaravanLooks.ShowRange * CaravanLooks.ShowRange)) continue;
                string key = v.Caravan.Id + "/" + v.Day;
                _seen.Add(key);
                Shown s;
                if (!_shown.TryGetValue(key, out s) || s.State != v.State)
                {
                    if (s != null && s.Root != null) Object.Destroy(s.Root);
                    _shown[key] = s = Build(v, key);
                }
                Place(s.Root.transform, v, x, z);
            }
            _gone.Clear();
            foreach (string key in _shown.Keys) if (!_seen.Contains(key)) _gone.Add(key);
            foreach (string key in _gone)
            {
                if (_shown[key].Root != null) Object.Destroy(_shown[key].Root);
                _shown.Remove(key);
            }
            SyncChests(a);
        }

        /// <summary>
        /// Sous-lot V2.41.3 (revue Codex de la PR #243) : les coffres à reprendre au fort (<see cref="AdventureSession.FortChests"/>), posés sur
        /// le sol affiché ; un coffre repris ou une quête fermée retire le sien (instance détruite).
        /// </summary>
        private void SyncChests(AdventureSession a)
        {
            IReadOnlyList<FortChest> chests = a.FortChests;
            _seen.Clear();
            for (int i = 0; i < chests.Count; i++)
            {
                FortChest c = chests[i];
                _seen.Add(c.Key);
                if (_chests.ContainsKey(c.Key) && _chests[c.Key] != null) continue;
                var holder = new GameObject("Coffre a reprendre " + c.Key);
                holder.transform.SetParent(_root.transform, false);
                holder.transform.position = StyleWorld.OnGround(new Vector3((float)c.X, 0f, (float)c.Z));
                var size = CaravanLooks.Size(CaravanLooks.Goods);
                GameObject model = StyleCatalog.Loaded != null ? StyleCatalog.Loaded.CaravanModel(CaravanLooks.Goods) : null;
                if (model != null) Fit(Object.Instantiate(model), holder.transform, size);
                else Stand(CaravanLooks.Goods, holder.transform, size);
                _chests[c.Key] = holder;
            }
            _gone.Clear();
            foreach (string key in _chests.Keys) if (!_seen.Contains(key)) _gone.Add(key);
            foreach (string key in _gone)
            {
                if (_chests[key] != null) Object.Destroy(_chests[key]);
                _chests.Remove(key);
            }
        }

        private static void Place(Transform root, CaravanView v, float x, float z)
        {
            float hx = Raw(v.Heading.X), hz = Raw(v.Heading.Z);
            float yaw = hx == 0f && hz == 0f ? 0f : Mathf.Atan2(hx, hz) * Mathf.Rad2Deg;
            root.position = new Vector3(x, 0f, z);
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            // Chaque pièce sur le sol affiché, sous sa propre place.
            for (int i = 0; i < root.childCount; i++)
            {
                Transform piece = root.GetChild(i);
                Vector3 p = piece.position;
                piece.position = new Vector3(p.x, StyleWorld.OnGround(new Vector3(p.x, 0f, p.z)).y, p.z);
            }
        }

        private Shown Build(CaravanView v, string key)
        {
            var s = new Shown { State = v.State, Root = new GameObject("Caravane " + key) };
            s.Root.transform.SetParent(_root.transform, false);
            IReadOnlyList<CaravanPiece> pieces = CaravanLooks.Layout(v);
            for (int i = 0; i < pieces.Count; i++)
            {
                CaravanPiece p = pieces[i];
                var holder = new GameObject(p.Model + i);
                holder.transform.SetParent(s.Root.transform, false);
                holder.transform.localPosition = new Vector3((float)p.Side, 0f, (float)p.Forward);
                holder.transform.localRotation = Quaternion.Euler(0f, (float)p.Yaw, 0f);
                var body = new GameObject("corps");
                body.transform.SetParent(holder.transform, false);
                body.transform.localRotation = Quaternion.Euler(0f, 0f, (float)p.Roll);
                var size = CaravanLooks.Size(p.Model);
                // Couché sur le flanc : la moitié de la largeur au-dessus du sol au lieu de rien.
                body.transform.localPosition = new Vector3(0f, p.Roll != 0 ? (float)size.Width * 0.5f : 0f, 0f);
                GameObject model = StyleCatalog.Loaded != null ? StyleCatalog.Loaded.CaravanModel(p.Model) : null;
                if (model != null) Fit(Object.Instantiate(model), body.transform, size);
                else Stand(p.Model, body.transform, size);
            }
            return s;
        }

        /// <summary>
        /// Le modèle à la taille demandée à l'atelier (sa longueur), posé sur le sol, puis rangé sous <paramref name="parent"/>. Relecture
        /// indépendante de la PR #241 : les bornes sont mesurées à l'identité, avant le rangement sous une pièce tournée (chariot couché,
        /// coffres de travers), sinon l'échelle et la hauteur seraient fausses.
        /// </summary>
        private static void Fit(GameObject g, Transform parent, (double Length, double Width, double Height) size)
        {
            // Sans parent, à l'origine ; la rotation et l'échelle propres du modèle (celles de son import) sont gardées.
            Quaternion own = g.transform.localRotation;
            Vector3 ownScale = g.transform.localScale;
            g.transform.position = Vector3.zero;
            Renderer[] renderers = g.GetComponentsInChildren<Renderer>();
            float scale = 1f, lift = 0f;
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                float longest = Mathf.Max(b.size.x, b.size.z);
                if (longest > 0.01f) scale = (float)size.Length / longest;
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
            if (model == CaravanLooks.Wagon)
            {
                Box(PrimitiveType.Cube, "plateau", Wood, parent, new Vector3(w * 0.9f, 0.25f, l * 0.85f), new Vector3(0f, 0.85f, 0f));
                Box(PrimitiveType.Cube, "bache", Canvas, parent, new Vector3(w * 0.88f, h - 1.1f, l * 0.7f), new Vector3(0f, 1.0f + (h - 1.1f) * 0.5f, -0.1f));
                Box(PrimitiveType.Cube, "timon", Wood, parent, new Vector3(0.12f, 0.12f, 1.4f), new Vector3(0f, 0.6f, l * 0.5f + 0.5f));
                for (int k = 0; k < 4; k++)
                {
                    float sx = (k % 2 == 0 ? -1f : 1f) * w * 0.5f, sz = (k < 2 ? 1f : -1f) * l * 0.3f;
                    Transform wheel = Box(PrimitiveType.Cylinder, "roue" + k, Wood, parent, new Vector3(1.1f, 0.06f, 1.1f), new Vector3(sx, 0.55f, sz));
                    wheel.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }
            else if (model == CaravanLooks.Team)
            {
                for (int k = 0; k < 2; k++)
                {
                    float sx = (k == 0 ? -1f : 1f) * w * 0.25f;
                    Box(PrimitiveType.Cube, "boeuf" + k, Ox, parent, new Vector3(w * 0.4f, h * 0.55f, l * 0.75f), new Vector3(sx, h * 0.55f, 0f));
                    Box(PrimitiveType.Cube, "tete" + k, Ox, parent, new Vector3(w * 0.25f, h * 0.3f, l * 0.2f), new Vector3(sx, h * 0.75f, l * 0.45f));
                }
                Box(PrimitiveType.Cube, "joug", Wood, parent, new Vector3(w * 0.95f, 0.12f, 0.15f), new Vector3(0f, h * 0.9f, l * 0.35f));
            }
            else
            {
                Box(PrimitiveType.Cube, "coffre1", Wood, parent, new Vector3(w * 0.5f, h * 0.45f, l * 0.4f), new Vector3(-w * 0.2f, h * 0.225f, -l * 0.2f));
                Box(PrimitiveType.Cube, "coffre2", Wood, parent, new Vector3(w * 0.45f, h * 0.4f, l * 0.35f), new Vector3(w * 0.25f, h * 0.2f, l * 0.25f));
                Box(PrimitiveType.Cube, "cercle", Iron, parent, new Vector3(w * 0.52f, 0.05f, l * 0.42f), new Vector3(-w * 0.2f, h * 0.35f, -l * 0.2f));
                Box(PrimitiveType.Cylinder, "drap", Cloth, parent, new Vector3(0.3f, l * 0.3f, 0.3f), new Vector3(0f, h * 0.6f, 0f)).localRotation = Quaternion.Euler(90f, 0f, 0f);
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
