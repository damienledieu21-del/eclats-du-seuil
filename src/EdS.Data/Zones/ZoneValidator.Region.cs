using System.Collections.Generic;
using System.Linq;
using EdS.Data.Validation;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.Data.Zones
{
    public static partial class ZoneValidator
    {
        /// <summary>
        /// Sous-lot V2.25.3 (grande carte, cases et repère) : version 12 encore lue (sous-ensemble de la version 13, sans <c>region</c>). Elle
        /// ajoutait la propagation des cicatrices (V2.6.1).
        /// </summary>
        public const int SpreadSchemaVersion = 12;

        /// <summary>
        /// Sous-lot V2.25.3 : bornes d'une zone à région (<c>region</c>, schéma 13). Au total : lieux, portes, routes et points d'une route ;
        /// par case de 128 m : lieux, portes et routes qui la touchent. Une zone sans région garde les bornes d'avant
        /// (<see cref="MaxPlaces"/>, <see cref="MaxDoors"/>, <see cref="MaxRoads"/>, <see cref="MaxRoadPoints"/>).
        /// </summary>
        public const int MaxRegionPlaces = 2048, MaxRegionDoors = 256, MaxRegionRoads = 1024, MaxRegionRoadPoints = 128;

        /// <inheritdoc cref="MaxRegionPlaces"/>
        public const int MaxPlacesPerCell = 64, MaxDoorsPerCell = 16, MaxRoadsPerCell = 32;

        /// <summary>
        /// Sous-lot V2.25.3 (schéma 13) : grille de la région. Coin sud-ouest en mètres entiers, à au plus
        /// <see cref="RegionGrid.MaxOriginMeters"/> m de zéro ; colonnes et rangées paires (des secteurs entiers), de 2 à 64 ; l'aire
        /// praticable et tous les obstacles du scénario dans la région. Null (erreurs au rapport) si elle est refusée.
        /// </summary>
        private static RegionGrid? CheckRegion(RegionData r, CombatSpace? space, ValidationReport report)
        {
            const string path = "$.data.region";
            bool ok = true;
            int x = 0, z = 0;
            if (!CheckPoint(r.Origin, path + ".origin", "coin sud-ouest [x, z] en mètres entiers", report)) ok = false;
            else if (!WholeMeters(r.Origin!.X!.Value, out x) || !WholeMeters(r.Origin.Z!.Value, out z))
            { report.Add(path + ".origin", ValidationCode.InvalidValue, $"coin sud-ouest en mètres entiers, à au plus {RegionGrid.MaxOriginMeters} m de zéro."); ok = false; }
            if (r.Columns is null) { Missing(report, path + ".columns", "colonnes de cases (nombre pair)"); ok = false; }
            else if (!RegionGrid.IsValidCount(r.Columns.Value, RegionGrid.MaxColumns))
            { report.Add(path + ".columns", ValidationCode.OutOfRange, $"{r.Columns} colonnes : nombre pair de {RegionGrid.SectorCells} à {RegionGrid.MaxColumns} attendu (des secteurs entiers)."); ok = false; }
            if (r.Rows is null) { Missing(report, path + ".rows", "rangées de cases (nombre pair)"); ok = false; }
            else if (!RegionGrid.IsValidCount(r.Rows.Value, RegionGrid.MaxRows))
            { report.Add(path + ".rows", ValidationCode.OutOfRange, $"{r.Rows} rangées : nombre pair de {RegionGrid.SectorCells} à {RegionGrid.MaxRows} attendu (des secteurs entiers)."); ok = false; }
            if (!ok) return null;
            var grid = new RegionGrid(x, z, r.Columns!.Value, r.Rows!.Value);
            if (space is null) return grid;
            if (!grid.Contains(space.Min) || !grid.Contains(space.Max))
            {
                report.Add(path, ValidationCode.OutOfRange, $"l'aire praticable de la zone ([{space.Min.X}, {space.Min.Z}] à [{space.Max.X}, {space.Max.Z}]) dépasse la région.");
                return null;
            }
            foreach (SpaceObstacle o in space.Obstacles)
                if (o.Points.Any(p => !grid.Contains(p)))
                {
                    report.Add(path, ValidationCode.OutOfRange, $"l'obstacle « {o.Id} » du scénario dépasse la région.");
                    ok = false;
                }
            return ok ? grid : null;
        }

        private static bool WholeMeters(Fixed value, out int meters)
        {
            meters = 0;
            if (value.Raw % Fixed.Scale != 0) return false;
            long m = value.Raw / Fixed.Scale;
            if (m < -RegionGrid.MaxOriginMeters || m > RegionGrid.MaxOriginMeters) return false;
            meters = (int)m;
            return true;
        }

        /// <summary>
        /// Sous-lot V2.25.3 : au plus <paramref name="max"/> éléments par case. L'erreur porte sur le premier élément (dans l'ordre du fichier)
        /// qui fait déborder une case, avec le nom de la case ; une seule erreur par case.
        /// </summary>
        private static void CheckPerCell(string listPath, string what, int max, IEnumerable<(int Index, IReadOnlyList<RegionCell> Cells)> items, ValidationReport report)
        {
            var counts = new Dictionary<RegionCell, int>();
            var reported = new HashSet<RegionCell>();
            foreach ((int index, IReadOnlyList<RegionCell> cells) in items)
                foreach (RegionCell c in cells)
                {
                    counts.TryGetValue(c, out int n);
                    counts[c] = ++n;
                    if (n > max && reported.Add(c))
                        report.Add($"{listPath}[{index}]", ValidationCode.LimitExceeded, $"case {c.Name} : plus de {max} {what}.");
                }
        }
    }
}
