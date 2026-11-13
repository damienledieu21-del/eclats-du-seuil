using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V1.1 : un point sûr découvert, destination du voyage rapide.</summary>
    public sealed class TravelDestination
    {
        public string Zone { get; }
        public string SafePoint { get; }
        public string Name { get; }
        public double X { get; }
        public double Z { get; }

        /// <summary>Le personnage y est (même zone, à <see cref="WorldSession.DiscoverRange"/>) : le voyage y est refusé
        /// (<see cref="TravelRefusal.AlreadyHere"/>).</summary>
        public bool Here { get; }

        internal TravelDestination(string zone, string safePoint, string name, double x, double z, bool here)
        {
            Zone = zone;
            SafePoint = safePoint;
            Name = name;
            X = x;
            Z = z;
            Here = here;
        }
    }

    /// <summary>
    /// Sous-lot V1.1 : le voyage rapide entre les points sûrs découverts (zones du dehors), proposé par la carte. Il marche hors combat (la
    /// règle de la halte et des talents : <see cref="WorldSession.InCombat"/>), sans dialogue ouvert ni reprise à faire, et reprend le chemin
    /// d'une porte : la session d'arrivée est préparée, la partie sauvegardée dans la zone d'arrivée, puis la session s'ouvre au point sûr
    /// (PV pleins ; niveau, équipement et talents pris en compte, comme à une halte). Gratuit dans la vague 1.
    /// </summary>
    public sealed partial class AdventureSession
    {
        /// <summary>Voyages rapides faits depuis l'ouverture de l'aventure (l'interface fait un fondu à chacun). Affichage seulement.</summary>
        public int Travels { get; private set; }

        /// <summary>Points sûrs découverts des zones du dehors, dans l'ordre des données (zone de départ d'abord).</summary>
        public IReadOnlyList<TravelDestination> TravelDestinations()
        {
            var list = new List<TravelDestination>();
            foreach (ZoneDefinition zone in _zones.Where(z => !z.Indoor))
                foreach (SafePoint s in zone.SafePoints)
                {
                    if (!World.IsDiscovered(zone, s.Id)) continue;
                    list.Add(new TravelDestination(zone.Id, s.Id, s.Name, s.Position.X.Raw / 10000.0, s.Position.Z.Raw / 10000.0, World.IsAt(zone, s.Id)));
                }
            return list;
        }

        /// <summary>
        /// Refus qu'aurait le voyage vers le point sûr <paramref name="safePoint"/> de la zone <paramref name="zone"/> maintenant (rien ne
        /// change) : destination inconnue ou pas découverte, personnage tombé, dialogue ouvert ou reprise à faire, déjà à ce point sûr, en
        /// plein combat.
        /// </summary>
        public TravelRefusal CanTravel(string zone, string safePoint)
        {
            ZoneDefinition? target = _zones.FirstOrDefault(z => z.Id == zone);
            if (target is null) return TravelRefusal.UnknownPlace;
            TravelRefusal refusal = World.CanTravel(target, safePoint);
            if (refusal == TravelRefusal.UnknownPlace || refusal == TravelRefusal.NotDiscovered) return refusal;
            if (MustContinue || Dialogue != null) return TravelRefusal.Unavailable;
            return refusal;
        }

        /// <summary>
        /// Voyage rapide vers le point sûr <paramref name="safePoint"/> de la zone <paramref name="zone"/> : la session d'arrivée est
        /// préparée, puis la partie sauvegardée dans cette zone, puis la session s'ouvre au point sûr. Un refus (<see cref="CanTravel"/>) ou
        /// une sauvegarde impossible (disque plein : <see cref="TravelRefusal.SaveFailed"/>) ne change rien ; un message dit pourquoi.
        /// </summary>
        /// <summary>
        /// Sous-lot V2.28b : place d'arrivée au point sûr <paramref name="safePoint"/> (règle d'arrivée, <c>WorldSession.SafeArrival</c>) :
        /// null si sa place est libre, sinon la place libre la plus proche. Sans place libre autour de lui : null aussi (l'arrivée attend au
        /// point sûr même, et la reprise ramène à l'entrée) : un voyage ne mène jamais ailleurs qu'à sa destination.
        /// </summary>
        private GroundVector? ArrivalPlace(WorldSave save, ZoneDefinition zone, string safePoint)
        {
            (string? at, GroundVector? place) = WorldSession.SafeArrival(save, zone, _progression, safePoint, ZoneId);
            return at == safePoint ? place : null;
        }

        public TravelRefusal Travel(string zone, string safePoint)
        {
            TravelRefusal refusal = CanTravel(zone, safePoint);
            if (refusal != TravelRefusal.None)
            {
                Say("text.ui.travel." + refusal.ToString().ToLowerInvariant());
                return refusal;
            }
            ZoneDefinition target = _zones.First(z => z.Id == zone);
            WorldSave here = World.Save();
            var moved = new WorldSave(target.Id, here.WorldTick, here.Sessions, here.Consumed, here.Character, sectors: here.Sectors);
            // Comme une porte : la session d'arrivée est construite avant d'écrire quoi que ce soit ; l'aventure ne change qu'une fois les
            // deux réussies.
            WorldSession next = Build(moved, target, safePoint, null, ArrivalPlace(moved, target, safePoint));
            moved = moved.WithResume(next.Resume);   // sous-lot V2.28 : la partie relancée reprend au point sûr d'arrivée
            try
            {
                Write(moved);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
            {
                Say("text.ui.save_failed", e.Message);
                return TravelRefusal.SaveFailed;
            }
            _zone = target;
            Travels = checked(Travels + 1);
            Commit(next);
            Say("text.ui.travel.arrived", target.SafePoints.First(s => s.Id == safePoint).Name);
            AnnounceLearned();
            return TravelRefusal.None;
        }
    }
}
