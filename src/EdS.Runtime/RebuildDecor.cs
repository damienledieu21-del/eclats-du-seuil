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
        /// <summary>Échafaudage fait par le code (pas de modèle à l'atelier : le fort des bandits n'en a qu'une fiche).</summary>
        public const string Scaffold = "echafaudage";

        public const string WoodFull = "h24_tas_bois_plein", WoodHalf = "h24_tas_bois_moitie", WoodLow = "h24_tas_bois_presque_vide";
        public const string EmptyCart = "r20_chariot_plateau_vide", LoadedCart = "r20_chariot_plateau_caisses";
        public const string Crate = "r27_caisse_moyenne", Barrel = "r26_tonneau_moyen", Chips = "tas_eclats", Tools = "outils";

        /// <summary>Rayon (m) d'emprise d'une pièce, pour l'écarter des obstacles et des autres pièces.</summary>
        public static double Radius(string piece)
        {
            switch (piece)
            {
                case Scaffold: return 3.0;
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
        /// Les pièces posées pour <paramref name="site"/> : chacune à la première place candidate libre (bord intérieur du lieu, à
        /// 2,5 m du bord, tous les 3 m, en partant du coin sud-ouest dans le sens des aiguilles d'une montre), tournée vers le centre ; une
        /// pièce sans place libre n'est pas posée.
        /// </summary>
        public static IReadOnlyList<DecorSpot> Layout(RebuildSite site, RebuildPhase phase, int stage, IReadOnlyList<SpaceObstacle> obstacles)
        {
            if (site is null) throw new ArgumentNullException(nameof(site));
            if (obstacles is null) throw new ArgumentNullException(nameof(obstacles));
            IReadOnlyList<string> pieces = PiecesFor(phase, stage);
            var placed = new List<DecorSpot>();
            if (pieces.Count == 0) return placed;
            double x0 = site.Place.Min.X.Raw / 10000.0, z0 = site.Place.Min.Z.Raw / 10000.0;
            double x1 = site.Place.Max.X.Raw / 10000.0, z1 = site.Place.Max.Z.Raw / 10000.0;
            double cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
            List<(double X, double Z)> spots = Ring(x0 + 2.5, z0 + 2.5, x1 - 2.5, z1 - 2.5, 3.0);
            foreach (string piece in pieces)
            {
                double r = Radius(piece);
                foreach ((double x, double z) in spots)
                {
                    if (Crowded(x, z, r, placed) || Blocked(x, z, r, obstacles)) continue;
                    double yaw = Math.Atan2(cx - x, cz - z) * 180.0 / Math.PI;
                    placed.Add(new DecorSpot(piece, x, z, Math.Round(yaw, 3), 1.0));
                    break;
                }
            }
            return placed;
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

        private static bool Crowded(double x, double z, double r, List<DecorSpot> placed)
        {
            foreach (DecorSpot p in placed)
            {
                double d = r + Radius(p.Piece) + 0.5, dx = p.X - x, dz = p.Z - z;
                if (dx * dx + dz * dz < d * d) return true;
            }
            return false;
        }

        /// <summary>Le disque (x, z, r) touche-t-il un obstacle (dedans, ou à moins de r de son bord) ?</summary>
        public static bool Blocked(double x, double z, double r, IReadOnlyList<SpaceObstacle> obstacles)
        {
            foreach (SpaceObstacle o in obstacles)
            {
                IReadOnlyList<GroundVector> pts = o.Points;
                bool inside = false;
                double best = double.MaxValue;
                for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
                {
                    double xi = pts[i].X.Raw / 10000.0, zi = pts[i].Z.Raw / 10000.0, xj = pts[j].X.Raw / 10000.0, zj = pts[j].Z.Raw / 10000.0;
                    if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside;
                    double dx = xj - xi, dz = zj - zi, len = dx * dx + dz * dz;
                    double t = len <= 0 ? 0 : Math.Max(0, Math.Min(1, ((x - xi) * dx + (z - zi) * dz) / len));
                    double px = xi + t * dx - x, pz = zi + t * dz - z;
                    best = Math.Min(best, px * px + pz * pz);
                }
                if (inside || best < r * r) return true;
            }
            return false;
        }
    }
}
