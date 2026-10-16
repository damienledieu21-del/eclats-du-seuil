using System.Text.Json;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using EdS.Simulation.Tests.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Regression;

/// <summary>
/// Correctif 6.1, M01 : l'entretien de la garde (2/s, soit 0,1 par tick de 50 ms) est cumulé sur tout le combat.
/// Avant, le reliquat fractionnaire était perdu à chaque reprise de garde : neuf ticks gardés, une pause, et ainsi de
/// suite, ne coûtaient jamais rien. Désormais le coût ne dépend que du nombre de ticks réellement gardés.
/// </summary>
[Trait("Category", "Engine")]
[Trait("Category", "Resources")]
[Trait("Category", "Regression")]
public class M01GuardUpkeepTests
{
    private static List<JsonElement> Fight(IEnumerable<CombatCommand> commands, long durationMs = 10_000, CombatRules? rules = null, params CombatantSetup[] opponents) =>
        Events(Run(Setup(commands, new CombatantSetup[] { Tank() }.Concat(opponents.Length == 0 ? new CombatantSetup[] { Dummy() } : opponents),
            durationMs: durationMs, rules: rules)).Log);

    private static long Upkeep(List<JsonElement> events) => events.OfKind("guardUpkeep").Sum(e => e.Int("amount"));

    /// <summary>Ticks gardés selon la règle documentée : garde active après la phase des commandes du tick.</summary>
    private static long GuardedTicks(IReadOnlyList<CombatCommand> commands, long durationTicks)
    {
        bool guarding = false;
        long count = 0;
        var byTick = commands.Select((c, i) => (c, i)).GroupBy(x => x.c.AtMs / 50).ToDictionary(g => g.Key, g => g.OrderBy(x => x.i).Select(x => x.c).ToList());
        for (long tick = 0; tick < durationTicks; tick++)
        {
            if (byTick.TryGetValue(tick, out var list))
                foreach (CombatCommand c in list)
                    guarding = c.Action == CombatAction.GuardStart || (c.Action != CombatAction.GuardStop && guarding);
            if (guarding) count++;
        }
        return count;
    }

    [Fact]
    public void Gardes_courtes_repetees_ne_sont_plus_gratuites()
    {
        // Dix gardes de 9 ticks (450 ms), séparées de 50 ms : 90 ticks gardés = 9 points. Avant : 0.
        var commands = Enumerable.Range(0, 10).SelectMany(i => new[] { GuardStart(i * 500), GuardStop(i * 500 + 450) }).ToArray();
        var events = Fight(commands);
        Assert.Equal(90, GuardedTicks(commands, 200));
        Assert.Equal(9, Upkeep(events));
        Assert.Equal(new long[] { 10, 21, 32, 43, 54, 65, 76, 87, 98 }, events.OfKind("guardUpkeep").Select(e => e.Tick()));
    }

    [Fact]
    public void Meme_nombre_de_ticks_gardes_meme_cout_quelle_que_soit_la_repartition()
    {
        var continuous = new[] { GuardStart(0), GuardStop(1000) };                                            // ticks 0 à 19
        var split = new[] { GuardStart(0), GuardStop(250), GuardStart(500), GuardStop(750),
                            GuardStart(1000), GuardStop(1250), GuardStart(1500), GuardStop(1750) };          // 4 × 5 ticks
        var sameTick = new[] { GuardStart(0), GuardStop(500), GuardStart(500), GuardStop(1000) };            // arrêt et reprise au tick 10
        foreach (var commands in new[] { continuous, split, sameTick })
        {
            Assert.Equal(20, GuardedTicks(commands, 200));
            Assert.Equal(2, Upkeep(Fight(commands)));
        }
    }

