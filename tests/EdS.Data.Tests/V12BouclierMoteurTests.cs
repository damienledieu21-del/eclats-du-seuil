using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Rules;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.2 : boucliers qui changent la garde. Règles en schéma 13 (bloc <c>combat.shield</c>, contenu 0.13.0), scénarios en schéma 13
/// (<c>shield</c> sur un personnage), moteur 0.25.0 et journal au format 19 (<c>shield</c> dans l'en-tête). Nouvelle référence figée
/// <c>Fixtures/reference-0.25.0-contenu-0.6.0-regles-0.13.0</c> : le scénario à déclencheurs (corps identique à la référence des règles
/// 0.12.0, qui reste figée et refusée au rejeu) et le scénario <c>entrainement_bouclier</c> (un pavois lourd et une rondache légère contre
/// les mêmes coups, aux mêmes instants).
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Regression")]
public class V12BouclierMoteurTests
{
    private const string Bouclier = "entrainement_bouclier";
    // Sous-lot V1.3 : la référence courante est celle du moteur 0.26.0 (règles 0.14.0) ; celle du moteur 0.25.0 reste figée (V13EffetsMoteurTests).
    private static string Reference(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference-0.29.0-contenu-0.6.0-regles-0.17.0", name + ".jsonl");   // V2.4 : moteur 0.28.0, règles 0.16.0 (corps identiques)
    private static string Frozen(string version, string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference-" + version, name + ".jsonl");

    private static CombatSetup Load(string name)
    {
        ScenarioLoadResult result = ScenarioJsonReader.ReadFile(ScenarioPath(name), ShippedData);
        Assert.True(result.IsValid, result.Report.ToString());
        return result.Setup!;
    }

    private static MemoryCombatLog Simulate(string name)
    {
        var log = new MemoryCombatLog();
        CombatEngine.Run(Load(name), log);
        return log;
    }

    private static ScenarioLoadResult Mutated(Action<JsonObject> mutate) => ReadMutated(mutate, scenario: Bouclier);

    private static void AssertError(ScenarioLoadResult result, string path, ValidationCode code)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Path == path && e.Code == code);
    }

    // --- Versions ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Versions_du_sous_lot()
    {
        // Sous-lot V1.3 : moteur 0.26.0, format 20, règles 0.14.0 (schéma 14), scénarios en schéma 14 ; le bloc combat.shield ne change pas.
        Assert.Equal(("0.29.0", 23), (CombatEngine.Version, CombatEngine.LogFormatVersion));   // V2.4.1 : bêtes qui se promènent
        // Sous-lot V1.8 : règles 0.15.0 (barre « 4 + ultime dès le début »), schéma inchangé.
        Assert.Equal((16, "0.17.0-provisoire"), (DataFormat.RulesSchemaVersion, DataFormat.ProvisionalRulesContentVersion));   // V2.4 : schéma 15, contenu 0.16.0
        Assert.Equal(17, ScenarioValidator.CurrentSchemaVersion);   // V2.4.1 : 17 (bêtes qui se promènent) ; V2.4 : 16 (bêtes qui fuient) ; sous-lot V1.8d : 15 (bonus modifié), moteur 0.27.0, format 21
        Assert.Equal("0.17.0-provisoire", ShippedData.Rules.ContentVersion);
        CombatRules rules = ShippedData.Rules.Combat;
        Assert.Equal((Fixed.Parse("0.45"), Fixed.Parse("0.7"), 4, 20, 50L), (rules.ShieldMinDamageReduction, rules.ShieldMaxDamageReduction,
            rules.ShieldMinCostPerBlock, rules.ShieldMaxCostPerBlock, rules.ShieldParryWindowShiftMs));
    }

    [Fact]
    public void Regles_au_schema_12_refusees_et_bloc_shield_obligatoire()
    {
        string rules = File.ReadAllText(Path.Combine(DataDirectory, "rules.json"));
        RulesLoadResult old = RulesJsonReader.Read(rules.Replace("\"schemaVersion\": 16", "\"schemaVersion\": 12"));
        Assert.False(old.IsValid);
        JsonObject root = JsonNode.Parse(rules)!.AsObject();
        root["data"]!["combat"]!.AsObject().Remove("shield");
        RulesLoadResult missing = RulesJsonReader.Read(root.ToJsonString());
        Assert.False(missing.IsValid);
        Assert.Contains(missing.Report.Errors, e => e.Path.StartsWith("$.data.combat.shield", StringComparison.Ordinal));
        root = JsonNode.Parse(rules)!.AsObject();
        root["data"]!["combat"]!["shield"]!["minCostPerBlock"] = 21;   // au-dessus du coût le plus haut
        RulesLoadResult over = RulesJsonReader.Read(root.ToJsonString());
        Assert.Contains(over.Report.Errors, e => e.Path == "$.data.combat.shield.minCostPerBlock");
    }

