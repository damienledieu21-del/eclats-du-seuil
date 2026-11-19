using System;
using System.Collections.Generic;
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
    }
}
