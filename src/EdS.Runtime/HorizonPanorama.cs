using System;

namespace EdS.Runtime
{
    /// <summary>Ce que la bande panoramique de l'horizon montre à cet instant : une ou deux textures et leur opacité (fondu de saison).</summary>
    public readonly struct PanoramaChoice : IEquatable<PanoramaChoice>
    {
        /// <summary>Nom de la texture du dessous (saison précédente pendant la transition), ou null quand une seule est montrée.</summary>
        public string? Under { get; }

        /// <summary>Nom de la texture du dessus (saison montrée, ou l'Envers).</summary>
        public string Top { get; }

        /// <summary>Opacité de la texture du dessous (0 : pas dessinée) et de celle du dessus, de 0 à 1.</summary>
        public float UnderAlpha { get; }
        public float TopAlpha { get; }

        public PanoramaChoice(string? under, string top, float underAlpha, float topAlpha)
        {
            Under = under;
            Top = top;
            UnderAlpha = underAlpha;
            TopAlpha = topAlpha;
        }

        public bool Equals(PanoramaChoice o) => Under == o.Under && Top == o.Top && UnderAlpha == o.UnderAlpha && TopAlpha == o.TopAlpha;
        public override bool Equals(object? obj) => obj is PanoramaChoice c && Equals(c);
        public override int GetHashCode() => (Top.GetHashCode() * 397) ^ (Under?.GetHashCode() ?? 0) ^ UnderAlpha.GetHashCode() ^ (TopAlpha.GetHashCode() * 7);
    }

    /// <summary>
    /// Sous-lot V2.29.2 : panoramas peints de l'horizon lointain (cinq images de ChatGPT, commandées par Damien : printemps, été, automne, hiver
    /// et l'Envers), qui remplacent les silhouettes de <see cref="HorizonProfile"/> dans la vue. Règles partagées, sans Unity et sans horloge :
    /// calage en azimut (nord à gauche de l'image, l'est au quart) et en angle (l'horizon à 200 px du bas d'une image de 1 024 px, 45,511 px/°),
    /// choix de la texture selon la saison du jeu et fondu entre deux textures pendant les 7 jours de transition, taille selon la machine.
    /// Affichage seulement.
    /// </summary>
    public static class HorizonPanorama
    {
        /// <summary>Image de référence (16 384 × 1 024, non versée : trop lourde) : 360° de large, 45,511 px/°.</summary>
        public const int ReferenceWidth = 16384, ReferenceHeight = 1024;

        /// <summary>Distance du profil de référence (m), la même que <see cref="HorizonProfile.Radius"/>.</summary>
        public const double ReferenceRadius = HorizonProfile.Radius;

        /// <summary>Pixels de l'image de référence sous la ligne d'horizon (200 : l'horizon est à y = 824 depuis le haut).</summary>
        public const int HorizonFromBottom = 200;

        /// <summary>Pixels par degré, vertical comme horizontal (16 384 / 360).</summary>
        public const double PixelsPerDegree = ReferenceWidth / 360.0;

        /// <summary>Angle du bas de l'image sous l'horizon (négatif) et du haut au-dessus (degrés) : 22,5° d'image en tout.</summary>
        public static readonly double BottomAngle = -HorizonFromBottom / PixelsPerDegree, TopAngle = (ReferenceHeight - HorizonFromBottom) / PixelsPerDegree;

        /// <summary>Hauteur de la ligne d'horizon dans l'image, de 0 (bas) à 1 (haut) : v = 0,1953125 quelle que soit la taille.</summary>
        public const double HorizonV = HorizonFromBottom / (double)ReferenceHeight;

        /// <summary>Nombre de saisons et rang de l'Envers dans les noms.</summary>
        public const int SeasonCount = 4;

        /// <summary>Noms des cinq panoramas (printemps 0 … hiver 3 comme <c>SeasonLook.Season</c>, puis l'Envers).</summary>
        public static readonly string[] Names = { "spring", "summer", "autumn", "winter", "reverse" };

        /// <summary>Nom de l'Envers.</summary>
        public const string Reverse = "reverse";

        /// <summary>Colonne (de 0 à 1) de l'image pour un azimut (0 : le nord, 90 : l'est, tournant vers l'est) ; un tour exact, raccord à 1.</summary>
        public static double U(double azimuthDegrees)
        {
            double u = azimuthDegrees / 360.0;
            return u - Math.Floor(u);
        }

        /// <summary>Azimut (0 à 360) d'une colonne de l'image (colonne 0 : le nord, 4 096 : l'est sur 16 384).</summary>
        public static double Azimuth(double u) => (u - Math.Floor(u)) * 360.0;

        /// <summary>Ligne de l'image (de 0 en bas à 1 en haut) pour un angle au-dessus de l'horizon (degrés) ; 0 de l'angle : <see cref="HorizonV"/>.</summary>
        public static double V(double angleDegrees) => (angleDegrees * PixelsPerDegree + HorizonFromBottom) / ReferenceHeight;

