using System.Net;
using System.Net.Sockets;
using System.Text;
using EdS.Domain.Combat;
using EdS.Net;
using FsCheck.Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 10.1 : protocole réseau (messages, lecture stricte, limites), découpage du flux, transport TCP et réseau simulé.
/// </summary>
[Trait("Category", "Network")]
public class Lot101ProtocolTests
{
    private const string Token = "0123456789abcdef0123456789abcdef";
    private const string Hash = "fnv1a64:0123456789abcdef";

    private static readonly CombatCommand[] SampleCommands =
    {
        new(500, "bastion", CombatAction.GuardStop),
        new(500, "bastion", "skill.bastion.frappe_du_rempart", "loup_a"),
        new(500, "bastion2", "skill.bastion.onde_de_rupture"),
        new(500, "bastion2", CombatAction.Parry),
    };

    /// <summary>Un exemplaire de chaque message, tous champs remplis ou absents.</summary>
    public static IEnumerable<object[]> AllMessages()
    {
        var talents = new Dictionary<string, int> { ["talent.bastion.rempart.garde_stable"] = 3, ["talent.bastion.rempart.echo_defensif"] = 1 };
        NetMessage[] messages =
        {
            new HelloMessage(1, "Dam é😀", new BuildChoice("build.bastion.rempart")),
            new HelloMessage(1, "Léa", new BuildChoice("build.bastion.gardien", talents)),
            new ReconnectMessage(1, Token),
            new BuildMessage(new BuildChoice("build.bastion.contrecoup", talents)),
            new ReadyMessage(true),
            new ReadyMessage(false),
            new InputMessage(true, false, null, null, null),
            new InputMessage(false, true, 4, "loup_a", "bastion2"),
            new PingMessage(123_456_789),
            new ResyncRequestMessage(),
            new ByeMessage(),
            new WelcomeMessage(1, "bastion2", Token, 50),
            new LobbyMessage(RoomPhase.Lobby, new[] { new SeatInfo("bastion", "Dam", true, true, "Rempart"), new SeatInfo("bastion2", null, false, false, null) }),
            new LobbyMessage(RoomPhase.Ended, Array.Empty<SeatInfo>()),
            new StartMessage("{\"type\":\"header\",\"x\":\"é\\n\"}"),
            new TickMessage(new TickCommands(10, SampleCommands), Hash),
            new TickMessage(new TickCommands(0, Array.Empty<CombatCommand>()), Hash),
            new ResyncMessage("{\"type\":\"header\"}", 42, new[] { new TickCommands(3, SampleCommands), new TickCommands(41, SampleCommands.Take(1).ToArray()) }, Hash),
            new RejectedMessage("Plus de 4 commandes de « bastion » au tick 12."),
            new PongMessage(-5),
            new EndMessage(1200, "durée atteinte"),
            new ErrorMessage("salle pleine"),
        };
        return messages.Select(m => new object[] { m });
    }

    [Theory]
    [MemberData(nameof(AllMessages))]
    public void Chaque_message_relu_a_l_identique_et_refuse_dans_l_autre_sens(NetMessage message)
    {
        string text = MessageCodec.EncodeText(message);
        NetMessage? read = MessageCodec.Decode(Encoding.UTF8.GetBytes(text), message.Direction, out string? error);
        Assert.True(read != null, error);
        Assert.Equal(message.GetType(), read!.GetType());
        Assert.Equal(text, MessageCodec.EncodeText(read));

        MessageDirection other = message.Direction == MessageDirection.ClientToServer ? MessageDirection.ServerToClient : MessageDirection.ClientToServer;
        Assert.Null(MessageCodec.Decode(Encoding.UTF8.GetBytes(text), other, out error));
        Assert.Contains("refusé dans ce sens", error);
    }

