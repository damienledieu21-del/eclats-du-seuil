using System.Text.Json;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Engine;

/// <summary>
/// Lot 7.2 : contrôles. Étourdissement 1,5 s (bloque compétences, garde, parade et attaques d'assaillant ; interrompt garde et
/// parade ; accorde 3 s d'immunité à son expiration) ; gel 4 s (−30 % de déplacement enregistré, ne bloque rien). La résistance
/// aux altérations réduit la durée des contrôles (facteur K / (K + résistance), K = 1000), jamais les dégâts des DOT.
/// Les personnages de test ont 10 de Volonté, soit 20 de résistance aux altérations : r = 0,9803, un étourdissement dure
/// ⌊ 30 × 0,9803 ⌋ = 29 ticks et un gel ⌊ 80 × 0,9803 ⌋ = 78 ticks.
/// </summary>
[Trait("Category", "Engine")]
[Trait("Category", "Status")]
public class Lot72ControlTests
{
    private static AssailantAttack Stun(string id = "assomme", bool unblockable = false) => Strike(id, 100, unblockable: unblockable, applies: Apply("stun"));
    private static AssailantAttack Freeze(string id = "gele") => Strike(id, 100, applies: Apply("freeze"));

    private static (CombatResult Result, List<JsonElement> Events) Fight(IEnumerable<CombatCommand> commands, long durationMs, CombatRules? rules,
        params CombatantSetup[] fighters)
    {
        var (result, log) = Run(Setup(commands, fighters, durationMs: durationMs, rules: rules ?? NoUpkeep));
        return (result, Events(log));
    }

    private static List<(long Tick, string Reason)> Rejections(List<JsonElement> events) =>
        events.OfKind("commandRejected").Select(e => (e.Tick(), e.Str("reason")!)).ToList();

    private static List<(long Tick, string State, string Reason)> Blocked(List<JsonElement> events) =>
        events.OfKind("stateBlocked").Select(e => (e.Tick(), e.Str("state")!, e.Str("reason")!)).ToList();

    // --- Étourdissement : refus ---------------------------------------------------------------------

    [Fact]
    public void Etourdi_refuse_competence_garde_et_parade_juste_apres_les_motifs_de_mort()
    {
        // Étourdissement au tick 20, fin au tick 49 (phase 1, avant les commandes). Au tick 22, tout est refusé « stunned »,
        // y compris une compétence non équipée (le motif d'étourdissement précède skillNotEquipped, D11). Au tick 50, accepté.
        var (_, events) = Fight(new[] { Cmd(1100, Frappe, target: "assaillant"), GuardStart(1100), Parry(1100), Cmd(1100, NonEquipee, target: "assaillant"),
                Cmd(2500, Frappe, target: "assaillant") }, 4000, null,
            Tank(), Assailant(intervalMs: 60_000, attacks: Stun()));
        Assert.Equal(new[] { (22L, "stunned"), (22L, "stunned"), (22L, "stunned"), (22L, "stunned") }, Rejections(events));
        Assert.Equal(49L, events.OfKind("stateExpired").Single(e => e.Str("state") == "stun").Tick());
        Assert.Equal(50L, Assert.Single(events.OfKind("skillCast")).Tick());
        JsonElement stun = events.OfKind("stateApplied").Single(e => e.Str("state") == "stun");
        Assert.Equal(("control", "assaillant", 49L), (stun.Str("category")!, stun.Str("source")!, stun.Int("untilTick")));
        Assert.False(stun.TryGetProperty("movementSpeedModifier", out _));
    }

