using System.Text.Json;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Engine;

/// <summary>Refus atomique : une commande refusée ne change rien (endurance, recharges, aléa) et laisse une trace.</summary>
[Trait("Category", "Engine")]
public class CommandRejectionTests
{
    private static string Reason(CombatSetup setup) =>
        Assert.Single(Events(Run(setup).Log).OfKind("commandRejected")).Str("reason")!;

    [Theory]
    [InlineData(NonEquipee, "mannequin", "skillNotEquipped")]
    [InlineData("skill.autre.inconnue", "mannequin", "skillNotEquipped")]
    [InlineData(Inerte, "mannequin", "skillNotImplemented")]
    [InlineData(Ruineuse, "mannequin", "insufficientEndurance")]
    [InlineData(Frappe, null, "targetRequired")]
    [InlineData(Frappe, "bastion", "targetNotEnemy")]
    [InlineData(Onde, "mannequin", "targetNotAllowed")]
    public void Motifs_de_refus(string skill, string? target, string reason)
    {
        var actives = new[] { Frappe, Onde, Ruineuse, Inerte };
        var setup = Setup(new[] { Cmd(0, skill, target: target) }, new CombatantSetup[] { Character(actives: actives), Dummy() });
        Assert.Equal(reason, Reason(setup));
    }

    [Fact]
    public void Un_mannequin_ne_peut_pas_agir() =>
        Assert.Equal("actorCannotAct", Reason(Setup(new[] { Cmd(0, Frappe, actor: "mannequin", target: "bastion") })));

    [Fact]
    public void Cible_alliee_refusee()
    {
        var setup = Setup(new[] { Cmd(0, Frappe, target: "allie") }, new CombatantSetup[] { Character(), Character("allie"), Dummy() });
        Assert.Equal("targetNotEnemy", Reason(setup));
    }

    [Fact]
    public void Refus_sans_effet_sur_l_endurance_ni_les_recharges()
    {
        // Sans régénération, l'endurance (100) permet 6 Heurts (90) ; le septième est refusé, puis Gratuite passe au même tick :
        // le refus n'a donc lancé ni le temps global de recharge ni la recharge de Heurt.
        var rules = new CombatRules { EnduranceRegenPerSecond = 0, GlobalCooldownMs = 0 };
        var commands = Enumerable.Range(0, 7).Select(i => Cmd(i * 10_000L, Heurt)).Append(Cmd(60_000, Gratuite)).ToArray();
        var (result, log) = Run(Setup(commands, durationMs: 70_000, rules: rules));
        var events = Events(log);
        var rejected = Assert.Single(events.OfKind("commandRejected"));
        Assert.Equal("insufficientEndurance", rejected.Str("reason"));
        Assert.Equal(1200, rejected.Tick());
        Assert.Equal(10, result["bastion"].Endurance);
        Assert.Contains(events.OfKind("skillCast"), e => e.Tick() == 1200 && e.Str("skill") == Gratuite);
        Assert.Equal(1, result["bastion"].CommandsRejected);
    }

    [Fact]
    public void Refus_ne_consomme_aucun_tirage_aleatoire()
    {
        // Mêmes commandes valides, avec et sans commandes refusées intercalées : mêmes dégâts, mêmes critiques.
        var valid = Enumerable.Range(0, 30).Select(i => Cmd(i * 1000L, Gratuite)).ToList();
        var withRejected = valid.SelectMany(c => new[] { c, Cmd(c.AtMs, Inerte, target: "mannequin"), Cmd(c.AtMs, Frappe, target: null) }).ToList();
        var actives = new[] { Frappe, Gratuite, Inerte };
        CombatantSetup[] combatants = { Character(actives: actives), Dummy() };

        static IEnumerable<string> Damage(MemoryCombatLog log) => Events(log).OfKind("damage")
            .Select(e => $"{e.Tick()}:{e.Int("amount")}:{e.GetProperty("critical").GetBoolean()}");
        var reference = Run(Setup(valid, combatants, durationMs: 30_000)).Log;
        var mixed = Run(Setup(withRejected, combatants, durationMs: 30_000)).Log;
        Assert.Equal(60, Events(mixed).OfKind("commandRejected").Count());
        Assert.Equal(Damage(reference), Damage(mixed));
    }

    [Fact]
    public void Cible_morte_et_acteur_mort_refuses()
    {
        // Trois équipes : « tueur » abat « victime » au tick 0 ; ensuite « tiers » vise la victime, et la victime tente d'agir.
        var strong = Attrs(force: 500);
        var weak = Attrs(vitalite: 0);
        CombatantSetup[] combatants =
        {
            Character("tueur", "rouge", attributes: strong), Character("victime", "bleu", attributes: weak), Character("tiers", "vert"),
        };
        var commands = new[]
        {
            Cmd(0, Frappe, "tueur", "victime"),
            Cmd(1000, Frappe, "tiers", "victime"),
            Cmd(1000, Frappe, "victime", "tiers"),
        };
        var events = Events(Run(Setup(commands, combatants)).Log);
        var reasons = events.OfKind("commandRejected").ToDictionary(e => e.Str("actor")!, e => e.Str("reason"));
        Assert.Equal("targetDead", reasons["tiers"]);
        Assert.Equal("actorDead", reasons["victime"]);
    }
}