    [Fact]
    public void Champs_des_messages_relus()
    {
        var input = (InputMessage)Decode("{\"type\":\"input\",\"guard\":true,\"parry\":false,\"slot\":2,\"target\":\"loup_a\",\"ally\":null}", MessageDirection.ClientToServer);
        Assert.Equal((true, false, 2, "loup_a", (string?)null), (input.Guard, input.Parry, input.Slot, input.Target, input.Ally));
        var tick = (TickMessage)Decode(MessageCodec.EncodeText(new TickMessage(new TickCommands(10, SampleCommands), Hash)), MessageDirection.ServerToClient);
        Assert.Equal(10, tick.Commands.Tick);
        Assert.Equal(SampleCommands.Select(c => (c.AtMs, c.Actor, c.Action, c.Skill, c.Target)),
            tick.Commands.Commands.Select(c => (c.AtMs, c.Actor, c.Action, c.Skill, c.Target)));
        var hello = (HelloMessage)Decode("{\"type\":\"hello\",\"protocol\":1,\"name\":\"Dam\",\"build\":{\"reference\":\"build.bastion.rempart\",\"talents\":{\"talent.a.b\":2}}}",
            MessageDirection.ClientToServer);
        Assert.Equal(2, hello.Build.Talents!["talent.a.b"]);
    }

    private static NetMessage Decode(string json, MessageDirection direction)
    {
        NetMessage? message = MessageCodec.Decode(Encoding.UTF8.GetBytes(json), direction, out string? error);
        Assert.True(message != null, error);
        return message!;
    }

    [Theory]
    [InlineData("[]", "objet attendu")]
    [InlineData("{}", "« type » obligatoire")]
    [InlineData("{\"type\":\"x\"}", "type de message inconnu")]
    [InlineData("{\"type\":3}", "texte")]
    [InlineData("{\"type\":\"ping\"}", "« clientTime » obligatoire")]
    [InlineData("{\"type\":\"ping\",\"clientTime\":1,\"extra\":2}", "champ inconnu")]
    [InlineData("{\"type\":\"ping\",\"clientTime\":1,\"clientTime\":2}", "plusieurs fois")]
    [InlineData("{\"type\":\"ping\",\"clientTime\":1.5}", "")]
    [InlineData("{\"type\":\"hello\",\"protocol\":\"1\",\"name\":\"Dam\",\"build\":{\"reference\":\"build.b.r\"}}", "")]
    [InlineData("{\"type\":\"hello\",\"protocol\":0,\"name\":\"Dam\",\"build\":{\"reference\":\"build.b.r\"}}", "hors de")]
    [InlineData("{\"type\":\"hello\",\"protocol\":1,\"name\":\"\",\"build\":{\"reference\":\"build.b.r\"}}", "texte invalide")]
    [InlineData("{\"type\":\"hello\",\"protocol\":1,\"name\":\"abcdefghijklmnopqrstuvwxy\",\"build\":{\"reference\":\"build.b.r\"}}", "texte invalide")]
    [InlineData("{\"type\":\"hello\",\"protocol\":1,\"name\":\" Dam\",\"build\":{\"reference\":\"build.b.r\"}}", "texte invalide")]
    [InlineData("{\"type\":\"hello\",\"protocol\":1,\"name\":\"D\\u0007m\",\"build\":{\"reference\":\"build.b.r\"}}", "texte invalide")]
    [InlineData("{\"type\":\"hello\",\"protocol\":1,\"name\":\"Dam\",\"build\":{\"reference\":\"build b\"}}", "identifiant invalide")]
    [InlineData("{\"type\":\"hello\",\"protocol\":1,\"name\":\"Dam\",\"build\":{}}", "« reference » obligatoire")]
    [InlineData("{\"type\":\"hello\",\"protocol\":1,\"name\":\"Dam\",\"build\":{\"reference\":\"b.r\",\"talents\":{\"t.a\":0}}}", "hors de [1, 100]")]
    [InlineData("{\"type\":\"hello\",\"protocol\":1,\"name\":\"Dam\",\"build\":{\"reference\":\"b.r\",\"talents\":{\"t.a\":101}}}", "hors de [1, 100]")]
    [InlineData("{\"type\":\"hello\",\"protocol\":1,\"name\":\"Dam\",\"build\":{\"reference\":\"b.r\",\"talents\":{\"t a\":1}}}", "identifiant de talent invalide")]
    [InlineData("{\"type\":\"hello\",\"protocol\":1,\"name\":\"Dam\",\"build\":{\"reference\":\"b.r\",\"talents\":[]}}", "objet attendu")]
    [InlineData("{\"type\":\"reconnect\",\"protocol\":1,\"token\":\"0123456789ABCDEF0123456789ABCDEF\"}", "jeton invalide")]
    [InlineData("{\"type\":\"reconnect\",\"protocol\":1,\"token\":\"0123\"}", "jeton invalide")]
    [InlineData("{\"type\":\"input\",\"guard\":true,\"parry\":false,\"slot\":5,\"target\":null,\"ally\":null}", "hors de [0, 4]")]
    [InlineData("{\"type\":\"input\",\"guard\":true,\"parry\":false,\"slot\":-1,\"target\":null,\"ally\":null}", "hors de [0, 4]")]
    [InlineData("{\"type\":\"input\",\"guard\":true,\"parry\":false,\"slot\":null,\"target\":\"a\\\"b\",\"ally\":null}", "identifiant invalide")]
    [InlineData("{\"type\":\"input\",\"guard\":true,\"parry\":false,\"slot\":null,\"target\":null}", "« ally » obligatoire")]
    [InlineData("{\"type\":\"ready\",\"ready\":1}", "")]
    [InlineData("{\"type\":\"resyncRequest\",\"x\":1}", "champ inconnu")]
    public void Message_client_refuse_avec_sa_raison(string json, string expected) =>
        Refused(json, MessageDirection.ClientToServer, expected);

