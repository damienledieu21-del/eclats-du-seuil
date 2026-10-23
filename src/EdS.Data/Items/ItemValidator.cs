using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Validation;
using EdS.Domain.Collections;
using EdS.Domain.Stats;

namespace EdS.Data.Items
{
    /// <summary>Rareté d'un objet (cadrage Q30) : six rangs, du commun au mythique. « Secret » est une origine, pas un rang.</summary>
    public enum ItemRarity
    {
        Common = 0,
        Rare = 1,
        VeryRare = 2,
        Epic = 3,
        Legendary = 4,
        Mythic = 5,
    }

    public enum ItemType
    {
        /// <summary>Se porte à un emplacement ; ne s'empile pas.</summary>
        Equipment = 0,

        /// <summary>Matériau de fabrication ou butin ; s'empile.</summary>
        Material = 1,

        /// <summary>Sous-lot 12.6 : objet de soin (potion, remède) ; s'empile, se boit en combat (recharge partagée).</summary>
        Consumable = 2,
    }

    /// <summary>Objet du catalogue, validé et immuable.</summary>
    public sealed class ItemDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public ItemType Type { get; }
        public ItemRarity Rarity { get; }

        /// <summary>Taille d'une pile : 1 pour un équipement.</summary>
        public int Stack { get; }

        /// <summary>Emplacement d'un équipement ; null pour un matériau.</summary>
        public string? Slot { get; }

        /// <summary>Niveau requis pour le porter (1 pour un matériau).</summary>
        public int RequiredLevel { get; }

        /// <summary>Bonus aux statistiques quand il est porté (aucun pour un matériau).</summary>
        public GearBonus Bonus { get; }

        /// <summary>Sous-lot 12.6 : PV rendus par un objet de soin ; 0 sinon.</summary>
        public long Heal { get; }

        /// <summary>Sous-lot 12.6 : recharge partagée qui suit son usage, en ms ; 0 sinon.</summary>
        public long CooldownMs { get; }

