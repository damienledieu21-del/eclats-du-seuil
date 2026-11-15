using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.WorldBosses;
using EdS.Data.Zones;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Grand Solitaire, 2e partie (décisions de Damien du 15 novembre 2026) : le boss du monde dans la partie solo, en temps serveur fourni par
/// l'hôte (<see cref="WorldSession.SetCivilTime"/>) : signes, apparition, annonce, assaut, départ ; mode solo (vie d'un seul joueur) ;
/// mémoire du vainqueur dans le registre des événements de la sauvegarde ; butin de la victoire (trophée et apparence à la première).
/// </summary>
[Trait("Category", "World")]
public class GrandSolitaireMondeTests
{
    private static readonly Lazy<ItemCatalog> Items = new(() => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!);
    private static readonly Lazy<WorldBossBook> Book = new(() => WorldBossJsonReader.ReadFile(WorldBossJsonReader.PathIn(DataDirectory),
        StoryJsonReader.ReadTextsFile(StoryJsonReader.TextsPathIn(DataDirectory)).Texts!, Zone(), Items.Value).Book!);

    private static WorldBossDefinition Boss => Book.Value.Boss("grand_solitaire");

    private static WorldSession Open(WorldSave? save = null)
    {
        ZoneDefinition zone = Zone();
        var hero = PlayerCharacters.Create("bastion", "class.bastion", Progression);
        var session = new WorldSession(save ?? new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone, ShippedData,
            Progression, ItemsCatalog, Book(zone));
        session.UseWorldBosses(Book.Value);
        return session;
    }

