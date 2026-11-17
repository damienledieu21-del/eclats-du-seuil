using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Space;

namespace EdS.Runtime
{
    /// <summary>
    /// V2.30.2 : un modèle de l'atelier du Bourg d'Aube fortifié posé par le jeu : pièce (fin du nom du FBX, « f01_palissade »), place du
    /// pivot (au sol), lacet (degrés, comme <c>Quaternion.Euler(0, yaw, 0)</c> d'Unity : le +z du modèle, sa face extérieure ou sa façade,
    /// va vers (sin, cos)) et obstacle qu'il habille (null : décor posé dans un passage libre, qu'il n'arrête pas).
    /// </summary>
    public readonly struct BourgPiece
    {
        public BourgPiece(string piece, double x, double z, double yaw, string? covers, double height = 0)
        {
            Height = height;
            Piece = piece;
            X = x;
            Z = z;
            Yaw = yaw;
            Covers = covers;
        }

        public string Piece { get; }
        public double X { get; }
        public double Z { get; }
        public double Yaw { get; }
        public string? Covers { get; }

        /// <summary>V2.30.5 : hauteur (m) du pivot au-dessus du sol (pièce murale : lanterne, auvent, linge, bannière, râtelier) ; 0 au sol.</summary>
        public double Height { get; }
    }

    /// <summary>
    /// V2.30.2 : habillage du Bourg : les pièces à poser et, pour chaque obstacle habillé, les pièces sans lesquelles la vue garde la forme
    /// faite par le code (<see cref="Needs"/>).
    /// </summary>
    public sealed class BourgDress
    {
        public BourgDress(IReadOnlyList<BourgPiece> pieces, IReadOnlyDictionary<string, IReadOnlyList<string>> needs)
        {
            Pieces = pieces;
            Needs = needs;
        }

        public IReadOnlyList<BourgPiece> Pieces { get; }

        /// <summary>Obstacle habillé → pièces qu'il lui faut (toutes présentes : la forme faite par le code n'est pas posée).</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Needs { get; }

        /// <summary>
        /// Pièces dont le pied touche le rectangle (<paramref name="minX"/>, <paramref name="minZ"/>) – (<paramref name="maxX"/>,
        /// <paramref name="maxZ"/>) agrandi de <paramref name="margin"/> m, dans leur ordre : celles qu'une fenêtre pose.
        /// </summary>
        public IEnumerable<BourgPiece> Near(double minX, double minZ, double maxX, double maxZ, double margin) =>
            Pieces.Where(p =>
            {
                var f = BourgModels.Box(p.Piece) != null ? BourgModels.Footprint(p.Piece, p.X, p.Z, p.Yaw) : (p.X, p.X, p.Z, p.Z);
                return f.Item2 >= minX - margin && f.Item1 <= maxX + margin && f.Item4 >= minZ - margin && f.Item3 <= maxZ + margin;
            });

        public static readonly BourgDress Empty = new BourgDress(Array.Empty<BourgPiece>(), new Dictionary<string, IReadOnlyList<string>>());
    }

    /// <summary>
    /// V2.30.2 : le Bourg d'Aube fortifié (V2.30) habillé des modèles de l'atelier (<c>Eclats_du_Seuil_AssetLab/Bourg_Fortifie</c>, exports
    /// de jeu : LOD1 sur atlas, échelle 1, pivot au sol), rangés dans « Assets/EdS/Modeles/Decor/bourg_*/bourg_&lt;pièce&gt;.fbx ».
    /// Enceinte : palissade en modules de 6 m le long de chaque pan (<c>palissade_ville_*</c>), tours de flanquement sur les tours
    /// (<c>tour_ville_*</c>), portes dans les ouvertures entre deux pans, tours de guet sur la ronde. Affichage seulement : données de zone
    /// et moteur inchangés. Sans Unity.
    /// </summary>
    public static class BourgModels
    {
        // --- Gabarits des exports de jeu ------------------------------------------------------------------------------------------

        /// <summary>
        /// Boîte d'une pièce dans le repère de Blender du FBX de jeu (export_jeu.json, « boite ») : X (droite vue de dehors), Y (vers
        /// l'intérieur ou l'arrière), hauteur. Unity lit x = −X, z = −Y.
        /// </summary>
        public static (double X0, double X1, double Y0, double Y1, double Top)? Box(string piece)
        {
            switch (piece)
            {
                case Wall: return (-3.0, 3.0, -0.16, 2.14, 4.8);
                case WallStairs: return (-3.0, 3.0, -0.16, 6.24, 4.79);
                case WallBroken: return (-3.0, 3.0, -0.16, 2.14, 4.79);
                case WallMended: return (-3.0, 3.0, -0.16, 2.14, 4.8);
                case Walkway: return (-3.0, 3.0, -1.2, 1.1, 5.92);
                case WatchTower: return (-2.5, 2.5, -2.5, 2.5, 9.403);
                case FlankTower: return (-3.5, 3.5, -3.5, 3.5, 12.7);
                case EastGate: return (-6.0, 6.0, -1.15, 5.9, 10.5);
                case WestGate: return (-8.65, 8.9, -1.3, 10.0, 13.8);
                case NorthGate: return (-4.6, 4.6, -1.5, 4.9, 10.11);
                // V2.30.3 : monuments, étals, maisons (exports de jeu des groupes Eglise, Edicules, Halle, Ecuries, Boutiques, Etals,
                // Maisons_*).
                case Church: return (-8.9, 8.69, -17.1, 15.69, 28.0);
                case Parvis: return (-11.0, 11.0, -6.0, 6.0, 0.3);
                case Cemetery: return (-14.03, 14.03, -11.05, 11.03, 1.8);
                case Hall: return (-12.0, 12.0, -7.0, 7.0, 10.1);
                case Provost: return (-8.0, 8.0, -5.97, 5.97, 20.0);
                case Guardhouse: return (-9.0, 9.0, -5.97, 5.97, 9.6);
                case Stables: return (-15.0, 15.0, -12.0, 12.0, 10.8);
                case Granary: return (-4.97, 4.97, -7.4, 8.6, 11.42);
                case Washhouse: return (-6.0, 6.0, -3.5, 3.5, 5.1);
                case Oven: return (-4.48, 4.48, -3.52, 4.4, 6.7);
                case Fountain: return (-2.25, 2.25, -2.25, 2.25, 3.2);
                case Well: return (-1.7, 1.7, -1.7, 1.7, 4.2);
                case "h01_artisan_etroite": return (-2.25, 2.25, -4.8, 4.3, 10.72);
                case "h02_artisan_moyenne": return (-3.0, 3.0, -4.55, 4.3, 10.72);
                case "h03_artisan_large": return (-3.75, 3.75, -4.55, 4.3, 10.72);
                case "h04_marchand_etroite": return (-2.5, 2.5, -5.8, 5.3, 11.9);
                case "h05_marchand_moyenne": return (-3.5, 3.5, -5.8, 5.3, 14.51);
                case "h06_marchand_large": return (-4.5, 4.5, -5.8, 5.3, 14.95);
                case "h07_notable_etroite": return (-3.0, 3.0, -6.8, 6.3, 15.3);
                case "h08_notable_moyenne": return (-4.25, 4.25, -6.8, 6.3, 15.3);
                case "h09_notable_large": return (-5.5, 5.5, -6.8, 6.3, 15.3);
                case "h10_angle": return (-4.55, 4.0, -4.55, 4.0, 10.57);
                case "h10_angle_miroir": return (-4.0, 4.55, -4.55, 4.0, 10.57);
                case "h11_passage": return (-4.0, 4.0, -5.3, 5.3, 13.6);
                case "h12_pauvre_enceinte": return (-2.8, 2.8, -2.8, 2.8, 4.66);
                case "p13_armurier": return (-3.98, 3.98, -5.3, 5.0, 11.8);
                case "p14_forgeron": return (-6.0, 6.0, -5.3, 4.97, 11.5);
                case "p15_tanneur": return (-4.97, 4.97, -6.3, 6.0, 12.1);
                case "p16_apothicaire": return (-3.48, 3.48, -4.8, 4.5, 11.1);
                case "p17_tailleur": return (-3.98, 3.98, -4.8, 4.5, 11.1);
                case "p18_taverne": return (-6.97, 6.97, -6.3, 6.0, 12.6);
                // V2.30.4 : rues, accessoires de rue et de garde, cours (groupes Voirie, Charrois_Mobilier, Accessoires_*, Cours).
                // V2.30.5 : fossé, talus, ponts ; pièces murales.
                case Ditch: return (-3.0, 3.0, -3.5, 3.5, 0.0);
                case Bank: return (-3.0, 3.0, -2.0, 2.0, 1.21);
                case EastBridge: return (-2.6, 2.6, -6.0, 6.0, 1.2);
                case WestBridge: return (-3.6, 3.6, -6.0, 6.0, 1.2);
                case NorthBridge: return (-2.0, 2.0, -6.0, 6.0, 1.2);
                case WallLantern: return (-0.55, 0.56, -0.25, 0.25, 1.2);
                case "r29_corde_linge": return (-4.0, 4.0, -0.08, 0.08, 1.86);
                case "r30_auvent_lin": case "r30_auvent_ocre": case "r30_auvent_rayee": case "r30_auvent_rayee_perches": return (-2.0, 2.0, -1.1, 1.1, 1.1);
                case "r30_auvent_lin_replie": return (-1.93, 1.98, -0.15, 0.15, 1.1);
                case "f18_ratelier_mural": return (-1.0, 1.0, -0.01, 0.33, 1.8);
                case "f19_banniere_potence": return (-0.6, 0.6, -0.05, 0.05, 2.4);
                case Paving: return (-3.0, 3.0, -3.0, 3.0, 0.18);
                case Curb: return (-3.0, 3.0, -0.12, 0.12, 0.18);
                case Street: return (-3.0, 3.0, -4.0, 4.0, 0.2);
                case Lane: return (-3.0, 3.0, -1.75, 1.75, 0.16);
                case "r19_charrette_caisses": case "r19_charrette_paille": case "r19_charrette_vide": return (-0.76, 0.76, -1.6, 1.6, 1.7);
                case "r20_chariot_bache": case "r20_chariot_plateau_caisses": case "r20_chariot_plateau_tonneaux": case "r20_chariot_plateau_vide":
                    return (-1.36, 1.36, -3.5, 3.5, 3.5);
                case "r22_banc_dossier": return (-1.1, 1.1, -0.21, 0.33, 0.9);
                case "r22_banc_sans_dossier": return (-1.1, 1.1, -0.21, 0.2, 0.45);
                case Trough: return (-1.5, 1.5, -0.55, 0.55, 0.8);
                case Pillory: return (-1.2, 1.2, -0.7, 0.7, 2.2);
                case "r25_panneau_roue_epi": case "r25_panneau_epi_cloche": case "r25_panneau_cloche_roue": return (-0.8, 0.8, -0.2, 0.2, 2.8);
                case "r26_tonneau_petit": return (-0.29, 0.29, -0.29, 0.29, 0.8);
                case "r26_tonneau_moyen": return (-0.44, 0.44, -0.44, 0.44, 1.2);
                case "r26_tonneau_grand": return (-0.54, 0.54, -0.54, 0.54, 1.4);
                case "r27_caisse_petite": return (-0.3, 0.3, -0.2, 0.2, 0.4);
                case "r27_caisse_moyenne": return (-0.6, 0.6, -0.4, 0.4, 0.8);
                case "r27_caisse_longue": return (-0.8, 0.8, -0.4, 0.4, 0.8);
                case "r28_ballot_tissu": case "r28_ballot_laine": case "r28_ballot_paille": return (-0.7, 0.7, -0.46, 0.46, 0.9);
                case TollBar: return (-3.5, 3.5, -0.4, 0.4, 1.33);
                case Brazier: return (-0.4, 0.4, -0.4, 0.4, 1.25);
                case Rack: return (-1.01, 1.01, -0.34, 0.34, 1.8);
                case Banner: return (-0.6, 0.6, -0.2, 0.2, 5.0);
                case "h21_appentis_ouvert": case "h21_appentis_ferme": return (-2.0, 2.0, -1.3, 1.3, 2.8);
                case "h22_puits": return (-0.71, 0.75, -0.62, 0.62, 2.6);
                case "h23_latrines": return (-0.75, 0.75, -0.9, 0.9, 2.6);
                case "h24_tas_bois_plein": case "h24_tas_bois_moitie": case "h24_tas_bois_presque_vide": return (-1.26, 1.26, -0.45, 0.45, 1.5);
                case "h25_potager_legumes": case "h25_potager_aromatiques": case "h25_potager_hiver": return (-1.98, 1.98, -1.5, 1.5, 0.73);
                default:
                    if (Array.IndexOf(Stalls, piece) >= 0) return (-1.5, 1.5, -1.2, 1.2, 2.8);
                    return null;
            }
        }

