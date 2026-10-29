using System;

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
            if (drop <= 1e-9 || room <= 0) return Math.Min(distance, MinDistance);
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

        /// <summary>Angle ramené dans [0, 360[.</summary>
        public static double Wrap(double degrees)
        {
            if (double.IsNaN(degrees) || double.IsInfinity(degrees)) return 0;
            double d = degrees % 360.0;
            return d < 0 ? d + 360.0 : d;
        }

        private static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
    }
}
