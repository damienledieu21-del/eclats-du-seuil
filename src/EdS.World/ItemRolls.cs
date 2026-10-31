using System;
using System.Collections.Generic;
using EdS.Data.Items;
using EdS.Data.Saves;
using EdS.Simulation.Randomness;

namespace EdS.World
{
    /// <summary>
    /// Sous-lot V1.3 (plan des Vallons solo : « reste du butin aux propriétés tirées au hasard ») : tirage des propriétés d'un objet du butin.
    /// Seuls les objets qui en tirent (<see cref="ItemCatalog.IsRollable"/> : ni nommés, ni pièces d'ensemble) en reçoivent ; la rareté fixe
    /// leur nombre et l'échelle des bornes, le niveau requis de l'objet fixe les bornes (<see cref="ItemCatalog.Bounds"/>) : le hasard ne
    /// choisit que les propriétés, parmi celles de sa sorte de place, et leur valeur dans ces bornes (cadrage Q30). Comme le butin, le tirage
    /// est déterminé par la clé unique de l'événement (mort d'une créature) : <see cref="Pcg32"/> initialisé par l'empreinte FNV-1a 64 bits
    /// de la clé, sur un flux propre à l'objet et à son rang dans la table (les tirages de chance du butin ne changent pas). Le même événement
    /// donne toujours le même objet.
    /// </summary>
    public static class ItemRolls
    {
        /// <summary>
        /// Instance de <paramref name="itemId"/> tombée de l'événement <paramref name="eventKey"/>, au rang <paramref name="drop"/> de sa table
        /// (<see cref="LootRoll.Rank"/> : le rang dans le fichier, pas parmi les objets tombés ; relecture de la PR #78) :
        /// aux propriétés tirées si l'objet en tire, sinon l'objet de base (quantité <paramref name="count"/>). Une instance aux propriétés
        /// tirées a toujours la quantité 1 et passe toujours <see cref="ItemCatalog.CheckInstance"/>.
        /// </summary>
        /// <exception cref="ArgumentException">Objet absent du catalogue, ou clé d'événement hors du format.</exception>
        public static ItemStack Roll(ItemCatalog catalog, string itemId, int count, string eventKey, int drop)
        {
            if (catalog is null) throw new ArgumentNullException(nameof(catalog));
            ItemDefinition item = catalog.Item(itemId);
            if (!SaveValidator.IsEventKey(eventKey)) throw new ArgumentException($"Clé d'événement « {eventKey} » hors du format.", nameof(eventKey));
            if (!catalog.IsRollable(item)) return new ItemStack(item.Id, count);
            var random = new Pcg32(Loot.Fnv1a(eventKey), Loot.Fnv1a("properties:" + item.Id + ":" + drop.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            var pool = new List<PropertyDefinition>(catalog.PropertiesFor(item));
            int wanted = Math.Min(catalog.RollCount(item.Rarity), pool.Count);
            var rolled = new List<RolledProperty>(wanted);
            for (int k = 0; k < wanted; k++)
            {
                int pick = (int)random.NextBounded((uint)pool.Count);
                PropertyDefinition property = pool[pick];
                pool.RemoveAt(pick);
                (int low, int high) = catalog.Bounds(property, item);
                int value = checked(low + (int)random.NextBounded((uint)(high - low + 1)));
                rolled.Add(new RolledProperty(property.Id, value));
            }
            return new ItemStack(item.Id, 1, rolled);
        }
    }
}
