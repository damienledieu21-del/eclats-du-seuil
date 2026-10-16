using System.Text.RegularExpressions;

namespace EdS.Data.Validation
{
    /// <summary>Vérification de l'en-tête commun à tous les fichiers de données (versions, type de fichier).</summary>
    public static class HeaderValidator
    {
        // SemVer 2.0 : MAJEUR.MINEUR.CORRECTIF, suffixe de pré-version et métadonnées facultatifs. Correctif 6.1 : chiffres
        // ASCII seulement ([0-9] et non \d, qui accepte tous les chiffres Unicode), identifiants de pré-version numériques
        // sans zéro initial, et correspondance de toute la chaîne (\z : « $ » acceptait un saut de ligne final).
        private static readonly Regex SemVer = new Regex(
            @"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)"
            + @"(-(0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(\.(0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*)?"
            + @"(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?\z",
            RegexOptions.CultureInvariant);

        public static bool IsSemVer(string? version) => version != null && SemVer.IsMatch(version);

        /// <summary>Vérifie l'en-tête. Renvoie faux si la version de schéma est présente mais non prise en charge :
        /// la structure du fichier est alors celle d'un autre format et le contenu ne doit pas être analysé.</summary>
        public static bool Validate(int? schemaVersion, string? kind, string? contentVersion,
            string expectedKind, int supportedSchemaVersion, ValidationReport report)
        {
            bool supported = true;
            if (schemaVersion is null)
            {
                if (!report.HasErrorAtOrAbove("$.schemaVersion"))
                    report.Add("$.schemaVersion", ValidationCode.MissingField, $"champ obligatoire absent (version du format, actuellement {supportedSchemaVersion}).");
            }
            else if (schemaVersion < supportedSchemaVersion)
            {
                supported = false;
                report.Add("$.schemaVersion", ValidationCode.UnsupportedSchemaVersion,
                    $"version de format {schemaVersion} antérieure à la version {supportedSchemaVersion} : structure incompatible, refusée. "
                    + "Pré-alpha : aucune migration n'est garantie ; mettre le fichier à jour (voir docs/DONNEES.md, « Versions de format »).");
            }
            else if (schemaVersion > supportedSchemaVersion)
            {
                supported = false;
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
            return supported;
        }
    }
}
