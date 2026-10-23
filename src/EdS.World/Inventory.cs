using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using EdS.Data.Items;
using EdS.Data.Saves;
using EdS.Domain.Stats;

namespace EdS.World
{
    /// <summary>Motif d'un refus d'opération sur l'inventaire ou l'équipement (rien n'a changé).</summary>
    public enum InventoryRefusal
    {
        None = 0,

        /// <summary>Plus assez de places (ni pile entamée, ni place libre).</summary>
        Full = 1,

        /// <summary>Place vide ou hors de l'inventaire, emplacement inconnu ou vide.</summary>
        NothingThere = 2,

        /// <summary>L'objet ne se porte pas (matériau).</summary>
        NotEquipment = 3,

        /// <summary>Niveau du personnage inférieur au niveau requis.</summary>
        LevelTooLow = 4,

        /// <summary>Quantité absente de l'inventaire, ou quantité invalide.</summary>
        NotEnough = 5,
    }

    /// <summary>
    /// Sous-lot 12.3 (cadrage Q4, Q5) : inventaire et équipement d'un personnage, contre le catalogue des objets. L'inventaire a
    /// <see cref="ItemCatalog.InventorySize"/> places ; une place tient une pile d'un même objet (un équipement par place, un matériau jusqu'à
    /// sa pile). Les places occupées sont gardées dans l'ordre, sans trou. Chaque opération est atomique : refusée, elle ne change rien.
    /// L'équipement porté donne son bonus (<see cref="Gear"/>) au prochain combat où le personnage entre (le combat en cours garde le sien).
    /// </summary>
    public sealed class Inventory
    {
        private readonly ItemCatalog _catalog;
        private readonly List<ItemStack> _stacks = new List<ItemStack>();
        private readonly SortedDictionary<string, string> _equipped = new SortedDictionary<string, string>(StringComparer.Ordinal);

        public ItemCatalog Catalog => _catalog;

        /// <summary>Places occupées, dans l'ordre (vue en lecture seule : on ne change l'inventaire que par ses opérations).</summary>
        public IReadOnlyList<ItemStack> Stacks => _stacks.AsReadOnly();

        /// <summary>Objet porté par emplacement (ordre ordinal ; vue en lecture seule).</summary>
        public IReadOnlyDictionary<string, string> Equipped => new ReadOnlyDictionary<string, string>(_equipped);

        public int FreePlaces => _catalog.InventorySize - _stacks.Count;

