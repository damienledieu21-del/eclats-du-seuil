using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EdS.Data.Json;
using EdS.Data.Validation;
using System.Collections.Generic;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.26 (secteurs de simulation ; rupture retenue par Damien le 11 novembre 2026) : moteur 0.31.0, journal au format 25 (état
/// emporté, places recyclées d'une zone, ±4 096 m, 256 obstacles), scénarios en schéma 18 ; instantané (6) et règles (16) inchangés.
/// Nouvelle référence figée <c>Fixtures/reference-0.31.0-contenu-0.6.0-regles-0.17.0</c> : les anciens journaux y ont exactement le même
/// corps (seul l'en-tête change), et <c>espace_secteur</c> (un secteur de 320 m au coin sud-est des bornes, 195 obstacles, un personnage
/// qui arrive avec un état emporté, un loup blessé qui arrive avec ses PV). Les anciennes références restent figées : elles sont refusées
/// au rejeu par ce moteur (autre version) et leur corps est reproduit octet par octet.
/// </summary>
[Trait("Category", "Sector")]
[Trait("Category", "Regression")]
public class V226SecteursReferenceTests
{
    private const string Secteur = "espace_secteur";
    private const string Latest = "reference-0.31.0-contenu-0.6.0-regles-0.17.0", Previous = "reference-0.30.0-contenu-0.6.0-regles-0.17.0";

    /// <summary>Empreinte du journal figé du scénario de référence (format 25), documentée dans MOTEUR.md.</summary>
    public const string SecteurFingerprint = "0196c32344ffa53961c96fe6fcd9b4dea5d7ba899e64f5b8e0c06dde3dea197b";

    private static string Fixture(string set, string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", set, name + ".jsonl");

    private static CombatSetup Load(string name)
    {
        ScenarioLoadResult result = ScenarioJsonReader.ReadFile(ScenarioPath(name), ShippedData);
        Assert.True(result.IsValid, result.Report.ToString());
        return result.Setup!;
    }

    private static MemoryCombatLog Simulate(CombatSetup setup)
    {
        var log = new MemoryCombatLog();
        CombatEngine.Run(setup, log);
        return log;
    }

    [Fact]
    public void Versions_du_sous_lot()
    {
        Assert.Equal(("0.31.0", 25, 18, 6), (CombatEngine.Version, CombatEngine.LogFormatVersion, ScenarioValidator.CurrentSchemaVersion, CombatSnapshot.FormatVersion));
        JsonDocument schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "scenario.schema.json")));
        JsonElement root = schema.RootElement;
        Assert.Equal(18, root.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetInt32());
        Assert.True(root.GetProperty("$defs").GetProperty("combatant").GetProperty("properties").TryGetProperty("carried", out _));
        Assert.Equal(4096, root.GetProperty("$defs").GetProperty("point").GetProperty("items").GetProperty("maximum").GetInt32());
    }

    [Fact]
    public void Reference_figee_identique_rejouee_et_empreinte()
    {
        string path = Fixture(Latest, Secteur);
        string[] frozen = File.ReadAllLines(path);
        Assert.Equal(frozen, Simulate(Load(Secteur)).Lines);
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(frozen, ShippedData).Status);
        Assert.Equal(SecteurFingerprint, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
        // Ce que le journal montre : 195 obstacles, coordonnées au-delà de l'ancienne borne, arrivées avec état.
        JsonElement header = JsonDocument.Parse(frozen[0]).RootElement;
        JsonElement scenario = header.GetProperty("scenario");
        Assert.True(scenario.GetProperty("space").GetProperty("obstacles").GetArrayLength() > 64);
        Assert.True(scenario.GetProperty("space").GetProperty("bounds").GetProperty("min")[0].GetDecimal() > 1000m);
        Assert.Equal(2, scenario.GetProperty("combatants").EnumerateArray().Count(c => c.TryGetProperty("carried", out _)));
        JsonElement joined = frozen.Skip(1).Select(l => JsonDocument.Parse(l).RootElement)
            .First(e => e.GetProperty("event").GetString() == "combatantJoined" && e.GetProperty("combatant").GetString() == "bastion");
        Assert.Equal((300L, 40L, 30L, 2L), (joined.GetProperty("health").GetInt64(), joined.GetProperty("endurance").GetInt64(),
            joined.GetProperty("resonance").GetInt64(), joined.GetProperty("rampartCharges").GetInt64()));
    }

    [Theory]
    [InlineData("espace_betes")]
    [InlineData("espace_chemins")]
    [InlineData("espace_promenade")]
    [InlineData("entrainement_bonus")]
    [InlineData("entrainement_bouclier")]
    [InlineData("entrainement_declencheurs")]
    [InlineData("entrainement_equipement")]
    public void Ancienne_reference_refusee_et_corps_reproduit(string name)
    {
        // Sans état emporté ni arrivée sans place : le moteur 0.31.0 reproduit octet par octet les lignes d'événements du moteur 0.30.0 ;
        // seul l'en-tête change (format 25, version 0.31.0). L'ancienne référence reste figée et refusée au rejeu.
        string[] old = File.ReadAllLines(Fixture(Previous, name)), latest = File.ReadAllLines(Fixture(Latest, name));
        Assert.Equal(ReplayStatus.Refused, CombatLogReplayer.Replay(old, ShippedData).Status);
        Assert.Equal(old.Skip(1), latest.Skip(1));
        Assert.Equal(latest, Simulate(Load(name)).Lines);
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(latest, ShippedData).Status);
        string Header(string line) => line.Replace("\"formatVersion\":24,\"engineVersion\":\"0.30.0\"", "\"formatVersion\":25,\"engineVersion\":\"0.31.0\"", StringComparison.Ordinal);
        Assert.Equal(latest[0], Header(old[0]));
    }

    [Fact]
    public void Evenement_du_hameau_corps_identique()
    {
        string[] old = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "evenements-0.30.0-regles-0.17.0", "evenement_hameau.jsonl"));
        string[] latest = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "evenements-0.31.0-regles-0.17.0", "evenement_hameau.jsonl"));
        Assert.Equal(old.Skip(1), latest.Skip(1));
        Assert.Equal(latest, Simulate(Load("evenement_hameau")).Lines);
    }

    [Fact]
    public void Etat_emporte_dans_un_fichier_de_scenario_lu_et_reecrit()
    {
        // Le scénario de référence porte deux états emportés ; l'en-tête du journal les réécrit sous la même forme, relue à l'identique.
        CombatSetup setup = Load(Secteur);
        CarriedState bastion = setup.Combatants.Single(c => c.Id == "bastion").Carried!;
        Assert.Equal((300L, 40L, 500L, 30, 2, 500L), (bastion.Health!.Value, bastion.Endurance!.Value, bastion.EnduranceRegenMs, bastion.Resonance,
            bastion.RampartCharges, bastion.GlobalCooldownMs));
        Assert.Equal(3000L, bastion.Cooldowns["skill.bastion.heurt_de_bouclier"]);
        Assert.Equal(60L, setup.Combatants.Single(c => c.Id == "loup_blesse").Carried!.Health);
        CombatSetup? reread = CombatLogReplayer.ReadSetup(CombatLogHeader.Write(setup), ShippedData, out var problems);
        Assert.Empty(problems);
        Assert.Equal(CombatLogHeader.Write(setup), CombatLogHeader.Write(reread!));
        // Un champ inconnu, un état sur un mannequin ou sans arrivée : refusés à la lecture, avec leur chemin.
        Assert.False(ReadMutated(root => DataOf(root)["combatants"]![1]!["carried"]!["inconnu"] = 1, scenario: Secteur).IsValid);
        Assert.Contains(ReadMutated(root => DataOf(root)["combatants"]![1]!.AsObject().Remove("arrivalMs"), scenario: Secteur).Report.Errors,
            e => e.Path.Contains("carried", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Recharges_emportees_en_double_ou_sans_duree_refusees_au_chemin(bool missing)
    {
        // Revue Codex de la PR #149 : en objets publics, une recharge donnée deux fois (ou sans durée) donne un rapport, pas une exception.
        var report = new ValidationReport();
        var document = new ScenarioDocument();
        JsonHeader header = JsonInput.Parse(JsonInput.Utf8(File.ReadAllText(ScenarioPath(Secteur)), report)!, report,
            (input, element) => document.Data = ScenarioJsonReader.ReadData(input, element, "$.data", ShippedData.Rules.Combat));
        (document.SchemaVersion, document.Kind, document.ContentVersion) = (header.SchemaVersion, header.Kind, header.ContentVersion);
        CombatantData hero = document.Data!.Combatants!.Single(c => c!.Id == "bastion")!;
        const string skill = "skill.bastion.heurt_de_bouclier";
        hero.Carried!.Cooldowns = missing
            ? new List<KeyValuePair<string, int?>> { new(skill, null) }
            : new List<KeyValuePair<string, int?>> { new(skill, 1000), new(skill, 2000) };
        ScenarioLoadResult result = ScenarioValidator.Validate(document, ShippedData);
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Path == "$.data.combatants[1].carried.cooldowns." + skill
            && e.Code == (missing ? ValidationCode.MissingField : ValidationCode.DuplicateId));
    }
}
