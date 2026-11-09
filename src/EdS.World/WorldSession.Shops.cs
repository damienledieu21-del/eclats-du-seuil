using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Merchants;
using EdS.Data.Story;

namespace EdS.World
{
    /// <summary>Sous-lot V2.14 : réponse d'un marchand du marché à qui l'on demande de baisser son prix.</summary>
    public enum HaggleRefusal
    {
        /// <summary>Accepté : ses prix baissent pour la journée.</summary>
        None = 0,

        /// <summary>Pas de jour de marché en cours (ou partie sans marché).</summary>
        NoMarket = 1,

        /// <summary>Ce marchand ne tient pas d'étal du marché.</summary>
        NotAStall = 2,

        /// <summary>Réputation trop basse auprès de sa faction pour marchander.</summary>
        StandingTooLow = 3,

        /// <summary>Il refuse aujourd'hui (le tirage du jour) ; redemander ne change rien.</summary>
        Refused = 4,

        /// <summary>Déjà accepté aujourd'hui.</summary>
        AlreadyDone = 5,

        /// <summary>Boutique fermée, ou trop loin du marchand.</summary>
        Closed = 6,
    }

    /// <summary>
    /// Sous-lot V2.14 (décision de Damien du 9 novembre 2026) : vraies boutiques et grand jour de marché. Une boutique (<see cref="Merchant.Shop"/>)
    /// a des horaires et un jour de repos ; fermée, le patron ou un commis répond à la porte et vend ce qui y est prévu (jamais de quête
    /// bloquée par un horaire). Les offres fraîches suivent les fournées : rien avant la première, pains chauds le matin, rupture le soir.
    /// Le grand jour de marché (<see cref="MerchantBook.Market"/>) se tient sur la place du bourg les jours de ses fêtes hebdomadaires :
    /// installation, marché, démontage ; ses étals marchandent selon la réputation. Tout se recalcule du jour et de la minute du monde.
    /// </summary>
    public sealed partial class WorldSession
    {
        /// <summary>Jour de la semaine du monde (0 à 6).</summary>
        private int WeekdayNow => WorldCalendar.WeekdayOf(Zone.Time.Day);

        /// <summary>
        /// Sous-lot V2.14 : état de la boutique de <paramref name="merchant"/> à cet instant : ouverte (ou sans horaires), à la porte (fermée,
        /// mais quelqu'un répond), fermée hors de ses heures ou un jour de repos. Les moments de fermeture <see cref="Merchant.Closed"/>
        /// comptent comme des heures de fermeture.
        /// </summary>
        /// <exception cref="ArgumentException">Marchand inconnu.</exception>
        public ShopState ShopStateOf(string merchant)
        {
            Merchant m = NeedMarket().Book.Merchant(merchant);
            if (!(m.Shop is Shop shop)) return ShopState.Open;
            int minute = Zone.Time.Minute;
            bool phaseClosed = m.Closed.Contains(Narrative.PhaseId(Zone.Time.Phase));
            bool open = shop.IsOpen(WeekdayNow, minute) && !phaseClosed;
            if (open) return ShopState.Open;
            if (shop.Door != DoorService.None) return ShopState.AtDoor;
            return shop.Rest.Contains(WeekdayNow) ? ShopState.RestDay : ShopState.ClosedHours;
        }

