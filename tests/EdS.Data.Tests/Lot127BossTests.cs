using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 12.7 : chef de fin et récompenses. Chef à formes successives (le Gardien du Seuil, puis éveillé), première victoire récompensée
/// une seule fois (registre du monde), butin à chaque victoire, défaite sans perte (cadrage Q8, H4).
/// </summary>
[Trait("Category", "World")]
public class Lot127BossTests
{
    private const string Mission = "quest.traces_sous_le_moulin";

    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;
    private static CharacterProgression Progression => ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;
    private static TextCatalog Texts => StoryJsonReader.ReadTextsFile(StoryJsonReader.TextsPathIn(DataDirectory)).Texts!;

    /// <summary>Zone des Vallons ; formes du chef retouchées au besoin ; entrée déplacée ; créatures de la zone calmées au besoin.</summary>
    private static ZoneLoadResult Load(Action<JsonObject>? zone = null, Action<JsonObject>? boss = null, double x = -18, double z = -12, bool calm = false,
        Action<JsonObject>? zoneScenario = null, EdS.Data.GameData? data = null)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(x, z);
        zone?.Invoke(root);
        string directory = Path.Combine(Path.GetTempPath(), "eds_boss_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "zones"));
            JsonObject population = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
            if (calm)   // créatures de la zone inoffensives (le rôdeur garde l'approche du chef)
                foreach (JsonNode? c in population["data"]!["combatants"]!.AsArray())
                    foreach (JsonNode? a in c!["attacks"]!.AsArray()) a!["rawDamage"] = 1;
            zoneScenario?.Invoke(population);
            File.WriteAllText(Path.Combine(directory, "zones", "vallons.json"), population.ToJsonString());
            JsonObject forms = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons_chef")))!.AsObject();
            boss?.Invoke(forms);
            File.WriteAllText(Path.Combine(directory, "zones", "vallons_chef.json"), forms.ToJsonString());
            return ZoneJsonReader.Read(root.ToJsonString(), directory, data ?? ShippedData, Items);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Chef affaibli (60 PV par forme), inoffensif ou mortel ; entrée à côté de lui ; le reste de la zone ne blesse pas.</summary>
    private static ZoneDefinition Arena(long damage = 1, int health = 60, Action<JsonObject>? zoneScenario = null) => Load(boss: f =>
    {
        foreach (JsonNode? c in f["data"]!["combatants"]!.AsArray())
        {
            c!["health"] = health;
            foreach (JsonNode? a in c["attacks"]!.AsArray()) a!["rawDamage"] = damage;
        }
    }, x: 51.5, z: 36, calm: true, zoneScenario: zoneScenario).Zone!;

    private static string First(ZoneLoadResult r)
    {
        Assert.False(r.IsValid);
        return r.Report.Errors[0].Path;
    }

    private static SavedCharacter Hero(SavedStory? story = null, params ItemStack[] items) =>
        PlayerCharacters.Create("bastion", "class.bastion", Progression).WithItems(items, new Dictionary<string, string>()).WithStory(story ?? SavedStory.Empty);

    private static WorldSession Play(ZoneDefinition zone, WorldSave save) =>
        new(save, zone, ShippedData, Progression, Items, StoryJsonReader.ReadStoryFile(StoryJsonReader.StoryPathIn(DataDirectory), Texts, zone, Items).Story!);

    /// <summary>Frappe la forme en cours du chef jusqu'à sa dernière chute (ou la fin du budget).</summary>
    private static List<SessionStep> Fight(WorldSession session, int ticks = 3000)
    {
        var steps = new List<SessionStep>();
        for (int i = 0; i < ticks && !session.BossDefeated && !session.IsDefeated; i++)
        {
            if (i % 20 == 1 && session.BossForm is string form)
                try { session.Zone.Engine.Submit(new CombatCommand(session.Zone.Engine.CurrentTick * 50, "bastion", "skill.bastion.frappe_du_rempart", form)); }
                catch (ArgumentException) { }
            steps.Add(session.Step());
        }
        for (int i = 0; i < 3; i++) steps.Add(session.Step());   // la récompense suit la victoire au même tick, le journal de quête au suivant
        return steps;
    }

    [Fact]
    public void Chef_des_Vallons_livre()
    {
        ZoneLoadResult loaded = Load();
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        Boss boss = loaded.Zone!.Boss!;
        Assert.Equal(("gardien_du_seuil", "fact.chef.vaincu", "loot.gardien"), (boss.Id, boss.Fact, boss.Loot));
        Assert.Equal(new[] { "gardien_du_seuil", "gardien_eveille" }, boss.Forms.Select(f => f.Id));
        Assert.Equal(new[] { ("item.lame_du_seuil", 1) }, boss.Rewards);
        Assert.Equal("zone.vallons/boss/gardien_du_seuil", loaded.Zone.VictoryKey);
        Assert.Equal((ItemRarity.Epic, 8), (Items.Item("item.lame_du_seuil").Rarity, Items.Item("item.lame_du_seuil").RequiredLevel));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "zone.schema.json"))
            .IsValid(JsonDocument.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"))).RootElement));
    }

