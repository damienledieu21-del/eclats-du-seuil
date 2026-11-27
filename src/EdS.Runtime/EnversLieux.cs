using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Zones;

namespace EdS.Runtime
{
    /// <summary>V2.52b : pièce d'un lieu de l'Envers (kit Envers2) d'après l'identifiant d'un obstacle (préfixe « envers_… »).</summary>
    public enum EnversLieuPiece
    {
        None = 0,

        /// <summary>Moulin renversé (« envers_moulin_renverse_… »), Envers du village.</summary>
        InvertedMill,

        /// <summary>Clocher renversé (« envers_clocher_renverse_… »), Envers du Bourg.</summary>
        InvertedBelfry,

        /// <summary>Cascade figée qui monte (« envers_cascade_figee_… »), Envers de la vallée.</summary>
        FrozenFall,

        /// <summary>Tonneaux renversés (« envers_tonneaux_renverses_… »), Envers des coteaux.</summary>
        Barrels,

        /// <summary>L'arbre millénaire mort (« envers_arbre_millenaire_mort »), Envers de la forêt.</summary>
        MillenaryTree,

        /// <summary>Cairn suspendu (« envers_cairn_suspendu_… »), Envers des collines et du col.</summary>
        FloatingCairn,

        /// <summary>Palissade calcinée (« envers_palissade_calcinee_… »), Envers du fort, en tuiles.</summary>
        BurntPalisade,

        /// <summary>Wagonnet suspendu (« envers_wagonnet_suspendu_… »), Envers de la carrière.</summary>
        HangingCart,

        /// <summary>Rosace brisée (« envers_rosace_brisee_… »), Envers de l'abbaye.</summary>
        BrokenRose,

        /// <summary>Barque noire (« envers_barque_noire_… »), Envers du lac.</summary>
        BlackBoat,

        /// <summary>Pont brisé flottant (« envers_pont_brise_… »), les trois passages.</summary>
        BrokenBridge,
    }

    /// <summary>
    /// Chantier V2.52b : le décor des douze lieux de l'Envers (kit Envers2 de Tripo, 13 modèles et 2 créatures, 27 novembre 2026), en fonctions
    /// pures testées : pièce et modèle d'un obstacle, hauteur, sol local (la pièce posée sur le relief affiché, hauteur du sol comprise), pièces
    /// d'une case (le décor est construit par cases autour de la caméra, au plus un nombre de pièces par case selon la machine), secrets
    /// (coffre, stèle), créatures (chien de cendre animé, spectre statique qui flotte en ondulant). Modèles pris dans
    /// <c>Resources/Envers2/&lt;nom&gt;</c>, formes simples en repli.
    /// </summary>
    public static class EnversLieux
    {
        /// <summary>Dossier des modèles dans le projet Unity (à plat : <c>&lt;nom&gt;.fbx</c>, <c>_couleur.jpg</c>, <c>_normale.png</c>).</summary>
        public const string AssetFolder = "Assets/EdS/Modeles/Resources/Envers2";

        /// <summary>Dossier des modèles sous <c>Resources</c>.</summary>
        public const string Folder = "Envers2/";

        /// <summary>Dossier des créatures (sous <c>Resources</c>, puis dans le projet).</summary>
        public const string CreatureFolder = "Envers2/Creatures/", CreatureAssetFolder = "Assets/EdS/Modeles/Resources/Envers2/Creatures";

        /// <summary>Objets des secrets.</summary>
        public const string ChestModel = "coffre_reflet", SteleModel = "stele_murmures";

        /// <summary>Créatures : le spectre (statique, sa marche bipède a échoué chez Tripo) et le chien de cendre (marche).</summary>
        public const string WraithModel = "spectre_reflet", HoundModel = "chien_de_cendre";

        /// <summary>Les 13 modèles statiques du kit, dans l'ordre des fiches de l'atelier (V01 à V13).</summary>
        public static readonly IReadOnlyList<string> Models = Array.AsReadOnly(new[]
        {
            "moulin_renverse", "clocher_renverse", "cascade_figee", "tonneaux_renverses", "arbre_millenaire_mort", "cairn_suspendu",
            "palissade_calcinee", "wagonnet_suspendu", "rosace_brisee", "barque_noire", "pont_brise_flottant", ChestModel, SteleModel,
        });

