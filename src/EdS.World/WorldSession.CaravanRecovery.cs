using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Caravans;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;

namespace EdS.World
{
    /// <summary>Sous-lot V2.41.3 : refus de <see cref="WorldSession.TakeCaravanChest"/> (rien ne change).</summary>
    public enum ChestRefusal
    {
        None = 0,

        /// <summary>Caravane, quête de reprise ou coffre inconnus.</summary>
        Unknown = 1,

        /// <summary>Aucune quête de reprise ouverte (pas de pillage, délai passé, ou tous les coffres déjà repris).</summary>
        Closed = 2,

        /// <summary>Ce coffre est déjà repris.</summary>
        Taken = 3,

        /// <summary>Le personnage est tombé, absent ou trop loin du coffre.</summary>
        NotHere = 4,

        /// <summary>Un ennemi est encore debout près du coffre : il faut d'abord battre ses gardiens.</summary>
        Guarded = 5,

        /// <summary>Le registre des événements ne peut pas garder la reprise (borne technique).</summary>
        Full = 6,
    }

    /// <summary>Sous-lot V2.41.3 : une quête de reprise ouverte, pour l'affichage (journal, coffres au fort).</summary>
    public sealed class CaravanRecoveryView
    {
        public Caravan Caravan { get; }
        public CaravanRecovery Recovery { get; }

        /// <summary>Jour du monde du pillage.</summary>
        public long PillageDay { get; }

        /// <summary>Dernier jour du monde où la quête est ouverte (compris).</summary>
        public long LastDay { get; }

        /// <summary>Coffres repris (bit k : coffre k).</summary>
        public int Taken { get; }

        public bool IsTaken(int chest) => (Taken & (1 << chest)) != 0;

        internal CaravanRecoveryView(Caravan caravan, long pillageDay, int taken)
        {
            Caravan = caravan;
            Recovery = caravan.Recovery!;
            PillageDay = pillageDay;
            LastDay = checked(pillageDay + Recovery.Days - 1);
            Taken = taken;
        }
    }

    public sealed partial class WorldSession
    {
        /// <summary>Nombre de coffres repris d'un masque (sans API .NET récente).</summary>
        internal static int ChestCount(int mask)
        {
            int n = 0;
            for (int m = mask; m != 0; m &= m - 1) n++;
            return n;
        }

        /// <summary>
        /// Jour du dernier pillage gardé au registre (<see cref="SavedEvent.LostDay"/> ; inconnu, sauvegarde v29 ou antérieure : la dernière
        /// issue si elle est un pillage), -1 sinon.
        /// </summary>
        private static long PillageDay(SavedEvent entry) =>
            entry.LostDay >= 0 ? entry.LostDay : entry.Outcome == EventOutcome.Lost ? entry.Last : -1;

        /// <summary>La quête de reprise de <paramref name="c"/> si elle est ouverte aujourd'hui, ou null.</summary>
        private CaravanRecoveryView? OpenRecovery(Caravan c)
        {
            if (c.Recovery is null || !(Definition?.EventOf(c.Attack.Id) is WorldEventDefinition e) || !HasEventEntry(e)) return null;
            SavedEvent entry = EventEntry(e);
            long day = PillageDay(entry);
            if (day < 0) return null;
            long since = Zone.Time.Day - day;
            // Relecture de la PR #243 : seuls les bits des coffres de la quête comptent (des bits en trop ne la bloquent pas).
            int taken = entry.Recovered & c.Recovery.AllChests;
            if (since < 0 || since >= c.Recovery.Days || taken == c.Recovery.AllChests) return null;
            return new CaravanRecoveryView(c, day, taken);
        }

        /// <summary>
        /// Sous-lot V2.41.3 : les quêtes de reprise ouvertes aujourd'hui (une par caravane pillée depuis moins de ses jours, tant que des
        /// coffres restent au fort), dans l'ordre du fichier.
        /// </summary>
        public IReadOnlyList<CaravanRecoveryView> CaravanRecoveries =>
            _caravans is null ? Array.Empty<CaravanRecoveryView>() : _caravans.Caravans.Select(OpenRecovery).OfType<CaravanRecoveryView>().ToList();

