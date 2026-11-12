using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Story;
using EdS.Domain.Space;

namespace EdS.Runtime
{
    /// <summary>V2.25.4 : les quatorze essences de l'atelier (dossier « Vegetation » : 6 arbres chacune, jeune, adulte a à d, vieux).</summary>
    public enum TreeEssence
    {
        Chene = 0,
        Hetre = 1,
        Chataignier = 2,
        Bouleau = 3,
        Epicea = 4,
        Pin = 5,
        Saule = 6,
        Aulne = 7,
        Cypres = 8,
        Pommier = 9,
        Poirier = 10,
        Mort = 11,
        Foudroye = 12,
        Corrompu = 13,
    }

    /// <summary>V2.25.4 : d'où vient un arbre posé par la vue (l'essence et l'âge en dépendent).</summary>
    public enum TreeSource
    {
        /// <summary>Obstacle « arbre… » de la zone (le moteur connaît son tronc).</summary>
        Obstacle = 0,

        /// <summary>Arbre d'un bois (obstacle « bois… », <see cref="TownRules.WoodTrees"/>).</summary>
        Woods = 1,

        /// <summary>Lisière hors de l'aire (<see cref="StyleRules.Forest"/>).</summary>
        Edge = 2,

        /// <summary>Rangée d'un verger (décor seul).</summary>
        Orchard = 3,

        /// <summary>Berge du lac ou de la rivière (décor seul).</summary>
        Bank = 4,

        /// <summary>Autour des pierres du Seuil (décor seul).</summary>
        Seuil = 5,
    }

    /// <summary>V2.25.4 : un arbre de l'atelier à poser : essence, modèle (« chene_adulte_b »), place, lacet (degrés), échelle, origine.</summary>
    public readonly struct TreeSpot
    {
        public TreeSpot(TreeEssence essence, string model, double x, double z, double yaw, double scale, TreeSource source)
        {
            Essence = essence;
            Model = model;
            X = x;
            Z = z;
            Yaw = yaw;
            Scale = scale;
            Source = source;
        }

        public TreeEssence Essence { get; }
        public string Model { get; }
        public double X { get; }
        public double Z { get; }

        /// <summary>Degrés, comme <c>Quaternion.Euler(0, yaw, 0)</c> d'Unity.</summary>
        public double Yaw { get; }
        public double Scale { get; }
        public TreeSource Source { get; }
    }

    /// <summary>
    /// V2.25.4 (« arbres de l'atelier ») : règles, sans Unity, des arbres faits à l'atelier qui remplacent ceux téléchargés (Quaternius,
    /// Sketchfab) : essence selon le lieu, âge et variante, lacet et légère échelle tirés de la place (même place, même arbre, à chaque
    /// lancement et sur chaque machine : empreinte entière de la position au centimètre, sans hasard), places des arbres de décor (vergers,
    /// berges, abords du Seuil) hors des routes, des points sûrs, des portes et des obstacles, distances des niveaux de détail selon la
    /// machine, colonne et ligne de l'imposteur (8 angles × 4 saisons), saison du monde. Affichage seulement : le moteur, les zones et les
    /// journaux ne changent pas.
    /// </summary>
    public static class AtelierTrees
    {
        /// <summary>Dossier des arbres dans le projet Unity (un dossier par essence, ses textures dans « textures »).</summary>
        public const string Folder = "Assets/EdS/Modeles/Arbres";

        /// <summary>Nom du dossier et préfixe des fichiers d'une essence (« chene », « epicea »…).</summary>
        public static string Name(TreeEssence essence) => Names[(int)essence];

        private static readonly string[] Names =
            { "chene", "hetre", "chataignier", "bouleau", "epicea", "pin", "saule", "aulne", "cypres", "pommier", "poirier", "mort", "foudroye", "corrompu" };

        /// <summary>Les quatorze essences, dans l'ordre de <see cref="TreeEssence"/>.</summary>
        public static IReadOnlyList<TreeEssence> Essences { get; } = Enumerable.Range(0, Names.Length).Select(i => (TreeEssence)i).ToList();

