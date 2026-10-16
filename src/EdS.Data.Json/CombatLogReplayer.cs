using System.Text.Json;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;

namespace EdS.Data.Json;

public enum ReplayStatus
{
    /// <summary>Le combat rejoué produit exactement le même journal.</summary>
    Identical = 0,

    /// <summary>Rejeu refusé avant simulation : moteur, format ou données différents de ceux du journal.</summary>
    Refused = 1,

    /// <summary>Le rejeu a eu lieu mais le journal produit diffère.</summary>
    Diverged = 2,

    /// <summary>Journal illisible (en-tête absent ou invalide).</summary>
    Invalid = 3,
}

public sealed class ReplayOutcome
{
    public ReplayStatus Status { get; }

    /// <summary>Raisons d'un refus ou d'un journal invalide.</summary>
    public IReadOnlyList<string> Problems { get; }

    /// <summary>Comparaison ligne à ligne, quand le rejeu a eu lieu.</summary>
    public ReplayComparison? Comparison { get; }

    internal ReplayOutcome(ReplayStatus status, IReadOnlyList<string> problems, ReplayComparison? comparison = null)
    {
        Status = status;
        Problems = problems;
        Comparison = comparison;
    }
}

/// <summary>
/// Rejeu d'un journal : lit l'en-tête, <b>vérifie d'abord</b> que le moteur, le format, la durée du tick et les
/// empreintes des données actuelles sont ceux du journal (sinon le journal n'est pas réputé reproductible et le rejeu
/// est refusé), puis rejoue le scénario de l'en-tête et compare les journaux octet par octet.
/// </summary>
public static class CombatLogReplayer
{
    public static ReplayOutcome ReplayFile(string path, GameData data)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllText(path).Split('\n');
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new ReplayOutcome(ReplayStatus.Invalid, [$"impossible de lire « {path} » : {e.Message}"]);
        }
        // Un journal se termine par « \n » : la dernière ligne vide n'en est pas une.
        int count = lines.Length > 0 && lines[^1].Length == 0 ? lines.Length - 1 : lines.Length;
        return Replay(lines.Take(count).ToArray(), data);
    }

    public static ReplayOutcome Replay(IReadOnlyList<string> lines, GameData data)
    {
        if (lines.Count == 0) return new ReplayOutcome(ReplayStatus.Invalid, ["journal vide."]);
        var report = new ValidationReport();
        LoggedHeader? header = ReadHeader(lines[0], report);
        if (header is null || !report.IsValid)
            return new ReplayOutcome(ReplayStatus.Invalid, report.Errors.Select(e => "en-tête " + e).ToArray());

        IReadOnlyList<string> problems = CombatReplay.CheckIdentity(header.EngineVersion, header.FormatVersion, header.TickMs,
            header.Identity, data.Identity, data.Rules.Combat.TickMilliseconds);
        if (problems.Count > 0) return new ReplayOutcome(ReplayStatus.Refused, problems);

        ScenarioLoadResult scenario = ScenarioValidator.ValidateData(header.Scenario, data, "$.scenario", report);
        if (scenario.Setup is null)
            return new ReplayOutcome(ReplayStatus.Invalid, report.Errors.Select(e => "en-tête " + e).ToArray());

        var replayed = new MemoryCombatLog();
        CombatEngine.Run(scenario.Setup, replayed);
        ReplayComparison comparison = CombatReplay.Compare(lines, replayed.Lines);
        return new ReplayOutcome(comparison.Identical ? ReplayStatus.Identical : ReplayStatus.Diverged, [], comparison);
    }

    private sealed class LoggedHeader
    {
        public int FormatVersion;
        public string EngineVersion = string.Empty;
        public int TickMs;
        public CombatDataIdentity Identity = null!;
        public ScenarioData? Scenario;
    }

    private static LoggedHeader? ReadHeader(string line, ValidationReport report)
    {
        LoggedHeader? result = null;
        JsonInput.ParseDocument(line, report, (input, root) =>
        {
            var header = new LoggedHeader();
            string? type = null, format = null, engine = null;
            int? formatVersion = null, tickMs = null;
            DataFileIdentity? rules = null;
            List<DataFileIdentity?>? classes = null;
            bool hasScenario = false;
            bool ok = input.ReadFields(root, "$", new Dictionary<string, Action<JsonElement, string>>
            {
                ["type"] = (v, p) => type = input.Text(v, p),
                ["format"] = (v, p) => format = input.Text(v, p),
                ["formatVersion"] = (v, p) => formatVersion = input.Int32(v, p),
                ["engineVersion"] = (v, p) => engine = input.Text(v, p),
                ["tickMs"] = (v, p) => tickMs = input.Int32(v, p),
                ["data"] = (v, p) => input.ReadFields(v, p, new Dictionary<string, Action<JsonElement, string>>
                {
                    ["rules"] = (rv, rp) => rules = ReadIdentity(input, rv, rp, "rules"),
                    ["classes"] = (cv, cp) => classes = input.ReadList(cv, cp, (e, ip) => ReadIdentity(input, e, ip, null)),
                }),
                ["scenario"] = (v, p) =>
                {
                    hasScenario = true;
                    header.Scenario = ScenarioJsonReader.ReadData(input, v, p);
                },
            });
            if (!ok) return;
            if (type != "header" || format != CombatEngine.LogFormat)
                report.Add("$.type", ValidationCode.WrongKind, $"première ligne attendue : en-tête « {CombatEngine.LogFormat} ».");
            if (formatVersion is null || engine is null || tickMs is null || rules is null || classes is null || classes.Any(c => c is null) || !hasScenario)
                report.Add("$", ValidationCode.MissingField, "en-tête incomplet (formatVersion, engineVersion, tickMs, data.rules, data.classes, scenario).");
            if (!report.IsValid) return;
            header.FormatVersion = formatVersion!.Value;
            header.EngineVersion = engine!;
            header.TickMs = tickMs!.Value;
            header.Identity = new CombatDataIdentity(rules!, classes!.Select(c => c!));
            result = header;
        });
        return result;
    }

    private static DataFileIdentity? ReadIdentity(JsonInput input, JsonElement element, string path, string? fixedId)
    {
        string? id = fixedId, contentVersion = null, fingerprint = null;
        var fields = new Dictionary<string, Action<JsonElement, string>>
        {
            ["contentVersion"] = (v, p) => contentVersion = input.Text(v, p),
            ["fingerprint"] = (v, p) => fingerprint = input.Text(v, p),
        };
        if (fixedId is null) fields["id"] = (v, p) => id = input.Text(v, p);
        if (!input.ReadFields(element, path, fields) || id is null || contentVersion is null || fingerprint is null) return null;
        return new DataFileIdentity(id, contentVersion, fingerprint);
    }
}
