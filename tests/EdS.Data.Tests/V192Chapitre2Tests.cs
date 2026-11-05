using System.Text.Json.Nodes;
using EdS.Data.Crafting;
using EdS.Data.Guild;
using EdS.Data.Json;
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
/// Sous-lot V1.9.2, chapitre 2 « Le bourg d'Aube » (docs/MONDE.md, docs/ECRITURE.md) : la quête principale du chapitre (Sœur Albe, les vols
/// d'éclats, Renart, l'inscription à la guilde) et ses six quêtes secondaires. Chaque quête est jouée de bout en bout par un pilote scripté :
/// dialogues, réponses, récolte aux nœuds, forge, inscription au bureau de la guilde, livraisons ; la partie est sauvegardée, relue et rouverte
/// à chaque déplacement (au même instant du monde). Contournements, dits là où ils ont lieu : la victoire sur l'archer des ruines est annoncée à
/// l'histoire sans livrer le combat (les combats ont leurs tests) ; une partie du minerai et des herbes est dans le sac de départ.
/// </summary>
[Trait("Category", "World")]
public class V192Chapitre2Tests
{
    private const string Mission = "quest.traces_sous_le_moulin", Bourg = "quest.bourg_d_aube", Dette = "quest.dette_a_l_auberge",
        Cloches = "quest.cloches_de_la_chapelle", Lanterne = "quest.derniere_lanterne", Puits = "quest.puits_qui_murmure",
        Apprentie = "quest.apprentie_de_maelle", Herbes = "quest.herbes_pour_la_chapelle", Peaux = "quest.peaux_pour_l_auberge", Bete = "quest.bete_blanche";

    // Où se tenir (à 1 à 2 m du PNJ, hors de tout obstacle) : dans les Vallons, puis dans les intérieurs.
    private static readonly (double X, double Z) Gaspard = (26, -13), Renart = (-47.5, -3.3), Lison = (-62.1, -1), Forge = (-19, -5), Colombe = (-22, -4.8),
        Epiciere = (-50.8, 4.4), Herboriste = (-65.2, -6.4), Gue = (12, 0.5), FilonOuest = (31, -12.5), FilonCentre = (44, -0.5), FilonEst = (54, 5.5),
        HerbesLac = (-12.5, -62), HerbesVerger = (54, -41.5);
    private static readonly (double X, double Z) Albe = (3.2, 3.2), Pretre = (-2, 3.6), Mathurin = (3.75, 1.6), Rosalie = (-1.8, -2.4), Maitresse = (0, 1.0);

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

    private static WorldSession OpenIn(string zone, (double X, double Z) at, SavedCharacter hero, long tick = 0) => Open(zone, at, hero, tick, null, out _);

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

    /// <summary>Le chapitre 1 fini (ou plus loin) : la mission continue après Gaspard, le chapitre 2 est fini.</summary>
    private static readonly string[] AfterChapter2 = { "fact.traces.recit", "fact.moulin.porte_ouverte", "fact.ch1.vers_le_bourg", "fact.ch2.albe.rencontree",
        "fact.ch2.vers_les_champs" };

