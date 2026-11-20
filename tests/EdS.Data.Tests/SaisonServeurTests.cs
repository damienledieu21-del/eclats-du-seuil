using System.Text;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Domain.Time;
using EdS.Net;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.44.1 (saisons, décision de Damien du 20 novembre 2026 : le serveur fait foi) : protocole 6, message <c>season</c> (instant
/// civil UTC de l'hôte du serveur) envoyé à l'accueil et à chaque relevé ; le client en tire la saison au lieu de sa propre horloge.
/// </summary>
[Trait("Category", "Network")]
public class SaisonServeurTests
{
    private const string Rempart = "build.bastion.rempart";

    private static long Utc(int y, int mo, int d, int h = 12) => new DateTimeOffset(y, mo, d, h, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    private static CombatSetup Duo => ScenarioJsonReader.ReadFile(ScenarioPath("coop/duo_meute"), ShippedData).Setup!;

    private static NetMessage? Decode(string json, MessageDirection direction, out string? error) =>
        MessageCodec.Decode(Encoding.UTF8.GetBytes(json), direction, out error);

    private sealed class Game
    {
        private int _tokens;

        public Game()
        {
            Net = new SimulatedNetwork(1);
            Listener = Net.Listen("salle");
            Room = new CombatRoom(ShippedData, Duo, new RoomOptions { TokenSource = () => (++_tokens).ToString("x32") });
        }

        public SimulatedNetwork Net { get; }
        public SimulatedListener Listener { get; }
        public CombatRoom Room { get; }
        public List<NetworkClient> Clients { get; } = new();

        public NetworkClient Join(string name)
        {
            var client = new NetworkClient(ShippedData, Net.Connect("salle", LinkProfile.Perfect), name, new BuildChoice(Rempart));
            Clients.Add(client);
            Run(0.2);
            Assert.Equal(ClientStatus.Lobby, client.Status);
            return client;
        }

        public void Run(double seconds, double step = 0.01)
        {
            for (double t = 0; t < seconds - 1e-9; t += step)
            {
                Net.Advance(step);
                while (Listener.TryAccept(out INetConnection connection)) Room.Accept(connection);
                Room.Advance(step);
                foreach (NetworkClient client in Clients) client.Advance(step);
            }
        }
    }

    [Fact]
    public void Protocole_6_et_message_season_aller_retour()
    {
        Assert.Equal(6, Protocol.Version);
        var message = new SeasonMessage(Utc(2026, 12, 8));
        string text = MessageCodec.EncodeText(message);
        Assert.Equal($"{{\"type\":\"season\",\"utc\":{Utc(2026, 12, 8)}}}", text);
        var back = Assert.IsType<SeasonMessage>(Decode(text, MessageDirection.ServerToClient, out string? error));
        Assert.Null(error);
        Assert.Equal(Utc(2026, 12, 8), back.UtcSeconds);
        // Frontières du calendrier : les deux bornes passent, au-delà rien.
        Assert.Equal(ParisTime.MinUnixSeconds, new SeasonMessage(ParisTime.MinUnixSeconds).UtcSeconds);
        Assert.Equal(ParisTime.MaxUnixSeconds, new SeasonMessage(ParisTime.MaxUnixSeconds).UtcSeconds);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeasonMessage(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeasonMessage(ParisTime.MaxUnixSeconds + 1));
        Assert.NotNull(Decode($"{{\"type\":\"season\",\"utc\":{ParisTime.MaxUnixSeconds}}}", MessageDirection.ServerToClient, out _));
    }

    [Theory]
    [InlineData("{\"type\":\"season\",\"utc\":-1}", "hors de")]
    [InlineData("{\"type\":\"season\"}", "utc")]
    [InlineData("{\"type\":\"season\",\"utc\":1.5}", "$.utc")]
    [InlineData("{\"type\":\"season\",\"utc\":\"1\"}", "$.utc")]
    [InlineData("{\"type\":\"season\",\"utc\":1,\"en_trop\":2}", "en_trop")]
    public void Message_season_invalide_refuse(string json, string expected)
    {
        Assert.Null(Decode(json, MessageDirection.ServerToClient, out string? error));
        Assert.Contains(expected, error);
        Assert.Null(Decode($"{{\"type\":\"season\",\"utc\":{ParisTime.MaxUnixSeconds + 1}}}", MessageDirection.ServerToClient, out _));
    }

    [Fact]
    public void Un_client_ne_peut_pas_envoyer_la_saison()
    {
        Assert.Null(Decode("{\"type\":\"season\",\"utc\":1}", MessageDirection.ClientToServer, out string? error));
        Assert.Contains("refusé dans ce sens", error);
    }

    [Fact]
    public void La_date_du_serveur_part_a_chaque_releve_et_a_l_accueil()
    {
        var game = new Game();
        NetworkClient dam = game.Join("Dam");
        Assert.Null(dam.ServerCivilUtcSeconds);   // hôte sans horloge civile : rien n'est envoyé
        Assert.Null(dam.ServerCivilNow);
        Assert.Null(dam.ServerSeason);

        game.Room.SetCivilTime(Utc(2026, 12, 8));
        game.Run(0.1);
        Assert.Equal(Utc(2026, 12, 8), dam.ServerCivilUtcSeconds);
        Assert.Equal((Season.Winter, IcePhase.Walkable), (dam.ServerSeason!.Value.Season, dam.ServerSeason.Value.Ice));

        // Un joueur arrivé après le relevé reçoit la date dès l'accueil.
        NetworkClient lea = game.Join("Léa");
        Assert.Equal(Utc(2026, 12, 8), lea.ServerCivilUtcSeconds);

        // Nouveau relevé : les deux joueurs suivent le serveur.
        game.Room.SetCivilTime(Utc(2027, 4, 2));
        game.Run(0.1);
        Assert.All(game.Clients, c => Assert.Equal(Season.Spring, c.ServerSeason!.Value.Season));
    }

    [Fact]
    public void Releve_refuse_atomique_rien_ne_part()
    {
        var game = new Game();
        NetworkClient dam = game.Join("Dam");
        game.Room.SetCivilTime(Utc(2026, 9, 15));
        game.Run(0.1);
        Assert.Throws<ArgumentOutOfRangeException>(() => game.Room.SetCivilTime(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => game.Room.SetCivilTime(ParisTime.MaxUnixSeconds + 1));
        game.Run(0.1);
        Assert.Equal(Utc(2026, 9, 15), game.Room.CivilUtcSeconds);
        Assert.Equal(Utc(2026, 9, 15), dam.ServerCivilUtcSeconds);
        Assert.Equal(Season.Autumn, dam.ServerSeason!.Value.Season);
    }

    [Fact]
    public void Le_client_prolonge_la_date_du_serveur_sans_lire_d_horloge()
    {
        var game = new Game();
        NetworkClient dam = game.Join("Dam");
        // Veille du printemps à 23 h 59 min 58 s, heure de Paris (UTC+1) : le client passe au printemps avec le temps écoulé chez lui.
        long eve = new DateTimeOffset(2027, 2, 28, 22, 59, 58, TimeSpan.Zero).ToUnixTimeSeconds();
        game.Room.SetCivilTime(eve);
        game.Run(0.05);
        long received = dam.ServerCivilNow!.Value;
        Assert.InRange(received, eve, eve + 1);
        Assert.Equal(Season.Winter, dam.ServerSeason!.Value.Season);
        game.Run(3);
        Assert.InRange(dam.ServerCivilNow!.Value, eve + 2, eve + 4);
        Assert.Equal(Season.Spring, dam.ServerSeason!.Value.Season);

        // Plafond : jamais au-delà du calendrier.
        game.Room.SetCivilTime(ParisTime.MaxUnixSeconds);
        game.Run(2);
        Assert.Equal(ParisTime.MaxUnixSeconds, dam.ServerCivilNow);
        Assert.NotNull(dam.ServerSeason);
    }

    [Fact]
    public void Deux_parties_identiques_donnent_les_memes_messages()
    {
        static List<string> Play()
        {
            var game = new Game();
            NetworkClient dam = game.Join("Dam");
            game.Room.SetCivilTime(Utc(2026, 12, 20));
            NetworkClient lea = game.Join("Léa");
            game.Run(0.5);
            return new List<string> { $"{dam.ServerCivilNow}", $"{lea.ServerCivilNow}", $"{dam.ServerSeason}", $"{lea.ServerSeason}" };
        }
        Assert.Equal(Play(), Play());
    }
}