        /// <summary>
        /// Sous-lot V2.14 : prochaine ouverture de la boutique de <paramref name="merchant"/> (jour du monde et minute), ou null (sans horaires,
        /// ou déjà ouverte).
        /// </summary>
        public (long Day, int Minute)? NextOpening(string merchant)
        {
            Merchant m = NeedMarket().Book.Merchant(merchant);
            if (!(m.Shop is Shop shop) || ShopStateOf(merchant) == ShopState.Open) return null;
            // Les moments de fermeture comptent aussi : on avance d'ouverture en ouverture jusqu'à une minute hors d'eux (une semaine au plus).
            long day = Zone.Time.Day;
            int minute = Zone.Time.Minute;
            for (int i = 0; i < 64; i++)
            {
                if (!(ShopRules.NextOpening(shop, day, minute) is (long d, int mm))) return null;
                for (int k = mm; k < ShopRules.MinutesPerDay; k++)
                {
                    if (!shop.IsOpen(WorldCalendar.WeekdayOf(d), k)) break;
                    if (!m.Closed.Contains(Narrative.PhaseId(WorldTime.PhaseOf(k)))) return (d, k);
                }
                day = d;
                minute = Math.Min(ShopRules.MinutesPerDay - 1, mm);
            }
            return null;
        }

        /// <summary>
        /// Sous-lot V2.14 : la boutique de <paramref name="merchant"/> est fermée mais quelqu'un répond à la porte : seules les offres
        /// <see cref="MerchantOffer.Door"/> se vendent, rien ne s'achète au joueur.
        /// </summary>
        public bool AtDoor(string merchant) => Market != null && ShopStateOf(merchant) == ShopState.AtDoor;

        /// <summary>
        /// Sous-lot V2.14 : unités d'une offre fraîche sur l'étagère à cette minute pour un stock plein du jour de <paramref name="full"/> :
        /// sorties des fournées passées, moins ce que les villageois ont acheté ; <paramref name="full"/> pour une offre qui n'est pas fraîche.
        /// </summary>
        private int ShelfNow(Merchant m, MerchantOffer? offer, int full)
        {
            if (offer is null || !offer.Fresh || m.Bakes.Count == 0) return full;
            return ShopRules.OnShelf(m.Bakes, m.SoldOut, full, Zone.Time.Minute);
        }

        /// <summary>Sous-lot V2.14 : une fournée de <paramref name="merchant"/> est sortie du four depuis moins d'une heure (pain chaud).</summary>
        public bool WarmBread(string merchant)
        {
            Merchant m = NeedMarket().Book.Merchant(merchant);
            return m.Bakes.Count > 0 && ShopRules.Warm(m.Bakes, Zone.Time.Minute);
        }

        /// <summary>Sous-lot V2.14 : minute de la prochaine fournée de <paramref name="merchant"/> aujourd'hui, ou null.</summary>
        public int? NextBake(string merchant)
        {
            Merchant m = NeedMarket().Book.Merchant(merchant);
            return m.Bakes.Count > 0 ? ShopRules.NextBake(m.Bakes, Zone.Time.Minute) : null;
        }

        // --- Grand jour de marché -----------------------------------------------------------------------------------

        /// <summary>Sous-lot V2.14 : le grand jour de marché des données, ou null.</summary>
        public MarketDay? MarketDay => Market?.Book.Market;

        /// <summary>Sous-lot V2.14 : la fête du marché d'aujourd'hui (un jour de marché), ou null.</summary>
        public Festival? MarketFestivalToday => MarketDay is MarketDay md && FestivalToday is Festival f && md.Festivals.Contains(f.Id) ? f : null;

        /// <summary>
        /// Sous-lot V2.14 : le marchand de passage (ses fêtes) vient au marché d'aujourd'hui et le marché est là (installation, marché ou
        /// démontage) : il est sur la place ; il vend pendant le marché.
        /// </summary>
        private bool AtMarket(IReadOnlyList<string> festivals, bool selling) =>
            festivals.Count > 0 && MarketFestivalToday is Festival f && festivals.Contains(f.Id)
            && (selling ? MarketPhase == MarketPhase.Open : MarketPhase != MarketPhase.Calm);

        /// <summary>Sous-lot V2.14 : où en est la grande place à cet instant (calme les autres jours).</summary>
        public MarketPhase MarketPhase => MarketDay is MarketDay md ? ShopRules.PhaseOf(md, MarketFestivalToday, Zone.Time.Minute) : MarketPhase.Calm;

