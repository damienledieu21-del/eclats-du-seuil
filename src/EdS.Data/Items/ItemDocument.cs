using System.Collections.Generic;

namespace EdS.Data.Items
{
    /// <summary>Sous-lot 12.3 : catalogue des objets tel que lu dans le fichier, avant validation.</summary>
    public sealed class ItemCatalogDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
        public ItemCatalogData? Data { get; set; }
    }

    public sealed class ItemCatalogData
    {
        /// <summary>Emplacements d'équipement (tranche : quatre, dont le bouclier).</summary>
        public List<string?>? Slots { get; set; }

        /// <summary>Places de l'inventaire (tranche : 24).</summary>
        public int? InventorySize { get; set; }

        public List<ItemData?>? Items { get; set; }

        public List<LootTableData?>? Loot { get; set; }
    }

    public sealed class ItemData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }

        /// <summary>« equipment », « material » ou « consumable » (sous-lot 12.6).</summary>
        public string? Type { get; set; }

        /// <summary>Rareté (« common »… « mythic »).</summary>
        public string? Rarity { get; set; }

        /// <summary>Taille d'une pile (matériau) ; un équipement ne s'empile pas.</summary>
        public int? Stack { get; set; }

        // Équipement
        public string? Slot { get; set; }
        public int? RequiredLevel { get; set; }

        /// <summary>Sous-lot 12.6 : objet de soin, PV rendus et recharge partagée (ms).</summary>
        public long? Heal { get; set; }
        public long? CooldownMs { get; set; }

        /// <summary>Bonus aux statistiques par identifiant (« armor »…), dans l'ordre du fichier.</summary>
        public List<KeyValuePair<string, int?>>? Bonus { get; set; }

        /// <summary>Champs effectivement présents, pour refuser ceux d'un autre type.</summary>
        public HashSet<string> Present { get; } = new HashSet<string>();
    }

    public sealed class LootTableData
    {
        public string? Id { get; set; }
        public List<LootDropData?>? Drops { get; set; }
    }

    public sealed class LootDropData
    {
        public string? Item { get; set; }
        public int? Count { get; set; }

        /// <summary>Chance de tomber, en pour cent (1 à 100).</summary>
        public int? ChancePercent { get; set; }
    }
}
