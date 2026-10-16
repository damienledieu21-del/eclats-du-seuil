using System.Collections;
using System.Reflection;
using System.Text.Json;
using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Engine;

/// <summary>
/// Lot 7.2 : altérations périodiques (DOT). Brûlure (magique, réduite par la résistance magique) et saignement (physique,
/// réduit par l'armure), 6 s, un tick toutes les 2 s : exactement 3 ticks, le dernier inflige ses dégâts puis expire l'instance.
/// Puissance figée à l'application ; défense de la cible relue à chaque tick ; ni critique, ni Fragilité, ni Résonance, ni
/// compteur de coups ; renouvellement par source à la plus forte puissance, cadence d'origine conservée.
/// </summary>
[Trait("Category", "Engine")]
[Trait("Category", "Status")]
public class Lot72DotTests
{
    private static DummySetup Target(int armor = 0, int magicResistance = 0, string id = "mannequin") =>
        new(id, "joueurs", Fixed.FromInt(armor), Fixed.FromInt(magicResistance));

    /// <summary>Assaillant qui frappe « mannequin » à <paramref name="firstMs"/>, puis toutes les <paramref name="intervalMs"/>.</summary>
    private static AssailantSetup Caster(string id = "assaillant", long firstMs = 1000, long intervalMs = 60_000, params AssailantAttack[] attacks) =>
        Assailant(firstMs, intervalMs, id, "cibles", "mannequin", attacks);

    private static AssailantAttack Burn(string coefficient = "0.4", long raw = 100, string id = "brule") =>
        Strike(id, raw, DamageType.Magique, applies: Apply("burn", coefficient));

    private static AssailantAttack Bleed(string coefficient = "0.4", long raw = 100, string id = "entaille") =>
        Strike(id, raw, DamageType.Physique, applies: Apply("bleed", coefficient));

    private static (CombatResult Result, List<JsonElement> Events) Fight(long durationMs, params CombatantSetup[] fighters) =>
        Fight(durationMs, null, Array.Empty<CombatCommand>(), fighters);

    private static (CombatResult Result, List<JsonElement> Events) Fight(long durationMs, CombatRules? rules, CombatCommand[] commands,
        params CombatantSetup[] fighters)
    {
        var (result, log) = Run(Setup(commands, fighters, durationMs: durationMs, rules: rules));
        return (result, Events(log));
    }

    private static List<(long Tick, long Amount)> Dots(IEnumerable<JsonElement> events, string? source = null) =>
        events.OfKind("dotDamage").Where(e => source is null || e.Str("source") == source).Select(e => (e.Tick(), e.Int("amount"))).ToList();

    private static long Periodic(int power, DerivedStats defender, DamageType type) =>
        DamageCalculator.ComputePeriodic(Fixed.FromInt(power), defender, type, true, DamageRules.Provisional);

    private static DerivedStats Defenses(int armor, int magicResistance) =>
        CombatantStats.ForDefenses(StatRules.Provisional, Fixed.FromInt(armor), Fixed.FromInt(magicResistance));

    // --- Cadence --------------------------------------------------------------------------------

    [Theory]
    [InlineData(1000, 20)]
    [InlineData(1050, 21)]
    public void Trois_ticks_exacts_pour_six_secondes_toutes_les_deux(long firstMs, long applied)
    {
        // Brûlure 0,4 × 100 dégâts bruts = 40, sans résistance : ticks à +40, +80 et +120 ticks ; le dernier expire l'instance.
        var (result, events) = Fight(10_000, Target(), Caster(firstMs: firstMs, attacks: Burn()));
        Assert.Equal(new[] { (applied + 40, 40L), (applied + 80, 40L), (applied + 120, 40L) }, Dots(events));
        Assert.Equal(new[] { 140L, 180L, 220L }, events.OfKind("dotDamage").Select(e => e.Int("damageTaken")));

        JsonElement state = Assert.Single(events.OfKind("stateApplied"));
        Assert.Equal((applied, "burn", "dot", "assaillant", applied + 120, 40m),
            (state.Tick(), state.Str("state")!, state.Str("category")!, state.Str("source")!, state.Int("untilTick"), state.GetProperty("power").GetDecimal()));
        JsonElement expired = Assert.Single(events.OfKind("stateExpired"));
        JsonElement last = events.OfKind("dotDamage").Last();
        Assert.Equal((applied + 120, "periodic"), (expired.Tick(), expired.Str("phase")!));
        Assert.Equal(last.Int("seq") + 1, expired.Int("seq"));   // même étape : dégâts du dernier tick, puis expiration
        Assert.Equal((state.Int("instance"), "dot", "assaillant"), (expired.Int("instance"), expired.Str("category")!, expired.Str("source")!));

        // Crédités à la source, sans compter de coup : un seul impact direct.
        Assert.Equal((220L, 220L, 1L), (result["mannequin"].DamageTaken, result["assaillant"].DamageDealt, result["assaillant"].Hits));
    }

