using System;
using System.Collections.Generic;
using EdS.Domain.Numerics;
using EdS.Domain.Time;

namespace EdS.Domain.Combat
{
    /// <summary>
    /// Paramètres du moteur de combat : rythme et endurance (lot 5, A3 et A6), limites de simulation, garde,
    /// parade, Charges de rempart, Résonance et états défensifs (lot 6, A1, A2, A4, A5 et A19), catalogue des altérations (lot 7.2).
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

        /// <summary>
        /// Nombre maximal d'événements de simulation comptés en un tick (phases 1 à 6, hors initialisation et finalisation) ;
        /// au-delà le combat est interrompu proprement. Au plus <see cref="MaxWorkPerTick"/> : chaque écriture coûte une unité de travail.
        /// </summary>
        public int MaxEventsPerTick { get; init; } = 1000;

        // Lot 7.1 : admission des scénarios et budgets d'exécution (docs/LIMITES_LOT_7.md). Valeurs configurées provisoires,
        // bornées par des plafonds techniques absolus.

        /// <summary>Nombre maximal de combattants d'un scénario (refus à la validation).</summary>
        public int MaxCombatants { get; init; } = 64;

        /// <summary>Nombre maximal de commandes d'un scénario (refus à la validation).</summary>
        public int MaxCommands { get; init; } = 20_000;

        /// <summary>Nombre maximal de commandes d'un même acteur au même tick (refus à la validation).</summary>
        public int MaxCommandsPerActorPerTick { get; init; } = 4;

        /// <summary>Nombre maximal d'événements en attente dans la file ; au-delà, arrêt <c>queueLimitExceeded</c>.</summary>
        public int MaxPendingEvents { get; init; } = 100_000;

        /// <summary>Budget de travail par tick (unités, voir docs/MOTEUR.md) ; au-delà, arrêt <c>workBudgetExceeded</c>.</summary>
        public int MaxWorkPerTick { get; init; } = 10_000;

        /// <summary>
        /// Profondeur maximale d'une chaîne de réactions (lot 7.3, C6) : une réaction n'est créée que si la profondeur de l'effet
        /// observé + 1 ne la dépasse pas ; sinon <c>triggerSuppressed</c>. 0 supprime toute réaction.
        /// </summary>
        public int MaxTriggerDepth { get; init; } = 4;

        /// <summary>Plafond technique de <see cref="MaxTriggerDepth"/> (Q10) : la chaîne causale reste une liste de 8 maillons au plus.</summary>
        public const int AbsoluteMaxTriggerDepth = 8;

        /// <summary>Plafonds techniques absolus des limites (enveloppes de pré-alpha, pas des capacités mesurées).</summary>
        public const int AbsoluteMinCombatants = 2, AbsoluteMaxCombatants = 256;
        public const int AbsoluteMaxCommands = 1_000_000;
        public const int AbsoluteMaxCommandsPerActorPerTick = 64;
        public const int AbsoluteMaxPendingEvents = 10_000_000;
        public const int AbsoluteMaxWorkPerTick = 10_000_000;

        /// <summary>Réduction des dégâts directs bloquables, après armure, pendant la garde (A1 : 0.6 = −60 %).</summary>
        public Fixed GuardDamageReduction { get; init; } = Fixed.FromRatio(6, 10);

        /// <summary>Endurance consommée par seconde de garde (A1), prélevée au fil des ticks sans dérive d'arrondi.</summary>
        public int GuardUpkeepPerSecond { get; init; } = 2;

        /// <summary>Endurance payée par impact bloqué (A1) ; si elle manque, la garde est brisée avant la réduction.</summary>
        public int GuardCostPerBlock { get; init; } = 10;

        /// <summary>
        /// Altération appliquée par la rupture de garde (A1, A19 ; lot 7.2) : sa définition dans <see cref="Statuses"/> fixe la
        /// durée et les blocages (ni garde ni parade, pas de régénération).
        /// </summary>
        public string GuardBreakStatus { get; init; } = "guardBreak";

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

