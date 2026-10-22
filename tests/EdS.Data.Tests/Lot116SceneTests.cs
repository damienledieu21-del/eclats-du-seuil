using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 11.6 : scène de démonstration des Vallons et pont temps réel d'un combat positionné. La marche saisie (flottants de
/// l'appareil) devient un cap fixe ; le pont n'envoie <c>move</c>, <c>stop</c> et <c>select</c> au moteur que quand ils changent, dans la
/// place laissée par le plafond de commandes du tick ; le moteur déplace le personnage ; le combat complété se rejoue à l'identique.
/// L'affichage sait qui est en vue du joueur (la caméra ne voit pas à travers les parois) et quelles attaques sont annoncées au sol.
/// </summary>
[Trait("Category", "Scene")]
[Trait("Category", "Runtime")]
public class Lot116SceneTests
{
    private const string Vallons = "vallons_demo";

    private static TrainingSession Session(long durationMs = 60_000, string scenario = Vallons) =>
        TrainingSession.Load(DataDirectory, ScenarioPath(scenario), "build.bastion.rempart", durationMs: durationMs);

    private static List<CombatCommand> At(RealtimeCombat combat, long tick) => combat.SubmittedCommands.Where(c => c.AtMs == tick * 50).ToList();

    private static GroundVector Position(RealtimeCombat combat) => combat.Player().View.Position!.Value;

