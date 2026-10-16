using System.Text;

namespace EdS.Data.Rules
{
    /// <summary>
    /// Empreinte stable des règles : SHA-256 d'une écriture canonique (version de schéma puis chaque paramètre
    /// dans l'ordre du catalogue, au format <c>chemin=valeur</c> de <see cref="CanonicalWriter"/> (version du contrat en tête depuis le
    /// correctif 7.3.1), puis les altérations dans l'ordre du fichier, voir <see cref="StatusShape"/>). Elle ne dépend ni des espaces, ni de l'ordre
    /// des clés, ni de l'écriture des nombres (« 2 » et « 2.0 » donnent la même empreinte), et elle ignore
    /// <c>contentVersion</c> : deux fichiers aux valeurs identiques ont la même empreinte.
    /// Elle sera inscrite dans le journal de combat pour garantir le rejeu.
    /// </summary>
    public static class RulesFingerprint
    {
        public static string CanonicalText(int schemaVersion, RulesDocument document)
        {
            var text = new StringBuilder();
            var w = new CanonicalWriter(text);
            text.Append("eds-rules\n");
            w.Value("canonical", CanonicalWriter.Version);   // correctif 7.3.1 (A73-M02)
            w.Value("schemaVersion", schemaVersion);
            foreach (RuleField field in RulesCatalog.Fields)
                w.Value(field.Path, document.Values[field.Path].ToCanonicalString());
            if (document.Statuses != null) StatusShape.AppendCanonical(text, StatusShape.Build(document.Statuses));
            return text.ToString();
        }

        public static string Compute(int schemaVersion, RulesDocument document) =>
            ContentFingerprint.Compute(CanonicalText(schemaVersion, document));
    }
}
