using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Validation;
using EdS.Domain;
using EdS.Domain.Combat;
using EdS.Domain.Space;

namespace EdS.Data.Zones
{
    public static partial class ZoneValidator
    {
        /// <summary>
        /// V2.51 (schéma 17) : reflet d'une zone de l'Envers tel que lu : zone de la surface reflétée (autre que la sienne), son scénario,
        /// obstacles retirés (identifiants d'obstacle, sans doublon, au plus <see cref="ZoneMirror.MaxRemoved"/>). Leur présence dans le
        /// scénario reflété est vérifiée à la composition (<see cref="Reflect"/>), la zone reflétée avec les portes (<see cref="CheckDoors"/>).
        /// </summary>
        private static ZoneMirror? CheckMirror(MirrorData m, string? ownId, ValidationReport report)
        {
            bool ok = true;
            if (m.Of is null) { Missing(report, "$.data.mirror.of", "zone reflétée « zone.… »"); ok = false; }
            else if (!EdS.Data.Saves.SaveValidator.IsZoneId(m.Of)) { report.Add("$.data.mirror.of", ValidationCode.InvalidValue, $"identifiant de zone « {m.Of} » invalide."); ok = false; }
            else if (m.Of == ownId) { report.Add("$.data.mirror.of", ValidationCode.InvalidValue, "une zone ne se reflète pas elle-même."); ok = false; }
            if (m.Scenario is null) { Missing(report, "$.data.mirror.scenario", "scénario de la zone reflétée"); ok = false; }
            else if (!IsScenarioPath(m.Scenario)) { report.Add("$.data.mirror.scenario", ValidationCode.InvalidValue, $"chemin de scénario « {m.Scenario} » invalide."); ok = false; }
            var removed = new List<string>();
            if (m.Remove != null)
            {
                if (m.Remove.Count > ZoneMirror.MaxRemoved)
                { report.Add("$.data.mirror.remove", ValidationCode.LimitExceeded, $"{m.Remove.Count} obstacles retirés, au plus {ZoneMirror.MaxRemoved}."); ok = false; }
                for (int i = 0; i < m.Remove.Count; i++)
                {
                    string path = $"$.data.mirror.remove[{i}]";
                    string? id = m.Remove[i];
                    if (id is null || !CombatSetup.IsValidName(id)) { report.Add(path, ValidationCode.InvalidValue, $"identifiant d'obstacle « {id} » invalide."); ok = false; }
                    else if (removed.Contains(id)) { report.Add(path, ValidationCode.DuplicateId, $"obstacle « {id} » déjà retiré plus haut."); ok = false; }
                    else removed.Add(id);
                }
            }
            return ok ? new ZoneMirror(m.Of!, m.Scenario!, removed) : null;
        }

