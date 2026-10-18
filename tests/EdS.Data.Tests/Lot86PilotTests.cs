using System.Text.Json.Nodes;
using EdS.Data.Builds;
using EdS.Data.Json.Measures;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using EdS.Simulation.Pilot;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 8.6 : API de pilotage du moteur (vue en lecture seule, soumission pour le tick courant) et pilote de mesure. Le pilote vit
/// hors du moteur ; un combat piloté est un combat ordinaire, complété de commandes, rejouable octet par octet.
/// </summary>
[Trait("Category", "Scenario")]
[Trait("Category", "Engine")]
public class Lot86PilotTests
{
    private static string MeasuresDirectory => Path.Combine(AppContext.BaseDirectory, "scenarios", "mesures");

    private static MeasureScenario Scenario(string id)
    {
        var failures = new List<(string, ScenarioLoadResult)>();
        MeasureScenario scenario = Measures.LoadScenarios(MeasuresDirectory, ShippedData, failures).Single(s => s.Id == id);
        Assert.Empty(failures);
        return scenario;
    }

    private static ReferenceBuild Build(string id)
    {
        Assert.True(ShippedData.TryGetBuild(id, out ReferenceBuild build));
        return build;
    }

    private static CombatSetup Frozen(Action<JsonObject>? mutate = null)
    {
        ScenarioLoadResult loaded = ReadFrozenMutated(root => mutate?.Invoke(root), scenario: "entrainement_garde_parade");
        Assert.True(loaded.Setup != null, string.Join("\n", loaded.Report.Errors));
        return loaded.Setup!;
    }

    private static CombatSetup WithoutCommands(CombatSetup s) =>
        new(s.Seed, s.DurationMs, s.Stats, s.Damage, s.Progression, s.Combat, s.Combatants, Array.Empty<CombatCommand>(), s.Data);

    private static IEnumerable<string> Body(MemoryCombatLog log) => log.Lines.Skip(1);   // sans l'en-tête (qui recopie les commandes)

    [Fact]
    public void Commandes_soumises_tick_par_tick_identiques_aux_commandes_du_scenario()
    {
        CombatSetup original = Frozen();
        var expected = new MemoryCombatLog();
        CombatResult expectedResult = CombatEngine.Run(original, expected);

        var log = new MemoryCombatLog();
        var engine = new CombatEngine(WithoutCommands(original), log);
        while (!engine.IsFinished)
        {
            foreach (CombatCommand c in original.Commands.Where(c => c.AtMs / 50 == engine.CurrentTick)) engine.Submit(c);
            engine.Step();
        }
        Assert.Equal(original.Commands, engine.SubmittedCommands);
        Assert.Equal(Body(expected), Body(log));
        Assert.Equal(expectedResult.EndTick, engine.Result!.EndTick);
        Assert.Equal(expectedResult["bastion"].Blocks, engine.Result["bastion"].Blocks);
        Assert.Equal(expectedResult["bastion"].PerfectParries, engine.Result["bastion"].PerfectParries);
    }

    [Fact]
    public void Soumission_refusee_hors_du_tick_courant_ou_pour_un_inconnu()
    {
        var engine = new CombatEngine(Frozen(), NullCombatLog.Instance);
        engine.Step();   // tick 1
        Assert.Throws<ArgumentException>(() => engine.Submit(new CombatCommand(0, "bastion", CombatAction.Parry)));
        Assert.Throws<ArgumentException>(() => engine.Submit(new CombatCommand(100, "bastion", CombatAction.Parry)));
        Assert.Throws<ArgumentException>(() => engine.Submit(new CombatCommand(60, "bastion", CombatAction.Parry)));
        Assert.Throws<ArgumentException>(() => engine.Submit(new CombatCommand(50, "personne", CombatAction.Parry)));
        Assert.Throws<ArgumentException>(() => engine.Submit(new CombatCommand(50, "bastion", "skill.bastion.frappe_du_rempart", "personne")));
        Assert.Throws<ArgumentNullException>(() => engine.Submit(null!));
        Assert.Empty(engine.SubmittedCommands);
        engine.Submit(new CombatCommand(50, "bastion", CombatAction.Parry));
        Assert.Single(engine.SubmittedCommands);
    }

