using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EdS.Game
{
    /// <summary>
    /// Essai réaliste (choix de Damien du 26 octobre 2026 : « un mélange de réaliste et de stylisé coloré », « lumineux, réaliste mais pas
    /// trop ») : le ciel, la lumière du soleil, la lumière d'ambiance, la brume et les couleurs de l'image, puis le sol en terrain à trois
    /// couches (terre, roche, herbe) et la forêt autour de l'aire. Tout vient du <see cref="StyleCatalog"/> ; sans ciel ni textures de sol,
    /// rien ne change. Affichage seulement : le moteur, les emprises et les obstacles restent les mêmes.
    /// </summary>
    public static class StyleWorld
    {
        private static Camera _camera;
        private static Light _sun;
        private static Tonemapping _tone;
        private static ColorAdjustments _colors;
        private static Bloom _bloom;
        private static Vignette _vignette;

        /// <summary>L'ambiance réaliste est en place (ciel du catalogue posé).</summary>
        public static bool Active { get; private set; }

        /// <summary>
        /// Pose le ciel, la brume, la lumière et les couleurs de l'image du catalogue sur <paramref name="camera"/> (à appeler une fois,
        /// après la création du soleil). Sans catalogue ou sans ciel : rien, et false.
        /// </summary>
        public static bool Ambience(Camera camera)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (camera == null || catalog == null || catalog.Sky == null) return false;
            _camera = camera;
            RenderSettings.skybox = catalog.Sky;
            camera.clearFlags = CameraClearFlags.Skybox;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional)
                {
                    _sun = light;
                    break;
                }
            // Couleurs de l'image : volume global d'URP (tons, exposition, contraste, saturation, halo, vignette). Sans URP, rien.
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                var go = new GameObject("Essai de style : couleurs de l'image");
                Volume volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10f;
                VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
                volume.sharedProfile = profile;
                _tone = profile.Add<Tonemapping>(true);
                _tone.mode.value = TonemappingMode.Neutral;
                _colors = profile.Add<ColorAdjustments>(true);
                _bloom = profile.Add<Bloom>(true);
                _bloom.threshold.value = 1.1f;
                _vignette = profile.Add<Vignette>(true);
            }
            Active = true;
            Refresh();
            return true;
        }

        /// <summary>
        /// Recopie les réglages du catalogue (soleil, ambiance, brume, couleurs). Appelée à chaque image dans l'éditeur : Damien règle les
        /// curseurs du catalogue pendant le jeu et voit le résultat aussitôt.
        /// </summary>
        public static void Refresh()
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (!Active || catalog == null) return;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = catalog.AmbientSky;
            RenderSettings.ambientEquatorColor = catalog.AmbientHorizon;
            RenderSettings.ambientGroundColor = catalog.AmbientGround;
            RenderSettings.fog = catalog.FogEnd > catalog.FogStart;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = catalog.FogColor;
            RenderSettings.fogStartDistance = catalog.FogStart;
            RenderSettings.fogEndDistance = catalog.FogEnd;
            if (_camera != null) _camera.farClipPlane = Mathf.Max(250f, catalog.FogEnd + 20f);
            if (_sun != null)
            {
                _sun.color = catalog.SunColor;
                _sun.intensity = catalog.SunIntensity;
                _sun.transform.rotation = Quaternion.Euler(catalog.SunHeight, catalog.SunTurn, 0f);
            }
            if (_colors != null)
            {
                _colors.postExposure.value = catalog.Exposure;
                _colors.contrast.value = catalog.Contrast;
                _colors.saturation.value = catalog.Saturation;
                _vignette.intensity.value = catalog.Vignette;
                // Le halo coûte cher sur téléphone (Note8) : seulement sur ordinateur.
                _bloom.active = !Application.isMobilePlatform && catalog.Bloom > 0f;
                _bloom.intensity.value = catalog.Bloom;
            }
        }

        /// <summary>
        /// Sol réaliste d'un combat positionné : un terrain plat (hauteur 0, comme le sol du moteur) qui couvre l'aire et
        /// <see cref="StyleCatalog.ForestMargin"/> mètres autour, peint en terre, roche et herbe d'après <paramref name="spots"/>
        /// (<see cref="StyleRules.GroundMix"/>). Null sans texture de sol dans le catalogue (le sol simple reste).
        /// </summary>
        public static GameObject Ground(Transform parent, float minX, float minZ, float maxX, float maxZ, IReadOnlyList<StyleSpot> spots)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null) return null;
            Texture2D any = catalog.GrassColor != null ? catalog.GrassColor : catalog.SoilColor != null ? catalog.SoilColor : catalog.RockColor;
            if (any == null) return null;
            float margin = catalog.ForestMargin;
            float x0 = minX - margin, z0 = minZ - margin, width = maxX - minX + 2f * margin, depth = maxZ - minZ + 2f * margin;

            var data = new TerrainData();
            data.heightmapResolution = 33;
            data.alphamapResolution = 256;
            data.size = new Vector3(width, 10f, depth);
            // Couches dans l'ordre de StyleGround (terre, roche, herbe) ; une couche sans texture prend celle d'une autre.
            data.terrainLayers = new[]
            {
                Layer(Pick(catalog.SoilColor, any), Pick(catalog.SoilNormal, catalog.GrassNormal), catalog.GroundTile),
                Layer(Pick(catalog.RockColor, any), catalog.RockNormal, catalog.GroundTile),
                Layer(Pick(catalog.GrassColor, any), Pick(catalog.GrassNormal, catalog.SoilNormal), catalog.GroundTile),
            };
            int res = data.alphamapResolution;
            var maps = new float[res, res, 3];
            for (int row = 0; row < res; row++)
                for (int col = 0; col < res; col++)
                {
                    StyleMix mix = StyleRules.GroundMix(x0 + (col + 0.5f) / res * width, z0 + (row + 0.5f) / res * depth, spots);
                    maps[row, col, (int)StyleGround.Soil] = (float)mix.Soil;
                    maps[row, col, (int)StyleGround.Rock] = (float)mix.Rock;
                    maps[row, col, (int)StyleGround.Grass] = (float)mix.Grass;
                }
            data.SetAlphamaps(0, 0, maps);

            var go = new GameObject("Sol (essai réaliste)");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x0, 0f, z0);
            Terrain terrain = go.AddComponent<Terrain>();
            terrain.terrainData = data;
            if (catalog.Terrain != null) terrain.materialTemplate = catalog.Terrain;
            terrain.basemapDistance = 400f;
            terrain.heightmapPixelError = 8f;
            terrain.drawTreesAndFoliage = false;
            return go;
        }

        /// <summary>Rend les données d'un terrain créé par <see cref="Ground"/> (couches et carte), avant de le détruire.</summary>
        public static void Release(GameObject ground)
        {
            Terrain terrain = ground != null ? ground.GetComponent<Terrain>() : null;
            if (terrain == null || terrain.terrainData == null) return;
            foreach (TerrainLayer layer in terrain.terrainData.terrainLayers)
                if (layer != null) Object.Destroy(layer);
            Object.Destroy(terrain.terrainData);
        }

        private static readonly Dictionary<StyleSurface, Material> SurfaceMaterials = new Dictionary<StyleSurface, Material>();
        private static Material _water;

        /// <summary>
        /// Essai réaliste (demande de Damien du 26 octobre 2026 : « les bâtiments et la rivière sont à faire aussi ») : matériau d'une matière
        /// de bâtiment (texture de couleur et de relief du catalogue) et taille d'une répétition ; null sans texture (couleur simple gardée).
        /// </summary>
        public static Material Surface(StyleSurface kind, out float tile)
        {
            tile = 2.5f;
            StyleCatalog catalog = StyleCatalog.Loaded;
            SurfaceLook look = catalog != null ? catalog.Surface(kind) : null;
            if (look == null) return null;
            tile = look.Tile > 0.1f ? look.Tile : 2.5f;
            if (SurfaceMaterials.TryGetValue(kind, out Material cached) && cached != null) return cached;
            Material material = ArenaView.MakeMaterial(Color.white);
            material.SetTexture("_BaseMap", look.Color);
            material.SetTexture("_MainTex", look.Color);
            if (look.Normal != null)
            {
                material.SetTexture("_BumpMap", look.Normal);
                material.EnableKeyword("_NORMALMAP");
            }
            material.SetFloat("_Smoothness", kind == StyleSurface.Roof ? 0.25f : 0.1f);
            SurfaceMaterials[kind] = material;
            return material;
        }

        /// <summary>
        /// Eau de la rivière : couleur du catalogue, surface lisse qui reflète le ciel, petites vagues (carte de relief faite par le code,
        /// sans fichier) qui glissent avec le temps (<see cref="AnimateWater"/>). Null sans catalogue.
        /// </summary>
        public static Material Water()
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null) return null;
            if (_water != null) return _water;
            _water = ArenaView.MakeMaterial(catalog.WaterColor);
            _water.SetTexture("_BumpMap", Ripples(128));
            _water.EnableKeyword("_NORMALMAP");
            _water.SetFloat("_BumpScale", 0.5f);
            _water.SetFloat("_Smoothness", 0.9f);
            return _water;
        }

        /// <summary>Fait glisser les vagues de l'eau (toutes les cartes d'URP Lit suivent le décalage de la carte de couleur).</summary>
        public static void AnimateWater(float time)
        {
            if (_water != null) _water.SetTextureOffset("_BaseMap", new Vector2(time * 0.03f, time * 0.05f));
        }

        /// <summary>
        /// Carte de relief de petites vagues, qui se répète sans couture : somme d'ondes entières sur la case, pente convertie en normale
        /// (x et y dans le rouge et le vert, alpha plein : lisible par le rendu sur ordinateur comme sur téléphone).
        /// </summary>
        private static Texture2D Ripples(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            var pixels = new Color32[size * size];
            float tau = 2f * Mathf.PI / size;
            // Ondes à vecteurs entiers (kx, ky) et amplitude : la carte se répète sans couture.
            int[] kx = { 3, 2, 7 }, ky = { 1, -5, 4 };
            float[] amplitude = { 1f, 0.6f, 0.3f };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = 0f, dy = 0f;
                    for (int w = 0; w < kx.Length; w++)
                    {
                        float c = Mathf.Cos(tau * (kx[w] * x + ky[w] * y)) * amplitude[w];
                        dx += kx[w] * c;
                        dy += ky[w] * c;
                    }
                    var n = new Vector3(-dx * 0.06f, -dy * 0.06f, 1f).normalized;
                    pixels[y * size + x] = new Color32((byte)(n.x * 127f + 128f), (byte)(n.y * 127f + 128f), (byte)(n.z * 127f + 128f), 255);
                }
            texture.SetPixels32(pixels);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.Apply(true);
            return texture;
        }

        private static Texture2D Pick(Texture2D wanted, Texture2D fallback) => wanted != null ? wanted : fallback;

        private static TerrainLayer Layer(Texture2D color, Texture2D normal, float tile)
        {
            var layer = new TerrainLayer();
            layer.diffuseTexture = color;
            layer.normalMapTexture = normal;
            layer.tileSize = new Vector2(tile, tile);
            return layer;
        }
    }
}
