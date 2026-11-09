namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.12.7 (essai du 9 novembre 2026 : flaques en disques presque noirs et opaques, « des trous », sur PC modeste) : réglages
    /// purs de l'aspect des flaques de la météo (<c>SpaceView.Weather</c>) : surface transparente (alpha entre 0,35 et 0,5), teinte qui
    /// reprend la couleur du ciel et du sol, bord fondu par une texture radiale générée en code, très légèrement au-dessus du sol. Sur PC
    /// modeste (et téléphone) : texture plus petite, sans reflet.
    /// </summary>
    public sealed class PuddleLook
    {
        private PuddleLook(double alpha, int textureSize, double lift, double smoothness, double skyShare)
        {
            Alpha = alpha;
            TextureSize = textureSize;
            Lift = lift;
            Smoothness = smoothness;
            SkyShare = skyShare;
        }

        /// <summary>Opacité au centre de la flaque (0 à 1).</summary>
        public double Alpha { get; }

        /// <summary>Côté (pixels) de la texture radiale du bord fondu.</summary>
        public int TextureSize { get; }

        /// <summary>Hauteur (m) de la flaque au-dessus du sol, pour ne pas se fondre dans les pavés.</summary>
        public double Lift { get; }

        /// <summary>Brillance de la matière (0 : mate, version très légère du PC modeste).</summary>
        public double Smoothness { get; }

        /// <summary>Part de la couleur du ciel dans la teinte (le reste vient du sol mouillé).</summary>
        public double SkyShare { get; }

        /// <summary>Réglages de la flaque ; <paramref name="light"/> : PC modeste ou téléphone.</summary>
        public static PuddleLook For(bool light) =>
            light ? new PuddleLook(0.4, 32, 0.012, 0.0, 0.5) : new PuddleLook(0.45, 128, 0.01, 0.85, 0.6);

        /// <summary>Teinte de la flaque : mélange du ciel et du sol, jamais plus sombre que 0,3 par canal (pas de trou noir).</summary>
        public (double R, double G, double B) Tint(double skyR, double skyG, double skyB, double groundR, double groundG, double groundB)
        {
            double Mix(double sky, double ground) => System.Math.Max(0.3, SkyShare * sky + (1.0 - SkyShare) * ground * 0.8);
            return (Mix(skyR, groundR), Mix(skyG, groundG), Mix(skyB, groundB));
        }

        /// <summary>Opacité d'un pixel de la texture radiale à la distance <paramref name="d"/> du centre (0 : centre, 1 : bord) : plein au cœur, fondu vers le bord.</summary>
        public static double Edge(double d)
        {
            if (d <= 0.55) return 1.0;
            if (d >= 1.0) return 0.0;
            double t = (1.0 - d) / 0.45;
            return t * t * (3.0 - 2.0 * t);
        }
    }
}
