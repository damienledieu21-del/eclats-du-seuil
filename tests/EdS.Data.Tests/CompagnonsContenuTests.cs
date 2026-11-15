using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EdS.Data.Allies;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Story;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Compagnons, contenu (décisions de Damien du 14 novembre 2026, dossier « Grand Solitaire et compagnons », p. 34 à 120) : les 14 compagnons
/// du dossier dans <c>data/v0/world/companions.json</c> (4 héros de l'histoire, 6 mercenaires de la Guilde à trait tiré au hasard, 4 bêtes
/// dont Cendre en trois stades), les dons des bêtes retenus, les quatre traits, et la quête de lien au feu de camp qui ouvre la deuxième
/// place (fin du chapitre 1, niveau doux 12).
/// </summary>
[Trait("Category", "Companion")]
[Trait("Category", "Data")]
public class CompagnonsContenuTests
{
    private static readonly CompanionCatalog Catalog = Load();

    private static CompanionCatalog Load()
    {
        CompanionLoadResult result = CompanionJsonReader.ReadFile(CompanionJsonReader.PathIn(DataDirectory), Book(Zone()));
        Assert.True(result.IsValid, result.Report.ToString());
        return result.Catalog!;
    }

    private static CompanionSheet Sheet(string id) => Catalog.TryGet(id, out CompanionSheet s) ? s : throw new ArgumentException(id);

    [Fact]
    public void Les_quatorze_compagnons_du_dossier()
    {
        Assert.Equal(14, Catalog.Companions.Count);
        Assert.Equal(new[] { "companion.nyx", "companion.albe", "companion.renart", "companion.gaspard" },
            Catalog.Companions.Where(c => c.Kind == CompanionKind.Hero).Select(c => c.Id));
        Assert.Equal(new[] { "companion.aveline", "companion.hadrien", "companion.ysee", "companion.nahla", "companion.silvere", "companion.odran" },
            Catalog.Companions.Where(c => c.Kind == CompanionKind.Mercenary).Select(c => c.Id));
        Assert.Equal(new[] { "companion.cendre", "companion.brisecanne", "companion.virevent", "companion.ronce" },
            Catalog.Companions.Where(c => c.Kind == CompanionKind.Beast).Select(c => c.Id));
        // Mercenaires : à la Guilde, selon le rang, contre une paie, trait tiré au hasard ; les autres, ni l'un ni l'autre.
        Assert.All(Catalog.Companions, c => Assert.Equal(c.Kind == CompanionKind.Mercenary, c.RandomTrait && c.Recruit.Source == "guild" && c.Recruit.Price > 0));
        Assert.Equal(new[] { "courageux", "peureux", "gourmand", "chanceux" }, Catalog.Traits.Select(t => t.Id));
        // Dons des bêtes retenus par Damien : le chien trouve des trésors, le faucon révèle la carte, l'ours porte des sacoches.
        Assert.Equal(("treasure", "map", "saddlebags"), (Sheet("companion.brisecanne").Gift.Effect, Sheet("companion.virevent").Gift.Effect, Sheet("companion.ronce").Gift.Effect));
        // Cendre en trois stades ; pas d'attaque à deux au stade du louveteau.
        CompanionSheet cendre = Sheet("companion.cendre");
        Assert.Equal(new[] { "louveteau", "jeune_loup", "loup_dresse" }, cendre.Stages.Select(s => s.Id));
        Assert.Null(cendre.Stages[0].Duo);
        Assert.All(Catalog.Companions.Where(c => c.Id != "companion.cendre"), c => Assert.NotNull(c.Stages.Single().Duo));
        // Les attaques à deux des héros portent les noms du dossier.
        Assert.Equal(new[] { "Contre-jour", "Aube partagée", "Marché de dupes", "La vieille leçon" },
            Catalog.Companions.Where(c => c.Kind == CompanionKind.Hero).Select(c => c.Stages[0].Duo!.Name));
    }

