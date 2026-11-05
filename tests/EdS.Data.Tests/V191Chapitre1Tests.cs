using System.Text.Json.Nodes;
using EdS.Data.Crafting;
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
/// Sous-lot V1.9.1, chapitre 1 « Les traces sous le moulin » (docs/ECRITURE.md) : la mission reprise en chapitre 1 de la quête principale
/// (la meute du pré, le souvenir de la Rupture, Gaspard qui envoie au bourg) et ses trois quêtes secondaires. Chaque quête est jouée de bout
/// en bout par un pilote scripté : dialogues, réponses, fabrication à la forge, livraisons, et victoires annoncées à l'histoire. Les
/// combats eux-mêmes, la récolte et le butin sont couverts par leurs propres tests : ici, le minerai et les flèches sont dans le sac de départ.
/// </summary>
[Trait("Category", "World")]
public class V191Chapitre1Tests
{
    private const string Mission = "quest.traces_sous_le_moulin", Ferrure = "quest.ferrure_de_rechange", Pain = "quest.pain_du_matin",
        Fleches = "quest.fleches_perdues";

    // Où se tenir pour chaque PNJ (à 1 à 1,5 m, hors de tout obstacle) et pour les lieux de la mission.
    private static readonly (double X, double Z) Odile = (-9, 2), Moulin = (-8, 5), Forge = (-19, -5), Pre = (-10, 30), Gaspard = (26, -13),
        Boulanger = (-42, 2), Archer = (1, -1.5);

