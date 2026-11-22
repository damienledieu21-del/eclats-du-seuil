using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Corruption;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.42.3 (décision de Damien du 21 novembre 2026) : défi de la source de la corruption. Le gardien de la source (trois formes)
/// se lance au cercle des pierres levées quand 3 autels sont dans leur répit ; sa défaite purifie la source : toute la région respire 8 jours,
/// puis la corruption repart du cercle. Données (<c>corruption.json</c> version 2, la version 1 encore lue), règles, refus atomiques,
/// combat, récompense de la première victoire, sauvegarde v33, défi perdu.
/// </summary>
[Trait("Category", "World")]
public class V2423SourceTests
{
    private const string Shard = "item.eclat_de_seuil";
    private const string Source = "cercle_des_pierres";
    private static readonly WorldClock Clock = new(50);
    private static readonly string[] Forms = { "gardien_de_la_source", "gardien_de_la_source_fissure", "gardien_de_la_source_dechaine" };

    private static ZoneDefinition Vallons { get; } = Zone();

    private static string Shipped => File.ReadAllText(CorruptionJsonReader.PathIn(DataDirectory));

    private static string GuardianScenario => File.ReadAllText(Path.Combine(ScenariosDirectory, "zones", "vallons_gardien_source.json"));

    /// <summary>La corruption livrée (modifiée), le scénario du gardien lu du dossier <paramref name="scenarios"/> (par défaut, celui du dépôt).</summary>
    private static CorruptionLoadResult Read(Action<JsonObject>? mutate = null, string? scenarios = "", Action<JsonObject>? root = null)
    {
        JsonObject doc = JsonNode.Parse(Shipped)!.AsObject();
        mutate?.Invoke(doc["data"]!.AsObject());
        root?.Invoke(doc);
        return CorruptionJsonReader.Read(doc.ToJsonString(), Vallons, ItemsCatalog, scenarios == "" ? ScenariosDirectory : scenarios, ShippedData);
    }

    private static CorruptionBook Book { get; } = Read().Book!;

    private static SavedCorruption Purified(params (string Area, long Day)[] x) => new(x.Select(e => new KeyValuePair<string, long>(e.Area, e.Day)));

    private static SavedCorruption ThreeAltars(long day) => Purified(("pierres_ouest", day), ("faille_du_nord", day), ("faille_de_l_est", day));

    private static long Noon(long day) => WorldEventSchedule.TickAt(Clock, day * 1440 + 12 * 60);

