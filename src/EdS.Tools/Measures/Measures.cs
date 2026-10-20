using System.Collections.ObjectModel;
using System.Globalization;
using EdS.Data;
using EdS.Data.Builds;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using EdS.Simulation.Pilot;

namespace EdS.Tools.Measures;

/// <summary>Scénario de mesure (sous-lot 8.6) : <c>s&lt;n&gt;_&lt;nom&gt;.json</c>, identifiant « S&lt;n&gt; ».</summary>
public sealed class MeasureScenario
{
    public string Id { get; }
    public string Name { get; }
    public CombatSetup Setup { get; }

    public MeasureScenario(string id, string name, CombatSetup setup)
    {
        Id = id;
        Name = name;
        Setup = setup;
    }
}

/// <summary>
/// Mesures d'un combat piloté (sous-lot 8.6), pour le personnage mesuré <c>bastion</c> et, s'il y en a un, son premier allié. Les
/// compteurs viennent du résultat du moteur (<see cref="CombatantResult"/>, X10) ; les pics de charge sont rapportés aux budgets.
/// </summary>
public sealed class MeasureResult
{
    public string Scenario { get; init; } = string.Empty;
    public string ScenarioName { get; init; } = string.Empty;
    public string Build { get; init; } = string.Empty;
    public PilotProfile Profile { get; init; }
    public long DurationMs { get; init; }

    /// <summary>Combat interrompu par une limite du moteur : mesures partielles, de diagnostic seulement.</summary>
    public bool Aborted { get; init; }
    public string EndReason { get; init; } = string.Empty;
    public long DamageDealt { get; init; }

    /// <summary>Dégâts infligés par seconde de vie du Bastion.</summary>
    public decimal Dps { get; init; }

    /// <summary>PV perdus : PV maximaux − PV restants (aucun soin au lot 8 ; l'excédent du coup fatal n'y figure pas).</summary>
    public long HealthLost { get; init; }
    public long MaxHealth { get; init; }

    /// <summary>Dégâts évités : atténués par la défense (garde, parade, réductions) + absorbés par ses boucliers + transférés à un protecteur.</summary>
    public long Avoided { get; init; }
    public long Mitigated { get; init; }
    public long Absorbed { get; init; }
    public long Transferred { get; init; }

    /// <summary>Parts des dégâts d'un allié reçues comme protecteur (avant sa garde et ses boucliers).</summary>
    public long Redirected { get; init; }
    public bool Died { get; init; }

    /// <summary>
    /// Survie en secondes : instant de la mort ; vivant à la fin, temps estimé au rythme de PV perdus observé (durée × PV max / PV perdus),
    /// plafonné à 10 fois la durée.
    /// </summary>
    public decimal Survival { get; init; }
    public bool? AllyDied { get; init; }
    public decimal? AllySurvival { get; init; }
    public long Blocks { get; init; }
    public long PerfectParries { get; init; }
    public long GuardBreaks { get; init; }
    public long SkillsCast { get; init; }
    public long Ultimates { get; init; }
    public long Rejected { get; init; }

    /// <summary>Pics de charge en pourcentage des budgets (travail et lignes par tick, file).</summary>
    public decimal WorkPeak { get; init; }
    public decimal EventsPeak { get; init; }
    public decimal QueuePeak { get; init; }
}

/// <summary>Verdict d'un objectif provisoire de la feuille de route.</summary>
public sealed class ObjectiveResult
{
    public string Id { get; }
    public string Description { get; }

    /// <summary>Vrai, faux, ou null si les combats nécessaires n'ont pas été mesurés.</summary>
    public bool? Passed { get; }
    public string Detail { get; }

    public ObjectiveResult(string id, string description, bool? passed, string detail)
    {
        Id = id;
        Description = description;
        Passed = passed;
        Detail = detail;
    }
}

/// <summary>
/// Sous-lot 8.6 : mesures des builds de référence. Chaque scénario de mesure est joué pour chaque build et chaque profil : le build du
/// personnage <c>bastion</c> est remplacé, le pilote (<see cref="CombatPilot"/>) joue le combat, puis le combat complété de ses
/// commandes est rejoué et vérifié (<see cref="PilotedCombat"/>).
/// </summary>
public static class Measures
{
    /// <summary>Identifiant du personnage mesuré dans un scénario de mesure.</summary>
    public const string MeasuredId = "bastion";

    /// <summary>Profil des objectifs de gameplay (les budgets valent pour tous les profils).</summary>
    public const PilotProfile ObjectiveProfile = PilotProfile.Standard;

