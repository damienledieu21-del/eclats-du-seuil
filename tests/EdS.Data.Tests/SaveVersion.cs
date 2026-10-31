using EdS.Data.Saves;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.7 : la version de sauvegarde écrite, telle qu'elle apparaît dans le texte (« "schemaVersion": 10 »), tirée de
/// <see cref="SaveValidator.CurrentSchemaVersion"/> : les tests de sauvegarde ne répètent plus le numéro (un seul endroit à changer quand une
/// version s'ajoute, sous-lot V1.3 en version 9, V1.7 en version 10).
/// </summary>
internal static class SaveVersion
{
    /// <summary>Version courante, telle qu'écrite.</summary>
    public static string Current => $"\"schemaVersion\": {SaveValidator.CurrentSchemaVersion}";

    /// <summary>Version suivante (pas encore lue), telle qu'écrite.</summary>
    public static string Future => $"\"schemaVersion\": {SaveValidator.CurrentSchemaVersion + 1}";

    /// <summary>Remplace « {current} » et « {future} » (données de théorie, qui ne peuvent pas lire une constante calculée).</summary>
    public static string Expand(string text) => text.Replace("{current}", SaveValidator.CurrentSchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture))
        .Replace("{future}", (SaveValidator.CurrentSchemaVersion + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
}
