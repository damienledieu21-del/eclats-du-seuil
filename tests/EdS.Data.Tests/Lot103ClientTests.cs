using System.Net;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Net;
using EdS.Runtime;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 10.3 : client réseau. Le combat local rejoue les commandes du serveur et garde son empreinte ; divergences, ticks perdus et
/// historiques faussés sont détectés et réparés ou font abandonner proprement ; reconnexion avec le jeton ; mesure du temps aller-retour ;
/// envois d'intentions regroupés sous la limite de débit du serveur. Réseau simulé (latence, gigue, coupures) et TCP réel.
/// </summary>
[Trait("Category", "Network")]
public class Lot103ClientTests
{
    internal const string Rempart = "build.bastion.rempart";
    internal const string Gardien = "build.bastion.gardien";
    private const string BarrierSkill = "skill.bastion.barriere_d_acier";

    private static CombatSetup Duo => ScenarioJsonReader.ReadFile(ScenarioPath("coop/duo_meute"), ShippedData).Setup!;

    /// <summary>
    /// Limite d'un test TCP en temps réel. Le combat dure 1,5 s de jeu : un test sain finit en quelques secondes et cette limite ne
    /// joue qu'en cas d'échec. Elle est large parce que la CI lance plusieurs assemblages de tests en parallèle sur deux cœurs (échecs
    /// observés à 15 s sous cette charge).
    /// </summary>
    private const double TcpDeadlineSeconds = 60;

    /// <summary>Les deux Bastions face à un mannequin qui ne frappe pas : le combat dure tout son temps.</summary>
    private static CombatSetup Calm(long durationMs = 20_000)
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

    /// <summary>Connexion intermédiaire qui modifie, retire ou remplace les messages du serveur avant le client (pannes provoquées).</summary>
    internal sealed class Tampering : INetConnection
    {
        private readonly INetConnection _inner;
        private readonly Queue<byte[]> _injected = new();

        public Tampering(INetConnection inner) => _inner = inner;

        /// <summary>Transforme un message reçu : le même, un autre, ou null pour le retirer.</summary>
        public Func<NetMessage, NetMessage?> Alter { get; set; } = m => m;

        public void Inject(byte[] raw) => _injected.Enqueue(raw);

        public string RemoteAddress => _inner.RemoteAddress;
        public bool IsOpen => _inner.IsOpen;
        public string? CloseReason => _inner.CloseReason;
        public bool Send(byte[] payload) => _inner.Send(payload);
        public void Close(string reason) => _inner.Close(reason);
        public void Dispose() => _inner.Dispose();

        public bool TryReceive(out byte[] payload)
        {
            if (_injected.Count > 0)
            {
                payload = _injected.Dequeue();
                return true;
            }
            while (_inner.TryReceive(out byte[] raw))
            {
                NetMessage? message = MessageCodec.Decode(raw, MessageDirection.ServerToClient, out _);
                if (message is null)
                {
                    payload = raw;
                    return true;
                }
                NetMessage? altered = Alter(message);
                if (altered is null) continue;
                payload = MessageCodec.Encode(altered);
                return true;
            }
            payload = Array.Empty<byte>();
            return false;
        }
    }

    /// <summary>Réseau simulé, salle et clients avancés ensemble par pas de temps virtuel.</summary>
    internal sealed class Game
    {
        private int _tokens;

        public Game(CombatSetup? scenario = null, RoomOptions? options = null, ulong seed = 7)
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
        public List<NetworkClient> Clients { get; } = new();

        /// <summary>Lien simulé de chaque client (pour le couper).</summary>
        public Dictionary<NetworkClient, SimulatedConnection> Links { get; } = new();

        /// <summary>Appelé pour chaque client à chaque pas (intentions scriptées).</summary>
        public Action<NetworkClient>? Script { get; set; }

        public SimulatedConnection Connect(LinkProfile? link = null) => Net.Connect("salle", link ?? LinkProfile.Perfect);

        public NetworkClient Join(string name, string build = Rempart, LinkProfile? link = null, Func<SimulatedConnection, INetConnection>? wrap = null,
            NetworkClientOptions? options = null)
        {
            SimulatedConnection connection = Connect(link);
            var client = new NetworkClient(ShippedData, wrap is null ? connection : wrap(connection), name, new BuildChoice(build), options);
            Clients.Add(client);
            Links[client] = connection;
            RunUntil(() => client.Status != ClientStatus.Connecting, 3);
            Run(0.05);
            return client;
        }

        public void Cut(NetworkClient client) => Net.Cut(Links[client]);

        public void Reconnect(NetworkClient client, LinkProfile? link = null)
        {
            SimulatedConnection connection = Connect(link);
            Links[client] = connection;
            client.Reconnect(connection);
        }

        public void Run(double seconds, double step = 0.01)
        {
            for (double t = 0; t < seconds - 1e-9; t += step)
            {
                Net.Advance(step);
                while (Listener.TryAccept(out INetConnection connection)) Room.Accept(connection);
                Room.Advance(step);
                foreach (NetworkClient client in Clients)
                {
                    Script?.Invoke(client);
                    client.Advance(step);
                }
            }
        }