        /// <summary>L'arbre millénaire (livré à part, posé avec la grande carte, V2.32) : dossier et modèle.</summary>
        public const string Millenaire = "millenaire";

        /// <summary>Les six arbres de chaque essence (fin du nom du modèle).</summary>
        public static IReadOnlyList<string> Ages { get; } = new[] { "jeune_a", "adulte_a", "adulte_b", "adulte_c", "adulte_d", "vieux_a" };

        /// <summary>Essence d'un modèle ou d'un dossier (« chene_adulte_a », « Assets/…/Arbres/chene/… ») ; null sinon.</summary>
        public static TreeEssence? EssenceOf(string? name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string file = name!.Replace('\\', '/');
            int slash = file.LastIndexOf('/');
            if (slash >= 0) file = file.Substring(slash + 1);
            for (int i = 0; i < Names.Length; i++)
                if (file == Names[i] || file.StartsWith(Names[i] + "_", StringComparison.Ordinal)) return (TreeEssence)i;
            return null;
        }

        // --- Tirage déterministe -------------------------------------------------------------------------------------------------

        /// <summary>
        /// Empreinte entière de la place (au centimètre) et d'un sel : mélange de splitmix64, sans hasard ni horloge ; la même sur toutes les
        /// machines.
        /// </summary>
        public static uint Seed(double x, double z, uint salt)
        {
            long qx = (long)Math.Round(x * 100.0), qz = (long)Math.Round(z * 100.0);
            unchecked
            {
                ulong h = (ulong)qx * 0x9E3779B97F4A7C15UL ^ (ulong)qz * 0xC2B2AE3D27D4EB4FUL ^ (salt + 0x632BE59BD9B4E019UL);
                h ^= h >> 30;
                h *= 0xBF58476D1CE4E5B9UL;
                h ^= h >> 27;
                h *= 0x94D049BB133111EBUL;
                h ^= h >> 31;
                return (uint)(h >> 16);
            }
        }

        /// <summary>Part dans [0, 1[ tirée de la place (<see cref="Seed"/>).</summary>
        public static double Share(double x, double z, uint salt) => (Seed(x, z, salt) >> 8) / 16777216.0;

        /// <summary>Lacet (degrés, de 0 à 360) tiré de la place.</summary>
        public static double YawAt(double x, double z) => Math.Floor(Share(x, z, 11) * 360.0);

        /// <summary>Légère variation d'échelle (0,9 à 1,1) tirée de la place ; les modèles sont à leur taille réelle.</summary>
        public static double ScaleAt(double x, double z) => 0.9 + 0.2 * Share(x, z, 13);

        // --- Essence selon le lieu -----------------------------------------------------------------------------------------------

        /// <summary>Jusqu'où (m) du lieu du Seuil les arbres sont corrompus.</summary>
        public const double SeuilReach = 8;

        /// <summary>Jusqu'où (m) de l'eau (rivière, lac) un arbre est un arbre de berge (saule, aulne).</summary>
        public const double BankReach = 6;

        private static readonly (TreeEssence Essence, int Weight)[] WolfForest =
            { (TreeEssence.Epicea, 28), (TreeEssence.Pin, 22), (TreeEssence.Hetre, 20), (TreeEssence.Chene, 16), (TreeEssence.Mort, 8), (TreeEssence.Foudroye, 6) };

        private static readonly (TreeEssence, int)[] Grove =
            { (TreeEssence.Hetre, 30), (TreeEssence.Chene, 30), (TreeEssence.Chataignier, 20), (TreeEssence.Bouleau, 20) };

        private static readonly (TreeEssence, int)[] Meadow = { (TreeEssence.Chene, 45), (TreeEssence.Chataignier, 30), (TreeEssence.Bouleau, 25) };
        private static readonly (TreeEssence, int)[] Orchard = { (TreeEssence.Pommier, 60), (TreeEssence.Poirier, 40) };
        private static readonly (TreeEssence, int)[] Banks = { (TreeEssence.Saule, 40), (TreeEssence.Aulne, 60) };

