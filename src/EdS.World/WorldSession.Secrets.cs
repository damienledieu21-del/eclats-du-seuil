using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;

namespace EdS.World
{
    /// <summary>V2.52 : motif d'un refus de fouille d'un secret (rien n'a changé).</summary>
    public enum SecretRefusal
    {
        None = 0,

        /// <summary>Secret inconnu de la zone de la partie (ou partie sans zone).</summary>
        Unknown,

        /// <summary>Déjà fouillé : ses récompenses ont été données (une stèle se relit, sans rien de plus).</summary>
        AlreadyOpened,

        /// <summary>Personnage absent, mort ou vaincu.</summary>
        Unavailable,

        /// <summary>Hors de portée (<see cref="WorldSession.SecretReach"/>).</summary>
        OutOfReach,

        /// <summary>En plein combat (<see cref="WorldSession.InCombat"/>).</summary>
        InCombat,

        /// <summary>Les récompenses ne tiennent pas dans l'inventaire.</summary>
        BagFull,

        /// <summary>Le registre du monde n'a plus la place de la clé du secret (en plus des morts possibles d'un tick).</summary>
        RegistryFull,
    }

    public sealed partial class WorldSession
    {
        /// <summary>V2.52 : portée de la fouille d'un secret, du centre du personnage à la place du secret (comme une porte).</summary>
        public static readonly Fixed SecretReach = Fixed.FromInt(2);

        /// <summary>V2.52 : le secret <paramref name="id"/> de la zone de la partie a déjà été fouillé (sa clé est au registre du monde).</summary>
        public bool IsSecretOpened(string id) => Definition is ZoneDefinition zone && zone.SecretOf(id) != null && Registered(zone.SecretKey(id));

        /// <summary>V2.52 : peut-on fouiller le secret <paramref name="id"/> maintenant ? <see cref="SecretRefusal.None"/> si oui.</summary>
        public SecretRefusal CanOpenSecret(string id)
        {
            if (!(Definition is ZoneDefinition zone) || id is null || !(zone.SecretOf(id) is ZoneSecret secret)) return SecretRefusal.Unknown;
            if (Registered(zone.SecretKey(id))) return SecretRefusal.AlreadyOpened;
            CombatantView me = Zone.Engine.View(CombatantId);
            if (_defeated || !me.IsPresent || me.IsDead || !(me.Position is GroundVector p)) return SecretRefusal.Unavailable;
            long dx = checked(p.X.Raw - secret.Position.X.Raw), dz = checked(p.Z.Raw - secret.Position.Z.Raw);
            if (checked(dx * dx + dz * dz) > checked(SecretReach.Raw * SecretReach.Raw)) return SecretRefusal.OutOfReach;
            if (InCombat) return SecretRefusal.InCombat;
            if (RegistryRoom - 1 < DeathReserve) return SecretRefusal.RegistryFull;
            Inventory trial = Inventory.Clone();
            foreach ((string item, int count) in secret.Rewards)
                if (trial.Add(item, count) != InventoryRefusal.None) return SecretRefusal.BagFull;
            return SecretRefusal.None;
        }

        /// <summary>
        /// V2.52 (schéma de zone 18) : fouille le secret <paramref name="id"/> à portée : ses récompenses entrent dans l'inventaire, en entier
        /// ou pas du tout, et sa clé au registre du monde (une seule fois, gardée par la sauvegarde). Refus atomique : rien ne change si
        /// <see cref="CanOpenSecret"/> refuse. Les objets donnés sont ajoutés à <paramref name="given"/> s'il est fourni.
        /// </summary>
        public SecretRefusal OpenSecret(string id, List<(string Item, int Count)>? given = null)
        {
            SecretRefusal refusal = CanOpenSecret(id);
            if (refusal != SecretRefusal.None) return refusal;
            ZoneDefinition zone = Definition!;
            ZoneSecret secret = zone.SecretOf(id)!;
            Inventory trial = Inventory.Clone();
            foreach ((string item, int count) in secret.Rewards) trial.Add(item, count);
            if (!Zone.Consume(zone.SecretKey(id))) return SecretRefusal.AlreadyOpened;
            Inventory.CopyFrom(trial);
            given?.AddRange(secret.Rewards);
            return SecretRefusal.None;
        }

        /// <summary>V2.52 : secrets de la zone de la partie à <see cref="SecretReach"/> du personnage vivant, le plus proche d'abord.</summary>
        public IReadOnlyList<ZoneSecret> SecretsInReach()
        {
            if (!(Definition is ZoneDefinition zone) || zone.Secrets.Count == 0) return Array.Empty<ZoneSecret>();
            CombatantView me = Zone.Engine.View(CombatantId);
            if (_defeated || !me.IsPresent || me.IsDead || !(me.Position is GroundVector p)) return Array.Empty<ZoneSecret>();
            long reach = checked(SecretReach.Raw * SecretReach.Raw);
            long D2(ZoneSecret s) { long dx = checked(p.X.Raw - s.Position.X.Raw), dz = checked(p.Z.Raw - s.Position.Z.Raw); return checked(dx * dx + dz * dz); }
            return zone.Secrets.Where(s => D2(s) <= reach).OrderBy(D2).ThenBy(s => s.Id, StringComparer.Ordinal).ToList();
        }
    }
}
