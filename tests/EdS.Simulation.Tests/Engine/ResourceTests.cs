using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using FsCheck.Xunit;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Engine;

/// <summary>Charges de rempart (A4) et Résonance (A5) : bornes, sources, consommateurs, zone, ultime, remise à zéro.</summary>
[Trait("Category", "Engine")]
public class ResourceTests
{
    private static readonly CombatantSetup[] TankVsDummies = { Tank(actives: new[] { Frappe, Onde, Rempart, Recharge }), Dummy("cible_a"), Dummy("cible_b") };

    [Fact]
    public void Attaque_directe_reussie_rapporte_3_une_seule_fois_meme_en_zone()
    {
        var (result, log) = Run(Setup(new[] { Cmd(0, Onde, target: null) }, TankVsDummies, durationMs: 1000));
        var events = Events(log);
        Assert.Equal(2, events.OfKind("damage").Count());
        var gain = Assert.Single(events.OfKind("resourceGained"));
        Assert.Equal(("resonance", "directHit", 3L), (gain.Str("resource")!, gain.Str("source")!, gain.Int("amount")));
        Assert.Equal(3, result["bastion"].Resonance);
    }

    [Fact]
    public void Charges_plafonnees_a_5_excedent_perdu_et_journalise()
    {
        // Sept parades réussies (une par impact, toutes les 2 s) : 5 Charges, deux gains perdus.
        var commands = Enumerable.Range(0, 7).Select(i => Parry(1000 + i * 2000L));
        var fighters = new CombatantSetup[] { Tank(), Assailant() };
        var (result, log) = Run(Setup(commands, fighters, durationMs: 15_000, rules: NoUpkeep));
        var charges = Events(log).OfKind("resourceGained").Where(e => e.Str("resource") == "rampartCharges").ToList();
        Assert.Equal(new long[] { 1, 2, 3, 4, 5, 5, 5 }, charges.Select(e => e.Int("value")));
        Assert.Equal(new long[] { 0, 0, 0, 0, 0, 1, 1 }, charges.Select(e => e.Int("wasted")));
        Assert.Equal(5, result["bastion"].RampartCharges);
        Assert.Equal(56, result["bastion"].Resonance);
    }

    [Fact]
    public void Resonance_plafonnee_a_100()
    {
        var rules = new CombatRules { ResonancePerDirectHit = 60, GlobalCooldownMs = 0 };
        var (result, log) = Run(Setup(new[] { Cmd(0, Gratuite), Cmd(50, Gratuite) }, durationMs: 1000, rules: rules));
        var gains = Events(log).OfKind("resourceGained").ToList();
        Assert.Equal(new long[] { 60, 100 }, gains.Select(e => e.Int("value")));
        Assert.Equal(20, gains[1].Int("wasted"));
        Assert.Equal(100, result["bastion"].Resonance);
    }

    [Fact]
    public void Ultime_coute_100_de_resonance_sans_generation_recursive()
    {
        // 50 par attaque : deux Gratuites (100), puis l'ultime dépense 100 et ne rapporte rien.
        var rules = new CombatRules { ResonancePerDirectHit = 50, GlobalCooldownMs = 0 };
        var commands = new[] { Cmd(0, Ultime), Cmd(50, Gratuite), Cmd(100, Gratuite), Cmd(150, Ultime) };
        var (result, log) = Run(Setup(commands, durationMs: 1000, rules: rules));
        var events = Events(log);
        var rejected = Assert.Single(events.OfKind("commandRejected"));
        Assert.Equal(("insufficientResonance", 0L), (rejected.Str("reason")!, rejected.Tick()));
        var cast = events.OfKind("skillCast").Single(e => e.Str("skill") == Ultime);
        Assert.Equal((100L, 0L), (cast.Int("resonanceCost"), cast.Int("resonance")));
        Assert.DoesNotContain(events.OfKind("resourceGained"), e => e.Tick() == 3);
        Assert.Equal(0, result["bastion"].Resonance);
    }

