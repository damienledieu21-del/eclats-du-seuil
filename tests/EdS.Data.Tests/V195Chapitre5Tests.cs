using System.Text.Json.Nodes;
using EdS.Data.Crafting;
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
/// Sous-lot V1.9.5, chapitre 5 « Le Seuil » (docs/MONDE.md, docs/ECRITURE.md) : la quête principale du chapitre (la manifestation, le rôdeur
/// du Seuil, le Gardien et sa forme éveillée, le retour chez Odile, la voix de l'Envers qui murmure « Celui-qui-Attend »), ses deux quêtes
/// secondaires, la fin de l'ancienne mission passée du chapitre 1 au chapitre 5 et la conversion des sauvegardes. Chaque quête est jouée de
/// bout en bout par un pilote scripté (dialogues, réponses, lieux atteints, livraisons ; la partie est sauvegardée, relue et rouverte à chaque
/// déplacement, au même instant du monde), ainsi qu'une partie entière du chapitre 1 au chapitre 5. Contournements, dits là où ils ont lieu :
/// les victoires sur les créatures (loups, rôdeur, bandits, archer, loup blanc) sont annoncées à l'histoire sans livrer le combat, et celles
/// sur les chefs par leur fait (<c>fact.chef.vaincu</c>, <c>fact.chef_des_bandits.vaincu</c>), comme le fait la partie à la fin du combat
/// de chef ; le minerai de la ferrure est dans le sac de départ ; les combats et la récolte ont leurs tests.
/// </summary>
[Trait("Category", "World")]
public class V195Chapitre5Tests
{
    private const string Ch1 = "quest.traces_sous_le_moulin", Bourg = "quest.bourg_d_aube", Hameau = "quest.champs_et_hameau", Tour = "quest.tour_et_camp",
        Main = "quest.le_seuil", Loup = "quest.le_loup_blanc", Pierres = "quest.pierres_gravees", Bete = "quest.bete_blanche", Herbes = "quest.herbes_pour_la_chapelle",
        Brunot = "quest.le_vieux_brunot";   // sous-lot V2.9.2 : faite, pour que Gaspard ne la propose pas à la place de « garde »

    // Où se tenir (à 1 à 2 m du PNJ, hors de tout obstacle) : dans les Vallons, puis dans les intérieurs.
    private static readonly (double X, double Z) Odile = (-9, 2), Moulin = (-8, 5), Forge = (-19, -5), Pre = (-10, 30), Gaspard = (26, -13),
        Renart = (-47.5, -3.3), Gue = (12, 0.5), Josse = (84, -37.5), Lac = (-12.5, -62), Thibaut = (118, 54.5),
        Tour0 = (15, 77), Camp0 = (90, 84), Seuil0 = (46, 38), Ysarn = (66, 48.5), Ruines0 = (22, -12), Foret0 = (-20, 60), Plaine0 = (75, -50);
    private static readonly (double X, double Z) Albe = (3.2, 3.2), Pretre = (-2, 3.6), Mathurin = (3.75, 1.6), Rosalie = (-1.8, -2.4), Maitresse = (0, 1.0);

