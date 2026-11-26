using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Domain.Space;

namespace EdS.Runtime
{
    /// <summary>Chantier V2.50.1b : ce que dessine un obstacle des grottes noyées du lac (bassins des roseaux, salle des épaves).</summary>
    public enum DrownedPiece
    {
        /// <summary>Rien de propre aux grottes noyées.</summary>
        None = 0,

        /// <summary>Paroi de calcaire mouillé (occulteur).</summary>
        Wall = 1,

        /// <summary>Pilier d'une voûte basse (occulteur) : la voûte est posée dessus.</summary>
        Vault = 2,

        /// <summary>Bassin naturel (eau, bas : on tire par-dessus).</summary>
        Basin = 3,

        /// <summary>Rocher usé par l'eau (occulteur).</summary>
        Rock = 4,

        /// <summary>Touffe d'algues (bas).</summary>
        Algae = 5,

        /// <summary>Épave d'une barque (occulteur).</summary>
        Wreck = 6,

        /// <summary>Coffre englouti (bas).</summary>
        Chest = 7,

        /// <summary>Repaire des pilleurs d'épaves (caisses, filets, lanternes) : sa lumière.</summary>
        Lair = 8,

        /// <summary>Ancre rouillée de l'épave.</summary>
        Anchor = 9,
    }

    /// <summary>
    /// Chantier V2.50.1b (grottes noyées du lac) : calculs purs du décor, testés sans Unity. Pièces reconnues au nom des obstacles
    /// (<see cref="PieceFor"/>), modèles du kit pris dans <c>Resources/Noyees/&lt;nom&gt;</c> (<see cref="Models"/>, repli en formes simples),
    /// hauteurs des fiches, eau des passages noyés posée devant chaque porte d'eau, côté paroi (<see cref="PoolAt"/>), entrée dans les
    /// roselières côté marais (<see cref="SurfaceModel"/>). Les murs sont tuilés d'après les bornes réelles du modèle (<see cref="SewerLooks.WallTiles"/>,
    /// <see cref="SewerLooks.TileFit"/>) ; les autres pièces mises à l'échelle sans étirement (<see cref="SewerLooks.Fit"/>), avant le lacet.
    /// </summary>
    public static class DrownedLooks
    {
        /// <summary>Dossier des modèles dans Unity (<c>Resources/Noyees/&lt;nom&gt;</c>).</summary>
        public const string Folder = "Noyees/";

        /// <summary>Dossier des modèles dans le projet Unity : <c>&lt;nom&gt;.fbx</c>, <c>&lt;nom&gt;_couleur.jpg</c>, <c>&lt;nom&gt;_normale.png</c> à plat.</summary>
        public const string AssetFolder = "Assets/EdS/Modeles/Resources/Noyees";

        public const string WallModel = "mur_noyee", VaultModel = "voute_noyee", BasinModel = "bassin_noyee", PassageModel = "passage_noyee", WreckModel = "epave_noyee",
            ChestModel = "coffre_noyee", AlgaeModel = "algues_noyee", RockModel = "rocher_noyee", ReedsModel = "roseaux_noyee", StalactiteModel = "stalactite_noyee",
            LairModel = "repaire_noyee", AnchorModel = "ancre_noyee";

        /// <summary>Les 12 modèles du kit de grotte noyée (atelier : <c>Noyees/modeles.json</c>), dans l'ordre des fiches.</summary>
        public static readonly IReadOnlyList<string> Models = new[]
        {
            WallModel, VaultModel, BasinModel, PassageModel, WreckModel, ChestModel, AlgaeModel, RockModel, ReedsModel, StalactiteModel, LairModel, AnchorModel,
        };

        private static readonly (string Prefix, DrownedPiece Piece)[] Prefixes =
        {
            ("mur_noyee", DrownedPiece.Wall), ("voute_noyee", DrownedPiece.Vault), ("bassin_noyee", DrownedPiece.Basin), ("rocher_noyee", DrownedPiece.Rock),
            ("algues_noyee", DrownedPiece.Algae), ("epave_noyee", DrownedPiece.Wreck), ("coffre_noyee", DrownedPiece.Chest),
            ("repaire_noyee", DrownedPiece.Lair), ("ancre_noyee", DrownedPiece.Anchor),
        };

        /// <summary>Ce que dessine l'obstacle <paramref name="obstacleId"/> (identifiant exact ou suivi de « _… »), <see cref="DrownedPiece.None"/> sinon.</summary>
        public static DrownedPiece PieceFor(string obstacleId)
        {
            if (string.IsNullOrEmpty(obstacleId)) return DrownedPiece.None;
            foreach ((string prefix, DrownedPiece piece) in Prefixes)
                if (obstacleId == prefix || obstacleId.StartsWith(prefix + "_", StringComparison.Ordinal)) return piece;
            return DrownedPiece.None;
        }