        /// <summary>Essences possibles d'un mélange (pour les tests et la documentation).</summary>
        public static IReadOnlyList<TreeEssence> Mix(string kind) => (kind switch
        {
            "foret" => WolfForest,
            "bosquet" => Grove,
            "verger" => Orchard,
            "berge" => Banks,
            "pre" => Meadow,
            _ => Array.Empty<(TreeEssence, int)>(),
        }).Select(m => m.Item1).ToList();

        private static TreeEssence Pick((TreeEssence Essence, int Weight)[] mix, double x, double z)
        {
            int total = mix.Sum(m => m.Weight), roll = (int)(Seed(x, z, 7) % (uint)total);
            foreach ((TreeEssence essence, int weight) in mix)
            {
                if (roll < weight) return essence;
                roll -= weight;
            }
            return mix[mix.Length - 1].Essence;
        }

        private static double BoxDistance(double x, double z, double minX, double minZ, double maxX, double maxZ)
        {
            double dx = Math.Max(0, Math.Max(minX - x, x - maxX)), dz = Math.Max(0, Math.Max(minZ - z, z - maxZ));
            return Math.Sqrt(dx * dx + dz * dz);
        }

        private static (double MinX, double MinZ, double MaxX, double MaxZ) Box(SpaceObstacle o) =>
            (o.Points.Min(p => p.X.Raw) / 10000.0, o.Points.Min(p => p.Z.Raw) / 10000.0, o.Points.Max(p => p.X.Raw) / 10000.0, o.Points.Max(p => p.Z.Raw) / 10000.0);

        private static bool IsWater(string id) => id.StartsWith("riviere", StringComparison.Ordinal);

        private static bool PlaceIs(MapPlace place, string word) => StyleRules.Key(place.Id).Contains(word);

        /// <summary>Le lieu dont le rectangle contient la place, le premier dans l'ordre de la zone ; la lisière (hors de l'aire) est ramenée au bord.</summary>
        private static MapPlace? PlaceAt(double x, double z, AdventureMap? map)
        {
            if (map == null) return null;
            double cx = Math.Max(map.MinX, Math.Min(map.MaxX, x)), cz = Math.Max(map.MinZ, Math.Min(map.MaxZ, z));
            return map.Places.FirstOrDefault(p => cx >= p.MinX && cx <= p.MaxX && cz >= p.MinZ && cz <= p.MaxZ);
        }

        /// <summary>
        /// Essence d'un arbre selon son lieu : près du Seuil, l'arbre corrompu ; au bord de l'eau, saule ou aulne ; dans la forêt des Loups,
        /// épicéa, pin, hêtre, chêne, arbres morts et foudroyés ; dans un autre bois, hêtre, chêne, châtaignier, bouleau ; dans les vergers,
        /// pommier ou poirier ; près des vignes (aucune dans les Vallons actuels), le cyprès ; ailleurs (prés, village, champs), chêne,
        /// châtaignier ou bouleau. Les arbres des vergers, des berges et du Seuil gardent l'essence de leur rôle.
        /// </summary>
        public static TreeEssence EssenceAt(double x, double z, TreeSource source, AdventureMap? map, IEnumerable<SpaceObstacle> obstacles)
        {
            switch (source)
            {
                case TreeSource.Orchard: return Pick(Orchard, x, z);
                case TreeSource.Bank: return Pick(Banks, x, z);
                case TreeSource.Seuil: return TreeEssence.Corrompu;
            }
            if (map != null && map.Places.Any(p => (PlaceIs(p, "seuil")) && BoxDistance(x, z, p.MinX, p.MinZ, p.MaxX, p.MaxZ) <= SeuilReach))
                return TreeEssence.Corrompu;
            MapPlace? place = PlaceAt(x, z, map);
            bool wolves = place != null && PlaceIs(place, "foret");
            if (source == TreeSource.Woods) return wolves ? Pick(WolfForest, x, z) : Pick(Grove, x, z);
            if (obstacles != null && obstacles.Any(o => IsWater(o.Id) && Within(Box(o), x, z, BankReach))) return Pick(Banks, x, z);
            if (place == null) return Pick(Meadow, x, z);
            if (wolves) return Pick(WolfForest, x, z);
            if (PlaceIs(place, "verger")) return Pick(Orchard, x, z);
            if (PlaceIs(place, "vigne")) return TreeEssence.Cypres;
            return Pick(Meadow, x, z);
        }