        public const string Wall = "f01_palissade";
        public const string WallStairs = "f03_palissade_escalier";
        public const string WallBroken = "f04_palissade_abimee";
        public const string WallMended = "f04_palissade_reparee";
        public const string Walkway = "f05_ronde_couverte";
        public const string WatchTower = "f06_tour_guet";
        public const string FlankTower = "f07_tour_flanquement";
        public const string EastGate = "f08_porte_est";
        public const string WestGate = "f09_porte_ouest";
        public const string NorthGate = "f10_porte_nord";

        public const string Church = "p01_eglise";
        public const string Parvis = "p02_parvis";
        public const string Cemetery = "p03_cimetiere";
        public const string Hall = "p04_halle";
        public const string Provost = "p05_prevot";
        public const string Guardhouse = "p06_corps_de_garde";
        public const string Stables = "p07_ecuries";
        public const string Granary = "p08_grenier";
        public const string Washhouse = "p09_lavoir";
        public const string Oven = "p10_four_banal";
        public const string Fountain = "p11_fontaine";
        public const string Well = "p12_puits";

        public const string Ditch = "f11_fosse";
        public const string Bank = "f12_talus";
        public const string EastBridge = "f13_pont_est";
        public const string WestBridge = "f14_pont_ouest";
        public const string NorthBridge = "f15_pont_nord";
        public const string WallLantern = "r21_lanterne_potence";
        public const string Line = "r29_corde_linge";
        public const string WallRack = "f18_ratelier_mural";
        public const string WallBanner = "f19_banniere_potence";
        public static readonly string[] Awnings = { "r30_auvent_lin", "r30_auvent_ocre", "r30_auvent_rayee", "r30_auvent_rayee_perches", "r30_auvent_lin_replie" };

        /// <summary>V2.30.5 : pièce posée au niveau du bord du fossé (pas au fond creusé) : modules du fossé et ponts.</summary>
        public static bool OnRim(string piece) => piece == Ditch || piece == EastBridge || piece == WestBridge || piece == NorthBridge;

        /// <summary>V2.30.5 : pièce du fossé (modules, ponts, talus) ou d'une façade (lanterne, linge, auvent, râtelier mural, bannière sur potence) ?</summary>
        public static bool IsFacadeOrMoat(string piece) => OnRim(piece) || piece == Bank || piece == WallLantern || piece == Line || piece == WallRack
            || piece == WallBanner || Array.IndexOf(Awnings, piece) >= 0;

        /// <summary>V2.30.5 : obstacle du fossé (bas : on voit par-dessus, on ne le franchit que par les ponts).</summary>
        public static bool IsDitch(string? id) => id != null && id.StartsWith("fosse_ville_", StringComparison.Ordinal);

        /// <summary>V2.30.5 : profondeur du fossé creusé dans le relief (fiche F11 : fond à −2,00) et demi-largeur de son fond plat.</summary>
        public const double DitchDepth = 2.0, DitchFloor = 2.0, DitchBank = 1.5;

        /// <summary>V2.30.5 : bâtiments ajoutés le long de la route d'Ambre (décision de Damien du 16 novembre 2026) et leur modèle.</summary>
        public static string? ShopModel(string? id)
        {
            switch (id)
            {
                case "maison_ville_armurier": return "p13_armurier";
                case "maison_ville_forgeron": return "p14_forgeron";
                case "maison_ville_tanneur": return "p15_tanneur";
                case "maison_ville_apothicaire": return "p16_apothicaire";
                case "maison_ville_tailleur": return "p17_tailleur";
                case "maison_ville_taverne": return "p18_taverne";
                case "maison_ville_notable": return "h09_notable_large";
                default: return null;
            }
        }

        public const string Paving = "r01_paves_place";
        public const string Curb = "r01_bordure";
        public const string Street = "r02_rue_pavee";
        public const string Lane = "r03_ruelle_terre";
        public const string Trough = "r23_abreuvoir";
        public const string Pillory = "r24_pilori";
        public const string TollBar = "f16_barriere_peage";
        public const string Brazier = "f17_brasero";
        public const string Rack = "f18_ratelier_pieds";
        public const string Banner = "f19_banniere_mat";

        /// <summary>Sols posés à plat (pavés, rue, ruelle, bordure) : ils peuvent couvrir une route et passer sous un étal ou le puits.</summary>
        public static bool IsGround(string piece) => piece == Paving || piece == Curb || piece == Street || piece == Lane;

        /// <summary>V2.30.4 : pièce du décor des rues (<see cref="Streets"/>) ?</summary>
        public static bool IsStreetDecor(string piece) => IsGround(piece) || piece == Trough || piece == Pillory || piece == TollBar || piece == Brazier
            || piece == Rack || piece == Banner || piece.StartsWith("r22_", StringComparison.Ordinal) || Array.IndexOf(Carts, piece) >= 0
            || Array.IndexOf(Wagons, piece) >= 0 || Array.IndexOf(Signs, piece) >= 0 || Array.IndexOf(Goods, piece) >= 0 || Array.IndexOf(Yards, piece) >= 0;