        public void RunUntil(Func<bool> done, double limitSeconds, double step = 0.01)
        {
            for (double t = 0; t < limitSeconds && !done(); t += step) Run(step, step);
            Assert.True(done(), "condition non atteinte dans le temps imparti");
        }

        /// <summary>Deux clients présentés et prêts : le combat commence chez eux.</summary>
        public (NetworkClient A, NetworkClient B) Start(LinkProfile? linkA = null, LinkProfile? linkB = null,
            Func<SimulatedConnection, INetConnection>? wrapA = null, NetworkClientOptions? optionsA = null)
        {
            NetworkClient a = Join("Dam", Rempart, linkA, wrapA, optionsA);
            NetworkClient b = Join("Léa", Gardien, linkB);
            Assert.Equal((ClientStatus.Lobby, ClientStatus.Lobby), (a.Status, b.Status));
            a.SetReady(true);
            b.SetReady(true);
            RunUntil(() => a.Status == ClientStatus.Combat && b.Status == ClientStatus.Combat, 5);
            return (a, b);
        }
    }

    /// <summary>Le journal joué par le serveur, avec l'en-tête vu par le client : son empreinte est celle du client à la fin.</summary>
    internal static string ServerHash(Game game, NetworkCombat combat)
    {
        Assert.NotNull(game.Room.LastJournal);
        return HashingCombatLog.Of(new[] { combat.Header }.Concat(game.Room.LastJournal!.Skip(1)));
    }

    /// <summary>Commandes admises par le serveur pendant le dernier combat (en-tête du journal rejouable).</summary>
    internal static IReadOnlyList<CombatCommand> ServerCommands(Game game)
    {
        CombatSetup? setup = CombatLogReplayer.ReadSetup(game.Room.LastJournal![0], ShippedData, out IReadOnlyList<string> problems);
        Assert.True(setup != null, string.Join(" ; ", problems));
        return setup!.Commands;
    }

    /// <summary>Intentions variées, les mêmes à chaque essai : garde tenue par moments, compétences, parades.</summary>
    private static Action<NetworkClient> Playing(int period = 37) => client =>
    {
        if (client.Status != ClientStatus.Combat || client.Combat is null) return;
        NetworkCombat combat = client.Combat;
        long step = (long)Math.Round(client.Now * 100);
        combat.SetGuard(step / 80 % 2 == 0);
        if (step % period == 0) combat.UseSlot((int)(step / period % 5));
        if (step % 53 == 0) combat.Parry();
        if (step % 301 == 0) combat.NextTarget();
    };

    // --- Combat joué de bout en bout ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, 0)]
    [InlineData(100, 40)]
    [InlineData(200, 80)]
    public void Deux_clients_jouent_tout_le_combat_en_accord_avec_le_serveur(int latencyMs, int jitterMs)
    {
        var game = new Game(Duo, seed: (ulong)(latencyMs + jitterMs + 1)) { Script = Playing() };
        var link = new LinkProfile(latencyMs, jitterMs);
        (NetworkClient a, NetworkClient b) = game.Start(link, link);
        Assert.Equal(("bastion", "bastion2"), (a.SeatId, b.SeatId));
        game.RunUntil(() => a.Status == ClientStatus.Lobby && b.Status == ClientStatus.Lobby, 70);

        foreach (NetworkClient client in new[] { a, b })
        {
            NetworkCombat combat = client.Combat!;
            Assert.True(combat.IsFinished);
            Assert.Equal(0, combat.Desyncs);
            Assert.Equal(0, combat.Resyncs);
            Assert.Equal(ServerHash(game, combat), combat.Hash);
            Assert.Equal(game.Room.LastResult!.EndReason, combat.Result!.EndReason);
            Assert.Equal(game.Room.LastResult.EndTick, combat.Result.EndTick);
            Assert.Equal(game.Room.LastResult.Winner, combat.Result.Winner);
            Assert.NotNull(combat.EndReason);
            Assert.Null(client.Error);
        }
        // Les intentions des deux joueurs sont arrivées au serveur et ont été jouées (garde, compétences, parades).
        IReadOnlyList<CombatCommand> commands = ServerCommands(game);
        foreach (string seat in new[] { "bastion", "bastion2" })
        {
            Assert.Contains(commands, c => c.Actor == seat && c.Action == CombatAction.GuardStart);
            Assert.Contains(commands, c => c.Actor == seat && c.Action == CombatAction.UseSkill);
            Assert.Contains(commands, c => c.Actor == seat && c.Action == CombatAction.Parry);
        }
        // Le temps aller-retour mesuré suit la latence du lien (deux sens, plus le pas de 10 ms).
        Assert.InRange(a.SmoothedRttMs!.Value, 2 * latencyMs, 2 * (latencyMs + jitterMs) + 40);
    }

