using System.Globalization;
using System.IO.Compression;
using System.Text;
using EdS.Data;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Zones;
using EdS.Domain.Space;

namespace EdS.Tools.Map
{
    /// <summary>
    /// Sous-lot V2.25.3 (grande carte, cases et repère) : outil <c>eds carte</c>. Liste des cases d'une région et de leur contenu, densité de
    /// points d'intérêt par case (plan, § 4.5 : au moins un par case de terre ferme, à terme), image de contrôle PNG de la grille, des secteurs
    /// et de ce qui existe. Lecture seule : rien n'est écrit dans les données. Sorties déterministes (mêmes données, mêmes octets).
    /// </summary>
    public static class RegionMap
    {
        public const int DefaultPixelsPerCell = 16, MinPixelsPerCell = 4, MaxPixelsPerCell = 128;

        /// <summary>Liste des cases non vides, rangées (sud puis nord, ouest puis est), avec leur secteur, leur emprise et leur contenu.</summary>
        public static string Cells(ZoneDefinition zone, bool withIds = false)
        {
            RegionGrid grid = GridOf(zone);
            IReadOnlyList<RegionCellContent> cut = RegionCutting.Cut(zone);
            var sb = new StringBuilder();
            sb.Append(CultureInfo.InvariantCulture, $"Région de {zone.Id} : {grid.Columns} × {grid.Rows} cases de {RegionGrid.CellMeters} m ({grid.CellCount}), ")
              .Append(CultureInfo.InvariantCulture, $"{grid.SectorColumns} × {grid.SectorRows} secteurs de {RegionGrid.SectorMeters} m ({grid.SectorCount}), ")
              .Append(CultureInfo.InvariantCulture, $"de [{grid.OriginX}, {grid.OriginZ}] à [{grid.Max.X}, {grid.Max.Z}].\n");
            int filled = 0;
            foreach (RegionCellContent c in cut)
            {
                if (c.Total == 0) continue;
                filled++;
                GroundVector min = grid.CellMin(c.Cell), max = grid.CellMax(c.Cell);
                sb.Append(CultureInfo.InvariantCulture, $"{c.Cell.Name,-8} {c.Cell.SectorName}  [{min.X}, {min.Z}] à [{max.X}, {max.Z}]  ")
                  .Append(CultureInfo.InvariantCulture, $"obstacles {c.Obstacles.Count}, lieux {c.Places.Count}, points sûrs {c.SafePoints.Count}, portes {c.Doors.Count}, ")
                  .Append(CultureInfo.InvariantCulture, $"routes {c.Roads.Count}, populations {c.Populations.Count}, places de nuit {c.NightSpots.Count} ; points d'intérêt {c.PointsOfInterest}\n");
                if (!withIds) continue;
                void Ids(string what, IReadOnlyList<string> ids)
                {
                    if (ids.Count > 0) sb.Append("    ").Append(what).Append(" : ").Append(string.Join(", ", ids)).Append('\n');
                }
                Ids("obstacles", c.Obstacles);
                Ids("lieux", c.Places);
                Ids("points sûrs", c.SafePoints);
                Ids("portes", c.Doors);
                Ids("routes", c.Roads);
                Ids("populations", c.Populations);
                Ids("places de nuit", c.NightSpots);
            }
            sb.Append(CultureInfo.InvariantCulture, $"{filled} cases occupées, {grid.CellCount - filled} vides.\n");
            return sb.ToString();
        }

