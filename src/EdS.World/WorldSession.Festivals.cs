using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Saves;
using EdS.Data.Story;

namespace EdS.World
{
    /// <summary>
    /// Sous-lot V2.10 (fêtes du bourg) : le calendrier (saison, jour de la semaine) et la fête en cours, recalculés du temps du monde (aucune
    /// horloge réelle, rien de sauvegardé sauf le dernier concours de chaque fête) ; les invités d'une fête se tiennent à leur place de fête,
    /// les PNJ de passage ne sont là que pendant leurs fêtes. Une fête ne bloque jamais rien : les quêtes, livraisons et boutiques de ses
    /// invités restent joignables à leur place de fête.
    /// </summary>
    public sealed partial class WorldSession
    {
        /// <summary>Saison du jour du monde (« spring », « summer », « autumn », « winter »).</summary>
        public string Season => WorldCalendar.SeasonOf(Zone.Time.Day);

        /// <summary>Jour de la semaine du monde (0 à 6).</summary>
        public int Weekday => WorldCalendar.WeekdayOf(Zone.Time.Day);

        /// <summary>La fête qui bat son plein maintenant, ou null (sans histoire : jamais).</summary>
        public Festival? Festival => Narrative?.Book.FestivalAt(Zone.Time.Day, Zone.Time.Minute);

        /// <summary>La fête du jour (à n'importe quelle heure), ou null : l'annonce du matin, l'affichage.</summary>
        public Festival? FestivalToday => Narrative?.Book.FestivalOn(Zone.Time.Day);

        string? INarrativeWorld.Festival => Festival?.Id;

        /// <summary>
        /// Prochaine fête à partir d'aujourd'hui (aujourd'hui compris si elle n'est pas finie), et son jour du monde ; null sans fête. Cherchée
        /// sur une année au plus.
        /// </summary>
        public (Festival Festival, long Day)? NextFestival()
        {
            if (!(Narrative is Narrative n) || n.Book.Festivals.Count == 0) return null;
            for (long day = Zone.Time.Day; day <= Zone.Time.Day + WorldCalendar.DaysPerYear; day++)
                foreach (Festival f in n.Book.Festivals)
                    if (f.OnDay(day) && (day > Zone.Time.Day || Zone.Time.Minute < f.To) && n.Book.FestivalOn(day) == f) return (f, day);
            return null;
        }

        /// <summary>
        /// Sous-lot V2.10 : le PNJ <paramref name="npc"/> est de passage et sa fête n'a pas lieu maintenant : il n'est pas dans le monde (on ne
        /// lui parle pas, sa boutique est fermée, l'affichage le cache).
        /// </summary>
        public bool NpcAway(Npc npc)
        {
            if (npc is null) throw new ArgumentNullException(nameof(npc));
            // Sous-lot V2.14 : les marchands de passage du grand jour de marché sont sur la place de l'installation au démontage.
            return npc.Festivals.Count > 0 && !(Festival is Festival f && npc.Festivals.Contains(f.Id)) && !AtMarket(npc.Festivals, false);
        }

        bool INarrativeWorld.NpcAway(Npc npc) => NpcAway(npc);

        /// <summary>Sous-lot V2.10 : place de fête du PNJ <paramref name="npc"/> si une fête bat son plein et qu'il y est invité, ou null.</summary>
        private FestivalGuest? GuestNow(Npc npc) => Festival?.Guest(npc.Id);

        /// <summary>Sous-lot V2.10 : le personnage a déjà concouru à la fête <paramref name="festival"/> aujourd'hui.</summary>
        public bool Contested(string festival) => Narrative is Narrative n && n.ContestDay(festival) == Zone.Time.Day;
    }
}
