using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.29 (horizon, plan de la grande carte § 6.3) : anneau des terres lointaines autour de la caméra (<see cref="HorizonProfile"/>) :
    /// les Crêtes de Fer enneigées au nord-est, la Forêt des Murmures voilée au nord-ouest, des collines basses ailleurs. Un seul maillage, posé
    /// juste en deçà du plan lointain de la caméra et qui la suit (jamais chargé par cases) ; dessiné après le ciel, sans écrire la profondeur :
    /// tout le décor proche passe devant. Les couleurs se fondent dans celle de la brume du moment (jour, nuit, météo).
    /// <para>
    /// Sous-lot V2.29.2 : les <b>panoramas peints</b> (<see cref="HorizonPanorama"/>) remplacent ces silhouettes dès que leurs textures sont
    /// là (Resources/EdsHorizon) : une bande cylindrique texturée (alpha, le ciel du jeu derrière), calée en azimut (nord à u = 0) et en
    /// angle (l'horizon à 200 px du bas, 45,511 px/°, donc juste quel que soit le plan lointain et la hauteur de la caméra), teintée par la
    /// lumière de l'heure (<see cref="StyleWorld.HorizonLight"/>), choisie selon la saison du jeu avec un fondu pendant la transition de
    /// 7 jours. L'Envers n'est pas encore un état du jeu : <see cref="Reversed"/> reste faux, sa texture n'est chargée que s'il devient vrai.
    /// Les silhouettes ne servent plus que de secours (textures absentes).
    /// </para>
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

        /// <summary>Sous-lot V2.29.2 : l'Envers est montré (aucun état du jeu ne le pose pour l'instant : ses textures restent prêtes, non chargées).</summary>
        public static bool Reversed { get; set; }

        private const int PanoramaSegments = 144;
        private static GameObject _band;
        private static Material _under, _top;
        private static Renderer _underRenderer;
        private static readonly Dictionary<string, Texture2D> Loaded = new Dictionary<string, Texture2D>();
        private static bool _bandBuilt;

        private static readonly Color Hills = new Color(0.36f, 0.43f, 0.47f), Forest = new Color(0.22f, 0.32f, 0.29f),
            Rock = new Color(0.33f, 0.35f, 0.42f), Snow = new Color(0.9f, 0.93f, 0.97f),
            Sea = new Color(0.30f, 0.42f, 0.50f);   // sous-lot V2.29 (2) : la mer de la Côte des Brumes, à l'ouest

        /// <summary>Place l'anneau autour de <paramref name="camera"/> (à chaque image) ; <paramref name="visible"/> faux le cache (intérieurs).</summary>
        public static void Follow(Camera camera, bool visible)
        {
            if (camera == null) return;
            if (_root == null) Build();
            if (!_bandBuilt) BuildBand();
            // Revue indépendante de la PR #203 : tant que la saison n'est pas connue, rien n'est montré (pas de printemps par défaut).
            if (!visible || !SeasonView.HasLook)
            {
                _root.SetActive(false);
                _band.SetActive(false);
                return;
            }
            bool painted = ShowPanorama(camera);
            _root.SetActive(!painted);
            if (painted) return;
            float radius = camera.farClipPlane * 0.92f;
            if (!Mathf.Approximately(radius, _radius))
            {
                _radius = radius;
                _root.transform.localScale = new Vector3(radius, radius / (float)HorizonProfile.Radius, radius);
            }
            Vector3 at = camera.transform.position;
            // Revue Codex de la PR #182 : l'anneau réduit garde les angles du profil de référence vus depuis la caméra (sa hauteur réduite
            // autour de l'altitude de la caméra, pas autour de 0) : y0 = altitude × (1 − échelle).
            float scale = radius / (float)HorizonProfile.Radius;
            _root.transform.position = new Vector3(at.x, at.y * (1f - scale), at.z);
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

        /// <summary>Pose la bande peinte (voir la classe) ; faux (et bande cachée) si les textures manquent : les silhouettes servent alors.</summary>
        private static bool ShowPanorama(Camera camera)
        {
            PanoramaChoice choice = HorizonPanorama.Choose(SeasonView.Shown.Season, SeasonView.Shown.Blend, Reversed);
            Texture2D top = Panorama(choice.Top);
            Texture2D under = choice.Under != null ? Panorama(choice.Under) : null;
            if (top == null)
            {
                _band.SetActive(false);
                return false;
            }
            _band.SetActive(true);
            float radius = camera.farClipPlane * 0.92f, scale = radius / (float)HorizonProfile.Radius;
            _band.transform.localScale = new Vector3(radius, scale, radius);
            Vector3 at = camera.transform.position;
            _band.transform.position = new Vector3(at.x, at.y * (1f - scale), at.z);   // angles justes à toute altitude (HorizonPanorama.WorldY)
            // La lumière de l'heure (jour, nuit, météo), un peu fondue dans la brume : le pied se perd dans la brume du moment.
            Color tint = Color.Lerp(StyleWorld.HorizonLight, RenderSettings.fogColor, 0.18f);
            _top.SetTexture("_MainTex", top);
            _top.SetColor("_Color", new Color(tint.r, tint.g, tint.b, choice.TopAlpha));
            bool both = under != null && choice.UnderAlpha > 0f;
            _underRenderer.gameObject.SetActive(both);
            if (both)
            {
                _under.SetTexture("_MainTex", under);
                _under.SetColor("_Color", new Color(tint.r, tint.g, tint.b, choice.UnderAlpha));
            }
            return true;
        }

        /// <summary>Texture d'un panorama pour la classe de la machine (4 096 sur téléphone, 8 192 sur PC) ; chargée une fois, null si absente.</summary>
        private static Texture2D Panorama(string name)
        {
            string path = HorizonPanorama.ResourcePath(name, DeviceQuality.Tier);
            if (Loaded.TryGetValue(path, out Texture2D t)) return t;
            t = Resources.Load<Texture2D>(path);
            if (t != null) t.wrapMode = TextureWrapMode.Clamp;   // un seul tour en u (colonnes extrêmes identiques), pas de reprise en hauteur
            Loaded[path] = t;
            return t;
        }

        private static void BuildBand()
        {
            _bandBuilt = true;
            int n = PanoramaSegments;
            var vertices = new Vector3[2 * (n + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[6 * n];
            // Rayon 1 ; hauteurs au rayon de référence (6 000 m) : l'échelle y du maillage les ramène au plan lointain.
            float bottom = (float)(HorizonProfile.Radius * System.Math.Tan(HorizonPanorama.BottomAngle * System.Math.PI / 180.0));
            float top = (float)(HorizonProfile.Radius * System.Math.Tan(HorizonPanorama.TopAngle * System.Math.PI / 180.0));
            for (int i = 0; i <= n; i++)
            {
                double azimuth = i * 360.0 / n, a = azimuth * System.Math.PI / 180.0;
                float x = (float)System.Math.Sin(a), z = (float)System.Math.Cos(a);   // 0 : le nord (+z), 90 : l'est (+x)
                vertices[2 * i] = new Vector3(x, bottom, z);
                vertices[2 * i + 1] = new Vector3(x, top, z);
                float u = i / (float)n;
                uv[2 * i] = new Vector2(u, 0f);
                uv[2 * i + 1] = new Vector2(u, 1f);
            }
            for (int i = 0; i < n; i++)
            {
                int a = 2 * i, t = 6 * i;
                triangles[t] = a; triangles[t + 1] = a + 1; triangles[t + 2] = a + 2;
                triangles[t + 3] = a + 1; triangles[t + 4] = a + 3; triangles[t + 5] = a + 2;
            }
            var mesh = new Mesh { name = "HorizonPanorama" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            _band = new GameObject("HorizonPanorama");
            Object.DontDestroyOnLoad(_band);
            _top = new Material(Shader.Find("Sprites/Default")) { name = "HorizonPanoramaTop", renderQueue = 2999 };
            _under = new Material(Shader.Find("Sprites/Default")) { name = "HorizonPanoramaUnder", renderQueue = 2998 };
            // Deux bandes sur le même maillage : la saison d'avant dessous (file 2998), la saison montrée dessus (2999) ; fondu pendant la transition.
            var underObject = new GameObject("Under");
            underObject.transform.SetParent(_band.transform, false);
            _underRenderer = Place(underObject, mesh, _under);
            Place(_band, mesh, _top);
            _band.SetActive(false);
        }

        private static Renderer Place(GameObject target, Mesh mesh, Material material)
        {
            target.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
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
                Color top = layer == HorizonLayer.Mountains ? (profile.Snow(i) ? Snow : Rock) : layer == HorizonLayer.Forest ? Forest
                    : layer == HorizonLayer.Sea ? Sea : Hills;
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