        /// <summary>
        /// Densité : une ligne par rangée, du nord au sud ; un caractère par case : « . » sans point d'intérêt (« o » s'il n'y a que du décor),
        /// 1 à 9 points d'intérêt, « + » au-delà. Puis le compte des cases sans point d'intérêt, et leur liste si <paramref name="listEmpty"/>.
        /// </summary>
        public static string Density(ZoneDefinition zone, bool listEmpty = false)
        {
            RegionGrid grid = GridOf(zone);
            IReadOnlyList<RegionCellContent> cut = RegionCutting.Cut(zone);
            var sb = new StringBuilder();
            sb.Append(CultureInfo.InvariantCulture, $"Densité de {zone.Id} (points d'intérêt par case de {RegionGrid.CellMeters} m : lieux, points sûrs, portes, populations) :\n");
            for (int r = grid.Rows - 1; r >= 0; r--)
            {
                sb.Append(CultureInfo.InvariantCulture, $"r{r,-3}");
                for (int c = 0; c < grid.Columns; c++)
                {
                    RegionCellContent cell = cut[r * grid.Columns + c];
                    int n = cell.PointsOfInterest;
                    sb.Append(n == 0 ? (cell.Total > 0 ? 'o' : '.') : n > 9 ? '+' : (char)('0' + n));
                }
                sb.Append('\n');
            }
            var empty = cut.Where(c => c.PointsOfInterest == 0).Select(c => c.Cell.Name).ToList();
            sb.Append(CultureInfo.InvariantCulture, $"{empty.Count} cases sans point d'intérêt sur {grid.CellCount}.\n");
            if (listEmpty)
                for (int i = 0; i < empty.Count; i += 16) sb.Append(string.Join(" ", empty.Skip(i).Take(16))).Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// Image de contrôle (PNG, 8 bits RVB) des cases <paramref name="first"/> à <paramref name="last"/> (par défaut toute la région) :
        /// cases teintées selon leur densité, lignes des cases (gris) et des secteurs (gris foncé), aire praticable (bleu), obstacles (gris
        /// plein, bas en bleu clair), lieux (orange), routes (brun), points sûrs (bleu), portes (rouge), populations (vert foncé).
        /// </summary>
        public static byte[] Png(ZoneDefinition zone, int pixelsPerCell = DefaultPixelsPerCell, RegionCell? first = null, RegionCell? last = null)
        {
            RegionGrid grid = GridOf(zone);
            if (pixelsPerCell < MinPixelsPerCell || pixelsPerCell > MaxPixelsPerCell) throw new ArgumentOutOfRangeException(nameof(pixelsPerCell));
            RegionCell a = first ?? grid.Cells[0], b = last ?? grid.Cells[grid.CellCount - 1];
            if (!grid.Has(a) || !grid.Has(b) || a.Column > b.Column || a.Row > b.Row) throw new ArgumentOutOfRangeException(nameof(last), "cadre hors de la grille ou retourné.");
            int cols = b.Column - a.Column + 1, rows = b.Row - a.Row + 1;
            var canvas = new Canvas(cols * pixelsPerCell + 1, rows * pixelsPerCell + 1);
            double x0 = grid.CellMin(a).X.Raw / 10_000.0, z1 = grid.CellMax(b).Z.Raw / 10_000.0;
            double scale = pixelsPerCell / (double)RegionGrid.CellMeters;
            int Px(EdS.Domain.Numerics.Fixed x) => (int)Math.Floor((x.Raw / 10_000.0 - x0) * scale);
            int Py(EdS.Domain.Numerics.Fixed z) => (int)Math.Floor((z1 - z.Raw / 10_000.0) * scale);

            IReadOnlyList<RegionCellContent> cut = RegionCutting.Cut(zone);
            for (int r = a.Row; r <= b.Row; r++)
                for (int c = a.Column; c <= b.Column; c++)
                {
                    RegionCellContent cell = cut[r * grid.Columns + c];
                    int n = cell.PointsOfInterest;
                    Rgb fill = n == 0 ? (cell.Total > 0 ? new Rgb(222, 226, 200) : new Rgb(240, 236, 224)) : new Rgb((byte)Math.Max(120, 210 - 6 * n), (byte)Math.Max(170, 235 - 4 * n), (byte)Math.Max(110, 180 - 6 * n));
                    int left = (c - a.Column) * pixelsPerCell, top = (b.Row - r) * pixelsPerCell;
                    canvas.FillRect(left, top, left + pixelsPerCell, top + pixelsPerCell, fill);
                }
            if (zone.Setup.Space is CombatSpace space)
            {
                foreach (SpaceObstacle o in space.Obstacles)
                    canvas.FillPolygon(o.Points.Select(p => (Px(p.X) + 0.5, Py(p.Z) + 0.5)).ToList(), o.Low ? new Rgb(150, 190, 220) : new Rgb(110, 110, 110));
                canvas.Rect(Px(space.Min.X), Py(space.Max.Z), Px(space.Max.X), Py(space.Min.Z), new Rgb(40, 80, 200));
            }
            foreach (Place p in zone.Places) canvas.Rect(Px(p.Min.X), Py(p.Max.Z), Px(p.Max.X), Py(p.Min.Z), new Rgb(230, 140, 30));
            foreach (Road road in zone.Roads)
                for (int i = 1; i < road.Points.Count; i++)
                    canvas.Line(Px(road.Points[i - 1].X), Py(road.Points[i - 1].Z), Px(road.Points[i].X), Py(road.Points[i].Z), new Rgb(140, 90, 40));
            foreach (Population p in zone.Populations)
                if (p.Template.Placement is CombatantPlacement at) canvas.Dot(Px(at.Position.X), Py(at.Position.Z), 0, new Rgb(20, 110, 40));
            foreach (SafePoint s in zone.SafePoints) canvas.Dot(Px(s.Position.X), Py(s.Position.Z), 2, new Rgb(30, 60, 220));
            foreach (Door d in zone.Doors) canvas.Dot(Px(d.Position.X), Py(d.Position.Z), 1, new Rgb(210, 30, 30));
            // Lignes des cases, puis des secteurs par-dessus.
            for (int c = 0; c <= cols; c++)
            {
                bool sector = (a.Column + c) % RegionGrid.SectorCells == 0;
                canvas.Line(c * pixelsPerCell, 0, c * pixelsPerCell, rows * pixelsPerCell, sector ? new Rgb(70, 70, 70) : new Rgb(185, 185, 185));
            }
            for (int r = 0; r <= rows; r++)
            {
                bool sector = (b.Row + 1 - r) % RegionGrid.SectorCells == 0;
                canvas.Line(0, r * pixelsPerCell, cols * pixelsPerCell, r * pixelsPerCell, sector ? new Rgb(70, 70, 70) : new Rgb(185, 185, 185));
            }
            return canvas.ToPng();
        }

        /// <summary>
        /// Lit une zone (et son scénario, ses chefs) avec les données de <paramref name="dataDirectory"/> et les scénarios de
        /// <paramref name="scenariosDirectory"/>. Null et le rapport sur <paramref name="error"/> si elle est refusée.
        /// </summary>
        public static ZoneDefinition? Load(string zonePath, string dataDirectory, string scenariosDirectory, TextWriter error)
        {
            GameDataLoadResult data = GameDataLoader.Load(dataDirectory);
            if (data.Data is null) { error.WriteLine($"{dataDirectory} : données refusées.\n" + string.Join("\n", data.Failures.Select(f => $"{f.Path} : {f.Report}"))); return null; }
            ItemCatalog? items = ItemJsonReader.ReadFile(ItemJsonReader.PathIn(dataDirectory), data.Data).Catalog;
            if (items is null) { error.WriteLine($"{dataDirectory} : catalogue des objets refusé."); return null; }
            ZoneLoadResult zone = ZoneJsonReader.ReadFile(zonePath, scenariosDirectory, data.Data, items);
            if (zone.Zone is null) { error.WriteLine($"{zonePath} : zone refusée.\n{zone.Report}"); return null; }
            return zone.Zone;
        }

        /// <summary>
        /// <c>eds carte cases|densite|image [--zone Z] [--data D] [--scenarios S] [--ids] [--vides] [--out F] [--pixels-par-case N]
        /// [--cadre cA_rB:cC_rD]</c>. Code 0 si tout va bien, 1 sur une erreur d'usage ou de données.
        /// </summary>
        public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
        {
            if (args.Count == 0 || args[0] is not ("cases" or "densite" or "image"))
            {
                error.WriteLine("Usage : eds carte cases|densite|image [--zone Z] [--data D] [--scenarios S] [--ids] [--vides] [--out F] [--pixels-par-case N] [--cadre c14_r12:c15_r13]");
                return 1;
            }
            string data = Path.Combine("data", "v0"), scenarios = "scenarios";
            string? zonePath = null, outPath = null, frame = null;
            bool ids = false, empty = false;
            int ppc = DefaultPixelsPerCell;
            for (int i = 1; i < args.Count; i++)
            {
                bool hasValue = i + 1 < args.Count;
                switch (args[i])
                {
                    case "--zone" when hasValue: zonePath = args[++i]; break;
                    case "--data" when hasValue: data = args[++i]; break;
                    case "--scenarios" when hasValue: scenarios = args[++i]; break;
                    case "--out" when hasValue: outPath = args[++i]; break;
                    case "--cadre" when hasValue: frame = args[++i]; break;
                    case "--ids": ids = true; break;
                    case "--vides": empty = true; break;
                    case "--pixels-par-case" when hasValue && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int n)
                                                  && n >= MinPixelsPerCell && n <= MaxPixelsPerCell:
                        ppc = n;
                        i++;
                        break;
                    default:
                        error.WriteLine($"Option inconnue ou invalide : {args[i]}");
                        return 1;
                }
            }
            zonePath ??= ZoneJsonReader.PathIn(data, "zone.vallons");
            ZoneDefinition? zone = Load(zonePath, data, scenarios, error);
            if (zone is null) return 1;
            if (zone.Region is not RegionGrid grid)
            {
                error.WriteLine($"{zonePath} : zone sans région (schéma 13, bloc region).");
                return 1;
            }
            switch (args[0])
            {
                case "cases":
                    output.Write(Cells(zone, ids));
                    return 0;
                case "densite":
                    output.Write(Density(zone, empty));
                    return 0;
            }
            RegionCell? first = null, last = null;
            if (frame != null)
            {
                string[] parts = frame.Split(':');
                if (parts.Length != 2 || !grid.TryParseCell(parts[0], out RegionCell f) || !grid.TryParseCell(parts[1], out RegionCell l) || f.Column > l.Column || f.Row > l.Row)
                {
                    error.WriteLine($"Cadre invalide : {frame} (« c<col>_r<rang>:c<col>_r<rang> », coin sud-ouest puis nord-est, dans la grille).");
                    return 1;
                }
                (first, last) = (f, l);
            }
            if (outPath is null)
            {
                error.WriteLine("eds carte image : --out fichier.png attendu.");
                return 1;
            }
            File.WriteAllBytes(outPath, Png(zone, ppc, first, last));
            output.WriteLine($"Image écrite : {outPath}");
            return 0;
        }

