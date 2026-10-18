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

        /// <summary>Sous-lot 8.3 (X5) : le lanceur lui-même, sans cible désignée. Compétence de soutien (sans dégâts).</summary>
        Self = 2,

        /// <summary>Sous-lot 8.3 (X5) : un allié vivant désigné par la commande, le lanceur compris. Compétence de soutien.</summary>
        Ally = 3,

        /// <summary>Sous-lot 8.3 (X5) : tous les alliés vivants, le lanceur compris, dans l'ordre de leurs identifiants. Soutien.</summary>
        AllAllies = 4,
    }

    /// <summary>Sous-lot 8.3 : ciblages et camp visé.</summary>
    public static class SkillTargetings
    {
        /// <summary>Ciblage d'un camp allié (soi, un allié, tous les alliés) : compétence de soutien, sans dégâts directs.</summary>
        public static bool IsFriendly(this SkillTargeting targeting) =>
            targeting == SkillTargeting.Self || targeting == SkillTargeting.Ally || targeting == SkillTargeting.AllAllies;

        /// <summary>La commande désigne-t-elle une cible (un ennemi ou un allié) ?</summary>
        public static bool NeedsTarget(this SkillTargeting targeting) => targeting == SkillTargeting.Enemy || targeting == SkillTargeting.Ally;
    }

    /// <summary>
    /// Partie « combat » d'une compétence lançable (lot 5) : dégâts directs, coût, recharge ; depuis le lot 6,
    /// caractère imparable et Charges de rempart consommées ou produites (sources et consommateurs configurables).
    /// Les compétences sont instantanées (A6) ; la recharge démarre au lancement.
    /// Une compétence sans ces données existe dans les builds mais ne peut pas encore être lancée.
    /// <para>Sous-lot 8.3 (X5) : les dégâts directs (puissance, type, coefficient) sont facultatifs, tous ensemble. Sans eux, la compétence
    /// est de soutien : aucun impact, aucune défense, aucune Résonance d'impact direct ; ses altérations s'appliquent à ses cibles. Une
    /// compétence qui vise un allié, soi ou tous les alliés est toujours de soutien.</para>
    /// </summary>
    public sealed class SkillCombatDefinition
    {
        /// <summary>Coefficient maximal (correctif 6.1, M04) : 100 = 10 000 % de la puissance, très au-delà du jeu prévu.</summary>
        public static readonly Fixed MaxCoefficient = Fixed.FromInt(100);

        /// <summary>Plafond des coûts et gains entiers d'une compétence (correctif 6.1).</summary>
        public const int MaxCost = 1_000_000;

        /// <summary>Sous-lot 8.3 : la compétence inflige-t-elle des dégâts directs ? Sinon <see cref="PowerType"/>,
        /// <see cref="DamageType"/> et <see cref="Coefficient"/> sont sans objet (valeurs par défaut).</summary>
        public bool HasDamage { get; }

        public PowerType PowerType { get; }
        public DamageType DamageType { get; }

        /// <summary>Coefficient appliqué à la puissance (1.2 = 120 %) ; 0 sans dégâts directs.</summary>
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

        /// <summary>
        /// Sous-lot 8.4 (X8) : le lancement dépense toutes les Charges détenues, au moins <see cref="RampartChargeCost"/> (exigées comme
        /// d'habitude) ; faux : exactement <see cref="RampartChargeCost"/>.
        /// </summary>
        public bool RampartChargeSpendAll { get; }

        /// <summary>
        /// Sous-lot 8.4 (X8) : ajout au coefficient par Charge dépensée par ce lancement (Contrecoup : 0,6 + 0,4 par Charge) ; 0 = aucun.
        /// Seulement avec des dégâts directs et une dépense de Charges possible.
        /// </summary>
        public Fixed CoefficientPerRampartCharge { get; }

        /// <summary>
        /// Sous-lot 8.4 : Résonance gagnée au lancement (Provocation : +5), plafonnée au maximum, l'excédent perdu ; 0 = aucune. Jamais
        /// sur une ultime, qui dépense la Résonance (A5).
        /// </summary>
        public int ResonanceGain { get; }

        /// <summary>Valeurs provisoires, non équilibrées.</summary>
        public bool IsProvisional { get; }

        /// <summary>
        /// Altérations appliquées à chaque cible touchée, après les dégâts de l'impact (lot 7.2) ; sous-lot 8.3 : à chaque cible d'une
        /// compétence de soutien, ou, pour une application à cible propre (<see cref="StatusApplication.Target"/>), une fois à ses cibles.
        /// </summary>
        public IReadOnlyList<StatusApplication> Applies { get; }

        /// <param name="powerType">Null avec <paramref name="damageType"/> et <paramref name="coefficient"/> : compétence de soutien.</param>
        /// <exception cref="ArgumentException">Seule une partie des trois données de dégâts est fournie.</exception>
        public SkillCombatDefinition(PowerType? powerType, DamageType? damageType, Fixed? coefficient, int enduranceCost,
            long cooldownMs, SkillTargeting targeting = SkillTargeting.Enemy, bool isProvisional = true,
            bool unblockable = false, int rampartChargeCost = 0, int rampartChargeGain = 0, IEnumerable<StatusApplication>? applies = null,
            bool rampartChargeSpendAll = false, Fixed? coefficientPerRampartCharge = null, int resonanceGain = 0)
        {
            int given = (powerType is null ? 0 : 1) + (damageType is null ? 0 : 1) + (coefficient is null ? 0 : 1);
            if (given != 0 && given != 3)
                throw new ArgumentException("dégâts directs : puissance, type et coefficient tous ensemble, ou aucun (compétence de soutien).");
            Applies = Frozen.ListOrEmpty(applies, nameof(applies));
            HasDamage = given == 3;
            PowerType = powerType ?? default;
            DamageType = damageType ?? default;
            Coefficient = coefficient ?? Fixed.Zero;
            EnduranceCost = enduranceCost;
            CooldownMs = cooldownMs;
            Targeting = targeting;
            IsProvisional = isProvisional;
            Unblockable = unblockable;
            RampartChargeCost = rampartChargeCost;
            RampartChargeGain = rampartChargeGain;
            RampartChargeSpendAll = rampartChargeSpendAll;
            CoefficientPerRampartCharge = coefficientPerRampartCharge ?? Fixed.Zero;
            ResonanceGain = resonanceGain;
        }

        /// <summary>Sous-lot 8.4 (X8) : Charges dépensées par un lancement accepté, le combattant en détenant <paramref name="held"/>.</summary>
        public int RampartChargesSpent(int held) => RampartChargeSpendAll ? held : RampartChargeCost;
    }
}
