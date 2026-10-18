using EdS.Data;

namespace EdS.Data.Tests;

public class DataFormatTests
{
    [Fact]
    public void Version_de_schema_des_regles_du_lot_8() => Assert.Equal(6, DataFormat.RulesSchemaVersion);   // décision du 18/10/2026 : plafonds dans les règles

    [Fact]
    public void Version_de_schema_des_classes_du_sous_lot_8_1() => Assert.Equal(5, DataFormat.ClassSchemaVersion);

    [Fact]
    public void Contenu_marque_provisoire() => Assert.Equal("0.6.0-provisoire", DataFormat.ProvisionalRulesContentVersion);
}
