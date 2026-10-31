using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EdS.Data.Guild;
using EdS.Data.Saves;
using EdS.Simulation.Randomness;

namespace EdS.World
{
    /// <summary>Sous-lot V1.7 : motif d'un refus de la Guilde des aventuriers (rien n'a changé).</summary>
    public enum GuildRefusal
    {
        None = 0,

        /// <summary>Partie sans guilde (données absentes) ou sans histoire.</summary>
        NoGuild = 1,

        /// <summary>Trop loin de la maîtresse de guilde (ou personnage absent, tombé, dans une autre zone).</summary>
        TooFar = 2,

        /// <summary>Le personnage n'est pas inscrit à la guilde.</summary>
        NotMember = 3,

        /// <summary>Le personnage est déjà inscrit.</summary>
        AlreadyMember = 4,

        /// <summary>Contrat inconnu, ou absent du tableau du jour.</summary>
        NotOnBoard = 5,

        /// <summary>Contrat déjà pris aujourd'hui (en cours, réussi ou abandonné) : il revient demain.</summary>
        AlreadyTaken = 6,

        /// <summary>Trop de contrats en cours à la fois.</summary>
        TooManyContracts = 7,

        /// <summary>Contrat qui n'est pas en cours.</summary>
        NotActive = 8,

        /// <summary>Pas d'épreuve au-dessus de ce rang dans le jeu actuel (le rang suivant viendra avec une autre région).</summary>
        TopRank = 9,

        /// <summary>Pas assez de points de guilde pour l'épreuve du rang suivant.</summary>
        NotEnoughPoints = 10,

        /// <summary>L'épreuve est déjà en cours.</summary>
        TrialActive = 11,

        /// <summary>Titre que le personnage n'a pas gagné.</summary>
        UnknownTitle = 12,

        /// <summary>Rang trop bas (porte gardée, boutique de la guilde).</summary>
        RankTooLow = 13,

        /// <summary>L'épreuve ne peut pas commencer (sa quête refusée par l'histoire : déjà finie, fermée).</summary>
        Locked = 14,
    }

    /// <summary>
    /// Sous-lot V1.7 : le tableau des contrats. Chaque jour du monde, pour chaque rang ouvert jusqu'à celui du personnage,
    /// <see cref="GuildBook.Daily"/> contrats de ce rang sont tirés par un <see cref="Pcg32"/> initialisé par l'empreinte FNV-1a de la clé du
    /// jour (<see cref="DayKey"/>, comme le stock des marchands du sous-lot V1.1 ; jour tiré des ticks, jamais de l'horloge réelle) : le même
    /// jour et le même rang donnent toujours le même tableau, dans l'ordre du fichier. Un rang plus haut voit donc plus de contrats, et des
    /// contrats plus durs et mieux payés.
    /// </summary>
    public static class GuildBoard
    {
        /// <summary>Clé du tirage des contrats d'un rang un jour du monde (<c>guild/f/day/3</c>).</summary>
        public static string DayKey(string rank, long day) =>
            "guild/" + (rank ?? throw new ArgumentNullException(nameof(rank))) + "/day/" + day.ToString(CultureInfo.InvariantCulture);

        /// <summary>Réponses de Damien du 31 octobre 2026 : clé du tirage des contrats pressés d'un rang un jour du monde (<c>guild/f/rush/day/3</c>).</summary>
        public static string RushKey(string rank, long day) =>
            "guild/" + (rank ?? throw new ArgumentNullException(nameof(rank))) + "/rush/day/" + day.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Contrats du tableau le jour <paramref name="day"/> pour un personnage du rang d'indice <paramref name="rank"/> (0 : G) : ceux de
        /// chaque rang ouvert jusqu'au sien, du plus bas au plus haut, chaque rang dans l'ordre du fichier. Réponses de Damien du 31 octobre
        /// 2026 : pour chaque rang, ses contrats normaux (<see cref="GuildBook.Daily"/>, clé <see cref="DayKey"/>), puis ses contrats pressés
        /// (<see cref="GuildBook.RushDaily"/>, tirés à part sur la clé <see cref="RushKey"/>), mieux payés, à finir avant la fin du jour.
        /// </summary>
        public static IReadOnlyList<GuildContract> Of(GuildBook book, int rank, long day)
        {
            if (book is null) throw new ArgumentNullException(nameof(book));
            if (day < 0) throw new ArgumentOutOfRangeException(nameof(day));
            var board = new List<GuildContract>();
            foreach (GuildRank r in book.Ranks.Where(x => x.Open && x.Index <= rank))
            {
                board.AddRange(Draw(book.Contracts.Where(c => c.Rank == r.Id && !c.Rush).ToList(), book.Daily,
                    new Pcg32(Loot.Fnv1a(DayKey(r.Id, day)), Loot.Fnv1a("guild:" + r.Id))));
                if (book.RushDaily > 0)
                    board.AddRange(Draw(book.Contracts.Where(c => c.Rank == r.Id && c.Rush).ToList(), book.RushDaily,
                        new Pcg32(Loot.Fnv1a(RushKey(r.Id, day)), Loot.Fnv1a("guild:rush:" + r.Id))));
            }
            return board.AsReadOnly();
        }

