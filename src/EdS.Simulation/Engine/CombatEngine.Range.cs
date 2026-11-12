using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.Simulation.Engine
{
    /// <summary>
    /// Sous-lot 11.4b : portées et ligne de vue (COMBAT_ESPACE.md, section 4). Seulement dans un combat positionné : sans espace, une
    /// portée est ignorée et tout le combat se déroule comme avant. Tout est entier (unité brute 0,0001 m) et les frontières sont
    /// incluses : à exactement la portée, on est à portée.
    /// <list type="bullet">
    /// <item><b>Portée</b> d'une compétence à cible, d'un effet de réaction dirigé, d'une protection : distance entre les <b>bords</b> des
    /// emprises (distance des centres moins les deux rayons) au plus égale à la portée.</item>
    /// <item><b>Rayon</b> d'une compétence de groupe, d'une application à cible propre de groupe, d'une réaction sur tous les ennemis :
    /// cercle centré sur le lanceur (ou le porteur) ; un combattant en fait partie si son emprise le touche (distance des centres au plus
    /// égale au rayon plus son propre rayon).</item>
    /// <item><b>Ligne de vue</b> d'une compétence à cible : le segment entre les deux centres ne touche aucun obstacle du décor (un segment
    /// qui effleure un sommet ou un bord est coupé).</item>
    /// </list>
    /// Les positions ne changent que dans la phase <c>movement</c> : les contrôles d'une commande, d'un impact et de son coût exact voient
    /// donc les mêmes positions.
    /// </summary>
    public sealed partial class CombatEngine
    {
        /// <summary>Vrai sans espace ni portée ; sinon, les bords des emprises de <paramref name="a"/> et <paramref name="b"/> sont à la portée au plus.</summary>
        private bool InReach(Fighter a, Fighter b, Fixed? range)
        {
            if (!_spatial || range is not { } r || ReferenceEquals(a, b)) return true;
            Body ba = a.Body!, bb = b.Body!;
            return WithinCenterDistance(ba, bb, checked(r.Raw + ba.Radius + bb.Radius));
        }

        /// <summary>Vrai sans espace ni rayon ; sinon, l'emprise de <paramref name="f"/> touche le cercle de ce rayon centré sur <paramref name="center"/>.</summary>
        private bool InArea(Fighter center, Fighter f, Fixed? radius)
        {
            if (!_spatial || radius is not { } r || ReferenceEquals(center, f)) return true;
            Body bc = center.Body!, bf = f.Body!;
            return WithinCenterDistance(bc, bf, checked(r.Raw + bf.Radius));
        }

        private static bool WithinCenterDistance(Body a, Body b, long reach) =>
            Planar.LengthSquared(checked(b.X - a.X), checked(b.Z - a.Z)) <= checked(reach * reach);

        /// <summary>
        /// Vrai sans espace ; sinon, aucun obstacle ne touche le segment entre les centres de <paramref name="a"/> et <paramref name="b"/>.
        /// Sous-lot 11.9 : un obstacle bas (rivière) n'arrête pas la vue.
        /// </summary>
        private bool InSight(Fighter a, Fighter b)
        {
            if (!_spatial || ReferenceEquals(a, b)) return true;
            Body ba = a.Body!, bb = b.Body!;
            return !_sightIndex.AnyIntersectsSegment(ba.X, ba.Z, bb.X, bb.Z);   // sous-lot V2.26 : décor rangé par cases
        }

        /// <summary>Contrôle de portée puis de ligne de vue d'une compétence à cible, au lancement ; null si la cible est atteignable.</summary>
        private string? CheckReach(Fighter actor, Fighter target, SkillCombatDefinition combat)
        {
            if (!InReach(actor, target, combat.Range)) return "targetOutOfRange";
            if (!InSight(actor, target)) return "noLineOfSight";
            return null;
        }

        /// <summary>Garde les combattants dont l'emprise touche le cercle centré sur <paramref name="center"/> ; la liste reçue telle quelle sans rayon.</summary>
        private IReadOnlyList<Fighter> WithinArea(Fighter center, IReadOnlyList<Fighter> fighters, Fixed? radius) =>
            !_spatial || radius is null ? fighters : fighters.Where(f => InArea(center, f, radius)).ToArray();
    }
}
