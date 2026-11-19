using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Feasts;

namespace EdS.Data.Saves
{
    /// <summary>
    /// Les 12 fêtes d'Orsédia (sauvegarde v27, sous-lot F2 ; décisions de Damien du 17 novembre 2026) : ce que le personnage garde d'une fête.
    /// <list type="bullet">
    /// <item><see cref="Coins"/> : sa monnaie de la fête, liée au personnage, gardée d'une année sur l'autre, ni échangeable ni convertible.</item>
    /// <item><see cref="Edition"/> et <see cref="Done"/> : l'édition en cours (année réelle de son premier jour quand la date est fournie, année
    /// du jeu sinon) et, pour chaque activité faite dans cette édition, son dernier jour (jour réel, ou jour du monde sans date).</item>
    /// <item><see cref="Wins"/> : éditions gagnées (récompense de l'année obtenue par les activités ; dès la première, le titre de la fête) ;
    /// <see cref="Bought"/> : récompenses rattrapées, achetées à prix plein à la fête suivante ; <see cref="Editions"/> : les deux dernières
    /// éditions dont le personnage a la récompense (de quoi savoir si celle de cette année et celle de l'an dernier sont déjà prises).</item>
    /// <item><see cref="Owned"/> : décorations et costume achetés (une fois chacun) ; <see cref="Dishes"/> : plats gardés.</item>
    /// </list>
    /// Immuable ; toute valeur hors du format est refusée à la construction.
    /// </summary>
    public sealed class SavedFeast
    {
        /// <summary>Borne technique de la monnaie d'une fête (le plafond du jeu est dans les données, au plus celle-ci).</summary>
        public const long MaxCoins = FeastValidator.MaxWalletCap;

        /// <summary>Borne technique des compteurs d'éditions gagnées et de récompenses rattrapées.</summary>
        public const long MaxCount = 1_000_000;

        /// <summary>Éditions gardées au plus (les deux dernières dont la récompense est prise).</summary>
        public const int KeptEditions = 2;

        /// <summary>Décorations (5) et costume (1) d'une fête ; plats et activités au plus.</summary>
        public const int MaxOwned = FeastValidator.Decorations + 1, MaxDishes = FeastValidator.MaxDishesCap, MaxDone = FeastValidator.MaxActivities;

        /// <summary>Plus grand numéro d'édition et plus grand jour (borne des jours de la sauvegarde).</summary>
        public const long MaxEdition = SavedEvent.MaxDay, MaxDayKey = SavedEvent.MaxDay;

