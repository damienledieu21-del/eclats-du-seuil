using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Space.SpaceTestKit;

namespace EdS.Simulation.Tests.Space;

/// <summary>
/// Sous-lot 11.4a : propriétés du déplacement sur des combats positionnés aléatoires (tirage fixe, donc reproductible) : à chaque tick,
/// toute emprise reste dans l'aire, ne chevauche aucun obstacle ni aucun combattant vivant qui lui est solide (règle A ou B), et n'avance
/// jamais plus que son pas ; le même combat donne le même journal ; les réserves de budget sont exactes (vérifiées en Debug à chaque
/// traitement). Les compteurs de couverture garantissent que les contacts, glissements et garde-fous sont réellement exercés.
/// </summary>
[Trait("Category", "Space")]
public class Lot114aPropertyTests
{
    private static Fixed Between(Random random, int minRaw, int maxRaw) => Fixed.FromRaw(random.Next(minRaw, maxRaw + 1));

    private static GroundVector Direction(Random random)
    {
        while (true)
        {
            var v = new GroundVector(Between(random, -10_000, 10_000), Between(random, -10_000, 10_000));
            if (!v.IsZero) return v;
        }
    }

    private static SpaceObstacle RandomObstacle(Random random, int index)
    {
        long cx = random.Next(-50_000, 50_001), cz = random.Next(-50_000, 50_001);
        long w = random.Next(2_000, 30_001), h = random.Next(2_000, 30_001);
        GroundVector P(long x, long z) => GroundVector.FromRaw(x, z);
        GroundVector[] points = random.Next(3) switch
        {
            0 => new[] { P(cx, cz), P(cx + w, cz), P(cx, cz + h) },                                     // triangle
            1 => new[] { P(cx, cz), P(cx + w, cz + h / 3), P(cx + w / 2, cz + h), P(cx - w / 3, cz + h / 2) },   // quadrilatère quelconque
            _ => new[] { P(cx, cz), P(cx + w, cz), P(cx + w, cz + h), P(cx, cz + h) },                  // rectangle
        };
        if (random.Next(2) == 0) Array.Reverse(points);   // sens horaire : accepté et retourné
        return new SpaceObstacle($"o{index}", points);
    }

    /// <summary>Combat positionné aléatoire valide : on retire au tirage les places qui chevauchent (la validation fait foi).</summary>
    private static CombatSetup RandomCombat(Random random)
    {
        var rules = new CombatRules
        {
            PlayerCreatureContact = random.Next(2) == 0 ? ContactRule.Solid : ContactRule.SlowedCrossing,
            GuardSpeedFactor = Between(random, 0, 10_000),
        };
        var obstacles = new List<SpaceObstacle>();
        for (int i = 0, n = random.Next(0, 5); i < n; i++)
        {
            SpaceObstacle o = RandomObstacle(random, i);
            if (ConvexPolygon.IsStrictlyConvex(o.Points, out _)) obstacles.Add(o);
        }
        CombatSpace space = Arena(6, obstacles.ToArray());
        int players = random.Next(1, 3), creatures = random.Next(1, 5);
        var combatants = new List<CombatantSetup>();
        for (int i = 0; i < players + creatures; i++)
            for (int attempt = 0; attempt < 1000; attempt++)
            {
                bool player = i < players, dummy = !player && random.Next(4) == 0;
                var position = new GroundVector(Between(random, -55_000, 55_000), Between(random, -55_000, 55_000));
                var placement = new CombatantPlacement(position, player ? Fixed.Parse("0.5") : Between(random, 3_000, 13_000), Direction(random),
                    player || dummy ? null : Between(random, 20_000, 120_000));
                CombatantSetup candidate = player ? Player($"j{i}", placement) : dummy ? PlacedDummy($"c{i}", placement) : Creature($"c{i}", placement, target: "j0");
                var trial = combatants.Append(candidate).ToList();
                string path = $"combatants[{trial.Count - 1}].";
                if (Spatial(space, trial, Array.Empty<CombatCommand>(), rules).Validate().Any(v => v.Fields.Any(f => f.StartsWith(path, StringComparison.Ordinal))))
                    continue;
                combatants.Add(candidate);
                break;
            }
        var commands = new List<CombatCommand>();
        foreach (CombatantSetup c in combatants.Where(c => c is not DummySetup))
            for (long t = 0; t < 4000; t += 50 * random.Next(4, 20))
            {
                int roll = random.Next(10);
                if (roll < 6) commands.Add(new CombatCommand(t, c.Id, Direction(random)));
                else if (roll < 8) commands.Add(Stop(t, c.Id));
                else if (c is CharacterSetup) commands.Add(new CombatCommand(t, c.Id, roll == 8 ? CombatAction.GuardStart : CombatAction.GuardStop));
            }
        return Spatial(space, combatants, commands, rules, durationMs: 4000, seed: random.Next());
    }

