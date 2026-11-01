using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using EdS.Data.Items;
using EdS.Data.Saves;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
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

        /// <summary>
        /// Remarque de Codex sur la PR #78 : porté, l'objet mènerait un bonus d'équipement au-delà de la limite du combat
        /// (<see cref="GearBonus.MaxValue"/>) ; le combat refuserait le personnage. Un catalogue validé ne le permet pas (pire cumul borné).
        /// </summary>
        GearLimit = 9,
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
    /// rangement et les filtres. Sous-lot V1.3 : chaque place de l'inventaire et chaque objet porté est une <b>instance d'objet</b>
    /// (<see cref="ItemStack"/>, avec ses propriétés tirées) ; une instance aux propriétés tirées ne s'empile pas et garde ses propriétés quand
    /// elle est portée, ôtée, échangée ou rangée. L'équipement porté donne aussi ses effets (<see cref="Effects"/> : objets nommés, paliers
    /// d'ensemble) et les bonus de ses ensembles.
    /// </summary>
    public sealed class Inventory
    {
        private readonly ItemCatalog _catalog;
        private readonly List<ItemStack> _stacks = new List<ItemStack>();
        private readonly SortedDictionary<string, ItemStack> _equipped = new SortedDictionary<string, ItemStack>(StringComparer.Ordinal);

        /// <summary>Sous-lot 12.6 (revue Codex de la PR #47) : objets de soin emportés au combat, réservés jusqu'à ce qu'ils soient bus.</summary>
        private readonly SortedDictionary<string, int> _reserved = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public ItemCatalog Catalog => _catalog;

        /// <summary>Sous-lot V1.2 : classe du personnage (places obligatoires selon la classe) ; null : seulement les places obligatoires pour tous.</summary>
        public string? ClassId { get; }

        /// <summary>Places occupées, dans l'ordre (vue en lecture seule : on ne change l'inventaire que par ses opérations).</summary>
        public IReadOnlyList<ItemStack> Stacks => _stacks.AsReadOnly();

        /// <summary>Objet porté par place (ordre ordinal ; vue en lecture seule, une copie).</summary>
        public IReadOnlyDictionary<string, string> Equipped =>
            new ReadOnlyDictionary<string, string>(_equipped.ToDictionary(e => e.Key, e => e.Value.Item, StringComparer.Ordinal));

        /// <summary>Relecture de la PR #75 : objet porté à la place <paramref name="place"/>, ou null (sans allouer de vue, pour l'affichage à chaque image).</summary>
        public string? WornAt(string place) => place != null && _equipped.TryGetValue(place, out ItemStack worn) ? worn.Item : null;

        /// <summary>Sous-lot V1.3 : instance portée à la place <paramref name="place"/> (avec ses propriétés tirées), ou null.</summary>
        public ItemStack? WornInstanceAt(string place) => place != null && _equipped.TryGetValue(place, out ItemStack worn) ? worn : null;

        public int FreePlaces => _catalog.InventorySize - _stacks.Count;

        /// <summary>Sous-lot V1.4 : places d'équipement occupées, dans l'ordre des places du catalogue.</summary>
        public IReadOnlyList<string> Places() => _catalog.Places.Select(p => p.Id).Where(_equipped.ContainsKey).ToList();

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
        /// <exception cref="ArgumentException">Sauvegarde incohérente avec le catalogue (premier motif dans le message). Remarque de Codex sur la
        /// PR #82 : un objet porté au-dessus du niveau n'en est plus une ; il est rangé dans le sac (voir l'autre forme).</exception>
        public static Inventory From(SavedCharacter character, ItemCatalog catalog, int level) => From(character, catalog, level, out _);

        /// <summary>
        /// Comme <see cref="From(SavedCharacter, ItemCatalog, int)"/>. Relecture de la PR #78 : les propriétés tirées des instances (sac et
        /// objets portés) sont d'abord accordées au catalogue actuel (<see cref="ItemCatalog.Settle"/> : valeur ramenée dans ses bornes,
        /// propriété inconnue retirée) : un réglage du catalogue ne rend jamais une partie illisible. <paramref name="adjusted"/> rend le
        /// compte rendu de ces changements (vide si rien n'a changé) ; la prochaine sauvegarde les garde.
        /// </summary>
        public static Inventory From(SavedCharacter character, ItemCatalog catalog, int level, out IReadOnlyList<string> adjusted)
        {
            if (character is null) throw new ArgumentNullException(nameof(character));
            if (catalog is null) throw new ArgumentNullException(nameof(catalog));
            var notes = new List<string>();
            adjusted = notes;
            var inventory = new Inventory(catalog, character.ClassId);
            // Remarque de Codex sur la PR #83 : les pièces du combat sont suivies par identité ; chaque place de l'inventaire reçoit donc sa
            // propre instance, même si le personnage donné partage un même objet entre deux places.
            var seen = new List<ItemStack>();
            ItemStack Distinct(ItemStack x)
            {
                if (seen.Any(y => ReferenceEquals(y, x))) x = x.WithCount(x.Count);
                seen.Add(x);
                return x;
            }
            if (character.Inventory.Count > catalog.InventorySize)
                throw new ArgumentException($"Inventaire de {character.Inventory.Count} places, au plus {catalog.InventorySize}.", nameof(character));
            for (int i = 0; i < character.Inventory.Count; i++)
            {
                ItemStack stack = character.Inventory[i];
                if (!catalog.TryGetItem(stack.Item, out ItemDefinition item))
                    throw new ArgumentException($"Inventaire, place {i} : objet « {stack.Item} » absent du catalogue.", nameof(character));
                if (stack.Count < 1 || stack.Count > item.Stack)
                    throw new ArgumentException($"Inventaire, place {i} : {stack.Count} « {stack.Item} », pile de 1 à {item.Stack}.", nameof(character));
                // Sous-lot V1.3 : propriétés tirées dans les bornes du catalogue (le validateur unique des instances), après leur accord au
                // catalogue actuel (relecture de la PR #78).
                stack = catalog.Settle(stack, out IReadOnlyList<string> settled);
                notes.AddRange(settled);
                if (catalog.CheckInstance(stack) is string problem) throw new ArgumentException($"Inventaire, place {i} : {problem}", nameof(character));
                inventory._stacks.Add(Distinct(stack));
            }
            foreach (KeyValuePair<string, string> e in character.Equipment)
            {
                if (!catalog.TryGetPlace(e.Key, out EquipmentPlace place)) throw new ArgumentException($"Emplacement « {e.Key} » inconnu du catalogue.", nameof(character));
                if (!catalog.TryGetItem(e.Value, out ItemDefinition item))
                    throw new ArgumentException($"Équipement « {e.Key} » : objet « {e.Value} » absent du catalogue.", nameof(character));
                if (item.Type != ItemType.Equipment || item.Slot != place.Accepts)
                    throw new ArgumentException($"Équipement « {e.Key} » : « {e.Value} » ne se porte pas à cet emplacement.", nameof(character));
                // Sous-lot V1.4 : l'instance portée (propriétés, qualité, usure), ou l'objet de base.
                ItemStack worn = character.WornInstance(e.Key)!;
                worn = catalog.Settle(worn, out IReadOnlyList<string> settledWorn);
                notes.AddRange(settledWorn);
                if (catalog.CheckInstance(worn) is string problem) throw new ArgumentException($"Équipement « {e.Key} » : {problem}", nameof(character));
                worn = Distinct(worn);
                // Remarque de Codex sur la PR #82 (le chargement d'une sauvegarde ne lève jamais pour un niveau devenu trop bas) : un objet
                // porté qui demande un niveau plus haut est rangé dans le sac s'il y a de la place (sinon il reste porté) ; le compte rendu le dit.
                if (item.RequiredLevel > level && inventory._stacks.Count < catalog.InventorySize)
                {
                    inventory._stacks.Add(worn);
                    notes.Add($"« {item.Id} » demande le niveau {item.RequiredLevel} (niveau {level}) : rangé dans le sac.");
                    continue;
                }
                inventory._equipped[e.Key] = worn;
            }
            // Remarque de Codex sur la PR #78 : un équipement relu au-delà de la limite du combat (impossible avec un catalogue validé) est
            // plafonné par Gear() ; le compte rendu le dit.
            GearBonus raw = inventory.RawGear();
            for (int g = 0; g < GearBonus.Fields.Count; g++)
                if (raw[g] > GearBonus.MaxValue) notes.Add($"bonus d'équipement « {GearBonus.Fields[g]} » de {raw[g]} ramené à {GearBonus.MaxValue} (limite du combat).");
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
                if (inventory.FreePlaceFor(catalog.Item(id)) is EquipmentPlace place) inventory._equipped[place.Id] = new ItemStack(id, 1);
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
                    ItemStack instance = _stacks[found];
                    _stacks.RemoveAt(found);
                    _equipped[place.Id] = instance;
                    done.Add(instance.Item);
                    continue;
                }
                // Objet de départ de la classe (ou de toutes : StartingFor d'une classe inconnue n'en rend que celles-là).
                string? start = _catalog.StartingFor(ClassId ?? "").FirstOrDefault(s => _catalog.Item(s).Slot == place.Accepts);
                if (start is null) continue;
                _equipped[place.Id] = new ItemStack(start, 1);
                done.Add(start);
            }
            return done;
        }

        /// <summary>
        /// Somme des bonus des objets portés (l'outil de métier n'en a pas). Sous-lot V1.3 : avec leurs propriétés tirées et les bonus aux
        /// statistiques des paliers d'ensemble atteints.
        /// </summary>
        public GearBonus Gear()
        {
            // Remarque de Codex sur la PR #78 : jamais au-delà de la limite du combat (dernier recours : un catalogue validé borne déjà le pire
            // cumul, et EquipTo refuse ce qui la dépasserait).
            GearBonus total = RawGear();
            if (WithinCombatLimit(total)) return total;
            var values = new int[GearBonus.Fields.Count];
            for (int i = 0; i < values.Length; i++) values[i] = Math.Min(total[i], GearBonus.MaxValue);
            return GearBonus.FromValues(values);
        }

        /// <summary>Somme des bonus portés, sans plafond.</summary>
        private GearBonus RawGear()
        {
            GearBonus total = GearBonus.None;
            foreach (ItemStack worn in _equipped.Values) total += _catalog.BonusOf(worn);   // sous-lot V1.4 : rien pour une pièce usée à zéro
            foreach ((ItemSet set, int count) in WornSets())
                foreach (SetBonus bonus in set.Bonuses)
                    if (bonus.Pieces <= count) total += bonus.Bonus;
            return total;
        }

        /// <summary>
        /// Remarque de Codex sur la PR #78 : chaque champ du bonus dans la limite que le combat admet (<c>CombatSetup.Validate</c> : de 0 à
        /// <see cref="GearBonus.MaxValue"/>).
        /// </summary>
        public static bool WithinCombatLimit(GearBonus gear)
        {
            if (gear is null) throw new ArgumentNullException(nameof(gear));
            for (int i = 0; i < GearBonus.Fields.Count; i++)
                if (gear[i] < 0 || gear[i] > GearBonus.MaxValue) return false;
            return true;
        }

        /// <summary>Remarque de Codex sur la PR #78 : le bonus porté, sans plafond, est-il dans la limite du combat ?</summary>
        public bool GearWithinCombatLimit => WithinCombatLimit(RawGear());

        /// <summary>
        /// Sous-lot V1.3 : ensembles dont au moins une pièce est portée, dans l'ordre du catalogue, avec le nombre de pièces différentes portées
        /// (une même pièce à deux places ne compte qu'une fois).
        /// </summary>
        public IReadOnlyList<(ItemSet Set, int Worn)> WornSets()
        {
            var worn = new List<(ItemSet, int)>();
            foreach (ItemSet set in _catalog.Sets)
            {
                // Sous-lot V1.4 : une pièce usée à zéro ne compte plus pour son ensemble.
                int count = _equipped.Values.Where(x => !_catalog.IsWornOut(x)).Select(x => x.Item).Distinct(StringComparer.Ordinal).Count(id => set.Pieces.Contains(id));
                if (count > 0) worn.Add((set, count));
            }
            return worn;
        }

        /// <summary>Sous-lot V1.3 : pièces de l'ensemble <paramref name="setId"/> portées (0 si aucune ou ensemble inconnu).</summary>
        public int WornPieces(string setId)
        {
            foreach ((ItemSet set, int count) in WornSets())
                if (set.Id == setId) return count;
            return 0;
        }

        /// <summary>
        /// Sous-lot V1.3 : effets de l'équipement porté, pour le prochain combat : ceux des objets nommés, dans l'ordre des places du
        /// catalogue (un même objet à deux places ne compte qu'une fois), puis ceux des paliers d'ensemble atteints, dans l'ordre des
        /// ensembles et des paliers.
        /// </summary>
        public IReadOnlyList<EquipmentEffect> Effects()
        {
            var effects = new List<EquipmentEffect>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (EquipmentPlace place in _catalog.Places)
                if (_equipped.TryGetValue(place.Id, out ItemStack worn) && !_catalog.IsWornOut(worn) && _catalog.Item(worn.Item).Effect is ItemEffect effect && seen.Add(worn.Item))
                    effects.Add(effect.Effect);
            foreach ((ItemSet set, int count) in WornSets())
                foreach (SetBonus bonus in set.Bonuses)
                    if (bonus.Pieces <= count && bonus.Effect is EquipmentEffect setEffect) effects.Add(setEffect);
            return effects;
        }

        /// <summary>Sous-lot V1.2 : garde du bouclier porté (place qui porte la garde), ou null : celle des règles.</summary>
        /// <remarks>
        /// Sous-lot V1.4 : un bouclier usé à zéro ne donne plus sa garde propre (null ici ; il reste porté). Au combat, voir
        /// <see cref="ShieldFor"/> : la garde de repli n'est jamais meilleure que la sienne.
        /// </remarks>
        public ShieldGuard? Shield =>
            _catalog.GuardPlace is { } place && _equipped.TryGetValue(place.Id, out ItemStack worn) && !_catalog.IsWornOut(worn) ? _catalog.Item(worn.Item).Guard : null;

        /// <summary>
        /// Sous-lot V1.4 (relecture indépendante de la PR #80) : garde avec laquelle le personnage entre au combat. Bouclier en état : sa
        /// garde propre ; aucun bouclier : null (celle des règles). Bouclier usé à zéro : pour chaque valeur, <b>la plus faible</b> de la
        /// sienne et de celle des règles (part bloquée la plus basse, coût d'un blocage le plus haut, fenêtre de parade la plus étroite),
        /// ramenée dans les bornes d'un bouclier (<c>combat.shield</c>, <see cref="ShieldGuard.Check"/>) : jamais meilleure que la sienne, et
        /// toujours admise par le combat.
        /// </summary>
        public ShieldGuard? ShieldFor(CombatRules rules)
        {
            if (rules is null) throw new ArgumentNullException(nameof(rules));
            if (!(_catalog.GuardPlace is { } place) || !_equipped.TryGetValue(place.Id, out ItemStack worn) || !(_catalog.Item(worn.Item).Guard is ShieldGuard own))
                return null;
            if (!_catalog.IsWornOut(worn)) return own;
            Fixed reduction = Fixed.Max(rules.ShieldMinDamageReduction, Fixed.Min(own.DamageReduction, rules.GuardDamageReduction));
            int cost = Math.Min(rules.ShieldMaxCostPerBlock, Math.Max(own.CostPerBlock, rules.GuardCostPerBlock));
            long low = Math.Max(rules.ParryWindowMs - rules.ShieldParryWindowShiftMs, rules.TickMilliseconds);
            if (rules.TickMilliseconds > 0 && low % rules.TickMilliseconds != 0) low += rules.TickMilliseconds - low % rules.TickMilliseconds;
            long window = Math.Min(own.ParryWindowMs, Math.Max(low, rules.ParryWindowMs));
            var fallback = new ShieldGuard(reduction, cost, window);
            return fallback.Check(rules, "shield").Count == 0 ? fallback : own;
        }

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
        /// Sous-lot V1.3 : range une instance d'objet. Aux propriétés tirées (valides pour le catalogue), elle prend une place libre à la fin ;
        /// sinon, comme <see cref="Add(string, int)"/>. Tout ou rien.
        /// </summary>
        /// <exception cref="ArgumentException">Objet absent du catalogue, quantité non positive, ou propriétés refusées par le catalogue.</exception>
        public InventoryRefusal Add(ItemStack stack)
        {
            if (stack is null) throw new ArgumentNullException(nameof(stack));
            if (!stack.IsUnique) return Add(stack.Item, stack.Count);
            if (_catalog.CheckInstance(stack) is string problem) throw new ArgumentException(problem, nameof(stack));
            if (FreePlaces < 1) return InventoryRefusal.Full;
            _stacks.Add(stack);
            return InventoryRefusal.None;
        }

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
                if (_stacks[i].Item == item && !_stacks[i].IsUnique && _stacks[i].Count < definition.Stack)
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
        /// réservés (<see cref="Reserved"/>) ne se retirent pas. Relecture de la PR #78 : les copies de base (sans propriété tirée) partent
        /// d'abord ; une instance aux propriétés tirées n'est prise que si les copies de base ne suffisent pas (fabrication, objet pris par
        /// une quête, livraison, vente par identifiant).
        /// </summary>
        public InventoryRefusal Remove(string item, int count) => Remove(item, count, out _);

        /// <summary>
        /// Sous-lot V1.3 : comme <see cref="Remove(string, int)"/>, et rend les instances retirées (avec leurs propriétés tirées), dans l'ordre du
        /// retrait, pour un rachat fidèle.
        /// </summary>
        public InventoryRefusal Remove(string item, int count, out IReadOnlyList<ItemStack> removed)
        {
            removed = Array.Empty<ItemStack>();
            if (count < 1 || Available(item) < count) return InventoryRefusal.NotEnough;
            removed = Take(item, count);
            return InventoryRefusal.None;
        }

        /// <summary>
        /// Sous-lot V1.3 : retire l'instance de la place <paramref name="place"/> de l'inventaire en entier (une instance aux propriétés tirées
        /// se vend ou se jette elle-même, pas une autre du même objet). Refusé si la place est vide ou si une partie est réservée.
        /// </summary>
        public InventoryRefusal RemoveAt(int place, out ItemStack? removed)
        {
            removed = null;
            if (place < 0 || place >= _stacks.Count) return InventoryRefusal.NothingThere;
            return RemoveAt(place, _stacks[place].Count, out removed);
        }

        /// <summary>
        /// Relecture de la PR #78 : retire <paramref name="count"/> objets de <b>la place exacte</b> <paramref name="place"/> de l'inventaire
        /// (la pile entière, ou seulement une partie : la place garde le reste), jamais d'une autre place du même objet. Tout ou rien : refusé
        /// si la place est vide (<see cref="InventoryRefusal.NothingThere"/>), si la quantité est hors de [1, pile] ou si elle entamerait la
        /// réserve (<see cref="InventoryRefusal.NotEnough"/>). Rend l'instance retirée (propriétés tirées comprises), à la quantité retirée.
        /// </summary>
        public InventoryRefusal RemoveAt(int place, int count, out ItemStack? removed)
        {
            removed = null;
            if (place < 0 || place >= _stacks.Count) return InventoryRefusal.NothingThere;
            ItemStack stack = _stacks[place];
            if (count < 1 || count > stack.Count || Available(stack.Item) < count) return InventoryRefusal.NotEnough;
            if (count == stack.Count) _stacks.RemoveAt(place);
            else _stacks[place] = stack.WithCount(stack.Count - count);
            removed = stack.WithCount(count);
            return InventoryRefusal.None;
        }

        // Relecture de la PR #78 : deux passes, des dernières places vers les premières : les copies de base d'abord, puis seulement s'il en
        // manque les instances aux propriétés tirées (une instance tirée ne part jamais quand les copies de base suffisent).
        private IReadOnlyList<ItemStack> Take(string item, int count)
        {
            var removed = new List<ItemStack>();
            int left = count;
            foreach (bool rolled in new[] { false, true })
                for (int i = _stacks.Count - 1; i >= 0 && left > 0; i--)
                    if (_stacks[i].Item == item && _stacks[i].IsUnique == rolled)
                    {
                        int take = Math.Min(left, _stacks[i].Count);
                        removed.Add(_stacks[i].WithCount(take));
                        if (take == _stacks[i].Count) _stacks.RemoveAt(i);
                        else _stacks[i] = _stacks[i].WithCount(_stacks[i].Count - take);
                        left -= take;
                    }
            return removed;
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
            ItemStack instance = _stacks[place];   // sous-lot V1.3 : l'instance, avec ses propriétés tirées
            bool hadPrevious = _equipped.TryGetValue(slot, out ItemStack previous);
            if (hadPrevious) _stacks[place] = previous;
            else _stacks.RemoveAt(place);
            _equipped[slot] = instance;
            // Remarque de Codex sur la PR #78 : jamais un équipement que le combat refuserait ; sinon tout revient comme avant.
            if (!GearWithinCombatLimit)
            {
                if (hadPrevious) { _equipped[slot] = previous; _stacks[place] = instance; }
                else { _equipped.Remove(slot); _stacks.Insert(place, instance); }
                return InventoryRefusal.GearLimit;
            }
            return InventoryRefusal.None;
        }

        /// <summary>
        /// Retire l'objet porté à <paramref name="slot"/> et le range à la fin de l'inventaire, s'il reste une place. Sous-lot V1.2 : refusé à
        /// une place obligatoire pour ce personnage (<see cref="InventoryRefusal.Required"/> : l'Éclat du Seuil, le bouclier du Bastion), où
        /// l'on porte un autre objet à la place.
        /// </summary>
        public InventoryRefusal Unequip(string slot)
        {
            if (slot is null || !_equipped.TryGetValue(slot, out ItemStack item)) return InventoryRefusal.NothingThere;
            if (_catalog.TryGetPlace(slot, out EquipmentPlace place) && place.IsRequiredFor(ClassId)) return InventoryRefusal.Required;
            if (FreePlaces < 1) return InventoryRefusal.Full;
            _equipped.Remove(slot);
            _stacks.Add(item);
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
            bool hasA = _equipped.TryGetValue(first, out ItemStack itemA), hasB = _equipped.TryGetValue(second, out ItemStack itemB);
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
            // Sous-lot V1.3 : une instance aux propriétés tirées reste une entrée à part (elle ne s'empile pas) ; à objet égal, l'objet de base
            // d'abord, puis les instances dans leur ordre d'avant (tri stable).
            var entries = new List<(string Item, int Count, ItemStack? Rolled)>();
            foreach (ItemStack s in _stacks)
            {
                if (s.IsUnique) { entries.Add((s.Item, s.Count, s)); continue; }
                int k = entries.FindIndex(c => c.Rolled is null && c.Item == s.Item);
                if (k < 0) entries.Add((s.Item, s.Count, null));
                else entries[k] = (s.Item, checked(entries[k].Count + s.Count), null);
            }
            var sorted = entries.OrderBy(c => c.Item, Comparer<string>.Create((x, y) => Compare(order, x, y))).ThenBy(c => c.Rolled is null ? 0 : 1).ToList();
            var stacks = new List<ItemStack>();
            foreach ((string item, int count, ItemStack? rolled) in sorted)
            {
                if (rolled != null) { stacks.Add(rolled); continue; }
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

        /// <summary>
        /// Sous-lot V1.4 (relecture indépendante de la PR #80) : use de <paramref name="loss"/> points les pièces <paramref name="pieces"/>
        /// (celles portées à l'entrée dans la zone, celles du combat), où qu'elles soient maintenant : portées (à n'importe quelle place) ou
        /// dans le sac ; une pièce vendue ou jetée depuis n'est plus usée. Remarque de Codex sur la PR #83 : chaque pièce est cherchée par
        /// <b>son identité</b> (la même instance, que l'inventaire déplace telle quelle entre les places et le sac), jamais par sa valeur : une
        /// copie identique dans le sac n'est pas usée à sa place ; une pièce qui ne s'use pas est sautée. Rend la nouvelle valeur de chaque pièce,
        /// dans l'ordre (la même si elle n'est plus là), et dans <paramref name="wornOut"/> les objets qui viennent d'arriver à zéro.
        /// </summary>
        internal IReadOnlyList<ItemStack> WearPieces(IReadOnlyList<ItemStack> pieces, int loss, out IReadOnlyList<string> wornOut) =>
            WearPieces(pieces, pieces.Select(_ => loss).ToList(), out wornOut);

        /// <summary>
        /// Sous-lot V1.8 : comme <see cref="WearPieces(IReadOnlyList{ItemStack}, int, out IReadOnlyList{string})"/>, chaque pièce avec sa
        /// propre perte (<paramref name="losses"/>, même ordre : la résistance à l'usure dépend du niveau et de la rareté de la pièce).
        /// </summary>
        internal IReadOnlyList<ItemStack> WearPieces(IReadOnlyList<ItemStack> pieces, IReadOnlyList<int> losses, out IReadOnlyList<string> wornOut)
        {
            if (pieces is null) throw new ArgumentNullException(nameof(pieces));
            if (losses is null || losses.Count != pieces.Count) throw new ArgumentException("Une perte par pièce.", nameof(losses));
            if (losses.Any(l => l < 0)) throw new ArgumentOutOfRangeException(nameof(losses));
            var updated = new List<ItemStack>(pieces);
            var zero = new List<string>();
            wornOut = zero;
            if (losses.All(l => l == 0)) return updated;
            var usedSlots = new HashSet<string>(StringComparer.Ordinal);
            var usedPlaces = new HashSet<int>();
            for (int k = 0; k < pieces.Count; k++)
            {
                ItemStack piece = pieces[k];
                if (!_catalog.TryGetItem(piece.Item, out ItemDefinition item) || !_catalog.Wears(item)) continue;
                int left = _catalog.WearLeft(piece);
                if (left == 0) continue;
                int lost = losses[k];   // sous-lot V1.8 : perte propre à la pièce (résistance à l'usure)
                if (lost == 0) continue;
                ItemStack after = piece.WithWear(Math.Max(0, left - lost));
                string? slot = _catalog.Places.Select(p => p.Id).FirstOrDefault(id => !usedSlots.Contains(id) && _equipped.TryGetValue(id, out ItemStack e) && ReferenceEquals(e, piece));
                if (slot != null)
                {
                    usedSlots.Add(slot);
                    _equipped[slot] = after;
                }
                else
                {
                    int place = -1;
                    for (int i = 0; i < _stacks.Count && place < 0; i++)
                        if (!usedPlaces.Contains(i) && ReferenceEquals(_stacks[i], piece)) place = i;
                    if (place < 0) continue;   // plus dans l'inventaire (vendue, jetée)
                    usedPlaces.Add(place);
                    _stacks[place] = after;
                }
                updated[k] = after;
                if (after.Wear == 0) zero.Add(piece.Item);
            }
            return updated;
        }

        /// <summary>
        /// Sous-lot V1.4 : pièces entamées (usure qui n'est pas pleine) : les objets portés dans l'ordre des places (<c>Slot</c>, place du sac
        /// −1), puis ceux du sac dans l'ordre (<c>Slot</c> null, <c>Place</c> : leur place).
        /// </summary>
        public IReadOnlyList<(string? Slot, int Place, ItemStack Stack)> Damaged()
        {
            var damaged = new List<(string?, int, ItemStack)>();
            foreach (EquipmentPlace place in _catalog.Places)
                if (_equipped.TryGetValue(place.Id, out ItemStack worn) && worn.Wear != null) damaged.Add((place.Id, -1, worn));
            for (int i = 0; i < _stacks.Count; i++)
                if (_stacks[i].Wear != null) damaged.Add((null, i, _stacks[i]));
            return damaged;
        }

        /// <summary>
        /// Sous-lot V1.4 : remet à neuf (usure pleine) toutes les pièces entamées, portées et dans le sac. Le reste ne change pas. Remarque de
        /// Codex sur la PR #83 : rend chaque pièce remplacée avec la nouvelle instance (pour suivre les pièces du combat par identité).
        /// </summary>
        internal IReadOnlyList<(ItemStack Before, ItemStack After)> RepairAll()
        {
            var replaced = new List<(ItemStack, ItemStack)>();
            foreach (string slot in _equipped.Keys.ToList())
                if (_equipped[slot].Wear != null)
                {
                    ItemStack after = _equipped[slot].WithWear(null);
                    replaced.Add((_equipped[slot], after));
                    _equipped[slot] = after;
                }
            for (int i = 0; i < _stacks.Count; i++)
                if (_stacks[i].Wear != null)
                {
                    ItemStack after = _stacks[i].WithWear(null);
                    replaced.Add((_stacks[i], after));
                    _stacks[i] = after;
                }
            return replaced;
        }

        /// <summary>
        /// Sous-lot V1.8 (réponse de Damien : réparer « tout d'un coup ou pièce par pièce ») : remet à neuf une seule pièce entamée, portée
        /// à la place <paramref name="slot"/> ou (slot null) dans le sac à la place <paramref name="place"/>. Rend la pièce telle qu'elle
        /// était, ou null si rien n'est entamé là (rien ne change) ; <paramref name="after"/> : la nouvelle instance (remarque de Codex sur la
        /// PR #83 : suivi des pièces du combat par identité).
        /// </summary>
        internal ItemStack? RepairOne(string? slot, int place, out ItemStack? after)
        {
            after = null;
            if (slot != null)
            {
                if (!_equipped.TryGetValue(slot, out ItemStack worn) || worn.Wear is null) return null;
                after = worn.WithWear(null);
                _equipped[slot] = after;
                return worn;
            }
            if (place < 0 || place >= _stacks.Count || _stacks[place].Wear is null) return null;
            ItemStack before = _stacks[place];
            after = before.WithWear(null);
            _stacks[place] = after;
            return before;
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
            foreach (KeyValuePair<string, ItemStack> e in other._equipped) _equipped[e.Key] = e.Value;
            _reserved.Clear();
            foreach (KeyValuePair<string, int> r in other._reserved) _reserved[r.Key] = r.Value;
        }

        /// <summary>Personnage avec cet inventaire et cet équipement (sous-lot V1.3 : propriétés tirées comprises).</summary>
        /// <remarks>Sous-lot V1.4 : chaque objet porté avec son instance (qualité et usure comprises).</remarks>
        public SavedCharacter SaveInto(SavedCharacter character) =>
            (character ?? throw new ArgumentNullException(nameof(character))).WithItems(_stacks,
                _equipped.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal));

        // Places nouvelles nécessaires pour ranger count item (piles entamées d'abord).
        private int? Room(string item, int count)
        {
            if (!_catalog.TryGetItem(item, out ItemDefinition definition) || count < 1) return null;
            long left = count;
            foreach (ItemStack s in _stacks)
                if (s.Item == item && !s.IsUnique) left -= definition.Stack - s.Count;
            if (left <= 0) return 0;
            return (int)((left + definition.Stack - 1) / definition.Stack);
        }
    }
}
