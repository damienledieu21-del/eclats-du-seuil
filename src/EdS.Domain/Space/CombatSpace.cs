using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Collections;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;

namespace EdS.Domain.Space
{
    /// <summary>Sous-lot 11.4 : obstacle solide du décor (mur, rocher, tronc), polygone convexe au sol.</summary>
    public sealed class SpaceObstacle
    {
        public string Id { get; }

        /// <summary>Sommets tels que donnés par les données, dans un sens ou dans l'autre (au moins 3, au plus 16).</summary>
        public IReadOnlyList<GroundVector> Points { get; }

        /// <summary>
        /// Sous-lot 11.9 (essai de Damien du 22 octobre 2026) : obstacle bas (rivière, muret) : il arrête la marche et les charges comme
        /// les autres, mais pas la vue (ligne de vue des compétences et des coups dirigés, affichage des ennemis). Faux par défaut.
        /// </summary>
        public bool Low { get; }

        public SpaceObstacle(string id, IEnumerable<GroundVector> points, bool low = false)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Points = Frozen.List(points, nameof(points));
            Low = low;
        }
    }

    /// <summary>
    /// Sous-lot 11.4 : espace d'un combat positionné (COMBAT_ESPACE.md, sections 1 à 3) : un rectangle praticable au sol et ses obstacles
    /// solides. Un seul niveau (proposition retenue pour la première démonstration) : les surfaces superposées viendront avec leurs passages.
    /// Immuable ; validé par <see cref="CombatSetup.Validate()"/>.
    /// </summary>
    public sealed class CombatSpace
    {
        /// <summary>Coin sud-ouest et coin nord-est de l'aire praticable : un combattant y garde toute son emprise.</summary>
        public GroundVector Min { get; }
        public GroundVector Max { get; }
        public IReadOnlyList<SpaceObstacle> Obstacles { get; }

        public CombatSpace(GroundVector min, GroundVector max, IEnumerable<SpaceObstacle>? obstacles = null)
        {
            Min = min;
            Max = max;
            Obstacles = Frozen.ListOrEmpty(obstacles, nameof(obstacles));
        }

        /// <summary>Incohérences de l'espace lui-même ; chemins de la forme du fichier de scénario (<c>space.obstacles[2].points</c>).</summary>
        public IReadOnlyList<RuleViolation> Validate()
        {
            var errors = new List<RuleViolation>();
            if (!SpaceLimits.InRange(Min) || !SpaceLimits.InRange(Max))
                errors.Add(new RuleViolation(RuleViolationKind.NumericLimit, $"coins hors de ±{SpaceLimits.MaxCoordinate} m (borne technique).", "space.bounds"));
            else if (Min.X >= Max.X || Min.Z >= Max.Z)
                errors.Add(new RuleViolation("le coin min doit être strictement au sud-ouest du coin max.", "space.bounds"));
            if (Obstacles.Count > SpaceLimits.MaxObstacles)
            {
                errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded, $"{Obstacles.Count} obstacles, au plus {SpaceLimits.MaxObstacles}.", "space.obstacles"));
                return errors;
            }
            var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < Obstacles.Count; i++)
            {
                SpaceObstacle o = Obstacles[i];
                string path = $"space.obstacles[{i}]";
                if (!CombatSetup.IsValidName(o.Id))
                    errors.Add(new RuleViolation($"identifiant « {o.Id} » invalide : minuscules, chiffres et « _ », commençant par une lettre.", path + ".id"));
                else if (ids.TryGetValue(o.Id, out int first))
                    errors.Add(new RuleViolation(RuleViolationKind.DuplicateId, $"identifiant « {o.Id} » déjà utilisé (space.obstacles[{first}]).", path + ".id"));
                else ids[o.Id] = i;
                if (o.Points.Count < SpaceLimits.MinVertices || o.Points.Count > SpaceLimits.MaxVertices)
                    errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded,
                        $"{o.Points.Count} sommets, de {SpaceLimits.MinVertices} à {SpaceLimits.MaxVertices}.", path + ".points"));
                else if (o.Points.Any(p => !SpaceLimits.InRange(p)))
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit, $"sommet hors de ±{SpaceLimits.MaxCoordinate} m (borne technique).", path + ".points"));
                else if (!ConvexPolygon.IsStrictlyConvex(o.Points, out _))
                    errors.Add(new RuleViolation("polygone non strictement convexe (sommets répétés ou alignés, côtés croisés, aire nulle) : découper l'obstacle en polygones convexes.",
                        path + ".points"));
            }
            return errors;
        }
    }

    /// <summary>
    /// Sous-lot 11.4 : place de départ d'un combattant dans un combat positionné : centre de son emprise, orientation, rayon de l'emprise
    /// circulaire et, pour un assaillant, sa vitesse propre (celle des règles sinon).
    /// </summary>
    public sealed class CombatantPlacement
    {
        public GroundVector Position { get; }

        /// <summary>Direction regardée, composantes dans [−1, 1] et non toutes deux nulles ; le moteur la normalise.</summary>
        public GroundVector Facing { get; }

        /// <summary>Rayon de l'emprise au sol (valeurs de départ retenues : 0,5 m le Bastion, 0,6 m un loup, 1,2 m un chef).</summary>
        public Fixed Radius { get; }

        /// <summary>Vitesse de marche propre d'un assaillant, en m/s ; null : celle des règles (<c>combat.movement.speed</c>).</summary>
        public Fixed? Speed { get; }

        /// <summary>Orientation par défaut : vers le nord (+z).</summary>
        public static GroundVector DefaultFacing { get; } = new GroundVector(Fixed.Zero, Fixed.One);

        public CombatantPlacement(GroundVector position, Fixed radius, GroundVector? facing = null, Fixed? speed = null)
        {
            Position = position;
            Radius = radius;
            Facing = facing ?? DefaultFacing;
            Speed = speed;
        }
    }
}
