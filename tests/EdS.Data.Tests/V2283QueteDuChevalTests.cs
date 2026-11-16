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
/// Sous-lot V2.28d (3), décision de Damien du 16 novembre 2026 : une vraie petite quête du cheval, au lieu du cheval donné à la fin de la
/// mission du chapitre 1. « Le cheval de la ferme » : une fois le chapitre 1 fini, Achille, le fermier des champs, demande de retrouver
/// Brume, sa jument, sauvée vers le lac ; atteindre les rives du lac, puis revenir lui parler : 40 XP et le cheval
/// (<see cref="PlayerCharacters.HorseQuest"/>). Un personnage qui a déjà son cheval le garde.
/// </summary>
[Trait("Category", "World")]
public class V2283QueteDuChevalTests
{
    private const string Mission = "quest.traces_sous_le_moulin", Cheval = "quest.le_cheval_de_la_ferme";
    private static readonly (double X, double Z) Ferme = (-64, -36), Rives = (-15, -700);

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

    private static SavedStory Done() =>
        new SavedStory(new[] { "fact.traces.recit", "fact.met.fermier" }, new Dictionary<string, QuestProgress> { [Mission] = new QuestProgress(QuestProgress.Done) });

    [Fact]
    public void Quete_dans_les_donnees()
    {
        Assert.Equal(Cheval, PlayerCharacters.HorseQuest);
        Assert.Equal(Mission, PlayerCharacters.ChapterOneQuest);
        StoryBook book = Book(Zone());
        Quest q = book.Quest(Cheval);
        Assert.Equal(Cheval, book.Quests[^1].Id);   // ajoutée à la fin : les quêtes d'avant gardent leur rang
        Assert.Equal(QuestCategory.Side, q.Category);
        Assert.Equal(new[] { "jument", "retour" }, q.Steps.Select(s => s.Id));
        Assert.Equal((ObjectiveKind.Reach, "rives_du_lac"), (q.Steps[0].Objective.Kind, q.Steps[0].Objective.Id));
        Assert.Equal((ObjectiveKind.Talk, "npc.fermier"), (q.Steps[1].Objective.Kind, q.Steps[1].Objective.Id));
        // Les répliques de la quête en cours passent avant toutes les autres d'Achille (une étape se termine en lui parlant) ; l'offre vient
        // après celles de la quête des sangliers (chapitre 3), avant ses répliques ordinaires.
        var order = book.Dialogue("dialogue.fermier").Entries.Select(e => e.Node).ToList();
        Assert.Equal(order.IndexOf("v2283_cheval") + 1, order.IndexOf("v2283_deja_monture"));
        Assert.Equal(new[] { "v2283_cheval_retour", "v2283_cheval_attente" }, order.Take(2));
        int offer = order.IndexOf("v2283_cheval");
        Assert.True(offer > order.IndexOf("ch3_sangliers") && offer < order.IndexOf("ch3_apres") && offer < order.IndexOf("ch3_accueil"));
    }

    [Fact]
    public void Achille_confie_Brume_a_qui_la_ramene_des_rives_du_lac()
    {
        // Avant la fin du chapitre 1 : pas d'offre, pas de cheval.
        WorldSession w = Open(Ferme, V11TestData.Hero(0));
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.fermier", out DialogueView? before));
        Assert.DoesNotContain("v2283", before!.Node, StringComparison.Ordinal);
        Assert.False(w.OwnsMount);
        // Chapitre 1 fini : la mission seule ne donne plus le cheval ; Achille propose la quête.
        w = Open(Ferme, V11TestData.Hero(0).WithStory(Done()));
        Assert.False(w.OwnsMount);
        Assert.False(w.CanCallMount);
        Say(w, "npc.fermier", "v2283_cheval");
        Assert.Contains($"QuestStarted {Cheval}/jument", Choose(w, 0));
        Say(w, "npc.fermier", "v2283_cheval_attente");
        Choose(w, 0);
        // Les rives du lac : Brume est retrouvée.
        w = Go(w, Rives);
        Ticks(w);
        Assert.Contains("fact.v2283.brume_retrouvee", w.Narrative!.Facts);
        Assert.False(w.OwnsMount);
        // Retour chez Achille : 40 XP et le cheval, gardé dans la sauvegarde.
        w = Go(w, Ferme);
        Say(w, "npc.fermier", "v2283_cheval_retour");
        Choose(w, 0);
        long xp = w.Character.Xp;
        List<string> end = Ticks(w);
        Assert.Contains($"QuestCompleted {Cheval}", end);
        Assert.Equal(xp + 40, w.Character.Xp);
        Assert.True(w.OwnsMount);
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!;
        Assert.Equal("mount.cheval", saved.Character!.Mount);
        // Une seule fois : Achille n'a plus de réplique de la quête.
        w = Go(w, Ferme);
        Assert.True(w.CanCallMount);
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.fermier", out DialogueView? after));
        Assert.DoesNotContain("v2283", after!.Node, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_cheval_deja_donne_est_garde()
    {
        // Une partie où le cheval est venu avec la fin de la mission (V2.28d (2b)) : la monture reste, la quête reste à faire (sans cheval de
        // plus à la fin : GiveHorse ne remplace jamais une monture).
        SavedCharacter rider = V11TestData.Hero(0).WithStory(Done()).WithMount("mount.cheval");
        SavedCharacter marked = PlayerCharacters.GiveHorse(rider, rider.Story);
        Assert.Equal("mount.cheval", marked.Mount);
        Assert.Contains(PlayerCharacters.MountFact, marked.Story.Facts);
        Assert.Same(marked, PlayerCharacters.GiveHorse(marked, marked.Story));   // idempotente
        // Relecture indépendante de la PR #192 : Achille ne propose pas la quête à qui a déjà une monture ; il le lui dit une fois.
        WorldSession w = Open(Ferme, rider);
        Assert.True(w.OwnsMount);
        Assert.True(w.CanCallMount);
        Say(w, "npc.fermier", "v2283_deja_monture");
        Choose(w, 0);
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.fermier", out DialogueView? next));
        Assert.DoesNotContain("v2283", next!.Node, StringComparison.Ordinal);
        Assert.False(w.Narrative!.IsActive(Cheval) || w.Narrative.IsDone(Cheval));
        // La mission seule ne donne pas le cheval ; la quête du cheval finie, oui.
        SavedCharacter walker = V11TestData.Hero(0).WithStory(Done());
        Assert.Null(PlayerCharacters.GiveHorse(walker, walker.Story).Mount);
        var done = new SavedStory(walker.Story.Facts, new Dictionary<string, QuestProgress>
        {
            [Mission] = new QuestProgress(QuestProgress.Done),
            [Cheval] = new QuestProgress(QuestProgress.Done),
        });
        Assert.Equal("mount.cheval", PlayerCharacters.GiveHorse(walker.WithStory(done), done).Mount);
    }
}
