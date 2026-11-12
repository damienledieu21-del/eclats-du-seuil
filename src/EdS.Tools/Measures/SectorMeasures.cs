using System.Diagnostics;
using System.Globalization;
using System.Text;
using EdS.Data;
using EdS.Data.Json;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;

namespace EdS.Tools.Measures
{
    /// <summary>
    /// Sous-lot V2.26 (PLAN_GRANDE_CARTE.md § 2.6 : « à mesurer au V2.26, eds mesures secteurs ») : coût du moteur par tick pour un secteur de
    /// simulation, sur la machine qui lance l'outil. Espace du scénario de référence <c>espace_secteur</c> (320 m, 195 obstacles), un
    /// mannequin au centre (cible inépuisable) et 59 loups (60 combattants) : « répartis » (sur tout le secteur, ceux à moins de 30 m
    /// s'engagent), « engagés » (tous engagés d'emblée, ils contournent le décor pour rejoindre la cible : le pire cas), « calme » (aucun
    /// joueur proche). Chaque cas : moyenne, 95e centile et maximum du temps d'un tick (après 100 ticks de mise en route), octets alloués
    /// par tick sur le fil de la simulation. Cible du plan : un secteur de 60 combattants à moins de 1 ms par tick. Mesure, pas un test :
    /// les chiffres varient avec la machine ; aucun résultat n'entre dans un journal.
    /// </summary>
    public static class SectorMeasures
    {
        public sealed record Result(string Case, int Combatants, int Obstacles, long Ticks, double MeanMs, double P95Ms, double MaxMs, double BytesPerTick);

        public const int Warmup = 100;

        /// <summary>Simulation d'un secteur de 60 combattants dans l'espace du scénario de référence.</summary>
        public static CombatSetup Sector(CombatSetup reference, string kind, long durationMs)
        {
            CombatSpace space = reference.Space ?? throw new ArgumentException("Scénario de référence sans espace.", nameof(reference));
            var wolf = (AssailantSetup)reference.Combatants.First(c => c is AssailantSetup);
            long cx = (space.Min.X.Raw + space.Max.X.Raw) / 2, cz = (space.Min.Z.Raw + space.Max.Z.Raw) / 2;
            var walls = space.Obstacles.Select(o => new ConvexPolygon(o.Points)).ToArray();
            var combatants = new List<CombatantSetup>
            {
                new DummySetup("cible", "joueurs", Fixed.FromInt(50), Fixed.Zero, placement: new CombatantPlacement(Pos(cx, cz), Fixed.FromRatio(1, 2))),
            };
            var behavior = kind == "engages" ? new AssailantBehavior(Fixed.FromRatio(1, 2), null, null) : wolf.Behavior;
            int placed = 0;
            for (int k = 0; placed < 59 && k < 4000; k++)
            {
                long x, z;
                if (kind == "repartis" || kind == "calme")
                {
                    // Grille sur tout l'espace (« calme » : la cible est retirée plus bas, personne ne s'engage).
                    x = space.Min.X.Raw + 120_000 + (k % 24) * 125_000;
                    z = space.Min.Z.Raw + 120_000 + (k / 24) * 125_000;
                }
                else
                {
                    // Anneaux de 15 à 60 m autour de la cible : tous engagés, ils la rejoignent en contournant le décor.
                    int ring = k / 20, slot = k % 20;
                    double angle = 2 * Math.PI * slot / 20 + ring * 0.13, r = 150_000 + ring * 50_000;
                    x = cx + (long)Math.Round(Math.Cos(angle) * r);
                    z = cz + (long)Math.Round(Math.Sin(angle) * r);
                }
                if (x + 6_000 > space.Max.X.Raw || z + 6_000 > space.Max.Z.Raw || Math.Abs(x - cx) < 20_000 && Math.Abs(z - cz) < 20_000) continue;
                if (walls.Any(w => w.OverlapsCircle(x, z, 7_000))) continue;
                if (combatants.Skip(1).Any(c => Planar.CirclesOverlap(x, z, 7_000, c.Placement!.Position.X.Raw, c.Placement.Position.Z.Raw, 7_000))) continue;
                combatants.Add(new AssailantSetup($"loup{placed++}", "meute", wolf.Armor, wolf.MagicResistance, null, wolf.FirstAttackMs, wolf.IntervalMs,
                    wolf.Attacks, 0, wolf.StatusResistance, wolf.Triggers, 1_000_000, new CombatantPlacement(Pos(x, z), wolf.Placement!.Radius, null, wolf.Placement.Speed),
                    null, behavior));
            }
            if (kind == "calme") combatants[0] = new DummySetup("cible", "joueurs", Fixed.FromInt(50), Fixed.Zero,
                placement: new CombatantPlacement(Pos(space.Min.X.Raw + 10_000, space.Min.Z.Raw + 10_000), Fixed.FromRatio(1, 2)));
            return new CombatSetup(reference.Seed, durationMs, reference.Stats, reference.Damage, reference.Progression, reference.Combat, combatants,
                Array.Empty<CombatCommand>(), reference.Data, space, zone: true);
        }

