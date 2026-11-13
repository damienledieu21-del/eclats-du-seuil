using System;
using System.Collections.Generic;
using EdS.Data.Zones;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.27 : ce qui montre les morceaux de terrain (Unity : <c>CellStreamView</c> ; tests : un enregistreur). Les tableaux d'un
    /// morceau (<see cref="TerrainPatch.Positions"/>…) ne valent que pendant l'appel : ils sont recyclés juste après.
    /// </summary>
    public interface ITerrainSink
    {
        /// <summary>Montre la case à l'anneau <paramref name="ring"/> avec ce maillage, ou la cache (et recycle son objet) si <paramref name="patch"/> est null.</summary>
        void ShowCell(RegionCell cell, int ring, TerrainPatch? patch);

        /// <summary>Remplace le maillage du bloc lointain (<paramref name="column"/>, <paramref name="row"/>).</summary>
        void ShowFarBlock(int column, int row, TerrainPatch patch);
    }

    /// <summary>
    /// Sous-lot V2.27 (docs/PLAN_GRANDE_CARTE.md, § 6.1) : le terrain affiché de toute une région, chargé par cases autour de la caméra.
    /// <list type="bullet">
    /// <item><b>Terrain lointain</b> : toute la région en maille de <see cref="StreamingProfile.FarStep"/> m, en blocs de
    /// <see cref="StreamingProfile.FarBlockCells"/> × <see cref="StreamingProfile.FarBlockCells"/> cases ; un bloc a un trou (avec jupe) sous
    /// chaque case qui a son propre maillage.</item>
    /// <item><b>Cases</b> : choisies par <see cref="CellStreamer"/> ; un changement construit le maillage de la case puis celui de son bloc
    /// lointain, et les montre <b>ensemble</b> (<see cref="ITerrainSink"/>, au même appel) : jamais de trou ni de double sol à l'écran.</item>
    /// <item><b>Construction étalée</b> : <see cref="Work"/> fait des pas (une rangée de hauteurs) tant que le budget de l'image n'est pas
    /// épuisé, et au moins un pas par image (le chargement avance toujours).</item>
    /// </list>
    /// Hauteurs : une seule fonction (<see cref="GroundRelief.Height"/> du relief en cours, V2.24). Sans horloge (le budget est une fonction
    /// donnée par l'appelant), sans Unity.
    /// </summary>
    public sealed class RegionTerrain
    {
        private sealed class Job
        {
            public CellChange? Change;
            public int BlockColumn, BlockRow;
            public TerrainPatch? Cell;
            public TerrainPatch? Far;
        }

        private readonly Func<double, double, double> _height;
        private readonly (double X0, double Z0, double X1, double Z1)? _covered;
        private readonly Queue<(int Column, int Row)> _farQueue = new Queue<(int, int)>();
        private readonly Dictionary<(int, int), Stack<TerrainBuffers>> _pool = new Dictionary<(int, int), Stack<TerrainBuffers>>();
        private Job? _job;

        public CellStreamer Streamer { get; }
        public StreamingProfile Profile => Streamer.Profile;
        public RegionGrid Grid => Streamer.Grid;
        public int FarColumns { get; }
        public int FarRows { get; }

        /// <summary>Changements montrés (cases et blocs lointains).</summary>
        public int Commits { get; private set; }

        /// <summary>Pas de construction faits depuis le début.</summary>
        public long Steps { get; private set; }

        /// <param name="covered">Rectangle déjà couvert par le terrain peint de la zone (aire et sa marge), ou null.</param>
        public RegionTerrain(RegionGrid grid, StreamingProfile profile, Func<double, double, double> height,
            (double X0, double Z0, double X1, double Z1)? covered = null)
        {
            Streamer = new CellStreamer(grid, profile);
            _height = height ?? throw new ArgumentNullException(nameof(height));
            if (covered is { } c && (!(c.X1 > c.X0) || !(c.Z1 > c.Z0))) throw new ArgumentException("Rectangle couvert vide.", nameof(covered));
            _covered = covered;
            FarColumns = (grid.Columns + StreamingProfile.FarBlockCells - 1) / StreamingProfile.FarBlockCells;
            FarRows = (grid.Rows + StreamingProfile.FarBlockCells - 1) / StreamingProfile.FarBlockCells;
            for (int r = 0; r < FarRows; r++)
                for (int k = 0; k < FarColumns; k++) _farQueue.Enqueue((k, r));
        }

        /// <summary>Rien à construire : terrain lointain fait, toutes les cases voulues à jour.</summary>
        public bool Idle => _job == null && _farQueue.Count == 0 && Streamer.Ready && !HasChange();

        private bool HasChange()
        {
            if (!Streamer.TryNext(out _)) return false;
            Streamer.Cancel();
            return true;
        }

        /// <summary>Tout le terrain lointain est montré.</summary>
        public bool FarReady => _farQueue.Count == 0 && (_job == null || _job.Change != null);

        public void Update(double x, double z, double seconds) => Streamer.Update(x, z, seconds);

        /// <summary>
        /// Construit pendant une image : des pas tant que <paramref name="overBudget"/> est faux, au moins un s'il y a du travail. Rend le
        /// nombre de pas faits.
        /// </summary>
        public int Work(Func<bool> overBudget, ITerrainSink sink)
        {
            if (overBudget == null) throw new ArgumentNullException(nameof(overBudget));
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            int steps = 0;
            do
            {
                if (_job == null && !Start()) break;
                Job job = _job!;
                bool done;
                if (job.Cell != null && !job.Cell.Done)
                {
                    job.Cell.StepOnce();
                    done = false;
                }
                else
                {
                    job.Far ??= FarPatch(job.BlockColumn, job.BlockRow, job.Change);
                    done = job.Far.StepOnce();
                }
                steps++;
                Steps++;
                if (done)
                {
                    _job = null;
                    if (job.Change is { } change)
                    {
                        sink.ShowCell(change.Cell, change.To, job.Cell);
                        Streamer.Complete(change);
                        Commits++;
                    }
                    sink.ShowFarBlock(job.BlockColumn, job.BlockRow, job.Far!);
                    Commits++;
                    // Recyclage : le montreur a copié les tableaux pendant son appel ; ils servent au morceau suivant de même taille.
                    Recycle(job.Cell);
                    Recycle(job.Far!);
                }
            }
            while (!overBudget());
            return steps;
        }

        /// <summary>Construit tout ce qui manque sans budget (chargement d'une zone, écran de chargement), au plus <paramref name="maxSteps"/> pas.</summary>
        public int Warm(ITerrainSink sink, int maxSteps = 1_000_000)
        {
            int steps = 0;
            while (steps < maxSteps && !Idle) steps += Work(() => steps >= maxSteps, sink);
            return steps;
        }

        private TerrainBuffers? Buffers(int quadsX, int quadsZ) =>
            _pool.TryGetValue((quadsX, quadsZ), out Stack<TerrainBuffers>? stack) && stack.Count > 0 ? stack.Pop() : null;

        private void Recycle(TerrainPatch? patch)
        {
            if (patch == null) return;
            var key = (patch.QuadsX, patch.QuadsZ);
            if (!_pool.TryGetValue(key, out Stack<TerrainBuffers>? stack)) _pool.Add(key, stack = new Stack<TerrainBuffers>());
            TerrainBuffers b = patch.Release();
            if (stack.Count < 2) stack.Push(b);   // borné : deux jeux par taille au plus
        }

        private bool Start()
        {
            if (_farQueue.Count > 0)
            {
                (int c, int r) = _farQueue.Dequeue();
                _job = new Job { BlockColumn = c, BlockRow = r };
                return true;
            }
            if (!Streamer.TryNext(out CellChange change)) return false;
            (double x0, double z0) = Streamer.Corner(change.Cell);
            TerrainPatch? cell = null;
            if (change.To != CellStreamer.Far)
            {
                int quads = Profile.ResolutionOf(change.To) - 1;
                cell = new TerrainPatch(x0, z0, (double)RegionGrid.CellMeters / quads, quads, quads, _height, null, _covered, Profile.SkirtMeters,
                    Buffers(quads, quads));
            }
            _job = new Job
            {
                Change = change,
                Cell = cell,
                BlockColumn = change.Cell.Column / StreamingProfile.FarBlockCells,
                BlockRow = change.Cell.Row / StreamingProfile.FarBlockCells,
            };
            return true;
        }

        /// <summary>Morceau du bloc lointain, avec un trou sous chaque case qui a son maillage (celle du changement : d'après sa finesse visée).</summary>
        private TerrainPatch FarPatch(int column, int row, CellChange? change)
        {
            const int per = RegionGrid.CellMeters / StreamingProfile.FarStep;
            int c0 = column * StreamingProfile.FarBlockCells, r0 = row * StreamingProfile.FarBlockCells;
            int cells = Math.Min(StreamingProfile.FarBlockCells, Grid.Columns - c0), rows = Math.Min(StreamingProfile.FarBlockCells, Grid.Rows - r0);
            bool Keep(int i, int j)
            {
                var cell = new RegionCell(c0 + i / per, r0 + j / per);
                int built = change is { } ch && ch.Cell == cell ? ch.To : Streamer.Built(cell);
                return built == CellStreamer.Far;
            }
            (double x0, double z0) = Streamer.Corner(new RegionCell(c0, r0));
            return new TerrainPatch(x0, z0, StreamingProfile.FarStep, cells * per, rows * per, _height, Keep, _covered, Profile.SkirtMeters,
                Buffers(cells * per, rows * per));
        }
    }
}