        /// <summary>Les deux créatures (R01, R02).</summary>
        public static readonly IReadOnlyList<string> CreatureModels = Array.AsReadOnly(new[] { WraithModel, HoundModel });

        private static readonly System.Text.RegularExpressions.Regex AssetName =
            new System.Text.RegularExpressions.Regex("^(?<name>[a-z_]+)\\.fbx$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        /// <summary>Nom du modèle statique du kit d'un chemin d'asset Unity, ou null.</summary>
        public static string? NameOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string file = path.Replace('\\', '/');
            int slash = file.LastIndexOf('/');
            var m = AssetName.Match(slash >= 0 ? file.Substring(slash + 1) : file);
            return m.Success && Models.Contains(m.Groups["name"].Value) ? m.Groups["name"].Value : null;
        }

        private static readonly (string Prefix, EnversLieuPiece Piece)[] Prefixes =
        {
            ("envers_moulin_renverse", EnversLieuPiece.InvertedMill), ("envers_clocher_renverse", EnversLieuPiece.InvertedBelfry),
            ("envers_cascade_figee", EnversLieuPiece.FrozenFall), ("envers_tonneaux_renverses", EnversLieuPiece.Barrels),
            ("envers_arbre_millenaire_mort", EnversLieuPiece.MillenaryTree), ("envers_cairn_suspendu", EnversLieuPiece.FloatingCairn),
            ("envers_palissade_calcinee", EnversLieuPiece.BurntPalisade), ("envers_wagonnet_suspendu", EnversLieuPiece.HangingCart),
            ("envers_rosace_brisee", EnversLieuPiece.BrokenRose), ("envers_barque_noire", EnversLieuPiece.BlackBoat),
            ("envers_pont_brise", EnversLieuPiece.BrokenBridge),
        };

        /// <summary>Pièce d'un lieu de l'Envers d'un obstacle ; <see cref="EnversLieuPiece.None"/> pour tout autre (dont les pièces de la V2.51).</summary>
        public static EnversLieuPiece PieceFor(string? id)
        {
            string key = id ?? string.Empty;
            foreach ((string prefix, EnversLieuPiece piece) in Prefixes)
                if (key.StartsWith(prefix, StringComparison.Ordinal)) return piece;
            return EnversLieuPiece.None;
        }

        public static string? ModelFor(EnversLieuPiece piece) => piece switch
        {
            EnversLieuPiece.InvertedMill => "moulin_renverse",
            EnversLieuPiece.InvertedBelfry => "clocher_renverse",
            EnversLieuPiece.FrozenFall => "cascade_figee",
            EnversLieuPiece.Barrels => "tonneaux_renverses",
            EnversLieuPiece.MillenaryTree => "arbre_millenaire_mort",
            EnversLieuPiece.FloatingCairn => "cairn_suspendu",
            EnversLieuPiece.BurntPalisade => "palissade_calcinee",
            EnversLieuPiece.HangingCart => "wagonnet_suspendu",
            EnversLieuPiece.BrokenRose => "rosace_brisee",
            EnversLieuPiece.BlackBoat => "barque_noire",
            EnversLieuPiece.BrokenBridge => "pont_brise_flottant",
            _ => null,
        };

        /// <summary>Hauteur (m) de la pièce posée sur l'emprise de son obstacle (celle de sa fiche).</summary>
        public static double HeightOf(EnversLieuPiece piece) => piece switch
        {
            EnversLieuPiece.InvertedMill => 10.0,
            EnversLieuPiece.InvertedBelfry => 12.0,
            EnversLieuPiece.FrozenFall => 12.0,
            EnversLieuPiece.Barrels => 3.0,
            EnversLieuPiece.MillenaryTree => 22.0,
            EnversLieuPiece.FloatingCairn => 5.0,
            EnversLieuPiece.BurntPalisade => 4.0,
            EnversLieuPiece.HangingCart => 3.0,
            EnversLieuPiece.BrokenRose => 7.0,
            EnversLieuPiece.BlackBoat => 2.0,
            EnversLieuPiece.BrokenBridge => 4.0,
            _ => 0.0,
        };

        /// <summary>La palissade calcinée se répète en tuiles le long de son obstacle (bornes réelles, sans trou) ; le reste d'un seul tenant.</summary>
        public static bool Tiled(EnversLieuPiece piece) => piece == EnversLieuPiece.BurntPalisade;

