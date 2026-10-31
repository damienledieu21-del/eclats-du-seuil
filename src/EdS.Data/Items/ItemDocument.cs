using System.Collections.Generic;
using EdS.Data.Equipment;
using EdS.Domain.Numerics;

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
        /// <summary>Sous-lot V1.2 (version 3) : places d'équipement du personnage (18 dans les Vallons), dans l'ordre de l'écran.</summary>
        public List<SlotData?>? Slots { get; set; }

        /// <summary>Places de l'inventaire (tranche : 24).</summary>
        public int? InventorySize { get; set; }

        /// <summary>Sous-lot V1.2 (version 3) : équipement de départ d'un nouveau personnage, porté d'emblée.</summary>
        public List<StartingItemData?>? Starting { get; set; }

        public List<ItemData?>? Items { get; set; }

        public List<LootTableData?>? Loot { get; set; }

        /// <summary>Sous-lot V1.3 (version 4) : ensembles (bonus à 2, 4 et 6 pièces portées).</summary>
        public List<SetData?>? Sets { get; set; }

        /// <summary>Sous-lot V1.3 (version 4) : tables des propriétés tirées (nombre par rareté, propriétés et leurs bornes par niveau).</summary>
        public PropertyTablesData? Properties { get; set; }
    }

    /// <summary>Sous-lot V1.3 : un ensemble tel que lu.</summary>
    public sealed class SetData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public List<SetBonusData?>? Bonuses { get; set; }
    }

    /// <summary>Sous-lot V1.3 : bonus d'un ensemble à un nombre de pièces portées.</summary>
    public sealed class SetBonusData
    {
        public int? Pieces { get; set; }

        /// <summary>Texte montré au joueur, relu par Damien.</summary>
        public string? Text { get; set; }

        /// <summary>Bonus aux statistiques par identifiant ; facultatif.</summary>
        public List<KeyValuePair<string, int?>>? Bonus { get; set; }

        /// <summary>Effets (modificateurs, déclencheurs) ; facultatifs.</summary>
        public EquipmentEffectData? Effects { get; set; }
    }

    /// <summary>Sous-lot V1.3 : tables des propriétés tirées telles que lues.</summary>
    public sealed class PropertyTablesData
    {
        public List<RarityRollData?>? Rarities { get; set; }
        public List<PropertyData?>? List { get; set; }
    }

    /// <summary>Sous-lot V1.3 : nombre de propriétés tirées et échelle des bornes pour une rareté.</summary>
    public sealed class RarityRollData
    {
        public string? Rarity { get; set; }
        public int? Count { get; set; }
        public int? ScalePercent { get; set; }
    }

    /// <summary>Sous-lot V1.3 : une propriété qui peut être tirée, telle que lue.</summary>
    public sealed class PropertyData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }

        /// <summary>Statistique augmentée (un champ du bonus d'équipement, « maxHealth »…).</summary>
        public string? Stat { get; set; }

        public int? Min { get; set; }
        public int? Max { get; set; }
        public int? MinPerLevel { get; set; }
        public int? MaxPerLevel { get; set; }

        /// <summary>Sortes de place où elle peut être tirée ; absente : toutes les places du combat.</summary>
        public List<string?>? Slots { get; set; }
    }

    /// <summary>Sous-lot V1.2 : une place d'équipement telle que lue.</summary>
    public sealed class SlotData
    {
        /// <summary>Identifiant de la place (« weapon », « ring_1 »…).</summary>
        public string? Id { get; set; }

        /// <summary>Sorte d'objet qu'elle reçoit (« ring » pour les deux places d'anneau) ; absente : son identifiant.</summary>
        public string? Accepts { get; set; }

        /// <summary>« combat » (par défaut), « shard » (l'Éclat du Seuil) ou « tool » (outil de métier, sans effet en combat).</summary>
        public string? Group { get; set; }

        /// <summary>Place toujours occupée, pour tous les personnages : on échange l'objet, on ne l'ôte pas.</summary>
        public bool? Required { get; set; }

        /// <summary>Classes pour lesquelles la place est toujours occupée (le bouclier du Bastion).</summary>
        public List<string?>? RequiredBy { get; set; }

        /// <summary>La place porte la garde du personnage : ses objets ont une garde propre (le bouclier).</summary>
        public bool? Guard { get; set; }

        /// <summary>Sous-lot V1.3 (version 4) : ses objets peuvent porter des effets (armes, boucliers et bijoux).</summary>
        public bool? Effects { get; set; }

        /// <summary>Champs effectivement présents.</summary>
        public HashSet<string> Present { get; } = new HashSet<string>();
    }

    /// <summary>Sous-lot V1.2 : objet de départ, pour toutes les classes ou pour une seule.</summary>
    public sealed class StartingItemData
    {
        public string? Item { get; set; }

        /// <summary>Classe qui le reçoit ; absente : toutes.</summary>
        public string? Class { get; set; }
    }

    /// <summary>Sous-lot V1.2 : garde propre d'un bouclier telle que lue.</summary>
    public sealed class ItemGuardData
    {
        public Fixed? DamageReduction { get; set; }
        public int? CostPerBlock { get; set; }
        public long? ParryWindowMs { get; set; }
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
        /// <summary>Sorte de place où il se porte (« weapon », « ring »…).</summary>
        public string? Slot { get; set; }

        public int? RequiredLevel { get; set; }

        /// <summary>Sous-lot 12.6 : objet de soin, PV rendus et recharge partagée (ms).</summary>
        public long? Heal { get; set; }
        public long? CooldownMs { get; set; }

        /// <summary>Bonus aux statistiques par identifiant (« armor »…), dans l'ordre du fichier.</summary>
        public List<KeyValuePair<string, int?>>? Bonus { get; set; }

        /// <summary>Sous-lot V1.2 : garde propre d'un bouclier (obligatoire pour un objet d'une place qui porte la garde, interdite ailleurs).</summary>
        public ItemGuardData? Guard { get; set; }

        /// <summary>Sous-lot V1.2 : arbres de talents auxquels l'objet convient (adéquation au build, H5), éventuellement aucun.</summary>
        public List<string?>? Fits { get; set; }

        /// <summary>Sous-lot V1.2 : icône peinte de l'objet (nom de fichier sans extension) ; absente : la fin de son identifiant.</summary>
        public string? Icon { get; set; }

        /// <summary>Sous-lot V1.3 (version 4) : objet nommé fait à la main (boss, quête, légendaire) : il ne tire aucune propriété.</summary>
        public bool? Named { get; set; }

        /// <summary>Sous-lot V1.3 (version 4) : ensemble dont l'objet est une pièce (« set.… ») ; facultatif.</summary>
        public string? Set { get; set; }

        /// <summary>Sous-lot V1.3 (version 4) : effets d'un objet nommé (texte, modificateurs, déclencheurs) ; facultatifs.</summary>
        public EquipmentEffectData? Effects { get; set; }

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