    [Theory]
    [InlineData("{\"type\":\"tick\",\"tick\":1,\"commands\":[],\"hash\":\"fnv1a64:0123\"}", "empreinte invalide")]
    [InlineData("{\"type\":\"tick\",\"tick\":-1,\"commands\":[],\"hash\":\"fnv1a64:0123456789abcdef\"}", "hors de")]
    [InlineData("{\"type\":\"tick\",\"tick\":1,\"commands\":[{\"atMs\":50,\"actor\":\"a\",\"action\":\"dance\"}],\"hash\":\"fnv1a64:0123456789abcdef\"}", "action inconnue")]
    [InlineData("{\"type\":\"tick\",\"tick\":1,\"commands\":[{\"atMs\":50,\"actor\":\"a\",\"action\":\"skill\",\"skill\":\"s.k\"}],\"hash\":\"fnv1a64:0123456789abcdef\"}", "« target » obligatoire")]
    [InlineData("{\"type\":\"tick\",\"tick\":1,\"commands\":[{\"atMs\":50,\"actor\":\"a\",\"action\":\"parry\",\"skill\":\"s.k\"}],\"hash\":\"fnv1a64:0123456789abcdef\"}", "réservés")]
    [InlineData("{\"type\":\"tick\",\"tick\":1,\"commands\":[{\"atMs\":-50,\"actor\":\"a\",\"action\":\"parry\"}],\"hash\":\"fnv1a64:0123456789abcdef\"}", "hors de")]
    [InlineData("{\"type\":\"resync\",\"header\":\"h\",\"ticks\":5,\"history\":[{\"tick\":3,\"commands\":[]},{\"tick\":3,\"commands\":[]}],\"hash\":\"fnv1a64:0123456789abcdef\"}", "strictement croissants")]
    [InlineData("{\"type\":\"resync\",\"header\":\"h\",\"ticks\":5,\"history\":[{\"tick\":5,\"commands\":[]}],\"hash\":\"fnv1a64:0123456789abcdef\"}", "strictement croissants")]
    [InlineData("{\"type\":\"resync\",\"header\":\"\",\"ticks\":5,\"history\":[],\"hash\":\"fnv1a64:0123456789abcdef\"}", "en-tête vide")]
    [InlineData("{\"type\":\"lobby\",\"phase\":\"pause\",\"seats\":[]}", "phase inconnue")]
    [InlineData("{\"type\":\"lobby\",\"phase\":\"lobby\",\"seats\":[{\"id\":\"a\",\"player\":null,\"connected\":true,\"ready\":false}]}", "« build » obligatoire")]
    [InlineData("{\"type\":\"welcome\",\"protocol\":1,\"seat\":\"bastion\",\"token\":\"0123456789abcdef0123456789abcdef\",\"tickMs\":0}", "hors de")]
    public void Message_serveur_refuse_avec_sa_raison(string json, string expected) =>
        Refused(json, MessageDirection.ServerToClient, expected);

    private static void Refused(string json, MessageDirection direction, string expected)
    {
        Assert.Null(MessageCodec.Decode(Encoding.UTF8.GetBytes(json), direction, out string? error));
        Assert.False(string.IsNullOrEmpty(error));
        Assert.Contains(expected, error, StringComparison.Ordinal);
    }

