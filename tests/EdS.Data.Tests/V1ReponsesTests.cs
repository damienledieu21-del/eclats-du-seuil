using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Json;
using EdS.Data.Merchants;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Réponses de Damien du 30 octobre 2026 (22 h 50) : Provocation (niveau 3) et Contrecoup (niveau 7) s'apprennent, dans la réserve ; une
/// place de la barre peut rester vide ; Garin l'armurier et Bertille l'épicière ferment la nuit, Sauge l'herboriste reste ouverte ; le
/// minerai de fer ne s'achète plus (la ferrure du moulin demande d'aller miner).
/// </summary>
[Trait("Category", "World")]
public class V1ReponsesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v1_reponses_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private string SavePath => Path.Combine(_folder, "partie", "sauvegarde.json");

    /// <summary>Tick du monde à 23 h du jour 0 (le monde commence à 8 h) : en pleine nuit, quelle que soit la durée de la journée.</summary>
    private static long NightTick => new WorldClock(50).TicksPerDay * (23 - 8) / 24;

    private const string Frappe = "skill.bastion.frappe_du_rempart", Heurt = "skill.bastion.heurt_de_bouclier", Barriere = "skill.bastion.barriere_d_acier",
        Onde = "skill.bastion.onde_de_rupture", Provocation = "skill.bastion.provocation", Contrecoup = "skill.bastion.contrecoup",
        Absolu = "skill.bastion.bastion_absolu", Roc = "skill.bastion.endurance_du_roc";

    /// <summary>Expérience cumulée des niveaux 2, 3, 4, 5, 6, 7 et 10 (progression livrée).</summary>
    private const long Level2 = 100, Level3 = 220, Level4 = 360, Level5 = 520, Level6 = 700, Level7 = 900, Level10 = 1620;

    private static ProgressionRules Rules => ShippedData.Rules.Progression;

    private static SavedCharacter Bastion(long xp, string[] actives, string? ultimate = null) =>
        PlayerCharacters.Create("bastion", "class.bastion", Progression).WithXp(xp).WithSkills(actives, ultimate, new[] { Roc });

    // --- Provocation et Contrecoup s'apprennent, dans la réserve ---------------------------------------------------------------

    [Fact]
    public void Provocation_et_Contrecoup_s_apprennent_dans_la_reserve()
    {
        Assert.Equal(new[] { Provocation }, PlayerCharacters.Learned(Bastion(Level3, new[] { Frappe, Heurt }), Progression).Except(new[] { Frappe, Heurt, Roc }));
        Assert.DoesNotContain(Contrecoup, PlayerCharacters.Learned(Bastion(Level6, new[] { Frappe, Heurt, Barriere }), Progression));
        Assert.Contains(Contrecoup, PlayerCharacters.Learned(Bastion(Level7, new[] { Frappe, Heurt, Barriere }), Progression));
        // La barre a de la place (une sauvegarde à une seule active) : la halte n'y met pas Provocation, ni Contrecoup.
        foreach (long xp in new[] { Level3, Level4 })
        {
            SavedCharacter hero = Bastion(xp, new[] { Frappe });
            Assert.Same(hero, PlayerCharacters.Learn(hero, Progression, Rules, out IReadOnlyList<SkillUnlock> none));
            Assert.Empty(none);
        }
        SavedCharacter seven = Bastion(Level7, new[] { Frappe, Heurt });
        SavedCharacter after = PlayerCharacters.Learn(seven, Progression, Rules, out IReadOnlyList<SkillUnlock> gained);
        Assert.Equal(new[] { Barriere }, gained.Select(u => u.Skill));   // la Barrière vient avec sa place ; Provocation et Contrecoup attendent
        Assert.Equal(new[] { Frappe, Heurt, Barriere }, after.ActiveSkills);
        // Au niveau 5, la troisième place va à la Barrière (décision du 25 octobre 2026), pas à Provocation, apprise plus tôt.
        SavedCharacter five = PlayerCharacters.Learn(Bastion(Level5, new[] { Frappe, Heurt }), Progression, Rules, out gained);
        Assert.Equal(new[] { Barriere }, gained.Select(u => u.Skill));
        Assert.Equal(new[] { Frappe, Heurt, Barriere }, five.ActiveSkills);
        // Au niveau 10 : l'Onde prend la quatrième place, l'ultime arrive ; Provocation et Contrecoup restent dans la réserve.
        SavedCharacter ten = PlayerCharacters.Learn(Bastion(Level10, new[] { Frappe, Heurt, Barriere }), Progression, Rules, out gained);
        Assert.Equal(new[] { Onde, Absolu }, gained.Select(u => u.Skill));
        Assert.Equal(new[] { Frappe, Heurt, Barriere, Onde }, ten.ActiveSkills);
        // Le joueur les place lui-même : le juge l'admet.
        Assert.Null(PlayerCharacters.CheckSkills(ten, new[] { Provocation, Heurt, Barriere, Contrecoup }, Absolu, new[] { Roc }, Progression,
            ShippedData.Classes.Single(c => c.Definition.Id == "class.bastion").Definition, Rules));
    }

    /// <summary>
    /// Dans l'aventure : le niveau 3 gagné en route annonce Provocation dans la réserve (elle est apprise tout de suite, pas à la halte) ;
    /// l'écran des compétences la montre en réserve et le joueur la place lui-même.
    /// </summary>
    [Fact]
    public void Le_niveau_3_annonce_Provocation_en_reserve()
    {
        AdventureSession a = KillWeakWolf(Level3 - 1);
        IReadOnlyList<string> messages = a.DrainMessages();
        Assert.Contains("Niveau 3 ! Il prendra effet à ta prochaine halte.", messages);
        Assert.Contains("Nouvelle compétence en réserve : Provocation. Place-la dans ta barre avec l'écran des compétences (B).", messages);
        Assert.DoesNotContain(messages, m => m.StartsWith("À ta prochaine halte, nouvelle compétence", StringComparison.Ordinal));
        SkillBook book = a.Skills();
        Assert.True(book.HasReserve(SkillKind.Active));
        Assert.Null(book.Card(Provocation)!.Place);
        Assert.Equal(new[] { Frappe, Heurt }, a.World.NextEntry.ActiveSkills);   // la halte ne la placera pas
        Assert.Null(a.PlaceSkill(SkillKind.Active, 1, Provocation));
        Assert.Equal(new[] { Frappe, Provocation }, a.World.Character.ActiveSkills);
    }

    /// <summary>Partie à <paramref name="xp"/> près d'un loup affaibli (1 PV : 1 point d'expérience), tué par le Bastion.</summary>
    private AdventureSession KillWeakWolf(long xp)
    {
        string scenarios = Path.Combine(_folder, "scenarios");
        Directory.CreateDirectory(Path.Combine(scenarios, "zones"));
        JsonObject vallons = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
        foreach (JsonNode? c in vallons["data"]!["combatants"]!.AsArray())
            if ((string)c!["id"]! == "loup1")
            {
                c["position"] = new JsonArray(-15, -12);
                c["health"] = 1;
            }
        File.WriteAllText(Path.Combine(scenarios, "zones", "vallons.json"), vallons.ToJsonString());
        File.Copy(ScenarioPath("zones/vallons_chef"), Path.Combine(scenarios, "zones", "vallons_chef.json"));
        CopyMissingZoneScenarios(scenarios);
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        SaveStore.Write(SavePath, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(),
            PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, Progression).WithXp(xp)));
        AdventureSession a = AdventureSession.Load(DataDirectory, scenarios, SavePath);
        a.DrainMessages();
        CombatEngine e = a.World.Zone.Engine;
        for (int i = 0; i < 400 && !e.View("loup1").IsDead; i++)
        {
            long now = e.CurrentTick * 50;
            CombatantView me = e.View(AdventureSession.PlayerId), wolf = e.View("loup1");
            if (me.Position is GroundVector p && wolf.Position is GroundVector w && wolf.IsPresent)
            {
                double dx = (w.X.Raw - p.X.Raw) / 1e4, dz = (w.Z.Raw - p.Z.Raw) / 1e4;
                a.Combat.SetMove(Math.Sqrt(dx * dx + dz * dz) > 2 ? dx : 0, Math.Sqrt(dx * dx + dz * dz) > 2 ? dz : 0);
                try { e.Submit(new CombatCommand(now, AdventureSession.PlayerId, Frappe, "loup1")); }
                catch (ArgumentException) { }
            }
            a.Advance(0.05);
        }
        Assert.True(e.View("loup1").IsDead);
        a.Combat.SetMove(0, 0);
        return a;
    }

    // --- Marchands fermés la nuit, sauf l'herboriste ---------------------------------------------------------------------------

    [Fact]
    public void Garin_et_Bertille_ferment_la_nuit_Sauge_jamais()
    {
        Assert.Equal(new[] { "night" }, ShippedMerchants.Merchant("merchant.armurier").Closed);
        Assert.Equal(new[] { "night" }, ShippedMerchants.Merchant("merchant.epicier").Closed);
        Assert.Empty(ShippedMerchants.Merchant("merchant.herboriste").Closed);
    }

    [Theory]
    [InlineData("merchant.armurier")]
    [InlineData("merchant.epicier")]
    [InlineData("merchant.herboriste")]
    public void La_nuit_sur_les_vraies_donnees(string merchant)
    {
        (double x, double z) = Near(merchant);
        ZoneDefinition zone = Zone(x, z);
        bool closesAtNight = merchant != "merchant.herboriste";
        // Le jour (le monde commence à 8 h) : tout est ouvert.
        WorldSession day = Play(zone, Hero(1000));
        Assert.Equal(DayPhase.Day, day.Zone.Time.Phase);
        Assert.Equal(TradeRefusal.None, day.CanTrade(merchant));
        // La nuit : Garin et Bertille ferment, Sauge reste ouverte. Un refus ne change rien.
        var night = new WorldSession(new WorldSave("zone.vallons", NightTick, 0, Array.Empty<string>(), Hero(1000)), zone, ShippedData, Progression,
            ItemsCatalog, Book(zone), market: new Market(ShippedMerchants));
        night.Step();
        Assert.Equal(DayPhase.Night, night.Zone.Time.Phase);
        string before = Snapshot(night);
        Assert.Equal(closesAtNight ? TradeRefusal.Closed : TradeRefusal.None, night.CanTrade(merchant));
        string item = night.Stock(merchant)[0].Item;
        Assert.Equal(closesAtNight ? TradeRefusal.Closed : TradeRefusal.None, night.Buy(merchant, item));
        if (closesAtNight) Assert.Equal(before, Snapshot(night));
    }

    /// <summary>Tick du monde où il est <paramref name="minute"/> au jour <paramref name="day"/> (le monde commence à 8 h du jour 0).</summary>
    private static long TickAt(long day, int minute)
    {
        var clock = new WorldClock(50);
        long tick = (day * WorldClock.MinutesPerDay + minute - WorldClock.DefaultStartMinute) * clock.TicksPerDay / WorldClock.MinutesPerDay;
        Assert.Equal(new WorldTime(day, minute), clock.At(tick));
        return tick;
    }

    /// <summary>Décision de Damien du 30 octobre 2026 (23 h 06) : la nuit va de 22 h à 5 h ; Garin et Bertille ferment à 22 h et rouvrent à 5 h.</summary>
    [Theory]
    [InlineData(0, 21 * 60 + 59, DayPhase.Dusk, true)]
    [InlineData(0, 22 * 60, DayPhase.Night, false)]
    [InlineData(1, 4 * 60 + 59, DayPhase.Night, false)]
    [InlineData(1, 5 * 60, DayPhase.Dawn, true)]
    public void Fermes_de_22_h_a_5_h(long day, int minute, DayPhase phase, bool open)
    {
        foreach (string merchant in new[] { "merchant.armurier", "merchant.epicier", "merchant.herboriste" })
        {
            (double x, double z) = Near(merchant);
            ZoneDefinition zone = Zone(x, z);
            var w = new WorldSession(new WorldSave("zone.vallons", TickAt(day, minute), 0, Array.Empty<string>(), Hero(1000)), zone, ShippedData, Progression,
                ItemsCatalog, Book(zone), market: new Market(ShippedMerchants));
            w.Step();
            Assert.Equal(phase, w.Zone.Time.Phase);
            bool expected = open || merchant == "merchant.herboriste";
            Assert.Equal(expected ? TradeRefusal.None : TradeRefusal.Closed, w.CanTrade(merchant));
        }
    }

    /// <summary>
    /// Depuis la relecture de la PR #72, un marchand fermé reste un PNJ : il dit « Boutique fermée à cette heure. » puis ouvre son
    /// dialogue. Vérifié ici sur les marchands livrés (seul le point d'entrée est déplacé près de chacun).
    /// </summary>
    [Theory]
    [InlineData("merchant.armurier", "npc.armurier")]
    [InlineData("merchant.epicier", "npc.epiciere")]
    [InlineData("merchant.herboriste", "npc.herboriste")]
    public void La_nuit_le_marchand_ferme_parle_quand_meme(string merchant, string npc)
    {
        (string data, string scenarios) = DataCopy(Near(merchant));
        SaveStore.Write(SavePath, new WorldSave("zone.vallons", NightTick, 0, Array.Empty<string>(), Hero(500)));
        AdventureSession a = Adventure(SavePath, data, scenarios);
        a.DrainMessages();
        Assert.Equal(DayPhase.Night, a.World.Zone.Time.Phase);
        Assert.Equal(npc, a.NpcInReach!.Id);
        Assert.True(a.Interact());
        if (merchant == "merchant.herboriste")
        {
            Assert.Equal(merchant, a.Shop);   // Sauge ouvre la nuit aussi
            return;
        }
        Assert.Null(a.Shop);
        Assert.NotNull(a.Dialogue);
        Assert.Equal("Boutique fermée à cette heure.", a.DrainMessages().First());
        a.CloseDialogue();
        Assert.False(a.OpenShop(merchant));
    }

    // --- Le minerai de fer ne s'achète plus ------------------------------------------------------------------------------------

    [Fact]
    public void Le_minerai_de_fer_ne_s_achete_plus()
    {
        foreach (Merchant m in ShippedMerchants.Merchants)
            Assert.DoesNotContain(m.Offers, o => o.Item == "item.minerai_de_fer");
        // Il se revend toujours (valeur de base gardée) : un minerai miné rapporte quelques pièces.
        Assert.Equal(6, new Market(ShippedMerchants).UnitPrice("merchant.epicier", "item.minerai_de_fer"));
        // Bertille garde de quoi vendre chaque jour : les potions de soin, plus une offre tirée parmi deux.
        Merchant bertille = ShippedMerchants.Merchant("merchant.epicier");
        Assert.Equal(new[] { "item.potion_de_soin" }, bertille.Offers.Where(o => o.Always).Select(o => o.Item));
        Assert.Equal((1, 2), (bertille.Daily, bertille.Offers.Count(o => !o.Always)));
    }

    // --- Outils ------------------------------------------------------------------------------------------------------------------

    /// <summary>Copie des données et scénarios livrés, le point d'entrée déplacé en <paramref name="entry"/>.</summary>
    private (string Data, string Scenarios) DataCopy((double X, double Z) entry)
    {
        string data = Path.Combine(_folder, "data", "v0"), scenarios = Path.Combine(_folder, "scenarios");
        CopyTree(DataDirectory, data);
        CopyTree(ScenariosDirectory, scenarios);
        string zone = ZoneJsonReader.PathIn(data, "zone.vallons");
        JsonObject root = JsonNode.Parse(File.ReadAllText(zone))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(entry.X, entry.Z);
        File.WriteAllText(zone, root.ToJsonString());
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        return (data, scenarios);
    }

    private static void CopyTree(string from, string to)
    {
        foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }
}
