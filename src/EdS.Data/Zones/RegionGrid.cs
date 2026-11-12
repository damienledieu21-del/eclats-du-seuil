using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EdS.Domain.Collections;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.Data.Zones
{
    /// <summary>
    /// Sous-lot V2.25.3 (schéma de zone 13, grande carte : « cases et repère ») : une case de 128 m de la grille d'une région. Colonne 0 à
    /// l'ouest, rangée 0 au sud. Nom <c>c&lt;colonne&gt;_r&lt;rangée&gt;</c> ; secteur de simulation (2 × 2 cases, 256 m)
    /// <c>s&lt;colonne&gt;_&lt;rangée&gt;</c> sur deux chiffres (<c>s07_06</c>). Rangement : par rangée, puis par colonne.
    /// </summary>
    public readonly struct RegionCell : IEquatable<RegionCell>, IComparable<RegionCell>
    {
        public int Column { get; }
        public int Row { get; }

        public RegionCell(int column, int row)
        {
            if (column < 0 || column >= RegionGrid.MaxColumns) throw new ArgumentOutOfRangeException(nameof(column));
            if (row < 0 || row >= RegionGrid.MaxRows) throw new ArgumentOutOfRangeException(nameof(row));
            Column = column;
            Row = row;
        }

        public string Name => "c" + Column.ToString(CultureInfo.InvariantCulture) + "_r" + Row.ToString(CultureInfo.InvariantCulture);

        public int SectorColumn => Column / RegionGrid.SectorCells;
        public int SectorRow => Row / RegionGrid.SectorCells;

        /// <summary>Secteur de simulation (V2.26) qui contient la case.</summary>
        public string SectorName => "s" + SectorColumn.ToString("D2", CultureInfo.InvariantCulture) + "_" + SectorRow.ToString("D2", CultureInfo.InvariantCulture);

        public bool Equals(RegionCell other) => Column == other.Column && Row == other.Row;
        public override bool Equals(object? obj) => obj is RegionCell c && Equals(c);
        public override int GetHashCode() => Row * RegionGrid.MaxColumns + Column;
        public int CompareTo(RegionCell other) => Row != other.Row ? Row.CompareTo(other.Row) : Column.CompareTo(other.Column);
        public static bool operator ==(RegionCell a, RegionCell b) => a.Equals(b);
        public static bool operator !=(RegionCell a, RegionCell b) => !a.Equals(b);
        public override string ToString() => Name;
    }

    /// <summary>
    /// Sous-lot V2.25.3 (schéma de zone 13) : repère et grille d'une région (docs/PLAN_GRANDE_CARTE.md, § 2.1). Un seul repère pour toute la
    /// région, en mètres (x vers l'est, z vers le nord), celui de la zone : rien ne bouge. Cases carrées de <see cref="CellMeters"/> m depuis
    /// le coin sud-ouest <see cref="OriginX"/>, <see cref="OriginZ"/> ; secteurs de <see cref="SectorCells"/> × <see cref="SectorCells"/> cases.
    /// Données seulement : le moteur ne connaît pas la grille (les secteurs de simulation viennent au V2.26). Entiers et virgule fixe,
    /// arithmétique vérifiée, aucun état mutable.
    /// </summary>
    public sealed class RegionGrid
    {
        /// <summary>Côté d'une case, en mètres (constante du schéma 13).</summary>
        public const int CellMeters = 128;

        /// <summary>Cases par côté d'un secteur de simulation (constante du schéma 13) : secteurs de 256 m.</summary>
        public const int SectorCells = 2;

        public const int SectorMeters = CellMeters * SectorCells;

        /// <summary>Colonnes et rangées au plus (64 × 128 m = 8 192 m).</summary>
        public const int MaxColumns = 64, MaxRows = 64;

        /// <summary>Coin sud-ouest au plus à cette distance de zéro, en mètres.</summary>
        public const int MaxOriginMeters = 100_000;

        /// <summary>Repère retenu des Vallons d'Aube agrandis (plan, § 2.1) : de (−1 908, −1 614) à (2 188, 1 458), 32 × 24 cases.</summary>
        public const int VallonsOriginX = -1908, VallonsOriginZ = -1614, VallonsColumns = 32, VallonsRows = 24;

        private static readonly long CellRaw = Fixed.FromInt(CellMeters).Raw;

        public int OriginX { get; }
        public int OriginZ { get; }
        public int Columns { get; }
        public int Rows { get; }

        /// <summary>Coin sud-ouest et coin nord-est de la région.</summary>
        public GroundVector Min { get; }
        public GroundVector Max { get; }

        public int CellCount => Columns * Rows;
        public int SectorColumns => Columns / SectorCells;
        public int SectorRows => Rows / SectorCells;
        public int SectorCount => SectorColumns * SectorRows;

        /// <summary>Toutes les cases, par rangée (du sud au nord) puis par colonne (d'ouest en est).</summary>
        public IReadOnlyList<RegionCell> Cells { get; }

        /// <summary>Grille de <paramref name="columns"/> × <paramref name="rows"/> cases (nombres pairs : des secteurs entiers).</summary>
        public RegionGrid(int originX, int originZ, int columns, int rows)
        {
            if (Math.Abs((long)originX) > MaxOriginMeters) throw new ArgumentOutOfRangeException(nameof(originX));
            if (Math.Abs((long)originZ) > MaxOriginMeters) throw new ArgumentOutOfRangeException(nameof(originZ));
            if (!IsValidCount(columns, MaxColumns)) throw new ArgumentOutOfRangeException(nameof(columns));
            if (!IsValidCount(rows, MaxRows)) throw new ArgumentOutOfRangeException(nameof(rows));
            OriginX = originX;
            OriginZ = originZ;
            Columns = columns;
            Rows = rows;
            Min = new GroundVector(Fixed.FromInt(originX), Fixed.FromInt(originZ));
            Max = new GroundVector(Fixed.FromInt(checked(originX + columns * CellMeters)), Fixed.FromInt(checked(originZ + rows * CellMeters)));
            var cells = new List<RegionCell>(columns * rows);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < columns; c++) cells.Add(new RegionCell(c, r));
            Cells = Frozen.List(cells, nameof(cells));
        }

        /// <summary>Repère des Vallons d'Aube agrandis (plan, § 2.1).</summary>
        public static RegionGrid Vallons() => new RegionGrid(VallonsOriginX, VallonsOriginZ, VallonsColumns, VallonsRows);

        /// <summary>Nombre de colonnes ou de rangées admis : pair, de 2 à <paramref name="max"/>.</summary>
        public static bool IsValidCount(int count, int max) => count >= SectorCells && count <= max && count % SectorCells == 0;

        /// <summary>Le point est dans la région (bords compris).</summary>
        public bool Contains(GroundVector p) => p.X >= Min.X && p.X <= Max.X && p.Z >= Min.Z && p.Z <= Max.Z;

        /// <summary>
        /// Case d'un point : chaque case contient son bord ouest et son bord sud (un point sur un bord entre deux cases est dans celle de l'est
        /// ou du nord) ; les bords est et nord de la région sont dans la dernière case.
        /// </summary>
        public RegionCell CellOf(GroundVector p)
        {
            if (!Contains(p)) throw new ArgumentOutOfRangeException(nameof(p), "point hors de la région.");
            return new RegionCell(Math.Min(FloorIndex(p.X, Min.X), Columns - 1), Math.Min(FloorIndex(p.Z, Min.Z), Rows - 1));
        }

        /// <summary>
        /// Cases que touche un rectangle fermé (un lieu, la boîte d'un obstacle, d'un tronçon de route) : toutes les cases qui contiennent un
        /// de ses points au sens de <see cref="CellOf"/> (revue Codex de la PR #148) ; un rectangle dont le bord est ou nord tombe sur un bord
        /// de case touche donc aussi la case suivante, comme le point de ce bord. Rangées par rangée, puis par colonne.
        /// </summary>
        public IReadOnlyList<RegionCell> CellsCovering(GroundVector min, GroundVector max)
        {
            if (!Contains(min) || !Contains(max) || min.X > max.X || min.Z > max.Z)
                throw new ArgumentOutOfRangeException(nameof(max), "rectangle hors de la région ou retourné.");
            (int c0, int c1) = Span(min.X, max.X, Min.X, Columns);
            (int r0, int r1) = Span(min.Z, max.Z, Min.Z, Rows);
            var cells = new List<RegionCell>((c1 - c0 + 1) * (r1 - r0 + 1));
            for (int r = r0; r <= r1; r++)
                for (int c = c0; c <= c1; c++) cells.Add(new RegionCell(c, r));
            return cells;
        }

        /// <summary>
        /// Cases que touche une ligne brisée (une route, sa ligne médiane) : réunion des cases de la boîte fermée de chaque tronçon, donc aussi
        /// la case de chaque sommet (<see cref="CellOf"/>), même posé sur un bord de case (un peu plus large que le tracé exact pour un tronçon
        /// en biais ; voulu : un comptage par excès). Rangées, sans doublon.
        /// </summary>
        public IReadOnlyList<RegionCell> CellsAlong(IReadOnlyList<GroundVector> points)
        {
            if (points is null) throw new ArgumentNullException(nameof(points));
            var set = new SortedSet<RegionCell>();
            foreach (GroundVector p in points) set.Add(CellOf(p));   // revue Codex de la PR #148 : chaque sommet dans sa case
            for (int i = 1; i < points.Count; i++)
            {
                GroundVector a = points[i - 1], b = points[i];
                var min = new GroundVector(Fixed.Min(a.X, b.X), Fixed.Min(a.Z, b.Z));
                var max = new GroundVector(Fixed.Max(a.X, b.X), Fixed.Max(a.Z, b.Z));
                foreach (RegionCell c in CellsCovering(min, max)) set.Add(c);
            }
            return set.ToList();
        }

        /// <summary>Cases que touche la boîte d'un polygone (un obstacle du scénario).</summary>
        public IReadOnlyList<RegionCell> CellsOfPolygon(IReadOnlyList<GroundVector> points)
        {
            if (points is null || points.Count == 0) throw new ArgumentException("polygone vide.", nameof(points));
            Fixed x0 = points[0].X, x1 = x0, z0 = points[0].Z, z1 = z0;
            foreach (GroundVector p in points)
            {
                x0 = Fixed.Min(x0, p.X); x1 = Fixed.Max(x1, p.X);
                z0 = Fixed.Min(z0, p.Z); z1 = Fixed.Max(z1, p.Z);
            }
            return CellsCovering(new GroundVector(x0, z0), new GroundVector(x1, z1));
        }

        public GroundVector CellMin(RegionCell c) =>
            new GroundVector(Fixed.FromInt(checked(OriginX + c.Column * CellMeters)), Fixed.FromInt(checked(OriginZ + c.Row * CellMeters)));

        public GroundVector CellMax(RegionCell c) =>
            new GroundVector(Fixed.FromInt(checked(OriginX + (c.Column + 1) * CellMeters)), Fixed.FromInt(checked(OriginZ + (c.Row + 1) * CellMeters)));

        public bool Has(RegionCell c) => c.Column < Columns && c.Row < Rows;

        /// <summary>Case de nom <paramref name="name"/> (<c>c14_r12</c>), si elle est dans la grille.</summary>
        public bool TryParseCell(string? name, out RegionCell cell)
        {
            cell = default;
            if (name is null || name.Length < 5 || name.Length > 8 || name[0] != 'c') return false;
            int sep = name.IndexOf("_r", StringComparison.Ordinal);
            if (sep < 2) return false;
            string cs = name.Substring(1, sep - 1), rs = name.Substring(sep + 2);
            if (!Digits(cs) || !Digits(rs)) return false;
            int c = int.Parse(cs, NumberStyles.None, CultureInfo.InvariantCulture), r = int.Parse(rs, NumberStyles.None, CultureInfo.InvariantCulture);
            if (c >= Columns || r >= Rows) return false;
            cell = new RegionCell(c, r);
            return cell.Name == name;   // refuse « c014_r1 »
        }

        private static bool Digits(string s) => s.Length > 0 && s.Length <= 2 && s.All(ch => ch >= '0' && ch <= '9');

        private static int FloorIndex(Fixed value, Fixed origin)
        {
            long offset = checked(value.Raw - origin.Raw);
            return checked((int)(offset / CellRaw));   // offset ≥ 0 : la division entière est le plancher
        }

        private static (int First, int Last) Span(Fixed min, Fixed max, Fixed origin, int count)
        {
            // Revue Codex de la PR #148 : rectangle fermé, mêmes bords que CellOf (un point sur un bord est à la case de l'est ou du nord).
            int first = Math.Min(FloorIndex(min, origin), count - 1);
            int last = Math.Min(FloorIndex(max, origin), count - 1);
            return (first, last);
        }
    }

    /// <summary>Sous-lot V2.25.3 : contenu d'une case (identifiants, dans l'ordre du fichier).</summary>
    public sealed class RegionCellContent
    {
        public RegionCell Cell { get; }
        public IReadOnlyList<string> Obstacles { get; }
        public IReadOnlyList<string> Places { get; }
        public IReadOnlyList<string> SafePoints { get; }
        public IReadOnlyList<string> Doors { get; }
        public IReadOnlyList<string> Roads { get; }
        public IReadOnlyList<string> Populations { get; }
        public IReadOnlyList<string> NightSpots { get; }

        /// <summary>
        /// Points d'intérêt (plan, § 4.5 ; provisoire) : lieux nommés, points sûrs, portes et populations. Obstacles, routes et places de nuit
        /// sont du décor ou des détails.
        /// </summary>
        public int PointsOfInterest => Places.Count + SafePoints.Count + Doors.Count + Populations.Count;

        /// <summary>Tout ce que la case contient.</summary>
        public int Total => PointsOfInterest + Obstacles.Count + Roads.Count + NightSpots.Count;

        internal RegionCellContent(RegionCell cell, IEnumerable<string> obstacles, IEnumerable<string> places, IEnumerable<string> safePoints,
            IEnumerable<string> doors, IEnumerable<string> roads, IEnumerable<string> populations, IEnumerable<string> nightSpots)
        {
            Cell = cell;
            Obstacles = Frozen.List(obstacles, nameof(obstacles));
            Places = Frozen.List(places, nameof(places));
            SafePoints = Frozen.List(safePoints, nameof(safePoints));
            Doors = Frozen.List(doors, nameof(doors));
            Roads = Frozen.List(roads, nameof(roads));
            Populations = Frozen.List(populations, nameof(populations));
            NightSpots = Frozen.List(nightSpots, nameof(nightSpots));
        }
    }

    /// <summary>
    /// Sous-lot V2.25.3 : découpage d'une zone en cases (<see cref="ZoneDefinition.Region"/>). Fonction pure et déterministe de la zone :
    /// une case par élément ponctuel (<see cref="RegionGrid.CellOf"/>), toutes les cases touchées pour un lieu, un obstacle
    /// (<see cref="RegionGrid.CellsOfPolygon"/>) ou une route (<see cref="RegionGrid.CellsAlong"/>). Ne change rien à la zone ni au jeu.
    /// </summary>
    public static class RegionCutting
    {
        /// <summary>Toutes les cases de la grille, rangées, avec leur contenu (vide pour la plupart).</summary>
        public static IReadOnlyList<RegionCellContent> Cut(ZoneDefinition zone)
        {
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            RegionGrid grid = zone.Region ?? throw new ArgumentException("zone sans région (schéma 13, bloc region).", nameof(zone));
            var buckets = new Dictionary<RegionCell, List<string>[]>();
            void Add(RegionCell c, int kind, string id)
            {
                if (!buckets.TryGetValue(c, out List<string>[]? b))
                    buckets[c] = b = Enumerable.Range(0, 7).Select(_ => new List<string>()).ToArray();
                b[kind].Add(id);
            }
            if (zone.Setup.Space is CombatSpace space)
                foreach (SpaceObstacle o in space.Obstacles)
                    foreach (RegionCell c in grid.CellsOfPolygon(o.Points)) Add(c, 0, o.Id);
            foreach (Place p in zone.Places)
                foreach (RegionCell c in grid.CellsCovering(p.Min, p.Max)) Add(c, 1, p.Id);
            foreach (SafePoint s in zone.SafePoints) Add(grid.CellOf(s.Position), 2, s.Id);
            foreach (Door d in zone.Doors) Add(grid.CellOf(d.Position), 3, d.Id);
            foreach (Road r in zone.Roads)
                foreach (RegionCell c in grid.CellsAlong(r.Points)) Add(c, 4, r.Id);
            foreach (Population p in zone.Populations)
                if (p.Template.Placement is CombatantPlacement at) Add(grid.CellOf(at.Position), 5, p.Creature);
            foreach (NightSpot n in zone.NightSpots) Add(grid.CellOf(n.Position), 6, n.Npc);
            var empty = Array.Empty<string>();
            return grid.Cells.Select(c => buckets.TryGetValue(c, out List<string>[]? b)
                ? new RegionCellContent(c, b[0], b[1], b[2], b[3], b[4], b[5], b[6])
                : new RegionCellContent(c, empty, empty, empty, empty, empty, empty, empty)).ToList();
        }
    }
}
