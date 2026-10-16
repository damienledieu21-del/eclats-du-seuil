using System.Text.Json;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Simulation.Engine;
using FsCheck.Xunit;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Triggers;

/// <summary>
/// Lot 7.3.7 : propriété (§10.2). Des combats générés au hasard, porteurs de déclencheurs mutuels (personnages et assaillants, tous
/// types d'abonnement et d'effets), terminent toujours ; aucune ligne de jeu ne dépasse la profondeur 4 ; aucune réaction ne suit un
/// diagnostic (graphe causal reconstruit et vérifié à chaque exécution, voir <see cref="EdS.TestSupport.CausalGraph"/>) ; deux
/// exécutions sont identiques octet par octet ; en Debug, chaque réservation est exacte. Le rejeu des journaux à déclencheurs est
/// vérifié côté données (<c>Lot73ReplayPropertyTests</c>).
/// </summary>
[Trait("Category", "Trigger")]
public class Lot73PropertyTests
{
    private static readonly TriggerEvent[] Kinds = Enum.GetValues<TriggerEvent>();
    private static readonly string[] Statuses = { "fragility", "bleed", "burn", "stun", "freeze" };

    private static T Pick<T>(Random rng, IReadOnlyList<T> items) => items[rng.Next(items.Count)];

    private static TriggerEffect Effect(Random rng) => rng.Next(3) switch
    {
        0 => TriggerEffect.Strike(Pick(rng, new[] { TriggerTarget.EventSource, TriggerTarget.EventTarget, TriggerTarget.AllEnemies }), Fixed.Parse("0.05")),
        1 => Pick(rng, Statuses) is var status
            ? TriggerEffect.ApplyStatus(Pick(rng, new[] { TriggerTarget.EventSource, TriggerTarget.EventTarget, TriggerTarget.Bearer, TriggerTarget.AllEnemies }),
                status, status is "bleed" or "burn" ? Fixed.Parse("0.1") : null)
            : null!,
        _ => TriggerEffect.GainResource(TriggerResource.Resonance, Fixed.One),
    };

    private static TriggerDefinition Trigger(Random rng)
    {
        TriggerEvent on = Pick(rng, Kinds);
        IReadOnlyList<TriggerRole> roles = TriggerDefinition.AllowedRoles(on);
        TriggerRole? role = roles.Count > 1 ? Pick(rng, roles) : null;
        var effects = Enumerable.Range(0, 1 + rng.Next(2)).Select(_ => Effect(rng)).ToArray();
        return new TriggerDefinition(on, effects, role, cooldownMs: rng.Next(2) * 500);
    }

    private static TriggerDefinition[] Triggers(Random rng) => Enumerable.Range(0, 1 + rng.Next(3)).Select(_ => Trigger(rng)).ToArray();

    /// <summary>Combat généré : 1 ou 2 personnages rouges, 1 personnage bleu et peut-être un assaillant bleu, tous porteurs.</summary>
    internal static CombatSetup Generate(int seed)
    {
        var rng = new Random(seed);
        string[] red = Enumerable.Range(0, 1 + rng.Next(2)).Select(i => $"r{i}").ToArray();
        var fighters = new List<CombatantSetup>();
        foreach (string id in red.Append("b0"))
            fighters.Add(Character(id, id == "b0" ? "bleu" : "rouge", rng.Next(3), Attrs(vitalite: rng.Next(2) == 0 ? 20 : 200),
                new[] { Frappe, Gratuite, Onde, Brasier }, Class(passiveTriggers: Triggers(rng)), passives: new[] { Passif }));
        if (rng.Next(2) == 0)
            fighters.Add(new AssailantSetup("a0", "bleu", Fixed.Zero, Fixed.Zero, Pick(rng, red), 50 * rng.Next(20), 50 * (5 + rng.Next(30)),
                new[] { rng.Next(2) == 0 ? Blow(1) : Strike("entaille", 1, applies: Apply("bleed", "0.2")) }, triggers: Triggers(rng)));
        var commands = new List<CombatCommand>();
        foreach (string actor in red.Append("b0"))
            for (int k = 0, n = rng.Next(4); k < n; k++)
            {
                string skill = Pick(rng, new[] { Frappe, Gratuite, Onde, Brasier });
                string target = actor == "b0" ? Pick(rng, red) : "b0";
                commands.Add(Cmd(50L * rng.Next(60), skill, actor, skill == Onde ? null : target));
            }
        return Setup(commands.OrderBy(c => c.AtMs), fighters, seed: seed, durationMs: 4000, rules: NoUpkeep);
    }

    [Fact]
    public void Generateur_produit_surtout_des_combats_valides_et_des_reactions()
    {
        int valid = 0, withReactions = 0;
        for (int seed = 0; seed < 100; seed++)
        {
            CombatSetup setup = Generate(seed);
            if (setup.Validate().Count > 0) continue;
            valid++;
            if (Run(setup).Log.Lines.Any(l => l.Contains("\"depth\":"))) withReactions++;
        }
        Assert.InRange(valid, 80, 100);     // 90 au gel du test
        Assert.InRange(withReactions, 60, 100);   // 73 au gel du test
    }

    [Property(MaxTest = 300)]
    public bool Combat_a_declencheurs_mutuels_termine_borne_et_reproductible(int seed)
    {
        CombatSetup setup = Generate(seed);
        if (setup.Validate().Count > 0) return true;   // combinaison refusée au chargement : hors propriété
        var (result, log) = Run(setup);                // reconstruit et vérifie le graphe causal
        Assert.True(result.EndTick <= setup.DurationMs / 50);
        foreach (JsonElement e in Events(log))
        {
            if (!e.TryGetProperty("depth", out JsonElement depth)) continue;
            Assert.InRange(depth.GetInt32(), 1, e.Str("event") == "triggerSuppressed" ? 5 : 4);
        }
        return log.Lines.SequenceEqual(Run(setup).Log.Lines);
    }
}