    [Fact]
    public void Invariants_du_deplacement_sur_combats_aleatoires()
    {
        var random = new Random(1140);
        int combats = 0, slowed = 0, contactTicks = 0, prevented = 0;
        for (int n = 0; n < 160; n++)
        {
            CombatSetup setup = RandomCombat(random);
            Assert.Empty(setup.Validate());
            CombatSpace space = setup.Space!;
            ConvexPolygon[] polygons = space.Obstacles.Select(o => new ConvexPolygon(o.Points)).ToArray();
            var log = new MemoryCombatLog();
            var engine = new CombatEngine(setup, log);
            var ids = setup.Combatants.Select(c => c.Id).ToArray();
            var previous = ids.ToDictionary(id => id, id => engine.View(id).Position!.Value);
            long maxStep = IntMath.MulDiv(SpaceLimits.MaxSpeed.Raw, 50, 1000, RoundingMode.Floor);
            while (engine.Step())
            {
                var views = ids.Select(engine.View).ToArray();
                foreach (CombatantView v in views)
                {
                    GroundVector p = v.Position!.Value;
                    long r = v.Radius!.Value.Raw;
                    Assert.True(p.X.Raw - r >= space.Min.X.Raw && p.X.Raw + r <= space.Max.X.Raw && p.Z.Raw - r >= space.Min.Z.Raw && p.Z.Raw + r <= space.Max.Z.Raw,
                        $"combat {n}, {v.Id} hors de l'aire en {p}");
                    Assert.All(polygons, o => Assert.False(o.OverlapsCircle(p.X.Raw, p.Z.Raw, r), $"combat {n}, {v.Id} dans un obstacle en {p}"));
                    long moved = IntMath.Sqrt(DistanceSquaredRaw(p, previous[v.Id]));
                    Assert.True(moved <= maxStep + 10, $"combat {n}, {v.Id} a avancé de {moved}");
                    if (moved > 0 && moved < 2400 && v.Kind == CombatantKind.Character && !v.Guarding) slowed++;
                    previous[v.Id] = p;
                }
                for (int i = 0; i < views.Length; i++)
                    for (int j = i + 1; j < views.Length; j++)
                    {
                        CombatantView a = views[i], b = views[j];
                        bool overlap = Planar.CirclesOverlap(a.Position!.Value.X.Raw, a.Position.Value.Z.Raw, a.Radius!.Value.Raw,
                            b.Position!.Value.X.Raw, b.Position.Value.Z.Raw, b.Radius!.Value.Raw);
                        long reach = a.Radius.Value.Raw + b.Radius.Value.Raw + 100;
                        if (DistanceSquaredRaw(a.Position.Value, b.Position.Value) < reach * reach) contactTicks++;
                        if (a.IsDead || b.IsDead) continue;
                        bool solid = setup.Combat.AreSolid(a.Kind == CombatantKind.Character, a.Radius.Value, b.Kind == CombatantKind.Character, b.Radius.Value);
                        Assert.False(solid && overlap, $"combat {n}, {a.Id} et {b.Id} solides se chevauchent");
                    }
            }
            prevented += log.Lines.Count(l => l.Contains("trapPrevented", StringComparison.Ordinal));
            var again = new MemoryCombatLog();
            CombatEngine.Run(setup, again);
            Assert.Equal(log.ToString(), again.ToString());
            combats++;
        }
        Assert.Equal(160, combats);
        Assert.True(contactTicks > 1000, $"contacts {contactTicks}");   // 3361 au tirage 1140
        Assert.True(slowed > 1000, $"pas réduits {slowed}");            // 3461 : glissements, contacts, traversées
        Assert.True(prevented > 5, $"garde-fous {prevented}");            // 23
    }
}
