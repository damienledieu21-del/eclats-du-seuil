using System;
using System.Collections.Generic;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot 10.2 : intentions d'un joueur (garde tenue, parade, compétence d'un emplacement) transformées en groupes de commandes du
    /// tick courant, chacun à soumettre tout ou rien (<see cref="CombatEngine.SubmitAll"/>). Partagé par le pont temps réel (sous-lot 9.2)
    /// et par le serveur (lot 10) : un joueur seul et un joueur en réseau produisent exactement les mêmes commandes.
    /// </summary>
    public static class IntentionPlanner
    {
        /// <summary>
        /// Groupes de commandes, dans l'ordre : quitter la garde si l'on lance et lancer (un seul groupe : un lancer refusé à l'admission
        /// ne fait pas quitter la garde, revue Codex de la PR #13), sinon prendre ou quitter la garde selon le bouton ; puis parer. Rien
        /// pour un combattant mort, ni pour un absent (attendu, refusé ou parti ; revue Codex de la PR #32, 28e tour : ses commandes seraient
        /// refusées et prendraient le quota du tick, et une parade au tick même de l'arrivée passerait malgré <c>CanParry</c> faux).
        /// </summary>
        /// <param name="me">Vue du joueur au début du tick.</param>
        /// <param name="atMs">Instant du tick courant.</param>
        /// <param name="targetFor">Cible d'une compétence (ennemie, alliée ou null pour une zone).</param>
        public static IReadOnlyList<CombatCommand[]> Plan(CombatantView me, long atMs, bool guardHeld, bool parry, int? slot,
            Func<SkillDefinition, string?> targetFor)
        {
            if (me is null) throw new ArgumentNullException(nameof(me));
            if (targetFor is null) throw new ArgumentNullException(nameof(targetFor));
            var groups = new List<CombatCommand[]>();
            if (me.IsDead || !me.IsPresent) return groups;
            if (slot is int s && SkillAt(me, s) is { } skill)
            {
                var cast = new CombatCommand(atMs, me.Id, skill.Id, targetFor(skill));
                groups.Add(me.Guarding ? new[] { new CombatCommand(atMs, me.Id, CombatAction.GuardStop), cast } : new[] { cast });
            }
            else if (guardHeld && !me.Guarding && me.CanGuard)
                groups.Add(new[] { new CombatCommand(atMs, me.Id, CombatAction.GuardStart) });
            else if (!guardHeld && me.Guarding)
                groups.Add(new[] { new CombatCommand(atMs, me.Id, CombatAction.GuardStop) });
            if (parry) groups.Add(new[] { new CombatCommand(atMs, me.Id, CombatAction.Parry) });
            return groups;
        }

        /// <summary>Compétence équipée dans un emplacement (0 à 5 : actives, <see cref="RealtimeCombat.UltimateSlot"/> : ultime) ; null sinon.</summary>
        public static SkillDefinition? SkillAt(CombatantView me, int slot)
        {
            if (me?.Character is null) return null;
            CharacterBuild build = me.Character.Build;
            string? id = slot == RealtimeCombat.UltimateSlot ? build.Ultimate : slot >= 0 && slot < build.ActiveSkills.Count ? build.ActiveSkills[slot] : null;
            return id != null && me.Character.Class.TryGetSkill(id, out SkillDefinition skill) && skill.Combat != null ? skill : null;
        }

        /// <summary>
        /// Raison visible pour laquelle la compétence d'un emplacement ne partirait pas à ce tick (hors de combat, contrôle, recharge,
        /// Résonance, endurance, Charges, aucune cible) ; null si elle semble lançable. Le même contrôle sert au pont temps réel (aucune
        /// commande pour une compétence visiblement indisponible) et au serveur (revue Codex de la PR #19), pour que solo et réseau
        /// produisent les mêmes commandes : une compétence indisponible ne fait jamais quitter la garde. Indicatif : le moteur reste juge.
        /// </summary>
        /// <param name="tick">Tick courant (prochain tick à simuler).</param>
        /// <param name="hasTarget">Une cible existe pour une compétence à cible ennemie unique.</param>
        public static string? Unavailable(CombatantView me, SkillDefinition skill, bool ultimate, long tick, double tickSeconds, CombatRules rules, bool hasTarget)
        {
            if (me is null) throw new ArgumentNullException(nameof(me));
            if (skill?.Combat is null) throw new ArgumentException("Compétence sans définition de combat.", nameof(skill));
            if (rules is null) throw new ArgumentNullException(nameof(rules));
            SkillCombatDefinition combat = skill.Combat;
            long readyTick = Math.Max(me.CooldownReadyTick(skill.Id), me.GlobalCooldownReadyTick);
            double remaining = Math.Max(0, readyTick - tick) * tickSeconds;
            return me.IsDead ? "hors de combat"
                : !me.CanCast ? "contrôlé"
                : tick < me.CooldownReadyTick(skill.Id) ? $"en recharge ({remaining:0.0} s)"
                : tick < me.GlobalCooldownReadyTick ? "recharge globale"
                : ultimate && me.Resonance < rules.UltimateResonanceCost ? $"Résonance {me.Resonance}/{rules.UltimateResonanceCost}"
                : me.Endurance < combat.EnduranceCost ? $"endurance {me.Endurance}/{combat.EnduranceCost}"
                : me.RampartCharges < combat.RampartChargeCost ? $"Charges {me.RampartCharges}/{combat.RampartChargeCost}"
                : combat.Targeting == SkillTargeting.Enemy && !hasTarget ? "aucune cible"
                : null;
        }
    }
}
