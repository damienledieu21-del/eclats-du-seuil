using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using EdS.Data.Builds;
using EdS.Data.Json.Text;
using EdS.Data.Validation;

namespace EdS.Data.Json
{
    /// <summary>
    /// Adaptateur System.Text.Json pour les builds de référence (<c>kind: "builds"</c>, sous-lot 8.5). Il ne vérifie que la forme ; tout
    /// le reste est délégué au validateur commun <see cref="BuildSetValidator"/>. Le build lui-même a la forme de celui d'un scénario.
    /// </summary>
    public static class BuildSetJsonReader
    {
        public static BuildSetLoadResult ReadFile(string path, GameData data)
        {
            var report = new ValidationReport();
            return Read(JsonInput.ReadFile(path, report), data, report);
        }

        public static BuildSetLoadResult Read(string json, GameData data)
        {
            var report = new ValidationReport();
            return Read(JsonInput.Utf8(json, report), data, report);
        }

        private static BuildSetLoadResult Read(byte[]? json, GameData data, ValidationReport report)
        {
            var document = new BuildSetDocument();
            if (json is null) return BuildSetValidator.Validate(document, data, report);
            JsonHeader header = JsonInput.Parse(json, report, (input, element) => document.Data = ReadData(input, element, "$.data"));
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            return BuildSetValidator.Validate(document, data, report);
        }

        private static BuildSetData? ReadData(JsonInput input, JsonElement element, string path)
        {
            var data = new BuildSetData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["class"] = (v, p) => data.Class = input.Text(v, p),
                ["builds"] = (v, p) => data.Builds = input.ReadLimitedList(v, p, BuildSetValidator.MaxBuilds,
                    n => $"{n} builds, au plus {BuildSetValidator.MaxBuilds} par fichier.", (e, ip) => ReadBuild(input, e, ip)),
            });
            return ok ? data : null;
        }

        private static ReferenceBuildData? ReadBuild(JsonInput input, JsonElement element, string path)
        {
            var b = new ReferenceBuildData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => b.Id = input.Text(v, p),
                ["name"] = (v, p) => b.Name = input.Text(v, p),
                ["category"] = (v, p) => b.Category = input.Text(v, p),
                ["build"] = (v, p) => b.Build = ScenarioJsonReader.ReadBuild(input, v, p),
            });
            return ok ? b : null;
        }
    }
}
