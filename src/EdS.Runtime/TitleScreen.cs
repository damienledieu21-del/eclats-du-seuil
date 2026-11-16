using System;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.55 : le moment de la journée d'un décor de l'écran titre.</summary>
    public enum TitleMoment
    {
        Dawn = 0,
        Day = 1,
        Night = 2,

        /// <summary>Crépuscule : quatre peintures à commander (une par saison, même cadrage) ; en attendant, <see cref="TitleScreen.DuskPainted"/> est faux et l'aube les remplace.</summary>
        Dusk = 3,
    }

    /// <summary>
    /// Sous-lot V2.55 : un décor de l'écran titre (douze peintures : quatre saisons, trois moments, plus l'Envers) et la façon de le mélanger avec
    /// le suivant. <see cref="Primary"/> est montré en entier, <see cref="Secondary"/> par-dessus avec l'opacité <see cref="Mix"/> (0 : seul le premier).
    /// </summary>
    public readonly struct TitleDecor
    {
        /// <summary>Numéro du dossier de livraison (1 à 13).</summary>
        public int Number { get; }

        /// <summary>Saison (0 printemps, 1 été, 2 automne, 3 hiver) ; -1 pour l'Envers.</summary>
        public int Season { get; }

        public TitleMoment Moment { get; }

        public TitleDecor(int number, int season, TitleMoment moment)
        {
            Number = number;
            Season = season;
            Moment = moment;
        }

        /// <summary>L'Envers (fêlure ouverte).</summary>
        public bool IsEnvers => Number == TitleScreen.EnversNumber;
    }

    /// <summary>Le choix du moment pour une minute du jour : deux moments voisins et le mélange de l'un vers l'autre.</summary>
    public readonly struct TitleMix
    {
        public TitleDecor Primary { get; }
        public TitleDecor Secondary { get; }

        /// <summary>Opacité du second décor par-dessus le premier, de 0 à 1.</summary>
        public float Mix { get; }

        public TitleMix(TitleDecor primary, TitleDecor secondary, float mix)
        {
            Primary = primary;
            Secondary = secondary;
            Mix = mix;
        }
    }

    /// <summary>Le placement d'un élément de l'écran titre dans la fenêtre : rectangle en unités de la fenêtre.</summary>
    public readonly struct TitleRect
    {
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }

        public TitleRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// Sous-lot V2.55 (sujet 13, « L'âme du jeu » : écran titre vivant) : les règles de l'écran titre, sans Unity. Le décor suit la saison du jeu et
    /// l'heure du monde (aube, jour, nuit, avec un fondu d'une heure entre deux) ; l'Envers remplace tout quand l'hôte dit qu'il est là (aucun état
    /// Envers n'existe encore dans le jeu : la règle est prête, l'hôte passe <c>false</c>). Le placement du personnage et du logo est donné dans le
    /// repère des peintures (3 840 × 2 160) et converti pour une fenêtre quelconque (image recadrée sans déformation, comme « ScaleAndCrop »).
    /// Un seul décor, le Seuil des Vallons, sert à tous les lieux. Affichage seulement : rien ici ne touche à la partie.
    /// </summary>
    public static class TitleScreen
    {
        /// <summary>Numéro de l'image de l'Envers.</summary>
        public const int EnversNumber = 13;

        /// <summary>Numéro de la première peinture de crépuscule (14 à 17, une par saison) : le créneau est réservé, les images ne sont pas livrées.</summary>
        public const int DuskFirstNumber = 14;

        /// <summary>
        /// Les quatre peintures de crépuscule sont-elles livrées ? <b>Non</b> (décision de Damien du 17 novembre 2026 : on les commandera) : tant que
        /// c'est faux, le crépuscule reprend la peinture de l'aube. Passer à vrai quand les images <c>titre_14…17</c> sont dans le dépôt.
        /// </summary>
        public const bool DuskPainted = false;

        /// <summary>Largeur et hauteur des peintures d'origine (pixels) ; tous les repères ci-dessous sont dans ce repère.</summary>
        public const float ReferenceWidth = 3840f, ReferenceHeight = 2160f;

        /// <summary>Bande de sécurité (hauteur) : un recadrage horizontal ne garde que ce qui est entre ces deux lignes.</summary>
        public const float SafeTop = 324f, SafeBottom = 1836f;

        /// <summary>Point des pieds du personnage (sur la terrasse plane, au centre-gauche).</summary>
        public const float FeetX = 1280f, FeetY = 1776f;

        /// <summary>Enveloppe du personnage : un tiers de la hauteur de l'image.</summary>
        public const float HeroX = 960f, HeroY = 1056f, HeroWidth = 640f, HeroHeight = 720f;

        /// <summary>Canevas proposé pour le logo 2 : 1 (zone calme en haut à droite).</summary>
        public const float LogoX = 2880f, LogoY = 270f, LogoWidth = 900f, LogoHeight = 450f;

        // Minutes du jour (heure du monde) où un moment passe au suivant, et demi-largeur du fondu (une heure au total).
        private const int DawnStarts = 300, DayStarts = 540, DuskStarts = 1050, NightStarts = 1260, HalfFade = 30;

        /// <summary>Fondu d'entrée de l'écran (secondes), puis de sortie quand le joueur lance le jeu.</summary>
        public const float FadeInSeconds = 0.8f, FadeOutSeconds = 0.5f;

        /// <summary>Un geste ne lance pas le jeu avant ce délai après l'apparition de l'écran (secondes).</summary>
        public const float StartAfter = 0.8f;

        private static readonly string[] SeasonNames = { "printemps", "ete", "automne", "hiver" };
        private static readonly string[] MomentNames = { "aube", "jour", "nuit", "crepuscule" };

        /// <summary>Le décor d'une saison et d'un moment (saison de 0 à 3).</summary>
        public static TitleDecor Decor(int season, TitleMoment moment)
        {
            if (season < 0 || season > 3) throw new ArgumentOutOfRangeException(nameof(season));
            int number = moment == TitleMoment.Dusk ? DuskFirstNumber + season : season * 3 + (int)moment + 1;
            return new TitleDecor(number, season, moment);
        }

        /// <summary>Le décor de l'Envers.</summary>
        public static TitleDecor Envers => new TitleDecor(EnversNumber, -1, TitleMoment.Night);

        /// <summary>
        /// Le choix du décor : l'Envers seul s'il est là ; sinon, d'après la minute du jour (0 à 1 439, toute valeur est ramenée dans le jour),
        /// le moment et son voisin avec le mélange. Le crépuscule (de 17 h 30 à 21 h) a sa peinture quand <paramref name="dusk"/> est vrai (<see cref="DuskPainted"/>), sinon il reprend celle de l'aube (soleil bas, tons chauds).
        /// </summary>
        public static TitleMix Pick(int season, int minuteOfDay, bool envers, bool dusk = DuskPainted)
        {
            if (envers) return new TitleMix(Envers, Envers, 0f);
            int m = ((minuteOfDay % 1440) + 1440) % 1440;
            // Segments : [Nuit jusqu'à 5 h] [Aube 5 h - 9 h] [Jour 9 h - 17 h 30] [Aube (crépuscule) 17 h 30 - 21 h] [Nuit dès 21 h].
            TitleMoment evening = dusk ? TitleMoment.Dusk : TitleMoment.Dawn, from, to;
            int boundary;
            if (m < DawnStarts - HalfFade) return Single(season, TitleMoment.Night);
            if (m < DawnStarts + HalfFade) { from = TitleMoment.Night; to = TitleMoment.Dawn; boundary = DawnStarts; }
            else if (m < DayStarts - HalfFade) return Single(season, TitleMoment.Dawn);
            else if (m < DayStarts + HalfFade) { from = TitleMoment.Dawn; to = TitleMoment.Day; boundary = DayStarts; }
            else if (m < DuskStarts - HalfFade) return Single(season, TitleMoment.Day);
            else if (m < DuskStarts + HalfFade) { from = TitleMoment.Day; to = evening; boundary = DuskStarts; }
            else if (m < NightStarts - HalfFade) return Single(season, evening);
            else if (m < NightStarts + HalfFade) { from = evening; to = TitleMoment.Night; boundary = NightStarts; }
            else return Single(season, TitleMoment.Night);
            float t = (m - (boundary - HalfFade)) / (2f * HalfFade);
            t = t * t * (3f - 2f * t);
            return new TitleMix(Decor(season, from), Decor(season, to), t);
        }

        private static TitleMix Single(int season, TitleMoment moment) => new TitleMix(Decor(season, moment), Decor(season, moment), 0f);

        /// <summary>Chemin dans <c>Resources</c> (sans extension) : 1 920 × 1 080 sur PC, 1 280 × 720 sur téléphone et PC modeste.</summary>
        public static string ResourcePath(TitleDecor decor, DeviceTier tier)
        {
            string name = decor.IsEnvers
                ? "13_envers"
                : decor.Number.ToString("00") + "_" + SeasonNames[decor.Season] + "_" + MomentNames[(int)decor.Moment];
            return "EdsInterface/Titre/" + (MachineTier.Light(tier) ? "1280" : "1920") + "/titre_" + name;
        }

        /// <summary>Teinte de la lumière qui éclaire le personnage (rouge, vert, bleu de 0 à 1) et sa force, selon le décor : le personnage est dans la lumière du décor.</summary>
        public static void LightFor(TitleDecor decor, out float r, out float g, out float b, out float intensity)
        {
            if (decor.IsEnvers) { r = 0.62f; g = 0.42f; b = 0.82f; intensity = 0.7f; return; }
            switch (decor.Moment)
            {
                case TitleMoment.Dawn: r = 1f; g = 0.78f; b = 0.58f; intensity = 1f; break;
                case TitleMoment.Dusk: r = 1f; g = 0.6f; b = 0.55f; intensity = 0.85f; break;
                case TitleMoment.Day: r = 1f; g = 0.96f; b = 0.88f; intensity = 1.15f; break;
                default: r = 0.45f; g = 0.58f; b = 0.9f; intensity = 0.55f; break;
            }
        }

        /// <summary>Lumière du personnage pour un mélange de décors (interpolée entre les deux).</summary>
        public static void LightFor(TitleMix mix, out float r, out float g, out float b, out float intensity)
        {
            LightFor(mix.Primary, out float r1, out float g1, out float b1, out float i1);
            LightFor(mix.Secondary, out float r2, out float g2, out float b2, out float i2);
            float t = mix.Mix;
            r = r1 + (r2 - r1) * t;
            g = g1 + (g2 - g1) * t;
            b = b1 + (b2 - b1) * t;
            intensity = i1 + (i2 - i1) * t;
        }

        /// <summary>Opacité de l'écran : fondu d'entrée, plateau tant que le joueur n'a rien fait, fondu de sortie après le lancement (<paramref name="leaving"/> secondes écoulées depuis le geste, négatif : pas encore).</summary>
        public static float Opacity(float shown, float leaving)
        {
            if (shown < 0f) return 0f;
            float inn = Math.Min(1f, shown / FadeInSeconds);
            if (leaving < 0f) return inn;
            return Math.Min(inn, Math.Max(0f, 1f - leaving / FadeOutSeconds));
        }

        /// <summary>Le menu répond-il aux appuis ? (l'écran est montré depuis assez longtemps)</summary>
        public static bool GestureStarts(float shown) => shown >= StartAfter;

        /// <summary>Colonne du menu (repère des peintures) : à droite sous le logo, loin du personnage.</summary>
        public const float MenuX = 2640f, MenuY = 900f, MenuWidth = 840f, MenuRowHeight = 150f;

        /// <summary>Rectangle de la rangée <paramref name="index"/> du menu pour une fenêtre donnée (rangées l'une sous l'autre, sans trou).</summary>
        public static TitleRect MenuRow(int index, float width, float height) =>
            Place(MenuX, MenuY + index * MenuRowHeight, MenuWidth, MenuRowHeight, width, height);

        /// <summary>L'écran est terminé (le fondu de sortie est fini).</summary>
        public static bool Finished(float leaving) => leaving >= FadeOutSeconds;

        /// <summary>Pulsation douce de l'invite (« Appuyez… ») : opacité de 0,45 à 1, période de 2,4 secondes.</summary>
        public static float PromptPulse(float shown) => 0.725f + 0.275f * (float)Math.Sin(shown * 2.0 * Math.PI / 2.4);

        /// <summary>Lente dérive du décor (zoom très léger) : échelle de 1 à 1,02 et retour, période de 40 secondes. Un repos vivant, sans mouvement brusque.</summary>
        public static float Drift(float shown) => 1.01f + 0.01f * (float)Math.Sin(shown * 2.0 * Math.PI / 40.0);

        /// <summary>Fenêtre de <paramref name="width"/> × <paramref name="height"/> : échelle et décalage qui recadrent le repère 3 840 × 2 160 sans déformation (l'image couvre toute la fenêtre, centrée).</summary>
        public static void Cover(float width, float height, out float scale, out float offsetX, out float offsetY)
        {
            if (width <= 0f || height <= 0f) throw new ArgumentOutOfRangeException(nameof(width));
            scale = Math.Max(width / ReferenceWidth, height / ReferenceHeight);
            offsetX = (width - ReferenceWidth * scale) / 2f;
            offsetY = (height - ReferenceHeight * scale) / 2f;
        }

        /// <summary>Un rectangle du repère des peintures, converti pour la fenêtre (même recadrage que le décor).</summary>
        public static TitleRect Place(float x, float y, float w, float h, float width, float height)
        {
            Cover(width, height, out float s, out float ox, out float oy);
            return new TitleRect(ox + x * s, oy + y * s, w * s, h * s);
        }

        /// <summary>
        /// Le rectangle pour le logo (canevas 2 : 1) : celui de la peinture, mais jamais plus étroit que <paramref name="minWidth"/> (petite fenêtre)
        /// sans sortir à droite de la fenêtre ; remonté dans la bande de sécurité si le recadrage l'avait coupé.
        /// </summary>
        public static TitleRect LogoRect(float width, float height, float minWidth)
        {
            TitleRect r = Place(LogoX, LogoY, LogoWidth, LogoHeight, width, height);
            float w = Math.Max(r.Width, minWidth), h = w / 2f;
            float x = Math.Min(r.X + r.Width - w, width - w);
            float y = Math.Max(r.Y, 0f);
            return new TitleRect(Math.Max(0f, x), y, w, h);
        }
    }
}