        /// <summary><paramref name="count"/> contrats tirés de <paramref name="pool"/> (mélange partiel), rendus dans l'ordre du fichier.</summary>
        private static IEnumerable<GuildContract> Draw(List<GuildContract> pool, int count, Pcg32 random)
        {
            int take = Math.Min(count, pool.Count);
            var chosen = new HashSet<GuildContract>();
            var shuffled = pool.ToList();
            for (int i = 0; i < take; i++)
            {
                int j = i + (int)random.NextBounded((uint)(shuffled.Count - i));
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
                chosen.Add(shuffled[i]);
            }
            return pool.Where(chosen.Contains);
        }
    }

    /// <summary>Sous-lot V1.7 : la carte d'aventurier en cours de partie (état mutable de l'histoire ; sauvegardée en <see cref="SavedGuildCard"/>).</summary>
    internal sealed class GuildCard
    {
        public int Rank;
        public long Points;
        public long Contracts;
        public readonly SortedSet<string> Titles = new SortedSet<string>(StringComparer.Ordinal);
        public string? Title;
        public readonly SortedDictionary<string, long> Active = new SortedDictionary<string, long>(StringComparer.Ordinal);
        public long Day;
        public readonly SortedSet<string> Taken = new SortedSet<string>(StringComparer.Ordinal);

        public static GuildCard From(SavedGuildCard saved)
        {
            var card = new GuildCard
            {
                Rank = GuildRanks.IndexOf(saved.Rank),
                Points = saved.Points,
                Contracts = saved.Contracts,
                Title = saved.Title,
                Day = saved.Day,
            };
            card.Titles.UnionWith(saved.Titles);
            foreach (KeyValuePair<string, long> a in saved.Active) card.Active[a.Key] = a.Value;
            card.Taken.UnionWith(saved.Taken);
            return card;
        }

        public SavedGuildCard ToSaved() =>
            new SavedGuildCard(GuildRanks.Ids[Rank], Points, Contracts, Titles, Title, new Dictionary<string, long>(Active, StringComparer.Ordinal), Day, Taken);

        /// <summary>Ajoute des points, arrêtés au plafond de la carte ; rend ceux réellement ajoutés.</summary>
        public long Add(long points)
        {
            if (points <= 0) return 0;
            long after = Math.Min(SavedGuildCard.MaxPoints, checked(Points + Math.Min(points, SavedGuildCard.MaxPoints)));
            long added = after - Points;
            Points = after;
            return added;
        }

        /// <summary>
        /// Garde le contrat comme pris le jour <paramref name="day"/> (passé au tableau de ce jour d'abord) ; faux, sans rien changer, si la
        /// liste du jour est pleine (<see cref="SavedGuildCard.MaxTaken"/>).
        /// </summary>
        public bool MarkTaken(string contract, long day)
        {
            if (day == Day && !Taken.Contains(contract) && Taken.Count >= SavedGuildCard.MaxTaken) return false;
            Today(day);
            Taken.Add(contract);
            return true;
        }

        /// <summary>Passe au tableau du jour <paramref name="day"/> : les contrats pris un autre jour se reprennent.</summary>
        public void Today(long day)
        {
            if (day == Day) return;
            Day = day;
            Taken.Clear();
        }
    }
}
