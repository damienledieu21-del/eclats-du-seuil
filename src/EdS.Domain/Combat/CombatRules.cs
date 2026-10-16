using System;
using System.Collections.Generic;
using EdS.Domain.Numerics;
using EdS.Domain.Time;

namespace EdS.Domain.Combat
{
    /// <summary>
    /// Paramètres du moteur de combat : rythme et endurance (lot 5, A3 et A6), limites de simulation, garde,
    /// parade, Charges de rempart, Résonance et états défensifs (lot 6, A1, A2, A4, A5 et A19).
    /// Valeurs par défaut <b>provisoires</b> ; toutes les durées sont en millisecondes et doivent tomber
    /// exactement sur un tick.
    /// </summary>
    public sealed class CombatRules
    {
        public static readonly CombatRules Provisional = new CombatRules();

        /// <summary>Durée d'un tick logique (50 ms validés ; 20 ms à l'essai plus tard).</summary>
        public int TickMilliseconds { get; init; } = TickRate.DefaultMillisecondsPerTick;

        /// <summary>Temps global de recharge après chaque compétence lancée (A6). 0 = aucun ; au plus <see cref="AbsoluteMaxDurationMs"/>.</summary>
        public long GlobalCooldownMs { get; init; } = 1000;

        /// <summary>Endurance regagnée par seconde (A3), en points entiers.</summary>
        public int EnduranceRegenPerSecond { get; init; } = 10;

        /// <summary>Temps sans dépense d'endurance avant que la régénération démarre (A3) ; au plus <see cref="AbsoluteMaxDurationMs"/>.</summary>
        public long EnduranceRegenDelayMs { get; init; } = 1000;

        /// <summary>Durée maximale d'un combat simulé : borne le nombre de ticks.</summary>
        public long MaxDurationMs { get; init; } = 600_000;

        /// <summary>Nombre maximal d'événements journalisés en un tick ; au-delà le combat est interrompu proprement.</summary>
        public int MaxEventsPerTick { get; init; } = 1000;

        /// <summary>Réduction des dégâts directs bloquables, après armure, pendant la garde (A1 : 0.6 = −60 %).</summary>
        public Fixed GuardDamageReduction { get; init; } = Fixed.FromRatio(6, 10);

        /// <summary>Endurance consommée par seconde de garde (A1), prélevée au fil des ticks sans dérive d'arrondi.</summary>
        public int GuardUpkeepPerSecond { get; init; } = 2;

        /// <summary>Endurance payée par impact bloqué (A1) ; si elle manque, la garde est brisée avant la réduction.</summary>
        public int GuardCostPerBlock { get; init; } = 10;

        /// <summary>Durée de la rupture de garde (A1, A19) : ni garde ni parade, pas de régénération.</summary>
        public long GuardBreakDurationMs { get; init; } = 3000;

        /// <summary>Fenêtre d'une parade parfaite (A2) : ticks [commande, commande + fenêtre[.</summary>
        public long ParryWindowMs { get; init; } = 150;

        /// <summary>Réduction des dégâts directs éligibles par une parade parfaite (A2 : 0.9 = −90 %).</summary>
        public Fixed ParryDamageReduction { get; init; } = Fixed.FromRatio(9, 10);

        /// <summary>Délai après une parade ratée avant de pouvoir parer de nouveau (A2), compté depuis la fin de la fenêtre.</summary>
        public long ParryFailureRecoveryMs { get; init; } = 500;

        /// <summary>Charges de rempart gagnées par parade parfaite réussie (A2, A4).</summary>
        public int RampartChargesPerParry { get; init; } = 1;

        /// <summary>Maximum de Charges de rempart (A4) ; l'excédent est perdu.</summary>
        public int RampartChargesMax { get; init; } = 5;

        /// <summary>Maximum de Résonance (A5).</summary>
        public int ResonanceMax { get; init; } = 100;

        /// <summary>Résonance gagnée par attaque directe réussie, une fois par action quel que soit le nombre de cibles (A5).</summary>
        public int ResonancePerDirectHit { get; init; } = 3;

        /// <summary>Résonance gagnée par parade parfaite réussie (A5).</summary>
        public int ResonancePerPerfectParry { get; init; } = 8;

        /// <summary>Résonance gagnée par blocage normal réussi (A5).</summary>
        public int ResonancePerBlock { get; init; } = 2;

        /// <summary>Résonance dépensée par l'ultime (A5).</summary>
        public int UltimateResonanceCost { get; init; } = 100;

        /// <summary>Durée de la Fragilité (A19) ; une réapplication renouvelle la durée sans cumul.</summary>
        public long FragilityDurationMs { get; init; } = 6000;

        /// <summary>Augmentation des dégâts directs reçus sous Fragilité (A19 : 0.1 = +10 %), catégorie « altération ».</summary>
        public Fixed FragilityDamageTakenIncrease { get; init; } = Fixed.FromRatio(1, 10);

