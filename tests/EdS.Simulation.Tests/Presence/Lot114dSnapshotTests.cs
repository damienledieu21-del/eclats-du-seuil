using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Presence.PresenceKit;

namespace EdS.Simulation.Tests.Presence;

/// <summary>
/// Sous-lot 11.4d : instantané d'état (ARCHITECTURE_MONDE.md, section 5 : « s'écrit de façon canonique et se relit à l'identique »).
/// Aller-retour exact : un moteur relu d'un instantané, sans rejouer le passé, écrit la même suite de journal que l'original et passe par
/// les mêmes états, tick après tick ; le texte relu puis réécrit est identique ; un instantané d'un autre combat, d'un autre moteur ou
/// retouché est refusé.
/// </summary>
[Trait("Category", "Presence")]
public class Lot114dSnapshotTests
{
    /// <summary>
    /// Relit l'état de <paramref name="engine"/> dans un second moteur, puis avance les deux ensemble jusqu'à la fin : mêmes lignes, même
    /// instantané à chaque tick, même résultat.
    /// </summary>
    internal static void AssertResumesIdentically(CombatEngine engine, MemoryCombatLog log)
    {
        CombatSnapshot snapshot = engine.Snapshot();
        CombatSnapshot parsed = CombatSnapshot.Parse(snapshot.Text);
        Assert.Equal((snapshot.Tick, snapshot.Fingerprint, snapshot.Setup), (parsed.Tick, parsed.Fingerprint, parsed.Setup));
        var resumed = new MemoryCombatLog();
        CombatEngine copy = CombatEngine.Restore(engine.CompletedSetup(), parsed, resumed);
        Assert.Equal(engine.CurrentTick, copy.CurrentTick);
        int from = log.Lines.Count;
        while (true)
        {
            bool a = engine.Step(), b = copy.Step();
            Assert.Equal(a, b);
            if (!a) break;
            Assert.Equal(engine.Snapshot().Text, copy.Snapshot().Text);
        }
        Assert.Equal(log.Lines.Skip(from), resumed.Lines);
        Assert.Equal((engine.Result!.EndReason, engine.Result.EndTick, engine.Result.Winner), (copy.Result!.EndReason, copy.Result.EndTick, copy.Result.Winner));
        Assert.Equal(engine.Result.Combatants.Select(c => (c.Id, c.DamageDealt, c.DamageTaken, c.Health, c.Presence)),
            copy.Result.Combatants.Select(c => (c.Id, c.DamageDealt, c.DamageTaken, c.Health, c.Presence)));
    }

    private static CombatSetup Training() => Setup(new[]
    {
        Cmd(0, Entaille, target: "assaillant"), GuardStart(1000), GuardStop(2500), Cmd(2600, Assommer, target: "assaillant"), Parry(3900),
        Cmd(5000, Frappe, target: "assaillant"), Leave(7000, "allie"),
    }, new CombatantSetup[]
    {
        Tank(endurance: 30, actives: new[] { Frappe, Entaille, Assommer, Onde }), Tank("allie"), Tank("renfort").Arriving(4000),
        Assailant(firstAttackMs: 1000, intervalMs: 1300, attacks: new[] { Strike("morsure", 80, EdS.Domain.Stats.DamageType.Physique, false, Apply("bleed", "0.5")), Blow() }),
    }, durationMs: 9000);

    [Fact]
    public void Un_instantane_a_chaque_tick_se_relit_et_le_combat_continue_a_l_identique()
    {
        // Pour quelques ticks (départ, avant une arrivée, au milieu d'un DOT, après le départ), un second moteur relu de l'instantané
        // continue comme l'original.
        foreach (long at in new long[] { 0, 1, 19, 37, 79, 80, 81, 140, 141, 179 })
        {
            var log = new MemoryCombatLog();
            var engine = new CombatEngine(Training(), log);
            while (engine.CurrentTick < at) engine.Step();
            AssertResumesIdentically(engine, log);
        }
    }

