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
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Essai du 12.9 (Damien, 25 octobre 2026, 3 h 13 : « Je ne peux pas battre les ennemis, ils sont trop forts ») : les créatures des Vallons
/// venaient de la démonstration du lot 11 (Bastion niveau 10, tous ses talents). Rééquilibrées pour un personnage qui débute ; ces combats
/// simulés (le Bastion frappe dès que ses compétences sont prêtes et marche vers sa cible, sans garde ni parade) gardent l'équilibre voulu.
/// </summary>
[Trait("Category", "World")]
public class Lot129BalanceTests
{
    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;
    private static CharacterProgression Progression => ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;

    /// <summary>Vallons livrés ; la créature choisie est amenée près de l'entrée (ou l'entrée près du chef, le reste de la zone calmé).</summary>
    private static WorldSession Duel(string creature, long xp, params ItemStack[] items)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        string directory = Path.Combine(Path.GetTempPath(), "eds_equilibre_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "zones"));
            JsonObject zone = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
            if (creature == "gardien")
            {
                root["data"]!["safePoints"]![0]!["position"] = new JsonArray(51.5, 36);
                foreach (JsonNode? c in zone["data"]!["combatants"]!.AsArray()) foreach (JsonNode? a in c!["attacks"]!.AsArray()) a!["rawDamage"] = 1;
            }
            else
                foreach (JsonNode? c in zone["data"]!["combatants"]!.AsArray())
                    if ((string)c!["id"]! == creature) c["position"] = new JsonArray(-15, -12);
            File.WriteAllText(Path.Combine(directory, "zones", "vallons.json"), zone.ToJsonString());
            File.Copy(ScenarioPath("zones/vallons_chef"), Path.Combine(directory, "zones", "vallons_chef.json"));
            File.Copy(ScenarioPath("zones/vallons_bandits"), Path.Combine(directory, "zones", "vallons_bandits.json"), true);   // sous-lot V1.6 : formes du chef des bandits
            CopyMissingZoneScenarios(directory);   // intérieurs : reliés aux Vallons par leurs portes
            ItemCatalog catalog = Items;
            ZoneDefinition z = ZoneJsonReader.Read(root.ToJsonString(), directory, ShippedData, catalog).Zone!;
            SavedCharacter hero = PlayerCharacters.Create("bastion", "class.bastion", Progression).WithXp(xp).WithItems(items, new Dictionary<string, string>());
            return new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero), z, ShippedData, Progression, catalog);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Combat simulé : compétences dès qu'elles sont prêtes, marche vers la cible, potion sous 40 % des PV ; rend les PV restants.</summary>
    private static long Fight(WorldSession session, string target, int seconds = 300)
    {
        CombatEngine e = session.Zone.Engine;
        bool boss = target.StartsWith("gardien", StringComparison.Ordinal);
        for (int i = 0; i < seconds * 20; i++)
        {
            if (boss && session.BossForm is string form) target = form;
            CombatantView me = e.View("bastion"), it = e.View(target);
            if (me.IsDead || session.BossDefeated || (!boss && it.IsDead)) break;
            long now = e.CurrentTick * 50;
            // Sous-lot 12.9b : toutes les compétences équipées (gagnées en montant de niveau comprises) ; la Barrière d'acier sur soi.
            // Une compétence par tick, à tour de rôle (plafond de commandes par combattant et par tick).
            string[] skills = session.Character.ActiveSkills.Append(session.Character.Ultimate).OfType<string>().ToArray();
            string skill = skills[i % skills.Length];
            try { e.Submit(new CombatCommand(now, "bastion", skill, skill == "skill.bastion.barriere_d_acier" ? "bastion" : target)); }
            catch (ArgumentException) { }
            if (me.Position is GroundVector a && it.Position is GroundVector b && it.IsPresent)
            {
                double dx = (b.X.Raw - a.X.Raw) / 1e4, dz = (b.Z.Raw - a.Z.Raw) / 1e4, d = Math.Sqrt(dx * dx + dz * dz);
                try
                {
                    e.Submit(d > 2.2
                        ? new CombatCommand(now, "bastion", new GroundVector(Fixed.FromRaw((long)(dx / d * 1e4)), Fixed.FromRaw((long)(dz / d * 1e4))))
                        : new CombatCommand(now, "bastion", CombatAction.Stop));
                }
                catch (ArgumentException) { }
            }
            if (me.Health < me.MaxHealth * 4 / 10 && me.ConsumableCharges.TryGetValue("item.potion_de_soin", out int left) && left > 0)
                try { session.Drink("item.potion_de_soin"); } catch (ArgumentException) { }
            session.Step();
        }
        return e.View("bastion").Health ?? 0;
    }

    [Fact]
    public void On_peut_semer_les_loups()
    {
        // Retour de Damien (fiche du 12.9) : « le loup est trop rapide je ne peux pas le fuir ». Les loups courent moins vite que le
        // Bastion : l'écart grandit jusqu'à leur laisse (18 m), et ils lâchent.
        JsonObject zone = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
        double player = (double)JsonNode.Parse(File.ReadAllText(Path.Combine(DataDirectory, "rules.json")))!["data"]!["combat"]!["movement"]!["speed"]!;
        // Sous-lot V1.6 : le loup blanc, bête élite, est à part (plus rapide que les loups, moins que le Bastion : on peut encore le semer).
        var wolves = zone["data"]!["combatants"]!.AsArray().Where(c => ((string)c!["id"]!).StartsWith("loup", StringComparison.Ordinal) && (string)c!["id"]! != "loup_blanc").ToList();
        Assert.Equal(12, wolves.Count);   // Vallons agrandis : sept loups de plus (forêt des Loups, hautes pierres)
        Assert.All(wolves, w => Assert.Equal(4.0, (double)w!["speed"]!));
        Assert.Equal(5.0, player);
        double white = (double)zone["data"]!["combatants"]!.AsArray().Single(c => (string)c!["id"]! == "loup_blanc")!["speed"]!;
        Assert.True(white > 4.0 && white < player);
    }

    [Theory]
    [InlineData("loup1", 0)]
    [InlineData("sanglier", 0)]
    [InlineData("archer", 0)]
    [InlineData("rodeur", 180)]   // niveau 3 (sous-lot V1.8 : au village, le rôdeur reste au niveau 1 ; plus haut, il monte avec le joueur)
    public void Une_creature_seule_se_bat_au_niveau_prevu(string creature, long xp)
    {
        WorldSession session = Duel(creature, xp);
        long left = Fight(session, creature);
        Assert.True(session.Zone.Engine.View(creature).IsDead, $"{creature} encore debout");
        Assert.True(left >= 50, $"{creature} : il ne reste que {left} PV au Bastion");
    }

    // Sous-lot V1.8 : le Gardien est la fin du chapitre 5 (niveau 25), au Seuil (niveau 21 au moins) : son combat est réglé dans
    // V18EquilibreTests (Gardien au niveau 25, garde et parade demandées) ; l'essai du niveau 10 n'a plus de sens.

    [Fact]
    public void Experience_des_creatures()
    {
        // Une dizaine de créatures pour les premiers niveaux ; le Gardien vaut une montée de niveau.
        CharacterProgression p = Progression;
        Assert.Equal((25L, 30L, 20L, 36L, 41L, 66L), (p.KillXp(150), p.KillXp(180), p.KillXp(120), p.KillXp(220), p.KillXp(250), p.KillXp(400)));
    }
}
