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

    /// <summary>Expérience cumulée des niveaux 3, 4, 5, 6, 7 et 10 (progression livrée).</summary>
    private const long Level3 = 220, Level4 = 360, Level5 = 520, Level6 = 700, Level7 = 900, Level10 = 1620;

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
        Assert.Contains("Nouvelle compétence en réserve : Provocation. Place-la dans ta barre depuis l'écran des compétences.", messages);
        Assert.DoesNotContain(messages, m => m.StartsWith("À ta prochaine halte, nouvelle compétence", StringComparison.Ordinal));
        SkillBook book = a.Skills();
        Assert.True(book.HasReserve(SkillKind.Active));
        Assert.Null(book.Card(Provocation)!.Place);
        Assert.Equal(new[] { Frappe, Heurt }, a.World.NextEntry.ActiveSkills);   // la halte ne la placera pas
        Assert.Null(a.PlaceSkill(SkillKind.Active, 1, Provocation));
        Assert.Equal(new[] { Frappe, Provocation }, a.World.Character.ActiveSkills);
    }

    // --- Une place de la barre peut rester vide ------------------------------------------------------------------------------

    /// <summary>Aventure sur les données livrées, depuis une sauvegarde de ce personnage (premier tick joué).</summary>
    private AdventureSession Load(SavedCharacter hero, IEnumerable<string>? consumed = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        SaveStore.Write(SavePath, new WorldSave("zone.vallons", 0, 0, consumed ?? Array.Empty<string>(), hero));
        AdventureSession a = AdventureSession.Load(DataDirectory, ScenariosDirectory, SavePath);
        a.DrainMessages();
        a.Advance(0.05);
        return a;
    }

    private static SavedCharacter Player(long xp) => PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, Progression).WithXp(xp);

    private static IReadOnlyList<string> Engaged(AdventureSession a) => a.World.Zone.Engine.View(AdventureSession.PlayerId).Character!.Build.ActiveSkills;

    private static string? EngagedUltimate(AdventureSession a) => a.World.Zone.Engine.View(AdventureSession.PlayerId).Character!.Build.Ultimate;

    /// <summary>Réponse de Damien du 30 octobre 2026 (« oui, permis ») : l'ultime retirée reste retirée, à la halte comme après relecture.</summary>
    [Fact]
    public void L_ultime_retiree_reste_retiree()
    {
        AdventureSession a = Load(Player(Level10));
        Assert.Equal(Absolu, a.World.Character.Ultimate);
        Assert.Equal(new[] { Barriere, Onde, Absolu }, a.World.Learned.Select(u => u.Skill));   // placées à l'entrée, données pour de bon
        Assert.True(a.World.Zone.IsConsumed("character/skill/" + Absolu));
        Assert.Null(a.RemoveSkill(SkillKind.Ultimate, 0));
        Assert.Contains("Compétences gardées : effet à ta prochaine halte.", a.DrainMessages());
        Assert.Null(a.World.Character.Ultimate);
        Assert.Null(a.World.NextEntry.Ultimate);   // l'écran montre la place vide : la halte ne la remplira pas
        Assert.Null(a.Skills().Place(SkillKind.Ultimate, 0)!.Skill);
        Assert.True(a.Rest());
        Assert.Null(EngagedUltimate(a));
        Assert.Empty(a.World.Learned);
        a.Save();
        AdventureSession again = AdventureSession.Load(DataDirectory, ScenariosDirectory, SavePath);
        Assert.Null(again.World.Character.Ultimate);
        Assert.Empty(again.World.Learned);
        Assert.Null(EngagedUltimate(again));
        // Le joueur la remet quand il veut.
        again.Advance(0.05);
        Assert.Null(again.PlaceSkill(SkillKind.Ultimate, 0, Absolu));
        Assert.Equal(Absolu, again.World.Character.Ultimate);
    }

    [Fact]
    public void Une_place_active_videe_reste_vide()
    {
        AdventureSession a = Load(Player(Level5));
        Assert.Equal(new[] { Frappe, Heurt, Barriere }, Engaged(a));
        Assert.Null(a.RemoveSkill(SkillKind.Active, 2));   // la Barrière va dans la réserve, Provocation y attend déjà
        Assert.Equal(new[] { Frappe, Heurt }, a.World.NextEntry.ActiveSkills);
        Assert.True(a.Skills().HasReserve(SkillKind.Active));
        Assert.True(a.Rest());
        Assert.Equal(new[] { Frappe, Heurt }, Engaged(a));
        Assert.Empty(a.World.Learned);
        // Une place du milieu aussi : les places se remplissent dans l'ordre, la dernière reste vide.
        Assert.Null(a.RemoveSkill(SkillKind.Active, 0));
        Assert.Equal(new[] { Heurt }, a.World.Character.ActiveSkills);
        a.Advance(0.05);   // le personnage arrive dans la nouvelle session
        Assert.True(a.Rest());
        Assert.Equal(new[] { Heurt }, Engaged(a));
        // Le joueur place lui-même Provocation, depuis la réserve.
        Assert.Null(a.PlaceSkill(SkillKind.Active, 1, Provocation));
        Assert.Equal(new[] { Heurt, Provocation }, a.World.Character.ActiveSkills);
    }

    /// <summary>
    /// Un niveau gagné en route : l'écran montre déjà la Barrière dans la place 3 (barre de la prochaine halte). Le joueur la retire : la
    /// halte ne la remet pas et ne l'annonce pas (elle est donnée quand il change sa barre).
    /// </summary>
    [Fact]
    public void Retirer_une_competence_gagnee_en_route()
    {
        AdventureSession a = Load(Player(Level5 - 1));
        ((INarrativeWorld)a.World).GiveXp(1);   // niveau 5 en route
        Assert.Equal(Barriere, a.Skills().Place(SkillKind.Active, 2)!.Skill);
        Assert.Null(a.RemoveSkill(SkillKind.Active, 2));
        Assert.Equal(new[] { Frappe, Heurt }, a.World.NextEntry.ActiveSkills);
        a.DrainMessages();
        Assert.True(a.Rest());
        Assert.Equal(new[] { Frappe, Heurt }, Engaged(a));
        Assert.Empty(a.World.Learned);
        Assert.DoesNotContain(a.DrainMessages(), m => m.StartsWith("Nouvelle compétence", StringComparison.Ordinal));
    }

    /// <summary>
    /// Sauvegarde d'avant (aucune clé « compétence donnée ») : à la première entrée, les compétences avec leur place déjà dans la barre
    /// sont données sans rien changer ni annoncer ; une qui manquait (place libre) est placée une dernière fois, comme avant.
    /// </summary>
    [Fact]
    public void Sauvegarde_d_avant()
    {
        SavedCharacter full = PlayerCharacters.Learn(Player(Level10), Progression, Rules, out _);
        AdventureSession a = Load(full);
        Assert.Empty(a.World.Learned);
        Assert.Equal(full.ActiveSkills, a.World.Character.ActiveSkills);
        Assert.All(new[] { Barriere, Onde, Absolu }, s => Assert.True(a.World.Zone.IsConsumed("character/skill/" + s), s));
        Assert.False(a.World.Zone.IsConsumed("character/skill/" + Provocation));   // la réserve n'a pas de clé : jamais placée d'office
        a.Save();
        Assert.Contains("character/skill/" + Barriere, SaveStore.Read(SavePath).Save!.Consumed);
        // Niveau 5 sans la Barrière, place libre, pas de clé : elle est placée à l'entrée.
        AdventureSession b = Load(Bastion(Level5, new[] { Frappe, Heurt }));
        Assert.Equal(new[] { Barriere }, b.World.Learned.Select(u => u.Skill));
        Assert.Equal(new[] { Frappe, Heurt, Barriere }, Engaged(b));
        // Avec la clé (elle a déjà été donnée, puis retirée) : la place reste vide.
        AdventureSession c = Load(Bastion(Level5, new[] { Frappe, Heurt }), new[] { "character/skill/" + Barriere });
        Assert.Empty(c.World.Learned);
        Assert.Equal(new[] { Frappe, Heurt }, Engaged(c));
    }

    [Fact]
    public void Learn_ne_redonne_pas_une_competence_deja_donnee()
    {
        SavedCharacter hero = Bastion(Level10, new[] { Frappe, Heurt });
        SavedCharacter placed = PlayerCharacters.Learn(hero, Progression, Rules, _ => false, out IReadOnlyList<SkillUnlock> learned, out IReadOnlyList<SkillUnlock> handed);
        Assert.Equal(new[] { Barriere, Onde, Absolu }, learned.Select(u => u.Skill));
        Assert.Equal(new[] { Barriere, Onde, Absolu }, handed.Select(u => u.Skill));   // jamais Provocation ni Contrecoup (réserve)
        Assert.Same(hero, PlayerCharacters.Learn(hero, Progression, Rules, _ => true, out learned, out handed));
        Assert.Empty(learned);
        Assert.Empty(handed);
        // Une seule déjà donnée : les autres le sont maintenant.
        SavedCharacter some = PlayerCharacters.Learn(hero, Progression, Rules, k => k == "character/skill/" + Barriere, out learned, out handed);
        Assert.Equal(new[] { Onde, Absolu }, learned.Select(u => u.Skill));
        Assert.Equal(new[] { Frappe, Heurt, Onde }, some.ActiveSkills);
        Assert.Equal(new[] { Frappe, Heurt, Barriere, Onde }, placed.ActiveSkills);
        Assert.Throws<ArgumentNullException>(() => PlayerCharacters.Learn(hero, Progression, Rules, null!, out _, out _));
    }

    // --- Relecture indépendante de la PR #74 ------------------------------------------------------------------------------------

    /// <summary>
    /// Sonde de la relecture, devenue test : registre presque plein. Avant la correction, la clé de l'ultime ne s'inscrivait pas ; retirée,
    /// elle réapparaissait dans la barre de la prochaine halte, revenait à chaque halte et « Nouvelle ultime » était annoncée de nouveau ; et
    /// l'inscription pouvait n'être que partielle. Désormais : à l'entrée, toutes les clés ou aucune ; un changement de barre qui demande des
    /// clés sans la place est refusé en entier, rien ne change.
    /// </summary>
    [Theory]
    [InlineData(2, false)]   // place pour 2 clés sur 3 (plus les morts possibles d'un tick) : aucune inscrite
    [InlineData(3, true)]    // place pour les 3 : toutes inscrites
    public void Registre_presque_plein_tout_ou_rien(int extra, bool given)
    {
        SavedCharacter hero = Player(Level10);
        string safeKey = Zone().SafePointKey(Zone().Entry.Id);   // point d'entrée déjà découvert : sa clé ne prend pas de place
        int combatants = Load(hero, new[] { safeKey }).World.Zone.Engine.CombatantIds.Count;
        string[] keys = Enumerable.Range(0, SaveValidator.MaxConsumed - combatants - extra - 1).Select(i => $"probe/k{i:D6}").Append(safeKey).ToArray();
        AdventureSession a = Load(hero, keys);
        Assert.Equal(new[] { Barriere, Onde, Absolu }, a.World.Learned.Select(u => u.Skill));   // placées à l'entrée dans tous les cas
        string[] skillKeys = new[] { Barriere, Onde, Absolu }.Select(k => "character/skill/" + k).ToArray();
        Assert.All(skillKeys, k => Assert.Equal(given, a.World.Zone.IsConsumed(k)));   // toutes ou aucune
        SavedCharacter before = a.World.Character;
        string? why = a.RemoveSkill(SkillKind.Ultimate, 0);
        if (given)
        {
            Assert.Null(why);
            Assert.Null(a.World.NextEntry.Ultimate);
            return;
        }
        Assert.Equal("Le registre du monde est plein : ce changement de compétences ne peut pas être gardé.", why);
        Assert.Same(before, a.World.Character);
        Assert.Equal(Absolu, a.World.NextEntry.Ultimate);
        Assert.Equal(BuildViolationCode.RegistryFull, a.World.SetSkills(new[] { Frappe, Heurt, Barriere, Onde }, null, new[] { Roc })!.Code);
        Assert.Same(before, a.World.Character);
        Assert.All(skillKeys, k => Assert.False(a.World.Zone.IsConsumed(k)));
        // À la halte : l'ultime est toujours là, et rien n'est annoncé de nouveau.
        a.DrainMessages();
        Assert.True(a.Rest());
        Assert.Equal(Absolu, EngagedUltimate(a));
        Assert.Empty(a.World.Learned);
        Assert.DoesNotContain(a.DrainMessages(), m => m.StartsWith("Nouvelle", StringComparison.Ordinal));
    }

    /// <summary>
    /// Propriété (relecture indépendante de la PR #74), avec le vrai registre : après tout changement de barre admis, la barre de la
    /// prochaine halte est exactement celle que le joueur a gardée ; et une halte (session suivante) l'engage telle quelle, sans rien
    /// annoncer. 300 barres tirées au hasard (graine fixe) dans un petit univers, depuis cinq départs (niveaux gagnés en route compris).
    /// </summary>
    [Fact]
    public void Propriete_apres_un_changement_admis_la_halte_garde_la_barre()
    {
        string[] actives = { Frappe, Heurt, Barriere, Onde, Provocation, Contrecoup };
        string[] passives = { Roc, "skill.bastion.bouclier_lourd" };
        var random = new Random(74);
        ZoneDefinition zone = Zone();
        int admitted = 0;
        foreach ((long xp, bool enRoute) in new[] { (0L, false), (Level5 - 1, true), (Level5, false), (Level10 - 1, true), (Level10, false) })
        {
            WorldSession w = Play(zone, Player(xp));
            if (enRoute) ((INarrativeWorld)w).GiveXp(1);
            SkillBar? last = null;
            for (int i = 0; i < 60; i++)
            {
                string[] a = actives.OrderBy(_ => random.Next()).Take(random.Next(0, 5)).ToArray();
                string? u = random.Next(3) == 0 ? null : Absolu;
                string[] p = passives.OrderBy(_ => random.Next()).Take(random.Next(0, 3)).ToArray();
                if (w.SetSkills(a, u, p) != null) continue;
                admitted++;
                last = new SkillBar(a, u, p);
                Assert.True(last.SameAs(SkillBar.Of(w.NextEntry)), $"{xp} : [{string.Join(",", a)}] {u} [{string.Join(",", p)}]");
                Assert.True(last.SameAs(SkillBar.Of(w.Character)));
            }
            if (last is null) continue;
            var next = new WorldSession(w.Save(), zone, ShippedData, Progression, ItemsCatalog, Book(zone), market: new Market(ShippedMerchants));
            Assert.Empty(next.Learned);
            Assert.True(last.SameAs(SkillBar.Of(next.Character)));
            Assert.True(last.SameAs(SkillBar.Of(next.NextEntry)));
        }
        Assert.True(admitted >= 30, admitted.ToString());
    }

    /// <summary>
    /// Relecture indépendante de la PR #74 : une boutique ouverte au crépuscule, quand la nuit tombe, dit « fermée » sur chaque offre
    /// (comme l'achat), au lieu de proposer des achats refusés.
    /// </summary>
    [Fact]
    public void Offres_d_une_boutique_fermee_entre_temps()
    {
        (string data, string scenarios) = DataCopy(Near("merchant.armurier"));
        SaveStore.Write(SavePath, new WorldSave("zone.vallons", TickAt(0, 21 * 60 + 58), 0, Array.Empty<string>(), Hero(100_000)));
        AdventureSession a = Adventure(SavePath, data, scenarios);
        Assert.True(a.OpenShop("merchant.armurier"));
        Assert.All(a.ShopOffers(), o => Assert.Equal(TradeRefusal.None, o.Refusal));
        for (int i = 0; i < 600 && a.World.Zone.Time.Phase != DayPhase.Night; i++) a.Advance(0.05);
        Assert.Equal(DayPhase.Night, a.World.Zone.Time.Phase);
        Assert.Equal("merchant.armurier", a.Shop);
        Assert.NotEmpty(a.ShopOffers());
        Assert.All(a.ShopOffers(), o => Assert.Equal(TradeRefusal.Closed, o.Refusal));
        Assert.Equal(TradeRefusal.Closed, a.Buy(a.ShopOffers()[0].Item));
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
        File.Copy(ScenarioPath("zones/vallons_bandits"), Path.Combine(scenarios, "zones", "vallons_bandits.json"), true);   // sous-lot V1.6 : formes du chef des bandits
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
        // Bertille garde de quoi vendre chaque jour : les potions de soin (et, depuis le sous-lot V1.2, la pioche du carrier), plus une
        // offre tirée parmi deux.
        Merchant bertille = ShippedMerchants.Merchant("merchant.epicier");
        Assert.Equal(new[] { "item.potion_de_soin", "item.pioche_du_carrier" }, bertille.Offers.Where(o => o.Always).Select(o => o.Item));
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