    [Fact]
    public void Etourdissement_avant_rupture_dans_l_ordre_du_catalogue()
    {
        // Garde brisée et étourdissement par le même impact au tick 100 : la garde est refusée « stunned » tant que dure
        // l'étourdissement (fin 129), puis « guardBroken » jusqu'à la fin de la rupture (160), puis acceptée.
        var (_, events) = Fight(new[] { GuardStart(0), GuardStart(5050), GuardStart(6500), GuardStart(8000) }, 9000,
            new CombatRules { GuardCostPerBlock = 1000, GuardUpkeepPerSecond = 0 },
            Tank(), Assailant(5000, 60_000, attacks: Stun()));
        Assert.Equal(new[] { (101L, "stunned"), (130L, "guardBroken") }, Rejections(events));
        Assert.Equal(new[] { 0L, 160L }, events.OfKind("guardStarted").Select(e => e.Tick()));
    }

    // --- Étourdissement : interruptions ---------------------------------------------------------------

    [Fact]
    public void Etourdissement_interrompt_la_garde_apres_le_blocage()
    {
        // Garde payée au tick 20 (blocage, Résonance), puis l'étourdissement l'interrompt : fin de garde sans commande.
        var (result, events) = Fight(new[] { GuardStart(0), GuardStart(1100) }, 4000, null,
            Tank(endurance: 50), Assailant(intervalMs: 60_000, attacks: Stun()));
        var resolution = events.Where(e => e.Tick() == 20 && e.Str("phase") == "resolution").Select(e => e.Str("event")!).ToList();
        Assert.Equal(new[] { "blocked", "damage", "resourceGained", "stateApplied", "guardStopped" }, resolution);
        JsonElement stopped = events.OfKind("guardStopped").Single();
        Assert.Equal((JsonValueKind.Null, "interrupted", "stun"), (stopped.GetProperty("command").ValueKind, stopped.Str("reason")!, stopped.Str("state")!));
        Assert.Equal(new[] { (22L, "stunned") }, Rejections(events));
        Assert.Equal(1L, result["bastion"].Blocks);
    }

    [Fact]
    public void Etourdissement_imparable_interrompt_la_parade()
    {
        var (_, events) = Fight(new[] { Parry(1000) }, 4000, null, Tank(), Assailant(intervalMs: 60_000, attacks: Stun(unblockable: true)));
        Assert.Equal("unblockable", Assert.Single(events.OfKind("damage")).Str("defense"));
        JsonElement ended = Assert.Single(events.OfKind("parryEnded"));   // la fin de fenêtre au tick 23 est périmée
        Assert.Equal((20L, "interrupted", JsonValueKind.Null), (ended.Tick(), ended.Str("outcome")!, ended.GetProperty("recoveryUntilTick").ValueKind));
        Assert.True(events.OfKind("stateApplied").First(e => e.Str("state") == "stun").Int("seq") < ended.Int("seq"));
    }

    [Fact]
    public void Parade_parfaite_empeche_l_etourdissement_de_l_impact_pare()
    {
        // Correctif 7.2 : l'impact paré n'applique pas ses altérations (stateBlocked, raison parried, source = l'assaillant) ;
        // la parade n'est donc pas interrompue et aucune immunité ne suit.
        var (result, events) = Fight(new[] { Parry(1000) }, 4000, null, Tank(), Assailant(intervalMs: 60_000, attacks: Stun()));
        Assert.Equal("parry", Assert.Single(events.OfKind("damage")).Str("defense"));
        JsonElement blocked = Assert.Single(events.OfKind("stateBlocked"));
        Assert.Equal((20L, "bastion", "stun", "control", "assaillant", "parried"),
            (blocked.Tick(), blocked.Str("combatant")!, blocked.Str("state")!, blocked.Str("category")!, blocked.Str("source")!, blocked.Str("reason")!));
        Assert.Empty(events.OfKind("stateApplied"));
        Assert.DoesNotContain(events.OfKind("parryEnded"), e => e.Str("outcome") == "interrupted");
        Assert.Empty(Rejections(events));
        Assert.Equal(1L, result["bastion"].PerfectParries);
    }