    [Fact]
    public void Meme_partie_meme_graine_memes_journaux()
    {
        string Play()
        {
            var game = new Game(Duo, seed: 11) { Script = Playing() };
            (NetworkClient a, _) = game.Start(new LinkProfile(80, 30), new LinkProfile(120, 10));
            game.RunUntil(() => a.Status == ClientStatus.Lobby, 70);
            return string.Join("\n", game.Room.LastJournal!) + "\n" + a.Combat!.Hash;
        }
        Assert.Equal(Play(), Play());
    }

    [Fact]
    public void Etats_du_client_salle_combat_puis_retour_en_salle()
    {
        var game = new Game(Calm(2_000));
        NetworkClient a = game.Join("Dam");
        Assert.Equal(ClientStatus.Lobby, a.Status);
        Assert.Equal("bastion", a.SeatId);
        Assert.NotNull(a.Token);
        Assert.Equal(50, a.TickMs);
        Assert.Equal(RoomPhase.Lobby, a.Lobby!.Phase);
        Assert.Null(a.Combat);

        // Build refusé (autre classe inconnue) : refus motivé, le client reste dans la salle.
        a.SetBuild(new BuildChoice("build.inconnu"));
        game.Run(0.1);
        Assert.Contains("build de référence inconnu", a.LastRejected);
        Assert.Equal(ClientStatus.Lobby, a.Status);
        a.SetBuild(new BuildChoice(Gardien));
        game.Run(0.1);
        Assert.Equal("Gardien", a.Lobby!.Seats[0].Build);

        NetworkClient b = game.Join("Léa");
        NetworkClient full = game.Join("Tom");
        Assert.Equal(ClientStatus.Failed, full.Status);
        Assert.Equal("refusé par le serveur : salle pleine.", full.Error);

        a.SetReady(true);
        b.SetReady(true);
        game.RunUntil(() => a.Status == ClientStatus.Combat, 2);
        // Le changement de build n'est envoyé qu'en salle d'attente.
        a.SetBuild(new BuildChoice(Rempart));
        game.RunUntil(() => a.Status == ClientStatus.Lobby, 5);
        Assert.Equal(RoomPhase.Ended, a.Lobby!.Phase);
        Assert.Equal("durée atteinte.", a.Combat!.EndReason);
        Assert.Null(a.Combat.EndProblem);
        Assert.Null(a.Error);
        Assert.Equal(ServerHash(game, a.Combat), a.Combat.Hash);

        // Un second combat dans la même salle : nouveau combat local.
        NetworkCombat first = a.Combat;
        a.SetReady(true);
        b.SetReady(true);
        game.RunUntil(() => a.Status == ClientStatus.Combat, 2);
        Assert.NotSame(first, a.Combat);
        Assert.InRange(a.Combat!.Tick, 0, 5);

        a.Leave();
        Assert.Equal(ClientStatus.Left, a.Status);
        game.Run(0.2);
        Assert.Equal(ClientStatus.Left, a.Status);
        Assert.Equal(0, a.Advance(0.1));
    }

    [Fact]
    public void Le_meme_ecran_sert_au_solo_et_au_reseau()
    {
        var game = new Game(Calm(5_000));
        (NetworkClient a, _) = game.Start();
        ICombatClient network = a.Combat!;
        // Le même combat (lu dans l'en-tête du serveur) joué seul, sans intention de part et d'autre.
        var solo = new RealtimeCombat(a.Combat!.Setup, "bastion");
        foreach (ICombatClient combat in new ICombatClient[] { network, solo })
        {
            Assert.Equal("bastion", combat.PlayerId);
            Assert.Equal("mannequin", combat.TargetId);
            Assert.Equal(0.05, combat.TickSeconds);
            Assert.Equal(5_000, combat.DurationMs);
            Assert.Equal(new[] { "bastion2", "mannequin" }, combat.Others().Select(o => o.Id));
            Assert.Equal(5, combat.Player().Slots.Count);
        }
        // Même état affiché au même tick : vue du joueur, emplacements, autres combattants.
        game.RunUntil(() => network.Tick >= 20, 3);
        while (solo.Tick < network.Tick) solo.StepOnce();
        Assert.Equal(solo.Player().Slots.Select(s => (s.SkillId, s.RemainingSeconds, s.Unavailable)),
            network.Player().Slots.Select(s => (s.SkillId, s.RemainingSeconds, s.Unavailable)));
        Assert.Equal(solo.Player().View.Health, network.Player().View.Health);
        Assert.Equal(solo.Others().Select(o => (o.Id, o.IsEnemy, o.View.Health)), network.Others().Select(o => (o.Id, o.IsEnemy, o.View.Health)));
    }

    // --- Intentions ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Intentions_regroupees_sous_la_limite_de_debit_du_serveur()
    {
        // Un joueur qui change de garde à chaque image (5 ms) pendant 10 s n'est jamais déconnecté pour débit excessif.
        var game = new Game(Calm(12_000));
        (NetworkClient a, NetworkClient b) = game.Start();
        game.Script = client =>
        {
            if (client == a && client.Combat is { } combat)
            {
                combat.SetGuard(!combat.GuardHeld);
                combat.Parry();
            }
        };
        game.Run(10, 0.005);
        Assert.Equal(ClientStatus.Combat, a.Status);
        Assert.Null(a.Error);
        Assert.Equal(2, game.Room.ConnectionCount);
        Assert.Equal(0, a.Combat!.Desyncs);
        Assert.Equal(ClientStatus.Combat, b.Status);
    }

