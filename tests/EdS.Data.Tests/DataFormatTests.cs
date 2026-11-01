using EdS.Data;

namespace EdS.Data.Tests;

public class DataFormatTests
{
    [Fact]
    public void Version_de_schema_des_regles_du_lot_8() => Assert.Equal(14, DataFormat.RulesSchemaVersion);   // 14 : bornes des effets d'équipement (V1.3) ; 6 : plafonds (18/10/2026) ; 7 : renforcements et boucliers (8.2) ; 8 : provocation, protection (8.3) ; 9 : L8-D2, guard.noUpkeep (8.4) ; 10 : déplacement et contact (11.4a) ; 11 : portée des protections (11.4b) ; 12 : arc du bouclier, réaction des annonces (11.4c) ; 13 : bornes de la garde des boucliers (V1.2)

    [Fact]
    public void Version_de_schema_des_classes_du_lot_8() => Assert.Equal(9, DataFormat.ClassSchemaVersion);   // 5 : modificateurs (8.1) ; 6 : shield.potencyDealt (8.2) ; 7 : soutien et protections (8.3) ; 8 : Charges dépensées, guard.noUpkeep (8.4) ; 9 : portées (11.4b)

    [Fact]
    public void Contenu_marque_provisoire() => Assert.Equal("0.15.0-provisoire", DataFormat.ProvisionalRulesContentVersion);   // 0.15.0 : barre de V1.8 ; 0.14.0 : effets d'équipement (V1.3) ; 0.13.0 : garde des boucliers (V1.2) ; 0.12.0 : 128 combattants (29 octobre 2026) ; 0.8.0 : altérations du Gardien (8.5) ; 0.9.0 : déplacement et contact (11.4a) ; 0.10.0 : portée du lien protecteur (11.4b) ; 0.11.0 : arc du bouclier, réaction des annonces (11.4c) ; 13 : bornes de la garde des boucliers (V1.2)
}
