using System;
using System.Collections.Generic;
using System.Text;
using EdS.Simulation.Engine;

namespace EdS.Net
{
    /// <summary>
    /// Sous-lot 10.2 : journal qui calcule au fil de l'eau l'empreinte FNV-1a sur 64 bits de ses lignes (octets UTF-8 de chaque ligne,
    /// puis « \n »), sans les garder. Serveur et clients simulent le même combat : leurs empreintes sont égales tick après tick, et la
    /// moindre divergence se voit au tick où elle naît. Garde la première ligne (en-tête), envoyée aux clients au début du combat.
    /// </summary>
    public sealed class HashingCombatLog : ICombatLog
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        private ulong _hash = OffsetBasis;

        /// <summary>Première ligne écrite (en-tête du journal) ; null avant.</summary>
        public string? Header { get; private set; }

        public long LineCount { get; private set; }

        /// <summary>Empreinte des lignes écrites jusqu'ici, au format du protocole (<c>fnv1a64:</c> et 16 chiffres hexadécimaux).</summary>
        public string Hash => Format(_hash);

        public void Append(string line)
        {
            if (line is null) throw new ArgumentNullException(nameof(line));
            Header ??= line;
            unchecked
            {
                foreach (byte b in Encoding.UTF8.GetBytes(line))
                {
                    _hash ^= b;
                    _hash *= Prime;
                }
                _hash ^= (byte)'\n';
                _hash *= Prime;
            }
            LineCount++;
        }

        /// <summary>Empreinte d'un journal complet (tests, outils).</summary>
        public static string Of(IEnumerable<string> lines)
        {
            var log = new HashingCombatLog();
            foreach (string line in lines) log.Append(line);
            return log.Hash;
        }

        private static string Format(ulong hash) => MessageCodec.HashPrefix + hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
    }
}
