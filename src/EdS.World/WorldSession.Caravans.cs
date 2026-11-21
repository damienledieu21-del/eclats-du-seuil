using System;
using System.Collections.Generic;
using EdS.Data.Caravans;
using EdS.Data.Saves;
using EdS.Data.Zones;

namespace EdS.World
{
    public sealed partial class WorldSession
    {
        /// <summary>Sous-lot V2.41.1 : caravanes de la zone (null : aucune, ou une autre zone que la leur).</summary>
        private CaravanBook? _caravans;

        /// <summary>
        /// Sous-lot V2.41.1 : les caravanes de <paramref name="book"/>, si elles sont de cette zone (leurs attaques doivent déjà être parmi
        /// ses événements, <see cref="CaravanBook.ApplyTo"/>) ; sinon aucune. Affichage seulement : leurs attaques jouent comme tout événement.
        /// </summary>
        public void UseCaravans(CaravanBook? book)
        {
            if (book != null && book.ZoneId == Definition?.Id)
            {
                foreach (Caravan c in book.Caravans)
                    if (Definition.EventOf(c.Attack.Id) is null)
                        throw new ArgumentException($"Attaque « {c.Attack.Id} » absente des événements de la zone (CaravanBook.ApplyTo).", nameof(book));
                _caravans = book;
            }
            else _caravans = null;
        }

        /// <summary>Sous-lot V2.41.1 : caravanes en jeu dans cette session, ou null.</summary>
        public CaravanBook? CaravanBook => _caravans;

        /// <summary>Sous-lot V2.41.1 : les caravanes sur la route à ce tick (place à la fraction de minute près), dans l'ordre du fichier.</summary>
        public IReadOnlyList<CaravanView> Caravans => _caravans is null ? Array.Empty<CaravanView>()
            : CaravanSchedule.All(Definition!.Id, _storyZone ?? Definition.Id, _caravans, CaravanSchedule.MilliAt(Zone.Clock, Zone.WorldTick), CaravanOutcome);

        /// <summary>Issue réglée au registre de l'attaque <paramref name="attack"/> du jour <paramref name="day"/>, ou null.</summary>
        private EventOutcome? CaravanOutcome(string attack, long day)
        {
            if (!(Definition?.EventOf(attack) is WorldEventDefinition e) || !HasEventEntry(e)) return null;
            SavedEvent entry = EventEntry(e);
            return entry.Last == day ? entry.Outcome : entry.Last > day ? EventOutcome.None : (EventOutcome?)null;
        }
    }
}