    [Fact]
    public void Caps_de_marche_fixes_et_directions_en_virgule_fixe()
    {
        Assert.Equal(0, MoveInput.Heading(1, 0));
        Assert.Equal(16, MoveInput.Heading(0, 1));
        Assert.Equal(32, MoveInput.Heading(-1, 0));
        Assert.Equal(48, MoveInput.Heading(0, -1));
        Assert.Equal(8, MoveInput.Heading(0.7, 0.7));
        Assert.Equal(0, MoveInput.Heading(1, -0.04));   // à moins d'un demi-cap : est
        Assert.Null(MoveInput.Heading(0.1, 0.1));        // zone morte
        Assert.Null(MoveInput.Heading(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MoveInput.Heading(double.NaN, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MoveInput.Direction(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MoveInput.Direction(MoveInput.Headings));
        for (int k = 0; k < MoveInput.Headings; k++)
        {
            GroundVector d = MoveInput.Direction(k);
            Assert.True(SpaceLimits.IsDirection(d), $"cap {k}");
            long squared = Planar.LengthSquared(d.X.Raw, d.Z.Raw);
            Assert.InRange(squared, 9_998L * 9_998L, 10_002L * 10_002L);
            Assert.Equal(k, MoveInput.Heading(d.X.Raw / 10_000.0, d.Z.Raw / 10_000.0));
            GroundVector opposite = MoveInput.Direction((k + MoveInput.Headings / 2) % MoveInput.Headings);
            Assert.Equal((-d.X.Raw, -d.Z.Raw), (opposite.X.Raw, opposite.Z.Raw));
        }
    }

    [Fact]
    public void Scene_des_Vallons_decor_passages_et_ennemis_caches_par_le_decor()
    {
        RealtimeCombat combat = Session().Start();
        CombatSpace space = combat.Space!;
        Assert.Equal(new[] { "moulin", "riviere_sud", "riviere_nord", "arbre1", "arbre2", "arbre3", "arbre4", "arbre5", "mur_nord", "mur_sud",
            "pilier1", "pilier2", "pilier3", "pilier4" }, space.Obstacles.Select(o => o.Id));
        SpaceObstacle south = space.Obstacles.Single(o => o.Id == "riviere_sud"), north = space.Obstacles.Single(o => o.Id == "riviere_nord");
        // Le pont (entre les deux bras de la rivière) et le passage étroit (entre les deux murs) laissent passer le Bastion (1 m de large).
        Assert.Equal(Fixed.Parse("3.5"), north.Points.Min(p => p.Z) - south.Points.Max(p => p.Z));
        SpaceObstacle wallN = space.Obstacles.Single(o => o.Id == "mur_nord"), wallS = space.Obstacles.Single(o => o.Id == "mur_sud");
        Assert.Equal(Fixed.Parse("2"), wallN.Points.Min(p => p.Z) - wallS.Points.Max(p => p.Z));
        // Au départ, tous les ennemis sont cachés au Bastion : les loups derrière le moulin, le sanglier et l'archer derrière la rivière.
        List<OtherState> others = combat.Others().ToList();
        Assert.Equal(new[] { "loup1", "loup2", "loup3", "sanglier", "archer" }, others.Select(o => o.Id));
        Assert.All(others, o => Assert.False(o.InSight, o.Id));
        // Sans espace, tout le monde est en vue et la marche est sans effet.
        RealtimeCombat flat = TrainingSession.Load(DataDirectory, ScenarioPath("entrainement_garde_parade"), durationMs: 5000).Start();
        Assert.Null(flat.Space);
        Assert.All(flat.Others(), o => Assert.True(o.InSight));
        flat.SetMove(1, 0);
        Assert.Null(flat.WishedHeading);
        for (int i = 0; i < 5; i++) flat.StepOnce();
        Assert.DoesNotContain(flat.SubmittedCommands, c => c.Action is CombatAction.Move or CombatAction.Stop or CombatAction.Select);
    }

    [Fact]
    public void Marche_simulee_par_le_moteur_une_commande_par_changement_de_cap()
    {
        RealtimeCombat combat = Session().Start();
        GroundVector start = Position(combat);
        combat.SetMove(1, 0);
        for (int i = 0; i < 20; i++) combat.StepOnce();
        // Une seule commande de marche (vers l'est), au premier tick ; aucun ennemi n'est en vue au départ : pas de sélection à ce tick.
        Assert.Equal(new[] { CombatAction.Move }, At(combat, 0).Select(c => c.Action));
        Assert.Equal(new[] { CombatAction.Move }, combat.SubmittedCommands.Where(c => c.Action != CombatAction.Select).Select(c => c.Action));
        Assert.Equal(MoveInput.Direction(0), combat.SubmittedCommands[0].Direction);
        GroundVector after = Position(combat);
        Assert.InRange((after.X - start.X).Raw, 40_000, 60_000);   // environ 5 m/s pendant 1 s
        Assert.Equal(start.Z, after.Z);
        // Même cap : rien de nouveau ; nouveau cap : une commande ; relâché : un arrêt, et le Bastion ne bouge plus.
        combat.SetMove(1, 0.03);
        combat.StepOnce();
        Assert.DoesNotContain(At(combat, 20), c => c.Action is CombatAction.Move or CombatAction.Stop);
        combat.SetMove(0, 1);
        combat.StepOnce();
        Assert.Equal(MoveInput.Direction(16), Assert.Single(At(combat, 21), c => c.Action == CombatAction.Move).Direction);
        for (int i = 0; i < 4; i++) combat.StepOnce();
        Assert.True(Position(combat).Z > after.Z);
        combat.SetMove(0.05, -0.1);   // sous la zone morte
        combat.StepOnce();
        Assert.Single(At(combat, 26), c => c.Action == CombatAction.Stop);
        Assert.DoesNotContain(combat.SubmittedCommands, c => c.Action == CombatAction.Move && c.AtMs > 21 * 50);
        GroundVector stopped = Position(combat);
        for (int i = 0; i < 5; i++) combat.StepOnce();
        Assert.Equal(stopped, Position(combat));
        Assert.Null(combat.Player().View.MoveDirection);
    }

    [Fact]
    public void Cible_selectionnee_envoyee_au_moteur_quand_elle_change()
    {
        // Annonces de l'espace (sous-lot 11.4c) : un ogre et un loup, sans décor, tous deux en vue.
        RealtimeCombat combat = Session(scenario: "espace_annonces").Start();
        combat.StepOnce();
        CombatCommand select = Assert.Single(At(combat, 0));
        Assert.Equal((CombatAction.Select, "ogre"), (select.Action, select.Target));
        Assert.Equal("ogre", combat.Player().View.SelectedTarget);
        combat.StepOnce();
        Assert.Empty(At(combat, 1));
        combat.NextTarget();
        combat.StepOnce();
        Assert.Equal("loup", Assert.Single(At(combat, 2)).Target);
    }

    [Fact]
    public void Ennemi_cache_ni_cible_ni_selectionne()
    {
        // Revue Codex de la PR #34 : un ennemi hors de vue ne s'affiche pas ; il ne devient donc ni la cible (fiche, compte à rebours,
        // compétences), ni la sélection envoyée au moteur (la garde ne se tourne pas vers lui).
        RealtimeCombat combat = Session(durationMs: 20_000).Start();
        Assert.Null(combat.TargetId);
        Assert.Null(combat.Player().TargetId);
        combat.NextTarget();
        Assert.Null(combat.TargetId);
        Assert.Contains("en vue", Assert.Throws<ArgumentException>(() => combat.SelectTarget("loup1")).Message);
        for (int i = 0; i < 5; i++) combat.StepOnce();
        Assert.DoesNotContain(combat.SubmittedCommands, c => c.Action == CombatAction.Select);
        Assert.Null(combat.Player().View.SelectedTarget);

        // Vers le nord : un loup finit par apparaître (au-dessus du moulin) ; il devient la cible, sa sélection part au tick suivant.
        combat.SetMove(0, 1);
        for (int i = 0; i < 200 && combat.TargetId is null; i++) combat.StepOnce();
        string seen = Assert.IsType<string>(combat.TargetId);
        Assert.True(combat.Others().Single(o => o.Id == seen).InSight);
        Assert.All(combat.Others().Where(o => o.IsEnemy && !o.InSight), o => Assert.NotEqual(seen, o.Id));
        combat.StepOnce();
        Assert.Equal(seen, combat.SubmittedCommands.Last(c => c.Action == CombatAction.Select).Target);
        Assert.Equal(seen, combat.Player().View.SelectedTarget);
        // Tab ne propose que des ennemis en vue.
        for (int i = 0; i < 6; i++)
        {
            combat.NextTarget();
            Assert.True(combat.Others().Single(o => o.Id == combat.TargetId).InSight, combat.TargetId);
        }

        // Retour vers le sud jusqu'à ne plus voir aucun ennemi : la cible est lâchée et la sélection vidée.
        combat.SetMove(0, -1);
        for (int i = 0; i < 200 && combat.Others().Any(o => o.IsEnemy && o.InSight); i++) combat.StepOnce();
        Assert.DoesNotContain(combat.Others(), o => o.IsEnemy && o.InSight);
        Assert.Null(combat.TargetId);
        combat.StepOnce();
        Assert.Null(combat.SubmittedCommands.Last(c => c.Action == CombatAction.Select).Target);
        Assert.Null(combat.Player().View.SelectedTarget);
        while (!combat.IsFinished) combat.StepOnce();
        var log = new MemoryCombatLog();
        combat.WriteLog(log);
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines.ToArray(), ShippedData).Status);
    }

    [Fact]
    public void Marche_et_cible_recalees_sur_le_moteur_apres_une_commande_du_scenario()
    {
        // Revue Codex de la PR #34 : un pont ouvert sur un scénario qui a ses propres commandes (la séance d'entraînement les retire,
        // pas un RealtimeCombat construit sur le combat du scénario). Dans espace_annonces, le scénario fait marcher le Bastion vers le
        // nord à 1 100 ms (tick 22) puis l'arrête à 1 650 ms (tick 33) ; un joueur qui tient l'est est remis sur son cap au tick qui suit
        // chaque commande du scénario.
        CombatSetup s = ScenarioJsonReader.ReadFile(ScenarioPath("espace_annonces"), ShippedData).Setup!;
        GroundVector east = MoveInput.EngineDirection(0), north = GroundVector.FromRaw(0, 10_000);
        var held = new RealtimeCombat(s, "bastion");
        held.SetMove(1, 0);
        while (held.Tick < 23) held.StepOnce();
        Assert.Equal(north, held.Player().View.MoveDirection);   // la commande du scénario a pris la main
        held.StepOnce();
        Assert.Equal(MoveInput.Direction(0), Assert.Single(At(held, 23), c => c.Action == CombatAction.Move).Direction);
        Assert.Equal(east, held.Player().View.MoveDirection);
        while (held.Tick < 34) held.StepOnce();
        Assert.Null(held.Player().View.MoveDirection);            // arrêt du scénario
        held.StepOnce();
        Assert.Single(At(held, 34), c => c.Action == CombatAction.Move);
        Assert.Equal(east, held.Player().View.MoveDirection);
        for (int i = 0; i < 10; i++) held.StepOnce();
        Assert.Equal(new long[] { 0, 23, 34 }, held.SubmittedCommands.Where(c => c.Action is CombatAction.Move or CombatAction.Stop).Select(c => c.AtMs / 50));

        // Sans saisie, les commandes du scénario gardent la main : ni marche ni arrêt envoyés, le Bastion marche puis s'arrête comme prévu.
        var idle = new RealtimeCombat(s, "bastion");
        while (idle.Tick < 30) idle.StepOnce();
        Assert.Equal(north, idle.Player().View.MoveDirection);
        while (idle.Tick < 40) idle.StepOnce();
        Assert.Null(idle.Player().View.MoveDirection);
        Assert.DoesNotContain(idle.SubmittedCommands, c => c.Action is CombatAction.Move or CombatAction.Stop);

        // Un cap relâché alors que le scénario a déjà arrêté le Bastion : pas d'arrêt envoyé (il serait refusé : « notMoving »).
        var released = new RealtimeCombat(s, "bastion");
        released.SetMove(0, 1);   // le nord, comme le scénario : rien à corriger à 1 100 ms
        while (released.Tick < 34) released.StepOnce();
        Assert.Null(released.Player().View.MoveDirection);
        released.DrainFeedback();
        released.SetMove(0, 0);
        released.StepOnce();
        Assert.Equal(new long[] { 0 }, released.SubmittedCommands.Where(c => c.Action is CombatAction.Move or CombatAction.Stop).Select(c => c.AtMs / 50));
        Assert.DoesNotContain(released.DrainFeedback(), f => f.Kind == FeedbackKind.Rejected);

        // Sélection : le scénario sélectionne le loup à 500 ms (tick 10) ; la cible du joueur (l'ogre) est renvoyée au tick suivant.
        var setup = new CombatSetup(s.Seed, s.DurationMs, s.Stats, s.Damage, s.Progression, s.Combat, s.Combatants,
            s.Commands.Append(CombatCommand.SelectTarget(500, "bastion", "loup")), s.Data, s.Space);
        var selecting = new RealtimeCombat(setup, "bastion");
        while (selecting.Tick < 11) selecting.StepOnce();
        Assert.Equal(("ogre", "loup"), (selecting.TargetId, selecting.Player().View.SelectedTarget));
        selecting.StepOnce();
        Assert.Equal("ogre", Assert.Single(At(selecting, 11), c => c.Action == CombatAction.Select).Target);
        Assert.Equal("ogre", selecting.Player().View.SelectedTarget);
        while (!selecting.IsFinished) selecting.StepOnce();
        var log = new MemoryCombatLog();
        selecting.WriteLog(log);
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines.ToArray(), ShippedData).Status);
    }

    [Fact]
    public void Plafond_de_commandes_par_tick_respecte_la_marche_attend_le_tick_suivant()
    {
        RealtimeCombat combat = Session(scenario: "espace_annonces").Start();
        int max = combat.CompletedSetup().Combat.MaxCommandsPerActorPerTick;
        combat.SetGuard(true);
        combat.SetMove(1, 0);
        combat.StepOnce();   // garde, marche, sélection : 3
        Assert.Equal(new[] { CombatAction.GuardStart, CombatAction.Move, CombatAction.Select }, At(combat, 0).Select(c => c.Action));
        for (int i = 0; i < 30; i++) combat.StepOnce();
        // En garde : une compétence (quitter la garde + lancer), une parade, une nouvelle marche et une nouvelle cible = 5 > 4.
        long tick = combat.Tick;
        Assert.True(combat.UseSlot(2), "provocation disponible");
        combat.Parry();
        combat.SetMove(0, 1);
        combat.NextTarget();
        combat.StepOnce();
        List<CombatCommand> first = At(combat, tick);
        Assert.Equal(max, first.Count);
        Assert.Equal(new[] { CombatAction.GuardStop, CombatAction.UseSkill, CombatAction.Parry, CombatAction.Move }, first.Select(c => c.Action));
        combat.StepOnce();
        // La sélection part au tick suivant, avec la reprise de la garde (bouton toujours tenu).
        Assert.Contains(At(combat, tick + 1), c => c.Action == CombatAction.Select && c.Target == "loup");
        Assert.All(combat.SubmittedCommands.GroupBy(c => c.AtMs), g => Assert.True(g.Count() <= max, $"{g.Count()} commandes à {g.Key} ms"));
    }

    [Fact]
    public void Partie_positionnee_au_hasard_rejouee_a_l_identique()
    {
        // Saisies tirées au hasard (marche, arrêt, garde, parade, compétences, cible) sur 30 s : le moteur reste seul juge, le combat
        // complété des commandes soumises se rejoue octet par octet, et le Bastion ne traverse jamais le décor.
        var random = new Random(116);
        RealtimeCombat combat = Session(durationMs: 30_000).Start();
        List<ConvexPolygon> obstacles = combat.Space!.Obstacles.Select(o => new ConvexPolygon(o.Points)).ToList();
        long radius = combat.Player().View.Radius!.Value.Raw;
        var visited = new HashSet<(long, long)>();
        while (!combat.IsFinished)
        {
            if (random.Next(10) == 0)
            {
                double a = random.NextDouble() * 2 * Math.PI;
                if (random.Next(5) == 0) combat.SetMove(0, 0);
                else combat.SetMove(Math.Cos(a), Math.Sin(a));
            }
            if (random.Next(15) == 0) combat.SetGuard(random.Next(2) == 0);
            if (random.Next(30) == 0) combat.Parry();
            if (random.Next(25) == 0) combat.UseSlot(random.Next(5));
            if (random.Next(40) == 0) combat.NextTarget();
            combat.StepOnce();
            if (combat.IsFinished) break;
            GroundVector p = Position(combat);
            visited.Add((p.X.Raw / 10_000, p.Z.Raw / 10_000));
            Assert.All(obstacles, o => Assert.False(o.OverlapsCircle(p.X.Raw, p.Z.Raw, radius), $"Bastion dans le décor en {p}"));
        }
        Assert.True(visited.Count > 20, $"{visited.Count} mètres carrés parcourus");
        Assert.True(combat.SubmittedCommands.Count(c => c.Action == CombatAction.Move) > 20);
        var log = new MemoryCombatLog();
        CombatResult replayed = combat.WriteLog(log);
        Assert.Equal((combat.Result!.EndReason, combat.Result.EndTick, combat.Result.Winner), (replayed.EndReason, replayed.EndTick, replayed.Winner));
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines.ToArray(), ShippedData).Status);
    }

    [Fact]
    public void Attaques_annoncees_exposees_a_l_affichage()
    {
        // La meute de l'espace (sous-lot 11.5) annonce une charge (bande) et une pluie de flèches (cercle) : le pont les donne telles que
        // le moteur les a verrouillées, sans rien modifier.
        ScenarioLoadResult loaded = ScenarioJsonReader.ReadFile(ScenarioPath("espace_meute"), ShippedData);
        var combat = new RealtimeCombat(loaded.Setup!, "bastion");
        var shapes = new HashSet<AttackShape>();
        while (!combat.IsFinished)
        {
            foreach (AnnouncedAttack attack in combat.Announced())
            {
                shapes.Add(attack.Shape);
                Assert.True(attack.ImpactTick >= combat.Tick);
                if (attack.Shape is AttackShape.Line or AttackShape.Circle) Assert.NotNull(attack.Zone);
            }
            combat.StepOnce();
        }
        Assert.Contains(AttackShape.Line, shapes);
        Assert.Contains(AttackShape.Circle, shapes);
    }
}
