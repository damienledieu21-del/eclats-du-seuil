using EdS.Data;

namespace EdS.Data.Tests;

public class DataFormatTests
{
    [Fact]
    public void Version_de_schema_des_regles_du_lot_8() => Assert.Equal(8, DataFormat.RulesSchemaVersion);   // 6 : plafonds (18/10/2026) ; 7 : renforcements et boucliers (8.2) ; 8 : provocation, protection (8.3)

    [Fact]
    public void Version_de_schema_des_classes_du_lot_8() => Assert.Equal(7, DataFormat.ClassSchemaVersion);   // 5 : modificateurs (8.1) ; 6 : shield.potencyDealt (8.2) ; 7 : soutien et protections (8.3)

    [Fact]
    public void Contenu_marque_provisoire() => Assert.Equal("0.6.0-provisoire", DataFormat.ProvisionalRulesContentVersion);
}
