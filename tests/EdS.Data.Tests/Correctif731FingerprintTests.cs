using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Correctif 7.3.1 (A73-M02) : l'écriture canonique des empreintes distingue un champ absent de l'identifiant valide <c>none</c>. Les
/// deux collisions de l'audit (filtre de statut d'un déclencheur, état accordé à l'expiration) donnent désormais des empreintes
/// différentes, et le rejeu d'un journal avec l'autre jeu de données est refusé avant toute simulation. Avant le correctif, les
/// en-têtes étaient identiques, le rejeu était lancé puis divergeait.
/// </summary>
[Trait("Category", "Data")]
[Trait("Category", "Regression")]
public class Correctif731FingerprintTests
{
    /// <summary>Altération valide nommée <c>none</c>, ajoutée aux deux jeux comparés (audit).</summary>
    private static JsonNode NoneStatus() => JsonNode.Parse("""
        { "id": "none", "category": "modifier", "durationMs": 1000, "stacking": "refresh", "resistance": "none", "damageTakenIncrease": 0.5 }
        """)!;

    private static GameData Data(Action<JsonObject>? rules = null, Action<JsonObject>? bastion = null)
    {
        string directory = CopyData((relative, text) =>
        {
            if (relative == "rules.json")
            {
                JsonObject root = JsonNode.Parse(text)!.AsObject();
                root["data"]!["statuses"]!.AsArray().Add(NoneStatus());
                rules?.Invoke(root);
                return root.ToJsonString();
            }
            if (relative == "classes/bastion.json" && bastion != null)
            {
                JsonObject root = JsonNode.Parse(text)!.AsObject();
                bastion(root);
                return root.ToJsonString();
            }
            return text;
        });
        try
        {
            GameDataLoadResult loaded = GameDataLoader.Load(directory);
            Assert.True(loaded.Data != null, string.Join("; ", loaded.Failures.SelectMany(f => f.Report.Errors.Select(e => e.ToString()))));
            return loaded.Data!;
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static void PassiveTrigger(JsonObject bastion, bool filterNone)
    {
        JsonObject skill = bastion["data"]!["skills"]!.AsArray().Select(s => s!.AsObject()).Single(s => s["id"]!.GetValue<string>() == "skill.bastion.test_passif_1");
        var trigger = JsonNode.Parse("""
            { "on": "onStateApplied", "role": "target", "effects": [ { "type": "gainResource", "resource": "resonance", "amount": 1 } ] }
            """)!.AsObject();
        if (filterNone) trigger["filter"] = JsonNode.Parse("""{ "status": "none" }""");
        skill["triggers"] = new JsonArray(trigger);
    }

    private static JsonNode FragilityAssailant(string target, params long[] atMs) => JsonNode.Parse($$"""
        { "id": "assaillant", "type": "assailant", "team": "cibles", "initiative": 0, "armor": 0, "magicResistance": 0, "target": "{{target}}",
          "firstAttackMs": {{atMs[0]}}, "intervalMs": {{(atMs.Length > 1 ? atMs[1] - atMs[0] : 60_000)}},
          "attacks": [ { "id": "tap", "rawDamage": 10, "damageType": "physique", "targeting": "enemy", "unblockable": false, "applies": [ { "status": "fragility" } ] } ] }
        """)!;

    private static List<string> Simulate(string scenarioJson, GameData data)
    {
        ScenarioLoadResult loaded = ScenarioJsonReader.Read(scenarioJson, data);
        Assert.True(loaded.Setup != null, string.Join("; ", loaded.Report.Errors.Select(e => e.ToString())));
        var log = new MemoryCombatLog();
        CombatEngine.Run(loaded.Setup!, log);
        return log.Lines.ToList();
    }

    private static void AssertRefusedBeforeSimulation(List<string> log, GameData other, string expectedProblem)
    {
        ReplayOutcome outcome = CombatLogReplayer.Replay(log, other);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Null(outcome.Comparison);   // aucune simulation lancée
        Assert.Contains(outcome.Problems, p => p.Contains(expectedProblem, StringComparison.Ordinal));
    }

    [Fact]
    public void Cas_A_filtre_de_statut_absent_ou_none()
    {
        GameData a = Data(bastion: b => PassiveTrigger(b, filterNone: false));
        GameData b = Data(bastion: b => PassiveTrigger(b, filterNone: true));
        string classA = a.Classes.Single().Fingerprint, classB = b.Classes.Single().Fingerprint;
        Assert.NotEqual(classA, classB);
        Assert.Equal(a.Rules.Fingerprint, b.Rules.Fingerprint);

        // Mannequin livré (passif 1 équipé) et un assaillant qui pose la Fragilité au tick 0 : la réaction ne joue qu'avec A.
        JsonObject scenario = JsonNode.Parse(MannequinJson)!.AsObject();
        DataOf(scenario)["combatants"]!.AsArray().Add(FragilityAssailant("bastion", 0));
        string json = scenario.ToJsonString();
        List<string> logA = Simulate(json, a), logB = Simulate(json, b);
        static int Reactions(List<string> log) => log.Count(l => l.Contains("\"event\":\"resourceGained\"") && l.Contains("\"depth\":1"));
        Assert.Equal((1, 0), (Reactions(logA), Reactions(logB)));
        Assert.NotEqual(logA[0], logB[0]);   // en-têtes complets différents

        AssertRefusedBeforeSimulation(logA, b, classB);
        AssertRefusedBeforeSimulation(logB, a, classA);
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(logA, a).Status);
    }

    [Fact]
    public void Cas_B_etat_accorde_a_l_expiration_absent_ou_none()
    {
        static JsonObject Fragility(JsonObject rules) => rules["data"]!["statuses"]!.AsArray().Select(s => s!.AsObject()).Single(s => s["id"]!.GetValue<string>() == "fragility");
        GameData a = Data();
        GameData b = Data(rules: r => Fragility(r)["grantsOnExpiry"] = "none");
        Assert.NotEqual(a.Rules.Fingerprint, b.Rules.Fingerprint);

        // Mannequin sans défense, Fragilité à 0 puis 6 500 ms sur 7 000 ms : B reçoit « none » à l'expiration (6 000 ms).
        string json = $$"""
            { "schemaVersion": 2, "kind": "scenario", "contentVersion": "0.1.0-test",
              "data": { "seed": 42, "durationMs": 7000, "commands": [],
                "combatants": [ { "id": "target", "type": "dummy", "team": "blue", "initiative": 0, "armor": 0, "magicResistance": 0 },
                  {{FragilityAssailant("target", 0, 6500).ToJsonString().Replace("\"cibles\"", "\"red\"")}} ] } }
            """;
        List<string> logA = Simulate(json, a), logB = Simulate(json, b);
        static long Damage(List<string> log) => log.Where(l => l.Contains("\"event\":\"damage\"")).Sum(l => JsonDocument.Parse(l).RootElement.GetProperty("amount").GetInt64());
        Assert.Equal((20L, 25L), (Damage(logA), Damage(logB)));
        Assert.Contains(logB, l => l.Contains("\"event\":\"stateApplied\"") && l.Contains("\"state\":\"none\""));
        Assert.NotEqual(logA[0], logB[0]);

        AssertRefusedBeforeSimulation(logA, b, b.Rules.Fingerprint);
        AssertRefusedBeforeSimulation(logB, a, a.Rules.Fingerprint);
    }

    [Fact]
    public void Ecriture_canonique_injective()
    {
        // Absent, présent « none », présent vide : trois lignes distinctes ; une liste à un élément « a,b » diffère de deux éléments.
        string Write(Action<CanonicalWriter> write)
        {
            var text = new StringBuilder();
            write(new CanonicalWriter(text));
            return text.ToString();
        }
        string[] forms = { Write(w => w.Optional("x", null)), Write(w => w.Optional("x", "none")), Write(w => w.Optional("x", "")) };
        Assert.Equal(3, forms.Distinct().Count());
        Assert.NotEqual(Write(w => w.List("l", new[] { "a,b" })), Write(w => w.List("l", new[] { "a", "b" })));
        Assert.NotEqual(Write(w => w.Value("v", "a\nb=c")), Write(w => { w.Value("v", "a"); }) + "b=c\n");
        Assert.Equal("x?\n", forms[0]);
        Assert.Equal("x=none\n", forms[1]);
    }

    [Fact]
    public void Journal_0_9_0_refuse_avant_simulation()
    {
        // Politique de compatibilité (correctif 7.3.1) : un journal du moteur 0.9.0 (contrat canonique 1) est refusé, pas migré.
        string[] old = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "logs-0.9.0", "mannequin.jsonl"));
        ReplayOutcome outcome = CombatLogReplayer.Replay(old, ShippedData);
        Assert.Equal(ReplayStatus.Refused, outcome.Status);
        Assert.Null(outcome.Comparison);
        Assert.Contains(outcome.Problems, p => p.Contains("0.9.0", StringComparison.Ordinal));
        Assert.Contains(outcome.Problems, p => p.Contains(ShippedData.Rules.Fingerprint, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("mannequin")]
    [InlineData("entrainement_garde_parade")]
    [InlineData("entrainement_rupture")]
    [InlineData("entrainement_alterations")]
    public void Journaux_de_reference_inchanges_hors_en_tete(string name)
    {
        // Les quatre journaux de référence de l'audit (0.9.0, figés au commit audité) et les journaux actuels ne diffèrent que par la
        // version du moteur et les empreintes de l'en-tête : toutes les lignes de combat sont identiques octet par octet.
        string[] old = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "logs-0.9.0", name + ".jsonl"));
        var log = new MemoryCombatLog();
        CombatEngine.Run(ScenarioJsonReader.ReadFile(ScenarioPath(name), ShippedData).Setup!, log);
        Assert.Equal(old.Skip(1), log.Lines.Skip(1));
        JsonObject Strip(string header)
        {
            JsonObject h = JsonNode.Parse(header)!.AsObject();
            Assert.True(h.Remove("engineVersion"));
            JsonObject data = h["data"]!.AsObject();
            data["rules"]!.AsObject().Remove("fingerprint");
            foreach (JsonNode? c in data["classes"]!.AsArray()) c!.AsObject().Remove("fingerprint");
            return h;
        }
        Assert.Equal(Strip(old[0]).ToJsonString(), Strip(log.Lines[0]).ToJsonString());
        Assert.Contains("\"engineVersion\":\"0.9.0\"", old[0]);
        Assert.Contains("\"engineVersion\":\"0.9.1\"", log.Lines[0]);
    }

    [Fact]
    public void Chaque_champ_facultatif_d_un_declencheur_change_la_forme_canonique()
    {
        // Variantes d'un même déclencheur différant par un seul champ facultatif : absent, puis chaque valeur possible (dont « none »
        // pour les identifiants libres). Toutes les formes canoniques sont distinctes.
        var effect = new TriggerEffect(TriggerEffectKind.ApplyStatus, TriggerTarget.EventSource, null, null, null, false, "bleed");
        var variants = new List<TriggerDefinition>();
        void Add(TriggerFilter? filter = null, TriggerRole? role = null, TriggerEffect? e = null) =>
            variants.Add(new TriggerDefinition(TriggerEvent.StateApplied, new[] { e ?? effect }, role, filter));
        Add();
        foreach (TriggerRole role in Enum.GetValues<TriggerRole>()) Add(role: role);
        foreach (string? status in new[] { "none", "fragility", "" }) Add(new TriggerFilter(status: status));
        foreach (StatusCategory category in Enum.GetValues<StatusCategory>()) Add(new TriggerFilter(category: category));
        foreach (TriggerResource resource in Enum.GetValues<TriggerResource>()) Add(new TriggerFilter(resource: resource));
        foreach (bool renewed in new[] { true, false }) Add(new TriggerFilter(renewed: renewed));
        foreach (DamageType type in Enum.GetValues<DamageType>()) Add(new TriggerFilter(damageType: type));
        Add(new TriggerFilter(skills: new[] { "none" }));
        Add(new TriggerFilter(skills: new[] { "a", "b" }));
        Add(e: new TriggerEffect(TriggerEffectKind.ApplyStatus, null, null, null, null, false, "bleed"));
        foreach (TriggerTarget target in Enum.GetValues<TriggerTarget>().Where(t => t != TriggerTarget.EventSource))   // EventSource : variante de base
            Add(e: new TriggerEffect(TriggerEffectKind.ApplyStatus, target, null, null, null, false, "bleed"));
        Add(e: new TriggerEffect(TriggerEffectKind.ApplyStatus, TriggerTarget.EventSource, null, null, null, false, "none"));
        Add(e: new TriggerEffect(TriggerEffectKind.ApplyStatus, TriggerTarget.EventSource, null, null, null, false, null));

        var forms = variants.Select(t =>
        {
            var text = new StringBuilder();
            Triggers.TriggerShape.AppendCanonical(text, "t", new[] { t });
            return text.ToString();
        }).ToList();
        Assert.Equal(forms.Count, forms.Distinct().Count());
    }
}
