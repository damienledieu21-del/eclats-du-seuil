using System;
using System.Globalization;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.13 (essai de Damien du 9 novembre 2026 : « le jeu est trop sombre ») : règle partagée, sans Unity, du curseur de luminosité
    /// des réglages de la pause. Le joueur choisit un pourcentage de 50 % à 200 % (100 % : la lumière prévue par le jeu) ; le jeu le convertit
    /// en décalage d'exposition du post-traitement (en vues, « EV » : chaque vue double ou divise la lumière). Affichage seulement : rien de
    /// la simulation, des sauvegardes de partie ni du réseau n'en dépend ; le choix est gardé dans les préférences du joueur.
    /// </summary>
    public static class BrightnessRules
    {
        /// <summary>Luminosité la plus basse (50 %).</summary>
        public const int MinPercent = 50;

        /// <summary>Luminosité la plus haute (200 %).</summary>
        public const int MaxPercent = 200;

        /// <summary>Luminosité par défaut (100 % : réglages du jeu).</summary>
        public const int DefaultPercent = 100;

        /// <summary>Pas des boutons « moins » et « plus » (points de pourcentage).</summary>
        public const int StepPercent = 10;

        /// <summary>Valeur lue des préférences ou d'un bouton, ramenée entre 50 et 200 (valeur absurde : 100).</summary>
        public static int Clamp(int percent) => percent < MinPercent ? MinPercent : percent > MaxPercent ? MaxPercent : percent;

        /// <summary>Valeur décimale lue des préférences : arrondie au pour cent, bornée ; NaN ou infini : 100.</summary>
        public static int Clamp(double percent)
        {
            if (double.IsNaN(percent) || double.IsInfinity(percent)) return DefaultPercent;
            return Clamp((int)Math.Round(percent, MidpointRounding.AwayFromZero));
        }

        /// <summary>Après un appui sur « moins » (<paramref name="direction"/> -1) ou « plus » (+1) : un pas de 10 points, borné ; 0 : inchangé.</summary>
        public static int Step(int percent, int direction)
        {
            int p = Clamp(percent);
            return direction == 0 ? p : Clamp(p + (direction < 0 ? -StepPercent : StepPercent));
        }

        /// <summary>Facteur de lumière : 100 % donne 1, 50 % donne 0,5, 200 % donne 2.</summary>
        public static double Factor(int percent) => Clamp(percent) / 100.0;

        /// <summary>
        /// Décalage d'exposition (en vues) à ajouter à l'exposition du post-traitement : log2 du facteur, soit 0 à 100 %, -1 à 50 %, +1 à 200 %.
        /// </summary>
        public static double ExposureOffset(int percent) => Math.Log(Factor(percent), 2.0);

        /// <summary>Texte du réglage, par exemple « Luminosité : 120 % ».</summary>
        public static string Label(int percent) => "Luminosité : " + Clamp(percent).ToString(CultureInfo.InvariantCulture) + " %";
    }
}