    private static CraftingBook Crafting(ZoneDefinition zone) => CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory), Texts, zone, ItemsCatalog).Book!;

    /// <summary>Partie ouverte au point (<paramref name="at"/>) avec l'histoire, les métiers et le commerce livrés ; un tick joué.</summary>
    private static WorldSession Open((double X, double Z) at, SavedCharacter hero, IEnumerable<string>? consumed = null) => Open(at, hero, consumed, out _);

    /// <summary>Comme <see cref="Open(ValueTuple{double, double}, SavedCharacter, IEnumerable{string}?)"/>, avec les événements de l'arrivée et des trois ticks suivants.</summary>
    private static WorldSession Open((double X, double Z) at, SavedCharacter hero, IEnumerable<string>? consumed, out List<string> events)
    {
        ZoneDefinition zone = Zone(at.X, at.Z);
        var session = new WorldSession(new WorldSave("zone.vallons", 0, 0, (consumed ?? Array.Empty<string>()).Append(PlayerCharacters.PlacesKey)
            .Distinct(StringComparer.Ordinal), hero), zone, ShippedData, Progression, ItemsCatalog, Book(zone), Crafting(zone),
            market: new Market(ShippedMerchants));
        events = session.Step().Story.Select(e => e.ToString()).ToList();
        events.AddRange(Ticks(session));
        return session;
    }

    /// <summary>Le pilote se rend ailleurs : la partie est sauvegardée, relue, et rouverte au nouveau point.</summary>
    private static WorldSession Go(WorldSession from, (double X, double Z) at) => Go(from, at, out _);

    private static WorldSession Go(WorldSession from, (double X, double Z) at, out List<string> events)
    {
        WorldSave save = SaveJsonReader.Read(SaveJsonReader.Write(from.Save())).Save!;
        return Open(at, save.Character!, save.Consumed, out events);
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

    private static SavedCharacter Hero(SavedStory? story, params ItemStack[] items) => V11TestData.Hero(0, items).WithStory(story ?? SavedStory.Empty);

    // --- Données -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Chapitre_1_et_ses_quetes_livres()
    {
        StoryBook book = Book(Zone());
        // Sous-lot V1.9.5 (choix de Damien du 5 novembre 2026) : le chapitre 1 finit chez Gaspard ; ses quatre dernières étapes sont au chapitre 5.
        Assert.Equal(new[] { "enquete", "traces", "passage", "pre", "meute", "gaspard" }, book.Quest(Mission).Steps.Select(s => s.Id));
        Assert.Empty(book.Quest(Mission).Effects);
        Assert.Equal(new[] { Ferrure, Pain, Fleches }, book.Quests.Skip(6).Take(3).Select(q => q.Id));   // ajoutées à la fin : les chemins d'avant restent (V1.9.2 : le chapitre 2 suit)
        Assert.All(new[] { Ferrure, Pain, Fleches }, q => Assert.Equal(QuestCategory.Side, book.Quest(q).Category));
        Assert.Equal(QuestCategory.Main, book.Quest(Mission).Category);
        Assert.Equal(new[] { "npc.boulanger", "npc.archer_garde" }, book.Npcs.Skip(12).Take(2).Select(n => n.Id));
        Assert.Equal("Miche du matin", ItemsCatalog.Item("item.miche_du_matin").Name);
        // Les flèches se récoltent sur les archers des ruines (niveau 6, à côté du gué), pas dans la plaine de l'est (niveau 11) : proposition.
        Assert.Contains(ItemsCatalog.Loot.Single(l => l.Id == "loot.archer").Drops, d => d.Item == "item.fleche_brisee");
        Assert.Equal(6, Zone().MinLevelAt(new GroundVector(Fixed.FromInt(21), Fixed.FromInt(1))));   // l'archer des ruines
    }

    [Fact]
    public void Nouveaux_PNJ_a_portee_depuis_une_place_libre()
    {
        ZoneDefinition zone = Zone();
        StoryBook book = Book(zone);
        foreach ((string npc, (double x, double z)) in new[] { ("npc.boulanger", Boulanger), ("npc.archer_garde", Archer) })
        {
            Npc n = book.Npc(npc);
            double nx = n.Position.X.Raw / 10000.0, nz = n.Position.Z.Raw / 10000.0;
            Assert.True(Math.Sqrt((nx - x) * (nx - x) + (nz - z) * (nz - z)) < 3, npc);
            foreach ((double px, double pz) in new[] { (x, z), (nx, nz) })
                Assert.All(zone.Setup.Space!.Obstacles, o => Assert.False(
                    o.Points.Min(p => p.X.Raw) / 10000.0 - 0.5 < px && px < o.Points.Max(p => p.X.Raw) / 10000.0 + 0.5
                    && o.Points.Min(p => p.Z.Raw) / 10000.0 - 0.5 < pz && pz < o.Points.Max(p => p.Z.Raw) / 10000.0 + 0.5, $"{npc} : {o.Id}"));
        }
    }

    // --- Pilotes : chaque quête de bout en bout -----------------------------------------------------------------------

    [Fact]
    public void Chapitre_1_joue_de_bout_en_bout()
    {
        // Odile raconte : la mission commence.
        WorldSession w = Open(Odile, Hero(null, new ItemStack("item.minerai_de_fer", 3)));
        Say(w, "npc.odile", "accueil");
        Choose(w, 0, "recit");
        Assert.Contains($"QuestStarted {Mission}/enquete", Choose(w, 0));
        Assert.Contains($"StepCompleted {Mission}/enquete", Ticks(w));
        // Les traces mènent au moulin.
        w = Go(w, Moulin, out List<string> moulin);
        Assert.Contains($"StepCompleted {Mission}/traces", moulin);
        // La forge : la ferrure forgée, posée par Maëlle ; la porte du moulin s'ouvre.
        w = Go(w, Forge);
        Assert.Equal(CraftRefusal.None, w.Craft("recipe.ferrure", out _));
        Say(w, "npc.maelle", "ferrure");
        Choose(w, 0, "faite");
        Assert.Contains($"StepCompleted {Mission}/passage", Ticks(w));
        // Le pré des loups, ses éclats.
        w = Go(w, Pre, out List<string> pre);
        Assert.Contains($"StepCompleted {Mission}/pre", pre);
        Assert.Contains("Discovered discovery.eclats_dans_le_pre", pre);
        Assert.Equal("meute", w.Narrative!.CurrentStep(Mission)!.Id);
        // La meute : deux loups ne suffisent pas ; au troisième, le souvenir de la Rupture.
        w.Narrative.Defeated("loup1", "loup");
        w.Narrative.Defeated("loup2", "loup");
        w.Narrative.Defeated("sanglier", "sanglier");   // une autre famille ne compte pas
        Assert.DoesNotContain($"StepCompleted {Mission}/meute", Ticks(w, 1));
        long xp = w.Character.Xp;
        w.Narrative.Defeated("loup3", "loup");
        Assert.Equal(new[] { $"StepCompleted {Mission}/meute", "Discovered discovery.souvenir_de_la_rupture", "XpGiven xp 80" }, Ticks(w, 1));
        Assert.Equal(xp + 80, w.Character.Xp);
        Assert.Contains("discovery.souvenir_de_la_rupture", w.Narrative.Discoveries);
        // Gaspard écoute, parle de l'Ordre de l'Aube, et envoie au bourg : fin du chapitre 1.
        w = Go(w, Gaspard);
        Say(w, "npc.gaspard", "ch1_souvenir");
        Choose(w, 0, "ch1_ordre");
        Assert.Equal(new[] { "FactLearned fact.ch1.gaspard.conseil", "TrustChanged npc.gaspard 1" }, Choose(w, 0).OrderBy(e => e, StringComparer.Ordinal));
        long money = w.Money;
        List<string> end = Ticks(w);
        Assert.Contains($"ObjectiveDone {Mission}/gaspard", end);
        Assert.Contains($"StepCompleted {Mission}/gaspard", end);
        Assert.Contains("XpGiven xp 100", end);
        Assert.Equal(money + 100, w.Money);
        Assert.True(w.Narrative!.Knows("fact.ch1.vers_le_bourg"));
        Assert.Contains($"QuestCompleted {Mission}", end);   // sous-lot V1.9.5 : fin du chapitre 1 (la suite de l'ancienne mission est au chapitre 5)
        Assert.True(w.Narrative.IsDone(Mission));
        // Rythme « rapide jusqu'au niveau 5 » : la mission seule, sans les loups ni les quêtes secondaires, mène au niveau 3.
        Assert.Equal(3, Progression.LevelFor(w.Character.Xp));
        Say(w, "npc.gaspard", "garde");   // ensuite, Gaspard reprend ses conseils
    }

    [Fact]
    public void La_ferrure_de_rechange_de_bout_en_bout()
    {
        // Odile ne la demande qu'une fois la porte du moulin ouverte.
        var before = new SavedStory(new[] { "fact.traces.recit" }, new Dictionary<string, QuestProgress> { [Mission] = new QuestProgress("passage") });
        Say(Open(Odile, Hero(before)), "npc.odile", "en_cours");
        var story = new SavedStory(new[] { "fact.traces.recit", "fact.moulin.porte_ouverte" },
            new Dictionary<string, QuestProgress> { [Mission] = new QuestProgress("pre") });
        WorldSession w = Open(Odile, Hero(story, new ItemStack("item.minerai_de_fer", 3)));
        Say(w, "npc.odile", "ch1_ferrure");
        Assert.Contains($"QuestStarted {Ferrure}/forger", Choose(w, 0));
        Say(w, "npc.odile", "ch1_ferrure_attente");
        // Maëlle s'en amuse ; la ferrure forgée remplit l'étape.
        w = Go(w, Forge);
        Say(w, "npc.maelle", "ch1_rechange");
        Assert.Equal(CraftRefusal.None, w.Craft("recipe.ferrure", out _));
        Assert.Contains($"StepCompleted {Ferrure}/forger", Ticks(w));
        // Livrée à Odile : sa confiance, l'expérience et les pièces, la quête finie.
        w = Go(w, Odile);
        long xp = w.Character.Xp, money = w.Money;
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.odile"));
        List<string> end = Ticks(w);
        Assert.Contains("ItemTaken item.ferrure 1", end);
        Assert.Contains("TrustChanged npc.odile 1", end);
        Assert.Contains($"QuestCompleted {Ferrure}", end);
        Assert.Equal((xp + 70, money + 80, 0), (w.Character.Xp, w.Money, w.Inventory.Count("item.ferrure")));
        Assert.True(w.Narrative!.IsDone(Ferrure));
        Say(w, "npc.odile", "en_cours");   // la mission reprend sa place
    }

    [Fact]
    public void Quetes_d_Odile_encore_offertes_une_fois_la_mission_finie()
    {
        // Revue Codex de la PR #88 : la mission finie (ou une ancienne sauvegarde où elle l'est) ne masque plus les quêtes d'Odile.
        var done = new SavedStory(new[] { "fact.traces.recit", "fact.moulin.porte_ouverte", "fact.odile.retour" },
            new Dictionary<string, QuestProgress> { [Mission] = new QuestProgress(QuestProgress.Done) });
        WorldSession w = Open(Odile, Hero(done));
        Say(w, "npc.odile", "ch1_ferrure");
        Assert.Contains($"QuestStarted {Ferrure}/forger", Choose(w, 0));
        Say(w, "npc.odile", "ch1_ferrure_attente");
        var pain = new SavedStory(new[] { "fact.traces.recit", "fact.odile.retour" },
            new Dictionary<string, QuestProgress> { [Mission] = new QuestProgress(QuestProgress.Done), [Pain] = new QuestProgress("odile") });
        Say(Open(Odile, Hero(pain, new ItemStack("item.miche_du_matin", 2))), "npc.odile", "ch1_pain");
        // Sans quête en attente, le merci d'après la mission.
        var after = new SavedStory(new[] { "fact.traces.recit", "fact.odile.retour" },
            new Dictionary<string, QuestProgress> { [Mission] = new QuestProgress(QuestProgress.Done) });
        Say(Open(Odile, Hero(after)), "npc.odile", "apres");
    }

    [Fact]
    public void Le_pain_du_matin_de_bout_en_bout()
    {
        WorldSession w = Open(Boulanger, Hero(null));
        Say(w, "npc.boulanger", "accueil");
        List<string> start = Choose(w, 0, "accepte").ToList();
        Assert.Contains($"QuestStarted {Pain}/odile", start);
        Assert.Contains("ItemGiven item.miche_du_matin 2", start);
        Say(w, "npc.boulanger", "en_cours");
        // Odile, au moulin.
        w = Go(w, Odile);
        Say(w, "npc.odile", "ch1_pain");
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.odile"));
        Assert.Contains($"StepCompleted {Pain}/odile", Ticks(w));
        // Gaspard, à la carrière.
        w = Go(w, Gaspard);
        Say(w, "npc.gaspard", "ch1_pain");
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.gaspard"));
        Assert.Contains($"StepCompleted {Pain}/gaspard", Ticks(w));
        Assert.Equal(0, w.Inventory.Count("item.miche_du_matin"));
        // Retour chez Firmin : remerciements, récompenses.
        w = Go(w, Boulanger);
        long xp = w.Character.Xp, money = w.Money;
        Say(w, "npc.boulanger", "retour");
        Choose(w, 0);
        List<string> end = Ticks(w);
        Assert.Contains($"StepCompleted {Pain}/retour", end);
        Assert.Contains($"QuestCompleted {Pain}", end);
        Assert.Equal((xp + 50, money + 60), (w.Character.Xp, w.Money));
        Say(w, "npc.boulanger", "apres");
        // La miche ne se vend pas (objet de quête, sans valeur).
        Assert.False(ShippedMerchants.Values.ContainsKey("item.miche_du_matin"));
    }

    [Fact]
    public void Les_fleches_perdues_de_bout_en_bout()
    {
        // Avant le niveau 4, Aldric ne demande rien et la quête reste fermée.
        WorldSession low = Open(Archer, Hero(null).WithXp(XpForLevel(3)));
        Assert.Empty(Say(low, "npc.archer_garde", "accueil").Options);
        Assert.False(low.Narrative!.IsOpen(Fleches));
        // Au niveau 4 : la demande ; la réponse moqueuse commence aussi la quête, mais Aldric s'en souvient.
        WorldSession w = Open(Archer, Hero(null, new ItemStack("item.fleche_brisee", 4)).WithXp(XpForLevel(4)));
        Say(w, "npc.archer_garde", "demande");
        List<string> start = Choose(w, 1).ToList();
        Assert.Contains($"QuestStarted {Fleches}/fleches", start);
        Assert.Contains("TrustChanged npc.archer_garde -1", start);
        Assert.Empty(Ticks(w));   // quatre flèches : pas assez
        // Sac complété à six flèches sans combat : le butin des archers des ruines est couvert par ses propres tests.
        WorldSave save = w.Save();
        SavedCharacter hero = save.Character!;
        hero = hero.WithItems(hero.Inventory.Select(s => s.Item == "item.fleche_brisee" ? new ItemStack(s.Item, 6) : s), hero.EquipmentInstances);
        w = Open(Archer, hero, save.Consumed, out List<string> arrival);
        Assert.Contains($"StepCompleted {Fleches}/fleches", arrival);
        Say(w, "npc.archer_garde", "en_cours");
        long xp = w.Character.Xp, money = w.Money;
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.archer_garde"));
        List<string> end = Ticks(w);
        Assert.Contains("ItemTaken item.fleche_brisee 6", end);
        Assert.Contains($"RewardToChoose {Fleches}", end);
        Assert.Equal((xp + 90, money + 100), (w.Character.Xp, w.Money));
        Say(w, "npc.archer_garde", "recompense");
        // Récompense au choix : deux pierres à aiguiser.
        Assert.Equal(DialogueRefusal.None, w.ChooseReward(Fleches, 1, out IReadOnlyList<StoryEvent> chosen));
        Assert.Equal(new[] { "ItemGiven item.pierre_a_aiguiser 2", $"QuestCompleted {Fleches}" }, chosen.Select(e => e.ToString()));
        Say(w, "npc.archer_garde", "apres");
    }
}
