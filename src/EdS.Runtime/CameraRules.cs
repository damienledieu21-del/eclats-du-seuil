using System;
using System.Collections.Generic;

namespace EdS.Runtime
{
    /// <summary>
    /// Essai réaliste 9 (Damien, 29 octobre 2026 : « je n'aime pas les vues de caméra », « elle bouge mal » ; style choisi : World of
    /// Warcraft) : règles de la caméra, sans Unity. La caméra tourne autour du personnage (orientation, inclinaison, distance réglées
    /// séparément), ne tourne jamais toute seule ; la molette rapproche ou éloigne d'une part de la distance, du très près au loin ; la
    /// souris l'oriente et l'incline. Angles en degrés, distances en mètres.
    /// </summary>
    public static class CameraRules
    {
        /// <summary>Distance la plus proche (presque à l'épaule) et la plus lointaine.</summary>
        public const double MinDistance = 1.6, MaxDistance = 24.0;

        /// <summary>Distance la plus lointaine dans une pièce (intérieurs) : la caméra reste dans la maison.</summary>
        public const double IndoorMaxDistance = 8.0;

        /// <summary>Inclinaison : un peu par en dessous (on lève les yeux) jusqu'à presque à la verticale.</summary>
        public const double MinPitch = -10.0, MaxPitch = 80.0;

        /// <summary>Réglage de départ : derrière le personnage, un peu au-dessus.</summary>
        public const double DefaultPitch = 22.0, DefaultDistance = 8.0;

        /// <summary>Part de la distance gagnée ou perdue par cran de molette (15 %).</summary>
        public const double ZoomStep = 0.85;

        /// <summary>Hauteur minimale de la caméra au-dessus du sol où l'on marche.</summary>
        public const double GroundClearance = 0.35;

        /// <summary>Sensibilité de la souris : de la moitié au double.</summary>
        public const double MinSensitivity = 0.5, MaxSensitivity = 2.0;

        /// <summary>Degrés par seconde des touches qui tournent la vue (A / E), tenues.</summary>
        public const double TurnSpeed = 110.0;

        /// <summary>Distance après <paramref name="notches"/> crans de molette (positif : plus près), bornée.</summary>
        public static double Zoom(double distance, double notches, bool indoors)
        {
            if (double.IsNaN(distance) || double.IsNaN(notches)) return DefaultDistance;
            return Clamp(distance * Math.Pow(ZoomStep, notches), MinDistance, indoors ? IndoorMaxDistance : MaxDistance);
        }

        /// <summary>
        /// Rapprochement au doigt (pincement) : <paramref name="viewDelta"/> de 1 couvre toute la plage, de la plus lointaine à la plus
        /// proche (positif : plus près).
        /// </summary>
        public static double Pinch(double distance, double viewDelta, bool indoors) =>
            Zoom(distance, viewDelta * Math.Log(MinDistance / MaxDistance) / Math.Log(ZoomStep), indoors);

        /// <summary>
        /// Inclinaison après un mouvement vertical de la souris (<paramref name="mouseY"/> positif : souris vers le haut, on lève les yeux,
        /// la caméra descend), selon la sensibilité ; inversée sur demande.
        /// </summary>
        public static double Tilt(double pitch, double mouseY, double sensitivity, bool invert)
        {
            if (double.IsNaN(pitch)) return DefaultPitch;
            if (double.IsNaN(mouseY)) return Clamp(pitch, MinPitch, MaxPitch);
            return Clamp(pitch - mouseY * 2.0 * Sensitivity(sensitivity) * (invert ? -1 : 1), MinPitch, MaxPitch);
        }

        /// <summary>Inclinaison après <paramref name="degrees"/> degrés demandés au doigt (positif : la caméra monte), bornée.</summary>
        public static double Incline(double pitch, double degrees) =>
            double.IsNaN(pitch) ? DefaultPitch : double.IsNaN(degrees) ? Clamp(pitch, MinPitch, MaxPitch) : Clamp(pitch + degrees, MinPitch, MaxPitch);

        /// <summary>Sensibilité bornée (1 si elle n'est pas un nombre).</summary>
        public static double Sensitivity(double sensitivity) =>
            double.IsNaN(sensitivity) ? 1.0 : Clamp(sensitivity, MinSensitivity, MaxSensitivity);

        /// <summary>Hauteur du point visé au-dessus des pieds : la tête de près (1,6 m), la poitrine de loin (1,2 m).</summary>
        public static double TargetHeight(double distance) =>
            1.6 - 0.4 * Clamp((distance - MinDistance) / (MaxDistance - MinDistance), 0, 1);

        /// <summary>
        /// Distance réellement tenue : par en dessous, la caméra se rapproche pour rester au-dessus du sol (<see cref="GroundClearance"/>),
        /// comme dans World of Warcraft quand la caméra touche le sol.
        /// </summary>
        public static double GroundSafeDistance(double distance, double pitch, double targetHeight)
        {
            if (pitch >= 0) return distance;
            double drop = Math.Sin(-pitch * Math.PI / 180.0);
            double room = targetHeight - GroundClearance;
            if (drop <= 1e-9) return distance;   // relecture : à peine sous l'horizontale, aucune contrainte
            if (room <= 0) return Math.Min(distance, MinDistance);
            return Math.Max(Math.Min(distance, MinDistance), Math.Min(distance, room / drop));
        }

