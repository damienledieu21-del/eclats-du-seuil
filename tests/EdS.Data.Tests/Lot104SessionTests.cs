using System.Net;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Net;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 10.4 : partie en réseau vue du jeu (<see cref="NetworkSession"/> : héberger, rejoindre, retour automatique après une
/// coupure, échecs de connexion) et équipier robot (<see cref="BotPlayer"/>) ; arrêt de l'hôte qui ferme les connexions. Ce que le jeu
/// Unity appelle, testé sans Unity.
/// </summary>
[Trait("Category", "Network")]
public class Lot104SessionTests
{
    private const string Rempart = "build.bastion.rempart";
    private const string Gardien = "build.bastion.gardien";

    private static CombatSetup Duo => ScenarioJsonReader.ReadFile(ScenarioPath("coop/duo_meute"), ShippedData).Setup!;

    /// <summary>Les deux Bastions face à un mannequin qui ne frappe pas.</summary>
    private static CombatSetup Calm(long durationMs)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(ScenarioPath("coop/duo_meute")))!.AsObject();
        root["data"]!["durationMs"] = durationMs;
        var combatants = root["data"]!["combatants"]!.AsArray();
        var players = combatants.Where(c => (string?)c!["type"] == "character").Select(c => c!.DeepClone()).ToList();
        combatants.Clear();
        foreach (var player in players) combatants.Add(player);
        combatants.Add(System.Text.Json.Nodes.JsonNode.Parse("{\"id\":\"mannequin\",\"type\":\"dummy\",\"team\":\"cibles\",\"initiative\":0,\"armor\":0,\"magicResistance\":0}"));
        ScenarioLoadResult result = ScenarioJsonReader.Read(root.ToJsonString(), ShippedData);
        Assert.True(result.Setup != null, result.Report.ToString());
        return result.Setup!;
    }

    /// <summary>Salle sur un réseau simulé ; les sessions s'y connectent par une fabrique de connexions.</summary>
    private sealed class Lan
    {
        private int _tokens;

        public Lan(CombatSetup scenario, RoomOptions? options = null)
        {
            Net = new SimulatedNetwork(5);
            Listener = Net.Listen("salle");
            options ??= new RoomOptions();
            options.TokenSource ??= () => (++_tokens).ToString("x32");
            Room = new CombatRoom(ShippedData, scenario, options);
        }

        public SimulatedNetwork Net { get; }
        public SimulatedListener Listener { get; }
        public CombatRoom Room { get; }
        public List<NetworkSession> Sessions { get; } = new();
        public List<SimulatedConnection> Links { get; } = new();
        public LinkProfile Link { get; set; } = new LinkProfile(30, 10);

        /// <summary>Faux : les connexions échouent (hôte injoignable).</summary>
        public bool Reachable { get; set; } = true;

        public Task<INetConnection> Connect()
        {
            if (!Reachable) return Task.FromException<INetConnection>(new SocketLikeException("hôte injoignable"));
            SimulatedConnection connection = Net.Connect("salle", Link);
            Links.Add(connection);
            return Task.FromResult<INetConnection>(connection);
        }

        public NetworkSession Session(string name, string build, BuildChoice? bot = null)
        {
            var session = new NetworkSession(ShippedData, Connect, name, new BuildChoice(build), null, bot is null ? null : Connect, bot);
            Sessions.Add(session);
            return session;
        }

        public void Run(double seconds, double step = 0.01)
        {
            for (double t = 0; t < seconds - 1e-9; t += step)
            {
                Net.Advance(step);
                while (Listener.TryAccept(out INetConnection connection)) Room.Accept(connection);
                Room.Advance(step);
                foreach (NetworkSession session in Sessions) session.Advance(step);
            }
        }

        public void RunUntil(Func<bool> done, double limitSeconds)
        {
            for (double t = 0; t < limitSeconds && !done(); t += 0.01) Run(0.01);
            Assert.True(done(), "condition non atteinte dans le temps imparti");
        }
    }

    private sealed class SocketLikeException : Exception
    {
        public SocketLikeException(string message) : base(message) { }
    }

    private static string ServerHash(CombatRoom room, NetworkCombat combat) =>
        HashingCombatLog.Of(new[] { combat.Header }.Concat(room.LastJournal!.Skip(1)));

    private static IReadOnlyList<CombatCommand> ServerCommands(CombatRoom room) =>
        CombatLogReplayer.ReadSetup(room.LastJournal![0], ShippedData, out _)!.Commands;

    [Fact]
    public void Robot_refuse_erreur_visible_place_libre()
    {
        // Revue Codex de la PR #21 (tour 6) : un robot refusé par le serveur (build inconnu) s'arrête avec la raison, que la salle d'attente
        // d'Unity affiche (« robot arrêté : … ») ; sa place reste libre, le joueur reste dans la salle.
        var lan = new Lan(Calm(3_000));
        NetworkSession session = lan.Session("Dam", Rempart, bot: new BuildChoice("build.inconnu"));
        lan.RunUntil(() => session.Client?.Status == ClientStatus.Lobby && session.Bot?.Client.Status == ClientStatus.Failed, 3);
        Assert.Contains("build de référence inconnu", session.Bot!.Client.Error);
        Assert.Null(session.Client!.Lobby!.Seats.Single(s => s.Id == "bastion2").Player);
        Assert.Equal(ClientStatus.Lobby, session.Client!.Status);
    }

    [Fact]
    public void Build_retenu_seulement_une_fois_accepte_par_le_serveur()
    {
        // Revue Codex de la PR #21 (tour 4) : un build demandé n'est retenu qu'après l'accord du serveur ; un refus, notamment quand la
        // demande arrive au serveur après le début du combat (le client était encore en salle d'attente), ne change rien.
        var net = new SimulatedNetwork(5);
        SimulatedListener listener = net.Listen("salle");
        int tokens = 0;
        var room = new CombatRoom(ShippedData, Calm(3_000), new RoomOptions { TokenSource = () => (++tokens).ToString("x32") });
        var slow = new NetworkClient(ShippedData, net.Connect("salle", new LinkProfile(100, 0)), "Dam", new BuildChoice(Rempart));
        var fast = new NetworkClient(ShippedData, net.Connect("salle", LinkProfile.Perfect), "Léa", new BuildChoice(Gardien));
        void Run(double seconds)
        {
            for (double t = 0; t < seconds - 1e-9; t += 0.01)
            {
                net.Advance(0.01);
                while (listener.TryAccept(out INetConnection connection)) room.Accept(connection);
                room.Advance(0.01);
                slow.Advance(0.01);
                fast.Advance(0.01);
            }
        }
        Run(0.5);
        Assert.Equal((ClientStatus.Lobby, ClientStatus.Lobby), (slow.Status, fast.Status));
        Assert.Equal(Rempart, slow.AcceptedBuild.Reference);

        // Accepté : retenu à la réponse du serveur, pas avant ; une demande à la fois.
        var gardien = new BuildChoice(Gardien);
        Assert.True(slow.SetBuild(gardien));
        Assert.Same(gardien, slow.PendingBuild);
        Assert.False(slow.SetBuild(new BuildChoice(Rempart)));
        Assert.Equal(Rempart, slow.AcceptedBuild.Reference);
        Run(0.3);
        Assert.Null(slow.PendingBuild);
        Assert.Same(gardien, slow.AcceptedBuild);
        Assert.Equal("Gardien", slow.Lobby!.Seats.Single(s => s.Id == slow.SeatId).Build);

        // Refusé (build inconnu) : le build retenu ne change pas.
        Assert.True(slow.SetBuild(new BuildChoice("build.inconnu")));
        Run(0.3);
        Assert.Null(slow.PendingBuild);
        Assert.Same(gardien, slow.AcceptedBuild);
        Assert.Contains("build de référence inconnu", slow.LastRejected);

        // Course : déjà prêt, il change de build au moment où l'autre lance le combat ; le serveur reçoit la demande en combat.
        slow.SetReady(true);
        Run(0.3);
        var rempart = new BuildChoice(Rempart);
        Assert.True(slow.SetBuild(rempart));   // encore en salle d'attente de son côté
        fast.SetReady(true);
        Run(0.4);
        Assert.Equal(ClientStatus.Combat, slow.Status);
        Assert.Null(slow.PendingBuild);
        Assert.Same(gardien, slow.AcceptedBuild);
        Assert.Equal("combat en cours : le build se change en salle d'attente.", slow.LastRejected);
        Assert.False(slow.SetBuild(rempart));   // pas en combat
    }

    [Fact]
    public void Build_sans_reponse_a_la_coupure_oublie_au_retour()
    {
        // Revue Codex de la PR #21 (tour 5) : la connexion tombe après le début du combat, avant le refus du build et la réponse à sa
        // mesure ; au retour, une mesure sur la nouvelle connexion ne doit pas valider ce build.
        var net = new SimulatedNetwork(5);
        SimulatedListener listener = net.Listen("salle");
        int tokens = 0;
        var room = new CombatRoom(ShippedData, Calm(6_000), new RoomOptions { TokenSource = () => (++tokens).ToString("x32") });
        var link = new LinkProfile(100, 0);
        SimulatedConnection first = net.Connect("salle", link);
        var slow = new NetworkClient(ShippedData, first, "Dam", new BuildChoice(Rempart));
        var fast = new NetworkClient(ShippedData, net.Connect("salle", LinkProfile.Perfect), "Léa", new BuildChoice(Gardien));
        void Run(double seconds)
        {
            for (double t = 0; t < seconds - 1e-9; t += 0.01)
            {
                net.Advance(0.01);
                while (listener.TryAccept(out INetConnection connection)) room.Accept(connection);
                room.Advance(0.01);
                slow.Advance(0.01);
                fast.Advance(0.01);
            }
        }
        Run(0.5);
        slow.SetReady(true);
        Run(0.3);
        Assert.True(slow.SetBuild(new BuildChoice(Gardien)));
        fast.SetReady(true);
        for (int i = 0; i < 100 && slow.Status != ClientStatus.Combat; i++) Run(0.01);
        Assert.Equal(ClientStatus.Combat, slow.Status);
        Assert.NotNull(slow.PendingBuild);   // refus et réponse à la mesure encore en route

        net.Cut(first);
        Run(0.05);
        Assert.Equal(ClientStatus.Disconnected, slow.Status);
        Assert.Null(slow.PendingBuild);
        slow.Reconnect(net.Connect("salle", link));
        for (int i = 0; i < 300 && !(slow.Status == ClientStatus.Combat && slow.SmoothedRttMs != null); i++) Run(0.01);
        Assert.Equal(ClientStatus.Combat, slow.Status);
        Assert.NotNull(slow.SmoothedRttMs);   // une mesure a abouti sur la nouvelle connexion
        Assert.Null(slow.PendingBuild);
        Assert.Equal(Rempart, slow.AcceptedBuild.Reference);
    }

    [Fact]
    public void Joueur_et_equipier_robot_jouent_toute_la_meute()
    {
        var lan = new Lan(Duo);
        NetworkSession session = lan.Session("Dam", Rempart, new BuildChoice(Gardien));
        lan.RunUntil(() => session.Client?.Status == ClientStatus.Lobby && session.Bot?.Client.Status == ClientStatus.Lobby, 3);
        Assert.Equal("bastion", session.Client!.SeatId);
        Assert.Equal("bastion2", session.Bot!.Client.SeatId);
        // Le robot se déclare prêt de lui-même ; le combat attend le joueur.
        lan.RunUntil(() => session.Client.Lobby!.Seats[1].Ready, 2);
        Assert.Equal(RoomPhase.Lobby, lan.Room.Phase);
        session.Client.SetReady(true);
        lan.RunUntil(() => session.Client.Status == ClientStatus.Combat, 2);
        // Le joueur garde la garde levée ; le robot joue seul.
        session.Client.Combat!.SetGuard(true);
        lan.RunUntil(() => session.Client.Status == ClientStatus.Lobby, 70);

        NetworkCombat mine = session.Client.Combat, bot = session.Bot.Client.Combat!;
        Assert.Equal(ServerHash(lan.Room, mine), mine.Hash);
        Assert.Equal(ServerHash(lan.Room, bot), bot.Hash);
        Assert.Equal(0, mine.Desyncs + bot.Desyncs);
        IReadOnlyList<CombatCommand> commands = ServerCommands(lan.Room);
        Assert.Contains(commands, c => c.Actor == "bastion2" && c.Action == CombatAction.GuardStart);
        Assert.Contains(commands, c => c.Actor == "bastion2" && c.Action == CombatAction.UseSkill);
        Assert.Contains(commands, c => c.Actor == "bastion2" && c.Action == CombatAction.Parry);
        Assert.Contains(commands, c => c.Actor == "bastion2" && c.Skill == "skill.bastion.barriere_d_acier" && c.Target == "bastion");
        // Après le combat, le robot est de nouveau prêt pour le suivant.
        lan.RunUntil(() => session.Client.Lobby!.Seats[1].Ready, 2);
        Assert.Null(session.Error);
    }

    [Fact]
    public void Robot_pare_aussi_au_combat_suivant()
    {
        // Revue Codex de la PR #21 : le suivi des coups parés repart de zéro à chaque combat ; les mêmes coups (même assaillant, même
        // tick) du combat suivant sont parés aussi.
        var lan = new Lan(Duo, new RoomOptions { DurationMs = 6_000 });
        NetworkSession session = lan.Session("Dam", Rempart, new BuildChoice(Gardien));
        lan.RunUntil(() => session.Client?.Status == ClientStatus.Lobby && session.Bot?.Client.Status == ClientStatus.Lobby, 3);
        var parries = new List<int>();
        for (int round = 0; round < 2; round++)
        {
            lan.RunUntil(() => session.Client!.Lobby!.Seats.Single(s => s.Id == session.Bot!.Client.SeatId).Ready, 3);
            session.Client!.SetReady(true);
            lan.RunUntil(() => session.Client.Status == ClientStatus.Combat, 2);
            session.Client.Combat!.SetGuard(true);
            lan.RunUntil(() => session.Client.Status == ClientStatus.Lobby && session.Bot!.Client.Status == ClientStatus.Lobby, 10);
            parries.Add(ServerCommands(lan.Room).Count(c => c.Actor == session.Bot!.Client.SeatId && c.Action == CombatAction.Parry));
        }
        Assert.True(parries[0] > 0, "aucune parade au premier combat");
        Assert.Equal(parries[0], parries[1]);   // même combat, mêmes coups : autant de parades la seconde fois
    }

    [Fact]
    public void Retour_automatique_apres_une_coupure()
    {
        var lan = new Lan(Calm(30_000));
        NetworkSession dam = lan.Session("Dam", Rempart), lea = lan.Session("Léa", Gardien);
        lan.RunUntil(() => dam.Client?.Status == ClientStatus.Lobby && lea.Client?.Status == ClientStatus.Lobby, 3);
        dam.Client!.SetReady(true);
        lea.Client!.SetReady(true);
        lan.RunUntil(() => dam.Client.Status == ClientStatus.Combat, 2);
        dam.Client.Combat!.SetGuard(true);
        lan.Run(2);
        Assert.True(dam.Client.Combat.Player().View.Guarding);

        // Coupure, et l'hôte injoignable 3 s : tentatives toutes les 2 s, puis retour avec le jeton.
        lan.Reachable = false;
        lan.Net.Cut(lan.Links[0]);
        lan.Run(0.05);
        Assert.Equal(ClientStatus.Disconnected, dam.Client.Status);
        lan.Run(3);
        Assert.Equal(ClientStatus.Disconnected, dam.Client.Status);
        Assert.Null(dam.Error);
        lan.Reachable = true;
        lan.RunUntil(() => dam.Client.Status == ClientStatus.Combat, 3);
        Assert.Equal(1, dam.Client.Combat!.Resyncs);
        // Revue Codex de la PR #21 : la garde tenue avant la coupure n'est pas renvoyée au retour (le jeu relit la touche).
        Assert.False(dam.Client.Combat.GuardHeld);
        lan.Run(0.5);
        Assert.False(dam.Client.Combat.Player().View.Guarding);
        Assert.InRange(dam.Client.Combat.Tick, lea.Client.Combat!.Tick - 3, lea.Client.Combat.Tick + 3);
        lan.RunUntil(() => dam.Client.Status == ClientStatus.Lobby, 40);
        Assert.Equal(ServerHash(lan.Room, dam.Client.Combat), dam.Client.Combat.Hash);
        Assert.Equal(0, dam.Client.Combat.Desyncs);
    }

    [Fact]
    public void Retour_impossible_apres_le_delai_du_serveur()
    {
        var lan = new Lan(Calm(120_000));
        NetworkSession dam = lan.Session("Dam", Rempart), lea = lan.Session("Léa", Gardien);
        lan.RunUntil(() => dam.Client?.Status == ClientStatus.Lobby && lea.Client?.Status == ClientStatus.Lobby, 3);
        dam.Client!.SetReady(true);
        lea.Client!.SetReady(true);
        lan.RunUntil(() => dam.Client.Status == ClientStatus.Combat, 2);
        lan.Reachable = false;
        lan.Net.Cut(lan.Links[0]);
        lan.Run(31, 0.05);
        Assert.Equal(ClientStatus.Disconnected, dam.Client.Status);
        Assert.False(dam.Client.CanReconnect);
        Assert.StartsWith("retour impossible", dam.Error);
        Assert.Equal(ClientStatus.Combat, lea.Client.Status);
    }

    [Fact]
    public void Echec_et_delai_de_connexion()
    {
        var lan = new Lan(Calm(5_000)) { Reachable = false };
        NetworkSession refused = lan.Session("Dam", Rempart);
        lan.Run(0.05);
        Assert.Null(refused.Client);
        Assert.Equal("connexion impossible : hôte injoignable", refused.Error);

        // Connexion qui ne répond jamais : abandonnée après 10 s.
        var never = new TaskCompletionSource<INetConnection>();
        var hanging = new NetworkSession(ShippedData, () => never.Task, "Dam", new BuildChoice(Rempart), null);
        Assert.True(hanging.Connecting);
        for (int i = 0; i < 99; i++) hanging.Advance(0.1);
        Assert.Null(hanging.Error);
        hanging.Advance(0.2);
        Assert.Equal("connexion impossible : délai de 10 s dépassé.", hanging.Error);
        Assert.False(hanging.Connecting);
        // Si elle aboutit plus tard, la connexion est refermée.
        var late = new SimulatedNetwork(1).Connect("nulle-part", LinkProfile.Perfect);
        never.SetResult(late);
        Thread.Sleep(50);
        hanging.Dispose();
        hanging.Advance(1);   // sans effet après l'arrêt

        // Fabrique qui lève : même message, aucune exception.
        var throwing = new NetworkSession(ShippedData, () => throw new InvalidOperationException("adresse inconnue"), "Dam", new BuildChoice(Rempart), null);
        throwing.Advance(0.01);
        Assert.Equal("connexion impossible : adresse inconnue", throwing.Error);

        Assert.Throws<ArgumentOutOfRangeException>(() => NetworkSession.Join(ShippedData, "127.0.0.1", 0, "Dam", new BuildChoice(Rempart)));
        Assert.Throws<ArgumentException>(() => NetworkSession.Join(ShippedData, "  ", 7777, "Dam", new BuildChoice(Rempart)));
        Assert.Throws<ArgumentException>(() => NetworkSession.Join(ShippedData, "127.0.0.1", 7777, "", new BuildChoice(Rempart)));
        Assert.NotNull(NetworkSession.LocalAddresses());
    }

    [Fact]
    public void Heberger_avec_un_robot_en_TCP_reel_puis_arreter()
    {
        using NetworkSession session = NetworkSession.HostGame(ShippedData, Calm(1_500), "Dam", new BuildChoice(Rempart), port: 0, lan: false,
            botBuild: new BuildChoice(Gardien));
        Assert.NotNull(session.Host);
        Assert.StartsWith("127.0.0.1:", session.Address);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        double last = 0;
        bool ready = false;
        // Les deux clients (joueur et robot, chacun sa connexion) doivent avoir reçu la fin : sous charge, l'un peut la recevoir plus tard.
        bool Done(NetworkClient? c) => c?.Combat?.IsFinished == true && c.Status == ClientStatus.Lobby;
        while (watch.Elapsed.TotalSeconds < 15 && !(Done(session.Client) && Done(session.Bot?.Client)))
        {
            double now = watch.Elapsed.TotalSeconds;
            session.Advance(now - last);
            last = now;
            if (!ready && session.Client?.Status == ClientStatus.Lobby)
            {
                session.Client.SetReady(true);
                ready = true;
            }
            Thread.Sleep(5);
        }
        Assert.Null(session.Error);
        NetworkCombat combat = session.Client!.Combat!;
        Assert.True(combat.IsFinished, $"{session.Client.Status} {session.Client.Error}");
        string expected = session.Host!.Read(room => HashingCombatLog.Of(new[] { combat.Header }.Concat(room.LastJournal!.Skip(1))));
        Assert.Equal(expected, combat.Hash);
        Assert.Equal(expected, session.Bot!.Client.Combat!.Hash);
        Assert.Equal(0, combat.Desyncs + session.Bot.Client.Combat.Desyncs);
        Assert.Null(combat.EndProblem);
        session.Dispose();
        Assert.False(session.Host.IsRunning);
        Assert.Equal(ClientStatus.Left, session.Client.Status);
        session.Dispose();   // sans effet la seconde fois
    }

    [Fact]
    public async Task Arret_de_l_hote_les_joueurs_voient_la_raison()
    {
        var listener = new TcpNetListener(IPAddress.Loopback, 0);
        var host = new RoomHost(new CombatRoom(ShippedData, Calm(60_000)), listener);
        host.Start();
        TcpNetConnection connection = await TcpNetConnection.ConnectAsync("127.0.0.1", listener.Port, Protocol.MaxServerMessageBytes, Protocol.MaxClientMessageBytes);
        var client = new NetworkClient(ShippedData, connection, "Dam", new BuildChoice(Rempart));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (client.Status == ClientStatus.Connecting && watch.ElapsedMilliseconds < 10_000)
        {
            client.Advance(0.01);
            Thread.Sleep(5);
        }
        Assert.Equal(ClientStatus.Lobby, client.Status);
        host.Dispose();
        while (client.Status == ClientStatus.Lobby && watch.ElapsedMilliseconds < 10_000)
        {
            client.Advance(0.01);
            Thread.Sleep(5);
        }
        Assert.Equal(ClientStatus.Failed, client.Status);
        Assert.Equal("refusé par le serveur : serveur arrêté.", client.Error);
        host.Dispose();   // sans effet la seconde fois
        connection.Dispose();
    }
}
