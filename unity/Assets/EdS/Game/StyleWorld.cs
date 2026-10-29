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
            RenderSettings.skybox = catalog.Sky;
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
            if (Indoors)
            {
                IndoorLight(catalog);
                return;
            }
            if (!Active || catalog == null) return;
            if (_camera != null && catalog.Sky != null) _camera.clearFlags = CameraClearFlags.Skybox;
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

        private static CameraClearFlags _outdoorClear = CameraClearFlags.Skybox;
        private static (AmbientMode Mode, Color Sky, Color Horizon, Color Ground, bool Fog) _outdoorAmbient =
            (AmbientMode.Skybox, new Color(0.5f, 0.6f, 0.76f), new Color(0.52f, 0.53f, 0.5f), new Color(0.26f, 0.24f, 0.2f), false);
        private static Color _outdoorBackground;

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
                _colors.postExposure.value = catalog.Exposure + catalog.IndoorExposure;
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
        private const float TerrainHeight = 10f;

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
                    heights[row, col] = (float)((TerrainBase - dig) / TerrainHeight);
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
            _deckRise > 0f ? (float)StyleShapes.StandHeight(x, z, _deckX0, _deckZ0, _deckX1, _deckZ1, _deckRise) : 0f;

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
