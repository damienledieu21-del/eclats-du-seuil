using EdS.Domain.Numerics;

namespace EdS.Domain.Combat
{
    /// <summary>
    /// Détail de chaque étape du calcul, dans l'ordre de résolution. Sert aux tests, au futur journal de combat
    /// et à la vérification manuelle des exemples chiffrés.
    /// </summary>
    public sealed class DamageBreakdown
    {
        public Fixed Power { get; internal set; }
        public Fixed AfterCoefficient { get; internal set; }
        public Fixed CompetenceMultiplier { get; internal set; }
        public Fixed TalentMultiplier { get; internal set; }
        public Fixed AlterationMultiplier { get; internal set; }
        public Fixed BonusMultiplier { get; internal set; }
        public Fixed AfterBonuses { get; internal set; }
        public bool IsCritical { get; internal set; }
        public Fixed CriticalMultiplier { get; internal set; }
        public Fixed AfterCritical { get; internal set; }
        public Fixed EffectiveDefense { get; internal set; }
        public long DefenseConstant { get; internal set; }

        /// <summary>Facteur de défense affiché à 4 décimales ; le calcul, lui, est exact (produit-quotient).</summary>
        public Fixed DefenseFactor { get; internal set; }

        public Fixed AfterDefense { get; internal set; }

        /// <summary>Réduction de garde ou de parade (0 = aucune), puis montant après cette réduction.</summary>
        public Fixed Mitigation { get; internal set; }

        public Fixed AfterMitigation { get; internal set; }

        /// <summary>Dégâts finaux entiers, après arrondi et minimum.</summary>
        public long FinalDamage { get; internal set; }
    }
}
