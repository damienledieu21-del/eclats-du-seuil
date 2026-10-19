using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Net;
using EdS.Runtime;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 10.6 : option B de la parade en réseau (décision de Damien du 19/10/2026 à 11:28). Le client affiche la télégraphie des
/// assaillants en avance du temps aller-retour lissé, plafonné ; rien d'autre ne change (le serveur juge toujours sur son tick). Le
/// robot, qui pare à vue sur cet affichage, réussit ses parades malgré 100 ms de latence d'un sens ; sans l'avance, il les rate.
/// </summary>
[Trait("Category", "Network")]
public class Lot106TelegraphTests
{
    private static CombatSetup Duo => ScenarioJsonReader.ReadFile(ScenarioPath("coop/duo_meute"), ShippedData).Setup!;

    /// <summary>Connexion qui retire les réponses aux mesures du temps aller-retour (aucune mesure n'aboutit).</summary>
    private sealed class NoPong : INetConnection
    {
        private readonly INetConnection _inner;

        public NoPong(INetConnection inner) => _inner = inner;

        public string RemoteAddress => _inner.RemoteAddress;
        public bool IsOpen => _inner.IsOpen;
        public string? CloseReason => _inner.CloseReason;
        public bool Send(byte[] payload) => _inner.Send(payload);
        public void Close(string reason) => _inner.Close(reason);
        public void Dispose() => _inner.Dispose();

        public bool TryReceive(out byte[] payload)
        {
            while (_inner.TryReceive(out payload))
                if (MessageCodec.Decode(payload, MessageDirection.ServerToClient, out _) is not PongMessage) return true;
            return false;
        }
    }

    /// <summary>Salle sur le réseau simulé et deux joueurs (clients ou robots), avancés ensemble par pas de 10 ms.</summary>
    private sealed class Duel
    {
        private int _tokens;
        private readonly SimulatedListener _listener;
        private readonly List<Action<double>> _advance = new();

        public Duel(ulong seed = 5)
        {
            Net = new SimulatedNetwork(seed);
            _listener = Net.Listen("salle");
            Room = new CombatRoom(ShippedData, Duo, new RoomOptions { TokenSource = () => (++_tokens).ToString("x32") });
        }

        public SimulatedNetwork Net { get; }
        public CombatRoom Room { get; }

        public NetworkClient Join(string name, string build, LinkProfile link, NetworkClientOptions options, bool bot = false,
            Func<INetConnection, INetConnection>? wrap = null)
        {
            INetConnection connection = Net.Connect("salle", link);
            var client = new NetworkClient(ShippedData, wrap is null ? connection : wrap(connection), name, new BuildChoice(build), options);
            if (bot)
            {
                var player = new BotPlayer(client);
                _advance.Add(player.Advance);
            }
            else _advance.Add(s => client.Advance(s));
            return client;
        }

        public void Run(double seconds)
        {
            for (double t = 0; t < seconds - 1e-9; t += 0.01)
            {
                Net.Advance(0.01);
                while (_listener.TryAccept(out INetConnection connection)) Room.Accept(connection);
                Room.Advance(0.01);
                foreach (Action<double> advance in _advance) advance(0.01);
            }
        }

        public void RunUntil(Func<bool> done, double limitSeconds)
        {
            for (double t = 0; t < limitSeconds && !done(); t += 0.01) Run(0.01);
            Assert.True(done(), "condition non atteinte dans le temps imparti");
        }
    }

    /// <summary>Temps avant le prochain coup sans avance : celui du moteur local (dernier tick reçu).</summary>
    private static double Raw(NetworkCombat combat, OtherState other) =>
        Math.Max(0, other.View.NextAttackTick!.Value - combat.Tick) * combat.TickSeconds;

