using EdS.Data;

namespace EdS.Data.Tests;

public class DataFormatTests
{
    [Fact]
    public void Version_de_schema_initiale() => Assert.Equal(1, DataFormat.CurrentSchemaVersion);

    [Fact]
    public void Contenu_marque_provisoire() => Assert.EndsWith("-provisoire", DataFormat.ProvisionalContentVersion);
}
