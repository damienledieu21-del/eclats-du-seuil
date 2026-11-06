using System;
using System.Collections.Generic;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.2 (météo) : ce que le temps change à l'affichage, en <b>facteurs</b> appliqués par-dessus la lumière de l'heure
    /// (<see cref="DayLight"/>, elle-même appliquée aux réglages du catalogue) : soleil voilé, ambiance plus basse et plus grise, ciel couvert,
    /// brume rapprochée, couleurs lavées, pluie, sol mouillé. Par temps clair, tout vaut 1 (ou 0) : le catalogue et la nuit de V2.1 ne changent
    /// pas. Affichage seulement : le moteur ne la connaît pas.
    /// </summary>
    public readonly struct WeatherLook
    {
        public WeatherLook(WeatherKind kind, double sun, double ambient, StyleTint ambientTint, double sky, StyleTint fogTint, double fogStart, double fogEnd,
            double exposure, double saturation, double rain, double storm, double mist, double wetness, double lamps)
        {
            Kind = kind;
            Sun = sun;
            Ambient = ambient;
            AmbientTint = ambientTint;
            Sky = sky;
            FogTint = fogTint;
            FogStart = fogStart;
            FogEnd = fogEnd;
            Exposure = exposure;
            Saturation = saturation;
            Rain = rain;
            Storm = storm;
            Mist = mist;
            Wetness = wetness;
            Lamps = lamps;
        }

        public WeatherKind Kind { get; }

        /// <summary>Force de la lumière directionnelle (soleil ou lune), en part de celle de l'heure (les nuages la voilent).</summary>
        public double Sun { get; }

        /// <summary>Force de l'ambiance, en part de celle de l'heure.</summary>
        public double Ambient { get; }

        /// <summary>Teinte de l'ambiance (grise et froide sous la pluie).</summary>
        public StyleTint AmbientTint { get; }

        /// <summary>Clarté du ciel, en part de celle de l'heure.</summary>
        public double Sky { get; }

        /// <summary>Teinte de la brume (grise sous la pluie, blanche le matin).</summary>
        public StyleTint FogTint { get; }

        /// <summary>Début et fin de la brume, en part de ceux du catalogue (la brume du matin les rapproche beaucoup).</summary>
        public double FogStart { get; }

        public double FogEnd { get; }

        /// <summary>Exposition et saturation ajoutées à celles de l'heure.</summary>
        public double Exposure { get; }

        public double Saturation { get; }

        /// <summary>Pluie (0 à 1) : nombre de gouttes, éclaboussures, nappe de pluie.</summary>
        public double Rain { get; }

        /// <summary>Orage (0 à 1).</summary>
        public double Storm { get; }

        /// <summary>Brume du matin (0 à 1).</summary>
        public double Mist { get; }

        /// <summary>Sol mouillé (0 à 1) : flaques.</summary>
        public double Wetness { get; }

        /// <summary>Lanternes allumées au moins à ce point (0 à 1) : sous un orage, le village allume plus tôt.</summary>
        public double Lamps { get; }

        /// <summary>Temps clair : aucun changement.</summary>
        public static WeatherLook Clear { get; } = new WeatherLook(WeatherKind.Clear, 1, 1, StyleTint.White, 1, StyleTint.White, 1, 1, 0, 0, 0, 0, 0, 0, 0);
    }

    /// <summary>Sous-lot V2.2 : une flaque d'eau posée par l'affichage au bord d'un chemin (toujours à la même place).</summary>
    public readonly struct Puddle
    {
        public Puddle(string id, double x, double z, double radius, double stretch, double yaw)
        {
            Id = id;
            X = x;
            Z = z;
            Radius = radius;
            Stretch = stretch;
            Yaw = yaw;
        }

        public string Id { get; }
        public double X { get; }
        public double Z { get; }

        /// <summary>Rayon (m) quand le sol est trempé.</summary>
        public double Radius { get; }

        /// <summary>Allongement (1 : ronde).</summary>
        public double Stretch { get; }

        public double Yaw { get; }
    }

    /// <summary>
    /// Sous-lot V2.2 (météo) : le temps à l'affichage (<see cref="WeatherLook"/>), les flaques, les villageois du décor qui s'abritent, et les
    /// sons de synthèse (pluie, tonnerre) quand aucun fichier d'ambiance n'est déposé. Aucune horloge réelle, aucun hasard : tout est tiré de
    /// l'état du temps (<see cref="WorldWeather"/>) et de l'empreinte des identifiants.
    /// </summary>
    public static class WeatherCycle
    {
        private static readonly StyleTint RainAmbient = new StyleTint(0.86, 0.9, 1.0);
        private static readonly StyleTint RainFog = new StyleTint(0.74, 0.78, 0.84);
        private static readonly StyleTint MistFog = new StyleTint(1.08, 1.08, 1.1);

        /// <summary>
        /// Ambiance la plus basse que le temps laisse à la nuit, en part de son ambiance (la nuit reste lisible sous l'orage : on voit les
        /// chemins et les loups).
        /// </summary>
        public const double NightFloor = 0.8;

        /// <summary>Ce que le temps <paramref name="state"/> change à l'affichage, à l'heure <paramref name="day"/>.</summary>
        public static WeatherLook Look(WeatherState state, DayLight day)
        {
            double overcast = Clamp((state.Clouds - 150) / 850.0);
            double rain = Clamp(state.Rain / (double)WorldWeather.Scale);
            double storm = Clamp(state.Storm / (double)WorldWeather.Scale);
            double mist = Clamp(state.Mist / (double)WorldWeather.Scale);
            double wet = Clamp(state.Wetness / (double)WorldWeather.Scale);
            double night = Clamp(day.Night);
            // Les nuages voilent le soleil (et la lune) ; l'ambiance baisse moins, et moins encore la nuit (déjà sombre) : la nuit reste lisible.
            double sun = 1 - 0.72 * overcast - 0.18 * storm;
            double ambient = 1 - (0.26 * overcast + 0.12 * storm) * (1 - 0.5 * night);
            ambient = Math.Max(ambient, 1 - (1 - NightFloor) * night - (1 - night) * 0.45);
            double sky = 1 - 0.5 * overcast - 0.2 * storm;
            StyleTint ambientTint = StyleTint.Lerp(StyleTint.White, RainAmbient, overcast * (1 - 0.5 * night));
            // Brume : grise sous la pluie, blanche le matin ; elle se rapproche (beaucoup le matin, un peu sous la pluie).
            StyleTint fog = StyleTint.Lerp(StyleTint.Lerp(StyleTint.White, RainFog, overcast * 0.7), MistFog, mist * (1 - night));
            double fogEnd = Math.Min(1 - 0.82 * mist, 1 - 0.45 * rain - 0.1 * storm);
            double fogStart = Math.Min(1 - mist, 1 - 0.6 * rain);
            double exposure = -0.22 * overcast - 0.1 * storm + 0.05 * mist * (1 - night);
            double saturation = -16 * overcast - 8 * rain + -6 * mist;
            double lamps = Clamp(storm * 0.9 + Math.Max(0, overcast - 0.6) * 0.5);
            return new WeatherLook(state.Kind, sun, ambient, ambientTint, sky, fog, fogStart, fogEnd, exposure, saturation, rain, storm, mist, wet, lamps);
        }

        /// <summary>
        /// Un temps type, en plein (pour le menu d'essai qui force un temps à l'affichage) : sans éclair (l'affichage les tire lui-même pendant
        /// un orage forcé).
        /// </summary>
        public static WeatherState Typical(WeatherKind kind) => kind switch
        {
            WeatherKind.Cloudy => new WeatherState(kind, 700, 0, 0, 0, 0, false, 0),
            WeatherKind.Mist => new WeatherState(kind, 450, 0, 0, 900, 0, false, 0),
            WeatherKind.Drizzle => new WeatherState(kind, 800, 300, 0, 0, 900, false, 0),
            WeatherKind.Shower => new WeatherState(kind, 900, 750, 0, 0, 1000, false, 0),
            WeatherKind.Storm => new WeatherState(kind, 1000, 1000, 1000, 0, 1000, false, 0),
            _ => new WeatherState(WeatherKind.Clear, 150, 0, 0, 0, 0, false, 0),
        };

        /// <summary>
        /// Un villageois du décor s'abrite sous la pluie : la plupart rentrent (comme la nuit) ; un sur trois reste dehors (toujours le même,
        /// sous un auvent ou parce qu'il s'en moque). Les marcheurs rentrent tous.
        /// </summary>
        public static bool StaysOutInRain(StyleVillager villager) =>
            villager.Activity != VillagerActivity.Walk && StyleRules.Variant((villager.Id ?? string.Empty) + "/pluie", 3) == 0;

        /// <summary>Pluie assez forte pour que les villageois du décor rentrent (une pluie fine ne chasse personne).</summary>
        public const double ShelterRain = 0.5;

        /// <summary>Écart entre deux flaques possibles le long d'un chemin (m).</summary>
        public const double PuddleSpacing = 7;

        /// <summary>
        /// Flaques le long des chemins : une place tous les <see cref="PuddleSpacing"/> m, une sur deux gardée (empreinte de l'identifiant),
        /// sur la chaussée ou juste au bord, là où <paramref name="free"/> le permet ; au plus <paramref name="max"/> en tout.
        /// </summary>
        public static IReadOnlyList<Puddle> Puddles(IReadOnlyList<StyleRoad> roads, Func<double, double, bool> free, int max)
        {
            if (roads is null) throw new ArgumentNullException(nameof(roads));
            if (free is null) throw new ArgumentNullException(nameof(free));
            var puddles = new List<Puddle>();
            foreach (StyleRoad road in roads)
            {
                IReadOnlyList<(double X, double Z)> points = road.Points;
                int n = 0;
                for (int i = 0; i + 1 < points.Count && puddles.Count < max; i++)
                {
                    (double ax, double az) = points[i];
                    (double bx, double bz) = points[i + 1];
                    double length = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                    if (length < 1e-6) continue;
                    double ux = (bx - ax) / length, uz = (bz - az) / length;
                    for (double d = PuddleSpacing / 2; d < length && puddles.Count < max; d += PuddleSpacing, n++)
                    {
                        string id = $"{road.Id}/flaque{n}";
                        if (StyleRules.Variant(id, 2) != 0) continue;
                        double side = (StyleRules.Variant(id + "/cote", 100) / 99.0 - 0.5) * road.Width * 0.8;
                        double x = ax + ux * d - uz * side, z = az + uz * d + ux * side;
                        if (!free(x, z)) continue;
                        double radius = 0.45 + StyleRules.Variant(id + "/taille", 100) / 99.0 * 0.6;
                        double stretch = 1 + StyleRules.Variant(id + "/long", 100) / 99.0 * 0.8;
                        puddles.Add(new Puddle(id, x, z, radius, stretch, StyleRules.Variant(id + "/angle", 360)));
                    }
                }
            }
            return puddles;
        }

        /// <summary>Taille d'une flaque (part de son rayon) selon le sol mouillé : rien sous 0,15, pleine à 0,85.</summary>
        public static double PuddleSize(double wetness) => Smooth((wetness - 0.15) / 0.7);

        /// <summary>
        /// Coup de tonnerre de synthèse (quand aucun fichier « orage », « thunder » ou « tonnerre » n'est déposé) : un craquement bref puis un
        /// grondement grave qui roule et s'éteint, de <paramref name="seconds"/> secondes, à <paramref name="rate"/> échantillons par seconde ;
        /// valeurs dans [-1, 1]. Toujours le même son pour la même graine (<paramref name="seed"/>) : bruit d'un générateur à congruence.
        /// </summary>
        public static float[] Thunder(int rate, double seconds, uint seed)
        {
            if (rate < 8000 || rate > 96000) throw new ArgumentOutOfRangeException(nameof(rate));
            if (!(seconds >= 0.5 && seconds <= 10)) throw new ArgumentOutOfRangeException(nameof(seconds));
            int count = (int)(rate * seconds);
            var samples = new float[count];
            uint state = seed | 1;
            double low = 0, lower = 0, crack = 0;
            double peak = 1e-9;
            var raw = new double[count];
            for (int i = 0; i < count; i++)
            {
                state = unchecked(state * 1664525u + 1013904223u);
                double noise = (state >> 8) / 8388608.0 - 1;
                double t = i / (double)rate;
                // Deux filtres passe-bas en chaîne : le grondement ; un bruit plus clair au début : le craquement.
                low += (noise - low) * 0.035;
                lower += (low - lower) * 0.02;
                crack += (noise - crack) * 0.45;
                double rumble = lower * 9 * Math.Exp(-t / (seconds * 0.38)) * (0.65 + 0.35 * Math.Sin(t * 5.3) * Math.Sin(t * 2.1));
                double snap = crack * Math.Exp(-t / 0.09) * 0.8;
                double attack = Math.Min(1, t / 0.012);
                double tail = Math.Min(1, (seconds - t) / 0.3);
                raw[i] = (rumble + snap) * attack * tail;
                peak = Math.Max(peak, Math.Abs(raw[i]));
            }
            for (int i = 0; i < count; i++) samples[i] = (float)(raw[i] / peak * 0.9);
            return samples;
        }

        /// <summary>
        /// Nappe de pluie de synthèse, qui tourne en boucle sans à-coup (quand aucun fichier « pluie », « rain » ou « averse » n'est déposé) :
        /// un bruit doux (filtré) et de petites gouttes ; <paramref name="seconds"/> secondes à <paramref name="rate"/> échantillons par seconde.
        /// Le début et la fin se fondent l'un dans l'autre.
        /// </summary>
        public static float[] Rain(int rate, double seconds, uint seed)
        {
            if (rate < 8000 || rate > 96000) throw new ArgumentOutOfRangeException(nameof(rate));
            if (!(seconds >= 1 && seconds <= 20)) throw new ArgumentOutOfRangeException(nameof(seconds));
            int count = (int)(rate * seconds);
            int fade = rate / 2;
            var raw = new double[count + fade];
            uint state = seed | 1;
            double soft = 0, drop = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                state = unchecked(state * 1664525u + 1013904223u);
                double noise = (state >> 8) / 8388608.0 - 1;
                soft += (noise - soft) * 0.25;
                // Une goutte de temps en temps : un petit clic qui s'éteint vite.
                if ((state & 0x3FF) < 3) drop = 0.9 * (((state >> 12) & 0xFF) / 255.0 - 0.5);
                drop *= 0.93;
                raw[i] = soft * 0.5 + (noise - soft) * 0.12 + drop;
            }
            var samples = new float[count];
            double peak = 1e-9;
            for (int i = 0; i < count; i++)
            {
                // La fin (au-delà de la boucle) revient en fondu sur le début : la boucle ne claque pas.
                double v = raw[i];
                if (i < fade) v = raw[i] * (i / (double)fade) + raw[count + i] * (1 - i / (double)fade);
                raw[i] = v;
                peak = Math.Max(peak, Math.Abs(v));
            }
            for (int i = 0; i < count; i++) samples[i] = (float)(raw[i] / peak * 0.8);
            return samples;
        }

        private static double Clamp(double v) => double.IsNaN(v) ? 0 : Math.Max(0, Math.Min(1, v));

        private static double Smooth(double t)
        {
            t = Clamp(t);
            return t * t * (3 - 2 * t);
        }
    }

    /// <summary>
    /// Sous-lot V2.2 : outil d'essai (menu de l'éditeur « Éclats du Seuil/Essai de la météo… ») : avance la sauvegarde jusqu'au prochain moment
    /// où il fait le temps voulu, pour que Damien le voie sans l'attendre. Seul le temps du monde avance (comme une longue attente) ; jamais en
    /// arrière. En mode Jeu, le menu force plutôt le temps à l'affichage (<see cref="WeatherLook"/>), sans rien changer à la partie.
    /// </summary>
    public static class WeatherTrial
    {
        /// <summary>Jours du monde fouillés au plus (au-delà, le temps voulu est jugé introuvable).</summary>
        public const int MaxDays = 400;

        /// <summary>
        /// Premier tick, à partir de <paramref name="fromTick"/> (compris), où il fait <paramref name="kind"/> dans la zone
        /// <paramref name="zone"/>, en plein (au milieu de l'épisode quand c'est possible : cinq minutes du monde après son début) ; null si
        /// aucun dans les <see cref="MaxDays"/> jours qui viennent.
        /// </summary>
        public static long? NextTickWith(WorldClock clock, string zone, long fromTick, WeatherKind kind)
        {
            if (clock is null) throw new ArgumentNullException(nameof(clock));
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            if (fromTick < 0) throw new ArgumentOutOfRangeException(nameof(fromTick));
            WorldTime start = clock.At(fromTick);
            long first = start.Day * WorldClock.MinutesPerDay + start.Minute;
            int run = 0;
            for (long m = first; m < first + (long)MaxDays * WorldClock.MinutesPerDay; m++)
            {
                long day = m / WorldClock.MinutesPerDay;
                int minute = (int)(m % WorldClock.MinutesPerDay);
                // Les journées sans le temps voulu sont sautées d'un coup (la brume ne vient que le matin d'un jour qui en a).
                if (minute == 0 && run == 0 && !Possible(WorldWeather.Day(zone, day), kind))
                {
                    m += WorldClock.MinutesPerDay - 1;
                    continue;
                }
                if (WorldWeather.At(zone, day, minute).Kind != kind)
                {
                    run = 0;
                    continue;
                }
                run++;
                if (run >= 5 || m == first) return TickOf(clock, fromTick, day, minute);
            }
            return null;
        }

        private static bool Possible(WeatherDay day, WeatherKind kind)
        {
            if (kind == WeatherKind.Mist) return day.Mist > 0;
            if (kind != WeatherKind.Shower && kind != WeatherKind.Storm) return true;   // clair, nuages, pluie fine : aussi pendant les changements
            for (int i = 0; i < WorldWeather.SlotsPerDay; i++)
            {
                WeatherKind slot = day.Slot(i);
                // Une averse passe aussi au début et à la fin d'un orage.
                if (slot == kind || kind == WeatherKind.Shower && slot == WeatherKind.Storm) return true;
            }
            return false;
        }

        /// <summary>Premier tick du jour <paramref name="day"/> à la minute <paramref name="minute"/>, après <paramref name="fromTick"/>.</summary>
        private static long TickOf(WorldClock clock, long fromTick, long day, int minute)
        {
            long tick = fromTick;
            long target = day * WorldClock.MinutesPerDay + minute;
            WorldTime now = clock.At(tick);
            long ahead = target - (now.Day * WorldClock.MinutesPerDay + now.Minute);
            tick = checked(tick + Math.Max(0, ahead - 1) * clock.TicksPerDay / WorldClock.MinutesPerDay);
            while (true)
            {
                WorldTime t = clock.At(tick);
                long at = t.Day * WorldClock.MinutesPerDay + t.Minute;
                if (at >= target) return tick;
                tick = checked(tick + 1);
            }
        }

        /// <summary>Le temps d'identifiant <paramref name="id"/> (« clear », « cloudy », « mist », « drizzle », « shower », « storm »), ou null.</summary>
        public static WeatherKind? KindOf(string id)
        {
            foreach (WeatherKind kind in (WeatherKind[])Enum.GetValues(typeof(WeatherKind)))
                if (WorldWeather.Id(kind) == id) return kind;
            return null;
        }

        /// <summary>Comme <see cref="MoveSave(string, WeatherKind, string, long)"/>, le temps donné par son identifiant (menu de l'éditeur).</summary>
        public static string MoveSave(string path, string weather) =>
            KindOf(weather) is WeatherKind kind ? MoveSave(path, kind) : $"Temps inconnu « {weather} ».";

        /// <summary>
        /// Avance la sauvegarde <paramref name="path"/> jusqu'au prochain moment où il fait <paramref name="kind"/> ; rend le message à
        /// montrer. Rien n'est écrit si la sauvegarde manque, ne se lit pas, ou si ce temps ne vient pas.
        /// </summary>
        /// <param name="zone">Zone dont on suit le temps (celle de l'histoire : les Vallons, même si la partie est dans un intérieur).</param>
        public static string MoveSave(string path, WeatherKind kind, string zone = "zone.vallons", long tickMilliseconds = 50)
        {
            if (!System.IO.File.Exists(path)) return "Aucune sauvegarde : lance d'abord l'aventure une fois.";
            var read = SaveStore.Read(path);
            if (!(read.Save is EdS.Data.Saves.WorldSave save)) return "Sauvegarde illisible : " + (read.Problem ?? "?");
            var clock = new WorldClock(tickMilliseconds);
            if (!(NextTickWith(clock, zone, save.WorldTick, kind) is long tick))
                return $"Pas de « {WorldWeather.Id(kind)} » dans les {MaxDays} jours qui viennent : rien n'a changé.";
            SaveStore.Write(path, new EdS.Data.Saves.WorldSave(save.Zone, tick, save.Sessions, save.Consumed, save.Character));
            return $"Partie avancée au {clock.At(tick)} (tick {tick}) : {WorldWeather.Id(kind)}.";
        }
    }
}
