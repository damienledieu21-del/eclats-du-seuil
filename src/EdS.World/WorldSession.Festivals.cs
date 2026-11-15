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
        /// <summary>
        /// Saison du monde (« spring », « summer », « autumn », « winter ») : la saison réelle si l'hôte a fourni une date (saisons, 15 novembre
        /// 2026), sinon celle du jour du monde.
        /// </summary>
        public string Season => WorldCalendar.Seasons[SeasonIndex];

        /// <summary>Jour de la semaine du monde (0 à 6).</summary>
        public int Weekday => WorldCalendar.WeekdayOf(Zone.Time.Day);

        /// <summary>La fête qui bat son plein maintenant, ou null (sans histoire : jamais).</summary>
        public Festival? Festival => Narrative?.Book.FestivalAt(Zone.Time.Day, Zone.Time.Minute, CivilDateOfDay(Zone.Time.Day));

        /// <summary>La fête du jour (à n'importe quelle heure), ou null : l'annonce du matin, l'affichage.</summary>
        public Festival? FestivalToday => FestivalOnDay(Zone.Time.Day);

        /// <summary>Fêtes aux vraies dates : la fête du jour du monde <paramref name="day"/> (date réelle de ce jour si l'hôte l'a fournie), ou null.</summary>
        public Festival? FestivalOnDay(long day) => Narrative?.Book.FestivalOn(day, CivilDateOfDay(day));

        /// <summary>Fêtes aux vraies dates : tour de la rotation des concours de la fête <paramref name="festival"/> aujourd'hui.</summary>
        public int FestivalRound(string festival) =>
            (Narrative ?? throw new InvalidOperationException("Partie sans histoire.")).Book.Festival(festival).RoundOn(Zone.Time.Day, CivilDateOfDay(Zone.Time.Day));

        int INarrativeWorld.FestivalRound(string festival) => FestivalRound(festival);

        string? INarrativeWorld.Festival => Festival?.Id;

        /// <summary>
        /// Prochaine fête à partir d'aujourd'hui (aujourd'hui compris si elle n'est pas finie), et son jour du monde ; null sans fête. Cherchée
        /// sur une année au plus : une année du jeu, ou une année réelle (en jours du monde) quand la date est fournie.
        /// </summary>
        public (Festival Festival, long Day)? NextFestival()
        {
            if (!(Narrative is Narrative n) || n.Book.Festivals.Count == 0) return null;
            long horizon = CivilUtcSeconds is null ? WorldCalendar.DaysPerYear : checked(366L * 86_400_000 / Zone.Clock.DayLengthMs + 1);
            for (long day = Zone.Time.Day; day <= Zone.Time.Day + horizon; day++)
            {
                if (!(FestivalOnDay(day) is Festival f)) continue;
                if (day > Zone.Time.Day || Zone.Time.Minute < f.To) return (f, day);
            }
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
