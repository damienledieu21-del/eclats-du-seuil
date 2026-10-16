using System.Text.RegularExpressions;

namespace EdS.Data.Validation
{
    /// <summary>Vérification de l'en-tête commun à tous les fichiers de données (versions, type de fichier).</summary>
    public static class HeaderValidator
    {
        // SemVer 2.0 : MAJEUR.MINEUR.CORRECTIF, suffixe de pré-version et métadonnées facultatifs.
        private static readonly Regex SemVer = new Regex(
            @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$",
            RegexOptions.CultureInvariant);

        public static bool IsSemVer(string? version) => version != null && SemVer.IsMatch(version);

        public static void Validate(int? schemaVersion, string? kind, string? contentVersion,
            string expectedKind, int supportedSchemaVersion, ValidationReport report)
        {
            if (schemaVersion is null)
            {
                if (!report.HasErrorAtOrAbove("$.schemaVersion"))
                    report.Add("$.schemaVersion", ValidationCode.MissingField, $"champ obligatoire absent (version du format, actuellement {supportedSchemaVersion}).");
            }
            else if (schemaVersion != supportedSchemaVersion)
            {
                report.Add("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion,
                    $"version de format {schemaVersion} non prise en charge ; ce programme lit la version {supportedSchemaVersion}.");
            }

            if (kind is null)
            {
                if (!report.HasErrorAtOrAbove("$.kind"))
                    report.Add("$.kind", ValidationCode.MissingField, $"champ obligatoire absent (valeur attendue : « {expectedKind} »).");
            }
            else if (kind != expectedKind)
            {
                report.Add("$.kind", ValidationCode.WrongKind, $"fichier de type « {kind} », « {expectedKind} » attendu.");
            }

            if (contentVersion is null)
            {
                if (!report.HasErrorAtOrAbove("$.contentVersion"))
                    report.Add("$.contentVersion", ValidationCode.MissingField, "champ obligatoire absent (version des valeurs, ex. « 0.1.0-provisoire »).");
            }
            else if (!IsSemVer(contentVersion))
            {
                report.Add("$.contentVersion", ValidationCode.InvalidContentVersion,
                    $"« {contentVersion} » n'est pas une version SemVer (ex. « 0.1.0 » ou « 0.1.0-provisoire »).");
            }
        }
    }
}