        /// <summary>Position de la caméra par rapport au point visé (x vers l'est, y vers le haut, z vers le nord).</summary>
        public static (double X, double Y, double Z) Offset(double yaw, double pitch, double distance)
        {
            double y = yaw * Math.PI / 180.0, p = pitch * Math.PI / 180.0;
            double flat = Math.Cos(p) * distance;
            return (-Math.Sin(y) * flat, Math.Sin(p) * distance, -Math.Cos(y) * flat);
        }

        /// <summary>
        /// Vue « derrière le personnage » (0 à 1) : 1 quand la caméra est basse (inclinaison de 15° et moins), 0 quand elle est haute (70° et
        /// plus). Au-delà de la moitié, reculer marche à reculons (le personnage garde son orientation), comme dans World of Warcraft.
        /// </summary>
        public static double Behind(double pitch) => Clamp((70.0 - pitch) / 55.0, 0, 1);

        /// <summary>
        /// Sous-lot V2.12.6 (essai du 9 novembre 2026 : « la caméra traverse les maisons du bourg ») : rayon de la caméra contre les murs (son
        /// plan proche est à 0,1 m, champ de 50°), distance la plus proche qu'un mur impose, et vitesse de retour (par seconde) une fois
        /// le mur passé.
        /// </summary>
        public const double CollisionRadius = 0.3, CollisionMinDistance = 0.5, CollisionReturnSpeed = 4.0;

        /// <summary>
        /// Sous-lot V2.12.6 : distance libre derrière le point visé <paramref name="target"/>, dans la direction <paramref name="direction"/>
        /// (celle de <see cref="Offset"/> pour une distance de 1), contre les boîtes des bâtiments <paramref name="boxes"/> élargies du rayon
        /// de la caméra : la caméra se tient devant le premier mur qu'elle rencontrerait, jamais plus près que
        /// <see cref="CollisionMinDistance"/>. <see cref="MaxDistance"/> si rien ne gêne. Une boîte qui contient déjà le point visé ne compte
        /// pas (le fondu du décor s'en charge). Un seul rayon par image, sans allocation ni moteur physique.
        /// </summary>
        public static double ClearDistance((double X, double Y, double Z) target, (double X, double Y, double Z) direction, IReadOnlyList<CameraBox>? boxes)
        {
            double best = MaxDistance;
            if (boxes == null || double.IsNaN(target.X + target.Y + target.Z + direction.X + direction.Y + direction.Z)) return best;
            double r = CollisionRadius;
            for (int i = 0; i < boxes.Count; i++)
            {
                CameraBox b = boxes[i];
                double x0 = b.MinX - r, y0 = b.MinY - r, z0 = b.MinZ - r, x1 = b.MaxX + r, y1 = b.MaxY + r, z1 = b.MaxZ + r;
                if (target.X >= x0 && target.X <= x1 && target.Y >= y0 && target.Y <= y1 && target.Z >= z0 && target.Z <= z1) continue;
                double t0 = 0, t1 = best;
                if (!Slab(target.X, direction.X, x0, x1, ref t0, ref t1) || !Slab(target.Y, direction.Y, y0, y1, ref t0, ref t1)
                    || !Slab(target.Z, direction.Z, z0, z1, ref t0, ref t1)) continue;
                if (t0 < best) best = t0;
            }
            return Math.Max(CollisionMinDistance, best);
        }

        private static bool Slab(double start, double delta, double lo, double hi, ref double t0, ref double t1)
        {
            if (Math.Abs(delta) < 1e-9) return start >= lo && start <= hi;
            double u = (lo - start) / delta, v = (hi - start) / delta;
            if (u > v) (u, v) = (v, u);
            if (u > t0) t0 = u;
            if (v < t1) t1 = v;
            return t0 <= t1;
        }

        /// <summary>
        /// Sous-lot V2.12.6 : distance libre affichée. Un mur qui se rapproche la ramène tout de suite (on ne voit jamais l'intérieur d'un
        /// mur) ; un mur qui s'éloigne la laisse revenir en douceur (<see cref="CollisionReturnSpeed"/>), sans à-coup.
        /// </summary>
        public static double SettleClear(double shown, double clear, double deltaTime)
        {
            if (double.IsNaN(clear)) return double.IsNaN(shown) ? MaxDistance : shown;
            if (double.IsNaN(shown) || clear <= shown) return clear;
            double dt = double.IsNaN(deltaTime) || deltaTime < 0 ? 0 : deltaTime;
            return shown + (clear - shown) * (1.0 - Math.Exp(-CollisionReturnSpeed * dt));
        }

        /// <summary>Angle ramené dans [0, 360[.</summary>
        public static double Wrap(double degrees)
        {
            if (double.IsNaN(degrees) || double.IsInfinity(degrees)) return 0;
            double d = degrees % 360.0;
            return d < 0 ? d + 360.0 : d;
        }

        private static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
    }

    /// <summary>Sous-lot V2.12.6 : boîte alignée d'un bâtiment (mètres, y vers le haut) contre laquelle la caméra s'arrête.</summary>
    public readonly struct CameraBox
    {
        public CameraBox(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
        {
            MinX = Math.Min(minX, maxX);
            MinY = Math.Min(minY, maxY);
            MinZ = Math.Min(minZ, maxZ);
            MaxX = Math.Max(minX, maxX);
            MaxY = Math.Max(minY, maxY);
            MaxZ = Math.Max(minZ, maxZ);
        }

        public double MinX { get; }
        public double MinY { get; }
        public double MinZ { get; }
        public double MaxX { get; }
        public double MaxY { get; }
        public double MaxZ { get; }
    }
}