    [Fact]
    public void Etourdissement_du_meme_tick_n_annule_pas_une_action_acceptee()
    {
        // D13 : au tick 20, la Frappe de bastion est acceptée en phase 2 ; l'assaillant (identifiant plus petit) résout d'abord
        // et l'étourdit ; la Frappe acceptée est tout de même résolue. Une garde prise au même tick est interrompue.
        var (_, events) = Fight(new[] { Cmd(1000, Frappe, target: "assaillant") }, 4000, null,
            Character(), Assailant(intervalMs: 60_000, attacks: Stun()));
        var resolution = events.Where(e => e.Tick() == 20 && e.Str("phase") == "resolution").ToList();
        Assert.Equal(new[] { ("damage", "assaillant"), ("stateApplied", "bastion"), ("damage", "bastion") },
            resolution.Where(e => e.Str("event") != "resourceGained")
                .Select(e => (e.Str("event")!, e.Str("event") == "damage" ? e.Str("source")! : e.Str("combatant")!)));

        var (_, guarded) = Fight(new[] { GuardStart(1000) }, 4000, null, Tank(), Assailant(intervalMs: 60_000, attacks: Stun()));
        Assert.Equal(new[] { "attackStarted", "guardStarted", "blocked", "damage", "resourceGained", "stateApplied", "guardStopped" },
            guarded.Where(e => e.Tick() == 20).Select(e => e.Str("event")!).Where(k => k != "enduranceRegenerated"));
    }

    // --- Assaillant étourdi ---------------------------------------------------------------------------

    [Fact]
    public void Assaillant_etourdi_saute_son_attaque_sans_changer_sa_cadence()
    {
        // Assommer à 900 ms (tick 18, fin 48) : l'attaque « a » du tick 20 est sautée (stunned) ; « b » suit au tick 60, puis « a ».
        var (_, events) = Fight(new[] { Cmd(900, Assommer, target: "assaillant") }, 7000, null,
            Character(actives: new[] { Assommer, Frappe, Heurt, Onde }), Assailant(attacks: new[] { Blow(id: "a"), Blow(id: "b") }));
        JsonElement skipped = Assert.Single(events.OfKind("attackSkipped"));
        Assert.Equal((20L, "a", "stunned"), (skipped.Tick(), skipped.Str("attack")!, skipped.Str("reason")!));
        Assert.Equal(new[] { (60L, "b"), (100L, "a") }, events.OfKind("attackStarted").Select(e => (e.Tick(), e.Str("attack")!)));
    }

    // --- Gel ------------------------------------------------------------------------------------------

    [Fact]
    public void Gel_ne_bloque_rien_et_enregistre_le_modificateur_de_deplacement()
    {
        // Gel au tick 20 (fin 98) : compétence, garde puis parade acceptées pendant le gel ; −0,3 de déplacement journalisé,
        // sans simulation du déplacement. Un assaillant gelé attaque normalement.
        var (_, events) = Fight(new[] { Cmd(1100, Frappe, target: "assaillant"), GuardStart(2000), GuardStop(2500), Parry(3000) }, 6000, null,
            Tank(), Assailant(intervalMs: 60_000, attacks: Freeze()));
        Assert.Empty(Rejections(events));
        Assert.Equal((1, 1, 1), (events.OfKind("skillCast").Count(), events.OfKind("guardStarted").Count(), events.OfKind("parryStarted").Count()));
        JsonElement freeze = Assert.Single(events.OfKind("stateApplied"));
        Assert.Equal(("freeze", "control", 98L, -0.3m),
            (freeze.Str("state")!, freeze.Str("category")!, freeze.Int("untilTick"), freeze.GetProperty("movementSpeedModifier").GetDecimal()));
        Assert.Equal(98L, Assert.Single(events.OfKind("stateExpired")).Tick());

        var (_, frozen) = Fight(new[] { Cmd(900, Givre, target: "assaillant") }, 5000, null,
            Character(actives: new[] { Givre, Frappe, Heurt, Onde }), Assailant());
        Assert.Equal(new long[] { 20, 60 }, frozen.OfKind("attackStarted").Select(e => e.Tick()));
        Assert.Empty(frozen.OfKind("attackSkipped"));
    }

