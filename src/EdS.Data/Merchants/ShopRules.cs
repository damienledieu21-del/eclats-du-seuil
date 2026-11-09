using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using EdS.Data.Scenarios;
using EdS.Data.Story;
using EdS.Domain.Collections;
using EdS.Domain.Space;

namespace EdS.Data.Merchants
{
    // --- Documents (avant validation), schéma 6 -------------------------------------------------------------

    /// <summary>Sous-lot V2.14 (schéma 6) : la boutique d'un marchand (vraie maison), avant validation.</summary>
    public sealed class ShopData
    {
        public string? Kind { get; set; }
        public string? Sign { get; set; }
        public string? Building { get; set; }
        public string? Zone { get; set; }
        public PointData? Counter { get; set; }
        public string? Open { get; set; }
        public string? Close { get; set; }
        public List<int?>? Rest { get; set; }
        public string? Door { get; set; }
        public string? Clerk { get; set; }
    }

    /// <summary>Sous-lot V2.14 (schéma 6) : une fournée, avant validation.</summary>
    public sealed class BakeData
    {
        public string? At { get; set; }
        public int? Percent { get; set; }
    }

    /// <summary>Sous-lot V2.14 (schéma 6) : le grand jour de marché, avant validation.</summary>
    public sealed class MarketDayData
    {
        public List<string?>? Festivals { get; set; }
        public string? Setup { get; set; }
        public string? Open { get; set; }
        public string? Close { get; set; }
        public string? Teardown { get; set; }
        public int? Crowd { get; set; }
        public List<string?>? Cries { get; set; }
        public List<StallData?>? Stalls { get; set; }
        public HaggleData? Haggle { get; set; }
    }

    /// <summary>Sous-lot V2.14 (schéma 6) : un étal du marché, avant validation.</summary>
    public sealed class StallData
    {
        public string? Id { get; set; }
        public string? Merchant { get; set; }
        public string? Kind { get; set; }
        public PointData? Position { get; set; }
        public string? Sign { get; set; }
    }

    /// <summary>Sous-lot V2.14 (schéma 6) : le marchandage, avant validation.</summary>
    public sealed class HaggleData
    {
        public string? Standing { get; set; }
        public int? Percent { get; set; }
        public int? Chance { get; set; }
        public int? Bonus { get; set; }
    }

    // --- Modèle validé ----------------------------------------------------------------------------------------

    /// <summary>Sous-lot V2.14 : qui répond quand la boutique est fermée : personne, le patron à sa porte, ou un commis.</summary>
    public enum DoorService
    {
        None = 0,
        Patron = 1,
        Clerk = 2,
    }

    /// <summary>
    /// Sous-lot V2.14 (décision de Damien du 9 novembre 2026) : une vraie boutique dans une vraie maison : son genre (boulangerie, forge,
    /// auberge…), son enseigne, le bâtiment du bourg qui l'abrite, ses horaires d'ouverture (de <see cref="Open"/> à <see cref="Close"/>, en
    /// minutes du jour, fin exclue ; une fermeture avant l'ouverture passe minuit), ses jours de repos (jours de la semaine) et qui répond à
    /// la porte quand elle est fermée (le patron, un commis, ou personne).
    /// </summary>
    public sealed class Shop
    {
        public string Kind { get; }
        public string Sign { get; }
        public string? Building { get; }

        /// <summary>Intérieur de la boutique (« zone.boulangerie »), où l'on achète au comptoir ; null : on achète au marchand seulement.</summary>
        public string? Zone { get; private set; }

        /// <summary>Place du comptoir dans <see cref="Zone"/> (servi par le patron ou le commis).</summary>
        public GroundVector Counter { get; private set; }

        internal Shop WithInterior(string? zone, GroundVector counter)
        {
            Zone = zone;
            Counter = counter;
            return this;
        }

        public int Open { get; }
        public int Close { get; }
        public IReadOnlyList<int> Rest { get; }
        public DoorService Door { get; }

        /// <summary>Clé du texte du commis (« Bastien, le commis »), si <see cref="Door"/> vaut <see cref="DoorService.Clerk"/>.</summary>
        public string? Clerk { get; }