    [Fact]
    public void Meme_combat_meme_instantane_et_texte_canonique()
    {
        var a = new CombatEngine(Training(), NullCombatLog.Instance);
        var b = new CombatEngine(Training(), NullCombatLog.Instance);
        for (int i = 0; i < 100; i++)
        {
            CombatSnapshot sa = a.Snapshot(), sb = b.Snapshot();
            Assert.Equal(sa.Text, sb.Text);
            Assert.Equal(sa.Fingerprint, sb.Fingerprint);
            Assert.StartsWith("sha256:", sa.Fingerprint);
            Assert.Equal(71, sa.Fingerprint.Length);
            Assert.DoesNotContain(' ', sa.Text);
            Assert.Equal(i, sa.Tick);
            a.Step();
            b.Step();
        }
        // Une ligne d'en-tête, une par combattant, une par élément de la file.
        string[] lines = a.Snapshot().Text.Split('\n');
        Assert.StartsWith("{\"snapshot\":1,\"engineVersion\":\"" + CombatEngine.Version + "\",\"formatVersion\":" + CombatEngine.LogFormatVersion, lines[0]);
        Assert.Equal(new[] { "bastion", "allie", "renfort", "assaillant" },
            lines.Skip(1).Take(4).Select(l => l.Substring(12, l.IndexOf('"', 12) - 12)));
        Assert.All(lines.Skip(5), l => Assert.StartsWith("{\"entry\":", l));
    }

    [Fact]
    public void Instantane_d_un_autre_combat_d_un_autre_moteur_ou_retouche_refuse()
    {
        var engine = new CombatEngine(Training(), NullCombatLog.Instance);
        for (int i = 0; i < 30; i++) engine.Step();
        CombatSnapshot snapshot = engine.Snapshot();
        CombatSetup setup = engine.CompletedSetup();
        // Autre combat (autre graine) : empreinte d'en-tête différente.
        CombatSetup other = Setup(setup.Commands, setup.Combatants, seed: 43, durationMs: 9000);
        Assert.Contains("autre combat", Assert.Throws<FormatException>(() => CombatEngine.Restore(other, snapshot, NullCombatLog.Instance)).Message);
        // Autre moteur.
        CombatSnapshot older = CombatSnapshot.Parse(snapshot.Text.Replace("\"engineVersion\":\"" + CombatEngine.Version + "\"", "\"engineVersion\":\"0.18.0\""));
        Assert.Contains("0.18.0", Assert.Throws<FormatException>(() => CombatEngine.Restore(setup, older, NullCombatLog.Instance)).Message);
        // Texte retouché : une valeur changée est un autre état, d'une autre empreinte ; un champ inconnu (texte non canonique), manquant ou
        // une ligne de trop est refusé.
        CombatSnapshot changed = CombatSnapshot.Parse(snapshot.Text.Replace("\"lastEid\":", "\"lastEid\":1"));
        Assert.NotEqual(snapshot.Fingerprint, changed.Fingerprint);
        Assert.Equal(changed.Text, CombatEngine.Restore(setup, changed, NullCombatLog.Instance).Snapshot().Text);
        Assert.Contains("canonique", Assert.Throws<FormatException>(() => CombatEngine.Restore(setup,
            CombatSnapshot.Parse(snapshot.Text.Replace("\"lastEid\":", "\"inconnu\":0,\"lastEid\":")), NullCombatLog.Instance)).Message);
        Assert.Throws<FormatException>(() => CombatEngine.Restore(setup, CombatSnapshot.Parse(snapshot.Text.Replace(",\"guarding\":", ",\"garde\":")), NullCombatLog.Instance));
        Assert.Throws<FormatException>(() => CombatEngine.Restore(setup, CombatSnapshot.Parse(snapshot.Text + "\n{\"entry\":0}"), NullCombatLog.Instance));
        // Revue Codex de la PR #32 : curseur de file et éléments relus tels qu'entre deux ticks.
        string head = snapshot.Text.Split('\n')[0];
        Assert.Contains("\"queue\":{\"started\":true,\"tick\":29,\"phase\":7,", head);
        Assert.Contains("Curseur", Assert.Throws<FormatException>(() => CombatEngine.Restore(setup,
            CombatSnapshot.Parse(snapshot.Text.Replace("\"queue\":{\"started\":true,\"tick\":29,\"phase\":7,", "\"queue\":{\"started\":true,\"tick\":30,\"phase\":7,")),
            NullCombatLog.Instance)).Message);
        Assert.Contains("Curseur", Assert.Throws<FormatException>(() => CombatEngine.Restore(setup,
            CombatSnapshot.Parse(snapshot.Text.Replace("\"queue\":{\"started\":true,\"tick\":29,\"phase\":7,", "\"queue\":{\"started\":true,\"tick\":29,\"phase\":2,")),
            NullCombatLog.Instance)).Message);
        string firstEntry = snapshot.Text.Split('\n').First(l => l.StartsWith("{\"entry\":", StringComparison.Ordinal));
        string past = System.Text.RegularExpressions.Regex.Replace(firstEntry, "\"tick\":[0-9]+", "\"tick\":12");
        Assert.Contains("déjà passé", Assert.Throws<FormatException>(() => CombatEngine.Restore(setup, CombatSnapshot.Parse(snapshot.Text.Replace(firstEntry, past)),
            NullCombatLog.Instance)).Message);
        Assert.Throws<FormatException>(() => CombatSnapshot.Parse("{\"snapshot\":2,\"tick\":0,\"setup\":\"x\"}"));
        Assert.Throws<FormatException>(() => CombatSnapshot.Parse("{\"snapshot\":1, \"tick\":0}"));
        // Un instantané refusé n'a rien écrit.
        var log = new MemoryCombatLog();
        Assert.Throws<FormatException>(() => CombatEngine.Restore(other, snapshot, log));
        Assert.Empty(log.Lines);
        while (engine.Step()) { }
        Assert.Throws<InvalidOperationException>(() => engine.Snapshot());
    }