    [Fact]
    public void Places_et_quete_de_lien_au_feu_de_camp()
    {
        Assert.Equal((1, 2, 12, 1), (Catalog.Slots.Initial, Catalog.Slots.Max, Catalog.Slots.SecondSlotLevel, Catalog.Slots.WorldBossInCombat));
        StoryBook story = Book(Zone());
        Assert.True(story.HasQuest(Catalog.Slots.SecondSlotQuest));
        string quest = File.ReadAllText(Path.Combine(DataDirectory, "world", "story.json"));
        JsonElement q = JsonDocument.Parse(quest).RootElement.GetProperty("data").GetProperty("quests").EnumerateArray()
            .Single(e => e.GetProperty("id").GetString() == Catalog.Slots.SecondSlotQuest);
        // Ouverte à la fin du chapitre 1 (chef des bandits vaincu), avec au moins un compagnon recruté (fait appris par le cœur) ; aucun fait
        // posé que personne ne lirait : la deuxième place se lit dans la quête finie (CompanionRules.BondQuestDone).
        Assert.Equal(new[] { "fact.chef_des_bandits.vaincu", StoryValidator.CompanionRecruitedFact },
            q.GetProperty("requires").EnumerateArray().Select(e => e.GetProperty("id").GetString()));
        Assert.DoesNotContain(q.GetProperty("effects").EnumerateArray(), e => e.GetProperty("do").GetString() == "setFact");
        Assert.True(story.CanLearn(StoryValidator.CompanionRecruitedFact));
    }

    [Fact]
    public void L_offre_de_Gaspard_n_est_jamais_masquee_par_un_accueil_durable()
    {
        // Revue Codex de la PR #175 : la première entrée satisfaite gagne ; l'offre passe avant l'accueil de nuit et l'accueil froid (cicatrices
        // du Seuil), et chaque accueil qui peut durer propose la reprise, comme celle de la piste des rôdeurs.
        StoryBook story = Book(Zone());
        Dialogue gaspard = story.Dialogues.Single(d => d.Id == "dialogue.gaspard");
        var order = gaspard.Entries.Select(e => e.Node).ToList();
        int offer = order.IndexOf("compagnons_lien");
        Assert.All(new[] { "ch1_nuit", "v261_froid", "v292_brunot", "v292_brunot_retour", "garde" }, n => Assert.True(order.IndexOf(n) > offer, n));
        Assert.All(gaspard.Nodes.Where(n => n.Id != "compagnons_lien" && n.Choices.Any(c => c.Text == "text.v27.gaspard.encore")),
            n => Assert.Contains(n.Choices, c => c.Text == "text.compagnons.lien_au_feu.reprendre" && c.Next == "compagnons_lien"));
    }

    [Fact]
    public void La_quete_de_lien_doit_exister_dans_l_histoire()
    {
        StoryBook story = Book(Zone());
        string json = File.ReadAllText(CompanionJsonReader.PathIn(DataDirectory));
        Assert.True(CompanionJsonReader.Read(json, story).IsValid);
        CompanionLoadResult missing = CompanionJsonReader.Read(json.Replace("\"quest.compagnons.lien_au_feu\"", "\"quest.compagnons.inconnue\"", StringComparison.Ordinal), story);
        Assert.False(missing.IsValid);
        Assert.Contains(missing.Report.Errors, e => e.Path == "$.data.slots.secondSlotQuest" && e.Code == EdS.Data.Validation.ValidationCode.UnknownReference);
        // Sans histoire, la référence n'est pas vérifiée (catalogue lu seul, par exemple par les outils).
        Assert.True(CompanionJsonReader.Read(json.Replace("\"quest.compagnons.lien_au_feu\"", "\"quest.compagnons.inconnue\"", StringComparison.Ordinal)).IsValid);
    }