        /// <summary>
        /// Définitions des altérations (lot 7.2, bloc <c>statuses</c> de <c>rules.json</c>) : DOT, contrôles, Fragilité, rupture
        /// de garde, immunités. Validées par <see cref="StatusCatalog.Validate"/>, chemins rattachés à <c>statuses[i]</c>.
        /// </summary>
        public StatusCatalog Statuses { get; init; } = StatusCatalog.Provisional;

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
            Duration(ParryWindowMs, nameof(ParryWindowMs), true);
            Duration(ParryFailureRecoveryMs, nameof(ParryFailureRecoveryMs), false);
            // Correctif 6.1.1 (CA-M01 C et D) : la recharge globale et le délai de régénération sont bornés comme les autres
            // durées. Avec un tick de 1 ms, dernier tick possible + délai en ticks ≤ 2 × 86 400 000 : très loin de 2⁶³.
            foreach (var (value, name) in new[] { (GlobalCooldownMs, nameof(GlobalCooldownMs)), (EnduranceRegenDelayMs, nameof(EnduranceRegenDelayMs)),
                (ParryWindowMs, nameof(ParryWindowMs)), (ParryFailureRecoveryMs, nameof(ParryFailureRecoveryMs)) })
                if (value > AbsoluteMaxDurationMs)
                    errors.Add(new RuleViolation($"ne peut pas dépasser {AbsoluteMaxDurationMs} ms (24 h).", name));

            void Fraction(Fixed value, string name)
            {
                if (value < Fixed.Zero || value > Fixed.One)
                    errors.Add(new RuleViolation("doit être comprise entre 0 et 1.", name));
            }
            Fraction(GuardDamageReduction, nameof(GuardDamageReduction));
            Fraction(ParryDamageReduction, nameof(ParryDamageReduction));
            if (Statuses is null)
                errors.Add(new RuleViolation("catalogue des altérations obligatoire.", nameof(GuardBreakStatus)));
            else if (GuardBreakStatus is null || !Statuses.TryGet(GuardBreakStatus, out StatusDefinition? guardBreak))
                errors.Add(new RuleViolation(RuleViolationKind.UnknownReference, $"altération inconnue « {GuardBreakStatus} ».", nameof(GuardBreakStatus)));
            else if (guardBreak.IsDot || guardBreak.Category == StatusCategory.Immunity)
                errors.Add(new RuleViolation("la rupture de garde doit être un contrôle ou un modificateur, ni périodique ni immunité.", nameof(GuardBreakStatus)));

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
            void Range(int value, int min, int max, string name)
            {
                if (value < min || value > max)
                    errors.Add(new RuleViolation($"doit être compris entre {min} et {max} (plafond technique).", name));
            }
            Range(MaxCombatants, AbsoluteMinCombatants, AbsoluteMaxCombatants, nameof(MaxCombatants));
            Range(MaxCommands, 0, AbsoluteMaxCommands, nameof(MaxCommands));
            Range(MaxCommandsPerActorPerTick, 1, AbsoluteMaxCommandsPerActorPerTick, nameof(MaxCommandsPerActorPerTick));
            Range(MaxPendingEvents, 1, AbsoluteMaxPendingEvents, nameof(MaxPendingEvents));
            Range(MaxWorkPerTick, 1, AbsoluteMaxWorkPerTick, nameof(MaxWorkPerTick));
            Range(MaxTriggerDepth, 0, AbsoluteMaxTriggerDepth, nameof(MaxTriggerDepth));
            if (MaxEventsPerTick < 1)
                errors.Add(new RuleViolation("doit valoir au moins 1.", nameof(MaxEventsPerTick)));
            else if (MaxWorkPerTick >= 1 && MaxEventsPerTick > MaxWorkPerTick)
                errors.Add(new RuleViolation("ne peut pas dépasser le budget de travail par tick : chaque événement coûte une unité de travail.",
                    nameof(MaxEventsPerTick), nameof(MaxWorkPerTick)));
            return errors;
        }
    }
}
