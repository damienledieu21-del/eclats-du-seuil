using System;
using System.Collections.Generic;
using EdS.Domain.Collections;
using EdS.Domain.Combat;
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
    /// Partie « combat » d'une compétence lançable (lot 5) : dégâts directs, coût, recharge ; depuis le lot 6,
    /// caractère imparable et Charges de rempart consommées ou produites (sources et consommateurs configurables).
    /// Les compétences sont instantanées (A6) ; la recharge démarre au lancement.
    /// Une compétence sans ces données existe dans les builds mais ne peut pas encore être lancée.
    /// </summary>
    public sealed class SkillCombatDefinition
    {
        /// <summary>Coefficient maximal (correctif 6.1, M04) : 100 = 10 000 % de la puissance, très au-delà du jeu prévu.</summary>
        public static readonly Fixed MaxCoefficient = Fixed.FromInt(100);

        /// <summary>Plafond des coûts et gains entiers d'une compétence (correctif 6.1).</summary>
        public const int MaxCost = 1_000_000;

        public PowerType PowerType { get; }
        public DamageType DamageType { get; }

        /// <summary>Coefficient appliqué à la puissance (1.2 = 120 %).</summary>
        public Fixed Coefficient { get; }

        public int EnduranceCost { get; }
        public long CooldownMs { get; }
        public SkillTargeting Targeting { get; }

        /// <summary>Imparable : ignore la garde et la parade de la cible (A1, A2).</summary>
        public bool Unblockable { get; }

        /// <summary>Charges de rempart exigées et dépensées au lancement (A4) ; 0 = aucune.</summary>
        public int RampartChargeCost { get; }

        /// <summary>Charges de rempart gagnées au lancement (A4) ; 0 = aucune. L'excédent est perdu.</summary>
        public int RampartChargeGain { get; }

        /// <summary>Valeurs provisoires, non équilibrées.</summary>
        public bool IsProvisional { get; }

        /// <summary>Altérations appliquées à chaque cible touchée, après les dégâts de l'impact (lot 7.2).</summary>
        public IReadOnlyList<StatusApplication> Applies { get; }

        public SkillCombatDefinition(PowerType powerType, DamageType damageType, Fixed coefficient, int enduranceCost,
            long cooldownMs, SkillTargeting targeting = SkillTargeting.Enemy, bool isProvisional = true,
            bool unblockable = false, int rampartChargeCost = 0, int rampartChargeGain = 0, IEnumerable<StatusApplication>? applies = null)
        {
            Applies = Frozen.ListOrEmpty(applies, nameof(applies));
            PowerType = powerType;
            DamageType = damageType;
            Coefficient = coefficient;
            EnduranceCost = enduranceCost;
            CooldownMs = cooldownMs;
            Targeting = targeting;
            IsProvisional = isProvisional;
            Unblockable = unblockable;
            RampartChargeCost = rampartChargeCost;
            RampartChargeGain = rampartChargeGain;
        }
    }
}