    /// <summary>Le texte avec, dans la première ligne qui commence par <paramref name="prefix"/>, la première occurrence de <paramref name="from"/> remplacée.</summary>
    private static string Tampered(string text, string prefix, string from, string to)
    {
        string[] lines = text.Split('\n');
        int i = Array.FindIndex(lines, l => l.StartsWith(prefix, StringComparison.Ordinal));
        Assert.True(i >= 0, prefix);
        int at = lines[i].IndexOf(from, StringComparison.Ordinal);
        Assert.True(at >= 0, $"{from} absent de {lines[i]}");
        lines[i] = lines[i].Substring(0, at) + to + lines[i].Substring(at + from.Length);
        return string.Join("\n", lines);
    }

    private static void AssertRefused(CombatSetup setup, string text, IEnumerable<(string Prefix, string From, string To, string Message)> cases)
    {
        foreach (var (prefix, from, to, message) in cases)
        {
            FormatException? error = null;
            try { CombatEngine.Restore(setup, CombatSnapshot.Parse(Tampered(text, prefix, from, to)), NullCombatLog.Instance); }
            catch (FormatException e) { error = e; }
            Assert.True(error != null && error.Message.Contains(message, StringComparison.Ordinal), $"{prefix} {from} → {to} : {error?.Message ?? "accepté"}");
        }
    }

