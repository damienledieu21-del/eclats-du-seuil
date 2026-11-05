using System.Text.Json.Nodes;
using EdS.Data.Guild;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.9.4, chapitre 4 « La tour et le camp » (docs/MONDE.md, docs/ECRITURE.md) : la quête principale du chapitre (la tour de guet
/// muette, Thibaut, le chef des bandits, les Rassembleurs et le Sans-Repos, le premier choix : épargner le chef ou non) et ses quatre quêtes
/// secondaires. Chaque quête est jouée de bout en bout par un pilote scripté : dialogues, réponses, lieux atteints, livraisons ; la partie est
/// sauvegardée, relue et rouverte à chaque déplacement (au même instant du monde). Contournements, dits là où ils ont lieu : les victoires sur
/// les créatures (archers, bandits, le Borgne) sont annoncées à l'histoire sans livrer le combat, et la victoire sur le chef des bandits par son
/// fait (<c>fact.chef_des_bandits.vaincu</c>), comme le fait la partie à la fin du combat de chef ; les combats ont leurs tests.
/// </summary>
[Trait("Category", "World")]
public class V194Chapitre4Tests
{
    private const string Bourg = "quest.bourg_d_aube", Hameau = "quest.champs_et_hameau", Main = "quest.tour_et_camp", Tour = "quest.tour_silencieuse",
        Deserteur = "quest.deserteur", Prime = "quest.prime_du_borgne", Carnet = "quest.marchand_qui_en_savait_trop", Herbes = "quest.herbes_pour_la_chapelle",
        Lanterne = "quest.derniere_lanterne", Potions = "quest.potions_du_hameau";

    // Où se tenir (à 1 à 2 m du PNJ, hors de tout obstacle) : dans les Vallons, puis dans les intérieurs.
    private static readonly (double X, double Z) Thibaut = (118, 54.5), Capitaine = (-2, -9.5), Colin = (68, -31.5), Josse = (84, -37.5), Aubine = (72, -37.5),
        Renart = (-47.5, -3.3), Epiciere = (-50.8, 4.4), Tour0 = (15, 77), Camp0 = (90, 84);
    private static readonly (double X, double Z) Albe = (3.2, 3.2), Rosalie = (-1.8, -2.4), Maitresse = (0, 1.0);

    private static readonly Lazy<ZoneDefinition> Vallons = new(() => Zone());
    private static readonly Lazy<StoryBook> Story = new(() => Book(Vallons.Value));
    private static readonly Lazy<GuildBook> Guilde = new(() =>
    {
        GuildLoadResult r = GuildJsonReader.ReadFile(GuildJsonReader.PathIn(DataDirectory), Texts, Story.Value, Vallons.Value,
            new[] { Vallons.Value }.Concat(V16TestData.Interiors).ToList(), ItemsCatalog, ShippedMerchants);
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Book!;
    });

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
    /// <paramref name="tick"/>, avec l'histoire, la guilde et le commerce livrés ; l'arrivée et trois ticks joués.
    /// </summary>
    private static WorldSession Open(string zone, (double X, double Z) at, SavedCharacter hero, long tick, IEnumerable<string>? consumed, out List<string> events)
    {
        bool outside = zone == "zone.vallons";
        ZoneDefinition definition = outside ? Zone(at.X, at.Z) : Room(zone, at);
        var session = new WorldSession(new WorldSave(zone, tick, 0, (consumed ?? Array.Empty<string>()).Append(PlayerCharacters.PlacesKey)
            .Distinct(StringComparer.Ordinal), hero), definition, ShippedData, Progression, ItemsCatalog, Story.Value, null,
            storyZone: outside ? null : "zone.vallons", market: new Market(ShippedMerchants), guild: Guilde.Value);
        events = session.Step().Story.Select(e => e.ToString()).ToList();
        events.AddRange(Ticks(session));
        return session;
    }

    private static WorldSession Open((double X, double Z) at, SavedCharacter hero) => Open("zone.vallons", at, hero, 0, null, out _);

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

    /// <summary>Le PNJ ouvre sur <paramref name="node"/> ; la réponse <paramref name="text"/> mène à <paramref name="next"/> (absente si null).</summary>
    private static IReadOnlyList<string> LeadsTo(WorldSession w, string npc, string node, string text, string? next)
    {
        DialogueView view = Say(w, npc, node);
        if (next is null) { Assert.DoesNotContain(view.Options, o => o.Text == text); return Array.Empty<string>(); }
        return Choose(w, Assert.Single(view.Options, o => o.Text == text).Index, next);
    }

    private static long XpForLevel(int level) => Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= level);

    private static SavedCharacter Hero(int level, SavedStory? story, long money = 0, params ItemStack[] items) =>
        V11TestData.Hero(money, items).WithStory(story ?? SavedStory.Empty).WithXp(XpForLevel(level));

    private static SavedStory Done(IEnumerable<string> facts, params string[] quests) =>
        new SavedStory(facts, quests.ToDictionary(q => q, _ => new QuestProgress(QuestProgress.Done)));

    private static SavedStory With(IEnumerable<string> facts, params (string Quest, string Step)[] quests) =>
        new SavedStory(facts, quests.ToDictionary(q => q.Quest, q => new QuestProgress(q.Step)));

    /// <summary>Inscrit à la guilde (rang G), les quêtes <paramref name="quests"/> finies.</summary>
    private static SavedStory Member(IEnumerable<string> facts, params string[] quests) =>
        new SavedStory(facts, quests.ToDictionary(q => q, _ => new QuestProgress(QuestProgress.Done)), guild: new SavedGuildCard("g", titles: new[] { "title.guilde.recrue" }));

    /// <summary>Le chapitre 3 fini.</summary>
    private static readonly string[] AfterChapter3 = { "fact.traces.recit", "fact.moulin.porte_ouverte", "fact.ch1.vers_le_bourg", "fact.ch2.albe.rencontree",
        "fact.ch2.vers_les_champs", "fact.ch3.hameau.raid", "fact.ch3.hameau.defendu", "fact.ch3.vers_la_tour" };

    // --- Données -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Chapitre_4_et_ses_quetes_livres()
    {
        StoryBook book = Story.Value;
        Assert.Equal(new[] { Main, Tour, Deserteur, Prime, Carnet }, book.Quests.Skip(22).Select(q => q.Id));   // ajoutées à la fin
        Assert.Equal(QuestCategory.Main, book.Quest(Main).Category);
        Assert.Equal(new[] { "rosalie", "tour", "camp", "chef", "choix", "lettre", "reponse", "albe" }, book.Quest(Main).Steps.Select(s => s.Id));
        Assert.Empty(book.Quest(Main).Requires);   // commencée par la fin du chapitre 3 : jamais fermée
        // Niveaux 15 à 20 : les quêtes secondaires s'ouvrent aux niveaux 15 et 16 ; leurs récompenses se portent au plus deux niveaux après.
        foreach ((string q, int level) in new[] { (Tour, 16), (Deserteur, 15), (Prime, 15), (Carnet, 16) })
        {
            Assert.Equal(QuestCategory.Side, book.Quest(q).Category);
            Assert.Equal(level, Assert.Single(book.Quest(q).Requires).Value);
            foreach (RewardChoice c in book.Quest(q).RewardChoices)
                Assert.True(ItemsCatalog.Item(c.Item).RequiredLevel <= level + 2, $"{q} : {c.Item}");
        }
        // Récompenses au choix : des pièces de la garde de la tour (déjà au catalogue, vendues par les marchands).
        Assert.Equal(new[] { "item.plastron_de_la_tour", "item.lame_de_la_tour" }, book.Quest(Tour).RewardChoices.Select(c => c.Item));
        Assert.Equal(new[] { "item.gantelets_de_la_tour", "item.brassards_de_garde" }, book.Quest(Prime).RewardChoices.Select(c => c.Item));
        // Créatures : les archers de la tour (lieu de niveau 11), un bandit du camp (niveau 16), et une seule nouvelle, le Grand Borgne ; toutes
        // montent au niveau du joueur moins deux.
        ZoneDefinition zone = Vallons.Value;
        foreach ((string creature, string place) in new[] { ("archer2", "tour_de_guet"), ("archer3", "tour_de_guet"), ("bandit_epee2", "camp_des_rodeurs"), ("grand_borgne", "camp_des_rodeurs") })
            Assert.Equal(place, zone.Places.Last(pl => Inside(pl, zone.PopulationOf(creature)!.Template.Placement!.Position)).Id);
        Assert.Contains(zone.Bosses, b => b.Id == "chef_des_bandits" && b.Fact == "fact.chef_des_bandits.vaincu");
        // Réponse de Damien du 5 novembre 2026 : le contrat « Prime : le Borgne » (rang F) garde le Borgne cadet, de niveau 6 ; la quête vise son
        // aîné, le Grand Borgne, une créature à part de niveau 17 au nord du camp : ils ne se marchent pas dessus.
        GuildContract contract = Guilde.Value.Contract("contract.prime_du_borgne");
        Objective borgne = book.Quest(Prime).Steps.Single(s => s.Id == "borgne").Objective;
        Assert.Equal((ObjectiveKind.Defeat, "le_borgne"), (contract.Objective.Kind, contract.Objective.Id));
        Assert.Equal((ObjectiveKind.Defeat, "grand_borgne", 1L), (borgne.Kind, borgne.Id, borgne.Value));
        Assert.Equal(((int?)6, (int?)17), (zone.PopulationOf("le_borgne")!.Level, zone.PopulationOf("grand_borgne")!.Level));
        Assert.Equal("bandit", zone.PopulationOf("grand_borgne")!.Family);
        Assert.Equal(17, zone.OwnMinLevel("grand_borgne", zone.PopulationOf("grand_borgne")!.Template.Placement!.Position));
        Assert.True(zone.Scaling!.CreatureLevel(17, 15) == 17 && zone.Scaling.CreatureLevel(6, 15) == 13);
        // Placé dans le camp, au nord, hors obstacle (1 m de marge), à plus de 10 m de toute route, dans l'aire praticable (2 m des bords).
        GroundVector gb = zone.PopulationOf("grand_borgne")!.Template.Placement!.Position;
        double gx = gb.X.Raw / 10000.0, gz = gb.Z.Raw / 10000.0;
        Assert.Equal("camp_des_rodeurs", zone.Places.Last(pl => Inside(pl, gb)).Id);
        Assert.All(zone.Setup.Space!.Obstacles, o => Assert.False(o.Points.Min(p => p.X.Raw) / 10000.0 - 1 < gx && gx < o.Points.Max(p => p.X.Raw) / 10000.0 + 1
            && o.Points.Min(p => p.Z.Raw) / 10000.0 - 1 < gz && gz < o.Points.Max(p => p.Z.Raw) / 10000.0 + 1, o.Id));
        foreach (Road r in zone.Roads)
            for (int i = 0; i + 1 < r.Points.Count; i++)
                Assert.True(Segment(gx, gz, r.Points[i], r.Points[i + 1]) - r.Width.Raw / 20000.0 > 10, r.Id);
        GroundVector lo = zone.Setup.Space.Min, hi = zone.Setup.Space.Max;
        Assert.True(lo.X.Raw / 10000.0 + 2 <= gx && gx <= hi.X.Raw / 10000.0 - 2 && lo.Z.Raw / 10000.0 + 2 <= gz && gz <= hi.Z.Raw / 10000.0 - 2);
        Assert.Equal(new[] { "npc.thibaut", "npc.capitaine", "npc.deserteur" }, book.Npcs.Skip(23).Select(n => n.Id));
        // Les six objets de quête ne se vendent pas.
        foreach (string item in new[] { "item.lettre_de_rosalie", "item.lettre_de_thibaut", "item.registre_de_garde", "item.cor_de_la_tour", "item.oeil_de_verre", "item.carnet_de_renart" })
        {
            Assert.Equal("quete", ItemsCatalog.Item(item).Icon);
            Assert.False(ShippedMerchants.Values.ContainsKey(item), item);
        }
        foreach (string d in new[] { "discovery.tour_vide", "discovery.mots_du_chef", "discovery.registre_de_garde", "discovery.carnet_de_renart" })
            Assert.Contains(book.Discoveries, x => x.Id == d);
        // Le dialogue de Sœur Albe garde ses seize entrées (le maximum) : le chapitre 4 passe par des réponses de ses nœuds existants.
        Assert.Equal(StoryValidator.MaxEntries, book.Dialogue("dialogue.albe").Entries.Count);
    }

    /// <summary>Distance du point (<paramref name="x"/>, <paramref name="z"/>) au segment [<paramref name="a"/>, <paramref name="b"/>], en mètres.</summary>
    private static double Segment(double x, double z, GroundVector a, GroundVector b)
    {
        double ax = a.X.Raw / 10000.0, az = a.Z.Raw / 10000.0, dx = b.X.Raw / 10000.0 - ax, dz = b.Z.Raw / 10000.0 - az;
        double t = Math.Clamp(((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz), 0, 1);
        return Math.Sqrt((x - ax - t * dx) * (x - ax - t * dx) + (z - az - t * dz) * (z - az - t * dz));
    }

    private static bool Inside(Place p, GroundVector at) => p.Min.X <= at.X && at.X <= p.Max.X && p.Min.Z <= at.Z && at.Z <= p.Max.Z;

    [Fact]
    public void Nouveaux_PNJ_a_portee_depuis_une_place_libre()
    {
        foreach ((string npc, (double x, double z) at) in new[] { ("npc.thibaut", Thibaut), ("npc.capitaine", Capitaine), ("npc.deserteur", Colin) })
        {
            Npc n = Story.Value.Npc(npc);
            Assert.Null(n.Zone);
            double nx = n.Position.X.Raw / 10000.0, nz = n.Position.Z.Raw / 10000.0;
            Assert.True(Math.Sqrt((nx - at.x) * (nx - at.x) + (nz - at.z) * (nz - at.z)) < 3, npc);
            foreach ((double px, double pz) in new[] { at, (nx, nz) })
                Assert.All(Vallons.Value.Setup.Space!.Obstacles, o => Assert.False(
                    o.Points.Min(p => p.X.Raw) / 10000.0 - 0.5 < px && px < o.Points.Max(p => p.X.Raw) / 10000.0 + 0.5
                    && o.Points.Min(p => p.Z.Raw) / 10000.0 - 0.5 < pz && pz < o.Points.Max(p => p.Z.Raw) / 10000.0 + 0.5, $"{npc} : {o.Id}"));
            // Aucune créature ne guette un PNJ : chacune est hors de portée d'aggro (13 m au plus, l'archer) ; Thibaut est au sud du camp.
            foreach (Population p in Vallons.Value.Populations)
            {
                GroundVector c = p.Template.Placement!.Position;
                double cx = c.X.Raw / 10000.0 - nx, cz = c.Z.Raw / 10000.0 - nz;
                Assert.True(Math.Sqrt(cx * cx + cz * cz) > 14, $"{npc} : {p.Creature}");
            }
        }
        // Les points où le pilote se tient à la tour et au camp sont libres eux aussi.
        foreach ((double px, double pz) in new[] { Tour0, Camp0 })
            Assert.All(Vallons.Value.Setup.Space!.Obstacles, o => Assert.False(
                o.Points.Min(p => p.X.Raw) / 10000.0 - 0.5 < px && px < o.Points.Max(p => p.X.Raw) / 10000.0 + 0.5
                && o.Points.Min(p => p.Z.Raw) / 10000.0 - 0.5 < pz && pz < o.Points.Max(p => p.Z.Raw) / 10000.0 + 0.5, o.Id));
    }

    // --- Pilote : la quête principale du chapitre 4 ---------------------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Chapitre_4_joue_de_bout_en_bout(bool livrer)
    {
        // Fin du chapitre 3 (Sœur Albe lit l'ordre scellé) : la fin du chapitre 3 commence le chapitre 4. La lanterne de Rosalie est déjà
        // allumée (chapitre 2), les herbes de Sœur Albe pas encore cueillies : son offre passe d'abord, le chapitre 4 par une réponse.
        var albe = new SavedStory(AfterChapter3.Take(7), new Dictionary<string, QuestProgress>
        {
            [Bourg] = new QuestProgress(QuestProgress.Done), [Lanterne] = new QuestProgress(QuestProgress.Done), [Hameau] = new QuestProgress("albe"),
        });
        WorldSession w = OpenIn("zone.chapelle", Albe, Hero(15, albe, 500));
        Say(w, "npc.albe", "ch3_ordre");
        Choose(w, 0, "ch3_tour");
        Choose(w, 0);
        List<string> start = Ticks(w);
        Assert.Contains($"QuestCompleted {Hameau}", start);
        Assert.Contains($"QuestStarted {Main}/rosalie", start);
        long xp0 = w.Character.Xp, money0 = w.Money;
        // Rosalie confie sa lettre pour Thibaut (et se souvient d'une promesse).
        w = Go(w, Rosalie, "zone.maison");
        Say(w, "npc.habitant", "ch4_rosalie");
        Assert.Contains("TrustChanged npc.habitant 1", Choose(w, 0));
        Assert.Contains($"StepCompleted {Main}/rosalie", Ticks(w));
        Assert.Equal(1, w.Inventory.Count("item.lettre_de_rosalie"));
        // La tour de guet (lieu atteint) : vide, et des traces vers le camp.
        w = Go(w, Tour0, "zone.vallons", out List<string> tour);
        tour.AddRange(Ticks(w));
        Assert.Contains($"StepCompleted {Main}/tour", tour);
        Assert.Contains("Discovered discovery.tour_vide", tour);
        // Thibaut, caché dans les rochers au sud du camp, avant le chef.
        w = Go(w, Thibaut);
        Say(w, "npc.thibaut", "ch4_camp");
        Choose(w, 1, "ch4_mere");
        Choose(w, 0);
        Assert.Empty(Ticks(w));
        // Le camp (lieu atteint), puis le chef des bandits : la victoire est annoncée à l'histoire par son fait, comme le fait la partie à la
        // fin du combat de chef (le combat lui-même est couvert par les tests des chefs).
        w = Go(w, Camp0, "zone.vallons", out List<string> camp);
        Assert.Contains($"StepCompleted {Main}/camp", camp.Concat(Ticks(w)));
        Assert.True(w.Narrative!.Learn("fact.chef_des_bandits.vaincu", new List<StoryEvent>()));
        List<string> chef = Ticks(w);
        Assert.Contains($"StepCompleted {Main}/chef", chef);
        Assert.Contains("Discovered discovery.mots_du_chef", chef);
        // Le premier choix, chez Thibaut (réponses de Damien du 5 novembre 2026) : livrer Mâchefer au capitaine, ou le tuer de sa main. Il reste
        // écrit dans les faits, et le capitaine en parle une fois si Mâchefer lui est livré.
        w = Go(w, Thibaut);
        DialogueView choice = Say(w, "npc.thibaut", "ch4_chef");
        Assert.Equal(2, choice.Options.Count);
        Choose(w, livrer ? 0 : 1, livrer ? "ch4_livre" : "ch4_abattu");
        Choose(w, 0);
        Assert.Contains($"StepCompleted {Main}/choix", Ticks(w));
        Assert.Equal((livrer, !livrer), (w.Narrative!.Knows("fact.ch4.chef.livre"), w.Narrative.Knows("fact.ch4.chef.abattu")));
        // La lettre de Rosalie donnée à Thibaut ; il répond.
        Say(w, "npc.thibaut", "ch4_lettre");
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.thibaut"));
        List<string> lettre = Ticks(w);
        Assert.Contains("ItemTaken item.lettre_de_rosalie 1", lettre);
        Assert.Contains($"StepCompleted {Main}/lettre", lettre);
        Assert.Equal(1, w.Inventory.Count("item.lettre_de_thibaut"));
        Say(w, "npc.thibaut", livrer ? "ch4_apres_livre" : "ch4_apres_abattu");
        w = Go(w, Capitaine);
        // Son offre de la tour passe d'abord ; une réponse mène à Mâchefer.
        if (livrer) Assert.Contains("FactLearned fact.ch4.capitaine.chef", LeadsTo(w, "npc.capitaine", "ch4_tour", "text.ch4.capitaine.vers_chef", "ch4_chef_livre")
            .Concat(Choose(w, 0)));
        else LeadsTo(w, "npc.capitaine", "ch4_tour", "text.ch4.capitaine.vers_chef", null);
        // La réponse portée à Rosalie.
        w = Go(w, Rosalie, "zone.maison");
        Say(w, "npc.habitant", "ch4_reponse");
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.habitant"));
        List<string> reponse = Ticks(w);
        Assert.Contains("ItemTaken item.lettre_de_thibaut 1", reponse);
        Assert.Contains($"StepCompleted {Main}/reponse", reponse);
        Assert.Equal(2, w.Narrative!.Trust("npc.habitant"));
        Say(w, "npc.habitant", "ch2_lanterne_apres");
        // Sœur Albe et le Sans-Repos, par une réponse de l'offre des herbes (son dialogue a déjà seize entrées). Fin du chapitre 4.
        w = Go(w, Albe, "zone.chapelle");
        DialogueView herbes = Say(w, "npc.albe", "ch2_herbes");
        Assert.Equal(DialogueRefusal.None, w.Choose(Assert.Single(herbes.Options, o => o.Text == "text.ch4.albe.vers_sans_repos").Index, out DialogueView? sansRepos, out _));
        Assert.Equal("ch4_sans_repos", sansRepos!.Node);
        // Sœur Albe répond selon le choix : une seule réponse, celle du choix fait.
        Choose(w, Assert.Single(sansRepos.Options).Index, livrer ? "ch4_livre" : "ch4_abattu");
        Choose(w, 0);
        List<string> end = Ticks(w);
        Assert.Contains($"QuestCompleted {Main}", end);
        Assert.True(w.Narrative!.Knows("fact.ch4.vers_le_seuil"));
        // Récompenses (proposées) : 2 200 points d'expérience et 13 argent ; du niveau 15 au niveau 17 sans les quêtes secondaires ni les
        // combats ; avec les quatre quêtes secondaires (2 100 points), niveau 18, et les combats du chapitre (camp et hautes pierres) mènent au 20.
        Assert.Equal((xp0 + 2200, money0 + 1300), (w.Character.Xp, w.Money));
        Assert.Equal((17, 18), (Progression.LevelFor(XpForLevel(15) + 2200), Progression.LevelFor(XpForLevel(15) + 2200 + 2100)));
        // Ensuite, chez Albe, l'offre des herbes passe d'abord ; les herbes finies, son mot d'après mène au Sans-Repos, plus à la tour.
        var after = Done(AfterChapter3.Append("fact.ch4.vers_le_seuil"), Bourg, Hameau, Main, Herbes);
        LeadsTo(OpenIn("zone.chapelle", Albe, Hero(17, after)), "npc.albe", "ch2_apres", "text.ch4.albe.vers_apres", "ch4_apres");
        LeadsTo(OpenIn("zone.chapelle", Albe, Hero(17, after)), "npc.albe", "ch2_apres", "text.ch3.albe.vers_tour", null);
        Say(OpenIn("zone.maison", Rosalie, Hero(17, Done(AfterChapter3, Bourg, Hameau, Main, Lanterne))), "npc.habitant", "ch4_apres");
    }

    [Fact]
    public void Ancienne_partie_au_chapitre_3_fini_commence_le_chapitre_4_chez_Albe()
    {
        // Une sauvegarde d'avant V1.9.4, le chapitre 3 fini : Sœur Albe envoie à la tour par une réponse, une seule fois ; ses herbes passent avant.
        WorldSession w = OpenIn("zone.chapelle", Albe, Hero(15, Done(AfterChapter3, Bourg, Hameau)));
        Assert.Contains($"QuestStarted {Main}/rosalie", LeadsTo(w, "npc.albe", "ch2_herbes", "text.ch4.albe.ancienne", "ch4_ancienne"));
        LeadsTo(w, "npc.albe", "ch2_herbes", "text.ch4.albe.ancienne", null);
        // Les herbes finies, par le mot d'après le chapitre 3 (« Et la tour de guet ? »).
        WorldSession old = OpenIn("zone.chapelle", Albe, Hero(15, Done(AfterChapter3, Bourg, Hameau, Herbes)));
        LeadsTo(old, "npc.albe", "ch2_apres", "text.ch3.albe.vers_tour", "ch3_apres");
        Assert.Contains($"QuestStarted {Main}/rosalie", Choose(old, Assert.Single(Say(old, "npc.albe", "ch2_apres").Options, o => o.Text == "text.ch4.albe.ancienne").Index, "ch4_ancienne"));
        // Avant la fin du chapitre 3, rien du chapitre 4.
        LeadsTo(OpenIn("zone.chapelle", Albe, Hero(15, With(AfterChapter3.Take(7), (Hameau, "retour")))), "npc.albe", "ch2_herbes", "text.ch4.albe.ancienne", null);
    }

    // --- Pilotes : les quatre quêtes secondaires ------------------------------------------------------------------------

    [Fact]
    public void La_tour_silencieuse_de_bout_en_bout()
    {
        WorldSession w = Open(Capitaine, Hero(16, Done(AfterChapter3, Bourg, Hameau)));
        Say(w, "npc.capitaine", "ch4_tour");
        Assert.Contains($"QuestStarted {Tour}/escalier", Choose(w, 0));
        Say(w, "npc.capitaine", "ch4_tour_attente");
        // Victoires annoncées à l'histoire (contournement : les combats ont leurs tests) : les deux archers de la tour, dans l'ordre ; un archer
        // des ruines ne compte pas.
        w.Narrative!.Defeated("archer", null);
        Assert.Empty(Ticks(w, 1));
        w.Narrative.Defeated("archer2", null);
        Assert.Contains($"StepCompleted {Tour}/escalier", Ticks(w, 1));
        w.Narrative.Defeated("archer3", null);
        Assert.Contains($"StepCompleted {Tour}/guetteur", Ticks(w, 1));
        // Dans la tour (lieu atteint) : le registre de la garde.
        w = Go(w, Tour0, "zone.vallons", out List<string> tour);
        tour.AddRange(Ticks(w));
        Assert.Contains($"StepCompleted {Tour}/registre", tour);
        Assert.Contains("Discovered discovery.registre_de_garde", tour);
        Assert.Equal(1, w.Inventory.Count("item.registre_de_garde"));
        w = Go(w, Capitaine);
        Say(w, "npc.capitaine", "ch4_tour_rapport");
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.capitaine"));
        Assert.Contains($"StepCompleted {Tour}/rapport", Ticks(w));
        Say(w, "npc.capitaine", "ch4_tour_cor");
        Choose(w, 0);
        // Le bandit au cor, à l'est des tentes du camp ; un autre bandit du camp ne compte pas.
        w.Narrative!.Defeated("bandit_epee1", "bandit");
        Assert.Empty(Ticks(w, 1));
        w.Narrative.Defeated("bandit_epee2", "bandit");
        Assert.Contains($"StepCompleted {Tour}/cor", Ticks(w, 1));
        Assert.Equal(1, w.Inventory.Count("item.cor_de_la_tour"));
        Say(w, "npc.capitaine", "ch4_tour_livrer");
        long xp = w.Character.Xp, money = w.Money;
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.capitaine"));
        List<string> end = Ticks(w);
        Assert.Contains("ItemTaken item.cor_de_la_tour 1", end);
        Assert.Contains("TrustChanged npc.capitaine 1", end);
        Assert.Contains($"RewardToChoose {Tour}", end);
        Assert.Equal((xp + 450, money + 400), (w.Character.Xp, w.Money));
        Say(w, "npc.capitaine", "ch4_tour_recompense");
        Assert.Equal(DialogueRefusal.None, w.ChooseReward(Tour, 1, out IReadOnlyList<StoryEvent> chosen));
        Assert.Equal(new[] { "ItemGiven item.lame_de_la_tour 1", $"QuestCompleted {Tour}" }, chosen.Select(e => e.ToString()));
        Say(w, "npc.capitaine", "ch4_tour_apres");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Le_deserteur_de_bout_en_bout(bool aider)
    {
        // Les potions d'Aubine pas encore commencées : leur offre passe d'abord, le déserteur par une réponse.
        WorldSession w = Open(Aubine, Hero(15, Done(AfterChapter3, Bourg, Hameau)));
        LeadsTo(w, "npc.herboriste_hameau", "ch3_potions", "text.ch4.herboriste_hameau.vers_deserteur", "ch4_deserteur");
        Assert.Contains($"QuestStarted {Deserteur}/colin", Choose(w, 0));
        Say(w, "npc.herboriste_hameau", "ch3_potions");
        w = Go(w, Colin);
        Say(w, "npc.deserteur", "ch4_colin");
        Choose(w, 0, "ch4_colin_choix");
        Choose(w, aider ? 0 : 1, aider ? "ch4_colin_aide" : "ch4_colin_livre");
        List<string> choice = Ticks(w);
        Assert.Contains($"StepCompleted {Deserteur}/colin", choice);
        Assert.Contains($"StepCompleted {Deserteur}/choix", choice);
        Assert.Equal((aider, !aider), (w.Narrative!.Knows("fact.ch4.deserteur.aide"), w.Narrative.Knows("fact.ch4.deserteur.livre")));
        w = Go(w, Aubine);
        long xp = w.Character.Xp, money = w.Money;
        Say(w, "npc.herboriste_hameau", aider ? "ch4_deserteur_aide" : "ch4_deserteur_livre");
        Assert.Contains($"QuestCompleted {Deserteur}", Ticks(w));
        Assert.Equal((xp + 450, money + 150, aider ? 1 : -1, aider ? 0 : 1),
            (w.Character.Xp, w.Money, w.Narrative!.Trust("npc.herboriste_hameau"), w.Narrative.Trust("npc.capitaine")));
        // Ensuite : l'offre des potions d'abord ; les potions finies, Aubine et Colin s'en souviennent.
        Say(w, "npc.herboriste_hameau", "ch3_potions");
        var facts = AfterChapter3.Append(aider ? "fact.ch4.deserteur.aide" : "fact.ch4.deserteur.livre");
        Say(Open(Aubine, Hero(15, Done(facts, Bourg, Hameau, Deserteur, Potions))), "npc.herboriste_hameau", aider ? "ch4_deserteur_apres_aide" : "ch4_deserteur_apres_livre");
        Say(Go(w, Colin), "npc.deserteur", aider ? "ch4_colin_apres_aide" : "ch4_colin_apres_livre");
    }

    [Fact]
    public void La_prime_du_Borgne_de_bout_en_bout()
    {
        WorldSession w = OpenIn("zone.guilde", Maitresse, Hero(15, Member(AfterChapter3, Bourg, Hameau)));
        Say(w, "npc.maitresse_guilde", "ch4_prime");
        Assert.Contains($"QuestStarted {Prime}/bertille", Choose(w, 0));
        Say(w, "npc.maitresse_guilde", "ch4_prime_attente");
        w = Go(w, Epiciere);
        Say(w, "npc.epiciere", "ch4_borgne");
        Choose(w, 0);
        Assert.Contains($"StepCompleted {Prime}/bertille", Ticks(w));
        // Victoire annoncée à l'histoire (contournement : les combats ont leurs tests) : le Grand Borgne, l'aîné ; ni un autre bandit, ni
        // le Borgne cadet du contrat de la guilde (niveau 6), qui garde sa prime à lui.
        w.Narrative!.Defeated("bandit_epee1", "bandit");
        w.Narrative.Defeated("le_borgne", "bandit");
        Assert.DoesNotContain($"StepCompleted {Prime}/borgne", Ticks(w, 1));
        w.Narrative.Defeated("grand_borgne", "bandit");
        Assert.Contains($"StepCompleted {Prime}/borgne", Ticks(w, 1));
        Assert.Equal(1, w.Inventory.Count("item.oeil_de_verre"));
        Say(w, "npc.epiciere", "ch4_oeil");
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.epiciere"));
        List<string> oeil = Ticks(w);
        Assert.Contains("ItemTaken item.oeil_de_verre 1", oeil);
        Assert.Contains("TrustChanged npc.epiciere 1", oeil);
        Say(w, "npc.epiciere", "accueil");
        w = Go(w, Maitresse, "zone.guilde");
        long xp = w.Character.Xp, money = w.Money;
        Say(w, "npc.maitresse_guilde", "ch4_prime_fin");
        List<string> end = Ticks(w);
        Assert.Contains($"RewardToChoose {Prime}", end);
        Assert.Equal((xp + 500, money + 300), (w.Character.Xp, w.Money));
        Say(w, "npc.maitresse_guilde", "ch4_prime_recompense");
        Assert.Equal(DialogueRefusal.None, w.ChooseReward(Prime, 0, out IReadOnlyList<StoryEvent> chosen));
        Assert.Equal(new[] { "ItemGiven item.gantelets_de_la_tour 1", $"QuestCompleted {Prime}", $"GuildPoints {Prime} 20" }, chosen.Select(e => e.ToString()));
        Say(w, "npc.maitresse_guilde", "membre");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Le_marchand_qui_en_savait_trop_de_bout_en_bout(bool cacher)
    {
        WorldSession w = Open(Renart, Hero(16, Done(AfterChapter3, Bourg, Hameau)));
        Say(w, "npc.renart", "ch4_carnet");
        IReadOnlyList<string> accepted = Choose(w, 0);
        Assert.Contains($"QuestStarted {Carnet}/josse", accepted);
        Assert.Contains("ItemGiven item.carnet_de_renart 1", accepted);
        Say(w, "npc.renart", "ch4_carnet_attente");
        w = Go(w, Josse);
        Say(w, "npc.doyen", "ch4_carnet");
        Choose(w, cacher ? 0 : 1, cacher ? "ch4_carnet_cache" : "ch4_carnet_lu");
        Assert.Contains($"StepCompleted {Carnet}/josse", Ticks(w));
        Say(w, "npc.doyen", "ch3_apres");
        w = Go(w, Renart);
        long xp = w.Character.Xp, money = w.Money;
        Say(w, "npc.renart", cacher ? "ch4_carnet_cache" : "ch4_carnet_lu");
        Choose(w, 0);
        Assert.Contains($"QuestCompleted {Carnet}", Ticks(w));
        // Le choix reste écrit dans les faits ; caché, Renart paie et s'en souvient ; lu, le carnet reste dans le sac et Renart le sait.
        Assert.Equal((xp + 450, money + (cacher ? 400 : 0), cacher ? 0 : 1, cacher ? 1 : -1, !cacher),
            (w.Character.Xp, w.Money, w.Inventory.Count("item.carnet_de_renart"), w.Narrative!.Trust("npc.renart"), w.Narrative.Discoveries.Contains("discovery.carnet_de_renart")));
        Say(w, "npc.renart", cacher ? "ch4_apres_cache" : "ch4_apres_lu");
    }

    // --- Leçon de la PR #88 : aucune entrée prioritaire ne masque définitivement une offre ------------------------------

    [Fact]
    public void Offres_de_chaque_donneur_encore_accessibles()
    {
        string[] facts = AfterChapter3;
        // Toutes les autres quêtes du donneur finies, les chapitres 2 à 4 compris : l'offre passe avant les mercis.
        Say(Open(Capitaine, Hero(16, Done(facts, Bourg, Hameau, Main))), "npc.capitaine", "ch4_tour");
        Say(Open(Aubine, Hero(15, Done(facts, Bourg, Hameau, Main, Potions))), "npc.herboriste_hameau", "ch4_deserteur");
        Say(OpenIn("zone.guilde", Maitresse, Hero(15, Member(facts, Bourg, Hameau, Main))), "npc.maitresse_guilde", "ch4_prime");
        Say(Open(Renart, Hero(16, Done(facts, Bourg, Hameau, Main))), "npc.renart", "ch4_carnet");
        // Chez Aubine, les potions offertes, en cours ou à récompenser gardent une réponse vers le déserteur ; le déserteur fini ne masque pas les potions.
        LeadsTo(Open(Aubine, Hero(15, With(facts, (Potions, "potions")))), "npc.herboriste_hameau", "ch3_potions_attente", "text.ch4.herboriste_hameau.vers_deserteur", "ch4_deserteur");
        Say(Open(Aubine, Hero(15, Done(facts.Append("fact.ch4.deserteur.aide"), Deserteur))), "npc.herboriste_hameau", "ch3_potions");
        // Pendant le chapitre 4, les donneurs offrent aussi ; chez Rosalie, la lanterne passe avant le mot d'après du chapitre 4.
        var chef = new SavedStory(facts, new Dictionary<string, QuestProgress> { [Bourg] = new QuestProgress(QuestProgress.Done), [Hameau] = new QuestProgress(QuestProgress.Done), [Main] = new QuestProgress("chef") });
        Say(Open(Renart, Hero(16, chef)), "npc.renart", "ch4_carnet");
        Say(OpenIn("zone.maison", Rosalie, Hero(15, With(facts, (Main, "tour")))), "npc.habitant", "ch2_lanterne");
        Say(OpenIn("zone.maison", Rosalie, Hero(17, Done(facts, Main))), "npc.habitant", "ch2_lanterne");
        // Chez Sœur Albe, l'offre des herbes garde la réponse du chapitre 4 ; chez Renart, l'ancienne partie du chapitre 2 passe avant le carnet.
        LeadsTo(OpenIn("zone.chapelle", Albe, Hero(17, With(facts.Append("fact.ch4.chef.abattu"), (Main, "albe")))), "npc.albe", "ch2_herbes",
            "text.ch4.albe.vers_sans_repos", "ch4_sans_repos");
        Say(Open(Renart, Hero(16, Done(facts.Take(5), Bourg))), "npc.renart", "ch3_ancienne");
        // Sous le niveau demandé (ou sans carte de la guilde), rien de nouveau n'est offert.
        Say(Open(Capitaine, Hero(15, null)), "npc.capitaine", "ch4_accueil");
        LeadsTo(Open(Aubine, Hero(14, null)), "npc.herboriste_hameau", "ch3_potions", "text.ch4.herboriste_hameau.vers_deserteur", null);
        Say(OpenIn("zone.guilde", Maitresse, Hero(14, Member(facts, Bourg))), "npc.maitresse_guilde", "membre");
        Say(OpenIn("zone.guilde", Maitresse, Hero(15, Done(facts, Bourg))), "npc.maitresse_guilde", "accueil");
        Say(Open(Renart, Hero(15, Done(facts, Bourg, Hameau))), "npc.renart", "ch2_apres");
        Say(Open(Thibaut, Hero(15, null)), "npc.thibaut", "ch4_accueil");
        Say(Open(Colin, Hero(15, null)), "npc.deserteur", "ch4_accueil");
    }
}
