using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EdS.Data.Homes;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>Maison du joueur (V2.53.3) : un meuble posé, tel qu'il se dessine (centre en mètres, rotation, emprise, modèle).</summary>
    public readonly struct HomePieceLook
    {
        public HomePieceLook(string piece, string? model, bool feast, double x, double z, double yaw, double width, double depth, int cellX, int cellZ)
        {
            Piece = piece;
            Model = model;
            Feast = feast;
            X = x;
            Z = z;
            Yaw = yaw;
            Width = width;
            Depth = depth;
            CellX = cellX;
            CellZ = cellZ;
        }

        /// <summary>Identifiant du meuble (catalogue de la maison).</summary>
        public string Piece { get; }

        /// <summary>Code du modèle : lot C (« C_IN1_B1_03 ») pour un objet, fête (« F01_D01 ») pour une décoration ; null sans modèle.</summary>
        public string? Model { get; }

        /// <summary>Le modèle est celui d'une décoration de fête.</summary>
        public bool Feast { get; }

        /// <summary>Centre de l'emprise, en mètres dans la zone de la maison.</summary>
        public double X { get; }
        public double Z { get; }

        /// <summary>Rotation en degrés (0, 90, 180, 270).</summary>
        public double Yaw { get; }

        /// <summary>Emprise une fois tournée, en mètres (largeur le long de x, profondeur le long de z).</summary>
        public double Width { get; }
        public double Depth { get; }

        /// <summary>Case du coin de l'emprise (pour reprendre ou déplacer le meuble).</summary>
        public int CellX { get; }
        public int CellZ { get; }
    }

    /// <summary>
    /// Maison du joueur (sous-lot V2.53.3) : de la maison gardée à ce que l'on dessine. La grille de 0,25 m part du coin bas-gauche du lieu
    /// de la zone intérieure (<c>sol_de_la_maison</c>, le seul lieu de la zone) ; chaque palier ouvre le sol à partir de ce coin. Pur et
    /// déterministe : mêmes données, même dessin.
    /// </summary>
    public static class HomeLook
    {
        /// <summary>Pas de la grille, en mètres.</summary>
        public const double Cell = 0.25;

        /// <summary>Coin de la grille : le coin bas-gauche du lieu de la zone intérieure.</summary>
        public static (double X, double Z) Origin(ZoneDefinition interior)
        {
            if (interior is null) throw new ArgumentNullException(nameof(interior));
            Place floor = interior.Places[0];
            return (floor.Min.X.Raw / (double)Fixed.Scale, floor.Min.Z.Raw / (double)Fixed.Scale);
        }

        /// <summary>Les meubles posés, dans l'ordre de la maison gardée.</summary>
        public static IReadOnlyList<HomePieceLook> Pieces(HomeBook book, SavedHome home, ZoneDefinition interior)
        {
            if (book is null) throw new ArgumentNullException(nameof(book));
            if (home is null) throw new ArgumentNullException(nameof(home));
            var list = new List<HomePieceLook>();
            foreach (SavedPiece p in home.Placed)
            {
                if (!(book.Piece(p.Piece) is HomePiece def)) continue;
                list.Add(At(def, p.X, p.Z, p.Turn, interior));
            }
            return list;
        }

        /// <summary>Le meuble <paramref name="piece"/> tel qu'il se dessinerait posé en (<paramref name="x"/>, <paramref name="z"/>), tourné de <paramref name="turn"/>.</summary>
        public static HomePieceLook At(HomePiece piece, int x, int z, int turn, ZoneDefinition interior)
        {
            if (piece is null) throw new ArgumentNullException(nameof(piece));
            (double ox, double oz) = Origin(interior);
            (int w, int d) = piece.Footprint(turn & 3);
            return new HomePieceLook(piece.Id, piece.FeastCode ?? LotCModels.ForItem(piece.Item), piece.FeastCode != null, ox + (x + w / 2.0) * Cell,
                oz + (z + d / 2.0) * Cell, (turn & 3) * 90.0, w * Cell, d * Cell, x, z);
        }

        /// <summary>Le sol ouvert aujourd'hui, en mètres : coin bas-gauche et coin haut-droit.</summary>
        public static (double MinX, double MinZ, double MaxX, double MaxZ) Floor(HomeBook book, SavedHome home, ZoneDefinition interior)
        {
            (double ox, double oz) = Origin(interior);
            HomeTier tier = HomeRules.Tier(home, book);
            return (ox, oz, ox + tier.Width * Cell, oz + tier.Depth * Cell);
        }

        /// <summary>Case de la grille sous le point (<paramref name="x"/>, <paramref name="z"/>) (peut être hors du sol).</summary>
        public static (int X, int Z) CellAt(ZoneDefinition interior, double x, double z)
        {
            (double ox, double oz) = Origin(interior);
            return ((int)Math.Floor((x - ox) / Cell), (int)Math.Floor((z - oz) / Cell));
        }

        /// <summary>Clé du dessin : change quand un meuble est posé, tourné, repris, ou quand la maison s'agrandit.</summary>
        public static string Key(SavedHome? home) => home is null ? string.Empty
            : home.Stage.ToString(CultureInfo.InvariantCulture) + "|" + string.Join(";", home.Placed.Select(p =>
                p.Piece + "@" + p.X.ToString(CultureInfo.InvariantCulture) + "," + p.Z.ToString(CultureInfo.InvariantCulture) + "," + p.Turn.ToString(CultureInfo.InvariantCulture)));
    }
}
