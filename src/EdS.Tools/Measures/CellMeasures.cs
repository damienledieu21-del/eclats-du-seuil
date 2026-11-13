using System.Diagnostics;
using System.Globalization;
using System.Text;
using EdS.Data.Zones;
using EdS.Runtime;

namespace EdS.Tools.Measures
{
    /// <summary>
    /// Sous-lot V2.27 (PLAN_GRANDE_CARTE.md, § 6.2 : « cibles à mesurer ») : coût du chargement par cases du terrain de la région, sur la
    /// machine qui lance l'outil, avec le vrai relief des Vallons (V2.24) et les réglages de chaque classe de machine. Une chevauchée part
    /// du cœur vers le nord-est (plusieurs bords de case et de secteur) à 5, 13 et 20 m/s, 30 images par seconde ; chaque image donne
    /// au plus <see cref="StreamingProfile.BudgetMs"/> ms à la construction (au moins un pas), copie des maillages comprise. Chiffres : chargement initial, temps par
    /// image (moyenne, 95e centile, maximum), images au-delà du budget de plus de 0,5 ms, images sans anneau 0 complet, mémoire et
    /// triangles des maillages montrés, appels de dessin du terrain. Mesure, pas un test : les chiffres dépendent de la machine (le
    /// processeur d'un PC de développement, ni un Note8 ni la carte graphique).
    /// </summary>
    public static class CellMeasures
    {
        public sealed record Result(string Tier, double Speed, double WarmMs, int Frames, double MeanMs, double P95Ms, double MaxMs, int Over,
            int NotReady, long MaxBytes, long MaxTriangles, int MaxDrawCalls, int AnchorChanges, double HeightMicros);

        private sealed class Counter : ITerrainSink
        {
            private readonly Dictionary<RegionCell, (long Bytes, int Triangles)> _cells = new();
            private readonly Dictionary<(int, int), (long Bytes, int Triangles)> _far = new();

            private float[] _copy = Array.Empty<float>();
            private int[] _indices = Array.Empty<int>();

            public void ShowCell(RegionCell cell, int ring, TerrainPatch? patch)
            {
                if (patch == null) _cells.Remove(cell);
                else
                {
                    Fill(patch);
                    _cells[cell] = (patch.Bytes, patch.TriangleCount);
                }
            }

            public void ShowFarBlock(int column, int row, TerrainPatch patch)
            {
                Fill(patch);
                _far[(column, row)] = (patch.Bytes, patch.TriangleCount);
            }

            /// <summary>
            /// Relecture indépendante de la PR #154 : le coût de la copie que fait Unity (CellStreamView.Fill : sommets, normales et coordonnées
            /// de texture vers ses tableaux, index) est compté dans le temps de l'image, comme dans le jeu (sans l'envoi à la carte graphique).
            /// </summary>
            private void Fill(TerrainPatch patch)
            {
                int n = patch.VertexCount;
                if (_copy.Length < n * 8) _copy = new float[n * 8];
                if (_indices.Length < patch.IndexCount) _indices = new int[patch.IndexCount];
                float[] p = patch.Positions, q = patch.Normals, t = patch.Uvs;
                for (int i = 0; i < n; i++)
                {
                    _copy[8 * i] = p[3 * i];
                    _copy[8 * i + 1] = p[3 * i + 1];
                    _copy[8 * i + 2] = p[3 * i + 2];
                    _copy[8 * i + 3] = q[3 * i];
                    _copy[8 * i + 4] = q[3 * i + 1];
                    _copy[8 * i + 5] = q[3 * i + 2];
                    _copy[8 * i + 6] = t[2 * i];
                    _copy[8 * i + 7] = t[2 * i + 1];
                }
                Array.Copy(patch.Indices, _indices, patch.IndexCount);
            }

            public long Bytes => _cells.Values.Concat(_far.Values).Sum(p => p.Bytes);
            public long Triangles => _cells.Values.Concat(_far.Values).Sum(p => (long)p.Triangles);
            public int DrawCalls => _cells.Values.Concat(_far.Values).Count(p => p.Triangles > 0);
        }

