using System;
using System.IO;
using EdS.Data.Json;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

[Trait("Category", "World")]
public class V261SuitesDuSeuilTests
{
    [Fact]
    public void Diagnostic()
    {
        var items = ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData);
        Assert.True(items.Catalog != null, items.Report.ToString());
        var texts = StoryJsonReader.ReadTextsFile(StoryJsonReader.TextsPathIn(DataDirectory));
        Assert.True(texts.Texts != null, texts.Report.ToString());
        var zone = ZoneJsonReader.Read(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")), Path.Combine(AppContext.BaseDirectory, "scenarios"), ShippedData, items.Catalog!);
        Assert.True(zone.IsValid, zone.Report.ToString());
        var story = StoryJsonReader.ReadStoryFile(StoryJsonReader.StoryPathIn(DataDirectory), texts.Texts!, zone.Zone!, items.Catalog!, V16TestData.Interiors);
        Assert.True(story.Story != null, story.Report.ToString());
    }
}
