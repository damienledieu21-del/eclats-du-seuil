using System;
using System.Collections.Generic;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.Domain.Combat
{
    /// <summary>
    /// Sous-lot 11.4 : espace d'un combat positionné (scénario, bloc <c>space</c>) : une seule surface praticable (la démonstration n'a
    /// qu'un niveau), rectangle <c>[MinX, MaxX] × [MinZ, MaxZ]</c> dont les bords sont solides. Un combat sans espace reste logique
    /// (portées ignorées, aucun déplacement), comme avant le sous-lot 11.4.
    /// </summary>
    public sealed class CombatSpace
    {
        public Fixed MinX { get; }
        public Fixed MaxX { get; }
        public Fixed MinZ { get; }
        public Fixed MaxZ { get; }

        public CombatSpace(Fixed minX, Fixed maxX, Fixed minZ, Fixed maxZ)
        {
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
        }

        /// <summary>Incohérences des bornes (chemins sous <c>space.bounds</c>).</summary>
        public IReadOnlyList<RuleViolation> Validate()
        {
            var errors = new List<RuleViolation>();
            foreach (var (value, name) in new[] { (MinX, "minX"), (MaxX, "maxX"), (MinZ, "minZ"), (MaxZ, "maxZ") })
                if (!SpaceLimits.IsCoordinate(value))
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                        $"coordonnée hors de [-{SpaceLimits.MaxCoordinate}, {SpaceLimits.MaxCoordinate}] m.", "space.bounds." + name));
            if (errors.Count > 0) return errors;
            if (MinX >= MaxX) errors.Add(new RuleViolation("minX doit être strictement inférieur à maxX.", "space.bounds.minX", "space.bounds.maxX"));
            if (MinZ >= MaxZ) errors.Add(new RuleViolation("minZ doit être strictement inférieur à maxZ.", "space.bounds.minZ", "space.bounds.maxZ"));
            return errors;
        }

        /// <summary>
        /// Centre ramené dans les bornes rétrécies du rayon (le cercle reste dans l'espace) : un déplacement contre un bord glisse le long
        /// de celui-ci, composante par composante.
        /// </summary>
        public GroundVector Clamp(GroundVector center, Fixed radius) => new GroundVector(
            Fixed.Clamp(center.X, MinX + radius, MaxX - radius),
            Fixed.Clamp(center.Z, MinZ + radius, MaxZ - radius));

        /// <summary>Le cercle de centre <paramref name="center"/> et de rayon <paramref name="radius"/> est-il entièrement dans l'espace ?</summary>
        public bool Contains(GroundVector center, Fixed radius) =>
            center.X >= MinX + radius && center.X <= MaxX - radius && center.Z >= MinZ + radius && center.Z <= MaxZ - radius;
    }

    /// <summary>
    /// Sous-lot 11.4 : placement de départ d'un combattant dans un combat positionné : position du centre de son emprise, orientation
    /// (direction non nulle, ramenée à 1 m par le moteur) et rayon de l'emprise circulaire (règles du combat dans l'espace, section 1).
    /// </summary>
    public sealed class CombatantPlacement
    {
        public GroundVector Position { get; }
        public GroundVector Facing { get; }
        public Fixed Radius { get; }

        public CombatantPlacement(GroundVector position, GroundVector facing, Fixed radius)
        {
            Position = position;
            Facing = facing;
            Radius = radius;
        }
    }
}