    [Fact]
    public void Emplacement_indisponible_rien_n_est_envoye_et_soutien_sur_l_allie()
    {
        var game = new Game(Calm(10_000));
        (NetworkClient a, NetworkClient b) = game.Start();
        NetworkCombat combat = a.Combat!;
        Assert.Equal("bastion2", combat.AllyId);
        combat.DrainFeedback();

        // Emplacement inexistant : retour « indisponible », aucune commande.
        Assert.False(combat.UseSlot(9));
        Assert.Contains(combat.DrainFeedback(), f => f.Kind == FeedbackKind.Unavailable && f.Detail == "emplacement vide");

        // Barrière d'acier (soutien à cible alliée) lancée sur l'autre joueur.
        Assert.True(ShippedData.TryGetBuild(Rempart, out EdS.Data.Builds.ReferenceBuild rempart));
        int slot = rempart.Build.ActiveSkills.ToList().IndexOf(BarrierSkill);
        Assert.True(slot >= 0);
        Assert.True(combat.UseSlot(slot));
        game.Run(0.3);
        // Juste après : en recharge, refusé côté client sans rien envoyer.
        Assert.False(combat.UseSlot(slot));
        Assert.Contains(combat.DrainFeedback(), f => f.Kind == FeedbackKind.Unavailable && f.Detail!.Contains("recharge"));
        Assert.Throws<ArgumentException>(() => combat.SelectAlly("mannequin"));
        Assert.Throws<ArgumentException>(() => combat.SelectTarget("bastion2"));
        game.RunUntil(() => a.Status == ClientStatus.Lobby && b.Status == ClientStatus.Lobby, 12);
        Assert.Contains(ServerCommands(game), c => c.Actor == "bastion" && c.Skill == BarrierSkill && c.Target == "bastion2");
    }

    [Fact]
    public void Refus_du_serveur_affiche_comme_retour()
    {
        var game = new Game(Calm(10_000));
        Tampering? wire = null;
        (NetworkClient a, _) = game.Start(wrapA: inner => wire = new Tampering(inner));
        a.Combat!.DrainFeedback();
        // Le serveur refuse un groupe de commandes (plafond) : le client l'affiche comme un retour et continue.
        wire!.Inject(MessageCodec.Encode(new RejectedMessage("Plus de 4 commandes de « bastion » au tick 12.")));
        game.Run(0.05);
        Assert.Equal("Plus de 4 commandes de « bastion » au tick 12.", a.LastRejected);
        Assert.Contains(a.Combat.DrainFeedback(), f => f.Kind == FeedbackKind.Rejected && f.CombatantId == "bastion" && f.Detail == a.LastRejected);
        Assert.Equal(ClientStatus.Combat, a.Status);
    }

    // --- Divergences provoquées ---------------------------------------------------------------------------------------

    [Fact]
    public void Commandes_perdues_divergence_detectee_et_reparee_par_l_historique()
    {
        var game = new Game(Duo) { Script = Playing() };
        int ticksWithCommands = 0;
        Tampering? wire = null;
        (NetworkClient a, NetworkClient b) = game.Start(new LinkProfile(60, 20), new LinkProfile(60, 20), inner => wire = new Tampering(inner)
        {
            // Le 5e tick porteur de commandes arrive vidé de ses commandes, avec l'empreinte du serveur.
            Alter = m => m is TickMessage t && t.Commands.Commands.Count > 0 && ++ticksWithCommands == 5
                ? new TickMessage(new TickCommands(t.Commands.Tick, Array.Empty<CombatCommand>()), t.Hash)
                : m,
        });
        game.RunUntil(() => a.Status == ClientStatus.Lobby && b.Status == ClientStatus.Lobby, 70);
        NetworkCombat combat = a.Combat!;
        Assert.Equal(1, combat.Desyncs);
        Assert.Equal(1, combat.Resyncs);
        Assert.Contains("empreinte", combat.LastDesync);
        Assert.Equal(ServerHash(game, combat), combat.Hash);
        Assert.Equal(0, b.Combat!.Desyncs);
        Assert.Null(a.Error);
    }