    /// <summary>Identifiants des profils (table figée : revue Codex de la PR #10).</summary>
    public static readonly IReadOnlyDictionary<string, PilotProfile> Profiles = new ReadOnlyDictionary<string, PilotProfile>(
        new Dictionary<string, PilotProfile>(StringComparer.Ordinal)
        {
            ["beginner"] = PilotProfile.Beginner,
            ["standard"] = PilotProfile.Standard,
            ["expert"] = PilotProfile.Expert,
        });

    public static string IdOf(PilotProfile profile) => Profiles.First(p => p.Value == profile).Key;

    /// <summary>
    /// Scénarios de mesure d'un dossier (<c>s&lt;n&gt;_*.json</c>, triés), lus avec les données ; erreurs de lecture signalées. Deux
    /// fichiers au même numéro (<c>s1_a.json</c>, <c>s1_b.json</c>) sont refusés (<see cref="ArgumentException"/>, revue Codex de la PR #10).
    /// </summary>
    public static IReadOnlyList<MeasureScenario> LoadScenarios(string directory, GameData data, List<(string Path, ScenarioLoadResult Result)> failures)
    {
        var scenarios = new List<MeasureScenario>();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        string[] files = Directory.GetFiles(directory, "s*.json");
        Array.Sort(files, StringComparer.Ordinal);
        foreach (string file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            int underscore = name.IndexOf('_');
            if (underscore < 2 || !name[1..underscore].All(char.IsAsciiDigit)) continue;
            string id = "S" + name[1..underscore];
            if (!names.TryAdd(id, name))
                throw new ArgumentException($"Numéro de scénario de mesure en double : {id} ({names[id]}.json et {name}.json).");
            ScenarioLoadResult loaded = ScenarioJsonReader.ReadFile(file, data);
            if (loaded.Setup is null) failures.Add((file, loaded));
            else scenarios.Add(new MeasureScenario(id, name, loaded.Setup));
        }
        return scenarios;
    }

    /// <summary>Le scénario avec le build <paramref name="build"/> pour le personnage mesuré.</summary>
    public static CombatSetup WithBuild(CombatSetup setup, ReferenceBuild build)
    {
        var bastion = setup.Combatants.OfType<CharacterSetup>().SingleOrDefault(c => c.Id == MeasuredId)
            ?? throw new ArgumentException($"Aucun personnage « {MeasuredId} » dans le scénario.", nameof(setup));
        if (bastion.Class.Id != build.Build.ClassId)
            throw new ArgumentException($"Build « {build.Id} » de la classe « {build.Build.ClassId} », personnage mesuré de la classe « {bastion.Class.Id} ».", nameof(build));
        var combatants = setup.Combatants.Select(c => c == bastion ? new CharacterSetup(bastion.Id, bastion.Team, bastion.Class, build.Build, bastion.Initiative, bastion.Placement, bastion.ArrivalMs) : c);
        return new CombatSetup(setup.Seed, setup.DurationMs, setup.Stats, setup.Damage, setup.Progression, setup.Combat, combatants, setup.Commands, setup.Data, setup.Space);
    }

