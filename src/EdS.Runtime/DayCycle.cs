using System;
using System.Collections.Generic;
using EdS.Data.Saves;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.1 (jour et nuit qui changent le monde, sujets 32 et 49) : lumière du monde à une heure donnée, pour l'affichage. Les
    /// valeurs sont des <b>facteurs</b> appliqués aux réglages du catalogue de Damien (soleil, ambiance, brume, ciel), qui restent ceux du plein
    /// jour : le jour ne change pas. La nuit est <b>bleue et lisible</b>, jamais noire : la lune (une lumière froide, haute) remplace le
    /// soleil, l'ambiance garde plus de la moitié de sa force, teintée de bleu. L'aube est rose, le crépuscule orangé ; tout change en douceur
    /// (images clés reliées par une courbe lisse), sans saut d'une image à l'autre. Affichage seulement : le moteur ne la connaît pas.
    /// </summary>
    public readonly struct DayLight
    {
        public DayLight(double minute, double sunLevel, StyleTint sunTint, double ownHeight, double catalogShare, bool moon, double ambientLevel, StyleTint ambientTint,
            StyleTint fogTint, double skyLevel, double exposure, double saturation, double lamps, double night)
        {
            Minute = minute;
            SunLevel = sunLevel;
            SunTint = sunTint;
            OwnHeight = ownHeight;
            CatalogShare = catalogShare;
            Moon = moon;
            AmbientLevel = ambientLevel;
            AmbientTint = ambientTint;
            FogTint = fogTint;
            SkyLevel = skyLevel;
            Exposure = exposure;
            Saturation = saturation;
            Lamps = lamps;
            Night = night;
        }

        /// <summary>Minute du jour du monde (0 à 1440, avec sa fraction).</summary>
        public double Minute { get; }

        /// <summary>Force de la lumière directionnelle (soleil le jour, lune la nuit), en part de celle du catalogue (0 à 1).</summary>
        public double SunLevel { get; }

        /// <summary>Teinte de la lumière directionnelle, multipliée à la couleur du soleil du catalogue.</summary>
        public StyleTint SunTint { get; }

        /// <summary>Hauteur propre de la lumière directionnelle (degrés au-dessus de l'horizon : lune haute, soleil bas à l'aube et au soir).</summary>
        public double OwnHeight { get; }

        /// <summary>Part de la hauteur du catalogue (1 en plein jour : le réglage de Damien ; 0 la nuit, à l'aube et au soir).</summary>
        public double CatalogShare { get; }

        /// <summary>Hauteur de la lumière directionnelle, le catalogue donnant celle du plein jour (<paramref name="catalogHeight"/>).</summary>
        public double Height(double catalogHeight) => OwnHeight + (catalogHeight - OwnHeight) * CatalogShare;

        /// <summary>La lumière directionnelle est la lune (direction opposée à celle du soleil du catalogue).</summary>
        public bool Moon { get; }

        /// <summary>Force de l'ambiance (ciel, horizon, sol), en part de celle du catalogue.</summary>
        public double AmbientLevel { get; }

        public StyleTint AmbientTint { get; }

        /// <summary>Teinte de la brume, multipliée à celle du catalogue.</summary>
        public StyleTint FogTint { get; }

        /// <summary>Clarté du ciel (exposition du ciel), en part de celle du catalogue.</summary>
        public double SkyLevel { get; }

        /// <summary>Exposition ajoutée à celle du catalogue (négative la nuit).</summary>
        public double Exposure { get; }

        /// <summary>Saturation ajoutée à celle du catalogue (la nuit lave un peu les couleurs).</summary>
        public double Saturation { get; }

        /// <summary>Lanternes et fenêtres allumées (0 éteintes, 1 pleinement) : elles s'allument au crépuscule et s'éteignent à l'aube.</summary>
        public double Lamps { get; }

        /// <summary>Part de nuit (0 le jour, 1 en pleine nuit) : oiseaux, grillons, lucioles, feux follets.</summary>
        public double Night { get; }
    }

    /// <summary>Teinte multiplicative (1, 1, 1 : aucune).</summary>
    public readonly struct StyleTint : IEquatable<StyleTint>
    {
        public StyleTint(double r, double g, double b)
        {
            R = r;
            G = g;
            B = b;
        }

        public double R { get; }
        public double G { get; }
        public double B { get; }

        public static readonly StyleTint White = new StyleTint(1, 1, 1);

        public static StyleTint Lerp(StyleTint a, StyleTint b, double t) => new StyleTint(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);

        public bool Equals(StyleTint other) => R == other.R && G == other.G && B == other.B;
        public override bool Equals(object? obj) => obj is StyleTint t && Equals(t);
        public override int GetHashCode() => (R, G, B).GetHashCode();
    }

    /// <summary>Sous-lot V2.1 : la lumière du monde selon l'heure (<see cref="DayLight"/>), et l'heure exacte d'un tick.</summary>
    public static class DayCycle
    {
        private readonly struct Key
        {
            public Key(double minute, double sun, StyleTint sunTint, double height, double share, double ambient, StyleTint ambientTint, StyleTint fog, double sky,
                double exposure, double saturation)
            {
                Minute = minute;
                Sun = sun;
                SunTint = sunTint;
                Height = height;
                Share = share;
                Ambient = ambient;
                AmbientTint = ambientTint;
                Fog = fog;
                Sky = sky;
                Exposure = exposure;
                Saturation = saturation;
            }

            public double Minute { get; }
            public double Sun { get; }
            public StyleTint SunTint { get; }
            public double Height { get; }
            public double Share { get; }
            public double Ambient { get; }
            public StyleTint AmbientTint { get; }
            public StyleTint Fog { get; }
            public double Sky { get; }
            public double Exposure { get; }
            public double Saturation { get; }
        }

        /// <summary>Hauteur de la lune la nuit (degrés).</summary>
        public const double MoonHeight = 42;

        /// <summary>
        /// Relecture de la PR #99 : minutes où la lune et le soleil se passent le relais (5 h 35 et 21 h 30) ; la lumière directionnelle y est
        /// éteinte (force 0), puis se rallume en douceur dans sa nouvelle direction.
        /// </summary>
        public const double MoonSets = 335, MoonRises = 1290;

        /// <summary>Hauteur du soleil levant ou couchant (degrés).</summary>
        public const double LowSun = 7;

        /// <summary>
        /// Ambiance la plus faible de la nuit, en part de celle du catalogue : la nuit reste lisible (on voit les chemins, les loups, les
        /// maisons). Provisoire, réglable.
        /// </summary>
        public const double NightAmbient = 0.7;

        private static readonly StyleTint MoonTint = new StyleTint(0.62, 0.74, 1.0);
        private static readonly StyleTint NightBlue = new StyleTint(0.52, 0.62, 1.0);
        private static readonly StyleTint NightFog = new StyleTint(0.3, 0.38, 0.58);
        private static readonly StyleTint DawnTint = new StyleTint(1.0, 0.78, 0.66);
        private static readonly StyleTint DawnAmbient = new StyleTint(0.95, 0.82, 0.86);
        private static readonly StyleTint DawnFog = new StyleTint(0.95, 0.78, 0.78);
        private static readonly StyleTint DuskTint = new StyleTint(1.0, 0.64, 0.42);
        private static readonly StyleTint DuskAmbient = new StyleTint(0.92, 0.74, 0.7);
        private static readonly StyleTint DuskFog = new StyleTint(0.9, 0.6, 0.5);

        /// <summary>
        /// Images clés de la journée, dans l'ordre des minutes ; entre deux, une courbe lisse. Pleine nuit de 22 h 30 à 4 h 15 ; aube rose
        /// vers 6 h ; plein jour (réglages du catalogue) de 7 h 30 à 18 h 30 ; crépuscule orangé vers 20 h 30. La lune prend le relais du soleil
        /// quand il est sous l'horizon (la lumière passe par un creux bref et doux, l'ambiance tient la scène lisible).
        /// </summary>
        private static readonly Key[] Keys =
        {
            new Key(0, 0.55, MoonTint, MoonHeight, 0, NightAmbient, NightBlue, NightFog, 0.32, -0.15, -12),
            new Key(255, 0.55, MoonTint, MoonHeight, 0, NightAmbient, NightBlue, NightFog, 0.32, -0.15, -12),
            new Key(MoonSets, 0, MoonTint, LowSun, 0, 0.74, NightBlue, NightFog, 0.4, -0.2, -9),
            new Key(360, 0.55, DawnTint, LowSun, 0, 0.82, DawnAmbient, DawnFog, 0.68, -0.08, -3),
            new Key(450, 1, StyleTint.White, LowSun, 1, 1, StyleTint.White, StyleTint.White, 1, 0, 0),
            new Key(1110, 1, StyleTint.White, LowSun, 1, 1, StyleTint.White, StyleTint.White, 1, 0, 0),
            new Key(1230, 0.6, DuskTint, LowSun, 0, 0.85, DuskAmbient, DuskFog, 0.66, -0.06, 4),
            new Key(MoonRises, 0, DuskTint, LowSun, 0, 0.74, NightBlue, NightFog, 0.38, -0.18, -8),
            new Key(1350, 0.55, MoonTint, MoonHeight, 0, NightAmbient, NightBlue, NightFog, 0.32, -0.15, -12),
            new Key(1440, 0.55, MoonTint, MoonHeight, 0, NightAmbient, NightBlue, NightFog, 0.32, -0.15, -12),
        };

        /// <summary>Lumière du monde à la minute <paramref name="minute"/> du jour (ramenée dans [0, 1440[).</summary>
        public static DayLight At(double minute)
        {
            if (double.IsNaN(minute) || double.IsInfinity(minute)) throw new ArgumentOutOfRangeException(nameof(minute));
            minute %= 1440;
            if (minute < 0) minute += 1440;
            int i = 0;
            while (i < Keys.Length - 2 && Keys[i + 1].Minute <= minute) i++;
            Key a = Keys[i], b = Keys[i + 1];
            double t = Smooth((minute - a.Minute) / (b.Minute - a.Minute));
            // La lune passe le relais au soleil (et l'inverse) quand la lumière directionnelle est éteinte (5 h 35, 21 h 30 : force 0
            // exactement, relecture de la PR #99) : le changement de direction ne se voit pas, aucune ombre ne saute.
            bool moon = minute < MoonSets || minute >= MoonRises;
            return new DayLight(minute, Lerp(a.Sun, b.Sun, t), StyleTint.Lerp(a.SunTint, b.SunTint, t), Lerp(a.Height, b.Height, t), Lerp(a.Share, b.Share, t), moon,
                Lerp(a.Ambient, b.Ambient, t),
                StyleTint.Lerp(a.AmbientTint, b.AmbientTint, t), StyleTint.Lerp(a.Fog, b.Fog, t), Lerp(a.Sky, b.Sky, t), Lerp(a.Exposure, b.Exposure, t),
                Lerp(a.Saturation, b.Saturation, t), Lamps(minute), NightShare(minute));
        }

        /// <summary>
        /// Lanternes et fenêtres : elles s'allument de 20 h à 21 h 30 et s'éteignent de 5 h 30 à 6 h 30 (le jour, éteintes). Courbe lisse.
        /// </summary>
        public static double Lamps(double minute)
        {
            if (minute < 330) return 1;
            if (minute < 390) return 1 - Smooth((minute - 330) / 60);
            if (minute < 1200) return 0;
            if (minute < 1290) return Smooth((minute - 1200) / 90);
            return 1;
        }

        /// <summary>Part de nuit : 1 de 22 h à 5 h (la nuit du monde), 0 de 7 h à 19 h, en douceur à l'aube et au crépuscule.</summary>
        public static double NightShare(double minute)
        {
            if (minute < 300) return 1;
            if (minute < 420) return 1 - Smooth((minute - 300) / 120);
            if (minute < 1140) return 0;
            if (minute < 1320) return Smooth((minute - 1140) / 180);
            return 1;
        }

        /// <summary>
        /// Minute du jour du monde au tick <paramref name="worldTick"/>, avec sa fraction (l'horloge du monde l'arrondit à la minute), plus
        /// <paramref name="partialTick"/> (de 0 à 1 : le temps écoulé depuis ce tick à l'écran) : le soleil avance sans à-coups.
        /// </summary>
        public static double ExactMinute(WorldClock clock, long worldTick, double partialTick = 0)
        {
            if (clock is null) throw new ArgumentNullException(nameof(clock));
            if (worldTick < 0) throw new ArgumentOutOfRangeException(nameof(worldTick));
            double part = double.IsNaN(partialTick) ? 0 : Math.Max(0, Math.Min(1, partialTick));
            long within = worldTick % clock.TicksPerDay;
            double minutes = clock.StartMinute + (within + part) * WorldClock.MinutesPerDay / clock.TicksPerDay;
            return minutes % WorldClock.MinutesPerDay;
        }

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        private static double Smooth(double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return t * t * (3 - 2 * t);
        }
    }

    /// <summary>Sous-lot V2.1 : une lumière de nuit posée par l'affichage (lanterne de porte, réverbère, feu follet, yeux d'un hibou).</summary>
    public enum NightLampKind
    {
        /// <summary>Lanterne accrochée à côté d'une porte.</summary>
        DoorLantern = 0,

        /// <summary>Lanterne sur un poteau, au bord d'une rue pavée.</summary>
        StreetLantern = 1,

        /// <summary>Feu follet qui flotte sur l'eau ou les ruines (la nuit seulement).</summary>
        Wisp = 2,

        /// <summary>Yeux d'un hibou dans un arbre (la nuit seulement).</summary>
        OwlEyes = 3,
    }

    /// <summary>Revue Codex de la PR #99 : une pièce d'une lanterne, en coordonnées locales à son support (centre et demi-tailles… voir <see cref="NightLamps.LanternPieces"/>).</summary>
    public readonly struct LanternPiece
    {
        public LanternPiece(string name, bool iron, bool glass, double x, double y, double z, double scaleX, double scaleY, double scaleZ)
        {
            Name = name;
            Iron = iron;
            Glass = glass;
            X = x;
            Y = y;
            Z = z;
            ScaleX = scaleX;
            ScaleY = scaleY;
            ScaleZ = scaleZ;
        }

        public string Name { get; }

        /// <summary>En fer sombre (poteau, potence, chapeau).</summary>
        public bool Iron { get; }

        /// <summary>Le verre, qui brille la nuit.</summary>
        public bool Glass { get; }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        /// <summary>Échelle d'Unity (taille pour un cube, rayon et demi-hauteur pour un cylindre).</summary>
        public double ScaleX { get; }
        public double ScaleY { get; }
        public double ScaleZ { get; }
    }

    public readonly struct NightLamp
    {
        public NightLamp(string id, NightLampKind kind, double x, double y, double z, double yaw = 0)
        {
            Id = id;
            Kind = kind;
            X = x;
            Y = y;
            Z = z;
            Yaw = yaw;
        }

        public string Id { get; }
        public NightLampKind Kind { get; }
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        /// <summary>Direction où elle regarde (degrés, comme <see cref="StyleRules.FaceYaw"/>).</summary>
        public double Yaw { get; }
    }

    /// <summary>
    /// Sous-lot V2.1 : où poser les lumières de la nuit, toujours au même endroit pour le même monde (aucun hasard : l'empreinte des
    /// identifiants). Affichage seulement.
    /// </summary>
    public static class NightLamps
    {
        /// <summary>Hauteur d'une lanterne de porte (m).</summary>
        public const double DoorLanternHeight = 2.2;

        /// <summary>Écart entre deux réverbères le long d'une rue pavée (m).</summary>
        public const double StreetSpacing = 14;

        /// <summary>Un bâtiment qui a une porte sur la rue reçoit une lanterne à côté (pas les tentes, les étals, le puits ni les bois).</summary>
        public static bool HasLantern(StyleBuilding kind) =>
            kind == StyleBuilding.House || kind == StyleBuilding.Inn || kind == StyleBuilding.Chapel || kind == StyleBuilding.Barn || kind == StyleBuilding.Tower;

        /// <summary>
        /// Lanterne de porte d'un bâtiment (boîte <paramref name="x0"/>…<paramref name="z1"/>, façade tournée vers <paramref name="facing"/>) :
        /// sur la façade, à 0,9 m à droite du milieu (la porte est au milieu), à 0,25 m du mur, à <see cref="DoorLanternHeight"/>.
        /// </summary>
        public static NightLamp DoorLantern(string id, double x0, double z0, double x1, double z1, (int X, int Z) facing)
        {
            double cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
            double halfAcross = facing.X != 0 ? (x1 - x0) / 2 : (z1 - z0) / 2;
            double fx = Math.Sign(facing.X), fz = facing.X != 0 ? 0 : Math.Sign(facing.Z == 0 ? 1 : facing.Z);
            // Le long de la façade : la droite quand on regarde la façade de dehors.
            double ax = -fz, az = fx;
            double x = cx + fx * (halfAcross + 0.25) + ax * 0.9, z = cz + fz * (halfAcross + 0.25) + az * 0.9;
            return new NightLamp(id + "/lanterne", NightLampKind.DoorLantern, x, DoorLanternHeight, z, StyleRules.FaceYaw(cx, cz, cx + fx, cz + fz));
        }

        /// <summary>
        /// Réverbères le long d'une rue pavée : un tous les <see cref="StreetSpacing"/> m, en alternant les côtés, au bord de la rue
        /// (0,6 m au-delà de sa demi-largeur), là où <paramref name="free"/> le permet (pas dans un mur, pas sur un étal).
        /// </summary>
        public static IReadOnlyList<NightLamp> Street(string id, IReadOnlyList<(double X, double Z)> points, double width, Func<double, double, bool> free)
        {
            if (points is null) throw new ArgumentNullException(nameof(points));
            if (free is null) throw new ArgumentNullException(nameof(free));
            var lamps = new List<NightLamp>();
            double carry = StreetSpacing / 2;
            int n = 0;
            for (int i = 0; i + 1 < points.Count; i++)
            {
                (double ax, double az) = points[i];
                (double bx, double bz) = points[i + 1];
                double length = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                if (length < 1e-6) continue;
                double ux = (bx - ax) / length, uz = (bz - az) / length;
                for (double d = carry; d <= length; d += StreetSpacing)
                {
                    double side = n % 2 == 0 ? 1 : -1;
                    double off = width / 2 + 0.6;
                    double x = ax + ux * d - uz * off * side, z = az + uz * d + ux * off * side;
                    if (free(x, z)) lamps.Add(new NightLamp($"{id}/reverbere{n}", NightLampKind.StreetLantern, x, 0, z));
                    n++;
                    carry = d + StreetSpacing - length;
                }
                if (carry > length) carry -= length;
                if (carry < 0) carry = 0;
            }
            return lamps;
        }

        /// <summary>
        /// Feux follets d'un lieu (boîte <paramref name="x0"/>…<paramref name="z1"/>) : <paramref name="count"/> lueurs à des places tirées de
        /// l'empreinte du lieu, entre 0,6 et 1,6 m du sol, là où <paramref name="free"/> le permet.
        /// </summary>
        public static IReadOnlyList<NightLamp> Wisps(string place, double x0, double z0, double x1, double z1, int count, Func<double, double, bool> free)
        {
            if (free is null) throw new ArgumentNullException(nameof(free));
            var lamps = new List<NightLamp>();
            for (int i = 0; i < count * 4 && lamps.Count < count; i++)
            {
                string key = $"{place}/follet{i}";
                double x = x0 + (x1 - x0) * StyleRules.Variant(key + "/x", 1000) / 999.0;
                double z = z0 + (z1 - z0) * StyleRules.Variant(key + "/z", 1000) / 999.0;
                if (!free(x, z)) continue;
                lamps.Add(new NightLamp(key, NightLampKind.Wisp, x, 0.6 + StyleRules.Variant(key + "/y", 100) / 100.0, z));
            }
            return lamps;
        }

        /// <summary>Un arbre sur cinq abrite un hibou (yeux à 3,2 m, tournés vers le centre du monde) ; toujours les mêmes arbres.</summary>
        public static NightLamp? Owl(string tree, double x, double z) =>
            StyleRules.Variant(tree + "/hibou", 5) == 0
                ? new NightLamp(tree + "/hibou", NightLampKind.OwlEyes, x, 3.2, z, StyleRules.FaceYaw(x, z, 0, 0))
                : (NightLamp?)null;

        /// <summary>
        /// Villageois d'affichage la nuit : la plupart rentrent chez eux ; un sur quatre (toujours le même) reste dehors, en veilleur. Les
        /// marcheurs rentrent tous.
        /// </summary>
        public static bool AwakeAtNight(StyleVillager villager) =>
            villager.Activity != VillagerActivity.Walk && StyleRules.Variant((villager.Id ?? string.Empty) + "/veilleur", 4) == 0;

        /// <summary>
        /// Revue Codex de la PR #99 : pièces d'une lanterne (poteau ou potence, chapeau, verre), en coordonnées <b>locales</b> à son support
        /// (posé au sol sous la lanterne et tourné de <see cref="NightLamp.Yaw"/> ; z local négatif : vers le mur).
        /// </summary>
        public static IReadOnlyList<LanternPiece> LanternPieces(NightLamp lamp)
        {
            double top = LanternTop(lamp);
            var pieces = new List<LanternPiece>();
            if (lamp.Kind == NightLampKind.StreetLantern) pieces.Add(new LanternPiece("poteau", true, false, 0, top / 2, 0, 0.09, top / 2, 0.09));
            else pieces.Add(new LanternPiece("potence", true, false, 0, top + 0.2, -0.12, 0.05, 0.05, 0.3));
            pieces.Add(new LanternPiece("chapeau", true, false, 0, top + 0.18, 0, 0.26, 0.06, 0.26));
            pieces.Add(new LanternPiece("verre", false, true, 0, top, 0, 0.2, 0.28, 0.2));
            return pieces;
        }

        /// <summary>Hauteur du verre d'une lanterne (m) : 2,7 m pour un réverbère, sinon sa hauteur propre.</summary>
        public static double LanternTop(NightLamp lamp) => lamp.Kind == NightLampKind.StreetLantern ? 2.7 : lamp.Y;

        /// <summary>
        /// Place dans le monde d'un point local au support de <paramref name="lamp"/> (au sol en X, Z, tourné de son angle comme
        /// <c>Quaternion.Euler(0, yaw, 0)</c> d'Unity : le z local suit la direction de <see cref="StyleRules.FaceYaw"/>).
        /// </summary>
        public static (double X, double Y, double Z) ToWorld(NightLamp lamp, double x, double y, double z)
        {
            double a = lamp.Yaw * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a);
            return (lamp.X + x * cos + z * sin, y, lamp.Z - x * sin + z * cos);
        }

        /// <summary>Un écho (créature qui revient, « loup_echo… ») : pâle et lumineux à l'affichage.</summary>
        public static bool IsEcho(string combatant) => (combatant ?? string.Empty).StartsWith("loup_echo", StringComparison.Ordinal);
    }
}

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.1 : outil d'essai (menu de l'éditeur « Éclats du Seuil/Essai de la nuit… ») : avance la sauvegarde jusqu'à une heure du
    /// monde, pour que Damien voie la nuit sans l'attendre une heure. Seul le temps du monde avance (comme une longue attente) : le personnage,
    /// le registre du monde et le reste de la sauvegarde ne changent pas ; jamais en arrière.
    /// </summary>
    public static class DayTrial
    {
        /// <summary>Premier tick, à partir de <paramref name="fromTick"/> (compris), où il est <paramref name="minute"/> au monde.</summary>
        public static long NextTickAt(WorldClock clock, long fromTick, int minute)
        {
            if (clock is null) throw new System.ArgumentNullException(nameof(clock));
            if (fromTick < 0) throw new System.ArgumentOutOfRangeException(nameof(fromTick));
            if (minute < 0 || minute >= WorldClock.MinutesPerDay) throw new System.ArgumentOutOfRangeException(nameof(minute));
            int now = clock.At(fromTick).Minute;
            long ahead = (minute - now + WorldClock.MinutesPerDay) % WorldClock.MinutesPerDay;
            long tick = checked(fromTick + ahead * clock.TicksPerDay / WorldClock.MinutesPerDay);
            // Arrondis : on recule tant que la minute d'avant est encore la bonne, puis on avance jusqu'à elle.
            while (tick > fromTick && clock.At(tick - 1).Minute == minute) tick--;
            while (clock.At(tick).Minute != minute) tick = checked(tick + 1);
            return tick;
        }

        /// <summary>
        /// Avance la sauvegarde <paramref name="path"/> jusqu'à <paramref name="minute"/> (horloge du monde de <paramref name="tickMilliseconds"/>
        /// ms par tick) ; rend le message à montrer. Rien n'est écrit si la sauvegarde manque ou ne se lit pas.
        /// </summary>
        /// <param name="stamp">Relecture de la PR #101 : marque de la copie gardée avant l'essai (l'heure, donnée par l'éditeur ; le cœur ne lit
        /// aucune horloge) ; null : « copie ».</param>
        public static string MoveSave(string path, int minute, long tickMilliseconds = 50, string? stamp = null)
        {
            if (!System.IO.File.Exists(path)) return "Aucune sauvegarde : lance d'abord l'aventure une fois.";
            SaveReadResult read = SaveStore.Read(path);
            if (!(read.Save is WorldSave save)) return "Sauvegarde illisible : " + (read.Problem ?? "?");
            var clock = new WorldClock(tickMilliseconds);
            long tick = NextTickAt(clock, save.WorldTick, minute);
            string copy = KeepCopy(path, stamp);
            SaveStore.Write(path, Advanced(save, tick));
            return $"Partie avancée au {clock.At(tick)} (tick {tick}). Copie d'avant l'essai : {System.IO.Path.GetFileName(copy)}.";
        }

        /// <summary>
        /// Sous-lot V2.10 (essai des fêtes) : premier tick, à partir de <paramref name="fromTick"/>, où il est <paramref name="minute"/> un
        /// jour du monde qui convient à <paramref name="day"/> (cherché sur deux années au plus) ; null s'il n'y en a pas.
        /// </summary>
        public static long? NextDayAt(WorldClock clock, long fromTick, System.Func<long, bool> day, int minute)
        {
            if (day is null) throw new System.ArgumentNullException(nameof(day));
            long tick = NextTickAt(clock, fromTick, minute);
            for (int i = 0; i <= 2 * EdS.Data.Story.WorldCalendar.DaysPerYear; i++)
            {
                if (day(clock.At(tick).Day)) return tick;
                tick = NextTickAt(clock, checked(tick + clock.TicksPerDay / 2), minute);   // le même moment, le jour suivant
            }
            return null;
        }

        /// <summary>Sous-lot V2.10 : les jours de la semaine <paramref name="weekday"/> (0 à 6) du calendrier du monde.</summary>
        public static System.Func<long, bool> Weekday(int weekday) => d => EdS.Data.Story.WorldCalendar.WeekdayOf(d) == weekday;

        /// <summary>Sous-lot V2.10 : le jour <paramref name="day"/> (0 à 27) de la saison <paramref name="season"/> (0 : printemps … 3 : hiver).</summary>
        public static System.Func<long, bool> SeasonDay(int season, int day) =>
            d => EdS.Data.Story.WorldCalendar.SeasonIndexOf(d) == season && EdS.Data.Story.WorldCalendar.DayOfSeason(d) == day;

        /// <summary>Sous-lot V2.10 : avance la sauvegarde jusqu'au prochain jour qui convient à <paramref name="day"/>, à <paramref name="minute"/>.</summary>
        public static string MoveSaveToDay(string path, System.Func<long, bool> day, int minute, long tickMilliseconds = 50, string? stamp = null)
        {
            if (!System.IO.File.Exists(path)) return "Aucune sauvegarde : lance d'abord l'aventure une fois.";
            SaveReadResult read = SaveStore.Read(path);
            if (!(read.Save is WorldSave save)) return "Sauvegarde illisible : " + (read.Problem ?? "?");
            var clock = new WorldClock(tickMilliseconds);
            if (!(NextDayAt(clock, save.WorldTick, day, minute) is long tick)) return "Aucun jour qui convient dans les deux années à venir.";
            string copy = KeepCopy(path, stamp);
            SaveStore.Write(path, Advanced(save, tick));
            return $"Partie avancée au {clock.At(tick)} (tick {tick}). Copie d'avant l'essai : {System.IO.Path.GetFileName(copy)}.";
        }

        /// <summary>Relecture de la PR #101 : la même sauvegarde, au tick <paramref name="tick"/> (drapeau de migration compris).</summary>
        public static WorldSave Advanced(WorldSave save, long tick) =>
            new WorldSave(save.Zone, tick, save.Sessions, save.Consumed, save.Character, save.Migrated, sectors: save.Sectors);

        /// <summary>
        /// Relecture de la PR #101 : avant un essai qui réécrit la sauvegarde, une copie à part (<c>vallons.json.essai-&lt;marque&gt;.bak</c>),
        /// qui n'écrase jamais une copie existante (un numéro est ajouté) ; rend son chemin.
        /// </summary>
        public static string KeepCopy(string path, string? stamp = null)
        {
            string root = path + ".essai-" + (string.IsNullOrEmpty(stamp) ? "copie" : stamp);
            string copy = root + ".bak";
            for (int n = 2; System.IO.File.Exists(copy); n++) copy = root + "-" + n.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bak";
            System.IO.File.Copy(path, copy);
            return copy;
        }
    }
}
