using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Net;
using EdS.Runtime;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 11.1 : des assaillants mortels en réseau. Le serveur résout les morts ; chaque client les rejoue à l'identique (même
/// empreinte), change de cible quand la sienne meurt, et voit la victoire des joueurs ; le journal rejouable du serveur porte les PV.
/// En solo, le pont temps réel fait de même.
/// </summary>
[Trait("Category", "Network")]
public class Lot111NetworkTests
{
    /// <summary>Coopération livrée dont les trois assaillants ont <paramref name="health"/> PV.</summary>
    private static CombatSetup MortalPack(long health)
    {
        ScenarioLoadResult loaded = ScenarioJsonReader.ReadFile(ScenarioPath("coop/duo_meute"), ShippedData);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        JsonObject root = JsonNode.Parse(File.ReadAllText(ScenarioPath("coop/duo_meute")))!.AsObject();
        foreach (JsonObject c in DataOf(root)["combatants"]!.AsArray().Select(c => c!.AsObject()).Where(c => c["type"]!.GetValue<string>() == "assailant"))
            c["health"] = health;
        ScenarioLoadResult mortal = ScenarioJsonReader.Read(root.ToJsonString(), ShippedData);
        Assert.True(mortal.IsValid, mortal.Report.ToString());
        return mortal.Setup!;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Deux_robots_tuent_la_meute_et_les_clients_suivent_le_serveur(int latencyMs)
    {
        int tokens = 0;
        var net = new SimulatedNetwork(11);
        SimulatedListener listener = net.Listen("salle");
        var room = new CombatRoom(ShippedData, MortalPack(250), new RoomOptions { TokenSource = () => (++tokens).ToString("x32") });
        var link = new LinkProfile(latencyMs, 0);
        var clients = new[] { "Dam", "Robot" }.Select((name, i) =>
            new NetworkClient(ShippedData, net.Connect("salle", link), name, new BuildChoice(i == 0 ? "build.bastion.contrecoup" : "build.bastion.rempart"),
                new NetworkClientOptions())).ToArray();
        BotPlayer[] bots = clients.Select(c => new BotPlayer(c)).ToArray();

        for (double t = 0; t < 70 && room.LastResult is null; t += 0.01)
        {
            net.Advance(0.01);
            while (listener.TryAccept(out INetConnection connection)) room.Accept(connection);
            room.Advance(0.01);
            foreach (BotPlayer bot in bots) bot.Advance(0.01);
        }
        // Les derniers ticks arrivent aux clients après la fin côté serveur.
        for (int i = 0; i < 100 && clients.Any(c => c.Combat is { IsFinished: false }); i++)
        {
            net.Advance(0.01);
            foreach (BotPlayer bot in bots) bot.Advance(0.01);
        }

        CombatResult server = Assert.IsType<CombatResult>(room.LastResult);
        Assert.Equal((CombatEndReason.TeamDefeated, "joueurs"), (server.EndReason, server.Winner));
        Assert.All(new[] { "loup_a", "loup_b", "hurleur" }, id => Assert.True(server[id].IsDead, id));
        Assert.True(server.EndTick < 60_000 / 50 - 1, "victoire avant la fin du temps imparti");
        foreach (NetworkClient client in clients)
        {
            NetworkCombat combat = client.Combat!;
            Assert.True(combat.IsFinished);
            Assert.Equal(0, combat.Desyncs);
            Assert.Equal(HashingCombatLog.Of(new[] { combat.Header }.Concat(room.LastJournal!.Skip(1))), combat.Hash);
            Assert.Equal((server.Winner, server.EndTick), (combat.Result!.Winner, combat.Result.EndTick));
            Assert.Null(combat.TargetId);   // plus aucun ennemi vivant : plus de cible
        }
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(room.LastJournal!, ShippedData).Status);
        Assert.Contains("\"health\":250", room.LastJournal![0], StringComparison.Ordinal);
    }

    [Fact]
    public void En_solo_la_cible_passe_au_loup_suivant_et_la_victoire_termine_le_combat()
    {
        var combat = new RealtimeCombat(MortalPack(120), "bastion");
        string first = combat.TargetId!;
        var seen = new List<string?> { first };
        for (int step = 0; step < 60 * 20 && !combat.IsFinished; step++)
        {
            if (step % 4 == 0) combat.UseSlot(step / 4 % 5);
            combat.Advance(0.05);
            if (combat.TargetId != seen[^1]) seen.Add(combat.TargetId);
        }
        Assert.True(combat.IsFinished);
        Assert.Equal("joueurs", combat.Result!.Winner);
        Assert.True(seen.Count >= 3, "la cible change quand la précédente meurt : " + string.Join(", ", seen));
        Assert.Null(seen[^1]);
        Assert.All(combat.Others().Where(o => o.IsEnemy), o => Assert.Null(o.NextAttackSeconds));   // morts : plus de prochain coup
    }
}
