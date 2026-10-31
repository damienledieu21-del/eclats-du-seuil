using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 12.2 : le personnage persistant. Progression en données (expérience par niveau, expérience des créatures, attributs de base
/// par classe et niveau, build de départ), personnage sauvegardé (sauvegarde en version 2, version 1 mise à jour à la lecture), et partie
/// en solo qui donne l'expérience des créatures vaincues.
/// </summary>
[Trait("Category", "World")]
public class Lot122CharacterTests
{
    private static string ProgressionPath => ProgressionJsonReader.PathIn(DataDirectory);

    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;

    private static CharacterProgression Shipped => ProgressionJsonReader.ReadFile(ProgressionPath, ShippedData).Progression!;

    private static ProgressionLoadResult Mutated(Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ProgressionPath))!.AsObject();
        mutate(root);
        return ProgressionJsonReader.Read(root.ToJsonString(), ShippedData);
    }

    private static JsonObject Data(JsonObject root) => root["data"]!.AsObject();

    private static JsonObject Bastion(JsonObject root) => Data(root)["classes"]![0]!.AsObject();

    private static ValidationError Error(ProgressionLoadResult result)
    {
        Assert.False(result.IsValid);
        return result.Report.Errors[0];
    }

    // --- Progression ------------------------------------------------------------------------------

    [Fact]
    public void Progression_livree()
    {
        ProgressionLoadResult loaded = ProgressionJsonReader.ReadFile(ProgressionPath, ShippedData);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        CharacterProgression p = loaded.Progression!;
        Assert.Equal(10, p.MaxLevel);   // cadrage Q3 : niveaux 1 à 10
        Assert.Equal(new long[] { 0, 100, 220, 360, 520, 700, 900, 1120, 1360, 1620 }, p.XpToLevel);
        Assert.Equal((1, 1, 2, 9, 10, 10), (p.LevelFor(0), p.LevelFor(99), p.LevelFor(100), p.LevelFor(1619), p.LevelFor(1620), p.LevelFor(1_000_000)));
        // Essai du 12.9 (25 octobre 2026) : 1 point d'expérience par tranche de 5 PV, pour les créatures rééquilibrées du niveau 1.
        Assert.Equal((30L, 52L, 24L, 1L), (p.KillXp(150), p.KillXp(260), p.KillXp(120), p.KillXp(4)));   // loup, sanglier, archer, 1 au moins
        ClassProgression bastion = p.ForClass("class.bastion");
        Assert.Equal(new Attributes(12, 6, 4, 12, 8, 6), bastion.At(1));
        Assert.Equal(new Attributes(18, 7, 5, 18, 10, 10), bastion.At(10));
        Assert.Equal(new[] { "skill.bastion.frappe_du_rempart", "skill.bastion.heurt_de_bouclier" }, bastion.StartActiveSkills);
        Assert.Equal(new[] { "skill.bastion.endurance_du_roc" }, bastion.StartPassiveSkills);
        Assert.Throws<ArgumentOutOfRangeException>(() => bastion.At(11));
        Assert.Throws<ArgumentException>(() => p.ForClass("class.mage"));
        Assert.Throws<ArgumentOutOfRangeException>(() => p.LevelFor(-1));
        // Le fichier livré suit son schéma JSON.
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "progression.schema.json"))
            .IsValid(JsonDocument.Parse(File.ReadAllText(ProgressionPath)).RootElement));
    }

    [Fact]
    public void Progression_invalide_refusee_avec_son_chemin()
    {
        Assert.Equal("$.data.maxLevel", Error(Mutated(r => Data(r)["maxLevel"] = 31)).Path);
        Assert.Equal("$.data.xpToLevel", Error(Mutated(r => Data(r)["xpToLevel"]!.AsArray().RemoveAt(9))).Path);
        Assert.Equal("$.data.xpToLevel[0]", Error(Mutated(r => Data(r)["xpToLevel"]![0] = 5)).Path);
        Assert.Equal("$.data.xpToLevel[3]", Error(Mutated(r => Data(r)["xpToLevel"]![3] = 220)).Path);
        Assert.Equal("$.data.killXpPerHealth", Error(Mutated(r => Data(r)["killXpPerHealth"] = 0)).Path);
        Assert.Equal("$.data.classes[0].class", Error(Mutated(r => Bastion(r)["class"] = "class.mage")).Path);
        Assert.Equal("$.data.classes[0].attributes", Error(Mutated(r => Bastion(r)["attributes"]!.AsArray().RemoveAt(0))).Path);
        Assert.Equal("$.data.classes[0].attributes[5].force", Error(Mutated(r => Bastion(r)["attributes"]![5]!["force"] = 1)).Path);
        Assert.Equal("$.data.classes[0].attributes[2].volonte", Error(Mutated(r => Bastion(r)["attributes"]![2]!.AsObject().Remove("volonte"))).Path);
        Assert.Equal("$.data.classes[0].attributes[0].chance", Error(Mutated(r => Bastion(r)["attributes"]![0]!["chance"] = 1)).Path);
        // Trois actives au niveau 1 (deux emplacements) : le build de départ est refusé par le validateur des builds.
        Assert.Equal("$.data.classes[0].startActiveSkills", Error(Mutated(r => Bastion(r)["startActiveSkills"]!.AsArray().Add("skill.bastion.provocation"))).Path);
        Assert.Equal("$.data.classes[0].startPassiveSkills", Error(Mutated(r => Bastion(r)["startPassiveSkills"] = new JsonArray("skill.bastion.inconnue"))).Path);
        Assert.Equal("$.data.classes[1].class", Error(Mutated(r => Data(r)["classes"]!.AsArray().Add(Bastion(r).DeepClone()))).Path);
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion),
            (Error(Mutated(r => r["schemaVersion"] = 3)).Path, Error(Mutated(r => r["schemaVersion"] = 3)).Code));
        // Sous-lot 12.9b : la version 1 (sans compétences gagnées) est refusée.
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion),
            (Error(Mutated(r => r["schemaVersion"] = 1)).Path, Error(Mutated(r => r["schemaVersion"] = 1)).Code));
    }

    // --- Personnage --------------------------------------------------------------------------------

    [Fact]
    public void Nouveau_personnage_et_son_build_au_niveau_de_son_experience()
    {
        CharacterProgression p = Shipped;
        SavedCharacter hero = PlayerCharacters.Create("bastion", "class.bastion", p);
        Assert.Equal((0L, 1), (hero.Xp, PlayerCharacters.Level(hero, p)));
        CharacterBuild build = PlayerCharacters.Build(hero, p);
        Assert.Equal((1, new Attributes(12, 6, 4, 12, 8, 6)), (build.Level, build.Attributes));
        CharacterSetup setup = PlayerCharacters.Setup(hero, p, ShippedData, new CombatantPlacement(GroundVector.Parse("-18", "-12"), Fixed.Parse("0.5")));
        Assert.Equal(("bastion", "joueurs"), (setup.Id, setup.Team));
        // Niveau 5 : attributs du niveau 5, mêmes compétences.
        CharacterBuild five = PlayerCharacters.Build(hero.WithXp(520), p);
        Assert.Equal((5, p.ForClass("class.bastion").At(5)), (five.Level, five.Attributes));
        // Un talent de trop pour son niveau : refusé à l'entrée (le build est validé contre la classe et le niveau).
        var greedy = new SavedCharacter("bastion", "class.bastion", 0, new Dictionary<string, int> { ["talent.bastion.rempart.garde_stable"] = 1 },
            hero.ActiveSkills, null, hero.PassiveSkills);
        Assert.Contains("niveau 1", Assert.Throws<ArgumentException>(() => PlayerCharacters.Setup(greedy, p, ShippedData,
            new CombatantPlacement(GroundVector.Parse("0", "0"), Fixed.Parse("0.5")))).Message);
        Assert.Throws<ArgumentException>(() => PlayerCharacters.Setup(new SavedCharacter("x", "class.mage", 0), p, ShippedData,
            new CombatantPlacement(GroundVector.Parse("0", "0"), Fixed.Parse("0.5"))));
    }

    // --- Sauvegarde, version 2 ---------------------------------------------------------------------

    private static WorldSave HeroSave(long xp = 40) =>
        new("zone.vallons", 1200, 1, new[] { "zone.vallons/1/death/loup1" }, PlayerCharacters.Create("bastion", "class.bastion", Shipped).WithXp(xp));

    [Fact]
    public void Sauvegarde_du_personnage_lue_et_ecrite_a_l_identique()
    {
        string text = SaveJsonReader.Write(HeroSave());
        Assert.Contains(SaveVersion.Current + ",", text, StringComparison.Ordinal);   // sous-lot V1.1 : version 6 (bourse) ; V1.2 : 7 (18 places) ; V1.6 : 8 (quête suivie) ; V1.3 : 9 (instances d'objets)
        Assert.Contains("\"character\": {\n      \"id\": \"bastion\",\n      \"class\": \"class.bastion\",\n      \"xp\": 40,", text, StringComparison.Ordinal);
        SaveLoadResult read = SaveJsonReader.Read(text);
        Assert.True(read.IsValid, read.Report.ToString());
        SavedCharacter c = read.Save!.Character!;
        Assert.Equal(("bastion", "class.bastion", 40L, false), (c.Id, c.ClassId, c.Xp, read.Save.Migrated));
        Assert.Equal(new[] { "skill.bastion.frappe_du_rempart", "skill.bastion.heurt_de_bouclier" }, c.ActiveSkills);
        Assert.Equal(text, SaveJsonReader.Write(read.Save));
        string talented = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), new SavedCharacter("bastion", "class.bastion", 1620,
            new Dictionary<string, int> { ["talent.bastion.rempart.peau_de_fer"] = 3, ["talent.bastion.gardien.vigilance"] = 2 }, new[] { "a.b" }, "skill.x", new[] { "c.d" })));
        Assert.Contains("\"talents\": {\"talent.bastion.gardien.vigilance\": 2, \"talent.bastion.rempart.peau_de_fer\": 3},", talented, StringComparison.Ordinal);
        Assert.Equal(talented, SaveJsonReader.Write(SaveJsonReader.Read(talented).Save!));
        Assert.Contains("\"character\": null,", SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>())), StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), new SavedCharacter("bastion", "Classe Bastion", 0))));
        // Les sauvegardes écrites suivent leur schéma JSON.
        MiniJsonSchema schema = MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json"));
        Assert.True(schema.IsValid(JsonDocument.Parse(text).RootElement));
        Assert.True(schema.IsValid(JsonDocument.Parse(talented).RootElement));
        Assert.True(schema.IsValid(JsonDocument.Parse(SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>()))).RootElement));
    }

    [Fact]
    public void Sauvegarde_de_version_1_mise_a_jour_a_la_lecture()
    {
        const string v1 = "{\"schemaVersion\": 1, \"kind\": \"save\", \"contentVersion\": \"0.1.0-provisoire\", \"data\": {\"zone\": \"zone.vallons\", \"worldTick\": 60, \"sessions\": 1, \"consumed\": [\"zone.vallons/1/death/loup1\"]}}";
        SaveLoadResult read = SaveJsonReader.Read(v1);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.True(read.Save!.Migrated);
        Assert.Null(read.Save.Character);
        Assert.Equal((60L, 1L), (read.Save.WorldTick, read.Save.Sessions));
        string rewritten = SaveJsonReader.Write(read.Save);
        Assert.Contains(SaveVersion.Current + ",", rewritten, StringComparison.Ordinal);
        Assert.False(SaveJsonReader.Read(rewritten).Save!.Migrated);
        // Un personnage dans une sauvegarde de version 1 n'existe pas encore : champ inconnu.
        Assert.Equal("$.data.character", Assert.Single(SaveJsonReader.Read(v1.Replace("\"consumed\"", "\"character\": null, \"consumed\"")).Report.Errors).Path);
    }

    [Theory]
    [InlineData("\"character\": null, ", "", "$.data.character")]                                   // absent en version 2
    [InlineData("\"id\": \"bastion\"", "\"id\": \"Bastion\"", "$.data.character.id")]
    [InlineData("\"xp\": 40", "\"xp\": -1", "$.data.character.xp")]
    [InlineData("\"xp\": 40", "\"xp\": 1000000001", "$.data.character.xp")]
    [InlineData("\"talents\": {}", "\"talents\": {\"t.a\": -1}", "$.data.character.talents.t.a")]
    [InlineData("\"activeSkills\": [", "\"activeSkills\": [null, ", "$.data.character.activeSkills[0]")]
    [InlineData("\"schemaVersion\": {current}", "\"schemaVersion\": {future}", "$.schemaVersion")]
    [InlineData("      \"ultimate\": null,\n", "", "$.data.character.ultimate")]                     // revue Codex de la PR #43 : obligatoire
    [InlineData("\"id\": \"bastion\"", "\"id\": \"b12345678901234567890123456789012345678901234567890123456789012345\"", "$.data.character.id")]   // 66 caractères
    [InlineData("\"class\": \"class.bastion\"", "\"class\": \"Classe Bastion\"", "$.data.character.class")]   // revue Codex de la PR #43 : format de l'écriture
    [InlineData("\"talents\": {}", "\"talents\": {\"Talent\": 1}", "$.data.character.talents.Talent")]
    [InlineData("\"activeSkills\": [", "\"activeSkills\": [\"Frappe du rempart\", ", "$.data.character.activeSkills[0]")]
    [InlineData("\"ultimate\": null", "\"ultimate\": \"Ultime\"", "$.data.character.ultimate")]
    public void Sauvegarde_du_personnage_invalide(string from, string to, string path)
    {
        (from, to) = (SaveVersion.Expand(from), SaveVersion.Expand(to));
        string text = SaveJsonReader.Write(HeroSave());
        if (from == "\"character\": null, ")
            text = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>())).Replace("    \"character\": null,\n", "");
        else
        {
            Assert.Contains(from, text, StringComparison.Ordinal);
            text = text.Replace(from, to, StringComparison.Ordinal);
        }
        SaveLoadResult read = SaveJsonReader.Read(text);
        Assert.False(read.IsValid);
        Assert.Equal(path, read.Report.Errors[0].Path);
    }

    [Fact]
    public void Ecriture_refuse_un_personnage_qui_ne_se_relirait_pas()
    {
        // Revue Codex de la PR #43 : mêmes bornes qu'à la lecture, pour ne jamais remplacer une sauvegarde par un fichier illisible.
        WorldSave With(SavedCharacter c) => new("zone.vallons", 0, 0, Array.Empty<string>(), c);
        Assert.Throws<ArgumentException>(() => SaveJsonReader.Write(With(new SavedCharacter("bastion", "class.bastion", -1))));
        Assert.Throws<ArgumentException>(() => SaveJsonReader.Write(With(new SavedCharacter("bastion", "class.bastion", SaveValidator.MaxXp + 1))));
        Assert.Throws<ArgumentException>(() => SaveJsonReader.Write(With(new SavedCharacter("bastion", "class.bastion", 0,
            new Dictionary<string, int> { ["talent.x"] = -1 }))));
        Assert.Throws<ArgumentException>(() => SaveJsonReader.Write(With(new SavedCharacter(new string('b', SaveValidator.MaxCharacterIdLength + 1), "class.bastion", 0))));
        Assert.True(SaveJsonReader.Read(SaveJsonReader.Write(With(new SavedCharacter(new string('b', SaveValidator.MaxCharacterIdLength), "class.bastion", 0)))).IsValid);
    }

    [Fact]
    public void Progression_exigee_pour_chaque_classe()
    {
        // Revue Codex de la PR #43 : une classe chargée sans progression est refusée au chargement, pas à la création d'un personnage.
        ProgressionLoadResult empty = Mutated(root => Data(root)["classes"] = new JsonArray());
        Assert.False(empty.IsValid);
        Assert.Equal("$.data.classes", Error(empty).Path);
    }

    // --- Partie en solo : expérience des créatures vaincues -----------------------------------------

    [Fact]
    public void Une_creature_vaincue_donne_son_experience_et_un_niveau()
    {
        // Progression d'essai : 1 point d'expérience par PV maximal, niveau 2 à 50 points ; un loup de 60 PV au contact du Bastion.
        CharacterProgression quick = Mutated(r =>
        {
            Data(r)["killXpPerHealth"] = 1;
            Data(r)["xpToLevel"] = new JsonArray(0, 50, 220, 360, 520, 700, 900, 1120, 1360, 1620);
        }).Progression!;
        ScenarioLoadResult loaded = ReadMutated(root =>
        {
            JsonObject wolf = root["data"]!["combatants"]!.AsArray().Select(c => c!.AsObject()).Single(c => c["id"]!.GetValue<string>() == "loup1");
            wolf["health"] = 60;
            wolf["position"] = new JsonArray(-16, -12);
        }, scenario: "zones/vallons");
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        var save = new WorldSave("zone.vallons", 7200, 1, Array.Empty<string>(), PlayerCharacters.Create("bastion", "class.bastion", quick));
        var session = new WorldSession(save, loaded.Setup!, ShippedData, quick, Items, new CombatantPlacement(GroundVector.Parse("-18", "-12"), Fixed.Parse("0.5")));
        Assert.Equal((2L, 1, 1), (session.Zone.Session, session.EnteredLevel, session.Level));
        var gains = new List<ExperienceGained>();
        for (int i = 0; i < 600 && gains.Count == 0; i++)
        {
            if (i % 20 == 1)
                try { session.Zone.Engine.Submit(new CombatCommand(session.Zone.Engine.CurrentTick * 50, "bastion", "skill.bastion.frappe_du_rempart", "loup1")); }
                catch (ArgumentException) { }
            gains.AddRange(session.Step().Experience);
        }
        ExperienceGained gain = Assert.Single(gains);
        Assert.Equal(("zone.vallons/2/death/loup1", 60L, 1, 2, true), (gain.Source.Key, gain.Xp, gain.LevelBefore, gain.LevelAfter, gain.LeveledUp));
        Assert.Equal((60L, 2, 1), (session.Character.Xp, session.Level, session.EnteredLevel));   // le niveau 2 prend effet à la prochaine entrée
        WorldSave after = session.Save();
        Assert.Equal((60L, 2L), (after.Character!.Xp, after.Sessions));
        Assert.Contains("zone.vallons/2/death/loup1", after.Consumed);
        Assert.True(after.WorldTick > 7200);
        // Reprise : le personnage entre au niveau 2, avec les attributs de ce niveau.
        var next = new WorldSession(SaveJsonReader.Read(SaveJsonReader.Write(after)).Save!, loaded.Setup!, ShippedData, quick, Items,
            new CombatantPlacement(GroundVector.Parse("-18", "-12"), Fixed.Parse("0.5")));
        Assert.Equal((3L, 2), (next.Zone.Session, next.EnteredLevel));
        next.Step();
        Assert.Equal(quick.ForClass("class.bastion").At(2).Vitalite, next.Zone.Engine.View("bastion").Character!.Build.Attributes.Vitalite);
    }

    /// <summary>Un loup de 60 PV au contact du Bastion, frappé jusqu'à sa mort ; rend la partie, les gains et la mort vue.</summary>
    private static (WorldSession Session, List<ExperienceGained> Gains, bool Died) KillWolf(long xp, ICombatLog? log = null)
    {
        ScenarioLoadResult loaded = ReadMutated(root =>
        {
            JsonObject wolf = root["data"]!["combatants"]!.AsArray().Select(c => c!.AsObject()).Single(c => c["id"]!.GetValue<string>() == "loup1");
            wolf["health"] = 60;
            wolf["position"] = new JsonArray(-16, -12);
        }, scenario: "zones/vallons");
        var save = new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), PlayerCharacters.Create("bastion", "class.bastion", Shipped).WithXp(xp));
        var session = new WorldSession(save, loaded.Setup!, ShippedData, Shipped, Items, new CombatantPlacement(GroundVector.Parse("-18", "-12"), Fixed.Parse("0.5")), log: log);
        var gains = new List<ExperienceGained>();
        for (int i = 0; i < 600 && !session.Zone.Engine.View("loup1").IsDead; i++)
        {
            if (i % 20 == 1)
                try { session.Zone.Engine.Submit(new CombatCommand(session.Zone.Engine.CurrentTick * 50, "bastion", "skill.bastion.frappe_du_rempart", "loup1")); }
                catch (ArgumentException) { }
            gains.AddRange(session.Step().Experience);
        }
        return (session, gains, session.Zone.Engine.View("loup1").IsDead);
    }

    [Theory]
    [InlineData("\"class\": \"class.bastion\"", "\"class\": \"Classe Bastion\"")]
    [InlineData("\"talents\": {}", "\"talents\": {\"Talent\": 1}")]
    [InlineData("\"activeSkills\": [", "\"activeSkills\": [\"Frappe du rempart\", ")]
    [InlineData("\"ultimate\": null", "\"ultimate\": \"Ultime\"")]
    [InlineData("\"id\": \"bastion\"", "\"id\": \"b12345678901234567890123456789012345678901234567890123456789012345\"")]
    [InlineData("\"sessions\": 1,", "\"sessions\": 630720001,")]   // revue Codex de la PR #43 (5e tour) : même borne que le temps du monde
    [InlineData("\"worldTick\": 1200,", "\"worldTick\": 630720001,")]
    [InlineData("\"contentVersion\": \"", "\"contentVersion\": \"v")]
    [InlineData("\"zone.vallons/1/death/loup1\"", "\"zone.vallons/1/death/loup1\", \"zone.vallons/1/death/loup1\"")]
    public void Schema_refuse_ce_que_la_lecture_refuse(string from, string to)
    {
        // Revue Codex de la PR #43 : le schéma JSON des sauvegardes suit le format exigé par la lecture.
        MiniJsonSchema schema = MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json"));
        string text = SaveJsonReader.Write(HeroSave());
        Assert.True(schema.IsValid(JsonDocument.Parse(text).RootElement));
        string bad = text.Replace(from, to, StringComparison.Ordinal);
        Assert.NotEqual(text, bad);
        Assert.False(SaveJsonReader.Read(bad).IsValid);
        Assert.False(schema.IsValid(JsonDocument.Parse(bad).RootElement));
    }

    [Fact]
    public void Talents_ecrits_dans_l_ordre_ordinal()
    {
        // Revue Codex de la PR #43 : l'ordre d'écriture ne dépend pas de l'ordre d'énumération d'un dictionnaire.
        var talents = new SortedDictionary<string, int>(Comparer<string>.Create((a, b) => -string.CompareOrdinal(a, b))) { ["t.a"] = 1, ["t.b"] = 2 };
        string text = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), new SavedCharacter("bastion", "class.bastion", 0, talents)));
        Assert.Contains("\"talents\": {\"t.a\": 1, \"t.b\": 2},", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Mort_creditee_au_personnage_qui_part_au_meme_tick()
    {
        // Revue Codex de la PR #43 : le départ prend effet en fin de tick, après les morts : une créature tuée au tick du départ compte.
        (WorldSession first, _, bool died) = KillWolf(0);
        Assert.True(died);
        long deathTick = first.Zone.Engine.CurrentTick - 1;   // la boucle s'arrête au tick qui suit la mort
        ScenarioLoadResult loaded = ReadMutated(root =>
        {
            JsonObject wolf = root["data"]!["combatants"]!.AsArray().Select(c => c!.AsObject()).Single(c => c["id"]!.GetValue<string>() == "loup1");
            wolf["health"] = 60;
            wolf["position"] = new JsonArray(-16, -12);
        }, scenario: "zones/vallons");
        var session = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), PlayerCharacters.Create("bastion", "class.bastion", Shipped)),
            loaded.Setup!, ShippedData, Shipped, Items, new CombatantPlacement(GroundVector.Parse("-18", "-12"), Fixed.Parse("0.5")));
        var gains = new List<ExperienceGained>();
        for (int i = 0; session.Zone.Engine.CurrentTick <= deathTick; i++)
        {
            if (i % 20 == 1)
                try { session.Zone.Engine.Submit(new CombatCommand(session.Zone.Engine.CurrentTick * 50, "bastion", "skill.bastion.frappe_du_rempart", "loup1")); }
                catch (ArgumentException) { }
            if (session.Zone.Engine.CurrentTick == deathTick) session.Zone.Leave("bastion");
            gains.AddRange(session.Step().Experience);
        }
        Assert.True(session.Zone.Engine.View("loup1").IsDead);
        Assert.True(session.Zone.Engine.View("bastion").HasLeft);
        Assert.Equal("zone.vallons/1/death/loup1", Assert.Single(gains).Source.Key);
    }

    [Fact]
    public void Experience_annoncee_telle_qu_ajoutee_sous_le_plafond()
    {
        // Revue Codex de la PR #43 : près du plafond, seule l'expérience réellement ajoutée est annoncée ; au plafond, rien.
        (WorldSession near, List<ExperienceGained> gains, bool died) = KillWolf(SaveValidator.MaxXp - 1);
        Assert.True(died);
        Assert.Equal(1L, Assert.Single(gains).Xp);
        Assert.Equal(SaveValidator.MaxXp, near.Character.Xp);
        (WorldSession full, List<ExperienceGained> none, bool diedToo) = KillWolf(SaveValidator.MaxXp);
        Assert.True(diedToo);
        Assert.Empty(none);
        Assert.Equal(SaveValidator.MaxXp, full.Character.Xp);
    }

    [Fact]
    public void Le_journal_de_la_session_decrit_le_personnage_des_l_en_tete()
    {
        // Revue Codex de la PR #43 : le personnage entre avec la session, dans sa simulation de départ ; l'en-tête du journal le décrit
        // (classe et build compris).
        var log = new MemoryCombatLog();
        (WorldSession session, _, bool died) = KillWolf(0, log);
        Assert.True(died);
        Assert.Contains("\"bastion\"", log.Lines[0], StringComparison.Ordinal);
        Assert.Contains("class.bastion", log.Lines[0], StringComparison.Ordinal);
        Assert.Contains(session.Zone.Engine.CompletedSetup().Combatants, c => c.Id == "bastion" && c.ArrivalMs == 0);
    }

    [Fact]
    public void Partie_sans_personnage_refusee()
    {
        Assert.Throws<ArgumentException>(() => new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>()),
            ScenarioJsonReader.ReadFile(ScenarioPath("zones/vallons"), ShippedData).Setup!, ShippedData, Shipped, Items,
            new CombatantPlacement(GroundVector.Parse("-18", "-12"), Fixed.Parse("0.5"))));
    }
}
