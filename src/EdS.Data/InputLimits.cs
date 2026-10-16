namespace EdS.Data
{
    /// <summary>
    /// Limites techniques des lecteurs (lot 7.1), non configurables : elles bornent le travail fait <b>avant</b> que les
    /// règles soient connues ou appliquées. Chacune est vérifiée avant le chargement complet de ce qu'elle protège :
    /// la taille d'un fichier avant de le lire, la taille d'un texte avant de l'encoder, la longueur d'un tableau avant
    /// de construire ses éléments.
    /// </summary>
    public static class InputLimits
    {
        /// <summary>Taille maximale d'un fichier de données JSON (règles, classe, scénario) et d'un texte JSON analysé : 16 Mio.</summary>
        public const long MaxDataFileBytes = 16L * 1024 * 1024;

        /// <summary>Taille maximale d'un journal rejoué : 256 Mio.</summary>
        public const long MaxLogFileBytes = 256L * 1024 * 1024;

        /// <summary>Profondeur maximale d'imbrication JSON (objets et tableaux).</summary>
        public const int MaxJsonDepth = 32;

        /// <summary>Nombre maximal d'erreurs conservées par rapport ; les suivantes sont seulement comptées.</summary>
        public const int MaxReportedErrors = 200;

        /// <summary>Taille lisible (« 16 Mio », « 1 234 octets »).</summary>
        public static string DescribeBytes(long bytes) =>
            bytes % (1024 * 1024) == 0 ? $"{bytes / (1024 * 1024)} Mio" : $"{bytes} octets";
    }
}