        private static bool Within((double MinX, double MinZ, double MaxX, double MaxZ) b, double x, double z, double reach) =>
            BoxDistance(x, z, b.MinX, b.MinZ, b.MaxX, b.MaxZ) <= reach;

        /// <summary>
        /// Âge et variante (« adulte_c ») tirés de la place : un obstacle (son tronc est au moteur) est adulte ; un arbre de verger est jeune
        /// une fois sur cinq ; autour du Seuil, adulte ou vieux ; ailleurs, jeune et vieux une fois sur sept chacun.
        /// </summary>
        public static string AgeAt(double x, double z, TreeSource source)
        {
            int roll = (int)(Seed(x, z, 3) % 100u), adult = (int)(Seed(x, z, 5) % 4u);
            string grown = Ages[1 + adult];
            switch (source)
            {
                case TreeSource.Obstacle: return grown;
                case TreeSource.Orchard: return roll < 20 ? Ages[0] : grown;
                case TreeSource.Seuil: return roll < 30 ? Ages[5] : grown;
                default: return roll < 14 ? Ages[0] : roll >= 86 ? Ages[5] : grown;
            }
        }

        /// <summary>L'arbre de l'atelier à poser en (<paramref name="x"/>, <paramref name="z"/>) : essence, modèle, lacet, échelle.</summary>
        public static TreeSpot Spot(double x, double z, TreeSource source, AdventureMap? map, IEnumerable<SpaceObstacle> obstacles)
        {
            TreeEssence essence = EssenceAt(x, z, source, map, obstacles);
            return new TreeSpot(essence, Name(essence) + "_" + AgeAt(x, z, source), x, z, YawAt(x, z), ScaleAt(x, z), source);
        }

        // --- Places libres -------------------------------------------------------------------------------------------------------

        /// <summary>Écart gardé (m) : au bord d'une route, autour d'un point sûr ou d'un PNJ, d'une porte, d'un obstacle (arbres de décor).</summary>
        public const double RoadGap = 2.5, ClearingGap = 6, DoorGap = 6, ObstacleGap = 2;

        /// <summary>Écart (m) entre deux arbres de décor.</summary>
        public const double DecorSpacing = 5;

        /// <summary>Obstacle qui ne compte pas pour un arbre d'un bois ou de la lisière (le bois lui-même, l'eau, les autres arbres).</summary>
        private static bool Soft(string id) => id.StartsWith("bois", StringComparison.Ordinal) || id.StartsWith("arbre", StringComparison.Ordinal) || IsWater(id);

        /// <summary>
        /// La place est-elle libre pour un arbre ? Jamais sur une route (bord à plus de <paramref name="roadGap"/> m), ni près d'un point
        /// sûr, d'un PNJ ou d'un atelier (<see cref="AdventureMap.Clearings"/>), ni près d'une porte, ni dans un obstacle bâti (boîte élargie
        /// de <paramref name="obstacleGap"/> m ; pour un arbre de décor, l'eau et les arbres comptent aussi).
        /// </summary>
        public static bool Free(double x, double z, AdventureMap? map, IEnumerable<SpaceObstacle> obstacles, bool decor,
            double roadGap = RoadGap, double obstacleGap = ObstacleGap)
        {
            if (map != null)
            {
                if (TownRules.RoadAt(x, z, map.Roads).Edge < roadGap) return false;
                double clearing = decor ? ClearingGap : 4, door = decor ? DoorGap : 4;
                if (map.Clearings.Any(c => (c.X - x) * (c.X - x) + (c.Z - z) * (c.Z - z) < clearing * clearing)) return false;
                if (map.Doors.Any(d => (d.X - x) * (d.X - x) + (d.Z - z) * (d.Z - z) < door * door)) return false;
            }
            foreach (SpaceObstacle o in obstacles ?? Array.Empty<SpaceObstacle>())
            {
                if (!decor && Soft(o.Id)) continue;
                if (Within(Box(o), x, z, obstacleGap)) return false;
            }
            return true;
        }

