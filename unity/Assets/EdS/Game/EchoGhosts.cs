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
    /// <see cref="Dress"/> rend faux et l'arène garde le loup pâle de la V2.1 : un écho ne devient jamais invisible. Affichage seulement.
    /// </summary>
    public sealed class EchoGhosts
    {
        /// <summary>Matériau modèle emporté dans le jeu construit (<c>EdsBuildShaders</c>).</summary>
        public const string MaterialName = "EdsEcho";

        private static Material _template;
        private static bool _templateLoaded;

        // Relecture de la PR #115 : identifiants des propriétés pris une fois.
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve"), SolidityId = Shader.PropertyToID("_Solidity"),
            GlowId = Shader.PropertyToID("_Glow"), SeedId = Shader.PropertyToID("_Seed"), HeightId = Shader.PropertyToID("_Height");

        /// <summary>Écart en deçà duquel le bloc de propriétés n'est pas renvoyé (relecture de la PR #115).</summary>
        private const float Epsilon = 0.004f;

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
            public float SentDissolve = -1f, SentSolidity = -1f, SentGlow = -1f;   // valeurs déjà envoyées (-1 : rien d'envoyé)
            public int Seen;
        }

        private readonly Dictionary<string, Ghost> _ghosts = new Dictionary<string, Ghost>();
        private readonly Dictionary<(Material, EchoKind), Material> _materials = new Dictionary<(Material, EchoKind), Material>();
        private readonly bool _mobile = Application.isMobilePlatform;
        private readonly List<string> _gone = new List<string>();
        private int _frame;

        /// <summary>Le matériau modèle du shader des échos, ou null s'il manque ou n'est pas pris en charge.</summary>
        public static Material Template
        {
            get
            {
                if (_templateLoaded) return _template;
                _templateLoaded = true;
                // Relecture de la PR #115 : le shader n'a qu'un sous-shader pour URP (« FallBack Off ») ; hors d'URP ou non pris en charge, il
                // ne dessinerait rien (ou en rose) : pas de modèle, l'écho garde sa teinte pâle de la V2.1.
                if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null) return _template = null;
                Material loaded = Resources.Load<Material>(MaterialName);
                if (loaded != null && loaded.shader != null && loaded.shader.isSupported) _template = loaded;
                else
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
            var ghost = new Ghost
            {
                Kind = kind,
                Renderers = renderers.ToArray(),
                Block = new MaterialPropertyBlock(),
                Seed = StyleRules.Variant(id, 1000) / 37f,
                Height = Mathf.Max(0.4f, height),
                Last = body.transform.position,
                Ember = Rgb(style.Ember),
                Seen = _frame,
            };
            ghost.Block.SetFloat(SeedId, ghost.Seed);
            ghost.Block.SetFloat(HeightId, ghost.Height);
            ghost.Block.SetFloat(DissolveId, 1f);   // invisible jusqu'à sa formation
            _ghosts[id] = ghost;
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
            // Poils et feuilles découpés : la même découpe (sinon les cartes de poils deviendraient des plaques). Relecture de la PR #115 : un
            // matériau d'URP en fondu (« _Surface » à 1 : poils en transparence) est découpé à 0,3 (le shader des échos n'a pas de fondu
            // propre à la texture) ; un matériau d'un autre shader sans découpe reste plein (limite, UNITY.md).
            bool clipped = source.HasProperty("_AlphaClip") && source.GetFloat("_AlphaClip") > 0.5f && source.HasProperty("_Cutoff");
            bool blended = source.HasProperty("_Surface") && source.GetFloat("_Surface") > 0.5f;
            m.SetFloat("_Cutoff", clipped ? source.GetFloat("_Cutoff") : blended ? 0.3f : 0f);
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
            if (body == null || !_ghosts.TryGetValue(id, out Ghost g)) return;
            g.Seen = _frame;
            if (!body.activeSelf) return;
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
            // Relecture de la PR #115 : le bloc n'est renvoyé que si une valeur a bougé (un écho formé, immobile, de jour : rien par image).
            if (Mathf.Abs(dissolve - g.SentDissolve) > Epsilon || Mathf.Abs(solidity - g.SentSolidity) > Epsilon || Mathf.Abs(glow - g.SentGlow) > Epsilon
                || (dissolve != g.SentDissolve && (dissolve == 0f || dissolve == 1f)))
            {
                g.Block.SetFloat(DissolveId, dissolve);
                g.Block.SetFloat(SolidityId, solidity);
                g.Block.SetFloat(GlowId, glow);
                for (int i = 0; i < g.Renderers.Length; i++)
                    if (g.Renderers[i] != null) g.Renderers[i].SetPropertyBlock(g.Block);
                g.SentDissolve = dissolve;
                g.SentSolidity = solidity;
                g.SentGlow = glow;
            }
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

        /// <summary>
        /// Relecture de la PR #115 : le combattant n'est plus là (pas encore arrivé, parti, retiré) : il rejouera sa formation à son retour.
        /// </summary>
        public void Absent(string id)
        {
            if (id == null || !_ghosts.TryGetValue(id, out Ghost g)) return;
            g.Seen = _frame;
            if (g.Shown < 0f && g.Dead < 0f) return;
            g.Shown = -1f;
            g.Dead = -1f;
            g.Trail = 0f;
            g.Block.SetFloat(DissolveId, 1f);
            for (int i = 0; i < g.Renderers.Length; i++)
                if (g.Renderers[i] != null) g.Renderers[i].SetPropertyBlock(g.Block);
            g.SentDissolve = 1f;
        }

        /// <summary>Début d'une image : les échos seront marqués vus par <see cref="Update"/> ou <see cref="Absent"/>.</summary>
        public void BeginFrame() => _frame++;

        /// <summary>
        /// Relecture de la PR #115 : fin d'une image : les échos que le combat ne montre plus (identité disparue du moteur, corps détruit)
        /// sont oubliés aussitôt (sans allocation tant que rien ne disparaît).
        /// </summary>
        public void EndFrame()
        {
            _gone.Clear();
            foreach (KeyValuePair<string, Ghost> pair in _ghosts)
                if (pair.Value.Seen != _frame) _gone.Add(pair.Key);
            for (int i = 0; i < _gone.Count; i++) _ghosts.Remove(_gone[i]);
        }

        private static Color Rgb((double R, double G, double B) c) => new Color((float)c.R, (float)c.G, (float)c.B);
    }
}