        /// <summary>Boutique déjà validée (le validateur <see cref="MerchantValidator"/> en donne le détail) ; contrôles de forme seulement.</summary>
        /// <exception cref="ArgumentException">Heures hors du jour, fenêtre vide, jour hors de la semaine.</exception>
        public Shop(string kind, string sign, string? building, int open, int close, IEnumerable<int> rest, DoorService door, string? clerk)
        {
            if (open < 0 || open >= ShopRules.MinutesPerDay) throw new ArgumentOutOfRangeException(nameof(open));
            // Ouverte jour et nuit : de 0 à MinutesPerDay (horaires absents du fichier).
            if (close < 0 || close > ShopRules.MinutesPerDay || close == open || (close == ShopRules.MinutesPerDay && open != 0))
                throw new ArgumentOutOfRangeException(nameof(close));
            if (rest is null || rest.Any(d => d < 0 || d >= WorldCalendar.DaysPerWeek)) throw new ArgumentOutOfRangeException(nameof(rest));
            Kind = kind;
            Sign = sign;
            Building = building;
            Open = open;
            Close = close;
            Rest = Frozen.List(rest, nameof(rest));
            Door = door;
            Clerk = clerk;
        }

        /// <summary>Sans horaires : ouverte jour et nuit (hors jours de repos et moments de fermeture).</summary>
        public bool AllDay => Open == 0 && Close == ShopRules.MinutesPerDay;

        /// <summary>Ouverte à la minute <paramref name="minute"/> d'un jour ordinaire (hors jour de repos).</summary>
        public bool InHours(int minute) => ShopRules.InWindow(Open, Close, minute);

        /// <summary>
        /// Jour de la semaine qui compte pour un horaire à <paramref name="minute"/> du jour <paramref name="weekday"/> : après minuit, une
        /// ouverture de la veille au soir (l'auberge jusqu'à 1 h) appartient encore à la veille.
        /// </summary>
        public int OpeningWeekday(int weekday, int minute) =>
            Close < Open && minute < Close ? (weekday + WorldCalendar.DaysPerWeek - 1) % WorldCalendar.DaysPerWeek : weekday;

        /// <summary>Ouverte ce jour de la semaine à cette minute : dans ses heures, et pas un jour de repos.</summary>
        public bool IsOpen(int weekday, int minute) => InHours(minute) && !Rest.Contains(OpeningWeekday(weekday, minute));
    }

    /// <summary>Sous-lot V2.14 : une fournée : à <see cref="At"/> (minute du jour), <see cref="Percent"/> % de la fournée du jour sortent du four.</summary>
    public sealed class Bake
    {
        public int At { get; }
        public int Percent { get; }

        /// <exception cref="ArgumentOutOfRangeException">Heure hors du jour, part hors de [1, 100] %.</exception>
        public Bake(int at, int percent)
        {
            if (at < 0 || at >= ShopRules.MinutesPerDay) throw new ArgumentOutOfRangeException(nameof(at));
            if (percent < 1 || percent > 100) throw new ArgumentOutOfRangeException(nameof(percent));
            At = at;
            Percent = percent;
        }
    }

    /// <summary>Sous-lot V2.14 : genre d'étal du marché (pour l'affichage : auvent, marchandises).</summary>
    public sealed class MarketStall
    {
        public string Id { get; }
        public string? Merchant { get; }
        public string Kind { get; }
        public GroundVector Position { get; }
        public string? Sign { get; }

        internal MarketStall(string id, string? merchant, string kind, GroundVector position, string? sign)
        {
            Id = id;
            Merchant = merchant;
            Kind = kind;
            Position = position;
            Sign = sign;
        }
    }

    /// <summary>
    /// Sous-lot V2.14 : le marchandage du jour de marché : au palier <see cref="Standing"/> (ou au-dessus) de sa faction, un marchand de
    /// l'étal accepte une fois par jour de baisser ses prix à <see cref="Percent"/> % avec une chance de <see cref="Chance"/> %, plus
    /// <see cref="Bonus"/> % par palier au-dessus (au plus 100). Le tirage est fixé par le jour et le marchand : redemander ne change rien.
    /// </summary>
    public sealed class HaggleRule
    {
        public string Standing { get; }
        public int Percent { get; }
        public int Chance { get; }
        public int Bonus { get; }

