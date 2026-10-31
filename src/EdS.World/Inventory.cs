using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using EdS.Data.Items;
using EdS.Data.Saves;
using EdS.Domain.Combat;
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

        /// <summary>L'objet ne se porte pas (matériau, objet de soin).</summary>
        NotEquipment = 3,

        /// <summary>Niveau du personnage inférieur au niveau requis.</summary>
        LevelTooLow = 4,

        /// <summary>Quantité absente de l'inventaire, ou quantité invalide.</summary>
        NotEnough = 5,

        /// <summary>Sous-lot V1.2 : l'objet ne va pas à cette place (ou les deux places ne reçoivent pas la même sorte d'objet).</summary>
        WrongSlot = 6,

        /// <summary>
        /// Sous-lot V1.2 : place toujours occupée (l'Éclat du Seuil, le bouclier du Bastion) : on y met un autre objet, on ne l'ôte pas.
        /// </summary>
        Required = 7,

        /// <summary>Sous-lot V1.2 : en plein combat (<see cref="WorldSession.InCombat"/>) : l'équipement se change hors combat.</summary>
        InCombat = 8,
    }

    /// <summary>Sous-lot V1.2 : ordre du rangement de l'inventaire (<see cref="Inventory.Sort"/>).</summary>
    public enum InventorySort
    {
        /// <summary>Équipement (dans l'ordre des places), puis objets de soin, puis matériaux ; à sorte égale, rareté puis niveau décroissants.</summary>
        Type = 0,

        /// <summary>Rareté décroissante, puis sorte, puis niveau décroissant.</summary>
        Rarity = 1,

        /// <summary>Niveau requis décroissant, puis sorte, puis rareté décroissante.</summary>
        Level = 2,
    }

    /// <summary>Sous-lot V1.2 : filtre de l'inventaire (<see cref="Inventory.Filter"/>), une vue qui ne change rien.</summary>
    public enum InventoryFilter
    {
        All = 0,

        /// <summary>Tout l'équipement.</summary>
        Equipment = 1,

        /// <summary>L'équipement que le personnage peut porter à son niveau.</summary>
        Wearable = 2,

        /// <summary>Les objets de soin.</summary>
        Consumables = 3,

        /// <summary>Les matériaux.</summary>
        Materials = 4,
    }

    /// <summary>
    /// Sous-lot 12.3 (cadrage Q4, Q5) : inventaire et équipement d'un personnage, contre le catalogue des objets. L'inventaire a
    /// <see cref="ItemCatalog.InventorySize"/> places ; une place tient une pile d'un même objet (un équipement par place, un matériau jusqu'à
    /// sa pile). Les places occupées sont gardées dans l'ordre, sans trou. Chaque opération est atomique : refusée, elle ne change rien.
    /// L'équipement porté donne son bonus (<see cref="Gear"/>) et la garde de son bouclier (<see cref="Shield"/>) au prochain combat où le
    /// personnage entre (le combat en cours garde les siens). Sous-lot V1.2 : les 18 places du catalogue (deux anneaux, deux reliques, l'Éclat
    /// du Seuil, l'outil de métier), les places obligatoires (on échange, on n'ôte pas), l'échange entre deux places d'une même sorte, le
    /// rangement et les filtres.
    /// </summary>
    public sealed class Inventory
    {
        private readonly ItemCatalog _catalog;
        private readonly List<ItemStack> _stacks = new List<ItemStack>();
        private readonly SortedDictionary<string, string> _equipped = new SortedDictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Sous-lot 12.6 (revue Codex de la PR #47) : objets de soin emportés au combat, réservés jusqu'à ce qu'ils soient bus.</summary>
        private readonly SortedDictionary<string, int> _reserved = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public ItemCatalog Catalog => _catalog;

        /// <summary>Sous-lot V1.2 : classe du personnage (places obligatoires selon la classe) ; null : seulement les places obligatoires pour tous.</summary>
        public string? ClassId { get; }

        /// <summary>Places occupées, dans l'ordre (vue en lecture seule : on ne change l'inventaire que par ses opérations).</summary>
        public IReadOnlyList<ItemStack> Stacks => _stacks.AsReadOnly();

        /// <summary>Objet porté par place (ordre ordinal ; vue en lecture seule).</summary>
        public IReadOnlyDictionary<string, string> Equipped => new ReadOnlyDictionary<string, string>(_equipped);

        /// <summary>Relecture de la PR #75 : objet porté à la place <paramref name="place"/>, ou null (sans allouer de vue, pour l'affichage à chaque image).</summary>
        public string? WornAt(string place) => place != null && _equipped.TryGetValue(place, out string id) ? id : null;

        public int FreePlaces => _catalog.InventorySize - _stacks.Count;

        public Inventory(ItemCatalog catalog, string? classId = null)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            ClassId = classId;
        }

        /// <summary>
        /// Inventaire et équipement d'un personnage sauvegardé, vérifiés contre le catalogue : objets connus, piles dans leur taille, au plus
        /// <see cref="ItemCatalog.InventorySize"/> places, équipement à une place qui reçoit sa sorte et à un niveau suffisant. Une sauvegarde
        /// de version 6 a ses quatre emplacements (arme, bouclier, torse, cou), qui gardent leur nom parmi les 18 places.
        /// </summary>
        /// <exception cref="ArgumentException">Sauvegarde incohérente avec le catalogue (premier motif dans le message).</exception>
        public static Inventory From(SavedCharacter character, ItemCatalog catalog, int level)
        {
            if (character is null) throw new ArgumentNullException(nameof(character));
            var inventory = new Inventory(catalog, character.ClassId);
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
                if (!catalog.TryGetPlace(e.Key, out EquipmentPlace place)) throw new ArgumentException($"Emplacement « {e.Key} » inconnu du catalogue.", nameof(character));
                if (!catalog.TryGetItem(e.Value, out ItemDefinition item))
                    throw new ArgumentException($"Équipement « {e.Key} » : objet « {e.Value} » absent du catalogue.", nameof(character));
                if (item.Type != ItemType.Equipment || item.Slot != place.Accepts)
                    throw new ArgumentException($"Équipement « {e.Key} » : « {e.Value} » ne se porte pas à cet emplacement.", nameof(character));
                if (item.RequiredLevel > level)
                    throw new ArgumentException($"Équipement « {e.Key} » : « {e.Value} » demande le niveau {item.RequiredLevel} (personnage niveau {level}).", nameof(character));
                inventory._equipped[e.Key] = e.Value;
            }
            return inventory;
        }

        /// <summary>
        /// Sous-lot V1.2 : le personnage avec son équipement de départ (<see cref="ItemCatalog.StartingFor"/>) porté, chaque objet à la
        /// première place libre de sa sorte (nouveau personnage : l'Éclat du Seuil, et pour le Bastion l'épée et l'écu). Un objet dont toutes
        /// les places sont déjà prises n'est pas donné. Rien d'autre ne change.
        /// </summary>
        public static SavedCharacter Outfit(SavedCharacter character, ItemCatalog catalog)
        {
            if (character is null) throw new ArgumentNullException(nameof(character));
            var inventory = From(character, catalog, int.MaxValue);
            foreach (string id in catalog.StartingFor(character.ClassId))
                if (inventory.FreePlaceFor(catalog.Item(id)) is EquipmentPlace place) inventory._equipped[place.Id] = id;
            return inventory.SaveInto(character);
        }

        /// <summary>
        /// Sous-lot V1.2 : remplit les places obligatoires vides de ce personnage (sauvegarde d'une version antérieure : l'Éclat du Seuil
        /// n'existait pas, et le bouclier du Bastion pouvait s'ôter ; ou sauvegarde de version 7 aux places obligatoires vides). Pour chaque
        /// place vide : l'objet de l'inventaire de sa sorte, à un niveau suffisant, le premier dans l'ordre des places ; sinon son objet de
        /// départ, donné. Rend les objets mis en place, dans l'ordre des places (vide s'il n'y avait rien à faire). Idempotente : un second
        /// appel ne change rien et rend une liste vide ; appelée à chaque chargement (relecture de la PR #75).
        /// </summary>
        public IReadOnlyList<string> FillRequired(int level)
        {
            var done = new List<string>();
            foreach (EquipmentPlace place in _catalog.Places)
            {
                if (!place.IsRequiredFor(ClassId) || _equipped.ContainsKey(place.Id)) continue;
                int found = _stacks.FindIndex(s => _catalog.Item(s.Item) is { Type: ItemType.Equipment } i && i.Slot == place.Accepts && i.RequiredLevel <= level);
                if (found >= 0)
                {
                    string id = _stacks[found].Item;
                    _stacks.RemoveAt(found);
                    _equipped[place.Id] = id;
                    done.Add(id);
                    continue;
                }
                // Objet de départ de la classe (ou de toutes : StartingFor d'une classe inconnue n'en rend que celles-là).
                string? start = _catalog.StartingFor(ClassId ?? "").FirstOrDefault(s => _catalog.Item(s).Slot == place.Accepts);
                if (start is null) continue;
                _equipped[place.Id] = start;
                done.Add(start);
            }
            return done;
        }

        /// <summary>Somme des bonus des objets portés (l'outil de métier n'en a pas).</summary>
        public GearBonus Gear()
        {
            GearBonus total = GearBonus.None;
            foreach (string item in _equipped.Values) total += _catalog.Item(item).Bonus;
            return total;
        }

        /// <summary>Sous-lot V1.2 : garde du bouclier porté (place qui porte la garde), ou null : celle des règles.</summary>
        public ShieldGuard? Shield =>
            _catalog.GuardPlace is { } place && _equipped.TryGetValue(place.Id, out string id) ? _catalog.Item(id).Guard : null;

        /// <summary>Quantité de <paramref name="item"/> dans l'inventaire (objets portés exclus).</summary>
        public int Count(string item) => _stacks.Where(s => s.Item == item).Sum(s => s.Count);

        /// <summary>
        /// Sous-lot 12.6 : quantité réservée (objets de soin emportés au combat) : elle reste dans l'inventaire et la sauvegarde, mais
        /// <see cref="Remove"/> ne peut pas la prendre ; seul le moteur l'use, quand le personnage boit.
        /// </summary>
        public int Reserved(string item) => item != null && _reserved.TryGetValue(item, out int n) ? n : 0;

        /// <summary>Quantité que <see cref="Remove"/> peut prendre (hors réserve).</summary>
        public int Available(string item) => Count(item) - Reserved(item);

        /// <summary>Réserve <paramref name="count"/> <paramref name="item"/> déjà dans l'inventaire et non réservés.</summary>
        /// <exception cref="ArgumentException">Quantité non positive ou absente (hors réserve).</exception>
        internal void Reserve(string item, int count)
        {
            if (count < 1 || Available(item) < count) throw new ArgumentException($"Réserve de {count} « {item} » impossible.", nameof(count));
            _reserved[item] = checked(Reserved(item) + count);
        }

        /// <summary>Use <paramref name="count"/> objets réservés : ils quittent l'inventaire et la réserve.</summary>
        /// <exception cref="ArgumentException">Plus que la réserve.</exception>
        internal void UseReserved(string item, int count)
        {
            int reserved = Reserved(item);
            if (count < 1 || count > reserved) throw new ArgumentException($"{count} « {item} » à user, {reserved} réservés.", nameof(count));
            if (count == reserved) _reserved.Remove(item);
            else _reserved[item] = reserved - count;
            Take(item, count);
        }

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

        /// <summary>
        /// Retire <paramref name="count"/> <paramref name="item"/> en entier, des dernières places vers les premières ; sinon rien. Les objets
        /// réservés (<see cref="Reserved"/>) ne se retirent pas.
        /// </summary>
        public InventoryRefusal Remove(string item, int count)
        {
            if (count < 1 || Available(item) < count) return InventoryRefusal.NotEnough;
            Take(item, count);
            return InventoryRefusal.None;
        }

        private void Take(string item, int count)
        {
            int left = count;
            for (int i = _stacks.Count - 1; i >= 0 && left > 0; i--)
                if (_stacks[i].Item == item)
                {
                    int take = Math.Min(left, _stacks[i].Count);
                    if (take == _stacks[i].Count) _stacks.RemoveAt(i);
                    else _stacks[i] = new ItemStack(item, _stacks[i].Count - take);
                    left -= take;
                }
        }

        /// <summary>Première place libre qui reçoit cet objet, dans l'ordre des places ; null si toutes sont prises ou s'il ne se porte pas.</summary>
        private EquipmentPlace? FreePlaceFor(ItemDefinition item) =>
            item.Type != ItemType.Equipment ? null : _catalog.PlacesFor(item.Slot).FirstOrDefault(p => !_equipped.ContainsKey(p.Id));

        /// <summary>
        /// Sous-lot V1.2 : place où <see cref="Equip"/> mettrait cet objet : la première place libre de sa sorte, sinon la première de sa sorte
        /// (échange) ; null s'il ne se porte pas.
        /// </summary>
        public string? PlaceFor(string item)
        {
            if (!_catalog.TryGetItem(item, out ItemDefinition definition) || definition.Type != ItemType.Equipment) return null;
            return (FreePlaceFor(definition) ?? _catalog.PlacesFor(definition.Slot).FirstOrDefault())?.Id;
        }

        /// <summary>
        /// Porte l'équipement de la place <paramref name="place"/> de l'inventaire, si le niveau <paramref name="level"/> suffit, à la place
        /// <see cref="PlaceFor"/> (un anneau va au premier doigt libre). L'objet qui y était porté prend sa place dans l'inventaire (échange :
        /// aucune place de plus n'est nécessaire).
        /// </summary>
        public InventoryRefusal Equip(int place, int level)
        {
            if (place < 0 || place >= _stacks.Count) return InventoryRefusal.NothingThere;
            string? slot = PlaceFor(_stacks[place].Item);
            return slot is null ? InventoryRefusal.NotEquipment : EquipTo(place, slot, level);
        }

        /// <summary>
        /// Sous-lot V1.2 : porte l'équipement de la place <paramref name="place"/> de l'inventaire à la place d'équipement
        /// <paramref name="slot"/> (par exemple le second anneau), qui doit recevoir sa sorte (<see cref="InventoryRefusal.WrongSlot"/>).
        /// L'objet qui y était porté prend sa place dans l'inventaire.
        /// </summary>
        public InventoryRefusal EquipTo(int place, string slot, int level)
        {
            if (place < 0 || place >= _stacks.Count) return InventoryRefusal.NothingThere;
            ItemDefinition item = _catalog.Item(_stacks[place].Item);
            if (item.Type != ItemType.Equipment) return InventoryRefusal.NotEquipment;
            if (slot is null || !_catalog.TryGetPlace(slot, out EquipmentPlace target) || target.Accepts != item.Slot) return InventoryRefusal.WrongSlot;
            if (item.RequiredLevel > level) return InventoryRefusal.LevelTooLow;
            if (_equipped.TryGetValue(slot, out string previous)) _stacks[place] = new ItemStack(previous, 1);
            else _stacks.RemoveAt(place);
            _equipped[slot] = item.Id;
            return InventoryRefusal.None;
        }

        /// <summary>
        /// Retire l'objet porté à <paramref name="slot"/> et le range à la fin de l'inventaire, s'il reste une place. Sous-lot V1.2 : refusé à
        /// une place obligatoire pour ce personnage (<see cref="InventoryRefusal.Required"/> : l'Éclat du Seuil, le bouclier du Bastion), où
        /// l'on porte un autre objet à la place.
        /// </summary>
        public InventoryRefusal Unequip(string slot)
        {
            if (slot is null || !_equipped.TryGetValue(slot, out string item)) return InventoryRefusal.NothingThere;
            if (_catalog.TryGetPlace(slot, out EquipmentPlace place) && place.IsRequiredFor(ClassId)) return InventoryRefusal.Required;
            if (FreePlaces < 1) return InventoryRefusal.Full;
            _equipped.Remove(slot);
            _stacks.Add(new ItemStack(item, 1));
            return InventoryRefusal.None;
        }

        /// <summary>
        /// Sous-lot V1.2 : échange le contenu de deux places de la même sorte (les deux anneaux, les deux reliques ; l'une peut être vide).
        /// Refusé si elles diffèrent de sorte ou sont la même (<see cref="InventoryRefusal.WrongSlot"/>), si les deux sont vides, ou si une
        /// place obligatoire resterait vide.
        /// </summary>
        public InventoryRefusal Swap(string first, string second)
        {
            if (first is null || second is null || first == second || !_catalog.TryGetPlace(first, out EquipmentPlace a) || !_catalog.TryGetPlace(second, out EquipmentPlace b)
                || a.Accepts != b.Accepts)
                return InventoryRefusal.WrongSlot;
            bool hasA = _equipped.TryGetValue(first, out string itemA), hasB = _equipped.TryGetValue(second, out string itemB);
            if (!hasA && !hasB) return InventoryRefusal.NothingThere;
            if ((!hasB && a.IsRequiredFor(ClassId)) || (!hasA && b.IsRequiredFor(ClassId))) return InventoryRefusal.Required;
            if (hasB) _equipped[first] = itemB; else _equipped.Remove(first);
            if (hasA) _equipped[second] = itemA; else _equipped.Remove(second);
            return InventoryRefusal.None;
        }

        /// <summary>
        /// Sous-lot V1.2 : range l'inventaire dans l'ordre <paramref name="order"/> et regroupe les piles entamées d'un même objet. Les
        /// quantités, les réserves et l'équipement porté ne changent pas ; le rangement prend au plus les places qu'il occupait.
        /// </summary>
        public void Sort(InventorySort order)
        {
            var counts = new List<(string Item, int Count)>();
            foreach (ItemStack s in _stacks)
            {
                int k = counts.FindIndex(c => c.Item == s.Item);
                if (k < 0) counts.Add((s.Item, s.Count));
                else counts[k] = (s.Item, checked(counts[k].Count + s.Count));
            }
            var sorted = counts.OrderBy(c => c.Item, Comparer<string>.Create((x, y) => Compare(order, x, y))).ToList();
            var stacks = new List<ItemStack>();
            foreach ((string item, int count) in sorted)
            {
                int stack = _catalog.Item(item).Stack, left = count;
                while (left > 0)
                {
                    int put = Math.Min(left, stack);
                    stacks.Add(new ItemStack(item, put));
                    left -= put;
                }
            }
            _stacks.Clear();
            _stacks.AddRange(stacks);
        }

        private int Compare(InventorySort order, string x, string y)
        {
            ItemDefinition a = _catalog.Item(x), b = _catalog.Item(y);
            int kind = KindRank(a).CompareTo(KindRank(b));
            int rarity = ((int)b.Rarity).CompareTo((int)a.Rarity);
            int level = b.RequiredLevel.CompareTo(a.RequiredLevel);
            int[] keys = order switch
            {
                InventorySort.Rarity => new[] { rarity, kind, level },
                InventorySort.Level => new[] { level, kind, rarity },
                _ => new[] { kind, rarity, level },
            };
            foreach (int k in keys)
                if (k != 0) return k;
            int name = string.CompareOrdinal(a.Name, b.Name);
            return name != 0 ? name : string.CompareOrdinal(a.Id, b.Id);
        }

        /// <summary>Rang de sorte : l'équipement dans l'ordre des places, puis les objets de soin, puis les matériaux.</summary>
        private int KindRank(ItemDefinition item)
        {
            if (item.Type == ItemType.Equipment)
            {
                for (int i = 0; i < _catalog.Places.Count; i++)
                    if (_catalog.Places[i].Accepts == item.Slot) return i;
                return _catalog.Places.Count;
            }
            return item.Type == ItemType.Consumable ? _catalog.Places.Count + 1 : _catalog.Places.Count + 2;
        }

        /// <summary>Sous-lot V1.2 : places de l'inventaire qui passent le filtre, dans l'ordre (une vue : rien ne change).</summary>
        public IReadOnlyList<int> Filter(InventoryFilter filter, int level)
        {
            var places = new List<int>();
            for (int i = 0; i < _stacks.Count; i++)
            {
                ItemDefinition item = _catalog.Item(_stacks[i].Item);
                bool keep = filter switch
                {
                    InventoryFilter.Equipment => item.Type == ItemType.Equipment,
                    InventoryFilter.Wearable => item.Type == ItemType.Equipment && item.RequiredLevel <= level,
                    InventoryFilter.Consumables => item.Type == ItemType.Consumable,
                    InventoryFilter.Materials => item.Type == ItemType.Material,
                    _ => true,
                };
                if (keep) places.Add(i);
            }
            return places;
        }

        /// <summary>Sous-lot 12.5 : copie indépendante (pour essayer des opérations avant de les appliquer pour de bon).</summary>
        public Inventory Clone()
        {
            var copy = new Inventory(_catalog, ClassId);
            copy.CopyFrom(this);
            return copy;
        }

        /// <summary>Prend les places, l'équipement et la réserve d'un autre inventaire du même catalogue.</summary>
        public void CopyFrom(Inventory other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));
            if (!ReferenceEquals(other._catalog, _catalog)) throw new ArgumentException("Inventaire d'un autre catalogue.", nameof(other));
            if (ReferenceEquals(other, this)) return;
            _stacks.Clear();
            _stacks.AddRange(other._stacks);
            _equipped.Clear();
            foreach (KeyValuePair<string, string> e in other._equipped) _equipped[e.Key] = e.Value;
            _reserved.Clear();
            foreach (KeyValuePair<string, int> r in other._reserved) _reserved[r.Key] = r.Value;
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
