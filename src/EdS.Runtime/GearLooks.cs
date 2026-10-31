using System;
using EdS.Data.Items;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V1.2 : forme montrée de l'arme portée.</summary>
    public enum WeaponLook
    {
        None = 0,
        Sword = 1,
        Mace = 2,
        Axe = 3,
        Hammer = 4,
    }

    /// <summary>Sous-lot V1.2 : forme montrée du bouclier porté.</summary>
    public enum ShieldLook
    {
        None = 0,

        /// <summary>Écu (la forme du bouclier du paladin).</summary>
        Heater = 1,

        /// <summary>Rondache ou targe : petit bouclier rond.</summary>
        Round = 2,

        /// <summary>Pavois : grand bouclier rectangulaire.</summary>
        Tower = 3,
    }

    /// <summary>
    /// Sous-lot V1.2 (« sur le Bastion, on voit l'arme, le bouclier et la cape ») : ce que l'affichage montre de l'équipement porté. Les pièces
    /// d'armure ne se voient pas : elles attendront des modèles en morceaux (aucun achat sans l'accord de Damien). Affichage seulement.
    /// </summary>
    public sealed class VisibleGear : IEquatable<VisibleGear>
    {
        public WeaponLook Weapon { get; }
        public ItemRarity WeaponRarity { get; }
        public ShieldLook Shield { get; }
        public ItemRarity ShieldRarity { get; }

        /// <summary>Une cape est portée.</summary>
        public bool Cloak { get; }
        public ItemRarity CloakRarity { get; }

        public VisibleGear(WeaponLook weapon, ItemRarity weaponRarity, ShieldLook shield, ItemRarity shieldRarity, bool cloak, ItemRarity cloakRarity)
        {
            Weapon = weapon;
            WeaponRarity = weaponRarity;
            Shield = shield;
            ShieldRarity = shieldRarity;
            Cloak = cloak;
            CloakRarity = cloakRarity;
        }

        public bool Equals(VisibleGear? other) => other is not null && Weapon == other.Weapon && WeaponRarity == other.WeaponRarity && Shield == other.Shield
            && ShieldRarity == other.ShieldRarity && Cloak == other.Cloak && CloakRarity == other.CloakRarity;

        public override bool Equals(object? obj) => Equals(obj as VisibleGear);

        public override int GetHashCode() => ((int)Weapon * 7 + (int)WeaponRarity) * 1000 + ((int)Shield * 7 + (int)ShieldRarity) * 10 + (Cloak ? 1 + (int)CloakRarity : 0);
    }

    /// <summary>Sous-lot V1.2 : formes montrées des objets visibles, d'après leur identifiant.</summary>
    public static class GearLooks
    {
        /// <summary>Forme de l'arme : marteau, masse ou fléau, hache ou hachette, sinon épée (épées, lames).</summary>
        public static WeaponLook WeaponOf(string id)
        {
            if (id is null) throw new ArgumentNullException(nameof(id));
            if (id.Contains("marteau")) return WeaponLook.Hammer;
            if (id.Contains("masse") || id.Contains("fleau")) return WeaponLook.Mace;
            if (id.Contains("hache")) return WeaponLook.Axe;
            return WeaponLook.Sword;
        }

        /// <summary>Forme du bouclier : pavois (grand), rondache ou targe (rond), sinon écu.</summary>
        public static ShieldLook ShieldOf(string id)
        {
            if (id is null) throw new ArgumentNullException(nameof(id));
            if (id.Contains("pavois") || id.Contains("tour")) return ShieldLook.Tower;
            if (id.Contains("rondache") || id.Contains("targe")) return ShieldLook.Round;
            return ShieldLook.Heater;
        }

        /// <summary>L'arme, le bouclier et la cape portés dans <paramref name="inventory"/>.</summary>
        public static VisibleGear For(Inventory inventory) => For(inventory, null);

        /// <summary>
        /// Relecture de la PR #75 : comme <see cref="For(Inventory)"/>, mais rend <paramref name="previous"/> lui-même s'il montre déjà
        /// la même chose : appelé à chaque image, rien n'est alloué tant que l'équipement visible ne change pas.
        /// </summary>
        public static VisibleGear For(Inventory inventory, VisibleGear? previous)
        {
            if (inventory is null) throw new ArgumentNullException(nameof(inventory));
            ItemDefinition? Worn(string place) => inventory.WornAt(place) is string id ? inventory.Catalog.Item(id) : null;
            ItemDefinition? weapon = Worn("weapon"), shield = Worn("shield"), cloak = Worn("cloak");
            WeaponLook weaponLook = weapon is null ? WeaponLook.None : WeaponOf(weapon.Id);
            ShieldLook shieldLook = shield is null ? ShieldLook.None : ShieldOf(shield.Id);
            ItemRarity weaponRarity = weapon?.Rarity ?? ItemRarity.Common, shieldRarity = shield?.Rarity ?? ItemRarity.Common;
            ItemRarity cloakRarity = cloak?.Rarity ?? ItemRarity.Common;
            if (previous != null && previous.Weapon == weaponLook && previous.WeaponRarity == weaponRarity && previous.Shield == shieldLook
                && previous.ShieldRarity == shieldRarity && previous.Cloak == (cloak != null) && previous.CloakRarity == cloakRarity)
                return previous;
            return new VisibleGear(weaponLook, weaponRarity, shieldLook, shieldRarity, cloak != null, cloakRarity);
        }
    }
}