        internal HaggleRule(string standing, int percent, int chance, int bonus)
        {
            Standing = standing;
            Percent = percent;
            Chance = chance;
            Bonus = bonus;
        }

        /// <summary>Chance de réussite au palier <paramref name="tier"/> (0 sous le palier demandé).</summary>
        public int ChanceAt(int tier)
        {
            int need = Reputations.TierIndex(Standing);
            if (tier < need) return 0;
            return Math.Min(100, checked(Chance + Bonus * (tier - need)));
        }
    }

    /// <summary>
    /// Sous-lot V2.14 (décision de Damien du 9 novembre 2026) : le grand jour de marché sur la grande place du bourg, les jours des fêtes
    /// hebdomadaires <see cref="Festivals"/> (une ou deux par semaine) : les étals s'installent à <see cref="Setup"/>, le marché tient de
    /// <see cref="Open"/> à <see cref="Close"/> (la fête, son animation, se tient dedans), puis on démonte jusqu'à <see cref="Teardown"/>. Les
    /// marchands de passage du marché sont là de l'installation au démontage et vendent pendant le marché. Le reste de la semaine, la place
    /// est calme.
    /// </summary>
    public sealed class MarketDay
    {
        public IReadOnlyList<string> Festivals { get; }
        public int Setup { get; }
        public int Open { get; }
        public int Close { get; }
        public int Teardown { get; }

        /// <summary>Villageois de plus sur la place pendant le marché (affichage).</summary>
        public int Crowd { get; }

        /// <summary>Criées des marchands (clés de texte), dites tour à tour pendant le marché.</summary>
        public IReadOnlyList<string> Cries { get; }

        public IReadOnlyList<MarketStall> Stalls { get; }
        public HaggleRule? Haggle { get; }

        internal MarketDay(IEnumerable<string> festivals, int setup, int open, int close, int teardown, int crowd, IEnumerable<string> cries, IEnumerable<MarketStall> stalls, HaggleRule? haggle)
        {
            Festivals = Frozen.List(festivals, nameof(festivals));
            Setup = setup;
            Open = open;
            Close = close;
            Teardown = teardown;
            Crowd = crowd;
            Cries = Frozen.List(cries, nameof(cries));
            Stalls = Frozen.List(stalls, nameof(stalls));
            Haggle = haggle;
        }

        /// <summary>Le marchand tient un étal du marché (il marchande).</summary>
        public bool HasStall(string merchant) => Stalls.Any(s => s.Merchant == merchant);
    }

    /// <summary>Sous-lot V2.14 : où en est la grande place un jour de marché.</summary>
    public enum MarketPhase
    {
        /// <summary>Pas de marché aujourd'hui, ou pas encore installé, ou déjà démonté : la place est calme.</summary>
        Calm = 0,

        /// <summary>Les étals s'installent (avant l'ouverture).</summary>
        Setup = 1,

        /// <summary>Le marché bat son plein.</summary>
        Open = 2,

        /// <summary>On démonte les étals (après la fermeture).</summary>
        Teardown = 3,
    }

    /// <summary>Sous-lot V2.14 : état d'une boutique à cet instant.</summary>
    public enum ShopState
    {
        /// <summary>Ouverte (ou sans horaires).</summary>
        Open = 0,

        /// <summary>Fermée (heure de fermeture), mais le patron ou un commis répond à la porte : seulement les offres « door ».</summary>
        AtDoor = 1,

        /// <summary>Fermée hors de ses horaires (personne ne répond).</summary>
        ClosedHours = 2,

        /// <summary>Fermée : jour de repos (personne ne répond).</summary>
        RestDay = 3,
    }

    /// <summary>
    /// Sous-lot V2.14 : règles pures des boutiques et du marché, recalculées du jour et de la minute du monde (aucune horloge réelle, aucun
    /// état) : fenêtres d'horaires, fournées et rupture du soir, phases du jour de marché, tirage du marchandage.
    /// </summary>
    public static class ShopRules
    {
        public const int MinutesPerDay = 1440;