    // --- Données -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Chapitre_2_et_ses_quetes_livres()
    {
        StoryBook book = Story.Value;
        Assert.Equal(new[] { Bourg, Dette, Cloches, Lanterne, Puits, Apprentie, Herbes }, book.Quests.Skip(9).Take(7).Select(q => q.Id));   // ajoutées à la fin (V1.9.3 : le chapitre 3 suit)
        Assert.Equal(QuestCategory.Main, book.Quest(Bourg).Category);
        Assert.Equal(new[] { "chapelle", "mathurin", "renart", "ruines", "retour", "guilde", "aveu" }, book.Quest(Bourg).Steps.Select(s => s.Id));
        Assert.Empty(book.Quest(Bourg).Requires);   // commencée par le chapitre 1 : jamais fermée
        // Niveaux 5 à 10 : les quêtes secondaires s'ouvrent au niveau 5 ou 6 (6 là où l'on récolte dans un lieu de niveau 6).
        foreach ((string q, int level) in new[] { (Dette, 5), (Cloches, 5), (Lanterne, 5), (Puits, 5), (Apprentie, 6), (Herbes, 6) })
        {
            Assert.Equal(QuestCategory.Side, book.Quest(q).Category);
            Assert.Equal(level, Assert.Single(book.Quest(q).Requires).Value);
        }
        // Sources des objets : l'archer des ruines (niveau 6), les filons de la carrière (6), les herbes du lac et des vergers (6).
        ZoneDefinition zone = Vallons.Value;
        Assert.Equal(6, zone.MinLevelAt(new GroundVector(Fixed.FromInt(21), Fixed.FromInt(1))));
        CraftingBook crafting = Crafting(zone);
        foreach (string node in new[] { "filon_ouest", "filon_centre", "filon_est", "herbes_lac", "herbes_verger" })
            Assert.Equal(6, zone.MinLevelAt(crafting.Nodes.Single(n => n.Id == node).Position));
        Assert.Equal(new[] { "npc.albe", "npc.renart", "npc.lison", "npc.apprentie" }, book.Npcs.Skip(14).Take(4).Select(n => n.Id));
        Assert.Equal("zone.chapelle", book.Npc("npc.albe").Zone);
        // Les cinq objets de quête ne se vendent pas.
        foreach (string item in new[] { "item.bourse_d_eclats", "item.bourse_de_renart", "item.corde_de_cloche", "item.huile_de_lampe", "item.pierre_benie" })
        {
            Assert.Equal("quete", ItemsCatalog.Item(item).Icon);
            Assert.False(ShippedMerchants.Values.ContainsKey(item), item);
        }
        Assert.Contains(book.Discoveries, d => d.Id == "discovery.sceau_d_ambre");
        Assert.Contains(book.Discoveries, d => d.Id == "discovery.voix_du_puits");
    }

    [Fact]
    public void Nouveaux_PNJ_a_portee_depuis_une_place_libre()
    {
        foreach ((string npc, (double x, double z) at, ZoneDefinition zone) in new[]
        {
            ("npc.renart", Renart, Vallons.Value), ("npc.lison", Lison, Vallons.Value), ("npc.apprentie", Colombe, Vallons.Value),
            ("npc.albe", Albe, V16TestData.Interiors.Single(z => z.Id == "zone.chapelle")),
        })
        {
            Npc n = Story.Value.Npc(npc);
            double nx = n.Position.X.Raw / 10000.0, nz = n.Position.Z.Raw / 10000.0;
            Assert.True(Math.Sqrt((nx - at.x) * (nx - at.x) + (nz - at.z) * (nz - at.z)) < 3, npc);
            foreach ((double px, double pz) in new[] { at, (nx, nz) })
                Assert.All(zone.Setup.Space!.Obstacles, o => Assert.False(
                    o.Points.Min(p => p.X.Raw) / 10000.0 - 0.5 < px && px < o.Points.Max(p => p.X.Raw) / 10000.0 + 0.5
                    && o.Points.Min(p => p.Z.Raw) / 10000.0 - 0.5 < pz && pz < o.Points.Max(p => p.Z.Raw) / 10000.0 + 0.5, $"{npc} : {o.Id}"));
        }
    }

    // --- Pilote : la quête principale du chapitre 2 ---------------------------------------------------------------------

