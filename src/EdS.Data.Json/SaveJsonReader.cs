using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using EdS.Data.Json.Text;
using EdS.Data.Saves;
using EdS.Data.Validation;

namespace EdS.Data.Json
{
    /// <summary>
    /// Sous-lot 12.1 : lecture stricte et écriture canonique des sauvegardes du monde (<c>kind: "save"</c>). La lecture ne vérifie que la
    /// forme ; le reste est délégué au validateur unique <see cref="SaveValidator"/>. L'écriture donne toujours le même texte pour la même
    /// sauvegarde (une ligne par champ, événements dans l'ordre ordinal), relu à l'identique.
    /// </summary>
    public static class SaveJsonReader
    {
        /// <summary>Version des valeurs d'une sauvegarde écrite par ce programme.</summary>
        public const string ContentVersion = "0.1.0-provisoire";

        /// <summary>Taille maximale d'un fichier de sauvegarde lu (enveloppe de la tranche : 100 000 événements de 200 caractères au plus).</summary>
        public const long MaxBytes = 32L * 1024 * 1024;

        public static SaveLoadResult ReadFile(string path)
        {
            var report = new ValidationReport();
            return Read(JsonInput.ReadFile(path, report, MaxBytes), report);
        }

        public static SaveLoadResult Read(string json)
        {
            var report = new ValidationReport();
            return Read(JsonInput.Utf8(json, report, MaxBytes), report);
        }

        private static SaveLoadResult Read(byte[]? json, ValidationReport report)
        {
            var document = new SaveDocument();
            if (json is null) return SaveValidator.Validate(document, report);
            JsonHeader header = JsonInput.Parse(json, report, (input, element) => document.Data = ReadData(input, element, "$.data"), MaxBytes);
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            return SaveValidator.Validate(document, report);
        }

