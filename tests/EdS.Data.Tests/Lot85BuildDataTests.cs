using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Builds;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 8.5 (décision A12) : builds de référence livrés en données (<c>data/v0/builds/bastion.json</c>, type « builds », schéma 1).
/// Builds de la feuille de route (Rempart, Contrecoup, Gardien, contrôles hybride et niveau 10), attributs communs du niveau 30,
/// validation unique (BuildValidator) avec des erreurs localisées, chargement avec les données, CLI et schéma JSON.
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Build")]
[Trait("Category", "Bastion")]
public class Lot85BuildDataTests
{
    private static string BuildsPath => Path.Combine(DataDirectory, "builds", "bastion.json");

    private static JsonObject ShippedBuilds() => JsonNode.Parse(File.ReadAllText(BuildsPath))!.AsObject();

    private static JsonObject Entry(JsonObject root, int index) => root["data"]!["builds"]![index]!.AsObject();

    private static BuildSetLoadResult ReadMutated(Action<JsonObject> mutate)
    {
        JsonObject root = ShippedBuilds();
        mutate(root);
        return BuildSetJsonReader.Read(root.ToJsonString(), ShippedData);
    }

    private static void AssertError(BuildSetLoadResult result, string path, ValidationCode code, string? message = null)
    {
        Assert.False(result.IsValid);
        Assert.True(result.Report.Errors.Any(e => e.Path == path && e.Code == code && (message is null || e.Message.Contains(message, StringComparison.Ordinal))),
            $"{path} [{code}] attendu ; obtenu :\n{result.Report}");
    }

    private static string S(string skill) => "skill.bastion." + skill;

    // --- Builds livrés ----------------------------------------------------------------------------------------------------------

    public static TheoryData<string, string, int, int[], string?, string[], string[]> Roadmap() => new()
    {
        // id, catégorie, niveau, points (Rempart, Contrecoup, Gardien), majeur, actives, passifs
        { "rempart", "reference", 30, new[] { 19, 2, 8 }, "Rempart éternel",
            new[] { "frappe_du_rempart", "heurt_de_bouclier", "barriere_d_acier", "onde_de_rupture" },
            new[] { "endurance_du_roc", "bouclier_lourd", "reserve_de_charges", "posture_de_fer" } },
        { "contrecoup", "reference", 30, new[] { 10, 19, 0 }, "Jugement du colosse",
            new[] { "frappe_du_rempart", "contrecoup", "onde_de_rupture", "heurt_de_bouclier" },
            new[] { "oeil_du_duelliste", "ferveur", "endurance_du_roc", "tenacite" } },
        { "gardien", "reference", 30, new[] { 9, 1, 19 }, "Serment du gardien",
            new[] { "provocation", "barriere_d_acier", "frappe_du_rempart", "heurt_de_bouclier" },
            new[] { "sentinelle", "posture_de_fer", "tenacite", "endurance_du_roc" } },
        { "hybride", "control", 30, new[] { 10, 10, 9 }, null,
            new[] { "frappe_du_rempart", "contrecoup", "barriere_d_acier", "onde_de_rupture" },
            new[] { "endurance_du_roc", "posture_de_fer", "oeil_du_duelliste", "ferveur" } },
        { "niveau_10", "control", 10, new[] { 4, 3, 2 }, null,
            new[] { "frappe_du_rempart", "heurt_de_bouclier", "contrecoup", "provocation" },
            new[] { "endurance_du_roc", "posture_de_fer" } },
    };

