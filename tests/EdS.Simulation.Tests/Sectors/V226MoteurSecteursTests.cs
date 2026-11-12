using System.Text.Json;
using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Space.SpaceTestKit;

namespace EdS.Simulation.Tests.Sectors;

/// <summary>
/// Sous-lot V2.26 (secteurs de simulation, PLAN_GRANDE_CARTE.md § 2.4 ; rupture retenue par Damien le 11 novembre 2026), côté moteur :
/// arrivée avec un état emporté (journal 25, moteur 0.31.0), recyclage des places d'une zone, coordonnées ±4 096 m, 256 obstacles rangés
/// par cases, grille de navigation agrandie.
/// </summary>
[Trait("Category", "Sector")]
public class V226MoteurSecteursTests
{
    // --- Outils --------------------------------------------------------------------------------------------------------------------

    private static List<JsonElement> Lines(MemoryCombatLog log, string kind) =>
        log.Lines.Skip(1).Select(l => JsonDocument.Parse(l).RootElement.Clone()).Where(e => e.GetProperty("event").GetString() == kind).ToList();

    private static CharacterSetup Carrying(CharacterSetup c, long arrivalMs, CarriedState? carried, CombatantPlacement? placement = null,
        IEnumerable<ConsumableSetup>? consumables = null) =>
        new(c.Id, c.Team, c.Class, c.Build, c.Initiative, placement ?? c.Placement, arrivalMs, c.Gear, consumables ?? c.Consumables, c.Shield, c.Equipment, carried);

    private static AssailantSetup Carrying(AssailantSetup a, long arrivalMs, CarriedState? carried) =>
        new(a.Id, a.Team, a.Armor, a.MagicResistance, a.Target, a.FirstAttackMs, a.IntervalMs, a.Attacks, a.Initiative, a.StatusResistance, a.Triggers,
            a.Health, a.Placement, arrivalMs, a.Behavior, carried);

    private static CombatSetup ZoneSetup(CombatSpace space, IEnumerable<CombatantSetup> combatants, IEnumerable<CombatCommand>? commands = null,
        CombatRules? rules = null, long durationMs = 10_000) =>
        new(42, durationMs, EdS.Domain.Stats.StatRules.Provisional, DamageRules.Provisional, ProgressionRules.Provisional, rules ?? NoUpkeep,
            combatants, commands ?? Array.Empty<CombatCommand>(), Identity, space, zone: true);

    // --- Versions et bornes ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Versions_et_bornes_du_sous_lot()
    {
        Assert.Equal(("0.31.0", 25), (CombatEngine.Version, CombatEngine.LogFormatVersion));
        Assert.Equal((4096, 256), (SpaceLimits.MaxCoordinateMeters, SpaceLimits.MaxObstacles));
        Assert.Equal(Fixed.FromInt(4096), SpaceLimits.MaxCoordinate);
        Assert.Equal(2L * 4096 * Fixed.Scale, Planar.MaxComponent);
        Assert.Equal(524_288, NavGrid.MaxCells);
        Assert.Equal(1024, CombatRules.MaxSessionCombatants);
    }

    [Theory]
    [InlineData(81_920_000L, 81_920_000L)]     // écart maximal sur les deux axes (deux coins opposés de ±4 096 m)
    [InlineData(-81_920_000L, 1L)]
    [InlineData(30_000_000L, -30_000_001L)]    // au-delà de l'ancien palier de précision (somme des carrés > 9,2·10¹⁴)
    [InlineData(20_000_000L, 20_000_000L)]     // l'ancien maximum : palier inchangé
    public void Normalisation_sans_debordement_jusqu_aux_bornes(long x, long z)
    {
        Assert.True(Planar.TryNormalize(x, z, out long ux, out long uz));
        long length = IntMath.Sqrt(Planar.LengthSquared(ux, uz));
        Assert.InRange(length, Planar.Unit - 2, Planar.Unit + 2);
        Assert.True(Planar.TryResize(x, z, Planar.MaxComponent, out long rx, out long rz));
        Assert.InRange(Math.Abs(rx), 0, Planar.MaxComponent + 1);
        Assert.InRange(Math.Abs(rz), 0, Planar.MaxComponent + 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => Planar.TryResize(Planar.MaxComponent + 1, 0, 1, out _, out _));
    }

