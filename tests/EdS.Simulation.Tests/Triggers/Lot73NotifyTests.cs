using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Triggers;

/// <summary>
/// Lot 7.3.3 : index des abonnés, notifications et évaluation. Avec <c>maxTriggerDepth = 0</c>, toute réaction acceptée devient
/// une suppression journalisée (<c>triggerSuppressed</c>, raison <c>depth</c>) : l'ordre et le résultat des évaluations se lisent
/// au journal. La trace interne <see cref="CombatEngine.ReactionTrace"/> donne les acceptations (profondeur 4).
/// </summary>
[Trait("Category", "Trigger")]
public class Lot73NotifyTests
{
    private static readonly CombatRules Depth0 = new() { MaxTriggerDepth = 0, GuardUpkeepPerSecond = 0 };

    /// <summary>Déclencheur de test dont l'effet (gain de Résonance du porteur) s'exécute dans la phase de la notification.</summary>
    private static TriggerDefinition On(TriggerEvent on, TriggerRole? role = null, TriggerFilter? filter = null, long cooldownMs = 0) =>
        new(on, new[] { TriggerEffect.GainResource(TriggerResource.Resonance, Fixed.One) }, role, filter, cooldownMs);

    private static List<JsonElement> Suppressed(IEnumerable<JsonElement> events) => events.OfKind("triggerSuppressed").ToList();

    private static long Eid(JsonElement e) => e.Int("eid");

    private static CharacterSetup Bearer(string id = "bastion", int initiative = 0, IEnumerable<TriggerDefinition>? talent = null,
        IEnumerable<TriggerDefinition>? passive = null, IEnumerable<TriggerDefinition>? frappe = null, string team = "joueurs",
        Attributes? attributes = null, string[]? actives = null) =>
        Character(id, team, initiative, attributes ?? Attrs(vitalite: 200), actives, Class(talentTriggers: talent, frappeTriggers: frappe, passiveTriggers: passive),
            talents: talent is null ? null : new Dictionary<string, int> { [TalentAlpha] = 1 }, passives: passive is null ? null : new[] { Passif });

    private static AssailantSetup Hitter(long firstMs = 1000, long intervalMs = 60_000, string id = "assaillant", int initiative = 0,
        IEnumerable<TriggerDefinition>? triggers = null, params AssailantAttack[] attacks) =>
        new(id, "cibles", Fixed.Zero, Fixed.Zero, "bastion", firstMs, intervalMs, attacks.Length == 0 ? new[] { Blow() } : attacks, initiative, triggers: triggers);

    private static (List<JsonElement> Events, CausalGraph Graph) Simulate(CombatSetup setup)
    {
        var (_, log) = Run(setup);
        return (Events(log), CausalGraph.Build(log.Lines));
    }

    [Fact]
    public void Reaction_acceptee_au_dela_de_la_profondeur_supprimee_et_journalisee()
    {
        var (events, graph) = Simulate(Setup(new[] { Cmd(0, Frappe) }, new CombatantSetup[] { Bearer(passive: new[] { On(TriggerEvent.HitDealt) }), Dummy() }, rules: Depth0));
        JsonElement damage = Assert.Single(events.OfKind("damage"));
        JsonElement line = Assert.Single(Suppressed(events));
        Assert.Equal(("skill.essai.passif/0@bastion#0", "command:0", Eid(damage), 1L, "depth", "resolution"),
            (line.Str("trigger")!, line.Str("root")!, line.Int("cause"), line.Int("depth"), line.Str("reason")!, line.Str("phase")!));
        Assert.False(line.TryGetProperty("eid", out _));                // diagnostic : aucun eid, rien ne le suit
        Assert.True(line.Int("seq") > damage.Int("seq"));
        Assert.Equal(Eid(damage), Assert.Single(graph.Cited).Cause);
    }

    [Fact]
    public void Filtre_et_role_rejettent_silencieusement()
    {
        var triggers = new[]
        {
            On(TriggerEvent.HitDealt, filter: new TriggerFilter(skills: new[] { Heurt })),        // autre compétence
            On(TriggerEvent.HitDealt, filter: new TriggerFilter(damageType: DamageType.Magique)),  // autre type
            On(TriggerEvent.HitDealt, filter: new TriggerFilter(defenses: new[] { HitDefense.Guard })),
            On(TriggerEvent.HitTaken),                                                             // le porteur n'est pas la cible
            On(TriggerEvent.HitDealt, filter: new TriggerFilter(skills: new[] { Frappe }, damageType: DamageType.Physique, defenses: new[] { HitDefense.None })),
        };
        var (events, _) = Simulate(Setup(new[] { Cmd(0, Frappe) }, new CombatantSetup[] { Bearer(passive: triggers), Dummy() }, rules: Depth0));
        Assert.Equal(new[] { "skill.essai.passif/4@bastion#0" }, Suppressed(events).Select(e => e.Str("trigger")));
    }

