using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Validation;
using EdS.Domain;
using EdS.Domain.Collections;
using EdS.Domain.Combat;
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
        /// <summary>Se porte à une place ; ne s'empile pas.</summary>
        Equipment = 0,

        /// <summary>Matériau de fabrication ou butin ; s'empile.</summary>
        Material = 1,

        /// <summary>Sous-lot 12.6 : objet de soin (potion, remède) ; s'empile, se boit en combat (recharge partagée).</summary>
        Consumable = 2,
    }

    /// <summary>Sous-lot V1.2 : groupe d'une place d'équipement.</summary>
    public enum PlaceGroup
    {
        /// <summary>Les 16 places du combat (arme, bouclier, armure, bijoux, reliques) : leurs bonus comptent en combat.</summary>
        Combat = 0,

        /// <summary>L'Éclat du Seuil du personnage ; il grandit avec l'histoire.</summary>
        Shard = 1,

        /// <summary>L'outil de métier : utile hors combat, sans bonus de combat.</summary>
        Tool = 2,
    }

    /// <summary>
    /// Sous-lot V1.2 : une place d'équipement du personnage. Plusieurs places peuvent recevoir la même sorte d'objet (deux anneaux, deux
    /// reliques). Une place obligatoire (<see cref="Required"/>, ou <see cref="RequiredBy"/> pour certaines classes) reste toujours occupée :
    /// on y échange l'objet, on ne l'ôte pas. La place qui porte la garde (<see cref="Guard"/>) reçoit les boucliers, dont la garde propre
    /// remplace celle des règles en combat.
    /// </summary>
    public sealed class EquipmentPlace
    {
        public string Id { get; }

        /// <summary>Sorte d'objet reçue (le <c>slot</c> des objets) ; son identifiant pour une place unique.</summary>
        public string Accepts { get; }

        public PlaceGroup Group { get; }
        public bool Required { get; }

        /// <summary>Classes pour lesquelles la place est obligatoire (vide : seulement <see cref="Required"/>).</summary>
        public IReadOnlyList<string> RequiredBy { get; }

        public bool Guard { get; }

        internal EquipmentPlace(string id, string accepts, PlaceGroup group, bool required, IEnumerable<string> requiredBy, bool guard)
        {
            Id = id;
            Accepts = accepts;
            Group = group;
            Required = required;
            RequiredBy = Frozen.List(requiredBy, nameof(requiredBy));
            Guard = guard;
        }

        /// <summary>La place est-elle toujours occupée pour un personnage de la classe <paramref name="classId"/> ?</summary>
        public bool IsRequiredFor(string? classId) => Required || (classId != null && RequiredBy.Contains(classId));
    }

    /// <summary>Sous-lot V1.2 : objet de départ, porté d'emblée par un nouveau personnage (de la classe donnée, ou de toutes).</summary>
    public sealed class StartingItem
    {
        public string Item { get; }

        /// <summary>Classe qui le reçoit ; null : toutes.</summary>
        public string? Class { get; }

        internal StartingItem(string item, string? @class)
        {
            Item = item;
            Class = @class;
        }
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

        /// <summary>Sorte de place où se porte un équipement (« weapon », « ring »…) ; null pour un matériau ou un objet de soin.</summary>
        public string? Slot { get; }

        /// <summary>Niveau requis pour le porter (1 pour un matériau).</summary>
        public int RequiredLevel { get; }

        /// <summary>Bonus aux statistiques quand il est porté (aucun pour un matériau).</summary>
        public GearBonus Bonus { get; }

        /// <summary>Sous-lot 12.6 : PV rendus par un objet de soin ; 0 sinon.</summary>
        public long Heal { get; }

        /// <summary>Sous-lot 12.6 : recharge partagée qui suit son usage, en ms ; 0 sinon.</summary>
        public long CooldownMs { get; }

        /// <summary>Sous-lot V1.2 : garde propre d'un bouclier (place qui porte la garde) ; null sinon.</summary>
        public ShieldGuard? Guard { get; }

        /// <summary>
        /// Sous-lot V1.2 (H5, adéquation au build) : arbres de talents auxquels l'objet convient, dans l'ordre du fichier ; vide : à tous.
        /// Une indication pour le joueur, sans effet sur le jeu.
        /// </summary>
        public IReadOnlyList<string> Fits { get; }

        /// <summary>Sous-lot V1.2 : icône peinte (nom de fichier sans extension) : celle du fichier, sinon la fin de l'identifiant.</summary>
        public string Icon { get; }

        internal ItemDefinition(string id, string name, ItemType type, ItemRarity rarity, int stack, string? slot, int requiredLevel, GearBonus bonus,
            long heal = 0, long cooldownMs = 0, ShieldGuard? guard = null, IEnumerable<string>? fits = null, string? icon = null)
        {
            Icon = icon ?? id.Substring(id.LastIndexOf('.') + 1);
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
            Guard = guard;
            Fits = Frozen.ListOrEmpty(fits, nameof(fits));
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
    /// Sous-lot 12.3 (cadrage Q4, Q5, Q30, H5) : catalogue validé des objets, places d'équipement, taille de l'inventaire et tables de butin.
    /// Sous-lot V1.2 : 18 places (cadrage Q4, choix de Damien du 29 octobre 2026), garde des boucliers, équipement de départ. Valeurs
    /// provisoires, réglables dans les données.
    /// </summary>
    public sealed class ItemCatalog
    {
        public int SchemaVersion { get; }
        public string ContentVersion { get; }

        /// <summary>Places d'équipement, dans l'ordre du fichier (celui de l'écran).</summary>
        public IReadOnlyList<EquipmentPlace> Places { get; }

        /// <summary>Identifiants des places, dans l'ordre du fichier.</summary>
        public IReadOnlyList<string> Slots { get; }

        public int InventorySize { get; }

        /// <summary>Sous-lot V1.2 : équipement de départ, dans l'ordre du fichier.</summary>
        public IReadOnlyList<StartingItem> Starting { get; }

        /// <summary>Objets, dans l'ordre du fichier.</summary>
        public IReadOnlyList<ItemDefinition> Items { get; }

        public IReadOnlyList<LootTable> Loot { get; }

        private readonly Dictionary<string, ItemDefinition> _items;
        private readonly Dictionary<string, LootTable> _loot;
        private readonly Dictionary<string, EquipmentPlace> _places;

        internal ItemCatalog(int schemaVersion, string contentVersion, IEnumerable<EquipmentPlace> places, int inventorySize, IEnumerable<StartingItem> starting,
            IEnumerable<ItemDefinition> items, IEnumerable<LootTable> loot)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            Places = Frozen.List(places, nameof(places));
            Slots = Frozen.List(Places.Select(p => p.Id), nameof(places));
            InventorySize = inventorySize;
            Starting = Frozen.List(starting, nameof(starting));
            Items = Frozen.List(items, nameof(items));
            Loot = Frozen.List(loot, nameof(loot));
            _items = Items.ToDictionary(i => i.Id, StringComparer.Ordinal);
            _loot = Loot.ToDictionary(l => l.Id, StringComparer.Ordinal);
            _places = Places.ToDictionary(p => p.Id, StringComparer.Ordinal);
        }

        public bool TryGetItem(string id, out ItemDefinition item) => _items.TryGetValue(id ?? string.Empty, out item!);

        public ItemDefinition Item(string id) =>
            TryGetItem(id, out ItemDefinition item) ? item : throw new ArgumentException($"Objet « {id} » absent du catalogue.", nameof(id));

        public bool TryGetLoot(string id, out LootTable table) => _loot.TryGetValue(id ?? string.Empty, out table!);

        public bool TryGetPlace(string id, out EquipmentPlace place) => _places.TryGetValue(id ?? string.Empty, out place!);

        /// <summary>Places qui reçoivent la sorte d'objet <paramref name="kind"/>, dans l'ordre du fichier (vide si aucune).</summary>
        public IReadOnlyList<EquipmentPlace> PlacesFor(string? kind) => Places.Where(p => p.Accepts == kind).ToList();

        /// <summary>La place qui porte la garde (le bouclier), ou null.</summary>
        public EquipmentPlace? GuardPlace => Places.FirstOrDefault(p => p.Guard);

        /// <summary>Objets de départ d'un personnage de la classe <paramref name="classId"/>, dans l'ordre du fichier.</summary>
        public IEnumerable<string> StartingFor(string classId) => Starting.Where(s => s.Class is null || s.Class == classId).Select(s => s.Item);
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
    /// Validateur unique du catalogue des objets (<c>kind: "items"</c>, sous-lot 12.3, version 3 au sous-lot V1.2) : places uniques et
    /// cohérentes, objets d'identifiant unique et de type connu (équipement : sorte de place du catalogue, niveau requis dans les niveaux des
    /// règles, bonus connus et bornés, garde d'un bouclier dans les bornes des règles, arbres de talents connus, pas de pile ; matériau et
    /// objet de soin : pile de 1 à 999, ni place ni bonus), rareté parmi les six rangs, équipement de départ qui remplit les places
    /// obligatoires, tables de butin vers des objets connus, en quantité d'au plus une pile.
    /// </summary>
    public static class ItemValidator
    {
        public const string Kind = "items";

        /// <summary>
        /// Version 1 (sous-lot 12.3) : <c>slots</c>, <c>inventorySize</c>, <c>items[]</c>, <c>loot[]</c>. Version 2 (sous-lot 12.6) : type
        /// <c>consumable</c> (objet de soin : <c>stack</c>, <c>heal</c>, <c>cooldownMs</c>). Version 3 (sous-lot V1.2) : les places deviennent des
        /// objets (<c>id</c>, <c>accepts</c>, <c>group</c>, <c>required</c>, <c>requiredBy</c>, <c>guard</c>), <c>starting[]</c> (équipement de
        /// départ), <c>guard</c> (garde d'un bouclier) et <c>fits</c> (arbres de talents) sur un équipement. Une version antérieure est refusée :
        /// le fichier se met à jour à la main. Les effets, les ensembles et les propriétés tirées (sous-lot V1.3) viendront avec une version
        /// suivante, en champs nouveaux d'un objet : la forme actuelle n'a rien à casser pour les accueillir.
        /// </summary>
        public const int CurrentSchemaVersion = 3;

        /// <summary>Places au plus : vingt objets au bonus maximal restent sous <see cref="GearBonus.MaxValue"/>.</summary>
        public const int MaxSlots = 20;
        public const int MaxInventorySize = 200;
        public const int MaxItems = 4096;
        public const int MaxLootTables = 1024;
        public const int MaxDrops = 16;
        public const int MaxStack = 999;
        public const int MaxStarting = 32;
        public const int MaxFits = 8;

        /// <summary>Sous-lot 12.6 : plus grand soin d'un objet, plus longue recharge (une heure), durée du tick des données (multiple obligé).</summary>
        public const long MaxHeal = 1_000_000, MaxCooldownMs = 3_600_000, TickMs = 50;

        /// <summary>Plus grand bonus d'un objet : <see cref="MaxSlots"/> objets portés restent sous <see cref="GearBonus.MaxValue"/>.</summary>
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

        /// <summary>Sous-lot V1.2 : groupes des places.</summary>
        public static IReadOnlyDictionary<string, PlaceGroup> Groups { get; } = new ReadOnlyDictionary<string, PlaceGroup>(new Dictionary<string, PlaceGroup>(StringComparer.Ordinal)
        {
            ["combat"] = PlaceGroup.Combat,
            ["shard"] = PlaceGroup.Shard,
            ["tool"] = PlaceGroup.Tool,
        });

        public static string RarityId(ItemRarity rarity) => Rarities.First(r => r.Value == rarity).Key;

        private static readonly Regex ItemId = new Regex(@"^item(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);
        private static readonly Regex LootId = new Regex(@"^loot(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);
        private static readonly Regex SlotId = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);
        private static readonly Regex IconName = new Regex(@"^[a-z0-9_]+\z", RegexOptions.CultureInvariant);

        public static bool IsItemId(string? id) => id != null && id.Length <= 200 && ItemId.IsMatch(id);

        public static bool IsSlotId(string? id) => id != null && id.Length <= 64 && SlotId.IsMatch(id);

        /// <summary>Le champ est-il renseigné dans l'objet (indépendamment du lecteur JSON) ?</summary>
        private static bool IsSet(ItemData item, string field) => field switch
        {
            "slot" => item.Slot != null,
            "requiredLevel" => item.RequiredLevel != null,
            "bonus" => item.Bonus != null,
            "guard" => item.Guard != null,
            "fits" => item.Fits != null,
            "stack" => item.Stack != null,
            "heal" => item.Heal != null,
            "cooldownMs" => item.CooldownMs != null,
            _ => false,
        };

        private static readonly string[] EquipmentFields = { "slot", "requiredLevel", "bonus", "guard", "fits" };
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
                Missing(report, "$.data", "places, taille de l'inventaire, équipement de départ, objets, butin");
                return new ItemCatalogLoadResult(report, null);
            }

            var places = new List<EquipmentPlace>();
            if (d.Slots is null) Missing(report, "$.data.slots", "places d'équipement");
            else
            {
                if (d.Slots.Count < 1 || d.Slots.Count > MaxSlots)
                    report.Add("$.data.slots", ValidationCode.OutOfRange, $"{d.Slots.Count} places, de 1 à {MaxSlots} attendues.");
                for (int i = 0; i < d.Slots.Count; i++)
                {
                    string path = $"$.data.slots[{i}]";
                    if (d.Slots[i] is not SlotData slot) { Missing(report, path, "place d'équipement"); continue; }
                    if (CheckPlace(slot, path, places, data, report) is { } place) places.Add(place);
                }
                if (places.Count(p => p.Guard) > 1)
                    report.Add("$.data.slots", ValidationCode.InvalidValue, "une seule place porte la garde (le bouclier).");
            }
            if (d.InventorySize is null) Missing(report, "$.data.inventorySize", "nombre de places de l'inventaire");
            else if (d.InventorySize < 1 || d.InventorySize > MaxInventorySize)
                report.Add("$.data.inventorySize", ValidationCode.OutOfRange, $"inventaire hors de [1, {MaxInventorySize}] places.");

            int maxLevel = data.Rules.Progression.MaxLevel;
            var trees = new HashSet<string>(data.Classes.SelectMany(c => c.Definition.Trees).Select(t => t.Id), StringComparer.Ordinal);
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
                    if (CheckItem(item, path, places, maxLevel, trees, data.Rules.Combat, ids, report) is { } definition) items.Add(definition);
                }
            }
            Dictionary<string, ItemDefinition> byId = items.ToDictionary(x => x.Id, StringComparer.Ordinal);

            var starting = new List<StartingItem>();
            if (d.Starting is null) Missing(report, "$.data.starting", "équipement de départ, éventuellement vide");
            else CheckStarting(d.Starting, places, byId, ids, data, starting, report);

            var loot = new List<LootTable>();
            if (d.Loot is null) Missing(report, "$.data.loot", "tables de butin, éventuellement vide");
            else
            {
                if (d.Loot.Count > MaxLootTables) report.Add("$.data.loot", ValidationCode.LimitExceeded, $"{d.Loot.Count} tables, au plus {MaxLootTables}.");
                var tables = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < d.Loot.Count; i++)
                {
                    string path = $"$.data.loot[{i}]";
                    if (d.Loot[i] is not LootTableData table) { Missing(report, path, "table de butin"); continue; }
                    if (CheckLoot(table, path, byId, ids, tables, report) is { } checkedTable) loot.Add(checkedTable);
                }
            }
            if (!report.IsValid || document.ContentVersion is null) return new ItemCatalogLoadResult(report, null);
            return new ItemCatalogLoadResult(report, new ItemCatalog(document.SchemaVersion!.Value, document.ContentVersion, places, d.InventorySize!.Value,
                starting, items, loot));
        }

        private static EquipmentPlace? CheckPlace(SlotData slot, string path, List<EquipmentPlace> before, GameData data, ValidationReport report)
        {
            bool ok = true;
            if (slot.Id is null) { Missing(report, path + ".id", "identifiant de place"); ok = false; }
            else if (!IsSlotId(slot.Id)) { report.Add(path + ".id", ValidationCode.InvalidValue, "place invalide : minuscules, chiffres et « _ », commençant par une lettre."); ok = false; }
            else if (before.Any(p => p.Id == slot.Id)) { report.Add(path + ".id", ValidationCode.DuplicateId, $"place « {slot.Id} » déjà donnée."); ok = false; }
            string accepts = slot.Accepts ?? slot.Id ?? "";
            if (slot.Accepts != null && !IsSlotId(slot.Accepts))
            {
                report.Add(path + ".accepts", ValidationCode.InvalidValue, "sorte d'objet invalide : minuscules, chiffres et « _ », commençant par une lettre.");
                ok = false;
            }
            PlaceGroup group = PlaceGroup.Combat;
            if (slot.Group != null && !Groups.TryGetValue(slot.Group, out group))
            {
                report.Add(path + ".group", ValidationCode.UnknownReference, $"groupe inconnu « {slot.Group} » ; groupes : {string.Join(", ", Groups.Keys)}.");
                ok = false;
            }
            bool required = slot.Required ?? false, guard = slot.Guard ?? false;
            var requiredBy = new List<string>();
            if (slot.RequiredBy != null)
                for (int k = 0; k < slot.RequiredBy.Count; k++)
                {
                    string rp = $"{path}.requiredBy[{k}]";
                    if (slot.RequiredBy[k] is not string c) { Missing(report, rp, "identifiant de classe"); ok = false; }
                    else if (!data.TryGetClass(c, out _)) { report.Add(rp, ValidationCode.UnknownReference, $"classe inconnue « {c} »."); ok = false; }
                    else if (requiredBy.Contains(c)) { report.Add(rp, ValidationCode.DuplicateId, $"classe « {c} » déjà donnée."); ok = false; }
                    else requiredBy.Add(c);
                }
            if (required && requiredBy.Count > 0)
            {
                report.Add(path + ".requiredBy", ValidationCode.InvalidValue, "place déjà obligatoire pour toutes les classes (required).");
                ok = false;
            }
            if (guard && group != PlaceGroup.Combat)
            {
                report.Add(path + ".guard", ValidationCode.InvalidValue, "la garde se porte à une place du combat.");
                ok = false;
            }
            // Les places d'une même sorte sont interchangeables : même groupe, même garde.
            if (before.FirstOrDefault(p => p.Accepts == accepts) is { } same && (same.Group != group || same.Guard != guard))
            {
                report.Add(path + ".accepts", ValidationCode.InvalidValue, $"la place « {same.Id} » reçoit déjà « {accepts} » avec un autre groupe ou une autre garde.");
                ok = false;
            }
            return ok ? new EquipmentPlace(slot.Id!, accepts, group, required, requiredBy, guard) : null;
        }

        private static ItemDefinition? CheckItem(ItemData item, string path, List<EquipmentPlace> places, int maxLevel, HashSet<string> trees, CombatRules rules,
            HashSet<string> ids, ValidationReport report)
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
            // Sous-lot V1.2 : icône facultative, un nom de fichier simple (minuscules, chiffres, « _ »).
            if (item.Icon != null && (item.Icon.Length > 64 || !IconName.IsMatch(item.Icon)))
            {
                report.Add(path + ".icon", ValidationCode.InvalidValue, "icône invalide : minuscules, chiffres et « _ », 64 caractères au plus.");
                ok = false;
            }
            if (item.Type is null) { Missing(report, path + ".type", "equipment, material ou consumable"); return null; }
            if (!Types.TryGetValue(item.Type, out ItemType type))
            {
                report.Add(path + ".type", ValidationCode.UnknownReference, $"type d'objet inconnu « {item.Type} » ; valeurs possibles : {string.Join(", ", Types.Keys)}.");
                return null;
            }
            string[] own = type == ItemType.Equipment ? EquipmentFields : type == ItemType.Material ? MaterialFields : ConsumableFields;
            // Revue Codex de la PR #44 : un champ d'un autre type est refusé qu'il vienne du lecteur JSON (Present) ou d'un objet public renseigné.
            foreach (string field in EquipmentFields.Concat(ConsumableFields).Distinct().Where(f => !own.Contains(f) && (item.Present.Contains(f) || IsSet(item, f))))
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
                return ok ? new ItemDefinition(item.Id!, item.Name!, type, rarity, item.Stack!.Value, null, 1, GearBonus.None, heal, cooldown, icon: item.Icon) : null;
            }
            EquipmentPlace? place = null;
            string kinds = string.Join(", ", places.Select(p => p.Accepts).Distinct());
            if (item.Slot is null) { Missing(report, path + ".slot", "sorte de place : " + kinds); ok = false; }
            else if ((place = places.FirstOrDefault(p => p.Accepts == item.Slot)) is null)
            {
                report.Add(path + ".slot", ValidationCode.UnknownReference, $"sorte de place inconnue « {item.Slot} » ; sortes : {kinds}.");
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
                // Sous-lot V1.2 : l'outil de métier sert hors combat ; il n'a aucun bonus de combat.
                if (place?.Group == PlaceGroup.Tool && values.Any(v => v != 0))
                {
                    report.Add(path + ".bonus", ValidationCode.InvalidValue, "un outil de métier n'a pas de bonus de combat.");
                    ok = false;
                }
            }
            // Sous-lot V1.2 : garde d'un bouclier, obligatoire à la place qui porte la garde, interdite ailleurs ; bornes : combat.shield des règles.
            ShieldGuard? guard = null;
            if (place != null && place.Guard && item.Guard is null) { Missing(report, path + ".guard", "garde du bouclier : damageReduction, costPerBlock, parryWindowMs"); ok = false; }
            else if (place != null && !place.Guard && item.Guard != null)
            {
                report.Add(path + ".guard", ValidationCode.InvalidValue, $"seul un objet de la place qui porte la garde (bouclier) a une garde.");
                ok = false;
            }
            else if (item.Guard is { } g)
            {
                bool complete = true;
                if (g.DamageReduction is null) { Missing(report, path + ".guard." + ShieldGuard.DamageReductionId, "part des dégâts bloquée"); complete = false; }
                if (g.CostPerBlock is null) { Missing(report, path + ".guard." + ShieldGuard.CostPerBlockId, "endurance d'un blocage"); complete = false; }
                if (g.ParryWindowMs is null) { Missing(report, path + ".guard." + ShieldGuard.ParryWindowMsId, "fenêtre de parade en ms"); complete = false; }
                if (complete)
                {
                    guard = new ShieldGuard(g.DamageReduction!.Value, g.CostPerBlock!.Value, g.ParryWindowMs!.Value);
                    // La même fonction que la validation du combat : un bouclier du catalogue est toujours accepté par le moteur.
                    foreach (RuleViolation v in guard.Check(rules, path + ".guard"))
                        foreach (string f in v.Fields) report.Add(f, ValidationCode.InvalidValue, v.Message);
                    if (guard.Check(rules, path + ".guard").Count > 0) ok = false;
                }
                else ok = false;
            }
            var fits = new List<string>();
            if (item.Fits != null)
            {
                if (item.Fits.Count > MaxFits) { report.Add(path + ".fits", ValidationCode.LimitExceeded, $"{item.Fits.Count} arbres, au plus {MaxFits}."); ok = false; }
                for (int k = 0; k < item.Fits.Count; k++)
                {
                    string fp = $"{path}.fits[{k}]";
                    if (item.Fits[k] is not string tree) { Missing(report, fp, "identifiant d'arbre de talents"); ok = false; }
                    else if (!trees.Contains(tree)) { report.Add(fp, ValidationCode.UnknownReference, $"arbre de talents inconnu « {tree} »."); ok = false; }
                    else if (fits.Contains(tree)) { report.Add(fp, ValidationCode.DuplicateId, $"arbre « {tree} » déjà donné."); ok = false; }
                    else fits.Add(tree);
                }
            }
            return ok ? new ItemDefinition(item.Id!, item.Name!, type, rarity, 1, item.Slot, item.RequiredLevel!.Value, GearBonus.FromValues(values), guard: guard, fits: fits, icon: item.Icon) : null;
        }

        /// <summary>
        /// Sous-lot V1.2 : équipement de départ. Chaque objet est un équipement de niveau 1 ; pour chaque classe chargée, ses objets de départ
        /// tiennent dans les places (pas plus d'objets d'une sorte que de places qui la reçoivent) et remplissent toutes ses places
        /// obligatoires : un nouveau personnage, et une sauvegarde mise à jour, ont toujours de quoi les occuper.
        /// </summary>
        private static void CheckStarting(List<StartingItemData?> list, List<EquipmentPlace> places, Dictionary<string, ItemDefinition> items, HashSet<string> ids,
            GameData data, List<StartingItem> starting, ValidationReport report)
        {
            if (list.Count > MaxStarting) report.Add("$.data.starting", ValidationCode.LimitExceeded, $"{list.Count} objets de départ, au plus {MaxStarting}.");
            bool ok = true;
            for (int i = 0; i < list.Count; i++)
            {
                string path = $"$.data.starting[{i}]";
                if (list[i] is not StartingItemData s) { Missing(report, path, "objet de départ"); ok = false; continue; }
                ItemDefinition? item = null;
                if (s.Item is null) { Missing(report, path + ".item", "identifiant d'objet"); ok = false; }
                else if (!items.TryGetValue(s.Item, out item))
                {
                    if (!ids.Contains(s.Item)) report.Add(path + ".item", ValidationCode.UnknownReference, $"objet inconnu « {s.Item} ».");
                    ok = false;
                }
                else if (item.Type != ItemType.Equipment) { report.Add(path + ".item", ValidationCode.InvalidValue, $"« {s.Item} » ne se porte pas."); ok = false; }
                else if (item.RequiredLevel != 1) { report.Add(path + ".item", ValidationCode.InvalidValue, $"« {s.Item} » demande le niveau {item.RequiredLevel} : un objet de départ se porte au niveau 1."); ok = false; }
                if (s.Class != null && !data.TryGetClass(s.Class, out _)) { report.Add(path + ".class", ValidationCode.UnknownReference, $"classe inconnue « {s.Class} »."); ok = false; }
                if (ok) starting.Add(new StartingItem(s.Item!, s.Class));
            }
            if (!ok || !report.IsValid) return;
            foreach (string classId in data.Classes.Select(c => c.Definition.Id))
            {
                var mine = starting.Where(s => s.Class is null || s.Class == classId).Select(s => items[s.Item]).ToList();
                foreach (IGrouping<string, ItemDefinition> kind in mine.GroupBy(x => x.Slot!))
                {
                    int room = places.Count(p => p.Accepts == kind.Key), wanted = kind.Count();
                    if (wanted > room)
                        report.Add("$.data.starting", ValidationCode.InvalidValue, $"{classId} : {wanted} objets de départ « {kind.Key} » pour {room} place(s).");
                }
                foreach (EquipmentPlace place in places.Where(p => p.IsRequiredFor(classId)))
                {
                    int required = places.Count(p => p.Accepts == place.Accepts && p.IsRequiredFor(classId));
                    if (mine.Count(x => x.Slot == place.Accepts) < required)
                        report.Add("$.data.starting", ValidationCode.MissingField,
                            $"{classId} : la place obligatoire « {place.Id} » n'a pas d'objet de départ (« {place.Accepts} »).");
                }
            }
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