        public static readonly string[] Carts = { "r19_charrette_caisses", "r19_charrette_paille", "r19_charrette_vide" };
        public static readonly string[] Wagons = { "r20_chariot_bache", "r20_chariot_plateau_caisses", "r20_chariot_plateau_tonneaux", "r20_chariot_plateau_vide" };
        public static readonly string[] Signs = { "r25_panneau_roue_epi", "r25_panneau_epi_cloche", "r25_panneau_cloche_roue" };

        /// <summary>Marchandises posées derrière les étals (tonneaux, caisses, ballots).</summary>
        public static readonly string[] Goods =
        {
            "r26_tonneau_petit", "r26_tonneau_moyen", "r26_tonneau_grand", "r27_caisse_petite", "r27_caisse_moyenne", "r27_caisse_longue",
            "r28_ballot_tissu", "r28_ballot_laine", "r28_ballot_paille",
        };

        /// <summary>Petits éléments de cour (H21 à H25), un derrière une maison sur trois.</summary>
        public static readonly string[] Yards =
        {
            "h21_appentis_ouvert", "h21_appentis_ferme", "h22_puits", "h23_latrines", "h24_tas_bois_plein", "h24_tas_bois_moitie",
            "h24_tas_bois_presque_vide", "h25_potager_legumes", "h25_potager_aromatiques", "h25_potager_hiver",
        };

        /// <summary>Les huit étals (R11 à R18) : les six étals de la place dans l'ordre de leur numéro, puis deux de décor.</summary>
        public static readonly string[] Stalls =
        {
            "r11_etal_legumes", "r12_etal_fruits", "r13_etal_pain", "r14_etal_poisson", "r15_etal_viande", "r16_etal_fromages",
            "r17_etal_etoffes", "r18_etal_poteries",
        };

        /// <summary>Maisons types de l'atelier (H01 à H12, avec la maison d'angle en miroir).</summary>
        public static readonly string[] Houses =
        {
            "h01_artisan_etroite", "h02_artisan_moyenne", "h03_artisan_large", "h04_marchand_etroite", "h05_marchand_moyenne",
            "h06_marchand_large", "h07_notable_etroite", "h08_notable_moyenne", "h09_notable_large", "h10_angle", "h10_angle_miroir",
            "h11_passage", "h12_pauvre_enceinte",
        };

        /// <summary>Longueur d'un module de palissade (A → B, m).</summary>
        public const double ModuleLength = 6.0;

        /// <summary>Largeur libre du passage d'une porte (fiches F08 à F10), centré sur son pivot.</summary>
        public static double Passage(string piece) => piece == EastGate ? 4.2 : piece == WestGate ? 6.0 : piece == NorthGate ? 3.0 : 0.0;

        /// <summary>
        /// Enfoncement (m) d'un module de palissade ou d'une tour dans ce qui le borde : la palissade s'arrête 0,30 m dans le mur d'une tour
        /// ou d'une porte (pas de jour entre eux).
        /// </summary>
        public const double Tuck = 0.3;

        /// <summary>
        /// Décalage (m, vers l'intérieur) de la ronde couverte F05 par rapport au pivot du module F01 : origines de l'atelier Y 1,20 (F05)
        /// et Y 0,16 (F01).
        /// </summary>
        public const double WalkwayInset = 1.2 - 0.16;

        /// <summary>Recul (m, vers l'intérieur) du centre d'une tour de guet F06 par rapport à la ligne de la ronde (accroche A_ronde Y −1,50).</summary>
        public const double WatchTowerInset = 1.5;

        /// <summary>
        /// Pièce assez haute pour cacher le joueur (tours, portes, bâtiments, au moins 6 m) : la vue ne fait d'occulteurs que pour elles
        /// (relecture indépendante : les centaines de modules de palissade et de petits décors n'en ont pas, pour le téléphone).
        /// </summary>
        public static bool Occludes(string piece)
        {
            var b = Box(piece);
            return b != null && b.Value.Top >= 6.0;
        }

        /// <summary>Rayon (m) du pied d'une pièce : la vue la pose au plus bas du sol affiché sous ce pied (V2.24).</summary>
        public static double Foot(string piece)
        {
            var b = Box(piece);
            if (b == null) return 0.5;
            var v = b.Value;
            return Math.Min(4.0, Math.Max(Math.Max(-v.X0, v.X1), Math.Max(-v.Y0, v.Y1)));
        }

        /// <summary>
        /// Rectangle (x0, x1, z0, z1) couvert au sol par la pièce posée en (<paramref name="x"/>, <paramref name="z"/>) au lacet
        /// <paramref name="yaw"/> (multiple de 90°).
        /// </summary>
        public static (double X0, double X1, double Z0, double Z1) Footprint(string piece, double x, double z, double yaw)
        {
            var b = Box(piece) ?? throw new ArgumentException("pièce inconnue : " + piece, nameof(piece));
            var corners = new[] { (b.X0, b.Y0), (b.X1, b.Y0), (b.X0, b.Y1), (b.X1, b.Y1) }.Select(c => Place(x, z, yaw, -c.Item1, -c.Item2)).ToList();
            return (corners.Min(c => c.X), corners.Max(c => c.X), corners.Min(c => c.Z), corners.Max(c => c.Z));
        }

        /// <summary>Point (<paramref name="lx"/>, <paramref name="lz"/>) du repère du modèle d'Unity posé en (x, z) au lacet <paramref name="yaw"/>.</summary>
        public static (double X, double Z) Place(double x, double z, double yaw, double lx, double lz)
        {
            double a = yaw * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a);
            return (Round(x + lx * cos + lz * sin), Round(z - lx * sin + lz * cos));
        }

        private static double Round(double v) => Math.Round(v, 6);

        // --- Pièces mobiles ouvertes ------------------------------------------------------------------------------------------------

        /// <summary>
        /// Pièce mobile d'une porte posée ouverte : objet du FBX (« Vantail_G »), charnière dans le repère du modèle d'Unity (x = −X,
        /// z = −Y des pivots de l'export de jeu), rotation autour de la verticale (degrés) et levée (m, la herse).
        /// </summary>
        public readonly struct OpenPart
        {
            public OpenPart(string name, double x, double z, double turn, double lift)
            {
                Name = name;
                X = x;
                Z = z;
                Turn = turn;
                Lift = lift;
            }

            public string Name { get; }
            public double X { get; }
            public double Z { get; }
            public double Turn { get; }
            public double Lift { get; }
        }

        /// <summary>Ouverture des vantaux (fiches F08 à F10 : 0 à 100°, vers la ville).</summary>
        public const double LeafOpen = 100;

        /// <summary>Course de la herse de la porte de l'Ouest (fiche F09 : 0 à 5,70 m).</summary>
        public const double PortcullisLift = 5.7;

        /// <summary>
        /// Pièces mobiles d'une porte, posées ouvertes (le passage des données est libre : les vantaux sont rabattus vers la ville, la herse
        /// levée) ; vide pour les autres pièces. Le vantail gauche (vu de dehors : X &lt; 0 dans Blender, x &gt; 0 dans Unity) tourne de −100°,
        /// le droit de +100° : leur bout libre part vers −z, l'intérieur.
        /// </summary>
        public static IReadOnlyList<OpenPart> OpenParts(string piece)
        {
            double hinge, depth;
            switch (piece)
            {
                case EastGate: hinge = 2.1; depth = 0.06; break;
                case WestGate: hinge = 3.0; depth = 0.31; break;
                case NorthGate: hinge = 1.5; depth = 0.06; break;
                default: return Array.Empty<OpenPart>();
            }
            var parts = new List<OpenPart>
            {
                new OpenPart("Vantail_G", hinge, -depth, -LeafOpen, 0),
                new OpenPart("Vantail_D", -hinge, -depth, LeafOpen, 0),
            };
            if (piece == WestGate) parts.Add(new OpenPart("Herse", 0, 0, 0, PortcullisLift));
            return parts;
        }

        // --- Habillage ---------------------------------------------------------------------------------------------------------------

        public static bool IsWall(string? id) => id != null && id.StartsWith("palissade_ville_", StringComparison.Ordinal);

        /// <summary>Tour de l'enceinte (le clocher de l'église n'en est pas une).</summary>
        public static bool IsTower(string? id) => id != null && id.StartsWith("tour_ville_", StringComparison.Ordinal) && id != "tour_ville_clocher";

        /// <summary>
        /// Habillage du Bourg pour les obstacles de l'aire entière (<see cref="AdventureMap.Bourg"/> ; sur une fenêtre, le centre et les portes
        /// de l'enceinte seraient faux) et les routes de la carte ; vide sans palissade du Bourg.
        /// </summary>
        public static BourgDress Dress(IReadOnlyList<SpaceObstacle> obstacles, IReadOnlyList<StyleRoad>? roads = null)
        {
            var pieces = new List<BourgPiece>();
            var needs = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            roads ??= Array.Empty<StyleRoad>();
            Enceinte(obstacles, pieces, needs);
            Monuments(obstacles, roads, pieces, needs);
            HousesOf(obstacles, roads, pieces, needs);
            Shops(obstacles, roads, pieces, needs);
            Moat(obstacles, pieces, needs);
            Streets(obstacles, roads, pieces);
            Facades(obstacles, pieces);
            if (pieces.Count == 0) return BourgDress.Empty;
            return new BourgDress(pieces, needs.ToDictionary(n => n.Key, n => (IReadOnlyList<string>)n.Value.Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal));
        }