    [Fact]
    public void Plafond_de_commandes_par_acteur_et_par_tick_compte_les_commandes_du_scenario()
    {
        // Le scénario donne déjà une commande au Bastion au tick 0 : trois de plus atteignent le plafond (4), la cinquième est refusée.
        var engine = new CombatEngine(Frozen(), NullCombatLog.Instance);
        int limit = Frozen().Combat.MaxCommandsPerActorPerTick;
        Assert.Equal(4, limit);
        for (int i = 1; i < limit; i++) engine.Submit(new CombatCommand(0, "bastion", CombatAction.GuardStart));
        Assert.Throws<InvalidOperationException>(() => engine.Submit(new CombatCommand(0, "bastion", CombatAction.Parry)));
        Assert.Equal(limit - 1, engine.SubmittedCommands.Count);
        engine.Submit(new CombatCommand(0, "assaillant", CombatAction.Parry));   // autre acteur : admis (refusé en combat, pas à la soumission)
    }

    [Fact]
    public void Soumission_refusee_apres_la_fin()
    {
        var engine = new CombatEngine(Frozen(root =>
        {
            DataOf(root)["durationMs"] = 100;
            DataOf(root)["commands"] = new JsonArray();
        }), NullCombatLog.Instance);
        while (!engine.IsFinished) engine.Step();
        Assert.Throws<InvalidOperationException>(() => engine.Submit(new CombatCommand(100, "bastion", CombatAction.Parry)));
    }

    [Fact]
    public void Vue_du_debut_de_tick()
    {
        var engine = new CombatEngine(Frozen(), NullCombatLog.Instance);
        CombatantView bastion = engine.View("bastion"), assailant = engine.View("assaillant");
        Assert.Equal(CombatantKind.Character, bastion.Kind);
        Assert.False(bastion.Guarding);
        Assert.True(bastion.CanCast && bastion.CanGuard && bastion.CanParry);
        Assert.Equal(bastion.MaxHealth, bastion.Health);
        Assert.Equal(CombatantKind.Assailant, assailant.Kind);
        Assert.Null(assailant.Health);
        Assert.Equal(20, assailant.NextAttackTick);
        Assert.Equal("coup", assailant.NextAttack!.Id);
        Assert.Equal("bastion", assailant.CurrentTarget);
        Assert.Throws<ArgumentException>(() => engine.View("personne"));

        engine.Step();   // garde prise au tick 0
        Assert.True(engine.View("bastion").Guarding);
        while (engine.CurrentTick <= 20) engine.Step();   // première attaque (et parade) au tick 20
        Assert.Equal(60, engine.View("assaillant").NextAttackTick);
        Assert.Equal("coup_bis", engine.View("assaillant").NextAttack!.Id);
    }

    [Fact]
    public void Vue_sans_reference_a_l_etat_du_moteur()
    {
        var engine = new CombatEngine(Frozen(), NullCombatLog.Instance);
        while (engine.CurrentTick <= 190) engine.Step();   // Frappe du rempart lancée à 9,5 s
        CombatantView view = engine.View("bastion");
        long ready = view.CooldownReadyTick("skill.bastion.frappe_du_rempart");
        Assert.True(ready > engine.CurrentTick);
        ((Dictionary<string, long>)view.CooldownReadyTicks)["skill.bastion.frappe_du_rempart"] = 0;
        Assert.Equal(ready, engine.View("bastion").CooldownReadyTick("skill.bastion.frappe_du_rempart"));
    }

    [Fact]
    public void Provocation_visible_dans_la_cible_courante_de_l_assaillant()
    {
        CombatSetup setup = Measures.WithBuild(Scenario("S4").Setup, Build(Measures.Gardien));
        var engine = new CombatEngine(setup, NullCombatLog.Instance);
        Assert.Equal("allie", engine.View("pillard_a").CurrentTarget);
        engine.Submit(new CombatCommand(0, "bastion", "skill.bastion.provocation", "pillard_a"));
        engine.Step();
        Assert.Equal("bastion", engine.View("pillard_a").CurrentTarget);
        Assert.Equal("allie", engine.View("pillard_b").CurrentTarget);
        Assert.Null(engine.View("pillard_a").Health);
    }

    [Fact]
    public void Combat_pilote_deterministe_et_rejouable()
    {
        foreach (PilotProfile profile in Measures.Profiles.Values)
        {
            CombatSetup setup = Measures.WithBuild(Scenario("S2").Setup, Build(Measures.Contrecoup));
            var a = new MemoryCombatLog();
            var b = new MemoryCombatLog();
            PilotedCombat first = PilotedCombat.Run(setup, Measures.MeasuredId, profile, a);
            PilotedCombat.Run(setup, Measures.MeasuredId, profile, b);
            Assert.Equal(a.ToString(), b.ToString());
            Assert.Empty(setup.Commands);
            Assert.NotEmpty(first.Setup.Commands);
            ReplayOutcome replay = CombatLogReplayer.Replay(a.Lines.ToList(), ShippedData);
            Assert.True(replay.Status == ReplayStatus.Identical, string.Join("\n", replay.Problems));
        }
    }

