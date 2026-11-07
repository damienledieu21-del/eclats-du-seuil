using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.9.1 (demande de Damien du 7 novembre 2026 : « échos fantômes ») : le rendu spectral des échos (<see cref="EchoLooks.Of"/> :
    /// bêtes revenues, échos de la nuit et des failles, loup blanc, Écho de la Rupture). Chaque matériau du modèle passe sur le shader
    /// « EdS/Echo » (une copie par matériau d'origine et par sorte d'écho, partagée par tous les échos) ; la dissolution, la solidité et
    /// l'éclat de chaque écho passent par un bloc de propriétés (aucune copie par écho). L'écho se forme en braises à son apparition, se défait
    /// en braises à sa mort, laisse quelques lueurs derrière lui quand il bouge. Sans le shader (absent, non pris en charge, rendu sans URP),
    /// <see cref="Dress"/> rend faux et l'arène garde le loup pâle de la V2.1. Affichage seulement.
    /// </summary>
    public sealed class EchoGhosts
    {
        /// <summary>Matériau modèle emporté dans le jeu construit (<c>EdsBuildShaders</c>).</summary>
        public const string MaterialName = "EdsEcho";

        private static Material _template;
        private static bool _templateLoaded;

        private sealed class Ghost
        {
            public EchoKind Kind;
            public Renderer[] Renderers;
            public MaterialPropertyBlock Block;
            public float Seed;
            public float Height;
            public float Shown = -1f;   // secondes depuis la première apparition (-1 : jamais vu)
            public float Dead = -1f;    // secondes depuis la mort (-1 : vivant)
            public Vector3 Last;
            public float Trail;
            public Color Ember;
        }

        private readonly Dictionary<string, Ghost> _ghosts = new Dictionary<string, Ghost>();
        private readonly Dictionary<(Material, EchoKind), Material> _materials = new Dictionary<(Material, EchoKind), Material>();
        private readonly bool _mobile = Application.isMobilePlatform;

        /// <summary>Le matériau modèle du shader des échos, ou null s'il manque ou n'est pas pris en charge.</summary>
        public static Material Template
        {
            get
            {
                if (_templateLoaded) return _template;
                _templateLoaded = true;
                _template = Resources.Load<Material>(MaterialName);
                if (_template == null)
                {
                    Shader shader = Shader.Find("EdS/Echo");
                    if (shader != null && shader.isSupported) _template = new Material(shader);
                }
                return _template;
            }
        }

        /// <summary>Oublie tous les échos et détruit leurs matériaux (nouveau combat).</summary>
        public void Clear()
        {
            foreach (Material m in _materials.Values)
                if (m != null) Object.Destroy(m);
            _materials.Clear();
            _ghosts.Clear();
        }

        /// <summary>Le combattant <paramref name="id"/> est-il habillé en écho fantôme ?</summary>
        public bool Has(string id) => id != null && _ghosts.ContainsKey(id);

        /// <summary>
        /// Habille <paramref name="body"/> (tous ses rendus, modèle compris) en écho de sorte <paramref name="kind"/> ; faux sans le shader
        /// (rien n'est changé). <paramref name="height"/> : hauteur du corps (m), pour que la dissolution parte de la tête.
        /// </summary>
        public bool Dress(string id, GameObject body, EchoKind kind, float height)
        {
            Material template = Template;
            if (template == null || kind == EchoKind.None || body == null) return false;
            EchoStyle style = EchoLooks.Style(kind);
            var renderers = new List<Renderer>();
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer) continue;
                Material[] materials = renderer.sharedMaterials;   // les matériaux d'origine ne sont jamais modifiés
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] != null) materials[i] = GhostOf(materials[i], kind, style, template);
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // un écho n'a pas d'ombre
                renderer.receiveShadows = false;
                renderers.Add(renderer);
            }
            _ghosts[id] = new Ghost
            {
                Kind = kind,
                Renderers = renderers.ToArray(),
                Block = new MaterialPropertyBlock(),
                Seed = StyleRules.Variant(id, 1000) / 37f,
                Height = Mathf.Max(0.4f, height),
                Last = body.transform.position,
                Ember = Rgb(style.Ember),
            };
            return true;
        }

        private Material GhostOf(Material source, EchoKind kind, EchoStyle style, Material template)
        {
            if (_materials.TryGetValue((source, kind), out Material shared) && shared != null) return shared;
            var m = new Material(template) { name = source.name + " (écho)" };
            string map = source.HasProperty("_BaseMap") ? "_BaseMap" : source.HasProperty("_MainTex") ? "_MainTex" : null;
            Texture texture = map != null ? source.GetTexture(map) : null;
            if (texture != null)
            {
                m.SetTexture("_BaseMap", texture);
                m.SetTextureScale("_BaseMap", source.GetTextureScale(map));
                m.SetTextureOffset("_BaseMap", source.GetTextureOffset(map));
            }
            // Poils et feuilles découpés : la même découpe (sinon les cartes de poils deviendraient des plaques).
            bool clipped = source.HasProperty("_AlphaClip") && source.GetFloat("_AlphaClip") > 0.5f && source.HasProperty("_Cutoff");
            m.SetFloat("_Cutoff", clipped ? source.GetFloat("_Cutoff") : 0f);
            m.SetColor("_Tint", Rgb(style.Body));
            m.SetColor("_RimColor", Rgb(style.Rim));
            m.SetColor("_EdgeColor", Rgb(style.Ember) * 1.6f);
            m.SetFloat("_RimPower", (float)style.RimPower);
            m.SetFloat("_Flicker", (float)style.Flicker);
            m.renderQueue = 3000;
            _materials[(source, kind)] = m;
            return m;
        }

        /// <summary>
        /// À chaque image, pour un écho habillé : dissolution (apparition, mort), solidité (sorte, vie, nuit, palier de la cicatrice sous lui),
        /// éclat, braises et traînée. <paramref name="night"/> : de 0 (jour) à 1 (nuit) ; <paramref name="deltaTime"/> : 0 en pause.
        /// </summary>
        public void Update(string id, GameObject body, bool dead, double health, int scarStage, float night, Effects effects, float deltaTime)
        {
            if (body == null || !_ghosts.TryGetValue(id, out Ghost g) || !body.activeSelf) return;
            Vector3 feet = body.transform.position - Vector3.up * ArenaView.FeetOffset;
            if (g.Shown < 0f)
            {
                g.Shown = 0f;
                g.Last = body.transform.position;
                if (!dead) effects?.EchoForm(feet, g.Height, g.Ember);
            }
            else g.Shown += deltaTime;
            if (dead && g.Dead < 0f)
            {
                g.Dead = 0f;
                effects?.EchoEmbers(feet, g.Height, g.Ember);
            }
            else if (dead) g.Dead += deltaTime;
            else g.Dead = -1f;
            float dissolve = (float)EchoLooks.Dissolve(g.Shown, g.Dead >= 0f ? g.Dead : (double?)null);
            float solidity = (float)EchoLooks.Solidity(g.Kind, health, scarStage, night);
            float glow = (float)EchoLooks.Glow(scarStage, night);
            g.Block.SetFloat("_Dissolve", dissolve);
            g.Block.SetFloat("_Solidity", solidity);
            g.Block.SetFloat("_Glow", glow);
            g.Block.SetFloat("_Seed", g.Seed);
            g.Block.SetFloat("_Height", g.Height);
            for (int i = 0; i < g.Renderers.Length; i++)
                if (g.Renderers[i] != null) g.Renderers[i].SetPropertyBlock(g.Block);
            // Traînée : quelques lueurs là où il passe, seulement quand il bouge (discret en combat).
            Vector3 at = body.transform.position;
            float moved = (at - g.Last).magnitude;
            g.Last = at;
            if (dead || effects == null || deltaTime <= 0f || moved < 0.002f) return;
            g.Trail += deltaTime * (float)EchoLooks.TrailRate(g.Kind, night, _mobile);
            while (g.Trail >= 1f)
            {
                g.Trail -= 1f;
                effects.Wisp(feet + Vector3.up * (g.Height * 0.5f), g.Ember);
            }
        }

        private static Color Rgb((double R, double G, double B) c) => new Color((float)c.R, (float)c.G, (float)c.B);
    }
}