        private static RegionGrid GridOf(ZoneDefinition zone) =>
            (zone ?? throw new ArgumentNullException(nameof(zone))).Region ?? throw new ArgumentException("zone sans région (schéma 13).", nameof(zone));

        private readonly record struct Rgb(byte R, byte G, byte B);

        /// <summary>Petite toile RVB, avec un encodeur PNG minimal (sans dépendance).</summary>
        private sealed class Canvas
        {
            private readonly int _w, _h;
            private readonly byte[] _pixels;

            public Canvas(int width, int height)
            {
                _w = width;
                _h = height;
                _pixels = new byte[width * height * 3];
            }

            public void Set(int x, int y, Rgb c)
            {
                if (x < 0 || y < 0 || x >= _w || y >= _h) return;
                int i = (y * _w + x) * 3;
                _pixels[i] = c.R;
                _pixels[i + 1] = c.G;
                _pixels[i + 2] = c.B;
            }

            public void FillRect(int x0, int y0, int x1, int y1, Rgb c)
            {
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++) Set(x, y, c);
            }

            public void Rect(int x0, int y0, int x1, int y1, Rgb c)
            {
                Line(x0, y0, x1, y0, c);
                Line(x1, y0, x1, y1, c);
                Line(x1, y1, x0, y1, c);
                Line(x0, y1, x0, y0, c);
            }