    // --- Résistances ----------------------------------------------------------------------------

    [Theory]
    [InlineData(100, 0)]
    [InlineData(0, 100)]
    [InlineData(250, 600)]
    public void Brulure_reduite_par_la_resistance_magique_et_saignement_par_l_armure(int armor, int magicResistance)
    {
        var (_, burn) = Fight(10_000, Target(armor, magicResistance), Caster(attacks: Burn()));
        var (_, bleed) = Fight(10_000, Target(armor, magicResistance), Caster(attacks: Bleed()));
        DerivedStats defender = Defenses(armor, magicResistance);
        long burnTick = Periodic(40, defender, DamageType.Magique), bleedTick = Periodic(40, defender, DamageType.Physique);
        Assert.Equal(magicResistance == 0 ? 40 : burnTick, burnTick);
        Assert.Equal(armor == 0 ? 40 : bleedTick, bleedTick);
        Assert.All(Dots(burn), d => Assert.Equal(burnTick, d.Amount));
        Assert.All(Dots(bleed), d => Assert.Equal(bleedTick, d.Amount));
        Assert.Equal(3, Dots(burn).Count);
        Assert.Equal(3, Dots(bleed).Count);
        if (magicResistance > 0) Assert.True(burnTick < 40);
        if (armor > 0) Assert.True(bleedTick < 40);
    }

