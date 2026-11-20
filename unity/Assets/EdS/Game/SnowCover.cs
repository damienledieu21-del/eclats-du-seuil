using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EdS.Game
{
    /// <summary>
    /// V2.44.3 (décision de Damien du 20 novembre 2026 : toits enneigés par shader sur tous les bâtiments, sans crédit) : neige posée sur
    /// le décor. Tant qu'il neige (<see cref="SeasonView.SnowShown"/>), chaque modèle opaque du décor (bâtiments, toits, rochers, murets,
    /// objets, sol de la région) reçoit un double qui le redessine avec « EdS/Neige » (une fois par sous-maillage) : le shader ne blanchit
    /// que les faces tournées vers le haut, selon la neige de la plaine ou celle du col au-dessus de sa limite. Les feuillages, l'herbe et
    /// l'eau ont leur propre neige (ou leur glace) ; les personnages et les bêtes (maillages animés) n'en reçoivent pas. Sans neige, les
    /// doubles sont cachés ; jamais dans une pièce. Affichage seulement.
    /// </summary>
    public sealed class SnowCover
    {
        /// <summary>Intervalle (s) entre deux recherches de nouveaux modèles (le décor change par cases et par fenêtres).</summary>
        public const float RescanSeconds = 2f;

        /// <summary>Doubles au plus (garde-fou de rendu sur mobile).</summary>
        public const int MaxOverlays = 6000;

        private const string OverlayName = "Neige posée";

        /// <summary>Shaders qui ont leur propre neige ou n'en portent pas (feuillage, herbe, eau, effets).</summary>
        private static readonly string[] Skipped = { "EdS/Feuillage", "EdS/Herbe", "EdS/Eau", "EdS/Particule", "EdS/Annonce", "EdS/Echo", "EdS/Faille", "EdS/Neige" };

        private readonly Dictionary<MeshRenderer, GameObject> _overlays = new Dictionary<MeshRenderer, GameObject>();
        private readonly HashSet<MeshRenderer> _refused = new HashSet<MeshRenderer>();
        private readonly List<MeshRenderer> _dead = new List<MeshRenderer>();
        private Material _material;
        private bool _tried;
        private bool _shown;
        private float _sinceScan = float.MaxValue;

        /// <summary>
        /// Chaque image : avec de la neige, les doubles sont montrés et, toutes les <see cref="RescanSeconds"/> s, les nouveaux modèles des
        /// racines <paramref name="roots"/> en reçoivent un ; sans neige (ou dans une pièce), ils sont cachés.
        /// </summary>
        public void Sync(float deltaTime, bool indoors, params Transform[] roots)
        {
            bool snowing = !indoors && SeasonView.SnowShown > 0.001f && Material() != null;
            if (!snowing)
            {
                if (_shown) Show(false);
                return;
            }
            if (!_shown) Show(true);
            _sinceScan += deltaTime;
            if (_sinceScan < RescanSeconds) return;
            _sinceScan = 0f;
            Prune();
            foreach (Transform root in roots)
                if (root != null) Scan(root);
        }

        /// <summary>Détruit tous les doubles (décor quitté).</summary>
        public void Clear()
        {
            foreach (GameObject o in _overlays.Values)
                if (o != null) Object.Destroy(o);
            _overlays.Clear();
            _refused.Clear();
            _shown = false;
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

        private void Show(bool shown)
        {
            _shown = shown;
            foreach (GameObject o in _overlays.Values)
                if (o != null) o.SetActive(shown);
        }

        private void Prune()
        {
            _dead.Clear();
            foreach (KeyValuePair<MeshRenderer, GameObject> e in _overlays)
                if (e.Key == null || e.Value == null) _dead.Add(e.Key);
            foreach (MeshRenderer r in _dead) _overlays.Remove(r);
            _refused.RemoveWhere(r => r == null);
        }

        private void Scan(Transform root)
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(false);
            if (renderers == null) return;
            foreach (MeshRenderer r in renderers)
            {
                if (_overlays.Count >= MaxOverlays) return;
                if (r == null || _overlays.ContainsKey(r) || _refused.Contains(r)) continue;
                if (r.gameObject.name == OverlayName || !Accepts(r))
                {
                    _refused.Add(r);
                    continue;
                }
                MeshFilter filter = r.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null)
                {
                    _refused.Add(r);
                    continue;
                }
                var overlay = new GameObject(OverlayName);
                overlay.layer = r.gameObject.layer;
                overlay.transform.SetParent(r.transform, false);
                overlay.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer drawn = overlay.AddComponent<MeshRenderer>();
                var materials = new Material[System.Math.Max(1, mesh.subMeshCount)];
                for (int i = 0; i < materials.Length; i++) materials[i] = _material;
                drawn.sharedMaterials = materials;
                drawn.shadowCastingMode = ShadowCastingMode.Off;
                drawn.receiveShadows = true;
                _overlays[r] = overlay;
            }
        }

        /// <summary>Un modèle opaque du décor : aucun de ses matériaux n'est transparent ni n'a sa propre neige.</summary>
        private static bool Accepts(MeshRenderer r)
        {
            if (!r.enabled) return false;
            Material[] materials = r.sharedMaterials;
            if (materials == null || materials.Length == 0) return false;
            foreach (Material m in materials)
            {
                if (m == null || m.shader == null) return false;
                if (m.renderQueue >= 2450) return false;   // découpé, transparent
                string name = m.shader.name;
                foreach (string s in Skipped)
                    if (name == s) return false;
            }
            return true;
        }
    }
}