    [Fact]
    public void Chapitre_2_joue_de_bout_en_bout()
    {
        // Fin du chapitre 1 : Gaspard envoie à la chapelle, et le chapitre 2 commence.
        var ch1 = With(new[] { "fact.traces.recit", "fact.moulin.porte_ouverte" }, (Mission, "gaspard"));
        WorldSession w = Open(Gaspard, Hero(5, ch1, 500));
        Say(w, "npc.gaspard", "ch1_souvenir");
        Choose(w, 1);
        List<string> start = Ticks(w);
        Assert.Contains($"StepCompleted {Mission}/gaspard", start);
        Assert.Contains($"QuestStarted {Bourg}/chapelle", start);
        Assert.Equal("manifestation", w.Narrative!.CurrentStep(Mission)!.Id);   // la suite de l'ancienne mission reste à sa place
        long xp0 = w.Character.Xp, money0 = w.Money;
        // Sœur Albe reconnaît l'Éclat et parle des vols.
        w = Go(w, Albe, "zone.chapelle");
        Say(w, "npc.albe", "ch2_eclat");
        Assert.Equal(new[] { "TrustChanged npc.albe 1" }, Choose(w, 0, "ch2_vols"));
        Choose(w, 1, "ch2_rassembleurs");
        Choose(w, 0);
        List<string> chapelle = Ticks(w);
        Assert.Contains($"StepCompleted {Bourg}/chapelle", chapelle);
        Assert.Contains("FactLearned fact.ch2.albe.rencontree", chapelle);
        Say(w, "npc.albe", "ch2_en_cours");
        // Mathurin a vu le voleur… avec Renart.
        w = Go(w, Mathurin, "zone.auberge");
        Say(w, "npc.aubergiste", "ch2_vol");
        Choose(w, 1, "ch2_vol_vin");
        Choose(w, 0);
        Assert.Contains($"StepCompleted {Bourg}/mathurin", Ticks(w));
        // Renart vend sa mémoire : un argent.
        w = Go(w, Renart);
        Say(w, "npc.renart", "ch2_piste");
        Assert.Equal(new[] { "MoneyTaken money 100", "FactLearned fact.ch2.renart.piste" }, Choose(w, 0, "ch2_ruines"));
        Choose(w, 0);
        Assert.Contains($"StepCompleted {Bourg}/renart", Ticks(w));
        // Les ruines : la victoire sur l'archer qui fait le guet est annoncée à l'histoire comme le fait la partie à la mort de la créature,
        // sans livrer le combat lui-même (couvert par les tests des zones et du moteur) ; un autre archer (celui de la tour) ne compte pas.
        w = Go(w, Gue);
        w.Narrative!.Defeated("archer2", "archer");
        Assert.DoesNotContain($"StepCompleted {Bourg}/ruines", Ticks(w, 1));
        w.Narrative.Defeated("archer", "archer");
        List<string> ruines = Ticks(w, 1);
        Assert.Contains($"StepCompleted {Bourg}/ruines", ruines);
        Assert.Contains("Discovered discovery.sceau_d_ambre", ruines);
        Assert.Equal(1, w.Inventory.Count("item.bourse_d_eclats"));
        // La bourse rendue à Sœur Albe ; elle envoie à la guilde.
        w = Go(w, Albe, "zone.chapelle");
        Say(w, "npc.albe", "ch2_bourse");
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.albe"));
        List<string> retour = Ticks(w);
        Assert.Contains("ItemTaken item.bourse_d_eclats 1", retour);
        Assert.Contains($"StepCompleted {Bourg}/retour", retour);
        Say(w, "npc.albe", "ch2_guilde");
        Choose(w, 1, "ch2_attendre_renart");
        Choose(w, 0);
        // Inscription au bureau de Dame Brunehilde (moteur de la guilde, V1.7).
        w = Go(w, Maitresse, "zone.guilde");
        Say(w, "npc.maitresse_guilde", "ch2_albe");
        Choose(w, 0, "guilde");
        Assert.Equal(GuildRefusal.None, w.JoinGuild(out IReadOnlyList<StoryEvent> joined));
        Assert.Equal("GuildJoined g", joined[0].ToString());   // puis la réputation déjà gagnée (quêtes finies, découvertes)
        Assert.Contains($"StepCompleted {Bourg}/guilde", Ticks(w));
        Say(w, "npc.maitresse_guilde", "membre");
        // Renart avoue : l'Ambre achète, les bandits des champs récoltent. Fin du chapitre 2.
        w = Go(w, Renart);
        Say(w, "npc.renart", "ch2_aveu");
        Assert.Equal(new[] { "FactLearned fact.ch2.renart.aveu", "TrustChanged npc.renart 1" }, Choose(w, 1, "ch2_aveu_fin").OrderBy(e => e, StringComparer.Ordinal));
        Choose(w, 0);
        List<string> end = Ticks(w);
        Assert.Contains($"QuestCompleted {Bourg}", end);
        Assert.True(w.Narrative!.Knows("fact.ch2.vers_les_champs"));
        Assert.True(w.Narrative.IsDone(Bourg));
        Say(w, "npc.renart", "ch2_apres");
        // Récompenses (proposées) : 1 120 points d'expérience et 7 argent, moins l'argent donné à Renart ; du niveau 5 au niveau 8 sans
        // les quêtes secondaires ni les combats.
        Assert.Equal((xp0 + 1120, money0 + 700 - 100), (w.Character.Xp, w.Money));
        Assert.Equal(8, Progression.LevelFor(w.Character.Xp));   // avec les six quêtes secondaires : niveau 10
        w = Go(w, Albe, "zone.chapelle");
        Say(w, "npc.albe", "ch2_herbes");   // une fois le chapitre fini, son offre passe avant le merci (leçon de la PR #88)
    }

