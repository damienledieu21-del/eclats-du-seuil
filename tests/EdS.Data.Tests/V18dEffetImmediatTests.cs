using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.8d (décision de Damien : effet immédiat) : un bonus consommé compte dans le combat dès le tick simulé suivant, un bonus fini
/// n'y compte plus dès le premier tick qu'il ne couvre pas, un remplacement échange les effets d'un coup ; le tout par la commande « bonus
/// modifié » du moteur, journalisée et rejouée à l'identique.
/// </summary>
[Trait("Category", "World")]
public class V18dEffetImmediatTests
{
    private static readonly ZoneDefinition Vallons = Zone();

    private const string Vigueur = "item.elixir_de_vigueur", Garde = "item.elixir_de_garde", Seuil = "item.elixir_du_seuil";
    private const string Pierre = "item.pierre_a_aiguiser", Huile = "item.huile_d_eclat";
    private const string Ragout = "item.test_ragout";   // plat d'essai, ajouté au catalogue pour ces tests seulement (aucun plat livré)

    private static readonly ItemCatalog WithMeal = MealCatalog();

    private static ItemCatalog MealCatalog()
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ItemJsonReader.PathIn(DataDirectory)))!.AsObject();
        root["data"]!["items"]!.AsArray().Add(new JsonObject
        {
            ["id"] = Ragout, ["name"] = "Ragoût d'essai", ["type"] = "boon", ["rarity"] = "common", ["stack"] = 5, ["boon"] = "meal", ["quality"] = "superior",
            ["bonus"] = new JsonObject { ["maxEndurance"] = 6 },
        });
        return ItemJsonReader.Read(root.ToJsonString(), ShippedData).Catalog!;
    }

    private static WorldSave SaveOf(SavedCharacter hero) => new("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, hero);

    private static WorldSession Reopen(WorldSave save, ItemCatalog? items = null, ICombatLog? log = null) =>
        new(save, Vallons, ShippedData, Progression, items ?? ItemsCatalog, log: log);

    private static WorldSession Open(SavedCharacter hero, ItemCatalog? items = null, ICombatLog? log = null)
    {
        WorldSession w = Reopen(SaveOf(hero), items, log);
        w.Step();
        return w;
    }

    private static CombatantView Me(WorldSession w) => w.Zone.Engine.View("bastion");

    private static GearBonus Without(WorldSession w) => w.Inventory.Gear();

    [Fact]
    public void Consomme_l_effet_est_dans_le_tick_simule_suivant()
    {
        WorldSession w = Open(Hero(0, new ItemStack(Vigueur, 1)));
        Assert.False(w.InCombat);
        GearBonus entry = w.EnteredGear;
        long maxBefore = Me(w).MaxHealth!.Value;
        Assert.Equal(BoonRefusal.None, w.UseBoon(Vigueur));
        GearBonus wanted = entry + new GearBonus(maxHealth: 40);
        Assert.Equal(wanted, w.CombatGear);
        CombatCommand sent = w.Zone.Engine.SubmittedCommands.Last();
        Assert.Equal((CombatAction.Gear, w.Zone.Engine.CurrentTick * 50, wanted), (sent.Action, sent.AtMs, sent.Gear));
        Assert.Equal(entry, Me(w).Gear);   // pas encore simulé
        w.Step();
        Assert.Equal(wanted, Me(w).Gear);
        Assert.True(Me(w).MaxHealth > maxBefore);
        Assert.Equal(Me(w).MaxHealth, Me(w).Health);   // pleins : pleins au nouveau maximum
        Assert.Equal(entry, w.EnteredGear);   // l'en-tête de la session ne change pas
    }

    [Fact]
    public void Retire_au_tick_exact_de_la_fin()
    {
        SavedCharacter hero = Hero().WithBoons(new[] { new SavedBoon(BoonKind.Elixir, Vigueur, 3) });
        WorldSession w = Reopen(SaveOf(hero));
        GearBonus with = w.EnteredGear, without = Without(w);
        Assert.Equal(without + new GearBonus(maxHealth: 40), with);
        // Le bonus couvre exactement ses 3 ticks : 0, 1 et 2 ; le tick 3 est simulé sans lui (bonus modifié au début du tick 3).
        var gearAtTick = new List<GearBonus?>();
        for (int i = 0; i < 5; i++)
        {
            w.Step();
            gearAtTick.Add(Me(w).Gear);   // bonus avec lequel le tick i vient d'être simulé
        }
        Assert.Equal(new GearBonus?[] { with, with, with, without, without }, gearAtTick);
        Assert.Null(w.BoonAt(BoonKind.Elixir));
        Assert.Equal(without, w.CombatGear);
        CombatCommand removal = Assert.Single(w.Zone.Engine.SubmittedCommands, c => c.Action == CombatAction.Gear);
        Assert.Equal(3 * 50, removal.AtMs);
    }

    [Fact]
    public void Remplacement_echange_les_effets_sans_trou_ni_double_compte()
    {
        WorldSession w = Open(Hero(0, new ItemStack(Vigueur, 2), new ItemStack(Garde, 1)));
        GearBonus without = Without(w);
        Assert.Equal(BoonRefusal.None, w.UseBoon(Vigueur));
        w.Step();
        Assert.Equal(without + new GearBonus(maxHealth: 40), Me(w).Gear);
        int before = w.Zone.Engine.SubmittedCommands.Count;
        // Le même objet repris : même total, aucun bonus modifié (rien ne bouge en combat).
        Assert.Equal(BoonRefusal.None, w.UseBoon(Vigueur));
        Assert.Equal(before, w.Zone.Engine.SubmittedCommands.Count);
        // L'élixir de garde remplace l'élixir de vigueur : un seul changement, au tick suivant, ni l'un et l'autre, ni aucun.
        Assert.Equal(BoonRefusal.None, w.UseBoon(Garde));
        Assert.Equal(before + 1, w.Zone.Engine.SubmittedCommands.Count);
        w.Step();
        Assert.Equal(without + new GearBonus(armor: 25, statusResistance: 10), Me(w).Gear);
    }

    [Fact]
    public void Trois_places_ensemble_dans_la_limite_du_combat()
    {
        SavedCharacter hero = Inventory.Outfit(Hero(0, new ItemStack(Ragout, 1), new ItemStack(Seuil, 1), new ItemStack(Huile, 1)), WithMeal);
        WorldSession w = Open(hero, WithMeal);
        GearBonus without = Without(w);
        foreach (string item in new[] { Huile, Ragout, Seuil }) Assert.Equal(BoonRefusal.None, w.UseBoon(item));
        w.Step();
        GearBonus expected = without + new GearBonus(physicalPower: 7, maxHealth: 60, maxEndurance: 16);
        Assert.Equal(expected, Me(w).Gear);
        Assert.All(Enumerable.Range(0, GearBonus.Fields.Count), g => Assert.True(Me(w).Gear![g] <= GearBonus.MaxValue));
        // Propriété : pour tout ensemble de bonus, le bonus du combat est la somme ramenée à la limite, champ par champ.
        var near = new GearBonus(physicalPower: GearBonus.MaxValue - 3, maxHealth: GearBonus.MaxValue, maxEndurance: 5);
        SavedBoon[] all = { new(BoonKind.Meal, Ragout, 10), new(BoonKind.Elixir, Seuil, 10), new(BoonKind.Weapon, Huile, 10) };
        for (int mask = 0; mask < 8; mask++)
        {
            SavedBoon[] some = all.Where((_, i) => (mask & (1 << i)) != 0).ToArray();
            GearBonus total = WorldSession.BoonGear(near, WithMeal, some), extra = WithMeal.BoonBonus(some);
            for (int g = 0; g < GearBonus.Fields.Count; g++) Assert.Equal(Math.Min((long)near[g] + extra[g], GearBonus.MaxValue), total[g]);
        }
    }

    [Fact]
    public void Sauvegarde_et_reprise_en_plein_bonus_memes_statistiques()
    {
        WorldSession w = Open(Hero(0, new ItemStack(Seuil, 1), new ItemStack(Pierre, 1)));
        Assert.Equal(BoonRefusal.None, w.UseBoon(Seuil));
        Assert.Equal(BoonRefusal.None, w.UseBoon(Pierre));
        for (int i = 0; i < 50; i++) w.Step();
        CombatantView here = Me(w);
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!;
        WorldSession back = Reopen(saved);
        CombatantView there = Me(back);
        Assert.Equal(here.Gear, there.Gear);
        Assert.Equal(w.CombatGear, back.EnteredGear);
        Assert.Equal((here.MaxHealth, here.MaxEndurance), (there.MaxHealth, there.MaxEndurance));
        Assert.Equal(w.CombatGear, back.CombatGear);
    }

    [Fact]
    public void Journal_de_la_zone_rejoue_octet_par_octet()
    {
        var log = new MemoryCombatLog();
        WorldSession w = Open(Hero(0, new ItemStack(Vigueur, 1), new ItemStack(Garde, 1)).WithBoons(new[] { new SavedBoon(BoonKind.Weapon, Pierre, 30) }), log: log);
        Assert.Equal(BoonRefusal.None, w.UseBoon(Vigueur));
        for (int i = 0; i < 40; i++) w.Step();
        Assert.Equal(BoonRefusal.None, w.UseBoon(Garde));
        for (int i = 0; i < 40; i++) w.Step();
        Assert.Equal(3, w.Zone.Engine.SubmittedCommands.Count(c => c.Action == CombatAction.Gear));   // vigueur, fin de la pierre, garde
        Assert.Equal(3, log.Lines.Count(l => l.Contains("\"event\":\"gearChanged\"", StringComparison.Ordinal)));
        // Le combat complété (arrivées et commandes soumises) rejoue la zone : mêmes lignes, même état.
        var again = new MemoryCombatLog();
        var replay = new CombatEngine(w.Zone.Engine.CompletedSetup(), again);
        while (replay.CurrentTick < w.Zone.Engine.CurrentTick) replay.Step();
        Assert.Equal(log.Lines.Skip(1), again.Lines.Skip(1));
        // Même état du personnage (les mesures de charge de la file, elles, dépendent de l'ordre des mises en file du pilote).
        CombatantView here = Me(w), there = replay.View("bastion");
        Assert.Equal((here.Gear, here.Health, here.MaxHealth, here.Endurance, here.MaxEndurance), (there.Gear, there.Health, there.MaxHealth, there.Endurance, there.MaxEndurance));
    }

    [Fact]
    public void Refuse_par_le_pilote_retente_au_tick_suivant()
    {
        WorldSession w = Open(Hero(0, new ItemStack(Vigueur, 1)));
        GearBonus without = Without(w);
        // Le plafond de commandes par acteur et par tick est atteint : le bonus modifié attend le tick suivant (l'objet est bien consommé).
        long at = w.Zone.Engine.CurrentTick * 50;
        int cap = w.Zone.Setup.Combat.MaxCommandsPerActorPerTick;
        for (int i = 0; i < cap; i++) w.Zone.Engine.Submit(new CombatCommand(at, "bastion", CombatAction.GuardStop));
        Assert.Equal(BoonRefusal.None, w.UseBoon(Vigueur));
        Assert.DoesNotContain(w.Zone.Engine.SubmittedCommands, c => c.Action == CombatAction.Gear);
        w.Step();
        Assert.Equal(without, Me(w).Gear);
        CombatCommand late = Assert.Single(w.Zone.Engine.SubmittedCommands, c => c.Action == CombatAction.Gear);
        Assert.Equal(at + 50, late.AtMs);
        w.Step();
        Assert.Equal(without + new GearBonus(maxHealth: 40), Me(w).Gear);
    }

    [Fact]
    public void Refus_de_consommation_aucun_bonus_modifie()
    {
        WorldSession w = Open(Hero(0, new ItemStack("item.herbe_de_l_aube", 1)));
        int before = w.Zone.Engine.SubmittedCommands.Count;
        Assert.Equal(BoonRefusal.NotBoon, w.UseBoon("item.herbe_de_l_aube"));
        Assert.Equal(BoonRefusal.NothingThere, w.UseBoon(Vigueur));
        Assert.Equal(before, w.Zone.Engine.SubmittedCommands.Count);
    }
}
