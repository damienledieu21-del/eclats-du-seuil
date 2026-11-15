using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.29 (horizon, plan de la grande carte § 6.3) : anneau des terres lointaines autour de la caméra (<see cref="HorizonProfile"/>) :
    /// les Crêtes de Fer enneigées au nord-est, la Forêt des Murmures voilée au nord-ouest, des collines basses ailleurs. Un seul maillage, posé
    /// juste en deçà du plan lointain de la caméra et qui la suit (jamais chargé par cases) ; dessiné après le ciel, sans écrire la profondeur :
    /// tout le décor proche passe devant. Les couleurs se fondent dans celle de la brume du moment (jour, nuit, météo).
    /// </summary>
    public static class HorizonView
    {
        private static GameObject _root;
        private static Mesh _mesh;
        private static Material _material;
        private static Color[] _base;
        private static Color[] _colors;
        private static Color _fog = new Color(-1f, 0f, 0f, 0f);
        private static float _radius = -1f;

        private static readonly Color Hills = new Color(0.36f, 0.43f, 0.47f), Forest = new Color(0.22f, 0.32f, 0.29f),
            Rock = new Color(0.33f, 0.35f, 0.42f), Snow = new Color(0.9f, 0.93f, 0.97f);

        /// <summary>Place l'anneau autour de <paramref name="camera"/> (à chaque image) ; <paramref name="visible"/> faux le cache (intérieurs).</summary>
        public static void Follow(Camera camera, bool visible)
        {
            if (camera == null) return;
            if (_root == null) Build();
            _root.SetActive(visible);
            if (!visible) return;
            float radius = camera.farClipPlane * 0.92f;
            if (!Mathf.Approximately(radius, _radius))
            {
                _radius = radius;
                _root.transform.localScale = new Vector3(radius, radius / (float)HorizonProfile.Radius, radius);
            }
            Vector3 at = camera.transform.position;
            _root.transform.position = new Vector3(at.x, 0f, at.z);
            Color fog = RenderSettings.fogColor;
            // Relecture de la PR #182 : recalculé seulement si la brume change de plus d'un 255e.
            if (Mathf.Abs(fog.r - _fog.r) > 1f / 255f || Mathf.Abs(fog.g - _fog.g) > 1f / 255f || Mathf.Abs(fog.b - _fog.b) > 1f / 255f)
            {
                _fog = fog;
                // Plus c'est bas, plus c'est voilé ; la forêt des Murmures dans sa brume, les neiges plus nettes.
                // Relecture de la PR #182 : l'alpha de la couleur de base porte la part de brume ; la couleur affichée reste opaque.
                for (int i = 0; i < _base.Length; i++)
                {
                    Color c = Color.Lerp(_base[i], fog, _base[i].a);
                    c.a = 1f;
                    _colors[i] = c;
                }
                _mesh.colors = _colors;
            }
        }

        private static void Build()
        {
            _radius = -1f;   // relecture de la PR #182 : échelle et couleurs reprises à la reconstruction
            _fog = new Color(-1f, 0f, 0f, 0f);
            HorizonProfile profile = HorizonProfile.Vallons;
            int n = HorizonProfile.Samples;
            var vertices = new Vector3[2 * (n + 1)];
            _base = new Color[vertices.Length];
            _colors = new Color[vertices.Length];
            var triangles = new int[6 * n];
            for (int i = 0; i <= n; i++)
            {
                (double x, double z) = HorizonProfile.Direction(i);
                float h = (float)profile.Height(i);
                // Rayon 1 : l'échelle de l'objet le porte au plan lointain ; hauteurs en mètres au rayon de référence (échelle y).
                vertices[2 * i] = new Vector3((float)x, -60f, (float)z);
                vertices[2 * i + 1] = new Vector3((float)x, h, (float)z);
                HorizonLayer layer = profile.Layer(i);
                Color top = layer == HorizonLayer.Mountains ? (profile.Snow(i) ? Snow : Rock) : layer == HorizonLayer.Forest ? Forest : Hills;
                Color foot = layer == HorizonLayer.Mountains ? Rock : top;
                // Alpha : part de la brume (0 net, 1 fondu) ; le pied se perd dans la brume.
                top.a = layer == HorizonLayer.Forest ? 0.6f : layer == HorizonLayer.Mountains ? 0.35f : 0.5f;
                foot.a = 0.85f;
                _base[2 * i] = foot;
                _base[2 * i + 1] = top;
            }
            for (int i = 0; i < n; i++)
            {
                int a = 2 * i, t = 6 * i;
                // Vu de l'intérieur de l'anneau (caméra au centre) : faces tournées vers le centre.
                triangles[t] = a; triangles[t + 1] = a + 1; triangles[t + 2] = a + 2;
                triangles[t + 3] = a + 1; triangles[t + 4] = a + 3; triangles[t + 5] = a + 2;
            }
            _mesh = new Mesh { name = "Horizon" };
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _mesh.RecalculateBounds();
            _root = new GameObject("Horizon");
            Object.DontDestroyOnLoad(_root);
            _root.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = _root.AddComponent<MeshRenderer>();
            _material = new Material(Shader.Find("Sprites/Default")) { name = "Horizon", renderQueue = 2999 };
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
