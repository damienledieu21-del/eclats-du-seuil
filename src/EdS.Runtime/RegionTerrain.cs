using System;
using System.Collections.Generic;
using EdS.Data.Zones;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.27 : ce qui montre les morceaux de terrain (Unity : <c>CellStreamView</c> ; tests : un enregistreur). Publication
    /// <b>en deux temps</b> (revue Codex de la PR #154) : <see cref="StageCell"/> et <see cref="StageFarBlock"/> <b>préparent</b> sans rien
    /// changer à l'écran (ils peuvent échouer) ; <see cref="Publish"/> montre d'un coup tout ce qui est préparé et <b>ne doit pas échouer</b>
    /// (échange de maillages déjà remplis) ; <see cref="Discard"/> oublie ce qui est préparé (après un échec). Les tableaux d'un morceau
    /// (<see cref="TerrainPatch.Positions"/>…) ne valent que pendant l'appel de préparation : ils sont recyclés ensuite.
    /// </summary>
    public interface ITerrainSink
    {
        /// <summary>Prépare la case à l'anneau <paramref name="ring"/> avec ce maillage, ou sa disparition si <paramref name="patch"/> est null.</summary>
        void StageCell(RegionCell cell, int ring, TerrainPatch? patch);

        /// <summary>Prépare le nouveau maillage du bloc lointain (<paramref name="column"/>, <paramref name="row"/>).</summary>
        void StageFarBlock(int column, int row, TerrainPatch patch);

        /// <summary>Montre d'un coup tout ce qui est préparé. Ne doit pas échouer.</summary>
        void Publish();

        /// <summary>Oublie tout ce qui est préparé, sans rien changer à l'écran. Ne doit pas échouer.</summary>
        void Discard();
    }

    /// <summary>
    /// Sous-lot V2.27 (docs/PLAN_GRANDE_CARTE.md, § 6.1) : le terrain affiché de toute une région, chargé par cases autour de la caméra.
    /// <list type="bullet">
    /// <item><b>Terrain lointain</b> : toute la région en maille de <see cref="StreamingProfile.FarStep"/> m, en blocs de
    /// <see cref="StreamingProfile.FarBlockCells"/> × <see cref="StreamingProfile.FarBlockCells"/> cases ; un bloc a un trou (avec jupe) sous
    /// chaque case qui a son propre maillage.</item>
    /// <item><b>Cases</b> : choisies par <see cref="CellStreamer"/> ; un changement construit le maillage de la case puis celui de son bloc
    /// lointain, et les montre <b>ensemble</b> (<see cref="ITerrainSink"/> : préparés, puis publiés d'un seul appel) : jamais de trou ni de double sol à l'écran.</item>
    /// <item><b>Construction étalée</b> : <see cref="Work"/> fait des pas (une rangée de hauteurs) tant que le budget de l'image n'est pas
    /// épuisé, et au moins un pas par image (le chargement avance toujours).</item>
    /// </list>
    /// Hauteurs : une seule fonction (<see cref="GroundRelief.Height"/> du relief en cours, V2.24). Sans horloge (le budget est une fonction
    /// donnée par l'appelant), sans Unity.
    /// </summary>
    public sealed class RegionTerrain
    {
        /// <summary>Le changement en cours (un seul objet, réutilisé : aucune allocation par case).</summary>
        private sealed class Job
        {
            public bool Active;
            public bool HasChange;
            public CellChange Change;
            public bool NeedsFar;
            public int BlockColumn, BlockRow;
            public TerrainPatch? Cell;
            public TerrainPatch? Far;

            /// <summary>Morceaux construits : il ne reste qu'à les montrer.</summary>
            public bool Ready => (Cell == null || Cell.Done) && (!NeedsFar || (Far != null && Far.Done));
        }

        private readonly Job _job = new Job();
        private readonly Func<int, int, bool> _farKeep;
        private int _farC0, _farR0;
        private readonly Func<double, double, double> _height;
        private readonly (double X0, double Z0, double X1, double Z1)? _covered;
        private readonly Queue<(int Column, int Row)> _farQueue = new Queue<(int, int)>();
        private readonly Dictionary<(int, int), Stack<TerrainBuffers>> _pool = new Dictionary<(int, int), Stack<TerrainBuffers>>();

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
            _farKeep = FarKeep;
            _height = height ?? throw new ArgumentNullException(nameof(height));
            if (covered is { } c && (!(c.X1 > c.X0) || !(c.Z1 > c.Z0))) throw new ArgumentException("Rectangle couvert vide.", nameof(covered));
            _covered = covered;
            FarColumns = (grid.Columns + StreamingProfile.FarBlockCells - 1) / StreamingProfile.FarBlockCells;
            FarRows = (grid.Rows + StreamingProfile.FarBlockCells - 1) / StreamingProfile.FarBlockCells;
            for (int r = 0; r < FarRows; r++)
                for (int k = 0; k < FarColumns; k++) _farQueue.Enqueue((k, r));
        }

        /// <summary>Rien à construire : terrain lointain fait, toutes les cases voulues à jour.</summary>
        public bool Idle => !_job.Active && _farQueue.Count == 0 && Streamer.Ready && !HasChange();

        private bool HasChange()
        {
            if (!Streamer.TryNext(out _)) return false;
            Streamer.Cancel();
            return true;
        }

        /// <summary>Tout le terrain lointain est montré.</summary>
        public bool FarReady => _farQueue.Count == 0 && (!_job.Active || _job.HasChange);

        /// <summary>Un changement est en cours de construction.</summary>
        public bool Busy => _job.Active;

        public void Update(double x, double z, double seconds) => Streamer.Update(x, z, seconds);

        /// <summary>
        /// Construit pendant une image : des pas tant que <paramref name="overBudget"/> est faux, au moins un s'il y a du travail. Rend le
        /// nombre de pas faits. Montrer un changement (copie des maillages par le <paramref name="sink"/>, case et bloc lointain ensemble)
        /// compte comme un pas, et ne se fait qu'<b>en premier pas d'une image</b> : son coût n'est jamais ajouté à des pas déjà faits.
        /// Une exception (hauteur non finie, montreur) abandonne proprement le changement en cours (<see cref="Cancel"/>) avant de remonter.
        /// </summary>
        public int Work(Func<bool> overBudget, ITerrainSink sink)
        {
            if (overBudget == null) throw new ArgumentNullException(nameof(overBudget));
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            int steps = 0;
            try
            {
                do
                {
                    if (!_job.Active && !Start()) break;
                    if (_job.Ready)
                    {
                        if (steps > 0) break;   // montré au début de l'image suivante, avec tout son budget
                        Commit(sink);
                    }
                    else if (_job.Cell != null && !_job.Cell.Done) _job.Cell.StepOnce();
                    else
                    {
                        _job.Far ??= FarPatch(_job.BlockColumn, _job.BlockRow);
                        _job.Far.StepOnce();
                    }
                    steps++;
                    Steps++;
                }
                while (!overBudget());
            }
            catch
            {
                Cancel();
                throw;
            }
            return steps;
        }

        /// <summary>
        /// Montre le changement : case et bloc lointain d'abord <b>préparés</b> (un échec ici est oublié par <see cref="ITerrainSink.Discard"/> :
        /// l'écran, les blocs et le <see cref="Streamer"/> restent dans leur état d'avant, et <see cref="Work"/> abandonne le changement, qui
        /// sera redemandé), puis <b>publiés</b> d'un seul appel, enfin enregistrés dans le <see cref="Streamer"/>.
        /// </summary>
        private void Commit(ITerrainSink sink)
        {
            try
            {
                if (_job.HasChange) sink.StageCell(_job.Change.Cell, _job.Change.To, _job.Cell);
                if (_job.NeedsFar) sink.StageFarBlock(_job.BlockColumn, _job.BlockRow, _job.Far!);
            }
            catch
            {
                sink.Discard();
                throw;
            }
            sink.Publish();
            if (_job.HasChange)
            {
                Streamer.Complete(_job.Change);
                Commits++;
            }
            if (_job.NeedsFar) Commits++;
            // Recyclage : le montreur a copié les tableaux pendant la préparation ; ils servent au morceau suivant de même taille.
            Recycle(_job.Cell);
            Recycle(_job.Far);
            Clear();
        }

        private void Clear()
        {
            _job.Active = false;
            _job.HasChange = false;
            _job.NeedsFar = false;
            _job.Cell = null;
            _job.Far = null;
        }

        /// <summary>
        /// Abandonne le changement en cours : rien n'est montré, le <see cref="Streamer"/> reprend son état d'avant (le changement sera
        /// redemandé), un bloc lointain du premier chargement est remis dans la file. Les tableaux du morceau abandonné ne sont pas recyclés.
        /// </summary>
        public void Cancel()
        {
            if (!_job.Active) return;
            if (_job.HasChange) Streamer.Cancel();
            else _farQueue.Enqueue((_job.BlockColumn, _job.BlockRow));
            Clear();
        }

        /// <summary>
        /// Construit tout ce qui manque sans budget de temps (chargement d'une zone, écran de chargement), en au plus
        /// <paramref name="maxSteps"/> pas (au moins un). Rend le nombre de pas faits.
        /// </summary>
        public int Warm(ITerrainSink sink, int maxSteps = 1_000_000)
        {
            if (maxSteps < 1) throw new ArgumentOutOfRangeException(nameof(maxSteps));
            int steps = 0;
            while (steps < maxSteps && !Idle)
            {
                int left = maxSteps - steps, n = 0;
                steps += Work(() => ++n >= left, sink);
            }
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
                _job.Active = true;
                _job.NeedsFar = true;
                _job.BlockColumn = c;
                _job.BlockRow = r;
                return true;
            }
            if (!Streamer.TryNext(out CellChange change)) return false;
            (double x0, double z0) = Streamer.Corner(change.Cell);
            if (change.To != CellStreamer.Far)
            {
                int quads = Profile.ResolutionOf(change.To) - 1;
                _job.Cell = new TerrainPatch(x0, z0, (double)RegionGrid.CellMeters / quads, quads, quads, _height, null, _covered, Profile.SkirtMeters,
                    Buffers(quads, quads));
            }
            _job.Active = true;
            _job.HasChange = true;
            _job.Change = change;
            // Le trou du bloc lointain ne change que si la case gagne ou perd son maillage (pas d'un anneau à l'autre).
            _job.NeedsFar = (change.From == CellStreamer.Far) != (change.To == CellStreamer.Far);
            _job.BlockColumn = change.Cell.Column / StreamingProfile.FarBlockCells;
            _job.BlockRow = change.Cell.Row / StreamingProfile.FarBlockCells;
            return true;
        }

        private const int FarPer = RegionGrid.CellMeters / StreamingProfile.FarStep;

        /// <summary>Le quadrilatère (i, j) du bloc lointain en préparation est-il fait ? (sa case n'a pas de maillage ; celle du changement : d'après sa finesse visée)</summary>
        private bool FarKeep(int i, int j)
        {
            var cell = new RegionCell(_farC0 + i / FarPer, _farR0 + j / FarPer);
            int built = _job.HasChange && _job.Change.Cell == cell ? _job.Change.To : Streamer.Built(cell);
            return built == CellStreamer.Far;
        }

        /// <summary>Morceau du bloc lointain, avec un trou sous chaque case qui a son maillage. Masque lu une fois, pendant ce changement.</summary>
        private TerrainPatch FarPatch(int column, int row)
        {
            _farC0 = column * StreamingProfile.FarBlockCells;
            _farR0 = row * StreamingProfile.FarBlockCells;
            int cells = Math.Min(StreamingProfile.FarBlockCells, Grid.Columns - _farC0), rows = Math.Min(StreamingProfile.FarBlockCells, Grid.Rows - _farR0);
            (double x0, double z0) = Streamer.Corner(new RegionCell(_farC0, _farR0));
            return new TerrainPatch(x0, z0, StreamingProfile.FarStep, cells * FarPer, rows * FarPer, _height, _farKeep, _covered, Profile.SkirtMeters,
                Buffers(cells * FarPer, rows * FarPer));
        }

        /// <summary>Triangles du terrain lointain entier (sans trou) : <c>2 × (colonnes × 4) × (rangées × 4)</c>, 24 576 pour les Vallons.</summary>
        public static long FarTriangles(RegionGrid grid) => 2L * grid.Columns * FarPer * grid.Rows * FarPer;
    }
}