    // --- Scénario ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Garde_du_bouclier_lue_dans_le_scenario()
    {
        CombatSetup setup = Load(Bouclier);
        var heavy = Assert.IsType<CharacterSetup>(setup.Combatants[0]);
        var light = Assert.IsType<CharacterSetup>(setup.Combatants[1]);
        Assert.Equal(new ShieldGuard(Fixed.Parse("0.7"), 14, 100), heavy.Shield);
        Assert.Equal(new ShieldGuard(Fixed.Parse("0.5"), 6, 200), light.Shield);
        Assert.Null(Assert.IsType<CharacterSetup>(Load("entrainement_garde_parade").Combatants[0]).Shield);
    }

    [Fact]
    public void Garde_hors_des_bornes_des_regles_refusee_avec_son_chemin()
    {
        AssertError(Mutated(r => Combatant(r, 0)["shield"]!["damageReduction"] = 0.71), "$.data.combatants[0].shield.damageReduction", ValidationCode.InvalidValue);
        AssertError(Mutated(r => Combatant(r, 0)["shield"]!["damageReduction"] = 0.44), "$.data.combatants[0].shield.damageReduction", ValidationCode.InvalidValue);
        AssertError(Mutated(r => Combatant(r, 0)["shield"]!["costPerBlock"] = 21), "$.data.combatants[0].shield.costPerBlock", ValidationCode.InvalidValue);
        AssertError(Mutated(r => Combatant(r, 0)["shield"]!["costPerBlock"] = 3), "$.data.combatants[0].shield.costPerBlock", ValidationCode.InvalidValue);
        AssertError(Mutated(r => Combatant(r, 1)["shield"]!["parryWindowMs"] = 250), "$.data.combatants[1].shield.parryWindowMs", ValidationCode.InvalidValue);
        AssertError(Mutated(r => Combatant(r, 1)["shield"]!["parryWindowMs"] = 175), "$.data.combatants[1].shield.parryWindowMs", ValidationCode.InvalidValue);
        AssertError(Mutated(r => Combatant(r, 1)["shield"]!["parryWindowMs"] = 50), "$.data.combatants[1].shield.parryWindowMs", ValidationCode.InvalidValue);
        Assert.True(Mutated(r => Combatant(r, 0)["shield"]!["damageReduction"] = 0.45).IsValid);
        Assert.True(Mutated(r => Combatant(r, 0)["shield"]!["costPerBlock"] = 20).IsValid);
    }

    [Fact]
    public void Garde_incomplete_ou_mal_placee_refusee()
    {
        AssertError(Mutated(r => Combatant(r, 0)["shield"]!.AsObject().Remove("costPerBlock")), "$.data.combatants[0].shield.costPerBlock", ValidationCode.MissingField);
        Assert.False(Mutated(r => Combatant(r, 0)["shield"]!["parade"] = 1).IsValid);   // champ inconnu
        AssertError(Mutated(r => Combatant(r, 2)["shield"] = Combatant(r, 0)["shield"]!.DeepClone()), "$.data.combatants[2].shield", ValidationCode.InvalidValue);
        Assert.False(Mutated(r => r["schemaVersion"] = 12).IsValid);   // un fichier de schéma 12 ne connaît pas la garde : mis à jour à la main
    }

    // --- Moteur ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Le_bouclier_lourd_bloque_plus_et_coute_plus_le_leger_pare_plus_tot()
    {
        CombatResult result = CombatEngine.Run(Load(Bouclier), new MemoryCombatLog());
        CombatantResult heavy = result["bastion_lourd"], light = result["bastion_leger"];
        // Mêmes coups (100 bruts, toutes les 2 s), mêmes commandes : garde levée, parades à 2 850 et 4 950 ms.
        // La parade de 2 850 ms couvre 2 850 à 2 950 ms avec le pavois (100 ms) : le coup de 3 000 ms passe ; la rondache (200 ms) le pare.
        Assert.Equal((1L, 1L), (heavy.PerfectParries, heavy.FailedParries));
        Assert.Equal((2L, 0L), (light.PerfectParries, light.FailedParries));
        Assert.True(heavy.DamageTaken < light.DamageTaken, $"{heavy.DamageTaken} ≥ {light.DamageTaken}");
        Assert.Equal((160L, 220L), (heavy.DamageTaken, light.DamageTaken));
        Assert.Equal((0L, 0L), (heavy.GuardBreaks, light.GuardBreaks));
        // Règles effectives écrites dans la ligne combatant de chacun.
        List<JsonElement> lines = Simulate(Bouclier).Lines.Select(l => JsonDocument.Parse(l).RootElement.Clone())
            .Where(e => e.TryGetProperty("event", out JsonElement ev) && ev.GetString() == "combatant").ToList();
        JsonElement Rules(string id) => lines.Single(e => e.GetProperty("combatant").GetString() == id).GetProperty("rules");
        Assert.Equal((0.7m, 14L, 100L), (Rules("bastion_lourd").GetProperty("guard.damageReduction").GetDecimal(),
            Rules("bastion_lourd").GetProperty("guard.costPerBlock").GetInt64(), Rules("bastion_lourd").GetProperty("parry.windowMs").GetInt64()));
        Assert.Equal((0.5m, 6L, 200L), (Rules("bastion_leger").GetProperty("guard.damageReduction").GetDecimal(),
            Rules("bastion_leger").GetProperty("guard.costPerBlock").GetInt64(), Rules("bastion_leger").GetProperty("parry.windowMs").GetInt64()));
    }

