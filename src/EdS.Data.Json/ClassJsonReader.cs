using System.Text.Json;
using EdS.Data.Classes;
using EdS.Data.Validation;
using EdS.Domain.Builds;

namespace EdS.Data.Json;

/// <summary>
/// Adaptateur System.Text.Json pour les fichiers de classe (ex. <c>bastion.json</c>), réservé aux outils .NET.
/// Il traduit le JSON en <see cref="ClassDocument"/> en signalant les erreurs de forme, puis délègue au validateur
/// commun <see cref="ClassValidator"/>. Les règles de progression (seuil des majeurs, niveaux…) viennent de <c>rules.json</c>.
/// </summary>
public static class ClassJsonReader
{
    public static ClassLoadResult ReadFile(string path, ProgressionRules progression)
    {
        var report = new ValidationReport();
        string? json = JsonInput.ReadFile(path, report);
        return json is null ? ClassValidator.Validate(new ClassDocument(), progression, report) : Read(json, progression, report);
    }

    public static ClassLoadResult Read(string json, ProgressionRules progression) => Read(json, progression, new ValidationReport());

    private static ClassLoadResult Read(string json, ProgressionRules progression, ValidationReport report)
    {
        var document = new ClassDocument();
        JsonHeader header = JsonInput.Parse(json, report, (input, data) => document.Data = ReadClass(input, data, "$.data"));
        document.SchemaVersion = header.SchemaVersion;
        document.Kind = header.Kind;
        document.ContentVersion = header.ContentVersion;
        return ClassValidator.Validate(document, progression, report);
    }

    /// <summary>Lit un objet dont les champs connus ont chacun un lecteur ; tout autre champ est signalé.</summary>
    private static bool ReadFields(JsonInput input, JsonElement element, string path, IReadOnlyDictionary<string, Action<JsonElement, string>> fields)
    {
        if (!input.RequireKind(element, JsonValueKind.Object, path, "objet")) return false;
        foreach (JsonProperty property in input.UniqueProperties(element, path))
        {
            string childPath = path + "." + property.Name;
            if (fields.TryGetValue(property.Name, out var read)) read(property.Value, childPath);
            else input.UnknownField(childPath, fields.Keys);
        }
        return true;
    }

    /// <summary>Lit un tableau d'objets ; un élément illisible reste null pour conserver les indices du fichier.</summary>
    private static List<T?>? ReadList<T>(JsonInput input, JsonElement element, string path, Func<JsonElement, string, T?> readItem) where T : class
    {
        if (!input.RequireKind(element, JsonValueKind.Array, path, "tableau")) return null;
        var list = new List<T?>();
        int index = 0;
        foreach (JsonElement item in element.EnumerateArray())
            list.Add(readItem(item, $"{path}[{index++}]"));
        return list;
    }

    private static string? Text(JsonInput input, JsonElement value, string path) =>
        input.TryReadString(value, path, out string? s) ? s : null;

    private static int? Int(JsonInput input, JsonElement value, string path) =>
        input.TryReadInt32(value, path, out int n) ? n : null;

    private static bool? Bool(JsonInput input, JsonElement value, string path) =>
        input.TryReadBoolean(value, path, out bool b) ? b : null;

    private static ClassData? ReadClass(JsonInput input, JsonElement element, string path)
    {
        var data = new ClassData();
        bool ok = ReadFields(input, element, path, new Dictionary<string, Action<JsonElement, string>>
        {
            ["id"] = (v, p) => data.Id = Text(input, v, p),
            ["name"] = (v, p) => data.Name = Text(input, v, p),
            ["trees"] = (v, p) => data.Trees = ReadList(input, v, p, (e, ip) => ReadTree(input, e, ip)),
            ["skills"] = (v, p) => data.Skills = ReadList(input, v, p, (e, ip) => ReadSkill(input, e, ip)),
        });
        return ok ? data : null;
    }

    private static TreeData? ReadTree(JsonInput input, JsonElement element, string path)
    {
        var tree = new TreeData();
        bool ok = ReadFields(input, element, path, new Dictionary<string, Action<JsonElement, string>>
        {
            ["id"] = (v, p) => tree.Id = Text(input, v, p),
            ["name"] = (v, p) => tree.Name = Text(input, v, p),
            ["talents"] = (v, p) => tree.Talents = ReadList(input, v, p, (e, ip) => ReadTalent(input, e, ip)),
        });
        return ok ? tree : null;
    }

    private static TalentData? ReadTalent(JsonInput input, JsonElement element, string path)
    {
        var talent = new TalentData();
        bool ok = ReadFields(input, element, path, new Dictionary<string, Action<JsonElement, string>>
        {
            ["id"] = (v, p) => talent.Id = Text(input, v, p),
            ["name"] = (v, p) => talent.Name = Text(input, v, p),
            ["maxRank"] = (v, p) => talent.MaxRank = Int(input, v, p),
            ["major"] = (v, p) => talent.Major = Bool(input, v, p),
            ["placeholder"] = (v, p) => talent.Placeholder = Bool(input, v, p),
            ["prerequisites"] = (v, p) => talent.Prerequisites = ReadList(input, v, p, (e, ip) => ReadRequirement(input, e, ip)),
        });
        return ok ? talent : null;
    }

    private static SkillData? ReadSkill(JsonInput input, JsonElement element, string path)
    {
        var skill = new SkillData();
        bool ok = ReadFields(input, element, path, new Dictionary<string, Action<JsonElement, string>>
        {
            ["id"] = (v, p) => skill.Id = Text(input, v, p),
            ["name"] = (v, p) => skill.Name = Text(input, v, p),
            ["kind"] = (v, p) => skill.Kind = Text(input, v, p),
            ["minLevel"] = (v, p) => skill.MinLevel = Int(input, v, p),
            ["placeholder"] = (v, p) => skill.Placeholder = Bool(input, v, p),
            ["talentRequirements"] = (v, p) => skill.TalentRequirements = ReadList(input, v, p, (e, ip) => ReadRequirement(input, e, ip)),
        });
        return ok ? skill : null;
    }

    private static RequirementData? ReadRequirement(JsonInput input, JsonElement element, string path)
    {
        var requirement = new RequirementData();
        bool ok = ReadFields(input, element, path, new Dictionary<string, Action<JsonElement, string>>
        {
            ["talent"] = (v, p) => requirement.Talent = Text(input, v, p),
            ["rank"] = (v, p) => requirement.Rank = Int(input, v, p),
        });
        return ok ? requirement : null;
    }
}
