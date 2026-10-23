using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 12.1 : le monde (projet EdS.World). Zone simulée par le moteur en mode zone (pas de fin quand une équipe tombe, durée jusqu'à
/// 24 h), personnages qui entrent et sortent, morts comme événements consommables à clé unique, rencontres en cours, temps du monde tiré
/// des ticks, sauvegarde versionnée écrite de façon atomique.
/// </summary>
[Trait("Category", "World")]
public class Lot121WorldTests
{
    private const string ZoneId = "zone.vallons";

    private static CombatSetup ZoneSetup(Action<JsonObject>? mutate = null)
    {
        ScenarioLoadResult loaded = mutate is null
            ? ScenarioJsonReader.ReadFile(ScenarioPath("zones/vallons"), ShippedData)
            : ReadMutated(mutate, scenario: "zones/vallons");
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        return loaded.Setup!;
    }

    /// <summary>Le Bastion des Vallons (build et place du scénario de démonstration).</summary>
    private static CharacterSetup Bastion() =>
        ScenarioJsonReader.ReadFile(ScenarioPath("vallons_demo"), ShippedData).Setup!.Combatants.OfType<CharacterSetup>().Single();

    private static CharacterSetup At(CharacterSetup c, string x, string z) =>
        new(c.Id, c.Team, c.Class, c.Build, c.Initiative, new CombatantPlacement(GroundVector.Parse(x, z), c.Placement!.Radius, c.Placement.Facing), c.ArrivalMs);

    private static Zone NewZone(CombatSetup? setup = null, long session = 1, long start = 0, IEnumerable<string>? consumed = null, ICombatLog? log = null,
        WorldClock? clock = null) =>
        new(ZoneId, setup ?? ZoneSetup(), session, start, consumed, log, clock);

    private static List<ZoneEvent> Run(Zone zone, int ticks)
    {
        var events = new List<ZoneEvent>();
        for (int i = 0; i < ticks && !zone.IsOver; i++) events.AddRange(zone.Step());
        return events;
    }

    // --- Scénario de zone et validation ------------------------------------------------------------

    [Fact]
    public void Scenario_de_zone_des_Vallons_valide_sans_personnage()
    {
        CombatSetup setup = ZoneSetup();
        Assert.True(setup.Zone);
        Assert.Equal(3_600_000, setup.DurationMs);   // une heure de jeu par session : au-delà de la limite des combats fermés (10 min)
        Assert.Equal(new[] { "loup1", "loup2", "loup3", "sanglier", "archer" }, setup.Combatants.Select(c => c.Id));
        Assert.Single(setup.Combatants.Select(c => c.Team).Distinct());   // une seule équipe : les joueurs entrent ensuite
        Assert.True(setup.Space!.Obstacles.Single(o => o.Id == "riviere_sud").Low);
    }

    [Fact]
    public void Validation_d_une_zone()
    {
        // Une zone est un combat positionné ; elle peut durer jusqu'à 24 h ; un combat fermé garde sa limite de 10 min et ses deux équipes.
        CombatSetup setup = ZoneSetup();
        CombatSetup Copy(long durationMs, bool zone, CombatSpace? space) =>
            new(setup.Seed, durationMs, setup.Stats, setup.Damage, setup.Progression, setup.Combat, setup.Combatants, setup.Commands, setup.Data, space, zone);
        Assert.Empty(Copy(CombatRules.AbsoluteMaxDurationMs, true, setup.Space).Validate());
        Assert.Equal("durationMs", Assert.Single(Assert.Single(Copy(CombatRules.AbsoluteMaxDurationMs + 50, true, setup.Space).Validate()).Fields));
        Assert.Contains(Copy(3_600_000, true, null).Validate(), v => v.Fields.Contains("zone"));
        IReadOnlyList<RuleViolation> closed = Copy(3_600_000, false, setup.Space).Validate();
        Assert.Contains(closed, v => v.Fields.Contains("durationMs"));
        Assert.Contains(closed, v => v.Fields.Contains("combatants") && v.Message.Contains("deux équipes"));
        // Lecture : « zone » est un booléen ; le schéma 9 est refusé.
        ValidationError typed = Assert.Single(ReadMutated(root => root["data"]!["zone"] = "oui", scenario: "zones/vallons").Report.Errors);
        Assert.Equal("$.data.zone", typed.Path);
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion),
            (Assert.Single(ReadMutated(root => root["schemaVersion"] = 9, scenario: "zones/vallons").Report.Errors).Path,
             Assert.Single(ReadMutated(root => root["schemaVersion"] = 9, scenario: "zones/vallons").Report.Errors).Code));
        Assert.Equal(10, ScenarioValidator.CurrentSchemaVersion);
        JsonNode schema = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "scenario.schema.json")))!;
        Assert.Equal(10, schema["properties"]!["schemaVersion"]!["const"]!.GetValue<int>());
        Assert.Equal("boolean", schema["properties"]!["data"]!["properties"]!["zone"]!["type"]!.GetValue<string>());
    }

    // --- Mode zone du moteur ----------------------------------------------------------------------

    [Fact]
    public void Une_zone_ne_s_arrete_pas_quand_une_equipe_tombe()
    {
        // Les Vallons en combat fermé (le scénario de démonstration, réduit à 3 s) : sans personnage vivant, le combat finirait. En zone,
        // aucun personnage n'est présent au départ : la zone tourne quand même, jusqu'au bout de sa durée.
        CombatSetup setup = ZoneSetup(root => root["data"]!["durationMs"] = 3000);
        var log = new MemoryCombatLog();
        Zone zone = NewZone(setup, log: log);
        Run(zone, 1000);
        Assert.True(zone.IsOver);
        Assert.Equal(60, zone.ElapsedTicks);
        JsonElement ended = log.Lines.Select(l => JsonDocument.Parse(l).RootElement).Single(e => e.TryGetProperty("event", out JsonElement k) && k.GetString() == "combatEnded");
        Assert.Equal("durationReached", ended.GetProperty("reason").GetString());
        Assert.True(JsonDocument.Parse(log.Lines[0]).RootElement.GetProperty("scenario").GetProperty("zone").GetBoolean());
        // Le journal d'une zone se rejoue octet par octet depuis son en-tête (format 16).
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines.ToArray(), ShippedData).Status);
        Assert.False(JsonDocument.Parse(new MemoryCombatLogOf(ScenarioPath("vallons_demo")).Header).RootElement.GetProperty("scenario").TryGetProperty("zone", out _));
    }

    /// <summary>En-tête du journal d'un scénario livré.</summary>
    private sealed class MemoryCombatLogOf
    {
        public string Header { get; }

        public MemoryCombatLogOf(string path)
        {
            var log = new MemoryCombatLog();
            var engine = new CombatEngine(ScenarioJsonReader.ReadFile(path, ShippedData).Setup!, log);
            engine.Step();
            Header = log.Lines[0];
        }
    }

    // --- Entrées, sorties, morts et rencontres -----------------------------------------------------

    [Fact]
    public void Un_personnage_entre_marche_rencontre_la_meute_et_sort()
    {
        Zone zone = NewZone();
        Run(zone, 10);
        zone.Enter(Bastion());
        Assert.Throws<ArgumentException>(() => zone.Enter(Bastion()));   // identifiant déjà pris
        zone.Step();
        CombatantView me = zone.Engine.View("bastion");
        Assert.True(me.IsPresent && !me.IsDead);
        Assert.Equal(10L, me.ArrivalTick);
        Assert.Empty(zone.Encounters());
        // Vers les loups, au nord (derrière le moulin) : ils s'engagent, une rencontre se forme autour du Bastion.
        zone.Engine.Submit(new CombatCommand(zone.Engine.CurrentTick * 50, "bastion", GroundVector.Parse("0.3", "1")));
        for (int i = 0; i < 400 && zone.Encounters().Count == 0; i++) zone.Step();
        Encounter encounter = Assert.Single(zone.Encounters());
        Assert.Equal(new[] { "bastion" }, encounter.Characters);
        Assert.Contains(encounter.Creatures, c => c.StartsWith("loup", StringComparison.Ordinal));
        Assert.DoesNotContain("sanglier", encounter.Creatures);   // hors meute (11.9), loin du Bastion
        // Il sort : la rencontre se défait (plus de cible présente).
        zone.Leave("bastion");
        zone.Step();
        Assert.False(zone.Engine.View("bastion").IsPresent);
        Assert.Empty(zone.Encounters());
        Assert.False(zone.IsOver);
    }

    [Fact]
    public void Une_mort_est_un_evenement_consommable_attribue_une_seule_fois()
    {
        // Un loup très fragile (1 PV) au contact du Bastion qui frappe : sa mort donne un événement de clé unique, inscrit au registre.
        CombatSetup setup = ZoneSetup(root =>
        {
            JsonObject wolf = root["data"]!["combatants"]!.AsArray().Select(c => c!.AsObject()).Single(c => c["id"]!.GetValue<string>() == "loup1");
            wolf["health"] = 1;
            wolf["position"] = new JsonArray(-16, -12);
        });
        Zone first = NewZone(setup, session: 3);
        first.Enter(Bastion());
        first.Step();
        first.Engine.Submit(new CombatCommand(first.Engine.CurrentTick * 50, "bastion", "skill.bastion.frappe_du_rempart", "loup1"));
        List<ZoneEvent> events = Run(first, 40);
        ZoneEvent death = Assert.Single(events);
        Assert.Equal((ZoneEventKind.Death, "zone.vallons/3/death/loup1", "loup1", true), (death.Kind, death.Key, death.Combatant, death.FirstTime));
        Assert.Contains("zone.vallons/3/death/loup1", first.Consumed);
        Assert.True(first.IsConsumed(death.Key));
        // Rechargée depuis la sauvegarde (même session rejouée, registre gardé) : la même mort n'est plus une première fois.
        WorldSave save = first.Save();
        Zone again = NewZone(setup, session: 3, consumed: save.Consumed);
        again.Enter(Bastion());
        again.Step();
        again.Engine.Submit(new CombatCommand(again.Engine.CurrentTick * 50, "bastion", "skill.bastion.frappe_du_rempart", "loup1"));
        Assert.False(Assert.Single(Run(again, 40)).FirstTime);
        Assert.False(again.Consume(death.Key));
        Assert.True(again.Consume("zone.vallons/3/quete/moulin"));
        Assert.Throws<ArgumentException>(() => again.Consume("pas une clé"));
    }

    [Fact]
    public void Zone_deterministe()
    {
        // Les mêmes entrées donnent la même zone, tick après tick (instantané du moteur identique).
        string Play()
        {
            Zone zone = NewZone();
            Run(zone, 5);
            zone.Enter(Bastion());
            zone.Step();
            zone.Engine.Submit(new CombatCommand(zone.Engine.CurrentTick * 50, "bastion", GroundVector.Parse("1", "0.5")));
            Run(zone, 300);
            return zone.Engine.Snapshot().Text;
        }
        Assert.Equal(Play(), Play());
    }

    [Fact]
    public void Zone_refusee_si_mal_formee()
    {
        CombatSetup setup = ZoneSetup();
        Assert.Throws<ArgumentException>(() => new Zone("vallons", setup, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Zone(ZoneId, setup, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Zone(ZoneId, setup, 1, -1));
        Assert.Throws<ArgumentException>(() => new Zone(ZoneId, setup, 1, 0, new[] { "pas une clé" }));
        CombatSetup closed = ScenarioJsonReader.ReadFile(ScenarioPath("vallons_demo"), ShippedData).Setup!;
        Assert.Throws<ArgumentException>(() => new Zone(ZoneId, closed, 1, 0));
    }

    // --- Temps du monde ---------------------------------------------------------------------------

    [Fact]
    public void Temps_du_monde_tire_des_ticks()
    {
        var clock = new WorldClock(50);   // journée provisoire de 2 h de jeu, début à 8 h
        Assert.Equal(new WorldTime(0, 8 * 60), clock.At(0));
        Assert.Equal(DayPhase.Day, clock.At(0).Phase);
        Assert.Equal(144_000, clock.TicksPerDay);
        Assert.Equal(new WorldTime(0, 14 * 60), clock.At(144_000 / 4));        // 30 min de jeu : un quart de journée
        Assert.Equal(new WorldTime(1, 8 * 60), clock.At(144_000));            // deux heures : le lendemain à la même heure
        Assert.Equal(DayPhase.Night, clock.At(144_000 * 2 / 3).Phase);        // 8 h + 16 h : minuit
        Assert.Equal(new WorldTime(0, 23 * 60 + 59), clock.At(144_000 * 2 / 3 - 1));
        Assert.Equal(DayPhase.Dawn, new WorldTime(0, 5 * 60).Phase);
        Assert.Equal(DayPhase.Dusk, new WorldTime(0, 20 * 60 + 59).Phase);
        Assert.Equal(DayPhase.Night, new WorldTime(0, 21 * 60).Phase);
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.At(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldClock(50, 59_950));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldClock(50, 60_010));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldClock(50, startMinute: 1440));
        Assert.Equal(new WorldTime(SaveValidator.MaxWorldTick / 144_000, 8 * 60), clock.At(SaveValidator.MaxWorldTick / 144_000 * 144_000));
        // La zone compte le temps du monde depuis le début de la partie : une session reprise à 1 h 30 de jeu (trois quarts de journée)
        // commence le lendemain à 2 h du matin.
        Zone zone = NewZone(start: 108_000);
        Assert.Equal(new WorldTime(1, 2 * 60), zone.Time);
        Run(zone, 2400);   // deux minutes de jeu : 24 minutes du monde
        Assert.Equal(110_400, zone.WorldTick);
        Assert.Equal(new WorldTime(1, 2 * 60 + 24), zone.Time);
    }

    // --- Sauvegardes ------------------------------------------------------------------------------

    private static string SaveText(string zone = "\"zone.vallons\"", string tick = "1234", string sessions = "2", string consumed = "[\"zone.vallons/1/death/loup1\", \"zone.vallons/2/death/loup2\"]",
        int version = 2, string kind = "save") =>
        $"{{\"schemaVersion\": {version}, \"kind\": \"{kind}\", \"contentVersion\": \"0.1.0-provisoire\", \"data\": {{\"zone\": {zone}, \"worldTick\": {tick}, \"sessions\": {sessions}, {(version >= 2 ? "\"character\": null, " : "")}\"consumed\": {consumed}}}}}";

    [Fact]
    public void Sauvegarde_lue_et_ecrite_a_l_identique()
    {
        SaveLoadResult read = SaveJsonReader.Read(SaveText());
        Assert.True(read.IsValid, read.Report.ToString());
        WorldSave save = read.Save!;
        Assert.Equal(("zone.vallons", 1234L, 2L), (save.Zone, save.WorldTick, save.Sessions));
        Assert.Equal(new[] { "zone.vallons/1/death/loup1", "zone.vallons/2/death/loup2" }, save.Consumed);
        string text = SaveJsonReader.Write(save);
        Assert.Equal(text, SaveJsonReader.Write(SaveJsonReader.Read(text).Save!));
        Assert.EndsWith("}\n", text, StringComparison.Ordinal);
        Assert.Contains("\n    \"consumed\": [\n      \"zone.vallons/1/death/loup1\",\n", text, StringComparison.Ordinal);
        Assert.Contains("\"consumed\": []", SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>())), StringComparison.Ordinal);
        // La sauvegarde trie et dédoublonne son registre ; l'écriture refuse une clé hors format.
        Assert.Equal(new[] { "a.b/1", "a.b/2" }, new WorldSave("zone.vallons", 0, 0, new[] { "a.b/2", "a.b/1", "a.b/2" }).Consumed);
        Assert.Throws<ArgumentException>(() => SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, new[] { "pas \"une\" clé" })));
        Assert.Throws<ArgumentException>(() => SaveJsonReader.Write(new WorldSave("vallons", 0, 0, Array.Empty<string>())));
    }

    [Theory]
    [InlineData("zone", "\"vallons\"", "$.data.zone", ValidationCode.InvalidValue)]
    [InlineData("zone", "null", "$.data.zone", null)]
    [InlineData("tick", "-1", "$.data.worldTick", ValidationCode.OutOfRange)]
    [InlineData("tick", "\"12\"", "$.data.worldTick", null)]
    [InlineData("sessions", "-1", "$.data.sessions", ValidationCode.OutOfRange)]
    [InlineData("consumed", "[\"zone.vallons/2/death/loup2\", \"zone.vallons/1/death/loup1\"]", "$.data.consumed[1]", ValidationCode.InvalidValue)]
    [InlineData("consumed", "[\"zone.vallons/1/death/loup1\", \"zone.vallons/1/death/loup1\"]", "$.data.consumed[1]", ValidationCode.DuplicateId)]
    [InlineData("consumed", "[\"pas une clé\"]", "$.data.consumed[0]", ValidationCode.InvalidValue)]
    [InlineData("consumed", "[\"seul\"]", "$.data.consumed[0]", ValidationCode.InvalidValue)]
    public void Sauvegarde_invalide_refusee_avec_son_chemin(string field, string value, string path, ValidationCode? code)
    {
        string text = field switch
        {
            "zone" => SaveText(zone: value),
            "tick" => SaveText(tick: value),
            "sessions" => SaveText(sessions: value),
            _ => SaveText(consumed: value),
        };
        SaveLoadResult read = SaveJsonReader.Read(text);
        Assert.False(read.IsValid);
        ValidationError error = Assert.Single(read.Report.Errors);
        Assert.Equal(path, error.Path);
        if (code is { } expected) Assert.Equal(expected, error.Code);
    }

    [Fact]
    public void Ecriture_refuse_ce_que_la_lecture_refuserait()
    {
        // Revue Codex de la PR #42 : mêmes bornes à l'écriture qu'à la lecture.
        Assert.Throws<ArgumentException>(() => SaveJsonReader.Write(new WorldSave("zone.vallons", -1, 0, Array.Empty<string>())));
        Assert.Throws<ArgumentException>(() => SaveJsonReader.Write(new WorldSave("zone.vallons", SaveValidator.MaxWorldTick + 1, 0, Array.Empty<string>())));
        Assert.Throws<ArgumentException>(() => SaveJsonReader.Write(new WorldSave("zone.vallons", 0, -1, Array.Empty<string>())));
        Assert.Throws<ArgumentException>(() => SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0,
            Enumerable.Range(0, SaveValidator.MaxConsumed + 1).Select(i => $"zone.vallons/1/death/c{i}"))));
        string edge = SaveJsonReader.Write(new WorldSave("zone.vallons", SaveValidator.MaxWorldTick, SaveValidator.MaxWorldTick, Array.Empty<string>()));
        Assert.True(SaveJsonReader.Read(edge).IsValid);
    }

    [Fact]
    public void Sauvegarde_pleine_lue_malgre_ses_seize_mio()
    {
        // Revue Codex de la PR #42 : 100 000 clés de 200 caractères (plus de 16 Mio) restent lisibles : la limite des sauvegardes (32 Mio)
        // vaut jusqu'à l'analyseur.
        string Key(int i) => "zone.vallons/" + i.ToString("D9") + "/" + new string('a', 200 - 23);
        var save = new WorldSave("zone.vallons", 0, 0, Enumerable.Range(0, SaveValidator.MaxConsumed).Select(Key));
        Assert.All(save.Consumed.Take(3), k => Assert.Equal(200, k.Length));
        string text = SaveJsonReader.Write(save);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(text) > InputLimits.MaxDataFileBytes);
        SaveLoadResult read = SaveJsonReader.Read(text);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.Equal(SaveValidator.MaxConsumed, read.Save!.Consumed.Count);
    }

    [Fact]
    public void Session_tient_sous_la_borne_du_temps_du_monde()
    {
        // Revue Codex de la PR #42 : la durée de la session est réservée d'avance sous la borne des sauvegardes.
        CombatSetup setup = ZoneSetup(root => root["data"]!["durationMs"] = 3000);
        // Revue Codex de la PR #42 (4e tour) : une session de N ticks (ticks 0 à N-1) avance le monde de N ticks, pas de N+1 : celle qui
        // finit exactement à la borne est admise.
        long sessionTicks = 3000 / 50;
        Zone last = NewZone(setup, start: SaveValidator.MaxWorldTick - sessionTicks);
        Run(last, 1000);
        Assert.True(last.IsOver);
        Assert.Equal(sessionTicks, last.ElapsedTicks);
        Assert.Equal(SaveValidator.MaxWorldTick, last.WorldTick);
        Assert.True(SaveJsonReader.Read(SaveJsonReader.Write(last.Save())).IsValid);
        Assert.Throws<ArgumentOutOfRangeException>(() => NewZone(setup, start: SaveValidator.MaxWorldTick - sessionTicks + 1));
    }

    [Fact]
    public void Registre_jamais_au_dela_de_sa_borne()
    {
        // Revue Codex de la PR #42 (2e tour) : place réservée avant chaque tick pour toutes les morts possibles (5 créatures).
        string Key(int i) => $"zone.vallons/0/death/c{i}";
        int creatures = ZoneSetup().Combatants.Count;
        Zone full = NewZone(consumed: Enumerable.Range(0, SaveValidator.MaxConsumed - creatures + 1).Select(Key));
        Assert.Throws<InvalidOperationException>(() => full.Step());
        Assert.Equal(0, full.Engine.CurrentTick);   // rien n'a bougé
        Assert.True(SaveJsonReader.Read(SaveJsonReader.Write(full.Save())).IsValid);
        Zone room = NewZone(consumed: Enumerable.Range(0, SaveValidator.MaxConsumed - creatures).Select(Key));
        room.Step();
        Assert.Equal(1, room.Engine.CurrentTick);
        Assert.False(room.Consume(Key(0)));   // déjà là : pas de place nécessaire
        Zone exact = NewZone(consumed: Enumerable.Range(0, SaveValidator.MaxConsumed).Select(Key));
        Assert.Throws<InvalidOperationException>(() => exact.Consume("zone.vallons/1/quest/x"));
        Assert.Equal(SaveValidator.MaxConsumed, exact.Save().Consumed.Count);
    }

    [Fact]
    public void Registre_initial_au_dela_de_la_borne_refuse()
    {
        // Revue Codex de la PR #42 (3e tour) : la zone n'admet pas un registre qu'elle ne pourrait ni faire avancer ni sauvegarder.
        string Key(int i) => $"zone.vallons/0/death/c{i}";
        Assert.Throws<ArgumentException>(() => NewZone(consumed: Enumerable.Range(0, SaveValidator.MaxConsumed + 1).Select(Key)));
        Assert.Equal(SaveValidator.MaxConsumed, NewZone(consumed: Enumerable.Range(0, SaveValidator.MaxConsumed).Select(Key)).Consumed.Count);
    }

    [Fact]
    public void Reserve_seulement_pour_les_morts_nouvelles()
    {
        // Revue Codex de la PR #42 (3e tour) : une session rechargée dont les morts sont déjà au registre avance avec un registre plein ;
        // un combattant immortel ne réserve pas de place.
        string Key(int i) => $"zone.vallons/0/death/c{i}";
        CombatSetup setup = ZoneSetup();
        string[] deaths = setup.Combatants.Select(c => $"{ZoneId}/1/death/{c.Id}").ToArray();
        Zone reloaded = NewZone(consumed: deaths.Concat(Enumerable.Range(0, SaveValidator.MaxConsumed - deaths.Length).Select(Key)));
        reloaded.Step();
        Assert.Equal(1, reloaded.Engine.CurrentTick);
        CombatSetup immortal = ZoneSetup(root =>
        {
            foreach (JsonNode? c in root["data"]!["combatants"]!.AsArray()) c!.AsObject().Remove("health");
        });
        Zone calm = NewZone(immortal, consumed: Enumerable.Range(0, SaveValidator.MaxConsumed).Select(Key));
        calm.Step();
        Assert.Equal(1, calm.Engine.CurrentTick);
    }

    [Fact]
    public void Reserve_sans_les_arrivees_futures()
    {
        // Revue Codex de la PR #42 (5e tour) : une créature qui n'arrivera que plus tard ne réserve pas de place au registre avant son tick.
        string Key(int i) => $"zone.vallons/0/death/c{i}";
        CombatSetup later = ZoneSetup(root =>
        {
            foreach (JsonNode? c in root["data"]!["combatants"]!.AsArray()) c!["arrivalMs"] = 1000;
        });
        Zone zone = NewZone(later, consumed: Enumerable.Range(0, SaveValidator.MaxConsumed).Select(Key));
        zone.Step();
        Assert.Equal(1, zone.Engine.CurrentTick);
    }

    [Fact]
    public void Zone_refusee_sans_rien_ecrire_au_journal()
    {
        // Revue Codex de la PR #42 (5e tour) : horloge et clés vérifiées avant la création du moteur (qui écrit l'en-tête du journal).
        var log = new MemoryCombatLog();
        Assert.Throws<ArgumentException>(() => NewZone(log: log, clock: new WorldClock(100)));
        Assert.Empty(log.Lines);
        CombatSetup longId = ZoneSetup(root => root["data"]!["combatants"]![0]!["id"] = "loup" + new string('x', 190));
        Assert.Throws<ArgumentException>(() => NewZone(longId, log: log));
        Assert.Empty(log.Lines);
        NewZone(log: log);
        Assert.NotEmpty(log.Lines);
    }

    [Fact]
    public void Cles_de_mort_toujours_dans_le_format()
    {
        // Revue Codex de la PR #42 (2e tour) : un identifiant de combattant ou de zone trop long ferait une clé hors du format.
        string longId = "loup" + new string('x', 190);
        CombatSetup setup = ZoneSetup(root => root["data"]!["combatants"]![0]!["id"] = longId);
        Assert.Throws<ArgumentException>(() => NewZone(setup));
        Assert.False(SaveValidator.IsZoneId("zone." + new string('a', 60)));
        Assert.Throws<ArgumentException>(() => new Zone("zone." + new string('a', 60), ZoneSetup(), 1, 0));
        Zone zone = NewZone();
        CharacterSetup bastion = ScenarioJsonReader.ReadFile(ScenarioPath("vallons_demo"), ShippedData).Setup!.Combatants.OfType<CharacterSetup>().First();
        var renamed = new CharacterSetup(longId, bastion.Team, bastion.Class, bastion.Build, 0, bastion.Placement);
        Assert.Throws<ArgumentException>(() => zone.Enter(renamed));
    }

    [Fact]
    public void Versions_et_type_de_sauvegarde()
    {
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion),
            (Assert.Single(SaveJsonReader.Read(SaveText(version: 3)).Report.Errors).Path, Assert.Single(SaveJsonReader.Read(SaveText(version: 3)).Report.Errors).Code));
        Assert.Equal("$.kind", Assert.Single(SaveJsonReader.Read(SaveText(kind: "scenario")).Report.Errors).Path);
        Assert.Equal("$.data.worldTick", Assert.Single(SaveJsonReader.Read(SaveText(tick: (SaveValidator.MaxWorldTick + 1).ToString())).Report.Errors).Path);
        Assert.Contains(SaveJsonReader.Read("{\"schemaVersion\": 1, \"kind\": \"save\", \"contentVersion\": \"0.1.0\", \"data\": {\"zone\": \"zone.vallons\", \"worldTick\": 0, \"sessions\": 0, \"consumed\": [], \"extra\": 1}}").Report.Errors,
            e => e.Path == "$.data.extra");
    }

    [Fact]
    public void Ecriture_atomique_avec_copie_de_secours()
    {
        string directory = Path.Combine(Path.GetTempPath(), "eds_save_" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "partie.json");
        try
        {
            // Rien encore : nouvelle partie.
            SaveReadResult nothing = SaveStore.Read(path);
            Assert.Null(nothing.Save);
            Assert.False(nothing.Unreadable);
            var first = new WorldSave("zone.vallons", 100, 1, new[] { "zone.vallons/1/death/loup1" });
            var second = new WorldSave("zone.vallons", 200, 2, new[] { "zone.vallons/1/death/loup1", "zone.vallons/2/death/loup2" });
            SaveStore.Write(path, first);
            Assert.False(File.Exists(SaveStore.TemporaryPath(path)));
            Assert.Equal(100, SaveStore.Read(path).Save!.WorldTick);
            SaveStore.Write(path, second);
            Assert.Equal(200, SaveStore.Read(path).Save!.WorldTick);
            Assert.Equal(SaveJsonReader.Write(first), File.ReadAllText(SaveStore.BackupPath(path)));   // la précédente devient la copie
            Assert.Equal(SaveJsonReader.Write(second), File.ReadAllText(path));
            // Sauvegarde abîmée (coupure, disque) : la copie de secours la remplace à la lecture.
            File.WriteAllText(path, "{ tronqué");
            SaveReadResult recovered = SaveStore.Read(path);
            Assert.True(recovered.FromBackup);
            Assert.Equal(100, recovered.Save!.WorldTick);
            // Revue Codex de la PR #42 (3e tour) : écrire après ce repli ne fait pas de la principale abîmée la copie de secours.
            var third = new WorldSave("zone.vallons", 300, 3, new[] { "zone.vallons/1/death/loup1" });
            SaveStore.Write(path, third);
            Assert.Equal(SaveJsonReader.Write(first), File.ReadAllText(SaveStore.BackupPath(path)));
            Assert.Equal(300, SaveStore.Read(path).Save!.WorldTick);
            File.WriteAllText(path, "{ tronqué");
            Assert.Equal(100, SaveStore.Read(path).Save!.WorldTick);
            File.Delete(path);
            Assert.True(SaveStore.Read(path).FromBackup);
            // Les deux abîmées : illisible, avec le motif.
            File.WriteAllText(path, "{}");
            File.WriteAllText(SaveStore.BackupPath(path), "[]");
            SaveReadResult broken = SaveStore.Read(path);
            Assert.Null(broken.Save);
            Assert.True(broken.Unreadable);
            Assert.Contains("sauvegarde invalide", broken.Problem, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Session_suivante_reprend_le_monde_de_la_sauvegarde()
    {
        // Une heure de jeu ne tient pas en mémoire de test : deux sessions courtes (3 s) qui se suivent par la sauvegarde.
        CombatSetup setup = ZoneSetup(root => root["data"]!["durationMs"] = 3000);
        Zone first = NewZone(setup);
        Run(first, 1000);
        WorldSave save = first.Save();
        Assert.Equal((60L, 1L), (save.WorldTick, save.Sessions));
        Zone next = NewZone(setup, session: save.Sessions + 1, start: save.WorldTick, consumed: save.Consumed);
        Run(next, 1000);
        Assert.Equal((120L, 2L), (next.Save().WorldTick, next.Save().Sessions));
        Assert.Equal(new WorldClock(50).At(120), next.Time);
    }
}
