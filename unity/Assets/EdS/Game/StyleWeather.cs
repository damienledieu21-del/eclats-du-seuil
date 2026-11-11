using EdS.Runtime;
using EdS.World;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.2 (météo) : la pluie, les éclairs et le tonnerre, sans asset payant. Les gouttes sont des particules étirées qui tombent
    /// autour de la caméra (le shader des particules du jeu, texture « lueur »), avec des éclaboussures au sol ; l'éclair est une lumière
    /// directionnelle froide qui s'allume deux fois très vite puis s'éteint ; le tonnerre suit après le délai du cœur
    /// (<see cref="WeatherState.ThunderDelayTenths"/>). Sons : un fichier « orage », « thunder » ou « tonnerre » (et « pluie », « rain » ou
    /// « averse », joué en boucle par <see cref="StyleAudio"/>) rangé dans <c>Assets/Style/Ambience</c> ; sans eux, un tonnerre et une pluie
    /// de synthèse (faits une seule fois au démarrage). Tout est créé une fois : rien n'est alloué à chaque image. Dans une pièce, ni gouttes
    /// ni éclairs (on entend la pluie sur le toit, et le tonnerre assourdi) ; pendant le souvenir de la Rupture, rien.
    /// </summary>
    public sealed class StyleWeather
    {
        /// <summary>
        /// Menu d'essai (en mode Jeu) : le temps forcé à l'affichage, ou null pour le temps du monde. Ne change rien à la partie (les répliques
        /// des villageois suivent le vrai temps).
        /// </summary>
        public static WeatherKind? Forced { get; set; }

        /// <summary>Menu d'essai : force le temps d'identifiant <paramref name="id"/> (« rain »… voir <see cref="WeatherTrial.KindOf"/>) ; null ou inconnu : le temps du monde.</summary>
        public static void Force(string id) => Forced = id == null ? (WeatherKind?)null : WeatherTrial.KindOf(id);

        private const float DropArea = 34f;

        private readonly GameObject _root;
        private readonly ParticleSystem _drops;
        private readonly ParticleSystem _splashes;
        private readonly Light _flash;
        private readonly AudioSource _thunderSource;
        private readonly AudioClip _thunderClip;

        /// <summary>Relecture de la PR #101 : sons de synthèse faits par cette météo, détruits avec elle (une nouvelle aventure les refait).</summary>
        private readonly System.Collections.Generic.List<AudioClip> _ownedClips = new System.Collections.Generic.List<AudioClip>();
        private readonly AudioSource _rainSource;
        private readonly bool _phone;
        private float _flashAt = -99f, _thunderAt = -1f, _thunderVolume = 1f, _nextForcedFlash;
        private long _flashMinute = long.MinValue;
        private float _shownRain = -1f, _column = -1f, _rateFactor = 1f;

        private StyleWeather()
        {
            _phone = Application.isMobilePlatform;
            // À la racine de la scène (pas sous la caméra) : la pluie et l'éclair gardent leur direction quand la caméra tourne.
            _root = new GameObject("Météo (V2.2)");
            Material template = Resources.Load<Material>(Effects.MaterialName);
            if (template == null)
            {
                Shader shader = Shader.Find("EdS/Particule");
                if (shader != null && shader.isSupported) template = new Material(shader);
            }
            Texture2D drop = UiKit.Texture("EdsInterface/Particules/lueur");
            Texture2D ring = UiKit.Texture("EdsInterface/Particules/anneau");
            if (template != null && drop != null)
            {
                _drops = Particles("Gouttes de pluie", template, drop, ParticleSystemRenderMode.Stretch, _phone ? WeatherCycle.PhoneMaxDrops : WeatherCycle.MaxDrops);
                ParticleSystem.MainModule main = _drops.main;
                main.startLifetime = 1f;   // revue Codex de la PR #101 : réglée d'après la hauteur de la caméra (Column)
                main.startSpeed = new ParticleSystem.MinMaxCurve((float)WeatherCycle.DropMinSpeed, (float)WeatherCycle.DropMaxSpeed);
                main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.04f);
                main.startColor = new Color(0.72f, 0.78f, 0.86f, 0.38f);
                ParticleSystem.ShapeModule shape = _drops.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(DropArea, DropArea, 0.1f);
                // La boîte émet vers son +Z : tournée vers le bas, les gouttes tombent (un peu penchées par le vent).
                _drops.transform.rotation = Quaternion.Euler(90f - (float)WeatherCycle.DropTilt, 20f, 0f);
                var renderer = _drops.GetComponent<ParticleSystemRenderer>();
                renderer.velocityScale = 0.045f;
                renderer.lengthScale = 1f;
            }
            if (template != null && ring != null)
            {
                _splashes = Particles("Éclaboussures", template, ring, ParticleSystemRenderMode.HorizontalBillboard, _phone ? 120 : 400);
                ParticleSystem.MainModule main = _splashes.main;
                main.startLifetime = 0.28f;
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
                main.startColor = new Color(0.8f, 0.85f, 0.92f, 0.35f);
                ParticleSystem.ShapeModule shape = _splashes.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(16f, 16f, 0.01f);
                _splashes.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                ParticleSystem.SizeOverLifetimeModule grow = _splashes.sizeOverLifetime;
                grow.enabled = true;
                grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.3f, 1f, 1f));
            }
            // L'éclair : une lumière froide, haute, sans ombre, éteinte le reste du temps.
            _flash = new GameObject("Éclair").AddComponent<Light>();
            _flash.transform.SetParent(_root.transform, false);
            _flash.transform.rotation = Quaternion.Euler(62f, 140f, 0f);
            _flash.type = LightType.Directional;
            _flash.color = new Color(0.78f, 0.84f, 1f);
            _flash.shadows = LightShadows.None;
            _flash.intensity = 0f;
            _flash.enabled = false;
            // Sons : le fichier du catalogue, sinon la synthèse (une fois).
            StyleCatalog catalog = StyleCatalog.Loaded;
            _thunderClip = catalog != null ? catalog.AmbienceClip(StyleAmbience.Thunder) : null;
            if (_thunderClip == null) _thunderClip = Clip("Tonnerre (synthèse)", WeatherCycle.Thunder(22050, 4.5, 2026), 22050);
            _thunderSource = Source("Tonnerre", false);
            if (catalog == null || catalog.AmbienceClip(StyleAmbience.Rain) == null)
            {
                _rainSource = Source("Pluie (synthèse)", true);
                _rainSource.clip = Clip("Pluie (synthèse)", WeatherCycle.Rain(22050, 6, 1106), 22050);
                _rainSource.volume = 0f;
            }
        }

        /// <summary>La météo de l'aventure (les gouttes suivent la caméra donnée à <see cref="Update"/>).</summary>
        public static StyleWeather Create() => new StyleWeather();

        private ParticleSystem Particles(string name, Material template, Texture2D texture, ParticleSystemRenderMode mode, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            ParticleSystem system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = max;
            main.gravityModifier = 0f;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var material = new Material(template) { name = name };
            material.SetTexture("_MainTex", texture);
            material.SetFloat("_SrcBlend", 5f);    // SrcAlpha
            material.SetFloat("_DstBlend", 10f);   // OneMinusSrcAlpha : gouttes claires, jamais additives
            material.SetFloat("_Intensity", 1f);
            material.SetFloat("_White", 1f);
            material.renderQueue = 3100;
            renderer.sharedMaterial = material;
            system.Play();
            return system;
        }

        private AudioSource Source(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = loop;
            return source;
        }

        private AudioClip Clip(string name, float[] samples, int rate)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            _ownedClips.Add(clip);
            return clip;
        }

        /// <summary>
        /// À chaque image dans l'aventure : gouttes et éclaboussures autour de <paramref name="camera"/> d'après <paramref name="look"/>, éclair
        /// et tonnerre d'après <paramref name="state"/> (un éclair par minute du monde marquée par le cœur, <paramref name="minuteKey"/>), nappe
        /// de pluie de synthèse sans fichier. <paramref name="indoors"/> : dans une pièce ; <paramref name="hushed"/> : souvenir de la Rupture
        /// (rien) ; <paramref name="paused"/> : sons plus bas.
        /// </summary>
        public void Update(WeatherLook look, WeatherState state, long minuteKey, Vector3 camera, bool indoors, bool hushed, bool paused, float deltaTime)
        {
            float rain = hushed ? 0f : (float)look.Rain;
            bool outside = !indoors && !hushed;
            float shown = outside ? rain : 0f;
            // Revue Codex de la PR #101 : la colonne de pluie suit la hauteur de la caméra (de près comme de 25 m de haut, les gouttes
            // touchent le sol) ; relue seulement quand elle change d'un demi-mètre.
            (double height, double life, double factor) = WeatherCycle.RainColumn(camera.y);
            bool columnChanged = Mathf.Abs((float)height - _column) > 0.5f;
            if (columnChanged && _drops != null)
            {
                _column = (float)height;
                _rateFactor = (float)factor;
                ParticleSystem.MainModule main = _drops.main;
                main.startLifetime = (float)life;
            }
            if (Mathf.Abs(shown - _shownRain) > 0.01f || columnChanged)
            {
                _shownRain = shown;
                if (_drops != null)
                {
                    ParticleSystem.EmissionModule e = _drops.emission;
                    e.rateOverTime = shown * _rateFactor * (float)(_phone ? WeatherCycle.PhoneDropRate : WeatherCycle.DropRate);
                }
                if (_splashes != null)
                {
                    ParticleSystem.EmissionModule e = _splashes.emission;
                    e.rateOverTime = shown * (_phone ? 60f : 220f);
                }
            }
            // V2.24 : au-dessus du sol affiché sous la caméra (relief).
            if (_drops != null) _drops.transform.position = StyleWorld.OnGround(new Vector3(camera.x - 2f, _column, camera.z - 2f));
            if (_splashes != null) _splashes.transform.position = StyleWorld.OnGround(new Vector3(camera.x, 0.05f, camera.z));

            // Éclairs : un par minute du monde marquée par le cœur ; en essai forcé, toutes les 12 à 30 secondes.
            float now = Time.time;
            bool storm = !hushed && look.Storm > 0.3;
            if (storm && Forced == WeatherKind.Storm)
            {
                if (now >= _nextForcedFlash)
                {
                    Strike(now, 0.6f + Mathf.Repeat(now * 7.3f, 2.4f), indoors);
                    _nextForcedFlash = now + 12f + Mathf.Repeat(now * 13.7f, 18f);
                }
            }
            else if (!hushed && state.Lightning && minuteKey != _flashMinute)
            {
                _flashMinute = minuteKey;
                Strike(now, state.ThunderDelayTenths / 10f, indoors);
            }
            // La lumière de l'éclair : deux éclats rapides (0 à 0,08 s, puis 0,16 à 0,3 s), rien dans une pièce.
            float age = now - _flashAt;
            float flash = age < 0.08f ? 1f - age / 0.08f : age >= 0.16f && age < 0.3f ? 0.7f * (1f - (age - 0.16f) / 0.14f) : 0f;
            bool lit = flash > 0f && !indoors;
            if (_flash.enabled != lit) _flash.enabled = lit;
            if (lit) _flash.intensity = flash * 3.2f;
            if (_thunderAt > 0f && now >= _thunderAt)
            {
                _thunderAt = -1f;
                StyleCatalog catalog = StyleCatalog.Loaded;
                // Revue Codex de la PR #101 : le volume d'ambiance du catalogue, sans plancher (0 : muet), comme la pluie.
                float volume = (catalog != null ? catalog.AmbienceVolume : 0.8f) * _thunderVolume * (paused ? 0.5f : 1f);
                _thunderSource.pitch = 0.9f + Mathf.Repeat(now * 3.1f, 0.25f);
                if (_thunderClip != null) _thunderSource.PlayOneShot(_thunderClip, volume);
            }

            // Pluie de synthèse (sans fichier « pluie ») : même volume que la nappe du catalogue.
            if (_rainSource != null)
            {
                StyleCatalog catalog = StyleCatalog.Loaded;
                float target = hushed ? 0f : (float)StyleRules.AmbienceVolume(StyleAmbience.Rain, double.NaN, false, false, indoors, 0, rain)
                    * (catalog != null ? catalog.AmbienceVolume : 0.6f) * (paused ? 0.5f : 1f);
                _rainSource.volume = Mathf.MoveTowards(_rainSource.volume, target, deltaTime * 0.4f);
                if (_rainSource.volume > 0.001f && !_rainSource.isPlaying) _rainSource.Play();
                else if (_rainSource.volume <= 0.001f && target <= 0f && _rainSource.isPlaying) _rainSource.Pause();
            }
        }

        private void Strike(float now, float delay, bool indoors)
        {
            _flashAt = now;
            _thunderAt = now + Mathf.Max(0.2f, delay);
            // Le tonnerre lointain est plus doux ; dans une pièce, assourdi.
            _thunderVolume = (delay > 2f ? 0.55f : 1f) * (indoors ? 0.45f : 1f);
        }

        /// <summary>Coupe tout (fin de l'aventure).</summary>
        public void Destroy()
        {
            if (_rainSource != null) _rainSource.Stop();
            _thunderSource.Stop();
            foreach (AudioClip clip in _ownedClips)
                if (clip != null) Object.Destroy(clip);
            _ownedClips.Clear();
            foreach (ParticleSystemRenderer r in _root.GetComponentsInChildren<ParticleSystemRenderer>())
                if (r.sharedMaterial != null) Object.Destroy(r.sharedMaterial);
            Object.Destroy(_root);
        }
    }
}
