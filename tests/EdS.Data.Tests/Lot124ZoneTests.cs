using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data;
using EdS.Data.Characters;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 12.4 : les Vallons en données. Zone du monde (points sûrs, lieux, populations, passages) validée contre son scénario et le
/// catalogue des objets ; réapparition sous une nouvelle identité ; laisse ; passage du moulin ouvert pour de bon.
/// </summary>
[Trait("Category", "World")]
public class Lot124ZoneTests
{
    private static string ScenariosDirectory => Path.Combine(AppContext.BaseDirectory, "scenarios");

    private static string ZonePath => ZoneJsonReader.PathIn(DataDirectory, "zone.vallons");

    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;

    private static CharacterProgression Progression => ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;

    /// <summary>Zone lue après retouches de son fichier et de son scénario (copié dans un dossier temporaire).</summary>
    private static ZoneLoadResult Zone(Action<JsonObject>? zone = null, Action<JsonObject>? scenario = null, GameData? data = null)
    {
        data ??= ShippedData;
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZonePath))!.AsObject();
        zone?.Invoke(root);
        if (scenario is null) return ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, data, Items);
        string directory = Path.Combine(Path.GetTempPath(), "eds_zone_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "zones"));
            JsonObject s = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
            scenario(s);
            File.WriteAllText(Path.Combine(directory, "zones", "vallons.json"), s.ToJsonString());
            File.Copy(ScenarioPath("zones/vallons_chef"), Path.Combine(directory, "zones", "vallons_chef.json"));   // sous-lot 12.7 : formes du chef
            File.Copy(ScenarioPath("zones/vallons_bandits"), Path.Combine(directory, "zones", "vallons_bandits.json"), true);   // sous-lot V1.6 : formes du chef des bandits
            CopyMissingZoneScenarios(directory);   // sous-lot V1.9.6 : formes de Croc-Gris
            return ZoneJsonReader.Read(root.ToJsonString(), directory, data, Items);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static JsonObject Data(JsonObject root) => root["data"]!.AsObject();

    private static JsonObject Creature(JsonObject scenario, string id) =>
        scenario["data"]!["combatants"]!.AsArray().Select(c => c!.AsObject()).Single(c => c["id"]!.GetValue<string>() == id);

    private static JsonObject Population(JsonObject zone, string creature) =>
        Data(zone)["populations"]!.AsArray().Select(p => p!.AsObject()).Single(p => p["creature"]!.GetValue<string>() == creature);

    private static string FirstError(ZoneLoadResult result)
    {
        Assert.False(result.IsValid);
        return result.Report.Errors[0].Path;
    }

    private static WorldSave NewGame(string zone = "zone.vallons") =>
        new(zone, 0, 0, Array.Empty<string>(), PlayerCharacters.Create("bastion", "class.bastion", Progression));

    private static void Strike(WorldSession session, string target)
    {
        try { session.Zone.Engine.Submit(new CombatCommand(session.Zone.Engine.CurrentTick * 50, "bastion", "skill.bastion.frappe_du_rempart", target)); }
        catch (ArgumentException) { }
    }

    // --- Données -----------------------------------------------------------------------------------

    [Fact]
    public void Vallons_livres()
    {
        ZoneLoadResult loaded = ZoneJsonReader.ReadFile(ZonePath, ScenariosDirectory, ShippedData, Items);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        ZoneDefinition z = loaded.Zone!;
        Assert.Equal(("zone.vallons", "Vallons d'Aube", "zones/vallons"), (z.Id, z.Name, z.ScenarioName));
        Assert.Equal("place_du_village", z.Entry.Id);
        Assert.Equal(new[] { "village", "moulin", "riviere", "pre_nord", "ruines", "carriere", "seuil", "coeur_du_seuil", "forge",
            "bourg", "champs", "lac", "vergers", "hameau", "plaine_est", "hautes_pierres", "tour_de_guet", "camp_des_rodeurs", "foret_des_loups" },
            z.Places.Select(p => p.Id));   // Vallons agrandis : dix lieux de plus ; V1.9.5 : le cœur du Seuil (Gardien au niveau 23 au moins)
        Assert.Equal(new[] { "village" }, z.PlacesAt(z.Entry.Position).Select(p => p.Id));
        Assert.Equal(48, z.Populations.Count);   // V2.1 : sept loups de la nuit et quatre échos ; V1.9.4 : le Grand Borgne ; sous-lot V1.6 : quatre bandits et le loup blanc ; V1.7 : le Borgne ; V1.9.3 : trois bandits (hameau, lac, vergers)
        Assert.Equal(z.Setup.Combatants.Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal),
            z.Populations.Select(p => p.Creature).OrderBy(i => i, StringComparer.Ordinal));   // chaque créature du scénario est peuplée
        Population wolf = z.PopulationOf("loup1")!;
        Assert.Equal((90_000L, Fixed.FromInt(20), "loot.loup"), (wolf.RespawnMs, wolf.LeashRange, wolf.Loot));
        Assert.Equal(("moulin", "porte_moulin"), (z.Passages[0].Id, z.Passages[0].Obstacle));
        Assert.Equal("zone.vallons/passage/moulin", z.PassageKey("moulin"));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "zone.schema.json"))
            .IsValid(JsonDocument.Parse(File.ReadAllText(ZonePath)).RootElement));
    }

    [Fact]
    public void Zone_invalide_refusee_avec_son_chemin()
    {
        Assert.Equal("$.data.entry", FirstError(Zone(z => Data(z)["entry"] = "nulle_part")));
        Assert.Equal("$.data.safePoints[1].position", FirstError(Zone(z => Data(z)["safePoints"]![1]!["position"] = new JsonArray(150, 0))));
        Assert.Equal("$.data.safePoints[1].id", FirstError(Zone(z => Data(z)["safePoints"]![1]!["id"] = "place_du_village")));
        Assert.Equal("$.data.places[0].max", FirstError(Zone(z => Data(z)["places"]![0]!["max"] = new JsonArray(-30, 4))));
        Assert.Equal("$.data.places[0]", FirstError(Zone(z => Data(z)["places"]![0]!["max"] = new JsonArray(150, 4))));
        Assert.Equal("$.data.populations[0].creature", FirstError(Zone(z => Population(z, "loup1")["creature"] = "ours")));
        Assert.Equal("$.data.populations[1].creature", FirstError(Zone(z => Population(z, "loup2")["creature"] = "loup1")));
        Assert.Equal("$.data.populations[0].respawnMs", FirstError(Zone(z => Population(z, "loup1")["respawnMs"] = 1025)));
        Assert.Equal("$.data.populations[0].respawnMs", FirstError(Zone(z => Population(z, "loup1")["respawnMs"] = 500)));
        Assert.Equal("$.data.populations[0].leashRange", FirstError(Zone(z => Population(z, "loup1")["leashRange"] = 0)));
        Assert.Equal("$.data.populations[0].leashRange", FirstError(Zone(z => Population(z, "loup1")["leashRange"] = 101)));
        Assert.Equal("$.data.populations[0].loot", FirstError(Zone(z => Population(z, "loup1")["loot"] = "loot.ours")));
        Assert.Equal("$.data.passages[0].obstacle", FirstError(Zone(z => Data(z)["passages"]![0]!["obstacle"] = "porte_inconnue")));
        Assert.Equal("$.data.id", FirstError(Zone(z => Data(z)["id"] = "vallons")));
        // Le scénario d'une zone est une simulation de zone ; une créature peuplée est mortelle.
        Assert.Equal("$.data.scenario", FirstError(Zone(z => Data(z)["scenario"] = "mannequin")));
        Assert.Equal("$.data.scenario", FirstError(Zone(z => Data(z)["scenario"] = "zones/inconnue")));
        Assert.Equal("$.data.populations[0].creature", FirstError(Zone(scenario: s => Creature(s, "loup1").Remove("health"))));
        Assert.Equal("$.data.populations[0].creature", FirstError(Zone(z => Population(z, "loup1")["creature"] = "loup1_v2",
            s => Creature(s, "loup1")["id"] = "loup1_v2")));   // suffixe des vies suivantes réservé
    }

    // --- Passage du moulin -------------------------------------------------------------------------

    [Fact]
    public void Passage_du_moulin_ouvert_pour_de_bon()
    {
        ZoneDefinition z = Zone().Zone!;
        Assert.Contains(ZoneSetups.For(z, Array.Empty<string>()).Space!.Obstacles, o => o.Id == "porte_moulin");
        Assert.DoesNotContain(ZoneSetups.For(z, new[] { "zone.vallons/passage/moulin" }).Space!.Obstacles, o => o.Id == "porte_moulin");
        // Réponses du 31 octobre 2026 : le loup blanc (créature de la nuit) n'est pas là au début ; sans passage ouvert, l'espace est celui du scénario.
        CombatSetup unchanged = ZoneSetups.For(z, new[] { "zone.vallons/1/death/loup1" });
        Assert.Same(z.Setup.Space, unchanged.Space);
        Assert.Equal(z.Setup.Combatants.Where(c => c.Id != "loup_blanc"), unchanged.Combatants);

        var session = new WorldSession(NewGame(), z, ShippedData, Progression, Items);
        Assert.Contains(session.Zone.Setup.Space!.Obstacles, o => o.Id == "porte_moulin");
        Assert.False(session.IsPassageOpen("moulin"));
        Assert.True(session.OpenPassage("moulin"));
        Assert.False(session.OpenPassage("moulin"));
        Assert.Throws<ArgumentException>(() => session.OpenPassage("pont"));
        session.Step();
        WorldSave save = SaveJsonReader.Read(SaveJsonReader.Write(session.Save())).Save!;
        Assert.Contains("zone.vallons/passage/moulin", save.Consumed);
        // Session suivante : le passage est ouvert, l'obstacle n'est plus là.
        var next = new WorldSession(save, z, ShippedData, Progression, Items);
        Assert.True(next.IsPassageOpen("moulin"));
        Assert.DoesNotContain(next.Zone.Setup.Space!.Obstacles, o => o.Id == "porte_moulin");
        Assert.Equal(new CombatantPlacement(z.Entry.Position, WorldSession.CharacterRadius).Position, next.Zone.Engine.View("bastion").Position);
        // Une sauvegarde d'une autre zone est refusée.
        Assert.Throws<ArgumentException>(() => new WorldSession(NewGame("zone.seuil"), z, ShippedData, Progression, Items));
    }

    // --- Réapparition ------------------------------------------------------------------------------

    [Fact]
    public void Creature_vaincue_reapparait_sous_une_nouvelle_identite()
    {
        // Loup affaibli près du point d'entrée, réapparition en 1 s.
        ZoneDefinition z = Zone(zone => Population(zone, "loup1")["respawnMs"] = 1000, s =>
        {
            Creature(s, "loup1")["health"] = 60;
            Creature(s, "loup1")["position"] = new JsonArray(-16, -12);
            Creature(s, "loup1")["attacks"]![0]!["rawDamage"] = 1;   // le Bastion tient toute la chasse
        }).Zone!;
        var session = new WorldSession(NewGame(), z, ShippedData, Progression, Items);
        var population = new List<PopulationEvent>();
        var xp = new List<ExperienceGained>();
        for (int i = 0; i < 2000 && population.Count(e => e.Kind == PopulationEventKind.Respawned) < 2; i++)
        {
            if (i % 20 == 1) Strike(session, session.CurrentCreature("loup1"));
            SessionStep step = session.Step();
            population.AddRange(step.Population);
            xp.AddRange(step.Experience);
        }
        Assert.Equal(new[] { "loup1_v2", "loup1_v3" }, population.Where(e => e.Kind == PopulationEventKind.Respawned).Select(e => e.Creature));
        Assert.All(population, e => Assert.Equal("loup1", e.Population));
        // Deux morts, deux clés, deux gains d'expérience ; la réapparition a lieu 1 s (20 ticks) après la mort, à la place du modèle.
        Assert.Equal(new[] { "zone.vallons/1/death/loup1", "zone.vallons/1/death/loup1_v2" }, xp.Select(g => g.Source.Key));
        PopulationEvent second = population.First(e => e.Creature == "loup1_v2");
        Assert.Equal(xp[0].Source.Tick + 20, second.Tick);
        CombatantView v3 = session.Zone.Engine.View("loup1_v3");
        Assert.Equal((GroundVector.Parse("-16", "-12"), 60L), (v3.Position!.Value, v3.Health!.Value));
        Assert.Equal("loup1_v3", session.CurrentCreature("loup1"));
        // Le butin de sa population (loot.loup) suit chaque vie : la clé de chaque mort fixe son tirage.
        LootTable table = Items.Loot.Single(t => t.Id == "loot.loup");
        int pelts = new[] { "zone.vallons/1/death/loup1", "zone.vallons/1/death/loup1_v2" }
            .Sum(k => Loot.Roll(table, k).Where(r => r.Item == "item.peau_de_loup").Sum(r => r.Count));
        Assert.Equal(pelts, session.Inventory.Count("item.peau_de_loup"));
    }

    [Fact]
    public void Reapparition_saute_une_identite_deja_prise()
    {
        // Revue Codex de la PR #45 : une créature du scénario hors population porte déjà « loup1_v2 » : la vie suivante de loup1 est
        // « loup1_v3 », sans essai répété sur l'identité prise.
        ZoneDefinition z = Zone(zone => Population(zone, "loup1")["respawnMs"] = 1000, s =>
        {
            Creature(s, "loup1")["health"] = 60;
            Creature(s, "loup1")["position"] = new JsonArray(-16, -12);
            Creature(s, "loup1")["attacks"]![0]!["rawDamage"] = 1;
            JsonObject twin = Creature(s, "loup5").DeepClone().AsObject();
            twin["id"] = "loup1_v2";
            twin["position"] = new JsonArray(-20, 36);
            s["data"]!["combatants"]!.AsArray().Add(twin);
        }).Zone!;
        var session = new WorldSession(NewGame(), z, ShippedData, Progression, Items);
        var population = new List<PopulationEvent>();
        for (int i = 0; i < 2000 && population.Count(e => e.Kind == PopulationEventKind.Respawned) < 2; i++)
        {
            if (i % 20 == 1) Strike(session, session.CurrentCreature("loup1"));
            population.AddRange(session.Step().Population);
        }
        Assert.Equal(new[] { "loup1_v3", "loup1_v4" }, population.Where(e => e.Kind == PopulationEventKind.Respawned).Select(e => e.Creature));
        Assert.Equal("loup1_v4", session.CurrentCreature("loup1"));
        Assert.True(session.Zone.Engine.View("loup1_v2").IsPresent);   // la créature du scénario n'a pas bougé de chez elle
    }

    // --- Laisse ------------------------------------------------------------------------------------

    [Fact]
    public void Creature_distancee_lache_et_revient_au_calme()
    {
        // Laisse d'un mètre : dès qu'il s'engage (7 m), le loup est trop loin de sa cible ; il quitte la zone (ni mort, ni butin, ni
        // expérience) et une vie calme le remplace chez lui, qui s'engage à nouveau… jusqu'au plafond des combattants de la session.
        ZoneDefinition z = Zone(zone => Population(zone, "loup1")["leashRange"] = 1,
            s => Creature(s, "loup1")["position"] = new JsonArray(-16, -8)).Zone!;
        var session = new WorldSession(NewGame(), z, ShippedData, Progression, Items);
        var population = new List<PopulationEvent>();
        var xp = new List<ExperienceGained>();
        for (int i = 0; i < 12000 && !population.Any(e => e.Kind == PopulationEventKind.Exhausted); i++)
        {
            SessionStep step = session.Step();
            population.AddRange(step.Population);
            xp.AddRange(step.Experience);
        }
        Assert.Equal(PopulationEventKind.Leashed, population[0].Kind);
        Assert.Equal(("loup1", PopulationEventKind.Respawned, "loup1_v2"), (population[0].Creature, population[1].Kind, population[1].Creature));
        CombatantView first = session.Zone.Engine.View("loup1");
        Assert.True(first.HasLeft && !first.IsDead);
        Assert.Empty(xp);
        Assert.Equal(new[] { PlayerCharacters.PlacesKey, "zone.vallons/safe/place_du_village" }, session.Save().Consumed);   // V1.1 : seul le point sûr d'entrée, découvert d'emblée (et V1.8 : la barre de V1.8 notée)
        // Plafond : 128 combattants depuis les règles 0.12.0 (64 avant ; décision de Damien du 29 octobre 2026) : les créatures de la zone
        // (32 depuis le sous-lot V1.6, 33 depuis V1.7, 36 depuis V1.9.3, 37 depuis V1.9.4), le Bastion, les premières formes du chef et du chef des bandits, puis les vies suivantes ; une place
        // gardée à la seconde forme de chacun (revue Codex de la PR #48) ; une seule annonce, puis plus rien pour cette population.
        int max = ShippedData.Rules.Combat.MaxCombatants;
        int creatures = z.Setup.Combatants.Count;
        Assert.Equal(48, creatures);   // V2.1 : sept loups de la nuit et quatre échos (absents le jour) ; V1.9.4 : le Grand Borgne ; sous-lot V1.6 : quatre bandits et le loup blanc ; V1.7 : le Borgne ; V1.9.3 : trois bandits du chapitre 3
        Assert.Equal(max - 3, session.Zone.Engine.CombatantIds.Count);   // V1.9.6 : trois secondes formes gardées (Croc-Gris)
        // Réponses du 31 octobre 2026 : le loup blanc (créature de la nuit) n'est pas là au début : une vie suivante de plus avant le plafond.
        // Sous-lot V1.9.6 : Croc-Gris, un troisième chef à deux formes (sa première arrivée, une place gardée à la seconde) : 7 au lieu de 5.
        // Sous-lot V2.1 : les onze créatures de la nuit (loups de la nuit, échos) ne sont pas là le jour : onze vies suivantes de plus.
        Assert.Equal(max - (creatures - 1 - 11) - 7, population.Count(e => e.Kind == PopulationEventKind.Respawned));
        Assert.Single(population, e => e.Kind == PopulationEventKind.Exhausted);
        for (int i = 0; i < 200; i++) Assert.Empty(session.Step().Population);
    }

    [Fact]
    public void Laisse_refusee_par_le_pilote_ne_fait_pas_echouer_le_tick() =>
        // Revue Codex de la PR #45 : plafond de commandes atteint (une seule admise, prise par le Bastion) : le départ du loup est refusé ;
        // le tick n'échoue pas, la laisse est retentée aux ticks suivants, le loup reste dans la zone.
        CaM01Data.WithDirectory(CaM01Data.RulesWith("\"maxCommands\": 20000", "\"maxCommands\": 1"), directory =>
        {
            GameData data = GameDataLoader.Load(directory).Data!;
            ZoneDefinition z = Zone(zone => Population(zone, "loup1")["leashRange"] = 1,
                s => Creature(s, "loup1")["position"] = new JsonArray(-16, -8), data).Zone!;
            var session = new WorldSession(NewGame(), z, data, Progression, Items);
            session.Zone.Engine.Submit(new CombatCommand(0, "bastion", CombatAction.GuardStart));
            var population = new List<PopulationEvent>();
            bool engaged = false;
            for (int i = 0; i < 200; i++)
            {
                population.AddRange(session.Step().Population);
                engaged |= session.Zone.Engine.View("loup1").Engaged;
            }
            Assert.True(engaged);
            Assert.DoesNotContain(population, e => e.Kind == PopulationEventKind.Leashed);
            CombatantView wolf = session.Zone.Engine.View("loup1");
            Assert.False(wolf.HasLeft);
            return 0;
        });

    [Fact]
    public void Point_sur_inaccessible_au_personnage_refuse()
    {
        // Revue Codex de la PR #45 : l'emprise du personnage (0,5 m) doit tenir au point sûr : dans l'aire et hors des obstacles (les places
        // des créatures de départ ne comptent que si elles lui sont solides : pas dans les Vallons, contact traversable).
        JsonArray At(double x, double z) => new(x, z);
        Assert.Equal("$.data.safePoints[0].position", FirstError(Zone(r => Data(r)["safePoints"]![0]!["position"] = At(-3, 9))));        // dans le moulin
        Assert.Equal("$.data.safePoints[0].position", FirstError(Zone(r => Data(r)["safePoints"]![0]!["position"] = At(-99.75, -12))));  // au bord
        Assert.Equal("$.data.safePoints[0].position", FirstError(Zone(r => Data(r)["safePoints"]![0]!["position"] = At(-6.25, 9))));     // contre le moulin
        Assert.True(Zone(r => Data(r)["safePoints"]![0]!["position"] = At(-99.5, -12)).IsValid);                                        // bord exact
    }

    [Fact]
    public void Laisse_mesuree_entre_les_bords()
    {
        GroundVector a = GroundVector.Parse("0", "0"), b = GroundVector.Parse("10", "0");
        Fixed r = Fixed.Parse("0.5");
        Assert.False(ZonePopulations.Beyond(a, r, b, r, Fixed.FromInt(9)));        // écart exact : 9 m, pas au-delà
        Assert.True(ZonePopulations.Beyond(a, r, b, r, Fixed.Parse("8.9999")));
        Assert.False(ZonePopulations.Beyond(a, r, GroundVector.Parse("6", "8"), r, Fixed.FromInt(9)));   // diagonale : 10 m entre centres
    }

    // --- Documents construits hors du lecteur JSON ----------------------------------------------------

    private static CombatSetup ZoneScenario => ScenarioJsonReader.ReadFile(ScenarioPath("zones/vallons"), ShippedData).Setup!;

    private static ZoneDocument Dto(Action<ZoneData>? edit = null)
    {
        var data = new ZoneData
        {
            Id = "zone.vallons",
            Name = "Vallons",
            Scenario = "zones/vallons",
            Entry = "place",
            SafePoints = new List<SafePointData?> { new() { Id = "place", Name = "Place", Position = new PointData { X = Fixed.FromInt(-18), Z = Fixed.FromInt(-12) } } },
            Places = new List<PlaceData?>(),
            Populations = new List<PopulationData?> { new() { Creature = "loup1", RespawnMs = 90000, LeashRange = Fixed.FromInt(20) } },
            Passages = new List<PassageData?>(),
        };
        edit?.Invoke(data);
        return new ZoneDocument { SchemaVersion = ZoneValidator.CurrentSchemaVersion, Kind = ZoneValidator.Kind, ContentVersion = "0.6.0", Data = data };
    }

    [Fact]
    public void Document_construit_valide() => Assert.True(ZoneValidator.Validate(Dto(), ZoneScenario, Items).IsValid);

    [Fact]
    public void Point_sans_coordonnee_refuse_sans_exception()
    {
        ZoneLoadResult safe = ZoneValidator.Validate(Dto(d => d.SafePoints![0]!.Position = new PointData { X = Fixed.Zero }), ZoneScenario, Items);
        Assert.Equal("$.data.safePoints[0].position", FirstError(safe));
        ZoneLoadResult place = ZoneValidator.Validate(Dto(d => d.Places!.Add(new PlaceData
        {
            Id = "village", Name = "Village", Min = new PointData { Z = Fixed.Zero }, Max = new PointData { X = Fixed.FromInt(1), Z = Fixed.FromInt(1) },
        })), ZoneScenario, Items);
        Assert.Equal("$.data.places[0].min", FirstError(place));
    }

    [Fact]
    public void Creature_reservee_refusee_meme_sans_scenario()
    {
        ZoneLoadResult result = ZoneValidator.Validate(Dto(d => d.Populations![0]!.Creature = "loup1_v2"), null, Items);
        Assert.Contains(result.Report.Errors, e => e.Path == "$.data.populations[0].creature" && e.Code == ValidationCode.InvalidValue);
        ZoneLoadResult longName = ZoneValidator.Validate(Dto(d => d.Populations![0]!.Creature = new string('a', ZoneValidator.MaxCreatureLength + 1)), null, Items);
        Assert.Contains(longName.Report.Errors, e => e.Path == "$.data.populations[0].creature" && e.Code == ValidationCode.InvalidValue);
    }

    [Fact]
    public void Creature_en_double_refusee_meme_si_la_premiere_est_invalide()
    {
        ZoneLoadResult result = ZoneValidator.Validate(Dto(d =>
        {
            d.Populations![0]!.RespawnMs = 1;
            d.Populations.Add(new PopulationData { Creature = "loup1", RespawnMs = 90000, LeashRange = Fixed.FromInt(20) });
        }), ZoneScenario, Items);
        Assert.Contains(result.Report.Errors, e => e.Path == "$.data.populations[1].creature" && e.Code == ValidationCode.DuplicateId);
    }

    [Fact]
    public void Schema_aligne_sur_le_validateur()
    {
        MiniJsonSchema schema = MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "zone.schema.json"));
        JsonObject Edited(Action<JsonObject> edit)
        {
            JsonObject root = JsonNode.Parse(File.ReadAllText(ZonePath))!.AsObject();
            edit(root);
            return root;
        }
        bool Valid(JsonObject root) => schema.IsValid(JsonDocument.Parse(root.ToJsonString()).RootElement);
        Assert.False(Valid(Edited(r => Data(r)["populations"]![0]!["creature"] = "loup1_v2")));
        Assert.False(Valid(Edited(r => Data(r)["populations"]![0]!["creature"] = new string('a', ZoneValidator.MaxCreatureLength + 1))));
        Assert.False(Valid(Edited(r => Data(r)["safePoints"]![0]!["id"] = new string('a', 65))));
        Assert.False(Valid(Edited(r => Data(r)["scenario"] = new string('a', 201))));
        Assert.True(Valid(Edited(_ => { })));
    }
}