    [Theory]
    [MemberData(nameof(Roadmap))]
    public void Builds_livres_conformes_a_la_feuille_de_route(string id, string category, int level, int[] points, string? major, string[] actives, string[] passives)
    {
        Assert.True(ShippedData.TryGetBuild("build.bastion." + id, out ReferenceBuild reference), id);
        CharacterBuild b = reference.Build;
        ClassDefinition bastion = ClassTestData.Bastion;
        Assert.Equal((category, "class.bastion", level), (BuildSetValidator.Id(reference.Category), b.ClassId, b.Level));
        // Attributs communs (feuille de route, A10), y compris pour le contrôle de niveau 10.
        Assert.Equal((30, 10, 6, 30, 15, 20), (b.Attributes.Force, b.Attributes.Dexterite, b.Attributes.Intelligence, b.Attributes.Vitalite, b.Attributes.Volonte, b.Attributes.Endurance));
        Assert.Equal(points, bastion.Trees.Select(t => (int)BuildValidator.PointsInTree(b, t)));
        Assert.Equal(major, bastion.Trees.SelectMany(t => t.Talents).Where(t => t.IsMajor && b.TalentRanks.GetValueOrDefault(t.Id) > 0).Select(t => t.Name).SingleOrDefault());
        Assert.Equal(actives.Select(S), b.ActiveSkills);
        Assert.Equal(S("bastion_absolu"), b.Ultimate);
        Assert.Equal(passives.Select(S), b.PassiveSkills);
        Assert.Empty(BuildValidator.Validate(b, bastion, ShippedData.Rules.Progression));
        Assert.Equal(ShippedData.Rules.Progression.TalentPoints(level), b.TalentRanks.Values.Sum());   // tous les points dépensés
    }

    [Fact]
    public void Fichier_livre_lu_avec_les_donnees()
    {
        LoadedBuildSet set = Assert.Single(ShippedData.BuildSets);
        Assert.Equal((1, "0.1.0-provisoire", "class.bastion"), (set.SchemaVersion, set.ContentVersion, set.ClassId));
        Assert.Equal(new[] { "build.bastion.rempart", "build.bastion.contrecoup", "build.bastion.gardien", "build.bastion.hybride", "build.bastion.niveau_10" },
            set.Builds.Select(b => b.Id));
        Assert.Equal(new[] { "Rempart", "Contrecoup", "Gardien", "Hybride sans majeur", "Niveau 10" }, set.Builds.Select(b => b.Name));
        Assert.False(ShippedData.TryGetBuild("build.bastion.inconnu", out _));
        // Les builds n'entrent pas dans l'identité des données d'un combat : celle-ci ne change pas avec eux.
        static string Of(CombatDataIdentity i) => string.Join(" ", new[] { i.Rules }.Concat(i.Classes).Select(f => $"{f.Id}/{f.ContentVersion}/{f.Fingerprint}"));
        Assert.Equal(Of(new GameData(ShippedData.Rules, ShippedData.Classes).Identity), Of(ShippedData.Identity));
    }

    private static JsonObject ToJson(CharacterBuild b)
    {
        var talents = new JsonObject();
        foreach (var (id, rank) in b.TalentRanks) talents[id] = rank;
        return new JsonObject
        {
            ["level"] = b.Level,
            ["attributes"] = new JsonObject { ["force"] = b.Attributes.Force, ["dexterite"] = b.Attributes.Dexterite, ["intelligence"] = b.Attributes.Intelligence,
                ["vitalite"] = b.Attributes.Vitalite, ["volonte"] = b.Attributes.Volonte, ["endurance"] = b.Attributes.Endurance },
            ["talents"] = talents,
            ["activeSkills"] = new JsonArray(b.ActiveSkills.Select(s => (JsonNode?)s).ToArray()),
            ["ultimate"] = b.Ultimate,
            ["passiveSkills"] = new JsonArray(b.PassiveSkills.Select(s => (JsonNode?)s).ToArray()),
        };
    }

