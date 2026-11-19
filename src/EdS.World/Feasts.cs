using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Feasts;
using EdS.Data.Saves;

namespace EdS.World
{
    /// <summary>Les 12 fêtes d'Orsédia : motif d'un refus (rien n'a changé).</summary>
    public enum FeastRefusal
    {
        None = 0,

        /// <summary>Pas de contenu de fête pour cette fête (ou pas de catalogue des fêtes).</summary>
        NoFeast = 1,

        UnknownActivity = 2,

        /// <summary>Activité déjà faite : un concours une fois par édition, une activité d'invité une fois par jour de fête.</summary>
        AlreadyDone = 3,

        UnknownRef = 4,

        /// <summary>La monnaie de fête ne s'achète pas (ni convertible ni échangeable).</summary>
        NotForSale = 5,

        /// <summary>Décoration ou costume déjà acheté.</summary>
        AlreadyOwned = 6,

        DishesFull = 7,
        NotEnoughCoins = 8,

        /// <summary>Rien à rattraper : la récompense de l'édition d'avant est déjà prise (ou il n'y en a pas).</summary>
        NothingToCatchUp = 9,

        /// <summary>La fête n'a pas lieu maintenant.</summary>
        NotNow = 10,

        /// <summary>Trop loin de l'invité ou du marchand.</summary>
        TooFar = 11,

        /// <summary>Le marchand de fête n'est pas là.</summary>
        MerchantAway = 12,

        /// <summary>Titre de fête pas encore gagné.</summary>
        TitleNotWon = 13,

        /// <summary>Compteur au plafond technique de la sauvegarde.</summary>
        CountersFull = 14,

        /// <summary>Sous-lot F6 : aucun plat de cette fête à manger.</summary>
        NoDish = 15,
    }

    /// <summary>Les 12 fêtes d'Orsédia : ce qu'une activité a rapporté.</summary>
    public readonly struct FeastGain
    {
        /// <summary>Pièces de la fête réellement gagnées (moins que le gain de l'activité au plafond du portefeuille, jamais au-delà).</summary>
        public long Gained { get; }

        /// <summary>Le plafond du portefeuille a retenu une partie du gain.</summary>
        public bool Capped { get; }

        /// <summary>La fête est gagnée avec cette activité : récompense de l'édition (et titre, la première fois).</summary>
        public bool Won { get; }

        /// <summary>Première édition gagnée : le titre de la fête est gagné.</summary>
        public bool Title { get; }

        public FeastGain(long gained, bool capped, bool won, bool title)
        {
            Gained = gained;
            Capped = capped;
            Won = won;
            Title = title;
        }
    }