    /// <summary>Première occurrence planifiée à partir du 1er septembre 2027 (fenêtres de 6 h, une chance sur deux).</summary>
    private static WorldBossOccurrence FirstOccurrence(long fromWindow = -1)
    {
        long w = fromWindow >= 0 ? fromWindow : WorldBossSchedule.WindowOf(Boss, new DateTimeOffset(2027, 9, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds());
        while (true)
        {
            if (WorldBossSchedule.InWindow(Boss, w) is WorldBossOccurrence o) return o;
            w++;
        }
    }

    private static void At(WorldSession w, long serverMs)
    {
        w.SetCivilTime(serverMs / 1000);
        w.Step();
    }

    private static IEnumerable<WorldBossNoticeKind> Kinds(WorldSession w) => w.WorldBossNotices.Select(n => n.Kind);

    [Fact]
    public void Sans_date_fournie_aucun_boss_du_monde()
    {
        WorldSession w = Open();
        for (int i = 0; i < 20; i++) w.Step();
        Assert.Null(w.ServerMs);
        Assert.Null(w.WorldBossNow("grand_solitaire"));
        Assert.Empty(w.WorldBossNotices);
        Assert.Throws<InvalidOperationException>(() => w.UseWorldBosses(Book.Value));
        Assert.Throws<ArgumentException>(() => w.WorldBossNow("inconnu"));
    }

    [Fact]
    public void Occurrence_entiere_signes_apparition_annonce_assaut_depart_et_memoire_du_vainqueur()
    {
        WorldBossOccurrence occ = FirstOccurrence();
        WorldSession w = Open();
        At(w, occ.SignsStartMs - 10_000);
        Assert.Null(w.WorldBossNow("grand_solitaire"));
        At(w, occ.SignsStartMs + 1000);
        WorldBossEvent e = w.WorldBossNow("grand_solitaire")!;
        Assert.Equal(new[] { WorldBossNoticeKind.Signs }, Kinds(w));
        Assert.Equal((WorldBossStage.Signs, false, occ.Window), (e.Stage, e.Shared, e.Window));
        At(w, e.AppearMs + 1000);
        Assert.Equal(new[] { WorldBossNoticeKind.Appeared }, Kinds(w));
        Assert.Equal(Boss.SoloHealth, e.MaxHealth);   // mode solo : la vie d'un seul joueur
        At(w, e.AnnounceAtMs + 1000);
        Assert.Equal(new[] { WorldBossNoticeKind.TargetAnnounced }, Kinds(w));
        Assert.NotNull(e.Target);
        At(w, e.LimitAtMs + 1000);
        Assert.Equal(new[] { WorldBossNoticeKind.Assault }, Kinds(w));
        // Une horloge reculée par l'hôte : l'événement attend, sans revenir en arrière.
        long now = e.NowMs;
        At(w, now - 600_000);
        Assert.Equal(now, e.NowMs);
        At(w, occ.SignsStartMs + WorldBossSchedule.LongestMs(Boss));
        Assert.Contains(WorldBossNoticeKind.ObjectiveDestroyed, Kinds(w));
        Assert.Equal(new[] { WorldBossNoticeKind.Victorious, WorldBossNoticeKind.Gone }, Kinds(w).Where(k => k >= WorldBossNoticeKind.Victorious));
        Assert.Null(w.WorldBossNow("grand_solitaire"));
        // Mémoire du vainqueur gardée dans le registre, relue de la sauvegarde ; il revient enragé à la prochaine occurrence.
        WorldBossMemory memory = w.WorldBossMemoryOf("grand_solitaire");
        Assert.Equal((0L, 1L, occ.Window, true), (memory.PlayerWins, memory.BossWins, memory.LastWindow, memory.Raged));
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!;
        Assert.Contains(saved.Character!.Events, x => x.Id == "zone.vallons/worldboss.grand_solitaire");
        WorldSession again = Open(saved);
        Assert.Equal(memory, again.WorldBossMemoryOf("grand_solitaire"));
        At(again, occ.SignsStartMs + 2000);   // même occurrence, déjà réglée : elle ne revient pas
        Assert.Null(again.WorldBossNow("grand_solitaire"));
        WorldBossOccurrence next = FirstOccurrence(occ.Window + 1);
        At(again, next.SignsStartMs + WorldBossEvent.StepMs);
        At(again, next.SignsStartMs + Boss.SignsMs + WorldBossEvent.StepMs);
        WorldBossEvent raged = again.WorldBossNow("grand_solitaire")!;
        Assert.Equal(("enrage", raged.MaxHealth), (raged.PhaseNow.Id, raged.Health));   // enragé d'emblée, à 100 % de vie
    }

    [Fact]
    public void Victoire_du_personnage_butin_de_la_premiere_victoire_une_seule_fois()
    {
        WorldBossOccurrence occ = FirstOccurrence();
        WorldSession w = Open();
        At(w, occ.SignsStartMs + Boss.SignsMs + 2000);
        WorldBossEvent e = w.WorldBossNow("grand_solitaire")!;
        Assert.True(e.IsFightable);
        Assert.True(e.Damage(w.CombatantId, e.MaxHealth));
        At(w, e.NowMs + 1000);
        Assert.Equal(new[] { WorldBossNoticeKind.Rewarded }, Kinds(w));
        WorldBossNotice reward = w.WorldBossNotices.Single();
        Assert.Contains(Boss.Loot.Trophy, reward.Items);
        Assert.Contains(Boss.Loot.Appearance, reward.Items);   // l'apparence à la première victoire, en attendant sa quête
        Assert.True(w.Inventory.Count(Boss.Loot.Trophy) >= 1);
        WorldBossMemory memory = w.WorldBossMemoryOf("grand_solitaire");
        Assert.Equal((1L, 0L, false), (memory.PlayerWins, memory.BossWins, memory.Raged));
        Assert.False(w.EventRegistry.Single(x => x.Id == w.WorldBossKey("grand_solitaire")).Owed);
        Assert.Null(w.WorldBossNow("grand_solitaire"));
        At(w, e.NowMs + 5000);
        Assert.Empty(w.WorldBossNotices);   // rien de plus
        // Seconde victoire : ni trophée ni apparence (la monture reste possible).
        WorldBossOccurrence next = FirstOccurrence(occ.Window + 1);
        At(w, next.SignsStartMs + Boss.SignsMs + 2000);
        WorldBossEvent e2 = w.WorldBossNow("grand_solitaire")!;
        e2.Damage(w.CombatantId, e2.MaxHealth);
        At(w, e2.NowMs + 1000);
        WorldBossNotice second = w.WorldBossNotices.Single(n => n.Kind == WorldBossNoticeKind.Rewarded);
        Assert.DoesNotContain(Boss.Loot.Trophy, second.Items);
        Assert.Equal(2L, w.WorldBossMemoryOf("grand_solitaire").PlayerWins);
    }

    [Fact]
    public void Meme_temps_serveur_meme_occurrence()
    {
        WorldBossOccurrence occ = FirstOccurrence();
        WorldSession a = Open(), b = Open();
        foreach (long t in new[] { occ.SignsStartMs + 1000, occ.SignsStartMs + Boss.SignsMs + 60_000, occ.SignsStartMs + Boss.SignsMs + Boss.AnnounceMs + 5000 })
        {
            At(a, t);
            At(b, t);
        }
        WorldBossEvent x = a.WorldBossNow("grand_solitaire")!, y = b.WorldBossNow("grand_solitaire")!;
        Assert.Equal((x.Site.Id, x.Position, x.Target?.Id, x.Stage), (y.Site.Id, y.Position, y.Target?.Id, y.Stage));
    }
}
