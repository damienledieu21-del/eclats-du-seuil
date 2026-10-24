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
    /// restent fermés. Le changement prend effet à la session qui commence.
    /// </summary>
    public static class ZoneSetups
    {
        public static CombatSetup For(ZoneDefinition zone, IEnumerable<string> consumed)
        {
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            var keys = new HashSet<string>(consumed ?? throw new ArgumentNullException(nameof(consumed)), StringComparer.Ordinal);
            var opened = new HashSet<string>(zone.Passages.Where(p => keys.Contains(zone.PassageKey(p.Id))).Select(p => p.Obstacle), StringComparer.Ordinal);
            CombatSetup s = zone.Setup;
            if (opened.Count == 0) return s;
            var space = new CombatSpace(s.Space!.Min, s.Space.Max, s.Space.Obstacles.Where(o => !opened.Contains(o.Id)));
            return new CombatSetup(s.Seed, s.DurationMs, s.Stats, s.Damage, s.Progression, s.Combat, s.Combatants, s.Commands, s.Data, space, s.Zone);
        }
    }
}
