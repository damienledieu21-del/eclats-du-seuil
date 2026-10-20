using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Space.SpaceTestKit;

namespace EdS.Simulation.Tests.Space;

/// <summary>
/// Sous-lot 11.4c : propriétés des attaques annoncées et de la garde de face sur des combats positionnés aléatoires (tirage fixe), jugées
/// par un oracle indépendant en flottants (marge 1 mm) :
/// <list type="bullet">
/// <item>la zone est verrouillée au tour de l'assaillant : origine, axe (vers la cible) ou centre (cible ou assaillant) d'après les
/// positions du début du tick ;</item>
/// <item>chaque impact frappe exactement les joueurs dont l'emprise touche la zone aux positions de la résolution (franchement dedans :
/// touchés, franchement dehors : épargnés) ;</item>
/// <item>une garde ou une parade n'est retenue que contre un coup venu franchement de face, un coup de dos jamais de face ;</item>
/// <item>un coup dirigé n'est lancé qu'à portée et sauté hors de portée.</item>
/// </list>
/// Le même combat donne le même journal ; les réserves de budget sont exactes (vérifiées en Debug).
/// </summary>
[Trait("Category", "Space")]
public class Lot114cAttackPropertyTests
{
    private const double Margin = 0.001;

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

    private static AssailantAttack RandomAttack(Random random, int k)
    {
        var shape = (AttackShape)random.Next(5);
        long telegraph = 50L * random.Next(0, 40);
        Fixed? F(int min, int max) => Between(random, min, max);
        bool? Maybe() => random.Next(3) switch { 0 => true, 1 => false, _ => null };
        return shape switch
        {
            AttackShape.Target => new AssailantAttack($"a{k}", 20, applies: null, shape: shape, telegraphMs: telegraph, range: random.Next(2) == 0 ? null : F(5_000, 40_000),
                guardable: Maybe(), parryable: Maybe()),
            AttackShape.Cone => new AssailantAttack($"a{k}", 20, shape: shape, telegraphMs: telegraph + 2000, range: F(10_000, 60_000), angle: random.Next(1, 181), guardable: Maybe()),
            AttackShape.Line => new AssailantAttack($"a{k}", 20, shape: shape, telegraphMs: telegraph + 2000, range: F(10_000, 60_000), width: F(2_000, 30_000), guardable: Maybe()),
            AttackShape.Circle => new AssailantAttack($"a{k}", 20, shape: shape, telegraphMs: telegraph + 2000, radius: F(5_000, 30_000),
                center: random.Next(2) == 0 ? ShapeCenter.Target : ShapeCenter.Self, guardable: Maybe()),
            _ => new AssailantAttack($"a{k}", 20, shape: shape, telegraphMs: telegraph + 2000, radius: F(5_000, 30_000), center: random.Next(2) == 0 ? ShapeCenter.Target : ShapeCenter.Self,
                persistentMs: 50L * random.Next(4, 30), pulseMs: 200),
        };
    }

    private static CombatSetup RandomCombat(Random random)
    {
        var walls = new List<SpaceObstacle>();
        for (int i = 0, n = random.Next(0, 3); i < n; i++)
        {
            long x = random.Next(-60_000, 60_001), z = random.Next(-60_000, 60_001), w = random.Next(2_000, 20_001), h = random.Next(2_000, 20_001);
            walls.Add(new SpaceObstacle($"mur{i}", new[] { GroundVector.FromRaw(x, z), GroundVector.FromRaw(x + w, z), GroundVector.FromRaw(x + w, z + h), GroundVector.FromRaw(x, z + h) }));
        }
        CombatSpace space = Arena(8, walls.ToArray());
        int heroes = random.Next(1, 4), creatures = random.Next(1, 4);
        var combatants = new List<CombatantSetup>();
        for (int i = 0; i < heroes + creatures; i++)
            for (int attempt = 0; attempt < 1000; attempt++)
            {
                bool hero = i < heroes;
                var placement = new CombatantPlacement(new GroundVector(Between(random, -70_000, 70_000), Between(random, -70_000, 70_000)),
                    hero ? Fixed.Parse("0.5") : Between(random, 3_000, 9_000), Direction(random), hero ? null : Between(random, 20_000, 60_000));
                CombatantSetup candidate = hero
                    ? Player($"h{i}", placement)
                    : new AssailantSetup($"c{i}", "cibles", Fixed.Zero, Fixed.Zero, $"h{random.Next(heroes)}", 50L * random.Next(0, 20), 50L * random.Next(10, 40),
                        Enumerable.Range(0, random.Next(1, 4)).Select(k => RandomAttack(random, k)).ToArray(), placement: placement);
                var trial = combatants.Append(candidate).ToList();
                string path = $"combatants[{trial.Count - 1}].";
                if (Spatial(space, trial, Array.Empty<CombatCommand>(), Rules).Validate().Any(v => v.Fields.Any(f => f.StartsWith(path, StringComparison.Ordinal))))
                    continue;
                combatants.Add(candidate);
                break;
            }
        string[] ids = combatants.Select(c => c.Id).ToArray();
        var commands = new List<CombatCommand>();
        foreach (CombatantSetup c in combatants)
            for (long t = 0; t < 4000; t += 50 * random.Next(2, 10))
            {
                int roll = random.Next(10);
                if (roll < 4) commands.Add(new CombatCommand(t, c.Id, Direction(random)));
                else if (roll < 5) commands.Add(Stop(t, c.Id));
                else if (c is not CharacterSetup) continue;
                else if (roll < 7) commands.Add(GuardStart(t, c.Id));
                else if (roll < 8) commands.Add(GuardStop(t, c.Id));
                else if (roll < 9) commands.Add(Parry(t, c.Id));
                else commands.Add(CombatCommand.SelectTarget(t, c.Id, random.Next(4) == 0 ? null : ids[random.Next(ids.Length)]));
            }
        return Spatial(space, combatants, commands, Rules, durationMs: 4000, seed: random.Next());
    }

