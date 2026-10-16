using EdS.Data;

namespace EdS.Data.Tests;

public class DataFormatTests
{
    [Fact]
    public void Version_de_schema_du_lot_6() => Assert.Equal(2, DataFormat.CurrentSchemaVersion);

    [Fact]
    public void Contenu_marque_provisoire() => Assert.EndsWith("-provisoire", DataFormat.ProvisionalContentVersion);
}
