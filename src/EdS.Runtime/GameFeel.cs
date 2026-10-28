using System;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;

namespace EdS.Runtime
{
    /// <summary>
    /// Essai réaliste 7 (demande de Damien du 28 octobre 2026 : « effets visuels […] impacts, étincelles », « une caméra qui tremble ») :
    /// règles de la sensation des coups, sans Unity et testées. La caméra tremble d'après un « choc » entre 0 et 1, ajouté à chaque coup
    /// et qui retombe seul ; le tremblement suit le choc (relecture de l'essai 7 : au carré, un coup gardé ou une parade restaient sous le
    /// pixel), borné, et ne dépend que du temps affiché. Affichage seulement : rien ne revient vers le moteur.
    /// </summary>
    public static class GameFeel
    {
        /// <summary>Choc perdu par seconde (une parade parfaite se sent un quart de seconde).</summary>
        public const double ShockDecay = 1.2;

        /// <summary>Écart le plus grand de la caméra (mètres) et sa plus grande inclinaison (degrés), choc plein.</summary>
        public const double MaxOffset = 0.25, MaxRoll = 2.0;

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
        /// (mètres), inclinaison (degrés). Des sinus de fréquences sans rapport simple, d'amplitude proportionnelle au choc : nul sans
        /// choc, jamais plus grand que <see cref="MaxOffset"/> et <see cref="MaxRoll"/> (un coup gardé, choc 0,15 : près de 4 cm).
        /// </summary>
        public static (double Right, double Up, double Roll) Offset(double shock, double time)
        {
            double k = Math.Max(0, Math.Min(1, shock));
            double right = (Math.Sin(time * 47.3) + Math.Sin(time * 71.9 + 1.3)) * 0.5;
            double up = (Math.Sin(time * 53.1 + 2.1) + Math.Sin(time * 89.7)) * 0.5;
            double roll = (Math.Sin(time * 39.7 + 0.7) + Math.Sin(time * 63.3 + 2.9)) * 0.5;
            return (k * MaxOffset * right, k * MaxOffset * up, k * MaxRoll * roll);
        }

        /// <summary>
        /// Relecture de l'essai 7 (« les loups n'attaquent jamais à l'écran ») : les attaques immédiates (morsure, flèche, éclat, griffe :
        /// ni annonce ni zone) ne sont jamais annoncées ; leur geste part quand le prochain tour de l'assaillant arrive dans moins de
        /// <paramref name="leadTicks"/> ticks et que sa cible est à portée (à <paramref name="slack"/> mètre près : elle bouge encore),
        /// comme le moteur le vérifiera au tour. Null sinon : mort, absent, pas engagé, attaques bloquées, attaque annoncée (son geste
        /// part de l'annonce), cible absente ou trop loin. <paramref name="now"/> en ticks (fraction comprise).
        /// </summary>
        public static AssailantAttack? Imminent(CombatantView assailant, CombatantView? target, double now, double leadTicks, double slack = 0.5)
        {
            if (assailant is null || assailant.IsDead || !assailant.IsPresent || !assailant.Engaged || assailant.AttacksBlocked) return null;
            if (!(assailant.NextAttackTick is long at) || !(assailant.NextAttack is AssailantAttack attack)) return null;
            if (attack.Shape != null && (attack.IsZone || attack.TelegraphMs > 0)) return null;
            double until = at - now;
            if (until < 0 || until > leadTicks) return null;
            if (attack.Targeting != SkillTargeting.Enemy) return attack;
            if (target is null || target.IsDead || !target.IsPresent) return null;
            if (attack.Range is Fixed range && assailant.Position is GroundVector a && target.Position is GroundVector b)
            {
                double dx = (b.X.Raw - a.X.Raw) / 10000.0, dz = (b.Z.Raw - a.Z.Raw) / 10000.0;
                double reach = (range.Raw + (assailant.Radius?.Raw ?? 0) + (target.Radius?.Raw ?? 0)) / 10000.0 + Math.Max(0, slack);
                if (dx * dx + dz * dz > reach * reach) return null;
            }
            return attack;
        }
    }
}
