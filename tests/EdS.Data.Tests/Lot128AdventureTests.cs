using System.Text.Json.Nodes;
using EdS.Data.Crafting;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 12.8 : l'aventure jouée en temps réel (ce que Unity affiche) : chargement du monde et de la sauvegarde, pont temps réel sur
/// la session de zone, cible choisie parmi les proches, messages par clés de texte, actions hors combat, sauvegarde automatique, halte et
/// reprise après une défaite.
/// </summary>
[Trait("Category", "World")]
public class Lot128AdventureTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_aventure_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private string SavePath => Path.Combine(_folder, "partie", "sauvegarde.json");
    private static string Scenarios => Path.Combine(AppContext.BaseDirectory, "scenarios");

    /// <summary>Aventure chargée, premier tick joué (le personnage arrive au point sûr).</summary>
    private AdventureSession New()
    {
        AdventureSession a = AdventureSession.Load(DataDirectory, Scenarios, SavePath);
        a.Advance(0.05);
        return a;
    }

    private static GroundVector Position(AdventureSession a) => a.World.Zone.Engine.View(AdventureSession.PlayerId).Position!.Value;

    private static double Distance(GroundVector p, double x, double z) => Math.Sqrt(Math.Pow(p.X.Raw / 1e4 - x, 2) + Math.Pow(p.Z.Raw / 1e4 - z, 2));

    /// <summary>Marche vers (x, z) jusqu'à <paramref name="within"/> mètres, tick par tick ; rend vrai une fois arrivé.</summary>
    private static bool WalkTo(AdventureSession a, double x, double z, double within = 2)
    {
        for (int i = 0; i < 2000; i++)
        {
            GroundVector p = Position(a);
            double dx = x - p.X.Raw / 1e4, dz = z - p.Z.Raw / 1e4;
            if (Math.Sqrt(dx * dx + dz * dz) <= within)
            {
                a.Combat.SetMove(0, 0);
                a.Advance(0.05);
                return true;
            }
            a.Combat.SetMove(dx, dz);
            a.Advance(0.05);
        }
        return false;
    }

    private static void Wait(AdventureSession a, double seconds)
    {
        for (double t = 0; t < seconds; t += 0.25) a.Advance(0.25);
    }

    [Fact]
    public void Nouvelle_partie_dans_les_Vallons()
    {
        AdventureSession a = New();
        Assert.Null(a.SaveProblem);
        Assert.Equal(new[] { "Nouvelle partie : bienvenue dans les Vallons d'Aube." }, a.DrainMessages());
        Assert.Equal(1, a.Opened);
        Assert.Equal(AdventureSession.PlayerId, a.Combat.PlayerId);
        Assert.Equal(new[] { "npc.odile", "npc.maelle", "npc.gaspard" }, a.Npcs().Select(n => n.Id));
        Assert.Equal("Odile, la meunière", a.Npcs()[0].Name);
        Assert.Equal(6, a.Nodes().Count);
        Assert.All(a.Nodes(), n => Assert.True(n.Ready));
        Assert.Equal(new[] { "place_du_village", "camp_de_la_carriere" }, a.SafePoints().Select(s => s.Id));
        Assert.Empty(a.Journal());
        Assert.Empty(a.Discoveries());
        Assert.Equal("Village", a.PlaceName);
        Assert.Equal(3, a.Professions().Count);
        Assert.True(a.IsBoss("gardien_du_seuil") && a.IsBoss("gardien_eveille") && !a.IsBoss("loup1"));
        // Les créatures sont loin de l'entrée : aucune cible (la plus proche est à plus de 20 m), alors qu'il y a des ennemis en vue.
        Assert.Null(a.Combat.TargetId);
        Assert.NotEmpty(CombatDisplay.LivingEnemies(a.World.Zone.Engine, AdventureSession.PlayerId));
        Assert.False(File.Exists(SavePath));   // rien n'est écrit tant que rien ne change
    }

    [Fact]
    public void Cible_choisie_parmi_les_proches()
    {
        AdventureSession a = New();
        // Vers le pré des loups (sud de la meute) : dès qu'un loup est à 20 m et en vue, il devient la cible, le plus proche d'abord.
        string? target = null;
        for (int i = 0; i < 1200 && target is null; i++)
        {
            GroundVector p = Position(a);
            a.Combat.SetMove(-4 - p.X.Raw / 1e4, 0 - p.Z.Raw / 1e4);
            a.Advance(0.05);
            target = a.Combat.TargetId;
        }
        Assert.NotNull(target);
        GroundVector me = Position(a);
        List<OtherState> near = CombatDisplay.NearbyEnemies(a.World.Zone.Engine, AdventureSession.PlayerId, CombatDisplay.SightObstacles(a.World.Zone.Setup.Space),
            AdventureSession.TargetRange).ToList();
        Assert.Contains(near, o => o.Id == target);
        Assert.All(near, o => Assert.True(Distance(me, o.View.Position!.Value.X.Raw / 1e4, o.View.Position!.Value.Z.Raw / 1e4) <= 20.0001));
        double[] d = near.Select(o => Distance(me, o.View.Position!.Value.X.Raw / 1e4, o.View.Position!.Value.Z.Raw / 1e4)).ToArray();
        Assert.Equal(d.OrderBy(x => x), d);
    }

    [Fact]
    public void Parler_a_Odile_commence_la_mission_et_sauvegarde()
    {
        AdventureSession a = New();
        a.DrainMessages();
        Assert.False(a.Interact());
        Assert.Equal(new[] { "Personne à qui parler, rien à récolter ici." }, a.DrainMessages());
        Assert.True(WalkTo(a, -8, 3, 2.5));
        Assert.Equal("npc.odile", a.NpcInReach!.Id);
        Assert.True(a.Interact());
        Assert.Equal("Odile, la meunière", a.DialogueNpc);
        Assert.Equal("Des griffes ont labouré la terre autour de mon moulin. Toutes les nuits, ça recommence.", a.DialogueText);
        Assert.Equal(new[] { "Racontez-moi." }, a.DialogueAnswers);
        Assert.Equal(DialogueRefusal.None, a.Choose(0));   // « Racontez-moi. »
        Assert.Equal(DialogueRefusal.None, a.Choose(0));   // « Je vais suivre ces traces. »
        Assert.Null(a.Dialogue);
        string[] said = a.DrainMessages().ToArray();
        Assert.Contains("Nouvelle quête : Les traces sous le moulin", said);
        Assert.Contains("Odile, la meunière t'accorde davantage sa confiance.", said);
        Assert.DoesNotContain(said, m => m.StartsWith("text.", StringComparison.Ordinal));
        JournalEntry quest = Assert.Single(a.Journal());
        Assert.False(quest.Done);
        Assert.NotNull(quest.Step);
        // Sauvegarde automatique : quelque chose a changé, au plus tard 5 s après.
        Wait(a, AdventureSession.AutosaveSeconds + 0.5);
        WorldSave saved = SaveStore.Read(SavePath).Save!;
        Assert.True(saved.Character!.Story.Quests.ContainsKey("quest.traces_sous_le_moulin"));
        Assert.Equal(1, saved.Character.Story.Trust["npc.odile"]);

        // Reprise : la partie continue là où elle en était (nouvelle session, même mission).
        AdventureSession again = New();
        Assert.Equal(new[] { "Partie reprise." }, again.DrainMessages());
        Assert.Equal(Assert.Single(a.Journal()).Step, Assert.Single(again.Journal()).Step);
    }

    [Fact]
    public void Halte_au_point_sur()
    {
        AdventureSession a = New();
        Assert.True(a.CanRest);
        Assert.True(WalkTo(a, -8, 3, 2.5));
        Assert.False(a.CanRest);   // loin du point sûr
        Assert.False(a.Rest());
        Assert.True(WalkTo(a, -18, -12, 1));
        a.DrainMessages();
        long tick = a.World.Zone.WorldTick;
        Assert.True(a.Rest());
        Assert.Equal(2, a.Opened);
        Assert.Equal(new[] { "Halte à Place du village : partie sauvegardée, PV pleins." }, a.DrainMessages());
        Assert.True(File.Exists(SavePath));
        Assert.Equal(1, SaveStore.Read(SavePath).Save!.Sessions);   // la sauvegarde de la halte clôt la première session
        Assert.True(a.World.Zone.WorldTick >= tick);
        Assert.Equal(new GroundVector(Fixed.FromInt(-18), Fixed.FromInt(-12)), Position(a));
    }

    [Fact]
    public void Recolte_et_atelier()
    {
        AdventureSession a = New();
        a.DrainMessages();
        // Atelier du village : rien n'est faisable sans matériaux.
        RecipeEntry ferrure = a.Recipes().Single(r => r.Id == "recipe.ferrure");
        Assert.Equal("Ferrure", ferrure.Name);
        Assert.Equal("Forge", ferrure.Profession);
        Assert.Equal(CraftRefusal.NotEnough, ferrure.Refusal);
        Assert.Equal("Minerai de fer 0/3", ferrure.Inputs);
        Assert.Equal(CraftRefusal.NotEnough, a.Craft("recipe.ferrure"));
        Assert.Equal(new[] { "Il te manque des matériaux." }, a.DrainMessages());
        // Herbes du village : récoltées, puis épuisées.
        WorldMarker herbs = a.Nodes().Single(n => n.Id == "herbes_village");
        Assert.True(WalkTo(a, herbs.X, herbs.Z, 1.5));
        a.DrainMessages();
        Assert.True(a.Interact());
        Assert.StartsWith("Récolté : Herbe de l'aube ×", Assert.Single(a.DrainMessages()));
        Assert.False(a.Nodes().Single(n => n.Id == "herbes_village").Ready);
        Assert.Null(a.NodeInReach);
    }

    [Fact]
    public void Boire_sans_objet_de_soin()
    {
        AdventureSession a = New();
        a.DrainMessages();
        Assert.False(a.Drink());
        Assert.Equal(new[] { "Aucun objet de soin emporté." }, a.DrainMessages());
    }

    [Fact]
    public void Equipement_refuse_avec_un_message_traduit()
    {
        AdventureSession a = New();
        a.DrainMessages();
        Assert.Equal(InventoryRefusal.NothingThere, a.Equip(0));
        Assert.Equal(InventoryRefusal.NothingThere, a.Unequip("weapon"));
        Assert.Equal(new[] { "Rien à cet endroit.", "Rien à cet endroit." }, a.DrainMessages());
    }

    [Fact]
    public void Tous_les_refus_ont_leur_texte()
    {
        TextCatalog texts = StoryJsonReader.ReadTextsFile(StoryJsonReader.TextsPathIn(DataDirectory)).Texts!;
        foreach (CraftRefusal r in Enum.GetValues<CraftRefusal>()) Assert.True(texts.Has("text.ui.craft." + r.ToString().ToLowerInvariant()), r.ToString());
        foreach (InventoryRefusal r in Enum.GetValues<InventoryRefusal>().Where(r => r != InventoryRefusal.None))
            Assert.True(texts.Has("text.ui.inventory." + r.ToString().ToLowerInvariant()), r.ToString());
        ItemCatalog items = ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;
        foreach (string slot in items.Slots) Assert.True(texts.Has("text.ui.slot." + slot), slot);
    }

    [Fact]
    public void Sauvegarde_illisible_signalee_puis_nouvelle_partie()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        File.WriteAllText(SavePath, "{ abîmée");
        AdventureSession a = New();
        Assert.NotNull(a.SaveProblem);
        Assert.Equal(0, a.World.Character.Xp);
        a.Save();
        Assert.Null(SaveStore.Read(SavePath).Problem);
    }

    /// <summary>Données copiées, un sanglier mortel posté près de l'entrée (ou sur elle).</summary>
    private (string Data, string Scenarios) DeadlyCopy(double x = -14)
    {
        string data = Path.Combine(_folder, "data", "v0"), scenarios = Path.Combine(_folder, "scenarios");
        Copy(DataDirectory, data);
        Copy(Scenarios, scenarios);
        string zone = Path.Combine(scenarios, "zones", "vallons.json");
        JsonObject root = JsonNode.Parse(File.ReadAllText(zone))!.AsObject();
        JsonObject boar = root["data"]!["combatants"]!.AsArray().Single(c => (string)c!["id"]! == "sanglier")!.AsObject();
        boar["position"] = new JsonArray(x, -12);
        foreach (JsonNode? attack in boar["attacks"]!.AsArray()) attack!["rawDamage"] = 100_000;
        File.WriteAllText(zone, root.ToJsonString());
        return (data, scenarios);
    }

    private static void Copy(string from, string to)
    {
        foreach (string dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(dir.Replace(from, to));
        Directory.CreateDirectory(to);
        foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories)) File.Copy(file, file.Replace(from, to));
    }

    [Fact]
    public void Defaite_puis_reprise_au_dernier_point_sur()
    {
        (string data, string scenarios) = DeadlyCopy();
        AdventureSession a = AdventureSession.Load(data, scenarios, SavePath);
        a.Advance(0.05);
        a.DrainMessages();
        Assert.False(a.Continue());
        for (int i = 0; i < 1200 && !a.World.IsDefeated; i++) a.Advance(0.05);
        Assert.True(a.World.IsDefeated);
        Assert.True(a.MustContinue);
        Assert.False(a.CanRest);
        Assert.Contains("Tu es tombé. Rien de ce que tu as acquis n'est perdu.", a.DrainMessages());
        long tick = a.Combat.Tick;
        Assert.Equal(0, a.Advance(1));   // tombé : plus rien n'avance avant la reprise
        Assert.Equal(tick, a.Combat.Tick);
        Assert.True(a.Continue());
        Assert.Equal(2, a.Opened);
        Assert.Equal(new[] { "Tu reprends à Place du village, PV pleins." }, a.DrainMessages());
        CombatantView me = a.World.Zone.Engine.View(AdventureSession.PlayerId);
        Assert.False(me.IsDead);
        Assert.Equal(me.MaxHealth, me.Health);
        Assert.False(a.World.IsDefeated);
    }

    [Fact]
    public void Arrivee_refusee_puis_nouvel_essai()
    {
        // Une créature sur le point sûr : le personnage n'arrive pas ; la partie le dit et une reprise réessaie.
        (string data, string scenarios) = DeadlyCopy(x: -18);
        AdventureSession a = AdventureSession.Load(data, scenarios, SavePath);
        a.Advance(0.05);
        Assert.True(a.Stranded);
        Assert.True(a.MustContinue);
        Assert.False(a.World.IsDefeated);
        Assert.Contains("Une créature occupe le point sûr : impossible d'y arriver pour l'instant.", a.DrainMessages());
        Assert.Equal(0, a.Advance(1));
        Assert.True(a.Continue());
        Assert.Equal(2, a.Opened);
        a.Advance(0.05);
        Assert.True(a.Stranded);   // le sanglier revient à sa place : toujours occupé
    }

    [Fact]
    public void Pont_temps_reel_sur_un_moteur_tenu_ailleurs()
    {
        AdventureSession a = New();
        Zone zone = a.World.Zone;
        int steps = 0;
        Assert.Throws<ArgumentException>(() => new RealtimeCombat(zone.Setup, zone.Engine, "loup1", () => steps++, null));
        Assert.Throws<ArgumentException>(() => new RealtimeCombat(zone.Setup, zone.Engine, "personne", () => steps++, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RealtimeCombat(zone.Setup, zone.Engine, "bastion", () => steps++, Fixed.Zero));
        var bridge = new RealtimeCombat(zone.Setup, zone.Engine, "bastion", () => { steps++; zone.Engine.Step(); }, null);
        long before = zone.Engine.CurrentTick;
        Assert.Equal(5, bridge.Advance(1.0));   // au plus 5 ticks par appel
        Assert.Equal(5, steps);
        Assert.Equal(before + 5, zone.Engine.CurrentTick);
    }

    [Fact]
    public void Session_ouverte_a_un_point_sur_choisi()
    {
        ZoneDefinition zone = ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"), Scenarios, ShippedData,
            ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!).Zone!;
        var progression = ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;
        ItemCatalog items = ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;
        var save = new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), PlayerCharacters.Create("bastion", "class.bastion", progression));
        var camp = new WorldSession(save, zone, ShippedData, progression, items, safePoint: "camp_de_la_carriere");
        camp.Step();
        Assert.Equal(new GroundVector(Fixed.FromInt(26), Fixed.FromInt(-15)), camp.Zone.Engine.View("bastion").Position);
        Assert.Throws<ArgumentException>(() => new WorldSession(save, zone, ShippedData, progression, items, safePoint: "nulle_part"));
    }
}
