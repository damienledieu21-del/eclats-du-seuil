using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Presence.PresenceKit;
using static EdS.Simulation.Tests.Space.SpaceTestKit;

namespace EdS.Simulation.Tests.Presence;

/// <summary>
/// Sous-lot 11.4d : arrivée pilotée (<see cref="CombatEngine.Join"/>) sans place réservée d'avance, et combat complété qui se rejoue à
/// l'identique ; propriété sur des combats aléatoires (arrivées du scénario et pilotées, départs, morts, commandes soumises) : le journal
/// joué et celui du combat complété ont le même corps, et aucun absent n'agit ni n'est frappé.
/// </summary>
[Trait("Category", "Presence")]
public class Lot114dJoinTests
{
    private static List<JsonElement> Lines(IEnumerable<string> log, string kind) =>
        log.Skip(1).Select(l => JsonDocument.Parse(l).RootElement.Clone()).Where(e => e.GetProperty("event").GetString() == kind).ToList();

    private static void AssertReplaysIdentically(CombatEngine engine, MemoryCombatLog live)
    {
        var replay = new MemoryCombatLog();
        CombatEngine.Run(engine.CompletedSetup(), replay);
        Assert.Equal(live.Lines.Skip(1), replay.Lines.Skip(1));
        EdS.TestSupport.CausalGraph.Build(replay.Lines);
    }

    [Fact]
    public void Un_joueur_rejoint_un_combat_en_cours_et_le_combat_complete_se_rejoue_a_l_identique()
    {
        var live = new MemoryCombatLog();
        var engine = new CombatEngine(Setup(new[] { Cmd(0, Frappe) }, new CombatantSetup[] { Tank(), Dummy(), Assailant(target: "bastion") }, durationMs: 4000), live);
        while (engine.CurrentTick < 40) engine.Step();
        engine.Join(Tank("renfort").Arriving(2000));
        engine.Submit(Cmd(2000, Gratuite, "renfort"));   // dès son tick d'arrivée
        while (engine.CurrentTick < 60) engine.Step();
        engine.Submit(Leave(3000, "bastion"));
        while (engine.Step()) { }
        Assert.Equal(40L, Lines(live.Lines, "combatantJoined").Single().Tick());
        Assert.Single(Lines(live.Lines, "skillCast"), c => c.Str("actor") == "renfort" && c.Tick() == 40);
        Assert.Equal(60L, Lines(live.Lines, "combatantLeft").Single().Tick());
        CombatSetup completed = engine.CompletedSetup();
        Assert.Equal(("renfort", 2000L), (completed.Combatants[^1].Id, completed.Combatants[^1].ArrivalMs));
        Assert.Equal(3, completed.Commands.Count);
        Assert.Equal(new[] { "renfort" }, engine.JoinedCombatants.Select(c => c.Id));
        AssertReplaysIdentically(engine, live);
        Assert.Equal(("joined", 40L), (engine.Result!["renfort"].Presence, engine.Result["renfort"].ArrivedTick));
    }

    [Fact]
    public void Un_ennemi_rejoint_un_combat_positionne()
    {
        var live = new MemoryCombatLog();
        var engine = new CombatEngine(Spatial(Arena(20), new CombatantSetup[] { Player("bastion", At("0", "0")), Creature("ogre", At("6", "0")) },
            new[] { Move(0, "bastion", "1", "0") }, NoUpkeep, 4000), live);
        while (engine.CurrentTick < 30) engine.Step();
        engine.Join(Creature("loup", At("-4", "0"), firstAttackMs: 500).Arriving(1500));
        while (engine.Step()) { }
        Assert.Equal(30L, Lines(live.Lines, "combatantJoined").Single().Tick());
        Assert.Contains(Lines(live.Lines, "attackStarted"), a => a.Str("actor") == "loup" && a.Tick() == 40);
        AssertReplaysIdentically(engine, live);
    }