    [Fact]
    public void Ancien_palier_de_precision_inchange()
    {
        // Propriété : pour tout vecteur d'avant V2.26 (composantes ≤ 2·10⁷), même résultat qu'avec l'ancien calcul (palier 10 000 ou 100).
        var random = new Random(2026);
        for (int i = 0; i < 5000; i++)
        {
            long x = random.NextInt64(-20_000_000, 20_000_001), z = random.NextInt64(-20_000_000, 20_000_001);
            if (x == 0 && z == 0) continue;
            long squared = Planar.LengthSquared(x, z);
            long precision = squared <= 92_000_000_000L ? 10_000 : 100;
            long norm = IntMath.Sqrt(squared * precision * precision);
            long ex = IntMath.MulDiv(x, Planar.Unit * precision, norm, RoundingMode.HalfAwayFromZero);
            long ez = IntMath.MulDiv(z, Planar.Unit * precision, norm, RoundingMode.HalfAwayFromZero);
            Assert.True(Planar.TryNormalize(x, z, out long ux, out long uz));
            Assert.Equal((ex, ez), (ux, uz));
        }
    }

    [Fact]
    public void Combat_au_coin_de_la_region_jusqu_a_4096_m()
    {
        // Un secteur collé au coin nord-est des bornes techniques : la marche, le contact et le bord de l'aire fonctionnent.
        CombatSpace space = Box("3776", "3776", "4096", "4096", Rect("rocher", "4090", "4090", "4095", "4095"));
        var setup = Spatial(space, new CombatantSetup[] { Player("bastion", At("4093", "4085")), PlacedDummy("souche", At("4080", "4080")) },
            new[] { Move(0, "bastion", "0", "1"), Move(2000, "bastion", "1", "0") }, NoUpkeep, 6000);
        var (track, log, engine) = Trace(setup);
        GroundVector end = engine.View("bastion").Position!.Value;
        Assert.True(end.X <= Fixed.FromInt(4096) - Fixed.Parse("0.5") && end.Z <= Fixed.FromInt(4096) - Fixed.Parse("0.5"));
        var rock = new ConvexPolygon(space.Obstacles[0].Points);
        foreach (GroundVector p in track["bastion"]) Assert.False(rock.OverlapsCircle(p.X.Raw, p.Z.Raw, 5_000 - 1));
        Assert.NotEmpty(Lines(log, "moved"));
    }

    // --- Obstacles rangés par cases ------------------------------------------------------------------------------------------------

    private static ConvexPolygon RandomPolygon(Random random, long spread, long offset)
    {
        while (true)
        {
            long cx = Math.Min(offset + random.NextInt64(-spread, spread), SpaceLimits.MaxCoordinate.Raw - 200_000), cz = random.NextInt64(-spread, spread);
            long w = random.NextInt64(1_000, 200_000), h = random.NextInt64(1_000, 200_000);
            var points = random.Next(2) == 0
                ? new[] { P(cx, cz), P(cx + w, cz), P(cx + w, cz + h), P(cx, cz + h) }
                : new[] { P(cx, cz), P(cx + w, cz + h / 3), P(cx + w / 2, cz + h) };
            if (ConvexPolygon.IsStrictlyConvex(points, out _)) return new ConvexPolygon(points);
        }
        static GroundVector P(long x, long z) => new(Fixed.FromRaw(x), Fixed.FromRaw(z));
    }

    [Theory]
    [InlineData(1, 0L)]
    [InlineData(2, 2_000_000L)]
    [InlineData(3, 40_000_000L)]   // obstacles jusqu'aux bornes de ±4 096 m : cases doublées
    public void Index_des_obstacles_identique_au_parcours_de_tous(int seed, long offset)
    {
        var random = new Random(seed);
        long spread = 1_600_000;   // ±160 m autour du décalage
        var polygons = Enumerable.Range(0, SpaceLimits.MaxObstacles).Select(_ => RandomPolygon(random, spread, offset)).ToList();
        var index = new ObstacleIndex(polygons);
        var buffer = new int[polygons.Count];
        for (int q = 0; q < 3000; q++)
        {
            long x = random.NextInt64(-spread, spread) + offset, z = random.NextInt64(-spread, spread);
            long bx = x + random.NextInt64(-300_000, 300_000), bz = z + random.NextInt64(-300_000, 300_000);
            long r = random.NextInt64(1, 60_000);
            Assert.Equal(polygons.Any(p => p.OverlapsCircle(x, z, r)), index.AnyOverlapsCircle(x, z, r));
            Assert.Equal(polygons.Any(p => p.OverlapsCapsule(x, z, bx, bz, r)), index.AnyOverlapsCapsule(x, z, bx, bz, r));
            Assert.Equal(polygons.Any(p => p.IntersectsSegment(x, z, bx, bz)), index.AnyIntersectsSegment(x, z, bx, bz));
            // Candidats : dans l'ordre des obstacles, sans doublon, et tous ceux dont la boîte touche la boîte demandée.
            int n = index.Candidates(x - r, z - r, x + r, z + r, buffer);
            int[] got = buffer.Take(n).ToArray();
            Assert.Equal(got.OrderBy(i => i).Distinct(), got);
            for (int i = 0; i < polygons.Count; i++)
            {
                ConvexPolygon p = polygons[i];
                bool touches = p.MaxX >= x - r && p.MinX <= x + r && p.MaxZ >= z - r && p.MinZ <= z + r;
                if (touches) Assert.Contains(i, got);
                if (p.OverlapsCircle(x, z, r)) Assert.Contains(i, got);
            }
        }
    }

