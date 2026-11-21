using System.Globalization;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.12.4 : la classe de la machine pour l'affichage.</summary>
    public enum DeviceTier
    {
        /// <summary>Un téléphone (Note8) : réglages allégés du sous-lot 11.8, ressources « jamais sur téléphone » écartées.</summary>
        Phone = 0,

        /// <summary>Un PC modeste (celui de Damien : 4 Go de RAM, GTX 470 à 1,2 Go) : textures réduites, ressources lourdes écartées.</summary>
        Modest = 1,

        /// <summary>Un PC assez fort : tout se montre, textures pleines.</summary>
        Full = 2,
    }

    /// <summary>
    /// Réglage « Qualité » (demande de Damien du 21 novembre 2026, remplace le choix des textures du sous-lot V2.12.4) : Automatique
    /// (détection de la machine, par défaut), ou un niveau forcé.
    /// </summary>
    public enum QualityChoice
    {
        Auto = 0,
        Low = 1,
        Medium = 2,
        High = 3,
    }

    /// <summary>Niveau d'affichage retenu : classe utilisée (chargement par cases, brume, images), ressources lourdes, limite de mipmaps.</summary>
    public readonly struct QualityLevel
    {
        public QualityLevel(DeviceTier tier, bool light, int mipLimit)
        {
            Tier = tier;
            Light = light;
            MipLimit = mipLimit;
        }

        /// <summary>Classe dont les réglages s'appliquent (<c>StreamingProfile.For</c>, distances des arbres, taille des images).</summary>
        public DeviceTier Tier { get; }

        /// <summary>Ressources « jamais sur téléphone » écartées ?</summary>
        public bool Light { get; }

        /// <summary>Limite de mipmaps des textures (0 pleine, 1 moitié, 2 quart).</summary>
        public int MipLimit { get; }

        /// <summary>Même classe et mêmes ressources lourdes (ce qui ne change qu'au lancement du jeu) ?</summary>
        public bool SameSession(QualityLevel other) => Tier == other.Tier && Light == other.Light;
    }

    /// <summary>
    /// Sous-lot V2.12.4 (essai de Damien du 9 novembre 2026 : « System out of memory » en sortant d'une maison, textures 2K/4K sur une GTX 470
    /// à 1224 Mo) : règle partagée, sans Unity, qui classe la machine et donne la limite de mipmaps des textures
    /// (<c>QualitySettings.globalTextureMipmapLimit</c> : 0 pleine, 1 moitié, 2 quart). Affichage seulement.
    /// </summary>
    public static class MachineTier
    {
        /// <summary>En dessous (Mo de mémoire vidéo) : PC modeste.</summary>
        public const int ModestVram = 3072;

        /// <summary>En dessous (Mo de mémoire système) : PC modeste.</summary>
        public const int ModestRam = 6144;

        /// <summary>Jusqu'à (Mo de mémoire vidéo, compris) : textures au quart sur un PC modeste.</summary>
        public const int QuarterVram = 1536;

        /// <summary>Jusqu'à (Mo de mémoire système, comprise) : textures au quart sur un PC modeste.</summary>
        public const int QuarterRam = 4096;

        /// <summary>
        /// Classe de la machine. Une mémoire inconnue (0 ou moins, ce que Unity rend parfois) ne compte pas : une machine dont les deux
        /// mémoires sont inconnues est classée <see cref="DeviceTier.Full"/> (comme avant ce sous-lot).
        /// </summary>
        public static DeviceTier Detect(int vramMb, int ramMb, bool phone)
        {
            if (phone) return DeviceTier.Phone;
            bool lowVram = vramMb > 0 && vramMb < ModestVram;
            bool lowRam = ramMb > 0 && ramMb < ModestRam;
            return lowVram || lowRam ? DeviceTier.Modest : DeviceTier.Full;
        }

        /// <summary>
        /// Limite de mipmaps automatique : 0 sur téléphone (inchangé) et sur PC fort ; sur PC modeste, 2 (quart) si la mémoire vidéo est
        /// d'au plus 1536 Mo ou la mémoire système d'au plus 4096 Mo, sinon 1 (moitié).
        /// </summary>
        public static int AutoMipLimit(int vramMb, int ramMb, bool phone)
        {
            if (Detect(vramMb, ramMb, phone) != DeviceTier.Modest) return 0;
            bool quarter = (vramMb > 0 && vramMb <= QuarterVram) || (ramMb > 0 && ramMb <= QuarterRam);
            return quarter ? 2 : 1;
        }

        /// <summary>Les ressources « jamais sur téléphone » sont-elles écartées ? (téléphone ou PC modeste)</summary>
        public static bool Light(DeviceTier tier) => tier != DeviceTier.Full;

        /// <summary>
        /// Niveau retenu pour un réglage sur cette machine. Automatique : la détection (classe, ressources lourdes, limite automatique).
        /// Basse : comme un PC modeste aux textures au quart (un téléphone reste téléphone). Moyenne : chargement et brume d'un PC complet,
        /// textures à moitié, ressources lourdes écartées. Haute : tout, textures pleines.
        /// </summary>
        public static QualityLevel Level(QualityChoice choice, int vramMb, int ramMb, bool phone)
        {
            DeviceTier detected = Detect(vramMb, ramMb, phone);
            return choice switch
            {
                QualityChoice.Low => new QualityLevel(detected == DeviceTier.Phone ? DeviceTier.Phone : DeviceTier.Modest, true, 2),
                QualityChoice.Medium => new QualityLevel(DeviceTier.Full, true, 1),
                QualityChoice.High => new QualityLevel(DeviceTier.Full, false, 0),
                _ => new QualityLevel(detected, Light(detected), AutoMipLimit(vramMb, ramMb, phone)),
            };
        }

        /// <summary>Haute forcée sur un téléphone ou un PC modeste : avertir que le jeu peut ralentir.</summary>
        public static bool Risky(QualityChoice choice, int vramMb, int ramMb, bool phone) =>
            choice == QualityChoice.High && Detect(vramMb, ramMb, phone) != DeviceTier.Full;

        /// <summary>Réglage suivant au bouton : automatique, basse, moyenne, haute, puis automatique.</summary>
        public static QualityChoice Next(QualityChoice choice) => choice switch
        {
            QualityChoice.Auto => QualityChoice.Low,
            QualityChoice.Low => QualityChoice.Medium,
            QualityChoice.Medium => QualityChoice.High,
            _ => QualityChoice.Auto,
        };

        /// <summary>Réglage lu des préférences (valeur hors de la liste : Automatique).</summary>
        public static QualityChoice Choice(int stored) =>
            stored >= (int)QualityChoice.Auto && stored <= (int)QualityChoice.High ? (QualityChoice)stored : QualityChoice.Auto;

        /// <summary>Nom d'un réglage : automatique, basse, moyenne, haute.</summary>
        public static string ChoiceName(QualityChoice choice) => choice switch
        {
            QualityChoice.Low => "basse",
            QualityChoice.Medium => "moyenne",
            QualityChoice.High => "haute",
            _ => "automatique",
        };

        /// <summary>Nom d'une limite de mipmaps : pleine, moitié, quart.</summary>
        public static string LimitName(int limit) => limit <= 0 ? "pleine" : limit == 1 ? "moitié" : "quart";

        /// <summary>« textures pleines », « textures à moitié », « textures au quart ».</summary>
        public static string TexturesPhrase(int limit) => limit <= 0 ? "textures pleines" : limit == 1 ? "textures à moitié" : "textures au quart";

        /// <summary>Avertissement court en Haute sur une petite machine.</summary>
        public const string SlowWarning = "peut ralentir sur cette machine";

        /// <summary>
        /// Texte du bouton des réglages, par exemple « Qualité : automatique (textures au quart) » ou « Qualité : haute », puis à la ligne
        /// « au prochain lancement · peut ralentir sur cette machine ». Les textures changent tout de suite ; la classe et les ressources lourdes,
        /// lues une fois au lancement (<paramref name="session"/>), changent au prochain lancement si elles diffèrent.
        /// </summary>
        public static string Label(QualityChoice choice, QualityLevel wanted, QualityLevel session, bool risky)
        {
            string text = "Qualité : " + ChoiceName(choice) + (choice == QualityChoice.Auto ? " (" + TexturesPhrase(wanted.MipLimit) + ")" : "");
            string notes = !wanted.SameSession(session) ? "au prochain lancement" : "";
            if (risky) notes += (notes.Length > 0 ? " · " : "") + SlowWarning;
            return notes.Length > 0 ? text + "\n" + notes : text;
        }

        /// <summary>Nom d'une classe de machine.</summary>
        public static string TierName(DeviceTier tier) => tier == DeviceTier.Phone ? "téléphone" : tier == DeviceTier.Modest ? "PC modeste" : "PC complet";

        /// <summary>Ligne du vérificateur : classe détectée, mémoires, limite de textures appliquée (réglage Automatique).</summary>
        public static string Describe(int vramMb, int ramMb, bool phone, int applied) => Describe(vramMb, ramMb, phone, applied, QualityChoice.Auto);

        /// <summary>Ligne du vérificateur avec le réglage du joueur : un niveau forcé est dit, et les ressources lourdes suivent ce niveau.</summary>
        public static string Describe(int vramMb, int ramMb, bool phone, int applied, QualityChoice choice)
        {
            DeviceTier tier = Detect(vramMb, ramMb, phone);
            QualityLevel level = Level(choice, vramMb, ramMb, phone);
            string forced = choice == QualityChoice.Auto ? "" : " ; qualité forcée : " + ChoiceName(choice) + " (réglages d'un " + TierName(level.Tier) + ")"
                + (Risky(choice, vramMb, ramMb, phone) ? ", " + SlowWarning : "");
            string light = level.Light && !phone ? " ; ressources lourdes (« jamais sur téléphone ») écartées" : "";
            return TierName(tier) + " (mémoire vidéo " + Mb(vramMb) + ", mémoire " + Mb(ramMb) + ")" + forced + " ; textures : " + LimitName(applied)
                + " (limite de mipmaps " + applied.ToString(CultureInfo.InvariantCulture) + ")" + light;
        }

        private static string Mb(int mb) => mb > 0 ? mb.ToString(CultureInfo.InvariantCulture) + " Mo" : "inconnue";
    }
}
