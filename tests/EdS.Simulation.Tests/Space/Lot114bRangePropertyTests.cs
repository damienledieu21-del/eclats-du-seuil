using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Space.Lot114bRangeTests;
using static EdS.Simulation.Tests.Space.SpaceTestKit;

namespace EdS.Simulation.Tests.Space;

/// <summary>
/// Sous-lot 11.4b : propriétés des portées sur des combats positionnés aléatoires (tirage fixe). Un oracle indépendant, en nombres
/// flottants avec une marge de 1 mm, juge chaque décision du moteur :
/// <list type="bullet">
/// <item>tout lancement d'une compétence à cible est à portée (bords des emprises) et sans mur franchement entre les centres ;</item>
/// <item>tout refus <c>targetOutOfRange</c> est franchement hors de portée, tout refus <c>noLineOfSight</c> à portée et près d'un mur ;
/// une commande franchement à portée et à vue n'est jamais refusée pour ces motifs ;</item>
/// <item>une onde touche à l'impact (positions après la phase movement) tous les ennemis franchement dans son rayon et aucun franchement
/// dehors ;</item>
/// <item>une riposte frappe un attaquant à portée ; une riposte annulée <c>outOfRange</c> visait un attaquant hors de portée ;</item>
/// <item>un transfert de protection n'a lieu qu'à portée du protecteur.</item>
/// </list>
/// Le même combat donne le même journal ; les réserves de budget sont exactes (vérifiées en Debug).
/// </summary>
[Trait("Category", "Space")]
public class Lot114bRangePropertyTests
{
    private const double Margin = 0.001;

    private sealed record Box(double X0, double Z0, double X1, double Z1);

    private static Fixed Between(Random random, int minRaw, int maxRaw) => Fixed.FromRaw(random.Next(minRaw, maxRaw + 1));

    private static GroundVector Direction(Random random)
    {
        while (true)
        {
            var v = new GroundVector(Between(random, -10_000, 10_000), Between(random, -10_000, 10_000));
            if (!v.IsZero) return v;
        }
    }

    /// <summary>Sans entretien de garde, recharge globale d'un tick : beaucoup de décisions de portée par combat.</summary>
    private static readonly CombatRules Rules = new() { GuardUpkeepPerSecond = 0, GlobalCooldownMs = 50 };

