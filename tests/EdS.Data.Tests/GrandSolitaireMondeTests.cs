using System.Text.Json.Nodes;
using EdS.Data.Items;
using EdS.Data.Story;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
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
    private static string ScenariosDirectory => Path.Combine(AppContext.BaseDirectory, "scenarios");
    private static TextCatalog Texts => StoryJsonReader.ReadTextsFile(StoryJsonReader.TextsPathIn(DataDirectory)).Texts!;
    private static readonly Lazy<WorldBossBook> Book = new(() => Load().Book!);

    private static WorldBossLoadResult Load(Action<JsonObject>? mutate = null)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(WorldBossJsonReader.PathIn(DataDirectory)))!.AsObject();
        mutate?.Invoke(root);
        return WorldBossJsonReader.Read(root.ToJsonString(), Texts, Zone(), Items.Value, ScenariosDirectory, ShippedData);
    }

    private static JsonObject B(JsonObject root) => root["data"]!["bosses"]![0]!.AsObject();

    private static WorldBossDefinition Boss => Book.Value.Boss("grand_solitaire");

    private static WorldSession Open(WorldSave? save = null, string? safePoint = null)
    {
        ZoneDefinition zone = Zone();
        var hero = PlayerCharacters.Create("bastion", "class.bastion", Progression);
        var session = new WorldSession(save ?? new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone, ShippedData,
            Progression, ItemsCatalog, Book(zone), safePoint: safePoint);
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

    [Fact]
    public void Schema_2_une_forme_par_phase_dans_le_scenario_des_formes()
    {
        WorldBossLoadResult r = Load();
        Assert.True(r.IsValid, r.Report.ToString());
        WorldBossDefinition b = r.Book!.Boss("grand_solitaire");
        Assert.Equal((2, 2, "0.2.0-provisoire"), (r.Book.SchemaVersion, WorldBossValidator.CurrentSchemaVersion, r.Book.ContentVersion));
        Assert.True(b.Fightable);
        Assert.Equal(new[] { "grand_solitaire_calme", "grand_solitaire_blesse", "grand_solitaire_enrage" }, b.Phases.Select(p => p.Form!.Id));
        // Chaque forme n'a que des attaques de sa phase, annoncées au sol (l'anneau attend une forme nouvelle du moteur).
        foreach (WorldBossPhase ph in b.Phases)
        {
            Assert.All(ph.Form!.Attacks, a => Assert.Contains(a.Id, ph.Attacks));
            Assert.All(ph.Form.Attacks, a => Assert.True(a.TelegraphMs > 0));
        }
        // Sans le dossier des scénarios (lecture d'avant), les formes ne sont pas résolues : le boss ne se combat pas dans le moteur.
        Assert.False(WorldBossJsonReader.ReadFile(WorldBossJsonReader.PathIn(DataDirectory), Texts, Zone(), Items.Value).Book!.Boss("grand_solitaire").Fightable);
        // Refus, au chemin précis.
        string First(Action<JsonObject> m) { WorldBossLoadResult x = Load(m); Assert.False(x.IsValid); return x.Report.Errors[0].Path; }
        Assert.Equal("$.data.bosses[0].phases[1].form", First(root => B(root)["phases"]![1]!["form"] = "grand_solitaire_calme"));
        Assert.Equal("$.data.bosses[0].phases[0].form", First(root => B(root)["phases"]![0]!["form"] = "inconnue"));
        Assert.Equal("$.data.bosses[0].phases[2].form", First(root => B(root)["phases"]![2]!.AsObject().Remove("form")));
        Assert.Equal("$.data.bosses[0].phases[0].form", First(root => B(root)["phases"]![0]!["form"] = "grand_solitaire_enrage"));   // attaques hors de la phase
        Assert.Equal("$.data.bosses[0].scenario", First(root => B(root)["scenario"] = "zones/inexistant"));
        Assert.Equal("$.data.bosses[0].scenario", First(root => B(root)["scenario"] = "../hors"));
        Assert.Equal("$.data.bosses[0].scenario", First(root => root["schemaVersion"] = 1));
        Assert.Equal("$.data.bosses[0].phases[0].form", First(root => { root["schemaVersion"] = 1; B(root).Remove("scenario"); }));
        // Le schéma 1 sans formes reste lu.
        WorldBossLoadResult v1 = Load(root => { root["schemaVersion"] = 1; B(root).Remove("scenario"); foreach (JsonNode? ph in B(root)["phases"]!.AsArray()) ph!.AsObject().Remove("form"); });
        Assert.True(v1.IsValid, v1.Report.ToString());
        Assert.False(v1.Book!.Boss("grand_solitaire").Fightable);
    }

    /// <summary>
    /// Une occurrence et un point sûr (où entre le personnage) près desquels le boss passe pendant son errance : la même occurrence, avec le
    /// personnage immobile à ce point, se déroule comme dans l'événement seul (mêmes places des joueurs).
    /// </summary>
    private static (WorldBossOccurrence Occurrence, string SafePoint) OnHisWay()
    {
        ZoneDefinition zone = Zone();
        long near = 25L * Fixed.One.Raw;
        WorldBossOccurrence occ = FirstOccurrence();
        for (int tries = 0; tries < 40; tries++, occ = FirstOccurrence(occ.Window + 1))
            foreach (SafePoint sp in zone.SafePoints)
            {
                var e = new WorldBossEvent(Boss, WorldBossMemory.None, occ, new[] { sp.Position }, shared: false);
                for (long t = e.AppearMs + 1000; t < e.LimitAtMs; t += 1000)
                {
                    e.Advance(t, new[] { sp.Position });
                    if (Planar.LengthSquared(e.Position.X.Raw - sp.Position.X.Raw, e.Position.Z.Raw - sp.Position.Z.Raw) <= near * near) return (occ, sp.Id);
                }
            }
        throw new InvalidOperationException("aucun point sûr sur son chemin");
    }

    [Fact]
    public void Combat_dans_le_moteur_engagement_degats_phases_et_degagement()
    {
        (WorldBossOccurrence occ, string safePoint) = OnHisWay();
        WorldSession w = Open(safePoint: safePoint);
        At(w, occ.SignsStartMs + 1000);
        WorldBossEvent e = w.WorldBossNow("grand_solitaire")!;
        long t = e.AppearMs;
        while (w.WorldBossForm("grand_solitaire") is null && t < e.LimitAtMs) At(w, t += 1000);
        string form = w.WorldBossForm("grand_solitaire")!;
        Assert.NotNull(form);
        Assert.Contains(WorldBossNoticeKind.Engaged, Kinds(w));
        Assert.True(e.Engaged);
        Assert.Equal("grand_solitaire_calme", form);
        CombatantView v = w.Zone.Engine.View(form);
        Assert.Equal((Boss.SoloHealth, Boss.SoloHealth), (v.MaxHealth, v.Health));   // mode solo : la vie d'un seul joueur
        Assert.True(w.IsWorldBossForm(form));
        // Le personnage frappe la forme : les dégâts comptent pour l'événement (participation du personnage).
        for (int k = 0; k < 1200 && e.Health == e.MaxHealth; k++)
        {
            if (k % 10 == 0)
            {
                CombatantView hero = w.Zone.Engine.View(w.CombatantId), boss = w.Zone.Engine.View(form);
                long ms = w.Zone.Engine.CurrentTick * w.Zone.Engine.TickMilliseconds;
                long dx = boss.Position!.Value.X.Raw - hero.Position!.Value.X.Raw, dz = boss.Position.Value.Z.Raw - hero.Position.Value.Z.Raw;
                if (Planar.LengthSquared(dx, dz) > 4L * Fixed.One.Raw * 4L * Fixed.One.Raw)
                {
                    Planar.TryNormalize(dx, dz, out long ax, out long az);
                    w.Zone.Engine.Submit(new CombatCommand(ms, w.CombatantId, GroundVector.FromRaw(ax, az)));   // le personnage s'approche
                }
                else
                {
                    w.Zone.Engine.Submit(new CombatCommand(ms, w.CombatantId, CombatAction.Stop));
                    w.Zone.Engine.Submit(new CombatCommand(ms, w.CombatantId, "skill.bastion.frappe_du_rempart", form));
                }
            }
            w.Step();
        }
        Assert.True(e.Health < e.MaxHealth, "aucun dégât compté");
        Assert.Equal(e.Health, w.Zone.Engine.View(form).Health);
        Assert.Contains(e.Contributions, c => c.Key == w.CombatantId && c.Value == e.MaxHealth - e.Health);
        // Changement de phase : la forme suivante prend sa place, avec la vie restante.
        GroundVector before = w.Zone.Engine.View(form).Position!.Value;
        e.Damage(w.CombatantId, e.Health - e.MaxHealth * 69 / 100);
        Assert.Equal("blesse", e.PhaseNow.Id);
        w.Step();   // l'ancienne forme part
        Assert.Null(w.WorldBossForm("grand_solitaire") is string pending && pending != form ? pending : null);
        w.Step();   // la suivante entre à sa place
        Assert.Contains(WorldBossNoticeKind.PhaseChanged, Kinds(w));
        string blesse = w.WorldBossForm("grand_solitaire")!;
        Assert.Equal("grand_solitaire_blesse", blesse);
        w.Step();
        Assert.True(w.Zone.Engine.View(form).HasLeft);
        Assert.True(w.Zone.Engine.View(blesse).IsPresent);
        CombatantView b = w.Zone.Engine.View(blesse);
        Assert.Equal(e.Health, b.Health);
        Assert.Equal(before, b.Position);
        // L'assaut le dégage : la forme quitte le moteur, il marche vers sa cible.
        At(w, e.LimitAtMs + 1000);
        Assert.Null(w.WorldBossForm("grand_solitaire"));
        Assert.False(e.Engaged);
        Assert.Equal(WorldBossStage.Assault, e.Stage);
        Assert.Contains(WorldBossNoticeKind.Disengaged, Kinds(w));
        w.Step();   // le départ prend effet au tick suivant
        Assert.True(w.Zone.Engine.View(blesse).HasLeft);
    }
}