        /// <summary>Nom du modèle de <paramref name="piece"/> ; null pour <see cref="DrownedPiece.None"/>.</summary>
        public static string? ModelFor(DrownedPiece piece) => piece switch
        {
            DrownedPiece.Wall => WallModel,
            DrownedPiece.Vault => VaultModel,
            DrownedPiece.Basin => BasinModel,
            DrownedPiece.Rock => RockModel,
            DrownedPiece.Algae => AlgaeModel,
            DrownedPiece.Wreck => WreckModel,
            DrownedPiece.Chest => ChestModel,
            DrownedPiece.Lair => LairModel,
            DrownedPiece.Anchor => AnchorModel,
            _ => null,
        };

        /// <summary>Les pièces hautes cachent le personnage : enregistrées comme occulteurs (fondues ou coupées comme les autres murs).</summary>
        public static bool Occludes(DrownedPiece piece) =>
            piece == DrownedPiece.Wall || piece == DrownedPiece.Vault || piece == DrownedPiece.Rock || piece == DrownedPiece.Wreck;

        /// <summary>Eau (bassin) : posée au ras du sol.</summary>
        public static bool IsWater(DrownedPiece piece) => piece == DrownedPiece.Basin;

        /// <summary>Hauteur (m) de chaque pièce, celle de sa fiche (voûte basse : 3 m) ; 0 pour <see cref="DrownedPiece.None"/>.</summary>
        public static double HeightOf(DrownedPiece piece) => piece switch
        {
            DrownedPiece.Wall => 5.0,
            DrownedPiece.Vault => 3.0,
            DrownedPiece.Basin => 0.8,
            DrownedPiece.Rock => 3.0,
            DrownedPiece.Algae => 1.2,
            DrownedPiece.Wreck => 2.5,
            DrownedPiece.Chest => 0.8,
            DrownedPiece.Lair => 2.2,
            DrownedPiece.Anchor => 3.0,
            _ => 0.0,
        };

        /// <summary>Le mur se répète en tuiles (longueur et épaisseur de sa fiche) ; les autres pièces non.</summary>
        public static bool Tiled(DrownedPiece piece) => piece == DrownedPiece.Wall;

        /// <summary>Vrai si l'un des obstacles est une pièce de grotte noyée : le dessous est une grotte noyée (ni rideau d'eau, ni autre kit).</summary>
        public static bool IsDrowned(IEnumerable<SpaceObstacle> obstacles) => obstacles != null && obstacles.Any(o => o != null && PieceFor(o.Id) != DrownedPiece.None);

        /// <summary>Lanterne du repaire des pilleurs (couleur de 0 à 1), intensité et portée (m).</summary>
        public static readonly (double R, double G, double B) LanternColor = (1.0, 0.72, 0.4);

        public const double LanternIntensity = 1.4, LanternRange = 7.0;

        /// <summary>Plafond des grottes noyées (m) : les stalactites y pendent ; leur hauteur, celle de leur fiche (m).</summary>
        public const double CeilingHeight = 5.0, StalactiteHeight = 2.5, StalactiteSide = 2.0;

        /// <summary>
        /// Stalactites : une grappe au plafond au-dessus de chaque rocher et de chaque pilier de voûte (centre de l'emprise), dans l'ordre des
        /// obstacles. Décor seulement (au-dessus des têtes : <see cref="CeilingHeight"/> moins <see cref="StalactiteHeight"/>).
        /// </summary>
        public static IReadOnlyList<(string Id, double X, double Z)> Stalactites(IEnumerable<SpaceObstacle> obstacles) =>
            (obstacles ?? Enumerable.Empty<SpaceObstacle>()).Where(o => o != null && (PieceFor(o.Id) == DrownedPiece.Rock || PieceFor(o.Id) == DrownedPiece.Vault))
                .Select(o => (o.Id, (o.Points.Min(p => p.X.Raw) + o.Points.Max(p => p.X.Raw)) / 2e4, (o.Points.Min(p => p.Z.Raw) + o.Points.Max(p => p.Z.Raw)) / 2e4))
                .ToList();

        /// <summary>Bas de la grappe de stalactites (m) : elle pend du plafond.</summary>
        public static double StalactiteBase => CeilingHeight - StalactiteHeight;

        /// <summary>Margelle d'un bassin en repli : largeur et hauteur de ses bordures (m).</summary>
        public const double RimWidth = 0.15, RimHeight = 0.12;

