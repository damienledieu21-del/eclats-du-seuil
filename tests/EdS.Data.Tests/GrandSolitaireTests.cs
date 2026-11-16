using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.WorldBosses;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Randomness;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Grand Solitaire (dossier « Grand Solitaire et compagnons », p. 3 à 33 ; règles retenues, valeurs à valider) : données (schéma 1,
/// validateur unique) et règles de l'événement en temps serveur : apparition tirée au sort loin des joueurs après 3 minutes de signes,
/// compte à rebours de 60 minutes que le combat ne remet pas à zéro, cible annoncée à 50 minutes, assaut à pied à 60, trois phases,
/// mémoire du vainqueur, vie selon les joueurs (pas les compagnons), un compagnon au combat, butin.
/// </summary>
[Trait("Category", "World")]
public class GrandSolitaireTests
{
    private const long Minute = 60_000;
    private static string ScenariosDirectory => Path.Combine(AppContext.BaseDirectory, "scenarios");
    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;
    private static TextCatalog Texts => StoryJsonReader.ReadTextsFile(StoryJsonReader.TextsPathIn(DataDirectory)).Texts!;
    private static ZoneDefinition Zone => ZoneJsonReader.Read(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")), ScenariosDirectory, ShippedData, Items).Zone!;

    private static WorldBossLoadResult Shipped() => WorldBossJsonReader.ReadFile(WorldBossJsonReader.PathIn(DataDirectory), Texts, Zone, Items);

    private static readonly Lazy<WorldBossDefinition> Loaded = new Lazy<WorldBossDefinition>(() => Shipped().Book!.Boss("grand_solitaire"));

    private static WorldBossDefinition Boss => Loaded.Value;

    private static WorldBossLoadResult Mutated(Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(WorldBossJsonReader.PathIn(DataDirectory)))!.AsObject();
        mutate(root);
        return WorldBossJsonReader.Read(root.ToJsonString(), Texts, Zone, Items);
    }

    private static JsonObject B(JsonObject root) => root["data"]!["bosses"]![0]!.AsObject();

    private static string First(WorldBossLoadResult r)
    {
        Assert.False(r.IsValid);
        return r.Report.Errors[0].Path;
    }

    private static GroundVector P(double x, double z) => new GroundVector(Fixed.Parse(x.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        Fixed.Parse(z.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    private static WorldBossEvent Start(WorldBossMemory? memory = null, int players = 10, long signsStart = 1_000_000, params GroundVector[] at)
    {
        var e = new WorldBossEvent(Boss, memory ?? WorldBossMemory.None, new WorldBossOccurrence(7, signsStart), at);
        e.SetPlayers(players);
        return e;
    }

    [Fact]
    public void Donnees_livrees_valides()
    {
        WorldBossLoadResult r = Shipped();
        Assert.True(r.IsValid, r.Report.ToString());
        WorldBossDefinition b = r.Book!.Boss("grand_solitaire");
        Assert.Equal((5, 100), (b.MinPlayers, b.MaxPlayers));
        Assert.Equal((3 * Minute, 60 * Minute, 50 * Minute), (b.SignsMs, b.LimitMs, b.AnnounceMs));
        Assert.Equal(new[] { "calme", "blesse", "enrage" }, b.Phases.Select(p => p.Id));
        Assert.Equal(new[] { 100, 70, 35 }, b.Phases.Select(p => p.FromPercent));
        Assert.Equal(1, b.CompanionsInCombat);
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "worldbosses.schema.json"))
            .IsValid(JsonDocument.Parse(File.ReadAllText(WorldBossJsonReader.PathIn(DataDirectory))).RootElement));
    }

    [Fact]
    public void Donnees_invalides_refusees_avec_leur_chemin()
    {
        Assert.Equal("$.data.bosses[0].maxPlayers", First(Mutated(r => B(r)["maxPlayers"] = 101)));
        WorldBossLoadResult fewer = Mutated(r => B(r)["maxPlayers"] = 4);   // moins que minPlayers (5)
        Assert.Equal(("$.data.bosses[0].maxPlayers", EdS.Data.Validation.ValidationCode.InvalidValue), (First(fewer), fewer.Report.Errors[0].Code));
        Assert.Equal("$.data.bosses[0].minPlayers", First(Mutated(r => B(r)["minPlayers"] = 0)));   // plancher technique : 1
        Assert.Equal("$.data.bosses[0].announceMs", First(Mutated(r => B(r)["announceMs"] = 3_600_000)));
        Assert.Equal("$.data.bosses[0].windowMinutes", First(Mutated(r => B(r)["windowMinutes"] = 60)));
        // Relecture de la PR #171 : la fenêtre contient l'assaut entier, marche comprise (la vraie borne, à la minute près).
        WorldBossDefinition b = Boss;
        long needed = b.SignsMs + b.LimitMs + b.AssaultMs;
        Assert.True(b.AssaultMs > b.Targets.Max(t => t.Objectives.Count) * b.ObjectiveMs + 60_000);   // la marche compte
        int tight = (int)(needed / Minute);
        Assert.Equal("$.data.bosses[0].windowMinutes", First(Mutated(r => B(r)["windowMinutes"] = tight)));
        Assert.True(Mutated(r => B(r)["windowMinutes"] = tight + 1).IsValid);
        Assert.Equal("$.data.bosses[0].companionsInCombat", First(Mutated(r => B(r)["companionsInCombat"] = 2)));
        Assert.Equal("$.data.bosses[0].name", First(Mutated(r => B(r)["name"] = "text.inconnu")));
        Assert.Equal("$.data.bosses[0].phases[0].fromPercent", First(Mutated(r => B(r)["phases"]![0]!["fromPercent"] = 90)));
        Assert.Equal("$.data.bosses[0].phases[2].fromPercent", First(Mutated(r => B(r)["phases"]![2]!["fromPercent"] = 70)));
        Assert.Equal("$.data.bosses[0].phases[1].attacks[2]", First(Mutated(r => B(r)["phases"]![1]!["attacks"]![2] = "souffle")));
        Assert.Equal("$.data.bosses[0].attacks[0].lockMs", First(Mutated(r => B(r)["attacks"]![0]!["lockMs"] = 2400)));
        Assert.Equal("$.data.bosses[0].attacks[0].angle", First(Mutated(r => B(r)["attacks"]![0]!["angle"] = 200)));
        Assert.Equal("$.data.bosses[0].attacks[3].outer", First(Mutated(r => B(r)["attacks"]![3]!["outer"] = 8)));
        Assert.Equal("$.data.bosses[0].attacks[4].countMax", First(Mutated(r => B(r)["attacks"]![4]!["countMax"] = 2)));
        Assert.Equal("$.data.bosses[0].attacks[4].anchor", First(Mutated(r => B(r)["attacks"]![4]!["anchor"] = "sky")));
        Assert.Equal("$.data.bosses[0].attacks[1].shape", First(Mutated(r => B(r)["attacks"]![1]!["shape"] = "spiral")));
        Assert.Equal("$.data.bosses[0].sites[0].position", First(Mutated(r => B(r)["sites"]![0]!["position"] = new JsonArray(-65, -48))));   // une maison
        Assert.Equal("$.data.bosses[0].sites[1].kind", First(Mutated(r => B(r)["sites"]![1]!["kind"] = "toit")));
        Assert.Equal("$.data.bosses[0].targets[0].place", First(Mutated(r => B(r)["targets"]![0]!["place"] = "atlantide")));
        Assert.Equal("$.data.bosses[0].targets[0].route[1]", First(Mutated(r => B(r)["targets"]![0]!["route"]![1] = new JsonArray(-30, -10))));   // dans maison3 (hameau ; V2.30.1 : maison4 a déménagé)
        Assert.Equal("$.data.bosses[0].targets[0].objectives[1].id", First(Mutated(r => B(r)["targets"]![0]!["objectives"]![1]!["id"] = "palissade")));
        Assert.Equal("$.data.bosses[0].loot.mount", First(Mutated(r => B(r)["loot"]!["mount"] = "item.licorne")));
        Assert.Equal("$.data.bosses[0].loot.mountChanceBasisPoints", First(Mutated(r => B(r)["loot"]!["mountChanceBasisPoints"] = 10_001)));
        Assert.Equal("$.data.bosses[0].fallback", First(Mutated(r => B(r)["fallback"] = new JsonArray(9999, 0))));
        Assert.Equal("$.data.bosses[0].radio", First(Mutated(r => B(r)["radio"] = 3)));   // champ inconnu, refusé à la lecture
        Assert.Equal("$.schemaVersion", First(Mutated(r => r["schemaVersion"] = 3)));   // Grand Solitaire, 2e partie : schéma 2 (scénario des formes)
        // Revue Codex de la PR #171 : valeurs extrêmes refusées avec leur chemin, sans débordement dans les calculs croisés.
        Assert.Equal("$.data.bosses[0].signsMs", First(Mutated(r => B(r)["signsMs"] = long.MaxValue)));
        Assert.Equal("$.data.bosses[0].objectiveMs", First(Mutated(r => B(r)["objectiveMs"] = long.MaxValue)));
        Assert.Equal("$.data.bosses[0].limitMs", First(Mutated(r => B(r)["limitMs"] = long.MaxValue)));
        Assert.Equal("$.data.bosses[0].announceMs", First(Mutated(r => B(r)["announceMs"] = long.MaxValue)));
        Assert.Equal("$.data.bosses[0].healthPerPlayer", First(Mutated(r => B(r)["healthPerPlayer"] = long.MaxValue)));
        Assert.Equal("$.data.bosses[0].maxPlayers", First(Mutated(r => B(r)["maxPlayers"] = int.MaxValue)));
        Assert.Equal("$.data.bosses[0].walkSpeed", First(Mutated(r => B(r)["walkSpeed"] = 1000)));
    }

    [Fact]
    public void Planification_deterministe_en_temps_serveur()
    {
        WorldBossDefinition b = Boss;
        long windowMs = WorldBossSchedule.WindowMs(b);
        int seen = 0;
        for (long w = 0; w < 400; w++)
        {
            WorldBossOccurrence? o = WorldBossSchedule.InWindow(b, w);
            Assert.Equal(o?.SignsStartMs, WorldBossSchedule.InWindow(b, w)?.SignsStartMs);   // même fenêtre, même tirage
            if (o is not WorldBossOccurrence x) continue;
            seen++;
            Assert.Equal(w, WorldBossSchedule.WindowOf(b, x.SignsStartMs));
            Assert.True(x.SignsStartMs + WorldBossSchedule.LongestMs(b) <= (w + 1) * windowMs);   // tout tient dans la fenêtre
        }
        Assert.InRange(seen, 150, 250);   // 50 % proposé
    }

    [Fact]
    public void Signes_puis_apparition_puis_compte_a_rebours_que_le_combat_ne_remet_pas_a_zero()
    {
        WorldBossEvent e = Start();
        long t0 = e.AppearMs;
        Assert.Equal(e.SignsStartMs + 3 * Minute, t0);
        Assert.Equal((WorldBossStage.Signs, WorldBossSign.Ground), (e.Stage, e.Sign));
        Assert.False(e.IsPresent);
        Assert.False(e.Damage("p1", 100));   // pas encore là : refusé
        e.Advance(t0 - 120_000 - 1);
        Assert.Equal(WorldBossSign.Ground, e.Sign);
        e.Advance(t0 - 120_000);
        Assert.Equal(WorldBossSign.Sky, e.Sign);
        e.Advance(t0 - 45_000);
        Assert.Equal(WorldBossSign.Animals, e.Sign);
        e.Advance(t0 - 1);
        Assert.Equal(WorldBossStage.Signs, e.Stage);
        e.Advance(t0);
        Assert.Equal((WorldBossStage.Roaming, WorldBossSign.None), (e.Stage, e.Sign));
        Assert.Equal(e.Site.Position, e.Position);
        Assert.Equal(60 * Minute, e.RemainingMs);
        // Combat : la limite ne bouge pas.
        long limit = e.LimitAtMs;
        e.Advance(t0 + 10 * Minute);
        Assert.True(e.Damage("p1", 1000));
        Assert.Equal(limit, e.LimitAtMs);
        Assert.Equal(50 * Minute, e.RemainingMs);
        e.Advance(t0 + 50 * Minute - 1);
        Assert.Null(e.Target);
        e.Advance(t0 + 50 * Minute);
        Assert.Equal(WorldBossStage.Announced, e.Stage);
        Assert.NotNull(e.Target);
        e.Advance(t0 + 60 * Minute - 1);
        Assert.Equal(WorldBossStage.Announced, e.Stage);
        e.Advance(t0 + 60 * Minute);
        Assert.Equal((WorldBossStage.Assault, 0L), (e.Stage, e.RemainingMs));
        Assert.Throws<ArgumentOutOfRangeException>(() => e.Advance(t0));   // le temps ne recule pas
        Assert.Equal(WorldBossStage.Assault, e.Stage);
    }

    [Fact]
    public void Assaut_a_pied_sans_teleportation_puis_victoire_du_boss_et_depart()
    {
        WorldBossEvent e = Start();
        long step = 250;   // un pas d'appel ; le monde avance par secondes entières
        long maxStep = Boss.WalkSpeed.Raw * WorldBossEvent.StepMs / 1000 + 1;
        GroundVector last = e.Position;
        long now = e.SignsStartMs;
        int objectives = 0;
        while (e.Stage != WorldBossStage.Gone)
        {
            now += step;
            e.Advance(now);
            long dx = e.Position.X.Raw - last.X.Raw, dz = e.Position.Z.Raw - last.Z.Raw;
            Assert.True(EdS.Domain.Numerics.IntMath.Sqrt(dx * dx + dz * dz) <= maxStep, $"saut de {dx}, {dz} à {now}");
            Assert.True(e.ObjectivesDestroyed >= objectives);
            objectives = e.ObjectivesDestroyed;
            last = e.Position;
            Assert.True(now < e.AppearMs + 4 * 60 * Minute, "l'assaut doit finir");
        }
        Assert.Equal(WorldBossOutcome.BossWon, e.Outcome);
        Assert.True(now <= e.SignsStartMs + WorldBossSchedule.LongestMs(Boss), $"assaut fini à {now - e.SignsStartMs} ms, borne {WorldBossSchedule.LongestMs(Boss)}");
        Assert.Equal(e.Target!.Objectives.Count, e.ObjectivesDestroyed);
        Assert.Equal(e.Target.Route[0], e.Position);   // reparti par sa route
        Assert.False(e.Damage("p1", 10));   // vainqueur : plus de combat
        WorldBossMemory after = e.MemoryAfter;
        Assert.True(after.Raged);
        Assert.Equal((0L, 1L, 7L), (after.PlayerWins, after.BossWins, after.LastWindow));
    }

    [Fact]
    public void Victoire_des_joueurs_pendant_l_assaut_et_cycle_de_rage_clos()
    {
        WorldBossEvent e = Start();
        e.Advance(e.AppearMs + 61 * Minute);
        Assert.Equal(WorldBossStage.Assault, e.Stage);
        Assert.True(e.Damage("p1", long.MaxValue / 4));
        Assert.Equal((WorldBossStage.Defeated, WorldBossOutcome.PlayersWon, 0L), (e.Stage, e.Outcome, e.Health));
        Assert.False(e.Damage("p2", 1));
        Assert.False(e.Contribute("p2", WorldBossContribution.Healing, 5));
        e.Advance(e.AppearMs + 90 * Minute);
        Assert.Equal(WorldBossStage.Defeated, e.Stage);   // plus rien ne bouge
        WorldBossMemory raged = new WorldBossMemory(0, 1, 6, WorldBossOutcome.BossWon);
        WorldBossEvent back = Start(raged);
        back.Advance(back.AppearMs);
        back.Damage("p1", back.MaxHealth);
        Assert.False(back.MemoryAfter.Raged);
        Assert.Equal((1L, 1L), (back.MemoryAfter.PlayerWins, back.MemoryAfter.BossWins));
        // Une occurrence réglée ne l'est qu'une fois.
        Assert.Equal(back.MemoryAfter, back.MemoryAfter.After(7, WorldBossOutcome.BossWon));
    }

    [Fact]
    public void Trois_phases_vie_selon_les_joueurs_figee_au_debut_de_chaque_phase()
    {
        WorldBossDefinition b = Boss;
        Assert.Equal(b.MaxHealthFor(5), b.MaxHealthFor(0));   // au moins 5 joueurs comptés
        Assert.Equal(b.MaxHealthFor(100), b.MaxHealthFor(250));   // au plus 100
        Assert.Equal(b.HealthBase + b.HealthPerPlayer * 40, b.MaxHealthFor(40));
        WorldBossEvent e = Start(players: 10);
        e.Advance(e.AppearMs);
        Assert.Equal(b.MaxHealthFor(10), e.MaxHealth);
        Assert.Equal(("calme", 0), (e.PhaseNow.Id, e.PlatePhase));
        Assert.Equal(new[] { "balayage", "pietinement" }, e.Attacks);
        e.SetPlayers(40);   // 30 joueurs arrivent : rien ne change avant la phase suivante
        e.Damage("p1", e.MaxHealth * 29 / 100);
        Assert.Equal(b.MaxHealthFor(10), e.MaxHealth);
        Assert.Equal("calme", e.PhaseNow.Id);
        e.Damage("p1", e.MaxHealth / 100);   // 70 % : blessé
        Assert.Equal("blesse", e.PhaseNow.Id);
        Assert.Equal(b.MaxHealthFor(40), e.MaxHealth);
        Assert.InRange(e.Health * 100 / e.MaxHealth, 69, 70);   // la part de vie est gardée
        Assert.Contains("charge", e.Attacks);
        e.Damage("p2", e.Health - e.MaxHealth * 35 / 100);
        Assert.Equal("enrage", e.PhaseNow.Id);
        Assert.Contains("anneau", e.Attacks);
        Assert.Contains("eclats", e.Attacks);
        Assert.Equal(100, e.DamagePercent);
    }

    [Fact]
    public void Memoire_du_vainqueur_enrage_a_pleine_vie_sans_bonus_de_degats()
    {
        WorldBossEvent e = Start(new WorldBossMemory(0, 1, 6, WorldBossOutcome.BossWon), players: 20);
        e.Advance(e.AppearMs);
        Assert.Equal(e.MaxHealth, e.Health);   // 100 % de vie
        Assert.Equal(Boss.MaxHealthFor(20), e.MaxHealth);
        Assert.Equal(("enrage", 0), (e.PhaseNow.Id, e.PlatePhase));
        Assert.Equal(100, e.DamagePercent);
        e.Damage("p1", e.MaxHealth * 40 / 100);   // sous 70 % : il ne redevient pas calme ; les plaques suivent la vie
        Assert.Equal(("enrage", 1), (e.PhaseNow.Id, e.PlatePhase));
    }

    [Fact]
    public void Apparition_loin_des_joueurs()
    {
        WorldBossDefinition b = Boss;
        foreach (WorldBossSite occupied in b.Sites)
            for (long w = 0; w < 20; w++)
            {
                var e = new WorldBossEvent(b, WorldBossMemory.None, new WorldBossOccurrence(w, 0), new[] { occupied.Position });
                Assert.NotEqual(occupied.Id, e.Site.Id);
            }
        // Tous occupés : celui dont le joueur le plus proche est le plus loin.
        GroundVector[] everywhere = b.Sites.Select(s => s.Position).ToArray();
        var crowded = new WorldBossEvent(b, WorldBossMemory.None, new WorldBossOccurrence(3, 0), everywhere);
        Assert.Contains(crowded.Site, b.Sites);
        // Le tirage est le même pour la même fenêtre.
        Assert.Equal(new WorldBossEvent(b, WorldBossMemory.None, new WorldBossOccurrence(5, 0), Array.Empty<GroundVector>()).Site.Id,
            new WorldBossEvent(b, WorldBossMemory.None, new WorldBossOccurrence(5, 0), Array.Empty<GroundVector>()).Site.Id);
    }

    [Fact]
    public void Attaques_annoncees_au_sol_memes_delais_de_5_a_100_joueurs()
    {
        WorldBossEvent e = Start(new WorldBossMemory(0, 1, 6, WorldBossOutcome.BossWon), players: 5);   // enragé : toutes les attaques
        e.Advance(e.AppearMs);
        GroundVector o = e.Position, north = P(0, 1);
        WorldBossTelegraph sweep = e.Telegraph("balayage", north, Array.Empty<GroundVector>());
        Assert.Equal((e.NowMs + 2400, e.NowMs + 1400, e.NowMs + 4900), (sweep.ImpactAtMs, sweep.LockAtMs, sweep.EndMs));
        Assert.True(sweep.Touches(P((double)o.X.Raw / 10000, (double)o.Z.Raw / 10000 + 13), Fixed.FromRatio(1, 2)));   // devant
        Assert.False(sweep.Touches(P((double)o.X.Raw / 10000, (double)o.Z.Raw / 10000 - 5), Fixed.FromRatio(1, 2)));   // derrière l'épaule
        WorldBossTelegraph ring = e.Telegraph("anneau", north, Array.Empty<GroundVector>());
        Fixed half = Fixed.FromRatio(1, 2);
        GroundVector At(double dz) => P((double)o.X.Raw / 10000, (double)o.Z.Raw / 10000 + dz);
        Assert.False(ring.Touches(At(5), half));   // zone centrale libre
        Assert.True(ring.Touches(At(15), half));
        Assert.False(ring.Touches(At(30), half));   // au-delà du bord extérieur
        WorldBossTelegraph charge = e.Telegraph("charge", north, Array.Empty<GroundVector>());
        Assert.True(charge.Touches(At(40), half));
        Assert.False(charge.Touches(P((double)o.X.Raw / 10000 + 8, (double)o.Z.Raw / 10000 + 20), half));   // sorti latéralement
        WorldBossTelegraph stomp = e.Telegraph("pietinement", north, Array.Empty<GroundVector>());
        Assert.Equal(2, stomp.Circles.Count);
        Assert.Equal(stomp.ImpactAtMs + 800 + 3500, stomp.EndMs);   // le second impact 0,8 s après le premier
        GroundVector[] crowd = Enumerable.Range(0, 20).Select(i => At(10 + i)).ToArray();
        Assert.Equal(3, e.Telegraph("eclats", north, crowd).Circles.Count);
        e.SetPlayers(100);
        WorldBossTelegraph many = e.Telegraph("eclats", north, crowd);
        Assert.Equal(8, many.Circles.Count);   // de 3 à 8 marques, jamais une sous chaque joueur
        Assert.Equal(3000, many.ImpactAtMs - many.StartMs);   // même annonce à 5 et à 100
        Assert.Equal(5, WorldBossTelegraph.CircleCount(Boss, Boss.Attack("eclats"), 50));
        Assert.Throws<InvalidOperationException>(() => Start().Telegraph("charge", north, crowd));   // pas encore là
    }

    [Fact]
    public void Attaques_de_la_phase_seulement()
    {
        WorldBossEvent e = Start();
        e.Advance(e.AppearMs);
        Assert.Throws<ArgumentException>(() => e.Telegraph("anneau", P(1, 0), Array.Empty<GroundVector>()));   // calme : pas d'anneau
        Assert.Throws<ArgumentException>(() => e.Telegraph("balayage", P(0, 0), Array.Empty<GroundVector>()));   // direction nulle
    }

    [Fact]
    public void Un_compagnon_au_combat_le_second_au_point_de_repli()
    {
        (IReadOnlyList<string> fight, IReadOnlyList<string> wait) = WorldBossCompanions.Assign(Boss, new[] { "nyx", "louveteau" });
        Assert.Equal(new[] { "nyx" }, fight);
        Assert.Equal(new[] { "louveteau" }, wait);
        Assert.Equal((1, 0), (WorldBossCompanions.Assign(Boss, new[] { "albe" }).InCombat.Count, WorldBossCompanions.Assign(Boss, new[] { "albe" }).AtFallback.Count));
        Assert.Empty(WorldBossCompanions.Assign(Boss, Array.Empty<string>()).InCombat);
        Assert.Equal(P(-30, 30), Boss.Fallback);
    }

    [Fact]
    public void Butin_selon_la_participation_trophee_a_la_premiere_victoire_monture_tres_rare()
    {
        WorldBossEvent e = Start(players: 6);
        e.Advance(e.AppearMs);
        e.Contribute("soigneuse", WorldBossContribution.Healing, 500);   // le soin compte
        e.Contribute("spectateur", WorldBossContribution.Protection, 0);   // rien d'utile : pas de butin
        e.Damage("frappeur", e.MaxHealth);
        Assert.Empty(WorldBossSpoils.Award(Start(), _ => 0));   // aucune victoire : rien
        IReadOnlyList<WorldBossAward> awards = WorldBossSpoils.Award(e, p => p == "frappeur" ? 3 : 0);
        Assert.Equal(new[] { "frappeur", "soigneuse" }, awards.Select(a => a.Player));
        Assert.DoesNotContain("item.trophee_des_trois_bornes", awards[0].Items);   // déjà vainqueur avant
        Assert.Contains("item.trophee_des_trois_bornes", awards[1].Items);
        Assert.Contains("item.mantelet_des_trois_bornes", awards[1].Items);
        Assert.Equal(awards.Select(a => string.Join(",", a.Items)), WorldBossSpoils.Award(e, p => p == "frappeur" ? 3 : 0).Select(a => string.Join(",", a.Items)));   // déterministe
        // Monture : 1 chance sur 100 par victoire et par participant (proposition).
        int mounts = 0;
        for (long w = 0; w < 4000; w++)
        {
            var x = new WorldBossEvent(Boss, WorldBossMemory.None, new WorldBossOccurrence(w, 0), Array.Empty<GroundVector>());
            x.Advance(x.AppearMs);
            x.Damage("p", x.MaxHealth);
            if (WorldBossSpoils.Award(x, _ => 1)[0].Items.Contains("item.appel_de_l_aurochs")) mounts++;
        }
        Assert.InRange(mounts, 20, 65);
    }

    [Fact]
    public void Memoire_du_vainqueur_dans_le_registre_des_evenements()
    {
        var m = new WorldBossMemory(2, 1, 41, WorldBossOutcome.BossWon);
        SavedEvent row = m.ToEvent("zone.vallons/grand_solitaire");
        Assert.Equal((2L, 1L, 41L, EventOutcome.Lost), (row.Won, row.Lost, row.Last, row.Outcome));
        Assert.Equal(m, WorldBossMemory.FromEvent(row));
        Assert.True(WorldBossMemory.FromEvent(row).Raged);
        Assert.Equal(WorldBossMemory.None, WorldBossMemory.FromEvent(null));
        Assert.Throws<ArgumentException>(() => new WorldBossMemory(0, 0, -1, WorldBossOutcome.PlayersWon));
        Assert.Throws<ArgumentException>(() => m.After(50, WorldBossOutcome.None));
    }

    [Fact]
    public void Assaut_le_plus_long_dans_la_borne_pour_chaque_cible_et_emplacement()
    {
        // Relecture de la PR #171 : quel que soit l'emplacement de départ et la cible tirés, l'occurrence finit dans LongestMs.
        long longest = WorldBossSchedule.LongestMs(Boss);
        var targets = new HashSet<string>();
        for (long w = 0; w < 40; w++)
        {
            var e = new WorldBossEvent(Boss, WorldBossMemory.None, new WorldBossOccurrence(w, 0), Array.Empty<GroundVector>());
            long t = 0;
            while (e.Stage != WorldBossStage.Gone) { t += 7_000; e.Advance(t); Assert.True(t <= longest + 7_000, $"fenêtre {w}"); }
            targets.Add(e.Target!.Id);
        }
        Assert.Equal(2, targets.Count);
    }

    [Fact]
    public void Meme_resultat_quelle_que_soit_la_cadence_des_appels()
    {
        WorldBossEvent fine = Start(players: 12), coarse = Start(players: 12), uneven = Start(players: 12);
        long t0 = fine.SignsStartMs, end = fine.AppearMs + 75 * Minute;
        long[] gaps = { 37, 999, 1001, 4_321, 12_000, 250, 60_000 };
        long u = t0;
        int k = 0;
        for (long t = t0 + 10_000; t <= end; t += 10_000)
        {
            for (long f = t - 9_750; f <= t; f += 250) fine.Advance(f);
            coarse.Advance(t);
            while (u < t) { u = Math.Min(t, u + gaps[k++ % gaps.Length]); uneven.Advance(u); }
            foreach (WorldBossEvent x in new[] { coarse, uneven })
                Assert.Equal((fine.Stage, fine.Position, fine.Site.Id, fine.Target?.Id, fine.ObjectivesDestroyed), (x.Stage, x.Position, x.Site.Id, x.Target?.Id, x.ObjectivesDestroyed));
        }
        Assert.Equal(WorldBossStage.Gone, fine.Stage);
    }

    [Fact]
    public void Etat_restaurable_qui_continue_comme_l_original()
    {
        WorldBossEvent e = Start(players: 30);
        e.Advance(e.AppearMs + 20 * Minute + 333);
        e.Damage("p1", e.MaxHealth / 2);   // phase blessé
        e.Contribute("p2", WorldBossContribution.Healing, 42);
        WorldBossSnapshot snap = e.Capture();
        WorldBossEvent back = WorldBossEvent.Restore(Boss, snap);
        Assert.Equal((e.Stage, e.Position, e.Site.Id, e.Health, e.MaxHealth, e.PhaseNow.Id, e.NowMs), (back.Stage, back.Position, back.Site.Id, back.Health, back.MaxHealth, back.PhaseNow.Id, back.NowMs));
        Assert.Equal(e.Contributions, back.Contributions);
        for (long t = e.NowMs; t <= e.AppearMs + 80 * Minute; t += 3_100)
        {
            e.Advance(t);
            back.Advance(t);
            Assert.Equal((e.Stage, e.Position, e.Target?.Id, e.ObjectivesDestroyed), (back.Stage, back.Position, back.Target?.Id, back.ObjectivesDestroyed));
        }
        Assert.Equal(e.MemoryAfter, back.MemoryAfter);
    }

    [Fact]
    public void Point_d_emergence_reverifie_a_l_apparition()
    {
        for (long w = 0; w < 20; w++)
        {
            var e = new WorldBossEvent(Boss, WorldBossMemory.None, new WorldBossOccurrence(w, 0), Array.Empty<GroundVector>());
            WorldBossSite provisional = e.Site;
            e.Advance(e.AppearMs - 1);
            Assert.Equal(provisional, e.Site);
            e.Advance(e.AppearMs, new[] { provisional.Position });   // un joueur s'est posté sur le point d'émergence pendant les signes
            Assert.NotEqual(provisional.Id, e.Site.Id);
            Assert.Equal(e.Site.Position, e.Position);
        }
    }

    [Fact]
    public void Memoire_du_vainqueur_sans_cumul_et_jamais_d_une_occurrence_future()
    {
        WorldBossMemory m = WorldBossMemory.None;
        for (long w = 1; w <= 5; w++)
        {
            var e = new WorldBossEvent(Boss, m, new WorldBossOccurrence(w * 10, 0), Array.Empty<GroundVector>());
            e.SetPlayers(10);
            e.Advance(e.AppearMs);
            // Après chaque victoire du boss : enragé, 100 % de vie, la même vie maximale, aucun bonus de dégâts qui s'accumule.
            Assert.Equal((Boss.MaxHealthFor(10), Boss.MaxHealthFor(10), 100), (e.Health, e.MaxHealth, e.DamagePercent));
            Assert.Equal(w > 1, e.PhaseNow.Id == "enrage");
            long t = e.NowMs;
            while (e.Stage != WorldBossStage.Gone) { t += 10_000; e.Advance(t); }
            m = e.MemoryAfter;
            Assert.Equal((w, true), (m.BossWins, m.Raged));
        }
        Assert.Throws<ArgumentException>(() => new WorldBossEvent(Boss, m, new WorldBossOccurrence(m.LastWindow, 0), Array.Empty<GroundVector>()));
        Assert.Throws<ArgumentException>(() => new WorldBossEvent(Boss, m, new WorldBossOccurrence(m.LastWindow - 1, 0), Array.Empty<GroundVector>()));
    }

    [Fact]
    public void Decisions_du_15_novembre_fenetre_solo_apparence_et_emplacements_occupes()
    {
        WorldBossDefinition b = Boss;
        // Fenêtre de 6 h, une chance sur deux.
        Assert.Equal((360, 50), (b.WindowMinutes, b.ChancePercent));
        int occurrences = 0;
        for (long w = 0; w < 2000; w++) if (WorldBossSchedule.InWindow(b, w) != null) occurrences++;
        Assert.InRange(occurrences, 900, 1100);
        // Mode solo : la vie compte pour un seul joueur tant que le monde n'est pas partagé, à chaque début de phase aussi.
        Assert.Equal(b.HealthBase + b.HealthPerPlayer, b.SoloHealth);
        var solo = new WorldBossEvent(b, WorldBossMemory.None, new WorldBossOccurrence(7, 1_000_000), Array.Empty<GroundVector>(), shared: false);
        solo.SetPlayers(1);
        solo.Advance(solo.AppearMs);
        Assert.Equal((false, b.SoloHealth), (solo.Shared, solo.MaxHealth));
        solo.Damage("seul", solo.MaxHealth * 31 / 100);   // phase blessée : toujours la vie d'un joueur
        Assert.Equal(("blesse", b.SoloHealth), (solo.PhaseNow.Id, solo.MaxHealth));
        WorldBossEvent back = WorldBossEvent.Restore(b, solo.Capture());
        Assert.Equal((false, solo.MaxHealth, solo.Health), (back.Shared, back.MaxHealth, back.Health));
        WorldBossEvent shared = Start(players: 1);   // monde partagé : au moins 5 joueurs comptés
        shared.Advance(shared.AppearMs);
        Assert.Equal((true, b.MaxHealthFor(5)), (shared.Shared, shared.MaxHealth));
        // Apparence donnée à la première victoire (en attendant sa quête), avec le trophée.
        solo.Damage("seul", solo.Health);
        Assert.Contains(b.Loot.Appearance, WorldBossSpoils.Award(solo, _ => 0)[0].Items);
        Assert.DoesNotContain(b.Loot.Appearance, WorldBossSpoils.Award(solo, _ => 1)[0].Items);
        // Tous les emplacements occupés : il apparaît quand même, à l'emplacement le moins mauvais (le plus loin des joueurs).
        var everywhere = b.Sites.Select(x => x.Position).ToList();
        everywhere.Add(GroundVector.FromRaw(b.Sites[0].Position.X.Raw + 1, b.Sites[0].Position.Z.Raw));
        var crowd = b.Sites.Skip(1).Select(x => x.Position).Concat(new[] { GroundVector.FromRaw(b.Sites[0].Position.X.Raw + 20 * Fixed.One.Raw, b.Sites[0].Position.Z.Raw) }).ToList();
        WorldBossSite chosen = WorldBossEvent.ChooseSite(b, crowd, new Pcg32(1, 1));
        Assert.Equal(b.Sites[0].Id, chosen.Id);   // le joueur le plus proche y est à 20 m, partout ailleurs à 0 m
        WorldBossEvent busy = new WorldBossEvent(b, WorldBossMemory.None, new WorldBossOccurrence(7, 1_000_000), everywhere, shared: false);
        busy.Advance(busy.AppearMs, everywhere);
        Assert.True(busy.IsPresent);
    }
}