    [Fact]
    public void Garde_dans_l_en_tete_et_rejeu_identique()
    {
        MemoryCombatLog log = Simulate(Bouclier);
        JsonElement header = JsonDocument.Parse(log.Lines[0]).RootElement;
        JsonElement shield = header.GetProperty("scenario").GetProperty("combatants")[0].GetProperty("shield");
        Assert.Equal("{\"damageReduction\":0.7,\"costPerBlock\":14,\"parryWindowMs\":100}", shield.GetRawText());
        Assert.False(header.GetProperty("scenario").GetProperty("combatants")[2].TryGetProperty("shield", out _));
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines, ShippedData).Status);
        CombatSetup? again = CombatLogReplayer.ReadSetup(log.Lines[0], ShippedData, out IReadOnlyList<string> problems);
        Assert.Empty(problems);
        Assert.Equal(new ShieldGuard(Fixed.Parse("0.5"), 6, 200), Assert.IsType<CharacterSetup>(again!.Combatants[1]).Shield);
    }

    [Fact]
    public void Sans_bouclier_rien_ne_change_dans_le_corps_du_journal()
    {
        // Le scénario sans bouclier écrit les mêmes lignes d'événements que sous le moteur 0.24.0 ; avec l'écu de départ (valeurs des
        // règles), aussi : seul l'en-tête porte la garde.
        IReadOnlyList<string> plain = Simulate("entrainement_garde_parade").Lines;
        ScenarioLoadResult withEcu = ReadMutated(r => Combatant(r, 0)["shield"] = JsonNode.Parse("{\"damageReduction\":0.6,\"costPerBlock\":10,\"parryWindowMs\":150}"),
            scenario: "entrainement_garde_parade");
        Assert.True(withEcu.IsValid, withEcu.Report.ToString());
        var log = new MemoryCombatLog();
        CombatEngine.Run(withEcu.Setup!, log);
        Assert.Equal(plain.Skip(1), log.Lines.Skip(1));
        Assert.NotEqual(plain[0], log.Lines[0]);
    }

    // --- Références figées -------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("entrainement_declencheurs")]
    [InlineData(Bouclier)]
    public void Journaux_identiques_octet_par_octet_aux_references_figees(string name)
    {
        string expected = File.ReadAllText(Reference(name));
        Assert.Equal(expected, Simulate(name).ToString());
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(File.ReadAllLines(Reference(name)), ShippedData).Status);
    }

    [Fact]
    public void Reference_des_regles_0_12_0_refusee_et_corps_reproduit()
    {
        // La référence des règles 0.12.0 (moteur 0.24.0, format 18) reste figée ; elle est refusée au rejeu (moteur et règles) ; le moteur
        // 0.25.0 en reproduit octet par octet les lignes d'événements : le scénario n'a pas de bouclier.
        string[] frozen = File.ReadAllLines(Frozen("0.24.0-contenu-0.6.0-regles-0.12.0", "entrainement_declencheurs"));
        ReplayOutcome outcome = CombatLogReplayer.Replay(frozen, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Contains(outcome.Problems, p => p.Contains("0.24.0", StringComparison.Ordinal));
        Assert.Contains(outcome.Problems, p => p.Contains("0.12.0-provisoire", StringComparison.Ordinal));
        string[] current = File.ReadAllLines(Reference("entrainement_declencheurs"));
        Assert.Equal(frozen.Skip(1), current.Skip(1));
        JsonElement before = JsonDocument.Parse(frozen[0]).RootElement, after = JsonDocument.Parse(current[0]).RootElement;
        Assert.Equal((18, "0.24.0"), (before.GetProperty("formatVersion").GetInt32(), before.GetProperty("engineVersion").GetString()));
        Assert.Equal((23, "0.29.0"), (after.GetProperty("formatVersion").GetInt32(), after.GetProperty("engineVersion").GetString()));
        Assert.Equal(before.GetProperty("scenario").GetRawText(), after.GetProperty("scenario").GetRawText());
        Assert.Equal(before.GetProperty("data").GetProperty("classes").GetRawText(), after.GetProperty("data").GetProperty("classes").GetRawText());
    }
}