        /// <summary>Un pan : axe (vrai : le long de x), coordonnée de sa ligne, étendue, face extérieure, identifiant.</summary>
        private sealed class Side
        {
            public bool AlongX;
            public double Line, From, To;
            public (int X, int Z) Out;
            public string Id = string.Empty;
        }

        private static (double X0, double X1, double Z0, double Z1) Bounds(SpaceObstacle o) =>
            (o.Points.Min(p => p.X.Raw) / 10000.0, o.Points.Max(p => p.X.Raw) / 10000.0, o.Points.Min(p => p.Z.Raw) / 10000.0, o.Points.Max(p => p.Z.Raw) / 10000.0);

        /// <summary>Lacet d'une face tournée vers <paramref name="facing"/> (quart de tour).</summary>
        private static double Yaw((int X, int Z) facing) => TownModels.FacingYaw(facing);

        private static void Need(Dictionary<string, List<string>> needs, string id, string piece)
        {
            if (!needs.TryGetValue(id, out List<string>? list)) needs[id] = list = new List<string>();
            list.Add(piece);
        }

        private static void Enceinte(IReadOnlyList<SpaceObstacle> obstacles, List<BourgPiece> pieces, Dictionary<string, List<string>> needs)
        {
            var walls = obstacles.Where(o => IsWall(o.Id)).OrderBy(o => o.Id, StringComparer.Ordinal).ToList();
            if (walls.Count == 0) return;
            // Centre de l'enceinte : celui des pans présents (la ville entière ou la fenêtre qui la voit).
            var all = walls.Select(Bounds).ToList();
            double cx = (all.Min(b => b.X0) + all.Max(b => b.X1)) / 2, cz = (all.Min(b => b.Z0) + all.Max(b => b.Z1)) / 2;
            var sides = new List<Side>();
            foreach (SpaceObstacle w in walls)
            {
                var b = Bounds(w);
                bool alongX = b.X1 - b.X0 >= b.Z1 - b.Z0;
                double line = alongX ? (b.Z0 + b.Z1) / 2 : (b.X0 + b.X1) / 2;
                double center = alongX ? cz : cx;
                int sign = line >= center ? 1 : -1;
                sides.Add(new Side
                {
                    AlongX = alongX, Line = line, From = alongX ? b.X0 : b.Z0, To = alongX ? b.X1 : b.Z1,
                    Out = alongX ? (0, sign) : (sign, 0), Id = w.Id,
                });
            }
            // Intervalles couverts le long de chaque ligne (tours, portes) : la palissade s'y arrête.
            var covered = new List<(Side Line, double A, double B)>();
            // Portes : l'ouverture entre deux pans de la même ligne (moins de 40 m).
            var gates = new List<(Side Line, double At, string Piece)>();
            foreach (var group in sides.GroupBy(s => (s.AlongX, Math.Round(s.Line, 1))))
            {
                var run = group.OrderBy(s => s.From).ToList();
                for (int i = 0; i + 1 < run.Count; i++)
                {
                    double gap = run[i + 1].From - run[i].To;
                    if (gap <= 0.5 || gap > 40) continue;
                    Side s = run[i];
                    string piece = s.Out == (1, 0) ? EastGate : s.Out == (-1, 0) ? WestGate : NorthGate;
                    double at = (run[i].To + run[i + 1].From) / 2;
                    gates.Add((s, at, piece));
                    (double x, double z) = s.AlongX ? (at, s.Line) : (s.Line, at);
                    double yaw = Yaw(s.Out);
                    pieces.Add(new BourgPiece(piece, x, z, yaw, null));
                    var f = Footprint(piece, x, z, yaw);
                    covered.Add((s, (s.AlongX ? f.X0 : f.Z0) + Tuck, (s.AlongX ? f.X1 : f.Z1) - Tuck));
                }
            }
            // Tours : sur la ligne d'un pan (ou au coin de deux) ; celles qu'une porte à tours (F09) recouvre restent sous elle.
            foreach (SpaceObstacle t in obstacles.Where(o => IsTower(o.Id)).OrderBy(o => o.Id, StringComparer.Ordinal))
            {
                var b = Bounds(t);
                double tx = (b.X0 + b.X1) / 2, tz = (b.Z0 + b.Z1) / 2, half = Math.Max(b.X1 - b.X0, b.Z1 - b.Z0) / 2;
                var on = sides.Where(s => Math.Abs((s.AlongX ? tz : tx) - s.Line) < 1.0 && (s.AlongX ? tx : tz) >= s.From - half - 1 && (s.AlongX ? tx : tz) <= s.To + half + 1).ToList();
                if (on.Count == 0) continue;
                var gate = gates.FirstOrDefault(g => g.Piece == WestGate && on.Any(s => s.AlongX == g.Line.AlongX && Math.Abs(s.Line - g.Line.Line) < 0.5)
                    && Math.Abs((g.Line.AlongX ? tx : tz) - g.At) < 15);
                if (gate.Piece != null)
                {
                    Need(needs, t.Id, gate.Piece);
                    continue;
                }
                // Coin : face vers le nord ou le sud (le pan le long de x) ; sinon la face extérieure de son pan.
                Side side = on.FirstOrDefault(s => s.AlongX) ?? on[0];
                string piece = half >= 3.4 ? FlankTower : WatchTower;
                pieces.Add(new BourgPiece(piece, tx, tz, Yaw(side.Out), t.Id));
                Need(needs, t.Id, piece);
                double reach = Box(piece)!.Value.X1 - Tuck;
                foreach (Side s in on) covered.Add((s, (s.AlongX ? tx : tz) - reach, (s.AlongX ? tx : tz) + reach));
            }
            // Palissade : chaque ligne d'un bout à l'autre (pans et ouvertures), moins ce que couvrent tours et portes, en modules de 6 m
            // au plus serrés (aucun jour) ; un module dans une ouverture (à côté d'une porte plus étroite qu'elle) n'habille aucun obstacle.
            foreach (var group in sides.GroupBy(s => (s.AlongX, Math.Round(s.Line, 1))).OrderBy(g => g.Key.AlongX).ThenBy(g => g.Key.Item2))
            {
                var run = group.OrderBy(s => s.From).ToList();
                Side first = run[0];
                double from = run.Min(s => s.From), to = run.Max(s => s.To);
                var cuts = covered.Where(c => c.Line.AlongX == first.AlongX && Math.Abs(c.Line.Line - first.Line) < 0.5).OrderBy(c => c.A).ToList();
                var free = new List<(double A, double B)>();
                double at = from;
                foreach (var c in cuts)
                {
                    if (c.A > at) free.Add((at, Math.Min(c.A, to)));
                    at = Math.Max(at, c.B);
                }
                if (at < to) free.Add((at, to));
                double yaw = Yaw(first.Out);
                int index = 0;
                foreach ((double a, double b) in free.Where(f => f.B - f.A > 0.5))
                {
                    int n = (int)Math.Ceiling((b - a) / ModuleLength - 1e-9);
                    double pitch = (b - a) / n;
                    for (int k = 0; k < n; k++)
                    {
                        // Les modules d'extrémité touchent le bord libre : centre à 3 m du bout, les autres au pas.
                        double c = n == 1 ? (a + b) / 2 : a + ModuleLength / 2 + k * (b - a - ModuleLength) / (n - 1);
                        (double x, double z) = first.AlongX ? (c, first.Line) : (first.Line, c);
                        Side? owner = run.FirstOrDefault(s => c >= s.From && c <= s.To);
                        string piece = WallPiece(first.Out, index++);
                        pieces.Add(new BourgPiece(piece, x, z, yaw, owner?.Id));
                        if (owner != null) Need(needs, owner.Id, piece);
                        // Ronde couverte (garde-corps et auvent) au-dessus des modules qui bordent une porte.
                        if (gates.Any(g => g.Line.AlongX == first.AlongX && Math.Abs(g.Line.Line - first.Line) < 0.5 && Math.Abs(g.At - c) < WalkwayReach))
                        {
                            (double wx, double wz) = (x - first.Out.X * WalkwayInset, z - first.Out.Z * WalkwayInset);
                            pieces.Add(new BourgPiece(Walkway, wx, wz, yaw, null));
                        }
                    }
                }
            }
            // Tours de guet sur la ronde (plan illustré : quatre), au milieu des plus longs pans de chaque côté sans porte ni tour proche.
            foreach (var group in sides.GroupBy(s => s.Out).OrderBy(g => g.Key.X).ThenBy(g => g.Key.Z))
            {
                Side longest = group.OrderByDescending(s => s.To - s.From).ThenBy(s => s.Id, StringComparer.Ordinal).First();
                if (longest.To - longest.From < 150) continue;
                double mid = (longest.From + longest.To) / 2;
                (double x, double z) = longest.AlongX ? (mid, longest.Line) : (longest.Line, mid);
                pieces.Add(new BourgPiece(WatchTower, Round(x - longest.Out.X * WatchTowerInset), Round(z - longest.Out.Z * WatchTowerInset), Yaw(longest.Out), null));
            }
        }

