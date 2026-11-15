using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace EdS.Data.Saves
{
    /// <summary>Pêche et carnet (sauvegarde v20) : une ligne du carnet des prises : l'espèce (son objet), le nombre de prises et la plus grande (cm).</summary>
    public sealed class SavedCatch
    {
        public string Fish { get; }
        public long Count { get; }

        /// <summary>Plus grande prise, en centimètres.</summary>
        public int BestCm { get; }

        public SavedCatch(string fish, long count, int bestCm)
        {
            if (!SavedFishing.IsFish(fish)) throw new ArgumentException($"Poisson « {fish} » hors du format des sauvegardes.", nameof(fish));
            if (count < 1 || count > SavedFishing.MaxCount) throw new ArgumentOutOfRangeException(nameof(count), count, $"Prises hors de [1, {SavedFishing.MaxCount}].");
            if (bestCm < 1 || bestCm > SavedFishing.MaxCm) throw new ArgumentOutOfRangeException(nameof(bestCm), bestCm, $"Taille hors de [1, {SavedFishing.MaxCm}] cm.");
            Fish = fish;
            Count = count;
            BestCm = bestCm;
        }

        public override bool Equals(object? obj) => obj is SavedCatch o && o.Fish == Fish && o.Count == Count && o.BestCm == BestCm;
        public override int GetHashCode() => (Fish.GetHashCode() * 31 + Count.GetHashCode()) * 31 + BestCm;
    }

    /// <summary>
    /// Pêche et carnet (sauvegarde v20, <c>character.fishing</c>) : lancers faits (compteur des tirages déterministes de la pêche : chaque
    /// lancer, réussi ou manqué, le fait avancer) et carnet des prises, une ligne par espèce, rangée par objet.
    /// </summary>
    public sealed class SavedFishing
    {
        public const long MaxCasts = 1_000_000_000, MaxCount = 1_000_000;
        public const int MaxCm = 1000, MaxLog = 64, MaxFishLength = 128;

        private static readonly Regex FishId = new Regex(@"^item(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);

        public static bool IsFish(string? id) => id != null && id.Length <= MaxFishLength && FishId.IsMatch(id);

        public long Casts { get; }
        public IReadOnlyList<SavedCatch> Log { get; }

        public static SavedFishing Empty { get; } = new SavedFishing(0, Array.Empty<SavedCatch>());

        public SavedFishing(long casts, IEnumerable<SavedCatch> log)
        {
            if (casts < 0 || casts > MaxCasts) throw new ArgumentOutOfRangeException(nameof(casts), casts, $"Lancers hors de [0, {MaxCasts}].");
            var list = (log ?? throw new ArgumentNullException(nameof(log))).ToList();
            if (list.Any(c => c is null)) throw new ArgumentNullException(nameof(log));
            if (list.Count > MaxLog) throw new ArgumentException($"{list.Count} lignes du carnet, au plus {MaxLog}.", nameof(log));
            if (list.Select(c => c.Fish).Distinct(StringComparer.Ordinal).Count() != list.Count) throw new ArgumentException("Poisson en double au carnet.", nameof(log));
            Casts = casts;
            Log = new ReadOnlyCollection<SavedCatch>(list.OrderBy(c => c.Fish, StringComparer.Ordinal).ToList());
        }

        public SavedCatch? Of(string fish) => Log.FirstOrDefault(c => c.Fish == fish);

        /// <summary>Un lancer de plus, sans prise.</summary>
        public SavedFishing WithCast() => new SavedFishing(checked(Casts + 1), Log);

        /// <summary>
        /// Une prise de <paramref name="fish"/> de <paramref name="cm"/> centimètres : le compte de l'espèce et sa plus grande prise. Carnet plein
        /// (une espèce nouvelle au-delà de <see cref="MaxLog"/>) : refusé.
        /// </summary>
        public SavedFishing WithCatch(string fish, int cm)
        {
            SavedCatch? old = Of(fish);
            var log = Log.Where(c => c.Fish != fish).ToList();
            log.Add(old is null ? new SavedCatch(fish, 1, cm) : new SavedCatch(fish, checked(old.Count + 1), Math.Max(old.BestCm, cm)));
            return new SavedFishing(Casts, log);
        }

        public override bool Equals(object? obj) => obj is SavedFishing o && o.Casts == Casts && o.Log.SequenceEqual(Log);
        public override int GetHashCode() => unchecked((int)Casts * 31 + Log.Count);
    }

    public sealed class SavedFishingData
    {
        public long? Casts { get; set; }
        public List<SavedCatchData?>? Log { get; set; }
    }

    public sealed class SavedCatchData
    {
        public string? Fish { get; set; }
        public long? Count { get; set; }
        public int? Best { get; set; }
    }
}
