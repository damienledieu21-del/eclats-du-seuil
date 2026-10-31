using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using EdS.Simulation.Pilot;
using EdS.World;
using Xunit.Abstractions;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.8b, difficulté normale (« on meurt parfois si on joue mal ; le Gardien demande garde et parade ») : combats simulés par le
/// moteur, un par chapitre de la quête principale, avec un Bastion typique du dernier niveau du chapitre (compétences gagnées en montant,
/// meilleur équipement ordinaire de son niveau, sans talent ni objet nommé) contre des créatures des Vallons montées à son niveau (zones en
/// mélange). Le joueur est le pilote de mesure du moteur (<see cref="CombatPilot"/> : garde tenue, parades selon le profil, compétences par
/// priorité), qui marche vers sa cible. Ces tests échouent si un combat devient trop facile ou impossible.
/// </summary>
[Trait("Category", "World")]
public class V18EquilibreTests
{
    private readonly ITestOutputHelper _out;

    public V18EquilibreTests(ITestOutputHelper output) => _out = output;

    private static ItemCatalog Items => V11TestData.ItemsCatalog;
    private static CharacterProgression Progression => V11TestData.Progression;

    /// <summary>Créature typique de chaque chapitre (son lieu dans les Vallons).</summary>
    public static readonly string[] Creatures = { "loup1", "archer", "loup7", "bandit_epee1", "rodeur" };

    /// <summary>
    /// Équipement typique du niveau <paramref name="level"/> : pour chaque place du combat, l'objet ordinaire (ni nommé, ni à effet, au plus
    /// très rare) du plus haut niveau requis qui ne le dépasse pas (le premier du catalogue à niveau égal).
    /// </summary>
    public static Dictionary<string, string> TypicalGear(int level)
    {
        var worn = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (EquipmentPlace place in Items.Places.Where(p => p.Group == PlaceGroup.Combat))
        {
            ItemDefinition? best = Items.Items.Where(i => i.Type == ItemType.Equipment && i.Slot == place.Accepts && i.RequiredLevel <= level && !i.Named
                    && i.Effect is null && i.Set is null && i.Rarity <= ItemRarity.VeryRare && i.Id != "item.epee_du_novice" && i.Id != "item.ecu_du_novice")
                .OrderByDescending(i => i.RequiredLevel).FirstOrDefault();
            if (best != null) worn[place.Id] = best.Id;
        }
        return worn;
    }

    /// <summary>Bastion typique du niveau : compétences gagnées (la 6e place reçoit Contrecoup dès le niveau 20), équipement typique, potions.</summary>
    public static SavedCharacter Typical(int level, int potions)
    {
        SavedCharacter hero = PlayerCharacters.Learn(PlayerCharacters.Create("bastion", "class.bastion", Progression).WithXp(Progression.XpToLevel[level - 1]),
            Progression, ShippedData.Rules.Progression, out _);
        if (level >= 20) hero = hero.WithSkills(hero.ActiveSkills.Append("skill.bastion.contrecoup").ToArray(), hero.Ultimate, hero.PassiveSkills);
        Dictionary<string, string> worn = TypicalGear(level);
        var stacks = worn.Values.Select(i => new ItemStack(i, 1)).ToList();
        if (potions > 0) stacks.Add(new ItemStack("item.potion_de_soin", potions));
        return hero.WithItems(stacks, worn);
    }

