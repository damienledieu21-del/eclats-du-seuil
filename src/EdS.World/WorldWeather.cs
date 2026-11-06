using System;
using System.Globalization;
using EdS.Simulation.Randomness;

namespace EdS.World
{
    /// <summary>
    /// Sous-lot V2.2 (météo) : le temps qu'il fait, tel qu'on le nomme (répliques des villageois, affichage). Le temps ne change aucune règle du
    /// combat : il est d'abord une ambiance.
    /// </summary>
    public enum WeatherKind
    {
        /// <summary>Ciel clair (quelques nuages).</summary>
        Clear = 0,

        /// <summary>Ciel couvert, sans pluie.</summary>
        Cloudy = 1,

        /// <summary>Brume du matin (de l'aube à 9 h 30 environ), sans pluie forte.</summary>
        Mist = 2,

        /// <summary>Pluie fine.</summary>
        Drizzle = 3,

        /// <summary>Averse.</summary>
        Shower = 4,

        /// <summary>Orage (rare) : pluie forte, éclairs et tonnerre, pour l'ambiance seulement.</summary>
        Storm = 5,
    }

    /// <summary>Sous-lot V2.2 : caractère d'une journée du monde, tiré une fois par jour ; il décide des temps de ses huit tranches de 3 h.</summary>
    public enum WeatherMood
    {
        /// <summary>Beau temps (le jour 0, toujours).</summary>
        Fair = 0,

        /// <summary>Temps variable : soleil, nuages, un peu de pluie fine.</summary>
        Changeable = 1,

        /// <summary>Journée pluvieuse.</summary>
        Rainy = 2,

        /// <summary>Journée orageuse (rare) : l'orage éclate l'après-midi ou le soir.</summary>
        Stormy = 3,
    }

    /// <summary>
    /// Sous-lot V2.2 : le temps d'un jour du monde dans une zone : son caractère, le temps de chacune de ses huit tranches de 3 h (0 h, 3 h…
    /// 21 h) et la force de sa brume du matin (0 : aucune). Valeur entière, sans allocation.
    /// </summary>
    public readonly struct WeatherDay : IEquatable<WeatherDay>
    {
        private readonly int _slots;

        internal WeatherDay(long day, WeatherMood mood, int slots, int mist)
        {
            Day = day;
            Mood = mood;
            _slots = slots;
            Mist = mist;
        }

        public long Day { get; }
        public WeatherMood Mood { get; }

        /// <summary>Force de la brume du matin au plus fort (0 à <see cref="WorldWeather.Scale"/>) ; 0 : pas de brume ce jour-là.</summary>
        public int Mist { get; }

        /// <summary>Temps de la tranche <paramref name="slot"/> (0 à 7 : de 0 h à 3 h, de 3 h à 6 h… de 21 h à minuit).</summary>
        public WeatherKind Slot(int slot)
        {
            if (slot < 0 || slot >= WorldWeather.SlotsPerDay) throw new ArgumentOutOfRangeException(nameof(slot));
            return (WeatherKind)((_slots >> (4 * slot)) & 0xF);
        }

        public bool Equals(WeatherDay other) => Day == other.Day && Mood == other.Mood && _slots == other._slots && Mist == other.Mist;
        public override bool Equals(object? obj) => obj is WeatherDay d && Equals(d);
        public override int GetHashCode() => unchecked((int)Day * 31 + _slots * 7 + Mist);
    }

