using System;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.9 (« vrais modèles et sons », demande de Damien du 7 novembre 2026) : quand les bêtes donnent de la voix, quel passage d'un
    /// long enregistrement jouer, quelle variante d'un son court, et l'allure du sanglier sans squelette (balancement, pas). Code pur, testé
    /// hors de Unity ; affichage et son seulement : rien ne change dans le monde, aucun hasard (les choix suivent un compteur).
    /// </summary>
    public static class BeastCalls
    {
        /// <summary>Vitesse (m/s) au-delà de laquelle un lièvre détale ; il doit être retombé sous <see cref="HareCalm"/> pour détaler de nouveau.</summary>
        public const double HareDashSpeed = 3.0, HareCalm = 1.2;

        /// <summary>
        /// Passages où l'on entend vraiment la bête dans les enregistrements livrés (début et durée, en secondes), mesurés sur les fichiers
        /// (énergie par quart de seconde, 7 novembre 2026) : le brame de naturenotesuk (n° 757773 de Freesound) a de longs silences entre six
        /// appels ; les hurlements de Paresh (n° 158780) et de Sacha Julien (n° 762242) commencent après un temps calme. Reconnus par le
        /// numéro dans le nom du fichier.
        /// </summary>
        private static readonly (string Key, (double Start, double Length)[] Cues)[] KnownCues =
        {
            ("757773", new[] { (4.25, 4.5), (32.25, 6.0), (60.75, 7.0), (90.5, 6.0), (109.75, 7.0), (134.0, 8.0) }),
            ("158780", new[] { (1.5, 9.0), (9.0, 9.0), (16.5, 9.0), (22.0, 9.0) }),
            ("762242", new[] { (13.0, 9.0), (20.0, 9.0), (27.0, 9.0), (31.5, 9.0) }),
            // 2e vague (mesurés de même, au vingt-cinquième de seconde) : un pas à la fois dans les enregistrements de pas (herbe de Fission9,
            // gravier de Yoyodaman234, feuilles et parquet de BigSoundBank), un meuglement, un aboiement, un cri de hulotte, les volées des
            // cloches d'église (celle de kevp888 commence après 17 s de rue, celle de klankbeeld après deux minutes).
            ("521587", new[] { (0.07, 0.45), (0.77, 0.45), (1.47, 0.45), (2.245, 0.45), (3.02, 0.45), (3.77, 0.45) }),
            ("223154", new[] { (0.0, 0.6) }),
            ("BSB0137", new[] { (0.295, 0.5), (1.17, 0.5), (1.895, 0.5), (2.845, 0.5), (3.62, 0.5), (4.595, 0.5), (5.495, 0.5), (6.47, 0.5), (7.245, 0.5) }),
            ("BSB0376", new[] { (0.02, 0.4), (1.695, 0.4), (3.295, 0.4), (4.12, 0.4), (4.92, 0.4), (5.795, 0.4), (6.395, 0.4), (7.17, 0.4), (8.295, 0.4), (9.17, 0.4), (10.02, 0.4), (10.87, 0.4) }),
            ("BSB0546", new[] { (1.5, 3.0), (5.5, 3.0), (9.5, 3.5) }),
            ("BSB2954", new[] { (0.0, 1.6), (4.5, 1.6), (10.5, 1.6), (13.5, 1.6) }),
            ("BSB1764", new[] { (0.0, 3.0), (5.0, 2.9) }),
            ("646519", new[] { (17.0, 12.0), (30.0, 12.0), (44.0, 12.0) }),
            ("622816", new[] { (200.0, 12.0), (400.0, 12.0), (600.0, 12.0) }),
        };

        /// <summary>
        /// Passage à jouer (début, durée) pour la <paramref name="n"/>-ième fois du son <paramref name="clipName"/>, long de
        /// <paramref name="clipLength"/> secondes, d'environ <paramref name="wanted"/> secondes : un passage mesuré si le fichier est connu,
        /// sinon un début qui avance de 7,3 s à chaque fois (repris au début à la fin du fichier). Un son plus court que
        /// <paramref name="wanted"/> se joue en entier.
        /// </summary>
        public static (double Start, double Length) Cue(string clipName, double clipLength, int n, double wanted)
        {
            if (double.IsNaN(clipLength) || clipLength <= 0 || double.IsNaN(wanted) || wanted <= 0) return (0, 0);
            if (clipLength <= wanted) return (0, clipLength);
            string name = clipName ?? string.Empty;
            int k = n < 0 ? 0 : n;
            foreach ((string key, (double Start, double Length)[] cues) in KnownCues)
                if (name.IndexOf(key, StringComparison.Ordinal) >= 0)
                {
                    (double start, double length) = cues[Mix(k) % cues.Length];
                    if (start < clipLength) return (start, Math.Min(length, clipLength - start));
                }
            double room = clipLength - wanted;
            return (k * 7.3 % room, wanted);
        }

        /// <summary>Variante <paramref name="n"/> parmi <paramref name="count"/>, mélangée (pas 0, 1, 2… dans l'ordre) mais toujours la même pour un même n.</summary>
        public static int Pick(int n, int count) => count <= 0 ? -1 : Mix(n) % count;

        /// <summary>
        /// Secondes avant le prochain appel du son <paramref name="sound"/> (<paramref name="n"/> : appels déjà faits) : hurlements toutes
        /// les 35 à 80 s, brame toutes les 100 à 220 s (rare), grognements du sanglier toutes les 9 à 22 s ; 2e vague : corbeaux 10 à 25 s,
        /// vache 20 à 50 s, chien 15 à 40 s, cheval 30 à 70 s, coq 40 à 80 s, poules 18 à 38 s, mouton 25 à 55 s, hulotte 30 à 70 s ; 30 s pour
        /// un autre son.
        /// </summary>
        public static double Interval(StyleSound sound, int n)
        {
            double u = Mix(n) % 1000 / 1000.0;
            switch (sound)
            {
                case StyleSound.Caw: return 10 + 15 * u;
                case StyleSound.Moo: return 20 + 30 * u;
                case StyleSound.Bark: return 15 + 25 * u;
                case StyleSound.Neigh: return 30 + 40 * u;
                case StyleSound.Rooster: return 40 + 40 * u;
                case StyleSound.Hens: return 18 + 20 * u;
                case StyleSound.Sheep: return 25 + 30 * u;
                case StyleSound.Owl: return 30 + 40 * u;
                case StyleSound.Howl: return 35 + 45 * u;
                case StyleSound.Bellow: return 100 + 120 * u;
                case StyleSound.BoarGrumble: return 9 + 13 * u;
                default: return 30;
            }
        }

        /// <summary>Les loups hurlent la nuit et dans la brume (<paramref name="night"/>, <paramref name="mist"/>).</summary>
        public static bool HowlTime(bool night, bool mist) => night || mist;

        /// <summary>Le cerf brame à l'aube (4 h 30 à 7 h 30 du monde) et au crépuscule (18 h 30 à 21 h 30) ; <paramref name="minute"/> du jour.</summary>
        public static bool BellowTime(int minute)
        {
            int m = ((minute % 1440) + 1440) % 1440;
            return (m >= 270 && m < 450) || (m >= 1110 && m < 1290);
        }

        /// <summary>
        /// Un lièvre détale : il était calme (<paramref name="wasCalm"/>) et sa vitesse affichée passe <see cref="HareDashSpeed"/>. Rend aussi
        /// s'il est de nouveau calme (sous <see cref="HareCalm"/>), pour la prochaine fois.
        /// </summary>
        public static (bool Dash, bool Calm) HareDash(bool wasCalm, double speed)
        {
            if (double.IsNaN(speed)) return (false, wasCalm);
            if (wasCalm && speed > HareDashSpeed) return (true, false);
            return (false, wasCalm || speed < HareCalm);
        }

        /// <summary>
        /// Volume d'un passage (0 à 1) à <paramref name="t"/> secondes de son début : montée en 0,3 s (au plus le quart du passage), descente
        /// sur la dernière seconde (au plus le tiers). Relecture de la PR #114 : un passage de moins d'une seconde (un pas, un coup) n'a pas
        /// de montée : il part à plein volume dès sa première image.
        /// </summary>
        public static double Envelope(double t, double length)
        {
            if (double.IsNaN(t) || double.IsNaN(length) || t < 0 || t >= length) return 0;
            double fadeIn = length < 1 ? 0 : Math.Min(0.3, length / 4);
            double fadeOut = Math.Min(1.0, length / 3);
            double rise = fadeIn > 0 ? t / fadeIn : 1;
            return Math.Max(0, Math.Min(1, Math.Min(rise, (length - t) / fadeOut)));
        }

        /// <summary>
        /// Sous-lot V2.9 (2e vague) : un pas tous les combien de mètres d'après la vitesse (m/s) : 0,75 m au pas, jusqu'à 1,4 m en courant ;
        /// à l'arrêt (sous 0,3 m/s), aucun pas.
        /// </summary>
        public static double StepLength(double speed) =>
            double.IsNaN(speed) || speed < 0.3 ? double.PositiveInfinity : 0.75 + 0.65 * Math.Max(0, Math.Min(1, (speed - 2) / 4));

        /// <summary>
        /// Sous-lot V2.9 (2e vague) : moments des cris de la ferme : le coq chante à l'aube (4 h 45 à 8 h), poules, vaches, chevaux, chiens et
        /// moutons le jour (6 h à 21 h), corbeaux le jour, la hulotte la nuit (20 h 30 à 5 h 30).
        /// </summary>
        public static bool CallTime(StyleSound sound, int minute)
        {
            int m = ((minute % 1440) + 1440) % 1440;
            switch (sound)
            {
                case StyleSound.Rooster: return m >= 285 && m < 480;
                case StyleSound.Owl: return m >= 1230 || m < 330;
                case StyleSound.Caw: return m >= 330 && m < 1230;
                default: return m >= 360 && m < 1260;
            }
        }

        /// <summary>Tocsin : nombre de coups et écart (s) entre deux coups (une cloche frappée vite, comme une alarme).</summary>
        public const int TocsinStrikes = 9;

        public const double TocsinGap = 0.75;

        /// <summary>
        /// Allure d'un modèle sans squelette (le sanglier de Hanabi_Map) : décalage vertical (fraction de sa hauteur), tangage, roulis et
        /// lacet (degrés ; tangage positif : le groin vers le sol) d'après sa vitesse (m/s), le temps (s) et ce qu'il fait
        /// (<c>root</c> : il fouille, la tête au sol par à-coups ; <c>graze</c> de même). Au pas, il se dandine ; au trot, il bondit un peu.
        /// </summary>
        public static (double Bob, double Pitch, double Roll, double Yaw) StaticGait(double speed, double time, string? wander)
        {
            if (double.IsNaN(speed) || double.IsInfinity(speed) || double.IsNaN(time) || double.IsInfinity(time)) return (0, 0, 0, 0);
            double t = time % 1000;
            double v = Math.Max(0, Math.Min(speed, 12));
            if (v < 0.3)
            {
                if (wander == "root" || wander == "graze")
                {
                    // Le groin fouille : la tête descend, trois petits coups par seconde, relevée un instant toutes les cinq secondes.
                    double up = (t % 5) < 0.7 ? 0.4 : 1;
                    return (-0.01 * up, (7 + 2.5 * Math.Sin(2 * Math.PI * 3 * t)) * up, 0, 1.5 * Math.Sin(2 * Math.PI * 0.4 * t));
                }
                // Il respire : un souffle lent.
                return (0.006 * Math.Sin(2 * Math.PI * 0.35 * t), 0, 0, 0);
            }
            // Pas : la cadence suit la vitesse (1,6 pas par seconde au pas, jusqu'à 4 au trot).
            double cadence = Math.Min(4, 1.2 + 0.45 * v);
            double phase = 2 * Math.PI * cadence * t;
            double trot = Math.Min(1, v / 5);
            double bob = (0.012 + 0.03 * trot) * Math.Abs(Math.Sin(phase));
            double pitch = (1.5 + 3 * trot) * Math.Sin(2 * phase) + 2 * trot;
            double roll = (3 - 1.5 * trot) * Math.Sin(phase);
            double yaw = 2 * (1 - trot) * Math.Sin(phase);
            return (bob, pitch, roll, yaw);
        }

        /// <summary>Mélange d'un compteur (empreinte entière, sans hasard ni allocation).</summary>
        private static int Mix(int n)
        {
            unchecked
            {
                uint x = (uint)n * 2654435761u + 0x9E3779B9u;
                x ^= x >> 15;
                x *= 2246822519u;
                x ^= x >> 13;
                return (int)(x & 0x7FFFFFFF);
            }
        }
    }
}