        /// <summary>
        /// Sous-lot V2.41.3 (décision de Damien du 21 novembre 2026) : reprend le coffre <paramref name="chest"/> de la caravane
        /// <paramref name="caravan"/> au fort, pendant sa quête de reprise : à portée (<see cref="CaravanRecovery.Reach"/>) et quand aucun
        /// ennemi n'est debout à moins de <see cref="CaravanRecovery.GuardRadius"/> du coffre. Chaque coffre enlève un jour au contrecoup des
        /// marchands ; le dernier l'annule et donne la récompense : la première fois, l'expérience (sous le plafond) et le cuivre ; ensuite,
        /// le cuivre seul (décision de Damien du 21 novembre 2026) ; le cuivre si la bourse le tient, sinon rien, comme la prime. Gardé au registre (sauvegarde v31). Annonces au tick suivant. Refus atomique.
        /// </summary>
        public ChestRefusal TakeCaravanChest(string caravan, int chest)
        {
            if (_caravans is null || caravan is null || !(_caravans.Of(caravan) is Caravan c) || !(c.Recovery is CaravanRecovery rec)
                || chest < 0 || chest >= rec.Chests.Count)
                return ChestRefusal.Unknown;
            if (!(OpenRecovery(c) is CaravanRecoveryView open)) return ChestRefusal.Closed;
            if (open.IsTaken(chest)) return ChestRefusal.Taken;
            GroundVector at = rec.Chests[chest];
            CombatantView me = Zone.Engine.View(CombatantId);
            if (!me.IsPresent || me.IsDead || !(me.Position is GroundVector p) || ZonePopulations.Beyond(p, Fixed.Zero, at, Fixed.Zero, CaravanRecovery.Reach))
                return ChestRefusal.NotHere;
            if (Guarded(me, at, rec.GuardRadius)) return ChestRefusal.Guarded;
            WorldEventDefinition e = Definition!.EventOf(c.Attack.Id)!;
            if (!HasEventRoom(e)) return ChestRefusal.Full;
            // Tout est calculé avant de rien changer.
            SavedEvent entry = EventEntry(e);
            int taken = (open.Taken | (1 << chest)) & rec.AllChests;
            bool done = taken == rec.AllChests;
            // Décision de Damien du 21 novembre 2026 (anti-triche) : l'expérience à la première reprise complète seulement ; ensuite, l'or seul
            // (comme la prime d'une caravane sauvée).
            bool first = done && !entry.RecoveredOnce;
            long xp = first ? Math.Min(SaveValidator.MaxXp, checked(_character.Xp + rec.Xp)) : _character.Xp;
            long money = _character.Money, paid = 0;
            if (done && rec.Money > 0 && Coins.TryAdd(_character.Money, rec.Money, out long purse)) { money = purse; paid = rec.Money; }
            var line = new SavedEvent(entry.Id, entry.Won, entry.Lost, entry.Last, entry.Outcome, entry.Owed, open.PillageDay, taken, entry.RecoveredOnce || done);
            if (!SetEventEntry(e, line)) return ChestRefusal.Full;
            long gained = xp - _character.Xp;
            _character = _character.WithMoney(money).WithXp(xp);
            _queuedNotices.Add(new WorldEventNotice(WorldEventNoticeKind.ChestRecovered, e.Id, string.Empty, open.PillageDay, chest));
            if (done) _queuedNotices.Add(new WorldEventNotice(WorldEventNoticeKind.Recovered, e.Id, string.Empty, open.PillageDay, 0, gained, paid));
            return ChestRefusal.None;
        }

        /// <summary>Un ennemi debout (présent, vivant, qui ne fuit pas) a son centre à moins de <paramref name="radius"/> de <paramref name="at"/>.</summary>
        private bool Guarded(CombatantView me, GroundVector at, Fixed radius)
        {
            foreach (string id in Zone.Engine.CombatantIds)
            {
                if (id == CombatantId) continue;
                CombatantView v = Zone.Engine.View(id);
                if (v.Team == me.Team || !v.IsPresent || v.IsDead || v.Flees || !(v.Position is GroundVector q)) continue;
                if (!ZonePopulations.Beyond(q, Fixed.Zero, at, Fixed.Zero, radius)) return true;
            }
            return false;
        }

        /// <summary>Sous-lot V2.41.3 : un pillage vient d'être réglé : la quête de reprise s'ouvre (annoncée si elle l'est encore aujourd'hui).</summary>
        private void RecoveryOpened(WorldEventDefinition e, EventOccurrence o, List<WorldEventNotice> notices)
        {
            if (e.Kind != WorldEventKind.Caravan || !(_caravans?.OfAttack(e.Id) is Caravan c) || !(c.Recovery is CaravanRecovery rec)) return;
            long since = Zone.Time.Day - o.Day;
            if (since >= 0 && since < rec.Days) notices.Add(new WorldEventNotice(WorldEventNoticeKind.RecoveryOpened, e.Id, o.Group.Id, o.Day));
        }
    }
}
