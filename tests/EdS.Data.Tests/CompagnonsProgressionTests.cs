using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EdS.Data.Allies;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using EdS.Simulation.Randomness;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// Compagnons, progression (décisions de Damien du 14 novembre 2026) : recrutement (histoire, Guilde selon le rang et la paie, trait tiré
/// au hasard des mercenaires), places (une, puis deux par la quête de lien au feu de camp à partir du niveau doux), niveau du personnage,
/// hors combat sans mort jusqu'à la halte, estime, un seul compagnon au combat pendant le boss du monde ; sauvegarde en version 20
/// (migration des versions antérieures).
/// </summary>
[Trait("Category", "Companion")]
public class CompagnonsProgressionTests
{
    private static readonly CompanionCatalog Catalog = CompanionJsonReader.Read(CompagnonsCatalogueTests.Catalogue).Catalog!;

    private static readonly RecruitContext Story = new(true, null, 0, Rng(7));

    /// <summary>Histoire sauvegardée où la quête de lien au feu de camp est finie.</summary>
    internal static readonly SavedStory QuestDone = new(quests: new Dictionary<string, QuestProgress>
        { ["quest.vallons.lien_au_feu"] = new QuestProgress(QuestProgress.Done) });

    /// <summary>Tirage borné d'un Pcg32 neuf de graine <paramref name="seed"/> (générateur injecté du recrutement).</summary>
    internal static Func<uint, uint> Rng(ulong seed) => new Pcg32(seed).NextBounded;

    private static SavedCompanions With(params string[] ids)
    {
        SavedCompanions roster = SavedCompanions.Empty;
        foreach (string id in ids)
        {
            RosterResult r = CompanionRules.Recruit(Catalog, roster, id, new RecruitContext(true, "s", 1_000_000, Rng(7)));
            Assert.True(r.Accepted, r.Reason);
            roster = r.Roster;
        }
        return roster;
    }

    // --- Recrutement ---------------------------------------------------------------------------------------------

    [Fact]
    public void Heros_et_bete_par_l_histoire_mercenaire_a_la_Guilde_selon_le_rang_et_la_paie()
    {
        SavedCompanions empty = SavedCompanions.Empty;
        Assert.Equal("notAvailable", CompanionRules.Recruit(Catalog, empty, "companion.nyx", new RecruitContext(false, "s", 0, Rng(1))).Reason);
        RosterResult nyx = CompanionRules.Recruit(Catalog, empty, "companion.nyx", Story);
        Assert.True(nyx.Accepted);
        Assert.Equal(new[] { "companion.nyx" }, nyx.Roster.Active);   // il prend la place libre
        Assert.Equal(("alreadyRecruited", "unknownCompanion"), (CompanionRules.Recruit(Catalog, nyx.Roster, "companion.nyx", Story).Reason,
            CompanionRules.Recruit(Catalog, nyx.Roster, "companion.inconnu", Story).Reason));
        Assert.Equal("guildRankTooLow", CompanionRules.Recruit(Catalog, nyx.Roster, "companion.aveline", new RecruitContext(true, "f", 1000, Rng(1))).Reason);
        Assert.Equal("guildRankTooLow", CompanionRules.Recruit(Catalog, nyx.Roster, "companion.aveline", new RecruitContext(true, null, 1000, Rng(1))).Reason);
        Assert.Equal("notEnoughMoney", CompanionRules.Recruit(Catalog, nyx.Roster, "companion.aveline", new RecruitContext(true, "e", 119, Rng(1))).Reason);
        RosterResult aveline = CompanionRules.Recruit(Catalog, nyx.Roster, "companion.aveline", new RecruitContext(false, "d", 120, Rng(1)));
        Assert.True(aveline.Accepted);
        Assert.Equal(120, aveline.Cost);
        Assert.Equal(new[] { "companion.nyx" }, aveline.Roster.Active);   // une seule place : elle attend
        Assert.NotNull(aveline.Roster.Find("companion.aveline")!.Trait);
        Assert.Null(aveline.Roster.Find("companion.nyx")!.Trait);
        Assert.Empty(CompanionRules.Check(Catalog, aveline.Roster));
    }