        /// <summary>
        /// Revue Codex de la PR #259 (P3) : margelle de repli du bassin [<paramref name="x0"/>, <paramref name="x1"/>] ×
        /// [<paramref name="z0"/>, <paramref name="z1"/>] : quatre bordures (centre et taille au sol) posées tout autour, à l'extérieur de
        /// l'emprise, jamais sur l'eau (une boîte pleine la cachait).
        /// </summary>
        public static IReadOnlyList<(double Cx, double Cz, double Sx, double Sz)> BasinRim(double x0, double z0, double x1, double z1)
        {
            double w = RimWidth, cx = (x0 + x1) / 2, cz = (z0 + z1) / 2, lx = x1 - x0 + 2 * w, lz = z1 - z0;
            return new[]
            {
                (cx, z0 - w / 2, lx, w),   // sud (coins compris)
                (cx, z1 + w / 2, lx, w),   // nord (coins compris)
                (x0 - w / 2, cz, w, lz),   // ouest
                (x1 + w / 2, cz, w, lz),   // est
            };
        }

        /// <summary>Longueur de l'eau d'un passage noyé (m), de la porte vers la paroi, et sa largeur (m).</summary>
        public const double PoolLength = 3.0, PoolWidth = 4.0;

        /// <summary>Recul de l'eau d'un passage noyé par rapport à la place de la porte (m) : on s'y tient au sec.</summary>
        public const double PoolGap = 0.8;

        /// <summary>
        /// Eau du passage noyé de la porte (<paramref name="x"/>, <paramref name="z"/>) : un rectangle de <see cref="PoolWidth"/> de large et de
        /// <see cref="PoolLength"/> de long au plus, tourné vers la paroi la plus proche (<see cref="CaveLooks.CurtainFacing"/>), à
        /// <see cref="PoolGap"/> de la porte, tenu dans l'aire [<paramref name="minX"/>, <paramref name="maxX"/>] × [<paramref name="minZ"/>,
        /// <paramref name="maxZ"/>] ; null s'il n'y reste pas 0,2 m.
        /// </summary>
        public static (double MinX, double MinZ, double MaxX, double MaxZ)? PoolAt(double x, double z, double minX, double minZ, double maxX, double maxZ)
        {
            var facing = CaveLooks.CurtainFacing(x, z, minX, minZ, maxX, maxZ);
            double half = PoolWidth / 2;
            double x0, x1, z0, z1;
            if (facing.OutX != 0)
            {
                double near = x + facing.OutX * PoolGap, far = x + facing.OutX * (PoolGap + PoolLength);
                x0 = Math.Min(near, far);
                x1 = Math.Max(near, far);
                z0 = z - half;
                z1 = z + half;
            }
            else
            {
                double near = z + facing.OutZ * PoolGap, far = z + facing.OutZ * (PoolGap + PoolLength);
                z0 = Math.Min(near, far);
                z1 = Math.Max(near, far);
                x0 = x - half;
                x1 = x + half;
            }
            x0 = Math.Max(x0, minX);
            z0 = Math.Max(z0, minZ);
            x1 = Math.Min(x1, maxX);
            z1 = Math.Min(z1, maxZ);
            return x1 - x0 >= 0.2 && z1 - z0 >= 0.2 ? (x0, z0, x1, z1) : ((double, double, double, double)?)null;
        }

        /// <summary>
        /// L'eau du passage noyé se voit-elle ? Toujours pour un passage toujours noyé (le siphon) ; à marée haute seulement pour un passage de
        /// marée (le chenal, à sec à marée basse). Faux pour une porte sans eau.
        /// </summary>
        public static bool PoolShown(bool water, bool tideOnly, bool highTide) => water && (!tideOnly || highTide);

        /// <summary>Hauteur du passage noyé (m) et sa largeur (m), d'après sa fiche.</summary>
        public const double PassageHeight = 3.0, PassageWidth = 4.0, PassageDepth = 3.0;

        /// <summary>Entrée dans les roselières, côté marais : taille de sa fiche (m).</summary>
        public const double ReedsWidth = 6.0, ReedsDepth = 4.0, ReedsHeight = 3.5;

        /// <summary>La porte de la surface <paramref name="doorId"/> qui descend aux grottes noyées : son modèle (l'entrée dans les roselières), ou null.</summary>
        public static string? SurfaceModel(string doorId) => doorId == "porte_grottes_noyees" ? ReedsModel : null;

        /// <summary>Lueur verte et bleue de l'eau des passages noyés (couleur de 0 à 1), intensité et portée (m).</summary>
        public static readonly (double R, double G, double B) PoolGlowColor = (0.35, 0.75, 0.7);

        public const double PoolGlowIntensity = 0.9, PoolGlowRange = 6.0;

        private static readonly Regex AssetName = new Regex("^(?<name>[a-z_]+)\\.fbx$", RegexOptions.CultureInvariant);

        /// <summary>Nom du modèle du kit (« mur_noyee ») d'un chemin d'asset Unity, ou null s'il n'est pas un modèle du kit.</summary>
        public static string? NameOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string file = path.Replace('\\', '/');
            int slash = file.LastIndexOf('/');
            Match m = AssetName.Match(slash >= 0 ? file.Substring(slash + 1) : file);
            return m.Success && Models.Contains(m.Groups["name"].Value) ? m.Groups["name"].Value : null;
        }
    }
}