        /// <summary>
        /// Plafond absolu (24 h) de <see cref="MaxDurationMs"/> et de toutes les durées des règles et des compétences, pour que
        /// les compteurs de ticks restent petits. Enveloppe technique provisoire de pré-alpha, pas une valeur d'équilibrage.
        /// </summary>
        public const long AbsoluteMaxDurationMs = 86_400_000;

        public TickRate Tick => new TickRate(TickMilliseconds);

        /// <summary>Liste des incohérences ; vide si les règles sont utilisables.</summary>
        public IReadOnlyList<RuleViolation> Validate()
        {
            var errors = new List<RuleViolation>();
            bool tickValid = TickMilliseconds > 0 && 1000 % TickMilliseconds == 0;
            if (!tickValid)
                errors.Add(new RuleViolation("la durée du tick doit être un diviseur strictement positif de 1000 ms.", nameof(TickMilliseconds)));

            void Duration(long value, string name, bool strictlyPositive)
            {
                if (value < 0 || (strictlyPositive && value == 0))
                    errors.Add(new RuleViolation(strictlyPositive ? "doit être strictement positive." : "ne peut pas être négative.", name));
                else if (tickValid && value % TickMilliseconds != 0)
                    errors.Add(new RuleViolation($"{value} ms n'est pas un multiple de la durée du tick ({TickMilliseconds} ms).", name, nameof(TickMilliseconds)));
            }

            Duration(GlobalCooldownMs, nameof(GlobalCooldownMs), false);
            Duration(EnduranceRegenDelayMs, nameof(EnduranceRegenDelayMs), false);
            Duration(MaxDurationMs, nameof(MaxDurationMs), true);
            if (MaxDurationMs > AbsoluteMaxDurationMs)
                errors.Add(new RuleViolation($"ne peut pas dépasser {AbsoluteMaxDurationMs} ms (24 h).", nameof(MaxDurationMs)));
            Duration(GuardBreakDurationMs, nameof(GuardBreakDurationMs), true);
            Duration(ParryWindowMs, nameof(ParryWindowMs), true);
            Duration(ParryFailureRecoveryMs, nameof(ParryFailureRecoveryMs), false);
            Duration(FragilityDurationMs, nameof(FragilityDurationMs), true);
            // Correctif 6.1.1 (CA-M01 C et D) : la recharge globale et le délai de régénération sont bornés comme les autres
            // durées. Avec un tick de 1 ms, dernier tick possible + délai en ticks ≤ 2 × 86 400 000 : très loin de 2⁶³.
            foreach (var (value, name) in new[] { (GlobalCooldownMs, nameof(GlobalCooldownMs)), (EnduranceRegenDelayMs, nameof(EnduranceRegenDelayMs)),
                (GuardBreakDurationMs, nameof(GuardBreakDurationMs)), (ParryWindowMs, nameof(ParryWindowMs)),
                (ParryFailureRecoveryMs, nameof(ParryFailureRecoveryMs)), (FragilityDurationMs, nameof(FragilityDurationMs)) })
                if (value > AbsoluteMaxDurationMs)
                    errors.Add(new RuleViolation($"ne peut pas dépasser {AbsoluteMaxDurationMs} ms (24 h).", name));

            void Fraction(Fixed value, string name)
            {
                if (value < Fixed.Zero || value > Fixed.One)
                    errors.Add(new RuleViolation("doit être comprise entre 0 et 1.", name));
            }
            Fraction(GuardDamageReduction, nameof(GuardDamageReduction));
            Fraction(ParryDamageReduction, nameof(ParryDamageReduction));
            if (FragilityDamageTakenIncrease < Fixed.Zero)
                errors.Add(new RuleViolation("ne peut pas être négative.", nameof(FragilityDamageTakenIncrease)));

            foreach (var (value, name) in new[]
            {
                (GuardUpkeepPerSecond, nameof(GuardUpkeepPerSecond)), (GuardCostPerBlock, nameof(GuardCostPerBlock)),
                (RampartChargesPerParry, nameof(RampartChargesPerParry)), (RampartChargesMax, nameof(RampartChargesMax)),
                (ResonanceMax, nameof(ResonanceMax)), (ResonancePerDirectHit, nameof(ResonancePerDirectHit)),
                (ResonancePerPerfectParry, nameof(ResonancePerPerfectParry)), (ResonancePerBlock, nameof(ResonancePerBlock)),
                (UltimateResonanceCost, nameof(UltimateResonanceCost)),
            })
                if (value < 0) errors.Add(new RuleViolation("ne peut pas être négatif.", name));
            if (UltimateResonanceCost > ResonanceMax)
                errors.Add(new RuleViolation("le coût de l'ultime dépasse la Résonance maximale : l'ultime serait impossible.", nameof(UltimateResonanceCost), nameof(ResonanceMax)));

            if (EnduranceRegenPerSecond < 0)
                errors.Add(new RuleViolation("ne peut pas être négative.", nameof(EnduranceRegenPerSecond)));
            if (MaxEventsPerTick < 1)
                errors.Add(new RuleViolation("doit valoir au moins 1.", nameof(MaxEventsPerTick)));
            return errors;
        }
    }
}
