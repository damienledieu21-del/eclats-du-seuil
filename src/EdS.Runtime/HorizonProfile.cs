using System;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.29 : nature d'un point de l'horizon lointain.</summary>
    public enum HorizonLayer
    {
        /// <summary>Collines basses d'Orsédia, tout autour.</summary>
        Hills = 0,

        /// <summary>La Forêt des Murmures, au nord-ouest : cimes géantes dans la brume.</summary>
        Forest = 1,

        /// <summary>Les Crêtes de Fer, au nord-est : pics enneigés.</summary>
        Mountains = 2,
    }

    /// <summary>
    /// Sous-lot V2.29 (horizon, plan de la grande carte § 6.3) : la silhouette des terres lointaines autour des Vallons, qui ne se charge
    /// jamais comme des cases. Les <b>Crêtes de Fer</b> au nord-est (pics enneigés, jusqu'à <see cref="MountainPeak"/> m), la
    /// <b>Forêt des Murmures</b> au nord-ouest (cimes géantes, comme les Vallons dans Orsédia, plan § 4.0 bis, Retenu, voilées de brume), des collines basses ailleurs. La silhouette est donnée pour un
    /// anneau de <see cref="Radius"/> m autour du joueur, en <see cref="Samples"/> points d'azimut (0 : le nord, +z ; 90 : l'est, +x),
    /// raccordés en douceur d'une nature à l'autre. Calcul déterministe (hachage entier), sans Unity : la vue Unity l'étire à sa propre
    /// distance (<c>HorizonView</c>). Affichage seulement. Les « cartes peintes » du plan restent à demander à ChatGPT : ces silhouettes
    /// les remplacent en attendant.
    /// </summary>
    public sealed class HorizonProfile
    {
        /// <summary>Points d'azimut (un tous les demi-degrés).</summary>
        public const int Samples = 720;

        /// <summary>Rayon de référence de l'anneau (m) : les hauteurs sont données pour cette distance.</summary>
        public const double Radius = 6000;

        /// <summary>Hauteur maximale des Crêtes de Fer et des collines (m, au rayon de référence).</summary>
        public const double MountainPeak = 900, HillsBase = 40, ForestTop = 190;

        /// <summary>Au-dessus de cette hauteur, la neige des Crêtes.</summary>
        public const double SnowLine = 560;

        /// <summary>Centres et demi-largeurs (degrés d'azimut) des Crêtes de Fer et de la Forêt des Murmures.</summary>
        public const double MountainsAt = 45, MountainsHalfWidth = 38, ForestAt = 315, ForestHalfWidth = 42;

        private readonly double[] _height = new double[Samples];
        private readonly HorizonLayer[] _layer = new HorizonLayer[Samples];

        private HorizonProfile()
        {
            for (int i = 0; i < Samples; i++)
            {
                double azimuth = i * 360.0 / Samples;
                double hills = HillsBase + 30 * Smooth(i, 24, 11) + 12 * Smooth(i, 8, 23);   // périodes qui divisent 720 (relecture de la PR #182)
                double mountains = Window(azimuth, MountainsAt, MountainsHalfWidth);
                double forest = Window(azimuth, ForestAt, ForestHalfWidth);
                // Crêtes : des pics nets (un tous les 4 à 5 degrés) sur un socle, la crête la plus haute au milieu de la chaîne.
                double ridge = 260 + 340 * Smooth(i, 36, 5) + 300 * Peaks(i);
                double canopy = 120 + 45 * Smooth(i, 10, 7) + 25 * Smooth(i, 3, 13);
                double h = hills;
                h = Math.Max(h, hills + (Math.Min(MountainPeak, ridge) - hills) * mountains);
                h = Math.Max(h, hills + (canopy - hills) * forest);
                _height[i] = Math.Min(MountainPeak, h);
                _layer[i] = mountains > 0.35 && _height[i] > hills + 80 ? HorizonLayer.Mountains
                    : forest > 0.35 ? HorizonLayer.Forest : HorizonLayer.Hills;
            }
        }

        /// <summary>La silhouette des Vallons (la même à chaque appel).</summary>
        public static HorizonProfile Vallons { get; } = new HorizonProfile();

        /// <summary>Hauteur (m, au rayon de référence) du point <paramref name="index"/> (pris modulo <see cref="Samples"/>).</summary>
        public double Height(int index) => _height[Wrap(index)];

        public HorizonLayer Layer(int index) => _layer[Wrap(index)];

        /// <summary>Le point est-il enneigé (Crêtes de Fer au-dessus de <see cref="SnowLine"/>) ?</summary>
        public bool Snow(int index) => _layer[Wrap(index)] == HorizonLayer.Mountains && _height[Wrap(index)] >= SnowLine;

        /// <summary>Azimut (degrés, 0 au nord) du point <paramref name="index"/>.</summary>
        public static double Azimuth(int index) => Wrap(index) * 360.0 / Samples;

        /// <summary>Direction horizontale (x, z) unitaire du point <paramref name="index"/>.</summary>
        public static (double X, double Z) Direction(int index)
        {
            double a = Azimuth(index) * Math.PI / 180;
            return (Math.Sin(a), Math.Cos(a));
        }

        private static int Wrap(int index) => ((index % Samples) + Samples) % Samples;

        /// <summary>Fenêtre douce : 1 au centre, 0 au-delà de la demi-largeur (cosinus), sur le cercle.</summary>
        private static double Window(double azimuth, double center, double halfWidth)
        {
            double d = Math.Abs(((azimuth - center) % 360 + 540) % 360 - 180);
            return d >= halfWidth ? 0 : 0.5 + 0.5 * Math.Cos(Math.PI * d / halfWidth);
        }

        /// <summary>Bruit lisse dans [0, 1] : valeurs hachées tous les <paramref name="period"/> points, raccordées en cosinus, périodique
        /// (<paramref name="period"/> divise <see cref="Samples"/>).</summary>
        private static double Smooth(int index, int period, int salt)
        {
            if (Samples % period != 0) throw new ArgumentOutOfRangeException(nameof(period));
            int knots = Samples / period;
            int k = index / period;
            double t = (index % period) / (double)period;
            double a = Unit(k % knots, salt), b = Unit((k + 1) % knots, salt);
            double s = 0.5 - 0.5 * Math.Cos(Math.PI * t);
            return a + (b - a) * s;
        }

        /// <summary>Pics : triangles de 8 à 10 points de large, de hauteur hachée dans [0, 1].</summary>
        private static double Peaks(int index)
        {
            const int width = 9;
            int k = index / width;
            double t = (index % width) / (double)width;
            double tip = 0.3 + 0.4 * Unit(k, 31);
            double shape = t <= tip ? t / tip : (1 - t) / (1 - tip);
            return shape * (0.35 + 0.65 * Unit(k, 37));
        }

        /// <summary>Hachage entier de <paramref name="k"/> dans [0, 1].</summary>
        private static double Unit(int k, int salt)
        {
            unchecked
            {
                uint h = (uint)k * 2654435761u ^ (uint)salt * 40503u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return (h % 10000u) / 9999.0;
            }
        }
    }
}
