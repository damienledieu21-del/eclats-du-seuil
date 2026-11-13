using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EdS.Data.Zones;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.27 : un changement de finesse d'une case (anneau 0, 1, ou <see cref="CellStreamer.Far"/> : déchargée).</summary>
    public readonly struct CellChange : IEquatable<CellChange>
    {
        public RegionCell Cell { get; }
        public int From { get; }
        public int To { get; }

        public CellChange(RegionCell cell, int from, int to)
        {
            Cell = cell;
            From = from;
            To = to;
        }

        /// <summary>La case gagne du détail (chargement ou passage à l'anneau 0).</summary>
        public bool Finer => To < From;

        public bool Equals(CellChange other) => Cell == other.Cell && From == other.From && To == other.To;
        public override bool Equals(object? obj) => obj is CellChange c && Equals(c);
        public override int GetHashCode() => (Cell.GetHashCode() * 4 + From) * 4 + To;
        public override string ToString() => Cell.Name + " " + From.ToString(CultureInfo.InvariantCulture) + ">" + To.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Sous-lot V2.27 (docs/PLAN_GRANDE_CARTE.md, § 6.1) : quelles cases de la région charger, à quelle finesse, et dans quel ordre, autour
    /// de la caméra. Sans Unity, sans horloge : la position et le temps d'affichage (secondes) sont donnés à <see cref="Update"/>.
    /// <list type="bullet">
    /// <item>Case d'ancrage avec hystérésis : elle ne change que si la caméra en sort de plus de <see cref="StreamingProfile.HysteresisMeters"/>.</item>
    /// <item>Anneaux autour de l'ancrage (distance de Tchebychev en cases) : 0 (détail), 1 (simplifié), au-delà rien (terrain lointain).</item>
    /// <item>Une case qui perd du détail le garde <see cref="StreamingProfile.UnloadDelaySeconds"/> s (demi-tour sans à-coup), sauf si la
    /// mémoire ou les triangles des maillages dépassent le budget : les plus anciennes rendent alors leur détail d'abord.</item>
    /// <item>Un seul changement à la fois (<see cref="TryNext"/> puis <see cref="Complete"/>), ordre stable : d'abord ce qui gagne du détail
    /// (anneau visé, distance, rangée, colonne), puis ce qui en perd (plus ancien d'abord, puis rangée, colonne).</item>
    /// </list>
    /// Borné : au plus une entrée par case de la région.
    /// </summary>
    public sealed class CellStreamer
    {
        /// <summary>« Anneau » d'une case sans maillage : seul le terrain lointain la couvre.</summary>
        public const int Far = 2;

        private sealed class Slot
        {
            public RegionCell Cell;
            public int Built = Far;
            public int Wanted = Far;
            public double CoarserSince = double.NaN;
            public long Bytes;
            public long Triangles;
        }

        private readonly SortedDictionary<RegionCell, Slot> _slots = new SortedDictionary<RegionCell, Slot>();
        private bool _hasAnchor, _busy;
        private CellChange _pending;
        private double _now;

        public RegionGrid Grid { get; }
        public StreamingProfile Profile { get; }

        /// <summary>Case d'ancrage (valable après le premier <see cref="Update"/>).</summary>
        public RegionCell Anchor { get; private set; }

        /// <summary>Octets des maillages de cases construits (estimation de <see cref="TerrainPatch.EstimateBytes"/>).</summary>
        public long Bytes { get; private set; }

        /// <summary>Triangles des maillages de cases construits (estimation de <see cref="TerrainPatch.EstimateTriangles"/>).</summary>
        public long Triangles { get; private set; }

        /// <summary>Changements d'ancrage depuis le début (mesures, tests d'hystérésis).</summary>
        public int AnchorChanges { get; private set; }

        private readonly long _farTriangles;

        public CellStreamer(RegionGrid grid, StreamingProfile profile)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _farTriangles = RegionTerrain.FarTriangles(grid);
        }

        /// <summary>Le budget de triangles compte aussi le terrain lointain entier (24 576 triangles pour les Vallons), toujours montré.</summary>
        public long TrianglesWithFar => checked(Triangles + _farTriangles);

        /// <summary>Case qui contient (x, z), le point ramené dans la région.</summary>
        public RegionCell CellAt(double x, double z)
        {
            int c = (int)Math.Floor((x - Grid.OriginX) / RegionGrid.CellMeters);
            int r = (int)Math.Floor((z - Grid.OriginZ) / RegionGrid.CellMeters);
            return new RegionCell(Math.Max(0, Math.Min(Grid.Columns - 1, c)), Math.Max(0, Math.Min(Grid.Rows - 1, r)));
        }

        /// <summary>Coin sud-ouest d'une case, en mètres.</summary>
        public (double X, double Z) Corner(RegionCell cell) =>
            (Grid.OriginX + (double)cell.Column * RegionGrid.CellMeters, Grid.OriginZ + (double)cell.Row * RegionGrid.CellMeters);

        /// <summary>Distance de Tchebychev (en cases) à l'ancrage.</summary>
        public int DistanceToAnchor(RegionCell cell) => Math.Max(Math.Abs(cell.Column - Anchor.Column), Math.Abs(cell.Row - Anchor.Row));

        /// <summary>Caméra en (x, z) au temps d'affichage <paramref name="seconds"/> (qui ne recule pas) : ancrage, anneaux voulus, délais.</summary>
        public void Update(double x, double z, double seconds)
        {
            if (double.IsNaN(x) || double.IsNaN(z) || double.IsInfinity(x) || double.IsInfinity(z))
                throw new ArgumentException("Position de caméra non finie.");
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) throw new ArgumentException("Temps non fini.", nameof(seconds));
            _now = Math.Max(_now, seconds);
            if (!_hasAnchor)
            {
                Anchor = CellAt(x, z);
                _hasAnchor = true;
            }
            else
            {
                (double ax, double az) = Corner(Anchor);
                double h = Profile.HysteresisMeters;
                if (x < ax - h || x > ax + RegionGrid.CellMeters + h || z < az - h || z > az + RegionGrid.CellMeters + h)
                {
                    RegionCell next = CellAt(x, z);
                    if (next != Anchor)
                    {
                        Anchor = next;
                        AnchorChanges++;
                    }
                }
            }
            foreach (Slot s in _slots.Values) s.Wanted = Far;
            int radius = Profile.ViewRadius;
            for (int r = Math.Max(0, Anchor.Row - radius); r <= Math.Min(Grid.Rows - 1, Anchor.Row + radius); r++)
                for (int c = Math.Max(0, Anchor.Column - radius); c <= Math.Min(Grid.Columns - 1, Anchor.Column + radius); c++)
                {
                    var cell = new RegionCell(c, r);
                    if (!_slots.TryGetValue(cell, out Slot? slot)) _slots.Add(cell, slot = new Slot { Cell = cell });
                    slot.Wanted = Profile.RingAt(DistanceToAnchor(cell));
                }
            var gone = new List<RegionCell>();
            foreach (Slot s in _slots.Values)
            {
                if (s.Wanted > s.Built)
                {
                    if (double.IsNaN(s.CoarserSince)) s.CoarserSince = _now;
                }
                else s.CoarserSince = double.NaN;
                if (s.Wanted == Far && s.Built == Far && !(_busy && _pending.Cell == s.Cell)) gone.Add(s.Cell);
            }
            foreach (RegionCell cell in gone) _slots.Remove(cell);
        }

        /// <summary>Finesse construite d'une case (<see cref="Far"/> sans maillage).</summary>
        public int Built(RegionCell cell) => _slots.TryGetValue(cell, out Slot? s) ? s.Built : Far;

        /// <summary>Finesse voulue d'une case d'après l'ancrage (<see cref="Far"/> hors de vue).</summary>
        public int Wanted(RegionCell cell) => _slots.TryGetValue(cell, out Slot? s) ? s.Wanted : Far;

        /// <summary>Cases qui ont un maillage, rangées (rangée puis colonne).</summary>
        public IReadOnlyList<RegionCell> Loaded => _slots.Values.Where(s => s.Built != Far).Select(s => s.Cell).ToList();

        /// <summary>Cases suivies (bornées par le nombre de cases de la région).</summary>
        public int Tracked => _slots.Count;

        /// <summary>Toutes les cases voulues ont au moins la finesse voulue (rien ne manque à l'écran).</summary>
        public bool Ready => _slots.Values.All(s => s.Built <= s.Wanted);

        /// <summary>L'anneau 0 voulu est entièrement construit au détail.</summary>
        public bool DetailReady => _slots.Values.All(s => s.Wanted != 0 || s.Built == 0);

        /// <summary>Secteurs de simulation (V2.26, <c>s&lt;col&gt;_&lt;rang&gt;</c>) qui ont au moins une case au détail, dans l'ordre des noms.</summary>
        public IReadOnlyList<string> DetailSectors =>
            _slots.Values.Where(s => s.Built == 0).Select(s => s.Cell.SectorName).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();

        /// <summary>Un changement est-il en cours (pris par <see cref="TryNext"/>, pas encore <see cref="Complete"/>) ?</summary>
        public bool Busy => _busy;

        private int Target(Slot s, bool pressure)
        {
            if (s.Wanted <= s.Built) return s.Wanted;
            bool expired = !double.IsNaN(s.CoarserSince) && _now - s.CoarserSince >= Profile.UnloadDelaySeconds;
            return expired || pressure ? s.Wanted : s.Built;
        }

        /// <summary>
        /// Prochain changement à construire, ou faux si tout est à jour. Un seul à la fois : refusé (exception) tant que le précédent n'est pas
        /// terminé par <see cref="Complete"/> ou <see cref="Cancel"/>.
        /// </summary>
        public bool TryNext(out CellChange change)
        {
            if (_busy) throw new InvalidOperationException("Un changement de case est déjà en cours.");
            change = default;
            Slot? best = null;
            int bestTarget = Far;
            foreach (Slot s in _slots.Values)
            {
                int target = Target(s, false);
                if (target >= s.Built) continue;
                if (best == null || Finer(s, target, best, bestTarget))
                {
                    best = s;
                    bestTarget = target;
                }
            }
            if (best == null)
            {
                bool pressure = Bytes > Profile.MemoryBudgetBytes || checked(Triangles + _farTriangles) > Profile.TerrainTriangleBudget;
                foreach (Slot s in _slots.Values)
                {
                    int target = Target(s, pressure);
                    if (target <= s.Built) continue;
                    if (best == null || s.CoarserSince < best.CoarserSince) // plus ancien d'abord ; à égalité, ordre des cases (déjà trié)
                    {
                        best = s;
                        bestTarget = target;
                    }
                }
            }
            if (best == null) return false;
            change = new CellChange(best.Cell, best.Built, bestTarget);
            _pending = change;
            _busy = true;
            return true;
        }

        private bool Finer(Slot a, int ta, Slot b, int tb)
        {
            if (ta != tb) return ta < tb;
            int da = DistanceToAnchor(a.Cell), db = DistanceToAnchor(b.Cell);
            if (da != db) return da < db;
            return a.Cell.CompareTo(b.Cell) < 0;
        }

        /// <summary>Le changement pris est construit et montré : la case a maintenant la finesse <see cref="CellChange.To"/>.</summary>
        public void Complete(CellChange change)
        {
            if (!_busy || !_pending.Equals(change)) throw new InvalidOperationException("Ce changement de case n'est pas celui en cours.");
            // Tout est calculé avant la moindre mutation : une exception ici ne laisse rien à moitié changé (le changement reste en cours).
            _slots.TryGetValue(change.Cell, out Slot? s);
            long oldBytes = s?.Bytes ?? 0, oldTriangles = s?.Triangles ?? 0;
            long bytes = change.To == Far ? 0 : TerrainPatch.EstimateBytes(Profile.ResolutionOf(change.To));
            long triangles = change.To == Far ? 0 : TerrainPatch.EstimateTriangles(Profile.ResolutionOf(change.To));
            long newBytes = checked(Bytes - oldBytes + bytes), newTriangles = checked(Triangles - oldTriangles + triangles);
            if (s == null) _slots.Add(change.Cell, s = new Slot { Cell = change.Cell });
            _busy = false;
            Bytes = newBytes;
            Triangles = newTriangles;
            s.Bytes = bytes;
            s.Triangles = triangles;
            s.Built = change.To;
            s.CoarserSince = s.Wanted > s.Built ? _now : double.NaN;
            if (s.Wanted == Far && s.Built == Far) _slots.Remove(change.Cell);
        }

        /// <summary>Abandonne le changement pris (rien ne change).</summary>
        public void Cancel()
        {
            _busy = false;
        }
    }
}