    [Fact]
    public void Renart_se_souvient_de_la_reponse_et_ne_fait_pas_credit()
    {
        var at = With(new[] { "fact.ch2.albe.rencontree" }, (Bourg, "renart"));
        // Sans un argent, la réponse payante est refusée et rien ne change ; les deux autres restent.
        WorldSession poor = Open(Renart, Hero(5, at, 50));
        Say(poor, "npc.renart", "ch2_piste");
        Assert.Equal(DialogueRefusal.NotEnoughMoney, poor.Choose(0, out _, out IReadOnlyList<StoryEvent> none));
        Assert.Empty(none);
        Assert.Equal(50, poor.Money);
        Assert.False(poor.Narrative!.Knows("fact.ch2.renart.piste"));
        // La menace donne la piste mais coûte sa confiance ; la flatterie la gagne.
        WorldSession firm = Open(Renart, Hero(5, at, 50));
        Say(firm, "npc.renart", "ch2_piste");
        Assert.Contains("TrustChanged npc.renart -1", Choose(firm, 1, "ch2_ruines"));
        Assert.True(firm.Narrative!.Knows("fact.ch2.renart.menace"));
        WorldSession sly = Open(Renart, Hero(5, at, 50));
        Say(sly, "npc.renart", "ch2_piste");
        Assert.Contains("TrustChanged npc.renart 1", Choose(sly, 2, "ch2_ruines"));
        Assert.Contains($"StepCompleted {Bourg}/renart", Ticks(sly));
    }

    [Fact]
    public void Ancienne_partie_deja_loin_dans_la_mission_commence_le_chapitre_2_chez_Albe()
    {
        // Une sauvegarde d'avant V1.9.1, déjà plus loin que le pré (sans les étapes de la meute et de Gaspard), ou la mission finie.
        foreach (SavedStory old in new[]
        {
            With(new[] { "fact.traces.recit", "fact.moulin.porte_ouverte" }, (Mission, "rodeur")),
            Done(new[] { "fact.traces.recit", "fact.moulin.porte_ouverte", "fact.odile.retour" }, Mission),
        })
        {
            WorldSession w = OpenIn("zone.chapelle", Albe, Hero(8, old));
            Say(w, "npc.albe", "ch2_ancienne");
            Assert.Contains($"QuestStarted {Bourg}/chapelle", Choose(w, 0));
            Say(w, "npc.albe", "ch2_eclat");
            Choose(w, 1, "ch2_vols");
            Choose(w, 0);
            Assert.Contains($"StepCompleted {Bourg}/chapelle", Ticks(w));
        }
        // Au milieu du chapitre 1 (avant Gaspard), Sœur Albe n'a rien à dire de l'Éclat.
        var middle = With(new[] { "fact.traces.recit" }, (Mission, "meute"));
        Assert.All(Say(OpenIn("zone.chapelle", Albe, Hero(5, middle)), "npc.albe", "ch2_accueil").Options, o => Assert.Equal("text.ch2.albe.accueil.partir", o.Text));
    }

    // --- Pilotes : les six quêtes secondaires ---------------------------------------------------------------------------

