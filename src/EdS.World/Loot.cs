using System;
using System.Collections.Generic;
using System.Text;
using EdS.Data.Items;
using EdS.Data.Saves;
using EdS.Simulation.Randomness;

namespace EdS.World
{
    /// <summary>Objet tombé d'une table de butin.</summary>
    public sealed class LootRoll
    {
        public string Item { get; }
        public int Count { get; }

        /// <summary>
        /// Relecture de la PR #78 : rang de l'objet dans sa table de butin (ordre du fichier, à partir de 0), qu'il soit tombé ou non avec
        /// d'autres : le flux du tirage de ses propriétés (<see cref="ItemRolls.Roll"/>) ne dépend que de lui.
        /// </summary>
        public int Rank { get; }

        internal LootRoll(string item, int count, int rank)
        {
            Item = item;
            Count = count;
            Rank = rank;
        }
    }

    /// <summary>
    /// Sous-lot 12.3 : tirage d'une table de butin. Le tirage est déterminé par la clé unique de l'événement qui le donne (mort d'une créature :
    /// <c>zone.vallons/2/death/loup1</c>) : le même événement donne toujours le même butin, et il n'est attribué qu'une fois (registre des
    /// événements consommés). Générateur <see cref="Pcg32"/> initialisé par l'empreinte FNV-1a 64 bits de la clé ; un tirage par objet de la
    /// table, dans l'ordre du fichier, consommé même à 100 %.
    /// </summary>
    public static class Loot
    {
        public static IReadOnlyList<LootRoll> Roll(LootTable table, string eventKey)
        {
            if (table is null) throw new ArgumentNullException(nameof(table));
            if (!SaveValidator.IsEventKey(eventKey)) throw new ArgumentException($"Clé d'événement « {eventKey} » hors du format.", nameof(eventKey));
            var random = new Pcg32(Fnv1a(eventKey), StreamOf(table.Id));
            var rolls = new List<LootRoll>();
            for (int rank = 0; rank < table.Drops.Count; rank++)
            {
                LootDrop drop = table.Drops[rank];
                if (random.RollBasisPoints(drop.ChancePercent * 100)) rolls.Add(new LootRoll(drop.Item, drop.Count, rank));
            }
            return rolls;
        }

        internal static ulong Fnv1a(string text)
        {
            ulong hash = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                hash ^= b;
                hash = unchecked(hash * 1099511628211UL);
            }
            return hash;
        }

        private static ulong StreamOf(string tableId) => Fnv1a("loot:" + tableId);
    }
}
