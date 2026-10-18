using System.Globalization;
using System.Text;
using System.Text.Json;
using EdS.Data.Builds;
using EdS.Simulation.Engine;

namespace EdS.Data.Json.Measures;

/// <summary>Sous-lot 8.6 : rapport des mesures (<c>eds report</c>), en texte ou en JSON, octet par octet reproductible.</summary>
public static class MeasureReport
{
    /// <summary>Version du format JSON du rapport (<c>kind: "measureReport"</c>).</summary>
    public const int FormatVersion = 1;

    private static string F(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    public static void WriteText(TextWriter w, GameData data, IReadOnlyList<MeasureScenario> scenarios, IReadOnlyList<ReferenceBuild> builds,
        IReadOnlyList<MeasureResult> results, IReadOnlyList<ObjectiveResult> objectives)
    {
        int profiles = results.Select(r => r.Profile).Distinct().Count();
        w.WriteLine($"Mesures des builds — valeurs provisoires (moteur {CombatEngine.Version}, règles {data.Rules.ContentVersion}) : " +
            $"{scenarios.Count} scénarios × {builds.Count} builds × {profiles} profils = {results.Count} combats");
        foreach (MeasureScenario scenario in scenarios)
        {
            w.WriteLine();
            w.WriteLine($"{scenario.Id} {scenario.Name} ({scenario.Setup.DurationMs / 1000m} s, graine {scenario.Setup.Seed})");
            w.WriteLine($"  {"build",-26} {"profil",-9} {"DPS",8} {"PV perdus",10} {"évités",8} {"survie",8} {"allié",8} {"blocs",6} {"parades",8} {"rupt.",6} {"compét.",8} {"ult.",5} {"refus",6} {"charge",7}");
            foreach (MeasureResult r in results.Where(r => r.Scenario == scenario.Id))
            {
                string survival = (r.Died ? "†" : "") + F(r.Survival);
                string ally = r.AllySurvival is decimal a ? (r.AllyDied == true ? "†" : "") + F(a) : "—";
                decimal load = Math.Max(r.WorkPeak, Math.Max(r.EventsPeak, r.QueuePeak));
                w.WriteLine($"  {r.Build,-26} {Measures.IdOf(r.Profile),-9} {F(r.Dps),8} {r.HealthLost,10} {r.Avoided,8} {survival,8} {ally,8} {r.Blocks,6} {r.PerfectParries,8} {r.GuardBreaks,6} {r.SkillsCast,8} {r.Ultimates,5} {r.Rejected,6} {F(load) + " %",7}");
            }
        }
        w.WriteLine();
        w.WriteLine($"Objectifs provisoires (profil {Measures.IdOf(Measures.ObjectiveProfile)} ; budgets : tous les profils)");
        foreach (ObjectiveResult o in objectives)
            w.WriteLine($"  {(o.Passed switch { true => "[OK]", false => "[ÉCHEC]", null => "[—]" }),-8} {o.Description} — {o.Detail}");
        w.WriteLine();
        w.WriteLine("Survie en secondes : instant de la mort (†) ; vivant à la fin, estimation au rythme de PV perdus observé (plafond : 10 fois la durée).");
        w.WriteLine("Évités : dégâts retirés par la garde, la parade et les réductions, absorbés par les boucliers et transférés à un protecteur.");
    }

    /// <summary>Rapport JSON : propriétés dans un ordre fixe, nombres en notation invariante, fin de ligne finale.</summary>
    public static byte[] ToJson(GameData data, IReadOnlyList<MeasureScenario> scenarios, IReadOnlyList<ReferenceBuild> builds,
        IReadOnlyList<MeasureResult> results, IReadOnlyList<ObjectiveResult> objectives)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteString("kind", "measureReport");
            w.WriteNumber("formatVersion", FormatVersion);
            w.WriteString("engineVersion", CombatEngine.Version);
            w.WriteStartObject("data");
            w.WriteString("rulesContentVersion", data.Rules.ContentVersion);
            w.WriteString("rulesFingerprint", data.Rules.Fingerprint);
            w.WriteStartArray("classes");
            foreach (var c in data.Classes)
            {
                w.WriteStartObject();
                w.WriteString("id", c.Definition.Id);
                w.WriteString("contentVersion", c.ContentVersion);
                w.WriteString("fingerprint", c.Fingerprint);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
            w.WriteString("objectiveProfile", Measures.IdOf(Measures.ObjectiveProfile));
            w.WriteStartArray("scenarios");
            foreach (MeasureScenario s in scenarios)
            {
                w.WriteStartObject();
                w.WriteString("id", s.Id);
                w.WriteString("name", s.Name);
                w.WriteNumber("durationMs", s.Setup.DurationMs);
                w.WriteNumber("seed", s.Setup.Seed);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteStartArray("builds");
            foreach (ReferenceBuild b in builds) w.WriteStringValue(b.Id);
            w.WriteEndArray();
            w.WriteStartArray("results");
            foreach (MeasureResult r in results)
            {
                w.WriteStartObject();
                w.WriteString("scenario", r.Scenario);
                w.WriteString("build", r.Build);
                w.WriteString("profile", Measures.IdOf(r.Profile));
                w.WriteNumber("durationMs", r.DurationMs);
                w.WriteBoolean("aborted", r.Aborted);
                w.WriteString("end", r.EndReason);
                w.WriteNumber("damageDealt", r.DamageDealt);
                w.WriteNumber("dps", r.Dps);
                w.WriteNumber("healthLost", r.HealthLost);
                w.WriteNumber("maxHealth", r.MaxHealth);
                w.WriteNumber("avoided", r.Avoided);
                w.WriteNumber("mitigated", r.Mitigated);
                w.WriteNumber("absorbed", r.Absorbed);
                w.WriteNumber("redirected", r.Redirected);
                w.WriteBoolean("died", r.Died);
                w.WriteNumber("survivalSeconds", r.Survival);
                if (r.AllySurvival is decimal a)
                {
                    w.WriteBoolean("allyDied", r.AllyDied == true);
                    w.WriteNumber("allySurvivalSeconds", a);
                }
                else
                {
                    w.WriteNull("allyDied");
                    w.WriteNull("allySurvivalSeconds");
                }
                w.WriteNumber("blocks", r.Blocks);
                w.WriteNumber("perfectParries", r.PerfectParries);
                w.WriteNumber("guardBreaks", r.GuardBreaks);
                w.WriteNumber("skillsCast", r.SkillsCast);
                w.WriteNumber("ultimates", r.Ultimates);
                w.WriteNumber("commandsRejected", r.Rejected);
                w.WriteStartObject("peaksPercent");
                w.WriteNumber("work", r.WorkPeak);
                w.WriteNumber("events", r.EventsPeak);
                w.WriteNumber("queue", r.QueuePeak);
                w.WriteEndObject();
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteStartArray("objectives");
            foreach (ObjectiveResult o in objectives)
            {
                w.WriteStartObject();
                w.WriteString("id", o.Id);
                w.WriteString("description", o.Description);
                if (o.Passed is bool passed) w.WriteBoolean("passed", passed);
                else w.WriteNull("passed");
                w.WriteString("detail", o.Detail);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        stream.WriteByte((byte)'\n');
        return stream.ToArray();
    }

    /// <summary>Nom du journal d'un combat mesuré : <c>&lt;scénario&gt;__&lt;build&gt;__&lt;profil&gt;.jsonl</c>.</summary>
    public static string LogFileName(MeasureScenario scenario, ReferenceBuild build, Simulation.Pilot.PilotProfile profile) =>
        $"{scenario.Name}__{build.Id}__{Measures.IdOf(profile)}.jsonl";

    internal static readonly Encoding Utf8 = new UTF8Encoding(false);
}