    private static (double X, double Z) P(GroundVector v) => (v.X.Raw / 1e4, v.Z.Raw / 1e4);

    private static (double X, double Z) Vec(JsonElement e, string name) =>
        (e.GetProperty(name)[0].GetDouble(), e.GetProperty(name)[1].GetDouble());

    private static double Dist((double X, double Z) a, (double X, double Z) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

    private static double SegmentDistance((double X, double Z) a, (double X, double Z) b, (double X, double Z) p)
    {
        double ex = b.X - a.X, ez = b.Z - a.Z, len2 = ex * ex + ez * ez;
        double t = len2 == 0 ? 0 : Math.Clamp(((p.X - a.X) * ex + (p.Z - a.Z) * ez) / len2, 0, 1);
        return Dist((a.X + t * ex, a.Z + t * ez), p);
    }

    /// <summary>Distance signée entre l'emprise (centre p, rayon r) et la zone décrite par sa ligne attackStarted : ≤ 0, elle la touche.</summary>
    private static double Gap(JsonElement started, (double X, double Z) p, double r)
    {
        var o = Vec(started, "origin");
        switch (started.Str("shape"))
        {
            case "circle":
            case "persistent":
                return Dist(o, p) - started.GetProperty("radius").GetDouble() - r;
            case "line":
            {
                var a = Vec(started, "axis");
                double len = started.GetProperty("range").GetDouble();
                return SegmentDistance(o, (o.X + a.X * len, o.Z + a.Z * len), p) - started.GetProperty("width").GetDouble() / 2 - r;
            }
            default:   // cone
            {
                var a = Vec(started, "axis");
                double len = started.GetProperty("range").GetDouble(), half = started.Int("angle") * Math.PI / 360;
                double ax = Math.Atan2(a.Z, a.X);
                double angle = Math.Abs(Math.IEEERemainder(Math.Atan2(p.Z - o.Z, p.X - o.X) - ax, 2 * Math.PI));
                double inside = Dist(o, p) < 1e-9 || angle <= half ? Dist(o, p) - len - r : double.MaxValue;
                double side(double s) => SegmentDistance(o, (o.X + Math.Cos(ax + s) * len, o.Z + Math.Sin(ax + s) * len), p) - r;
                return Math.Min(inside, Math.Min(side(half), side(-half)));
            }
        }
    }

    [Fact]
    public void Annonces_zones_et_bouclier_conformes_a_un_oracle_independant()
    {
        var random = new Random(1143);
        int locked = 0, hits = 0, spared = 0, frontal = 0, behind = 0, skipped = 0, pulses = 0;
        for (int n = 0; n < 200; n++)
        {
            CombatSetup setup = RandomCombat(random);
            Assert.Empty(setup.Validate());
            var radius = setup.Combatants.ToDictionary(c => c.Id, c => c.Placement!.Radius.Raw / 1e4);
            var heroes = setup.Combatants.OfType<CharacterSetup>().Select(c => c.Id).ToArray();
            var assailants = setup.Combatants.OfType<AssailantSetup>().ToDictionary(a => a.Id);
            var log = new MemoryCombatLog();
            var engine = new CombatEngine(setup, log);
            var pos = setup.Combatants.ToDictionary(c => c.Id, _ => new List<GroundVector>());
            var facing = setup.Combatants.ToDictionary(c => c.Id, _ => new List<GroundVector>());
            void Record()
            {
                foreach (CombatantSetup c in setup.Combatants)
                {
                    CombatantView v = engine.View(c.Id);
                    pos[c.Id].Add(v.Position!.Value);
                    facing[c.Id].Add(v.Facing!.Value);
                }
            }
            Record();
            while (engine.Step()) Record();
            Record();
            var events = Events(log);
            var started = events.OfKind("attackStarted").Where(e => e.TryGetProperty("shape", out _)).ToDictionary(e => e.Int("eid"));
            string at = $"combat {n}";

            // Verrouillage : origine et axe, ou centre, d'après les positions du début du tick du tour.
            foreach (JsonElement s in started.Values.Where(s => s.Str("shape") != "target"))
            {
                locked++;
                int t = (int)s.Tick();
                string actor = s.Str("actor")!, target = s.Str("target")!;
                var origin = Vec(s, "origin");
                bool self = s.Str("shape") is "cone" or "line" || assailants[actor].Attacks.Single(x => x.Id == s.Str("attack")).Center == ShapeCenter.Self;
                Assert.True(Dist(origin, P(pos[self ? actor : target][t])) < Margin, $"{at} : origine de {s.Str("attack")} au tick {t}");
                if (s.Str("shape") is "cone" or "line")
                {
                    var a = Vec(s, "axis");
                    var (fx, fz) = P(pos[target][t]);
                    double dx = fx - origin.X, dz = fz - origin.Z, len = Math.Sqrt(dx * dx + dz * dz);
                    if (len > 0.01) Assert.True(Dist(a, (dx / len, dz / len)) < 0.01, $"{at} : axe de {s.Str("attack")}");
                }
            }

            // Impacts : touchés et épargnés aux positions de la résolution.
            foreach (JsonElement landed in events.OfKind("attackLanded"))
            {
                int t = (int)landed.Tick();
                JsonElement s = started[landed.Int("cause")];
                if (s.Str("shape") == "persistent") pulses++;
                var hit = landed.GetProperty("targets").EnumerateArray().Select(x => x.GetString()!).ToHashSet();
                if (s.Str("shape") == "target") continue;
                foreach (string h in heroes)
                {
                    double gap = Gap(s, P(pos[h][t + 1]), radius[h]);
                    if (gap < -Margin) { Assert.Contains(h, hit); hits++; }
                    else if (gap > Margin) { Assert.DoesNotContain(h, hit); spared++; }
                }
            }

            // Bouclier : garde ou parade seulement de face, « behind » jamais de face (positions et orientations de la résolution).
            foreach (JsonElement d in events.OfKind("damage").Where(d => d.Str("attack") != null))
            {
                string? defense = d.Str("defense");
                if (defense is not ("guard" or "guardBroken" or "parry" or "behind")) continue;
                int t = (int)d.Tick();
                string target = d.Str("target")!;
                JsonElement cause = events.First(e => e.TryGetProperty("eid", out JsonElement eid) && eid.GetInt64() == d.Int("cause"));
                (double X, double Z) from = cause.Str("event") == "attackLanded" && started[cause.Int("cause")].Str("shape") != "target"
                    ? Vec(started[cause.Int("cause")], "origin") : P(pos[d.Str("source")!][t + 1]);
                var (px, pz) = P(pos[target][t + 1]);
                var (fx, fz) = P(facing[target][t + 1]);
                double dot = (from.X - px) * fx + (from.Z - pz) * fz;
                if (defense == "behind") { Assert.True(dot < Margin, $"{at} : coup « de dos » venu de face au tick {t}"); behind++; }
                else { Assert.True(dot > -Margin, $"{at} : {defense} contre un coup de dos au tick {t}"); frontal++; }
            }

            // Coups dirigés : lancés à portée, sautés hors de portée.
            foreach (JsonElement e in events.OfKind("attackSkipped").Where(e => e.Str("reason") == "targetOutOfRange"))
            {
                skipped++;
                int t = (int)e.Tick();
                AssailantAttack attack = assailants[e.Str("actor")!].Attacks.Single(x => x.Id == e.Str("attack"));
                double edge = Dist(P(pos[e.Str("actor")!][t]), P(pos[e.Str("target")!][t])) - radius[e.Str("actor")!] - radius[e.Str("target")!];
                Assert.True(edge >= attack.Range!.Value.Raw / 1e4 - Margin, $"{at} : coup sauté à portée au tick {t}");
            }
            foreach (JsonElement s in started.Values.Where(s => s.Str("shape") == "target" && s.TryGetProperty("range", out _)))
            {
                int t = (int)s.Tick();
                double edge = Dist(P(pos[s.Str("actor")!][t]), P(pos[s.Str("target")!][t])) - radius[s.Str("actor")!] - radius[s.Str("target")!];
                Assert.True(edge <= s.GetProperty("range").GetDouble() + Margin, $"{at} : coup dirigé lancé hors de portée au tick {t}");
            }

            var again = new MemoryCombatLog();
            CombatEngine.Run(setup, again);
            Assert.Equal(log.ToString(), again.ToString());
        }
        string counts = $"verrouillées {locked}, touchés {hits}, épargnés {spared}, de face {frontal}, de dos {behind}, sautés {skipped}, impulsions {pulses}";
        // Au tirage 1143 : 1232 zones verrouillées, 120 touchés et 944 épargnés, 36 défenses de face et 27 coups de dos, 104 coups dirigés
        // sautés, 282 impulsions de zones persistantes.
        Assert.True(locked > 800 && hits > 80 && spared > 600 && frontal > 25 && behind > 18 && skipped > 70 && pulses > 180, counts);
    }
}
