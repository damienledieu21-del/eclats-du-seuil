using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using EdS.Data;
using EdS.Data.Json;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.World;

namespace EdS.Tools.Zones;

/// <summary>
/// Sous-lots V2.31 et suivants (lieux de la grande carte) : l'aire d'une zone à région dépasse 256 obstacles ; elle se lit comme une aire et
/// se simule par secteurs. Outils de la ligne de commande (<c>eds validate</c>, <c>eds simulate</c>).
/// </summary>
public static class ZoneAreas
{
    /// <summary>Le fichier est-il un scénario de zone (<c>data.zone</c> vrai) ? Faux s'il est illisible (le lecteur dira pourquoi).</summary>
    public static bool IsZoneScenario(string path)
    {
        try
        {
            using JsonDocument d = JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { MaxDepth = 64 });
            return d.RootElement.ValueKind == JsonValueKind.Object && d.RootElement.TryGetProperty("data", out JsonElement data)
                && data.ValueKind == JsonValueKind.Object && data.TryGetProperty("zone", out JsonElement zone) && zone.ValueKind == JsonValueKind.True;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return false; }
    }

    /// <summary>
    /// Combat du secteur du point d'entrée de la zone dont <paramref name="scenarioPath"/> est l'aire (zone cherchée dans
    /// <c>world/zones</c> du dossier de données, par son champ <c>scenario</c>, relatif au dossier <c>scenarios</c> qui contient le fichier) ;
    /// null avec la raison sinon.
    /// </summary>
    public static CombatSetup? EntrySector(string scenarioPath, string dataDirectory, GameData data, out string? sector, out string? why)
    {
        sector = null;
        string full = Path.GetFullPath(scenarioPath);
        string? root = Path.GetDirectoryName(full);
        while (root != null && Path.GetFileName(root) != "scenarios") root = Path.GetDirectoryName(root);
        if (root is null) { why = "fichier hors d'un dossier « scenarios »."; return null; }
        string id = Path.GetRelativePath(root, full).Replace('\\', '/');
        id = id.EndsWith(".json", StringComparison.Ordinal) ? id.Substring(0, id.Length - 5) : id;
        string zones = Path.Combine(dataDirectory, "world", "zones");
        string? zonePath = Directory.Exists(zones) ? Directory.GetFiles(zones, "*.json").OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault(f => ScenarioOf(f) == id) : null;
        if (zonePath is null) { why = $"aucune zone de {zones} n'a « {id} » pour aire."; return null; }
        var items = ItemJsonReader.ReadFile(ItemJsonReader.PathIn(dataDirectory), data);
        if (items.Catalog is null) { why = items.Report.ToString(); return null; }
        ZoneLoadResult zone = ZoneJsonReader.ReadFile(zonePath, root, data, items.Catalog);
        if (zone.Zone is null) { why = zone.Report.ToString(); return null; }
        if (zone.Zone.Region is null) { why = $"{zonePath} : zone sans région."; return null; }
        SectorMap map = SectorMap.For(zone.Zone);
        sector = map.HomeSector;
        why = null;
        return map.SetupFor(map.HomeSector, 0);
    }

    private static string? ScenarioOf(string zonePath)
    {
        try
        {
            using JsonDocument d = JsonDocument.Parse(File.ReadAllBytes(zonePath), new JsonDocumentOptions { MaxDepth = 64 });
            return d.RootElement.TryGetProperty("data", out JsonElement data) && data.ValueKind == JsonValueKind.Object
                && data.TryGetProperty("scenario", out JsonElement s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { return null; }
    }
}