    // --- Cumul « keepLonger » --------------------------------------------------------------------------

    [Fact]
    public void Controle_renouvele_seulement_si_la_nouvelle_fin_est_plus_tardive()
    {
        // Deux gels au tick 20 (fins égales) : le second est refusé « notLonger ». Un troisième au tick 40 repousse la fin à 118 :
        // même instance, génération 2, l'échéance de 98 devient périmée.
        var (_, events) = Fight(Array.Empty<CombatCommand>(), 8000, null, Tank(),
            Assailant(intervalMs: 60_000, id: "a", attacks: Freeze()), Assailant(intervalMs: 60_000, id: "b", attacks: Freeze()),
            Assailant(2000, 60_000, id: "c", attacks: Freeze()));
        Assert.Equal(new[] { (20L, "freeze", "notLonger") }, Blocked(events));
        Assert.Equal("b", events.OfKind("stateBlocked").Single().Str("source"));
        var applied = events.OfKind("stateApplied").ToList();
        Assert.Equal(new[] { ("a", 1L, 98L, false), ("c", 2L, 118L, true) },
            applied.Select(a => (a.Str("source")!, a.Int("generation"), a.Int("untilTick"), a.GetProperty("renewed").GetBoolean())));
        Assert.Equal(applied[0].Int("instance"), applied[1].Int("instance"));
        JsonElement expired = Assert.Single(events.OfKind("stateExpired"));
        Assert.Equal((118L, "c", 2L), (expired.Tick(), expired.Str("source")!, expired.Int("generation")));
    }

    // --- Immunité ----------------------------------------------------------------------------------------

    [Fact]
    public void Immunite_de_trois_secondes_apres_l_etourdissement()
    {
        // Étourdissement au tick 20, fin au tick 49 : immunité de 60 ticks (non réduite) accordée dans la même étape (même source),
        // fin au tick 109. Étourdissement refusé « immune » aux ticks 60 et 108 ; accepté au tick 109 (l'immunité expire en phase 1).
        var (_, events) = Fight(Array.Empty<CombatCommand>(), 8000, null, Tank(),
            Assailant(intervalMs: 60_000, id: "a", attacks: Stun()), Assailant(3000, 60_000, id: "b", attacks: Stun()),
            Assailant(5400, 60_000, id: "c", attacks: Stun()), Assailant(5450, 60_000, id: "d", attacks: Stun()));
        Assert.Equal(new[] { (60L, "stun", "immune"), (108L, "stun", "immune") }, Blocked(events));
        var at49 = events.Where(e => e.Tick() == 49).Select(e => (e.Str("event")!, e.Str("state")!, e.Str("source")!)).ToList();
        Assert.Equal(new[] { ("stateExpired", "stun", "a"), ("stateApplied", "stunImmunity", "a") }, at49);
        JsonElement immunity = events.OfKind("stateApplied").Single(e => e.Str("state") == "stunImmunity" && e.Tick() == 49);
        Assert.Equal(("immunity", 109L), (immunity.Str("category")!, immunity.Int("untilTick")));
        Assert.Equal(new[] { (20L, "a"), (109L, "d") },
            events.OfKind("stateApplied").Where(e => e.Str("state") == "stun").Select(e => (e.Tick(), e.Str("source")!)));
        Assert.Equal(109L, events.OfKind("stateExpired").Single(e => e.Str("state") == "stunImmunity" && e.Tick() == 109).Tick());
    }

