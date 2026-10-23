using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System;
using EdS.Data.Json.Text;
using EdS.Data.Validation;
using EdS.Domain.Numerics;

namespace EdS.Data.Json
{
    /// <summary>En-tête commun à tous les fichiers de données.</summary>
    internal sealed class JsonHeader
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
    }

    /// <summary>
    /// Lecture JSON stricte partagée par tous les adaptateurs : syntaxe, en-tête, types, champs inconnus ou en double,
    /// nombres. Toute erreur est ajoutée au rapport avec son chemin et, si possible, sa ligne.
    /// </summary>
    internal sealed class JsonInput
    {
        // Sous-lot 9.1 : analyseur strict maison (EdS.Data.Json.Text) : ni virgule finale ni commentaire, profondeur et nombre de
        // valeurs bornés.
        private static readonly JsonDocumentOptions DocumentOptions =
            new JsonDocumentOptions { MaxDepth = InputLimits.MaxJsonDepth, MaxValues = InputLimits.MaxJsonValues };

        public ValidationReport Report { get; }
        private readonly JsonElement _root;
        private JsonLineIndex? _lines;

        private JsonInput(ValidationReport report, JsonElement root)
        {
            Report = report;
            _root = root;
        }

        /// <summary>
        /// Ligne d'un chemin JSON. L'index des lignes n'est construit qu'à la première demande : un fichier sans erreur
        /// localisée n'en paie jamais le coût.
        /// </summary>
        public long? LineOf(string path, int occurrence = 0) => (_lines ??= JsonLineIndex.Build(_root)).Of(path, occurrence);

        /// <summary>
        /// Lit un fichier en octets UTF-8, sans marque d'ordre, en refusant avant lecture un fichier de plus de
        /// <paramref name="maxBytes"/> octets. La lecture elle-même est bornée : un fichier qui grandit entre le contrôle et
        /// la lecture est refusé de la même façon. En cas d'échec, le rapport contient <see cref="ValidationCode.FileUnreadable"/>
        /// ou <see cref="ValidationCode.FileTooLarge"/>.
        /// </summary>
        public static byte[]? ReadFile(string path, ValidationReport report, long maxBytes = InputLimits.MaxDataFileBytes)
        {
            try
            {
                long length = new FileInfo(path).Length;
                if (length > maxBytes)
                {
                    TooLarge(report, length, maxBytes, path);
                    return null;
                }
                byte[]? bytes = ReadBounded(path, maxBytes);
                if (bytes is null)
                {
                    TooLarge(report, null, maxBytes, path);
                    return null;
                }
                return bytes;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                report.Add("$", ValidationCode.FileUnreadable, $"impossible de lire « {path} » : {e.Message}");
                return null;
            }
        }

        /// <summary>Lit au plus <paramref name="maxBytes"/> octets ; null si le flux en contient davantage. La marque UTF-8 est retirée.</summary>
        internal static byte[]? ReadBounded(string path, long maxBytes)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (buffer.Length + read > maxBytes) return null;
                buffer.Write(chunk, 0, read);
            }
            byte[] bytes = buffer.ToArray();
            return bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? bytes[3..] : bytes;
        }

        private static void TooLarge(ValidationReport report, long? length, long maxBytes, string? path)
        {
            string what = path is null ? "texte JSON" : $"fichier « {path} »";
            string size = length is { } n ? $" de {n} octets" : string.Empty;
            report.Add("$", ValidationCode.FileTooLarge,
                $"{what}{size} trop volumineux : au plus {InputLimits.DescribeBytes(maxBytes)} ; refusé avant lecture du contenu.");
        }

        /// <summary>
        /// Encode un texte JSON en UTF-8 après avoir vérifié sa taille, sans l'encoder d'abord : un texte de plus de
        /// <see cref="InputLimits.MaxDataFileBytes"/> octets est refusé (<see cref="ValidationCode.FileTooLarge"/>) et null est rendu.
        /// </summary>
        /// <param name="maxBytes">Sous-lot 12.1 (revue Codex de la PR #42) : limite propre à un genre de fichier (sauvegarde : 32 Mio) ;
        /// <see cref="InputLimits.MaxDataFileBytes"/> par défaut.</param>
        public static byte[]? Utf8(string json, ValidationReport report, long maxBytes = InputLimits.MaxDataFileBytes)
        {
            // Chaque unité UTF-16 donne au moins un octet : une longueur au-delà de la limite suffit à refuser sans compter.
            long bytes = json.Length > maxBytes ? json.Length : Encoding.UTF8.GetByteCount(json);
            if (bytes > maxBytes)
            {
                TooLarge(report, bytes, maxBytes, null);
                return null;
            }
            return Encoding.UTF8.GetBytes(json);
        }

        /// <summary>
        /// Analyse le texte puis lit l'en-tête commun ; <paramref name="readData"/> reçoit l'élément <c>data</c>.
        /// </summary>
        /// <param name="maxBytes">Limite de taille du texte (celle de la lecture qui l'a produit).</param>
        public static JsonHeader Parse(byte[] utf8, ValidationReport report, Action<JsonInput, JsonElement> readData,
            long maxBytes = InputLimits.MaxDataFileBytes)
        {
            var header = new JsonHeader();
            ParseDocument(utf8, report, (input, root) => input.ReadRoot(root, header, readData), maxBytes);
            return header;
        }

        /// <summary>Analyse un texte JSON strict (taille vérifiée avant encodage) et confie la racine à <paramref name="readRoot"/>.</summary>
        public static void ParseDocument(string json, ValidationReport report, Action<JsonInput, JsonElement> readRoot)
        {
            if (Utf8(json, report) is { } utf8) ParseDocument(utf8, report, readRoot);
        }

        /// <summary>Analyse des octets UTF-8 (journal, fichiers sans en-tête commun) et confie la racine à <paramref name="readRoot"/>.</summary>
        public static void ParseDocument(byte[] utf8, ValidationReport report, Action<JsonInput, JsonElement> readRoot,
            long maxBytes = InputLimits.MaxDataFileBytes)
        {
            if (utf8.Length > maxBytes)
            {
                TooLarge(report, utf8.Length, maxBytes, null);
                return;
            }
            JsonDocument parsed;
            try
            {
                parsed = JsonDocument.Parse(utf8, DocumentOptions);
            }
            catch (JsonException e) when (e.LimitExceeded)
            {
                // Texte bien formé jusque-là mais démesuré : limite de volume, comme un tableau trop long.
                report.Add("$", ValidationCode.LimitExceeded, e.Message, e.LineNumber + 1);
                return;
            }
            catch (JsonException e)
            {
                report.Add("$", ValidationCode.InvalidJson, "JSON invalide : " + FirstSentence(e.Message), e.LineNumber + 1);
                return;
            }

            using (parsed)
                readRoot(new JsonInput(report, parsed.RootElement), parsed.RootElement);
        }

        /// <summary>Lit un objet dont les champs connus ont chacun un lecteur ; tout autre champ est signalé.</summary>
        public bool ReadFields(JsonElement element, string path, IReadOnlyDictionary<string, Action<JsonElement, string>> fields)
        {
            if (!RequireKind(element, JsonValueKind.Object, path, "objet")) return false;
            foreach (JsonProperty property in UniqueProperties(element, path))
            {
                string childPath = path + "." + property.Name;
                if (fields.TryGetValue(property.Name, out var read)) read(property.Value, childPath);
                else UnknownField(childPath, fields.Keys);
            }
            return true;
        }

        /// <summary>Lit un tableau ; un élément illisible reste null pour conserver les indices du fichier.</summary>
        public List<T?>? ReadList<T>(JsonElement element, string path, Func<JsonElement, string, T?> readItem) where T : class
        {
            if (!RequireKind(element, JsonValueKind.Array, path, "tableau")) return null;
            return ReadItems(element, path, readItem);
        }

        /// <summary>
        /// Lit un tableau dont la longueur est plafonnée. La longueur est lue avant tout élément : au-delà de
        /// <paramref name="max"/>, une seule erreur <see cref="ValidationCode.LimitExceeded"/> est rapportée sur le tableau,
        /// aucun élément n'est construit ni analysé, et null est rendu.
        /// </summary>
        public List<T?>? ReadLimitedList<T>(JsonElement element, string path, int max, Func<int, string> describeExcess,
            Func<JsonElement, string, T?> readItem) where T : class
        {
            if (!RequireKind(element, JsonValueKind.Array, path, "tableau")) return null;
            int length = element.GetArrayLength();
            if (length > max)
            {
                // Recherche ciblée de la ligne : on ne construit pas l'index complet d'un fichier refusé pour son volume.
                Report.Add(path, ValidationCode.LimitExceeded, describeExcess(length), _lines?.Of(path) ?? JsonLineIndex.FindFirst(_root, path));
                return null;
            }
            return ReadItems(element, path, readItem);
        }

        private static List<T?> ReadItems<T>(JsonElement element, string path, Func<JsonElement, string, T?> readItem) where T : class
        {
            var list = new List<T?>();
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
                list.Add(readItem(item, $"{path}[{index++}]"));
            return list;
        }

        public string? Text(JsonElement value, string path) => TryReadString(value, path, out string? s) ? s : null;

        /// <summary>Texte, ou null si la valeur est un null JSON explicite.</summary>
        public string? NullableText(JsonElement value, string path) => value.ValueKind == JsonValueKind.Null ? null : Text(value, path);

        public int? Int32(JsonElement value, string path) => TryReadInt32(value, path, out int n) ? n : null;

        public long? Int64(JsonElement value, string path) => TryReadInteger(value, path, out long n) ? n : null;

        public Fixed? Decimal(JsonElement value, string path) => TryReadDecimal(value, path, out Fixed f) ? f : null;

        public bool? Boolean(JsonElement value, string path) => TryReadBoolean(value, path, out bool b) ? b : null;

        private void ReadRoot(JsonElement root, JsonHeader header, Action<JsonInput, JsonElement> readData)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                Report.Add("$", ValidationCode.WrongType, $"objet JSON attendu à la racine, {Describe(root.ValueKind)} trouvé.", 1);
                return;
            }

            foreach (JsonProperty property in UniqueProperties(root, "$"))
            {
                string path = "$." + property.Name;
                JsonElement value = property.Value;
                switch (property.Name)
                {
                    case "schemaVersion":
                        if (TryReadInteger(value, path, out long version))
                        {
                            if (version is < int.MinValue or > int.MaxValue)
                                Report.Add(path, ValidationCode.OutOfRange, "version hors plage.", LineOf(path));
                            else
                                header.SchemaVersion = (int)version;
                        }
                        break;
                    case "kind":
                        if (TryReadString(value, path, out string? kind)) header.Kind = kind;
                        break;
                    case "contentVersion":
                        if (TryReadString(value, path, out string? contentVersion)) header.ContentVersion = contentVersion;
                        break;
                    case "data":
                        readData(this, value);
                        break;
                    case "$schema":
                        // Indication pour les éditeurs (auto-complétion) ; ignorée par le programme.
                        TryReadString(value, path, out _);
                        break;
                    default:
                        Report.Add(path, ValidationCode.UnknownField,
                            "champ inconnu ; champs possibles : $schema, schemaVersion, kind, contentVersion, data.", LineOf(path));
                        break;
                }
            }
        }

        private static string FirstSentence(string message)
        {
            int end = message.IndexOf(" Path:", StringComparison.Ordinal);
            return end > 0 ? message[..end] : message;
        }

        public bool RequireKind(JsonElement element, JsonValueKind expected, string path, string label)
        {
            if (element.ValueKind == expected) return true;
            Report.Add(path, ValidationCode.WrongType, $"{label} attendu, {Describe(element.ValueKind)} trouvé.", LineOf(path));
            return false;
        }

        /// <summary>Propriétés d'un objet, chaque nom une seule fois ; les doublons sont signalés.</summary>
        public IEnumerable<JsonProperty> UniqueProperties(JsonElement element, string jsonPath)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (seen.Add(property.Name))
                {
                    yield return property;
                    continue;
                }
                string path = jsonPath + "." + property.Name;
                Report.Add(path, ValidationCode.DuplicateField, "champ présent plusieurs fois dans le même objet ; seule la première occurrence est lue.",
                    LineOf(path, occurrence: 1));
            }
        }

        public void UnknownField(string path, IEnumerable<string> expected) =>
            Report.Add(path, ValidationCode.UnknownField, $"champ inconnu ; champs possibles : {string.Join(", ", expected)}.", LineOf(path));

        public bool TryReadDecimal(JsonElement value, string path, out Fixed result)
        {
            result = Fixed.Zero;
            if (value.ValueKind != JsonValueKind.Number)
            {
                Report.Add(path, ValidationCode.WrongType, $"nombre attendu, {Describe(value.ValueKind)} trouvé.", LineOf(path));
                return false;
            }
            string raw = value.GetRawText();
            if (Fixed.TryParse(raw, out result)) return true;

            bool wellFormed = raw.IndexOfAny(new[] { 'e', 'E' }) < 0 && (raw.IndexOf('.') < 0 || raw.Length - raw.IndexOf('.') - 1 <= Fixed.Decimals);
            if (wellFormed)
                Report.Add(path, ValidationCode.OutOfRange, $"{raw} est hors de la plage représentable.", LineOf(path));
            else
                Report.Add(path, ValidationCode.InvalidNumber, $"{raw} : 4 décimales au plus et pas d'exposant (ex. 1.5, 0.0001, 1000).", LineOf(path));
            return false;
        }

        public bool TryReadInteger(JsonElement value, string path, out long result)
        {
            result = 0;
            if (value.ValueKind != JsonValueKind.Number)
            {
                Report.Add(path, ValidationCode.WrongType, $"nombre entier attendu, {Describe(value.ValueKind)} trouvé.", LineOf(path));
                return false;
            }
            string raw = value.GetRawText();
            if (raw.IndexOfAny(new[] { '.', 'e', 'E' }) >= 0)
            {
                Report.Add(path, ValidationCode.InvalidNumber, $"{raw} : nombre entier attendu (sans décimale ni exposant).", LineOf(path));
                return false;
            }
            if (long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result)) return true;
            Report.Add(path, ValidationCode.OutOfRange, $"{raw} dépasse la plage d'un entier 64 bits.", LineOf(path));
            return false;
        }

        /// <summary>Entier 32 bits ; hors plage → <see cref="ValidationCode.OutOfRange"/>.</summary>
        public bool TryReadInt32(JsonElement value, string path, out int result)
        {
            result = 0;
            if (!TryReadInteger(value, path, out long n)) return false;
            if (n is < int.MinValue or > int.MaxValue)
            {
                Report.Add(path, ValidationCode.OutOfRange, $"{n} dépasse la plage d'un entier 32 bits.", LineOf(path));
                return false;
            }
            result = (int)n;
            return true;
        }

        public bool TryReadString(JsonElement value, string path, out string? result)
        {
            result = null;
            if (value.ValueKind != JsonValueKind.String)
            {
                Report.Add(path, ValidationCode.WrongType, $"texte attendu, {Describe(value.ValueKind)} trouvé.", LineOf(path));
                return false;
            }
            result = value.GetString();
            return true;
        }

        public bool TryReadBoolean(JsonElement value, string path, out bool result)
        {
            result = false;
            if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                result = value.GetBoolean();
                return true;
            }
            Report.Add(path, ValidationCode.WrongType, $"booléen (true ou false) attendu, {Describe(value.ValueKind)} trouvé.", LineOf(path));
            return false;
        }

        public static string Describe(JsonValueKind kind) => kind switch
        {
            JsonValueKind.Object => "un objet",
            JsonValueKind.Array => "un tableau",
            JsonValueKind.String => "un texte",
            JsonValueKind.Number => "un nombre",
            JsonValueKind.True or JsonValueKind.False => "un booléen",
            JsonValueKind.Null => "null",
            _ => "une valeur inattendue",
        };
    }
}
