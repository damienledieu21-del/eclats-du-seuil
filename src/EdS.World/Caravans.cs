using System;
using System.Collections.Generic;
using EdS.Data.Caravans;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Simulation.Randomness;
using EdS.Domain.Space;

namespace EdS.World
{
    /// <summary>Sous-lot V2.41.1 : où en est une caravane.</summary>
    public enum CaravanState
    {
        /// <summary>Sur la route, en marche.</summary>
        OnRoad = 0,

        /// <summary>Arrêtée à sa halte : les bandits l'attaquent (ou l'attaque est finie et pas encore réglée).</summary>
        Attacked = 1,

        /// <summary>Pillée : l'épave reste à la halte (jusqu'à la fin des conséquences de l'attaque, ou de la journée).</summary>
        Pillaged = 2,
    }

    /// <summary>Sous-lot V2.41.1 : une caravane à montrer (affichage seulement : ce n'est pas un combattant).</summary>
    public sealed class CaravanView
    {
        public Caravan Caravan { get; }

        /// <summary>Jour du monde de son départ.</summary>
        public long Day { get; }

        public CaravanState State { get; }
        public GroundVector Position { get; }

        /// <summary>Direction de la marche (celle du tronçon de route où elle est), non normée.</summary>
        public GroundVector Heading { get; }

        /// <summary>Distance parcourue depuis le départ.</summary>
        public Fixed Distance { get; }

        internal CaravanView(Caravan caravan, long day, CaravanState state, Fixed distance)
        {
            Caravan = caravan;
            Day = day;
            State = state;
            Distance = distance;
            (Position, Heading) = caravan.At(distance);
        }
    }

    /// <summary>
    /// Sous-lot V2.41.1 : les caravanes, calculées et non simulées (plan de la grande carte, § 5) : la place d'une caravane est une fonction
    /// pure du temps du monde (en millièmes de minute), de son attaque (occurrence tirée comme tout événement du monde) et de l'issue gardée au
    /// registre. Le jour d'une attaque, la caravane s'arrête à sa halte du moment où elle l'atteint jusqu'à la fin de l'attaque ; sauvée ou
    /// passée, elle repart alors ; pillée, son épave reste à la halte jusqu'à la fin des conséquences (<see cref="WorldEventDefinition.AftermathMinutes"/>).
    /// </summary>
    public static class CaravanSchedule
    {
        /// <summary>Millièmes de minute du monde par minute.</summary>
        public const long Milli = 1000;

        /// <summary>
        /// Les gardes de l'occurrence <paramref name="o"/> repoussent seuls les bandits (tirage fixe par occurrence, indépendant des tirages de
        /// l'événement : même issue pour tous les personnages et à chaque relecture).
        /// </summary>
        public static bool GuardsHold(string zone, WorldEventDefinition e, EventOccurrence o)
        {
            if (e is null) throw new ArgumentNullException(nameof(e));
            if (o is null) throw new ArgumentNullException(nameof(o));
            if (e.GuardsPercent <= 0) return false;
            var random = new Pcg32(Loot.Fnv1a(WorldEventSchedule.Key(zone, e.Id, o.Window)), Loot.Fnv1a("caravan-guards:" + e.Id));
            return random.NextBounded(100) < (uint)e.GuardsPercent;
        }

        /// <summary>Instant absolu du monde en millièmes de minute au tick <paramref name="worldTick"/> (même découpage que <see cref="WorldClock.At"/>).</summary>
        public static long MilliAt(WorldClock clock, long worldTick)
        {
            if (clock is null) throw new ArgumentNullException(nameof(clock));
            if (worldTick < 0) throw new ArgumentOutOfRangeException(nameof(worldTick));
            long ms = checked(worldTick * clock.TickMilliseconds);
            long dayCount = ms / clock.DayLengthMs, within = ms % clock.DayLengthMs;
            return checked((dayCount * WorldClock.MinutesPerDay + clock.StartMinute) * Milli + within * WorldClock.MinutesPerDay * Milli / clock.DayLengthMs);
        }

