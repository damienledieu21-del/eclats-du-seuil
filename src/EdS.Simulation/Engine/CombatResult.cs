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

        /// <summary>Limite de simulation atteinte (budget d'événements, débordement numérique) : arrêt propre.</summary>
        Aborted = 2,
    }

    /// <summary>Statistiques d'un combattant en fin de combat.</summary>
    public sealed class CombatantResult
    {
        public string Id { get; internal set; } = string.Empty;
        public string Team { get; internal set; } = string.Empty;
        public long DamageDealt { get; internal set; }
        public long DamageTaken { get; internal set; }
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

        /// <summary>Raison de l'interruption (<c>overflow</c>, <c>eventBudgetExceeded</c>) ; null sinon.</summary>
        public string? AbortReason { get; }

        /// <summary>Dernier tick simulé.</summary>
        public long EndTick { get; }

        /// <summary>Durée simulée : (dernier tick + 1) × durée du tick.</summary>
        public long DurationMs { get; }

        /// <summary>Équipe gagnante quand une seule équipe survit ; null sinon.</summary>
        public string? Winner { get; }

        /// <summary>Dans l'ordre du scénario.</summary>
        public IReadOnlyList<CombatantResult> Combatants { get; }

        internal CombatResult(CombatEndReason reason, string? abortReason, long endTick, long durationMs, string? winner, IReadOnlyList<CombatantResult> combatants)
        {
            EndReason = reason;
            AbortReason = abortReason;
            EndTick = endTick;
            DurationMs = durationMs;
            Winner = winner;
            Combatants = combatants;
        }

        public CombatantResult this[string id] =>
            Combatants.FirstOrDefault(c => c.Id == id) ?? throw new KeyNotFoundException($"Combattant inconnu : {id}.");
    }
}