    [Fact]
    public void Cout_exact_de_l_expiration_qui_accorde_l_immunite()
    {
        // Tick 49 : retrait 1 + fin de l'étourdissement (retrait d'état 1, ligne 1) + immunité (application 1, échéance 1,
        // ligne 1) = 6, + 2 combattants × 3 passages = 12, deux lignes.
        var engine = new CombatEngine(Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Tank(), Assailant(intervalMs: 60_000, attacks: Stun()) },
            durationMs: 4000, rules: NoUpkeep), new MemoryCombatLog());
        while (engine.CurrentTick <= 49) engine.Step();
        Assert.Equal((12L, 2L), (engine.LastTickWork, engine.LastTickEvents));
    }

    // --- Résistance aux altérations --------------------------------------------------------------------

    [Theory]
    [InlineData(0, 30, 80)]        // r = 1
    [InlineData(1000, 15, 40)]     // r = 0,5
    [InlineData(3000, 7, 20)]      // r = 0,25 : ⌊ 30 × 0,25 ⌋ = 7
    [InlineData(500, 19, 53)]      // r = 0,6666 (arrondi vers le bas) : ⌊ 19,998 ⌋ = 19 et ⌊ 53,328 ⌋ = 53
    public void Duree_des_controles_reduite_par_la_resistance(int resistance, long stunTicks, long freezeTicks)
    {
        var dummy = new DummySetup("mannequin", "cibles", default, default, statusResistance: Fixed.FromInt(resistance));
        var (_, events) = Fight(new[] { Cmd(0, Assommer), Cmd(1000, Givre) }, 10_000, null,
            Character(actives: new[] { Assommer, Givre, Frappe, Heurt }), dummy);
        var applied = events.OfKind("stateApplied").Where(e => e.Str("state") != "stunImmunity").ToDictionary(e => e.Str("state")!);
        Assert.Equal(stunTicks, applied["stun"].Int("untilTick") - applied["stun"].Tick());
        Assert.Equal(freezeTicks, applied["freeze"].Int("untilTick") - applied["freeze"].Tick());
    }

    [Fact]
    public void Resiste_a_zero_tick_sans_immunite_et_dot_non_reduit()
    {
        // Résistance 10⁶ : r brut = ⌊ 10⁴ × 10⁷ / (10⁷ + 10¹⁰) ⌋ = 9 ; 30 × 9 / 10⁴ → 0 tick : refus « resisted », aucune
        // immunité. La brûlure, elle, garde ses 3 ticks entiers : la résistance aux altérations ne réduit pas les DOT.
        var dummy = new DummySetup("mannequin", "cibles", default, default, statusResistance: Fixed.FromInt(1_000_000));
        var (_, events) = Fight(new[] { Cmd(0, Assommer), Cmd(1000, Givre), Cmd(2000, Brasier) }, 10_000, null,
            Character(actives: new[] { Assommer, Givre, Brasier, Frappe }), dummy);
        Assert.Equal(new[] { (0L, "stun", "resisted"), (20L, "freeze", "resisted") }, Blocked(events));
        JsonElement burn = Assert.Single(events.OfKind("stateApplied"));
        Assert.Equal(("burn", 160L), (burn.Str("state")!, burn.Int("untilTick")));
        Assert.Equal(3, events.OfKind("dotDamage").Count());
        Assert.DoesNotContain(events, e => e.Str("event")!.StartsWith("state") && e.Str("state") == "stunImmunity");
    }

    [Fact]
    public void Constante_de_resistance_configurable()
    {
        // K = 3000 et résistance 1000 : r = 0,75 ; étourdissement ⌊ 30 × 0,75 ⌋ = 22 ticks.
        var dummy = new DummySetup("mannequin", "cibles", default, default, statusResistance: Fixed.FromInt(1000));
        var setup = Setup(new[] { Cmd(0, Assommer) }, new CombatantSetup[] { Character(actives: new[] { Assommer, Frappe, Heurt, Onde }), dummy }, rules: NoUpkeep);
        setup = new CombatSetup(42, 10_000, new StatRules { StatusResistanceConstant = 3000 }, DamageRules.Provisional, EdS.Domain.Builds.ProgressionRules.Provisional,
            NoUpkeep, setup.Combatants, setup.Commands, Identity);
        JsonElement stun = Events(Run(setup).Log).OfKind("stateApplied").Single(e => e.Str("state") == "stun");
        Assert.Equal(22L, stun.Int("untilTick"));
    }
}