    [Fact]
    public void Telegraphie_avancee_du_temps_aller_retour_lisse_affichage_seulement()
    {
        var duel = new Duel();
        var link = new LinkProfile(100, 0);
        NetworkClient advanced = duel.Join("Dam", "build.bastion.rempart", link, new NetworkClientOptions());
        NetworkClient plain = duel.Join("Léa", "build.bastion.gardien", link, new NetworkClientOptions { TelegraphAdvance = false });
        duel.RunUntil(() => advanced.Status == ClientStatus.Lobby && plain.Status == ClientStatus.Lobby, 3);
        advanced.SetReady(true);
        plain.SetReady(true);
        duel.RunUntil(() => advanced.Status == ClientStatus.Combat && plain.Status == ClientStatus.Combat, 5);
        Assert.True(new NetworkClientOptions().TelegraphAdvance, "avance de la télégraphie active par défaut (option B)");

        int checkedThreats = 0, shortened = 0;
        for (int i = 0; i < 300; i++)
        {
            duel.Run(0.03);
            NetworkCombat a = advanced.Combat!, b = plain.Combat!;
            if (a.IsFinished || b.IsFinished) break;
            // Temps aller-retour lissé de 200 ms (deux fois 100 ms) : avance de 200 ms, sous le plafond de 1 s.
            Assert.Equal(Math.Min(1.0, advanced.SmoothedRttMs!.Value / 1000), a.TelegraphLeadSeconds, 9);
            Assert.InRange(a.TelegraphLeadSeconds, 0.19, 0.23);
            Assert.Equal(0, b.TelegraphLeadSeconds);
            foreach (OtherState other in a.Others().Where(o => o.View.NextAttackTick is long))
            {
                double raw = Raw(a, other);
                Assert.Equal(Math.Max(0, raw - a.TelegraphLeadSeconds), other.NextAttackSeconds!.Value, 9);
                checkedThreats++;
                if (raw > a.TelegraphLeadSeconds) shortened++;
            }
            foreach (OtherState other in b.Others().Where(o => o.View.NextAttackTick is long))
                Assert.Equal(Raw(b, other), other.NextAttackSeconds!.Value, 9);
            // Seule la télégraphie change : le reste de l'affichage est celui du moteur.
            Assert.All(a.Others(), o => Assert.Equal(o.View.NextAttackTick is null, o.NextAttackSeconds is null));
        }
        Assert.True(checkedThreats > 100 && shortened > 50, $"{checkedThreats} télégraphies vérifiées, {shortened} avancées");
        duel.RunUntil(() => duel.Room.LastResult != null && advanced.Status == ClientStatus.Lobby && plain.Status == ClientStatus.Lobby, 70);
        // Le serveur juge seul : les deux clients finissent avec son empreinte, avance ou pas.
        string serverHash(NetworkCombat c) => HashingCombatLog.Of(new[] { c.Header }.Concat(duel.Room.LastJournal!.Skip(1)));
        Assert.Equal(serverHash(advanced.Combat!), advanced.Combat!.Hash);
        Assert.Equal(serverHash(plain.Combat!), plain.Combat!.Hash);
    }

    [Fact]
    public void Avance_plafonnee_et_nulle_sans_mesure()
    {
        var duel = new Duel(seed: 9);
        NetworkClient far = duel.Join("Dam", "build.bastion.rempart", new LinkProfile(700, 0), new NetworkClientOptions());
        NetworkClient capped = duel.Join("Léa", "build.bastion.gardien", new LinkProfile(700, 0), new NetworkClientOptions { MaxTelegraphLeadSeconds = 0.3 });
        duel.RunUntil(() => far.Status == ClientStatus.Lobby && capped.Status == ClientStatus.Lobby, 5);
        far.SetReady(true);
        capped.SetReady(true);
        duel.RunUntil(() => far.Status == ClientStatus.Combat && capped.Status == ClientStatus.Combat && far.Combat!.Tick > 20, 10);
        // 1,4 s d'aller-retour : avance plafonnée à 1 s (réglage par défaut) ou au plafond choisi.
        Assert.InRange(far.SmoothedRttMs!.Value, 1390, 1430);
        Assert.Equal(1.0, far.Combat!.TelegraphLeadSeconds);
        Assert.Equal(0.3, capped.Combat!.TelegraphLeadSeconds);

        // Aucune réponse aux mesures : pas de temps aller-retour, pas d'avance.
        var quiet = new Duel(seed: 11);
        NetworkClient blind = quiet.Join("Dam", "build.bastion.rempart", new LinkProfile(50, 0), new NetworkClientOptions(), wrap: c => new NoPong(c));
        NetworkClient other = quiet.Join("Léa", "build.bastion.gardien", new LinkProfile(50, 0), new NetworkClientOptions());
        quiet.RunUntil(() => blind.Status == ClientStatus.Lobby && other.Status == ClientStatus.Lobby, 3);
        blind.SetReady(true);
        other.SetReady(true);
        quiet.RunUntil(() => blind.Status == ClientStatus.Combat && blind.Combat!.Tick > 20, 5);
        Assert.Null(blind.SmoothedRttMs);
        Assert.Equal(0, blind.Combat!.TelegraphLeadSeconds);
        Assert.All(blind.Combat.Others().Where(o => o.View.NextAttackTick is long), o => Assert.Equal(Raw(blind.Combat, o), o.NextAttackSeconds!.Value, 9));
    }