    private static readonly Lazy<ZoneDefinition> Vallons = new(() => Zone());
    private static readonly Lazy<StoryBook> Story = new(() => Book(Vallons.Value));
    private static readonly Lazy<CraftingBook> Crafts = new(() =>
        CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory), Texts, Vallons.Value, ItemsCatalog).Book!);
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

    /// <summary>Partie ouverte dans <paramref name="zone"/> au point <paramref name="at"/>, à l'instant <paramref name="tick"/> ; l'arrivée et trois ticks joués.</summary>
    private static WorldSession Open(string zone, (double X, double Z) at, SavedCharacter hero, long tick, IEnumerable<string>? consumed, out List<string> events)
    {
        bool outside = zone == "zone.vallons";
        ZoneDefinition definition = outside ? Zone(at.X, at.Z) : Room(zone, at);
        var session = new WorldSession(new WorldSave(zone, tick, 0, (consumed ?? Array.Empty<string>()).Append(PlayerCharacters.PlacesKey)
            .Distinct(StringComparer.Ordinal), hero), definition, ShippedData, Progression, ItemsCatalog, Story.Value, outside ? Crafts.Value : null,
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
        // Sous-lot V2.7 : la réputation (quêtes finies, créatures vaincues, contrats) a ses propres tests (V27ReputationTests).
        for (int i = 0; i < ticks; i++) events.AddRange(w.Step().Story.Where(e => e.Kind != StoryEventKind.ReputationChanged && e.Kind != StoryEventKind.StandingChanged).Select(e => e.ToString()));
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

    /// <summary>Le chapitre 4 fini (Mâchefer livré au capitaine).</summary>
    private static readonly string[] AfterChapter4 = { "fact.traces.recit", "fact.moulin.porte_ouverte", "fact.ch1.vers_le_bourg", "fact.ch2.albe.rencontree",
        "fact.ch2.vers_les_champs", "fact.ch3.hameau.raid", "fact.ch3.hameau.defendu", "fact.ch3.vers_la_tour", "fact.ch4.chef.decide", "fact.ch4.chef.livre",
        "fact.ch4.albe.sans_repos", "fact.ch4.vers_le_seuil" };

    private static readonly string[] Chapters1To4 = { Ch1, Bourg, Hameau, Tour };

    private static bool Inside(Place p, GroundVector at) => p.Min.X <= at.X && at.X <= p.Max.X && p.Min.Z <= at.Z && at.Z <= p.Max.Z;

    private static bool InObstacle(double x, double z, double margin) => Vallons.Value.Setup.Space!.Obstacles.Any(o =>
        o.Points.Min(p => p.X.Raw) / 10000.0 - margin < x && x < o.Points.Max(p => p.X.Raw) / 10000.0 + margin
        && o.Points.Min(p => p.Z.Raw) / 10000.0 - margin < z && z < o.Points.Max(p => p.Z.Raw) / 10000.0 + margin);

    // --- Données -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Chapitre_5_et_ses_quetes_livres()
    {
        StoryBook book = Story.Value;
        Assert.Equal(new[] { Main, Loup, Pierres }, book.Quests.Skip(27).Take(3).Select(q => q.Id));   // ajoutées à la fin (V1.9.6 : l'ouverture vient après)
        Assert.Equal(QuestCategory.Main, book.Quest(Main).Category);
        Assert.Empty(book.Quest(Main).Requires);   // commencée par la fin du chapitre 4 : jamais fermée
        // Choix de Damien du 5 novembre 2026 : les quatre dernières étapes de l'ancienne mission sont au chapitre 5, sous les mêmes
        // identifiants et les mêmes objectifs ; le chapitre 1 finit chez Gaspard, sans récompense finale (ses 300 points et 5 argent sont
        // dans celle du chapitre 5).
        Assert.Equal(new[] { "manifestation", "rodeur", "chef", "retour", "voix", "albe" }, book.Quest(Main).Steps.Select(s => s.Id));
        Assert.Equal(StoryConversions.MovedSteps, book.Quest(Main).Steps.Take(4).Select(s => s.Id));
        Assert.True(StoryConversions.Applies(book));
        Assert.Equal("gaspard", book.Quest(Ch1).Steps.Last().Id);
        Assert.Empty(book.Quest(Ch1).Effects);
        Assert.Equal(new[] { (ObjectiveKind.Reach, "seuil"), (ObjectiveKind.Defeat, "rodeur"), (ObjectiveKind.Fact, "fact.chef.vaincu"), (ObjectiveKind.Fact, "fact.odile.retour"),
            (ObjectiveKind.Reach, "moulin"), (ObjectiveKind.Fact, "fact.ch5.albe.voix") }, book.Quest(Main).Steps.Select(s => (s.Objective.Kind, s.Objective.Id)));
        // La fin du chapitre 4 commence le chapitre 5.
        Assert.Contains(book.Quest(Tour).Steps.Single(s => s.Id == "albe").Effects, e => e.Kind == EffectKind.StartQuest && e.Id == Main);
        // Niveaux 20 à 25 : le rôdeur au niveau 21 (le Seuil), le Gardien au moins au niveau 23 (le cœur du Seuil) ; au niveau 25 du joueur,
        // le Gardien est au 23, comme le combat d'équilibrage le demande (V18EquilibreTests).
        ZoneDefinition zone = Vallons.Value;
        Assert.Equal(21, zone.OwnMinLevel("rodeur", zone.PopulationOf("rodeur")!.Template.Placement!.Position));
        Assert.Equal("seuil", zone.Places.Last(pl => Inside(pl, zone.PopulationOf("rodeur")!.Template.Placement!.Position)).Id);
        foreach (var form in zone.Bosses.Single(b => b.Id == "gardien_du_seuil").Forms)
        {
            Assert.Equal(23, zone.MinLevelAt(form.Placement!.Position));
            Assert.Equal(23, zone.Scaling!.CreatureLevel(23, 21));
            Assert.Equal(23, zone.Scaling.CreatureLevel(23, 25));
        }
        // Les quêtes secondaires s'ouvrent au niveau 21 ; « Le loup blanc » après « La bête blanche » (le même loup blanc qui revient : pas de
        // doublon de créature ni de quête) ; leurs récompenses se portent au plus deux niveaux après.
        Assert.Equal(new[] { 21L }, book.Quest(Pierres).Requires.Select(r => r.Value));
        Assert.Contains(book.Quest(Loup).Requires, r => r.Kind == ConditionKind.QuestDone && r.Id == Bete);
        Assert.Equal((ObjectiveKind.Defeat, "loup_blanc"), (book.Quest(Loup).Steps[1].Objective.Kind, book.Quest(Loup).Steps[1].Objective.Id));
        foreach (string q in new[] { Loup, Pierres })
        {
            Assert.Equal(QuestCategory.Side, book.Quest(q).Category);
            foreach (RewardChoice c in book.Quest(q).RewardChoices) Assert.True(ItemsCatalog.Item(c.Item).RequiredLevel <= 23, $"{q} : {c.Item}");
        }
        // « Les pierres gravées » : l'objet existant (une relique, rare, niveau 10) est la pierre du grand-père de Gaspard, donnée à la fin.
        Assert.Equal(("relic", 10), (ItemsCatalog.Item("item.pierre_gravee").Slot!, ItemsCatalog.Item("item.pierre_gravee").RequiredLevel));
        Assert.Contains(book.Quest(Pierres).Effects, e => e.Kind == EffectKind.GiveItem && e.Id == "item.pierre_gravee");
        Assert.Equal(new[] { "ruines", "lac", "foret_des_loups", "plaine_est" }, book.Quest(Pierres).Steps.Take(4).Select(s => s.Objective.Id));
        Assert.Equal(new[] { "npc.ysarn" }, book.Npcs.Skip(26).Take(1).Select(n => n.Id));   // V1.9.6 : la reine Ysolde vient après
        foreach (string d in new[] { "discovery.celui_qui_attend", "discovery.traces_du_loup_blanc", "discovery.pierre_gravee_ruines", "discovery.pierre_gravee_lac",
            "discovery.pierre_gravee_foret", "discovery.pierre_gravee_plaine", "discovery.lecture_des_pierres" })
            Assert.Contains(book.Discoveries, x => x.Id == d);
        // Sœur Albe : douze entrées (les quatre des anciennes étapes sont parties) ; le chapitre 5 passe par des réponses de ses nœuds.
        Assert.Equal(19, book.Dialogue("dialogue.albe").Entries.Count);   // V2.6.1 : la quête des cicatrices (six) ; V2.6 : les échos rôdeurs
        Assert.DoesNotContain(book.Dialogue("dialogue.albe").Entries, e => e.Conditions.Any(c => c.Id == Main));
        Assert.All(book.Dialogues, d => Assert.True(d.Entries.Count <= StoryValidator.MaxEntries, d.Id));
        // Récompenses (proposées) : 3 400 points et 21 argent 50 pour le chapitre ; 2 600 points pour les deux quêtes secondaires. Du niveau
        // 20 au 21 sans les quêtes secondaires ni les combats ; avec elles, au 23 ; les combats du Seuil mènent au 25.
        Assert.Equal((21, 23), (Progression.LevelFor(XpForLevel(20) + 3400), Progression.LevelFor(XpForLevel(20) + 3400 + 2600)));
    }

    [Fact]
    public void Ysarn_a_portee_depuis_une_place_libre_loin_des_creatures()
    {
        Npc n = Story.Value.Npc("npc.ysarn");
        Assert.Null(n.Zone);
        double nx = n.Position.X.Raw / 10000.0, nz = n.Position.Z.Raw / 10000.0;
        Assert.Equal("hautes_pierres", Vallons.Value.Places.Last(pl => Inside(pl, n.Position)).Id);
        Assert.True(Math.Sqrt((nx - Ysarn.X) * (nx - Ysarn.X) + (nz - Ysarn.Z) * (nz - Ysarn.Z)) < 3);
        foreach ((double px, double pz) in new[] { Ysarn, (nx, nz) }) Assert.False(InObstacle(px, pz, 0.5), $"{px}, {pz}");
        // Aucune créature ni forme du Gardien ne guette Ysarn : chacune à plus de 14 m (13 m au plus d'aggro, l'archer).
        IEnumerable<GroundVector> creatures = Vallons.Value.Populations.Select(p => p.Template.Placement!.Position)
            .Concat(Vallons.Value.Bosses.SelectMany(b => b.Forms).Select(f => f.Placement!.Position));
        foreach (GroundVector c in creatures)
        {
            double cx = c.X.Raw / 10000.0 - nx, cz = c.Z.Raw / 10000.0 - nz;
            Assert.True(Math.Sqrt(cx * cx + cz * cz) > 14, $"{cx}, {cz}");
        }
        // Les points où le pilote se tient (le Seuil, les quatre pierres) sont libres, et dans le bon lieu.
        foreach (((double x, double z) at, string place) in new[] { (Seuil0, "seuil"), (Ruines0, "ruines"), (Foret0, "foret_des_loups"), (Plaine0, "plaine_est"), (Lac, "lac") })
        {
            Assert.False(InObstacle(at.x, at.z, 0.5), place);
            Assert.Contains(Vallons.Value.Places, pl => pl.Id == place && pl.Min.X.Raw <= at.x * 10000 && at.x * 10000 <= pl.Max.X.Raw
                && pl.Min.Z.Raw <= at.z * 10000 && at.z * 10000 <= pl.Max.Z.Raw);
        }
    }

    // --- Pilote : la quête principale du chapitre 5 ---------------------------------------------------------------------

    [Fact]
    public void Chapitre_5_joue_de_bout_en_bout()
    {
        // Fin du chapitre 4 (Sœur Albe et le Sans-Repos) : la fin du chapitre 4 commence le chapitre 5. Les herbes de Sœur Albe pas encore
        // cueillies : son offre passe d'abord, les chapitres 4 et 5 par des réponses.
        var albe = new SavedStory(AfterChapter4.Take(10), new Dictionary<string, QuestProgress>
        {
            [Ch1] = new QuestProgress(QuestProgress.Done), [Bourg] = new QuestProgress(QuestProgress.Done), [Hameau] = new QuestProgress(QuestProgress.Done),
            [Tour] = new QuestProgress("albe"),
        }, new Dictionary<string, int> { ["npc.odile"] = 1 });
        WorldSession w = OpenIn("zone.chapelle", Albe, Hero(20, albe, 500));
        Assert.Equal(DialogueRefusal.None, w.Choose(Assert.Single(Say(w, "npc.albe", "ch2_herbes").Options, o => o.Text == "text.ch4.albe.vers_sans_repos").Index, out _, out _));
        Choose(w, 0, "ch4_livre");
        Choose(w, 0);
        List<string> start = Ticks(w);
        Assert.Contains($"QuestCompleted {Tour}", start);
        Assert.Contains($"QuestStarted {Main}/manifestation", start);
        long xp0 = w.Character.Xp, money0 = w.Money;
        // La manifestation, entre les pierres dressées (lieu atteint).
        w = Go(w, Seuil0, "zone.vallons", out List<string> seuil);
        seuil.AddRange(Ticks(w));
        Assert.Contains($"StepCompleted {Main}/manifestation", seuil);
        Assert.Contains("Discovered discovery.manifestation", seuil);
        // Gaspard sait comment éviter l'onde du rôdeur.
        w = Go(w, Gaspard);
        Say(w, "npc.gaspard", "rodeur");
        Assert.Contains("TrustChanged npc.gaspard 1", Choose(w, 0));
        // Le rôdeur, puis le Gardien : victoires annoncées à l'histoire (le rôdeur comme à la mort d'une créature, le Gardien par son fait,
        // comme le fait la partie à la fin du combat de chef) ; un autre rôdeur ne compte pas.
        w.Narrative!.Defeated("rodeur2", null);
        Assert.Empty(Ticks(w, 1));
        w.Narrative.Defeated("rodeur", null);
        Assert.Contains($"StepCompleted {Main}/rodeur", Ticks(w, 1));
        Assert.True(w.Narrative.Learn("fact.chef.vaincu", new List<StoryEvent>()));
        List<string> chef = Ticks(w);
        Assert.Contains($"StepCompleted {Main}/chef", chef);
        Assert.Contains("Discovered discovery.gardien", chef);
        // Retour chez Odile, qui avoue les voix de la porte.
        w = Go(w, Odile);
        Say(w, "npc.odile", "retour");
        Assert.Contains("ItemGiven item.amulette_de_l_aube 1", Choose(w, 0, "ch5_aveu"));
        Choose(w, 0);
        Assert.Contains($"StepCompleted {Main}/retour", Ticks(w));
        Say(w, "npc.odile", "ch1_ferrure");   // son offre passe d'abord (leçon de la PR #88)
        // La voix de l'Envers, derrière la vieille porte du moulin (lieu atteint).
        w = Go(w, Moulin, "zone.vallons", out List<string> porte);
        porte.AddRange(Ticks(w));
        Assert.Contains($"StepCompleted {Main}/voix", porte);
        Assert.Contains("Discovered discovery.celui_qui_attend", porte);
        // Sœur Albe ne connaît pas Celui-qui-Attend (ce n'est pas le Sans-Repos). Fin du chapitre 5, et des Vallons.
        w = Go(w, Albe, "zone.chapelle");
        LeadsTo(w, "npc.albe", "ch2_herbes", "text.ch5.albe.vers_voix", "ch5_voix");
        Assert.Contains("FactLearned fact.ch5.albe.voix", Choose(w, 0, "ch5_suite"));
        Choose(w, 0);
        List<string> end = Ticks(w);
        Assert.Contains($"QuestCompleted {Main}", end);
        Assert.True(w.Narrative!.Knows("fact.ch5.vers_la_suite"));
        Assert.Equal((xp0 + 3400, money0 + 2150), (w.Character.Xp, w.Money));
        // Ensuite : chez Odile, le merci ; chez Albe, son mot d'après le chapitre 5 (par celui du chapitre 4) ; chez Ysarn, le sien.
        w = Go(w, Odile);
        Say(w, "npc.odile", "ch1_ferrure");
        Say(Open(Odile, Hero(25, Done(AfterChapter4.Append("fact.odile.retour"), Chapters1To4.Append(Main).Append("quest.ferrure_de_rechange").Append("quest.pont_des_vallons").ToArray()))), "npc.odile", "apres");
        var after = Done(AfterChapter4.Append("fact.ch5.albe.voix"), Chapters1To4.Append(Main).Append(Herbes).ToArray());
        WorldSession apres = OpenIn("zone.chapelle", Albe, Hero(25, after));
        LeadsTo(apres, "npc.albe", "ch2_apres", "text.ch4.albe.vers_apres", "ch4_apres");
        Choose(apres, 0, "ch5_apres");
        Say(Open(Ysarn, Hero(25, after)), "npc.ysarn", "ch5_apres");
    }

    [Fact]
    public void Partie_de_bout_en_bout_du_chapitre_1_au_chapitre_5()
    {
        // Une seule partie, du premier mot d'Odile à la voix de l'Envers : les cinq quêtes principales dans l'ordre, chacune commencée par
        // la fin de la précédente ; la partie est sauvegardée, relue et rouverte à chaque déplacement.
        WorldSession w = Open(Odile, Hero(1, null, 0, new ItemStack("item.minerai_de_fer", 3)));
        // Chapitre 1 : les traces sous le moulin.
        Say(w, "npc.odile", "accueil");
        Choose(w, 0, "recit");
        Assert.Contains($"QuestStarted {Ch1}/enquete", Choose(w, 0));
        Ticks(w);
        w = Go(w, Moulin);
        w = Go(w, Forge);
        Assert.Equal(CraftRefusal.None, w.Craft("recipe.ferrure", out _));
        Say(w, "npc.maelle", "ferrure");
        Choose(w, 0, "faite");
        Ticks(w);
        w = Go(w, Pre);
        foreach (string loup in new[] { "loup1", "loup2", "loup3" }) w.Narrative!.Defeated(loup, "loup");
        Assert.Contains($"StepCompleted {Ch1}/meute", Ticks(w, 1));
        // Sous-lot V1.9.6 : le souvenir passé (son texte, le repli) ; Croc-Gris, sa victoire annoncée par son fait (V196OuvertureTests joue
        // la scène et le combat).
        Assert.True(w.SkipScene("scene.souvenir_de_la_rupture"));
        Assert.True(w.Narrative!.Learn("fact.croc_gris.vaincu", new List<StoryEvent>()));
        Assert.Contains($"StepCompleted {Ch1}/croc_gris", Ticks(w, 1));
        w = Go(w, Gaspard);
        Say(w, "npc.gaspard", "ch1_souvenir");
        Choose(w, 1);
        List<string> ch1 = Ticks(w);
        Assert.Contains($"QuestCompleted {Ch1}", ch1);
        Assert.Contains($"QuestStarted {Bourg}/chapelle", ch1);
        // Chapitre 2 : le bourg d'Aube.
        w = Go(w, Albe, "zone.chapelle");
        Say(w, "npc.albe", "ch2_eclat");
        Choose(w, 0, "ch2_vols");
        Choose(w, 0);
        Ticks(w);
        w = Go(w, Mathurin, "zone.auberge");
        Say(w, "npc.aubergiste", "ch2_vol");
        Choose(w, 1, "ch2_vol_vin");
        Choose(w, 0);
        Ticks(w);
        w = Go(w, Renart);
        Say(w, "npc.renart", "ch2_piste");
        Choose(w, 0, "ch2_ruines");
        Choose(w, 0);
        Ticks(w);
        w = Go(w, Gue);
        w.Narrative!.Defeated("archer", "archer");
        Ticks(w, 1);
        w = Go(w, Albe, "zone.chapelle");
        Say(w, "npc.albe", "ch2_bourse");
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.albe"));
        Ticks(w);
        w = Go(w, Maitresse, "zone.guilde");
        Say(w, "npc.maitresse_guilde", "ch2_albe");
        Choose(w, 0, "guilde");
        Assert.Equal(GuildRefusal.None, w.JoinGuild(out _));
        Ticks(w);
        w = Go(w, Renart);
        Say(w, "npc.renart", "ch2_aveu");
        Choose(w, 1, "ch2_aveu_fin");
        Choose(w, 0);
        List<string> ch2 = Ticks(w);
        Assert.Contains($"QuestCompleted {Bourg}", ch2);
        Assert.Contains($"QuestStarted {Hameau}/hameau", ch2);
        // Chapitre 3 : les champs et le hameau.
        w = Go(w, Josse);
        Say(w, "npc.doyen", "ch3_raid");
        Choose(w, 0, "ch3_raid_qui");
        Choose(w, 0);
        Ticks(w);
        w.Narrative!.Defeated("bandit_epee3", "bandit");
        Ticks(w, 1);
        w = Go(w, Lac);
        w.Narrative!.Defeated("bandit_epee4", "bandit");
        Ticks(w, 1);
        w = Go(w, Josse);
        Say(w, "npc.doyen", "ch3_sac");
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.doyen"));
        Ticks(w);
        w = Go(w, Albe, "zone.chapelle");
        Say(w, "npc.albe", "ch3_ordre");
        Choose(w, 0, "ch3_tour");
        Choose(w, 0);
        List<string> ch3 = Ticks(w);
        Assert.Contains($"QuestCompleted {Hameau}", ch3);
        Assert.Contains($"QuestStarted {Tour}/rosalie", ch3);
        // Chapitre 4 : la tour et le camp (Mâchefer livré au capitaine).
        w = Go(w, Rosalie, "zone.maison");
        Say(w, "npc.habitant", "ch4_rosalie");
        Choose(w, 0);
        Ticks(w);
        w = Go(w, Tour0);
        w = Go(w, Camp0);
        Ticks(w);
        Assert.True(w.Narrative!.Learn("fact.chef_des_bandits.vaincu", new List<StoryEvent>()));
        Ticks(w);
        w = Go(w, Thibaut);
        Say(w, "npc.thibaut", "ch4_chef");
        Choose(w, 0, "ch4_livre");
        Choose(w, 0);
        Ticks(w);
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.thibaut"));
        Ticks(w);
        w = Go(w, Rosalie, "zone.maison");
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.habitant"));
        Ticks(w);
        w = Go(w, Albe, "zone.chapelle");
        DialogueView herbes = Say(w, "npc.albe", "ch2_herbes");
        Assert.Equal(DialogueRefusal.None, w.Choose(Assert.Single(herbes.Options, o => o.Text == "text.ch4.albe.vers_sans_repos").Index, out _, out _));
        Choose(w, 0, "ch4_livre");
        Choose(w, 0);
        List<string> ch4 = Ticks(w);
        Assert.Contains($"QuestCompleted {Tour}", ch4);
        Assert.Contains($"QuestStarted {Main}/manifestation", ch4);
        // Chapitre 5 : le Seuil.
        w = Go(w, Seuil0);
        Ticks(w);
        w.Narrative!.Defeated("rodeur", null);
        Ticks(w, 1);
        Assert.True(w.Narrative.Learn("fact.chef.vaincu", new List<StoryEvent>()));
        Ticks(w);
        w = Go(w, Odile);
        Say(w, "npc.odile", "retour");
        Choose(w, 0, "ch5_aveu");
        Choose(w, 0);
        Ticks(w);
        w = Go(w, Moulin);
        Ticks(w);
        w = Go(w, Albe, "zone.chapelle");
        LeadsTo(w, "npc.albe", "ch2_herbes", "text.ch5.albe.vers_voix", "ch5_voix");
        Choose(w, 0, "ch5_suite");
        Choose(w, 0);
        Assert.Contains($"QuestCompleted {Main}", Ticks(w));
        // Les cinq chapitres finis, rien d'autre de principal en cours ; la partie relue est la même.
        Assert.All(Chapters1To4.Append(Main), q => Assert.True(w.Narrative!.IsDone(q), q));
        Assert.DoesNotContain(Story.Value.Quests.Where(q => q.Category == QuestCategory.Main), q => w.Narrative!.IsActive(q.Id));
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!;
        Assert.Equal(18, SaveValidator.CurrentSchemaVersion);   // V2.26 : 18 (registre purgé, secteurs)   /* V2.7 : 15 (réputation) ; V2.6 : 14 (cicatrices) ; V2.5 : 13 (registre des événements) */   // la structure de la sauvegarde ne change pas
        Assert.Equal(new QuestProgress(QuestProgress.Done), saved.Character!.Story.Quests[Main]);
    }

    // --- Conversion des sauvegardes (la fin de l'ancienne mission passe au chapitre 5) ---------------------------------

    [Theory]
    [InlineData("enquete")]
    [InlineData("traces")]
    [InlineData("passage")]
    public void Ancienne_partie_avant_les_etapes_passees_n_est_pas_convertie(string step)
    {
        // V1.10.1 : une partie arrêtée au chapitre 1 avant les quatre étapes passées reste telle quelle (même objet, quête suivie comprise) ;
        // le chapitre 5 n'est pas commencé et aucun fait n'est ajouté.
        Assert.True(StoryConversions.Applies(Story.Value));
        Assert.True(Story.Value.Quest(Ch1).StepIndex(step) >= 0);   // l'étape existe vraiment au chapitre 1
        var old = new SavedStory(new[] { "fact.traces.recit" }, new Dictionary<string, QuestProgress> { [Ch1] = new QuestProgress(step) },
            new Dictionary<string, int> { ["npc.odile"] = 1 }, new string[0], tracked: Ch1);
        SavedStory converted = StoryConversions.Upgrade(Story.Value, old);
        Assert.Same(old, converted);
        Assert.Equal(step, converted.Quests[Ch1].Step);
        Assert.DoesNotContain(Main, converted.Quests.Keys);
        Assert.Equal(Ch1, converted.Tracked);
        Assert.DoesNotContain(StoryConversions.NextFact, converted.Facts);
    }

    [Fact]
    public void Suivi_de_quete_d_une_autre_quete_n_est_pas_deplace_par_la_conversion()
    {
        // Une partie à l'étape « retour » qui suivait une autre quête garde ce suivi ; seul le suivi du chapitre 1 passe au chapitre 5.
        string other = Story.Value.Quests.First(q => q.Id != Ch1 && q.Id != Main).Id;
        var old = new SavedStory(new[] { "fact.traces.recit", "fact.moulin.porte_ouverte" },
            new Dictionary<string, QuestProgress> { [Ch1] = new QuestProgress("retour") }, new Dictionary<string, int>(), new string[0], tracked: other);
        SavedStory converted = StoryConversions.Upgrade(Story.Value, old);
        Assert.Equal(other, converted.Tracked);
        Assert.Equal("retour", converted.Quests[Main].Step);
        Assert.Same(converted, StoryConversions.Upgrade(Story.Value, converted));
    }

    [Theory]
    [InlineData("manifestation")]
    [InlineData("rodeur")]
    [InlineData("chef")]
    [InlineData("retour")]
    public void Ancienne_partie_arretee_a_une_etape_passee_reprend_au_chapitre_5(string step)
    {
        // Une sauvegarde d'avant V1.9.5 (version 12, même structure), suivie à cette étape de l'ancienne mission : à la lecture, le chapitre 1
        // est fini et le chapitre 5 reprend à la même étape, suivi ; rien n'est donné, ni à l'ouverture ni aux ticks suivants.
        var old = new SavedStory(new[] { "fact.traces.recit", "fact.moulin.porte_ouverte", "fact.manifestation.vue" },
            new Dictionary<string, QuestProgress> { [Ch1] = new QuestProgress(step) }, new Dictionary<string, int> { ["npc.odile"] = 1 },
            new[] { "discovery.manifestation" }, tracked: Ch1);
        SavedStory converted = StoryConversions.Upgrade(Story.Value, old);
        Assert.Equal(new[] { Main, Ch1 }, converted.Quests.Keys);   // ordre ordinal
        Assert.Equal((QuestProgress.Done, step, Main), (converted.Quests[Ch1].Step, converted.Quests[Main].Step, converted.Tracked));
        Assert.Equal(old.Facts, converted.Facts);
        Assert.Equal(old.Trust, converted.Trust);
        Assert.Equal(old.Discoveries, converted.Discoveries);
        Assert.Same(converted, StoryConversions.Upgrade(Story.Value, converted));   // idempotente
        SavedCharacter hero = Hero(20, old, 300);
        WorldSession w = Open(Gaspard, hero);
        Assert.Empty(Ticks(w, 5));
        Assert.Equal((hero.Xp, 300L), (w.Character.Xp, w.Money));
        Assert.Equal((true, step, Main), (w.Narrative!.IsDone(Ch1), w.Narrative.CurrentStep(Main)!.Id, w.Narrative.Tracked));
        // La sauvegarde écrite ensuite porte l'état converti (toujours en version 12).
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!;
        Assert.Equal(converted.Quests, saved.Character!.Story.Quests);
    }

    [Fact]
    public void Ancienne_partie_arretee_au_rodeur_finit_le_chapitre_5_sans_rien_recevoir_deux_fois()
    {
        // Avant V1.9.5, la mission au rôdeur, le chapitre 2 jamais commencé (Sœur Albe le proposera) : la partie finit le chapitre 5 ; elle
        // reçoit les récompenses des étapes qui restent et la récompense finale, rien de plus (ni la manifestation, déjà passée).
        var old = new SavedStory(new[] { "fact.traces.recit", "fact.moulin.porte_ouverte", "fact.manifestation.vue" },
            new Dictionary<string, QuestProgress> { [Ch1] = new QuestProgress("rodeur") }, new Dictionary<string, int> { ["npc.odile"] = 1 });
        WorldSession w = Open(Gaspard, Hero(20, old, 300));
        long xp0 = w.Character.Xp, money0 = w.Money;
        Say(w, "npc.gaspard", "rodeur");
        w.Narrative!.Defeated("rodeur", null);
        Assert.Contains($"StepCompleted {Main}/rodeur", Ticks(w, 1));
        Assert.True(w.Narrative.Learn("fact.chef.vaincu", new List<StoryEvent>()));
        Ticks(w);
        w = Go(w, Odile);
        Say(w, "npc.odile", "retour");
        Choose(w, 0, "ch5_aveu");
        Choose(w, 0);
        Ticks(w);
        w = Go(w, Moulin);
        Ticks(w);
        // Chez Sœur Albe, le chapitre 2 pas commencé : son nœud d'ancienne partie garde une réponse vers la voix.
        w = Go(w, Albe, "zone.chapelle");
        LeadsTo(w, "npc.albe", "ch2_ancienne", "text.ch5.albe.vers_voix", "ch5_voix");
        Choose(w, 0, "ch5_suite");
        Choose(w, 0);
        Assert.Contains($"QuestCompleted {Main}", Ticks(w));
        Assert.Equal((xp0 + 500 + 800 + 300 + 300 + 200 + 1000, money0 + 150 + 500 + 1500), (w.Character.Xp, w.Money));
        // Le chapitre 2 reste à commencer chez elle.
        DialogueView ancienne = Say(w, "npc.albe", "ch2_ancienne");
        Assert.DoesNotContain(ancienne.Options, o => o.Text == "text.ch5.albe.vers_voix");
        Assert.Contains($"QuestStarted {Bourg}/chapelle", Choose(w, Assert.Single(ancienne.Options, o => o.Text == "text.ch2.albe.ancienne.ecouter").Index));
    }

    [Fact]
    public void Ancienne_partie_a_la_mission_finie_a_les_deux_chapitres_finis()
    {
        // Avant V1.9.5, la mission finie (le fait du retour d'Odile appris) : les chapitres 1 et 5 sont finis, rien n'est redonné ; la fin
        // du chapitre 4, plus tard, ne recommence pas le chapitre 5.
        var old = new SavedStory(new[] { "fact.traces.recit", "fact.moulin.porte_ouverte", "fact.manifestation.vue", "fact.chef.vaincu", "fact.odile.retour" },
            new Dictionary<string, QuestProgress> { [Ch1] = new QuestProgress(QuestProgress.Done) }, new Dictionary<string, int> { ["npc.odile"] = 1 },
            new[] { "discovery.manifestation", "discovery.gardien" });
        SavedStory converted = StoryConversions.Upgrade(Story.Value, old);
        Assert.Equal((QuestProgress.Done, QuestProgress.Done), (converted.Quests[Ch1].Step, converted.Quests[Main].Step));
        // Relecture de la PR #93 : le fait vers la suite est appris, comme à la fin du chapitre 5 ; relire la partie convertie ne change rien.
        Assert.Contains(StoryConversions.NextFact, converted.Facts);
        Assert.Same(converted, StoryConversions.Upgrade(Story.Value, converted));
        SavedCharacter hero = Hero(20, old, 300);
        WorldSession w = Open(Odile, hero);
        Assert.Empty(Ticks(w, 5));
        Assert.Equal((hero.Xp, 300L), (w.Character.Xp, w.Money));
        Say(w, "npc.odile", "ch1_ferrure");   // ses offres d'abord
        Say(Open(Odile, Hero(20, Done(old.Facts, Ch1, "quest.ferrure_de_rechange", "quest.pont_des_vallons"))), "npc.odile", "apres");
        // Sœur Albe propose le chapitre 2 ; à la fin du chapitre 4, le chapitre 5 (déjà fini) n'est pas recommencé.
        LeadsTo(OpenIn("zone.chapelle", Albe, Hero(20, old)), "npc.albe", "ch2_ancienne", "text.ch5.albe.vers_voix", null);
        var tour = new SavedStory(old.Facts.Concat(AfterChapter4.Take(10)), new Dictionary<string, QuestProgress>
        {
            [Ch1] = new QuestProgress(QuestProgress.Done), [Bourg] = new QuestProgress(QuestProgress.Done), [Hameau] = new QuestProgress(QuestProgress.Done),
            [Tour] = new QuestProgress("albe"),
        });
        WorldSession end = OpenIn("zone.chapelle", Albe, Hero(20, tour));
        Assert.Equal(DialogueRefusal.None, end.Choose(Assert.Single(Say(end, "npc.albe", "ch2_herbes").Options, o => o.Text == "text.ch4.albe.vers_sans_repos").Index, out _, out _));
        Choose(end, Assert.Single(end.Narrative!.Open!.Options).Index, "ch4_livre");
        Choose(end, 0);
        List<string> events = Ticks(end);
        Assert.Contains($"QuestCompleted {Tour}", events);
        Assert.DoesNotContain(events, e => e.StartsWith($"QuestStarted {Main}", StringComparison.Ordinal));
        Assert.True(end.Narrative.IsDone(Main));
    }

    [Theory]
    [InlineData("pre")]
    [InlineData("meute")]
    [InlineData("gaspard")]
    [InlineData(QuestProgress.Done)]
    public void Partie_avant_les_etapes_passees_ou_au_chapitre_1_fini_ne_change_pas(string step)
    {
        // Chapitre 1 en cours avant la manifestation, ou fini chez Gaspard (sans le retour d'Odile) : rien ne change, le chapitre 5 viendra
        // avec la fin du chapitre 4.
        var story = new SavedStory(new[] { "fact.traces.recit", "fact.moulin.porte_ouverte" }, new Dictionary<string, QuestProgress> { [Ch1] = new QuestProgress(step) },
            tracked: step == QuestProgress.Done ? null : Ch1);
        Assert.Same(story, StoryConversions.Upgrade(Story.Value, story));
        WorldSession w = Open(Gaspard, Hero(5, story));
        Assert.False(w.Narrative!.IsActive(Main) || w.Narrative.IsDone(Main));
        Assert.Same(SavedStory.Empty, StoryConversions.Upgrade(Story.Value, SavedStory.Empty));
    }

    [Fact]
    public void Ancienne_partie_au_chapitre_4_fini_commence_le_chapitre_5_chez_Albe()
    {
        // Une sauvegarde d'avant V1.9.5, le chapitre 4 fini : Sœur Albe envoie aux hautes pierres par une réponse, une seule fois ; ses herbes passent avant.
        WorldSession w = OpenIn("zone.chapelle", Albe, Hero(20, Done(AfterChapter4, Chapters1To4)));
        Assert.Contains($"QuestStarted {Main}/manifestation", LeadsTo(w, "npc.albe", "ch2_herbes", "text.ch5.albe.ancienne", "ch5_ancienne"));
        LeadsTo(w, "npc.albe", "ch2_herbes", "text.ch5.albe.ancienne", null);
        // Les herbes finies, par son mot d'après le chapitre 2.
        WorldSession old = OpenIn("zone.chapelle", Albe, Hero(20, Done(AfterChapter4, Chapters1To4.Append(Herbes).ToArray())));
        Assert.Contains($"QuestStarted {Main}/manifestation", LeadsTo(old, "npc.albe", "ch2_apres", "text.ch5.albe.ancienne", "ch5_ancienne"));
        // Avant la fin du chapitre 4, rien du chapitre 5.
        LeadsTo(OpenIn("zone.chapelle", Albe, Hero(20, With(AfterChapter4.Take(10), (Tour, "reponse")))), "npc.albe", "ch2_herbes", "text.ch5.albe.ancienne", null);
    }

    // --- Pilotes : les deux quêtes secondaires --------------------------------------------------------------------------

    [Fact]
    public void Le_loup_blanc_de_bout_en_bout()
    {
        // Le loup blanc de « La bête blanche » (Frère Anselme) revient : la quête s'ouvre au niveau 21, une fois la bête blanche abattue.
        Say(Open(Gaspard, Hero(21, null)), "npc.gaspard", "ch5_pierres");
        LeadsTo(Open(Gaspard, Hero(21, null)), "npc.gaspard", "ch5_pierres", "text.ch5.gaspard.vers_loup", null);
        Say(Open(Gaspard, Hero(21, Done(new[] { "fact.traces.recit" }, Pierres))), "npc.gaspard", "ch5_loup_anselme");
        Say(Open(Gaspard, Hero(20, Done(new[] { "fact.traces.recit" }, Bete, Brunot))), "npc.gaspard", "garde");
        WorldSession w = Open(Gaspard, Hero(21, Done(new[] { "fact.traces.recit" }, Bete)));
        Say(w, "npc.gaspard", "ch5_loup");
        Assert.Contains($"QuestStarted {Loup}/traces", Choose(w, 0));
        Say(w, "npc.gaspard", "ch5_loup_attente");
        // Les traces, dans la forêt des Loups (lieu atteint).
        w = Go(w, Foret0, "zone.vallons", out List<string> traces);
        traces.AddRange(Ticks(w));
        Assert.Contains($"StepCompleted {Loup}/traces", traces);
        Assert.Contains("Discovered discovery.traces_du_loup_blanc", traces);
        // Le loup blanc (victoire annoncée à l'histoire ; un loup de la meute ne compte pas).
        w.Narrative!.Defeated("loup7", "loup");
        Assert.Empty(Ticks(w, 1));
        w.Narrative.Defeated("loup_blanc", "loup_blanc");
        Assert.Contains($"StepCompleted {Loup}/loup", Ticks(w, 1));
        // Frère Anselme, à la chapelle : la bête qu'il avait bénie ne trouve pas le repos.
        w = Go(w, Pretre, "zone.chapelle");
        Say(w, "npc.pretre", "ch5_loup");
        Choose(w, 0);
        Assert.Contains($"StepCompleted {Loup}/anselme", Ticks(w));
        Say(w, "npc.pretre", "ch2_cloches");   // ensuite, ses offres
        // Retour chez Gaspard : récompense au choix.
        w = Go(w, Gaspard);
        long xp = w.Character.Xp, money = w.Money;
        Say(w, "npc.gaspard", "ch5_loup_retour");
        List<string> end = Ticks(w);
        Assert.Contains($"RewardToChoose {Loup}", end);
        Assert.Contains("TrustChanged npc.gaspard 1", end);
        Assert.Equal((xp + 1200, money + 800), (w.Character.Xp, w.Money));
        DialogueView recompense = Say(w, "npc.gaspard", "ch5_loup_recompense");
        Assert.Contains(recompense.Options, o => o.Text == "text.ch5.gaspard.vers_pierres");   // l'autre offre reste accessible
        Assert.Equal(DialogueRefusal.None, w.ChooseReward(Loup, 0, out IReadOnlyList<StoryEvent> chosen));
        Assert.Equal(new[] { "ItemGiven item.cape_du_seuil 1", $"QuestCompleted {Loup}", "ReputationChanged villagers/questSide 10" }, chosen.Select(e => e.ToString()));
        Say(w, "npc.gaspard", "ch5_pierres");
    }

    [Fact]
    public void Les_pierres_gravees_de_bout_en_bout()
    {
        WorldSession w = Open(Gaspard, Hero(21, null));
        Say(w, "npc.gaspard", "ch5_pierres");
        Assert.Contains($"QuestStarted {Pierres}/ruines", Choose(w, 0));
        Say(w, "npc.gaspard", "ch5_pierres_attente");
        // Quatre pierres gravées, partout dans les Vallons (lieux atteints, dans l'ordre) : chacune sa découverte et 100 points.
        foreach (((double, double) at, string step) in new[] { (Ruines0, "ruines"), (Lac, "lac"), (Foret0, "foret"), (Plaine0, "plaine") })
        {
            long before = w.Character.Xp;
            w = Go(w, at, "zone.vallons", out List<string> found);
            found.AddRange(Ticks(w));
            Assert.Contains($"StepCompleted {Pierres}/{step}", found);
            Assert.Contains(found, e => e.StartsWith("Discovered discovery.pierre_gravee_", StringComparison.Ordinal));
            Assert.Equal(before + 100, w.Character.Xp);
        }
        // Ysarn, le Fêlé des hautes pierres, lit les quatre lignes.
        w = Go(w, Ysarn);
        Say(w, "npc.ysarn", "ch5_lecture");
        Assert.Contains("FactLearned fact.ch5.pierres.lues", Choose(w, 0, "ch5_lu"));
        Choose(w, 0);
        List<string> lu = Ticks(w);
        Assert.Contains($"StepCompleted {Pierres}/ysarn", lu);
        Assert.Contains("Discovered discovery.lecture_des_pierres", lu);
        Say(w, "npc.ysarn", "ch5_accueil");
        // Retour chez Gaspard : sa propre pierre gravée, et une récompense au choix.
        w = Go(w, Gaspard);
        long xp = w.Character.Xp, money = w.Money;
        Say(w, "npc.gaspard", "ch5_pierres_retour");
        List<string> end = Ticks(w);
        Assert.Contains($"RewardToChoose {Pierres}", end);
        Assert.Contains("ItemGiven item.pierre_gravee 1", end);
        Assert.Equal((xp + 1000, money + 600, 1), (w.Character.Xp, w.Money, w.Inventory.Count("item.pierre_gravee")));
        Say(w, "npc.gaspard", "ch5_pierres_recompense");
        Assert.Equal(DialogueRefusal.None, w.ChooseReward(Pierres, 1, out IReadOnlyList<StoryEvent> chosen));
        Assert.Equal(new[] { "ItemGiven item.marteau_des_hautes_pierres 1", $"QuestCompleted {Pierres}", "ReputationChanged villagers/questSide 10" }, chosen.Select(e => e.ToString()));
        Say(w, "npc.gaspard", "ch5_loup_anselme");   // la bête blanche pas encore abattue : Gaspard envoie chez Frère Anselme
    }

    // --- Leçon de la PR #88 : aucune entrée prioritaire ne masque définitivement une offre ------------------------------

    [Fact]
    public void Offres_de_chaque_donneur_encore_accessibles()
    {
        string[] facts = AfterChapter4;
        // Chez Gaspard, chaque attente et l'astuce du rôdeur gardent une réponse vers l'autre offre.
        LeadsTo(Open(Gaspard, Hero(21, new SavedStory(facts, new Dictionary<string, QuestProgress> { [Bete] = new(QuestProgress.Done), [Loup] = new("traces") }))),
            "npc.gaspard", "ch5_loup_attente", "text.ch5.gaspard.vers_pierres", "ch5_pierres");
        LeadsTo(Open(Gaspard, Hero(21, new SavedStory(facts, new Dictionary<string, QuestProgress> { [Bete] = new(QuestProgress.Done), [Pierres] = new("lac") }))),
            "npc.gaspard", "ch5_pierres_attente", "text.ch5.gaspard.vers_loup", "ch5_loup");
        var rodeur = new SavedStory(facts, new Dictionary<string, QuestProgress> { [Bete] = new(QuestProgress.Done), [Main] = new("rodeur") });
        LeadsTo(Open(Gaspard, Hero(21, rodeur)), "npc.gaspard", "rodeur", "text.ch5.gaspard.vers_loup", "ch5_loup");
        LeadsTo(Open(Gaspard, Hero(21, rodeur)), "npc.gaspard", "rodeur", "text.ch5.gaspard.vers_pierres", "ch5_pierres");
        // Toutes les quêtes de Gaspard finies : ses conseils.
        Say(Open(Gaspard, Hero(25, Done(facts, Bete, Loup, Pierres, Brunot))), "npc.gaspard", "garde");
        // Chez Odile, au retour du chapitre 5 : son aveu garde les réponses vers la ferrure et le pont.
        var retour = new SavedStory(facts, new Dictionary<string, QuestProgress> { [Main] = new("retour") }, new Dictionary<string, int> { ["npc.odile"] = 1 });
        WorldSession odile = Open(Odile, Hero(21, retour));
        Say(odile, "npc.odile", "retour");
        Assert.Equal(DialogueRefusal.None, odile.Choose(0, out DialogueView? aveu, out _));
        Assert.Equal("ch5_aveu", aveu!.Node);
        Assert.Equal(new[] { "text.ch5.odile.aveu.ecouter", "text.ch5.odile.vers_ferrure", "text.ch3.odile.vers_pont" }, aveu.Options.Select(o => o.Text));
        // Chez Sœur Albe, l'offre des herbes, leur attente et leur récompense gardent les réponses du chapitre 5.
        foreach ((string node, SavedStory story) in new[]
        {
            ("ch2_herbes", new SavedStory(facts, new Dictionary<string, QuestProgress> { [Bourg] = new(QuestProgress.Done), [Main] = new("albe") })),
            ("ch2_herbes_attente", new SavedStory(facts, new Dictionary<string, QuestProgress> { [Bourg] = new(QuestProgress.Done), [Herbes] = new("herbes"), [Main] = new("albe") })),
        })
            LeadsTo(OpenIn("zone.chapelle", Albe, Hero(21, story)), "npc.albe", node, "text.ch5.albe.vers_voix", "ch5_voix");
        // Chez Frère Anselme, l'étape du loup blanc ne passe qu'une fois (objectif « parler à ») ; ensuite ses offres reviennent.
        var anselme = new SavedStory(facts, new Dictionary<string, QuestProgress> { [Bete] = new(QuestProgress.Done), [Loup] = new("anselme") });
        WorldSession pretre = OpenIn("zone.chapelle", Pretre, Hero(21, anselme));
        Say(pretre, "npc.pretre", "ch5_loup");
        Ticks(pretre);
        Say(pretre, "npc.pretre", "ch2_cloches");
        // Sous le niveau demandé, rien de nouveau n'est offert ; Ysarn parle à tous.
        Say(Open(Gaspard, Hero(20, null)), "npc.gaspard", "garde");
        Say(Open(Ysarn, Hero(1, null)), "npc.ysarn", "ch5_accueil");
        Say(Open(Ysarn, Hero(21, With(facts, (Main, "rodeur")))), "npc.ysarn", "ch5_seuil");
    }
}