    [Fact]
    public void La_quete_de_lien_finie_ouvre_la_deuxieme_place_au_niveau_12()
    {
        SavedCompanions roster = CompanionRules.Recruit(Catalog, SavedCompanions.Empty, "companion.nyx", new RecruitContext(true, null, 0, CompagnonsProgressionTests.Rng(1))).Roster;
        var done = new SavedStory(quests: new System.Collections.Generic.Dictionary<string, QuestProgress>
            { ["quest.compagnons.lien_au_feu"] = new QuestProgress(QuestProgress.Done) });
        Assert.Equal("bondQuestNotDone", CompanionRules.OpenSecondSlot(Catalog, roster, 20, SavedStory.Empty).Reason);
        Assert.Equal("levelTooLow", CompanionRules.OpenSecondSlot(Catalog, roster, 11, done).Reason);
        SavedCompanions two = CompanionRules.OpenSecondSlot(Catalog, roster, 12, done).Roster;
        Assert.Equal(2, two.Slots);
        Assert.Empty(CompanionRules.Check(Catalog, two, done, 12));
        // Recrues de la Guilde : chaque mercenaire reçoit un des quatre traits.
        foreach (CompanionSheet m in Catalog.Companions.Where(c => c.Kind == CompanionKind.Mercenary))
            Assert.NotNull(Catalog.Trait(CompanionRules.Recruit(Catalog, two, m.Id, new RecruitContext(false, "s", 1_000_000, CompagnonsProgressionTests.Rng(9))).Roster.Find(m.Id)!.Trait));
    }