    /// <summary>Joue un combat mesuré et calcule ses mesures ; <paramref name="log"/> reçoit le journal du combat complété.</summary>
    public static MeasureResult Measure(MeasureScenario scenario, ReferenceBuild build, PilotProfile profile, ICombatLog? log = null)
    {
        CombatSetup setup = WithBuild(scenario.Setup, build);
        PilotedCombat combat = PilotedCombat.Run(setup, MeasuredId, profile, log ?? NullCombatLog.Instance);
        CombatResult result = combat.Result;
        CombatantResult me = result[MeasuredId];
        CharacterSetup bastion = setup.Combatants.OfType<CharacterSetup>().Single(c => c.Id == MeasuredId);
        CombatantResult? ally = setup.Combatants.OfType<CharacterSetup>().Where(c => c.Team == bastion.Team && c.Id != MeasuredId)
            .Select(c => result[c.Id]).FirstOrDefault();
        long tickMs = setup.Combat.TickMilliseconds;
        long maxHealth = MaxHealthOf(me);
        long aliveMs = me.DeathTick is long death ? death * tickMs : result.DurationMs;
        CombatRules limits = setup.Combat;
        return new MeasureResult
        {
            Scenario = scenario.Id,
            ScenarioName = scenario.Name,
            Build = build.Id,
            Profile = profile,
            DurationMs = result.DurationMs,
            Aborted = result.EndReason == CombatEndReason.Aborted,
            EndReason = result.AbortReason ?? result.EndReason.ToString(),
            DamageDealt = me.DamageDealt,
            Dps = aliveMs == 0 ? 0m : Math.Round(me.DamageDealt * 1000m / aliveMs, 2),
            HealthLost = maxHealth - (me.Health ?? 0),
            MaxHealth = maxHealth,
            Mitigated = me.DamageMitigated,
            Absorbed = me.DamageAbsorbed,
            Transferred = me.DamageTransferred,
            Avoided = me.DamageMitigated + me.DamageAbsorbed + me.DamageTransferred,
            Redirected = me.DamageRedirected,
            Died = me.IsDead,
            Survival = SurvivalSeconds(me, maxHealth, result.DurationMs, tickMs),
            AllyDied = ally?.IsDead,
            AllySurvival = ally is null ? null : SurvivalSeconds(ally, MaxHealthOf(ally), result.DurationMs, tickMs),
            Blocks = me.Blocks,
            PerfectParries = me.PerfectParries,
            GuardBreaks = me.GuardBreaks,
            SkillsCast = me.SkillsCast,
            Ultimates = me.UltimatesCast,
            Rejected = me.CommandsRejected,
            WorkPeak = Percent(result.Peaks.WorkPerTick, limits.MaxWorkPerTick),
            EventsPeak = Percent(result.Peaks.EventsPerTick, limits.MaxEventsPerTick),
            QueuePeak = Percent(result.Peaks.PendingEvents, limits.MaxPendingEvents),
        };
    }

    private static long MaxHealthOf(CombatantResult r) => r.MaxHealth ?? 0;

    /// <summary>Survie en secondes (voir <see cref="MeasureResult.Survival"/>).</summary>
    public static decimal SurvivalSeconds(CombatantResult r, long maxHealth, long durationMs, long tickMs)
    {
        if (r.DeathTick is long death) return death * tickMs / 1000m;
        decimal cap = durationMs * 10 / 1000m;
        long lost = maxHealth - (r.Health ?? 0);
        if (lost <= 0) return cap;
        return Math.Min(cap, Math.Round(durationMs / 1000m * maxHealth / lost, 1));
    }

    private static decimal Percent(long value, long limit) => limit <= 0 ? 0m : Math.Round(value * 100m / limit, 1);

    /// <summary>Tous les combats : chaque scénario, chaque build, chaque profil (dans cet ordre).</summary>
    public static IReadOnlyList<MeasureResult> RunAll(IEnumerable<MeasureScenario> scenarios, IReadOnlyList<ReferenceBuild> builds,
        IReadOnlyList<PilotProfile> profiles, Func<MeasureScenario, ReferenceBuild, PilotProfile, ICombatLog?>? logs = null)
    {
        var results = new List<MeasureResult>();
        foreach (MeasureScenario scenario in scenarios)
            foreach (ReferenceBuild build in builds)
                foreach (PilotProfile profile in profiles)
                    results.Add(Measure(scenario, build, profile, logs?.Invoke(scenario, build, profile)));
        return results;
    }

    public const string Rempart = "build.bastion.rempart";
    public const string Contrecoup = "build.bastion.contrecoup";
    public const string Gardien = "build.bastion.gardien";

