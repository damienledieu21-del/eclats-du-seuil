using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using EdS.Simulation.Tests.Presence;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Space.SpaceTestKit;

namespace EdS.Simulation.Tests.Behavior;

/// <summary>
/// Sous-lot 11.5 : propriétés des comportements sur des combats positionnés aléatoires (tirage fixe) : un ennemi ne bouge pas avant de
/// s'engager et ne s'engage qu'une fois ; il ne bouge pas pendant l'annonce d'une de ses attaques ; une charge s'arrête dans l'aire et hors
/// du décor ; le même combat donne le même journal ; un instantané pris en cours de route continue à l'identique (réserves de budget
/// vérifiées exactes en Debug).
/// </summary>
[Trait("Category", "Behavior")]
public class Lot115BehaviorPropertyTests
{
    private static readonly CombatRules Rules = new() { GuardUpkeepPerSecond = 0, TelegraphMinReactionMs = 0, GlobalCooldownMs = 50 };

    private static Fixed Between(Random random, int minRaw, int maxRaw) => Fixed.FromRaw(random.Next(minRaw, maxRaw + 1));

    private static GroundVector Direction(Random random)
    {
        while (true)
        {
            var v = new GroundVector(Between(random, -10_000, 10_000), Between(random, -10_000, 10_000));
            if (!v.IsZero) return v;
        }
    }

    private static AssailantAttack RandomAttack(Random random, int k) => random.Next(4) switch
    {
        0 => new AssailantAttack($"a{k}", 10, applies: null, shape: AttackShape.Target, range: Between(random, 5_000, 30_000)),
        1 => new AssailantAttack($"a{k}", 10, shape: AttackShape.Circle, telegraphMs: 50L * random.Next(12, 30), radius: Between(random, 5_000, 20_000)),
        2 => new AssailantAttack($"a{k}", 10, shape: AttackShape.Line, telegraphMs: 50L * random.Next(12, 30), range: Between(random, 20_000, 80_000),
            width: Between(random, 5_000, 15_000), charge: true),
        _ => new AssailantAttack($"a{k}", 10),
    };

    private sealed record Wall(double MinX, double MinZ, double MaxX, double MaxZ);

    private static (CombatSetup Setup, List<Wall> Walls) RandomCombat(Random random)
    {
        var walls = new List<Wall>();
        var obstacles = new List<SpaceObstacle>();
        for (int i = 0, n = random.Next(0, 3); i < n; i++)
        {
            long x = random.Next(-60_000, 60_001), z = random.Next(-60_000, 60_001), w = random.Next(5_000, 20_001), h = random.Next(5_000, 20_001);
            obstacles.Add(new SpaceObstacle($"mur{i}", new[] { GroundVector.FromRaw(x, z), GroundVector.FromRaw(x + w, z), GroundVector.FromRaw(x + w, z + h), GroundVector.FromRaw(x, z + h) }));
            walls.Add(new Wall(x / 1e4, z / 1e4, (x + w) / 1e4, (z + h) / 1e4));
        }
        CombatSpace space = Arena(10, obstacles.ToArray());
        int heroes = random.Next(1, 4), creatures = random.Next(2, 7);
        var combatants = new List<CombatantSetup>();
        for (int i = 0; i < heroes + creatures; i++)
            for (int attempt = 0; attempt < 1000; attempt++)
            {
                bool hero = i < heroes;
                var placement = new CombatantPlacement(new GroundVector(Between(random, -90_000, 90_000), Between(random, -90_000, 90_000)),
                    hero ? Fixed.Parse("0.5") : Between(random, 3_000, 8_000), Direction(random), hero ? null : Between(random, 30_000, 70_000));
                CombatantSetup candidate;
                if (hero) candidate = Player($"h{i}", placement);
                else
                {
                    Fixed max = Between(random, 0, 60_000);
                    var behavior = new AssailantBehavior(max, random.Next(3) == 0 ? Between(random, 0, (int)max.Raw) : null,
                        random.Next(2) == 0 ? Between(random, 20_000, 120_000) : null, random.Next(2) == 0);
                    candidate = new AssailantSetup($"c{i}", "meute", Fixed.Zero, Fixed.Zero, random.Next(3) == 0 ? $"h{random.Next(heroes)}" : null, 50L * random.Next(0, 30),
                        50L * random.Next(10, 50), Enumerable.Range(0, random.Next(1, 3)).Select(k => RandomAttack(random, k)).ToArray(),
                        health: random.Next(3) == 0 ? 200 + random.Next(400) : null, placement: placement, behavior: behavior);
                }
                var trial = combatants.Append(candidate).ToList();
                string path = $"combatants[{trial.Count - 1}].";
                if (Spatial(space, trial, Array.Empty<CombatCommand>(), Rules).Validate().Any(v => v.Fields.Any(f => f.StartsWith(path, StringComparison.Ordinal)))) continue;
                combatants.Add(candidate);
                break;
            }
        var commands = new List<CombatCommand>();
        foreach (CombatantSetup c in combatants.Where(c => c is CharacterSetup))
            for (long t = 0; t < 6000; t += 50 * random.Next(4, 20))
            {
                int roll = random.Next(6);
                if (roll < 3) commands.Add(new CombatCommand(t, c.Id, Direction(random)));
                else if (roll < 4) commands.Add(Stop(t, c.Id));
                else if (roll < 5) commands.Add(GuardStart(t, c.Id));
                else commands.Add(GuardStop(t, c.Id));
            }
        return (Spatial(space, combatants, commands, Rules, durationMs: 6000, seed: random.Next()), walls);
    }

