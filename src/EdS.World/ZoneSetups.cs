using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Space;

namespace EdS.World
{
    /// <summary>
    /// Sous-lot 12.4 : simulation d'une session de zone d'après l'état du monde. Un passage ouvert pour de bon (clé
    /// <see cref="ZoneDefinition.PassageKey"/> au registre du monde, raccourci du moulin) retire son obstacle de l'espace ; les autres
    /// restent fermés. Le changement prend effet à la session qui commence. Réponses de Damien du 31 octobre 2026 : une créature rare de la
    /// nuit (<see cref="Population.Night"/>, le loup blanc) n'est pas là au début d'une session ; elle arrive plus tard, après son signe.
    /// </summary>
    public static class ZoneSetups
    {
        public static CombatSetup For(ZoneDefinition zone, IEnumerable<string> consumed)
        {
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            var keys = new HashSet<string>(consumed ?? throw new ArgumentNullException(nameof(consumed)), StringComparer.Ordinal);
            var opened = new HashSet<string>(zone.Passages.Where(p => keys.Contains(zone.PassageKey(p.Id))).Select(p => p.Obstacle), StringComparer.Ordinal);
            var night = new HashSet<string>(zone.Populations.Where(p => p.Night != null).Select(p => p.Creature), StringComparer.Ordinal);
            CombatSetup s = zone.Setup;
            if (opened.Count == 0 && night.Count == 0) return s;
            CombatSpace? space = opened.Count == 0 ? s.Space : new CombatSpace(s.Space!.Min, s.Space.Max, s.Space.Obstacles.Where(o => !opened.Contains(o.Id)));
            IEnumerable<CombatantSetup> combatants = night.Count == 0 ? s.Combatants : s.Combatants.Where(c => !night.Contains(c.Id)).ToList();
            return new CombatSetup(s.Seed, s.DurationMs, s.Stats, s.Damage, s.Progression, s.Combat, combatants, s.Commands, s.Data, space, s.Zone);
        }
    }
}