    [Fact]
    public void Deux_cent_cinquante_six_obstacles_admis_et_journal_identique_a_un_parcours_naif()
    {
        // 256 obstacles (la borne), dont la plupart loin du combat : la marche, la poursuite et la ligne de vue sont exactement celles que
        // donnerait le seul décor proche (mêmes lignes que le même combat avec ses quatre seuls obstacles proches).
        var near = new[] { Rect("mur", "-1", "3", "4", "4"), Rect("pilier", "6", "-2", "7", "-1"), Rect("caisse", "-5", "-4", "-4", "-3"),
            new SpaceObstacle("muret", new[] { V("-3", "-1"), V("-2", "-1"), V("-2", "1") }, low: true) };
        var far = Enumerable.Range(0, SpaceLimits.MaxObstacles - near.Length)
            .Select(i => Rect($"loin{i}", $"{-150 + (i % 16) * 18}", $"{60 + (i / 16) * 5}", $"{-149 + (i % 16) * 18}", $"{61 + (i / 16) * 5}")).ToArray();
        CombatantSetup[] fighters = { Player("bastion", At("0", "0")),
            Creature("loup", At("2", "8", "0.6"), health: 300, firstAttackMs: 500, team: "meute") };
        var chase = new AssailantSetup("loup", "meute", Fixed.Zero, Fixed.Zero, null, 500, 1500, new[] { Blow(raw: 5) }, health: 300,
            placement: At("2", "8", "0.6"), behavior: new AssailantBehavior(Fixed.Parse("0.5"), null, null));
        fighters[1] = chase;
        CombatCommand[] commands = { Move(0, "bastion", "1", "-1"), Stop(1500, "bastion"), Move(2500, "bastion", "-1", "0"), Stop(4000, "bastion") };
        var all = new CombatSpace(V(-160, -160), V(160, 160), near.Concat(far));
        var few = new CombatSpace(V(-160, -160), V(160, 160), near);
        var withAll = new MemoryCombatLog();
        var withFew = new MemoryCombatLog();
        CombatEngine.Run(Spatial(all, fighters, commands, NoUpkeep, 6000), withAll);
        CombatEngine.Run(Spatial(few, fighters, commands, NoUpkeep, 6000), withFew);
        Assert.Equal(withFew.Lines.Skip(1), withAll.Lines.Skip(1));
        Assert.NotEmpty(Lines(withAll, "moved"));
        Assert.Equal(SpaceLimits.MaxObstacles, all.Obstacles.Count);
        Assert.Empty(all.Validate());
    }

