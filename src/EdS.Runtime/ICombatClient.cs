using System.Collections.Generic;
using EdS.Simulation.Engine;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot 10.3 : ce que l'affichage (Unity) attend d'un combat en cours, qu'il soit joué seul (<see cref="RealtimeCombat"/>, le moteur
    /// tourne sur place) ou en réseau (client du lot 10, le moteur rejoue les commandes du serveur). Le même écran sert aux deux.
    /// </summary>
    public interface ICombatClient
    {
        /// <summary>Personnage du joueur.</summary>
        string PlayerId { get; }

        /// <summary>Cible des compétences à cible ennemie unique ; null s'il n'y a plus d'ennemi vivant.</summary>
        string? TargetId { get; }

        bool IsFinished { get; }

        /// <summary>Résultat du combat une fois terminé ; null avant (ou si le combat a été arrêté avant sa fin).</summary>
        CombatResult? Result { get; }

        /// <summary>Prochain tick à simuler (affiché).</summary>
        long Tick { get; }

        double TickSeconds { get; }

        long DurationMs { get; }

        /// <summary>Fraction (0 à 1) du tick en cours déjà écoulée, pour interpoler l'affichage.</summary>
        double TickProgress { get; }

        bool GuardHeld { get; }

        void SetGuard(bool held);

        void Parry();

        /// <summary>Compétence d'un emplacement (0 à 3 : actives, <see cref="RealtimeCombat.UltimateSlot"/> : ultime) au prochain tick.</summary>
        bool UseSlot(int slot);

        void NextTarget();

        void SelectTarget(string id);

        /// <summary>Avance du temps réel écoulé ; rend le nombre de ticks simulés.</summary>
        int Advance(double seconds);

        /// <summary>Retours apparus depuis le dernier appel.</summary>
        IReadOnlyList<CombatFeedback> DrainFeedback();

        PlayerState Player();

        IReadOnlyList<OtherState> Others();
    }
}