    [Fact]
    public void Tick_refuse_ni_retour_fantome_ni_choix_sur_sa_base()
    {
        // Revue Codex de la PR #20 : un tick aux commandes altérées mais acceptées par le moteur local (une frappe ajoutée sur le
        // mannequin) diverge ; ses effets ne sont jamais publiés : aucun dégât fantôme, aucun choix fondé sur lui.
        var game = new Game(Calm(4_000), seed: 3);
        var link = new LinkProfile(80, 0);
        (NetworkClient a, NetworkClient b) = game.Start(link, link, inner => new Tampering(inner)
        {
            Alter = m => m is TickMessage t && t.Commands.Tick == 20
                ? new TickMessage(new TickCommands(20, new[] { new CombatCommand(1000, "bastion", "skill.bastion.frappe_du_rempart", "mannequin") }), t.Hash)
                : m,
        });
        var seen = new List<CombatFeedback>();
        bool checkedWhileAwaiting = false;
        string? hashAt20 = null;
        game.Script = client =>
        {
            if (client != a || client.Combat is not { } combat) return;
            seen.AddRange(combat.DrainFeedback());
            if (combat.Tick == 20 && !combat.AwaitingResync) hashAt20 = combat.Hash;
            if (combat.AwaitingResync && !checkedWhileAwaiting)
            {
                checkedWhileAwaiting = true;
                // Revue Codex de la PR #20 (tour 4) : le moteur affiché est remis au dernier tick accepté ; la frappe du tick refusé
                // n'apparaît ni sur le mannequin, ni dans l'endurance ou la recharge du joueur.
                Assert.Equal(20, combat.Tick);
                Assert.Equal(hashAt20, combat.Hash);
                Assert.Equal(0, combat.Others().Single(o => o.Id == "mannequin").View.Counters.DamageTaken + combat.Player().View.Counters.DamageDealt);
                Assert.Equal(combat.Player().View.MaxEndurance, combat.Player().View.Endurance);
                Assert.All(combat.Player().Slots, s => Assert.Equal(0, s.RemainingSeconds));
                Assert.False(combat.UseSlot(0));
                Assert.Contains(combat.DrainFeedback(), f => f.Detail == "resynchronisation en cours");
                string? target = combat.TargetId;
                combat.NextTarget();
                combat.SelectTarget("inconnu");   // ignoré pendant l'attente, sans exception
                Assert.Equal(target, combat.TargetId);
            }
        };
        game.RunUntil(() => a.Status == ClientStatus.Lobby && b.Status == ClientStatus.Lobby, 8);
        Assert.True(checkedWhileAwaiting);
        Assert.Equal((1, 1), (a.Combat!.Desyncs, a.Combat.Resyncs));
        Assert.DoesNotContain(seen, f => f.Kind == FeedbackKind.DamageTaken);   // personne n'a frappé en vrai
        Assert.Equal(ServerHash(game, a.Combat), a.Combat.Hash);
    }

    [Fact]
    public void Tick_perdu_trou_detecte_et_repare()
    {
        var game = new Game(Calm(6_000)) { Script = Playing() };
        int ticks = 0;
        (NetworkClient a, _) = game.Start(wrapA: inner => new Tampering(inner) { Alter = m => m is TickMessage && ++ticks == 30 ? null : m });
        game.RunUntil(() => a.Status == ClientStatus.Lobby, 10);
        Assert.Equal(1, a.Combat!.Desyncs);
        Assert.Contains("tick 30 reçu, tick 29 attendu", a.Combat.LastDesync);
        Assert.Equal(1, a.Combat.Resyncs);
        Assert.Equal(ServerHash(game, a.Combat), a.Combat.Hash);
    }

    [Theory]
    [InlineData(true, "divergence pas encore réparée à la fin du combat (tick 39 : empreinte")]
    [InlineData(false, "fin annoncée au tick 39, combat local au tick 39 pas terminé")]
    public void Fin_du_combat_avec_une_divergence_pas_reparee_jamais_acceptee_en_silence(bool alterHash, string expected)
    {
        // Revue Codex de la PR #20 : le dernier tick faussé (ou perdu) laisse le client en attente d'historique quand la fin arrive ; le
        // serveur ne répond plus après la fin : le client le dit au lieu d'accepter un résultat divergent.
        var game = new Game(Calm(2_000));
        (NetworkClient a, NetworkClient b) = game.Start(wrapA: inner => new Tampering(inner)
        {
            Alter = m => m is TickMessage t && t.Commands.Tick == 39 ? alterHash ? new TickMessage(t.Commands, "fnv1a64:0000000000000000") : null : m,
        });
        game.RunUntil(() => a.Status == ClientStatus.Lobby && b.Status == ClientStatus.Lobby, 5);
        NetworkCombat combat = a.Combat!;
        Assert.True(combat.IsFinished);
        Assert.StartsWith(expected, combat.EndProblem);
        Assert.Null(combat.Result);
        Assert.Equal("fin du combat non vérifiée : " + combat.EndProblem, a.Error);
        Assert.False(combat.AwaitingResync);
        // L'autre joueur, lui, a une fin vérifiée.
        Assert.Null(b.Combat!.EndProblem);
        Assert.NotNull(b.Combat.Result);
        Assert.Null(b.Error);
        // Le client reste dans la salle : un nouveau combat efface l'erreur.
        a.SetReady(true);
        b.SetReady(true);
        game.RunUntil(() => a.Status == ClientStatus.Combat, 2);
        Assert.Null(a.Error);
    }