    [Fact]
    public void Listes_et_textes_bornes()
    {
        string talents = string.Join(",", Enumerable.Range(0, Protocol.MaxTalents + 1).Select(i => $"\"t.a{i}\":1"));
        Refused("{\"type\":\"build\",\"build\":{\"reference\":\"b.r\",\"talents\":{" + talents + "}}}", MessageDirection.ClientToServer, "au plus 256 talents");
        string commands = string.Join(",", Enumerable.Repeat("{\"atMs\":50,\"actor\":\"a\",\"action\":\"parry\"}", Protocol.MaxCommandsPerTick + 1));
        Refused("{\"type\":\"tick\",\"tick\":1,\"commands\":[" + commands + "],\"hash\":\"" + Hash + "\"}", MessageDirection.ServerToClient, "au plus 256 par tick");
        string seats = string.Join(",", Enumerable.Repeat("{\"id\":\"a\",\"player\":null,\"connected\":false,\"ready\":false,\"build\":null}", Protocol.MaxSeats + 1));
        Refused("{\"type\":\"lobby\",\"phase\":\"lobby\",\"seats\":[" + seats + "]}", MessageDirection.ServerToClient, "au plus 8");
        Refused("{\"type\":\"error\",\"reason\":\"" + new string('x', Protocol.MaxReasonLength + 1) + "\"}", MessageDirection.ServerToClient, "texte invalide");
        Refused("{\"type\":\"hello\",\"protocol\":1,\"name\":\"Dam\",\"build\":{\"reference\":\"" + new string('a', Protocol.MaxIdLength + 1) + "\"}}",
            MessageDirection.ClientToServer, "identifiant invalide");
        // Demi-paire de substitution : refusée dès l'analyse (sous-lot 9.1).
        Refused("{\"type\":\"hello\",\"protocol\":1,\"name\":\"D\\ud800m\",\"build\":{\"reference\":\"b.r\"}}", MessageDirection.ClientToServer, "JSON invalide");
        Assert.Null(MessageCodec.Decode(new byte[] { 0xFF, 0xFE }, MessageDirection.ClientToServer, out _));
    }

    /// <summary>Octets altérés au hasard dans des messages valides : la lecture ne lève jamais d'exception.</summary>
    [Property(MaxTest = 500)]
    public bool Lecture_sans_exception_sur_des_octets_alteres(int seed, byte value, int position)
    {
        object[][] all = AllMessages().ToArray();
        var random = new Random(seed);
        var message = (NetMessage)all[random.Next(all.Length)][0];
        var bytes = MessageCodec.Encode(message).ToList();
        int at = (int)((uint)position % (uint)bytes.Count);
        switch ((uint)seed % 3)
        {
            case 0: bytes[at] = value; break;
            case 1: bytes.Insert(at, value); break;
            default: bytes.RemoveAt(at); break;
        }
        foreach (MessageDirection direction in new[] { MessageDirection.ClientToServer, MessageDirection.ServerToClient })
        {
            NetMessage? read = MessageCodec.Decode(bytes.ToArray(), direction, out string? error);
            if (read is null && string.IsNullOrEmpty(error)) return false;
        }
        return true;
    }

    // --- Découpage du flux -------------------------------------------------------------------------------------------------

    [Fact]
    public void Messages_encadres_et_reconstitues_quel_que_soit_le_decoupage()
    {
        byte[] frame = Framing.Frame(new byte[] { 1, 2, 3 });
        Assert.Equal(new byte[] { 0, 0, 0, 3, 1, 2, 3 }, frame);
        Assert.Equal(new byte[] { 0, 1, 0, 0 }, Framing.Frame(new byte[65536]).Take(4));
        Assert.Throws<ArgumentException>(() => Framing.Frame(Array.Empty<byte>()));

        byte[][] payloads = Enumerable.Range(1, 20).Select(n => Enumerable.Range(0, n * 7).Select(i => (byte)(i * n)).ToArray()).ToArray();
        byte[] stream = payloads.SelectMany(Framing.Frame).ToArray();
        foreach (int chunk in new[] { 1, 2, 3, 5, 64, stream.Length })
        {
            var reader = new FrameReader(1024);
            var received = new List<byte[]>();
            for (int offset = 0; offset < stream.Length; offset += chunk)
                received.AddRange(reader.Push(stream, offset, Math.Min(chunk, stream.Length - offset)));
            Assert.Null(reader.Error);
            Assert.Equal(payloads, received);
        }
    }

