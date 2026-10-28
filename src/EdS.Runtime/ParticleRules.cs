namespace EdS.Runtime
{
    /// <summary>
    /// Essai réaliste 8 : réglage d'URP d'un matériau de particules fait pour le rendu intégré de Unity. Surface (0 opaque, 1 transparente),
    /// mélange d'URP (0 alpha, 1 prémultiplié, 2 additif, 3 multiplié), découpe, facteurs de mélange (valeurs de
    /// <c>UnityEngine.Rendering.BlendMode</c> : 0 Zero, 1 One, 2 DstColor, 5 SrcAlpha, 10 OneMinusSrcAlpha), écriture de la profondeur, file
    /// de rendu et mot-clé du mélange (null s'il n'en faut pas).
    /// </summary>
    public readonly struct ParticleBlend
    {
        public ParticleBlend(int surface, int blend, bool alphaClip, int src, int dst, int srcAlpha, int dstAlpha, bool zWrite, int queue, string? keyword)
        {
            Surface = surface;
            Blend = blend;
            AlphaClip = alphaClip;
            Src = src;
            Dst = dst;
            SrcAlpha = srcAlpha;
            DstAlpha = dstAlpha;
            ZWrite = zWrite;
            Queue = queue;
            Keyword = keyword;
        }

        public int Surface { get; }
        public int Blend { get; }
        public bool AlphaClip { get; }
        public int Src { get; }
        public int Dst { get; }
        public int SrcAlpha { get; }
        public int DstAlpha { get; }
        public bool ZWrite { get; }
        public int Queue { get; }
        public string? Keyword { get; }
    }

    /// <summary>
    /// Essai réaliste 8 (demande de Damien du 28 octobre 2026 : effets de combat de Hit Effects FREE et de Magic Effects FREE) : ces packs
    /// sont faits pour le rendu intégré de Unity (shaders « Particles/Standard Unlit » et « Particles/Standard Surface ») et sortiraient
    /// roses avec URP. La préparation passe leurs matériaux sur les shaders de particules d'URP, comme le convertisseur de Unity ; ces règles,
    /// sans Unity, disent comment.
    /// </summary>
    public static class ParticleRules
    {
        private const int Zero = 0, One = 1, DstColor = 2, SrcAlphaFactor = 5, OneMinusSrcAlpha = 10;

        /// <summary>File de rendu des objets opaques, découpés et transparents.</summary>
        public const int OpaqueQueue = 2000, CutoutQueue = 2450, TransparentQueue = 3000;

        /// <summary>
        /// Réglage d'URP pour le mode du rendu intégré <paramref name="mode"/> (« _Mode » : 0 opaque, 1 découpé, 2 fondu, 3 transparent,
        /// 4 additif, 5 soustractif, 6 modulé), comme le convertisseur de Unity : le fondu devient un mélange alpha, le transparent un mélange
        /// prémultiplié, l'additif un mélange additif, le modulé un mélange multiplié. URP n'a pas de mélange soustractif : il devient un
        /// mélange alpha. Un mode inconnu est traité comme un fondu (le cas de tous les matériaux de ces packs).
        /// </summary>
        public static ParticleBlend Blend(double mode)
        {
            switch ((int)System.Math.Round(mode))
            {
                case 0: return new ParticleBlend(0, 0, false, One, Zero, One, Zero, true, OpaqueQueue, null);
                case 1: return new ParticleBlend(0, 0, true, One, Zero, One, Zero, true, CutoutQueue, "_ALPHATEST_ON");
                case 3: return Transparent(1);
                case 4: return Transparent(2);
                case 6: return Transparent(3);
                default: return Transparent(0);
            }
        }

        /// <summary>Surface transparente d'URP avec le mélange <paramref name="blend"/> (0 alpha, 1 prémultiplié, 2 additif, 3 multiplié).</summary>
        public static ParticleBlend Transparent(int blend)
        {
            switch (blend)
            {
                case 1: return new ParticleBlend(1, 1, false, One, OneMinusSrcAlpha, One, OneMinusSrcAlpha, false, TransparentQueue, "_ALPHAPREMULTIPLY_ON");
                case 2: return new ParticleBlend(1, 2, false, SrcAlphaFactor, One, One, One, false, TransparentQueue, null);
                case 3: return new ParticleBlend(1, 3, false, DstColor, Zero, Zero, One, false, TransparentQueue, "_ALPHAMODULATE_ON");
                default: return new ParticleBlend(1, 0, false, SrcAlphaFactor, OneMinusSrcAlpha, One, OneMinusSrcAlpha, false, TransparentQueue, null);
            }
        }

        /// <summary>
        /// Mode du rendu intégré d'un ancien shader de particules (« Legacy Shaders/Particles/Additive », « …/Alpha Blended », « …/Multiply »),
        /// d'après son nom : 4 additif, 3 prémultiplié, 6 modulé, 2 fondu sinon.
        /// </summary>
        public static int LegacyMode(string shaderName)
        {
            string name = StyleRules.Key(shaderName);
            if (name.Contains("additive")) return 4;
            // « Alpha Blended Premultiply » contient aussi « multiply ».
            if (name.Contains("premultiply")) return 3;
            if (name.Contains("multiply")) return 6;
            return 2;
        }

        /// <summary>
        /// Shader de particules du rendu intégré (sortirait rose avec URP) : « Particles/… » (shaders standard) ou les anciens
        /// (« Legacy Shaders/Particles/… », « Mobile/Particles/… »). Les shaders d'URP et les Shader Graph des packs ne comptent pas.
        /// </summary>
        public static bool IsBuiltInParticle(string shaderName)
        {
            string name = shaderName ?? string.Empty;
            return name.StartsWith("Particles/", System.StringComparison.Ordinal) || name.StartsWith("Legacy Shaders/Particles/", System.StringComparison.Ordinal)
                || name.StartsWith("Mobile/Particles/", System.StringComparison.Ordinal);
        }

        /// <summary>Le shader d'URP qui remplace : avec lumière pour « Particles/Standard Surface », sans lumière sinon.</summary>
        public static string UrpShader(string shaderName) =>
            shaderName == "Particles/Standard Surface" ? "Universal Render Pipeline/Particles/Lit" : "Universal Render Pipeline/Particles/Unlit";

        /// <summary>
        /// Mot-clé d'URP du mode de couleur des particules (« _ColorMode » : 0 multiplié, 1 additif, 2 soustractif, 3 incrusté, 4 couleur,
        /// 5 différence), null pour le multiplié ; et le vecteur « _BaseColorAddSubDiff » des modes additif, soustractif et différence.
        /// </summary>
        public static (string? Keyword, float X, float Y) ColorMode(double mode)
        {
            switch ((int)System.Math.Round(mode))
            {
                case 1: return ("_COLORADDSUBDIFF_ON", 1f, 0f);
                case 2: return ("_COLORADDSUBDIFF_ON", -1f, 0f);
                case 3: return ("_COLOROVERLAY_ON", 0f, 0f);
                case 4: return ("_COLORCOLOR_ON", 0f, 0f);
                case 5: return ("_COLORADDSUBDIFF_ON", -1f, 1f);
                default: return (null, 0f, 0f);
            }
        }
    }
}
