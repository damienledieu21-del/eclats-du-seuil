using System.Net;
using System.Reflection;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Net;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 10.2 : salle de combat autoritaire (salle d'attente, combat, intentions, refus, débit, déconnexion et reconnexion, fin,
/// journal rejouable), sur le réseau simulé et en TCP réel.
/// </summary>
[Trait("Category", "Network")]
public class Lot102ServerTests
{
    private const string Rempart = "build.bastion.rempart";
    private const string Gardien = "build.bastion.gardien";

    private static CombatSetup Duo => ScenarioJsonReader.ReadFile(ScenarioPath("coop/duo_meute"), ShippedData).Setup!;

    /// <summary>Les deux Bastions du duo face à un mannequin qui ne frappe pas : le combat dure (tests de présence).</summary>
    private static CombatSetup Calm
    {
        get
        {
            var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(ScenarioPath("coop/duo_meute")))!.AsObject();
            var combatants = root["data"]!["combatants"]!.AsArray();
            var players = combatants.Where(c => (string?)c!["type"] == "character").Select(c => c!.DeepClone()).ToList();
            combatants.Clear();
            foreach (var player in players) combatants.Add(player);
            combatants.Add(System.Text.Json.Nodes.JsonNode.Parse("{\"id\":\"mannequin\",\"type\":\"dummy\",\"team\":\"cibles\",\"initiative\":0,\"armor\":0,\"magicResistance\":0}"));
            ScenarioLoadResult result = ScenarioJsonReader.Read(root.ToJsonString(), ShippedData);
            Assert.True(result.Setup != null, result.Report.ToString());
            return result.Setup!;
        }
    }

    private static CharacterBuild BuildOf(string id)
    {
        Assert.True(ShippedData.TryGetBuild(id, out EdS.Data.Builds.ReferenceBuild build));
        return build.Build;
    }

    /// <summary>Joueur scripté : envoie des messages et range tous ceux qu'il reçoit.</summary>
    private sealed class Bot
    {
        public Bot(SimulatedConnection connection) => Connection = connection;
        public SimulatedConnection Connection { get; }
        public List<NetMessage> Received { get; } = new();

        public void Send(NetMessage message) => Assert.True(Connection.Send(message));

        public void Pump()
        {
            while (Connection.TryReceive(MessageDirection.ServerToClient, out NetMessage? message, out string? error))
            {
                Assert.True(message != null, error);
                Received.Add(message!);
            }
        }

        public IEnumerable<T> All<T>() => Received.OfType<T>();
        public T Last<T>() => Received.OfType<T>().Last();
        public WelcomeMessage Welcome => Last<WelcomeMessage>();

        public void Input(bool guard = false, bool parry = false, int? slot = null, string? target = null, string? ally = null) =>
            Send(new InputMessage(guard, parry, slot, target, ally));
    }

    /// <summary>Réseau simulé, salle et joueurs, avancés ensemble par pas de 10 ms.</summary>
    private sealed class World
    {
        private int _tokens;

        public World(CombatSetup? scenario = null, RoomOptions? options = null, ulong seed = 1)
        {
            Net = new SimulatedNetwork(seed);
            Listener = Net.Listen("salle");
            options ??= new RoomOptions();
            options.TokenSource ??= () => (++_tokens).ToString("x32");
            Room = new CombatRoom(ShippedData, scenario ?? Duo, options);
        }

        public SimulatedNetwork Net { get; }
        public SimulatedListener Listener { get; }
        public CombatRoom Room { get; }
        public List<Bot> Bots { get; } = new();

        public Bot Connect(LinkProfile? link = null)
        {
            var bot = new Bot(Net.Connect("salle", link ?? LinkProfile.Perfect));
            Bots.Add(bot);
            return bot;
        }

        public Bot Join(string name, string build = Rempart, IReadOnlyDictionary<string, int>? talents = null, LinkProfile? link = null)
        {
            Bot bot = Connect(link);
            bot.Send(new HelloMessage(Protocol.Version, name, new BuildChoice(build, talents)));
            Run(0.05);
            return bot;
        }

        /// <summary>Livre ce qui est déjà parti (latence nulle) sans avancer la salle.</summary>
        public void Flush()
        {
            Net.Advance(0);
            foreach (Bot bot in Bots) bot.Pump();
        }

        public void Run(double seconds, double step = 0.01)
        {
            for (double t = 0; t < seconds - 1e-9; t += step)
            {
                Net.Advance(step);
                while (Listener.TryAccept(out INetConnection connection)) Room.Accept(connection);
                Room.Advance(step);
                foreach (Bot bot in Bots) bot.Pump();
            }
        }

        /// <summary>Deux joueurs présentés et prêts : le combat commence.</summary>
        public (Bot A, Bot B) Start(IReadOnlyDictionary<string, int>? talentsB = null)
        {
            Bot a = Join("Dam");
            Bot b = Join("Léa", Gardien, talentsB);
            a.Send(new ReadyMessage(true));
            b.Send(new ReadyMessage(true));
            Run(0.05);
            Assert.Equal(RoomPhase.Combat, Room.Phase);
            return (a, b);
        }
    }

    /// <summary>Client minimal : rejoue l'en-tête et les commandes reçues avec son propre moteur, et rend l'empreinte (logique du 10.3).</summary>
    private sealed class Replica
    {
        private readonly CombatEngine _engine;
        private readonly HashingCombatLog _log = new();

        public Replica(string header)
        {
            CombatSetup? setup = CombatLogReplayer.ReadSetup(header, ShippedData, out IReadOnlyList<string> problems);
            Assert.True(setup != null, string.Join(" ; ", problems));
            Setup = setup!;
            _engine = new CombatEngine(Setup, _log);
        }

        /// <summary>Sous-lot 11.7 : réplique qui reprend à un état courant, sans rien rejouer.</summary>
        public Replica(StateMessage state)
        {
            CombatSetup? setup = CombatLogReplayer.ReadSetup(state.Header, ShippedData, out IReadOnlyList<string> problems);
            Assert.True(setup != null, string.Join(" ; ", problems));
            Setup = setup!;
            _log = new HashingCombatLog(state.Hash);
            _engine = CombatEngine.Restore(Setup, CombatSnapshot.Parse(state.Snapshot), _log);
        }

        public CombatSetup Setup { get; }
        public long Tick => _engine.CurrentTick;
        public string Hash => _log.Hash;

        public void Apply(TickCommands tick)
        {
            Assert.Equal(_engine.CurrentTick, tick.Tick);
            if (tick.Commands.Count > 0) _engine.SubmitAll(tick.Commands);
            _engine.Step();
        }

    }

    private static void AssertTicksReplayed(Bot bot)
    {
        var replica = new Replica(bot.Last<StartMessage>().Header);
        foreach (TickMessage tick in bot.All<TickMessage>())
        {
            replica.Apply(tick.Commands);
            Assert.Equal(tick.Hash, replica.Hash);
        }
    }

    private static IEnumerable<CombatCommand> Commands(Bot bot, string actor) =>
        bot.All<TickMessage>().SelectMany(t => t.Commands.Commands).Where(c => c.Actor == actor);

    // --- Salle d'attente ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Salle_d_attente_places_builds_et_salle_pleine()
    {
        var world = new World();
        Bot a = world.Join("Dam");
        Assert.Equal(("bastion", 50, Protocol.Version), (a.Welcome.Seat, a.Welcome.TickMs, a.Welcome.ProtocolVersion));
        LobbyMessage lobby = a.Last<LobbyMessage>();
        Assert.Equal(RoomPhase.Lobby, lobby.Phase);
        Assert.Equal(("bastion", "Dam", true, false, "Rempart"), (lobby.Seats[0].Id, lobby.Seats[0].Player, lobby.Seats[0].Connected, lobby.Seats[0].Ready, lobby.Seats[0].Build));
        Assert.Equal(("bastion2", (string?)null, false), (lobby.Seats[1].Id, lobby.Seats[1].Player, lobby.Seats[1].Connected));

        // Build composé : celui du Rempart avec un point de moins en Riposte précise.
        var talents = new Dictionary<string, int>(BuildOf(Rempart).TalentRanks) { ["talent.bastion.contrecoup.riposte_precise"] = 1 };
        Bot b = world.Join("Léa", Rempart, talents);
        Assert.Equal("bastion2", b.Welcome.Seat);
        Assert.NotEqual(a.Welcome.Token, b.Welcome.Token);
        Assert.Equal("Personnalisé (Rempart)", a.Last<LobbyMessage>().Seats[1].Build);

        Bot c = world.Join("Tom");
        Assert.Equal("salle pleine.", c.Last<ErrorMessage>().Reason);
        Assert.False(c.Connection.IsOpen);
        Assert.Equal(2, world.Room.ConnectionCount);
    }

    [Theory]
    [InlineData(1, Rempart, "protocole 1 non pris en charge")]   // sous-lot 11.7 : le protocole 1 (lot 10) n'est plus servi
    [InlineData(2, Rempart, "protocole 2 non pris en charge")]   // sous-lot 11.9 : protocole 3 (marche à reculons)
    [InlineData(4, Rempart, "protocole 4 non pris en charge")]
    [InlineData(3, "build.bastion.inconnu", "build de référence inconnu")]
    public void Presentation_refusee(int protocol, string build, string expected)
    {
        var world = new World();
        Bot bot = world.Connect();
        bot.Send(new HelloMessage(protocol, "Dam", new BuildChoice(build)));
        world.Run(0.05);
        Assert.Contains(expected, bot.Last<ErrorMessage>().Reason);
        Assert.False(bot.Connection.IsOpen);
        Assert.Null(world.Room.SeatIds.Select(_ => (string?)null).FirstOrDefault(x => x != null));
    }

    [Fact]
    public void Build_invalide_refuse_build_valide_remet_a_zero_le_pret()
    {
        var world = new World();
        // Trop de points : 30 au niveau 30 (29 disponibles).
        var tooMany = new Dictionary<string, int>(BuildOf(Rempart).TalentRanks) { ["talent.bastion.contrecoup.main_lourde"] = 1 };
        Bot refused = world.Connect();
        refused.Send(new HelloMessage(Protocol.Version, "Dam", new BuildChoice(Rempart, tooMany)));
        world.Run(0.05);
        Assert.Contains("build refusé", refused.Last<ErrorMessage>().Reason);
        Assert.Contains("TooManyPoints", refused.Last<ErrorMessage>().Reason);

        Bot a = world.Join("Dam");
        a.Send(new ReadyMessage(true));
        world.Run(0.05);
        Assert.True(a.Last<LobbyMessage>().Seats[0].Ready);
        a.Send(new BuildMessage(new BuildChoice(Rempart, tooMany)));
        world.Run(0.05);
        Assert.Contains("TooManyPoints", a.Last<RejectedMessage>().Reason);
        Assert.True(a.Last<LobbyMessage>().Seats[0].Ready);
        a.Send(new BuildMessage(new BuildChoice(Gardien)));
        world.Run(0.05);
        Assert.Equal(("Gardien", false), (a.Last<LobbyMessage>().Seats[0].Build, a.Last<LobbyMessage>().Seats[0].Ready));
    }

    [Fact]
    public void Message_avant_presentation_silence_et_inondation_refuses()
    {
        var world = new World();
        Bot early = world.Connect();
        early.Send(new ReadyMessage(true));
        Bot silent = world.Connect();
        Bot pinger = world.Connect();
        pinger.Send(new PingMessage(42));
        world.Run(0.05);
        Assert.Equal("présentation attendue (hello ou reconnect).", early.Last<ErrorMessage>().Reason);
        Assert.Equal(42, pinger.Last<PongMessage>().ClientTime);
        Assert.True(silent.Connection.IsOpen);
        world.Run(5);
        Assert.Contains("présentation attendue (hello ou reconnect) dans les 5 s", silent.Last<ErrorMessage>().Reason);
        Assert.False(silent.Connection.IsOpen);

        // Inondation : 300 messages d'un coup (rafale permise : 120).
        Bot flood = world.Join("Dam");
        for (int i = 0; i < 300; i++) flood.Send(new PingMessage(i));
        world.Run(0.05);
        Assert.Contains("trop de messages : au plus 60 par seconde", flood.Last<ErrorMessage>().Reason);
        Assert.False(flood.Connection.IsOpen);
        // La place est libérée (salle d'attente).
        Assert.Null(world.Join("Léa").Received.OfType<ErrorMessage>().FirstOrDefault());

        // Message illisible ou de l'autre sens : connexion fermée par la lecture stricte.
        Bot liar = world.Connect();
        liar.Connection.Send(MessageCodec.Encode(new WelcomeMessage(1, "bastion", "0123456789abcdef0123456789abcdef", 50)));
        world.Run(0.05);
        Assert.False(liar.Connection.IsOpen);
    }

    // --- Combat ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Combat_commence_quand_tous_sont_prets_et_chaque_tick_se_rejoue_chez_le_client()
    {
        var world = new World();
        var talents = new Dictionary<string, int>(BuildOf(Gardien).TalentRanks) { ["talent.bastion.contrecoup.riposte_precise"] = 0 };
        talents.Remove("talent.bastion.contrecoup.riposte_precise");
        (Bot a, Bot b) = world.Start(talents);
        Assert.Equal(a.Last<StartMessage>().Header, b.Last<StartMessage>().Header);
        var replica = new Replica(a.Last<StartMessage>().Header);
        CharacterBuild played = replica.Setup.Combatants.OfType<CharacterSetup>().Single(c => c.Id == "bastion2").Build;
        Assert.False(played.TalentRanks.ContainsKey("talent.bastion.contrecoup.riposte_precise"));
        Assert.Equal(RoomPhase.Combat, a.Last<LobbyMessage>().Phase);

        world.Run(1);
        long[] ticks = a.All<TickMessage>().Select(t => t.Commands.Tick).ToArray();
        Assert.True(ticks.Length >= 19, $"{ticks.Length} ticks");
        Assert.Equal(Enumerable.Range(0, ticks.Length).Select(i => (long)i), ticks);
        Assert.Equal(a.All<TickMessage>().Select(t => t.Hash), b.All<TickMessage>().Select(t => t.Hash));
        AssertTicksReplayed(a);
        world.Flush();
        Assert.Equal(world.Room.Hash, a.Last<TickMessage>().Hash);
    }

    [Fact]
    public void Intentions_rangees_au_tick_garde_competences_cibles_et_parade()
    {
        var world = new World();
        (Bot a, Bot b) = world.Start();
        world.Run(0.2);

        a.Input(guard: true);
        world.Run(0.1);
        Assert.Contains(Commands(a, "bastion"), c => c.Action == CombatAction.GuardStart);

        // Compétence en garde : quitter la garde et lancer au même tick, sur la cible choisie.
        a.Input(guard: true, slot: 0, target: "loup_b");
        world.Run(0.1);
        CombatCommand[] cast = a.All<TickMessage>().Select(t => t.Commands.Commands.Where(c => c.Actor == "bastion").ToArray())
            .First(c => c.Any(x => x.Action == CombatAction.UseSkill));
        Assert.Equal(new[] { CombatAction.GuardStop, CombatAction.UseSkill }, cast.Select(c => c.Action));
        Assert.Equal(("skill.bastion.frappe_du_rempart", "loup_b"), (cast[1].Skill, cast[1].Target));

        // Cible inconnue : le serveur prend le premier ennemi vivant.
        world.Run(1.2);   // recharge globale
        b.Input(slot: 2, target: "personne");
        world.Run(0.1);
        CombatCommand frappe = Commands(b, "bastion2").Last(c => c.Action == CombatAction.UseSkill);
        Assert.Equal(("skill.bastion.frappe_du_rempart", "loup_a"), (frappe.Skill, frappe.Target));

        // Soutien d'un allié (Barrière d'acier, emplacement 1 du Gardien) : l'autre joueur par défaut, soi-même si demandé.
        world.Run(1.2);
        b.Input(slot: 1);
        world.Run(0.1);
        CombatCommand barrier = Commands(b, "bastion2").Last(c => c.Action == CombatAction.UseSkill);
        Assert.Equal(("skill.bastion.barriere_d_acier", "bastion"), (barrier.Skill, barrier.Target));

        a.Input(parry: true);
        world.Run(0.1);
        Assert.Contains(Commands(a, "bastion"), c => c.Action == CombatAction.Parry);
        AssertTicksReplayed(a);
        AssertTicksReplayed(b);
    }

    /// <summary>Les règles du scénario avec un plafond de commandes par acteur et par tick changé.</summary>
    [Fact]
    public void Competence_indisponible_refusee_avant_la_planification_la_garde_reste()
    {
        // Revue Codex de la PR #19 : comme en solo, une compétence visiblement indisponible ne produit aucune commande et ne fait pas
        // quitter la garde ; le joueur reçoit un refus motivé.
        var world = new World(Calm);
        (Bot a, _) = world.Start();
        a.Input(guard: true);
        world.Run(0.2);
        a.Input(guard: true, slot: 0);
        world.Run(0.2);
        Assert.Contains(Commands(a, "bastion"), c => c.Action == CombatAction.UseSkill && c.Skill == "skill.bastion.frappe_du_rempart");
        int before = Commands(a, "bastion").Count();
        int rejected = a.All<RejectedMessage>().Count();

        // Juste après : en recharge. Ultime, une fois la recharge globale passée : Résonance insuffisante.
        a.Input(guard: true, slot: 0);
        world.Run(1.2);
        a.Input(guard: true, slot: RealtimeCombatUltimate);
        world.Run(0.1);
        Assert.Equal(before, Commands(a, "bastion").Count());   // aucune commande : ni lancer, ni garde quittée
        string[] reasons = a.All<RejectedMessage>().Skip(rejected).Select(r => r.Reason).ToArray();
        Assert.True(reasons.Length == 2, string.Join(" | ", reasons));
        Assert.True(reasons[0].StartsWith("Frappe du rempart : en recharge", StringComparison.Ordinal), reasons[0]);
        Assert.True(reasons[1].Contains(": Résonance ", StringComparison.Ordinal) && reasons[1].EndsWith("/100", StringComparison.Ordinal), reasons[1]);
        Assert.True(a.Connection.IsOpen);
        AssertTicksReplayed(a);
    }

    private const int RealtimeCombatUltimate = EdS.Runtime.RealtimeCombat.UltimateSlot;

    private static CombatSetup WithPerTickLimit(CombatSetup setup, int perTick)
    {
        var rules = new CombatRules();
        foreach (PropertyInfo property in typeof(CombatRules).GetProperties().Where(p => p.CanWrite))
            property.SetValue(rules, property.GetValue(setup.Combat));
        typeof(CombatRules).GetProperty(nameof(CombatRules.MaxCommandsPerActorPerTick))!.SetValue(rules, perTick);
        return new CombatSetup(setup.Seed, setup.DurationMs, setup.Stats, setup.Damage, setup.Progression, rules, setup.Combatants, setup.Commands, setup.Data);
    }

    [Fact]
    public void Groupe_refuse_a_l_admission_signale_au_joueur_et_rien_soumis()
    {
        var world = new World(WithPerTickLimit(Duo, 1));
        (Bot a, _) = world.Start();
        a.Input(guard: true);
        world.Run(0.1);
        int before = Commands(a, "bastion").Count();
        a.Input(guard: true, slot: 0);
        world.Run(0.1);
        Assert.StartsWith("Plus de 1 commandes de « bastion »", a.Last<RejectedMessage>().Reason);
        Assert.Equal(before, Commands(a, "bastion").Count());
        AssertTicksReplayed(a);
    }

    [Fact]
    public void Rejoindre_pendant_un_combat_refuse_mais_ping_repondu()
    {
        var world = new World();
        (Bot a, _) = world.Start();
        Bot late = world.Join("Tom");
        Assert.Contains("combat en cours", late.Last<ErrorMessage>().Reason);
        a.Send(new PingMessage(7));
        world.Run(0.05);
        Assert.Equal(7, a.Last<PongMessage>().ClientTime);
    }

    // --- Déconnexion et reconnexion ----------------------------------------------------------------------------------------

    [Fact]
    public void Deconnexion_combat_continue_garde_relachee_puis_retour_avec_le_jeton()
    {
        var world = new World();
        (Bot a, Bot b) = world.Start();
        a.Input(guard: true);
        world.Run(0.5);
        string token = a.Welcome.Token;
        world.Net.Cut(a.Connection);
        int ticksBefore = b.All<TickMessage>().Count();
        world.Run(0.5);

        // Le combat continue pour l'autre joueur ; le personnage absent quitte la garde au tick suivant.
        Assert.True(b.All<TickMessage>().Count() > ticksBefore + 5);
        Assert.Contains(Commands(b, "bastion"), c => c.Action == CombatAction.GuardStop);
        Assert.False(b.Last<LobbyMessage>().Seats[0].Connected);
        Assert.Equal("Dam", b.Last<LobbyMessage>().Seats[0].Player);

        // Mauvais jeton refusé ; le bon rend la place et l'état courant (sous-lot 11.7 : au lot 10, tout l'historique).
        Bot intruder = world.Connect();
        intruder.Send(new ReconnectMessage(Protocol.Version, "ffffffffffffffffffffffffffffffff"));
        world.Run(0.05);
        Assert.Equal("jeton inconnu ou expiré.", intruder.Last<ErrorMessage>().Reason);

        Bot back = world.Connect();
        back.Send(new ReconnectMessage(Protocol.Version, token));
        world.Run(0.05);
        Assert.Equal(("bastion", token), (back.Welcome.Seat, back.Welcome.Token));
        StateMessage state = back.Last<StateMessage>();
        // Revue Codex de la PR #19 : le majorant calculé avant le combat couvre bien un vrai en-tête (l'instantané est contrôlé à l'envoi).
        Assert.True(MessageCodec.Encode(new StateMessage(state.Header, "-", state.Hash)).Length <= CombatRoom.StateBound(new Replica(state).Setup, 50));
        var replica = new Replica(state);
        Assert.Equal(state.Hash, replica.Hash);
        long resumed = replica.Tick;
        // Puis les ticks suivants se rejouent à la suite.
        int seen = back.All<TickMessage>().Count(t => t.Commands.Tick >= resumed);
        world.Run(0.5);
        foreach (TickMessage tick in back.All<TickMessage>().Where(t => t.Commands.Tick >= resumed))
        {
            replica.Apply(tick.Commands);
            Assert.Equal(tick.Hash, replica.Hash);
        }
        Assert.True(back.All<TickMessage>().Count() > seen);
        Assert.True(b.Last<LobbyMessage>().Seats[0].Connected);

        // Une demande d'état courant est servie, une seconde trop rapprochée est refusée.
        back.Send(new ResyncRequestMessage());
        back.Send(new ResyncRequestMessage());
        world.Run(0.05);
        Assert.Contains("état courant déjà demandé", back.Last<RejectedMessage>().Reason);
    }

    [Fact]
    public void Absence_de_plus_de_30_secondes_place_abandonnee_puis_combat_arrete_quand_tous_sont_partis()
    {
        var world = new World(Calm, new RoomOptions { DurationMs = 120_000 });
        (Bot a, Bot b) = world.Start();
        string token = a.Welcome.Token;
        world.Net.Cut(a.Connection);
        world.Run(31);
        Assert.Equal(RoomPhase.Combat, world.Room.Phase);
        Bot back = world.Connect();
        back.Send(new ReconnectMessage(Protocol.Version, token));
        world.Run(0.05);
        Assert.Equal("jeton inconnu ou expiré.", back.Last<ErrorMessage>().Reason);

        // Le dernier joueur s'en va : plus personne, le combat s'arrête et la salle se vide.
        b.Send(new ByeMessage());
        world.Run(0.1);
        Assert.Equal(RoomPhase.Ended, world.Room.Phase);
        Assert.Equal(0, world.Room.ConnectionCount);
        Bot next = world.Join("Tom");
        Assert.Equal("bastion", next.Welcome.Seat);
    }

    // --- Fin et nouvelle partie --------------------------------------------------------------------------------------------

    [Fact]
    public void Build_du_siege_garde_l_arrivee_du_personnage()
    {
        // Revue Codex de la PR #32 (20e tour) : le build choisi pour un siège remplace celui du scénario sans effacer l'instant d'arrivée
        // du personnage (le renfort du scénario livré espace_renfort arrive à 3 s).
        var world = new World(ScenarioJsonReader.ReadFile(ScenarioPath("espace_renfort"), ShippedData).Setup!);
        (Bot a, _) = world.Start();
        System.Text.Json.JsonElement combatants = System.Text.Json.JsonDocument.Parse(a.Last<StartMessage>().Header).RootElement
            .GetProperty("scenario").GetProperty("combatants");
        Assert.Equal(("renfort", 3000L), (combatants[1].GetProperty("id").GetString(), combatants[1].GetProperty("arrivalMs").GetInt64()));
    }

    [Fact]
    public void Fin_du_combat_journal_rejouable_puis_nouvelle_partie()
    {
        var world = new World(options: new RoomOptions { DurationMs = 3000 });
        IReadOnlyList<string>? ended = null;
        world.Room.CombatEnded = journal => ended = journal;
        (Bot a, Bot b) = world.Start();
        a.Input(guard: true);
        world.Run(1);
        b.Input(slot: 0);
        world.Run(2.5);

        EndMessage end = a.Last<EndMessage>();
        Assert.Equal(("durée atteinte.", 59L), (end.Reason, end.Tick));   // dernier tick simulé : 3 s = ticks 0 à 59
        Assert.Equal(RoomPhase.Ended, a.Last<LobbyMessage>().Phase);
        Assert.False(a.Last<LobbyMessage>().Seats[0].Ready);
        Assert.Same(world.Room.LastJournal, ended);
        Assert.NotNull(ended);

        // Journal du serveur : rejoué octet par octet ; ses lignes de combat sont celles que les clients ont vérifiées.
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(ended!.ToList(), ShippedData).Status);
        string live = HashingCombatLog.Of(new[] { a.Last<StartMessage>().Header }.Concat(ended!.Skip(1)));
        Assert.Equal(a.Last<TickMessage>().Hash, live);
        Assert.Contains("\"commands\":[{", ended[0], StringComparison.Ordinal);   // l'en-tête du journal contient les commandes jouées

        // Nouvelle partie quand tous sont de nouveau prêts : nouveau combat depuis le tick 0.
        int starts = a.All<StartMessage>().Count();
        a.Send(new ReadyMessage(true));
        b.Send(new ReadyMessage(true));
        world.Run(0.2);
        Assert.Equal(starts + 1, a.All<StartMessage>().Count());
        Assert.Equal(0, a.All<TickMessage>().Last(t => t.Commands.Tick == 0).Commands.Tick);
    }

    [Fact]
    public void Journaux_jamais_ecrases_serveur_relance_sur_le_meme_dossier()
    {
        // Revue Codex de la PR #19 : un serveur relancé sur le même dossier --journaux continue la numérotation au lieu d'écraser.
        string directory = Path.Combine(Path.GetTempPath(), "eds-journaux-" + Guid.NewGuid().ToString("N"));
        try
        {
            IReadOnlyList<string> Play()
            {
                var world = new World(options: new RoomOptions { DurationMs = 1000 });
                (Bot a, _) = world.Start();
                a.Input(guard: true);
                world.Run(1.2);
                Assert.NotNull(world.Room.LastJournal);
                return world.Room.LastJournal!;
            }
            IReadOnlyList<string> first = Play(), second = Play();
            var run1 = new JournalFolder(directory);
            Assert.Equal(Path.Combine(run1.Directory, "combat-001.jsonl"), run1.Write(first));
            // Relance : nouveau dossier ouvert sur le même chemin, nouveaux combats numérotés à la suite.
            var run2 = new JournalFolder(directory);
            Assert.Equal(Path.Combine(run2.Directory, "combat-002.jsonl"), run2.Write(second));
            Assert.Equal(string.Concat(first.Select(l => l + "\n")), File.ReadAllText(Path.Combine(directory, "combat-001.jsonl")));
            Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(File.ReadAllLines(Path.Combine(directory, "combat-002.jsonl")).ToList(), ShippedData).Status);

            // Numéro le plus grand repris (trous compris), fichiers étrangers ignorés, fichier existant jamais remplacé.
            File.WriteAllText(Path.Combine(directory, "combat-010.jsonl"), "ancien");
            File.WriteAllText(Path.Combine(directory, "combat-abc.jsonl"), "autre");
            File.WriteAllText(Path.Combine(directory, "notes.jsonl"), "autre");
            Assert.EndsWith("combat-011.jsonl", run1.Write(first));
            Assert.Equal("ancien", File.ReadAllText(Path.Combine(directory, "combat-010.jsonl")));
            Assert.Equal(6, Directory.GetFiles(directory).Length);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Hote_arrete_ferme_les_connexions_des_joueurs()
    {
        // Revue Codex de la PR #19 : un hôte libéré ferme aussi les connexions déjà prises par la salle, avec la raison.
        var room = new CombatRoom(ShippedData, Calm);
        var listener = new TcpNetListener(IPAddress.Loopback, 0);
        var host = new RoomHost(room, listener);
        host.Start();
        using TcpNetConnection a = await TcpNetConnection.ConnectAsync("127.0.0.1", listener.Port, Protocol.MaxServerMessageBytes, Protocol.MaxClientMessageBytes);
        a.Send(new HelloMessage(Protocol.Version, "Dam", new BuildChoice(Rempart)));
        var received = new List<NetMessage>();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!received.OfType<WelcomeMessage>().Any())
        {
            Assert.True(watch.Elapsed.TotalSeconds < 10, "accueil attendu");
            while (a.TryReceive(MessageDirection.ServerToClient, out NetMessage? m, out string? e)) received.Add(m ?? throw new InvalidOperationException(e));
            await Task.Delay(5);
        }
        Assert.Equal(1, host.Read(r => r.ConnectionCount));
        host.Dispose();
        Assert.Equal(0, host.Read(r => r.ConnectionCount));
        while (a.IsOpen)
        {
            Assert.True(watch.Elapsed.TotalSeconds < 20, "fermeture attendue");
            await Task.Delay(5);
        }
        while (a.TryReceive(MessageDirection.ServerToClient, out NetMessage? m, out _)) if (m != null) received.Add(m);
        Assert.Equal("serveur arrêté.", received.OfType<ErrorMessage>().Single().Reason);
        Assert.Equal("connexion fermée par l'autre côté.", a.CloseReason);
        host.Dispose();   // sans effet la seconde fois
    }

    [Fact]
    public void Delais_appliques_avant_les_messages_en_attente()
    {
        // Revue Codex de la PR #19 : un hello lu après l'échéance de 5 s, ou un reconnect lu après celle de 30 s, est refusé même s'il
        // attendait déjà dans la file (premier passage de la salle après une longue pause).
        var world = new World();
        Bot late = world.Connect();
        world.Run(0.02);
        late.Send(new HelloMessage(Protocol.Version, "Dam", new BuildChoice(Rempart)));
        world.Net.Advance(0);
        world.Room.Advance(5.1);
        world.Flush();
        Assert.Empty(late.All<WelcomeMessage>());
        Assert.StartsWith("présentation attendue", late.Last<ErrorMessage>().Reason);

        var game = new World(Calm, new RoomOptions { DurationMs = 120_000, HelloTimeoutSeconds = 60 });
        (Bot a, Bot b) = game.Start();
        string token = a.Welcome.Token;
        game.Net.Cut(a.Connection);
        game.Run(1);
        Bot back = game.Connect();
        game.Run(0.02);
        back.Send(new ReconnectMessage(Protocol.Version, token));
        game.Net.Advance(0);
        game.Room.Advance(30);
        game.Flush();
        Assert.Empty(back.All<WelcomeMessage>());
        Assert.Equal("jeton inconnu ou expiré.", back.Last<ErrorMessage>().Reason);
        Assert.Equal(RoomPhase.Combat, game.Room.Phase);
        _ = b;
    }

    [Fact]
    public void Etat_courant_trop_gros_pour_le_protocole_combat_refuse_avant_de_commencer()
    {
        // Revue Codex de la PR #19 (sous-lot 11.7 : état courant, au lot 10 l'historique) : avec les règles livrées, l'en-tête du combat
        // complété tient largement dans un message ; avec un plafond de commandes démesuré, le combat est refusé avant de commencer au lieu
        // de couper un joueur à son retour.
        var shipped = new World();
        (Bot a, _) = shipped.Start();
        long bound = CombatRoom.StateBound(new Replica(a.Last<StartMessage>().Header).Setup, 50);
        Assert.InRange(bound, 1, Protocol.MaxServerMessageBytes);

        var huge = new World(WithMaxCommands(Duo, CombatRules.AbsoluteMaxCommands));
        Bot x = huge.Join("Dam");
        Bot y = huge.Join("Léa", Gardien);
        x.Send(new ReadyMessage(true));
        y.Send(new ReadyMessage(true));
        huge.Run(0.1);
        Assert.Equal(RoomPhase.Lobby, huge.Room.Phase);
        Assert.Empty(x.All<StartMessage>());
        Assert.StartsWith("combat refusé : un état courant pourrait atteindre", x.Last<RejectedMessage>().Reason);
        Assert.False(x.Last<LobbyMessage>().Seats[0].Ready);
    }

    private static CombatSetup WithMaxCommands(CombatSetup setup, int maxCommands)
    {
        var rules = new CombatRules();
        foreach (PropertyInfo property in typeof(CombatRules).GetProperties().Where(p => p.CanWrite))
            property.SetValue(rules, property.GetValue(setup.Combat));
        typeof(CombatRules).GetProperty(nameof(CombatRules.MaxCommands))!.SetValue(rules, maxCommands);
        return new CombatSetup(setup.Seed, setup.DurationMs, setup.Stats, setup.Damage, setup.Progression, rules, setup.Combatants, setup.Commands, setup.Data);
    }

    [Theory]
    [InlineData("--journaux", "--local")]
    [InlineData("--journaux")]
    [InlineData("--data")]
    [InlineData("--scenario", "--port", "1")]
    [InlineData("--journaux", "")]
    public void Serveur_dedie_option_sans_valeur_refusee(params string[] args)
    {
        // Revue Codex de la PR #19 : « --journaux --local » ne doit pas prendre « --local » pour un dossier (et ouvrir tout le réseau).
        TextWriter error = Console.Error;
        try
        {
            var captured = new StringWriter();
            Console.SetError(captured);
            Assert.Equal(2, EdS.Server.Program.Main(args));
            Assert.Contains("valeur manquante après", captured.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(error);
        }
    }

    [Fact]
    public void Meme_scenario_memes_entrees_meme_journal()
    {
        IReadOnlyList<string> Play(LinkProfile link, ulong seed)
        {
            var world = new World(options: new RoomOptions { DurationMs = 2000 }, seed: seed);
            Bot a = world.Join("Dam", link: link);
            Bot b = world.Join("Léa", Gardien, link: link);
            a.Send(new ReadyMessage(true));
            b.Send(new ReadyMessage(true));
            world.Run(0.3);
            a.Input(guard: true);
            world.Run(0.6);
            b.Input(slot: 0);
            world.Run(1.5);
            Assert.Equal(RoomPhase.Ended, world.Room.Phase);
            return world.Room.LastJournal!;
        }
        Assert.Equal(Play(new LinkProfile(30, 20), 5), Play(new LinkProfile(30, 20), 5));
    }

    // --- TCP réel ----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Hote_TCP_deux_joueurs_combat_et_empreintes()
    {
        var room = new CombatRoom(ShippedData, Duo, new RoomOptions { DurationMs = 2000 });
        using var listener = new TcpNetListener(IPAddress.Loopback, 0);
        using var host = new RoomHost(room, listener);
        host.Start();
        using TcpNetConnection a = await TcpNetConnection.ConnectAsync("127.0.0.1", listener.Port, Protocol.MaxServerMessageBytes, Protocol.MaxClientMessageBytes);
        using TcpNetConnection b = await TcpNetConnection.ConnectAsync("127.0.0.1", listener.Port, Protocol.MaxServerMessageBytes, Protocol.MaxClientMessageBytes);
        a.Send(new HelloMessage(Protocol.Version, "Dam", new BuildChoice(Rempart)));
        b.Send(new HelloMessage(Protocol.Version, "Léa", new BuildChoice(Gardien)));
        var receivedA = new List<NetMessage>();
        var receivedB = new List<NetMessage>();
        void Collect(INetConnection c, List<NetMessage> into)
        {
            while (c.TryReceive(MessageDirection.ServerToClient, out NetMessage? m, out string? e))
            {
                Assert.True(m != null, e);
                into.Add(m!);
            }
        }
        var watch = System.Diagnostics.Stopwatch.StartNew();
        bool ready = false;
        while (!receivedA.OfType<EndMessage>().Any() || !receivedB.OfType<EndMessage>().Any())
        {
            Assert.True(watch.Elapsed.TotalSeconds < 20, "combat TCP trop long");
            Assert.Null(host.Error);
            Collect(a, receivedA);
            Collect(b, receivedB);
            if (!ready && receivedA.OfType<WelcomeMessage>().Any() && receivedB.OfType<WelcomeMessage>().Any())
            {
                a.Send(new ReadyMessage(true));
                b.Send(new ReadyMessage(true));
                a.Send(new InputMessage(true, false, null, null, null));
                ready = true;
            }
            await Task.Delay(5);
        }
        Assert.Equal(40, receivedA.OfType<TickMessage>().Count());
        Assert.Equal(receivedA.OfType<TickMessage>().Select(t => t.Hash), receivedB.OfType<TickMessage>().Select(t => t.Hash));
        var replica = new Replica(receivedA.OfType<StartMessage>().Single().Header);
        foreach (TickMessage tick in receivedA.OfType<TickMessage>())
        {
            replica.Apply(tick.Commands);
            Assert.Equal(tick.Hash, replica.Hash);
        }
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(host.Read(r => r.LastJournal)!.ToList(), ShippedData).Status);
    }

    [Fact]
    public void Textes_rendus_acceptables_pour_le_protocole()
    {
        Assert.Equal("a b", Protocol.Text(" a\nb "));
        Assert.Equal("?", Protocol.Text(" \t "));
        Assert.Equal("?", Protocol.Text(null));
        Assert.Equal(new string('x', 512), Protocol.Text(new string('x', 600)));
        Assert.Equal("ab 😀", Protocol.Text("ab 😀"));
        Assert.Equal("a", Protocol.Text("a\ud800"));
        string cut = Protocol.Text(new string('x', 511) + "😀", 512);
        Assert.Equal(511, cut.Length);
        Assert.True(MessageCodec.IsText(cut, 512));
    }
}
