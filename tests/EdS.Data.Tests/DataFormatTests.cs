using EdS.Data;

namespace EdS.Data.Tests;

public class DataFormatTests
{
    [Fact]
    public void Version_de_schema_des_regles_du_lot_7_2() => Assert.Equal(4, DataFormat.RulesSchemaVersion);

    [Fact]
    public void Version_de_schema_des_classes_du_lot_7_2() => Assert.Equal(3, DataFormat.ClassSchemaVersion);

    [Fact]
    public void Contenu_marque_provisoire() => Assert.Equal("0.4.1-provisoire", DataFormat.ProvisionalRulesContentVersion);
}