    [Theory]
    [InlineData(new byte[] { 0, 0, 0, 0 }, "message vide")]
    [InlineData(new byte[] { 0, 0, 4, 1 }, "1025 octets annoncé, au plus 1024")]
    [InlineData(new byte[] { 0x7F, 0xFF, 0xFF, 0xFF, 1, 2 }, "2147483647 octets annoncé")]
    [InlineData(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, "4294967295 octets annoncé")]
    public void En_tete_de_message_fautif_refuse_avant_lecture(byte[] bytes, string expected)
    {
        var reader = new FrameReader(1024);
        Assert.Empty(reader.Push(bytes, 0, bytes.Length));
        Assert.Contains(expected, reader.Error);
        // Après une faute, plus rien n'est lu, même un message correct.
        byte[] valid = Framing.Frame(new byte[] { 1 });
        Assert.Empty(reader.Push(valid, 0, valid.Length));
    }

    // --- Réseau simulé -----------------------------------------------------------------------------------------------------

    private static byte[] Bytes(int n) => new[] { (byte)n };

    [Fact]
    public void Reseau_simule_latence_ordre_et_determinisme()
    {
        var net = new SimulatedNetwork(7);
        SimulatedListener server = net.Listen("salle");
        SimulatedConnection c = net.Connect("salle", new LinkProfile(50, 30));
        Assert.True(server.TryAccept(out INetConnection s));
        Assert.False(server.TryAccept(out _));
        for (int i = 0; i < 20; i++) Assert.True(c.Send(Bytes(i)));
        net.Advance(0.049);
        Assert.False(s.TryReceive(out _));          // aucun message avant la latence
        net.Advance(0.032);                         // latence + gigue maximale écoulées
        var received = new List<byte>();
        while (s.TryReceive(out byte[] payload)) received.Add(payload[0]);
        Assert.Equal(Enumerable.Range(0, 20).Select(i => (byte)i), received);   // ordre conservé malgré la gigue

        // Même graine, mêmes délais : la suite des arrivées est identique ; autre graine, autre suite.
        Assert.Equal(DeliveryTimes(3), DeliveryTimes(3));
        Assert.NotEqual(DeliveryTimes(3), DeliveryTimes(4));
    }

    private static double[] DeliveryTimes(ulong seed)
    {
        var net = new SimulatedNetwork(seed);
        SimulatedListener server = net.Listen("salle");
        SimulatedConnection c = net.Connect("salle", new LinkProfile(20, 40));
        server.TryAccept(out INetConnection s);
        var times = new List<double>();
        for (int i = 0; i < 30; i++)
        {
            c.Send(Bytes(i));
            for (int step = 0; step < 10; step++)
            {
                net.Advance(0.001);
                while (s.TryReceive(out _)) times.Add(Math.Round(net.Now, 6));
            }
        }
        net.Advance(1);
        while (s.TryReceive(out _)) times.Add(Math.Round(net.Now, 6));
        Assert.Equal(30, times.Count);
        return times.ToArray();
    }

    [Fact]
    public void Reseau_simule_fermeture_coupure_et_limite_de_taille()
    {
        var net = new SimulatedNetwork(1);
        SimulatedListener server = net.Listen("salle", maxReceivePayload: 8);
        SimulatedConnection c = net.Connect("salle", new LinkProfile(10, 0));
        server.TryAccept(out INetConnection s);

        // Fermeture : l'autre côté lit d'abord les messages déjà partis, puis apprend la fermeture.
        c.Send(Bytes(1));
        c.Close("au revoir");
        Assert.False(c.IsOpen);
        Assert.False(c.Send(Bytes(2)));
        Assert.True(s.IsOpen);
        net.Advance(0.02);
        Assert.True(s.TryReceive(out byte[] last));
        Assert.Equal(1, last[0]);
        Assert.False(s.IsOpen);
        Assert.Equal("connexion fermée par l'autre côté.", s.CloseReason);

        // Coupure : les deux côtés fermés tout de suite, messages en route perdus.
        SimulatedConnection c2 = net.Connect("salle", new LinkProfile(10, 0));
        server.TryAccept(out INetConnection s2);
        c2.Send(Bytes(9));
        net.Cut(c2);
        Assert.Equal(0, net.InFlightCount);
        Assert.False(c2.IsOpen);
        Assert.False(s2.IsOpen);
        net.Advance(1);
        Assert.False(s2.TryReceive(out _));

        // Message plus grand que ce que le serveur accepte : faute de protocole, les deux côtés fermés.
        SimulatedConnection c3 = net.Connect("salle", new LinkProfile(10, 0));
        server.TryAccept(out INetConnection s3);
        Assert.False(c3.Send(new byte[9]));
        Assert.False(c3.IsOpen);
        Assert.Contains("faute de protocole", s3.CloseReason);

        // Adresse sans écoute.
        Assert.False(net.Connect("ailleurs", LinkProfile.Perfect).IsOpen);
    }

