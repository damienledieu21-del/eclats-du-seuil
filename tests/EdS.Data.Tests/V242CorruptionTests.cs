using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Corruption;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.42.1 : corruption qui s'étend et recule (cœur). Données <c>corruption.json</c> (version 1) et leur validateur à chemin
/// précis ; règles recalculées du temps du monde et des autels purifiés (terre gagnée 2 jours après son origine, un palier tous les 2 jours
/// jusqu'au 3, répit de 4 jours, corruption tenue par la source) ; purification d'un autel (refus atomiques, éclat, expérience, fait) ;
/// sauvegarde v31 (<c>character.corruption</c>) ; annonces ; marque à l'écran.
/// </summary>
[Trait("Category", "World")]
public class V242CorruptionTests
{
    private const string Shard = "item.eclat_de_seuil";
    private static readonly WorldClock Clock = new(50);

    private static ZoneDefinition Vallons { get; } = Zone();

    private static string Shipped => File.ReadAllText(CorruptionJsonReader.PathIn(DataDirectory));

    private static CorruptionLoadResult Read(Action<JsonObject>? mutate = null)
    {
        JsonObject root = JsonNode.Parse(Shipped)!.AsObject();
        mutate?.Invoke(root["data"]!.AsObject());
        return CorruptionJsonReader.Read(root.ToJsonString(), Vallons, ItemsCatalog);
    }

    private static CorruptionBook Book { get; } = Read().Book!;

    private static int Index(string area) => Book.Of(area)!.Index;

    private static SavedCorruption Purified(params (string Area, long Day)[] x) =>
        new(x.Select(e => new KeyValuePair<string, long>(e.Area, e.Day)));

    private static long Noon(long day) => WorldEventSchedule.TickAt(Clock, day * 1440 + 12 * 60);

    /// <summary>Partie au tick <paramref name="tick"/>, entrée en (<paramref name="x"/>, <paramref name="z"/>), corruption livrée en jeu.</summary>
    private static WorldSession Session(double x, double z, long tick, SavedCharacter hero)
    {
        ZoneDefinition zone = Zone(x, z);
        var w = new WorldSession(new WorldSave("zone.vallons", tick, 0, new[] { PlayerCharacters.PlacesKey }, hero), zone, ShippedData, Progression,
            ItemsCatalog, V11TestData.Book(zone), safePoint: "place_du_village");
        w.UseCorruption(Read().Book);
        return w;
    }

    private static List<CorruptionNotice> Run(WorldSession w, int ticks)
    {
        var notices = new List<CorruptionNotice>();
        for (int i = 0; i < ticks; i++)
        {
            w.Step();
            notices.AddRange(w.CorruptionNotices);
        }
        return notices;
    }

