using System;
using System.Collections.Generic;
using EdS.Data.Rebuilds;
using EdS.Domain.Space;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>
    /// Lieux reconstruits, sous-lot V2.43.3 (décor selon l'état) : ce qui se voit autour d'un lieu, d'après son état. En ruine : rien de
    /// plus que le décor du lieu. En chantier : un échafaudage (fait par le code, faute de modèle : <see cref="Scaffold"/>), le tas de bois
    /// du Bourg qui se vide à chaque palier, un chariot à plateau vide, le tas d'éclats et les outils de la carrière. Reconstruit : un
    /// chariot chargé de caisses, une caisse et un tonneau (un lieu habité). Les modèles sont ceux de l'atelier (Bourg et carrière) ; un
    /// modèle absent du catalogue n'est simplement pas posé. Placement déterministe : des places candidates sur le bord intérieur du lieu,
    /// la première assez loin des obstacles et des pièces déjà posées. Affichage seulement : aucune règle ne lit ce décor.
    /// </summary>
    public static class RebuildDecor
    {
        /// <summary>
        /// Échafaudage : le modèle Tripo des lieux reconstruits (« chantiers_echafaudage_simple », 20 novembre 2026) ; sans lui, la vue en
        /// fait un en bois brut par le code.
        /// </summary>
        public const string Scaffold = "echafaudage_simple";

        /// <summary>Modèles Tripo des lieux reconstruits (Decor/chantiers, 20 novembre 2026), avec leur emprise au sol (m, largeur X, profondeur Z).</summary>
        public static readonly IReadOnlyDictionary<string, (double W, double D)> Models = new Dictionary<string, (double, double)>
        {
            ["echafaudage_simple"] = (4.3, 3.7), ["echafaudage_grand"] = (8.1, 5.7), ["palan_et_pierres"] = (4.8, 2.7),
            ["pont_en_chantier"] = (11.6, 8.4), ["pont_reconstruit"] = (12.9, 3.7),
            ["tour_de_signal_en_chantier"] = (8.5, 8.2), ["tour_de_signal_restauree"] = (9.7, 9.4),
            ["dortoir_en_chantier"] = (14.0, 10.4), ["dortoir_couvert"] = (16.1, 11.5),
            ["roue_du_foulon"] = (4.6, 2.4), ["maillets_du_foulon"] = (3.0, 2.6),
        };

        /// <summary>
        /// Grand décor d'un lieu (20 novembre 2026) : le modèle du lieu en chantier et une fois reconstruit, posé sur son obstacle d'ancrage
        /// (ou au centre du lieu), décalé et tourné ; <c>Covers</c> : l'obstacle d'ancrage n'est plus dessiné (le modèle le remplace).
        /// </summary>
        private static readonly Dictionary<string, (string? Anchor, bool Covers, (string Piece, double Dx, double Dz, double Yaw)[] Building, (string Piece, double Dx, double Dz, double Yaw)[] Rebuilt)> Landmarks =
            new Dictionary<string, (string?, bool, (string, double, double, double)[], (string, double, double, double)[])>
            {
                // Le pont enjambe le ravin (12 m d'est en ouest) : le modèle a sa longueur sur X.
                ["pont_effondre"] = ("ravin_pont_effondre_passage", false, new[] { ("pont_en_chantier", 0.0, 0.0, 0.0) }, new[] { ("pont_reconstruit", 0.0, 0.0, 0.0) }),
                // La tour de signal des bergers : le modèle remplace la tour dessinée par le code.
                ["tour_de_signal"] = ("tour_de_signal_bergers", true, new[] { ("tour_de_signal_en_chantier", 0.0, 0.0, 0.0) }, new[] { ("tour_de_signal_restauree", 0.0, 0.0, 0.0) }),
                // Le dortoir, au milieu de ses murs éboulés.
                ["dortoir_de_l_abbaye"] = (null, false, new[] { ("dortoir_en_chantier", 0.0, 0.0, 0.0) }, new[] { ("dortoir_couvert", 0.0, 0.0, 0.0) }),
                // Le moulin à foulon : le palan pendant le chantier ; la roue côté rivière et les maillets de l'autre côté une fois reconstruit.
                ["moulin_a_foulon"] = ("maison_moulin_a_foulon_chute", false, new[] { ("palan_et_pierres", 8.0, 0.0, 90.0) },
                    new[] { ("roue_du_foulon", -7.5, 0.0, 90.0), ("maillets_du_foulon", 7.5, 0.0, 90.0) }),
            };

        /// <summary>Le grand décor du lieu <paramref name="site"/> dans l'état <paramref name="phase"/> (rien en ruine), et l'obstacle qu'il remplace (ou null).</summary>
        public static (IReadOnlyList<DecorSpot> Spots, string? Covered) Landmark(RebuildSite site, RebuildPhase phase, IReadOnlyList<SpaceObstacle> obstacles)
        {
            if (site is null) throw new ArgumentNullException(nameof(site));
            if (phase == RebuildPhase.Ruin || !Landmarks.TryGetValue(site.Id, out var l)) return (Array.Empty<DecorSpot>(), null);
            double cx = (site.Place.Min.X.Raw + site.Place.Max.X.Raw) / 20000.0, cz = (site.Place.Min.Z.Raw + site.Place.Max.Z.Raw) / 20000.0;
            if (l.Anchor != null)
                foreach (SpaceObstacle o in obstacles ?? Array.Empty<SpaceObstacle>())
                {
                    if (o.Id != l.Anchor) continue;
                    double x0 = double.MaxValue, x1 = double.MinValue, z0 = double.MaxValue, z1 = double.MinValue;
                    foreach (GroundVector p in o.Points)
                    {
                        x0 = Math.Min(x0, p.X.Raw / 10000.0); x1 = Math.Max(x1, p.X.Raw / 10000.0);
                        z0 = Math.Min(z0, p.Z.Raw / 10000.0); z1 = Math.Max(z1, p.Z.Raw / 10000.0);
                    }
                    cx = (x0 + x1) / 2; cz = (z0 + z1) / 2;
                    break;
                }
            var spots = new List<DecorSpot>();
            foreach ((string piece, double dx, double dz, double yaw) in phase == RebuildPhase.Rebuilt ? l.Rebuilt : l.Building)
                spots.Add(new DecorSpot(piece, cx + dx, cz + dz, yaw, 1.0));
            return (spots, l.Covers ? l.Anchor : null);
        }

        /// <summary>L'emprise au sol d'une pièce posée (boîte tournée), pour que les petites pièces l'évitent.</summary>
        public static IReadOnlyList<(double X, double Z)> BoxOf(DecorSpot s) =>
            Models.TryGetValue(s.Piece, out var m) ? FeastLooks.Box(s.X, s.Z, s.Yaw, m.W, m.D) : FeastLooks.Box(s.X, s.Z, s.Yaw, 2 * Radius(s.Piece), 2 * Radius(s.Piece));

        public const string WoodFull = "h24_tas_bois_plein", WoodHalf = "h24_tas_bois_moitie", WoodLow = "h24_tas_bois_presque_vide";
        public const string EmptyCart = "r20_chariot_plateau_vide", LoadedCart = "r20_chariot_plateau_caisses";
        public const string Crate = "r27_caisse_moyenne", Barrel = "r26_tonneau_moyen", Chips = "tas_eclats", Tools = "outils";

        /// <summary>Rayon (m) d'emprise d'une pièce, pour l'écarter des obstacles et des autres pièces.</summary>
        public static double Radius(string piece)
        {
            switch (piece)
            {
                case Scaffold: return 2.9;
                case EmptyCart: case LoadedCart: return 2.4;
                case WoodFull: case WoodHalf: case WoodLow: case Chips: return 1.6;
                case Tools: return 1.0;
                default: return 0.8;
            }
        }

        /// <summary>Pièces à poser pour un lieu dans l'état <paramref name="phase"/>, <paramref name="stage"/> paliers finis.</summary>
        public static IReadOnlyList<string> PiecesFor(RebuildPhase phase, int stage)
        {
            switch (phase)
            {
                case RebuildPhase.Building:
                    return new[] { Scaffold, stage <= 0 ? WoodFull : stage == 1 ? WoodHalf : WoodLow, EmptyCart, Chips, Tools };
                case RebuildPhase.Rebuilt:
                    return new[] { LoadedCart, Crate, Barrel };
                default:
                    return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Les pièces posées pour <paramref name="site"/> : chacune à la première place candidate qui convient (bord intérieur du lieu, à
        /// 2,5 m du bord, tous les 3 m, en partant du coin sud-ouest ; puis des anneaux dans la marge du lieu, tous les 3 m), tournée vers le centre. Relecture indépendante de la PR #225 : mêmes
        /// règles que le décor des fêtes (<see cref="FeastLooks.Fits"/>) : emprise hors des obstacles, hors des rues et chemins, à
        /// <see cref="FeastLooks.DoorClearance"/> m des portes, à <see cref="FeastLooks.PeopleClearance"/> m des PNJ (places de jour et de
        /// pluie), des invités et des points sûrs (<paramref name="avoid"/>), sans toucher une pièce déjà posée. Une pièce sans place n'est
        /// pas posée. Déterministe.
        /// </summary>
        public static IReadOnlyList<DecorSpot> Layout(RebuildSite site, RebuildPhase phase, int stage, IReadOnlyList<IReadOnlyList<(double X, double Z)>> obstacles,
            IReadOnlyList<StyleRoad>? roads = null, IReadOnlyList<(double X, double Z)>? doors = null, IReadOnlyList<(double X, double Z)>? avoid = null,
            IReadOnlyList<IReadOnlyList<(double X, double Z)>>? reserved = null)
        {
            if (site is null) throw new ArgumentNullException(nameof(site));
            if (obstacles is null) throw new ArgumentNullException(nameof(obstacles));
            IReadOnlyList<string> pieces = PiecesFor(phase, stage);
            var placed = new List<DecorSpot>();
            if (pieces.Count == 0) return placed;
            IReadOnlyList<StyleRoad> streets = FeastLooks.Streets(roads) ?? Array.Empty<StyleRoad>();
            var taken = new List<IReadOnlyList<(double X, double Z)>>(reserved ?? Array.Empty<IReadOnlyList<(double X, double Z)>>());   // le grand décor du lieu
            double x0 = site.Place.Min.X.Raw / 10000.0, z0 = site.Place.Min.Z.Raw / 10000.0;
            double x1 = site.Place.Max.X.Raw / 10000.0, z1 = site.Place.Max.Z.Raw / 10000.0;
            double cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
            // Bord intérieur du lieu d'abord, puis des anneaux de plus en plus larges dans sa marge (un petit lieu, comme le moulin à foulon).
            List<(double X, double Z)> spots = Ring(x0 + 2.5, z0 + 2.5, x1 - 2.5, z1 - 2.5, 3.0);
            double margin = site.Margin.Raw / 10000.0;
            for (double out_ = 3.0; out_ <= margin; out_ += 3.0) spots.AddRange(Ring(x0 - out_, z0 - out_, x1 + out_, z1 + out_, 3.0));
            foreach (string piece in pieces)
            {
                double side = 2 * Radius(piece);
                foreach ((double x, double z) in spots)
                {
                    double yaw = Math.Round(Math.Atan2(cx - x, cz - z) * 180.0 / Math.PI, 3);
                    if (!FeastLooks.Fits(x, z, yaw, side, side, obstacles, taken, streets, doors ?? Array.Empty<(double, double)>(),
                        avoid ?? Array.Empty<(double, double)>())) continue;
                    placed.Add(new DecorSpot(piece, x, z, yaw, 1.0));
                    taken.Add(FeastLooks.Box(x, z, yaw, side + 1.0, side + 1.0));   // un demi-mètre entre deux pièces
                    break;
                }
            }
            return placed;
        }

        /// <summary>Les polygones des obstacles, en mètres (pour <see cref="Layout"/>).</summary>
        public static IReadOnlyList<IReadOnlyList<(double X, double Z)>> Polygons(IEnumerable<SpaceObstacle> obstacles)
        {
            var list = new List<IReadOnlyList<(double X, double Z)>>();
            foreach (SpaceObstacle o in obstacles)
            {
                var pts = new List<(double X, double Z)>(o.Points.Count);
                foreach (GroundVector p in o.Points) pts.Add((p.X.Raw / 10000.0, p.Z.Raw / 10000.0));
                list.Add(pts);
            }
            return list;
        }

        private static List<(double X, double Z)> Ring(double x0, double z0, double x1, double z1, double step)
        {
            var ring = new List<(double, double)>();
            if (x1 < x0 || z1 < z0) { ring.Add(((x0 + x1) / 2, (z0 + z1) / 2)); return ring; }
            for (double z = z0; z <= z1; z += step) ring.Add((x0, z));
            for (double x = x0 + step; x <= x1; x += step) ring.Add((x, z1));
            for (double z = z1 - step; z >= z0; z -= step) ring.Add((x1, z));
            for (double x = x1 - step; x > x0; x -= step) ring.Add((x, z0));
            return ring;
        }
    }
}
