using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Domain.Builds;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.5, écran des compétences (décisions de Damien : changer ses compétences « partout hors combat » ; la barre grandit avec le
/// niveau). La barre se change hors combat (aucun ennemi à moins de 12 m, aucun ennemi engagé qui vise le personnage), jugée par un seul
/// juge (<see cref="PlayerCharacters.CheckSkills"/> : places ouvertes, compétences apprises, validateur unique des builds, barre pas pire),
/// gardée dans la sauvegarde et prise en compte à l'entrée suivante, comme les talents. Un refus ne change rien.
/// </summary>
[Trait("Category", "World")]
public class V15SkillScreenTests : IDisposable
{
    private const string Frappe = "skill.bastion.frappe_du_rempart", Heurt = "skill.bastion.heurt_de_bouclier", Barriere = "skill.bastion.barriere_d_acier",
        Onde = "skill.bastion.onde_de_rupture", Provocation = "skill.bastion.provocation", Contrecoup = "skill.bastion.contrecoup",
        Absolu = "skill.bastion.bastion_absolu", Roc = "skill.bastion.endurance_du_roc", BouclierLourd = "skill.bastion.bouclier_lourd";

    /// <summary>Expérience cumulée des niveaux 1, 4, 5, 9 et 10 (progression livrée).</summary>
    private const long Level1 = 0, Level4 = 519, Level5 = 520, Level9 = 1360, Level10 = 1620;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v15_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static CharacterProgression Shipped => ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;
    private static ProgressionRules Rules => ShippedData.Rules.Progression;
    private static ClassDefinition Bastion => ShippedData.Classes.Single(c => c.Definition.Id == "class.bastion").Definition;

    /// <summary>Bastion à <paramref name="xp"/>, ses compétences gagnées déjà équipées (comme après une halte).</summary>
    private static SavedCharacter Hero(long xp) =>
        PlayerCharacters.Learn(PlayerCharacters.Create("bastion", "class.bastion", Shipped).WithXp(xp), Shipped, Rules, out _);

    private static BuildViolation? Check(SavedCharacter hero, string[] actives, string? ultimate, string[] passives) =>
        PlayerCharacters.CheckSkills(hero, actives, ultimate, passives, Shipped, Bastion, Rules);

    // --- Le juge (EdS.World) ------------------------------------------------------------------------------------------------------

    [Fact]
    public void Apprises_et_a_venir_selon_le_niveau()
    {
        Assert.Equal(new[] { Frappe, Heurt, Roc }, PlayerCharacters.Learned(Hero(Level1), Shipped));
        // Réponse de Damien du 30 octobre 2026 : Provocation au niveau 3, Contrecoup au niveau 7 (dans la réserve).
        Assert.Equal(new[] { Frappe, Heurt, Roc, Provocation }, PlayerCharacters.Learned(Hero(Level4), Shipped));
        Assert.Equal(new[] { Frappe, Heurt, Roc, Provocation, Barriere }, PlayerCharacters.Learned(Hero(Level5), Shipped));
        Assert.Equal(new[] { Frappe, Heurt, Roc, Provocation, Barriere, Contrecoup, Onde, Absolu }, PlayerCharacters.Learned(Hero(Level10), Shipped));
        Assert.Equal(new[] { (3, Provocation), (5, Barriere), (7, Contrecoup), (10, Onde), (10, Absolu) },
            PlayerCharacters.Upcoming(Hero(Level1), Shipped).Select(u => (u.Level, u.Skill)));
        Assert.Equal(new[] { (10, Onde), (10, Absolu) }, PlayerCharacters.Upcoming(Hero(Level9), Shipped).Select(u => (u.Level, u.Skill)));
        Assert.Empty(PlayerCharacters.Upcoming(Hero(Level10), Shipped));
    }

    [Fact]
    public void Changer_l_ordre_de_la_barre_est_admis()
    {
        SavedCharacter hero = Hero(Level10);
        Assert.Equal(new[] { Frappe, Heurt, Barriere, Onde }, hero.ActiveSkills);
        Assert.Null(Check(hero, new[] { Onde, Barriere, Heurt, Frappe }, Absolu, new[] { Roc }));
        Assert.Null(Check(hero, new[] { Frappe, Heurt, Barriere, Onde }, Absolu, new[] { Roc }));   // la même barre
        Assert.Null(Check(Hero(Level1), new[] { Heurt, Frappe }, null, new[] { Roc }));
    }

    [Theory]
    [InlineData(Level1, 3, "place active 3 fermée : elle s'ouvre au niveau 5 (niveau actuel 1).")]
    [InlineData(Level4, 3, "place active 3 fermée : elle s'ouvre au niveau 5 (niveau actuel 4).")]
    [InlineData(Level5, 4, "place active 4 fermée : elle s'ouvre au niveau 10 (niveau actuel 5).")]
    [InlineData(Level10, 5, "la barre n'a que 4 place(s) active(s).")]
    public void Une_place_active_fermee_est_refusee_avec_son_niveau(long xp, int count, string message)
    {
        SavedCharacter hero = Hero(xp);
        string[] actives = new[] { Frappe, Heurt, Barriere, Onde, Provocation }.Take(count).ToArray();
        BuildViolation why = Check(hero, actives, hero.Ultimate, hero.PassiveSkills.ToArray())!;
        Assert.Equal(BuildViolationCode.TooManySkills, why.Code);
        Assert.Equal($"activeSkills[{count - 1}]", why.Subject);
        Assert.Equal(message, why.Message);
    }

    [Fact]
    public void Ultime_et_passives_fermees_aussi()
    {
        BuildViolation ultimate = Check(Hero(Level9), new[] { Frappe, Heurt, Barriere }, Absolu, new[] { Roc })!;
        Assert.Equal(BuildViolationCode.UltimateLocked, ultimate.Code);
        Assert.Equal("place de l'ultime fermée : elle s'ouvre au niveau 10 (niveau actuel 9).", ultimate.Message);
        BuildViolation passive = Check(Hero(Level1), new[] { Frappe, Heurt }, null, new[] { Roc, BouclierLourd })!;
        Assert.Equal(BuildViolationCode.TooManySkills, passive.Code);
        Assert.Equal("passiveSkills[1]", passive.Subject);
        Assert.Equal("place passive 2 fermée : elle s'ouvre au niveau 5 (niveau actuel 1).", passive.Message);
        BuildViolation third = Check(Hero(Level10), new[] { Frappe, Heurt, Barriere, Onde }, Absolu, new[] { Roc, BouclierLourd, "skill.bastion.ferveur" })!;
        Assert.Equal("place passive 3 fermée : elle s'ouvre au niveau 20 (niveau actuel 10).", third.Message);
    }

    [Fact]
    public void Une_competence_non_apprise_est_refusee()
    {
        BuildViolation later = Check(Hero(Level4), new[] { Frappe, Barriere }, null, new[] { Roc })!;
        Assert.Equal(BuildViolationCode.SkillNotLearned, later.Code);
        Assert.Equal("activeSkills[1]", later.Subject);
        Assert.Equal("« Barrière d'acier » n'est pas encore apprise : elle s'apprend au niveau 5 (niveau actuel 4).", later.Message);
        BuildViolation never = Check(Hero(Level10), new[] { Frappe, Heurt, Barriere, Onde }, Absolu, new[] { Roc, BouclierLourd })!;
        Assert.Equal(BuildViolationCode.SkillNotLearned, never.Code);
        Assert.Equal("« Bouclier lourd » n'est pas apprise.", never.Message);
        BuildViolation passive = Check(Hero(Level5), new[] { Frappe, Heurt, Barriere }, null, new[] { Roc, BouclierLourd })!;
        Assert.Equal(("passiveSkills[1]", BuildViolationCode.SkillNotLearned), (passive.Subject, passive.Code));
        BuildViolation unknown = Check(Hero(Level1), new[] { Frappe, "skill.bastion.inconnue" }, null, new[] { Roc })!;
        Assert.Equal(BuildViolationCode.SkillNotLearned, unknown.Code);
    }

    [Fact]
    public void Le_validateur_des_builds_juge_le_reste()
    {
        SavedCharacter hero = Hero(Level10);
        Assert.Equal(BuildViolationCode.DuplicateSkill, Check(hero, new[] { Frappe, Frappe, Barriere, Onde }, Absolu, new[] { Roc })!.Code);
        Assert.Equal(BuildViolationCode.WrongSlotKind, Check(hero, new[] { Frappe, Heurt, Barriere, Roc }, Absolu, new[] { Onde })!.Code);
        Assert.Equal(BuildViolationCode.WrongSlotKind, Check(hero, new[] { Frappe, Heurt, Barriere, Absolu }, Onde, new[] { Roc })!.Code);
    }

    [Fact]
    public void Une_place_ouverte_ne_reste_pas_vide_quand_une_competence_attend()
    {
        SavedCharacter hero = Hero(Level10);
        BuildViolation active = Check(hero, new[] { Frappe, Heurt, Barriere }, Absolu, new[] { Roc })!;
        Assert.Equal(BuildViolationCode.SlotLeftEmpty, active.Code);
        Assert.Equal("activeSkills[3]", active.Subject);
        Assert.Equal("place active 4 vide alors que « Provocation » attend : une place ouverte ne se vide pas.", active.Message);
        BuildViolation ultimate = Check(hero, new[] { Frappe, Heurt, Barriere, Onde }, null, new[] { Roc })!;
        Assert.Equal(("ultimate", BuildViolationCode.SlotLeftEmpty), (ultimate.Subject, ultimate.Code));
        BuildViolation passive = Check(hero, new[] { Frappe, Heurt, Barriere, Onde }, Absolu, Array.Empty<string>())!;
        Assert.Equal(("passiveSkills[0]", BuildViolationCode.SlotLeftEmpty), (passive.Subject, passive.Code));
        // Rien n'attend : une place libre reste libre (passive 2 ouverte au niveau 5, aucune autre passive apprise).
        Assert.Null(Check(Hero(Level5), new[] { Frappe, Heurt, Barriere }, null, new[] { Roc }));
    }

    /// <summary>
    /// Relecture indépendante de la PR #71 : une barre trouée (sauvegarde d'avant, compétences de départ ou places changées dans les
    /// données) se répare pas à pas : un changement n'est refusé que s'il rend la barre pire (plus de places vides qu'une compétence apprise
    /// en attente pourrait remplir). <see cref="PlayerCharacters.Learn"/> ne remplit que les compétences gagnées en montant, pas celles de
    /// départ : sans cette règle, une barre de niveau 1 sans active refusait tout.
    /// </summary>
    [Fact]
    public void Une_barre_trouee_se_repare_pas_a_pas()
    {
        SavedCharacter empty = Hero(Level1).WithSkills(Array.Empty<string>(), null, Array.Empty<string>());
        Assert.Same(empty, PlayerCharacters.Learn(empty, Shipped, Rules, out _));   // la halte ne la répare pas
        Assert.Null(Check(empty, new[] { Frappe }, null, Array.Empty<string>()));        // une active de plus : mieux
        Assert.Null(Check(empty, Array.Empty<string>(), null, new[] { Roc }));           // la passive : mieux aussi
        Assert.Null(Check(empty, new[] { Heurt, Frappe }, null, new[] { Roc }));         // tout d'un coup
        Assert.Null(Check(empty, Array.Empty<string>(), null, Array.Empty<string>()));   // rien de changé : pas pire
        SavedCharacter one = empty.WithSkills(new[] { Frappe }, null, Array.Empty<string>());
        Assert.Null(Check(one, new[] { Heurt }, null, Array.Empty<string>()));           // remplacer : pas pire
        BuildViolation worse = Check(one, Array.Empty<string>(), null, Array.Empty<string>())!;   // retirer la seule : pire
        Assert.Equal(("activeSkills[0]", BuildViolationCode.SlotLeftEmpty), (worse.Subject, worse.Code));
        Assert.Equal("place active 1 vide alors que « Frappe du rempart » attend : une place ouverte ne se vide pas.", worse.Message);
        // Niveau 10 sans les compétences de départ (données changées) : la halte ne les remet pas ; elles se reposent une à une, et la
        // barre ne se vide pas davantage.
        SavedCharacter late = Hero(Level10).WithSkills(new[] { Barriere, Onde }, Absolu, new[] { Roc });
        Assert.Same(late, PlayerCharacters.Learn(late, Shipped, Rules, out _));
        Assert.Null(Check(late, new[] { Barriere, Onde, Frappe }, Absolu, new[] { Roc }));
        Assert.Equal(BuildViolationCode.SlotLeftEmpty, Check(late, new[] { Barriere }, Absolu, new[] { Roc })!.Code);
        // Une compétence gagnée en route que la halte placera compte déjà : la retirer de la barre montrée est pire.
        SavedCharacter route = Hero(Level10).WithSkills(new[] { Frappe, Heurt, Barriere }, null, new[] { Roc });
        Assert.Equal(BuildViolationCode.SlotLeftEmpty, Check(route, new[] { Frappe, Heurt, Barriere }, Absolu, new[] { Roc })!.Code);
        Assert.Null(Check(route, new[] { Onde, Heurt, Barriere, Frappe }, Absolu, new[] { Roc }));
    }

    [Fact]
    public void Une_competence_deja_dans_la_barre_reste_admise()
    {
        // Sauvegarde d'avant (ou données changées) : Provocation est portée sans être apprise. Elle peut rester et changer de place ; une
        // fois retirée, elle ne revient plus.
        SavedCharacter hero = Hero(Level1).WithSkills(new[] { Provocation, Frappe }, null, new[] { Roc });
        Assert.Null(Check(hero, new[] { Frappe, Provocation }, null, new[] { Roc }));
        Assert.Null(Check(hero, new[] { Frappe, Heurt }, null, new[] { Roc }));
        SavedCharacter after = hero.WithSkills(new[] { Frappe, Heurt }, null, new[] { Roc });
        Assert.Equal(BuildViolationCode.SkillNotLearned, Check(after, new[] { Frappe, Provocation }, null, new[] { Roc })!.Code);
    }

    [Fact]
    public void Entrees_impossibles()
    {
        SavedCharacter hero = Hero(Level1);
        Assert.Throws<ArgumentNullException>(() => PlayerCharacters.CheckSkills(null!, new[] { Frappe }, null, new[] { Roc }, Shipped, Bastion, Rules));
        Assert.Throws<ArgumentNullException>(() => Check(hero, null!, null, new[] { Roc }));
        Assert.Throws<ArgumentNullException>(() => Check(hero, new[] { Frappe }, null, null!));
        Assert.Throws<ArgumentException>(() => Check(hero, new[] { Frappe, null! }, null, new[] { Roc }));
        Assert.Throws<ArgumentException>(() => Check(hero, new[] { Frappe, Heurt }, null, new string[] { null! }));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerCharacters.ClosedPlace(SkillKind.Active, -1, 1, Rules));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerCharacters.ClosedPlace(SkillKind.Ultimate, 1, 10, Rules));
        Assert.Null(PlayerCharacters.ClosedPlace(SkillKind.Active, 1, 1, Rules));
        Assert.Null(PlayerCharacters.ClosedPlace(SkillKind.Ultimate, 0, 10, Rules));
    }

    /// <summary>
    /// Propriété, sur toutes les barres d'un petit univers (actives de 0 à 4 parmi cinq, ultime ou non, passives de 0 à 2) et à plusieurs
    /// niveaux : le juge admet exactement les barres qui remplissent les places ouvertes de compétences apprises distinctes (toutes, s'il y
    /// en a moins que de places ; un oracle écrit à part), et une barre admise ne change pas à la halte suivante
    /// (<see cref="PlayerCharacters.Learn"/> n'y ajoute rien).
    /// </summary>
    [Fact]
    public void Propriete_le_juge_admet_exactement_les_barres_pleines_d_apprises()
    {
        string[] pool = { Frappe, Heurt, Barriere, Onde, Provocation };
        var activeBars = new List<string[]> { Array.Empty<string>() };
        for (int length = 1; length <= 4; length++)
            activeBars.AddRange(activeBars.Where(b => b.Length == length - 1).SelectMany(b => pool.Select(s => b.Append(s).ToArray())).ToList());
        string[][] passiveBars = { Array.Empty<string>(), new[] { Roc }, new[] { BouclierLourd }, new[] { Roc, BouclierLourd }, new[] { Roc, Roc } };
        int admitted = 0, checkedBars = 0;
        foreach (long xp in new[] { Level1, Level5, Level10 })
        {
            SavedCharacter hero = Hero(xp);
            int level = Shipped.LevelFor(xp);
            var learned = PlayerCharacters.Learned(hero, Shipped);
            string[] learnedActives = learned.Where(s => Bastion.TryGetSkill(s, out SkillDefinition d) && d.Kind == SkillKind.Active).OrderBy(s => s).ToArray();
            foreach (string[] actives in activeBars)
                foreach (string? ultimate in new[] { null, Absolu })
                    foreach (string[] passives in passiveBars)
                    {
                        checkedBars++;
                        bool expected = actives.Distinct().Count() == actives.Length && actives.All(learnedActives.Contains)
                            && actives.Length == Math.Min(Rules.ActiveSlots(level), learnedActives.Length)
                            && ultimate == (level >= 10 ? Absolu : null)
                            && passives.SequenceEqual(new[] { Roc });
                        BuildViolation? why = Check(hero, actives, ultimate, passives);
                        Assert.True(expected == (why is null), $"niveau {level}, [{string.Join(", ", actives)}], {ultimate}, [{string.Join(", ", passives)}] : {why}");
                        if (why != null) continue;
                        admitted++;
                        SavedCharacter set = hero.WithSkills(actives, ultimate, passives);
                        Assert.Same(set, PlayerCharacters.Learn(set, Shipped, Rules, out IReadOnlyList<SkillUnlock> gained));
                        Assert.Empty(gained);
                    }
        }
        Assert.True(checkedBars > 15_000, checkedBars.ToString());
        // Niveau 1 : 2 ordres de Frappe et Heurt ; niveau 5 : 3 places parmi 4 apprises (Provocation comprise), 4 × 3 × 2 = 24 ; niveau 10 :
        // 4 places parmi les 5 apprises de l'univers, 5 × 4 × 3 × 2 = 120.
        Assert.Equal(2 + 24 + 120, admitted);
    }

    // --- La barre et l'écran (EdS.Runtime) ---------------------------------------------------------------------------------------

    [Fact]
    public void Placer_echange_deplace_ou_remplace()
    {
        var bar = new SkillBar(new[] { Frappe, Heurt, Barriere }, null, new[] { Roc });
        Assert.Equal(new[] { Barriere, Heurt, Frappe }, bar.Place(SkillKind.Active, 0, Barriere).Actives);   // échange
        Assert.Equal(new[] { Heurt, Barriere, Frappe }, bar.Place(SkillKind.Active, 3, Frappe).Actives);     // vers une place vide : à la fin
        Assert.Equal(new[] { Frappe, Onde, Barriere }, bar.Place(SkillKind.Active, 1, Onde).Actives);        // de la réserve : remplace
        Assert.Equal(new[] { Frappe, Heurt, Barriere, Onde }, bar.Place(SkillKind.Active, 3, Onde).Actives); // de la réserve, place vide
        Assert.True(bar.Place(SkillKind.Active, 2, Barriere).SameAs(bar));                                    // sur sa propre place
        SkillBar crossed = bar.Place(SkillKind.Passive, 0, Frappe);                                          // une active vers une place passive
        Assert.Equal(new[] { Heurt, Barriere }, crossed.Actives);
        Assert.Equal(new[] { Frappe }, crossed.Passives);
        Assert.Equal(Absolu, bar.Place(SkillKind.Ultimate, 0, Absolu).Ultimate);
        Assert.Equal(new[] { Frappe, Barriere }, bar.Remove(SkillKind.Active, 1).Actives);
        Assert.Same(bar, bar.Remove(SkillKind.Active, 3));
        Assert.Null(bar.Place(SkillKind.Ultimate, 0, Absolu).Remove(SkillKind.Ultimate, 0).Ultimate);
        Assert.Throws<ArgumentOutOfRangeException>(() => bar.Place(SkillKind.Active, -1, Frappe));
        Assert.Throws<ArgumentOutOfRangeException>(() => bar.Place(SkillKind.Ultimate, 1, Absolu));
        Assert.Throws<ArgumentOutOfRangeException>(() => bar.Remove(SkillKind.Passive, -2));
        Assert.Throws<ArgumentNullException>(() => bar.Place(SkillKind.Active, 0, null!));
    }

    [Fact]
    public void L_ecran_montre_places_apprises_et_a_venir()
    {
        SkillBook book = SkillBook.For(Hero(Level5), Shipped, Bastion, Rules, null, id => "description de " + id);
        Assert.Equal(5, book.Level);
        Assert.True(book.CanChange);
        Assert.Null(book.Locked);
        // La 2e place passive est ouverte, mais aucune passive n'attend : l'écran dit « rien à placer » (relecture de la PR #71).
        Assert.False(book.HasReserve(SkillKind.Passive));
        Assert.True(book.HasReserve(SkillKind.Active));   // Provocation, apprise au niveau 3, attend dans la réserve
        Assert.False(SkillBook.For(Hero(Level5), Shipped, Bastion, Rules, "motif").CanChange);
        Assert.Equal(new[] { ("1", 1, true, Frappe), ("2", 1, true, Heurt), ("3", 5, true, Barriere), ("4", 10, false, (string?)null) },
            book.Places.Where(p => p.Kind == SkillKind.Active).Select(p => (p.Key, p.OpenLevel, p.IsOpen, p.Skill)));
        SkillPlace ultimate = book.Place(SkillKind.Ultimate, 0)!;
        Assert.Equal(("R", 10, false, (string?)null), (ultimate.Key, ultimate.OpenLevel, ultimate.IsOpen, ultimate.Skill));
        Assert.Equal(new[] { (1, true, Roc), (5, true, (string?)null), (20, false, null), (30, false, null) },
            book.Places.Where(p => p.Kind == SkillKind.Passive).Select(p => (p.OpenLevel, p.IsOpen, p.Skill)));
        Assert.Equal(new[] { Frappe, Heurt, Roc, Provocation, Barriere }, book.Learned.Select(c => c.Id));
        Assert.All(book.Learned, c => Assert.True(c.Learned && (c.Place != null) == (c.Id != Provocation)));
        Assert.Equal(new[] { (Contrecoup, 7), (Onde, 10), (Absolu, 10) }, book.Upcoming.Select(c => (c.Id, c.Level)));
        Assert.All(book.Upcoming, c => Assert.True(!c.Learned && c.Place == null));
        SkillCard frappe = book.Card(Frappe)!;
        Assert.Equal(("Frappe du rempart", 10, 6.0, (double?)2.5, "description de " + Frappe), (frappe.Name, frappe.EnduranceCost, frappe.CooldownSeconds, frappe.RangeMeters, frappe.Description));
        Assert.Equal(5, book.Card(Barriere)!.Level);
        Assert.Equal(3, book.Card(Provocation)!.Level);
        Assert.Null(book.Card(BouclierLourd));
        Assert.Null(book.Place(SkillKind.Active, 4));
    }

    [Fact]
    public void Chaque_competence_du_bastion_a_sa_description()
    {
        TextCatalog texts = StoryJsonReader.ReadTextsFile(StoryJsonReader.TextsPathIn(DataDirectory)).Texts!;
        foreach (SkillDefinition skill in Bastion.Skills)
            Assert.True(texts.Has(AdventureSession.SkillTextKey(skill.Id)), skill.Id);
        foreach (string key in new[] { "text.ui.tab.skills", "text.ui.skills.saved", "text.ui.skills.in_combat" }) Assert.True(texts.Has(key), key);
    }

    // --- Dans l'aventure ---------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Partie à <paramref name="xp"/> ; <paramref name="wolf"/> : le loup 1 posé à cette place (mètres), avec <paramref name="aggro"/> pour
    /// distance d'engagement s'il est donné.
    /// </summary>
    private AdventureSession Start(long xp, (int X, int Z)? wolf = null, int? aggro = null, SavedCharacter? character = null)
    {
        string scenarios = Path.Combine(AppContext.BaseDirectory, "scenarios");
        if (wolf is (int x, int z))
        {
            scenarios = Path.Combine(_folder, "scenarios");
            Directory.CreateDirectory(Path.Combine(scenarios, "zones"));
            JsonObject vallons = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
            foreach (JsonNode? c in vallons["data"]!["combatants"]!.AsArray())
                if ((string)c!["id"]! == "loup1")
                {
                    c["position"] = new JsonArray(x, z);
                    if (aggro is int range) c["behavior"]!["aggroRange"] = range;
                }
            File.WriteAllText(Path.Combine(scenarios, "zones", "vallons.json"), vallons.ToJsonString());
            CopyMissingZoneScenarios(scenarios);
        }
        string savePath = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        SaveStore.Write(savePath, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(),
            character ?? PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, Shipped).WithXp(xp)));
        AdventureSession a = AdventureSession.Load(DataDirectory, scenarios, savePath);
        a.DrainMessages();
        a.Advance(0.05);
        return a;
    }

    private static IReadOnlyList<string> Engaged(AdventureSession a) => a.World.Zone.Engine.View(AdventureSession.PlayerId).Character!.Build.ActiveSkills;

    [Fact]
    public void Les_competences_se_changent_dans_l_aventure_et_prennent_effet_a_la_halte()
    {
        AdventureSession a = Start(Level10);
        Assert.True(a.CanChangeSkills);
        Assert.Equal(new[] { Frappe, Heurt, Barriere, Onde }, Engaged(a));
        Assert.Null(a.PlaceSkill(SkillKind.Active, 0, Onde));
        Assert.Contains("Compétences gardées : effet à ta prochaine halte.", a.DrainMessages());
        Assert.Equal(new[] { Onde, Heurt, Barriere, Frappe }, a.World.Character.ActiveSkills);
        Assert.Equal(new[] { Onde, Heurt, Barriere, Frappe }, a.Skills().Places.Where(p => p.Kind == SkillKind.Active).Select(p => p.Skill));
        // Le combat en cours garde la barre d'entrée ; la halte prend la nouvelle, sans rien « apprendre » de nouveau.
        Assert.Equal(new[] { Frappe, Heurt, Barriere, Onde }, Engaged(a));
        Assert.True(a.Rest());
        Assert.Equal(new[] { Onde, Heurt, Barriere, Frappe }, Engaged(a));
        Assert.Empty(a.World.Learned);
        // Gardée dans la sauvegarde : une partie relue la reprend.
        AdventureSession again = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), a.SavePath);
        Assert.Equal(new[] { Onde, Heurt, Barriere, Frappe }, again.World.Character.ActiveSkills);
        Assert.Equal(new[] { Onde, Heurt, Barriere, Frappe }, Engaged(again));
        Assert.Equal(Absolu, again.World.Character.Ultimate);
    }

    [Fact]
    public void Un_refus_ne_change_rien()
    {
        AdventureSession a = Start(Level5);
        a.Save();   // Barrière d'acier, gagnée au niveau 5, vient d'être équipée à l'entrée : sauvegardée d'abord
        SavedCharacter before = a.World.Character;
        string saved = File.ReadAllText(a.SavePath);
        Assert.Equal("place active 4 fermée : elle s'ouvre au niveau 10 (niveau actuel 5).", a.PlaceSkill(SkillKind.Active, 3, Frappe));
        Assert.Equal("place de l'ultime fermée : elle s'ouvre au niveau 10 (niveau actuel 5).", a.PlaceSkill(SkillKind.Ultimate, 0, Absolu));
        Assert.Equal("« Contrecoup » n'est pas encore apprise : elle s'apprend au niveau 7 (niveau actuel 5).", a.PlaceSkill(SkillKind.Active, 0, Contrecoup));
        Assert.Equal("« Onde de rupture » n'est pas encore apprise : elle s'apprend au niveau 10 (niveau actuel 5).", a.PlaceSkill(SkillKind.Active, 1, Onde));
        Assert.StartsWith("« Frappe du rempart » est une compétence active", a.PlaceSkill(SkillKind.Passive, 1, Frappe));
        Assert.Equal("place active 3 vide alors que « Provocation » attend : une place ouverte ne se vide pas.", a.RemoveSkill(SkillKind.Active, 2));
        Assert.Equal(BuildViolationCode.DuplicateSkill, a.World.SetSkills(new[] { Frappe, Frappe, Barriere }, null, new[] { Roc })!.Code);
        Assert.Same(before, a.World.Character);
        Assert.DoesNotContain("Compétences gardées : effet à ta prochaine halte.", a.DrainMessages());
        a.Advance(AdventureSession.AutosaveSeconds + 1);   // rien de changé : pas de sauvegarde à faire
        Assert.Equal(saved, File.ReadAllText(a.SavePath));
        Assert.Throws<ArgumentNullException>(() => a.PlaceSkill(SkillKind.Active, 0, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => a.PlaceSkill(SkillKind.Active, -1, Frappe));
        Assert.Throws<ArgumentOutOfRangeException>(() => a.RemoveSkill(SkillKind.Ultimate, 2));
        Assert.Throws<ArgumentNullException>(() => a.SetSkills(null!, null, new[] { Roc }));
        Assert.Throws<ArgumentNullException>(() => a.World.SetSkills(new[] { Frappe }, null, null!));
        // Une place passive libre sans passive en attente, et rien à changer : admis sans bruit.
        Assert.Null(a.PlaceSkill(SkillKind.Passive, 0, Roc));
        Assert.Same(before, a.World.Character);
    }

    [Fact]
    public void Pas_pres_d_un_ennemi()
    {
        AdventureSession a = Start(Level10, wolf: (-15, -12));   // à 3 m du point sûr d'entrée, engagé dès le premier tick (la règle des 12 m seule : La_regle_des_12_m_seule)
        Assert.False(a.CanChangeSkills);
        Assert.False(a.Skills().CanChange);
        SavedCharacter before = a.World.Character;
        Assert.Equal("Pas en plein combat : éloigne-toi des ennemis pour changer tes compétences.", a.PlaceSkill(SkillKind.Active, 0, Onde));
        Assert.Contains("Pas en plein combat : éloigne-toi des ennemis pour changer tes compétences.", a.DrainMessages());
        Assert.Equal("Pas en plein combat : éloigne-toi des ennemis pour changer tes compétences.",
            a.SetSkills(new[] { Onde, Heurt, Barriere, Frappe }, Absolu, new[] { Roc }));
        Assert.Same(before, a.World.Character);
    }

    [Fact]
    public void Pas_avec_un_ennemi_engage_meme_loin()
    {
        // Un loup à 16 m, qui s'engage de 30 m : plus loin que la règle des 12 m (les talents se changent encore), mais il vise le Bastion.
        AdventureSession a = Start(Level10, wolf: (-18, 4), aggro: 30);
        var wolf = a.World.Zone.Engine.View(a.World.CurrentCreature("loup1"));
        Assert.True(wolf.Engaged);
        Assert.Equal(AdventureSession.PlayerId, wolf.CurrentTarget);
        Assert.True(a.CanChangeTalents);
        Assert.False(a.CanChangeSkills);
        SavedCharacter before = a.World.Character;
        Assert.Equal("Pas en plein combat : éloigne-toi des ennemis pour changer tes compétences.", a.PlaceSkill(SkillKind.Active, 0, Onde));
        Assert.Same(before, a.World.Character);
    }

    [Fact]
    public void Pas_pendant_un_dialogue()
    {
        // CanChangeSkills est écrit à part de CanChangeTalents (les talents iront chez le maître de classe, cadrage Q21) : il garde
        // pourtant le refus d'un dialogue ouvert.
        AdventureSession a = Start(Level10);
        for (int i = 0; i < 2000 && a.NpcInReach?.Id != "npc.odile"; i++)
        {
            EdS.Domain.Space.GroundVector p = a.World.Zone.Engine.View(AdventureSession.PlayerId).Position!.Value;
            a.Combat.SetMove(-8 - p.X.Raw / 1e4, 3 - p.Z.Raw / 1e4);
            a.Advance(0.05);
        }
        a.Combat.SetMove(0, 0);
        Assert.True(a.Interact());
        Assert.NotNull(a.Dialogue);
        Assert.False(a.CanChangeSkills);
        Assert.Equal("Finis d'abord la conversation pour changer tes compétences.", a.SkillsLocked);
        Assert.Equal(a.SkillsLocked, a.Skills().Locked);
        SavedCharacter before = a.World.Character;
        Assert.Equal("Finis d'abord la conversation pour changer tes compétences.", a.PlaceSkill(SkillKind.Active, 0, Onde));
        Assert.Same(before, a.World.Character);
        a.CloseDialogue();
        Assert.Null(a.Dialogue);
        Assert.True(a.CanChangeSkills);
        Assert.Null(a.SkillsLocked);
        Assert.Null(a.PlaceSkill(SkillKind.Active, 0, Onde));
    }

    /// <summary>
    /// Relecture indépendante de la PR #71 : la règle des 12 m seule. Un loup à 8 m qui ne s'engage qu'à 1 m (donc pas engagé) refuse le
    /// changement ; à 14 m, il est permis.
    /// </summary>
    [Theory]
    [InlineData(-10, false)]   // 8 m du point sûr d'entrée (-18, -12)
    [InlineData(-4, true)]     // 14 m
    public void La_regle_des_12_m_seule(int x, bool allowed)
    {
        AdventureSession a = Start(Level10, wolf: (x, -12), aggro: 1);
        var wolf = a.World.Zone.Engine.View(a.World.CurrentCreature("loup1"));
        Assert.False(wolf.Engaged);
        Assert.Equal(allowed, a.CanChangeSkills);
        SavedCharacter before = a.World.Character;
        string? why = a.PlaceSkill(SkillKind.Active, 0, Onde);
        if (allowed)
        {
            Assert.Null(why);
            Assert.Equal(new[] { Onde, Heurt, Barriere, Frappe }, a.World.Character.ActiveSkills);
        }
        else
        {
            Assert.Equal("Pas en plein combat : éloigne-toi des ennemis pour changer tes compétences.", why);
            Assert.Same(before, a.World.Character);
        }
    }

    [Fact]
    public void Pas_une_fois_tombe()
    {
        // Le sanglier rendu mortel sur le chemin, comme Lot128AdventureTests.Defaite_puis_reprise_au_dernier_point_sur.
        string data = Path.Combine(_folder, "data", "v0"), scenarios = Path.Combine(_folder, "scenarios");
        CopyTree(DataDirectory, data);
        CopyTree(Path.Combine(AppContext.BaseDirectory, "scenarios"), scenarios);
        string zone = Path.Combine(scenarios, "zones", "vallons.json");
        JsonObject root = JsonNode.Parse(File.ReadAllText(zone))!.AsObject();
        JsonObject boar = root["data"]!["combatants"]!.AsArray().Single(c => (string)c!["id"]! == "sanglier")!.AsObject();
        boar["position"] = new JsonArray(-14, -12);
        foreach (JsonNode? attack in boar["attacks"]!.AsArray()) attack!["rawDamage"] = 100_000;
        File.WriteAllText(zone, root.ToJsonString());
        string savePath = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        SaveStore.Write(savePath, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(),
            PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, Shipped).WithXp(Level10)));
        AdventureSession a = AdventureSession.Load(data, scenarios, savePath);
        for (int i = 0; i < 1200 && !a.World.IsDefeated; i++) a.Advance(0.05);
        Assert.True(a.World.IsDefeated);
        a.DrainMessages();
        SavedCharacter before = a.World.Character;
        Assert.Equal("Tu es tombé : reprends au dernier point sûr pour changer tes compétences.", a.PlaceSkill(SkillKind.Active, 0, Onde));
        Assert.Contains("Tu es tombé : reprends au dernier point sûr pour changer tes compétences.", a.DrainMessages());
        Assert.Same(before, a.World.Character);
    }

    private static void CopyTree(string from, string to)
    {
        foreach (string dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(dir.Replace(from, to));
        Directory.CreateDirectory(to);
        foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories)) File.Copy(file, file.Replace(from, to));
    }

    /// <summary>
    /// Relecture indépendante de la PR #71 : une sauvegarde de niveau 1 sans active (données de départ changées) se répare à l'écran,
    /// une compétence à la fois ; une fois pleine, elle ne se vide plus.
    /// </summary>
    [Fact]
    public void Une_sauvegarde_a_barre_vide_se_repare_a_l_ecran()
    {
        SavedCharacter hero = PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, Shipped)
            .WithSkills(Array.Empty<string>(), null, new[] { Roc });
        AdventureSession a = Start(Level1, character: hero);
        Assert.Empty(a.World.Character.ActiveSkills);
        Assert.Null(a.PlaceSkill(SkillKind.Active, 0, Frappe));
        Assert.Equal(new[] { Frappe }, a.World.Character.ActiveSkills);
        Assert.Null(a.PlaceSkill(SkillKind.Active, 1, Heurt));
        Assert.Equal(new[] { Frappe, Heurt }, a.World.Character.ActiveSkills);
        Assert.Equal("place active 2 vide alors que « Heurt de bouclier » attend : une place ouverte ne se vide pas.", a.RemoveSkill(SkillKind.Active, 1));
        Assert.True(a.Rest());
        Assert.Equal(new[] { Frappe, Heurt }, Engaged(a));
    }

    /// <summary>
    /// Relecture indépendante de la PR #71 : une action sans effet n'écrit rien et ne dit rien ; après un niveau gagné en route, la halte
    /// annonce donc toujours la compétence et sa touche (12.9b).
    /// </summary>
    [Fact]
    public void Une_action_sans_effet_garde_l_annonce_de_la_halte()
    {
        AdventureSession a = Start(Level4);
        ((INarrativeWorld)a.World).GiveXp(1);   // niveau 5 en route
        SavedCharacter before = a.World.Character;
        Assert.Equal(new[] { Frappe, Heurt }, before.ActiveSkills);
        a.DrainMessages();
        Assert.Null(a.PlaceSkill(SkillKind.Active, 2, Barriere));   // sur sa propre place (barre de la prochaine halte)
        Assert.Equal("la barre n'a que 4 place(s) active(s).", a.PlaceSkill(SkillKind.Active, 7, Barriere));   // refus : rien d'écrit
        Assert.Null(a.SetSkills(new[] { Frappe, Heurt, Barriere }, null, new[] { Roc }));   // la même barre
        Assert.Null(a.RemoveSkill(SkillKind.Passive, 1));   // place vide : rien à retirer
        Assert.Same(before, a.World.Character);
        Assert.Empty(a.DrainMessages());
        Assert.True(a.Rest());
        Assert.Equal(new[] { Barriere }, a.World.Learned.Select(u => u.Skill));
        Assert.Contains(a.DrainMessages(), m => m.StartsWith("Nouvelle compétence : Barrière d'acier (touche 3", StringComparison.Ordinal));
    }

    [Fact]
    public void Un_niveau_gagne_en_route_ouvre_sa_place_tout_de_suite_dans_l_ecran()
    {
        AdventureSession a = Start(Level4);
        Assert.Equal(new[] { Frappe, Heurt }, a.World.Character.ActiveSkills);
        ((INarrativeWorld)a.World).GiveXp(1);   // niveau 5 en route
        Assert.Equal(5, a.World.Level);
        // L'écran montre déjà la barre de la prochaine halte : Barrière d'acier dans la place 3, qui vient de s'ouvrir.
        SkillBook book = a.Skills();
        Assert.Equal(Barriere, book.Place(SkillKind.Active, 2)!.Skill);
        Assert.Null(a.PlaceSkill(SkillKind.Active, 0, Barriere));
        Assert.Equal(new[] { Barriere, Heurt, Frappe }, a.World.Character.ActiveSkills);
        Assert.True(a.Rest());
        Assert.Equal(new[] { Barriere, Heurt, Frappe }, Engaged(a));
        Assert.Empty(a.World.Learned);   // déjà placée : rien à annoncer ni à déplacer à la halte
    }

    [Fact]
    public void Sans_l_ecran_la_halte_equipe_toujours_les_competences_gagnees()
    {
        AdventureSession a = Start(Level4);
        ((INarrativeWorld)a.World).GiveXp(1);
        Assert.True(a.Rest());
        Assert.Equal(new[] { Frappe, Heurt, Barriere }, Engaged(a));
        Assert.Equal(new[] { Barriere }, a.World.Learned.Select(u => u.Skill));
    }
}
