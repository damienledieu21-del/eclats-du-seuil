using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Validation;
using EdS.Domain.Builds;
using EdS.Net;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.8a, progression : niveaux de 1 à 25 dans les Vallons (plafond du sujet 54), données jusqu'au niveau 30, cinq chapitres de
/// cinq niveaux, rythme rapide jusqu'au niveau 5 puis plus lent (progression en schéma 3) ; barre « 4 + ultime dès le début », 5e place au
/// niveau 10 et 6e au niveau 20 (règles 0.15.0), pont temps réel et protocole réseau 4 (six actives et l'ultime) ; une compétence vient
/// avec sa place quand une place de sa sorte est libre à son niveau.
/// </summary>
[Trait("Category", "World")]
public class V18ProgressionTests : IDisposable
{
    private const string Frappe = "skill.bastion.frappe_du_rempart", Heurt = "skill.bastion.heurt_de_bouclier", Barriere = "skill.bastion.barriere_d_acier",
        Onde = "skill.bastion.onde_de_rupture", Provocation = "skill.bastion.provocation", Contrecoup = "skill.bastion.contrecoup",
        Absolu = "skill.bastion.bastion_absolu", Roc = "skill.bastion.endurance_du_roc";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v18_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static ProgressionRules Rules => ShippedData.Rules.Progression;

    private static JsonObject ShippedProgression() =>
        JsonNode.Parse(File.ReadAllText(ProgressionJsonReader.PathIn(DataDirectory)))!.AsObject();

    private static ProgressionLoadResult Mutated(Action<JsonObject> mutate)
    {
        JsonObject root = ShippedProgression();
        mutate(root);
        return ProgressionJsonReader.Read(root.ToJsonString(), ShippedData);
    }

    private static (string Path, ValidationCode Code) Error(ProgressionLoadResult r)
    {
        Assert.False(r.IsValid);
        ValidationError e = r.Report.Errors.First();
        return (e.Path, e.Code);
    }

    // --- Niveaux, plafond, chapitres, rythme ------------------------------------------------------------------------------------