        /// <summary>Angle au-dessus de l'horizon d'une ligne de l'image (inverse de <see cref="V"/>).</summary>
        public static double AngleOf(double v) => (v * ReferenceHeight - HorizonFromBottom) / PixelsPerDegree;

        /// <summary>
        /// Hauteur dans le monde (m) d'un point du cylindre de rayon <paramref name="radius"/> vu à <paramref name="angleDegrees"/> au-dessus de
        /// l'horizon, la caméra étant à <paramref name="cameraY"/>. La hauteur de la caméra est prise comme pour <see cref="HorizonProfile"/>
        /// (l'horizon plat du profil de référence est à 6 000 m, au niveau 0) : relativement à la caméra, la hauteur est
        /// <c>radius × (tan angle − cameraY / 6 000)</c>, donc l'angle vu ne dépend ni du plan lointain (<paramref name="radius"/>) ni de
        /// l'altitude, sauf le léger abaissement de l'horizon en altitude.
        /// </summary>
        public static double WorldY(double angleDegrees, double cameraY, double radius) =>
            cameraY + radius * (Math.Tan(angleDegrees * Math.PI / 180.0) - cameraY / ReferenceRadius);

        /// <summary>Angle vu de la caméra (degrés, au-dessus de l'horizontale de l'œil) d'un point à la hauteur <paramref name="worldY"/> sur le cylindre.</summary>
        public static double SeenAngle(double worldY, double cameraY, double radius) => Math.Atan2(worldY - cameraY, radius) * 180.0 / Math.PI;

        /// <summary>
        /// Rangées de sommets de la bande (revue Codex de la PR #203) : les lignes de l'image sont linéaires en degrés mais la hauteur d'un
        /// cylindre l'est en tangente ; deux rangées seulement décaleraient l'horizon de 0,1° et les crêtes de 0,3°. Une rangée par demi-degré
        /// (donc exactement à 0° et à 8,5°), plus le bas et le haut de l'image. <c>V</c> : ligne de l'image (0 en bas, 1 en haut) ;
        /// <c>Height</c> : hauteur au rayon de référence (m), <c>6 000 × tan(angle)</c>, que l'échelle y de la vue ramène au plan lointain.
        /// </summary>
        public static (double V, double Height)[] Rows()
        {
            var rows = new System.Collections.Generic.List<(double, double)>();
            rows.Add((0.0, ReferenceRadius * Math.Tan(BottomAngle * Math.PI / 180.0)));
            for (int k = (int)Math.Floor(BottomAngle * 2) + 1; k * 0.5 < TopAngle; k++)
            {
                double angle = k * 0.5;
                if (angle <= BottomAngle) continue;
                rows.Add((V(angle), ReferenceRadius * Math.Tan(angle * Math.PI / 180.0)));
            }
            rows.Add((1.0, ReferenceRadius * Math.Tan(TopAngle * Math.PI / 180.0)));
            return rows.ToArray();
        }

        /// <summary>Taille de l'image utilisée : 4 096 × 256 sur téléphone, 8 192 × 512 sur PC (jamais 16 384).</summary>
        public static int Width(DeviceTier tier) => tier == DeviceTier.Phone ? 4096 : 8192;

        /// <summary>Hauteur de l'image utilisée (voir <see cref="Width"/>).</summary>
        public static int Height(DeviceTier tier) => Width(tier) / 16;

        /// <summary>Chemin Resources d'un panorama (sans extension) pour une machine : <c>EdsHorizon/horizon_spring_8192</c>.</summary>
        public static string ResourcePath(string name, DeviceTier tier) => "EdsHorizon/horizon_" + name + "_" + Width(tier);

        /// <summary>
        /// Choix de la texture pour la saison montrée : <paramref name="season"/> (0 printemps … 3 hiver) et <paramref name="blend"/> la
        /// progression de la transition (0 : tout la saison d'avant, 1 : tout la saison montrée, <c>SeasonLook.Blend</c>). Pendant la
        /// transition de 7 jours, la saison d'avant reste dessous et la nouvelle apparaît dessus ; la saison d'avant s'efface seulement
        /// dans la seconde moitié, pour que l'ensemble ne s'éclaircisse jamais. <paramref name="reversed"/> (l'Envers, pas encore affiché
        /// dans le jeu) montre l'Envers seul, sans fondu.
        /// </summary>
        public static PanoramaChoice Choose(int season, float blend, bool reversed)
        {
            if (reversed) return new PanoramaChoice(null, Reverse, 0f, 1f);
            if (season < 0 || season >= SeasonCount) throw new ArgumentOutOfRangeException(nameof(season));
            float t = blend < 0f ? 0f : blend > 1f ? 1f : blend;
            string top = Names[season];
            if (t >= 1f) return new PanoramaChoice(null, top, 0f, 1f);
            string under = Names[(season + SeasonCount - 1) % SeasonCount];
            float underAlpha = Math.Min(1f, 2f * (1f - t));
            return new PanoramaChoice(under, top, underAlpha, t);
        }

        /// <summary>Rang de la saison précédente (printemps : l'hiver).</summary>
        public static int Previous(int season) => (season + SeasonCount - 1) % SeasonCount;
    }
}