        internal ItemDefinition(string id, string name, ItemType type, ItemRarity rarity, int stack, string? slot, int requiredLevel, GearBonus bonus,
            long heal = 0, long cooldownMs = 0)
        {
            Heal = heal;
            CooldownMs = cooldownMs;
            Id = id;
            Name = name;
            Type = type;
            Rarity = rarity;
            Stack = stack;
            Slot = slot;
            RequiredLevel = requiredLevel;
            Bonus = bonus;
        }
    }

    public sealed class LootDrop
    {
        public string Item { get; }
        public int Count { get; }

        /// <summary>Chance de tomber, en pour cent (1 à 100).</summary>
        public int ChancePercent { get; }

        internal LootDrop(string item, int count, int chancePercent)
        {
            Item = item;
            Count = count;
            ChancePercent = chancePercent;
        }
    }

    /// <summary>Table de butin : chaque objet tombe indépendamment, selon sa chance, dans l'ordre du fichier.</summary>
    public sealed class LootTable
    {
        public string Id { get; }
        public IReadOnlyList<LootDrop> Drops { get; }

        internal LootTable(string id, IEnumerable<LootDrop> drops)
        {
            Id = id;
            Drops = Frozen.List(drops, nameof(drops));
        }
    }

    /// <summary>
    /// Sous-lot 12.3 (cadrage Q4, Q5, Q30, H5) : catalogue validé des objets, emplacements d'équipement, taille de l'inventaire et tables de
    /// butin. Valeurs provisoires de la tranche, réglables dans les données.
    /// </summary>
    public sealed class ItemCatalog
    {
        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public IReadOnlyList<string> Slots { get; }
        public int InventorySize { get; }

        /// <summary>Objets, dans l'ordre du fichier.</summary>
        public IReadOnlyList<ItemDefinition> Items { get; }

        public IReadOnlyList<LootTable> Loot { get; }

        private readonly Dictionary<string, ItemDefinition> _items;
        private readonly Dictionary<string, LootTable> _loot;

        internal ItemCatalog(int schemaVersion, string contentVersion, IEnumerable<string> slots, int inventorySize, IEnumerable<ItemDefinition> items,
            IEnumerable<LootTable> loot)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            Slots = Frozen.List(slots, nameof(slots));
            InventorySize = inventorySize;
            Items = Frozen.List(items, nameof(items));
            Loot = Frozen.List(loot, nameof(loot));
            _items = Items.ToDictionary(i => i.Id, StringComparer.Ordinal);
            _loot = Loot.ToDictionary(l => l.Id, StringComparer.Ordinal);
        }

        public bool TryGetItem(string id, out ItemDefinition item) => _items.TryGetValue(id ?? string.Empty, out item!);

        public ItemDefinition Item(string id) =>
            TryGetItem(id, out ItemDefinition item) ? item : throw new ArgumentException($"Objet « {id} » absent du catalogue.", nameof(id));

        public bool TryGetLoot(string id, out LootTable table) => _loot.TryGetValue(id ?? string.Empty, out table!);
    }

    public sealed class ItemCatalogLoadResult
    {
        public ValidationReport Report { get; }
        public ItemCatalog? Catalog { get; }
        public bool IsValid => Catalog != null;

        internal ItemCatalogLoadResult(ValidationReport report, ItemCatalog? catalog)
        {
            Report = report;
            Catalog = catalog;
        }
    }

    /// <summary>
    /// Validateur unique du catalogue des objets (<c>kind: "items"</c>, sous-lot 12.3) : emplacements uniques, objets d'identifiant unique
    /// et de type connu (équipement : emplacement du catalogue, niveau requis dans les niveaux des règles, bonus connus et bornés, pas de
    /// pile ; matériau : pile de 1 à 999, ni emplacement ni bonus), rareté parmi les six rangs, tables de butin vers des objets connus, en
    /// quantité d'au plus une pile.
    /// </summary>
    public static class ItemValidator
    {
        public const string Kind = "items";

        /// <summary>
        /// Version 1 (sous-lot 12.3) : <c>slots</c>, <c>inventorySize</c>, <c>items[]</c>, <c>loot[]</c>. Version 2 (sous-lot 12.6) : type
        /// <c>consumable</c> (objet de soin : <c>stack</c>, <c>heal</c>, <c>cooldownMs</c>).
        /// </summary>
        public const int CurrentSchemaVersion = 2;

        public const int MaxSlots = 12;
        public const int MaxInventorySize = 200;
        public const int MaxItems = 4096;
        public const int MaxLootTables = 1024;
        public const int MaxDrops = 16;
        public const int MaxStack = 999;

        /// <summary>Sous-lot 12.6 : plus grand soin d'un objet, plus longue recharge (une heure), durée du tick des données (multiple obligé).</summary>
        public const long MaxHeal = 1_000_000, MaxCooldownMs = 3_600_000, TickMs = 50;

        /// <summary>Plus grand bonus d'un objet : douze objets portés restent sous <see cref="GearBonus.MaxValue"/>.</summary>
        public const int MaxItemBonus = 50_000;

        // Revue Codex de la PR #44 : tables globales réellement non modifiables (une table partagée modifiée changerait toutes les validations
        // suivantes du processus).
        public static IReadOnlyDictionary<string, ItemRarity> Rarities { get; } = new ReadOnlyDictionary<string, ItemRarity>(new Dictionary<string, ItemRarity>(StringComparer.Ordinal)
        {
            ["common"] = ItemRarity.Common,
            ["rare"] = ItemRarity.Rare,
            ["very_rare"] = ItemRarity.VeryRare,
            ["epic"] = ItemRarity.Epic,
            ["legendary"] = ItemRarity.Legendary,
            ["mythic"] = ItemRarity.Mythic,
        });

        public static IReadOnlyDictionary<string, ItemType> Types { get; } = new ReadOnlyDictionary<string, ItemType>(new Dictionary<string, ItemType>(StringComparer.Ordinal)
        {
            ["equipment"] = ItemType.Equipment,
            ["material"] = ItemType.Material,
            ["consumable"] = ItemType.Consumable,
        });

        public static string RarityId(ItemRarity rarity) => Rarities.First(r => r.Value == rarity).Key;

        private static readonly Regex ItemId = new Regex(@"^item(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);
        private static readonly Regex LootId = new Regex(@"^loot(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);
        private static readonly Regex SlotId = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

        public static bool IsItemId(string? id) => id != null && id.Length <= 200 && ItemId.IsMatch(id);

        public static bool IsSlotId(string? id) => id != null && id.Length <= 64 && SlotId.IsMatch(id);

        private static readonly string[] EquipmentFields = { "slot", "requiredLevel", "bonus" };
        private static readonly string[] MaterialFields = { "stack" };
        private static readonly string[] ConsumableFields = { "stack", "heal", "cooldownMs" };

        public static ItemCatalogLoadResult Validate(ItemCatalogDocument document, GameData data, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (data is null) throw new ArgumentNullException(nameof(data));
            report ??= new ValidationReport();
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, CurrentSchemaVersion, report))
                return new ItemCatalogLoadResult(report, null);
            ItemCatalogData? d = document.Data;
            if (d is null)
            {
                Missing(report, "$.data", "emplacements, taille de l'inventaire, objets, butin");
                return new ItemCatalogLoadResult(report, null);
            }

            var slots = new List<string>();
            if (d.Slots is null) Missing(report, "$.data.slots", "emplacements d'équipement");
            else
            {
                if (d.Slots.Count < 1 || d.Slots.Count > MaxSlots)
                    report.Add("$.data.slots", ValidationCode.OutOfRange, $"{d.Slots.Count} emplacements, de 1 à {MaxSlots} attendus.");
                for (int i = 0; i < d.Slots.Count; i++)
                {
                    string path = $"$.data.slots[{i}]";
                    if (d.Slots[i] is not string slot) Missing(report, path, "identifiant d'emplacement");
                    else if (!IsSlotId(slot)) report.Add(path, ValidationCode.InvalidValue, "emplacement invalide : minuscules, chiffres et « _ », commençant par une lettre.");
                    else if (slots.Contains(slot)) report.Add(path, ValidationCode.DuplicateId, $"emplacement « {slot} » déjà donné.");
                    else slots.Add(slot);
                }
            }
            if (d.InventorySize is null) Missing(report, "$.data.inventorySize", "nombre de places de l'inventaire");
            else if (d.InventorySize < 1 || d.InventorySize > MaxInventorySize)
                report.Add("$.data.inventorySize", ValidationCode.OutOfRange, $"inventaire hors de [1, {MaxInventorySize}] places.");

            int maxLevel = data.Rules.Progression.MaxLevel;
            var items = new List<ItemDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (d.Items is null) Missing(report, "$.data.items", "objets du catalogue");
            else
            {
                if (d.Items.Count > MaxItems) report.Add("$.data.items", ValidationCode.LimitExceeded, $"{d.Items.Count} objets, au plus {MaxItems}.");
                for (int i = 0; i < d.Items.Count; i++)
                {
                    string path = $"$.data.items[{i}]";
                    if (d.Items[i] is not ItemData item) { Missing(report, path, "objet"); continue; }
                    if (CheckItem(item, path, slots, maxLevel, ids, report) is { } definition) items.Add(definition);
                }
            }

            var loot = new List<LootTable>();
            if (d.Loot is null) Missing(report, "$.data.loot", "tables de butin, éventuellement vide");
            else
            {
                if (d.Loot.Count > MaxLootTables) report.Add("$.data.loot", ValidationCode.LimitExceeded, $"{d.Loot.Count} tables, au plus {MaxLootTables}.");
                var tables = new HashSet<string>(StringComparer.Ordinal);
                Dictionary<string, ItemDefinition> byId = items.ToDictionary(x => x.Id, StringComparer.Ordinal);
                for (int i = 0; i < d.Loot.Count; i++)
                {
                    string path = $"$.data.loot[{i}]";
                    if (d.Loot[i] is not LootTableData table) { Missing(report, path, "table de butin"); continue; }
                    if (CheckLoot(table, path, byId, ids, tables, report) is { } checkedTable) loot.Add(checkedTable);
                }
            }
            if (!report.IsValid || document.ContentVersion is null) return new ItemCatalogLoadResult(report, null);
            return new ItemCatalogLoadResult(report, new ItemCatalog(document.SchemaVersion!.Value, document.ContentVersion, slots, d.InventorySize!.Value, items, loot));
        }

        private static ItemDefinition? CheckItem(ItemData item, string path, List<string> slots, int maxLevel, HashSet<string> ids, ValidationReport report)
        {
            bool ok = true;
            if (item.Id is null) { Missing(report, path + ".id", "identifiant « item.… »"); ok = false; }
            else if (!IsItemId(item.Id)) { report.Add(path + ".id", ValidationCode.InvalidValue, "identifiant invalide : « item. » puis minuscules, chiffres, « _ » et « . »."); ok = false; }
            else if (!ids.Add(item.Id)) { report.Add(path + ".id", ValidationCode.DuplicateId, $"objet « {item.Id} » déjà décrit plus haut."); ok = false; }
            if (string.IsNullOrWhiteSpace(item.Name)) { Missing(report, path + ".name", "nom affiché"); ok = false; }
            ItemRarity rarity = default;
            if (item.Rarity is null) { Missing(report, path + ".rarity", string.Join(", ", Rarities.Keys)); ok = false; }
            else if (!Rarities.TryGetValue(item.Rarity, out rarity))
            {
                report.Add(path + ".rarity", ValidationCode.UnknownReference, $"rareté inconnue « {item.Rarity} » ; valeurs possibles : {string.Join(", ", Rarities.Keys)}.");
                ok = false;
            }
            if (item.Type is null) { Missing(report, path + ".type", "equipment, material ou consumable"); return null; }
            if (!Types.TryGetValue(item.Type, out ItemType type))
            {
                report.Add(path + ".type", ValidationCode.UnknownReference, $"type d'objet inconnu « {item.Type} » ; valeurs possibles : {string.Join(", ", Types.Keys)}.");
                return null;
            }
            string[] own = type == ItemType.Equipment ? EquipmentFields : type == ItemType.Material ? MaterialFields : ConsumableFields;
            foreach (string field in EquipmentFields.Concat(ConsumableFields).Distinct().Where(f => !own.Contains(f) && item.Present.Contains(f)))
            {
                report.Add($"{path}.{field}", ValidationCode.InvalidValue, $"champ « {field} » sans objet pour le type {item.Type}.");
                ok = false;
            }
            if (type != ItemType.Equipment)
            {
                if (item.Stack is null) { Missing(report, path + ".stack", "taille d'une pile"); ok = false; }
                else if (item.Stack < 1 || item.Stack > MaxStack) { report.Add(path + ".stack", ValidationCode.OutOfRange, $"pile hors de [1, {MaxStack}]."); ok = false; }
                long heal = 0, cooldown = 0;
                if (type == ItemType.Consumable)
                {
                    // Sous-lot 12.6 : un objet de soin rend des PV (au plus jusqu'au maximum) et lance la recharge partagée.
                    if (item.Heal is null) { Missing(report, path + ".heal", "PV rendus"); ok = false; }
                    else if (item.Heal < 1 || item.Heal > MaxHeal) { report.Add(path + ".heal", ValidationCode.OutOfRange, $"soin hors de [1, {MaxHeal}]."); ok = false; }
                    else heal = item.Heal.Value;
                    if (item.CooldownMs is null) { Missing(report, path + ".cooldownMs", "recharge partagée en ms"); ok = false; }
                    else if (item.CooldownMs < 0 || item.CooldownMs > MaxCooldownMs || item.CooldownMs % TickMs != 0)
                    { report.Add(path + ".cooldownMs", ValidationCode.OutOfRange, $"recharge hors de [0, {MaxCooldownMs}] ms ou pas un multiple de {TickMs} ms."); ok = false; }
                    else cooldown = item.CooldownMs.Value;
                }
                return ok ? new ItemDefinition(item.Id!, item.Name!, type, rarity, item.Stack!.Value, null, 1, GearBonus.None, heal, cooldown) : null;
            }
            if (item.Slot is null) { Missing(report, path + ".slot", "emplacement : " + string.Join(", ", slots)); ok = false; }
            else if (!slots.Contains(item.Slot))
            {
                report.Add(path + ".slot", ValidationCode.UnknownReference, $"emplacement inconnu « {item.Slot} » ; emplacements : {string.Join(", ", slots)}.");
                ok = false;
            }
            if (item.RequiredLevel is null) { Missing(report, path + ".requiredLevel", "niveau requis"); ok = false; }
            else if (item.RequiredLevel < 1 || item.RequiredLevel > maxLevel)
            {
                report.Add(path + ".requiredLevel", ValidationCode.OutOfRange, $"niveau requis hors de [1, {maxLevel}] (progression.maxLevel des règles).");
                ok = false;
            }
            var values = new int[GearBonus.Fields.Count];
            if (item.Bonus is null) { Missing(report, path + ".bonus", "bonus aux statistiques, éventuellement vide"); ok = false; }
            else
            {
                // Revue Codex de la PR #44 : un bonus une seule fois (catalogue construit en objets publics), jamais écrasé par un second.
                var seenBonus = new HashSet<string>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, int?> entry in item.Bonus)
                {
                    string bonusPath = $"{path}.bonus.{entry.Key}";
                    int field = GearBonus.FieldIndex(entry.Key);
                    if (field < 0)
                    {
                        report.Add(bonusPath, ValidationCode.UnknownReference, $"bonus inconnu « {entry.Key} » ; bonus possibles : {string.Join(", ", GearBonus.Fields)}.");
                        ok = false;
                    }
                    else if (!seenBonus.Add(entry.Key)) { report.Add(bonusPath, ValidationCode.DuplicateId, $"bonus « {entry.Key} » donné deux fois."); ok = false; }
                    else if (entry.Value is not int value) { Missing(report, bonusPath, "entier"); ok = false; }
                    else if (value < 0 || value > MaxItemBonus) { report.Add(bonusPath, ValidationCode.OutOfRange, $"bonus hors de [0, {MaxItemBonus}]."); ok = false; }
                    else values[field] = value;
                }
            }
            return ok ? new ItemDefinition(item.Id!, item.Name!, type, rarity, 1, item.Slot, item.RequiredLevel!.Value, GearBonus.FromValues(values)) : null;
        }

        private static LootTable? CheckLoot(LootTableData table, string path, Dictionary<string, ItemDefinition> items, HashSet<string> itemIds,
            HashSet<string> tables, ValidationReport report)
        {
            bool ok = true;
            if (table.Id is null) { Missing(report, path + ".id", "identifiant « loot.… »"); ok = false; }
            else if (table.Id.Length > 200 || !LootId.IsMatch(table.Id))
            {
                report.Add(path + ".id", ValidationCode.InvalidValue, "identifiant invalide : « loot. » puis minuscules, chiffres, « _ » et « . ».");
                ok = false;
            }
            else if (!tables.Add(table.Id)) { report.Add(path + ".id", ValidationCode.DuplicateId, $"table « {table.Id} » déjà décrite plus haut."); ok = false; }
            var drops = new List<LootDrop>();
            if (table.Drops is null) { Missing(report, path + ".drops", "objets qui peuvent tomber"); return null; }
            if (table.Drops.Count < 1 || table.Drops.Count > MaxDrops)
            {
                report.Add(path + ".drops", ValidationCode.OutOfRange, $"{table.Drops.Count} objets, de 1 à {MaxDrops} attendus.");
                ok = false;
            }
            for (int i = 0; i < table.Drops.Count; i++)
            {
                string dropPath = $"{path}.drops[{i}]";
                if (table.Drops[i] is not LootDropData drop) { Missing(report, dropPath, "objet qui peut tomber"); ok = false; continue; }
                ItemDefinition? item = null;
                if (drop.Item is null) { Missing(report, dropPath + ".item", "identifiant d'objet"); ok = false; }
                else if (!items.TryGetValue(drop.Item, out item))
                {
                    // Un objet refusé plus haut est déjà signalé : pas de seconde erreur pour la même cause.
                    if (!itemIds.Contains(drop.Item)) report.Add(dropPath + ".item", ValidationCode.UnknownReference, $"objet inconnu « {drop.Item} ».");
                    ok = false;
                }
                if (drop.Count is null) { Missing(report, dropPath + ".count", "quantité"); ok = false; }
                else if (drop.Count < 1 || (item != null && drop.Count > item.Stack))
                {
                    report.Add(dropPath + ".count", ValidationCode.OutOfRange, $"quantité hors de [1, {item?.Stack ?? MaxStack}] (au plus une pile).");
                    ok = false;
                }
                if (drop.ChancePercent is null) { Missing(report, dropPath + ".chancePercent", "chance en pour cent"); ok = false; }
                else if (drop.ChancePercent < 1 || drop.ChancePercent > 100)
                {
                    report.Add(dropPath + ".chancePercent", ValidationCode.OutOfRange, "chance hors de [1, 100] %.");
                    ok = false;
                }
                if (ok) drops.Add(new LootDrop(drop.Item!, drop.Count!.Value, drop.ChancePercent!.Value));
            }
            return ok ? new LootTable(table.Id!, drops) : null;
        }

        private static void Missing(ValidationReport report, string path, string what)
        {
            if (!report.HasErrorAtOrAbove(path)) report.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({what}).");
        }
    }
}
