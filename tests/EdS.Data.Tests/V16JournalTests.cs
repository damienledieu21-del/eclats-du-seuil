using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Zones;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.6 : l'aventure côté interface : journal à deux onglets (principales, secondaires), quête suivie montrée sous la carte et
/// objectif sur la carte, récompenses au choix, PNJ des intérieurs à qui parler et qui ont un corps dans le décor de leur pièce.
/// </summary>
[Trait("Category", "Runtime")]
public class V16JournalTests : IDisposable
{
    private const string Peaux = "quest.peaux_pour_l_auberge", Moulin = "quest.traces_sous_le_moulin";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v16_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private string SavePath => Path.Combine(_folder, "partie", "sauvegarde.json");

    /// <summary>Aventure reprise dans la zone <paramref name="zone"/> avec ces quêtes.</summary>
    private AdventureSession With(string zone, IReadOnlyDictionary<string, QuestProgress> quests, string? tracked = null, params ItemStack[] items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        SavedCharacter hero = Hero(0, items).WithStory(new SavedStory(new[] { "fact.traces.recit" }, quests, null, null, tracked));
        SaveStore.Write(SavePath, new WorldSave(zone, 0, 1, Array.Empty<string>(), hero));
        return Adventure(SavePath);
    }

    [Fact]
    public void Journal_a_deux_onglets()
    {
        AdventureSession a = With("zone.vallons", new Dictionary<string, QuestProgress>
        {
            [Moulin] = new QuestProgress("traces"), [Peaux] = new QuestProgress("chasse", 1), ["quest.bete_blanche"] = new QuestProgress(QuestProgress.Done),
        });
        JournalEntry main = Assert.Single(a.Journal(QuestCategory.Main));
        Assert.Equal((Moulin, false), (main.Id, main.Done));
        IReadOnlyList<JournalEntry> side = a.Journal(QuestCategory.Side);
        Assert.Equal(new[] { (Peaux, false), ("quest.bete_blanche", true) }, side.Select(q => (q.Id, q.Done)));
        Assert.Equal(("Des peaux pour l'auberge", "1/3"), (side[0].Name, side[0].Progress));
        Assert.Equal(3, a.Journal().Count);
        // Sans quête suivie : la première principale en cours est montrée.
        Assert.Equal(Moulin, a.Followed!.Id);
        Assert.True(a.Track(Peaux));
        Assert.Equal(Peaux, a.Followed!.Id);
        Assert.True(a.Followed.Tracked);
        Assert.False(a.Track("quest.bete_blanche"));
        // L'objectif sur la carte suit la quête suivie : le loup le plus proche (famille « loup »).
        MapMarker objective = a.Objective!;
        string[] wolves = a.World.Definition!.Populations.Where(p => p.Family == "loup").Select(p => p.Creature).ToArray();
        Assert.Contains(a.World.Definition.Populations.Where(p => wolves.Contains(p.Creature)),
            p => p.Template.Placement!.Position.X.Raw / 10000.0 == objective.X && p.Template.Placement.Position.Z.Raw / 10000.0 == objective.Z);
        // La quête suivie est sauvegardée.
        a.Save();
        Assert.Equal(Peaux, SaveStore.Read(SavePath).Save!.Character!.Story.Tracked);
    }

    [Fact]
    public void Objectif_chez_un_PNJ_d_interieur_puis_dans_la_piece()
    {
        // Dehors, livrer à Mathurin : l'objectif est la porte de l'auberge.
        AdventureSession a = With("zone.vallons", new Dictionary<string, QuestProgress> { [Peaux] = new QuestProgress("peaux") }, Peaux);
        Door inn = a.World.Definition!.Doors.Single(d => d.To == "zone.auberge");
        Assert.Equal((inn.Position.X.Raw / 10000.0, inn.Position.Z.Raw / 10000.0), (a.Objective!.X, a.Objective.Z));
        // Dans l'auberge : Mathurin lui-même, qui a un corps dans le décor de la pièce.
        AdventureSession inside = With("zone.auberge", new Dictionary<string, QuestProgress> { [Peaux] = new QuestProgress("peaux") }, Peaux);
        Assert.True(inside.Indoors);
        Assert.Equal((3.75, 3.55), (inside.Objective!.X, inside.Objective.Z));
        Assert.Equal("npc.aubergiste", Assert.Single(inside.Npcs()).Id);
        Assert.Contains(("npc.aubergiste", 3.75, 3.55), inside.Map.Speakers);
        StyleVillager body = Assert.Single(InteriorRules.Layout(inside.Map).Villagers, v => v.Id == "npc.aubergiste");
        Assert.Equal((3.75, 3.55), (body.X, body.Z));
        // Aucun villageois de décor ne se pose sur lui (l'aubergiste du décor laisse sa place).
        Assert.All(InteriorRules.Layout(inside.Map).Villagers.Where(v => v.Id != "npc.aubergiste"),
            v => Assert.True((v.X - 3.75) * (v.X - 3.75) + (v.Z - 3.55) * (v.Z - 3.55) > 0.8, v.Id));
    }

    [Fact]
    public void Recompense_au_choix_dans_le_journal()
    {
        AdventureSession a = With("zone.vallons", new Dictionary<string, QuestProgress> { [Peaux] = new QuestProgress(QuestProgress.Reward) }, Peaux);
        JournalEntry q = Assert.Single(a.Journal(QuestCategory.Side));
        Assert.True(q.ChoosingReward);
        Assert.Equal(new[] { "3 × Potion de soin", "Remède de l'aube" }, q.RewardChoices);
        Assert.Equal("Choisis ta récompense.", q.Step);
        Assert.Null(a.Objective);   // rien à atteindre : le choix se fait dans le journal
        a.DrainMessages();
        Assert.Equal(DialogueRefusal.None, a.ChooseReward(Peaux, 0));
        Assert.Contains(a.DrainMessages(), m => m.Contains("Des peaux pour l'auberge", StringComparison.Ordinal));
        Assert.True(Assert.Single(a.Journal(QuestCategory.Side)).Done);
        Assert.Equal(3, a.World.Inventory.Count("item.potion_de_soin"));
        Assert.Null(a.Followed);
    }

    [Fact]
    public void Aide_des_touches_dit_le_journal()
    {
        IReadOnlyList<HelpKey> keys = HelpKeys.Adventure(AdventureText("text.ui.help"));
        Assert.Equal("Journal des quêtes (suivre, récompenses)", keys.Single(k => k.Key == "J").Action);
    }

    private static string AdventureText(string key) => Texts.Get(key);
}