        private static readonly Regex FestivalId = new Regex(@"^festival(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);
        private static readonly Regex OwnedCode = new Regex(@"^F(0[1-9]|1[0-2])_(D0[1-5]|C01)\z", RegexOptions.CultureInvariant);

        public static bool IsFestival(string? id) => id != null && id.Length <= 64 && FestivalId.IsMatch(id);

        /// <summary>Code d'une décoration ou d'un costume (« F04_D01 », « F04_C01 »).</summary>
        public static bool IsOwnedCode(string? code) => code != null && OwnedCode.IsMatch(code);

        public string Festival { get; }
        public long Coins { get; }
        public long? Edition { get; }
        public IReadOnlyDictionary<string, long> Done { get; }
        public long Wins { get; }
        public long Bought { get; }
        public IReadOnlyList<long> Editions { get; }
        public IReadOnlyList<string> Owned { get; }
        public int Dishes { get; }

        public SavedFeast(string festival, long coins = 0, long? edition = null, IReadOnlyDictionary<string, long>? done = null, long wins = 0, long bought = 0,
            IEnumerable<long>? editions = null, IEnumerable<string>? owned = null, int dishes = 0)
        {
            if (!IsFestival(festival)) throw new ArgumentException($"Fête « {festival} » hors du format des sauvegardes.", nameof(festival));
            if (coins < 0 || coins > MaxCoins) throw new ArgumentOutOfRangeException(nameof(coins), coins, $"Monnaie de fête hors de [0, {MaxCoins}].");
            var d = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, long> e in done ?? new Dictionary<string, long>())
            {
                if (!FeastValidator.IsActivityId(e.Key)) throw new ArgumentException($"Activité « {e.Key} » hors du format des sauvegardes.", nameof(done));
                if (e.Value < 0 || e.Value > MaxDayKey) throw new ArgumentOutOfRangeException(nameof(done), e.Value, $"Jour hors de [0, {MaxDayKey}].");
                d[e.Key] = e.Value;
            }
            if (d.Count > MaxDone) throw new ArgumentException($"{d.Count} activités faites, au plus {MaxDone}.", nameof(done));
            if ((edition is null) != (d.Count == 0)) throw new ArgumentException("Édition en cours seulement avec des activités faites (et inversement).", nameof(edition));
            if (edition is long ed && (ed < 0 || ed > MaxEdition)) throw new ArgumentOutOfRangeException(nameof(edition), ed, $"Édition hors de [0, {MaxEdition}].");
            if (wins < 0 || wins > MaxCount) throw new ArgumentOutOfRangeException(nameof(wins), wins, $"Éditions gagnées hors de [0, {MaxCount}].");
            if (bought < 0 || bought > MaxCount) throw new ArgumentOutOfRangeException(nameof(bought), bought, $"Récompenses rattrapées hors de [0, {MaxCount}].");
            var eds = (editions ?? Array.Empty<long>()).ToList();
            if (!AreEditions(eds, checked(wins + bought))) throw new ArgumentException("Éditions gardées : au plus 2, croissantes, sans doublon, pas plus que de récompenses.", nameof(editions));
            var own = (owned ?? Array.Empty<string>()).ToList();
            if (own.Count > MaxOwned || own.Any(c => !IsOwnedCode(c)) || !Sorted(own) || own.Select(c => c.Substring(0, 3)).Distinct().Count() > 1)
                throw new ArgumentException("Décorations et costume : au plus 6 codes d'une même fête, en ordre ordinal, sans doublon.", nameof(owned));
            if (dishes < 0 || dishes > MaxDishes) throw new ArgumentOutOfRangeException(nameof(dishes), dishes, $"Plats hors de [0, {MaxDishes}].");
            Festival = festival;
            Coins = coins;
            Edition = edition;
            Done = new ReadOnlyDictionary<string, long>(d);
            Wins = wins;
            Bought = bought;
            Editions = new ReadOnlyCollection<long>(eds);
            Owned = new ReadOnlyCollection<string>(own);
            Dishes = dishes;
        }

        /// <summary>Éditions gardées valides : au plus <see cref="KeptEditions"/>, strictement croissantes, dans les bornes, pas plus que <paramref name="rewards"/>.</summary>
        public static bool AreEditions(IReadOnlyList<long> editions, long rewards)
        {
            if (editions is null || editions.Count > KeptEditions || editions.Count > rewards) return false;
            for (int i = 0; i < editions.Count; i++)
                if (editions[i] < 0 || editions[i] > MaxEdition || (i > 0 && editions[i - 1] >= editions[i])) return false;
            return true;
        }

        private static bool Sorted(IReadOnlyList<string> list)
        {
            for (int i = 1; i < list.Count; i++)
                if (string.CompareOrdinal(list[i - 1], list[i]) >= 0) return false;
            return true;
        }

        /// <summary>Rien de gardé : l'entrée n'est pas écrite.</summary>
        public bool IsEmpty => Coins == 0 && Done.Count == 0 && Wins == 0 && Bought == 0 && Owned.Count == 0 && Dishes == 0;

        /// <summary>Le titre de la fête est gagné (au moins une édition gagnée).</summary>
        public bool HasTitle => Wins > 0;

        /// <summary>Récompenses de la fête possédées (gagnées et rattrapées).</summary>
        public long Rewards => checked(Wins + Bought);

        /// <summary>Le personnage a la récompense de l'édition <paramref name="edition"/>.</summary>
        public bool HasReward(long edition) => Editions.Contains(edition);

        /// <summary>Activités faites dans l'édition <paramref name="edition"/> (aucune si l'édition gardée est une autre).</summary>
        public IReadOnlyDictionary<string, long> DoneIn(long edition) => Edition == edition ? Done : new ReadOnlyDictionary<string, long>(new Dictionary<string, long>());