    [Fact]
    public void Ordre_total_des_abonnes()
    {
        // Un impact de l'assaillant sur le Bastion sert onHitDealt (assaillant) et onHitTaken (Bastion). À initiative égale,
        // identifiant ordinal : « assaillant » avant « bastion ». Chez le Bastion : talent, passif (indices 0 puis 1), compétence.
        TriggerDefinition taken = On(TriggerEvent.HitTaken);
        CombatantSetup[] Fighters(int bastionInitiative) => new CombatantSetup[]
        {
            Bearer(initiative: bastionInitiative, talent: new[] { taken }, passive: new[] { taken, taken }, frappe: new[] { taken }),
            Hitter(triggers: new[] { On(TriggerEvent.HitDealt) }),
        };
        string[] bastion = { "talent.essai.alpha.a/0@bastion#0", "skill.essai.passif/0@bastion#0", "skill.essai.passif/1@bastion#0", "skill.essai.frappe/0@bastion#0" };

        var equal = Suppressed(Simulate(Setup(Array.Empty<CombatCommand>(), Fighters(0), durationMs: 2000, rules: Depth0)).Events).Select(e => e.Str("trigger"));
        Assert.Equal(new[] { "assailant.assaillant/0@assaillant#0" }.Concat(bastion), equal);

        // Initiative du Bastion supérieure : il passe avant l'assaillant ; l'ordre du scénario ne compte pas.
        var first = Suppressed(Simulate(Setup(Array.Empty<CombatCommand>(), Fighters(5).Reverse(), durationMs: 2000, rules: Depth0)).Events).Select(e => e.Str("trigger"));
        Assert.Equal(bastion.Append("assailant.assaillant/0@assaillant#0"), first);
    }

    [Fact]
    public void Declencheurs_d_alteration_apres_les_permanents_et_identite_par_instance()
    {
        // « marque » (modificateur, 3 s) porte un déclencheur onHitTaken. Posée par la première attaque (tick 20), elle réagit aux
        // impacts suivants, après le passif du Bastion ; son identité porte le numéro d'instance.
        var marked = new StatusDefinition("marque", StatusCategory.Modifier, 3000, StatusStacking.Refresh, triggers: new[] { On(TriggerEvent.HitTaken) });
        var rules = new CombatRules { MaxTriggerDepth = 0, GuardUpkeepPerSecond = 0, Statuses = new StatusCatalog(StatusCatalog.Provisional.Definitions.Append(marked)) };
        var attack = Strike("marquage", 100, applies: Apply("marque"));
        var (events, _) = Simulate(Setup(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { Bearer(passive: new[] { On(TriggerEvent.HitTaken) }), Hitter(1000, 500, attacks: attack) }, durationMs: 1600, rules: rules));
        long instance = events.OfKind("stateApplied").First().Int("instance");
        var byTick = Suppressed(events).GroupBy(e => e.Tick()).ToDictionary(g => g.Key, g => g.Select(e => e.Str("trigger")!).ToList());
        // Tick 20 : l'impact précède la pose ; seul le passif réagit à l'impact, puis l'état appliqué n'a pas d'abonné.
        Assert.Equal(new[] { "skill.essai.passif/0@bastion#0" }, byTick[20]);
        Assert.Equal(new[] { "skill.essai.passif/0@bastion#0", $"marque/0@bastion#{instance}" }, byTick[30]);
    }

