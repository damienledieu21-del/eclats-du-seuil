using System.Text.Json.Nodes;
using EdS.Data.Classes;
using EdS.Data.Json;
using EdS.Data.Validation;
using EdS.Domain.Builds;
using static EdS.Data.Tests.ClassTestData;

namespace EdS.Data.Tests;

[Trait("Category", "Data")]
public class ClassJsonReaderTests
{
    private static void AssertError(ClassLoadResult result, string path, ValidationCode code)
    {
        Assert.False(result.IsValid);
        Assert.True(result.Report.Errors.Any(e => e.Path == path && e.Code == code),
            $"{path} [{code}] attendu ; obtenu :\n{result.Report}");
    }

    // --- Fichier livré --------------------------------------------------------------

    [Fact]
    public void Bastion_livre_valide()
    {
        ClassLoadResult result = ClassJsonReader.ReadFile(BastionPath, Progression);
        Assert.True(result.IsValid, result.Report.ToString());
        Assert.Equal(9, result.Class!.SchemaVersion);   // sous-lot 11.4b : portées
        // 0.3.0 : fiches complètes des compétences (sous-lot 8.4) ; 0.4.0 : talents, intermédiaires et passifs (sous-lot 8.5) ; 0.5.0 :
        // Provocation inflige des dégâts (sous-lot 8.7, décision de Damien du 18/10/2026) ; 0.6.0 : portées du Bastion (sous-lot 11.4b,
        // retenues par Damien le 20/10/2026).
        Assert.Equal("0.6.0-provisoire", result.Class.ContentVersion);
        Assert.Matches("^sha256:[0-9a-f]{64}$", result.Class.Fingerprint);
    }

    [Fact]
    public void Bastion_contient_les_talents_decides()
    {
        ClassDefinition bastion = Bastion;
        Assert.Equal("class.bastion", bastion.Id);
        Assert.Equal(new[] { "Rempart", "Contrecoup", "Gardien" }, bastion.Trees.Select(t => t.Name));

        // Sous-lot 8.5 : les trois talents notables puis les trois intermédiaires de cinq rangs de chaque arbre (A2, noms de travail).
        void Real(int tree, params (string Name, int Rank, bool Major)[] expected) =>
            Assert.Equal(expected, bastion.Trees[tree].Talents.Select(t => (t.Name, t.MaxRank, t.IsMajor)));
        Real(0, ("Garde stable", 3, false), ("Écho défensif", 1, false), ("Rempart éternel", 1, true),
            ("Peau de fer", 5, false), ("Souffle du roc", 5, false), ("Poids du rempart", 5, false));
        Real(1, ("Riposte précise", 3, false), ("Fureur contenue", 2, false), ("Jugement du colosse", 1, true),
            ("Main lourde", 5, false), ("Élan", 5, false), ("Écho de guerre", 5, false));
        Real(2, ("Égide réactive", 3, false), ("Lien protecteur", 1, false), ("Serment du gardien", 1, true),
            ("Vigilance", 5, false), ("Présence", 5, false), ("Rempart vivant", 5, false));
    }

    [Fact]
    public void Plus_aucun_talent_ni_passif_de_test()
    {
        // Sous-lot 8.5 : plus aucun « [TEST] » ni marqueur placeholder dans le contenu livré ; les huit passifs du catalogue.
        Assert.All(Bastion.Trees.SelectMany(t => t.Talents), t => Assert.False(t.IsPlaceholder, t.Name));
        Assert.All(Bastion.Skills, s => Assert.False(s.IsPlaceholder, s.Name));
        Assert.DoesNotContain("[TEST]", BastionJson);
        Assert.DoesNotContain("test_", BastionJson);
        Assert.Equal(new[] { "Endurance du roc", "Bouclier lourd", "Réserve de Charges", "Œil du duelliste", "Ferveur", "Posture de fer", "Sentinelle", "Ténacité" },
            Bastion.Skills.Where(s => s.Kind == SkillKind.Passive).Select(s => s.Name));
    }

    [Fact]
    public void Un_ultime_et_des_competences_de_chaque_type()
    {
        Assert.Equal("Bastion absolu", Assert.Single(Bastion.Skills, s => s.Kind == SkillKind.Ultimate).Name);
        Assert.True(Bastion.Skills.Count(s => s.Kind == SkillKind.Active) >= Progression.ActiveSlots(Progression.MaxLevel));
        Assert.True(Bastion.Skills.Count(s => s.Kind == SkillKind.Passive) >= Progression.PassiveSlots(Progression.MaxLevel));
    }

    // --- Erreurs de forme ------------------------------------------------------------

    [Fact]
    public void Champ_obligatoire_absent() =>
        AssertError(ReadMutated(r => Talent(r, 0, 1).Remove("maxRank")), "$.data.trees[0].talents[1].maxRank", ValidationCode.MissingField);

    [Fact]
    public void Champ_inconnu_avec_ligne()
    {
        ClassLoadResult result = ReadMutated(r => Skill(r, 2)["cooldown"] = 3);
        AssertError(result, "$.data.skills[2].cooldown", ValidationCode.UnknownField);
        Assert.NotNull(result.Report.Errors.Single().Line);
    }

