using System;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;

namespace EdS.Domain.Builds
{
    /// <summary>Cibles d'une compétence (A7 : cibles logiques, sans position ni distance).</summary>
    public enum SkillTargeting
    {
        /// <summary>Un ennemi désigné par la commande.</summary>
        Enemy = 0,

        /// <summary>Tous les ennemis éligibles (vivants), dans l'ordre de leurs identifiants.</summary>
        AllEnemies = 1,
    }

    /// <summary>
    /// Partie « combat » d'une compétence lançable (lot 5) : dégâts directs, coût, recharge.
    /// Les compétences sont instantanées (A6) ; la recharge démarre au lancement.
    /// Une compétence sans ces données existe dans les builds mais ne peut pas encore être lancée.
    /// </summary>
    public sealed class SkillCombatDefinition
    {
        public PowerType PowerType { get; }
        public DamageType DamageType { get; }

        /// <summary>Coefficient appliqué à la puissance (1.2 = 120 %).</summary>
        public Fixed Coefficient { get; }

        public int EnduranceCost { get; }
        public long CooldownMs { get; }
        public SkillTargeting Targeting { get; }

        /// <summary>Valeurs provisoires, non équilibrées.</summary>
        public bool IsProvisional { get; }

        public SkillCombatDefinition(PowerType powerType, DamageType damageType, Fixed coefficient, int enduranceCost,
            long cooldownMs, SkillTargeting targeting = SkillTargeting.Enemy, bool isProvisional = true)
        {
            PowerType = powerType;
            DamageType = damageType;
            Coefficient = coefficient;
            EnduranceCost = enduranceCost;
            CooldownMs = cooldownMs;
            Targeting = targeting;
            IsProvisional = isProvisional;
        }
    }
}
