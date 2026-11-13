using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Json;
using EdS.Data.Zones;
using EdS.Data.Saves;
using EdS.Data.Validation;
using EdS.World;
using EdS.Tools.Measures;
using EdS.Domain.Combat;
using static EdS.Data.Tests.ScenarioTestData;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.26 (PLAN_GRANDE_CARTE.md § 2.4, « purge du registre ») : sauvegarde en version 18 : le registre ne garde aucune clé de mort
/// (purgées à la fermeture de leur session), sessions des secteurs (<c>sectors</c>) ; une sauvegarde de version 17 est lue, purgée et
/// mise à jour.
/// </summary>
[Trait("Category", "Sector")]
public class V226RegistreTests
{
    private const string V17 = "{\n  \"schemaVersion\": 17,\n  \"kind\": \"save\",\n  \"contentVersion\": \"0.1.0-provisoire\",\n  \"data\": {\n"
        + "    \"zone\": \"zone.vallons\",\n    \"worldTick\": 1200,\n    \"sessions\": 3,\n    \"character\": null,\n"
        + "    \"consumed\": [\n      \"zone.vallons/1/death/loup1\",\n      \"zone.vallons/3/death/loup2\",\n      \"zone.vallons/passage/moulin\",\n"
        + "      \"zone.vallons/s08_05/2/death/loup1_v2\"\n    ]\n  }\n}\n";

    [Fact]
    public void Cles_de_mort()
    {
        Assert.True(SaveValidator.IsDeathKey("zone.vallons/3/death/loup1"));
        Assert.True(SaveValidator.IsDeathKey("zone.vallons/s08_05/12/death/loup1_v2"));
        Assert.False(SaveValidator.IsDeathKey("zone.vallons/passage/moulin"));
        Assert.False(SaveValidator.IsDeathKey("zone.vallons/boss/gardien_du_seuil"));
        Assert.False(SaveValidator.IsDeathKey("zone.vallons/x/3/death/loup1"));   // pas un nom de secteur
        Assert.False(SaveValidator.IsDeathKey("zone.vallons/3/death/"));
    }

    [Fact]
    public void Version_17_lue_purgee_et_reecrite_en_18()
    {
        Assert.Equal((19, 18), (SaveValidator.CurrentSchemaVersion, SaveValidator.SectorsSince));   // V2.28 : 19 (point de reprise)
        SaveLoadResult read = SaveJsonReader.Read(V17);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.True(read.Save!.Migrated);
        Assert.Equal(new[] { "zone.vallons/passage/moulin" }, read.Save.Consumed);
        Assert.Empty(read.Save.Sectors);
        string written = SaveJsonReader.Write(read.Save);
        Assert.Contains(SaveVersion.Current, written, StringComparison.Ordinal);
        Assert.DoesNotContain("death", written, StringComparison.Ordinal);
        Assert.DoesNotContain("sectors", written, StringComparison.Ordinal);
        Assert.Equal(written, SaveJsonReader.Write(SaveJsonReader.Read(written).Save!));
    }

