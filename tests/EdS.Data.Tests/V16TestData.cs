using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Zones;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.6 : les intérieurs livrés (auberge, chapelle, maison), où vivent des PNJ de l'histoire. L'histoire se valide avec eux
/// (<see cref="StoryJsonReader.ReadStoryFile"/>, paramètre <c>interiors</c>).
/// </summary>
internal static class V16TestData
{
    private static readonly Lazy<IReadOnlyList<ZoneDefinition>> Linked = new(() =>
    {
        ItemCatalog items = ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;
        WorldZonesLoadResult zones = ZoneJsonReader.ReadLinked(DataDirectory, "zone.vallons", Path.Combine(AppContext.BaseDirectory, "scenarios"), ShippedData, items);
        Assert.True(zones.IsValid, zones.Report.ToString());
        return zones.Zones.Skip(1).ToList();
    });

    /// <summary>Zones reliées aux Vallons par des portes (sans les Vallons).</summary>
    public static IReadOnlyList<ZoneDefinition> Interiors => Linked.Value;
}