        public static Result Measure(RegionGrid grid, GroundRelief relief, (double, double, double, double) covered, DeviceTier tier, double speed, double seconds)
        {
            StreamingProfile profile = StreamingProfile.For(tier);
            Func<double, double, double> height = (x, z) => relief.Height(x, z);
            var watch = Stopwatch.StartNew();
            const int samples = 20_000;
            double sink = 0;
            for (int k = 0; k < samples; k++) sink += height(-1800 + k * 0.19, -1500 + k * 0.13);
            double micros = watch.Elapsed.TotalMilliseconds * 1000 / samples + (sink == double.MaxValue ? 1 : 0);
            var terrain = new RegionTerrain(grid, profile, height, covered);
            var counter = new Counter();
            const double x0 = 20, z0 = 10, fps = 30;
            terrain.Update(x0, z0, 0);
            watch.Restart();
            terrain.Warm(counter);
            double warm = watch.Elapsed.TotalMilliseconds;
            var times = new List<double>();
            int over = 0, notReady = 0, maxDraw = 0;
            long maxBytes = 0, maxTriangles = 0;
            for (int f = 0; f < seconds * fps; f++)
            {
                double t = f / fps;
                terrain.Update(x0 + speed * t * 0.8, z0 + speed * t * 0.6, t);
                watch.Restart();
                terrain.Work(() => watch.Elapsed.TotalMilliseconds >= profile.BudgetMs, counter);
                double ms = watch.Elapsed.TotalMilliseconds;
                times.Add(ms);
                if (ms > profile.BudgetMs + 0.5) over++;
                if (!terrain.Streamer.DetailReady) notReady++;
                maxBytes = Math.Max(maxBytes, counter.Bytes);
                maxTriangles = Math.Max(maxTriangles, counter.Triangles);
                maxDraw = Math.Max(maxDraw, counter.DrawCalls);
            }
            double[] sorted = times.OrderBy(x => x).ToArray();
            return new Result(tier.ToString(), speed, warm, sorted.Length, sorted.Average(), sorted[(int)Math.Ceiling(sorted.Length * 0.95) - 1], sorted[^1],
                over, notReady, maxBytes, maxTriangles, maxDraw, terrain.Streamer.AnchorChanges, micros);
        }

        public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
        {
            if (args.Count == 0 || args[0] != "cases")
            {
                error.WriteLine("Usage : eds mesures cases [--secondes N] [--data D] [--scenarios S]");
                return 1;
            }
            double seconds = 40;
            string data = Path.Combine("data", "v0"), scenarios = "scenarios";
            for (int i = 1; i < args.Count; i++)
            {
                bool hasValue = i + 1 < args.Count;
                switch (args[i])
                {
                    case "--secondes" when hasValue && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n >= 1 && n <= 120:
                        seconds = n;
                        i++;
                        break;
                    case "--data" when hasValue: data = args[++i]; break;
                    case "--scenarios" when hasValue: scenarios = args[++i]; break;
                    default:
                        error.WriteLine($"Option inconnue ou invalide : {args[i]}");
                        return 1;
                }
            }
            string folder = Path.Combine(Path.GetTempPath(), "eds_mesures_cases_" + Guid.NewGuid().ToString("N"));
            try
            {
                AdventureSession session = AdventureSession.Load(data, scenarios, Path.Combine(folder, "partie.json"));
                session.Advance(0.05);
                AdventureMap map = session.Map;
                if (map.Region is null)
                {
                    error.WriteLine("La zone de départ n'a pas de région (schéma de zone 13).");
                    return 1;
                }
                GroundRelief relief = GroundRelief.For(map);
                // Terrain peint de la zone : l'aire et la marge de forêt par défaut du catalogue (45 m).
                var covered = (map.MinX - 45, map.MinZ - 45, map.MaxX + 45, map.MaxZ + 45);
                var results = new List<Result>();
                foreach (DeviceTier tier in new[] { DeviceTier.Phone, DeviceTier.Modest, DeviceTier.Full })
                    foreach (double speed in new[] { 5.0, 13.0, 20.0 })
                        results.Add(Measure(map.Region, relief, covered, tier, speed, seconds));
                output.Write(Format(results));
                return 0;
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        public static string Format(IReadOnlyList<Result> results)
        {
            var b = new StringBuilder();
            b.AppendLine("Mesures du chargement par cases (V2.27) — terrain de la région, processeur de cette machine ; budget : 2 ms par image");
            b.AppendLine("| machine | vitesse (m/s) | hauteur (µs) | chargement (ms) | images | moyenne (ms) | 95e centile (ms) | maximum (ms) | > 2,5 ms | sans anneau 0 complet | mémoire (Mo) | triangles | appels de dessin | changements d'ancrage |");
            b.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (Result r in results)
                b.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"| {r.Tier} | {r.Speed:0} | {r.HeightMicros:0.00} | {r.WarmMs:0} | {r.Frames} | {r.MeanMs:0.000} | {r.P95Ms:0.000} | {r.MaxMs:0.000} | {r.Over} | {r.NotReady} | {r.MaxBytes / 1048576.0:0.0} | {r.MaxTriangles} | {r.MaxDrawCalls} | {r.AnchorChanges} |"));
            return b.ToString();
        }
    }
}
