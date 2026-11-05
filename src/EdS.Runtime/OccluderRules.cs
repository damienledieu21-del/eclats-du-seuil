using System;
using System.Collections.Generic;

namespace EdS.Runtime
{
    /// <summary>
    /// V1.10 (essai 10 de Damien sous Unity, 5 novembre 2026 : « les murs ne passent pas en fondu », « la caméra passe dans les poutres ») :
    /// règles, sans Unity, du décor qui cache le joueur. Un décor qui se trouve entre la caméra et le corps du joueur (pieds, milieu ou tête,
    /// et plus seulement la tête) devient transparent (<see cref="FadeAlpha"/>) au lieu de s'abaisser ou de disparaître d'un coup ; un décor
    /// « au-dessus » (poutres d'une pièce, toit sous lequel on se tient) disparaît en fondu dès que la caméra arrive à sa hauteur, pour
    /// qu'elle ne passe jamais dedans. Affichage seulement : rien de cela ne touche au moteur.
    /// </summary>
    public static class OccluderRules
    {
        /// <summary>Opacité d'un décor qui cache le joueur : assez pour deviner le mur, assez peu pour voir le personnage.</summary>
        public const double FadeAlpha = 0.25;

        /// <summary>Marge (m) sous un décor au-dessus : la caméra qui arrive à moins de cette hauteur sous lui le fait disparaître.</summary>
        public const double OverheadMargin = 0.35;

        /// <summary>Vitesse du fondu, en opacité par seconde (un quart de seconde de l'opaque au transparent).</summary>
        public const double FadeSpeed = 4.0;

        /// <summary>Sous cette opacité, le décor n'est plus dessiné du tout.</summary>
        public const double HiddenBelow = 0.02;

        /// <summary>
        /// Points du corps regardés, en mètres au-dessus du milieu du corps affiché (les pieds plus 1 m) : bas des jambes, milieu, tête.
        /// Avant V1.10, seule la tête comptait : un mur qui cachait les jambes et le buste restait opaque.
        /// </summary>
        public static IReadOnlyList<double> BodyPoints => Body;

        // Revue Codex de la PR #97 : un tableau parcouru par indice (un foreach sur IReadOnlyList allouerait son énumérateur à chaque appel).
        private static readonly double[] Body = { -0.7, 0.0, 1.0 };

        /// <summary>Hystérésis (m) du décor au-dessus : la caméra doit redescendre de plus que cela pour le faire revenir (pas d'oscillation).</summary>
        public const double OverheadHysteresis = 0.1;

        /// <summary>La caméra (à la hauteur <paramref name="cameraY"/>) est-elle à hauteur d'un décor au-dessus dont le bas est <paramref name="bottom"/> ?</summary>
        public static bool Overhead(double cameraY, double bottom) => cameraY > bottom - OverheadMargin;

        /// <summary>
        /// Même question, avec hystérésis (relecture de la PR #97) : déjà à sa hauteur (<paramref name="wasAtHeight"/>), la caméra le reste
        /// tant qu'elle ne redescend pas de plus de <see cref="OverheadHysteresis"/> sous le seuil d'entrée.
        /// </summary>
        public static bool Overhead(double cameraY, double bottom, bool wasAtHeight) =>
            wasAtHeight ? cameraY > bottom - OverheadMargin - OverheadHysteresis : Overhead(cameraY, bottom);

        /// <summary>Le segment [<paramref name="a"/>, <paramref name="b"/>] traverse-t-il la boîte alignée [<paramref name="min"/>, <paramref name="max"/>] ? (méthode des dalles)</summary>
        public static bool SegmentHitsBox((double X, double Y, double Z) a, (double X, double Y, double Z) b, (double X, double Y, double Z) min,
            (double X, double Y, double Z) max)
        {
            // Revue Codex de la PR #97 : sans aucune allocation (appelé jusqu'à trois fois par décor et par image) : un axe à la fois.
            double t0 = 0, t1 = 1;
            return Slab(a.X, b.X - a.X, min.X, max.X, ref t0, ref t1) && Slab(a.Y, b.Y - a.Y, min.Y, max.Y, ref t0, ref t1)
                && Slab(a.Z, b.Z - a.Z, min.Z, max.Z, ref t0, ref t1);
        }

        /// <summary>Une dalle de la méthode des dalles : resserre [t0, t1] sur l'axe ; false si le segment la manque.</summary>
        private static bool Slab(double start, double delta, double lo, double hi, ref double t0, ref double t1)
        {
            if (Math.Abs(delta) < 1e-9) return start >= lo && start <= hi;
            double u = (lo - start) / delta, v = (hi - start) / delta;
            if (u > v) (u, v) = (v, u);
            t0 = Math.Max(t0, u);
            t1 = Math.Min(t1, v);
            return t0 <= t1;
        }

        /// <summary>
        /// Le décor (boîte [<paramref name="min"/>, <paramref name="max"/>]) cache-t-il le joueur, vu de <paramref name="camera"/> : est-il
        /// entre la caméra et l'un des points du corps (<see cref="BodyPoints"/>, au-dessus du milieu du corps <paramref name="player"/>) ?
        /// </summary>
        public static bool MasksBody((double X, double Y, double Z) camera, (double X, double Y, double Z) player, (double X, double Y, double Z) min,
            (double X, double Y, double Z) max)
        {
            for (int i = 0; i < Body.Length; i++)
                if (SegmentHitsBox(camera, (player.X, player.Y + Body[i], player.Z), min, max)) return true;
            return false;
        }

        /// <summary>
        /// Opacité visée : 0 pour un décor au-dessus atteint par la caméra (ou un toit sous lequel on se tient), <see cref="FadeAlpha"/> pour
        /// un décor qui cache le joueur, 1 sinon.
        /// </summary>
        public static double Target(bool masks, bool hidden) => hidden ? 0 : masks ? FadeAlpha : 1;

        /// <summary>Opacité après <paramref name="deltaTime"/> secondes, en allant vers <paramref name="target"/> à <see cref="FadeSpeed"/>.</summary>
        public static double Step(double current, double target, double deltaTime)
        {
            if (double.IsNaN(current)) current = 1;
            double step = Math.Max(0, deltaTime) * FadeSpeed;
            double next = current < target ? Math.Min(target, current + step) : Math.Max(target, current - step);
            return Math.Max(0, Math.Min(1, next));
        }

        /// <summary>Le décor est-il encore dessiné à l'opacité <paramref name="alpha"/> ?</summary>
        public static bool Shown(double alpha) => alpha > HiddenBelow;

        /// <summary>Le décor est-il en train de passer en transparence (matériaux transparents à mettre) ?</summary>
        public static bool Clear(double alpha) => alpha < 0.999;
    }
}