    [Fact]
    public void Arrivee_pilotee_refusee_sans_rien_changer()
    {
        var engine = new CombatEngine(Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Tank(), Dummy() }, durationMs: 2000,
            rules: new CombatRules { MaxCombatants = 3 }), NullCombatLog.Instance);
        engine.Step();
        Assert.Throws<ArgumentException>(() => engine.Join(Tank("renfort")));                     // sans instant d'arrivée
        Assert.Throws<ArgumentException>(() => engine.Join(Tank("renfort").Arriving(0)));         // tick passé
        Assert.Throws<ArgumentException>(() => engine.Join(Tank("renfort").Arriving(100)));       // tick à venir
        Assert.Throws<ArgumentException>(() => engine.Join(Tank("mannequin").Arriving(50)));      // identifiant pris
        engine.Join(Tank("renfort").Arriving(50));
        var full = Assert.Throws<ArgumentException>(() => engine.Join(Tank("second").Arriving(50)));   // 4 combattants, au plus 3
        Assert.Contains("combatants", full.Message);
        Assert.Equal(new[] { "bastion", "mannequin", "renfort" }, engine.CombatantIds);
        Assert.Throws<ArgumentException>(() => engine.Submit(Cmd(50, Gratuite, "second")));       // inconnu
        while (engine.Step()) { }
        Assert.Throws<InvalidOperationException>(() => engine.Join(Tank("tard").Arriving(2000)));
    }

    // --- Propriété -------------------------------------------------------------------------------------------

    private static readonly CombatRules Rules = new() { GuardUpkeepPerSecond = 0, TelegraphMinReactionMs = 0, GlobalCooldownMs = 50 };

    private static readonly string[] Skills = { Frappe, Entaille, Assommer, Onde, Gratuite, Heurt };

    internal sealed record Plan(CombatSetup Base, List<CombatantSetup> Joins, List<CombatCommand> Live);

    private static Fixed Between(Random random, int minRaw, int maxRaw) => Fixed.FromRaw(random.Next(minRaw, maxRaw + 1));

    private static GroundVector Direction(Random random)
    {
        while (true)
        {
            var v = new GroundVector(Between(random, -10_000, 10_000), Between(random, -10_000, 10_000));
            if (!v.IsZero) return v;
        }
    }

    private static AssailantAttack RandomAttack(Random random, int k, bool spatial)
    {
        if (!spatial || random.Next(3) == 0)
            return random.Next(3) == 0 ? Strike($"a{k}", 40, EdS.Domain.Stats.DamageType.Physique, false, Apply("bleed", "0.2")) : Blow(raw: 30 + random.Next(60), id: $"a{k}");
        long telegraph = 50L * random.Next(0, 30) + 1500;
        return random.Next(3) switch
        {
            0 => new AssailantAttack($"a{k}", 40, applies: null, shape: AttackShape.Target, telegraphMs: telegraph - 1500, range: Between(random, 10_000, 40_000)),
            1 => new AssailantAttack($"a{k}", 40, shape: AttackShape.Circle, telegraphMs: telegraph, radius: Between(random, 5_000, 25_000),
                center: random.Next(2) == 0 ? ShapeCenter.Target : ShapeCenter.Self),
            _ => new AssailantAttack($"a{k}", 40, shape: AttackShape.Cone, telegraphMs: telegraph, range: Between(random, 10_000, 50_000), angle: random.Next(30, 181)),
        };
    }

    internal static Plan RandomPlan(Random random)
    {
        bool spatial = random.Next(2) == 0;
        CombatSpace? space = spatial ? Arena(8, new SpaceObstacle("mur", new[] { V("-1", "-1"), V("1", "-1"), V("1", "1"), V("-1", "1") })) : null;
        int heroes = random.Next(2, 4), creatures = random.Next(1, 4);
        var all = new List<CombatantSetup>();
        var live = new HashSet<string>(StringComparer.Ordinal);
        long seed = random.Next();
        CombatSetup Build(IEnumerable<CombatantSetup> combatants, IEnumerable<CombatCommand> commands) => space is null
            ? Setup(commands, combatants, seed, 4000, Rules)
            : Spatial(space, combatants, commands, Rules, 4000, seed);
        // La cible de scénario d'un assaillant est un héros connu du combat de départ (pas une arrivée pilotée).
        string[] Targets() => all.Where(c => c is CharacterSetup && !live.Contains(c.Id)).Select(c => c.Id).ToArray();
        for (int i = 0; i < heroes + creatures; i++)
            for (int attempt = 0; attempt < 1000; attempt++)
            {
                bool hero = i < heroes;
                CombatantPlacement? placement = spatial
                    ? new CombatantPlacement(new GroundVector(Between(random, -70_000, 70_000), Between(random, -70_000, 70_000)),
                        hero ? Fixed.Parse("0.5") : Between(random, 3_000, 9_000), Direction(random), hero ? null : Between(random, 20_000, 60_000))
                    : null;
                long? arrival = i > 0 && i != heroes && random.Next(5) < 2 ? 50L * random.Next(0, 60) : (long?)null;
                CombatantSetup candidate = hero
                    ? new CharacterSetup($"h{i}", "joueurs", Essai, new CharacterBuild("class.essai", 10, Attrs(vitalite: 20 + random.Next(150)), null,
                        Skills.OrderBy(_ => random.Next()).Take(4).ToArray(), Ultime, null), random.Next(3), placement, arrival)
                    : new AssailantSetup($"c{i}", "cibles", Fixed.Zero, Fixed.Zero, Targets()[random.Next(Targets().Length)], 50L * random.Next(0, 20), 50L * random.Next(10, 40),
                        Enumerable.Range(0, random.Next(1, 4)).Select(k => RandomAttack(random, k, spatial)).ToArray(), random.Next(3),
                        health: random.Next(2) == 0 ? null : 300 + random.Next(1500), placement: placement, arrivalMs: arrival);
                var trial = all.Append(candidate).ToList();
                string path = $"combatants[{trial.Count - 1}].";
                if (Build(trial, Array.Empty<CombatCommand>()).Validate().Any(v => v.Fields.Any(f => f.StartsWith(path, StringComparison.Ordinal)))) continue;
                all.Add(candidate);
                if (arrival != null && random.Next(2) == 0) live.Add(candidate.Id);   // arrivée pilotée (Join) plutôt que du scénario
                break;
            }
        string[] ids = all.Select(c => c.Id).ToArray();
        var commands = new List<CombatCommand>();
        foreach (CombatantSetup c in all)
        {
            for (long t = 0; t < 4000; t += 50 * random.Next(2, 12))
            {
                int roll = random.Next(12);
                if (spatial && roll < 3) commands.Add(new CombatCommand(t, c.Id, Direction(random)));
                else if (spatial && roll < 4) commands.Add(Stop(t, c.Id));
                else if (c is not CharacterSetup ch) continue;
                else if (roll < 8) commands.Add(new CombatCommand(t, c.Id, ch.Build.ActiveSkills[random.Next(4)], random.Next(5) == 0 ? null : ids[random.Next(ids.Length)]));
                else if (roll < 9) commands.Add(GuardStart(t, c.Id));
                else if (roll < 10) commands.Add(GuardStop(t, c.Id));
                else if (roll < 11) commands.Add(Parry(t, c.Id));
                else if (spatial) commands.Add(CombatCommand.SelectTarget(t, c.Id, ids[random.Next(ids.Length)]));
            }
            if (random.Next(4) == 0) commands.Add(Leave(50L * random.Next(0, 80), c.Id));
        }
        commands = commands.OrderBy(c => c.AtMs).ToList();
        // Le scénario de départ ne connaît pas les arrivées pilotées : leurs commandes, et celles qui les visent, sont soumises en direct ;
        // une partie des autres aussi.
        bool Mentions(CombatCommand c) => live.Contains(c.Actor) || (c.Target != null && live.Contains(c.Target));
        var baseCommands = commands.Where(c => !Mentions(c) && random.Next(2) == 0).ToList();
        var liveCommands = commands.Except(baseCommands).ToList();
        CombatSetup baseSetup = Build(all.Where(c => !live.Contains(c.Id)), baseCommands);
        return new Plan(baseSetup, all.Where(c => live.Contains(c.Id)).ToList(), liveCommands);
    }

    [Fact]
    public void Arrivees_et_departs_aleatoires_rejoues_a_l_identique_et_absents_inertes()
    {
        var random = new Random(1144);
        int joined = 0, scenarioArrivals = 0, refused = 0, left = 0, cancelled = 0, absentActor = 0, absentTarget = 0, submitted = 0;
        for (int n = 0; n < 150; n++)
        {
            Plan plan = RandomPlan(random);
            var log = new MemoryCombatLog();
            var engine = new CombatEngine(plan.Base, log);
            var known = new HashSet<string>(plan.Base.Combatants.Select(c => c.Id), StringComparer.Ordinal);
            while (!engine.IsFinished)
            {
                long tick = engine.CurrentTick;
                foreach (CombatantSetup c in plan.Joins.Where(c => c.ArrivalMs / 50 == tick))
                {
                    engine.Join(c);
                    known.Add(c.Id);
                    joined++;
                }
                var batch = plan.Live.Where(c => c.AtMs / 50 == tick && known.Contains(c.Actor) && (c.Target is null || known.Contains(c.Target))).ToList();
                foreach (var group in batch.GroupBy(c => c.Actor))
                    try
                    {
                        engine.SubmitAll(group.ToList());
                        submitted += group.Count();
                    }
                    catch (InvalidOperationException) { }   // plafond par acteur et par tick
                engine.Step();
            }
            AssertReplaysIdentically(engine, log);

            // Présence d'après le journal : présent depuis le départ ou son arrivée, jusqu'au tick de son départ compris.
            var from = new Dictionary<string, long>(StringComparer.Ordinal);
            var until = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (JsonElement c in Lines(log.Lines, "combatant")) from[c.Str("combatant")!] = 0;
            foreach (JsonElement c in Lines(log.Lines, "combatantJoined")) from[c.Str("combatant")!] = c.Tick();
            foreach (JsonElement c in Lines(log.Lines, "combatantLeft")) until[c.Str("combatant")!] = c.Tick();
            bool Present(string? id, long t) => id != null && from.TryGetValue(id, out long f) && f <= t && (!until.TryGetValue(id, out long u) || t <= u);
            foreach (string kind in new[] { "skillCast", "attackStarted", "guardStarted", "parryStarted", "moveStarted", "targetSelected" })
                foreach (JsonElement e in Lines(log.Lines, kind)) Assert.True(Present(e.Str("actor"), e.Tick()), $"{kind} d'un absent : {e}");
            foreach (JsonElement e in Lines(log.Lines, "damage"))
            {
                Assert.True(Present(e.Str("target"), e.Tick()), $"coup sur un absent : {e}");
                Assert.True(Present(e.Str("source"), e.Tick()), $"coup d'un absent : {e}");
            }
            foreach (JsonElement e in Lines(log.Lines, "dotDamage")) Assert.True(Present(e.Str("target"), e.Tick()), $"DOT sur un absent : {e}");
            foreach (JsonElement e in Lines(log.Lines, "moved"))
                foreach (JsonElement c in e.GetProperty("combatants").EnumerateArray()) Assert.True(Present(c.Str("combatant"), e.Tick()), $"un absent bouge : {e}");
            foreach (JsonElement e in Lines(log.Lines, "attackLanded"))
                foreach (JsonElement t in e.GetProperty("targets").EnumerateArray()) Assert.True(Present(t.GetString(), e.Tick()), $"zone sur un absent : {e}");
            scenarioArrivals += Lines(log.Lines, "combatantJoined").Count;
            refused += Lines(log.Lines, "arrivalRefused").Count;
            left += Lines(log.Lines, "combatantLeft").Count;
            cancelled += Lines(log.Lines, "leaveCancelled").Count;
            absentActor += Lines(log.Lines, "commandRejected").Count(r => r.Str("reason") == "actorAbsent");
            absentTarget += Lines(log.Lines, "commandRejected").Concat(Lines(log.Lines, "attackSkipped")).Count(r => r.Str("reason") == "targetAbsent");
        }
        string counts = $"pilotées {joined}, arrivées {scenarioArrivals}, refusées {refused}, départs {left}, annulés {cancelled}, acteurs absents {absentActor}, cibles absentes {absentTarget}, soumises {submitted}";
        // Relevé au tirage 1144 : pilotées 76, arrivées 144, refusées 4, départs 152, acteurs absents 722, cibles absentes 206, soumises 2673
        // (une mort au tick même du départ est rare ici : leaveCancelled est couvert par Lot114dPresenceTests).
        Assert.True(joined > 50 && scenarioArrivals > 100 && refused > 0 && left > 100 && absentActor > 400 && absentTarget > 120 && submitted > 2000, counts);
    }
}
