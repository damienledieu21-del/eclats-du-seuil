using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Net;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.Tools.Network;
using static EdS.Data.Tests.Lot103ClientTests;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 11.7 : démonstration réseau minimale. Deux joueurs dans les Vallons : déplacements, sélection et attaques résolus par le
/// serveur ; le second joueur arrive pendant l'affrontement et tout le monde repart de l'état courant, sans rien rejouer ni réattribuer ;
/// la place du joueur est prédite à l'affichage.
/// </summary>
[Trait("Category", "Duo")]
[Trait("Category", "Network")]
public class Lot117DuoTests
{
    /// <summary>Les Vallons à deux, raccourcis ; <paramref name="change"/> retouche le scénario (combattants, durée…).</summary>
    internal static CombatSetup Vallons(long durationMs = 20_000, Action<JsonObject>? change = null)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ScenarioPath("coop/vallons_duo")))!.AsObject();
        root["data"]!["durationMs"] = durationMs;
        change?.Invoke(root["data"]!.AsObject());
        ScenarioLoadResult result = ScenarioJsonReader.Read(root.ToJsonString(), ShippedData);
        Assert.True(result.Setup != null, result.Report.ToString());
        return result.Setup!;
    }

    /// <summary>La meute tout près des Bastions, engagée d'emblée et fragile : des morts arrivent vite.</summary>
    private static void PackNearby(JsonObject data)
    {
        var at = new Dictionary<string, (double X, double Z, int Health)>
        {
            ["loup1"] = (-14, -12, 60), ["loup2"] = (-14, -10, 400), ["loup3"] = (-14, -14, 900),
        };
        foreach (JsonNode? node in data["combatants"]!.AsArray())
        {
            JsonObject c = node!.AsObject();
            if (!at.TryGetValue((string)c["id"]!, out var place)) continue;
            c["position"] = new JsonArray(place.X, place.Z);
            c["health"] = place.Health;
            c["behavior"]!.AsObject().Remove("aggroRange");
        }
    }

    /// <summary>Joueur scripté dans l'espace : marche (caps changeants, arrêts), garde par moments, compétences, parades, cible.</summary>
    private static Action<NetworkClient> Walking(int period = 41) => client =>
    {
        if (client.Status != ClientStatus.Combat || client.Combat is null) return;
        NetworkCombat combat = client.Combat;
        long step = (long)Math.Round(client.Now * 100);
        int phase = (int)(step / 70 % 6);
        double angle = (step / 70 % 16) * Math.PI / 8 + (client.SeatId == "bastion" ? 0 : 1);
        if (phase == 5) combat.SetMove(0, 0);
        else combat.SetMove(Math.Cos(angle), Math.Sin(angle));
        combat.SetGuard(step / 90 % 3 == 0);
        if (step % period == 0) combat.UseSlot((int)(step / period % 5));
        if (step % 59 == 0) combat.Parry();
        if (step % 233 == 0) combat.NextTarget();
        combat.PredictedPosition();   // l'affichage la demande à chaque image
    };

    /// <summary>Retours relevés par client, tout au long du combat.</summary>
    private sealed class Feedbacks
    {
        public readonly Dictionary<NetworkClient, List<CombatFeedback>> Seen = new();

        public Action<NetworkClient> Wrap(Action<NetworkClient> script) => client =>
        {
            script(client);
            if (client.Combat is null) return;
            if (!Seen.TryGetValue(client, out List<CombatFeedback>? list)) Seen[client] = list = new List<CombatFeedback>();
            list.AddRange(client.Combat.DrainFeedback());
        };
    }

    private static Dictionary<string, CombatantView> Views(NetworkCombat combat) =>
        new[] { combat.Player().View }.Concat(combat.Others().Select(o => o.View)).ToDictionary(v => v.Id);

    [Fact]
    public void Deplacements_selection_et_attaques_resolus_par_le_serveur()
    {
        var game = new Game(Vallons(20_000, PackNearby)) { Script = Walking() };
        (NetworkClient a, NetworkClient b) = game.Start(new LinkProfile(40, 10), new LinkProfile(70, 20));
        game.RunUntil(() => a.Status == ClientStatus.Lobby && b.Status == ClientStatus.Lobby, 30);
        IReadOnlyList<CombatCommand> commands = ServerCommands(game);
        foreach (string id in new[] { "bastion", "bastion2" })
        {
            Assert.Contains(commands, c => c.Actor == id && c.Action == CombatAction.Move);
            Assert.Contains(commands, c => c.Actor == id && c.Action == CombatAction.Stop);
            Assert.Contains(commands, c => c.Actor == id && c.Action == CombatAction.Select);
        }
        // Les deux clients ont rejoué exactement le combat du serveur, sans divergence.
        Assert.Equal(ServerHash(game, a.Combat!), a.Combat!.Hash);
        Assert.Equal(ServerHash(game, b.Combat!), b.Combat!.Hash);
        Assert.Equal((0, 0), (a.Combat.Desyncs, b.Combat.Desyncs));
        // La meute est morte (au moins le loup fragile), chez les deux clients de la même façon.
        Dictionary<string, CombatantView> seenByA = Views(a.Combat), seenByB = Views(b.Combat);
        Assert.True(seenByA["loup1"].IsDead);
        Assert.Equal(seenByA.Keys.OrderBy(k => k, StringComparer.Ordinal), seenByB.Keys.OrderBy(k => k, StringComparer.Ordinal));
        foreach (string id in seenByA.Keys)
            Assert.Equal((seenByA[id].IsDead, seenByA[id].Position, seenByA[id].Health), (seenByB[id].IsDead, seenByB[id].Position, seenByB[id].Health));
    }

    [Fact]
    public void Second_joueur_arrive_pendant_l_affrontement_sans_rien_rejouer_ni_reattribuer()
    {
        var feedbacks = new Feedbacks();
        var game = new Game(Vallons(25_000, PackNearby), new RoomOptions { JoinDuringCombat = true }) { Script = feedbacks.Wrap(Walking()) };
        var link = new LinkProfile(50, 10);
        NetworkClient a = game.Join("Dam", Rempart, link);
        a.SetReady(true);
        game.RunUntil(() => a.Status == ClientStatus.Combat, 5);
        // Seul : bastion2 n'est pas dans le combat de départ.
        Assert.DoesNotContain(a.Combat!.Others(), o => o.Id == "bastion2");
        game.Run(6);
        Assert.Equal(ClientStatus.Combat, a.Status);

        // Arrivée : place prise, état courant pour tout le monde.
        NetworkClient b = game.Join("Léa", Gardien, link);
        game.RunUntil(() => b.Status == ClientStatus.Combat, 5);
        NetworkCombat late = b.Combat!;
        long joined = late.Player().View.ArrivalTick!.Value;
        Assert.InRange(joined, 100, 200);   // environ 6 s après le début
        Assert.True(late.Player().View.IsPresent);
        Dictionary<string, CombatantCounters> atJoin = Views(late).ToDictionary(p => p.Key, p => p.Value.Counters);
        HashSet<string> deadAtJoin = Views(late).Values.Where(v => v.IsDead).Select(v => v.Id).ToHashSet();
        game.Run(0.5);
        Assert.Contains(a.Combat.Others(), o => o.Id == "bastion2" && o.View.IsPresent && o.View.ArrivalTick == joined);
        Assert.Equal(1, a.Combat.Resyncs);   // l'arrivée a fait repartir le premier joueur de l'état courant
        Assert.Equal(1, a.Combat.RosterVersion);   // revue Codex de la PR #35 : l'affichage du premier joueur sait qu'un combattant est arrivé

        game.RunUntil(() => a.Status == ClientStatus.Lobby && b.Status == ClientStatus.Lobby, 40);
        // Même combat que le serveur, de bout en bout, pour les deux (le second a commencé à l'état courant).
        string server = ServerHash(game, a.Combat);
        Assert.Equal(server, a.Combat.Hash);
        Assert.Equal(server, late.Hash);
        Assert.Equal((0, 0), (a.Combat.Desyncs, late.Desyncs));
        CombatSetup completed = CombatLogReplayer.ReadSetup(game.Room.LastJournal![0], ShippedData, out _)!;
        Assert.Equal(joined * 50, completed.Combatants.Single(c => c.Id == "bastion2").ArrivalMs);

        // Aucun événement réattribué : les retours de chacun totalisent exactement ce qui est arrivé pendant sa présence.
        Dictionary<string, CombatantView> end = Views(a.Combat);
        foreach ((NetworkClient client, Func<string, long> before, ISet<string> deadBefore) in new[]
        {
            (a, (Func<string, long>)(_ => 0), (ISet<string>)new HashSet<string>()),
            (b, id => atJoin.TryGetValue(id, out CombatantCounters? c) ? c.DamageTaken : 0, deadAtJoin),
        })
        {
            List<CombatFeedback> seen = feedbacks.Seen[client];
            foreach (CombatantView v in end.Values)
            {
                Assert.Equal(v.Counters.DamageTaken - before(v.Id), seen.Where(f => f.Kind == FeedbackKind.DamageTaken && f.CombatantId == v.Id).Sum(f => f.Amount));
                int deaths = seen.Count(f => f.Kind == FeedbackKind.Died && f.CombatantId == v.Id);
                Assert.Equal(v.IsDead && !deadBefore.Contains(v.Id) ? 1 : 0, deaths);
            }
        }
        Assert.True(end["loup1"].IsDead);
    }

    [Fact]
    public void Arrivee_sur_une_place_prise_decalee_a_cote()
    {
        // Le premier joueur se poste sur la place du second (2 m à l'est de la sienne) : le second entre à 1,5 m de là, sans chevauchement.
        var game = new Game(Vallons(20_000), new RoomOptions { JoinDuringCombat = true });
        NetworkClient a = game.Join("Dam");
        a.SetReady(true);
        game.RunUntil(() => a.Status == ClientStatus.Combat, 5);
        a.Combat!.SetMove(1, 0);
        game.RunUntil(() => a.Combat.Player().View.Position!.Value.X >= Fixed.FromInt(-16), 3);
        a.Combat.SetMove(0, 0);
        game.Run(0.3);
        GroundVector held = a.Combat.Player().View.Position!.Value;
        Assert.InRange(held.X.Raw, -160_000, -150_000);
        NetworkClient b = game.Join("Léa", Gardien);
        game.RunUntil(() => b.Status == ClientStatus.Combat, 5);
        GroundVector placed = b.Combat!.Player().View.Position!.Value;
        Assert.NotEqual(new GroundVector(Fixed.FromInt(-16), Fixed.FromInt(-12)), placed);
        double gap = Math.Sqrt(Math.Pow((placed.X.Raw - held.X.Raw) / 1e4, 2) + Math.Pow((placed.Z.Raw - held.Z.Raw) / 1e4, 2));
        Assert.True(gap >= 1.0, $"emprises à {gap} m");
        Assert.Equal(b.Combat.Hash, a.Combat.Hash);
    }

    [Fact]
    public void Arrivee_du_scenario_au_meme_tick_reservee()
    {
        // Revue Codex de la PR #35 : un loup du scénario arrive, au tick même où le second joueur entre, sur la place de ce dernier.
        // L'arrivée du loup est déjà en file et passe la première : la salle réserve sa place et le joueur entre à côté (sinon il recevait
        // la place, puis son arrivée était refusée et il restait absent tout le combat).
        long JoinTick(CombatSetup scenario, out Game game, out NetworkClient b)
        {
            game = new Game(scenario, new RoomOptions { JoinDuringCombat = true });
            long joined = -1;
            Game g = game;
            game.Room.Trace = line => { if (line.Contains("pendant le combat", StringComparison.Ordinal)) joined = g.Room.Tick; };
            NetworkClient a = game.Join("Dam");
            a.SetReady(true);
            game.RunUntil(() => a.Status == ClientStatus.Combat, 5);
            game.Run(1);
            b = game.Join("Léa", Gardien);
            NetworkClient bb = b;
            game.RunUntil(() => bb.Status == ClientStatus.Combat, 5);
            game.Run(0.5);
            return joined;
        }
        long tick = JoinTick(Vallons(20_000), out _, out _);
        Assert.True(tick > 0, "entrée pendant le combat");
        CombatSetup scenario = Vallons(20_000, data =>
        {
            JsonObject wolf = data["combatants"]!.AsArray().Select(n => n!.AsObject()).Single(c => (string)c["id"]! == "loup2");
            wolf["position"] = new JsonArray(-16, -12);   // la place du scénario de bastion2
            wolf["arrivalMs"] = tick * 50;
        });
        Assert.Equal(tick, JoinTick(scenario, out Game game2, out NetworkClient b2));
        Dictionary<string, CombatantView> views = Views(b2.Combat!);
        Assert.True(views["loup2"].IsPresent, "le loup est arrivé");
        CombatantView me = b2.Combat!.Player().View;
        Assert.True(me.IsPresent, "le second joueur est entré, à côté");
        Assert.NotEqual(new GroundVector(Fixed.FromInt(-16), Fixed.FromInt(-12)), me.Position);
        Assert.Equal(ClientStatus.Combat, b2.Status);
        Assert.Equal(RoomPhase.Combat, game2.Room.Phase);
    }

    [Fact]
    public void Arrivee_dont_le_build_ferait_deborder_l_etat_refusee_sans_toucher_au_combat()
    {
        // Revue Codex de la PR #35 (3e tour) : le majorant de l'état courant est vérifié au début avec le build du scénario pour la place
        // libre ; un arrivant qui choisit un build plus long (plus de talents dans l'en-tête) le revérifie avant d'entrer. Plafond de
        // commandes choisi pour que le combat commence avec le build court (niveau 10, pour les deux places) et déborde avec un long.
        const string Short = "build.bastion.niveau_10", Long = "build.bastion.contrecoup";
        Assert.True(ShippedData.TryGetBuild(Short, out EdS.Data.Builds.ReferenceBuild shortBuild));
        Assert.True(ShippedData.TryGetBuild(Long, out EdS.Data.Builds.ReferenceBuild longBuild));
        CombatSetup base_ = Vallons(20_000);
        CombatSetup With(int maxCommands, EdS.Domain.Builds.CharacterBuild second, bool joinerAtEnd)
        {
            var rules = new CombatRules();
            foreach (System.Reflection.PropertyInfo property in typeof(CombatRules).GetProperties().Where(p => p.CanWrite))
                property.SetValue(rules, property.GetValue(base_.Combat));
            typeof(CombatRules).GetProperty(nameof(CombatRules.MaxCommands))!.SetValue(rules, maxCommands);
            IEnumerable<CombatantSetup> combatants = base_.Combatants.Select(c => c is CharacterSetup ch
                ? new CharacterSetup(ch.Id, ch.Team, ch.Class, ch.Id == "bastion" ? shortBuild.Build : second, ch.Initiative, ch.Placement,
                    ch.Id == "bastion2" && joinerAtEnd ? base_.DurationMs : ch.ArrivalMs)
                : c);
            return new CombatSetup(base_.Seed, base_.DurationMs, base_.Stats, base_.Damage, base_.Progression, rules, combatants, base_.Commands, base_.Data, base_.Space);
        }
        long Bound(int m, EdS.Domain.Builds.CharacterBuild second) => CombatRoom.StateBound(With(m, second, true), 50);
        int lo = 1, hi = CombatRules.AbsoluteMaxCommands;
        while (lo < hi)
        {
            int mid = lo + (hi - lo + 1) / 2;
            if (Bound(mid, shortBuild.Build) <= Protocol.MaxServerMessageBytes) lo = mid;
            else hi = mid - 1;
        }
        Assert.True(Bound(lo, longBuild.Build) > Protocol.MaxServerMessageBytes, $"plafond {lo} : le build long tient aussi");

        var game = new Game(With(lo, shortBuild.Build, false), new RoomOptions { JoinDuringCombat = true });
        NetworkClient a = game.Join("Dam", Short);
        a.SetReady(true);
        game.RunUntil(() => a.Status == ClientStatus.Combat, 5);
        game.Run(0.5);
        long tick = game.Room.Tick;
        NetworkClient b = game.Join("Léa", Long);
        game.Run(0.5);
        Assert.NotEqual(ClientStatus.Combat, b.Status);
        Assert.Contains("avec ce build", b.Error ?? "", StringComparison.Ordinal);
        Assert.Equal(ClientStatus.Combat, a.Status);
        Assert.Equal(RoomPhase.Combat, game.Room.Phase);
        Assert.False(Views(a.Combat!).TryGetValue("bastion2", out CombatantView? second) && second.IsPresent);
        Assert.True(game.Room.Tick > tick, "le combat continue");
        // Le build court, lui, entre.
        NetworkClient c = game.Join("Léa", Short);
        game.RunUntil(() => c.Status == ClientStatus.Combat, 5);
    }

    [Fact]
    public void Esquive_mesuree_selon_la_latence()
    {
        // `eds reseau --esquive` : sans latence, le joueur parfait esquive tout ; à 200 ms d'un sens, plus rien n'arrive à temps
        // (aller-retour de 400 ms pour 400 ms de marge). Le serveur juge, les clients concordent.
        JsonObject root = JsonNode.Parse(File.ReadAllText(ScenarioPath("coop/esquive_duo")))!.AsObject();
        root["data"]!["durationMs"] = 20_000;
        CombatSetup scenario = ScenarioJsonReader.Read(root.ToJsonString(), ShippedData).Setup!;
        DodgeMeasureResult fast = NetworkDodge.Run(ShippedData, scenario, 0, 0);
        DodgeMeasureResult slow = NetworkDodge.Run(ShippedData, scenario, 200, 0);
        Assert.True(fast.Attacks >= 10, $"{fast.Attacks} impacts");
        Assert.Equal(fast.Attacks, fast.Dodged);
        Assert.Equal(fast.Attacks, slow.Attacks);
        Assert.True(slow.Dodged < slow.Attacks / 2, $"{slow.Dodged} esquivés sur {slow.Attacks} à 200 ms");
        Assert.All(new[] { fast, slow }, r => Assert.True(r.HashesMatch && r.Desyncs == 0));
        var report = new StringWriter();
        NetworkDodge.Write(report, new[] { fast, slow }, "esquive_duo");
        Assert.Contains("| 0 ms | 0 ms |", report.ToString());
        Assert.Contains("| 200 ms | 0 ms |", report.ToString());
        Assert.Throws<ArgumentOutOfRangeException>(() => NetworkDodge.Run(ShippedData, scenario, -1, 0));
        Assert.Throws<ArgumentException>(() => NetworkDodge.Run(ShippedData, Lot103Calm(), 0, 0));
    }

    // --- Prédiction du déplacement ------------------------------------------------------------------------------------------

    private static (CombatEngine Engine, CombatSetup Setup) Moving(int heading = 0, bool guard = false)
    {
        CombatSetup setup = Vallons();
        var engine = new CombatEngine(setup, NullCombatLog.Instance);
        if (guard) engine.Submit(new CombatCommand(0, "bastion", CombatAction.GuardStart));
        engine.Submit(new CombatCommand(0, "bastion", MoveInput.Direction(heading)));
        engine.Step();
        return (engine, setup);
    }

    private static (double X, double Z) Meters(GroundVector p) => (p.X.Raw / 1e4, p.Z.Raw / 1e4);

    [Fact]
    public void Prediction_en_avance_puis_sur_la_saisie_puis_abandonnee()
    {
        (CombatEngine engine, CombatSetup setup) = Moving();
        CombatantView me = engine.View("bastion");
        var obstacles = CombatDisplay.Obstacles(setup.Space)!;
        var prediction = new MovementPrediction();
        (double x, double z) = Meters(me.Position!.Value);
        Assert.Equal(5.0, MovementPrediction.Speed(me, setup.Combat));
        // Le serveur marche déjà vers l'est, comme la saisie : place du tick, avancée du temps du tick et du délai (5 m/s × 0,2 s).
        Assert.Equal((x + 1.0, z), prediction.Predict(me, setup.Space!, obstacles, setup.Combat, 0, 10.0, 0.15, 0.05));
        // Délai borné à 0,5 s.
        Assert.Equal((x + 2.5, z), new MovementPrediction().Predict(me, setup.Space!, obstacles, setup.Combat, 0, 10.0, 3.0, 0));
        // Nouvelle saisie (nord) pas encore appliquée : on part de la dernière place montrée et on suit la saisie dès maintenant.
        Assert.Equal((x + 1.0, z), prediction.Predict(me, setup.Space!, obstacles, setup.Combat, 16, 10.0, 0.15, 0.05));
        Assert.Equal((x + 1.0, z + 0.5), prediction.Predict(me, setup.Space!, obstacles, setup.Combat, 16, 10.1, 0.15, 0.05));
        // Arrêt pas encore appliqué : la place montrée ne bouge plus.
        Assert.Equal((x + 1.0, z + 0.5), prediction.Predict(me, setup.Space!, obstacles, setup.Combat, null, 10.2, 0.15, 0.05));
        Assert.Equal((x + 1.0, z + 0.5), prediction.Predict(me, setup.Space!, obstacles, setup.Combat, null, 11.1, 0.15, 0.05));
        // Le serveur ne suit pas au bout d'une seconde : prédiction abandonnée, place du serveur (glissée dans le tick, sans délai).
        Assert.Equal((x + 0.25, z), prediction.Predict(me, setup.Space!, obstacles, setup.Combat, null, 11.3, 0.15, 0.05));
        // Le serveur s'arrête : il suit la saisie, sa place est montrée.
        engine.Submit(new CombatCommand(50, "bastion", CombatAction.Stop));
        engine.Step();
        CombatantView stopped = engine.View("bastion");
        Assert.Null(stopped.MoveDirection);
        Assert.Equal(Meters(stopped.Position!.Value), prediction.Predict(stopped, setup.Space!, obstacles, setup.Combat, null, 11.4, 0.15, 0.05));
    }

    [Fact]
    public void Prediction_arretee_par_le_bord_et_le_decor_ralentie_en_garde()
    {
        (CombatEngine engine, CombatSetup setup) = Moving(32, guard: true);
        CombatantView me = engine.View("bastion");
        var obstacles = CombatDisplay.Obstacles(setup.Space)!;
        long r = me.Radius!.Value.Raw;
        Assert.Equal(2.5, MovementPrediction.Speed(me, setup.Combat));   // en garde : moitié
        // Bord ouest de l'aire (x = −24, rayon 0,5 m).
        Assert.Equal((-23.5, -12.0), MovementPrediction.Walk(-18, -12, MoveInput.Direction(32), 10, r, setup.Space!, obstacles));
        // Tronc de l'arbre 3 (x de −9,4 à −8,6) : arrêt juste avant.
        (double X, double Z) tree = MovementPrediction.Walk(-12, -6, MoveInput.Direction(0), 10, r, setup.Space!, obstacles);
        Assert.InRange(tree.X, -9.95, -9.9);
        Assert.Equal(-6.0, tree.Z);
        // Sans place (combat sans espace) ou avec un temps non fini : refus.
        Assert.Throws<ArgumentException>(() => new MovementPrediction().Predict(new CombatEngine(Lot103Calm(), NullCombatLog.Instance).View("bastion"),
            setup.Space!, obstacles, setup.Combat, 0, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MovementPrediction().Predict(me, setup.Space!, obstacles, setup.Combat, 0, double.NaN, 0, 0));
    }

    private static CombatSetup Lot103Calm() => ScenarioJsonReader.ReadFile(ScenarioPath("coop/duo_meute"), ShippedData).Setup!;

    [Fact]
    public void Prediction_en_reseau_part_tout_de_suite_et_s_arrete_ou_le_serveur_s_arrete()
    {
        var game = new Game(Vallons(20_000));
        var link = new LinkProfile(100, 0);
        (NetworkClient a, _) = game.Start(link, link);
        game.Script = client => client.Combat?.PredictedPosition();   // l'affichage la demande à chaque image
        game.Run(2.5);   // temps aller-retour mesuré
        NetworkCombat combat = a.Combat!;
        Assert.InRange(a.SmoothedRttMs!.Value, 190, 260);
        (double X, double Z) start = combat.PredictedPosition()!.Value;
        GroundVector server = combat.Player().View.Position!.Value;
        Assert.Equal(Meters(server), start);

        combat.SetMove(1, 0);
        game.Run(0.08);
        // Le serveur n'a encore rien reçu (100 ms d'un sens) ; la place prédite, elle, est déjà partie.
        Assert.Equal(server, combat.Player().View.Position);
        Assert.True(combat.PredictedPosition()!.Value.X - start.X >= 0.3, $"{combat.PredictedPosition()} depuis {start}");
        game.Run(1);
        (double X, double Z) walking = combat.PredictedPosition()!.Value;
        double shown = Meters(combat.Player().View.Position!.Value).X;
        Assert.InRange(walking.X - shown, 1.0, 1.6);   // en avance d'environ 5 m/s × (aller-retour + un tick)

        combat.SetMove(0, 0);
        (double X, double Z) atStop = combat.PredictedPosition()!.Value;
        game.Run(0.1);
        Assert.Equal(atStop, combat.PredictedPosition()!.Value);   // arrêt pas encore appliqué : la place montrée ne bouge plus
        game.Run(1.5);
        (double X, double Z) final = combat.PredictedPosition()!.Value;
        Assert.Equal(Meters(combat.Player().View.Position!.Value), final);   // le serveur est arrêté : sa place
        Assert.InRange(Math.Abs(final.X - atStop.X), 0, 0.5);   // là où l'affichage l'avait arrêté, à 50 cm près
        Assert.Equal(0, combat.Desyncs);
    }
}