    [Fact]
    public void Arrets_et_reprises_multiples_dans_le_meme_tick()
    {
        // Prise puis arrêt au même tick : rien n'est compté. Prise, arrêt, reprise : le tick compte une seule fois.
        var none = Fight(new[] { GuardStart(0), GuardStop(0), GuardStart(500), GuardStop(500) });
        Assert.Equal(0, Upkeep(none));
        Assert.Equal(2, none.OfKind("guardStarted").Count());

        var flicker = new List<CombatCommand> { GuardStart(0) };
        for (int t = 1; t < 20; t++) flicker.AddRange(new[] { GuardStop(t * 50), GuardStart(t * 50), GuardStop(t * 50), GuardStart(t * 50) });
        flicker.Add(GuardStop(1000));
        Assert.Equal(20, GuardedTicks(flicker, 200));
        var events = Fight(flicker);
        Assert.Equal(2, Upkeep(events));
        Assert.Equal(new long[] { 9, 19 }, events.OfKind("guardUpkeep").Select(e => e.Tick()));
    }

    [Fact]
    public void Propriete_cout_egal_au_plancher_des_ticks_gardes()
    {
        var random = new Random(601);
        for (int run = 0; run < 150; run++)
        {
            var commands = new List<CombatCommand>();
            int count = random.Next(1, 40);
            for (int i = 0; i < count; i++)
            {
                long at = random.Next(0, 200) * 50L;
                commands.Add(random.Next(2) == 0 ? GuardStart(at) : GuardStop(at));
            }
            long guarded = GuardedTicks(commands, 200);
            Assert.Equal(guarded * 2 * 50 / 1000, Upkeep(Fight(commands)));   // ⌊ 2/s × ticks × 50 ms / 1000 ⌋
        }
    }

    [Fact]
    public void Parade_pendant_la_garde_ne_change_pas_l_entretien()
    {
        var plain = Fight(new[] { GuardStart(0) });
        var withParries = Fight(new[] { GuardStart(0), Parry(1000), Parry(3000), Parry(6000) });
        Assert.Equal(Upkeep(plain), Upkeep(withParries));
        Assert.Equal(20, Upkeep(plain));
    }

    [Fact]
    public void Rupture_par_l_entretien_le_reliquat_reste_du_apres_la_rupture()
    {
        // Endurance 100 ; entretien aux ticks 9 et 19 (98), blocage au tick 20 (coût 95 → 3), entretien aux ticks 29, 39, 49 (0).
        // Tick 59 : 1 dû, 0 disponible → rupture « upkeep » ; ce tick n'est pas compté (59 ticks gardés, 0,9 de reliquat).
        // Rupture jusqu'au tick 119, régénération ensuite (10 points au tick 140). Reprise au tick 140 : le 60e tick gardé
        // rend dû le point en suspens, payé immédiatement (avant le correctif : seulement au tick 149).
        var rules = new CombatRules { GuardCostPerBlock = 95 };
        var events = Fight(new[] { GuardStart(0), GuardStart(7000) }, durationMs: 9000, rules: rules,
            Assailant(intervalMs: 9000));
        var broken = Assert.Single(events.OfKind("guardBroken"));
        Assert.Equal((59L, "upkeep", 0L), (broken.Tick(), broken.Str("reason")!, broken.Int("endurance")));
        var upkeep = events.OfKind("guardUpkeep").Select(e => (e.Tick(), e.Int("amount"), e.Int("endurance"))).ToList();
        Assert.Equal(new[] { (9L, 1L, 99L), (19L, 1L, 98L), (29L, 1L, 2L), (39L, 1L, 1L), (49L, 1L, 0L), (140L, 1L, 9L), (150L, 1L, 8L), (160L, 1L, 7L), (170L, 1L, 6L) }, upkeep);
    }

    [Fact]
    public void Rupture_par_un_blocage_le_tick_de_rupture_n_est_pas_garde()
    {
        // Blocage impayable au tick 20 : la garde tombe en phase 3, le tick 20 n'est pas gardé (ticks 0 à 19 : 2 points).
        var rules = new CombatRules { GuardCostPerBlock = 101 };
        var events = Fight(new[] { GuardStart(0) }, durationMs: 3000, rules: rules, Assailant(intervalMs: 9000));
        Assert.Equal(20L, Assert.Single(events.OfKind("guardBroken")).Tick());
        Assert.Equal(new long[] { 9, 19 }, events.OfKind("guardUpkeep").Select(e => e.Tick()));
    }
}