    /// <summary>Vallons livrés ; les créatures données sont amenées près de l'entrée (au village : leur niveau est celui du joueur moins 2).</summary>
    public static WorldSession Arena(SavedCharacter hero, string[] creatures, bool boss = false)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        string directory = Path.Combine(Path.GetTempPath(), "eds_v18_equilibre_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "zones"));
            JsonObject zone = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
            if (boss) root["data"]!["safePoints"]![0]!["position"] = new JsonArray(51.5, 36);
            int k = 0;
            foreach (JsonNode? c in zone["data"]!["combatants"]!.AsArray())
            {
                string id = (string)c!["id"]!;
                if (boss) foreach (JsonNode? a in c["attacks"]!.AsArray()) a!["rawDamage"] = 1;   // le reste de la zone calmé
                else if (creatures.Contains(id)) { c["position"] = new JsonArray(-14 + 2 * k, -12 + (k % 2) * 2); k++; }
            }
            File.WriteAllText(Path.Combine(directory, "zones", "vallons.json"), zone.ToJsonString());
            File.Copy(ScenarioPath("zones/vallons_chef"), Path.Combine(directory, "zones", "vallons_chef.json"));
            File.Copy(ScenarioPath("zones/vallons_bandits"), Path.Combine(directory, "zones", "vallons_bandits.json"), true);
            CopyMissingZoneScenarios(directory);
            ZoneDefinition z = ZoneJsonReader.Read(root.ToJsonString(), directory, ShippedData, Items).Zone!;
            return new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero), z, ShippedData, Progression, Items);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Combat simulé : le pilote du moteur décide (garde, parades selon <paramref name="profile"/>, compétences), le Bastion marche vers son
    /// ennemi le plus proche encore debout et boit une potion sous 40 % de ses PV. Rend les PV restants (0 s'il tombe) et si tous les
    /// ennemis donnés sont vaincus.
    /// </summary>
    public static (long Left, bool Won) Fight(WorldSession session, string[] enemies, PilotProfile? profile, int seconds = 300)
    {
        CombatEngine e = session.Zone.Engine;
        // La défense vient du pilote de mesure du moteur (garde tenue, parades parfaites selon le profil) ; null : ni garde ni parade.
        CombatPilot? pilot = profile is PilotProfile p0 ? new CombatPilot(e, "bastion", p0) : null;
        bool boss = enemies.Length == 1 && enemies[0] == "gardien_du_seuil";
        var defeated = new HashSet<string>(StringComparer.Ordinal);
        int perTick = ShippedData.Rules.Combat.MaxCommandsPerActorPerTick;
        for (int i = 0; i < seconds * 20; i++)
        {
            // Une créature partie au bout de sa laisse revient sous un autre nom : on suit la vie en cours de chaque population.
            string[] targets = boss ? (session.BossForm is string form ? new[] { form } : Array.Empty<string>())
                : enemies.Where(p => !defeated.Contains(p)).Select(session.CurrentCreature).ToArray();
            CombatantView me = e.View("bastion");
            if (me.IsDead || (boss ? session.BossDefeated : targets.Length == 0)) break;
            long now = e.CurrentTick * 50;
            var commands = (pilot?.Decide() ?? Array.Empty<CombatCommand>())
                .Where(c => c.Action == CombatAction.Parry || c.Action == CombatAction.GuardStart || c.Action == CombatAction.GuardStop).ToList();
            CombatantView? it = targets.Select(e.View).Where(v => !v.IsDead && v.IsPresent && v.Position != null)
                .OrderBy(v => Distance(me, v)).FirstOrDefault();
            if (it != null && me.Position is GroundVector a && it.Position is GroundVector b)
            {
                // Compétences à tour de rôle sur l'ennemi le plus proche (la Barrière d'acier sur soi), sauf au tick d'une parade.
                string[] skills = session.Character.ActiveSkills.Append(session.Character.Ultimate).OfType<string>().ToArray();
                if (commands.All(c => c.Action != CombatAction.Parry) && commands.Count < perTick)
                {
                    string skill = skills[i % skills.Length];
                    commands.Add(new CombatCommand(now, "bastion", skill, skill == "skill.bastion.barriere_d_acier" ? "bastion" : it.Id));
                }
                double dx = (b.X.Raw - a.X.Raw) / 1e4, dz = (b.Z.Raw - a.Z.Raw) / 1e4, d = Math.Sqrt(dx * dx + dz * dz);
                if (commands.Count < perTick)
                    commands.Add(d > 2.2
                        ? new CombatCommand(now, "bastion", new GroundVector(Fixed.FromRaw((long)(dx / d * 1e4)), Fixed.FromRaw((long)(dz / d * 1e4))))
                        : new CombatCommand(now, "bastion", CombatAction.Stop));
            }
            int sent = 0;
            foreach (CombatCommand c in commands)
                try { e.Submit(c); sent++; } catch (ArgumentException) { } catch (InvalidOperationException) { }
            if (sent < perTick && me.Health < me.MaxHealth * 4 / 10 && me.ConsumableCharges.TryGetValue("item.potion_de_soin", out int left) && left > 0)
                try { session.Drink("item.potion_de_soin"); } catch (ArgumentException) { } catch (InvalidOperationException) { }
            session.Step();
            if (!boss)
                foreach (string p in enemies)
                    if (!defeated.Contains(p) && e.View(session.CurrentCreature(p)).IsDead) defeated.Add(p);
        }
        bool won = boss ? session.BossDefeated : defeated.Count == enemies.Length;
        return (e.View("bastion").IsDead ? 0 : e.View("bastion").Health ?? 0, won);
    }

    private static double Distance(CombatantView a, CombatantView b) =>
        a.Position is GroundVector p && b.Position is GroundVector q
            ? Math.Sqrt(Math.Pow((p.X.Raw - q.X.Raw) / 1e4, 2) + Math.Pow((p.Z.Raw - q.Z.Raw) / 1e4, 2)) : double.MaxValue;

    /// <summary>Groupe typique de chaque chapitre (trois créatures ; deux rôdeurs au chapitre 5).</summary>
    public static string[] Pack(int chapter) => chapter switch
    {
        1 => new[] { "loup1", "loup2", "loup3" },
        2 => new[] { "archer", "sanglier6", "sanglier7" },
        3 => new[] { "loup7", "loup8", "loup9" },
        4 => new[] { "bandit_epee1", "bandit_epee2", "bandit_arc1" },
        _ => new[] { "rodeur", "rodeur2" },
    };

    private void Say(string what, int level, (long Left, bool Won) r, WorldSession w) =>
        _out.WriteLine($"{what} niveau {level} : {(r.Won ? "gagné" : "perdu")}, {r.Left}/{w.Zone.Engine.View("bastion").MaxHealth} PV");

    /// <summary>Jamais impossible : une créature typique du chapitre se bat seule, sans garde, sans parade ni potion, avec plus de la moitié des PV à la fin.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Une_creature_seule_se_bat_sans_defense(int chapter)
    {
        int level = chapter * 5;
        string c = Creatures[chapter - 1];
        WorldSession w = Arena(Typical(level, 0), new[] { c });
        (long left, bool won) r = Fight(w, new[] { c }, null);
        Say(c, level, r, w);
        Assert.True(r.won, $"chapitre {chapter} : {c} encore debout");
        Assert.True(r.left * 2 >= w.Zone.Engine.View("bastion").MaxHealth!.Value, $"chapitre {chapter} : il ne reste que {r.left} PV");
    }

    /// <summary>
    /// Ni trop facile ni impossible : un groupe du chapitre (trois potions) entame vraiment le Bastion qui ne se défend pas (au plus 85 % de
    /// ses PV à la fin ; au chapitre 5, il tombe) ; avec la garde et les parades (pilote expert), il gagne.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Un_groupe_demande_de_se_defendre(int chapter)
    {
        int level = chapter * 5;
        string[] pack = Pack(chapter);
        WorldSession bare = Arena(Typical(level, 3), pack);
        (long Left, bool Won) loose = Fight(bare, pack, null);
        Say("groupe sans défense", level, loose, bare);
        long max = bare.Zone.Engine.View("bastion").MaxHealth!.Value;
        Assert.True(loose.Left * 100 <= max * 85, $"chapitre {chapter} : trop facile ({loose.Left}/{max} PV sans se défendre)");
        if (chapter == 5) Assert.Equal(0, loose.Left);   // « on meurt parfois si on joue mal »
        WorldSession guarded = Arena(Typical(level, 3), pack);
        (long Left, bool Won) good = Fight(guarded, pack, PilotProfile.Expert);
        Say("groupe avec garde et parade", level, good, guarded);
        Assert.True(good.Won && good.Left > 0, $"chapitre {chapter} : impossible même en se défendant ({good.Left} PV)");
    }

    /// <summary>« Le Gardien demande garde et parade » : au niveau 25, sans défense ni potion, il gagne ; avec garde, parades et potions, le Bastion gagne.</summary>
    [Fact]
    public void Le_Gardien_demande_garde_et_parade()
    {
        WorldSession bare = Arena(Typical(25, 0), Array.Empty<string>(), boss: true);
        (long Left, bool Won) loose = Fight(bare, new[] { "gardien_du_seuil" }, null);
        Say("Gardien sans défense ni potion", 25, loose, bare);
        Assert.False(loose.Won);
        Assert.Equal(0, loose.Left);
        WorldSession ready = Arena(Typical(25, 5), Array.Empty<string>(), boss: true);
        (long Left, bool Won) good = Fight(ready, new[] { "gardien_du_seuil" }, PilotProfile.Expert);
        Say("Gardien avec garde, parade et cinq potions", 25, good, ready);
        Assert.True(good.Won && good.Left > 0);
    }
}
