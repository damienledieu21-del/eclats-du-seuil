using System.Text.Json.Nodes;
using EdS.Data.Crafting;
using EdS.Data.Guild;
using EdS.Data.Json;
using EdS.Data.Merchants;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.9.3, chapitre 3 « Les champs et le hameau » (docs/MONDE.md, docs/ECRITURE.md) : la quête principale du chapitre (le hameau
/// attaqué, la piste des bandits jusqu'au lac, l'ordre scellé de Havre d'Ambre), ses cinq quêtes secondaires et l'étal de Renart. Chaque
/// quête est jouée de bout en bout par un pilote scripté : dialogues, réponses, forge, livraisons, lieux atteints ; la partie est sauvegardée,
/// relue et rouverte à chaque déplacement (au même instant du monde). Contournements, dits là où ils ont lieu : les victoires (bandits,
/// sangliers) sont annoncées à l'histoire sans livrer le combat (les combats ont leurs tests) ; le minerai et les herbes sont dans le sac de
/// départ (la récolte a ses tests).
/// </summary>
[Trait("Category", "World")]
public class V193Chapitre3Tests
{
    private const string Mission = "quest.traces_sous_le_moulin", Bourg = "quest.bourg_d_aube", Hameau = "quest.champs_et_hameau",
        Sangliers = "quest.sangliers_des_champs", Lettre = "quest.lettre_au_fond_du_lac", Vergers = "quest.vergers_pilles",
        Potions = "quest.potions_du_hameau", Pont = "quest.pont_des_vallons", Ferrure = "quest.ferrure_de_rechange", Pain = "quest.pain_du_matin";

    // Où se tenir (à 1 à 2 m du PNJ, hors de tout obstacle) : dans les Vallons, puis dans la chapelle.
    private static readonly (double X, double Z) Josse = (84, -37.5), Aubine = (72, -37.5), Achille = (-66, -37.5), Garance = (58, -29.5),
        Mahaut = (-64, -19.5), Renart = (-47.5, -3.3), Odile = (-9, 2), Gaspard = (26, -13), Forge = (-19, -5), Lac = (-12.5, -62), Vergers0 = (54, -41.5);
    private static readonly (double X, double Z) Albe = (3.2, 3.2);

    /// <summary>Heure du monde : 22 h 27 le premier jour (la journée dure trois heures réelles et commence à 8 h).</summary>
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

    /// <summary>Intérieur livré (<paramref name="id"/>), point sûr d'arrivée déplacé en (<paramref name="at"/>).</summary>
    private static ZoneDefinition Room(string id, (double X, double Z) at)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, id)))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(at.X, at.Z);
        ZoneLoadResult loaded = ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        return loaded.Zone!;
    }

    /// <summary>
    /// Partie ouverte dans la zone <paramref name="zone"/> (les Vallons, ou un intérieur) au point <paramref name="at"/>, à l'instant
    /// <paramref name="tick"/>, avec l'histoire, la guilde, les métiers (Vallons) et le commerce livrés ; l'arrivée et trois ticks joués.
    /// </summary>
    private static WorldSession Open(string zone, (double X, double Z) at, SavedCharacter hero, long tick, IEnumerable<string>? consumed, out List<string> events)
    {
        bool outside = zone == "zone.vallons";
        ZoneDefinition definition = outside ? Zone(at.X, at.Z) : Room(zone, at);
        var session = new WorldSession(new WorldSave(zone, tick, 0, (consumed ?? Array.Empty<string>()).Append(PlayerCharacters.PlacesKey)
            .Distinct(StringComparer.Ordinal), hero), definition, ShippedData, Progression, ItemsCatalog, Story.Value, outside ? Crafting(definition) : null,
            storyZone: outside ? null : "zone.vallons", market: new Market(ShippedMerchants), guild: Guilde.Value);
        events = session.Step().Story.Select(e => e.ToString()).ToList();
        events.AddRange(Ticks(session));
        return session;
    }

    private static WorldSession Open((double X, double Z) at, SavedCharacter hero, long tick = 0) => Open("zone.vallons", at, hero, tick, null, out _);

    private static WorldSession OpenIn(string zone, (double X, double Z) at, SavedCharacter hero) => Open(zone, at, hero, 0, null, out _);

    /// <summary>Le pilote se rend ailleurs : la partie est sauvegardée, relue, et rouverte au nouveau point, au même instant du monde.</summary>
    private static WorldSession Go(WorldSession from, (double X, double Z) at, string zone = "zone.vallons") => Go(from, at, zone, out _);

    private static WorldSession Go(WorldSession from, (double X, double Z) at, string zone, out List<string> events)
    {
        WorldSave save = SaveJsonReader.Read(SaveJsonReader.Write(from.Save())).Save!;
        return Open(zone, at, save.Character!, save.WorldTick, save.Consumed, out events);
    }

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

    private static long XpForLevel(int level) => Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= level);

    private static SavedCharacter Hero(int level, SavedStory? story, long money = 0, params ItemStack[] items) =>
        V11TestData.Hero(money, items).WithStory(story ?? SavedStory.Empty).WithXp(XpForLevel(level));

    private static SavedStory Done(IEnumerable<string> facts, params string[] quests) =>
        new SavedStory(facts, quests.ToDictionary(q => q, _ => new QuestProgress(QuestProgress.Done)));

    private static SavedStory With(IEnumerable<string> facts, params (string Quest, string Step)[] quests) =>
        new SavedStory(facts, quests.ToDictionary(q => q.Quest, q => new QuestProgress(q.Step)));

    /// <summary>Le chapitre 2 fini (la mission continue après Gaspard).</summary>
    private static readonly string[] AfterChapter2 = { "fact.traces.recit", "fact.moulin.porte_ouverte", "fact.ch1.vers_le_bourg", "fact.ch2.albe.rencontree",
        "fact.ch2.vers_les_champs" };

    private static readonly string[] AfterChapter3 = AfterChapter2.Concat(new[] { "fact.ch3.hameau.raid", "fact.ch3.hameau.defendu", "fact.ch3.vers_la_tour" }).ToArray();

    // --- Données -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Chapitre_3_et_ses_quetes_livres()
    {
        StoryBook book = Story.Value;
        Assert.Equal(new[] { Hameau, Sangliers, Lettre, Vergers, Potions, Pont }, book.Quests.Skip(16).Select(q => q.Id));   // ajoutées à la fin
        Assert.Equal(QuestCategory.Main, book.Quest(Hameau).Category);
        Assert.Equal(new[] { "hameau", "defendre", "piste", "lac", "retour", "albe" }, book.Quest(Hameau).Steps.Select(s => s.Id));
        Assert.Empty(book.Quest(Hameau).Requires);   // commencée par la fin du chapitre 2 : jamais fermée
        // Niveaux 10 à 15 : les quêtes secondaires s'ouvrent du niveau 10 au niveau 12 ; leurs récompenses se portent au plus deux niveaux après.
        foreach ((string q, int level) in new[] { (Sangliers, 10), (Potions, 10), (Lettre, 11), (Vergers, 11), (Pont, 12) })
        {
            Assert.Equal(QuestCategory.Side, book.Quest(q).Category);
            Assert.Equal(level, Assert.Single(book.Quest(q).Requires).Value);
            Assert.NotEmpty(book.Quest(q).RewardChoices.Concat(q == Lettre ? new RewardChoice[1] : Array.Empty<RewardChoice>()));   // la lettre : un choix dans les faits
            foreach (RewardChoice c in book.Quest(q).RewardChoices)
                Assert.True(ItemsCatalog.Item(c.Item).RequiredLevel <= level + 2, $"{q} : {c.Item}");
        }
        // Sources : les bandits du hameau, du lac et des vergers (lieux de niveau 6), les sangliers des champs (niveau 1) ; toutes les
        // créatures montent au niveau du joueur moins deux : au moins 8 pour un joueur de niveau 10.
        ZoneDefinition zone = Vallons.Value;
        foreach ((string creature, int min) in new[] { ("bandit_epee3", 6), ("bandit_epee4", 6), ("bandit_arc3", 6), ("sanglier4", 1), ("sanglier5", 1) })
        {
            Population p = zone.PopulationOf(creature)!;
            Assert.Equal(min, zone.MinLevelAt(p.Template.Placement!.Position));
            Assert.True(zone.Scaling!.CreatureLevel(min, 10) >= 8, creature);
        }
        Assert.Equal(new[] { "hameau", "lac", "vergers" }, new[] { "bandit_epee3", "bandit_epee4", "bandit_arc3" }
            .Select(c => zone.Places.Last(pl => Inside(pl, zone.PopulationOf(c)!.Template.Placement!.Position)).Id));
        Assert.All(new[] { "bandit_epee3", "bandit_epee4", "bandit_arc3" }, c => Assert.Equal("bandit", zone.PopulationOf(c)!.Family));
        Assert.Equal(new[] { "npc.doyen", "npc.herboriste_hameau", "npc.fermier", "npc.fermiere", "npc.veuve" }, book.Npcs.Skip(18).Select(n => n.Id));
        // Les trois objets de quête ne se vendent pas.
        foreach (string item in new[] { "item.sac_d_eclats", "item.lettre_mouillee", "item.sac_de_pommes" })
        {
            Assert.Equal("quete", ItemsCatalog.Item(item).Icon);
            Assert.False(ShippedMerchants.Values.ContainsKey(item), item);
        }
        Assert.Contains(book.Discoveries, d => d.Id == "discovery.ordre_d_ambre");
    }

    private static bool Inside(Place p, GroundVector at) => p.Min.X <= at.X && at.X <= p.Max.X && p.Min.Z <= at.Z && at.Z <= p.Max.Z;

    [Fact]
    public void Nouveaux_PNJ_a_portee_depuis_une_place_libre()
    {
        foreach ((string npc, (double x, double z) at) in new[]
        {
            ("npc.doyen", Josse), ("npc.herboriste_hameau", Aubine), ("npc.fermier", Achille), ("npc.fermiere", Garance), ("npc.veuve", Mahaut),
        })
        {
            Npc n = Story.Value.Npc(npc);
            Assert.Null(n.Zone);
            double nx = n.Position.X.Raw / 10000.0, nz = n.Position.Z.Raw / 10000.0;
            Assert.True(Math.Sqrt((nx - at.x) * (nx - at.x) + (nz - at.z) * (nz - at.z)) < 3, npc);
            foreach ((double px, double pz) in new[] { at, (nx, nz) })
                Assert.All(Vallons.Value.Setup.Space!.Obstacles, o => Assert.False(
                    o.Points.Min(p => p.X.Raw) / 10000.0 - 0.5 < px && px < o.Points.Max(p => p.X.Raw) / 10000.0 + 0.5
                    && o.Points.Min(p => p.Z.Raw) / 10000.0 - 0.5 < pz && pz < o.Points.Max(p => p.Z.Raw) / 10000.0 + 0.5, $"{npc} : {o.Id}"));
            // Aucun bandit ne guette un donneur : chacun est hors de portée d'aggro (13 m au plus, l'archer) de tout PNJ.
            foreach (string bandit in new[] { "bandit_epee3", "bandit_epee4", "bandit_arc3" })
            {
                GroundVector b = Vallons.Value.PopulationOf(bandit)!.Template.Placement!.Position;
                double bx = b.X.Raw / 10000.0 - nx, bz = b.Z.Raw / 10000.0 - nz;
                Assert.True(Math.Sqrt(bx * bx + bz * bz) > 14, $"{npc} : {bandit}");
            }
        }
    }

    // --- Pilote : la quête principale du chapitre 3 ---------------------------------------------------------------------

    [Fact]
    public void Chapitre_3_joue_de_bout_en_bout()
    {
        // Fin du chapitre 2 (une sauvegarde d'avant V1.9.3 à l'étape de l'aveu) : l'aveu de Renart commence le chapitre 3.
        var aveu = With(new[] { "fact.traces.recit", "fact.moulin.porte_ouverte", "fact.ch1.vers_le_bourg", "fact.ch2.albe.rencontree", "fact.ch2.renart.piste" },
            (Bourg, "aveu"));
        WorldSession w = Open(Renart, Hero(10, aveu, 500));
        Say(w, "npc.renart", "ch2_aveu");
        Choose(w, 1, "ch2_aveu_fin");
        Choose(w, 0);
        List<string> start = Ticks(w);
        Assert.Contains($"QuestCompleted {Bourg}", start);
        Assert.Contains($"QuestStarted {Hameau}/hameau", start);
        long xp0 = w.Character.Xp, money0 = w.Money;
        // Maître Josse raconte l'attaque.
        w = Go(w, Josse);
        Say(w, "npc.doyen", "ch3_raid");
        Choose(w, 0, "ch3_raid_qui");
        Choose(w, 0);
        List<string> raid = Ticks(w);
        Assert.Contains($"StepCompleted {Hameau}/hameau", raid);
        Assert.Contains("FactLearned fact.ch3.hameau.raid", raid);
        Say(w, "npc.doyen", "ch3_defendre");
        // Le meneur, au nord du hameau : la victoire est annoncée à l'histoire comme le fait la partie à la mort de la créature, sans livrer
        // le combat lui-même (couvert par les tests des zones et du moteur) ; un bandit du camp ne compte pas.
        w.Narrative!.Defeated("bandit_epee1", "bandit");
        Assert.DoesNotContain($"StepCompleted {Hameau}/defendre", Ticks(w, 1));
        w.Narrative.Defeated("bandit_epee3", "bandit");
        List<string> defense = Ticks(w, 1);
        Assert.Contains($"StepCompleted {Hameau}/defendre", defense);
        Assert.Contains("FactLearned fact.ch3.hameau.defendu", defense);
        Say(w, "npc.doyen", "ch3_piste");
        // La piste jusqu'au lac (lieu atteint), puis le bandit qui garde les sacs.
        w = Go(w, Lac, "zone.vallons", out List<string> arrived);
        Assert.Contains($"StepCompleted {Hameau}/piste", arrived.Concat(Ticks(w)));
        w.Narrative!.Defeated("bandit_epee4", "bandit");
        List<string> lac = Ticks(w, 1);
        Assert.Contains($"StepCompleted {Hameau}/lac", lac);
        Assert.Contains("Discovered discovery.ordre_d_ambre", lac);
        Assert.Equal(1, w.Inventory.Count("item.sac_d_eclats"));
        // Le sac rendu à Josse.
        w = Go(w, Josse);
        Say(w, "npc.doyen", "ch3_sac");
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.doyen"));
        List<string> retour = Ticks(w);
        Assert.Contains("ItemTaken item.sac_d_eclats 1", retour);
        Assert.Contains("TrustChanged npc.doyen 1", retour);
        Assert.Contains($"StepCompleted {Hameau}/retour", retour);
        Say(w, "npc.doyen", "ch3_apres");
        // Sœur Albe lit l'ordre scellé et envoie à la tour de guet. Fin du chapitre 3.
        w = Go(w, Albe, "zone.chapelle");
        Say(w, "npc.albe", "ch3_ordre");
        Choose(w, 0, "ch3_tour");
        Choose(w, 0);
        List<string> end = Ticks(w);
        Assert.Contains($"QuestCompleted {Hameau}", end);
        Assert.True(w.Narrative!.Knows("fact.ch3.vers_la_tour"));
        // Récompenses (proposées) : 1 200 points d'expérience et 9 argent 50 cuivre ; du niveau 10 au niveau 12 sans les quêtes secondaires ni
        // les combats ; avec les cinq quêtes secondaires (1 560 points), niveau 14, et le niveau 15 avec les combats du chapitre.
        Assert.Equal((xp0 + 1200, money0 + 950), (w.Character.Xp, w.Money));
        Assert.Equal((12, 14), (Progression.LevelFor(XpForLevel(10) + 1200), Progression.LevelFor(XpForLevel(10) + 1200 + 1560)));
        // Ensuite, l'offre des herbes de Sœur Albe passe d'abord ; une fois les herbes finies, son mot d'après mène à la tour par une réponse.
        AssertLeadsTo(w, "npc.albe", "ch2_herbes", "text.ch3.albe.vers_tour", null);
        AssertLeadsTo(OpenIn("zone.chapelle", Albe, Hero(12, Done(AfterChapter3, Bourg, Hameau, "quest.herbes_pour_la_chapelle"))), "npc.albe", "ch2_apres",
            "text.ch3.albe.vers_tour", "ch3_apres");
        w = Go(w, Renart);
        Say(w, "npc.renart", "ch2_apres");
    }

    [Fact]
    public void Ancienne_partie_au_chapitre_2_fini_commence_le_chapitre_3_chez_Renart()
    {
        // Une sauvegarde d'avant V1.9.3, le chapitre 2 fini : Renart envoie au hameau, une seule fois (de jour comme de nuit, étal fermé).
        foreach (long tick in new long[] { 0, Nuit })
        {
            WorldSession w = Open("zone.vallons", Renart, Hero(9, Done(AfterChapter2, Bourg)), tick, null, out _);
            Say(w, "npc.renart", "ch3_ancienne");
            Assert.Contains($"QuestStarted {Hameau}/hameau", Choose(w, 0));
            Say(w, "npc.renart", "ch2_apres");
        }
        // Avant la fin du chapitre 2, rien du chapitre 3.
        Say(Open(Renart, Hero(9, With(AfterChapter2, (Bourg, "guilde")))), "npc.renart", "ch2_accueil");
    }

    // --- Pilotes : les cinq quêtes secondaires --------------------------------------------------------------------------

    [Fact]
    public void Les_sangliers_des_champs_de_bout_en_bout()
    {
        WorldSession w = Open(Achille, Hero(10, Done(AfterChapter2, Bourg)));
        Say(w, "npc.fermier", "ch3_sangliers");
        Assert.Contains($"QuestStarted {Sangliers}/grand", Choose(w, 0));
        Say(w, "npc.fermier", "ch3_sangliers_attente");
        // Victoires annoncées à l'histoire (contournement : les combats ont leurs tests) : les deux sangliers des champs, pas un autre.
        w.Narrative!.Defeated("sanglier", "sanglier");
        Assert.Empty(Ticks(w, 1));
        w.Narrative.Defeated("sanglier4", "sanglier");
        Assert.Contains($"StepCompleted {Sangliers}/grand", Ticks(w, 1));
        w.Narrative.Defeated("sanglier5", "sanglier");
        Assert.Contains($"StepCompleted {Sangliers}/laie", Ticks(w, 1));
        w = Go(w, Achille);
        long xp = w.Character.Xp, money = w.Money;
        Say(w, "npc.fermier", "ch3_sangliers_retour");
        Choose(w, 0);
        List<string> end = Ticks(w);
        Assert.Contains("TrustChanged npc.fermier 1", end);
        Assert.Contains($"RewardToChoose {Sangliers}", end);
        Assert.Equal((xp + 280, money + 180), (w.Character.Xp, w.Money));
        Say(w, "npc.fermier", "ch3_sangliers_recompense");
        Assert.Equal(DialogueRefusal.None, w.ChooseReward(Sangliers, 1, out IReadOnlyList<StoryEvent> chosen));
        Assert.Equal(new[] { "ItemGiven item.elixir_de_garde 2", $"QuestCompleted {Sangliers}" }, chosen.Select(e => e.ToString()));
        Say(w, "npc.fermier", "ch3_apres");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void La_lettre_au_fond_du_lac_de_bout_en_bout(bool rendre)
    {
        WorldSession w = Open(Mahaut, Hero(11, Done(AfterChapter2, Bourg)));
        Say(w, "npc.veuve", "ch3_lettre");
        Assert.Contains($"QuestStarted {Lettre}/lac", Choose(w, 0));
        Say(w, "npc.veuve", "ch3_lettre_attente");
        // Les roseaux du lac (lieu atteint) : la lettre.
        w = Go(w, Lac, "zone.vallons", out List<string> arrived);
        Assert.Contains($"StepCompleted {Lettre}/lac", arrived.Concat(Ticks(w)));
        Assert.Equal(1, w.Inventory.Count("item.lettre_mouillee"));
        w = Go(w, Mahaut);
        long xp = w.Character.Xp, money = w.Money;
        Say(w, "npc.veuve", "ch3_lettre_retour");
        List<string> choice = Choose(w, rendre ? 0 : 1, rendre ? "ch3_lettre_rendue" : "ch3_lettre_gardee").ToList();
        Choose(w, 0);
        Assert.Contains($"QuestCompleted {Lettre}", Ticks(w));
        // Le choix reste écrit dans les faits ; rendue, Mahaut paie et s'en souvient ; gardée, la lettre reste dans le sac.
        Assert.Equal((rendre, !rendre), (w.Narrative!.Knows("fact.ch3.lettre.rendue"), w.Narrative.Knows("fact.ch3.lettre.gardee")));
        Assert.Equal((xp + 320, money + (rendre ? 150 : 0), rendre ? 0 : 1, rendre ? 1 : 0),
            (w.Character.Xp, w.Money, w.Inventory.Count("item.lettre_mouillee"), w.Narrative.Trust("npc.veuve")));
        Assert.Equal(rendre, choice.Contains("ItemTaken item.lettre_mouillee 1"));
        w = Go(w, Mahaut);
        Say(w, "npc.veuve", rendre ? "ch3_apres_rendue" : "ch3_apres_gardee");
    }

    [Fact]
    public void Les_vergers_pilles_de_bout_en_bout()
    {
        WorldSession w = Open(Garance, Hero(11, Done(AfterChapter2, Bourg)));
        Say(w, "npc.fermiere", "ch3_vergers");
        Assert.Contains($"QuestStarted {Vergers}/pillard", Choose(w, 0));
        Say(w, "npc.fermiere", "ch3_vergers_attente");
        // Victoire annoncée à l'histoire (contournement : les combats ont leurs tests) : l'archer des vergers, pas celui du camp.
        w = Go(w, Vergers0);
        w.Narrative!.Defeated("bandit_arc1", "bandit");
        Assert.Empty(Ticks(w, 1));
        w.Narrative.Defeated("bandit_arc3", "bandit");
        Assert.Contains($"StepCompleted {Vergers}/pillard", Ticks(w, 1));
        Assert.Equal(1, w.Inventory.Count("item.sac_de_pommes"));
        w = Go(w, Garance);
        Say(w, "npc.fermiere", "ch3_vergers_sac");
        long xp = w.Character.Xp, money = w.Money;
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.fermiere"));
        List<string> end = Ticks(w);
        Assert.Contains("ItemTaken item.sac_de_pommes 1", end);
        Assert.Contains("TrustChanged npc.fermiere 1", end);
        Assert.Contains($"RewardToChoose {Vergers}", end);
        Assert.Equal((xp + 340, money + 200), (w.Character.Xp, w.Money));
        Say(w, "npc.fermiere", "ch3_vergers_recompense");
        Assert.Equal(DialogueRefusal.None, w.ChooseReward(Vergers, 0, out IReadOnlyList<StoryEvent> chosen));
        Assert.Equal(new[] { "ItemGiven item.brigandine 1", $"QuestCompleted {Vergers}" }, chosen.Select(e => e.ToString()));
        Say(w, "npc.fermiere", "ch3_apres");
    }

    [Fact]
    public void Les_potions_du_hameau_de_bout_en_bout()
    {
        // Six herbes dans le sac de départ (la récolte a ses tests) et l'herboristerie au niveau 2 (40 points de métier) ; les deux élixirs
        // de vigueur sont faits à l'atelier de la forge (fabrication réelle). Des élixirs, pas des potions de soin : celles-ci partent au
        // combat (réservées à l'entrée de zone) et ne se livrent pas.
        SavedCharacter hero = Hero(10, Done(AfterChapter2, Bourg), 0, new ItemStack("item.herbe_de_l_aube", 6))
            .WithProfessions(new Dictionary<string, long> { ["herboristerie"] = 40 });
        WorldSession w = Open(Aubine, hero);
        Say(w, "npc.herboriste_hameau", "ch3_potions");
        Assert.Contains($"QuestStarted {Potions}/potions", Choose(w, 0));
        Assert.Empty(Ticks(w));   // aucun élixir : rien à livrer
        Say(w, "npc.herboriste_hameau", "ch3_potions_attente");
        w = Go(w, Forge);
        for (int i = 0; i < 2; i++) Assert.Equal(CraftRefusal.None, w.Craft("recipe.elixir_de_vigueur", out _));
        Assert.Contains($"StepCompleted {Potions}/potions", Ticks(w));
        w = Go(w, Aubine);
        long xp = w.Character.Xp, money = w.Money;
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.herboriste_hameau"));
        List<string> end = Ticks(w);
        Assert.Contains("ItemTaken item.elixir_de_vigueur 2", end);
        Assert.Contains("TrustChanged npc.herboriste_hameau 1", end);
        Assert.Contains($"RewardToChoose {Potions}", end);
        Assert.Equal((xp + 260, money + 150), (w.Character.Xp, w.Money));
        Say(w, "npc.herboriste_hameau", "ch3_potions_recompense");
        Assert.Equal(DialogueRefusal.None, w.ChooseReward(Potions, 1, out IReadOnlyList<StoryEvent> chosen));
        Assert.Equal(new[] { "ItemGiven item.potion_de_soin 5", $"QuestCompleted {Potions}" }, chosen.Select(e => e.ToString()));
        Say(w, "npc.herboriste_hameau", "ch3_apres");
    }

    [Fact]
    public void Le_pont_des_Vallons_de_bout_en_bout()
    {
        // Six minerais dans le sac de départ (la récolte a ses tests) ; les deux ferrures sont forgées à l'enclume de Maëlle.
        var story = Done(AfterChapter2.Append("fact.odile.retour"), Mission, Bourg, Ferrure);
        WorldSession w = Open(Odile, Hero(12, story, 0, new ItemStack("item.minerai_de_fer", 6)));
        Say(w, "npc.odile", "ch3_pont");
        Assert.Contains($"QuestStarted {Pont}/gaspard", Choose(w, 0));
        Say(w, "npc.odile", "ch3_pont_attente");
        w = Go(w, Gaspard);
        Say(w, "npc.gaspard", "ch3_pont");
        Choose(w, 0);
        Assert.Contains($"StepCompleted {Pont}/gaspard", Ticks(w));
        Say(w, "npc.gaspard", "garde");
        w = Go(w, Forge);
        Assert.Equal(CraftRefusal.None, w.Craft("recipe.ferrure", out _));
        Assert.DoesNotContain(Ticks(w), e => e.StartsWith("StepCompleted", StringComparison.Ordinal));
        Assert.Equal(CraftRefusal.None, w.Craft("recipe.ferrure", out _));
        Assert.Contains($"StepCompleted {Pont}/ferrures", Ticks(w));
        w = Go(w, Odile);
        long xp = w.Character.Xp, money = w.Money;
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.odile"));
        List<string> end = Ticks(w);
        Assert.Contains("ItemTaken item.ferrure 2", end);
        Assert.Contains("TrustChanged npc.odile 1", end);
        Assert.Contains($"RewardToChoose {Pont}", end);
        Assert.Equal((xp + 360, money + 220), (w.Character.Xp, w.Money));
        Say(w, "npc.odile", "ch3_pont_recompense");
        Assert.Equal(DialogueRefusal.None, w.ChooseReward(Pont, 1, out IReadOnlyList<StoryEvent> chosen));
        Assert.Equal(new[] { "ItemGiven item.rondache_du_lac 1", $"QuestCompleted {Pont}" }, chosen.Select(e => e.ToString()));
        Say(w, "npc.odile", "apres");
    }

    // --- L'étal de Renart (petite suite du chapitre 2, réponse de Damien du 5 novembre 2026) ----------------------------

    [Fact]
    public void Renart_tient_un_etal_un_peu_plus_cher_que_les_autres()
    {
        Merchant renart = ShippedMerchants.Merchant("merchant.renart");
        Assert.Equal(("npc.renart", "text.merchant.renart.greeting"), (renart.Npc, renart.Greeting));
        Assert.True(Texts.Has(renart.Greeting));
        // Chaque article vendu ailleurs l'est moins cher ailleurs ; Renart rachète moins bien que tous les autres.
        foreach (MerchantOffer o in renart.Offers)
            foreach (Merchant other in ShippedMerchants.Merchants.Where(m => m.Id != renart.Id))
                foreach (MerchantOffer same in other.Offers.Where(x => x.Item == o.Item))
                    Assert.True(o.Price > same.Price, $"{o.Item} : {other.Id}");
        Assert.All(ShippedMerchants.Merchants.Where(m => m.Id != renart.Id), m => Assert.True(renart.SellPercent < m.SellPercent, m.Id));
        // De jour, on achète à son étal ; la nuit, l'étal est fermé, et Renart parle comme avant.
        WorldSession day = Open(Renart, Hero(10, Done(AfterChapter3, Bourg, Hameau), 1000));
        Assert.Equal(TradeRefusal.None, day.Buy("merchant.renart", "item.potion_de_soin"));
        Assert.Equal((920, 1), (day.Money, day.Inventory.Count("item.potion_de_soin")));
        WorldSession night = Open("zone.vallons", Renart, Hero(10, Done(AfterChapter3, Bourg, Hameau), 1000), Nuit, null, out _);
        Assert.Equal(TradeRefusal.Closed, night.Buy("merchant.renart", "item.potion_de_soin"));
        Assert.Equal(1000, night.Money);
        Say(night, "npc.renart", "ch2_apres");
    }

    // --- Leçon de la PR #88 : aucune quête finie (principale ou non) ne masque une offre du même PNJ -------------------

    [Fact]
    public void Offres_de_chaque_donneur_encore_accessibles()
    {
        string[] facts = AfterChapter3.Append("fact.odile.retour").ToArray();
        // Toutes les autres quêtes du donneur finies, la mission et les chapitres 2 et 3 compris : l'offre passe avant les mercis.
        Say(Open(Achille, Hero(12, Done(facts, Mission, Bourg, Hameau))), "npc.fermier", "ch3_sangliers");
        Say(Open(Aubine, Hero(12, Done(facts, Mission, Bourg, Hameau))), "npc.herboriste_hameau", "ch3_potions");
        Say(Open(Garance, Hero(12, Done(facts, Mission, Bourg, Hameau))), "npc.fermiere", "ch3_vergers");
        Say(Open(Mahaut, Hero(12, Done(facts, Mission, Bourg, Hameau))), "npc.veuve", "ch3_lettre");
        Say(Open(Odile, Hero(12, Done(facts, Mission, Bourg, Hameau, Ferrure, Pain))), "npc.odile", "ch3_pont");
        // Pendant le chapitre 3, les donneurs offrent aussi.
        Say(Open(Achille, Hero(12, With(facts, (Hameau, "defendre")))), "npc.fermier", "ch3_sangliers");
        // Chez Odile, l'offre de la ferrure (jamais acceptée) et son attente gardent une réponse vers le pont ; le pont fini ne masque pas la ferrure.
        AssertLeadsTo(Open(Odile, Hero(12, Done(facts, Mission))), "npc.odile", "ch1_ferrure", "text.ch3.odile.vers_pont", "ch3_pont");
        AssertLeadsTo(Open(Odile, Hero(12, With(facts, (Ferrure, "forger")))), "npc.odile", "ch1_ferrure_attente", "text.ch3.odile.vers_pont", "ch3_pont");
        Say(Open(Odile, Hero(12, Done(facts, Mission, Pont))), "npc.odile", "ch1_ferrure");
        // Le chapitre 3 fini ne masque ni les herbes de Sœur Albe ni l'ardoise de Mathurin.
        Say(OpenIn("zone.chapelle", Albe, Hero(12, Done(facts, Mission, Bourg, Hameau))), "npc.albe", "ch2_herbes");
        Say(OpenIn("zone.auberge", (3.75, 1.6), Hero(12, Done(facts, Mission, Bourg, Hameau))), "npc.aubergiste", "accueil");
        // Sous le niveau demandé, rien de nouveau n'est offert (et pas de réponse vers le pont).
        Say(Open(Achille, Hero(9, null)), "npc.fermier", "ch3_accueil");
        Say(Open(Aubine, Hero(9, null)), "npc.herboriste_hameau", "ch3_accueil");
        Say(Open(Garance, Hero(10, null)), "npc.fermiere", "ch3_accueil");
        Say(Open(Mahaut, Hero(10, null)), "npc.veuve", "ch3_accueil");
        Assert.DoesNotContain(Say(Open(Odile, Hero(11, Done(facts, Mission))), "npc.odile", "ch1_ferrure").Options, o => o.Text == "text.ch3.odile.vers_pont");
    }

    /// <summary>Le PNJ ouvre sur <paramref name="node"/>, dont la réponse <paramref name="text"/> mène à <paramref name="next"/>.</summary>
    private static void AssertLeadsTo(WorldSession w, string npc, string node, string text, string? next)
    {
        DialogueView view = Say(w, npc, node);
        if (next is null) Assert.DoesNotContain(view.Options, o => o.Text == text);
        else Choose(w, Assert.Single(view.Options, o => o.Text == text).Index, next);
    }
}