        private static GroundVector Pos(long x, long z) => new GroundVector(Fixed.FromRaw(x), Fixed.FromRaw(z));

        public static Result Measure(string kind, CombatSetup setup, int ticks)
        {
            var engine = new CombatEngine(setup, NullCombatLog.Instance);
            for (int t = 0; t < Warmup && !engine.IsFinished; t++) engine.Step();
            var times = new double[ticks];
            long before = GC.GetAllocatedBytesForCurrentThread();
            var watch = new Stopwatch();
            int done = 0;
            for (; done < ticks && !engine.IsFinished; done++)
            {
                watch.Restart();
                engine.Step();
                times[done] = watch.Elapsed.TotalMilliseconds;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            double[] sorted = times.Take(done).OrderBy(x => x).ToArray();
            return new Result(kind, setup.Combatants.Count, setup.Space!.Obstacles.Count, done, sorted.Average(),
                sorted[(int)Math.Min(sorted.Length - 1, Math.Ceiling(sorted.Length * 0.95) - 1)], sorted[^1], (double)allocated / Math.Max(1, done));
        }

        public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
        {
            if (args.Count == 0 || args[0] != "secteurs")
            {
                error.WriteLine("Usage : eds mesures secteurs [--ticks N] [--data D] [--scenario S]");
                return 1;
            }
            int ticks = 1200;
            string data = Path.Combine("data", "v0"), scenario = Path.Combine("scenarios", "espace_secteur.json");
            for (int i = 1; i < args.Count; i++)
            {
                bool hasValue = i + 1 < args.Count;
                switch (args[i])
                {
                    case "--ticks" when hasValue && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n >= 1 && n <= 72_000:
                        ticks = n;
                        i++;
                        break;
                    case "--data" when hasValue: data = args[++i]; break;
                    case "--scenario" when hasValue: scenario = args[++i]; break;
                    default:
                        error.WriteLine($"Option inconnue ou invalide : {args[i]}");
                        return 1;
                }
            }
            GameDataLoadResult loaded = GameDataLoader.Load(data, builds: false);
            if (loaded.Data is null)
            {
                foreach (var (path, report) in loaded.Failures) error.WriteLine($"{path} : {report}");
                return 1;
            }
            var read = ScenarioJsonReader.ReadFile(scenario, loaded.Data);
            if (read.Setup is null)
            {
                error.WriteLine(read.Report.ToString());
                return 1;
            }
            long durationMs = (long)(ticks + Warmup + 1) * read.Setup.Combat.TickMilliseconds;
            var results = new[] { "repartis", "engages", "calme" }.Select(k => Measure(k, Sector(read.Setup, k, durationMs), ticks)).ToList();
            output.Write(Format(results));
            return 0;
        }

        public static string Format(IReadOnlyList<Result> results)
        {
            var b = new StringBuilder();
            b.AppendLine("Mesures des secteurs (V2.26) — temps du moteur par tick, sur cette machine ; cible : 60 combattants < 1 ms par tick");
            b.AppendLine("| cas | combattants | obstacles | ticks | moyenne (ms) | 95e centile (ms) | maximum (ms) | octets alloués par tick | cible |");
            b.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (Result r in results)
                b.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"| {r.Case} | {r.Combatants} | {r.Obstacles} | {r.Ticks} | {r.MeanMs:0.000} | {r.P95Ms:0.000} | {r.MaxMs:0.000} | {r.BytesPerTick:0} | {(r.MeanMs < 1 ? "atteinte" : "non atteinte")} |"));
            return b.ToString();
        }
    }
}
