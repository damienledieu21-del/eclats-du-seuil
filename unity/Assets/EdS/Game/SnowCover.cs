using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace EdS.Game
{
    /// <summary>
    /// V2.44.3 (décision de Damien du 20 novembre 2026 : toits enneigés par shader sur tous les bâtiments, sans crédit) : neige posée sur
    /// le décor. Tant qu'il neige (<see cref="SeasonView.SnowShown"/>), chaque modèle opaque du décor (bâtiments, toits, rochers, murets,
    /// objets, sol de la région) reçoit un double qui le redessine avec « EdS/Neige » (une fois par sous-maillage) : le shader ne blanchit
    /// que les faces tournées vers le haut. Règles (portables, testées) : <see cref="WinterLook"/>. Relecture indépendante de la PR #232 :
    /// le double suit son modèle à chaque image (coupé, en fondu parce qu'il cache le joueur : sa neige aussi) et le fondu des obstacles
    /// l'ignore ; aucun modèle animé (bêtes, objets tenus) ; aucune allocation par image. Affichage seulement.
    /// </summary>
    public sealed class SnowCover
    {
        private readonly Dictionary<MeshRenderer, MeshRenderer> _overlays = new Dictionary<MeshRenderer, MeshRenderer>();
        private readonly HashSet<MeshRenderer> _refused = new HashSet<MeshRenderer>();
        private readonly List<MeshRenderer> _dead = new List<MeshRenderer>();
        private readonly List<MeshRenderer> _found = new List<MeshRenderer>();
        private readonly List<(string Shader, int Queue)> _materials = new List<(string Shader, int Queue)>();
        private Material _material;
        private bool _tried;
        private float _sinceScan = float.MaxValue;

        /// <summary>
        /// Chaque image : chaque double suit son modèle (montré seulement s'il neige, le modèle affiché et opaque) ; toutes les
        /// <see cref="WinterLook.RescanSeconds"/> s, les nouveaux modèles des deux racines en reçoivent un. Rien dans une pièce.
        /// </summary>
        public void Sync(float deltaTime, bool indoors, Transform decor, Transform region)
        {
            bool snowing = !indoors && SeasonView.SnowShown > 0.001f && Material() != null;
            Follow(snowing);
            if (!snowing) return;
            _sinceScan += deltaTime;
            if (_sinceScan < WinterLook.RescanSeconds) return;
            _sinceScan = 0f;
            Prune();
            if (decor != null) Scan(decor);
            if (region != null) Scan(region);
            if (decor != null) Terrains(decor);
        }

        private readonly Dictionary<Terrain, MeshRenderer> _terrains = new Dictionary<Terrain, MeshRenderer>();
        private readonly List<Terrain> _foundTerrains = new List<Terrain>();
        private readonly List<Terrain> _deadTerrains = new List<Terrain>();

        /// <summary>Revue Codex de la PR #232 : maillages de neige du sol, détruits même quand leur décor l'a déjà été.</summary>
        private readonly Dictionary<Terrain, Mesh> _terrainMeshes = new Dictionary<Terrain, Mesh>();

        /// <summary>
        /// Revue Codex de la PR #232 : le sol du catalogue est un Terrain d'Unity (pas de MeshRenderer) : il reçoit une couche de neige, un
        /// maillage de son relief affiché (au plus 201 × 201 sommets) dessiné avec « EdS/Neige », juste au-dessus de lui, une fois par terrain.
        /// </summary>
        private void Terrains(Transform root)
        {
            _foundTerrains.Clear();
            root.GetComponentsInChildren(false, _foundTerrains);
            foreach (Terrain terrain in _foundTerrains)
            {
                if (terrain == null || terrain.terrainData == null || _terrains.ContainsKey(terrain)) continue;
                Vector3 origin = terrain.transform.position, size = terrain.terrainData.size;
                float step = WinterLook.TerrainSnowStep(Mathf.Max(size.x, size.z));
                int cols = Mathf.Max(1, Mathf.CeilToInt(size.x / step)), rows = Mathf.Max(1, Mathf.CeilToInt(size.z / step));
                var vertices = new Vector3[(cols + 1) * (rows + 1)];
                for (int r = 0; r <= rows; r++)
                    for (int c = 0; c <= cols; c++)
                    {
                        float x = origin.x + Mathf.Min(size.x, c * step), z = origin.z + Mathf.Min(size.z, r * step);
                        // Revue Codex de la PR #232 : hauteur réelle du Terrain (lits des rivières creusés compris), pas celle où l'on se tient.
                        vertices[r * (cols + 1) + c] = new Vector3(x, origin.y + terrain.SampleHeight(new Vector3(x, 0f, z)) + 0.03f, z);
                    }
                var triangles = new int[cols * rows * 6];
                int t = 0;
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        int a = r * (cols + 1) + c, b = a + 1, d = a + cols + 1, e = d + 1;
                        triangles[t++] = a; triangles[t++] = d; triangles[t++] = b;
                        triangles[t++] = b; triangles[t++] = d; triangles[t++] = e;
                    }
                var mesh = new Mesh { name = "Neige du sol" };
                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var layer = new GameObject(WinterLook.OverlayName + " (sol)");
                layer.transform.SetParent(terrain.transform.parent, false);
                layer.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer drawn = layer.AddComponent<MeshRenderer>();
                drawn.sharedMaterial = _material;
                drawn.shadowCastingMode = ShadowCastingMode.Off;
                drawn.receiveShadows = true;
                _terrains[terrain] = drawn;
                _terrainMeshes[terrain] = mesh;
            }
        }

        /// <summary>Détruit les doubles et le matériau (décor quitté).</summary>
        public void Clear()
        {
            foreach (MeshRenderer o in _overlays.Values)
                if (o != null) Object.Destroy(o.gameObject);
            _overlays.Clear();
            _refused.Clear();
            foreach (MeshRenderer o in _terrains.Values)
                if (o != null) Object.Destroy(o.gameObject);
            _terrains.Clear();
            foreach (Mesh m in _terrainMeshes.Values)
                if (m != null) Object.Destroy(m);
            _terrainMeshes.Clear();
            if (_material != null) Object.Destroy(_material);
            _material = null;
            _tried = false;
            _sinceScan = float.MaxValue;
        }

        private Material Material()
        {
            if (_tried) return _material;
            _tried = true;
            Material loaded = Resources.Load<Material>("EdsNeige");
            if (loaded != null) _material = new Material(loaded);
            else
            {
                Shader shader = Shader.Find("EdS/Neige");
                if (shader != null && shader.isSupported) _material = new Material(shader);
            }
            if (_material != null) _material.enableInstancing = true;
            return _material;
        }

        private void Follow(bool snowing)
        {
            foreach (KeyValuePair<Terrain, MeshRenderer> e in _terrains)
                if (e.Key != null && e.Value != null && e.Value.enabled != snowing) e.Value.enabled = snowing;
            foreach (KeyValuePair<MeshRenderer, MeshRenderer> e in _overlays)
            {
                if (e.Key == null || e.Value == null) continue;
                Material first = e.Key.sharedMaterial;
                bool shown = WinterLook.OverlayShown(snowing, e.Key.enabled, first != null ? first.renderQueue : WinterLook.CutoutQueue);
                if (e.Value.enabled != shown) e.Value.enabled = shown;
            }
        }

        private void Prune()
        {
            _dead.Clear();
            foreach (KeyValuePair<MeshRenderer, MeshRenderer> e in _overlays)
                if (e.Key == null || e.Value == null) _dead.Add(e.Key);
            foreach (MeshRenderer r in _dead) _overlays.Remove(r);
            _refused.RemoveWhere(r => r == null);
            _deadTerrains.Clear();
            foreach (KeyValuePair<Terrain, MeshRenderer> e in _terrains)
                if (e.Key == null || e.Value == null) _deadTerrains.Add(e.Key);
            foreach (Terrain t in _deadTerrains)
            {
                if (_terrains.TryGetValue(t, out MeshRenderer layer) && layer != null) Object.Destroy(layer.gameObject);
                _terrains.Remove(t);
                if (_terrainMeshes.TryGetValue(t, out Mesh mesh) && mesh != null) Object.Destroy(mesh);
                _terrainMeshes.Remove(t);
            }
        }

        private void Scan(Transform root)
        {
            _found.Clear();
            root.GetComponentsInChildren(false, _found);
            foreach (MeshRenderer r in _found)
            {
                if (_overlays.Count >= WinterLook.MaxOverlays) return;
                if (r == null || _overlays.ContainsKey(r) || _refused.Contains(r)) continue;
                MeshFilter filter = r.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null)
                {
                    _refused.Add(r);
                    continue;
                }
                // Revue Codex de la PR #232 : un refus passager (coupé, en fondu) n'est pas retenu : le modèle est réexaminé plus tard.
                WinterLook.Verdict verdict = WinterLook.Judge(r.gameObject.name, r.enabled, Animated(r.transform), Materials(r));
                if (verdict == WinterLook.Verdict.Never) _refused.Add(r);
                if (verdict != WinterLook.Verdict.Accepted) continue;
                var overlay = new GameObject(WinterLook.OverlayName);
                overlay.layer = r.gameObject.layer;
                overlay.transform.SetParent(r.transform, false);   // suit la place et l'échelle de son modèle (obstacle abaissé compris)
                overlay.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer drawn = overlay.AddComponent<MeshRenderer>();
                var materials = new Material[System.Math.Max(1, mesh.subMeshCount)];
                for (int i = 0; i < materials.Length; i++) materials[i] = _material;
                drawn.sharedMaterials = materials;
                drawn.shadowCastingMode = ShadowCastingMode.Off;
                drawn.receiveShadows = true;
                _overlays[r] = drawn;
            }
        }

        private List<(string Shader, int Queue)> Materials(MeshRenderer r)
        {
            _materials.Clear();
            Material[] materials = r.sharedMaterials;
            if (materials == null) return _materials;
            foreach (Material m in materials)
                _materials.Add(m == null || m.shader == null ? ((string)null, WinterLook.CutoutQueue) : (m.shader.name, m.renderQueue));
            return _materials;
        }

        /// <summary>Relecture de la PR #232 : un ancêtre animé (bête, objet tenu, partie rigide d'un modèle animé).</summary>
        private static bool Animated(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.GetComponent<Animator>() != null || t.GetComponent<Animation>() != null) return true;
            return false;
        }
    }
}