    [Fact]
    public void Une_dette_a_l_auberge_de_bout_en_bout()
    {
        // Les peaux de loup jamais acceptées : l'ardoise se demande quand même, depuis l'accueil de Mathurin.
        WorldSession w = OpenIn("zone.auberge", Mathurin, Hero(5, Done(AfterChapter2, Bourg)));
        Say(w, "npc.aubergiste", "accueil");
        Choose(w, 2, "ch2_dette");
        Assert.Contains($"QuestStarted {Dette}/renart", Choose(w, 0));
        Say(w, "npc.aubergiste", "ch2_dette_attente");
        // Renart rend la bourse (une seule fois).
        w = Go(w, Renart);
        Say(w, "npc.renart", "ch2_dette");
        Assert.Equal(new[] { "ItemGiven item.bourse_de_renart 1", "FactLearned fact.ch2.dette.bourse" }, Choose(w, 0));
        Assert.Contains($"StepCompleted {Dette}/renart", Ticks(w));
        Say(w, "npc.renart", "ch3_ancienne");   // V1.9.3 : chapitre 2 fini avant le chapitre 3, Renart l'envoie au hameau
        // Rapportée à Mathurin.
        w = Go(w, Mathurin, "zone.auberge");
        long xp = w.Character.Xp, money = w.Money;
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.aubergiste"));
        List<string> end = Ticks(w);
        Assert.Contains("ItemTaken item.bourse_de_renart 1", end);
        Assert.Contains("TrustChanged npc.aubergiste 1", end);
        Assert.Contains($"QuestCompleted {Dette}", end);
        Assert.Equal((xp + 120, money + 150), (w.Character.Xp, w.Money));
        // Les peaux de loup restent offertes ; l'ardoise, elle, ne l'est plus.
        DialogueView after = Say(w, "npc.aubergiste", "accueil");
        Assert.Contains(after.Options, o => o.Text == "text.aubergiste.accueil.accepter");
        Assert.DoesNotContain(after.Options, o => o.Text == "text.ch2.aubergiste.accueil.ardoise");
        // Les peaux finies aussi : le merci de la dette.
        Say(OpenIn("zone.auberge", Mathurin, Hero(5, Done(AfterChapter2, Bourg, Dette, Peaux))), "npc.aubergiste", "ch2_dette_apres");
    }

    [Fact]
    public void Les_cloches_de_la_chapelle_de_bout_en_bout()
    {
        WorldSession w = OpenIn("zone.chapelle", Pretre, Hero(5, Done(AfterChapter2, Bourg, Bete)));
        Say(w, "npc.pretre", "ch2_cloches");
        Assert.Contains($"QuestStarted {Cloches}/corde", Choose(w, 0));
        Say(w, "npc.pretre", "ch2_cloches_attente");
        // Bertille donne la corde.
        w = Go(w, Epiciere);
        Say(w, "npc.epiciere", "ch2_corde");
        Assert.Contains("ItemGiven item.corde_de_cloche 1", Choose(w, 0));
        Assert.Contains($"StepCompleted {Cloches}/corde", Ticks(w));
        Say(w, "npc.epiciere", "accueil");
        // Livrée au frère.
        w = Go(w, Pretre, "zone.chapelle");
        long xp = w.Character.Xp, money = w.Money;
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.pretre"));
        List<string> end = Ticks(w);
        Assert.Contains("TrustChanged npc.pretre 1", end);
        Assert.Contains($"QuestCompleted {Cloches}", end);
        Assert.Equal((xp + 110, money + 100), (w.Character.Xp, w.Money));
        Say(w, "npc.pretre", "ch2_cloches_apres");
    }

    [Fact]
    public void La_derniere_lanterne_de_bout_en_bout()
    {
        WorldSession w = OpenIn("zone.maison", Rosalie, Hero(5, null));
        Say(w, "npc.habitant", "ch2_lanterne");
        Assert.Contains($"QuestStarted {Lanterne}/huile", Choose(w, 0));
        Say(w, "npc.habitant", "ch2_lanterne_attente");
        w = Go(w, Herboriste);
        Say(w, "npc.herboriste", "ch2_huile");
        Assert.Contains("ItemGiven item.huile_de_lampe 1", Choose(w, 0));
        Assert.Contains($"StepCompleted {Lanterne}/huile", Ticks(w));
        Say(w, "npc.herboriste", "accueil");
        w = Go(w, Rosalie, "zone.maison");
        long xp = w.Character.Xp, money = w.Money;
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.habitant"));
        List<string> end = Ticks(w);
        Assert.Contains("TrustChanged npc.habitant 1", end);
        Assert.Contains($"QuestCompleted {Lanterne}", end);
        Assert.Equal((xp + 120, money + 80), (w.Character.Xp, w.Money));
        Say(w, "npc.habitant", "ch2_lanterne_apres");
    }

