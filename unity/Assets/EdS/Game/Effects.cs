using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Essai réaliste 7 (demande de Damien du 28 octobre 2026 : « impacts, étincelles et lueurs des compétences, en particules faites dans
    /// Unity ») : les effets visuels des combats, en 3D. Un système de particules par texture (textures CC0 du « Particle Pack » de
    /// Kenney, <c>Resources/EdsInterface/Particules</c>), créé une fois, sans émission continue : chaque effet y lance quelques particules
    /// (<see cref="ParticleSystem.Emit(ParticleSystem.EmitParams, int)"/>), qui vivent moins d'une seconde et demie. Léger pour le
    /// téléphone : douze systèmes en tout, quelques centaines de particules au plus, aucun éclairage. Sans le shader « EdS/Particule »
    /// (projet non préparé), aucun effet : le jeu ne change pas. Affichage seulement : rien ne revient vers le moteur.
    /// </summary>
    public sealed class Effects
    {
        /// <summary>Nom du matériau modèle emporté dans le jeu construit (<c>EdsBuildShaders</c>).</summary>
        public const string MaterialName = "EdsParticule";

        private enum Kind { Spark, Flash, Glow, Ring, Smoke, Dust, Mote, Emblem, Star, Slash, Swirl, Beam }

        private readonly Dictionary<Kind, ParticleSystem> _systems = new Dictionary<Kind, ParticleSystem>();
        private readonly System.Random _random = new System.Random(20261028);

        /// <summary>Or des Éclats (parade, niveau, Charges), turquoise de la garde, rouge du sang, blanc chaud des étincelles.</summary>
        public static readonly Color Gold = new Color(1f, 0.78f, 0.35f), Teal = new Color(0.45f, 0.95f, 1f), Blood = new Color(0.75f, 0.08f, 0.06f),
            Hot = new Color(1f, 0.86f, 0.6f), Heal = new Color(0.45f, 1f, 0.55f), Arcane = new Color(0.72f, 0.55f, 1f);

        private readonly Transform _root;

        private Effects(Transform root, Material template)
        {
            _root = root;
            // Texture, mélange additif ou non, rendu, taille au fil de la vie (null : constante), gravité, plafond de particules.
            Make(root, template, Kind.Spark, "etincelle", true, ParticleSystemRenderMode.Stretch, Shrink(), 1.6f, 240);
            Make(root, template, Kind.Flash, "eclair", true, ParticleSystemRenderMode.Billboard, Grow(0.6f, 1.3f), 0f, 24);
            Make(root, template, Kind.Glow, "lueur", true, ParticleSystemRenderMode.Billboard, Grow(0.8f, 1.2f), 0f, 48);
            Make(root, template, Kind.Ring, "anneau", true, ParticleSystemRenderMode.HorizontalBillboard, Grow(0.2f, 1f), 0f, 24);
            Make(root, template, Kind.Smoke, "fumee", false, ParticleSystemRenderMode.Billboard, Grow(0.6f, 1.6f), -0.05f, 64);
            Make(root, template, Kind.Dust, "poussiere", false, ParticleSystemRenderMode.Billboard, Grow(0.5f, 1.3f), 0.2f, 64);
            Make(root, template, Kind.Mote, "magie", true, ParticleSystemRenderMode.Billboard, Shrink(), -0.25f, 160);
            Make(root, template, Kind.Emblem, "embleme", true, ParticleSystemRenderMode.Billboard, Grow(0.5f, 1.4f), 0f, 8);
            Make(root, template, Kind.Star, "etoile", true, ParticleSystemRenderMode.Billboard, Shrink(), 0.3f, 96);
            Make(root, template, Kind.Slash, "taille", true, ParticleSystemRenderMode.Billboard, Grow(0.85f, 1.15f), 0f, 16);
            Make(root, template, Kind.Swirl, "tourbillon", true, ParticleSystemRenderMode.HorizontalBillboard, Grow(0.4f, 1.1f), 0f, 16);
            Make(root, template, Kind.Beam, "rayon", true, ParticleSystemRenderMode.VerticalBillboard, Grow(0.8f, 1f), 0f, 8);
        }

        /// <summary>Les effets du jeu, ou null si le shader des particules manque (rien ne s'affiche alors).</summary>
        public static Effects Create()
        {
            Material template = Resources.Load<Material>(MaterialName);
            if (template == null)
            {
                Shader shader = Shader.Find("EdS/Particule");
                if (shader == null || !shader.isSupported) return null;
                template = new Material(shader);
            }
            var root = new GameObject("Effets (particules)");
            return new Effects(root.transform, template);
        }

        // --- Effets ---------------------------------------------------------------------------------------------------------------
        // Chaque effet part des pieds du combattant (<paramref name="feet"/>, sur le tablier du pont s'il y est) ; la poitrine est aux deux
        // tiers de sa hauteur (<paramref name="height"/> : 1,8 m pour le chevalier, moins d'un mètre pour un loup).

        private static Vector3 Chest(Vector3 feet, float height) => feet + Vector3.up * Mathf.Max(0.5f, height * 0.65f);

        private static Vector3 Floor(Vector3 feet) => feet + Vector3.up * 0.06f;

        /// <summary>
        /// Essai réaliste 8 (demande de Damien du 28 octobre 2026 : effets de combat des packs gratuits) : l'effet <paramref name="kind"/>
        /// d'un pack (catalogue), posé en <paramref name="at"/>, tourné de <paramref name="yaw"/> degrés, à l'échelle
        /// <paramref name="scale"/> (particules comprises), éteint après sa durée (<see cref="PackEffect"/>). Sur ordinateur seulement ; false sans lui (les particules
        /// du jeu restent).
        /// </summary>
        private bool Pack(StyleVfx kind, Vector3 at, float yaw = 0f, float scale = 1f)
        {
            if (Application.isMobilePlatform) return false;
            StyleCatalog catalog = StyleCatalog.Loaded;
            VfxLook look = catalog != null ? catalog.Effect(kind) : null;
            if (look == null) return false;
            GameObject fx = Object.Instantiate(look.Prefab, at, Quaternion.Euler(0f, yaw + look.Yaw, 0f), _root);
            float k = look.Scale * scale;
            if (Mathf.Abs(k - 1f) > 0.01f)
            {
                fx.transform.localScale = fx.transform.localScale * k;
                foreach (ParticleSystem system in fx.GetComponentsInChildren<ParticleSystem>())
                {
                    ParticleSystem.MainModule main = system.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
            }
            // Relecture indépendante : les auras en boucle cessent d'émettre à la fin de leur durée et s'éteignent, sans disparaître d'un coup.
            fx.AddComponent<PackEffect>().Begin(look.Life);
            return true;
        }

        /// <summary>
        /// Essai réaliste 8 : effet continu d'un pack (flammes et fumée du foyer de la forge, Particle Pack de Unity) posé en
        /// <paramref name="at"/> sous <paramref name="parent"/>, à l'échelle du catalogue ; il vit avec son parent. Sur ordinateur
        /// seulement ; null sans lui.
        /// </summary>
        public static GameObject Ambient(StyleVfx kind, Transform parent, Vector3 at)
        {
            if (Application.isMobilePlatform) return null;
            StyleCatalog catalog = StyleCatalog.Loaded;
            VfxLook look = catalog != null ? catalog.Effect(kind) : null;
            if (look == null) return null;
            GameObject fx = Object.Instantiate(look.Prefab, at, Quaternion.Euler(0f, look.Yaw, 0f), parent);
            if (Mathf.Abs(look.Scale - 1f) > 0.01f)
            {
                fx.transform.localScale = fx.transform.localScale * look.Scale;
                foreach (ParticleSystem system in fx.GetComponentsInChildren<ParticleSystem>())
                {
                    ParticleSystem.MainModule main = system.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
            }
            // V1.10 (essai 10 : « tache blanche du feu ») : le halo et l'air qui ondule du pack (une grande lueur additive et un disque de
            // distorsion, faits pour l'ancien rendu) saturent en blanc sous le halo (bloom) et l'exposition d'URP ; la lumière du foyer
            // éclaire déjà. Ils ne sont plus dessinés : restent les flammes, les braises et les étincelles.
            foreach (ParticleSystemRenderer r in fx.GetComponentsInChildren<ParticleSystemRenderer>())
            {
                Material m = r.sharedMaterial;
                if (ParticleRules.AmbientHalo(r.gameObject.name, m != null ? m.name : null)) r.enabled = false;
            }
            return fx;
        }

        /// <summary>
        /// V1.10 (essai 10 : « fumée noire de la forge ») : fumée continue faite par le jeu, à la place de celle du Particle Pack. Celle du
        /// pack est une fumée éclairée (« Particles/Standard Surface », passée sur « Particles/Lit » d'URP) : elle ne prend que la lumière qui
        /// la touche, et au-dessus du foyer (à l'ombre du toit, la lueur orange par dessous) elle sortait presque noire. Celle-ci n'est pas
        /// éclairée : gris clair, mélange alpha (shader « EdS/Particule », texture « fumee »), qui monte, grossit et s'efface. Null sans le
        /// shader ou la texture (pas de fumée).
        /// </summary>
        public static GameObject Smoke(Transform parent, Vector3 at)
        {
            Material material = SmokeMaterial();
            if (material == null) return null;
            var go = new GameObject("Fumée de la forge");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            ParticleSystem system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Application.isMobilePlatform ? 16 : 40;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.76f, 0.74f, 0.42f), new Color(0.88f, 0.86f, 0.84f, 0.3f));
            main.gravityModifier = -0.02f;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = Application.isMobilePlatform ? 3f : 7f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.2f;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // le cône d'émission vers le haut
            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
            ParticleSystem.SizeOverLifetimeModule grow = system.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, Grow(0.7f, 3.2f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = material;
            system.Play();
            return go;
        }

        /// <summary>
        /// Relecture de la PR #97 : matériau de la fumée des forges, fait une fois et partagé par toutes (jamais une copie par forge, qui
        /// n'était pas détruite avec elle) ; null sans le shader ou la texture.
        /// </summary>
        private static Material SmokeMaterial()
        {
            if (_smoke != null) return _smoke;
            Material template = Resources.Load<Material>(MaterialName);
            if (template == null)
            {
                Shader shader = Shader.Find("EdS/Particule");
                if (shader == null || !shader.isSupported) return null;
                template = new Material(shader);
            }
            Texture2D image = UiKit.Texture("EdsInterface/Particules/fumee");
            if (image == null) return null;
            var material = new Material(template) { name = "Fumée de la forge" };
            material.SetTexture("_MainTex", image);
            material.SetFloat("_SrcBlend", 5f);    // SrcAlpha
            material.SetFloat("_DstBlend", 10f);   // OneMinusSrcAlpha : fumée claire, jamais additive ni multipliée
            material.SetFloat("_Intensity", 1f);
            material.SetFloat("_White", 1f);       // fondu sans bords sombres (comme la fumée des combats)
            material.renderQueue = 3100;
            _smoke = material;
            return _smoke;
        }

        private static Material _smoke;

        /// <summary>Coup reçu : gerbe d'étincelles, éclair, gouttes sombres ; <paramref name="strength"/> de 0 à 1 (part de vie perdue).</summary>
        public void Hit(Vector3 feet, float height, float strength, bool critical)
        {
            Vector3 at = Chest(feet, height);
            float k = Mathf.Clamp01(strength);
            // Essai réaliste 8 : l'impact du pack s'il y est (un peu de sang du jeu en plus).
            if (Pack(critical ? StyleVfx.Crit : StyleVfx.Hit, at, 0f, 0.8f + 0.4f * k))
            {
                Burst(Kind.Star, at, 3 + (int)(5 * k), 0.8f, 2.2f, 0.06f, 0.14f, 0.3f, 0.6f, Blood, 0.6f);
                return;
            }
            Burst(Kind.Spark, at, 8 + (int)(14 * k), 3.5f, 7.5f, 0.05f, 0.09f, 0.22f, 0.42f, Hot, 1.2f);
            Burst(Kind.Flash, at, 1, 0f, 0f, 0.9f + 0.8f * k, 1.3f + k, 0.1f, 0.14f, critical ? Gold : Hot, 0f);
            Burst(Kind.Star, at, 3 + (int)(5 * k), 0.8f, 2.2f, 0.06f, 0.14f, 0.3f, 0.6f, Blood, 0.6f);
            if (critical) Burst(Kind.Ring, Floor(feet), 1, 0f, 0f, 2.2f, 2.6f, 0.3f, 0.3f, Gold, 0f);
        }

        /// <summary>Coup gardé : étincelles froides qui jaillissent du bouclier, petit éclair turquoise.</summary>
        public void Blocked(Vector3 feet, float height)
        {
            Vector3 at = Chest(feet, height);
            if (Pack(StyleVfx.Block, at, 0f, 0.7f)) return;
            Burst(Kind.Spark, at, 14, 4f, 8f, 0.05f, 0.08f, 0.2f, 0.36f, Teal, 1.5f);
            Burst(Kind.Flash, at, 1, 0f, 0f, 1.1f, 1.4f, 0.1f, 0.12f, Teal, 0f);
        }

        /// <summary>Parade parfaite : éclair d'or, emblème des Éclats, onde au sol, étoiles.</summary>
        public void Parry(Vector3 feet, float height)
        {
            Vector3 at = Chest(feet, height);
            // Essai réaliste 8 : l'éclat sacré du pack, et toujours l'emblème des Éclats.
            if (Pack(StyleVfx.Parry, at))
            {
                Burst(Kind.Emblem, at, 1, 0f, 0f, 1.6f, 1.6f, 0.45f, 0.45f, Gold, 0f);
                return;
            }
            Burst(Kind.Flash, at, 1, 0f, 0f, 2.4f, 2.8f, 0.16f, 0.16f, Gold, 0f);
            Burst(Kind.Emblem, at, 1, 0f, 0f, 1.6f, 1.6f, 0.45f, 0.45f, Gold, 0f);
            Burst(Kind.Ring, Floor(feet), 1, 0f, 0f, 3.6f, 3.6f, 0.45f, 0.45f, Gold, 0f);
            Burst(Kind.Spark, at, 22, 4f, 9f, 0.05f, 0.09f, 0.25f, 0.45f, Gold, 1.2f);
            Burst(Kind.Star, at, 8, 1f, 2.6f, 0.1f, 0.22f, 0.4f, 0.8f, Gold, 0.2f);
        }

        /// <summary>Garde brisée : éclats rouges et fumée.</summary>
        public void GuardBroken(Vector3 feet, float height)
        {
            Vector3 at = Chest(feet, height);
            if (Pack(StyleVfx.GuardBroken, at, 0f, 0.6f)) return;
            Burst(Kind.Spark, at, 18, 3f, 7f, 0.06f, 0.1f, 0.25f, 0.45f, new Color(1f, 0.35f, 0.25f), 1.4f);
            Burst(Kind.Smoke, at, 4, 0.3f, 0.9f, 0.6f, 1f, 0.6f, 0.9f, new Color(0.25f, 0.22f, 0.22f, 0.7f), 0f);
        }

        /// <summary>Taille d'épée : arc de lumière devant le corps (tourné vers <paramref name="yaw"/>), quelques étincelles.</summary>
        public void Slash(Vector3 feet, float height, float yaw, Color color)
        {
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 front = Chest(feet, height) + forward * 0.9f;
            if (Pack(StyleVfx.Slash, Chest(feet, height) + forward * 0.4f, yaw)) return;
            EmitOne(Kind.Slash, front, Vector3.zero, 1.9f, 0.2f, color, (float)(_random.NextDouble() * 60.0 - 30.0));
            Burst(Kind.Spark, front, 5, 1.5f, 3.5f, 0.04f, 0.07f, 0.15f, 0.25f, color, 0.8f);
        }

        /// <summary>Sort ou soutien lancé : cercle tournant au sol, lueur, éclats qui montent.</summary>
        public void Cast(Vector3 feet, Color color)
        {
            Vector3 ground = Floor(feet);
            if (Pack(StyleVfx.Cast, ground)) return;
            EmitOne(Kind.Swirl, ground, Vector3.zero, 2.6f, 0.7f, color, (float)(_random.NextDouble() * 360.0));
            Burst(Kind.Ring, ground, 1, 0f, 0f, 3f, 3f, 0.5f, 0.5f, color, 0f);
            Burst(Kind.Glow, feet + Vector3.up * 1f, 1, 0f, 0f, 2.2f, 2.4f, 0.35f, 0.35f, color, 0f);
            Rise(Kind.Mote, feet, 16, 0.8f, 0.12f, 0.24f, 0.7f, 1.1f, color);
        }

        /// <summary>Niveau gagné : colonne de lumière, onde dorée, étoiles qui montent.</summary>
        public void LevelUp(Vector3 feet)
        {
            if (Pack(StyleVfx.LevelUp, feet)) return;
            EmitOne(Kind.Beam, feet + Vector3.up * 2.2f, Vector3.zero, 5f, 1.3f, Gold, 0f);
            Burst(Kind.Ring, Floor(feet), 1, 0f, 0f, 5f, 5f, 0.8f, 0.8f, Gold, 0f);
            Rise(Kind.Star, feet, 18, 0.9f, 0.12f, 0.26f, 0.9f, 1.4f, Gold);
            Rise(Kind.Mote, feet, 20, 0.7f, 0.1f, 0.2f, 0.8f, 1.2f, Hot);
        }

        /// <summary>Charge de Rempart gagnée : éclat d'or bref sur le bouclier.</summary>
        public void Charge(Vector3 feet, float height)
        {
            Vector3 at = Chest(feet, height);
            if (Pack(StyleVfx.Charge, at, 0f, 0.5f)) return;
            Burst(Kind.Star, at, 1, 0f, 0f, 0.7f, 0.7f, 0.35f, 0.35f, Gold, 0f);
            Burst(Kind.Spark, at, 6, 1.5f, 3f, 0.04f, 0.06f, 0.2f, 0.3f, Gold, 0.4f);
        }

        /// <summary>Potion bue : éclats verts qui montent.</summary>
        public void Healed(Vector3 feet)
        {
            if (Pack(StyleVfx.Heal, feet)) return;
            Rise(Kind.Mote, feet, 18, 0.6f, 0.1f, 0.2f, 0.8f, 1.2f, Heal);
        }

        /// <summary>Mort : nuage de poussière au sol.</summary>
        public void Death(Vector3 feet)
        {
            Vector3 ground = feet + Vector3.up * 0.2f;
            if (Pack(StyleVfx.Death, feet)) return;
            Burst(Kind.Dust, ground, 8, 0.6f, 1.6f, 0.5f, 1f, 0.7f, 1.1f, new Color(0.55f, 0.48f, 0.38f, 0.75f), 0f);
            Burst(Kind.Smoke, ground, 4, 0.3f, 0.8f, 0.8f, 1.4f, 0.9f, 1.3f, new Color(0.6f, 0.58f, 0.55f, 0.5f), 0f);
        }

        /// <summary>
        /// Sous-lot V2.9.1 : un écho qui se forme : lueurs de sa couleur qui montent autour de lui, un éclat à la poitrine.
        /// </summary>
        public void EchoForm(Vector3 feet, float height, Color color)
        {
            Rise(Kind.Mote, feet, Application.isMobilePlatform ? 8 : 16, 0.7f, 0.06f, 0.14f, 0.7f, 1.3f, color);
            Burst(Kind.Glow, Chest(feet, height), 1, 0f, 0f, 1.2f, 1.6f, 0.5f, 0.6f, new Color(color.r, color.g, color.b, 0.6f), 0f);
        }

        /// <summary>
        /// Sous-lot V2.9.1 : un écho qui meurt se défait en braises : étincelles de sa couleur lancées vers le haut, lueurs qui montent, un
        /// éclair pâle (au lieu de la poussière d'une bête de chair).
        /// </summary>
        public void EchoEmbers(Vector3 feet, float height, Color color)
        {
            bool phone = Application.isMobilePlatform;
            Burst(Kind.Spark, Chest(feet, height), phone ? 8 : 18, 0.6f, 2.2f, 0.05f, 0.12f, 0.6f, 1.2f, color, 1f);
            Rise(Kind.Mote, feet, phone ? 10 : 24, 0.6f, 0.05f, 0.12f, 1f, 1.8f, color);
            Burst(Kind.Flash, Chest(feet, height), 1, 0f, 0f, 1.4f, 1.8f, 0.3f, 0.35f, new Color(color.r, color.g, color.b, 0.7f), 0f);
        }

        /// <summary>Sous-lot V2.9.1 : une lueur de la traînée d'un écho, qui monte doucement et s'éteint.</summary>
        public void Wisp(Vector3 at, Color color)
        {
            Vector3 jitter = new Vector3(Range(-0.25f, 0.25f), Range(-0.2f, 0.2f), Range(-0.25f, 0.25f));
            EmitOne(Kind.Mote, at + jitter, new Vector3(0f, Range(0.2f, 0.5f), 0f), Range(0.05f, 0.1f), Range(0.6f, 1f),
                new Color(color.r, color.g, color.b, 0.7f), Range(0f, 360f));
        }

        /// <summary>
        /// Sous-lot V2.9.1 : une lueur aspirée par une faille : née à <paramref name="radius"/> mètres de <paramref name="center"/>, elle y
        /// arrive en fin de vie.
        /// </summary>
        public void Pull(Vector3 center, float radius, Color color)
        {
            Vector3 from = center + RandomDirection() * radius;
            if (from.y < 0.1f) from.y = 0.1f + Range(0f, 0.6f);
            float life = Range(0.9f, 1.5f);
            EmitOne(Kind.Glow, from, (center - from) / life, Range(0.08f, 0.18f), life, color, Range(0f, 360f));
        }

        /// <summary>Frappe au sol d'un adversaire (cri et onde du Gardien) : onde, poussière ; <paramref name="radius"/> en mètres.</summary>
        public void Slam(Vector3 feet, float radius)
        {
            Vector3 ground = Floor(feet);
            if (Pack(StyleVfx.Slam, ground, 0f, Mathf.Clamp(radius / 2.5f, 0.6f, 2.5f))) return;
            Burst(Kind.Ring, ground, 1, 0f, 0f, radius * 2f, radius * 2f, 0.4f, 0.4f, Hot, 0f);
            Burst(Kind.Dust, ground, 10, 1f, 3f, 0.4f, 0.9f, 0.6f, 1f, new Color(0.55f, 0.48f, 0.38f, 0.7f), 0f);
        }

        // --- Particules -----------------------------------------------------------------------------------------------------------

        private void Make(Transform root, Material template, Kind kind, string texture, bool additive, ParticleSystemRenderMode mode,
            AnimationCurve size, float gravity, int max)
        {
            Texture2D image = UiKit.Texture("EdsInterface/Particules/" + texture);
            if (image == null) return;
            var go = new GameObject("Effet " + texture);
            go.transform.SetParent(root, false);
            ParticleSystem system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            // Relecture de l'essai 7 : un système en boucle hors de l'écran est mis en pause par défaut ; ceux-ci sont posés à l'origine et
            // émettent ailleurs (coordonnées du monde) : toujours simulés, pour que leurs particules vivent où qu'elles soient.
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.gravityModifier = gravity;
            main.startSpeed = 0f;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            // Fondu : la particule paraît vite, brille, puis s'efface.
            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.85f, 0.55f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
            if (size != null)
            {
                ParticleSystem.SizeOverLifetimeModule grow = system.sizeOverLifetime;
                grow.enabled = true;
                grow.size = new ParticleSystem.MinMaxCurve(1f, size);
            }
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            if (mode == ParticleSystemRenderMode.Stretch)
            {
                renderer.velocityScale = 0.05f;
                renderer.lengthScale = 1.5f;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var material = new Material(template);
            material.SetTexture("_MainTex", image);
            material.SetFloat("_SrcBlend", 5f);                       // SrcAlpha
            material.SetFloat("_DstBlend", additive ? 1f : 10f);      // One (additif) ou OneMinusSrcAlpha
            material.SetFloat("_Intensity", additive ? 1.6f : 1f);
            material.SetFloat("_White", additive ? 0f : 1f);   // relecture de l'essai 7 : fondu sans bords sombres
            material.renderQueue = 3100;
            renderer.sharedMaterial = material;
            system.Play();
            _systems[kind] = system;
        }

        /// <summary>Gerbe : <paramref name="count"/> particules lancées dans toutes les directions (vers le haut surtout).</summary>
        private void Burst(Kind kind, Vector3 at, int count, float minSpeed, float maxSpeed, float minSize, float maxSize, float minLife, float maxLife,
            Color color, float upward)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = RandomDirection();
                direction.y = Mathf.Abs(direction.y) * upward + direction.y * (1f - Mathf.Min(1f, upward));
                Vector3 velocity = direction.normalized * Range(minSpeed, maxSpeed);
                EmitOne(kind, at, velocity, Range(minSize, maxSize), Range(minLife, maxLife), color, Range(0f, 360f));
            }
        }

        /// <summary>Particules qui montent doucement autour des pieds, dans un cercle de <paramref name="radius"/> mètres.</summary>
        private void Rise(Kind kind, Vector3 feet, int count, float radius, float minSize, float maxSize, float minLife, float maxLife, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Range(0f, Mathf.PI * 2f), r = radius * Mathf.Sqrt(Range(0f, 1f));
                Vector3 at = feet + new Vector3(Mathf.Cos(a) * r, Range(0.05f, 1.2f), Mathf.Sin(a) * r);
                EmitOne(kind, at, new Vector3(0f, Range(0.6f, 1.6f), 0f), Range(minSize, maxSize), Range(minLife, maxLife), color, Range(0f, 360f));
            }
        }

        private void EmitOne(Kind kind, Vector3 at, Vector3 velocity, float size, float life, Color color, float rotation)
        {
            if (!_systems.TryGetValue(kind, out ParticleSystem system) || system == null) return;
            var p = new ParticleSystem.EmitParams
            {
                position = at,
                velocity = velocity,
                startSize = size,
                startLifetime = life,
                startColor = color,
                rotation = rotation,
                applyShapeToPosition = false,
            };
            system.Emit(p, 1);
        }

        private Vector3 RandomDirection()
        {
            float z = Range(-1f, 1f), a = Range(0f, Mathf.PI * 2f), r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            return new Vector3(r * Mathf.Cos(a), z, r * Mathf.Sin(a));
        }

        private float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);

        private static AnimationCurve Shrink() => new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.15f));

        private static AnimationCurve Grow(float from, float to) => new AnimationCurve(new Keyframe(0f, from), new Keyframe(1f, to));
    }
}
