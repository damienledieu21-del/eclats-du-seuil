using EdS.Domain.Combat;
using EdS.Domain.Space;

namespace EdS.Simulation.Engine
{
    /// <summary>
    /// Sous-lot V2.28d (2), monture (sujet 7 de la feuille de route : « en général on descend pour combattre ») : un personnage d'un combat
    /// positionné qui a une monture (<see cref="CharacterSetup.Mount"/>, le cheval à 13 m/s) monte en selle (<c>mount</c>) ou en descend
    /// (<c>dismount</c>). En selle, il se déplace à la vitesse de sa monture et n'a aucune action de combat (compétence, garde, parade,
    /// esquive, soin, attaque à deux avec un compagnon : refus <c>mounted</c> ; un ordre reste possible) ; il peut marcher, s'arrêter, sélectionner une cible et partir (passage d'un secteur à l'autre :
    /// il arrive en selle, <see cref="CarriedState.Mounted"/>). Être frappé ne le fait pas descendre (choix de Claude, question posée à Damien).
    /// Refus atomiques pour monter, dans cet ordre : <c>actorDead</c>/<c>actorAbsent</c>, <c>actorCannotMount</c> (pas de monture, ou combat
    /// sans espace), <c>alreadyMounted</c>, contrôle qui bloque le déplacement (raison du contrôle), <c>guardActive</c>, <c>dodgeActive</c>
    /// (bond en cours), <c>parryActive</c>, <c>parryRecovery</c>, <c>duoInProgress</c> (attaque à deux avec un compagnon en cours), <c>enemyNear</c> (un combattant vivant et présent d'une autre équipe à moins de
    /// <see cref="CombatRules.MountSafeDistance"/>). Pour descendre : <c>actorDead</c>/<c>actorAbsent</c>, <c>notMounted</c>.
    /// </summary>
    public sealed partial class CombatEngine
    {
        private string? CheckMount(long tick, Fighter actor)
        {
            if (actor.Out) return OutReason(actor, "actor");
            if (actor.Character?.Mount is null || !_spatial) return "actorCannotMount";
            if (actor.Mounted) return "alreadyMounted";
            if (BlockReason(actor, StatusBlocks.Movement) is { } blocked) return blocked;
            if (actor.Guarding) return "guardActive";
            if (actor.DodgeStartTick is long dodged && tick < checked(dodged + DodgeDurationTicks)) return "dodgeActive";
            // Relecture de la PR #180 : ni parade ouverte ou en récupération, ni attaque à deux en cours avec un compagnon.
            if (actor.ParryOpen) return "parryActive";
            if (tick < actor.ParryRecoveryUntilTick) return "parryRecovery";
            foreach (Fighter f in _fightersById)
                if (f.Companion is { } c && ReferenceEquals(c.Owner, actor) && tick < c.DuoReadyTick) return "duoInProgress";
            long safe = _rules.MountSafeDistance.Raw;
            Body body = actor.Body!;
            foreach (Fighter other in _fightersById)
            {
                if (other.Out || other.Team == actor.Team || other.Body is null) continue;
                if (Planar.LengthSquared(checked(other.Body.X - body.X), checked(other.Body.Z - body.Z)) < checked(safe * safe)) return "enemyNear";
            }
            return null;
        }
    }
}
