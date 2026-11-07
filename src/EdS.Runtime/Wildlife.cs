using System;
using System.Collections.Generic;
using EdS.Data.Saves;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.4 : bête paisible dessinée en formes simples (aucun modèle livré).</summary>
    public enum WildlifeKind
    {
        None = 0,

        /// <summary>Cerf (« cerf1 »…) : grand corps fauve, longues pattes, bois.</summary>
        Deer = 1,

        /// <summary>Lièvre (« lievre1 »…) : petit corps gris-brun, longues oreilles, saute.</summary>
        Hare = 2,
    }

    /// <summary>Forme d'une pièce d'une bête paisible (primitives de Unity).</summary>
    public enum WildlifeShape
    {
        Sphere = 0,
        Capsule = 1,
        Cube = 2,
        Cylinder = 3,
    }

    /// <summary>
    /// Sous-lot V2.4 : une pièce du corps d'une bête paisible, en mètres, pieds au sol (y = 0), museau vers +z. <see cref="Leg"/> : une patte
    /// (1 avant gauche, 2 avant droite, 3 arrière gauche, 4 arrière droite ; 0 pour le reste), qui balance à la course.
    /// </summary>
    public sealed class WildlifePart
    {
        public string Name { get; }
        public WildlifeShape Shape { get; }
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public double ScaleX { get; }
        public double ScaleY { get; }
        public double ScaleZ { get; }

        /// <summary>Inclinaison autour de l'axe x (degrés) : le cou levé, les oreilles couchées.</summary>
        public double Pitch { get; }

        public double R { get; }
        public double G { get; }
        public double B { get; }
        public int Leg { get; }

        public WildlifePart(string name, WildlifeShape shape, double x, double y, double z, double sx, double sy, double sz, (double R, double G, double B) color,
            double pitch = 0, int leg = 0)
        {
            Name = name;
            Shape = shape;
            X = x;
            Y = y;
            Z = z;
            ScaleX = sx;
            ScaleY = sy;
            ScaleZ = sz;
            Pitch = pitch;
            R = color.R;
            G = color.G;
            B = color.B;
            Leg = leg;
        }
    }

    /// <summary>
    /// Sous-lot V2.4 (animaux paisibles) : formes et allure des bêtes paisibles sans modèle (cerf, lièvre), en code pur (testé hors de Unity) ;
    /// Unity les construit en primitives (<c>WildlifeLook</c>). Affichage seulement : rien ne change dans le monde.
    /// </summary>
    public static class Wildlife
    {
        private static readonly (double, double, double) Fawn = (0.55, 0.36, 0.2), Belly = (0.82, 0.7, 0.55), Antler = (0.86, 0.8, 0.66),
            Hoof = (0.18, 0.14, 0.1), HareFur = (0.5, 0.43, 0.34), HareTail = (0.95, 0.94, 0.9), Eye = (0.05, 0.05, 0.05);

        /// <summary>Bête paisible d'un combattant, d'après son identifiant (vies suivantes comprises : « cerf2_v3 »).</summary>
        public static WildlifeKind KindOf(string id)
        {
            string key = id ?? string.Empty;
            if (key.StartsWith("cerf", StringComparison.Ordinal)) return WildlifeKind.Deer;
            if (key.StartsWith("lievre", StringComparison.Ordinal)) return WildlifeKind.Hare;
            return WildlifeKind.None;
        }

        /// <summary>Hauteur au garrot (m), pour le nom et la barre de vie au-dessus de la bête.</summary>
        public static double Height(WildlifeKind kind) => kind == WildlifeKind.Deer ? 1.9 : kind == WildlifeKind.Hare ? 0.45 : 0;

        /// <summary>Pièces du corps (vide pour <see cref="WildlifeKind.None"/>).</summary>
        public static IReadOnlyList<WildlifePart> Parts(WildlifeKind kind)
        {
            switch (kind)
            {
                case WildlifeKind.Deer:
                    return new[]
                    {
                        new WildlifePart("corps", WildlifeShape.Capsule, 0, 1.05, 0, 0.42, 0.62, 0.42, Fawn, pitch: 90),
                        new WildlifePart("ventre", WildlifeShape.Sphere, 0, 0.92, 0, 0.36, 0.3, 0.9, Belly),
                        new WildlifePart("cou", WildlifeShape.Capsule, 0, 1.45, 0.5, 0.18, 0.32, 0.18, Fawn, pitch: 35),
                        new WildlifePart("tete", WildlifeShape.Sphere, 0, 1.72, 0.72, 0.2, 0.2, 0.34, Fawn),
                        new WildlifePart("museau", WildlifeShape.Sphere, 0, 1.66, 0.9, 0.12, 0.1, 0.14, Hoof),
                        new WildlifePart("oeil_g", WildlifeShape.Sphere, -0.09, 1.76, 0.8, 0.04, 0.04, 0.04, Eye),
                        new WildlifePart("oeil_d", WildlifeShape.Sphere, 0.09, 1.76, 0.8, 0.04, 0.04, 0.04, Eye),
                        new WildlifePart("bois_g", WildlifeShape.Cylinder, -0.12, 2.02, 0.66, 0.04, 0.22, 0.04, Antler, pitch: -20),
                        new WildlifePart("bois_d", WildlifeShape.Cylinder, 0.12, 2.02, 0.66, 0.04, 0.22, 0.04, Antler, pitch: -20),
                        new WildlifePart("andouiller_g", WildlifeShape.Cylinder, -0.16, 2.12, 0.78, 0.03, 0.12, 0.03, Antler, pitch: 40),
                        new WildlifePart("andouiller_d", WildlifeShape.Cylinder, 0.16, 2.12, 0.78, 0.03, 0.12, 0.03, Antler, pitch: 40),
                        new WildlifePart("queue", WildlifeShape.Sphere, 0, 1.15, -0.62, 0.1, 0.12, 0.08, Belly),
                        new WildlifePart("patte_avg", WildlifeShape.Cylinder, -0.14, 0.38, 0.36, 0.07, 0.38, 0.07, Fawn, leg: 1),
                        new WildlifePart("patte_avd", WildlifeShape.Cylinder, 0.14, 0.38, 0.36, 0.07, 0.38, 0.07, Fawn, leg: 2),
                        new WildlifePart("patte_arg", WildlifeShape.Cylinder, -0.14, 0.38, -0.4, 0.08, 0.38, 0.08, Fawn, leg: 3),
                        new WildlifePart("patte_ard", WildlifeShape.Cylinder, 0.14, 0.38, -0.4, 0.08, 0.38, 0.08, Fawn, leg: 4),
                    };
                case WildlifeKind.Hare:
                    return new[]
                    {
                        new WildlifePart("corps", WildlifeShape.Sphere, 0, 0.2, 0, 0.26, 0.24, 0.42, HareFur),
                        new WildlifePart("tete", WildlifeShape.Sphere, 0, 0.32, 0.2, 0.16, 0.15, 0.18, HareFur),
                        new WildlifePart("oreille_g", WildlifeShape.Capsule, -0.04, 0.48, 0.16, 0.04, 0.1, 0.03, HareFur, pitch: -15),
                        new WildlifePart("oreille_d", WildlifeShape.Capsule, 0.04, 0.48, 0.16, 0.04, 0.1, 0.03, HareFur, pitch: -15),
                        new WildlifePart("oeil_g", WildlifeShape.Sphere, -0.06, 0.34, 0.27, 0.025, 0.025, 0.025, Eye),
                        new WildlifePart("oeil_d", WildlifeShape.Sphere, 0.06, 0.34, 0.27, 0.025, 0.025, 0.025, Eye),
                        new WildlifePart("queue", WildlifeShape.Sphere, 0, 0.24, -0.22, 0.08, 0.08, 0.08, HareTail),
                        new WildlifePart("patte_avg", WildlifeShape.Capsule, -0.06, 0.06, 0.14, 0.04, 0.06, 0.04, HareFur, leg: 1),
                        new WildlifePart("patte_avd", WildlifeShape.Capsule, 0.06, 0.06, 0.14, 0.04, 0.06, 0.04, HareFur, leg: 2),
                        new WildlifePart("patte_arg", WildlifeShape.Capsule, -0.08, 0.06, -0.12, 0.06, 0.07, 0.12, HareFur, leg: 3),
                        new WildlifePart("patte_ard", WildlifeShape.Capsule, 0.08, 0.06, -0.12, 0.06, 0.07, 0.12, HareFur, leg: 4),
                    };
                default:
                    return Array.Empty<WildlifePart>();
            }
        }

        /// <summary>
        /// Allure à la course (vitesse affichée <paramref name="speed"/> en m/s, temps <paramref name="time"/> en s) : hauteur du rebond du
        /// corps (m) et balancement de chaque patte (degrés, indice 1 à 4 ; 0 inutilisé). Le cerf galope (pattes avant et arrière opposées), le
        /// lièvre bondit (pattes ensemble, rebond plus haut). À l'arrêt : aucun mouvement. Borné : rebond au plus 0,25 m, patte au plus 40°.
        /// </summary>
        public static (double Bob, double[] Legs) Gait(WildlifeKind kind, double speed, double time)
        {
            var legs = new double[5];
            if (kind == WildlifeKind.None || !(speed > 0.2) || double.IsNaN(time) || double.IsInfinity(time)) return (0, legs);
            double run = Math.Min(1, speed / (kind == WildlifeKind.Deer ? 6 : 7));
            double rate = kind == WildlifeKind.Deer ? 2.4 : 3.2;   // foulées par seconde
            double phase = 2 * Math.PI * rate * (time % 1000);
            double swing = Math.Sin(phase);
            if (kind == WildlifeKind.Deer)
            {
                legs[1] = legs[2] = 35 * run * swing;
                legs[3] = legs[4] = -35 * run * swing;
                return (0.08 * run * Math.Abs(swing), legs);
            }
            legs[1] = legs[2] = 30 * run * swing;
            legs[3] = legs[4] = 40 * run * swing;
            return (0.22 * run * Math.Max(0, swing), legs);
        }

        /// <summary>Sous-lot V2.4.1 : pièces de la tête (rattachées à la nuque, qui se baisse pour brouter).</summary>
        private static readonly HashSet<string> HeadParts = new HashSet<string>(StringComparer.Ordinal)
        {
            "cou", "tete", "museau", "oeil_g", "oeil_d", "bois_g", "bois_d", "andouiller_g", "andouiller_d", "oreille_g", "oreille_d",
        };

        /// <summary>Sous-lot V2.4.1 : la pièce <paramref name="name"/> tourne-t-elle avec la nuque ?</summary>
        public static bool IsHead(string name) => name != null && HeadParts.Contains(name);

        /// <summary>Sous-lot V2.4.1 : nuque (pivot de la tête) d'une bête, en mètres dans son repère : là où le cou rejoint le corps.</summary>
        public static (double X, double Y, double Z) Neck(WildlifeKind kind) => kind == WildlifeKind.Deer ? (0, 1.3, 0.38) : (0, 0.28, 0.12);

        /// <summary>
        /// Sous-lot V2.4.1 (bêtes qui se promènent) : posture d'une bête selon ce qu'elle fait (<c>CombatantView.Wander</c>) : inclinaison de
        /// la nuque (degrés, positive vers le sol) et abaissement du corps (m). Un cerf qui broute baisse la tête et mâchonne (petits coups de
        /// tête) ; qui guette, la relève ; un lièvre tapi entre deux bonds se ramasse, aux aguets il se dresse. Sans état (fuite, combat,
        /// marche) : tête droite. Affichage seulement.
        /// </summary>
        public static (double HeadPitch, double Crouch) Pose(WildlifeKind kind, string? wander, double time)
        {
            if (kind == WildlifeKind.None || double.IsNaN(time) || double.IsInfinity(time)) return (0, 0);
            double t = time % 1000;
            switch (wander)
            {
                case "graze":
                case "root":
                    // Tête au sol, petits coups de mâchoire (deux par seconde), un relevé d'un instant toutes les sept secondes.
                    double lift = (t % 7) < 0.6 ? 35 : 0;
                    return ((kind == WildlifeKind.Deer ? 62 : 28) - lift + 4 * Math.Sin(2 * Math.PI * 2 * t), 0);
                case "watch":
                    // Tête haute, qui balaie un peu.
                    return (kind == WildlifeKind.Deer ? -8 : -18, kind == WildlifeKind.Hare ? -0.04 : 0);
                case "crouch":
                    return (6, kind == WildlifeKind.Hare ? 0.05 : 0);
                default:
                    return (0, 0);
            }
        }
    }

    /// <summary>
    /// Sous-lot V2.4 : outil d'essai (menu de l'éditeur « Éclats du Seuil/Essai des animaux… ») : avance la sauvegarde jusqu'au moment où l'on
    /// voit les bêtes (comme l'essai de la nuit et celui de la météo) : seul le temps du monde avance, jamais en arrière, une copie est gardée.
    /// </summary>
    public static class WildlifeTrial
    {
        /// <summary>Minute de l'arrivée des cerfs (5 h 10, juste après le début de l'aube) et des lièvres (19 h 10, au crépuscule).</summary>
        public const int DeerMinute = 5 * 60 + 10, HareMinute = 19 * 60 + 10;

        /// <summary>Temps où les cerfs sortent (ils s'abritent sous l'averse et l'orage) : ceux des populations « cerf » des Vallons.</summary>
        public static bool DeerWeather(WeatherKind kind) => kind != WeatherKind.Shower && kind != WeatherKind.Storm;

        /// <summary>
        /// Premier tick, à partir de <paramref name="fromTick"/>, à <see cref="DeerMinute"/> d'un jour où il fait un temps à cerfs dans
        /// <paramref name="zone"/> ; null si aucun dans les <see cref="WeatherTrial.MaxDays"/> jours qui viennent.
        /// </summary>
        public static long? NextDeerDawn(WorldClock clock, string zone, long fromTick)
        {
            if (clock is null) throw new ArgumentNullException(nameof(clock));
            long tick = DayTrial.NextTickAt(clock, fromTick, DeerMinute);
            for (int day = 0; day < WeatherTrial.MaxDays; day++)
            {
                if (DeerWeather(WorldWeather.At(zone, clock.At(tick)).Kind)) return tick;
                tick = DayTrial.NextTickAt(clock, checked(tick + 1), DeerMinute);
            }
            return null;
        }

        /// <summary>Avance la sauvegarde <paramref name="path"/> à l'aube des cerfs ; rend le message à montrer (où aller voir les bêtes).</summary>
        public static string MoveToDeer(string path, string zone = "zone.vallons", long tickMilliseconds = 50, string? stamp = null)
        {
            if (!System.IO.File.Exists(path)) return "Aucune sauvegarde : lance d'abord l'aventure une fois.";
            SaveReadResult read = SaveStore.Read(path);
            if (!(read.Save is WorldSave save)) return "Sauvegarde illisible : " + (read.Problem ?? "?");
            var clock = new WorldClock(tickMilliseconds);
            if (!(NextDeerDawn(clock, zone, save.WorldTick) is long tick)) return "Pas d'aube sans averse dans les jours qui viennent : rien n'a changé.";
            string copy = DayTrial.KeepCopy(path, stamp);
            SaveStore.Write(path, DayTrial.Advanced(save, tick));
            return $"Partie avancée au {clock.At(tick)} (tick {tick}) : les cerfs boivent au sud du lac des Aubes et paissent à la lisière de la "
                + $"forêt des Loups (passer la porte du moulin). Copie d'avant l'essai : {System.IO.Path.GetFileName(copy)}.";
        }

        /// <summary>Avance la sauvegarde au crépuscule (19 h 10) : les lièvres sortent dans les champs du sud et les vergers.</summary>
        public static string MoveToHares(string path, long tickMilliseconds = 50, string? stamp = null) =>
            DayTrial.MoveSave(path, HareMinute, tickMilliseconds, stamp) + " Les lièvres sortent dans les champs du sud et les vergers ; les loups de la nuit arrivent à 22 h.";

        /// <summary>
        /// Sous-lot V2.4.1 (essai des bêtes qui se promènent) : avance la sauvegarde au matin (7 h 30, un jour sans averse) : les cerfs broutent,
        /// les loups patrouillent, les sangliers fouillent.
        /// </summary>
        public static string MoveToWanderMorning(string path, string zone = "zone.vallons", long tickMilliseconds = 50, string? stamp = null)
        {
            if (!System.IO.File.Exists(path)) return "Aucune sauvegarde : lance d'abord l'aventure une fois.";
            SaveReadResult read = SaveStore.Read(path);
            if (!(read.Save is WorldSave save)) return "Sauvegarde illisible : " + (read.Problem ?? "?");
            var clock = new WorldClock(tickMilliseconds);
            long tick = DayTrial.NextTickAt(clock, save.WorldTick, WanderMorningMinute);
            int day = 0;
            for (; day < WeatherTrial.MaxDays && !DeerWeather(WorldWeather.At(zone, clock.At(tick)).Kind); day++)
                tick = DayTrial.NextTickAt(clock, checked(tick + 1), WanderMorningMinute);
            if (day == WeatherTrial.MaxDays) return "Pas de matin sans averse dans les jours qui viennent : rien n'a changé.";
            string copy = DayTrial.KeepCopy(path, stamp);
            SaveStore.Write(path, DayTrial.Advanced(save, tick));
            return $"Partie avancée au {clock.At(tick)} (tick {tick}). " + WanderGuide + $" Copie d'avant l'essai : {System.IO.Path.GetFileName(copy)}.";
        }

        /// <summary>Sous-lot V2.4.1 : minute du matin de l'essai des bêtes qui se promènent (7 h 30).</summary>
        public const int WanderMorningMinute = 7 * 60 + 30;

        /// <summary>Sous-lot V2.4.1 : où regarder les bêtes qui se promènent (texte de l'essai).</summary>
        public const string WanderGuide = "Au sud du lac des Aubes, la harde broute en grappe derrière son chef et change de coin d'herbe ; "
            + "dans le pré au-dessus du moulin, les loups patrouillent en file sur leur circuit ; les sangliers fouillent les champs du sud. "
            + "Approche-toi d'un cerf : la harde détale, puis, après 15 secondes au calme, revient brouter chez elle.";

        /// <summary>Sous-lot V2.4.1 : avance la sauvegarde au crépuscule : les lièvres sautillent par à-coups dans les champs du sud et les vergers.</summary>
        public static string MoveToWanderDusk(string path, long tickMilliseconds = 50, string? stamp = null) =>
            DayTrial.MoveSave(path, HareMinute, tickMilliseconds, stamp) + " Les lièvres sautillent par à-coups dans les champs du sud et les vergers, "
            + "s'arrêtent aux aguets entre deux bonds ; à 22 h, les loups de la nuit patrouillent en meute autour du moulin.";

        /// <summary>Avance la sauvegarde au prochain matin de brume : loups gris aux champs du sud, échos au lac et aux ruines.</summary>
        public static string MoveToMist(string path, string? stamp = null) =>
            WeatherTrial.MoveSave(path, WeatherKind.Mist, stamp: stamp) + " Dans la brume : loups gris au nord des champs du sud (près des sangliers), échos au lac et aux ruines.";
    }
}
