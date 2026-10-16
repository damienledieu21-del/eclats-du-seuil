using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Engine;

/// <summary>Ordre stable des événements simultanés et des phases d'un tick.</summary>
[Trait("Category", "Engine")]
public class OrderingTests
{
    [Fact]
    public void Initiative_decroissante_puis_identifiant_quel_que_soit_l_ordre_du_scenario()
    {
        CombatantSetup[] combatants =
        {
            Character("alpha", initiative: 0), Character("beta", initiative: 5), Character("gamma", initiative: 0), Dummy(),
        };
        var commands = new[] { Cmd(0, Frappe, "gamma"), Cmd(0, Frappe, "alpha"), Cmd(0, Frappe, "beta") };
        var casts = Events(Run(Setup(commands, combatants)).Log).OfKind("skillCast").Select(e => e.Str("actor"));
        Assert.Equal(new[] { "beta", "alpha", "gamma" }, casts);

        // Ordre du scénario inversé : même journal d'événements, au numéro de commande près (champ command, racine command:<n>).
        var reversed = Run(Setup(commands.Reverse(), combatants)).Log.Lines.Skip(1)
            .Select(l => System.Text.RegularExpressions.Regex.Replace(l, "\"command\":\\d+,|\"root\":\"command:\\d+\",", ""));
        var original = Run(Setup(commands, combatants)).Log.Lines.Skip(1)
            .Select(l => System.Text.RegularExpressions.Regex.Replace(l, "\"command\":\\d+,|\"root\":\"command:\\d+\",", ""));
        Assert.Equal(original, reversed);
    }

    [Fact]
    public void Commandes_simultanees_d_un_meme_acteur_dans_l_ordre_du_scenario()
    {
        var events = Events(Run(Setup(new[] { Cmd(0, Heurt), Cmd(0, Frappe) })).Log);
        Assert.Equal(Heurt, Assert.Single(events.OfKind("skillCast")).Str("skill"));
        Assert.Equal("globalCooldown", Assert.Single(events.OfKind("commandRejected")).Str("reason"));
    }

    [Fact]
    public void Phases_dans_l_ordre_et_sequence_croissante()
    {
        var events = Events(Run(Setup(new[] { Cmd(0, Frappe), Cmd(6000, Frappe) })).Log);
        string[] phaseOrder = { "setup", "expirations", "commands", "resolution", "periodic", "regeneration", "endOfTick" };
        for (int i = 1; i < events.Count; i++)
        {
            Assert.Equal(events[i - 1].Int("seq") + 1, events[i].Int("seq"));
            Assert.True(events[i - 1].Tick() <= events[i].Tick());
            if (events[i - 1].Tick() == events[i].Tick())
                Assert.True(Array.IndexOf(phaseOrder, events[i - 1].Str("phase")) <= Array.IndexOf(phaseOrder, events[i].Str("phase")),
                    $"{events[i - 1]} puis {events[i]}");
        }
    }

    [Fact]
    public void Zone_touche_les_ennemis_vivants_par_identifiant()
    {
        CombatantSetup[] combatants = { Character(), Dummy("cible_b"), Dummy("cible_a"), Character("allie") };
        var targets = Events(Run(Setup(new[] { Cmd(0, Onde, target: null) }, combatants)).Log).OfKind("damage").Select(e => e.Str("target"));
        Assert.Equal(new[] { "cible_a", "cible_b" }, targets);
    }

    [Fact]
    public void Resolution_apres_toutes_les_commandes_du_tick()
    {
        // Deux acteurs, même tick : les deux lancements (phase commandes) précèdent les deux impacts (phase résolution),
        // chacun suivi de la Résonance gagnée par son auteur.
        CombatantSetup[] combatants = { Character("alpha"), Character("beta"), Dummy() };
        var kinds = Events(Run(Setup(new[] { Cmd(0, Frappe, "alpha"), Cmd(0, Frappe, "beta") }, combatants)).Log)
            .Where(e => e.Tick() == 0 && e.Str("phase") != "setup").Select(e => e.Str("event"));
        Assert.Equal(new[] { "skillCast", "skillCast", "damage", "resourceGained", "damage", "resourceGained" }, kinds);
    }
}