        public Inventory(ItemCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        /// <summary>
        /// Inventaire et équipement d'un personnage sauvegardé, vérifiés contre le catalogue : objets connus, piles dans leur taille, au plus
        /// <see cref="ItemCatalog.InventorySize"/> places, équipement à son emplacement et à un niveau suffisant.
        /// </summary>
        /// <exception cref="ArgumentException">Sauvegarde incohérente avec le catalogue (premier motif dans le message).</exception>
        public static Inventory From(SavedCharacter character, ItemCatalog catalog, int level)
        {
            if (character is null) throw new ArgumentNullException(nameof(character));
            var inventory = new Inventory(catalog);
            if (character.Inventory.Count > catalog.InventorySize)
                throw new ArgumentException($"Inventaire de {character.Inventory.Count} places, au plus {catalog.InventorySize}.", nameof(character));
            for (int i = 0; i < character.Inventory.Count; i++)
            {
                ItemStack stack = character.Inventory[i];
                if (!catalog.TryGetItem(stack.Item, out ItemDefinition item))
                    throw new ArgumentException($"Inventaire, place {i} : objet « {stack.Item} » absent du catalogue.", nameof(character));
                if (stack.Count < 1 || stack.Count > item.Stack)
                    throw new ArgumentException($"Inventaire, place {i} : {stack.Count} « {stack.Item} », pile de 1 à {item.Stack}.", nameof(character));
                inventory._stacks.Add(stack);
            }
            foreach (KeyValuePair<string, string> e in character.Equipment)
            {
                if (!catalog.Slots.Contains(e.Key)) throw new ArgumentException($"Emplacement « {e.Key} » inconnu du catalogue.", nameof(character));
                if (!catalog.TryGetItem(e.Value, out ItemDefinition item))
                    throw new ArgumentException($"Équipement « {e.Key} » : objet « {e.Value} » absent du catalogue.", nameof(character));
                if (item.Type != ItemType.Equipment || item.Slot != e.Key)
                    throw new ArgumentException($"Équipement « {e.Key} » : « {e.Value} » ne se porte pas à cet emplacement.", nameof(character));
                if (item.RequiredLevel > level)
                    throw new ArgumentException($"Équipement « {e.Key} » : « {e.Value} » demande le niveau {item.RequiredLevel} (personnage niveau {level}).", nameof(character));
                inventory._equipped[e.Key] = e.Value;
            }
            return inventory;
        }

        /// <summary>Somme des bonus des objets portés.</summary>
        public GearBonus Gear()
        {
            GearBonus total = GearBonus.None;
            foreach (string item in _equipped.Values) total += _catalog.Item(item).Bonus;
            return total;
        }

        /// <summary>Quantité de <paramref name="item"/> dans l'inventaire (objets portés exclus).</summary>
        public int Count(string item) => _stacks.Where(s => s.Item == item).Sum(s => s.Count);

        /// <summary>Peut-on ranger <paramref name="count"/> <paramref name="item"/> en entier ?</summary>
        public bool CanAdd(string item, int count) => Room(item, count) is int needed && needed <= FreePlaces;

        /// <summary>
        /// Range <paramref name="count"/> <paramref name="item"/> en entier : d'abord dans les piles entamées du même objet, dans l'ordre, puis
        /// dans de nouvelles places ; sinon rien (<see cref="InventoryRefusal.Full"/>).
        /// </summary>
        /// <exception cref="ArgumentException">Objet absent du catalogue ou quantité non positive.</exception>
        public InventoryRefusal Add(string item, int count)
        {
            ItemDefinition definition = _catalog.Item(item);
            if (count < 1) throw new ArgumentException("Quantité positive attendue.", nameof(count));
            if (!CanAdd(item, count)) return InventoryRefusal.Full;
            int left = count;
            for (int i = 0; i < _stacks.Count && left > 0; i++)
                if (_stacks[i].Item == item && _stacks[i].Count < definition.Stack)
                {
                    int put = Math.Min(left, definition.Stack - _stacks[i].Count);
                    _stacks[i] = new ItemStack(item, _stacks[i].Count + put);
                    left -= put;
                }
            while (left > 0)
            {
                int put = Math.Min(left, definition.Stack);
                _stacks.Add(new ItemStack(item, put));
                left -= put;
            }
            return InventoryRefusal.None;
        }

        /// <summary>Retire <paramref name="count"/> <paramref name="item"/> en entier, des dernières places vers les premières ; sinon rien.</summary>
        public InventoryRefusal Remove(string item, int count)
        {
            if (count < 1 || Count(item) < count) return InventoryRefusal.NotEnough;
            int left = count;
            for (int i = _stacks.Count - 1; i >= 0 && left > 0; i--)
                if (_stacks[i].Item == item)
                {
                    int take = Math.Min(left, _stacks[i].Count);
                    if (take == _stacks[i].Count) _stacks.RemoveAt(i);
                    else _stacks[i] = new ItemStack(item, _stacks[i].Count - take);
                    left -= take;
                }
            return InventoryRefusal.None;
        }

        /// <summary>
        /// Porte l'équipement de la place <paramref name="place"/> à son emplacement, si le niveau <paramref name="level"/> suffit. L'objet
        /// qui y était porté prend sa place dans l'inventaire (échange : aucune place de plus n'est nécessaire).
        /// </summary>
        public InventoryRefusal Equip(int place, int level)
        {
            if (place < 0 || place >= _stacks.Count) return InventoryRefusal.NothingThere;
            ItemDefinition item = _catalog.Item(_stacks[place].Item);
            if (item.Type != ItemType.Equipment) return InventoryRefusal.NotEquipment;
            if (item.RequiredLevel > level) return InventoryRefusal.LevelTooLow;
            string slot = item.Slot!;
            if (_equipped.TryGetValue(slot, out string previous)) _stacks[place] = new ItemStack(previous, 1);
            else _stacks.RemoveAt(place);
            _equipped[slot] = item.Id;
            return InventoryRefusal.None;
        }

        /// <summary>Retire l'objet porté à <paramref name="slot"/> et le range à la fin de l'inventaire, s'il reste une place.</summary>
        public InventoryRefusal Unequip(string slot)
        {
            if (slot is null || !_equipped.TryGetValue(slot, out string item)) return InventoryRefusal.NothingThere;
            if (FreePlaces < 1) return InventoryRefusal.Full;
            _equipped.Remove(slot);
            _stacks.Add(new ItemStack(item, 1));
            return InventoryRefusal.None;
        }

        /// <summary>Personnage avec cet inventaire et cet équipement.</summary>
        public SavedCharacter SaveInto(SavedCharacter character) =>
            (character ?? throw new ArgumentNullException(nameof(character))).WithItems(_stacks, new Dictionary<string, string>(_equipped, StringComparer.Ordinal));

        // Places nouvelles nécessaires pour ranger count item (piles entamées d'abord).
        private int? Room(string item, int count)
        {
            if (!_catalog.TryGetItem(item, out ItemDefinition definition) || count < 1) return null;
            long left = count;
            foreach (ItemStack s in _stacks)
                if (s.Item == item) left -= definition.Stack - s.Count;
            if (left <= 0) return 0;
            return (int)((left + definition.Stack - 1) / definition.Stack);
        }
    }
}