    [Fact]
    public void Reseau_simule_ecoute_arretee_et_lecteur_qui_ne_lit_plus()
    {
        // Revue Codex de la PR #18 : une écoute arrêtée coupe les connexions pas encore acceptées et libère son adresse.
        var net = new SimulatedNetwork(1);
        SimulatedListener first = net.Listen("salle");
        SimulatedConnection waiting = net.Connect("salle", new LinkProfile(10, 0));
        Assert.True(waiting.Send(Bytes(1)));
        first.Dispose();
        Assert.False(waiting.IsOpen);
        Assert.Equal("écoute arrêtée.", waiting.CloseReason);
        Assert.False(waiting.Send(Bytes(2)));
        Assert.Equal(0, net.InFlightCount);
        Assert.False(first.TryAccept(out _));
        Assert.False(net.Connect("salle", LinkProfile.Perfect).IsOpen);
        first.Dispose();   // sans effet la seconde fois

        SimulatedListener second = net.Listen("salle");
        SimulatedConnection client = net.Connect("salle", LinkProfile.Perfect);
        Assert.True(second.TryAccept(out INetConnection server));

        // Comme en TCP, un côté qui ne lit plus est coupé au-delà de 4 096 messages en attente, au lieu de grossir sans fin.
        for (int i = 0; i < TcpNetConnection.MaxPendingReceived; i++) Assert.True(client.Send(Bytes(i)));
        net.Advance(0);
        Assert.True(server.IsOpen);
        Assert.True(client.Send(Bytes(0)));
        net.Advance(0);
        Assert.False(server.IsOpen);
        Assert.False(client.IsOpen);
        Assert.Equal("trop de messages en attente de lecture.", server.CloseReason);
    }