    [Fact]
    public void La_quete_de_lien_de_bout_en_bout_dans_la_partie()
    {
        const string Quest = "quest.compagnons.lien_au_feu";
        SavedCharacter hero = Hero().WithXp(Progression.XpToLevel[11]).WithStory(new SavedStory(new[] { "fact.chef_des_bandits.vaincu" }));

        // Chef des bandits vaincu, mais aucun compagnon recruté : la quête n'est pas ouverte, Gaspard ne la propose pas.
        WorldSession alone = Play(Zone(27, -12), hero);
        Assert.False(alone.Narrative!.IsOpen(Quest));
        Assert.Equal(DialogueRefusal.None, alone.Talk("npc.gaspard", out DialogueView? view));
        Assert.NotEqual("compagnons_lien", view!.Node);

        // Recruter Nyx (amenée par l'histoire) apprend le fait du recrutement ; refus atomique sans elle.
        WorldSession w = Play(Zone(27, -12), hero);
        Assert.Equal("notAvailable", w.RecruitCompanion(Catalog, "companion.nyx", available: false));
        Assert.False(w.Narrative!.Knows(StoryValidator.CompanionRecruitedFact));
        Assert.True(w.Companions.IsEmpty);
        Assert.Null(w.RecruitCompanion(Catalog, "companion.nyx", available: true));
        Assert.True(w.Narrative.Knows(StoryValidator.CompanionRecruitedFact));
        Assert.True(w.Narrative.IsOpen(Quest));
        Assert.Equal("bondQuestNotDone", w.OpenSecondCompanionSlot(Catalog));

        // Gaspard la propose ; « Pas maintenant » la laisse ouverte, et on peut y revenir.
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.gaspard", out view));
        Assert.Equal("compagnons_lien", view!.Node);
        Assert.Equal(DialogueRefusal.None, w.Choose(1, out _, out _));
        Assert.True(w.Narrative.IsOpen(Quest));
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.gaspard", out view));
        DialogueOption back = view!.Options.Single(o => o.Text == "text.compagnons.lien_au_feu.reprendre");
        Assert.Equal(DialogueRefusal.None, w.Choose(back.Index, out DialogueView? offer, out _));
        Assert.Equal("compagnons_lien", offer!.Node);
        Assert.Equal(DialogueRefusal.None, w.Choose(0, out _, out IReadOnlyList<StoryEvent> started));
        Assert.Contains(started, e => e.Kind == StoryEventKind.QuestStarted && e.Id == Quest);

        // Au feu du camp des rôdeurs : la quête se termine, puis la deuxième place s'ouvre (niveau 12 atteint).
        WorldSession camp = Play(Zone(100, 70), w.Save().Character!);
        var events = new List<StoryEvent>();
        for (int i = 0; i < 4 && !camp.Narrative!.IsDone(Quest); i++) events.AddRange(camp.Step().Story);
        Assert.True(camp.Narrative!.IsDone(Quest));
        Assert.Null(camp.OpenSecondCompanionSlot(Catalog));
        Assert.Equal(2, camp.Companions.Slots);
        Assert.Equal("allSlotsOpen", camp.OpenSecondCompanionSlot(Catalog));
        Assert.Equal(2, camp.Save().Character!.Companions.Slots);

        // Une sauvegarde déjà dotée d'un compagnon, sans le fait (troupe recrutée avant la quête) : le fait est appris au chargement.
        WorldSession old = Play(Zone(27, -12), hero.WithCompanions(w.Companions));
        Assert.True(old.Narrative!.Knows(StoryValidator.CompanionRecruitedFact));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(18)]
    [InlineData(30)]
    [InlineData(60)]
    public void Chaque_compagnon_a_chaque_palier_se_bat_aux_cotes_du_Bastion(int level)
    {
        CombatSetup baseSetup = ScenarioJsonReader.ReadFile(ScenarioPath("espace_compagnon"), ShippedData).Setup!;
        foreach (CompanionSheet sheet in Catalog.Companions)
        {
            AssailantSetup companion = CompanionCombat.Build(Catalog, sheet, level, CompanionRules.CombatantId(sheet.Id), "bastion", "joueurs", 100,
                new CombatantPlacement(GroundVector.Parse("-6", "1.5"), Fixed.Parse("0.5")));
            var setup = new CombatSetup(baseSetup.Seed, 6000, baseSetup.Stats, baseSetup.Damage, baseSetup.Progression, baseSetup.Combat,
                baseSetup.Combatants.Where(c => c.Id != "nyx").Append(companion), Array.Empty<CombatCommand>(), baseSetup.Data, baseSetup.Space);
            Assert.Empty(setup.Validate());
            var log = new MemoryCombatLog();
            CombatEngine.Run(setup, log);
            // Des dégâts réellement infligés (montant positif), pas seulement une ligne qui le nomme.
            long dealt = log.Lines.Where(l => l.Contains("\"event\":\"damage\"", StringComparison.Ordinal))
                .Select(l => JsonDocument.Parse(l).RootElement)
                .Where(e => e.GetProperty("source").GetString() == companion.Id)
                .Sum(e => e.GetProperty("amount").GetInt64());
            Assert.True(dealt > 0, $"{sheet.Id} au niveau {level} n'inflige aucun dégât.");
        }
    }

    [Fact]
    public void Cendre_grandit_d_un_stade_a_l_autre_sans_jamais_reculer()
    {
        CompanionSheet cendre = Sheet("companion.cendre");
        for (int i = 1; i < cendre.Stages.Count; i++)
        {
            CompanionStage before = cendre.Stages[i - 1], after = cendre.Stages[i];
            Assert.True(after.FromLevel > before.FromLevel);
            // Au niveau où le stade suivant arrive, et à tous les niveaux après : jamais moins de PV, d'armure, de résistance, de dégâts.
            for (int level = after.FromLevel; level <= CompanionCombat.MaxLevel; level++)
            {
                Assert.True(after.Health.At(level) >= before.Health.At(level), $"PV, stade {after.Id}, niveau {level}");
                Assert.True(after.Armor.At(level) >= before.Armor.At(level), $"armure, stade {after.Id}, niveau {level}");
                Assert.True(after.MagicResistance.At(level) >= before.MagicResistance.At(level), $"résistance, stade {after.Id}, niveau {level}");
                Assert.True(after.Attacks.Max(a => a.Damage.At(level)) >= before.Attacks.Max(a => a.Damage.At(level)), $"dégâts, stade {after.Id}, niveau {level}");
            }
            Assert.True(after.Attacks.Count >= before.Attacks.Count);
            Assert.True(after.IntervalMs <= before.IntervalMs);
            Assert.True(before.Duo is null || after.Duo != null);
        }
    }

