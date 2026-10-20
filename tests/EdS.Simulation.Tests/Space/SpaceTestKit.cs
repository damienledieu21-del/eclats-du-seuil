using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Space;

/// <summary>Sous-lot 11.4a : combats positionnés de test (aire, obstacles, places, commandes de déplacement) et lecture des positions.</summary>
internal static class SpaceTestKit
{
    /// <summary>Aire carrée de ±<paramref name="half"/> m, avec des obstacles.</summary>
    public static CombatSpace Arena(int half = 10, params SpaceObstacle[] obstacles) => new(V(-half, -half), V(half, half), obstacles);

    public static CombatSpace Box(string minX, string minZ, string maxX, string maxZ, params SpaceObstacle[] obstacles) =>
        new(GroundVector.Parse(minX, minZ), GroundVector.Parse(maxX, maxZ), obstacles);

    public static GroundVector V(string x, string z) => GroundVector.Parse(x, z);
    public static GroundVector V(long x, long z) => new(Fixed.FromInt(x), Fixed.FromInt(z));

    /// <summary>Rectangle aligné sur les axes, sommets dans le sens trigonométrique.</summary>
    public static SpaceObstacle Rect(string id, string minX, string minZ, string maxX, string maxZ) =>
        new(id, new[] { V(minX, minZ), V(maxX, minZ), V(maxX, maxZ), V(minX, maxZ) });

    public static CombatantPlacement At(string x, string z, string radius = "0.5", GroundVector? facing = null, string? speed = null) =>
        new(V(x, z), Fixed.Parse(radius), facing, speed is null ? null : Fixed.Parse(speed));

    /// <summary>Personnage de test (classe d'essai) placé.</summary>
    public static CharacterSetup Player(string id, CombatantPlacement placement, Attributes? attributes = null, string team = "joueurs")
    {
        CharacterSetup c = Character(id, team, attributes: attributes ?? Attrs(vitalite: 200));
        return new CharacterSetup(c.Id, c.Team, c.Class, c.Build, c.Initiative, placement, c.ArrivalMs);
    }

    /// <summary>Assaillant placé, qui n'attaque pas pendant les essais (première attaque après la fin).</summary>
    public static AssailantSetup Creature(string id, CombatantPlacement placement, long? health = null, string? target = "bastion",
        long firstAttackMs = 600_000, string team = "cibles", params AssailantAttack[] attacks) =>
        new(id, team, Fixed.Zero, Fixed.Zero, target, firstAttackMs, 2000, attacks.Length == 0 ? new[] { Blow() } : attacks, health: health, placement: placement);

    public static DummySetup PlacedDummy(string id, CombatantPlacement placement, string team = "cibles") =>
        new(id, team, Fixed.FromInt(100), Fixed.Zero, placement: placement);

    public static CombatCommand Move(long atMs, string actor, string x, string z) => new(atMs, actor, V(x, z));
    public static CombatCommand Stop(long atMs, string actor) => new(atMs, actor, CombatAction.Stop);

    public static CombatSetup Spatial(CombatSpace space, IEnumerable<CombatantSetup> combatants, IEnumerable<CombatCommand> commands,
        CombatRules? rules = null, long durationMs = 10_000, long seed = 42) =>
        new(seed, durationMs, StatRules.Provisional, DamageRules.Provisional, ProgressionRules.Provisional, rules ?? CombatRules.Provisional,
            combatants, commands, Identity, space);

    /// <summary>Simule tick par tick et relève la position de chaque combattant au début de chaque tick (index = tick).</summary>
    public static (Dictionary<string, List<GroundVector>> Track, MemoryCombatLog Log, CombatEngine Engine) Trace(CombatSetup setup)
    {
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log);
        var track = setup.Combatants.ToDictionary(c => c.Id, _ => new List<GroundVector>());
        do
            foreach (var (id, list) in track) list.Add(engine.View(id).Position!.Value);
        while (engine.Step());
        EdS.TestSupport.CausalGraph.Build(log.Lines);
        return (track, log, engine);
    }

    /// <summary>Position après <paramref name="ticks"/> ticks simulés (au début du tick suivant).</summary>
    public static GroundVector After(this Dictionary<string, List<GroundVector>> track, string id, int ticks) => track[id][ticks];

    public static long DistanceSquaredRaw(GroundVector a, GroundVector b) =>
        Planar.LengthSquared(a.X.Raw - b.X.Raw, a.Z.Raw - b.Z.Raw);

    public static List<JsonElement> Lines(MemoryCombatLog log, string kind) => Events(log).OfKind(kind).ToList();
}
