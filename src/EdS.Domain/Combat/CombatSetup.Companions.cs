using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Stats;

namespace EdS.Domain.Combat
{
    /// <summary>
    /// Compagnons (décisions de Damien du 14 novembre 2026) : validation des compagnons d'un combat et des commandes qui les visent (ordre,
    /// attaque à deux). Une seule validation, celle du combat (<see cref="Validate()"/>), avec chemin précis.
    /// </summary>
    public sealed partial class CombatSetup
    {
        /// <summary>
        /// Chaque compagnon : combat positionné, comportement sans distance d'engagement, ni errance, ni fuite, PV, pas de cible de scénario ;
        /// Éveillé connu, personnage de la même équipe, au plus <see cref="CompanionSetup.MaxPerOwner"/> compagnons par Éveillé ; jauge,
        /// distances, fenêtre et gains dans leurs bornes ; attaque à deux bornée, d'identifiant distinct de ses attaques.
        /// </summary>
        private void ValidateCompanions(Dictionary<string, int> ids, List<RuleViolation> errors)
        {
            long tick = Combat.TickMilliseconds;
            var perOwner = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < Combatants.Count; i++)
            {
                if (Combatants[i] is not AssailantSetup { Companion: { } companion } a) continue;
                string path = $"combatants[{i}]";
                string at = path + ".companion";
                if (Space is null) errors.Add(new RuleViolation("un compagnon se bat dans un combat positionné (bloc space absent).", at));
                if (a.Behavior is not { } behavior)
                    errors.Add(new RuleViolation("un compagnon a un comportement (behavior) : il se débrouille seul.", path + ".behavior"));
                else
                {
                    if (behavior.AggroRange != null)
                        errors.Add(new RuleViolation("un compagnon est engagé d'emblée (pas de aggroRange).", path + ".behavior.aggroRange"));
                    if (behavior.Wander != null)
                        errors.Add(new RuleViolation("un compagnon n'erre pas (wander) : il suit son Éveillé.", path + ".behavior.wander"));
                    if (behavior.Flee) errors.Add(new RuleViolation("un compagnon ne fuit pas (flee).", path + ".behavior.flee"));
                }
                if (a.Health is null) errors.Add(new RuleViolation("un compagnon a des PV (health) : à 0, il est hors combat.", path + ".health"));
                if (a.Target != null) errors.Add(new RuleViolation("un compagnon choisit sa cible selon l'ordre reçu (pas de target).", path + ".target"));
                if (!ids.TryGetValue(companion.Owner, out int ownerIndex))
                    errors.Add(new RuleViolation(RuleViolationKind.UnknownReference, $"combattant inconnu « {companion.Owner} ».", at + ".owner"));
                else if (Combatants[ownerIndex] is not CharacterSetup owner)
                    errors.Add(new RuleViolation("l'Éveillé d'un compagnon est un personnage.", at + ".owner"));
                else
                {
                    if (!string.Equals(owner.Team, a.Team, StringComparison.Ordinal))
                        errors.Add(new RuleViolation("un compagnon est de l'équipe de son Éveillé.", path + ".team"));
                    perOwner.TryGetValue(owner.Id, out int count);
                    perOwner[owner.Id] = ++count;
                    if (count == CompanionSetup.MaxPerOwner + 1)
                        errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded,
                            $"{count}ᵉ compagnon de « {owner.Id} », au plus {CompanionSetup.MaxPerOwner}.", at + ".owner"));
                }
                if (companion.Bond < 0 || companion.Bond > CompanionSetup.BondMax)
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit, $"jauge de lien hors de [0, {CompanionSetup.BondMax}].", at + ".bond"));
                if (companion.FollowDistance <= Fixed.Zero || companion.FollowDistance > CompanionSetup.MaxFollowDistance)
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                        $"distance de suite hors de ]0, {CompanionSetup.MaxFollowDistance}] m.", at + ".followDistance"));
                if (companion.Leash < companion.FollowDistance || companion.Leash > AssailantBehavior.MaxDistance)
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                        $"laisse hors de [distance de suite, {AssailantBehavior.MaxDistance}] m.", at + ".leash"));
                if (companion.BondWindowMs < tick || companion.BondWindowMs > CompanionSetup.MaxBondWindowMs)
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                        $"fenêtre de coordination hors de [{tick}, {CompanionSetup.MaxBondWindowMs}] ms.", at + ".bondWindowMs"));
                else if (companion.BondWindowMs % tick != 0)
                    errors.Add(new RuleViolation($"{companion.BondWindowMs} ms n'est pas un multiple de la durée du tick ({tick} ms).", at + ".bondWindowMs"));
                if (companion.CoordinatedGain < 0 || companion.CoordinatedGain > CompanionSetup.MaxGain)
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit, $"gain hors de [0, {CompanionSetup.MaxGain}].", at + ".coordinatedGain"));
                if (companion.ProtectGain < 0 || companion.ProtectGain > CompanionSetup.MaxGain)
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit, $"gain hors de [0, {CompanionSetup.MaxGain}].", at + ".protectGain"));
                if (companion.Duo is { } duo) ValidateDuo(a, duo, at + ".duo", tick, errors);
            }
        }

        private static void ValidateDuo(AssailantSetup a, CompanionDuo duo, string at, long tick, List<RuleViolation> errors)
        {
            if (!IsValidName(duo.Id))
                errors.Add(new RuleViolation($"identifiant « {duo.Id} » invalide : minuscules, chiffres et « _ », commençant par une lettre.", at + ".id"));
            else if (a.Attacks.Any(x => x.Id == duo.Id))
                errors.Add(new RuleViolation(RuleViolationKind.DuplicateId, $"« {duo.Id} » est déjà une attaque du compagnon.", at + ".id"));
            if (duo.RawDamage < 0 || duo.RawDamage > CombatantStats.MaxRawDamage)
                errors.Add(new RuleViolation($"dégâts bruts hors de [0, {CombatantStats.MaxRawDamage}].", at + ".rawDamage"));
            if (!Enum.IsDefined(typeof(DamageType), duo.DamageType)) errors.Add(new RuleViolation("type de dégâts inconnu.", at + ".damageType"));
            bool Phase(long ms, string field, long min)
            {
                if (ms < min || ms > CompanionDuo.MaxPhaseMs)
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit, $"durée hors de [{min}, {CompanionDuo.MaxPhaseMs}] ms.", $"{at}.{field}"));
                else if (tick > 0 && ms % tick != 0)
                    errors.Add(new RuleViolation($"{ms} ms n'est pas un multiple de la durée du tick ({tick} ms).", $"{at}.{field}"));
                else return true;
                return false;
            }
            // La récupération court depuis le départ : elle couvre au moins l'annonce et un tick de plus (le coup part avant la fin). Revue Codex de
            // la PR #169 : la borne n'est calculée qu'une fois l'annonce dans ses bornes (aucun débordement sur une annonce démesurée).
            bool telegraphOk = Phase(duo.TelegraphMs, "telegraphMs", tick);
            Phase(duo.RecoveryMs, "recoveryMs", telegraphOk ? duo.TelegraphMs + tick : tick);
            if (!SpaceLimits.IsRange(duo.Range))
                errors.Add(new RuleViolation(RuleViolationKind.NumericLimit, $"portée hors de [{SpaceLimits.MinRange}, {SpaceLimits.MaxRange}] m (borne technique).", at + ".range"));
        }

        /// <summary>
        /// Compagnons : mêmes contrôles de forme qu'une commande du scénario, pour une commande soumise en cours de combat (pilote) : le
        /// scénario complété reste valide. Liste vide si la commande est bien formée.
        /// </summary>
        /// <param name="find">Combattant d'un identifiant, arrivées en cours de combat comprises (relecture indépendante de la PR #169 : un
        /// personnage et son compagnon arrivés par <c>Join</c> sont connus du pilote, pas de <see cref="Combatants"/>) ; null s'il est inconnu.</param>
        public static IReadOnlyList<RuleViolation> CheckCompanionCommand(CombatCommand command, Func<string, CombatantSetup?> find, bool spatial)
        {
            if (command is null) throw new ArgumentNullException(nameof(command));
            if (find is null) throw new ArgumentNullException(nameof(find));
            var errors = new List<RuleViolation>();
            ValidateCompanionCommand(command, "command", find, spatial, errors);
            return errors;
        }

        /// <summary>
        /// Ordre (<c>order</c>) et attaque à deux (<c>duo</c>) : combat positionné ; l'acteur est un personnage, le compagnon l'un des siens ;
        /// un ordre connu, une cible seulement (et obligatoirement) pour « cibler » ; une attaque à deux a une cible et exige un compagnon qui en
        /// a une. Seules ces commandes nomment un compagnon ou un ordre.
        /// </summary>
        private void ValidateCompanionCommand(CombatCommand command, string path, Dictionary<string, int> ids, List<RuleViolation> errors) =>
            ValidateCompanionCommand(command, path, id => ids.TryGetValue(id, out int i) ? Combatants[i] : null, Space != null, errors);

        private static void ValidateCompanionCommand(CombatCommand command, string path, Func<string, CombatantSetup?> find, bool spatial, List<RuleViolation> errors)
        {
            bool companionCommand = command.Action == CombatAction.Order || command.Action == CombatAction.Duo;
            if (!companionCommand)
            {
                if (command.Companion != null) errors.Add(new RuleViolation("seul un ordre (order) ou une attaque à deux (duo) nomme un compagnon.", path + ".companion"));
                if (command.Order != null) errors.Add(new RuleViolation("seul un ordre (order) nomme un ordre.", path + ".order"));
                return;
            }
            if (!spatial) errors.Add(new RuleViolation("ordre ou attaque à deux dans un combat sans espace (bloc space absent).", path + ".action"));
            if (find(command.Actor) is { } actor && actor is not CharacterSetup)
                errors.Add(new RuleViolation("seul un personnage commande un compagnon.", path + ".actor"));
            AssailantSetup? companion = null;
            if (command.Companion is null) errors.Add(new RuleViolation("compagnon obligatoire.", path + ".companion"));
            else if (find(command.Companion) is not { } found)
                errors.Add(new RuleViolation(RuleViolationKind.UnknownReference, $"combattant inconnu « {command.Companion} ».", path + ".companion"));
            else if (found is not AssailantSetup { Companion: { } link } a || !string.Equals(link.Owner, command.Actor, StringComparison.Ordinal))
                errors.Add(new RuleViolation($"« {command.Companion} » n'est pas un compagnon de « {command.Actor} ».", path + ".companion"));
            else companion = a;
            if (command.Action == CombatAction.Order)
            {
                if (command.Order is not { } order || !Enum.IsDefined(typeof(CompanionOrder), order))
                    errors.Add(new RuleViolation("ordre obligatoire : follow, wait, target, protect ou return.", path + ".order"));
                else if (order == CompanionOrder.Target && command.Target is null)
                    errors.Add(new RuleViolation("l'ordre « cibler » désigne une cible.", path + ".target"));
                else if (order != CompanionOrder.Target && command.Target != null)
                    errors.Add(new RuleViolation("seul l'ordre « cibler » a une cible.", path + ".target"));
            }
            else
            {
                if (command.Order != null) errors.Add(new RuleViolation("une attaque à deux ne porte pas d'ordre.", path + ".order"));
                if (command.Target is null) errors.Add(new RuleViolation("une attaque à deux a une cible commune.", path + ".target"));
                if (companion is { Companion: { Duo: null } })
                    errors.Add(new RuleViolation($"« {companion.Id} » n'a pas encore d'attaque à deux.", path + ".companion"));
            }
        }
    }
}
