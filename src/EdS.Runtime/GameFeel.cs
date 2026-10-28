using System;

namespace EdS.Runtime
{
    /// <summary>
    /// Essai réaliste 7 (demande de Damien du 28 octobre 2026 : « effets visuels […] impacts, étincelles », « une caméra qui tremble ») :
    /// règles de la sensation des coups, sans Unity et testées. La caméra tremble d'après un « choc » entre 0 et 1, ajouté à chaque coup
    /// et qui retombe seul ; le tremblement vaut le carré du choc (un petit coup se sent à peine, un gros secoue), borné, et ne dépend que
    /// du temps affiché. Affichage seulement : rien ne revient vers le moteur.
    /// </summary>
    public static class GameFeel
    {
        /// <summary>Choc perdu par seconde.</summary>
        public const double ShockDecay = 1.8;

        /// <summary>Écart le plus grand de la caméra (mètres) et sa plus grande inclinaison (degrés), choc plein.</summary>
        public const double MaxOffset = 0.22, MaxRoll = 1.6;

        /// <summary>
        /// Choc ajouté par un retour de combat. Pour le joueur : un coup reçu selon la part de vie perdue (de 0,2 à 0,8), un coup gardé
        /// 0,15, une parade parfaite 0,3, une garde brisée 0,5, la mort 0,6, un coup critique qu'il porte 0,12. Pour un adversaire : la
        /// mort du Gardien 0,5. Sinon rien.
        /// </summary>
        public static double Shock(FeedbackKind kind, bool onPlayer, long amount, long maxHealth, bool boss)
        {
            if (onPlayer)
            {
                switch (kind)
                {
                    case FeedbackKind.DamageTaken:
                        double part = maxHealth > 0 ? Math.Max(0, Math.Min(1, amount / (double)maxHealth)) : 0;
                        return Math.Min(0.8, 0.2 + 2.4 * part);
                    case FeedbackKind.Blocked: return 0.15;
                    case FeedbackKind.PerfectParry: return 0.3;
                    case FeedbackKind.GuardBroken: return 0.5;
                    case FeedbackKind.Died: return 0.6;
                    case FeedbackKind.CriticalHit: return 0.12;
                    default: return 0;
                }
            }
            if (kind == FeedbackKind.Died && boss) return 0.5;
            return 0;
        }

        /// <summary>Choc après un ajout : la somme, bornée à 1.</summary>
        public static double Add(double shock, double added) => Math.Max(0, Math.Min(1, shock + Math.Max(0, added)));

        /// <summary>Choc qui retombe pendant <paramref name="seconds"/>.</summary>
        public static double Settle(double shock, double seconds) => Math.Max(0, shock - ShockDecay * Math.Max(0, seconds));

        /// <summary>
        /// Écart de la caméra au temps <paramref name="time"/> (secondes) pour le choc <paramref name="shock"/> : à droite et en haut
        /// (mètres), inclinaison (degrés). Trois sinus de fréquences sans rapport simple, d'amplitude choc² : nul sans choc, jamais plus
        /// grand que <see cref="MaxOffset"/> et <see cref="MaxRoll"/>.
        /// </summary>
        public static (double Right, double Up, double Roll) Offset(double shock, double time)
        {
            double s = Math.Max(0, Math.Min(1, shock));
            double k = s * s;
            double right = (Math.Sin(time * 47.3) + Math.Sin(time * 71.9 + 1.3)) * 0.5;
            double up = (Math.Sin(time * 53.1 + 2.1) + Math.Sin(time * 89.7)) * 0.5;
            double roll = (Math.Sin(time * 39.7 + 0.7) + Math.Sin(time * 63.3 + 2.9)) * 0.5;
            return (k * MaxOffset * right, k * MaxOffset * up, k * MaxRoll * roll);
        }
    }
}
