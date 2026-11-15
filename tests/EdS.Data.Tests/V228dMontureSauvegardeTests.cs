using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Characters;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.28d (2b) : la monture côté monde. Sauvegarde en version 22 (<c>character.mount</c>), cheval donné à la fin du chapitre 1,
/// transmis par <see cref="PlayerCharacters.Setup"/> hors des intérieurs, montée de 1 s par le pont temps réel (décisions de Damien du
/// 15 novembre 2026 : monter prend 1 seconde ; être frappé en selle ne fait pas descendre).
/// </summary>
[Trait("Category", "World")]
public class V228dMontureSauvegardeTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v228d_monture_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private string SavePath => Path.Combine(_folder, "partie", "sauvegarde.json");
    private static string Scenarios => Path.Combine(AppContext.BaseDirectory, "scenarios");

    private static CharacterProgression Progression =>
        ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;

    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;

    private static SavedStory Chapter1Done => new SavedStory(quests: new Dictionary<string, QuestProgress>
    {
        [PlayerCharacters.HorseQuest] = new QuestProgress(QuestProgress.Done),
    });

    private static SavedCharacter Hero(SavedStory? story = null)
    {
        SavedCharacter c = Inventory.Outfit(PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, Progression), Items);
        return story is null ? c : c.WithStory(story);
    }

    private AdventureSession Start(SavedCharacter hero)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        SaveStore.Write(SavePath, new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, hero));
        AdventureSession a = AdventureSession.Load(DataDirectory, Scenarios, SavePath);
        a.Advance(0.05);
        return a;
    }

    private static void Ticks(AdventureSession a, int count)
    {
        for (int i = 0; i < count; i++) a.Advance(0.05);
    }

    // --- Format (sauvegarde v22) ---------------------------------------------------------------------------------------

    [Fact]
    public void Version_22_monture_ecrite_et_relue()
    {
        Assert.Equal(22, SaveValidator.CurrentSchemaVersion);
        Assert.Equal(22, SaveValidator.MountSince);
        var save = new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero().WithMount(PlayerCharacters.Horse));
        string text = SaveJsonReader.Write(save);
        Assert.Contains("\"mount\": \"mount.cheval\"", text);
        SaveLoadResult back = SaveJsonReader.Read(text);
        Assert.True(back.Report.IsValid, string.Join("; ", back.Report.Errors));
        Assert.Equal(PlayerCharacters.Horse, back.Save!.Character!.Mount);
        Assert.Equal(text, SaveJsonReader.Write(back.Save));
        // Sans monture : pas de champ.
        Assert.DoesNotContain("\"mount\"", SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero())));
    }

    [Fact]
    public void Monture_inconnue_ou_trop_tot_refusee_au_bon_chemin()
    {
        string text = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero().WithMount(PlayerCharacters.Horse)));
        SaveLoadResult unknown = SaveJsonReader.Read(text.Replace("\"mount.cheval\"", "\"mount.licorne\""));
        Assert.Contains(unknown.Report.Errors, e => e.Path == "$.data.character.mount");
        SaveLoadResult early = SaveJsonReader.Read(text.Replace(SaveVersion.Current, "\"schemaVersion\": 21"));
        Assert.Contains(early.Report.Errors, e => e.Path == "$.data.character.mount" && e.Message.Contains("version 22"));
        // Une sauvegarde de version 20 sans monture est lue et mise à jour.
        string old = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero())).Replace(SaveVersion.Current, "\"schemaVersion\": 20");
        SaveLoadResult read = SaveJsonReader.Read(old);
        Assert.True(read.Report.IsValid, string.Join("; ", read.Report.Errors));
        Assert.Null(read.Save!.Character!.Mount);
        Assert.Contains(SaveVersion.Current, SaveJsonReader.Write(read.Save));
    }

    // --- Cheval donné, monture transmise -------------------------------------------------------------------------------

    [Fact]
    public void Cheval_donne_a_la_fin_du_chapitre_1_seulement()
    {
        Assert.Null(PlayerCharacters.GiveHorse(Hero(), SavedStory.Empty).Mount);
        SavedCharacter done = Hero(Chapter1Done);
        SavedCharacter given = PlayerCharacters.GiveHorse(done, done.Story);
        Assert.Equal(PlayerCharacters.Horse, given.Mount);
        Assert.Same(given, PlayerCharacters.GiveHorse(given, given.Story));   // idempotente
        Assert.Null(PlayerCharacters.MountOf(Hero()));
        Assert.Equal(MountSetup.HorseSpeed, PlayerCharacters.MountOf(given)!.Speed);
    }

    [Fact]
    public void Setup_transmet_la_monture_seulement_si_demande()
    {
        SavedCharacter hero = Hero().WithMount(PlayerCharacters.Horse);
        var placement = new CombatantPlacement(GroundVector.Parse("0", "0"), Fixed.Parse("0.5"));
        Assert.Null(PlayerCharacters.Setup(hero, Progression, ShippedData, placement).Mount);
        Assert.Equal(Fixed.FromInt(13), PlayerCharacters.Setup(hero, Progression, ShippedData, placement, withMount: true).Mount!.Speed);
        Assert.Null(PlayerCharacters.Setup(Hero(), Progression, ShippedData, placement, withMount: true).Mount);
    }

    [Fact]
    public void Sans_chapitre_1_pas_de_monture_dans_les_vallons()
    {
        AdventureSession a = Start(Hero());
        Assert.False(a.World.CanCallMount);
        Assert.False(a.Combat.HasMount);
        Assert.False(a.Combat.CallMount());
        Assert.Contains(a.Combat.DrainFeedback(), f => f.Kind == FeedbackKind.Unavailable && f.Detail == "pas de monture ici");
    }

    [Fact]
    public void Sauvegarde_d_avant_chapitre_1_fini_recoit_le_cheval_et_le_garde()
    {
        AdventureSession a = Start(Hero(Chapter1Done));
        Assert.True(a.World.CanCallMount);
        Assert.Equal(PlayerCharacters.Horse, a.World.Character.Mount);
        Assert.Equal(PlayerCharacters.Horse, a.World.Save().Character!.Mount);
    }

    // --- Montée de 1 s -------------------------------------------------------------------------------------------------

    [Fact]
    public void Monter_prend_une_seconde_puis_descendre_est_immediat()
    {
        AdventureSession a = Start(Hero(Chapter1Done));
        Assert.True(a.Combat.CallMount());
        Assert.True(a.Combat.Mounting);
        Assert.False(a.Combat.CallMount());   // montée déjà en cours
        Ticks(a, 19);
        Assert.False(a.Combat.Mounted);
        Ticks(a, 2);   // la commande part au 20e tick, appliquée au suivant
        Assert.True(a.Combat.Mounted, string.Join("; ", a.Combat.DrainFeedback().Select(f => f.Detail)));
        Assert.False(a.Combat.Mounting);
        Assert.False(a.Combat.CallMount());   // déjà en selle
        a.Combat.SendMountAway();
        Ticks(a, 2);
        Assert.False(a.Combat.Mounted);
    }

    [Fact]
    public void Marcher_pendant_la_montee_l_annule_et_on_ne_monte_pas_en_marche()
    {
        AdventureSession a = Start(Hero(Chapter1Done));
        Assert.True(a.Combat.CallMount());
        Ticks(a, 5);
        a.Combat.SetMove(1, 0);
        Ticks(a, 30);
        Assert.False(a.Combat.Mounting);
        Assert.False(a.Combat.Mounted);
        Assert.Contains(a.Combat.DrainFeedback(), f => f.Detail == "montée annulée");
        Assert.False(a.Combat.CallMount());   // en marche
        a.Combat.SetMove(0, 0);
        Ticks(a, 2);
        // Renvoyer pendant la montée l'annule aussi, sans descente à faire.
        Assert.True(a.Combat.CallMount());
        a.Combat.SendMountAway();
        Ticks(a, 25);
        Assert.False(a.Combat.Mounted);
    }

    [Fact]
    public void Montee_rejouable_dans_le_journal_des_commandes()
    {
        AdventureSession a = Start(Hero(Chapter1Done));
        Assert.True(a.Combat.CallMount());
        Ticks(a, 22);
        Assert.True(a.Combat.Mounted);
        CombatCommand mount = Assert.Single(a.Combat.SubmittedCommands, c => c.Action == CombatAction.Mount);
        Assert.Equal(AdventureSession.PlayerId, mount.Actor);
    }
}