        /// <summary>Les grandes pièces cachent le personnage : elles s'effacent en fondu (occulteurs).</summary>
        public static bool Occludes(EnversLieuPiece piece) => piece == EnversLieuPiece.InvertedMill || piece == EnversLieuPiece.InvertedBelfry
            || piece == EnversLieuPiece.FrozenFall || piece == EnversLieuPiece.MillenaryTree || piece == EnversLieuPiece.BurntPalisade
            || piece == EnversLieuPiece.BrokenRose;

        /// <summary>
        /// Les pièces qui flottent (cairn suspendu, pont brisé) : posées au-dessus du plus haut sol de leur emprise, plus cet écart (m) ; les
        /// autres posées sur le plus bas (leur pied ne flotte jamais au-dessus d'une pente).
        /// </summary>
        public static double Lift(EnversLieuPiece piece) => piece == EnversLieuPiece.FloatingCairn ? 0.6 : piece == EnversLieuPiece.BrokenBridge ? 1.0 : 0.0;

        /// <summary>
        /// Sol (m) sous l'emprise [<paramref name="x0"/>, <paramref name="x1"/>] × [<paramref name="z0"/>, <paramref name="z1"/>] d'une pièce, d'après
        /// la hauteur du relief affiché <paramref name="height"/> (centre et quatre coins) : le plus bas pour une pièce posée, le plus haut plus
        /// <see cref="Lift"/> pour une pièce qui flotte.
        /// </summary>
        public static double Ground(EnversLieuPiece piece, double x0, double z0, double x1, double z1, Func<double, double, double> height)
        {
            if (height is null) throw new ArgumentNullException(nameof(height));
            double cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
            double[] h = { height(cx, cz), height(x0, z0), height(x1, z0), height(x0, z1), height(x1, z1) };
            return Lift(piece) > 0 ? h.Max() + Lift(piece) : h.Min();
        }

        // --- Construction par cases autour de la caméra ------------------------------------------------------------------

        /// <summary>Pièces au plus par case et par image, selon la machine (le PC de Damien est modeste) : les plus grandes d'abord.</summary>
        public static int MaxPiecesPerCell(DeviceTier tier) => tier switch
        {
            DeviceTier.Phone => 4,
            DeviceTier.Modest => 10,
            _ => 32,
        };

        /// <summary>Secrets dessinés quelle que soit la machine (on doit pouvoir les trouver).</summary>
        public static bool SecretsAlwaysShown => true;

        /// <summary>Case (colonne, rangée) de la grille <paramref name="region"/> qui contient le point, bornée à la grille.</summary>
        public static (int Column, int Row) CellOf(RegionGrid region, double x, double z)
        {
            if (region is null) throw new ArgumentNullException(nameof(region));
            int c = (int)Math.Floor((x - region.OriginX) / RegionGrid.CellMeters), r = (int)Math.Floor((z - region.OriginZ) / RegionGrid.CellMeters);
            return (Math.Min(Math.Max(c, 0), region.Columns - 1), Math.Min(Math.Max(r, 0), region.Rows - 1));
        }

        /// <summary>
        /// Les pièces de la case (<paramref name="column"/>, <paramref name="row"/>) parmi <paramref name="pieces"/> (centre de l'emprise et aire),
        /// les plus grandes d'abord puis dans l'ordre donné, au plus <see cref="MaxPiecesPerCell"/> : leurs rangs dans la liste.
        /// </summary>
        public static IReadOnlyList<int> CellPieces(RegionGrid region, int column, int row, IReadOnlyList<(double X, double Z, double Area)> pieces, DeviceTier tier)
        {
            if (pieces is null) throw new ArgumentNullException(nameof(pieces));
            var inCell = new List<int>();
            for (int i = 0; i < pieces.Count; i++)
                if (CellOf(region, pieces[i].X, pieces[i].Z) == (column, row)) inCell.Add(i);
            return inCell.OrderByDescending(i => pieces[i].Area).ThenBy(i => i).Take(MaxPiecesPerCell(tier)).ToList();
        }