    [Fact]
    public void Reseau_simule_file_d_envoi_bornee_en_messages_et_en_octets()
    {
        // Revue Codex de la PR #18 (tour 2) : sans avancer le réseau, un émetteur ne peut pas accumuler des messages en route sans fin.
        var net = new SimulatedNetwork(3);
        SimulatedListener listener = net.Listen("salle");
        SimulatedConnection client = net.Connect("salle", new LinkProfile(100, 0));
        Assert.True(listener.TryAccept(out INetConnection server));
        for (int i = 0; i < TcpNetConnection.MaxPendingSend; i++) Assert.True(client.Send(Bytes(i)));
        Assert.Equal(TcpNetConnection.MaxPendingSend, net.InFlightCount);
        Assert.False(client.Send(Bytes(0)));
        Assert.False(client.IsOpen);
        Assert.Equal("file d'envoi pleine : l'autre côté ne lit plus.", client.CloseReason);
        // Les messages déjà partis arrivent, puis la fin du flux ; rien n'est resté en route.
        net.Advance(1);
        Assert.Equal(0, net.InFlightCount);
        for (int i = 0; i < TcpNetConnection.MaxPendingSend; i++)
        {
            Assert.True(server.TryReceive(out byte[] got));
            Assert.Equal(Bytes(i), got);
        }
        Assert.False(server.TryReceive(out _));
        Assert.Equal("connexion fermée par l'autre côté.", server.CloseReason);

        // En octets : 64 Mio en route au plus (16 messages de 4 Mio dans le sens serveur → client).
        var bytesNet = new SimulatedNetwork(4);
        SimulatedListener bytesListener = bytesNet.Listen("salle");
        bytesNet.Connect("salle", new LinkProfile(100, 0));
        Assert.True(bytesListener.TryAccept(out INetConnection writer));
        var big = new byte[Protocol.MaxServerMessageBytes];
        int fit = (int)(TcpNetConnection.MaxPendingBytes / big.Length);
        Assert.Equal(16, fit);
        for (int i = 0; i < fit; i++) Assert.True(writer.Send(big));
        Assert.False(writer.Send(Bytes(1)));
        Assert.Equal("file d'envoi pleine : l'autre côté ne lit plus.", writer.CloseReason);

        // Le compte baisse à la livraison : un émetteur au rythme du réseau n'est jamais coupé.
        var steady = new SimulatedNetwork(5);
        SimulatedListener steadyListener = steady.Listen("salle");
        SimulatedConnection talker = steady.Connect("salle", new LinkProfile(100, 0));
        Assert.True(steadyListener.TryAccept(out INetConnection hearer));
        for (int i = 0; i < 3 * TcpNetConnection.MaxPendingSend; i++)
        {
            Assert.True(talker.Send(Bytes(i)), $"envoi {i}");
            steady.Advance(0.05);
            while (hearer.TryReceive(out _)) { }
        }
        Assert.True(talker.IsOpen);
        Assert.True(hearer.IsOpen);

        // L'écoute simulée range au plus 64 connexions pas encore acceptées, comme l'écoute TCP.
        var crowd = new SimulatedNetwork(6);
        SimulatedListener door = crowd.Listen("salle");
        for (int i = 0; i < TcpNetListener.MaxPendingAccepted; i++) Assert.True(crowd.Connect("salle", LinkProfile.Perfect).IsOpen);
        SimulatedConnection late = crowd.Connect("salle", LinkProfile.Perfect);
        Assert.False(late.IsOpen);
        Assert.Equal("connexion refusée : écoute saturée.", late.CloseReason);
        Assert.True(door.TryAccept(out _));
        Assert.True(crowd.Connect("salle", LinkProfile.Perfect).IsOpen);
    }

    // --- TCP ---------------------------------------------------------------------------------------------------------------