    [Fact]
    public void Etat_courant_fausse_le_client_abandonne_apres_trois_essais()
    {
        // Sous-lot 11.7 : l'empreinte d'un état courant ne se vérifie qu'au tick suivant ; un état faussé est repris, puis le tick suivant
        // diverge ; après trois essais sans tick accepté, le client abandonne.
        var game = new Game(Calm(20_000));
        int ticks = 0;
        (NetworkClient a, NetworkClient b) = game.Start(wrapA: inner => new Tampering(inner)
        {
            Alter = m => m switch
            {
                TickMessage t when ++ticks == 10 => new TickMessage(t.Commands, "fnv1a64:0000000000000000"),
                StateMessage s => new StateMessage(s.Header, s.Snapshot, "fnv1a64:0000000000000000"),
                _ => m,
            },
        });
        game.RunUntil(() => a.Status == ClientStatus.Failed, 15);
        // Divergence du tick 10, état faussé repris, divergence au tick suivant, deuxième demande refusée par le serveur (au plus une par
        // seconde) puis renvoyée après le délai d'attente, état repris, nouvelle divergence : trois demandes sans tick accepté.
        Assert.StartsWith("désynchronisation persistante après 3 demandes d'état courant : tick ", a.Error);
        Assert.Contains("empreinte fnv1a64:0000000000000000 au lieu de", a.Error);
        Assert.Equal(3, a.Combat!.Desyncs);
        Assert.Equal(2, a.Combat.Resyncs);
        // Le client a fermé sa connexion : le serveur garde sa place 30 s, le combat continue pour l'autre joueur.
        game.Run(0.5);
        Assert.Equal(ClientStatus.Combat, b.Status);
        Assert.Equal(1, game.Room.ConnectionCount);
    }

    [Fact]
    public void Message_illisible_du_serveur_le_client_s_arrete_sans_exception()
    {
        var game = new Game(Calm(10_000));
        Tampering? wire = null;
        (NetworkClient a, _) = game.Start(wrapA: inner => wire = new Tampering(inner));
        wire!.Inject(System.Text.Encoding.UTF8.GetBytes("{\"type\":\"tick\",\"tick\":-1}"));
        game.Run(0.05);
        Assert.Equal(ClientStatus.Failed, a.Status);
        Assert.StartsWith("message du serveur refusé : ", a.Error);
        Assert.Equal(0, a.Advance(1));
    }

    [Theory]
    [InlineData("{\"type\":\"header\"}", "début de combat illisible")]
    [InlineData(null, "début de combat sans le personnage « bastion ».")]
    public void Debut_de_combat_illisible_ou_sans_le_joueur_refuse(string? header, string expected)
    {
        var game = new Game(Calm(10_000));
        // null : l'en-tête du serveur, où le personnage du joueur est renommé.
        NetworkClient a = game.Join("Dam", wrap: inner => new Tampering(inner)
        {
            Alter = m => m is StartMessage s ? new StartMessage(header ?? s.Header.Replace("\"bastion\"", "\"autre\"")) : m,
        });
        NetworkClient b = game.Join("Léa");
        a.SetReady(true);
        b.SetReady(true);
        game.RunUntil(() => a.Status == ClientStatus.Failed, 2);
        Assert.StartsWith(expected, a.Error);
        Assert.Null(a.Combat);
    }

    [Fact]
    public void Client_du_protocole_4_refuse_un_serveur_du_protocole_3()
    {
        // Relecture indépendante de la PR #82 : au protocole 3, l'emplacement 4 était l'ultime ; un client du protocole 4 refuse donc
        // l'accueil d'un serveur du protocole 3, sans entrer dans la salle.
        var game = new Game(Calm(10_000));
        NetworkClient a = game.Join("Dam", wrap: inner => new Tampering(inner)
        {
            Alter = m => m is WelcomeMessage w ? new WelcomeMessage(3, w.Seat, w.Token, w.TickMs) : m,
        });
        game.RunUntil(() => a.Status == ClientStatus.Failed, 2);
        Assert.Equal(ClientStatus.Failed, a.Status);
        Assert.Contains($"protocole 3 du serveur, version {Protocol.Version} attendue", a.Error);
        Assert.Equal(4, Protocol.Version);
        Assert.Null(a.Combat);
    }

