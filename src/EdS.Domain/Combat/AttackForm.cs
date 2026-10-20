using System;
using System.Collections.Generic;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.Domain.Combat
{
    /// <summary>
    /// Sous-lot 11.4c (COMBAT_ESPACE.md, section 5, L11-D7) : forme d'une attaque d'assaillant dans un combat positionné. La forme et ses
    /// défenses (garde, parade) sont deux informations distinctes.
    /// </summary>
    public enum AttackShape
    {
        /// <summary>Coup dirigé sur la cible de l'assaillant (portée entre les bords des emprises, ligne de vue) ; garde et parade selon les règles.</summary>
        Target = 0,

        /// <summary>Cône depuis l'assaillant, vers sa cible, de longueur <c>range</c> et d'ouverture <c>angle</c> (1° à 180°).</summary>
        Cone = 1,

        /// <summary>Cercle de rayon <c>radius</c> autour d'un point : la cible (au début de l'annonce) ou l'assaillant.</summary>
        Circle = 2,

        /// <summary>Bande de largeur <c>width</c> depuis l'assaillant vers sa cible, de longueur <c>range</c> (bouts arrondis).</summary>
        Line = 3,

        /// <summary>Cercle qui reste <c>persistentMs</c> et frappe toutes les <c>tickMs</c> ce qui est dedans ; il faut en sortir.</summary>
        Persistent = 4,
    }

    /// <summary>Sous-lot 11.4c : centre d'une zone circulaire (cercle, zone persistante).</summary>
    public enum ShapeCenter
    {
        /// <summary>La position de la cible au début de l'annonce.</summary>
        Target = 0,

        /// <summary>La position de l'assaillant au début de l'annonce.</summary>
        Self = 1,
    }

    /// <summary>Identifiants stables des formes et des centres (fichiers et journal).</summary>
    public static class AttackShapeIds
    {
        public static IReadOnlyDictionary<string, AttackShape> Shapes { get; } = new System.Collections.ObjectModel.ReadOnlyDictionary<string, AttackShape>(
            new Dictionary<string, AttackShape>(StringComparer.Ordinal)
            {
                ["target"] = AttackShape.Target,
                ["cone"] = AttackShape.Cone,
                ["circle"] = AttackShape.Circle,
                ["line"] = AttackShape.Line,
                ["persistent"] = AttackShape.Persistent,
            });

        public static IReadOnlyDictionary<string, ShapeCenter> Centers { get; } = new System.Collections.ObjectModel.ReadOnlyDictionary<string, ShapeCenter>(
            new Dictionary<string, ShapeCenter>(StringComparer.Ordinal)
            {
                ["target"] = ShapeCenter.Target,
                ["self"] = ShapeCenter.Self,
            });

        public static string Of(AttackShape shape) => shape switch
        {
            AttackShape.Target => "target",
            AttackShape.Cone => "cone",
            AttackShape.Circle => "circle",
            AttackShape.Line => "line",
            AttackShape.Persistent => "persistent",
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null),
        };

        public static string Of(ShapeCenter center) => center == ShapeCenter.Self ? "self" : "target";
    }

    /// <summary>
    /// Sous-lot 11.4c : zone d'une attaque, verrouillée au début de son annonce (positions en unités brutes). L'impact frappe, au tick
    /// d'impact, chaque combattant dont l'emprise <b>touche</b> la zone (frontière comprise) :
    /// <list type="bullet">
    /// <item>cercle et zone persistante : distance du centre au plus rayon de la zone + rayon de l'emprise ;</item>
    /// <item>bande : distance au segment de l'axe au plus demi-largeur + rayon de l'emprise (bouts arrondis) ;</item>
    /// <item>cône (ouverture ≤ 180°) : centre de l'emprise dans l'ouverture et à au plus longueur + rayon de l'origine, ou emprise qui touche
    /// l'un des deux côtés.</item>
    /// </list>
    /// L'origine (<see cref="OriginX"/>, <see cref="OriginZ"/>) est aussi l'origine du coup pour le bouclier : l'assaillant pour un cône ou une
    /// bande, le centre pour un cercle (COMBAT_ESPACE.md, section 6).
    /// </summary>
    public sealed class AttackZone
    {
        public AttackShape Shape { get; }
        public long OriginX { get; }
        public long OriginZ { get; }

        /// <summary>Axe unitaire (longueur <see cref="Planar.Unit"/>) d'un cône ou d'une bande ; (0, 0) pour un cercle.</summary>
        public long AxisX { get; }
        public long AxisZ { get; }

        /// <summary>Longueur (cône, bande) ou rayon (cercle, zone persistante), en unités brutes.</summary>
        public long Reach { get; }

        /// <summary>Ouverture d'un cône, en degrés ; 0 sinon.</summary>
        public int Angle { get; }

        /// <summary>Demi-largeur d'une bande, en unités brutes ; 0 sinon.</summary>
        public long HalfWidth { get; }

        public AttackZone(AttackShape shape, long originX, long originZ, long axisX, long axisZ, long reach, int angle = 0, long halfWidth = 0)
        {
            Shape = shape;
            OriginX = originX;
            OriginZ = originZ;
            AxisX = axisX;
            AxisZ = axisZ;
            Reach = reach;
            Angle = angle;
            HalfWidth = halfWidth;
        }

        /// <summary>Bout de l'axe (bande) ou d'un côté (cône, <paramref name="halfDegrees"/> = ± ouverture en demi-degrés).</summary>
        private void End(int halfDegrees, out long x, out long z)
        {
            Angles.Rotate(AxisX, AxisZ, halfDegrees, out long ux, out long uz);
            x = checked(OriginX + ux * Reach / Planar.Unit);
            z = checked(OriginZ + uz * Reach / Planar.Unit);
        }

        /// <summary>L'emprise (centre P, rayon r) touche-t-elle la zone ?</summary>
        public bool Touches(long px, long pz, long r)
        {
            switch (Shape)
            {
                case AttackShape.Circle:
                case AttackShape.Persistent:
                    return Planar.WithinDistance(OriginX, OriginZ, px, pz, checked(Reach + r));
                case AttackShape.Line:
                {
                    End(0, out long ex, out long ez);
                    return Planar.SegmentTouchesCircle(OriginX, OriginZ, ex, ez, px, pz, checked(HalfWidth + r));
                }
                case AttackShape.Cone:
                {
                    long vx = checked(px - OriginX), vz = checked(pz - OriginZ);
                    if (Angles.WithinArc(AxisX, AxisZ, vx, vz, Angle) && Planar.WithinDistance(OriginX, OriginZ, px, pz, checked(Reach + r))) return true;
                    End(Angle, out long lx, out long lz);
                    End(-Angle, out long rx, out long rz);
                    return Planar.SegmentTouchesCircle(OriginX, OriginZ, lx, lz, px, pz, r) || Planar.SegmentTouchesCircle(OriginX, OriginZ, rx, rz, px, pz, r);
                }
                default:
                    return false;
            }
        }

        /// <summary>
        /// Distance (unités brutes) qu'un combattant de rayon <paramref name="radius"/> doit parcourir, au pire, pour sortir d'une zone de
        /// cette forme et de ces dimensions : rayon de la zone + emprise (cercle), demi-largeur + emprise (bande), rayon du cercle inscrit
        /// + emprise (cône : longueur × sin(a/2) / (1 + sin(a/2))). Sert au contrôle des annonces évitables.
        /// </summary>
        public static long EscapeDistance(AttackShape shape, long reach, int angle, long halfWidth, long radius)
        {
            switch (shape)
            {
                case AttackShape.Circle:
                case AttackShape.Persistent:
                    return checked(reach + radius);
                case AttackShape.Line:
                    return checked(halfWidth + radius);
                case AttackShape.Cone:
                {
                    Angles.SinCos(angle, out long sin, out _);   // sinus de la demi-ouverture (angle demi-degrés)
                    return checked(reach * sin / (Angles.Scale + sin) + radius);
                }
                default:
                    return 0;
            }
        }
    }
}
