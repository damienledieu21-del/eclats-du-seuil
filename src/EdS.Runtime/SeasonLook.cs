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

        /// <summary>
        /// V2.44.3 : neige du col (0 à <see cref="WinterSnow"/>), posée au-dessus de <see cref="SnowLine"/> (mètres, hauteur du relief
        /// affiché) avec un fondu de <see cref="SnowBand"/> m en dessous ; 0 sans contenu saisonnier de version 2.
        /// </summary>
        public float PassSnow { get; }

        public float SnowLine { get; }

        /// <summary>V2.44.3 : hauteur (m) du fondu sous la limite de la neige du col.</summary>
        public const float SnowBand = 25f;

        /// <summary>V2.44.3 : jours de fondu de la neige au début et à la fin de sa période (la neige s'installe, puis fond).</summary>
        public const int SnowRampDays = 7;

        /// <summary>Saison montrée (rang 0 à 3) et progression de la transition (0 à 1, déjà adoucie).</summary>
        public int Season { get; }
        public float Blend { get; }

        public SeasonLook(float r, float g, float b, float strength, float snow, float ice, int season, float blend)
            : this(r, g, b, strength, snow, ice, season, blend, 0f, 0f) { }

        public SeasonLook(float r, float g, float b, float strength, float snow, float ice, int season, float blend, float passSnow, float snowLine)
        {
            PassSnow = passSnow;
            SnowLine = snowLine;
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

        /// <summary>
        /// V2.44.3 (décisions de Damien du 20 novembre 2026) : la neige suit le contenu saisonnier de version 2 (<paramref name="snow"/>) au
        /// jour <paramref name="date"/> : en plaine pendant sa période, au col au-dessus de sa limite ; elle s'installe et fond sur
        /// <see cref="SnowRampDays"/> jours. Sans contenu ou sans date : la neige de la saison, comme avant.
        /// </summary>
        public SeasonLook WithSnow(EdS.Data.Seasons.SeasonSnow? snow, CivilDate? date)
        {
            if (snow is null || !(date is CivilDate d)) return this;
            return new SeasonLook(R, G, B, Strength, WinterSnow * Cover(snow.Lowland, d), Ice, Season, Blend, WinterSnow * Cover(snow.Pass, d),
                snow.PassAltitude.Raw / 10000f);
        }

        /// <summary>V2.44.3 : dans un intérieur, ni neige ni glace (la teinte reste).</summary>
        public SeasonLook Indoors() => new SeasonLook(R, G, B, Strength, 0f, 0f, Season, Blend);

        /// <summary>Part de neige (0 à 1) de la période <paramref name="span"/> au jour <paramref name="date"/> : fondu de 7 jours aux deux bouts.</summary>
        public static float Cover(EdS.Data.Seasons.AnnualSpan span, CivilDate date)
        {
            if (!span.Contains(date)) return 0f;
            if (date.Month == 2 && date.Day == 29) date = new CivilDate(date.Year, 2, 28);   // le 29 février compte comme le 28
            long since = DaysFrom(span.From, date, back: true), until = DaysFrom(span.To, date, back: false);
            float f = Math.Min(since + 1, until + 1) / (float)SnowRampDays;
            return f > 1f ? 1f : f;
        }

        /// <summary>Jours entre <paramref name="date"/> et l'occurrence de <paramref name="day"/> juste avant (ou juste après) ; grand si hors du calendrier.</summary>
        private static long DaysFrom(EdS.Data.Seasons.AnnualDate day, CivilDate date, bool back)
        {
            for (int step = 0; step < 2; step++)   // relecture de la PR #232 : sans allocation
            {
                int y = back ? date.Year - step : date.Year + step;
                if (y < CivilDate.MinYear || y > CivilDate.MaxYear) continue;
                long n = new CivilDate(y, day.Month, day.Day).DayNumber - date.DayNumber;
                if (back ? n <= 0 : n >= 0) return Math.Abs(n);
            }
            return SnowRampDays;
        }

        /// <summary>Rendu sans date fournie : la saison du calendrier du jeu, sans transition ni glace.</summary>
        public static SeasonLook Steady(int season) => Of(season, season, 1f, 0f);

        public bool Equals(SeasonLook o) => R == o.R && G == o.G && B == o.B && Strength == o.Strength && Snow == o.Snow && Ice == o.Ice && Season == o.Season
            && Blend == o.Blend && PassSnow == o.PassSnow && SnowLine == o.SnowLine;
        public override bool Equals(object? obj) => obj is SeasonLook l && Equals(l);
        public override int GetHashCode() => (Season * 397) ^ R.GetHashCode() ^ (Snow.GetHashCode() * 7);
    }
}