    [Fact]
    public void Commandes_de_depart_conservees_par_le_pilote()
    {
        CombatSetup setup = Frozen(root => DataOf(root)["commands"] = new JsonArray(new JsonObject { ["atMs"] = 0, ["actor"] = "bastion", ["action"] = "guardStart" }));
        PilotedCombat combat = PilotedCombat.Run(setup, "bastion", PilotProfile.Standard, NullCombatLog.Instance);
        Assert.Equal(setup.Commands[0], combat.Setup.Commands[0]);
        Assert.True(combat.Setup.Commands.Count > 1);
        // Le pilote voit l'état du début du tick, pas les commandes en attente : son lancer du tick 0 (aucun impact à ce tick) est
        // traité après la prise de garde du scénario (rang 0) et refusé, le Bastion étant en garde ; le combat reste reproductible.
        Assert.Equal(CombatAction.UseSkill, combat.Setup.Commands[1].Action);
        Assert.Equal(0, combat.Setup.Commands[1].AtMs);
        Assert.Equal(1, combat.Result["bastion"].CommandsRejected);
    }

    [Fact]
    public void Pilote_refuse_un_non_personnage_et_un_profil_inconnu()
    {
        var engine = new CombatEngine(Frozen(), NullCombatLog.Instance);
        Assert.Throws<ArgumentException>(() => new CombatPilot(engine, "assaillant", PilotProfile.Standard));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CombatPilot(engine, "bastion", (PilotProfile)7));
        Assert.Throws<ArgumentException>(() => new CombatPilot(engine, "personne", PilotProfile.Standard));
    }

    /// <summary>Attaques éligibles à une parade du Bastion d'après le journal : bloquables, qui le visent ou frappent en zone.</summary>
    private static int EligibleAttacks(MemoryCombatLog log) =>
        log.Lines.Count(l => l.Contains("\"event\":\"attackStarted\"", StringComparison.Ordinal) && l.Contains("\"unblockable\":false", StringComparison.Ordinal)
            && (l.Contains("\"target\":\"bastion\"", StringComparison.Ordinal) || l.Contains("\"target\":null", StringComparison.Ordinal)));

    [Fact]
    public void Profils_debutant_aucune_parade_standard_une_sur_deux_expert_toutes()
    {
        // S2 avec le Rempart : le Bastion survit au profil standard et expert, jamais contrôlé, aucune récupération de parade ratée.
        var counts = new Dictionary<PilotProfile, (int Parries, int Eligible, long Perfect)>();
        foreach (PilotProfile profile in Measures.Profiles.Values)
        {
            var log = new MemoryCombatLog();
            PilotedCombat combat = PilotedCombat.Run(Measures.WithBuild(Scenario("S2").Setup, Build(Measures.Rempart)), Measures.MeasuredId, profile, log);
            int parries = combat.Setup.Commands.Count(c => c.Action == CombatAction.Parry);
            counts[profile] = (parries, EligibleAttacks(log), combat.Result["bastion"].PerfectParries);
            Assert.Equal(0, combat.Result["bastion"].FailedParries);
        }
        Assert.Equal(0, counts[PilotProfile.Beginner].Parries);
        Assert.Equal(0, counts[PilotProfile.Beginner].Perfect);
        Assert.Equal((counts[PilotProfile.Standard].Eligible + 1) / 2, counts[PilotProfile.Standard].Parries);
        Assert.Equal(counts[PilotProfile.Expert].Eligible, counts[PilotProfile.Expert].Parries);
        Assert.Equal(counts[PilotProfile.Expert].Parries, counts[PilotProfile.Expert].Perfect);
    }

    [Fact]
    public void Aucune_garde_face_a_un_mannequin()
    {
        PilotedCombat combat = PilotedCombat.Run(Measures.WithBuild(Scenario("S1").Setup, Build(Measures.Rempart)), Measures.MeasuredId, PilotProfile.Expert, NullCombatLog.Instance);
        Assert.DoesNotContain(combat.Setup.Commands, c => c.Action == CombatAction.GuardStart || c.Action == CombatAction.Parry);
        Assert.Equal(0, combat.Result["bastion"].GuardTicks);
        Assert.True(combat.Result["bastion"].SkillsCast > 0);
    }

    [Fact]
    public void Garde_baissee_pour_regenerer_sans_rupture_par_l_entretien()
    {
        // Contrecoup (garde cassable) au profil débutant sur S2 : il bloque tout ; la pause de régénération évite les ruptures dues à
        // une endurance insuffisante (aucune altération de rupture n'est posée par les attaques de S2).
        var log = new MemoryCombatLog();
        PilotedCombat combat = PilotedCombat.Run(Measures.WithBuild(Scenario("S2").Setup, Build(Measures.Contrecoup)), Measures.MeasuredId, PilotProfile.Beginner, log);
        Assert.Equal(0, combat.Result["bastion"].GuardBreaks);
        Assert.Contains(combat.Setup.Commands, c => c.Action == CombatAction.GuardStop);
        Assert.Contains(log.Lines, l => l.Contains("\"event\":\"enduranceRegenerated\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Provocation_jamais_sur_un_assaillant_sans_cible_unique()
    {
        // Revue Codex de la PR #10 : en S3, le hurleur n'attaque qu'en zone (cible nulle) ; il n'a rien à détourner.
        foreach (PilotProfile profile in Measures.Profiles.Values)
        {
            PilotedCombat combat = PilotedCombat.Run(Measures.WithBuild(Scenario("S3").Setup, Build(Measures.Gardien)), Measures.MeasuredId, profile, NullCombatLog.Instance);
            Assert.DoesNotContain(combat.Setup.Commands, c => c.Skill == "skill.bastion.provocation" && c.Target == "hurleur");
        }
    }

    [Fact]
    public void Rapports_de_PV_compares_sans_arrondi()
    {
        // Revue Codex de la PR #10 : F44/F45 < F45/F46 (F44 × F46 − F45² = −1), mais les deux rapports ont le même double.
        const long f44 = 701_408_733, f45 = 1_134_903_170, f46 = 1_836_311_903;
        Assert.Equal((double)f44 / f45, (double)f45 / f46);
        Assert.Equal(-1, CombatPilot.CompareHealthRatio(f44, f45, f45, f46));
        Assert.Equal(1, CombatPilot.CompareHealthRatio(f45, f46, f44, f45));
        Assert.Equal(0, CombatPilot.CompareHealthRatio(1, 2, 200, 400));
        Assert.Equal(0, CombatPilot.CompareHealthRatio(int.MaxValue, int.MaxValue, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatPilot.CompareHealthRatio(1, 0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatPilot.CompareHealthRatio(1, 1, 1, -1));
    }

    [Fact]
    public void Soumission_refusee_quand_la_file_ou_ses_evenements_initiaux_sont_pleins()
    {
        // Revue Codex de la PR #10 : la capacité de la file est contrôlée avant toute modification ; le scénario complété garde un
        // nombre d'événements initiaux (une entrée par commande) admissible.
        Action<JsonObject> noCommands = root => DataOf(root)["commands"] = new JsonArray();
        long bound = Frozen(noCommands).ComputeQueueBound().Total;
        long capacity = checked(bound + 2);
        string directory = CopyFrozenData((path, text) => path == "rules.json" ? text.Replace("\"maxPendingEvents\": 100000", $"\"maxPendingEvents\": {capacity}", StringComparison.Ordinal) : text);
        try
        {
            GameData data = GameDataLoader.Load(directory).Data!;
            Assert.Equal(capacity, data.Rules.Combat.MaxPendingEvents);
            ScenarioLoadResult loaded = ReadFrozenMutated(noCommands, data, "entrainement_garde_parade");
            Assert.True(loaded.Setup != null, string.Join("\n", loaded.Report.Errors));
            CombatSetup setup = loaded.Setup!;
            var engine = new CombatEngine(setup, NullCombatLog.Instance);
            int accepted = 0;
            InvalidOperationException? refused = null;
            while (!engine.IsFinished && refused is null)
            {
                try
                {
                    engine.Submit(new CombatCommand(engine.CurrentTick * 50, "bastion", CombatAction.Parry));
                    accepted++;
                }
                catch (InvalidOperationException e) { refused = e; }
                engine.Step();
            }
            Assert.NotNull(refused);
            Assert.Contains("maxPendingEvents", refused!.Message, StringComparison.Ordinal);
            Assert.Equal(capacity - setup.InitialEventCount(), accepted);
            Assert.Equal(accepted, engine.SubmittedCommands.Count);   // le refus n'a rien ajouté
            while (!engine.IsFinished) engine.Step();
            Assert.True(engine.Result!.Peaks.PendingEvents <= capacity);
        }
        finally { Directory.Delete(directory, true); }
    }
}
