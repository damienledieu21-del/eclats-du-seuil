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
            IReadOnlyList<IReadOnlyList<(double X, double Z)>> rivers = null)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null) return null;
            Texture2D any = catalog.GrassColor != null ? catalog.GrassColor : catalog.SoilColor != null ? catalog.SoilColor : catalog.RockColor;
            if (any == null) return null;
            float margin = catalog.ForestMargin;
            float x0 = minX - margin, z0 = minZ - margin, width = maxX - minX + 2f * margin, depth = maxZ - minZ + 2f * margin;

            var data = new TerrainData();
            // Assez fin pour une berge (environ 0,4 m entre deux points sur l'aire des Vallons) ; plus grossier sur téléphone.
            data.heightmapResolution = Application.isMobilePlatform ? 257 : 513;
            data.alphamapResolution = Application.isMobilePlatform ? 256 : 512;
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
            data.terrainLayers = new[]
            {
                Layer(Pick(catalog.SoilColor, any), Pick(catalog.SoilNormal, catalog.GrassNormal), catalog.GroundTile, Color.white),
                Layer(Pick(catalog.RockColor, any), catalog.RockNormal, catalog.GroundTile, Color.white),
                Layer(Pick(catalog.GrassColor, any), Pick(catalog.GrassNormal, catalog.SoilNormal), catalog.GroundTile, catalog.GrassTint),
            };
            int res = data.alphamapResolution;
            var maps = new float[res, res, 3];
            for (int row = 0; row < res; row++)
                for (int col = 0; col < res; col++)
                {
                    double x = x0 + (col + 0.5f) / res * width, z = z0 + (row + 0.5f) / res * depth;
                    StyleMix mix = StyleRules.GroundMix(x, z, spots);
                    if (banks.Count > 0) mix = StyleRules.RiverMix(mix, StyleRules.UnionDistance(x, z, rivers, banks));
                    maps[row, col, (int)StyleGround.Soil] = (float)mix.Soil;
                    maps[row, col, (int)StyleGround.Rock] = (float)mix.Rock;
                    maps[row, col, (int)StyleGround.Grass] = (float)mix.Grass;
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
