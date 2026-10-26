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

        /// <summary>Essai réaliste : bois mort (souches, troncs couchés), posé dans la forêt autour de l'aire.</summary>
        Debris = 6,
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

    /// <summary>Emprise au sol (rectangle) d'une pièce d'un modèle, pour reconnaître un lot de pièces séparées.</summary>
    public readonly struct StyleBox
    {
        public StyleBox(double minX, double minZ, double maxX, double maxZ)
        {
            MinX = Math.Min(minX, maxX);
            MinZ = Math.Min(minZ, maxZ);
            MaxX = Math.Max(minX, maxX);
            MaxZ = Math.Max(minZ, maxZ);
        }

        public double MinX { get; }
        public double MinZ { get; }
        public double MaxX { get; }
        public double MaxZ { get; }
        public double Area => (MaxX - MinX) * (MaxZ - MinZ);
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
            [StyleSlot.Rock] = (new[] { "rock", "boulder", "stone" }, new[] { "path", "pebble", "small", "tiny" }),
            [StyleSlot.Plant] = (new[] { "bush", "fern", "grass", "flower", "plant", "clover", "shrub", "moss", "nettle", "celandine", "dandelion", "weed" },
                new[] { "big", "tall", "tree", "rock" }),
            [StyleSlot.Debris] = (new[] { "stump", "log", "trunk", "root", "branch" }, new[] { "quiver" }),
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

        /// <summary>Sources réalistes : dossiers où sont rangés les fichiers de Poly Haven et de Mixamo.</summary>
        private static readonly string[] Realistic = { "polyhaven", "mixamo" };

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
        /// Modèles retenus pour le catalogue (au plus <paramref name="max"/>) : les candidats dans leur ordre ; pour les rochers, seulement
        /// les réalistes s'il y en a (un rocher dessiné détonne sur le sol réaliste ; les plantes, elles, mélangent les deux).
        /// </summary>
        public static IReadOnlyList<string> ForCatalog(IEnumerable<string> paths, StyleSlot slot, int max)
        {
            IReadOnlyList<string> all = Candidates(paths, slot);
            if (slot == StyleSlot.Rock && all.Any(IsRealistic)) all = all.Where(IsRealistic).ToList();
            return all.Take(Math.Max(0, max)).ToList();
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
        public static bool IsNormalMap(string path) => Words(path).Any(w => w == "nor" || w == "normal" || w == "nrm");

        /// <summary>Carte de couleur : ni relief, ni rugosité, ni occlusion, ni hauteur, ni transparence.</summary>
        public static bool IsColorMap(string path) =>
            !IsNormalMap(path) && !Words(path).Any(w => w == "rough" || w == "roughness" || w == "ao" || w == "disp" || w == "height" || w == "arm"
                || w == "spec" || w == "metal" || w == "alpha" || w == "opacity" || w == "mask");

        /// <summary>Carte de transparence (« _alpha_ », « _opacity »).</summary>
        public static bool IsAlphaMap(string path) => Words(path).Any(w => w == "alpha" || w == "opacity");

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
        /// Échelle d'une plante : sa taille réelle si elle est plausible (entre 5 cm et 2 m : les modèles réalistes sont à l'échelle du
        /// mètre), sinon la hauteur demandée.
        /// </summary>
        public static double NaturalScale(double modelHeight, double height) =>
            modelHeight >= 0.05 && modelHeight <= 2.0 ? 1 : Scale(modelHeight, height);

        /// <summary>
        /// Un modèle fait de plusieurs pièces posées côte à côte (un lot de rochers, des touffes d'herbe) plutôt que d'un seul objet (un
        /// tronc et son feuillage se recouvrent) : au moins deux pièces, et aucune paire dont le recouvrement au sol dépasse le cinquième de
        /// la plus petite.
        /// </summary>
        public static bool IsCollection(IReadOnlyList<StyleBox> parts)
        {
            if (parts == null || parts.Count < 2) return false;
            for (int i = 0; i < parts.Count; i++)
                for (int j = i + 1; j < parts.Count; j++)
                {
                    StyleBox a = parts[i], b = parts[j];
                    double w = Math.Min(a.MaxX, b.MaxX) - Math.Max(a.MinX, b.MinX);
                    double d = Math.Min(a.MaxZ, b.MaxZ) - Math.Max(a.MinZ, b.MinZ);
                    if (w < 0 || d < 0) continue;   // à l'écart (des bords qui se touchent ne se recouvrent pas)
                    double smallest = Math.Min(a.Area, b.Area);
                    if (smallest <= 1e-9 || w * d > smallest * 0.2) return false;   // une pièce plate posée sur l'autre : un seul objet
                }
            return true;
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

        /// <summary>
        /// Couche de sol qu'appelle un obstacle autour de lui : la roche pour les blocs, les pierres du Seuil, les murs et les piliers, la
        /// terre pour les arbres et le moulin ; null pour les autres (rivière…).
        /// </summary>
        public static StyleGround? GroundFor(string id)
        {
            string key = Key(id);
            if (ForObstacle(id) == StyleSlot.Rock || key.StartsWith("mur", StringComparison.Ordinal) || key.StartsWith("pilier", StringComparison.Ordinal))
                return StyleGround.Rock;
            if (key.StartsWith("arbre", StringComparison.Ordinal) || key.StartsWith("moulin", StringComparison.Ordinal)) return StyleGround.Soil;
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