    [Theory]
    [InlineData("build.bastion.rempart")]
    [InlineData("build.bastion.contrecoup")]
    [InlineData("build.bastion.gardien")]
    [InlineData("build.bastion.hybride")]
    [InlineData("build.bastion.niveau_10")]
    public void Chaque_build_joue_un_combat_d_entrainement(string id)
    {
        // Un assaillant toutes les 1,5 s pendant 20 s, un allié et un mannequin ; garde, parades, chaque active lancée une fois.
        Assert.True(ShippedData.TryGetBuild(id, out ReferenceBuild reference));
        var commands = new JsonArray();
        long at = 0;
        foreach (string skill in reference.Build.ActiveSkills)
        {
            string? target = skill switch { "skill.bastion.barriere_d_acier" => "allie", "skill.bastion.onde_de_rupture" => null, _ => "assaillant" };
            commands.Add(new JsonObject { ["atMs"] = at, ["actor"] = "bastion", ["skill"] = skill, ["target"] = target });
            at += 1000;
        }
        commands.Add(new JsonObject { ["atMs"] = 5000, ["actor"] = "bastion", ["action"] = "guardStart" });
        for (long t = 5950; t < 20_000; t += 3000) commands.Add(new JsonObject { ["atMs"] = t, ["actor"] = "bastion", ["action"] = "parry" });
        JsonObject Character(string name, JsonObject build) => new()
        {
            ["id"] = name, ["type"] = "character", ["team"] = "joueurs", ["initiative"] = 0, ["class"] = "class.bastion", ["build"] = build,
        };
        var root = new JsonObject
        {
            ["schemaVersion"] = 4, ["kind"] = "scenario", ["contentVersion"] = "0.1.0-test",
            ["data"] = new JsonObject
            {
                ["seed"] = 7, ["durationMs"] = 20_000, ["commands"] = commands,
                ["combatants"] = new JsonArray(
                    Character("bastion", ToJson(reference.Build)),
                    Character("allie", new JsonObject { ["level"] = 1, ["attributes"] = new JsonObject { ["force"] = 10, ["dexterite"] = 10, ["intelligence"] = 10,
                        ["vitalite"] = 30, ["volonte"] = 10, ["endurance"] = 10 }, ["talents"] = new JsonObject(), ["activeSkills"] = new JsonArray(), ["passiveSkills"] = new JsonArray() }),
                    new JsonObject { ["id"] = "assaillant", ["type"] = "assailant", ["team"] = "cibles", ["initiative"] = 0, ["armor"] = 50, ["magicResistance"] = 0,
                        ["target"] = "bastion", ["firstAttackMs"] = 1000, ["intervalMs"] = 1500,
                        ["attacks"] = new JsonArray(new JsonObject { ["id"] = "coup", ["rawDamage"] = 60, ["damageType"] = "physique", ["targeting"] = "enemy", ["unblockable"] = false,
                            ["applies"] = new JsonArray(new JsonObject { ["status"] = "fragility" }) }) },
                    new JsonObject { ["id"] = "mannequin", ["type"] = "dummy", ["team"] = "cibles", ["initiative"] = 0, ["armor"] = 0, ["magicResistance"] = 0 }),
            },
        };
        ScenarioLoadResult loaded = ScenarioJsonReader.Read(root.ToJsonString(), ShippedData);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        var log = new MemoryCombatLog();
        CombatResult result = CombatEngine.Run(loaded.Setup!, log);
        Assert.Equal(CombatEndReason.DurationReached, result.EndReason);
        Assert.True(result["bastion"].SkillsCast >= 3, $"{id} : {result["bastion"].SkillsCast} compétences");
        Assert.True(result["bastion"].DamageDealt > 0);
        Assert.False(result["bastion"].IsDead);
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines, ShippedData).Status);
    }

    // --- Validation : en-tête et forme ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Version_de_schema_non_prise_en_charge(int version) =>
        AssertError(ReadMutated(r => r["schemaVersion"] = version), "$.schemaVersion", ValidationCode.UnsupportedSchemaVersion);

    [Fact]
    public void En_tete_controle()
    {
        AssertError(ReadMutated(r => r["kind"] = "scenario"), "$.kind", ValidationCode.WrongKind);
        AssertError(ReadMutated(r => r.Remove("contentVersion")), "$.contentVersion", ValidationCode.MissingField);
        AssertError(ReadMutated(r => r.Remove("data")), "$.data", ValidationCode.MissingField);
    }

    [Fact]
    public void Classe_obligatoire_et_chargee()
    {
        AssertError(ReadMutated(r => r["data"]!.AsObject().Remove("class")), "$.data.class", ValidationCode.MissingField);
        AssertError(ReadMutated(r => r["data"]!["class"] = "class.mage"), "$.data.class", ValidationCode.UnknownReference, "class.bastion");
    }

    [Fact]
    public void Liste_des_builds_obligatoire_non_vide_et_bornee()
    {
        AssertError(ReadMutated(r => r["data"]!.AsObject().Remove("builds")), "$.data.builds", ValidationCode.MissingField);
        AssertError(ReadMutated(r => r["data"]!["builds"] = new JsonArray()), "$.data.builds", ValidationCode.InvalidValue);
        BuildSetLoadResult tooMany = ReadMutated(r =>
        {
            var builds = new JsonArray();
            for (int i = 0; i < BuildSetValidator.MaxBuilds + 1; i++)
            {
                JsonObject b = Entry(r, 4).DeepClone().AsObject();
                b["id"] = $"build.bastion.controle_{i}";
                builds.Add(b);
            }
            r["data"]!["builds"] = builds;
        });
        AssertError(tooMany, "$.data.builds", ValidationCode.LimitExceeded, "65 builds, au plus 64");
        // 64 : admis.
        BuildSetLoadResult cap = ReadMutated(r =>
        {
            var builds = new JsonArray();
            for (int i = 0; i < BuildSetValidator.MaxBuilds; i++)
            {
                JsonObject b = Entry(r, 4).DeepClone().AsObject();
                b["id"] = $"build.bastion.controle_{i}";
                builds.Add(b);
            }
            r["data"]!["builds"] = builds;
        });
        Assert.True(cap.IsValid, cap.Report.ToString());
    }

    [Fact]
    public void Identifiants_stables_uniques_et_de_la_classe()
    {
        AssertError(ReadMutated(r => Entry(r, 0)["id"] = "Rempart"), "$.data.builds[0].id", ValidationCode.InvalidValue);
        AssertError(ReadMutated(r => Entry(r, 0)["id"] = "talent.bastion.rempart"), "$.data.builds[0].id", ValidationCode.InvalidValue);
        AssertError(ReadMutated(r => Entry(r, 1)["id"] = "build.bastion.rempart"), "$.data.builds[1].id", ValidationCode.DuplicateId);
        AssertError(ReadMutated(r => Entry(r, 2)["id"] = "build.mage.gardien"), "$.data.builds[2].id", ValidationCode.InvalidValue, "build.bastion.");
        AssertError(ReadMutated(r => Entry(r, 0).Remove("id")), "$.data.builds[0].id", ValidationCode.MissingField);
    }

    [Fact]
    public void Nom_et_categorie_controles()
    {
        AssertError(ReadMutated(r => Entry(r, 0)["name"] = "  "), "$.data.builds[0].name", ValidationCode.InvalidValue);
        AssertError(ReadMutated(r => Entry(r, 0).Remove("name")), "$.data.builds[0].name", ValidationCode.MissingField);
        AssertError(ReadMutated(r => Entry(r, 0)["category"] = "principal"), "$.data.builds[0].category", ValidationCode.UnknownReference);
        AssertError(ReadMutated(r => Entry(r, 0).Remove("category")), "$.data.builds[0].category", ValidationCode.MissingField);
        AssertError(ReadMutated(r => Entry(r, 0)["role"] = "tank"), "$.data.builds[0].role", ValidationCode.UnknownField);
    }

    [Fact]
    public void Forme_du_build_commune_aux_scenarios()
    {
        AssertError(ReadMutated(r => Entry(r, 0).Remove("build")), "$.data.builds[0].build", ValidationCode.MissingField);
        AssertError(ReadMutated(r => Entry(r, 0)["build"]!.AsObject().Remove("level")), "$.data.builds[0].build.level", ValidationCode.MissingField);
        AssertError(ReadMutated(r => Entry(r, 0)["build"]!["attributes"]!.AsObject().Remove("volonte")), "$.data.builds[0].build.attributes.volonte", ValidationCode.MissingField);
        AssertError(ReadMutated(r => Entry(r, 0)["build"]!["attributes"]!["charisme"] = 3), "$.data.builds[0].build.attributes.charisme", ValidationCode.UnknownReference);
        AssertError(ReadMutated(r => Entry(r, 0)["build"]!["attributes"]!["force"] = -1), "$.data.builds[0].build.attributes.force", ValidationCode.InvalidValue);
        AssertError(ReadMutated(r => Entry(r, 0)["build"]!["activeSkills"]!.AsArray().Add(null)), "$.data.builds[0].build.activeSkills[4]", ValidationCode.WrongType);
        AssertError(ReadMutated(r => Entry(r, 0)["build"]!["level"] = "trente"), "$.data.builds[0].build.level", ValidationCode.WrongType);
    }

    [Fact]
    public void Regles_du_build_par_le_validateur_unique_rattachees_au_champ_fautif()
    {
        // Revue Codex de la PR #9 : chaque violation du validateur unique vise le champ concerné, avec le code du validateur.
        const string B = "$.data.builds";
        AssertError(ReadMutated(r => Entry(r, 0)["build"]!["talents"]!["talent.bastion.rempart.test_palier_1"] = 1),
            $"{B}[0].build.talents.talent.bastion.rempart.test_palier_1", ValidationCode.UnknownReference, "[UnknownTalent]");
        // Majeur sous le seuil : rattaché au majeur ; rang hors bornes : au talent.
        AssertError(ReadMutated(r => Entry(r, 0)["build"]!["talents"]!["talent.bastion.rempart.poids_du_rempart"] = 4),
            $"{B}[0].build.talents.talent.bastion.rempart.rempart_eternel", ValidationCode.InvalidValue, "[MajorThresholdNotMet]");
        AssertError(ReadMutated(r => Entry(r, 2)["build"]!["talents"]!["talent.bastion.gardien.presence"] = 6),
            $"{B}[2].build.talents.talent.bastion.gardien.presence", ValidationCode.InvalidValue, "[RankOutOfRange]");
        AssertError(ReadMutated(r => Entry(r, 3)["build"]!["talents"]!["talent.bastion.gardien.presence"] = 1),
            $"{B}[3].build.talents", ValidationCode.InvalidValue, "[TooManyPoints]");
        AssertError(ReadMutated(r => Entry(r, 4)["build"]!["passiveSkills"]!.AsArray().Add(S("ferveur"))),
            $"{B}[4].build.passiveSkills", ValidationCode.InvalidValue, "[TooManySkills]");
        AssertError(ReadMutated(r => Entry(r, 1)["build"]!["activeSkills"]!.AsArray().Add(S("inexistante"))),
            $"{B}[1].build.activeSkills[4]", ValidationCode.UnknownReference, "[UnknownSkill]");
        AssertError(ReadMutated(r => Entry(r, 1)["build"]!["activeSkills"]![1] = S("frappe_du_rempart")),
            $"{B}[1].build.activeSkills[1]", ValidationCode.InvalidValue, "[DuplicateSkill]");
        AssertError(ReadMutated(r => Entry(r, 0)["build"]!["passiveSkills"]![0] = S("provocation")),
            $"{B}[0].build.passiveSkills[0]", ValidationCode.InvalidValue, "[WrongSlotKind]");
        AssertError(ReadMutated(r => Entry(r, 0)["build"]!["level"] = 31), $"{B}[0].build.level", ValidationCode.InvalidValue, "[LevelOutOfRange]");
        AssertError(ReadMutated(r => Entry(r, 0)["build"]!["attributes"]!["force"] = 1_000_001), $"{B}[0].build.attributes.force", ValidationCode.InvalidValue,
            "[AttributeOutOfRange]");
        BuildSetLoadResult locked = ReadMutated(r => Entry(r, 4)["build"]!["level"] = 9);
        AssertError(locked, $"{B}[4].build.ultimate", ValidationCode.InvalidValue, "[UltimateLocked]");
        AssertError(locked, $"{B}[4].build.activeSkills", ValidationCode.InvalidValue, "[TooManySkills]");
        Assert.DoesNotContain(locked.Report.Errors, e => e.Path == $"{B}[4].build");   // plus aucune erreur sur le bloc entier
    }

    [Theory]
    [InlineData("classId", "$.data.class")]
    [InlineData("progression", "$.b")]
    [InlineData("level", "$.b.level")]
    [InlineData("attributes.volonte", "$.b.attributes.volonte")]
    [InlineData("talents", "$.b.talents")]
    [InlineData("activeSkills", "$.b.activeSkills")]
    [InlineData("activeSkills[3]", "$.b.activeSkills[3]")]
    [InlineData("passiveSkills[0]", "$.b.passiveSkills[0]")]
    [InlineData("ultimate", "$.b.ultimate")]
    [InlineData("talent.bastion.rempart.garde_stable", "$.b.talents.talent.bastion.rempart.garde_stable")]
    [InlineData("level\n", "$.b.talents.level\n")]   // fin de chaîne exigée (\z) : un saut de ligne n'est pas un emplacement
    public void Chemin_de_chaque_sujet_de_violation(string subject, string expected) =>
        Assert.Equal(expected, BuildSetValidator.PathOf(new BuildViolation(BuildViolationCode.UnknownTalent, subject, "m"), "$.b"));

    // --- Chargement, CLI, schéma ------------------------------------------------------------------------------------------------

    [Fact]
    public void Tables_et_builds_charges_non_modifiables()
    {
        // Revue Codex de la PR #9 : la table publique des catégories est immuable, même après transtypage ; les builds chargés aussi.
        var categories = Assert.IsAssignableFrom<IDictionary<string, ReferenceBuildCategory>>(BuildSetValidator.Categories);
        Assert.Throws<NotSupportedException>(() => categories.Remove("reference"));
        Assert.Throws<NotSupportedException>(() => categories["principal"] = ReferenceBuildCategory.Reference);
        Assert.Equal(new[] { "control", "reference" }, BuildSetValidator.Categories.Keys.OrderBy(k => k, StringComparer.Ordinal));
        var builds = Assert.IsAssignableFrom<IList<ReferenceBuild>>(Assert.Single(ShippedData.BuildSets).Builds);
        Assert.Throws<NotSupportedException>(() => builds.RemoveAt(0));
        var sets = Assert.IsAssignableFrom<IList<LoadedBuildSet>>(ShippedData.BuildSets);
        Assert.Throws<NotSupportedException>(() => sets.Clear());
    }

    [Fact]
    public void Un_fichier_de_builds_refuse_fait_echouer_le_chargement_des_donnees()
    {
        string directory = CopyData((relative, text) => relative == "builds/bastion.json"
            ? text.Replace("\"talent.bastion.gardien.vigilance\": 5", "\"talent.bastion.gardien.vigilance\": 6", StringComparison.Ordinal) : text);
        try
        {
            GameDataLoadResult loaded = GameDataLoader.Load(directory);
            Assert.Null(loaded.Data);
            var (path, report) = Assert.Single(loaded.Failures);
            Assert.EndsWith(Path.Combine("builds", "bastion.json"), path);
            Assert.Contains(report.Errors, e => e.Path == "$.data.builds[0].build.talents.talent.bastion.gardien.vigilance" && e.Message.Contains("[RankOutOfRange]"));
            // Sans les builds (eds validate d'un fichier de builds), les règles et les classes se chargent.
            Assert.Empty(GameDataLoader.Load(directory, builds: false).Data!.BuildSets);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Identifiant_de_build_deja_defini_par_un_autre_fichier()
    {
        string directory = CopyData((_, text) => text);
        try
        {
            File.Copy(Path.Combine(directory, "builds", "bastion.json"), Path.Combine(directory, "builds", "copie.json"));
            GameDataLoadResult loaded = GameDataLoader.Load(directory);
            Assert.Null(loaded.Data);
            var (path, report) = Assert.Single(loaded.Failures);
            Assert.EndsWith("copie.json", path);
            Assert.Equal(5, report.Errors.Count(e => e.Code == ValidationCode.DuplicateId && e.Message.Contains("un autre fichier")));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Cli_valide_les_builds_et_resume_chacun()
    {
        var run = Lot71Files.Cli(AppContext.BaseDirectory, "validate", BuildsPath);
        Assert.True(run.ExitCode == 0, run.Error);
        Assert.Contains("contentVersion 0.1.0-provisoire", run.Output);
        Assert.Contains("classe         Bastion (class.bastion), 5 builds", run.Output);
        Assert.Contains("build.bastion.rempart      Rempart (reference), niveau 30 : Rempart 19, Contrecoup 2, Gardien 8 ; Rempart éternel ; 4 actives, ultime, 4 passifs", run.Output);
        Assert.Contains("build.bastion.niveau_10    Niveau 10 (control), niveau 10 : Rempart 4, Contrecoup 3, Gardien 2 ; aucun majeur ; 4 actives, ultime, 2 passifs", run.Output);
        Lot71Files.InTempDirectory(directory =>
        {
            string path = Path.Combine(directory, "builds.json");
            JsonObject root = ShippedBuilds();
            Entry(root, 0)["category"] = "principal";
            File.WriteAllText(path, root.ToJsonString());
            var refused = Lot71Files.Cli(AppContext.BaseDirectory, "validate", path);
            Assert.Equal(1, refused.ExitCode);
            Assert.Contains("$.data.builds[0].category", refused.Error);
            return 0;
        });
    }

    [Fact]
    public void Schema_json_decrit_exactement_les_champs_lus()
    {
        JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "builds.schema.json")))!.AsObject();
        JsonObject scenario = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "scenario.schema.json")))!.AsObject();
        static string[] Keys(JsonNode node) => node["properties"]!.AsObject().Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        static string[] Required(JsonNode node) => node["required"]!.AsArray().Select(n => n!.GetValue<string>()).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Assert.Equal((1, "builds"), (schema["properties"]!["schemaVersion"]!["const"]!.GetValue<int>(), schema["properties"]!["kind"]!["const"]!.GetValue<string>()));
        Assert.Equal(new[] { "builds", "class" }, Keys(schema["properties"]!["data"]!));
        Assert.Equal(new[] { "builds", "class" }, Required(schema["properties"]!["data"]!));
        JsonNode entry = schema["$defs"]!["referenceBuild"]!;
        Assert.Equal(new[] { "build", "category", "id", "name" }, Keys(entry));
        Assert.Equal(new[] { "build", "category", "id", "name" }, Required(entry));
        Assert.Equal(BuildSetValidator.Categories.Keys.OrderBy(k => k, StringComparer.Ordinal), entry["properties"]!["category"]!["enum"]!.AsArray().Select(n => n!.GetValue<string>()).OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(BuildSetValidator.MaxBuilds, schema["properties"]!["data"]!["properties"]!["builds"]!["maxItems"]!.GetValue<int>());
        // Même forme de build que le personnage d'un scénario.
        Assert.Equal(scenario["$defs"]!["build"]!.ToJsonString(), schema["$defs"]!["build"]!.ToJsonString());
        Assert.Equal(BuildSetValidator.CurrentSchemaVersion, DataFormat.BuildsSchemaVersion);
    }
}