        /// <summary>Un arbre d'un bois (obstacle « bois… ») est-il gardé ? Pas sur une route, un point sûr, une porte ni un autre obstacle.</summary>
        public static bool WoodFree(double x, double z, AdventureMap? map, IEnumerable<SpaceObstacle> obstacles) =>
            Free(x, z, map, obstacles, false, 1, 1);

        /// <summary>Verger : écart entre deux rangs (z) et entre deux arbres d'un rang (x), marge au bord du lieu (m).</summary>
        public const double OrchardRow = 8, OrchardStep = 7, OrchardMargin = 3;

        /// <summary>Berges : écart à la boîte de l'eau, pas le long du bord (m), part des places gardées.</summary>
        public const double BankOffset = 3, BankStep = 8, BankShare = 0.6;

        /// <summary>Abords du Seuil : rayon (m) autour du centre des pierres, nombre de places essayées.</summary>
        public const double SeuilRadius = 13;

        public const int SeuilTries = 9;

        /// <summary>
        /// Arbres de décor posés par le code (le moteur ne les connaît pas : on passe au travers) : rangs des vergers (pommiers, poiriers),
        /// berges de la rivière et du lac (saules, aulnes, une place sur <see cref="BankShare"/>), arbres corrompus autour des pierres du
        /// Seuil ; chacun libre (<see cref="Free"/>), dans l'aire, à plus de <see cref="DecorSpacing"/> m des autres. Les mêmes à chaque
        /// lancement.
        /// </summary>
        public static IReadOnlyList<TreeSpot> DecorTrees(AdventureMap? map, IReadOnlyList<SpaceObstacle> obstacles)
        {
            var spots = new List<TreeSpot>();
            if (map == null || map.Indoor) return spots;
            obstacles = obstacles ?? Array.Empty<SpaceObstacle>();
            void Try(double x, double z, TreeSource source)
            {
                if (x < map.MinX + 1.5 || x > map.MaxX - 1.5 || z < map.MinZ + 1.5 || z > map.MaxZ - 1.5) return;
                if (!Free(x, z, map, obstacles, true)) return;
                if (spots.Any(s => (s.X - x) * (s.X - x) + (s.Z - z) * (s.Z - z) < DecorSpacing * DecorSpacing)) return;
                spots.Add(Spot(x, z, source, map, obstacles));
            }
            // Vergers : rangs d'est en ouest, légèrement décalés (± 0,6 m).
            foreach (MapPlace place in map.Places.Where(p => PlaceIs(p, "verger")))
                for (double z = place.MinZ + OrchardMargin; z <= place.MaxZ - OrchardMargin; z += OrchardRow)
                    for (double x = place.MinX + OrchardMargin; x <= place.MaxX - OrchardMargin; x += OrchardStep)
                        Try(x + (Share(x, z, 17) - 0.5) * 1.2, z + (Share(x, z, 19) - 0.5) * 1.2, TreeSource.Orchard);
            // Berges : tour de la boîte de chaque eau, à BankOffset m, un pas tous les BankStep m.
            foreach (SpaceObstacle water in obstacles.Where(o => IsWater(o.Id)).OrderBy(o => o.Id, StringComparer.Ordinal))
            {
                var b = Box(water);
                double x0 = b.MinX - BankOffset, z0 = b.MinZ - BankOffset, x1 = b.MaxX + BankOffset, z1 = b.MaxZ + BankOffset;
                var corners = new[] { (x0, z0), (x1, z0), (x1, z1), (x0, z1) };
                for (int side = 0; side < 4; side++)
                {
                    var a = corners[side];
                    var c = corners[(side + 1) % 4];
                    double length = Math.Sqrt((c.Item1 - a.Item1) * (c.Item1 - a.Item1) + (c.Item2 - a.Item2) * (c.Item2 - a.Item2));
                    int n = (int)Math.Floor(length / BankStep);
                    for (int k = 0; k <= n; k++)
                    {
                        double t = n == 0 ? 0.5 : (double)k / n;
                        double x = a.Item1 + (c.Item1 - a.Item1) * t, z = a.Item2 + (c.Item2 - a.Item2) * t;
                        if (Share(x, z, 23) < BankShare) Try(x, z, TreeSource.Bank);
                    }
                }
            }
            // Abords du Seuil : quelques arbres corrompus en anneau, hors du cercle de cailloux.
            (double X, double Z)? heart = AtelierDecor.SeuilCenter(obstacles);
            if (heart != null)
                for (int i = 0; i < SeuilTries; i++)
                {
                    double a = (i * 40.0 + 15.0) * Math.PI / 180.0;
                    Try(heart.Value.X + SeuilRadius * Math.Sin(a), heart.Value.Z + SeuilRadius * Math.Cos(a), TreeSource.Seuil);
                }
            return spots;
        }