    [Fact]
    public void Recharge_interne_acceptation_puis_refus_silencieux_et_reprise_au_tick_exact()
    {
        // Recharge interne de 1 s (20 ticks) ; impacts tous les 10 ticks à partir du tick 20 : acceptés aux ticks 20, 40, 60.
        var bastion = Bearer(passive: new[] { On(TriggerEvent.HitTaken, cooldownMs: 1000) });
        var setup = Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { bastion, Hitter(1000, 500) }, durationMs: 3500, rules: NoUpkeep);
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log) { ReactionTrace = new List<(long, string, long)>() };
        while (engine.Step()) { }
        Assert.Equal(new long[] { 20, 40, 60 }, engine.ReactionTrace!.Select(r => r.Tick));
        Assert.All(engine.ReactionTrace!, r => Assert.Equal("skill.essai.passif/0@bastion#0", r.Trigger));
        Assert.Empty(Events(log).OfKind("triggerSuppressed"));
        CausalGraph.Build(log.Lines);
    }

    [Fact]
    public void Cout_exact_d_une_notification_et_d_une_evaluation()
    {
        // Même combat, avec puis sans abonné : au tick de l'impact, une notification coûte 1 (mise en file) + 1 (retrait) + 1 par
        // candidat évalué ; un rejet par le filtre n'écrit rien. Un type sans abonné ne coûte rien de plus.
        long Work(CombatantSetup bastion)
        {
            var engine = new CombatEngine(Setup(new[] { Cmd(0, Frappe) }, new[] { bastion, Dummy() }, rules: NoUpkeep), new MemoryCombatLog());
            engine.Step();
            return engine.LastTickWork;
        }
        long baseline = Work(Bearer());
        var rejected = On(TriggerEvent.HitDealt, filter: new TriggerFilter(skills: new[] { Heurt }));
        Assert.Equal(baseline + 3, Work(Bearer(passive: new[] { rejected })));
        Assert.Equal(baseline + 4, Work(Bearer(passive: new[] { rejected, rejected })));
        Assert.Equal(baseline, Work(Bearer(passive: new[] { On(TriggerEvent.Block, TriggerRole.Target) })));   // aucun blocage
    }

    [Fact]
    public void Diagnostics_jamais_notifies()
    {
        // Le Bastion réagit à chaque état appliqué sur lui. Le second étourdissement est refusé (immunité, stateBlocked) : aucune
        // notification. Les suppressions correspondent exactement aux lignes stateApplied du Bastion.
        var stun = Strike("assomme", 100, applies: Apply("stun"));
        var (events, graph) = Simulate(Setup(Array.Empty<CombatCommand>(),
            new CombatantSetup[] { Bearer(passive: new[] { On(TriggerEvent.StateApplied, TriggerRole.Target) }), Hitter(1000, 2000, attacks: stun) },
            durationMs: 8000, rules: Depth0));
        Assert.NotEmpty(events.OfKind("stateBlocked"));
        var applied = events.OfKind("stateApplied").Select(Eid).ToList();
        Assert.Equal(applied, graph.Cited.Select(c => c.Cause));
    }

    [Fact]
    public void Mort_reaction_du_tueur_et_porteur_mort_ignore()
    {
        // « fort » tue « faible » d'une Entaille (saignement posé) : la mort notifie onDeath (rôle source : le tueur) ; le retrait
        // du saignement notifie onStateRemoved, auquel « faible », mort, ne réagit pas, mais « fort », source de l'état, oui.
        var fort = Bearer("fort", passive: new[] { On(TriggerEvent.Death), On(TriggerEvent.StateRemoved, TriggerRole.Source) }, team: "rouge",
            attributes: Attrs(force: 500), actives: new[] { Entaille, Frappe, Heurt, Onde });
        var faible = Bearer("faible", passive: new[] { On(TriggerEvent.StateRemoved, TriggerRole.Target) }, team: "bleu", attributes: Attrs(vitalite: 0));
        var (events, _) = Simulate(Setup(new[] { Cmd(0, Entaille, "fort", "faible") }, new CombatantSetup[] { fort, faible }, rules: Depth0));
        JsonElement died = Assert.Single(events.OfKind("died"));
        JsonElement removed = Assert.Single(events.OfKind("stateRemoved"));
        var endOfTick = Suppressed(events).Where(e => e.Str("phase") == "endOfTick").Select(e => (e.Str("trigger")!, e.Int("cause"))).ToList();
        Assert.Equal(new[] { ("skill.essai.passif/0@fort#0", Eid(died)), ("skill.essai.passif/1@fort#0", Eid(removed)) }, endOfTick);
    }

    [Fact]
    public void Aucun_type_d_abonnement_ne_designe_un_diagnostic()
    {
        string[] diagnostics = { "stateBlocked", "commandRejected", "attackSkipped", "triggerSuppressed", "reactionCancelled", "simulationAborted", "combatEnded" };
        foreach (string id in TriggerIds.Events.Keys)
            Assert.DoesNotContain(diagnostics, d => id.Equals("on" + char.ToUpperInvariant(d[0]) + d.Substring(1), StringComparison.Ordinal));
        Assert.Equal(12, Enum.GetValues<TriggerEvent>().Length);
    }
}
