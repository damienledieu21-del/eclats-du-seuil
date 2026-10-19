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
        /// pour un combattant mort.
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
            if (me.IsDead) return groups;
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

        /// <summary>Compétence équipée dans un emplacement (0 à 3 : actives, <see cref="RealtimeCombat.UltimateSlot"/> : ultime) ; null sinon.</summary>
        public static SkillDefinition? SkillAt(CombatantView me, int slot)
        {
            if (me?.Character is null) return null;
            CharacterBuild build = me.Character.Build;
            string? id = slot == RealtimeCombat.UltimateSlot ? build.Ultimate : slot >= 0 && slot < build.ActiveSkills.Count ? build.ActiveSkills[slot] : null;
            return id != null && me.Character.Class.TryGetSkill(id, out SkillDefinition skill) && skill.Combat != null ? skill : null;
        }
    }
}
