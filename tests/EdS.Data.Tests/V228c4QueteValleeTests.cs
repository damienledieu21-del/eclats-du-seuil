using EdS.Data.Crafting;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Zones;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.28c (4), décision de Damien du 15 novembre 2026 : l'éboulement et les gués de la vallée s'ouvrent par une quête à la fin du
/// chapitre 1, comme la porte du moulin. Quête minimale « Les passages de la vallée » : Gaspard la propose une fois le chapitre 1 fini (une
/// réponse de son dialogue ordinaire, donc aussi dans une ancienne partie déjà plus loin), Aldric, au gué, la termine ; ses effets ouvrent
/// les trois passages, en entier ou pas du tout (registre réservé avant toute mutation, comme la porte du moulin).
/// </summary>
[Trait("Category", "World")]
public class V228c4QueteValleeTests
{
    private const string Mission = "quest.traces_sous_le_moulin", Vallee = "quest.passages_de_la_vallee";
    private static readonly string[] Passages = { "eboulement", "gue_vallee_sud", "gue_vallee_nord" };
    private static readonly (double X, double Z) Gaspard = (26, -13), Archer = (1, -1.5);

    private static WorldSession Open((double X, double Z) at, SavedCharacter hero, IEnumerable<string>? consumed = null)
    {
        ZoneDefinition zone = Zone(at.X, at.Z);
        CraftingBook crafting = CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory), Texts, zone, ItemsCatalog).Book!;
        var session = new WorldSession(new WorldSave("zone.vallons", 0, 0, (consumed ?? Array.Empty<string>()).Append(PlayerCharacters.PlacesKey)
            .Distinct(StringComparer.Ordinal), hero), zone, ShippedData, Progression, ItemsCatalog, Book(zone), crafting, market: new Market(ShippedMerchants));
        session.Step();
        Ticks(session);
        return session;
    }

    private static WorldSession Go(WorldSession from, (double X, double Z) at)
    {
        WorldSave save = SaveJsonReader.Read(SaveJsonReader.Write(from.Save())).Save!;
        return Open(at, save.Character!, save.Consumed);
    }

    private static List<string> Ticks(WorldSession w, int ticks = 3)
    {
        var events = new List<string>();
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

    private static SavedCharacter Hero(SavedStory story) => V11TestData.Hero(0).WithStory(story);

    [Fact]
    public void Quete_livree_apres_le_chapitre_1_et_ouvrant_les_trois_passages()
    {
        ZoneDefinition zone = Zone();
        StoryBook book = Book(zone);
        Quest q = book.Quest(Vallee);
        Assert.Equal(QuestCategory.Side, q.Category);
        Assert.Equal(new[] { "aldric" }, q.Steps.Select(s => s.Id));
        Assert.Equal(Passages, q.Effects.Where(e => e.Kind == EffectKind.OpenPassage).Select(e => e.Id));
        Assert.All(Passages, p => Assert.Contains(zone.Passages, x => x.Id == p));
        // Ajoutée à la fin : les quêtes d'avant gardent leur rang.
        Assert.Equal(Vallee, book.Quests[^2].Id);   // V2.28d (3) : la quête du cheval après elle
    }

    private static long XpForLevel(int level) => Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= level);

    private static SavedStory Done(params string[] facts) =>
        new SavedStory(facts.Prepend("fact.traces.recit"), new Dictionary<string, QuestProgress> { [Mission] = new QuestProgress(QuestProgress.Done) });


    [Fact]
    public void Gaspard_la_propose_a_la_fin_du_chapitre_1_et_Aldric_ouvre_les_passages()
    {
        // Avant la fin du chapitre 1 : ni offre ni réponse, les passages restent fermés.
        WorldSession w = Open(Gaspard, Hero(SavedStory.Empty));
        Say(w, "npc.gaspard", "garde");
        Assert.All(Passages, p => Assert.False(w.IsPassageOpen(p)));
        // Chapitre 1 fini (une ancienne partie, sans la quête dans son registre, aussi) : Gaspard la propose d'office.
        w = Open(Gaspard, Hero(Done("fact.moulin.porte_ouverte")));
        Say(w, "npc.gaspard", "v228_vallee");
        Assert.Contains($"QuestStarted {Vallee}/aldric", Choose(w, 0));
        Say(w, "npc.gaspard", "garde");
        Assert.All(Passages, p => Assert.False(w.IsPassageOpen(p)));
        // Aldric, au gué : les trois passages s'ouvrent, gardés dans la sauvegarde.
        w = Go(w, Archer);
        Say(w, "npc.archer_garde", "v228_vallee");
        Choose(w, 0);
        long xp = w.Character.Xp;
        List<string> end = Ticks(w);
        Assert.Contains($"QuestCompleted {Vallee}", end);
        Assert.All(Passages, p => Assert.Contains($"PassageOpened {p}", end));
        Assert.Equal(xp + 60, w.Character.Xp);
        w = Go(w, Archer);
        Assert.All(Passages, p => Assert.True(w.IsPassageOpen(p)));
        Assert.True(w.Narrative!.IsDone(Vallee));
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.archer_garde", out DialogueView? after));
        Assert.NotEqual("v228_vallee", after!.Node);
    }

    [Fact]
    public void Offre_prioritaire_sur_les_repliques_qui_passent_avant_garde()
    {
        // Relecture indépendante de la PR #179 : Gaspard propose la quête d'office, avant ses offres, la nuit, le froid des cicatrices et
        // Brunot (seuls passent avant les étapes de quête en cours et les récompenses).
        WorldSession brunot = Open(Gaspard, V11TestData.Hero(0).WithStory(Done("fact.croc_gris.vaincu")).WithXp(XpForLevel(14)));
        Say(brunot, "npc.gaspard", "v228_vallee");
        Assert.Contains($"QuestStarted {Vallee}/aldric", Choose(brunot, 0));
        Say(brunot, "npc.gaspard", "v292_brunot");   // la quête acceptée, Brunot reprend sa place
        Dialogue gaspard = Book(Zone()).Dialogue("dialogue.gaspard");
        var order = gaspard.Entries.Select(e => e.Node).ToList();
        int offer = order.IndexOf("v228_vallee");
        Assert.All(new[] { "ch1_nuit", "v261_froid", "v292_brunot", "v27_gaspard_offre", "ch5_loup", "ch5_pierres", "garde" },
            node => Assert.True(order.IndexOf(node) > offer, node));
    }

    [Fact]
    public void Replique_d_Aldric_prioritaire_meme_avec_ses_quetes_en_cours()
    {
        // Revue Codex de la PR #179 : parler à Aldric finit la quête ; ses indications passent donc avant ses autres répliques (flèches,
        // garde de nuit en cours).
        Assert.Equal("v228_vallee", Book(Zone()).Dialogue("dialogue.archer_garde").Entries[0].Node);
        var story = new SavedStory(new[] { "fact.traces.recit" }, new Dictionary<string, QuestProgress>
        {
            [Mission] = new QuestProgress(QuestProgress.Done),
            [Vallee] = new QuestProgress("aldric"),
            ["quest.fleches_perdues"] = new QuestProgress("fleches"),
        });
        Say(Open(Archer, Hero(story)), "npc.archer_garde", "v228_vallee");
    }

    [Fact]
    public void Sauvegarde_a_l_etape_d_Aldric_reprise()
    {
        var story = new SavedStory(new[] { "fact.traces.recit" }, new Dictionary<string, QuestProgress>
        {
            [Mission] = new QuestProgress(QuestProgress.Done),
            [Vallee] = new QuestProgress("aldric"),
        });
        WorldSession w = Open(Archer, Hero(story));
        Say(w, "npc.archer_garde", "v228_vallee");
        Choose(w, 0);
        Assert.Contains($"QuestCompleted {Vallee}", Ticks(w));
        Assert.All(Passages, p => Assert.True(w.IsPassageOpen(p)));
    }
}
