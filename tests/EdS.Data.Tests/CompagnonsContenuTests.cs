using System;
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
        CompanionLoadResult result = CompanionJsonReader.ReadFile(CompanionJsonReader.PathIn(DataDirectory));
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
        // Ouverte à la fin du chapitre 1 (chef des bandits vaincu), elle apprend le fait de la deuxième place.
        Assert.Equal("fact.chef_des_bandits.vaincu", q.GetProperty("requires")[0].GetProperty("id").GetString());
        Assert.Contains(q.GetProperty("effects").EnumerateArray(), e => e.GetProperty("do").GetString() == "setFact"
            && e.GetProperty("id").GetString() == "fact.compagnons.deuxieme_place");
    }

    [Fact]
    public void La_quete_de_lien_finie_ouvre_la_deuxieme_place_au_niveau_12()
    {
        SavedCompanions roster = CompanionRules.Recruit(Catalog, SavedCompanions.Empty, "companion.nyx", new RecruitContext(true, null, 0, 1)).Roster;
        var done = new SavedStory(quests: new System.Collections.Generic.Dictionary<string, QuestProgress>
            { ["quest.compagnons.lien_au_feu"] = new QuestProgress(QuestProgress.Done) });
        Assert.Equal("bondQuestNotDone", CompanionRules.OpenSecondSlot(Catalog, roster, 20, SavedStory.Empty).Reason);
        Assert.Equal("levelTooLow", CompanionRules.OpenSecondSlot(Catalog, roster, 11, done).Reason);
        SavedCompanions two = CompanionRules.OpenSecondSlot(Catalog, roster, 12, done).Roster;
        Assert.Equal(2, two.Slots);
        Assert.Empty(CompanionRules.Check(Catalog, two, done, 12));
        // Recrues de la Guilde : chaque mercenaire reçoit un des quatre traits.
        foreach (CompanionSheet m in Catalog.Companions.Where(c => c.Kind == CompanionKind.Mercenary))
            Assert.NotNull(Catalog.Trait(CompanionRules.Recruit(Catalog, two, m.Id, new RecruitContext(false, "s", 1_000_000, 9)).Roster.Find(m.Id)!.Trait));
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
            Assert.Contains(log.Lines, l => l.Contains($"\"source\":\"{companion.Id}\"", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Schema_JSON_du_catalogue()
    {
        MiniJsonSchema schema = MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "companions.schema.json"));
        Assert.True(schema.IsValid(JsonDocument.Parse(File.ReadAllText(CompanionJsonReader.PathIn(DataDirectory))).RootElement));
    }
}