        // --- Niveaux de détail ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Distances (m) de passage du LOD1 au LOD2, du LOD2 à l'imposteur, et de l'imposteur à rien, selon la machine : plus courtes sur
        /// téléphone, intermédiaires sur un PC modeste.
        /// </summary>
        public static (double Lod2, double Impostor, double Cull) LodDistances(DeviceTier tier) => tier switch
        {
            DeviceTier.Phone => (18, 50, 200),
            DeviceTier.Modest => (28, 80, 300),
            _ => (40, 120, 450),
        };

        /// <summary>
        /// Part de la hauteur de l'écran (seuil d'un LOD d'Unity, <c>LOD.screenRelativeTransitionHeight</c>) d'un objet de taille
        /// <paramref name="size"/> (m) vu à <paramref name="distance"/> m par une caméra d'angle vertical <paramref name="fieldOfView"/>
        /// (degrés), entre 0 et 1.
        /// </summary>
        public static double ScreenHeight(double size, double distance, double fieldOfView)
        {
            if (!(size > 0) || !(distance > 0)) return 1;
            double fov = Math.Max(1, Math.Min(179, double.IsNaN(fieldOfView) ? 60 : fieldOfView));
            return Math.Max(0, Math.Min(1, size / (2 * distance * Math.Tan(fov * Math.PI / 360))));
        }

        /// <summary>Les trois seuils d'Unity (LOD1, LOD2, imposteur), strictement décroissants.</summary>
        public static (double Lod1, double Lod2, double Impostor) LodHeights(double size, DeviceTier tier, double fieldOfView)
        {
            var d = LodDistances(tier);
            double a = ScreenHeight(size, d.Lod2, fieldOfView), b = ScreenHeight(size, d.Impostor, fieldOfView), c = ScreenHeight(size, d.Cull, fieldOfView);
            b = Math.Min(b, a * 0.9);
            c = Math.Min(c, b * 0.9);
            return (a, b, Math.Max(c, 0.0005));
        }

        // --- Imposteur -----------------------------------------------------------------------------------------------------------

        /// <summary>Grille de l'imposteur : 8 colonnes (angles de 45°), 4 lignes (printemps, été, automne, hiver, de haut en bas).</summary>
        public const int ImpostorColumns = 8, ImpostorRows = 4;

        /// <summary>
        /// Colonne de l'imposteur pour une caméra en (<paramref name="dx"/>, <paramref name="dz"/>) dans le repère de l'arbre (après son
        /// lacet) : l'angle le plus proche parmi les 8. Colonne 0 : caméra devant l'arbre (+z, Blender −Y) ; la colonne k est l'arbre
        /// tourné de k × 45° dans Blender, soit la caméra à l'angle k × 45° (de +z vers +x) dans Unity. Sans allocation.
        /// </summary>
        public static int ImpostorColumn(double dx, double dz)
        {
            if (dx == 0 && dz == 0) return 0;
            double angle = Math.Atan2(dx, dz) * 180.0 / Math.PI;
            if (angle < 0) angle += 360.0;
            return (int)Math.Floor(angle / 45.0 + 0.5) % ImpostorColumns;
        }

        /// <summary>
        /// Décalage d'UV (<c>_BaseMap_ST.zw</c>) de la cellule (colonne, saison) : le quad du FBX montre la cellule (0, printemps), en haut à
        /// gauche ; une colonne vaut 1/8 en u, une saison −1/4 en v.
        /// </summary>
        public static (double U, double V) ImpostorOffset(int column, int season) =>
            ((((column % ImpostorColumns) + ImpostorColumns) % ImpostorColumns) / (double)ImpostorColumns, -Season(season) / (double)ImpostorRows);

