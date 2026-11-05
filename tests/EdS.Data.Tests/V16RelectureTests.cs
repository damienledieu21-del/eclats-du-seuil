using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Relecture indépendante de la PR #76 (sous-lot V1.6), premier tour : sondes P1 à P5 devenues tests de non-régression, et les autres
/// remarques (quête qui s'ouvre « après » celle qui la commence, cycles d'ouverture, livraison atomique avec l'étape, dialogue pendant le
/// choix de la récompense, expérience comptée avant l'ouverture, place de la clé de victoire au registre, chefs qui se chevauchent,
/// récompense du chef des bandits, deux chefs vaincus au même tick, potions réservées jamais livrées).
/// </summary>
[Trait("Category", "World")]
public class V16RelectureTests
{
    private const string Peaux = "quest.peaux_pour_l_auberge", Bete = "quest.bete_blanche";

    private static JsonObject StoryJson() => JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!.AsObject();

    private static StoryLoadResult Read(JsonObject root) => StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Zone(), ItemsCatalog, V16TestData.Interiors);

    private static ZoneDefinition Inside(string id) => V16TestData.Interiors.Single(z => z.Id == id);

    private static JsonObject Npc(JsonObject root, string id) => root["data"]!["npcs"]!.AsArray().Single(n => (string)n!["id"]! == id)!.AsObject();

    private static JsonObject Quest(JsonObject root, string id) => root["data"]!["quests"]!.AsArray().Single(q => (string)q!["id"]! == id)!.AsObject();

    private static JsonObject Dialogue(JsonObject root, string id) => root["data"]!["dialogues"]!.AsArray().Single(q => (string)q!["id"]! == id)!.AsObject();

    private static StoryBook NearDoor(string npc, Action<JsonObject>? more = null)
    {
        JsonObject root = StoryJson();
        JsonObject n = Npc(root, npc);
        Door door = Inside((string)n["zone"]!).Doors[0];
        n["position"] = new JsonArray(Math.Round(door.Position.X.Raw / 10000.0, 4), Math.Round(door.Position.Z.Raw / 10000.0 + 1.5, 4));
        more?.Invoke(root);
        StoryLoadResult r = Read(root);
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Story!;
    }

    private static WorldSession InRoom(string zone, StoryBook book, SavedCharacter hero)
    {
        var s = new WorldSession(new WorldSave(zone, 0, 0, new[] { PlayerCharacters.PlacesKey }, hero), Inside(zone), ShippedData, Progression, ItemsCatalog, book,
            door: "sortie", storyZone: "zone.vallons");
        s.Step();
        return s;
    }

    private static SavedCharacter WithQuest(SavedCharacter hero, string quest, string step, long progress = 0) =>
        hero.WithStory(new SavedStory(null, new Dictionary<string, QuestProgress> { [quest] = new QuestProgress(step, progress) }));

    private static List<string> Steps(WorldSession w, int ticks)
    {
        var e = new List<string>();
        for (int i = 0; i < ticks; i++) e.AddRange(w.Step().Story.Select(x => x.ToString()));
        return e;
    }

    // --- P1 : une quête qui en commence une autre ouverte « après elle » --------------------------------------------------

    [Fact]
    public void P1_quete_qui_commence_la_suivante_ouverte_apres_elle()
    {
        // « La bête blanche » s'ouvre après « Des peaux pour l'auberge », qui la commence à sa fin : avant la relecture, la dernière étape
        // était refusée à chaque tick (QuestLocked), les peaux déjà prises.
        StoryBook book = NearDoor("npc.aubergiste", r =>
        {
            Quest(r, Bete)["requires"] = new JsonArray(new JsonObject { ["if"] = "questDone", ["id"] = Peaux });
            Quest(r, Peaux)["effects"]!.AsArray().Add(new JsonObject { ["do"] = "startQuest", ["id"] = Bete });
            Quest(r, Peaux).Remove("rewardChoice");
        });
        WorldSession w = InRoom("zone.auberge", book, WithQuest(Hero(0, new ItemStack("item.peau_de_loup", 3)), Peaux, "peaux"));
        Deliver(w, "npc.aubergiste");   // réponses du 31 octobre 2026 : la livraison est une réponse du dialogue
        List<string> events = Steps(w, 20);
        Assert.DoesNotContain(events, e => e.StartsWith("QuestLocked", StringComparison.Ordinal));
        Assert.Contains($"QuestCompleted {Peaux}", events);
        Assert.Contains($"QuestStarted {Bete}/temoin", events);
        Assert.Equal(1, events.Count(e => e == "ItemTaken item.peau_de_loup 3"));
        Assert.True(w.Narrative!.IsDone(Peaux) && w.Narrative.IsActive(Bete));
        Assert.Equal(0, w.Inventory.Count("item.peau_de_loup"));
    }

    [Fact]
    public void Quete_a_recompense_au_choix_ouvre_la_suivante_sur_questReward()
    {
        // Revue Codex de la PR #76 : l'état d'après la dernière étape d'une quête à récompense au choix est l'attente du choix. La suivante,
        // ouverte sur questReward, commence ; ouverte sur questDone, elle ne commence pas trop tôt (l'étape attend, rien n'est pris).
        WorldSession Play(string gate)
        {
            StoryBook book = NearDoor("npc.aubergiste", r =>
            {
                Quest(r, Bete)["requires"] = new JsonArray(new JsonObject { ["if"] = gate, ["id"] = Peaux });
                Quest(r, Peaux)["effects"]!.AsArray().Add(new JsonObject { ["do"] = "startQuest", ["id"] = Bete });
            });
            WorldSession w = InRoom("zone.auberge", book, WithQuest(Hero(0, new ItemStack("item.peau_de_loup", 3)), Peaux, "peaux"));
            Deliver(w, "npc.aubergiste");   // réponses du 31 octobre 2026 : la livraison est une réponse du dialogue
            return w;
        }
        WorldSession reward = Play("questReward");
        List<string> events = Steps(reward, 10);
        Assert.Contains($"QuestStarted {Bete}/temoin", events);
        Assert.Contains($"RewardToChoose {Peaux}", events);
        Assert.True(reward.Narrative!.IsChoosingReward(Peaux) && reward.Narrative.IsActive(Bete));
        Assert.Equal(0, reward.Inventory.Count("item.peau_de_loup"));

        WorldSession done = Play("questDone");
        Assert.Equal(new[] { $"ObjectiveDone {Peaux}/peaux", $"QuestLocked {Peaux}/peaux" }, Steps(done, 10));
        Assert.False(done.Narrative!.IsActive(Bete) || done.Narrative.IsChoosingReward(Peaux));
        Assert.Equal(3, done.Inventory.Count("item.peau_de_loup"));
    }

    [Fact]
    public void Quete_donnee_deux_fois_refusee_sans_exception()
    {
        // Revue Codex de la PR #76 : un identifiant de quête en double (avec des conditions d'ouverture) est un refus précis, jamais une
        // exception avant le rapport.
        JsonObject root = StoryJson();
        Quest(root, Bete)["requires"] = new JsonArray(new JsonObject { ["if"] = "questDone", ["id"] = Peaux });
        int n = root["data"]!["quests"]!.AsArray().Count;   // sous-lot V1.7 : l'histoire livrée a aussi les épreuves de la guilde
        root["data"]!["quests"]!.AsArray().Add(Quest(root, Peaux).DeepClone());
        root["data"]!["quests"]!.AsArray().Add(Quest(root, Bete).DeepClone());
        StoryLoadResult r = Read(root);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == $"$.data.quests[{n}].id" && e.Code == ValidationCode.DuplicateId);
        Assert.Contains(r.Report.Errors, e => e.Path == $"$.data.quests[{n + 1}].id" && e.Code == ValidationCode.DuplicateId);
        Assert.DoesNotContain(r.Report.Errors, e => e.Path.Contains("requires", StringComparison.Ordinal));
    }

    [Fact]
    public void Etape_finale_refusee_ne_prend_jamais_les_objets()
    {
        // La dernière étape prend aussi 50 cuivre : la bourse est vide, l'étape attend, et les peaux restent dans le sac (rien n'est pris
        // avant que tout soit vérifié).
        StoryBook book = NearDoor("npc.aubergiste", r =>
            Quest(r, Peaux)["steps"]![1]!["effects"]!.AsArray().Add(new JsonObject { ["do"] = "takeMoney", ["value"] = 50 }));
        WorldSession w = InRoom("zone.auberge", book, WithQuest(Hero(0, new ItemStack("item.peau_de_loup", 3)), Peaux, "peaux"));
        Deliver(w, "npc.aubergiste");   // réponses du 31 octobre 2026 : la livraison est une réponse du dialogue
        Assert.Equal(new[] { $"ObjectiveDone {Peaux}/peaux", $"MoneyShort {Peaux}/peaux" }, Steps(w, 10));
        Assert.Equal(3, w.Inventory.Count("item.peau_de_loup"));
        Assert.Equal("peaux", w.Narrative!.CurrentStep(Peaux)!.Id);
    }

    [Fact]
    public void Objets_promis_puis_vendus_livraison_a_refaire()
    {
        StoryBook book = NearDoor("npc.aubergiste");
        WorldSession w = InRoom("zone.auberge", book, WithQuest(Hero(0, new ItemStack("item.peau_de_loup", 3)), Peaux, "peaux"));
        Deliver(w, "npc.aubergiste");   // réponses du 31 octobre 2026 : la livraison est une réponse du dialogue
        Assert.Equal(1, w.Narrative!.Progress(Peaux));
        Assert.Equal(InventoryRefusal.None, w.Inventory.Remove("item.peau_de_loup", 1));   // une peau partie avant le tick
        Steps(w, 3);
        Assert.Equal(("peaux", 0L, 2), (w.Narrative.CurrentStep(Peaux)!.Id, w.Narrative.Progress(Peaux), w.Inventory.Count("item.peau_de_loup")));
    }

    [Fact]
    public void Potions_reservees_jamais_livrees()
    {
        // Une étape qui demande une potion de soin : la seule potion du sac est emportée au combat (réservée), elle n'est pas livrée.
        StoryBook book = NearDoor("npc.aubergiste", r => Quest(r, Peaux)["steps"]![1]!["objective"]!["id"] = "item.potion_de_soin");
        WorldSession w = InRoom("zone.auberge", book, WithQuest(Hero(0, new ItemStack("item.potion_de_soin", 3)), Peaux, "peaux"));
        Assert.Equal(3, w.Inventory.Reserved("item.potion_de_soin"));
        // Réponses du 31 octobre 2026 : la réponse de livraison n'est pas proposée (rien de libre), rien n'est promis.
        Assert.Equal(DialogueRefusal.NoSuchChoice, Deliver(w, "npc.aubergiste"));
        Assert.DoesNotContain(w.Narrative!.Open!.Options, o => o.Delivery != null);
        Assert.Empty(Steps(w, 3));
        Assert.Equal((3, 0L), (w.Inventory.Count("item.potion_de_soin"), w.Narrative.Progress(Peaux)));
    }

    // --- P2 : conditions d'ouverture sur soi-même ou en cycle --------------------------------------------------------------

    [Theory]
    [InlineData("questDone")]
    [InlineData("questActive")]
    [InlineData("questReward")]
    public void P2_quete_ouverte_sur_son_propre_etat_refusee(string kind)
    {
        JsonObject root = StoryJson();
        Quest(root, Bete)["requires"] = new JsonArray(new JsonObject { ["if"] = kind, ["id"] = Bete });
        StoryLoadResult r = Read(root);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.quests[2].requires[0].id" && e.Code == ValidationCode.InvalidValue);
    }

    [Fact]
    public void P2_cycle_d_ouverture_refuse_avec_ses_chemins()
    {
        JsonObject root = StoryJson();
        Quest(root, Bete)["requires"] = new JsonArray(new JsonObject { ["if"] = "levelAtLeast", ["value"] = 3 }, new JsonObject { ["if"] = "questDone", ["id"] = Peaux });
        Quest(root, Peaux)["requires"] = new JsonArray(new JsonObject { ["if"] = "questActive", ["id"] = Bete });
        StoryLoadResult r = Read(root);
        Assert.False(r.IsValid);
        Assert.Equal(new[] { "$.data.quests[1].requires[0]", "$.data.quests[2].requires[1]" }, r.Report.Errors.Select(e => e.Path).OrderBy(p => p, StringComparer.Ordinal));
        // Une chaîne sans cycle passe.
        JsonObject chain = StoryJson();
        Quest(chain, Bete)["requires"] = new JsonArray(new JsonObject { ["if"] = "questDone", ["id"] = Peaux });
        Quest(chain, Peaux)["requires"] = new JsonArray(new JsonObject { ["if"] = "questDone", ["id"] = "quest.traces_sous_le_moulin" });
        Assert.True(Read(chain).IsValid, Read(chain).Report.ToString());
    }

    // --- P3 : parler à un PNJ qui n'a rien à dire -------------------------------------------------------------------------

    [Fact]
    public void P3_objectif_rempli_sans_rien_a_dire()
    {
        StoryBook book = NearDoor("npc.habitant", r =>
            Dialogue(r, "dialogue.habitant")["entries"] = new JsonArray(new JsonObject
            {
                ["conditions"] = new JsonArray(new JsonObject { ["if"] = "questDone", ["id"] = Bete }), ["node"] = "temoin",
            }));
        WorldSession w = InRoom("zone.maison", book, WithQuest(Hero(), Bete, "temoin"));
        Assert.Equal(DialogueRefusal.NothingToSay, w.Talk("npc.habitant", out DialogueView? view));
        Assert.Null(view);
        Assert.Equal(1, w.Narrative!.LastTalkFilled);   // l'interface annonce l'objectif, pas « rien à dire »
        Assert.Equal(new[] { $"ObjectiveDone {Bete}/temoin", $"StepCompleted {Bete}/temoin" }, Steps(w, 3));
    }

    // --- Réponses de Damien du 31 octobre 2026 : la livraison est une réponse du dialogue ---------------------------------------

    [Fact]
    public void Livraison_par_une_reponse_seulement_quand_les_objets_sont_la()
    {
        StoryBook book = NearDoor("npc.aubergiste");
        WorldSession w = InRoom("zone.auberge", book, WithQuest(Hero(0, new ItemStack("item.peau_de_loup", 3), new ItemStack("item.potion_de_soin", 2)), Peaux, "peaux"));
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.aubergiste", out DialogueView? view));
        // Parler ne promet rien ; la réponse « Voici : … » est la dernière, après celles du nœud.
        Assert.Equal(0, w.Narrative!.Progress(Peaux));
        Assert.Empty(Steps(w, 2));
        DialogueOption give = view!.Options.Last();
        Assert.Equal((Peaux, "item.peau_de_loup", 3L), (give.Delivery, give.Item, give.Count));
        Assert.Equal(book.Dialogue("dialogue.aubergiste").Node("peaux").Choices.Count, give.Index);
        Assert.Equal(DialogueRefusal.None, w.Choose(give.Index, out DialogueView? next, out _));
        // Le dialogue reste ouvert, sans la réponse de livraison (les peaux sont promises).
        Assert.DoesNotContain(next!.Options, o => o.Delivery != null);
        Assert.Equal(DialogueRefusal.NoSuchChoice, w.Choose(give.Index, out _, out _));
        Assert.Equal(new[] { $"ObjectiveDone {Peaux}/peaux", $"StepCompleted {Peaux}/peaux", "ItemTaken item.peau_de_loup 3" },
            Steps(w, 3).Take(3));
        Assert.Equal(0, w.Inventory.Count("item.peau_de_loup"));
        Assert.Equal(2, w.Inventory.Count("item.potion_de_soin"));
        // Trop peu de peaux : pas de réponse de livraison.
        WorldSession few = InRoom("zone.auberge", book, WithQuest(Hero(0, new ItemStack("item.peau_de_loup", 2)), Peaux, "peaux"));
        few.Talk("npc.aubergiste", out DialogueView? none);
        Assert.DoesNotContain(none!.Options, o => o.Delivery != null);
    }

    [Fact]
    public void Un_PNJ_qui_n_a_rien_a_dire_ouvre_ses_seules_livraisons()
    {
        // L'aubergiste n'a plus que des entrées fermées : rien à dire, mais des peaux à recevoir.
        StoryBook book = NearDoor("npc.aubergiste", r =>
        {
            JsonObject d = r["data"]!["dialogues"]!.AsArray().Single(x => (string)x!["id"]! == "dialogue.aubergiste")!.AsObject();
            foreach (JsonNode? e in d["entries"]!.AsArray())
                e!["conditions"] = new JsonArray(new JsonObject { ["if"] = "fact", ["id"] = "fact.chef.vaincu" });
        });
        WorldSession w = InRoom("zone.auberge", book, WithQuest(Hero(0, new ItemStack("item.peau_de_loup", 3)), Peaux, "peaux"));
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.aubergiste", out DialogueView? view));
        Assert.Equal(("text.dialogue.livraison", 0), (view!.Text, view.Options.Single().Index));
        Assert.Equal(DialogueRefusal.None, w.Choose(0, out DialogueView? next, out _));
        Assert.Null(next);
        Assert.Null(w.Narrative!.Open);   // plus rien à livrer : le dialogue se ferme
        Assert.Equal(1, w.Narrative.Progress(Peaux));
        // Sans peaux : rien à dire.
        WorldSession empty = InRoom("zone.auberge", book, WithQuest(Hero(), Peaux, "peaux"));
        Assert.Equal(DialogueRefusal.NothingToSay, empty.Talk("npc.aubergiste", out _));
    }

    // --- P4 : dialogue pendant le choix de la récompense --------------------------------------------------------------------

    [Fact]
    public void P4_Mathurin_parle_de_la_recompense_qui_attend()
    {
        StoryBook book = NearDoor("npc.aubergiste");
        WorldSession w = InRoom("zone.auberge", book, WithQuest(Hero(0, new ItemStack("item.peau_de_loup", 3)), Peaux, "peaux"));
        w.Talk("npc.aubergiste", out DialogueView? first);
        Assert.Equal("text.aubergiste.peaux", first!.Text);
        // Réponses du 31 octobre 2026 : la réponse de livraison vient après celles du nœud.
        DialogueOption give = first.Options.Single(o => o.Delivery == Peaux);
        Assert.Equal(("text.dialogue.livrer", "item.peau_de_loup", 3L), (give.Text, give.Item, give.Count));
        Assert.Equal(DialogueRefusal.None, w.Choose(give.Index, out _, out _));
        Steps(w, 3);
        Assert.True(w.Narrative!.IsChoosingReward(Peaux));
        w.Talk("npc.aubergiste", out DialogueView? waiting);
        Assert.Equal("text.aubergiste.recompense", waiting!.Text);
        // Frère Anselme aussi, pour « La bête blanche ».
        WorldSession chapel = InRoom("zone.chapelle", NearDoor("npc.pretre"), WithQuest(Hero(), Bete, QuestProgress.Reward));
        chapel.Talk("npc.pretre", out DialogueView? priest);
        Assert.Equal("text.pretre.recompense", priest!.Text);
    }

    // --- P5 : expérience et quête du même niveau dans une réponse ---------------------------------------------------------

    [Fact]
    public void P5_experience_donnee_avant_de_juger_l_ouverture()
    {
        StoryBook book = NearDoor("npc.aubergiste", r =>
        {
            JsonArray eff = Dialogue(r, "dialogue.aubergiste")["nodes"]![0]!["choices"]![1]!["effects"]!.AsArray();
            eff.Add(new JsonObject { ["do"] = "giveXp", ["value"] = 5000 });
            eff.Add(new JsonObject { ["do"] = "startQuest", ["id"] = Bete });
        });
        WorldSession w = InRoom("zone.auberge", book, Hero());
        Assert.Equal(1, w.Level);
        w.Talk("npc.aubergiste", out DialogueView? view);
        Assert.Equal(new[] { "text.aubergiste.accueil.accepter", "text.aubergiste.accueil.plus_tard" }, view!.Options.Select(o => o.Text));
        Assert.Equal(DialogueRefusal.None, w.Choose(1, out _, out IReadOnlyList<StoryEvent> events));
        Assert.Contains($"QuestStarted {Bete}/temoin", events.Select(e => e.ToString()));
        Assert.True(w.Level >= 3);
    }

    // --- Chefs : places, récompense du chef des bandits, deux chefs au même tick, registre presque plein ----------------------

    /// <summary>
    /// Vallons retouchés : formes des chefs à <paramref name="health"/> PV, inoffensives ; créatures de la zone inoffensives ; chaque chef
    /// réduit à sa première forme si <paramref name="single"/> ; entrée en (<paramref name="x"/>, <paramref name="z"/>).
    /// </summary>
    private static ZoneLoadResult Load(double x, double z, int health = 1, bool single = true, Action<JsonObject>? bandits = null, Action<JsonObject>? zone = null)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(x, z);
        if (single)
        {
            root["data"]!["boss"]!["forms"] = new JsonArray("gardien_du_seuil");
            root["data"]!["chiefs"]![0]!["forms"] = new JsonArray("chef_des_bandits");
        }
        zone?.Invoke(root);
        string directory = Path.Combine(Path.GetTempPath(), "eds_v16r_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "zones"));
            void Calm(string name, Action<JsonObject>? more, bool chief)
            {
                JsonObject s = JsonNode.Parse(File.ReadAllText(ScenarioPath(name)))!.AsObject();
                foreach (JsonNode? c in s["data"]!["combatants"]!.AsArray())
                {
                    if (chief) c!["health"] = health;
                    foreach (JsonNode? a in c!["attacks"]!.AsArray()) a!["rawDamage"] = 1;
                }
                more?.Invoke(s);
                File.WriteAllText(Path.Combine(directory, name + ".json"), s.ToJsonString());
            }
            Calm("zones/vallons", null, false);
            Calm("zones/vallons_chef", null, true);
            Calm("zones/vallons_bandits", bandits, true);
            Calm("zones/vallons_croc_gris", null, true);   // sous-lot V1.9.6 : Croc-Gris, calmé comme les autres chefs
            return ZoneJsonReader.Read(root.ToJsonString(), directory, ShippedData, ItemsCatalog);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static WorldSession Play(ZoneDefinition zone, SavedCharacter hero, IEnumerable<string>? consumed = null)
    {
        // Sauvegarde de V1.8 (relecture de la PR #82) : PlayerCharacters.PlacesKey déjà au registre.
        var w = new WorldSession(new WorldSave("zone.vallons", 0, 0, (consumed ?? Array.Empty<string>()).Append(PlayerCharacters.PlacesKey)
                .Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToArray(), hero),
            zone, ShippedData, Progression, ItemsCatalog, Book(zone));
        w.Step();
        return w;
    }

    /// <summary>Frappe le chef des bandits jusqu'à sa chute (ou la fin du budget) ; rend les pas joués.</summary>
    private static List<SessionStep> BeatBandit(WorldSession w, int ticks = 600)
    {
        var steps = new List<SessionStep>();
        for (int i = 0; i < ticks; i++)
        {
            if (i % 20 == 1 && w.BossForms[1] is string form && w.Zone.Engine.View(form).IsPresent && !w.Zone.Engine.View(form).IsDead)
                try { w.Zone.Engine.Submit(new CombatCommand(w.Zone.Engine.CurrentTick * 50, "bastion", "skill.bastion.frappe_du_rempart", form)); }
                catch (ArgumentException) { }
            steps.Add(w.Step());
            if (steps[steps.Count - 1].Boss.Any(b => b.Kind == BossEventKind.Defeated && b.Boss == "chef_des_bandits")) break;
        }
        for (int i = 0; i < 3; i++) steps.Add(w.Step());
        return steps;
    }

    [Fact]
    public void Victoire_sur_le_chef_des_bandits_recompensee_une_seule_fois()
    {
        ZoneDefinition z = Load(129.5, 92).Zone!;
        WorldSession w = Play(z, Hero());
        List<SessionStep> steps = BeatBandit(w);
        Assert.Contains(steps.SelectMany(s => s.Boss), b => b.Kind == BossEventKind.Defeated && b.Boss == "chef_des_bandits");
        // Réponses de Damien du 31 octobre 2026 : sa récompense de première victoire est la rondache nommée du chef (effet de bouclier).
        Assert.Equal(new[] { ("item.rondache_du_chef_des_bandits", 1) }, steps.SelectMany(s => s.Rewards));
        Assert.Equal(1, w.Inventory.Count("item.rondache_du_chef_des_bandits"));
        Assert.True(w.Zone.IsConsumed("zone.vallons/boss/chef_des_bandits"));
        Assert.False(w.Zone.IsConsumed("zone.vallons/boss/gardien_du_seuil"));
        Assert.True(w.Narrative!.Knows("fact.chef_des_bandits.vaincu"));
        // Session suivante : le chef revient, sa victoire ne redonne rien.
        WorldSave saved = w.Save();
        var again = new WorldSession(saved, z, ShippedData, Progression, ItemsCatalog, Book(z));
        again.Step();
        List<SessionStep> second = BeatBandit(again);
        Assert.Contains(second.SelectMany(s => s.Boss), b => b.Kind == BossEventKind.Defeated && b.Boss == "chef_des_bandits");
        Assert.Empty(second.SelectMany(s => s.Rewards));
        Assert.Equal(1, again.Inventory.Count("item.rondache_du_chef_des_bandits"));
    }

    [Theory]
    [InlineData(1, false)]   // la clé et les morts possibles d'un tick ne tiennent pas : la récompense attend (annoncé une fois)
    [InlineData(2, true)]    // juste la place : donnée
    public void Victoire_avec_un_registre_presque_plein(int extra, bool given)
    {
        ZoneDefinition z = Load(129.5, 92).Zone!;
        string safe = z.SafePointKey(z.Entry.Id);
        int combatants = Play(z, Hero(), new[] { safe }).Zone.Engine.CombatantIds.Count;
        // Avant la mort du chef : combattants + extra places ; sa mort en prend une.
        // Une clé de moins pour PlayerCharacters.PlacesKey (ajoutée par Play).
        string[] keys = Enumerable.Range(0, SaveValidator.MaxConsumed - combatants - extra - 2).Select(i => $"probe/k{i:D6}").Append(safe).ToArray();
        WorldSession w = Play(z, Hero(), keys);
        List<SessionStep> steps = BeatBandit(w);
        for (int i = 0; i < 20; i++) steps.Add(w.Step());
        Assert.Contains(steps.SelectMany(s => s.Boss), b => b.Kind == BossEventKind.Defeated && b.Boss == "chef_des_bandits");
        Assert.Equal(given, w.Zone.IsConsumed("zone.vallons/boss/chef_des_bandits"));
        Assert.Equal(given ? 1 : 0, w.Inventory.Count("item.rondache_du_chef_des_bandits"));
        Assert.Equal(given, w.Narrative!.Knows("fact.chef_des_bandits.vaincu"));
        Assert.Equal(given ? 0 : 1, steps.SelectMany(s => s.Story).Count(e => e.Kind == StoryEventKind.RewardBlocked && e.Id == "zone.vallons/boss/chef_des_bandits"));
    }

    [Fact]
    public void Deux_chefs_vaincus_au_meme_tick()
    {
        // Le chef des bandits posé près du Gardien, une forme chacun, 1 PV : l'onde de rupture (niveau 10) les abat ensemble.
        ZoneLoadResult loaded = Load(51.5, 36, bandits: s =>
        {
            JsonObject chef = s["data"]!["combatants"]![0]!.AsObject();
            chef["position"] = new JsonArray(53, 39.5);
            s["data"]!["space"] = JsonNode.Parse("{\"bounds\": {\"min\": [40, 24], \"max\": [140, 100]}, \"obstacles\": []}");
        });
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        ZoneDefinition z = loaded.Zone!;
        WorldSession w = Play(z, PlayerCharacters.Create("bastion", "class.bastion", Progression).WithXp(Progression.XpToLevel[9]));   // niveau 10
        Assert.Contains("skill.bastion.onde_de_rupture", w.Zone.Engine.View("bastion").Character!.Build.ActiveSkills);
        var steps = new List<SessionStep>();
        for (int i = 0; i < 200 && steps.SelectMany(s => s.Boss).Count(b => b.Kind == BossEventKind.Defeated) < 2; i++)
        {
            if (i % 40 == 1)
                try { w.Zone.Engine.Submit(new CombatCommand(w.Zone.Engine.CurrentTick * 50, "bastion", "skill.bastion.onde_de_rupture")); }
                catch (ArgumentException) { }
            steps.Add(w.Step());
        }
        for (int i = 0; i < 3; i++) steps.Add(w.Step());
        SessionStep both = Assert.Single(steps, s => s.Boss.Count(b => b.Kind == BossEventKind.Defeated) == 2);
        Assert.Equal(new[] { "gardien_du_seuil", "chef_des_bandits" }, both.Boss.Where(b => b.Kind == BossEventKind.Defeated).Select(b => b.Boss));
        Assert.Equal(new[] { ("item.lame_du_seuil", 1), ("item.rondache_du_chef_des_bandits", 1) }, both.Rewards);
        Assert.True(w.Zone.IsConsumed(z.VictoryKey) && w.Zone.IsConsumed(z.VictoryKeyOf(z.Chiefs[0])));
        Assert.True(w.Narrative!.Knows("fact.chef.vaincu") && w.Narrative.Knows("fact.chef_des_bandits.vaincu"));
    }

    [Fact]
    public void Forme_d_un_chef_sur_la_premiere_forme_d_un_autre_refusee()
    {
        // La seconde forme du chef des bandits posée sur la place de départ du Gardien : refusée, à son chemin.
        ZoneLoadResult r = Load(-18, -12, single: false, bandits: s =>
        {
            s["data"]!["combatants"]![1]!["position"] = new JsonArray(54, 36);
            s["data"]!["space"] = JsonNode.Parse("{\"bounds\": {\"min\": [40, 24], \"max\": [140, 100]}, \"obstacles\": []}");
        });
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.chiefs[0].forms[1]" && e.Code == ValidationCode.InvalidValue);
        // À sa place livrée, tout passe.
        Assert.True(Load(-18, -12, single: false).IsValid);
    }
}
