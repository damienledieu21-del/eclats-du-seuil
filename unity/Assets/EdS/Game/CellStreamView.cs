using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using EdS.Data.Zones;
using EdS.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.27 (docs/PLAN_GRANDE_CARTE.md, § 6) : montre dans Unity le terrain de la région chargé par cases autour de la caméra
    /// (<see cref="RegionTerrain"/> décide et construit, sans Unity). Réglages selon la classe de la machine (<see cref="DeviceQuality.Tier"/>,
    /// <see cref="StreamingProfile"/>). Au plus <see cref="StreamingProfile.BudgetMs"/> ms de construction par image ; une case et son bloc
    /// lointain changent à la même image (jamais de trou). Objets et maillages <b>recyclés</b> : une case quittée rend son objet, la suivante
    /// le reprend. Montrer un changement (copie des maillages) est le premier pas d'une image, compté dans son budget. Le terrain est gardé tant que la zone ne change pas : ressortir d'une maison ne le refait pas. Hauteurs : le relief
    /// affiché de la zone (<see cref="StyleWorld.Relief"/>, V2.24) ; le terrain peint de la zone reste au-dessus (<see cref="SpaceView.Covered"/>).
    /// Affichage seulement.
    /// </summary>
    public sealed class CellStreamView : ITerrainSink
    {
        private static CellStreamView _current;

        private readonly string _key;
        private readonly GameObject _root;
        private readonly RegionTerrain _terrain;
        private readonly Material _material;
        private readonly Dictionary<RegionCell, MeshRenderer> _cells = new Dictionary<RegionCell, MeshRenderer>();
        private readonly Stack<MeshRenderer> _pool = new Stack<MeshRenderer>();
        private readonly Dictionary<int, MeshRenderer> _far = new Dictionary<int, MeshRenderer>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly Stopwatch _watch = new Stopwatch();
        private Vector3[] _vertices = new Vector3[0];
        private Vector3[] _normals = new Vector3[0];
        private Vector2[] _uvs = new Vector2[0];
        private float _worstMs, _lastMs;
        private int _frames;

        private CellStreamView(string key, RegionGrid grid, StreamingProfile profile, GroundRelief relief, Rect covered)
        {
            _key = key;
            _root = new GameObject("Terrain de la région");
            (double, double, double, double)? hole = null;
            if (covered.width > 0f && covered.height > 0f) hole = (covered.x, covered.y, covered.x + covered.width, covered.y + covered.height);
            _terrain = new RegionTerrain(grid, profile, (x, z) => relief.Height(x, z), hole);
            _material = ArenaView.MakeMaterial(new Color(0.36f, 0.45f, 0.26f));
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog != null && catalog.GrassColor != null)
            {
                _material.SetTexture("_BaseMap", catalog.GrassColor);
                _material.SetTexture("_MainTex", catalog.GrassColor);
                _material.SetColor("_BaseColor", new Color(0.82f, 0.86f, 0.78f));
            }
            _material.SetFloat("_Smoothness", 0.05f);
        }

        /// <summary>
        /// Zone affichée : <paramref name="map"/> dehors avec une région (schéma de zone 13), sinon null (pièce, arène) : le terrain est caché
        /// (gardé). <paramref name="covered"/> : rectangle du sol de la zone (x, z, largeur, profondeur).
        /// </summary>
        public static void Use(AdventureMap map, Rect covered)
        {
            Forget();
            if (map == null || map.Indoor || map.Region == null)
            {
                if (_current != null) _current._root.SetActive(false);
                StyleWorld.ViewDistance = 0f;
                return;
            }
            StreamingProfile profile = StreamingProfile.For(DeviceQuality.Tier);
            GroundRelief relief = StyleWorld.Relief;
            string key = map.Signature + "|" + profile.Tier + "|" + (relief.IsFlat ? "plat" : "relief") + "|"
                + covered.x.ToString(CultureInfo.InvariantCulture) + "," + covered.y.ToString(CultureInfo.InvariantCulture) + ","
                + covered.width.ToString(CultureInfo.InvariantCulture) + "," + covered.height.ToString(CultureInfo.InvariantCulture);
            if (_current != null && _current._key != key)
            {
                _current.Destroy();
                _current = null;
            }
            if (_current == null)
            {
                _current = new CellStreamView(key, map.Region, profile, relief, covered);
                // Chargement de la zone (l'écran se construit déjà) : terrain lointain et anneaux autour du centre de l'aire, sans budget.
                _current._terrain.Update((map.MinX + map.MaxX) / 2, (map.MinZ + map.MaxZ) / 2, Time.unscaledTime);
                _current._terrain.Warm(_current);
            }
            _current._root.SetActive(true);
            StyleWorld.ViewDistance = (float)profile.ViewMeters;
        }

        /// <summary>Une image : la caméra en <paramref name="camera"/> ; construit au plus le budget de temps de la machine.</summary>
        public static void Frame(Vector3 camera)
        {
            Forget();
            CellStreamView view = _current;
            if (view == null || !view._root.activeSelf) return;
            view._terrain.Update(camera.x, camera.z, Time.unscaledTime);
            double budget = view._terrain.Profile.BudgetMs;
            view._watch.Reset();
            view._watch.Start();
            Stopwatch watch = view._watch;
            view._terrain.Work(() => watch.Elapsed.TotalMilliseconds >= budget, view);
            view._watch.Stop();
            view._lastMs = (float)view._watch.Elapsed.TotalMilliseconds;
            // Pire image des 300 dernières (environ 10 s), pour la ligne de mesure.
            if (++view._frames % 300 == 0) view._worstMs = 0f;
            view._worstMs = Mathf.Max(view._worstMs, view._lastMs);
        }

        /// <summary>Ligne de mesure (vérification, essais sur téléphone et PC modeste) : cases, mémoire, temps de construction.</summary>
        public static string Stats
        {
            get
            {
                CellStreamView v = _current;
                if (v == null) return "terrain de la région : aucun";
                CellStreamer s = v._terrain.Streamer;
                return string.Format(CultureInfo.InvariantCulture,
                    "cases {0} (ancrage {1}, détail prêt : {2}), {3:0.0} Mo, dernière image {4:0.00} ms, pire {5:0.00} ms (budget {6} ms), {7} changements",
                    s.Loaded.Count, s.Anchor.Name, s.DetailReady ? "oui" : "non", s.Bytes / 1048576.0, v._lastMs, v._worstMs, v._terrain.Profile.BudgetMs,
                    v._terrain.Commits);
            }
        }

        /// <summary>Réglages d'une classe de machine, en une ligne.</summary>
        public static string Describe(StreamingProfile p) => string.Format(CultureInfo.InvariantCulture,
            "{0} : détail {1} case(s) autour ({2} points par côté), vue {3} cases ({4} points), brume à {5:0} m, {6} ms par image, {7} Mo",
            p.Tier, p.DetailRadius, p.DetailResolution, p.ViewRadius, p.SimpleResolution, p.ViewMeters, p.BudgetMs, p.MemoryBudgetBytes >> 20);

        private MeshRenderer Make(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var mesh = new Mesh();
            mesh.MarkDynamic();
            _meshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            return renderer;
        }

        private void Fill(MeshRenderer renderer, TerrainPatch patch)
        {
            int n = patch.VertexCount;
            if (_vertices.Length < n)
            {
                _vertices = new Vector3[n];
                _normals = new Vector3[n];
                _uvs = new Vector2[n];
            }
            float[] p = patch.Positions, q = patch.Normals, t = patch.Uvs;
            for (int i = 0; i < n; i++)
            {
                _vertices[i] = new Vector3(p[3 * i], p[3 * i + 1], p[3 * i + 2]);
                _normals[i] = new Vector3(q[3 * i], q[3 * i + 1], q[3 * i + 2]);
                _uvs[i] = new Vector2(t[2 * i], t[2 * i + 1]);
            }
            Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            mesh.Clear();
            mesh.indexFormat = patch.WideIndices ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(_vertices, 0, n);
            mesh.SetNormals(_normals, 0, n);
            mesh.SetUVs(0, _uvs, 0, n);
            mesh.SetTriangles(patch.Indices, 0, patch.IndexCount, 0);
            renderer.gameObject.SetActive(false);   // préparé, pas encore montré
        }

        // Revue Codex de la PR #154 : publication atomique. Préparer remplit un objet de réserve (inactif, rien ne change à l'écran) ;
        // publier échange d'un coup les objets préparés avec ceux montrés (activer, désactiver, rendre au recyclage : rien qui puisse
        // échouer) ; oublier rend les objets préparés au recyclage.
        private readonly List<(bool Far, RegionCell Cell, int Block, MeshRenderer Renderer, bool Visible)> _staged =
            new List<(bool, RegionCell, int, MeshRenderer, bool)>();

        private MeshRenderer Spare() => _pool.Count > 0 ? _pool.Pop() : Make("Terrain");

        void ITerrainSink.StageCell(RegionCell cell, int ring, TerrainPatch patch)
        {
            MeshRenderer renderer = null;
            if (patch != null)
            {
                renderer = Spare();
                _staged.Add((false, cell, 0, renderer, patch.IndexCount > 0));   // inscrit avant de remplir : un échec le rend au recyclage (Discard)
                renderer.gameObject.name = "Case " + cell.Name + " anneau " + ring;
                Fill(renderer, patch);
                bool shadows = ring == 0 && _terrain.Profile.DetailShadows;
                renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = ring == 0;
            }
            else _staged.Add((false, cell, 0, null, false));
        }

        void ITerrainSink.StageFarBlock(int column, int row, TerrainPatch patch)
        {
            MeshRenderer renderer = Spare();
            _staged.Add((true, default(RegionCell), row * 1000 + column, renderer, patch.IndexCount > 0));
            renderer.gameObject.name = "Terrain lointain " + column + "," + row;
            Fill(renderer, patch);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void ITerrainSink.Publish()
        {
            foreach ((bool far, RegionCell cell, int block, MeshRenderer next, bool visible) in _staged)
            {
                MeshRenderer old;
                bool had = far ? _far.TryGetValue(block, out old) : _cells.TryGetValue(cell, out old);
                if (next != null)
                {
                    next.gameObject.SetActive(visible);
                    if (far) _far[block] = next;
                    else _cells[cell] = next;
                }
                else if (!far) _cells.Remove(cell);
                if (had)
                {
                    old.gameObject.SetActive(false);
                    _pool.Push(old);   // recyclage : le prochain morceau reprend l'objet et son maillage
                }
            }
            _staged.Clear();
        }

        void ITerrainSink.Discard()
        {
            foreach ((bool far, RegionCell cell, int block, MeshRenderer renderer, bool visible) in _staged)
                if (renderer != null)
                {
                    renderer.gameObject.SetActive(false);
                    _pool.Push(renderer);
                }
            _staged.Clear();
        }

        /// <summary>
        /// Relecture indépendante de la PR #154 : la racine a été détruite par Unity (changement de scène, arrêt du mode jeu) alors que le
        /// statique la garde : on libère les maillages et le matériau, et le terrain sera refait au prochain <see cref="Use"/>.
        /// </summary>
        private static void Forget()
        {
            if (_current == null || _current._root != null) return;
            _current.Destroy();
            _current = null;
            StyleWorld.ViewDistance = 0f;
        }

        private void Destroy()
        {
            _terrain.Cancel();
            foreach (Mesh mesh in _meshes) if (mesh != null) Object.Destroy(mesh);   // objets recyclés (inactifs) compris
            _meshes.Clear();
            _staged.Clear();
            _cells.Clear();
            _far.Clear();
            _pool.Clear();
            if (_root != null) Object.Destroy(_root);
            if (_material != null) Object.Destroy(_material);
        }
    }
}