    [Fact]
    public void Chaque_mercenaire_tient_son_role()
    {
        // Relecture de la PR #175 : les six mercenaires se distinguent par leur rôle (fourchettes provisoires, question posée à Damien).
        CompanionStage Stage(string id) => Sheet(id).Stages.Single();
        long Hp(string id) => Stage(id).Health.At(20);
        long Armor(string id) => Stage(id).Armor.At(20);
        long Dps(string id) => Stage(id).Attacks.Max(a => a.Damage.At(20)) * 1000 / Stage(id).IntervalMs;
        string[] mercenaries = Catalog.Companions.Where(c => c.Kind == CompanionKind.Mercenary).Select(c => c.Id).ToArray();
        // Le bouclier : le plus de PV et d'armure, le moins de dégâts.
        Assert.All(mercenaries.Where(m => m != "companion.hadrien"), m => Assert.True(Armor("companion.hadrien") > Armor(m), m));
        Assert.All(mercenaries.Where(m => m != "companion.hadrien"), m => Assert.True(Hp("companion.hadrien") > Hp(m), m));
        Assert.All(mercenaries.Where(m => m != "companion.hadrien" && m != "companion.nahla"), m => Assert.True(Dps(m) > Dps("companion.hadrien"), m));
        // La brute et la lame : les plus gros dégâts ; l'archère frappe de loin, avec le moins de PV et d'armure.
        Assert.Equal(new[] { "companion.aveline", "companion.odran" }, mercenaries.OrderByDescending(Dps).Take(2).OrderBy(m => m, StringComparer.Ordinal));
        Assert.Equal("companion.ysee", mercenaries.OrderBy(Hp).First());
        Assert.Equal("companion.ysee", mercenaries.OrderBy(Armor).First());
        Assert.All(mercenaries.Where(m => m != "companion.ysee"), m => Assert.True(Stage("companion.ysee").MaxRange > Stage(m).MaxRange, m));
        // L'éclaireur : le plus rapide et le plus vif ; la soigneuse : les plus petits dégâts.
        Assert.All(mercenaries.Where(m => m != "companion.silvere"), m => Assert.True(Stage("companion.silvere").Speed > Stage(m).Speed, m));
        Assert.Equal("companion.silvere", mercenaries.OrderBy(m => Stage(m).IntervalMs).First());
        Assert.Equal("companion.nahla", mercenaries.OrderBy(m => Stage(m).Attacks.Max(a => a.Damage.At(20))).First());
        // Ronce, l'ours protecteur : solide, mais jamais au-dessus du bouclier de la Guilde ni des héros de l'histoire.
        CompanionStage ronce = Stage("companion.ronce");
        Assert.True(ronce.Health.At(20) <= Hp("companion.hadrien") && ronce.Armor.At(20) <= Armor("companion.hadrien"));
        Assert.True(ronce.Attacks.Max(a => a.Damage.At(20)) <= Stage("companion.odran").Attacks.Max(a => a.Damage.At(20)));
        Assert.All(Catalog.Companions.Where(c => c.Kind == CompanionKind.Hero),
            h => Assert.True(ronce.Duo!.Damage.At(20) <= h.Stages[0].Duo!.Damage.At(20), h.Id));
    }

    [Fact]
    public void Schema_JSON_du_catalogue()
    {
        MiniJsonSchema schema = MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "companions.schema.json"));
        Assert.True(schema.IsValid(JsonDocument.Parse(File.ReadAllText(CompanionJsonReader.PathIn(DataDirectory))).RootElement));
    }
}
