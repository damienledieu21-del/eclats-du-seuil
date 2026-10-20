using System;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot 11.6 : direction de marche saisie (clavier, souris, plus tard pouce ou manette) ramenée à l'un des <see cref="Headings"/>
    /// caps fixes. Le pont n'envoie une commande au moteur que quand le cap change : un joueur qui tourne la caméra en marchant ne produit
    /// pas une commande par tick. La saisie est en flottants (elle vient de l'appareil), mais la commande porte une direction en virgule
    /// fixe tirée d'une table d'entiers : le combat complété se rejoue à l'identique sur n'importe quel appareil.
    /// </summary>
    public static class MoveInput
    {
        /// <summary>Nombre de caps (un tous les 5,625°).</summary>
        public const int Headings = 64;

        /// <summary>Saisie plus courte que ce seuil (longueur du vecteur) : pas de marche (zone morte d'un stick ou d'un pouce).</summary>
        public const double DeadZone = 0.2;

        /// <summary>Cosinus des caps 0 à 16 (premier quart de tour), en unités brutes, arrondis au plus proche.</summary>
        private static readonly long[] Quarter = { 10000, 9952, 9808, 9569, 9239, 8819, 8315, 7730, 7071, 6344, 5556, 4714, 3827, 2903, 1951, 980, 0 };

        /// <summary>
        /// Cap de la saisie (<paramref name="x"/>, <paramref name="z"/>) dans le repère du sol (x vers l'est, z vers le nord), de 0 (est) à
        /// <see cref="Headings"/> − 1 dans le sens trigonométrique ; null sous la zone morte (arrêt). Une saisie non finie est refusée.
        /// </summary>
        public static int? Heading(double x, double z)
        {
            if (double.IsNaN(x) || double.IsNaN(z) || double.IsInfinity(x) || double.IsInfinity(z))
                throw new ArgumentOutOfRangeException(nameof(x), "Saisie de déplacement non finie.");
            if (x * x + z * z < DeadZone * DeadZone) return null;
            double turns = Math.Atan2(z, x) / (2 * Math.PI);   // dans [−0,5 ; 0,5]
            int k = (int)Math.Round(turns * Headings, MidpointRounding.AwayFromZero);
            return (k % Headings + Headings) % Headings;
        }

        /// <summary>Direction (unitaire à 10⁻⁴ près, composantes dans [−1, 1]) du cap <paramref name="heading"/>.</summary>
        public static GroundVector Direction(int heading)
        {
            if (heading < 0 || heading >= Headings) throw new ArgumentOutOfRangeException(nameof(heading), $"Cap {heading} hors de [0, {Headings - 1}].");
            return new GroundVector(Fixed.FromRaw(Cos(heading)), Fixed.FromRaw(Cos(heading - Headings / 4)));
        }

        /// <summary>Cosinus du cap <paramref name="k"/> (tout entier) par symétrie du premier quart de tour.</summary>
        private static long Cos(int k)
        {
            int m = (k % Headings + Headings) % Headings;
            if (m <= 16) return Quarter[m];
            if (m <= 32) return -Quarter[32 - m];
            if (m <= 48) return -Quarter[m - 32];
            return Quarter[Headings - m];
        }
    }
}
