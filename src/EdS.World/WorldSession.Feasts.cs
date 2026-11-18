using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Feasts;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Domain.Time;

namespace EdS.World
{
    /// <summary>
    /// Les 12 fêtes d'Orsédia (sous-lot F2, sauvegarde v27 ; décisions de Damien du 17 novembre 2026) : le catalogue des fêtes en jeu, l'état
    /// gardé du personnage (<see cref="SavedCharacter.Feasts"/>), l'édition et le jour de fête du moment, les titres de fête. Les règles sont
    /// dans <see cref="FeastRules"/> ; activités et marchand de fête au sous-lot F3.
    /// </summary>
    public sealed partial class WorldSession
    {
        private FeastBook? _feasts;

        /// <summary>Le catalogue des fêtes en jeu, ou null (aucun : rien de fête ne s'ouvre).</summary>
        public FeastBook? FeastBook => _feasts;

        /// <summary>Ce que le personnage garde des fêtes (portefeuilles, récompenses, achats, titre affiché).</summary>
        public SavedFeasts FeastState => _character.Feasts;

        /// <summary>
        /// Met le catalogue des fêtes en jeu (null : aucun). L'état gardé doit aller avec lui (fêtes connues, achats et activités de leur fête),
        /// sinon <see cref="ArgumentException"/> et rien ne change.
        /// </summary>
        public void UseFeasts(FeastBook? book)
        {
            if (book != null && FeastRules.Mismatch(_character.Feasts, book) is string why)
                throw new ArgumentException($"Fêtes sauvegardées hors du catalogue des fêtes : {why}.", nameof(book));
            _feasts = book;
        }

        /// <summary>
        /// Édition et jour de fête de la fête <paramref name="festival"/> aujourd'hui, ou null si elle n'a pas lieu aujourd'hui. Fête datée et
        /// date fournie : l'année réelle de son premier jour et le jour réel (fenêtre de 7 à 14 jours) ; sinon le calendrier du jeu : l'année du
        /// jeu et le jour du monde (comportement d'avant les vraies dates).
        /// </summary>
        public (long Edition, long Day)? FeastDay(string festival)
        {
            if (!(Narrative is Narrative n) || !n.Book.HasFestival(festival)) return null;
            Festival f = n.Book.Festival(festival);
            long today = Zone.Time.Day;
            if (f.CalendarOnly || f.Weekday != null) return null;
            if (f.HasDate && CivilDateOfDay(today) is CivilDate civil)
                return f.OccurrenceOf(civil) is CivilDate start ? (start.Year, civil.DayNumber) : ((long, long)?)null;
            return f.OnDay(today) ? (today / WorldCalendar.DaysPerYear, today) : ((long, long)?)null;
        }

        /// <summary>Titres de fête gagnés (« title.fete.… »), dans l'ordre de l'année ; aucun sans catalogue.</summary>
        public IReadOnlyList<string> FeastTitles => _feasts is null ? Array.Empty<string>() : FeastRules.Titles(_character.Feasts, _feasts);

        /// <summary>Titre de fête affiché, ou null.</summary>
        private string? ShownFeastTitle => _feasts != null && _character.Feasts.Shown is string s && _feasts.Of(s) is Feast f ? f.TitleId : null;

        /// <summary>Affiche le titre de la fête <paramref name="title"/> (gagné) : efface les autres titres affichés. Refus : rien ne change.</summary>
        private FeastRefusal ShowFeastTitle(string title, List<StoryEvent> events)
        {
            if (_feasts is null || !(_feasts.ByTitle(title) is Feast f)) return FeastRefusal.NoFeast;
            if (!_character.Feasts.Of(f.Festival).HasTitle) return FeastRefusal.TitleNotWon;
            if (_character.Feasts.Shown == f.Festival) return FeastRefusal.None;
            if (Narrative?.ShownTitle != null) Narrative.ShowTitle(null, new List<StoryEvent>());   // le titre de guilde s'efface sans annonce
            if (_character.Cards is SavedCards cards && cards.Shown) _character = _character.WithCards(cards.WithShown(false));
            _character = _character.WithFeasts(_character.Feasts.WithShown(f.Festival));
            events.Add(new StoryEvent(StoryEventKind.TitleShown, title));
            return FeastRefusal.None;
        }