        // --- V2.30.3 : monuments, étals, maisons --------------------------------------------------------------------------------

        /// <summary>
        /// Une pièce posée dans le repère d'un obstacle (<paramref name="o"/>, sa boîte) dont la façade regarde <paramref name="facing"/> :
        /// <paramref name="right"/> le long de la façade (vers la droite vue de dehors), <paramref name="ahead"/> vers l'avant, depuis le
        /// centre de l'obstacle.
        /// </summary>
        private static BourgPiece At(string piece, (double X0, double X1, double Z0, double Z1) o, (int X, int Z) facing, double right, double ahead,
            string? covers)
        {
            double cx = (o.X0 + o.X1) / 2, cz = (o.Z0 + o.Z1) / 2;
            // Droite vue de dehors (en regardant le bâtiment, donc vers −facing) : (−fz, fx).
            double x = cx + facing.X * ahead - facing.Z * right, z = cz + facing.Z * ahead + facing.X * right;
            return new BourgPiece(piece, Round(x), Round(z), Yaw(facing), covers);
        }

        /// <summary>Façade d'un bâtiment de la ville vers la route la plus proche (sans route : vers le sud).</summary>
        private static (int X, int Z) RoadSide(string id, (double X0, double X1, double Z0, double Z1) b, IReadOnlyList<StyleRoad> roads) =>
            roads.Count == 0 ? (0, -1) : TownRules.FacingOf(id, b.X0, b.Z0, b.X1, b.Z1, roads, null);