            public void Dot(int x, int y, int radius, Rgb c) => FillRect(x - radius, y - radius, x + radius + 1, y + radius + 1, c);

            public void Line(int x0, int y0, int x1, int y1, Rgb c)
            {
                int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
                while (true)
                {
                    Set(x0, y0, c);
                    if (x0 == x1 && y0 == y1) return;
                    int e2 = 2 * err;
                    if (e2 >= dy) { err += dy; x0 += sx; }
                    if (e2 <= dx) { err += dx; y0 += sy; }
                }
            }

            /// <summary>Polygone plein (pair-impair, au centre des pixels), contour compris.</summary>
            public void FillPolygon(IReadOnlyList<(double X, double Y)> points, Rgb c)
            {
                int minY = Math.Max(0, (int)Math.Floor(points.Min(p => p.Y))), maxY = Math.Min(_h - 1, (int)Math.Ceiling(points.Max(p => p.Y)));
                int minX = Math.Max(0, (int)Math.Floor(points.Min(p => p.X))), maxX = Math.Min(_w - 1, (int)Math.Ceiling(points.Max(p => p.X)));
                for (int y = minY; y <= maxY; y++)
                    for (int x = minX; x <= maxX; x++)
                    {
                        double px = x + 0.5, py = y + 0.5;
                        bool inside = false;
                        for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
                            if ((points[i].Y > py) != (points[j].Y > py)
                                && px < (points[j].X - points[i].X) * (py - points[i].Y) / (points[j].Y - points[i].Y) + points[i].X)
                                inside = !inside;
                        if (inside) Set(x, y, c);
                    }
                for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
                    Line((int)points[j].X, (int)points[j].Y, (int)points[i].X, (int)points[i].Y, c);
            }