    /// <summary>
    /// Gardien du défi modifié (PV, dégâts, cadence de chaque forme) dans un dossier de scénarios temporaire ; rend le catalogue lu avec lui.
    /// </summary>
    private static CorruptionBook WithGuardian(int health, int damage, int intervalMs)
    {
        string dir = Path.Combine(Path.GetTempPath(), "eds_v2423_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "zones"));
        try
        {
            JsonObject g = JsonNode.Parse(GuardianScenario)!.AsObject();
            foreach (JsonNode? c in g["data"]!["combatants"]!.AsArray())
            {
                c!["health"] = health;
                c["intervalMs"] = intervalMs;
                c["firstAttackMs"] = Math.Min(1500, intervalMs);
                foreach (JsonNode? a in c["attacks"]!.AsArray()) a!["rawDamage"] = damage;
            }
            File.WriteAllText(Path.Combine(dir, "zones", "vallons_gardien_source.json"), g.ToJsonString());
            CorruptionLoadResult r = Read(scenarios: dir);
            Assert.True(r.IsValid, r.Report.ToString());
            return r.Book!;
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>Partie au jour <paramref name="day"/> (midi), entrée en (<paramref name="x"/>, <paramref name="z"/>), avec la corruption <paramref name="book"/>.</summary>
    private static WorldSession Session(double x, double z, long day, SavedCharacter hero, CorruptionBook? book = null, IEnumerable<string>? consumed = null)
    {
        ZoneDefinition zone = Zone(x, z);
        var w = new WorldSession(new WorldSave("zone.vallons", Noon(day), 0, (consumed ?? Array.Empty<string>()).Append(PlayerCharacters.PlacesKey).Distinct().ToArray(), hero),
            zone, ShippedData, Progression, ItemsCatalog, V11TestData.Book(zone), safePoint: "place_du_village");
        w.UseCorruption(book ?? Book);
        w.Step();
        return w;
    }

    private static SavedCharacter Strong(SavedCorruption corruption) =>
        Hero().WithXp(Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= 22)).WithCorruption(corruption);

    /// <summary>Frappe la forme du gardien en jeu jusqu'à sa défaite (ou <paramref name="max"/> ticks), puis quelques ticks.</summary>
    private static List<SessionStep> Fight(WorldSession w, List<CorruptionNotice> notices, int max = 8000)
    {
        var steps = new List<SessionStep>();
        for (int i = 0; i < max && !steps.Any(s => s.Boss.Any(b => b.Kind == BossEventKind.Defeated)); i++)
        {
            string? form = w.SourceGuardianForm;
            if (i % 20 == 0 && form != null && w.Zone.Engine.CombatantIds.Contains(form))
                try { w.Zone.Engine.Submit(new CombatCommand(w.Zone.Engine.CurrentTick * 50, w.CombatantId, "skill.bastion.frappe_du_rempart", form)); }
                catch (ArgumentException) { }
            steps.Add(w.Step());
            notices.AddRange(w.CorruptionNotices);
        }
        for (int i = 0; i < 5; i++)
        {
            steps.Add(w.Step());
            notices.AddRange(w.CorruptionNotices);
        }
        return steps;
    }

    // --- Données et formats -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Defi_livre_valide_trois_formes_et_textes()
    {
        CorruptionChallenge c = Book.Challenge!;
        Assert.Equal(("gardien_de_la_source", 3, 8, 400L), (c.Boss.Id, c.MinAltars, c.RespiteDays, c.Xp));
        Assert.Equal(Forms, c.Boss.Forms.Select(f => f.Id));
        Assert.Equal(new[] { (Shard, 3) }, c.Boss.Rewards);
        Assert.Equal("fact.corruption.source_purifiee", c.Boss.Fact);
        Assert.All(c.Boss.Forms, f => Assert.False(CorruptionValidator.Beyond(f.Placement!.Position, Book.Source.Position, Book.Source.Radius)));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "corruption.schema.json")).IsValid(JsonDocument.Parse(Shipped).RootElement));
        Assert.All(new[] { "challenge_started", "source_purified", "challenge_failed", "source_blocked" }, k => Assert.True(Texts.Has("text.corruption." + k), k));
        Assert.All(new[] { "challenge", "challenge_few", "challenge_resting", "challenge_far", "challenge_no_room", "source_xp" },
            k => Assert.True(Texts.Has("text.ui.corruption." + k), k));
        Assert.True(Texts.Has("text.ui.boss_phase.gardien_de_la_source") && Texts.Has("text.ui.boss_defeated.gardien_de_la_source"));
        // Unity : le gardien prend le personnage du Gardien (dossier Mixamo « gardien »), chaque forme a son nom.
        Assert.All(Forms, f => Assert.Equal(("gardien", StyleSlot.Character), (StyleRules.CharacterOf(f), StyleRules.ForCombatant(f, false))));
        Assert.All(Forms, f => Assert.NotEqual(f, DisplayNames.Of(f)));
    }

    [Fact]
    public void Version_1_encore_lue_sans_defi_et_refusee_avec()
    {
        CorruptionLoadResult v1 = Read(d => d.Remove("challenge"), root: r => r["schemaVersion"] = 1);
        Assert.True(v1.IsValid, v1.Report.ToString());
        Assert.Null(v1.Book!.Challenge);
        CorruptionLoadResult bad = Read(root: r => r["schemaVersion"] = 1);
        Assert.False(bad.IsValid);
        Assert.Contains(bad.Report.Errors, e => e.Path == "$.data.challenge");
        Assert.False(Read(root: r => r["schemaVersion"] = 3).IsValid);
    }

    public static IEnumerable<object[]> Refus() => new[]
    {
        R("autels_negatifs", d => Ch(d)["minAltars"] = -1, "$.data.challenge.minAltars"),
        R("autels_9", d => Ch(d)["minAltars"] = 9, "$.data.challenge.minAltars"),
        R("autels_absents", d => Ch(d).Remove("minAltars"), "$.data.challenge.minAltars"),
        R("repit_0", d => Ch(d)["respiteDays"] = 0, "$.data.challenge.respiteDays"),
        R("repit_31", d => Ch(d)["respiteDays"] = 31, "$.data.challenge.respiteDays"),
        R("xp_negative", d => Ch(d)["xp"] = -1, "$.data.challenge.xp"),
        R("xp_trop", d => Ch(d)["xp"] = 100_001, "$.data.challenge.xp"),
        R("gardien_absent", d => Ch(d).Remove("boss"), "$.data.challenge.boss"),
        R("gardien_chef_de_zone", d => Ch(d)["boss"]!["id"] = "croc_gris", "$.data.challenge.boss.id"),
        R("forme_inconnue", d => Ch(d)["boss"]!["forms"]![0] = "inconnu", "$.data.challenge.boss.forms[0]"),
        R("forme_d_un_chef", d => Ch(d)["boss"]!["forms"]![0] = "croc_gris", "$.data.challenge.boss.forms[0]"),
        R("recompense_inconnue", d => Ch(d)["boss"]!["rewards"]![0]!["item"] = "item.inconnu", "$.data.challenge.boss.rewards[0].item"),
        R("fait_mal_forme", d => Ch(d)["boss"]!["fact"] = "source", "$.data.challenge.boss.fact"),
        R("hors_du_disque", d => d["source"]!["radius"] = 10, "$.data.challenge.boss.forms[0]"),
        R("champ_inconnu", d => Ch(d)["bonus"] = 1, "$.data.challenge.bonus"),
    };

    private static JsonObject Ch(JsonObject d) => d["challenge"]!.AsObject();

    private static object[] R(string name, Action<JsonObject> mutate, string path) => new object[] { name, mutate, path };

    [Theory]
    [MemberData(nameof(Refus))]
    public void Defi_refuse_avec_chemin_precis(string name, Action<JsonObject> mutate, string path)
    {
        CorruptionLoadResult r = Read(mutate);
        Assert.False(r.IsValid, name);
        Assert.Null(r.Book);
        Assert.Contains(r.Report.Errors, e => e.Path == path);
    }

    [Fact]
    public void Scenario_du_gardien_absent_ou_non_charge()
    {
        CorruptionLoadResult unloaded = Read(scenarios: null);
        Assert.False(unloaded.IsValid);
        Assert.Contains(unloaded.Report.Errors, e => e.Path == "$.data.challenge.boss.scenario");
        CorruptionLoadResult missing = Read(d => Ch(d)["boss"]!["scenario"] = "zones/inexistant");
        Assert.False(missing.IsValid);
        Assert.Single(missing.Report.Errors, e => e.Path == "$.data.challenge.boss.scenario");
    }

    [Fact]
    public void Frontieres_acceptees()
    {
        foreach ((string field, long value) in new[] { ("minAltars", 0L), ("minAltars", 8L), ("respiteDays", 1L), ("respiteDays", 30L), ("xp", 0L), ("xp", 100_000L) })
        {
            CorruptionLoadResult r = Read(d => Ch(d)[field] = value);
            Assert.True(r.IsValid, $"{field} = {value} : {r.Report}");
        }
    }

    // --- Règles --------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Repit_de_toute_la_region_puis_la_corruption_repart_du_cercle()
    {
        Assert.Equal(0, CorruptionRules.SourceSince(Book, SavedCorruption.Empty));
        SavedCorruption saved = Purified((Source, 10));
        Assert.Equal(18, CorruptionRules.SourceSince(Book, saved));
        Assert.True(CorruptionRules.SourceResting(Book, saved, 17));
        Assert.False(CorruptionRules.SourceResting(Book, saved, 18));
        long[] since = CorruptionRules.CorruptedSince(Book, saved);
        // Au départ du cercle (jour 18), une terre de plus tous les 2 jours, dans l'ordre du fichier, comme au début de la partie.
        long[] fresh = CorruptionRules.CorruptedSince(Book, SavedCorruption.Empty);
        Assert.Equal(fresh.Select(d => d + 18), since);
        for (int i = 0; i < Book.Areas.Count; i++) Assert.Equal(0, CorruptionRules.StageAt(since[i], Book, 19));
        Assert.Equal(1, CorruptionRules.StageAt(since[0], Book, 20));
        // Un catalogue sans défi ignore le jour gardé pour la source.
        CorruptionBook v1 = Read(d => d.Remove("challenge"), root: r => r["schemaVersion"] = 1).Book!;
        Assert.Equal(fresh, CorruptionRules.CorruptedSince(v1, saved));
        Assert.Equal(0, CorruptionRules.SourceSince(v1, saved));
    }

    [Fact]
    public void Autels_en_repit_frontieres()
    {
        SavedCorruption saved = Purified(("pierres_ouest", 6), ("faille_du_nord", 8), ("ailleurs", 9));
        Assert.Equal(0, CorruptionRules.AltarsResting(Book, saved, 5));   // purifié après ce jour : pas compté
        Assert.Equal(1, CorruptionRules.AltarsResting(Book, saved, 6));
        Assert.Equal(2, CorruptionRules.AltarsResting(Book, saved, 9));   // « ailleurs » : terre absente du catalogue
        Assert.Equal(1, CorruptionRules.AltarsResting(Book, saved, 10));  // 6 + 4 : répit fini
        Assert.Equal(0, CorruptionRules.AltarsResting(Book, saved, 12));
    }

    [Fact]
    public void Creatures_du_cercle_normales_pendant_le_repit()
    {
        long[] since = CorruptionRules.CorruptedSince(Book, Purified((Source, 10)));
        Assert.Equal(100, CorruptionRules.StrengthAt(Book, since, 17, Book.Source.Position, 18));
        Assert.Equal(130, CorruptionRules.StrengthAt(Book, since, 18, Book.Source.Position, 18));
        Assert.Equal(130, CorruptionRules.StrengthAt(Book, since, 17, Book.Source.Position));   // l'ancienne forme : source toujours corrompue
    }

    // --- Refus atomiques -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Refus_atomiques_du_defi()
    {
        void Refused(WorldSession w, SourceChallengeRefusal expected)
        {
            SavedCorruption before = w.CorruptionState;
            int combatants = w.Zone.Engine.CombatantIds.Count();
            Assert.Equal(expected, w.ChallengeSource());
            Assert.Same(before, w.CorruptionState);
            Assert.Equal(combatants, w.Zone.Engine.CombatantIds.Count());
            Assert.False(w.SourceChallengeUnderway);
            Assert.Null(w.SourceGuardianForm);
            w.Step();
            Assert.DoesNotContain(w.CorruptionNotices, n => n.Kind == CorruptionNoticeKind.ChallengeStarted);
        }
        Refused(Session(1420, -630, 6, Strong(ThreeAltars(6))), SourceChallengeRefusal.TooFar);
        Refused(Session(1556, -632, 6, Strong(Purified(("pierres_ouest", 6), ("faille_du_nord", 6)))), SourceChallengeRefusal.TooFewAltars);
        Refused(Session(1556, -632, 6, Strong(ThreeAltars(2))), SourceChallengeRefusal.TooFewAltars);   // répits finis (2 + 4 ≤ 6)
        Refused(Session(1556, -632, 6, Strong(ThreeAltars(6).With(Source, 0))), SourceChallengeRefusal.Resting);   // jour 0 + 8 > 6
        var full = ThreeAltars(6);
        for (int i = 0; full.Purified.Count < SavedCorruption.MaxEntries; i++) full = full.With("terre_oubliee_" + i, 1);
        Refused(Session(1556, -632, 6, Strong(full)), SourceChallengeRefusal.Full);
        CorruptionBook v1 = Read(d => d.Remove("challenge"), root: r => r["schemaVersion"] = 1).Book!;
        Refused(Session(1556, -632, 6, Strong(ThreeAltars(6)), v1), SourceChallengeRefusal.NoChallenge);
    }

    // --- Combat, purification, récompense, sauvegarde ----------------------------------------------------------------------------

    [Fact]
    public void Gardien_vaincu_la_source_purifiee_recompense_une_fois_puis_rejouable_apres_le_repit()
    {
        CorruptionBook weak = WithGuardian(health: 10, damage: 1, intervalMs: 60_000);
        WorldSession w = Session(1556, -632, 6, Strong(ThreeAltars(6)), weak);
        Assert.Equal(3, w.AltarsResting);
        long xpBefore = w.Save().Character!.Xp;
        Assert.Equal(SourceChallengeRefusal.None, w.ChallengeSource());
        Assert.True(w.SourceChallengeUnderway);
        Assert.Equal("gardien_de_la_source", w.SourceGuardianForm);
        Assert.Equal(SourceChallengeRefusal.Underway, w.ChallengeSource());
        var notices = new List<CorruptionNotice>();
        List<SessionStep> steps = Fight(w, notices);
        Assert.Contains(notices, n => n.Kind == CorruptionNoticeKind.ChallengeStarted && n.Area == Source);
        // Trois formes, l'une après l'autre, puis la victoire.
        Assert.Equal(new[] { 0, 1, 2 }, steps.SelectMany(s => s.Boss).Where(b => b.Kind == BossEventKind.FormArrived && b.Boss == "gardien_de_la_source").Select(b => b.Phase));
        CorruptionNotice purified = Assert.Single(notices, n => n.Kind == CorruptionNoticeKind.SourcePurified);
        Assert.Equal((8, 400L), (purified.Stage, purified.Xp));
        Assert.DoesNotContain(notices, n => n.Kind == CorruptionNoticeKind.Receded);   // une seule annonce pour toute la région
        Assert.Equal(new[] { (Shard, 3) }, steps.SelectMany(s => s.Rewards));
        Assert.Equal(6, w.CorruptionState.DayOf(Source));
        Assert.All(w.Corruption, v => Assert.Equal(0, v.Stage));   // la source comprise
        Assert.True(w.SourceResting);
        Assert.Equal(8, w.SourceRestDays);
        Assert.False(w.SourceChallengeUnderway);
        Assert.True(w.Save().Character!.Xp >= checked(xpBefore + 400));
        Assert.Equal(SourceChallengeRefusal.Resting, w.ChallengeSource());

        // Sauvegarde v33 : la source garde son jour sous son identifiant ; relue telle quelle.
        string json = SaveJsonReader.Write(w.Save());
        Assert.Contains("\"schemaVersion\": 33", json, StringComparison.Ordinal);
        Assert.Contains($"\"{Source}\": 6", json, StringComparison.Ordinal);
        Assert.Contains("fact.corruption.source_purifiee", json, StringComparison.Ordinal);
        WorldSave saved = SaveJsonReader.Read(json).Save!;
        Assert.Equal(6, saved.Character!.Corruption.DayOf(Source));
        Assert.Contains(Vallons.Id + "/boss/gardien_de_la_source", saved.Consumed);

        // Après le répit (jour 14) et trois autels de nouveau purifiés : rejouable, sans seconde récompense, l'expérience oui.
        SavedCharacter again = saved.Character.WithCorruption(saved.Character.Corruption.With("pierres_ouest", 14).With("faille_du_nord", 14).With("faille_de_l_est", 14));
        WorldSession w2 = Session(1556, -632, 14, again, weak, saved.Consumed);
        Assert.False(w2.SourceResting);
        Assert.Equal(SourceChallengeRefusal.None, w2.ChallengeSource());
        var notices2 = new List<CorruptionNotice>();
        List<SessionStep> steps2 = Fight(w2, notices2);
        Assert.Empty(steps2.SelectMany(s => s.Rewards));
        Assert.Equal(400L, Assert.Single(notices2, n => n.Kind == CorruptionNoticeKind.SourcePurified).Xp);
        Assert.Equal(14, w2.CorruptionState.DayOf(Source));
    }

    [Fact]
    public void Defi_perdu_personnage_tombe_rien_ne_change()
    {
        CorruptionBook deadly = WithGuardian(health: 100_000, damage: 5_000, intervalMs: 500);
        WorldSession w = Session(1556, -632, 6, Strong(ThreeAltars(6)), deadly);
        SavedCorruption before = w.CorruptionState;
        Assert.Equal(SourceChallengeRefusal.None, w.ChallengeSource());
        string form = w.SourceGuardianForm!;
        var notices = new List<CorruptionNotice>();
        for (int i = 0; i < 4000 && !notices.Any(n => n.Kind == CorruptionNoticeKind.ChallengeFailed); i++)
        {
            w.Step();
            notices.AddRange(w.CorruptionNotices);
        }
        Assert.True(w.IsDefeated);
        Assert.Single(notices, n => n.Kind == CorruptionNoticeKind.ChallengeFailed);
        Assert.DoesNotContain(notices, n => n.Kind == CorruptionNoticeKind.SourcePurified);
        Assert.Same(before, w.CorruptionState);
        Assert.False(w.SourceChallengeUnderway);
        Assert.Equal(SourceChallengeRefusal.Defeated, w.ChallengeSource());
        for (int i = 0; i < 3; i++) w.Step();
        CombatantView v = w.Zone.Engine.View(form);
        Assert.False(v.IsPresent && !v.IsDead);   // le gardien est reparti
    }

    // --- Relecture indépendante de la PR #252 : frontières et cas limites ---------------------------------------------------------

    /// <summary>Le catalogue livré avec le disque de la source réduit à <paramref name="radius"/>.</summary>
    private static CorruptionBook WithSourceRadius(double radius)
    {
        CorruptionLoadResult r = Read(d => d["source"]!["radius"] = radius);
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Book!;
    }

    [Theory]
    [InlineData(15.0, false)]      // 15 + 30 = 45 m : exactement à la laisse, le défi tient
    [InlineData(14.9999, true)]    // 44,9999 m : juste au-delà, le défi est perdu
    public void Laisse_exactement_rayon_plus_30_puis_juste_au_dela(double radius, bool lost)
    {
        CorruptionBook slow = WithGuardian(health: 100_000, damage: 1, intervalMs: 60_000);
        WorldSession w = Session(1610, -625, 6, Strong(ThreeAltars(6)), slow);   // 45 m du centre : au bord du disque (45 m)
        GroundVector at = w.Zone.Engine.View(w.CombatantId).Position!.Value;
        Assert.Equal((Fixed.FromInt(1610), Fixed.FromInt(-625)), (at.X, at.Z));
        Assert.Equal(SourceChallengeRefusal.None, w.ChallengeSource());
        // Même centre, disque réduit : le personnage, immobile, est à rayon + 30 m (ou juste au-delà) ; le gardien reste dans le disque.
        w.UseCorruption(WithSourceRadius(radius));
        var notices = new List<CorruptionNotice>();
        for (int i = 0; i < 3; i++)
        {
            w.Step();
            notices.AddRange(w.CorruptionNotices);
        }
        Assert.Equal(lost, notices.Any(n => n.Kind == CorruptionNoticeKind.ChallengeFailed));
        Assert.Equal(!lost, w.SourceChallengeUnderway);
        Assert.Null(w.CorruptionState.DayOf(Source));
    }

    /// <summary>
    /// Revue Codex de la PR #252 : défi perdu (laisse franchie) au tick où le moteur refuse le départ du gardien. <paramref name="refuse"/>
    /// prépare ce refus juste avant le départ ; le défi est réglé quand même (échec annoncé, sans exception) et un tick suivant ne relance rien.
    /// </summary>
    private static void Defi_perdu_depart_refuse(Action<Zone, string> refuse, Action<JsonObject>? scenario = null)
    {
        CorruptionBook slow = WithGuardian(health: 100_000, damage: 1, intervalMs: 60_000);
        ZoneDefinition zone = Zone(1610, -625, scenario);
        var w = new WorldSession(new WorldSave("zone.vallons", Noon(6), 0, new[] { PlayerCharacters.PlacesKey }, Strong(ThreeAltars(6))),
            zone, ShippedData, Progression, ItemsCatalog, V11TestData.Book(zone), safePoint: "place_du_village");
        w.UseCorruption(slow);
        w.Step();
        SavedCorruption before = w.CorruptionState;
        Assert.Equal(SourceChallengeRefusal.None, w.ChallengeSource());
        w.Step();
        Assert.True(w.SourceChallengeUnderway);
        int calls = 0;
        w.BeforeSourceLeaveForTest = (z, form) => { calls++; refuse(z, form); };
        w.UseCorruption(WithSourceRadius(14.9999));   // laisse juste franchie : le défi est perdu au tick suivant
        var notices = new List<CorruptionNotice>();
        for (int i = 0; i < 5; i++)
        {
            w.Step();
            notices.AddRange(w.CorruptionNotices);
        }
        Assert.Equal(1, calls);   // un seul essai de départ : aucun tick suivant ne le relance
        Assert.Single(notices, n => n.Kind == CorruptionNoticeKind.ChallengeFailed && n.Area == Source);
        Assert.DoesNotContain(notices, n => n.Kind == CorruptionNoticeKind.SourcePurified);
        Assert.False(w.SourceChallengeUnderway);
        Assert.Same(before, w.CorruptionState);
    }

    [Fact]
    public void Defi_perdu_session_terminee_echec_propre()
    {
        Defi_perdu_depart_refuse((z, _) =>
        {
            for (int i = 0; i < 10_000 && !z.IsOver; i++) z.Step();   // la session du combat arrive au bout de sa durée
            Assert.True(z.IsOver);
            Assert.Throws<InvalidOperationException>(() => z.Engine.Submit(new CombatCommand(z.Engine.CurrentTick * 50, "x", CombatAction.GuardStart)));
        }, s => s["data"]!["durationMs"] = 120_000);
    }

    [Fact]
    public void Defi_perdu_plafond_de_commandes_atteint_echec_propre()
    {
        Defi_perdu_depart_refuse((z, form) =>
        {
            // Le gardien a déjà toutes ses commandes du tick : son départ (une commande de plus) est refusé par le moteur.
            for (int i = 0; i < 100; i++)
                try { z.Engine.Submit(new CombatCommand(z.Engine.CurrentTick * 50, form, CombatAction.GuardStart)); }
                catch (InvalidOperationException) { break; }
            Assert.Throws<InvalidOperationException>(() => z.Engine.Submit(new CombatCommand(z.Engine.CurrentTick * 50, form, CombatAction.GuardStart)));
        });
    }

    [Fact]
    public void Defi_perdu_au_passage_dans_une_autre_session()
    {
        CorruptionBook slow = WithGuardian(health: 100_000, damage: 1, intervalMs: 60_000);
        WorldSession w = Session(1556, -632, 6, Strong(ThreeAltars(6)), slow);
        SavedCorruption before = w.CorruptionState;
        Assert.Equal(SourceChallengeRefusal.None, w.ChallengeSource());
        w.Step();
        Assert.True(w.SourceChallengeUnderway);
        // Passage de secteur (l'ancienne session reste ouverte) : le personnage n'est plus dans la session du combat.
        ZoneDefinition zone = Zone(1556, -632);
        var other = new WorldSession(w.Save(), zone, ShippedData, Progression, ItemsCatalog, V11TestData.Book(zone), safePoint: "place_du_village");
        other.Step();   // le personnage y est présent, debout, au même endroit : seule la session change
        Assert.True(other.Zone.Engine.View(other.CombatantId).IsPresent);
        w.PassToForTest(other.Zone);
        var notices = new List<CorruptionNotice>();
        for (int i = 0; i < 3; i++)
        {
            w.Step();
            notices.AddRange(w.CorruptionNotices);
        }
        Assert.Single(notices, n => n.Kind == CorruptionNoticeKind.ChallengeFailed);
        Assert.False(w.SourceChallengeUnderway);
        Assert.Same(before, w.CorruptionState);
    }

    [Fact]
    public void Session_renouvelee_gel_ou_fonte_le_combat_du_gardien_continue()
    {
        CorruptionBook sturdy = WithGuardian(health: 400, damage: 1, intervalMs: 60_000);
        WorldSession w = Session(1556, -632, 6, Strong(ThreeAltars(6)), sturdy);
        Assert.Equal(SourceChallengeRefusal.None, w.ChallengeSource());
        for (int i = 0; i < 5; i++) w.Step();
        Zone first = w.Zone;
        string form = w.SourceGuardianForm!;
        w.SeasonRenewForTest();
        var notices = new List<CorruptionNotice>();
        SessionStep renewal = w.Step();
        notices.AddRange(w.CorruptionNotices);
        Assert.NotSame(first, w.Zone);
        Assert.Equal(1, w.Renewals);
        Assert.DoesNotContain(notices, n => n.Kind == CorruptionNoticeKind.ChallengeFailed);
        Assert.True(w.SourceChallengeUnderway);
        Assert.StartsWith(form, w.SourceGuardianForm!);
        Assert.DoesNotContain(renewal.Boss, b => b.Kind == BossEventKind.FormArrived);   // pas de retour à la première forme d'un nouveau défi
        List<SessionStep> steps = Fight(w, notices);
        Assert.Single(notices, n => n.Kind == CorruptionNoticeKind.SourcePurified);
        Assert.DoesNotContain(notices, n => n.Kind == CorruptionNoticeKind.ChallengeFailed);
        Assert.Equal(new[] { (Shard, 3) }, steps.SelectMany(s => s.Rewards));
        Assert.Equal(6, w.CorruptionState.DayOf(Source));
    }

    [Fact]
    public void Victoire_sans_place_dans_la_sauvegarde_source_bloquee()
    {
        CorruptionBook weak = WithGuardian(health: 10, damage: 1, intervalMs: 60_000);
        WorldSession w = Session(1556, -632, 6, Strong(ThreeAltars(6)), weak);
        Assert.Equal(SourceChallengeRefusal.None, w.ChallengeSource());
        // La sauvegarde se remplit pendant le combat : plus de place pour la source à la victoire.
        SavedCorruption full = w.CorruptionState;
        for (int i = 0; full.Purified.Count < SavedCorruption.MaxEntries; i++) full = full.With("terre_oubliee_" + i, 1);
        w.SetCorruptionForTest(full);
        long xp = w.Save().Character!.Xp;
        var notices = new List<CorruptionNotice>();
        Fight(w, notices);
        Assert.Single(notices, n => n.Kind == CorruptionNoticeKind.SourceBlocked && n.Area == Source);
        Assert.DoesNotContain(notices, n => n.Kind == CorruptionNoticeKind.SourcePurified || n.Kind == CorruptionNoticeKind.ChallengeFailed);
        Assert.Same(full, w.CorruptionState);
        Assert.Null(w.CorruptionState.DayOf(Source));
        Assert.False(w.SourceResting);
        Assert.False(w.SourceChallengeUnderway);
        Assert.True(w.Save().Character!.Xp - xp < 400);   // pas l'expérience du défi (celle des formes vaincues seulement)
    }

    private static string SaveOf(SavedCorruption corruption) =>
        SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, Hero().WithCorruption(corruption)));

    [Fact]
    public void Sauvegarde_v32_relue_puis_ecrite_en_v33()
    {
        string v33 = SaveOf(Purified(("faille_du_nord", 5), ("pierres_ouest", 3)));
        Assert.Contains("\"schemaVersion\": 33", v33, StringComparison.Ordinal);
        string v32 = v33.Replace("\"schemaVersion\": 33", "\"schemaVersion\": 32");
        SaveLoadResult r = SaveJsonReader.Read(v32);
        Assert.True(r.IsValid, r.Report.ToString());
        Assert.Equal(new[] { "faille_du_nord:5", "pierres_ouest:3" }, r.Save!.Character!.Corruption.Purified.Select(e => $"{e.Key}:{e.Value}"));
        Assert.Equal(v33, SaveJsonReader.Write(r.Save));   // réécrite en v33, même contenu
    }

    [Fact]
    public void Sauvegarde_v33_aller_retour_avec_la_cle_de_la_source()
    {
        string json = SaveOf(ThreeAltars(6).With(Source, 6));
        Assert.Contains($"\"{Source}\": 6", json, StringComparison.Ordinal);
        SaveLoadResult r = SaveJsonReader.Read(json);
        Assert.True(r.IsValid, r.Report.ToString());
        Assert.Equal(6, r.Save!.Character!.Corruption.DayOf(Source));
        Assert.Equal(json, SaveJsonReader.Write(r.Save));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json")).IsValid(JsonDocument.Parse(json).RootElement));
    }

    [Fact]
    public void Sauvegarde_et_rechargement_en_plein_defi()
    {
        CorruptionBook slow = WithGuardian(health: 100_000, damage: 1, intervalMs: 60_000);
        WorldSession w = Session(1556, -632, 6, Strong(ThreeAltars(6)), slow);
        Assert.Equal(SourceChallengeRefusal.None, w.ChallengeSource());
        for (int i = 0; i < 5; i++) w.Step();
        Assert.True(w.SourceChallengeUnderway);
        // Le défi n'est pas gardé : la partie rechargée n'a ni gardien ni purification, et le défi se relance.
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!;
        Assert.Null(saved.Character!.Corruption.DayOf(Source));
        Assert.DoesNotContain(Vallons.Id + "/boss/gardien_de_la_source", saved.Consumed);
        WorldSession back = Session(1556, -632, 6, saved.Character, slow, saved.Consumed);
        Assert.False(back.SourceChallengeUnderway);
        Assert.Null(back.SourceGuardianForm);
        Assert.Equal(3, back.AltarsResting);
        Assert.Equal(SourceChallengeRefusal.None, back.ChallengeSource());
        Assert.True(back.SourceChallengeUnderway);
    }

    [Fact]
    public void Gardien_rejoue_apparence_d_echo_revenu()
    {
        Func<string, bool> knows = f => f == "fact.corruption.source_purifiee";
        Assert.True(EchoLooks.ChiefSlain(Vallons, knows, "gardien_de_la_source_fissure", Book));
        Assert.True(EchoLooks.ChiefSlain(Vallons, knows, "gardien_de_la_source_v2", Book));
        Assert.False(EchoLooks.ChiefSlain(Vallons, _ => false, "gardien_de_la_source", Book));
        Assert.False(EchoLooks.ChiefSlain(Vallons, knows, "gardien_de_la_source"));   // sans le catalogue de la corruption
        Assert.Equal("Poing de pierre", DisplayNames.Attack("poing_de_pierre"));
        Assert.Equal("Ruée des pierres", DisplayNames.Attack("ruee_des_pierres"));
    }
}