    [Fact]
    public void Chef_invalide_refuse_avec_son_chemin()
    {
        static JsonObject B(JsonObject r) => r["data"]!["boss"]!.AsObject();
        Assert.Equal("$.data.boss.forms[1]", First(Load(z => B(z)["forms"]![1] = "inconnu")));
        Assert.Equal("$.data.boss.forms[1]", First(Load(z => B(z)["forms"]![1] = "gardien_du_seuil")));
        Assert.Equal("$.data.boss.forms[0]", First(Load(boss: f => f["data"]!["combatants"]![0]!["id"] = "loup1", zone: z => B(z)["forms"]![0] = "loup1")));
        Assert.Equal("$.data.boss.fact", First(Load(z => B(z)["fact"] = "chef_vaincu")));
        Assert.Equal("$.data.boss.rewards[0].item", First(Load(z => B(z)["rewards"]![0]!["item"] = "item.inconnu")));
        Assert.Equal("$.data.boss.rewards[0].count", First(Load(z => B(z)["rewards"]![0]!["count"] = 2)));
        Assert.Equal("$.data.boss.loot", First(Load(z => B(z)["loot"] = "loot.inconnu")));
        Assert.Equal("$.data.boss.scenario", First(Load(z => B(z)["scenario"] = "zones/inconnu")));
        // Avant la revue Codex : la place d'une forme doit tenir dans la simulation de la zone (ici, un rocher ajouté sur la place du
        // chef), sinon son arrivée serait refusée.
        Assert.Equal("$.data.boss.forms[0]", First(Load(zoneScenario: s => s["data"]!["space"]!["obstacles"]!.AsArray().Add(JsonNode.Parse(
            "{\"id\": \"rocher_chef\", \"points\": [[53, 35], [55, 35], [55, 37], [53, 37]]}")))));
        // Le schéma suit les identifiants des formes (réservés « _v<nombre> », uniques).
        MiniJsonSchema schema = MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "zone.schema.json"));
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        B(root)["forms"]![1] = "gardien_du_seuil";
        Assert.False(schema.IsValid(JsonDocument.Parse(root.ToJsonString()).RootElement));
        B(root)["forms"]![1] = "gardien_v2";
        Assert.False(schema.IsValid(JsonDocument.Parse(root.ToJsonString()).RootElement));
    }

    [Fact]
    public void Forme_sur_un_point_sur_refusee()
    {
        // Revue Codex de la PR #48 : une forme (solide pour le personnage : grande créature) sur un point sûr rendrait l'arrivée de l'un ou de
        // l'autre impossible ; refusée au chargement, au chemin de la forme.
        Assert.Equal("$.data.boss.forms[0]", First(Load(x: 54, z: 34.5)));
        Assert.Equal("$.data.boss.forms[1]", First(Load(x: 56, z: 31.5)));
    }

    [Fact]
    public void Forme_dont_la_cible_n_existe_que_dans_le_scenario_du_chef_refusee()
    {
        // Revue Codex de la PR #48 : la seconde forme vise un combattant présent seulement dans le scénario du chef (jamais importé dans la
        // zone) : son arrivée serait refusée par le moteur ; la zone est refusée au chemin de la forme.
        ZoneLoadResult r = Load(boss: f =>
        {
            JsonArray combatants = f["data"]!["combatants"]!.AsArray();
            JsonObject extra = combatants[0]!.DeepClone().AsObject();
            extra["id"] = "sbire";
            extra["team"] = "sauvages";
            extra["position"] = new JsonArray(45, 22);
            combatants.Add(extra);
            combatants[1]!["target"] = "sbire";
        });
        Assert.Equal("$.data.boss.forms[1]", First(r));
        Assert.Contains("simulation de la zone", r.Report.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Formes_qui_ne_tiennent_pas_dans_la_session_refusees() =>
        // Revue Codex de la PR #48 (3e tour) : les créatures (27 depuis les Vallons agrandis), le personnage et 2 formes ne tiennent pas sous
        // un plafond de 29 combattants (la zone seule, 28, y tient).
        CaM01Data.WithDirectory(CaM01Data.RulesWith("\"maxCombatants\": 64", "\"maxCombatants\": 29"), directory =>
        {
            ZoneLoadResult r = Load(data: GameDataLoader.Load(directory).Data!);
            Assert.Equal("$.data.boss.forms", First(r));
            Assert.Equal(ValidationCode.LimitExceeded, r.Report.Errors[0].Code);
            return 0;
        });

    [Fact]
    public void Forme_suivante_attend_une_arrivee_du_meme_tick_sur_sa_place()
    {
        // Revue Codex de la PR #48 (3e tour) : une créature arrive sur la place de la seconde forme au tick même où celle-ci devait se
        // relever ; la forme attend que la place se libère, puis arrive, et le chef reste vainqueur possible.
        WorldSession reference = Play(Arena(), new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero()));
        long relief = Fight(reference).SelectMany(s => s.Boss).Single(b => b.Kind == BossEventKind.FormArrived && b.Phase == 1).Tick;
        ZoneDefinition crowded = Arena(zoneScenario: sc =>
        {
            JsonArray combatants = sc["data"]!["combatants"]!.AsArray();
            JsonObject intruder = combatants.Select(c => c!.AsObject()).First(c => c["id"]!.GetValue<string>() == "loup1").DeepClone().AsObject();
            intruder["id"] = "intrus";
            intruder["position"] = new JsonArray(56, 33);
            intruder["arrivalMs"] = relief * 50;
            combatants.Add(intruder);
        });
        WorldSession session = Play(crowded, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero()));
        List<SessionStep> steps = Fight(session, 6000);
        BossEvent second = steps.SelectMany(s => s.Boss).Single(b => b.Kind == BossEventKind.FormArrived && b.Phase == 1);
        Assert.True(second.Tick > relief);
        Assert.Equal("gardien_eveille", second.Form);
        Assert.True(session.BossDefeated);
    }

    [Fact]
    public void Victoire_reconnue_si_le_personnage_part_au_meme_tick()
    {
        // Revue Codex de la PR #48 (4e tour) : le personnage quitte la zone au tick où la dernière forme tombe ; présent au début du tick et
        // vivant, il reçoit la récompense unique et le fait, comme l'expérience.
        WorldSession reference = Play(Arena(), new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero()));
        long end = Fight(reference).SelectMany(s => s.Boss).Single(b => b.Kind == BossEventKind.Defeated).Tick;
        WorldSession session = Play(Arena(), new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero()));
        var steps = new List<SessionStep>();
        for (int i = 0; i < 3000 && !session.BossDefeated; i++)
        {
            if (i % 20 == 1 && session.BossForm is string form)
                try { session.Zone.Engine.Submit(new CombatCommand(session.Zone.Engine.CurrentTick * 50, "bastion", "skill.bastion.frappe_du_rempart", form)); }
                catch (ArgumentException) { }
            if (session.Zone.Engine.CurrentTick == end - 1) session.Zone.Leave("bastion");
            steps.Add(session.Step());
        }
        Assert.True(session.BossDefeated);
        Assert.True(session.Zone.Engine.View("bastion").HasLeft);
        Assert.Equal(new[] { ("item.lame_du_seuil", 1) }, steps.SelectMany(s => s.Rewards));
        Assert.Contains("zone.vallons/boss/gardien_du_seuil", session.Save().Consumed);
    }

    [Fact]
    public void Victoire_au_dernier_tick_de_la_session_reconnue()
    {
        // Revue Codex de la PR #48 : la dernière forme tombe au tick qui termine la session : la victoire, la récompense unique et le fait
        // comptent quand même.
        WorldSession reference = Play(Arena(), new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero()));
        long end = Fight(reference).SelectMany(s => s.Boss).Single(b => b.Kind == BossEventKind.Defeated).Tick;
        ZoneDefinition shortened = Arena(zoneScenario: s => s["data"]!["durationMs"] = end * 50);
        WorldSession session = Play(shortened, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero()));
        List<SessionStep> steps = Fight(session);
        Assert.True(session.Zone.Engine.IsFinished);
        Assert.True(session.BossDefeated);
        Assert.Equal(new[] { ("item.lame_du_seuil", 1) }, steps.SelectMany(s => s.Rewards));
        Assert.Contains("zone.vallons/boss/gardien_du_seuil", session.Save().Consumed);
    }

    [Fact]
    public void Personnage_qui_porte_l_identite_du_chef_refuse_avant_l_ouverture()
    {
        // Le chef arrive avec la session sous l'identité de sa forme : un personnage du même nom est refusé avant l'ouverture de la zone
        // (aucune ligne de journal).
        ZoneDefinition arena = Arena();
        var log = new MemoryCombatLog();
        SavedCharacter hero = PlayerCharacters.Create("gardien_du_seuil", "class.bastion", Progression);
        Assert.Throws<ArgumentException>(() => new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero), arena, ShippedData,
            Progression, Items, null, null, log));
        Assert.Empty(log.Lines);
    }

    [Fact]
    public void Victoire_recompensee_une_seule_fois()
    {
        ZoneDefinition arena = Arena();
        var start = new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero(new SavedStory(null,
            new Dictionary<string, QuestProgress> { [Mission] = new QuestProgress("chef") })));
        WorldSession first = Play(arena, start);
        List<SessionStep> steps = Fight(first);
        BossEvent[] boss = steps.SelectMany(s => s.Boss).ToArray();
        Assert.Equal(new[] { (BossEventKind.FormArrived, "gardien_du_seuil", 0), (BossEventKind.FormArrived, "gardien_eveille", 1), (BossEventKind.Defeated, "gardien_eveille", 1) },
            boss.Select(b => (b.Kind, b.Form, b.Phase)));
        Assert.Equal(new[] { ("item.lame_du_seuil", 1) }, steps.SelectMany(s => s.Rewards));
        Assert.Equal(1, first.Inventory.Count("item.lame_du_seuil"));
        Assert.Equal(2, first.Inventory.Count("item.eclat_de_seuil"));   // butin de la dernière forme (loot.gardien)
        Assert.Contains(steps.SelectMany(s => s.Story), e => e.Kind == StoryEventKind.FactLearned && e.Id == "fact.chef.vaincu");
        Assert.Contains(steps.SelectMany(s => s.Story), e => e.Kind == StoryEventKind.StepCompleted && e.Step == "chef");
        // Chaque forme a sa mort et son expérience.
        Assert.Equal(new[] { "zone.vallons/1/death/gardien_du_seuil", "zone.vallons/1/death/gardien_eveille" },
            steps.SelectMany(s => s.Experience).Select(g => g.Source.Key));
        WorldSave after = SaveJsonReader.Read(SaveJsonReader.Write(first.Save())).Save!;
        Assert.Contains("zone.vallons/boss/gardien_du_seuil", after.Consumed);

        // Session suivante : le chef est revenu ; vaincu de nouveau, il donne son butin, plus la récompense.
        WorldSession second = Play(arena, after);
        Assert.Equal("gardien_du_seuil", second.BossForm);
        List<SessionStep> again = Fight(second);
        Assert.True(second.BossDefeated);
        Assert.Empty(again.SelectMany(s => s.Rewards));
        Assert.Equal(1, second.Inventory.Count("item.lame_du_seuil"));
        Assert.Equal(4, second.Inventory.Count("item.eclat_de_seuil"));
    }

    [Fact]
    public void Recompense_qui_attend_une_place()
    {
        ZoneDefinition arena = Arena();
        WorldSession session = Play(arena, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(),
            Hero(null, Enumerable.Repeat(new ItemStack("item.gambison", 1), 24).ToArray())));
        List<SessionStep> steps = Fight(session);
        Assert.True(session.BossDefeated);
        Assert.Empty(steps.SelectMany(s => s.Rewards));
        Assert.Single(steps.SelectMany(s => s.Story), e => e.Kind == StoryEventKind.RewardBlocked);
        Assert.DoesNotContain("zone.vallons/boss/gardien_du_seuil", session.Save().Consumed);
        // Une place libérée : la récompense arrive, une fois.
        session.Inventory.Remove("item.gambison", 1);
        SessionStep next = session.Step();
        Assert.Equal(new[] { ("item.lame_du_seuil", 1) }, next.Rewards);
        Assert.Empty(session.Step().Rewards);
        Assert.Contains("zone.vallons/boss/gardien_du_seuil", session.Save().Consumed);
    }

    [Fact]
    public void Defaite_sans_perte_et_retour_au_point_sur()
    {
        // Chef mortel : le Bastion tombe. Rien d'acquis n'est perdu ; la partie reprend au point sûr, PV pleins.
        ZoneDefinition arena = Arena(damage: 100_000, health: 1_000_000);
        var acquired = new SavedStory(new[] { "fact.traces.recit" }, new Dictionary<string, QuestProgress> { [Mission] = new QuestProgress("chef") },
            new Dictionary<string, int> { ["npc.odile"] = 1 }, new[] { "discovery.manifestation" });
        SavedCharacter hero = Hero(acquired, new ItemStack("item.gambison", 1), new ItemStack("item.peau_de_loup", 7)).WithXp(900)
            .WithProfessions(new Dictionary<string, long> { ["forge"] = 30 });
        WorldSession session = Play(arena, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero));
        List<SessionStep> steps = Fight(session);
        Assert.True(session.IsDefeated);
        Assert.Single(steps, s => s.Defeated);
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(session.Save())).Save!;
        SavedCharacter kept = saved.Character!;
        Assert.Equal(hero.Xp, kept.Xp);
        Assert.Equal(hero.Inventory, kept.Inventory);
        Assert.Equal(hero.Story.Facts, kept.Story.Facts);
        Assert.Equal(hero.Story.Quests, kept.Story.Quests);
        Assert.Equal(hero.Story.Trust, kept.Story.Trust);
        Assert.Equal(hero.Story.Discoveries, kept.Story.Discoveries);
        Assert.Equal(hero.Professions, kept.Professions);
        // Reprise : au point d'entrée, vivant, PV pleins.
        WorldSession back = Play(Load().Zone!, saved);
        back.Step();
        var me = back.Zone.Engine.View("bastion");
        Assert.False(me.IsDead);
        Assert.Equal(me.MaxHealth, me.Health);
        Assert.Equal(Load().Zone!.Entry.Position, me.Position);
    }
}