            public byte[] ToPng()
            {
                using var png = new MemoryStream();
                png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
                var header = new byte[13];
                BigEndian(header, 0, (uint)_w);
                BigEndian(header, 4, (uint)_h);
                header[8] = 8;   // 8 bits par canal
                header[9] = 2;   // RVB
                Chunk(png, "IHDR", header);
                using var raw = new MemoryStream();
                using (var z = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
                    for (int y = 0; y < _h; y++)
                    {
                        z.WriteByte(0);   // pas de filtre
                        z.Write(_pixels, y * _w * 3, _w * 3);
                    }
                Chunk(png, "IDAT", raw.ToArray());
                Chunk(png, "IEND", Array.Empty<byte>());
                return png.ToArray();
            }

            private static void Chunk(Stream s, string type, byte[] data)
            {
                var length = new byte[4];
                BigEndian(length, 0, (uint)data.Length);
                s.Write(length);
                byte[] typeBytes = Encoding.ASCII.GetBytes(type);
                s.Write(typeBytes);
                s.Write(data);
                uint crc = Crc32(data, Crc32(typeBytes, 0xFFFFFFFFu));   // CRC du type, puis des données
                var c = new byte[4];
                BigEndian(c, 0, crc ^ 0xFFFFFFFFu);
                s.Write(c);
            }

            private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
            {
                uint c = (uint)n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                return c;
            }).ToArray();

            private static uint Crc32(byte[] bytes, uint crc)
            {
                foreach (byte b in bytes) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
                return crc;
            }

            private static void BigEndian(byte[] buffer, int offset, uint value)
            {
                buffer[offset] = (byte)(value >> 24);
                buffer[offset + 1] = (byte)(value >> 16);
                buffer[offset + 2] = (byte)(value >> 8);
                buffer[offset + 3] = (byte)value;
            }
        }
    }
}