    private static (CombatSetup Setup, Box[] Walls) RandomCombat(Random random)
    {
        var walls = new List<SpaceObstacle>();
        for (int i = 0, n = random.Next(0, 5); i < n; i++)
        {
            long x = random.Next(-80_000, 80_001), z = random.Next(-80_000, 80_001);
            long w = random.Next(2_000, 40_001), h = random.Next(2_000, 40_001);
            walls.Add(new SpaceObstacle($"mur{i}", new[] { GroundVector.FromRaw(x, z), GroundVector.FromRaw(x + w, z), GroundVector.FromRaw(x + w, z + h), GroundVector.FromRaw(x, z + h) }));
        }
        CombatSpace space = Arena(9, walls.ToArray());
        int heroes = random.Next(1, 3), others = random.Next(1, 5);
        var combatants = new List<CombatantSetup>();
        for (int i = 0; i < heroes + others; i++)
            for (int attempt = 0; attempt < 1000; attempt++)
            {
                bool hero = i < heroes, dummy = !hero && random.Next(3) == 0;
                int target = random.Next(heroes);
                // Une créature sur deux commence près de sa cible (mêlée, ripostes), les autres n'importe où.
                GroundVector position = !hero && random.Next(2) == 0 && combatants.Count > target
                    ? new GroundVector(combatants[target].Placement!.Position.X + Between(random, -45_000, 45_000), combatants[target].Placement!.Position.Z + Between(random, -45_000, 45_000))
                    : new GroundVector(Between(random, -85_000, 85_000), Between(random, -85_000, 85_000));
                var placement = new CombatantPlacement(position, Between(random, 3_000, 12_000), Direction(random), hero || dummy ? null : Between(random, 20_000, 80_000));
                CombatantSetup candidate = hero
                    ? Hero($"h{i}", placement, new[] { Coup, Appel, Vague, Lien }, new[] { Riposte })
                    : dummy ? PlacedDummy($"c{i}", placement)
                    : Creature($"c{i}", placement, target: $"h{target}", firstAttackMs: 50 * random.Next(0, 40));
                var trial = combatants.Append(candidate).ToList();
                string path = $"combatants[{trial.Count - 1}].";
                if (Spatial(space, trial, Array.Empty<CombatCommand>(), Rules).Validate().Any(v => v.Fields.Any(f => f.StartsWith(path, StringComparison.Ordinal))))
                    continue;
                combatants.Add(candidate);
                break;
            }
        string[] heroIds = combatants.OfType<CharacterSetup>().Select(c => c.Id).ToArray();
        string[] enemyIds = combatants.Where(c => c is not CharacterSetup).Select(c => c.Id).ToArray();
        var commands = new List<CombatCommand>();
        foreach (CombatantSetup c in combatants.Where(c => c is not DummySetup))
            for (long t = 0; t < 4000; t += 50 * random.Next(2, 12))
            {
                int roll = random.Next(12);
                if (roll < 4) commands.Add(new CombatCommand(t, c.Id, Direction(random)));
                else if (roll < 5) commands.Add(Stop(t, c.Id));
                else if (c is not CharacterSetup) continue;
                else if (roll < 8 && enemyIds.Length > 0) commands.Add(Cmd(t, roll == 5 ? Appel : Coup, c.Id, enemyIds[random.Next(enemyIds.Length)]));
                else if (roll < 9) commands.Add(Cmd(t, Vague, c.Id, null));
                else if (roll < 10) commands.Add(Cmd(t, Lien, c.Id, heroIds[random.Next(heroIds.Length)]));
                else commands.Add(Parry(t, c.Id));
            }
        // Parades calées sur les coups des créatures (une fois sur deux) : parades parfaites, donc ripostes.
        foreach (AssailantSetup a in combatants.OfType<AssailantSetup>())
            for (long t = a.FirstAttackMs; t < 4000; t += a.IntervalMs)
                if (random.Next(2) == 0) commands.Add(Parry(t, a.Target!));
        Box[] boxes = walls.Select(w => new Box(w.Points[0].X.Raw / 1e4, w.Points[0].Z.Raw / 1e4, w.Points[2].X.Raw / 1e4, w.Points[2].Z.Raw / 1e4)).ToArray();
        return (Spatial(space, combatants, commands, Rules, durationMs: 4000, seed: random.Next()), boxes);
    }

    /// <summary>Segment [a, b] contre un rectangle fermé (méthode des tranches), en flottants.</summary>
    private static bool Crosses(double ax, double az, double bx, double bz, Box box)
    {
        double t0 = 0, t1 = 1;
        bool Clip(double p, double q)
        {
            if (p == 0) return q >= 0;
            double r = q / p;
            if (p < 0) { if (r > t1) return false; if (r > t0) t0 = r; }
            else { if (r < t0) return false; if (r < t1) t1 = r; }
            return true;
        }
        double dx = bx - ax, dz = bz - az;
        return Clip(-dx, ax - box.X0) && Clip(dx, box.X1 - ax) && Clip(-dz, az - box.Z0) && Clip(dz, box.Z1 - az) && t0 <= t1;
    }

    private static Box Grow(Box b, double by) => new(b.X0 - by, b.Z0 - by, b.X1 + by, b.Z1 + by);

    private static (double X, double Z) P(GroundVector v) => (v.X.Raw / 1e4, v.Z.Raw / 1e4);

    private static double Centers(GroundVector a, GroundVector b)
    {
        var (ax, az) = P(a);
        var (bx, bz) = P(b);
        return Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
    }

