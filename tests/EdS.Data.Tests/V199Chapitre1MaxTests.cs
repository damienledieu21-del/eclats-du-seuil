using System.Text.Json.Nodes;
using EdS.Data.Crafting;
using EdS.Data.Guild;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Zones;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.9.9, « chapitre 1 au max, le contenu » : Dame Brunehilde quand Bérenger t'envoie (fait posé par la paie du premier contrat,
/// 15 points de guilde), la poussière d'écho (butin propre aux échos de nuit, vendue ou mêlée à l'herbe de l'aube chez l'herboriste), le
/// village qui se souvient (Croc-Gris, la garde de nuit, le chat, le puits) et le second temps fort du souvenir (les Voix de la Rupture,
/// seconde forme de l'écho ; Ysolde a deux répliques de plus). Aucun format ne change : objets en schéma 7, histoire en schéma 7.
/// </summary>
[Trait("Category", "World")]
public class V199Chapitre1MaxTests
{
    private const string Mission = "quest.traces_sous_le_moulin", Chat = "quest.chat_de_rosalie", Puits = "quest.puits_du_moulin",
        Contrat = "quest.premier_contrat", Pain = "quest.pain_du_matin", Croc = "fact.croc_gris.vaincu", Recommande = "fact.ch1.contrat.berenger",
        Poussiere = "item.poussiere_d_echo";

    private static readonly (double X, double Z) Odile = (-9, 2), Boulanger = (-42, 2), Archer = (1, -1.5), Gaspard = (26, -13),
        Capitaine = (-2, -9.5), Herboriste = (-65.2, -6.4), Forge = (-19, -5), Maitresse = (0, 1.0), Ysolde = (-1, -4.6);

    private const long Nuit = 130_000;

    private static readonly Lazy<ZoneDefinition> Vallons = new(() => Zone());
    private static readonly Lazy<StoryBook> Story = new(() => Book(Vallons.Value));
    private static readonly Lazy<GuildBook> Guilde = new(() =>
    {
        GuildLoadResult r = GuildJsonReader.ReadFile(GuildJsonReader.PathIn(DataDirectory), Texts, Story.Value, Vallons.Value,
            new[] { Vallons.Value }.Concat(V16TestData.Interiors).ToList(), ItemsCatalog, ShippedMerchants);
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Book!;
    });

