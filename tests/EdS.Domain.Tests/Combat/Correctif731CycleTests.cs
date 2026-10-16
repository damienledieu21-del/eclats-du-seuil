using System.Diagnostics;
using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Tests.Builds;
using FsCheck.Xunit;

namespace EdS.Domain.Tests.Combat;

/// <summary>
/// Correctif 7.3.1 (A73-M03) : l'analyse statique des cycles calcule les composantes fortement connexes du graphe biparti
/// « déclencheur → type d'événement → déclencheur » (linéaire), au lieu d'une matrice d'atteignabilité parcourue depuis chaque
/// déclencheur (Θ(n³) en temps, Θ(n²) en mémoire). Avant le correctif, le graphe dense de l'audit coûtait environ 1 s à 1 024
/// déclencheurs et 8 fois plus à chaque doublement. Les alertes restent identiques (propriété contre l'ancien algorithme) ;
/// des plafonds globaux refusent les volumes au-delà de la capacité mesurée avant toute analyse.
/// </summary>
[Trait("Category", "Trigger")]
[Trait("Category", "Limits")]
public class Correctif731CycleTests
{
    private static readonly TriggerEffect Strike = TriggerEffect.Strike(TriggerTarget.EventSource, Fixed.One);

    /// <summary>Graphe dense de l'audit : n ripostes « impact subi → frappe la source », toutes mutuellement atteignables.</summary>
    private static NamedTrigger[] Dense(int n) => Enumerable.Range(0, n)
        .Select(i => new NamedTrigger($"skill.audit{i / 8}/{i % 8}", $"skills[{i / 8}].triggers[{i % 8}]", new TriggerDefinition(TriggerEvent.HitTaken, new[] { Strike })))
        .ToArray();

    [Theory]
    [InlineData(1024)]
    [InlineData(2048)]
    [InlineData(4096)]
    [InlineData(16384)]   // quatre fois le plafond global : la marge de l'algorithme, pas une taille admise
    public void Graphe_dense_de_l_audit_analyse_en_temps_lineaire(int n)
    {
        NamedTrigger[] graph = Dense(n);
        TriggerGraph.Cycles(Dense(8), null);   // compilation à la volée hors mesure
        var clock = Stopwatch.StartNew();
        IReadOnlyList<RuleViolation> warnings = TriggerGraph.Cycles(graph, null);
        clock.Stop();
        RuleViolation warning = Assert.Single(warnings);
        Assert.Equal("skills[0].triggers[0]", Assert.Single(warning.Fields));
        Assert.Contains($", … et {n - TriggerGraph.MaxLabelsPerWarning} autres)", warning.Message);
        Assert.InRange(warning.Message.Length, 0, 600);   // avertissement borné, quelle que soit la taille de la composante
        // Borne large (quelques millisecondes mesurées) : l'ancien algorithme dépassait la seconde dès 1 024 déclencheurs.
        Assert.True(clock.ElapsedMilliseconds <= 500, $"{n} déclencheurs : {clock.ElapsedMilliseconds} ms");
    }

