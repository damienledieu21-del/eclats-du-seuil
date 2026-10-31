using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Equipment;
using EdS.Data.Saves;
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

        /// <summary>Sous-lot V1.3 : ses objets peuvent porter des effets (armes et bijoux).</summary>
        public bool Effects { get; }

        internal EquipmentPlace(string id, string accepts, PlaceGroup group, bool required, IEnumerable<string> requiredBy, bool guard, bool effects = false)
        {
            Id = id;
            Accepts = accepts;
            Group = group;
            Required = required;
            RequiredBy = Frozen.List(requiredBy, nameof(requiredBy));
            Guard = guard;
            Effects = effects;
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

        /// <summary>Sous-lot V1.3 : objet nommé fait à la main (boss, quête, légendaire) : il ne tire jamais de propriété.</summary>
        public bool Named { get; }

        /// <summary>Sous-lot V1.3 : ensemble dont l'objet est une pièce, ou null.</summary>
        public string? Set { get; }

        /// <summary>Sous-lot V1.3 : effets de l'objet (texte, modificateurs, déclencheurs), ou null.</summary>
        public ItemEffect? Effect { get; }

        internal ItemDefinition(string id, string name, ItemType type, ItemRarity rarity, int stack, string? slot, int requiredLevel, GearBonus bonus,
            long heal = 0, long cooldownMs = 0, ShieldGuard? guard = null, IEnumerable<string>? fits = null, string? icon = null, bool named = false,
            string? set = null, ItemEffect? effect = null)
        {
            Named = named;
            Set = set;
            Effect = effect;
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

    /// <summary>Sous-lot V1.3 : effets d'un objet nommé : texte montré au joueur et effets de combat (source : l'identifiant de l'objet).</summary>
    public sealed class ItemEffect
    {
        public string Text { get; }
        public EquipmentEffect Effect { get; }

        internal ItemEffect(string text, EquipmentEffect effect)
        {
            Text = text;
            Effect = effect;
        }
    }

    /// <summary>
    /// Sous-lot V1.3 : bonus d'un ensemble quand au moins <see cref="Pieces"/> de ses pièces sont portées : texte, bonus aux statistiques et
    /// effets de combat (source « set.….&lt;pièces&gt; »), l'un ou l'autre ou les deux.
    /// </summary>
    public sealed class SetBonus
    {
        public int Pieces { get; }
        public string Text { get; }
        public GearBonus Bonus { get; }
        public EquipmentEffect? Effect { get; }

        internal SetBonus(int pieces, string text, GearBonus bonus, EquipmentEffect? effect)
        {
            Pieces = pieces;
            Text = text;
            Bonus = bonus;
            Effect = effect;
        }
    }

    /// <summary>
    /// Sous-lot V1.3 (feuille de route, « équipement au max » : ensembles de 2, 4 et 6 pièces) : un ensemble, ses pièces (objets nommés qui le
    /// citent, dans l'ordre du catalogue) et ses bonus à 2, 4 et 6 pièces portées, dans l'ordre croissant.
    /// </summary>
    public sealed class ItemSet
    {
        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<string> Pieces { get; }
        public IReadOnlyList<SetBonus> Bonuses { get; }

        internal ItemSet(string id, string name, IEnumerable<string> pieces, IEnumerable<SetBonus> bonuses)
        {
            Id = id;
            Name = name;
            Pieces = Frozen.List(pieces, nameof(pieces));
            Bonuses = Frozen.List(bonuses, nameof(bonuses));
        }

        /// <summary>Source des effets du palier à <paramref name="pieces"/> pièces (« set.garde_de_la_tour.4 »).</summary>
        public string SourceOf(int pieces) => Id + "." + pieces.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Sous-lot V1.3 (cadrage Q30 : « identité définie et variations bornées ; la rareté n'est pas le hasard des caractéristiques ») : une
    /// propriété qui peut être tirée sur un objet du butin : la statistique qu'elle augmente et ses bornes, qui montent avec le niveau de
    /// l'objet (<see cref="ItemCatalog.Bounds"/>), dans les sortes de place où elle peut sortir.
    /// </summary>
    public sealed class PropertyDefinition
    {
        public string Id { get; }
        public string Name { get; }

        /// <summary>Rang de la statistique dans <see cref="GearBonus.Fields"/>.</summary>
        public int Stat { get; }

        public int Min { get; }
        public int Max { get; }
        public int MinPerLevel { get; }
        public int MaxPerLevel { get; }

        /// <summary>Sortes de place où elle peut être tirée ; vide : toutes les places du combat.</summary>
        public IReadOnlyList<string> Slots { get; }

        internal PropertyDefinition(string id, string name, int stat, int min, int max, int minPerLevel, int maxPerLevel, IEnumerable<string> slots)
        {
            Id = id;
            Name = name;
            Stat = stat;
            Min = min;
            Max = max;
            MinPerLevel = minPerLevel;
            MaxPerLevel = maxPerLevel;
            Slots = Frozen.List(slots, nameof(slots));
        }

        /// <summary>Peut-elle sortir sur un objet de la sorte de place <paramref name="kind"/> ?</summary>
        public bool FitsKind(string? kind) => Slots.Count == 0 || (kind != null && Slots.Contains(kind));
    }

    /// <summary>Sous-lot V1.3 : nombre de propriétés tirées et échelle des bornes (en pour cent) d'une rareté.</summary>
    public sealed class RarityRoll
    {
        public ItemRarity Rarity { get; }
        public int Count { get; }
        public int ScalePercent { get; }

        internal RarityRoll(ItemRarity rarity, int count, int scalePercent)
        {
            Rarity = rarity;
            Count = count;
            ScalePercent = scalePercent;
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

        /// <summary>Sous-lot V1.3 : ensembles, dans l'ordre du fichier.</summary>
        public IReadOnlyList<ItemSet> Sets { get; }

        /// <summary>Sous-lot V1.3 : propriétés qui peuvent être tirées, dans l'ordre du fichier (celui du tirage).</summary>
        public IReadOnlyList<PropertyDefinition> Properties { get; }

        /// <summary>Sous-lot V1.3 : nombre de propriétés et échelle par rareté, dans l'ordre des raretés.</summary>
        public IReadOnlyList<RarityRoll> RarityRolls { get; }

        private readonly Dictionary<string, ItemSet> _sets;
        private readonly Dictionary<string, PropertyDefinition> _properties;

        internal ItemCatalog(int schemaVersion, string contentVersion, IEnumerable<EquipmentPlace> places, int inventorySize, IEnumerable<StartingItem> starting,
            IEnumerable<ItemDefinition> items, IEnumerable<LootTable> loot, IEnumerable<ItemSet>? sets = null, IEnumerable<PropertyDefinition>? properties = null,
            IEnumerable<RarityRoll>? rarityRolls = null)
        {
            Sets = Frozen.ListOrEmpty(sets, nameof(sets));
            Properties = Frozen.ListOrEmpty(properties, nameof(properties));
            RarityRolls = Frozen.List((rarityRolls ?? Array.Empty<RarityRoll>()).OrderBy(r => (int)r.Rarity), nameof(rarityRolls));
            _sets = Sets.ToDictionary(x => x.Id, StringComparer.Ordinal);
            _properties = Properties.ToDictionary(x => x.Id, StringComparer.Ordinal);
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

        public bool TryGetSet(string id, out ItemSet set) => _sets.TryGetValue(id ?? string.Empty, out set!);

        public bool TryGetProperty(string id, out PropertyDefinition property) => _properties.TryGetValue(id ?? string.Empty, out property!);

        /// <summary>Sous-lot V1.3 : nombre de propriétés tirées sur un objet de cette rareté (0 si la rareté n'en tire pas).</summary>
        public int RollCount(ItemRarity rarity) => RarityRolls.FirstOrDefault(r => r.Rarity == rarity)?.Count ?? 0;

        /// <summary>
        /// Sous-lot V1.3 : l'objet tire-t-il des propriétés quand il tombe du butin ? Un équipement des places du combat, non nommé (ni pièce
        /// d'ensemble, ni objet fait à la main), d'une rareté qui en tire.
        /// </summary>
        public bool IsRollable(ItemDefinition item) =>
            item is { Type: ItemType.Equipment, Named: false, Set: null } && PlacesFor(item.Slot).FirstOrDefault() is { Group: PlaceGroup.Combat }
            && RollCount(item.Rarity) > 0;

        /// <summary>Sous-lot V1.3 : propriétés qui peuvent sortir sur cet objet (sa sorte de place), dans l'ordre du catalogue.</summary>
        public IReadOnlyList<PropertyDefinition> PropertiesFor(ItemDefinition item) =>
            item is null ? throw new ArgumentNullException(nameof(item)) : Properties.Where(p => p.FitsKind(item.Slot)).ToList();

        /// <summary>
        /// Sous-lot V1.3 : bornes d'une propriété tirée sur <paramref name="item"/> : celles de la propriété au niveau requis de l'objet
        /// (min + minPerLevel × (niveau − 1), de même pour max), multipliées par l'échelle de sa rareté (pour cent, arrondi vers le bas), au
        /// moins 1. La rareté fixe le nombre et l'échelle ; le hasard ne choisit qu'à l'intérieur de ces bornes (Q30). Arithmétique vérifiée.
        /// </summary>
        public (int Min, int Max) Bounds(PropertyDefinition property, ItemDefinition item)
        {
            if (property is null) throw new ArgumentNullException(nameof(property));
            if (item is null) throw new ArgumentNullException(nameof(item));
            long scale = RarityRolls.FirstOrDefault(r => r.Rarity == item.Rarity)?.ScalePercent ?? 100;
            long steps = Math.Max(0, item.RequiredLevel - 1);
            long low = checked((property.Min + property.MinPerLevel * steps) * scale / 100);
            long high = checked((property.Max + property.MaxPerLevel * steps) * scale / 100);
            low = Math.Max(1, low);
            high = Math.Max(low, high);
            return ((int)Math.Min(low, int.MaxValue), (int)Math.Min(high, int.MaxValue));
        }

        /// <summary>
        /// Sous-lot V1.3 : validateur unique d'une instance d'objet contre le catalogue (sauvegarde relue, tirage du butin). Un objet de base
        /// (sans propriété) est toujours admis. Aux propriétés tirées : un objet qui en tire (<see cref="IsRollable"/>), quantité 1, exactement
        /// <see cref="RollCount"/> propriétés, connues, distinctes, admises pour sa sorte de place, chacune dans ses bornes (<see cref="Bounds"/>).
        /// Rend le premier motif de refus, ou null.
        /// </summary>
        public string? CheckInstance(ItemStack stack)
        {
            if (stack is null) throw new ArgumentNullException(nameof(stack));
            if (!TryGetItem(stack.Item, out ItemDefinition item)) return $"objet « {stack.Item} » absent du catalogue.";
            if (!stack.IsRolled) return null;
            if (!IsRollable(item)) return $"« {item.Id} » ne tire pas de propriété (objet nommé, pièce d'ensemble, rareté ou place sans tirage).";
            if (stack.Count != 1) return $"« {item.Id} » aux propriétés tirées : quantité 1 (il ne s'empile pas).";
            int count = RollCount(item.Rarity);
            if (stack.Properties.Count != count) return $"« {item.Id} » ({RarityId(item.Rarity)}) tire {count} propriété(s), {stack.Properties.Count} données.";
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (RolledProperty r in stack.Properties)
            {
                if (!TryGetProperty(r.Id, out PropertyDefinition property)) return $"propriété « {r.Id} » absente du catalogue.";
                if (!seen.Add(r.Id)) return $"propriété « {r.Id} » tirée deux fois.";
                if (!property.FitsKind(item.Slot)) return $"propriété « {r.Id} » impossible sur « {item.Slot} ».";
                (int low, int high) = Bounds(property, item);
                if (r.Value < low || r.Value > high) return $"propriété « {r.Id} » : {r.Value} hors de [{low}, {high}] pour « {item.Id} ».";
            }
            return null;
        }

        /// <summary>Sous-lot V1.3 : bonus d'une instance : celui de l'objet, plus ses propriétés tirées (supposées valides).</summary>
        public GearBonus BonusOf(ItemStack stack)
        {
            if (stack is null) throw new ArgumentNullException(nameof(stack));
            GearBonus bonus = Item(stack.Item).Bonus;
            if (!stack.IsRolled) return bonus;
            var values = new int[GearBonus.Fields.Count];
            foreach (RolledProperty r in stack.Properties)
                if (TryGetProperty(r.Id, out PropertyDefinition property)) values[property.Stat] = checked(values[property.Stat] + r.Value);
            return bonus + GearBonus.FromValues(values);
        }

        private static string RarityId(ItemRarity rarity) => ItemValidator.RarityId(rarity);
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
        /// départ), <c>guard</c> (garde d'un bouclier) et <c>fits</c> (arbres de talents) sur un équipement. Version 4 (sous-lot V1.3) :
        /// <c>effects</c> sur une place (ses objets peuvent porter des effets : armes et bijoux) ; <c>named</c> (objet nommé fait à la main),
        /// <c>set</c> (pièce d'un ensemble) et <c>effects</c> (texte, modificateurs, déclencheurs) sur un équipement ; <c>sets[]</c> (ensembles :
        /// bonus à 2, 4 et 6 pièces portées) et <c>properties</c> (tables des propriétés tirées : nombre et échelle par rareté, propriétés et
        /// leurs bornes par niveau). Une version antérieure est refusée : le fichier se met à jour à la main.
        /// </summary>
        public const int CurrentSchemaVersion = 4;

        /// <summary>Sous-lot V1.3 : ensembles, paliers d'un ensemble, propriétés qui peuvent être tirées, au plus.</summary>
        public const int MaxSets = 64, MaxSetBonuses = 3, MaxProperties = 256;

        /// <summary>Sous-lot V1.3 : pièces d'un ensemble (de 2 à 6) et paliers admis.</summary>
        public const int MinSetPieces = 2, MaxSetPieces = 6;

        private static readonly Regex SetId = new Regex(@"^set(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);
        private static readonly Regex PropertyIdPattern = new Regex(@"^property(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);

        /// <summary>Identifiant d'ensemble valide : « set. » puis minuscules, chiffres, « _ » et « . », 190 caractères au plus (la source d'un palier y ajoute « .6 »).</summary>
        public static bool IsSetId(string? id) => id != null && id.Length <= 190 && SetId.IsMatch(id);

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
            "named" => item.Named != null,
            "set" => item.Set != null,
            "effects" => item.Effects != null,
            _ => false,
        };

        private static readonly string[] EquipmentFields = { "slot", "requiredLevel", "bonus", "guard", "fits", "named", "set", "effects" };
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
            // Sous-lot V1.3 : ensembles, puis tables des propriétés tirées.
            var sets = new List<ItemSet>();
            if (d.Sets is null) Missing(report, "$.data.sets", "ensembles, éventuellement vide");
            else CheckSets(d.Sets, d.Items, items, data.Rules.Combat, sets, report);
            var properties = new List<PropertyDefinition>();
            var rolls = new List<RarityRoll>();
            if (d.Properties is null) Missing(report, "$.data.properties", "tables des propriétés tirées : rarities, list");
            else CheckProperties(d.Properties, places, maxLevel, properties, rolls, report);
            if (!report.IsValid || document.ContentVersion is null) return new ItemCatalogLoadResult(report, null);
            var catalog = new ItemCatalog(document.SchemaVersion!.Value, document.ContentVersion, places, d.InventorySize!.Value, starting, items, loot, sets,
                properties, rolls);
            // Chaque objet qui tire des propriétés en a assez de possibles pour sa sorte de place.
            for (int i = 0; i < d.Items!.Count; i++)
                if (d.Items[i] is { Id: string id } && catalog.TryGetItem(id, out ItemDefinition item) && catalog.IsRollable(item)
                    && catalog.PropertiesFor(item).Count < catalog.RollCount(item.Rarity))
                    report.Add($"$.data.items[{i}].rarity", ValidationCode.InvalidValue,
                        $"« {id} » tire {catalog.RollCount(item.Rarity)} propriété(s), {catalog.PropertiesFor(item).Count} possible(s) pour « {item.Slot} ».");
            return report.IsValid ? new ItemCatalogLoadResult(report, catalog) : new ItemCatalogLoadResult(report, null);
        }

        /// <summary>
        /// Sous-lot V1.3 : ensembles. Identifiant « set.… » unique, nom, de 2 à 6 pièces (les objets qui le citent, tous nommés), 1 à 3 paliers
        /// à 2, 4 ou 6 pièces, croissants et au plus le nombre de pièces, chacun avec son texte et un bonus aux statistiques ou des effets
        /// (bornés par <c>combat.equipment</c>, <see cref="EquipmentEffect.Check"/>). Un objet qui cite un ensemble inconnu est refusé.
        /// </summary>
        private static void CheckSets(List<SetData?> list, List<ItemData?>? rawItems, List<ItemDefinition> items, CombatRules rules, List<ItemSet> sets,
            ValidationReport report)
        {
            if (list.Count > MaxSets) report.Add("$.data.sets", ValidationCode.LimitExceeded, $"{list.Count} ensembles, au plus {MaxSets}.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < list.Count; i++)
            {
                string path = $"$.data.sets[{i}]";
                if (list[i] is not SetData set) { Missing(report, path, "ensemble"); continue; }
                bool ok = true;
                if (set.Id is null) { Missing(report, path + ".id", "identifiant « set.… »"); ok = false; }
                else if (!IsSetId(set.Id)) { report.Add(path + ".id", ValidationCode.InvalidValue, "identifiant invalide : « set. » puis minuscules, chiffres, « _ » et « . »."); ok = false; }
                else if (!ids.Add(set.Id)) { report.Add(path + ".id", ValidationCode.DuplicateId, $"ensemble « {set.Id} » déjà décrit plus haut."); ok = false; }
                if (string.IsNullOrWhiteSpace(set.Name)) { Missing(report, path + ".name", "nom affiché"); ok = false; }
                var pieces = set.Id is null ? new List<string>() : items.Where(x => x.Set == set.Id).Select(x => x.Id).ToList();
                if (set.Id != null && (pieces.Count < MinSetPieces || pieces.Count > MaxSetPieces))
                {
                    report.Add(path + ".id", ValidationCode.InvalidValue, $"« {set.Id} » a {pieces.Count} pièce(s) : de {MinSetPieces} à {MaxSetPieces}.");
                    ok = false;
                }
                var bonuses = new List<SetBonus>();
                if (set.Bonuses is null) { Missing(report, path + ".bonuses", "paliers à 2, 4 ou 6 pièces"); ok = false; }
                else
                {
                    if (set.Bonuses.Count < 1 || set.Bonuses.Count > MaxSetBonuses)
                    { report.Add(path + ".bonuses", ValidationCode.OutOfRange, $"{set.Bonuses.Count} paliers, de 1 à {MaxSetBonuses}."); ok = false; }
                    int last = 0;
                    for (int k = 0; k < set.Bonuses.Count; k++)
                    {
                        string bp = $"{path}.bonuses[{k}]";
                        if (set.Bonuses[k] is not SetBonusData b) { Missing(report, bp, "palier"); ok = false; continue; }
                        if (b.Pieces is null) { Missing(report, bp + ".pieces", "nombre de pièces : 2, 4 ou 6"); ok = false; continue; }
                        int n = b.Pieces.Value;
                        if (n != 2 && n != 4 && n != 6) { report.Add(bp + ".pieces", ValidationCode.InvalidValue, "palier à 2, 4 ou 6 pièces."); ok = false; }
                        else if (n <= last) { report.Add(bp + ".pieces", ValidationCode.InvalidValue, "paliers dans l'ordre croissant, chacun une fois."); ok = false; }
                        else if (set.Id != null && n > pieces.Count) { report.Add(bp + ".pieces", ValidationCode.OutOfRange, $"palier à {n} pièces pour {pieces.Count} pièce(s)."); ok = false; }
                        last = Math.Max(last, n);
                        if (string.IsNullOrWhiteSpace(b.Text)) { Missing(report, bp + ".text", "texte montré au joueur"); ok = false; }
                        int[] values = CheckBonusMap(b.Bonus ?? new List<KeyValuePair<string, int?>>(), bp + ".bonus", report, ref ok);
                        EquipmentEffect? effect = null;
                        if (b.Effects is { } e)
                        {
                            int before = report.Errors.Count;
                            EquipmentEffectShape.Check(e, bp + ".effects", withId: false, report);
                            if (report.Errors.Count == before && set.Id != null)
                            {
                                effect = EquipmentEffectShape.Build(e, set.Id + "." + n.ToString(System.Globalization.CultureInfo.InvariantCulture));
                                if (!EquipmentEffectShape.Report(effect.Check(rules, bp + ".effects"), report)) ok = false;
                            }
                            else ok = false;
                        }
                        if (b.Bonus is null && b.Effects is null) { Missing(report, bp + ".bonus", "bonus aux statistiques ou effets"); ok = false; }
                        if (ok) bonuses.Add(new SetBonus(n, b.Text!, GearBonus.FromValues(values), effect));
                    }
                }
                if (ok) sets.Add(new ItemSet(set.Id!, set.Name!, pieces, bonuses));
            }
            // Un objet qui cite un ensemble inconnu.
            if (rawItems != null)
                for (int i = 0; i < rawItems.Count; i++)
                    if (rawItems[i] is { Set: string setId } && IsSetId(setId) && !ids.Contains(setId))
                        report.Add($"$.data.items[{i}].set", ValidationCode.UnknownReference, $"ensemble inconnu « {setId} ».");
        }

        /// <summary>
        /// Sous-lot V1.3 : tables des propriétés tirées. Chaque rareté une fois (nombre de 0 à <see cref="SaveValidator.MaxProperties"/>, échelle de
        /// 1 à 1 000 %) ; chaque propriété : identifiant « property.… » unique, nom, statistique du bonus d'équipement, bornes au niveau 1
        /// (1 ≤ min ≤ max) et par niveau (0 ≤ minPerLevel ≤ maxPerLevel), sortes de place du combat ; au niveau maximal et à la plus grande
        /// échelle, la borne haute reste sous <see cref="SaveValidator.MaxPropertyValue"/>.
        /// </summary>
        private static void CheckProperties(PropertyTablesData t, List<EquipmentPlace> places, int maxLevel, List<PropertyDefinition> properties,
            List<RarityRoll> rolls, ValidationReport report)
        {
            const string path = "$.data.properties";
            int maxScale = 100;
            if (t.Rarities is null) Missing(report, path + ".rarities", "nombre de propriétés par rareté");
            else
            {
                for (int i = 0; i < t.Rarities.Count; i++)
                {
                    string rp = $"{path}.rarities[{i}]";
                    if (t.Rarities[i] is not RarityRollData r) { Missing(report, rp, "rareté"); continue; }
                    bool ok = true;
                    ItemRarity rarity = default;
                    if (r.Rarity is null) { Missing(report, rp + ".rarity", string.Join(", ", Rarities.Keys)); ok = false; }
                    else if (!Rarities.TryGetValue(r.Rarity, out rarity)) { report.Add(rp + ".rarity", ValidationCode.UnknownReference, $"rareté inconnue « {r.Rarity} »."); ok = false; }
                    else if (rolls.Any(x => x.Rarity == rarity)) { report.Add(rp + ".rarity", ValidationCode.DuplicateId, $"rareté « {r.Rarity} » déjà donnée."); ok = false; }
                    if (r.Count is null) { Missing(report, rp + ".count", "nombre de propriétés tirées"); ok = false; }
                    else if (r.Count < 0 || r.Count > SaveValidator.MaxProperties) { report.Add(rp + ".count", ValidationCode.OutOfRange, $"nombre hors de [0, {SaveValidator.MaxProperties}]."); ok = false; }
                    if (r.ScalePercent is null) { Missing(report, rp + ".scalePercent", "échelle des bornes, en pour cent"); ok = false; }
                    else if (r.ScalePercent < 1 || r.ScalePercent > 1000) { report.Add(rp + ".scalePercent", ValidationCode.OutOfRange, "échelle hors de [1, 1000] %."); ok = false; }
                    if (ok)
                    {
                        rolls.Add(new RarityRoll(rarity, r.Count!.Value, r.ScalePercent!.Value));
                        maxScale = Math.Max(maxScale, r.ScalePercent.Value);
                    }
                }
                foreach (KeyValuePair<string, ItemRarity> rarity in Rarities)
                    if (report.IsValid && !rolls.Any(x => x.Rarity == rarity.Value))
                        report.Add(path + ".rarities", ValidationCode.MissingField, $"rareté « {rarity.Key} » absente (chaque rareté une fois).");
            }
            if (t.List is null) { Missing(report, path + ".list", "propriétés qui peuvent être tirées"); return; }
            if (t.List.Count > MaxProperties) report.Add(path + ".list", ValidationCode.LimitExceeded, $"{t.List.Count} propriétés, au plus {MaxProperties}.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var kinds = new HashSet<string>(places.Where(p => p.Group == PlaceGroup.Combat).Select(p => p.Accepts), StringComparer.Ordinal);
            for (int i = 0; i < t.List.Count; i++)
            {
                string pp = $"{path}.list[{i}]";
                if (t.List[i] is not PropertyData d) { Missing(report, pp, "propriété"); continue; }
                bool ok = true;
                if (d.Id is null) { Missing(report, pp + ".id", "identifiant « property.… »"); ok = false; }
                else if (d.Id.Length > 200 || !PropertyIdPattern.IsMatch(d.Id)) { report.Add(pp + ".id", ValidationCode.InvalidValue, "identifiant invalide : « property. » puis minuscules, chiffres, « _ » et « . »."); ok = false; }
                else if (!ids.Add(d.Id)) { report.Add(pp + ".id", ValidationCode.DuplicateId, $"propriété « {d.Id} » déjà décrite plus haut."); ok = false; }
                if (string.IsNullOrWhiteSpace(d.Name)) { Missing(report, pp + ".name", "nom affiché"); ok = false; }
                int stat = -1;
                if (d.Stat is null) { Missing(report, pp + ".stat", string.Join(", ", GearBonus.Fields)); ok = false; }
                else if ((stat = GearBonus.FieldIndex(d.Stat)) < 0) { report.Add(pp + ".stat", ValidationCode.UnknownReference, $"statistique inconnue « {d.Stat} » ; possibles : {string.Join(", ", GearBonus.Fields)}."); ok = false; }
                if (d.Min is null) { Missing(report, pp + ".min", "borne basse au niveau 1"); ok = false; }
                if (d.Max is null) { Missing(report, pp + ".max", "borne haute au niveau 1"); ok = false; }
                if (d.MinPerLevel is null) { Missing(report, pp + ".minPerLevel", "hausse de la borne basse par niveau"); ok = false; }
                if (d.MaxPerLevel is null) { Missing(report, pp + ".maxPerLevel", "hausse de la borne haute par niveau"); ok = false; }
                if (ok)
                {
                    if (d.Min < 1 || d.Max < d.Min) { report.Add(pp + ".max", ValidationCode.OutOfRange, "bornes au niveau 1 : 1 ≤ min ≤ max."); ok = false; }
                    if (d.MinPerLevel < 0 || d.MaxPerLevel < d.MinPerLevel) { report.Add(pp + ".maxPerLevel", ValidationCode.OutOfRange, "hausses par niveau : 0 ≤ minPerLevel ≤ maxPerLevel."); ok = false; }
                    long top = ((long)d.Max!.Value + (long)d.MaxPerLevel!.Value * Math.Max(0, maxLevel - 1)) * maxScale / 100;
                    if (ok && top > SaveValidator.MaxPropertyValue)
                    { report.Add(pp + ".max", ValidationCode.OutOfRange, $"borne haute {top} au niveau {maxLevel} (échelle {maxScale} %), au plus {SaveValidator.MaxPropertyValue}."); ok = false; }
                }
                var slots = new List<string>();
                if (d.Slots != null)
                    for (int k = 0; k < d.Slots.Count; k++)
                    {
                        string sp = $"{pp}.slots[{k}]";
                        if (d.Slots[k] is not string kind) { Missing(report, sp, "sorte de place"); ok = false; }
                        else if (!kinds.Contains(kind)) { report.Add(sp, ValidationCode.UnknownReference, $"sorte de place du combat inconnue « {kind} »."); ok = false; }
                        else if (slots.Contains(kind)) { report.Add(sp, ValidationCode.DuplicateId, $"sorte « {kind} » déjà donnée."); ok = false; }
                        else slots.Add(kind);
                    }
                if (ok) properties.Add(new PropertyDefinition(d.Id!, d.Name!, stat, d.Min!.Value, d.Max!.Value, d.MinPerLevel!.Value, d.MaxPerLevel!.Value, slots));
            }
        }

        /// <summary>Bonus aux statistiques par identifiant (objet, palier d'ensemble) : champs connus, une fois chacun, de 0 à <see cref="MaxItemBonus"/>.</summary>
        private static int[] CheckBonusMap(List<KeyValuePair<string, int?>> bonus, string path, ValidationReport report, ref bool ok)
        {
            var values = new int[GearBonus.Fields.Count];
            var seenBonus = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int?> entry in bonus)
            {
                string bonusPath = $"{path}.{entry.Key}";
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
            return values;
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
            bool required = slot.Required ?? false, guard = slot.Guard ?? false, effects = slot.Effects ?? false;
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
            // Sous-lot V1.3 : des effets seulement à une place du combat.
            if (effects && group != PlaceGroup.Combat)
            {
                report.Add(path + ".effects", ValidationCode.InvalidValue, "des effets seulement à une place du combat.");
                ok = false;
            }
            // Les places d'une même sorte sont interchangeables : même groupe, même garde, mêmes effets.
            if (before.FirstOrDefault(p => p.Accepts == accepts) is { } same && (same.Group != group || same.Guard != guard || same.Effects != effects))
            {
                report.Add(path + ".accepts", ValidationCode.InvalidValue, $"la place « {same.Id} » reçoit déjà « {accepts} » avec un autre groupe, une autre garde ou d'autres effets.");
                ok = false;
            }
            return ok ? new EquipmentPlace(slot.Id!, accepts, group, required, requiredBy, guard, effects) : null;
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
                values = CheckBonusMap(item.Bonus, path + ".bonus", report, ref ok);
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
            // Sous-lot V1.3 : objet nommé, pièce d'ensemble, effets. Une pièce d'ensemble et un objet à effets sont nommés (faits à la main) ; des
            // effets seulement à une place qui les admet (armes et bijoux), avec leur texte, bornés par combat.equipment des règles.
            bool named = item.Named ?? false;
            if (item.Set != null && !IsSetId(item.Set)) { report.Add(path + ".set", ValidationCode.InvalidValue, "ensemble invalide : « set. » puis minuscules, chiffres, « _ » et « . »."); ok = false; }
            else if (item.Set != null && !named) { report.Add(path + ".named", ValidationCode.InvalidValue, "une pièce d'ensemble est un objet nommé (named: true)."); ok = false; }
            ItemEffect? effect = null;
            if (item.Effects is { } e)
            {
                if (place != null && !place.Effects) { report.Add(path + ".effects", ValidationCode.InvalidValue, $"pas d'effet à la place « {place.Accepts} » (armes et bijoux seulement)."); ok = false; }
                else if (!named) { report.Add(path + ".named", ValidationCode.InvalidValue, "un objet à effets est un objet nommé (named: true)."); ok = false; }
                else if (string.IsNullOrWhiteSpace(e.Text)) { Missing(report, path + ".effects.text", "texte montré au joueur"); ok = false; }
                else
                {
                    int before = report.Errors.Count;
                    EquipmentEffectShape.Check(e, path + ".effects", withId: false, report);
                    if (report.Errors.Count == before && item.Id != null)
                    {
                        EquipmentEffect built = EquipmentEffectShape.Build(e, item.Id);
                        if (EquipmentEffectShape.Report(built.Check(rules, path + ".effects"), report)) effect = new ItemEffect(e.Text!, built);
                        else ok = false;
                    }
                    else ok = false;
                }
            }
            return ok ? new ItemDefinition(item.Id!, item.Name!, type, rarity, 1, item.Slot, item.RequiredLevel!.Value, GearBonus.FromValues(values), guard: guard,
                fits: fits, icon: item.Icon, named: named, set: item.Set, effect: effect) : null;
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
