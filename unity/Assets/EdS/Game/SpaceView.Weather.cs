using System.Collections.Generic;
using System.Linq;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.2 (météo) : le décor du temps, sans asset payant. Des flaques (disques plats, sombres et lisses, qui reflètent le ciel)
    /// toujours aux mêmes places le long des chemins, qui grandissent avec le sol mouillé et sèchent après la pluie ; sous une vraie pluie,
    /// les villageois du décor rentrent (un sur trois reste dehors, <see cref="WeatherCycle.StaysOutInRain"/>). Un seul matériau pour toutes
    /// les flaques ; leur taille ne change que quand le sol change (rien à chaque image).
    /// Sous-lot V2.12.7 (essai du 9 novembre 2026 : sur PC modeste, des disques presque noirs et opaques, « des trous ») : chaque flaque est
    /// un carré posé à plat, en matière URP transparente (alpha 0,35 à 0,5), teintée du ciel et du sol (<see cref="PuddleLook"/>), au bord
    /// fondu par une texture radiale générée en code, sans ombre, à peine au-dessus du sol ; sur PC modeste, texture minuscule et mate.
    /// </summary>
    public sealed partial class SpaceView
    {
        private const int MaxPuddles = 48;

        private readonly List<(Puddle Data, Transform Body)> _puddles = new List<(Puddle, Transform)>();
        private Material _puddleMaterial;
        private Texture2D _puddleTexture;
        private PuddleLook _puddleLook;
        private GameObject _weatherRoot;
        private float _shownWet = -1f;
        private bool _sheltering;

        /// <summary>Pose les flaques (cachées) le long des chemins, hors des bâtiments et des obstacles.</summary>
        private void WeatherDecor()
        {
            _weatherRoot = new GameObject("Météo (V2.2)");
            _weatherRoot.transform.SetParent(_root.transform, false);
            _puddleLook = PuddleLook.For(DeviceQuality.Light || Application.isMobilePlatform);
            _puddleTexture = RadialTexture(_puddleLook.TextureSize);
            Material baseMaterial = ArenaView.MakeMaterial(Color.white);
            _puddleMaterial = Transparent(baseMaterial);
            Object.Destroy(baseMaterial);
            _puddleMaterial.SetTexture("_BaseMap", _puddleTexture);
            _puddleMaterial.SetTexture("_MainTex", _puddleTexture);
            if (_puddleMaterial.HasProperty("_Smoothness")) _puddleMaterial.SetFloat("_Smoothness", (float)_puddleLook.Smoothness);
            if (_puddleMaterial.HasProperty("_Metallic")) _puddleMaterial.SetFloat("_Metallic", 0f);
            TintPuddles();
            bool Free(double x, double z) => !_shapes.Any(o => x > o.MinX - 0.6 && x < o.MaxX + 0.6 && z > o.MinZ - 0.6 && z < o.MaxZ + 0.6);
            int max = Application.isMobilePlatform || DeviceQuality.Light ? MaxPuddles / 2 : MaxPuddles;
            foreach (Puddle p in WeatherCycle.Puddles(_roads, Free, max))
            {
                GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Quad);   // V2.12.7 : à plat, texture radiale sur tout le carré
                disc.name = p.Id;
                Object.Destroy(disc.GetComponent<Collider>());
                disc.transform.SetParent(_weatherRoot.transform, false);
                disc.transform.position = new Vector3((float)p.X, (float)(p.Y + _puddleLook.Lift), (float)p.Z);   // revue Codex de la PR #101 : au-dessus des pavés
                disc.transform.rotation = Quaternion.Euler(90f, (float)p.Yaw, 0f);   // face visible vers le haut
                Renderer renderer = disc.GetComponent<Renderer>();
                renderer.sharedMaterial = _puddleMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                disc.SetActive(false);
                _puddles.Add((p, disc.transform));
            }
        }

        /// <summary>
        /// À chaque image dehors : les flaques d'après le sol mouillé, les villageois du décor qui s'abritent sous une vraie pluie (appliqué
        /// par <see cref="Night"/>, avec ceux qui rentrent la nuit).
        /// </summary>
        public void Weather(WeatherLook look)
        {
            _sheltering = look.Rain >= WeatherCycle.ShelterRain;
            float wet = (float)look.Wetness;
            if (Mathf.Abs(wet - _shownWet) < 0.01f) return;
            _shownWet = wet;
            TintPuddles();   // le ciel a pu changer (heure, nuages) depuis la dernière fois
            float size = (float)WeatherCycle.PuddleSize(wet);
            foreach ((Puddle data, Transform body) in _puddles)
            {
                if (body == null) continue;
                bool show = size > 0.02f;
                if (body.gameObject.activeSelf != show) body.gameObject.SetActive(show);
                if (!show) continue;
                float r = (float)data.Radius * 2f * size;
                body.localScale = new Vector3(r * (float)data.Stretch, r, 1f);
            }
        }

        /// <summary>Teinte de la flaque : le ciel du moment mêlé au sol mouillé, opacité de <see cref="PuddleLook.Alpha"/>.</summary>
        private void TintPuddles()
        {
            if (_puddleMaterial == null || _puddleLook == null) return;
            Color sky = RenderSettings.ambientSkyColor;
            (double r, double g, double b) = _puddleLook.Tint(sky.r, sky.g, sky.b, 0.3, 0.28, 0.26);
            var tint = new Color((float)r, (float)g, (float)b, (float)_puddleLook.Alpha);
            _puddleMaterial.SetColor("_BaseColor", tint);
            _puddleMaterial.SetColor("_Color", tint);
        }

        /// <summary>Texture radiale générée en code : blanche, opaque au cœur, fondue vers le bord (<see cref="PuddleLook.Edge"/>).</summary>
        private static Texture2D RadialTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    byte alpha = (byte)Mathf.RoundToInt(255f * (float)PuddleLook.Edge(Mathf.Sqrt(dx * dx + dy * dy)));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            texture.SetPixels32(pixels);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.Apply(false);
            return texture;
        }

        private void ReleaseWeather()
        {
            if (_puddleTexture != null) Object.Destroy(_puddleTexture);
            _puddleTexture = null;
            if (_puddleMaterial != null) Object.Destroy(_puddleMaterial);
            _puddleMaterial = null;
            _puddles.Clear();
        }
    }
}