        /// <summary>
        /// Monuments du cœur de la ville (V2.30) : l'église face au sud (vers la route d'Ambre et la place), son parvis devant, le
        /// cimetière clos à l'est ; le clocher des données (derrière le chevet) est couvert par l'église, qui porte le sien ; la halle face
        /// au nord (vers la place) ; les écuries face au sud (vers la route), leur façade sur le bord de l'obstacle ; le puits de la place ;
        /// la grande maison en cour des bâtiments publics (maison du prévôt et beffroi, corps de garde, four banal devant, grenier au fond) ;
        /// la fontaine et le lavoir au sud de la place ; les six étals de la place tournés vers la route, deux de plus à côté des étals du
        /// sud.
        /// </summary>
        private static void Monuments(IReadOnlyList<SpaceObstacle> obstacles, IReadOnlyList<StyleRoad> roads, List<BourgPiece> pieces,
            Dictionary<string, List<string>> needs)
        {
            void Put(BourgPiece p)
            {
                pieces.Add(p);
                if (p.Covers != null) Need(needs, p.Covers, p.Piece);
            }
            var byId = obstacles.ToDictionary(o => o.Id, o => o, StringComparer.Ordinal);
            if (byId.TryGetValue("chapelle_ville_eglise", out SpaceObstacle? church))
            {
                var b = Bounds(church);
                Put(At(Church, b, (0, -1), 0, 0, church.Id));
                // Parvis : contre la façade (seuil Y −17 de l'église : 2 m devant l'obstacle), 12 m de profondeur.
                Put(At(Parvis, b, (0, -1), 0, (b.Z1 - b.Z0) / 2 + 2 + 6, null));
                // Cimetière clos : à l'est de la nef, portillon vers l'église (face à l'ouest).
                Put(new BourgPiece(Cemetery, Round(b.X1 + 3 + 11.05), Round((b.Z0 + b.Z1) / 2), Yaw((-1, 0)), null));
                if (byId.ContainsKey("tour_ville_clocher")) Need(needs, "tour_ville_clocher", Church);
            }
            if (byId.TryGetValue("grange_ville_halle", out SpaceObstacle? hall)) Put(At(Hall, Bounds(hall), (0, 1), 0, 0, hall.Id));
            if (byId.TryGetValue("grange_ville_ecurie", out SpaceObstacle? stables))
            {
                var b = Bounds(stables);
                // Façade (Y −12) sur le bord sud de l'obstacle : le corps arrière dépasse au nord (U de 24 m sur 16).
                Put(At(Stables, b, (0, -1), 0, (b.Z1 - b.Z0) / 2 - 12, stables.Id));
            }
            if (byId.TryGetValue("puits_ville_marche", out SpaceObstacle? well)) Put(At(Well, Bounds(well), (0, -1), 0, 0, well.Id));
            if (byId.TryGetValue("maison_ville_grande_maison", out SpaceObstacle? great))
            {
                var b = Bounds(great);
                double front = (b.Z1 - b.Z0) / 2;   // façades au bord sud (vers la place)
                // Le long de la façade (droite vue de dehors, depuis la place : vers +x) : corps de garde à l'est, prévôt, four banal à l'ouest.
                Put(At(Guardhouse, b, (0, -1), 16, front - 5.4, great.Id));
                Put(At(Provost, b, (0, -1), -2, front - 5.4, great.Id));
                Put(At(Oven, b, (0, -1), -16.5, front - 2.025, great.Id));
                // Grenier au fond, à l'ouest, face à la cour (au sud) : sa façade (Y −7) à 16 m du bord sud.
                Put(At(Granary, b, (0, -1), -18, front - 16 - 7, great.Id));
            }
            // Relecture indépendante : l'étal de l'atelier se lit dans le numéro N de « etal_ville_N » (1 à 6), pas dans le rang.
            foreach (SpaceObstacle stall in obstacles.Where(o => o.Id.StartsWith("etal_ville_", StringComparison.Ordinal)).OrderBy(o => o.Id, StringComparer.Ordinal))
            {
                if (!int.TryParse(stall.Id.Substring("etal_ville_".Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int n)
                    || n < 1 || n > 6) continue;
                var b = Bounds(stall);
                Put(At(Stalls[n - 1], b, RoadSide(stall.Id, b, roads), 0, 0, stall.Id));
                // Les deux derniers étals de l'atelier (étoffes, poteries) à côté des étals 5 et 6 (au sud), sur la même ligne.
                if (n >= 5) Put(At(Stalls[n + 1], b, RoadSide(stall.Id, b, roads), 4.5, 0, null));
            }
            if (byId.TryGetValue("puits_ville_marche", out SpaceObstacle? square))
            {
                var b = Bounds(square);
                double cx = (b.X0 + b.X1) / 2;
                // Au sud de la route d'Ambre, entre les étals du sud : fontaine, puis lavoir derrière elle.
                Put(new BourgPiece(Fountain, Round(cx), Round(b.Z0 - 52), Yaw((0, 1)), null));
                Put(new BourgPiece(Washhouse, Round(cx), Round(b.Z0 - 80), Yaw((0, 1)), null));
            }
        }

        /// <summary>Marge (m) admise entre le pied d'une maison type et l'emprise de sa maison (rives, encorbellements).</summary>
        public const double HouseTolerance = 0.15;

        /// <summary>Part de la surface de la plus grande maison qui tient en dessous de laquelle une maison type n'est pas tirée.</summary>
        public const double HouseChoice = 0.55;

        /// <summary>
        /// Maisons de la ville (<c>maison_ville_NN</c>, hors la grande maison) : pignon sur rue (décision de Damien pour les maisons types)
        /// sur le petit côté de l'emprise, vers la route la plus proche des deux petits côtés ; maison type tirée (d'après l'identifiant)
        /// parmi celles qui tiennent dans l'emprise et dont la surface vaut au moins <see cref="HouseChoice"/> de la plus grande qui tient.
        /// </summary>
        private static void HousesOf(IReadOnlyList<SpaceObstacle> obstacles, IReadOnlyList<StyleRoad> roads, List<BourgPiece> pieces,
            Dictionary<string, List<string>> needs)
        {
            foreach (SpaceObstacle o in obstacles.Where(o => IsTownHouse(o.Id)).OrderBy(o => o.Id, StringComparer.Ordinal))
            {
                var b = Bounds(o);
                (int X, int Z) facing = HouseFacing(b, roads);
                string? model = HouseModel(o.Id, b, facing);
                if (model == null) continue;
                pieces.Add(At(model, b, facing, 0, 0, o.Id));
                Need(needs, o.Id, model);
            }
        }

        /// <summary>Maison numérotée de la ville (« maison_ville_NN »).</summary>
        public static bool IsTownHouse(string? id) =>
            id != null && id.StartsWith("maison_ville_", StringComparison.Ordinal) && id.Length > 13 && id.Skip(13).All(char.IsDigit);

        /// <summary>Façade d'une maison : sur un petit côté, celui dont le milieu est le plus près d'une route (à égalité : est, puis nord).</summary>
        public static (int X, int Z) HouseFacing((double X0, double X1, double Z0, double Z1) b, IReadOnlyList<StyleRoad> roads)
        {
            bool alongX = b.X1 - b.X0 >= b.Z1 - b.Z0;   // grand côté le long de x : façade à l'est ou à l'ouest
            (double ax, double az, (int, int) a) = alongX ? (b.X1, (b.Z0 + b.Z1) / 2, (1, 0)) : ((b.X0 + b.X1) / 2, b.Z1, (0, 1));
            (double bx, double bz, (int, int) c) = alongX ? (b.X0, (b.Z0 + b.Z1) / 2, (-1, 0)) : ((b.X0 + b.X1) / 2, b.Z0, (0, -1));
            if (roads.Count == 0) return a;
            return TownRules.RoadAt(bx, bz, roads).Edge < TownRules.RoadAt(ax, az, roads).Edge - 1e-9 ? c : a;
        }

        /// <summary>Maison type d'une emprise (null si aucune ne tient).</summary>
        public static string? HouseModel(string id, (double X0, double X1, double Z0, double Z1) b, (int X, int Z) facing)
        {
            double cx = (b.X0 + b.X1) / 2, cz = (b.Z0 + b.Z1) / 2, yaw = Yaw(facing);
            var fits = Houses.Where(h =>
            {
                var f = Footprint(h, cx, cz, yaw);
                return f.X0 >= b.X0 - HouseTolerance && f.X1 <= b.X1 + HouseTolerance && f.Z0 >= b.Z0 - HouseTolerance && f.Z1 <= b.Z1 + HouseTolerance;
            }).Select(h => (Piece: h, Area: Area(h))).ToList();
            if (fits.Count == 0) return null;
            double best = fits.Max(f => f.Area);
            var pool = fits.Where(f => f.Area >= HouseChoice * best).Select(f => f.Piece).OrderBy(p => p, StringComparer.Ordinal).ToList();
            return pool[StyleRules.Variant(id + "/maison", pool.Count)];
        }

        private static double Area(string piece)
        {
            var v = Box(piece)!.Value;
            return (v.X1 - v.X0) * (v.Y1 - v.Y0);
        }

        // --- V2.30.4 : rues, accessoires, cours (décor) -------------------------------------------------------------------------

        /// <summary>Écart (m) gardé entre un accessoire et un obstacle, une route ou un autre accessoire.</summary>
        public const double PropClearance = 0.3;

        /// <summary>
        /// Décor des rues (aucun obstacle ne lui correspond ; il n'arrête rien) : place du marché pavée (modules de 6 m sur le rectangle des
        /// étals agrandi de 3 m) et bordée ; route d'Ambre pavée entre les portes (hors de la place), chemin des vignes en ruelle de terre ;
        /// aux portes, braseros et râtelier dedans, bannières dehors, barrière de péage ouverte le long de la route devant la porte de l'Est,
        /// chariots de caravane dans la porte de l'Ouest, un panneau dans chaque porte ; marchandises derrière les étals, charrettes, bancs
        /// autour de la fontaine, pilori sur la place, abreuvoir devant les écuries ; un élément de cour derrière une maison sur trois. Une
        /// place prise par un obstacle, une route (sauf les sols) ou un autre accessoire est sautée.
        /// </summary>
        private static void Streets(IReadOnlyList<SpaceObstacle> obstacles, IReadOnlyList<StyleRoad> roads, List<BourgPiece> pieces)
        {
            var gates = pieces.Where(p => p.Piece == EastGate || p.Piece == WestGate || p.Piece == NorthGate).ToList();
            if (gates.Count == 0) return;
            var boxes = obstacles.Select(o => (o.Id, Box: Bounds(o))).ToList();
            var taken = pieces.Where(p => !IsGround(p.Piece) && Box(p.Piece) != null).Select(p => Footprint(p.Piece, p.X, p.Z, p.Yaw)).ToList();
            var placed = new List<BourgPiece>();
            bool Try(string piece, double x, double z, double yaw)
            {
                var f = Footprint(piece, x, z, yaw);
                bool ground = IsGround(piece);
                double m = ground ? 0 : PropClearance;
                foreach (var (id, b) in boxes)
                {
                    if (ground && (id.StartsWith("etal_ville_", StringComparison.Ordinal) || id == "puits_ville_marche"
                        || id.StartsWith(AdventureSession.WaySignPrefix, StringComparison.Ordinal))) continue;
                    if (f.X0 < b.X1 + m && f.X1 > b.X0 - m && f.Z0 < b.Z1 + m && f.Z1 > b.Z0 - m) return false;
                }
                if (ground)
                {
                    foreach (BourgPiece q in pieces.Concat(placed))
                    {
                        if (q.Piece != Fountain && q.Piece != Washhouse && q.Piece != Parvis && q.Piece != Cemetery && q.Piece != Pillory && q.Piece != Trough) continue;
                        var t = Footprint(q.Piece, q.X, q.Z, q.Yaw);
                        if (f.X0 < t.X1 && f.X1 > t.X0 && f.Z0 < t.Z1 && f.Z1 > t.Z0) return false;
                    }
                }
                else
                {
                    foreach (var t in taken)
                        if (f.X0 < t.X1 + m && f.X1 > t.X0 - m && f.Z0 < t.Z1 + m && f.Z1 > t.Z0 - m) return false;
                    var corners = new[] { (f.X0, f.Z0), (f.X0, f.Z1), (f.X1, f.Z0), (f.X1, f.Z1), (x, z) };
                    if (roads.Count > 0 && corners.Any(c => TownRules.RoadAt(c.Item1, c.Item2, roads).Edge < m)) return false;
                    taken.Add(f);
                }
                placed.Add(new BourgPiece(piece, Round(x), Round(z), Round(yaw), null));
                return true;
            }
            // Portes : dedans braseros et râtelier, dehors bannières ; panneau à 25 m dedans ; péage et chariots.
            int gateIndex = 0;
            foreach (BourgPiece g in gates.OrderBy(g => g.Piece, StringComparer.Ordinal))
            {
                double a = g.Yaw * Math.PI / 180;
                (double ox, double oz) = (Math.Sin(a), Math.Cos(a));     // vers l'extérieur
                (double sx, double sz) = (Math.Cos(a), -Math.Sin(a));    // le long du mur
                double half = Passage(g.Piece) / 2, wide = Box(g.Piece)!.Value.X1 + 1;
                (double X, double Z) P(double out_, double along) => (g.X + ox * out_ + sx * along, g.Z + oz * out_ + sz * along);
                foreach (int side in new[] { -1, 1 })
                {
                    var b = P(-8, side * (half + 3));
                    Try(Brazier, b.X, b.Z, g.Yaw);
                    var f = P(2.6, side * wide);   // V2.30.5 : entre la palissade et le fossé (4,2 m)
                    Try(Banner, f.X, f.Z, g.Yaw);
                }
                var r = P(-12, half + 5);
                Try(Rack, r.X, r.Z, g.Yaw + 180);
                var sign = P(-25, half + 3);
                Try(Signs[gateIndex++ % Signs.Length], sign.X, sign.Z, g.Yaw + 90);
                if (g.Piece == EastGate)
                {
                    var t = P(18, half + 4.5);
                    Try(TollBar, t.X, t.Z, g.Yaw + 90);   // ouverte : la lisse le long de la route
                }
                if (g.Piece == WestGate)
                    for (int k = 0; k < Wagons.Length; k++)
                    {
                        var c = P(-22 - 9 * k, (k % 2 == 0 ? 1 : -1) * (half + 6));
                        Try(Wagons[k % Wagons.Length], c.X, c.Z, g.Yaw);
                    }
            }
            // Étals de la place : marchandises derrière chacun (côté opposé à sa face), une charrette à côté des étals du sud.
            int stallIndex = 0, cartIndex = 0;
            foreach (BourgPiece s in pieces.Where(p => Array.IndexOf(Stalls, p.Piece) >= 0 && p.Covers != null).OrderBy(p => p.Covers, StringComparer.Ordinal))
            {
                double a = s.Yaw * Math.PI / 180;
                (double fx, double fz) = (Math.Sin(a), Math.Cos(a));
                (double rx, double rz) = (Math.Cos(a), -Math.Sin(a));
                for (int k = 0; k < 3; k++)
                {
                    string good = Goods[(stallIndex * 3 + k) % Goods.Length];
                    Try(good, s.X - fx * 3.0 + rx * (k - 1) * 2.3, s.Z - fz * 3.0 + rz * (k - 1) * 2.3, s.Yaw + StyleRules.Variant(s.Covers + "/lacet" + k, 30) - 15);
                }
                stallIndex++;
                // Une charrette à côté de chaque étal, du côté libre, tant qu'il en reste à poser (trois).
                if (cartIndex < Carts.Length && (Try(Carts[cartIndex], s.X - rx * 5, s.Z - rz * 5, s.Yaw + 90) || Try(Carts[cartIndex], s.X + rx * 5, s.Z + rz * 5, s.Yaw + 90)))
                    cartIndex++;
            }
            // Bancs autour de la fontaine, pilori sur la place, abreuvoir devant les écuries.
            BourgPiece? fountain = pieces.Where(p => p.Piece == Fountain).Select(p => (BourgPiece?)p).FirstOrDefault();
            if (fountain != null)
            {
                BourgPiece f = fountain.Value;
                Try("r22_banc_dossier", f.X - 5, f.Z, -90 + 180);
                Try("r22_banc_dossier", f.X + 5, f.Z, 90 + 180);
                Try("r22_banc_sans_dossier", f.X, f.Z - 5, 0);
                Try(Pillory, f.X + 14, f.Z + 4, 0);
            }
            BourgPiece? stables = pieces.Where(p => p.Piece == Stables).Select(p => (BourgPiece?)p).FirstOrDefault();
            if (stables != null) Try(Trough, stables.Value.X + 9, stables.Value.Z - 14, 0);
            // Cours : derrière une maison sur trois (côté opposé à sa façade), un élément tiré de son identifiant.
            var houses = pieces.Where(p => IsTownHouse(p.Covers)).OrderBy(p => p.Covers, StringComparer.Ordinal).ToList();
            for (int k = 0; k < houses.Count; k += 3)
            {
                BourgPiece h = houses[k];
                var hb = boxes.Single(b => b.Id == h.Covers).Box;
                double a = h.Yaw * Math.PI / 180;
                (double fx, double fz) = (Math.Round(Math.Sin(a)), Math.Round(Math.Cos(a)));
                double back = fx != 0 ? (hb.X1 - hb.X0) / 2 : (hb.Z1 - hb.Z0) / 2;
                string yard = Yards[StyleRules.Variant(h.Covers + "/cour", Yards.Length)];
                var yb = Box(yard)!.Value;
                double depth = Math.Max(yb.Y1 - yb.Y0, yb.X1 - yb.X0) / 2;
                Try(yard, h.X - fx * (back + PropClearance + 0.2 + depth), h.Z - fz * (back + PropClearance + 0.2 + depth), h.Yaw + 180);
            }
            // Sols en dernier (relecture indépendante) : ni sous la fontaine, le lavoir, le parvis, le cimetière, le pilori ou l'abreuvoir.
            // Place du marché : le rectangle des étals agrandi de 3 m, pavé de modules de 6 m, bordé là où rien ne passe.
            var stalls = boxes.Where(b => b.Id.StartsWith("etal_ville_", StringComparison.Ordinal)).Select(b => b.Box).ToList();
            (double X0, double X1, double Z0, double Z1) square = default;
            if (stalls.Count > 0)
            {
                square = (stalls.Min(b => b.X0) - 3, stalls.Max(b => b.X1) + 3, stalls.Min(b => b.Z0) - 3, stalls.Max(b => b.Z1) + 3);
                int nx = (int)Math.Ceiling((square.X1 - square.X0) / 6), nz = (int)Math.Ceiling((square.Z1 - square.Z0) / 6);
                square = (square.X0, square.X0 + 6 * nx, square.Z0, square.Z0 + 6 * nz);
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < nz; j++) Try(Paving, square.X0 + 3 + 6 * i, square.Z0 + 3 + 6 * j, 0);
                for (int i = 0; i < nx; i++)
                {
                    TryCurb(square.X0 + 3 + 6 * i, square.Z0 - 0.12, 0);
                    TryCurb(square.X0 + 3 + 6 * i, square.Z1 + 0.12, 180);
                }
                for (int j = 0; j < nz; j++)
                {
                    TryCurb(square.X0 - 0.12, square.Z0 + 3 + 6 * j, -90);
                    TryCurb(square.X1 + 0.12, square.Z0 + 3 + 6 * j, 90);
                }
            }
            void TryCurb(double x, double z, double yaw)
            {
                // Pas de bordure en travers d'une route.
                if (roads.Count > 0 && TownRules.RoadAt(x, z, roads).Edge < 3.5) return;
                Try(Curb, x, z, yaw);
            }
            bool InSquare(double x, double z) => stalls.Count > 0 && x > square.X0 - 4 && x < square.X1 + 4 && z > square.Z0 - 4 && z < square.Z1 + 4;
            // Route d'Ambre pavée et chemin des vignes en terre, dans les murs, hors de la place et des portes (12 m).
            var walls = boxes.Where(b => IsWall(b.Id)).Select(b => b.Box).ToList();
            double wx0 = walls.Min(b => b.X0), wx1 = walls.Max(b => b.X1), wz0 = walls.Min(b => b.Z0), wz1 = walls.Max(b => b.Z1);
            foreach (StyleRoad road in roads.Where(r => r.Id == "route_d_ambre_ouest" || r.Id == "chemin_des_vignes"))
            {
                string piece = road.Id == "route_d_ambre_ouest" ? Street : Lane;
                for (int i = 0; i + 1 < road.Points.Count; i++)
                {
                    (double ax, double az) = road.Points[i];
                    (double bx, double bz) = road.Points[i + 1];
                    double length = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                    double yaw = Math.Atan2(-(bz - az), bx - ax) * 180 / Math.PI;   // le x du modèle (6 m) le long de la route
                    for (double t = 3; t + 3 <= length + 1e-9; t += 6)
                    {
                        double x = ax + (bx - ax) * t / length, z = az + (bz - az) * t / length;
                        if (x < wx0 + 12 || x > wx1 - 12 || z < wz0 + 12 || z > wz1 - 12 || InSquare(x, z)) continue;
                        Try(piece, x, z, yaw);
                    }
                }
            }
            pieces.AddRange(placed);
        }

