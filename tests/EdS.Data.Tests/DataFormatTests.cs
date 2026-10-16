using EdS.Data;

namespace EdS.Data.Tests;

public class DataFormatTests
{
    [Fact]
    public void Version_de_schema_des_regles_du_lot_7_1() => Assert.Equal(3, DataFormat.RulesSchemaVersion);

    [Fact]
    public void Version_de_schema_des_classes_inchangee() => Assert.Equal(2, DataFormat.ClassSchemaVersion);

    [Fact]
    public void Contenu_marque_provisoire() => Assert.Equal("0.3.0-provisoire", DataFormat.ProvisionalRulesContentVersion);
}