    [Fact]
    public void Le_puits_qui_murmure_de_bout_en_bout()
    {
        // De jour : la demande ; la réponse moqueuse commence aussi la quête, mais Lison s'en souvient.
        WorldSession w = Open(Lison, Hero(5, Done(AfterChapter2, Bourg)));
        Say(w, "npc.lison", "ch2_demande");
        List<string> start = Choose(w, 1).ToList();
        Assert.Contains($"QuestStarted {Puits}/ecouter", start);
        Assert.Contains("TrustChanged npc.lison -1", start);
        Say(w, "npc.lison", "ch2_jour");
        // La nuit, le puits dit un nom.
        WorldSave day = w.Save();
        w = Open("zone.vallons", Lison, day.Character!, Nuit, day.Consumed, out _);
        Assert.Equal(DayPhase.Night, w.Zone.Time.Phase);
        Say(w, "npc.lison", "ch2_nuit");
        Assert.Equal(new[] { "FactLearned fact.ch2.puits.ecoute", "Discovered discovery.voix_du_puits" }, Choose(w, 0, "ch2_nom"));
        Choose(w, 0);
        Assert.Contains($"StepCompleted {Puits}/ecouter", Ticks(w));
        // Sœur Albe donne une pierre bénie, une seule fois.
        w = Go(w, Albe, "zone.chapelle");
        Say(w, "npc.albe", "ch2_puits");
        Assert.Contains("ItemGiven item.pierre_benie 1", Choose(w, 0));
        Assert.Contains($"StepCompleted {Puits}/pierre", Ticks(w));
        Say(w, "npc.albe", "ch2_apres");   // niveau 5 : pas encore les herbes (niveau 6)
        // Lison la jette au fond.
        w = Go(w, Lison);
        Say(w, "npc.lison", "ch2_attente");
        long xp = w.Character.Xp, money = w.Money;
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.lison"));
        List<string> end = Ticks(w);
        Assert.Contains($"QuestCompleted {Puits}", end);
        Assert.Equal((xp + 150, money + 80, 0), (w.Character.Xp, w.Money, w.Narrative!.Trust("npc.lison")));
        Say(w, "npc.lison", "ch2_apres");
    }

    [Fact]
    public void L_apprentie_de_Maelle_de_bout_en_bout()
    {
        // Trois minerais dans le sac de départ ; les trois autres sont récoltés aux trois filons de la carrière (récolte réelle).
        var story = Done(new[] { "fact.traces.recit", "fact.moulin.porte_ouverte", "fact.odile.retour" }, Mission, "quest.ferrure_de_rechange");
        WorldSession w = Open(Forge, Hero(6, story, 0, new ItemStack("item.minerai_de_fer", 3)));
        Say(w, "npc.maelle", "ch2_apprentie");
        Assert.Contains($"QuestStarted {Apprentie}/colombe", Choose(w, 0));
        Say(w, "npc.maelle", "ch2_apprentie_attente");
        w = Go(w, Colombe);
        Say(w, "npc.apprentie", "ch2_debut");
        Choose(w, 0);
        Assert.Contains($"StepCompleted {Apprentie}/colombe", Ticks(w));
        Say(w, "npc.apprentie", "ch2_minerai");
        foreach (((double X, double Z) at, string node) in new[] { (FilonOuest, "filon_ouest"), (FilonCentre, "filon_centre"), (FilonEst, "filon_est") })
        {
            w = Go(w, at);
            Assert.Equal(CraftRefusal.None, w.Gather(node, out _));
        }
        Assert.Contains($"StepCompleted {Apprentie}/minerai", Ticks(w));
        // Deux ferrures à l'enclume de Maëlle.
        w = Go(w, Forge);
        Assert.Equal(CraftRefusal.None, w.Craft("recipe.ferrure", out _));
        Assert.DoesNotContain(Ticks(w), e => e.StartsWith("StepCompleted", StringComparison.Ordinal));
        Assert.Equal(CraftRefusal.None, w.Craft("recipe.ferrure", out _));
        Assert.Contains($"StepCompleted {Apprentie}/forger", Ticks(w));
        long xp = w.Character.Xp, money = w.Money;
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.maelle"));
        List<string> end = Ticks(w);
        Assert.Contains("ItemTaken item.ferrure 2", end);
        Assert.Contains("TrustChanged npc.maelle 1", end);
        // Sous-lot V1.9.3b : la quête finit par une épée de garde de qualité supérieure (objectif craftQuality, petit jeu de la forge).
        Assert.Contains($"StepCompleted {Apprentie}/montrer", end);
        Assert.DoesNotContain($"RewardToChoose {Apprentie}", end);
        Say(w, "npc.maelle", "ch2_apprentie_epee");
        w = Go(w, Colombe);
        Say(w, "npc.apprentie", "ch2_epee");
        w = Go(w, Forge);
        // Contournement : le minerai de quelques essais est mis dans le sac (la récolte est jouée plus haut).
        Assert.Equal(InventoryRefusal.None, w.Inventory.Add("item.minerai_de_fer", 21));
        end = new List<string>();
        for (int attempt = 0; attempt < 3 && !end.Contains($"RewardToChoose {Apprentie}"); attempt++)
        {
            Assert.Equal(CraftRefusal.None, w.Craft("recipe.ferrure", out _));
            Assert.Equal(CraftRefusal.None, w.Craft("recipe.epee_de_garde", new ForgeStrikes(100, 100, 100), out CraftEvent? sword));
            List<string> after = Ticks(w);
            // Une épée de qualité normale ne remplit pas l'étape ; supérieure ou chef-d'œuvre, si.
            Assert.Equal(sword!.Quality >= ItemQuality.Superior, after.Contains($"StepCompleted {Apprentie}/epee"));
            end.AddRange(after);
        }
        Assert.Contains($"RewardToChoose {Apprentie}", end);
        Assert.Equal((xp + 220, money + 150), (w.Character.Xp, w.Money));
        Assert.Equal(DialogueRefusal.None, w.ChooseReward(Apprentie, 0, out IReadOnlyList<StoryEvent> chosen));
        Assert.Equal(new[] { "ItemGiven item.casque_de_fer 1", $"QuestCompleted {Apprentie}" }, chosen.Select(e => e.ToString()));
        Say(w, "npc.maelle", "ch2_apprentie_apres");
        w = Go(w, Colombe);
        Say(w, "npc.apprentie", "ch2_apres");
    }