    [Fact]
    public void Sources_et_consommateurs_de_charges_configurables()
    {
        // Rempart exige 2 Charges : refusé à vide ; Recharge en produit 3 (plafonnées à 5) ; Rempart passe ensuite.
        var rules = new CombatRules { GlobalCooldownMs = 0 };
        var commands = new[] { Cmd(0, Rempart, target: "cible_a"), Cmd(50, Recharge, target: "cible_a"), Cmd(100, Recharge, target: "cible_a"), Cmd(150, Rempart, target: "cible_a") };
        var (result, log) = Run(Setup(commands, TankVsDummies, durationMs: 1000, rules: rules));
        var events = Events(log);
        Assert.Equal("insufficientRampartCharges", Assert.Single(events.OfKind("commandRejected")).Str("reason"));
        var charges = events.OfKind("resourceGained").Where(e => e.Str("resource") == "rampartCharges").ToList();
        Assert.Equal(new[] { (3L, 0L, "skill"), (2L, 1L, "skill") }, charges.Select(e => (e.Int("amount"), e.Int("wasted"), e.Str("source")!)));
        var rempart = events.OfKind("skillCast").Single(e => e.Str("skill") == Rempart);
        Assert.Equal((2L, 3L), (rempart.Int("rampartChargeCost"), rempart.Int("rampartCharges")));
        Assert.Equal(3, result["bastion"].RampartCharges);
    }

    [Fact]
    public void Chaque_combat_repart_de_zero()
    {
        // Même configuration jouée deux fois : les ressources ne survivent pas au combat (A4 : remise à zéro).
        var commands = new[] { Parry(1000), Parry(3000) };
        CombatSetup setup = Setup(commands, new CombatantSetup[] { Tank(), Assailant() }, durationMs: 4000, rules: NoUpkeep);
        var first = Run(setup);
        var second = Run(setup);
        Assert.Equal(first.Log.ToString(), second.Log.ToString());
        var start = Events(second.Log).OfKind("combatant").First();
        Assert.Equal((0L, 0L), (start.Int("resonance"), start.Int("rampartCharges")));
        Assert.Equal(2, second.Result["bastion"].RampartCharges);
    }

    /// <summary>Scénario aléatoire : parades, gardes, compétences et attaques mêlées sur 20 s.</summary>
    private static CombatSetup RandomTraining(int seed, int[] plan)
    {
        var commands = new List<CombatCommand>();
        for (int i = 0; i < plan.Length; i++)
        {
            long value = Math.Abs((long)plan[i]);
            long at = value % 400 * 50L;
            commands.Add((value / 400 % 5) switch
            {
                0 => Parry(at),
                1 => GuardStart(at),
                2 => GuardStop(at),
                3 => Cmd(at, Onde, target: null),
                _ => Cmd(at, Recharge, target: "assaillant"),
            });
        }
        var fighters = new CombatantSetup[]
        {
            Tank(actives: new[] { Frappe, Onde, Rempart, Recharge }),
            Assailant(intervalMs: 300, attacks: new[] { Blow(20), Blow(20, unblockable: true, id: "perce"), Blow(20, fragility: true, id: "fele") }),
            Assailant(firstAttackMs: 150, intervalMs: 450, id: "second", target: null, attacks: Blow(20, targeting: SkillTargeting.AllEnemies, id: "onde")),
        };
        return Setup(commands, fighters, seed: seed, durationMs: 20_000);
    }

    [Property(MaxTest = 200)]
    public bool Ressources_toujours_dans_leurs_bornes(int seed, int[] plan)
    {
        var (result, log) = Run(RandomTraining(Math.Abs(seed % 1000), plan ?? Array.Empty<int>()));
        foreach (JsonElement e in Events(log))
        {
            string kind = e.Str("event")!;
            if (kind == "resourceGained")
            {
                long value = e.Int("value");
                long max = e.Str("resource") == "resonance" ? 100 : 5;
                if (value < 0 || value > max || e.Int("amount") < 0 || e.Int("wasted") < 0) return false;
            }
            if (kind == "skillCast" && (e.Int("resonance") < 0 || e.Int("rampartCharges") < 0 || e.Int("endurance") < 0)) return false;
            if (e.TryGetProperty("endurance", out JsonElement endurance) && endurance.ValueKind == JsonValueKind.Number && endurance.GetInt64() < 0) return false;
        }
        CombatantResult bastion = result["bastion"];
        return bastion.Resonance is >= 0 and <= 100 && bastion.RampartCharges is >= 0 and <= 5 && bastion.Endurance >= 0;
    }

    [Property(MaxTest = 100)]
    public bool Entrainement_aleatoire_reproductible(int seed, int[] plan)
    {
        CombatSetup setup = RandomTraining(Math.Abs(seed % 1000), plan ?? Array.Empty<int>());
        return Run(setup).Log.ToString() == Run(setup).Log.ToString();
    }
}
