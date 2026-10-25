using System;
using System.Collections.Generic;
using EdS.Data.Json.Text;
using EdS.Data.Characters;
using EdS.Data.Validation;

namespace EdS.Data.Json
{
    /// <summary>
    /// Sous-lot 12.2 : lecture stricte de la progression du personnage persistant (<c>kind: "progression"</c>, <c>data/v0/world/progression.json</c>).
    /// Elle ne vérifie que la forme ; le reste va au validateur unique <see cref="ProgressionValidator"/>, contre les données du jeu.
    /// </summary>
    public static class ProgressionJsonReader
    {
        /// <summary>Chemin de la progression dans un dossier de données (<c>data/v0</c>).</summary>
        public static string PathIn(string dataDirectory) => System.IO.Path.Combine(dataDirectory, "world", "progression.json");

        public static ProgressionLoadResult ReadFile(string path, GameData data)
        {
            var report = new ValidationReport();
            return Read(JsonInput.ReadFile(path, report), data, report);
        }

        public static ProgressionLoadResult Read(string json, GameData data)
        {
            var report = new ValidationReport();
            return Read(JsonInput.Utf8(json, report), data, report);
        }

        private static ProgressionLoadResult Read(byte[]? json, GameData data, ValidationReport report)
        {
            var document = new ProgressionDocument();
            if (json is null) return ProgressionValidator.Validate(document, data, report);
            JsonHeader header = JsonInput.Parse(json, report, (input, element) => document.Data = ReadData(input, element, "$.data"));
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            return ProgressionValidator.Validate(document, data, report);
        }

        private static ProgressionData? ReadData(JsonInput input, JsonElement element, string path)
        {
            var d = new ProgressionData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["maxLevel"] = (v, p) => d.MaxLevel = input.Int32(v, p),
                ["xpToLevel"] = (v, p) => d.XpToLevel = ReadLongs(input, v, p),
                ["killXpPerHealth"] = (v, p) => d.KillXpPerHealth = input.Int64(v, p),
                ["classes"] = (v, p) => d.Classes = input.ReadLimitedList(v, p, 64, n => $"{n} classes, au plus 64.", (e, ip) => ReadClass(input, e, ip)),
            });
            return ok ? d : null;
        }

        private static List<long?>? ReadLongs(JsonInput input, JsonElement element, string path)
        {
            if (!input.RequireKind(element, JsonValueKind.Array, path, "liste d'entiers")) return null;
            var list = new List<long?>();
            int i = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (i >= 1000) { input.Report.Add(path, ValidationCode.LimitExceeded, "au plus 1000 niveaux."); return null; }
                list.Add(input.Int64(item, $"{path}[{i++}]"));
            }
            return list;
        }

        private static ClassProgressionData? ReadClass(JsonInput input, JsonElement element, string path)
        {
            var c = new ClassProgressionData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["class"] = (v, p) => c.Class = input.Text(v, p),
                ["attributes"] = (v, p) => c.Attributes = input.ReadLimitedList(v, p, 1000, n => $"{n} niveaux, au plus 1000.",
                    (e, ip) => ScenarioJsonReader.ReadIntegerMap(input, e, ip)),
                ["startActiveSkills"] = (v, p) => c.StartActiveSkills = input.ReadList(v, p, (e, ip) => input.Text(e, ip)),
                ["startPassiveSkills"] = (v, p) => c.StartPassiveSkills = input.ReadList(v, p, (e, ip) => input.Text(e, ip)),
                ["skillUnlocks"] = (v, p) => c.SkillUnlocks = input.ReadLimitedList(v, p, ProgressionValidator.MaxSkillUnlocks,
                    n => $"{n} compétences gagnées, au plus {ProgressionValidator.MaxSkillUnlocks}.", (e, ip) => ReadUnlock(input, e, ip)),
            });
            return ok ? c : null;
        }

        private static SkillUnlockData? ReadUnlock(JsonInput input, JsonElement element, string path)
        {
            var u = new SkillUnlockData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["level"] = (v, p) => u.Level = input.Int32(v, p),
                ["skill"] = (v, p) => u.Skill = input.Text(v, p),
            });
            return ok ? u : null;
        }
    }
}
