using System.Text.Json;
using EdS.Data.Classes;
using EdS.Data.Validation;
using EdS.Domain.Builds;
using EdS.Domain.Numerics;

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
        return Read(JsonInput.ReadFile(path, report), progression, report);
    }

    public static ClassLoadResult Read(string json, ProgressionRules progression)
    {
        var report = new ValidationReport();
        return Read(JsonInput.Utf8(json, report), progression, report);
    }

    private static ClassLoadResult Read(byte[]? json, ProgressionRules progression, ValidationReport report)
    {
        if (json is null) return ClassValidator.Validate(new ClassDocument(), progression, report);
        var document = new ClassDocument();
        JsonHeader header = JsonInput.Parse(json, report, (input, data) => document.Data = ReadClass(input, data, "$.data"));
        document.SchemaVersion = header.SchemaVersion;
        document.Kind = header.Kind;
        document.ContentVersion = header.ContentVersion;
        return ClassValidator.Validate(document, progression, report);
    }

    private static bool ReadFields(JsonInput input, JsonElement element, string path, IReadOnlyDictionary<string, Action<JsonElement, string>> fields) =>
        input.ReadFields(element, path, fields);

    private static List<T?>? ReadList<T>(JsonInput input, JsonElement element, string path, Func<JsonElement, string, T?> readItem) where T : class =>
        input.ReadList(element, path, readItem);

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
            ["triggers"] = (v, p) => talent.Triggers = TriggerJsonReader.ReadList(input, v, p),
            ["modifiers"] = (v, p) => talent.Modifiers = ModifierJsonReader.ReadList(input, v, p),
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
            ["combat"] = (v, p) => skill.Combat = ReadCombat(input, v, p),
            ["triggers"] = (v, p) => skill.Triggers = TriggerJsonReader.ReadList(input, v, p),
            ["modifiers"] = (v, p) => skill.Modifiers = ModifierJsonReader.ReadList(input, v, p),
        });
        return ok ? skill : null;
    }

    private static SkillCombatData? ReadCombat(JsonInput input, JsonElement element, string path)
    {
        var combat = new SkillCombatData();
        bool ok = ReadFields(input, element, path, new Dictionary<string, Action<JsonElement, string>>
        {
            ["powerType"] = (v, p) => combat.PowerType = Text(input, v, p),
            ["damageType"] = (v, p) => combat.DamageType = Text(input, v, p),
            ["coefficient"] = (v, p) => combat.Coefficient = input.TryReadDecimal(v, p, out Fixed c) ? c : null,
            ["enduranceCost"] = (v, p) => combat.EnduranceCost = Int(input, v, p),
            ["cooldownMs"] = (v, p) => combat.CooldownMs = input.TryReadInteger(v, p, out long n) ? n : null,
            ["targeting"] = (v, p) => combat.Targeting = Text(input, v, p),
            ["unblockable"] = (v, p) => combat.Unblockable = Bool(input, v, p),
            ["rampartChargeCost"] = (v, p) => combat.RampartChargeCost = Int(input, v, p),
            ["rampartChargeGain"] = (v, p) => combat.RampartChargeGain = Int(input, v, p),
            ["provisional"] = (v, p) => combat.Provisional = Bool(input, v, p),
            ["applies"] = (v, p) => combat.Applies = ReadList(input, v, p, (e, ip) => ReadApplication(input, e, ip)),
        });
        return ok ? combat : null;
    }

    /// <summary>Altération portée (lot 7.2), même forme dans les classes et les scénarios.</summary>
    internal static StatusApplicationData? ReadApplication(JsonInput input, JsonElement element, string path)
    {
        var application = new StatusApplicationData();
        bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
        {
            ["status"] = (v, p) => application.Status = input.Text(v, p),
            ["coefficient"] = (v, p) => application.Coefficient = input.Decimal(v, p),
            ["target"] = (v, p) => application.Target = input.Text(v, p),
        });
        return ok ? application : null;
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