    /// <summary>
    /// Sous-lot V2.2 : le temps à une minute du monde. Les forces sont des entiers de 0 à <see cref="WorldWeather.Scale"/> (pour mille) :
    /// nuages, pluie, orage, brume, sol mouillé. <see cref="Lightning"/> : un éclair tombe pendant cette minute du monde ; le tonnerre suit
    /// après <see cref="ThunderDelayTenths"/> dixièmes de seconde (affichage).
    /// </summary>
    public readonly struct WeatherState : IEquatable<WeatherState>
    {
        public WeatherState(WeatherKind kind, int clouds, int rain, int storm, int mist, int wetness, bool lightning, int thunderDelayTenths)
        {
            Kind = kind;
            Clouds = clouds;
            Rain = rain;
            Storm = storm;
            Mist = mist;
            Wetness = wetness;
            Lightning = lightning;
            ThunderDelayTenths = thunderDelayTenths;
        }

        /// <summary>Le temps qu'on nomme (le plus marquant : l'orage, puis la pluie, la brume, les nuages).</summary>
        public WeatherKind Kind { get; }

        public int Clouds { get; }
        public int Rain { get; }
        public int Storm { get; }
        public int Mist { get; }

        /// <summary>Sol mouillé (flaques) : monte avec la pluie, sèche en deux heures du monde environ après elle.</summary>
        public int Wetness { get; }

        public bool Lightning { get; }

        /// <summary>Délai du tonnerre après l'éclair, en dixièmes de seconde (0 sans éclair) : court quand l'orage est sur la zone.</summary>
        public int ThunderDelayTenths { get; }

        /// <summary>Il pleut (pluie fine, averse ou orage).</summary>
        public bool Raining => Kind == WeatherKind.Drizzle || Kind == WeatherKind.Shower || Kind == WeatherKind.Storm;

        public bool Equals(WeatherState o) => Kind == o.Kind && Clouds == o.Clouds && Rain == o.Rain && Storm == o.Storm && Mist == o.Mist && Wetness == o.Wetness
            && Lightning == o.Lightning && ThunderDelayTenths == o.ThunderDelayTenths;

        public override bool Equals(object? obj) => obj is WeatherState s && Equals(s);
        public override int GetHashCode() => unchecked(((int)Kind * 1009 + Clouds) * 31 + Rain * 17 + Storm * 13 + Mist * 7 + Wetness + (Lightning ? 1 : 0));

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "{0} (nuages {1}, pluie {2}, orage {3}, brume {4}, sol {5}{6})", WorldWeather.Id(Kind), Clouds, Rain, Storm, Mist,
                Wetness, Lightning ? ", éclair" : string.Empty);
    }

    /// <summary>
    /// Sous-lot V2.2 (météo, plan de l'alpha, chantier 14) : le temps du monde, <b>recalculé</b> du jour et de la minute du monde et de la zone,
    /// sans horloge réelle ni état : rien n'est sauvegardé (la sauvegarde garde le tick du monde, qui donne le jour et la minute ; la même
    /// partie a donc toujours le même temps, avant comme après un chargement). Chaque jour a son caractère et le temps de ses huit tranches de
    /// 3 h du monde (22 min 30 s de jeu), tirés par un <see cref="Pcg32"/> initialisé par l'empreinte FNV-1a de la clé du jour
    /// (<c>zone.vallons/weather/12</c>), comme le stock des marchands et les nuits rares. D'une tranche à l'autre, le temps change en douceur
    /// pendant les <see cref="BlendMinutes"/> dernières minutes du monde de la tranche (environ 5 min 30 s de jeu), d'un jour à l'autre aussi.
    /// La brume du matin monte à l'aube (4 h à 5 h 30), tient jusqu'à 7 h et se lève avant 9 h 30. Les éclairs ne tombent que pendant un orage,
    /// à des minutes tirées d'un hachage stable (zone, minute). Valeurs <b>provisoires</b>, réglables. Le jour 0 (début du monde, à 8 h) est
    /// toujours beau : la première heure du jeu se joue au soleil.
    /// </summary>
    public static class WorldWeather
    {
        /// <summary>Échelle des forces (pour mille).</summary>
        public const int Scale = 1000;

        /// <summary>Durée d'une tranche de temps (minutes du monde) : 3 h du monde, 22 min 30 s de jeu.</summary>
        public const int SlotMinutes = 180;

        public const int SlotsPerDay = WorldClock.MinutesPerDay / SlotMinutes;

        /// <summary>Durée d'un changement de temps (minutes du monde, à la fin d'une tranche) : environ 5 min 30 s de jeu.</summary>
        public const int BlendMinutes = 45;

        /// <summary>Brume du matin : monte de 4 h à 5 h 30, tient jusqu'à 7 h, se lève jusqu'à 9 h 30 (minutes du jour).</summary>
        public const int MistRise = 240, MistFull = 330, MistHold = 420, MistGone = 570;

        /// <summary>Le sol mouillé sèche en environ deux heures du monde (huit relevés, un par quart d'heure).</summary>
        public const int DrySamples = 8, DryStep = 15;

        /// <summary>Tranches où un orage peut éclater (de 12 h à 21 h) ; ailleurs, une journée orageuse a des averses.</summary>
        public const int FirstStormSlot = 4, LastStormSlot = 6;

        /// <summary>Identifiants des temps (conditions <c>weather</c> de l'histoire) ; « rain » : n'importe quelle pluie.</summary>
        public static string Id(WeatherKind kind) => kind switch
        {
            WeatherKind.Clear => "clear",
            WeatherKind.Cloudy => "cloudy",
            WeatherKind.Mist => "mist",
            WeatherKind.Drizzle => "drizzle",
            WeatherKind.Shower => "shower",
            _ => "storm",
        };

        /// <summary>Identifiant « rain » : pluie fine, averse ou orage.</summary>
        public const string AnyRain = "rain";

        /// <summary>Le temps <paramref name="kind"/> répond à l'identifiant <paramref name="id"/> (« rain » : toute pluie).</summary>
        public static bool Matches(WeatherKind kind, string id) =>
            id == AnyRain ? kind == WeatherKind.Drizzle || kind == WeatherKind.Shower || kind == WeatherKind.Storm : string.Equals(Id(kind), id, StringComparison.Ordinal);

        private static readonly ulong Stream = Loot.Fnv1a("weather");

        /// <summary>Clé du jour <paramref name="day"/> de la zone <paramref name="zone"/> (graine de son tirage).</summary>
        public static string Key(string zone, long day)
        {
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            if (day < 0) throw new ArgumentOutOfRangeException(nameof(day));
            return zone + "/weather/" + day.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Le temps du jour <paramref name="day"/> de la zone <paramref name="zone"/> (toujours le même pour la même clé).</summary>
        public static WeatherDay Day(string zone, long day)
        {
            string key = Key(zone, day);
            // Le jour 0 (le monde commence à 8 h) est toujours beau, sans brume : la première heure du jeu se joue au soleil.
            if (day == 0) return new WeatherDay(0, WeatherMood.Fair, 0, 0);
            var random = new Pcg32(Loot.Fnv1a(key), Stream);
            uint roll = random.NextBounded(100);
            WeatherMood mood = roll < 50 ? WeatherMood.Fair : roll < 78 ? WeatherMood.Changeable : roll < 93 ? WeatherMood.Rainy : WeatherMood.Stormy;
            int slots = 0;
            WeatherKind previous = WeatherKind.Clear;
            for (int i = 0; i < SlotsPerDay; i++)
            {
                // Le temps tient : plus d'une fois sur deux, une tranche garde celui de la précédente (durées crédibles, 3 h à 12 h).
                WeatherKind kind = i > 0 && random.NextBounded(100) < 55 ? previous : Draw(mood, random.NextBounded(100));
                // L'orage n'éclate que l'après-midi ou le soir ; ailleurs, et au-delà d'une tranche, une averse.
                if (kind == WeatherKind.Storm && (i < FirstStormSlot || i > LastStormSlot || previous == WeatherKind.Storm)) kind = WeatherKind.Shower;
                slots |= (int)kind << (4 * i);
                previous = kind;
            }
            // Brume du matin : plus souvent un jour humide ; jamais sous une averse ou un orage à l'aube (la pluie la chasse).
            int chance = mood switch { WeatherMood.Fair => 35, WeatherMood.Changeable => 55, WeatherMood.Rainy => 50, _ => 25 };
            int mist = random.NextBounded(100) < chance ? 550 + (int)random.NextBounded(451) : 0;
            for (int i = MistRise / SlotMinutes; i <= MistHold / SlotMinutes; i++)
            {
                var dawn = (WeatherKind)((slots >> (4 * i)) & 0xF);
                if (dawn == WeatherKind.Shower || dawn == WeatherKind.Storm) mist = 0;
            }
            return new WeatherDay(day, mood, slots, mist);
        }

        private static WeatherKind Draw(WeatherMood mood, uint roll) => mood switch
        {
            WeatherMood.Fair => roll < 75 ? WeatherKind.Clear : WeatherKind.Cloudy,
            WeatherMood.Changeable => roll < 30 ? WeatherKind.Clear : roll < 78 ? WeatherKind.Cloudy : WeatherKind.Drizzle,
            WeatherMood.Rainy => roll < 30 ? WeatherKind.Cloudy : roll < 70 ? WeatherKind.Drizzle : WeatherKind.Shower,
            _ => roll < 20 ? WeatherKind.Cloudy : roll < 55 ? WeatherKind.Shower : WeatherKind.Storm,
        };

        /// <summary>Nuages, pluie et orage d'une tranche (pour mille).</summary>
        private static (int Clouds, int Rain, int Storm) Levels(WeatherKind kind) => kind switch
        {
            WeatherKind.Clear => (150, 0, 0),
            WeatherKind.Cloudy => (650, 0, 0),
            WeatherKind.Drizzle => (800, 300, 0),
            WeatherKind.Shower => (900, 750, 0),
            WeatherKind.Storm => (1000, 1000, 1000),
            _ => (500, 0, 0),
        };

        /// <summary>Le temps à l'heure <paramref name="time"/> du monde dans la zone <paramref name="zone"/>.</summary>
        public static WeatherState At(string zone, WorldTime time) => At(zone, time.Day, time.Minute);

        /// <summary>Le temps au jour <paramref name="day"/>, à la minute <paramref name="minute"/> (0 à 1439), dans la zone <paramref name="zone"/>.</summary>
        public static WeatherState At(string zone, long day, int minute)
        {
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            if (day < 0) throw new ArgumentOutOfRangeException(nameof(day));
            if (minute < 0 || minute >= WorldClock.MinutesPerDay) throw new ArgumentOutOfRangeException(nameof(minute));
            long absolute = checked(day * WorldClock.MinutesPerDay + minute);
            WeatherDay today = Day(zone, day);
            WeatherDay? tomorrow = null, yesterday = null;
            WeatherDay DayOf(long d)
            {
                if (d == day) return today;
                if (d == day + 1) return tomorrow ??= Day(zone, d);
                return yesterday ??= Day(zone, d);
            }
            (int clouds, int rain, int storm) = Sky(absolute, DayOf);
            int mist = MistAt(today.Mist, minute);
            // Sol mouillé : la pluie des deux dernières heures, de moins en moins (un relevé par quart d'heure).
            int wet = 0;
            for (int k = 0; k <= DrySamples; k++)
            {
                long at = absolute - (long)k * DryStep;
                if (at < 0) break;
                int r = k == 0 ? rain : Sky(at, DayOf).Rain;
                int w = Math.Min(Scale, r * 3) * (Scale - k * 110) / Scale;
                if (w > wet) wet = w;
            }
            WeatherKind kind = storm >= 500 ? WeatherKind.Storm : rain >= 550 ? WeatherKind.Shower : rain >= 120 ? WeatherKind.Drizzle
                : mist >= 350 ? WeatherKind.Mist : clouds >= 450 ? WeatherKind.Cloudy : WeatherKind.Clear;
            // Éclairs : pendant l'orage, environ une minute du monde sur trois (25 s de jeu) ; au loin (orage qui arrive ou s'en va), plus rares
            // et le tonnerre plus tardif.
            bool lightning = false;
            int delay = 0;
            if (storm >= 300)
            {
                ulong h = Mix(Loot.Fnv1a(zone) ^ unchecked((ulong)absolute * 0x9E3779B97F4A7C15UL));
                bool near = storm >= 600;
                lightning = (int)(h % 100) < (near ? 30 : 10);
                if (lightning) delay = near ? 4 + (int)(h / 100 % 20) : 20 + (int)(h / 100 % 30);
            }
            return new WeatherState(kind, clouds, rain, storm, mist, wet, lightning, delay);
        }

        /// <summary>Nuages, pluie et orage à la minute absolue <paramref name="absolute"/> (depuis minuit du jour 0), fondus en fin de tranche.</summary>
        private static (int Clouds, int Rain, int Storm) Sky(long absolute, Func<long, WeatherDay> dayOf)
        {
            long slot = absolute / SlotMinutes;
            int within = (int)(absolute - slot * SlotMinutes);
            (int c, int r, int s) a = Levels(KindOf(slot, dayOf));
            int from = SlotMinutes - BlendMinutes;
            if (within < from) return a;
            (int c, int r, int s) b = Levels(KindOf(slot + 1, dayOf));
            int t = Smooth((within - from) * Scale / BlendMinutes);
            return (Lerp(a.c, b.c, t), Lerp(a.r, b.r, t), Lerp(a.s, b.s, t));
        }

        private static WeatherKind KindOf(long slot, Func<long, WeatherDay> dayOf) => dayOf(slot / SlotsPerDay).Slot((int)(slot % SlotsPerDay));

        /// <summary>Brume du matin à la minute <paramref name="minute"/>, d'une force <paramref name="peak"/> au plus fort.</summary>
        public static int MistAt(int peak, int minute)
        {
            if (peak <= 0 || minute < MistRise || minute >= MistGone) return 0;
            if (minute < MistFull) return peak * Smooth((minute - MistRise) * Scale / (MistFull - MistRise)) / Scale;
            if (minute < MistHold) return peak;
            return peak * (Scale - Smooth((minute - MistHold) * Scale / (MistGone - MistHold))) / Scale;
        }

        /// <summary>Courbe lisse entière (smoothstep) de 0 à <see cref="Scale"/>.</summary>
        private static int Smooth(int t)
        {
            t = Math.Max(0, Math.Min(Scale, t));
            return (int)((long)t * t * (3L * Scale - 2L * t) / ((long)Scale * Scale));
        }

        private static int Lerp(int a, int b, int t) => a + (b - a) * t / Scale;

        /// <summary>Mélange stable (SplitMix64) d'un entier : un hachage, sans état ni allocation.</summary>
        private static ulong Mix(ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