        // --- Saisons -------------------------------------------------------------------------------------------------------------

        /// <summary>Saison par défaut (sans monde : l'arène, l'essai) : l'été.</summary>
        public const int DefaultSeason = 1;

        /// <summary>Mots des saisons dans les noms des textures (même ordre que <see cref="WorldCalendar.Seasons"/>).</summary>
        public static IReadOnlyList<string> SeasonWords { get; } = new[] { "printemps", "ete", "automne", "hiver" };

        /// <summary>Saison ramenée entre 0 et 3 (hors bornes : l'été).</summary>
        public static int Season(int season) => season >= 0 && season < SeasonWords.Count ? season : DefaultSeason;

        /// <summary>Saison du monde au jour <paramref name="day"/> (calendrier des fêtes et des marchands, 28 jours par saison).</summary>
        public static int SeasonOfDay(long day) => day < 0 ? DefaultSeason : WorldCalendar.SeasonIndexOf(day);

        /// <summary>Saison montrée : celle forcée par le menu d'essai (0 à 3), sinon celle du monde.</summary>
        public static int Shown(int forced, int world) => forced >= 0 && forced < SeasonWords.Count ? forced : Season(world);

        /// <summary>Atlas de feuillage d'une essence pour une saison (« chene_feuillage_automne_couleur »).</summary>
        public static string LeafTexture(TreeEssence essence, int season) => Name(essence) + "_feuillage_" + SeasonWords[Season(season)] + "_couleur";

        /// <summary>Carte de relief du feuillage d'une saison (« chene_feuillage_automne_normale »).</summary>
        public static string LeafNormal(TreeEssence essence, int season) => Name(essence) + "_feuillage_" + SeasonWords[Season(season)] + "_normale";

        /// <summary>Feuilles au sol (objet « …_feuilles_sol ») : en automne seulement.</summary>
        public static bool GroundLeaves(int season) => Season(season) == 2;

        /// <summary>Rôle d'un objet du FBX d'un arbre : « LOD1 », « LOD2 », « LOD3 » (imposteur), « collision », « sol », ou null.</summary>
        public static string? PartOf(string? name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string n = name!;
            if (n.EndsWith("_LOD1", StringComparison.Ordinal)) return "LOD1";
            if (n.EndsWith("_LOD2", StringComparison.Ordinal)) return "LOD2";
            if (n.EndsWith("_LOD3", StringComparison.Ordinal)) return "LOD3";
            if (n.EndsWith("_feuilles_sol", StringComparison.Ordinal)) return "sol";
            if (n.Contains("_collision")) return "collision";
            return null;
        }

        /// <summary>Matière du feuillage d'une essence (« chene_Feuillage »), dont la texture change avec la saison.</summary>
        public static bool IsFoliage(string? material) => material != null && material.EndsWith("_Feuillage", StringComparison.Ordinal);

        /// <summary>Matière d'écorce lumineuse (veines de l'arbre corrompu : « corrompu_Ecorce », « corrompu_Ecorce2 »).</summary>
        public static bool IsGlowingBark(string? material) => material != null && material.StartsWith("corrompu_Ecorce", StringComparison.Ordinal);

        /// <summary>Lueur des veines de l'arbre corrompu : comme les pierres du Seuil, discrète le jour, nette la nuit.</summary>
        public static double CorruptGlow(double lamps) => AtelierDecor.SeuilGlow(lamps);

        /// <summary>Vent des arbres de l'atelier (écorce et feuillage, même valeur : le feuillage reste sur ses branches).</summary>
        public const double Wind = 0.08;

        /// <summary>Couleur de sommet du feuillage (variation par carte), multipliée par 1,1 dans le shader (neutre : 1/1,1).</summary>
        public const double VertexTint = 1.1;

        /// <summary>Seuil de la transparence découpée du feuillage (rapport de l'atelier : ~0,5).</summary>
        public const double Cutoff = 0.5;
    }
}