        /// <summary>
        /// La caravane <paramref name="c"/> à l'instant <paramref name="nowMilli"/> (millièmes de minute absolus), ou null si elle n'est pas sur
        /// la route. <paramref name="outcomeOf"/> rend l'issue réglée de l'attaque du jour donné (null : pas encore réglée).
        /// </summary>
        public static CaravanView? At(string zone, string weatherZone, Caravan c, long nowMilli, Func<long, EventOutcome?> outcomeOf)
        {
            if (c is null) throw new ArgumentNullException(nameof(c));
            if (outcomeOf is null) throw new ArgumentNullException(nameof(outcomeOf));
            long today = Math.Max(0, nowMilli) / (WorldClock.MinutesPerDay * Milli);
            // Un voyage dure moins d'une journée (validateur) : seuls les départs de la veille et du jour peuvent être en route.
            for (long day = today; day >= Math.Max(0, today - 1); day--)
            {
                if (!c.Runs(day)) continue;
                CaravanView? v = OnDay(zone, weatherZone, c, day, nowMilli, outcomeOf);
                if (v != null) return v;
            }
            return null;
        }

        private static CaravanView? OnDay(string zone, string weatherZone, Caravan c, long day, long now, Func<long, EventOutcome?> outcomeOf)
        {
            long depart = checked((day * WorldClock.MinutesPerDay + c.StartMinute) * Milli);
            if (now < depart) return null;
            EventOccurrence? o = WorldEventSchedule.InWindow(zone, weatherZone, c.Attack, day / c.Attack.EveryDays);
            if (o != null && o.Day != day) o = null;
            Fixed Walked(long from, long to) => Fixed.FromRaw(checked(c.MetersPerMinute.Raw * (to - from) / Milli));
            Fixed walked;
            if (o is null) walked = Walked(depart, now);
            else
            {
                long end = checked(o.EndAt * Milli);
                if (now < end) return new CaravanView(c, day, now >= o.StartAt * Milli ? CaravanState.Attacked : CaravanState.OnRoad,
                    Fixed.Min(Walked(depart, now), c.HaltDistance));
                EventOutcome? outcome = outcomeOf(day);
                if (outcome is null) return new CaravanView(c, day, CaravanState.Attacked, Fixed.Min(Walked(depart, now), c.HaltDistance));
                if (outcome == EventOutcome.Lost)
                {
                    // Bornée à la fin du lendemain : seuls les départs de la veille et du jour sont regardés.
                    long gone = Math.Min(checked(end + c.Attack.AftermathMinutes * Milli), checked((day + 2) * WorldClock.MinutesPerDay * Milli));
                    return now < gone ? new CaravanView(c, day, CaravanState.Pillaged, c.HaltDistance) : null;
                }
                // Sauvée ou passée : elle repart de la halte à la fin de l'attaque (si elle l'avait atteinte ; sinon elle a simplement marché).
                Fixed before = Walked(depart, end);
                walked = before < c.HaltDistance ? Walked(depart, now) : c.HaltDistance + Walked(end, now);
            }
            return walked >= c.Length ? null : new CaravanView(c, day, CaravanState.OnRoad, walked);
        }

        /// <summary>Les caravanes de <paramref name="book"/> sur la route à l'instant <paramref name="nowMilli"/>, dans l'ordre du fichier.</summary>
        public static IReadOnlyList<CaravanView> All(string zone, string weatherZone, CaravanBook book, long nowMilli, Func<string, long, EventOutcome?> outcomeOf)
        {
            if (book is null) throw new ArgumentNullException(nameof(book));
            var list = new List<CaravanView>();
            foreach (Caravan c in book.Caravans)
                if (At(zone, weatherZone, c, nowMilli, d => outcomeOf(c.Attack.Id, d)) is CaravanView v) list.Add(v);
            return list;
        }
    }
}