    [Fact]
    public void Niveaux_de_1_a_25_donnees_jusqu_a_30_et_cinq_chapitres()
    {
        CharacterProgression p = Progression;
        Assert.Equal((3, "0.2.0-provisoire"), (p.SchemaVersion, p.ContentVersion));
        Assert.Equal((30, 25), (p.MaxLevel, p.LevelCap));
        Assert.Equal(30, p.XpToLevel.Count);
        Assert.Equal(30, p.ForClass("class.bastion").Attributes.Count);
        Assert.Equal(new[] { 1, 6, 11, 16, 21 }, p.ChapterStartLevels);
        Assert.Equal(new[] { 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5 },
            Enumerable.Range(1, 30).Select(p.ChapterOf));
        Assert.Throws<ArgumentOutOfRangeException>(() => p.ChapterOf(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => p.ChapterOf(31));
        // Plafond : l'expérience continue de s'accumuler, le niveau s'arrête à 25 (il comptera quand le plafond montera).
        Assert.Equal(24, p.LevelFor(p.XpToLevel[24] - 1));
        Assert.Equal(25, p.LevelFor(p.XpToLevel[24]));
        Assert.Equal(25, p.LevelFor(p.XpToLevel[29] + 1_000_000));
        // Attributs jamais en baisse, jusqu'au niveau 30.
        IReadOnlyList<EdS.Domain.Stats.Attributes> a = p.ForClass("class.bastion").Attributes;
        Assert.Equal((18, 18), (a[9].Force, a[9].Vitalite));   // niveau 10 inchangé
        Assert.True(a[24].Force > a[9].Force && a[24].Vitalite > a[9].Vitalite);
    }

    [Fact]
    public void Rythme_rapide_jusqu_au_niveau_5_puis_plus_lent()
    {
        IReadOnlyList<long> xp = Progression.XpToLevel;
        long[] step = Enumerable.Range(1, xp.Count - 1).Select(i => xp[i] - xp[i - 1]).ToArray();   // step[i] : du niveau i+1 au niveau i+2
        // Jusqu'au niveau 5 : moins de 500 points, une quinzaine de loups (30 points chacun) ; chaque niveau d'après coûte plus que tout
        // le chemin jusqu'au niveau 5 à partir du 7.
        Assert.True(xp[4] < 500, xp[4].ToString());
        Assert.All(step.Take(4), s => Assert.True(s <= 140));
        Assert.True(step[4] >= 2 * step[3]);   // la cassure : le niveau 6 coûte plus du double du niveau 5
        for (int i = 1; i < step.Length; i++) Assert.True(step[i] > step[i - 1], $"niveau {i + 2} : {step[i]} après {step[i - 1]}");
        Assert.Equal(22_010, xp[24]);   // niveau 25
    }

    [Fact]
    public void Validation_du_plafond_et_des_chapitres()
    {
        Assert.Equal(("$.data.levelCap", ValidationCode.MissingField), Error(Mutated(r => r["data"]!.AsObject().Remove("levelCap"))));
        Assert.Equal(("$.data.levelCap", ValidationCode.OutOfRange), Error(Mutated(r => r["data"]!["levelCap"] = 31)));
        Assert.Equal(("$.data.levelCap", ValidationCode.OutOfRange), Error(Mutated(r => r["data"]!["levelCap"] = 0)));
        Assert.Equal(("$.data.chapterStartLevels", ValidationCode.MissingField), Error(Mutated(r => r["data"]!.AsObject().Remove("chapterStartLevels"))));
        Assert.Equal(("$.data.chapterStartLevels", ValidationCode.InvalidValue), Error(Mutated(r => r["data"]!["chapterStartLevels"] = new JsonArray())));
        Assert.Equal(("$.data.chapterStartLevels[0]", ValidationCode.InvalidValue), Error(Mutated(r => r["data"]!["chapterStartLevels"] = new JsonArray(2, 6))));
        Assert.Equal(("$.data.chapterStartLevels[2]", ValidationCode.InvalidValue), Error(Mutated(r => r["data"]!["chapterStartLevels"] = new JsonArray(1, 6, 6))));
        Assert.Equal(("$.data.chapterStartLevels[1]", ValidationCode.OutOfRange), Error(Mutated(r => r["data"]!["chapterStartLevels"] = new JsonArray(1, 26))));
        Assert.Equal(("$.data.chapterStartLevels", ValidationCode.LimitExceeded),
            Error(Mutated(r => r["data"]!["chapterStartLevels"] = new JsonArray(Enumerable.Range(1, ProgressionValidator.MaxChapters + 1).Select(i => (JsonNode?)i).ToArray()))));
        Assert.Equal(("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion), Error(Mutated(r => r["schemaVersion"] = 2)));
        // Un seul chapitre, plafond au niveau 1 : admis.
        Assert.True(Mutated(r =>
        {
            r["data"]!["levelCap"] = 1;
            r["data"]!["chapterStartLevels"] = new JsonArray(1);
        }).IsValid);
    }

    [Fact]
    public void Barre_de_l_ecran_du_personnage_jusqu_au_plafond()
    {
        // La barre d'expérience de l'écran du personnage s'arrête au plafond (25), pas au niveau 30 des données.
        string savePath = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        SaveStore.Write(savePath, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(),
            PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, Progression).WithXp(Progression.XpToLevel[24] + 500)));
        AdventureSession a = AdventureSession.Load(DataDirectory, ScenariosDirectory, savePath);
        Assert.Equal(25, a.World.Level);
        Assert.Equal((Progression.XpToLevel[24], (long?)null), a.LevelXp);
    }

    // --- Barre : places et compétences --------------------------------------------------------------------------------------------

    [Fact]
    public void Quatre_places_et_l_ultime_des_le_niveau_1()
    {
        Assert.Equal(new[] { 1, 1, 1, 1, 10, 20 }, Rules.ActiveSlotLevels);
        Assert.Equal(1, Rules.UltimateUnlockLevel);
        Assert.Equal((4, 4, 5, 5, 6, 6), (Rules.ActiveSlots(1), Rules.ActiveSlots(9), Rules.ActiveSlots(10), Rules.ActiveSlots(19), Rules.ActiveSlots(20), Rules.ActiveSlots(25)));
        Assert.True(Rules.IsUltimateUnlocked(1));
        Assert.Equal(new[] { 1, 5, 20, 30 }, Rules.PassiveSlotLevels);   // inchangées
        // Le Bastion a six actives : avec la 6e place, toutes tiennent dans la barre.
        Assert.Equal(6, ShippedData.Classes.Single(c => c.Definition.Id == "class.bastion").Definition.Skills.Count(s => s.Kind == SkillKind.Active));
    }

    [Fact]
    public void Une_place_libre_se_lit_dans_les_regles()
    {
        // Règles livrées : 4 places au niveau 1, 2 promises au départ.
        Assert.True(SkillUnlock.HasFreePlace(SkillKind.Active, 3, 2, Rules));    // Provocation
        Assert.True(SkillUnlock.HasFreePlace(SkillKind.Active, 5, 3, Rules));    // Barrière d'acier
        Assert.False(SkillUnlock.HasFreePlace(SkillKind.Active, 7, 4, Rules));   // Contrecoup : barre pleine
        Assert.True(SkillUnlock.HasFreePlace(SkillKind.Active, 10, 4, Rules));   // Onde de rupture : la 5e place s'ouvre
        Assert.True(SkillUnlock.HasFreePlace(SkillKind.Ultimate, 10, 0, Rules)); // Bastion absolu
        Assert.False(SkillUnlock.HasFreePlace(SkillKind.Ultimate, 10, 1, Rules));
        Assert.False(SkillUnlock.HasFreePlace(SkillKind.Passive, 3, 1, Rules));
        Assert.True(SkillUnlock.HasFreePlace(SkillKind.Passive, 5, 1, Rules));
        // Avec les règles d'avant V1.8 (2 places au niveau 1, 3 au 5, 4 et l'ultime au 10), la même lecture redonne exactement la réponse
        // de Damien du 30 octobre 2026 : Provocation et Contrecoup en réserve, Barrière d'acier, Onde de rupture et Bastion absolu avec leur
        // place.
        var before = new ProgressionRules { ActiveSlotLevels = new[] { 1, 1, 5, 10 }, UltimateUnlockLevel = 10 };
        Assert.False(SkillUnlock.HasFreePlace(SkillKind.Active, 3, 2, before));
        Assert.True(SkillUnlock.HasFreePlace(SkillKind.Active, 5, 2, before));
        Assert.False(SkillUnlock.HasFreePlace(SkillKind.Active, 7, 3, before));
        Assert.True(SkillUnlock.HasFreePlace(SkillKind.Active, 10, 3, before));
        Assert.True(SkillUnlock.HasFreePlace(SkillKind.Ultimate, 10, 0, before));
        Assert.False(SkillUnlock.HasFreePlace(SkillKind.Ultimate, 9, 0, before));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillUnlock.HasFreePlace(SkillKind.Active, 1, -1, Rules));
        Assert.Throws<ArgumentNullException>(() => SkillUnlock.HasFreePlace(SkillKind.Active, 1, 0, null!));
    }