        /// <summary>Efface le titre de fête affiché (sans annonce), s'il y en a un.</summary>
        private void HideFeastTitle()
        {
            if (_character.Feasts.Shown != null) _character = _character.WithFeasts(_character.Feasts.WithShown(null));
        }

        /// <summary>Remplace l'état des fêtes du personnage (opération des règles déjà acceptée).</summary>
        private void SetFeasts(SavedFeasts next) => _character = _character.WithFeasts(next);

        // --- Sous-lot F3 : moteur de fête (activités, marchand de fête, concours de l'histoire) ---------------------------------------

        /// <summary>Dernier gain de fête (activité ou concours de l'histoire) : fête, activité et gain ; null avant le premier. Pour l'affichage.</summary>
        public (string Festival, string Activity, FeastGain Gain)? LastFeastGain { get; private set; }

        /// <summary>La fête d'Orsédia qui bat son plein maintenant (fête de l'histoire en cours avec du contenu de fête), ou null.</summary>
        public Feast? FeastNow => _feasts != null && Festival is Festival f ? _feasts.Of(f.Id) : null;

        /// <summary>
        /// Le personnage prend part à l'activité <paramref name="activity"/> de la fête en cours, auprès de son invité (à portée de parole) :
        /// une fois par jour de fête, elle rapporte la monnaie de la fête (plafonnée) et compte pour la récompense de l'édition. Les concours de
        /// l'histoire passent par leur dialogue (effet <c>contest</c>), pas par ici. Refus : rien ne change.
        /// </summary>
        public FeastRefusal JoinFeast(string activity, out FeastGain gain)
        {
            gain = default;
            if (_feasts is null) return FeastRefusal.NoFeast;
            if (!(FeastNow is Feast feast)) return FeastRefusal.NotNow;
            if (!(feast.Activity(activity) is FeastActivity a) || a.Host is null) return FeastRefusal.UnknownActivity;
            if (InCombat) return FeastRefusal.NotNow;
            if (!(Narrative is Narrative n) || !n.CanReach(a.Host)) return FeastRefusal.TooFar;
            if (!(FeastDay(feast.Festival) is (long edition, long day))) return FeastRefusal.NotNow;
            FeastRefusal refusal = FeastRules.Earn(_character.Feasts, _feasts, feast.Festival, a.Id, edition, day, out SavedFeasts next, out gain);
            if (refusal != FeastRefusal.None) return refusal;
            SetFeasts(next);
            LastFeastGain = (feast.Festival, a.Id, gain);
            return FeastRefusal.None;
        }

        /// <summary>Une offre du marchand de fête : la référence, son prix et ce que donnerait l'achat maintenant (<see cref="FeastRefusal.None"/> : achetable).</summary>
        public sealed class FeastOffer
        {
            public FeastRef Ref { get; }
            public int Price { get; }
            public FeastRefusal Status { get; }

            internal FeastOffer(FeastRef r, FeastRefusal status)
            {
                Ref = r;
                Price = r.Price!.Value;
                Status = status;
            }
        }

        /// <summary>
        /// Le marchand de fête (<see cref="FeastBook.Merchant"/>) est là et à portée : la fête en cours, sinon le motif. Il vend ce qui est à
        /// la fête en cours seulement.
        /// </summary>
        public FeastRefusal FeastShopOpen(out Feast? feast)
        {
            feast = null;
            if (_feasts is null) return FeastRefusal.NoFeast;
            if (!(FeastNow is Feast f)) return FeastRefusal.NotNow;
            if (!(Narrative is Narrative n) || NpcAway(n.Book.Npc(_feasts.Merchant))) return FeastRefusal.MerchantAway;
            if (!n.CanReach(_feasts.Merchant)) return FeastRefusal.TooFar;
            feast = f;
            return FeastRefusal.None;
        }