    /// <summary>
    /// Les 12 fêtes d'Orsédia (sous-lot F2 ; décisions de Damien du 17 novembre 2026) : règles pures du portefeuille de fête, sur l'état gardé
    /// (<see cref="SavedFeasts"/>) et le catalogue (<see cref="FeastBook"/>). Chaque opération est vérifiée et atomique : un refus rend l'état
    /// d'origine, inchangé ; rien n'est tronqué en silence (le gain retenu par le plafond est rendu dans <see cref="FeastGain"/>).
    /// <list type="bullet">
    /// <item>Monnaie propre à chaque fête, gagnée aux activités, plafonnée (<see cref="FeastBook.WalletCap"/>), jamais convertie.</item>
    /// <item>Une récompense annuelle par fête et par personnage : gagnée en faisant <see cref="Feast.RewardAfter"/> activités différentes dans
    /// l'édition ; ratée, elle se rattrape à l'édition suivante, achetée à prix plein chez le marchand de fête.</item>
    /// <item>Le titre de la fête vient avec la première édition gagnée (pas avec un rattrapage).</item>
    /// </list>
    /// L'édition et le jour viennent de la session : année réelle du premier jour de la fête et jour réel quand la date est fournie ; année du
    /// jeu et jour du monde sinon.
    /// </summary>
    public static class FeastRules
    {
        /// <summary>
        /// Le personnage fait l'activité <paramref name="activity"/> de la fête <paramref name="festival"/>, édition <paramref name="edition"/>,
        /// jour <paramref name="day"/>.
        /// </summary>
        public static FeastRefusal Earn(SavedFeasts state, FeastBook book, string festival, string activity, long edition, long day, out SavedFeasts next, out FeastGain gain)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (book is null) throw new ArgumentNullException(nameof(book));
            next = state;
            gain = default;
            if (!(book.Of(festival) is Feast f)) return FeastRefusal.NoFeast;
            if (!(f.Activity(activity) is FeastActivity a)) return FeastRefusal.UnknownActivity;
            if (edition < 0 || edition > SavedFeast.MaxEdition || day < 0 || day > SavedFeast.MaxDayKey) return FeastRefusal.NotNow;
            SavedFeast e = state.Of(f.Festival);
            // Relecture indépendante de la PR #215 : jamais d'édition plus ancienne que celle déjà connue (date réelle disparue : années du
            // jeu après des années réelles) ; sinon la progression serait écrasée et une récompense pourrait être regagnée.
            if (edition < KnownEdition(e)) return FeastRefusal.NotNow;
            IReadOnlyDictionary<string, long> done = e.DoneIn(edition);
            if (a.Contest != null ? done.ContainsKey(a.Id) : done.TryGetValue(a.Id, out long last) && last == day) return FeastRefusal.AlreadyDone;
            var now = new Dictionary<string, long>(done.ToDictionary(x => x.Key, x => x.Value), StringComparer.Ordinal) { [a.Id] = day };
            long room = Math.Max(0, book.WalletCap - e.Coins);
            long gained = Math.Min(room, a.Coins);
            bool won = now.Count >= f.RewardAfter && f.RewardAfter > 0 && !e.HasReward(edition);
            if (won && e.Wins >= SavedFeast.MaxCount) return FeastRefusal.CountersFull;
            SavedFeast updated = e.With(coins: checked(e.Coins + gained), edition: edition, done: now, resetDone: true,
                wins: won ? checked(e.Wins + 1) : e.Wins, editions: won ? e.EditionsWith(edition) : e.Editions);
            next = state.With(updated);
            gain = new FeastGain(gained, gained < a.Coins, won, won && e.Wins == 0);
            return FeastRefusal.None;
        }