    [Fact]
    public void Une_competence_de_depart_de_trop_est_refusee()
    {
        // Cinq actives au départ : la 5e place n'est ouverte qu'au niveau 10 ; le build de départ est refusé avec son chemin.
        ProgressionLoadResult r = Mutated(root =>
        {
            JsonArray start = root["data"]!["classes"]![0]!["startActiveSkills"]!.AsArray();
            foreach (string s in new[] { Provocation, Barriere, Onde }) start.Add(s);
            // Ces compétences ne sont plus gagnées plus haut (elles seraient déjà connues).
            JsonArray unlocks = root["data"]!["classes"]![0]!["skillUnlocks"]!.AsArray();
            foreach (JsonNode? u in unlocks.Where(u => new[] { Provocation, Barriere, Onde }.Contains((string)u!["skill"]!)).ToList()) unlocks.Remove(u);
        });
        Assert.Equal("$.data.classes[0].startActiveSkills", Error(r).Path);
    }

    private AdventureSession Start(long xp, IEnumerable<string>? consumed = null, SavedCharacter? hero = null)
    {
        string savePath = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        SaveStore.Write(savePath, new WorldSave("zone.vallons", 0, 0, consumed ?? Array.Empty<string>(),
            hero ?? PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, Progression).WithXp(xp)));
        AdventureSession a = AdventureSession.Load(DataDirectory, ScenariosDirectory, savePath);
        a.DrainMessages();
        a.Advance(0.05);
        return a;
    }

    [Fact]
    public void Au_niveau_20_six_actives_et_l_ultime_dans_le_combat()
    {
        // Niveau 20 : la halte a mis Provocation, la Barrière, l'Onde et Bastion absolu ; Contrecoup attend dans la réserve, la 6e place
        // est libre. Le joueur l'y met ; à la halte, le pont temps réel montre les six actives (0 à 5) et l'ultime (6).
        AdventureSession a = Start(Progression.XpToLevel[19]);
        Assert.Equal(20, a.World.Level);
        Assert.Equal(new[] { Frappe, Heurt, Provocation, Barriere, Onde }, a.World.Character.ActiveSkills);
        Assert.True(a.Skills().HasReserve(SkillKind.Active));
        Assert.Null(a.PlaceSkill(SkillKind.Active, 5, Contrecoup));
        Assert.True(a.Rest());
        a.Advance(0.05);
        PlayerState me = a.Combat.Player();
        Assert.Equal(new[] { (0, Frappe), (1, Heurt), (2, Provocation), (3, Barriere), (4, Onde), (5, Contrecoup), (RealtimeCombat.UltimateSlot, Absolu) },
            me.Slots.Select(s => (s.Slot, s.SkillId)));
        Assert.Equal(new[] { false, false, false, false, false, false, true }, me.Slots.Select(s => s.IsUltimate));
        Assert.Equal((6, 6, 6), (RealtimeCombat.UltimateSlot, RealtimeCombat.MaxActiveSlots, Protocol.MaxSlot));
        // Hors de combat, aucune compétence ne part, mais chaque emplacement est reconnu (pas « emplacement vide »).
        a.Combat.DrainFeedback();
        foreach (int slot in new[] { 4, 5, RealtimeCombat.UltimateSlot }) a.Combat.UseSlot(slot);
        Assert.DoesNotContain(a.Combat.DrainFeedback(), f => f.Detail == "emplacement vide");
        a.Combat.UseSlot(7);
        Assert.Contains(a.Combat.DrainFeedback(), f => f.Detail == "emplacement vide");
    }

    [Fact]
    public void Au_niveau_1_la_place_de_l_ultime_est_ouverte_et_vide()
    {
        AdventureSession a = Start(0);
        SkillBook book = a.Skills();
        SkillPlace ultimate = book.Place(SkillKind.Ultimate, 0)!;
        Assert.Equal((true, (string?)null), (ultimate.IsOpen, ultimate.Skill));
        Assert.Equal(new[] { true, true, true, true, false, false }, book.Places.Where(p => p.Kind == SkillKind.Active).Select(p => p.IsOpen));
        Assert.Equal(new[] { Frappe, Heurt }, a.World.Character.ActiveSkills);
        Assert.Null(a.World.Character.Ultimate);
        a.Combat.DrainFeedback();
        a.Combat.UseSlot(RealtimeCombat.UltimateSlot);
        Assert.Contains(a.Combat.DrainFeedback(), f => f.Detail == "emplacement vide");
    }

    // --- Protocole 4 ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Protocole_4_six_actives_et_l_ultime()
    {
        Assert.Equal(4, Protocol.Version);
        foreach (int slot in new[] { 0, 4, 5, 6 })
        {
            string json = $"{{\"type\":\"input\",\"guard\":false,\"parry\":false,\"slot\":{slot},\"target\":null,\"ally\":null,\"heading\":null,\"backward\":false}}";
            NetMessage? m = MessageCodec.Decode(System.Text.Encoding.UTF8.GetBytes(json), MessageDirection.ClientToServer, out string? error);
            Assert.True(error is null, error);
            Assert.Equal(slot, Assert.IsType<InputMessage>(m).Slot);
        }
        string seven = "{\"type\":\"input\",\"guard\":false,\"parry\":false,\"slot\":7,\"target\":null,\"ally\":null,\"heading\":null,\"backward\":false}";
        Assert.Null(MessageCodec.Decode(System.Text.Encoding.UTF8.GetBytes(seven), MessageDirection.ClientToServer, out string? refused));
        Assert.Contains("hors de [0, 6]", refused);
    }

    // --- Références figées ------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("entrainement_declencheurs")]
    [InlineData("entrainement_bouclier")]
    [InlineData("entrainement_equipement")]
    public void References_des_regles_0_14_0_refusees_et_corps_reproduits(string name)
    {
        // Règles 0.15.0, même moteur 0.26.0 (format 20) : les références des règles 0.14.0 restent figées, refusées au rejeu par l'identité
        // des règles ; leurs lignes d'événements sont reproduites octet par octet. Seule l'identité des règles change dans l'en-tête.
        string Frozen(string rules) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference-0.26.0-contenu-0.6.0-regles-" + rules, name + ".jsonl");
        string[] frozen = File.ReadAllLines(Frozen("0.14.0")), current = File.ReadAllLines(Frozen("0.15.0"));
        ReplayOutcome outcome = CombatLogReplayer.Replay(frozen, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Contains(outcome.Problems, p => p.Contains("0.14.0-provisoire", StringComparison.Ordinal));
        Assert.Equal(frozen.Skip(1), current.Skip(1));
        JsonElement before = JsonDocument.Parse(frozen[0]).RootElement, after = JsonDocument.Parse(current[0]).RootElement;
        Assert.Equal(("0.26.0", 20), (after.GetProperty("engineVersion").GetString(), after.GetProperty("formatVersion").GetInt32()));
        Assert.Equal(before.GetProperty("engineVersion").GetString(), after.GetProperty("engineVersion").GetString());
        Assert.Equal("0.15.0-provisoire", after.GetProperty("data").GetProperty("rules").GetProperty("contentVersion").GetString());
        Assert.Equal(before.GetProperty("scenario").GetRawText(), after.GetProperty("scenario").GetRawText());
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(current, ShippedData).Status);
    }
}