    [Fact]
    public void Comportements_aleatoires_engagement_annonce_charge_et_reprise()
    {
        var random = new Random(1151);
        int engaged = 0, moves = 0, holds = 0, charges = 0, resumed = 0;
        for (int n = 0; n < 120; n++)
        {
            var (setup, walls) = RandomCombat(random);
            var (track, log, _) = Trace(setup);
            var again = new MemoryCombatLog();
            CombatEngine.Run(setup, again);
            Assert.Equal(log.Lines, again.Lines);

            var behaving = setup.Combatants.OfType<AssailantSetup>().Where(a => a.Behavior != null).ToDictionary(a => a.Id, StringComparer.Ordinal);
            var engagedAt = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (JsonElement e in Lines(log, "engaged")) Assert.True(engagedAt.TryAdd(e.Str("actor")!, e.Tick()), $"engagé deux fois : {e}");
            engaged += engagedAt.Count;
            foreach (var (id, a) in behaving)
            {
                long from = a.Behavior!.AggroRange is null ? 0 : engagedAt.TryGetValue(id, out long t) ? t : long.MaxValue;
                // Un ennemi pas encore engagé ne bouge pas (positions au début de chaque tick ; il bouge au plus tôt dans son tick d'engagement).
                for (int k = 1; k < track[id].Count && k <= Math.Min(from, track[id].Count - 1); k++)
                    Assert.True(track[id][k] == track[id][0], $"{id} bouge avant de s'engager (tick {k - 1})");
                if (track[id].Distinct().Count() > 1) moves++;
            }
            // Pendant l'annonce d'une de ses attaques (de son tour à son impact), un ennemi à comportement ne marche pas (une charge le déplace
            // au tick de l'impact, après la phase de déplacement).
            foreach (JsonElement s in Lines(log, "attackStarted").Where(s => behaving.ContainsKey(s.Str("actor")!) && s.TryGetProperty("impactTick", out _)))
            {
                long start = s.Tick(), impact = s.Int("impactTick");
                if (impact == start || impact >= 120) continue;   // sans annonce, ou impact après la fin (jamais programmé)
                string id = s.Str("actor")!;
                // Une annonce plus courte que l'intervalle peut chevaucher la charge d'une annonce précédente : la charge, elle, déplace.
                if (Lines(log, "charged").Any(c => c.Str("actor") == id && c.Tick() >= start && c.Tick() < impact)) continue;
                for (long t = start + 1; t <= Math.Min(impact, track[id].Count - 1); t++)
                    Assert.True(track[id][(int)t] == track[id][(int)start + 1], $"{id} marche pendant son annonce (tick {t})");
                holds++;
            }
            // Une charge s'arrête dans l'aire, hors du décor.
            foreach (JsonElement c in Lines(log, "charged"))
            {
                double x = c.GetProperty("position")[0].GetDouble(), z = c.GetProperty("position")[1].GetDouble();
                double r = behaving[c.Str("actor")!].Placement!.Radius.Raw / 1e4;
                Assert.InRange(x, -10 + r - 1e-4, 10 - r + 1e-4);
                Assert.InRange(z, -10 + r - 1e-4, 10 - r + 1e-4);
                foreach (Wall w in walls)
                {
                    double dx = Math.Max(Math.Max(w.MinX - x, 0), x - w.MaxX), dz = Math.Max(Math.Max(w.MinZ - z, 0), z - w.MaxZ);
                    Assert.True(dx * dx + dz * dz >= r * r - 1e-6, $"charge dans le décor : {c}");
                }
                charges++;
            }
            // Un instantané à un tick tiré au hasard continue à l'identique.
            if (n % 3 == 0)
            {
                var resumedLog = new MemoryCombatLog();
                var engine = new CombatEngine(setup, resumedLog);
                long at = random.Next(1, 100);
                while (!engine.IsFinished && engine.CurrentTick < at) engine.Step();
                if (!engine.IsFinished)
                {
                    Lot114dSnapshotTests.AssertResumesIdentically(engine, resumedLog);
                    resumed++;
                }
            }
        }
        string counts = $"engagés {engaged}, qui bougent {moves}, annonces tenues {holds}, charges {charges}, reprises {resumed}";
        // Relevé au tirage 1151 : engagés 196, qui bougent 403, annonces tenues 722, charges 428, reprises 40.
        Assert.True(engaged > 150 && moves > 300 && holds > 500 && charges > 300 && resumed > 30, counts);
    }
}