        /// <summary>Les secrets de la case (rangs dans la liste), tous, quelle que soit la machine.</summary>
        public static IReadOnlyList<int> CellSecrets(RegionGrid region, int column, int row, IReadOnlyList<MapSecret> secrets)
        {
            if (secrets is null) throw new ArgumentNullException(nameof(secrets));
            var list = new List<int>();
            for (int i = 0; i < secrets.Count; i++)
                if (CellOf(region, secrets[i].X, secrets[i].Z) == (column, row)) list.Add(i);
            return list;
        }

        // --- Secrets ---------------------------------------------------------------------------------------------------

        public static string SecretModel(SecretKind kind) => kind == SecretKind.Chest ? ChestModel : SteleModel;

        /// <summary>Emprise (longueur, largeur, hauteur en m) d'un secret dessiné : le coffre et la stèle de leurs fiches.</summary>
        public static (double Length, double Width, double Height) SecretSize(SecretKind kind) => kind == SecretKind.Chest ? (1.4, 0.9, 1.0) : (1.0, 0.6, 2.2);

        /// <summary>Lacet (degrés) d'un secret, tiré de son identifiant (stable d'une partie à l'autre).</summary>
        public static double SecretYaw(string id) => Math.Floor(EnversLook.Hash01(StableHash(id), 17) * 360.0);

        /// <summary>Lueur violette d'un coffre ou d'une stèle (pour les trouver dans la pénombre).</summary>
        public static readonly (double R, double G, double B) SecretGlow = (0.62, 0.46, 1.0);

        public const double SecretGlowIntensity = 0.8, SecretGlowRange = 4.0;

        // --- Créatures -------------------------------------------------------------------------------------------------

        /// <summary>
        /// Modèle de la créature <paramref name="id"/> : le spectre pour les moines renversés et l'abbé renversé (« moine_reflet3 »,
        /// « moine_reflet_abbe_dechire_v2 »), le chien de cendre pour les chiens (« chien_reflet1 ») ; null pour toute autre.
        /// </summary>
        public static string? CreatureModel(string id)
        {
            string key = StyleRules.Key(id ?? string.Empty);
            if (key.StartsWith("moinereflet", StringComparison.Ordinal)) return WraithModel;
            if (StyleRules.IsAshHound(id ?? string.Empty)) return HoundModel;
            return null;
        }

        /// <summary>Le chien de cendre a sa marche (« Walk ») ; le spectre est statique et flotte.</summary>
        public static bool CreatureWalks(string name) => name == HoundModel;

        /// <summary>Le spectre flotte au-dessus du sol en ondulant (mouvement par le code, sans animation).</summary>
        public static bool Floats(string name) => name == WraithModel;

        /// <summary>Hauteur (m) d'un spectre : celle d'un homme ; l'abbé renversé un peu plus grand.</summary>
        public static double WraithHeight(string id) => StyleRules.Key(id ?? string.Empty).StartsWith("moinerefletabbe", StringComparison.Ordinal) ? 2.2 : 1.85;

        /// <summary>Hauteur moyenne (m) du spectre au-dessus du sol, et amplitude (m) et période (s) de son ondulation.</summary>
        public const double WraithLift = 0.3, WraithBobMeters = 0.12, WraithBobSeconds = 3.2;

        /// <summary>
        /// Hauteur (m) du spectre <paramref name="id"/> au-dessus de sa place à l'instant <paramref name="seconds"/> : entre
        /// <see cref="WraithLift"/> ± <see cref="WraithBobMeters"/>, déphasé par l'identifiant (les moines ne flottent pas ensemble).
        /// </summary>
        public static double WraithBob(string id, double seconds)
        {
            double phase = EnversLook.Hash01(StableHash(id), 29);
            return WraithLift + WraithBobMeters * Math.Sin(2 * Math.PI * (seconds / WraithBobSeconds + phase));
        }

        /// <summary>
        /// Relecture indépendante de la PR #263 : le spectre n'ondule que vivant ; mort ou tombé, il retombe à sa place et ne bouge plus.
        /// </summary>
        public static bool WraithMoves(bool dead) => !dead;

        /// <summary>Hachage stable d'un identifiant (FNV-1a, sans dépendre de string.GetHashCode).</summary>
        public static int StableHash(string? id)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in id ?? string.Empty) h = (h ^ c) * 16777619;
                return (int)(h & 0x7FFFFFFF);
            }
        }
    }
}
