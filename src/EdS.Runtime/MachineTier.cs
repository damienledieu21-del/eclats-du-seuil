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

    /// <summary>Sous-lot V2.12.4 : choix du joueur pour la qualité des textures (réglages de la pause), Auto par défaut.</summary>
    public enum TextureChoice
    {
        Auto = 0,
        Full = 1,
        Half = 2,
        Quarter = 3,
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

        /// <summary>Limite de mipmaps appliquée : celle du choix du joueur, ou l'automatique pour <see cref="TextureChoice.Auto"/>.</summary>
        public static int MipLimit(TextureChoice choice, int vramMb, int ramMb, bool phone) => choice switch
        {
            TextureChoice.Full => 0,
            TextureChoice.Half => 1,
            TextureChoice.Quarter => 2,
            _ => AutoMipLimit(vramMb, ramMb, phone),
        };

        /// <summary>Les ressources « jamais sur téléphone » sont-elles écartées ? (téléphone ou PC modeste)</summary>
        public static bool Light(DeviceTier tier) => tier != DeviceTier.Full;

        /// <summary>Choix suivant au bouton des réglages : auto, pleine, moitié, quart, puis auto.</summary>
        public static TextureChoice Next(TextureChoice choice) => choice switch
        {
            TextureChoice.Auto => TextureChoice.Full,
            TextureChoice.Full => TextureChoice.Half,
            TextureChoice.Half => TextureChoice.Quarter,
            _ => TextureChoice.Auto,
        };

        /// <summary>Choix lu des préférences (valeur hors de la liste : Auto).</summary>
        public static TextureChoice Choice(int stored) =>
            stored >= (int)TextureChoice.Auto && stored <= (int)TextureChoice.Quarter ? (TextureChoice)stored : TextureChoice.Auto;

        /// <summary>Nom d'une limite de mipmaps : pleine, moitié, quart.</summary>
        public static string LimitName(int limit) => limit <= 0 ? "pleine" : limit == 1 ? "moitié" : "quart";

        /// <summary>Texte du bouton des réglages, par exemple « Qualité des textures : auto (quart) ».</summary>
        public static string Label(TextureChoice choice, int applied) =>
            "Qualité des textures : " + (choice == TextureChoice.Auto ? "auto (" + LimitName(applied) + ")" : LimitName(applied));

        /// <summary>Nom d'une classe de machine.</summary>
        public static string TierName(DeviceTier tier) => tier == DeviceTier.Phone ? "téléphone" : tier == DeviceTier.Modest ? "PC modeste" : "PC complet";

        /// <summary>Ligne du vérificateur : classe détectée, mémoires, limite de textures appliquée.</summary>
        public static string Describe(int vramMb, int ramMb, bool phone, int applied)
        {
            DeviceTier tier = Detect(vramMb, ramMb, phone);
            string light = Light(tier) && !phone ? " ; ressources lourdes (« jamais sur téléphone ») écartées" : "";
            return TierName(tier) + " (mémoire vidéo " + Mb(vramMb) + ", mémoire " + Mb(ramMb) + ") ; textures : " + LimitName(applied)
                + " (limite de mipmaps " + applied.ToString(CultureInfo.InvariantCulture) + ")" + light;
        }

        private static string Mb(int mb) => mb > 0 ? mb.ToString(CultureInfo.InvariantCulture) + " Mo" : "inconnue";
    }
}