        /// <summary>
        /// L'étal de fête sur la place (décision de Damien du 18 novembre 2026 : un marchand dédié, le forain, dont le décor change chaque
        /// mois), pour l'affichage : la fête en cours, la place du forain et les décorations de la fête qui habillent l'étal ; null hors fête
        /// ou si le forain n'est pas là.
        /// </summary>
        public (Feast Feast, string Merchant, EdS.Domain.Space.GroundVector Position, IReadOnlyList<FeastRef> Decor)? FeastStall =>
            _feasts != null && FeastNow is Feast f && Narrative is Narrative n && !NpcAway(n.Book.Npc(_feasts.Merchant))
                ? (f, _feasts.Merchant, NpcPosition(n.Book.Npc(_feasts.Merchant)), f.Stall)
                : ((Feast, string, EdS.Domain.Space.GroundVector, IReadOnlyList<FeastRef>)?)null;

        /// <summary>
        /// L'étal du marchand de fête : décorations, costume, plat et récompense de l'édition d'avant (rattrapage) de la fête en cours, avec
        /// leur prix et ce que donnerait l'achat ; vide si l'étal n'est pas ouvert. Rien ne change.
        /// </summary>
        public IReadOnlyList<FeastOffer> FeastShop()
        {
            if (FeastShopOpen(out Feast? feast) != FeastRefusal.None || !(FeastDay(feast!.Festival) is (long edition, long _))) return Array.Empty<FeastOffer>();
            var offers = new List<FeastOffer>();
            foreach (FeastRef r in feast.Refs)
                if (r.Price != null) offers.Add(new FeastOffer(r, FeastRules.Buy(_character.Feasts, _feasts!, r.Code, edition, out _)));
            return offers;
        }

        /// <summary>Achète la référence <paramref name="code"/> de la fête en cours chez le marchand de fête. Refus : rien ne change.</summary>
        public FeastRefusal BuyFeast(string code)
        {
            FeastRefusal open = FeastShopOpen(out Feast? feast);
            if (open != FeastRefusal.None) return open;
            if (!(_feasts!.Ref(code) is FeastRef r) || r.Feast != feast!.Number) return FeastRefusal.UnknownRef;
            if (InCombat || !(FeastDay(feast.Festival) is (long edition, long _))) return FeastRefusal.NotNow;
            FeastRefusal refusal = FeastRules.Buy(_character.Feasts, _feasts, r.Code, edition, out SavedFeasts next);
            if (refusal == FeastRefusal.None) SetFeasts(next);
            return refusal;
        }

        /// <summary>
        /// Concours de l'histoire (effet <c>contest</c>) : le concours de clé <paramref name="key"/> de la fête <paramref name="festival"/> vient
        /// d'avoir lieu ; s'il est lié à une activité de fête, il rapporte sa monnaie (une fois par édition). Ne lève pas, ne refuse rien du
        /// concours lui-même : la monnaie s'ajoute, ou rien (pas de catalogue, édition inconnue, déjà fait dans l'édition).
        /// </summary>
        private void FeastContest(string festival, string key)
        {
            if (_feasts is null || !(_feasts.Of(festival) is Feast feast)) return;
            if (!(feast.Activities.FirstOrDefault(a => a.Contest == key) is FeastActivity a)) return;
            if (!(FeastDay(festival) is (long edition, long day))) return;
            if (FeastRules.Earn(_character.Feasts, _feasts, festival, a.Id, edition, day, out SavedFeasts next, out FeastGain gain) != FeastRefusal.None) return;
            SetFeasts(next);
            LastFeastGain = (festival, a.Id, gain);
        }
    }
}