    /// <summary>
    /// Objectifs provisoires de la feuille de route, au profil standard : S1 écart de DPS des trois builds de référence ≤ 15 % ; S2 DPS du
    /// Contrecoup ≥ +25 % sur le Rempart ; S2 et S5 survie et dégâts évités du Rempart ≥ +25 % sur le Contrecoup ; S4 allié protégé au moins
    /// 1,5 fois plus longtemps avec le Gardien qu'avec chacun des deux autres ; tous les combats sous 50 % de chaque budget du moteur.
    /// </summary>
    /// <remarks>
    /// Revue Codex de la PR #10 : un combat interrompu (mesures de diagnostic) n'entre dans aucun objectif de gameplay, qui devient alors
    /// « non mesuré » ; il fait échouer l'objectif des budgets (une limite du moteur a été atteinte).
    /// </remarks>
    public static IReadOnlyList<ObjectiveResult> Objectives(IReadOnlyList<MeasureResult> results)
    {
        MeasureResult? Of(string scenario, string build) =>
            results.FirstOrDefault(r => r.Scenario == scenario && r.Build == build && r.Profile == ObjectiveProfile && !r.Aborted);
        static string F(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        static string Pct(decimal ratio) => (ratio >= 1 ? "+" : "") + ((ratio - 1) * 100).ToString("0.#", CultureInfo.InvariantCulture) + " %";
        var objectives = new List<ObjectiveResult>();

        var s1 = new[] { Rempart, Contrecoup, Gardien }.Select(b => Of("S1", b)).ToList();
        if (s1.Any(r => r is null)) objectives.Add(new ObjectiveResult("S1.dps", "S1 : écart de DPS des trois builds de référence ≤ 15 %", null, "non mesuré"));
        else
        {
            decimal max = s1.Max(r => r!.Dps), min = s1.Min(r => r!.Dps);
            decimal spread = max == 0 ? 0 : (max - min) / max;
            objectives.Add(new ObjectiveResult("S1.dps", "S1 : écart de DPS des trois builds de référence ≤ 15 %", spread <= 0.15m,
                $"écart {F(spread * 100)} % (Rempart {F(s1[0]!.Dps)}, Contrecoup {F(s1[1]!.Dps)}, Gardien {F(s1[2]!.Dps)})"));
        }

        void Ratio(string id, string description, decimal? numerator, decimal? denominator, decimal threshold, string label)
        {
            if (numerator is null || denominator is null) objectives.Add(new ObjectiveResult(id, description, null, "non mesuré"));
            else
            {
                decimal ratio = denominator == 0 ? (numerator > 0 ? decimal.MaxValue : 1) : numerator.Value / denominator.Value;
                objectives.Add(new ObjectiveResult(id, description, ratio >= threshold,
                    $"{label} : {F(numerator.Value)} contre {F(denominator.Value)}, soit {(ratio == decimal.MaxValue ? "∞" : Pct(ratio))}"));
            }
        }

        Ratio("S2.dps", "S2 : DPS du Contrecoup ≥ +25 % sur le Rempart", Of("S2", Contrecoup)?.Dps, Of("S2", Rempart)?.Dps, 1.25m, "DPS");
        foreach (string s in new[] { "S2", "S5" })
        {
            Ratio($"{s}.survie", $"{s} : survie du Rempart ≥ +25 % sur le Contrecoup", Of(s, Rempart)?.Survival, Of(s, Contrecoup)?.Survival, 1.25m, "survie (s)");
            Ratio($"{s}.evites", $"{s} : dégâts évités du Rempart ≥ +25 % sur le Contrecoup", Of(s, Rempart)?.Avoided, Of(s, Contrecoup)?.Avoided, 1.25m, "dégâts évités");
        }
        decimal? gardien = Of("S4", Gardien)?.AllySurvival;
        decimal? others = new[] { Of("S4", Rempart)?.AllySurvival, Of("S4", Contrecoup)?.AllySurvival } is var o && o.All(v => v != null) ? o.Max() : null;
        Ratio("S4.allie", "S4 : allié protégé ≥ 1,5 fois plus longtemps avec le Gardien", gardien, others, 1.5m, "survie de l'allié (s, Gardien contre le meilleur des deux autres)");

        MeasureResult? aborted = results.FirstOrDefault(r => r.Aborted);
        if (results.Count == 0) objectives.Add(new ObjectiveResult("budgets", "Tous les combats < 50 % de chaque budget du moteur", null, "non mesuré"));
        else if (aborted != null)
            objectives.Add(new ObjectiveResult("budgets", "Tous les combats < 50 % de chaque budget du moteur", false,
                $"combat interrompu ({aborted.Scenario}, {aborted.Build}, {IdOf(aborted.Profile)} : {aborted.EndReason}), {results.Count(r => r.Aborted)} au total"));
        else
        {
            MeasureResult worst = results.OrderByDescending(r => Math.Max(r.WorkPeak, Math.Max(r.EventsPeak, r.QueuePeak))).First();
            decimal peak = Math.Max(worst.WorkPeak, Math.Max(worst.EventsPeak, worst.QueuePeak));
            objectives.Add(new ObjectiveResult("budgets", "Tous les combats < 50 % de chaque budget du moteur", peak < 50m,
                $"pic maximal {F(peak)} % ({worst.Scenario}, {worst.Build}, {IdOf(worst.Profile)} : travail {F(worst.WorkPeak)} %, lignes {F(worst.EventsPeak)} %, file {F(worst.QueuePeak)} %)"));
        }
        return objectives;
    }
}