    // --- Grille de navigation -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Grille_d_un_secteur_et_de_sa_marge_en_cases_de_50_cm()
    {
        // 320 m de côté (secteur de 256 m et marge de 32 m) : 640 × 640 = 409 600 cases de 0,5 m (au-delà de l'ancienne borne de 262 144).
        long half = 1_600_000;
        var wall = new ConvexPolygon(new[] { V("-1", "-20"), V("1", "-20"), V("1", "20"), V("-1", "20") });
        var grid = new NavGrid(-half, -half, half, half, new[] { wall }, 5_000);
        Assert.Equal((5_000L, 640, 640, 409_600), (grid.Cell, grid.Width, grid.Height, grid.Count));
        // Chemin d'un coin à l'autre autour du mur : les indices de case au-delà de 2¹⁸ tiennent dans la clé du tas.
        int start = grid.NearestFree(-1_550_000, -1_550_000, 2), goal = grid.NearestFree(1_550_000, 1_550_000, 2);
        Assert.True(goal >= 1 << 18);
        var path = new int[4096];
        NavStatus status = new NavSearch().Find(grid, start, goal, 131_072, path, out int length);
        Assert.Equal(NavStatus.Found, status);
        Assert.Equal(goal, path[length - 1]);
        // 256 m de côté : exactement la grille d'avant.
        var small = new NavGrid(-1_280_000, -1_280_000, 1_280_000, 1_280_000, Array.Empty<ConvexPolygon>(), 5_000);
        Assert.Equal((5_000L, 512, 512), (small.Cell, small.Width, small.Height));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavSearch().Find(grid, start, goal, NavSearch.MaxExpandedLimit + 1, path, out _));
    }

    // --- État emporté ---------------------------------------------------------------------------------------------------------------

    private static CombatSetup Duel(IEnumerable<CombatantSetup> combatants, IEnumerable<CombatCommand> commands, long durationMs = 20_000) =>
        Setup(commands, combatants, durationMs: durationMs, rules: NoUpkeep);

    [Fact]
    public void Arrivee_avec_etat_emporte_ressources_et_recharges()
    {
        CharacterSetup tank = Tank(endurance: 50);
        var carried = new CarriedState(health: 1000, endurance: 20, enduranceRegenMs: 500, resonance: 40, rampartCharges: 2, globalCooldownMs: 500,
            cooldowns: new[] { new KeyValuePair<string, long>(Frappe, 3000) });
        var setup = Duel(new CombatantSetup[] { Carrying(tank, 1000, carried), Dummy(), Assailant() },
            new[] { Cmd(1000, Gratuite), Cmd(1500, Frappe), Cmd(4000, Frappe) });
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log);
        while (engine.Step()) { }
        JsonElement joined = Lines(log, "combatantJoined").Single();
        Assert.Equal((20L, 1000L, 20L, 40L, 2L), (joined.GetProperty("tick").GetInt64(), joined.GetProperty("health").GetInt64(),
            joined.GetProperty("endurance").GetInt64(), joined.GetProperty("resonance").GetInt64(), joined.GetProperty("rampartCharges").GetInt64()));
        // Recharge globale emportée (500 ms) : la gratuite du tick d'arrivée est refusée ; la frappe (3 s restantes) aussi à 1,5 s.
        List<JsonElement> rejected = Lines(log, "commandRejected");
        Assert.Contains(rejected, r => r.GetProperty("tick").GetInt64() == 20 && r.GetProperty("reason").GetString() == "globalCooldown");
        Assert.Contains(rejected, r => r.GetProperty("tick").GetInt64() == 30 && r.GetProperty("skill").GetString() == Frappe && r.GetProperty("reason").GetString() == "cooldown");
        Assert.Contains(Lines(log, "cooldownReady"), r => r.GetProperty("tick").GetInt64() == 80 && r.GetProperty("skill").GetString() == Frappe);
        Assert.Contains(Lines(log, "skillCast"), r => r.GetProperty("tick").GetInt64() == 80 && r.GetProperty("skill").GetString() == Frappe);
        // Régénération : reprend 500 ms après l'arrivée (tick 30 ; premier point entier un tick plus tard).
        Assert.InRange(Lines(log, "enduranceRegenerated").First().GetProperty("tick").GetInt64(), 30L, 31L);
        // Rejeu et instantané : le combat avec état se rejoue et se reprend à l'identique.
        var replay = new MemoryCombatLog();
        CombatEngine.Run(setup, replay);
        Assert.Equal(log.Lines, replay.Lines);
        CausalGraph.Build(log.Lines);
    }

    [Fact]
    public void Instantane_d_un_combattant_arrive_avec_etat_relu_et_verifie()
    {
        var carried = new CarriedState(health: 900, endurance: 10, resonance: 15, globalCooldownMs: 250,
            cooldowns: new[] { new KeyValuePair<string, long>(Heurt, 5000) }, consumableCooldownMs: 1000);
        CharacterSetup tank = Tank(endurance: 30);
        var potion = new[] { new ConsumableSetup("item.potion_test", 50, 2, 2000) };
        var setup = Duel(new CombatantSetup[] { Carrying(tank, 500, carried, consumables: potion), Dummy(), Assailant() },
            new[] { Cmd(2000, Gratuite), Cmd(3000, Frappe), CombatCommand.Use(3500, "bastion", "item.potion_test") });
        var engine = new CombatEngine(setup, NullCombatLog.Instance);
        for (int t = 0; t < 120; t++)
        {
            engine.Step();
            if (t % 9 != 3) continue;
            CombatSnapshot snapshot = engine.Snapshot();
            CombatSnapshot parsed = CombatSnapshot.Parse(snapshot.Text);
            Assert.Equal(snapshot.Text, CombatEngine.Restore(setup, parsed, NullCombatLog.Instance).Snapshot().Text);
            Assert.Equal(snapshot.Text, CombatEngine.RestoreVerified(setup, parsed, NullCombatLog.Instance).Snapshot().Text);
        }
    }

    [Fact]
    public void Etat_exporte_puis_importe_donne_le_meme_combattant()
    {
        // Propriété : à n'importe quel tick, l'état emporté (Carried) d'un personnage qui a combattu, réimporté dans une autre session au
        // tick suivant, donne les mêmes PV, ressources et recharges restantes.
        var random = new Random(226);
        for (int run = 0; run < 12; run++)
        {
            var commands = Enumerable.Range(0, 30).Select(i => Cmd(250L * random.Next(1, 60), new[] { Frappe, Heurt, Gratuite, Onde }[random.Next(4)]))
                .GroupBy(c => (c.AtMs, c.Skill)).Select(g => g.First()).ToArray();
            var potion = new[] { new ConsumableSetup("item.potion_test", 40, 3, 1500) };
            CharacterSetup tank = Character(attributes: Attrs(vitalite: 60, endurance: 20));
            var setup = Duel(new CombatantSetup[] { Carrying(tank, 0, null, consumables: potion), Dummy(), Assailant(intervalMs: 700) },
                commands.Append(CombatCommand.Use(5000, "bastion", "item.potion_test")), 20_000);
            var a = new CombatEngine(setup, NullCombatLog.Instance);
            int stop = random.Next(30, 300);
            for (int t = 0; t < stop; t++) a.Step();
            if (a.View("bastion").IsDead) continue;
            CarriedState carried = a.Carried("bastion");
            long at = a.CurrentTick * 50;
            var other = Duel(new CombatantSetup[] { Dummy("souche"), Carrying(tank, at, carried, consumables: a.CarriedConsumables("bastion")) },
                Array.Empty<CombatCommand>(), checked(at + 5000));
            var b = new CombatEngine(other, NullCombatLog.Instance);
            while (b.CurrentTick <= a.CurrentTick) b.Step();
            CombatantView va = a.View("bastion"), vb = b.View("bastion");
            Assert.Equal((va.Health, va.Resonance, va.RampartCharges), (vb.Health, vb.Resonance, vb.RampartCharges));
            Assert.InRange(vb.Endurance, va.Endurance, va.Endurance + 1);   // la régénération reprend au tick d'arrivée (au point près)
            long next = a.CurrentTick;
            long Left(long ready) => Math.Max(0, ready - next);
            Assert.Equal(Left(va.GlobalCooldownReadyTick), Left(vb.GlobalCooldownReadyTick));
            foreach (var cooldown in va.CooldownReadyTicks.Where(c => c.Value > next))
                Assert.Equal(cooldown.Value, vb.CooldownReadyTicks[cooldown.Key]);
            Assert.Equal(Left(va.ConsumableReadyTick) > 0 && va.ConsumableCharges.Values.Any(v => v > 0) ? Left(va.ConsumableReadyTick) : 0,
                Left(vb.ConsumableReadyTick));
            Assert.Equal(va.ConsumableCharges.Where(c => c.Value > 0), vb.ConsumableCharges.Where(c => c.Value > 0));
        }
    }

    [Fact]
    public void Etat_emporte_hors_bornes_refuse_avec_son_chemin()
    {
        CharacterSetup tank = Tank();
        long max = new CombatEngine(Duel(new CombatantSetup[] { tank, Dummy() }, Array.Empty<CombatCommand>()), NullCombatLog.Instance).View("bastion").MaxHealth!.Value;
        string? Path(CombatantSetup c) =>
            Duel(new CombatantSetup[] { c, Dummy() }, Array.Empty<CombatCommand>()).Validate().FirstOrDefault()?.Fields.Single();
        KeyValuePair<string, long> Cd(string skill, long ms) => new(skill, ms);
        Assert.Null(Path(Carrying(tank, 500, new CarriedState(health: max))));
        Assert.Equal("combatants[0].carried", Path(Carrying(tank, 0, new CarriedState(health: 1)) is var present
            ? new CharacterSetup(present.Id, present.Team, present.Class, present.Build, carried: new CarriedState(health: 1)) : null!));
        Assert.Equal("combatants[0].carried.health", Path(Carrying(tank, 500, new CarriedState(health: max + 1))));
        Assert.Equal("combatants[0].carried.health", Path(Carrying(tank, 500, new CarriedState(health: 0))));
        Assert.Equal("combatants[0].carried.endurance", Path(Carrying(tank, 500, new CarriedState(endurance: -1))));
        Assert.Equal("combatants[0].carried.enduranceRegenMs", Path(Carrying(tank, 500, new CarriedState(enduranceRegenMs: 500))));
        Assert.Equal("combatants[0].carried.enduranceRegenMs", Path(Carrying(tank, 500, new CarriedState(endurance: 0, enduranceRegenMs: 30))));
        Assert.Equal("combatants[0].carried.resonance", Path(Carrying(tank, 500, new CarriedState(resonance: 101))));
        Assert.Equal("combatants[0].carried.rampartCharges", Path(Carrying(tank, 500, new CarriedState(rampartCharges: -1))));
        Assert.Equal("combatants[0].carried.globalCooldownMs", Path(Carrying(tank, 500, new CarriedState(globalCooldownMs: 60_000))));
        Assert.Equal("combatants[0].carried.cooldowns." + NonEquipee, Path(Carrying(tank, 500, new CarriedState(cooldowns: new[] { Cd(NonEquipee, 50) }))));
        Assert.Equal("combatants[0].carried.cooldowns." + Gratuite, Path(Carrying(tank, 500, new CarriedState(cooldowns: new[] { Cd(Gratuite, 50) }))));
        Assert.Equal("combatants[0].carried.cooldowns." + Frappe, Path(Carrying(tank, 500, new CarriedState(cooldowns: new[] { Cd(Frappe, 6050) }))));
        Assert.Equal("combatants[0].carried.cooldowns." + Frappe, Path(Carrying(tank, 500, new CarriedState(cooldowns: new[] { Cd(Frappe, 75) }))));
        Assert.Equal("combatants[0].carried.consumableCooldownMs", Path(Carrying(tank, 500, new CarriedState(consumableCooldownMs: 50))));
        Assert.Throws<ArgumentException>(() => new CarriedState(cooldowns: new[] { Cd(Frappe, 50), Cd(Frappe, 100) }));
        // Un assaillant n'emporte que ses PV, dans [1, ses PV].
        AssailantSetup wolf = Creature("loup", At("0", "0"), health: 100);
        AssailantSetup plain = new(wolf.Id, "meute", wolf.Armor, wolf.MagicResistance, "mannequin", wolf.FirstAttackMs, wolf.IntervalMs, wolf.Attacks, health: 100);
        Assert.Null(Path(Carrying(plain, 500, new CarriedState(health: 60))));
        Assert.Equal("combatants[0].carried.health", Path(Carrying(plain, 500, new CarriedState(health: 101))));
        Assert.Equal("combatants[0].carried", Path(Carrying(plain, 500, new CarriedState(endurance: 10))));
    }

    [Fact]
    public void Etat_emporte_arrivee_refusee_sans_rien_appliquer()
    {
        // Place prise à l'arrivée : arrivalRefused, l'état n'est pas appliqué et aucune échéance de recharge n'est mise en file.
        CharacterSetup arriving = Carrying(Player("bastion", At("0", "0")), 500,
            new CarriedState(health: 100, cooldowns: new[] { new KeyValuePair<string, long>(Frappe, 1000) }));
        var setup = Spatial(Arena(), new CombatantSetup[] { Creature("ogre", At("0", "0", "1.2"), team: "cibles"), arriving },
            Array.Empty<CombatCommand>(), NoUpkeep, 4000);
        var log = new MemoryCombatLog();
        CombatEngine.Run(setup, log);
        Assert.Single(Lines(log, "arrivalRefused"));
        Assert.Empty(Lines(log, "cooldownReady"));
    }

    // --- Places recyclées -----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Zone_un_mort_ou_un_parti_libere_sa_place_sans_identite_reutilisee()
    {
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, MaxCombatants = 3 };
        CombatSpace space = Arena(30);
        AssailantSetup Wolf(string id, string x) => new(id, "meute", Fixed.Zero, Fixed.Zero, "bastion", 600_000, 2000, new[] { Blow() }, health: 1,
            placement: At(x, "0", "0.6"));
        var engine = new CombatEngine(ZoneSetup(space, new CombatantSetup[] { Player("bastion", At("0", "0")), Wolf("loup1", "5") },
            new[] { Cmd(0, Gratuite, target: "loup1") }, rules), NullCombatLog.Instance);
        Assert.Equal((2, 1), (engine.PlacesTaken, engine.FreePlaces));
        engine.Step();
        engine.Step();
        Assert.True(engine.View("loup1").IsDead);
        Assert.Equal((1, 2), (engine.PlacesTaken, engine.FreePlaces));
        // Trois places : le mort a rendu la sienne ; deux arrivées passent, la troisième est refusée sans rien changer.
        engine.Join(Wolf("loup2", "8").WithArrival(engine.CurrentTick * 50));
        engine.Join(Wolf("loup3", "-8").WithArrival(engine.CurrentTick * 50));
        Assert.Equal(0, engine.FreePlaces);
        var refused = Assert.Throws<ArgumentException>(() => engine.Join(Wolf("loup4", "12").WithArrival(engine.CurrentTick * 50)));
        Assert.Contains("place", refused.Message);
        Assert.DoesNotContain("loup4", engine.CombatantIds);
        // Une identité déjà vue (même morte) n'est jamais reprise.
        Assert.Throws<ArgumentException>(() => engine.Join(Wolf("loup1", "12").WithArrival(engine.CurrentTick * 50)));
        engine.Step();
        Assert.Equal(4, engine.CombatantIds.Count);   // plus d'identités que de places
        Assert.Equal(3, engine.PlacesTaken);
        Assert.Equal(CombatRules.MaxSessionCombatants - 4, engine.IdentitiesLeft);
        // Le combat complété (4 identités pour 3 places) se valide et se rejoue.
        CombatSetup completed = engine.CompletedSetup();
        Assert.Empty(completed.Validate());
        CombatEngine.Run(completed, NullCombatLog.Instance);
    }

    [Fact]
    public void Zone_arrivee_du_scenario_sans_place_refusee_full()
    {
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, MaxCombatants = 2 };
        var setup = ZoneSetup(Arena(30), new CombatantSetup[] { Player("bastion", At("0", "0")), Creature("loup", At("5", "0"), health: 10),
            Creature("renfort", At("-5", "0"), health: 10).WithArrival(500) }, rules: rules, durationMs: 2000);
        Assert.Empty(setup.Validate());
        var log = new MemoryCombatLog();
        CombatEngine.Run(setup, log);
        JsonElement refused = Lines(log, "arrivalRefused").Single();
        Assert.Equal(("renfort", "full"), (refused.GetProperty("combatant").GetString(), refused.GetProperty("reason").GetString()));
        // Combat fermé : la règle d'avant (identités) ; une zone : présents au départ au plus les places, identités au plus 1 024.
        Assert.Contains(Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Tank(), Dummy(), Dummy("autre") }, rules: rules).Validate(),
            v => v.Kind == RuleViolationKind.LimitExceeded);
        var crowd = ZoneSetup(Arena(30), new CombatantSetup[] { Player("bastion", At("0", "0")), Creature("a", At("5", "0")), Creature("b", At("-5", "0")) }, rules: rules);
        Assert.Contains(crowd.Validate(), v => v.Kind == RuleViolationKind.LimitExceeded && v.Fields.Single() == "combatants");
        var many = ZoneSetup(Arena(400), Enumerable.Range(0, CombatRules.MaxSessionCombatants + 1)
            .Select(i => (CombatantSetup)Creature($"c{i}", At($"{-390 + (i % 60) * 13}", $"{-390 + (i / 60) * 13}")).WithArrival(50)), rules: new CombatRules());
        Assert.Contains(many.Validate(), v => v.Kind == RuleViolationKind.LimitExceeded && v.Message.Contains("1024"));
    }
}

internal static class V226Kit
{
    public static CombatantSetup WithArrival(this CombatantSetup c, long arrivalMs) => EdS.Simulation.Tests.Presence.PresenceKit.Arriving(c, arrivalMs);
}
