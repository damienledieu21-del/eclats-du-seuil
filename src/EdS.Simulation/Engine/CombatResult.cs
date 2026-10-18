using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Simulation.Engine
{
    public enum CombatEndReason
    {
        /// <summary>Durée du scénario atteinte.</summary>
        DurationReached = 0,

        /// <summary>Il ne reste qu'une équipe (ou aucune) en vie.</summary>
        TeamDefeated = 1,

        /// <summary>Limite de simulation atteinte (budget de travail, d'événements, capacité de la file, débordement numérique) : arrêt propre.</summary>
        Aborted = 2,
    }

    /// <summary>Statistiques d'un combattant en fin de combat.</summary>
    public sealed class CombatantResult
    {
        public string Id { get; internal set; } = string.Empty;
        public string Team { get; internal set; } = string.Empty;
        public long DamageDealt { get; internal set; }

        /// <summary>
        /// PV perdus. Sous-lot 8.2 : les dégâts absorbés par un bouclier n'en font pas partie (<see cref="DamageAbsorbed"/>) ; les
        /// dégâts infligés de la source (<see cref="DamageDealt"/>) les comprennent.
        /// </summary>
        public long DamageTaken { get; internal set; }

        /// <summary>Sous-lot 8.2 (X4) : dégâts subis absorbés par les boucliers de ce combattant.</summary>
        public long DamageAbsorbed { get; internal set; }

        /// <summary>Sous-lot 8.3 (X6) : part de ses dégâts transférée à ses protecteurs (protégé).</summary>
        public long DamageTransferred { get; internal set; }

        /// <summary>Sous-lot 8.3 (X6) : parts reçues des alliés qu'il protège, avant sa garde et ses boucliers (protecteur).</summary>
        public long DamageRedirected { get; internal set; }
        public long Hits { get; internal set; }
        public long CriticalHits { get; internal set; }
        public long SkillsCast { get; internal set; }
        public long CommandsRejected { get; internal set; }

        /// <summary>PV restants ; null pour un mannequin ou un assaillant aux PV infinis.</summary>
        public long? Health { get; internal set; }

        /// <summary>Endurance restante ; null hors personnage.</summary>
        public long? Endurance { get; internal set; }

        /// <summary>Résonance et Charges de rempart en fin de combat (avant leur remise à zéro) ; null hors personnage.</summary>
        public int? Resonance { get; internal set; }

        public int? RampartCharges { get; internal set; }

        /// <summary>Impacts bloqués par la garde, parades parfaites réussies et ratées, ruptures de garde subies.</summary>
        public long Blocks { get; internal set; }

        public long PerfectParries { get; internal set; }
        public long FailedParries { get; internal set; }
        public long GuardBreaks { get; internal set; }

        public bool IsDead { get; internal set; }
    }

    public sealed class CombatResult
    {
        public CombatEndReason EndReason { get; }

        /// <summary>
        /// Raison de l'interruption (<c>workBudgetExceeded</c>, <c>queueLimitExceeded</c>, <c>eventBudgetExceeded</c>,
        /// <c>overflow</c>) ; null sinon.
        /// </summary>
        public string? AbortReason { get; }

        /// <summary>Dernier tick simulé.</summary>
        public long EndTick { get; }

        /// <summary>Durée simulée : (dernier tick + 1) × durée du tick.</summary>
        public long DurationMs { get; }

        /// <summary>Équipe gagnante quand une seule équipe survit ; null sinon.</summary>
        public string? Winner { get; }

        /// <summary>Dans l'ordre du scénario.</summary>
        public IReadOnlyList<CombatantResult> Combatants { get; }

        /// <summary>
        /// Pics de charge mesurés (lot 7.1), hors journal : travail maximal consommé en un tick, lignes comptées maximales
        /// en un tick, taille maximale de la file d'attente (construction comprise). Un tick interrompu compte pour ce
        /// qu'il a consommé avant l'arrêt.
        /// </summary>
        public CombatLoadPeaks Peaks { get; }

        internal CombatResult(CombatEndReason reason, string? abortReason, long endTick, long durationMs, string? winner,
            IReadOnlyList<CombatantResult> combatants, CombatLoadPeaks peaks)
        {
            Peaks = peaks;
            EndReason = reason;
            AbortReason = abortReason;
            EndTick = endTick;
            DurationMs = durationMs;
            Winner = winner;
            Combatants = EdS.Domain.Collections.Frozen.List(combatants, nameof(combatants));
        }

        public CombatantResult this[string id] =>
            Combatants.FirstOrDefault(c => c.Id == id) ?? throw new KeyNotFoundException($"Combattant inconnu : {id}.");
    }

    /// <summary>Pics de charge d'un combat (lot 7.1) ; voir <see cref="CombatResult.Peaks"/>.</summary>
    public sealed class CombatLoadPeaks
    {
        /// <summary>Unités de travail consommées au tick le plus chargé.</summary>
        public long WorkPerTick { get; }

        /// <summary>Lignes de simulation comptées au tick le plus chargé.</summary>
        public long EventsPerTick { get; }

        /// <summary>Taille maximale atteinte par la file d'attente.</summary>
        public long PendingEvents { get; }

        internal CombatLoadPeaks(long workPerTick, long eventsPerTick, long pendingEvents)
        {
            WorkPerTick = workPerTick;
            EventsPerTick = eventsPerTick;
            PendingEvents = pendingEvents;
        }
    }
}