    [Fact]
    public void Herbes_pour_la_chapelle_de_bout_en_bout()
    {
        // Deux herbes dans le sac de départ ; les deux autres sont cueillies au lac et aux vergers (récolte réelle).
        WorldSession w = OpenIn("zone.chapelle", Albe, Hero(6, Done(AfterChapter2, Bourg), 0, new ItemStack("item.herbe_de_l_aube", 2)));
        Say(w, "npc.albe", "ch2_herbes");
        Assert.Contains($"QuestStarted {Herbes}/herbes", Choose(w, 0));
        Assert.Empty(Ticks(w));   // deux herbes : pas assez
        Say(w, "npc.albe", "ch2_herbes_attente");
        w = Go(w, HerbesLac);
        Assert.Equal(CraftRefusal.None, w.Gather("herbes_lac", out _));
        w = Go(w, HerbesVerger);
        Assert.Equal(CraftRefusal.None, w.Gather("herbes_verger", out _));
        Assert.Contains($"StepCompleted {Herbes}/herbes", Ticks(w));
        w = Go(w, Albe, "zone.chapelle");
        long xp = w.Character.Xp, money = w.Money;
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.albe"));
        List<string> end = Ticks(w);
        Assert.Contains("ItemTaken item.herbe_de_l_aube 4", end);
        Assert.Contains($"RewardToChoose {Herbes}", end);
        Assert.Equal((xp + 130, money + 100), (w.Character.Xp, w.Money));
        Say(w, "npc.albe", "ch2_herbes_recompense");
        Assert.Equal(DialogueRefusal.None, w.ChooseReward(Herbes, 1, out IReadOnlyList<StoryEvent> chosen));
        Assert.Equal(new[] { "ItemGiven item.remede_de_l_aube 1", $"QuestCompleted {Herbes}" }, chosen.Select(e => e.ToString()));
        Say(w, "npc.albe", "ch2_apres");
    }

    // --- Leçon de la PR #88 : aucune quête finie (principale ou non) ne masque une offre du même PNJ -------------------