    // --- Données et formats -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Donnees_livrees_valides_versions_et_schemas()
    {
        CorruptionLoadResult r = Read();
        Assert.True(r.IsValid, r.Report.ToString());
        Assert.Equal((1, 31, 31), (CorruptionValidator.CurrentSchemaVersion, SaveValidator.CurrentSchemaVersion, SaveValidator.CorruptionSince));
        Assert.Equal(("cercle_des_pierres", 2, 2, 3, 4, 150L, 10), (Book.Source.Id, Book.SpreadDays, Book.GrowDays, Book.MaxStage, Book.RespiteDays, Book.PurifyXp, Book.StrengthPercent));
        Assert.Equal(Shard, Book.PurifyItem);
        Assert.Equal(8, Book.Areas.Count);
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "corruption.schema.json"))
            .IsValid(JsonDocument.Parse(Shipped).RootElement));
        // Chaque terre a son nom dans les textes, et chaque annonce son texte.
        Assert.All(Book.Areas.Select(a => a.Id).Append(Book.Source.Id), a => Assert.True(Texts.Has("text.corruption.area." + a), a));
        Assert.All(new[] { "spread", "grew", "weakened", "receded", "purified" }, k => Assert.True(Texts.Has("text.corruption." + k), k));
        Assert.All(new[] { "purify", "too_far", "clean", "no_item", "purified_xp", "label", "source", "stage.1", "stage.2", "stage.3" },
            k => Assert.True(Texts.Has("text.ui.corruption." + k), k));
        // Autels à l'écart des routes (8 m au moins de chaque tronçon) : on purifie hors du passage des caravanes.
        foreach (CorruptionArea a in Book.Areas)
            foreach (Road road in Vallons.Roads)
                for (int i = 1; i < road.Points.Count; i++)
                    Assert.True(SegmentDistance(a.Altar, road.Points[i - 1], road.Points[i]) > 8, $"{a.Id} sur {road.Id}");
    }

    private static double SegmentDistance(GroundVector p, GroundVector a, GroundVector b)
    {
        double px = p.X.Raw / 10000.0, pz = p.Z.Raw / 10000.0, ax = a.X.Raw / 10000.0, az = a.Z.Raw / 10000.0, bx = b.X.Raw / 10000.0, bz = b.Z.Raw / 10000.0;
        double dx = bx - ax, dz = bz - az, len = dx * dx + dz * dz;
        double t = len == 0 ? 0 : Math.Clamp(((px - ax) * dx + (pz - az) * dz) / len, 0, 1);
        return Math.Sqrt(Math.Pow(px - ax - t * dx, 2) + Math.Pow(pz - az - t * dz, 2));
    }

    public static IEnumerable<object[]> Refus() => new[]
    {
        R("zone", d => d["zone"] = "zone.ailleurs", "$.data.zone"),
        R("etendue_0", d => d["spreadDays"] = 0, "$.data.spreadDays"),
        R("etendue_31", d => d["spreadDays"] = 31, "$.data.spreadDays"),
        R("palier_9", d => d["maxStage"] = 9, "$.data.maxStage"),
        R("repit_absent", d => d.Remove("respiteDays"), "$.data.respiteDays"),
        R("objet_inconnu", d => d["purifyItem"] = "item.inconnu", "$.data.purifyItem"),
        R("xp_negative", d => d["purifyXp"] = -1, "$.data.purifyXp"),
        R("force_negative", d => d["strengthPercent"] = -1, "$.data.strengthPercent"),
        R("force_101", d => d["strengthPercent"] = 101, "$.data.strengthPercent"),
        R("force_absente", d => d.Remove("strengthPercent"), "$.data.strengthPercent"),
        R("fait_mal_forme", d => d["purifiedFact"] = "corruption", "$.data.purifiedFact"),
        R("source_autel", d => d["source"]!["altar"] = new JsonArray(1565, -625), "$.data.source.altar"),
        R("source_origine", d => d["source"]!["from"] = new JsonArray("pierres_ouest"), "$.data.source.from"),
        R("terres_vides", d => d["areas"] = new JsonArray(), "$.data.areas"),
        R("id_double", d => d["areas"]![1]!["id"] = "pierres_ouest", "$.data.areas[1].id"),
        R("id_source", d => d["areas"]![0]!["id"] = "cercle_des_pierres", "$.data.areas[0].id"),
        R("id_mal_forme", d => d["areas"]![0]!["id"] = "Pierres", "$.data.areas[0].id"),
        R("lieu_inconnu", d => d["areas"]![0]!["place"] = "nulle_part", "$.data.areas[0].place"),
        R("hors_aire", d => d["areas"]![0]!["position"] = new JsonArray(9000, 0), "$.data.areas[0].position"),
        R("rayon_petit", d => d["areas"]![0]!["radius"] = 9.9999, "$.data.areas[0].radius"),
        R("rayon_grand", d => d["areas"]![0]!["radius"] = 400.0001, "$.data.areas[0].radius"),
        R("autel_hors_disque", d => d["areas"]![0]!["altar"] = new JsonArray(1420, -700.0001), "$.data.areas[0].altar"),
        R("origine_inconnue", d => d["areas"]![0]!["from"] = new JsonArray("nulle_part"), "$.data.areas[0].from[0]"),
        R("origine_plus_loin", d => d["areas"]![0]!["from"] = new JsonArray("landes_grises"), "$.data.areas[0].from[0]"),
        R("origine_elle_meme", d => d["areas"]![0]!["from"] = new JsonArray("pierres_ouest"), "$.data.areas[0].from[0]"),
        R("origine_double", d => d["areas"]![4]!["from"] = new JsonArray("pierres_ouest", "pierres_ouest"), "$.data.areas[4].from[1]"),
        R("origines_vides", d => d["areas"]![0]!["from"] = new JsonArray(), "$.data.areas[0].from"),
        R("autel_absent", d => d["areas"]![0]!.AsObject().Remove("altar"), "$.data.areas[0].altar"),
    };

    private static object[] R(string name, Action<JsonObject> mutate, string path) => new object[] { name, mutate, path };

    [Theory]
    [MemberData(nameof(Refus))]
    public void Refus_au_chemin_precis(string name, Action<JsonObject> mutate, string path)
    {
        CorruptionLoadResult r = Read(mutate);
        Assert.False(r.IsValid, name);
        Assert.Contains(r.Report.Errors, e => e.Path == path);
    }

    [Fact]
    public void Bornes_du_catalogue_et_rayon_extreme()
    {
        // 32 terres au plus ; la 33e est refusée par la lecture.
        CorruptionLoadResult many = Read(d =>
        {
            var areas = new JsonArray();
            for (int i = 0; i < 33; i++)
                areas.Add(new JsonObject { ["id"] = $"t{i}", ["position"] = new JsonArray(1420, -630), ["radius"] = 400, ["altar"] = new JsonArray(1420, -230), ["from"] = new JsonArray("cercle_des_pierres") });
            d["areas"] = areas;
        });
        Assert.False(many.IsValid);
        Assert.Contains(many.Report.Errors, e => e.Path == "$.data.areas");
        // Rayon de 400 m et autel juste au bord : accepté (distance au carré en unités brutes, sans débordement).
        CorruptionLoadResult edge = Read(d => { d["areas"]![0]!["radius"] = 400; d["areas"]![0]!["altar"] = new JsonArray(1420, -230); });
        Assert.True(edge.IsValid, edge.Report.ToString());
        Assert.False(Read(d => d["areas"]![0]!["altar"] = new JsonArray(1420, -229.9999)).IsValid);
    }

    // --- Règles ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void La_corruption_s_etend_de_proche_en_proche()
    {
        long[] since = CorruptionRules.CorruptedSince(Book, SavedCorruption.Empty);
        // Décision de Damien (revue de la PR #242) : une seule terre de plus tous les 2 jours, dans l'ordre du fichier.
        Assert.Equal(new long[] { 2, 4, 6, 8, 10, 12, 14, 16 }, since);
        Assert.Equal(new[] { "pierres_ouest", "faille_du_nord", "faille_de_l_est", "faille_du_sud", "landes_grises", "marches_de_l_abbaye", "confins_du_sud", "plaine_grise" },
            Book.Areas.Select(a => a.Id));
        // Jour 1 : aucune terre ; jour 2 : 1 ; jour 4 : 2 ; … ; jour 16 : 8 (et jamais deux le même jour).
        for (long day = 0; day <= 18; day++)
            Assert.Equal((int)Math.Min(8, day / 2), since.Count(c => CorruptionRules.StageAt(c, Book, day) > 0));
        // Paliers : propre la veille, 1 le jour même, un de plus tous les 2 jours, au plus 3.
        Assert.Equal(new[] { 0, 1, 1, 2, 2, 3, 3 }, Enumerable.Range(1, 7).Select(d => CorruptionRules.StageAt(2, Book, d)).ToArray());
        Assert.Equal(3, CorruptionRules.StageAt(2, Book, SavedEvent.MaxDay));
        Assert.Equal((2L, 4L, 6L, (long?)null), (CorruptionRules.NextChangeDay(2, Book, 0)!.Value, CorruptionRules.NextChangeDay(2, Book, 2)!.Value,
            CorruptionRules.NextChangeDay(2, Book, 5)!.Value, CorruptionRules.NextChangeDay(2, Book, 6)));
    }

    [Fact]
    public void Un_autel_purifie_fait_reculer_sa_terre_et_celles_qu_elle_seule_nourrit()
    {
        long[] since = CorruptionRules.CorruptedSince(Book, Purified(("pierres_ouest", 10)));
        Assert.Equal(14, since[Index("pierres_ouest")]);   // répit de 4 jours
        Assert.Equal(16, since[Index("landes_grises")]);   // coupée de la source : elle recule, et revient 2 jours après
        Assert.Equal(18, since[Index("plaine_grise")]);
        Assert.Equal(4, since[Index("faille_du_nord")]);   // les autres terres ne bougent pas
        // Une terre à deux origines garde la plus tôt : purifier l'une ne la coupe pas.
        since = CorruptionRules.CorruptedSince(Book, Purified(("faille_du_sud", 20)));
        Assert.Equal(14, since[Index("confins_du_sud")]);
        since = CorruptionRules.CorruptedSince(Book, Purified(("faille_du_sud", 20), ("faille_de_l_est", 21)));
        Assert.Equal(26, since[Index("confins_du_sud")]);
        // Une terre inconnue du catalogue est sans effet.
        Assert.Equal(CorruptionRules.CorruptedSince(Book, SavedCorruption.Empty), CorruptionRules.CorruptedSince(Book, Purified(("terre_disparue", 50))));
        // Bornes : le jour le plus grand de la sauvegarde, sans débordement.
        since = CorruptionRules.CorruptedSince(Book, Purified(("pierres_ouest", SavedEvent.MaxDay)));
        Assert.Equal(SavedEvent.MaxDay + 4 + 2 + 2, since[Index("plaine_grise")]);
        Assert.Equal(0, CorruptionRules.StageAt(since[Index("plaine_grise")], Book, SavedEvent.MaxDay));
    }

    [Fact]
    public void Autels_purifies_bornes_de_la_sauvegarde()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Purified(("pierres_ouest", -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Purified(("pierres_ouest", SavedEvent.MaxDay + 1)));
        Assert.Throws<ArgumentException>(() => Purified(("Pierres", 1)));
        Assert.Throws<ArgumentException>(() => Purified(("a", 1), ("a", 2)));
        Assert.Throws<ArgumentException>(() => Purified(Enumerable.Range(0, 33).Select(i => ($"t{i}", 1L)).ToArray()));
        SavedCorruption s = Purified(("b", 2), ("a", 1)).With("b", 5).With("c", 0);
        Assert.Equal(new[] { "a:1", "b:5", "c:0" }, s.Purified.Select(e => $"{e.Key}:{e.Value}"));
        Assert.Equal((5L, (long?)null), (s.DayOf("b")!.Value, s.DayOf("z")));
    }

    // --- Créatures plus fortes (décision de Damien du 21 novembre 2026) ------------------------------------------------------------

    private static GroundVector At(string x, string z) => new(Fixed.Parse(x), Fixed.Parse(z));

    [Fact]
    public void Force_des_creatures_dix_pour_cent_par_palier_frontieres_comprises()
    {
        long[] since = CorruptionRules.CorruptedSince(Book, SavedCorruption.Empty);
        GroundVector ouest = At("1420", "-630");
        // Paliers 0 à 3 au centre du versant ouest (gagné le jour 2) : 100, 110, 120, 130 %.
        Assert.Equal(new long[] { 100, 110, 110, 120, 120, 130, 130 }, Enumerable.Range(1, 7).Select(d => CorruptionRules.StrengthAt(Book, since, d, ouest)).ToArray());
        // La source : toujours au palier maximal, même le jour 0.
        Assert.Equal(130, CorruptionRules.StrengthAt(Book, since, 0, At("1565", "-625")));
        Assert.Equal(100, CorruptionRules.StrengthAt(Book, since, 0, ouest));
        // Bord du disque compris ; un dix-millième de mètre au-delà, plus rien (aucune autre terre là).
        Assert.Equal(130, CorruptionRules.StrengthAt(Book, since, 6, At("1490", "-630")));
        Assert.Equal(100, CorruptionRules.StrengthAt(Book, since, 6, At("1490.0001", "-630")));
        Assert.Equal(100, CorruptionRules.StrengthAt(Book, since, 6, At("0", "0")));
        // Autel purifié : plus de force pendant le répit.
        long[] purified = CorruptionRules.CorruptedSince(Book, Purified(("pierres_ouest", 6)));
        Assert.Equal(100, CorruptionRules.StrengthAt(Book, purified, 6, ouest));
        Assert.Equal(100, CorruptionRules.StrengthAt(Book, purified, 6, At("1150", "-560")));   // les landes, coupées, reculent aussi
        // Bornes : 100 % par palier au palier 8, sans débordement.
        CorruptionBook strongest = Read(d => { d["strengthPercent"] = 100; d["maxStage"] = 8; }).Book!;
        Assert.Equal(900, CorruptionRules.StrengthAt(strongest, CorruptionRules.CorruptedSince(strongest, SavedCorruption.Empty), SavedEvent.MaxDay, ouest));
        Assert.Throws<ArgumentException>(() => CorruptionRules.StrengthAt(Book, new long[1], 6, ouest));
    }

    [Fact]
    public void Creatures_de_la_zone_renforcees_selon_leur_terre()
    {
        // Les échos des terres corrompues : chacun renforcé au pourcentage de sa place, le jour 9 ; le modèle de sa population aussi.
        long[] since = CorruptionRules.CorruptedSince(Book, SavedCorruption.Empty);
        ZoneDefinition strong = CorruptionRules.Strengthen(Vallons, Book, SavedCorruption.Empty, 9);
        Assert.NotSame(Vallons, strong);
        var before = Vallons.Setup.Combatants.OfType<EdS.Domain.Combat.AssailantSetup>().ToDictionary(a => a.Id);
        int changed = 0;
        foreach (EdS.Domain.Combat.AssailantSetup a in strong.Setup.Combatants.OfType<EdS.Domain.Combat.AssailantSetup>())
        {
            EdS.Domain.Combat.AssailantSetup old = before[a.Id];
            bool boss = Vallons.Bosses.Any(b => b.Forms.Any(f => f.Id == a.Id));
            long percent = old.Placement is CombatantPlacement at && !boss ? CorruptionRules.StrengthAt(Book, since, 9, at.Position) : 100;
            Assert.Equal(percent == 100 ? old.Health : ZoneScaling.Strengthen(old, percent).Health, a.Health);
            Assert.Equal(old.Attacks.Select(x => percent == 100 ? x.RawDamage : x.RawDamage * percent / 100), a.Attacks.Select(x => x.RawDamage));
            if (percent > 100) changed++;
        }
        Assert.True(changed >= 2, $"{changed} créatures renforcées");
        Population echo = strong.Populations.Single(p => p.Creature == "echo_corrompu1");
        Population plain = Vallons.Populations.Single(p => p.Creature == "echo_corrompu1");
        long echoPercent = CorruptionRules.StrengthAt(Book, since, 9, plain.Template.Placement!.Position);
        Assert.Equal(130, echoPercent);
        Assert.Equal(ZoneScaling.Strengthen(plain.Template, 130).Health, echo.Template.Health);
        Assert.Same(echo.Template, strong.Setup.Combatants.Single(c => c.Id == echo.Template.Id));
        // Les créatures loin de toute terre ne changent pas ; sans corruption, à 0 %, ou pour une autre zone : la même zone.
        Population wolf = strong.Populations.First(p => CorruptionRules.StrengthAt(Book, since, 9, p.Template.Placement!.Position) == 100);
        Assert.Same(Vallons.Populations.Single(p => p.Creature == wolf.Creature).Template, wolf.Template);
        Assert.Same(Vallons, CorruptionRules.Strengthen(Vallons, null, SavedCorruption.Empty, 9));
        Assert.Same(Vallons, CorruptionRules.Strengthen(Vallons, Read(d => d["strengthPercent"] = 0).Book, SavedCorruption.Empty, 9));
        // Déterministe : mêmes données, même jour, mêmes créatures.
        Assert.Equal(strong.Setup.Combatants.OfType<EdS.Domain.Combat.AssailantSetup>().Select(a => a.Health),
            CorruptionRules.Strengthen(Vallons, Book, SavedCorruption.Empty, 9).Setup.Combatants.OfType<EdS.Domain.Combat.AssailantSetup>().Select(a => a.Health));
    }

    // --- Sauvegarde v31 ------------------------------------------------------------------------------------------------------------

    private static string SaveWith(string corruption, int? version = null)
    {
        SavedCharacter hero = Hero();
        if (corruption.Length > 0) hero = hero.WithCorruption(Purified(("landes_grises", 7), ("pierres_ouest", 3)));
        string json = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, hero));
        if (corruption.Length > 0) json = json.Replace("\"corruption\": {\"landes_grises\": 7, \"pierres_ouest\": 3}", corruption);
        return version is int v ? json.Replace(SaveVersion.Current, $"\"schemaVersion\": {v}") : json;
    }

    [Fact]
    public void Sauvegarde_v31_aller_retour_et_v30_encore_lue()
    {
        string json = SaveWith("\"corruption\": {\"landes_grises\": 7, \"pierres_ouest\": 3}");
        Assert.Contains("\"corruption\": {\"landes_grises\": 7, \"pierres_ouest\": 3}", json);
        SaveLoadResult r = SaveJsonReader.Read(json);
        Assert.True(r.IsValid, r.Report.ToString());
        Assert.Equal(new[] { "landes_grises:7", "pierres_ouest:3" }, r.Save!.Character!.Corruption.Purified.Select(e => $"{e.Key}:{e.Value}"));
        Assert.Equal(json, SaveJsonReader.Write(r.Save));
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json")).IsValid(JsonDocument.Parse(json).RootElement));
        // Sans autel purifié : pas de champ.
        Assert.DoesNotContain("\"corruption\"", SaveWith(string.Empty));
        // Version 30 : lue telle quelle sans le champ ; avec, refusée au chemin précis.
        Assert.True(SaveJsonReader.Read(SaveWith(string.Empty, 30)).IsValid);
        SaveLoadResult old = SaveJsonReader.Read(SaveWith("\"corruption\": {\"landes_grises\": 7, \"pierres_ouest\": 3}", 30));
        Assert.Contains(old.Report.Errors, e => e.Path == "$.data.character.corruption" && e.Code == ValidationCode.UnknownField);
    }

    [Theory]
    [InlineData("\"corruption\": {}", "$.data.character.corruption", ValidationCode.InvalidValue)]
    [InlineData("\"corruption\": {\"pierres_ouest\": 3, \"landes_grises\": 7}", "$.data.character.corruption.landes_grises", ValidationCode.InvalidValue)]
    [InlineData("\"corruption\": {\"Landes\": 7}", "$.data.character.corruption.Landes", ValidationCode.InvalidValue)]
    [InlineData("\"corruption\": {\"landes_grises\": -1}", "$.data.character.corruption.landes_grises", ValidationCode.OutOfRange)]
    [InlineData("\"corruption\": {\"landes_grises\": 100000001}", "$.data.character.corruption.landes_grises", ValidationCode.OutOfRange)]
    [InlineData("\"corruption\": {\"landes_grises\": \"7\"}", "$.data.character.corruption.landes_grises", null)]
    [InlineData("\"corruption\": [1]", "$.data.character.corruption", null)]
    public void Sauvegarde_refusee_au_chemin_precis(string field, string path, ValidationCode? code)
    {
        SaveLoadResult r = SaveJsonReader.Read(SaveWith(field));
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == path && (code is null || e.Code == code));
    }

    // --- Partie ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Purifier_un_autel_refus_atomiques_puis_un_eclat()
    {
        // Loin de l'autel : refusé ; terre inconnue ou source : refusé.
        WorldSession far = Session(1420, -630, Noon(6), Hero(0, new ItemStack(Shard, 1)));
        Run(far, 2);
        Assert.Equal(CorruptionRefusal.TooFar, far.PurifyAltar("pierres_ouest"));
        Assert.Equal(CorruptionRefusal.NoArea, far.PurifyAltar("nulle_part"));
        Assert.Equal(CorruptionRefusal.NoArea, far.PurifyAltar("cercle_des_pierres"));
        // À l'autel, sans éclat : refusé, rien ne change.
        WorldSession poor = Session(1421, -670, Noon(6), Hero());
        Run(poor, 2);
        string before = Snapshot(poor) + poor.Character.Xp + poor.CorruptionState.Purified.Count;
        Assert.Equal(CorruptionRefusal.NoItem, poor.PurifyAltar("pierres_ouest"));
        Assert.Equal(before, Snapshot(poor) + poor.Character.Xp + poor.CorruptionState.Purified.Count);
        // Le jour 1, la terre n'est pas encore gagnée : rien à purifier, l'éclat reste.
        WorldSession early = Session(1421, -670, Noon(1), Hero(0, new ItemStack(Shard, 1)));
        Run(early, 2);
        Assert.Equal(0, early.Corruption.Single(v => v.Area.Id == "pierres_ouest").Stage);
        Assert.Equal(CorruptionRefusal.NotCorrupted, early.PurifyAltar("pierres_ouest"));
        Assert.Equal(1, early.Inventory.Count(Shard));
        // Jour 18 : purifiée ; l'éclat est pris, l'expérience donnée, le fait appris tout de suite ; les landes et la plaine reculent.
        WorldSession w = Session(1421, -670, Noon(18), Hero(0, new ItemStack(Shard, 2)));
        Run(w, 2);
        Assert.Equal((3, 3, 2), (w.Corruption.Single(v => v.Area.Id == "pierres_ouest").Stage, w.Corruption.Single(v => v.Area.Id == "landes_grises").Stage,
            w.Corruption.Single(v => v.Area.Id == "plaine_grise").Stage));
        long xp = w.Character.Xp;
        Assert.Equal(CorruptionRefusal.None, w.PurifyAltar("pierres_ouest"));
        Assert.Equal(1, w.Inventory.Count(Shard));
        Assert.Equal(xp + 150, w.Character.Xp);
        Assert.Contains("fact.corruption.autel_purifie", w.Save().Character!.Story.Facts);
        Assert.Equal(18L, w.Save().Character!.Corruption.DayOf("pierres_ouest"));
        Assert.All(w.Corruption.Where(v => v.Area.Id is "pierres_ouest" or "landes_grises" or "plaine_grise"), v => Assert.False(v.IsCorrupted, v.Area.Id));
        Assert.Equal(22L, w.Corruption.Single(v => v.Area.Id == "pierres_ouest").Since);
        List<CorruptionNotice> said = Run(w, 1);
        Assert.Equal(new[] { "Purified:pierres_ouest:3:150", "Receded:landes_grises:0:0", "Receded:plaine_grise:0:0" },
            said.Select(n => $"{n.Kind}:{n.Area}:{n.Stage}:{n.Xp}"));
        Assert.Empty(Run(w, 3));
        // Purifiée : plus rien à purifier pendant le répit (le second éclat reste).
        Assert.Equal(CorruptionRefusal.NotCorrupted, w.PurifyAltar("pierres_ouest"));
        Assert.Equal(1, w.Inventory.Count(Shard));
        // Une partie reprise de la sauvegarde garde l'autel purifié.
        var again = new WorldSession(w.Save(), Zone(1421, -670), ShippedData, Progression, ItemsCatalog, V11TestData.Book(Vallons), safePoint: "place_du_village");
        again.UseCorruption(Book);
        Assert.False(again.Corruption.Single(v => v.Area.Id == "landes_grises").IsCorrupted);
    }

    [Fact]
    public void Fin_du_repit_et_repurification()
    {
        // Purifié le jour 18 : propre les jours 19 à 21 (P+3), de nouveau corrompu au palier 1 le jour 22 (P+4), et purifiable encore.
        WorldSession w = Session(1421, -670, Noon(18), Hero(0, new ItemStack(Shard, 2)));
        Run(w, 2);
        Assert.Equal(CorruptionRefusal.None, w.PurifyAltar("pierres_ouest"));
        WorldSave saved = w.Save();
        WorldSession At(long day)
        {
            var s = new WorldSession(new WorldSave("zone.vallons", Noon(day), saved.Sessions, saved.Consumed, saved.Character!), Zone(1421, -670), ShippedData,
                Progression, ItemsCatalog, V11TestData.Book(Vallons), safePoint: "place_du_village");
            s.UseCorruption(Book);
            Run(s, 2);
            return s;
        }
        WorldSession p3 = At(21);
        Assert.Equal(0, p3.Corruption.Single(v => v.Area.Id == "pierres_ouest").Stage);
        Assert.Equal(CorruptionRefusal.NotCorrupted, p3.PurifyAltar("pierres_ouest"));
        WorldSession p4 = At(22);
        Assert.Equal(1, p4.Corruption.Single(v => v.Area.Id == "pierres_ouest").Stage);
        Assert.Equal(0, p4.Corruption.Single(v => v.Area.Id == "landes_grises").Stage);   // revient 2 jours après son origine
        long xp = p4.Character.Xp;
        Assert.Equal(CorruptionRefusal.None, p4.PurifyAltar("pierres_ouest"));
        Assert.Equal((0, xp + 150, 22L), (p4.Inventory.Count(Shard), p4.Character.Xp, p4.Save().Character!.Corruption.DayOf("pierres_ouest")!.Value));
    }

    [Fact]
    public void Plafond_d_experience_et_personnage_tombe()
    {
        // Au plafond d'expérience : purifiée, rien de gagné (annoncé à 0).
        WorldSession top = Session(1421, -670, Noon(18), Hero(0, new ItemStack(Shard, 1)).WithXp(SaveValidator.MaxXp));
        Run(top, 2);
        Assert.Equal(CorruptionRefusal.None, top.PurifyAltar("pierres_ouest"));
        Assert.Equal(SaveValidator.MaxXp, top.Character.Xp);
        Assert.Equal(0L, Assert.Single(Run(top, 1), n => n.Kind == CorruptionNoticeKind.Purified).Xp);
        // Tombé (un sanglier mortel à côté de l'autel) : refusé, rien ne change.
        ZoneDefinition deadly = Zone(1421, -670, scenario: root =>
        {
            JsonObject boar = root["data"]!["combatants"]!.AsArray().Single(c => (string)c!["id"]! == "sanglier")!.AsObject();
            boar["position"] = new JsonArray(1428.5, -670);   // à 8 m au moins du bord de son secteur (x = 1 420)
            foreach (JsonNode? attack in boar["attacks"]!.AsArray()) attack!["rawDamage"] = 100_000;
        });
        var w = new WorldSession(new WorldSave("zone.vallons", Noon(18), 0, new[] { PlayerCharacters.PlacesKey }, Hero(0, new ItemStack(Shard, 1))), deadly, ShippedData,
            Progression, ItemsCatalog, V11TestData.Book(deadly), safePoint: "place_du_village");
        w.UseCorruption(Book);
        for (int i = 0; i < 2400 && !w.IsDefeated; i++) w.Step();
        Assert.True(w.IsDefeated);
        string before = Snapshot(w) + w.Character.Xp + w.CorruptionState.Purified.Count;
        Assert.Equal(CorruptionRefusal.Defeated, w.PurifyAltar("pierres_ouest"));
        Assert.Equal(before, Snapshot(w) + w.Character.Xp + w.CorruptionState.Purified.Count);
    }

    [Fact]
    public void La_corruption_gagne_une_terre_a_minuit_annoncee_une_fois()
    {
        // Juste avant le jour 2 : aucune terre ; au passage de minuit, une seule est gagnée (la première du fichier).
        long tick = WorldEventSchedule.TickAt(Clock, 2 * 1440) - 3;
        WorldSession w = Session(1421, -670, tick, Hero());
        List<CorruptionNotice> said = Run(w, 8);
        CorruptionNotice n0 = Assert.Single(said);
        Assert.Equal((CorruptionNoticeKind.Spread, "pierres_ouest", 1, 2L), (n0.Kind, n0.Area, n0.Stage, n0.Day));
        // Au jour 4, la deuxième, et le versant ouest prend son palier 2.
        said = Run(Session(1421, -670, WorldEventSchedule.TickAt(Clock, 4 * 1440) - 3, Hero()), 8);
        Assert.Equal(new[] { "Grew:pierres_ouest:2", "Spread:faille_du_nord:1" }, said.Select(n => $"{n.Kind}:{n.Area}:{n.Stage}"));
        // Au premier tick d'une session, rien n'est annoncé (les paliers sont seulement notés).
        Assert.Empty(Run(Session(1421, -670, Noon(9), Hero()), 2));
    }

    [Fact]
    public void Sans_corruption_ou_dans_une_autre_zone_rien()
    {
        WorldSession w = Session(1421, -670, Noon(6), Hero(0, new ItemStack(Shard, 1)));
        w.UseCorruption(null);
        Assert.Empty(w.Corruption);
        Assert.Null(w.CorruptionBook);
        Assert.Null(w.CorruptionAt(new GroundVector(Fixed.FromInt(1565), Fixed.FromInt(-625))));
        Assert.Equal(CorruptionRefusal.NoArea, w.PurifyAltar("pierres_ouest"));
        Assert.Empty(Run(w, 2));
    }

    // --- Affichage -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Marque_a_l_ecran_et_terre_la_plus_corrompue()
    {
        WorldSession w = Session(1421, -670, Noon(9), Hero());
        Run(w, 1);
        IReadOnlyList<CorruptionView> all = w.Corruption;
        Assert.Same(all, w.Corruption);   // la même liste tant que rien ne change
        Assert.True(all[0].IsSource);
        static GroundVector P(int x, int z) => new(Fixed.FromInt(x), Fixed.FromInt(z));
        Assert.Equal(1000, AdventureSession.CorruptionFadeAt(all, P(1565, -625)));   // au cœur de la source
        Assert.Equal(1000, AdventureSession.CorruptionFadeAt(all, P(1420, -630)));   // pierres ouest, palier 3 (jour 9)
        Assert.Equal(0, AdventureSession.CorruptionFadeAt(all, P(0, 0)));
        Assert.InRange(AdventureSession.CorruptionFadeAt(all, P(1420, -590)), 1, 999);
        Assert.Equal("cercle_des_pierres", w.CorruptionAt(P(1565, -625))!.Area.Id);
        Assert.Equal("faille_du_nord", w.CorruptionAt(P(1680, -450))!.Area.Id);
        Assert.Null(w.CorruptionAt(P(880, -420)));   // la plaine n'est gagnée que le jour 16
        Assert.Null(w.CorruptionAt(P(0, 0)));
    }
}