    /// <summary>
    /// Aucune altération du sous-lot 7.2 ne change une défense en combat ; pour prouver que la défense n'est pas figée à
    /// l'application (seule la puissance l'est), le test remplace par réflexion les statistiques de la cible entre deux ticks.
    /// </summary>
    [Theory]
    [InlineData("bleed", 300, 0)]
    [InlineData("burn", 0, 300)]
    public void Defense_relue_a_chaque_tick(string status, int armor, int magicResistance)
    {
        AssailantAttack attack = status == "burn" ? Burn() : Bleed();
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Target(), Caster(attacks: attack) }, durationMs: 10_000), log);
        while (engine.CurrentTick <= 60) engine.Step();   // premier tick (60) joué sans défense
        ReplaceStats(engine, "mannequin", Defenses(armor, magicResistance));
        while (!engine.IsFinished) engine.Step();

        DamageType type = status == "burn" ? DamageType.Magique : DamageType.Physique;
        long reduced = Periodic(40, Defenses(armor, magicResistance), type);
        Assert.True(reduced < 40);
        Assert.Equal(new[] { (60L, 40L), (100L, reduced), (140L, reduced) }, Dots(Events(log)));
    }

    private static void ReplaceStats(CombatEngine engine, string id, DerivedStats stats)
    {
        var byId = (IDictionary)typeof(CombatEngine).GetField("_byId", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(engine)!;
        object fighter = byId[id]!;
        fighter.GetType().GetField("Stats", BindingFlags.Public | BindingFlags.Instance)!.SetValue(fighter, stats);
    }

    // --- Ni critique, ni Fragilité, ni Résonance, ni coup ----------------------------------------

    [Fact]
    public void Tick_sans_critique_fragilite_resonance_ni_coup()
    {
        // Brasier (0,5 magique + brûlure 0,5) à 100 ms sur un mannequin maintenu fragile par un allié : l'impact direct est
        // fragile ; les ticks valent la puissance figée arrondie (aucune défense), sans critique ni Fragilité, et ne rapportent
        // ni Résonance ni coup.
        var fele = Assailant(0, 1000, "fele", "joueurs", "mannequin", Strike("fele", 1, applies: Apply("fragility")));
        var (result, events) = Fight(10_000, null, new[] { Cmd(100, Brasier) },
            Character(actives: new[] { Brasier, Frappe, Heurt, Onde }), new DummySetup("mannequin", "cibles", default, default), fele);

        JsonElement direct = events.OfKind("damage").Single(e => e.Str("source") == "bastion");
        Assert.True(direct.GetProperty("fragile").GetBoolean());
        JsonElement burn = events.OfKind("stateApplied").Single(e => e.Str("state") == "burn");
        Fixed power = Fixed.Parse(burn.GetProperty("power").GetRawText());
        long expected = DamageCalculator.ComputePeriodic(power, Defenses(0, 0), DamageType.Magique, true, DamageRules.Provisional);
        Assert.Equal(new[] { (42L, expected), (82L, expected), (122L, expected) }, Dots(events));
        Assert.All(events.OfKind("dotDamage"), e => Assert.False(e.TryGetProperty("critical", out _)));

        var gains = events.OfKind("resourceGained").Where(e => e.Str("actor") == "bastion").ToList();
        Assert.Equal(new[] { (2L, "directHit") }, gains.Select(g => (g.Tick(), g.Str("source")!)));
        Assert.Equal(1L, result["bastion"].Hits);
        Assert.Equal(direct.Int("amount") + 3 * expected, result["bastion"].DamageDealt);
    }

    // --- Renouvellement ---------------------------------------------------------------------------

    private static (CombatResult Result, List<JsonElement> Events) Sequence(long intervalMs, params AssailantAttack[] attacks) =>
        Fight(20_000, Target(), Caster(intervalMs: intervalMs, attacks: attacks.Concat(Pauses(20)).ToArray()));

    /// <summary>Attaques sans altération qui complètent une séquence (identifiants distincts).</summary>
    private static IEnumerable<AssailantAttack> Pauses(int count) => Enumerable.Range(0, count).Select(i => Blow(id: $"pause_{i}"));

    [Fact]
    public void Renouvellement_plus_faible_garde_la_puissance_et_repousse_la_fin()
    {
        // Brûlure 40 au tick 20 (fin 140), renouvelée au tick 60 par une brûlure 20 de la même source : même instance,
        // génération 2, puissance 40 conservée, fin 180. Cadence d'origine : 60, 100, 140, 180 (le tick 60 suit la résolution).
        var (_, events) = Sequence(2000, Burn("0.4"), Burn("0.2", id: "faible"));
        var applied = events.OfKind("stateApplied").ToList();
        Assert.Equal(new[] { (20L, 1L, 140L, false, 40m), (60L, 2L, 180L, true, 40m) }, applied.Select(a =>
            (a.Tick(), a.Int("generation"), a.Int("untilTick"), a.GetProperty("renewed").GetBoolean(), a.GetProperty("power").GetDecimal())));
        Assert.Equal(applied[0].Int("instance"), applied[1].Int("instance"));
        Assert.Equal(new[] { (60L, 40L), (100L, 40L), (140L, 40L), (180L, 40L) }, Dots(events));
        Assert.Equal(180L, Assert.Single(events.OfKind("stateExpired")).Tick());
    }

    [Fact]
    public void Renouvellement_plus_fort_releve_la_puissance_des_le_tick_suivant()
    {
        // Brûlure 20 au tick 20, renouvelée à 80 au tick 100 : le tick 100 (phase périodique, après la résolution) vaut déjà 80.
        var (_, events) = Sequence(2000, Burn("0.2", id: "faible"), Blow(id: "simple"), Burn("0.8", id: "fort"));
        Assert.Equal(new[] { (60L, 20L), (100L, 80L), (140L, 80L), (180L, 80L), (220L, 80L) }, Dots(events));
        Assert.Equal(220L, Assert.Single(events.OfKind("stateExpired")).Tick());
    }

    [Fact]
    public void Renouvellement_hors_cadence_garde_la_cadence_d_origine()
    {
        // Application au tick 20, renouvellement au tick 50 (fin demandée 170) : les ticks restent à 60, 100, 140 ; le suivant
        // (180) dépasserait la fin demandée. Correctif 7.2 : la fin effective, alignée sur la cadence, est 140, et c'est elle
        // que le journal annonce (untilTick), avec le prochain tick (nextTick) ; l'expiration réelle tombe à ce tick.
        var (_, events) = Sequence(1500, Burn("0.4"), Burn("0.4", id: "encore"));
        var applied = events.OfKind("stateApplied").ToList();
        Assert.Equal(new[] { (140L, 60L), (140L, 60L) }, applied.Select(a => (a.Int("untilTick"), a.Int("nextTick"))));
        Assert.Equal(new[] { (60L, 40L), (100L, 40L), (140L, 40L) }, Dots(events));
        Assert.Equal(applied[^1].Int("untilTick"), Assert.Single(events.OfKind("stateExpired")).Tick());
    }

    [Fact]
    public void Expiree_puis_reappliquee_devient_une_nouvelle_instance()
    {
        // Expiration au tick 140, nouvelle application au tick 180 : nouvel identifiant, génération 1, cadence repartie de 180.
        var (_, events) = Sequence(8000, Burn("0.4"), Burn("0.4", id: "encore"));
        var applied = events.OfKind("stateApplied").ToList();
        Assert.Equal(2, applied.Count);
        Assert.NotEqual(applied[0].Int("instance"), applied[1].Int("instance"));
        Assert.Equal((180L, 1L, false), (applied[1].Tick(), applied[1].Int("generation"), applied[1].GetProperty("renewed").GetBoolean()));
        Assert.Equal(new long[] { 60, 100, 140, 220, 260, 300 }, Dots(events).Select(d => d.Tick));
    }

    // --- Sources multiples ------------------------------------------------------------------------

    [Fact]
    public void Deux_sources_deux_instances_independantes()
    {
        // Deux assaillants brûlent la même cible au même tick (40 et 80) : deux instances, ticks entrelacés dans l'ordre de
        // programmation, chacun crédité à sa source. Le renouvellement de « b » au tick 60 ne touche pas l'instance de « a ».
        var (result, events) = Fight(20_000, Target(),
            Caster("a", attacks: Burn("0.4")), Caster("b", intervalMs: 2000, attacks: new[] { Burn("0.8"), Burn("0.8", id: "encore") }.Concat(Pauses(8)).ToArray()));
        var applied = events.OfKind("stateApplied").ToList();
        Assert.Equal(new[] { ("a", 20L, false), ("b", 20L, false), ("b", 60L, true) }, applied.Select(a => (a.Str("source")!, a.Tick(), a.GetProperty("renewed").GetBoolean())));
        Assert.NotEqual(applied[0].Int("instance"), applied[1].Int("instance"));
        Assert.Equal(new[] { (60L, 40L), (100L, 40L), (140L, 40L) }, Dots(events, "a"));
        Assert.Equal(new[] { (60L, 80L), (100L, 80L), (140L, 80L), (180L, 80L) }, Dots(events, "b"));
        Assert.Equal(new[] { "a", "b", "a", "b", "a", "b", "b" }, events.OfKind("dotDamage").Select(e => e.Str("source")!));
        long direct(string id) => events.OfKind("damage").Where(e => e.Str("source") == id).Sum(e => e.Int("amount"));
        Assert.Equal((direct("a") + 120, direct("b") + 320), (result["a"].DamageDealt, result["b"].DamageDealt));
        Assert.Equal(new[] { ("a", 140L), ("b", 180L) }, events.OfKind("stateExpired").Select(e => (e.Str("source")!, e.Tick())));
    }

    // --- Morts ------------------------------------------------------------------------------------

    [Fact]
    public void Mort_de_la_source_le_dot_continue_et_reste_credite()
    {
        // « fragile » entaille le mannequin à 0 ms puis meurt au tick 10 ; son saignement continue (40, 80, 120) et lui est crédité.
        var killer = Assailant(500, 60_000, "tueur", "cibles", "fragile", Blow(raw: 1_000_000));
        var (result, events) = Fight(10_000, null, new[] { Cmd(0, Entaille, actor: "fragile") },
            Character("fragile", actives: new[] { Entaille, Frappe, Heurt, Onde }), Tank(), new DummySetup("mannequin", "cibles", default, default), killer);
        Assert.Equal(10L, events.OfKind("died").Single(e => e.Str("combatant") == "fragile").Tick());
        var dots = Dots(events, "fragile");
        Assert.Equal(new long[] { 40, 80, 120 }, dots.Select(d => d.Tick));
        long direct = events.OfKind("damage").Single(e => e.Str("source") == "fragile").Int("amount");
        Assert.Equal(direct + dots.Sum(d => d.Amount), result["fragile"].DamageDealt);
    }

    [Fact]
    public void Mort_de_la_cible_retire_l_instance_et_perime_les_ticks()
    {
        // Saignement de puissance 100 × 100 sur « cible » : le premier tick (60) la tue ; l'instance est retirée à la fin du
        // tick (raison death) ; le tick programmé à 100 est périmé : un retrait de file, aucune ligne.
        var caster = Assailant(1000, 60_000, "assaillant", "cibles", "cible", Strike("entaille", 100, applies: Apply("bleed", "100")));
        var setup = Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Character("cible"), Tank(), caster }, durationMs: 10_000);
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log);
        while (engine.CurrentTick <= 100) engine.Step();
        Assert.Equal((3L * 3 + 1, 0L), (engine.LastTickWork, engine.LastTickEvents));   // tick 100 : 3 combattants × 3 passages + retrait périmé
        while (!engine.IsFinished) engine.Step();

        var events = Events(log);
        JsonElement dot = Assert.Single(events.OfKind("dotDamage"));
        Assert.Equal((60L, 0L), (dot.Tick(), dot.Int("health")));
        Assert.True(dot.Int("overkill") > 0);
        JsonElement removed = Assert.Single(events.OfKind("stateRemoved"));
        Assert.Equal((60L, "bleed", "dot", "assaillant", "death"),
            (removed.Tick(), removed.Str("state")!, removed.Str("category")!, removed.Str("source")!, removed.Str("reason")!));
        Assert.True(events.OfKind("died").Single().Int("seq") < removed.Int("seq"));
        Assert.Empty(events.OfKind("stateExpired"));
    }

    // --- Coûts réservés et budgets ----------------------------------------------------------------

    [Fact]
    public void Cout_exact_d_un_tick_de_dot()
    {
        // 2 combattants × 3 passages = 6. Tick ordinaire de DOT : retrait 1 + impact 1 + ligne 1 + tick suivant en file 1 = 10,
        // une ligne. Dernier tick : retrait 1 + impact 1 + ligne 1 + fin de l'instance (retrait 1, ligne 1) = 11, deux lignes.
        var engine = new CombatEngine(Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Target(), Caster(attacks: Burn()) }, durationMs: 10_000),
            new MemoryCombatLog());
        var costs = new Dictionary<long, (long, long)>();
        while (!engine.IsFinished)
        {
            long tick = engine.CurrentTick;
            engine.Step();
            costs[tick] = (engine.LastTickWork, engine.LastTickEvents);
        }
        Assert.Equal((10L, 1L), costs[60]);
        Assert.Equal((10L, 1L), costs[100]);
        Assert.Equal((11L, 2L), costs[140]);
        Assert.Equal((6L, 0L), costs[180]);
    }

    private static CombatRules Budgets(int work, CombatRules? rules = null) =>
        new()
        {
            MaxWorkPerTick = work, MaxEventsPerTick = Math.Min(1000, work), Statuses = (rules ?? CombatRules.Provisional).Statuses,
            GuardCostPerBlock = (rules ?? CombatRules.Provisional).GuardCostPerBlock, GuardUpkeepPerSecond = (rules ?? CombatRules.Provisional).GuardUpkeepPerSecond,
        };

    /// <summary>
    /// Catalogue de test : un DOT magique qui accorde à son expiration « choc », un contrôle sans état accordé qui interrompt la
    /// garde. Le dernier tick écrit alors quatre lignes (dégâts, expiration, choc, fin de garde), plus que tout autre tick.
    /// </summary>
    private static readonly StatusCatalog WithVenom = new(StatusCatalog.Provisional.Definitions.Concat(new[]
    {
        new StatusDefinition("venin", StatusCategory.Dot, 6000, StatusStacking.PerSourceKeepStronger, StatusResistanceMode.Defense,
            periodMs: 2000, damageType: DamageType.Magique, grantsOnExpiry: "choc"),
        new StatusDefinition("choc", StatusCategory.Control, 1000, StatusStacking.KeepLonger, StatusResistanceMode.Duration,
            blocks: StatusBlocks.Skills, interrupts: StatusInterrupts.Guard, blockReason: "shocked"),
    }));

    [Fact]
    public void Dernier_tick_de_dot_atomique_aux_frontieres()
    {
        // Venin au tick 20 (attaque, dégâts, état : 3 lignes), garde prise au tick 30 (1 ligne). Tick 140 : dégâts du dernier
        // tick, expiration, choc accordé, fin de garde interrompue = 4 lignes, réservées en une seule étape.
        CombatSetup Scenario(int events) => Setup(new[] { GuardStart(1500) },
            new CombatantSetup[] { Tank(), Assailant(1000, 60_000, attacks: Strike("venin", 100, DamageType.Magique, applies: Apply("venin", "0.4"))) },
            durationMs: 10_000, rules: new CombatRules { Statuses = WithVenom, GuardUpkeepPerSecond = 0, MaxEventsPerTick = events });
        var reference = Run(Scenario(1000));
        Assert.Equal(4, reference.Result.Peaks.EventsPerTick);
        var last = Events(reference.Log).Where(e => e.Tick() == 140).Select(e => e.Str("event")!).ToList();
        Assert.Equal(new[] { "dotDamage", "stateExpired", "stateApplied", "guardStopped" }, last);
        Assert.NotEqual(CombatEndReason.Aborted, Run(Scenario(4)).Result.EndReason);

        var (result, log) = Run(Scenario(3));
        Assert.Equal(("eventBudgetExceeded", 140L), (result.AbortReason, result.EndTick));
        var events = Events(log);
        Assert.Equal(new long[] { 60, 100 }, Dots(events).Select(d => d.Tick));   // aucun effet partiel au tick 140
        Assert.Empty(events.OfKind("stateExpired"));
        Assert.Empty(events.OfKind("guardStopped"));
        Assert.Equal(new[] { "venin" }, events.OfKind("stateApplied").Select(e => e.Str("state")!));
        long dot = Dots(events)[0].Amount;
        Assert.Equal(events.OfKind("damage").Single().Int("amount") + 2 * dot, result["bastion"].DamageTaken);
    }

    [Fact]
    public void Impact_compose_degats_rupture_dot_et_etourdissement_refuse_en_entier()
    {
        // Impact sur un personnage en garde qui ne peut pas payer le blocage, portant saignement et étourdissement :
        // tour 2 + impact 2 + rupture (ligne, état 3) 4 + saignement 3 + étourdissement 3 = 14 ; + 6 passages = 20.
        // Avec 13, la réserve de l'impact (2 + 12 > 13) échoue : ni dégâts, ni rupture, ni état.
        CombatSetup Scenario(int work) => Setup(new[] { GuardStart(0) },
            new CombatantSetup[] { Tank(), Assailant(5000, 60_000, attacks: Strike("lourd", 100, applies: new[] { Apply("bleed", "0.4"), Apply("stun") })) },
            rules: Budgets(work, new CombatRules { GuardCostPerBlock = 1000, GuardUpkeepPerSecond = 0 }));
        var reference = Run(Scenario(10_000));
        var referenceEvents = Events(reference.Log);
        Assert.Equal(new[] { "guardBreak", "bleed", "stun" }, referenceEvents.OfKind("stateApplied").Where(e => e.Tick() == 100).Select(e => e.Str("state")!));
        Assert.Equal(20, reference.Result.Peaks.WorkPerTick);

        var (result, log) = Run(Scenario(13));
        Assert.Equal(("workBudgetExceeded", 100L), (result.AbortReason, result.EndTick));
        var events = Events(log);
        Assert.Single(events.OfKind("attackStarted"));
        Assert.Empty(events.OfKind("guardBroken"));
        Assert.Empty(events.OfKind("stateApplied"));
        Assert.Empty(events.OfKind("damage"));
        Assert.Equal((0L, 0L), (result["bastion"].GuardBreaks, result["bastion"].DamageTaken));
    }

    // --- Précontrôle numérique ---------------------------------------------------------------------

    private static void AssertRefusedBeforeLog(CombatSetup setup, Func<RuleViolation, bool> match)
    {
        Assert.Contains(setup.Validate(), v => match(v));
        var log = new MemoryCombatLog();
        var error = Assert.Throws<CombatSetupException>(() => new CombatEngine(setup, log));
        Assert.Contains(error.Violations, v => match(v));
        Assert.Empty(log.Lines);
    }

    private static CombatSetup LongFight(string coefficient, StatRules? stats = null, CombatantSetup? target = null) =>
        new(42, CombatRules.AbsoluteMaxDurationMs, stats ?? StatRules.Provisional, DamageRules.Provisional, ProgressionRules.Provisional,
            new CombatRules { MaxDurationMs = CombatRules.AbsoluteMaxDurationMs }, new[] { target ?? Tank(), Assailant(1000, 60_000, target: (target ?? Tank()).Id,
                attacks: Strike("entaille", CombatantStats.MaxRawDamage, applies: Apply("bleed", coefficient))) }, Array.Empty<CombatCommand>(), Identity);

    [Fact]
    public void Pire_tick_de_dot_dans_le_precontrole_numerique()
    {
        // Durée maximale (1 728 000 ticks × 1000 lignes) et dégâts bruts maximaux : l'impact direct tient, un saignement de
        // coefficient 5,3375 aussi ; à 5,3376, le cumul du pire tick (puissance figée 5,3376 × 10⁹) ne tient plus : refus.
        // Correctif 7.3.1 (A73-M01) : le tick est calculé par la formule périodique, sans Fragilité ni bonus de catégorie (le DOT
        // n'en reçoit pas) ; l'ancien calcul direct, Fragilité comprise, refusait déjà le coefficient 5, qui tient en réalité.
        Assert.Empty(LongFight("4").Validate());
        Assert.Empty(LongFight("5.3375").Validate());
        AssertRefusedBeforeLog(LongFight("5.3376"), v => v.Kind == RuleViolationKind.NumericLimit && v.Fields.Contains("combatants[1]"));
        AssertRefusedBeforeLog(LongFight("100"), v => v.Message.Contains("altération « bleed »") || v.Message.Contains("cumul"));
    }

    [Fact]
    public void Resistance_aux_alterations_et_constante_bornees_avant_tout_journal()
    {
        Fixed max = CombatantStats.MaxDefense;
        CombatSetup With(Fixed resistance, StatRules? stats = null) => new(42, 10_000, stats ?? StatRules.Provisional, DamageRules.Provisional,
            ProgressionRules.Provisional, CombatRules.Provisional,
            new CombatantSetup[] { Character(), new DummySetup("mannequin", "cibles", default, default, statusResistance: resistance) },
            Array.Empty<CombatCommand>(), Identity);
        Assert.Empty(With(max).Validate());
        AssertRefusedBeforeLog(With(max + Fixed.FromRaw(1)), v => v.Fields.Contains("combatants[1].statusResistance"));
        AssertRefusedBeforeLog(With(Fixed.FromInt(-1)), v => v.Fields.Contains("combatants[1].statusResistance"));
        foreach (long constant in new[] { 0L, -1L, 1_000_000_001L })
            AssertRefusedBeforeLog(With(Fixed.Zero, new StatRules { StatusResistanceConstant = constant }),
                v => v.Fields.Any(f => f.Contains("StatusResistanceConstant", StringComparison.Ordinal)));
        Assert.Empty(With(max, new StatRules { StatusResistanceConstant = 1_000_000_000 }).Validate());
    }
}
