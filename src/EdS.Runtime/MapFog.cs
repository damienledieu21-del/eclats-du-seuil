using System;
using EdS.Data.Zones;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.29 (2) : image du brouillard de la grande carte, une texel par case de la région (rangée 0 au sud, en bas de l'image,
    /// comme les textures de Unity) : parchemin presque opaque sur une case jamais vue, transparent sur une case vue. Affichée par-dessus la
    /// carte peinte, filtrée (bords doux).
    /// </summary>
    public static class MapFog
    {
        /// <summary>Couleur du brouillard (parchemin), opacité comprise.</summary>
        public const byte R = 214, G = 200, B = 168, Alpha = 235;

        /// <summary>
        /// Décision de Damien du 15 novembre 2026 (vers 21 h 15) : sous le brouillard, tout est caché. Un repère de la carte (nom de lieu,
        /// PNJ, point sûr, porte, créature) en (<paramref name="x"/>, <paramref name="z"/>) ne se montre que sur une case vue ; sans
        /// brouillard (<paramref name="cells"/> null : intérieur, zone sans région) ou hors de la grille, il se montre.
        /// </summary>
        public static bool Visible(ExploredCells? cells, double x, double z)
        {
            if (cells is null) return true;
            RegionGrid g = cells.Grid;
            int column = (int)Math.Floor((x - g.OriginX) / RegionGrid.CellMeters), row = (int)Math.Floor((z - g.OriginZ) / RegionGrid.CellMeters);
            if (column < 0 || row < 0 || column >= g.Columns || row >= g.Rows) return true;
            return cells.IsSeen(column, row);
        }

        /// <summary>Pixels RGBA (colonnes × rangées × 4 octets), rangée 0 d'abord.</summary>
        public static byte[] Rgba(ExploredCells cells)
        {
            if (cells is null) throw new ArgumentNullException(nameof(cells));
            int columns = cells.Grid.Columns, rows = cells.Grid.Rows;
            var pixels = new byte[checked(columns * rows * 4)];
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                {
                    int i = (column + row * columns) * 4;
                    pixels[i] = R;
                    pixels[i + 1] = G;
                    pixels[i + 2] = B;
                    pixels[i + 3] = cells.IsSeen(column, row) ? (byte)0 : Alpha;
                }
            return pixels;
        }
    }
}
