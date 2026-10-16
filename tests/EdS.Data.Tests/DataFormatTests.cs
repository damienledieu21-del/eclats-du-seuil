using EdS.Data;

namespace EdS.Data.Tests;

public class DataFormatTests
{
    [Fact]
    public void Version_de_schema_des_regles_du_lot_7_3() => Assert.Equal(5, DataFormat.RulesSchemaVersion);

    [Fact]
    public void Version_de_schema_des_classes_du_sous_lot_8_1() => Assert.Equal(5, DataFormat.ClassSchemaVersion);

    [Fact]
    public void Contenu_marque_provisoire() => Assert.Equal("0.5.0-provisoire", DataFormat.ProvisionalRulesContentVersion);
}