        /// <summary>Genres de boutiques connus.</summary>
        public static IReadOnlyList<string> Kinds { get; } = new ReadOnlyCollection<string>(new[]
            { "bakery", "forge", "inn", "tailor", "herbalist", "grocer", "armorer", "guild", "stall" });

        /// <summary>Genres d'étals du marché connus.</summary>
        public static IReadOnlyList<string> StallKinds { get; } = new ReadOnlyCollection<string>(new[]
            { "produce", "cheese", "cloth", "spices", "pottery", "fish", "meat", "flowers", "toys", "exotic", "honey", "wine", "bread", "tools" });

        /// <summary>Minute du jour d'une heure « HH:MM » (00:00 à 23:59), ou null si elle est mal formée.</summary>
        public static int? MinuteOf(string? at)
        {
            if (at is null || at.Length != 5 || at[2] != ':') return null;
            foreach (int i in new[] { 0, 1, 3, 4 })
                if (at[i] < '0' || at[i] > '9') return null;
            int hour = (at[0] - '0') * 10 + (at[1] - '0'), minute = (at[3] - '0') * 10 + (at[4] - '0');
            return hour < 24 && minute < 60 ? hour * 60 + minute : (int?)null;
        }

        /// <summary>« HH:MM » d'une minute du jour.</summary>
        public static string Clock(int minute)
        {
            if (minute < 0 || minute >= MinutesPerDay) throw new ArgumentOutOfRangeException(nameof(minute));
            return (minute / 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture) + ":"
                + (minute % 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// La minute <paramref name="minute"/> est dans la fenêtre [<paramref name="from"/>, <paramref name="to"/>[ ; une fin avant le début
        /// passe minuit (de 18 h à 1 h). Une fenêtre vide (<paramref name="from"/> = <paramref name="to"/>) ne contient rien.
        /// </summary>
        public static bool InWindow(int from, int to, int minute)
        {
            if (minute < 0 || minute >= MinutesPerDay) throw new ArgumentOutOfRangeException(nameof(minute));
            return from <= to ? minute >= from && minute < to : minute >= from || minute < to;
        }

        /// <summary>
        /// Prochaine ouverture de <paramref name="shop"/> à partir du jour <paramref name="day"/>, minute <paramref name="minute"/> (exclue) :
        /// jour du monde et minute ; null si elle n'ouvre jamais (jamais avec une boutique validée). Cherchée sur une semaine et un jour.
        /// </summary>
        public static (long Day, int Minute)? NextOpening(Shop shop, long day, int minute)
        {
            if (shop is null) throw new ArgumentNullException(nameof(shop));
            if (day < 0) throw new ArgumentOutOfRangeException(nameof(day));
            if (minute < 0 || minute >= MinutesPerDay) throw new ArgumentOutOfRangeException(nameof(minute));
            for (long d = day; d <= checked(day + WorldCalendar.DaysPerWeek + 1); d++)
            {
                int start = d == day ? minute + 1 : 0;
                for (int m = start; m < MinutesPerDay; m++)
                {
                    int previous = m == 0 ? MinutesPerDay - 1 : m - 1;
                    long pd = m == 0 ? d - 1 : d;
                    bool wasOpen = pd >= 0 && shop.IsOpen(WorldCalendar.WeekdayOf(pd), previous);
                    if (!wasOpen && shop.IsOpen(WorldCalendar.WeekdayOf(d), m)) return (d, m);
                }
            }
            return null;
        }

        /// <summary>
        /// Part de la fournée du jour sortie du four à la minute <paramref name="minute"/> (pour cent, de 0 à 100) : la somme des fournées
        /// passées.
        /// </summary>
        public static int Baked(IReadOnlyList<Bake> bakes, int minute)
        {
            if (bakes is null) throw new ArgumentNullException(nameof(bakes));
            int sum = 0;
            foreach (Bake b in bakes)
                if (b.At <= minute) sum = checked(sum + b.Percent);
            return Math.Min(100, sum);
        }

        /// <summary>
        /// Part de la fournée du jour déjà achetée par les villageois à la minute <paramref name="minute"/> (pour cent) : de 0 à la première
        /// fournée à 100 à <paramref name="soldOut"/>, en ligne droite, jamais plus que ce qui est sorti du four.
        /// </summary>
        public static int Villagers(IReadOnlyList<Bake> bakes, int soldOut, int minute)
        {
            if (bakes is null) throw new ArgumentNullException(nameof(bakes));
            if (bakes.Count == 0) return 0;
            int first = bakes[0].At;
            if (minute <= first) return 0;
            int linear = minute >= soldOut ? 100 : checked((minute - first) * 100) / (soldOut - first);
            return Math.Min(Baked(bakes, minute), linear);
        }

        /// <summary>
        /// Unités d'une offre fraîche (pains, viennoiseries) sur l'étagère à la minute <paramref name="minute"/>, pour un stock plein de
        /// <paramref name="full"/> : ce qui est sorti du four moins ce que les villageois ont acheté (arrondi vers le bas, jamais négatif).
        /// Avant la première fournée : rien ; à partir de <paramref name="soldOut"/> : rupture.
        /// </summary>
        public static int OnShelf(IReadOnlyList<Bake> bakes, int soldOut, int full, int minute)
        {
            if (full < 0) throw new ArgumentOutOfRangeException(nameof(full));
            int percent = Baked(bakes, minute) - Villagers(bakes, soldOut, minute);
            return checked(full * Math.Max(0, percent)) / 100;
        }

        /// <summary>Une fournée est sortie du four depuis moins de <paramref name="warmMinutes"/> minutes (le pain est encore chaud).</summary>
        public static bool Warm(IReadOnlyList<Bake> bakes, int minute, int warmMinutes = 60)
        {
            if (bakes is null) throw new ArgumentNullException(nameof(bakes));
            return bakes.Any(b => minute >= b.At && minute < b.At + warmMinutes);
        }

        /// <summary>Prochaine fournée après la minute <paramref name="minute"/> aujourd'hui, ou null.</summary>
        public static int? NextBake(IReadOnlyList<Bake> bakes, int minute)
        {
            if (bakes is null) throw new ArgumentNullException(nameof(bakes));
            foreach (Bake b in bakes)
                if (b.At > minute) return b.At;
            return null;
        }

        /// <summary>
        /// Phase de la grande place à la minute <paramref name="minute"/> d'un jour où se tient le marché de la fête <paramref name="festival"/>
        /// (null : pas de marché ce jour-là : calme).
        /// </summary>
        public static MarketPhase PhaseOf(MarketDay market, Festival? festival, int minute)
        {
            if (market is null) throw new ArgumentNullException(nameof(market));
            if (minute < 0 || minute >= MinutesPerDay) throw new ArgumentOutOfRangeException(nameof(minute));
            if (festival is null || !market.Festivals.Contains(festival.Id)) return MarketPhase.Calm;
            if (minute >= market.Setup && minute < market.Open) return MarketPhase.Setup;
            if (minute >= market.Open && minute < market.Close) return MarketPhase.Open;
            if (minute >= market.Close && minute < market.Teardown) return MarketPhase.Teardown;
            return MarketPhase.Calm;
        }

        /// <summary>
        /// Tirage du marchandage (0 à 99) de <paramref name="merchant"/> le jour <paramref name="day"/> : empreinte FNV-1a de
        /// « haggle/&lt;marchand&gt;/&lt;jour&gt; », fixe pour le jour (redemander ne change rien).
        /// </summary>
        public static int HaggleRoll(string merchant, long day)
        {
            if (merchant is null) throw new ArgumentNullException(nameof(merchant));
            if (day < 0) throw new ArgumentOutOfRangeException(nameof(day));
            const uint offset = 2166136261, prime = 16777619;
            uint hash = offset;
            foreach (char c in "haggle/" + merchant + "/" + day.ToString(System.Globalization.CultureInfo.InvariantCulture))
                unchecked
                {
                    hash ^= (byte)(c & 0xFF);
                    hash *= prime;
                    hash ^= (byte)(c >> 8);
                    hash *= prime;
                }
            return (int)(hash % 100);
        }
    }
}