    [Fact]
    public void Ligne_exacte_dans_un_tableau()
    {
        // La ligne pointe sur la valeur fautive dans le fichier tel qu'écrit.
        string json = BastionJson;
        string marker = "\"maxRank\": 1,";
        int lineOfFirstMaxRank1 = json[..json.IndexOf(marker, StringComparison.Ordinal)].Count(c => c == '\n') + 1;
        string broken = json.Remove(json.IndexOf(marker, StringComparison.Ordinal), marker.Length).Insert(json.IndexOf(marker, StringComparison.Ordinal), "\"maxRank\": \"un\",");
        ClassLoadResult result = ClassJsonReader.Read(broken, Progression);
        ValidationError error = Assert.Single(result.Report.Errors);
        Assert.Equal("$.data.trees[0].talents[1].maxRank", error.Path);
        Assert.Equal(ValidationCode.WrongType, error.Code);
        Assert.Equal(lineOfFirstMaxRank1, error.Line);
    }

    [Fact]
    public void Type_de_competence_inconnu() =>
        AssertError(ReadMutated(r => Skill(r, 0)["kind"] = "reactive"), "$.data.skills[0].kind", ValidationCode.UnknownReference);

    [Fact]
    public void Element_null_dans_une_liste() =>
        AssertError(ReadMutated(r => DataOf(r)["skills"]!.AsArray().Insert(1, null)), "$.data.skills[1]", ValidationCode.WrongType);

    [Fact]
    public void Mauvais_type_de_fichier() =>
        AssertError(ReadMutated(r => r["kind"] = "rules"), "$.kind", ValidationCode.WrongKind);