    [Fact]
    public void Trait_tire_au_hasard_deterministe_et_parmi_les_traits_du_catalogue()
    {
        // Revue Codex de la PR #172 : le trait vient du Pcg32 injecté (un tirage borné), même graine, même trait.
        var drawn = new HashSet<string>();
        for (ulong seed = 0; seed < 64; seed++)
        {
            string trait = CompanionRules.DrawTrait(Catalog, Rng(seed));
            Assert.Equal(trait, CompanionRules.DrawTrait(Catalog, Rng(seed)));
            Assert.Equal(Catalog.Traits[(int)new Pcg32(seed).NextBounded((uint)Catalog.Traits.Count)].Id, trait);
            Assert.NotNull(Catalog.Trait(trait));
            drawn.Add(trait);
        }
        Assert.Equal(Catalog.Traits.Count, drawn.Count);   // tous les traits sortent
        Assert.Throws<InvalidOperationException>(() => CompanionRules.DrawTrait(Catalog, _ => (uint)Catalog.Traits.Count));
        // Un seul tirage, et seulement pour un mercenaire réellement recruté : un refus, un héros ou un réembauchage ne consomment rien.
        int calls = 0;
        uint Counting(uint bound) { calls++; return 0; }
        SavedCompanions empty = SavedCompanions.Empty;
        Assert.False(CompanionRules.Recruit(Catalog, empty, "companion.aveline", new RecruitContext(false, "c", 0, Counting)).Accepted);
        Assert.False(CompanionRules.Recruit(Catalog, empty, "companion.aveline", new RecruitContext(false, null, 1_000, Counting)).Accepted);
        SavedCompanions nyx = CompanionRules.Recruit(Catalog, empty, "companion.nyx", new RecruitContext(true, null, 0, Counting)).Roster;
        Assert.Equal(0, calls);
        SavedCompanions hired = CompanionRules.Recruit(Catalog, nyx, "companion.aveline", new RecruitContext(false, "c", 1_000, Counting)).Roster;
        Assert.Equal(1, calls);
        SavedCompanions gone = CompanionRules.Dismiss(Catalog, hired, "companion.aveline").Roster;
        Assert.True(CompanionRules.Recruit(Catalog, gone, "companion.aveline", new RecruitContext(false, "c", 1_000, Counting)).Accepted);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Refus_atomiques_la_troupe_ne_change_pas()
    {
        SavedCompanions roster = With("companion.nyx");
        foreach (RosterResult refused in new[]
        {
            CompanionRules.Recruit(Catalog, roster, "companion.nyx", Story),
            CompanionRules.Dismiss(Catalog, roster, "companion.nyx"),
            CompanionRules.SetActive(roster, new[] { "companion.nyx", "companion.cendre" }),
            CompanionRules.OpenSecondSlot(Catalog, roster, 30, SavedStory.Empty),
            CompanionRules.React(roster, "companion.cendre", 5),
        })
        {
            Assert.False(refused.Accepted);
            Assert.Same(roster, refused.Roster);
            Assert.Equal(0, refused.Cost);
        }
    }

    // --- Places ----------------------------------------------------------------------------------------------------

    [Fact]
    public void Deuxieme_place_par_la_quete_de_lien_et_le_niveau_doux()
    {
        SavedCompanions roster = With("companion.nyx", "companion.cendre");
        Assert.Equal("noFreeSlot", CompanionRules.SetActive(roster, new[] { "companion.nyx", "companion.cendre" }).Reason);
        Assert.Equal("bondQuestNotDone", CompanionRules.OpenSecondSlot(Catalog, roster, 30, SavedStory.Empty).Reason);
        Assert.Equal("levelTooLow", CompanionRules.OpenSecondSlot(Catalog, roster, 11, QuestDone).Reason);
        RosterResult open = CompanionRules.OpenSecondSlot(Catalog, roster, 12, QuestDone);
        Assert.True(open.Accepted);
        Assert.Equal(2, open.Roster.Slots);
        Assert.Equal("allSlotsOpen", CompanionRules.OpenSecondSlot(Catalog, open.Roster, 12, QuestDone).Reason);
        SavedCompanions two = CompanionRules.SetActive(open.Roster, new[] { "companion.cendre", "companion.nyx" }).Roster;
        Assert.Equal(new[] { "companion.cendre", "companion.nyx" }, two.Active);
        Assert.Equal("duplicate", CompanionRules.SetActive(two, new[] { "companion.nyx", "companion.nyx" }).Reason);
        // Boss du monde : un seul compagnon au combat, le premier de la troupe ; le second attend au point de repli.
        Assert.Equal(new[] { "companion.cendre" }, CompanionRules.Fighters(Catalog, two, worldBoss: true).Select(c => c.Id));
        Assert.Equal(2, CompanionRules.Fighters(Catalog, two, worldBoss: false).Count);
    }

    // --- Combat, hors combat sans mort, halte ------------------------------------------------------------------------

    private static CombatSetup Fight(IEnumerable<AssailantSetup> companions)
    {
        CombatSetup s = ScenarioJsonReader.ReadFile(ScenarioTestData.ScenarioPath("espace_compagnon"), ScenarioTestData.ShippedData).Setup!;
        return new CombatSetup(s.Seed, 8000, s.Stats, s.Damage, s.Progression, s.Combat, s.Combatants.Where(c => c.Id != "nyx").Concat(companions),
            Array.Empty<CombatCommand>(), s.Data, s.Space);
    }

    [Fact]
    public void Le_compagnon_monte_de_niveau_avec_le_personnage_tombe_puis_se_releve_au_camp()
    {
        SavedCompanions roster = With("companion.cendre");
        var placements = new Func<int, CombatantPlacement>(i => new CombatantPlacement(GroundVector.Parse("-5", (1.5m + i).ToString(System.Globalization.CultureInfo.InvariantCulture)), Fixed.Parse("0.5")));
        AssailantSetup low = CompanionRules.Combatants(Catalog, roster, 3, "bastion", "joueurs", false, placements).Single();
        AssailantSetup high = CompanionRules.Combatants(Catalog, roster, 20, "bastion", "joueurs", false, placements).Single();
        Assert.Equal(("cendre", 140L), (low.Id, low.Health!.Value));            // louveteau, niveau 3 : 120 + 2 × 10
        Assert.Equal(320L + 19 * 30, high.Health!.Value);                         // loup dressé, niveau 20
        Assert.Null(low.Companion!.Duo);
        Assert.NotNull(high.Companion!.Duo);
        // Le louveteau de niveau 1 face à deux loups : il tombe, la troupe le garde hors combat.
        AssailantSetup cub = CompanionRules.Combatants(Catalog, roster, 1, "bastion", "joueurs", false, placements).Single();
        var log = new MemoryCombatLog();
        CombatResult result = CombatEngine.Run(Fight(new[] { cub }), log);
        CombatantResult cendre = result.Combatants.Single(c => c.Id == "cendre");
        Assert.NotNull(cendre.DeathTick);   // il tombe
        SavedCompanions after = CompanionRules.AfterCombat(roster, new[] { ("companion.cendre", 30, cendre.DeathTick != null) });
        Assert.True(after.Find("companion.cendre")!.Down);
        Assert.Single(after.Recruited);   // jamais retiré : hors combat, pas mort
        SavedCompanions down = CompanionRules.AfterCombat(roster, new[] { ("companion.cendre", 30, true) });
        Assert.Empty(CompanionRules.Fighters(Catalog, down, false));   // hors combat : il ne se bat pas avant la halte
        SavedCompanions camp = CompanionRules.Camp(Catalog, down);
        Assert.Equal((false, 40), (camp.Find("companion.cendre")!.Down, camp.Find("companion.cendre")!.Bond));   // 30 + halte (10)
        Assert.Single(CompanionRules.Fighters(Catalog, camp, false));
        // La jauge revient au combat suivant.
        Assert.Equal(40, CompanionRules.Combatants(Catalog, camp, 1, "bastion", "joueurs", false, placements).Single().Companion!.Bond);
        Assert.Equal(100, CompanionRules.AfterCombat(camp, new[] { ("companion.cendre", 250, false) }).Find("companion.cendre")!.Bond);
    }

    [Fact]
    public void Estime_bornee_et_renvoi_des_mercenaires_seulement()
    {
        SavedCompanions roster = With("companion.nyx", "companion.aveline");
        Assert.Equal(100, CompanionRules.React(roster, "companion.nyx", 500).Roster.Find("companion.nyx")!.Approval);
        Assert.Equal(-100, CompanionRules.React(roster, "companion.nyx", int.MinValue).Roster.Find("companion.nyx")!.Approval);
        Assert.Equal("notMercenary", CompanionRules.Dismiss(Catalog, roster, "companion.nyx").Reason);
        SavedCompanions left = CompanionRules.Dismiss(Catalog, roster, "companion.aveline").Roster;
        Assert.Null(left.Find("companion.aveline"));
    }

    [Fact]
    public void Mercenaire_renvoye_puis_reembauche_garde_son_estime_et_son_trait()
    {
        // Décision de Damien du 15 novembre 2026 : seule la jauge de lien revient à 0.
        var guild = new RecruitContext(false, "c", 1_000, Rng(7));
        SavedCompanions hired = CompanionRules.Recruit(Catalog, SavedCompanions.Empty, "companion.aveline", guild).Roster;
        hired = CompanionRules.React(CompanionRules.AfterCombat(hired, new[] { ("companion.aveline", 60, false) }), "companion.aveline", -35).Roster;
        SavedCompanion before = hired.Find("companion.aveline")!;
        Assert.Equal((60, -35), (before.Bond, before.Approval));

        SavedCompanions gone = CompanionRules.Dismiss(Catalog, hired, "companion.aveline").Roster;
        Assert.Null(gone.Find("companion.aveline"));
        Assert.Empty(gone.Active);
        Assert.False(gone.IsEmpty);
        SavedCompanion former = gone.FindFormer("companion.aveline")!;
        Assert.Equal((0, -35, before.Trait, false), (former.Bond, former.Approval, former.Trait, former.Down));
        Assert.Empty(CompanionRules.Check(Catalog, gone));

        // Réembauché avec une autre graine : le même trait, la même estime, la jauge à 0 ; il quitte les anciens et paie de nouveau.
        foreach (ulong seed in new ulong[] { 1, 2, 3, 99, 12345 })
        {
            RosterResult back = CompanionRules.Recruit(Catalog, gone, "companion.aveline", new RecruitContext(false, "c", 1_000, Rng(seed)));
            Assert.True(back.Accepted);
            Assert.True(Catalog.TryGet("companion.aveline", out CompanionSheet sheet));
            Assert.Equal(sheet.Recruit.Price, back.Cost);
            SavedCompanion again = back.Roster.Find("companion.aveline")!;
            Assert.Equal((0, -35, before.Trait, false), (again.Bond, again.Approval, again.Trait, again.Down));
            Assert.Null(back.Roster.FindFormer("companion.aveline"));
            Assert.Empty(back.Roster.Former);
        }

        // Aller-retour par la sauvegarde, schéma documentaire compris.
        string text = Saved(gone);
        Assert.Contains("\"former\": [", text);
        SaveLoadResult read = SaveJsonReader.Read(text);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.Equal(gone, read.Save!.Character!.Companions);
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json")).IsValid(JsonDocument.Parse(text).RootElement));
        Assert.DoesNotContain("former", Saved(hired));

        // Formes impossibles : refusées à la construction et à la lecture, avec chemin.
        SavedCompanion aveline = new("companion.aveline", 0, 5, before.Trait);
        Assert.Throws<ArgumentException>(() => new SavedCompanions(1, new[] { aveline }, null, new[] { aveline }));
        Assert.Throws<ArgumentException>(() => new SavedCompanions(1, null, null, new[] { aveline.With(bond: 10) }));
        Assert.Throws<ArgumentException>(() => new SavedCompanions(1, null, null, new[] { aveline.With(down: true) }));
        Assert.Throws<ArgumentException>(() => new SavedCompanions(1, null, null, new[] { aveline, aveline }));
        string both = text.Replace("\"recruited\": []", $"\"recruited\": [{{\"id\": \"companion.aveline\", \"bond\": 0, \"approval\": 0, \"trait\": \"{before.Trait}\", \"down\": false}}]");
        Assert.NotEqual(text, both);
        Assert.Contains(SaveJsonReader.Read(both).Report.Errors, e => e.Path == "$.data.character.companions.former[0].id");
        Assert.Contains(SaveJsonReader.Read(text.Replace("\"approval\": -35", "\"approval\": 101")).Report.Errors,
            e => e.Path == "$.data.character.companions.former[0].approval");
        Assert.Contains(SaveJsonReader.Read(text.Replace("\"approval\": -35", "\"approval\": -35, \"bond\": 3")).Report.Errors,
            e => e.Path == "$.data.character.companions.former[0].bond");
        Assert.Contains(SaveJsonReader.Read(text.Replace(SaveVersion.Current, "\"schemaVersion\": 19")).Report.Errors, e => e.Path == "$.data.character.companions");
        // Accord avec le catalogue : un héros ne peut pas être un ancien mercenaire.
        Assert.Contains(CompanionRules.Check(Catalog, new SavedCompanions(1, null, null, new[] { new SavedCompanion("companion.nyx") })),
            p => p.Contains("pas un mercenaire"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Proprietes_suite_d_operations_au_hasard_troupe_toujours_valide(int seed)
    {
        var random = new Random(seed);
        string[] ids = { "companion.nyx", "companion.aveline", "companion.cendre", "companion.inconnu" };
        SavedCompanions roster = SavedCompanions.Empty;
        for (int step = 0; step < 300; step++)
        {
            string id = ids[random.Next(ids.Length)];
            RosterResult r = random.Next(8) switch
            {
                0 => CompanionRules.Recruit(Catalog, roster, id, new RecruitContext(random.Next(2) == 0, "c", random.Next(300), Rng((ulong)step))),
                1 => CompanionRules.Dismiss(Catalog, roster, id),
                2 => CompanionRules.SetActive(roster, ids.Where(_ => random.Next(3) == 0).ToArray()),
                3 => CompanionRules.OpenSecondSlot(Catalog, roster, random.Next(1, 30), random.Next(2) == 0 ? QuestDone : SavedStory.Empty),
                4 => CompanionRules.React(roster, id, random.Next(-50, 51)),
                5 => RosterResult.Ok(CompanionRules.AfterCombat(roster, new[] { (id, random.Next(-10, 200), random.Next(2) == 0) })),
                6 => RosterResult.Ok(CompanionRules.Camp(Catalog, roster)),
                _ => RosterResult.Ok(roster),
            };
            if (!r.Accepted) Assert.Same(roster, r.Roster);
            roster = r.Roster;
            Assert.Empty(CompanionRules.Check(Catalog, roster));
            Assert.True(roster.Active.Count <= roster.Slots && roster.Recruited.All(c => c.Bond is >= 0 and <= 100 && c.Approval is >= -100 and <= 100));
            Assert.True(CompanionRules.Fighters(Catalog, roster, true).Count <= 1);
            // Aller-retour par la sauvegarde.
            SavedCharacter hero = new SavedCharacter("heros", "class.bastion", 0).WithCompanions(roster);
            string text = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), hero));
            SaveLoadResult back = SaveJsonReader.Read(text);
            Assert.True(back.IsValid, back.Report.ToString());
            Assert.Equal(roster, back.Save!.Character!.Companions);
        }
    }

    // --- Sauvegarde (version 20) -----------------------------------------------------------------------------------

    private static string Saved(SavedCompanions roster) =>
        SaveJsonReader.Write(new WorldSave("zone.vallons", 12, 1, Array.Empty<string>(), new SavedCharacter("heros", "class.bastion", 0).WithCompanions(roster)));

    [Fact]
    public void Sauvegarde_version_20_aller_retour_absente_quand_vide_et_migration()
    {
        Assert.Equal((23, 20), (SaveValidator.CurrentSchemaVersion, SaveValidator.CompanionsSince));   // monture : 22
        SavedCompanions roster = CompanionRules.React(CompanionRules.AfterCombat(With("companion.nyx", "companion.aveline"),
            new[] { ("companion.nyx", 55, true) }).WithSecond(), "companion.aveline", -20).Roster;
        string text = Saved(roster);
        Assert.Contains("\"companions\": {\"slots\": 2", text);
        SaveLoadResult back = SaveJsonReader.Read(text);
        Assert.True(back.IsValid, back.Report.ToString());
        Assert.Equal(roster, back.Save!.Character!.Companions);
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json")).IsValid(JsonDocument.Parse(text).RootElement));
        // Vide : absente du fichier.
        string empty = Saved(SavedCompanions.Empty);
        Assert.DoesNotContain("companions", empty);
        // Migration : une sauvegarde de version 19 se lit (aucun compagnon) ; elle ne peut pas en porter.
        SaveLoadResult old = SaveJsonReader.Read(empty.Replace(SaveVersion.Current, "\"schemaVersion\": 19"));
        Assert.True(old.IsValid, old.Report.ToString());
        Assert.True(old.Save!.Character!.Companions.IsEmpty);
        Assert.True(old.Save.Migrated);
        SaveLoadResult refused = SaveJsonReader.Read(text.Replace(SaveVersion.Current, "\"schemaVersion\": 19"));
        Assert.Contains(refused.Report.Errors, e => e.Path == "$.data.character.companions");
    }

    [Fact]
    public void Chargement_deuxieme_place_et_traits_controles_avec_le_catalogue_et_la_partie()
    {
        // Relecture indépendante de la PR #172 : une sauvegarde à deux places sans quête ni niveau, ou aux traits incohérents, est relevée.
        SavedCompanions two = With("companion.nyx", "companion.aveline").WithSecond();
        Assert.Empty(CompanionRules.Check(Catalog, two, QuestDone, 12));
        Assert.Contains(CompanionRules.Check(Catalog, two, SavedStory.Empty, 12), p => p.Contains("quest.vallons.lien_au_feu"));
        Assert.Contains(CompanionRules.Check(Catalog, two, QuestDone, 11), p => p.Contains("niveau 11"));
        Assert.Empty(CompanionRules.Check(Catalog, SavedCompanions.Empty, SavedStory.Empty, 1));
        // Fichier retouché : deux places, un héros avec un trait, un mercenaire sans trait, un trait inconnu.
        string text = Saved(two);
        SavedCompanions read(string t) => SaveJsonReader.Read(t).Save!.Character!.Companions;
        string trait = two.Find("companion.aveline")!.Trait!;
        Assert.Contains(CompanionRules.Check(Catalog, read(text.Replace("\"id\": \"companion.nyx\", \"bond\": 0, \"approval\": 0, \"trait\": null",
            "\"id\": \"companion.nyx\", \"bond\": 0, \"approval\": 0, \"trait\": \"courageux\"")), QuestDone, 12), p => p.Contains("trait en trop"));
        Assert.Contains(CompanionRules.Check(Catalog, read(text.Replace($"\"trait\": \"{trait}\"", "\"trait\": null")), QuestDone, 12), p => p.Contains("trait manquant"));
        Assert.Contains(CompanionRules.Check(Catalog, read(text.Replace($"\"trait\": \"{trait}\"", "\"trait\": \"distrait\"")), QuestDone, 12), p => p.Contains("trait inconnu"));
        Assert.Contains(CompanionRules.Check(Catalog, read(text), SavedStory.Empty, 30), p => p.Contains("deuxième place"));
    }

    [Fact]
    public void Migration_depuis_la_version_15_sans_compagnon()
    {
        // Une sauvegarde de version 15 (réputation, sans stock, aspect, secteurs, reprise ni compagnons) se lit et se met à jour.
        string v15 = Saved(SavedCompanions.Empty).Replace(SaveVersion.Current, "\"schemaVersion\": 15");
        SaveLoadResult old = SaveJsonReader.Read(v15);
        Assert.True(old.IsValid, old.Report.ToString());
        Assert.True(old.Save!.Migrated);
        Assert.True(old.Save.Character!.Companions.IsEmpty);
        Assert.Contains(SaveVersion.Current, SaveJsonReader.Write(old.Save));
    }

    [Theory]
    [InlineData("\"slots\": 2", "\"slots\": 3", "$.data.character.companions.slots")]
    [InlineData("\"bond\": 55", "\"bond\": 101", "$.data.character.companions.recruited[1].bond")]
    [InlineData("\"approval\": -20", "\"approval\": -101", "$.data.character.companions.recruited[0].approval")]
    [InlineData("\"id\": \"companion.nyx\"", "\"id\": \"nyx\"", "$.data.character.companions.recruited[1].id")]
    [InlineData("\"id\": \"companion.nyx\"", "\"id\": \"companion.aveline\"", "$.data.character.companions.recruited[1].id")]
    [InlineData("\"active\": [\"companion.nyx\", \"companion.aveline\"]", "\"active\": [\"companion.nyx\", \"companion.nyx\"]", "$.data.character.companions.active[1]")]
    [InlineData("\"active\": [\"companion.nyx\", \"companion.aveline\"]", "\"active\": [\"companion.cendre\"]", "$.data.character.companions.active[0]")]
    [InlineData(", \"down\": true", "", "$.data.character.companions.recruited[1].down")]
    public void Sauvegarde_mal_formee_refusee_avec_chemin(string from, string to, string path)
    {
        SavedCompanions roster = CompanionRules.React(CompanionRules.AfterCombat(With("companion.nyx", "companion.aveline"),
            new[] { ("companion.nyx", 55, true) }).WithSecond(), "companion.aveline", -20).Roster;
        roster = CompanionRules.SetActive(roster, new[] { "companion.nyx", "companion.aveline" }).Roster;
        string text = Saved(roster);
        Assert.Contains(from, text);
        SaveLoadResult result = SaveJsonReader.Read(text.Replace(from, to));
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Path == path);
    }
}

internal static class CompagnonsTestRoster
{
    /// <summary>Ouvre la deuxième place (quête finie, niveau 12).</summary>
    public static SavedCompanions WithSecond(this SavedCompanions roster) =>
        CompanionRules.OpenSecondSlot(CompanionJsonReader.Read(CompagnonsCatalogueTests.Catalogue).Catalog!, roster, 12, CompagnonsProgressionTests.QuestDone) is
            { Accepted: true } open ? open.Roster : throw new InvalidOperationException("deuxième place refusée");
}
