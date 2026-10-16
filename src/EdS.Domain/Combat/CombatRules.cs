using System;
using System.Collections.Generic;
using EdS.Domain.Time;

namespace EdS.Domain.Combat
{
    /// <summary>
    /// Paramètres du moteur de combat (lot 5, arbitrages A3, A6 et limites de simulation).
    /// Valeurs par défaut <b>provisoires</b> ; toutes les durées sont en millisecondes et doivent tomber
    /// exactement sur un tick.
    /// </summary>
    public sealed class CombatRules
    {
        public static readonly CombatRules Provisional = new CombatRules();

        /// <summary>Durée d'un tick logique (50 ms validés ; 20 ms à l'essai plus tard).</summary>
        public int TickMilliseconds { get; init; } = TickRate.DefaultMillisecondsPerTick;

        /// <summary>Temps global de recharge après chaque compétence lancée (A6). 0 = aucun.</summary>
        public long GlobalCooldownMs { get; init; } = 1000;

        /// <summary>Endurance regagnée par seconde (A3), en points entiers.</summary>
        public int EnduranceRegenPerSecond { get; init; } = 10;

        /// <summary>Temps sans dépense d'endurance avant que la régénération démarre (A3).</summary>
        public long EnduranceRegenDelayMs { get; init; } = 1000;

        /// <summary>Durée maximale d'un combat simulé : borne le nombre de ticks.</summary>
        public long MaxDurationMs { get; init; } = 600_000;

        /// <summary>Nombre maximal d'événements journalisés en un tick ; au-delà le combat est interrompu proprement.</summary>
        public int MaxEventsPerTick { get; init; } = 1000;

        /// <summary>Plafond absolu de <see cref="MaxDurationMs"/> (24 h), pour que les compteurs de ticks restent petits.</summary>
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
            if (EnduranceRegenPerSecond < 0)
                errors.Add(new RuleViolation("ne peut pas être négative.", nameof(EnduranceRegenPerSecond)));
            if (MaxEventsPerTick < 1)
                errors.Add(new RuleViolation("doit valoir au moins 1.", nameof(MaxEventsPerTick)));
            return errors;
        }
    }
}
