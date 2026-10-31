using System;
using System.Globalization;
using EdS.Data.Zones;
using EdS.Simulation.Randomness;

namespace EdS.World
{
    /// <summary>
    /// Réponses de Damien du 31 octobre 2026 : les créatures rares de la nuit (le loup blanc). Chaque nuit du monde (de 22 h à 5 h) a son numéro :
    /// celui du jour où elle commence. Une créature a une chance d'apparaître cette nuit-là, tirée par un <see cref="Pcg32"/> initialisé par
    /// l'empreinte FNV-1a de la <b>clé de la nuit</b> (<c>zone.vallons/night/3/loup_blanc</c>), comme le stock des marchands : la même nuit
    /// donne toujours le même tirage. Vaincue cette nuit-là, la clé entre au registre du monde : elle ne revient pas avant la nuit suivante.
    /// </summary>
    public static class NightCreatures
    {
        /// <summary>Numéro de la nuit à l'heure <paramref name="time"/> (le jour où elle a commencé, à 22 h), ou null hors de la nuit.</summary>
        public static long? NightOf(WorldTime time)
        {
            if (time.Phase != DayPhase.Night) return null;
            if (time.Minute >= 22 * 60) return time.Day;
            // Avant 5 h : la nuit a commencé la veille (le jour 0 commence à 8 h : sa nuit d'avant n'existe pas).
            return time.Day > 0 ? time.Day - 1 : (long?)null;
        }

        /// <summary>Clé de la nuit <paramref name="night"/> pour la créature <paramref name="creature"/> de la zone <paramref name="zone"/>.</summary>
        public static string Key(string zone, long night, string creature)
        {
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            if (creature is null) throw new ArgumentNullException(nameof(creature));
            if (night < 0) throw new ArgumentOutOfRangeException(nameof(night));
            return zone + "/night/" + night.ToString(CultureInfo.InvariantCulture) + "/" + creature;
        }

        /// <summary>La créature a sa chance cette nuit-là (tirage déterministe sur la clé de la nuit, en pour cent).</summary>
        public static bool Appears(string zone, long night, Population population)
        {
            if (population is null) throw new ArgumentNullException(nameof(population));
            if (!(population.Night is NightSpawn spawn)) return false;
            var random = new Pcg32(Loot.Fnv1a(Key(zone, night, population.Creature)), Loot.Fnv1a("night:" + population.Creature));
            return random.NextBounded(100) < (uint)spawn.ChancePercent;
        }
    }
}