    /// <summary>Ancien algorithme (lot 7.3), conservé comme oracle : atteignabilité complète par parcours depuis chaque nœud.</summary>
    private static List<(string Message, string Path)> Reference(IReadOnlyList<NamedTrigger> triggers, StatusCatalog? catalog)
    {
        int n = triggers.Count;
        var produced = triggers.Select(t => new HashSet<TriggerEvent>(t.Definition.ProducedEvents(catalog))).ToArray();
        var reach = new bool[n, n];
        for (int s = 0; s < n; s++)
        {
            var queue = new Queue<int>();
            queue.Enqueue(s);
            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                for (int v = 0; v < n; v++)
                    if (!reach[s, v] && produced[u].Contains(triggers[v].Definition.On))
                    {
                        reach[s, v] = true;
                        queue.Enqueue(v);
                    }
            }
        }
        var result = new List<(string, string)>();
        var assigned = new bool[n];
        for (int i = 0; i < n; i++)
        {
            if (assigned[i] || !reach[i, i]) continue;
            var members = new List<int>();
            for (int j = i; j < n; j++)
                if (!assigned[j] && (j == i || (reach[i, j] && reach[j, i])))
                {
                    members.Add(j);
                    assigned[j] = true;
                }
            string cycle = members.Count == 1 ? $"{triggers[i].Label} → {triggers[i].Label}" : string.Join(", ", members.Select(m => triggers[m].Label));
            result.Add(($"cycle statique de déclencheurs ({cycle}) :", triggers[i].Path));
        }
        return result;
    }

    private static readonly string[] Statuses = { "fragility", "bleed", "burn", "stun", "freeze", "guardBreak" };

    [Property(MaxTest = 500)]
    public bool Memes_alertes_que_l_ancien_algorithme(int seed)
    {
        // Graphes aléatoires d'au plus 8 déclencheurs par composante (sous le seuil de troncature des libellés), types et effets variés.
        var rng = new Random(seed);
        TriggerEvent[] kinds = Enum.GetValues<TriggerEvent>();
        var triggers = Enumerable.Range(0, rng.Next(1, 9)).Select(i =>
        {
            TriggerEffect effect = rng.Next(3) switch
            {
                0 => TriggerEffect.Strike(TriggerTarget.EventSource, Fixed.One, unblockable: rng.Next(2) == 0),
                1 => TriggerEffect.ApplyStatus(TriggerTarget.EventSource, Statuses[rng.Next(Statuses.Length)], null),
                _ => TriggerEffect.GainResource(TriggerResource.Resonance, Fixed.One),
            };
            return new NamedTrigger($"t{i}", $"p{i}", new TriggerDefinition(kinds[rng.Next(kinds.Length)], new[] { effect }));
        }).ToArray();
        StatusCatalog? catalog = rng.Next(2) == 0 ? StatusCatalog.Provisional : null;
        var expected = Reference(triggers, catalog);
        var actual = TriggerGraph.Cycles(triggers, catalog);
        return expected.Count == actual.Count
            && expected.Zip(actual).All(p => p.Second.Message.StartsWith(p.First.Message, StringComparison.Ordinal) && p.Second.Fields.Single() == p.First.Path);
    }

    [Fact]
    public void Alertes_par_composante_bornees_par_le_nombre_de_types()
    {
        // Chaque composante cyclique contient un type d'événement : au plus un avertissement par type, même avec beaucoup de
        // déclencheurs de toutes sortes.
        var rng = new Random(7);
        TriggerEvent[] kinds = Enum.GetValues<TriggerEvent>();
        var triggers = Enumerable.Range(0, 4096).Select(i => new NamedTrigger($"t{i}", $"p{i}",
            new TriggerDefinition(kinds[rng.Next(kinds.Length)], new[] { rng.Next(2) == 0 ? Strike : TriggerEffect.ApplyStatus(TriggerTarget.EventSource, "fragility", null) }))).ToArray();
        Assert.InRange(TriggerGraph.Cycles(triggers, StatusCatalog.Provisional).Count, 1, kinds.Length);
    }

    // --- Plafonds d'une classe -----------------------------------------------------------------

    /// <summary>Classe d'essai plus <paramref name="skills"/> passifs de <paramref name="perSkill"/> ripostes, et un passif de
    /// <paramref name="extra"/> ripostes si non nul.</summary>
    private static ClassDefinition WithTriggers(int skills, int perSkill, int extra = 0, int talentsPerTree = 1)
    {
        var trees = new[] { "alpha", "beta", "gamma" }.Select(k => new TalentTreeDefinition($"tree.essai.{k}", k,
            Enumerable.Range(0, talentsPerTree).Select(i => new TalentDefinition($"talent.essai.{k}.t{i}", "T", 1)).ToArray())).ToArray();
        SkillDefinition Passive(string id, int count) => new(id, "Audit", SkillKind.Passive,
            triggers: Enumerable.Range(0, count).Select(_ => new TriggerDefinition(TriggerEvent.HitTaken, new[] { Strike })));
        var list = FictionalClass.Skills.Concat(Enumerable.Range(0, skills).Select(i => Passive($"skill.essai.audit{i}", perSkill)));
        if (extra > 0) list = list.Append(Passive("skill.essai.extra", extra));
        return new ClassDefinition(FictionalClass.Id, "Essai", trees, list.ToArray());
    }

    private static IEnumerable<RuleViolation> Limits(ClassDefinition definition) =>
        definition.Validate(ProgressionRules.Provisional).Where(v => v.Kind == RuleViolationKind.LimitExceeded);

    [Fact]
    public void Plafond_de_declencheurs_d_une_classe_N_moins_1_N_N_plus_1()
    {
        Assert.Equal(512, ClassDefinition.MaxTriggers);
        Assert.Empty(Limits(WithTriggers(63, 8, extra: 7)));            // N − 1 = 511
        Assert.Empty(Limits(WithTriggers(64, 8)));                      // N = 512
        ClassDefinition over = WithTriggers(64, 8, extra: 1);           // N + 1 = 513
        RuleViolation violation = Assert.Single(over.Validate(ProgressionRules.Provisional));   // refus seul, sans autre contrôle
        Assert.Equal((RuleViolationKind.LimitExceeded, "skills"), (violation.Kind, Assert.Single(violation.Fields)));
        Assert.Contains("513 déclencheurs", violation.Message);
    }

    [Fact]
    public void Plafonds_de_competences_et_de_talents()
    {
        int base_ = FictionalClass.Skills.Count();
        Assert.Empty(Limits(WithTriggers(ClassDefinition.MaxSkills - base_, 0)));
        Assert.Contains(Limits(WithTriggers(ClassDefinition.MaxSkills - base_ + 1, 0)), v => v.Fields.Contains("skills"));
        Assert.DoesNotContain(Limits(WithTriggers(0, 0, talentsPerTree: 85)), v => v.Fields.Contains("trees"));    // 255 talents
        Assert.Contains(Limits(WithTriggers(0, 0, talentsPerTree: 86)), v => v.Fields.Contains("trees"));          // 258 talents
    }

    [Theory]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(512)]
    public void Classes_de_l_audit_refusees_rapidement(int skills)
    {
        // 1 024, 2 048 et 4 096 déclencheurs : refus par le plafond, avant l'analyse des cycles.
        ClassDefinition definition = WithTriggers(skills, 8);
        var clock = Stopwatch.StartNew();
        IReadOnlyList<RuleViolation> violations = definition.Validate(ProgressionRules.Provisional);
        clock.Stop();
        Assert.All(violations, v => Assert.Equal(RuleViolationKind.LimitExceeded, v.Kind));   // rien d'autre n'est examiné
        Assert.Contains(violations, v => v.Message.Contains($"{skills * 8} déclencheurs"));
        Assert.True(clock.ElapsedMilliseconds <= 500, $"{skills * 8} déclencheurs : refus en {clock.ElapsedMilliseconds} ms");
    }
}