    [Fact]
    public void Offres_de_chaque_donneur_encore_accessibles()
    {
        string[] facts = AfterChapter2.Append("fact.odile.retour").ToArray();
        // Toutes les autres quêtes du donneur finies, la mission et le chapitre 2 compris : l'offre passe avant les mercis.
        Say(OpenIn("zone.auberge", Mathurin, Hero(8, Done(facts, Mission, Bourg, Peaux))), "npc.aubergiste", "ch2_dette");
        Say(OpenIn("zone.chapelle", Pretre, Hero(8, Done(facts, Mission, Bourg, Bete))), "npc.pretre", "ch2_cloches");
        Say(OpenIn("zone.maison", Rosalie, Hero(8, Done(facts, Mission, Bourg, Bete))), "npc.habitant", "ch2_lanterne");
        Say(Open(Lison, Hero(8, Done(facts, Mission, Bourg))), "npc.lison", "ch2_demande");
        Say(Open(Forge, Hero(8, Done(facts, Mission, Bourg, "quest.ferrure_de_rechange", "quest.pain_du_matin"))), "npc.maelle", "ch2_apprentie");
        Say(OpenIn("zone.chapelle", Albe, Hero(8, Done(facts, Mission, Bourg, Puits))), "npc.albe", "ch2_herbes");
        // Les nouvelles quêtes finies ne masquent pas non plus les anciennes offres (peaux de loup, loup blanc).
        Say(OpenIn("zone.auberge", Mathurin, Hero(8, Done(facts, Bourg, Dette))), "npc.aubergiste", "accueil");
        Say(OpenIn("zone.chapelle", Pretre, Hero(8, Done(facts, Bourg, Cloches))), "npc.pretre", "demande");
        // Une ancienne quête en cours ou refusée laisse une réponse vers la nouvelle offre.
        AssertLeadsTo(OpenIn("zone.auberge", Mathurin, Hero(8, With(facts, (Peaux, "chasse")))), "npc.aubergiste", "en_cours", "text.ch2.aubergiste.accueil.ardoise", "ch2_dette");
        AssertLeadsTo(OpenIn("zone.auberge", Mathurin, Hero(8, With(facts, (Peaux, "peaux")))), "npc.aubergiste", "peaux", "text.ch2.aubergiste.accueil.ardoise", "ch2_dette");
        AssertLeadsTo(OpenIn("zone.chapelle", Pretre, Hero(8, null)), "npc.pretre", "demande", "text.ch2.pretre.demande.cloches", "ch2_cloches");
        AssertLeadsTo(OpenIn("zone.chapelle", Pretre, Hero(8, With(facts, (Bete, "chasse")))), "npc.pretre", "en_cours", "text.ch2.pretre.demande.cloches", "ch2_cloches");
        // Sœur Albe pendant les longues étapes du chapitre 2 (la bourse à rapporter, l'inscription) : les herbes restent demandables.
        string[] met = { "fact.ch1.vers_le_bourg", "fact.ch2.albe.rencontree" };
        AssertLeadsTo(OpenIn("zone.chapelle", Albe, Hero(8, With(met, (Bourg, "retour")))), "npc.albe", "ch2_bourse", "text.ch2.albe.autre_chose", "ch2_herbes");
        AssertLeadsTo(OpenIn("zone.chapelle", Albe, Hero(8, With(met, (Bourg, "guilde")))), "npc.albe", "ch2_guilde", "text.ch2.albe.autre_chose", "ch2_herbes");
        // Les anciennes étapes gardent leur nœud (sauvegardes d'avant V1.9.2) ; sous le niveau 5, rien de nouveau n'est offert.
        Say(OpenIn("zone.maison", Rosalie, Hero(8, With(facts, (Bete, "temoin")))), "npc.habitant", "temoin");
        Assert.DoesNotContain(Say(OpenIn("zone.auberge", Mathurin, Hero(4, null)), "npc.aubergiste", "accueil").Options,
            o => o.Text == "text.ch2.aubergiste.accueil.ardoise");
        Say(OpenIn("zone.maison", Rosalie, Hero(4, null)), "npc.habitant", "accueil");
        Say(Open(Lison, Hero(4, null)), "npc.lison", "ch2_accueil");
    }

    /// <summary>Le PNJ ouvre sur <paramref name="node"/>, dont la réponse <paramref name="text"/> mène à <paramref name="next"/>.</summary>
    private static void AssertLeadsTo(WorldSession w, string npc, string node, string text, string next)
    {
        DialogueView view = Say(w, npc, node);
        Choose(w, Assert.Single(view.Options, o => o.Text == text).Index, next);
    }
}