    [Fact]
    public void Identifiant_de_combattant_hors_protocole_refuse_par_le_client_et_le_serveur()
    {
        // Revue Codex de la PR #20 (tour 4) : un ennemi dont l'identifiant dépasse 128 caractères serait visé, puis nommé dans un message
        // que le serveur refuse. Le client refuse l'en-tête avant le combat ; le serveur refuse le scénario dès la création de la salle.
        string longId = new string('m', Protocol.MaxIdLength + 1);
        var game = new Game(Calm(10_000));
        NetworkClient a = game.Join("Dam", wrap: inner => new Tampering(inner)
        {
            Alter = m => m is StartMessage s ? new StartMessage(s.Header.Replace("\"mannequin\"", "\"" + longId + "\"")) : m,
        });
        NetworkClient b = game.Join("Léa");
        a.SetReady(true);
        b.SetReady(true);
        game.RunUntil(() => a.Status == ClientStatus.Failed, 2);
        Assert.StartsWith("début de combat refusé : $.scenario.combatants[2].id, identifiant incompatible avec le protocole", a.Error);
        Assert.Null(a.Combat);

        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(ScenarioPath("coop/duo_meute")))!.AsObject();
        root["data"]!["combatants"]!.AsArray().Last()!["id"] = longId;
        ScenarioLoadResult loaded = ScenarioJsonReader.Read(root.ToJsonString(), ShippedData);
        Assert.True(loaded.Setup != null, loaded.Report.ToString());
        int last = loaded.Setup!.Combatants.Count - 1;
        var error = Assert.Throws<ArgumentException>(() => new CombatRoom(ShippedData, loaded.Setup));
        Assert.StartsWith($"Combattant {last} (combatants[{last}].id) : identifiant invalide pour le protocole", error.Message);
    }

    // --- Coupures et retour ---------------------------------------------------------------------------------------------

    [Fact]
    public void Coupure_puis_retour_avec_le_jeton_et_l_historique()
    {
        var game = new Game(Duo) { Script = Playing() };
        var link = new LinkProfile(40, 10);
        (NetworkClient a, NetworkClient b) = game.Start(link, link);
        game.RunUntil(() => a.Combat!.Tick >= 100, 10);
        game.Script = null;
        a.Combat!.SetGuard(true);
        game.Run(0.3);
        Assert.True(a.Combat.Player().View.Guarding);

        // Coupure brutale : le client le voit, garde son jeton ; le serveur continue le combat.
        game.Cut(a);
        game.Run(0.05);
        Assert.Equal(ClientStatus.Disconnected, a.Status);
        Assert.Equal("coupure réseau.", a.Error);
        Assert.True(a.CanReconnect);
        long frozen = a.Combat.Tick;
        game.Run(3);
        Assert.Equal(frozen, a.Combat.Tick);
        Assert.InRange(a.DisconnectedSeconds!.Value, 2.9, 3.1);
        Assert.Equal(ClientStatus.Combat, b.Status);
        Assert.True(b.Combat!.Tick > frozen + 50);

        // Retour : même place, état courant du serveur, le combat reprend à son tick sans rien rejouer (sous-lot 11.7 ; au lot 10,
        // l'historique complet était rejoué).
        game.Reconnect(a, link);
        Assert.Equal(ClientStatus.Reconnecting, a.Status);
        // Revue Codex de la PR #20 (tour 5) : pas de seconde demande par-dessus la première (le serveur refuserait le jeton tant que
        // l'ancienne connexion n'est pas détachée) ; le refus ne change rien.
        INetConnection extra = game.Connect();
        Assert.Throws<InvalidOperationException>(() => a.Reconnect(extra));
        Assert.Equal(ClientStatus.Reconnecting, a.Status);
        extra.Close("essai terminé.");
        // Pas à pas, pour lire ce que rend Advance au pas du retour : aucun tick passé rejoué, seulement ceux reçus après l'état.
        for (int i = 0; i < 400 && a.Status != ClientStatus.Combat; i++)
        {
            game.Net.Advance(0.01);
            while (game.Listener.TryAccept(out INetConnection connection)) game.Room.Accept(connection);
            game.Room.Advance(0.01);
            b.Advance(0.01);
            int ticks = a.Advance(0.01);
            if (a.Status == ClientStatus.Combat) Assert.InRange(ticks, 0, 2);
            else Assert.Equal(0, ticks);
        }
        Assert.Equal(ClientStatus.Combat, a.Status);
        Assert.Equal(1, a.Combat.Resyncs);
        Assert.Equal(0, a.Combat.Desyncs);
        Assert.Null(a.DisconnectedSeconds);
        Assert.True(a.Combat.Tick > frozen + 50);
        Assert.False(a.Combat.Player().View.Guarding);
        game.Script = Playing();
        game.RunUntil(() => a.Status == ClientStatus.Lobby && b.Status == ClientStatus.Lobby, 70);
        Assert.Equal(ServerHash(game, a.Combat), a.Combat.Hash);
        Assert.Equal(ServerHash(game, b.Combat), b.Combat.Hash);

        // Pendant l'absence, le serveur a relâché la garde du personnage (décision du 19/10/2026).
        IReadOnlyList<CombatCommand> commands = ServerCommands(game);
        Assert.Contains(commands, c => c.Actor == "bastion" && c.Action == CombatAction.GuardStop && c.AtMs > frozen * 50 && c.AtMs < (frozen + 60) * 50);
        Assert.Throws<InvalidOperationException>(() => a.Reconnect(game.Connect()));
    }

    [Fact]
    public void Retour_trop_tard_refuse_par_le_serveur()
    {
        var game = new Game(Calm(60_000), new RoomOptions { ReconnectSeconds = 2 });
        (NetworkClient a, NetworkClient b) = game.Start();
        game.Cut(a);
        game.Run(2.5);
        Assert.Equal(ClientStatus.Disconnected, a.Status);
        game.Reconnect(a);
        game.RunUntil(() => a.Status == ClientStatus.Failed, 2);
        Assert.Equal("refusé par le serveur : jeton inconnu ou expiré.", a.Error);
        Assert.Equal(ClientStatus.Combat, b.Status);
    }

    [Fact]
    public void Coupure_hors_combat_le_client_s_arrete()
    {
        var game = new Game(Calm(2_000));
        NetworkClient a = game.Join("Dam");
        game.Cut(a);
        game.Run(0.05);
        Assert.Equal(ClientStatus.Failed, a.Status);
        Assert.False(a.CanReconnect);
        Assert.Throws<InvalidOperationException>(() => a.Reconnect(game.Connect()));
        Assert.Throws<ArgumentException>(() => new NetworkClient(ShippedData, game.Connect(), " Dam", new BuildChoice(Rempart)));
        Assert.Throws<ArgumentException>(() => new NetworkClient(ShippedData, game.Connect(), new string('x', 25), new BuildChoice(Rempart)));
        // Messages vérifiés par le lecteur strict avant l'envoi : une erreur de l'appelant n'est pas une faute de protocole.
        Assert.Throws<ArgumentException>(() => new NetworkClient(ShippedData, game.Connect(), "Dam", new BuildChoice("build inconnu")));
        NetworkClient b = game.Join("Léa");
        Assert.Throws<ArgumentException>(() => b.SetBuild(new BuildChoice(Rempart, new Dictionary<string, int> { ["talent.bastion.rempart.garde_stable"] = -1 })));
        Assert.Equal(ClientStatus.Lobby, b.Status);
    }

    [Fact]
    public void Commande_inconnue_du_serveur_divergence_sans_exception()
    {
        var game = new Game(Calm(4_000));
        int ticks = 0;
        (NetworkClient a, _) = game.Start(wrapA: inner => new Tampering(inner)
        {
            Alter = m => m is TickMessage t && ++ticks == 12
                ? new TickMessage(new TickCommands(t.Commands.Tick, new[] { new CombatCommand(t.Commands.Tick * 50, "fantome", CombatAction.Parry) }), t.Hash)
                : m,
        });
        game.RunUntil(() => a.Status == ClientStatus.Lobby, 6);
        Assert.Equal(1, a.Combat!.Desyncs);
        Assert.Contains("refusées par le moteur local", a.Combat.LastDesync);
        Assert.Equal(1, a.Combat.Resyncs);
        Assert.Equal(ServerHash(game, a.Combat), a.Combat.Hash);
    }

    // --- TCP réel ------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Deux_clients_TCP_contre_un_hote_temps_reel()
    {
        using var listener = new TcpNetListener(IPAddress.Loopback, 0);
        var room = new CombatRoom(ShippedData, Calm(1_500));
        using var host = new RoomHost(room, listener);
        host.Start();
        NetworkClient Join(TcpNetConnection connection, string name, string build) => new(ShippedData, connection, name, new BuildChoice(build));
        TcpNetConnection ca = await TcpNetConnection.ConnectAsync("127.0.0.1", listener.Port, Protocol.MaxServerMessageBytes, Protocol.MaxClientMessageBytes);
        TcpNetConnection cb = await TcpNetConnection.ConnectAsync("127.0.0.1", listener.Port, Protocol.MaxServerMessageBytes, Protocol.MaxClientMessageBytes);
        using (ca)
        using (cb)
        {
            NetworkClient a = Join(ca, "Dam", Rempart), b = Join(cb, "Léa", Gardien);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            double last = 0;
            bool readySent = false;
            while (watch.Elapsed.TotalSeconds < TcpDeadlineSeconds && !(a.Combat?.IsFinished == true && b.Combat?.IsFinished == true && a.Status == ClientStatus.Lobby && b.Status == ClientStatus.Lobby))
            {
                double now = watch.Elapsed.TotalSeconds;
                if (!readySent && a.Status == ClientStatus.Lobby && b.Status == ClientStatus.Lobby)
                {
                    a.SetReady(true);
                    b.SetReady(true);
                    readySent = true;
                }
                a.Combat?.SetGuard(true);
                a.Advance(now - last);
                b.Advance(now - last);
                last = now;
                Thread.Sleep(5);
            }
            Assert.Null(host.Error);
            string state = $"A : {a.Status} {a.Error}, B : {b.Status} {b.Error}, prêts envoyés : {readySent}";
            Assert.True(a.Combat != null && b.Combat != null, "combat jamais commencé après " + TcpDeadlineSeconds + " s. " + state);
            Assert.True(a.Combat!.IsFinished && b.Combat!.IsFinished, "combat non terminé. " + state);
            Assert.Equal(0, a.Combat.Desyncs + b.Combat!.Desyncs);
            string expected = host.Read(r => HashingCombatLog.Of(new[] { a.Combat.Header }.Concat(r.LastJournal!.Skip(1))));
            Assert.Equal(expected, a.Combat.Hash);
            Assert.Equal(expected, b.Combat.Hash);
            Assert.NotNull(a.RttMs);
            Assert.InRange(a.RttMs!.Value, 0, 1000);
        }
    }
}