    [Theory]
    [InlineData(-0.001)]
    [InlineData(10.001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Plafond_invalide_refuse(double max)
    {
        var options = new NetworkClientOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxTelegraphLeadSeconds = max);
        Assert.Equal(1.0, options.MaxTelegraphLeadSeconds);
    }

    [Theory]
    [InlineData(-0.001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Avance_invalide_refusee_par_l_affichage(double lead)
    {
        var engine = new CombatEngine(Duo, new HashingCombatLog());
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatDisplay.Others(engine, "bastion", lead));
    }

    [Fact]
    public void Plafond_aux_bornes_accepte_et_solo_sans_avance()
    {
        var options = new NetworkClientOptions { MaxTelegraphLeadSeconds = 0 };
        Assert.Equal(0, options.MaxTelegraphLeadSeconds);
        options.MaxTelegraphLeadSeconds = 10;
        Assert.Equal(10, options.MaxTelegraphLeadSeconds);
        // En solo (et sans avance), l'affichage est celui du moteur ; une grande avance ne rend jamais un temps négatif.
        var engine = new CombatEngine(Duo, new HashingCombatLog());
        for (int i = 0; i < 30; i++) engine.Step();
        IReadOnlyList<OtherState> solo = CombatDisplay.Others(engine, "bastion"), far = CombatDisplay.Others(engine, "bastion", 5);
        Assert.All(solo.Zip(far), p =>
        {
            Assert.Equal(p.First.View.NextAttackTick is long n ? Math.Max(0, n - engine.CurrentTick) * 0.05 : (double?)null, p.First.NextAttackSeconds);
            Assert.Equal(p.First.NextAttackSeconds is null ? (double?)null : 0.0, p.Second.NextAttackSeconds);
        });
        Assert.Contains(solo, o => o.NextAttackSeconds > 0);
    }

    [Fact]
    public void Robot_pare_a_vue_sur_la_telegraphie_avancee_malgre_la_latence()
    {
        // Deux robots Rempart à 100 ms d'un sens, l'un avec l'avance (réglage du jeu), l'autre sans (option A) : mêmes réflexes, même combat.
        var duel = new Duel(seed: 3);
        var link = new LinkProfile(100, 0);
        NetworkClient advanced = duel.Join("Robot B", "build.bastion.rempart", link, new NetworkClientOptions(), bot: true);
        NetworkClient plain = duel.Join("Robot A", "build.bastion.rempart", link, new NetworkClientOptions { TelegraphAdvance = false }, bot: true);
        duel.RunUntil(() => duel.Room.LastResult != null && advanced.Status == ClientStatus.Lobby && plain.Status == ClientStatus.Lobby, 80);
        CombatResult result = duel.Room.LastResult!;
        CombatantResult b = result.Combatants.Single(c => c.Id == advanced.SeatId), a = result.Combatants.Single(c => c.Id == plain.SeatId);
        // Avec l'avance, le robot fait aussi bien qu'à 0 ms (environ deux parfaites pour une ratée) ; sans, il pare toujours trop tard.
        Assert.True(b.PerfectParries >= 25 && b.PerfectParries >= 2 * b.FailedParries,
            $"avec l'avance : {b.PerfectParries} parfaites, {b.FailedParries} ratées");
        Assert.True(a.FailedParries >= 20 && a.PerfectParries * 4 < a.FailedParries,
            $"sans l'avance : {a.PerfectParries} parfaites, {a.FailedParries} ratées");
    }
}
