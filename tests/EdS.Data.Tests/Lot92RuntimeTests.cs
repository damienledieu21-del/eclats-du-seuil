using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Runtime;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 9.2 : pont temps réel (<see cref="RealtimeCombat"/>) et séance d'entraînement (<see cref="TrainingSession"/>). Le pont découpe
/// le temps réel en ticks, transforme les intentions du joueur en commandes du tick suivant et tire ses retours des compteurs du moteur ;
/// le combat complété des commandes soumises se rejoue à l'identique.
/// </summary>
[Trait("Category", "Runtime")]
public class Lot92RuntimeTests
{
    private static string Scenario(string name) => ScenarioPath(name);

    private static TrainingSession Session(string scenario = "entrainement_garde_parade", string? build = null, long? durationMs = 60_000) =>
        TrainingSession.Load(DataDirectory, Scenario(scenario), build, durationMs: durationMs);

    private static List<CombatCommand> Commands(RealtimeCombat combat, long tick) =>
        combat.SubmittedCommands.Where(c => c.AtMs == tick * 50).ToList();

    [Fact]
    public void Seance_sans_commande_au_build_choisi_et_a_la_duree_maximale()
    {
        TrainingSession session = TrainingSession.Load(DataDirectory, Scenario("entrainement_garde_parade"), "build.bastion.gardien");
        Assert.Empty(session.Setup.Commands);
        Assert.Equal(session.Setup.Combat.MaxDurationMs, session.Setup.DurationMs);
        Assert.Same(session.Build!.Build, session.Setup.Combatants.OfType<CharacterSetup>().Single(c => c.Id == "bastion").Build);
        Assert.Equal(new[] { "build.bastion.rempart", "build.bastion.contrecoup", "build.bastion.gardien", "build.bastion.hybride", "build.bastion.niveau_10" },
            session.AvailableBuilds().Select(b => b.Id));
        Assert.Equal("build.bastion.rempart", session.WithBuild("build.bastion.rempart").Build!.Id);

        TrainingSession scripted = Session();
        Assert.Null(scripted.Build);
        Assert.Equal(60_000, scripted.Setup.DurationMs);
    }

    [Fact]
    public void Seance_refusee_pour_un_build_un_personnage_ou_des_donnees_invalides()
    {
        Assert.Throws<ArgumentException>(() => Session(build: "build.bastion.inconnu"));
        Assert.Throws<ArgumentException>(() => TrainingSession.Load(DataDirectory, Scenario("entrainement_garde_parade"), playerId: "personne"));
        Assert.Throws<InvalidDataException>(() => TrainingSession.Load(Path.Combine(DataDirectory, "absent"), Scenario("entrainement_garde_parade")));
        string bad = Path.Combine(Path.GetTempPath(), "eds-scenario-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(bad, "{ \"schemaVersion\": 3, ");
            var e = Assert.Throws<InvalidDataException>(() => TrainingSession.Load(DataDirectory, bad));
            Assert.Contains("InvalidJson", e.Message, StringComparison.Ordinal);
        }
        finally { File.Delete(bad); }
    }