    private static CraftingBook Crafting(ZoneDefinition zone) => CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory), Texts, zone, ItemsCatalog).Book!;

    private static ZoneDefinition Room(string id, (double X, double Z) at)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, id)))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(at.X, at.Z);
        ZoneLoadResult loaded = ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        return loaded.Zone!;
    }

    private static WorldSession Open(string zone, (double X, double Z) at, SavedCharacter hero, long tick = 0)
    {
        bool outside = zone == "zone.vallons";
        ZoneDefinition definition = outside ? Zone(at.X, at.Z) : Room(zone, at);
        var session = new WorldSession(new WorldSave(zone, tick, 0, new[] { PlayerCharacters.PlacesKey }, hero), definition, ShippedData, Progression,
            ItemsCatalog, Story.Value, outside ? Crafting(definition) : null, storyZone: outside ? null : "zone.vallons", market: new Market(ShippedMerchants),
            guild: Guilde.Value);
        session.Step();
        Ticks(session);
        return session;
    }

    private static WorldSession Open((double X, double Z) at, SavedCharacter hero, long tick = 0) => Open("zone.vallons", at, hero, tick);

    private static List<string> Ticks(WorldSession w, int ticks = 3)
    {
        var events = new List<string>();
        for (int i = 0; i < ticks; i++) events.AddRange(w.Step().Story.Select(e => e.ToString()));
        return events;
    }

    private static DialogueView Say(WorldSession w, string npc, string node)
    {
        Assert.Equal(DialogueRefusal.None, w.Talk(npc, out DialogueView? view));
        Assert.Equal(node, view!.Node);
        return view;
    }

    private static IReadOnlyList<string> Choose(WorldSession w, int index, string? next = null)
    {
        Assert.Equal(DialogueRefusal.None, w.Choose(index, out DialogueView? view, out IReadOnlyList<StoryEvent> events));
        Assert.Equal(next, view?.Node);
        return events.Select(e => e.ToString()).ToList();
    }

    private static IReadOnlyList<string> Pick(WorldSession w, DialogueView view, string text, string? next = null) =>
        Choose(w, view.Options.Single(o => o.Text == text).Index, next);

    private static long XpForLevel(int level) => Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= level);

    private static SavedCharacter Hero(int level, SavedStory? story = null, params ItemStack[] items) =>
        V11TestData.Hero(0, items).WithStory(story ?? SavedStory.Empty).WithXp(XpForLevel(level));

    private static SavedStory Done(IEnumerable<string> facts, params string[] quests) =>
        new SavedStory(facts, quests.ToDictionary(q => q, _ => new QuestProgress(QuestProgress.Done)));

    private static SavedStory With(IEnumerable<string> facts, params (string Quest, string Step)[] quests) =>
        new SavedStory(facts, quests.ToDictionary(q => q.Quest, q => new QuestProgress(q.Step)));

    // --- Versions -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Contenus_montes_sans_changer_de_format()
    {
        ItemCatalog items = ItemJsonReader.ReadFile(Path.Combine(DataDirectory, "world", "items.json"), ShippedData).Catalog!;
        Assert.Equal((7, 7, "0.17.0-provisoire"), (items.SchemaVersion, ItemValidator.CurrentSchemaVersion, items.ContentVersion));
        Assert.Equal((13, "0.25.0-provisoire"), (Story.Value.SchemaVersion, Story.Value.ContentVersion));   // V2.6.1 : schéma 11, contenu 0.21.0 ; V2.2 : schéma 8 (weather), contenu 0.14.0 ; V2.3 : schéma 9, contenu 0.15.0
        Assert.Equal("0.11.0-provisoire", ShippedMerchants.ContentVersion);   // V2.9.2 : 0.9.0 (ours, cape du vieux Brunot) ; V2.7 : 0.8.0 (factions, marché noir) ;   // V2.6.1 : inchangé (le chapelet de l'Aube ne se vend pas, réponse de Damien du 7 novembre 2026)
        Assert.Equal("0.7.0-provisoire", Crafting(Vallons.Value).ContentVersion);
        Assert.Equal("0.3.0-provisoire", Guilde.Value.ContentVersion);   // V2.9.2 : points du vieux Brunot
        Assert.Equal((12, "0.17.0-provisoire"), (Vallons.Value.SchemaVersion, Vallons.Value.ContentVersion));   // V2.6.1 : propagation ; V2.6 : cicatrices ; V2.4 : animaux, brume
        Assert.Equal("0.25.0-provisoire", Texts.ContentVersion);   // V2.6.1 : suites du Seuil (0.21.0) ; V2.6 : le Seuil qui grandit (0.20.0) ; V2.3.1 : PNJ qui changent de lieu ; V1.9.11 : retrouvailles de tous les PNJ qui parlent ; V2.5 : événements du monde ; V1.9.10 : retrouvailles ; V2.4 : signes des bêtes (0.14.0 de V2.3 + 1) ; V2.3 : répliques du moment ; V2.2 : répliques du temps
        // Une partie d'avant (le butin du loup, sans poussière) se relit et se rejoue avec le nouveau catalogue.
        WorldSession old = Open(Gaspard, Hero(3, null, new ItemStack("item.peau_de_loup", 3)));
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(old.Save())).Save!;
        Assert.Equal(16, SaveValidator.CurrentSchemaVersion);   // V2.7 : 15 (réputation) ; V2.6 : 14 (cicatrices) ; V2.5 : 13 (registre des événements) ; sinon inchangée
        Assert.Equal(3, Open(Gaspard, saved.Character!).Inventory.Count("item.peau_de_loup"));
    }

    // --- 1. Dame Brunehilde, quand Bérenger t'envoie -------------------------------------------------------------------------------

    [Fact]
    public void La_paie_du_contrat_pose_la_recommandation()
    {
        WorldSession w = Open(Capitaine, Hero(5, With(Array.Empty<string>(), (Contrat, "retour"))));
        Say(w, "npc.capitaine", "ch1_contrat_retour");
        w.Narrative!.Close();
        List<string> paie = Ticks(w);
        Assert.Contains($"FactLearned {Recommande}", paie);
        Assert.Contains($"RewardToChoose {Contrat}", paie);
    }

    [Fact]
    public void Brunehilde_fait_semblant_puis_avoue_et_la_carte_compte_les_loups()
    {
        SavedStory envoye = Done(new[] { Recommande }, Contrat), seul = Done(Array.Empty<string>(), Contrat);
        // Sans la recommandation : l'accueil d'avant, sans réponse de plus.
        WorldSession sans = Open("zone.guilde", Maitresse, Hero(5, seul));
        Assert.DoesNotContain(Say(sans, "npc.maitresse_guilde", "accueil").Options, o => o.Text == "text.ch1.maitresse.berenger.dire");
        // Avec : « Bérenger ? Connais pas. » puis le registre.
        WorldSession w = Open("zone.guilde", Maitresse, Hero(5, envoye));
        Pick(w, Say(w, "npc.maitresse_guilde", "accueil"), "text.ch1.maitresse.berenger.dire", "ch1_berenger");
        Choose(w, 1);
        // La recommandation vaut 15 points de guilde à l'inscription (réputation, comme les exploits d'avant).
        Assert.Equal(sans.Narrative!.ReputationPoints() + 15, w.Narrative!.ReputationPoints());
        Assert.Equal(GuildRefusal.None, w.JoinGuild(out IReadOnlyList<StoryEvent> joined));
        Assert.Equal("GuildJoined g", joined[0].ToString());
        Assert.Contains(joined, e => e.Kind == StoryEventKind.GuildReputation && e.Value == w.Narrative.ReputationPoints());
        // Inscrite : la réplique d'après, une fois, puis l'aveu ; ensuite, le mot des membres.
        DialogueView membre = Say(w, "npc.maitresse_guilde", "ch1_berenger_membre");
        Assert.Contains("FactLearned fact.ch1.maitresse.berenger", Pick(w, membre, "text.ch1.maitresse.berenger_membre.semblant", "ch1_berenger_aveu"));
        Say(w, "npc.maitresse_guilde", "membre");
        // Sans recommandation, une inscrite n'a jamais cette réplique.
        Assert.Equal(GuildRefusal.None, sans.JoinGuild(out _));
        Say(sans, "npc.maitresse_guilde", "membre");
        // La réplique ne masque pas l'offre de la prime (niveau 15), placée avant elle.
        WorldSession prime = Open("zone.guilde", Maitresse, Hero(15, envoye));
        Assert.Equal(GuildRefusal.None, prime.JoinGuild(out _));
        Say(prime, "npc.maitresse_guilde", "ch4_prime");
    }

    // --- 2. La poussière d'écho ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Les_echos_de_nuit_laissent_leur_poussiere_et_elle_seule()
    {
        ItemDefinition dust = ItemsCatalog.Item(Poussiere);
        Assert.Equal((ItemType.Material, ItemRarity.Common, 20), (dust.Type, dust.Rarity, dust.Stack));
        Assert.True(ShippedMerchants.TryGetValue(Poussiere, out long value));
        Assert.Equal(30, value);
        LootTable echo = ItemsCatalog.Loot.Single(l => l.Id == "loot.echo");
        Assert.Equal(new[] { (Poussiere, 1, 80), (Poussiere, 1, 25) }, echo.Drops.Select(d => (d.Item, d.Count, d.ChancePercent)));
        // Les quatre échos (et eux seuls) ont ce butin ; les loups de la nuit gardent celui du loup.
        Population[] echoes = Vallons.Value.Populations.Where(p => p.Family == "echo").ToArray();
        // Sous-lot V2.4 : et les deux échos de la brume.
        Assert.Equal(new[] { "loup_echo1", "loup_echo2", "loup_echo3", "loup_echo4", "loup_echo5", "loup_echo6" }, echoes.Select(p => p.Creature));
        Assert.All(echoes, p => Assert.Equal("loot.echo", p.Loot));
        Assert.All(Vallons.Value.Populations.Where(p => p.Creature.StartsWith("loup_nuit", StringComparison.Ordinal)), p => Assert.Equal("loot.loup", p.Loot));
        Assert.DoesNotContain(ItemsCatalog.Loot.Where(l => l.Id != "loot.echo").SelectMany(l => l.Drops), d => d.Item == Poussiere);
        // Tirage déterminé par la mort : 0, 1 ou 2 poussières, la même chaque fois pour la même mort.
        var counts = new int[3];
        for (int i = 1; i <= 200; i++)
        {
            string key = $"zone.vallons/{i}/death/loup_echo1";
            IReadOnlyList<LootRoll> rolls = Loot.Roll(echo, key);
            Assert.All(rolls, r => Assert.Equal(Poussiere, r.Item));
            Assert.Equal(rolls.Select(r => r.Rank), Loot.Roll(echo, key).Select(r => r.Rank));
            counts[rolls.Count]++;
        }
        Assert.All(counts, c => Assert.True(c > 0, string.Join(", ", counts)));
    }

    [Fact]
    public void La_poussiere_se_mele_a_l_herbe_de_l_aube()
    {
        Recipe recipe = Crafting(Vallons.Value).Recipe("recipe.remede_d_echo");
        Assert.Equal(("herboristerie", 3, "forge"), (recipe.Profession, recipe.RequiredLevel, recipe.Place));
        Assert.Equal(new[] { ("item.herbe_de_l_aube", 3), (Poussiere, 3) }, recipe.Inputs);   // choix de Damien du 6 novembre 2026 : trois poussières
        Assert.Equal(("item.remede_de_l_aube", 1), recipe.Output);
        SavedCharacter herboriste = Hero(3, null, new ItemStack("item.herbe_de_l_aube", 3), new ItemStack(Poussiere, 4))
            .WithProfessions(new Dictionary<string, long> { ["herboristerie"] = 400 });
        WorldSession w = Open(Forge, herboriste);
        Assert.Equal(CraftRefusal.None, w.Craft("recipe.remede_d_echo", out CraftEvent? made));
        Assert.Equal("item.remede_de_l_aube", made!.Item);
        Assert.Equal((0, 1, 1), (w.Inventory.Count("item.herbe_de_l_aube"), w.Inventory.Count(Poussiere), w.Inventory.Count("item.remede_de_l_aube")));
        // Refus atomique : il manque une poussière, rien n'est pris.
        WorldSession manque = Open(Forge, Hero(3, null, new ItemStack("item.herbe_de_l_aube", 3), new ItemStack(Poussiere, 2))
            .WithProfessions(new Dictionary<string, long> { ["herboristerie"] = 400 }));
        Assert.Equal(CraftRefusal.NotEnough, manque.Craft("recipe.remede_d_echo", out _));
        Assert.Equal((3, 2), (manque.Inventory.Count("item.herbe_de_l_aube"), manque.Inventory.Count(Poussiere)));
    }

    [Fact]
    public void Sauge_et_Gaspard_parlent_de_la_poussiere_une_fois()
    {
        foreach ((string npc, (double, double) at, long tick, string node, string ask, string reply) in new[]
                 {
                     ("npc.herboriste", Herboriste, 0L, "accueil", "text.ch1.herboriste.poussiere.montrer", "ch1_poussiere"),
                     ("npc.herboriste", Herboriste, Nuit, "ch1_nuit", "text.ch1.herboriste.poussiere.montrer", "ch1_poussiere"),
                     ("npc.gaspard", Gaspard, 0L, "garde", "text.ch1.gaspard.poussiere.dire", "ch1_poussiere"),
                     ("npc.gaspard", Gaspard, Nuit, "ch1_nuit", "text.ch1.gaspard.poussiere.dire", "ch1_poussiere"),
                 })
        {
            Assert.DoesNotContain(Say(Open(at, Hero(1), tick), npc, node).Options, o => o.Text == ask);   // sans poussière, rien
            WorldSession w = Open(at, Hero(1, null, new ItemStack(Poussiere, 1)), tick);
            Assert.NotEmpty(Pick(w, Say(w, npc, node), ask, reply));
            Assert.DoesNotContain(Say(w, npc, node).Options, o => o.Text == ask);
        }
        // Chez Sauge, la suite : d'où vient-elle (rien n'est nommé).
        WorldSession s = Open(Herboriste, Hero(1, null, new ItemStack(Poussiere, 1)));
        Pick(s, Say(s, "npc.herboriste", "accueil"), "text.ch1.herboriste.poussiere.montrer", "ch1_poussiere");
        Choose(s, 0, "ch1_poussiere_source");
    }

    // --- 4. Le village se souvient ------------------------------------------------------------------------------------------------

    private static readonly SavedStory Firmin = Done(new[] { "fact.ch1.boulanger.merci", "fact.ch1.boulanger.puits" }, Pain, Puits);

    private static SavedStory Plus(SavedStory story, IEnumerable<string> facts, params string[] done) =>
        new SavedStory(story.Facts.Concat(facts).Distinct(StringComparer.Ordinal),
            story.Quests.Concat(done.Select(q => new KeyValuePair<string, QuestProgress>(q, new QuestProgress(QuestProgress.Done))))
                .GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Last().Value));

    public static TheoryData<string, string, string, string, string, string> Souvenirs => new()
    {
        // (sujet, PNJ, nœud du jour, réponse, nœud de la réplique, fait « déjà dit »)
        { "croc", "npc.gaspard", "garde", "text.ch1.gaspard.croc_mort.dire", "ch1_croc_mort", "fact.ch1.gaspard.croc" },
        { "croc", "npc.odile", "en_cours", "text.ch1.odile.croc_mort.dire", "ch1_croc_mort", "fact.ch1.odile.croc" },
        { "croc", "npc.archer_garde", "accueil", "text.ch1.archer_garde.croc_mort.dire", "ch1_croc_mort", "fact.ch1.archer_garde.croc" },
        { "croc", "npc.boulanger", "apres", "text.ch1.boulanger.croc_mort.dire", "ch1_croc_mort", "fact.ch1.boulanger.croc" },
        { "croc", "npc.herboriste", "accueil", "text.ch1.herboriste.croc_mort.dire", "ch1_croc_mort", "fact.ch1.herboriste.croc" },
        { "croc", "npc.maelle", "bonjour", "text.ch1.maelle.croc_mort.dire", "ch1_croc_mort", "fact.ch1.maelle.croc" },
        { "secret", "npc.gaspard", "garde", "text.ch1.gaspard.aldric.dire", "ch1_aldric_secret", "fact.ch1.gaspard.aldric" },
        { "moque", "npc.gaspard", "garde", "text.ch1.gaspard.aldric.dire", "ch1_aldric_moque", "fact.ch1.gaspard.aldric" },
        { "secret", "npc.boulanger", "apres", "text.ch1.boulanger.aldric.dire", "ch1_aldric_secret", "fact.ch1.boulanger.aldric" },
        { "moque", "npc.boulanger", "apres", "text.ch1.boulanger.aldric.dire", "ch1_aldric_moque", "fact.ch1.boulanger.aldric" },
        { "chat", "npc.boulanger", "apres", "text.ch1.boulanger.chat_rentre.dire", "ch1_chat_rentre", "fact.ch1.boulanger.chat" },
        { "chat", "npc.odile", "en_cours", "text.ch1.odile.chat.dire", "ch1_chat", "fact.ch1.odile.chat" },
        { "puits", "npc.odile", "en_cours", "text.ch1.odile.puits.dire", "ch1_puits", "fact.ch1.odile.puits" },
        { "puits", "npc.herboriste", "accueil", "text.ch1.herboriste.puits.dire", "ch1_puits", "fact.ch1.herboriste.puits" },
    };

    private static (double, double) Where(string npc) => npc switch
    {
        "npc.gaspard" => Gaspard, "npc.odile" => Odile, "npc.archer_garde" => Archer, "npc.boulanger" => Boulanger, "npc.herboriste" => Herboriste,
        _ => Forge,
    };

    /// <summary>L'histoire d'un joueur qui a fait <paramref name="subject"/> (ou rien : <c>null</c>), là où le PNJ dit son mot du jour.</summary>
    private static SavedStory StoryFor(string npc, string? subject)
    {
        SavedStory base0 = npc == "npc.boulanger" ? Firmin
            : npc == "npc.odile" ? With(new[] { "fact.traces.recit" }, (Mission, "gaspard"))
            : SavedStory.Empty;
        return subject switch
        {
            "croc" => Plus(base0, new[] { Croc }),
            "secret" => Plus(base0, new[] { "fact.ch1.aldric.secret" }, "quest.garde_de_nuit"),
            "moque" => Plus(base0, new[] { "fact.ch1.aldric.moque" }, "quest.garde_de_nuit"),
            "chat" => Plus(base0, Array.Empty<string>(), Chat),
            "puits" => Plus(base0, new[] { "fact.ch1.boulanger.puits" }, Puits),
            _ => base0,
        };
    }

    [Theory]
    [MemberData(nameof(Souvenirs))]
    public void Le_village_se_souvient_une_fois(string subject, string npc, string node, string ask, string reply, string said)
    {
        // Sans l'exploit, pas de réponse ; avec, la réponse mène à la réplique, une seule fois.
        Assert.DoesNotContain(Say(Open(Where(npc), Hero(1, StoryFor(npc, null))), npc, node).Options, o => o.Text == ask);
        WorldSession w = Open(Where(npc), Hero(1, StoryFor(npc, subject)));
        IReadOnlyList<string> events = Pick(w, Say(w, npc, node), ask, reply);
        Assert.Contains($"FactLearned {said}", events);
        if (npc == "npc.boulanger" && subject == "croc") Assert.Contains("TrustChanged npc.boulanger 1", events);   // la couronne de Firmin, goûtée sur place
        Assert.DoesNotContain(Say(w, npc, node).Options, o => o.Text == ask);
        // La réplique se relit après une sauvegarde : elle n'est plus proposée.
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!;
        Assert.DoesNotContain(Say(Open(Where(npc), saved.Character!), npc, node).Options, o => o.Text == ask);
    }

    /// <summary>Les répliques « le village se souvient » et celles de la poussière (réponses dites une fois).</summary>
    private static readonly string[] Memories =
    {
        "text.ch1.gaspard.croc_mort.dire", "text.ch1.gaspard.poussiere.dire", "text.ch1.gaspard.aldric.dire", "text.ch1.odile.croc_mort.dire",
        "text.ch1.odile.chat.dire", "text.ch1.odile.puits.dire", "text.ch1.archer_garde.croc_mort.dire", "text.ch1.boulanger.croc_mort.dire",
        "text.ch1.boulanger.chat_rentre.dire", "text.ch1.boulanger.aldric.dire", "text.ch1.herboriste.croc_mort.dire", "text.ch1.herboriste.puits.dire",
        "text.ch1.herboriste.poussiere.montrer", "text.ch1.maelle.croc_mort.dire",
    };

    private static string Signature(DialogueChoice c) =>
        c.Text + "|" + string.Join(",", c.Conditions.Select(x => $"{x.Kind}:{x.Id}:{x.Value}:{x.Step}")) + "|" + c.Next;

    [Fact]
    public void Chaque_replique_est_proposee_depuis_toutes_les_entrees_du_PNJ()
    {
        // Relecture de la PR #100 : une réplique ne doit pas être masquée par une entrée prioritaire (une offre, une étape en cours) :
        // chaque nœud où mène une entrée du PNJ la propose, avec les mêmes conditions (au plus huit réponses par nœud).
        int checkedNodes = 0;
        foreach (Dialogue d in Story.Value.Dialogues)
        {
            string[] memories = d.Nodes.SelectMany(n => n.Choices).Where(c => Memories.Contains(c.Text)).Select(Signature).Distinct().ToArray();
            if (memories.Length == 0) continue;
            foreach (string entry in d.Entries.Select(e => e.Node).Distinct())
            {
                DialogueNode node = d.Nodes.Single(n => n.Id == entry);
                string[] offered = node.Choices.Select(Signature).ToArray();
                Assert.All(memories, m => Assert.True(offered.Contains(m), $"{d.Id} / {entry} : il manque {m}"));
                Assert.True(node.Choices.Count <= 8, $"{d.Id} / {entry}");
                checkedNodes++;
            }
        }
        Assert.Equal(Memories.Length, Story.Value.Dialogues.SelectMany(d => d.Nodes).SelectMany(n => n.Choices).Select(c => c.Text).Intersect(Memories).Count());
        Assert.True(checkedNodes >= 60, checkedNodes.ToString());
        // Exemples joués : Sauge pendant les champignons, Gaspard à l'étape de Croc-Gris.
        WorldSession s = Open(Herboriste, Hero(4, With(Array.Empty<string>(), ("quest.champignons_de_sauge", "cueillir")), new ItemStack(Poussiere, 1)));
        Pick(s, Say(s, "npc.herboriste", "ch1_champignons_attente"), "text.ch1.herboriste.poussiere.montrer", "ch1_poussiere");
        WorldSession g = Open(Gaspard, Hero(3, With(new[] { "fact.traces.recit" }, (Mission, "croc_gris")), new ItemStack(Poussiere, 1)));
        Pick(g, Say(g, "npc.gaspard", "ch1_croc"), "text.ch1.gaspard.poussiere.dire", "ch1_poussiere");
    }

    [Fact]
    public void Le_village_se_souvient_aussi_la_nuit_et_apres_la_mission()
    {
        SavedStory croc = Done(new[] { Croc });
        foreach ((string npc, (double, double) at) in new[] { ("npc.gaspard", Gaspard), ("npc.archer_garde", Archer), ("npc.herboriste", Herboriste) })
            Assert.Contains(Say(Open(at, Hero(1, croc), Nuit), npc, "ch1_nuit").Options, o => o.Text.EndsWith(".croc_mort.dire", StringComparison.Ordinal));
        Assert.Contains(Say(Open((-22.6, -8), Hero(1, croc), Nuit), "npc.maelle", "ch1_nuit").Options, o => o.Text == "text.ch1.maelle.croc_mort.dire");
        Assert.Contains(Say(Open(Boulanger, Hero(1, Plus(Firmin, new[] { Croc })), Nuit), "npc.boulanger", "ch1_nuit").Options,
            o => o.Text == "text.ch1.boulanger.croc_mort.dire");
        // Gaspard, à l'étape où il écoute le souvenir (juste après Croc-Gris) ; Odile, la mission finie.
        WorldSession g = Open(Gaspard, Hero(3, With(new[] { "fact.traces.recit", Croc, "fact.ch1.silhouette" }, (Mission, "gaspard"))));
        Pick(g, Say(g, "npc.gaspard", "ch1_souvenir"), "text.ch1.gaspard.croc_mort.dire", "ch1_croc_mort");
        WorldSession o = Open(Odile, Hero(5, Done(new[] { "fact.traces.recit", Croc }, Mission, Puits)));
        DialogueView apres = Say(o, "npc.odile", "ch5_ch1_apres");
        Pick(o, apres, "text.ch1.odile.puits.dire", "ch1_puits");
        // Aldric, après sa garde : moqué ou aidé, il apprend aussi Croc-Gris.
        WorldSession a = Open(Archer, Hero(3, Plus(Done(new[] { "fact.ch1.aldric.moque", Croc }), Array.Empty<string>(), "quest.garde_de_nuit")));
        Pick(a, Say(a, "npc.archer_garde", "ch1_garde_moque"), "text.ch1.archer_garde.croc_mort.dire", "ch1_croc_mort");
    }

    // --- 5. Le souvenir : les Voix de la Rupture -----------------------------------------------------------------------------------

    [Fact]
    public void L_echo_a_une_seconde_forme_et_Ysolde_deux_repliques_de_plus()
    {
        ZoneDefinition room = Room("zone.souvenir_rupture", Ysolde);
        Boss echo = Assert.Single(room.Chiefs);
        Assert.Equal(new[] { "echo_de_la_rupture", "voix_de_la_rupture" }, echo.Forms.Select(f => f.Id));
        Assert.Equal(("fact.ch1.souvenir.vecu", (long?)110), (echo.Fact, echo.Forms[1].Health));
        Assert.Equal("Voix de la Rupture", DisplayNames.Of("voix_de_la_rupture"));
        // Pendant le souvenir, la porte ouverte : « Ce que nous avons appelé. »
        WorldSession w = Open("zone.souvenir_rupture", Ysolde, Hero(3, With(new[] { "fact.ch1.souvenir.a_revivre", "fact.ch1.souvenir.porte" }, (Mission, "croc_gris"))));
        Pick(w, Say(w, "npc.ysolde", "porte"), "text.ch1.ysolde.porte.question", "porte_voix");
        Choose(w, 0);
        // Après : le linceul vide, et un mot sur les lèvres.
        WorldSession apres = Open("zone.souvenir_rupture", Ysolde, Hero(3, With(new[] { "fact.ch1.souvenir.porte", "fact.ch1.souvenir.vecu" }, (Mission, "croc_gris"))));
        Pick(apres, Say(apres, "npc.ysolde", "apres"), "text.ch1.ysolde.apres.fils", "apres_fils");
    }

    [Fact]
    public void Chaque_chef_a_son_message_de_seconde_forme()
    {
        string dir = Path.Combine(Path.GetTempPath(), "eds_v199_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            AdventureSession a = Adventure(Path.Combine(dir, "partie.json"));
            // Les chefs à plusieurs formes des données livrées ont chacun leur message (le Gardien garde le sien, celui d'avant).
            foreach (string boss in new[] { "gardien_du_seuil", "echo_de_la_rupture", "croc_gris", "chef_des_bandits" })
            {
                Assert.Equal("text.ui.boss_phase." + boss, a.BossPhaseText(boss));
                Assert.True(a.Texts.Has(a.BossPhaseText(boss)));
            }
            Assert.Equal("Le Gardien du Seuil se relève, éveillé !", a.Text(a.BossPhaseText("gardien_du_seuil")));
            // Repli pour un chef sans message à lui : le message commun, neutre, qui nomme le chef.
            Assert.Equal("text.ui.boss_phase", a.BossPhaseText("loup_blanc"));
            Assert.Equal("Loup blanc se relève, plus redoutable encore !",
                string.Format(System.Globalization.CultureInfo.InvariantCulture, a.Text("text.ui.boss_phase"), DisplayNames.Of("loup_blanc")));
            Assert.Contains("Voix de la Rupture", a.Text(a.BossPhaseText("echo_de_la_rupture")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
