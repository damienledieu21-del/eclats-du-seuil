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
            // Essai réaliste 4 : sur téléphone, le rendu léger du catalogue (ombres et effets réduits) ; l'ordinateur garde celui du projet.
            // Revue Codex de la PR #59 : les réglages du téléphone du sous-lot 11.8 coupent les ombres (qualité et soleil) ; avec le rendu
            // léger, le soleil retrouve des ombres dures (35 m, deux cascades, réglées dans ce rendu).
            bool phoneShadows = Application.isMobilePlatform && catalog.PhonePipeline != null;
            if (phoneShadows)
            {
                QualitySettings.renderPipeline = catalog.PhonePipeline;
                // Nom qualifié : URP a aussi un ShadowQuality (UnityEngine.Rendering.Universal) ; non qualifié, Unity refuse de compiler
                // (essai de Damien du 27 octobre 2026 au soir).
                QualitySettings.shadows = UnityEngine.ShadowQuality.HardOnly;
            }
            // Sous-lot V2.1 : une copie du ciel, dont la clarté et la teinte suivent l'heure (le ciel du catalogue n'est jamais modifié).
            if (_sky == null)
            {
                _sky = new Material(catalog.Sky);
                _skyExposure = _sky.HasProperty("_Exposure") ? _sky.GetFloat("_Exposure") : 1f;
                _skyTint = _sky.HasProperty("_Tint") ? _sky.GetColor("_Tint") : Color.white;
            }
            RenderSettings.skybox = _sky;
            camera.clearFlags = CameraClearFlags.Skybox;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional)
                {
                    _sun = light;
                    if (phoneShadows) light.shadows = LightShadows.Hard;
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
            // Sous-lot V1.9.8 : le souvenir de la Rupture a sa propre ambiance, avant celle des pièces.
            if (Memory)
            {
                MemoryLight(catalog);
                return;
            }
            if (Indoors)
            {
                IndoorLight(catalog);
                return;
            }
            if (!Active || catalog == null) return;
            if (_camera != null && catalog.Sky != null) _camera.clearFlags = CameraClearFlags.Skybox;
            // Sous-lot V2.1 : la lumière de l'heure (facteurs sur les réglages du catalogue, qui restent ceux du plein jour).
            DayLight light = _hasLight ? _light : DayCycle.At(12 * 60);
            if (Application.isEditor && catalog.PreviewHour >= 0f) light = DayCycle.At(catalog.PreviewHour * 60f);
            // Sous-lot V2.2 : le temps (facteurs par-dessus l'heure ; temps clair : aucun changement).
            WeatherLook weather = _weather;
            float ambient = (float)light.AmbientLevel;
            // La clarté de la nuit se règle dans le catalogue (1 : la nuit prévue) ; elle ne touche pas le jour.
            ambient = Mathf.Min(1f, ambient * Mathf.Lerp(1f, catalog.NightBrightness, (float)light.Night)) * (float)weather.Ambient;
            Color ambientTint = Tint(light.AmbientTint) * Tint(weather.AmbientTint) * ambient;
            // Sous-lot V2.6 : près d'une cicatrice du Seuil, la lumière tire vers le froid.
            ambientTint = Color.Lerp(ambientTint, ambientTint * ScarCold, _scarFade);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = catalog.AmbientSky * ambientTint;
            RenderSettings.ambientEquatorColor = catalog.AmbientHorizon * ambientTint;
            RenderSettings.ambientGroundColor = catalog.AmbientGround * ambientTint;
            // Sous-lot V2.2 : sans brume au catalogue, la brume du matin et la pluie en posent une quand même (de 30 à 180 m par temps clair).
            bool catalogFog = catalog.FogEnd > catalog.FogStart;
            bool weatherFog = weather.FogEnd < 0.95;
            float fogStart = catalogFog ? catalog.FogStart : 30f, fogEnd = catalogFog ? catalog.FogEnd : 180f;
            RenderSettings.fog = catalogFog || weatherFog;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Color.Lerp(catalog.FogColor * Tint(light.FogTint) * Tint(weather.FogTint), ScarCold * (0.4f + 0.6f * ambient), _scarFade * 0.7f);
            if (_scarFade > 0.01f) RenderSettings.fog = true;
            if (_sky != null)
            {
                if (_sky.HasProperty("_Exposure"))
                    _sky.SetFloat("_Exposure", _skyExposure * (float)light.SkyLevel * Mathf.Lerp(1f, catalog.NightBrightness, (float)light.Night) * (float)weather.Sky);
                if (_sky.HasProperty("_Tint")) _sky.SetColor("_Tint", _skyTint * Tint(light.FogTint) * Tint(weather.FogTint));
            }
            Lamps = Mathf.Max((float)light.Lamps, (float)weather.Lamps);
            NightShare = (float)light.Night;
            // La brume du matin et la pluie rapprochent la brume du catalogue (jamais à moins de 9 m : on voit toujours autour de soi).
            RenderSettings.fogStartDistance = fogStart * (float)weather.FogStart;
            RenderSettings.fogEndDistance = Mathf.Max(9f, fogEnd * (float)weather.FogEnd * Mathf.Lerp(1f, 0.3f, _scarFade));   // sous-lot V2.6 : brume froide plus proche
            if (_camera != null) _camera.farClipPlane = Mathf.Max(250f, catalog.FogEnd + 20f);
            if (_sun != null)
            {
                // Le jour, le soleil du catalogue ; la nuit, la lune (froide, haute, à l'opposé) ; entre les deux, l'aube rose et le soir orangé.
                _sun.color = catalog.SunColor * Tint(light.SunTint);
                _sun.intensity = catalog.SunIntensity * (float)light.SunLevel * Mathf.Lerp(1f, catalog.NightBrightness, (float)light.Night) * (float)weather.Sun;
                _sun.transform.rotation = Quaternion.Euler((float)light.Height(catalog.SunHeight), catalog.SunTurn + (light.Moon ? 180f : 0f), 0f);
            }
            if (_colors != null)
            {
                _colors.postExposure.value = catalog.Exposure + (float)light.Exposure + (float)weather.Exposure + BrightnessSetting.ExposureOffset;   // V2.13 : curseur de luminosité
                _colors.contrast.value = catalog.Contrast;
                _colors.saturation.value = catalog.Saturation + (float)light.Saturation + (float)weather.Saturation - 70f * _scarFade;   // sous-lot V2.6 : couleurs fanées
                _vignette.intensity.value = catalog.Vignette;
                // Le halo coûte cher sur téléphone (Note8) : seulement sur ordinateur.
                _bloom.active = !Application.isMobilePlatform && catalog.Bloom > 0f;
                _bloom.intensity.value = catalog.Bloom;
            }
        }

        /// <summary>
        /// Intérieurs (choix de Damien du 28 octobre 2026 : une porte qui mène à un intérieur séparé) : le personnage est dans une pièce. Pas
        /// de ciel ni de soleil ni de brume : le noir autour de la pièce, une ambiance sombre et chaude, et ce sont le feu, les bougies et les
        /// lanternes qui éclairent (<see cref="SpaceView"/>). Rendu dehors, tout reprend les réglages du catalogue.
        /// </summary>
        public static bool Indoors { get; private set; }

        /// <summary>Entre dans une pièce ou en sort (voir <see cref="Indoors"/>).</summary>
        public static void SetIndoors(bool indoors)
        {
            if (Indoors == indoors) return;
            Indoors = indoors;
            if (_sun == null)
                foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (light.type == LightType.Directional)
                    {
                        _sun = light;
                        break;
                    }
            Camera camera = _camera != null ? _camera : Camera.main;
            if (indoors && camera != null)
            {
                _outdoorClear = camera.clearFlags;
                _outdoorBackground = camera.backgroundColor;
            }
            if (indoors)
                _outdoorAmbient = (RenderSettings.ambientMode, RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor,
                    RenderSettings.fog);
            if (!indoors && !Active)
            {
                (RenderSettings.ambientMode, RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor,
                    RenderSettings.fog) = _outdoorAmbient;
                if (_sun != null && _outdoorSun > 0f) _sun.intensity = _outdoorSun;
                if (camera != null)
                {
                    camera.clearFlags = _outdoorClear;
                    camera.backgroundColor = _outdoorBackground;
                }
            }
            Refresh();
        }

        /// <summary>
        /// Sous-lot V1.9.8 (demande de Damien du 5 novembre 2026 : une ouverture marquante) : le personnage revit un souvenir (la Rupture,
        /// il y a trois cents ans). Changement d'ambiance net : nuit bleue, brume, lumière froide, couleurs lavées, halo fort ; c'est la porte
        /// de lumière qui éclaire (<see cref="StoryStage"/>). Rendu, tout reprend : la pièce, ou dehors.
        /// </summary>
        public static bool Memory { get; private set; }

        /// <summary>Entre dans un souvenir ou en sort (voir <see cref="Memory"/>).</summary>
        public static void SetMemory(bool memory)
        {
            if (Memory == memory) return;
            Memory = memory;
            if (!memory)
            {
                if (_colors != null && _colors.colorFilter != null) _colors.colorFilter.value = Color.white;
                RenderSettings.fogMode = FogMode.Linear;
                // Sans catalogue, Refresh ne remet rien dehors : on rend l'ambiance d'avant la première pièce.
                if (!Active && !Indoors)
                {
                    (RenderSettings.ambientMode, RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor,
                        RenderSettings.fog) = _outdoorAmbient;
                    if (_sun != null && _outdoorSun > 0f) _sun.intensity = _outdoorSun;
                    Camera outdoor = _camera != null ? _camera : Camera.main;
                    if (outdoor != null)
                    {
                        outdoor.clearFlags = _outdoorClear;
                        outdoor.backgroundColor = _outdoorBackground;
                    }
                }
            }
            Refresh();
        }

        /// <summary>Couleurs du souvenir : brume de nuit, ciel au loin, lumière ambiante froide.</summary>
        public static readonly Color MemoryFog = new Color(0.3f, 0.36f, 0.58f), MemorySky = new Color(0.04f, 0.05f, 0.11f);

        private static void MemoryLight(StyleCatalog catalog)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.4f, 0.46f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.26f, 0.3f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.1f, 0.1f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = MemoryFog;
            RenderSettings.fogDensity = 0.035f;
            if (_sun != null)
            {
                if (_outdoorSun <= 0f && _sun.intensity > 0f) _outdoorSun = _sun.intensity;
                _sun.intensity = 0f;
            }
            Camera camera = _camera != null ? _camera : Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = MemorySky;
            }
            if (_colors != null)
            {
                float exposure = catalog != null ? catalog.Exposure + catalog.IndoorExposure : 0f;
                _colors.postExposure.value = exposure + 0.35f + BrightnessSetting.ExposureOffset;
                _colors.contrast.value = (catalog != null ? catalog.Contrast : 0f) + 12f;
                _colors.saturation.value = -45f;
                if (_colors.colorFilter != null) _colors.colorFilter.value = new Color(0.86f, 0.93f, 1.08f);
                _vignette.intensity.value = 0.45f;
                _bloom.active = !Application.isMobilePlatform;
                _bloom.intensity.value = Mathf.Max(1.2f, (catalog != null ? catalog.Bloom : 0f) * 2f);
            }
        }

        private static CameraClearFlags _outdoorClear = CameraClearFlags.Skybox;
        private static (AmbientMode Mode, Color Sky, Color Horizon, Color Ground, bool Fog) _outdoorAmbient =
            (AmbientMode.Skybox, new Color(0.5f, 0.6f, 0.76f), new Color(0.52f, 0.53f, 0.5f), new Color(0.26f, 0.24f, 0.2f), false);
        private static Color _outdoorBackground;

        /// <summary>Sous-lot V2.1 : copie du ciel du catalogue (clarté et teinte de l'heure), sa clarté et sa teinte d'origine.</summary>
        private static Material _sky;
        private static float _skyExposure = 1f;
        private static Color _skyTint = Color.white;

        /// <summary>Sous-lot V2.1 : lumière de l'heure donnée par le jeu (sinon, le plein jour).</summary>
        private static DayLight _light;
        private static bool _hasLight;

        /// <summary>Sous-lot V2.1 : lanternes et fenêtres allumées (0 à 1), selon l'heure montrée (aperçu de l'éditeur compris).</summary>
        public static float Lamps { get; private set; }

        /// <summary>Sous-lot V2.1 : part de nuit (0 à 1), selon l'heure montrée.</summary>
        public static float NightShare { get; private set; }

        /// <summary>
        /// Sous-lot V2.1 (jour et nuit qui changent le monde) : la lumière de l'heure du monde (<see cref="DayCycle"/>), appliquée aux réglages
        /// du catalogue à chaque image dehors ; null : le plein jour (entraînement, réseau). Les pièces et le souvenir gardent leur ambiance.
        /// </summary>
        public static void SetDayLight(DayLight? light)
        {
            _hasLight = light.HasValue;
            if (light.HasValue) _light = light.Value;
            // Sans catalogue (pas d'ambiance réaliste), les lanternes suivent quand même l'heure.
            Lamps = light.HasValue ? Mathf.Max((float)light.Value.Lamps, (float)_weather.Lamps) : 0f;
            NightShare = light.HasValue ? (float)light.Value.Night : 0f;
            Refresh();
        }

        /// <summary>Sous-lot V2.2 : le temps montré (temps clair hors de l'aventure).</summary>
        private static WeatherLook _weather = WeatherLook.Clear;

        /// <summary>Sous-lot V2.2 : le temps montré (pluie, brume, sol mouillé), pour la météo et le décor.</summary>
        public static WeatherLook Weather => _weather;

        /// <summary>
        /// Sous-lot V2.2 (météo) : le temps du monde (<see cref="WeatherCycle.Look"/>), appliqué par-dessus la lumière de l'heure ; null : temps
        /// clair (entraînement, réseau). Appelée avant <see cref="SetDayLight"/>, qui l'applique.
        /// </summary>
        public static void SetWeather(WeatherLook? look) => _weather = look ?? WeatherLook.Clear;

        /// <summary>
        /// Sous-lot V2.6 : à quel point les couleurs se fanent autour de la caméra (0 à 1 ; une cicatrice du Seuil) : moins de couleurs, une
        /// lumière et une brume plus froides, une brume plus proche. Affichage seulement.
        /// </summary>
        public static void SetScarFade(float fade) => _scarFade = Mathf.Clamp01(fade);

        private static float _scarFade;

        /// <summary>Teinte froide d'une cicatrice (lumière et brume).</summary>
        private static readonly Color ScarCold = new Color(0.62f, 0.7f, 0.82f);

        private static Color Tint(StyleTint t) => new Color((float)t.R, (float)t.G, (float)t.B);

        /// <summary>Force du soleil avant la première entrée dans une pièce (sans catalogue, pour la lui rendre en sortant).</summary>
        private static float _outdoorSun;

        private static void IndoorLight(StyleCatalog catalog)
        {
            Color ambient = catalog != null ? catalog.IndoorAmbient : new Color(0.2f, 0.16f, 0.13f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambient;
            RenderSettings.ambientEquatorColor = ambient * new Color(0.85f, 0.85f, 0.85f);
            RenderSettings.ambientGroundColor = ambient * new Color(0.5f, 0.5f, 0.5f);
            RenderSettings.fog = false;
            if (_sun != null)
            {
                if (_outdoorSun <= 0f && _sun.intensity > 0f) _outdoorSun = _sun.intensity;
                _sun.intensity = 0f;
            }
            Camera camera = _camera != null ? _camera : Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.01f, 0.008f, 0.006f);
            }
            if (_colors != null && catalog != null)
            {
                _colors.postExposure.value = catalog.Exposure + catalog.IndoorExposure + BrightnessSetting.ExposureOffset;
                _colors.contrast.value = catalog.Contrast;
                _colors.saturation.value = catalog.Saturation;
                _vignette.intensity.value = Mathf.Min(0.5f, catalog.Vignette + 0.12f);
                _bloom.active = !Application.isMobilePlatform && catalog.Bloom > 0f;
                _bloom.intensity.value = catalog.Bloom * 1.4f;
            }
        }

        /// <summary>Le sol du terrain est à cette profondeur (m) sous son origine : de quoi creuser le lit de la rivière.</summary>
        private const float TerrainBase = 3f;

        /// <summary>Hauteur totale (m) que le terrain peut décrire.</summary>
        private const float TerrainHeight = 40f;   // V2.24 : collines et falaises hors de l'aire (jusqu'à une vingtaine de mètres)

        /// <summary>
        /// Sol réaliste d'un combat positionné : un terrain (hauteur 0 où l'on marche, comme le sol du moteur) qui couvre l'aire et
        /// <see cref="StyleCatalog.ForestMargin"/> mètres autour, peint en terre, roche et herbe d'après <paramref name="spots"/>
        /// (<see cref="StyleRules.GroundMix"/>). Essai de Damien du 27 octobre 2026 (« l'eau n'est pas du tout réaliste ») : le lit de la
        /// rivière est creusé sous la réunion des polygones de <paramref name="rivers"/> (revue Codex de la PR #58 : un seul lit, sans
        /// berge entre deux polygones voisins), avec des berges en pente douce (<see cref="StyleRules.ChannelDepth"/>) ; le sol hors de l'eau
        /// reste à 0. Null sans texture de sol dans le catalogue (le sol
        /// simple reste).
        /// </summary>
        public static GameObject Ground(Transform parent, float minX, float minZ, float maxX, float maxZ, IReadOnlyList<StyleSpot> spots,
            IReadOnlyList<IReadOnlyList<(double X, double Z)>> rivers = null, IReadOnlyList<StyleRoad> roads = null, IReadOnlyList<StyleField> fields = null,
            IReadOnlyList<IReadOnlyList<(double X, double Z)>> floors = null)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null) return null;
            Texture2D any = catalog.GrassColor != null ? catalog.GrassColor : catalog.SoilColor != null ? catalog.SoilColor : catalog.RockColor;
            if (any == null) return null;
            float margin = catalog.ForestMargin;
            float x0 = minX - margin, z0 = minZ - margin, width = maxX - minX + 2f * margin, depth = maxZ - minZ + 2f * margin;

            var data = new TerrainData();
            // Assez fin pour une berge (environ 0,4 m entre deux points sur l'aire des Vallons) ; plus grossier sur téléphone. Vallons
            // agrandis : la finesse suit la taille du terrain (autant de points par mètre qu'avant, au plus 1025 sur ordinateur, 513 sur
            // téléphone).
            bool phone = Application.isMobilePlatform;
            float side = Mathf.Max(width, depth);
            data.heightmapResolution = Resolution(side / 0.34f, phone ? 512 : 1024, 256) + 1;
            data.alphamapResolution = Resolution(side / 0.34f, phone ? 512 : 1024, 256);
            data.size = new Vector3(width, TerrainHeight, depth);
            int hres = data.heightmapResolution;
            var heights = new float[hres, hres];
            IReadOnlyList<(double X0, double Z0, double X1, double Z1)> banks = StyleRules.UnionBoundary(rivers);
            for (int row = 0; row < hres; row++)
                for (int col = 0; col < hres; col++)
                {
                    double x = x0 + col / (double)(hres - 1) * width, z = z0 + row / (double)(hres - 1) * depth;
                    double dig = banks.Count > 0 ? StyleRules.ChannelDepth(StyleRules.UnionDistance(x, z, rivers, banks)) : 0;
                    heights[row, col] = (float)((TerrainBase - dig + (_relief.IsFlat ? 0 : _relief.Height(x, z))) / TerrainHeight);   // V2.24 : relief
                }
            data.SetHeights(0, 0, heights);
            // Couches dans l'ordre de StyleGround (terre, roche, herbe) ; une couche sans texture prend celle d'une autre. Essai du 27 octobre
            // 2026 : l'herbe de Poly Haven sortait jaune ; elle prend la teinte du catalogue.
            // Essai réaliste 8 : sur ordinateur, deux couches de plus s'il y a leurs textures : la terre battue des chemins et la terre
            // labourée des champs (sinon ils prennent la terre, comme avant).
            bool extra = !phone && (catalog.RoadColor != null || catalog.FieldColor != null);
            var layers = new List<TerrainLayer>
            {
                Layer(Pick(catalog.SoilColor, any), Pick(catalog.SoilNormal, catalog.GrassNormal), catalog.GroundTile, Color.white),
                Layer(Pick(catalog.RockColor, any), catalog.RockNormal, catalog.GroundTile, Color.white),
                Layer(Pick(catalog.GrassColor, any), Pick(catalog.GrassNormal, catalog.SoilNormal), catalog.GroundTile, catalog.GrassTint),
            };
            if (extra)
            {
                Texture2D soil = Pick(catalog.SoilColor, any);
                layers.Add(Layer(Pick(catalog.RoadColor, soil), catalog.RoadColor != null ? catalog.RoadNormal : catalog.SoilNormal, catalog.GroundTile * 0.8f, Color.white));
                layers.Add(Layer(Pick(catalog.FieldColor, soil), catalog.FieldColor != null ? catalog.FieldNormal : catalog.SoilNormal, catalog.GroundTile * 0.6f, Color.white));
            }
            data.terrainLayers = layers.ToArray();
            int res = data.alphamapResolution;
            var maps = new float[res, res, extra ? 5 : 3];
            // Vallons agrandis : taches rangées par cases, chemins de terre battue, terre sous les champs.
            var grid = new SpotGrid(spots);
            for (int row = 0; row < res; row++)
                for (int col = 0; col < res; col++)
                {
                    double x = x0 + (col + 0.5f) / res * width, z = z0 + (row + 0.5f) / res * depth;
                    StyleMix mix = grid.Mix(x, z);
                    double road = roads != null && roads.Count > 0 ? TownRules.RoadWeight(x, z, TownRules.RoadAt(x, z, roads)) : 0;
                    if (!extra && road > 0) mix = new StyleMix(mix.Soil * (1 - road) + road, mix.Rock * (1 - road), mix.Grass * (1 - road));
                    // Relecture des Vallons agrandis : sous-bois de terre sous les bois (comme la carte peinte), fondu sur un mètre et demi.
                    if (floors != null)
                        foreach (IReadOnlyList<(double X, double Z)> floor in floors)
                        {
                            double inside = StyleRules.InsideDistance(x, z, floor);
                            if (inside <= -1.5) continue;
                            double w = System.Math.Min(1, (inside + 1.5) / 3);
                            mix = new StyleMix(mix.Soil * (1 - w) + w, mix.Rock * (1 - w), mix.Grass * (1 - w));
                        }
                    // V2.24 : la roche affleure sur les pentes raides des collines et falaises hors de l'aire (pente lue sur la carte des hauteurs).
                    if (!_relief.IsFlat && _relief.Outside(x, z) > 0)
                    {
                        int hr = Mathf.Min(hres - 2, row * (hres - 1) / res), hc = Mathf.Min(hres - 2, col * (hres - 1) / res);
                        double sx = (heights[hr, hc + 1] - heights[hr, hc]) * TerrainHeight * (hres - 1) / width;
                        double sz = (heights[hr + 1, hc] - heights[hr, hc]) * TerrainHeight * (hres - 1) / depth;
                        double steep = System.Math.Max(0, System.Math.Min(1, (System.Math.Sqrt(sx * sx + sz * sz) - 0.6) / 0.5));
                        if (steep > 0) mix = new StyleMix(mix.Soil * (1 - steep), mix.Rock * (1 - steep) + steep, mix.Grass * (1 - steep));
                    }
                    double field = 0;
                    if (fields != null)
                        foreach (StyleField f in fields)
                            if (x > f.MinX - 0.3 && x < f.MaxX + 0.3 && z > f.MinZ - 0.3 && z < f.MaxZ + 0.3)
                            {
                                if (extra) field = 1;
                                else mix = new StyleMix(1, 0, 0);
                                break;
                            }
                    // Chemin et champ prennent leur part ; la berge de la rivière reprend la terre partout (pas de chemin sous l'eau).
                    double wet = banks.Count > 0 ? StyleRules.RiverWet(StyleRules.UnionDistance(x, z, rivers, banks)) : 0;
                    StyleLayers parts = StyleRules.GroundLayers(mix, extra ? road : 0, field, wet);
                    maps[row, col, (int)StyleGround.Soil] = (float)parts.Soil;
                    maps[row, col, (int)StyleGround.Rock] = (float)parts.Rock;
                    maps[row, col, (int)StyleGround.Grass] = (float)parts.Grass;
                    if (extra)
                    {
                        maps[row, col, (int)StyleGround.Road] = (float)parts.Road;
                        maps[row, col, (int)StyleGround.Field] = (float)parts.Field;
                    }
                }
            data.SetAlphamaps(0, 0, maps);

            var go = new GameObject("Sol (essai réaliste)");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x0, -TerrainBase, z0);
            Terrain terrain = go.AddComponent<Terrain>();
            terrain.terrainData = data;
            if (catalog.Terrain != null) terrain.materialTemplate = catalog.Terrain;
            terrain.basemapDistance = 400f;
            // Berges fines sur ordinateur (le lit ne doit pas remonter au-dessus de l'eau au loin) ; moins de détail sur téléphone.
            terrain.heightmapPixelError = Application.isMobilePlatform ? 8f : 3f;
            terrain.drawTreesAndFoliage = false;
            return go;
        }

        /// <summary>Vallons agrandis : résolution d'une carte du terrain : la puissance de 2 qui approche <paramref name="wanted"/>, bornée.</summary>
        private static int Resolution(float wanted, int max, int min)
        {
            int r = min;
            while (r < max && r < wanted) r *= 2;
            return Mathf.Min(max, r);
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
        /// Eau de la rivière. Essai de Damien du 27 octobre 2026 (« l'eau n'est pas du tout réaliste ») : le matériau d'eau du catalogue
        /// (shader « EdS/Eau » : transparente, reflet du vrai ciel selon l'angle de vue, deux couches de vagues qui avancent, éclat du
        /// soleil), avec le ciel du catalogue pour ses reflets. Sans lui, comme avant : couleur du catalogue, surface lisse, petites vagues
        /// (carte de relief faite par le code) qui glissent (<see cref="AnimateWater"/>). Null sans catalogue.
        /// </summary>
        public static Material Water()
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null) return null;
            if (_water != null) return _water;
            // Essai réaliste 8 (point 3 de la demande de Damien du 28 octobre 2026 : l'eau des samples de Unity) : sur ordinateur, l'eau de
            // lac des Production Ready Shaders (rides qui bougent, reflets, lit de la rivière vu par transparence, couleur de la profondeur),
            // si la préparation l'a trouvée et que son shader se compile. Elle se règle dans son matériau (WaterLake). Le téléphone garde
            // « EdS/Eau » (cette eau lit l'image et la profondeur de la scène, trop coûteuses pour lui).
            if (!Application.isMobilePlatform && catalog.SampleWater != null && catalog.SampleWater.shader != null && catalog.SampleWater.shader.isSupported)
            {
                _water = new Material(catalog.SampleWater);
                _shaderWater = true;
                return _water;
            }
            if (catalog.Water != null && catalog.Water.shader != null && catalog.Water.shader.isSupported)
            {
                _water = new Material(catalog.Water);
                _shaderWater = true;
                // Vagues faites par le code (format lu tel quel par le shader, sur ordinateur comme sur téléphone).
                if (_water.HasProperty("_Ripples")) _water.SetTexture("_Ripples", Ripples(256));
                Texture sky = catalog.Sky != null && catalog.Sky.HasProperty("_MainTex") ? catalog.Sky.GetTexture("_MainTex") : null;
                if (sky != null && _water.HasProperty("_SkyTex")) _water.SetTexture("_SkyTex", sky);
                if (_water.HasProperty("_Color")) _water.SetColor("_Color", catalog.WaterColor);
                return _water;
            }
            _water = ArenaView.MakeMaterial(catalog.WaterColor);
            _water.SetTexture("_BumpMap", Ripples(128));
            _water.EnableKeyword("_NORMALMAP");
            _water.SetFloat("_BumpScale", 0.5f);
            _water.SetFloat("_Smoothness", 0.9f);
            return _water;
        }

        /// <summary>
        /// Fait glisser les vagues de l'eau simple (toutes les cartes d'URP Lit suivent le décalage de la carte de couleur) ; l'eau du
        /// shader « EdS/Eau » avance seule.
        /// </summary>
        public static void AnimateWater(float time)
        {
            if (_water != null && !_shaderWater) _water.SetTextureOffset("_BaseMap", new Vector2(time * 0.03f, time * 0.05f));
            // Essai réaliste 4 : les reflets de la sonde, dès que sa texture existe (sinon le ciel seul).
            if (_water != null && _shaderWater)
            {
                Texture reflections = _probe != null ? _probe.realtimeTexture : null;
                _water.SetTexture("_ReflectionCube", reflections);
                _water.SetFloat("_ProbeWeight", reflections != null ? 1f : 0f);
            }
        }

        private static ReflectionProbe _probe;

        /// <summary>
        /// Essai réaliste 4 : sonde de reflets prise une fois au-dessus de l'eau (<paramref name="center"/>, sur <paramref name="size"/>), pour
        /// que la vraie eau reflète les berges, les arbres et le moulin ; l'eau elle-même (<paramref name="water"/>, couche « TransparentFX »)
        /// n'y est pas. Rien sans la vraie eau.
        /// </summary>
        public static ReflectionProbe Reflections(Transform parent, Vector3 center, Vector3 size, IEnumerable<GameObject> water)
        {
            if (!RealWater) return null;
            const int WaterLayer = 1;   // TransparentFX, couche fournie par Unity
            foreach (GameObject go in water) go.layer = WaterLayer;
            var holder = new GameObject("Reflets de la rivière (essai réaliste)");
            holder.transform.SetParent(parent, false);
            holder.transform.position = center;
            ReflectionProbe probe = holder.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = Application.isMobilePlatform ? 128 : 512;
            probe.size = size;
            probe.hdr = true;
            probe.cullingMask = ~(1 << WaterLayer);
            probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
            probe.RenderProbe();
            _probe = probe;
            return probe;
        }

        /// <summary>L'eau vient du shader « EdS/Eau » (surface sous le sol, berges creusées).</summary>
        public static bool RealWater => _water != null && _shaderWater;

        private static Material _rock;

        /// <summary>
        /// Essai réaliste 5 : matériau de pierre brute (texture de roche du sol du catalogue, avec son relief) pour le muret de pierres
        /// sèches, les culées du pont et les socles de l'abri ; null sans elle. <paramref name="tile"/> : une répétition tous les 1,2 m.
        /// </summary>
        public static Material Rock(out float tile)
        {
            tile = 1.2f;
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null || catalog.RockColor == null) return null;
            if (_rock != null) return _rock;
            _rock = ArenaView.MakeMaterial(Color.white);
            _rock.SetTexture("_BaseMap", catalog.RockColor);
            _rock.SetTexture("_MainTex", catalog.RockColor);
            if (catalog.RockNormal != null)
            {
                _rock.SetTexture("_BumpMap", catalog.RockNormal);
                _rock.EnableKeyword("_NORMALMAP");
            }
            _rock.SetFloat("_Smoothness", 0.08f);
            return _rock;
        }

        private static float _deckX0, _deckZ0, _deckX1, _deckZ1, _deckRise;

        /// <summary>Essai réaliste 5 : pont cintré posé (emprise du tablier et flèche) ; une flèche nulle l'enlève.</summary>
        public static void Deck(float x0, float z0, float x1, float z1, float rise)
        {
            _deckX0 = x0;
            _deckZ0 = z0;
            _deckX1 = x1;
            _deckZ1 = z1;
            _deckRise = rise;
        }

        /// <summary>
        /// Essai réaliste 5 : hauteur où se tient un personnage en (x, z) : sur le tablier bombé du pont, 0 ailleurs
        /// (<see cref="StyleShapes.StandHeight"/>). Affichage seulement : le moteur ne connaît pas la hauteur.
        /// </summary>
        public static float StandHeight(float x, float z) =>
            (_relief.IsFlat ? 0f : (float)_relief.Height(x, z))
            + (_deckRise > 0f ? (float)StyleShapes.StandHeight(x, z, _deckX0, _deckZ0, _deckX1, _deckZ1, _deckRise) : 0f);

        private static GroundRelief _relief = GroundRelief.Flat;

        /// <summary>
        /// Sous-lot V2.24 (décision de Damien du 11 novembre 2026) : relief affiché du décor posé (<see cref="GroundRelief"/>) ; null : sol plat.
        /// Tout ce qui se tient au sol passe par <see cref="StandHeight"/> (personnages, décor, marqueurs, effets) ; le moteur reste plat.
        /// </summary>
        public static void SetRelief(GroundRelief relief)
        {
            _relief = relief ?? GroundRelief.Flat;
            _reliefKey = null;
        }

        private static string _reliefKey;

        /// <summary>
        /// Revue Codex de la PR #144 (2e tour) : pose le relief de la zone de <paramref name="map"/> (plat sans carte, dans une pièce, ou sans
        /// terrain peint : le sol de remplacement est un pavé plat). Appelé avant de construire les repères d'une zone (qui précèdent le décor
        /// de <c>SpaceView</c>) et par <c>SpaceView</c> ; la même carte garde le même relief (pas recalculé).
        /// </summary>
        public static void UseRelief(AdventureMap map)
        {
            if (map == null || !CanPaintGround)
            {
                SetRelief(null);
                return;
            }
            if (_reliefKey == map.Signature) return;
            SetRelief(GroundRelief.For(map));
            _reliefKey = map.Signature;
        }

        /// <summary>Revue Codex de la PR #144 : <see cref="Ground"/> peut-il faire un terrain (catalogue et au moins une texture de sol) ?</summary>
        public static bool CanPaintGround
        {
            get
            {
                StyleCatalog catalog = StyleCatalog.Loaded;
                return catalog != null && (catalog.GrassColor != null || catalog.SoilColor != null || catalog.RockColor != null);
            }
        }

        /// <summary>Sous-lot V2.24 : le sol affiché a-t-il du relief ?</summary>
        public static bool HasRelief => !_relief.IsFlat;

        /// <summary>Sous-lot V2.24 : relief affiché en cours (plat hors des zones dehors).</summary>
        public static GroundRelief Relief => _relief;

        /// <summary>Sous-lot V2.24 : <paramref name="p"/> remonté de la hauteur du sol affiché sous lui (y : hauteur au-dessus du sol).</summary>
        public static Vector3 OnGround(Vector3 p) => new Vector3(p.x, p.y + StandHeight(p.x, p.z), p.z);

        /// <summary>Sous-lot V2.24 : rotation qui couche une chose plate (flaque, disque) sur la pente du sol affiché en (x, z).</summary>
        public static Quaternion GroundTilt(float x, float z)
        {
            if (_relief.IsFlat) return Quaternion.identity;
            const float e = 0.3f;
            var normal = new Vector3(StandHeight(x - e, z) - StandHeight(x + e, z), 2f * e, StandHeight(x, z - e) - StandHeight(x, z + e));
            return Quaternion.FromToRotation(Vector3.up, normal.normalized);
        }

        /// <summary>Revue Codex de la PR #60 : emprise du tablier du pont cintré (x0, z0, x1, z1), ou null sans pont.</summary>
        public static (double X0, double Z0, double X1, double Z1)? Deck() =>
            _deckRise > 0f ? (_deckX0, _deckZ0, _deckX1, _deckZ1) : ((double, double, double, double)?)null;

        /// <summary>Revue Codex de la PR #60 : la boîte [x0, x1] × [z0, z1] touche-t-elle le tablier du pont cintré ?</summary>
        public static bool OnDeck(float x0, float z0, float x1, float z1) =>
            _deckRise > 0f && x1 >= _deckX0 && x0 <= _deckX1 && z1 >= _deckZ0 && z0 <= _deckZ1;

        private static bool _shaderWater;

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
            int[] kx = { 3, 2, 7, -5, 11 }, ky = { 1, -5, 4, 6, -3 };
            float[] amplitude = { 1f, 0.6f, 0.3f, 0.25f, 0.12f };
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

        private static TerrainLayer Layer(Texture2D color, Texture2D normal, float tile, Color tint)
        {
            var layer = new TerrainLayer();
            layer.diffuseTexture = color;
            layer.normalMapTexture = normal;
            layer.tileSize = new Vector2(tile, tile);
            // Le terrain d'URP multiplie la couleur de la couche par (max - min) : la teinte.
            layer.diffuseRemapMin = Vector4.zero;
            layer.diffuseRemapMax = new Vector4(tint.r, tint.g, tint.b, 1f);
            return layer;
        }
    }
}
