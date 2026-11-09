using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EdS.Runtime
{
    /// <summary>Cartes d'une pièce d'un modèle de Poly Haven à plusieurs matières (couleur, relief, rugosité, métal ; null : absente).</summary>
    public sealed class StylePieceMaps
    {
        public StylePieceMaps(string piece, string? color, string? normal, string? rough, string? metal)
        {
            Piece = piece;
            Color = color;
            Normal = normal;
            Rough = rough;
            Metal = metal;
        }

        /// <summary>Nom réduit de la pièce (« barrel01 », « pieces »).</summary>
        public string Piece { get; }

        public string? Color { get; }

        public string? Normal { get; }

        public string? Rough { get; }

        public string? Metal { get; }
    }

    /// <summary>
    /// Sous-lot V2.12.6 (essai du 9 novembre 2026 : « objets tout blancs à la forge de Maëlle ») : un modèle de Poly Haven fait de plusieurs
    /// pièces (« wooden_barrels_01 » : deux tonneaux et un lot de douelles, cercles…) livre une texture par pièce, nommée
    /// « &lt;modèle&gt;_&lt;pièce&gt;_diff|nor_gl|rough|metal_&lt;résolution&gt; ». Chaque matière intégrée du modèle prend les cartes de la pièce à son
    /// nom (« barrel01 », « wooden_barrels_01_barrel01 », « Barrel_01.001 ») ; les matières restées sans pièce prennent, dans l'ordre ordinal,
    /// les pièces restées sans matière quand il y en a autant. Sans Unity, testé.
    /// </summary>
    public static class PieceTextures
    {
        private static readonly HashSet<string> MapWords = new HashSet<string>(StringComparer.Ordinal)
        {
            "diff", "diffuse", "albedo", "basecolor", "color", "col", "nor", "normal", "nrm", "nm", "gl", "dx", "rough", "roughness", "metal",
            "metallic", "arm", "ao", "disp", "displacement", "height", "spec", "specular", "gloss", "alpha", "opacity", "mask",
        };

        /// <summary>Nom réduit du modèle, sans extension ni résolution (« wooden_barrels_01_1k.fbx » → « woodenbarrels01 »).</summary>
        public static string ModelKey(string modelPath)
        {
            List<string> words = Words(modelPath);
            if (words.Count > 1) words.RemoveAt(words.Count - 1);   // extension
            return StyleRules.Key(string.Concat(words.Where(w => !IsResolution(w))));
        }

        /// <summary>
        /// Pièce d'une texture (« wooden_barrels_01_barrel01_diff_1k.png » → « barrel01 ») : ses mots sans le genre de carte, la résolution
        /// ni l'extension, moins le nom du modèle ; null si la texture n'est pas à ce modèle ou n'a pas de pièce (texture du modèle entier).
        /// </summary>
        public static string? PieceOf(string modelKey, string texture)
        {
            if (string.IsNullOrEmpty(modelKey) || string.IsNullOrEmpty(texture)) return null;
            List<string> words = Words(texture);
            if (words.Count > 1) words.RemoveAt(words.Count - 1);
            string stem = StyleRules.Key(string.Concat(words.Where(w => !MapWords.Contains(w) && !IsResolution(w))));
            if (!stem.StartsWith(modelKey, StringComparison.Ordinal) || stem.Length == modelKey.Length) return null;
            return stem.Substring(modelKey.Length);
        }

        /// <summary>Pièces du modèle trouvées parmi <paramref name="textures"/> (au moins une carte de couleur), dans l'ordre ordinal.</summary>
        public static IReadOnlyList<string> Pieces(string modelPath, IEnumerable<string> textures)
        {
            string model = ModelKey(modelPath);
            return (textures ?? Array.Empty<string>()).Where(t => t != null && StyleRules.IsColorMap(t)).Select(t => PieceOf(model, t))
                .Where(p => p != null).Select(p => p!).Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// Cartes de chaque matière (<paramref name="materials"/>, noms donnés par Unity) d'un modèle à pièces. Une matière absente du
        /// résultat garde la règle d'avant (<see cref="StyleRules.MaterialTexture"/>). Vide si le modèle n'a pas de textures par pièce.
        /// </summary>
        public static IReadOnlyDictionary<string, StylePieceMaps> Link(string modelPath, IEnumerable<string> materials, IEnumerable<string> textures)
        {
            var result = new Dictionary<string, StylePieceMaps>(StringComparer.Ordinal);
            string model = ModelKey(modelPath);
            List<string> all = (textures ?? Array.Empty<string>()).Where(t => t != null).OrderBy(t => t, StringComparer.Ordinal).ToList();
            IReadOnlyList<string> pieces = Pieces(modelPath, all);
            if (pieces.Count == 0) return result;
            List<string> names = (materials ?? Array.Empty<string>()).Where(m => m != null).Distinct(StringComparer.Ordinal)
                .OrderBy(m => m, StringComparer.Ordinal).ToList();
            var used = new HashSet<string>(StringComparer.Ordinal);
            var unmatched = new List<string>();
            foreach (string material in names)
            {
                string? piece = Match(model, material, pieces);
                if (piece == null)
                {
                    unmatched.Add(material);
                    continue;
                }
                result[material] = Maps(model, piece, all);
                used.Add(piece);
            }
            // Noms sans rapport (« Material », « Material.001 ») : autant de matières que de pièces restées libres → par ordre ordinal.
            List<string> free = pieces.Where(p => !used.Contains(p)).ToList();
            if (unmatched.Count > 0 && unmatched.Count == free.Count)
                for (int i = 0; i < unmatched.Count; i++) result[unmatched[i]] = Maps(model, free[i], all);
            return result;
        }

        /// <summary>
        /// Pièce au nom de la matière : nom égal (« barrel01 », « wooden_barrels_01_barrel01 »), sinon nom qui finit par la pièce la plus
        /// longue (« tonneau_barrel01 ») ; le suffixe de Blender (« .001 ») ne compte pas. Null sinon.
        /// </summary>
        public static string? Match(string modelKey, string material, IReadOnlyList<string> pieces)
        {
            string key = StyleRules.Key(Regex.Replace(material ?? string.Empty, @"\.[0-9]{3}$", ""));
            if (key.Length == 0) return null;
            foreach (string p in pieces)
                if (key == p || key == modelKey + p) return p;
            string? best = null;
            foreach (string p in pieces)
                if (key.EndsWith(p, StringComparison.Ordinal) && (best == null || p.Length > best.Length)) best = p;
            return best;
        }

        private static StylePieceMaps Maps(string model, string piece, List<string> all)
        {
            string? Of(Func<string, bool> kind) => all.FirstOrDefault(t => kind(t) && PieceOf(model, t) == piece);
            return new StylePieceMaps(piece, Of(StyleRules.IsColorMap), Of(StyleRules.IsNormalMap), Of(IsRoughMap), Of(StyleRules.IsMetalMap));
        }

        /// <summary>Carte de rugosité (« _rough_ », « _roughness »).</summary>
        public static bool IsRoughMap(string path) => Words(path).Any(w => w == "rough" || w == "roughness");

        private static bool IsResolution(string word) => Regex.IsMatch(word, "^[0-9]+k$");

        private static List<string> Words(string path)
        {
            string p = (path ?? string.Empty).Replace('\\', '/');
            int slash = p.LastIndexOf('/');
            return p.Substring(slash + 1).ToLowerInvariant().Split(new[] { '_', '-', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }
    }
}