        /// <summary>
        /// Sous-lot V2.14 : prochain jour de marché à partir d'aujourd'hui (aujourd'hui s'il n'est pas fini) : jour du monde et fête ; null
        /// sans marché. Cherché sur deux semaines.
        /// </summary>
        public (long Day, Festival Festival)? NextMarketDay()
        {
            if (!(MarketDay is MarketDay md) || !(Narrative is Narrative n)) return null;
            for (long day = Zone.Time.Day; day <= Zone.Time.Day + 2 * WorldCalendar.DaysPerWeek; day++)
                if (n.Book.FestivalOn(day) is Festival f && md.Festivals.Contains(f.Id) && (day > Zone.Time.Day || Zone.Time.Minute < md.Teardown)) return (day, f);
            return null;
        }

        /// <summary>
        /// Sous-lot V2.14 : criée du moment (clé de texte), tour à tour toutes les cinq minutes du monde pendant le marché ; null hors du
        /// marché ou sans criée.
        /// </summary>
        public string? MarketCry => MarketPhase == MarketPhase.Open && MarketDay is MarketDay md && md.Cries.Count > 0
            ? md.Cries[(int)((Zone.Time.Day * 7 + Zone.Time.Minute / 5) % md.Cries.Count)] : null;

        /// <summary>
        /// Sous-lot V2.14 : prix marchandé de <paramref name="merchant"/> aujourd'hui, en pour cent (100 sans marchandage accepté). Valable le
        /// seul jour où il a été accepté, pendant le marché.
        /// </summary>
        public int HagglePercent(string merchant) =>
            Market is Market market && market.Haggled(merchant, Zone.Time.Day) is int percent && MarketPhase == MarketPhase.Open ? percent : 100;

        /// <summary>
        /// Sous-lot V2.14 : chance de réussite d'un marchandage avec <paramref name="merchant"/> (pour cent ; 0 : impossible : pas d'étal, pas
        /// de marché, réputation trop basse).
        /// </summary>
        public int HaggleChance(string merchant)
        {
            if (!(MarketDay is MarketDay md) || !(md.Haggle is HaggleRule rule) || !md.HasStall(merchant) || MarketPhase != MarketPhase.Open) return 0;
            int tier = Narrative is Narrative n ? n.StandingIndex(NeedMarket().Book.Merchant(merchant).Faction) : Reputations.TierIndex("neutral");
            return rule.ChanceAt(tier);
        }

        /// <summary>
        /// Sous-lot V2.14 : demande à <paramref name="merchant"/> (un étal du marché, ouvert, à portée) de baisser ses prix pour la journée.
        /// Accepté si le tirage du jour (<see cref="ShopRules.HaggleRoll"/>) est sous la chance que donne la réputation ; le même jour, la
        /// réponse ne change pas. Refusé, rien ne change.
        /// </summary>
        public HaggleRefusal Haggle(string merchant)
        {
            Market market = NeedMarket();
            Merchant m = market.Book.Merchant(merchant);
            if (!(market.Book.Market is MarketDay md) || md.Haggle is null || MarketPhase != MarketPhase.Open) return HaggleRefusal.NoMarket;
            if (!md.HasStall(m.Id)) return HaggleRefusal.NotAStall;
            if (CanTrade(m.Id) != TradeRefusal.None) return HaggleRefusal.Closed;
            if (market.Haggled(m.Id, Zone.Time.Day) != null) return HaggleRefusal.AlreadyDone;
            int chance = HaggleChance(m.Id);
            if (chance <= 0) return HaggleRefusal.StandingTooLow;
            if (ShopRules.HaggleRoll(m.Id, Zone.Time.Day) >= chance) return HaggleRefusal.Refused;
            market.RecordHaggle(m.Id, Zone.Time.Day, md.Haggle.Percent);
            return HaggleRefusal.None;
        }
    }
}
