using System;
using EdS.Domain.Time;

namespace EdS.Runtime
{
    /// <summary>
    /// Saisons (décision de Damien du 14 novembre 2026 : neige et hiver blanc en effet visuel d'abord, sans règle de combat) : paramètres de
    /// rendu de la saison, pour le décor seulement : teinte de saison (rouge, vert, bleu autour de 0,5 = neutre, et force du mélange), neige
    /// posée sur les faces tournées vers le haut (0 à 1) et glace du lac (0 à 1). La transition de 7 jours mélange la saison d'avant et la
    /// nouvelle (courbe douce, « smoothstep », comme le propose le dossier des saisons). Rien de cela n'entre dans le monde ni dans le combat.
    /// </summary>
    public readonly struct SeasonLook : IEquatable<SeasonLook>
    {
        /// <summary>Force du mélange de la teinte de saison sur les feuillages et l'herbe (proposition, réglable).</summary>
        public const float TintStrength = 0.6f;

        /// <summary>Neige au plus fort de l'hiver (proposition : un hiver blanc qui laisse lire les chemins).</summary>
        public const float WinterSnow = 0.65f;

        public float R { get; }
        public float G { get; }
        public float B { get; }

        /// <summary>Force de la teinte (0 : aucune).</summary>
        public float Strength { get; }

        public float Snow { get; }
        public float Ice { get; }

        /// <summary>Saison montrée (rang 0 à 3) et progression de la transition (0 à 1, déjà adoucie).</summary>
        public int Season { get; }
        public float Blend { get; }

        public SeasonLook(float r, float g, float b, float strength, float snow, float ice, int season, float blend)
        {
            R = r;
            G = g;
            B = b;
            Strength = strength;
            Snow = snow;
            Ice = ice;
            Season = season;
            Blend = blend;
        }

        // Teintes proposées (autour de 0,5 = neutre) : printemps vert tendre, été chaud, automne roux, hiver froid.
        private static readonly float[,] Tints =
        {
            { 0.49f, 0.54f, 0.47f },
            { 0.53f, 0.51f, 0.45f },
            { 0.60f, 0.48f, 0.36f },
            { 0.47f, 0.49f, 0.53f },
        };

        private static float Smooth(float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return t * t * (3f - 2f * t);
        }

        private static float SnowOf(int season) => season == 3 ? WinterSnow : 0f;

        /// <summary>Rendu de la saison <paramref name="season"/>, venue de <paramref name="previous"/> à la progression <paramref name="transition"/> (0 à 1).</summary>
        public static SeasonLook Of(int season, int previous, float transition, float ice)
        {
            if (season < 0 || season > 3) throw new ArgumentOutOfRangeException(nameof(season));
            if (previous < 0 || previous > 3) throw new ArgumentOutOfRangeException(nameof(previous));
            float t = Smooth(transition);
            float Mix(int c) => Tints[previous, c] + (Tints[season, c] - Tints[previous, c]) * t;
            float snow = SnowOf(previous) + (SnowOf(season) - SnowOf(previous)) * t;
            float i = ice < 0f ? 0f : ice > 1f ? 1f : ice;
            return new SeasonLook(Mix(0), Mix(1), Mix(2), TintStrength, snow, i, season, t);
        }

        /// <summary>Rendu de l'état réel de la saison (date fournie par l'hôte ou le serveur).</summary>
        public static SeasonLook Of(SeasonState state) =>
            Of((int)state.Season, (int)state.Previous, state.Transition.Raw / 10000f, state.IceFormation.Raw / 10000f);

        /// <summary>Rendu sans date fournie : la saison du calendrier du jeu, sans transition ni glace.</summary>
        public static SeasonLook Steady(int season) => Of(season, season, 1f, 0f);

        public bool Equals(SeasonLook o) => R == o.R && G == o.G && B == o.B && Strength == o.Strength && Snow == o.Snow && Ice == o.Ice && Season == o.Season
            && Blend == o.Blend;
        public override bool Equals(object? obj) => obj is SeasonLook l && Equals(l);
        public override int GetHashCode() => (Season * 397) ^ R.GetHashCode() ^ (Snow.GetHashCode() * 7);
    }
}
