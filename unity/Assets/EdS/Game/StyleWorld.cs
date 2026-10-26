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
