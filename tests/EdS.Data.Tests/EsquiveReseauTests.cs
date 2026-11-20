using System.Text;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Space;
using EdS.Net;
using EdS.Runtime;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.Lot103ClientTests;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Esquive (décision de Damien du 14 novembre 2026) côté temps réel et réseau : intentions (<see cref="IntentionPlanner"/>), pont temps réel
/// (<see cref="RealtimeCombat.Dodge"/>), protocole 5 (<c>dodge</c> dans les intentions, action <c>dodge</c> dans les commandes) et salle
/// (le serveur reste seul juge).
/// </summary>
[Trait("Category", "Network")]
[Trait("Category", "Runtime")]
public class EsquiveReseauTests
{
    private const string Hash = "fnv1a64:0123456789abcdef";

    /// <summary>Scénario de l'esquive sans ses commandes : l'ogre frappe, le personnage est piloté.</summary>
    private static CombatSetup Arena()
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ScenarioPath("espace_esquive")))!.AsObject();
        root["data"]!["commands"] = new JsonArray();
        ScenarioLoadResult result = ScenarioJsonReader.Read(root.ToJsonString(), ShippedData);
        Assert.True(result.Setup != null, result.Report.ToString());
        return result.Setup!;
    }

    // --- Protocole 5 ----------------------------------------------------------------------------------------

    [Fact]
    public void Protocole_5_intention_et_commandes_d_esquive_relues_a_l_identique()
    {
        Assert.Equal(6, Protocol.Version);   // V2.44.1 : protocole 6 garde l'esquive
        var input = new InputMessage(false, false, null, null, null, 16, dodge: true);
        string text = MessageCodec.EncodeText(input);
        var read = (InputMessage)MessageCodec.Decode(Encoding.UTF8.GetBytes(text), MessageDirection.ClientToServer, out string? error)!;
        Assert.True(read != null, error);
        Assert.Equal((true, (int?)16), (read!.Dodge, read.Heading));
        Assert.Equal(text, MessageCodec.EncodeText(read));

        CombatCommand[] commands = { CombatCommand.DodgeToward(550, "bastion", GroundVector.Parse("0", "1")), CombatCommand.DodgeToward(600, "bastion2", null) };
        string tick = MessageCodec.EncodeText(new TickMessage(new TickCommands(11, commands), Hash));
        var back = (TickMessage)MessageCodec.Decode(Encoding.UTF8.GetBytes(tick), MessageDirection.ServerToClient, out error)!;
        Assert.True(back != null, error);
        Assert.Equal(commands.Select(c => (c.AtMs, c.Actor, c.Action, c.Direction)), back!.Commands.Commands.Select(c => (c.AtMs, c.Actor, c.Action, c.Direction)));
        Assert.Equal(tick, MessageCodec.EncodeText(back));
    }

    [Theory]
    [InlineData("{\"type\":\"input\",\"guard\":true,\"parry\":false,\"slot\":null,\"target\":null,\"ally\":null,\"heading\":null,\"backward\":false}", "« dodge » obligatoire")]
    [InlineData("{\"type\":\"input\",\"guard\":true,\"parry\":false,\"slot\":null,\"target\":null,\"ally\":null,\"heading\":null,\"backward\":false,\"dodge\":1}", "dodge")]
    public void Intention_sans_dodge_refusee(string json, string expected)
    {
        Assert.Null(MessageCodec.Decode(Encoding.UTF8.GetBytes(json), MessageDirection.ClientToServer, out string? error));
        Assert.Contains(expected, error);
    }

    [Theory]
    [InlineData("{\"atMs\":50,\"actor\":\"b\",\"action\":\"dodge\",\"backward\":true}")]
    [InlineData("{\"atMs\":50,\"actor\":\"b\",\"action\":\"dodge\",\"target\":null}")]
    [InlineData("{\"atMs\":50,\"actor\":\"b\",\"action\":\"dodge\",\"direction\":[0,0]}")]
    public void Commande_dodge_mal_formee_refusee(string command)
    {
        string json = "{\"type\":\"tick\",\"tick\":1,\"commands\":[" + command + "],\"hash\":\"" + Hash + "\"}";
        Assert.Null(MessageCodec.Decode(Encoding.UTF8.GetBytes(json), MessageDirection.ServerToClient, out _));
    }

    // --- Intentions ---------------------------------------------------------------------------------------------

    [Fact]
    public void Planificateur_quitte_la_garde_pour_bondir_et_ne_la_reprend_pas_au_meme_tick()
    {
        var engine = new CombatEngine(Arena(), new MemoryCombatLog());
        engine.Submit(new CombatCommand(0, "bastion", CombatAction.GuardStart));
        engine.Step();
        CombatantView me = engine.View("bastion");
        Assert.True(me.Guarding && me.CanDodge);
        IReadOnlyList<CombatCommand[]> groups = IntentionPlanner.Plan(me, 50, guardHeld: true, parry: false, slot: null, _ => null, dodge: true,
            dodgeDirection: GroundVector.Parse("1", "0"));
        CombatCommand[] group = Assert.Single(groups);
        Assert.Equal(new[] { CombatAction.GuardStop, CombatAction.Dodge }, group.Select(c => c.Action));
        Assert.Equal(GroundVector.Parse("1", "0"), group[1].Direction);
        // Sans demande : la garde tenue reste (aucune commande) ; une créature n'esquive jamais.
        Assert.Empty(IntentionPlanner.Plan(me, 50, true, false, null, _ => null));
        Assert.False(engine.View("ogre").CanDodge);
    }

    [Fact]
    public void Planificateur_garde_la_garde_quand_l_esquive_serait_refusee()
    {
        // Relecture indépendante de la PR #164 : en recharge (ou sans endurance), l'esquive serait refusée ; la garde tenue n'est pas lâchée.
        var engine = new CombatEngine(Arena(), new MemoryCombatLog());
        engine.Submit(CombatCommand.DodgeToward(0, "bastion", GroundVector.Parse("0", "1")));
        engine.Step();
        engine.Submit(new CombatCommand(50, "bastion", CombatAction.GuardStart));
        engine.Step();
        CombatantView me = engine.View("bastion");
        Assert.True(me.Guarding && me.CanDodge);
        Assert.False(me.DodgeAvailable);   // recharge jusqu'au tick 20
        Assert.Empty(IntentionPlanner.Plan(me, 100, guardHeld: true, parry: false, slot: null, _ => null, dodge: true));
        while (engine.CurrentTick < me.DodgeReadyTick) engine.Step();
        Assert.True(engine.View("bastion").DodgeAvailable);
    }

    [Fact]
    public void Pont_temps_reel_bond_dans_le_cap_du_joystick_ou_vers_l_arriere()
    {
        var combat = new RealtimeCombat(Arena(), "bastion");
        combat.SetMove(0, 1);
        combat.Dodge();
        combat.StepOnce();
        CombatCommand dodge = Assert.Single(combat.SubmittedCommands, c => c.Action == CombatAction.Dodge);
        Assert.Equal(MoveInput.Direction(MoveInput.Heading(0, 1)!.Value), dodge.Direction);
        // Joystick au repos, recharge passée : vers l'arrière.
        combat.SetMove(0, 0);
        for (int i = 0; i < 25; i++) combat.StepOnce();
        combat.Dodge();
        combat.StepOnce();
        Assert.Equal(2, combat.SubmittedCommands.Count(c => c.Action == CombatAction.Dodge));
        Assert.Null(combat.SubmittedCommands.Last(c => c.Action == CombatAction.Dodge).Direction);
        Assert.True(combat.Player().View.DodgeReadyTick > combat.Tick);
    }

    // --- Salle ------------------------------------------------------------------------------------------------

    [Fact]
    public void Salle_le_serveur_applique_l_esquive_et_le_client_la_voit()
    {
        var game = new Game(Lot117DuoTests.Vallons(6_000));
        bool asked = false;
        game.Script = client =>
        {
            if (client.Status != ClientStatus.Combat || client.Combat is null || client.SeatId != "bastion") return;
            NetworkCombat combat = client.Combat;
            if (!asked && combat.Tick >= 10)
            {
                combat.SetMove(1, 0);
                combat.Dodge();
                asked = true;
            }
        };
        (NetworkClient a, NetworkClient b) = game.Start();
        game.RunUntil(() => a.Status == ClientStatus.Lobby && b.Status == ClientStatus.Lobby, 30);
        Assert.True(asked);
        Assert.Contains(game.Room.LastJournal!, l => l.Contains("\"event\":\"dodgeStarted\"") && l.Contains("\"actor\":\"bastion\""));
        Assert.Contains(ServerCommands(game), c => c.Action == CombatAction.Dodge && c.Actor == "bastion" && c.Direction != null);
        NetworkCombat seen = a.Combat!;
        Assert.True(seen.IsFinished);
        Assert.Equal((0, 0), (seen.Desyncs, seen.Resyncs));
        Assert.Equal(ServerHash(game, seen), seen.Hash);   // le client a suivi le serveur, esquive comprise
    }
}
