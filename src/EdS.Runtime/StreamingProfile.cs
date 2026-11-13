using System;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.27 (docs/PLAN_GRANDE_CARTE.md, § 6) : réglages du chargement par cases selon la classe de la machine
    /// (<see cref="MachineTier"/>). Affichage seulement : rien ici n'entre dans le moteur, les journaux ou le réseau.
    /// Anneaux comptés en cases autour de la case d'ancrage (distance de Tchebychev) : anneau 0 (détail) jusqu'à
    /// <see cref="DetailRadius"/>, anneau 1 (formes simplifiées) jusqu'à <see cref="ViewRadius"/>, au-delà le terrain basse
    /// résolution de toute la région.
    /// </summary>
    public sealed class StreamingProfile
    {
        /// <summary>Pas du terrain basse résolution de la région (m) : maille de 32 m (plan, § 6.1).</summary>
        public const int FarStep = 32;

        /// <summary>Cases par côté d'un bloc du terrain lointain (8 × 128 m = 1 024 m : 12 blocs pour les Vallons, 12 appels de dessin).</summary>
        public const int FarBlockCells = 8;

        public DeviceTier Tier { get; }

        /// <summary>Anneau 0 : cases autour de la case d'ancrage (1 : 3 × 3 cases, 384 m ; 2 : 5 × 5 cases).</summary>
        public int DetailRadius { get; }

        /// <summary>Anneau 1 : jusqu'à ce rayon (3 : 7 × 7 cases, environ 900 m).</summary>
        public int ViewRadius { get; }

        /// <summary>Points par côté de la carte de hauteurs d'une case de l'anneau 0 (129 sur PC, 65 sur téléphone : plan, § 6.1).</summary>
        public int DetailResolution { get; }

        /// <summary>Points par côté d'une case de l'anneau 1.</summary>
        public int SimpleResolution { get; }

        /// <summary>Distance de vue (brume), en mètres (600 sur téléphone, 900 sur PC modeste, toute la région sur PC complet).</summary>
        public double ViewMeters { get; }

        /// <summary>Temps de construction par image (ms) : au plus 2 ms (plan, § 6.1).</summary>
        public double BudgetMs { get; }

        /// <summary>
        /// Hystérésis (m) : la case d'ancrage ne change que lorsque la caméra la dépasse de cette distance (pas d'aller-retour au bord
        /// d'une case ; même idée que les 8 m du passage de secteur, V2.26).
        /// </summary>
        public double HysteresisMeters { get; }

        /// <summary>Une case quittée (ou qui perd du détail) est gardée telle quelle ce temps (s) : pas d'à-coup en faisant demi-tour.</summary>
        public double UnloadDelaySeconds { get; }

        /// <summary>Jupe (m) sous les bords de chaque morceau de terrain : aucune fente entre deux finesses voisines.</summary>
        public double SkirtMeters { get; }

        /// <summary>Mémoire des maillages de terrain (octets, copie du processeur et de la carte graphique comprises).</summary>
        public long MemoryBudgetBytes { get; }

        /// <summary>Herbe à moins de (m) (anneau 0 seulement, plan, § 6.1).</summary>
        public double GrassMeters { get; }

        /// <summary>L'anneau 0 porte-t-il des ombres ? (jamais l'anneau 1 ni le terrain lointain)</summary>
        public bool DetailShadows { get; }

        /// <summary>Cibles du plan (§ 6.2), à mesurer : appels de dessin et triangles visibles.</summary>
        public int DrawCallBudget { get; }

        public long TriangleBudget { get; }

        /// <summary>
        /// Part des triangles visibles laissée au terrain des cases (4/5 de <see cref="TriangleBudget"/>) : au-delà, les cases gardées après
        /// leur départ rendent leur détail tout de suite, comme pour la mémoire.
        /// </summary>
        public long TerrainTriangleBudget => TriangleBudget * 4 / 5;

        private StreamingProfile(DeviceTier tier, int detail, int view, int detailRes, int simpleRes, double viewMeters, long memory,
            bool shadows, int drawCalls, long triangles)
        {
            Tier = tier;
            DetailRadius = detail;
            ViewRadius = view;
            DetailResolution = detailRes;
            SimpleResolution = simpleRes;
            ViewMeters = viewMeters;
            BudgetMs = 2.0;
            HysteresisMeters = 16.0;
            UnloadDelaySeconds = 10.0;
            SkirtMeters = 6.0;
            MemoryBudgetBytes = memory;
            GrassMeters = 40.0;
            DetailShadows = shadows;
            DrawCallBudget = drawCalls;
            TriangleBudget = triangles;
        }

        /// <summary>Réglages d'une classe de machine (plan, § 6.2 : téléphone Note8, PC modeste, PC complet).</summary>
        public static StreamingProfile For(DeviceTier tier) => tier switch
        {
            DeviceTier.Phone => new StreamingProfile(tier, 1, 2, 65, 17, 600, 16L << 20, false, 150, 300_000),
            DeviceTier.Modest => new StreamingProfile(tier, 1, 3, 129, 33, 900, 24L << 20, true, 300, 600_000),
            DeviceTier.Full => new StreamingProfile(tier, 2, 3, 129, 33, 6000, 96L << 20, true, 1500, 3_000_000),
            _ => throw new ArgumentOutOfRangeException(nameof(tier)),
        };

        /// <summary>Points par côté pour un anneau (0 ou 1) ; 0 au-delà (pas de maillage de case).</summary>
        public int ResolutionOf(int ring) => ring == 0 ? DetailResolution : ring == 1 ? SimpleResolution : 0;

        /// <summary>Anneau voulu d'une case à <paramref name="distance"/> cases de l'ancrage (2 : terrain lointain seulement).</summary>
        public int RingAt(int distance) => distance <= DetailRadius ? 0 : distance <= ViewRadius ? 1 : CellStreamer.Far;
    }
}
