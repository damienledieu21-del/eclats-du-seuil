using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Domain.Combat
{
    /// <summary>
    /// Sous-lot V2.26 (secteurs de simulation, PLAN_GRANDE_CARTE.md § 2.4, point 1 ; rupture retenue par Damien le 11 novembre 2026) : état
    /// de combat qu'un combattant emporte d'une session à l'autre quand il passe d'un secteur au voisin : il arrive avec cet état au lieu d'un
    /// personnage plein. Moteur 0.31.0, journal au format 25, scénarios en schéma 18 (<c>carried</c> sur un combattant qui arrive en route).
    /// <list type="bullet">
    /// <item>Points de vie (personnage, ou assaillant mortel) ; endurance et délai restant avant la reprise de sa régénération ; Résonance et
    /// Charges de Rempart (personnage).</item>
    /// <item>Recharges restantes (personnage) : recharge globale, recharge de chaque compétence lançable, recharge partagée des objets de
    /// soin. Les charges restantes des objets de soin passent par les objets eux-mêmes (<see cref="ConsumableSetup.Charges"/>).</item>
    /// <item>L'orientation passe par la place d'arrivée (<see cref="Space.CombatantPlacement.Facing"/>).</item>
    /// </list>
    /// Les altérations ne passent pas (limite connue du V2.26) : le passage ne se fait que hors de toute rencontre, et une altération est liée à
    /// sa source, restée dans l'autre secteur. Toutes les durées sont des multiples du tick, comptées à partir du tick d'arrivée. Immuable ;
    /// validé avec le combat (<see cref="CombatSetup.Validate()"/>).
    /// </summary>
    public sealed class CarriedState
    {
        /// <summary>Points de vie à l'arrivée, dans [1, maximum] ; null : pleins (comme une arrivée ordinaire).</summary>
        public long? Health { get; }

        /// <summary>Endurance à l'arrivée (personnage), dans [0, maximum] ; null : pleine.</summary>
        public long? Endurance { get; }

        /// <summary>Délai restant avant la reprise de la régénération d'endurance, en ms, dans [0, délai des règles] ; 0 : dès l'arrivée.</summary>
        public long EnduranceRegenMs { get; }

        public int Resonance { get; }
        public int RampartCharges { get; }

        /// <summary>Recharge globale restante, en ms (0 : prête).</summary>
        public long GlobalCooldownMs { get; }

        /// <summary>Recharge restante de chaque compétence, en ms (strictement positive), dans l'ordre ordinal des compétences.</summary>
        public IReadOnlyDictionary<string, long> Cooldowns { get; }

        /// <summary>Recharge partagée restante des objets de soin, en ms (0 : prête).</summary>
        public long ConsumableCooldownMs { get; }

        public CarriedState(long? health = null, long? endurance = null, long enduranceRegenMs = 0, int resonance = 0, int rampartCharges = 0,
            long globalCooldownMs = 0, IEnumerable<KeyValuePair<string, long>>? cooldowns = null, long consumableCooldownMs = 0)
        {
            Health = health;
            Endurance = endurance;
            EnduranceRegenMs = enduranceRegenMs;
            Resonance = resonance;
            RampartCharges = rampartCharges;
            GlobalCooldownMs = globalCooldownMs;
            var sorted = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, long> c in cooldowns ?? Array.Empty<KeyValuePair<string, long>>())
            {
                if (c.Key is null) throw new ArgumentNullException(nameof(cooldowns));
                if (sorted.ContainsKey(c.Key)) throw new ArgumentException($"recharge de « {c.Key} » donnée deux fois.", nameof(cooldowns));
                sorted[c.Key] = c.Value;
            }
            Cooldowns = sorted;
            ConsumableCooldownMs = consumableCooldownMs;
        }

        /// <summary>Rien d'emporté : l'arrivée est celle d'un combattant plein.</summary>
        public bool IsEmpty => Health is null && Endurance is null && EnduranceRegenMs == 0 && Resonance == 0 && RampartCharges == 0
            && GlobalCooldownMs == 0 && Cooldowns.Count == 0 && ConsumableCooldownMs == 0;

        /// <summary>Recharges de compétence au plus (borne de validation, au-delà des compétences d'une classe).</summary>
        public const int MaxCooldowns = 32;

        public override string ToString() =>
            $"PV {Health?.ToString() ?? "pleins"}, endurance {Endurance?.ToString() ?? "pleine"}, recharges {string.Join(", ", Cooldowns.Select(c => c.Key + "=" + c.Value))}";
    }
}
