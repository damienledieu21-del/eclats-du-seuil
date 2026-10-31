using System.Text.RegularExpressions;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.4 : une sauvegarde écrite (version 11) ramenée à la forme des versions antérieures, sans le compteur des fabrications
/// (<c>crafts</c>, la dernière ligne du personnage) : la base des tests qui fabriquent une sauvegarde d'une version plus ancienne. Une
/// instance de base n'a ni qualité ni usure : rien d'autre à retirer.
/// </summary>
internal static class SaveV11
{
    public static string Older(string text) => Regex.Replace(text, ",\n      \"crafts\": [0-9]+\n", "\n");
}
