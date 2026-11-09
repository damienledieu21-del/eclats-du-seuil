using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.12.7 : ce qui est cassé dans une matière importée (le problème que la vérification de l'essai signale).</summary>
    public enum MaterialFault
    {
        /// <summary>Rien à signaler.</summary>
        None = 0,
        /// <summary>Shader absent, en erreur (« Hidden/InternalErrorShader ») ou d'un autre rendu qu'URP : l'objet sort rose magenta.</summary>
        Pink,
        /// <summary>Matière sans texture ni couleur (blanche par défaut) : l'objet sort blanc pur.</summary>
        White,
    }

    /// <summary>
    /// Sous-lot V2.12.7 (essai du 9 novembre 2026 : four et comptoir blancs, petits objets roses près de « Herbe de l'Aube », tonneau bleu
    /// uni) : règles pures, sans Unity, de la réparation des matières des modèles posés (EdsMaterialRepair) et de leur vérification.
    /// Une matière dont le shader n'est pas d'URP est convertie en URP Lit ; une matière blanche sans texture reprend la texture d'une pièce
    /// de même nom, sinon une couleur de repli d'après le genre d'objet (bois, brique, pierre, argile, métal, pain…).
    /// </summary>
    public static class MaterialRepairRules
    {
        /// <summary>Seuil à partir duquel un canal compte comme blanc (couleur par défaut d'une matière sans réglage).</summary>
        public const double WhiteFrom = 0.9;

        /// <summary>
        /// Ce shader sortirait-il rose (ou faux) avec URP et doit-il passer sur « Universal Render Pipeline/Lit » ? Vrai pour un shader
        /// absent, « Hidden/InternalErrorShader », « Standard » et ses variantes, « Legacy Shaders/ », « Mobile/ », « Nature/ ». Faux pour
        /// URP, les Shader Graph, les shaders du jeu (« EdS/ »), les particules (convertis à part), l'interface et les sprites.
        /// </summary>
        public static bool NeedsUrp(string? shaderName)
        {
            if (string.IsNullOrWhiteSpace(shaderName)) return true;
            string n = shaderName!.Trim();
            if (n.Equals("Hidden/InternalErrorShader", StringComparison.OrdinalIgnoreCase)) return true;
            if (n.StartsWith("Hidden/", StringComparison.OrdinalIgnoreCase) || n.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal)
                || n.StartsWith("Shader Graphs/", StringComparison.Ordinal) || n.StartsWith("EdS/", StringComparison.Ordinal)
                || n.StartsWith("Particles/", StringComparison.Ordinal) || n.StartsWith("Sprites/", StringComparison.Ordinal)
                || n.StartsWith("UI/", StringComparison.Ordinal) || n.StartsWith("TextMeshPro/", StringComparison.Ordinal)
                || n.StartsWith("HDRP/", StringComparison.Ordinal) || n.StartsWith("Skybox/", StringComparison.Ordinal)) return false;
            return n.Equals("Standard", StringComparison.Ordinal) || n.StartsWith("Standard ", StringComparison.Ordinal)
                || n.StartsWith("Legacy Shaders/", StringComparison.Ordinal) || n.StartsWith("Mobile/", StringComparison.Ordinal)
                || n.StartsWith("Nature/", StringComparison.Ordinal) || n.StartsWith("Diffuse", StringComparison.Ordinal)
                || n.StartsWith("Unlit/", StringComparison.Ordinal) || n.StartsWith("VertexLit", StringComparison.Ordinal)
                || n.StartsWith("Specular", StringComparison.Ordinal) || n.StartsWith("Bumped", StringComparison.Ordinal);
        }

        /// <summary>Matière blanche par défaut : aucune texture et une couleur blanche (ou sans réglage).</summary>
        public static bool IsPlainWhite(double r, double g, double b, bool hasTexture) =>
            !hasTexture && r >= WhiteFrom && g >= WhiteFrom && b >= WhiteFrom;

        /// <summary>Le défaut d'une matière d'après son shader, sa texture et sa couleur.</summary>
        public static MaterialFault Fault(string? shaderName, double r, double g, double b, bool hasTexture) =>
            NeedsUrp(shaderName) ? MaterialFault.Pink : IsPlainWhite(r, g, b, hasTexture) ? MaterialFault.White : MaterialFault.None;

        /// <summary>
        /// Texture de couleur d'une matière : celle dont le nom commence par celui de la matière (sans « mat », « material »…), sinon celle
        /// qui porte le nom du modèle (« Brick_Bread_Oven_torricane_BaseColor » pour « eds_SF_Brick_Bread_Oven_torricane.fbx »). Jamais « la
        /// seule du dossier » : un dossier de lot en a pour plusieurs modèles (le four ne prendrait pas la texture d'un pain). Null sinon.
        /// </summary>
        public static string? BestTexture(string material, string model, IEnumerable<string> colorMaps)
        {
            List<string> maps = (colorMaps ?? Array.Empty<string>()).Where(t => !string.IsNullOrEmpty(t) && StyleRules.IsColorMap(t))
                .OrderBy(t => t, StringComparer.Ordinal).ToList();
            string stem = StripMaterial(StyleRules.Key(material));
            if (stem.Length >= 3 && !Generic.Contains(stem)
                && maps.FirstOrDefault(t => StyleRules.Key(StyleRules.FileName(t)).StartsWith(stem, StringComparison.Ordinal)) is string named) return named;
            string core = ModelCore(model);
            if (core.Length >= 4)
            {
                string? own = maps.FirstOrDefault(t => StyleRules.Key(StyleRules.FileName(t)).Contains(core));
                if (own != null) return own;
            }
            return null;
        }

        /// <summary>Nom réduit d'un modèle sans le préfixe du jeu (« eds_SF_ ») ni l'extension.</summary>
        public static string ModelCore(string model)
        {
            string name = StyleRules.FileName(model ?? string.Empty);
            int dot = name.LastIndexOf('.');
            if (dot > 0) name = name.Substring(0, dot);
            string key = StyleRules.Key(name);
            foreach (string prefix in new[] { "edssf", "edspoly", "eds" })
                if (key.StartsWith(prefix, StringComparison.Ordinal) && key.Length > prefix.Length + 3) return key.Substring(prefix.Length);
            return key;
        }

        private static string StripMaterial(string key)
        {
            foreach (string suffix in new[] { "material", "mat", "mtl" })
                if (key.EndsWith(suffix, StringComparison.Ordinal) && key.Length > suffix.Length + 2) key = key.Substring(0, key.Length - suffix.Length);
            return key.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        }

        private static readonly HashSet<string> Generic = new HashSet<string>(StringComparer.Ordinal)
            { "default", "defaultmaterial", "lambert", "blinn", "phong", "standard", "material", "mat", "none", "initialshadinggroup" };

        /// <summary>Gris de fer, repli des objets de métal (rendus aussi un peu métalliques par la réparation).</summary>
        public static readonly (double, double, double) MetalColor = (0.32, 0.33, 0.36);

        // Genre d'objet -> couleur de repli, du plus précis au plus général (le premier mot trouvé dans le nom gagne).
        private static readonly (string[] Words, (double, double, double) Color)[] Fallbacks =
        {
            (new[] { "oven", "four", "brick", "brique", "chimney", "cheminee", "hearth" }, (0.56, 0.28, 0.2)),
            (new[] { "bread", "pain", "loaf", "baguette", "croissant", "roll", "bun", "pastry", "brioche", "toast", "flatbread" }, (0.74, 0.5, 0.24)),
            (new[] { "cheese", "fromage" }, (0.88, 0.72, 0.28)),
            (new[] { "meat", "sausage", "ham", "viande", "saucisse", "steak" }, (0.56, 0.22, 0.18)),
            (new[] { "apple", "tomato", "pomme", "tomate", "cherry", "strawberry" }, (0.68, 0.14, 0.1)),
            (new[] { "pear", "poire", "lemon", "citron", "banana" }, (0.72, 0.7, 0.24)),
            (new[] { "orange", "pumpkin", "carrot", "citrouille", "carotte" }, (0.86, 0.46, 0.12)),
            (new[] { "bottle", "bouteille", "glass", "flask", "verre" }, (0.2, 0.42, 0.3)),
            (new[] { "flour", "farine", "sack", "sac", "cloth", "linen", "tent", "tente", "canvas", "banner", "bunting", "sheet" }, (0.84, 0.78, 0.62)),
            (new[] { "plate", "assiette", "bowl", "bol", "dish", "cup", "tankard", "mug" }, (0.78, 0.74, 0.66)),
            (new[] { "clay", "argile", "pot", "vase", "jar", "pitcher", "amphora", "terracotta", "tuile" }, (0.62, 0.38, 0.24)),
            (new[] { "stone", "pierre", "rock", "wall", "castle", "tower", "bridge", "arch", "menhir", "marble", "granite", "fountain", "tomb", "gravestone", "pillar" }, (0.5, 0.49, 0.46)),
            (new[] { "metal", "iron", "anvil", "enclume", "hammer", "marteau", "sword", "epee", "knife", "dagger", "blade", "nail", "chain", "steel", "armor", "helmet",
                "axe", "mace", "lantern", "gate", "grille", "pan", "kettle", "cauldron" }, MetalColor),
            (new[] { "hay", "foin", "straw", "paille", "thatch", "chaume" }, (0.74, 0.62, 0.3)),
        };

        /// <summary>Bois sombre : repli de tout ce qui n'a pas de genre reconnu (table, comptoir, tonneau, caisse, charrette, échelle…).</summary>
        public static readonly (double R, double G, double B) Wood = (0.45, 0.31, 0.19);

        /// <summary>
        /// Couleur de repli d'une matière sans texture ni couleur, d'après les noms donnés (matière, modèle, dossier) : le premier genre
        /// reconnu l'emporte (nom de la matière d'abord, puis le modèle), sinon le bois. Jamais du blanc.
        /// </summary>
        public static (double R, double G, double B) FallbackColor(params string?[] names)
        {
            foreach (string? name in names)
            {
                if (string.IsNullOrEmpty(name)) continue;
                string[] words = Split(name!);
                foreach ((string[] list, (double, double, double) color) in Fallbacks)
                    if (words.Any(w => list.Contains(w))) return color;
            }
            return Wood;
        }

        /// <summary>Mots d'un nom (« Brick_Bread_Oven » → brick, bread, oven ; « WoodenBox1 » → wooden, box).</summary>
        public static string[] Split(string name)
        {
            var words = new List<string>();
            var current = new System.Text.StringBuilder();
            char previous = ' ';
            foreach (char c in name)
            {
                if (!char.IsLetter(c))
                {
                    if (current.Length > 0) words.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    if (current.Length > 0 && char.IsUpper(c) && char.IsLower(previous))
                    {
                        words.Add(current.ToString());
                        current.Clear();
                    }
                    current.Append(char.ToLowerInvariant(c));
                }
                previous = c;
            }
            if (current.Length > 0) words.Add(current.ToString());
            return words.ToArray();
        }

        /// <summary>Teinte franchement bleue (le tonneau uni de l'essai du 9 novembre 2026).</summary>
        public static bool IsBlue(double r, double g, double b) => b > r + 0.08 && b >= g;

        /// <summary>
        /// Une matière a-t-elle l'air de bois ? Sans texture : brune ou sombre, ni bleue ni blanche ; avec texture : sa teinte n'est pas bleue.
        /// </summary>
        public static bool LooksLikeWood(double r, double g, double b, bool hasTexture)
        {
            if (IsBlue(r, g, b)) return false;
            if (hasTexture) return true;
            return !IsPlainWhite(r, g, b, false) && r >= b;
        }

        /// <summary>
        /// Un tonneau (ses matières : couleur et présence d'une texture) est de bois : aucune matière bleue, et au moins la moitié des matières
        /// ont l'air de bois (les cercles de fer, gris, ne l'emportent pas sur les planches ; un seul pan brun ne suffit plus). Sans matière : non.
        /// </summary>
        public static bool BarrelIsWood(IEnumerable<(double R, double G, double B, bool Textured)> materials)
        {
            List<(double R, double G, double B, bool Textured)> all = (materials ?? Array.Empty<(double, double, double, bool)>()).ToList();
            if (all.Count == 0 || all.Any(m => IsBlue(m.R, m.G, m.B))) return false;
            return all.Count(m => LooksLikeWood(m.R, m.G, m.B, m.Textured)) * 2 >= all.Count;
        }
    }
}
