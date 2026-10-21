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

    /// <summary>Le texte avec un élément de file ajouté (ligne sans numéro), sous la prochaine séquence libre (en-tête mis à jour).</summary>
    private static string WithEntry(string text, string body)
    {
        List<string> lines = text.Split('\n').ToList();
        var head = System.Text.RegularExpressions.Regex.Match(lines[0], "\"next\":([0-9]+),\"count\":([0-9]+)");
        long next = long.Parse(head.Groups[1].Value), count = long.Parse(head.Groups[2].Value);
        lines[0] = lines[0].Replace(head.Value, $"\"next\":{next + 1},\"count\":{count + 1}");
        var peak = System.Text.RegularExpressions.Regex.Match(lines[0], "\"peakPending\":([0-9]+)");
        lines[0] = lines[0].Replace(peak.Value, $"\"peakPending\":{Math.Max(long.Parse(peak.Groups[1].Value), count + 1)}");
        lines.Add("{\"entry\":" + next + "," + body);
        // À sa place dans l'ordre de la file : tick, phase, séquence.
        static (long, long, long) Order(string l) => (long.Parse(System.Text.RegularExpressions.Regex.Match(l, "\"tick\":([0-9]+)").Groups[1].Value),
            long.Parse(System.Text.RegularExpressions.Regex.Match(l, "\"phase\":([0-9]+)").Groups[1].Value),
            long.Parse(System.Text.RegularExpressions.Regex.Match(l, "^\\{\"entry\":([0-9]+)").Groups[1].Value));
        List<string> entries = lines.Where(l => l.StartsWith("{\"entry\":", StringComparison.Ordinal)).OrderBy(Order).ToList();
        return string.Join("\n", lines.Where(l => !l.StartsWith("{\"entry\":", StringComparison.Ordinal)).Concat(entries));
    }

    private static void AssertRefused(CombatSetup setup, string text, string message)
    {
        Exception? refused = Record.Exception(() => CombatEngine.Restore(setup, CombatSnapshot.Parse(text), NullCombatLog.Instance));
        Assert.True(refused is FormatException && refused.Message.Contains(message, StringComparison.Ordinal), refused?.Message ?? "accepté");
    }

    [Fact]
    public void Marche_generateur_instances_retirees_et_competences_non_equipees_refuses()
    {
        // Revue Codex de la PR #32 (21e tour). 1) Marche recoupée avec la dernière commande move ou stop : arrêté après un stop, puis en
        // marche vers +x ; une marche retouchée ou une autre direction sont refusées.
        const string Bastion = "{\"fighter\":\"bastion\"";
        CombatSetup walk = Space.SpaceTestKit.Spatial(Space.SpaceTestKit.Arena(10), new CombatantSetup[]
        {
            Space.SpaceTestKit.Player("bastion", Space.SpaceTestKit.At("0", "0")),
            Space.SpaceTestKit.PlacedDummy("cible", Space.SpaceTestKit.At("-5", "-5")),
        }, new[] { Space.SpaceTestKit.Move(0, "bastion", "0", "1"), Space.SpaceTestKit.Stop(250, "bastion"), Space.SpaceTestKit.Move(500, "bastion", "1", "0") },
            durationMs: 3000);
        var walker = new CombatEngine(walk, NullCombatLog.Instance);
        while (walker.CurrentTick < 8) walker.Step();
        string stopped = walker.Snapshot().Text;
        Assert.Equal(stopped, CombatEngine.Restore(walker.CompletedSetup(), CombatSnapshot.Parse(stopped), NullCombatLog.Instance).Snapshot().Text);
        AssertRefused(walker.CompletedSetup(), stopped, new[] { (Bastion, "\"moving\":false", "\"moving\":true", "Marche de « bastion » différente") });
        while (walker.CurrentTick < 12) walker.Step();
        string moving = walker.Snapshot().Text;
        AssertRefused(walker.CompletedSetup(), moving, new[]
        {
            (Bastion, "\"directionX\":10000,\"directionZ\":0", "\"directionX\":0,\"directionZ\":10000", "Marche de « bastion » différente"),
            (Bastion, "\"moving\":true", "\"moving\":false", "Marche de « bastion » différente"),
        });

        // 2) Générateur : après quelques ticks (coups critiques possibles du Bastion), un état retouché est hors d'atteinte de celui du
        // départ ; un assaillant ne tire jamais.
        var training = new CombatEngine(Training(), NullCombatLog.Instance);
        while (training.CurrentTick < 55) training.Step();
        string text = training.Snapshot().Text;
        CombatSetup setup = training.CompletedSetup();
        Assert.Equal(text, CombatEngine.Restore(setup, CombatSnapshot.Parse(text), NullCombatLog.Instance).Snapshot().Text);
        foreach (string who in new[] { "bastion", "assaillant" })
        {
            string prefix = "{\"fighter\":\"" + who + "\"";
            string draw = System.Text.RegularExpressions.Regex.Match(text.Split('\n').First(l => l.StartsWith(prefix, StringComparison.Ordinal)), "\"random\":\\[\"[0-9a-f]").Value;
            string redraw = draw.Substring(0, draw.Length - 1) + (draw[draw.Length - 1] == '0' ? '1' : '0');
            AssertRefused(setup, text, new[] { (prefix, draw, redraw, $"État du générateur de « {who} »") });
        }

        // 3) Instances citées par la file : l'étourdissement de l'assaillant (actif) cité pour le Bastion ; un second tick du saignement ;
        // une instance retirée citée sous deux altérations, ou avec une génération au-delà des rangs d'application.
        string expiry = text.Split('\n').First(l => l.Contains("\"item\":\"expiry\"", StringComparison.Ordinal) && l.Contains("\"state\":\"stun\"", StringComparison.Ordinal));
        string stun = expiry.Substring(expiry.IndexOf(',') + 1);
        AssertRefused(setup, WithEntry(text, stun.Replace("\"fighter\":\"assaillant\"", "\"fighter\":\"bastion\"", StringComparison.Ordinal)), "citée avec une autre identité");
        string dot = text.Split('\n').First(l => l.Contains("\"item\":\"dot\"", StringComparison.Ordinal));
        string bleed = dot.Substring(dot.IndexOf(',') + 1);
        string other = System.Text.RegularExpressions.Regex.Match(bleed, "\"fighter\":\"[a-z]+\"").Value;
        AssertRefused(setup, WithEntry(text, bleed.Replace(other, other == "\"fighter\":\"bastion\"" ? "\"fighter\":\"allie\"" : "\"fighter\":\"bastion\"", StringComparison.Ordinal)),
            "autre identité");
        // Plus tard, une instance retirée que la file ne cite plus (au moins deux instances numérotées).
        long gone = 0, lastInstance = 0, lastApplication = 0;
        string late = text;
        while (gone == 0 && !training.IsFinished)
        {
            training.Step();
            late = training.Snapshot().Text;
            lastInstance = long.Parse(System.Text.RegularExpressions.Regex.Match(late, "\"lastStatusInstance\":([0-9]+)").Groups[1].Value);
            lastApplication = long.Parse(System.Text.RegularExpressions.Regex.Match(late, "\"lastApplication\":([0-9]+)").Groups[1].Value);
            var live = System.Text.RegularExpressions.Regex.Matches(late, "\\{\"id\":([0-9]+),\"generation\"").Select(m => long.Parse(m.Groups[1].Value)).ToHashSet();
            if (lastInstance >= 2)
                gone = Enumerable.Range(1, (int)lastInstance).Select(i => (long)i).FirstOrDefault(i => !live.Contains(i)
                    && !late.Contains("\"instance\":" + i + ",", StringComparison.Ordinal));
        }
        Assert.True(gone > 0, "instance retirée");
        CombatSetup lateSetup = training.CompletedSetup();
        long tick = training.CurrentTick + 5;
        string Stale(string state, long generation) =>
            $"\"tick\":{tick},\"phase\":1,\"item\":\"expiry\",\"fighter\":\"assaillant\",\"state\":\"{state}\",\"keySource\":null,\"instance\":{gone},\"generation\":{generation}}}";
        string once = WithEntry(late, Stale("stun", 1));
        Assert.Equal(once, CombatEngine.Restore(lateSetup, CombatSnapshot.Parse(once), NullCombatLog.Instance).Snapshot().Text);   // échéance périmée possible
        AssertRefused(lateSetup, WithEntry(once, Stale("freeze", 2)), "autre identité");
        AssertRefused(lateSetup, WithEntry(late, Stale("stun", lastApplication)), "au-delà des rangs d'application");
        tick = training.CurrentTick + 100_000;
        AssertRefused(lateSetup, WithEntry(late, Stale("stun", 1)), "au-delà de la plus longue durée");

        // 4) Seules les compétences lançables comptent : le Bastion pose un saignement de coefficient 0,1 par son passif ; Entaille (0,5),
        // non équipée, ne relève pas la borne de sa puissance.
        var bleeding = new TriggerDefinition(TriggerEvent.SkillCast, new[] { TriggerEffect.ApplyStatus(TriggerTarget.EventTarget, "bleed", Fixed.Parse("0.1")) });
        CombatSetup passive = Setup(new[] { Cmd(0, Gratuite, target: "assaillant") }, new CombatantSetup[] { Bearer(new[] { bleeding }), Hitter(Strike("coup", 10)) },
            durationMs: 3000);
        var bleeder = new CombatEngine(passive, NullCombatLog.Instance);
        while (bleeder.CurrentTick < 5) bleeder.Step();
        string bled = bleeder.Snapshot().Text;
        Assert.Contains("\"state\":\"bleed\"", bled, StringComparison.Ordinal);
        Assert.Equal(bled, CombatEngine.Restore(bleeder.CompletedSetup(), CombatSnapshot.Parse(bled), NullCombatLog.Instance).Snapshot().Text);
        string tripled = new System.Text.RegularExpressions.Regex("(\"state\":\"bleed\"[^{}]*?\"power\":)([0-9]+)").Replace(bled,
            m => m.Groups[1].Value + (long.Parse(m.Groups[2].Value) * 3), 1);
        AssertRefused(bleeder.CompletedSetup(), tripled, "puissance au-delà du plus fort DOT possible");
    }

    [Fact]
    public void Impulsion_et_selection_recoupees_avec_leur_origine()
    {
        // Revue Codex de la PR #32 (22e tour). 1) Zone qui reste (impacts aux ticks 40, 50, 60, 70) : après la première impulsion, l'impact
        // en file porte pulse 1 et le tick du premier impact ; repartir de l'impulsion 0 au tick de l'entrée (les deux retouchés ensemble)
        // est refusé : le tour qui l'a annoncée donne le premier impact.
        var persistent = new AssailantAttack("brasier", 50, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false, null, AttackShape.Persistent,
            1000, radius: Fixed.Parse("1.5"), persistentMs: 2000, pulseMs: 500);
        CombatSetup zone = Space.SpaceTestKit.Spatial(Space.SpaceTestKit.Arena(20), new CombatantSetup[]
        {
            Space.SpaceTestKit.Player("bastion", Space.SpaceTestKit.At("0", "0")), Space.SpaceTestKit.PlacedDummy("cible", Space.SpaceTestKit.At("-5", "-5")),
            new AssailantSetup("ours", "cibles", Fixed.Zero, Fixed.Zero, "bastion", 1000, 10_000, new[] { persistent }, placement: Space.SpaceTestKit.At("8", "0")),
        }, new[] { CombatCommand.SelectTarget(0, "bastion", "ours"), CombatCommand.SelectTarget(500, "bastion", "cible") }, durationMs: 5000);
        var engine = new CombatEngine(zone, NullCombatLog.Instance);
        while (engine.CurrentTick < 45) engine.Step();
        string text = engine.Snapshot().Text;
        CombatSetup setup = engine.CompletedSetup();
        Assert.Equal(text, CombatEngine.Restore(setup, CombatSnapshot.Parse(text), NullCombatLog.Instance).Snapshot().Text);
        string impact = text.Split('\n').First(l => l.Contains("\"item\":\"impact\"", StringComparison.Ordinal));
        Assert.Contains("\"pulse\":1,\"impactTick\":40", impact, StringComparison.Ordinal);
        Assert.Contains("\"tick\":50,", impact, StringComparison.Ordinal);
        AssertRefused(setup, text, new[] { (impact.Substring(0, impact.IndexOf(',')), "\"pulse\":1,\"impactTick\":40", "\"pulse\":0,\"impactTick\":50",
            "sans le tour d'assaillant qui l'a annoncé") });
        // 2) Sélection : l'ours au tick 0, puis la cible au tick 10 ; remettre l'ours (sélectionné avant) ou rien est refusé.
        const string Bastion = "{\"fighter\":\"bastion\"";
        Assert.Contains("\"selected\":\"cible\"", text.Split('\n').First(l => l.StartsWith(Bastion, StringComparison.Ordinal)), StringComparison.Ordinal);
        // 3) Relecture d'ensemble : le Bastion d'entraînement a fini sa garde (guardStop à 2,5 s) ; une garde retouchée au tick 55 est refusée.
        var training = new CombatEngine(Training(), NullCombatLog.Instance);
        while (training.CurrentTick < 55) training.Step();
        AssertRefused(training.CompletedSetup(), training.Snapshot().Text, new[] { (Bastion, "\"guarding\":false", "\"guarding\":true", "Garde de « bastion » tenue après") });
        AssertRefused(setup, text, new[]
        {
            (Bastion, "\"selected\":\"cible\"", "\"selected\":\"ours\"", "Sélection de « bastion » différente de sa dernière commande select admise"),
            (Bastion, "\"selected\":\"cible\"", "\"selected\":null", "Sélection de « bastion » différente"),
        });
    }

    [Fact]
    public void Source_observee_et_rangs_d_application_recoupes()
    {
        // Revue Codex de la PR #32 (23e tour). 1) Riposte reportée à un tick de saignement (le porteur est la cible, la frappe vise la source
        // de l'effet) : changer la source de l'effet observé (l'assaillant) pour le Bastion lui-même est refusé.
        CombatSetup riposte = Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Bearer(new[] { Riposte(TriggerEvent.DotTick) }),
            Hitter(Strike("entaille", 100, applies: Apply("bleed", "0.4"))) }, durationMs: 8000, rules: NoUpkeep);
        var engine = new CombatEngine(riposte, NullCombatLog.Instance);
        while (!engine.Snapshot().Text.Contains("\"item\":\"reaction\"", StringComparison.Ordinal)) engine.Step();
        string text = engine.Snapshot().Text;
        CombatSetup setup = engine.CompletedSetup();
        string reaction = text.Split('\n').First(l => l.Contains("\"item\":\"reaction\"", StringComparison.Ordinal));
        AssertRefused(setup, text, new[] { (reaction.Substring(0, reaction.IndexOf(',')), "\"source\":\"assaillant\",\"target\":\"bastion\"",
            "\"source\":\"bastion\",\"target\":\"bastion\"", "source de l'effet observé différente de sa provenance") });
        // 2) Deux altérations actives (au tick 55 de l'entraînement) : échanger leurs rangs d'application est refusé.
        var training = new CombatEngine(Training(), NullCombatLog.Instance);
        while (training.CurrentTick < 55) training.Step();
        string snap = training.Snapshot().Text;
        var seqs = System.Text.RegularExpressions.Regex.Matches(snap, "\"appliedSeq\":([0-9]+)").Select(m => m.Groups[1].Value).ToList();
        Assert.True(seqs.Count >= 2, "deux altérations actives");
        string swapped = snap.Replace("\"appliedSeq\":" + seqs[0] + ",", "\"appliedSeq\":X,", StringComparison.Ordinal)
            .Replace("\"appliedSeq\":" + seqs[1] + ",", "\"appliedSeq\":" + seqs[0] + ",", StringComparison.Ordinal)
            .Replace("\"appliedSeq\":X,", "\"appliedSeq\":" + seqs[1] + ",", StringComparison.Ordinal);
        Assert.NotEqual(snap, swapped);
        AssertRefused(training.CompletedSetup(), swapped, "incohérent avec la ligne qui l'a posée");
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
        Assert.StartsWith("{\"snapshot\":" + CombatSnapshot.FormatVersion + ",\"engineVersion\":\"" + CombatEngine.Version + "\",\"formatVersion\":" + CombatEngine.LogFormatVersion, lines[0]);
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
        CombatSnapshot changed = CombatSnapshot.Parse(snapshot.Text.Replace("\"seq\":", "\"seq\":1"));   // 15e tour : lastEid ne dépasse plus seq
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
        Assert.Throws<FormatException>(() => CombatSnapshot.Parse("{\"snapshot\":99,\"tick\":0,\"setup\":\"x\"}"));
        Assert.Throws<FormatException>(() => CombatSnapshot.Parse("{\"snapshot\":1, \"tick\":0}"));
        // 21e tour : une version hors de la plage des entiers 32 bits est un texte illisible, pas un débordement.
        Assert.Throws<FormatException>(() => CombatSnapshot.Parse("{\"snapshot\":9223372036854775807,\"tick\":0,\"setup\":\"x\"}"));
        // Un instantané refusé n'a rien écrit.
        var log = new MemoryCombatLog();
        Assert.Throws<FormatException>(() => CombatEngine.Restore(other, snapshot, log));
        Assert.Empty(log.Lines);
        while (engine.Step()) { }
        Assert.Throws<InvalidOperationException>(() => engine.Snapshot());
    }

    /// <summary>Le texte avec, dans la première ligne qui commence par <paramref name="prefix"/>, la première occurrence de <paramref name="from"/> remplacée.</summary>
    internal static string Tampered(string text, string prefix, string from, string to)
    {
        string[] lines = text.Split('\n');
        int i = Array.FindIndex(lines, l => l.StartsWith(prefix, StringComparison.Ordinal));
        Assert.True(i >= 0, prefix);
        int at = lines[i].IndexOf(from, StringComparison.Ordinal);
        Assert.True(at >= 0, $"{from} absent de {lines[i]}");
        lines[i] = lines[i].Substring(0, at) + to + lines[i].Substring(at + from.Length);
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Le texte avec une copie de l'élément de file qui commence par <paramref name="prefix"/>, sous la prochaine séquence libre (compte et
    /// prochaine séquence et pic de la file de l'en-tête mis à jour) : une échéance en double.
    /// </summary>
    private static string WithDuplicate(string text, string prefix)
    {
        List<string> lines = text.Split('\n').ToList();
        string line = lines.First(l => l.StartsWith(prefix, StringComparison.Ordinal));
        var head = System.Text.RegularExpressions.Regex.Match(lines[0], "\"next\":([0-9]+),\"count\":([0-9]+)");
        long next = long.Parse(head.Groups[1].Value), count = long.Parse(head.Groups[2].Value);
        lines[0] = lines[0].Replace(head.Value, $"\"next\":{next + 1},\"count\":{count + 1}");
        var peak = System.Text.RegularExpressions.Regex.Match(lines[0], "\"peakPending\":([0-9]+)");
        lines[0] = lines[0].Replace(peak.Value, $"\"peakPending\":{long.Parse(peak.Groups[1].Value) + 1}");
        lines.Add("{\"entry\":" + next + line.Substring(line.IndexOf(',')));
        return string.Join("\n", lines);
    }

    /// <summary>Le texte sans l'élément de file qui commence par <paramref name="prefix"/> et contient <paramref name="marker"/> (compte de la file mis à jour).</summary>
    private static string WithoutEntry(string text, string prefix, string marker)
    {
        List<string> lines = text.Split('\n').ToList();
        int i = lines.FindIndex(l => l.StartsWith(prefix, StringComparison.Ordinal) && l.Contains(marker, StringComparison.Ordinal));
        Assert.True(i > 0, marker);
        lines.RemoveAt(i);
        var head = System.Text.RegularExpressions.Regex.Match(lines[0], "\"count\":([0-9]+)");
        lines[0] = lines[0].Replace(head.Value, $"\"count\":{long.Parse(head.Groups[1].Value) - 1}");
        return string.Join("\n", lines);
    }

    internal static void AssertRefused(CombatSetup setup, string text, IEnumerable<(string Prefix, string From, string To, string Message)> cases)
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
        // Un autre état possible (l'ogre a pris un point de dégâts) : accepté.
        string ogre = Tampered(Tampered(text, "{\"fighter\":\"ogre\"", "\"health\":500", "\"health\":499"), "{\"fighter\":\"ogre\"", "\"damageTaken\":0", "\"damageTaken\":1");
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
            (Ogre, "\"engaged\":false", "\"engaged\":true", "Engagement de « ogre »"),   // sous-lot 11.5 : sans comportement
            // Présence.
            (Bastion, "\"leaveCommand\":null", "\"leaveCommand\":0", "Départ de « bastion » encore demandé"),
            (Ogre, "\"presence\":0", "\"presence\":1", "de « ogre »"),   // refusé par ses tours (9e tour) ou par son arrivée absente
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
            // Revue Codex de la PR #32 (4e tour) : impulsion à son tick.
            (Impact, "\"tick\":40", "\"tick\":41", "hors de son tick"),
            // 15e tour : un mannequin qui marche ; un numéro de ligne en deçà du dernier événement.
            (Cible, "\"moving\":false,\"directionX\":0,\"directionZ\":0", "\"moving\":true,\"directionX\":10000,\"directionZ\":0", "qui ne marche jamais"),
            // Relecture d'ensemble (4) : marche de l'ogre sans commande move ; cible sélectionnée par le Bastion sans commande select.
            (Ogre, "\"moving\":false,\"directionX\":0,\"directionZ\":0", "\"moving\":true,\"directionX\":10000,\"directionZ\":0", "Marche de « ogre »"),
            (Bastion, "\"selected\":null", "\"selected\":\"ogre\"", "Sélection de « bastion »"),
        });
        // 15e tour : numéro de ligne en deçà du dernier événement (chaque événement a pris une ligne).
        string seq = System.Text.RegularExpressions.Regex.Match(text.Split('\n')[0], "\"seq\":[0-9]+").Value;
        AssertRefused(setup, text, new[] { (Head, seq, "\"seq\":1", "en deçà du dernier événement") });
        // Revue Codex de la PR #32 (6e tour) : un compteur sans marge pour la suite du combat est refusé (sinon le prochain numéro
        // déborderait).
        foreach (string counter in new[] { "seq", "lastEid", "lastStatusInstance", "lastApplication" })
        {
            string huge = new System.Text.RegularExpressions.Regex("\"" + counter + "\":[0-9]+").Replace(text, "\"" + counter + "\":" + (long.MaxValue - 10), 1);   // en-tête : première ligne
            Assert.NotEqual(text, huge);
            Assert.Contains("trop grands", Assert.Throws<FormatException>(() => CombatEngine.Restore(setup, CombatSnapshot.Parse(huge), NullCombatLog.Instance)).Message);
        }

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
            (Bastion, "\"keySource\":\"assaillant\"", "\"keySource\":null", "tick de DOT impossible"),   // 9e tour : son tick de DOT cite l'ancienne clé
            (Bastion, "\"untilTick\":140", "\"untilTick\":20", "déjà échue"),
            (Bastion, "\"untilTick\":140", "\"untilTick\":100000", "au-delà de sa plus longue durée possible"),   // 10e tour : saignement (DOT)
            (Bastion, "\"absorb\":0,", "\"absorb\":5,", "absorption impossible"),
            ("{\"entry\":3,", "\"index\":2", "\"index\":3", "hors de son tick ou en double"),
            ("{\"entry\":0,", "\"fighter\":\"renfort\"", "\"fighter\":\"allie\"", "n'est pas attendu"),
            (Renfort, "\"presence\":1", "\"presence\":0", "n'est pas attendu"),
            (Allie, "\"arrivedTick\":null", "\"arrivedTick\":10", "pas celle de son combat"),   // 14e tour : présent dès l'origine, sans tick d'arrivée
            (Head, "[\"bastion\",50,1]", "[\"bastion\",50,2]", "par acteur et par tick"),
            (Bastion, "\"nextTick\":60", "\"nextTick\":61", "échéance absente de la file"),
            (Bastion, "\"nextTick\":60", "\"nextTick\":80", "au-delà de sa plus longue durée possible"),   // 10e tour : plus d'une période après le tick 29
            ("{\"entry\":9,", "\"instance\":1}", "\"instance\":0}", "Altération non numérotée"),
            // 4e tour : arrivée au tick prévu, échéance d'altération une seule fois.
            ("{\"entry\":0,", "\"tick\":80", "\"tick\":81", "hors du tick prévu"),
            // 5e tour : tours déduits de la cadence, flux de tirage du combattant, combattant attendu dans son état de départ.
            (Assaillant, "\"nextAttack\":1", "\"nextAttack\":0", "Tours de « assaillant » incohérents avec sa cadence"),
            (Assaillant, "\"nextTurn\":2", "\"nextTurn\":3", "Tours de « assaillant » incohérents"),
            (Assaillant, "\"151c15caa15e12f3\"", "\"151c15caa15e12f5\"", "Flux de tirage de « assaillant »"),
            (Renfort, "\"resonance\":0", "\"resonance\":1", "« renfort », pas encore entré"),
            (Bastion, "\"globalCooldownReadyTick\":20", "\"globalCooldownReadyTick\":1000", "Recharge globale ou parade de « bastion » au-delà"),
        });
        // Revue Codex de la PR #32 (8e tour) : un renfort attendu, déclaré refusé avant son tick d'arrivée (et son arrivée retirée de la file).
        string refused = WithoutEntry(Tampered(text, Renfort, "\"presence\":1", "\"presence\":3"), "{\"entry\":", "\"item\":\"arrival\"");
        Assert.Contains("incohérente avec son tick d'arrivée", Assert.Throws<FormatException>(() => CombatEngine.Restore(setup, CombatSnapshot.Parse(refused),
            NullCombatLog.Instance)).Message);
        foreach (string entry in new[] { "{\"entry\":9,", "{\"entry\":11," })
            Assert.Contains("en double", Assert.Throws<FormatException>(() => CombatEngine.Restore(setup, CombatSnapshot.Parse(WithDuplicate(text, entry)),
                NullCombatLog.Instance)).Message);
        // Parade ouverte (tick 79, fenêtre jusqu'au tick 81) : sa fin en double est refusée.
        while (training.CurrentTick < 79) training.Step();
        string parry = training.Snapshot().Text;
        Assert.Contains("\"parryOpen\":true", parry);
        string end = parry.Split('\n').First(l => l.Contains("\"item\":\"parryEnd\"", StringComparison.Ordinal));
        Assert.Contains("en double", Assert.Throws<FormatException>(() => CombatEngine.Restore(training.CompletedSetup(),
            CombatSnapshot.Parse(WithDuplicate(parry, end.Substring(0, end.IndexOf(',') + 1))), NullCombatLog.Instance)).Message);

        // Un renfort positionné attendu garde la place de son scénario (5e tour).
        CombatSetup awaited = Space.SpaceTestKit.Spatial(Space.SpaceTestKit.Arena(10), new CombatantSetup[]
        {
            Space.SpaceTestKit.Player("bastion", Space.SpaceTestKit.At("0", "0")),
            Space.SpaceTestKit.Creature("ogre", Space.SpaceTestKit.At("3", "0"), health: 500),
            Space.SpaceTestKit.Creature("renfort", Space.SpaceTestKit.At("6", "6"), health: 500).Arriving(2000),
        }, Array.Empty<CombatCommand>(), durationMs: 6000);
        var waiting = new CombatEngine(awaited, NullCombatLog.Instance);
        while (waiting.CurrentTick < 5) waiting.Step();
        AssertRefused(waiting.CompletedSetup(), waiting.Snapshot().Text, new[]
        {
            ("{\"fighter\":\"renfort\"", "\"x\":60000", "\"x\":50000", "« renfort », pas encore entré"),
        });

        // Attaque dirigée annoncée : son impact garde sa cible.
        CombatSetup aimed = Space.SpaceTestKit.Spatial(Space.SpaceTestKit.Arena(10), new CombatantSetup[]
        {
            Space.SpaceTestKit.Player("bastion", Space.SpaceTestKit.At("0", "0")),
            Space.SpaceTestKit.Creature("ogre", Space.SpaceTestKit.At("3", "0"), health: 500, firstAttackMs: 500,
                attacks: new AssailantAttack("jet", 10, shape: AttackShape.Target, telegraphMs: 1500, range: Fixed.Parse("8"))),
        }, Array.Empty<CombatCommand>(), durationMs: 6000);
        var thrower = new CombatEngine(aimed, NullCombatLog.Instance);
        while (thrower.CurrentTick < 15) thrower.Step();
        string thrown = thrower.Snapshot().Text;
        string impactLine = thrown.Split('\n').First(l => l.Contains("\"item\":\"impact\"", StringComparison.Ordinal));
        AssertRefused(thrower.CompletedSetup(), thrown, new[]
        {
            (impactLine.Substring(0, impactLine.IndexOf(',') + 1), "\"target\":\"bastion\"", "\"target\":null", "impact dirigé sans cible"),
            // Revue Codex de la PR #32 (9e tour) : la cible verrouillée est un adversaire de l'assaillant.
            (impactLine.Substring(0, impactLine.IndexOf(',') + 1), "\"target\":\"bastion\"", "\"target\":\"ogre\"", "impact dirigé sans cible adverse"),
        });
        // 9e tour : deux emprises solides présentes ne se chevauchent pas (le Bastion posé sur l'ogre).
        string[] thrownLines = thrown.Split('\n');
        var place = new System.Text.RegularExpressions.Regex("\"x\":-?[0-9]+,\"z\":-?[0-9]+");
        string ogrePlace = place.Match(thrownLines.First(l => l.StartsWith("{\"fighter\":\"ogre\"", StringComparison.Ordinal))).Value;
        int bastionLine = Array.FindIndex(thrownLines, l => l.StartsWith("{\"fighter\":\"bastion\"", StringComparison.Ordinal));
        thrownLines[bastionLine] = place.Replace(thrownLines[bastionLine], ogrePlace, 1);
        Assert.Contains("qui se chevauchent", Assert.Throws<FormatException>(() => CombatEngine.Restore(thrower.CompletedSetup(),
            CombatSnapshot.Parse(string.Join("\n", thrownLines)), NullCombatLog.Instance)).Message);

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
        // Réaction reportée : l'effet observé ne cite que des combattants du combat.
        while (!reacting.Snapshot().Text.Contains("\"item\":\"reaction\"", StringComparison.Ordinal)) reacting.Step();
        string deferred = reacting.Snapshot().Text;
        string reaction = deferred.Split('\n').First(l => l.Contains("\"item\":\"reaction\"", StringComparison.Ordinal));
        AssertRefused(reacting.CompletedSetup(), deferred, new[] { (reaction.Substring(0, reaction.IndexOf(',') + 1), "\"source\":\"assaillant\"", "\"source\":\"inconnu\"",
            "combattant inconnu") });
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
    public void Compteurs_recharges_et_puissances_impossibles_refuses()
    {
        // Revue Codex de la PR #32 (7e tour) : prochain numéro de la file, recharges des déclencheurs portés par une altération, bilan,
        // entretien de la garde, régénération et puissance d'un DOT ont maintenant des bornes.
        var epine = new StatusDefinition("epine", StatusCategory.Modifier, 2050, StatusStacking.Refresh, triggers: new[] { Riposte(TriggerEvent.DotTick) });
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, Statuses = new StatusCatalog(StatusCatalog.Provisional.Definitions.Append(epine)) };
        CombatSetup setup = Setup(Array.Empty<CombatCommand>(), new CombatantSetup[]
        {
            Bearer(), Hitter(Strike("piqure", 100, applies: new[] { Apply("bleed", "0.4"), Apply("epine") })),
        }, durationMs: 4000, rules: rules);
        var engine = new CombatEngine(setup, NullCombatLog.Instance);
        while (!engine.Snapshot().Text.Contains("\"slots\":[", StringComparison.Ordinal) || engine.CurrentTick < 30) engine.Step();
        string text = engine.Snapshot().Text;
        CombatSetup completed = engine.CompletedSetup();
        Assert.Equal(text, CombatEngine.Restore(completed, CombatSnapshot.Parse(text), NullCombatLog.Instance).Snapshot().Text);
        foreach ((string pattern, string value, string message) in new[]
        {
            ("\"next\":[0-9]+", "\"next\":" + (long.MaxValue - 10), "trop grands"),
            ("\"slots\":\\[[0-9]+", "\"slots\":[-1", "Recharge d'un déclencheur"),
            ("\"slots\":\\[[0-9]+", "\"slots\":[" + (long.MaxValue - 10), "Recharge d'un déclencheur"),
            ("\"damageTaken\":[0-9]+", "\"damageTaken\":-1", "de « bastion »"),   // bilan, ou dégâts subis loin des PV perdus
            ("\"damageTaken\":[0-9]+", "\"damageTaken\":" + (long.MaxValue - 10), "de « bastion »"),
            ("\"guardTicks\":[0-9]+", "\"guardTicks\":100000", "Bilan de"),
            ("\"upkeepRemainder\":[0-9]+", "\"upkeepRemainder\":10000000", "Entretien de la garde"),
            ("\"upkeepPaid\":[0-9]+", "\"upkeepPaid\":5", "Entretien de la garde"),
            ("\"regenGranted\":[0-9]+", "\"regenGranted\":-1", "Échéances de"),
            ("\"regenFromTick\":(null|[0-9]+)", "\"regenFromTick\":100000", "Régénération"),
            ("\"power\":[0-9]+", "\"power\":-1", "puissance impossible"),
            // Relecture d'ensemble (20e tour) : puissance du saignement au-delà de sa plus forte application (puissance × 0,4).
            ("(\"state\":\"bleed\"[^{}]*?\"power\":)[0-9]+", "${1}1000000000000", "puissance au-delà du plus fort DOT possible"),
        })
        {
            string tampered = new System.Text.RegularExpressions.Regex(pattern).Replace(text, value, 1);
            Assert.NotEqual(text, tampered);
            Exception? refused = Record.Exception(() => CombatEngine.Restore(completed, CombatSnapshot.Parse(tampered), NullCombatLog.Instance));
            Assert.True(refused is FormatException && refused.Message.Contains(message, StringComparison.Ordinal), $"{value} : {refused?.Message ?? "accepté"}");
        }
    }

    [Fact]
    public void Echeances_reactions_et_provenances_impossibles_refusees()
    {
        // Revue Codex de la PR #32 (10e tour). 1) Étourdissement de l'assaillant (Assommer au tick 52) : son échéance et son entrée de file
        // repoussées ensemble restent cohérentes entre elles, mais au-delà de la plus longue durée qu'il a pu recevoir : refusées.
        var training = new CombatEngine(Training(), NullCombatLog.Instance);
        while (training.CurrentTick < 55) training.Step();
        string text = training.Snapshot().Text;
        CombatSetup setup = training.CompletedSetup();
        Assert.Equal(text, CombatEngine.Restore(setup, CombatSnapshot.Parse(text), NullCombatLog.Instance).Snapshot().Text);
        string[] lines = text.Split('\n');
        int expiry = Array.FindIndex(lines, l => l.StartsWith("{\"entry\":", StringComparison.Ordinal) && l.Contains("\"item\":\"expiry\"", StringComparison.Ordinal)
            && l.Contains("\"fighter\":\"assaillant\"", StringComparison.Ordinal));
        Assert.True(expiry > 0, "étourdissement en file");
        string at = System.Text.RegularExpressions.Regex.Match(lines[expiry], "\"tick\":[0-9]+").Value;
        long until = long.Parse(at.Substring("\"tick\":".Length), System.Globalization.CultureInfo.InvariantCulture);
        int assailant = Array.FindIndex(lines, l => l.StartsWith("{\"fighter\":\"assaillant\"", StringComparison.Ordinal));
        Assert.Contains($"\"untilTick\":{until}", lines[assailant], StringComparison.Ordinal);
        lines[expiry] = lines[expiry].Replace(at, $"\"tick\":{until + 1000}", StringComparison.Ordinal);
        lines[assailant] = lines[assailant].Replace($"\"untilTick\":{until}", $"\"untilTick\":{until + 1000}", StringComparison.Ordinal);
        Assert.Contains("au-delà de sa plus longue durée possible", Assert.Throws<FormatException>(() => CombatEngine.Restore(setup,
            CombatSnapshot.Parse(string.Join("\n", lines)), NullCombatLog.Instance)).Message);

        // 2) Réaction reportée (riposte aux ticks de DOT) : échangée contre l'autre déclencheur du porteur, qui ne frappe pas ; recharge
        // interne du déclencheur retouchée ; effet observé renuméroté (la provenance de la réaction ne le cite plus).
        var gain = new TriggerDefinition(TriggerEvent.DotTick, new[] { TriggerEffect.GainResource(TriggerResource.Resonance, Fixed.FromInt(1)) }, TriggerRole.Target);
        CombatSetup riposte = Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { Bearer(new[] { Riposte(TriggerEvent.DotTick), gain }),
            Hitter(Strike("entaille", 100, applies: Apply("bleed", "0.4"))) }, durationMs: 8000, rules: NoUpkeep);
        var reacting = new CombatEngine(riposte, NullCombatLog.Instance);
        while (!reacting.Snapshot().Text.Contains("\"item\":\"reaction\"", StringComparison.Ordinal)) reacting.Step();
        string deferred = reacting.Snapshot().Text;
        CombatSetup completed = reacting.CompletedSetup();
        Assert.Equal(deferred, CombatEngine.Restore(completed, CombatSnapshot.Parse(deferred), NullCombatLog.Instance).Snapshot().Text);
        string reaction = deferred.Split('\n').First(l => l.Contains("\"item\":\"reaction\"", StringComparison.Ordinal));
        string entry = reaction.Substring(0, reaction.IndexOf(',') + 1);
        long eid = long.Parse(System.Text.RegularExpressions.Regex.Match(reaction, "\"eid\":([0-9]+)").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        string ready = System.Text.RegularExpressions.Regex.Match(deferred.Split('\n').First(l => l.StartsWith("{\"fighter\":\"bastion\"", StringComparison.Ordinal)),
            "\"triggers\":\\[[0-9]+").Value;
        Assert.NotEqual("\"triggers\":[0", ready);
        AssertRefused(completed, deferred, new[]
        {
            (entry, "\"index\":0", "\"index\":1", "réaction reportée incohérente"),
            (entry, $"\"eid\":{eid}", $"\"eid\":{eid - 1}", "provenance de réaction incohérente"),
            ("{\"fighter\":\"bastion\"", ready, "\"triggers\":[0", "réaction reportée incohérente"),
        });

        // 3) Provenances que les constructeurs ne produisent pas : profondeur sans chaîne, dernier déclencheur hors de la chaîne, maillon
        // en double, réaction sans cause, profondeur au-delà des règles.
        foreach ((string pattern, string value, string message) in new[]
        {
            ("\"depth\":0,\"chain\":\\[\\]", "\"depth\":3,\"chain\":[]", "pour une chaîne de 0"),
            ("\"chain\":\\[\\],\"trigger\":null", "\"chain\":[],\"trigger\":\"x/0@bastion#0\"", "bout de sa chaîne"),
        })
        {
            string tampered = new System.Text.RegularExpressions.Regex(pattern).Replace(deferred, value, 1);
            Assert.NotEqual(deferred, tampered);
            Assert.Contains(message, Assert.Throws<FormatException>(() => CombatEngine.Restore(completed, CombatSnapshot.Parse(tampered), NullCombatLog.Instance)).Message);
        }
        Assert.Contains("en double", Assert.Throws<FormatException>(() => Provenance.Restore("r", 2, new[] { "a", "a" }, "a", 5, null, null, null)).Message);
        Assert.Contains("sans cause", Assert.Throws<FormatException>(() => Provenance.Restore("r", 1, new[] { "a" }, "a", null, null, null, null)).Message);
        string deep = new System.Text.RegularExpressions.Regex("\"depth\":1,\"chain\":\\[(\"[^\"]+\")\\],\"trigger\":(\"[^\"]+\")")
            .Replace(deferred, "\"depth\":5,\"chain\":[\"a\",\"b\",\"c\",\"d\",$1],\"trigger\":$2", 1);
        Assert.NotEqual(deferred, deep);
        Assert.Contains("profondeur maximale", Assert.Throws<FormatException>(() => CombatEngine.Restore(completed, CombatSnapshot.Parse(deep), NullCombatLog.Instance)).Message);
    }

    [Fact]
    public void Generations_rangs_file_et_cause_mortelle_impossibles_refuses()
    {
        // Revue Codex de la PR #32 (11e à 19e tours). Combat d'entraînement au tick 30 : un saignement sur le Bastion et un sur l'assaillant.
        var training = new CombatEngine(Training(), NullCombatLog.Instance);
        while (training.CurrentTick < 30) training.Step();
        string text = training.Snapshot().Text;
        CombatSetup setup = training.CompletedSetup();
        string[] lines = text.Split('\n');
        string bastion = lines.First(l => l.StartsWith("{\"fighter\":\"bastion\"", StringComparison.Ordinal));
        string assailant = lines.First(l => l.StartsWith("{\"fighter\":\"assaillant\"", StringComparison.Ordinal));
        string generation = System.Text.RegularExpressions.Regex.Match(bastion, "\"id\":2,\"generation\":[0-9]+").Value;
        string mine = System.Text.RegularExpressions.Regex.Match(bastion, "\"appliedSeq\":[0-9]+").Value;
        string other = System.Text.RegularExpressions.Regex.Match(assailant, "\"appliedSeq\":[0-9]+").Value;
        Assert.NotEqual(mine, other);
        const string Bastion = "{\"fighter\":\"bastion\"";
        AssertRefused(setup, text, new[]
        {
            // Génération d'une altération active : au moins 1, au plus le dernier rang d'application.
            (Bastion, generation, "\"id\":2,\"generation\":0", "instance impossible ou en double"),
            (Bastion, generation, "\"id\":2,\"generation\":2147483647", "instance impossible ou en double"),
            // Deux altérations au même rang d'application.
            (Bastion, mine, other, "rang d'application impossible ou en double"),
            // 13e tour : recharge globale sans lancer à son origine (le Bastion a lancé Entaille au tick 0 : prête au tick 20).
            (Bastion, "\"globalCooldownReadyTick\":20", "\"globalCooldownReadyTick\":0", "Recharge globale de « bastion » sans lancer"),
            (Bastion, "\"globalCooldownReadyTick\":20", "\"globalCooldownReadyTick\":25", "Recharge globale de « bastion » sans lancer"),
            // Cause mortelle d'un vivant.
            (Bastion, "\"lethalCause\":null", "\"lethalCause\":{\"root\":\"x\",\"depth\":0,\"chain\":[],\"trigger\":null,\"cause\":null,\"source\":null,\"instance\":null,\"generation\":null}",
                "Cause mortelle de « bastion » vivant"),
        });
        // 12e tour : entretien dû sans l'entretien payé (le Bastion garde au tick 30), refusé.
        string due = System.Text.RegularExpressions.Regex.Match(bastion, "\"upkeepDue\":([0-9]+)").Groups[1].Value;
        AssertRefused(setup, text, new[] { (Bastion, $"\"upkeepDue\":{due},", $"\"upkeepDue\":{long.Parse(due, System.Globalization.CultureInfo.InvariantCulture) + 1},",
            "Entretien de la garde de « bastion »") });
        // 12e tour : régénération en cours (Heurt au tick 0, 15 d'endurance, puis rien) : le cumul accordé est celui du tick précédent, pas moins.
        var resting = new CombatEngine(Setup(new[] { Cmd(0, Heurt, target: "assaillant") }, new CombatantSetup[]
        {
            Tank(actives: new[] { Heurt }), Assailant(firstAttackMs: 60_000, intervalMs: 60_000),
        }, durationMs: 9000), NullCombatLog.Instance);
        var regen = new System.Text.RegularExpressions.Regex("\"regenFromTick\":([0-9]+),\"regenGranted\":([1-9][0-9]*)");
        System.Text.RegularExpressions.Match granted = System.Text.RegularExpressions.Match.Empty;
        while (!resting.IsFinished)
        {
            granted = regen.Match(resting.Snapshot().Text.Split('\n').First(l => l.StartsWith(Bastion, StringComparison.Ordinal)));
            if (granted.Success && long.Parse(granted.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) < resting.CurrentTick - 1) break;
            resting.Step();
        }
        Assert.False(resting.IsFinished, "régénération en cours");
        AssertRefused(resting.CompletedSetup(), resting.Snapshot().Text, new[] { (Bastion, granted.Value,
            granted.Value.Replace("\"regenGranted\":" + granted.Groups[2].Value, "\"regenGranted\":0", StringComparison.Ordinal), "Régénération de « bastion » impossible") });
        // 15e tour : parade réussie ou récupération de parade sans parade au bilan ; plus d'instances d'altération que de rangs d'application.
        long applications = long.Parse(System.Text.RegularExpressions.Regex.Match(lines[0], "\"lastApplication\":([0-9]+)").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        string instancesField = System.Text.RegularExpressions.Regex.Match(lines[0], "\"lastStatusInstance\":[0-9]+").Value;
        AssertRefused(setup, text, new[]
        {
            (Bastion, "\"parrySucceeded\":false", "\"parrySucceeded\":true", "Parade de « bastion » incohérente"),
            (Bastion, "\"parryRecoveryUntilTick\":0", "\"parryRecoveryUntilTick\":5", "Parade de « bastion » incohérente"),
            ("{\"snapshot\"", instancesField, $"\"lastStatusInstance\":{applications + 1}", "rangs d'application"),
        });
        // 14e tour : mesures de charge au-delà des budgets ; dernier tick au-delà des pics.
        foreach ((string pattern, string value) in new[] { ("\"peakWork\":[0-9]+", "\"peakWork\":9223372036854775000"), ("\"lastTickEvents\":[0-9]+", "\"lastTickEvents\":1000000000") })
        {
            string tampered = new System.Text.RegularExpressions.Regex(pattern).Replace(text, value, 1);
            Assert.NotEqual(text, tampered);
            Assert.Contains("Mesures de charge", Assert.Throws<FormatException>(() => CombatEngine.Restore(setup, CombatSnapshot.Parse(tampered), NullCombatLog.Instance)).Message);
        }
        // 14e tour : le renfort, arrivé au tick 80 (4 s), ne peut pas être déclaré arrivé à un autre tick.
        var late = new CombatEngine(Training(), NullCombatLog.Instance);
        while (late.CurrentTick < 90) late.Step();
        string arrived = late.Snapshot().Text;
        Assert.Contains("\"arrivedTick\":80", arrived.Split('\n').First(l => l.StartsWith("{\"fighter\":\"renfort\"", StringComparison.Ordinal)), StringComparison.Ordinal);
        AssertRefused(late.CompletedSetup(), arrived, new[]
        {
            ("{\"fighter\":\"renfort\"", "\"arrivedTick\":80", "\"arrivedTick\":79", "pas celle de son combat"),
            ("{\"fighter\":\"renfort\"", "\"arrivedTick\":80", "\"arrivedTick\":null", "pas celle de son combat"),
        });
        // 16e tour : hors d'un combat positionné, une arrivée n'est jamais refusée (le renfort, arrivé au tick 80, déclaré refusé dans son
        // état de départ) ; un départ suit une commande leave à son tick (l'allié part au tick 140 : déclaré parti au tick 130).
        string start = new CombatEngine(Training(), NullCombatLog.Instance).Snapshot().Text.Split('\n').First(l => l.StartsWith("{\"fighter\":\"renfort\"", StringComparison.Ordinal));
        string[] refusedLines = arrived.Split('\n');
        int renfort = Array.FindIndex(refusedLines, l => l.StartsWith("{\"fighter\":\"renfort\"", StringComparison.Ordinal));
        refusedLines[renfort] = start.Replace("\"presence\":1", "\"presence\":3", StringComparison.Ordinal);
        Assert.Contains("refusée hors d'un combat positionné", Assert.Throws<FormatException>(() => CombatEngine.Restore(late.CompletedSetup(),
            CombatSnapshot.Parse(string.Join("\n", refusedLines)), NullCombatLog.Instance)).Message);
        while (late.CurrentTick < 150) late.Step();
        string gone = late.Snapshot().Text;
        string allie = gone.Split('\n').First(l => l.StartsWith("{\"fighter\":\"allie\"", StringComparison.Ordinal));
        Assert.Contains("\"leftTick\":140", allie, StringComparison.Ordinal);
        // 16e tour : un parti n'a ni compétence, ni garde, ni parade disponibles (la vue suit la présence, comme pour la marche).
        CombatantView leftView = late.View("allie");
        Assert.False(leftView.IsPresent || leftView.CanCast || leftView.CanGuard || leftView.CanParry);
        string earlier = gone.Replace(allie, allie.Replace("\"leftTick\":140", "\"leftTick\":130", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("sans commande de départ", Assert.Throws<FormatException>(() => CombatEngine.Restore(late.CompletedSetup(),
            CombatSnapshot.Parse(earlier), NullCombatLog.Instance)).Message);
        // 17e tour : avant le premier tick, l'état du tirage d'un combattant présent est celui de départ ; un départ suit l'arrivée (le renfort
        // arrivé au tick 80, déclaré parti au tick 0 sur une commande leave refusée alors) ; les lancers comptés ne dépassent pas les
        // commandes de compétence passées.
        var fresh = new CombatEngine(Training(), NullCombatLog.Instance);
        string zero = fresh.Snapshot().Text;
        string draw = System.Text.RegularExpressions.Regex.Match(zero.Split('\n').First(l => l.StartsWith(Bastion, StringComparison.Ordinal)), "\"random\":\\[\"[0-9a-f]").Value;
        string redraw = draw.Substring(0, draw.Length - 1) + (draw[draw.Length - 1] == '0' ? '1' : '0');
        AssertRefused(fresh.CompletedSetup(), zero, new[] { (Bastion, draw, redraw, "avant le premier tick") });
        AssertRefused(setup, text, new[] { (Bastion, "\"skillsCast\":1,", "\"skillsCast\":2,", "Lancers ou refus de « bastion »") });
        CombatSetup leaving = Setup(new[] { Cmd(0, Entaille, target: "assaillant"), Leave(0, "renfort") }, new CombatantSetup[]
        {
            Tank(endurance: 30, actives: new[] { Frappe, Entaille, Assommer, Onde }), Tank("allie"), Tank("renfort").Arriving(4000),
            Assailant(firstAttackMs: 1000, intervalMs: 1300),
        }, durationMs: 9000);
        var stay = new CombatEngine(leaving, NullCombatLog.Instance);
        while (stay.CurrentTick < 90) stay.Step();
        string stayed = stay.Snapshot().Text;
        string renfortLine = stayed.Split('\n').First(l => l.StartsWith("{\"fighter\":\"renfort\"", StringComparison.Ordinal));
        Assert.Contains("\"presence\":0", renfortLine, StringComparison.Ordinal);
        string departed = stayed.Replace(renfortLine, renfortLine.Replace("\"presence\":0", "\"presence\":2", StringComparison.Ordinal)
            .Replace("\"leftTick\":null", "\"leftTick\":0", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("avant son arrivée", Assert.Throws<FormatException>(() => CombatEngine.Restore(stay.CompletedSetup(), CombatSnapshot.Parse(departed),
            NullCombatLog.Instance)).Message);
        // Relecture d'ensemble : sans soin, un vivant a perdu exactement ses dégâts subis ; l'endurance n'est entamée qu'avec une reprise de
        // régénération en attente ; une reprise à venir (le Bastion garde : l'entretien la repousse) n'a encore rien accordé.
        string health = System.Text.RegularExpressions.Regex.Match(bastion, "\"health\":[0-9]+").Value;
        long healthValue = long.Parse(health.Substring("\"health\":".Length), System.Globalization.CultureInfo.InvariantCulture);
        Assert.Matches("\"regenFromTick\":(3[0-9]|[4-9][0-9])", bastion);
        AssertRefused(setup, text, new[]
        {
            (Bastion, health, $"\"health\":{healthValue - 1}", "Dégâts subis de « bastion » incohérents"),
            ("{\"fighter\":\"allie\"", "\"endurance\":100", "\"endurance\":99", "entamée sans reprise"),
            (Bastion, "\"regenGranted\":0", "\"regenGranted\":1", "accordée avant sa reprise"),
        });
        // Relecture d'ensemble : fenêtre de parade sans parade, parade comptée sans commande de parade passée ; provocation venue d'un allié.
        AssertRefused(setup, text, new[]
        {
            (Bastion, "\"parryEndTick\":0", "\"parryEndTick\":5", "fenêtre"),
            (Bastion, "\"perfectParries\":0", "\"perfectParries\":1", "Parade de « bastion » incohérente avec ses commandes"),
        });
        var taunting = new CombatEngine(Space.SpaceTestKit.Spatial(Space.SpaceTestKit.Arena(30), new CombatantSetup[]
        {
            Space.Lot114bRangeTests.Hero("bastion", Space.SpaceTestKit.At("0", "0"), new[] { Space.Lot114bRangeTests.Cri }),
            Space.SpaceTestKit.Creature("ogre", Space.SpaceTestKit.At("3", "0"), health: 500, firstAttackMs: 600_000),
            Space.SpaceTestKit.Creature("loup", Space.SpaceTestKit.At("6", "0"), health: 500, firstAttackMs: 600_000),
        }, new[] { Cmd(0, Space.Lot114bRangeTests.Cri, target: null) }, durationMs: 2000), NullCombatLog.Instance);
        taunting.Step();
        string taunted = taunting.Snapshot().Text;
        Assert.Equal(taunted, CombatEngine.Restore(taunting.CompletedSetup(), CombatSnapshot.Parse(taunted), NullCombatLog.Instance).Snapshot().Text);
        // La provocation de l'ogre (et son échéance en file) attribuée au loup, son allié.
        string[] tauntLines = taunted.Split('\n');
        for (int k = 0; k < tauntLines.Length; k++)
            if (tauntLines[k].StartsWith("{\"fighter\":\"ogre\"", StringComparison.Ordinal))
                tauntLines[k] = tauntLines[k].Replace("\"keySource\":\"bastion\"", "\"keySource\":\"loup\"", StringComparison.Ordinal)
                    .Replace("\"source\":\"bastion\"", "\"source\":\"loup\"", StringComparison.Ordinal);   // l'altération et son origine (19e tour)
            else if (tauntLines[k].StartsWith("{\"entry\":", StringComparison.Ordinal) && tauntLines[k].Contains("\"fighter\":\"ogre\"", StringComparison.Ordinal)
                && tauntLines[k].Contains("Taunt\"", StringComparison.Ordinal))
                tauntLines[k] = tauntLines[k].Replace("\"keySource\":\"bastion\"", "\"keySource\":\"loup\"", StringComparison.Ordinal);
        string fromAlly = string.Join("\n", tauntLines);
        Assert.NotEqual(taunted, fromAlly);
        Assert.Contains("source qui ne convient pas", Assert.Throws<FormatException>(() => CombatEngine.Restore(taunting.CompletedSetup(),
            CombatSnapshot.Parse(fromAlly), NullCombatLog.Instance)).Message);
        // Relecture d'ensemble (3) : garde ou ticks gardés de l'allié sans commande de garde ; parade du Bastion sans commande à son tick ;
        // tours d'un personnage.
        AssertRefused(setup, text, new[]
        {
            ("{\"fighter\":\"allie\"", "\"guarding\":false", "\"guarding\":true", "Garde ou parade de « allie » sans commande"),
            ("{\"fighter\":\"allie\"", "\"guardTicks\":0", "\"guardTicks\":3", "Garde ou parade de « allie » sans commande"),
            (Bastion, "\"parryStartTick\":0,\"parryEndTick\":0", "\"parryStartTick\":5,\"parryEndTick\":" + (5 + training.View("bastion").Rules!.ParryWindowMs / 50), "Garde ou parade de « bastion » sans commande"),
            ("{\"fighter\":\"allie\"", "\"nextTurn\":0", "\"nextTurn\":1", "Tours de « allie »"),
        });
        // 19e tour : l'origine d'une altération porte sa source, son instance et sa génération ; les dégâts infligés ont leur contrepartie
        // reçue (subis, absorbés ou transférés) quelque part.
        // Le saignement du Bastion (et son tick de DOT en file) attribué à l'allié, l'origine gardant l'assaillant.
        string[] bleedLines = text.Split('\n');
        for (int k = 0; k < bleedLines.Length; k++)
            if (bleedLines[k].StartsWith(Bastion, StringComparison.Ordinal))
                bleedLines[k] = bleedLines[k].Replace("\"keySource\":\"assaillant\",\"source\":\"assaillant\"", "\"keySource\":\"allie\",\"source\":\"allie\"", StringComparison.Ordinal);
            else if (bleedLines[k].StartsWith("{\"entry\":", StringComparison.Ordinal) && bleedLines[k].Contains("\"item\":\"dot\",\"fighter\":\"bastion\"", StringComparison.Ordinal))
                bleedLines[k] = bleedLines[k].Replace("\"keySource\":\"assaillant\"", "\"keySource\":\"allie\"", StringComparison.Ordinal);
        string reattributed = string.Join("\n", bleedLines);
        Assert.NotEqual(text, reattributed);
        Assert.Contains("provenance différente de l'altération", Assert.Throws<FormatException>(() => CombatEngine.Restore(setup, CombatSnapshot.Parse(reattributed),
            NullCombatLog.Instance)).Message);
        string dealt = System.Text.RegularExpressions.Regex.Match(bastion, "\"damageDealt\":[0-9]+").Value;
        long dealtValue = long.Parse(dealt.Substring("\"damageDealt\":".Length), System.Globalization.CultureInfo.InvariantCulture);
        AssertRefused(setup, text, new[] { (Bastion, dealt, $"\"damageDealt\":{dealtValue + 1_000_000}", "Dégâts du bilan incohérents") });
        // 18e tour : un coup critique est un coup, un ultime une compétence lancée ; un mort dont le départ a été annulé n'est pas parti.
        string hits = System.Text.RegularExpressions.Regex.Match(bastion, "\"hits\":[0-9]+").Value;
        long hitCount = long.Parse(hits.Substring("\"hits\":".Length), System.Globalization.CultureInfo.InvariantCulture);
        AssertRefused(setup, text, new[]
        {
            (Bastion, "\"criticalHits\":", $"\"criticalHits\":{hitCount + 1}0", "Bilan de « bastion » impossible"),
            (Bastion, "\"ultimatesCast\":0", "\"ultimatesCast\":2", "Bilan de « bastion » impossible"),
        });
        var dying = new CombatEngine(Setup(new[] { Leave(1000, "bastion") }, new CombatantSetup[]
        {
            Character(attributes: Attrs(vitalite: 1)), Tank("allie"), Assailant(firstAttackMs: 1000, intervalMs: 10_000, attacks: Blow(raw: 100_000_000)),
        }, durationMs: 3000), NullCombatLog.Instance);
        while (dying.CurrentTick < 25) dying.Step();
        string dead = dying.Snapshot().Text;
        string deadLine = dead.Split('\n').First(l => l.StartsWith(Bastion, StringComparison.Ordinal));
        Assert.Contains("\"dead\":true", deadLine, StringComparison.Ordinal);
        string leftDead = dead.Replace(deadLine, deadLine.Replace("\"presence\":0", "\"presence\":2", StringComparison.Ordinal)
            .Replace("\"leftTick\":null", "\"leftTick\":20", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("mort, déclaré parti", Assert.Throws<FormatException>(() => CombatEngine.Restore(dying.CompletedSetup(), CombatSnapshot.Parse(leftDead),
            NullCombatLog.Instance)).Message);
        // 14e tour : un texte tronqué au milieu de true, false ou null est illisible (FormatException), pas une erreur d'indice.
        foreach (string truncated in new[] { "n", "tru", "{\"a\":fals", "[nul" })
            Assert.Throws<FormatException>(() => CombatSnapshot.Parse(truncated));
        // File vide (un Bastion et un mannequin, sans commande) : son prochain numéro ne peut pas être négatif.
        var quiet = new CombatEngine(Space.SpaceTestKit.Spatial(Space.SpaceTestKit.Arena(10), new CombatantSetup[]
        {
            Space.SpaceTestKit.Player("bastion", Space.SpaceTestKit.At("0", "0")), Space.SpaceTestKit.PlacedDummy("cible", Space.SpaceTestKit.At("3", "0")),
        }, Array.Empty<CombatCommand>(), durationMs: 2000), NullCombatLog.Instance);
        while (quiet.CurrentTick < 5) quiet.Step();
        string empty = quiet.Snapshot().Text;
        Assert.Contains("\"count\":0}", empty.Split('\n')[0], StringComparison.Ordinal);
        string negative = new System.Text.RegularExpressions.Regex("\"next\":[0-9]+").Replace(empty, "\"next\":-5", 1);
        Assert.NotEqual(empty, negative);
        Assert.Contains("Prochain numéro de la file négatif", Assert.Throws<FormatException>(() => CombatEngine.Restore(quiet.CompletedSetup(),
            CombatSnapshot.Parse(negative), NullCombatLog.Instance)).Message);
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
