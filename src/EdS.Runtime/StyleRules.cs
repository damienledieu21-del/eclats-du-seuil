using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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

        /// <summary>Essai réaliste : bois mort (souches, troncs couchés), posé dans la forêt autour de l'aire.</summary>
        Debris = 6,

        /// <summary>
        /// Essai réaliste (demande de Damien du 26 octobre 2026) : un personnage de Mixamo rangé dans son dossier
        /// (<c>Mixamo/Archer</c>…), pour un PNJ ou un ennemi d'après son identifiant (<see cref="StyleRules.CharacterOf"/>).
        /// </summary>
        Character = 7,

        /// <summary>Essai réaliste (27 octobre 2026) : arbuste des haies (« haie_ouest » était un bloc de pierre).</summary>
        Shrub = 8,
    }

    /// <summary>Essai réaliste : matière d'un bâtiment (textures d'un dossier « Surfaces »).</summary>
    public enum StyleSurface
    {
        /// <summary>Pierre (murs, piliers de pierre).</summary>
        Stone = 0,

        /// <summary>Crépi (murs du moulin).</summary>
        Plaster = 1,

        /// <summary>Tuiles (toits).</summary>
        Roof = 2,

        /// <summary>Planches (pont, ailes du moulin, piliers de bois).</summary>
        Wood = 3,

        /// <summary>Pavés (murets du bord de l'aire).</summary>
        Cobble = 4,

        /// <summary>Écorce (rondins de la palissade ; demande de Damien du 27 octobre 2026).</summary>
        Bark = 5,
    }

    /// <summary>
    /// Essai réaliste (demandes de Damien du 27 octobre 2026 : « la forge est à faire, les minerais et l'herbe de l'Aube aussi, le pont ») :
    /// objet de décor rangé dans les packs, choisi par son nom (<see cref="StyleRules.PropCandidates"/>).
    /// </summary>
    public enum StyleProp
    {
        /// <summary>Foyer de la forge (« stone_fire_pit »).</summary>
        FirePit = 0,

        /// <summary>Marteau posé sur l'enclume.</summary>
        Hammer = 1,

        /// <summary>Caisse près de la forge.</summary>
        Crate = 2,

        /// <summary>Tonneau près de la forge.</summary>
        Barrel = 3,

        /// <summary>Enclume (sinon faite par le code).</summary>
        Anvil = 4,

        /// <summary>Tronçon de ponton en bois, pour le pont.</summary>
        Bridge = 5,

        /// <summary>Plante des nœuds d'herboristerie (herbe de l'Aube).</summary>
        Herb = 6,

        /// <summary>Moulin (modèle de Sketchfab choisi par Damien le 27 octobre 2026).</summary>
        Mill = 7,
    }

    /// <summary>Clôture qu'habille un obstacle en longueur (<see cref="StyleRules.FenceFor"/>).</summary>
    public enum StyleFence
    {
        None = 0,

        /// <summary>Palissade de rondins taillés en pointe (murs, portes).</summary>
        Palisade = 1,

        /// <summary>Haie d'arbustes.</summary>
        Hedge = 2,
    }

    /// <summary>Un rondin de palissade, posé debout : centre au sol, rayon et hauteur (m).</summary>
    public readonly struct StyleLog
    {
        public StyleLog(double x, double z, double radius, double height)
        {
            X = x;
            Z = z;
            Radius = radius;
            Height = height;
        }

        public double X { get; }
        public double Z { get; }
        public double Radius { get; }
        public double Height { get; }
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
    /// Essai réaliste (choix de Damien du 26 octobre 2026) : couche de sol du terrain. L'ordre est celui des couches du terrain dans Unity.
    /// </summary>
    public enum StyleGround
    {
        /// <summary>Terre de forêt (sous les arbres, et par taches).</summary>
        Soil = 0,

        /// <summary>Roche (autour des blocs de la carrière, des pierres du Seuil, des murs).</summary>
        Rock = 1,

        /// <summary>Herbe (le reste du sol).</summary>
        Grass = 2,
    }

    /// <summary>Un endroit du sol qui change sa couche autour de lui (un rocher appelle la roche, un arbre la terre).</summary>
    public readonly struct StyleSpot
    {
        public StyleSpot(double x, double z, double radius, StyleGround ground)
        {
            X = x;
            Z = z;
            Radius = radius;
            Ground = ground;
        }

        public double X { get; }
        public double Z { get; }

        /// <summary>Rayon (m) où la couche est pleine ; elle s'efface ensuite sur <see cref="StyleRules.SpotFade"/> mètres.</summary>
        public double Radius { get; }

        public StyleGround Ground { get; }
    }

    /// <summary>Part de chaque couche en un point du sol (somme 1).</summary>
    public readonly struct StyleMix
    {
        public StyleMix(double soil, double rock, double grass)
        {
            Soil = soil;
            Rock = rock;
            Grass = grass;
        }

        public double Soil { get; }
        public double Rock { get; }
        public double Grass { get; }

        public double this[StyleGround ground] => ground == StyleGround.Soil ? Soil : ground == StyleGround.Rock ? Rock : Grass;
    }

    /// <summary>
    /// Essai de style (décision de Damien du 25 octobre 2026 : « un petit essai de style » après le 12.9, avant le lot 13, avec des packs
    /// gratuits) : règles sans Unity, testées, pour habiller les Vallons avec des modèles et des sons déposés par Damien dans
    /// <c>unity/Assets/Style</c>, quels que soient les noms des fichiers des packs. Le jeu, lui, ne change pas : ni le moteur, ni les
    /// emprises, ni les obstacles ; seuls l'aspect et le son changent, et sans pack tout reste en formes simples.
    /// <para>
    /// Essai réaliste (26 octobre 2026 : le premier essai, tout en formes simples des packs « low poly », ne plaît pas ; Damien choisit
    /// « un mélange de réaliste et de stylisé coloré », « lumineux, réaliste mais pas trop ») : les modèles réalistes (Poly Haven, Mixamo)
    /// passent devant les packs stylisés, le sol devient un terrain à trois couches (terre, roche, herbe) et la scène reçoit un ciel, une
    /// lumière, une brume et des couleurs. Toujours rien côté moteur.
    /// </para>
    /// </summary>
    public static class StyleRules
    {
        private static readonly Dictionary<StyleSlot, (string[] Want, string[] Avoid)> Models = new Dictionary<StyleSlot, (string[], string[])>
        {
            // Le paladin de Mixamo d'abord, sinon le chevalier des packs. Les fichiers d'animation de Mixamo (« Sword And Shield Idle »)
            // ne sont pas des héros.
            [StyleSlot.Hero] = (new[] { "paladin", "knight" }, new[] { "weapon", "sword", "shield" }),
            [StyleSlot.Wolf] = (new[] { "wolf" }, new string[0]),
            [StyleSlot.Boar] = (new[] { "boar", "pig" }, new string[0]),
            [StyleSlot.Tree] = (new[] { "firtree", "pine", "commontree", "tree", "birch", "maple" },
                new[] { "dead", "stump", "log", "leaves", "leaf", "trunk", "root", "bark", "branch" }),
            // « stone_fire_pit » est le foyer de la forge, pas un rocher ; la pervenche est l'herbe de l'Aube, pas une plante du décor.
            [StyleSlot.Rock] = (new[] { "rock", "boulder", "stone" }, new[] { "path", "pebble", "small", "tiny", "fire", "pit" }),
            [StyleSlot.Plant] = (new[] { "bush", "fern", "grass", "flower", "plant", "clover", "shrub", "moss", "nettle", "celandine", "dandelion", "weed" },
                new[] { "big", "tall", "tree", "rock", "periwinkle" }),
            [StyleSlot.Debris] = (new[] { "stump", "log", "trunk", "root", "branch" }, new[] { "quiver" }),
            [StyleSlot.Shrub] = (new[] { "shrub", "hedge", "bush" }, new[] { "dead", "berry", "flower" }),
            // Les personnages sont rangés par dossier (StyleRules.Characters), pas reconnus par leur nom.
            [StyleSlot.Character] = (new string[0], new string[0]),
        };

        /// <summary>Mots des objets de décor (nom du fichier), par ordre de préférence.</summary>
        private static readonly Dictionary<StyleProp, (string[] Want, string[] Avoid)> PropWords = new Dictionary<StyleProp, (string[], string[])>
        {
            [StyleProp.FirePit] = (new[] { "firepit", "fireplace", "brazier", "hearth" }, new string[0]),
            [StyleProp.Hammer] = (new[] { "hammer" }, new string[0]),
            [StyleProp.Crate] = (new[] { "crate" }, new string[0]),
            [StyleProp.Barrel] = (new[] { "barrel" }, new string[0]),
            [StyleProp.Anvil] = (new[] { "anvil" }, new string[0]),
            [StyleProp.Bridge] = (new[] { "pier", "bridge" }, new string[0]),
            [StyleProp.Herb] = (new[] { "periwinkle", "herb" }, new string[0]),
            [StyleProp.Mill] = (new[] { "windmill", "mill" }, new string[0]),
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

        /// <summary>
        /// Mots des animations. « sword » n'est pas un mot d'attaque : les animations de Mixamo s'appellent toutes « Sword And Shield … »
        /// (l'attente doit tourner en boucle).
        /// </summary>
        private static readonly Dictionary<StyleMotion, string[]> Clips = new Dictionary<StyleMotion, string[]>
        {
            [StyleMotion.Idle] = new[] { "idle", "stand", "breath" },
            [StyleMotion.Walk] = new[] { "walk" },
            [StyleMotion.Run] = new[] { "run", "gallop", "sprint", "trot" },
            [StyleMotion.Attack] = new[] { "attack", "slash", "strike", "bite", "punch", "hit" },
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

        /// <summary>Sources réalistes : dossiers où sont rangés les fichiers de Poly Haven, de Mixamo et de Sketchfab.</summary>
        private static readonly string[] Realistic = { "polyhaven", "mixamo", "sketchfab" };

        /// <summary>Profondeur (m) du lit de la rivière sous le sol, une fois la berge descendue.</summary>
        public const double RiverBed = 1.0;

        /// <summary>Largeur (m) de la berge : le lit descend du bord de la rivière jusqu'à sa profondeur sur cette distance.</summary>
        public const double RiverBank = 1.0;

        /// <summary>Hauteur (m) de la surface de l'eau, sous le sol où l'on marche (berges visibles).</summary>
        public const double WaterLevel = -0.3;

        /// <summary>Distance (m) en deçà de laquelle un PNJ se tourne vers le joueur.</summary>
        public const double FaceRange = 8.0;

        /// <summary>Mots des textures de sol (dans un dossier « Ground ») : roche, puis herbe ; le reste est de la terre.</summary>
        private static readonly string[] RockGround = { "rock", "trail", "stone", "gravel", "cliff" };
        private static readonly string[] GrassGround = { "grass", "meadow", "lawn", "moss" };

        /// <summary>Distance (m) sur laquelle la couche d'un endroit s'efface autour de son rayon.</summary>
        public const double SpotFade = 3.0;

        /// <summary>Taille (m) des taches de terre semées dans l'herbe.</summary>
        public const double PatchSize = 9.0;

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

        /// <summary>Le fichier vient-il d'une source réaliste (rangé dans un dossier Poly Haven ou Mixamo) ?</summary>
        public static bool IsRealistic(string path)
        {
            string key = Key(path);
            return Realistic.Any(r => key.Contains(r));
        }

        /// <summary>
        /// Modèles qui correspondent à <paramref name="slot"/>, dans un ordre stable : les FBX d'abord (les packs livrent aussi des OBJ sans
        /// animation), puis les sources réalistes, puis selon le premier mot cherché qu'ils contiennent (« paladin » avant « knight »), puis
        /// dans l'ordre ordinal de leur chemin.
        /// </summary>
        public static IReadOnlyList<string> Candidates(IEnumerable<string> paths, StyleSlot slot) =>
            (paths ?? Array.Empty<string>()).Where(p => Matches(p, slot))
                .OrderBy(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(p => IsRealistic(p) ? 0 : 1)
                .ThenBy(p => WantRank(p, slot))
                .ThenBy(p => p, StringComparer.Ordinal).ToList();

        /// <summary>
        /// Modèles retenus pour le catalogue (au plus <paramref name="max"/>) : les candidats dans leur ordre ; pour les rochers et les
        /// plantes, seulement les réalistes s'il y en a (un rocher dessiné détonne sur le sol réaliste ; essai de Damien du 27 octobre 2026 :
        /// « les espèces de buissons tout rouges » étaient des buissons d'automne du MegaKit, mêlés aux plantes de Poly Haven).
        /// </summary>
        public static IReadOnlyList<string> ForCatalog(IEnumerable<string> paths, StyleSlot slot, int max)
        {
            IReadOnlyList<string> all = Candidates(paths, slot);
            if ((slot == StyleSlot.Rock || slot == StyleSlot.Plant || slot == StyleSlot.Shrub) && all.Any(IsRealistic)) all = all.Where(IsRealistic).ToList();
            return all.Take(Math.Max(0, max)).ToList();
        }

        /// <summary>
        /// Modèles d'un objet de décor (<see cref="StyleProp"/>), dans l'ordre des <see cref="Candidates"/> : FBX d'abord, puis sources
        /// réalistes, puis premier mot cherché, puis chemin ordinal.
        /// </summary>
        public static IReadOnlyList<string> PropCandidates(IEnumerable<string> paths, StyleProp prop)
        {
            (string[] want, string[] avoid) = PropWords[prop];
            int Rank(string p)
            {
                string file = Key(FileName(p));
                for (int i = 0; i < want.Length; i++)
                    if (file.Contains(want[i])) return i;
                return want.Length;
            }
            return (paths ?? Array.Empty<string>()).Where(p =>
                {
                    string file = Key(FileName(p));
                    return want.Any(w => file.Contains(w)) && !avoid.Any(a => file.Contains(a));
                })
                .OrderBy(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(p => IsRealistic(p) ? 0 : 1)
                .ThenBy(Rank)
                .ThenBy(p => p, StringComparer.Ordinal).ToList();
        }

        private static int WantRank(string path, StyleSlot slot)
        {
            string file = Key(FileName(path));
            string[] want = Models[slot].Want;
            for (int i = 0; i < want.Length; i++)
                if (file.Contains(want[i])) return i;
            return want.Length;
        }

        /// <summary>
        /// Animations à part du modèle du héros (Mixamo livre le personnage et chaque animation dans des fichiers séparés) : les autres FBX
        /// rangés dans le même dossier que lui, dans l'ordre ordinal.
        /// </summary>
        public static IReadOnlyList<string> Companions(string hero, IEnumerable<string> paths)
        {
            if (string.IsNullOrEmpty(hero)) return Array.Empty<string>();
            string folder = Folder(hero);
            return (paths ?? Array.Empty<string>())
                .Where(p => p != null && p != hero && p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) && Folder(p) == folder)
                .OrderBy(p => p, StringComparer.Ordinal).ToList();
        }

        /// <summary>Nom d'une animation tirée d'un fichier : le nom du fichier sans extension (« Sword And Shield Idle »).</summary>
        public static string ClipName(string path)
        {
            string file = FileName(path);
            int dot = file.LastIndexOf('.');
            return dot > 0 ? file.Substring(0, dot) : file;
        }

        /// <summary>
        /// Couche de sol d'une texture : seulement les textures d'un dossier nommé « Ground » ; roche ou herbe d'après le nom, terre sinon.
        /// Null : ce n'est pas une texture de sol.
        /// </summary>
        public static StyleGround? GroundOf(string path)
        {
            if (!Segments(path).Any(s => s == "ground")) return null;
            string file = Key(FileName(path));
            if (RockGround.Any(w => file.Contains(w))) return StyleGround.Rock;
            if (GrassGround.Any(w => file.Contains(w))) return StyleGround.Grass;
            return StyleGround.Soil;
        }

        /// <summary>Carte de relief (« _nor_gl_ », « _normal ») : un mot du nom de fichier.</summary>
        public static bool IsNormalMap(string path) => Words(path).Any(w => w == "nor" || w == "normal" || w == "nrm" || w == "nm");

        /// <summary>Carte de couleur : ni relief, ni rugosité, ni occlusion, ni hauteur, ni transparence.</summary>
        public static bool IsColorMap(string path) =>
            !IsNormalMap(path) && !Words(path).Any(w => w == "rough" || w == "roughness" || w == "ao" || w == "disp" || w == "height" || w == "arm"
                || w == "spec" || w == "specular" || w == "metal" || w == "metallic" || w == "gloss" || w == "alpha" || w == "opacity" || w == "mask"
                || w == "glow" || w == "emission" || w == "emissive" || w == "translucency" || w == "transmission" || w == "opass");

        /// <summary>Carte de transparence (« _alpha_ », « _opacity », « _opass » du moulin de Sketchfab).</summary>
        public static bool IsAlphaMap(string path) => Words(path).Any(w => w == "alpha" || w == "opacity" || w == "opass");

        /// <summary>Texture de couleur (ou de relief) choisie pour une couche de sol, ou null ; ordre ordinal des chemins.</summary>
        public static string? GroundTexture(IEnumerable<string> paths, StyleGround ground, bool normal) =>
            (paths ?? Array.Empty<string>()).Where(p => GroundOf(p) == ground && (normal ? IsNormalMap(p) : IsColorMap(p)))
                .OrderBy(p => p, StringComparer.Ordinal).FirstOrDefault();

        /// <summary>Ciel : une image HDR (.hdr ou .exr) dont le nom ou le dossier parle de ciel (« …_puresky_1k.hdr »).</summary>
        public static bool IsSky(string path)
        {
            string p = path ?? string.Empty;
            bool hdr = p.EndsWith(".hdr", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".exr", StringComparison.OrdinalIgnoreCase);
            return hdr && (Key(FileName(p)).Contains("sky") || Segments(p).Any(s => s == "sky"));
        }

        /// <summary>Premier ciel dans l'ordre ordinal, ou null.</summary>
        public static string? Sky(IEnumerable<string> paths) =>
            (paths ?? Array.Empty<string>()).Where(IsSky).OrderBy(p => p, StringComparer.Ordinal).FirstOrDefault();

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
                hash = unchecked(hash * 16777619);
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
                        ?? names.FirstOrDefault(n => HasWord(n, new[] { word }));
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
        public static bool Loops(string clip) =>
            new[] { StyleMotion.Idle, StyleMotion.Walk, StyleMotion.Run }.Any(m => HasWord(clip, Clips[m]))
                && !HasWord(clip, Clips[StyleMotion.Death]) && !HasWord(clip, Clips[StyleMotion.Attack]);

        /// <summary>
        /// Un mot du nom commence-t-il par l'un des mots cherchés ? Les mots sont coupés aux signes et aux majuscules (« Run_swordAttack » →
        /// run, sword, attack). Essai du 26 octobre 2026 : « Gardien Idle » contient « die » au milieu d'un mot, et ne doit pas passer pour
        /// une mort.
        /// </summary>
        private static bool HasWord(string clip, string[] wanted) =>
            Regex.Matches(clip ?? string.Empty, "[A-Z]?[a-z]+|[A-Z]+(?![a-z])").Cast<Match>().Select(m => m.Value.ToLowerInvariant())
                .Any(w => wanted.Any(x => w.StartsWith(x, StringComparison.Ordinal)));

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
        /// Échelle d'une plante : sa taille réelle si elle est plausible (entre 2 cm et 2 m : les modèles réalistes sont à l'échelle du
        /// mètre ; la mousse ne fait que quelques centimètres), sinon la hauteur demandée.
        /// </summary>
        public static double NaturalScale(double modelHeight, double height) =>
            modelHeight >= 0.02 && modelHeight <= 2.0 ? 1 : Scale(modelHeight, height);

        /// <summary>Modèle exporté par Poly Haven : nom de fichier terminé par sa résolution (« rock_07_1k »).</summary>
        public static bool IsPolyHavenName(string model) => Regex.IsMatch(model ?? string.Empty, "_(1|2|4|8|16)k$", RegexOptions.IgnoreCase);

        /// <summary>
        /// Pièces à garder d'un modèle de Poly Haven pour l'élément <paramref name="id"/> (essai de Damien du 26 octobre 2026 : des rangées
        /// de pissenlits et des arbustes alignés). Poly Haven livre chaque modèle en lot de variantes posées côte à côte
        /// (« dandelion_01_a_LOD0 », « dandelion_01_b_LOD0 »…), chacune avec ses niveaux de détail (« _LOD1 »…) : on garde une seule
        /// variante, choisie d'après l'identifiant, à son meilleur niveau de détail. Null : tout garder (pas un modèle de Poly Haven, ou
        /// une seule pièce).
        /// </summary>
        public static IReadOnlyList<int>? KeepParts(string model, IReadOnlyList<string> parts, string id)
        {
            if (!IsPolyHavenName(model) || parts == null || parts.Count < 2) return null;
            var named = parts.Select((p, i) => (Index: i, Variant: Regex.Replace(p ?? string.Empty, "_LOD[0-9]+$", "", RegexOptions.IgnoreCase),
                Lod: Lod(p))).ToList();
            List<string> variants = named.Select(n => n.Variant).Distinct().OrderBy(v => v, StringComparer.Ordinal).ToList();
            string chosen = variants[Math.Max(0, Variant((id ?? string.Empty) + "/variante", variants.Count))];
            int best = named.Where(n => n.Variant == chosen).Min(n => n.Lod);
            return named.Where(n => n.Variant == chosen && n.Lod == best).Select(n => n.Index).ToList();
        }

        private static int Lod(string? part)
        {
            Match m = Regex.Match(part ?? string.Empty, "_LOD([0-9]+)$", RegexOptions.IgnoreCase);
            return m.Success && int.TryParse(m.Groups[1].Value, out int lod) ? lod : 0;
        }

        /// <summary>
        /// Couches du sol en (<paramref name="x"/>, <paramref name="z"/>) : la roche autour des endroits rocheux, la terre autour des arbres
        /// et par taches (bruit fixe, sans hasard), l'herbe ailleurs. La roche l'emporte, puis la terre ; la somme fait 1.
        /// </summary>
        public static StyleMix GroundMix(double x, double z, IReadOnlyList<StyleSpot> spots)
        {
            double rock = 0, soil = 0;
            foreach (StyleSpot spot in spots ?? Array.Empty<StyleSpot>())
            {
                double dx = x - spot.X, dz = z - spot.Z;
                double outside = Math.Max(0, Math.Sqrt(dx * dx + dz * dz) - Math.Max(0, spot.Radius));
                double w = Math.Max(0, 1 - outside / SpotFade);
                if (spot.Ground == StyleGround.Rock) rock = Math.Max(rock, w);
                else if (spot.Ground == StyleGround.Soil) soil = Math.Max(soil, w * 0.85);
            }
            // Taches de terre dans l'herbe : bruit de valeur lissé, sur une grille de PatchSize mètres.
            double patch = Math.Min(1, Math.Max(0, (Noise(x / PatchSize, z / PatchSize) - 0.58) * 2.5)) * 0.7;
            soil = Math.Max(soil, patch);
            double r = Math.Min(1, rock), s = (1 - r) * Math.Min(1, soil);
            return new StyleMix(s, r, Math.Max(0, 1 - r - s));
        }

        /// <summary>
        /// Places des arbres de la lisière, hors de l'aire de jeu (simple décor : le moteur ne les connaît pas) : dans la bande de
        /// <paramref name="margin"/> mètres autour de l'aire, à plus de <paramref name="gap"/> mètres de son bord et à plus de
        /// <paramref name="spacing"/> mètres l'un de l'autre. Suite fixe : les mêmes places à chaque lancement ; au plus
        /// <paramref name="count"/> (moins si la bande est pleine).
        /// </summary>
        public static IReadOnlyList<(double X, double Z)> Forest(double minX, double minZ, double maxX, double maxZ, double margin, int count,
            double gap = 4, double spacing = 3.5)
        {
            var spots = new List<(double X, double Z)>();
            if (count <= 0 || margin <= gap) return spots;
            uint seed = 2463534242;
            double Next()
            {
                seed ^= seed << 13;
                seed ^= seed >> 17;
                seed ^= seed << 5;
                return (seed >> 8) / 16777216.0;
            }
            for (int attempt = 0; attempt < count * 30 && spots.Count < count; attempt++)
            {
                double x = minX - margin + (maxX - minX + 2 * margin) * Next();
                double z = minZ - margin + (maxZ - minZ + 2 * margin) * Next();
                if (x > minX - gap && x < maxX + gap && z > minZ - gap && z < maxZ + gap) continue;
                if (spots.Any(s => (s.X - x) * (s.X - x) + (s.Z - z) * (s.Z - z) < spacing * spacing)) continue;
                spots.Add((x, z));
            }
            return spots;
        }

        /// <summary>Bruit de valeur lissé dans [0, 1], fixe (empreinte entière des coins de la case).</summary>
        public static double Noise(double x, double z)
        {
            double fx = Math.Floor(x), fz = Math.Floor(z);
            long ix = (long)fx, iz = (long)fz;
            double tx = Smooth(x - fx), tz = Smooth(z - fz);
            double a = Corner(ix, iz), b = Corner(ix + 1, iz), c = Corner(ix, iz + 1), d = Corner(ix + 1, iz + 1);
            return (a + (b - a) * tx) + ((c + (d - c) * tx) - (a + (b - a) * tx)) * tz;
        }

        private static double Smooth(double t) => t * t * (3 - 2 * t);

        private static double Corner(long x, long z)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)z * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215.0;
            }
        }

        /// <summary>
        /// Ce qu'habille un combattant, d'après son identifiant (vies suivantes comprises : « loup1_v3 ») : le joueur en chevalier, les loups
        /// et les sangliers ; depuis l'essai réaliste (demande de Damien du 26 octobre 2026), les autres (archer, rôdeur, Gardien) prennent le
        /// personnage de leur dossier Mixamo s'il existe (<see cref="CharacterOf"/>) ; sans lui, ils gardent leur forme simple.
        /// </summary>
        public static StyleSlot ForCombatant(string id, bool player)
        {
            if (player) return StyleSlot.Hero;
            string key = Key(id);
            if (key.StartsWith("loup", StringComparison.Ordinal)) return StyleSlot.Wolf;
            if (key.StartsWith("sanglier", StringComparison.Ordinal)) return StyleSlot.Boar;
            return StyleSlot.Character;
        }

        /// <summary>
        /// Personnage d'un PNJ ou d'un ennemi : le premier mot de son identifiant, sans le préfixe « npc » ni les chiffres (« npc.odile » →
        /// odile, « gardien_eveille » → gardien, « archer_v2 » → archer). C'est le nom du dossier du personnage dans <c>Assets/Style/Mixamo</c>,
        /// réduit. Vide si l'identifiant n'a aucun mot.
        /// </summary>
        public static string CharacterOf(string id)
        {
            foreach (string word in Regex.Split(id ?? string.Empty, "[^A-Za-z]+"))
            {
                string w = Key(word);
                if (w.Length == 0 || w == "npc" || w == "pnj") continue;
                return w;
            }
            return string.Empty;
        }

        /// <summary>
        /// Personnages rangés chacun dans un dossier de <c>Mixamo</c> (« Assets/Style/Mixamo/Archer/Archer Idle.fbx ») : nom du dossier
        /// réduit → ses FBX dans l'ordre ordinal. Le héros, posé à la racine de <c>Mixamo</c>, et les dossiers « Textures » n'en sont pas.
        /// </summary>
        public static IReadOnlyDictionary<string, IReadOnlyList<string>> Characters(IEnumerable<string> paths)
        {
            var found = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string path in paths ?? Array.Empty<string>())
            {
                if (path == null || !path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                string[] parts = path.Replace('\\', '/').Split('/');
                if (parts.Length < 3 || Key(parts[parts.Length - 3]) != "mixamo") continue;
                string name = Key(parts[parts.Length - 2]);
                if (name.Length == 0 || name == "textures") continue;
                if (!found.TryGetValue(name, out List<string>? list)) found[name] = list = new List<string>();
                list.Add(path);
            }
            return found.ToDictionary(e => e.Key, e => (IReadOnlyList<string>)e.Value.OrderBy(p => p, StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
        }

        /// <summary>
        /// Texture d'un matériau de Mixamo (couleur, ou relief si <paramref name="normal"/>) : celle dont le nom commence par celui du matériau
        /// (« Body_MAT1 » → Body_DIFF.jpg, « maria_M1 » → maria_diffuse.png), sinon la seule texture de ce genre s'il n'y en a qu'une
        /// (« warrok_MAT » → bear_diffuse.png) ; null sinon.
        /// </summary>
        public static string? MaterialTexture(string material, IEnumerable<string> textures, bool normal)
        {
            List<string> kind = (textures ?? Array.Empty<string>()).Where(t => t != null && (normal ? IsNormalMap(t) : IsColorMap(t)))
                .OrderBy(t => t, StringComparer.Ordinal).ToList();
            string stem = Regex.Replace(Key(material), "(mat|m)[0-9]*$", "");
            if (stem.Length > 0 && kind.FirstOrDefault(t => Key(FileName(t)).StartsWith(stem, StringComparison.Ordinal)) is string named) return named;
            return kind.Count == 1 ? kind[0] : null;
        }

        /// <summary>
        /// Matière d'une texture de bâtiment : seulement les textures d'un dossier nommé « Surfaces » ; crépi, tuiles, planches, pavés ou
        /// pierre d'après le nom (dans cet ordre : « plastered_stone_wall » est du crépi, « cobblestone » des pavés). Null sinon.
        /// </summary>
        public static StyleSurface? SurfaceOf(string path)
        {
            if (!Segments(path).Any(s => s == "surfaces")) return null;
            string file = Key(FileName(path));
            if (file.Contains("bark")) return StyleSurface.Bark;
            if (file.Contains("plaster")) return StyleSurface.Plaster;
            if (file.Contains("roof") || file.Contains("tile")) return StyleSurface.Roof;
            if (file.Contains("plank") || file.Contains("wood") || file.Contains("timber")) return StyleSurface.Wood;
            if (file.Contains("cobble") || file.Contains("paving") || file.Contains("pavement")) return StyleSurface.Cobble;
            if (file.Contains("stone") || file.Contains("brick") || file.Contains("wall") || file.Contains("rock")) return StyleSurface.Stone;
            return null;
        }

        /// <summary>Texture de couleur (ou de relief) d'une matière de bâtiment, ou null ; ordre ordinal des chemins.</summary>
        public static string? SurfaceTexture(IEnumerable<string> paths, StyleSurface surface, bool normal) =>
            (paths ?? Array.Empty<string>()).Where(p => SurfaceOf(p) == surface && (normal ? IsNormalMap(p) : IsColorMap(p)))
                .OrderBy(p => p, StringComparer.Ordinal).FirstOrDefault();

        /// <summary>
        /// Matière d'un obstacle bâti : écorce pour les murs et les portes (palissade de rondins, choix de Damien du 27 octobre 2026),
        /// planches pour les piliers ; null pour les autres (le moulin a ses propres matières : crépi, tuiles, planches ; la rivière son eau).
        /// </summary>
        public static StyleSurface? SurfaceFor(string id)
        {
            if (FenceFor(id) == StyleFence.Palisade) return StyleSurface.Bark;
            if (Key(id).StartsWith("pilier", StringComparison.Ordinal)) return StyleSurface.Wood;
            return null;
        }

        /// <summary>
        /// Clôture qu'habille un obstacle en longueur : palissade de rondins pour les murs et les portes (« mur_nord », « porte_moulin ») ;
        /// essai de Damien du 27 octobre 2026 : « le mur à côté de l'archer ne me plaît pas, peut-être mettre quelque chose d'autre » ; haie
        /// d'arbustes pour les haies (« haie_ouest », jusque-là un bloc de pierre). Aucune pour les autres.
        /// </summary>
        public static StyleFence FenceFor(string id)
        {
            string key = Key(id);
            if (key.StartsWith("mur", StringComparison.Ordinal) || key.StartsWith("porte", StringComparison.Ordinal)
                || key.StartsWith("palissade", StringComparison.Ordinal)) return StyleFence.Palisade;
            if (key.StartsWith("haie", StringComparison.Ordinal)) return StyleFence.Hedge;
            return StyleFence.None;
        }

        /// <summary>Rayon (m) d'un rondin de palissade.</summary>
        public const double LogRadius = 0.16;

        /// <summary>
        /// Rondins d'une palissade sur l'emprise rectangulaire [minX, maxX] × [minZ, maxZ] : alignés le long du grand côté, jointifs, en deux
        /// rangs décalés si l'emprise est assez épaisse (sinon un seul, au milieu) ; hauteurs de 2,6 à 3,1 m, fixes d'après
        /// l'identifiant (les mêmes à chaque lancement). Le moteur garde son emprise : la palissade n'en sort pas.
        /// </summary>
        public static IReadOnlyList<StyleLog> Palisade(double minX, double minZ, double maxX, double maxZ, string id)
        {
            var logs = new List<StyleLog>();
            bool alongX = maxX - minX >= maxZ - minZ;
            double length = alongX ? maxX - minX : maxZ - minZ, thickness = alongX ? maxZ - minZ : maxX - minX;
            if (length < 2 * LogRadius || thickness <= 0) return logs;
            double step = 2 * LogRadius * 0.92;
            int count = (int)Math.Floor((length - 2 * LogRadius) / step) + 1;
            double start = (alongX ? minX : minZ) + (length - (count - 1) * step) / 2;
            double middle = alongX ? (minZ + maxZ) / 2 : (minX + maxX) / 2;
            bool two = thickness >= 4 * LogRadius;
            double offset = two ? Math.Min(0.14, thickness / 2 - LogRadius) : 0;
            for (int row = 0; row < (two ? 2 : 1); row++)
                for (int i = 0; i < count - (row == 1 ? 1 : 0); i++)
                {
                    double along = start + i * step + (row == 1 ? step / 2 : 0);
                    double across = middle + (row == 0 ? offset : -offset);
                    double height = 2.6 + 0.005 * Variant(id + "/" + row + "/" + i, 101);
                    logs.Add(alongX ? new StyleLog(along, across, LogRadius, height) : new StyleLog(across, along, LogRadius, height));
                }
            return logs;
        }

        /// <summary>
        /// Places régulières le long du grand côté de l'emprise [minX, maxX] × [minZ, maxZ], au milieu de sa largeur, tous les
        /// <paramref name="spacing"/> mètres environ (au moins une ; les bouts à une demi-place du bord) : les arbustes d'une haie.
        /// </summary>
        public static IReadOnlyList<(double X, double Z)> Row(double minX, double minZ, double maxX, double maxZ, double spacing)
        {
            bool alongX = maxX - minX >= maxZ - minZ;
            double length = alongX ? maxX - minX : maxZ - minZ;
            int count = Math.Max(1, (int)Math.Round(length / Math.Max(0.1, spacing)));
            double step = length / count;
            var spots = new List<(double X, double Z)>();
            for (int i = 0; i < count; i++)
            {
                double along = (alongX ? minX : minZ) + step * (i + 0.5);
                spots.Add(alongX ? (along, (minZ + maxZ) / 2) : ((minX + maxX) / 2, along));
            }
            return spots;
        }

        /// <summary>
        /// Distance (m) d'un point au bord d'un polygone convexe (sommets dans un sens ou dans l'autre) : positive dedans, négative dehors
        /// (la plus petite des distances signées aux côtés). <see cref="double.NegativeInfinity"/> pour un polygone de moins de 3 sommets.
        /// </summary>
        public static double InsideDistance(double x, double z, IReadOnlyList<(double X, double Z)> polygon)
        {
            if (polygon == null || polygon.Count < 3) return double.NegativeInfinity;
            double area = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                area += a.X * b.Z - b.X * a.Z;
            }
            double sign = area >= 0 ? 1 : -1;
            double inside = double.PositiveInfinity;
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                double ex = b.X - a.X, ez = b.Z - a.Z, len = Math.Sqrt(ex * ex + ez * ez);
                if (len < 1e-9) continue;
                // Sens trigonométrique (aire positive) : l'intérieur est à gauche de chaque côté.
                double d = sign * (ex * (z - a.Z) - ez * (x - a.X)) / len;
                inside = Math.Min(inside, d);
            }
            return double.IsPositiveInfinity(inside) ? double.NegativeInfinity : inside;
        }

        /// <summary>
        /// Profondeur (m, positive) du lit de la rivière à <paramref name="inside"/> mètres en dedans de son bord : nulle au bord et dehors,
        /// puis la berge descend en douceur jusqu'à <see cref="RiverBed"/> sur <see cref="RiverBank"/> mètres.
        /// </summary>
        public static double ChannelDepth(double inside) =>
            inside <= 0 ? 0 : RiverBed * Smooth(Math.Min(1, inside / RiverBank));

        /// <summary>Demi-largeur (m) de la bande de terre mouillée autour du bord de la rivière.</summary>
        public const double RiverShore = 0.8;

        /// <summary>
        /// Couches du sol près de la rivière (<paramref name="inside"/> : distance en dedans de son bord, négative dehors) : la terre gagne
        /// sur la berge et le lit (pas d'herbe sous l'eau), pleine à <see cref="RiverShore"/> mètres en dedans, sans effet à
        /// <see cref="RiverShore"/> mètres dehors ; la somme reste 1.
        /// </summary>
        public static StyleMix RiverMix(StyleMix mix, double inside)
        {
            double wet = Math.Max(0, Math.Min(1, (inside + RiverShore) / (2 * RiverShore)));
            return new StyleMix(mix.Soil * (1 - wet) + wet, mix.Rock * (1 - wet), mix.Grass * (1 - wet));
        }

        /// <summary>Angle (degrés, 0 vers +z, 90 vers +x, comme Unity) pour regarder de (fromX, fromZ) vers (toX, toZ).</summary>
        public static double FaceYaw(double fromX, double fromZ, double toX, double toZ) =>
            Math.Atan2(toX - fromX, toZ - fromZ) * 180 / Math.PI;

        /// <summary>
        /// Angle <paramref name="current"/> tourné vers <paramref name="target"/> d'au plus <paramref name="maxStep"/> degrés, par le plus
        /// court chemin ; résultat dans ]-180, 180].
        /// </summary>
        public static double Turn(double current, double target, double maxStep)
        {
            double delta = Wrap(target - current);
            double step = Math.Max(-Math.Abs(maxStep), Math.Min(Math.Abs(maxStep), delta));
            return Wrap(current + step);
        }

        private static double Wrap(double angle)
        {
            double a = angle % 360;
            if (a <= -180) a += 360;
            if (a > 180) a -= 360;
            return a;
        }

        /// <summary>
        /// Pont fait d'un ponton modulaire (Poly Haven « modular_wooden_pier » : planches, pieux et tronçons) : la pièce montrée, le premier
        /// tronçon dans l'ordre ordinal (« section_01 », tablier posé sur ses pieux), sinon la première pièce ; -1 s'il n'y a rien.
        /// </summary>
        public static int BridgeSection(IReadOnlyList<string> parts)
        {
            if (parts == null || parts.Count == 0) return -1;
            var sections = parts.Select((p, i) => (Name: p ?? string.Empty, Index: i)).Where(p => Key(p.Name).Contains("section"))
                .OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
            return sections.Count > 0 ? sections[0].Index : 0;
        }

        /// <summary>
        /// Pièces d'un modèle livré en plusieurs exemplaires posés côte à côte (le moulin de Sketchfab : le moulin détaillé et sa version
        /// simplifiée, à une vingtaine de mètres) : la pièce la plus détaillée (<paramref name="detail"/> le plus grand, la première à égalité)
        /// et, de proche en proche, celles dont la boîte touche une pièce gardée (à <paramref name="margin"/> mètres près). Indices dans
        /// l'ordre ; vide sans pièce.
        /// </summary>
        public static IReadOnlyList<int> Cluster(IReadOnlyList<(double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ)> boxes,
            IReadOnlyList<int> detail, double margin = 0.05)
        {
            if (boxes == null || boxes.Count == 0) return Array.Empty<int>();
            int first = 0;
            for (int i = 1; i < boxes.Count; i++)
                if (detail != null && i < detail.Count && detail[i] > (first < detail.Count ? detail[first] : 0)) first = i;
            var kept = new HashSet<int> { first };
            bool grew = true;
            while (grew)
            {
                grew = false;
                for (int i = 0; i < boxes.Count; i++)
                {
                    if (kept.Contains(i)) continue;
                    var b = boxes[i];
                    if (!kept.Any(k => boxes[k].MinX - margin <= b.MaxX && b.MinX <= boxes[k].MaxX + margin && boxes[k].MinY - margin <= b.MaxY
                        && b.MinY <= boxes[k].MaxY + margin && boxes[k].MinZ - margin <= b.MaxZ && b.MinZ <= boxes[k].MaxZ + margin)) continue;
                    kept.Add(i);
                    grew = true;
                }
            }
            return kept.OrderBy(i => i).ToList();
        }

        /// <summary>Pièce qui donne la hauteur du tablier du ponton (« planks ») : le dessus des planches ; -1 s'il n'y en a pas.</summary>
        public static int BridgeDeck(IReadOnlyList<string> parts)
        {
            if (parts == null) return -1;
            for (int i = 0; i < parts.Count; i++)
                if (Key(parts[i]).Contains("plank")) return i;
            return -1;
        }

        /// <summary>Ce qu'habille un obstacle du décor : arbres, blocs de la carrière et pierres du Seuil. Null : il garde sa forme.</summary>
        public static StyleSlot? ForObstacle(string id)
        {
            string key = Key(id);
            if (key.StartsWith("arbre", StringComparison.Ordinal)) return StyleSlot.Tree;
            if (key.StartsWith("bloc", StringComparison.Ordinal) || key.StartsWith("pierreseuil", StringComparison.Ordinal)) return StyleSlot.Rock;
            return null;
        }

        /// <summary>
        /// Couche de sol qu'appelle un obstacle autour de lui : la roche pour les blocs, les pierres du Seuil et les piliers, la terre pour
        /// les arbres, le moulin et les clôtures (palissades, haies, portes : terre battue au pied) ; null pour les autres (rivière…).
        /// </summary>
        public static StyleGround? GroundFor(string id)
        {
            string key = Key(id);
            if (ForObstacle(id) == StyleSlot.Rock || key.StartsWith("pilier", StringComparison.Ordinal)) return StyleGround.Rock;
            if (key.StartsWith("arbre", StringComparison.Ordinal) || key.StartsWith("moulin", StringComparison.Ordinal) || FenceFor(id) != StyleFence.None)
                return StyleGround.Soil;
            return null;
        }

        private static string FileName(string path)
        {
            string p = (path ?? string.Empty).Replace('\\', '/');
            int slash = p.LastIndexOf('/');
            return slash >= 0 ? p.Substring(slash + 1) : p;
        }

        private static string Folder(string path)
        {
            string p = (path ?? string.Empty).Replace('\\', '/');
            int slash = p.LastIndexOf('/');
            return slash >= 0 ? p.Substring(0, slash) : string.Empty;
        }

        /// <summary>Dossiers du chemin, réduits (« Assets/Style/PolyHaven/Ground/x.jpg » → assets, style, polyhaven, ground).</summary>
        private static IEnumerable<string> Segments(string path)
        {
            string[] parts = (path ?? string.Empty).Replace('\\', '/').Split('/');
            return parts.Take(Math.Max(0, parts.Length - 1)).Select(Key);
        }

        /// <summary>Mots du nom de fichier, coupés aux signes (« forest_ground_04_nor_gl_1k.jpg » → forest, ground, 04, nor, gl, 1k, jpg).</summary>
        private static IEnumerable<string> Words(string path) =>
            FileName(path).ToLowerInvariant().Split(new[] { '_', '-', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries);
    }
}