        /// <summary>
        /// V2.51 (schéma 17) : l'aire d'une zone de l'Envers, composée. Les obstacles du scénario reflété <paramref name="mirrored"/> (dans
        /// son ordre), moins <paramref name="removed"/>, puis ceux du scénario propre <paramref name="own"/> ; tout le reste (graine, durée,
        /// règles, combattants, trêves) vient du scénario propre. Les deux scénarios ont les mêmes bornes ; un obstacle propre ne reprend pas
        /// l'identifiant d'un obstacle gardé ; la composition se valide comme une aire de zone (combattants hors des obstacles reflétés). Null,
        /// avec les erreurs dans <paramref name="report"/> (chemins <c>$.data.mirror…</c>), si elle est refusée.
        /// </summary>
        public static CombatSetup? Reflect(CombatSetup own, CombatSetup mirrored, IReadOnlyList<string> removed, ValidationReport report)
        {
            if (own is null) throw new ArgumentNullException(nameof(own));
            if (mirrored is null) throw new ArgumentNullException(nameof(mirrored));
            if (removed is null) throw new ArgumentNullException(nameof(removed));
            if (report is null) throw new ArgumentNullException(nameof(report));
            if (!(own.Space is CombatSpace space) || !(mirrored.Space is CombatSpace from) || !own.Zone || !mirrored.Zone)
            {
                report.Add("$.data.mirror.scenario", ValidationCode.InvalidValue, "le reflet relie deux simulations de zone positionnées (zone: true, bloc space).");
                return null;
            }
            if (space.Min != from.Min || space.Max != from.Max)
            {
                report.Add("$.data.mirror.scenario", ValidationCode.InvalidValue, "l'Envers a les mêmes bornes que la zone qu'il reflète (même grille, mêmes coordonnées).");
                return null;
            }
            var known = new HashSet<string>(from.Obstacles.Select(o => o.Id), StringComparer.Ordinal);
            bool ok = true;
            for (int i = 0; i < removed.Count; i++)
                if (!known.Contains(removed[i]))
                {
                    report.Add($"$.data.mirror.remove[{i}]", ValidationCode.UnknownReference, $"obstacle « {removed[i]} » absent du scénario reflété.");
                    ok = false;
                }
            var gone = new HashSet<string>(removed, StringComparer.Ordinal);
            List<SpaceObstacle> kept = from.Obstacles.Where(o => !gone.Contains(o.Id)).ToList();
            var keptIds = new HashSet<string>(kept.Select(o => o.Id), StringComparer.Ordinal);
            for (int i = 0; i < space.Obstacles.Count; i++)
                if (keptIds.Contains(space.Obstacles[i].Id))
                {
                    report.Add("$.data.scenario", ValidationCode.DuplicateId,
                        $"obstacle « {space.Obstacles[i].Id} » (space.obstacles[{i}]) déjà dans le reflet : un autre identifiant, ou le retirer du reflet.");
                    ok = false;
                }
            if (!ok) return null;
            var merged = new CombatSpace(space.Min, space.Max, kept.Concat(space.Obstacles));
            var setup = new CombatSetup(own.Seed, own.DurationMs, own.Stats, own.Damage, own.Progression, own.Combat, own.Combatants, own.Commands, own.Data,
                merged, own.Zone, own.Truces);
            IReadOnlyList<RuleViolation> violations = setup.ValidateArea();
            if (violations.Count > 0)
            {
                report.Add("$.data.mirror", ValidationCode.InvalidValue, $"aire de l'Envers refusée : {string.Join(", ", violations[0].Fields)} : {violations[0].Message}");
                return null;
            }
            return setup;
        }

        /// <summary>
        /// V2.51 : le reflet d'une zone de l'Envers parmi les zones reliées : la zone reflétée est là, à la surface, sans reflet elle-même,
        /// avec le même scénario et la même grille de région.
        /// </summary>
        private static void CheckMirrorOf(ZoneDefinition z, IReadOnlyDictionary<string, ZoneDefinition> byId, ValidationReport report)
        {
            if (!(z.Mirror is ZoneMirror m)) return;
            if (!byId.TryGetValue(m.Of, out ZoneDefinition? surface))
                report.Add("$.data.mirror.of", ValidationCode.UnknownReference, $"{z.Id} : zone reflétée « {m.Of} » absente des zones reliées.");
            else if (surface.Floor != ZoneFloor.Surface || surface.Mirror != null)
                report.Add("$.data.mirror.of", ValidationCode.InvalidValue, $"{z.Id} : la zone reflétée « {m.Of} » n'est pas une zone de la surface.");
            else if (surface.ScenarioName != m.Scenario)
                report.Add("$.data.mirror.scenario", ValidationCode.InvalidValue, $"{z.Id} : scénario « {m.Scenario} » ; celui de {m.Of} est « {surface.ScenarioName} ».");
            else if (!(surface.Region is RegionGrid r) || !(z.Region is RegionGrid e) || r.OriginX != e.OriginX || r.OriginZ != e.OriginZ
                || r.Columns != e.Columns || r.Rows != e.Rows)
                report.Add("$.data.region", ValidationCode.InvalidValue, $"{z.Id} : l'Envers a la même grille de région que {m.Of}.");
        }
    }
}
