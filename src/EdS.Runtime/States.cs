using System.Collections.Generic;
using EdS.Simulation.Engine;

namespace EdS.Runtime
{
    /// <summary>Sous-lot 9.2 : état du joueur au début du prochain tick, prêt à afficher (barres, emplacements, garde, parade).</summary>
    public sealed class PlayerState
    {
        /// <summary>Vue complète du moteur (PV, endurance, Résonance, Charges, garde, parade, altérations, compteurs).</summary>
        public CombatantView View { get; }
        public int ResonanceMax { get; }
        public int RampartChargesMax { get; }

        /// <summary>Emplacements équipés : actives (0 à 3) puis l'ultime (<see cref="RealtimeCombat.UltimateSlot"/>).</summary>
        public IReadOnlyList<SkillSlot> Slots { get; }
        public string? TargetId { get; }

        /// <summary>Temps restant de récupération après une parade ratée (0 si aucune).</summary>
        public double ParryRecoverySeconds { get; }

        public PlayerState(CombatantView view, int resonanceMax, int rampartChargesMax, IReadOnlyList<SkillSlot> slots, string? targetId,
            double parryRecoverySeconds)
        {
            View = view;
            ResonanceMax = resonanceMax;
            RampartChargesMax = rampartChargesMax;
            Slots = slots;
            TargetId = targetId;
            ParryRecoverySeconds = parryRecoverySeconds;
        }
    }

    /// <summary>Compétence d'un emplacement : nom, coût, recharge et raison visible d'indisponibilité (null si elle semble lançable).</summary>
    public sealed class SkillSlot
    {
        public int Slot { get; }
        public string SkillId { get; }
        public string Name { get; }
        public bool IsUltimate { get; }
        public int EnduranceCost { get; }

        /// <summary>Recharge effective (modificateurs compris), en secondes.</summary>
        public double CooldownSeconds { get; }

        /// <summary>Temps restant avant de pouvoir la lancer (recharge propre ou recharge globale).</summary>
        public double RemainingSeconds { get; }

        /// <summary>
        /// Raison d'indisponibilité vue par le client (recharge, ressources, contrôle, cible) ; indicative : le moteur reste seul juge.
        /// </summary>
        public string? Unavailable { get; }

        public SkillSlot(int slot, string skillId, string name, bool isUltimate, int enduranceCost, double cooldownSeconds, double remainingSeconds,
            string? unavailable)
        {
            Slot = slot;
            SkillId = skillId;
            Name = name;
            IsUltimate = isUltimate;
            EnduranceCost = enduranceCost;
            CooldownSeconds = cooldownSeconds;
            RemainingSeconds = remainingSeconds;
            Unavailable = unavailable;
        }
    }

    /// <summary>Un autre combattant (ennemi ou allié) : sa vue et, pour un assaillant, le temps avant sa prochaine attaque (télégraphie).</summary>
    public sealed class OtherState
    {
        public CombatantView View { get; }
        public string Id => View.Id;
        public bool IsEnemy { get; }

        /// <summary>Secondes avant la prochaine attaque d'un assaillant ; null pour un mannequin, un personnage ou un assaillant sans tour à venir.</summary>
        public double? NextAttackSeconds { get; }

        public OtherState(CombatantView view, bool isEnemy, double? nextAttackSeconds)
        {
            View = view;
            IsEnemy = isEnemy;
            NextAttackSeconds = nextAttackSeconds;
        }
    }

    public enum FeedbackKind
    {
        /// <summary>PV perdus (<see cref="CombatFeedback.Amount"/>).</summary>
        DamageTaken = 0,

        /// <summary>Dégâts absorbés par un bouclier.</summary>
        Absorbed = 1,
        Blocked = 2,
        PerfectParry = 3,
        FailedParry = 4,
        GuardBroken = 5,

        /// <summary>Coup critique porté par ce combattant.</summary>
        CriticalHit = 6,
        Ultimate = 7,

        /// <summary>Commande refusée par le moteur (ou abandonnée par le pont : plafond, file pleine).</summary>
        Rejected = 8,

        /// <summary>Compétence visiblement indisponible : aucune commande soumise.</summary>
        Unavailable = 9,
        Died = 10,
    }

    /// <summary>Retour à afficher : nature, combattant concerné, quantité (dégâts, nombre de blocages…) et précision éventuelle.</summary>
    public sealed class CombatFeedback
    {
        public FeedbackKind Kind { get; }
        public string CombatantId { get; }
        public long Amount { get; }
        public string? Detail { get; }

        public CombatFeedback(FeedbackKind kind, string combatantId, long amount, string? detail)
        {
            Kind = kind;
            CombatantId = combatantId;
            Amount = amount;
            Detail = detail;
        }

        public override string ToString() => $"{Kind} {CombatantId} {Amount}{(Detail is null ? "" : " " + Detail)}";
    }
}