    [Fact]
    public void Instantane_retouche_en_un_etat_impossible_refuse()
    {
        // Revue Codex de la PR #32 (2e tour) : un état retouché se réécrit à l'identique ; il est refusé s'il n'est pas un état que le moteur
        // atteint entre deux ticks. Combat positionné au tick 15 : une onde annoncée (impact au tick 40), le tour suivant de l'ogre en file.
        CombatSetup spatial = Space.SpaceTestKit.Spatial(Space.SpaceTestKit.Arena(10), new CombatantSetup[]
        {
            Space.SpaceTestKit.Player("bastion", Space.SpaceTestKit.At("0", "0")),
            Space.SpaceTestKit.Creature("ogre", Space.SpaceTestKit.At("3", "0"), health: 500, firstAttackMs: 500,
                attacks: new AssailantAttack("onde", 10, shape: AttackShape.Circle, telegraphMs: 1500, radius: Fixed.Parse("2"))),
            Space.SpaceTestKit.PlacedDummy("cible", Space.SpaceTestKit.At("-5", "-5")),
        }, new[] { Space.SpaceTestKit.Move(0, "bastion", "0", "1") }, durationMs: 6000);
        var engine = new CombatEngine(spatial, NullCombatLog.Instance);
        while (engine.CurrentTick < 15) engine.Step();
        string text = engine.Snapshot().Text;
        CombatSetup setup = engine.CompletedSetup();
        Assert.Equal(text, CombatEngine.Restore(setup, CombatSnapshot.Parse(text), NullCombatLog.Instance).Snapshot().Text);
        string ogre = Tampered(text, "{\"fighter\":\"ogre\"", "\"health\":500", "\"health\":499");   // un autre état possible : accepté
        Assert.Equal(ogre, CombatEngine.Restore(setup, CombatSnapshot.Parse(ogre), NullCombatLog.Instance).Snapshot().Text);
        const string Head = "{\"snapshot\"", Bastion = "{\"fighter\":\"bastion\"", Ogre = "{\"fighter\":\"ogre\"", Cible = "{\"fighter\":\"cible\"",
            Impact = "{\"entry\":3,", Turn = "{\"entry\":2,";
        AssertRefused(setup, text, new[]
        {
            // Attaques annoncées : chacune un impact en file, une seule fois, toutes.
            (Head, "\"announced\":[3]", "\"announced\":[3,3]", "Attaque annoncée 3"),
            (Head, "\"announced\":[3]", "\"announced\":[2]", "Attaque annoncée 2"),
            (Head, "\"announced\":[3]", "\"announced\":[]", "Impact en file absent"),
            // Points de vie et mort.
            (Ogre, "\"health\":500", "\"health\":-1", "Points de vie de « ogre »"),
            (Ogre, "\"health\":500", "\"health\":501", "Points de vie de « ogre »"),
            (Ogre, "\"health\":500", "\"health\":null", "Points de vie de « ogre »"),
            (Cible, "\"health\":null", "\"health\":7", "Points de vie de « cible »"),
            (Ogre, "\"dead\":false", "\"dead\":true", "Mort de « ogre »"),
            (Ogre, "\"health\":500", "\"health\":0", "Mort de « ogre »"),
            // Ressources et garde.
            (Bastion, "\"endurance\":100", "\"endurance\":101", "Endurance de « bastion »"),
            (Bastion, "\"resonance\":0", "\"resonance\":1000000", "Résonance, charges ou garde de « bastion »"),
            (Ogre, "\"resonance\":0", "\"resonance\":1", "Résonance, charges ou garde de « ogre »"),
            (Ogre, "\"guarding\":false", "\"guarding\":true", "Résonance, charges ou garde de « ogre »"),
            // Présence.
            (Bastion, "\"leaveCommand\":null", "\"leaveCommand\":0", "Départ de « bastion » encore demandé"),
            (Ogre, "\"presence\":0", "\"presence\":1", "Arrivée de « ogre »"),
            (Ogre, "\"presence\":0", "\"presence\":2", "Départ de « ogre »"),
            // Place et orientation.
            (Ogre, "\"x\":30000", "\"x\":100000", "Place de « ogre » hors de l'aire"),
            (Bastion, "\"facingZ\":10000", "\"facingZ\":5000", "non unitaire"),
            // File : zone, impulsion, assaillant, tour, phase.
            (Impact, "\"reach\":20000", "\"reach\":30000", "zone incohérente"),
            (Impact, "\"shape\":2", "\"shape\":9", "AttackShape inconnu"),
            (Impact, "\"pulse\":0", "\"pulse\":1", "impact impossible"),
            (Impact, "\"assailant\":\"ogre\"", "\"assailant\":\"bastion\"", "pas un assaillant"),
            (Turn, "\"fighter\":\"ogre\"", "\"fighter\":\"bastion\"", "tour impossible"),
            (Turn, "\"phase\":2", "\"phase\":3", "tour impossible"),
            // Compteurs.
            (Head, "\"commandsPerTick\":[]", "\"commandsPerTick\":[[\"inconnu\",20,1]]", "Commandes comptées"),
            (Head, "\"initialEvents\":-1", "\"initialEvents\":5", "Compteurs"),
            (Head, "\"deferredPending\":0", "\"deferredPending\":1", "réactions reportées"),
            (Head, "\"lastEid\":2", "\"lastEid\":-1", "Compteurs"),
            // Revue Codex de la PR #32 (3e tour) : état qui se déduit du combat complété, tours, parade, provenances.
            (Head, "\"subscribed\":[]", "\"subscribed\":[0]", "Abonnements incohérents"),
            (Head, "\"commandCount\":1", "\"commandCount\":2", "commandes comptées"),
            (Turn, "\"tick\":50", "\"tick\":51", "Tours de « ogre »"),
            (Turn, "\"turn\":1", "\"turn\":0", "Tours de « ogre »"),
            (Bastion, "\"parryOpen\":false", "\"parryOpen\":true", "Parade ouverte de « bastion »"),
            (Head, "\"lastEid\":2", "\"lastEid\":1", "Provenance citant l'événement 2"),
        });

        // Combat d'entraînement au tick 30 : un saignement sur chacun, des commandes à venir, un renfort attendu.
        var training = new CombatEngine(Training(), NullCombatLog.Instance);
        while (training.CurrentTick < 30) training.Step();
        text = training.Snapshot().Text;
        setup = training.CompletedSetup();
        const string Assaillant = "{\"fighter\":\"assaillant\"", Allie = "{\"fighter\":\"allie\"", Renfort = "{\"fighter\":\"renfort\"";
        AssertRefused(setup, text, new[]
        {
            (Bastion, "\"id\":2,\"generation\"", "\"id\":3,\"generation\"", "instance impossible ou en double"),
            (Assaillant, "\"id\":1,\"generation\"", "\"id\":2,\"generation\"", "instance impossible ou en double"),
            (Bastion, "\"keySource\":\"assaillant\"", "\"keySource\":null", "clé de cumul"),
            (Bastion, "\"untilTick\":140", "\"untilTick\":20", "déjà échue"),
            (Bastion, "\"absorb\":0,", "\"absorb\":5,", "absorption impossible"),
            ("{\"entry\":3,", "\"index\":2", "\"index\":3", "hors de son tick ou en double"),
            ("{\"entry\":0,", "\"fighter\":\"renfort\"", "\"fighter\":\"allie\"", "n'est pas attendu"),
            (Renfort, "\"presence\":1", "\"presence\":0", "n'est pas attendu"),
            (Allie, "\"arrivedTick\":null", "\"arrivedTick\":10", "Bilan de présence"),
            (Head, "[\"bastion\",50,1]", "[\"bastion\",50,2]", "par acteur et par tick"),
            (Bastion, "\"nextTick\":60", "\"nextTick\":80", "échéance absente de la file"),
            ("{\"entry\":9,", "\"instance\":1}", "\"instance\":0}", "Altération non numérotée"),
        });

        // Abonnements d'un déclencheur permanent (riposte aux ticks de DOT) : la liste reçue doit être exactement celle des combattants.
        CombatSetup riposte = Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Bearer(new[] { Riposte(TriggerEvent.DotTick) }),
            Hitter(Strike("entaille", 100, applies: Apply("bleed", "0.4"))) }, durationMs: 8000, rules: NoUpkeep);
        var reacting = new CombatEngine(riposte, NullCombatLog.Instance);
        while (reacting.CurrentTick < 5) reacting.Step();
        text = reacting.Snapshot().Text;
        string subscribed = text.Split('\n')[0];
        int from = subscribed.IndexOf("\"subscribed\":[", StringComparison.Ordinal);
        string list = subscribed.Substring(from, subscribed.IndexOf(']', from) - from + 1);
        Assert.NotEqual("\"subscribed\":[]", list);
        AssertRefused(reacting.CompletedSetup(), text, new[] { (Head, list, "\"subscribed\":[]", "Abonnements incohérents") });
    }

    [Fact]
    public void Un_moteur_relu_accepte_commandes_et_arrivees_et_se_rejoue_a_l_identique()
    {
        // L'original et la copie reçoivent les mêmes commandes et la même arrivée après l'instantané : ils restent identiques, et le combat
        // complété de la copie (scénario relu + ses soumissions) se rejoue comme l'original de bout en bout.
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(Training(), log);
        while (engine.CurrentTick < 50) engine.Step();
        var resumed = new MemoryCombatLog();
        CombatEngine copy = CombatEngine.Restore(engine.CompletedSetup(), engine.Snapshot(), resumed);
        int from = log.Lines.Count;
        foreach (CombatEngine e in new[] { engine, copy })
        {
            e.Submit(Cmd(2500, Onde, target: null));
            e.Join(Tank("tardif").Arriving(2500));
            e.Submit(GuardStart(2500, "tardif"));
        }
        while (engine.Step() | copy.Step()) { }
        Assert.Equal(log.Lines.Skip(from), resumed.Lines);
        var full = new MemoryCombatLog();
        CombatEngine.Run(copy.CompletedSetup(), full);
        Assert.Equal(log.Lines.Skip(1), full.Lines.Skip(1));
    }

    /// <summary>
    /// Pour chaque tick où l'instantané contient <paramref name="marker"/>, un moteur avancé jusque-là est relu et continue à l'identique ;
    /// renvoie le nombre de ces ticks.
    /// </summary>
    private static int ResumeWhere(CombatSetup setup, string marker)
    {
        var probe = new CombatEngine(setup, NullCombatLog.Instance);
        var ticks = new List<long>();
        while (!probe.IsFinished)
        {
            if (probe.Snapshot().Text.Contains(marker, StringComparison.Ordinal)) ticks.Add(probe.CurrentTick);
            probe.Step();
        }
        foreach (long at in ticks)
        {
            var log = new MemoryCombatLog();
            var engine = new CombatEngine(setup, log);
            while (engine.CurrentTick < at) engine.Step();
            AssertResumesIdentically(engine, log);
        }
        return ticks.Count;
    }

    private static TriggerDefinition Riposte(TriggerEvent on) => new(on, new[] { TriggerEffect.Strike(TriggerTarget.EventSource, Fixed.Parse("0.5")) }, TriggerRole.Target);

    private static CharacterSetup Bearer(IEnumerable<TriggerDefinition>? passive = null, int vitalite = 200) =>
        Character("bastion", "joueurs", 0, Attrs(vitalite: vitalite), null, Class(passiveTriggers: passive), passives: passive is null ? null : new[] { Passif });

    private static AssailantSetup Hitter(params AssailantAttack[] attacks) => new("assaillant", "cibles", Fixed.Zero, Fixed.Zero, "bastion", 1000, 60_000, attacks);

    [Fact]
    public void Instantane_avec_une_reaction_reportee_au_tick_suivant()
    {
        // Riposte aux ticks de saignement (phase 4) : reportée en résolution du tick suivant ; entre les deux, elle est dans la file.
        CombatSetup setup = Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Bearer(new[] { Riposte(TriggerEvent.DotTick) }),
            Hitter(Strike("entaille", 100, applies: Apply("bleed", "0.4"))) }, durationMs: 8000, rules: NoUpkeep);
        Assert.Equal(3, ResumeWhere(setup, "\"item\":\"reaction\""));
    }

    [Fact]
    public void Instantane_avec_un_declencheur_porte_par_une_alteration_active_ou_retiree()
    {
        // « epine » porte la riposte. Cas 1 : elle expire au tick 61 (phase 1), après l'instantané : la réaction reportée la cite, active
        // (originGone au tick 61). Cas 2 : le porteur meurt au tick 60 (fin de tick), ses altérations sont retirées : la réaction reportée
        // cite une instance disparue, recréée détachée à la relecture (bearerDead au tick 61).
        var epine = new StatusDefinition("epine", StatusCategory.Modifier, 2050, StatusStacking.Refresh, triggers: new[] { Riposte(TriggerEvent.DotTick) });
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, Statuses = new StatusCatalog(StatusCatalog.Provisional.Definitions.Append(epine)) };
        CombatSetup alive = Setup(Array.Empty<CombatCommand>(), new CombatantSetup[]
        {
            Bearer(), Hitter(Strike("piqure", 100, applies: new[] { Apply("bleed", "0.4"), Apply("epine") })),
        }, durationMs: 4000, rules: rules);
        Assert.True(ResumeWhere(alive, "\"detached\":false") >= 1);
        Assert.True(ResumeWhere(alive, "\"slots\":[") >= 20);
        CombatSetup dying = Setup(Array.Empty<CombatCommand>(), new CombatantSetup[]
        {
            Bearer(vitalite: 0), Tank("allie"), Hitter(Strike("piqure", 1, applies: new[] { Apply("bleed", "100"), Apply("epine") })),
        }, durationMs: 4000, rules: rules);
        Assert.Equal(1, ResumeWhere(dying, "\"detached\":true"));
        var (_, log) = Run(dying);
        Assert.Equal("bearerDead", Events(log).OfKind("reactionCancelled").Single().Str("reason"));
    }

    [Fact]
    public void Combats_aleatoires_arrivees_departs_et_instantanes()
    {
        // Combats du générateur des arrivées (positionnés ou non, arrivées pilotées, départs, morts, DOT, étourdissements, zones annoncées) :
        // à trois ticks tirés au hasard, un instantané relu continue à l'identique.
        var random = new Random(11441);
        int resumed = 0, withImpacts = 0, withPending = 0;
        for (int n = 0; n < 40; n++)
        {
            Lot114dJoinTests.Plan plan = Lot114dJoinTests.RandomPlan(random);
            long[] at = Enumerable.Range(0, 3).Select(_ => (long)random.Next(0, 80)).ToArray();
            foreach (long tick in at)
            {
                var log = new MemoryCombatLog();
                var engine = new CombatEngine(plan.Base, log);
                var known = new HashSet<string>(plan.Base.Combatants.Select(c => c.Id), StringComparer.Ordinal);
                while (!engine.IsFinished && engine.CurrentTick < tick)
                {
                    long t = engine.CurrentTick;
                    foreach (CombatantSetup c in plan.Joins.Where(c => c.ArrivalMs / 50 == t))
                    {
                        engine.Join(c);
                        known.Add(c.Id);
                    }
                    foreach (var group in plan.Live.Where(c => c.AtMs / 50 == t && known.Contains(c.Actor) && (c.Target is null || known.Contains(c.Target))).GroupBy(c => c.Actor))
                        try { engine.SubmitAll(group.ToList()); }
                        catch (InvalidOperationException) { }
                    engine.Step();
                }
                if (engine.IsFinished) continue;
                string text = engine.Snapshot().Text;
                if (text.Contains("\"item\":\"impact\"")) withImpacts++;
                if (text.Contains("\"presence\":1")) withPending++;
                AssertResumesIdentically(engine, log);
                resumed++;
            }
        }
        Assert.True(resumed > 100 && withImpacts > 10 && withPending > 20, $"relus {resumed}, avec impacts {withImpacts}, avec arrivées attendues {withPending}");
    }
}
