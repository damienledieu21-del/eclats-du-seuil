using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Runtime
{
    /// <summary>Ce qu'un modèle de l'essai de style représente.</summary>
    public enum StyleSlot
    {
        Hero = 0,
        Wolf = 1,
        Boar = 2,
        Tree = 3,
        Rock = 4,
        Plant = 5,
    }

    /// <summary>Ce que fait un corps animé, pour choisir son animation.</summary>
    public enum StyleMotion
    {
        Idle = 0,
        Walk = 1,
        Run = 2,
        Attack = 3,
        Death = 4,
    }

    /// <summary>Un son de l'essai de style.</summary>
    public enum StyleSound
    {
        Hit = 0,
        Block = 1,
        Parry = 2,
        Death = 3,
        Potion = 4,
        Gather = 5,
        Craft = 6,
    }

    /// <summary>
    /// Essai de style (décision de Damien du 25 octobre 2026 : « un petit essai de style » après le 12.9, avant le lot 13, avec des packs
    /// gratuits) : règles sans Unity, testées, pour habiller les Vallons avec des modèles et des sons déposés par Damien dans
    /// <c>unity/Assets/Style</c>, quels que soient les noms des fichiers des packs. Le jeu, lui, ne change pas : ni le moteur, ni les
    /// emprises, ni les obstacles ; seuls l'aspect et le son changent, et sans pack tout reste en formes simples.
    /// </summary>
    public static class StyleRules
    {
        private static readonly Dictionary<StyleSlot, (string[] Want, string[] Avoid)> Models = new Dictionary<StyleSlot, (string[], string[])>
        {
            [StyleSlot.Hero] = (new[] { "knight" }, new[] { "weapon", "sword", "shield" }),
            [StyleSlot.Wolf] = (new[] { "wolf" }, new string[0]),
            [StyleSlot.Boar] = (new[] { "boar", "pig" }, new string[0]),
            [StyleSlot.Tree] = (new[] { "commontree", "tree", "birch", "pine", "maple" }, new[] { "dead", "stump", "log", "leaves", "leaf", "trunk" }),
            [StyleSlot.Rock] = (new[] { "rock", "boulder", "stone" }, new[] { "path", "pebble", "small", "tiny" }),
            [StyleSlot.Plant] = (new[] { "bush", "fern", "grass", "flower", "plant", "clover" }, new[] { "big", "tall", "tree" }),
        };

        /// <summary>Mots cherchés dans les noms des sons, par ordre de préférence (noms des packs gratuits de Kenney d'abord).</summary>
        private static readonly Dictionary<StyleSound, string[]> Sounds = new Dictionary<StyleSound, string[]>
        {
            [StyleSound.Hit] = new[] { "impactpunchmedium", "impactpunch", "punch", "hit" },
            [StyleSound.Block] = new[] { "impactplate", "impactmetal", "shield", "block" },
            [StyleSound.Parry] = new[] { "impactbell", "metalclick", "parry", "clang" },
            [StyleSound.Death] = new[] { "impactsoftheavy", "impactwoodheavy", "death", "fall" },
            [StyleSound.Potion] = new[] { "bottle", "drink", "potion", "liquid", "handlesmallleather", "cloth" },
            [StyleSound.Gather] = new[] { "impactmining", "chop", "dig", "gather" },
            [StyleSound.Craft] = new[] { "anvil", "forge", "impactmetalheavy", "metalpot" },
        };

        private static readonly Dictionary<StyleMotion, string[]> Clips = new Dictionary<StyleMotion, string[]>
        {
            [StyleMotion.Idle] = new[] { "idle", "stand", "breath" },
            [StyleMotion.Walk] = new[] { "walk" },
            [StyleMotion.Run] = new[] { "run", "gallop", "sprint", "trot" },
            [StyleMotion.Attack] = new[] { "attack", "slash", "sword", "bite", "punch", "hit" },
            [StyleMotion.Death] = new[] { "death", "die", "dead", "dying" },
        };

        /// <summary>Repli quand le modèle n'a pas l'animation demandée.</summary>
        private static readonly Dictionary<StyleMotion, StyleMotion[]> Fallbacks = new Dictionary<StyleMotion, StyleMotion[]>
        {
            [StyleMotion.Idle] = new StyleMotion[0],
            [StyleMotion.Walk] = new[] { StyleMotion.Run, StyleMotion.Idle },
            [StyleMotion.Run] = new[] { StyleMotion.Walk, StyleMotion.Idle },
            [StyleMotion.Attack] = new[] { StyleMotion.Idle },
            [StyleMotion.Death] = new StyleMotion[0],
        };

        /// <summary>Nom réduit pour comparer : minuscules, lettres et chiffres seulement (« Common_Tree_1.fbx » → « commontree1fbx »).</summary>
        public static string Key(string name) =>
            new string((name ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        /// <summary>Le fichier (chemin ou nom) correspond-il à <paramref name="slot"/> ? On juge le nom du fichier, pas ses dossiers.</summary>
        public static bool Matches(string path, StyleSlot slot)
        {
            string file = Key(FileName(path));
            (string[] want, string[] avoid) = Models[slot];
            return want.Any(w => file.Contains(w)) && !avoid.Any(a => file.Contains(a));
        }

        /// <summary>
        /// Modèles qui correspondent à <paramref name="slot"/>, les FBX d'abord (les packs livrent aussi des OBJ sans animation), puis dans
        /// l'ordre ordinal de leur chemin (choix stable).
        /// </summary>
        public static IReadOnlyList<string> Candidates(IEnumerable<string> paths, StyleSlot slot) =>
            (paths ?? Array.Empty<string>()).Where(p => Matches(p, slot))
                .OrderBy(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(p => p, StringComparer.Ordinal).ToList();

        /// <summary>
        /// Premier son qui correspond à <paramref name="sound"/> (mots dans l'ordre de préférence, puis chemin ordinal), ou null.
        /// </summary>
        public static string? PickSound(IEnumerable<string> paths, StyleSound sound)
        {
            var files = (paths ?? Array.Empty<string>()).OrderBy(p => p, StringComparer.Ordinal).ToList();
            foreach (string word in Sounds[sound])
                if (files.FirstOrDefault(p => Key(FileName(p)).Contains(word)) is string found) return found;
            return null;
        }

        /// <summary>
        /// Choix stable d'un modèle parmi <paramref name="count"/> pour l'élément <paramref name="id"/> (« arbre3 » a toujours le même arbre) :
        /// empreinte FNV-1a de l'identifiant, sans hasard. -1 s'il n'y a aucun modèle.
        /// </summary>
        public static int Variant(string id, int count)
        {
            if (count <= 0) return -1;
            uint hash = 2166136261;
            foreach (char c in id ?? string.Empty)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return (int)(hash % (uint)count);
        }

        /// <summary>
        /// Animation à jouer parmi celles du modèle (noms des clips, « HumanArmature|Idle »), avec repli ; null si aucune ne convient. Pour
        /// chaque mot, le nom exact l'emporte (« Idle » avant « Idle_swordLeft »), puis un nom qui commence par le mot, puis un nom qui le
        /// contient.
        /// </summary>
        public static string? Clip(IEnumerable<string> clips, StyleMotion motion)
        {
            var names = (clips ?? Array.Empty<string>()).ToList();
            foreach (StyleMotion m in new[] { motion }.Concat(Fallbacks[motion]))
                foreach (string word in Clips[m])
                {
                    string? found = names.FirstOrDefault(n => Segment(n) == word) ?? names.FirstOrDefault(n => Segment(n).StartsWith(word, StringComparison.Ordinal))
                        ?? names.FirstOrDefault(n => Key(n).Contains(word));
                    if (found != null) return found;
                }
            return null;
        }

        /// <summary>Dernier segment d'un nom de clip (après le dernier « | »), réduit.</summary>
        private static string Segment(string clip)
        {
            string c = clip ?? string.Empty;
            int bar = c.LastIndexOf('|');
            return Key(bar >= 0 ? c.Substring(bar + 1) : c);
        }

        /// <summary>Animations qui tournent en boucle (attente, marche, course).</summary>
        public static bool Loops(string clip)
        {
            string key = Key(clip);
            return new[] { StyleMotion.Idle, StyleMotion.Walk, StyleMotion.Run }.Any(m => Clips[m].Any(w => key.Contains(w)))
                && !Clips[StyleMotion.Death].Any(w => key.Contains(w)) && !Clips[StyleMotion.Attack].Any(w => key.Contains(w));
        }

        /// <summary>
        /// Allure d'après la vitesse au sol affichée (m/s) : attente sous 0,3, marche sous 3,5, course au-delà ; la mort l'emporte, puis
        /// l'attaque.
        /// </summary>
        public static StyleMotion Motion(double speed, bool dead, bool attacking) =>
            dead ? StyleMotion.Death : attacking ? StyleMotion.Attack : speed < 0.3 ? StyleMotion.Idle : speed < 3.5 ? StyleMotion.Walk : StyleMotion.Run;

        /// <summary>Facteur d'échelle uniforme qui donne au modèle la hauteur voulue (1 si sa hauteur est nulle ou inconnue).</summary>
        public static double Scale(double modelHeight, double height) =>
            modelHeight > 1e-6 && height > 0 ? height / modelHeight : 1;

        /// <summary>
        /// Ce qu'habille un combattant, d'après son identifiant (vies suivantes comprises : « loup1_v3 ») : le joueur en chevalier, les loups
        /// et les sangliers ; les autres (archer, rôdeur, Gardien, PNJ) gardent leur forme simple pendant l'essai. Null : rien.
        /// </summary>
        public static StyleSlot? ForCombatant(string id, bool player)
        {
            if (player) return StyleSlot.Hero;
            string key = Key(id);
            if (key.StartsWith("loup", StringComparison.Ordinal)) return StyleSlot.Wolf;
            if (key.StartsWith("sanglier", StringComparison.Ordinal)) return StyleSlot.Boar;
            return null;
        }

        /// <summary>Ce qu'habille un obstacle du décor : arbres, blocs de la carrière et pierres du Seuil. Null : il garde sa forme.</summary>
        public static StyleSlot? ForObstacle(string id)
        {
            string key = Key(id);
            if (key.StartsWith("arbre", StringComparison.Ordinal)) return StyleSlot.Tree;
            if (key.StartsWith("bloc", StringComparison.Ordinal) || key.StartsWith("pierreseuil", StringComparison.Ordinal)) return StyleSlot.Rock;
            return null;
        }

        private static string FileName(string path)
        {
            string p = (path ?? string.Empty).Replace('\\', '/');
            int slash = p.LastIndexOf('/');
            return slash >= 0 ? p.Substring(slash + 1) : p;
        }
    }
}