        public SavedFeast With(long? coins = null, long? edition = null, IReadOnlyDictionary<string, long>? done = null, bool resetDone = false, long? wins = null,
            long? bought = null, IEnumerable<long>? editions = null, IEnumerable<string>? owned = null, int? dishes = null) =>
            new SavedFeast(Festival, coins ?? Coins, resetDone ? edition : edition ?? Edition, resetDone ? done : done ?? Done, wins ?? Wins, bought ?? Bought,
                editions ?? Editions, owned ?? Owned, dishes ?? Dishes);

        /// <summary>Éditions gardées, l'édition <paramref name="edition"/> en plus (les deux plus récentes seulement).</summary>
        public IEnumerable<long> EditionsWith(long edition) => Editions.Append(edition).Distinct().OrderBy(e => e).Skip(Math.Max(0, Editions.Append(edition).Distinct().Count() - KeptEditions)).ToList();

        public override bool Equals(object? obj) => obj is SavedFeast o && o.Festival == Festival && o.Coins == Coins && o.Edition == Edition
            && o.Done.SequenceEqual(Done) && o.Wins == Wins && o.Bought == Bought && o.Editions.SequenceEqual(Editions) && o.Owned.SequenceEqual(Owned) && o.Dishes == Dishes;

        public override int GetHashCode() => unchecked(Festival.GetHashCode() * 31 + (int)Coins * 7 + (int)Wins + Dishes);
    }

    /// <summary>
    /// Les 12 fêtes d'Orsédia (sauvegarde v27, <c>character.feasts</c>) : un portefeuille par fête (entrées non vides seulement, rangées par
    /// fête) et le titre de fête affiché sous le nom (<see cref="Shown"/> : la fête dont le titre est affiché, ou null ; un seul titre affiché
    /// en tout, avec ceux de la guilde et « As des Trois Feux »).
    /// </summary>
    public sealed class SavedFeasts
    {
        /// <summary>Fêtes gardées au plus (12 fêtes d'Orsédia ; marge pour la suite).</summary>
        public const int MaxEntries = 16;

        public static SavedFeasts Empty { get; } = new SavedFeasts(null, null);

        public IReadOnlyDictionary<string, SavedFeast> Entries { get; }

        public string? Shown { get; }

        public SavedFeasts(IEnumerable<SavedFeast>? entries, string? shown = null)
        {
            var map = new SortedDictionary<string, SavedFeast>(StringComparer.Ordinal);
            foreach (SavedFeast f in entries ?? Array.Empty<SavedFeast>())
            {
                if (f is null) throw new ArgumentNullException(nameof(entries));
                if (map.ContainsKey(f.Festival)) throw new ArgumentException($"Fête « {f.Festival} » en double.", nameof(entries));
                if (!f.IsEmpty) map[f.Festival] = f;
            }
            if (map.Count > MaxEntries) throw new ArgumentException($"{map.Count} fêtes, au plus {MaxEntries}.", nameof(entries));
            if (shown != null && !(map.TryGetValue(shown, out SavedFeast s) && s.HasTitle))
                throw new ArgumentException($"Titre de « {shown} » affiché sans être gagné.", nameof(shown));
            Entries = new ReadOnlyDictionary<string, SavedFeast>(map);
            Shown = shown;
        }

        public bool IsEmpty => Entries.Count == 0 && Shown is null;

        /// <summary>L'entrée de la fête <paramref name="festival"/> (vide si rien n'est gardé).</summary>
        public SavedFeast Of(string festival) => Entries.TryGetValue(festival, out SavedFeast f) ? f : new SavedFeast(festival);

        /// <summary>Mêmes fêtes, l'entrée de <paramref name="feast"/> remplacée.</summary>
        public SavedFeasts With(SavedFeast feast) =>
            new SavedFeasts(Entries.Values.Where(f => f.Festival != (feast ?? throw new ArgumentNullException(nameof(feast))).Festival).Append(feast), Shown);

        public SavedFeasts WithShown(string? shown) => new SavedFeasts(Entries.Values, shown);

        public override bool Equals(object? obj) => obj is SavedFeasts o && o.Shown == Shown && o.Entries.Count == Entries.Count
            && o.Entries.All(e => Entries.TryGetValue(e.Key, out SavedFeast f) && f.Equals(e.Value));

        public override int GetHashCode() => Entries.Count * 31 + (Shown?.GetHashCode() ?? 0);
    }
}