        // --- V2.30.5 : décisions de Damien du 16 novembre 2026 (vers 21 h 30) -------------------------------------------------------

        /// <summary>Boutiques P13 à P18 et maison notable H09 sur leurs obstacles, façade vers la route la plus proche.</summary>
        private static void Shops(IReadOnlyList<SpaceObstacle> obstacles, IReadOnlyList<StyleRoad> roads, List<BourgPiece> pieces, Dictionary<string, List<string>> needs)
        {
            foreach (SpaceObstacle o in obstacles.Where(o => ShopModel(o.Id) != null).OrderBy(o => o.Id, StringComparer.Ordinal))
            {
                var b = Bounds(o);
                string model = ShopModel(o.Id)!;
                pieces.Add(At(model, b, RoadSide(o.Id, b, roads), 0, 0, o.Id));
                Need(needs, o.Id, model);
            }
        }

        /// <summary>
        /// Fossé : modules F11 de 6 m serrés le long de chaque obstacle du fossé (face extérieure dehors) ; un pont devant chaque porte
        /// (F13 à l'est, F14 à l'ouest, F15 à la poterne), au milieu du fossé ; talus F12 au pied intérieur de chaque module de palissade
        /// (pas sous les escaliers de ronde).
        /// </summary>
        private static void Moat(IReadOnlyList<SpaceObstacle> obstacles, List<BourgPiece> pieces, Dictionary<string, List<string>> needs)
        {
            var walls = obstacles.Where(o => IsWall(o.Id)).Select(Bounds).ToList();
            if (walls.Count == 0) return;
            double cx = (walls.Min(b => b.X0) + walls.Max(b => b.X1)) / 2, cz = (walls.Min(b => b.Z0) + walls.Max(b => b.Z1)) / 2;
            foreach (SpaceObstacle o in obstacles.Where(o => IsDitch(o.Id)).OrderBy(o => o.Id, StringComparer.Ordinal))
            {
                var b = Bounds(o);
                bool alongX = b.X1 - b.X0 >= b.Z1 - b.Z0;
                double line = alongX ? (b.Z0 + b.Z1) / 2 : (b.X0 + b.X1) / 2;
                int sign = line >= (alongX ? cz : cx) ? 1 : -1;
                (int, int) facing = alongX ? (0, sign) : (sign, 0);
                double from = alongX ? b.X0 : b.Z0, to = alongX ? b.X1 : b.Z1;
                int n = Math.Max(1, (int)Math.Ceiling((to - from) / ModuleLength - 1e-9));
                for (int k = 0; k < n; k++)
                {
                    double c = n == 1 ? (from + to) / 2 : from + ModuleLength / 2 + k * (to - from - ModuleLength) / (n - 1);
                    (double x, double z) = alongX ? (c, line) : (line, c);
                    pieces.Add(new BourgPiece(Ditch, Round(x), Round(z), Yaw(facing), o.Id));
                }
                Need(needs, o.Id, Ditch);
            }
            if (!obstacles.Any(o => IsDitch(o.Id))) return;
            foreach (BourgPiece g in pieces.Where(p => p.Piece == EastGate || p.Piece == WestGate || p.Piece == NorthGate).ToList())
            {
                double a = g.Yaw * Math.PI / 180;
                string bridge = g.Piece == EastGate ? EastBridge : g.Piece == WestGate ? WestBridge : NorthBridge;
                pieces.Add(new BourgPiece(bridge, Round(g.X + Math.Sin(a) * MoatCenter), Round(g.Z + Math.Cos(a) * MoatCenter), g.Yaw, null));
            }
            foreach (BourgPiece t in pieces.Where(p => p.Piece == Wall || p.Piece == WallBroken || p.Piece == WallMended).ToList())
            {
                double a = t.Yaw * Math.PI / 180;
                pieces.Add(new BourgPiece(Bank, Round(t.X - Math.Sin(a) * BankInset), Round(t.Z - Math.Cos(a) * BankInset), t.Yaw, null));
            }
        }