    /// <summary>Positions au début de chaque tick, et après le dernier (index = tick) : la position d'impact du tick t est à l'index t + 1.</summary>
    private static (Dictionary<string, List<GroundVector>> Track, MemoryCombatLog Log) TraceToEnd(CombatSetup setup)
    {
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log);
        var track = setup.Combatants.ToDictionary(c => c.Id, _ => new List<GroundVector>());
        void Record()
        {
            foreach (var (id, list) in track) list.Add(engine.View(id).Position!.Value);
        }
        Record();
        while (engine.Step()) Record();
        Record();
        EdS.TestSupport.CausalGraph.Build(log.Lines);
        return (track, log);
    }

    [Fact]
    public void Decisions_de_portee_et_de_vue_conformes_a_un_oracle_independant()
    {
        var random = new Random(1141);
        int casts = 0, outOfRange = 0, noSight = 0, waveHits = 0, waveMisses = 0, ripostes = 0, riposteCancels = 0, transfers = 0;
        for (int n = 0; n < 200; n++)
        {
            var (setup, walls) = RandomCombat(random);
            Assert.Empty(setup.Validate());
            var radius = setup.Combatants.ToDictionary(c => c.Id, c => c.Placement!.Radius.Raw / 1e4);
            var (track, log) = TraceToEnd(setup);
            var events = Events(log);
            double Edge(string a, string b, int tick) => Centers(track[a][tick], track[b][tick]) - radius[a] - radius[b];
            bool SurelyBlocked(string a, string b, int tick)
            {
                var (ax, az) = P(track[a][tick]);
                var (bx, bz) = P(track[b][tick]);
                return walls.Any(w => w.X1 - w.X0 > 2 * Margin && w.Z1 - w.Z0 > 2 * Margin && Crosses(ax, az, bx, bz, Grow(w, -Margin)));
            }
            bool MaybeBlocked(string a, string b, int tick)
            {
                var (ax, az) = P(track[a][tick]);
                var (bx, bz) = P(track[b][tick]);
                return walls.Any(w => Crosses(ax, az, bx, bz, Grow(w, Margin)));
            }
            double RangeOf(string skill) => skill == Coup ? 2.5 : 15;

            // Commandes de compétence à cible : décision au début du tick (avant la phase movement).
            var decided = new Dictionary<long, (string Kind, JsonElement Line)>();
            foreach (JsonElement e in events.OfKind("skillCast")) decided[e.Int("command")] = ("cast", e);
            foreach (JsonElement e in events.OfKind("commandRejected")) decided[e.Int("command")] = ("rejected", e);
            for (int index = 0; index < setup.Commands.Count; index++)
            {
                CombatCommand c = setup.Commands[index];
                if (c.Skill is not (Coup or Appel or Lien) || !decided.TryGetValue(index, out var outcome)) continue;
                int tick = (int)(c.AtMs / 50);
                double edge = Edge(c.Actor, c.Target!, tick), range = RangeOf(c.Skill);
                bool self = c.Actor == c.Target;
                string? reason = outcome.Kind == "rejected" ? outcome.Line.Str("reason") : null;
                string at = $"combat {n}, commande {index} ({c.Skill} {c.Actor}→{c.Target}, tick {tick}, bords {edge:F5})";
                if (outcome.Kind == "cast")
                {
                    casts++;
                    Assert.True(self || edge <= range + Margin, $"{at} : lancée hors de portée");
                    Assert.False(!self && SurelyBlocked(c.Actor, c.Target!, tick), $"{at} : lancée à travers un mur");
                }
                else if (reason == "targetOutOfRange")
                {
                    outOfRange++;
                    Assert.True(!self && edge >= range - Margin, $"{at} : refusée hors de portée à tort");
                }
                else if (reason == "noLineOfSight")
                {
                    noSight++;
                    Assert.True(!self && edge <= range + Margin && MaybeBlocked(c.Actor, c.Target!, tick), $"{at} : refusée sans vue à tort");
                }
                if (!self && edge < range - Margin && !MaybeBlocked(c.Actor, c.Target!, tick))
                    Assert.True(reason is not ("targetOutOfRange" or "noLineOfSight"), $"{at} : refusée {reason} alors qu'à portée et à vue");
            }

            // Ondes : jugées à l'impact, positions après la phase movement du tick (début du tick suivant).
            foreach (JsonElement cast in events.OfKind("skillCast").Where(e => e.Str("skill") == Vague))
            {
                int tick = (int)cast.Tick();
                string caster = cast.Str("actor")!;
                var hit = events.OfKind("damage").Where(d => d.Tick() == tick && d.Str("source") == caster && d.Str("skill") == Vague)
                    .Select(d => d.Str("target")!).ToHashSet();
                foreach (CombatantSetup enemy in setup.Combatants.Where(x => x is not CharacterSetup))
                {
                    double reach = Centers(track[caster][tick + 1], track[enemy.Id][tick + 1]) - radius[enemy.Id];
                    if (reach < 5 - Margin) { Assert.Contains(enemy.Id, hit); waveHits++; }
                    else if (reach > 5 + Margin) { Assert.DoesNotContain(enemy.Id, hit); waveMisses++; }
                }
            }

            // Ripostes : frappe de réaction (ni compétence ni attaque) sur l'attaquant, à l'impact.
            foreach (JsonElement d in events.OfKind("damage").Where(d => d.TryGetProperty("trigger", out _) && d.Str("skill") is null && d.Str("attack") is null))
            {
                ripostes++;
                int tick = (int)d.Tick();
                Assert.True(Edge(d.Str("source")!, d.Str("target")!, tick + 1) <= 2.5 + Margin, $"combat {n} : riposte hors de portée au tick {tick}");
            }
            foreach (JsonElement r in events.OfKind("reactionCancelled").Where(r => r.Str("reason") == "outOfRange"))
            {
                riposteCancels++;
                int tick = (int)r.Tick();
                JsonElement cause = events.Single(e => e.TryGetProperty("eid", out JsonElement eid) && eid.GetInt64() == r.Int("cause"));
                // Cause : la parade parfaite (parrySucceeded) ; l'attaquant est la source de l'impact paré.
                string bearer = cause.Str("actor")!, attacker = cause.Str("source")!;
                Assert.True(Edge(bearer, attacker, tick + 1) >= 2.5 - Margin, $"combat {n} : riposte annulée à portée au tick {tick}");
            }

            // Transferts de protection : seulement à portée du protecteur.
            foreach (JsonElement t in events.OfKind("damageTransferred"))
            {
                transfers++;
                int tick = (int)t.Tick();
                Assert.True(Edge(t.Str("protected")!, t.Str("target")!, tick + 1) <= 15 + Margin, $"combat {n} : transfert hors de portée au tick {tick}");
            }

            var again = new MemoryCombatLog();
            CombatEngine.Run(setup, again);
            Assert.Equal(log.ToString(), again.ToString());
        }
        // Couverture : chaque décision est réellement exercée.
        string counts = $"lancements {casts}, hors de portée {outOfRange}, sans vue {noSight}, onde {waveHits}/{waveMisses}, ripostes {ripostes}/{riposteCancels}, transferts {transfers}";
        // Au tirage 1141 : 686 lancements, 575 refus hors de portée, 39 sans vue, onde 267 touchés et 582 manqués, 68 transferts ; 29
        // ripostes et 76 annulées (sous-lot 11.4c : la parade ne vaut plus contre un coup venu de dos, moins de parades parfaites).
        Assert.True(casts > 500, counts);
        Assert.True(outOfRange > 400, counts);
        Assert.True(noSight > 25, counts);
        Assert.True(waveHits > 150 && waveMisses > 400, counts);
        Assert.True(ripostes > 15 && riposteCancels > 40, counts);
        Assert.True(transfers > 40, counts);
    }
}