    [Fact]
    public void Version_18_secteurs_et_refus()
    {
        var save = new WorldSave("zone.vallons", 1200, 3, new[] { "zone.vallons/passage/moulin", "zone.vallons/3/death/loup1" }, null, false,
            new[] { new KeyValuePair<string, long>("s08_05", 2), new KeyValuePair<string, long>("s07_05", 4) });
        Assert.Equal(new[] { "zone.vallons/passage/moulin" }, save.Consumed);   // purgée à la construction
        Assert.Equal(new[] { "s07_05", "s08_05" }, save.Sectors.Keys);
        string text = SaveJsonReader.Write(save);
        Assert.Contains("\"sectors\": {\n      \"s07_05\": 4,\n      \"s08_05\": 2\n    },", text, StringComparison.Ordinal);
        WorldSave back = SaveJsonReader.Read(text).Save!;
        Assert.Equal(save.Sectors, back.Sectors);
        Assert.False(back.Migrated);
        void Refused(string from, string to, string path)
        {
            SaveLoadResult r = SaveJsonReader.Read(text.Replace(from, to, StringComparison.Ordinal));
            Assert.False(r.IsValid);
            Assert.Contains(r.Report.Errors, e => e.Path.StartsWith(path, StringComparison.Ordinal));
        }
        Refused("\"zone.vallons/passage/moulin\"", "\"zone.vallons/3/death/loup1\", \"zone.vallons/passage/moulin\"", "$.data.consumed[0]");
        Refused("\"s07_05\": 4", "\"s07_05\": 0", "$.data.sectors.s07_05");
        Refused("\"s07_05\": 4", "\"secteur\": 4", "$.data.sectors.secteur");
        Refused("\"s07_05\": 4,\n      \"s08_05\": 2", "\"s08_05\": 2,\n      \"s07_05\": 4", "$.data.sectors.s07_05");
        Refused(SaveVersion.Current, "\"schemaVersion\": 17", "$.data.sectors");   // champ inconnu en version 17
        Assert.Throws<ArgumentException>(() => new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), sectors: new[] { new KeyValuePair<string, long>("s1", 1) }));
        Assert.Throws<ArgumentException>(() => new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), sectors: new[] { new KeyValuePair<string, long>("s01_01", 0) }));
    }

    [Fact]
    public void Registre_borne_apres_des_heures_de_morts()
    {
        // Les morts d'une session remplissent son registre vivant ; la sauvegarde (fermeture) les purge : 100 000 clés de mort ne bloquent
        // plus une partie.
        var keys = Enumerable.Range(0, SaveValidator.MaxConsumed).Select(i => $"zone.vallons/7/death/c{i}").ToArray();
        var save = new WorldSave("zone.vallons", 0, 7, keys.Append("zone.vallons/passage/moulin"));
        Assert.Equal(new[] { "zone.vallons/passage/moulin" }, save.Consumed);
    }

    [Fact]
    public void Mesures_des_secteurs_soixante_combattants_dans_l_espace_de_reference()
    {
        CombatSetup reference = ScenarioJsonReader.ReadFile(ScenarioPath("espace_secteur"), ShippedData).Setup!;
        foreach (string kind in new[] { "repartis", "engages", "calme" })
        {
            CombatSetup sector = SectorMeasures.Sector(reference, kind, 10_000);
            Assert.Empty(sector.Validate());
            Assert.Equal((60, 195, true), (sector.Combatants.Count, sector.Space!.Obstacles.Count, sector.Zone));
            if (kind == "calme")
            {
                // Revue Codex de la PR #151 : au calme, personne ne s'engage pendant toute la mesure.
                var engine = new EdS.Simulation.Engine.CombatEngine(sector, EdS.Simulation.Engine.NullCombatLog.Instance);
                for (int t = 0; t < 150; t++) engine.Step();
                Assert.DoesNotContain(engine.CombatantIds, id => engine.View(id).Engaged);
            }
            SectorMeasures.Result r = SectorMeasures.Measure(kind, sector, 20);
            Assert.Equal(20, r.Ticks);
            // Revue Codex de la PR #151 (10e tour) : « répartis » a bien des engagés et des non-engagés ; « engagés » : tous ; « calme » : aucun.
            if (kind == "repartis") Assert.InRange(r.Engaged, 1, 57);
            if (kind == "engages") Assert.Equal(59, r.Engaged);
            if (kind == "calme") Assert.Equal(0, r.Engaged);
            Assert.True(r.MeanMs >= 0 && r.MaxMs >= r.P95Ms);
        }
        Assert.Contains("| engages | 60 | 195 | 59 / 0 |", SectorMeasures.Format(new[] { new SectorMeasures.Result("engages", 60, 195, 20, 0.5, 0.7, 0.9, 1000, 59) }), StringComparison.Ordinal);
    }

    [Fact]
    public void Trop_de_secteurs_une_seule_erreur_de_volume()
    {
        // Relecture indépendante de la PR #151 : au-delà de 1 024 secteurs, la lecture échoue (pas de troncature silencieuse).
        var save = new WorldSave("zone.vallons", 0, 1, Array.Empty<string>(), sectors: new[] { new KeyValuePair<string, long>("s00_00", 1) });
        string text = SaveJsonReader.Write(save);
        string many = string.Join(",\n", Enumerable.Range(0, SaveValidator.MaxSectors + 1).Select(i => $"      \"s{i / 64:D2}_{i % 64:D2}\": 1"));
        SaveLoadResult r = SaveJsonReader.Read(text.Replace("      \"s00_00\": 1", many, StringComparison.Ordinal));
        Assert.False(r.IsValid);
        ValidationError e = Assert.Single(r.Report.Errors);
        Assert.Equal(("$.data.sectors", ValidationCode.LimitExceeded), (e.Path, e.Code));
        string exact = string.Join(",\n", Enumerable.Range(0, SaveValidator.MaxSectors).Select(i => $"      \"s{i / 64:D2}_{i % 64:D2}\": 1"));
        SaveLoadResult ok = SaveJsonReader.Read(text.Replace("      \"s00_00\": 1", exact, StringComparison.Ordinal));
        Assert.True(ok.IsValid, ok.Report.ToString());
        Assert.Equal(SaveValidator.MaxSectors, ok.Save!.Sectors.Count);
    }

    [Fact]
    public void Partie_solo_garde_les_sessions_des_secteurs()
    {
        // Revue Codex de la PR #151 : une sauvegarde v18 avec des sessions de secteurs, reprise par la partie solo puis réécrite, les garde.
        ZoneDefinition zone = V11TestData.Zone();
        WorldSave start = new(zone.Id, 0, 2, Array.Empty<string>(), V11TestData.Hero(), sectors: new[] { new KeyValuePair<string, long>("s08_06", 3) });
        WorldSave read = SaveJsonReader.Read(SaveJsonReader.Write(start)).Save!;
        var session = new WorldSession(read, zone, ShippedData, V11TestData.Progression, V11TestData.ItemsCatalog);
        for (int i = 0; i < 5; i++) session.Step();
        WorldSave after = SaveJsonReader.Read(SaveJsonReader.Write(session.Save())).Save!;
        Assert.Equal(3L, after.Sectors["s08_06"]);
        Assert.Equal(3L, after.Sessions);
    }
}