        /// <summary>
        /// V2.30.5 (revue Codex de la PR #204) : place au sol de la lanterne de porte d'un monument habillé, d'après la pièce posée et non
        /// d'après la boîte de son obstacle (l'église avance de 2 m devant la sienne ; la cour du prévôt a quatre bâtiments) : sur la façade
        /// du modèle (le devant de son pied, Y le plus petit de l'export), 0,25 m devant, 0,9 m à droite du milieu vu de dehors.
        /// </summary>
        public static (double X, double Z) DoorLanternOf(BourgPiece p)
        {
            var b = Box(p.Piece) ?? throw new ArgumentException("pièce inconnue : " + p.Piece, nameof(p));
            double a = p.Yaw * Math.PI / 180, fx = Math.Sin(a), fz = Math.Cos(a);
            double ahead = -b.Y0 + 0.25;
            return (Round(p.X + fx * ahead - fz * 0.9), Round(p.Z + fz * ahead + fx * 0.9));
        }

        /// <summary>
        /// V2.30.5 (revue Codex de la PR #204) : fenêtres de façade à éclairer la nuit pour les modèles dont les volets ne sont pas des objets
        /// séparés (boutiques P13 à P18, maison du prévôt, corps de garde, écuries : un seul maillage par FBX). Chaque fenêtre : centre
        /// (x, y, z) dans le repère du modèle d'Unity (z = −Y de Blender : devant la façade, 3 cm en avant du mur, Y de la façade d'après le
        /// recentrage de l'export de jeu), largeur et hauteur (m). Rez-de-chaussée à 1,60 m, étage à 4,80 m (étage Z 3,20 des fiches), au
        /// quart et aux trois quarts de la largeur (la porte est au milieu) ; écuries : quatre baies du logement (fiche P07 : X 5, 11, 19, 25,
        /// allège 4). Vide pour les autres pièces. Approximation : les baies réelles des modèles n'ont pas été relevées.
        /// </summary>
        public static IReadOnlyList<(double X, double Y, double Z, double Width, double Height)> FacadeWindows(string piece)
        {
            double facade;
            switch (piece)
            {
                case "p13_armurier": case "p14_forgeron": facade = 4.6; break;
                case "p15_tanneur": case "p18_taverne": facade = 5.6; break;
                case "p16_apothicaire": case "p17_tailleur": facade = 4.1; break;
                case Provost: case Guardhouse: facade = 5.4; break;
                case Stables:
                    return new[] { 5.0, 11.0, 19.0, 25.0 }.Select(x => (Round(15.0 - x), 4.6, 12.03, 1.0, 1.0)).ToList();
                default: return Array.Empty<(double, double, double, double, double)>();
            }
            var b = Box(piece)!.Value;
            double quarter = (b.X1 - b.X0) / 4, z = facade + 0.03;
            return new[] { (-quarter, 1.6), (quarter, 1.6), (-quarter, 4.8), (quarter, 4.8) }
                .Select(w => (Round(w.Item1), w.Item2, Round(z), 0.8, 1.0)).ToList();
        }

        /// <summary>V2.30.5 : pièce d'un monument habillé qui a une lanterne de porte (église, halle, écuries, bâtiments de la cour du prévôt).</summary>
        public static bool HasDoorLantern(string piece) =>
            piece == Church || piece == Hall || piece == Stables || piece == Provost || piece == Guardhouse || piece == Granary || piece == Oven;

        /// <summary>Milieu du fossé (m hors de la ligne de la palissade : de 4,2 à 11,2 m, accroche « face_exterieure_rideau » de F11).</summary>
        public const double MoatCenter = 7.7;

        /// <summary>Centre du talus (m vers la ville) : son bord A (Y −2) contre la face intérieure du rideau (0,16 m).</summary>
        public const double BankInset = 2.16;

        /// <summary>
        /// Façades vivantes : une lanterne sur potence (R21, fixée à 3,50 m) à droite de la porte de chaque maison et de chaque boutique ; un
        /// auvent de toile (R30, fixé à 3,20 m) au-dessus de l'entrée des boutiques ; deux bannières sur potence (F19, fixées à 4,50 m) sur la
        /// maison du prévôt ; un râtelier mural (F18) sur le corps de garde ; une corde à linge (R29, anneaux à 4,20 m) entre deux maisons dont
        /// les façades se font face à moins de 8 m. Les hauteurs sont celles des accroches de l'atelier moins la hauteur du modèle.
        /// </summary>
        private static void Facades(IReadOnlyList<SpaceObstacle> obstacles, List<BourgPiece> pieces)
        {
            var boxes = obstacles.ToDictionary(o => o.Id, Bounds, StringComparer.Ordinal);
            var added = new List<BourgPiece>();
            (double fx, double fz, double rx, double rz, double half) Frame(BourgPiece p)
            {
                double a = p.Yaw * Math.PI / 180;
                double fx = Math.Round(Math.Sin(a)), fz = Math.Round(Math.Cos(a));
                var b = boxes[p.Covers!];
                double half = fx != 0 ? (b.X1 - b.X0) / 2 : (b.Z1 - b.Z0) / 2;
                return (fx, fz, -fz, fx, half);   // droite vue de dehors : (−fz, fx)
            }
            int awning = 0;
            foreach (BourgPiece p in pieces.Where(p => p.Covers != null && boxes.ContainsKey(p.Covers) && (IsTownHouse(p.Covers) || ShopModel(p.Covers) != null)))
            {
                var (fx, fz, rx, rz, half) = Frame(p);
                double ox = p.X + fx * (half + 0.25), oz = p.Z + fz * (half + 0.25);
                added.Add(new BourgPiece(WallLantern, Round(ox + rx * 0.9), Round(oz + rz * 0.9), p.Yaw, null, 3.5 - 1.2));
                if (ShopModel(p.Covers) != null && p.Covers != "maison_ville_notable")
                    added.Add(new BourgPiece(Awnings[awning++ % Awnings.Length], Round(p.X + fx * (half + 1.1)), Round(p.Z + fz * (half + 1.1)), p.Yaw, null, 3.2 - 1.1));
            }
            foreach (BourgPiece p in pieces.Where(p => p.Piece == Provost || p.Piece == Guardhouse).ToList())
            {
                var (fx, fz, rx, rz, half) = Frame(p);
                // La façade du modèle (Y −5,40) devant le pivot ; les accessoires à 0,1 m devant elle.
                double ox = p.X + fx * 5.5, oz = p.Z + fz * 5.5;
                if (p.Piece == Provost)
                    foreach (int side in new[] { -1, 1 })
                        added.Add(new BourgPiece(WallBanner, Round(ox + rx * side * 4), Round(oz + rz * side * 4), p.Yaw, null, 4.5 - 2.4));
                else
                    added.Add(new BourgPiece(WallRack, Round(ox + rx * 5), Round(oz + rz * 5), p.Yaw, null, 0.3));
            }
            // Linge : dans la ruelle entre deux bâtiments voisins d'un même rang (2 à 8 m d'écart le long de x, au moins 3 m en regard) ; les
            // maisons numérotées sont trop espacées (plus de 30 m) : ce sont les boutiques de la route d'Ambre qui en ont.
            var houses = pieces.Where(p => IsTownHouse(p.Covers) || ShopModel(p.Covers) != null).OrderBy(p => p.Covers, StringComparer.Ordinal).ToList();
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (BourgPiece h in houses)
            {
                if (used.Contains(h.Covers!)) continue;
                var b = boxes[h.Covers!];
                foreach (BourgPiece o in houses)
                {
                    if (o.Covers == h.Covers || used.Contains(o.Covers!)) continue;
                    var c = boxes[o.Covers!];
                    double gap = c.X0 - b.X1, overlap = Math.Min(b.Z1, c.Z1) - Math.Max(b.Z0, c.Z0);
                    if (gap < 2 || gap > 8 || overlap < 3) continue;
                    // La corde (x du modèle, 8 m) d'un mur à l'autre, au milieu de la partie en regard.
                    added.Add(new BourgPiece(Line, Round(b.X1 + gap / 2), Round((Math.Max(b.Z0, c.Z0) + Math.Min(b.Z1, c.Z1)) / 2), 0, null, 4.2 - 1.86));
                    used.Add(h.Covers!);
                    used.Add(o.Covers!);
                    break;
                }
            }
            pieces.AddRange(added);
        }

        /// <summary>Distance (m, le long du pan) entre le centre d'une porte et celui d'un module coiffé de la ronde couverte.</summary>
        public const double WalkwayReach = 20;

        /// <summary>Un escalier de ronde tous les <see cref="StairsEvery"/> modules (≈ 150 m).</summary>
        public const int StairsEvery = 25;

        /// <summary>
        /// Module du rang <paramref name="index"/> d'une ligne : un escalier tous les <see cref="StairsEvery"/>, sinon un module sur dix
        /// abîmé ou réparé (tirés du rang et du côté : toujours les mêmes), les autres droits.
        /// </summary>
        public static string WallPiece((int X, int Z) side, int index)
        {
            if (index % StairsEvery == StairsEvery / 2) return WallStairs;
            int v = StyleRules.Variant("bourg/palissade/" + side.X + "," + side.Z + "/" + index, 20);
            return v == 0 ? WallBroken : v == 1 ? WallMended : Wall;
        }
    }
}