    [Fact]
    public void Fichier_absent()
    {
        ClassLoadResult result = ClassJsonReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "absent.json"), Progression);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Report.Errors);
    }

    // --- Cohérence (validateur commun) ----------------------------------------------

    [Fact]
    public void Reference_de_talent_inconnue() =>
        AssertError(ReadMutated(r => Talent(r, 0, 1)["prerequisites"]![0]!["talent"] = "talent.bastion.rempart.fantome"),
            "$.data.trees[0].talents[1].prerequisites[0].talent", ValidationCode.UnknownReference);

    [Fact]
    public void Identifiant_en_double() =>
        AssertError(ReadMutated(r => Skill(r, 1)["id"] = Skill(r, 0)["id"]!.GetValue<string>()), "$.data.skills[1].id", ValidationCode.DuplicateId);

    [Fact]
    public void Rang_exige_superieur_au_rang_maximum() =>
        AssertError(ReadMutated(r => Talent(r, 0, 1)["prerequisites"]![0]!["rank"] = 4), "$.data.trees[0].talents[1].prerequisites[0].rank", ValidationCode.InvalidValue);

    [Fact]
    public void Majeur_rendu_inaccessible() =>
        // Sans Poids du rempart (5 points, sous-lot 8.5), Rempart n'offre plus que 14 points avant son majeur.
        AssertError(ReadMutated(r => DataOf(r)["trees"]![0]!["talents"]!.AsArray().RemoveAt(5)), "$.data.trees[0].talents[2]", ValidationCode.InvalidValue);

    [Fact]
    public void Ultime_avant_le_niveau_10_refusee()
    {
        // Sous-lot V1.8 : la place de l'ultime s'ouvre au niveau 1 dans les règles livrées ; avec des règles où elle s'ouvre au niveau 10
        // (celles d'avant), une ultime disponible au niveau 9 est toujours refusée.
        int ultimate = Bastion.Skills.ToList().FindIndex(s => s.Kind == SkillKind.Ultimate);
        JsonObject root = Shipped();
        Skill(root, ultimate)["minLevel"] = 9;
        AssertError(ClassJsonReader.Read(ToJson(root), new ProgressionRules { UltimateUnlockLevel = 10 }), $"$.data.skills[{ultimate}].minLevel",
            ValidationCode.InvalidValue);
        Assert.True(ReadMutated(r => Skill(r, ultimate)["minLevel"] = 9).IsValid);
    }

    [Fact]
    public void Seuil_des_majeurs_lu_dans_rules_json()
    {
        // Avec un seuil de 25, aucune branche du Bastion ne permet d'atteindre son majeur.
        var strict = new ProgressionRules { MajorTalentThreshold = 25 };
        ClassLoadResult result = ClassJsonReader.Read(BastionJson, strict);
        Assert.Equal(3, result.Report.Errors.Count(e => e.Message.StartsWith("talent majeur inaccessible", StringComparison.Ordinal)));
    }

    // --- Données de combat (lot 5) ----------------------------------------------------

    private static int FrappeIndex => Bastion.Skills.ToList().FindIndex(s => s.Id == "skill.bastion.frappe_du_rempart");

    [Fact]
    public void Donnees_de_combat_champ_absent() =>
        AssertError(ReadMutated(r => Skill(r, FrappeIndex)["combat"]!.AsObject().Remove("cooldownMs")),
            $"$.data.skills[{FrappeIndex}].combat.cooldownMs", ValidationCode.MissingField);

    [Fact]
    public void Donnees_de_combat_valeur_inconnue() =>
        AssertError(ReadMutated(r => Skill(r, FrappeIndex)["combat"]!["damageType"] = "feu"),
            $"$.data.skills[{FrappeIndex}].combat.damageType", ValidationCode.UnknownReference);

    [Fact]
    public void Donnees_de_combat_cout_negatif() =>
        AssertError(ReadMutated(r => Skill(r, FrappeIndex)["combat"]!["enduranceCost"] = -1),
            $"$.data.skills[{FrappeIndex}].combat.enduranceCost", ValidationCode.InvalidValue);

    [Theory]
    [InlineData("unblockable")]
    [InlineData("rampartChargeCost")]
    [InlineData("rampartChargeGain")]
    public void Donnees_de_combat_du_lot_6_obligatoires(string field) =>
        AssertError(ReadMutated(r => Skill(r, FrappeIndex)["combat"]!.AsObject().Remove(field)),
            $"$.data.skills[{FrappeIndex}].combat.{field}", ValidationCode.MissingField);

    [Fact]
    public void Donnees_de_combat_du_lot_6_lues()
    {
        var frappe = Bastion.Skills[FrappeIndex].Combat!;
        Assert.Equal((false, 0, 0), (frappe.Unblockable, frappe.RampartChargeCost, frappe.RampartChargeGain));
        var changed = ReadMutated(r =>
        {
            Skill(r, FrappeIndex)["combat"]!["unblockable"] = true;
            Skill(r, FrappeIndex)["combat"]!["rampartChargeCost"] = 2;
        }).Class!;
        Assert.True(changed.Definition.Skills[FrappeIndex].Combat!.Unblockable);
        Assert.Equal(2, changed.Definition.Skills[FrappeIndex].Combat!.RampartChargeCost);
        Assert.NotEqual(ClassJsonReader.Read(BastionJson, Progression).Class!.Fingerprint, changed.Fingerprint);
    }

    [Fact]
    public void Donnees_de_combat_dans_l_empreinte() =>
        Assert.NotEqual(ClassJsonReader.Read(BastionJson, Progression).Class!.Fingerprint,
            ReadMutated(r => Skill(r, FrappeIndex)["combat"]!["coefficient"] = 1.25).Class!.Fingerprint);

    // --- Empreinte -------------------------------------------------------------------

    [Fact]
    public void Empreinte_independante_de_la_mise_en_forme_et_de_la_version()
    {
        string original = ClassJsonReader.Read(BastionJson, Progression).Class!.Fingerprint;
        JsonObject root = Shipped();
        root["contentVersion"] = "0.2.0";
        JsonObject talent = Talent(root, 1, 0);
        var reordered = new JsonObject();
        foreach (var (key, value) in talent.ToList().AsEnumerable().Reverse()) { talent.Remove(key); reordered[key] = value; }
        DataOf(root)["trees"]![1]!["talents"]![0] = reordered;
        Assert.Equal(original, ClassJsonReader.Read(root.ToJsonString(), Progression).Class!.Fingerprint);
    }

    [Fact]
    public void Empreinte_change_avec_le_contenu()
    {
        string original = ClassJsonReader.Read(BastionJson, Progression).Class!.Fingerprint;
        Assert.NotEqual(original, ReadMutated(r => Talent(r, 0, 0)["name"] = "Garde stable II").Class!.Fingerprint);
        Assert.NotEqual(original, ReadMutated(r => Skill(r, 0)["minLevel"] = 2).Class!.Fingerprint);
    }

    [Fact]
    public void Validateur_commun_utilisable_sans_JSON()
    {
        // Chemin d'un futur chargeur Unity : il remplit un ClassDocument et appelle le même validateur.
        var document = new ClassDocument
        {
            SchemaVersion = DataFormat.ClassSchemaVersion, Kind = "class", ContentVersion = "0.1.0",
            Data = new ClassData
            {
                Id = "class.mini", Name = "Mini",
                Trees = Enumerable.Range(1, 3).Select(i => (TreeData?)new TreeData
                {
                    Id = $"tree.mini.t{i}", Name = $"T{i}",
                    Talents = new List<TalentData?> { new() { Id = $"talent.mini.t{i}.a", Name = "A", MaxRank = 1, Major = false, Placeholder = false, Prerequisites = new() } },
                }).ToList(),
                Skills = new List<SkillData?> { new() { Id = "skill.mini.coup", Name = "Coup", Kind = "active", MinLevel = 1, Placeholder = false, TalentRequirements = new() } },
            },
        };
        Assert.True(ClassValidator.Validate(document, Progression).IsValid);

        document.Data.Skills[0]!.TalentRequirements!.Add(new RequirementData { Talent = "talent.mini.t9.z", Rank = 1 });
        AssertError(ClassValidator.Validate(document, Progression), "$.data.skills[0].talentRequirements[0].talent", ValidationCode.UnknownReference);
    }
}
