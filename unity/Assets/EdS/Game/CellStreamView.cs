using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        // Sous-lot V2.28d (1) : décor de la région par cases (CellDecor), pour les cases de l'anneau de détail (0) seulement.
        private readonly AdventureMap _whole;
        private readonly RegionGrid _grid;
        private readonly GroundRelief _relief;
        private readonly (double, double, double, double)? _hole;
        private readonly Dictionary<RegionCell, GameObject> _decor = new Dictionary<RegionCell, GameObject>();
        private readonly Material _water, _ravine, _road, _trunk, _leaves, _needles;

        private CellStreamView(string key, AdventureMap whole, RegionGrid grid, StreamingProfile profile, GroundRelief relief, Rect covered)
        {
            _key = key;
            _root = new GameObject("Terrain de la région");
            (double, double, double, double)? hole = null;
            if (covered.width > 0f && covered.height > 0f) hole = (covered.x, covered.y, covered.x + covered.width, covered.y + covered.height);
            _whole = whole;
            _grid = grid;
            _relief = relief;
            _hole = hole;
            _water = StyleWorld.Water() ?? ArenaView.MakeMaterial(new Color(0.18f, 0.36f, 0.62f));
            _ravine = ArenaView.MakeMaterial(new Color(0.22f, 0.2f, 0.18f));
            _road = ArenaView.MakeMaterial(new Color(0.48f, 0.4f, 0.28f));
            _trunk = ArenaView.MakeMaterial(new Color(0.3f, 0.21f, 0.13f));
            _leaves = ArenaView.MakeMaterial(new Color(0.22f, 0.36f, 0.16f));
            _needles = ArenaView.MakeMaterial(new Color(0.13f, 0.26f, 0.15f));
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
                _current = new CellStreamView(key, map.Whole, map.Region, profile, relief, covered);
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

        // Sous-lot V2.28d (1) : décor préparé avec sa case (calculé dans le budget de l'image), montré à la publication.
        private readonly Dictionary<RegionCell, CellDecorSet> _stagedDecor = new Dictionary<RegionCell, CellDecorSet>();

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
                // Sous-lot V2.28d (1) : le décor des cases de détail, calculé ici (dans le budget de l'image).
                if (ring == 0 && !_decor.ContainsKey(cell)) _stagedDecor[cell] = CellDecor.For(_whole, _grid, cell, _hole);
                else if (ring == 0) _stagedDecor.Remove(cell);
                else _stagedDecor[cell] = null;
            }
            else
            {
                _staged.Add((false, cell, 0, null, false));
                _stagedDecor[cell] = null;
            }
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
            // Sous-lot V2.28d (1) : décor montré avec sa case de détail, retiré quand la case quitte l'anneau de détail ou se décharge.
            foreach (KeyValuePair<RegionCell, CellDecorSet> d in _stagedDecor)
            {
                _decor.TryGetValue(d.Key, out GameObject old);
                if (d.Value == null)
                {
                    if (old != null) DestroyDecor(old);
                    _decor.Remove(d.Key);
                }
                else
                {
                    if (old != null) DestroyDecor(old);
                    _decor[d.Key] = BuildDecor(d.Value);
                }
            }
            _stagedDecor.Clear();
        }

        /// <summary>
        /// Sous-lot V2.28d (1) : objets du décor d'une case : eau et ravins (plats, posés sur le relief), routes (rubans drapés sur le relief),
        /// arbres (un maillage de troncs et un de houppiers par case : cônes pour la forêt des Loups, boules taillées pour les feuillus),
        /// rochers des parois (modèles de l'atelier). Affichage seulement.
        /// </summary>
        private GameObject BuildDecor(CellDecorSet set)
        {
            var root = new GameObject("Décor " + set.Cell.Name);
            root.transform.SetParent(_root.transform, false);
            if (set.IsEmpty) return root;
            var flat = new List<Vector3>();
            var flatTris = new List<int>();
            foreach (IReadOnlyList<(double X, double Z)> poly in set.Water) Fan(poly, 0.03f, false, flat, flatTris);
            AddMesh(root, "Eau", flat, flatTris, _water, false);
            flat = new List<Vector3>();
            flatTris = new List<int>();
            foreach (IReadOnlyList<(double X, double Z)> poly in set.Ravines) Fan(poly, 0.03f, true, flat, flatTris);
            AddMesh(root, "Ravins", flat, flatTris, _ravine, false);
            var road = new List<Vector3>();
            var roadTris = new List<int>();
            foreach (CellRoad r in set.Roads) Ribbon(r, road, roadTris);
            AddMesh(root, "Routes", road, roadTris, _road, false);
            var trunks = new List<Vector3>();
            var trunkTris = new List<int>();
            var crowns = new List<Vector3>();
            var crownTris = new List<int>();
            var needles = new List<Vector3>();
            var needleTris = new List<int>();
            foreach (CellTree t in set.Trees)
            {
                float y = (float)_relief.Height(t.X, t.Z), s = (float)t.Scale;
                var foot = new Vector3((float)t.X, y, (float)t.Z);
                Pyramid(foot, 0.25f * s, 2.2f * s, trunks, trunkTris);
                if (t.Wolves) Pyramid(foot + Vector3.up * 1.6f * s, 2.0f * s, 7.5f * s, needles, needleTris);
                else
                {
                    Pyramid(foot + Vector3.up * 1.8f * s, 2.6f * s, 3.4f * s, crowns, crownTris);
                    Pyramid(foot + Vector3.up * (5.2f * s), 2.6f * s, -2.2f * s, crowns, crownTris);
                }
            }
            AddMesh(root, "Troncs", trunks, trunkTris, _trunk, true);
            AddMesh(root, "Feuillus", crowns, crownTris, _leaves, true);
            AddMesh(root, "Résineux", needles, needleTris, _needles, true);
            foreach (CellRock rock in set.Rocks)
            {
                var at = new Vector3((float)rock.X, (float)_relief.Height(rock.X, rock.Z), (float)rock.Z);
                StyleLook.Decor(root.transform, StyleSlot.Rock, rock.Id, at, 0f, (float)rock.Size * (1.0f + 0.003f * StyleRules.Variant(rock.Id + "/taille", 100)));
            }
            return root;
        }

        private void Fan(IReadOnlyList<(double X, double Z)> poly, float lift, bool drape, List<Vector3> vertices, List<int> triangles)
        {
            int start = vertices.Count;
            foreach ((double x, double z) in poly) vertices.Add(new Vector3((float)x, (drape ? (float)_relief.Height(x, z) : 0f) + lift, (float)z));
            for (int i = 1; i + 1 < poly.Count; i++)
            {
                // Vers le haut quel que soit le sens du polygone.
                Vector3 a = vertices[start], b = vertices[start + i], c = vertices[start + i + 1];
                bool up = Vector3.Cross(b - a, c - a).y > 0f;
                triangles.Add(start);
                triangles.Add(up ? start + i + 1 : start + i);
                triangles.Add(up ? start + i : start + i + 1);
            }
        }

        private void Ribbon(CellRoad road, List<Vector3> vertices, List<int> triangles)
        {
            float half = (float)road.Width / 2f;
            var samples = new List<(double X, double Z, double Nx, double Nz)>();
            for (int i = 0; i + 1 < road.Points.Count; i++)
            {
                var a = road.Points[i];
                var b = road.Points[i + 1];
                double len = System.Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
                if (len < 1e-6) continue;
                double nx = -(b.Z - a.Z) / len, nz = (b.X - a.X) / len;
                int n = Mathf.Max(1, Mathf.CeilToInt((float)(len / 4.0)));
                for (int k = i == 0 ? 0 : 1; k <= n; k++) samples.Add((a.X + (b.X - a.X) * k / n, a.Z + (b.Z - a.Z) * k / n, nx, nz));
            }
            if (samples.Count < 2) return;
            int start = vertices.Count;
            foreach ((double x, double z, double nx, double nz) in samples)
                foreach (float side in new[] { -half, half })
                {
                    double px = x + nx * side, pz = z + nz * side;
                    vertices.Add(new Vector3((float)px, (float)_relief.Height(px, pz) + 0.06f, (float)pz));
                }
            for (int k = 0; k + 1 < samples.Count; k++)
            {
                int a = start + 2 * k;
                // Normale vers le haut : (gauche, droite) le long de la route, la gauche à +normale.
                triangles.Add(a); triangles.Add(a + 2); triangles.Add(a + 1);
                triangles.Add(a + 1); triangles.Add(a + 2); triangles.Add(a + 3);
            }
        }

        /// <summary>Pyramide à base carrée (pointe en haut si <paramref name="height"/> > 0, en bas sinon) : quatre faces, sans fond.</summary>
        private static void Pyramid(Vector3 foot, float half, float height, List<Vector3> vertices, List<int> triangles)
        {
            Vector3 tip = foot + Vector3.up * height;
            var corners = new[] { foot + new Vector3(-half, 0f, -half), foot + new Vector3(half, 0f, -half), foot + new Vector3(half, 0f, half), foot + new Vector3(-half, 0f, half) };
            for (int i = 0; i < 4; i++)
            {
                int start = vertices.Count;
                vertices.Add(corners[i]);
                vertices.Add(corners[(i + 1) % 4]);
                vertices.Add(tip);
                bool up = height > 0f;
                triangles.Add(start);
                triangles.Add(up ? start + 2 : start + 1);
                triangles.Add(up ? start + 1 : start + 2);
            }
        }

        private void AddMesh(GameObject parent, string name, List<Vector3> vertices, List<int> triangles, Material material, bool shadows)
        {
            if (vertices.Count == 0 || triangles.Count == 0) return;
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var mesh = new Mesh { name = parent.name + " " + name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _meshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows && _terrain.Profile.DetailShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        private void DestroyDecor(GameObject root)
        {
            foreach (MeshFilter f in root.GetComponentsInChildren<MeshFilter>(true))
                if (f.sharedMesh != null && _meshes.Remove(f.sharedMesh)) Object.Destroy(f.sharedMesh);
            Object.Destroy(root);
        }

        void ITerrainSink.Discard()
        {
            _stagedDecor.Clear();
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
            _stagedDecor.Clear();
            _decor.Clear();   // leurs objets sont sous la racine, détruite ci-dessous ; leurs maillages, avec les autres
            _cells.Clear();
            _far.Clear();
            _pool.Clear();
            if (_root != null) Object.Destroy(_root);
            if (_material != null) Object.Destroy(_material);
            foreach (Material m in new[] { _ravine, _road, _trunk, _leaves, _needles }) if (m != null) Object.Destroy(m);
            if (_water != null && _water != StyleWorld.Water()) Object.Destroy(_water);
        }
    }
}