    /// <summary>Attend une condition (fils TCP) au plus 10 s.</summary>
    private static void Eventually(Func<bool> condition, string what)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.ElapsedMilliseconds > 10_000) throw new TimeoutException(what);
            Thread.Sleep(5);
        }
    }

    private static NetMessage Receive(INetConnection connection, MessageDirection direction)
    {
        NetMessage? message = null;
        string? error = null;
        Eventually(() => connection.TryReceive(direction, out message, out error), "message attendu");
        Assert.True(message != null, error);
        return message!;
    }

    [Fact]
    public async Task TCP_messages_dans_les_deux_sens_dans_l_ordre()
    {
        using var listener = new TcpNetListener(IPAddress.Loopback, 0);
        using TcpNetConnection client = await TcpNetConnection.ConnectAsync("127.0.0.1", listener.Port, Protocol.MaxServerMessageBytes, Protocol.MaxClientMessageBytes);
        INetConnection server = null!;
        Eventually(() => listener.TryAccept(out server), "connexion acceptée");
        using (server)
        {
            Assert.True(client.Send(new HelloMessage(1, "Dam", new BuildChoice("build.bastion.rempart"))));
            Assert.IsType<HelloMessage>(Receive(server, MessageDirection.ClientToServer));
            for (int i = 0; i < 200; i++) Assert.True(client.Send(new PingMessage(i)));
            for (int i = 0; i < 200; i++) Assert.Equal(i, ((PingMessage)Receive(server, MessageDirection.ClientToServer)).ClientTime);

            // Un gros message du serveur (historique) passe en un seul morceau logique.
            string header = new string('h', 1_000_000);
            Assert.True(server.Send(new ResyncMessage(header, 1, Array.Empty<TickCommands>(), Hash)));
            Assert.Equal(header, ((ResyncMessage)Receive(client, MessageDirection.ServerToClient)).Header);

            // Fermeture par le serveur : le message d'erreur arrive avant la fin.
            server.Send(new ErrorMessage("salle pleine"));
            server.Close("salle pleine");
            Assert.Equal("salle pleine", ((ErrorMessage)Receive(client, MessageDirection.ServerToClient)).Reason);
            Eventually(() => !client.IsOpen, "fermeture vue du client");
            Assert.Equal("connexion fermée par l'autre côté.", client.CloseReason);
        }
    }

    [Fact]
    public async Task TCP_message_trop_grand_ou_illisible_ferme_la_connexion()
    {
        using var listener = new TcpNetListener(IPAddress.Loopback, 0);
        using TcpNetConnection client = await TcpNetConnection.ConnectAsync("127.0.0.1", listener.Port, Protocol.MaxServerMessageBytes, Protocol.MaxClientMessageBytes);
        INetConnection server = null!;
        Eventually(() => listener.TryAccept(out server), "connexion acceptée");
        // Côté client : un message plus grand que ce que le serveur accepte n'est pas envoyé, la connexion est fermée.
        Assert.False(client.Send(new byte[Protocol.MaxClientMessageBytes + 1]));
        Assert.False(client.IsOpen);
        Assert.Contains("l'autre côté en accepte 8192 au plus", client.CloseReason);
        server.Dispose();

        // Un pair malveillant qui annonce 1 Gio : le serveur ferme avant de lire ou d'allouer.
        using var raw = new TcpClient();
        raw.Connect(IPAddress.Loopback, listener.Port);
        INetConnection victim = null!;
        Eventually(() => listener.TryAccept(out victim), "connexion acceptée");
        using (victim)
        {
            raw.GetStream().Write(new byte[] { 0x40, 0, 0, 0 }, 0, 4);
            Eventually(() => !victim.IsOpen, "fermeture pour faute de protocole");
            Assert.Contains("faute de protocole : message de 1073741824 octets annoncé, au plus 8192", victim.CloseReason);
        }

        // Un message encadré mais illisible : refusé et connexion fermée par la lecture typée.
        using TcpNetConnection other = await TcpNetConnection.ConnectAsync("127.0.0.1", listener.Port, Protocol.MaxServerMessageBytes, Protocol.MaxClientMessageBytes);
        INetConnection third = null!;
        Eventually(() => listener.TryAccept(out third), "connexion acceptée");
        using (third)
        {
            other.Send(Encoding.UTF8.GetBytes("{\"type\":\"welcome\"}"));
            NetMessage? message = null;
            string? error = null;
            Eventually(() => third.TryReceive(MessageDirection.ClientToServer, out message, out error), "message reçu");
            Assert.Null(message);
            Assert.Contains("refusé dans ce sens", error);
            Assert.False(third.IsOpen);
        }
    }

    [Fact]
    public async Task TCP_ecoute_arretee_ne_rend_plus_aucune_connexion()
    {
        // Revue Codex de la PR #18 (tour 2) : des connexions arrivent pendant l'arrêt ; aucune ne survit à l'écoute ni n'est rendue après.
        var listener = new TcpNetListener(IPAddress.Loopback, 0);
        int port = listener.Port;
        Task<TcpNetConnection>[] connecting = Enumerable.Range(0, 16)
            .Select(_ => TcpNetConnection.ConnectAsync("127.0.0.1", port, Protocol.MaxServerMessageBytes, Protocol.MaxClientMessageBytes))
            .ToArray();
        Thread.Sleep(20);
        listener.Dispose();
        Assert.False(listener.TryAccept(out _));
        listener.Dispose();   // sans effet la seconde fois
        foreach (Task<TcpNetConnection> attempt in connecting)
        {
            TcpNetConnection client;
            try
            {
                client = await attempt;
            }
            catch (SocketException)
            {
                continue;   // refusée : l'écoute était déjà arrêtée
            }
            using (client)
                Eventually(() => !client.IsOpen, "connexion fermée par l'écoute arrêtée");
        }
        Thread.Sleep(50);
        Assert.False(listener.TryAccept(out _));
    }

    [Fact]
    public async Task TCP_connexion_refusee_sans_serveur()
    {
        int port;
        using (var probe = new TcpListener(IPAddress.Loopback, 0))
        {
            probe.Start();
            port = ((IPEndPoint)probe.LocalEndpoint).Port;
        }
        await Assert.ThrowsAnyAsync<SocketException>(() => TcpNetConnection.ConnectAsync("127.0.0.1", port, 1024, 1024));
    }
}