        /// <summary>
        /// Le personnage achète la référence <paramref name="code"/> chez le marchand de fête, pendant l'édition <paramref name="edition"/> de sa
        /// fête : une décoration ou le costume (une fois chacun), un plat (jusqu'à <see cref="FeastBook.MaxDishes"/>), ou la récompense de
        /// l'édition d'avant si elle a été ratée (rattrapage, prix plein). La monnaie ne s'achète pas.
        /// </summary>
        public static FeastRefusal Buy(SavedFeasts state, FeastBook book, string code, long edition, out SavedFeasts next)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (book is null) throw new ArgumentNullException(nameof(book));
            next = state;
            if (!(book.Ref(code) is FeastRef r)) return FeastRefusal.UnknownRef;
            if (r.Category == FeastCategory.Currency || r.Price is null) return FeastRefusal.NotForSale;
            if (edition < 0 || edition > SavedFeast.MaxEdition) return FeastRefusal.NotNow;
            Feast f = book.Feasts.Single(x => x.Number == r.Feast);
            SavedFeast e = state.Of(f.Festival);
            int price = r.Price.Value;
            SavedFeast updated;
            switch (r.Category)
            {
                case FeastCategory.Decoration:
                case FeastCategory.Costume:
                    if (e.Owned.Contains(r.Code)) return FeastRefusal.AlreadyOwned;
                    if (e.Coins < price) return FeastRefusal.NotEnoughCoins;
                    updated = e.With(coins: e.Coins - price, owned: e.Owned.Append(r.Code).OrderBy(c => c, StringComparer.Ordinal).ToList());
                    break;
                case FeastCategory.Dish:
                    if (e.Dishes >= book.MaxDishes) return FeastRefusal.DishesFull;
                    if (e.Coins < price) return FeastRefusal.NotEnoughCoins;
                    updated = e.With(coins: e.Coins - price, dishes: e.Dishes + 1);
                    break;
                default:   // récompense : rattrapage de l'édition d'avant
                    // Relecture indépendante de la PR #215 : seulement depuis une édition au moins aussi récente que la dernière connue.
                    if (edition < KnownEdition(e)) return FeastRefusal.NotNow;
                    long missed = edition - 1;
                    if (missed < 0 || e.HasReward(missed)) return FeastRefusal.NothingToCatchUp;
                    if (e.Bought >= SavedFeast.MaxCount) return FeastRefusal.CountersFull;
                    if (e.Coins < price) return FeastRefusal.NotEnoughCoins;
                    updated = e.With(coins: e.Coins - price, bought: checked(e.Bought + 1), editions: e.EditionsWith(missed));
                    break;
            }
            next = state.With(updated);
            return FeastRefusal.None;
        }

        /// <summary>
        /// Sous-lot F6 : le personnage mange un plat de la fête <paramref name="festival"/> (un de moins au compteur des plats gardés). Aucun
        /// effet de jeu : le réconfort n'est qu'affiché (aucune statistique de combat). Format de sauvegarde inchangé (le compteur
        /// <c>dishes</c> existe depuis la v27). Refus : rien ne change.
        /// </summary>
        public static FeastRefusal Eat(SavedFeasts state, FeastBook book, string festival, out SavedFeasts next)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (book is null) throw new ArgumentNullException(nameof(book));
            next = state;
            if (!(book.Of(festival) is Feast)) return FeastRefusal.NoFeast;
            SavedFeast e = state.Of(festival);
            if (e.Dishes <= 0) return FeastRefusal.NoDish;
            next = state.With(e.With(dishes: e.Dishes - 1));
            return FeastRefusal.None;
        }

        /// <summary>
        /// Dernière édition connue de la fête pour ce personnage (édition des activités en cours, dernière récompense prise), ou -1. Une
        /// édition plus ancienne est refusée (<see cref="FeastRefusal.NotNow"/>).
        /// </summary>
        public static long KnownEdition(SavedFeast e) =>
            Math.Max(e.Edition ?? -1, e.Editions.Count > 0 ? e.Editions[e.Editions.Count - 1] : -1);

        /// <summary>La récompense de l'édition d'avant (<paramref name="edition"/> − 1) peut être rattrapée (sans regarder la bourse).</summary>
        public static bool CanCatchUp(SavedFeasts state, string festival, long edition) =>
            edition >= 1 && edition >= KnownEdition(state.Of(festival)) && !state.Of(festival).HasReward(edition - 1);

        /// <summary>Titres de fête gagnés, dans l'ordre de l'année.</summary>
        public static IReadOnlyList<string> Titles(SavedFeasts state, FeastBook book) =>
            book.Feasts.Where(f => state.Of(f.Festival).HasTitle).Select(f => f.TitleId).ToList().AsReadOnly();

        /// <summary>
        /// L'état gardé <paramref name="state"/> va avec le catalogue : fêtes connues, achats de leur fête, activités connues. Sinon le motif.
        /// </summary>
        public static string? Mismatch(SavedFeasts state, FeastBook book)
        {
            foreach (SavedFeast e in state.Entries.Values)
            {
                if (!(book.Of(e.Festival) is Feast f)) return $"fête « {e.Festival} » sans contenu de fête";
                string prefix = $"F{f.Number:00}_";
                if (e.Owned.FirstOrDefault(c => !c.StartsWith(prefix, StringComparison.Ordinal) || book.Ref(c) is null) is string code) return $"achat « {code} » hors de « {e.Festival} »";
                if (e.Done.Keys.FirstOrDefault(k => f.Activity(k) is null) is string act) return $"activité « {act} » inconnue de « {e.Festival} »";
            }
            return null;
        }
    }
}