        private static SaveData? ReadData(JsonInput input, JsonElement element, string path)
        {
            var data = new SaveData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["zone"] = (v, p) => data.Zone = input.Text(v, p),
                ["worldTick"] = (v, p) => data.WorldTick = input.Int64(v, p),
                ["sessions"] = (v, p) => data.Sessions = input.Int64(v, p),
                ["consumed"] = (v, p) => data.Consumed = input.ReadLimitedList(v, p, SaveValidator.MaxConsumed,
                    n => $"{n} événements consommés, au plus {SaveValidator.MaxConsumed}.", (e, ip) => input.Text(e, ip)),
                ["character"] = (v, p) =>   // sous-lot 12.2 (version 2) : null ou le personnage
                {
                    data.HasCharacter = true;
                    if (v.ValueKind != JsonValueKind.Null) data.Character = ReadCharacter(input, v, p);
                },
            });
            return ok ? data : null;
        }

        private static CharacterData? ReadCharacter(JsonInput input, JsonElement element, string path)
        {
            var c = new CharacterData();
            bool ok = input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => c.Id = input.Text(v, p),
                ["class"] = (v, p) => c.Class = input.Text(v, p),
                ["xp"] = (v, p) => c.Xp = input.Int64(v, p),
                ["talents"] = (v, p) => c.Talents = ScenarioJsonReader.ReadIntegerMap(input, v, p),
                ["activeSkills"] = (v, p) => c.ActiveSkills = input.ReadLimitedList(v, p, 64, n => $"{n} compétences, au plus 64.", (e, ip) => input.Text(e, ip)),
                ["ultimate"] = (v, p) =>
                {
                    c.HasUltimate = true;
                    c.Ultimate = input.NullableText(v, p);
                },
                ["passiveSkills"] = (v, p) => c.PassiveSkills = input.ReadLimitedList(v, p, 64, n => $"{n} compétences, au plus 64.", (e, ip) => input.Text(e, ip)),
            });
            return ok ? c : null;
        }

        // Identifiants écrits sans échappement : ASCII en minuscules, chiffres, « _ » et « . ».
        private static readonly System.Text.RegularExpressions.Regex SafeId =
            new System.Text.RegularExpressions.Regex(@"^[a-z0-9_.]+\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        private static string Quoted(string id) =>
            SafeId.IsMatch(id) ? "\"" + id + "\"" : throw new ArgumentException($"Identifiant « {id} » hors du format des sauvegardes.", nameof(id));

        /// <summary>Texte canonique d'une sauvegarde (UTF-8, fins de ligne « \n », dernière ligne terminée).</summary>
        public static string Write(WorldSave save)
        {
            if (save is null) throw new ArgumentNullException(nameof(save));
            // Les identifiants et les clés sont déjà validés (ASCII sans guillemet ni barre oblique inverse) : aucun échappement nécessaire.
            if (!SaveValidator.IsZoneId(save.Zone) || save.Consumed.Any(k => !SaveValidator.IsEventKey(k)))
                throw new ArgumentException("Sauvegarde invalide : identifiant de zone ou clé d'événement hors du format.", nameof(save));
            // Revue Codex de la PR #42 : mêmes bornes qu'à la lecture, pour ne jamais écrire une sauvegarde qui ne se relirait pas.
            if (save.WorldTick < 0 || save.WorldTick > SaveValidator.MaxWorldTick || save.Sessions < 0 || save.Sessions > SaveValidator.MaxWorldTick
                || save.Consumed.Count > SaveValidator.MaxConsumed)
                throw new ArgumentException($"Sauvegarde invalide : temps du monde ou sessions hors de [0, {SaveValidator.MaxWorldTick}], ou plus de {SaveValidator.MaxConsumed} événements.", nameof(save));
            var b = new StringBuilder();
            b.Append("{\n");
            b.Append("  \"schemaVersion\": ").Append(SaveValidator.CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            b.Append("  \"kind\": \"").Append(SaveValidator.Kind).Append("\",\n");
            b.Append("  \"contentVersion\": \"").Append(ContentVersion).Append("\",\n");
            b.Append("  \"data\": {\n");
            b.Append("    \"zone\": \"").Append(save.Zone).Append("\",\n");
            b.Append("    \"worldTick\": ").Append(save.WorldTick.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            b.Append("    \"sessions\": ").Append(save.Sessions.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            WriteCharacter(b, save.Character);
            if (save.Consumed.Count == 0) b.Append("    \"consumed\": []\n");
            else
            {
                b.Append("    \"consumed\": [\n");
                for (int i = 0; i < save.Consumed.Count; i++)
                    b.Append("      \"").Append(save.Consumed[i]).Append(i + 1 < save.Consumed.Count ? "\",\n" : "\"\n");
                b.Append("    ]\n");
            }
            b.Append("  }\n}\n");
            string text = b.ToString();
            // Revue Codex de la PR #43 : ce qui est écrit doit se relire (bornes du personnage comprises : expérience, rangs, identifiants),
            // sinon une sauvegarde valide serait remplacée par un fichier illisible.
            SaveLoadResult check = Read(text);
            if (!check.IsValid)
                throw new ArgumentException($"Sauvegarde invalide : {check.Report.Errors[0].Path} : {check.Report.Errors[0].Message}", nameof(save));
            return text;
        }

        private static void WriteCharacter(StringBuilder b, SavedCharacter? c)
        {
            if (c is null)
            {
                b.Append("    \"character\": null,\n");
                return;
            }
            string List(IEnumerable<string> ids) => "[" + string.Join(", ", ids.Select(Quoted)) + "]";
            b.Append("    \"character\": {\n");
            b.Append("      \"id\": ").Append(Quoted(c.Id)).Append(",\n");
            b.Append("      \"class\": ").Append(Quoted(c.ClassId)).Append(",\n");
            b.Append("      \"xp\": ").Append(c.Xp.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            b.Append("      \"talents\": {").Append(string.Join(", ", c.Talents.Select(t => Quoted(t.Key) + ": " + t.Value.ToString(CultureInfo.InvariantCulture)))).Append("},\n");
            b.Append("      \"activeSkills\": ").Append(List(c.ActiveSkills)).Append(",\n");
            b.Append("      \"ultimate\": ").Append(c.Ultimate is null ? "null" : Quoted(c.Ultimate)).Append(",\n");
            b.Append("      \"passiveSkills\": ").Append(List(c.PassiveSkills)).Append("\n");
            b.Append("    },\n");
        }
    }
}