    [Fact]
    public void Temps_reel_decoupe_en_ticks_de_50_ms_et_rattrapage_borne()
    {
        RealtimeCombat combat = Session().Start();
        Assert.Equal(0, combat.Advance(0.049));
        Assert.Equal(1, combat.Advance(0.001));
        Assert.Equal(1, combat.Tick);
        Assert.Equal(2, combat.Advance(0.1));
        Assert.Equal(5, combat.Advance(10));   // une image de 10 s : 5 ticks, le reste abandonné
        Assert.Equal(8, combat.Tick);
        Assert.Equal(0, combat.Advance(0.01));
        Assert.InRange(combat.TickProgress, 0.19, 0.21);
        Assert.Throws<ArgumentOutOfRangeException>(() => combat.Advance(-0.01));
        Assert.Throws<ArgumentOutOfRangeException>(() => combat.Advance(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => combat.Advance(double.PositiveInfinity));
    }

    [Fact]
    public void Garde_tenue_reprise_apres_un_lancer_et_relachee()
    {
        RealtimeCombat combat = Session().Start();
        combat.SetGuard(true);
        combat.StepOnce();
        Assert.Equal(new[] { CombatAction.GuardStart }, Commands(combat, 0).Select(c => c.Action));
        Assert.True(combat.Player().View.Guarding);

        // Lancer en garde : la garde est quittée au même tick, puis reprise au tick suivant tant qu'elle est tenue.
        Assert.True(combat.UseSlot(0));
        combat.StepOnce();
        Assert.Equal(new[] { CombatAction.GuardStop, CombatAction.UseSkill }, Commands(combat, 1).Select(c => c.Action));
        Assert.Equal("skill.bastion.frappe_du_rempart", Commands(combat, 1)[1].Skill);
        Assert.Equal("assaillant", Commands(combat, 1)[1].Target);
        combat.StepOnce();
        Assert.Equal(new[] { CombatAction.GuardStart }, Commands(combat, 2).Select(c => c.Action));

        combat.SetGuard(false);
        combat.StepOnce();
        Assert.Equal(new[] { CombatAction.GuardStop }, Commands(combat, 3).Select(c => c.Action));
        Assert.False(combat.Player().View.Guarding);
        combat.StepOnce();
        Assert.Empty(Commands(combat, 4));
    }

    [Fact]
    public void Competence_visiblement_indisponible_signalee_sans_commande()
    {
        RealtimeCombat combat = Session().Start();
        Assert.True(combat.UseSlot(0));
        combat.StepOnce();
        combat.DrainFeedback();
        SkillSlot frappe = combat.Player().Slots.Single(s => s.Slot == 0);
        Assert.StartsWith("en recharge", frappe.Unavailable);
        Assert.InRange(frappe.RemainingSeconds, 5.9, 6.0);
        Assert.False(combat.UseSlot(0));
        Assert.False(combat.UseSlot(7));
        CombatFeedback[] feedback = combat.DrainFeedback().ToArray();
        Assert.Equal(new[] { FeedbackKind.Unavailable, FeedbackKind.Unavailable }, feedback.Select(f => f.Kind));
        Assert.StartsWith("Frappe du rempart : en recharge", feedback[0].Detail);
        Assert.Equal("emplacement vide", feedback[1].Detail);
        int before = combat.SubmittedCommands.Count;
        combat.StepOnce();
        Assert.Equal(before, combat.SubmittedCommands.Count);

        // Pendant la recharge globale (1 s après un lancer), puis : l'ultime attend 100 de Résonance.
        Assert.Equal("recharge globale", combat.Player().Slots.Single(s => s.IsUltimate).Unavailable);
        while (combat.Tick < 30) combat.StepOnce();
        Assert.Matches("^Résonance [0-9]+/100$", combat.Player().Slots.Single(s => s.IsUltimate).Unavailable);
    }

    [Fact]
    public void Parade_au_tick_de_l_impact_et_retours_de_combat()
    {
        // Assaillant du scénario : première attaque au tick 20. Garde tenue, parade demandée juste avant ce tick.
        RealtimeCombat combat = Session().Start();
        combat.SetGuard(true);
        while (combat.Tick < 20) combat.StepOnce();
        combat.DrainFeedback();
        combat.Parry();
        combat.StepOnce();
        Assert.Contains(Commands(combat, 20), c => c.Action == CombatAction.Parry);
        Assert.Contains(combat.DrainFeedback(), f => f.Kind == FeedbackKind.PerfectParry && f.CombatantId == "bastion" && f.Amount == 1);

        // Attaque suivante (tick 60) bloquée par la garde ; puis, garde relâchée, des PV perdus.
        while (combat.Tick <= 60) combat.StepOnce();
        Assert.Contains(combat.DrainFeedback(), f => f.Kind == FeedbackKind.Blocked && f.CombatantId == "bastion");
        combat.SetGuard(false);
        while (combat.Tick <= 100) combat.StepOnce();
        Assert.Contains(combat.DrainFeedback(), f => f.Kind == FeedbackKind.DamageTaken && f.CombatantId == "bastion" && f.Amount > 0);
    }

    [Fact]
    public void Telegraphie_et_vue_des_adversaires()
    {
        RealtimeCombat combat = Session().Start();
        OtherState assailant = Assert.Single(combat.Others());
        Assert.True(assailant.IsEnemy);
        Assert.Equal(1.0, assailant.NextAttackSeconds!.Value, 3);
        Assert.Equal("bastion", assailant.View.CurrentTarget);
        while (combat.Tick < 10) combat.StepOnce();
        Assert.Equal(0.5, combat.Others()[0].NextAttackSeconds!.Value, 3);
    }

    [Fact]
    public void Cibles_parcourues_parmi_les_ennemis_vivants()
    {
        RealtimeCombat combat = TrainingSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios", "mesures", "s3_meute.json"), durationMs: 10_000).Start();
        Assert.Equal("loup_a", combat.TargetId);
        combat.NextTarget();
        Assert.Equal("loup_b", combat.TargetId);
        combat.NextTarget();
        Assert.Equal("hurleur", combat.TargetId);
        combat.NextTarget();
        Assert.Equal("loup_a", combat.TargetId);
        combat.SelectTarget("hurleur");
        Assert.Equal("hurleur", combat.TargetId);
        Assert.Throws<ArgumentException>(() => combat.SelectTarget("bastion"));
    }

    [Fact]
    public void Soutien_sur_soi_en_solo()
    {
        RealtimeCombat combat = Session(build: "build.bastion.gardien").Start();
        SkillSlot barriere = combat.Player().Slots.Single(s => s.SkillId == "skill.bastion.barriere_d_acier");
        Assert.True(combat.UseSlot(barriere.Slot));
        combat.StepOnce();
        CombatCommand cast = Commands(combat, 0).Single(c => c.Action == CombatAction.UseSkill);
        Assert.Equal("bastion", cast.Target);
        Assert.Contains("steelBarrier", combat.Player().View.States);
    }

    /// <summary>Joue un combat au hasard (graine fixe) jusqu'au bout, en passant par des images de durées irrégulières.</summary>
    private static RealtimeCombat PlayRandomly(int seed, string? build)
    {
        var random = new Random(seed);
        RealtimeCombat combat = Session(build: build, durationMs: 20_000).Start();
        while (!combat.IsFinished)
        {
            switch (random.Next(10))
            {
                case 0: combat.SetGuard(!combat.GuardHeld); break;
                case 1: combat.Parry(); break;
                case 2: combat.UseSlot(random.Next(6)); break;
                case 3: combat.NextTarget(); break;
            }
            combat.Advance(random.NextDouble() * 0.12);
        }
        return combat;
    }

    [Theory]
    [InlineData(1, "build.bastion.rempart")]
    [InlineData(2, "build.bastion.contrecoup")]
    [InlineData(3, "build.bastion.gardien")]
    [InlineData(4, null)]
    public void Combat_complete_des_commandes_soumises_rejoue_a_l_identique(int seed, string? build)
    {
        RealtimeCombat combat = PlayRandomly(seed, build);
        Assert.NotEmpty(combat.SubmittedCommands);
        var log = new MemoryCombatLog();
        CombatResult replay = CombatEngine.Run(combat.CompletedSetup(), log);
        CombatResult played = combat.Result!;
        Assert.Equal((played.EndReason, played.EndTick), (replay.EndReason, replay.EndTick));
        foreach (CombatantResult r in played.Combatants)
        {
            CombatantResult x = replay[r.Id];
            Assert.Equal((r.DamageDealt, r.DamageTaken, r.Blocks, r.PerfectParries, r.FailedParries, r.SkillsCast, r.CommandsRejected, r.Health),
                (x.DamageDealt, x.DamageTaken, x.Blocks, x.PerfectParries, x.FailedParries, x.SkillsCast, x.CommandsRejected, x.Health));
        }
        // Le journal du combat rejoué se rejoue lui-même octet par octet.
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines.ToList(), ShippedData).Status);
    }

    [Fact]
    public void Compteurs_de_la_vue_egaux_au_resultat_final()
    {
        RealtimeCombat combat = PlayRandomly(5, "build.bastion.rempart");
        CombatantResult result = combat.Result!["bastion"];
        CombatantCounters counters = combat.Player().View.Counters;
        Assert.Equal((result.DamageDealt, result.DamageTaken, result.Blocks, result.PerfectParries, result.SkillsCast, result.UltimatesCast),
            (counters.DamageDealt, counters.DamageTaken, counters.Blocks, counters.PerfectParries, counters.SkillsCast, counters.UltimatesCast));
        Assert.Equal(0, combat.Advance(1));
        combat.StepOnce();   // sans effet une fois terminé
    }

    [Fact]
    public void Retours_bornes_quand_le_client_ne_les_lit_pas()
    {
        RealtimeCombat combat = Session().Start();
        for (int i = 0; i < RealtimeCombat.MaxPendingFeedback + 10; i++) combat.UseSlot(7);
        Assert.Equal(RealtimeCombat.MaxPendingFeedback, combat.DrainFeedback().Count);
        Assert.Empty(combat.DrainFeedback());
    }

    [Fact]
    public void Joueur_qui_n_est_pas_un_personnage_refuse()
    {
        TrainingSession session = Session();
        Assert.Throws<ArgumentException>(() => new RealtimeCombat(session.Setup, "assaillant"));
        Assert.Throws<ArgumentException>(() => new RealtimeCombat(session.Setup, "personne"));
    }
}
