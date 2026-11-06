using System.Text.RegularExpressions;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.4 : une sauvegarde écrite ramenée à la forme des versions antérieures à la version 11, sans le compteur des fabrications
/// (<c>crafts</c>) ni, depuis la version 12 (sous-lot V1.8c), les bonus actifs (<c>boons</c>, la dernière ligne du personnage) : la base des
/// tests qui fabriquent une sauvegarde d'une version plus ancienne. Une instance de base n'a ni qualité ni usure : rien d'autre à retirer.
/// </summary>
internal static class SaveV11
{
    public static string Older(string text) => Regex.Replace(V11(text), ",\n      \"crafts\": [0-9]+\n", "\n");

    /// <summary>Sous-lot V1.8c : la sauvegarde sous la forme de la version 11, sans les bonus actifs (le numéro de version n'est pas changé).</summary>
    public static string V11(string text) => Regex.Replace(V12(text), ",\n      \"boons\": \\[[^\n]*\\]\n", "\n");

    /// <summary>Sous-lot V2.5 : la sauvegarde sous la forme de la version 12, sans le registre des événements (dernier champ du personnage).</summary>
    public static string V12(string text) => Regex.Replace(text, ",\n      \"events\": (\\[\\]|\\[\n(        [^\n]*\n)*      \\])\n", "\n");
}
